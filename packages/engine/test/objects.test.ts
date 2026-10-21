import { describe, expect, it } from 'vitest';
import { blastTiles } from '../src/abilities';
import { coverAgainst } from '../src/cover';
import { anchorOf, decorCode, objectAt, objectTiles } from '../src/decor';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { kindAt } from '../src/grid';
import { marketStock, rollRoom, roomAt } from '../src/loot';
import { Rng } from '../src/rng';
import { createMission } from '../src/setup';
import { executeCommand, Sim } from '../src/rules';
import type { Command } from '../src/protocol';
import type { Decor, GameState, Slot } from '../src/types';
import { addUnit, stateFrom } from './helpers';

// docs/MAPAS.md, phase 1: objects over several tiles (a car 1 × 3, a bed 1 × 2): the anchor says what it is, every
// other tile is a part leading to it; they are cover along all of them, and they go as one.

const ctx = (seed = 1) => ({ slot: 0 as Slot, connected: [0] as Slot[], rng: new Rng(seed) });

function run(s: GameState, cmd: Command, seed = 1): GameEvent[] {
  const r = executeCommand(s, cmd, ctx(seed));
  if (!r.ok) throw new Error(r.error);
  return r.events;
}

/** A car facing east, its nose on (x, y) and its body two tiles behind it: low cover, dressed. */
function parkCar(s: GameState, x: number, y: number): void {
  const decor = (s.decor ??= new Array<Decor>(s.width * s.height).fill(0));
  for (let k = 0; k < 3; k++) s.tiles[y * s.width + x - k] = 'low';
  decor[y * s.width + x] = decorCode('carFront', 1);
  // Each part faces the step towards the anchor: east.
  decor[y * s.width + x - 1] = decorCode('part', 1);
  decor[y * s.width + x - 2] = decorCode('part', 1);
}

describe('objects over several tiles (docs/MAPAS.md, phase 1)', () => {
  it('a part leads to its anchor; the anchor knows all its tiles; old cars and blocks are whole too', () => {
    const s = stateFrom(['........', '........', '........']);
    parkCar(s, 4, 1);
    const d = s.decor!;
    expect(anchorOf(d, s.width, 1 * 8 + 2)).toBe(1 * 8 + 4);
    expect(objectTiles(d, s.width, 1 * 8 + 4).sort()).toEqual([10, 11, 12]);
    expect(objectAt(d, s.width, 10)).toMatchObject({ kind: 'carFront', facing: 1, anchor: 12 });
    // A part that leads nowhere is nothing.
    d[0] = decorCode('part', 3);
    expect(anchorOf(d, s.width, 0)).toBe(-1);
    expect(objectAt(d, s.width, 0)).toBeNull();
    // A car of before: its two halves.
    d[2 * 8 + 6] = decorCode('carFront', 1);
    d[2 * 8 + 5] = decorCode('carRear', 1);
    expect(objectAt(d, s.width, 2 * 8 + 5)).toMatchObject({ kind: 'carFront', anchor: 2 * 8 + 6, tiles: [22, 21] });
    // A lorry of before: the block of its kind.
    for (const i of [0 * 8 + 6, 0 * 8 + 7]) d[i] = decorCode('truck', 0);
    expect(objectAt(d, s.width, 7)?.tiles).toEqual([6, 7]);
    // Anything else on one tile stands alone.
    d[2 * 8 + 1] = decorCode('bed', 2);
    expect(objectAt(d, s.width, 17)?.tiles).toEqual([17]);
  });

  it('a parked car is cover along its whole length', () => {
    const s = stateFrom(['........', '........', '........']);
    parkCar(s, 5, 1);
    // Behind each of its tiles, from across it.
    for (const x of [3, 4, 5]) expect(coverAgainst(s, { x, y: 2, z: 0 }, { x, y: 0 })).toBe(1);
  });

  it('a grenade that reaches one end of a car takes all of it; the partner replays it the same', () => {
    const s = stateFrom(['...........', '...........', '...........', '...........', '...........']);
    parkCar(s, 9, 2);
    addUnit(s, 's1', 'assault', 1, 2);
    const before = cloneState(s);
    // At the car's rear: a radius that never reaches its nose, four tiles further on.
    const reached = blastTiles(s, 'grenade', { x: 5, y: 2, z: 0 }, s.units[0]).map((p) => p.x);
    expect(reached).toContain(7);
    expect(reached).not.toContain(9);
    const events = run(s, { type: 'ability', unit: 's1', ability: 'grenade', tile: { x: 5, y: 2, z: 0 } });
    for (const x of [7, 8, 9]) expect(kindAt(s, { x, y: 2, z: 0 }), `${x}`).toBe('floor');
    for (const e of events) applyEvent(before, e);
    expect(before.tiles).toEqual(s.tiles);
  });

  it('in the base each piece stands alone: a grenade takes only the log piles it reaches, not the whole row', () => {
    const s = stateFrom(['....................', '....................', '....................', '....................', '....................']);
    const decor = (s.decor = new Array<Decor>(s.width * s.height).fill(0));
    for (let x = 2; x < 18; x++) {
      s.tiles[2 * s.width + x] = 'low';
      decor[2 * s.width + x] = decorCode('logPile', 0);
    }
    s.base = { pieces: [] };
    addUnit(s, 's1', 'assault', 2, 4);
    const reached = new Set(blastTiles(s, 'grenade', { x: 2, y: 1, z: 0 }, s.units[0]).filter((p) => p.y === 2).map((p) => p.x));
    run(s, { type: 'ability', unit: 's1', ability: 'grenade', tile: { x: 2, y: 1, z: 0 } });
    for (let x = 2; x < 18; x++) expect(kindAt(s, { x, y: 2, z: 0 }), `${x}`).toBe(reached.has(x) ? 'floor' : 'low');
    expect(kindAt(s, { x: 17, y: 2, z: 0 })).toBe('low');
  });

  it('a silenced patrol car takes the siren\'s tile into it, a part of it', () => {
    const s = stateFrom(['......', '......', '......']);
    s.mission = { ...s.mission, kind: 'sabotage', alarm: 'police', objectiveUnit: 'relay', turnsLeft: 5 };
    const relay = addUnit(s, 'relay', 'relay', 1, 1, { objective: true });
    // Its nose (sirenCar, facing east) and a part beyond it: with the siren's tile, 1 × 3.
    s.decor = new Array<Decor>(s.width * s.height).fill(0);
    s.tiles[1 * 6 + 2] = s.tiles[1 * 6 + 3] = 'high';
    s.decor[1 * 6 + 2] = decorCode('sirenCar', 1);
    s.decor[1 * 6 + 3] = decorCode('part', 3);
    addUnit(s, 's1', 'assault', 0, 0);
    const sim = new Sim(s, new Rng(1), [0]);
    sim.damage(relay, 99, 0);
    expect(kindAt(s, relay.pos)).toBe('high');
    expect(objectAt(s.decor, s.width, 1 * 6 + 1)).toMatchObject({ kind: 'sirenCar', anchor: 1 * 6 + 2 });
    expect(objectTiles(s.decor, s.width, 1 * 6 + 2).sort()).toEqual([7, 8, 9]);
  });

  it('a room is the kind its pieces make it, each counted once whatever tiles it takes', () => {
    // A bed over two tiles and two desks: an office, not a bedroom (by tiles it would be two and two).
    const s = stateFrom(['#######', '#.....#', '#.....#', '#.....#', '#######']);
    s.ground = s.tiles.map(() => 2);
    s.decor = new Array<Decor>(s.width * s.height).fill(0);
    const put = (x: number, y: number, code: Decor) => {
      s.tiles[y * s.width + x] = 'low';
      s.decor![y * s.width + x] = code;
    };
    put(1, 1, decorCode('bed', 2));
    put(1, 2, decorCode('part', 0));
    put(5, 1, decorCode('desk', 2));
    put(5, 3, decorCode('desk', 0));
    expect(roomAt(s, { x: 3, y: 2, z: 0 })?.spot).toBe('office');
  });
});

describe('supermarkets (playtest 21/10, #19)', () => {
  it('a stocked one always gives food; a picked one is a shop, maybe nothing', () => {
    const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 2 }, slots: [0], rng: new Rng(2) });
    const m = s.markets![0]!;
    const room = roomAt(s, { x: m.x + 2, y: m.y + 2, z: 0 })!;
    expect(room).not.toBeNull();
    for (const stock of ['full', 'picked'] as const) {
      m.stock = stock;
      expect(marketStock(s, room)).toBe(stock);
    }
    const rolls = (stock: 'full' | 'picked') => Array.from({ length: 60 }, (_, k) => rollRoom(room, 2, new Rng(k + 1), false, false, stock));
    for (const loot of rolls('full')) {
      expect(loot?.food).toBeGreaterThanOrEqual(6);
      expect(loot?.food).toBeLessThanOrEqual(10);
    }
    expect(rolls('picked').some((loot) => loot === null)).toBe(true);
    // Elsewhere, no stock.
    const out = roomAt(s, s.units.find((u) => u.team === 'xcom')!.pos);
    if (out) expect(marketStock(s, out)).toBeNull();
  });
});
