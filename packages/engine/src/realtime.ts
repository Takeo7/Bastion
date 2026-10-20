import { abilitiesOf, abilityBlocker, abilityTargets, allyTargets, canTargetTile, meleeTargets } from './abilities';
import { ABILITIES, TEMPLATES, SOLDIER_MOBILITY, attackWeapon, commandsFor, isAttack, loadedFor } from './content';
import { canStopAt } from './flights';
import { getUnit, posOf, unitAt } from './grid';
import { inMeleeReach } from './aim';
import { COST_DIAG, COST_ORTHO, buildPath, computeReach, stepCost } from './path';
import type { Command } from './protocol';
import type { Rng } from './rng';
import { Sim, type CommandContext, type CommandResult } from './rules';
import type { GameEvent } from './events';
import type { AbilityId, AttackAbility, GameState, Pos, RtOrder, RtUnit, Slot, Unit } from './types';

// The real-time mode (docs/TIEMPO-REAL.md): the host's clock moves the mission on a tick at a time, and every unit
// does what its standing order asks of it, all at once. Everything is done through Sim, so the rules of the turns
// (doors, being seen, shots, bites, infection, abilities, the outcome) are the same; only the time each thing takes
// is new. What lasts a turn by turns (cover, smoke, a stun, an ability's cooldown) lasts a round (RT_ROUND).

/** One tick of the host's clock (ms): some ten a second. */
export const RT_TICK = 100;
/** A soldier's step, one tile straight on (ms): the trot the client walks at. Diagonals, stairs and the slow take longer. */
export const RT_STEP = 500;
/** Aiming before each shot, per action the weapon takes by turns (ms): a sniper rifle, twice as long. */
export const RT_AIM = 700;
/** Reloading (ms). */
export const RT_RELOAD = 1800;
/** Winding up a blow, and (the dead) getting its breath back after it (ms). */
export const RT_STRIKE = 600;
export const RT_RECOVER = 900;
/** Waiting for someone in the way to move on, or for a line of fire (ms); so many waits and it looks for another way. */
export const RT_WAIT = 300;
const WAITS = 3;
/** A round (ms): what lasts a turn by turns lasts one (events.ts rtRound), an ability's cooldown so many. */
export const RT_ROUND = 6000;
/** The clock while someone chooses what an ability is aimed at: very slow, time to decide (Arturo, 20/10). */
export const RT_SLOW = 0.1;
/** What getting an ability ready takes before it happens (ms); an attack's is its aim (rtAimMs), a blow's RT_STRIKE. */
const RT_ACT: Partial<Record<AbilityId, number>> = {
  reload: RT_RELOAD,
  hunker: 300,
  firstAid: 1500,
  patchUp: 1500,
  revival: 1500,
  restoration: 1500,
  searchRoom: 3000,
  search: 1500,
  openDoor: 400,
  closeDoor: 400,
  smash: 800,
  elbowRoom: RT_STRIKE,
  grenade: 900,
  launch: 900,
  missiles: 900,
  smoke: 900,
  flashbang: 900,
  stone: 700,
};
const RT_ACT_DEFAULT = 800;
/** Not in real time yet (docs/TIEMPO-REAL.md: the evacuation and the objectives come later; the rest are the turns' own). */
const NOT_YET = new Set<AbilityId>(['evac', 'hack', 'runAndGun', 'faceoff', 'killZone', 'suppression']);
/** The attacks kept up until the target is down; any other, once. */
const KEPT_UP = new Set<AbilityId>(['shoot', 'pistol', 'slash', 'stunLance', 'lightningHands']);
/** How far the dead look for a way to whom they chase (path cost: half-tiles). */
const CHASE_COST = 90;
/**
 * How many of the dead look for a new way in one tick: when a big group wakes up, the rest wait a tick or two (a
 * tick in which all of them did cost the host 100 ms, and the clock stuttered).
 */
const CHASES_PER_TICK = 4;
let chases = 0;

/** How long `u` takes over one step from where it stands to `to` (ms), by its pace and what the step costs. */
export function rtStepMs(s: GameState, u: Unit, to: Pos): number {
  const cost = stepCost(s, u.pos.x, u.pos.y, to.x - u.pos.x, to.y - u.pos.y) ?? COST_ORTHO;
  const pace = SOLDIER_MOBILITY / Math.max(1, TEMPLATES[u.template].mobility + u.mods.mobility);
  return Math.round(RT_STEP * pace * (cost / COST_ORTHO));
}

/** How long `u` aims before each shot with `ability` (ms): per action of its weapon; a special shot, half as long again. */
export function rtAimMs(u: Unit, ability: AttackAbility = 'shoot'): number {
  return Math.round(RT_AIM * Math.max(1, attackWeapon(u, ability).apCost) * (KEPT_UP.has(ability) ? 1 : 1.5));
}

/** What getting `ability` ready takes (ms). */
export function rtActMs(u: Unit, ability: AbilityId): number {
  if (isAttack(ability)) return ABILITIES[ability].target === 'melee' ? RT_STRIKE : rtAimMs(u, ability);
  return RT_ACT[ability] ?? RT_ACT_DEFAULT;
}

/** Why `u` can't use `ability` in real time now, or null: the turns' reasons (but actions), and what isn't here yet. */
export function rtBlocker(s: GameState, u: Unit, ability: AbilityId): string | null {
  if (NOT_YET.has(ability)) return 'En tiempo real, eso aún no.';
  const why = abilityBlocker(s, u, ability);
  // Out of rounds is no reason: it reloads first.
  if (why && isAttack(ability) && !loadedFor(u, ability) && u.mags !== 0) return null;
  return why;
}

/** Who acts in real time: the living on the map, not stunned. The dead of a group not yet up stand where they are, and so do those that don't bite (the Chillón, the Hinchado: for later). */
function acting(s: GameState, u: Unit): boolean {
  if (!u.alive || u.evacuated || u.captive || u.stunned) return false;
  if (u.team === 'xcom') return true;
  if (!TEMPLATES[u.template].abilities.includes('claw')) return false;
  const pod = u.podId ? s.pods.find((p) => p.id === u.podId) : undefined;
  return !pod || pod.active;
}

/**
 * The steps from where `u` stands to `to`: round whoever stands in the way now, or, if there is no way round (a
 * doorway someone stands in), through them: they will move on, or it waits (step).
 */
function route(s: GameState, u: Unit, to: Pos): Pos[] | null {
  // Searched only as far as a way there could go, detours and all (the whole map each time cost the host 10 ms an order).
  // Only when that finds none, the whole map (a long way round a block): never «no way» for a tile there is a way to.
  const maxCost = Math.max(60, Math.max(Math.abs(to.x - u.pos.x), Math.abs(to.y - u.pos.y)) * COST_DIAG * 3);
  const all = () => true;
  return (
    buildPath(s, u, computeReach(s, u, maxCost), to) ??
    buildPath(s, u, computeReach(s, u, maxCost, u.pos, all), to) ??
    buildPath(s, u, computeReach(s, u, 100_000, u.pos, all), to)
  );
}

/** Its record, made if it has none yet. */
function rtOf(s: GameState, u: Unit): RtUnit {
  return (s.realtime!.units[u.id] ??= { order: { kind: 'idle' }, doing: 'idle', until: s.realtime!.clock });
}

/**
 * Moves the mission on `dt` ms of the host's (a tick), at the clock's speed: a round gone by, if one is; then what
 * each one was doing and has finished (a shot at the end of its aim, a blow at the end of its wind-up, an ability got
 * ready), and what each one does next. Nothing while paused or over. The events, to send to everyone.
 */
export function realtimeStep(s: GameState, rng: Rng, connected: readonly Slot[], dt = RT_TICK): GameEvent[] {
  const rt = s.realtime;
  if (!rt || rt.paused || s.outcome) return [];
  const sim = new Sim(s, rng, connected);
  const now = rt.clock + Math.max(1, Math.round(dt * (rt.speed ?? 1)));
  chases = CHASES_PER_TICK;
  if (Math.floor(now / RT_ROUND) > Math.floor(rt.clock / RT_ROUND)) {
    // Bengalas and fires on their last round go out first, as at the turn's end (rules.ts).
    for (const l of [...(s.lights ?? [])]) if (l.turns !== undefined && l.turns <= 1) sim.emit({ t: 'lightOff', id: l.id });
    sim.emit({ t: 'rtRound' });
  }
  sim.emit({ t: 'rtClock', clock: now });
  for (const u of [...s.units]) {
    if (sim.over) break;
    if (!u.alive) continue;
    // Its own round (the stunned and the idle too): what lasts a turn on it, from when it began, not the clock's.
    const me = rtOf(s, u);
    me.round ??= now + RT_ROUND;
    if (now >= me.round) {
      sim.emit({ t: 'rtRound', unit: u.id });
      me.round += RT_ROUND;
    }
    if (!acting(s, u)) continue;
    if (me.until > now) continue;
    finish(sim, u, me, now);
    if (!u.alive || sim.over) continue;
    if (u.team === 'xcom') soldier(sim, u, me, now);
    else dead(sim, u, me, now);
  }
  return sim.events;
}

/** What `u` was doing and has just finished: the shot at the end of its aim, the blow at the end of its wind-up, the reload, the ability got ready. */
function finish(sim: Sim, u: Unit, me: RtUnit, now: number): void {
  const s = sim.s;
  const target = me.target ? getUnit(s, me.target) : undefined;
  const ability = me.ability;
  if (me.doing === 'reload') {
    sim.emit({ t: 'reload', unit: u.id });
    return;
  }
  if (u.team !== 'xcom') {
    // One of the dead: its bite.
    if (me.doing === 'strike' && target?.alive && inMeleeReach(s, u.pos, target.pos)) sim.attack(u, target, 'claw');
    return;
  }
  if (!ability) return;
  if (me.doing === 'aim' && isAttack(ability) && target?.alive && abilityTargets(s, u, ability).includes(target) && !abilityBlocker(s, u, ability)) {
    sim.perform(u, ability, { target });
    used(sim, u, me);
  } else if (me.doing === 'strike' && isAttack(ability)) {
    const option = target?.alive ? meleeTargets(s, u, ability).find((o) => o.target.id === target.id) : undefined;
    if (option && !abilityBlocker(s, u, ability)) {
      sim.perform(u, ability, { melee: option });
      used(sim, u, me);
    }
  } else if (me.doing === 'act' && me.order.kind === 'act' && me.order.ability === ability && ready(s, u, me.order)) {
    const order = me.order;
    sim.perform(u, ability, { ...(order.target ? { target: getUnit(s, order.target) } : {}), ...(order.tile ? { tile: order.tile } : {}) });
    // Under cover a whole round from now, as by turns until its next (its round starts again).
    if (ability === 'hunker') me.round = now + RT_ROUND;
    used(sim, u, me);
  }
}

/** Its order once used: a one-off one is done (the gun's attack and the watch go on). */
function used(sim: Sim, u: Unit, me: RtUnit): void {
  const o = me.order;
  if (o.kind === 'act' || (o.kind === 'attack' && !KEPT_UP.has(o.ability ?? 'shoot'))) sim.emit({ t: 'rtOrder', unit: u.id, order: { kind: 'idle' } });
}

/** Whether `u` can use its one-off ability from where it stands, now. */
function ready(s: GameState, u: Unit, order: Extract<RtOrder, { kind: 'act' }>): boolean {
  if (abilityBlocker(s, u, order.ability)) return false;
  const def = ABILITIES[order.ability];
  if (def.target === 'tile') return !!order.tile && canTargetTile(s, u, order.ability, order.tile);
  if (def.target === 'ally') {
    const ally = order.target ? getUnit(s, order.target) : undefined;
    return !!ally && allyTargets(s, u, order.ability as 'revival' | 'firstAid' | 'patchUp').includes(ally);
  }
  return true;
}

/** `u` starts doing `doing` until `until`, said only if it is something new. */
function start(sim: Sim, u: Unit, me: RtUnit, doing: RtUnit['doing'], until: number, target?: string, ability?: AbilityId): void {
  if (doing === me.doing && target === me.target && (doing === 'idle' || doing === 'wait')) {
    me.until = until;
    return;
  }
  sim.emit({ t: 'rtDoing', unit: u.id, doing, until, ...(target ? { target } : {}), ...(ability ? { ability } : {}) });
}

/** One step along `me.path`, waiting for whoever is in the way (and, after a few waits, a new way); false if it can't. */
function step(sim: Sim, u: Unit, me: RtUnit, now: number, goal: () => Pos[] | null): boolean {
  me.path ??= goal() ?? undefined;
  const next = me.path?.[0];
  if (!next) return false;
  const there = unitAt(sim.s, next);
  // One of its own standing idle in the way steps aside, off the way (a squad starts close together).
  if (there && there !== u && there.team === u.team && idle(sim.s, there) && makeWay(sim, there, me.path!, now)) {
    // Not into the tile while the other is still stepping out of it.
    start(sim, u, me, 'wait', now + RT_WAIT);
    return true;
  }
  if (there && there !== u) {
    me.waited = (me.waited ?? 0) + 1;
    if (me.waited > WAITS) {
      delete me.path;
      me.waited = 0;
    }
    start(sim, u, me, 'wait', now + RT_WAIT);
    return true;
  }
  const ms = rtStepMs(sim.s, u, next);
  const from = { ...u.pos };
  sim.moveAlong(u, [next], 0);
  me.waited = 0;
  // Stopped short (seen by a group, a trap): it goes on from where it is, by a new way.
  if (u.pos.x === from.x && u.pos.y === from.y && u.pos.z === from.z) {
    delete me.path;
    start(sim, u, me, 'wait', now + RT_WAIT);
    return true;
  }
  me.path!.shift();
  if (!me.path!.length) delete me.path;
  start(sim, u, me, 'walk', now + ms);
  return true;
}

/** Whether `u` stands with nothing to do (no order, nothing under way). */
function idle(s: GameState, u: Unit): boolean {
  const me = s.realtime!.units[u.id];
  return !me || (me.order.kind === 'idle' && (me.doing === 'idle' || me.until <= s.realtime!.clock));
}

/** `u` steps to a free tile beside it that is not on `way`, if there is one; whether it did. */
function makeWay(sim: Sim, u: Unit, way: Pos[], now: number): boolean {
  const s = sim.s;
  const on = (p: Pos) => way.some((q) => q.x === p.x && q.y === p.y && q.z === p.z);
  const reach = computeReach(s, u, 3);
  for (const [i] of [...reach.cost].sort((a, b) => a[1] - b[1])) {
    const path = buildPath(s, u, reach, posOf(s, i));
    const to = path?.[path.length - 1];
    if (!path || path.length !== 1 || !to || on(to) || unitAt(s, to)) continue;
    const ms = rtStepMs(s, u, to);
    sim.moveAlong(u, path, 0);
    sim.emit({ t: 'rtDoing', unit: u.id, doing: 'walk', until: now + ms });
    return true;
  }
  return false;
}

/** A soldier: its order, or nothing (one without orders stands: defending itself comes later, docs/TIEMPO-REAL.md §6). */
function soldier(sim: Sim, u: Unit, me: RtUnit, now: number): void {
  const order = me.order;
  switch (order.kind) {
    case 'move':
      if (u.pos.x === order.to.x && u.pos.y === order.to.y && u.pos.z === order.to.z) return done(sim, u, me, now);
      if (!step(sim, u, me, now, () => route(sim.s, u, order.to))) done(sim, u, me, now);
      return;
    case 'attack':
      return attack(sim, u, me, now, order.target, (order.ability ?? 'shoot') as AttackAbility);
    case 'watch':
      return watch(sim, u, me, now, !!order.melee);
    case 'act':
      return act(sim, u, me, now, order);
  }
  start(sim, u, me, 'idle', now);
}

/** Attacks `targetId` with `ability`: reloads if it must, gets within reach (a blow: alongside), then aims or winds up. */
function attack(sim: Sim, u: Unit, me: RtUnit, now: number, targetId: string, ability: AttackAbility): void {
  const s = sim.s;
  const target = getUnit(s, targetId);
  if (!target?.alive) return done(sim, u, me, now);
  if (!loadedFor(u, ability)) return u.mags === 0 ? done(sim, u, me, now) : start(sim, u, me, 'reload', now + RT_RELOAD);
  const why = abilityBlocker(s, u, ability);
  if (why) return done(sim, u, me, now);
  const melee = ABILITIES[ability].target === 'melee';
  const inReach = melee ? inMeleeReach(s, u.pos, target.pos) : abilityTargets(s, u, ability).includes(target);
  if (inReach) {
    delete me.path;
    return start(sim, u, me, melee ? 'strike' : 'aim', now + rtActMs(u, ability), target.id, ability);
  }
  // Out of reach or of sight: towards it until it isn't.
  approach(sim, u, me, now, target.pos, target.id);
}

/** Keeps watch where it stands: the nearest enemy that comes within reach, shot (or struck, with the blade). */
function watch(sim: Sim, u: Unit, me: RtUnit, now: number, melee: boolean): void {
  const s = sim.s;
  const ability: AttackAbility = melee ? 'slash' : 'shoot';
  if (!melee && !loadedFor(u, ability)) return u.mags === 0 ? start(sim, u, me, 'idle', now) : start(sim, u, me, 'reload', now + RT_RELOAD);
  const near = (melee ? meleeTargets(s, u, ability).map((o) => o.target) : abilityTargets(s, u, ability)).sort((a, b) => far(u, a) - far(u, b))[0];
  if (!near || abilityBlocker(s, u, ability)) return start(sim, u, me, 'wait', now + RT_WAIT);
  start(sim, u, me, melee ? 'strike' : 'aim', now + rtActMs(u, ability), near.id, ability);
}

/** A one-off ability: from where it can be used (getting there first, beside the ally, in throwing range), got ready, then done. */
function act(sim: Sim, u: Unit, me: RtUnit, now: number, order: Extract<RtOrder, { kind: 'act' }>): void {
  const s = sim.s;
  if (rtBlocker(s, u, order.ability) && ABILITIES[order.ability].target !== 'tile' && ABILITIES[order.ability].target !== 'ally') return done(sim, u, me, now);
  if (ready(s, u, order)) {
    delete me.path;
    return start(sim, u, me, 'act', now + rtActMs(u, order.ability), order.target, order.ability);
  }
  const ally = order.target ? getUnit(s, order.target) : undefined;
  const where = ally?.alive ? ally.pos : order.tile;
  if (!where) return done(sim, u, me, now);
  approach(sim, u, me, now, where, ally?.id);
}

/** A step towards `p` (beside it, where it can't be stood on); re-found when what it goes for has moved off the end. */
function approach(sim: Sim, u: Unit, me: RtUnit, now: number, p: Pos, target?: string): void {
  const end = me.path?.[me.path.length - 1];
  if (end && !(Math.abs(end.x - p.x) <= 1 && Math.abs(end.y - p.y) <= 1)) delete me.path;
  if (!step(sim, u, me, now, () => towards(sim.s, u, p))) {
    // Nowhere nearer: given up.
    if (!me.path) return done(sim, u, me, now);
    start(sim, u, me, 'wait', now + RT_WAIT, target);
  }
}

/** Its order done (or impossible): it stands. */
function done(sim: Sim, u: Unit, me: RtUnit, now: number): void {
  if (me.order.kind !== 'idle') sim.emit({ t: 'rtOrder', unit: u.id, order: { kind: 'idle' } });
  start(sim, u, me, 'idle', now);
}

/**
 * One of the dead, once its group is up: it goes for the nearest of the living (it knows where they are), bites
 * whoever is within reach, winds up first and gets its breath back after.
 */
function dead(sim: Sim, u: Unit, me: RtUnit, now: number): void {
  if (me.doing === 'strike') return start(sim, u, me, 'wait', now + RT_RECOVER);
  const living = sim.s.units.filter((o) => o.alive && o.team === 'xcom' && !o.evacuated && !o.captive);
  const near = living.filter((o) => o.pos.z === u.pos.z || Math.abs(o.pos.z - u.pos.z) <= 1).sort((a, b) => far(u, a) - far(u, b));
  const bite = near.find((o) => inMeleeReach(sim.s, u.pos, o.pos));
  if (bite) {
    delete me.path;
    return start(sim, u, me, 'strike', now + RT_STRIKE, bite.id);
  }
  const prey = near[0];
  if (!prey) return start(sim, u, me, 'idle', now);
  // A new way whenever its prey has moved off the end of the old one.
  const end = me.path?.[me.path.length - 1];
  if (end && !(Math.abs(end.x - prey.pos.x) <= 1 && Math.abs(end.y - prey.pos.y) <= 1)) delete me.path;
  if (!me.path && chases-- <= 0) return start(sim, u, me, 'wait', now + RT_TICK);
  if (!step(sim, u, me, now, () => chase(sim.s, u, prey.pos))) start(sim, u, me, 'wait', now + RT_WAIT * 2);
}

function far(a: Unit, b: Unit): number {
  return Math.max(Math.abs(a.pos.x - b.pos.x), Math.abs(a.pos.y - b.pos.y));
}

/** The way to `p` if it can be stood on, else to the nearest free tile beside it. */
function towards(s: GameState, u: Unit, p: Pos): Pos[] | null {
  if (canStopAt(s, p) && !unitAt(s, p)) return route(s, u, p);
  return chase(s, u, p);
}

/** The way to a free tile from which `u` can reach `p` with a blow, the nearest: round the others, or through them. */
function chase(s: GameState, u: Unit, p: Pos): Pos[] | null {
  return beside(s, u, p, computeReach(s, u, CHASE_COST)) ?? beside(s, u, p, computeReach(s, u, CHASE_COST, u.pos, () => true));
}

function beside(s: GameState, u: Unit, at: Pos, reach: ReturnType<typeof computeReach>): Pos[] | null {
  let best: Pos[] | null = null;
  for (let dy = -1; dy <= 1; dy++) {
    for (let dx = -1; dx <= 1; dx++) {
      if (!dx && !dy) continue;
      const p = { x: at.x + dx, y: at.y + dy, z: at.z };
      if (!canStopAt(s, p) || !inMeleeReach(s, p, at)) continue;
      const path = buildPath(s, u, reach, p);
      if (path && (!best || path.length < best.length)) best = path;
    }
  }
  return best;
}

/**
 * A player's order in real time (rules.ts executeCommand sends them here while the mission is played so): a unit's
 * standing order (`to`, go there; `target`, attack it, or with `ability` use that on it; `ability` alone or with
 * `tile`, use it; nothing, stand), the pause, or the slow clock while an ability is aimed.
 */
export function executeRealtimeCommand(s: GameState, cmd: Command, ctx: CommandContext): CommandResult {
  const rt = s.realtime!;
  const sim = new Sim(s, ctx.rng, ctx.connected);
  if (cmd.type === 'rtPause') {
    if (rt.paused !== cmd.paused) sim.emit({ t: 'rtPaused', paused: cmd.paused, slot: ctx.slot });
    return { ok: true, events: sim.events };
  }
  if (cmd.type === 'rtSlow') {
    const speed = cmd.slow ? RT_SLOW : 1;
    if ((rt.speed ?? 1) !== speed) sim.emit({ t: 'rtSpeed', speed });
    return { ok: true, events: sim.events };
  }
  if (cmd.type !== 'rtOrder') return { ok: false, error: 'En tiempo real, eso aún no se puede.' };
  const u = getUnit(s, cmd.unit);
  if (!u || !u.alive || u.team !== 'xcom' || u.evacuated || u.captive) return { ok: false, error: 'Ese soldado no puede.' };
  if (!commandsFor(ctx.slot, u.owner, ctx.connected)) return { ok: false, error: 'Es de la escuadra del otro jugador.' };
  const me = rtOf(s, u);
  const order = orderFor(s, u, cmd);
  if (typeof order === 'string') return { ok: false, error: order };
  sim.emit({ t: 'rtOrder', unit: u.id, order: order.order });
  // The way found, kept for its first step (the host's own: not in the event).
  if (order.way) me.path = order.way;
  // An aim at someone else is let go at once; a step under way, a reload and a blow are finished first.
  if (me.doing === 'aim' || me.doing === 'wait' || me.doing === 'idle' || me.doing === 'act') sim.emit({ t: 'rtDoing', unit: u.id, doing: 'idle', until: rt.clock });
  return { ok: true, events: sim.events };
}

/** The standing order a player's command makes, or why it can't be given. */
function orderFor(s: GameState, u: Unit, cmd: Extract<Command, { type: 'rtOrder' }>): { order: RtOrder; way?: Pos[] } | string {
  const ability = cmd.ability;
  if (ability) {
    if (!abilitiesOf(u).includes(ability)) return 'No tiene esa habilidad.';
    const why = rtBlocker(s, u, ability);
    const def = ABILITIES[ability];
    // Where it must go first (an ally, a tile out of reach), what stops it now may not then.
    if (why && def.target !== 'tile' && def.target !== 'ally') return why;
    if (ability === 'overwatch' || ability === 'overwatchMelee') return { order: { kind: 'watch', ...(ability === 'overwatchMelee' ? { melee: true } : {}) } };
    if (isAttack(ability) && (def.target === 'enemy' || def.target === 'melee')) {
      const target = cmd.target ? getUnit(s, cmd.target) : undefined;
      if (!target?.alive || target.team === 'xcom') return 'Ahí no hay a quién atacar.';
      return { order: { kind: 'attack', target: target.id, ability } };
    }
    if (def.target === 'ally') {
      const ally = cmd.target ? getUnit(s, cmd.target) : undefined;
      if (!ally?.alive || ally.team !== u.team) return 'Elige a un compañero.';
      return { order: { kind: 'act', ability, target: ally.id } };
    }
    if (def.target === 'tile') {
      if (!cmd.tile) return 'Elige una casilla.';
      return { order: { kind: 'act', ability, tile: { x: cmd.tile.x, y: cmd.tile.y, z: cmd.tile.z ?? 0 } } };
    }
    if (def.target === 'self') return { order: { kind: 'act', ability } };
    return 'En tiempo real, eso aún no.';
  }
  if (cmd.target) {
    const target = getUnit(s, cmd.target);
    if (!target?.alive || target.team === 'xcom') return 'Ahí no hay a quién disparar.';
    return { order: { kind: 'attack', target: target.id } };
  }
  if (cmd.to) {
    const to: Pos = { x: cmd.to.x, y: cmd.to.y, z: cmd.to.z ?? 0 };
    const way = canStopAt(s, to) ? route(s, u, to) : null;
    if (!way) return 'No se puede llegar ahí.';
    return { order: { kind: 'move', to }, way };
  }
  return { order: { kind: 'idle' } };
}
