import * as THREE from 'three';
import { Actors, Clock, LabFx, skirmish } from './actors';
import { baseSpec, buildBlocks, buildProp, specFromMaterial, type KitCtx, type MatSpec } from './kit';
import { ACTORS, PARADE, PROPS } from './layout';
import { buildOverlay } from './overlay';
import { syncResolution, type ArtStyle, type Pop, type StyleCtx, type StyleRuntime } from './stylekit';

// One live rendering of the diorama in one style: its own WebGL canvas, 2D
// overlay, scene, skirmish loop and clock. The lab page shows one; the
// comparison page runs several side by side with the same camera pose and
// the same clock steps.

/** Time in the skirmish loop where a shot, tracers and an explosion overlap. */
export const KEY_MOMENT = 1.78;

export const CAMERA_PRESETS = {
  tactica: { label: 'Táctica', pos: [12.2, 16.2, 20.6], target: [9, 0, 6.6] },
  accion: { label: 'Acción', pos: [1.4, 2.4, 8.6], target: [12.5, 0.9, 6.2] },
  cenital: { label: 'Cenital', pos: [9, 23, 12.5], target: [9, 0, 6.2] },
  iso: { label: 'Isométrica', pos: [21.7, 12.6, 19.2], target: [9, 0, 6.5] },
  escuadra: { label: 'Escuadra', pos: [8.6, 1.9, 9.6], target: [3.4, 0.9, 6.8] },
  aliens: { label: 'Aliens', pos: [9.2, 1.9, 4.2], target: [14, 1, 7.2] },
  desfile: { label: 'Desfile', pos: [42.1, 3.2, 20.6], target: [42.1, 0.8, 6] },
} as const;
export type PresetId = keyof typeof CAMERA_PRESETS;

/** Fonts used by the 2D overlays (loaded from Google Fonts by the page). */
export function preloadFonts(): Promise<unknown> {
  const faces = ['24px Bangers', '700 30px Caveat', '500 24px Caveat', '30px VT323', '22px DotGothic16', '30px "Archivo Black"', '700 22px Rajdhani'];
  return Promise.all(faces.map((f) => document.fonts.load(f).catch(() => undefined)));
}

/** Lets the async skirmish script react between hand-stepped frames. */
async function flush(): Promise<void> {
  for (let i = 0; i < 12; i++) await Promise.resolve();
}

function specKey(s: MatSpec): string {
  return [s.color.getHex(), s.emissive?.getHex() ?? '-', s.emissiveIntensity ?? '-', s.metalness, s.roughness, s.opacity ?? 1, s.role ?? '-', s.alien ? 1 : 0].join('|');
}

export interface ViewOptions {
  /** Upper bound for the device pixel ratio (several views at once are costly). */
  maxPixelRatio?: number;
  /** Space covered by page UI inside this view, for the 2D overlays. */
  insets?: () => { left: number; bottom: number };
}

export class LabView {
  readonly el: HTMLDivElement;
  readonly renderer: THREE.WebGLRenderer;
  readonly hudCanvas: HTMLCanvasElement;
  readonly clock = new Clock();
  style!: ArtStyle;
  paused = false;
  showUi = false;

  private readonly hud: CanvasRenderingContext2D;
  private readonly time = { value: 0 };
  private readonly persp = new THREE.PerspectiveCamera(40, 1, 0.1, 400);
  private readonly ortho = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 400);
  private camera: THREE.PerspectiveCamera | THREE.OrthographicCamera = this.persp;
  private readonly target = new THREE.Vector3(9, 0, 6.6);
  private scene = new THREE.Scene();
  private runtime: StyleRuntime = { composer: null };
  private actors: Actors | null = null;
  private fx: LabFx | null = null;
  private overlay: THREE.Group | null = null;
  private generation = 0;
  private pops: Pop[] = [];
  private lastPose = -1;
  private readonly resizeObserver: ResizeObserver;
  private size: [number, number] = [0, 0];

  constructor(parent: HTMLElement, private readonly opts: ViewOptions = {}) {
    this.el = document.createElement('div');
    this.el.className = 'lab-view';
    this.renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFShadowMap;
    this.renderer.domElement.className = 'gl';
    this.hudCanvas = document.createElement('canvas');
    this.hudCanvas.className = 'hud';
    this.hud = this.hudCanvas.getContext('2d')!;
    this.el.append(this.renderer.domElement, this.hudCanvas);
    parent.append(this.el);
    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(this.el);
  }

  get canvas(): HTMLCanvasElement {
    return this.renderer.domElement;
  }

  private get ratio(): number {
    return Math.min(window.devicePixelRatio || 1, this.opts.maxPixelRatio ?? 2);
  }

  /** Builds the whole scene for `style` and restarts the skirmish. */
  build(style: ArtStyle): void {
    this.generation++;
    this.clock.clear();
    this.clock.time = 0;
    this.disposeScene();
    this.style = style;
    this.scene = new THREE.Scene();
    const { renderer } = this;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1;
    renderer.shadowMap.enabled = true;
    renderer.setPixelRatio(style.pixelScale ? 1 / style.pixelScale : this.ratio);
    this.el.classList.toggle('pixelated', style.pixelScale > 0 && !style.pixelSmooth);
    this.camera = style.camera === 'ortho' ? this.ortho : this.persp;
    this.applyPose();
    this.pops = [];
    this.lastPose = -1;

    const ctx: StyleCtx = { renderer, scene: this.scene, camera: this.camera, noEdges: [], anchors: [], time: this.time };
    const cache = new Map<string, THREE.Material>();
    const material = (spec: MatSpec) => {
      const key = specKey(spec);
      let m = cache.get(key);
      if (!m) cache.set(key, (m = style.material(spec, ctx)));
      return m;
    };
    const kit: KitCtx = { seg: style.seg, mat: (role) => material(style.role?.(role, baseSpec(role)) ?? baseSpec(role)) };

    const props = new THREE.Group();
    for (const def of PROPS) {
      if (style.props === 'blocks') {
        const blocks = buildBlocks(def);
        if (blocks) props.add(blocks);
        continue;
      }
      const prop = buildProp(def, kit);
      props.add(style.prop ? style.prop(prop, ctx) : prop);
    }
    this.scene.add(props);
    props.updateMatrixWorld(true);
    props.traverse((o) => {
      if (o.userData.light) ctx.anchors.push({ pos: o.getWorldPosition(new THREE.Vector3()), light: o.userData.light });
    });

    this.scene.add(style.ground(ctx));
    style.environment(ctx);

    this.actors = new Actors(
      [...ACTORS, ...PARADE],
      (rig, def) => {
      if (!style.keepUnits) {
        rig.root.traverse((o) => {
          if (!(o instanceof THREE.Mesh) || o === rig.flash) return;
          const spec = specFromMaterial(o.material as THREE.Material, def.team === 'alien');
          if (spec) o.material = material(style.unitSpec?.(spec, def) ?? spec);
        });
      }
        style.rig?.(rig, def, ctx);
      },
      style.keepUnits ? 'placeholders' : 'figures',
    );
    this.scene.add(this.actors.group);

    const fx = new LabFx(this.clock, style.fxLights, style.debris ?? ((color) => new THREE.MeshStandardMaterial({ color, transparent: true })));
    fx.onEvent = (kind, at) => {
      const p = this.project(at);
      this.pops.push({ kind, x: p.x, y: p.y, born: this.clock.time, seed: Math.random() });
    };
    this.fx = fx;
    this.scene.add(fx.group);
    this.overlay = buildOverlay();
    this.overlay.visible = this.showUi;
    this.scene.add(this.overlay);
    ctx.noEdges.push(fx.group, this.overlay);

    this.runtime = style.post(ctx);
    this.resize();
    void this.loop(this.generation);
  }

  private async loop(gen: number): Promise<void> {
    const alive = () => gen === this.generation;
    await this.clock.wait(0.4);
    while (alive() && this.actors && this.fx) await skirmish(this.clock, this.actors, this.fx, alive);
  }

  private disposeScene(): void {
    this.runtime.composer?.dispose();
    this.scene.traverse((o) => {
      if (o instanceof THREE.Mesh) {
        const mats = Array.isArray(o.material) ? o.material : [o.material];
        for (const m of mats) m.dispose();
      }
    });
    this.scene.environment?.dispose();
    this.scene.clear();
  }

  // ------------------------------------------------------------- camera

  /** Copies the shared camera pose (position looking at target). */
  setPose(position: THREE.Vector3, target: THREE.Vector3): void {
    this.persp.position.copy(position);
    this.target.copy(target);
    this.applyPose();
  }

  private applyPose(): void {
    const w = this.el.clientWidth || 1;
    const h = this.el.clientHeight || 1;
    // Narrow views (side by side, phones) keep the horizontal framing of a wide one.
    const widen = Math.max(1, 1.6 / (w / h));
    this.persp.aspect = w / h;
    this.persp.fov = THREE.MathUtils.radToDeg(2 * Math.atan(Math.tan(THREE.MathUtils.degToRad(20)) * widen));
    this.persp.lookAt(this.target);
    this.persp.updateProjectionMatrix();
    // The orthographic camera shows what the perspective one would at the orbit distance.
    this.ortho.position.copy(this.persp.position);
    this.ortho.quaternion.copy(this.persp.quaternion);
    const half = this.persp.position.distanceTo(this.target) * Math.tan(THREE.MathUtils.degToRad(20)) * widen;
    this.ortho.top = half;
    this.ortho.bottom = -half;
    this.ortho.left = (-half * w) / h;
    this.ortho.right = (half * w) / h;
    this.ortho.updateProjectionMatrix();
  }

  project(p: THREE.Vector3 | [number, number, number]): { x: number; y: number } {
    const v = Array.isArray(p) ? new THREE.Vector3(...p) : p.clone();
    v.project(this.camera);
    return { x: ((v.x + 1) / 2) * this.el.clientWidth, y: ((1 - v.y) / 2) * this.el.clientHeight };
  }

  // -------------------------------------------------------------- frame

  resize(): void {
    const w = this.el.clientWidth;
    const h = this.el.clientHeight;
    if (!w || !h) return;
    this.size = [w, h];
    this.renderer.setSize(w, h, false);
    this.applyPose();
    const composer = this.runtime.composer;
    if (composer) {
      composer.setPixelRatio(this.renderer.getPixelRatio());
      composer.setSize(w, h);
      const px = this.renderer.getDrawingBufferSize(new THREE.Vector2());
      syncResolution(composer, px.x, px.y);
    }
  }

  advance(dt: number): void {
    if (!this.style || !this.actors || !this.fx) return;
    if (!this.paused) this.clock.advance(dt);
    this.time.value = this.clock.time;
    if (this.style.stepped) {
      // Hold each pose for a whole drawing, like animation on twos.
      const pose = Math.floor(this.clock.time * this.style.stepped);
      if (pose !== this.lastPose) this.actors.tick(pose / this.style.stepped);
      this.lastPose = pose;
    } else this.actors.tick(this.clock.time);
    this.fx.tick(this.paused ? 0 : dt);
    this.runtime.tick?.(this.paused ? 0 : dt, this.clock.time);
  }

  draw(): void {
    if (!this.style || !this.el.clientWidth) return;
    // Layout changes can arrive before the resize observer reports them.
    if (this.el.clientWidth !== this.size[0] || this.el.clientHeight !== this.size[1]) this.resize();
    const composer = this.runtime.composer;
    if (composer) {
      for (const p of composer.passes) {
        const u = (p as { uniforms?: Record<string, THREE.IUniform> }).uniforms;
        if (u?.time) u.time.value = this.clock.time;
      }
      composer.render();
    } else this.renderer.render(this.scene, this.camera);
    this.drawHud();
  }

  private drawHud(): void {
    const w = this.el.clientWidth;
    const h = this.el.clientHeight;
    const ratio = Math.min(window.devicePixelRatio || 1, 2);
    if (this.hudCanvas.width !== Math.round(w * ratio) || this.hudCanvas.height !== Math.round(h * ratio)) {
      this.hudCanvas.width = Math.round(w * ratio);
      this.hudCanvas.height = Math.round(h * ratio);
    }
    const g = this.hud;
    g.setTransform(1, 0, 0, 1, 0, 0);
    g.clearRect(0, 0, this.hudCanvas.width, this.hudCanvas.height);
    const now = this.clock.time;
    this.pops = this.pops.filter((p) => now - p.born < 3 && now >= p.born);
    if (!this.style.hud) return;
    const insets = this.opts.insets?.() ?? { left: 0, bottom: 0 };
    // Overlays are laid out for a full screen; small views draw them scaled down.
    const k = Math.min(1, Math.max(0.45, w / 760));
    g.setTransform(ratio * k, 0, 0, ratio * k, 0, 0);
    g.save();
    this.style.hud({
      g,
      w: w / k,
      h: h / k,
      time: now,
      pops: k === 1 ? this.pops : this.pops.map((p) => ({ ...p, x: p.x / k, y: p.y / k })),
      project: (p) => {
        const s = this.project(p);
        return { x: s.x / k, y: s.y / k };
      },
      left: insets.left / k,
      bottom: insets.bottom / k,
    });
    g.restore();
  }

  /** Advances by hand (also while the tab is hidden), letting the script react between steps. */
  async step(n: number, dt: number): Promise<void> {
    for (let i = 0; i < n; i++) {
      this.advance(dt);
      await flush();
    }
    this.draw();
  }

  /** Restarts the skirmish from the opening stance without rebuilding the scene. */
  restart(): void {
    if (!this.actors) return;
    this.generation++;
    this.clock.clear();
    this.clock.time = 0;
    this.actors.resetPoses();
    this.fx?.reset();
    this.pops = [];
    this.lastPose = -1;
    void this.loop(this.generation);
  }

  /** Restarts and freezes on the key moment (shot + explosion). */
  async keyMoment(): Promise<void> {
    this.restart();
    this.paused = false;
    const gen = this.generation;
    for (let t = 0; t < KEY_MOMENT; t += 1 / 60) {
      this.advance(1 / 60);
      await flush();
      if (gen !== this.generation) return;
    }
    this.paused = true;
  }

  /** Where a unit stands and how fast its legs are moving (tooling). */
  actorState(id: string): { x: number; z: number; run: number } | null {
    const a = this.actors?.byId.get(id);
    return a ? { x: a.rig.root.position.x, z: a.rig.root.position.z, run: a.pose.run } : null;
  }

  /** Overrides pose values of one unit (tooling: check aim, run, hit... on any model). */
  posePatch(id: string, patch: Record<string, number>): void {
    const a = this.actors?.byId.get(id);
    if (a) Object.assign(a.pose, patch);
  }

  setUi(on: boolean): void {
    this.showUi = on;
    if (this.overlay) this.overlay.visible = on;
  }

  dispose(): void {
    this.generation++;
    this.resizeObserver.disconnect();
    this.disposeScene();
    this.renderer.dispose();
    this.renderer.forceContextLoss();
    this.el.remove();
  }
}
