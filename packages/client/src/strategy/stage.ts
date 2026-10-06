import * as THREE from 'three';
import { CSS2DRenderer } from 'three/addons/renderers/CSS2DRenderer.js';
import { voxelPost, type VoxelPost } from '../game/post';
import { FrameLimiter, onGraphics, pixelRatio, type GraphicsSettings } from '../settings';
import { easeInOut, tween } from '../game/tween';

/** A camera placement: where it stands, what it looks at, and its field of view. */
export interface Shot {
  pos: THREE.Vector3;
  target: THREE.Vector3;
  fov?: number;
}

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
  private frameId = 0;
  private moveId = 0;
  private drag: { x: number; y: number; moved: boolean } | null = null;
  private disposed = false;
  /** Voxel post-processing; it follows whichever set is active. */
  private readonly post: VoxelPost;
  private readonly limiter = new FrameLimiter();
  private effects: GraphicsSettings['effects'] = 'high';
  private readonly unsubscribe: () => void;

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
    this.gl.setPixelRatio(pixelRatio());
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

  /** Moves the camera to `shot` in set `id`, cutting through black when the set changes. */
  async show(id: string, shot: Shot, instant = false): Promise<void> {
    const move = ++this.moveId;
    if (id !== this.active) {
      if (this.active && !instant) {
        this.fade.classList.add('on');
        await new Promise((r) => setTimeout(r, 260));
        if (move !== this.moveId) return;
      }
      for (const [key, labels] of this.labels) labels.domElement.style.display = key === id ? '' : 'none';
      this.active = id;
      this.place(shot);
      this.fade.classList.remove('on');
      return;
    }
    if (instant) {
      this.place(shot);
      return;
    }
    const fromPos = this.camera.position.clone();
    const fromTarget = this.target.clone();
    const fromFov = this.camera.fov;
    await tween(0.9, (t) => {
      if (move !== this.moveId) return;
      const k = easeInOut(t);
      this.camera.position.lerpVectors(fromPos, shot.pos, k);
      this.target.lerpVectors(fromTarget, shot.target, k);
      this.camera.fov = fromFov + ((shot.fov ?? 40) - fromFov) * k;
      this.camera.updateProjectionMatrix();
      this.camera.lookAt(this.target);
    });
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
      if (this.drag.moved && set?.drag?.(dx, dy)) {
        this.drag.x = e.clientX;
        this.drag.y = e.clientY;
        return;
      }
    }
    const hot = this.hotspotAt(e);
    set?.hover?.(hot);
    this.gl.domElement.style.cursor = hot ? 'pointer' : set?.drag ? 'grab' : 'default';
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
    this.gl.setSize(w, h);
    this.post.composer.setPixelRatio(this.gl.getPixelRatio());
    this.post.composer.setSize(w, h);
    for (const labels of this.labels.values()) labels.setSize(w, h);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
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
