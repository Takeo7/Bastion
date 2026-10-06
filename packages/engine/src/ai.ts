import { abilitiesOf, abilityBlocker, abilityTargets, blastTiles, canTargetTile, meleeTargets, psiChance } from './abilities';
import { previewShot, weaponOf } from './aim';
import { ABILITIES, isAttack, TEMPLATES } from './content';
import { coverAgainst, hasAnyCover } from './cover';
import { combatants, DIRS8, distance, getUnit, sameTile, unitAt } from './grid';
import { sightOrigin } from './los';
import { buildPath, computeReach, costPerAp, destinations, type Reach } from './path';
import type { Sim } from './rules';
import type { AbilityId, GameState, Pod, Unit, Vec2 } from './types';

interface Candidate {
  pos: Vec2;
  cost: number;
  score: number;
}

/**
 * Desirability of standing on `pos`: good cover against every soldier that can
 * see it, being flanked is very bad, and a good shot from there is a bonus.
 */
function tileScore(s: GameState, u: Unit, pos: Vec2, enemies: Unit[], shotWeight: number): number {
  let score = 0;
  let bestShot = 0;
  for (const e of enemies) {
    if (sightOrigin(s, e.pos, pos, TEMPLATES[e.template].sight)) {
      const cover = coverAgainst(s, pos, e.pos);
      score += cover === 2 ? 25 : cover === 1 ? 10 : -30;
    }
    const shot = previewShot(s, u, e, { from: pos });
    if (shot) bestShot = Math.max(bestShot, shot.hit + (shot.flanked ? 15 : 0));
  }
  score += bestShot * shotWeight;
  const nearest = Math.min(...enemies.map((e) => distance(pos, e.pos)));
  if (nearest > 14) score -= (nearest - 14) * 3;
  // Ranged aliens keep their distance: hugging a soldier gets them flanked and shot point-blank.
  if (nearest < 2.5) score -= 70;
  else if (nearest < 4.5) score -= (4.5 - nearest) * 16;
  return score;
}

function bestCandidate(s: GameState, u: Unit, reach: Reach, score: (p: Vec2) => number): Candidate | null {
  const options = [{ pos: u.pos, cost: 0 }, ...destinations(s, u, reach)];
  let best: Candidate | null = null;
  for (const o of options) {
    const c = { pos: o.pos, cost: o.cost, score: score(o.pos) - o.cost * 0.3 };
    if (
      !best ||
      c.score > best.score ||
      (c.score === best.score && (c.cost < best.cost || (c.cost === best.cost && (c.pos.y < best.pos.y || (c.pos.y === best.pos.y && c.pos.x < best.pos.x)))))
    ) {
      best = c;
    }
  }
  return best;
}

/** Where a freshly activated alien runs to: cover first, shots barely matter. */
export function chooseScamperTile(s: GameState, u: Unit): Vec2 | null {
  const enemies = combatants(s, 'xcom');
  if (!enemies.length) return null;
  const reach = computeReach(s, u, costPerAp(u));
  const best = bestCandidate(s, u, reach, (p) => tileScore(s, u, p, enemies, 0.1));
  return best && !sameTile(best.pos, u.pos) ? best.pos : null;
}

/**
 * Best impact point for an explosive: as many soldiers as possible, never an
 * ally. Soldiers well protected from bullets count extra.
 */
function bestBlast(s: GameState, u: Unit, ability: 'grenade' | 'launch' | 'missiles'): { tile: Vec2; score: number } | null {
  const soldiers = combatants(s, 'xcom');
  const candidates: Vec2[] = [];
  for (const soldier of soldiers) {
    candidates.push(soldier.pos);
    for (const d of DIRS8) candidates.push({ x: soldier.pos.x + d.x, y: soldier.pos.y + d.y });
  }
  let best: { tile: Vec2; score: number } | null = null;
  for (const tile of candidates) {
    if (!canTargetTile(s, u, ability, tile)) continue;
    let score = 0;
    let friendly = false;
    for (const p of blastTiles(s, ability, tile, u)) {
      const hit = unitAt(s, p);
      if (!hit) continue;
      if (hit.team === u.team) {
        friendly = true;
        break;
      }
      const shot = previewShot(s, u, hit);
      score += 1 + (!shot || shot.hit < 35 ? 0.5 : 0);
    }
    if (friendly || score === 0) continue;
    if (!best || score > best.score) best = { tile, score };
  }
  return best;
}

function moveToward(sim: Sim, u: Unit, enemies: Unit[]): boolean {
  const s = sim.s;
  const reach = computeReach(s, u, costPerAp(u));
  const cand = bestCandidate(s, u, reach, (p) => {
    const nearest = Math.min(...enemies.map((e) => distance(p, e.pos)));
    return -nearest * 4 + (hasAnyCover(s, p) ? 6 : 0);
  });
  if (!cand || sameTile(cand.pos, u.pos)) return false;
  const path = buildPath(s, u, reach, cand.pos);
  if (!path) return false;
  sim.moveAlong(u, path, 1);
  return true;
}

/** Reachable tile closest to `goal` (ties: cheaper, then reading order). */
function closestTo(s: GameState, u: Unit, reach: Reach, goal: Vec2, minGap = 0): Vec2 | null {
  let best: { pos: Vec2; d: number; cost: number } | null = null;
  for (const o of destinations(s, u, reach)) {
    const d = distance(o.pos, goal);
    if (d < minGap) continue;
    if (!best || d < best.d - 1e-9 || (Math.abs(d - best.d) < 1e-9 && (o.cost < best.cost || (o.cost === best.cost && (o.pos.y < best.pos.y || (o.pos.y === best.pos.y && o.pos.x < best.pos.x)))))) {
      best = { pos: o.pos, d, cost: o.cost };
    }
  }
  return best && best.d < distance(u.pos, goal) ? best.pos : null;
}

/**
 * Inactive pods walk their patrol route: the leader heads for the next
 * waypoint, the others follow. Walking into sight of the squad activates the
 * pod (handled by Sim.moveAlong).
 */
export function patrolPod(sim: Sim, pod: Pod): void {
  const s = sim.s;
  const isActive = () => s.pods.find((p) => p.id === pod.id)?.active ?? true;
  const members = pod.unitIds.map((id) => getUnit(s, id)).filter((u): u is Unit => !!u?.alive);
  const leader = members[0];
  if (!leader || !pod.patrol.length) return;

  let index = pod.patrolIndex % pod.patrol.length;
  const reachLeader = computeReach(s, leader, costPerAp(leader));
  let dest = closestTo(s, leader, reachLeader, pod.patrol[index]!);
  if (!dest || distance(leader.pos, pod.patrol[index]!) <= 2) {
    index = (index + 1) % pod.patrol.length;
    sim.emit({ t: 'patrol', pod: pod.id, index });
    dest = closestTo(s, leader, reachLeader, pod.patrol[index]!);
  }
  if (dest) {
    const path = buildPath(s, leader, reachLeader, dest);
    if (path) sim.moveAlong(leader, path, 0);
  }
  for (const f of members.slice(1)) {
    if (sim.over || isActive() || !leader.alive) return;
    if (!f.alive || distance(f.pos, leader.pos) <= 1.5) continue;
    const reach = computeReach(s, f, costPerAp(f));
    const spot = closestTo(s, f, reach, leader.pos, 1);
    const path = spot ? buildPath(s, f, reach, spot) : null;
    if (path) sim.moveAlong(f, path, 0);
  }
}

/** One alien's full turn (also drives mind-controlled soldiers). */
export function takeAlienTurn(sim: Sim, u: Unit): void {
  const s = sim.s;
  const abilities = abilitiesOf(u);
  const usable = (a: AbilityId) => abilities.includes(a) && !abilityBlocker(s, u, a);
  for (let guard = 0; guard < 6 && u.alive && u.ap > 0 && !sim.over; guard++) {
    const enemies = combatants(s, 'xcom');
    if (!enemies.length) return;

    if (weaponOf(u).clip > 0 && u.ammo <= 0 && usable('reload')) {
      sim.perform(u, 'reload');
      continue;
    }

    // Sectoids go for the mind first, and raise the dead when they can.
    if (usable('mindspin') && sim.alienBudget.psi > 0) {
      const victims = abilityTargets(s, u, 'mindspin').sort((a, b) => b.rank - a.rank || psiChance(b) - psiChance(a) || a.id.localeCompare(b.id));
      if (victims.length) {
        sim.alienBudget.psi--;
        sim.perform(u, 'mindspin', { target: victims[0]! });
        return;
      }
    }
    if (usable('reanimate')) {
      const corpse = abilityTargets(s, u, 'reanimate')[0];
      if (corpse) {
        sim.perform(u, 'reanimate', { target: corpse });
        return;
      }
    }

    // Explosives against clustered or well-covered soldiers.
    for (const ability of ['missiles', 'grenade', 'launch'] as const) {
      if (!usable(ability) || sim.alienBudget.explosives <= 0) continue;
      const blast = bestBlast(s, u, ability);
      if (blast && blast.score >= 2) {
        sim.alienBudget.explosives--;
        sim.perform(u, ability, { tile: blast.tile });
        return;
      }
    }

    // Melee units charge whoever they can reach.
    const melee = abilities.find((a) => ABILITIES[a].target === 'melee');
    const shoots = abilities.includes('shoot') && !weaponOf(u).melee;
    if (melee && isAttack(melee) && usable(melee)) {
      const options = meleeTargets(s, u, melee);
      if (options.length) {
        const pick = options.sort((a, b) => a.target.hp - b.target.hp || a.path.length - b.path.length)[0]!;
        sim.perform(u, melee, { melee: pick });
        return;
      }
      if ((u.ap >= 2 || !shoots) && moveToward(sim, u, enemies)) continue;
    }
    if (!shoots) return;

    const dmg = weaponOf(u).damage;
    let best: { target: Unit; hit: number; value: number } | null = null;
    for (const e of enemies) {
      const p = previewShot(s, u, e);
      if (!p) continue;
      const value = p.hit + (p.flanked ? 20 : 0) + (e.hp <= dmg ? 15 : 0);
      if (!best || value > best.value) best = { target: e, hit: p.hit, value };
    }

    if (u.ap >= 2) {
      const reach = computeReach(s, u, costPerAp(u));
      if (best) {
        const here = tileScore(s, u, u.pos, enemies, 0.5);
        const cand = bestCandidate(s, u, reach, (p) => tileScore(s, u, p, enemies, 0.5));
        if (cand && !sameTile(cand.pos, u.pos) && cand.score > here + 8) {
          const path = buildPath(s, u, reach, cand.pos);
          if (path) {
            sim.moveAlong(u, path, 1);
            continue;
          }
        }
        if (best.hit >= 35 && usable('shoot')) {
          sim.perform(u, 'shoot', { target: best.target });
          return;
        }
      } else if (moveToward(sim, u, enemies)) {
        continue;
      }
    } else if (best && best.hit >= 20 && usable('shoot')) {
      sim.perform(u, 'shoot', { target: best.target });
      return;
    }

    if (usable('overwatch')) sim.perform(u, 'overwatch');
    else if (usable('reload')) sim.perform(u, 'reload');
    return;
  }
}
