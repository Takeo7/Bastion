import { decorOf } from './decor';
import { canStandOn, DIRS4, inBounds, kindAt, tileIndex } from './grid';
import type { Decor, GameState, Level, Place, Pos, TileKind, Vec2 } from './types';

// docs/MAPAS.md, phase 2: a building's stairs are a flight of several tiles
// (3, 2 in small houses: Arturo, 19/10), 38° as real ones, not a storey up in
// one tile. Its tiles are `ladder` tiles on the storey it climbs from, in a
// line the way it climbs: the top one dressed 'stairs' facing that way (as
// the one-tile flights of before), each below it a 'part' facing it too
// (decor.ts objectTiles). The foot is the tile behind the lowest, on that
// storey; the landing the tile in front of the top, a storey up; over the
// flight the storey above has a hole (the stairwell).
//
// One goes on a flight only from its foot, the way it climbs, and off it
// only at its landing (and back down), never off its sides past the
// banisters; nobody stops on it (Arturo, 19/10: «solo se entra por el pie y
// nadie se para a mitad»). A flight without parts is one of before, of one
// tile: it keeps the rules it had (docs/FASE7.md §2), so a mission saved
// before, and «La torre», play as they did.

/** The longest flight there is (docs/MAPAS.md §4.4). */
export const MAX_FLIGHT = 3;

/** A building's flight of stairs. */
export interface Flight {
  /** The level its tiles stand on: its landing is a storey up. */
  z: number;
  /** The way it climbs (a DIRS4 index). */
  up: number;
  /** Its tiles from the lowest up (the last, the top, dressed 'stairs'). */
  tiles: Vec2[];
}

/** The decor of level z, if any. */
const decorOn = (s: GameState, z: number) => (z ? s.levels?.[z - 1]?.decor : s.decor);

/**
 * The flight of stairs place `p` is a tile of, and which of its tiles it is
 * (0 the lowest), or null: one of a single tile too (one of before). On a map
 * without storeys (as a mission saved before them), none: there is nothing to
 * climb to.
 */
export function flightAt(s: GameState, p: Pos): { flight: Flight; step: number } | null {
  if (!s.levels || kindAt(s, p) !== 'ladder') return null;
  const decor = decorOn(s, p.z);
  if (!decor) return null;
  // Up the parts to the top.
  let top: Vec2 = { x: p.x, y: p.y };
  let look = decorOf(decor[tileIndex(s, p.x, p.y)]);
  const facings: number[] = [];
  for (let k = 0; look?.kind === 'part' && k < MAX_FLIGHT; k++) {
    facings.push(look.facing);
    const d = DIRS4[look.facing]!;
    top = { x: top.x + d.x, y: top.y + d.y };
    if (!inBounds(s, top.x, top.y) || kindAt(s, { ...top, z: p.z }) !== 'ladder') return null;
    look = decorOf(decor[tileIndex(s, top.x, top.y)]);
  }
  if (look?.kind !== 'stairs' || facings.some((f) => f !== look!.facing)) return null;
  const up = look.facing;
  const d = DIRS4[up]!;
  // Down from the top: the parts behind it that climb to it.
  const tiles: Vec2[] = [top];
  for (let n = 1; n < MAX_FLIGHT; n++) {
    const q = { x: top.x - d.x * n, y: top.y - d.y * n };
    if (!inBounds(s, q.x, q.y) || kindAt(s, { ...q, z: p.z }) !== 'ladder') break;
    const part = decorOf(decor[tileIndex(s, q.x, q.y)]);
    if (part?.kind !== 'part' || part.facing !== up) break;
    tiles.unshift(q);
  }
  const step = tiles.length - 1 - facings.length;
  return step < 0 ? null : { flight: { z: p.z, up, tiles }, step };
}

/** flightAt for a flight of several tiles; null on one of a single tile (one of before), as on anything else. */
export function longFlightAt(s: GameState, p: Pos): { flight: Flight; step: number } | null {
  const at = flightAt(s, p);
  return at && at.flight.tiles.length > 1 ? at : null;
}

/** Where one steps onto a flight from: the tile behind its lowest, on its level. */
export function flightFoot(f: Flight): Pos {
  const d = DIRS4[f.up]!;
  return { x: f.tiles[0]!.x - d.x, y: f.tiles[0]!.y - d.y, z: f.z };
}

/** Where one steps off a flight at the top: the tile in front of it, a storey up. */
export function flightLanding(f: Flight): Pos {
  const d = DIRS4[f.up]!;
  const top = f.tiles[f.tiles.length - 1]!;
  return { x: top.x + d.x, y: top.y + d.y, z: f.z + 1 };
}

/** Whether place `p` is on a flight of several tiles, where nobody stops. */
export function midFlight(s: GameState, p: Place): boolean {
  const q: Pos = { x: p.x, y: p.y, z: p.z ?? 0 };
  return kindAt(s, q) === 'ladder' && !!longFlightAt(s, q);
}

/**
 * Whether a unit can end a move on (or be put on) place `p`: what stands
 * there lets it (grid.ts canStandOn: not a window's frame), and it is not on
 * a flight of stairs of several tiles.
 */
export function canStopAt(s: GameState, p: Place): boolean {
  const q: Pos = { x: p.x, y: p.y, z: p.z ?? 0 };
  const kind = kindAt(s, q);
  return canStandOn(kind) && (kind !== 'ladder' || !longFlightAt(s, q));
}

/**
 * Where what comes to rest on place `p` (a body's gear, the objective)
 * stays: on `p`, or off a flight of several tiles at its nearer end (its
 * foot, or its landing), where one can stop and pick it up.
 */
export function restingPlace(s: GameState, p: Pos): Pos {
  const on = kindAt(s, p) === 'ladder' ? longFlightAt(s, p) : null;
  if (!on) return { ...p };
  return on.step * 2 < on.flight.tiles.length - 1 ? flightFoot(on.flight) : flightLanding(on.flight);
}

/**
 * Whether the slab of storey place `p` (≥ 1) bears a flight of several
 * tiles: one of its tiles, its foot or its landing. A grenade never breaks it
 * (docs/MAPAS.md, phase 2): no flight is left hanging, nor half of one.
 */
export function bearsFlight(s: GameState, p: Pos): boolean {
  if (p.z < 1) return false;
  if (midFlight(s, p)) return true;
  for (const d of DIRS4) {
    // Its foot: the flight starts beside it, on its level, climbing away from it.
    const next = { x: p.x + d.x, y: p.y + d.y, z: p.z };
    const on = inBounds(s, next.x, next.y) && kindAt(s, next) === 'ladder' ? longFlightAt(s, next) : null;
    if (on && on.step === 0 && DIRS4[on.flight.up]!.x === d.x && DIRS4[on.flight.up]!.y === d.y) return true;
    // Its landing: the top of a flight beside it, a storey down, climbing towards it.
    const below = { x: p.x + d.x, y: p.y + d.y, z: p.z - 1 };
    const under = inBounds(s, below.x, below.y) && kindAt(s, below) === 'ladder' ? longFlightAt(s, below) : null;
    if (under && under.step === under.flight.tiles.length - 1 && DIRS4[under.flight.up]!.x === -d.x && DIRS4[under.flight.up]!.y === -d.y) return true;
  }
  return false;
}

/**
 * Whether tile `i` of one level (its `tiles` and `decor`, `width` wide) is on
 * a flight of several tiles: a part of one, or a top with one behind it. For
 * what works on a level's arrays (furnishing.ts), without a state.
 */
export function longFlightTile(tiles: readonly (TileKind | null)[], decor: readonly Decor[] | undefined, width: number, i: number): boolean {
  if (tiles[i] !== 'ladder') return false;
  const look = decorOf(decor?.[i]);
  if (look?.kind === 'part') return true;
  if (look?.kind !== 'stairs') return false;
  const d = DIRS4[look.facing]!;
  const x = (i % width) - d.x;
  const y = Math.floor(i / width) - d.y;
  if (x < 0 || y < 0 || x >= width || y * width >= tiles.length) return false;
  const behind = decorOf(decor?.[y * width + x]);
  return tiles[y * width + x] === 'ladder' && behind?.kind === 'part' && behind.facing === look.facing;
}

/** Maps by the levels they were built with, and whether they have flights of several tiles. */
const known = new WeakMap<Level[], boolean>();

/**
 * Whether the map has flights of stairs of several tiles (docs/MAPAS.md,
 * phase 2), as generated maps do since; a map without (one saved before, a
 * hand-made one) plays as it did.
 */
export function hasLongFlights(s: GameState): boolean {
  if (!s.levels) return false;
  const seen = known.get(s.levels);
  if (seen !== undefined) return seen;
  const decors = [s.decor, ...s.levels.map((l) => l.decor)];
  const out = decors.some((decor, z) => !!decor?.some((code, i) => {
    if (decorOf(code)?.kind !== 'stairs') return false;
    return !!longFlightAt(s, { x: i % s.width, y: Math.floor(i / s.width), z });
  }));
  known.set(s.levels, out);
  return out;
}
