import { describe, expect, it } from 'vitest';
import { breaksFloor, riseSpot } from '../src/abilities';
import { decorCode } from '../src/decor';
import { applyEvent, cloneState } from '../src/events';
import { bearsFlight, canStopAt, flightAt, flightFoot, flightLanding, hasLongFlights, longFlightAt, midFlight, restingPlace } from '../src/flights';
import { heightAt, nodeOf, slabAt, tileIndex } from '../src/grid';
import { canSee } from '../src/los';
import { buildPath, COST_CLIMB, COST_DIAG, COST_ORTHO, computeReach, destinations, stepBetween, stepsFrom } from '../src/path';
import { Rng } from '../src/rng';
import { executeCommand, Sim } from '../src/rules';
import { createMission, normalizeMission } from '../src/setup';
import type { GameState, Pos, Slot, Unit } from '../src/types';
import { addUnit, stateFrom, withLevels } from './helpers';

// docs/MAPAS.md, phase 2: a building's stairs are a flight of several tiles (3, 2 in small houses), climbed from its
// foot to its landing a storey up, where nobody stops (Arturo, 19/10: «solo se entra por el pie y nadie se para a mitad»).

const at = (x: number, y: number, z = 0): Pos => ({ x, y, z });
const ctx = (seed = 1) => ({ slot: 0 as Slot, connected: [0] as Slot[], rng: new Rng(seed) });

/**
 * A two-storey hall (x 1–12, y 1–5) with a flight of three up its middle row, climbing east: its tiles (4,3), (5,3)
 * and (6,3), its foot (3,3), its landing (7,3) on the first floor, the stairwell over the flight. A door at (12,3)
 * to the street.
 */
function hall(length = 3): GameState {
  const s = stateFrom([
    '..............',
    '.############.',
    '.#..........#.',
    '.#..........+.',
    '.#..........#.',
    '.############.',
    '..............',
  ]);
  withLevels(s, [
    [
      '              ',
      ' ############ ',
      ' #..........# ',
      ' #..........# ',
      ' #..........# ',
      ' ############ ',
      '              ',
    ],
  ]);
  s.decor = new Array(s.width * s.height).fill(0);
  for (let n = 0; n < length; n++) {
    const i = tileIndex(s, 6 - n, 3);
    s.tiles[i] = 'ladder';
    s.decor[i] = decorCode(n === 0 ? 'stairs' : 'part', 1);
    s.levels![0]!.tiles[i] = null;
  }
  return s;
}

/** Whoever of `s` stands on a flight of several tiles (nobody should). */
const onFlights = (s: GameState) => s.units.filter((u) => u.alive && midFlight(s, u.pos));

describe('a flight of stairs of several tiles', () => {
  it('is its tiles in a line, its foot behind the lowest and its landing in front of the top, a storey up', () => {
    const s = hall();
    for (const [x, step] of [[4, 0], [5, 1], [6, 2]] as const) {
      const on = flightAt(s, at(x, 3))!;
      expect(on.step).toBe(step);
      expect(on.flight).toEqual({ z: 0, up: 1, tiles: [{ x: 4, y: 3 }, { x: 5, y: 3 }, { x: 6, y: 3 }] });
    }
    expect(flightFoot(flightAt(s, at(5, 3))!.flight)).toEqual(at(3, 3));
    expect(flightLanding(flightAt(s, at(5, 3))!.flight)).toEqual(at(7, 3, 1));
    expect(flightAt(s, at(3, 3))).toBeNull();
    expect(hasLongFlights(s)).toBe(true);
    // The stairwell over all of it.
    for (const x of [4, 5, 6]) expect(slabAt(s, x, 3, 1)).toBe(false);
  });

  it('is entered only from its foot, the way it climbs, and left only by its landing or its foot', () => {
    const s = hall();
    // Foot to landing: a tile's walk each step, the one up onto the landing too (no climb on top).
    expect(stepBetween(s, at(3, 3), at(4, 3))).toBe(COST_ORTHO);
    expect(stepBetween(s, at(4, 3), at(5, 3))).toBe(COST_ORTHO);
    expect(stepBetween(s, at(5, 3), at(6, 3))).toBe(COST_ORTHO);
    expect(stepBetween(s, at(6, 3), at(7, 3, 1))).toBe(COST_ORTHO);
    // And back down.
    expect(stepBetween(s, at(7, 3, 1), at(6, 3))).toBe(COST_ORTHO);
    expect(stepBetween(s, at(6, 3), at(5, 3))).toBe(COST_ORTHO);
    expect(stepBetween(s, at(4, 3), at(3, 3))).toBe(COST_ORTHO);
    // Never from a side, nor diagonally, nor across a banister's corner.
    for (const x of [4, 5, 6]) {
      expect(stepBetween(s, at(x, 2), at(x, 3))).toBeNull();
      expect(stepBetween(s, at(x, 3), at(x, 4))).toBeNull();
    }
    expect(stepBetween(s, at(3, 2), at(4, 3))).toBeNull();
    expect(stepBetween(s, at(4, 3), at(5, 4))).toBeNull();
    expect(stepBetween(s, at(4, 2), at(3, 3))).toBeNull();
    expect(stepBetween(s, at(2, 2), at(3, 3))).toBe(COST_DIAG);
    // Not onto the top from in front of it on its own floor, nor off it there; not off the bottom onto the landing's floor.
    expect(stepBetween(s, at(7, 3), at(6, 3))).toBeNull();
    expect(stepBetween(s, at(6, 3), at(7, 3))).toBeNull();
    expect(stepBetween(s, at(4, 3), at(3, 3, 1))).toBeNull();
    // Upstairs, nobody drops into the stairwell from beside it; only the landing leads down.
    for (const x of [4, 5, 6]) expect(stepsFrom(s, at(x, 2, 1), 0, 1)).toEqual([]);
    expect(stepsFrom(s, at(3, 3, 1), 1, 0)).toEqual([]);
    expect(stepsFrom(s, at(7, 3, 1), -1, 0)).toEqual([{ to: at(6, 3), cost: COST_ORTHO }]);
  });

  it('costs as four tiles from its foot to its landing (three for a flight of two): one more than a flight of one', () => {
    for (const [length, tiles] of [[3, 4], [2, 3]] as const) {
      const s = hall(length);
      const foot = flightFoot(flightAt(s, at(6, 3))!.flight);
      const u = addUnit(s, 's1', 'assault', foot.x, foot.y);
      expect(computeReach(s, u, 100).cost.get(nodeOf(s, at(7, 3, 1)))).toBe(tiles * COST_ORTHO);
    }
    // A flight of one tile (of before), from the floor in front of its foot: a tile and a climb.
    const old = hall(1);
    const u = addUnit(old, 's1', 'assault', 5, 3);
    expect(computeReach(old, u, 100).cost.get(nodeOf(old, at(7, 3, 1)))).toBe(2 * COST_ORTHO + COST_CLIMB);
  });

  it('is no place to stop: never a destination, nor a path end, a step-out or a place to rise or drop things', () => {
    const s = hall();
    const u = addUnit(s, 's1', 'assault', 2, 3);
    const reach = computeReach(s, u, 100);
    // Walked across (the way up passes it), never stopped on.
    for (const x of [4, 5, 6]) {
      expect(reach.cost.has(nodeOf(s, at(x, 3)))).toBe(true);
      expect(canStopAt(s, at(x, 3))).toBe(false);
      expect(buildPath(s, u, reach, at(x, 3))).toBeNull();
    }
    const ends = destinations(s, u, reach).map((d) => d.pos);
    expect(ends.some((p) => midFlight(s, p))).toBe(false);
    expect(ends).toContainEqual(at(3, 3));
    expect(ends).toContainEqual(at(7, 3, 1));
    expect(buildPath(s, u, reach, at(8, 3, 1))).toEqual([at(3, 3), at(4, 3), at(5, 3), at(6, 3), at(7, 3, 1), at(8, 3, 1)]);
    // A move that can't reach the landing ends short of the flight: its foot is as far as it goes.
    const short = computeReach(s, u, 3 * COST_ORTHO);
    expect(destinations(s, u, short).map((d) => d.pos).filter((p) => p.x > 3 && p.y === 3)).toEqual([]);
    // What a soldier falling on it carried lands at its nearer end.
    expect(restingPlace(s, at(4, 3))).toEqual(at(3, 3));
    expect(restingPlace(s, at(6, 3))).toEqual(at(7, 3, 1));
    // A body on it rises at its nearer free end.
    const body = addUnit(s, 'a1', 'zombie', 5, 3, { alive: false });
    expect(riseSpot(s, body)).toEqual(at(7, 3, 1));
    body.pos = at(4, 3);
    expect(riseSpot(s, body)).toEqual(at(3, 3));
    addUnit(s, 's2', 'assault', 3, 3);
    expect(riseSpot(s, body)).toEqual(at(7, 3, 1));
  });

  it('a walk stopped on it goes back down to where it could stop; a patrol that sees the squad from it stops off it', () => {
    const s = hall();
    const u = addUnit(s, 's1', 'assault', 2, 3);
    addUnit(s, 'a9', 'zombie', 0, 6);
    const path = buildPath(s, u, computeReach(s, u, 100), at(9, 3, 1))!;
    // Somebody comes onto the landing after the walk was planned.
    const other = addUnit(s, 's2', 'assault', 7, 3, { z: 1 });
    const client = cloneState(s);
    const sim = new Sim(s, new Rng(1), [0]);
    sim.moveAlong(u, path, 1);
    expect(u.pos).toEqual(at(3, 3));
    expect(onFlights(s)).toEqual([]);
    for (const e of sim.events) applyEvent(client, e);
    expect(client.units.find((x) => x.id === 's1')!.pos).toEqual(at(3, 3));
    s.units = s.units.filter((x) => x !== other);
    u.pos = at(11, 2);

    // A zombie of a pod on patrol going up: it sees a soldier from the flight's second tile, and stops on the landing.
    const z = addUnit(s, 'a1', 'zombie', 2, 2);
    z.podId = 'p1';
    s.pods.push({ id: 'p1', active: false, unitIds: [z.id], patrol: [], patrolIndex: 0 });
    const walk = buildPath(s, z, computeReach(s, z, 100), at(10, 3, 1))!;
    const patrol = new Sim(s, new Rng(1), [0]);
    patrol.spottedFrom = (_, p) => (p.x >= 5 ? u : undefined);
    patrol.moveAlong(z, walk, 0);
    expect(z.pos).toEqual(at(7, 3, 1));
    expect(s.pods[0]!.active).toBe(true);
  });

  it('holds its slabs: a grenade breaks neither its landing, nor its foot nor its tiles upstairs', () => {
    const s = hall();
    // A storey more: a flight from the first floor to a roof, over the landing's floor.
    s.levels!.push({ tiles: s.levels![0]!.tiles.map((k) => (k === null ? 'floor' : k)) });
    expect(bearsFlight(s, at(7, 3, 1))).toBe(true);
    expect(breaksFloor(s, 'grenade', at(7, 3, 1))).toBe(false);
    expect(bearsFlight(s, at(8, 3, 1))).toBe(false);
    expect(breaksFloor(s, 'grenade', at(8, 3, 1))).toBe(true);
    // One upstairs: its foot and its tiles bear it too.
    const i = (x: number) => tileIndex(s, x, 4);
    s.levels![0]!.decor = new Array(s.width * s.height).fill(0);
    for (const [x, kind] of [[5, 'part'], [6, 'part'], [7, 'stairs']] as const) {
      s.levels![0]!.tiles[i(x)] = 'ladder';
      s.levels![0]!.decor[i(x)] = decorCode(kind, 1);
      s.levels![1]!.tiles[i(x)] = null;
    }
    expect([4, 5, 6, 7].every((x) => bearsFlight(s, at(x, 4, 1)))).toBe(true);
    expect(bearsFlight(s, at(8, 4, 2))).toBe(true);
    expect(breaksFloor(s, 'grenade', at(5, 4, 1))).toBe(false);
  });

  it('upstairs one looks down the stairwell, the whole of its length; the floor round it hides the hall below', () => {
    const s = hall();
    const up = addUnit(s, 's1', 'assault', 5, 2, { z: 1 });
    expect(canSee(s, up, addUnit(s, 'a1', 'zombie', 3, 3, { facing: 1 }))).toBe(true);
    expect(canSee(s, up, addUnit(s, 'a2', 'zombie', 10, 2, { facing: 1 }))).toBe(false);
  });
});

describe('the dead and the living on flights', () => {
  /** An active pod of `units`. */
  const active = (s: GameState, id: string, units: Unit[]) => {
    for (const u of units) u.podId = id;
    s.pods.push({ id, active: true, unitIds: units.map((u) => u.id), patrol: [], patrolIndex: 0 });
  };

  it('a horde upstairs comes down the flight to a soldier below, and nobody ends a turn on it', () => {
    const s = hall();
    s.activeTeam = 'alien';
    const soldier = addUnit(s, 's1', 'assault', 10, 4);
    const dead = [addUnit(s, 'a1', 'zombie', 10, 2, { z: 1 }), addUnit(s, 'a2', 'zombie', 11, 4, { z: 1 }), addUnit(s, 'a3', 'zombie', 9, 4, { z: 1 }), addUnit(s, 'a4', 'zombie', 2, 2, { z: 1 })];
    active(s, 'p1', dead);
    for (const z of dead) z.heard = { ...soldier.pos };
    let down = 0;
    for (let turn = 0; turn < 6; turn++) {
      soldier.hp = soldier.maxHp;
      const sim = new Sim(s, new Rng(turn), [0]);
      for (const z of dead) z.ap = 2;
      sim.runAlienTurn();
      expect(onFlights(s), `turn ${turn}`).toEqual([]);
      down = dead.filter((z) => z.alive && z.pos.z === 0).length;
      if (down >= 2) break;
    }
    expect(down).toBeGreaterThanOrEqual(2);
  });

  it('a living enemy upstairs goes round by the flight to a soldier below it, and ends no move on it', () => {
    const s = hall();
    s.activeTeam = 'alien';
    const soldier = addUnit(s, 's1', 'assault', 10, 4);
    const lancer = addUnit(s, 'a1', 'lancer', 10, 3, { z: 1 });
    active(s, 'p1', [lancer]);
    lancer.heard = { ...soldier.pos };
    for (let turn = 0; turn < 4 && lancer.pos.z === 1; turn++) {
      lancer.ap = 2;
      new Sim(s, new Rng(turn), [0]).runAlienTurn();
      expect(onFlights(s)).toEqual([]);
    }
    expect(heightAt(s, lancer.pos)).toBe(0);
  });

  it('the Hinchado, too slow to climb a flight of three in one action, waits at its foot and climbs it with both', () => {
    const s = hall();
    s.activeTeam = 'alien';
    s.mission.siege = { hold: 99 };
    const soldier = addUnit(s, 's1', 'assault', 10, 3, { z: 1 });
    const bloater = addUnit(s, 'a1', 'bloater', 2, 2);
    active(s, 'p1', [bloater]);
    for (let turn = 0; turn < 4 && bloater.pos.z === 0; turn++) {
      soldier.hp = soldier.maxHp;
      bloater.ap = 2;
      new Sim(s, new Rng(turn), [0]).runAlienTurn();
      expect(onFlights(s), `turn ${turn}`).toEqual([]);
    }
    expect(bloater.pos.z).toBe(1);
  });
});

describe('maps of before', () => {
  it('a mission saved with flights of one tile plays as it did: on them from any side at the bottom, a climb, a place to stand', () => {
    const s = hall(1);
    const saved = normalizeMission(cloneState(s));
    expect(hasLongFlights(saved)).toBe(false);
    expect(longFlightAt(saved, at(6, 3))).toBeNull();
    expect(flightAt(saved, at(6, 3))!.flight.tiles).toHaveLength(1);
    expect(canStopAt(saved, at(6, 3))).toBe(true);
    expect(stepBetween(saved, at(6, 2), at(6, 3))).toBe(COST_ORTHO);
    expect(stepBetween(saved, at(6, 3), at(7, 3, 1))).toBe(COST_ORTHO + COST_CLIMB);
    expect(stepBetween(saved, at(7, 3, 1), at(6, 3))).toBe(COST_ORTHO + COST_CLIMB);
    const u = addUnit(saved, 's1', 'assault', 2, 2);
    addUnit(saved, 'a1', 'zombie', 0, 6);
    const r = executeCommand(saved, { type: 'move', unit: 's1', to: at(6, 3) }, ctx());
    expect(r.ok).toBe(true);
    expect(u.pos).toEqual(at(6, 3));
    const up = executeCommand(saved, { type: 'move', unit: 's1', to: at(9, 3, 1) }, ctx());
    expect(up.ok).toBe(true);
    expect(u.pos).toEqual(at(9, 3, 1));
  });

  it('«La torre» keeps its flights of one tile; a generated mission saved and loaded keeps its flights whole', () => {
    const torre = createMission({ map: { kind: 'handmade', id: 'torre' }, slots: [0], rng: new Rng(1) });
    expect(hasLongFlights(torre)).toBe(false);
    const gen = createMission({ map: { kind: 'generated', biome: 'city', seed: 707 }, slots: [0], rng: new Rng(1), kind: 'hack' });
    expect(hasLongFlights(gen)).toBe(true);
    const loaded = normalizeMission(JSON.parse(JSON.stringify(gen)) as GameState);
    expect(loaded.tiles).toEqual(gen.tiles);
    expect(loaded.levels).toEqual(gen.levels);
    expect(loaded.decor).toEqual(gen.decor);
    // Nobody starts on one.
    expect(onFlights(gen)).toEqual([]);
  });
});
