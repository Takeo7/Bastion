import { abilitiesOf, abilityBlocker, abilityTargets, blastTiles, breakable, canTargetTile, meleeTargets, psiChance } from './abilities';
import { inMeleeReach, previewShot, weaponOf } from './aim';
import { ABILITIES, hostileTo, isAttack, TEMPLATES } from './content';
import { coverAgainst, hasAnyCover } from './cover';
import { canStopAt, hasLongFlights } from './flights';
import { combatants, DIRS8, distance, getUnit, heightAt, inBounds, kindAt, nodeOf, placesOn, posOf, samePos, storeysApart, surfaceAt, unitAt } from './grid';
import { reachesToSearch } from './loot';
import { canSee, sightOrigin, sightTo } from './los';
import { buildPath, COST_ORTHO, computeReach, costPerAp, destinations, type Reach } from './path';
import type { Sim } from './rules';
import type { AbilityId, AttackAbility, GameState, Pod, Unit, Pos } from './types';

/**
 * How far one place is from another for a walker (docs/FASE6-PLAN.md): the
 * tiles between them, and for each storey between them the way round to the
 * stairs. Without storeys, the distance. With flights of several tiles
 * (docs/MAPAS.md, phase 2) the way round is a tile longer; a map without them
 * (saved before, hand-made) as it was.
 */
const STOREY_WALK = 4;
const OLD_STOREY_WALK = 3;
const farness = (s: GameState, a: Pos, b: Pos) => distance(a, b) + (hasLongFlights(s) ? STOREY_WALK : OLD_STOREY_WALK) * storeysApart(s, a, b);

/** Beside `b` (or on it), on its floor: a walker that got there has arrived. */
const arrived = (s: GameState, a: Pos, b: Pos, gap: number) => distance(a, b) <= gap && storeysApart(s, a, b) === 0;

/** Points a living enemy gives a place for each storey it stands over the nearest soldier: they like to be posted up high. */
const HIGH_GROUND = 6;

interface Candidate {
  pos: Pos;
  cost: number;
  score: number;
}

/**
 * Desirability of standing on `pos`: good cover against every soldier that can
 * see it, being flanked is very bad, and a good shot from there is a bonus.
 */
function tileScore(s: GameState, u: Unit, pos: Pos, enemies: Unit[], shotWeight: number): number {
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
  // The high ground (storeys over the nearest soldier, on a map with them).
  if (s.levels) {
    const below = [...enemies].sort((a, b) => distance(pos, a.pos) - distance(pos, b.pos))[0];
    if (below) score += HIGH_GROUND * Math.max(0, Math.round(heightAt(s, pos) - heightAt(s, below.pos)));
  }
  const nearest = Math.min(...enemies.map((e) => distance(pos, e.pos)));
  if (nearest > 14) score -= (nearest - 14) * 3;
  // Ranged aliens keep their distance: hugging a soldier gets them flanked and shot point-blank.
  if (nearest < 2.5) score -= 70;
  else if (nearest < 4.5) score -= (4.5 - nearest) * 16;
  return score;
}

function bestCandidate(s: GameState, u: Unit, reach: Reach, score: (p: Pos) => number): Candidate | null {
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
export function chooseScamperTile(s: GameState, u: Unit): Pos | null {
  const enemies = combatants(s, 'xcom');
  if (!enemies.length) return null;
  const reach = computeReach(s, u, costPerAp(u));
  const best = bestCandidate(s, u, reach, (p) => tileScore(s, u, p, enemies, 0.1));
  return best && !samePos(best.pos, u.pos) ? best.pos : null;
}

/**
 * Best impact point for an explosive: as many soldiers as possible, never an
 * ally. Soldiers well protected from bullets count extra.
 */
function bestBlast(s: GameState, u: Unit, ability: 'grenade' | 'launch' | 'missiles'): { tile: Pos; score: number } | null {
  const soldiers = combatants(s, 'xcom');
  const candidates: Pos[] = [];
  for (const soldier of soldiers) {
    candidates.push(soldier.pos);
    for (const d of DIRS8) candidates.push({ x: soldier.pos.x + d.x, y: soldier.pos.y + d.y, z: soldier.pos.z });
  }
  let best: { tile: Pos; score: number } | null = null;
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
function moveToward(sim: Sim, u: Unit, goals: Pos[]): boolean {
  const s = sim.s;
  const nearest = (p: Pos) => Math.min(...goals.map((g) => farness(s, p, g)));
  // A goal on another storey: the way there goes round by the stairs, further first (a walk planned ahead).
  const target = [...goals].sort((a, b) => farness(s, u.pos, a) - farness(s, u.pos, b))[0];
  if (target && storeysApart(s, u.pos, target) > 0) return walk(sim, u, target, 1);
  const reach = computeReach(s, u, costPerAp(u));
  const cand = bestCandidate(s, u, reach, (p) => -nearest(p) * 4 + (hasAnyCover(s, p) ? 6 : 0));
  if (!cand || samePos(cand.pos, u.pos)) return false;
  const path = buildPath(s, u, reach, cand.pos);
  if (!path) return false;
  sim.moveAlong(u, path, 1);
  return true;
}

/** Reachable tile closest to `goal` (ties: cheaper, then reading order). */
function closestTo(s: GameState, u: Unit, reach: Reach, goal: Pos, minGap = 0): Pos | null {
  // Without storeys, the distance squared: the same order, without a root for every tile (a horde asks it a lot).
  const far = s.levels ? (p: Pos) => farness(s, p, goal) : (p: Pos) => (p.x - goal.x) ** 2 + (p.y - goal.y) ** 2;
  const gap = s.levels ? minGap : minGap * minGap;
  let best: { pos: Pos; d: number; cost: number } | null = null;
  for (const o of destinations(s, u, reach)) {
    const d = far(o.pos);
    if (d < gap) continue;
    if (!best || d < best.d - 1e-9 || (Math.abs(d - best.d) < 1e-9 && (o.cost < best.cost || (o.cost === best.cost && (o.pos.y < best.pos.y || (o.pos.y === best.pos.y && o.pos.x < best.pos.x)))))) {
      best = { pos: o.pos, d, cost: o.cost };
    }
  }
  return best && best.d < far(u.pos) ? best.pos : null;
}

/** How far ahead a walk is planned, in actions: enough to go round a building to its door. */
const LOOKAHEAD = 4;
/** To a goal on another storey, the way round by the stairs may first lead away: planned as far as it goes. */
const LOOKAHEAD_STOREYS = 40;

/** How much longer a way round its own may be than the way through them, in half-tiles (COST_ORTHO a tile): a few tiles. */
const DETOUR = 4 * COST_ORTHO;

/** A planned walk, and whether it waits behind its own (none of it free to take). */
interface Walk {
  path: Pos[] | null;
  jammed: boolean;
}

/**
 * A unit's walk this turn toward `goal`: the shortest way to the free tile
 * nearest it (a pod on patrol goes round by the doors and keeps out of
 * windows), as far along as `actions` go. The way is planned through its own
 * side, who will move on, as the walls won't; but nobody walks through anyone
 * (Arturo, 19/10). With its own in the way, it goes round them if that is
 * hardly longer (DETOUR), and so a crowd spreads over open ground; else it
 * queues behind them, as at a siege's gate, rather than press on a wall
 * beside it; its way taken from the start, it closes in on where it jams as
 * far as the free ground lets it, or waits (`jammed`). Ending nowhere one can
 * stand within reach (a window), the free tile nearest the goal that
 * `actions` reach. No path when it gets no closer.
 */
function planWalk(s: GameState, u: Unit, goal: Pos, minGap = 0, actions = 1): Walk {
  const ahead = storeysApart(s, u.pos, goal) > 0 ? LOOKAHEAD_STOREYS : LOOKAHEAD;
  const reachOf = (round: boolean) => computeReach(s, u, costPerAp(u) * Math.max(ahead, actions), u.pos, round ? undefined : (o) => !hostileTo(o, u));
  const through = reachOf(false);
  const dest = closestTo(s, u, through, goal, minGap);
  const path = dest && buildPath(s, u, through, dest);
  if (!path) return { path: null, jammed: false };
  const budget = costPerAp(u) * actions;
  /** How far along `way` the unit can go now (the last place it can stop at), and who stops it first. */
  const stretch = (way: Pos[], reach: Reach) => {
    let end = -1;
    let jam: Pos | null = null;
    for (let i = 0; i < way.length; i++) {
      const p = way[i]!;
      if (reach.cost.get(nodeOf(s, p))! > budget) break;
      if (inTheWay(s, i ? way[i - 1]! : u.pos, p)) {
        jam = p;
        break;
      }
      if (canStopAt(s, p)) end = i;
    }
    return { end, jam };
  };
  const { end, jam } = stretch(path, through);
  // Held up behind its own before half its walk: the way round them, if there is one.
  const round = jam && (end < 0 || through.cost.get(nodeOf(s, path[end]!))! * 2 < budget) ? reachOf(true) : null;
  if (round) {
    const cost = round.cost.get(nodeOf(s, dest!));
    if (cost !== undefined && cost <= through.cost.get(nodeOf(s, dest!))! + DETOUR) {
      const other = buildPath(s, u, round, dest!)!;
      const go = stretch(other, round).end;
      if (go >= 0) return { path: other.slice(0, go + 1), jammed: false };
    }
  }
  if (end >= 0) return { path: path.slice(0, end + 1), jammed: false };
  // Within reach now, round everyone (the way round, cut to this walk's actions: a Dijkstra's way there costs no more).
  const near = round ? within(round, budget) : computeReach(s, u, budget);
  const step = closestTo(s, u, near, jam ?? goal, jam ? 0 : minGap);
  const closer = step && buildPath(s, u, near, step);
  return { path: closer ?? null, jammed: !closer && !!jam };
}

/** `reach` cut to what costs `budget` at most. */
function within(reach: Reach, budget: number): Reach {
  return { origin: reach.origin, cost: new Map([...reach.cost].filter(([, c]) => c <= budget)), prev: reach.prev };
}

/** planWalk's path. */
function walkPath(s: GameState, u: Unit, goal: Pos, minGap = 0, actions = 1): Pos[] | null {
  return planWalk(s, u, goal, minGap, actions).path;
}

/** Whether somebody stops the step from `a` onto `b`: standing on it, or, on a diagonal, on a corner it would cut (path.ts computeReach). */
function inTheWay(s: GameState, a: Pos, b: Pos): boolean {
  if (unitAt(s, b)) return true;
  if (a.x === b.x || a.y === b.y) return false;
  const h = heightAt(s, a);
  return [surfaceAt(s, b.x, a.y, h), surfaceAt(s, a.x, b.y, h)].some((p) => !!p && !!unitAt(s, p));
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
    const path = !arrived(s, leader.pos, heard, 1.5) ? walkPath(s, leader, heard, 0, INVESTIGATE_ACTIONS) : null;
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
  if (!path || arrived(s, leader.pos, pod.patrol[index]!, 2)) {
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
    if (!f.alive || arrived(s, f.pos, leader.pos, 1.5)) continue;
    const walk = walkPath(s, f, leader.pos, 1, actions);
    if (walk) sim.moveAlong(f, walk, 0);
  }
}

/**
 * Where a living enemy heads when it has nothing to shoot: the soldiers, or
 * the last noise it heard while it sees none of them (reached, it forgets it).
 */
function headFor(sim: Sim, u: Unit, enemies: Unit[]): Pos[] {
  const s = sim.s;
  if (u.heard && !enemies.some((e) => canSee(s, u, e))) {
    if (!arrived(s, u.pos, u.heard, 1.5)) return [u.heard];
    sim.emit({ t: 'forgot', unit: u.id });
  }
  return enemies.map((e) => e.pos);
}

/** Whom `u` fights (content.ts hostileTo), able to be fought. */
function foes(s: GameState, u: Unit): Unit[] {
  return s.units.filter((t) => t.alive && hostileTo(u, t) && !t.objective && !t.captive);
}

/** How near one of the dead must be for the living to turn on it. */
const ZOMBIE_THREAT = 8;

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
    // The squad, and the dead it sees close by (18/10 #47): a zombie at hand is a threat too.
    const enemies = [...combatants(s, 'xcom'), ...foes(s, u).filter((e) => e.team !== 'xcom' && farness(s, u.pos, e.pos) <= ZOMBIE_THREAT && canSee(s, u, e))];
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
      // The squad first; a zombie right on them before anyone.
      const value = p.hit + (p.flanked ? 20 : 0) + (e.hp <= dmg ? 15 : 0) + (e.team === 'xcom' ? 10 : farness(s, u.pos, e.pos) <= 2 ? 30 : 0);
      if (!best || value > best.value) best = { target: e, hit: p.hit, value };
    }

    if (u.ap >= 2) {
      const reach = computeReach(s, u, costPerAp(u));
      if (best) {
        const here = tileScore(s, u, u.pos, enemies, 0.5);
        const cand = bestCandidate(s, u, reach, (p) => tileScore(s, u, p, enemies, 0.5));
        if (cand && !samePos(cand.pos, u.pos) && cand.score > here + 8) {
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

/** Nearest first (storeys counting as the way round to the stairs); ties by id, so the choice is the same every time. */
const byDistance = (s: GameState, from: Pos) => (a: Unit, b: Unit) => farness(s, from, a.pos) - farness(s, from, b.pos) || a.id.localeCompare(b.id);

/** Tiles `u` covers in one action: no soldier further than this (plus a step) can be reached this action. */
const stride = (u: Unit) => TEMPLATES[u.template].mobility + u.mods.mobility + 1.5;

/**
 * Whether one of the dead can only walk this turn, and doesn't scream. It
 * bites (or bursts) only beside a soldier: one already there, or one it sees
 * and can reach with a step (two actions: a step and a bite, playtest 16/10).
 * The rest only walk, and the enemy turn moves them after those who go for
 * the squad, all at once (Sim.runAlienTurn; Arturo, 17/10).
 */
export function onlyWalks(s: GameState, u: Unit): boolean {
  if (!TEMPLATES[u.template].undead || abilitiesOf(u).includes('mindspin')) return false;
  return !foes(s, u).some((e) => inMeleeReach(s, u.pos, e.pos) || (farness(s, u.pos, e.pos) <= stride(u) + 1 && canSee(s, u, e)));
}

/**
 * The dead that only walk, in the order they go: the nearest to the living
 * first (ties by id), so a crowd moves from its front and those behind find
 * the way ahead free, not taken by the ones still to move (19/10).
 */
export function frontFirst(s: GameState, walkers: Unit[]): Unit[] {
  const gap = (u: Unit) => Math.min(Infinity, ...foes(s, u).map((e) => farness(s, u.pos, e.pos)));
  return walkers
    .map((u) => ({ u, d: gap(u) }))
    .sort((a, b) => a.d - b.d || a.u.id.localeCompare(b.u.id))
    .map((o) => o.u);
}

/** One action's walk toward `goal`, round whatever is in the way; false when it gets no closer. */
function walk(sim: Sim, u: Unit, goal: Pos, minGap: number): boolean {
  const { path, jammed } = planWalk(sim.s, u, goal, minGap);
  // Waiting behind its own: it goes again once the rest have moved (Sim.runAlienTurn).
  if (jammed) sim.jammed.add(u.id);
  if (!path) return false;
  sim.moveAlong(u, path, 1);
  return true;
}

/** In a siege, tiles of walk a point of what a door or barricade holds is worth: the dead go for the weakest. */
const BREACH_HP_WEIGHT = 0.5;

/**
 * In a siege (docs/FASE12.md §4), where one of the dead breaks its way to
 * `goal` when it has no way there: the door or barricade beside a free tile
 * it can get to, counting the walk to it, the way left from it to the goal
 * and what it still holds. Only one that opens the way somewhere it can't get
 * to yet (not the other leaf of a gate already broken). `stand`, where it
 * beats on it from. Null when it has a way there, or nothing to break.
 */
export function breachPlan(s: GameState, u: Unit, goal: Pos, ways?: Map<number, Reach>): { tile: Pos; stand: Pos } | null {
  const holds = s.breakables;
  if (!s.mission.siege || !holds) return null;
  // Whether the ground leaves a way: its own crowd in a gap is about to move on, it doesn't close it. The same for
  // all the dead from one place, while nothing changes (`ways`, Sim.ways: one that waited behind its own asks again).
  const from = nodeOf(s, u.pos);
  const all = ways?.get(from) ?? computeReach(s, u, Infinity, u.pos, (o) => !hostileTo(o, u));
  ways?.set(from, all);
  // Somewhere to stand by the goal: a window's frame beside it is no way in, nobody stops there.
  for (let dy = -1; dy <= 1; dy++) {
    for (let dx = -1; dx <= 1; dx++) {
      if (!inBounds(s, goal.x + dx, goal.y + dy)) continue;
      for (const p of placesOn(s, goal.x + dx, goal.y + dy)) if (all.cost.has(nodeOf(s, p)) && arrived(s, p, goal, 1.5) && canStopAt(s, p)) return null;
    }
  }
  let best: { tile: Pos; stand: Pos; score: number } | null = null;
  for (const key of Object.keys(holds)) {
    const tile = posOf(s, Number(key));
    if (!breakable(s, tile)) continue;
    const opens = DIRS8.some((d) => {
      const beyond = { x: tile.x + d.x, y: tile.y + d.y, z: tile.z };
      const kind = kindAt(s, beyond);
      return kind !== 'door' && canStopAt(s, beyond) && !all.cost.has(nodeOf(s, beyond));
    });
    if (!opens) continue;
    for (const d of DIRS8) {
      const stand = { x: tile.x + d.x, y: tile.y + d.y, z: tile.z };
      if (!inBounds(s, stand.x, stand.y) || !reachesToSearch(s, stand, tile)) continue;
      const here = samePos(stand, u.pos);
      const cost = here ? 0 : all.cost.get(nodeOf(s, stand));
      if (cost === undefined || (!here && (unitAt(s, stand) || !canStopAt(s, stand)))) continue;
      const score = cost / COST_ORTHO + farness(s, tile, goal) + holds[Number(key)]! * BREACH_HP_WEIGHT;
      if (!best || score < best.score - 1e-9) best = { tile, stand, score };
    }
  }
  return best && { tile: best.tile, stand: best.stand };
}

/**
 * One action toward `goal`: a walk, or in a siege, with no way there, the
 * way broken open (breachPlan): a blow at the door or barricade beside it,
 * or the walk up to it. False when it does neither.
 */
function advance(sim: Sim, u: Unit, goal: Pos, minGap: number): boolean {
  const plan = abilitiesOf(u).includes('smash') ? breachPlan(sim.s, u, goal, sim.ways) : null;
  if (!plan) return walk(sim, u, goal, minGap);
  if (!samePos(plan.stand, u.pos)) return walk(sim, u, plan.stand, 0);
  if (abilityBlocker(sim.s, u, 'smash')) return false;
  sim.perform(u, 'smash', { tile: plan.tile });
  return true;
}

/** The way, within one action, to a tile beside one of `soldiers` (the cheapest): for the Hinchado. */
function besideSoldier(s: GameState, u: Unit, soldiers: Unit[]): Pos[] | null {
  const near = soldiers.filter((t) => farness(s, u.pos, t.pos) <= stride(u));
  if (!near.length) return null;
  const reach = computeReach(s, u, costPerAp(u));
  let best: { path: Pos[]; cost: number } | null = null;
  for (const t of near) {
    for (const d of DIRS8) {
      const tile = { x: t.pos.x + d.x, y: t.pos.y + d.y, z: t.pos.z };
      if (!inBounds(s, tile.x, tile.y) || !inMeleeReach(s, tile, t.pos)) continue;
      const cost = reach.cost.get(nodeOf(s, tile));
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
  if (!cand || samePos(cand.pos, u.pos)) return false;
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
 * reach per action and a sight check per soldier. In a siege they know where
 * the living are: seeing nobody, they go for the nearest soldier, and with no
 * way to them, they break one (advance; Arturo, 18/10).
 */
export function takeZombieTurn(sim: Sim, u: Unit): void {
  const s = sim.s;
  const abilities = abilitiesOf(u);
  const usable = (a: AbilityId) => abilities.includes(a) && !abilityBlocker(s, u, a);
  const bite = abilities.find((a): a is AttackAbility => ABILITIES[a].target === 'melee' && isAttack(a));
  for (let guard = 0; guard < 6 && u.alive && u.ap > 0 && !sim.over; guard++) {
    // Any of the living: the squad, and the Saqueadores too (18/10 #47).
    const soldiers = foes(s, u);
    if (!soldiers.length) return;
    const seen = soldiers.filter((e) => canSee(s, u, e)).sort(byDistance(s, u.pos));

    if (abilities.includes('mindspin')) {
      // The Chillón: a scream at whoever it sees (one a turn for the whole horde), or a corpse raised.
      if (usable('mindspin') && sim.alienBudget.psi > 0) {
        // Only at whoever it sees itself: the dead don't tell each other (Arturo, 16/10).
        const victims = abilityTargets(s, u, 'mindspin')
          .filter((v) => seen.includes(v) && v.team === 'xcom')
          .sort((a, b) => b.rank - a.rank || psiChance(b, s) - psiChance(a, s) || a.id.localeCompare(b.id));
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
      // A bite costs an action and leaves the other (playtest 16/10 #21): only whoever it has beside it, the weakest first.
      // A step is another action: to the nearest it sees itself, not whoever another of the dead sees (#12).
      if (bite && usable(bite) && soldiers.some((e) => inMeleeReach(s, u.pos, e.pos))) {
        const beside = meleeTargets(s, u, bite).filter((o) => !o.path.length);
        if (beside.length) {
          const pick = beside.sort((a, b) => a.target.hp - b.target.hp || a.target.id.localeCompare(b.target.id))[0]!;
          sim.perform(u, bite, { melee: pick });
          continue;
        }
      }
      if (seen.length) {
        if (advance(sim, u, seen[0]!.pos, 1)) continue;
        return;
      }
    }

    // Nobody in sight: in a siege, for the nearest soldier, wherever they are.
    if (s.mission.siege) {
      const prey = [...soldiers].sort(byDistance(s, u.pos))[0]!;
      if (advance(sim, u, prey.pos, 1)) continue;
      return;
    }
    // Elsewhere, to the last noise heard, and having found nobody there, wandering.
    if (u.heard) {
      if (!arrived(s, u.pos, u.heard, 1.5) && advance(sim, u, u.heard, 0)) continue;
      sim.emit({ t: 'forgot', unit: u.id });
    }
    wander(sim, u);
    return;
  }
}
