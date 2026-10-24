/**
 * Writes engine-cs/golden/step2/*.json: whole missions played by the TypeScript engine, for
 * engine-cs/tests/Step2GoldenTests.cs to replay in C# (docs/UNITY.md §3, hito 1 paso 2).
 *
 *   npx tsx engine-cs/golden/record-step2.ts
 *
 * A simple bot plays both seats with nothing but the engine's own functions: it shoots the best target it has, strikes
 * whoever is alongside, throws a grenade at a crowd, searches rooms and boxes, picks things up, uses medicines, goes for
 * the objective and the bus, keeps watch or takes cover, and ends the turn (which runs the enemy's). One Rng for the
 * commands, kept the whole mission, as the server does; the bot's own choices draw on another, never on that one.
 *
 * Each file holds the mission's state after createMission, the commands' Rng seed, and for every operation (a command,
 * or syncTurn, runOutOfTime, callAbandon, answerAbandon): what it was, its result (ok or the error), its events, the
 * Rng's state after it, and how the state changed (`delta`, see stateDelta), from which the test rebuilds the state
 * TypeScript had after each operation and compares it with C#'s.
 */
import { mkdirSync, writeFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';
import { abilitiesOf, abilityBlocker, abilityTargets, blastTiles, canHackFrom, canTargetTile, doorTiles, inEvacZone, meleeTargets } from '../../packages/engine/src/abilities';
import { previewShot } from '../../packages/engine/src/aim';
import { ABILITIES, commandsFor, isAttack } from '../../packages/engine/src/content';
import type { GameEvent } from '../../packages/engine/src/events';
import { combatants, distance, getUnit, samePos } from '../../packages/engine/src/grid';
import { packSlots, searchableTiles, searchSpot, wasSearched } from '../../packages/engine/src/loot';
import { buildPath, computeReach, costPerAp, destinations, maxMoveCost } from '../../packages/engine/src/path';
import type { Command } from '../../packages/engine/src/protocol';
import { Rng } from '../../packages/engine/src/rng';
import { answerAbandon, beltable, callAbandon, evacWins, executeCommand, runOutOfTime, syncTurn, wearable } from '../../packages/engine/src/rules';
import { createMission, makeUnit, type MissionOptions } from '../../packages/engine/src/setup';
import { lightTiles } from '../../packages/engine/src/los';
import { missionLines, NO_GROWTH, NO_SKILLS } from '../../packages/engine/src/skills';
import { SIEGE, ZERO_MODS } from '../../packages/engine/src/content';
import { nodeOf, tileIndex } from '../../packages/engine/src/grid';
import { addUnit, stateFrom, withLevels } from '../../packages/engine/test/helpers';
import type { AttackAbility, GameState, Pod, Pos, Slot, Unit } from '../../packages/engine/src/types';

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, 'step2');
mkdirSync(out, { recursive: true });

// ------------------------------------------------------------------- the state's changes

/** JSON with its keys sorted, to tell whether two values are the same. */
function canon(v: unknown): string {
  if (v === null || v === undefined) return 'null';
  if (Array.isArray(v)) return `[${v.map(canon).join(',')}]`;
  if (typeof v === 'object') {
    const o = v as Record<string, unknown>;
    return `{${Object.keys(o)
      .filter((k) => o[k] !== undefined && o[k] !== null)
      .sort()
      .map((k) => `${JSON.stringify(k)}:${canon(o[k])}`)
      .join(',')}}`;
  }
  return JSON.stringify(v);
}

/** Arrays of numbers or strings (or null) kept as changes by index: `{ $sparse: [i, v, i, v…] }` when the length stays. */
const SPARSE = new Set(['tiles', 'elev', 'ground', 'decor', 'storeys']);

function sparse(prev: unknown, now: unknown): unknown {
  if (!Array.isArray(prev) || !Array.isArray(now) || prev.length !== now.length) return now ?? null;
  const pairs: unknown[] = [];
  for (let i = 0; i < now.length; i++) if (prev[i] !== now[i]) pairs.push(i, now[i] ?? null);
  return { $sparse: pairs };
}

/**
 * How `now` differs from `prev` (both plain JSON states), key by key: a key that changed, whole (null: gone); the
 * big arrays by index (SPARSE); the storeys level by level, their tiles and decor by index; the units by index, field
 * by field (`$new`: a unit that wasn't there).
 */
function stateDelta(prev: Record<string, any>, now: Record<string, any>): Record<string, unknown> {
  const d: Record<string, unknown> = {};
  const keys = new Set([...Object.keys(prev), ...Object.keys(now)]);
  for (const k of keys) {
    const a = prev[k];
    const b = now[k];
    if (k === 'units') {
      const units: Record<string, unknown> = {};
      for (let i = 0; i < b.length; i++) {
        if (i >= a.length) {
          units[i] = { $new: b[i] };
          continue;
        }
        const fields: Record<string, unknown> = {};
        for (const f of new Set([...Object.keys(a[i]), ...Object.keys(b[i])])) if (canon(a[i][f]) !== canon(b[i][f])) fields[f] = b[i][f] ?? null;
        if (Object.keys(fields).length) units[i] = fields;
      }
      if (Object.keys(units).length) d.units = units;
      continue;
    }
    if (canon(a) === canon(b)) continue;
    if (SPARSE.has(k)) d[k] = sparse(a, b);
    else if (k === 'levels' && Array.isArray(a) && Array.isArray(b) && a.length === b.length) {
      const levels: Record<string, unknown> = {};
      b.forEach((level: Record<string, unknown>, i: number) => {
        const was = a[i] as Record<string, unknown>;
        const change: Record<string, unknown> = {};
        if (canon(was.tiles) !== canon(level.tiles)) change.tiles = sparse(was.tiles, level.tiles);
        if (canon(was.decor) !== canon(level.decor)) change.decor = sparse(was.decor, level.decor);
        if (Object.keys(change).length) levels[i] = change;
      });
      d.levels = { $levels: levels };
    } else d[k] = b ?? null;
  }
  return d;
}

const json = <T>(x: T): T => JSON.parse(JSON.stringify(x)) as T;

// ------------------------------------------------------------------- the bot

const near = (a: Pos, b: Pos) => distance(a, b);

/** Who the squad fights: the enemies alive, able to be fought. */
function enemiesOf(s: GameState): Unit[] {
  return s.units.filter((u) => u.alive && u.team === 'alien' && !u.captive);
}

/** Where `u` should head for: the objective, the bus, or the nearest enemy. */
function goalOf(s: GameState, u: Unit, leaving: boolean): Pos | null {
  const m = s.mission;
  const evac = m.evacZone.length ? m.evacZone.map((z) => ({ x: z.x, y: z.y, z: 0 })).sort((a, b) => near(u.pos, a) - near(u.pos, b))[0]! : null;
  if (leaving && evac) return evac;
  if (m.kind === 'recovery' && m.item) {
    if (m.item.carrier === u.id && evac) return evac;
    if (!m.item.carrier && m.item.pos) return { x: m.item.pos.x, y: m.item.pos.y, z: m.item.pos.z ?? 0 };
  }
  if (m.kind === 'hack' && m.terminal && !m.objectiveDone) return m.terminal;
  if (m.kind === 'rescue' && m.objectiveUnit) {
    const vip = getUnit(s, m.objectiveUnit);
    if (vip?.alive && vip.captive) return vip.pos;
    if (vip?.alive && evac && (vip.id === u.id || vip.owner === u.owner)) return evac;
  }
  if (m.kind === 'sabotage' && m.objectiveUnit) {
    const relay = getUnit(s, m.objectiveUnit);
    if (relay?.alive) return relay.pos;
  }
  if (m.kind === 'scavenge') {
    // The nearest loot box not yet opened, else the bus.
    const boxes: Pos[] = [];
    for (let y = 0; y < s.height; y++) for (let x = 0; x < s.width; x++) {
      const p = { x, y, z: 0 };
      if (searchSpot(s, p) && !wasSearched(s, p)) boxes.push(p);
    }
    boxes.sort((a, b) => near(u.pos, a) - near(u.pos, b) || a.y - b.y || a.x - b.x);
    if (boxes[0]) return boxes[0];
    if (evac) return evac;
  }
  const foes = enemiesOf(s).filter((e) => !e.objective).sort((a, b) => near(u.pos, a.pos) - near(u.pos, b.pos) || a.id.localeCompare(b.id));
  return foes[0]?.pos ?? evac;
}

/** One move toward `goal`: the reachable place nearest it (one action, or both now and then). */
function moveToward(s: GameState, u: Unit, goal: Pos, bot: Rng): Command | null {
  const dash = u.ap >= 2 && bot.next() < 0.3;
  const reach = computeReach(s, u, dash ? maxMoveCost(u) : costPerAp(u));
  let best: { pos: Pos; d: number; cost: number } | null = null;
  for (const o of destinations(s, u, reach)) {
    const d = near(o.pos, goal) + 3 * Math.abs(o.pos.z - (goal.z ?? 0));
    if (!best || d < best.d - 1e-9 || (Math.abs(d - best.d) < 1e-9 && o.cost < best.cost)) best = { pos: o.pos, d, cost: o.cost };
  }
  const here = near(u.pos, goal) + 3 * Math.abs(u.pos.z - (goal.z ?? 0));
  if (!best || best.d >= here - 0.5) return null;
  if (!buildPath(s, u, reach, best.pos)) return null;
  return { type: 'move', unit: u.id, to: best.pos };
}

/** Any ability `u` can use now, on a target it allows: the rare ones get their turn too. */
function tryAny(s: GameState, u: Unit, bot: Rng): Command | null {
  const list = abilitiesOf(u).filter((a) => abilityBlocker(s, u, a) === null && a !== 'hack' && a !== 'evac');
  if (!list.length) return null;
  const start = bot.int(list.length);
  for (let k = 0; k < list.length; k++) {
    const a = list[(start + k) % list.length]!;
    const def = ABILITIES[a];
    if (def.target === 'enemy' || def.target === 'melee' || def.target === 'ally' || def.target === 'corpse') {
      const targets = abilityTargets(s, u, a);
      if (targets.length) return { type: 'ability', unit: u.id, ability: a, target: targets[bot.int(targets.length)]!.id };
      continue;
    }
    if (def.target === 'tile') {
      let tiles: Pos[] = [];
      if (a === 'search') tiles = searchableTiles(s, u);
      else if (a === 'openDoor') tiles = doorTiles(s, u, 'door');
      else if (a === 'closeDoor') tiles = doorTiles(s, u, 'doorOpen');
      else if (a === 'smash') continue;
      else {
        const squad = (p: Pos) => s.units.some((o) => o.alive && o.team === 'xcom' && samePos(o.pos, p));
        tiles = enemiesOf(s)
          .map((o) => o.pos)
          .filter((p) => canTargetTile(s, u, a, p) && (a === 'smoke' || a === 'stone' || !blastTiles(s, a, p, u).some(squad)));
      }
      if (tiles.length) return { type: 'ability', unit: u.id, ability: a, tile: { ...tiles[bot.int(tiles.length)]! } };
      continue;
    }
    if (a === 'runAndGun' && u.ap > 1) continue;
    return { type: 'ability', unit: u.id, ability: a };
  }
  return null;
}

/** The arena's soldiers hold where they are (a siege): no walking out to the dead. */
let stay = false;

/** What `u` does now, or null to leave it be this turn. `hurry`: time is short, the bus leaves with whoever is in it. */
function choose(s: GameState, u: Unit, bot: Rng, leaving: boolean, hurry = false): Command | null {
  const usable = (a: string) => abilityBlocker(s, u, a as never) === null;
  const ability = (a: string, extra: Partial<Extract<Command, { type: 'ability' }>> = {}): Command => ({ type: 'ability', unit: u.id, ability: a as never, ...extra });
  // On the bus with the job done: home.
  const aboard = combatants(s, 'xcom').every((o) => o.automaton || inEvacZone(s, o.pos));
  if (leaving && s.mission.kind !== 'elimination' && inEvacZone(s, u.pos) && !s.evacCall && (aboard || hurry)) return { type: 'evac', unit: u.id };
  // What lies on its tile: picked up.
  const left = (s.dropped ?? []).find((d) => samePos(d.pos, u.pos));
  if (left && (left.loot.mags || (u.backpack?.length ?? 0) < packSlots(u))) return { type: 'item', unit: u.id, action: 'take', id: left.id };
  // The backpack: a medicine when hurt, a vest, a belt thing, a weapon now and then; a full one drops something.
  const pack = u.backpack ?? [];
  for (let i = 0; i < pack.length; i++) {
    const l = pack[i]!;
    if (l.medicines && (u.hp < u.maxHp || u.infected)) return { type: 'item', unit: u.id, action: 'use', index: i };
    if (l.item && wearable(l.item) && !u.worn?.length) return { type: 'item', unit: u.id, action: 'equip', index: i };
    if (l.item && beltable(l.item) && bot.next() < 0.5) return { type: 'item', unit: u.id, action: 'equip', index: i };
    if (l.weapon && u.arms && bot.next() < 0.4) return { type: 'item', unit: u.id, action: 'equip', index: i };
  }
  if (pack.length >= packSlots(u) && bot.next() < 0.3) return { type: 'item', unit: u.id, action: 'drop', index: 0 };
  // A partner beside it, hurt, with a medicine to spare: given.
  const mate = s.units.find((o) => o.alive && o.team === 'xcom' && o.id !== u.id && !o.captive && !o.objective && Math.max(Math.abs(o.pos.x - u.pos.x), Math.abs(o.pos.y - u.pos.y)) === 1 && o.pos.z === u.pos.z);
  const medicine = pack.findIndex((l) => l.medicines);
  if (mate && medicine >= 0 && mate.hp < mate.maxHp && bot.next() < 0.5) return { type: 'item', unit: u.id, action: bot.next() < 0.5 ? 'use' : 'give', index: medicine, to: mate.id };

  if (mate && pack.length && (mate.backpack?.length ?? 0) < packSlots(mate) && bot.next() < 0.1) return { type: 'item', unit: u.id, action: 'give', index: pack.length - 1, to: mate.id };
  if (pack.length && bot.next() < 0.04) return { type: 'item', unit: u.id, action: 'drop', index: pack.length - 1 };
  // Any ability now and then, the rare ones too.
  if (bot.next() < 0.25) {
    const any = tryAny(s, u, bot);
    if (any) return any;
  }

  const abilities = abilitiesOf(u);
  // Out of rounds: reload.
  if (u.ammo <= 0 && usable('reload')) return ability('reload');
  // The objective's terminal, beside it.
  if (s.mission.terminal && !s.mission.objectiveDone && canHackFrom(s, u) && usable('hack')) return ability('hack');
  // A blade at whoever is alongside, now and then.
  for (const blade of ['slash', 'elbowRoom'] as const) {
    if (!abilities.includes(blade) || !usable(blade)) continue;
    const options = blade === 'slash' ? meleeTargets(s, u, 'slash') : [];
    if (blade === 'elbowRoom' && bot.next() < 0.5) return ability('elbowRoom');
    if (options.length && bot.next() < 0.6) return ability('slash', { target: options[0]!.target.id });
  }
  // A grenade at a crowd.
  for (const throwIt of ['grenade', 'flashbang', 'smoke'] as const) {
    if (!abilities.includes(throwIt) || !usable(throwIt)) continue;
    for (const e of enemiesOf(s)) {
      if (!canTargetTile(s, u, throwIt, e.pos)) continue;
      const hit = blastTiles(s, throwIt, e.pos, u).map((p) => s.units.find((o) => o.alive && samePos(o.pos, p)));
      const foes = hit.filter((o) => o && o.team === 'alien').length;
      const friends = hit.filter((o) => o && o.team === 'xcom').length;
      if (throwIt === 'smoke' ? friends >= 1 && bot.next() < 0.2 : foes >= 2 && friends === 0) return ability(throwIt, { tile: { ...e.pos } });
    }
  }
  // The squad's special shots, now and then; else the best plain shot.
  const specials = abilities.filter((a) => isAttack(a) && a !== 'shoot' && a !== 'slash' && ABILITIES[a].target === 'enemy');
  for (const a of specials) {
    if (!usable(a) || bot.next() < 0.5) continue;
    if (a === 'faceoff') return ability(a);
    const targets = abilityTargets(s, u, a);
    if (targets.length) return ability(a, { target: targets[0]!.id });
  }
  if (abilities.includes('suppression') && usable('suppression') && bot.next() < 0.2) {
    const targets = abilityTargets(s, u, 'suppression');
    if (targets.length) return ability('suppression', { target: targets[0]!.id });
  }
  for (const shot of ['shoot', 'pistol'] as const) {
    if (!abilities.includes(shot) || !usable(shot)) continue;
    let best: { t: Unit; hit: number } | null = null;
    for (const t of abilityTargets(s, u, shot)) {
      const p = previewShot(s, u, t, { ability: shot as AttackAbility });
      if (p && (!best || p.hit > best.hit)) best = { t, hit: p.hit };
    }
    if (best && best.hit >= 25) return ability(shot, { target: best.t.id });
  }
  // A stone at an enemy in sight, now and then.
  if (abilities.includes('stone') && usable('stone') && bot.next() < 0.15) {
    const e = enemiesOf(s).find((o) => canTargetTile(s, u, 'stone', o.pos));
    if (e) return ability('stone', { tile: { ...e.pos } });
  }
  // A medkit on a hurt partner, or on itself.
  if (abilities.includes('firstAid') && usable('firstAid')) {
    const hurt = abilityTargets(s, u, 'firstAid')[0];
    if (hurt && bot.next() < 0.7) return ability('firstAid', { target: hurt.id });
  }
  // Searching: the room it stands in, a box beside it.
  if (abilities.includes('searchRoom') && usable('searchRoom') && bot.next() < 0.8) return ability('searchRoom');
  if (abilities.includes('search') && usable('search')) return ability('search', { tile: searchableTiles(s, u)[0]! });
  // A door beside it, now and then.
  if (usable('openDoor') && bot.next() < 0.1) return ability('openDoor', { tile: doorTiles(s, u, 'door')[0]! });
  if (usable('closeDoor') && bot.next() < 0.05) return ability('closeDoor', { tile: doorTiles(s, u, 'doorOpen')[0]! });
  // On toward what it is after.
  const goal = stay ? null : goalOf(s, u, leaving);
  if (goal && !(leaving && inEvacZone(s, u.pos))) {
    const move = moveToward(s, u, goal, bot);
    if (move) return move;
  }
  // Nothing to do: keep watch, or take cover.
  const watch = abilities.includes('overwatchMelee') && bot.next() < 0.2 ? 'overwatchMelee' : 'overwatch';
  if (usable(watch) && bot.next() < 0.6) return ability(watch);
  if (usable('hunker')) return ability('hunker');
  return null;
}

// ------------------------------------------------------------------- a whole mission

interface Plan {
  name: string;
  /** createMission's options; or `make`, a state built by hand (an arena). */
  options?: Omit<MissionOptions, 'rng' | 'slots'>;
  make?: () => GameState;
  seed: number;
  turns: number;
  /** Turns on which seat 1 is gone (from the first to the last): syncTurn as it goes and as it comes back. */
  away?: [number, number];
  /** Orders given first, as they are (an arena's opening), before the bot plays. */
  script?: [Slot, Command][];
  /** The squad holds where it is. */
  stay?: boolean;
  /** A turn on which seat 1's clock runs out instead of its ending the turn. */
  outOfTime?: number;
  /** A turn on which seat 0 asks to abandon (seat 1 says no), and one on which seat 1 asks and seat 0 says yes. */
  abandon?: [number, number];
}

function play(plan: Plan): void {
  const s = plan.make ? plan.make() : createMission({ ...plan.options, slots: [0, 1], rng: new Rng(plan.seed) } as MissionOptions);
  const initial = json(s);
  const rngSeed = plan.seed * 7919 + 13;
  const rng = new Rng(rngSeed);
  const bot = new Rng(plan.seed + 99);
  const ops: unknown[] = [];
  let prev = initial as unknown as Record<string, any>;
  let connected: Slot[] = [0, 1];
  skipped.clear();

  const record = (op: Record<string, unknown>, events: GameEvent[]) => {
    const now = json(s) as unknown as Record<string, any>;
    ops.push({ ...op, events: json(events), rng: rng.state, delta: stateDelta(prev, now) });
    prev = now;
  };
  const command = (slot: Slot, cmd: Command) => {
    const r = executeCommand(s, cmd, { slot, connected, rng });
    record({ op: 'command', slot, connected: [...connected], cmd, ok: r.ok, ...(r.ok ? {} : { error: r.error }) }, r.ok ? r.events : []);
    return r.ok;
  };
  const sync = () => record({ op: 'syncTurn', connected: [...connected] }, syncTurn(s, { connected, rng }));

  stay = !!plan.stay;
  for (const [slot, cmd] of plan.script ?? []) command(slot, cmd);
  let refusals = 0;
  let outOfTime = false;
  const orders = new Map<string, number>();
  for (let guard = 0; guard < 4000 && !s.outcome && s.turn <= plan.turns; guard++) {
    if (s.activeTeam !== 'xcom') break;
    const turn = s.turn;
    // Seat 1 goes away and comes back.
    if (plan.away && turn === plan.away[0] && connected.length === 2) {
      connected = [0];
      sync();
      continue;
    }
    if (plan.away && turn > plan.away[1] && connected.length === 1) {
      connected = [0, 1];
      sync();
      continue;
    }
    // Abandoning: asked and refused, then asked and accepted.
    if (plan.abandon && turn === plan.abandon[0] && !s.abandonCall && !ops.some((o) => (o as { op: string }).op === 'callAbandon')) {
      record({ op: 'callAbandon', slot: 0, connected: [...connected] }, callAbandon(s, 0, connected));
      record({ op: 'answerAbandon', slot: 1, accept: false }, answerAbandon(s, 1, false));
      continue;
    }
    if (plan.abandon && turn === plan.abandon[1]) {
      record({ op: 'callAbandon', slot: 1, connected: [...connected] }, callAbandon(s, 1, connected));
      record({ op: 'answerAbandon', slot: 0, accept: true }, answerAbandon(s, 0, true));
      break;
    }
    // An evacuation asked for: the partner answers (no, the first time).
    if (s.evacCall) {
      const other: Slot = s.evacCall.by === 0 ? 1 : 0;
      command(other, { type: 'evacAnswer', accept: refusals++ > 0 });
      continue;
    }
    // Seat 1's clock runs out, once.
    if (plan.outOfTime === turn && !outOfTime && connected.includes(1) && !s.ready[1]) {
      outOfTime = true;
      record({ op: 'runOutOfTime', slot: 1, connected: [...connected] }, runOutOfTime(s, 1, { connected, rng }));
      continue;
    }
    const holder: Slot = s.command !== undefined && connected.includes(s.command) ? s.command : connected[0]!;
    const partner: Slot = holder === 0 ? 1 : 0;
    // Now and then, an order out of turn (refused) or the command handed over.
    if (connected.includes(partner) && bot.next() < 0.03) {
      const theirs = combatants(s, 'xcom').find((o) => o.owner === partner && o.ap > 0);
      if (theirs) command(partner, { type: 'ability', unit: theirs.id, ability: 'hunker' });
      continue;
    }
    if (connected.includes(partner) && !s.ready[partner] && bot.next() < 0.03) {
      command(holder, { type: 'pass' });
      continue;
    }
    const m = s.mission;
    const vip = m.kind === 'rescue' && m.objectiveUnit ? getUnit(s, m.objectiveUnit) : undefined;
    const leaving = m.kind !== 'elimination' && (evacWins(s) || m.objectiveDone || !!m.item?.carrier || (!!vip && !vip.captive) || turn >= plan.turns - 3);
    const mine = combatants(s, 'xcom').filter((o) => o.ap > 0 && commandsFor(holder, o.owner, connected));
    const active = s.activeUnit ? mine.find((o) => o.id === s.activeUnit) : undefined;
    let acted = false;
    for (const u of active ? [active, ...mine.filter((o) => o !== active)] : mine) {
      if (skipped.get(u.id) === turn || (orders.get(`${u.id}@${turn}`) ?? 0) >= 8) continue;
      orders.set(`${u.id}@${turn}`, (orders.get(`${u.id}@${turn}`) ?? 0) + 1);
      const cmd = choose(s, u, bot, leaving, turn >= plan.turns - 2);
      if (!cmd) {
        skipped.set(u.id, turn);
        continue;
      }
      const apBefore = u.ap;
      const ok = command(holder, cmd);
      // Refused, or it changed nothing: that one is done for the turn.
      const live = getUnit(s, u.id);
      if (!ok || (live && live.alive && live.ap === apBefore && cmd.type !== 'item')) skipped.set(u.id, turn);
      acted = true;
      break;
    }
    if (acted) continue;
    // Nobody left to act for the holder: its turn is over.
    if (bot.next() < 0.05) command(holder, { type: 'endTurn', ready: false });
    command(holder, { type: 'endTurn', ready: true });
  }
  const file = { name: plan.name, options: plan.options ?? 'arena', seed: plan.seed, rngSeed, state: initial, ops };
  const text = JSON.stringify(file);
  writeFileSync(join(out, `${plan.name}.json`), text);
  const events = ops.reduce((n: number, o) => n + (o as { events: unknown[] }).events.length, 0);
  console.log(`${plan.name}: ${ops.length} operations, ${events} events, turn ${s.turn}, ${s.outcome ?? 'going on'} ${s.outcomeReason ?? ''} (${(text.length / 1e6).toFixed(2)} MB)`);
}

/** The bot's soldiers done for the turn: by id, the turn. */
const skipped = new Map<string, number>();


// ------------------------------------------------------------------- arenas
// States built by hand (packages/engine/test/helpers.ts) for what the generated missions hardly ever reach.

const P = (x: number, y: number, z = 0): Pos => ({ x, y, z });

/** A soldier with arms of its own, lines a step short of a bar, and `extra`. */
function soldier(s: GameState, id: string, x: number, y: number, owner: Slot, extra: Partial<Unit> & { z?: number } = {}): Unit {
  const start = { ...NO_SKILLS, shooting: 9, melee: 9, throwing: 9, medicine: 8, searching: 8, fitness: 9 };
  return addUnit(s, id, 'rookie', x, y, {
    owner,
    arms: { primary: 'assaultRifle', condition: 80 },
    ammo: 4,
    pistolAmmo: 6,
    mags: 3,
    lines: missionLines(start, { ...NO_GROWTH }, 0.2),
    ...extra,
  });
}

/** A pod of these units, awake or not. */
function podOf(s: GameState, id: string, units: Unit[], active = true, patrol: Pos[] = []): Pod {
  const p: Pod = { id, active, unitIds: units.map((u) => u.id), patrol, patrolIndex: 0 };
  for (const u of units) u.podId = id;
  s.pods.push(p);
  return p;
}

/** An enemy as createMission makes it: looking one way (setup.ts makeUnit), unlike addUnit's. */
function enemy(s: GameState, id: string, template: Unit['template'], x: number, y: number, z = 0): Unit {
  const u = makeUnit(id, template, id, null, { x, y, z }, null);
  s.units.push(u);
  return u;
}

/** Talents of every kind, a mind taken, a corpse to raise, the dead and the living together. */
function skillsArena(): GameState {
  const s = stateFrom([
    '..........................',
    '..h....H......h.....H.....',
    '..........................',
    '....h.......#####.........',
    '............#...#....h....',
    '..H.........#...+.........',
    '............#####.........',
    '.......h..................',
    '..........................',
    '....H.......h......H......',
    '..........................',
    '.......h.........h........',
    '..........................',
    '..........................',
  ]);
  s.mission.difficulty = 2;
  soldier(s, 's1', 8, 8, 0, { perks: ['suppression', 'rupture', 'holoTargeting', 'coveringFire', 'killZone', 'guardCrit'], mods: { ...ZERO_MODS, holoAim: 5, suppressAim: 10, gunAim: 50 } });
  soldier(s, 's2', 3, 10, 0, { perks: ['revivalProtocol', 'restoration', 'bridge', 'lightHands'], mods: { ...ZERO_MODS, aid: 10, heal: 1 }, charges: { restoration: 1, firstAid: 2 }, gear: ['firstAid'], hp: 3, infected: true, disoriented: 1, backpack: [{ medicines: 2 }, { item: 'nanoVest' }] });
  soldier(s, 's3', 4, 11, 1, { arms: { primary: 'pumpShotgun', melee: 'machete' }, perks: ['implacable', 'bladeMaster', 'secondWind', 'elbowRoom', 'bladeWall'], mods: { ...ZERO_MODS, slashDisorient: 1, meleeDamage: 2, meleeAim: 60 }, backpack: [{ weapon: 'huntingRifle', condition: 70, mods: ['stock'] }, { item: 'fragGrenade' }], stunned: true });
  soldier(s, 's4', 5, 9, 1, { arms: { primary: 'assaultRifle', condition: 25, mods: ['scope', 'silencer'] }, perks: ['patchUp', 'rupture', 'salvo', 'safeBlast', 'goodArm', 'runAndGun', 'shadowstep', 'faceoff', 'aimedShot', 'rapidFire', 'chainShot', 'lightningHands'], charges: { grenade: 3, patchUp: 1, flashbang: 1, smoke: 1 }, gear: ['fragGrenade', 'flashbang', 'smokeGrenade'], mods: { ...ZERO_MODS, care: 1, grenadeDamage: 1, grenadeRadius: 1, quiet: 1, stoneNoise: 4, gunAim: 50 } });
  const fallen = soldier(s, 's6', 10, 6, 0, { alive: false, hp: 0 });
  fallen.appearance = { primary: '#335577', secondary: '#aabbcc', visor: '#ff0000', head: 'helmet', build: 'normal' };
  const sectoid = enemy(s, 'a6', 'sectoid', 23, 6);
  const taken = soldier(s, 's5', 25, 0, 1, { team: 'alien', controlledBy: 'a6', controlTurns: 2 });
  const weak = enemy(s, 'a9', 'zombie', 5, 12);
  weak.hp = 1;
  podOf(s, 'p1', [enemy(s, 'a1', 'lancer', 20, 4), enemy(s, 'a2', 'trooper', 22, 8), enemy(s, 'a3', 'officer', 21, 11)]);
  podOf(s, 'p2', [enemy(s, 'a4', 'zombie', 16, 2), enemy(s, 'a5', 'zombie', 17, 12), sectoid, enemy(s, 'a7', 'bloater', 15, 9), enemy(s, 'a8', 'xenoid', 24, 12), weak, enemy(s, 'a11', 'mec', 14, 8), taken]);
  return s;
}

/** A recovery: the item, the bus and a patrol far off; one of the squad bitten, and no Cuarentena at home. */
function evacArena(): GameState {
  const s = stateFrom([
    '......................',
    '......................',
    '.....h........H.......',
    '......................',
    '..........#+##........',
    '..........#..#....h...',
    '..h.......#..#........',
    '..........####........',
    '......................',
    '.....H.........h......',
    '......................',
    '......................',
  ]);
  s.mission = { ...s.mission, kind: 'recovery', turnsLeft: 12, item: { pos: P(12, 5), carrier: null, evacuated: false }, evacZone: [{ x: 1, y: 1 }, { x: 2, y: 1 }, { x: 1, y: 2 }, { x: 2, y: 2 }], quarantine: false, difficulty: 1 };
  soldier(s, 's1', 3, 4, 0);
  soldier(s, 's2', 4, 5, 0, { infected: true });
  soldier(s, 's3', 3, 6, 1, { backpack: [{ medicines: 1 }, { food: 3 }, { supplies: 12 }] });
  soldier(s, 's4', 4, 7, 1, { packSlots: 4 });
  podOf(s, 'p1', [enemy(s, 'a1', 'zombie', 19, 2), enemy(s, 'a2', 'zombie', 20, 3)], false, [P(19, 2), P(19, 9)]);
  s.concealed = true;
  return s;
}

/** A radio to mend, hard: the alarm, the horde it brings, the clock. */
function hackArena(): GameState {
  const s = stateFrom([
    '..................',
    '.....h......H.....',
    '..................',
    '.......C..........',
    '..................',
    '..H.........h.....',
    '..................',
    '..................',
  ]);
  s.mission = { ...s.mission, kind: 'hack', turnsLeft: 8, terminal: P(7, 3), evacZone: [{ x: 0, y: 7 }, { x: 1, y: 7 }], hackBonus: -45, difficulty: 2, horde: ['zombie', 'zombie'] };
  soldier(s, 's1', 6, 4, 0);
  soldier(s, 's2', 8, 4, 1, { mods: { ...ZERO_MODS, hack: 10 } });
  soldier(s, 's3', 2, 6, 0);
  podOf(s, 'p1', [enemy(s, 'a1', 'trooper', 16, 1)], false, [P(16, 1), P(16, 6)]);
  s.concealed = true;
  return s;
}

/** A yard under siege: doors and barricades to break, traps, wire, a turret, waves by every side. */
function siegeArena(): GameState {
  const s = stateFrom([
    '................',
    '................',
    '..#####+#####...',
    '..#..........#..',
    '..#..........#..',
    '..#..........#..',
    '..+..........+..',
    '..#..........#..',
    '..#..........#..',
    '..####hh######..',
    '................',
    '................',
  ]);
  s.mission = { ...s.mission, siege: { hold: SIEGE.hold }, difficulty: 2 };
  s.breakables = { [tileIndex(s, 7, 2)]: SIEGE.door, [tileIndex(s, 2, 6)]: SIEGE.door, [tileIndex(s, 13, 6)]: SIEGE.door, [tileIndex(s, 6, 9)]: SIEGE.barricade, [tileIndex(s, 7, 9)]: SIEGE.barricade };
  s.traps = [
    { piece: 'b1', kind: 'spikes', node: nodeOf(s, P(7, 1)), charges: 2 },
    { piece: 'b2', kind: 'snare', node: nodeOf(s, P(1, 6)), charges: 1 },
    { piece: 'b3', kind: 'mine', node: nodeOf(s, P(6, 10)), charges: 1 },
    { piece: 'b4', kind: 'snare', node: nodeOf(s, P(14, 6)), charges: 1 },
  ];
  s.slow = [nodeOf(s, P(14, 6)), nodeOf(s, P(7, 10))];
  s.base = { pieces: [] };
  soldier(s, 's1', 6, 5, 0);
  soldier(s, 's2', 8, 5, 0);
  soldier(s, 's3', 6, 7, 1);
  soldier(s, 's4', 9, 7, 1);
  const turret = addUnit(s, 'tur-b9', 'turret', 7, 4, { owner: null, automaton: { piece: 'b9' }, ap: 0 });
  turret.ammo = 6;
  podOf(s, 'p1', [enemy(s, 'a1', 'mec', 7, 0), enemy(s, 'a2', 'mec', 0, 6), enemy(s, 'a3', 'mec', 15, 6), enemy(s, 'a4', 'mec', 6, 11)]);
  s.mission.reinforcements = [
    { id: 'r1', turn: 1, pos: null, units: ['zombie', 'zombie'], landed: false, horde: true, side: 0 },
    { id: 'r2', turn: 2, pos: null, units: ['zombie', 'xenoid'], landed: false, horde: true, side: 2 },
    { id: 'r3', turn: 3, pos: null, units: ['zombie'], landed: false, horde: true, side: 1 },
  ];
  return s;
}

/** A building of two storeys, the dead upstairs in a crowd: a grenade on its floor brings it down. */
function storeysArena(): GameState {
  const s = stateFrom([
    '................',
    '.##########.....',
    '.#S.......#.....',
    '.#........+.....',
    '.#........#.....',
    '.#........#.....',
    '.##########.....',
    '................',
  ]);
  withLevels(s, [
    [
      '                ',
      ' ########## ',
      ' #........# ',
      ' #........= ',
      ' #........# ',
      ' #........# ',
      ' ########## ',
      '                ',
    ].map((r) => r.padEnd(16, ' ')),
  ]);
  soldier(s, 's1', 3, 3, 0, { z: 1, charges: { grenade: 2 }, gear: ['fragGrenade'] });
  soldier(s, 's2', 12, 3, 0);
  soldier(s, 's3', 12, 5, 1, { charges: { grenade: 1 }, gear: ['fragGrenade'] });
  soldier(s, 's4', 13, 1, 1);
  podOf(s, 'p1', [enemy(s, 'a1', 'mec', 7, 4, 1), enemy(s, 'a2', 'xenoid', 8, 4, 1), enemy(s, 'a3', 'mec', 7, 5, 1), enemy(s, 'a4', 'zombie', 4, 5, 0)]);
  s.mission.item = { pos: P(7, 4, 1), carrier: null, evacuated: false };
  return s;
}

/** A rescue: the VIP waiting a few steps off, the bus behind. */
function rescueArena(): GameState {
  const s = stateFrom([
    '..................',
    '....h.......H.....',
    '..................',
    '.........#+#......',
    '.........#.#......',
    '..H......###......',
    '..................',
    '..................',
  ]);
  s.mission = { ...s.mission, kind: 'rescue', turnsLeft: 10, objectiveUnit: 'vip', evacZone: [{ x: 0, y: 6 }, { x: 1, y: 6 }, { x: 0, y: 7 }, { x: 1, y: 7 }], difficulty: 1 };
  soldier(s, 's1', 3, 3, 0);
  soldier(s, 's2', 4, 4, 1);
  addUnit(s, 'vip', 'vip', 10, 4, { owner: null, captive: true, ap: 0 });
  podOf(s, 'p1', [enemy(s, 'a1', 'zombie', 16, 1), enemy(s, 'a2', 'zombie', 17, 6)], false);
  return s;
}

/** A night with a Bengala burning out, the squad hidden. */
function nightArena(): GameState {
  const s = createMission({ map: { kind: 'generated', biome: 'wilds', seed: 12 }, slots: [0, 1], rng: new Rng(12), kind: 'elimination', night: true, difficulty: 1 });
  const at = s.units.find((u) => u.team === 'xcom')!.pos;
  (s.lights ??= []).push({ id: 'bengala1', tile: { ...at }, radius: 4, turns: 2, tiles: lightTiles(s, at, 4, 1) });
  return s;
}

const generated = (biome: 'city' | 'wilds' | 'facility', seed: number) => ({ kind: 'generated' as const, biome, seed });
const plans: Plan[] = [
  { name: 'city-3-elimination', options: { map: generated('city', 3), kind: 'elimination' }, seed: 3, turns: 25, outOfTime: 3 },
  { name: 'wilds-5-recovery', options: { map: generated('wilds', 5), kind: 'recovery' }, seed: 5, turns: 25 },
  { name: 'facility-2-hack', options: { map: generated('facility', 2), kind: 'hack' }, seed: 2, turns: 25, away: [4, 5] },
  { name: 'city-11-scavenge', options: { map: generated('city', 11), kind: 'scavenge' }, seed: 11, turns: 25 },
  { name: 'wilds-9-rescue', options: { map: generated('wilds', 9), kind: 'rescue' }, seed: 9, turns: 25 },
  { name: 'city-4-night', options: { map: generated('city', 4), kind: 'elimination', night: true }, seed: 4, turns: 25 },
  { name: 'torre', options: { map: { kind: 'handmade', id: 'torre' }, kind: 'elimination' }, seed: 21, turns: 25 },
  { name: 'city-6-hard-veterans', options: { map: generated('city', 6), kind: 'elimination', difficulty: 4, veterans: true }, seed: 6, turns: 25 },
  { name: 'facility-8-sabotage', options: { map: generated('facility', 8), kind: 'sabotage', difficulty: 3 }, seed: 8, turns: 25 },
  { name: 'bastion-siege', options: { map: { kind: 'handmade', id: 'bastion' }, difficulty: 3, veterans: true }, seed: 31, turns: 25 },
  {
    name: 'arena-skills',
    make: skillsArena,
    seed: 41,
    turns: 12,
    outOfTime: 2,
    script: [
      [0, { type: 'ability', unit: 's2', ability: 'revival', target: 's3' }],
      [0, { type: 'pass' }],
      [1, { type: 'ability', unit: 's3', ability: 'slash', target: 'a9' }],
      [1, { type: 'ability', unit: 's3', ability: 'overwatchMelee' }],
      [0, { type: 'ability', unit: 's1', ability: 'suppression', target: 'a3' }],
      [1, { type: 'ability', unit: 's4', ability: 'patchUp', target: 's4' }],
      [1, { type: 'ability', unit: 's4', ability: 'rupture', target: 'a11' }],
    ],
  },
  { name: 'arena-evac', make: evacArena, seed: 42, turns: 14 },
  { name: 'arena-hack', make: hackArena, seed: 43, turns: 10, away: [2, 3] },
  { name: 'arena-siege', make: siegeArena, seed: 44, turns: 14, stay: true },
  { name: 'arena-storeys', make: storeysArena, seed: 45, turns: 10, script: [[0, { type: 'ability', unit: 's1', ability: 'grenade', tile: { x: 7, y: 4, z: 1 } }]] },
  { name: 'arena-rescue', make: rescueArena, seed: 47, turns: 10 },
  { name: 'arena-night', make: nightArena, seed: 46, turns: 12, abandon: [2, 6] },
];

const only = process.argv[2];
for (const plan of plans) if (!only || plan.name === only) play(plan);
