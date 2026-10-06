import { COVER_MAX_ANGLE_DEG } from './content';
import { coverFrom, DIRS4, elevAt } from './grid';
import type { CoverLevel, GameState, Vec2 } from './types';

const COVER_MIN_DOT = Math.cos((COVER_MAX_ANGLE_DEG * Math.PI) / 180);

/**
 * Cover offered on each side of tile `p`, in N, E, S, W order. Heights count:
 * a rooftop edge gives nothing to whoever stands on the roof, while the wall of
 * a raised block is full cover for whoever stands at its foot.
 */
export function coverSides(s: GameState, p: Vec2): CoverLevel[] {
  const elev = elevAt(s, p.x, p.y);
  return DIRS4.map((d) => coverFrom(s, elev, { x: p.x + d.x, y: p.y + d.y }));
}

export function hasAnyCover(s: GameState, p: Vec2): boolean {
  return coverSides(s, p).some((c) => c > 0);
}

/**
 * Best cover a unit at `target` gets against an attacker at `attacker`.
 * 0 means the target is flanked.
 */
export function coverAgainst(s: GameState, target: Vec2, attacker: Vec2): CoverLevel {
  const vx = attacker.x - target.x;
  const vy = attacker.y - target.y;
  const len = Math.hypot(vx, vy);
  if (len === 0) return 0;
  let best: CoverLevel = 0;
  coverSides(s, target).forEach((level, i) => {
    if (level <= best) return;
    const d = DIRS4[i]!;
    if ((vx * d.x + vy * d.y) / len >= COVER_MIN_DOT) best = level;
  });
  return best;
}
