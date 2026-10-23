import { NIGHT, TEMPLATES } from './content';
import { canStopAt } from './flights';
import {
  blocksSightAt,
  BODY_HEIGHT,
  coverAt,
  DIRS4,
  distance,
  EYE_HEIGHT,
  heightAt,
  inBounds,
  isWindow,
  kindAt,
  levelCount,
  nodeOf,
  groundSightHeight,
  obstacleHeight,
  opaqueAtIndex,
  placesOn,
  roofSide,
  samePos,
  slabAt,
  tileIndex,
  toPos,
} from './grid';
import type { GameState, Place, Pos, TileKind, Unit, Vec2 } from './types';

/**
 * The most windows a line of sight goes through (playtest 2026-10-16): from
 * outside, through a window, what is inside; never on out through a second one.
 */
export const MAX_WINDOWS_SEEN_THROUGH = 1;
/**
 * How near a window (tiles, any way) one end of a line must be for it to go
 * through (Ana, 23/10 #21): who looks from right at the glass sees out, and
 * who stands right at it is seen; from further off, only the glass.
 */
export const WINDOW_SIGHT_REACH = 1;

/**
 * Whether a line from `a`, `ha` storeys up, to `b`, `hb` up (by default a
 * unit's eyes and the body of whoever stands there, on their levels), sees
 * through: on a map with storeys, through floors and ceilings only where they
 * have a hole (lineThroughStoreys); without them, as it always did (lineFlat).
 * Either way, through MAX_WINDOWS_SEEN_THROUGH windows at most, and only
 * those within `windowReach` of `a` or `b` (WINDOW_SIGHT_REACH).
 */
export function lineClear(s: GameState, a: Place, b: Place, ha = heightAt(s, toPos(a)) + EYE_HEIGHT, hb = heightAt(s, toPos(b)) + BODY_HEIGHT, windowReach = WINDOW_SIGHT_REACH): boolean {
  return s.levels ? lineThroughStoreys(s, a, b, ha, hb, windowReach) : lineFlat(s, a, b, ha, hb, windowReach);
}

/**
 * Grid walk between tile centres in 3D: the line goes from height `ha` above
 * tile `a` to height `hb` above tile `b` (absolute storeys) and is blocked by
 * any tile whose opaque column it meets (blocksSightAt: what stands there, and
 * the upper storeys of a tall building), and by a building's roof: it can't go
 * from over the roof to under the ceiling between two of the building's tiles
 * (roofSide). Start and end tiles never block, but they count for the roof.
 * When the line passes exactly through a corner it is blocked only if both
 * tiles beside the corner block (permissive, symmetric).
 */
function lineFlat(s: GameState, a: Vec2, b: Vec2, ha: number, hb: number, windowReach: number): boolean {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const nx = Math.abs(dx);
  const ny = Math.abs(dy);
  const sx = Math.sign(dx);
  const sy = Math.sign(dy);
  const len2 = dx * dx + dy * dy || 1;
  /** Height of the line where it crosses tile (x, y). */
  const lineAt = (x: number, y: number) => {
    const t = Math.min(1, Math.max(0, ((x - a.x) * dx + (y - a.y) * dy) / len2));
    return ha + (hb - ha) * t;
  };
  const blocks = (x: number, y: number) => blocksSightAt(s, x, y, lineAt(x, y));
  /** Where the line stood against the roof of the last building tile it crossed. */
  let side = roofSide(s, a.x, a.y, ha);
  const throughRoof = (x: number, y: number, h: number) => {
    const now = roofSide(s, x, y, h);
    const pierced = now !== null && side !== null && now !== side;
    side = now;
    return pierced;
  };
  let x = a.x;
  let y = a.y;
  let ix = 0;
  let iy = 0;
  let windows = 0;

  while (ix < nx || iy < ny) {
    const decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
    if (decision === 0) {
      if (blocks(x + sx, y) && blocks(x, y + sy)) return false;
      x += sx;
      y += sy;
      ix++;
      iy++;
    } else if (decision < 0) {
      x += sx;
      ix++;
    } else {
      y += sy;
      iy++;
    }
    if (x === b.x && y === b.y) return !throughRoof(x, y, hb);
    if (blocks(x, y) || throughRoof(x, y, lineAt(x, y))) return false;
    // A window counts where the line goes through it, on the ground floor (not over its roof).
    const i = y * s.width + x;
    if (isWindow(s.tiles[i]!) && lineAt(x, y) < (s.elev[i] ?? 0) + 1 && (farFromWindow(x, y, a, b, windowReach) || ++windows > MAX_WINDOWS_SEEN_THROUGH)) return false;
  }
  return true;
}

/**
 * The same walk on a map with storeys (docs/FASE6-PLAN.md), heights taken the
 * same way. On each tile the line crosses, what stands there at its height
 * stops it (opaqueAtIndex: the ground's objects; a storey's walls and closed doors,
 * not its windows). Between two tiles, a floor or ceiling it passes through
 * stops it: the slab of the tile where it crosses that height, the one it
 * leaves or the one it enters. So does one between the two heights when the
 * line goes straight up or down a single tile. Start and end tiles never
 * block, but their slabs count; corners as lineFlat.
 */
function lineThroughStoreys(s: GameState, a: Vec2, b: Vec2, ha: number, hb: number, windowReach: number): boolean {
  if (a.x === b.x && a.y === b.y) return !slabsBetween(s, a.x, a.y, ha, hb);
  // Written for speed (the fog runs it tens of thousands of times): indices, no closures.
  const w = s.width;
  const levels = s.levels!;
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const nx = Math.abs(dx);
  const ny = Math.abs(dy);
  const sx = Math.sign(dx);
  const sy = Math.sign(dy);
  const len2 = dx * dx + dy * dy;
  const rise = hb - ha;
  // The height of the line where it crosses the tile (ox, oy) away from `a` is
  // ha + rise * Math.min(1, Math.max(0, (ox * dx + oy * dy) / len2)), written
  // out where it is needed: a closure here costs more than the walk itself.
  let pi = a.y * w + a.x;
  let ph = ha;
  let x = a.x;
  let y = a.y;
  let ix = 0;
  let iy = 0;
  let windows = 0;
  while (ix < nx || iy < ny) {
    const decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
    if (decision === 0) {
      const ox = x - a.x;
      const oy = y - a.y;
      if (
        opaqueAtIndex(s, y * w + x + sx, ha + rise * Math.min(1, Math.max(0, ((ox + sx) * dx + oy * dy) / len2))) &&
        opaqueAtIndex(s, (y + sy) * w + x, ha + rise * Math.min(1, Math.max(0, (ox * dx + (oy + sy) * dy) / len2)))
      )
        return false;
      x += sx;
      y += sy;
      ix++;
      iy++;
    } else if (decision < 0) {
      x += sx;
      ix++;
    } else {
      y += sy;
      iy++;
    }
    const i = y * w + x;
    const end = x === b.x && y === b.y;
    const h = end ? hb : ha + rise * Math.min(1, Math.max(0, ((x - a.x) * dx + (y - a.y) * dy) / len2));
    // Through a floor or ceiling between the last tile and this one: the slab of the tile where it crosses that height.
    if (h !== ph) {
      const lo = Math.min(ph, h);
      const hi = Math.max(ph, h);
      for (let k = Math.floor(lo + 1e-6) + 1; k < hi - 1e-6; k++) {
        const j = (k - ph) / (h - ph) < 0.5 ? pi : i;
        if (k <= levels.length && (levels[k - 1]!.tiles[j] ?? null) !== null) return false;
      }
    }
    if (end) return true;
    if (opaqueAtIndex(s, i, h)) return false;
    // A window on the floor the line crosses at: the ground's within its first storey, else that storey's.
    const k = Math.floor(h + 1e-6);
    if (
      isWindow(h < (s.elev[i] ?? 0) + 1 ? s.tiles[i]! : k >= 1 ? (levels[k - 1]?.tiles[i] ?? null) : null) &&
      (farFromWindow(x, y, a, b, windowReach) || ++windows > MAX_WINDOWS_SEEN_THROUGH)
    )
      return false;
    pi = i;
    ph = h;
  }
  return true;
}

/** Whether a window on (x, y) is further than `reach` from both ends of a line, `a` and `b`: it shows only the glass. */
function farFromWindow(x: number, y: number, a: Vec2, b: Vec2, reach: number): boolean {
  return Math.max(Math.abs(x - a.x), Math.abs(y - a.y)) > reach && Math.max(Math.abs(x - b.x), Math.abs(y - b.y)) > reach;
}

/** Whether a slab of tile (x, y) lies strictly between heights `a` and `b`. */
function slabsBetween(s: GameState, x: number, y: number, a: number, b: number): boolean {
  const lo = Math.min(a, b);
  const hi = Math.max(a, b);
  for (let k = Math.floor(lo + 1e-6) + 1; k < hi - 1e-6; k++) if (slabAt(s, x, y, k)) return true;
  return false;
}

/**
 * Tiles a unit at `p` can look/shoot from: its own tile plus the XCOM-style
 * "step out" tiles beside any adjacent full cover, on the same level.
 */
export function peekOrigins(s: GameState, p: Pos): Pos[] {
  const origins: Pos[] = [p];
  const h = heightAt(s, p);
  for (const d of DIRS4) {
    if (coverAt(s, p, { x: p.x + d.x, y: p.y + d.y }) !== 2) continue;
    for (const side of [{ x: d.y, y: -d.x }, { x: -d.y, y: d.x }]) {
      const q = { x: p.x + side.x, y: p.y + side.y, z: p.z };
      if (canStopAt(s, q) && heightAt(s, q) === h && !origins.some((o) => samePos(o, q))) origins.push(q);
    }
  }
  return origins;
}

/** Origin tile a viewer at `from` uses to see `to`, or null if there is no line of sight. */
export function sightOrigin(s: GameState, from: Pos, to: Pos, range: number): Pos | null {
  if (distance(from, to) > range) return null;
  const targets = peekOrigins(s, to);
  for (const o of peekOrigins(s, from)) {
    for (const t of targets) {
      if (lineClear(s, o, t)) return o;
    }
  }
  return null;
}

/** Tiles a unit sees by day: its kind's, plus Ojos del viento. */
export function sightOf(u: Unit): number {
  return TEMPLATES[u.template].sight + (u.mods?.sight ?? 0);
}

// ------------------------------------------------------------------ night

/**
 * The places (nodes, grid.ts nodeOf; on the ground y * width + x) a light on
 * `tile`, `height` storeys above its floor, reaches: within `radius`, and in
 * its line of sight (walls, roofs and floors keep it out). Worked out once,
 * when the light goes on.
 */
export function lightTiles(s: GameState, tile: Pos, radius: number, height: number): number[] {
  const out: number[] = [];
  const r = Math.ceil(radius);
  const from = heightAt(s, tile) + height;
  for (let y = tile.y - r; y <= tile.y + r; y++) {
    for (let x = tile.x - r; x <= tile.x + r; x++) {
      if (!inBounds(s, x, y) || distance(tile, { x, y }) > radius) continue;
      // Light gets out through a window from anywhere in the room.
      for (const p of placesOn(s, x, y)) if (samePos(p, tile) || lineClear(s, tile, p, from, undefined, Infinity)) out.push(nodeOf(s, p));
    }
  }
  return out;
}

/** Whether `p` is lit (GameState.lights): at night, whoever stands there is seen from as far as by day. */
export function isLit(s: GameState, p: Place): boolean {
  const i = nodeOf(s, toPos(p));
  return (s.lights ?? []).some((l) => distance(l.tile, p) <= l.radius && (l.tiles?.includes(i) ?? true));
}

/** Every lit place, by node (on the ground, y * width + x), for looking at many places at once. */
export function litMask(s: GameState): boolean[] {
  const mask = new Array<boolean>(s.width * s.height * levelCount(s)).fill(false);
  for (const l of s.lights ?? []) {
    if (l.tiles) {
      for (const i of l.tiles) mask[i] = true;
      continue;
    }
    // A light saved before its tiles were worked out: its whole circle.
    const r = Math.ceil(l.radius);
    for (let y = Math.max(0, l.tile.y - r); y <= Math.min(s.height - 1, l.tile.y + r); y++) {
      for (let x = Math.max(0, l.tile.x - r); x <= Math.min(s.width - 1, l.tile.x + r); x++) {
        if (distance(l.tile, { x, y }) <= l.radius) mask[y * s.width + x] = true;
      }
    }
  }
  return mask;
}

/** How far the living see in the dark (NIGHT in content.ts); the dead see as ever. */
export function nightSight(u: Unit): number {
  const full = sightOf(u);
  return TEMPLATES[u.template].undead ? full : Math.min(full, Math.max(NIGHT.minSight, full - NIGHT.sightCut));
}

/** How far `viewer` sees something on `p`: its sight by day, or at night unless `p` is lit (`lit`: a litMask, to spare work). */
export function sightTo(s: GameState, viewer: Unit, p: Place, lit?: readonly boolean[]): number {
  if (!s.mission?.night) return sightOf(viewer);
  return (lit ? lit[nodeOf(s, toPos(p))] : isLit(s, p)) ? sightOf(viewer) : nightSight(viewer);
}

/** How close a soldier on `p` must be for `viewer` to notice it while the squad is hidden: halved at night, unless lit. */
export function detectionTo(s: GameState, viewer: Unit, p: Place): number {
  const range = TEMPLATES[viewer.template].detection;
  return s.mission?.night && !isLit(s, p) ? range * NIGHT.detection : range;
}

export function canSee(s: GameState, viewer: Unit, target: Unit, viewerPos: Pos = viewer.pos): boolean {
  return inViewCone(viewer, target.pos, viewerPos) && sightOrigin(s, viewerPos, target.pos, sightTo(s, viewer, target.pos)) !== null;
}

/** How wide the enemies see, in degrees round where they face (Arturo, 17/10: «en cono y no en área»; the living too, 18/10 #46). */
export const VIEW_CONE = 120;
/** Whoever is this close, a dead one notices whatever way it faces. */
export const AWARE_RANGE = 1.5;

/**
 * Whether `p` lies in `viewer`'s field of view from `from`: the squad looks
 * all round; the enemies, dead or living («tienen ojos igual», Arturo 18/10
 * #46), only ahead of them, in VIEW_CONE, or right beside them. `facing`
 * overrides the unit's own (a patrol on its way, before its move is written
 * down). Without a facing, all round.
 */
export function inViewCone(viewer: Unit, p: Place, from: Pos = viewer.pos, facing = viewer.facing): boolean {
  if (viewer.team === 'xcom' || facing === undefined) return true;
  const dx = p.x - from.x;
  const dy = p.y - from.y;
  if (Math.hypot(dx, dy) <= AWARE_RANGE) return true;
  const off = Math.abs(((Math.atan2(dy, dx) - facing + 3 * Math.PI) % (2 * Math.PI)) - Math.PI);
  return off <= (VIEW_CONE / 2) * (Math.PI / 180);
}

/** The way from `a` to `b`, as a facing (radians); null when they are the same tile. */
export function facingTo(a: Place, b: Place): number | null {
  return a.x === b.x && a.y === b.y ? null : Math.atan2(b.y - a.y, b.x - a.x);
}

/** Whether a viewer standing at `from` can see the tile `to` (no peeks on the target side). */
export function canSeeTile(s: GameState, from: Pos, to: Pos, range: number): boolean {
  if (distance(from, to) > range) return false;
  return peekOrigins(s, from).some((o) => lineClear(s, o, to));
}

/** Units that give their team vision: alive, not the relay, not a captive VIP. */
export function isViewer(u: Unit, team: Unit['team']): boolean {
  return u.alive && u.team === team && !u.objective && !u.captive;
}

/**
 * How far the squad sees the ground, past where it sees enemies (Arturo, 21/10: «que se vea hasta el final… y
 * luego un cono más pequeño de largo donde se detecten enemigos»; 22/10: a long street, to its end, to the map's
 * edge): across the whole map, teamView.
 */
export function viewReach(s: GameState): number {
  return Math.hypot(s.width, s.height);
}

/**
 * Places in the line of sight of any living unit of `team`, as far as the map goes, day or night (Arturo, 21/10
 * and 22/10): the ground the player sees, enemies or not. Enemies only show within teamVisibility.
 */
export function teamView(s: GameState, team: Unit['team']): boolean[] {
  // Across the whole map it costs some 10 ms, and it changes only as the viewers move or the map does (a door, a
  // wall blown through): the last one is kept for whatever else changes (the dead on their turn).
  const viewers = s.units.filter((u) => isViewer(u, team)).map((u) => `${u.id}@${u.pos.x},${u.pos.y},${u.pos.z ?? 0}`);
  const key = `${team}|${s.width}x${s.height}|${mapStamp(s)}|${viewers.join(';')}`;
  if (lastView?.key !== key) lastView = { key, mask: teamVisibility(s, team, viewReach(s)) };
  return lastView.mask.slice();
}

/** The last teamView worked out and what from (teamView). */
let lastView: { key: string; mask: boolean[] } | null = null;

/** A hash of what stops sight on the map: its tiles on every level and their heights. */
function mapStamp(s: GameState): number {
  let h = 0;
  const add = (k: TileKind | null) => {
    h = (Math.imul(h, 31) + (k === null ? 1 : k.charCodeAt(0) * 7 + k.length)) | 0;
  };
  for (const k of s.tiles) add(k);
  for (const level of s.levels ?? []) for (const k of level.tiles) add(k);
  for (const e of s.elev) h = (Math.imul(h, 31) + e) | 0;
  return h;
}

/**
 * Places visible to any living unit of `team`, as a boolean mask by node
 * (grid.ts nodeOf: the ground first, y * width + x, then each storey, where
 * only its slabs are worked out). At night, the dark ones only from close by.
 * On a map with storeys, the same lines as lineClear, walked on a SightGrid.
 * With `reach`, every viewer looks that far instead, and the dark cuts nothing
 * (teamView).
 */
export function teamVisibility(s: GameState, team: Unit['team'], reach?: number): boolean[] {
  const area = s.width * s.height;
  const levels = levelCount(s);
  const mask = new Array<boolean>(area * levels).fill(false);
  const lit = s.mission?.night && reach === undefined ? litMask(s) : null;
  // Worked out on every call: it is cheap next to the walks, and the state changes under it.
  const v: Viewing | null = s.levels ? { g: sightGrid(s), w: s.width, area, n: levels - 1, elev: s.elev, mask, lit, ox: [], oy: [], eyes: [], blocked: [], dark: 0 } : null;
  // The viewers first, each with its origins and the square it looks over. A line found blocked is remembered
  // only for an origin that more than one viewer looks from (a viewer looks at each place once), and only over
  // their squares: no map-sized buffer per origin (docs/MAPAS.md §7.3).
  const looks: { u: Unit; range: number; origins: Pos[]; eyes: number[]; keys: string[]; minX: number; maxX: number; minY: number; maxY: number }[] = [];
  /** By origin and eye height, how many viewers look from it, over what, and (shared) what was found blocked from it. */
  const tried = new Map<string, { viewers: number; x0: number; y0: number; x1: number; y1: number; memo: Blocked | null }>();
  for (const u of s.units) {
    if (!isViewer(u, team)) continue;
    const range = reach ?? sightOf(u);
    const origins = peekOrigins(s, u.pos);
    const eyes = origins.map((o) => heightAt(s, o) + EYE_HEIGHT);
    const look = {
      u, range, origins, eyes, keys: origins.map((o, k) => `${o.x},${o.y},${o.z},${eyes[k]}`),
      minX: Math.max(0, Math.floor(u.pos.x - range)), maxX: Math.min(s.width - 1, Math.ceil(u.pos.x + range)),
      minY: Math.max(0, Math.floor(u.pos.y - range)), maxY: Math.min(s.height - 1, Math.ceil(u.pos.y + range)),
    };
    looks.push(look);
    if (!v) continue;
    for (const key of look.keys) {
      const t = tried.get(key);
      if (!t) tried.set(key, { viewers: 1, x0: look.minX, y0: look.minY, x1: look.maxX, y1: look.maxY, memo: null });
      else {
        t.viewers++;
        t.x0 = Math.min(t.x0, look.minX);
        t.y0 = Math.min(t.y0, look.minY);
        t.x1 = Math.max(t.x1, look.maxX);
        t.y1 = Math.max(t.y1, look.maxY);
      }
    }
  }
  for (const { u, range, origins, eyes, keys, minX, maxX, minY, maxY } of looks) {
    const dark = lit ? nightSight(u) : range;
    if (v) {
      v.dark = dark;
      v.ox = origins.map((o) => o.x);
      v.oy = origins.map((o) => o.y);
      v.eyes = eyes;
      v.blocked = keys.map((key) => {
        const t = tried.get(key)!;
        if (t.viewers < 2) return null;
        const w = t.x1 - t.x0 + 1;
        const h = t.y1 - t.y0 + 1;
        return (t.memo ??= { x0: t.x0, y0: t.y0, w, h, data: new Uint8Array(w * h * levels) });
      });
    }
    for (let y = minY; y <= maxY; y++) {
      for (let x = minX; x <= maxX; x++) {
        // distance(u.pos, { x, y }), without the object.
        const d = hypotOf(u.pos.x - x, u.pos.y - y);
        if (d > range) continue;
        if (v) {
          seeTile(v, x, y, d);
          continue;
        }
        // Without storeys, the ground only.
        const i = y * s.width + x;
        if (mask[i] || (d > dark && !lit?.[i])) continue;
        const t = { x, y, z: 0 };
        const body = (s.elev[i] ?? 0) + BODY_HEIGHT;
        if (origins.some((o, k) => lineClear(s, o, t, eyes[k], body))) mask[i] = true;
      }
    }
  }
  return mask;
}

/** Math.hypot of whole offsets up to HYPOT_SIDE - 1 a side, by |dy| * HYPOT_SIDE + |dx|: teamVisibility asks at every tile of a square. */
const HYPOT_SIDE = 64;
let hypots: Float64Array | null = null;

/** Math.hypot(dx, dy), the same value, from the table for whole offsets. */
function hypotOf(dx: number, dy: number): number {
  const ax = Math.abs(dx);
  const ay = Math.abs(dy);
  if (ax >= HYPOT_SIDE || ay >= HYPOT_SIDE || (ax | 0) !== ax || (ay | 0) !== ay) return Math.hypot(dx, dy);
  if (!hypots) {
    hypots = new Float64Array(HYPOT_SIDE * HYPOT_SIDE);
    for (let y = 0; y < HYPOT_SIDE; y++) for (let x = 0; x < HYPOT_SIDE; x++) hypots[y * HYPOT_SIDE + x] = Math.hypot(x, y);
  }
  return hypots[ay * HYPOT_SIDE + ax]!;
}

/**
 * What lineThroughStoreys reads, laid out flat for the fog: by tile, the top
 * of the ground's opaque column (elevation plus what stands there); by storey
 * and tile, the top of what stands on its slab (-Infinity without a slab), and
 * whether there is a slab.
 */
interface SightGrid {
  ground: Float64Array;
  top: Float64Array;
  slab: Uint8Array;
  /** By level (the ground first) and tile, a window there (MAX_WINDOWS_SEEN_THROUGH). */
  window: Uint8Array;
  /** By tile, the ground's elevation: its windows count below one storey over it. */
  elev: Float64Array;
}

/** The last SightGrid made, its buffers filled again for the next map of the same size (teamVisibility runs on every change). */
let lastGrid: { area: number; n: number; g: SightGrid } | null = null;

function sightGrid(s: GameState): SightGrid {
  const area = s.width * s.height;
  const n = s.levels?.length ?? 0;
  if (lastGrid?.area !== area || lastGrid.n !== n) {
    lastGrid = { area, n, g: { ground: new Float64Array(area), top: new Float64Array(area * n), slab: new Uint8Array(area * n), window: new Uint8Array(area * (n + 1)), elev: new Float64Array(area) } };
  }
  const { ground, window, elev, top, slab } = lastGrid.g;
  window.fill(0);
  for (let i = 0; i < area; i++) {
    elev[i] = s.elev[i] ?? 0;
    ground[i] = elev[i]! + groundSightHeight(s, i);
    if (isWindow(s.tiles[i]!)) window[i] = 1;
  }
  top.fill(-Infinity);
  slab.fill(0);
  for (let k = 1; k <= n; k++) {
    const tiles = s.levels![k - 1]!.tiles;
    for (let i = 0; i < area; i++) {
      const kind = tiles[i] ?? null;
      if (kind === null) continue;
      top[(k - 1) * area + i] = k + obstacleHeight(kind);
      slab[(k - 1) * area + i] = 1;
      if (isWindow(kind)) window[k * area + i] = 1;
    }
  }
  return lastGrid.g;
}

/** opaqueAtIndex on a SightGrid (`n` storeys of `area` tiles). */
function gridOpaque(ground: Float64Array, top: Float64Array, area: number, n: number, i: number, h: number): boolean {
  if (ground[i]! > h + 1e-6) return true;
  const k = Math.floor(h + 1e-6);
  if (k < 1 || k > n) return false;
  return top[(k - 1) * area + i]! > h + 1e-6;
}

/** lineThroughStoreys from tile (ax, ay) to (bx, by) on a SightGrid, step for step. */
function gridLine(g: SightGrid, w: number, area: number, n: number, ax: number, ay: number, bx: number, by: number, ha: number, hb: number): boolean {
  const { ground, top, slab, window, elev } = g;
  if (ax === bx && ay === by) {
    const lo = Math.min(ha, hb);
    const hi = Math.max(ha, hb);
    for (let k = Math.floor(lo + 1e-6) + 1; k < hi - 1e-6; k++) if (k >= 1 && k <= n && slab[(k - 1) * area + ay * w + ax]) return false;
    return true;
  }
  const dx = bx - ax;
  const dy = by - ay;
  const nx = Math.abs(dx);
  const ny = Math.abs(dy);
  const sx = Math.sign(dx);
  const sy = Math.sign(dy);
  const len2 = dx * dx + dy * dy;
  const rise = hb - ha;
  let pi = ay * w + ax;
  let ph = ha;
  let x = ax;
  let y = ay;
  let ix = 0;
  let iy = 0;
  let windows = 0;
  while (ix < nx || iy < ny) {
    const decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
    if (decision === 0) {
      const ox = x - ax;
      const oy = y - ay;
      if (
        gridOpaque(ground, top, area, n, y * w + x + sx, ha + rise * Math.min(1, Math.max(0, ((ox + sx) * dx + oy * dy) / len2))) &&
        gridOpaque(ground, top, area, n, (y + sy) * w + x, ha + rise * Math.min(1, Math.max(0, (ox * dx + (oy + sy) * dy) / len2)))
      )
        return false;
      x += sx;
      y += sy;
      ix++;
      iy++;
    } else if (decision < 0) {
      x += sx;
      ix++;
    } else {
      y += sy;
      iy++;
    }
    const i = y * w + x;
    const end = x === bx && y === by;
    const h = end ? hb : ha + rise * Math.min(1, Math.max(0, ((x - ax) * dx + (y - ay) * dy) / len2));
    if (h !== ph) {
      const lo = Math.min(ph, h);
      const hi = Math.max(ph, h);
      for (let k = Math.floor(lo + 1e-6) + 1; k < hi - 1e-6; k++) {
        const j = (k - ph) / (h - ph) < 0.5 ? pi : i;
        if (k <= n && slab[(k - 1) * area + j]) return false;
      }
    }
    if (end) return true;
    if (gridOpaque(ground, top, area, n, i, h)) return false;
    const k = h < elev[i]! + 1 ? 0 : Math.floor(h + 1e-6);
    // farFromWindow, written out.
    if (
      k <= n &&
      window[k * area + i] &&
      ((Math.max(Math.abs(x - ax), Math.abs(y - ay)) > WINDOW_SIGHT_REACH && Math.max(Math.abs(x - bx), Math.abs(y - by)) > WINDOW_SIGHT_REACH) || ++windows > MAX_WINDOWS_SEEN_THROUGH)
    )
      return false;
    pi = i;
    ph = h;
  }
  return true;
}

/** One viewer of teamVisibility on a map with storeys: its origins, their eyes, and the lines already found blocked from each. */
interface Viewing {
  g: SightGrid;
  w: number;
  area: number;
  n: number;
  elev: readonly number[];
  mask: boolean[];
  lit: readonly boolean[] | null;
  ox: number[];
  oy: number[];
  eyes: number[];
  /** By origin, what was found blocked from it (null: no other viewer looks from there). */
  blocked: (Blocked | null)[];
  dark: number;
}

/** The places of a square (all levels) that lines from one origin were found not to reach. */
interface Blocked {
  x0: number;
  y0: number;
  w: number;
  h: number;
  /** By level, row and column of the square. */
  data: Uint8Array;
}

/** teamVisibility's look at tile (x, y), `d` away: the ground, then each storey with a slab here. */
function seeTile(v: Viewing, x: number, y: number, d: number): void {
  const area = v.area;
  const i = y * v.w + x;
  for (let z = 0; z <= v.n; z++) {
    if (z > 0 && !v.g.slab[(z - 1) * area + i]) continue;
    const node = z * area + i;
    if (v.mask[node] || (d > v.dark && !(v.lit !== null && v.lit[node]))) continue;
    const body = (z === 0 ? (v.elev[i] ?? 0) : z) + BODY_HEIGHT;
    for (let k = 0; k < v.eyes.length; k++) {
      const blocked = v.blocked[k]!;
      const at = blocked ? (z * blocked.h + y - blocked.y0) * blocked.w + x - blocked.x0 : 0;
      if (blocked && blocked.data[at]) continue;
      if (gridLine(v.g, v.w, area, v.n, v.ox[k]!, v.oy[k]!, x, y, v.eyes[k]!, body)) {
        v.mask[node] = true;
        break;
      }
      if (blocked) blocked.data[at] = 1;
    }
  }
}

/**
 * One floor of the cutaway (the floors above it hidden), tile by tile: the
 * level the camera looks down on, the highest at or below `floor` with a slab
 * there — or else the ground.
 */
export function floorLevels(s: GameState, floor: number): number[] {
  const area = s.width * s.height;
  const out = new Array<number>(area);
  const top = Math.min(floor, levelCount(s) - 1);
  for (let i = 0; i < area; i++) {
    let z = top;
    while (z > 0 && (s.levels![z - 1]!.tiles[i] ?? null) === null) z--;
    out[i] = z;
  }
  return out;
}

/**
 * A mask by node (teamVisibility, litMask, the client's explored places) for
 * one floor of the cutaway, tile by tile: on each tile, its value on the level
 * floorLevels shows there.
 */
export function floorView(s: GameState, mask: readonly boolean[], floor: number): boolean[] {
  const area = s.width * s.height;
  return floorLevels(s, floor).map((z, i) => !!mask[z * area + i]);
}

/** Enemies of `team` that at least one living member of `team` can see. */
export function visibleEnemies(s: GameState, team: Unit['team']): Unit[] {
  const viewers = s.units.filter((u) => isViewer(u, team));
  return s.units.filter((t) => t.alive && t.team !== team && viewers.some((v) => canSee(s, v, t)));
}
