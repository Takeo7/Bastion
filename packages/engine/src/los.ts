import { TEMPLATES } from './content';
import { BODY_HEIGHT, canStandOn, coverFrom, DIRS4, distance, elevAt, EYE_HEIGHT, sameTile, sightTop, tileAt } from './grid';
import type { GameState, Unit, Vec2 } from './types';

/**
 * Grid walk between tile centres in 3D: the line goes from height `ha` above
 * tile `a` to height `hb` above tile `b` (absolute storeys) and is blocked by
 * any tile whose opaque column rises above it. Start and end tiles never block.
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
  const blocks = (x: number, y: number) => sightTop(s, x, y) > lineAt(x, y) + 1e-6;
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
    if (x === b.x && y === b.y) return true;
    if (blocks(x, y)) return false;
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

export function canSee(s: GameState, viewer: Unit, target: Unit, viewerPos: Vec2 = viewer.pos): boolean {
  return sightOrigin(s, viewerPos, target.pos, TEMPLATES[viewer.template].sight) !== null;
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

/** Tiles visible to any living unit of `team`, as a boolean mask (index = y * width + x). */
export function teamVisibility(s: GameState, team: Unit['team']): boolean[] {
  const mask = new Array<boolean>(s.width * s.height).fill(false);
  const viewers = s.units.filter((u) => isViewer(u, team));
  for (const u of viewers) {
    const range = TEMPLATES[u.template].sight;
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
        if (distance(u.pos, t) > range) continue;
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
