import { TEMPLATES } from './content';
import { canStandOn, DIRS8, inBounds, isWalkable, isWindow } from './grid';
import type { GameState, Unit, Vec2 } from './types';

/** Movement costs in half-tiles so diagonals (1.5 tiles) stay integral. */
export const COST_ORTHO = 2;
export const COST_DIAG = 3;
/** Extra cost to climb a ladder one storey up (or down it). */
export const COST_CLIMB = 2;
/** Extra cost per storey when dropping off an edge. */
export const COST_DROP = 2;
/** Extra cost to vault through a window. */
export const COST_VAULT = 2;
/** Deepest drop a unit will jump down, in storeys. */
const MAX_DROP = 2;

/**
 * Cost of a single step between neighbouring tiles (without unit checks), or
 * null if the step is impossible: walls, climbing without a ladder, cutting
 * corners, or slipping diagonally between levels or through a window.
 */
export function stepCost(s: GameState, x: number, y: number, dx: number, dy: number): number | null {
  const nx = x + dx;
  const ny = y + dy;
  if (!inBounds(s, nx, ny)) return null;
  const w = s.width;
  const from = s.tiles[y * w + x]!;
  const to = s.tiles[ny * w + nx]!;
  if (!isWalkable(to)) return null;
  const fromElev = s.elev[y * w + x] ?? 0;
  const dz = (s.elev[ny * w + nx] ?? 0) - fromElev;
  const diagonal = dx !== 0 && dy !== 0;

  if (diagonal) {
    if (dz !== 0 || isWindow(from) || isWindow(to)) return null;
    for (const [ox, oy] of [[nx, y], [x, ny]] as const) {
      const side = s.tiles[oy * w + ox]!;
      if (!isWalkable(side) || isWindow(side) || (s.elev[oy * w + ox] ?? 0) !== fromElev) return null;
    }
    return COST_DIAG;
  }

  let cost = COST_ORTHO + (isWindow(to) ? COST_VAULT : 0);
  if (dz > 0) {
    if (dz !== 1 || from !== 'ladder') return null;
    cost += COST_CLIMB;
  } else if (dz < 0) {
    if (-dz > MAX_DROP) return null;
    cost += to === 'ladder' && dz === -1 ? COST_CLIMB : COST_DROP * -dz;
  }
  return cost;
}

export interface Reach {
  origin: Vec2;
  /** tile index -> accumulated cost */
  cost: Map<number, number>;
  /** tile index -> previous tile index */
  prev: Map<number, number>;
}

/** Cost budget covered by one action point. */
export function costPerAp(u: Unit): number {
  return (TEMPLATES[u.template].mobility + u.mods.mobility) * COST_ORTHO;
}

export function maxMoveCost(u: Unit): number {
  return costPerAp(u) * Math.min(u.ap, 2);
}

/** AP needed to move with the given path cost (1 = blue move, 2 = dash). */
export function apForCost(u: Unit, cost: number): number {
  return cost <= costPerAp(u) ? 1 : 2;
}

class MinHeap {
  private items: [number, number][] = [];
  get size() {
    return this.items.length;
  }
  push(priority: number, value: number) {
    const a = this.items;
    a.push([priority, value]);
    let i = a.length - 1;
    while (i > 0) {
      const p = (i - 1) >> 1;
      if (a[p]![0] <= a[i]![0]) break;
      [a[p], a[i]] = [a[i]!, a[p]!];
      i = p;
    }
  }
  pop(): [number, number] {
    const a = this.items;
    const top = a[0]!;
    const last = a.pop()!;
    if (a.length) {
      a[0] = last;
      let i = 0;
      for (;;) {
        const l = 2 * i + 1;
        const r = l + 1;
        let m = i;
        if (l < a.length && a[l]![0] < a[m]![0]) m = l;
        if (r < a.length && a[r]![0] < a[m]![0]) m = r;
        if (m === i) break;
        [a[m], a[i]] = [a[i]!, a[m]!];
        i = m;
      }
    }
    return top;
  }
}

/**
 * Dijkstra over 8 directions. Allies can be walked through (but not stopped on),
 * enemies block, and diagonals may not cut corners of blocked tiles.
 */
export function computeReach(s: GameState, u: Unit, maxCost: number, from: Vec2 = u.pos): Reach {
  const w = s.width;
  const blockedByEnemy = new Set<number>();
  for (const o of s.units) {
    if (o.alive && o.team !== u.team) blockedByEnemy.add(o.pos.y * w + o.pos.x);
  }
  const start = from.y * w + from.x;
  const cost = new Map<number, number>([[start, 0]]);
  const prev = new Map<number, number>();
  const heap = new MinHeap();
  heap.push(0, start);

  while (heap.size) {
    const [c, i] = heap.pop();
    if (c > (cost.get(i) ?? Infinity)) continue;
    const x = i % w;
    const y = (i - x) / w;
    for (const d of DIRS8) {
      const nx = x + d.x;
      const ny = y + d.y;
      const step = stepCost(s, x, y, d.x, d.y);
      if (step === null) continue;
      const ni = ny * w + nx;
      if (blockedByEnemy.has(ni)) continue;
      if (d.x !== 0 && d.y !== 0 && (blockedByEnemy.has(y * w + nx) || blockedByEnemy.has(ny * w + x))) continue;
      const nc = c + step;
      if (nc > maxCost) continue;
      if (nc < (cost.get(ni) ?? Infinity)) {
        cost.set(ni, nc);
        prev.set(ni, i);
        heap.push(nc, ni);
      }
    }
  }
  return { origin: from, cost, prev };
}

function occupied(s: GameState, i: number, ignore: Unit): boolean {
  const x = i % s.width;
  const y = (i - x) / s.width;
  return s.units.some((o) => o !== ignore && o.alive && o.pos.x === x && o.pos.y === y);
}

/** Tiles the unit may end its move on (excludes origin, occupied tiles and window frames). */
export function destinations(s: GameState, u: Unit, reach: Reach): { pos: Vec2; cost: number }[] {
  const out: { pos: Vec2; cost: number }[] = [];
  const start = reach.origin.y * s.width + reach.origin.x;
  for (const [i, c] of reach.cost) {
    if (i === start || occupied(s, i, u) || !canStandOn(s.tiles[i]!)) continue;
    const x = i % s.width;
    out.push({ pos: { x, y: (i - x) / s.width }, cost: c });
  }
  return out;
}

/** Path from the reach origin to `to` (origin excluded), or null if unreachable / occupied. */
export function buildPath(s: GameState, u: Unit, reach: Reach, to: Vec2): Vec2[] | null {
  if (!inBounds(s, to.x, to.y)) return null;
  const end = to.y * s.width + to.x;
  const start = reach.origin.y * s.width + reach.origin.x;
  if (end === start || !reach.cost.has(end) || occupied(s, end, u) || !canStandOn(s.tiles[end]!)) return null;
  const path: Vec2[] = [];
  let i: number | undefined = end;
  while (i !== undefined && i !== start) {
    const x: number = i % s.width;
    path.push({ x, y: (i - x) / s.width });
    i = reach.prev.get(i);
  }
  return path.reverse();
}
