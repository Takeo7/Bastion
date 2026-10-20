import { TEMPLATES, SOLDIER_MOBILITY, attackWeapon, commandsFor, loadedFor } from './content';
import { canStopAt } from './flights';
import { getUnit, posOf, unitAt } from './grid';
import { inMeleeReach, previewShot } from './aim';
import { COST_DIAG, COST_ORTHO, buildPath, computeReach, stepCost } from './path';
import type { Command } from './protocol';
import type { Rng } from './rng';
import { Sim, type CommandContext, type CommandResult } from './rules';
import type { GameEvent } from './events';
import type { GameState, Pos, RtUnit, Slot, Unit } from './types';

// The real-time mode, step 1 (docs/TIEMPO-REAL.md §6): the host's clock moves the mission on a tick at a time, and
// every unit does what its standing order asks of it, all at once. Everything is done through Sim, so the rules of
// the turns (doors, being seen, shots, bites, infection, the outcome) are the same; only the time each thing takes
// is new. The prototype: a skirmish alone, moving and shooting by clicking, the dead chasing and biting, the pause.

/** One tick of the host's clock (ms): some ten a second. */
export const RT_TICK = 100;
/** A soldier's step, one tile straight on (ms): the trot the client walks at. Diagonals, stairs and the slow take longer. */
export const RT_STEP = 500;
/** Aiming before each shot, per action the weapon takes by turns (ms): a sniper rifle, twice as long. */
export const RT_AIM = 700;
/** Reloading (ms). */
export const RT_RELOAD = 1800;
/** One of the dead winding up a blow, and getting its breath back after it (ms). */
export const RT_STRIKE = 600;
export const RT_RECOVER = 900;
/** Waiting for someone in the way to move on, or for a line of fire (ms); so many waits and it looks for another way. */
export const RT_WAIT = 300;
const WAITS = 3;
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

/** How long `u` aims before each shot (ms). */
export function rtAimMs(u: Unit): number {
  return RT_AIM * Math.max(1, attackWeapon(u, 'shoot').apCost);
}

/**
 * Who acts in real time: the living on the map. The dead of a group not yet up stand where they are, and (step 1)
 * so do those that don't bite (the Chillón, the Hinchado: their turns are for later).
 */
function acting(s: GameState, u: Unit): boolean {
  if (!u.alive || u.evacuated || u.captive) return false;
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
 * Moves the mission on `dt` ms (a tick of the host's clock): first what each one was doing and has finished (a shot
 * fired at the end of its aim, a blow at the end of its wind-up, a reload), then what each one does next. Nothing
 * while paused or over. The events, to send to everyone.
 */
export function realtimeStep(s: GameState, rng: Rng, connected: readonly Slot[], dt = RT_TICK): GameEvent[] {
  const rt = s.realtime;
  if (!rt || rt.paused || s.outcome) return [];
  const sim = new Sim(s, rng, connected);
  const now = rt.clock + dt;
  chases = CHASES_PER_TICK;
  sim.emit({ t: 'rtClock', clock: now });
  for (const u of [...s.units]) {
    if (sim.over) break;
    if (!acting(s, u)) continue;
    const me = rtOf(s, u);
    if (me.until > now) continue;
    finish(sim, u, me);
    if (!u.alive || sim.over) continue;
    if (u.team === 'xcom') soldier(sim, u, me, now);
    else dead(sim, u, me, now);
  }
  return sim.events;
}

/** What `u` was doing and has just finished: the shot at the end of its aim, the blow at the end of its wind-up, the reload. */
function finish(sim: Sim, u: Unit, me: RtUnit): void {
  const target = me.target ? getUnit(sim.s, me.target) : undefined;
  if (me.doing === 'aim' && target?.alive && previewShot(sim.s, u, target)) sim.attack(u, target, 'shoot');
  else if (me.doing === 'strike' && target?.alive && inMeleeReach(sim.s, u.pos, target.pos)) sim.attack(u, target, 'claw');
  else if (me.doing === 'reload') sim.emit({ t: 'reload', unit: u.id });
}

/** `u` starts doing `doing` until `until`, said only if it is something new. */
function start(sim: Sim, u: Unit, me: RtUnit, doing: RtUnit['doing'], until: number, target?: string): void {
  if (doing === me.doing && target === me.target && (doing === 'idle' || doing === 'wait')) {
    me.until = until;
    return;
  }
  sim.emit({ t: 'rtDoing', unit: u.id, doing, until, ...(target ? { target } : {}) });
}

/** One step along `me.path`, waiting for whoever is in the way (and, after a few waits, a new way); false if it can't. */
function step(sim: Sim, u: Unit, me: RtUnit, now: number, goal: () => Pos[] | null): boolean {
  me.path ??= goal() ?? undefined;
  const next = me.path?.[0];
  if (!next) return false;
  const there = unitAt(sim.s, next);
  // One of its own standing idle in the way steps aside, off the way (a squad starts packed together).
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

/** A soldier: its order, or nothing (step 1: one without orders stands; defending itself is step 2). */
function soldier(sim: Sim, u: Unit, me: RtUnit, now: number): void {
  const order = me.order;
  if (order.kind === 'move') {
    if (u.pos.x === order.to.x && u.pos.y === order.to.y && u.pos.z === order.to.z) return done(sim, u, me, now);
    if (!step(sim, u, me, now, () => route(sim.s, u, order.to))) done(sim, u, me, now);
    return;
  }
  if (order.kind === 'attack') {
    const target = getUnit(sim.s, order.target);
    if (!target?.alive) return done(sim, u, me, now);
    if (!loadedFor(u, 'shoot')) return start(sim, u, me, 'reload', now + RT_RELOAD);
    // In sight and in reach, it stops and aims; if not, it goes towards it until it has a line of fire.
    if (previewShot(sim.s, u, target)) {
      delete me.path;
      return start(sim, u, me, 'aim', now + rtAimMs(u), target.id);
    }
    const end = me.path?.[me.path.length - 1];
    if (end && !(Math.abs(end.x - target.pos.x) <= 1 && Math.abs(end.y - target.pos.y) <= 1)) delete me.path;
    if (!step(sim, u, me, now, () => chase(sim.s, u, target))) start(sim, u, me, 'wait', now + RT_WAIT, target.id);
    return;
  }
  start(sim, u, me, 'idle', now);
}

/** Its order done (or impossible): it stands. */
function done(sim: Sim, u: Unit, me: RtUnit, now: number): void {
  if (me.order.kind !== 'idle') sim.emit({ t: 'rtOrder', unit: u.id, order: { kind: 'idle' } });
  start(sim, u, me, 'idle', now);
}

/**
 * One of the dead, once its group is up: it goes for the nearest of the living (it knows where they are, step 1),
 * bites whoever is within reach, winds up first and gets its breath back after.
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
  if (!step(sim, u, me, now, () => chase(sim.s, u, prey))) start(sim, u, me, 'wait', now + RT_WAIT * 2);
}

function far(a: Unit, b: Unit): number {
  return Math.max(Math.abs(a.pos.x - b.pos.x), Math.abs(a.pos.y - b.pos.y));
}

/** The way to a free tile from which `u` can reach `prey` with a blow, the nearest: round the others, or through them. */
function chase(s: GameState, u: Unit, prey: Unit): Pos[] | null {
  return near(s, u, prey, computeReach(s, u, CHASE_COST)) ?? near(s, u, prey, computeReach(s, u, CHASE_COST, u.pos, () => true));
}

function near(s: GameState, u: Unit, prey: Unit, reach: ReturnType<typeof computeReach>): Pos[] | null {
  let best: Pos[] | null = null;
  for (let dy = -1; dy <= 1; dy++) {
    for (let dx = -1; dx <= 1; dx++) {
      if (!dx && !dy) continue;
      const p = { x: prey.pos.x + dx, y: prey.pos.y + dy, z: prey.pos.z };
      if (!canStopAt(s, p) || !inMeleeReach(s, p, prey.pos)) continue;
      const path = buildPath(s, u, reach, p);
      if (path && (!best || path.length < best.length)) best = path;
    }
  }
  return best;
}

/**
 * A player's order in real time (rules.ts executeCommand sends them here while the mission is played so): a unit's
 * standing order (`to`, go there; `target`, attack until down; neither, stand), or the pause. Anything else waits
 * for a later step (docs/TIEMPO-REAL.md §6).
 */
export function executeRealtimeCommand(s: GameState, cmd: Command, ctx: CommandContext): CommandResult {
  const rt = s.realtime!;
  const sim = new Sim(s, ctx.rng, ctx.connected);
  if (cmd.type === 'rtPause') {
    if (rt.paused !== cmd.paused) sim.emit({ t: 'rtPaused', paused: cmd.paused, slot: ctx.slot });
    return { ok: true, events: sim.events };
  }
  if (cmd.type !== 'rtOrder') return { ok: false, error: 'En tiempo real, eso aún no se puede.' };
  const u = getUnit(s, cmd.unit);
  if (!u || !u.alive || u.team !== 'xcom' || u.evacuated || u.captive) return { ok: false, error: 'Ese soldado no puede.' };
  if (!commandsFor(ctx.slot, u.owner, ctx.connected)) return { ok: false, error: 'Es de la escuadra del otro jugador.' };
  const me = rtOf(s, u);
  if (cmd.target) {
    const target = getUnit(s, cmd.target);
    if (!target?.alive || target.team === 'xcom') return { ok: false, error: 'Ahí no hay a quién disparar.' };
    sim.emit({ t: 'rtOrder', unit: u.id, order: { kind: 'attack', target: target.id } });
  } else if (cmd.to) {
    const to: Pos = { x: cmd.to.x, y: cmd.to.y, z: cmd.to.z ?? 0 };
    const way = canStopAt(s, to) ? route(s, u, to) : null;
    if (!way) return { ok: false, error: 'No se puede llegar ahí.' };
    sim.emit({ t: 'rtOrder', unit: u.id, order: { kind: 'move', to } });
    // The way found, kept for its first step (the host's own: not in the event).
    me.path = way;
  } else sim.emit({ t: 'rtOrder', unit: u.id, order: { kind: 'idle' } });
  // An aim at someone else is let go at once; a step under way, a reload and a blow are finished first.
  if (me.doing === 'aim' || me.doing === 'wait' || me.doing === 'idle') sim.emit({ t: 'rtDoing', unit: u.id, doing: 'idle', until: rt.clock });
  return { ok: true, events: sim.events };
}
