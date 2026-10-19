import { describe, expect, it } from 'vitest';
import { abilityTargets } from '../src/abilities';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { kindAt, nodeOf } from '../src/grid';
import { buildPath, computeReach, COST_ORTHO, maxMoveCost } from '../src/path';
import { Rng } from '../src/rng';
import { executeCommand, Sim } from '../src/rules';
import type { GameState, Slot } from '../src/types';
import { addUnit, stateFrom } from './helpers';

// Arturo, 19/10: «los compañeros se bloquean». Nobody walks through anyone,
// whoever's side, as in XCOM; and nobody is ever put where somebody stands.

const ctx = (seed = 1) => ({ slot: 0 as Slot, connected: [0] as Slot[], rng: new Rng(seed) });

/** Two living units on one place, if any, after every event of `events` replayed on `start`. */
function stacked(start: GameState, events: GameEvent[]): string | null {
  const s = cloneState(start);
  for (const e of events) {
    applyEvent(s, e);
    const seen = new Map<number, string>();
    for (const u of s.units) {
      if (!u.alive) continue;
      const node = nodeOf(s, u.pos);
      if (seen.has(node)) return `${seen.get(node)} and ${u.id} on ${u.pos.x},${u.pos.y},${u.pos.z} after ${e.t}`;
      seen.set(node, u.id);
    }
  }
  return null;
}

describe('nobody walks through anyone (19/10)', () => {
  it('in a corridor a tile wide, an ally is a wall: no way past', () => {
    const s = stateFrom(['#########', '#.......#', '#########']);
    const u = addUnit(s, 's1', 'assault', 1, 1);
    addUnit(s, 's2', 'assault', 3, 1);
    const reach = computeReach(s, u, 1000);
    expect(reach.cost.has(nodeOf(s, { x: 2, y: 1, z: 0 }))).toBe(true);
    for (const x of [3, 4, 5, 6, 7]) expect(reach.cost.has(nodeOf(s, { x, y: 1, z: 0 }))).toBe(false);
    const r = executeCommand(s, { type: 'move', unit: 's1', to: { x: 5, y: 1 } }, ctx());
    expect(r.ok).toBe(false);
    expect(u.pos).toEqual({ x: 1, y: 1, z: 0 });
  });

  it('with a way round, it goes round the ally, never through or past its corners', () => {
    // The corridor (row 1) and a loop under it (row 3), joined at both ends.
    const s = stateFrom(['#########', '#.......#', '#.#####.#', '#.......#', '#########']);
    const u = addUnit(s, 's1', 'assault', 1, 1, { ap: 2 });
    addUnit(s, 's2', 'assault', 4, 1);
    const reach = computeReach(s, u, maxMoveCost(u));
    const path = buildPath(s, u, reach, { x: 6, y: 1 });
    expect(path).not.toBeNull();
    expect(path!.some((p) => p.x === 4 && p.y === 1)).toBe(false);
    expect(path!.some((p) => p.y === 3)).toBe(true);
    expect(reach.cost.get(nodeOf(s, { x: 6, y: 1, z: 0 }))).toBe(COST_ORTHO * 11);
    const r = executeCommand(s, { type: 'move', unit: 's1', to: { x: 6, y: 1 } }, ctx());
    expect(r.ok).toBe(true);
    if (r.ok) {
      const steps = r.events.flatMap((e) => (e.t === 'moved' ? e.path : []));
      expect(steps.some((p) => p.x === 4 && p.y === 1)).toBe(false);
    }
    expect(u.pos).toEqual({ x: 6, y: 1, z: 0 });
  });

  it('a door opens before the soldier steps into it, not after', () => {
    const s = stateFrom(['#######', '#..+..#', '#######']);
    addUnit(s, 's1', 'assault', 1, 1);
    const r = executeCommand(s, { type: 'move', unit: 's1', to: { x: 5, y: 1 } }, ctx());
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    const order = r.events.filter((e) => e.t === 'moved' || e.t === 'tileChanged');
    const door = order.findIndex((e) => e.t === 'tileChanged');
    expect(door).toBeGreaterThan(0);
    // Up to the door's side first, then the door opens, then through it.
    expect(order[door - 1]).toMatchObject({ t: 'moved', path: [{ x: 2, y: 1, z: 0 }] });
    expect(order[door]).toMatchObject({ t: 'tileChanged', pos: { x: 3, y: 1, z: 0 }, kind: 'doorOpen' });
    expect(order[door + 1]).toMatchObject({ t: 'moved', path: [{ x: 3, y: 1, z: 0 }, { x: 4, y: 1, z: 0 }, { x: 5, y: 1, z: 0 }] });
  });

  it('the dead never stack: a horde queues and spreads round a room with one door, and still gets in to bite', () => {
    const rows = [
      '..........................',
      '..........................',
      '.........#######..........',
      '.........#.....#..........',
      '.........#.....+..........',
      '.........#.....#..........',
      '.........#######..........',
      '..........................',
      '..........................',
      '..........................',
    ];
    const s = stateFrom(rows);
    // A siege's dead know where the living are (ai.ts takeZombieTurn); the door stands open.
    s.mission.siege = { hold: 99 };
    s.tiles[4 * rows[0]!.length + 15] = 'doorOpen';
    s.activeTeam = 'alien';
    addUnit(s, 's1', 'assault', 11, 4, { hp: 99, maxHp: 99 });
    const ids: string[] = [];
    for (let k = 0; k < 12; k++) {
      const id = `z${k + 1}`;
      addUnit(s, id, 'zombie', 18 + (k % 6), 7 + Math.floor(k / 6), { podId: 'p1' });
      ids.push(id);
    }
    s.pods.push({ id: 'p1', active: true, unitIds: ids, patrol: [], patrolIndex: 0 });
    let bites = 0;
    for (let turn = 0; turn < 8 && !s.outcome; turn++) {
      const start = cloneState(s);
      const sim = new Sim(s, new Rng(turn + 1), [0]);
      for (const u of s.units) if (u.alive && u.team === 'alien') u.ap = 2;
      s.activeTeam = 'alien';
      expect(() => sim.runAlienTurn()).not.toThrow();
      expect(stacked(start, sim.events)).toBeNull();
      bites += sim.events.filter((e) => e.t === 'shot' && e.ability === 'claw').length;
    }
    expect(bites).toBeGreaterThan(0);
    // In through the door, one at a time: some of them made it inside.
    const inside = s.units.filter((u) => u.alive && u.team === 'alien' && u.pos.x >= 10 && u.pos.x <= 14 && u.pos.y >= 3 && u.pos.y <= 5);
    expect(inside.length).toBeGreaterThan(0);
  });

  it('in a corridor a tile wide the dead wait in line behind each other, the front one biting', () => {
    const s = stateFrom(['############', '#..........#', '############']);
    s.activeTeam = 'alien';
    addUnit(s, 's1', 'assault', 1, 1, { hp: 99, maxHp: 99 });
    const ids = ['z1', 'z2', 'z3', 'z4'];
    ids.forEach((id, k) => addUnit(s, id, 'zombie', 7 + k, 1, { podId: 'p1' }));
    s.pods.push({ id: 'p1', active: true, unitIds: ids, patrol: [], patrolIndex: 0 });
    let bites = 0;
    for (let turn = 0; turn < 6; turn++) {
      const start = cloneState(s);
      const sim = new Sim(s, new Rng(turn + 7), [0]);
      for (const u of s.units) if (u.alive && u.team === 'alien') u.ap = 2;
      sim.runAlienTurn();
      expect(stacked(start, sim.events)).toBeNull();
      bites += sim.events.filter((e) => e.t === 'shot' && e.ability === 'claw').length;
    }
    expect(bites).toBeGreaterThan(0);
    // A queue, nose to tail, behind the one at the soldier.
    const xs = ids.map((id) => s.units.find((u) => u.id === id)!.pos.x).sort((a, b) => a - b);
    expect(xs).toEqual([2, 3, 4, 5]);
  });

  it('the Chillón never raises a corpse onto somebody: it gets up beside them, or not at all', () => {
    const s = stateFrom(Array.from({ length: 12 }, () => '.'.repeat(12)));
    s.activeTeam = 'alien';
    addUnit(s, 's1', 'assault', 2, 2);
    const screamer = addUnit(s, 'a1', 'sectoid', 9, 2, { podId: 'p1', ap: 2 });
    s.pods.push({ id: 'p1', active: true, unitIds: ['a1'], patrol: [], patrolIndex: 0 });
    const corpse = addUnit(s, 'a5', 'trooper', 7, 4, { alive: false, hp: 0 });
    // A soldier stands on the body.
    addUnit(s, 's2', 'assault', 7, 4);
    expect(abilityTargets(s, screamer, 'reanimate')).toContain(corpse);
    const sim = new Sim(s, new Rng(1), [0]);
    sim.perform(screamer, 'reanimate', { target: corpse });
    const zombie = s.units.find((u) => u.template === 'zombie')!;
    expect(zombie.alive).toBe(true);
    expect(zombie.pos).not.toEqual(corpse.pos);
    expect(Math.max(Math.abs(zombie.pos.x - 7), Math.abs(zombie.pos.y - 4))).toBe(1);
    expect(s.units.filter((u) => u.alive && u.pos.x === zombie.pos.x && u.pos.y === zombie.pos.y)).toHaveLength(1);

    // Hemmed in all round, with somebody on it: it can't get up, and the Chillón doesn't try.
    const t = stateFrom(['.....', '.###.', '.#.#.', '.###.', '.....']);
    t.activeTeam = 'alien';
    const scream2 = addUnit(t, 'a1', 'sectoid', 0, 0, { podId: 'p1', ap: 2 });
    t.pods.push({ id: 'p1', active: true, unitIds: ['a1'], patrol: [], patrolIndex: 0 });
    // Seen over the walls from above is not the point: the walls are low here, the body in sight.
    t.tiles = t.tiles.map((k) => (k === 'wall' ? 'low' : k));
    const body = addUnit(t, 'a5', 'trooper', 2, 2, { alive: false, hp: 0 });
    addUnit(t, 's1', 'assault', 2, 2);
    expect(kindAt(t, body.pos)).toBe('floor');
    expect(abilityTargets(t, scream2, 'reanimate')).not.toContain(body);
  });

  it('a silenced patrol car stays solid over both its tiles', () => {
    const s = stateFrom(['......', '......', '......']);
    s.mission = { ...s.mission, kind: 'sabotage', alarm: 'police', objectiveUnit: 'relay', turnsLeft: 5 };
    const relay = addUnit(s, 'relay', 'relay', 2, 1, { objective: true });
    s.tiles[1 * 6 + 3] = 'high';
    addUnit(s, 's1', 'assault', 0, 0);
    const sim = new Sim(s, new Rng(1), [0]);
    sim.damage(relay, 99, 0);
    expect(relay.alive).toBe(false);
    expect(kindAt(s, relay.pos)).toBe('high');
    expect(computeReach(s, s.units.find((u) => u.id === 's1')!, 100).cost.has(nodeOf(s, relay.pos))).toBe(false);
  });

  it('those dropping round a flare with no room for all of them wait: nobody lands on anyone', () => {
    // A pocket of three tiles walled in, a soldier on one of them: two free within reach of the flare.
    const s = stateFrom(['#########', '#########', '#########', '###...###', '#########', '#########', '#########', '.........']);
    addUnit(s, 's1', 'assault', 0, 7);
    addUnit(s, 's2', 'assault', 3, 3);
    s.mission.reinforcements.push({ id: 'r1', turn: 1, pos: { x: 4, y: 3, z: 0 }, units: ['zombie', 'zombie', 'zombie'], landed: false });
    const sim = new Sim(s, new Rng(1), [0]);
    sim.landReinforcements(1);
    expect(sim.events.some((e) => e.t === 'spawned')).toBe(false);
    expect(s.units.filter((u) => u.alive)).toHaveLength(2);
  });
});
