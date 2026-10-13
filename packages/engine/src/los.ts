import { NIGHT, TEMPLATES } from './content';
import {
  blocksSightAt,
  BODY_HEIGHT,
  canStandOn,
  coverAt,
  DIRS4,
  distance,
  EYE_HEIGHT,
  heightAt,
  inBounds,
  kindAt,
  levelCount,
  nodeOf,
  opaqueAtIndex,
  placesOn,
  roofSide,
  samePos,
  slabAt,
  tileIndex,
  toPos,
} from './grid';
import type { GameState, Place, Pos, Unit, Vec2 } from './types';

/**
 * Whether a line from `a`, `ha` storeys up, to `b`, `hb` up (by default a
 * unit's eyes and the body of whoever stands there, on their levels), sees
 * through: on a map with storeys, through floors and ceilings only where they
 * have a hole (lineThroughStoreys); without them, as it always did (lineFlat).
 */
export function lineClear(s: GameState, a: Place, b: Place, ha = heightAt(s, toPos(a)) + EYE_HEIGHT, hb = heightAt(s, toPos(b)) + BODY_HEIGHT): boolean {
  return s.levels ? lineThroughStoreys(s, a, b, ha, hb) : lineFlat(s, a, b, ha, hb);
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
function lineFlat(s: GameState, a: Vec2, b: Vec2, ha: number, hb: number): boolean {
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
  }
  return true;
}

/**
 * The same walk on a map with storeys (docs/FASE6-PLAN.md), heights taken the
 * same way. On each tile the line crosses, what stands there at its height
 * stops it (opaqueAt: the ground's objects; a storey's walls and closed doors,
 * not its windows). Between two tiles, a floor or ceiling it passes through
 * stops it: the slab of the tile where it crosses that height, the one it
 * leaves or the one it enters. So does one between the two heights when the
 * line goes straight up or down a single tile. Start and end tiles never
 * block, but their slabs count; corners as lineFlat.
 */
function lineThroughStoreys(s: GameState, a: Vec2, b: Vec2, ha: number, hb: number): boolean {
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
  /** Height of the line where it crosses the tile `(ox, oy)` away from `a`. */
  const at = (ox: number, oy: number) => ha + rise * Math.min(1, Math.max(0, (ox * dx + oy * dy) / len2));
  let pi = a.y * w + a.x;
  let ph = ha;
  let x = a.x;
  let y = a.y;
  let ix = 0;
  let iy = 0;
  while (ix < nx || iy < ny) {
    const decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
    if (decision === 0) {
      if (opaqueAtIndex(s, y * w + x + sx, at(x + sx - a.x, y - a.y)) && opaqueAtIndex(s, (y + sy) * w + x, at(x - a.x, y + sy - a.y))) return false;
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
    const h = end ? hb : at(x - a.x, y - a.y);
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
    pi = i;
    ph = h;
  }
  return true;
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
      if (canStandOn(kindAt(s, q)) && heightAt(s, q) === h && !origins.some((o) => samePos(o, q))) origins.push(q);
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
      for (const p of placesOn(s, x, y)) if (samePos(p, tile) || lineClear(s, tile, p, from)) out.push(nodeOf(s, p));
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
  return sightOrigin(s, viewerPos, target.pos, sightTo(s, viewer, target.pos)) !== null;
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
 * Places visible to any living unit of `team`, as a boolean mask by node
 * (grid.ts nodeOf: the ground first, y * width + x, then each storey, where
 * only its slabs are worked out). At night, the dark ones only from close by.
 */
export function teamVisibility(s: GameState, team: Unit['team']): boolean[] {
  const area = s.width * s.height;
  const levels = levelCount(s);
  const mask = new Array<boolean>(area * levels).fill(false);
  const viewers = s.units.filter((u) => isViewer(u, team));
  const lit = s.mission?.night ? litMask(s) : null;
  for (const u of viewers) {
    const range = sightOf(u);
    const dark = lit ? nightSight(u) : range;
    const origins = peekOrigins(s, u.pos);
    const eyes = origins.map((o) => heightAt(s, o) + EYE_HEIGHT);
    const minX = Math.max(0, Math.floor(u.pos.x - range));
    const maxX = Math.min(s.width - 1, Math.ceil(u.pos.x + range));
    const minY = Math.max(0, Math.floor(u.pos.y - range));
    const maxY = Math.min(s.height - 1, Math.ceil(u.pos.y + range));
    for (let y = minY; y <= maxY; y++) {
      for (let x = minX; x <= maxX; x++) {
        const d = distance(u.pos, { x, y });
        if (d > range) continue;
        const i = y * s.width + x;
        // The ground, then each storey with a slab here (only those are worked out).
        for (let z = 0; z < levels; z++) {
          if (z > 0 && (s.levels![z - 1]!.tiles[i] ?? null) === null) continue;
          const node = z * area + i;
          if (mask[node] || (d > dark && !lit?.[node])) continue;
          const t = { x, y, z };
          const body = (z === 0 ? (s.elev[i] ?? 0) : z) + BODY_HEIGHT;
          if (origins.some((o, k) => lineClear(s, o, t, eyes[k], body))) mask[node] = true;
        }
      }
    }
  }
  return mask;
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
