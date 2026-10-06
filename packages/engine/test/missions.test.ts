import { describe, expect, it } from 'vitest';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { makeUnit } from '../src/setup';
import { Rng } from '../src/rng';
import { executeCommand, Sim } from '../src/rules';
import type { Command } from '../src/protocol';
import type { GameState, MissionKind } from '../src/types';
import { addUnit, stateFrom } from './helpers';

const ctx = (seed = 1) => ({ slot: 0 as const, connected: [0 as const], rng: new Rng(seed) });

function run(s: GameState, cmd: Command, seed = 1): GameEvent[] {
  const r = executeCommand(s, cmd, ctx(seed));
  if (!r.ok) throw new Error(r.error);
  return r.events;
}

function field(kind: MissionKind, rows = Array.from({ length: 20 }, () => '.'.repeat(20))): GameState {
  const s = stateFrom(rows);
  s.mission = { ...s.mission, kind, turnsLeft: 5, evacZone: [{ x: 0, y: 19 }, { x: 1, y: 19 }] };
  return s;
}

describe('sabotage', () => {
  it('destroying the relay secures the objective and stops the timer; then the hostiles must fall', () => {
    const s = field('sabotage');
    addUnit(s, 's1', 'sharpshooter', 0, 0);
    const relay = { ...makeUnit('relay', 'relay', 'Retransmisor', null, { x: 6, y: 0 }, null), objective: true };
    s.units.push(relay);
    s.mission.objectiveUnit = 'relay';
    const alien = addUnit(s, 'a1', 'trooper', 19, 19);
    const sim = new Sim(s, new Rng(1), [0]);
    sim.damage(relay, 99, 0);
    sim.checkOutcome();
    expect(s.mission.objectiveDone).toBe(true);
    expect(s.mission.turnsLeft).toBeNull();
    expect(s.outcome).toBeNull();
    sim.damage(alien, 99, 0);
    sim.checkOutcome();
    expect(s.outcome).toBe('victory');
  });

  it('the relay never counts as a hostile for an elimination check', () => {
    const s = field('elimination');
    addUnit(s, 's1', 'sharpshooter', 0, 0);
    s.units.push({ ...makeUnit('relay', 'relay', 'R', null, { x: 6, y: 0 }, null), objective: true });
    const sim = new Sim(s, new Rng(1), [0]);
    sim.checkOutcome();
    expect(s.outcome).toBe('victory');
  });
});

describe('hack', () => {
  function hackField() {
    const rows = Array.from({ length: 20 }, () => '.'.repeat(20));
    rows[5] = '.....C' + '.'.repeat(14);
    const s = field('hack', rows);
    s.mission.terminal = { x: 5, y: 5 };
    s.concealed = true;
    return s;
  }

  it('only next to the terminal, or within the drone range for a specialist', () => {
    const s = hackField();
    addUnit(s, 's1', 'assault', 0, 0);
    addUnit(s, 's2', 'specialist', 5, 12);
    addUnit(s, 'a1', 'trooper', 19, 0);
    expect(executeCommand(s, { type: 'ability', unit: 's1', ability: 'hack' }, ctx()).ok).toBe(false);
    expect(executeCommand(s, { type: 'ability', unit: 's2', ability: 'hack' }, ctx()).ok).toBe(true);
  });

  it('success secures the objective; failure raises the alarm and brings a drop', () => {
    let failed: GameState | null = null;
    for (let seed = 1; seed < 40; seed++) {
      const s = hackField();
      addUnit(s, 's1', 'assault', 4, 4);
      addUnit(s, 's2', 'assault', 0, 19); // still has actions: the XCOM turn goes on
      addUnit(s, 'a1', 'trooper', 19, 0);
      const events = run(s, { type: 'ability', unit: 's1', ability: 'hack' }, seed);
      const hack = events.find((e) => e.t === 'hacked') as Extract<GameEvent, { t: 'hacked' }>;
      if (hack.success) {
        expect(s.mission.objectiveDone).toBe(true);
      } else {
        expect(events.some((e) => e.t === 'reinforcementsIncoming')).toBe(true);
        expect(s.concealed).toBe(false);
        failed = s;
      }
    }
    expect(failed).not.toBeNull();
    // The drop lands at the end of the alien turn, already alert.
    const s = failed!;
    const before = s.units.length;
    new Sim(s, new Rng(2), [0]).runAlienTurn();
    expect(s.units.length).toBeGreaterThan(before);
    expect(s.mission.reinforcements.every((r) => r.landed)).toBe(true);
    expect(s.pods.find((p) => p.id.startsWith('pr'))?.active).toBe(true);
  });
});

describe('rescue', () => {
  function rescueField() {
    const s = field('rescue');
    s.units.push({ ...makeUnit('vip', 'vip', 'VIP', null, { x: 8, y: 8 }, null), captive: true, ap: 0 });
    s.mission.objectiveUnit = 'vip';
    addUnit(s, 's1', 'assault', 0, 8);
    addUnit(s, 'a1', 'trooper', 19, 0);
    return s;
  }

  it('the VIP joins whoever reaches them, then must be extracted', () => {
    const s = rescueField();
    run(s, { type: 'move', unit: 's1', to: { x: 7, y: 8 } });
    const vip = s.units.find((u) => u.id === 'vip')!;
    expect(vip.captive).toBe(false);
    expect(vip.owner).toBe(0);
    vip.pos = { x: 0, y: 19 };
    vip.ap = 2;
    run(s, { type: 'ability', unit: 'vip', ability: 'evac' });
    expect(s.mission.objectiveDone).toBe(true);
  });

  it('losing the VIP fails the mission', () => {
    const s = rescueField();
    const sim = new Sim(s, new Rng(1), [0]);
    sim.damage(s.units.find((u) => u.id === 'vip')!, 99, 0);
    sim.checkOutcome();
    expect(s.outcome).toBe('defeat');
    expect(s.outcomeReason).toBe('objectiveLost');
  });

  it('a captive VIP is ignored by the aliens and gives no vision', () => {
    const s = rescueField();
    s.units.find((u) => u.id === 's1')!.pos = { x: 0, y: 0 };
    const sim = new Sim(s, new Rng(1), [0]);
    expect(sim.podSpotsSquad(s.units.find((u) => u.id === 'a1')!, { x: 19, y: 19 })).toBe(false);
  });
});

describe('patrols and reinforcements', () => {
  it('a patrolling pod walks its route and stops when it spots the squad', () => {
    const s = field('elimination', Array.from({ length: 30 }, () => '.'.repeat(30)));
    s.concealed = false;
    addUnit(s, 's1', 'specialist', 2, 28);
    const a = addUnit(s, 'a1', 'trooper', 28, 2, { podId: 'p1' });
    addUnit(s, 'a2', 'trooper', 28, 3, { podId: 'p1' });
    s.pods.push({ id: 'p1', active: false, unitIds: ['a1', 'a2'], patrol: [{ x: 28, y: 2 }, { x: 4, y: 26 }], patrolIndex: 0 });
    const client = cloneState(s);
    const sim = new Sim(s, new Rng(1), [0]);
    let moved = false;
    for (let turn = 0; turn < 8 && !s.pods[0]!.active; turn++) {
      const before = { ...a.pos };
      sim.runAlienTurn();
      if (a.pos.x !== before.x || a.pos.y !== before.y) moved = true;
    }
    expect(moved).toBe(true);
    expect(s.pods[0]!.active).toBe(true);
    for (const e of sim.events) applyEvent(client, e);
    expect(client).toEqual(s);
  });

  it('scheduled reinforcements are announced a turn early and land alert', () => {
    const s = field('recovery', Array.from({ length: 30 }, () => '.'.repeat(30)));
    s.concealed = false;
    addUnit(s, 's1', 'specialist', 15, 15);
    addUnit(s, 'a1', 'trooper', 29, 29);
    s.mission.turnsLeft = 20;
    s.mission.reinforcements = [{ id: 'r1', turn: 2, pos: null, units: ['trooper', 'trooper'], landed: false }];
    const sim = new Sim(s, new Rng(4), [0]);
    sim.runAlienTurn(); // turn 1: flare for turn 2
    expect(s.mission.reinforcements[0]!.pos).not.toBeNull();
    expect(s.mission.reinforcements[0]!.landed).toBe(false);
    sim.runAlienTurn(); // turn 2: drop
    expect(s.mission.reinforcements[0]!.landed).toBe(true);
    const dropped = s.units.filter((u) => u.id.startsWith('r1'));
    expect(dropped).toHaveLength(2);
    for (const u of dropped) {
      const d = Math.hypot(u.pos.x - 15, u.pos.y - 15);
      expect(d).toBeGreaterThanOrEqual(3);
    }
  });
});
