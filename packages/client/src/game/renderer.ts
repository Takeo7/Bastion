import * as THREE from 'three';
import { CSS2DRenderer } from 'three/addons/renderers/CSS2DRenderer.js';
import { perfBegin, perfFrame } from '../perf';
import { FrameLimiter, onGraphics, pixelRatio, postQuality, QualityGuard, type GraphicsSettings } from '../settings';
import { fitPostToPixelRatio, lightenPost, postChain, UNIT_LAYER, type PostChain } from './post';

type FrameFn = (dt: number, time: number) => void;

/** Vertical sky gradient (top to horizon) as a screen-space background. */
function skyGradient(stops: string[]): THREE.Texture {
  const canvas = document.createElement('canvas');
  canvas.width = 2;
  canvas.height = 256;
  const ctx = canvas.getContext('2d')!;
  const g = ctx.createLinearGradient(0, 0, 0, canvas.height);
  stops.forEach((c, i) => g.addColorStop(i / (stops.length - 1), c));
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
}

/** A time of day: the sky, the haze, the lights, the exposure and the grade. */
export interface Mood {
  sky: string[];
  haze: number;
  hazeDensity: number;
  sun: number;
  sunIntensity: number;
  /** From the map's middle to the sun (or the moon). */
  sunFrom: readonly [number, number, number];
  skyLight: number;
  groundLight: number;
  skyIntensity: number;
  fill: number;
  fillIntensity: number;
  exposure: number;
  bloom: { strength: number; threshold: number };
  /** The tints and lift are display values (the grade runs after tone mapping and sRGB). */
  grade: { saturation: number; contrast: number; vignette: number; shadowTint: RGB; highlightTint: RGB; lift: RGB };
}

type RGB = readonly [number, number, number];

/**
 * The battle's light, set against Synty's own promo captures (reference
 * block, /referencia.html). Day (Apocalypse's city): a low, warm sun from
 * the upper right of the default view throws long shadows down and to the
 * left; a lavender sky fills the shade and the ground bounces warm light
 * back; the grade tints the shade lavender and the light peach and lifts the
 * blacks, the captures' soft pastel. Night (Woodland's motel): a cold moon,
 * a dense teal haze, and the warm pools of the lamps (mapView). A neutral
 * tone mapping keeps the atlases' colours (ACES turned their reds orange).
 */
const MOODS: Record<'day' | 'night' | 'dawn' | 'dusk', Mood> = {
  day: {
    sky: ['#8f97b4', '#c8c0cc', '#ecd0b8'],
    // Second look against the capture (2026-10-14, measured by tone bands): the far streets read through a
    // grey-violet veil, and the frame came out darker than the capture (its middle tones a third brighter)
    // with duller, more violet shade. A thinner, warmer haze; brighter, with the shade lifted towards mauve.
    haze: 0xd4c8bc,
    hazeDensity: 0.0032,
    sun: 0xffd6b0,
    sunIntensity: 3.5,
    // About 40° up; in the default view (yaw 45°) it stands to the upper right.
    sunFrom: [5, 26, -31],
    skyLight: 0xbdb6d8,
    groundLight: 0x82674f,
    skyIntensity: 1.75,
    fill: 0xfff0e0,
    fillIntensity: 0.3,
    // Neutral against ACES and AgX on the reference block: closest to the capture's median and saturation (ACES and AgX washed it out).
    exposure: 1.34,
    bloom: { strength: 0.35, threshold: 0.92 },
    grade: { saturation: 0.9, contrast: 1.04, vignette: 0.2, shadowTint: [1.0, 0.92, 1.02], highlightTint: [1.04, 1.0, 0.95], lift: [0.075, 0.055, 0.07] },
  },
  night: {
    sky: ['#081419', '#0f2328', '#173233'],
    haze: 0x1b3035,
    hazeDensity: 0.024,
    sun: 0x9cc2e0,
    sunIntensity: 1.0,
    sunFrom: [-18, 30, -14],
    skyLight: 0x4a7480,
    groundLight: 0x161c20,
    skyIntensity: 1.7,
    fill: 0x9fb8d0,
    fillIntensity: 0.15,
    exposure: 1.2,
    // Only what shines blooms (a lamp's head, a Bengala, a fire), not the ground it lights: «la luz deslumbra mucho de noche» (Arturo 17/10).
    bloom: { strength: 0.45, threshold: 0.88 },
    grade: { saturation: 1.0, contrast: 1.06, vignette: 0.34, shadowTint: [0.9, 1.0, 1.04], highlightTint: [1.06, 1.0, 0.9], lift: [0.015, 0.03, 0.035] },
  },
  // Dawn (the trailer's opening, docs/TRAILER.md): the sun just up in the east, low and orange, its shadows
  // the length of the street; the sky blue overhead and peach at the horizon, a warm haze in the far streets.
  dawn: {
    sky: ['#56679a', '#b893a6', '#f3b487'],
    haze: 0xd8b4a2,
    hazeDensity: 0.024,
    sun: 0xffad73,
    sunIntensity: 3.0,
    sunFrom: [31, 8, -9],
    skyLight: 0x9aa4d2,
    groundLight: 0x6a5244,
    skyIntensity: 1.35,
    fill: 0xffe2cc,
    fillIntensity: 0.22,
    exposure: 1.32,
    bloom: { strength: 0.5, threshold: 0.86 },
    grade: { saturation: 0.95, contrast: 1.07, vignette: 0.3, shadowTint: [0.9, 0.93, 1.1], highlightTint: [1.08, 1.0, 0.9], lift: [0.04, 0.035, 0.065] },
  },
  // Dusk (the cover, docs/TRAILER.md): the sun low in the west, gold on what faces it and long shadows east; the
  // sky deep blue overhead and burning at the horizon; the shade a cool violet.
  dusk: {
    sky: ['#24326a', '#7d4f86', '#f08f5a', '#f6b26a', '#f6b26a'],
    haze: 0xc8927e,
    hazeDensity: 0.0085,
    sun: 0xffa45c,
    sunIntensity: 3.6,
    sunFrom: [-30, 10, 9],
    skyLight: 0x8a8fc8,
    groundLight: 0x5e4436,
    skyIntensity: 1.5,
    fill: 0xffd8bc,
    fillIntensity: 0.25,
    exposure: 1.42,
    bloom: { strength: 0.55, threshold: 0.85 },
    grade: { saturation: 1.0, contrast: 1.08, vignette: 0.32, shadowTint: [0.88, 0.9, 1.12], highlightTint: [1.1, 1.0, 0.88], lift: [0.04, 0.03, 0.07] },
  },
};

/** Winter daylight in Brena; ordinary missions keep their approved lighting. */
const SNOW_DAY: Mood = {
  ...MOODS.day,
  sky: ['#9eafc8', '#c7d6e3', '#dfebf2'],
  haze: 0xc3d4e2,
  sun: 0xeaf3ff,
  sunIntensity: 2.9,
  skyLight: 0xc1d5ed,
  groundLight: 0x899bad,
  fill: 0xeaf3ff,
  exposure: 1.18,
  grade: { ...MOODS.day.grade, shadowTint: [0.92, 0.98, 1.06], highlightTint: [1, 1, 1.02], lift: [0.04, 0.05, 0.065] },
};

/** The battle's daylight, or the snow's: for a scene that is the map but not the battle (the base's clearing, strategy/clearingArt.ts). */
export function dayMood(snow: boolean): Mood {
  return snow ? SNOW_DAY : MOODS.day;
}

/** The map changed (rebuilt): the sun's shadow map is drawn again on the next frame. */
let shadowsStale = true;
export function shadowsChanged(): void {
  shadowsStale = true;
}

/** GPU work the map asks for (baking its contact occlusion), done before the next frame is drawn. */
const jobs: ((gl: THREE.WebGLRenderer) => void)[] = [];
export function beforeNextFrame(job: (gl: THREE.WebGLRenderer) => void): void {
  jobs.push(job);
}

/**
 * For a renderer other than the battle's drawing the map (the base's clearing,
 * on the strategy stage): the map's pending GPU work done on it, and whether
 * the shadow map is to be drawn again (it is taken).
 */
export function runMapJobs(gl: THREE.WebGLRenderer): boolean {
  while (jobs.length) jobs.shift()!(gl);
  const stale = shadowsStale;
  shadowsStale = false;
  return stale;
}

/** While animated characters cast shadows and nothing else moves, the shadow map catches up with their poses this often. */
const POSE_SHADOWS = 0.1;
/** Frames between looks for what casts shadows and moves (units come and go). */
const CASTERS_EVERY = 30;
/** Casters smaller than this (radius, world units) don't call for a new shadow map when they move. */
const MIN_CASTER = 0.25;

/** The sun's shadow map by effects level: its size and how soft its edges (PCF's disk radius, in texels). */
const SHADOW_QUALITY = { high: { size: 4096, radius: 3 }, medium: { size: 2048, radius: 2 }, low: { size: 2048, radius: 1 } } as const;

/**
 * The side of the sun's shadow box over a whole 48-tile map (world units).
 * A map that fits in it is shadowed whole, as it always was; a bigger one
 * (docs/MAPAS.md §7.3) only round what the camera sees, in a box at least
 * this wide: its shadows as sharp as a 48-tile map's, whatever its size.
 */
const SHADOW_SPAN = Math.hypot(48, 48) + 4;
/** A box that follows the view grows in steps of this, and shrinks only when that much too big: fewer redraws. */
const SHADOW_STEP = 8;
/** How far past the map's edge the shadows still fall (the closures past it, the ground before the haze). */
const SHADOW_BEYOND = 8;
/** What takes shadows in view stands no higher than this (roofs and what is on them), and what casts them, than this. */
const SHADOW_TOP = 12;
const CASTER_TOP = 24;
/** The rays of the view's corners that miss the ground end this far out (looking at the horizon). */
const VIEW_REACH = 90;
/** The shadow bias, for a depth range of BIAS_SPAN (the old fixed 1 to 90): kept the same in world units whatever the range. */
const SHADOW_BIAS = -0.0004;
const BIAS_SPAN = 89;
const NORMAL_BIAS = 0.02;

/** Owns the WebGL renderer, the scene, the lights and the render loop. */
export class Renderer {
  readonly scene = new THREE.Scene();
  readonly camera = new THREE.PerspectiveCamera(40, 1, 0.1, 400);
  readonly gl: THREE.WebGLRenderer;
  readonly labels = new CSS2DRenderer();
  private readonly sun = new THREE.DirectionalLight();
  private readonly sky = new THREE.HemisphereLight();
  private mood: Mood = MOODS.day;
  /** The map's size, for the sun's shadow camera. */
  private mapSize = { width: 30, height: 30 };
  /**
   * A map bigger than SHADOW_SPAN: the sun's frame (its right and up, from
   * the sun) and what of the map's box it covers, in it; the shadow box as
   * now fitted to the view (left, bottom, side), null to fit it again.
   */
  private sunFrame: { right: THREE.Vector3; up: THREE.Vector3; x0: number; x1: number; y0: number; y1: number } | null = null;
  private shadowBox: { left: number; bottom: number; side: number } | null = null;
  private readonly corner = new THREE.Vector3();
  private readonly far = new THREE.Vector3();
  private readonly offset = new THREE.Vector3();
  /**
   * Soft light from the camera, without shadows. The sun comes from behind
   * the default camera, so without it the sides of the soldiers you see are
   * in shade and take the sky's blue.
   */
  readonly fill = new THREE.DirectionalLight();
  private readonly look = new THREE.Vector3();
  private readonly frameFns = new Set<FrameFn>();
  private readonly clock = new THREE.Timer();
  /** Post-processing: ambient occlusion in corners and contacts, bloom on lights. */
  private readonly post: PostChain;
  /** The camera close in (closeUp). */
  private near = false;
  private readonly limiter = new FrameLimiter();
  /** Lowers the quality if the mission doesn't run fluidly (playtest: «me va a 25 fps»). */
  private readonly guard = new QualityGuard(() => this.onQualityLowered?.());
  /** The guard took a step down: the game says so, once. */
  onQualityLowered?: () => void;
  private effects: GraphicsSettings['effects'] = 'high';
  private readonly unsubscribe: () => void;
  private readonly resizeObserver = new ResizeObserver(() => this.resize());
  /**
   * The shadow map is drawn only when something in it changes (240 fps budget:
   * it was a second drawing of the whole map every frame). What moves and casts
   * shadows, where it was at the last look (position and facing, and shown or not).
   */
  private casters: THREE.Object3D[] = [];
  private casterSeen = new Float32Array(0);
  private castersAge = CASTERS_EVERY;
  private sinceShadows = 0;

  constructor(private readonly container: HTMLElement) {
    this.gl = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    this.gl.shadowMap.enabled = true;
    this.gl.shadowMap.type = THREE.PCFShadowMap;
    this.gl.shadowMap.autoUpdate = false;
    this.gl.toneMapping = THREE.NeutralToneMapping;
    container.append(this.gl.domElement);

    this.labels.domElement.className = 'labels-layer';
    container.append(this.labels.domElement);

    this.scene.fog = new THREE.FogExp2(0xffffff, 0);
    this.scene.add(this.sky);
    this.sun.castShadow = true;
    this.sun.shadow.mapSize.set(2048, 2048);
    this.sun.shadow.bias = SHADOW_BIAS;
    this.sun.shadow.normalBias = NORMAL_BIAS;
    this.scene.add(this.sun, this.sun.target, this.fill, this.fill.target);

    // Characters live on their own layer (post.ts): seen and shadowed, left out of the occlusion's pre-pass.
    this.camera.layers.enable(UNIT_LAYER);
    this.post = postChain(this.gl, this.scene, this.camera);
    this.setMood(MOODS.day);

    this.resizeObserver.observe(container);
    this.unsubscribe = onGraphics((g) => this.applyGraphics(g));
    this.gl.setAnimationLoop(() => this.frame());
  }

  /** Graphics settings, live: resolution, shadows and how much post-processing (less what the guard took). */
  private applyGraphics(g: GraphicsSettings): void {
    this.effects = g.effects;
    const shadow = SHADOW_QUALITY[g.effects];
    if (this.sun.shadow.mapSize.x !== shadow.size) {
      this.sun.shadow.mapSize.set(shadow.size, shadow.size);
      this.sun.shadow.map?.dispose();
      this.sun.shadow.map = null;
    }
    this.sun.shadow.radius = shadow.radius;
    // Its texels are another size: snapped again.
    this.shadowBox = null;
    shadowsStale = true;
    const q = postQuality();
    this.post.ao.enabled = q.ao !== 'off' && !this.near;
    lightenPost(this.post, q.ao === 'light', q.bloom === 'light');
    if (this.gl.shadowMap.enabled !== g.shadows) {
      this.gl.shadowMap.enabled = g.shadows;
      this.sun.castShadow = g.shadows;
      // Materials compile with or without shadow lookups: rebuild them.
      this.scene.traverse((o) => {
        const m = (o as THREE.Mesh).material;
        for (const mat of Array.isArray(m) ? m : m ? [m] : []) mat.needsUpdate = true;
      });
    }
    this.resize();
  }

  dispose(): void {
    this.unsubscribe();
    this.resizeObserver.disconnect();
    this.gl.setAnimationLoop(null);
    this.post.composer.dispose();
    this.gl.dispose();
    // dispose() leaves the WebGL context alive until the page goes: one per mission, and Chrome keeps about 16.
    this.gl.forceContextLoss();
  }

  /** Day or night (the reference block's night test; missions are by day). */
  setNight(on: boolean, snow = false): void {
    this.setMood(on ? MOODS.night : snow ? SNOW_DAY : MOODS.day);
  }

  /**
   * Close in on the characters (the trailer's cinema camera): no ambient
   * occlusion. The characters stay out of its pre-pass (skinned, they would
   * stand in it in their bind pose), and from close what is behind them shows
   * through them in it.
   */
  set closeUp(on: boolean) {
    this.near = on;
    this.post.ao.enabled = postQuality().ao !== 'off' && !on;
  }

  /** The time of day by name: also dawn, for the trailer. */
  setTime(time: keyof typeof MOODS): void {
    this.setMood(MOODS[time]);
  }

  private setMood(m: Mood): void {
    this.mood = m;
    (this.scene.background as THREE.Texture | null)?.dispose();
    this.scene.background = skyGradient(m.sky);
    const fog = this.scene.fog as THREE.FogExp2;
    fog.color.set(m.haze);
    fog.density = m.hazeDensity;
    this.sun.color.set(m.sun);
    this.sun.intensity = m.sunIntensity;
    this.sky.color.set(m.skyLight);
    this.sky.groundColor.set(m.groundLight);
    this.sky.intensity = m.skyIntensity;
    this.fill.color.set(m.fill);
    this.fill.intensity = m.fillIntensity;
    this.gl.toneMappingExposure = m.exposure;
    this.post.bloom.strength = m.bloom.strength;
    this.post.bloom.threshold = m.bloom.threshold;
    const g = this.post.grade;
    g.saturation.value = m.grade.saturation;
    g.contrast.value = m.grade.contrast;
    g.vignette.value = m.grade.vignette;
    g.shadowTint.value.setRGB(...m.grade.shadowTint, THREE.LinearSRGBColorSpace);
    g.highlightTint.value.setRGB(...m.grade.highlightTint, THREE.LinearSRGBColorSpace);
    g.lift.value.setRGB(...m.grade.lift, THREE.LinearSRGBColorSpace);
    this.fitMap(this.mapSize.width, this.mapSize.height);
  }

  /**
   * Points the sun at the map's middle, and its shadow camera at the whole
   * map; on a map bigger than SHADOW_SPAN, at what the camera sees (fitShadow),
   * from as near and as far as anything on the map stands.
   */
  fitMap(width: number, height: number): void {
    this.mapSize = { width, height };
    const cx = width / 2;
    const cz = height / 2;
    const r = Math.hypot(width, height) / 2 + 2;
    const from = this.mood.sunFrom;
    this.sun.position.set(cx + from[0], from[1], cz + from[2]);
    this.sun.target.position.set(cx, 0, cz);
    const cam = this.sun.shadow.camera;
    this.shadowBox = null;
    shadowsStale = true;
    if (r * 2 <= SHADOW_SPAN + 1e-6) {
      this.sunFrame = null;
      cam.left = -r;
      cam.right = r;
      cam.top = r;
      cam.bottom = -r;
      cam.near = 1;
      cam.far = 90;
      cam.updateProjectionMatrix();
      this.sun.shadow.bias = SHADOW_BIAS;
      this.sun.shadow.normalBias = NORMAL_BIAS;
      return;
    }
    // The sun's frame as its shadow camera has it (it looks at the target, up the world's up).
    const look = this.sun.target.position.clone().sub(this.sun.position).normalize();
    const right = new THREE.Vector3(0, 1, 0).cross(look.clone().negate()).normalize();
    const up = look.clone().negate().cross(right);
    // The map's box (and a little beyond), from the sun: across, what the box may cover; along, how near and far.
    const frame = { right, up, x0: Infinity, x1: -Infinity, y0: Infinity, y1: -Infinity };
    let near = Infinity;
    let far = -Infinity;
    for (const x of [-SHADOW_BEYOND, width + SHADOW_BEYOND]) {
      for (const z of [-SHADOW_BEYOND, height + SHADOW_BEYOND]) {
        for (const y of [-1, CASTER_TOP]) {
          const p = this.offset.set(x, y, z).sub(this.sun.position);
          const depth = p.dot(look);
          near = Math.min(near, depth);
          far = Math.max(far, depth);
          if (y > SHADOW_TOP) continue;
          frame.x0 = Math.min(frame.x0, p.dot(right));
          frame.x1 = Math.max(frame.x1, p.dot(right));
          frame.y0 = Math.min(frame.y0, p.dot(up));
          frame.y1 = Math.max(frame.y1, p.dot(up));
        }
      }
    }
    this.sunFrame = frame;
    cam.near = near - 1;
    cam.far = far + 1;
    cam.updateProjectionMatrix();
    this.sun.shadow.bias = (SHADOW_BIAS * BIAS_SPAN) / (cam.far - cam.near);
  }

  /**
   * On a map bigger than SHADOW_SPAN: the shadow box round what the camera
   * sees (its view's corners down to the ground and up to SHADOW_TOP, in the
   * sun's frame, kept to the map), SHADOW_SPAN wide or more, its edges on the
   * shadow map's texels (no shimmer as it moves). Fitted again only when the
   * view leaves it, or it is much too big: each time, the shadow map is drawn
   * again; in between, it is kept (updateShadows).
   */
  private fitShadow(): void {
    const frame = this.sunFrame;
    if (!frame) return;
    const camera = this.camera;
    camera.updateMatrixWorld();
    const origin = this.sun.position;
    let x0 = Infinity;
    let x1 = -Infinity;
    let y0 = Infinity;
    let y1 = -Infinity;
    const take = (p: THREE.Vector3) => {
      const o = this.offset.copy(p).sub(origin);
      const x = o.dot(frame.right);
      const y = o.dot(frame.up);
      x0 = Math.min(x0, x);
      x1 = Math.max(x1, x);
      y0 = Math.min(y0, y);
      y1 = Math.max(y1, y);
    };
    for (const [sx, sy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]] as const) {
      const a = this.corner.set(sx, sy, -1).unproject(camera);
      const b = this.far.set(sx, sy, 1).unproject(camera).sub(a).normalize();
      // Where this corner's ray meets the ground and SHADOW_TOP (or gives up), and the near corner if under SHADOW_TOP.
      for (const level of [0, SHADOW_TOP]) {
        const t = b.y < -1e-3 ? (level - a.y) / b.y : -1;
        if (t >= 0) take(this.offset.copy(b).multiplyScalar(Math.min(t, VIEW_REACH)).add(a));
        else if (level === 0) take(this.offset.copy(b).multiplyScalar(VIEW_REACH).add(a));
      }
      if (a.y < SHADOW_TOP) take(a);
    }
    // Only as far as the map goes.
    x0 = Math.max(x0, frame.x0);
    x1 = Math.min(x1, frame.x1);
    y0 = Math.max(y0, frame.y0);
    y1 = Math.min(y1, frame.y1);
    if (x1 < x0 || y1 < y0) return;
    const need = Math.max(x1 - x0, y1 - y0) + 2;
    const side = SHADOW_SPAN + Math.ceil(Math.max(0, need - SHADOW_SPAN) / SHADOW_STEP) * SHADOW_STEP;
    const box = this.shadowBox;
    if (box && box.side >= side && box.side <= side + SHADOW_STEP && x0 >= box.left && x1 <= box.left + box.side && y0 >= box.bottom && y1 <= box.bottom + box.side) return;
    const texel = side / this.sun.shadow.mapSize.x;
    const left = Math.round(((x0 + x1) / 2 - side / 2) / texel) * texel;
    const bottom = Math.round(((y0 + y1) / 2 - side / 2) / texel) * texel;
    this.shadowBox = { left, bottom, side };
    const cam = this.sun.shadow.camera;
    cam.left = left;
    cam.right = left + side;
    cam.bottom = bottom;
    cam.top = bottom + side;
    cam.updateProjectionMatrix();
    // Texels as big as a 48-tile map's, or bigger: the normal offset grows with them.
    this.sun.shadow.normalBias = (NORMAL_BIAS * side) / SHADOW_SPAN;
    shadowsStale = true;
  }

  /**
   * Asks for the shadow map to be drawn when it would change: the map was
   * rebuilt, a caster moved, turned, appeared or went, or (POSE_SHADOWS) a
   * character's pose went on. Static meshes (matrixAutoUpdate off) only change
   * with a rebuild, so only the others are watched.
   */
  private updateShadows(dt: number): void {
    const map = this.gl.shadowMap;
    if (!map.enabled) return;
    if (++this.castersAge >= CASTERS_EVERY) {
      this.castersAge = 0;
      const casters: THREE.Object3D[] = [];
      this.scene.traverse((o) => {
        if (!(o as THREE.Mesh).isMesh || !o.castShadow || !o.matrixAutoUpdate) return;
        // Bodies, heads, weapons; not trinkets that spin all the time (Velmar's orbiting tiles): their shadows go along when the map is drawn.
        const g = (o as THREE.Mesh).geometry;
        if (!g.boundingSphere) g.computeBoundingSphere();
        if ((g.boundingSphere?.radius ?? 1) * o.matrixWorld.getMaxScaleOnAxis() >= MIN_CASTER) casters.push(o);
      });
      if (casters.length !== this.casters.length) shadowsStale = true;
      this.casters = casters;
      if (this.casterSeen.length !== casters.length * 5) this.casterSeen = new Float32Array(casters.length * 5);
    }
    // Moved since the shadows were drawn: further than a breath (idle poses go by POSE_SHADOWS), turned, shown or hidden.
    let moved = false;
    let posing = false;
    const seen = this.casterSeen;
    for (let i = 0; i < this.casters.length && !moved; i++) {
      const c = this.casters[i]!;
      let shown = c.visible;
      for (let p = c.parent; shown && p; p = p.parent) shown = p.visible;
      const e = c.matrixWorld.elements;
      const k = i * 5;
      if (Math.abs(seen[k]! - e[12]!) + Math.abs(seen[k + 1]! - e[13]!) + Math.abs(seen[k + 2]! - e[14]!) > 0.03) moved = true;
      if (Math.abs(seen[k + 3]! - (e[0]! + e[2]! * 2)) > 0.02 || seen[k + 4] !== (shown ? 1 : 0)) moved = true;
      if (shown && (c as THREE.SkinnedMesh).isSkinnedMesh) posing = true;
    }
    this.sinceShadows += dt;
    if (shadowsStale || moved || (posing && this.sinceShadows >= POSE_SHADOWS)) {
      map.needsUpdate = true;
      shadowsStale = false;
      this.sinceShadows = 0;
      // Where everything is as these shadows are drawn.
      for (let i = 0; i < this.casters.length; i++) {
        const c = this.casters[i]!;
        let shown = c.visible;
        for (let p = c.parent; shown && p; p = p.parent) shown = p.visible;
        const e = c.matrixWorld.elements;
        seen[i * 5] = e[12]!;
        seen[i * 5 + 1] = e[13]!;
        seen[i * 5 + 2] = e[14]!;
        seen[i * 5 + 3] = e[0]! + e[2]! * 2;
        seen[i * 5 + 4] = shown ? 1 : 0;
      }
    }
  }

  onFrame(fn: FrameFn): () => void {
    this.frameFns.add(fn);
    return () => this.frameFns.delete(fn);
  }

  private resize(): void {
    const w = this.container.clientWidth || window.innerWidth;
    const h = this.container.clientHeight || window.innerHeight;
    this.gl.setPixelRatio(pixelRatio(w, h, true));
    this.gl.setSize(w, h);
    this.post.composer.setPixelRatio(this.gl.getPixelRatio());
    fitPostToPixelRatio(this.post, this.gl.getPixelRatio());
    this.post.composer.setSize(w, h);
    this.labels.setSize(w, h);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  /** Draws one frame as the player sees it: scene with post-processing, then the CSS2D labels. */
  render(dt = 0): void {
    while (jobs.length) jobs.shift()!(this.gl);
    this.fitShadow();
    this.updateShadows(dt);
    this.camera.getWorldDirection(this.look);
    this.fill.position.copy(this.camera.position);
    this.fill.target.position.copy(this.camera.position).add(this.look);
    if (this.effects === 'low') this.gl.render(this.scene, this.camera);
    else this.post.composer.render(dt);
    this.labels.render(this.scene, this.camera);
  }

  private frame(): void {
    // With a frame-rate cap, skipped ticks neither simulate nor draw: dt spans them.
    const now = performance.now();
    if (!this.limiter.ready(now)) return;
    perfBegin(this.gl);
    this.guard.frame(now);
    this.clock.update();
    const dt = Math.min(this.clock.getDelta(), 0.1);
    const time = this.clock.getElapsed();
    for (const fn of this.frameFns) fn(dt, time);
    this.render(dt);
    perfFrame(this.gl, 'combate');
  }
}
