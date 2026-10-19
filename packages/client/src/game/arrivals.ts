import * as THREE from 'three';
import { COST_ORTHO, computeReach, nodeOf, posOf, stepBetween, type GameState, type Pos, type Unit, type Vec2 } from '@bastion/engine';
import { tileToWorld } from './coords';
import type { Fx } from './fx';
import type { Sfx } from './sfx';
import { DEAD_TEMPLATES } from './syntyManifest';
import { wait } from './tween';
import type { UnitViews } from './unitViews';
import { planWalks, type Walker } from './walkPlan';

// Reinforcements and the horde arriving (instead of drop pods): the dead
// stagger in from the edge of the map with a moan and dust, raiders run in, a
// pickup's headlights flaring at the edge behind them. Each comes in along a
// real way from the nearest tile of the edge it can walk in by (path.ts, as
// the rules move them), round what stands in the way, not through it; those
// going deepest set off first and the rest after them, none through another
// (walkPlan.ts). Whoever has no such way in (far from any edge, or walled off
// from it) gets up out of a cloud of dust where they are.

/** Tiles in from an edge that a unit still walks in from it (its way's cost, a straight one). */
const EDGE_WALK = 7;
/** Seconds per tile: the dead stagger, the living run (as a move). */
const PACE = { dead: 0.42, living: 0.16 };
/** No arrival walk lasts longer than this (waiting for those before it aside), so the turn keeps moving. */
const LONGEST = 2.2;
/** A diagonal step takes this much longer than a straight one. */
const DIAGONAL = 1.4;

/** One arrival's way in: from the edge of the map (just outside its tile) through each tile to its own. */
interface WayIn {
  u: Unit;
  zombie: boolean;
  /** The tiles, the edge's first, the unit's own last. */
  tiles: Pos[];
  /** Out of the map from the edge tile. */
  out: Vec2;
}

/** Plays the arrival of `arrived` (units the squad sees, not yet in the state); resolves when they all stand on their tiles. */
export async function playArrival(units: UnitViews, fx: Fx, sfx: Sfx, s: GameState, arrived: Unit[]): Promise<void> {
  if (!arrived.length) return;
  const dead = arrived.filter((u) => DEAD_TEMPLATES.has(u.template)).length;
  if (dead) sfx.moan(tileToWorld(arrived[0]!.pos), dead);
  let engine = false;
  const ways: WayIn[] = [];
  const risers: Unit[] = [];
  for (const u of arrived) {
    const way = wayIn(s, u);
    if (way) ways.push(way);
    else risers.push(u);
  }
  // The deepest in first, so that nobody is left standing in the way of those still to come by them.
  ways.sort((a, b) => b.tiles.length - a.tiles.length);
  const walkers: Walker[] = ways.map((w) => {
    const pace = (w.zombie ? PACE.dead : PACE.living) * Math.min(1, LONGEST / natural(w));
    const steps = [0.5 * pace];
    for (let k = 1; k < w.tiles.length; k++) steps.push(pace * (w.tiles[k]!.x !== w.tiles[k - 1]!.x && w.tiles[k]!.y !== w.tiles[k - 1]!.y ? DIAGONAL : 1));
    // Just outside its edge tile: one key for all those coming in by it (they don't hold each other up there).
    const outside = -1 - nodeOf(s, w.tiles[0]!);
    const corners = w.tiles.map((t, k) => {
      const next = w.tiles[k + 1];
      if (!next || next.x === t.x || next.y === t.y) return null;
      return [nodeOf(s, { x: next.x, y: t.y, z: t.z }), nodeOf(s, { x: t.x, y: next.y, z: t.z })] as const;
    });
    return { tiles: [outside, ...w.tiles.map((t) => nodeOf(s, t))], steps, corners: [null, ...corners] };
  });
  const plans = planWalks(walkers);
  await Promise.all([
    ...ways.map(async (w, i) => {
      const plan = plans[i]!.waits;
      await wait(plan[0] ?? 0);
      const edge = tileToWorld(w.tiles[0]!);
      const out = new THREE.Vector3(w.out.x, 0, w.out.y);
      const from = edge.clone().addScaledVector(out, 0.5);
      fx.dust(from);
      if (!w.zombie && !engine) {
        engine = true;
        fx.headlights(from.clone().addScaledVector(out, 1.5), out.clone().negate());
        sfx.vehicle(from);
      }
      await units.arrive(w.u, [from, ...w.tiles.map((t) => tileToWorld(t))], walkers[i]!.steps, [0, ...plan.slice(1)]);
    }),
    ...risers.map(async (u, i) => {
      await wait(i * 0.2);
      const to = tileToWorld(u.pos);
      fx.dust(to, 9);
      if (!DEAD_TEMPLATES.has(u.template) && !engine) {
        engine = true;
        sfx.vehicle(to);
      }
      await units.getUp(u);
    }),
  ]);
}

/** The seconds a way in takes at its pace, uncapped. */
function natural(w: WayIn): number {
  let tiles = 0.5;
  for (let k = 1; k < w.tiles.length; k++) tiles += w.tiles[k]!.x !== w.tiles[k - 1]!.x && w.tiles[k]!.y !== w.tiles[k - 1]!.y ? DIAGONAL : 1;
  return tiles * (w.zombie ? PACE.dead : PACE.living);
}

/**
 * Unit `u`'s way in from the edge of the map: the cheapest way (the rules' own, path.ts) from a tile of the
 * edge on the ground to its tile, EDGE_WALK at most, every step of it one the rules allow that way round;
 * null if there is none (it gets up where it is).
 */
function wayIn(s: GameState, u: Unit): WayIn | null {
  const at: Pos = { x: u.pos.x, y: u.pos.y, z: u.pos.z ?? 0 };
  if (at.z !== 0) return null;
  const reach = computeReach(s, u, EDGE_WALK * COST_ORTHO, at);
  let best = -1;
  let cost = Infinity;
  for (const [node, c] of reach.cost) {
    const p = posOf(s, node);
    if (p.z !== 0 || c >= cost || (p.x !== 0 && p.y !== 0 && p.x !== s.width - 1 && p.y !== s.height - 1)) continue;
    best = node;
    cost = c;
  }
  if (best < 0) return null;
  // From the edge back to its tile, then each step checked the way it is walked (a drop one way is a climb the other).
  const tiles: Pos[] = [];
  for (let n: number | undefined = best; n !== undefined; n = reach.prev.get(n)) tiles.push(posOf(s, n));
  for (let k = 1; k < tiles.length; k++) if (stepBetween(s, tiles[k - 1]!, tiles[k]!) === null) return null;
  const edge = tiles[0]!;
  const outs: Vec2[] = [];
  if (edge.x === 0) outs.push({ x: -1, y: 0 });
  if (edge.x === s.width - 1) outs.push({ x: 1, y: 0 });
  if (edge.y === 0) outs.push({ x: 0, y: -1 });
  if (edge.y === s.height - 1) outs.push({ x: 0, y: 1 });
  // At a corner, out the side facing away from where it heads.
  const next = tiles[1];
  const out = next ? outs.reduce((a, b) => (a.x * (next.x - edge.x) + a.y * (next.y - edge.y) <= b.x * (next.x - edge.x) + b.y * (next.y - edge.y) ? a : b)) : outs[0]!;
  return { u, zombie: DEAD_TEMPLATES.has(u.template), tiles, out };
}
