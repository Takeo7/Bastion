import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { DOOM_MAX, HOME_REGION, REGIONS, type CampaignState, type RegionId } from '@bastion/engine';
import { StaticBatch } from '../game/batch';
import { extendShader } from '../game/fogOfWar';
import type { BaseModelId } from '../game/syntyManifest';
import { KitBatch, kitMesh, kitModel, loadBaseKit, type Finish } from './baseKit';
import { skyTexture, type Shot, type StageMood, type StageSet } from './stage';

// The map of the valley of Valdesombra (docs/LORE.md, «El mundo»), where the
// globe was: a diorama in Synty's low-poly, seen from the south. The sierra to
// the north with Atalaya under its peaks, closed valleys to the north-east,
// the reservoir and its dead power station to the east, the mines in the
// western hills, the farms of La Vega along the river in the middle, the two
// dead cities (Ciudad Varela to the west, Puerto Galena by the sea), and the
// ash plain to the south where the Marea comes up: a dark tide on the ground
// that creeps towards Atalaya as the zombi activity grows. Each of the eight
// zones carries its marker (contact, Nidos, operations, rumours); La Mula
// drives the roads between them. The same set is the main menu's backdrop.
//
// Everything that stands still is merged by material (game/batch.ts); the
// sun's shadow map is drawn only when something moves (stage.ts).

/** The valley's size: x from west to east, z from north (the sierra) to south (the plain), in map units. */
const W = 30;
const D = 22;
const HW = W / 2;
const HD = D / 2;
/** Game units (a Synty model's own size) to map units. */
const MINI = 0.075;

/** Where each zone lies in the valley (the engine's lat/lon stay for the rules). */
const ZONES: Record<RegionId, { x: number; z: number }> = {
  weu: { x: -4.6, z: -5.4 },
  eeu: { x: 8.2, z: -5.6 },
  eas: { x: 8.6, z: 0.7 },
  sa: { x: -9.8, z: -0.4 },
  oce: { x: 1.6, z: 1.4 },
  afr: { x: -6.8, z: 5.0 },
  me: { x: 8.4, z: 6.4 },
  na: { x: -0.6, z: 8.9 },
};

/** Atalaya: the station on its shelf under the peaks, above Altomonte's villages. */
const ATALAYA = { x: -3.4, z: -7.0 };

/** The roads between the zones (La Mula drives them), each with a bend to look less ruled. */
const ROADS: [RegionId, RegionId, [number, number]][] = [
  ['weu', 'oce', [-1.6, -1.8]],
  ['weu', 'sa', [-8.6, -3.4]],
  ['weu', 'eeu', [2.0, -6.4]],
  ['eeu', 'eas', [6.8, -1.8]],
  ['eas', 'oce', [5.4, 0.6]],
  ['oce', 'afr', [-2.6, 3.6]],
  ['oce', 'me', [4.6, 4.4]],
  ['afr', 'na', [-4.0, 7.6]],
  ['me', 'na', [4.0, 8.6]],
  ['sa', 'afr', [-10.0, 3.0]],
];

/** The river: from under the dam, through La Vega and down to the sea in the south-east corner. */
const RIVER: [number, number][] = [
  [10.35, 0.95],
  [8.8, 1.9],
  [6.4, 2.6],
  [3.8, 3.0],
  [2.4, 3.9],
  [3.2, 5.4],
  [5.6, 6.9],
  [7.9, 8.1],
  [10.4, 9.2],
  [13.0, 10.3],
  [16.0, 12.0],
];

/** The ash plain: grey, burnt ground round Los Llanos (ragged at its edge). */
function ashen(x: number, z: number): boolean {
  const dx = (x - 0.4) / 5.4;
  const dz = (z - 9.6) / 3.4;
  return dx * dx + dz * dz < 0.75 + (noise(x * 0.8, z * 0.8) - 0.5) * 0.6;
}

/** The reservoir: its basin up on a shelf, its water level, and the dam across the gap where the river leaves it. */
const RESERVOIR = { x: 11.2, z: -2.0, r: 2.6, level: 1.2, dam: 2.5 };

// ------------------------------------------------------------- terrain

function hash(x: number, z: number): number {
  const h = Math.sin(x * 127.1 + z * 311.7) * 43758.5453;
  return h - Math.floor(h);
}

/** Smooth value noise in [0, 1]. */
function noise(x: number, z: number): number {
  const xi = Math.floor(x);
  const zi = Math.floor(z);
  const xf = x - xi;
  const zf = z - zi;
  const u = xf * xf * (3 - 2 * xf);
  const v = zf * zf * (3 - 2 * zf);
  const a = hash(xi, zi);
  const b = hash(xi + 1, zi);
  const c = hash(xi, zi + 1);
  const d = hash(xi + 1, zi + 1);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}

const fbm = (x: number, z: number) => noise(x, z) * 0.5 + noise(x * 2.1 + 5, z * 2.1 + 3) * 0.3 + noise(x * 4.3 + 1, z * 4.3 + 9) * 0.2;
const smooth = (a: number, b: number, t: number) => {
  const k = THREE.MathUtils.clamp((t - a) / (b - a), 0, 1);
  return k * k * (3 - 2 * k);
};
const bump = (x: number, z: number, cx: number, cz: number, r: number) => {
  const d2 = ((x - cx) ** 2 + (z - cz) ** 2) / (r * r);
  return d2 >= 1 ? 0 : (1 - d2) ** 2;
};

function distToPolyline(x: number, z: number, line: [number, number][]): number {
  let best = Infinity;
  for (let i = 0; i + 1 < line.length; i++) {
    const [ax, az] = line[i]!;
    const [bx, bz] = line[i + 1]!;
    const dx = bx - ax;
    const dz = bz - az;
    const t = THREE.MathUtils.clamp(((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz), 0, 1);
    best = Math.min(best, Math.hypot(x - ax - dx * t, z - az - dz * t));
  }
  return best;
}

/** How high the valley is at (x, z): the sierra, the hills, the reservoir's upland, the river's bed and the sea. */
function heightAt(x: number, z: number): number {
  const n = fbm(x * 0.35, z * 0.35);
  let h = 0.2 + (n - 0.5) * 0.35;
  // The sierra to the north, its ridge broken into peaks.
  h += smooth(3.6, 9.2, -z) * (2.4 + 2.4 * fbm(x * 0.55 + 7, z * 0.4 + 2));
  // Atalaya's shelf under the peaks, levelled.
  h = THREE.MathUtils.lerp(h, 2.8, smooth(3.0, 1.3, Math.hypot(x - ATALAYA.x, z - ATALAYA.z)));
  // The western hills (Carboneda) and the closed valleys to the north-east (Valdelobos).
  h += 1.6 * bump(x, z, -12.2, -0.8, 4.8) * (0.55 + n);
  h += 1.9 * bump(x, z, 9.6, -6.4, 5.2) * (0.45 + 0.9 * noise(x * 0.9, z * 0.9));
  // The reservoir's shelf, its basin dug out under the water.
  const d = Math.hypot(x - RESERVOIR.x, z - RESERVOIR.z);
  h = THREE.MathUtils.lerp(h, Math.max(h, 1.5 + n * 0.2), smooth(4.2, 3.0, d));
  h = THREE.MathUtils.lerp(h, 0.6, smooth(RESERVOIR.r, RESERVOIR.r - 0.8, d));
  // The river's bed and the sea.
  const r = distToPolyline(x, z, RIVER);
  h = THREE.MathUtils.lerp(-0.22, h, smooth(0.14, 0.6, r));
  h = THREE.MathUtils.lerp(h, -0.7, smooth(5.2, 2.4, Math.hypot(x - 15.5, z - 12)));
  return h;
}

const C = (hex: number) => new THREE.Color(hex);
const GROUND = {
  bed: C(0x8f8463),
  shore: C(0x9c9070),
  snow: C(0xf0f3f7),
  rock: C(0x8a857f),
  darkRock: C(0x6e6a66),
  meadow: C(0x8a985c),
  grass: C(0x86a052),
  grass2: C(0x739049),
  forest: C(0x5d7a43),
  ash: C(0x8f877b),
  ash2: C(0x7d756b),
  city: C(0x8c8981),
  fields: [C(0xb3b25d), C(0x93a650), C(0xc6b06a), C(0x7f9a48), C(0xa5a75a)],
};

/** The colour of the ground at a face: by height, slope and what stands there. */
function groundColour(x: number, z: number, h: number, up: number, out: THREE.Color): THREE.Color {
  const n = noise(x * 1.3, z * 1.3);
  if (h < -0.08) return out.copy(GROUND.bed);
  if (h < 0.04) return out.copy(GROUND.shore);
  if (h > 3.2 - n * 0.6 || (h > 2.2 && up > 0.8 && Math.hypot(x - ATALAYA.x, z - ATALAYA.z) < 2.4)) return out.copy(up > 0.55 ? GROUND.snow : GROUND.rock);
  if (h > 2.3 || up < 0.62) return out.copy(n > 0.5 ? GROUND.rock : GROUND.darkRock);
  if (h > 1.5) return out.copy(GROUND.meadow).lerp(GROUND.forest, n);
  // The ash plain where the Marea comes up.
  if (ashen(x, z)) return out.copy(GROUND.ash).lerp(GROUND.ash2, n);
  // The dead cities' ground.
  if (Math.hypot(x - ZONES.afr.x, z - ZONES.afr.z) < 1.9 || Math.hypot(x - ZONES.me.x - 0.6, z - ZONES.me.z + 0.2) < 1.5) return out.copy(GROUND.city);
  // La Vega's patchwork of fields.
  if (Math.hypot(x - ZONES.oce.x, z - ZONES.oce.z - 0.3) < 3.6) return out.copy(GROUND.fields[Math.floor(hash(Math.floor(x * 1.4), Math.floor(z * 1.4)) * GROUND.fields.length)]!);
  return out.copy(GROUND.grass).lerp(GROUND.grass2, n);
}

/**
 * The ground as faceted low-poly (each triangle one colour), with the Marea
 * drawn on it: past its front (a z that moves north as the zombi activity
 * grows, ragged by noise) the land goes a bruised dark, a red glow along the
 * edge.
 */
function terrain(marea: { value: number }): THREE.Mesh {
  const cols = 132;
  const rows = 96;
  const positions: number[] = [];
  const colours: number[] = [];
  const p = (i: number, j: number): [number, number, number] => {
    const x = -HW + (i / cols) * W;
    const z = -HD + (j / rows) * D;
    return [x, heightAt(x, z), z];
  };
  const a = new THREE.Vector3();
  const b = new THREE.Vector3();
  const c = new THREE.Vector3();
  const colour = new THREE.Color();
  const tri = (pa: [number, number, number], pb: [number, number, number], pc: [number, number, number]) => {
    a.set(...pa);
    b.set(...pb);
    c.set(...pc);
    const normal = b.clone().sub(a).cross(c.clone().sub(a)).normalize();
    groundColour((a.x + b.x + c.x) / 3, (a.z + b.z + c.z) / 3, (a.y + b.y + c.y) / 3, normal.y, colour);
    positions.push(...pa, ...pb, ...pc);
    for (let k = 0; k < 3; k++) colours.push(colour.r, colour.g, colour.b);
  };
  for (let j = 0; j < rows; j++) {
    for (let i = 0; i < cols; i++) {
      const [p00, p10, p01, p11] = [p(i, j), p(i + 1, j), p(i, j + 1), p(i + 1, j + 1)];
      // Alternate the diagonal: the facets don't line up into stripes.
      if ((i + j) % 2) {
        tri(p00, p01, p10);
        tri(p10, p01, p11);
      } else {
        tri(p00, p01, p11);
        tri(p00, p11, p10);
      }
    }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(colours, 3));
  g.computeVertexNormals();
  const material = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.95, flatShading: true });
  extendShader(material, 'valley-marea', (shader) => {
    Object.assign(shader.uniforms, { mareaFront: marea });
    shader.vertexShader = shader.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 vMapPos;').replace('#include <begin_vertex>', '#include <begin_vertex>\nvMapPos = position;');
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', '#include <common>\nvarying vec3 vMapPos;\nuniform float mareaFront;')
      .replace(
        '#include <color_fragment>',
        `#include <color_fragment>
        float mareaEdge = mareaFront + sin(vMapPos.x * 1.7) * 0.35 + sin(vMapPos.x * 4.1 + 1.3) * 0.15;
        float mareaIn = smoothstep(mareaEdge - 0.05, mareaEdge + 0.5, vMapPos.z);
        diffuseColor.rgb = mix(diffuseColor.rgb, diffuseColor.rgb * vec3(0.36, 0.27, 0.32), mareaIn);`,
      )
      .replace(
        '#include <emissivemap_fragment>',
        `#include <emissivemap_fragment>
        totalEmissiveRadiance += vec3(0.85, 0.08, 0.05) * (1.0 - smoothstep(0.0, 0.16, abs(vMapPos.z - mareaEdge))) * 0.45;`,
      );
  });
  const mesh = new THREE.Mesh(g, material);
  mesh.receiveShadow = true;
  mesh.castShadow = true;
  mesh.matrixAutoUpdate = false;
  return mesh;
}

/** The diorama's sides: earth and rock down from the edge of the map. */
function skirt(): THREE.Mesh {
  const positions: number[] = [];
  const colours: number[] = [];
  const top = C(0x6b5a46);
  const low = C(0x3c3129);
  const bottom = -1.8;
  const edge: [number, number][] = [];
  const steps = 64;
  for (let i = 0; i <= steps; i++) edge.push([-HW + (i / steps) * W, -HD]);
  for (let i = 1; i <= steps; i++) edge.push([HW, -HD + (i / steps) * D]);
  for (let i = 1; i <= steps; i++) edge.push([HW - (i / steps) * W, HD]);
  for (let i = 1; i <= steps; i++) edge.push([-HW, HD - (i / steps) * D]);
  for (let i = 0; i + 1 < edge.length; i++) {
    const [ax, az] = edge[i]!;
    const [bx, bz] = edge[i + 1]!;
    const ha = Math.max(heightAt(ax, az), -0.04);
    const hb = Math.max(heightAt(bx, bz), -0.04);
    positions.push(ax, ha, az, ax, bottom, az, bx, hb, bz, bx, hb, bz, ax, bottom, az, bx, bottom, bz);
    for (const k of [top, low, top, top, low, low]) colours.push(k.r, k.g, k.b);
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(colours, 3));
  g.computeVertexNormals();
  const mesh = new THREE.Mesh(g, new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 1, flatShading: true, side: THREE.DoubleSide }));
  mesh.matrixAutoUpdate = false;
  return mesh;
}

/** Water: the river and the sea at one level, the reservoir higher up behind its dam. */
function water(): THREE.Group {
  const g = new THREE.Group();
  const material = new THREE.MeshStandardMaterial({ color: 0x4f8aa0, roughness: 0.25, metalness: 0.1, transparent: true, opacity: 0.88 });
  const sea = new THREE.Mesh(new THREE.PlaneGeometry(W, D).rotateX(-Math.PI / 2), material);
  sea.position.y = -0.04;
  const lake = new THREE.Mesh(new THREE.CircleGeometry(RESERVOIR.dam - 0.05, 40).rotateX(-Math.PI / 2), material);
  lake.position.set(RESERVOIR.x, RESERVOIR.level, RESERVOIR.z);
  for (const m of [sea, lake]) {
    m.receiveShadow = true;
    m.matrixAutoUpdate = false;
    m.updateMatrix();
    g.add(m);
  }
  return g;
}

/** A road as a ribbon over the ground, through its bend. */
function roadPoints(a: { x: number; z: number }, b: { x: number; z: number }, bend: [number, number]): THREE.Vector3[] {
  const curve = new THREE.QuadraticBezierCurve(new THREE.Vector2(a.x, a.z), new THREE.Vector2(...bend), new THREE.Vector2(b.x, b.z));
  return curve.getSpacedPoints(Math.ceil(curve.getLength() / 0.22)).map((p) => new THREE.Vector3(p.x, Math.max(heightAt(p.x, p.y), 0.02) + 0.03, p.y));
}

function roadRibbon(points: THREE.Vector3[], width: number): THREE.BufferGeometry {
  const positions: number[] = [];
  for (let i = 0; i + 1 < points.length; i++) {
    const a = points[i]!;
    const b = points[i + 1]!;
    const side = new THREE.Vector3(b.z - a.z, 0, a.x - b.x).normalize().multiplyScalar(width / 2);
    const [a0, a1, b0, b1] = [a.clone().add(side), a.clone().sub(side), b.clone().add(side), b.clone().sub(side)];
    positions.push(...a0.toArray(), ...a1.toArray(), ...b0.toArray(), ...b0.toArray(), ...a1.toArray(), ...b1.toArray());
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  g.computeVertexNormals();
  return g;
}

// ------------------------------------------------------------- markers

const glows = new Map<string, THREE.Material>();
function glow(color: number, intensity = 1.4, opacity = 1, additive = false): THREE.Material {
  const key = `${color}:${intensity}:${opacity}:${additive}`;
  let m = glows.get(key);
  if (!m) {
    m = new THREE.MeshStandardMaterial({ color, emissive: color, emissiveIntensity: intensity, transparent: opacity < 1, opacity, depthWrite: opacity >= 1, blending: additive ? THREE.AdditiveBlending : THREE.NormalBlending });
    glows.set(key, m);
  }
  return m;
}

const SHAPES = {
  pad: new THREE.CylinderGeometry(0.3, 0.34, 0.06, 24),
  beacon: new THREE.CylinderGeometry(0.035, 0.035, 1, 8).translate(0, 0.5, 0),
  nest: new THREE.ConeGeometry(0.26, 0.42, 7).translate(0, 0.21, 0),
  offer: new THREE.OctahedronGeometry(0.15, 0),
  rumour: new THREE.SphereGeometry(0.11, 12, 8),
  hit: new THREE.SphereGeometry(0.75, 8, 6),
};
const PAD_GREY = new THREE.MeshStandardMaterial({ color: 0x8a8c88, roughness: 0.8 });
const HIT = new THREE.MeshBasicMaterial({ visible: false });

interface Marker {
  region: RegionId;
  group: THREE.Group;
  label: CSS2DObject;
}

/** How far the map pans (drag, focus) across and towards the camera: north is open, south its edge shows soon. */
const PAN_X: [number, number] = [-8, 8];
const PAN_Z: [number, number] = [-1.5, 7];

/** The light after the scene (stage.ts): a little warmer and richer than the base, the shade cool. */
const MAP_MOOD: StageMood = {
  exposure: 1.12,
  bloom: { strength: 0.42, threshold: 0.85 },
  grade: { saturation: 1.02, contrast: 1.06, vignette: 0.3, shadowTint: [0.93, 0.95, 1.08], highlightTint: [1.05, 1.0, 0.93], lift: [0.055, 0.055, 0.075] },
};

/**
 * The valley map: the eight zones with their markers, the Marea, La Mula on
 * the roads. Drag to pan it, click a zone's marker to select it.
 */
export class GeoscapeSet implements StageSet {
  readonly scene = new THREE.Scene();
  /** No GTAO: at the map's scale it changes next to nothing and costs a whole pass of the scene. */
  readonly ambientOcclusion = false;
  readonly mood = MAP_MOOD;
  /** The diorama, panned under the camera (drag, focus). */
  private readonly valley = new THREE.Group();
  private readonly markerGroup = new THREE.Group();
  private readonly markers: Marker[] = [];
  private readonly statics = new StaticBatch(8);
  private readonly ring: THREE.Mesh;
  private readonly scanRing: THREE.Mesh;
  private readonly bus = new THREE.Group();
  private readonly busLabel: CSS2DObject;
  private readonly clouds = new THREE.Group();
  private readonly sun = new THREE.DirectionalLight(0xffdcb4, 3.0);
  /** The Marea's front, in map z (south edge: none yet; Atalaya: the end); in the main menu, a little of it. */
  private readonly marea = { value: THREE.MathUtils.lerp(HD + 1, ATALAYA.z, 0.15) };
  private mareaGoal = this.marea.value;
  /** The road graph's polylines, by both ends. */
  private readonly roads = new Map<string, THREE.Vector3[]>();
  /** La Mula's way to its zone, how far along it is, and where it is now. */
  private path: THREE.Vector3[] = [];
  private along = 0;
  private busRegion: RegionId = HOME_REGION;
  private moving = false;
  /** In the main menu La Mula drives from zone to zone, stopping a while at each. */
  private idle = 0;
  private readonly pan = new THREE.Vector2();
  private readonly panGoal = new THREE.Vector2();
  private inCampaign = false;
  private scanning = 0;
  private lastKey = '';
  private shadowsStale = true;

  constructor(private readonly onPick: (region: RegionId) => void) {
    // A clear spring afternoon over the valley: a low, warm sun from the west, a cool sky in the shade.
    this.scene.background = skyTexture(['#7d92bd', '#b6c1d8', '#ecd7c4']);
    this.scene.fog = new THREE.Fog(0xd8d2d4, 34, 70);
    this.scene.add(new THREE.HemisphereLight(0xb7c3e2, 0x87806f, 1.25));
    this.sun.position.set(-20, 15, 8);
    this.sun.castShadow = true;
    this.sun.shadow.mapSize.set(2048, 2048);
    this.sun.shadow.bias = -0.0005;
    this.sun.shadow.normalBias = 0.02;
    Object.assign(this.sun.shadow.camera, { left: -20, right: 20, top: 16, bottom: -16, near: 1, far: 70 });
    this.scene.add(this.sun, this.sun.target);
    // A sea of cloud far under the diorama.
    const cloudSea = new THREE.Mesh(new THREE.CircleGeometry(80, 32).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0xe6e2e6, fog: true }));
    cloudSea.position.y = -6;
    this.scene.add(cloudSea);

    this.valley.add(terrain(this.marea), skirt(), water(), this.markerGroup, this.bus, this.statics.group, this.clouds);
    this.scene.add(this.valley);
    // The roads, one mesh; their polylines for La Mula.
    const ribbons: THREE.BufferGeometry[] = [];
    for (const [a, b, bend] of ROADS) {
      const points = roadPoints(ZONES[a], ZONES[b], bend);
      this.roads.set(`${a}|${b}`, points);
      this.roads.set(`${b}|${a}`, points.slice().reverse());
      ribbons.push(roadRibbon(points, 0.13));
    }
    const roads = new THREE.Mesh(mergeGeometries(ribbons), new THREE.MeshStandardMaterial({ color: 0x8a7a64, roughness: 1, polygonOffset: true, polygonOffsetFactor: -2 }));
    for (const r of ribbons) r.dispose();
    roads.receiveShadow = true;
    roads.matrixAutoUpdate = false;
    this.valley.add(roads);

    // The selected zone's ring and the scan's ring, flat on the ground.
    this.ring = new THREE.Mesh(new THREE.RingGeometry(0.46, 0.56, 40).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, depthWrite: false, toneMapped: false }));
    this.ring.visible = false;
    this.scanRing = new THREE.Mesh(new THREE.RingGeometry(0.3, 0.36, 40).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0x54d1ff, transparent: true, opacity: 0, depthWrite: false, toneMapped: false }));
    this.valley.add(this.ring, this.scanRing);

    const name = document.createElement('div');
    name.className = 'globe-label ship';
    name.textContent = 'La Mula';
    name.style.fontWeight = '700';
    name.style.borderLeft = '3px solid #ffb020';
    this.busLabel = new CSS2DObject(name);
    this.busLabel.position.set(0, 0.55, 0);
    this.busLabel.visible = false;
    this.bus.add(this.busLabel);
    this.placeBus(HOME_REGION);
    void loadBaseKit().then(() => this.build());
  }

  shot(): Shot {
    // The wheel looks closer (towards the pointer); a drag pans the map.
    return { pos: new THREE.Vector3(-1.4, 17.5, 19.5), target: new THREE.Vector3(-1.4, 0, 2.2), fov: 40, zoom: { min: 0.4, reach: 9 } };
  }

  /** Pans the map so a zone comes towards the middle (never so far that its south edge shows). */
  focus(region: RegionId): void {
    const z = ZONES[region];
    this.panGoal.set(THREE.MathUtils.clamp(-z.x * 0.5, ...PAN_X), THREE.MathUtils.clamp(-z.z * 0.4, ...PAN_Z));
  }

  /** La Mula drives to `region` (missions take it there), along the roads. */
  flyTo(region: RegionId): void {
    this.inCampaign = true;
    this.drive(region);
  }

  /** La Mula along the roads to `region`, from wherever it is. */
  private drive(region: RegionId): void {
    if (region === this.busRegion && !this.moving) return;
    const route = this.route(this.busRegion, region);
    this.busRegion = region;
    if (!route.length) return this.placeBus(region);
    this.path = route;
    this.along = 0;
    this.moving = true;
  }

  /** Scanning: a ring pulses out from La Mula. */
  setScanning(on: boolean): void {
    this.scanning = on ? 1.6 : 0;
  }

  update(c: CampaignState, selected: RegionId | null): void {
    this.inCampaign = true;
    // The Marea: from beyond the south edge (none) to Atalaya (the end).
    const k = THREE.MathUtils.clamp(c.doom / DOOM_MAX, 0, 1);
    this.mareaGoal = THREE.MathUtils.lerp(HD + 1, ATALAYA.z, k);
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
      const at = ZONES[r.id];
      const group = new THREE.Group();
      group.position.set(at.x, Math.max(heightAt(at.x, at.z), 0) + 0.04, at.z);
      group.userData.hotspot = r.id;
      const pad = new THREE.Mesh(SHAPES.pad, r.contacted ? glow(0x3fa9ff, 0.9) : PAD_GREY);
      pad.castShadow = true;
      group.add(pad);
      // A beam of light over the zone: tall and blue where we have contact, short and grey where not.
      const beam = new THREE.Mesh(SHAPES.beacon, r.contacted ? glow(0x3fa9ff, 1.6, 0.75, true) : glow(0x9aa4ad, 0.6, 0.6, true));
      beam.scale.y = r.contacted ? 1.3 : 0.55;
      group.add(beam);
      if (r.facility) {
        // A Nido: a dark mound glowing red.
        const nest = new THREE.Mesh(SHAPES.nest, glow(0x8a1010, 1.2));
        nest.position.set(0.42, 0, 0.05);
        nest.userData.pulse = 3;
        group.add(nest);
      }
      const offers = c.offers.filter((o) => o.region === r.id);
      offers.forEach((o, i) => {
        // Story chapters glow blue, like the pen on the Expediente; side quests green.
        const tone = o.final ? 0xff2a5a : o.story ? 0x4a7dff : o.quest ? 0x3fd68a : 0xff9a10;
        const diamond = new THREE.Mesh(SHAPES.offer, glow(tone, 1.5));
        diamond.position.set(-0.36 - i * 0.3, 0.42, 0.1);
        diamond.userData.spin = true;
        diamond.castShadow = true;
        group.add(diamond);
      });
      // A rumour floats over its zone, brighter while the Bastion listens to it.
      const rumor = c.rumors.find((x) => x.region === r.id);
      if (rumor) {
        const listening = rumor.id === c.listening;
        const orb = new THREE.Mesh(SHAPES.rumour, glow(listening ? 0x7dffb0 : 0xfff0c0, listening ? 1.9 : 1.2));
        orb.position.set(0.36, 0.8, -0.1);
        orb.userData.pulse = listening ? 5 : 2.5;
        group.add(orb);
      }
      group.add(new THREE.Mesh(SHAPES.hit, HIT));
      const el = document.createElement('div');
      el.className = `globe-label${r.contacted ? ' contacted' : ''}${r.facility ? ' facility' : ''}${r.id === selected ? ' selected' : ''}`;
      el.textContent = `${def.name}${offers.length ? ` · ${offers.length}` : ''}${rumor ? ' · ?' : ''}`;
      const tag = new CSS2DObject(el);
      tag.position.set(0, 1.0, 0);
      group.add(tag);
      this.markerGroup.add(group);
      this.markers.push({ region: r.id, group, label: tag });
    }
    const sel = selected ? ZONES[selected] : null;
    this.ring.visible = !!sel;
    if (sel) this.ring.position.set(sel.x, Math.max(heightAt(sel.x, sel.z), 0) + 0.12, sel.z);
    this.shadowsStale = true;
  }

  hits(): THREE.Object3D[] {
    return this.markers.map((m) => m.group);
  }

  pick(hot: string): void {
    this.onPick(hot as RegionId);
  }

  /** Dragging pans the map (within its edges). */
  drag(dx: number, dy: number): boolean {
    this.panGoal.x = THREE.MathUtils.clamp(this.panGoal.x + dx * 0.03, ...PAN_X);
    this.panGoal.y = THREE.MathUtils.clamp(this.panGoal.y + dy * 0.03, ...PAN_Z);
    return true;
  }

  /** Before each frame (stage.ts): the shadow map only when something in it moved (La Mula casts none). */
  beforeRender(gl: THREE.WebGLRenderer): void {
    if (this.shadowsStale) {
      gl.shadowMap.needsUpdate = true;
      this.shadowsStale = false;
    }
  }

  tick(dt: number, time: number): void {
    // The map eases to where it is panned.
    // While it moves, the shadow map moves with it.
    if (this.pan.distanceToSquared(this.panGoal) > 1e-6) {
      this.pan.lerp(this.panGoal, 1 - Math.exp(-dt * 5));
      if (this.pan.distanceToSquared(this.panGoal) < 1e-4) this.pan.copy(this.panGoal);
      this.shadowsStale = true;
    }
    this.valley.position.set(this.pan.x, 0, this.pan.y);
    this.sun.target.position.set(this.pan.x, 0, this.pan.y);
    this.sun.position.set(this.pan.x - 20, 15, this.pan.y + 8);
    // The Marea creeps to where the zombi activity puts it.
    this.marea.value += (this.mareaGoal - this.marea.value) * (1 - Math.exp(-dt * 1.5));
    for (const m of this.markers) {
      for (const child of m.group.children) {
        if (child.userData.spin) child.rotation.y = time * 2;
        if (child.userData.pulse) child.scale.setScalar(1 + Math.sin(time * child.userData.pulse) * 0.12);
      }
    }
    if (this.ring.visible) (this.ring.material as THREE.MeshBasicMaterial).opacity = 0.6 + Math.sin(time * 4) * 0.3;
    // La Mula along its road, nose first.
    if (this.moving && this.path.length > 1) {
      const length = this.path.length - 1;
      this.along = Math.min(length, this.along + dt * 12);
      const i = Math.min(length - 1, Math.floor(this.along));
      const a = this.path[i]!;
      const b = this.path[i + 1]!;
      this.bus.position.lerpVectors(a, b, this.along - i);
      this.bus.rotation.y = Math.atan2(b.x - a.x, b.z - a.z);
      if (this.along >= length) this.moving = false;
    } else if (!this.inCampaign && (this.idle += dt) > 3) {
      this.idle = 0;
      const next = ROADS.flatMap(([a, b]) => (a === this.busRegion ? [b] : b === this.busRegion ? [a] : []));
      this.drive(next[Math.floor(hash(time, 7) * next.length)]!);
    }
    this.busLabel.visible = this.inCampaign;
    // The scan's ring spreads from La Mula.
    const pulse = this.scanning > 0 ? (time * 1.2) % 1 : 0;
    this.scanRing.position.copy(this.bus.position).setY(this.bus.position.y + 0.08);
    this.scanRing.scale.setScalar(1 + pulse * 6);
    (this.scanRing.material as THREE.MeshBasicMaterial).opacity = this.scanning > 0 ? 0.9 * (1 - pulse) : 0;
    if (this.scanning > 0) this.scanning = Math.max(0, this.scanning - dt);
    // Clouds drift east over the sierra and come back round.
    for (const cloud of this.clouds.children) {
      cloud.position.x += dt * (0.25 + (cloud.userData.speed as number));
      if (cloud.position.x > HW + 4) cloud.position.x = -HW - 4;
    }
  }

  // ------------------------------------------------------------- inner

  /** La Mula parked at a zone, beside its marker. */
  private placeBus(region: RegionId): void {
    const z = ZONES[region];
    const x = z.x + 0.55;
    const zz = z.z + 0.45;
    this.bus.position.set(x, Math.max(heightAt(x, zz), 0) + 0.02, zz);
    this.moving = false;
  }

  /** The roads from one zone to another (fewest roads first), as one polyline; empty if none. */
  private route(from: RegionId, to: RegionId): THREE.Vector3[] {
    if (from === to) return [];
    const prev = new Map<RegionId, RegionId>([[from, from]]);
    const queue: RegionId[] = [from];
    while (queue.length) {
      const at = queue.shift()!;
      if (at === to) break;
      for (const [a, b] of ROADS) {
        const next = a === at ? b : b === at ? a : null;
        if (next && !prev.has(next)) {
          prev.set(next, at);
          queue.push(next);
        }
      }
    }
    if (!prev.has(to)) return [];
    const zones: RegionId[] = [to];
    while (zones[0] !== from) zones.unshift(prev.get(zones[0]!)!);
    const points: THREE.Vector3[] = [];
    for (let i = 0; i + 1 < zones.length; i++) points.push(...(this.roads.get(`${zones[i]}|${zones[i + 1]}`) ?? []));
    // From where it stands to the road, and off the road to its parking beside the marker.
    const end = ZONES[to];
    const park = new THREE.Vector3(end.x + 0.55, Math.max(heightAt(end.x + 0.55, end.z + 0.45), 0) + 0.02, end.z + 0.45);
    return [this.bus.position.clone(), ...points, park];
  }

  /** The models: Atalaya and the zones' towns, farms, mines and works, the forests and peaks, La Mula and the clouds. */
  private build(): void {
    const kit = new KitBatch();
    const put = (id: BaseModelId, x: number, z: number, o: { fit?: number; s?: number; sy?: number; yaw?: number; y?: number; finish?: Finish } = {}) => {
      const size = kitModel(id)?.size;
      if (!size) return;
      const s = o.fit ? o.fit / Math.max(size.x, size.z) : (o.s ?? 1) * MINI;
      kit.put(id, { x, z, y: o.y ?? Math.max(heightAt(x, z), 0) - 0.01, yaw: o.yaw ?? 0, s, sy: o.sy, finish: o.finish ?? 'map' });
    };
    const scatter = (cx: number, cz: number, r: number, n: number, seed: number, each: (x: number, z: number, k: number) => void) => {
      for (let k = 0; k < n; k++) {
        const a = hash(seed, k) * Math.PI * 2;
        const d = Math.sqrt(hash(k, seed + 7)) * r;
        each(cx + Math.cos(a) * d, cz + Math.sin(a) * d, k);
      }
    };

    // The sierra's peaks along the north edge.
    const ranges: BaseModelId[] = ['range1', 'range2', 'range3', 'range4'];
    for (let i = 0; i < 11; i++) {
      const x = -HW + 1.2 + i * 2.75 + (hash(i, 3) - 0.5) * 1.2;
      const z = -HD + 1.4 + hash(i, 5) * 1.4;
      put(ranges[i % 4]!, x, z, { s: 3.4 + hash(i, 9) * 1.6, sy: 2.2 + hash(i, 11) * 1.2, yaw: hash(i, 13) * 6.3, y: heightAt(x, z) - 0.6, finish: 'mapMountain' });
    }

    // Atalaya on its shelf: the bunker, its mast, two lookouts, the water tower; the dam beside it is the reservoir's.
    const ax = ATALAYA.x;
    const az = ATALAYA.z;
    put('bunker', ax, az, { s: 1.8, yaw: Math.PI, finish: 'mapSnow' });
    put('mast', ax - 0.75, az - 0.4, { s: 1.6, finish: 'mapSnow' });
    put('waterTower', ax + 0.9, az - 0.25, { s: 1.2, finish: 'mapSnow' });
    for (const [dx, dz] of [
      [-1.0, 0.55],
      [1.0, 0.6],
    ] as const)
      put('lookout', ax + dx, az + dz, { s: 1.5, finish: 'mapSnow' });
    put('armyTent', ax + 0.1, az + 0.8, { s: 1.3, finish: 'mapSnow' });
    for (let t = -1.2; t <= 1.2; t += 0.2) put('jersey', ax + t, az + 1.2, { s: 1.2, finish: 'mapSnow' });

    // Altomonte's villages under the station.
    const village = (cx: number, cz: number, seed: number, snow: boolean) => {
      const houses: BaseModelId[] = ['mapHouse', 'mapHouseSquare', 'mapHouseL', 'mapHouse'];
      scatter(cx, cz, 0.9, 6, seed, (x, z, k) => put(houses[k % houses.length]!, x, z, { fit: 0.38, yaw: hash(k, seed) * 6.3, finish: snow ? 'mapSnow' : 'map' }));
      put('mapChurch', cx + 0.2, cz - 0.2, { fit: 0.5, yaw: 0.4, finish: snow ? 'mapSnow' : 'map' });
    };
    village(ZONES.weu.x - 1.0, ZONES.weu.z + 0.5, 11, true);
    // Valdelobos: villages in the closed valleys.
    village(ZONES.eeu.x - 0.6, ZONES.eeu.z + 0.6, 23, false);
    village(ZONES.eeu.x + 3.2, ZONES.eeu.z - 1.0, 29, false);

    // El Pantano Gris: the dam across the reservoir's lip, the dead power station below it, a line of pylons.
    const gap = Math.atan2(RIVER[0]![1] - RESERVOIR.z, RIVER[0]![0] - RESERVOIR.x);
    for (let i = 0; i < 10; i++) {
      const a = gap + (i / 9 - 0.5) * 1.1;
      const block = new THREE.Vector3(RESERVOIR.x + Math.cos(a) * RESERVOIR.dam, -0.3, RESERVOIR.z + Math.sin(a) * RESERVOIR.dam);
      kit.prim(UNIT, DAM, new THREE.Matrix4().compose(block, new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), -a), new THREE.Vector3(0.24, RESERVOIR.level + 0.45, 0.32)));
    }
    put('mapCooling', 12.6, 2.6, { fit: 1.0 });
    put('mapChimney', 11.6, 3.2, { s: 0.9 });
    put('mapWarehouse', 13.6, 1.4, { fit: 0.9, yaw: 0.3 });
    for (let i = 0; i < 5; i++) {
      const x = RESERVOIR.x - 2 - i * 2.6;
      const z = RESERVOIR.z + 5.2 + i * 0.5;
      put('pylon', x, z, { s: 0.45, yaw: 1.4 });
    }

    // Carboneda: mine mouths in the western hills, spoil heaps and a works shed.
    for (const [dx, dz, yaw] of [
      [-0.9, -1.2, 0.6],
      [0.8, -1.5, -0.3],
    ] as const)
      put('mapMine', ZONES.sa.x + dx, ZONES.sa.z + dz, { fit: 0.5, yaw, y: heightAt(ZONES.sa.x + dx, ZONES.sa.z + dz) - 0.1 });
    put('siteSpoil', ZONES.sa.x - 0.2, ZONES.sa.z - 0.2, { s: 1.6 });
    put('siteSpoil', ZONES.sa.x + 1.2, ZONES.sa.z + 0.4, { s: 1.2, yaw: 1 });
    put('mapWarehouse', ZONES.sa.x + 0.9, ZONES.sa.z + 1.3, { fit: 0.8, yaw: -0.5 });
    put('siteDozer', ZONES.sa.x - 1.0, ZONES.sa.z + 0.6, { s: 0.9, yaw: 0.8 });

    // La Vega: farms along the river, fields and orchards.
    const vx = ZONES.oce.x;
    const vz = ZONES.oce.z;
    put('mapBarn', vx - 1.1, vz - 0.9, { fit: 0.6, yaw: 0.2 });
    put('mapSilo', vx - 0.5, vz - 1.2, { fit: 0.22 });
    put('mapSilo', vx - 0.25, vz - 1.15, { fit: 0.22 });
    put('mapHouse', vx + 0.9, vz - 0.8, { fit: 0.4, yaw: -0.3 });
    for (let i = 0; i < 10; i++) {
      const x = vx - 3 + (i % 5) * 1.3 + hash(i, 41) * 0.3;
      const z = vz + 0.1 + Math.floor(i / 5) * 1.25 + hash(i, 43) * 0.3;
      if (distToPolyline(x, z, RIVER) < 0.7) continue;
      put(i % 3 ? 'mapCrops' : 'mapField', x, z, { fit: 0.8, yaw: Math.round(hash(i, 47) * 4) * (Math.PI / 2) });
    }
    scatter(vx + 2.6, vz - 1.4, 0.8, 9, 53, (x, z) => put('mapTree', x, z, { fit: 0.22 }));

    // Ciudad Varela: the first city to fall, a grid of dead blocks.
    const city = (cx: number, cz: number, cols: number, rows: number, seed: number, kinds: BaseModelId[]) => {
      for (let j = 0; j < rows; j++)
        for (let i = 0; i < cols; i++) {
          const x = cx + (i - (cols - 1) / 2) * 0.75;
          const z = cz + (j - (rows - 1) / 2) * 0.75;
          // A square left open round the zone's marker.
          if (Object.values(ZONES).some((p) => Math.hypot(p.x - x, p.z - z) < 0.6)) continue;
          put(kinds[Math.floor(hash(i + seed, j) * kinds.length)]!, x, z, { fit: 0.55, yaw: Math.round(hash(j, i + seed) * 4) * (Math.PI / 2) });
        }
    };
    city(ZONES.afr.x, ZONES.afr.z, 4, 3, 61, ['mapFlats', 'mapOffice', 'mapShop', 'mapFactory', 'mapMotel', 'mapHouseSquare']);
    // Puerto Galena: the dead city and the Instituto Galena by the river's mouth, warehouses on the bank, the lighthouse.
    put('mapFactory', ZONES.me.x + 1.0, ZONES.me.z - 0.6, { fit: 0.9, yaw: 0.2 });
    city(ZONES.me.x + 0.4, ZONES.me.z + 0.5, 3, 2, 71, ['mapFlats', 'mapOffice', 'mapShop', 'mapHouseSquare']);
    put('mapWarehouse', ZONES.me.x - 0.8, ZONES.me.z + 1.6, { fit: 0.7, yaw: 0.5 });
    put('mapLighthouse', 12.4, 8.6, { fit: 0.35 });

    // Los Llanos de Ceniza: dead trees and wrecks on the ash.
    scatter(ZONES.na.x, ZONES.na.z, 3.2, 14, 81, (x, z, k) => put(k % 4 ? 'pineDead' : 'rustyCar', x, z, { s: k % 4 ? 1.1 : 0.9, yaw: hash(k, 83) * 6.3 }));

    // Forests on the slopes (snowy higher up), copses in the lowlands, crags in the hills.
    const pines: BaseModelId[] = ['pine', 'pineSmall', 'pineTall'];
    for (let k = 0; k < 420; k++) {
      const x = -HW + 0.4 + hash(k, 101) * (W - 0.8);
      const z = -HD + 0.4 + hash(k, 103) * (D - 0.8);
      const h = heightAt(x, z);
      if (h < 0.55 || h > 3.0 || fbm(x * 0.5 + 3, z * 0.5) < 0.45) continue;
      if (Object.values(ZONES).some((p) => Math.hypot(p.x - x, p.z - z) < 1.3) || Math.hypot(ATALAYA.x - x, ATALAYA.z - z) < 1.6) continue;
      put(pines[k % 3]!, x, z, { s: 1.0 + hash(k, 107) * 0.5, yaw: hash(k, 109) * 6.3, finish: h > 1.7 ? 'mapSnow' : 'map' });
    }
    for (let k = 0; k < 160; k++) {
      const x = -HW + 1 + hash(k, 113) * (W - 2);
      const z = -5 + hash(k, 117) * (HD + 4);
      const h = heightAt(x, z);
      if (h < 0.06 || h > 1.3 || fbm(x * 0.6, z * 0.6 + 4) < 0.5 || distToPolyline(x, z, RIVER) < 0.45) continue;
      if (Object.values(ZONES).some((p) => Math.hypot(p.x - x, p.z - z) < 1.6)) continue;
      if (ashen(x, z)) continue;
      put(k % 2 ? 'mapTrees' : 'mapTreesWide', x, z, { fit: 0.7, yaw: hash(k, 119) * 6.3 });
    }
    const crags: BaseModelId[] = ['crag', 'crag2', 'crag3', 'crag4'];
    for (let k = 0; k < 40; k++) {
      const x = -HW + 1 + hash(k, 121) * (W - 2);
      const z = -HD + 1 + hash(k, 123) * (D - 2);
      const h = heightAt(x, z);
      if (h < 1.2 || h > 3.4) continue;
      put(crags[k % 4]!, x, z, { s: 0.8 + hash(k, 127) * 0.8, yaw: hash(k, 129) * 6.3, finish: 'mapMountain', y: h - 0.05 });
    }

    this.statics.begin(null);
    kit.addTo(this.statics);
    this.statics.commit();

    // La Mula: the base's bus, merged (nothing moves on it at this size), nose along +z. It casts no
    // shadow: at this size none shows, and the shadow map need not be drawn again as it drives.
    const bus = kitModel('mulaBus');
    if (bus) {
      const parts = new StaticBatch(50);
      parts.begin(null);
      const still = new THREE.Matrix4();
      for (const part of kitMesh('mulaBus', 'map').children) parts.add((part as THREE.Mesh).geometry, (part as THREE.Mesh).material as THREE.Material, still, false);
      parts.commit();
      parts.group.scale.setScalar(0.62 / bus.size.z);
      this.bus.add(parts.group);
    }

    // Clouds drifting over the sierra (no shadows: they move all the time).
    const cloudIds: BaseModelId[] = ['mapCloud', 'mapCloud2', 'mapCloud3'];
    for (let i = 0; i < 7; i++) {
      const g = new KitBatch();
      g.put(cloudIds[i % 3]!, { s: 0.22 + hash(i, 131) * 0.12, finish: 'map' });
      const cloud = g.build(false);
      cloud.position.set(-HW + hash(i, 133) * W, 5.2 + hash(i, 137) * 1.6, -HD + 1 + hash(i, 139) * 9);
      cloud.userData.speed = hash(i, 141) * 0.2;
      this.clouds.add(cloud);
    }
    this.shadowsStale = true;
  }
}

/** A unit box standing on y = 0 (the dam's blocks). */
const UNIT = new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0);
const DAM = new THREE.MeshStandardMaterial({ color: 0xa3a39e, roughness: 0.9 });
