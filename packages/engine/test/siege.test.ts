import { describe, expect, it } from 'vitest';
import { abilityBlocker, breakablesBeside } from '../src/abilities';
import { breachPlan, takeZombieTurn } from '../src/ai';
import { SIEGE, TEMPLATES } from '../src/content';
import { decorOf } from '../src/decor';
import type { GameEvent } from '../src/events';
import { kindAt, nodeOf, tileIndex } from '../src/grid';
import { MAPS, parseMap } from '../src/maps';
import { computeReach } from '../src/path';
import { Rng } from '../src/rng';
import { executeCommand, Sim } from '../src/rules';
import { createMission } from '../src/setup';
import type { GameState, Pod, Slot, Unit } from '../src/types';
import { addUnit, stateFrom } from './helpers';

// Fase 12, hito 1 (docs/FASE12.md): the base as a battlefield, and a siege of it.

const of = <T extends GameEvent['t']>(events: GameEvent[], t: T) => events.filter((e): e is Extract<GameEvent, { t: T }> => e.t === t);
const sim = (s: GameState, seed = 1) => new Sim(s, new Rng(seed), [0]);
const ground = (x: number, y: number) => ({ x, y, z: 0 });

/**
 * A small walled yard: a door (north) and two barricades (south). In a siege
 * every door and barricade holds what SIEGE says.
 */
function yard(): GameState {
  const s = stateFrom([
    '..........',
    '.###+####.',
    '.#......#.',
    '.#......#.',
    '.#......#.',
    '.###hh###.',
    '..........',
  ]);
  s.mission.siege = { hold: SIEGE.hold };
  s.breakables = { [tileIndex(s, 4, 1)]: SIEGE.door, [tileIndex(s, 4, 5)]: SIEGE.barricade, [tileIndex(s, 5, 5)]: SIEGE.barricade };
  return s;
}

function pod(s: GameState, id: string, units: Unit[]): Pod {
  const p: Pod = { id, active: true, unitIds: units.map((u) => u.id), patrol: [], patrolIndex: 0 };
  for (const u of units) u.podId = id;
  s.pods.push(p);
  return p;
}

describe('the base as a battlefield', () => {
  it('«El Bastión» is a walled base: a gate and a barricade on each side, a watchtower on each corner, the Sala de radio in the middle', () => {
    const def = MAPS.bastion!;
    const map = parseMap(def);
    expect(def.siege).toBe(true);
    expect(map.width).toBe(36);
    expect(map.height).toBe(36);
    expect(map.barricades).toHaveLength(12);
    expect(map.tiles.filter((k) => k === 'door').length).toBe(8 + 5);
    // The barricades look like barricades, facing across the wall they stand in.
    const look = (x: number, y: number) => decorOf(map.decor?.[y * map.width + x]);
    expect(look(24, 6)).toEqual({ kind: 'barricade', facing: 0 });
    expect(look(29, 11)).toEqual({ kind: 'barricade', facing: 1 });
    // The towers stand a storey up, with a ladder from inside.
    expect(map.elev[7 * map.width + 7]).toBe(1);
    expect(map.tiles[7 * map.width + 9]).toBe('ladder');
    expect(map.spawns.length).toBeGreaterThanOrEqual(6);
  });

  it('shut, the wall keeps the dead out: no way in from any side, nor up the towers', () => {
    const s = createMission({ map: { kind: 'handmade', id: 'bastion' }, slots: [0], rng: new Rng(2) });
    const inside = (n: number) => {
      const x = n % s.width;
      const y = Math.floor(n / s.width);
      return x > 6 && x < 29 && y > 6 && y < 29;
    };
    for (const [i, [x, y]] of [[17, 1], [34, 17], [17, 34], [1, 17], [1, 1], [34, 34]].entries()) {
      const z = addUnit(s, `z${i}`, 'zombie', x!, y!);
      const reach = computeReach(s, z, Infinity);
      expect([...reach.cost.keys()].filter(inside)).toEqual([]);
      // The towers' platforms stay out of reach too (a storey up, their ladders inside).
      expect([...reach.cost.keys()].some((n) => s.elev[n]! > 0)).toBe(false);
    }
  });

  it('a mission on it is always a siege: no enemies yet, waves from every front, every door and barricade holding, magazines at hand', () => {
    const s = createMission({ map: { kind: 'handmade', id: 'bastion' }, slots: [0, 1], rng: new Rng(7), kind: 'recovery' });
    expect(s.mission.kind).toBe('elimination');
    expect(s.mission.siege).toEqual({ hold: SIEGE.hold });
    expect(s.units.filter((u) => u.team === 'alien')).toHaveLength(0);
    expect(s.concealed).toBe(false);
    const soldiers = s.units.filter((u) => u.team === 'xcom');
    expect(soldiers).toHaveLength(6);
    for (const u of soldiers) expect(u.mags).toBeGreaterThanOrEqual(SIEGE.mags);
    const holds = Object.entries(s.breakables!);
    expect(holds.filter(([, hp]) => hp === SIEGE.door)).toHaveLength(13);
    expect(holds.filter(([, hp]) => hp === SIEGE.barricade)).toHaveLength(12);
    for (const [node] of holds) expect(['door', 'low']).toContain(s.tiles[Number(node)]);
    // Five waves, in groups of five at most, each group by a side at random (Arturo, 18/10: from every front).
    const waves = s.mission.reinforcements;
    expect([...new Set(waves.map((r) => r.turn))]).toEqual([1, 3, 5, 7, 9]);
    for (const r of waves) {
      expect(r.horde).toBe(true);
      expect(r.units.length).toBeLessThanOrEqual(5);
      expect(r.side).toBeGreaterThanOrEqual(0);
      expect(r.side).toBeLessThan(4);
    }
    expect(waves.flatMap((r) => r.units)).toContain('sectoid');
    // Over a few sieges, every side.
    const sides = new Set([7, 8, 9, 10].flatMap((seed) => createMission({ map: { kind: 'handmade', id: 'bastion' }, slots: [0], rng: new Rng(seed) }).mission.reinforcements.map((r) => r.side)));
    expect([...sides].sort()).toEqual([0, 1, 2, 3]);
  });
});

describe('the dead against doors and barricades', () => {
  it('in a siege the dead never open a door, the squad does; elsewhere they still do', () => {
    const s = yard();
    const z = addUnit(s, 'z1', 'zombie', 4, 0);
    const soldier = addUnit(s, 's1', 'rookie', 4, 2);
    const door = nodeOf(s, ground(4, 1));
    expect(computeReach(s, z, 100).cost.has(door)).toBe(false);
    expect(computeReach(s, soldier, 100).cost.has(door)).toBe(true);
    delete s.mission.siege;
    expect(computeReach(s, z, 100).cost.has(door)).toBe(true);
  });

  it('with no way to the soldiers it beats on the door beside it: a Zombi takes off 3 a blow, twice a turn, and the blow is heard', () => {
    const s = yard();
    const z = addUnit(s, 'z1', 'zombie', 4, 0);
    const far = addUnit(s, 'z2', 'zombie', 9, 6, { heard: null });
    pod(s, 'p1', [z]);
    pod(s, 'p2', [far]);
    addUnit(s, 's1', 'rookie', 4, 4);
    expect(breakablesBeside(s, z.pos)).toEqual([ground(4, 1)]);
    expect(abilityBlocker(s, z, 'smash')).toBeNull();
    const run = sim(s);
    takeZombieTurn(run, z);
    const blows = of(run.events, 'smashed');
    expect(blows.map((e) => [e.amount, e.left])).toEqual([
      [3, SIEGE.door - 3],
      [3, SIEGE.door - 6],
    ]);
    expect(s.breakables![tileIndex(s, 4, 1)]).toBe(SIEGE.door - 6);
    expect(z.pos).toEqual(ground(4, 0));
    // Heard by the dead round about: they come to the weak point.
    expect(of(run.events, 'noise').some((e) => e.radius === SIEGE.smashNoise && e.heard.includes('z2'))).toBe(true);
  });

  it('at nothing it gives way: the way in is open ground, and the next one walks in', () => {
    const s = yard();
    s.breakables![tileIndex(s, 4, 1)] = 3;
    const z = addUnit(s, 'z1', 'zombie', 4, 0);
    pod(s, 'p1', [z]);
    addUnit(s, 's1', 'rookie', 7, 4);
    const run = sim(s);
    takeZombieTurn(run, z);
    expect(of(run.events, 'smashed')[0]).toMatchObject({ amount: 3, left: 0 });
    expect(of(run.events, 'tileChanged')[0]).toMatchObject({ pos: ground(4, 1), kind: 'floor' });
    expect(kindAt(s, ground(4, 1))).toBe('floor');
    // A breach, kept as such.
    expect(s.breakables![tileIndex(s, 4, 1)]).toBe(0);
    // With its second action it is through.
    expect(z.pos.y).toBeGreaterThan(1);
  });

  it('it goes for the weakest point, counting the walk there and the way left to the soldier', () => {
    const s = yard();
    // A barricade in the north wall too, two tiles off.
    s.tiles[tileIndex(s, 2, 1)] = 'low';
    s.breakables![tileIndex(s, 2, 1)] = SIEGE.barricade;
    const z = addUnit(s, 'z1', 'zombie', 4, 0);
    addUnit(s, 's1', 'rookie', 4, 4);
    // The barricade holds half of what the door does: worth the two steps.
    expect(breachPlan(s, z, ground(4, 3))).toEqual({ tile: ground(2, 1), stand: expect.objectContaining({ y: 0 }) });
    // As strong as the door, the door beside it.
    s.breakables![tileIndex(s, 2, 1)] = SIEGE.door;
    expect(breachPlan(s, z, ground(4, 3))).toEqual({ tile: ground(4, 1), stand: ground(4, 0) });
  });

  it('it never beats on what opens nothing new: the other leaf of a gate already broken', () => {
    const s = stateFrom([
      '............',
      '.####++####.',
      '.#........#.',
      '.#..####..#.',
      '.#..#..+..#.',
      '.#..####..#.',
      '.##########.',
    ]);
    s.mission.siege = { hold: SIEGE.hold };
    s.breakables = { [tileIndex(s, 5, 1)]: SIEGE.door, [tileIndex(s, 6, 1)]: SIEGE.door, [tileIndex(s, 7, 4)]: SIEGE.door };
    const z = addUnit(s, 'z1', 'zombie', 6, 0);
    addUnit(s, 's1', 'rookie', 9, 5);
    // Both leaves shut: the one on the way in.
    expect(breachPlan(s, z, ground(5, 4))?.tile.y).toBe(1);
    // One broken: through it, for the door of the room where the goal is.
    s.tiles[tileIndex(s, 5, 1)] = 'floor';
    s.breakables[tileIndex(s, 5, 1)] = 0;
    expect(breachPlan(s, z, ground(5, 4))).toEqual({ tile: ground(7, 4), stand: expect.anything() });
  });

  it('with a way in it walks, and it bites before it beats', () => {
    const s = yard();
    s.tiles[tileIndex(s, 4, 1)] = 'doorOpen';
    const z = addUnit(s, 'z1', 'zombie', 4, 0);
    pod(s, 'p1', [z]);
    addUnit(s, 's1', 'rookie', 7, 4);
    expect(breachPlan(s, z, ground(7, 4))).toBeNull();
    const run = sim(s);
    takeZombieTurn(run, z);
    expect(of(run.events, 'smashed')).toHaveLength(0);
    expect(of(run.events, 'moved').length).toBeGreaterThan(0);

    const t = yard();
    const biter = addUnit(t, 'z1', 'zombie', 3, 0);
    pod(t, 'p1', [biter]);
    const soldier = addUnit(t, 's1', 'rookie', 2, 0, { hp: 20, maxHp: 20 });
    const run2 = sim(t);
    takeZombieTurn(run2, biter);
    expect(of(run2.events, 'smashed')).toHaveLength(0);
    expect(of(run2.events, 'abilityUsed').every((e) => e.ability === 'claw')).toBe(true);
    expect(soldier.hp).toBeLessThan(20);
  });

  it('a grenade that takes a barricade leaves a breach too; one that blows a door open does not', () => {
    const s = yard();
    const run = sim(s);
    run.emit({ t: 'tileChanged', pos: ground(4, 5), kind: 'floor' });
    run.emit({ t: 'tileChanged', pos: ground(4, 1), kind: 'doorOpen' });
    expect(s.breakables![tileIndex(s, 4, 5)]).toBe(0);
    expect(s.breakables![tileIndex(s, 4, 1)]).toBe(SIEGE.door);
    expect(breakablesBeside(s, ground(4, 6))).toEqual([ground(5, 5)]);
  });

  it('only those with claws beat: a Hinchado waits at the wall', () => {
    expect(TEMPLATES.zombie.abilities).toContain('smash');
    expect(TEMPLATES.xenoid.abilities).toContain('smash');
    expect(TEMPLATES.mec.abilities).toContain('smash');
    expect(TEMPLATES.bloater.abilities).not.toContain('smash');
    const s = yard();
    const b = addUnit(s, 'b1', 'bloater', 4, 0);
    pod(s, 'p1', [b]);
    addUnit(s, 's1', 'rookie', 4, 4);
    const run = sim(s);
    takeZombieTurn(run, b);
    expect(of(run.events, 'smashed')).toHaveLength(0);
  });
});

describe('how a siege ends', () => {
  it('the dead inside are no defeat; nobody left to defend it is', () => {
    const s = yard();
    s.mission.reinforcements = [{ id: 'r1', turn: 5, pos: null, units: ['zombie'], landed: false, horde: true, side: 0 }];
    const soldier = addUnit(s, 's1', 'rookie', 7, 4);
    addUnit(s, 'z1', 'zombie', 4, 3);
    const run = sim(s);
    run.checkOutcome();
    expect(s.outcome).toBeNull();
    run.damage(soldier, 99, 0);
    run.checkOutcome();
    expect([s.outcome, s.outcomeReason]).toEqual(['defeat', 'wiped']);
  });

  it('held through its last enemy turn, or every wave in and down, the base stands', () => {
    const s = yard();
    s.mission.reinforcements = [{ id: 'r1', turn: 20, pos: null, units: ['zombie'], landed: false, horde: true, side: 0 }];
    addUnit(s, 's1', 'rookie', 4, 3);
    s.turn = SIEGE.hold - 1;
    sim(s).runAlienTurn();
    expect(s.outcome).toBeNull();
    sim(s).runAlienTurn();
    expect([s.outcome, s.outcomeReason]).toEqual(['victory', 'objective']);

    const t = yard();
    t.mission.reinforcements = [{ id: 'r1', turn: 1, pos: null, units: ['zombie'], landed: true, horde: true, side: 0 }];
    addUnit(t, 's1', 'rookie', 4, 3);
    sim(t).checkOutcome();
    expect([t.outcome, t.outcomeReason]).toEqual(['victory', 'objective']);
  });

  it('each group of a wave comes in by its side, alert', () => {
    const s = createMission({ map: { kind: 'handmade', id: 'bastion' }, slots: [0], rng: new Rng(3) });
    const first = s.mission.reinforcements.filter((r) => r.turn === 1);
    const run = sim(s);
    run.landReinforcements(1);
    expect(s.units.filter((u) => u.team === 'alien')).toHaveLength(first.reduce((n, r) => n + r.units.length, 0));
    for (const r of first) {
      const side = r.side!;
      const onSide = (u: Unit) => [u.pos.y < 2, u.pos.x >= s.width - 2, u.pos.y >= s.height - 2, u.pos.x < 2][side];
      // In the middle half of that side, not round the corner.
      const along = (u: Unit) => (side % 2 === 0 ? u.pos.x : u.pos.y);
      for (const u of s.units.filter((x) => x.id.startsWith(`${r.id}u`))) {
        expect(onSide(u)).toBe(true);
        expect(along(u)).toBeGreaterThanOrEqual(9);
        expect(along(u)).toBeLessThan(27);
        // They know where the living are: nothing to listen for.
        expect(u.heard ?? null).toBeNull();
      }
    }
    for (const p of s.pods) expect(p.active).toBe(true);
  });

  it('seeing nobody, the dead of a siege go for the nearest soldier', () => {
    const s = yard();
    s.tiles[tileIndex(s, 4, 1)] = 'doorOpen';
    const z = addUnit(s, 'z1', 'zombie', 9, 0, { heard: ground(0, 6) });
    pod(s, 'p1', [z]);
    addUnit(s, 's1', 'rookie', 5, 4);
    const run = sim(s);
    takeZombieTurn(run, z);
    // Through the open door, not to the noise.
    expect(z.pos.y).toBeGreaterThan(0);
    expect(Math.abs(z.pos.x - 5) + Math.abs(z.pos.y - 4)).toBeLessThan(9 + 4);
  });

  it('left undefended, the dead break in and go for the soldiers', () => {
    const s = createMission({ map: { kind: 'handmade', id: 'bastion' }, slots: [0], rng: new Rng(11), difficulty: 4 });
    // The squad shut in the barracks, out of the way: nobody to hold the wall.
    const barracks = [ground(10, 11), ground(11, 11), ground(12, 11), ground(13, 11), ground(10, 12), ground(11, 12)];
    s.units.filter((u) => u.team === 'xcom').forEach((u, i) => (u.pos = barracks[i]!));
    const ctx = { slot: 0 as Slot, connected: [0] as Slot[], rng: new Rng(5) };
    const events: GameEvent[] = [];
    for (let turn = 0; turn < SIEGE.hold && !s.outcome; turn++) {
      const r = executeCommand(s, { type: 'endTurn', ready: true }, ctx);
      expect(r.ok).toBe(true);
      if (r.ok) events.push(...r.events);
    }
    expect(of(events, 'smashed').length).toBeGreaterThan(0);
    expect(of(events, 'tileChanged').some((e) => e.kind === 'floor')).toBe(true);
    // Shut in, they are hunted down: bitten, and some of them lost.
    expect(of(events, 'abilityUsed').filter((e) => e.ability === 'claw').length).toBeGreaterThan(0);
    expect(of(events, 'died').some((e) => s.units.find((u) => u.id === e.unit)?.team === 'xcom')).toBe(true);
  });
});
