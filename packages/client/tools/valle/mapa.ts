// The valley's map from Arturo's painting (assets-src/valle/mapa-valle.png, which is not uploaded):
//
//   npx tsx packages/client/tools/valle/mapa.ts
//
// - public/maps/valle/mapa.jpg: the painting as the game shows it;
// - src/strategy/valleyRoads.ts: each road of the engine's graph (ROADS) along the painting's own roads, from
//   one zone's spot (valleyGeography.ts ZONE_PIXELS) to the other's: the shortest way over the painting's road
//   pixels (light, unsaturated), through the waypoints below where the shortest would take another road, then
//   simplified to a line of points.
// ffmpeg reads and writes the images (/opt/homebrew/bin/ffmpeg, or the one on the PATH).
import { execFileSync } from 'node:child_process';
import { existsSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ROADS, type RegionId } from '@bastion/engine';
import { IMAGE, ZONE_PIXELS } from '../../src/strategy/valleyGeography';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../../..');
const SOURCE = resolve(ROOT, 'assets-src/valle/mapa-valle.png');
const OUT_IMAGE = resolve(ROOT, 'packages/client/public/maps/valle/mapa.jpg');
const OUT_ROADS = resolve(ROOT, 'packages/client/src/strategy/valleyRoads.ts');
const FFMPEG = existsSync('/opt/homebrew/bin/ffmpeg') ? '/opt/homebrew/bin/ffmpeg' : 'ffmpeg';

/** Where a road must pass (painting pixels), when the shortest way would take another: Volkren's along the north by the lake, Galen's from Llanos by the east. */
const VIA: Partial<Record<string, [number, number][]>> = {
  'eeu|weu': [
    [1270, 155],
    [860, 112],
    [655, 165],
  ],
  'me|na': [[1220, 662]],
};

const { w: W, h: H } = IMAGE;
const rgb = execFileSync(FFMPEG, ['-loglevel', 'error', '-i', SOURCE, '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], { maxBuffer: W * H * 4 });
if (rgb.length !== W * H * 3) throw new Error(`La imagen no mide ${W}×${H}.`);

// A road: a light, unsaturated pixel. Walking a road costs 1 (0.7 in its middle, so the way keeps to it); off it, 40.
const road = new Uint8Array(W * H);
for (let i = 0; i < W * H; i++) {
  const r = rgb[i * 3]!;
  const g = rgb[i * 3 + 1]!;
  const b = rgb[i * 3 + 2]!;
  road[i] = Math.max(r, g, b) > 175 && Math.max(r, g, b) - Math.min(r, g, b) < 45 ? 1 : 0;
}
const cost = new Float32Array(W * H);
for (let y = 0; y < H; y++)
  for (let x = 0; x < W; x++) {
    const i = y * W + x;
    if (!road[i]) {
      cost[i] = 40;
      continue;
    }
    let middle = true;
    for (let dy = -2; dy <= 2 && middle; dy++)
      for (let dx = -2; dx <= 2 && middle; dx++) {
        const xx = x + dx;
        const yy = y + dy;
        if (xx < 0 || yy < 0 || xx >= W || yy >= H || !road[yy * W + xx]) middle = false;
      }
    cost[i] = middle ? 0.7 : 1;
  }

// A* on the pixels, with a binary heap on typed arrays.
let keys = new Float64Array(1 << 20);
let vals = new Int32Array(1 << 20);
let size = 0;
function push(k: number, v: number): void {
  if (size === keys.length) {
    const k2 = new Float64Array(size * 2);
    k2.set(keys);
    keys = k2;
    const v2 = new Int32Array(size * 2);
    v2.set(vals);
    vals = v2;
  }
  let i = size++;
  while (i > 0) {
    const p = (i - 1) >> 1;
    if (keys[p]! <= k) break;
    keys[i] = keys[p]!;
    vals[i] = vals[p]!;
    i = p;
  }
  keys[i] = k;
  vals[i] = v;
}
function pop(): number {
  const top = vals[0]!;
  const k = keys[--size]!;
  const v = vals[size]!;
  let i = 0;
  for (;;) {
    const l = 2 * i + 1;
    const r = l + 1;
    let m = -1;
    let mk = k;
    if (l < size && keys[l]! < mk) {
      m = l;
      mk = keys[l]!;
    }
    if (r < size && keys[r]! < mk) m = r;
    if (m < 0) break;
    keys[i] = keys[m]!;
    vals[i] = vals[m]!;
    i = m;
  }
  keys[i] = k;
  vals[i] = v;
  return top;
}
const STEPS: [number, number, number][] = [
  [1, 0, 1],
  [-1, 0, 1],
  [0, 1, 1],
  [0, -1, 1],
  [1, 1, Math.SQRT2],
  [1, -1, Math.SQRT2],
  [-1, 1, Math.SQRT2],
  [-1, -1, Math.SQRT2],
];
function shortest(s: [number, number], t: [number, number]): [number, number][] {
  const g = new Float32Array(W * H).fill(Infinity);
  const from = new Int32Array(W * H).fill(-1);
  const closed = new Uint8Array(W * H);
  const si = s[1] * W + s[0];
  const ti = t[1] * W + t[0];
  const guess = (i: number) => Math.hypot((i % W) - t[0], Math.floor(i / W) - t[1]) * 0.7;
  g[si] = 0;
  size = 0;
  push(guess(si), si);
  while (size) {
    const i = pop();
    if (closed[i]) continue;
    closed[i] = 1;
    if (i === ti) break;
    const x = i % W;
    const y = Math.floor(i / W);
    for (const [dx, dy, d] of STEPS) {
      const xx = x + dx;
      const yy = y + dy;
      if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
      const j = yy * W + xx;
      if (closed[j]) continue;
      const ng = g[i]! + d * cost[j]!;
      if (ng < g[j]!) {
        g[j] = ng;
        from[j] = i;
        push(ng + guess(j), j);
      }
    }
  }
  const path: [number, number][] = [];
  for (let i = ti; i !== -1; i = from[i]!) path.push([i % W, Math.floor(i / W)]);
  return path.reverse();
}

/** Douglas-Peucker: the fewest points within `eps` pixels of the way. */
function simplify(pts: [number, number][], eps: number): [number, number][] {
  if (pts.length < 3) return pts;
  const a = pts[0]!;
  const b = pts[pts.length - 1]!;
  const L = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1;
  let best = 0;
  let k = 0;
  for (let i = 1; i < pts.length - 1; i++) {
    const p = pts[i]!;
    const d = Math.abs((b[1] - a[1]) * p[0] - (b[0] - a[0]) * p[1] + b[0] * a[1] - b[1] * a[0]) / L;
    if (d > best) {
      best = d;
      k = i;
    }
  }
  if (best <= eps) return [a, b];
  return [...simplify(pts.slice(0, k + 1), eps).slice(0, -1), ...simplify(pts.slice(k), eps)];
}

const lines: string[] = [];
for (const [a, b] of ROADS as [RegionId, RegionId][]) {
  const key = `${a}|${b}`;
  const stops = [ZONE_PIXELS[a], ...(VIA[key] ?? []), ZONE_PIXELS[b]];
  let path: [number, number][] = [];
  for (let k = 0; k + 1 < stops.length; k++) path = path.concat(shortest(stops[k]!, stops[k + 1]!).slice(k ? 1 : 0));
  const off = path.filter(([x, y]) => !road[y * W + x]).length;
  const line = simplify(path, 1.6);
  console.log(`${key}: ${path.length} px (${off} fuera de la carretera), ${line.length} puntos`);
  lines.push(`  '${key}': [${line.map(([x, y]) => `[${x}, ${y}]`).join(', ')}],`);
}

writeFileSync(
  OUT_ROADS,
  `// Generated by tools/valle/mapa.ts from Arturo's painting: each road of the engine's graph along the
// painting's own, in its pixels (valleyGeography.ts turns them into the map's units). Not edited by hand.

export const ROAD_PIXELS: Record<string, [number, number][]> = {
${lines.join('\n')}
};
`,
);
execFileSync(FFMPEG, ['-loglevel', 'error', '-y', '-i', SOURCE, '-q:v', '2', OUT_IMAGE]);
console.log(`${OUT_IMAGE}\n${OUT_ROADS}`);
