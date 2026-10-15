import * as THREE from 'three';
import { DOOM_MAX, MAREA_SOURCE, REGIONS, ROADS, mareaThreshold, type RegionId } from '@bastion/engine';

// The valley of Kessra's geography (docs/LORE.md, «El mundo»): its size, the
// ground's height (the sierra to the north, the western hills, the closed
// valleys to the north-east, the reservoir's shelf, the river's bed and the
// sea), the woods, the ash plain, the river, the reservoir, Atalaya, the two
// dead cities, the towns, the roads between the zones and the zombi activity at
// which the Marea reaches each point. Where the zones lie, the roads and the
// Marea's thresholds are the engine's (campaign.ts, docs/FASE5.md); the
// roads' bends are only drawn. The valley's map (geoscape.ts, valleyArt.ts)
// draws it.

/** The valley's size: x from west to east, z from north (the sierra) to south (the plain), in map units. */
export const W = 30;
export const D = 22;
export const HW = W / 2;
export const HD = D / 2;

/** Where each zone lies in the valley: the engine's geography. */
export const ZONES: Record<RegionId, { x: number; z: number }> = REGIONS;
const ZONE_LIST = Object.values(ZONES);

/** How far (x, z) lies from the nearest zone's marker. */
function nearestZone(x: number, z: number): number {
  let best = Infinity;
  for (const p of ZONE_LIST) best = Math.min(best, Math.hypot(x - p.x, z - p.z));
  return best;
}

/** Atalaya: the station on its shelf under the peaks, above Aldren's villages. */
export const ATALAYA = { x: -3.4, z: -7.0 };

/** Where each road (the engine's ROADS) bends, so it looks less ruled; one the map doesn't know runs straight. */
const BENDS: Record<string, [number, number]> = {
  'oce|weu': [-1.6, -1.8],
  'sa|weu': [-8.6, -3.4],
  'eeu|weu': [2.0, -6.4],
  'eas|eeu': [6.8, -1.8],
  'eas|oce': [5.4, 0.6],
  'afr|oce': [-2.6, 3.6],
  'me|oce': [4.6, 4.4],
  'afr|na': [-4.0, 7.6],
  'me|na': [4.0, 8.6],
  'afr|sa': [-10.0, 3.0],
};

function bendOf(a: RegionId, b: RegionId): [number, number] {
  return BENDS[a < b ? `${a}|${b}` : `${b}|${a}`] ?? [(ZONES[a].x + ZONES[b].x) / 2, (ZONES[a].z + ZONES[b].z) / 2];
}


/** The river: from under the dam, through La Vega and down to the sea in the south-east corner. */
export const RIVER: [number, number][] = [
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


/** How far into the ash plain (x, z) lies: above 0 inside it, round Los Llanos (ragged at its edge). */
function ashDepth(x: number, z: number): number {
  const dx = (x - 0.4) / 5.4;
  const dz = (z - 9.6) / 3.4;
  return 0.75 + (noise(x * 0.8, z * 0.8) - 0.5) * 0.6 - (dx * dx + dz * dz);
}


/** The reservoir: its basin up on a shelf, its water level, and the dam across the gap where the river leaves it. */
export const RESERVOIR = { x: 11.2, z: -2.0, r: 2.6, level: 1.2, dam: 2.5 };

/** The two dead cities: a grid of blocks round the zone (a square left open at its marker), streets between them. */
export const CITIES: { id: RegionId; dx: number; dz: number; cols: number; rows: number; cell: number }[] = [
  { id: 'afr', dx: 0, dz: 0, cols: 8, rows: 6, cell: 0.62 },
  { id: 'me', dx: 0.35, dz: -0.15, cols: 6, rows: 5, cell: 0.62 },
];

/** The towns: where each stands, its houses, which way its lane runs, its seed, whether it lies under the snow and what you know it by. */
export const TOWNS: { x: number; z: number; n: number; axis: number; seed: number; snow: boolean; landmark: 'church' | 'tower' }[] = [
  // Aldren's villages under Atalaya, in the snow.
  { x: ZONES.weu.x - 1.3, z: ZONES.weu.z + 0.55, n: 10, axis: 0.25, seed: 11, snow: true, landmark: 'church' },
  { x: ZONES.weu.x + 1.4, z: ZONES.weu.z + 1.0, n: 6, axis: -0.4, seed: 13, snow: true, landmark: 'tower' },
  // Volkren: villages in the closed valleys.
  { x: ZONES.eeu.x - 0.9, z: ZONES.eeu.z + 0.6, n: 9, axis: 0.5, seed: 23, snow: false, landmark: 'church' },
  { x: ZONES.eeu.x + 3.1, z: ZONES.eeu.z - 0.9, n: 6, axis: -0.3, seed: 29, snow: false, landmark: 'tower' },
  // Carvik: the miners' row.
  { x: ZONES.sa.x + 0.9, z: ZONES.sa.z + 1.3, n: 6, axis: 1.2, seed: 31, snow: false, landmark: 'tower' },
];

/** Whether (x, z) lies in a city (with `margin` round it). */
function inCity(x: number, z: number, margin = 0): boolean {
  return CITIES.some((c) => Math.abs(x - ZONES[c.id].x - c.dx) < (c.cols * c.cell) / 2 + margin && Math.abs(z - ZONES[c.id].z - c.dz) < (c.rows * c.cell) / 2 + margin);
}

/** How far (x, z) lies outside the nearest city's grid (0 inside it). */
function cityDistance(x: number, z: number): number {
  let best = Infinity;
  for (const c of CITIES) {
    const dx = Math.max(0, Math.abs(x - ZONES[c.id].x - c.dx) - (c.cols * c.cell) / 2);
    const dz = Math.max(0, Math.abs(z - ZONES[c.id].z - c.dz) - (c.rows * c.cell) / 2);
    best = Math.min(best, Math.hypot(dx, dz));
  }
  return best;
}

/** How thick the broadleaf woods are at (x, z): they grow in patches (above WOODS), the ground darker under them. */
const woods = (x: number, z: number) => fbm(x * 0.55 + 40, z * 0.55 + 12);
const WOODS = 0.58;


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

export function distToPolyline(x: number, z: number, line: [number, number][]): number {
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
export function heightAt(x: number, z: number): number {
  const n = fbm(x * 0.35, z * 0.35);
  let h = 0.2 + (n - 0.5) * 0.35;
  // The sierra to the north, its ridge broken into peaks.
  h += smooth(3.6, 9.2, -z) * (2.4 + 2.4 * fbm(x * 0.55 + 7, z * 0.4 + 2));
  // Atalaya's shelf under the peaks, levelled.
  h = THREE.MathUtils.lerp(h, 2.8, smooth(3.0, 1.3, Math.hypot(x - ATALAYA.x, z - ATALAYA.z)));
  // The western hills (Carvik) and the closed valleys to the north-east (Volkren).
  h += 1.6 * bump(x, z, -12.2, -0.8, 4.8) * (0.55 + n);
  h += 1.9 * bump(x, z, 9.6, -6.4, 5.2) * (0.45 + 0.9 * noise(x * 0.9, z * 0.9));
  // Rolling hills over the valley floor, only ever rising (the floor stays above the river): none
  // where the cities stand, round the zones' markers, by the river or on the ash plain.
  const r = distToPolyline(x, z, RIVER);
  const rise = Math.max(0, fbm(x * 0.24 + 31, z * 0.24 + 17) - 0.44) * 1.5;
  if (rise > 0) h += rise * smooth(0.3, 1.4, cityDistance(x, z)) * smooth(0.7, 1.8, nearestZone(x, z)) * smooth(0.5, 1.8, r) * smooth(0.4, -0.2, ashDepth(x, z));
  // The reservoir's shelf, its basin dug out under the water.
  const d = Math.hypot(x - RESERVOIR.x, z - RESERVOIR.z);
  h = THREE.MathUtils.lerp(h, Math.max(h, 1.5 + n * 0.2), smooth(4.2, 3.0, d));
  h = THREE.MathUtils.lerp(h, 0.6, smooth(RESERVOIR.r, RESERVOIR.r - 0.8, d));
  // The river's bed and the sea.
  h = THREE.MathUtils.lerp(-0.22, h, smooth(0.14, 0.6, r));
  h = THREE.MathUtils.lerp(h, -0.7, smooth(5.2, 2.4, seaDistance(x, z)));
  return h;
}

/**
 * How far inland (x, z) lies from out at sea, off the south-east corner: the
 * coast runs from the south-west to the north-east with its bays, the sea
 * open beyond it, past the map's edge (2.4 and under, deep water; 5.2 and
 * over, dry land).
 */
export function seaDistance(x: number, z: number): number {
  const along = (x - 15.5) * 0.78 - (z - 12) * 0.62;
  const out = (x - 15.5) * 0.62 + (z - 12) * 0.78;
  const coast = 1.1 * Math.sin(along * 0.42 + 1.3) + 0.5 * Math.sin(along * 1.05 + 0.4);
  return Math.max(0, 1.6 - out + coast);
}

/** A road as a ribbon over the ground, through its bend. */
function roadPoints(a: { x: number; z: number }, b: { x: number; z: number }, bend: [number, number]): THREE.Vector3[] {
  const curve = new THREE.QuadraticBezierCurve(new THREE.Vector2(a.x, a.z), new THREE.Vector2(...bend), new THREE.Vector2(b.x, b.z));
  return curve.getSpacedPoints(Math.ceil(curve.getLength() / 0.22)).map((p) => new THREE.Vector3(p.x, Math.max(heightAt(p.x, p.y), 0.02) + 0.03, p.y));
}

/** A road's polyline between two zones, from `a` to `b` (the same one each way, reversed). */
const roadLines = new Map<string, THREE.Vector3[]>();
export function roadLine(a: RegionId, b: RegionId): THREE.Vector3[] {
  let line = roadLines.get(`${a}|${b}`);
  if (!line) {
    line = roadPoints(ZONES[a], ZONES[b], bendOf(a, b));
    roadLines.set(`${a}|${b}`, line);
    roadLines.set(`${b}|${a}`, line.slice().reverse());
  }
  return line;
}

// ------------------------------------------------------------- the Marea

/**
 * The Marea on the map. Each point of the valley has the zombi activity at
 * which the tide reaches it, baked once into a texture: round each zone its
 * threshold (the engine's mareaThreshold), a little more the further out;
 * along each road, from one end's threshold to the other's; along the south
 * edge, a strip just before Los Llanos falls. The ground under the activity
 * now is stained, so a zone's marker lies inside the stain exactly while the
 * zone is under the Marea, and between thresholds the tide creeps up the
 * roads towards the next zone. Easing the activity drawn animates it.
 */
export const FIELD = { w: 240, d: 176 };

/** The activity at which a zone falls; Aldren only past the end (at DOOM_MAX the campaign is lost). */
export const threshold = (id: RegionId) => mareaThreshold(id) ?? DOOM_MAX + 0.5;

export function mareaReach(x: number, z: number, roads: { a: RegionId; b: RegionId; points: THREE.Vector2[]; lengths: number[] }[]): number {
  let v = Infinity;
  let near = Infinity;
  // Round each zone: its threshold over the zone itself (a zone that falls is engulfed at once), rising further out, steeply past a reach.
  for (const id of Object.keys(ZONES) as RegionId[]) {
    const d = Math.hypot(x - ZONES[id].x, z - ZONES[id].z);
    near = Math.min(near, d);
    v = Math.min(v, threshold(id) - 0.5 + Math.min(Math.max(0, d - 1.2), 2.2) * 0.9 + Math.max(0, d - 3.4) * 3);
  }
  // Along each road, from one end's threshold to the other's, a corridor either side (never too wide).
  for (const r of roads) {
    let best = Infinity;
    let at = 0;
    for (let i = 0; i + 1 < r.points.length; i++) {
      const p = r.points[i]!;
      const q = r.points[i + 1]!;
      const dx = q.x - p.x;
      const dz = q.y - p.y;
      const t = THREE.MathUtils.clamp(((x - p.x) * dx + (z - p.y) * dz) / (dx * dx + dz * dz), 0, 1);
      const d = Math.hypot(x - p.x - dx * t, z - p.y - dz * t);
      if (d < best) {
        best = d;
        at = (r.lengths[i]! + (r.lengths[i + 1]! - r.lengths[i]!) * t) / r.lengths[r.lengths.length - 1]!;
      }
    }
    v = Math.min(v, THREE.MathUtils.lerp(threshold(r.a), threshold(r.b), at) - 0.5 + Math.max(0, best - 0.25) + Math.max(0, best - 2) * 3);
  }
  // The strip along the south edge, where it comes up from (never more than a strip: the zones and roads carry it on).
  const edge = HD - z;
  v = Math.min(v, threshold(MAREA_SOURCE) - 1.8 + edge * 0.65 + Math.max(0, edge - 2) * 3);
  // A ragged edge, but not round the markers.
  return v + (fbm(x * 0.9 + 11, z * 0.9 + 5) - 0.5) * 0.7 * smooth(0.6, 1.4, near);
}


/** The roads as mareaReach follows them: their points and how far along each one lies. */
export function mareaRoads(): Parameters<typeof mareaReach>[2] {
  return ROADS.map(([a, b]) => {
    const curve = new THREE.QuadraticBezierCurve(new THREE.Vector2(ZONES[a].x, ZONES[a].z), new THREE.Vector2(...bendOf(a, b)), new THREE.Vector2(ZONES[b].x, ZONES[b].z));
    const points = curve.getSpacedPoints(12);
    const lengths = [0];
    for (let i = 1; i < points.length; i++) lengths.push(lengths[i - 1]! + points[i]!.distanceTo(points[i - 1]!));
    return { a, b, points, lengths };
  });
}

let roadsForMarea: Parameters<typeof mareaReach>[2] | null = null;

/**
 * The valley's geography as this map draws it, for the 2D map's sketches
 * (lab/mapLab.ts; Arturo, 2026-10-14: «que el valle sea un mapa y no un
 * 3D»): its size, the ground's height, the woods, the ash plain, the river,
 * the reservoir, Atalaya, the cities, the towns, each road's line and the
 * zombi activity at which the Marea reaches a point.
 */
export const GEOGRAPHY = {
  width: W,
  depth: D,
  heightAt,
  woods,
  woodsAbove: WOODS,
  ashDepth,
  seaDistance,
  river: RIVER,
  reservoir: RESERVOIR,
  atalaya: ATALAYA,
  cities: CITIES,
  towns: TOWNS,
  road: (a: RegionId, b: RegionId): [number, number][] => roadLine(a, b).map((p) => [p.x, p.z]),
  mareaAt: (x: number, z: number): number => mareaReach(x, z, (roadsForMarea ??= mareaRoads())),
};
