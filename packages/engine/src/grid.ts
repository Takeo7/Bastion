import type { CoverLevel, GameState, TileKind, Unit, Vec2 } from './types';

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

/** Units can walk across the tile (doors open, windows are vaulted). */
export function isWalkable(kind: TileKind): boolean {
  return kind === 'floor' || kind === 'ladder' || kind === 'door' || kind === 'doorOpen' || kind === 'window' || kind === 'windowBroken';
}

/** Units can end a move on the tile (nobody stands inside a window frame). */
export function canStandOn(kind: TileKind): boolean {
  return isWalkable(kind) && kind !== 'window' && kind !== 'windowBroken';
}

export function isWindow(kind: TileKind): boolean {
  return kind === 'window' || kind === 'windowBroken';
}

/** Top of the opaque column on a tile, in absolute storeys (raised floors are solid underneath). */
export function sightTop(s: GameState, x: number, y: number): number {
  if (!inBounds(s, x, y)) return Infinity;
  const kind = tileAt(s, x, y);
  return elevAt(s, x, y) + OBSTACLE_HEIGHT[kind];
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

/** Legacy flat check: whether the object on a tile blocks sight at ground level. */
export function blocksSight(kind: TileKind): boolean {
  return OBSTACLE_HEIGHT[kind] >= 0.75;
}

/** Cover offered by the tile `side` to a unit standing at elevation `standElev`. */
export function coverFrom(s: GameState, standElev: number, side: Vec2): CoverLevel {
  if (!inBounds(s, side.x, side.y)) return 2;
  const kind = tileAt(s, side.x, side.y);
  const rel = elevAt(s, side.x, side.y) + COVER_HEIGHT[kind] - standElev;
  if (rel >= 0.75) return 2;
  if (rel >= 0.3) return 1;
  return 0;
}

/** Flat cover value of a tile kind (used by map tooling and tests). */
export function coverOf(kind: TileKind): CoverLevel {
  const h = COVER_HEIGHT[kind];
  return h >= 0.75 ? 2 : h >= 0.3 ? 1 : 0;
}

export function sameTile(a: Vec2, b: Vec2): boolean {
  return a.x === b.x && a.y === b.y;
}

export function distance(a: Vec2, b: Vec2): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

export function unitAt(s: GameState, p: Vec2): Unit | undefined {
  return s.units.find((u) => u.alive && u.pos.x === p.x && u.pos.y === p.y);
}

export function getUnit(s: GameState, id: string): Unit | undefined {
  return s.units.find((u) => u.id === id);
}

export function aliveUnits(s: GameState, team?: Unit['team']): Unit[] {
  return s.units.filter((u) => u.alive && (team === undefined || u.team === team));
}

/** Living enemies that fight: the relay and captive VIPs don't count. */
export function combatants(s: GameState, team: Unit['team']): Unit[] {
  return s.units.filter((u) => u.alive && u.team === team && !u.objective && !u.captive);
}
