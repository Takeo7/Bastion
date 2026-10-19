import { TEMPLATES, TRAPS } from './content';
import { decorOf } from './decor';
import { canStopAt, longFlightAt, type Flight } from './flights';
import { DIRS4, DIRS8, elevAt, heightAt, inBounds, isWalkable, isWindow, kindAt, nodeOf, posOf, slabAt, surfaceAt, toPos } from './grid';
import type { GameState, Place, Pos, Unit } from './types';

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
 * Cost of a single step between neighbouring tiles on the ground (without unit
 * checks), or null if the step is impossible: walls, climbing without a ladder,
 * cutting corners, or slipping diagonally between levels or through a window.
 * Maps without storeys move by it; stepBetween is the same on any level.
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

/** The way a building's stairs on `p` climb (a DIRS4 index), or null for a ladder or anything else. */
function stairsFacing(s: GameState, p: Pos): number | null {
  const decor = p.z ? s.levels?.[p.z - 1]?.decor : s.decor;
  const look = decorOf(decor?.[p.y * s.width + p.x]);
  return look?.kind === 'stairs' ? look.facing : null;
}

/**
 * Cost of a step from `from` to `to`, on the next tile, on any level (without
 * unit checks), or null if it can't be taken (docs/FASE6-PLAN.md): stepCost's
 * rules by height instead of elevation. A ladder (or a building's stairs)
 * climbs one storey onto the next tile, with nothing overhead (a stairwell);
 * going down onto one costs the same. A building's stairs are left and taken
 * at the top only by their landing, the tile they face (docs/FASE7.md); a
 * ladder by any side. Any other way down is a drop off an edge, with no floor
 * or roof in between, out of an upper window too (docs/FASE7.md). A flight of
 * several tiles is flightStep's.
 */
export function stepBetween(s: GameState, from: Pos, to: Pos): number | null {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  if (Math.max(Math.abs(dx), Math.abs(dy)) !== 1 || !inBounds(s, to.x, to.y)) return null;
  const fromKind = kindAt(s, from);
  const toKind = kindAt(s, to);
  if (!isWalkable(toKind)) return null;
  const on = fromKind === 'ladder' ? longFlightAt(s, from) : null;
  const onto = toKind === 'ladder' ? longFlightAt(s, to) : null;
  if (on || onto) return flightStep(s, from, to, on, onto);
  const h = heightAt(s, from);
  const landing = heightAt(s, to);
  const dz = landing - h;

  if (dx !== 0 && dy !== 0) {
    if (dz !== 0 || isWindow(fromKind) || isWindow(toKind)) return null;
    for (const [ox, oy] of [[to.x, from.y], [from.x, to.y]] as const) {
      const side = surfaceAt(s, ox, oy, h);
      // Nor past the corner of a flight's banister.
      if (!side || isWindow(kindAt(s, side)) || (kindAt(s, side) === 'ladder' && longFlightAt(s, side))) return null;
    }
    return COST_DIAG;
  }

  let cost = COST_ORTHO + (isWindow(toKind) ? COST_VAULT : 0);
  if (dz > 0) {
    if (dz !== 1 || fromKind !== 'ladder' || slabAt(s, from.x, from.y, h + 1)) return null;
    const up = stairsFacing(s, from);
    if (up !== null && (DIRS4[up]!.x !== dx || DIRS4[up]!.y !== dy)) return null;
    cost += COST_CLIMB;
  } else if (dz < 0) {
    if (-dz > MAX_DROP) return null;
    for (let k = landing + 1; k <= h; k++) if (slabAt(s, to.x, to.y, k)) return null;
    const up = toKind === 'ladder' && dz === -1 ? stairsFacing(s, to) : null;
    if (up !== null && (DIRS4[up]!.x !== -dx || DIRS4[up]!.y !== -dy)) return null;
    cost += toKind === 'ladder' && dz === -1 ? COST_CLIMB : COST_DROP * -dz;
  }
  return cost;
}

/**
 * A step onto, along or off a flight of stairs of several tiles (docs/MAPAS.md,
 * phase 2), `on` the one `from` is on and `onto` the one `to` is on: only
 * along it, the way it climbs or back; on it from its foot, off it at the top
 * onto its landing a storey up (and down the same way), off it at the bottom
 * onto its foot. Never across its banisters, nor diagonally. Each step costs
 * a tile's walk, the one up to the landing too (no climb on top): a flight of
 * three, foot to landing, costs as four tiles.
 */
function flightStep(s: GameState, from: Pos, to: Pos, on: { flight: Flight; step: number } | null, onto: { flight: Flight; step: number } | null): number | null {
  const f = (on ?? onto)!.flight;
  const u = DIRS4[f.up]!;
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const along = dx === u.x && dy === u.y ? 1 : dx === -u.x && dy === -u.y ? -1 : 0;
  if (!along) return null;
  const top = f.tiles.length - 1;
  const rise = heightAt(s, to) - heightAt(s, from);
  const cost = COST_ORTHO + (isWindow(kindAt(s, to)) ? COST_VAULT : 0);
  if (on && onto) return from.z === to.z && on.flight.tiles[0]!.x === onto.flight.tiles[0]!.x && on.flight.tiles[0]!.y === onto.flight.tiles[0]!.y && onto.step === on.step + along ? cost : null;
  if (on) {
    // Off at the top onto the landing, or at the bottom onto the foot.
    if (along > 0) return on.step === top && to.z === f.z + 1 && rise === 1 ? cost : null;
    return on.step === 0 && to.z === f.z && rise === 0 ? cost : null;
  }
  // On from the foot, or down from the landing onto the top.
  if (along > 0) return onto!.step === 0 && from.z === f.z && rise === 0 ? cost : null;
  return onto!.step === top && from.z === f.z + 1 && rise === -1 ? cost : null;
}

/**
 * Every step from `from` onto the tile (dx, dy) away, to each of its levels a
 * unit can get to, with its cost. On a map without storeys, stepCost's one.
 * With storeys there are three at most, each checked by stepBetween: the floor
 * at the same height, the one a storey up (from a ladder or stairs), and the
 * highest one below (going down, or a drop).
 */
export function stepsFrom(s: GameState, from: Pos, dx: number, dy: number): { to: Pos; cost: number }[] {
  if (!s.levels) {
    const cost = from.z === 0 ? stepCost(s, from.x, from.y, dx, dy) : null;
    return cost === null ? [] : [{ to: { x: from.x + dx, y: from.y + dy, z: 0 }, cost }];
  }
  const out: { to: Pos; cost: number }[] = [];
  const nx = from.x + dx;
  const ny = from.y + dy;
  if (!inBounds(s, nx, ny)) return out;
  const take = (to: Pos | null) => {
    const cost = to && stepBetween(s, from, to);
    if (to && cost !== null && cost !== undefined) out.push({ to, cost });
  };
  const h = heightAt(s, from);
  take(surfaceAt(s, nx, ny, h));
  if (dx !== 0 && dy !== 0) return out;
  if (kindAt(s, from) === 'ladder') take(surfaceAt(s, nx, ny, h + 1));
  for (let k = h - 1; k >= Math.max(0, h - MAX_DROP); k--) {
    const below = surfaceAt(s, nx, ny, k);
    if (below) {
      take(below);
      break;
    }
    if (slabAt(s, nx, ny, k) || elevAt(s, nx, ny) > k) break;
  }
  return out;
}

export interface Reach {
  origin: Pos;
  /** node (grid.ts nodeOf; on the ground, the tile index) -> accumulated cost */
  cost: Map<number, number>;
  /** node -> previous node */
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

/** An alien whose pod is still on patrol: the squad hasn't run into it yet. */
export function onPatrol(s: GameState, u: Unit): boolean {
  return u.team === 'alien' && !!u.podId && s.pods.some((p) => p.id === u.podId && !p.active);
}

/**
 * Dijkstra over 8 directions, on every level (docs/FASE6-PLAN.md: up and down
 * ladders and stairs, off edges). Every living unit but the walker blocks its
 * place, whoever's side (Arturo, 19/10: «los compañeros se bloquean», as in
 * XCOM): a soldier, one of the dead, a turret, the VIP; not the floor above or
 * below. Diagonals may not cut the corners of blocked tiles. A pod on patrol
 * keeps to doors and open ground (playtest: undiscovered aliens went into a
 * building through a window): vaulting in is for aliens in the fight. In a
 * siege the dead don't open doors: they break them down (docs/FASE12.md §4).
 * `ignore`: units a plan may look past, as if gone (ai.ts breachPlan asks
 * whether the ground leaves a way, its own crowd about to move); never for a
 * walk.
 */
export function computeReach(s: GameState, u: Unit, maxCost: number, from: Pos = u.pos, ignore?: (o: Unit) => boolean): Reach {
  const windows = !onPatrol(s, u);
  const shut = !!s.mission.siege && !!TEMPLATES[u.template].undead;
  // Barbed wire (docs/FASE12.md §11.4): costlier to cross, for everyone.
  const slow = s.slow?.length ? new Set(s.slow) : null;
  const taken = new Set<number>();
  for (const o of s.units) {
    if (o.alive && o !== u && !ignore?.(o)) taken.add(nodeOf(s, o.pos));
  }
  const blocked = (p: Pos | null) => !!p && taken.has(nodeOf(s, p));
  const start = nodeOf(s, from);
  const cost = new Map<number, number>([[start, 0]]);
  const prev = new Map<number, number>();
  const heap = new MinHeap();
  heap.push(0, start);

  while (heap.size) {
    const [c, i] = heap.pop();
    if (c > (cost.get(i) ?? Infinity)) continue;
    const p = posOf(s, i);
    for (const d of DIRS8) {
      for (const { to, cost: step } of stepsFrom(s, p, d.x, d.y)) {
        const ni = nodeOf(s, to);
        if (taken.has(ni) || (!windows && isWindow(kindAt(s, to))) || (shut && kindAt(s, to) === 'door')) continue;
        if (d.x !== 0 && d.y !== 0) {
          const h = heightAt(s, p);
          if (blocked(surfaceAt(s, to.x, p.y, h)) || blocked(surfaceAt(s, p.x, to.y, h))) continue;
        }
        const nc = c + step + (slow?.has(ni) ? TRAPS.wire : 0);
        if (nc > maxCost) continue;
        if (nc < (cost.get(ni) ?? Infinity)) {
          cost.set(ni, nc);
          prev.set(ni, i);
          heap.push(nc, ni);
        }
      }
    }
  }
  return { origin: from, cost, prev };
}

function occupied(s: GameState, i: number, ignore: Unit): boolean {
  const p = posOf(s, i);
  return s.units.some((o) => o !== ignore && o.alive && o.pos.x === p.x && o.pos.y === p.y && o.pos.z === p.z);
}

/** Tiles the unit may end its move on (excludes origin, occupied tiles, window frames and flights of stairs: flights.ts canStopAt). */
export function destinations(s: GameState, u: Unit, reach: Reach): { pos: Pos; cost: number }[] {
  const out: { pos: Pos; cost: number }[] = [];
  const start = nodeOf(s, reach.origin);
  for (const [i, c] of reach.cost) {
    const p = posOf(s, i);
    if (i === start || occupied(s, i, u) || !canStopAt(s, p)) continue;
    out.push({ pos: p, cost: c });
  }
  return out;
}

/** Path from the reach origin to `to` (origin excluded), or null if unreachable / occupied. */
export function buildPath(s: GameState, u: Unit, reach: Reach, to: Place): Pos[] | null {
  if (!inBounds(s, to.x, to.y)) return null;
  const end = nodeOf(s, toPos(to));
  const start = nodeOf(s, reach.origin);
  if (end === start || !reach.cost.has(end) || occupied(s, end, u) || !canStopAt(s, posOf(s, end))) return null;
  const path: Pos[] = [];
  let i: number | undefined = end;
  while (i !== undefined && i !== start) {
    path.push(posOf(s, i));
    i = reach.prev.get(i);
  }
  return path.reverse();
}
