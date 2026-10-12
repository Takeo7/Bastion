import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { REGIONS, type CampaignState, type RegionId } from '@bastion/engine';
import { voxelField, voxelize, type Colour } from '../game/voxel';
import type { Shot, StageSet } from './stage';

const RADIUS = 4;
/** Voxel art (M5): the globe is 72 voxels across, with land and mountains raised in steps. */
const VOXEL = RADIUS / 36;
/** Markers, rings and the dropship use finer voxels. */
const SMALL_VOXEL = 0.045;

/** Coarse continent outlines as [lon, lat] rings — enough to read as Earth. */
const CONTINENTS: [number, number][][] = [
  [
    [-168, 65],
    [-140, 70],
    [-95, 72],
    [-75, 62],
    [-60, 50],
    [-67, 44],
    [-75, 35],
    [-81, 25],
    [-97, 26],
    [-105, 20],
    [-95, 16],
    [-85, 10],
    [-80, 8],
    [-90, 15],
    [-105, 22],
    [-117, 32],
    [-124, 40],
    [-125, 49],
    [-135, 58],
    [-150, 60],
    [-165, 60],
  ],
  [
    [-55, 60],
    [-45, 60],
    [-20, 70],
    [-25, 82],
    [-60, 82],
    [-70, 76],
  ],
  [
    [-80, 8],
    [-60, 10],
    [-50, 0],
    [-35, -7],
    [-40, -22],
    [-55, -35],
    [-65, -55],
    [-72, -50],
    [-72, -30],
    [-80, -5],
  ],
  [
    [-10, 36],
    [-9, 43],
    [-5, 48],
    [3, 51],
    [8, 54],
    [5, 60],
    [15, 69],
    [30, 70],
    [40, 66],
    [50, 55],
    [40, 45],
    [28, 41],
    [20, 40],
    [15, 38],
    [12, 44],
    [3, 42],
    [-5, 36],
  ],
  [
    [-5, 50],
    [2, 52],
    [-2, 57],
    [-6, 58],
    [-5, 54],
  ],
  [
    [-17, 15],
    [-10, 28],
    [-5, 35],
    [10, 37],
    [32, 31],
    [43, 12],
    [51, 12],
    [40, -15],
    [32, -28],
    [20, -35],
    [12, -17],
    [9, 4],
    [-8, 5],
  ],
  [
    [28, 41],
    [40, 45],
    [50, 55],
    [40, 66],
    [60, 70],
    [100, 78],
    [140, 72],
    [170, 66],
    [160, 58],
    [140, 50],
    [130, 42],
    [122, 30],
    [110, 20],
    [105, 10],
    [100, 13],
    [95, 20],
    [90, 22],
    [80, 15],
    [72, 20],
    [60, 25],
    [50, 30],
    [45, 40],
    [35, 37],
  ],
  [
    [35, 32],
    [45, 13],
    [55, 17],
    [58, 23],
    [50, 29],
  ],
  [
    [95, 5],
    [105, -6],
    [120, -8],
    [140, -6],
    [130, 0],
    [118, 5],
    [105, 2],
  ],
  [
    [130, 32],
    [140, 36],
    [142, 44],
    [140, 40],
    [132, 34],
  ],
  [
    [113, -22],
    [115, -34],
    [130, -32],
    [140, -38],
    [150, -37],
    [153, -28],
    [145, -15],
    [136, -12],
    [130, -12],
    [122, -17],
  ],
];

/** Linear-space voxel colour from a hex value. */
function tone(hex: number, glow = false): Colour {
  const c = new THREE.Color(hex);
  return { r: c.r, g: c.g, b: c.b, glow };
}

const GROUND = {
  deep: tone(0x1d4a73),
  shallow: tone(0x2e77a2),
  seaIce: tone(0xc9dbe6),
  jungle: tone(0x3b6a37),
  savanna: tone(0x8f9550),
  desert: tone(0xc8a469),
  temperate: tone(0x5f8e49),
  taiga: tone(0x4a6a4b),
  rock: tone(0x7c705c),
  core: tone(0x3a3530),
  snow: tone(0xe6edf1),
};

/** Land mask painted from the outlines, 2 px per degree: 0 ocean, 1 shallows, 2 land. */
function landMask(): (lat: number, lon: number) => number {
  const W = 720;
  const H = 360;
  const c = document.createElement('canvas');
  c.width = W;
  c.height = H;
  const g = c.getContext('2d', { willReadFrequently: true })!;
  const px = (lon: number, lat: number): [number, number] => [((lon + 180) / 360) * W, ((90 - lat) / 180) * H];
  const trace = (ring: [number, number][]) => {
    g.beginPath();
    ring.forEach(([lon, lat], i) => (i ? g.lineTo(...px(lon, lat)) : g.moveTo(...px(lon, lat))));
    g.closePath();
  };
  g.fillStyle = '#000';
  g.fillRect(0, 0, W, H);
  g.strokeStyle = '#800000';
  g.lineWidth = 9;
  g.lineJoin = 'round';
  for (const ring of CONTINENTS) (trace(ring), g.stroke());
  g.fillStyle = '#f00';
  for (const ring of CONTINENTS) (trace(ring), g.fill());
  g.fillRect(0, px(0, -68)[1], W, H);
  const data = g.getImageData(0, 0, W, H).data;
  return (lat, lon) => {
    const [x, y] = px(lon, lat);
    const i = (Math.min(H - 1, Math.max(0, Math.floor(y))) * W + (((Math.floor(x) % W) + W) % W)) * 4;
    const red = data[i]!;
    return red > 192 ? 2 : red > 64 ? 1 : 0;
  };
}

/** Latitude and longitude (-180..180) of a unit direction: the inverse of latLonToVector. */
function toLatLon(d: THREE.Vector3): [number, number] {
  const lat = THREE.MathUtils.radToDeg(Math.asin(THREE.MathUtils.clamp(d.y, -1, 1)));
  const lon = THREE.MathUtils.radToDeg(Math.atan2(d.z, -d.x)) - 180;
  return [lat, ((lon + 540) % 360) - 180];
}

/** Smooth, seamless relief over the sphere (roughly -1.5..1.5). */
function relief(d: THREE.Vector3): number {
  return Math.sin(d.x * 5.1 + 1.7) * Math.sin(d.y * 4.3 + 0.4) * Math.sin(d.z * 5.7 + 2.2) + 0.45 * Math.sin(d.x * 11.3 - d.z * 9.1 + d.y * 7.7);
}

function biome(lat: number, lon: number, n: number): Colour {
  const a = Math.abs(lat);
  if (lat < -62 || a > 72 || (lat > 60 && lon > -75 && lon < -15)) return GROUND.snow;
  if (a < 12) return GROUND.jungle;
  if (a < 32) return n < 0.1 ? GROUND.desert : GROUND.savanna;
  if (a < 55) return GROUND.temperate;
  return GROUND.taiga;
}

/** The Earth never changes: built once (~100 ms), then cloned into every geoscape. */
let earthCache: {
  model: THREE.Group;
  surface: (dir: THREE.Vector3) => number;
} | null = null;
function cachedEarth(): {
  model: THREE.Group;
  surface: (dir: THREE.Vector3) => number;
} {
  earthCache ??= voxelEarth();
  return { model: earthCache.model.clone(), surface: earthCache.surface };
}

/**
 * The voxel Earth: oceans at RADIUS (shallows lighter, sea ice at the pole),
 * land one voxel up, mountains two and three (rock, then snow).
 * `surface(dir)` is the ground radius under a unit direction, for markers.
 */
function voxelEarth(): {
  model: THREE.Group;
  surface: (dir: THREE.Vector3) => number;
} {
  const mask = landMask();
  const ground = (dir: THREE.Vector3) => {
    const [lat, lon] = toLatLon(dir);
    const kind = mask(lat, lon);
    const n = kind === 2 ? relief(dir) : 0;
    return {
      lat,
      lon,
      kind,
      n,
      level: kind < 2 ? 0 : n > 1.12 ? 3 : n > 0.85 ? 2 : 1,
    };
  };
  const d = new THREE.Vector3();
  const n = Math.ceil(RADIUS / VOXEL) + 4;
  const model = voxelField(
    [-n, -n, -n],
    [n - 1, n - 1, n - 1],
    (x, y, z) => {
      d.set(x + 0.5, y + 0.5, z + 0.5).multiplyScalar(VOXEL);
      const r = d.length();
      if (r > RADIUS + 3.5 * VOXEL) return null;
      // The core is never seen.
      if (r < RADIUS - 2 * VOXEL) return GROUND.core;
      const g = ground(d.divideScalar(r));
      if (r > RADIUS + g.level * VOXEL) return null;
      if (g.kind === 0) return g.lat > 80 ? GROUND.seaIce : GROUND.deep;
      if (g.kind === 1) return GROUND.shallow;
      if (r > RADIUS + 2 * VOXEL) return g.level === 3 ? GROUND.snow : GROUND.rock;
      if (r > RADIUS + VOXEL) return GROUND.rock;
      return biome(g.lat, g.lon, g.n);
    },
    { size: VOXEL, jitter: 0.12 },
  );
  // Each face's normal is bent halfway towards the sphere's: the steps keep their
  // facets, but the risers turned away from the sun do not go black.
  model.traverse((o) => {
    if (!(o instanceof THREE.Mesh)) return;
    const pos = o.geometry.getAttribute('position');
    const nrm = o.geometry.getAttribute('normal');
    const face = new THREE.Vector3();
    const radial = new THREE.Vector3();
    for (let i = 0; i < nrm.count; i++) {
      radial.fromBufferAttribute(pos, i).normalize();
      face.fromBufferAttribute(nrm, i).add(radial).normalize();
      nrm.setXYZ(i, face.x, face.y, face.z);
    }
  });
  return { model, surface: (dir) => RADIUS + ground(dir).level * VOXEL };
}

/** Glowing material: voxelize() turns emissive parts into unlit, bloom-bright voxels. */
function glow(color: number, intensity = 1.2): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({
    color,
    emissive: color,
    emissiveIntensity: intensity,
  });
}

/** Makes a voxel model see-through (beacons, rings). Returns its materials, for fading. */
function seeThrough(model: THREE.Object3D, opacity: number, additive = false): THREE.Material[] {
  const materials: THREE.Material[] = [];
  model.traverse((o) => {
    if (!(o instanceof THREE.Mesh)) return;
    const m = o.material as THREE.Material;
    m.transparent = true;
    m.opacity = opacity;
    m.depthWrite = false;
    if (additive) m.blending = THREE.AdditiveBlending;
    o.castShadow = false;
    materials.push(m);
  });
  return materials;
}

/** Marker parts are voxelized once per kind; markers get clones that share geometry and materials. */
const parts = new Map<string, THREE.Group>();
function part(key: string, build: () => THREE.Mesh, opacity = 1, additive = false): THREE.Group {
  let template = parts.get(key);
  if (!template) {
    template = voxelize([{ mesh: build(), matrix: new THREE.Matrix4() }], SMALL_VOXEL);
    if (opacity < 1) seeThrough(template, opacity, additive);
    parts.set(key, template);
  }
  return template.clone();
}

/** How high La Mula flies over the ground, and how far from a region's marker it parks (degrees of latitude, longitude). */
const SHIP_ALT = 0.95;
const PARK: [number, number] = [4, 8];

/**
 * La Mula seen from above, nose along +z and up along +y: a pale hull with a
 * dark outline (it reads on ocean and on land alike), an amber band and tail
 * beacon, and two rotors that turn (`rotors`, to spin in tick()). Not the
 * players' colours: those are the commanders', and the ship is everyone's.
 */
function voxelShip(): { model: THREE.Group; rotors: THREE.Group[] } {
  const hull = new THREE.MeshStandardMaterial({ color: 0xf4efe2, metalness: 0.2, roughness: 0.55 });
  const band = new THREE.MeshStandardMaterial({ color: 0xffb020, metalness: 0.2, roughness: 0.5 });
  const glass = new THREE.MeshStandardMaterial({ color: 0x24384a, metalness: 0.5, roughness: 0.3 });
  const at = (x: number, y: number, z: number) => new THREE.Matrix4().makeTranslation(x, y, z);
  const parts: [number, number, number, number, number, number, THREE.Material][] = [
    // w, h, l, x, y, z
    [0.17, 0.11, 0.6, 0, 0, 0, hull],
    [0.15, 0.09, 0.12, 0, -0.01, 0.34, hull],
    [0.13, 0.05, 0.08, 0, 0.04, 0.33, glass],
    [0.14, 0.08, 0.18, 0, 0.08, -0.22, hull],
    [0.175, 0.115, 0.05, 0, 0, -0.08, band],
    [0.06, 0.05, 0.06, 0, 0.06, 0.2, hull],
  ];
  const model = voxelize(
    [
      ...parts.map(([w, h, l, x, y, z, m]) => ({ mesh: new THREE.Mesh(new THREE.BoxGeometry(w, h, l), m), matrix: at(x, y, z) })),
      { mesh: new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.04, 0.04), glow(0xff5a3a, 2.4)), matrix: at(0, 0.14, -0.28) },
    ],
    0.02,
  );
  // The outline: the hull's boxes a little fatter, dark, drawn inside out so only their rim shows round the ship.
  const rim = new THREE.MeshBasicMaterial({ color: 0x0b0f14, side: THREE.BackSide });
  for (const [w, h, l, x, y, z] of parts.slice(0, 4)) {
    const shell = new THREE.Mesh(new THREE.BoxGeometry(w + 0.05, h + 0.05, l + 0.05), rim);
    shell.position.set(x, y, z);
    model.add(shell);
  }
  // Two three-bladed rotors: the forward one low over the cockpit, the aft one on the tail pylon.
  const rotors = [
    [0.22, 0.08],
    [-0.22, 0.13],
  ].map(([z, y]) => {
    const blades = new THREE.MeshStandardMaterial({ color: 0x1d2124, roughness: 0.6 });
    const tips = new THREE.MeshStandardMaterial({ color: 0xffb020, roughness: 0.5 });
    const sources = [0, 1, 2].flatMap((k) => {
      const a = (k * Math.PI * 2) / 3;
      const turn = new THREE.Matrix4().makeRotationY(a);
      return [
        { mesh: new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.015, 0.035), blades), matrix: turn.clone().multiply(at(0.1, 0, 0)) },
        { mesh: new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.016, 0.036), tips), matrix: turn.clone().multiply(at(0.215, 0, 0)) },
      ];
    });
    const rotor = voxelize(sources, 0.015);
    rotor.position.set(0, y!, z!);
    model.add(rotor);
    return rotor;
  });
  return { model, rotors };
}

/** A soft round glow, for the halo behind the ship. */
function haloTexture(): THREE.Texture {
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const g = c.getContext('2d')!;
  const grad = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  grad.addColorStop(0, 'rgba(255,255,255,0.9)');
  grad.addColorStop(0.35, 'rgba(255,255,255,0.45)');
  grad.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = grad;
  g.fillRect(0, 0, 64, 64);
  const texture = new THREE.CanvasTexture(c);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
}

/** Point on the sphere for a latitude/longitude, matching SphereGeometry's UV layout. */
export function latLonToVector(lat: number, lon: number, radius = RADIUS): THREE.Vector3 {
  const phi = ((lon + 180) / 360) * Math.PI * 2;
  const theta = ((90 - lat) / 180) * Math.PI;
  return new THREE.Vector3(-radius * Math.cos(phi) * Math.sin(theta), radius * Math.cos(theta), radius * Math.sin(phi) * Math.sin(theta));
}

interface Marker {
  region: RegionId;
  group: THREE.Group;
  label: CSS2DObject;
  normal: THREE.Vector3;
  /** Distance from the centre to where the marker sits, above the ground. */
  height: number;
}

/**
 * Geoscape: the Earth with the resistance regions, alien facilities, mission
 * sites and the Bastion's dropship. Drag to spin it, click a region to select it.
 */
export class GeoscapeSet implements StageSet {
  readonly scene = new THREE.Scene();
  private readonly globe = new THREE.Group();
  private readonly markerGroup = new THREE.Group();
  private readonly markers: Marker[] = [];
  /** Ground radius under a direction (the voxel terrain has land and mountains). */
  private readonly surface: (dir: THREE.Vector3) => number;
  private readonly ring: THREE.Group;
  private readonly ringFade: THREE.Material[];
  private readonly ship = new THREE.Group();
  private readonly shipRotors: THREE.Group[];
  private readonly halo: THREE.Sprite;
  private readonly tether: THREE.Line;
  private readonly shipLabel: CSS2DObject;
  /** Where the ship's nose points (globe space), eased towards the way it moves. */
  private readonly heading = new THREE.Vector3(0, 0, 1);
  private readonly lastShipPos = new THREE.Vector3();
  /** Seconds since the ship reached its region, for its slow circling. */
  private loiter = 0;
  /** The menu's backdrop never gets a campaign: the ship's name only shows once update() has run. */
  private inCampaign = false;
  private readonly scanRing: THREE.Group;
  private readonly scanFade: THREE.Material[];
  private shipFrom = new THREE.Vector3();
  private shipTo = new THREE.Vector3();
  private shipT = 1;
  private shipRegion: RegionId | null = null;
  private yaw = 0;
  private targetYaw = 0;
  private pitch = 0.3;
  private idle = 0;
  private scanning = 0;
  private lastKey = '';

  constructor(private readonly onPick: (region: RegionId) => void) {
    this.scene.background = new THREE.Color(0x03070b);
    this.scene.add(new THREE.AmbientLight(0x6a8aa8, 1.3));
    const sun = new THREE.DirectionalLight(0xdfefff, 2.2);
    sun.position.set(-6, 4, 8);
    this.scene.add(sun);
    const stars: number[] = [];
    for (let i = 0; i < 1500; i++) {
      const v = new THREE.Vector3().randomDirection().multiplyScalar(120 + Math.random() * 80);
      stars.push(v.x, v.y, v.z);
    }
    this.scene.add(
      new THREE.Points(
        new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(stars, 3)),
        new THREE.PointsMaterial({
          color: 0xbfd8ee,
          size: 0.6,
          sizeAttenuation: true,
        }),
      ),
    );

    const earth = cachedEarth();
    this.surface = earth.surface;
    const atmosphere = new THREE.Mesh(
      new THREE.SphereGeometry(RADIUS * 1.07, 48, 32),
      new THREE.MeshBasicMaterial({
        color: 0x3fa9ff,
        transparent: true,
        opacity: 0.13,
        side: THREE.BackSide,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
      }),
    );
    this.ring = voxelize(
      [
        {
          mesh: new THREE.Mesh(new THREE.RingGeometry(0.34, 0.44, 32), glow(0xffffff, 1)),
          matrix: new THREE.Matrix4(),
        },
      ],
      SMALL_VOXEL,
    );
    this.ringFade = seeThrough(this.ring, 0.9);
    this.ring.visible = false;
    // La Mula flying over the globe: a soft halo behind it, a line down to the ground under it, and its name.
    const mula = voxelShip();
    // About twice the old dropship's size: a ship's length is a good finger's width at the default zoom.
    mula.model.scale.setScalar(1.4);
    this.ship.add(mula.model);
    this.shipRotors = mula.rotors;
    this.halo = new THREE.Sprite(
      new THREE.SpriteMaterial({ map: haloTexture(), color: 0xffcf73, blending: THREE.AdditiveBlending, transparent: true, depthWrite: false, opacity: 0.5 }),
    );
    // Just under the hull, so from the camera it glows round the ship rather than over it.
    this.halo.position.y = -0.18;
    this.halo.scale.set(2, 2, 1);
    this.ship.add(this.halo);
    this.tether = new THREE.Line(
      new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute([0, 0, 0, 0, 0, 0], 3)),
      new THREE.LineBasicMaterial({ color: 0xffcf73, transparent: true, opacity: 0.65 }),
    );
    this.tether.frustumCulled = false;
    const name = document.createElement('div');
    name.className = 'globe-label ship';
    name.textContent = 'La Mula';
    name.style.fontWeight = '700';
    name.style.borderLeft = '3px solid #ffb020';
    this.shipLabel = new CSS2DObject(name);
    this.shipLabel.position.set(0, 0.5, 0);
    this.shipLabel.visible = false;
    this.ship.add(this.shipLabel);
    this.scanRing = voxelize(
      [
        {
          mesh: new THREE.Mesh(new THREE.RingGeometry(0.2, 0.26, 32), glow(0x54d1ff, 1.2)),
          matrix: new THREE.Matrix4(),
        },
      ],
      SMALL_VOXEL,
    );
    this.scanFade = seeThrough(this.scanRing, 0);
    this.globe.add(earth.model, this.markerGroup, this.ring, this.ship, this.tether, this.scanRing);
    this.scene.add(this.globe, atmosphere);
    this.yaw = this.targetYaw = this.yawFacing(REGIONS.weu.lon);
    this.placeShip(REGIONS.weu.lat, REGIONS.weu.lon, true);
  }

  shot(): Shot {
    return {
      pos: new THREE.Vector3(0.6, 0.4, 19.5),
      target: new THREE.Vector3(0.6, 0.4, 0),
      fov: 36,
    };
  }

  private yawFacing(lon: number): number {
    const p = latLonToVector(0, lon);
    return -Math.atan2(p.x, p.z);
  }

  /** Turns the globe so a region faces the camera. */
  focus(region: RegionId): void {
    this.targetYaw = this.yawFacing(REGIONS[region].lon);
    this.idle = 0;
  }

  /** The dropship flies to `region` (missions take it there). */
  flyTo(region: RegionId): void {
    if (region === this.shipRegion) return;
    this.shipRegion = region;
    const def = REGIONS[region];
    this.placeShip(def.lat, def.lon, false);
  }

  /** Sends the ship to park beside a region's marker (not on it: its beacon and label stay in sight). */
  private placeShip(lat: number, lon: number, instant: boolean): void {
    const to = latLonToVector(lat + PARK[0], lon + PARK[1], RADIUS + SHIP_ALT);
    this.shipFrom.copy(this.ship.position.lengthSq() ? this.ship.position : to);
    this.shipTo.copy(to);
    this.shipT = instant ? 1 : 0;
    if (instant) this.ship.position.copy(this.shipTo);
    else this.loiter = 0;
  }

  /** Scanning: the ring under the ship pulses and the globe turns faster. */
  setScanning(on: boolean): void {
    this.scanning = on ? 1.6 : 0;
  }

  update(c: CampaignState, selected: RegionId | null): void {
    this.inCampaign = true;
    const key = JSON.stringify([c.regions, c.offers.map((o) => [o.id, o.region, o.facility, o.final, o.story, o.quest]), c.rumors.map((x) => [x.id, x.region]), c.listening, selected]);
    if (key === this.lastKey) return;
    this.lastKey = key;
    for (const m of this.markers) {
      this.markerGroup.remove(m.group);
      m.label.element.remove();
    }
    this.markers.length = 0;
    for (const r of c.regions) {
      const def = REGIONS[r.id];
      const normal = latLonToVector(def.lat, def.lon, 1);
      // Markers stand on the voxel ground, a little above its stepped top.
      const height = this.surface(normal) + VOXEL * 0.8;
      const group = new THREE.Group();
      group.position.copy(normal.clone().multiplyScalar(height));
      group.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), normal);
      group.userData.hotspot = r.id;
      const pad = r.contacted
        ? part('pad:contacted', () => new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.22, 0.05, 20), glow(0x3fa9ff, 1)), 0.85)
        : part('pad', () => new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.22, 0.05, 20), new THREE.MeshStandardMaterial({ color: 0x8a96a3 })));
      group.add(pad);
      const tall = r.contacted ? 0.9 : 0.4;
      const beacon = part(
        `beacon:${r.contacted}`,
        () => new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, tall, 6), glow(r.contacted ? 0x3fa9ff : 0x8a96a3, r.contacted ? 1.4 : 0.6)),
        0.7,
        true,
      );
      beacon.position.y = tall / 2;
      group.add(beacon);
      if (r.facility) {
        const pyramid = part('facility', () => new THREE.Mesh(new THREE.ConeGeometry(0.22, 0.45, 4), glow(0xff2a3a)));
        pyramid.position.set(0.32, 0.22, 0);
        group.add(pyramid);
      }
      const offers = c.offers.filter((o) => o.region === r.id);
      offers.forEach((o, i) => {
        // Story chapters glow blue, like the pen on the Expediente; side quests green.
        const tone = o.final ? 'final' : o.story ? 'story' : o.quest ? 'side' : 'op';
        const diamond = part(`offer:${tone}`, () => new THREE.Mesh(new THREE.OctahedronGeometry(0.13, 0), glow({ final: 0xff2a5a, story: 0x4a7dff, side: 0x3fd68a, op: 0xff9a10 }[tone], 1.4)));
        diamond.position.set(-0.3 - i * 0.24, 0.3, 0.1);
        diamond.userData.spin = true;
        group.add(diamond);
      });
      // A rumour floats over its region, brighter while the Bastion listens to it.
      const rumor = c.rumors.find((x) => x.region === r.id);
      if (rumor) {
        const listening = rumor.id === c.listening;
        const orb = part(`rumour:${listening}`, () => new THREE.Mesh(new THREE.SphereGeometry(0.1, 12, 8), glow(listening ? 0x7dffb0 : 0xfff0c0, listening ? 1.8 : 1.1)));
        orb.position.set(0.3, 0.62, -0.1);
        // It breathes, faster while the Bastion listens.
        orb.userData.pulse = listening ? 5 : 2.5;
        group.add(orb);
      }
      group.add(new THREE.Mesh(new THREE.SphereGeometry(0.5, 8, 6), new THREE.MeshBasicMaterial({ visible: false })));
      const el = document.createElement('div');
      el.className = `globe-label${r.contacted ? ' contacted' : ''}${r.facility ? ' facility' : ''}${r.id === selected ? ' selected' : ''}`;
      el.textContent = `${def.name}${offers.length ? ` · ${offers.length}` : ''}${rumor ? ' · ?' : ''}`;
      const tag = new CSS2DObject(el);
      tag.position.set(0, 0.75, 0);
      group.add(tag);
      this.markerGroup.add(group);
      this.markers.push({ region: r.id, group, label: tag, normal, height });
    }
    const sel = this.markers.find((m) => m.region === selected);
    this.ring.visible = !!sel;
    if (sel) {
      this.ring.position.copy(sel.normal.clone().multiplyScalar(sel.height));
      this.ring.lookAt(sel.normal.clone().multiplyScalar(RADIUS * 2));
    }
  }

  hits(): THREE.Object3D[] {
    return this.markers.map((m) => m.group);
  }

  pick(hot: string): void {
    this.onPick(hot as RegionId);
  }

  drag(dx: number, dy: number): boolean {
    this.targetYaw += dx * 0.008;
    this.pitch = THREE.MathUtils.clamp(this.pitch + dy * 0.005, -0.9, 0.9);
    this.idle = 0;
    return true;
  }

  tick(dt: number, time: number): void {
    this.idle += dt;
    if (this.scanning > 0) this.targetYaw += dt * 0.6;
    else if (this.idle > 6) this.targetYaw += dt * 0.04;
    this.yaw += (this.targetYaw - this.yaw) * (1 - Math.exp(-dt * 5));
    this.globe.rotation.set(this.pitch, this.yaw, 0, 'XYZ');
    this.globe.updateMatrixWorld(true);
    const toCamera = new THREE.Vector3(0, 0, 1);
    for (const m of this.markers) {
      const facing = m.normal.clone().applyQuaternion(this.globe.quaternion).dot(toCamera);
      m.label.visible = facing > 0.25;
      m.group.traverse((child) => {
        if (child.userData.spin) child.rotation.y = time * 2;
        if (child.userData.pulse) child.scale.setScalar(1 + Math.sin(time * child.userData.pulse) * 0.12);
      });
    }
    for (const m of this.ringFade) m.opacity = 0.6 + Math.sin(time * 4) * 0.3;
    // La Mula: a great-circle hop to its destination, then slow circles over it, nose first.
    if (this.shipT < 1) {
      this.shipT = Math.min(1, this.shipT + dt * 0.6);
      const k = this.shipT * this.shipT * (3 - 2 * this.shipT);
      this.ship.position
        .copy(this.shipFrom)
        .lerp(this.shipTo, k)
        .setLength(RADIUS + SHIP_ALT + Math.sin(k * Math.PI) * 0.6);
    } else {
      this.loiter += dt;
      const n = this.shipTo.clone().normalize();
      const east = new THREE.Vector3(0, 1, 0).cross(n).normalize();
      const north = n.clone().cross(east);
      const a = this.loiter * 0.45;
      const r = 0.16 * Math.min(1, this.loiter / 2);
      this.ship.position
        .copy(this.shipTo)
        .addScaledVector(east, Math.cos(a) * r)
        .addScaledVector(north, Math.sin(a) * r)
        .setLength(RADIUS + SHIP_ALT + Math.sin(time * 1.6) * 0.02);
    }
    const up = this.ship.position.clone().normalize();
    const moved = this.ship.position.clone().sub(this.lastShipPos);
    if (moved.lengthSq() > 1e-10 && this.lastShipPos.lengthSq()) this.heading.lerp(moved.normalize(), 1 - Math.exp(-dt * 4));
    this.lastShipPos.copy(this.ship.position);
    const forward = this.heading.clone().addScaledVector(up, -this.heading.dot(up));
    if (forward.lengthSq() < 1e-6) forward.set(0, 1, 0).addScaledVector(up, -up.y);
    forward.normalize();
    this.ship.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(new THREE.Vector3().crossVectors(up, forward), up, forward));
    for (const [i, rotor] of this.shipRotors.entries()) rotor.rotation.y += dt * (i ? -9 : 9);
    (this.halo.material as THREE.SpriteMaterial).opacity = 0.42 + Math.sin(time * 2.6) * 0.16;
    this.halo.scale.setScalar(1.95 + Math.sin(time * 2.6) * 0.12);
    // The line down to the ground, under the hull.
    const line = this.tether.geometry.getAttribute('position') as THREE.BufferAttribute;
    const foot = up.clone().multiplyScalar(this.surface(up) + VOXEL * 0.2);
    const top = this.ship.position.clone().addScaledVector(up, -0.08);
    line.setXYZ(0, foot.x, foot.y, foot.z);
    line.setXYZ(1, top.x, top.y, top.z);
    line.needsUpdate = true;
    this.shipLabel.visible = this.inCampaign && this.ship.getWorldPosition(new THREE.Vector3()).normalize().dot(toCamera) > 0.2;
    const pulse = this.scanning > 0 ? (time * 1.2) % 1 : 0;
    this.scanRing.position.copy(this.shipTo.clone().setLength(this.surface(this.shipTo.clone().normalize()) + VOXEL * 0.6));
    this.scanRing.lookAt(this.shipTo.clone().multiplyScalar(2));
    this.scanRing.scale.setScalar(1 + pulse * 5);
    for (const m of this.scanFade) m.opacity = this.scanning > 0 ? 0.9 * (1 - pulse) : 0;
    if (this.scanning > 0) this.scanning = Math.max(0, this.scanning - dt);
  }
}
