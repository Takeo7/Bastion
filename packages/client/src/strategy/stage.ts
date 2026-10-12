import * as THREE from 'three';
import { CSS2DRenderer } from 'three/addons/renderers/CSS2DRenderer.js';
import { fitPostToPixelRatio, voxelPost, type VoxelPost } from '../game/post';
import { FrameLimiter, onGraphics, pixelRatio, type GraphicsSettings } from '../settings';
import { ambience } from '../audio/ambience';
import { music } from '../audio/music';
import { easeInOut, tween } from '../game/tween';

/** A camera placement: where it stands, what it looks at, and its field of view. */
export interface Shot {
  pos: THREE.Vector3;
  target: THREE.Vector3;
  fov?: number;
  /**
   * Keep this box whole in the part of the screen the panels leave free: the
   * camera keeps its direction and moves along it and sideways until it fits.
   */
  fit?: THREE.Box3;
  /**
   * The wheel (or a trackpad pinch) zooms in, towards the pointer, down to
   * `min` times the shot's distance; zoomed in, a drag pans, up to `reach`
   * world units from the shot's target.
   */
  zoom?: { min: number; reach: number };
}

/** Panels of the screens over the stage: a fitted shot keeps clear of them. */
const PANELS = '.strat-top, .strat-nav, .strat-left, .strat-right';

/** One 3D set of the strategy layer (the Bastion cutaway, the geoscape). */
export interface StageSet {
  readonly scene: THREE.Scene;
  tick(dt: number, time: number): void;
  /** Objects the pointer can hit; picks walk up to the first ancestor with `userData.hotspot`. */
  hits(): THREE.Object3D[];
  pick?(hotspot: string): void;
  hover?(hotspot: string | null): void;
  /** Drag with the pointer; return true when the set used it (e.g. spinning the globe). */
  drag?(dx: number, dy: number): boolean;
  /** Screen-space ambient occlusion (voxel art): on for solid sets like the Bastion, off for the globe. */
  readonly ambientOcclusion?: boolean;
}

/**
 * Full-screen 3D stage behind the strategy screens. Shots within the same set
 * are camera moves; switching sets cuts through black, XCOM-style.
 */
export class Stage {
  readonly element = document.createElement('div');
  readonly camera = new THREE.PerspectiveCamera(40, 1, 0.1, 600);
  private readonly gl = new THREE.WebGLRenderer({ antialias: true });
  private readonly labels = new Map<string, CSS2DRenderer>();
  private readonly fade = document.createElement('div');
  private readonly sets = new Map<string, StageSet>();
  private readonly target = new THREE.Vector3();
  private readonly clock = new THREE.Timer();
  private readonly resizeObserver: ResizeObserver;
  private readonly raycaster = new THREE.Raycaster();
  private active = '';
  /** Whether the set shown picks the music and ambience (the base does; the menu's backdrop does not). */
  soundtrack = true;
  private frameId = 0;
  private moveId = 0;
  private drag: { x: number; y: number; moved: boolean } | null = null;
  private disposed = false;
  /** Voxel post-processing; it follows whichever set is active. */
  private readonly post: VoxelPost;
  private readonly limiter = new FrameLimiter();
  private effects: GraphicsSettings['effects'] = 'high';
  private readonly unsubscribe: () => void;
  /** The shot shown, as asked for and as fitted to the screen. */
  private asked: Shot | null = null;
  private shot: Shot | null = null;
  /** A camera move between shots is playing: the zoom keeps its hands off. */
  private moving = false;
  /** Zoom within a shot: distance factor and target offset, and where they are heading. */
  private readonly zoom = { factor: 1, goal: 1, offset: new THREE.Vector3(), offsetGoal: new THREE.Vector3() };

  constructor() {
    this.element.className = 'stage';
    this.fade.className = 'stage-fade';
    this.gl.toneMapping = THREE.ACESFilmicToneMapping;
    this.gl.toneMappingExposure = 1.15;
    this.gl.shadowMap.enabled = true;
    this.gl.shadowMap.type = THREE.PCFShadowMap;
    this.post = voxelPost(this.gl, new THREE.Scene(), this.camera);
    this.element.append(this.gl.domElement, this.fade);
    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(this.element);
    const canvas = this.gl.domElement;
    canvas.addEventListener('pointerdown', (e) => (this.drag = { x: e.clientX, y: e.clientY, moved: false }));
    canvas.addEventListener('pointermove', (e) => this.onMove(e));
    // Not passive: a pinch on a trackpad would zoom the whole page otherwise.
    canvas.addEventListener('wheel', (e) => this.onWheel(e), { passive: false });
    window.addEventListener('pointerup', this.onUp);
    this.unsubscribe = onGraphics((g) => this.applyGraphics(g));
    this.loop();
  }

  /** Graphics settings, live: resolution, shadows and how much post-processing. */
  private applyGraphics(g: GraphicsSettings): void {
    this.effects = g.effects;
    if (this.gl.shadowMap.enabled !== g.shadows) {
      this.gl.shadowMap.enabled = g.shadows;
      for (const set of this.sets.values()) {
        set.scene.traverse((o) => {
          const m = (o as THREE.Mesh).material;
          for (const mat of Array.isArray(m) ? m : m ? [m] : []) mat.needsUpdate = true;
        });
      }
    }
    this.resize();
  }

  add(id: string, set: StageSet): void {
    this.sets.set(id, set);
    const labels = new CSS2DRenderer();
    labels.domElement.className = 'stage-labels';
    labels.domElement.style.display = 'none';
    this.element.insertBefore(labels.domElement, this.fade);
    this.labels.set(id, labels);
    this.resize();
  }

  get current(): string {
    return this.active;
  }

  /** Moves the camera to `shot` in set `id`, cutting through black when the set changes. Any zoom is let go. */
  async show(id: string, asked: Shot, instant = false): Promise<void> {
    const move = ++this.moveId;
    this.asked = asked;
    this.resetZoom();
    if (id !== this.active) {
      this.moving = true;
      if (this.active && !instant) {
        this.fade.classList.add('on');
        await new Promise((r) => setTimeout(r, 260));
        if (move !== this.moveId) return;
      }
      for (const [key, labels] of this.labels) labels.domElement.style.display = key === id ? '' : 'none';
      this.active = id;
      if (this.soundtrack) {
        music.play(id === 'geo' ? 'geo' : 'base');
        ambience.play(id === 'geo' ? 'geo' : 'base');
      }
      this.place((this.shot = this.fitted(asked)));
      this.moving = false;
      this.fade.classList.remove('on');
      return;
    }
    const shot = (this.shot = this.fitted(asked));
    if (instant) {
      this.place(shot);
      return;
    }
    const fromPos = this.camera.position.clone();
    const fromTarget = this.target.clone();
    const fromFov = this.camera.fov;
    this.moving = true;
    await tween(0.9, (t) => {
      if (move !== this.moveId) return;
      const k = easeInOut(t);
      this.camera.position.lerpVectors(fromPos, shot.pos, k);
      this.target.lerpVectors(fromTarget, shot.target, k);
      this.camera.fov = fromFov + ((shot.fov ?? 40) - fromFov) * k;
      this.camera.updateProjectionMatrix();
      this.camera.lookAt(this.target);
    });
    if (move === this.moveId) this.moving = false;
  }

  /**
   * How far the screen's panels reach in from each edge of the stage: a bar
   * touching the top or the bottom edge takes that side, a sheet the side its
   * middle is on.
   */
  private insets(): { top: number; right: number; bottom: number; left: number } {
    const out = { top: 0, right: 0, bottom: 0, left: 0 };
    const box = this.element.getBoundingClientRect();
    for (const el of this.element.parentElement?.querySelectorAll<HTMLElement>(PANELS) ?? []) {
      const r = el.getBoundingClientRect();
      if (r.width < 4 || r.height < 4) continue;
      if (r.top <= box.top + 2) out.top = Math.max(out.top, r.bottom - box.top);
      else if (r.bottom >= box.bottom - 2) out.bottom = Math.max(out.bottom, box.bottom - r.top);
      else if (r.left + r.width / 2 < box.left + box.width / 2) out.left = Math.max(out.left, r.right - box.left);
      else out.right = Math.max(out.right, box.right - r.left);
    }
    return out;
  }

  /**
   * A shot with `fit` moved along its view and sideways until its box fills
   * the free part of the screen (playtest: «las instalaciones se cortan un
   * poco por abajo»). A few rounds of measuring the projected box converge.
   */
  private fitted(shot: Shot): Shot {
    const w = this.element.clientWidth;
    const h = this.element.clientHeight;
    if (!shot.fit || !w || !h) return shot;
    const margin = 12;
    const inset = this.insets();
    let [left, right, top, bottom] = [inset.left + margin, inset.right + margin, inset.top + margin, inset.bottom + margin];
    // On a small window the panels may cover most of it: the box keeps at least half each way.
    const squeeze = (a: number, b: number, size: number) => (a + b > size / 2 ? (size / 2) / (a + b) : 1);
    const kx = squeeze(left, right, w);
    const ky = squeeze(top, bottom, h);
    [left, right, top, bottom] = [left * kx, right * kx, top * ky, bottom * ky];
    const free = { x0: (left / w) * 2 - 1, x1: 1 - (right / w) * 2, y0: (bottom / h) * 2 - 1, y1: 1 - (top / h) * 2 };
    const camera = new THREE.PerspectiveCamera(shot.fov ?? 40, w / h, 0.1, 1000);
    const back = shot.pos.clone().sub(shot.target);
    let distance = back.length();
    back.normalize();
    const target = shot.target.clone();
    const { min, max } = shot.fit;
    const corners = [0, 1, 2, 3, 4, 5, 6, 7].map((i) => new THREE.Vector3(i & 1 ? max.x : min.x, i & 2 ? max.y : min.y, i & 4 ? max.z : min.z));
    const p = new THREE.Vector3();
    const right3 = new THREE.Vector3();
    const up3 = new THREE.Vector3();
    for (let round = 0; round < 12; round++) {
      camera.position.copy(target).addScaledVector(back, distance);
      camera.lookAt(target);
      camera.updateMatrixWorld();
      let [x0, x1, y0, y1] = [Infinity, -Infinity, Infinity, -Infinity];
      for (const c of corners) {
        p.copy(c).project(camera);
        [x0, x1, y0, y1] = [Math.min(x0, p.x), Math.max(x1, p.x), Math.min(y0, p.y), Math.max(y1, p.y)];
      }
      const half = Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)) * distance;
      right3.setFromMatrixColumn(camera.matrixWorld, 0);
      up3.setFromMatrixColumn(camera.matrixWorld, 1);
      target.addScaledVector(right3, ((x0 + x1) / 2 - (free.x0 + free.x1) / 2) * half * camera.aspect);
      target.addScaledVector(up3, ((y0 + y1) / 2 - (free.y0 + free.y1) / 2) * half);
      distance *= Math.max((x1 - x0) / (free.x1 - free.x0), (y1 - y0) / (free.y1 - free.y0));
    }
    return { ...shot, pos: target.clone().addScaledVector(back, distance), target };
  }

  private resetZoom(): void {
    this.zoom.factor = this.zoom.goal = 1;
    this.zoom.offset.set(0, 0, 0);
    this.zoom.offsetGoal.set(0, 0, 0);
  }

  /** Where the pointer meets the ground (y = 0), or null. */
  private groundAt(e: MouseEvent): THREE.Vector3 | null {
    const rect = this.gl.domElement.getBoundingClientRect();
    this.raycaster.setFromCamera(new THREE.Vector2(((e.clientX - rect.left) / rect.width) * 2 - 1, -((e.clientY - rect.top) / rect.height) * 2 + 1), this.camera);
    return this.raycaster.ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 1, 0), 0), new THREE.Vector3());
  }

  /** The wheel, or a trackpad pinch (a wheel with ctrl held): zooms towards the pointer. */
  private onWheel(e: WheelEvent): void {
    const range = this.shot?.zoom;
    if (!range || this.moving) return;
    e.preventDefault();
    const z = this.zoom;
    const delta = e.deltaY * (e.deltaMode === 1 ? 16 : 1);
    const goal = THREE.MathUtils.clamp(z.goal * Math.exp(delta * (e.ctrlKey ? 0.01 : 0.0015)), range.min, 1);
    // The point under the pointer stays under it: the target scales about it as the distance does.
    const point = this.groundAt(e);
    if (point && z.goal > 0) {
      const target = this.shot!.target.clone().add(z.offsetGoal);
      target.sub(point).multiplyScalar(goal / z.goal).add(point);
      z.offsetGoal.copy(target.sub(this.shot!.target));
    }
    z.goal = goal;
    this.clampOffset();
  }

  /** Zoomed in, a drag pans; zoomed all the way out, the shot is as it was. */
  private pan(dx: number, dy: number): boolean {
    const range = this.shot?.zoom;
    if (!range || this.moving || this.zoom.goal >= 0.999) return false;
    const distance = this.camera.position.distanceTo(this.target);
    const perPixel = (2 * distance * Math.tan(THREE.MathUtils.degToRad(this.camera.fov / 2))) / Math.max(1, this.element.clientHeight);
    const look = this.camera.getWorldDirection(new THREE.Vector3());
    const ahead = new THREE.Vector3(look.x, 0, look.z).normalize();
    const side = new THREE.Vector3(-ahead.z, 0, ahead.x);
    // Ground seen at an angle is foreshortened along the view.
    const along = perPixel / Math.max(0.3, -look.y);
    this.zoom.offsetGoal.addScaledVector(side, -dx * perPixel).addScaledVector(ahead, dy * along);
    this.clampOffset();
    return true;
  }

  /** The further out, the closer to the shot's own target: fully out, exactly on it. */
  private clampOffset(): void {
    const range = this.shot?.zoom;
    if (!range) return;
    const limit = (range.reach * (1 - this.zoom.goal)) / Math.max(0.01, 1 - range.min);
    const o = this.zoom.offsetGoal;
    o.set(THREE.MathUtils.clamp(o.x, -limit, limit), THREE.MathUtils.clamp(o.y, -limit, limit), THREE.MathUtils.clamp(o.z, -limit, limit));
  }

  /** Eases the camera to the zoom asked for, within the shot shown. */
  private applyZoom(dt: number): void {
    const shot = this.shot;
    if (!shot?.zoom || this.moving) return;
    const z = this.zoom;
    if (z.factor === z.goal && z.offset.equals(z.offsetGoal)) return;
    const k = 1 - Math.exp(-dt * 12);
    z.factor += (z.goal - z.factor) * k;
    z.offset.lerp(z.offsetGoal, k);
    if (Math.abs(z.factor - z.goal) < 1e-4 && z.offset.distanceToSquared(z.offsetGoal) < 1e-8) {
      z.factor = z.goal;
      z.offset.copy(z.offsetGoal);
    }
    this.target.copy(shot.target).add(z.offset);
    this.camera.position.copy(shot.pos).sub(shot.target).multiplyScalar(z.factor).add(this.target);
    this.camera.lookAt(this.target);
  }

  /** Fades the stage to black (e.g. before leaving for a mission). */
  blackout(): void {
    this.fade.classList.add('on');
  }

  private place(shot: Shot): void {
    this.camera.position.copy(shot.pos);
    this.target.copy(shot.target);
    this.camera.fov = shot.fov ?? 40;
    this.camera.updateProjectionMatrix();
    this.camera.lookAt(this.target);
  }

  /** Screen position (pixels in the stage) of a world point, for anchoring DOM to 3D. */
  project(p: THREE.Vector3): { x: number; y: number } {
    const v = p.clone().project(this.camera);
    return { x: ((v.x + 1) / 2) * this.element.clientWidth, y: ((1 - v.y) / 2) * this.element.clientHeight };
  }

  private hotspotAt(e: PointerEvent): string | null {
    const set = this.sets.get(this.active);
    if (!set) return null;
    const rect = this.gl.domElement.getBoundingClientRect();
    const ndc = new THREE.Vector2(((e.clientX - rect.left) / rect.width) * 2 - 1, -((e.clientY - rect.top) / rect.height) * 2 + 1);
    this.raycaster.setFromCamera(ndc, this.camera);
    let o: THREE.Object3D | null = this.raycaster.intersectObjects(set.hits(), true)[0]?.object ?? null;
    while (o && !o.userData.hotspot) o = o.parent;
    return (o?.userData.hotspot as string | undefined) ?? null;
  }

  private onMove(e: PointerEvent): void {
    const set = this.sets.get(this.active);
    if (this.drag && (e.buttons & 1) !== 0) {
      const dx = e.clientX - this.drag.x;
      const dy = e.clientY - this.drag.y;
      if (Math.abs(dx) + Math.abs(dy) > 3) this.drag.moved = true;
      if (this.drag.moved && (set?.drag?.(dx, dy) || this.pan(dx, dy))) {
        this.drag.x = e.clientX;
        this.drag.y = e.clientY;
        return;
      }
    }
    const hot = this.hotspotAt(e);
    set?.hover?.(hot);
    this.gl.domElement.style.cursor = hot ? 'pointer' : set?.drag || this.zoom.goal < 0.999 ? 'grab' : 'default';
  }

  private readonly onUp = (e: PointerEvent): void => {
    const drag = this.drag;
    this.drag = null;
    if (!drag || drag.moved || e.target !== this.gl.domElement) return;
    const hot = this.hotspotAt(e);
    if (hot) this.sets.get(this.active)?.pick?.(hot);
  };

  private resize(): void {
    const w = this.element.clientWidth;
    const h = this.element.clientHeight;
    if (!w || !h) return;
    this.gl.setPixelRatio(pixelRatio(w, h));
    this.gl.setSize(w, h);
    this.post.composer.setPixelRatio(this.gl.getPixelRatio());
    fitPostToPixelRatio(this.post, this.gl.getPixelRatio());
    this.post.composer.setSize(w, h);
    for (const labels of this.labels.values()) labels.setSize(w, h);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    // A fitted shot fits the new size (from where it is zoomed out).
    if (this.asked?.fit && !this.moving) {
      this.resetZoom();
      this.place((this.shot = this.fitted(this.asked)));
    }
  }

  private loop = (): void => {
    if (this.disposed) return;
    this.frameId = requestAnimationFrame(this.loop);
    const set = this.sets.get(this.active);
    // Hidden (display: none) or detached: nothing to draw.
    if (!set || !this.element.isConnected || !this.element.clientWidth) return;
    if (!this.limiter.ready()) return;
    this.clock.update();
    const dt = Math.min(this.clock.getDelta(), 0.1);
    this.applyZoom(dt);
    set.tick(dt, this.clock.getElapsed());
    this.post.scene.scene = set.scene;
    this.post.ao.scene = set.scene;
    this.post.ao.enabled = !!set.ambientOcclusion && this.effects === 'high';
    if (this.effects === 'low') this.gl.render(set.scene, this.camera);
    else this.post.composer.render(dt);
    this.labels.get(this.active)?.render(set.scene, this.camera);
  };

  dispose(): void {
    this.disposed = true;
    this.unsubscribe();
    cancelAnimationFrame(this.frameId);
    this.resizeObserver.disconnect();
    window.removeEventListener('pointerup', this.onUp);
    this.post.composer.dispose();
    this.gl.dispose();
    this.element.remove();
  }
}
