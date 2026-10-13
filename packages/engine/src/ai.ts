import { abilitiesOf, abilityBlocker, abilityTargets, blastTiles, canTargetTile, meleeTargets, psiChance } from './abilities';
import { inMeleeReach, previewShot, weaponOf } from './aim';
import { ABILITIES, isAttack, TEMPLATES } from './content';
import { coverAgainst, hasAnyCover } from './cover';
import { canStandOn, combatants, DIRS8, distance, getUnit, inBounds, sameTile, tileAt, unitAt } from './grid';
import { canSee, sightOrigin, sightTo } from './los';
import { buildPath, computeReach, costPerAp, destinations, type Reach } from './path';
import type { Sim } from './rules';
import type { AbilityId, AttackAbility, GameState, Pod, Unit, Vec2 } from './types';

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
    if (sightOrigin(s, e.pos, pos, sightTo(s, e, pos))) {
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

/** One action toward the nearest of `goals`, ending in cover if it can. */
function moveToward(sim: Sim, u: Unit, goals: Vec2[]): boolean {
  const s = sim.s;
  const reach = computeReach(s, u, costPerAp(u));
  const cand = bestCandidate(s, u, reach, (p) => {
    const nearest = Math.min(...goals.map((g) => distance(p, g)));
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

/** How far ahead a walk is planned, in actions: enough to go round a building to its door. */
const LOOKAHEAD = 4;

/**
 * A unit's walk this turn toward `goal`: the shortest way to the free tile
 * nearest it (a pod on patrol goes round by the doors and keeps out of
 * windows), as far along as `actions` go. Null when it gets no closer.
 */
function walkPath(s: GameState, u: Unit, goal: Vec2, minGap = 0, actions = 1): Vec2[] | null {
  const all = computeReach(s, u, costPerAp(u) * Math.max(LOOKAHEAD, actions));
  const dest = closestTo(s, u, all, goal, minGap);
  const path = dest && buildPath(s, u, all, dest);
  if (!path) return null;
  const budget = costPerAp(u) * actions;
  let end = -1;
  for (let i = 0; i < path.length; i++) {
    const p = path[i]!;
    if (all.cost.get(p.y * s.width + p.x)! > budget) break;
    if (canStandOn(tileAt(s, p.x, p.y)) && !unitAt(s, p)) end = i;
  }
  return end >= 0 ? path.slice(0, end + 1) : null;
}

/** Actions a pod still unaware spends going to look where it heard something (its round takes one). */
const INVESTIGATE_ACTIONS = 2;

/**
 * Inactive pods walk their patrol route: the leader heads for the next
 * waypoint, the others follow. A pod that heard something goes to look there
 * first (docs/FASE2.md), faster, and back to its round once it finds nobody.
 * Walking into sight of the squad activates the pod (handled by Sim.moveAlong):
 * the noise alone never does.
 */
export function patrolPod(sim: Sim, pod: Pod): void {
  const s = sim.s;
  const members = pod.unitIds.map((id) => getUnit(s, id)).filter((u): u is Unit => !!u?.alive);
  const leader = members[0];
  if (!leader) return;

  const heard = members.find((m) => m.heard)?.heard;
  if (heard) {
    const path = distance(leader.pos, heard) > 1.5 ? walkPath(s, leader, heard, 0, INVESTIGATE_ACTIONS) : null;
    if (path) {
      sim.moveAlong(leader, path, 0);
      follow(sim, pod, members, INVESTIGATE_ACTIONS);
      return;
    }
    // Nobody there, or no way to it.
    for (const m of members) if (m.heard) sim.emit({ t: 'forgot', unit: m.id });
  }
  if (!pod.patrol.length) return;

  let index = pod.patrolIndex % pod.patrol.length;
  let path = walkPath(s, leader, pod.patrol[index]!);
  if (!path || distance(leader.pos, pod.patrol[index]!) <= 2) {
    index = (index + 1) % pod.patrol.length;
    sim.emit({ t: 'patrol', pod: pod.id, index });
    path = walkPath(s, leader, pod.patrol[index]!);
  }
  if (path) sim.moveAlong(leader, path, 0);
  follow(sim, pod, members, 1);
}

/** The rest of an unaware pod keep up with its leader (the first of `members`). */
function follow(sim: Sim, pod: Pod, members: Unit[], actions: number): void {
  const s = sim.s;
  const leader = members[0]!;
  for (const f of members.slice(1)) {
    if (sim.over || (s.pods.find((p) => p.id === pod.id)?.active ?? true) || !leader.alive) return;
    if (!f.alive || distance(f.pos, leader.pos) <= 1.5) continue;
    const walk = walkPath(s, f, leader.pos, 1, actions);
    if (walk) sim.moveAlong(f, walk, 0);
  }
}

/**
 * Where a living enemy heads when it has nothing to shoot: the soldiers, or
 * the last noise it heard while it sees none of them (reached, it forgets it).
 */
function headFor(sim: Sim, u: Unit, enemies: Unit[]): Vec2[] {
  const s = sim.s;
  if (u.heard && !enemies.some((e) => canSee(s, u, e))) {
    if (distance(u.pos, u.heard) > 1.5) return [u.heard];
    sim.emit({ t: 'forgot', unit: u.id });
  }
  return enemies.map((e) => e.pos);
}

/**
 * One enemy's full turn (also drives mind-controlled soldiers). The living
 * take cover and shoot; the dead have their own (takeZombieTurn).
 */
export function takeAlienTurn(sim: Sim, u: Unit): void {
  if (TEMPLATES[u.template].undead) {
    takeZombieTurn(sim, u);
    return;
  }
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
      if ((u.ap >= 2 || !shoots) && moveToward(sim, u, headFor(sim, u, enemies))) continue;
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
      } else if (moveToward(sim, u, headFor(sim, u, enemies))) {
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

// ---------------------------------------------------------------- the dead

/** The Chillón keeps this far from the soldiers it sees: it screams, it doesn't bite. */
const SCREAMER_GAP = 5;

/** Nearest first; ties by id, so the choice is the same every time. */
const byDistance = (from: Vec2) => (a: Unit, b: Unit) => distance(from, a.pos) - distance(from, b.pos) || a.id.localeCompare(b.id);

/** Tiles `u` covers in one action: no soldier further than this (plus a step) can be reached this action. */
const stride = (u: Unit) => TEMPLATES[u.template].mobility + u.mods.mobility + 1.5;

/** One action's walk toward `goal`, round whatever is in the way; false when it gets no closer. */
function walk(sim: Sim, u: Unit, goal: Vec2, minGap: number): boolean {
  const path = walkPath(sim.s, u, goal, minGap);
  if (!path) return false;
  sim.moveAlong(u, path, 1);
  return true;
}

/** The way, within one action, to a tile beside one of `soldiers` (the cheapest): for the Hinchado. */
function besideSoldier(s: GameState, u: Unit, soldiers: Unit[]): Vec2[] | null {
  const near = soldiers.filter((t) => distance(u.pos, t.pos) <= stride(u));
  if (!near.length) return null;
  const reach = computeReach(s, u, costPerAp(u));
  let best: { path: Vec2[]; cost: number } | null = null;
  for (const t of near) {
    for (const d of DIRS8) {
      const tile = { x: t.pos.x + d.x, y: t.pos.y + d.y };
      if (!inBounds(s, tile.x, tile.y) || !inMeleeReach(s, tile, t.pos)) continue;
      const cost = reach.cost.get(tile.y * s.width + tile.x);
      if (cost === undefined || (best && cost >= best.cost)) continue;
      const path = buildPath(s, u, reach, tile);
      if (path) best = { path, cost };
    }
  }
  return best?.path ?? null;
}

/** The Chillón steps back (one action) from the soldiers it sees. */
function backAway(sim: Sim, u: Unit, seen: Unit[]): boolean {
  const s = sim.s;
  const reach = computeReach(s, u, costPerAp(u));
  const cand = bestCandidate(s, u, reach, (p) => Math.min(SCREAMER_GAP + 2, ...seen.map((e) => distance(p, e.pos))) * 4);
  if (!cand || sameTile(cand.pos, u.pos)) return false;
  const path = buildPath(s, u, reach, cand.pos);
  if (!path) return false;
  sim.moveAlong(u, path, 1);
  return true;
}

/** Nothing seen and nothing heard: a few aimless steps (one action). */
function wander(sim: Sim, u: Unit): void {
  const s = sim.s;
  const reach = computeReach(s, u, costPerAp(u));
  const options = destinations(s, u, reach).filter((o) => distance(o.pos, u.pos) >= 2);
  if (!options.length) return;
  const path = buildPath(s, u, reach, options[sim.rng.int(options.length)]!.pos);
  if (path) sim.moveAlong(u, path, 1);
}

/**
 * One zombie's turn (docs/FASE2.md). No cover, no guns: it goes for the
 * nearest soldier it sees and bites; seeing nobody, it goes to the last noise
 * it heard, and having heard nothing, it wanders. The Chillón screams (and
 * every zombie within 16 tiles hears it) and keeps its distance; the Hinchado
 * walks up to a soldier and bursts. Cheap enough for a horde of twenty: one
 * reach per action and a sight check per soldier.
 */
export function takeZombieTurn(sim: Sim, u: Unit): void {
  const s = sim.s;
  const abilities = abilitiesOf(u);
  const usable = (a: AbilityId) => abilities.includes(a) && !abilityBlocker(s, u, a);
  const bite = abilities.find((a): a is AttackAbility => ABILITIES[a].target === 'melee' && isAttack(a));
  for (let guard = 0; guard < 6 && u.alive && u.ap > 0 && !sim.over; guard++) {
    const soldiers = combatants(s, 'xcom');
    if (!soldiers.length) return;
    const seen = soldiers.filter((e) => canSee(s, u, e)).sort(byDistance(u.pos));

    if (abilities.includes('mindspin')) {
      // The Chillón: a scream at whoever it sees (one a turn for the whole horde), or a corpse raised.
      if (usable('mindspin') && sim.alienBudget.psi > 0) {
        const victims = abilityTargets(s, u, 'mindspin').sort((a, b) => b.rank - a.rank || psiChance(b, s) - psiChance(a, s) || a.id.localeCompare(b.id));
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
      if (seen.length) {
        if (distance(u.pos, seen[0]!.pos) < SCREAMER_GAP && backAway(sim, u, seen)) continue;
        return;
      }
    } else if (abilities.includes('burst')) {
      // The Hinchado: beside a soldier, it bursts; otherwise it lumbers up to the nearest it sees.
      if (usable('burst') && soldiers.some((e) => inMeleeReach(s, u.pos, e.pos))) {
        sim.perform(u, 'burst');
        return;
      }
      if (seen.length) {
        const way = besideSoldier(s, u, seen);
        if (way) {
          sim.moveAlong(u, way, 1);
          continue;
        }
        if (walk(sim, u, seen[0]!.pos, 1)) continue;
        return;
      }
    } else {
      // A bite for whoever is within one action's reach (the nearest), or a step toward the nearest in sight.
      if (bite && usable(bite) && soldiers.some((e) => distance(u.pos, e.pos) <= stride(u))) {
        const options = meleeTargets(s, u, bite);
        if (options.length) {
          const pick = options.sort((a, b) => a.path.length - b.path.length || a.target.hp - b.target.hp || a.target.id.localeCompare(b.target.id))[0]!;
          sim.perform(u, bite, { melee: pick });
          return;
        }
      }
      if (seen.length) {
        if (walk(sim, u, seen[0]!.pos, 1)) continue;
        return;
      }
    }

    // Nobody in sight: to the last noise heard, and having found nobody there, wandering.
    if (u.heard) {
      if (distance(u.pos, u.heard) > 1.5 && walk(sim, u, u.heard, 0)) continue;
      sim.emit({ t: 'forgot', unit: u.id });
    }
    wander(sim, u);
    return;
  }
}
