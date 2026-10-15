import { describe, expect, it } from 'vitest';
import { decorCode, decorOf } from '../src/decor';
import { cloneState } from '../src/events';
import { DIRS4, kindAt, nodeOf, posOf, slabAt } from '../src/grid';
import { roomAt } from '../src/loot';
import { COST_CLIMB, COST_ORTHO, stepBetween } from '../src/path';
import { normalizeMission } from '../src/setup';
import { repairStairAccess } from '../src/stairAccess';
import { addUnit, stateFrom, withLevels } from './helpers';

/** A saved two-storey house, with an old flight against one outer wall. */
function house(up: number) {
  const rows = ['#######', '#.....#', '#.....#', '#.....#', '#.....#', '#.....#', '#######'];
  const s = withLevels(stateFrom(rows), [rows, rows.map((row) => row.replaceAll('.', 'R'))]);
  s.mapId = 'gen-city-saved';
  s.ground = s.tiles.map(() => 2);
  s.storeys = s.tiles.map(() => 2);
  s.decor = s.tiles.map(() => 0);
  const d = DIRS4[up]!;
  const at = { x: 3 - d.x * 2, y: 3 - d.y * 2, z: 0 };
  const i = at.y * s.width + at.x;
  s.tiles[i] = 'ladder';
  s.decor[i] = decorCode('stairs', up);
  s.levels![0]!.tiles[i] = null;
  return { s, at, i };
}

describe('saved stairs', () => {
  it('moves wall-adjacent flights in all four directions and keeps both floors accessible', () => {
    for (let up = 0; up < 4; up++) {
      const { s, at, i } = house(up);
      const soldier = addUnit(s, 's1', 'assault', at.x, at.y);
      s.dropped = [{ id: 'drop', pos: { x: 3, y: 3, z: 1 }, loot: { medicines: 1 } }];
      const beforeUnits = cloneState(s).units;
      const beforeMission = cloneState(s).mission;
      const oldRoom = roomAt(s, { x: 3, y: 3, z: 1 })!;
      s.roomsSearched = [oldRoom.id];
      normalizeMission(s);
      expect(s.tiles[i]).toBe('floor');
      expect(slabAt(s, at.x, at.y, 1)).toBe(true);
      const j = s.decor!.findIndex((d) => decorOf(d)?.kind === 'stairs');
      expect(j).not.toBe(i);
      const p = posOf(s, j);
      const facing = decorOf(s.decor![j])!.facing;
      const d = DIRS4[facing]!;
      const foot = { x: p.x - d.x, y: p.y - d.y, z: 0 };
      const landing = { x: p.x + d.x, y: p.y + d.y, z: 1 };
      for (const side of DIRS4) for (const z of [0, 1]) expect(kindAt(s, { x: p.x + side.x, y: p.y + side.y, z })).toBe('floor');
      expect(kindAt(s, foot)).toBe('floor');
      expect(slabAt(s, p.x, p.y, 1)).toBe(false);
      expect(stepBetween(s, foot, p)).toBe(COST_ORTHO);
      expect(stepBetween(s, p, landing)).toBe(COST_ORTHO + COST_CLIMB);
      expect(stepBetween(s, landing, p)).toBe(COST_ORTHO + COST_CLIMB);
      expect(s.roomsSearched).toContain(roomAt(s, landing)!.id);
      expect(s.units).toEqual(beforeUnits);
      expect(s.mission).toEqual(beforeMission);
      expect(soldier.pos).toEqual(at);
      expect(kindAt(s, s.dropped[0]!.pos)).toBe('floor');
      const fixed = cloneState(s);
      normalizeMission(s);
      expect(s).toEqual(fixed);
    }
  });

  it('chooses another stairwell when a unit or the objective occupies the nearest one', () => {
    const { s } = house(1);
    const soldier = addUnit(s, 's1', 'assault', 2, 3, { z: 1 });
    s.mission.item = { pos: { x: 2, y: 2, z: 1 }, carrier: null, evacuated: false };
    repairStairAccess(s);
    expect(kindAt(s, soldier.pos)).toBe('floor');
    expect(kindAt(s, s.mission.item!.pos!)).toBe('floor');
    expect(s.decor!.some((d, i) => decorOf(d)?.kind === 'stairs' && i !== 3 * s.width + 1)).toBe(true);
  });

  it('repairs a flight starting on an upper floor as well as on the ground', () => {
    const { s, i } = house(1);
    const rows = ['#######', '#.....#', '#.....#', '#.....#', '#.....#', '#.....#', '#######'];
    const above = withLevels(stateFrom(rows), [rows, rows, rows]).levels!;
    s.levels = above;
    s.tiles[i] = 'floor';
    s.decor![i] = 0;
    s.levels[0]!.tiles[i] = 'ladder';
    s.levels[0]!.decor = s.tiles.map(() => 0);
    s.levels[0]!.decor![i] = decorCode('stairs', 1);
    s.levels[1]!.tiles[i] = null;
    repairStairAccess(s);
    expect(s.levels[0]!.tiles[i]).toBe('floor');
    expect(s.levels[1]!.tiles[i]).toBe('floor');
    const j = s.levels[0]!.decor!.findIndex((d) => decorOf(d)?.kind === 'stairs');
    const p = { ...posOf(s, j), z: 1 };
    const d = DIRS4[decorOf(s.levels[0]!.decor![j])!.facing]!;
    const landing = { x: p.x + d.x, y: p.y + d.y, z: 2 };
    expect(s.levels[1]!.tiles[j]).toBeNull();
    expect(stepBetween(s, p, landing)).toBe(COST_ORTHO + COST_CLIMB);
    expect(stepBetween(s, landing, p)).toBe(COST_ORTHO + COST_CLIMB);
  });

  it('leaves a cramped saved room and handmade maps intact when no safe relocation fits', () => {
    const s = withLevels(stateFrom(['#####', '#S..#', '#####']), [['#####', '# ..#', '#####']]);
    s.mapId = 'gen-city-cramped';
    s.ground = s.tiles.map(() => 2);
    s.storeys = s.tiles.map(() => 2);
    s.decor = s.tiles.map(() => 0);
    s.decor[nodeOf(s, { x: 1, y: 1, z: 0 })] = decorCode('stairs', 1);
    const before = cloneState(s);
    repairStairAccess(s);
    expect(s).toEqual(before);
    const handmade = house(1).s;
    handmade.mapId = 'torre';
    const original = cloneState(handmade);
    repairStairAccess(handmade);
    expect(handmade).toEqual(original);
  });
});
