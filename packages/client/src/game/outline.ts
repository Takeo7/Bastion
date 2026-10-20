import type { FrameKind, GameState } from '@bastion/engine';

// The outline round what is played (docs/MAPAS.md §5.10, GameState.frame), as
// the client reads it: which piece each tile is, and the river, the railway or
// the motorway along a side as one straight band (frameArt.ts draws it, the
// ground leaves the river's water open: ground.ts).

/** Tiles past the map's edge the outline goes on (the haze takes it from there). */
export const BEYOND = 6;
/** How far past the map's edge a canal's far bank is (tiles). */
export const FAR_BANK = 5;

/** Each tile's piece of the outline (its index in GameState.frame), -1 where it is played. */
export function frameOf(s: GameState): Int16Array {
  const at = new Int16Array(s.width * s.height).fill(-1);
  (s.frame ?? []).forEach((p, k) => {
    for (let y = p.y; y < p.y + p.h; y++) for (let x = p.x; x < p.x + p.w; x++) at[y * s.width + x] = k;
  });
  return at;
}

/**
 * A river, the railway or the motorway along one side (`side`, DIRS4, out of
 * the map): straight along all of it, `depth` tiles in from the edge, the
 * shallowest its pieces reach anywhere along it. What they take past that
 * (the outline's jogs) is its banks, verges and slopes. The country's water
 * or ravine (docs/MAPAS.md §5.10, phase 5) the same, along the stretch of the
 * side it runs (`from`–`to`, tiles along it).
 */
export interface Band {
  kind: FrameKind;
  side: number;
  depth: number;
  from: number;
  to: number;
}

/** The kinds that run along a side as a band; those of them the ground opens for (down to their water or floor). */
const BANDS = new Set<FrameKind>(['river', 'rail', 'highway', 'water', 'ravine']);
export const SUNKEN = new Set<FrameKind>(['river', 'water', 'ravine']);

/** The map's band (one at most: docs/MAPAS.md §5.10), or null. */
export function bandOf(s: GameState): Band | null {
  const frame = s.frame ?? [];
  let first = frame.find((p) => p.side !== undefined && BANDS.has(p.kind));
  let side = first?.side;
  if (!first) {
    // The country's water or ravine says no side: the one its biggest piece runs along furthest.
    const natural = frame.filter((p) => p.kind === 'water' || p.kind === 'ravine');
    if (!natural.length) return null;
    first = natural.reduce((a, p) => (p.w * p.h > a.w * a.h ? p : a));
    const runs = [first.y === 0 ? first.w : 0, first.x + first.w === s.width ? first.h : 0, first.y + first.h === s.height ? first.w : 0, first.x === 0 ? first.h : 0];
    side = runs.indexOf(Math.max(...runs));
    if (!runs[side]) return null;
  }
  const at = frameOf(s);
  const { kind } = first;
  const n = side! % 2 ? s.height : s.width;
  const across = side! % 2 ? s.width : s.height;
  let depth = Infinity;
  let [from, to] = [n, -1];
  for (let t = 0; t < n; t++) {
    let k = 0;
    while (k < across) {
      const [x, y] = cell(s, side!, t, k);
      const i = at[y * s.width + x]!;
      if (i < 0 || frame[i]!.kind !== kind) break;
      k++;
    }
    if (k > 0) [depth, from, to] = [Math.min(depth, k), Math.min(from, t), t];
  }
  return Number.isFinite(depth) ? { kind, side: side!, depth, from, to } : null;
}

/** The tile `k` in from the edge on `side`, `t` along it. */
export function cell(s: GameState, side: number, t: number, k: number): [number, number] {
  return side === 0 ? [t, k] : side === 1 ? [s.width - 1 - k, t] : side === 2 ? [t, s.height - 1 - k] : [k, t];
}

/** Whether tile (x, y) is in band `b`: its kind, less than its depth in from its side. */
export function inBand(s: GameState, b: Band, x: number, y: number, at = frameOf(s)): boolean {
  const i = at[y * s.width + x]!;
  if (i < 0 || s.frame![i]!.kind !== b.kind) return false;
  const k = b.side === 0 ? y : b.side === 1 ? s.width - 1 - x : b.side === 2 ? s.height - 1 - y : x;
  const t = b.side % 2 ? y : x;
  return k < b.depth && t >= b.from && t <= b.to;
}

/**
 * Where the camera may look on a map with an outline: the bounds of what is
 * played, without the entrances' strips out to the edge (the rows and columns
 * with less than a quarter of their tiles played); null without an outline.
 */
export function playBounds(s: GameState): { x0: number; y0: number; x1: number; y1: number } | null {
  if (!s.frame?.length || !s.ground) return null;
  const { width: w, height: h } = s;
  const rows = new Array<number>(h).fill(0);
  const columns = new Array<number>(w).fill(0);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (s.ground[y * w + x] === 4) continue;
      rows[y]!++;
      columns[x]!++;
    }
  }
  const first = (counts: number[], least: number) => counts.findIndex((n) => n >= least);
  const last = (counts: number[], least: number) => counts.length - 1 - [...counts].reverse().findIndex((n) => n >= least);
  const [x0, x1, y0, y1] = [first(columns, h / 4), last(columns, h / 4), first(rows, w / 4), last(rows, w / 4)];
  return x0 < 0 || y0 < 0 ? null : { x0, y0, x1: x1 + 1, y1: y1 + 1 };
}
