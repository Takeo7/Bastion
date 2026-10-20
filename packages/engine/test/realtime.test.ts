import { describe, expect, it } from 'vitest';
import type { GameEvent } from '../src/events';
import { RT_AIM, RT_RECOVER, RT_ROUND, RT_SLOW, RT_STEP, RT_STRIKE, RT_TICK, executeRealtimeCommand, realtimeStep, rtStepMs } from '../src/realtime';
import { Rng } from '../src/rng';
import { executeCommand } from '../src/rules';
import { createMission } from '../src/setup';
import type { GameState } from '../src/types';
import { addUnit, stateFrom } from './helpers';

// docs/TIEMPO-REAL.md, step 1: the host's clock runs the mission a tick at a time; the players give standing orders.

const ctx = (slot: 0 | 1 = 0) => ({ slot, connected: [0, 1] as (0 | 1)[], rng: new Rng(7) });

function field(rows = Array.from({ length: 12 }, () => '.'.repeat(16))): GameState {
  const s = stateFrom(rows);
  s.realtime = { clock: 0, paused: false, units: {} };
  return s;
}

function order(s: GameState, cmd: Parameters<typeof executeRealtimeCommand>[1], slot: 0 | 1 = 0): void {
  const r = executeCommand(s, cmd, ctx(slot));
  if (!r.ok) throw new Error(r.error);
}

/** Runs the clock for `ms`, the events of it. */
function run(s: GameState, ms: number, rng = new Rng(3)): GameEvent[] {
  const out: GameEvent[] = [];
  for (let t = 0; t < ms; t += RT_TICK) out.push(...realtimeStep(s, rng, [0, 1]));
  return out;
}

describe('the real-time clock (realtime.ts)', () => {
  it('a soldier walks to where it was sent, a tile at a time, at its trot', () => {
    const s = field();
    const u = addUnit(s, 's1', 'assault', 2, 5);
    order(s, { type: 'rtOrder', unit: 's1', to: { x: 6, y: 5 } });
    expect(rtStepMs(s, u, { x: 3, y: 5, z: 0 })).toBe(RT_STEP);
    // Four steps of half a second: not there after a second and a half, there by two (and a tick).
    run(s, 1500);
    expect(u.pos.x).toBeLessThan(6);
    const events = run(s, 600);
    expect(u.pos).toEqual({ x: 6, y: 5, z: 0 });
    // Its order done, it stands.
    expect(s.realtime!.units.s1!.order).toEqual({ kind: 'idle' });
    expect(events.some((e) => e.t === 'rtOrder' && e.order.kind === 'idle')).toBe(true);
  });

  it('nothing moves while paused, and either player pauses it for both', () => {
    const s = field();
    const u = addUnit(s, 's1', 'assault', 2, 5);
    order(s, { type: 'rtOrder', unit: 's1', to: { x: 8, y: 5 } });
    order(s, { type: 'rtPause', paused: true }, 1);
    expect(run(s, 3000)).toEqual([]);
    expect(u.pos.x).toBe(2);
    expect(s.realtime!.clock).toBe(0);
    order(s, { type: 'rtPause', paused: false }, 0);
    run(s, 1000);
    expect(u.pos.x).toBeGreaterThan(2);
  });

  it('a soldier sent at one of the dead aims, shoots with the odds of the turns, and keeps at it until it is down', () => {
    const s = field();
    const u = addUnit(s, 's1', 'assault', 2, 5);
    u.mods.aim = 200;
    const z = addUnit(s, 'z1', 'zombie', 8, 5);
    // Another, asleep out of sight: the mission goes on once the first is down.
    addUnit(s, 'z2', 'zombie', 15, 0, { podId: 'p2' });
    s.pods.push({ id: 'p2', active: false, unitIds: ['z2'], patrol: [], patrolIndex: 0 });
    order(s, { type: 'rtOrder', unit: 's1', target: 'z1' });
    // No shot before its aim is done.
    expect(run(s, RT_AIM - RT_TICK).some((e) => e.t === 'shot')).toBe(false);
    const events = run(s, 8000);
    const shots = events.filter((e) => e.t === 'shot');
    expect(shots.length).toBeGreaterThan(0);
    expect(z.alive).toBe(false);
    expect(s.realtime!.units.s1!.order).toEqual({ kind: 'idle' });
    // Shots a whole aim apart, never quicker.
    const times: number[] = [];
    let clock = 0;
    for (const e of events) {
      if (e.t === 'rtClock') clock = e.clock;
      if (e.t === 'shot') times.push(clock);
    }
    for (let i = 1; i < times.length; i++) expect(times[i]! - times[i - 1]!).toBeGreaterThanOrEqual(RT_AIM);
    expect(u.alive).toBe(true);
  });

  it('sent at one it has no line of fire to, it goes round until it has, and then shoots', () => {
    const rows = Array.from({ length: 12 }, (_, y) => (y >= 2 && y <= 9 ? '.......#........' : '.'.repeat(16)));
    const s = field(rows);
    const u = addUnit(s, 's1', 'assault', 3, 5);
    u.mods.aim = 200;
    const z = addUnit(s, 'z1', 'zombie', 11, 5, { podId: 'p1' });
    s.pods.push({ id: 'p1', active: false, unitIds: ['z1'], patrol: [], patrolIndex: 0 });
    addUnit(s, 'z2', 'zombie', 15, 0, { podId: 'p2' });
    s.pods.push({ id: 'p2', active: false, unitIds: ['z2'], patrol: [], patrolIndex: 0 });
    order(s, { type: 'rtOrder', unit: 's1', target: 'z1' });
    const events = run(s, 20_000);
    expect(events.some((e) => e.t === 'moved' && e.unit === 's1')).toBe(true);
    expect(events.some((e) => e.t === 'shot' && e.unit === 's1')).toBe(true);
    expect(z.alive).toBe(false);
  });

  it('out of rounds, it reloads before the next shot', () => {
    const s = field();
    const u = addUnit(s, 's1', 'assault', 2, 5);
    u.mods.aim = -200;
    addUnit(s, 'z1', 'zombie', 9, 5, { hp: 99, maxHp: 99 });
    u.ammo = 1;
    order(s, { type: 'rtOrder', unit: 's1', target: 'z1' });
    const events = run(s, 6000);
    const kinds = events.filter((e) => e.t === 'shot' || e.t === 'reload').map((e) => e.t);
    expect(kinds.slice(0, 3)).toEqual(['shot', 'reload', 'shot']);
  });

  it('the dead of a group that is up come for the nearest of the living and bite, a breath between blows', () => {
    const s = field();
    const u = addUnit(s, 's1', 'assault', 2, 5, { hp: 50, maxHp: 50 });
    u.mods.defense = -200;
    const z = addUnit(s, 'z1', 'zombie', 10, 5, { podId: 'p1' });
    s.pods.push({ id: 'p1', active: true, unitIds: ['z1'], patrol: [], patrolIndex: 0 });
    run(s, 9000);
    expect(Math.max(Math.abs(z.pos.x - u.pos.x), Math.abs(z.pos.y - u.pos.y))).toBe(1);
    const events = run(s, 6000);
    const bites: number[] = [];
    let clock = 0;
    for (const e of events) {
      if (e.t === 'rtClock') clock = e.clock;
      if (e.t === 'shot' && e.unit === 'z1') bites.push(clock);
    }
    expect(bites.length).toBeGreaterThan(1);
    for (let i = 1; i < bites.length; i++) expect(bites[i]! - bites[i - 1]!).toBeGreaterThanOrEqual(RT_STRIKE + RT_RECOVER);
    expect(u.hp).toBeLessThan(50);
  });

  it("a group not yet up stands where it is; the dead never walk through anyone", () => {
    const s = field();
    addUnit(s, 's1', 'assault', 2, 5);
    const asleep = addUnit(s, 'z1', 'zombie', 12, 2, { podId: 'p1' });
    s.pods.push({ id: 'p1', active: false, unitIds: ['z1'], patrol: [], patrolIndex: 0 });
    const zs = ['z2', 'z3', 'z4'].map((id, i) => addUnit(s, id, 'zombie', 12, 6 + i, { podId: 'p2' }));
    s.pods.push({ id: 'p2', active: true, unitIds: ['z2', 'z3', 'z4'], patrol: [], patrolIndex: 0 });
    for (let t = 0; t < 12_000; t += RT_TICK) {
      realtimeStep(s, new Rng(t), [0, 1]);
      const at = new Set(s.units.filter((x) => x.alive).map((x) => `${x.pos.x},${x.pos.y}`));
      expect(at.size).toBe(s.units.filter((x) => x.alive).length);
    }
    expect(asleep.pos).toEqual({ x: 12, y: 2, z: 0 });
    expect(zs.every((z) => !z.alive || Math.abs(z.pos.x - 2) <= 3)).toBe(true);
  });

  it('while an ability is aimed the clock goes very slow, for everyone; then at its pace again', () => {
    const s = field();
    addUnit(s, 's1', 'assault', 2, 5);
    order(s, { type: 'rtSlow', slow: true });
    run(s, 1000);
    expect(s.realtime!.clock).toBe(Math.round(1000 * RT_SLOW));
    order(s, { type: 'rtSlow', slow: false });
    run(s, 1000);
    expect(s.realtime!.clock).toBe(Math.round(1000 * RT_SLOW) + 1000);
  });

  it('abilities as by turns: a grenade thrown from where it reaches, getting there first; once, and its use spent', () => {
    const s = field(Array.from({ length: 12 }, () => '.'.repeat(30)));
    const u = addUnit(s, 's1', 'assault', 1, 5);
    for (const [id, x, y] of [['z1', 26, 5], ['z2', 27, 5], ['z3', 26, 6]] as const) addUnit(s, id, 'zombie', x, y, { podId: 'p1' });
    s.pods.push({ id: 'p1', active: false, unitIds: ['z1', 'z2', 'z3'], patrol: [], patrolIndex: 0 });
    addUnit(s, 'z9', 'zombie', 29, 0, { podId: 'p9' });
    s.pods.push({ id: 'p9', active: false, unitIds: ['z9'], patrol: [], patrolIndex: 0 });
    // From 25 tiles off, out of throwing range: it walks nearer first.
    order(s, { type: 'rtOrder', unit: 's1', ability: 'grenade', tile: { x: 26, y: 5 } });
    const events = run(s, 15_000);
    expect(events.some((e) => e.t === 'moved' && e.unit === 's1')).toBe(true);
    expect(events.filter((e) => e.t === 'explosive' && e.unit === 's1').length).toBe(1);
    expect(u.charges.grenade ?? 0).toBe(0);
    expect(s.realtime!.units.s1!.order).toEqual({ kind: 'idle' });
  });

  it('keeping watch, it shoots what comes in reach, and keeps at it', () => {
    const s = field();
    const u = addUnit(s, 's1', 'assault', 2, 5);
    u.mods.aim = 200;
    addUnit(s, 'z1', 'zombie', 13, 5, { podId: 'p1', hp: 99, maxHp: 99 });
    s.pods.push({ id: 'p1', active: true, unitIds: ['z1'], patrol: [], patrolIndex: 0 });
    order(s, { type: 'rtOrder', unit: 's1', ability: 'overwatch' });
    const events = run(s, 6000);
    expect(events.filter((e) => e.t === 'shot' && e.unit === 's1').length).toBeGreaterThan(1);
    expect(s.realtime!.units.s1!.order).toEqual({ kind: 'watch' });
    expect(u.pos).toEqual({ x: 2, y: 5, z: 0 });
  });

  it('a blow: it goes alongside, then strikes with the blade until the other is down', () => {
    const s = field();
    const u = addUnit(s, 's1', 'assault', 2, 5);
    u.mods.aim = 200;
    const z = addUnit(s, 'z1', 'zombie', 7, 5, { podId: 'p1' });
    s.pods.push({ id: 'p1', active: false, unitIds: ['z1'], patrol: [], patrolIndex: 0 });
    addUnit(s, 'z9', 'zombie', 15, 0, { podId: 'p9' });
    s.pods.push({ id: 'p9', active: false, unitIds: ['z9'], patrol: [], patrolIndex: 0 });
    order(s, { type: 'rtOrder', unit: 's1', ability: 'slash', target: 'z1' });
    const events = run(s, 10_000);
    expect(events.some((e) => e.t === 'shot' && e.unit === 's1' && e.ability === 'slash')).toBe(true);
    expect(z.alive).toBe(false);
  });

  it('what lasts a turn lasts a round: under cover a while, then not', () => {
    const rows = Array.from({ length: 12 }, (_, y) => (y === 4 ? '...h............' : '.'.repeat(16)));
    const s = field(rows);
    const u = addUnit(s, 's1', 'assault', 3, 5);
    // A while into the clock's round first: its cover lasts a whole one from when it took it, not to the clock's next.
    run(s, 4200);
    order(s, { type: 'rtOrder', unit: 's1', ability: 'hunker' });
    run(s, 1000);
    expect(u.hunkered).toBe(true);
    run(s, RT_ROUND - 1500);
    expect(u.hunkered).toBe(true);
    run(s, 1500);
    expect(u.hunkered).toBe(false);
  });

  it('only real-time orders while in real time; only one’s own soldiers; and not in a mission by turns', () => {
    const s = field();
    addUnit(s, 's1', 'assault', 2, 5, { owner: 1 });
    addUnit(s, 'z1', 'zombie', 9, 5);
    expect(executeCommand(s, { type: 'move', unit: 's1', to: { x: 4, y: 5 } }, ctx(1)).ok).toBe(false);
    expect(executeCommand(s, { type: 'rtOrder', unit: 's1', to: { x: 4, y: 5 } }, { ...ctx(0), connected: [0, 1] }).ok).toBe(false);
    expect(executeCommand(s, { type: 'rtOrder', unit: 's1', target: 's1' }, ctx(1)).ok).toBe(false);
    expect(executeCommand(s, { type: 'rtOrder', unit: 's1', to: { x: 4, y: 5 } }, ctx(1)).ok).toBe(true);
    const turns = stateFrom(['....']);
    addUnit(turns, 's1', 'assault', 0, 0);
    expect(executeCommand(turns, { type: 'rtPause', paused: true }, ctx(0)).ok).toBe(false);
  });

  it('a skirmish in real time starts paused, on a generated map, and the same orders give the same mission', () => {
    const play = () => {
      const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 7919 }, slots: [0], rng: new Rng(5), kind: 'elimination', realtime: true });
      expect(s.realtime).toEqual({ clock: 0, paused: true, units: {} });
      const soldier = s.units.find((u) => u.team === 'xcom')!;
      const far = { x: soldier.pos.x, y: soldier.pos.y, z: 0 };
      // Somewhere a few tiles off it can get to.
      for (const d of [[4, 0], [-4, 0], [0, 4], [0, -4]]) {
        const to = { x: far.x + d[0]!, y: far.y + d[1]!, z: 0 };
        if (executeCommand(s, { type: 'rtOrder', unit: soldier.id, to }, { slot: 0, connected: [0], rng: new Rng(1) }).ok) break;
      }
      executeCommand(s, { type: 'rtPause', paused: false }, { slot: 0, connected: [0], rng: new Rng(1) });
      const rng = new Rng(9);
      const events: GameEvent[] = [];
      for (let t = 0; t < 20_000; t += RT_TICK) events.push(...realtimeStep(s, rng, [0]));
      return { events: events.length, pos: soldier.pos, clock: s.realtime!.clock };
    };
    const a = play();
    expect(a.clock).toBe(20_000);
    expect(play()).toEqual(a);
  });
});
