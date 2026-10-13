import { NIGHT, TEMPLATES } from './content';
import { blocksSightAt, BODY_HEIGHT, canStandOn, coverFrom, DIRS4, distance, elevAt, EYE_HEIGHT, inBounds, roofSide, sameTile, tileAt, tileIndex } from './grid';
import type { GameState, Unit, Vec2 } from './types';

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
export function lineClear(s: GameState, a: Vec2, b: Vec2, ha = elevAt(s, a.x, a.y) + EYE_HEIGHT, hb = elevAt(s, b.x, b.y) + BODY_HEIGHT): boolean {
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
 * Tiles a unit at `p` can look/shoot from: its own tile plus the XCOM-style
 * "step out" tiles beside any adjacent full cover, on the same level.
 */
export function peekOrigins(s: GameState, p: Vec2): Vec2[] {
  const origins: Vec2[] = [p];
  const elev = elevAt(s, p.x, p.y);
  for (const d of DIRS4) {
    if (coverFrom(s, elev, { x: p.x + d.x, y: p.y + d.y }) !== 2) continue;
    for (const side of [{ x: d.y, y: -d.x }, { x: -d.y, y: d.x }]) {
      const q = { x: p.x + side.x, y: p.y + side.y };
      if (canStandOn(tileAt(s, q.x, q.y)) && elevAt(s, q.x, q.y) === elev && !origins.some((o) => sameTile(o, q))) origins.push(q);
    }
  }
  return origins;
}

/** Origin tile a viewer at `from` uses to see `to`, or null if there is no line of sight. */
export function sightOrigin(s: GameState, from: Vec2, to: Vec2, range: number): Vec2 | null {
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
 * The tiles (index = y * width + x) a light on `tile`, `height` storeys above
 * its floor, reaches: within `radius`, and in its line of sight (walls and
 * roofs keep it out). Worked out once, when the light goes on.
 */
export function lightTiles(s: GameState, tile: Vec2, radius: number, height: number): number[] {
  const out: number[] = [];
  const r = Math.ceil(radius);
  const from = elevAt(s, tile.x, tile.y) + height;
  for (let y = tile.y - r; y <= tile.y + r; y++) {
    for (let x = tile.x - r; x <= tile.x + r; x++) {
      if (!inBounds(s, x, y) || distance(tile, { x, y }) > radius) continue;
      if ((x === tile.x && y === tile.y) || lineClear(s, tile, { x, y }, from)) out.push(tileIndex(s, x, y));
    }
  }
  return out;
}

/** Whether `p` is lit (GameState.lights): at night, whoever stands there is seen from as far as by day. */
export function isLit(s: GameState, p: Vec2): boolean {
  const i = p.y * s.width + p.x;
  return (s.lights ?? []).some((l) => distance(l.tile, p) <= l.radius && (l.tiles?.includes(i) ?? true));
}

/** Every lit tile (index = y * width + x), for looking at many tiles at once. */
export function litMask(s: GameState): boolean[] {
  const mask = new Array<boolean>(s.width * s.height).fill(false);
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
export function sightTo(s: GameState, viewer: Unit, p: Vec2, lit?: readonly boolean[]): number {
  if (!s.mission?.night) return sightOf(viewer);
  return (lit ? lit[p.y * s.width + p.x] : isLit(s, p)) ? sightOf(viewer) : nightSight(viewer);
}

/** How close a soldier on `p` must be for `viewer` to notice it while the squad is hidden: halved at night, unless lit. */
export function detectionTo(s: GameState, viewer: Unit, p: Vec2): number {
  const range = TEMPLATES[viewer.template].detection;
  return s.mission?.night && !isLit(s, p) ? range * NIGHT.detection : range;
}

export function canSee(s: GameState, viewer: Unit, target: Unit, viewerPos: Vec2 = viewer.pos): boolean {
  return sightOrigin(s, viewerPos, target.pos, sightTo(s, viewer, target.pos)) !== null;
}

/** Whether a viewer standing at `from` can see the tile `to` (no peeks on the target side). */
export function canSeeTile(s: GameState, from: Vec2, to: Vec2, range: number): boolean {
  if (distance(from, to) > range) return false;
  return peekOrigins(s, from).some((o) => lineClear(s, o, to));
}

/** Units that give their team vision: alive, not the relay, not a captive VIP. */
export function isViewer(u: Unit, team: Unit['team']): boolean {
  return u.alive && u.team === team && !u.objective && !u.captive;
}

/** Tiles visible to any living unit of `team`, as a boolean mask (index = y * width + x). At night, the dark ones only from close by. */
export function teamVisibility(s: GameState, team: Unit['team']): boolean[] {
  const mask = new Array<boolean>(s.width * s.height).fill(false);
  const viewers = s.units.filter((u) => isViewer(u, team));
  const lit = s.mission?.night ? litMask(s) : null;
  for (const u of viewers) {
    const range = sightOf(u);
    const dark = lit ? nightSight(u) : range;
    const origins = peekOrigins(s, u.pos);
    const minX = Math.max(0, Math.floor(u.pos.x - range));
    const maxX = Math.min(s.width - 1, Math.ceil(u.pos.x + range));
    const minY = Math.max(0, Math.floor(u.pos.y - range));
    const maxY = Math.min(s.height - 1, Math.ceil(u.pos.y + range));
    for (let y = minY; y <= maxY; y++) {
      for (let x = minX; x <= maxX; x++) {
        const i = y * s.width + x;
        if (mask[i]) continue;
        const t = { x, y };
        const d = distance(u.pos, t);
        if (d > range || (d > dark && !lit?.[i])) continue;
        if (origins.some((o) => lineClear(s, o, t))) mask[i] = true;
      }
    }
  }
  return mask;
}

/** Enemies of `team` that at least one living member of `team` can see. */
export function visibleEnemies(s: GameState, team: Unit['team']): Unit[] {
  const viewers = s.units.filter((u) => isViewer(u, team));
  return s.units.filter((t) => t.alive && t.team !== team && viewers.some((v) => canSee(s, v, t)));
}
