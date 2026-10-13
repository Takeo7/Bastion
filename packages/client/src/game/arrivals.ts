import * as THREE from 'three';
import type { GameState, Unit, Vec2 } from '@bastion/engine';
import { tileToWorld } from './coords';
import type { Fx } from './fx';
import type { Sfx } from './sfx';
import { DEAD_TEMPLATES } from './syntyManifest';
import { wait } from './tween';
import type { UnitViews } from './unitViews';

// Reinforcements and the horde arriving (instead of drop pods): the dead
// stagger in from the nearest edge of the map with a moan and dust, raiders
// run in, a pickup's headlights flaring at the edge behind them. Whoever
// arrives far from any edge gets up out of a cloud of dust where they are.

/** Tiles in from an edge that a unit still walks in from it. */
const EDGE_WALK = 7;
/** Seconds per tile: the dead stagger, the living run (as a move). */
const PACE = { dead: 0.42, living: 0.16 };
/** No arrival walk lasts longer than this, so the turn keeps moving. */
const LONGEST = 2.2;

/** Plays the arrival of `arrived` (units the squad sees, not yet in the state); resolves when they all stand on their tiles. */
export async function playArrival(units: UnitViews, fx: Fx, sfx: Sfx, s: GameState, arrived: Unit[]): Promise<void> {
  if (!arrived.length) return;
  const dead = arrived.filter((u) => DEAD_TEMPLATES.has(u.template)).length;
  if (dead) sfx.moan(tileToWorld(arrived[0]!.pos), dead);
  let engine = false;
  await Promise.all(
    arrived.map(async (u, i) => {
      await wait(i * 0.2);
      const zombie = DEAD_TEMPLATES.has(u.template);
      const to = tileToWorld(u.pos);
      const edge = nearestEdge(s, u.pos);
      if (edge.dist > EDGE_WALK) {
        fx.dust(to, 9);
        if (!zombie && !engine) {
          engine = true;
          sfx.vehicle(to);
        }
        await units.getUp(u);
        return;
      }
      const out = new THREE.Vector3(edge.dir.x, 0, edge.dir.y);
      const tiles = edge.dist + 1.5;
      const from = to.clone().addScaledVector(out, tiles).setY(0);
      fx.dust(to.clone().addScaledVector(out, edge.dist + 0.5).setY(0));
      if (!zombie && !engine) {
        engine = true;
        fx.headlights(from.clone().addScaledVector(out, 1.5), out.clone().negate());
        sfx.vehicle(from);
      }
      await units.arrive(u, from, Math.min(LONGEST, tiles * (zombie ? PACE.dead : PACE.living)));
    }),
  );
}

/** The map edge nearest to `p`: the way out and how many tiles away. */
function nearestEdge(s: GameState, p: Vec2): { dir: Vec2; dist: number } {
  const edges = [
    { dir: { x: -1, y: 0 }, dist: p.x },
    { dir: { x: 1, y: 0 }, dist: s.width - 1 - p.x },
    { dir: { x: 0, y: -1 }, dist: p.y },
    { dir: { x: 0, y: 1 }, dist: s.height - 1 - p.y },
  ];
  return edges.reduce((a, b) => (b.dist < a.dist ? b : a));
}
