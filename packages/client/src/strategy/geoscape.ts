import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { activeOffer, BUS, HOME_REGION, LANDMARKS, MAREA_SOURCE, REGIONS, roadRoute, underMarea, type CampaignState, type MissionOffer, type RegionId } from '@bastion/engine';
import type { Shot, Stage, StageMood, StageSet } from './stage';
import { ATALAYA_WORKS, BASTION, D, FIELD, HD, HW, mareaReach, mareaRoads, roadLine, threshold, VALLEY_LANDMARKS, W, ZONES } from './valleyGeography';
import './valley.css';

// «El valle»: the map of the valley of Kessra (docs/LORE.md, «El mundo»), the
// painting Arturo chose (15/10, «este va a ser el mapa»): the valley seen from
// above, its towns, roads, river and lakes, Atalaya's works in the north-west,
// Galen's city in the east, Los Llanos' great works in the south. Where the
// zones, the roads and the places lie on it is valleyGeography.ts. Live over
// it, only the Marea (the violet storm, its bright edge climbing the roads
// zone by zone as the zombi activity grows: the engine's thresholds), the
// zones' markers (contact, Nidos, operations, rumours), La Lata on the roads
// and the names (HTML, valley.css). The painting is always seen whole, fitted
// to the part of the screen the panels leave, the table round it (playtest
// 15/10: no blur, nothing mirrored round it, no zoom). A zone picked while a
// sheet is open over the map is kept clear of it: the map slides aside as far
// as it takes, its size as it was, and back when the sheet closes. The same
// set is the main menu's backdrop, with the Marea a strip to the south.

/** The painting (tools/valle/mapa.ts). */
const MAP_URL = 'maps/valle/mapa.jpg';
/** The painting, decoded: the menu and each visit to the base fetched and decoded (and kept) one of their own. */
let painting: Promise<ImageBitmap> | null = null;
/** The valley: the painting's whole, what fits the free part of the screen. */
const VALLEY = new THREE.Box3(new THREE.Vector3(-HW, 0, -HD), new THREE.Vector3(HW, 0, HD));
/** The free part of the screen keeps this much round the valley (pixels). */
const MARGIN = 12;
/** How far a picked zone keeps from a sheet's edge (pixels): room for its name and its badges. */
const CLEAR = 150;
/** What lies round the map: the panels' steel. */
const TABLE = new THREE.Color(0x1b1e20);
const GROUND = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
/** In the main menu, how long La Lata takes from one zone to the next, and stops at each (seconds). */
const MENU_TRIP = 9;
const MENU_STOP = 6;

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

const GROUND_VERTEX = /* glsl */ `
  varying vec2 vMap;
  void main() {
    vMap = position.xz;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }`;

/**
 * The painting, and what has happened to it: Atalaya's works burnt out
 * (black and grey, a few embers, smoke drifting off east), the Bastion's
 * clearing in the pines above Brena (a meadow, its cross of tracks, the
 * caravan in the middle); and the Marea over it all: where the activity has
 * reached, the ground bled of colour and stained violet, a veil drifting over
 * it; at its edge a glow and a bright line (a few pixels whatever the size).
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

  float box(vec2 p, vec2 size, float r) {
    vec2 q = abs(p) - size + r;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
  }

  void main() {
    vec3 c = texture2D(map, (vMap + vec2(${f1(HW)}, ${f1(HD)})) / vec2(${f1(W)}, ${f1(D)})).rgb;
    // Atalaya, burnt out: its works charred (its roofs and yards most, the grass round them scorched), soot in patches,
    // a few embers glowing, smoke off east on the wind. Colours here are linear, as the painting is read.
    vec2 ra = (vMap - vec2(${f1(ATALAYA_WORKS.x)}, ${f1(ATALAYA_WORKS.z)})) / vec2(${f1(ATALAYA_WORKS.rx)}, ${f1(ATALAYA_WORKS.rz)});
    float ruin = smoothstep(1.0, 0.6, length(ra) + (noise(vMap * 2.6) - 0.5) * 0.4);
    float soot = noise(vMap * 5.0 + 2.0) * 0.6 + noise(vMap * 13.0) * 0.4;
    float lum = dot(c, vec3(0.2126, 0.7152, 0.0722));
    vec3 scorched = mix(c * vec3(0.55, 0.45, 0.3), vec3(lum) * vec3(0.3, 0.27, 0.24), 0.5);
    vec3 charred = mix(vec3(lum) * vec3(0.32, 0.3, 0.28), vec3(0.008, 0.007, 0.006), smoothstep(0.4, 0.75, soot) * 0.8);
    c = mix(c, mix(scorched, charred, smoothstep(0.12, 0.35, lum)), ruin);
    float ember = smoothstep(0.93, 0.985, noise(vMap * 24.0 + 4.0)) * ruin * (0.55 + 0.45 * sin(time * 2.1 + vMap.x * 11.0 + vMap.y * 7.0));
    c += vec3(1.0, 0.38, 0.1) * ember * 0.75;
    vec2 wind = vec2(1.0, 0.35);
    vec2 off = vMap - vec2(${f1(ATALAYA_WORKS.x)}, ${f1(ATALAYA_WORKS.z)});
    float down = dot(off, normalize(wind));
    float across = dot(off, vec2(-normalize(wind).y, normalize(wind).x));
    float plume = smoothstep(-0.4, 0.3, down) * exp(-down / 3.2) * exp(-across * across / (0.25 + max(down, 0.0) * 0.35));
    float puff = noise(off * 1.6 - wind * time * 0.12) * 0.65 + noise(off * 3.7 - wind * time * 0.2 + 5.0) * 0.35;
    c = mix(c, vec3(0.25, 0.25, 0.24), clamp(plume * smoothstep(0.3, 0.8, puff), 0.0, 1.0) * 0.55);
    // The Bastion: a meadow cut out of the pines, its tracks out to the four sides, the caravan in the middle.
    vec2 rb = vMap - vec2(${f1(BASTION.x)}, ${f1(BASTION.z)});
    float d = length(rb * vec2(1.0, 1.12)) + (noise(vMap * 9.0) - 0.5) * 0.12;
    // The pines round it, in clumps, darker than the painting's woods; the meadow inside sunlit and dry.
    float ring = smoothstep(0.24, 0.32, d) * smoothstep(0.62, 0.4, d) * smoothstep(0.35, 0.62, noise(vMap * 16.0 + 3.0));
    c = mix(c, vec3(0.012, 0.045, 0.014) * (0.8 + 0.4 * noise(vMap * 40.0)), ring * 0.75);
    float glade = smoothstep(0.3, 0.22, d);
    c = mix(c, vec3(0.36, 0.38, 0.11) * (0.9 + 0.2 * noise(vMap * 30.0)), glade * 0.9);
    float track = max(1.0 - smoothstep(0.012, 0.024, abs(rb.x)), 1.0 - smoothstep(0.012, 0.024, abs(rb.y))) * smoothstep(0.5, 0.22, length(rb));
    c = mix(c, vec3(0.33, 0.21, 0.11), track * 0.85);
    float shade = box(rb - vec2(0.012, 0.014), vec2(0.05, 0.026), 0.012);
    c *= 1.0 - 0.45 * (1.0 - smoothstep(0.0, 0.012, shade));
    float caravan = box(rb, vec2(0.05, 0.026), 0.012);
    c = mix(c, vec3(0.52, 0.12, 0.025), 1.0 - smoothstep(0.0, 0.004, caravan));
    c = mix(c, vec3(0.8, 0.74, 0.62), 1.0 - smoothstep(0.0, 0.004, box(rb - vec2(-0.012, -0.003), vec2(0.009, 0.008), 0.002)));
    // Where it reaches, the field read a little askew: its edge wavers the same everywhere, steep or not.
    vec2 warp = (vec2(noise(vMap * 0.8 + 3.1), noise(vMap * 0.8 + 9.7)) - 0.5) * 0.7 + (vec2(noise(vMap * 2.7 + 1.3), noise(vMap * 2.7 + 5.9)) - 0.5) * 0.22;
    vec2 fuv = (vMap + warp + vec2(${f1(HW)}, ${f1(HD)})) / vec2(${f1(W)}, ${f1(D)});
    float edge = texture2D(field, fuv).r - level;
    // Screen pixels from the edge (CSS ones), negative inside.
    float px = edge / max(fwidth(edge), 1e-5) / pixel;
    vec2 drift = vMap * 0.7 + vec2(time * 0.05, time * 0.018);
    float veil = noise(drift) * 0.6 + noise(drift * 2.3 + 7.0) * 0.4;
    vec3 under = c * vec3(0.305, 0.127, 0.429);
    under = mix(under, vec3(0.205, 0.028, 0.352), 0.2);
    under = mix(under, vec3(0.515, 0.19, 0.79), 0.16 * smoothstep(0.45, 0.9, veil));
    c = mix(c, under, smoothstep(0.6, -0.6, px));
    float flicker = 0.85 + 0.15 * sin(time * 1.7 + vMap.x * 1.1 + vMap.y * 0.7);
    c += vec3(0.79, 0.155, 1.0) * (exp(-abs(px) / 7.0) * 0.36 + exp(-abs(px) / 22.0) * 0.12) * flicker;
    c = mix(c, vec3(0.97, 0.72, 1.0), 1.0 - smoothstep(0.7, 1.7, abs(px)));
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
 * the roads. A click on a zone selects it.
 */
/**
 * The operations the valley shows: while the siege is on (Arturo, 18/10 #50: «cuando toque asedio, solo tiene
 * que aparecer esa misión»), only it; the others wait, hidden, and come back once it is over.
 */
export function shownOffers(c: CampaignState): MissionOffer[] {
  const now = activeOffer(c);
  return now?.siege ? [now] : c.offers;
}

export class GeoscapeSet implements StageSet {
  readonly scene = new THREE.Scene();
  readonly ambientOcclusion = false;
  /** No post-processing: the map is shown as painted, its glow in its own shader. */
  readonly plain = true;
  readonly mood = MAP_MOOD;
  /** The map, scaled and moved under the still camera; its children are in map units. */
  private readonly map = new THREE.Group();
  /** The painting, one plane; drawn once its image is in. */
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
   * panels), times how much closer a lab frames it (look: 1 in the game).
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
  /** How far the map has moved aside (to the left) for a zone picked while a sheet is open; back to nothing when it closes. */
  private aside = 0;
  /** The zone picked (update): kept clear of a sheet open over the map. */
  private selected: RegionId | null = null;
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
    this.ground = new THREE.Mesh(new THREE.PlaneGeometry(W, D).rotateX(-Math.PI / 2), HIT);
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

    // The Bastion: the clearing in the pines above Brena.
    const home = el('div', 'valley-place');
    home.append(el('i', 'valley-home'), el('b', 'valley-name', LANDMARKS.atalaya.toUpperCase()));
    this.atalaya = new CSS2DObject(home);
    this.atalaya.position.set(BASTION.x, 0, BASTION.z);
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

  /** Keeps a zone clear of a sheet over the map (moving the map as little as it takes), while the panels open for it. */
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
    this.selected = selected;
    // The Marea at the campaign's activity: shown as it is the first time, then creeping to each new extent.
    this.levelGoal = c.doom;
    if (!this.levelKnown) this.level = c.doom;
    this.levelKnown = true;
    const offers = shownOffers(c);
    const key = JSON.stringify([c.regions, offers.map((o) => [o.id, o.region, o.kind, o.facility, o.final, o.story, o.quest]), c.rumors.map((x) => [x.id, x.region]), c.listening, selected, c.doom]);
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
      // The siege is at home, not out in the valley: no pin for it.
      for (const o of offers.filter((x) => x.region === r.id && !x.siege)) {
        const icon = el('span', 'mission-icon');
        icon.dataset.kind = o.kind;
        icon.style.setProperty('--icon', offerTone(o));
        icon.append(el('span', ''));
        badges.append(icon);
      }
      const rumor = c.rumors.find((x) => x.region === r.id);
      if (rumor) badges.append(el('i', `valley-rumor${rumor.id === c.listening ? ' listening' : ''}`));
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
      this.zoomGoal = Math.max(1, l.scale);
      this.scaleGoal = this.fit * this.zoomGoal;
      this.panGoal.set((this.free.x0 + this.free.x1) / 2 - l.x * this.scaleGoal, (this.free.z0 + this.free.z1) / 2 - l.z * this.scaleGoal);
      this.looking = null;
    }
    // The zone picked (or asked for, while the panels open for it) clear of a sheet over the map.
    if (this.focusing && this.focusing.until < 0) this.focusing.until = time + 0.8;
    if (this.focusing && time > this.focusing.until) this.focusing = null;
    const keep = this.focusing?.region ?? this.selected;
    if (keep && this.insets.right > 0) this.keepInView(keep);
    // The map eases to its size and place; the first frame takes them as they are.
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
      this.aside = 0;
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
   * The scale at which the valley fits whole in the part of the screen the
   * panels leave (fitArea, measured all along: they slide in as a screen
   * opens); framed closer by a lab (look), what is in the middle of the free
   * part stays there.
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
   * Where the map lies: in the middle of the part it is fitted to, moved
   * aside for a zone picked. Where it is bigger than the free part of the
   * screen (a sheet open over it, or framed closer by a lab), it covers that
   * part: no table shows there.
   */
  private clampPan(): void {
    const f = this.free;
    const a = this.fitArea;
    if (!(f.x1 > f.x0) || !(a.x1 > a.x0)) return;
    const s = this.scaleGoal;
    const c = VALLEY.getCenter(new THREE.Vector3());
    // A lab's closer framing keeps where it was put (look).
    const close = this.zoomGoal > 1;
    const axis = (v: number, lo: number, hi: number, middle: number) => (lo <= hi ? THREE.MathUtils.clamp(close ? v : middle, lo, hi) : middle);
    // Moved aside, its left edge stays on the screen.
    const middle = (a.x0 + a.x1) / 2 - s * c.x;
    const edge = this.screen.x0 + MARGIN * this.worldPerPixel() - s * VALLEY.min.x;
    this.panGoal.x = axis(this.panGoal.x, f.x1 - s * VALLEY.max.x, f.x0 - s * VALLEY.min.x, Math.max(middle + this.aside, Math.min(edge, middle)));
    this.panGoal.y = axis(this.panGoal.y, f.z1 - s * VALLEY.max.z, f.z0 - s * VALLEY.min.z, (a.z0 + a.z1) / 2 - s * c.z);
  }

  /**
   * Moves the map aside, as little as it takes, for a zone (its name and its
   * badges) to clear the sheet open on the right; kept while the sheet stays
   * open, whatever is picked next, so the map does not slide back and forth.
   */
  private keepInView(region: RegionId): void {
    const f = this.free;
    const a = this.fitArea;
    if (!(f.x1 > f.x0) || !(a.x1 > a.x0)) return;
    // Where the zone lies with the map in the middle.
    const x = (a.x0 + a.x1) / 2 + (ZONES[region].x - VALLEY.getCenter(new THREE.Vector3()).x) * this.scaleGoal;
    this.aside = Math.min(this.aside, f.x1 - CLEAR * this.worldPerPixel() - x);
  }

  /** The painting, fetched and decoded off the main thread once for the session, its mipmaps made on the GPU. */
  private load(): void {
    painting ??= fetch(`${import.meta.env.BASE_URL}${MAP_URL}`)
      .then((r) => (r.ok ? r.blob() : Promise.reject(new Error(`${r.status}`))))
      .then((blob) => createImageBitmap(blob))
      .catch((e: Error) => {
        painting = null;
        throw e;
      });
    painting
      .then((bitmap) => {
        const texture = new THREE.Texture(bitmap);
        texture.flipY = false;
        texture.colorSpace = THREE.SRGBColorSpace;
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
      // Along its road at a steady pace, never more than a few seconds a trip; in the main menu unhurried, some nine
      // seconds a trip, easing off and on at its ends (playtest 19/10, #1: it went far too fast).
      let length = 0;
      for (let i = 1; i < this.path.length; i++) length += this.path[i]!.distanceTo(this.path[i - 1]!);
      this.along = Math.min(length, this.along + dt * (this.inCampaign ? Math.max(3.6, length / 3) : Math.max(1.2, length / MENU_TRIP)));
      const t = length > 0 ? this.along / length : 1;
      let left = this.inCampaign ? this.along : length * t * t * (3 - 2 * t);
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
    } else if (!this.inCampaign && (this.idle += dt) > MENU_STOP) {
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
