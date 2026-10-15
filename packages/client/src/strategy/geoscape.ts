import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { BUS, HOME_REGION, LANDMARKS, MAREA_SOURCE, REGIONS, roadRoute, underMarea, type CampaignState, type MissionOffer, type RegionId } from '@bastion/engine';
import type { Shot, Stage, StageMood, StageSet } from './stage';
import { ATALAYA, D, FIELD, HD, HW, mareaReach, mareaRoads, roadLine, threshold, VALLEY_LANDMARKS, W, ZONES } from './valleyGeography';
import './valley.css';

// «El valle»: the map of the valley of Kessra (docs/LORE.md, «El mundo»), the
// painting Arturo chose (15/10, «este va a ser el mapa»): the valley seen from
// above, its towns, roads, river and lakes, Atalaya's works in the north-west,
// Galen's city in the east, Los Llanos' great works in the south. Where the
// zones, the roads and the places lie on it is valleyGeography.ts. Round the
// painting, the painting itself mirrored and out of focus, so no screen ever
// shows its edge. Live over it, only the Marea (the violet storm, its bright
// edge climbing the roads zone by zone as the zombi activity grows: the
// engine's thresholds), the zones' markers (contact, Nidos, operations,
// rumours), La Lata on the roads and the names (HTML, valley.css). The wheel
// zooms towards the pointer, a drag pans, and the valley always fits the part
// of the screen the panels leave. The same set is the main menu's backdrop,
// with the Marea a strip to the south.

/** The painting (tools/valle/mapa.ts). */
const MAP_URL = 'maps/valle/mapa.jpg';
/** How far round the painting its blurred mirror reaches (map units), and its last stretch fading into the table: past any screen. */
const AROUND = { x: 16, z: 12, fade: 2 };
/** The valley: what fits the free part of the screen when zoomed out, and what covers it when zoomed in (a hair inside the painting's edge). */
const VALLEY = new THREE.Box3(new THREE.Vector3(-HW + 0.15, 0, -HD + 0.15), new THREE.Vector3(HW - 0.15, 0, HD - 0.15));
/** The closest zoom, in times the whole valley: the painting is 1536 pixels wide, a little over a screen; past twice that it goes soft. */
const ZOOM_MAX = 2;
/** The free part of the screen keeps this much round the valley when zoomed out (pixels). */
const MARGIN = 12;
/** What lies round the map, far out: the panels' steel. */
const TABLE = new THREE.Color(0x1b1e20);
const GROUND = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
/** Where La Lata parks at a zone, from its pin: below it, clear of the name and the badges. */
const PARK = { x: 0.3, z: 0.72 };

// ------------------------------------------------------------- the Marea

/** The Marea's uniforms: where it reaches, the activity drawn, the clock and the screen's pixel. */
const MAREA = {
  field: { value: null as THREE.Texture | null },
  level: { value: 0 },
  time: { value: 0 },
  pixel: { value: 1 },
};

/** The activity at which the Marea reaches each point (valleyGeography.ts mareaReach), over the valley. */
function mareaField(): THREE.Texture {
  if (MAREA.field.value) return MAREA.field.value;
  const roads = mareaRoads();
  const data = new Uint16Array(FIELD.w * FIELD.d);
  for (let j = 0; j < FIELD.d; j++)
    for (let i = 0; i < FIELD.w; i++) {
      const v = mareaReach(-HW + ((i + 0.5) / FIELD.w) * W, -HD + ((j + 0.5) / FIELD.d) * D, roads);
      data[j * FIELD.w + i] = THREE.DataUtils.toHalfFloat(Math.min(v, 40));
    }
  const texture = new THREE.DataTexture(data, FIELD.w, FIELD.d, THREE.RedFormat, THREE.HalfFloatType);
  texture.magFilter = texture.minFilter = THREE.LinearFilter;
  texture.needsUpdate = true;
  return (MAREA.field.value = texture);
}

const f1 = (n: number) => n.toFixed(2);
const linear = (c: THREE.Color) => `vec3(${f1(c.r)}, ${f1(c.g)}, ${f1(c.b)})`;

const GROUND_VERTEX = /* glsl */ `
  varying vec2 vMap;
  void main() {
    vMap = position.xz;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }`;

/**
 * The painting, and the Marea over it: where the activity has reached, the
 * ground bled of colour and stained violet, a veil drifting over it; at its
 * edge a glow and a bright line (a few pixels whatever the zoom). Out of the
 * painting, the painting mirrored (the texture repeats mirrored) and ever more
 * out of focus (a coarser level of its mipmaps), a little dimmer, until it
 * fades into the table far out.
 */
const GROUND_FRAGMENT = /* glsl */ `
  uniform sampler2D map;
  uniform sampler2D field;
  uniform float level;
  uniform float time;
  uniform float pixel;
  varying vec2 vMap;

  float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
  }
  float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
  }

  void main() {
    vec2 uv = (vMap + vec2(${f1(HW)}, ${f1(HD)})) / vec2(${f1(W)}, ${f1(D)});
    // How far out of the painting (map units): there, out of focus and dimmer.
    vec2 beyond = max(max(-uv, uv - 1.0), 0.0) * vec2(${f1(W)}, ${f1(D)});
    float out_ = length(beyond);
    vec3 c = texture2D(map, uv, clamp(out_ * 1.6, 0.0, 6.5)).rgb;
    float far = smoothstep(0.0, 6.0, out_);
    c = mix(c, vec3(dot(c, vec3(0.3, 0.55, 0.15))), 0.25 * far) * (1.0 - 0.22 * far);
    // Where it reaches, the field read a little askew: its edge wavers the same everywhere, steep or not.
    vec2 warp = (vec2(noise(vMap * 0.8 + 3.1), noise(vMap * 0.8 + 9.7)) - 0.5) * 0.7 + (vec2(noise(vMap * 2.7 + 1.3), noise(vMap * 2.7 + 5.9)) - 0.5) * 0.22;
    vec2 fuv = (vMap + warp + vec2(${f1(HW)}, ${f1(HD)})) / vec2(${f1(W)}, ${f1(D)});
    float d = texture2D(field, fuv).r - level;
    // Screen pixels from the edge (CSS ones), negative inside.
    float px = d / max(fwidth(d), 1e-5) / pixel;
    vec2 drift = vMap * 0.7 + vec2(time * 0.05, time * 0.018);
    float veil = noise(drift) * 0.6 + noise(drift * 2.3 + 7.0) * 0.4;
    vec3 under = c * vec3(0.305, 0.127, 0.429);
    under = mix(under, vec3(0.205, 0.028, 0.352), 0.2);
    under = mix(under, vec3(0.515, 0.19, 0.79), 0.16 * smoothstep(0.45, 0.9, veil));
    c = mix(c, under, smoothstep(0.6, -0.6, px));
    float flicker = 0.85 + 0.15 * sin(time * 1.7 + vMap.x * 1.1 + vMap.y * 0.7);
    c += vec3(0.79, 0.155, 1.0) * (exp(-abs(px) / 7.0) * 0.36 + exp(-abs(px) / 22.0) * 0.12) * flicker;
    c = mix(c, vec3(0.97, 0.72, 1.0), 1.0 - smoothstep(0.7, 1.7, abs(px)));
    vec2 e = vec2(${f1(HW + AROUND.x)}, ${f1(HD + AROUND.z)}) - abs(vMap);
    c = mix(${linear(TABLE)}, c, smoothstep(0.0, ${f1(AROUND.fade)}, min(e.x, e.y)));
    gl_FragColor = vec4(c, 1.0);
    #include <tonemapping_fragment>
    #include <colorspace_fragment>
  }`;

// ------------------------------------------------------------- markers

/** An operation's colour, as the list of operations has it (screen.ts). */
const offerTone = (o: MissionOffer) => (o.final ? '#ff5a8a' : o.facility ? '#ff4a5a' : o.story ? '#4a7dff' : o.quest ? '#2f9e6a' : '#ffb030');

function el(tag: string, className: string, text?: string): HTMLElement {
  const e = document.createElement(tag);
  e.className = className;
  if (text) e.textContent = text;
  return e;
}

interface Marker {
  region: RegionId;
  label: CSS2DObject;
  /** What the pointer hits: the pin and the name over it, sized in screen pixels each frame. */
  hit: THREE.Mesh;
  name: HTMLElement;
  /** The name's width in pixels, once it is on the page. */
  width: number;
}

const HIT_SHAPE = new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2);
const HIT = new THREE.MeshBasicMaterial({ visible: false });

/** No tone mapping (and, drawn plain, no bloom or grade): the map as it was painted. */
const MAP_MOOD: StageMood = {
  exposure: 1,
  toneMapping: THREE.NoToneMapping,
  bloom: { strength: 0, threshold: 1 },
  grade: { saturation: 1, contrast: 1, vignette: 0, shadowTint: [1, 1, 1], highlightTint: [1, 1, 1], lift: [0, 0, 0] },
};

/** A rectangle of the ground (world units). */
interface Rect {
  x0: number;
  x1: number;
  z0: number;
  z1: number;
}

/**
 * The valley map: the eight zones with their markers, the Marea, La Lata on
 * the roads. The wheel zooms, a drag pans, a click on a zone selects it.
 */
export class GeoscapeSet implements StageSet {
  readonly scene = new THREE.Scene();
  readonly ambientOcclusion = false;
  /** No post-processing: the map is shown as painted, its glow in its own shader. */
  readonly plain = true;
  readonly mood = MAP_MOOD;
  /** The map, scaled (zoom) and moved (pan) under the still camera; its children are in map units. */
  private readonly map = new THREE.Group();
  /** The painting and what lies round it, one plane; drawn once its image is in. */
  private readonly ground: THREE.Mesh;
  /** The painting decoded and waiting to go to the GPU (beforeRender). */
  private upload: THREE.Texture | null = null;
  private readonly markers: Marker[] = [];
  private readonly landmarks: CSS2DObject[] = [];
  private readonly atalaya: CSS2DObject;
  private readonly bus: CSS2DObject;
  private readonly busName: HTMLElement;
  private readonly scanRing: THREE.Mesh;
  private stage: Stage | null = null;
  /** The zombi activity the Marea is drawn at, easing to the campaign's; in the main menu, only its strip along the south. */
  private level = threshold(MAREA_SOURCE) - 0.8;
  private levelGoal = this.level;
  private levelKnown = false;
  /**
   * The map's scale, and where it is heading: the scale at which the valley
   * fits the free part of the screen (measured all along, so it follows the
   * panels), times the zoom asked for (1 to ZOOM_MAX).
   */
  private scale = 1;
  private scaleGoal = 1;
  private fit = 1;
  private zoomGoal = 1;
  private readonly pan = new THREE.Vector2();
  private readonly panGoal = new THREE.Vector2();
  /** Whether the view has been measured yet (the first frame takes the pan as it is, without easing). */
  private placed = false;
  /** The ground the screen shows, and the part of it the panels leave free (world units), measured each frame. */
  private readonly screen: Rect = { x0: 0, x1: 0, z0: 0, z1: 0 };
  private readonly free: Rect = { x0: 0, x1: 0, z0: 0, z1: 0 };
  /**
   * What the valley is fitted to: the free part as it is with no sheet open
   * on the right, and the left panel as wide as when it was last unfolded (on
   * a small screen it folds away while a sheet is open). Opening a sheet
   * leaves the map's size alone: it only moves aside (keepInView).
   */
  private readonly fitArea: Rect = { x0: 0, x1: 0, z0: 0, z1: 0 };
  private fitLeft: number | null = null;
  /** How far the map has moved aside for a zone picked while a sheet is open (back to nothing when it closes). */
  private readonly aside = new THREE.Vector2();
  private insets = { top: 0, right: 0, bottom: 0, left: 0 };
  private frame = 0;
  private pixel = 1;
  /** A zone asked to be kept in view, while the panels settle after it is picked. */
  private focusing: { region: RegionId; until: number } | null = null;
  /** A spot asked to be framed (look()), once the view is measured. */
  private looking: { x: number; z: number; scale: number } | null = null;
  private clock = 0;
  /** La Lata's way to its zone (map units), how far along it is, and where it is now. */
  private path: THREE.Vector2[] = [];
  private along = 0;
  private readonly busAt = new THREE.Vector2();
  private busRegion: RegionId = HOME_REGION;
  private moving = false;
  /** In the main menu La Lata drives from zone to zone, stopping a while at each. */
  private idle = 0;
  private inCampaign = false;
  private scanning = 0;
  private lastKey = '';
  private hovered: string | null = null;
  private readonly ray = new THREE.Raycaster();
  private readonly ndc = new THREE.Vector2();
  private readonly point = new THREE.Vector3();

  constructor(private readonly onPick: (region: RegionId) => void) {
    this.scene.background = TABLE;
    mareaField();
    this.scene.add(this.map);
    this.ground = new THREE.Mesh(new THREE.PlaneGeometry(W + 2 * AROUND.x, D + 2 * AROUND.z).rotateX(-Math.PI / 2), HIT);
    this.ground.visible = false;
    this.ground.frustumCulled = false;
    this.map.add(this.ground);
    this.load();

    // The scan's ring spreads from La Lata.
    this.scanRing = new THREE.Mesh(
      new THREE.RingGeometry(0.3, 0.36, 48).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0x54d1ff, transparent: true, opacity: 0, depthTest: false, depthWrite: false, toneMapped: false }),
    );
    this.scanRing.renderOrder = 10;
    this.map.add(this.scanRing);

    // Atalaya, the Bastion, under the peaks.
    const home = el('div', 'valley-place');
    home.append(el('i', 'valley-home'), el('b', 'valley-name', LANDMARKS.atalaya.toUpperCase()));
    this.atalaya = new CSS2DObject(home);
    this.atalaya.position.set(ATALAYA.x, 0, ATALAYA.z);
    this.atalaya.visible = false;
    this.map.add(this.atalaya);

    // The places one knows the valley by (Lore's names).
    for (const l of VALLEY_LANDMARKS) {
      const e = el('div', 'valley-landmark');
      e.append(el('span', '', LANDMARKS[l.key]));
      const label = new CSS2DObject(e);
      label.position.set(l.at[0], 0, l.at[1]);
      label.visible = false;
      this.landmarks.push(label);
      this.map.add(label);
    }

    const bus = el('div', 'valley-bus');
    this.busName = el('b', '', BUS.name);
    this.busName.hidden = true;
    bus.append(el('i', ''), this.busName);
    this.bus = new CSS2DObject(bus);
    this.map.add(this.bus);
    this.placeBus(HOME_REGION);
  }

  attach(stage: Stage): void {
    this.stage = stage;
  }

  shot(): Shot {
    // Straight down (a hair to the south, so north is up); the map itself is fitted to the panels (fitView), not the camera.
    const target = VALLEY.getCenter(new THREE.Vector3());
    return { pos: target.clone().add(new THREE.Vector3(0, 40, 0.02)), target, fov: 36 };
  }

  /** Keeps a zone in the free part of the screen (moving the map as little as it takes), while the panels open for it. */
  focus(region: RegionId): void {
    // Timed from the map's next frame (it may be asked from another view), the panels measured afresh.
    this.focusing = { region, until: -1 };
    this.frame = 0;
  }

  /** Frames the map's point (x, z) in the middle of the free part of the screen, at `scale` times the whole map (labs, the trailer). */
  look(x: number, z: number, scale = 1): void {
    this.looking = { x, z, scale };
    this.focusing = null;
  }

  /** La Lata drives to `region` (missions take it there), along the roads. */
  flyTo(region: RegionId): void {
    this.inCampaign = true;
    this.drive(region);
  }

  /** Scanning: a ring pulses out from La Lata. */
  setScanning(on: boolean): void {
    this.scanning = on ? 1.6 : 0;
  }

  update(c: CampaignState, selected: RegionId | null): void {
    this.inCampaign = true;
    // The Marea at the campaign's activity: shown as it is the first time, then creeping to each new extent.
    this.levelGoal = c.doom;
    if (!this.levelKnown) this.level = c.doom;
    this.levelKnown = true;
    const key = JSON.stringify([c.regions, c.offers.map((o) => [o.id, o.region, o.kind, o.facility, o.final, o.story, o.quest]), c.rumors.map((x) => [x.id, x.region]), c.listening, selected, c.doom]);
    if (key === this.lastKey) return;
    this.lastKey = key;
    for (const m of this.markers) {
      this.map.remove(m.label, m.hit);
      m.label.element.remove();
    }
    this.markers.length = 0;
    for (const r of c.regions) {
      const at = ZONES[r.id];
      const drowned = underMarea(c, r.id);
      const root = el('div', `valley-zone${r.contacted ? ' contacted' : ''}${r.facility ? ' nest' : ''}${drowned ? ' marea' : ''}${r.id === selected ? ' selected' : ''}`);
      const name = el('b', 'valley-name', REGIONS[r.id].name.toUpperCase());
      const badges = el('span', 'valley-badges');
      // A Nido first, then the operations in the list's colours and icons, then a rumour (brighter while the Bastion listens).
      if (r.facility) badges.append(el('i', 'valley-nest'));
      for (const o of c.offers.filter((x) => x.region === r.id)) {
        const icon = el('span', 'mission-icon');
        icon.dataset.kind = o.kind;
        icon.style.setProperty('--icon', offerTone(o));
        icon.append(el('span', ''));
        badges.append(icon);
      }
      const rumor = c.rumors.find((x) => x.region === r.id);
      if (rumor) badges.append(el('i', `valley-rumor${rumor.id === c.listening ? ' listening' : ''}`, '?'));
      root.append(el('i', 'valley-pin'), name, badges);
      const label = new CSS2DObject(root);
      label.position.set(at.x, 0, at.z);
      const hit = new THREE.Mesh(HIT_SHAPE, HIT);
      hit.userData.hotspot = r.id;
      this.map.add(label, hit);
      this.markers.push({ region: r.id, label, hit, name, width: 0 });
    }
    if (this.hovered) this.hover(this.hovered);
  }

  hits(): THREE.Object3D[] {
    return this.markers.map((m) => m.hit);
  }

  pick(hot: string): void {
    this.onPick(hot as RegionId);
  }

  hover(hot: string | null): void {
    this.hovered = hot;
    for (const m of this.markers) m.label.element.classList.toggle('hovered', m.region === hot);
  }

  /** A drag pans the map with the pointer (the ground under it stays under it). */
  drag(dx: number, dy: number): boolean {
    const perPixel = this.worldPerPixel();
    this.panGoal.x += dx * perPixel;
    this.panGoal.y += dy * perPixel;
    this.clampPan();
    this.pan.copy(this.panGoal);
    this.focusing = null;
    return true;
  }

  /** The wheel zooms about the point under the pointer. */
  zoom(factor: number, at: THREE.Vector3 | null): void {
    const g = at ? new THREE.Vector2(at.x, at.z) : new THREE.Vector2((this.free.x0 + this.free.x1) / 2, (this.free.z0 + this.free.z1) / 2);
    const local = g.clone().sub(this.panGoal).divideScalar(this.scaleGoal);
    this.zoomGoal = THREE.MathUtils.clamp(this.zoomGoal / factor, 1, ZOOM_MAX);
    this.scaleGoal = this.fit * this.zoomGoal;
    this.panGoal.copy(g).addScaledVector(local, -this.scaleGoal);
    this.clampPan();
    this.focusing = null;
  }

  /** Before each frame: the Marea as this map has it, and the painting sent to the GPU once decoded. */
  beforeRender(gl: THREE.WebGLRenderer): void {
    MAREA.level.value = this.level;
    MAREA.time.value = this.clock;
    MAREA.pixel.value = this.pixel = gl.getPixelRatio();
    if (this.upload) {
      gl.initTexture(this.upload);
      this.upload = null;
      this.ground.visible = true;
    }
  }

  tick(dt: number, time: number): void {
    this.clock = time;
    this.measure();
    this.fitView();
    if (this.looking && this.free.x1 > this.free.x0) {
      const l = this.looking;
      this.zoomGoal = THREE.MathUtils.clamp(l.scale, 1, ZOOM_MAX);
      this.scaleGoal = this.fit * this.zoomGoal;
      this.panGoal.set((this.free.x0 + this.free.x1) / 2 - l.x * this.scaleGoal, (this.free.z0 + this.free.z1) / 2 - l.z * this.scaleGoal);
      this.looking = null;
    }
    // The map eases to its zoom and pan; the first frame takes them as they are.
    if (this.focusing) {
      if (this.focusing.until < 0) this.focusing.until = time + 0.8;
      this.keepInView(this.focusing.region);
      if (time > this.focusing.until) this.focusing = null;
    }
    this.clampPan();
    const k = this.placed ? 1 - Math.exp(-dt * 12) : 1;
    if (this.free.x1 > this.free.x0) this.placed = true;
    this.scale += (this.scaleGoal - this.scale) * k;
    this.pan.lerp(this.panGoal, k);
    if (Math.abs(this.scale - this.scaleGoal) < 1e-4) this.scale = this.scaleGoal;
    if (this.pan.distanceToSquared(this.panGoal) < 1e-8) this.pan.copy(this.panGoal);
    this.map.scale.set(this.scale, 1, this.scale);
    this.map.position.set(this.pan.x, 0, this.pan.y);
    // The Marea creeps up the roads, or back down them, to where the zombi activity puts it.
    this.level += (this.levelGoal - this.level) * (1 - Math.exp(-dt * 1.2));
    if (Math.abs(this.levelGoal - this.level) < 1e-3) this.level = this.levelGoal;
    this.labels();
    this.driveBus(dt, time);
  }

  // ------------------------------------------------------------- inner

  /** The ground the screen shows and the part the panels leave (world units), from the stage's camera. */
  private measure(): void {
    const stage = this.stage;
    if (!stage) return;
    const w = stage.element.clientWidth;
    const h = stage.element.clientHeight;
    if (!w || !h) return;
    // The panels are measured every few frames: they move rarely, and reading them costs a layout.
    if (this.frame++ % 6 === 0) this.insets = stage.insets();
    const camera = stage.camera;
    camera.updateMatrixWorld();
    const i = this.insets;
    const corners = (x0: number, x1: number, y0: number, y1: number, out: Rect) => {
      out.x0 = out.z0 = Infinity;
      out.x1 = out.z1 = -Infinity;
      for (const [x, y] of [[x0, y0], [x1, y0], [x0, y1], [x1, y1]] as const) {
        this.ray.setFromCamera(this.ndc.set(x, y), camera);
        const p = this.ray.ray.intersectPlane(GROUND, this.point);
        if (!p) continue;
        out.x0 = Math.min(out.x0, p.x);
        out.x1 = Math.max(out.x1, p.x);
        out.z0 = Math.min(out.z0, p.z);
        out.z1 = Math.max(out.z1, p.z);
      }
    };
    corners(-1, 1, -1, 1, this.screen);
    // On a small window the panels may cover most of it: the free part keeps at least half each way (as stage.ts fits).
    const squeeze = (a: number, b: number, size: number) => (a + b > size / 2 ? size / 2 / (a + b) : 1);
    const kx = squeeze(i.left, i.right, w);
    const ky = squeeze(i.top, i.bottom, h);
    corners((i.left * kx * 2) / w - 1, 1 - (i.right * kx * 2) / w, (i.bottom * ky * 2) / h - 1, 1 - (i.top * ky * 2) / h, this.free);
    if (i.right <= 0) {
      this.fitLeft = i.left;
      this.aside.set(0, 0);
    }
    const left = this.fitLeft ?? i.left;
    const kf = squeeze(left, 0, w);
    corners((left * kf * 2) / w - 1, 1, (i.bottom * ky * 2) / h - 1, 1 - (i.top * ky * 2) / h, this.fitArea);
  }

  /** World units a screen pixel. */
  private worldPerPixel(): number {
    const w = this.stage?.element.clientWidth ?? 0;
    return w ? (this.screen.x1 - this.screen.x0) / w : 0.03;
  }

  /**
   * The scale at which the valley fits the part of the screen the panels
   * leave (fitArea, measured all along: they slide in as a screen opens): the
   * zoom keeps its factor over it, and what is in the middle of the free part
   * stays there.
   */
  private fitView(): void {
    const f = this.free;
    const a = this.fitArea;
    if (!(f.x1 > f.x0) || !(a.x1 > a.x0)) return;
    const m = MARGIN * this.worldPerPixel();
    const size = VALLEY.getSize(new THREE.Vector3());
    const fit = Math.max(0.05, Math.min((a.x1 - a.x0 - 2 * m) / size.x, (a.z1 - a.z0 - 2 * m) / size.z));
    if (Math.abs(fit - this.fit) < 1e-6 && this.placed) return;
    const middle = new THREE.Vector2((f.x0 + f.x1) / 2, (f.z0 + f.z1) / 2);
    const at = middle.clone().sub(this.panGoal).divideScalar(this.scaleGoal);
    this.fit = fit;
    this.scaleGoal = fit * this.zoomGoal;
    this.panGoal.copy(middle).addScaledVector(at, -this.scaleGoal);
  }

  /**
   * The pan the map may have at its zoom: where the valley is bigger than the
   * free part of the screen, covering it; where smaller (zoomed out), in the
   * middle of the part it is fitted to, moved aside for a zone picked.
   */
  private clampPan(): void {
    const f = this.free;
    const a = this.fitArea;
    if (!(f.x1 > f.x0) || !(a.x1 > a.x0)) return;
    const s = this.scaleGoal;
    const c = VALLEY.getCenter(new THREE.Vector3());
    const axis = (v: number, lo: number, hi: number, middle: number) => (lo <= hi ? THREE.MathUtils.clamp(v, lo, hi) : middle);
    this.panGoal.x = axis(this.panGoal.x, f.x1 - s * VALLEY.max.x, f.x0 - s * VALLEY.min.x, (a.x0 + a.x1) / 2 - s * c.x + this.aside.x);
    this.panGoal.y = axis(this.panGoal.y, f.z1 - s * VALLEY.max.z, f.z0 - s * VALLEY.min.z, (a.z0 + a.z1) / 2 - s * c.z + this.aside.y);
  }

  /** Moves the map just enough for a zone to lie well inside the free part of the screen. */
  private keepInView(region: RegionId): void {
    const f = this.free;
    if (!(f.x1 > f.x0)) return;
    const z = ZONES[region];
    const mx = (f.x1 - f.x0) * 0.2;
    const mz = (f.z1 - f.z0) * 0.2;
    const x = this.panGoal.x + z.x * this.scaleGoal;
    const y = this.panGoal.y + z.z * this.scaleGoal;
    const dx = Math.max(0, f.x0 + mx - x) - Math.max(0, x - (f.x1 - mx));
    const dy = Math.max(0, f.z0 + mz - y) - Math.max(0, y - (f.z1 - mz));
    this.panGoal.x += dx;
    this.panGoal.y += dy;
    // Kept while the sheet stays open (clampPan), so the zone does not slide back under it.
    if (this.insets.right > 0) this.aside.x += dx;
    if (this.insets.right > 0) this.aside.y += dy;
  }

  /** The painting, fetched and decoded off the main thread, its mipmaps (the blur round it) made on the GPU. */
  private load(): void {
    fetch(`${import.meta.env.BASE_URL}${MAP_URL}`)
      .then((r) => (r.ok ? r.blob() : Promise.reject(new Error(`${r.status}`))))
      .then((blob) => createImageBitmap(blob))
      .then((bitmap) => {
        const texture = new THREE.Texture(bitmap);
        texture.flipY = false;
        texture.colorSpace = THREE.SRGBColorSpace;
        texture.wrapS = texture.wrapT = THREE.MirroredRepeatWrapping;
        texture.anisotropy = 4;
        texture.needsUpdate = true;
        this.ground.material = new THREE.ShaderMaterial({
          uniforms: { map: { value: texture }, ...MAREA },
          vertexShader: GROUND_VERTEX,
          fragmentShader: GROUND_FRAGMENT,
          depthTest: false,
          depthWrite: false,
          toneMapped: false,
        });
        this.upload = texture;
      })
      .catch((e: Error) => console.warn(`[valle] No se ha podido cargar el mapa (${e.message}).`));
  }

  /** The names: what the pointer hits sized to them; the small places only once the map is big enough for them. */
  private labels(): void {
    const perPixel = this.worldPerPixel() / this.scale;
    for (const m of this.markers) {
      if (!m.width && m.name.isConnected) m.width = m.name.offsetWidth;
      const z = ZONES[m.region];
      // From under the pin to over the name.
      m.hit.scale.set(Math.max(44, m.width + 16) * perPixel, 1, 48 * perPixel);
      m.hit.position.set(z.x, 0, z.z - 14 * perPixel);
    }
    const big = this.inCampaign && 1 / perPixel >= 30;
    for (const l of this.landmarks) l.visible = big;
    this.atalaya.visible = this.inCampaign;
  }

  /** La Lata along the roads to `region`, from wherever it is. */
  private drive(region: RegionId): void {
    if (region === this.busRegion && !this.moving) return;
    const route = this.route(this.busRegion, region);
    this.busRegion = region;
    if (!route.length) return this.placeBus(region);
    this.path = route;
    this.along = 0;
    this.moving = true;
  }

  private driveBus(dt: number, time: number): void {
    if (this.moving && this.path.length > 1) {
      // Along its road at a steady pace, never more than a few seconds a trip.
      let length = 0;
      for (let i = 1; i < this.path.length; i++) length += this.path[i]!.distanceTo(this.path[i - 1]!);
      this.along = Math.min(length, this.along + dt * Math.max(3.6, length / 3));
      let left = this.along;
      for (let i = 1; i < this.path.length; i++) {
        const a = this.path[i - 1]!;
        const b = this.path[i]!;
        const d = a.distanceTo(b);
        if (left <= d || i === this.path.length - 1) {
          this.busAt.lerpVectors(a, b, d > 0 ? Math.min(1, left / d) : 1);
          break;
        }
        left -= d;
      }
      if (this.along >= length) this.moving = false;
    } else if (!this.inCampaign && (this.idle += dt) > 3) {
      this.idle = 0;
      const next = REGIONS[this.busRegion].roads;
      this.drive(next[Math.floor(Math.random() * next.length)]!);
    }
    this.bus.position.set(this.busAt.x, 0, this.busAt.y);
    this.busName.hidden = !this.inCampaign;
    // The scan's ring spreads from La Lata.
    const pulse = this.scanning > 0 ? (time * 1.2) % 1 : 0;
    this.scanRing.position.copy(this.bus.position);
    this.scanRing.scale.setScalar(1 + pulse * 6);
    (this.scanRing.material as THREE.MeshBasicMaterial).opacity = this.scanning > 0 ? 0.9 * (1 - pulse) : 0;
    if (this.scanning > 0) this.scanning = Math.max(0, this.scanning - dt);
  }

  /** La Lata parked at a zone, beside its marker. */
  private placeBus(region: RegionId): void {
    const z = ZONES[region];
    this.busAt.set(z.x + PARK.x, z.z + PARK.z);
    this.moving = false;
  }

  /** The engine's road route from one zone to another (roadRoute), as one line; empty if there is none. */
  private route(from: RegionId, to: RegionId): THREE.Vector2[] {
    const zones = roadRoute(from, to);
    if (zones.length < 2 || zones[0] !== from) return [];
    const points: THREE.Vector2[] = [this.busAt.clone()];
    for (let i = 0; i + 1 < zones.length; i++) for (const p of roadLine(zones[i]!, zones[i + 1]!)) points.push(new THREE.Vector2(p.x, p.z));
    // Off the road to its parking beside the marker.
    const end = ZONES[to];
    points.push(new THREE.Vector2(end.x + PARK.x, end.z + PARK.z));
    return points;
  }
}
