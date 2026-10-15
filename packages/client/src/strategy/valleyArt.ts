import { LANDMARKS as NAMES, REGIONS, ROADS, type RegionId } from '@bastion/engine';
import { GEOGRAPHY as G } from './valleyGeography';

// The valley's map as Arturo chose it (15/10, «el Ilustrado»; his Fortnite
// reference, Ana's #21): a painted game map seen from straight above, of the
// geography of valleyGeography.ts with what makes it a place: a reservoir with
// its shore, arms and dam, a river wandering to the sea, fields in parcels with
// their rows, woods tree by tree, and every zone a settlement with its streets
// and houses and a landmark one knows it by (Atalaya walled with its helipad and
// dam lake; Aldren's chalets round the church, and Telm; Volkren and its toll;
// Carvik's terraces and the open pit; the power station under the dam and the
// Pico Gris; La Vega's square and wineries; Corran's grid with its hospital;
// Galen's port and the Instituto's domes; the truck stop and the radio on Los
// Llanos). Only the ground: what changes (the Marea, the markers, the names)
// is drawn live over it (geoscape.ts). It is baked once into tiles at three
// scales (VALLEY_TILES; lab/valleyMaps.ts bakes them into
// public/maps/valle/), since nothing on it ever changes.

type Pt = [number, number];

// ------------------------------------------------------------ random and noise

function seeded(seed: number): () => number {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const hash2 = (x: number, z: number) => {
  const s = Math.sin(x * 127.1 + z * 311.7) * 43758.5453;
  return s - Math.floor(s);
};
function vnoise(x: number, z: number): number {
  const xi = Math.floor(x);
  const zi = Math.floor(z);
  const xf = x - xi;
  const zf = z - zi;
  const u = xf * xf * (3 - 2 * xf);
  const v = zf * zf * (3 - 2 * zf);
  const a = hash2(xi, zi);
  const b = hash2(xi + 1, zi);
  const c = hash2(xi, zi + 1);
  const d = hash2(xi + 1, zi + 1);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}
function fbm(x: number, z: number, octaves = 4): number {
  let s = 0;
  let amp = 0.5;
  let f = 1;
  let n = 0;
  for (let i = 0; i < octaves; i++) {
    s += amp * vnoise(x * f, z * f);
    n += amp;
    amp *= 0.5;
    f *= 2.03;
  }
  return s / n;
}
const smooth = (a: number, b: number, x: number) => {
  const t = Math.min(1, Math.max(0, (x - a) / (b - a)));
  return t * t * (3 - 2 * t);
};

// ------------------------------------------------------------ lines

/** A Catmull-Rom curve through `pts`, a point every `step` or so. */
function curve(pts: Pt[], step = 0.06): Pt[] {
  if (pts.length < 3) return pts.slice();
  const out: Pt[] = [];
  for (let i = 0; i + 1 < pts.length; i++) {
    const p0 = pts[Math.max(0, i - 1)]!;
    const p1 = pts[i]!;
    const p2 = pts[i + 1]!;
    const p3 = pts[Math.min(pts.length - 1, i + 2)]!;
    const n = Math.max(1, Math.ceil(Math.hypot(p2[0] - p1[0], p2[1] - p1[1]) / step));
    for (let k = 0; k < n; k++) {
      const t = k / n;
      const t2 = t * t;
      const t3 = t2 * t;
      const f = (a: number, b: number, c: number, d: number) => 0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
      out.push([f(p0[0], p1[0], p2[0], p3[0]), f(p0[1], p1[1], p2[1], p3[1])]);
    }
  }
  out.push(pts[pts.length - 1]!);
  return out;
}
function distSeg(x: number, z: number, a: Pt, b: Pt): number {
  const dx = b[0] - a[0];
  const dz = b[1] - a[1];
  const l2 = dx * dx + dz * dz || 1e-9;
  const t = Math.min(1, Math.max(0, ((x - a[0]) * dx + (z - a[1]) * dz) / l2));
  return Math.hypot(x - a[0] - dx * t, z - a[1] - dz * t);
}
function distLine(x: number, z: number, line: Pt[]): number {
  let best = Infinity;
  for (let i = 0; i + 1 < line.length; i++) best = Math.min(best, distSeg(x, z, line[i]!, line[i + 1]!));
  return best;
}
function lengthOf(line: Pt[]): number {
  let l = 0;
  for (let i = 0; i + 1 < line.length; i++) l += Math.hypot(line[i + 1]![0] - line[i]![0], line[i + 1]![1] - line[i]![1]);
  return l;
}
/** The point `s` along a line, and its heading. */
function at(line: Pt[], s: number): { p: Pt; a: number } {
  for (let i = 0; i + 1 < line.length; i++) {
    const a = line[i]!;
    const b = line[i + 1]!;
    const l = Math.hypot(b[0] - a[0], b[1] - a[1]);
    if (s <= l || i + 2 === line.length) {
      const t = l ? Math.min(1, s / l) : 0;
      return { p: [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t], a: Math.atan2(b[1] - a[1], b[0] - a[0]) };
    }
    s -= l;
  }
  return { p: line[0]!, a: 0 };
}


// ------------------------------------------------------------ the land

const H = (x: number, z: number) => G.heightAt(x, z);

/** The reservoir: its shore out from its middle by angle, with the side valleys it flooded, and the dam at its outlet. */
const LAKE = { x: G.reservoir.x, z: G.reservoir.z };
const DAM_ANGLE = Math.atan2(G.river[0]![1] - LAKE.z, G.river[0]![0] - LAKE.x);
const ARMS: [number, number, number][] = [
  [-2.3, 1.2, 0.32],
  [-1.15, 0.7, 0.26],
  [0.35, 0.6, 0.34],
  [2.85, 0.7, 0.3],
];
const angDiff = (a: number, b: number) => Math.atan2(Math.sin(a - b), Math.cos(a - b));
function lakeR(a: number): number {
  let r = 2.85 + 0.55 * (fbm(Math.cos(a) * 1.6 + 5, Math.sin(a) * 1.6 + 5) - 0.5);
  for (const [ang, len, wid] of ARMS) {
    const d = angDiff(a, ang);
    r += len * Math.exp(-(d * d) / (wid * wid));
  }
  // Straight across the gap where the dam holds it.
  const d = Math.abs(angDiff(a, DAM_ANGLE));
  if (d < 0.32) r = 2.95 / Math.cos(d);
  return r;
}
const LAKE_SHORE: Pt[] = Array.from({ length: 360 }, (_, k): Pt => {
  const a = (k / 360) * Math.PI * 2;
  const r = lakeR(a);
  return [LAKE.x + Math.cos(a) * r, LAKE.z + Math.sin(a) * r];
});
const inLake = (x: number, z: number) => Math.hypot(x - LAKE.x, z - LAKE.z) < lakeR(Math.atan2(z - LAKE.z, x - LAKE.x));
const DAM: Pt[] = [-0.3, 0, 0.3].map((d): Pt => [LAKE.x + Math.cos(DAM_ANGLE + d) * 2.98, LAKE.z + Math.sin(DAM_ANGLE + d) * 2.98]);
/** Atalaya's own little dam lake, up beside the station. */
const TARN = { x: -2.35, z: -7.45, r: 0.42 };
const inTarn = (x: number, z: number) => Math.hypot((x - TARN.x) / 1.3, z - TARN.z) < TARN.r * (1 + 0.15 * (vnoise(x * 4, z * 4) - 0.5));
const inSea = (x: number, z: number) => G.seaDistance(x, z) < 6.5 && H(x, z) < -0.32;

/** The river from the dam to the sea, wandering, wider as it goes. */
const RIVER = curve(
  G.river.map(([x, z], i) => {
    const w = i === 0 || i === G.river.length - 1 ? 0 : 0.35;
    return [x + (vnoise(i * 3.1, 7) - 0.5) * w, z + (vnoise(i * 2.3, 11) - 0.5) * w] as Pt;
  }),
  0.05,
);
const RIVER_LEN = lengthOf(RIVER);
const riverWidth = (t: number) => 0.13 + 0.2 * t;

/** The roads (the engine's, as the map draws them), and the one that climbs to Atalaya. */
const ROAD_LINES: { a: RegionId; b: RegionId; line: Pt[] }[] = ROADS.map(([a, b]) => ({ a, b, line: curve(G.road(a, b) as Pt[], 0.06) }));
const CLIMB: Pt[] = curve([
  [-4.6, -5.4],
  [-4.25, -5.85],
  [-4.75, -6.2],
  [-4.2, -6.5],
  [-3.9, -6.85],
  [-3.4, -7.0],
]);

// ------------------------------------------------------------ what stands on it

type Kind = 'house' | 'chalet' | 'terrace' | 'block' | 'tower' | 'civic' | 'church' | 'shed' | 'hall' | 'hospital' | 'lab' | 'barracks' | 'kiosk' | 'ruin' | 'truck' | 'canopy' | 'booth';
interface House {
  x: number;
  z: number;
  w: number;
  d: number;
  a: number;
  kind: Kind;
  tone: number;
  zone: RegionId | 'atalaya';
}
type RoundKind = 'dome' | 'tank' | 'cooling' | 'helipad' | 'antenna' | 'heap' | 'fountain' | 'crane';
interface Round {
  x: number;
  z: number;
  r: number;
  kind: RoundKind;
  zone: RegionId | 'atalaya';
}
type AreaKind = 'square' | 'park' | 'yard' | 'pit' | 'dock' | 'compound' | 'lot' | 'garden' | 'cemetery' | 'switchyard';
interface Area {
  pts: Pt[];
  kind: AreaKind;
  zone: RegionId | 'atalaya';
  level?: number;
}
type WayKind = 'street' | 'avenue' | 'lane' | 'track' | 'rail' | 'wall' | 'pier' | 'fence' | 'barrier';
interface Way {
  line: Pt[];
  w: number;
  kind: WayKind;
  zone: RegionId | 'atalaya';
}
interface Tree {
  x: number;
  z: number;
  r: number;
  tone: number;
  conifer: boolean;
}

const F = { houses: [] as House[], rounds: [] as Round[], areas: [] as Area[], ways: [] as Way[], trees: [] as Tree[] };

// What is taken, on a grid of 0.025: roads, streets, buildings, water. Trees and houses go where it's free.
const OCC = 0.025;
const OX = -34;
const OZ = -22;
const OW = 2720;
const OH = 1920;
let occ = new Uint8Array(0);
function mark(x: number, z: number, r: number): void {
  const i0 = Math.floor((x - r - OX) / OCC);
  const i1 = Math.floor((x + r - OX) / OCC);
  const j0 = Math.floor((z - r - OZ) / OCC);
  const j1 = Math.floor((z + r - OZ) / OCC);
  for (let j = Math.max(0, j0); j <= Math.min(OH - 1, j1); j++)
    for (let i = Math.max(0, i0); i <= Math.min(OW - 1, i1); i++) {
      const cx = OX + (i + 0.5) * OCC - x;
      const cz = OZ + (j + 0.5) * OCC - z;
      if (cx * cx + cz * cz <= r * r + OCC * OCC * 0.25) occ[j * OW + i] = 1;
    }
}
function markLine(line: Pt[], r: number): void {
  for (let i = 0; i + 1 < line.length; i++) {
    const a = line[i]!;
    const b = line[i + 1]!;
    const n = Math.ceil(Math.hypot(b[0] - a[0], b[1] - a[1]) / (OCC * 0.6));
    for (let k = 0; k <= n; k++) mark(a[0] + ((b[0] - a[0]) * k) / n, a[1] + ((b[1] - a[1]) * k) / n, r);
  }
}
function free(x: number, z: number, r: number): boolean {
  const i0 = Math.floor((x - r - OX) / OCC);
  const i1 = Math.floor((x + r - OX) / OCC);
  const j0 = Math.floor((z - r - OZ) / OCC);
  const j1 = Math.floor((z + r - OZ) / OCC);
  for (let j = j0; j <= j1; j++)
    for (let i = i0; i <= i1; i++) {
      const cx = OX + (i + 0.5) * OCC - x;
      const cz = OZ + (j + 0.5) * OCC - z;
      if (cx * cx + cz * cz > r * r + OCC * OCC * 0.5) continue;
      if (i < 0 || j < 0 || i >= OW || j >= OH || occ[j * OW + i]) return false;
    }
  return true;
}
const wet = (x: number, z: number) => inLake(x, z) || inTarn(x, z) || inSea(x, z) || distLine(x, z, RIVER) < 0.22;
/** How steep the ground is at (x, z). */
const slope = (x: number, z: number) => Math.hypot(H(x + 0.05, z) - H(x - 0.05, z), H(x, z + 0.05) - H(x, z - 0.05)) / 0.1;


function house(h: House, pad = 0.008): boolean {
  if (wet(h.x, h.z) || !free(h.x, h.z, Math.min(h.w, h.d) * 0.45 + pad)) return false;
  F.houses.push(h);
  mark(h.x, h.z, Math.max(h.w, h.d) * 0.45 + pad);
  return true;
}
function addWay(w: Way, keep = 0.008): void {
  F.ways.push(w);
  markLine(w.line, w.w / 2 + keep);
}
function addArea(a: Area): void {
  F.areas.push(a);
  const cx = a.pts.reduce((s, p) => s + p[0], 0) / a.pts.length;
  const cz = a.pts.reduce((s, p) => s + p[1], 0) / a.pts.length;
  const r = Math.max(...a.pts.map((p) => Math.hypot(p[0] - cx, p[1] - cz)));
  for (let j = -r; j <= r; j += OCC) for (let i = -r; i <= r; i += OCC) if (inPoly(cx + i, cz + j, a.pts)) mark(cx + i, cz + j, OCC / 2);
}
function inPoly(x: number, z: number, pts: Pt[]): boolean {
  let inside = false;
  for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
    const [xi, zi] = pts[i]!;
    const [xj, zj] = pts[j]!;
    if (zi > z !== zj > z && x < ((xj - xi) * (z - zi)) / (zj - zi) + xi) inside = !inside;
  }
  return inside;
}
/** An oriented rectangle's corners. */
function rect(cx: number, cz: number, w: number, d: number, a: number): Pt[] {
  const c = Math.cos(a);
  const s = Math.sin(a);
  return [
    [-w / 2, -d / 2],
    [w / 2, -d / 2],
    [w / 2, d / 2],
    [-w / 2, d / 2],
  ].map(([u, v]) => [cx + u! * c - v! * s, cz + u! * s + v! * c] as Pt);
}

/** Houses both sides of a street, facing it, from `from` to `to` along it. */
function lineHouses(line: Pt[], zone: House['zone'], o: { kind: Kind; spacing: number; w: [number, number]; d: [number, number]; back: number; seed: number; skip?: number; from?: number; to?: number }): void {
  const R = seeded(o.seed);
  const L = lengthOf(line);
  for (let s = (o.from ?? 0) + o.spacing * 0.5; s < (o.to ?? L) - o.spacing * 0.3; s += o.spacing * (0.85 + R() * 0.3)) {
    const { p, a } = at(line, s);
    for (const side of [-1, 1]) {
      if (R() < (o.skip ?? 0.06)) continue;
      const w = o.w[0] + R() * (o.w[1] - o.w[0]);
      const d = o.d[0] + R() * (o.d[1] - o.d[0]);
      const off = o.back + d / 2 + R() * 0.015;
      const nx = -Math.sin(a) * side;
      const nz = Math.cos(a) * side;
      house({ x: p[0] + nx * off, z: p[1] + nz * off, w, d, a: a + (R() - 0.5) * 0.08, kind: o.kind, tone: R(), zone });
    }
  }
}

/** The stretches of the roads within `r` of `c`: a village's main street is the road that runs through it. */
function roadsNear(c: Pt, r: number): Pt[][] {
  const out: Pt[][] = [];
  for (const road of [...ROAD_LINES.map((x) => x.line), CLIMB]) {
    let cur: Pt[] = [];
    for (const p of road) {
      if (Math.hypot(p[0] - c[0], p[1] - c[1]) < r) cur.push(p);
      else if (cur.length) {
        if (cur.length > 2) out.push(cur);
        cur = [];
      }
    }
    if (cur.length > 2) out.push(cur);
  }
  return out;
}

/**
 * A village round `c`: its houses along the roads through it, side streets
 * off them with more, a square at its middle with its church or hall.
 */
function village(zone: House['zone'], c: Pt, o: { r: number; branches: number; seed: number; kind?: Kind; spacing?: number; church?: boolean; square?: boolean; dir?: number }): void {
  const R = seeded(o.seed);
  const kind = o.kind ?? 'house';
  const spacing = o.spacing ?? 0.165;
  let mains = roadsNear(c, o.r);
  if (!mains.length) {
    const a = o.dir ?? R() * Math.PI;
    const line = curve([-1, -0.5, 0, 0.5, 1].map((t) => [c[0] + Math.cos(a) * t * o.r + Math.sin(t * 2 + o.seed) * 0.05, c[1] + Math.sin(a) * t * o.r] as Pt), 0.04);
    addWay({ line, w: 0.055, kind: 'street', zone });
    mains = [line];
  }
  if (o.square !== false) {
    const sq = rect(c[0] + 0.03, c[1] - 0.17, 0.36, 0.26, (o.dir ?? 0) + 0.1);
    addArea({ pts: sq, kind: 'square', zone });
    if (o.church) house({ x: c[0] + 0.06, z: c[1] - 0.41, w: 0.28, d: 0.12, a: (o.dir ?? 0) + 0.1, kind: 'church', tone: R(), zone }, 0.005);
    F.rounds.push({ x: c[0] + 0.03, z: c[1] - 0.17, r: 0.045, kind: 'fountain', zone });
  }
  const streets: Pt[][] = [];
  for (let k = 0; k < o.branches; k++) {
    const main = mains[k % mains.length]!;
    const L = lengthOf(main);
    const { p, a } = at(main, L * (0.2 + (0.6 * (k + 0.5)) / o.branches) + (R() - 0.5) * 0.1);
    const side = k % 2 ? 1 : -1;
    const dir = a + (Math.PI / 2) * side + (R() - 0.5) * 0.5;
    const len = o.r * (0.45 + R() * 0.4);
    const pts: Pt[] = [];
    for (let t = 0; t <= 1.0001; t += 0.25) pts.push([p[0] + Math.cos(dir) * len * t + Math.sin(t * 3 + k) * 0.03, p[1] + Math.sin(dir) * len * t]);
    const line = curve(pts, 0.04);
    if (line.some(([x, z]) => wet(x, z))) continue;
    addWay({ line, w: 0.045, kind: 'street', zone }, 0.01);
    streets.push(line);
  }
  for (const [n, main] of mains.entries()) lineHouses(main, zone, { kind, spacing, w: [0.12, 0.17], d: [0.09, 0.12], back: 0.1, seed: o.seed + n * 7 });
  for (const [n, s] of streets.entries()) lineHouses(s, zone, { kind, spacing: spacing * 1.05, w: [0.11, 0.15], d: [0.085, 0.11], back: 0.05, seed: o.seed + 100 + n * 13, from: 0.12 });
  // A second row behind the first along the main street, and a few farmsteads out along the roads.
  for (const [n, main] of mains.entries()) lineHouses(main, zone, { kind, spacing: spacing * 1.2, w: [0.11, 0.15], d: [0.085, 0.11], back: 0.27, seed: o.seed + 200 + n * 3, skip: 0.4 });
  for (const [n, main] of mains.entries()) lineHouses(main, zone, { kind, spacing: 0.4, w: [0.13, 0.18], d: [0.09, 0.12], back: 0.11, seed: o.seed + 300 + n, skip: 0.7 });
}

/**
 * A city: a grid of blocks turned `angle` round `c`, its nodes nudged so
 * nothing is ruled straight, avenues every few streets; most blocks built
 * round a yard, some a park, a square or a car park, one the hospital (or
 * whatever `special` puts there); its edge ragged.
 */
function city(zone: RegionId, c: Pt, o: { cols: number; rows: number; cell: number; angle: number; seed: number; special: (i: number, j: number) => Kind | AreaKind | null }): void {
  const R = seeded(o.seed);
  const ux = Math.cos(o.angle);
  const uz = Math.sin(o.angle);
  const vx = -uz;
  const vz = ux;
  const W = o.cols * o.cell;
  const D = o.rows * o.cell;
  const node = (i: number, j: number): Pt => {
    const u = i * o.cell - W / 2 + (hash2(i + o.seed, j) - 0.5) * 0.06;
    const v = j * o.cell - D / 2 + (hash2(i, j + o.seed) - 0.5) * 0.06;
    return [c[0] + ux * u + vx * v, c[1] + uz * u + vz * v];
  };
  const kept = (i: number, j: number) => {
    const edge = i === 0 || j === 0 || i === o.cols - 1 || j === o.rows - 1;
    const p = node(i + 0.5, j + 0.5);
    if (wet(p[0], p[1])) return false;
    return !edge || hash2(i * 3 + o.seed, j * 5) > 0.35;
  };
  // Streets: every grid line, as far as the blocks beside it go; avenues every third.
  for (let i = 0; i <= o.cols; i++) {
    const pts: Pt[] = [];
    for (let j = 0; j <= o.rows; j++) pts.push(node(i, j));
    addWay({ line: pts, w: i % 3 === 0 ? 0.075 : 0.05, kind: i % 3 === 0 ? 'avenue' : 'street', zone }, 0.005);
  }
  for (let j = 0; j <= o.rows; j++) {
    const pts: Pt[] = [];
    for (let i = 0; i <= o.cols; i++) pts.push(node(i, j));
    addWay({ line: pts, w: j % 3 === 0 ? 0.075 : 0.05, kind: j % 3 === 0 ? 'avenue' : 'street', zone }, 0.005);
  }
  for (let j = 0; j < o.rows; j++)
    for (let i = 0; i < o.cols; i++) {
      if (!kept(i, j)) continue;
      const inset = 0.05;
      const corners = [node(i, j), node(i + 1, j), node(i + 1, j + 1), node(i, j + 1)];
      const mid: Pt = [(corners[0]![0] + corners[2]![0]) / 2, (corners[0]![1] + corners[2]![1]) / 2];
      const block = corners.map(([x, z]) => [x + (mid[0] - x) * (inset / (o.cell * 0.7)), z + (mid[1] - z) * (inset / (o.cell * 0.7))] as Pt);
      const special = o.special(i, j);
      if (special === 'park' || special === 'square' || special === 'lot' || special === 'yard' || special === 'dock') {
        addArea({ pts: block, kind: special, zone });
        if (special === 'park') {
          for (let t = 0; t < 14; t++) {
            const u = 0.15 + R() * 0.7;
            const v = 0.15 + R() * 0.7;
            const x = block[0]![0] + (block[1]![0] - block[0]![0]) * u + (block[3]![0] - block[0]![0]) * v;
            const z = block[0]![1] + (block[1]![1] - block[0]![1]) * u + (block[3]![1] - block[0]![1]) * v;
            F.trees.push({ x, z, r: 0.04 + R() * 0.03, tone: R(), conifer: false });
          }
        }
        continue;
      }
      if (special === 'hospital' || special === 'hall' || special === 'tower') {
        addArea({ pts: block, kind: 'yard', zone });
        const s = o.cell - inset * 2.6;
        if (special === 'hospital') {
          // An H: two wings and the block between.
          for (const [du, w, d] of [
            [-0.32, 0.16, 0.9],
            [0.32, 0.16, 0.9],
            [0, 0.5, 0.2],
          ] as const)
            house({ x: mid[0] + ux * du * s, z: mid[1] + uz * du * s, w: w * s, d: d * s, a: o.angle, kind: 'hospital', tone: 0, zone }, 0);
        } else house({ x: mid[0], z: mid[1], w: s * 0.8, d: s * 0.6, a: o.angle, kind: special === 'hall' ? 'hall' : 'tower', tone: R(), zone }, 0);
        continue;
      }
      // Built round a yard: each side split into buildings of its own.
      addArea({ pts: block, kind: 'garden', zone });
      for (let e = 0; e < 4; e++) {
        const a = block[e]!;
        const b = block[(e + 1) % 4]!;
        const len = Math.hypot(b[0] - a[0], b[1] - a[1]);
        const n = Math.max(2, Math.round(len / (0.13 + R() * 0.06)));
        const dirA = Math.atan2(b[1] - a[1], b[0] - a[0]);
        const nx = -Math.sin(dirA);
        const nz = Math.cos(dirA);
        // Inward: towards the block's middle.
        const inward = (mid[0] - a[0]) * nx + (mid[1] - a[1]) * nz > 0 ? 1 : -1;
        for (let k = 0; k < n; k++) {
          const t0 = (k + 0.04) / n;
          const t1 = (k + 0.96) / n;
          const depth = 0.1 + R() * 0.05;
          const mx = a[0] + (b[0] - a[0]) * ((t0 + t1) / 2) + nx * inward * depth / 2;
          const mz = a[1] + (b[1] - a[1]) * ((t0 + t1) / 2) + nz * inward * depth / 2;
          // The corners are left to the side running across, so they don't overlap.
          const shrink = k === 0 || k === n - 1 ? 0.75 : 1;
          F.houses.push({ x: mx, z: mz, w: len * (t1 - t0) * shrink, d: depth, a: dirA, kind: R() < 0.08 ? 'tower' : 'block', tone: R(), zone });
        }
      }
      mark(mid[0], mid[1], o.cell * 0.45);
    }
  // Its outskirts: houses along the roads out of it.
  for (const line of roadsNear(c, Math.max(W, D) * 0.95)) lineHouses(line, zone, { kind: 'house', spacing: 0.13, w: [0.09, 0.12], d: [0.07, 0.09], back: 0.095, seed: o.seed + 9, skip: 0.3 });
}

// ------------------------------------------------------------ the zones

const Z = (id: RegionId): Pt => [REGIONS[id].x, REGIONS[id].z];

/** Atalaya: the old military station on its shelf, walled, its barracks, hangar, helipad and mast; its dam lake beside. */
function atalaya(): void {
  const c: Pt = [G.atalaya.x, G.atalaya.z];
  const wall: Pt[] = [
    [c[0] - 0.55, c[1] - 0.3],
    [c[0] + 0.25, c[1] - 0.45],
    [c[0] + 0.6, c[1] - 0.05],
    [c[0] + 0.45, c[1] + 0.4],
    [c[0] - 0.2, c[1] + 0.48],
    [c[0] - 0.62, c[1] + 0.15],
  ];
  addArea({ pts: wall, kind: 'compound', zone: 'atalaya' });
  addWay({ line: [...wall, wall[0]!], w: 0.03, kind: 'wall', zone: 'atalaya' });
  for (const [dx, dz, w, d, kind] of [
    [-0.28, -0.12, 0.34, 0.09, 'barracks'],
    [-0.28, 0.04, 0.34, 0.09, 'barracks'],
    [0.22, -0.18, 0.3, 0.2, 'hall'],
    [0.05, 0.25, 0.22, 0.1, 'civic'],
  ] as const)
    F.houses.push({ x: c[0] + dx, z: c[1] + dz, w, d, a: -0.12, kind, tone: 0.3, zone: 'atalaya' });
  F.rounds.push({ x: c[0] + 0.36, z: c[1] + 0.16, r: 0.08, kind: 'helipad', zone: 'atalaya' });
  F.rounds.push({ x: c[0] - 0.45, z: c[1] + 0.25, r: 0.05, kind: 'antenna', zone: 'atalaya' });
  F.rounds.push({ x: c[0] - 0.05, z: c[1] - 0.32, r: 0.05, kind: 'tank', zone: 'atalaya' });
}

function zones(): void {
  atalaya();
  // Aldren: the sierra's villages, chalets round a church, and two hamlets.
  village('weu', Z('weu'), { r: 1.1, branches: 6, seed: 11, kind: 'chalet', church: true });
  village('weu', [G.towns[0]!.x, G.towns[0]!.z], { r: 0.55, branches: 2, seed: 12, kind: 'chalet', square: false });
  village('weu', [G.towns[1]!.x, G.towns[1]!.z], { r: 0.5, branches: 2, seed: 13, kind: 'chalet', square: false });
  // Volkren: villages in the closed valleys, a filling station on the road down.
  village('eeu', Z('eeu'), { r: 0.95, branches: 5, seed: 21, church: true });
  village('eeu', [G.towns[2]!.x, G.towns[2]!.z], { r: 0.55, branches: 2, seed: 22, square: false });
  village('eeu', [G.towns[3]!.x, G.towns[3]!.z], { r: 0.45, branches: 2, seed: 23, square: false });
  toll();
  // Carvik: the miners' terraces, the open pit to the west, its spoil heaps, the headframe and the track.
  village('sa', Z('sa'), { r: 0.95, branches: 6, seed: 31, kind: 'terrace', spacing: 0.14 });
  village('sa', [G.towns[4]!.x, G.towns[4]!.z], { r: 0.55, branches: 3, seed: 32, kind: 'terrace', square: false });
  const pit: Pt = [-11.75, -1.55];
  for (let level = 0; level < 5; level++) {
    const r = 1.0 - level * 0.18;
    addArea({ pts: Array.from({ length: 48 }, (_, k) => {
      const a = (k / 48) * Math.PI * 2;
      const rr = r * (1 + 0.12 * Math.sin(a * 3 + 1) + 0.06 * Math.sin(a * 5));
      return [pit[0] + Math.cos(a) * rr * 1.25, pit[1] + Math.sin(a) * rr] as Pt;
    }), kind: 'pit', zone: 'sa', level });
  }
  F.rounds.push({ x: -10.55, z: -2.55, r: 0.22, kind: 'heap', zone: 'sa' }, { x: -12.9, z: 0.05, r: 0.28, kind: 'heap', zone: 'sa' }, { x: -10.3, z: -0.95, r: 0.06, kind: 'crane', zone: 'sa' });
  house({ x: -10.55, z: -1.15, w: 0.24, d: 0.12, a: 0.3, kind: 'shed', tone: 0.2, zone: 'sa' });
  addWay({ line: curve([[-10.45, -1.0], [-10.15, -0.75], [-9.95, -0.55]]), w: 0.03, kind: 'rail', zone: 'sa' });
  // El Pantano Gris: the dead power station below the dam, its switchyard and cooling tower; the repeater on the Pico Gris.
  village('eas', Z('eas'), { r: 0.65, branches: 3, seed: 41, square: false });
  const below: Pt = [G.river[0]![0] - 0.3, G.river[0]![1] + 0.5];
  house({ x: below[0], z: below[1], w: 0.5, d: 0.2, a: -0.4, kind: 'hall', tone: 0.5, zone: 'eas' }, 0);
  addArea({ pts: rect(below[0] - 0.55, below[1] + 0.25, 0.42, 0.3, -0.4), kind: 'switchyard', zone: 'eas' });
  F.rounds.push({ x: below[0] + 0.45, z: below[1] + 0.3, r: 0.2, kind: 'cooling', zone: 'eas' }, { x: 7.5, z: -0.75, r: 0.06, kind: 'antenna', zone: 'eas' });
  // La Vega: the farming town round its square, its wineries out among the fields.
  village('oce', Z('oce'), { r: 1.2, branches: 7, seed: 51, church: true });
  for (const [x, z, a] of [
    [0.4, 2.35, 0.2],
    [2.9, 0.55, -0.3],
    [2.3, 2.6, 0.5],
  ] as const) {
    F.houses.push({ x, z: z - 0.05, w: 0.36, d: 0.14, a, kind: 'hall', tone: 0.7, zone: 'oce' });
    F.houses.push({ x: x + 0.12, z: z + 0.1, w: 0.14, d: 0.1, a: a + Math.PI / 2, kind: 'hall', tone: 0.7, zone: 'oce' });
    addArea({ pts: rect(x, z, 0.5, 0.36, a), kind: 'yard', zone: 'oce' });
  }
  // Corran: the first city to fall, its hospital at the middle.
  city('afr', Z('afr'), { cols: 8, rows: 6, cell: 0.6, angle: 0.1, seed: 61, special: (i, j) => (i === 4 && j === 2 ? 'hospital' : i === 2 && j === 3 ? 'park' : i === 3 && j === 3 ? 'square' : (i * 7 + j * 3) % 11 === 0 ? 'lot' : null) });
  // Galen: the dead city by the river, its port, and the Instituto Galen's domes on the hill to the east.
  const gc: Pt = [REGIONS.me.x + 0.25, REGIONS.me.z - 0.3];
  city('me', gc, { cols: 6, rows: 5, cell: 0.6, angle: -0.22, seed: 71, special: (i, j) => (i === 1 && j === 1 ? 'park' : i === 3 && j === 2 ? 'square' : j === 4 && i >= 3 ? 'dock' : null) });
  for (let k = 0; k < 5; k++) {
    const { p, a } = at(RIVER, RIVER_LEN * (0.73 + k * 0.025));
    const n = a + Math.PI / 2;
    addWay({ line: [p, [p[0] + Math.cos(n) * 0.35, p[1] + Math.sin(n) * 0.35]], w: 0.05, kind: 'pier', zone: 'me' });
    if (k % 2 === 0) F.rounds.push({ x: p[0] + Math.cos(n) * 0.25, z: p[1] + Math.sin(n) * 0.25, r: 0.05, kind: 'crane', zone: 'me' });
  }
  const inst: Pt = [11.05, 5.05];
  addArea({ pts: rect(inst[0], inst[1], 1.0, 0.7, -0.15), kind: 'compound', zone: 'me' });
  addWay({ line: [...rect(inst[0], inst[1], 1.0, 0.7, -0.15), rect(inst[0], inst[1], 1.0, 0.7, -0.15)[0]!], w: 0.02, kind: 'fence', zone: 'me' });
  for (const [dx, dz, r] of [
    [-0.22, -0.1, 0.13],
    [0.12, -0.12, 0.09],
    [0.28, 0.12, 0.07],
  ] as const)
    F.rounds.push({ x: inst[0] + dx, z: inst[1] + dz, r, kind: 'dome', zone: 'me' });
  house({ x: inst[0] - 0.1, z: inst[1] + 0.2, w: 0.42, d: 0.12, a: -0.15, kind: 'lab', tone: 0, zone: 'me' }, 0);
  // Los Llanos de Ceniza: the truck stop, the Congregación's radio, burnt farms out on the ash.
  const stop: Pt = [REGIONS.na.x + 0.4, REGIONS.na.z - 0.35];
  addArea({ pts: rect(stop[0], stop[1], 0.7, 0.42, 0.15), kind: 'lot', zone: 'na' });
  for (let k = 0; k < 4; k++) F.houses.push({ x: stop[0] - 0.22 + k * 0.14, z: stop[1] + 0.06, w: 0.05, d: 0.2, a: 0.15, kind: 'truck', tone: k / 4, zone: 'na' });
  F.houses.push({ x: stop[0] + 0.1, z: stop[1] - 0.14, w: 0.26, d: 0.09, a: 0.15, kind: 'kiosk', tone: 0, zone: 'na' });
  F.rounds.push({ x: 1.35, z: 8.05, r: 0.07, kind: 'antenna', zone: 'na' });
  house({ x: 1.2, z: 8.2, w: 0.14, d: 0.1, a: 0.4, kind: 'civic', tone: 0, zone: 'na' });
  const R = seeded(81);
  for (let k = 0; k < 9; k++) {
    const x = -4 + R() * 8;
    const z = 7.6 + R() * 2.2;
    for (let n = 0; n < 3; n++) house({ x: x + (R() - 0.5) * 0.25, z: z + (R() - 0.5) * 0.25, w: 0.08, d: 0.06, a: R() * 3, kind: 'ruin', tone: R(), zone: 'na' });
  }
}

/** Volkren's toll (LORE.md: the Cuervos charge the road): a booth beside the road down to Aldren, a barrier across it. */
const TOLL = (() => {
  const r = ROAD_LINES.find((x) => (x.a === 'eeu' && x.b === 'weu') || (x.a === 'weu' && x.b === 'eeu'))!;
  const line = r.a === 'eeu' ? r.line : r.line.slice().reverse();
  return at(line, 1.6);
})();
function toll(): void {
  const { p, a } = TOLL;
  const nx = -Math.sin(a);
  const nz = Math.cos(a);
  F.houses.push({ x: p[0] + nx * 0.17, z: p[1] + nz * 0.17, w: 0.12, d: 0.08, a, kind: 'booth', tone: 0, zone: 'eeu' });
  F.ways.push({ line: [[p[0] - nx * 0.11, p[1] - nz * 0.11], [p[0] + nx * 0.11, p[1] + nz * 0.11]], w: 0.025, kind: 'barrier', zone: 'eeu' });
  mark(p[0] + nx * 0.17, p[1] + nz * 0.17, 0.08);
}

// ------------------------------------------------------------ fields and woods

/**
 * Where the valley is farmed: round each farming zone, as far as `r`, with
 * what it grows; its parcels in strips turned `angle` (along its main road).
 */
type Crop = 'wheat' | 'green' | 'plough' | 'vine' | 'pasture' | 'burnt' | 'orchard';
const FARMS: { c: Pt; r: number; crops: Crop[]; angle: number }[] = [
  { c: [1.6, 2.1], r: 4.6, crops: ['wheat', 'green', 'plough', 'vine', 'wheat', 'green', 'orchard', 'pasture'], angle: 0.62 },
  { c: [8.3, -4.7], r: 1.7, crops: ['pasture', 'green', 'pasture', 'plough'], angle: -0.25 },
  { c: [-8.6, 1.4], r: 1.7, crops: ['plough', 'green', 'wheat'], angle: 1.15 },
  { c: [-5.2, -4.5], r: 1.3, crops: ['pasture', 'pasture', 'green'], angle: 0.2 },
  { c: [-4.4, 6.9], r: 1.8, crops: ['wheat', 'plough', 'green'], angle: 0.1 },
  { c: [-0.6, 8.6], r: 3.2, crops: ['burnt', 'burnt', 'plough'], angle: 0.15 },
  { c: [6.0, 3.4], r: 1.8, crops: ['green', 'wheat', 'orchard'], angle: 0.45 },
];
/** A parcel: what it grows, its own shade, how far (x, z) is from its edge, and where across its rows (0 to 1). */
interface Parcel {
  f: { crop: Crop; tone: number };
  edge: number;
  rows: number;
}
/** The parcel at (x, z) of the farm it is most in: strips 0.42 wide, cut into lengths of their own. */
function parcelAt(x: number, z: number): Parcel | null {
  let best = -1;
  let w = 0;
  FARMS.forEach((farm, k) => {
    const d = Math.hypot(x - farm.c[0], z - farm.c[1]) / farm.r;
    if (d < 1.15 && 1 - d > w) {
      w = 1 - d;
      best = k;
    }
  });
  if (best < 0) return null;
  const farm = FARMS[best]!;
  const ca = Math.cos(farm.angle);
  const sa = Math.sin(farm.angle);
  const u = (x - farm.c[0]) * ca + (z - farm.c[1]) * sa;
  const v = -(x - farm.c[0]) * sa + (z - farm.c[1]) * ca;
  // Strips of their own widths: the farm's columns, every one 0.3 to 0.55 across.
  let col = Math.floor(u / 0.42);
  let start = col * 0.42 + (hash2(col, best) - 0.5) * 0.12;
  if (u < start) {
    col--;
    start = col * 0.42 + (hash2(col, best) - 0.5) * 0.12;
  }
  const next = (col + 1) * 0.42 + (hash2(col + 1, best) - 0.5) * 0.12;
  const colW = next - start;
  const len = 0.22 + 0.34 * hash2(col, best * 13 + 1);
  const vv = v + hash2(col, best * 7 + 3) * len;
  const row = Math.floor(vv / len);
  const fu = (u - start) / colW;
  const fv = vv / len - row;
  const edge = Math.min(Math.min(fu, 1 - fu) * colW, Math.min(fv, 1 - fv) * len);
  // Neighbours often grow the same: the crop by a coarser patch than the parcel.
  const id = hash2(Math.floor(col / 2) * 3.1 + best, Math.floor(row / (1 + (col & 1))) * 1.7 + best * 5);
  const along = hash2(col + 0.5, row + best) > 0.5;
  return { f: { crop: farm.crops[Math.floor(id * farm.crops.length)]!, tone: hash2(row, col + 9) }, edge, rows: along ? fu : fv };
}
/** How farmed (x, z) is: fields only on the flat, away from the woods and the water. */
function farmland(x: number, z: number, h: number): number {
  let k = 0;
  for (const farm of FARMS) k = Math.max(k, smooth(farm.r, farm.r * 0.6, Math.hypot(x - farm.c[0], z - farm.c[1]) * (0.85 + 0.3 * vnoise(x * 0.8, z * 0.8))));
  return k * smooth(2.4, 1.6, h) * smooth(0.06, 0.0, wood(x, z) - 0.02);
}
/** How wooded (x, z) is: above 0 in the woods. */
const wood = (x: number, z: number) => G.woods(x, z) - G.woodsAbove + 0.02 * (vnoise(x * 3, z * 3) - 0.5);

function plantTrees(): void {
  const R = seeded(101);
  const step = 0.105;
  for (let z = VALLEY_TILES.z0 + 0.3; z < VALLEY_TILES.z1 - 0.3; z += step)
    for (let x = VALLEY_TILES.x0 + 0.3; x < VALLEY_TILES.x1 - 0.3; x += step) {
      const tx = x + (R() - 0.5) * step * 0.9;
      const tz = z + (R() - 0.5) * step * 0.9;
      const h = H(tx, tz);
      if (h > 3.9 || wet(tx, tz)) continue;
      const w = wood(tx, tz);
      const fl = farmland(tx, tz, h);
      const mountain = smooth(2.2, 3.0, h);
      // The woods, dense; the sierra's pines thinning out up towards the snow; lone trees in the meadows and along the fields.
      let p = w > 0.04 ? 0.95 : w > 0.015 ? 0.4 : mountain > 0 && h < 3.5 ? 0.35 * mountain : 0.003;
      if (fl > 0.5) {
        const fa = parcelAt(tx, tz);
        p = fa && fa.edge < 0.03 && fa.f.crop !== 'burnt' ? 0.09 : fa?.f.crop === 'orchard' ? 0.4 : 0;
      }
      if (G.ashDepth(tx, tz) > 0.1) p *= 0.15;
      if (R() > p) continue;
      const r = (w > 0 ? 0.075 : 0.062) + R() * 0.035;
      if (!free(tx, tz, r * 0.6)) continue;
      F.trees.push({ x: tx, z: tz, r, tone: R(), conifer: h > 1.6 || (w > 0 && fbm(tx * 0.7, tz * 0.7) > 0.55) });
    }
}

/** Lays out everything the map draws, the first time it is painted (the game only reads where the landmarks are). */
let laidOut = false;
function layOut(): void {
  if (laidOut) return;
  laidOut = true;
  occ = new Uint8Array(OW * OH);
  for (const r of ROAD_LINES) markLine(r.line, 0.075);
  markLine(CLIMB, 0.06);
  markLine(RIVER, 0.2);
  markLine(LAKE_SHORE.concat([LAKE_SHORE[0]!]), 0.08);
  zones();
  plantTrees();
}


// ------------------------------------------------------------ the view

export interface View {
  cx: number;
  cz: number;
  /** Pixels a unit. */
  k: number;
  w: number;
  h: number;
}
/** The whole valley on a 1440 × 900 screen, the scale the map's sizes are thought at. */
const WHOLE_K = 48;

let ctx: CanvasRenderingContext2D;
let V: View = { cx: 0, cz: 0, k: WHOLE_K, w: 1, h: 1 };
const px = (x: number) => (x - V.cx) * V.k + V.w / 2;
const py = (z: number) => (z - V.cz) * V.k + V.h / 2;
/** How much bigger than on the whole valley things are drawn (the detail's zoom), for line widths and sizes. */
const zoom = () => V.k / WHOLE_K;

type RGB = [number, number, number];
const hex = (h: number): RGB => [(h >> 16) & 255, (h >> 8) & 255, h & 255];
const mix = (a: RGB, b: RGB, t: number): RGB => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
const css = (c: RGB, a = 1) => `rgba(${Math.round(c[0])}, ${Math.round(c[1])}, ${Math.round(c[2])}, ${a})`;

/** The land, as each style paints a sample of it. */
interface Sample {
  x: number;
  z: number;
  h: number;
  /** Light on the slope: 0 in shade, 1 facing the light; 0.5 flat. */
  light: number;
  wood: number;
  ash: number;
  farm: number;
  field: Parcel | null;
  grain: number;
}

/** The land's samples at half the view's resolution, and the light on them (from the north-west). */
function sampleLand(): { S: Sample[]; GW: number; GH: number } {
  const GW = V.w / 2;
  const GH = V.h / 2;
  const hs = new Float32Array((GW + 2) * (GH + 2));
  const wx = (i: number) => V.cx + ((i * 2 + 1) / V.k) - V.w / 2 / V.k;
  const wz = (j: number) => V.cz + ((j * 2 + 1) / V.k) - V.h / 2 / V.k;
  for (let j = -1; j <= GH; j++) for (let i = -1; i <= GW; i++) hs[(j + 1) * (GW + 2) + i + 1] = H(wx(i), wz(j));
  const S: Sample[] = new Array(GW * GH);
  const cell = 2 / V.k;
  for (let j = 0; j < GH; j++)
    for (let i = 0; i < GW; i++) {
      const x = wx(i);
      const z = wz(j);
      const hh = (a: number, b: number) => hs[(b + 1) * (GW + 2) + a + 1]!;
      const h = hh(i, j);
      const dx = (hh(i + 1, j) - hh(i - 1, j)) / (2 * cell);
      const dz = (hh(i, j + 1) - hh(i, j - 1)) / (2 * cell);
      // Normal (−dx, 1, −dz) against the light from the north-west, up high.
      const n = Math.hypot(dx, 1, dz);
      const lx = -0.55;
      const ly = 0.72;
      const lz = -0.42;
      const light = Math.max(0, (-dx * lx + ly - dz * lz) / n / Math.hypot(lx, ly, lz));
      const farm = farmland(x, z, h);
      S[j * GW + i] = { x, z, h, light, wood: wood(x, z), ash: G.ashDepth(x, z), farm, field: farm > 0.05 ? parcelAt(x, z) : null, grain: hash2(i, j) };
    }
  return { S, GW, GH };
}

/** Paints the land from the samples, a colour each, drawn up to the view. */
function paintLand(land: { S: Sample[]; GW: number; GH: number }, colour: (s: Sample) => RGB): void {
  const half = document.createElement('canvas');
  half.width = land.GW;
  half.height = land.GH;
  const c = half.getContext('2d')!;
  const img = c.createImageData(land.GW, land.GH);
  for (let k = 0; k < land.S.length; k++) {
    const [r, g, b] = colour(land.S[k]!);
    img.data[k * 4] = r;
    img.data[k * 4 + 1] = g;
    img.data[k * 4 + 2] = b;
    img.data[k * 4 + 3] = 255;
  }
  c.putImageData(img, 0, 0);
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(half, 0, 0, V.w, V.h);
}

function polygon(pts: Pt[]): void {
  ctx.beginPath();
  pts.forEach(([x, z], k) => (k ? ctx.lineTo(px(x), py(z)) : ctx.moveTo(px(x), py(z))));
  ctx.closePath();
}
function stroke(line: Pt[], width: number, color: string, dash: number[] = []): void {
  ctx.beginPath();
  line.forEach(([x, z], k) => (k ? ctx.lineTo(px(x), py(z)) : ctx.moveTo(px(x), py(z))));
  ctx.lineWidth = width;
  ctx.strokeStyle = color;
  ctx.lineCap = ctx.lineJoin = 'round';
  ctx.setLineDash(dash);
  ctx.stroke();
  ctx.setLineDash([]);
}
/** The river, wider as it goes: in short stretches of their own width. */
function strokeRiver(extra: number, color: string): void {
  ctx.strokeStyle = color;
  ctx.lineCap = 'round';
  const L = RIVER_LEN;
  let s = 0;
  for (let i = 0; i + 1 < RIVER.length; i++) {
    const a = RIVER[i]!;
    const b = RIVER[i + 1]!;
    // Into the sea, it is the sea.
    if (inSea(a[0], a[1]) && inSea(b[0], b[1])) break;
    ctx.beginPath();
    ctx.moveTo(px(a[0]), py(a[1]));
    ctx.lineTo(px(b[0]), py(b[1]));
    ctx.lineWidth = (riverWidth(s / L) + extra) * V.k;
    ctx.stroke();
    s += Math.hypot(b[0] - a[0], b[1] - a[1]);
  }
}
function waterShapes(fill: string): void {
  ctx.fillStyle = fill;
  polygon(LAKE_SHORE);
  ctx.fill();
  ctx.beginPath();
  ctx.ellipse(px(TARN.x), py(TARN.z), TARN.r * 1.3 * V.k, TARN.r * V.k, 0, 0, Math.PI * 2);
  ctx.fill();
}
/** An oriented box on the map. */
function box(h: { x: number; z: number; w: number; d: number; a: number }, dx = 0, dz = 0): void {
  polygon(rect(h.x + dx, h.z + dz, h.w, h.d, h.a));
}
const inView = (x: number, z: number, m = 0.5) => Math.abs(x - V.cx) < V.w / 2 / V.k + m && Math.abs(z - V.cz) < V.h / 2 / V.k + m;


/** The places one knows the valley by, named small on the map (engine lore.ts LANDMARKS: Lore's names), and where. */
export const VALLEY_LANDMARKS: { key: keyof typeof NAMES; at: Pt }[] = [];
{
  const L = (key: keyof typeof NAMES, at: Pt) => VALLEY_LANDMARKS.push({ key, at });
  L('telm', [G.towns[1]!.x + 0.15, G.towns[1]!.z + 0.6]);
  L('mine', [-11.75, -0.35]);
  L('dam', [(DAM[0]![0] + DAM[2]![0]) / 2 + 0.55, (DAM[0]![1] + DAM[2]![1]) / 2 + 0.2]);
  L('plant', [G.river[0]![0] - 0.3, G.river[0]![1] + 0.85]);
  L('peak', [7.5, -1.05]);
  L('wineries', [2.95, 0.2]);
  L('toll', [TOLL.p[0] + 0.05, TOLL.p[1] + 0.42]);
  L('hospital', [-6.4, 5.5]);
  const port = at(RIVER, RIVER_LEN * 0.74).p;
  L('port', [port[0] + 0.25, port[1] + 0.55]);
  L('institute', [11.05, 5.6]);
  L('truckStop', [REGIONS.na.x + 1.25, REGIONS.na.z + 0.5]);
  L('station', [1.35, 7.7]);
}

/** The ground a settlement stands on: each building's lot, grown and run together, in `fill`. */
function townGround(fill: string, grow: number, blur: number): void {
  const layer = document.createElement('canvas');
  layer.width = V.w;
  layer.height = V.h;
  const g = layer.getContext('2d')!;
  g.fillStyle = fill;
  for (const h of F.houses) {
    if (h.kind === 'ruin' || h.kind === 'truck' || !inView(h.x, h.z, 1)) continue;
    const pts = rect(h.x, h.z, h.w + grow, h.d + grow, h.a);
    g.beginPath();
    pts.forEach(([x, z], k) => (k ? g.lineTo(px(x), py(z)) : g.moveTo(px(x), py(z))));
    g.closePath();
    g.fill();
  }
  ctx.save();
  ctx.filter = `blur(${blur}px)`;
  ctx.drawImage(layer, 0, 0);
  ctx.restore();
}


// ------------------------------------------------------------ the styles

/** Roof colours of each style, by kind and tone. */
interface Roofs {
  roof: (h: House) => RGB;
  shadow: string | null;
  ridge: number;
}

function drawWays(kinds: WayKind[], width: (w: Way) => number, color: (w: Way) => string): void {
  for (const w of F.ways) if (kinds.includes(w.kind) && w.line.some(([x, z]) => inView(x, z))) stroke(w.line, width(w), color(w));
}
function drawAreas(fill: (a: Area) => string | null, line?: (a: Area) => string | null): void {
  for (const a of F.areas) {
    if (!a.pts.some(([x, z]) => inView(x, z, 1))) continue;
    const f = fill(a);
    if (f) {
      polygon(a.pts);
      ctx.fillStyle = f;
      ctx.fill();
    }
    const l = line?.(a);
    if (l) {
      polygon(a.pts);
      ctx.lineWidth = Math.max(1, 0.012 * V.k);
      ctx.strokeStyle = l;
      ctx.stroke();
    }
  }
}
function drawHouses(r: Roofs): void {
  const visible = F.houses.filter((h) => inView(h.x, h.z));
  if (r.shadow) {
    ctx.fillStyle = r.shadow;
    const off = 0.022;
    for (const h of visible) {
      if (h.kind === 'ruin') continue;
      box(h, off * (h.kind === 'tower' ? 2.2 : 1), off * (h.kind === 'tower' ? 2.2 : 1));
      ctx.fill();
    }
  }
  for (const h of visible) {
    const c = r.roof(h);
    box(h);
    ctx.fillStyle = css(c);
    ctx.fill();
    // A pitched roof's ridge (and its two sides lit differently) on houses; flat ones a rim.
    if (r.ridge > 0 && (h.kind === 'house' || h.kind === 'chalet' || h.kind === 'terrace' || h.kind === 'church' || h.kind === 'barracks')) {
      const half = rect(h.x - (Math.sin(-h.a) * h.d) / 4, h.z - (Math.cos(h.a) * h.d) / 4, h.w, h.d / 2, h.a);
      polygon(half);
      ctx.fillStyle = css(mix(c, [0, 0, 0], r.ridge));
      ctx.fill();
    } else if (r.ridge > 0 && h.kind !== 'ruin' && h.kind !== 'truck') {
      box({ ...h, w: h.w * 0.78, d: h.d * 0.7 });
      ctx.fillStyle = css(mix(c, [255, 255, 255], r.ridge * 0.6));
      ctx.fill();
    }
  }
}


function illustrated(): void {
  ctx.fillStyle = '#5fae48';
  ctx.fillRect(0, 0, V.w, V.h);
  const land = sampleLand();
  const grass = hex(0x7cc456);
  const grass2 = hex(0x5fae48);
  const dry = hex(0xb7c35a);
  const forest = hex(0x3f8a3f);
  const rock = hex(0xa89c8e);
  const snow = hex(0xf6f8fb);
  const ash = hex(0xa9a49c);
  const ash2 = hex(0x8c8780);
  const sand = hex(0xe9dfb2);
  const sea = hex(0x3fb6d8);
  const deep = hex(0x2f8fc4);
  const crops: Record<Crop, RGB> = { wheat: hex(0xe8c860), green: hex(0x9fd666), plough: hex(0xc19865), vine: hex(0x93b85a), pasture: hex(0x8fd16a), burnt: hex(0x7b746c), orchard: hex(0x86c45a) };
  paintLand(land, (s) => {
    const n = fbm(s.x * 0.6, s.z * 0.6);
    let c = mix(grass, grass2, smooth(0.35, 0.7, n));
    c = mix(c, dry, smooth(0.62, 0.8, fbm(s.x * 0.3 + 9, s.z * 0.3)) * 0.6);
    if (s.wood > 0) c = mix(c, forest, smooth(0, 0.04, s.wood));
    if (s.field && s.farm > 0.3) {
      const crop = crops[s.field.f.crop];
      let f = mix(crop, [255, 255, 255], (s.field.f.tone - 0.5) * 0.14);
      // Its rows, where the view is near enough to tell them.
      if (V.k > 100 && s.field.f.crop !== 'pasture' && s.field.f.crop !== 'burnt') f = mix(f, [60, 50, 20], 0.12 * (0.5 + 0.5 * Math.sin(s.field.rows * Math.PI * 2 * (s.field.f.crop === 'vine' ? 7 : 10))));
      // Between parcels, a strip of grass.
      c = mix(c, f, smooth(0.3, 0.6, s.farm) * smooth(0.012, 0.024, s.field.edge));
    }
    // The sierra: rock, then snow.
    c = mix(c, rock, smooth(2.5, 3.0, s.h + (n - 0.5) * 0.4));
    c = mix(c, snow, smooth(3.4, 3.55, s.h + (fbm(s.x * 2, s.z * 2) - 0.5) * 0.3));
    if (s.ash > 0) c = mix(c, mix(ash, ash2, smooth(0.4, 0.7, n)), smooth(0, 0.25, s.ash));
    // Light: slopes facing it brighter, those away in a cool shade; up in the sierra, sharper, as rock and snow are.
    const high = smooth(2.3, 3.0, s.h);
    const l = (s.light - 0.62) * (1.5 + high * 2.2);
    c = l > 0 ? mix(c, [255, 252, 235], Math.min(0.35 + high * 0.15, l * 0.5)) : mix(c, high > 0.5 ? [70, 95, 140] : [40, 60, 90], Math.min(0.45 + high * 0.1, -l * 0.6));
    // The sea off the south-east corner, where the river ends: a beach, the shallows, deeper out (flat: no light on it).
    const sd = G.seaDistance(s.x, s.z);
    if (sd < 7) {
      const out = smooth(7, 6.2, sd);
      c = mix(c, sand, smooth(-0.16, -0.24, s.h) * out);
      c = mix(c, mix(sea, deep, smooth(-0.42, -0.66, s.h)), smooth(-0.27, -0.33, s.h) * out);
    }
    const g = (s.grain - 0.5) * 6;
    return [c[0] + g, c[1] + g, c[2] + g];
  });
  // Water: shallows round the shore, the deep turquoise in.
  ctx.save();
  ctx.lineJoin = 'round';
  ctx.lineWidth = 0.12 * V.k;
  ctx.strokeStyle = '#e9dfb2';
  polygon(LAKE_SHORE);
  ctx.stroke();
  ctx.restore();
  strokeRiver(0.08, '#e3d9a8');
  waterShapes('#3fb6d8');
  strokeRiver(0, '#3fb6d8');
  ctx.save();
  polygon(LAKE_SHORE);
  ctx.clip();
  ctx.lineWidth = 0.35 * V.k;
  ctx.strokeStyle = 'rgba(30, 120, 175, 0.5)';
  ctx.filter = `blur(${0.15 * V.k}px)`;
  polygon(LAKE_SHORE.map(([x, z]) => [LAKE.x + (x - LAKE.x) * 0.82, LAKE.z + (z - LAKE.z) * 0.82] as Pt));
  ctx.fillStyle = 'rgba(30, 130, 185, 0.6)';
  ctx.fill();
  ctx.restore();
  stroke(DAM, 0.08 * V.k, '#d8d4c8');
  townGround('rgba(214, 205, 170, 0.75)', 0.2, 1.2 * Math.sqrt(zoom()));
  // Areas: squares paved, yards and lots concrete, the pit's terraces, gardens green.
  drawAreas((a) => {
    switch (a.kind) {
      case 'square':
        return '#efe6d2';
      case 'yard':
      case 'lot':
      case 'compound':
      case 'dock':
        return '#c9c4b8';
      case 'switchyard':
        return '#b9b6ae';
      case 'park':
        return '#6fbd4f';
      case 'garden':
        return '#8ccc63';
      case 'pit':
        return css(mix(hex(0xc8a77e), hex(0x8a6f55), (a.level ?? 0) / 4));
      default:
        return null;
    }
  });
  // Roads: a dark casing, grey asphalt, a pale line; the streets lighter.
  const roads = [...ROAD_LINES.map((r) => r.line), CLIMB];
  for (const l of roads) stroke(l, 0.16 * V.k, '#4d5257');
  drawWays(['street', 'avenue', 'lane'], (w) => (w.w + 0.03) * V.k, () => '#5c6166');
  for (const l of roads) stroke(l, 0.12 * V.k, '#878c91');
  drawWays(['street', 'avenue', 'lane'], (w) => w.w * V.k, () => '#a3a7aa');
  if (zoom() > 1.5) for (const l of roads) stroke(l, Math.max(1, 0.012 * V.k), 'rgba(255, 255, 240, 0.85)', [0.06 * V.k, 0.06 * V.k]);
  drawWays(['rail'], (w) => w.w * V.k, () => '#5a4a3c');
  drawWays(['pier'], (w) => w.w * V.k, () => '#b9b2a2');
  drawWays(['wall'], (w) => w.w * V.k, () => '#e1ddd2');
  drawWays(['fence'], (w) => w.w * V.k, () => '#8f948f');
  // The toll's barrier, striped.
  for (const w of F.ways) if (w.kind === 'barrier' && inView(w.line[0]![0], w.line[0]![1])) {
    stroke(w.line, w.w * V.k + 2, '#3a2a22');
    stroke(w.line, w.w * V.k, '#f4efe6');
    stroke(w.line, w.w * V.k, '#d33a2c', [0.03 * V.k, 0.03 * V.k]);
  }
  drawHouses({
    roof: (h) => {
      const t = h.tone;
      switch (h.kind) {
        case 'chalet':
          return mix(hex(0x8a5a3c), hex(0x5d6b78), t > 0.6 ? 1 : 0);
        case 'terrace':
          return mix(hex(0x8a5b4c), hex(0x6d6a70), t);
        case 'house':
          return [hex(0xd2603f), hex(0xb94a36), hex(0xe08a52), hex(0x8f5a42), hex(0x6c7a8a)][Math.floor(t * 5)]!;
        case 'block':
          return mix(hex(0xc9bba6), hex(0xa79a8a), t);
        case 'tower':
          return mix(hex(0x9aa3ab), hex(0x7f8a94), t);
        case 'church':
          return hex(0xb34b38);
        case 'hospital':
          return hex(0xeef0f0);
        case 'hall':
        case 'shed':
          return mix(hex(0x8d9aa5), hex(0xb7653e), h.zone === 'oce' ? 1 : 0);
        case 'lab':
          return hex(0xe6e9ec);
        case 'barracks':
          return hex(0x6f7b5c);
        case 'civic':
          return hex(0xb9a27c);
        case 'kiosk':
          return hex(0xd7473a);
        case 'canopy':
          return hex(0xf2f2ee);
        case 'booth':
          return hex(0xd7473a);
        case 'truck':
          return [hex(0xc4472f), hex(0x3f73a6), hex(0xd9a03a), hex(0xe9e6df)][Math.floor(t * 4)]!;
        case 'ruin':
          return mix(hex(0x4a4440), hex(0x6a625a), t);
      }
    },
    shadow: 'rgba(25, 45, 40, 0.32)',
    ridge: 0.18,
  });
  rounds((r) => {
    switch (r.kind) {
      case 'dome':
        return { fill: '#f2f4f5', ring: '#9fb0bd', hi: 'rgba(255,255,255,0.9)' };
      case 'cooling':
        return { fill: '#d9d6cf', ring: '#9a958c', hole: '#6c6862' };
      case 'tank':
        return { fill: '#c9cdd1', ring: '#8a9096' };
      case 'helipad':
        return { fill: '#5f666c', ring: '#f2f2ee', h: '#f2f2ee' };
      case 'heap':
        return { fill: '#8e8577', ring: '#6f675c' };
      case 'fountain':
        return { fill: '#7fd0e6', ring: '#d8d0bc' };
      case 'antenna':
        return { fill: '#d64535', ring: '#f2f2ee', mast: true };
      case 'crane':
        return { fill: '#e2b73c', ring: '#8a6f2a', mast: true };
    }
  });
  trees({ crown: (t) => css(t.conifer ? mix(hex(0x2f7d46), hex(0x3c8f4f), t.tone) : mix(hex(0x4c9c3e), hex(0x6cb44a), t.tone)), shadow: 'rgba(20, 50, 30, 0.38)', light: 'rgba(200, 245, 150, 0.35)' });
}


/** A word on the map (the helipad's H). */
function text(t: string, x: number, y: number, o: { font: string; fill: string; spacing?: number }): void {
  ctx.save();
  ctx.font = o.font;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  (ctx as unknown as { letterSpacing: string }).letterSpacing = `${o.spacing ?? 0}px`;
  ctx.fillStyle = o.fill;
  ctx.fillText(t, x, y);
  ctx.restore();
}

function rounds(look: (r: Round) => { fill: string; ring: string; hi?: string; hole?: string; h?: string; mast?: boolean }): void {
  for (const r of F.rounds) {
    if (!inView(r.x, r.z)) continue;
    const l = look(r);
    const cx = px(r.x);
    const cy = py(r.z);
    const rr = Math.max(2, r.r * V.k);
    if (l.mast) {
      ctx.beginPath();
      ctx.arc(cx, cy, rr * 0.45, 0, Math.PI * 2);
      ctx.fillStyle = l.fill;
      ctx.fill();
      ctx.lineWidth = Math.max(1, rr * 0.18);
      ctx.strokeStyle = l.ring;
      for (const k of [1, 1.8]) {
        ctx.beginPath();
        ctx.arc(cx, cy, rr * k, 0, Math.PI * 2);
        ctx.stroke();
      }
      continue;
    }
    ctx.beginPath();
    ctx.arc(cx, cy, rr, 0, Math.PI * 2);
    ctx.fillStyle = l.fill;
    ctx.fill();
    ctx.lineWidth = Math.max(1, rr * 0.16);
    ctx.strokeStyle = l.ring;
    ctx.stroke();
    if (l.hole) {
      ctx.beginPath();
      ctx.arc(cx, cy, rr * 0.62, 0, Math.PI * 2);
      ctx.fillStyle = l.hole;
      ctx.fill();
    }
    if (l.hi) {
      ctx.beginPath();
      ctx.arc(cx - rr * 0.3, cy - rr * 0.3, rr * 0.35, 0, Math.PI * 2);
      ctx.fillStyle = l.hi;
      ctx.fill();
    }
    if (l.h) text('H', cx, cy + 1, { font: `700 ${Math.round(rr * 1.1)}px Barlow`, fill: l.h, spacing: 0 });
  }
}

function trees(look: { crown: (t: Tree) => string; shadow: string | null; light: string | null }): void {
  const visible = F.trees.filter((t) => inView(t.x, t.z, 0.2));
  if (look.shadow) {
    ctx.fillStyle = look.shadow;
    ctx.beginPath();
    for (const t of visible) {
      const r = t.r * V.k;
      ctx.moveTo(px(t.x) + r * 0.35 + r, py(t.z) + r * 0.35);
      ctx.arc(px(t.x) + r * 0.35, py(t.z) + r * 0.35, r, 0, Math.PI * 2);
    }
    ctx.fill();
  }
  for (const t of visible) {
    const r = t.r * V.k;
    ctx.beginPath();
    if (t.conifer) {
      // A pine from above: a star of needles.
      for (let k = 0; k < 14; k++) {
        const a = (k / 14) * Math.PI * 2;
        const rr = k % 2 ? r * 0.62 : r;
        k ? ctx.lineTo(px(t.x) + Math.cos(a) * rr, py(t.z) + Math.sin(a) * rr) : ctx.moveTo(px(t.x) + rr, py(t.z));
      }
      ctx.closePath();
    } else ctx.arc(px(t.x), py(t.z), r, 0, Math.PI * 2);
    ctx.fillStyle = look.crown(t);
    ctx.fill();
  }
  if (look.light) {
    ctx.fillStyle = look.light;
    ctx.beginPath();
    for (const t of visible) {
      const r = t.r * V.k;
      ctx.moveTo(px(t.x) - r * 0.25 + r * 0.45, py(t.z) - r * 0.25);
      ctx.arc(px(t.x) - r * 0.25, py(t.z) - r * 0.25, r * 0.45, 0, Math.PI * 2);
    }
    ctx.fill();
  }
}


// ------------------------------------------------------------ baking

/**
 * The map's tiles: the world they cover (the valley and, round it, enough
 * of the sierra, the woods, the sea and the plain to fill a screen behind the
 * panels whatever the framing), each level `n` × `n` tiles of `tile` pixels;
 * the sharpest only over the valley itself (`sharp`), the level below doing
 * for the rest.
 */
export const VALLEY_TILES = {
  x0: -34,
  x1: 34,
  z0: -22,
  z1: 26,
  levels: [1, 2, 4, 8],
  tile: [2048, 1446] as const,
  sharp: { x0: -17, x1: 17, z0: -11.5, z1: 13 },
};

/** The ground a tile covers. */
export function tileBounds(level: number, i: number, j: number): { x0: number; x1: number; z0: number; z1: number } {
  const n = VALLEY_TILES.levels[level]!;
  const w = (VALLEY_TILES.x1 - VALLEY_TILES.x0) / n;
  const h = (VALLEY_TILES.z1 - VALLEY_TILES.z0) / n;
  return { x0: VALLEY_TILES.x0 + i * w, x1: VALLEY_TILES.x0 + (i + 1) * w, z0: VALLEY_TILES.z0 + j * h, z1: VALLEY_TILES.z0 + (j + 1) * h };
}

/** Whether a tile is baked: all of every level but the sharpest, which only covers the valley. */
export function tileExists(level: number, i: number, j: number): boolean {
  if (level < VALLEY_TILES.levels.length - 1) return true;
  const b = tileBounds(level, i, j);
  const s = VALLEY_TILES.sharp;
  return b.x1 > s.x0 && b.x0 < s.x1 && b.z1 > s.z0 && b.z0 < s.z1;
}

/** The view a tile is drawn in. */
export function tileView(level: number, i: number, j: number): View {
  const b = tileBounds(level, i, j);
  return { cx: (b.x0 + b.x1) / 2, cz: (b.z0 + b.z1) / 2, k: VALLEY_TILES.tile[0] / (b.x1 - b.x0), w: VALLEY_TILES.tile[0], h: VALLEY_TILES.tile[1] };
}

/** Paints the valley's ground as seen in `view` onto `canvas` (its size set to the view's). */
export function paintValley(canvas: HTMLCanvasElement, view: View): void {
  layOut();
  V = view;
  canvas.width = view.w;
  canvas.height = view.h;
  ctx = canvas.getContext('2d')!;
  illustrated();
}
