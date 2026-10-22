import { decorSightHeight } from './decor';
import type { CoverLevel, GameState, Place, Pos, TileKind, Unit, Vec2 } from './types';

/** Cardinal directions in N, E, S, W order (y grows southwards). */
export const DIRS4: readonly Vec2[] = [
  { x: 0, y: -1 },
  { x: 1, y: 0 },
  { x: 0, y: 1 },
  { x: -1, y: 0 },
];

export const DIRS8: readonly Vec2[] = [
  ...DIRS4,
  { x: 1, y: -1 },
  { x: 1, y: 1 },
  { x: -1, y: 1 },
  { x: -1, y: -1 },
];

// Heights are measured in storeys. A wall is exactly one storey tall.
/** Eye height of a unit above the floor it stands on. */
export const EYE_HEIGHT = 0.7;
/** Point on a target that a shot or a glance has to reach. */
export const BODY_HEIGHT = 0.55;

/** Height of what stands on a tile, above the tile's own elevation. */
const OBSTACLE_HEIGHT: Record<TileKind, number> = {
  floor: 0,
  ladder: 0,
  doorOpen: 0,
  wall: 1,
  door: 1,
  console: 0.8,
  high: 0.8,
  low: 0.4,
  // A roof nobody stands on: the slab is all there is (docs/FASE6-PLAN.md).
  roof: 0,
  // Windows are holes in a wall: they never stop a line of sight...
  window: 0,
  windowBroken: 0,
};

/** ...but whoever stands beside one is protected by the wall around it. */
const COVER_HEIGHT: Record<TileKind, number> = { ...OBSTACLE_HEIGHT, window: 1, windowBroken: 1 };

export function inBounds(s: GameState, x: number, y: number): boolean {
  return x >= 0 && y >= 0 && x < s.width && y < s.height;
}

export function tileIndex(s: GameState, x: number, y: number): number {
  return y * s.width + x;
}

/** Out-of-bounds reads behave like an indestructible wall. */
export function tileAt(s: GameState, x: number, y: number): TileKind {
  return inBounds(s, x, y) ? s.tiles[tileIndex(s, x, y)]! : 'wall';
}

/** Elevation (storeys) of the tile's floor. */
export function elevAt(s: GameState, x: number, y: number): number {
  return inBounds(s, x, y) ? s.elev[tileIndex(s, x, y)] ?? 0 : 0;
}

// ---------------------------------------------------------------- levels
// Buildings of several storeys (docs/FASE6-PLAN.md): the ground is `tiles` and
// `elev`, as ever; storey k is `levels[k - 1]`, k storeys up. A place is a
// Pos: a tile and its level.

/** The highest level a map can have: the roof of a three-storey building. */
export const TOP_LEVEL = 3;

/** Levels of the map, the ground included: 1 without storeys. */
export function levelCount(s: GameState): number {
  return 1 + (s.levels?.length ?? 0);
}

/** A place's index among all of the map's (the ground's first, so on the ground it is the tile's index). */
export function nodeOf(s: GameState, p: Pos): number {
  return (p.z * s.height + p.y) * s.width + p.x;
}

/** The place with index `node` (nodeOf). */
export function posOf(s: GameState, node: number): Pos {
  const area = s.width * s.height;
  const z = Math.floor(node / area);
  const i = node - z * area;
  const x = i % s.width;
  return { x, y: (i - x) / s.width, z };
}

/** A place on the ground. */
export function ground(p: Vec2): Pos {
  return { x: p.x, y: p.y, z: 0 };
}

/** A place from the wire, the client or an older save, where a missing level is the ground. */
export function toPos(p: Place): Pos {
  return { x: p.x, y: p.y, z: p.z ?? 0 };
}

/**
 * What stands on a place: on the ground the tile, as tileAt; on a storey what
 * stands on its slab, or null where there is none (open air). Out of bounds,
 * or above the map's storeys, like tileAt: a wall on the ground, air above.
 */
export function kindAt(s: GameState, p: Pos): TileKind | null {
  if (p.z === 0) return tileAt(s, p.x, p.y);
  if (!inBounds(s, p.x, p.y)) return null;
  return s.levels?.[p.z - 1]?.tiles[tileIndex(s, p.x, p.y)] ?? null;
}

/** Height (storeys) of a place's floor: the tile's elevation on the ground, k on storey k. */
export function heightAt(s: GameState, p: Pos): number {
  return p.z === 0 ? elevAt(s, p.x, p.y) : p.z;
}

/** Whether storey `k` (≥ 1) has a slab on tile (x, y): a floor, a ceiling below it, or a roof. */
export function slabAt(s: GameState, x: number, y: number, k: number): boolean {
  if (k < 1 || !inBounds(s, x, y)) return false;
  return (s.levels?.[k - 1]?.tiles[tileIndex(s, x, y)] ?? null) !== null;
}

/**
 * The places of tile (x, y) there is something on: the ground, and each storey
 * with a slab there (docs/FASE6-PLAN.md).
 */
export function placesOn(s: GameState, x: number, y: number): Pos[] {
  const out: Pos[] = [{ x, y, z: 0 }];
  const i = tileIndex(s, x, y);
  s.levels?.forEach((l, k) => {
    if ((l.tiles[i] ?? null) !== null) out.push({ x, y, z: k + 1 });
  });
  return out;
}

/**
 * Whether what happens on `a` (a blast, smoke, a song, an aura) reaches `b`,
 * height-wise (docs/FASE6-PLAN.md): at most a storey apart, with no floor or
 * ceiling between them in either's column — a floor stops a blast (decision
 * 5). Without storeys, always: the map was flat for it.
 */
export function sameSpace(s: GameState, a: Place, b: Place): boolean {
  if (!s.levels) return true;
  const pa = toPos(a);
  const pb = toPos(b);
  const ha = heightAt(s, pa);
  const hb = heightAt(s, pb);
  if (Math.abs(ha - hb) > 1 + 1e-6) return false;
  const lo = Math.min(ha, hb);
  const hi = Math.max(ha, hb);
  for (let k = Math.floor(lo + 1e-6) + 1; k <= hi + 1e-6; k++) if (slabAt(s, pa.x, pa.y, k) || slabAt(s, pb.x, pb.y, k)) return false;
  return true;
}

/**
 * Storeys between two places, for how far a noise carries (decision 3 in
 * FASE6-PLAN.md: +1 of distance a storey). None without storeys.
 */
export function storeysApart(s: GameState, a: Place, b: Place): number {
  return s.levels ? Math.abs(Math.round(heightAt(s, toPos(a))) - Math.round(heightAt(s, toPos(b)))) : 0;
}

/**
 * The level of tile (x, y) whose floor is at height `h` and can be walked on,
 * or null: the ground when its elevation is `h`, else storey `h`.
 */
export function surfaceAt(s: GameState, x: number, y: number, h: number): Pos | null {
  if (!inBounds(s, x, y)) return null;
  if (elevAt(s, x, y) === h && isWalkable(tileAt(s, x, y))) return { x, y, z: 0 };
  if (h < 1 || h > TOP_LEVEL || h !== Math.floor(h)) return null;
  const kind = s.levels?.[h - 1]?.tiles[tileIndex(s, x, y)] ?? null;
  return kind !== null && isWalkable(kind) ? { x, y, z: h } : null;
}

/** Units can walk across the tile (doors open, windows are vaulted); never open air. */
export function isWalkable(kind: TileKind | null): boolean {
  return kind === 'floor' || kind === 'ladder' || kind === 'door' || kind === 'doorOpen' || kind === 'window' || kind === 'windowBroken';
}

/** Units can end a move on the tile (nobody stands inside a window frame); by its kind alone: a flight of stairs is flights.ts canStopAt's. */
export function canStandOn(kind: TileKind | null): boolean {
  return isWalkable(kind) && kind !== 'window' && kind !== 'windowBroken';
}

export function isWindow(kind: TileKind | null): boolean {
  return kind === 'window' || kind === 'windowBroken';
}

/** Top of the opaque column on a tile, in absolute storeys (raised floors are solid underneath). */
export function sightTop(s: GameState, x: number, y: number): number {
  if (!inBounds(s, x, y)) return Infinity;
  return elevAt(s, x, y) + groundSightHeight(s, tileIndex(s, x, y));
}

/**
 * How tall what stands on ground tile `i` is for sight: its kind's height, but for full cover lower than the eyes,
 * the object's own (decor.ts decorSightHeight: Arturo, 22/10). Cover goes by the kind alone.
 */
export function groundSightHeight(s: GameState, i: number): number {
  const kind = s.tiles[i]!;
  return (kind === 'high' ? decorSightHeight(s.decor, s.width, i) : null) ?? OBSTACLE_HEIGHT[kind];
}

/** Storeys of the building a tile belongs to (GameState.storeys): 0 outside one, and on maps without them. */
export function storeysAt(s: GameState, x: number, y: number): number {
  return inBounds(s, x, y) ? s.storeys?.[tileIndex(s, x, y)] ?? 0 : 0;
}

/**
 * Whether what stands on a tile stops a line of sight crossing it at height
 * `h` (absolute storeys): below the top of its object, as ever, and in a
 * building of two storeys or more, anywhere from the ground storey's ceiling
 * up to its roof. So its walls and closed doors go all the way up, its windows
 * and doorways are holes in the ground storey under a lintel, and above the
 * ground storey its floors and ceilings fill the inside. The roof itself (also
 * a one-storey building's) is lineClear's: see roofSide. A map without storeys
 * is as it always was.
 */
export function blocksSightAt(s: GameState, x: number, y: number, h: number): boolean {
  if (sightTop(s, x, y) > h + 1e-6) return true;
  const n = storeysAt(s, x, y);
  if (n < 2) return false;
  const base = elevAt(s, x, y);
  return h > base + 1 - 1e-6 && base + n > h + 1e-6;
}

/**
 * Where a line at height `h` crossing a building's tile stands against it:
 * 'under' its ground storey's ceiling, 'over' its roof, or null (no building,
 * or inside its upper storeys, which blocksSightAt stops). A line that goes
 * from one to the other between two tiles of a building went through its roof.
 */
export function roofSide(s: GameState, x: number, y: number, h: number): 'under' | 'over' | null {
  const n = storeysAt(s, x, y);
  if (n < 1) return null;
  const base = elevAt(s, x, y);
  if (h < base + 1 - 1e-6) return 'under';
  return h > base + n - 1e-6 ? 'over' : null;
}

/**
 * On a map with storeys (docs/FASE6-PLAN.md), whether what stands on tile `i`
 * (on the map: lineClear's walk) stops a line crossing it at height `h`: the
 * ground and its object, as sightTop, and on each storey what stands on its
 * slab, from the slab up — a wall or a closed door the whole storey, a window
 * nothing (a hole on its floor). The slabs, floors and ceilings, are
 * lineClear's: a line can only cross one between two tiles. `storeys` plays
 * no part.
 */
export function opaqueAtIndex(s: GameState, i: number, h: number): boolean {
  if ((s.elev[i] ?? 0) + groundSightHeight(s, i) > h + 1e-6) return true;
  const k = Math.floor(h + 1e-6);
  if (k < 1 || !s.levels || k > s.levels.length) return false;
  const kind = s.levels[k - 1]!.tiles[i] ?? null;
  return kind !== null && k + OBSTACLE_HEIGHT[kind] > h + 1e-6;
}

/** Height of what stands on a tile of `kind` above its floor (what opaqueAtIndex compares). */
export function obstacleHeight(kind: TileKind): number {
  return OBSTACLE_HEIGHT[kind];
}


/** Cover offered by the tile `side` to a unit standing at elevation `standElev`. */
export function coverFrom(s: GameState, standElev: number, side: Vec2): CoverLevel {
  if (!inBounds(s, side.x, side.y)) return 2;
  const kind = tileAt(s, side.x, side.y);
  return coverLevel(elevAt(s, side.x, side.y) + COVER_HEIGHT[kind] - standElev);
}

function coverLevel(rel: number): CoverLevel {
  if (rel >= 0.75) return 2;
  if (rel >= 0.3) return 1;
  return 0;
}

/**
 * Cover the tile `side` gives a unit standing on `stand`, on its level
 * (docs/FASE6-PLAN.md): what stands on the ground beside it (a raised block
 * taller than the unit is a wall), or on the storey it stands on — its walls
 * and windows full, furniture as on the ground, a roof's parapet half. Open
 * air beside (a roof's edge) gives nothing. Without storeys, coverFrom.
 */
export function coverAt(s: GameState, stand: Pos, side: Vec2): CoverLevel {
  const h = heightAt(s, stand);
  if (!s.levels || !inBounds(s, side.x, side.y)) return coverFrom(s, h, side);
  let top = elevAt(s, side.x, side.y) + COVER_HEIGHT[tileAt(s, side.x, side.y)];
  for (let k = 1; k <= s.levels.length; k++) {
    const kind = s.levels[k - 1]!.tiles[tileIndex(s, side.x, side.y)] ?? null;
    if (kind !== null && k <= h + 1e-6) top = Math.max(top, k + COVER_HEIGHT[kind]);
  }
  return coverLevel(top - h);
}

/** Flat cover value of a tile kind (used by map tooling and tests). */
export function coverOf(kind: TileKind): CoverLevel {
  const h = COVER_HEIGHT[kind];
  return h >= 0.75 ? 2 : h >= 0.3 ? 1 : 0;
}

/** Same tile, whatever the level (sameTile) — see samePos for places. */
export function sameTile(a: Vec2, b: Vec2): boolean {
  return a.x === b.x && a.y === b.y;
}

/** Same place: same tile and same level. */
export function samePos(a: Pos, b: Pos): boolean {
  return a.x === b.x && a.y === b.y && a.z === b.z;
}

export function distance(a: Vec2, b: Vec2): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

/** The living unit on a place (its level too; without one, on the ground). */
export function unitAt(s: GameState, p: Place): Unit | undefined {
  const z = p.z ?? 0;
  return s.units.find((u) => u.alive && u.pos.x === p.x && u.pos.y === p.y && u.pos.z === z);
}

export function getUnit(s: GameState, id: string): Unit | undefined {
  return s.units.find((u) => u.id === id);
}

/** Living enemies that fight: the relay and captive VIPs don't count. */
export function combatants(s: GameState, team: Unit['team']): Unit[] {
  return s.units.filter((u) => u.alive && u.team === team && !u.objective && !u.captive);
}
