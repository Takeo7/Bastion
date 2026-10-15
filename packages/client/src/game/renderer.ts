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
interface Mood {
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
    bloom: { strength: 0.85, threshold: 0.7 },
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

/** While animated characters cast shadows and nothing else moves, the shadow map catches up with their poses this often. */
const POSE_SHADOWS = 0.1;
/** Frames between looks for what casts shadows and moves (units come and go). */
const CASTERS_EVERY = 30;
/** Casters smaller than this (radius, world units) don't call for a new shadow map when they move. */
const MIN_CASTER = 0.25;

/** The sun's shadow map by effects level: its size and how soft its edges (PCF's disk radius, in texels). */
const SHADOW_QUALITY = { high: { size: 4096, radius: 3 }, medium: { size: 2048, radius: 2 }, low: { size: 2048, radius: 1 } } as const;

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
    this.sun.shadow.bias = -0.0004;
    this.sun.shadow.normalBias = 0.02;
    this.scene.add(this.sun, this.sun.target, this.fill, this.fill.target);

    // Characters live on their own layer (post.ts): seen and shadowed, left out of the occlusion's pre-pass.
    this.camera.layers.enable(UNIT_LAYER);
    this.post = postChain(this.gl, this.scene, this.camera);
    this.setMood(MOODS.day);

    new ResizeObserver(() => this.resize()).observe(container);
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
    this.gl.setAnimationLoop(null);
    this.post.composer.dispose();
    this.gl.dispose();
  }

  /** Day or night (the reference block's night test; missions are by day). */
  setNight(on: boolean): void {
    this.setMood(on ? MOODS.night : MOODS.day);
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

  /** Points the sun and its shadow camera at the whole map. */
  fitMap(width: number, height: number): void {
    this.mapSize = { width, height };
    const cx = width / 2;
    const cz = height / 2;
    const r = Math.hypot(width, height) / 2 + 2;
    const from = this.mood.sunFrom;
    this.sun.position.set(cx + from[0], from[1], cz + from[2]);
    this.sun.target.position.set(cx, 0, cz);
    const cam = this.sun.shadow.camera;
    cam.left = -r;
    cam.right = r;
    cam.top = r;
    cam.bottom = -r;
    cam.near = 1;
    cam.far = 90;
    cam.updateProjectionMatrix();
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
