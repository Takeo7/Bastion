import { describe, expect, it } from 'vitest';
import { abilityBlocker, breachedWalls, breaksFloor } from '../src/abilities';
import { FALL_DAMAGE, SPARE_MAGS } from '../src/content';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { rollLoot } from '../src/loot';
import type { Command } from '../src/protocol';
import { Rng } from '../src/rng';
import { executeCommand } from '../src/rules';
import { normalizeMission } from '../src/setup';
import type { GameState, Slot } from '../src/types';
import { decorOf } from '../src/decor';
import { kindAt, slabAt } from '../src/grid';
import { COST_DROP, COST_ORTHO, stepBetween } from '../src/path';
import { addUnit, stateFrom, withLevels } from './helpers';

// Phase 7 (docs/FASE7.md): magazines are counted (§3), walls (§4) and floors (§5) break.

const ctx = (seed = 1) => ({ slot: 0 as Slot, connected: [0] as Slot[], rng: new Rng(seed) });

function run(s: GameState, cmd: Command, seed = 1): GameEvent[] {
  const r = executeCommand(s, cmd, ctx(seed));
  if (!r.ok) throw new Error(r.error);
  return r.events;
}

const reload = (unit: string): Command => ({ type: 'ability', unit, ability: 'reload' });

describe('counted magazines', () => {
  it('a soldier takes its spares into the mission; enemies and weapons without a magazine count none', () => {
    const s = stateFrom(['.....']);
    expect(addUnit(s, 's1', 'assault', 0, 0).mags).toBe(SPARE_MAGS);
    expect(addUnit(s, 'a1', 'trooper', 4, 0).mags).toBeUndefined();
    expect(addUnit(s, 'z1', 'zombie', 3, 0).mags).toBeUndefined();
  });

  it('reloading spends a spare, and with none left the soldier cannot reload', () => {
    const s = stateFrom(['.....']);
    const u = addUnit(s, 's1', 'assault', 0, 0, { ammo: 0, mags: 1 });
    run(s, reload('s1'));
    expect(u.ammo).toBeGreaterThan(0);
    expect(u.mags).toBe(0);
    u.ammo = 0;
    u.ap = 2;
    expect(abilityBlocker(s, u, 'reload')).toBe('No quedan cargadores.');
    expect(abilityBlocker(s, u, 'shoot')).toBe('Sin munición y sin cargadores.');
    expect(executeCommand(s, reload('s1'), ctx()).ok).toBe(false);
  });

  it('an enemy without the count reloads as before', () => {
    const s = stateFrom(['.....']);
    const a = addUnit(s, 'a1', 'trooper', 4, 0, { ammo: 0 });
    a.ap = 2;
    expect(abilityBlocker(s, a, 'reload')).toBeNull();
  });

  it('a search may turn up a magazine, only for a searcher who counts them, and it goes to the spares', () => {
    let found = 0;
    for (let seed = 0; seed < 400; seed++) {
      const counted = rollLoot('crates', 2, new Rng(seed), false, true);
      const plain = rollLoot('crates', 2, new Rng(seed), false, false);
      if (counted?.mags) {
        found++;
        // For anyone else, that same roll turns up nothing; every other roll is the same.
        expect(plain).toBeNull();
      } else expect(plain).toEqual(counted);
    }
    expect(found).toBeGreaterThan(10);
    const s = stateFrom(['.....']);
    const u = addUnit(s, 's1', 'assault', 0, 0, { mags: 0 });
    applyEvent(s, { t: 'searched', unit: 's1', tile: { x: 1, y: 0, z: 0 }, loot: { mags: 1 } });
    expect(u.mags).toBe(1);
    expect(u.backpack ?? []).toEqual([]);
  });

  it('a mission saved before magazines were counted gives the squad its spares', () => {
    const s = stateFrom(['.....']);
    const u = addUnit(s, 's1', 'assault', 0, 0);
    const a = addUnit(s, 'a1', 'trooper', 4, 0);
    delete u.mags;
    const old = normalizeMission(cloneState(s));
    expect(old.units.find((o) => o.id === u.id)!.mags).toBe(SPARE_MAGS);
    expect(old.units.find((o) => o.id === a.id)!.mags).toBeUndefined();
  });
});

describe('walls that break', () => {
  /** A room (x 1–7, y 1–5) with the street round it. */
  const room = () =>
    stateFrom([
      '.........',
      '.#######.',
      '.#.....#.',
      '.#.....#.',
      '.#.....#.',
      '.#######.',
      '.........',
    ]);
  const at = (x: number, y: number, z = 0) => ({ x, y, z });
  const throwAt = (s: GameState, x: number, y: number, z = 0): GameEvent[] =>
    run(s, { type: 'ability', unit: 's1', ability: 'grenade', tile: at(x, y, z) });

  it('a grenade leaves the walls it reaches as rubble, low cover, and nothing further off', () => {
    const s = room();
    addUnit(s, 's1', 'assault', 6, 4);
    const before = cloneState(s);
    const events = throwAt(s, 2, 3);
    const broken = events.filter((e): e is Extract<GameEvent, { t: 'tileChanged' }> => e.t === 'tileChanged' && kindAt(before, e.pos) === 'wall');
    expect(broken.length).toBeGreaterThan(0);
    expect(broken.every((e) => e.kind === 'low')).toBe(true);
    // The west wall beside the blast is rubble; the far east wall stands.
    expect(kindAt(s, at(1, 3))).toBe('low');
    expect(kindAt(s, at(7, 3))).toBe('wall');
    // It keeps the look of a breached wall, so a reloaded mission draws it so too.
    expect(decorOf(s.decor![1 + 3 * s.width])?.kind).toBe('breach');
    // The partner, replaying the events, sees the same walls.
    for (const e of events) applyEvent(before, e);
    expect(before.tiles).toEqual(s.tiles);
    expect(before.decor).toEqual(s.decor);
  });

  it('smoke, a flashbang or a molotov leave the walls alone', () => {
    const s = room();
    for (const ability of ['smoke', 'flashbang', 'fireball'] as const) expect(breachedWalls(s, ability, at(2, 3))).toEqual([]);
    expect(breachedWalls(s, 'launch', at(2, 3)).length).toBeGreaterThan(0);
  });

  it('upstairs, the walls of that floor break and the ones below stand', () => {
    const s = withLevels(room(), [
      [
        '         ',
        ' ####### ',
        ' #.....# ',
        ' #.....# ',
        ' #.....# ',
        ' ####### ',
        '         ',
      ],
    ]);
    const walls = breachedWalls(s, 'grenade', at(2, 3, 1));
    expect(walls.length).toBeGreaterThan(0);
    expect(walls.every((p) => p.z === 1)).toBe(true);
  });
});

describe('floors that break', () => {
  /** A room (x 1–7, y 1–5) with a first floor over it, walled the same. */
  const house = () =>
    withLevels(
      stateFrom([
        '.........',
        '.#######.',
        '.#.....#.',
        '.#.....#.',
        '.#.....#.',
        '.#######.',
        '.........',
      ]),
      [
        [
          '         ',
          ' ####### ',
          ' #.....# ',
          ' #.....# ',
          ' #.....# ',
          ' ####### ',
          '         ',
        ],
      ],
    );
  const at = (x: number, y: number, z = 0) => ({ x, y, z });

  it('a grenade upstairs opens the floor where it lands: who stands there falls a storey, hurt through the armour', () => {
    const s = house();
    addUnit(s, 's1', 'assault', 6, 4, { z: 1 });
    const z = addUnit(s, 'z1', 'zombie', 3, 3, { z: 1, hp: 50, maxHp: 50, armor: 2 });
    const before = cloneState(s);
    const events = run(s, { type: 'ability', unit: 's1', ability: 'grenade', tile: at(3, 3, 1) });
    expect(slabAt(s, 3, 3, 1)).toBe(false);
    expect(slabAt(s, 4, 3, 1)).toBe(true);
    const i = events.findIndex((e) => e.t === 'fell');
    expect(events[i]).toEqual({ t: 'fell', unit: 'z1', from: at(3, 3, 1), to: at(3, 3) });
    expect(events.findIndex((e) => e.t === 'floorBroken')).toBeLessThan(i);
    expect(events[i + 1]).toMatchObject({ t: 'damaged', unit: 'z1', amount: FALL_DAMAGE, mitigated: 0 });
    expect(z.pos).toEqual(at(3, 3));
    // The partner, replaying the events, has the same hole and the zombie in the same place.
    for (const e of events) applyEvent(before, e);
    expect(before.levels).toEqual(s.levels);
    expect(before.units.find((u) => u.id === 'z1')!.pos).toEqual(at(3, 3));
    // The hole is a way down, like an edge.
    expect(stepBetween(s, at(2, 3, 1), at(3, 3))).toBe(COST_ORTHO + COST_DROP);
  });

  it('who falls onto someone lands on the nearest free place beside them', () => {
    const s = house();
    addUnit(s, 's1', 'assault', 6, 4, { z: 1 });
    addUnit(s, 'z1', 'zombie', 3, 3, { z: 1, hp: 50, maxHp: 50 });
    addUnit(s, 'z2', 'zombie', 3, 3, { hp: 50, maxHp: 50 });
    const events = run(s, { type: 'ability', unit: 's1', ability: 'grenade', tile: at(3, 3, 1) });
    const fell = events.find((e): e is Extract<GameEvent, { t: 'fell' }> => e.t === 'fell')!;
    expect(fell.to.z).toBe(0);
    expect(Math.max(Math.abs(fell.to.x - 3), Math.abs(fell.to.y - 3))).toBe(1);
  });

  it('only a frag grenade on a storey: not on the ground, not smoke', () => {
    const s = house();
    expect(breaksFloor(s, 'grenade', at(3, 3, 1))).toBe(true);
    expect(breaksFloor(s, 'grenade', at(3, 3))).toBe(false);
    expect(breaksFloor(s, 'smoke', at(3, 3, 1))).toBe(false);
    expect(breaksFloor(s, 'flashbang', at(3, 3, 1))).toBe(false);
  });
});
