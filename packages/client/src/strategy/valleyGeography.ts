import * as THREE from 'three';
import { DOOM_MAX, LANDMARKS, MAREA_SOURCE, ROADS, mareaThreshold, type RegionId } from '@bastion/engine';
import { ROAD_PIXELS } from './valleyRoads';

// The valley of Kessra on Arturo's map (15/10: «este va a ser el mapa»), a
// painting seen from above (assets-src/valle/mapa-valle.png, 1536×1024; in
// the game public/maps/valle/mapa.jpg): where each zone, Atalaya and the
// places one knows the valley by lie on it, the roads between the zones along
// the painting's own (traced by tools/valle/mapa.ts into valleyRoads.ts) and
// the zombi activity at which the Marea reaches each point, up the roads from
// Los Llanos de Ceniza in the south. The road graph and the Marea's
// thresholds are the engine's (campaign.ts REGIONS, ROADS); its x/z are these.

/** The painting's size in pixels, and its pixels a map unit. */
export const IMAGE = { w: 1536, h: 1024 };
export const PX = 51.2;
/** The valley's size: x from west to east, z from north to south, in map units (the painting's whole). */
export const W = IMAGE.w / PX;
export const D = IMAGE.h / PX;
export const HW = W / 2;
export const HD = D / 2;

/** A pixel of the painting, as a point of the map. */
export const onMap = (u: number, v: number) => ({ x: u / PX - HW, z: v / PX - HD });

/** Where each zone lies on the painting: Llanos the great works in the south, Galen the city on the right, Corran the one with the roundabout. */
export const ZONE_PIXELS: Record<RegionId, [number, number]> = {
  na: [915, 868],
  sa: [180, 462],
  weu: [455, 212],
  eeu: [1415, 88],
  afr: [432, 718],
  me: [1322, 398],
  eas: [962, 192],
  oce: [748, 480],
};

export const ZONES = Object.fromEntries((Object.keys(ZONE_PIXELS) as RegionId[]).map((id) => [id, onMap(...ZONE_PIXELS[id])])) as Record<RegionId, { x: number; z: number }>;
const ZONE_LIST = Object.values(ZONES);

/** Atalaya: the walled works with its water, up in the north-west above Brena; burnt out since the autumn (Lore, «Atalaya cayó»). */
export const ATALAYA = onMap(222, 118);
/** The middle of Atalaya's works and how far they spread (map units), for its ruin over the painting (geoscape.ts). */
export const ATALAYA_WORKS = { ...onMap(260, 135), rx: 1.55, rz: 1.55 };
/** The Bastion (docs/FASE12.md §11): the clearing in the pines above Brena where the caravan stands. */
export const BASTION = onMap(352, 72);

/** The places one knows the valley by (Lore's names, engine lore.ts LANDMARKS), each on something of the painting. */
export const VALLEY_LANDMARKS: { key: keyof typeof LANDMARKS; at: [number, number] }[] = (
  [
    // Atalaya: the walled works, burnt out (Lore, «Atalaya cayó»).
    ['ruins', 222, 118],
    // Brena: the hamlet on the road east.
    ['telm', 620, 150],
    // Carvik's mine: the works on the hill with its conveyor, east of the town.
    ['mine', 445, 540],
    // El Pantano Gris: the dam at the lake's mouth, the dead power plant down the river, the hill with the repeater.
    ['dam', 918, 205],
    ['plant', 985, 448],
    ['peak', 1040, 238],
    // La Vega: the fields south of the crossing.
    ['wineries', 756, 612],
    // Velsa: the Cuervos' toll on the road down to Brena.
    ['toll', 840, 92],
    // Corran: the big block east of the roundabout.
    ['hospital', 612, 735],
    // Galen: the great buildings north of the roundabout, the docks on the water.
    ['institute', 1400, 300],
    ['port', 1462, 480],
    // Los Llanos de Ceniza: the mast east of the works, the truck park at their foot.
    ['station', 1030, 895],
    ['truckStop', 848, 938],
  ] as const
).map(([key, u, v]) => {
  const p = onMap(u, v);
  return { key, at: [p.x, p.z] };
});

// ------------------------------------------------------------- roads

/** The road from `a` to `b` along the painting's (valleyRoads.ts), its points on the ground; [a, b] in a straight line if it was not traced. */
const roadLines = new Map<string, THREE.Vector3[]>();
export function roadLine(a: RegionId, b: RegionId): THREE.Vector3[] {
  let line = roadLines.get(`${a}|${b}`);
  if (!line) {
    const traced = ROAD_PIXELS[a < b ? `${a}|${b}` : `${b}|${a}`];
    const points = traced ? traced.map(([u, v]) => onMap(u, v)) : [ZONES[a], ZONES[b]];
    const forward = (traced && a > b ? points.slice().reverse() : points).map((p) => new THREE.Vector3(p.x, 0, p.z));
    roadLines.set(`${a}|${b}`, forward);
    roadLines.set(`${b}|${a}`, forward.slice().reverse());
    line = forward;
  }
  return line;
}

// ------------------------------------------------------------- the Marea

function hash(x: number, z: number): number {
  const h = Math.sin(x * 127.1 + z * 311.7) * 43758.5453;
  return h - Math.floor(h);
}

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

/** The Marea's field over the valley, in cells (the painting's aspect). */
export const FIELD = { w: 240, d: 160 };

/** The activity at which a zone falls; Brena only past the end (at DOOM_MAX the campaign is lost). */
export const threshold = (id: RegionId) => mareaThreshold(id) ?? DOOM_MAX + 0.5;

/** The roads as mareaReach follows them: their points and how far along each one lies. */
export type MareaRoads = { a: RegionId; b: RegionId; points: THREE.Vector2[]; lengths: number[] }[];

/**
 * The zombi activity at which the Marea reaches (x, z): round each zone its
 * threshold, rising further out; along each road from one end's threshold to
 * the other's, in a corridor either side; a strip along the south edge,
 * where it comes up from; a ragged edge, but not round the markers.
 */
export function mareaReach(x: number, z: number, roads: MareaRoads): number {
  let v = Infinity;
  let near = Infinity;
  for (const id of Object.keys(ZONES) as RegionId[]) {
    const d = Math.hypot(x - ZONES[id].x, z - ZONES[id].z);
    near = Math.min(near, d);
    v = Math.min(v, threshold(id) - 0.5 + Math.min(Math.max(0, d - 1.2), 2.2) * 0.9 + Math.max(0, d - 3.4) * 3);
  }
  for (const r of roads) {
    let best = Infinity;
    let at = 0;
    for (let i = 0; i + 1 < r.points.length; i++) {
      const p = r.points[i]!;
      const q = r.points[i + 1]!;
      const dx = q.x - p.x;
      const dz = q.y - p.y;
      const t = THREE.MathUtils.clamp(((x - p.x) * dx + (z - p.y) * dz) / (dx * dx + dz * dz || 1), 0, 1);
      const d = Math.hypot(x - p.x - dx * t, z - p.y - dz * t);
      if (d < best) {
        best = d;
        at = (r.lengths[i]! + (r.lengths[i + 1]! - r.lengths[i]!) * t) / r.lengths[r.lengths.length - 1]!;
      }
    }
    v = Math.min(v, THREE.MathUtils.lerp(threshold(r.a), threshold(r.b), at) - 0.5 + Math.max(0, best - 0.25) + Math.max(0, best - 2) * 3);
  }
  const edge = HD - z;
  v = Math.min(v, threshold(MAREA_SOURCE) - 1.8 + edge * 0.65 + Math.max(0, edge - 2) * 3);
  return v + (fbm(x * 0.9 + 11, z * 0.9 + 5) - 0.5) * 0.7 * smooth(0.6, 1.4, near);
}

export function mareaRoads(): MareaRoads {
  return ROADS.map(([a, b]) => {
    const points = roadLine(a, b).map((p) => new THREE.Vector2(p.x, p.z));
    const lengths = [0];
    for (let i = 1; i < points.length; i++) lengths.push(lengths[i - 1]! + points[i]!.distanceTo(points[i - 1]!));
    return { a, b, points, lengths };
  });
}

/** How far (x, z) lies from the nearest zone's marker. */
export function nearestZone(x: number, z: number): number {
  let best = Infinity;
  for (const p of ZONE_LIST) best = Math.min(best, Math.hypot(x - p.x, z - p.z));
  return best;
}
