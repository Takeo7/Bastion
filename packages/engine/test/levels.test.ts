import { describe, expect, it } from 'vitest';
import { allyTargets, blastTiles, canHackFrom, inSilence, inSmoke } from '../src/abilities';
import { previewShot } from '../src/aim';
import { coverAgainst, coverSides } from '../src/cover';
import { decorCode, decorOf } from '../src/decor';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import {
  blocksSightAt,
  BODY_HEIGHT,
  canStandOn,
  coverFrom,
  DIRS4,
  DIRS8,
  elevAt,
  EYE_HEIGHT,
  heightAt,
  isWalkable,
  kindAt,
  levelCount,
  nodeOf,
  posOf,
  roofSide,
  samePos,
  sameTile,
  slabAt,
  storeysAt,
  surfaceAt,
  tileAt,
  isWindow,
  tileIndex,
  unitAt,
} from '../src/grid';
import { canSee, floorLevels, floorView, isLit, lightTiles, lineClear, MAX_WINDOWS_SEEN_THROUGH, peekOrigins, sightOf, teamVisibility } from '../src/los';
import { MAPS, parseMap } from '../src/maps';
import { buildPath, COST_CLIMB, COST_DIAG, COST_DROP, COST_ORTHO, COST_VAULT, computeReach, destinations, stepBetween, stepCost, stepsFrom } from '../src/path';
import { campaignCommandSchema, commandSchema } from '../src/protocol';
import { Rng } from '../src/rng';
import { duet, executeCommand, Sim } from '../src/rules';
import { roomAt, searchSpot } from '../src/loot';
import { TEMPLATES } from '../src/content';
import { createMission, makeUnit, normalizeMission } from '../src/setup';
import type { GameState, Pos, Slot, Unit, Vec2 } from '../src/types';
import { addUnit, stateFrom, withLevels } from './helpers';

// Phase 6, steps 1 and 2 (docs/FASE6-PLAN.md): places with a level, and moving up and down.

const ctx = (seed = 1) => ({ slot: 0 as Slot, connected: [0] as Slot[], rng: new Rng(seed) });
const at = (x: number, y: number, z = 0): Pos => ({ x, y, z });

/**
 * A two-storey building with a roof you can walk on (x 1–7, y 1–5): stairs
 * from the ground at (2,2) to the first floor, and from (6,4) up to the roof.
 * The first floor has a window over the ground floor's door. A lean-to with a
 * pitched roof beside it (x 8–9, y 1–2), and a shed with a flat roof and a
 * ladder up its side (x 10–12, y 6–8).
 */
function building(): GameState {
  const s = stateFrom([
    '..............',
    '.#######......',
    '.#S....#......',
    '.#.....+......',
    '.#.....#......',
    '.#######......',
    '..........###.',
    '.........L#.#.',
    '..........###.',
  ]);
  return withLevels(s, [
    [
      '              ',
      ' #######RR    ',
      ' # ....#RR    ',
      ' #.....=      ',
      ' #....S#      ',
      ' #######      ',
      '          ... ',
      '          ... ',
      '          ... ',
    ],
    [
      '              ',
      ' .......      ',
      ' .......      ',
      ' .......      ',
      ' ..... .      ',
      ' .......      ',
      '              ',
      '              ',
      '              ',
    ],
  ]);
}

describe('places with a level', () => {
  it('a node puts the ground first, so on the ground it is the tile index', () => {
    const s = building();
    expect(levelCount(s)).toBe(3);
    expect(levelCount(stateFrom(['...']))).toBe(1);
    const area = s.width * s.height;
    for (let n = 0; n < area * levelCount(s); n++) expect(nodeOf(s, posOf(s, n))).toBe(n);
    expect(nodeOf(s, at(5, 3))).toBe(tileIndex(s, 5, 3));
    expect(posOf(s, area + tileIndex(s, 5, 3))).toEqual(at(5, 3, 1));
  });

  it('what stands on each level, how high it is, its slab and where one can stand', () => {
    const s = building();
    expect(kindAt(s, at(2, 2))).toBe('ladder');
    expect(kindAt(s, at(2, 2, 1))).toBeNull();
    expect(kindAt(s, at(3, 2, 1))).toBe('floor');
    expect(kindAt(s, at(7, 3, 1))).toBe('window');
    expect(kindAt(s, at(8, 1, 1))).toBe('roof');
    expect(kindAt(s, at(0, 0, 3))).toBeNull();
    expect(kindAt(s, at(-1, 0))).toBe('wall');
    expect([heightAt(s, at(3, 3)), heightAt(s, at(3, 3, 1)), heightAt(s, at(3, 3, 2))]).toEqual([0, 1, 2]);
    // The stairwell has no slab; the lean-to's roof is one nobody stands on.
    expect(slabAt(s, 2, 2, 1)).toBe(false);
    expect(slabAt(s, 3, 2, 1)).toBe(true);
    expect(slabAt(s, 8, 1, 1)).toBe(true);
    expect(slabAt(s, 3, 2, 0)).toBe(false);
    expect(surfaceAt(s, 3, 2, 0)).toEqual(at(3, 2));
    expect(surfaceAt(s, 3, 2, 1)).toEqual(at(3, 2, 1));
    expect(surfaceAt(s, 3, 2, 2)).toEqual(at(3, 2, 2));
    expect(surfaceAt(s, 2, 2, 1)).toBeNull();
    expect(surfaceAt(s, 8, 1, 1)).toBeNull();
    expect(surfaceAt(s, 1, 2, 0)).toBeNull();
    // Raised ground (a block of flats' roof) is the ground's surface at its height.
    const flats = stateFrom(['.1.'], ['.1.']);
    expect(surfaceAt(flats, 1, 0, 1)).toEqual(at(1, 0));
  });

  it('one unit above another is not on the same place; a place without its level is the ground', () => {
    const s = building();
    const below = addUnit(s, 's1', 'assault', 3, 3);
    const above = addUnit(s, 'a1', 'trooper', 3, 3, { z: 1 });
    expect(below.pos).toEqual(at(3, 3));
    expect(above.pos).toEqual(at(3, 3, 1));
    expect(unitAt(s, at(3, 3, 1))).toBe(above);
    expect(unitAt(s, at(3, 3))).toBe(below);
    expect(unitAt(s, { x: 3, y: 3 })).toBe(below);
    expect(unitAt(s, at(3, 3, 2))).toBeUndefined();
    expect(samePos(below.pos, above.pos)).toBe(false);
    expect(makeUnit('x', 'zombie', 'Zombi', null, { x: 1, y: 1 }, null).pos).toEqual(at(1, 1));
  });

  it('a mission saved before storeys gets every place on the ground, and plays on', () => {
    const s = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(2), kind: 'recovery' });
    s.lights = [{ id: 'l1', tile: at(3, 3), radius: 3 }];
    s.smoke = [{ pos: at(4, 4), radius: 2, turns: 2 }];
    s.units[s.units.length - 1]!.heard = at(5, 5);
    // As it was saved: no level anywhere.
    const old = JSON.parse(JSON.stringify(s, (key, value) => (key === 'z' ? undefined : value))) as GameState;
    expect(JSON.stringify(old)).not.toContain('"z"');
    normalizeMission(old);
    expect(old).toEqual(s);
    expect(normalizeMission(cloneState(old))).toEqual(old);
    const soldier = old.units.find((u) => u.team === 'xcom')!;
    const r = executeCommand(old, { type: 'move', unit: soldier.id, to: { x: soldier.pos.x, y: soldier.pos.y - 2 } }, ctx());
    expect(r.ok).toBe(true);
    const moved = r.ok ? r.events.find((e): e is Extract<GameEvent, { t: 'moved' }> => e.t === 'moved') : undefined;
    expect(moved?.path.every((p) => p.z === 0)).toBe(true);
  });

  it('commands may say the level or not (then the ground); four levels at most', () => {
    const move = (to: object) => commandSchema.safeParse({ type: 'move', unit: 's1', to }).success;
    expect(move({ x: 1, y: 2 })).toBe(true);
    expect(move({ x: 1, y: 2, z: 3 })).toBe(true);
    expect(move({ x: 1, y: 2, z: 4 })).toBe(false);
    expect(move({ x: 1, y: 2, z: -1 })).toBe(false);
    expect(commandSchema.safeParse({ type: 'ability', unit: 's1', ability: 'grenade', tile: { x: 1, y: 2, z: 1 } }).success).toBe(true);
    // Nothing else in the protocol changes.
    expect(campaignCommandSchema.safeParse({ type: 'advance', ready: true }).success).toBe(true);
  });

  it('a tile that changes on a storey changes there, on the host and on the clients; searching marks the place', () => {
    const s = building();
    const client = cloneState(s);
    const events: GameEvent[] = [
      { t: 'tileChanged', pos: at(7, 3, 1), kind: 'windowBroken' },
      { t: 'searched', unit: 's1', tile: at(4, 4, 1), loot: null },
    ];
    addUnit(s, 's1', 'assault', 4, 3, { z: 1 });
    addUnit(client, 's1', 'assault', 4, 3, { z: 1 });
    for (const e of events) applyEvent(client, e);
    expect(kindAt(client, at(7, 3, 1))).toBe('windowBroken');
    expect(kindAt(client, at(7, 3))).toBe('door');
    expect(client.searched).toEqual([nodeOf(s, at(4, 4, 1))]);
  });
});

describe('moving up and down', () => {
  it('stairs climb a storey like a ladder, up to the first floor and on to the roof, and back down', () => {
    const s = building();
    const climb = COST_ORTHO + COST_CLIMB;
    expect(stepBetween(s, at(2, 2), at(3, 2, 1))).toBe(climb);
    expect(stepBetween(s, at(2, 2), at(2, 3, 1))).toBe(climb);
    expect(stepBetween(s, at(3, 2, 1), at(2, 2))).toBe(climb);
    expect(stepBetween(s, at(6, 4, 1), at(6, 3, 2))).toBe(climb);
    // Only from the stairs, one storey at a time.
    expect(stepBetween(s, at(3, 2), at(4, 2, 1))).toBeNull();
    expect(stepBetween(s, at(3, 2, 1), at(3, 3, 2))).toBeNull();
    expect(stepBetween(s, at(2, 2), at(3, 2, 2))).toBeNull();
    // From a soldier on the ground floor to the roof and back: the path climbs both flights.
    const u = addUnit(s, 's1', 'assault', 4, 3);
    const reach = computeReach(s, u, 1000);
    const roof = at(3, 3, 2);
    const path = buildPath(s, u, reach, roof)!;
    expect(path.at(-1)).toEqual(roof);
    const climbs = path.slice(1).filter((p, i) => heightAt(s, p) > heightAt(s, path[i]!));
    expect(climbs).toHaveLength(2);
    expect(path.some((p) => samePos(p, at(2, 2)))).toBe(true);
    expect(path.some((p) => samePos(p, at(6, 4, 1)))).toBe(true);
    // Every place a unit can stand on, on every level, is in reach; the stairwell and the roofs nobody stands on are not.
    const ends = destinations(s, u, reach).map((d) => d.pos);
    expect(ends.some((p) => samePos(p, at(5, 2, 1)))).toBe(true);
    expect(ends.some((p) => samePos(p, at(2, 2, 1)))).toBe(false);
    expect(ends.some((p) => samePos(p, at(8, 1, 1)))).toBe(false);
    expect(ends.some((p) => samePos(p, at(7, 3, 1)))).toBe(false);
  });

  it("a building's stairs are left and taken at the top only by their landing, the tile they face; a ladder by any side", () => {
    const s = building();
    // The stairs at (2,2) climb east, onto (3,2) on the first floor.
    s.decor = new Array(s.width * s.height).fill(0);
    s.decor[tileIndex(s, 2, 2)] = decorCode('stairs', 1);
    const climb = COST_ORTHO + COST_CLIMB;
    expect(stepBetween(s, at(2, 2), at(3, 2, 1))).toBe(climb);
    expect(stepBetween(s, at(3, 2, 1), at(2, 2))).toBe(climb);
    // Not off the side of the flight, up or down, nor dropping into the stairwell.
    expect(stepBetween(s, at(2, 2), at(2, 3, 1))).toBeNull();
    expect(stepBetween(s, at(2, 3, 1), at(2, 2))).toBeNull();
    expect(stepsFrom(s, at(2, 3, 1), 0, -1)).toEqual([]);
    // On its own level the foot of the flight is a tile like any other.
    expect(stepBetween(s, at(2, 3), at(2, 2))).toBe(COST_ORTHO);
    // The upstairs is still all in reach, by the landing.
    const u = addUnit(s, 's1', 'assault', 4, 3);
    const reach = computeReach(s, u, 1000);
    const path = buildPath(s, u, reach, at(2, 4, 1))!;
    expect(path.some((p) => samePos(p, at(3, 2, 1)))).toBe(true);
    // The ladder up the shed's side, with no stairs on it, still climbs as before.
    expect(stepBetween(s, at(9, 7), at(10, 7, 1))).toBe(climb);
  });

  it('floors are solid: nobody walks or drops through them, and enemies on another floor block nothing', () => {
    const s = building();
    // Inside, the floor above is no way down or up.
    expect(stepBetween(s, at(4, 3, 1), at(5, 3))).toBeNull();
    expect(stepBetween(s, at(4, 3, 2), at(5, 3, 1))).toBeNull();
    expect(stepsFrom(s, at(4, 3, 1), 1, 0)).toEqual([{ to: at(5, 3, 1), cost: COST_ORTHO }]);
    expect(stepsFrom(s, at(4, 3), 1, 1)).toEqual([{ to: at(5, 4), cost: COST_DIAG }]);
    // A zombie upstairs doesn't stop a soldier crossing the ground floor under it, one on the ground does.
    const u = addUnit(s, 's1', 'assault', 2, 4);
    const zombie = addUnit(s, 'a1', 'zombie', 4, 4, { z: 1 });
    const reach = () => computeReach(s, u, 1000);
    expect(reach().cost.get(nodeOf(s, at(6, 4)))).toBe(4 * COST_ORTHO);
    expect(reach().cost.has(nodeOf(s, at(4, 4, 1)))).toBe(false);
    zombie.pos = at(4, 4);
    expect(reach().cost.get(nodeOf(s, at(6, 4)))).toBeGreaterThan(4 * COST_ORTHO);
    expect(reach().cost.has(nodeOf(s, at(4, 4, 1)))).toBe(true);
    // Two units, one above the other: each stands on its own place.
    const ally = addUnit(s, 's2', 'assault', 5, 3, { z: 1 });
    expect(destinations(s, u, reach()).some((d) => samePos(d.pos, at(5, 3)))).toBe(true);
    expect(destinations(s, u, reach()).some((d) => samePos(d.pos, ally.pos))).toBe(false);
  });

  it('off a roof or out of an upper window one drops to the street (two storeys at most), never through a roof below', () => {
    const s = building();
    expect(stepBetween(s, at(7, 4, 2), at(8, 4))).toBe(COST_ORTHO + 2 * COST_DROP);
    expect(stepBetween(s, at(7, 1, 2), at(8, 1))).toBeNull();
    // Into the first floor's window from the room, and out of it to the street below (docs/FASE7.md).
    expect(stepBetween(s, at(6, 3, 1), at(7, 3, 1))).toBe(COST_ORTHO + COST_VAULT);
    expect(stepBetween(s, at(7, 3, 1), at(8, 3))).toBe(COST_ORTHO + COST_DROP);
    const jumper = addUnit(s, 's0', 'assault', 6, 3, { z: 1 });
    const out = buildPath(s, jumper, computeReach(s, jumper, 1000), at(9, 3))!;
    expect(out.slice(0, 2)).toEqual([at(7, 3, 1), at(8, 3)]);
    s.units = s.units.filter((o) => o !== jumper);
    // A ladder up the side of a flat one-storey roof (decision 1), and a drop back off its edge.
    expect(stepBetween(s, at(9, 7), at(10, 7, 1))).toBe(COST_ORTHO + COST_CLIMB);
    expect(stepBetween(s, at(10, 6, 1), at(10, 5))).toBe(COST_ORTHO + COST_DROP);
    expect(stepBetween(s, at(11, 7, 1), at(11, 7))).toBeNull();
    const u = addUnit(s, 's1', 'assault', 9, 6);
    expect(computeReach(s, u, 1000).cost.has(nodeOf(s, at(12, 8, 1)))).toBe(true);
  });

  it('a move upstairs: the command, the events and the doors on the way say the level, and clients replay it', () => {
    const s = building();
    s.levels![0]!.tiles[tileIndex(s, 4, 3)] = 'door';
    s.levels![0]!.tiles[tileIndex(s, 4, 2)] = 'wall';
    s.levels![0]!.tiles[tileIndex(s, 4, 4)] = 'wall';
    const u = addUnit(s, 's1', 'assault', 3, 3);
    u.mods = { ...u.mods, mobility: 20 };
    addUnit(s, 'a1', 'zombie', 12, 0);
    const client = cloneState(s);
    const to = at(6, 3, 1);
    const r = executeCommand(s, { type: 'move', unit: 's1', to }, ctx());
    expect(r.ok).toBe(true);
    const events = r.ok ? r.events : [];
    for (const e of events) applyEvent(client, e);
    expect(u.pos).toEqual(to);
    expect(client.units.find((x) => x.id === 's1')!.pos).toEqual(to);
    const opened = events.find((e): e is Extract<GameEvent, { t: 'tileChanged' }> => e.t === 'tileChanged');
    expect(opened).toEqual({ t: 'tileChanged', pos: at(4, 3, 1), kind: 'doorOpen' });
    expect(events.find((e) => e.t === 'noise')).toMatchObject({ tile: at(4, 3, 1) });
    expect(client.levels).toEqual(s.levels);
    expect(client.tiles).toEqual(s.tiles);
    expect(kindAt(s, at(4, 3))).toBe('floor');
    // A command without a level goes to the ground floor.
    const down = executeCommand(s, { type: 'move', unit: 's1', to: { x: 5, y: 2 } }, ctx());
    expect(down.ok).toBe(true);
    expect(u.pos).toEqual(at(5, 2));
  });

  it('the steps from a place are every level of the next tile the rule allows, no more', () => {
    for (const s of [building(), createMission({ map: { kind: 'handmade', id: 'torre' }, slots: [0], rng: new Rng(1) })]) {
      for (let n = 0; n < s.width * s.height * levelCount(s); n++) {
        const p = posOf(s, n);
        for (const d of DIRS8) {
          const every = Array.from({ length: levelCount(s) }, (_, z) => at(p.x + d.x, p.y + d.y, z))
            .map((to) => ({ to, cost: stepBetween(s, p, to) }))
            .filter((x): x is { to: Pos; cost: number } => x.cost !== null);
          const steps = stepsFrom(s, p, d.x, d.y);
          expect(steps, `${p.x},${p.y},${p.z} → ${d.x},${d.y}`).toEqual(expect.arrayContaining(every));
          expect(steps).toHaveLength(every.length);
        }
      }
    }
  }, 60_000);

  it('maps without storeys move exactly as before: one step per direction, the same costs', () => {
    for (const seed of [3, 11]) {
      const s = createMission({ map: { kind: 'generated', biome: 'city', seed }, slots: [0], rng: new Rng(seed) });
      // As a mission saved before storeys had floors.
      delete s.levels;
      const rng = new Rng(seed);
      for (let k = 0; k < 3000; k++) {
        const p = at(rng.int(s.width), rng.int(s.height));
        for (const d of DIRS8) {
          const old = stepCost(s, p.x, p.y, d.x, d.y);
          expect(stepsFrom(s, p, d.x, d.y)).toEqual(old === null ? [] : [{ to: at(p.x + d.x, p.y + d.y), cost: old }]);
          // The rule for every level gives the same on the ground.
          if (s.tiles[tileIndex(s, p.x, p.y)] !== 'wall') expect(stepBetween(s, p, at(p.x + d.x, p.y + d.y))).toBe(old);
        }
      }
    }
  });
});

describe('la torre', () => {
  it('a skirmish map with storeys: the Cuervos start on the first floor, everything is in reach, the stairs face up', () => {
    const map = parseMap(MAPS.torre!);
    expect(map.levels).toHaveLength(3);
    const s = createMission({ map: { kind: 'handmade', id: 'torre' }, slots: [0, 1], rng: new Rng(1) });
    expect(s.levels).toHaveLength(3);
    // How tall each building stands, for the client: the tower three storeys, the shed and the house one.
    expect([s.storeys![tileIndex(s, 12, 7)], s.storeys![tileIndex(s, 3, 14)], s.storeys![tileIndex(s, 19, 15)], s.storeys![0]]).toEqual([3, 1, 1, 0]);
    const cuervos = s.units.filter((u) => u.team === 'alien' && u.template !== 'zombie');
    expect(cuervos.map((u) => u.pos.z)).toEqual([1, 1]);
    expect(s.units.filter((u) => u.template === 'zombie').every((u) => u.pos.z === 0)).toBe(true);
    // Stairs: a ladder with its decor, looking toward the floor it climbs to.
    expect(kindAt(s, at(10, 5))).toBe('ladder');
    expect(decorOf(s.decor![tileIndex(s, 10, 5)])).toEqual({ kind: 'stairs', facing: 1 });
    expect(decorOf(s.levels![0]!.decor![tileIndex(s, 15, 9)])).toEqual({ kind: 'stairs', facing: 0 });
    expect(decorOf(s.levels![1]!.decor![tileIndex(s, 10, 9)])).toEqual({ kind: 'stairs', facing: 0 });
    // From the squad's spawn: the roof of the tower, the shed's roof by its ladder.
    const scout = { ...s.units.find((u) => u.team === 'xcom')!, id: 'scout' };
    s.units = [scout];
    const reach = computeReach(s, scout, 10_000);
    for (const p of [at(12, 7, 3), at(15, 6, 1), at(3, 14, 1)]) expect(reach.cost.has(nodeOf(s, p)), `${p.x},${p.y},${p.z}`).toBe(true);
    // The pitched roof is no place to stand.
    expect(reach.cost.has(nodeOf(s, at(19, 15, 1)))).toBe(false);
  });
});

// ---------------------------------------------------------------- step 3

/**
 * lineClear as it was before storeys had floors (phase 5, with `storeys`), to
 * hold maps without them against; with the one rule added since, on purpose:
 * through two windows at most, counted where the line goes through them
 * (MAX_WINDOWS_SEEN_THROUGH, playtest 2026-10-14).
 */
function lineClearPhase5(s: GameState, a: Vec2, b: Vec2, ha = elevAt(s, a.x, a.y) + EYE_HEIGHT, hb = elevAt(s, b.x, b.y) + BODY_HEIGHT): boolean {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const nx = Math.abs(dx);
  const ny = Math.abs(dy);
  const sx = Math.sign(dx);
  const sy = Math.sign(dy);
  const len2 = dx * dx + dy * dy || 1;
  const lineAt = (x: number, y: number) => ha + (hb - ha) * Math.min(1, Math.max(0, ((x - a.x) * dx + (y - a.y) * dy) / len2));
  const blocks = (x: number, y: number) => blocksSightAt(s, x, y, lineAt(x, y));
  let side = roofSide(s, a.x, a.y, ha);
  const throughRoof = (x: number, y: number, h: number) => {
    const now = roofSide(s, x, y, h);
    const pierced = now !== null && side !== null && now !== side;
    side = now;
    return pierced;
  };
  let x = a.x;
  let y = a.y;
  let ix = 0;
  let iy = 0;
  let windows = 0;
  while (ix < nx || iy < ny) {
    const decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
    if (decision === 0) {
      if (blocks(x + sx, y) && blocks(x, y + sy)) return false;
      x += sx;
      y += sy;
      ix++;
      iy++;
    } else if (decision < 0) {
      x += sx;
      ix++;
    } else {
      y += sy;
      iy++;
    }
    if (x === b.x && y === b.y) return !throughRoof(x, y, hb);
    if (blocks(x, y) || throughRoof(x, y, lineAt(x, y))) return false;
    if (isWindow(tileAt(s, x, y)) && lineAt(x, y) < elevAt(s, x, y) + 1 && ++windows > MAX_WINDOWS_SEEN_THROUGH) return false;
  }
  return true;
}

/** teamVisibility as it was (phase 5), on lineClearPhase5 and the step-outs of then. */
function visibilityPhase5(s: GameState, team: Unit['team']): boolean[] {
  const mask = new Array<boolean>(s.width * s.height).fill(false);
  const peeks = (p: Vec2): Vec2[] => {
    const out: Vec2[] = [p];
    const elev = elevAt(s, p.x, p.y);
    for (const d of DIRS4) {
      if (coverFrom(s, elev, { x: p.x + d.x, y: p.y + d.y }) !== 2) continue;
      for (const side of [{ x: d.y, y: -d.x }, { x: -d.y, y: d.x }]) {
        const q = { x: p.x + side.x, y: p.y + side.y };
        if (canStandOn(tileAt(s, q.x, q.y)) && elevAt(s, q.x, q.y) === elev && !out.some((o) => sameTile(o, q))) out.push(q);
      }
    }
    return out;
  };
  for (const u of s.units.filter((v) => v.alive && v.team === team && !v.objective && !v.captive)) {
    const range = sightOf(u);
    const origins = peeks(u.pos);
    for (let y = 0; y < s.height; y++) {
      for (let x = 0; x < s.width; x++) {
        const i = y * s.width + x;
        if (mask[i] || Math.hypot(u.pos.x - x, u.pos.y - y) > range) continue;
        if (origins.some((o) => lineClearPhase5(s, o, { x, y }))) mask[i] = true;
      }
    }
  }
  return mask;
}

describe('sight through storeys', () => {
  it('maps without storeys see exactly as before: every line, every fog', () => {
    for (const seed of [7, 21]) {
      const s = createMission({ map: { kind: 'generated', biome: 'city', seed }, slots: [0], rng: new Rng(seed) });
      // As a mission saved before storeys had floors: everyone on the ground.
      delete s.levels;
      for (const u of s.units) u.pos = { ...u.pos, z: 0 };
      expect(s.storeys!.some((n) => n >= 2)).toBe(true);
      const rng = new Rng(seed);
      for (let k = 0; k < 4000; k++) {
        const a = at(rng.int(s.width), rng.int(s.height));
        const b = at(rng.int(s.width), rng.int(s.height));
        const ha = elevAt(s, a.x, a.y) + rng.pick([EYE_HEIGHT, 1.7, 2.7]);
        expect(lineClear(s, a, b, ha)).toBe(lineClearPhase5(s, a, b, ha));
        expect(lineClear(s, a, b)).toBe(lineClearPhase5(s, a, b));
      }
      expect(teamVisibility(s, 'xcom')).toEqual(visibilityPhase5(s, 'xcom'));
      expect(teamVisibility(s, 'alien')).toEqual(visibilityPhase5(s, 'alien'));
    }
  });

  it('with storeys but nothing on them, the ground sees as a map without storeys', () => {
    const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 7 }, slots: [0], rng: new Rng(7) });
    delete s.storeys;
    delete s.levels;
    const empty = cloneState(s);
    empty.levels = [{ tiles: new Array(s.width * s.height).fill(null) }];
    const rng = new Rng(3);
    for (let k = 0; k < 4000; k++) {
      const a = at(rng.int(s.width), rng.int(s.height));
      const b = at(rng.int(s.width), rng.int(s.height));
      const ha = elevAt(s, a.x, a.y) + rng.pick([EYE_HEIGHT, 1.7, 2.7]);
      expect(lineClear(empty, a, b, ha)).toBe(lineClear(s, a, b, ha));
    }
  });

  it('floors and ceilings block: from the ground floor nobody sees the floor above, nor from the roof the rooms below', () => {
    const s = building();
    expect(lineClear(s, at(2, 4), at(5, 4, 1))).toBe(false);
    expect(lineClear(s, at(5, 4, 1), at(2, 4))).toBe(false);
    expect(lineClear(s, at(3, 3, 1), at(6, 2, 1))).toBe(true);
    expect(lineClear(s, at(4, 3), at(6, 2))).toBe(true);
    expect(lineClear(s, at(4, 3, 2), at(5, 4))).toBe(false);
    expect(lineClear(s, at(4, 3, 2), at(5, 4, 1))).toBe(false);
    // From the roof's edge, the street below is in sight; the roof itself hides what's close under it.
    expect(lineClear(s, at(7, 3, 2), at(12, 3))).toBe(true);
    expect(lineClear(s, at(4, 3, 2), at(9, 3))).toBe(false);
  });

  it('a target straight above or below is hidden by the floor between; down a stairwell it is not', () => {
    const s = building();
    expect(lineClear(s, at(3, 3), at(3, 3, 1))).toBe(false);
    expect(lineClear(s, at(3, 3, 1), at(3, 3))).toBe(false);
    expect(lineClear(s, at(11, 7, 1), at(11, 7))).toBe(false);
    // Down the stairwell, from beside it; a step further back, the floor's edge is in the way.
    expect(lineClear(s, at(3, 2, 1), at(2, 2))).toBe(true);
    expect(lineClear(s, at(4, 2, 1), at(2, 2))).toBe(false);
  });

  it('windows are holes on their own floor: from the street into the first floor and back out, not through a wall', () => {
    const s = building();
    expect(lineClear(s, at(12, 3), at(6, 3, 1))).toBe(true);
    expect(lineClear(s, at(6, 3, 1), at(12, 3))).toBe(true);
    const walled = building();
    walled.levels![0]!.tiles[tileIndex(walled, 7, 3)] = 'wall';
    expect(lineClear(walled, at(12, 3), at(6, 3, 1))).toBe(false);
    expect(lineClear(walled, at(6, 3, 1), at(12, 3))).toBe(false);
    // The ground floor's door, closed, shows nothing; open, its own floor (with a wall over it, nothing upstairs).
    expect(lineClear(walled, at(12, 3), at(5, 3))).toBe(false);
    walled.tiles[tileIndex(walled, 7, 3)] = 'doorOpen';
    expect(lineClear(walled, at(12, 3), at(5, 3))).toBe(true);
    expect(lineClear(walled, at(9, 3), at(5, 3, 1))).toBe(false);
  });

  it('who sees whom: through an upper window both ways, never through a floor', () => {
    const s = building();
    const sniper = addUnit(s, 'a1', 'trooper', 6, 3, { z: 1 });
    const street = addUnit(s, 's1', 'sharpshooter', 12, 3);
    const below = addUnit(s, 's2', 'assault', 5, 3);
    expect(canSee(s, sniper, street)).toBe(true);
    expect(canSee(s, street, sniper)).toBe(true);
    expect(canSee(s, sniper, below)).toBe(false);
    expect(canSee(s, below, sniper)).toBe(false);
    // From up there the shot has the height.
    expect(previewShot(s, sniper, street)?.mods.some((m) => m.label === 'Ventaja de altura')).toBe(true);
    expect(previewShot(s, street, sniper)?.mods.some((m) => m.label === 'Ventaja de altura')).toBe(false);
  });

  it('the fog has a layer per level; a floor of the cutaway shows the highest place at or below it', () => {
    const s = building();
    addUnit(s, 's1', 'assault', 6, 3, { z: 2 });
    addUnit(s, 'a1', 'zombie', 13, 0);
    const mask = teamVisibility(s, 'xcom');
    const area = s.width * s.height;
    expect(mask).toHaveLength(area * 3);
    const seen = (p: Pos) => mask[nodeOf(s, p)];
    expect(seen(at(2, 2, 2))).toBe(true);
    expect(seen(at(12, 3))).toBe(true);
    expect(seen(at(4, 4))).toBe(false);
    expect(seen(at(4, 4, 1))).toBe(false);
    // Only slabs are worked out above the ground: open air is never "seen".
    expect(seen(at(12, 3, 1))).toBe(false);
    // The roof's floor of the cutaway: the roof where there is one, the ground elsewhere.
    const top = floorView(s, mask, 2);
    expect(top[tileIndex(s, 4, 4)]).toBe(true);
    expect(top[tileIndex(s, 12, 3)]).toBe(true);
    // What each floor of the cutaway looks down on: the roof over the building, the ground elsewhere and in the stairwell.
    expect(floorLevels(s, 2)[tileIndex(s, 4, 4)]).toBe(2);
    expect(floorLevels(s, 1)[tileIndex(s, 2, 2)]).toBe(0);
    expect(floorLevels(s, 1)[tileIndex(s, 3, 2)]).toBe(1);
    expect(floorLevels(s, 9)[tileIndex(s, 12, 3)]).toBe(0);
    // The ground floor's: inside, the room nobody sees.
    const ground = floorView(s, mask, 0);
    expect(ground[tileIndex(s, 4, 4)]).toBe(false);
    expect(ground[tileIndex(s, 12, 3)]).toBe(true);
    // Without storeys, the mask is the map's tiles and floorView gives it back.
    const flat = stateFrom(['....', '....']);
    addUnit(flat, 's1', 'assault', 0, 0);
    const m = teamVisibility(flat, 'xcom');
    expect(m).toHaveLength(8);
    expect(floorView(flat, m, 0)).toEqual(m);
  });

  it('a light lights its own floor and what it sees from there: not through the floor below it', () => {
    const s = building();
    s.mission.night = true;
    const tile = at(4, 3, 1);
    s.lights = [{ id: 'f1', tile, radius: 3, turns: 2, tiles: lightTiles(s, tile, 3, 0.5) }];
    expect(isLit(s, at(5, 3, 1))).toBe(true);
    expect(isLit(s, at(5, 3))).toBe(false);
    expect(isLit(s, { x: 5, y: 3 })).toBe(false);
  });
});

describe('cover per level', () => {
  it("upstairs: the storey's walls and windows are full cover, its furniture as on the ground; a roof's edge gives nothing", () => {
    const s = building();
    s.levels![0]!.tiles[tileIndex(s, 4, 2)] = 'low';
    // Against the east wall on the first floor; beside the window; beside the desk.
    expect(coverSides(s, at(6, 2, 1))).toEqual([2, 2, 0, 0]);
    expect(coverSides(s, at(6, 3, 1))).toEqual([0, 2, 0, 0]);
    expect(coverSides(s, at(4, 3, 1))[0]).toBe(1);
    // On the roof, the edge is open air; below it, on the ground floor, the same tile has the wall.
    expect(coverSides(s, at(7, 3, 2))).toEqual([0, 0, 0, 0]);
    expect(coverSides(s, at(6, 3))[1]).toBe(2);
    // A parapet is half cover; the street beside a building's wall is full, as ever.
    const torre = createMission({ map: { kind: 'handmade', id: 'torre' }, slots: [0], rng: new Rng(1) });
    expect(coverSides(torre, at(10, 5, 3))[3]).toBe(1);
    expect(coverSides(torre, at(8, 6))[1]).toBe(2);
  });

  it('flanking upstairs is by direction, as on the ground; the step out of cover keeps to the floor', () => {
    const s = building();
    expect(coverAgainst(s, at(6, 2, 1), { x: 12, y: 2 })).toBe(2);
    expect(coverAgainst(s, at(6, 2, 1), { x: 2, y: 4 })).toBe(0);
    // Behind the east wall on the first floor, a soldier steps out along it, on that floor.
    const peeks = peekOrigins(s, at(6, 2, 1));
    expect(peeks).toContainEqual(at(6, 3, 1));
    expect(peeks.every((p) => p.z === 1)).toBe(true);
  });
});

describe('time budgets', () => {
  // docs/FASE6-PLAN.md: the fog ≤ 6 ms on a town with its storeys. The best of a few runs, with room for a loaded machine.
  it('the fog of a town with storeys, each team', () => {
    const s = createMission({ map: { kind: 'generated', biome: 'city', seed: 21 }, slots: [0], rng: new Rng(1) });
    expect(s.levels!.flatMap((l) => l.tiles).filter((k) => k !== null).length).toBeGreaterThan(1000);
    for (const team of ['xcom', 'alien'] as const) {
      let best = Infinity;
      for (let k = 0; k < 6; k++) {
        const t0 = Date.now();
        teamVisibility(s, team);
        best = Math.min(best, Date.now() - t0);
      }
      expect(best, team).toBeLessThan(12);
    }
  });
});

// ---------------------------------------------------------------- step 4

describe('noise and blasts by floor', () => {
  it('a storey between is one more tile for a noise to carry (decision 3); maps without storeys hear as before', () => {
    const s = building();
    addUnit(s, 's1', 'assault', 4, 3, { z: 1 });
    const near = addUnit(s, 'a1', 'zombie', 8, 3);
    const far = addUnit(s, 'a2', 'zombie', 9, 3);
    const upstairs = addUnit(s, 'a3', 'zombie', 9, 3, { z: 1 });
    for (const [z, id] of [[near, 'pn'], [far, 'pf'], [upstairs, 'pu']] as const) {
      z.podId = id;
      s.pods.push({ id, active: false, unitIds: [z.id], patrol: [], patrolIndex: 0 });
    }
    const sim = new Sim(s, new Rng(1), [0]);
    sim.makeNoise(at(4, 3, 1), 5);
    const noise = sim.events.find((e): e is Extract<GameEvent, { t: 'noise' }> => e.t === 'noise')!;
    expect(noise.heard.sort()).toEqual(['a1', 'a3']);
    // The same on a map without storeys: only the distance.
    const flat = stateFrom(['..........']);
    addUnit(flat, 's1', 'assault', 4, 0);
    const z = addUnit(flat, 'a1', 'zombie', 9, 0);
    z.podId = 'p';
    flat.pods.push({ id: 'p', active: false, unitIds: ['a1'], patrol: [], patrolIndex: 0 });
    const flatSim = new Sim(flat, new Rng(1), [0]);
    flatSim.makeNoise(at(4, 0), 5);
    expect(flatSim.events.find((e): e is Extract<GameEvent, { t: 'noise' }> => e.t === 'noise')!.heard).toEqual(['a1']);
  });

  it('a floor stops a blast (decision 5): a grenade upstairs hurts that floor only, and breaks what stands there', () => {
    const s = building();
    s.levels![0]!.tiles[tileIndex(s, 5, 2)] = 'low';
    s.tiles[tileIndex(s, 5, 2)] = 'low';
    const thrower = addUnit(s, 's1', 'grenadier', 12, 3);
    const up = addUnit(s, 'a1', 'trooper', 4, 3, { z: 1 });
    const under = addUnit(s, 'a2', 'trooper', 4, 3);
    const roof = addUnit(s, 'a3', 'trooper', 4, 3, { z: 2 });
    const center = at(4, 2, 1);
    const area = blastTiles(s, 'grenade', center, thrower);
    expect(area.every((p) => p.z === 1)).toBe(true);
    expect(area).toContainEqual(at(4, 3, 1));
    const sim = new Sim(s, new Rng(1), [0]);
    sim.explode(thrower, center, 'grenade');
    const hurt = sim.events.filter((e): e is Extract<GameEvent, { t: 'damaged' }> => e.t === 'damaged').map((e) => e.unit);
    expect(hurt).toContain(up.id);
    expect(hurt).not.toContain(under.id);
    expect(hurt).not.toContain(roof.id);
    const broken = sim.events.filter((e): e is Extract<GameEvent, { t: 'tileChanged' }> => e.t === 'tileChanged');
    expect(broken).toContainEqual({ t: 'tileChanged', pos: at(5, 2, 1), kind: 'floor' });
    expect(kindAt(s, at(5, 2))).toBe('low');
    // On the ground outside, a grenade reaches the street, not the window above it nor the roof.
    const street = blastTiles(s, 'grenade', at(9, 3), thrower);
    expect(street.some((p) => p.z > 0)).toBe(false);
    expect(street).toContainEqual(at(8, 3));
  });

  it('without storeys a blast reaches every height within its radius, as before', () => {
    const s = stateFrom(['......', '......'], ['..22..', '......']);
    const tiles = blastTiles(s, 'grenade', at(1, 1), undefined);
    expect(tiles).toContainEqual(at(2, 0));
    expect(tiles.every((p) => p.z === 0)).toBe(true);
  });

  it('smoke and Silencio stay on their floor', () => {
    const s = building();
    s.smoke = [{ pos: at(4, 3, 1), radius: 2, turns: 2 }];
    s.silences = [{ pos: at(4, 3, 1), radius: 2, turns: 2 }];
    expect(inSmoke(s, at(5, 3, 1))).toBe(true);
    expect(inSmoke(s, at(5, 3))).toBe(false);
    expect(inSilence(s, at(5, 3, 1))).toBe(true);
    expect(inSilence(s, { x: 5, y: 3 })).toBe(false);
  });

  it('by hand only beside you on your floor: first aid, the terminal, a partner singing; the Grillo flies anywhere in range', () => {
    const s = building();
    const medic = addUnit(s, 's1', 'assault', 4, 3, { z: 1, gear: ['firstAid'], charges: { firstAid: 1 } });
    const hurtBelow = addUnit(s, 's2', 'assault', 5, 3, { hp: 2 });
    const hurtBeside = addUnit(s, 's3', 'assault', 5, 4, { z: 1, hp: 2 });
    expect(allyTargets(s, medic, 'firstAid')).toEqual([hurtBeside]);
    expect(hurtBelow.pos).toEqual(at(5, 3));
    const grillo = addUnit(s, 's4', 'specialist', 12, 3, { charges: { medkit: 2 } });
    expect(allyTargets(s, grillo, 'medkit')).toEqual(expect.arrayContaining([hurtBelow, hurtBeside]));
    // The terminal: from beside it, on its floor.
    s.mission.terminal = at(3, 4);
    s.tiles[tileIndex(s, 3, 4)] = 'console';
    expect(canHackFrom(s, medic, at(3, 3, 1))).toBe(false);
    expect(canHackFrom(s, medic, at(3, 3))).toBe(true);
    // «A dos voces»: the partner's singer beside you, not on the floor below.
    const singer = addUnit(s, 's5', 'assault', 4, 4, { owner: 1 });
    singer.mods = { ...singer.mods, singer: 1 };
    expect(duet(s, medic)).toBe(0);
    singer.pos = at(4, 4, 1);
    expect(duet(s, medic)).toBe(1);
  });
});

// ---------------------------------------------------------------- step 5

/** Every place on every level of `s`. */
const everywhere = (s: GameState) => Array.from({ length: s.width * s.height * levelCount(s) }, (_, n) => posOf(s, n));

/** A building's stairs: a ladder dressed as stairs, on any level. */
function stairsOf(s: GameState): { at: Pos; up: number }[] {
  return everywhere(s).flatMap((p) => {
    const decor = p.z === 0 ? s.decor : s.levels?.[p.z - 1]?.decor;
    const look = decorOf(decor?.[tileIndex(s, p.x, p.y)]);
    return kindAt(s, p) === 'ladder' && look?.kind === 'stairs' ? [{ at: p, up: look.facing }] : [];
  });
}

describe("the generator's storeys", () => {
  const towns = Array.from({ length: 16 }, (_, k) => createMission({ map: { kind: 'generated', biome: 'city', seed: 101 * (k + 1) }, slots: [0], rng: new Rng(k), kind: 'hack' }));

  it('towns of both styles stand their buildings for real: storeys, stairs up each one, windows, rooms to search', () => {
    let three = 0;
    let windows = 0;
    const rooms = new Set<string>();
    for (const s of towns) {
      expect(s.levels?.length).toBeGreaterThanOrEqual(2);
      if (s.levels!.length >= 3) three++;
      for (const { at: p, up } of stairsOf(s)) {
        // A stairwell over the flight, a floor to step off onto on the side it climbs to.
        expect(slabAt(s, p.x, p.y, p.z + 1), `${p.x},${p.y},${p.z}`).toBe(false);
        expect(canStandOn(kindAt(s, { x: p.x + DIRS4[up]!.x, y: p.y + DIRS4[up]!.y, z: p.z + 1 })), `${p.x},${p.y},${p.z}`).toBe(true);
      }
      for (const p of everywhere(s).filter((q) => q.z > 0)) {
        if (kindAt(s, p) === 'window') {
          windows++;
          // A window upstairs looks out onto open air, never into the next building's storey.
          expect(DIRS4.some((d) => kindAt(s, { x: p.x + d.x, y: p.y + d.y, z: p.z }) === null), `${p.x},${p.y},${p.z}`).toBe(true);
        }
        // Upstairs rooms to search too (docs/FASE8.md §B), under the storey above.
        const room = roomAt(s, p);
        if (room) rooms.add(`${s.mapId}:${room.id}`);
      }
    }
    expect(three).toBeGreaterThan(0);
    expect(windows).toBeGreaterThan(100);
    expect(rooms.size).toBeGreaterThan(50);
  });

  it("every place one can stand on, on every level, is in the squad's reach", () => {
    for (const s of [...towns.slice(0, 8), ...['wilds', 'facility'].flatMap((biome) => [1, 2, 3].map((k) => createMission({ map: { kind: 'generated', biome: biome as 'wilds', seed: 57 * k }, slots: [0], rng: new Rng(k) })))]) {
      const scout = { ...s.units.find((u) => u.team === 'xcom')!, id: 'scout' };
      const alone = { ...s, units: [scout] };
      const reach = computeReach(alone, scout, 100_000);
      const lost = everywhere(s).filter((p) => p.z > 0 && canStandOn(kindAt(s, p)) && !reach.cost.has(nodeOf(s, p)));
      expect(lost, s.mapId).toEqual([]);
    }
  });

  it('keeps stairs away from walls on both floors, with an open foot and a usable landing in both directions', () => {
    let count = 0;
    for (const s of towns) for (const { at: p, up } of stairsOf(s)) {
      const label = `${s.mapId}: ${p.x},${p.y},${p.z}`;
      const d = DIRS4[up]!;
      const foot = { x: p.x - d.x, y: p.y - d.y, z: p.z };
      const landing = { x: p.x + d.x, y: p.y + d.y, z: p.z + 1 };
      for (const side of DIRS4) for (const z of [p.z, p.z + 1]) {
        expect(['floor', 'low', 'high'], label).toContain(kindAt(s, { x: p.x + side.x, y: p.y + side.y, z }));
      }
      expect(kindAt(s, foot), label).toBe('floor');
      expect(stepBetween(s, foot, p), label).not.toBeNull();
      expect(stepBetween(s, p, landing), label).toBe(COST_ORTHO + COST_CLIMB);
      expect(stepBetween(s, landing, p), label).toBe(COST_ORTHO + COST_CLIMB);
      count++;
    }
    expect(count).toBeGreaterThan(20);
  });

  it('flat roofs: over two or three storeys reached by the stairs, with a parapet; over one, a ladder up the side about half the time; houses pitched', () => {
    let decks = 0;
    let parapets = 0;
    let ladders = 0;
    let pitched = 0;
    for (const s of towns) {
      const top = everywhere(s).filter((p) => p.z > 0);
      decks += top.filter((p) => kindAt(s, p) === 'floor' && !slabAt(s, p.x, p.y, p.z + 1) && storeysAt(s, p.x, p.y) === p.z).length;
      parapets += top.filter((p) => kindAt(s, p) === 'low' && !searchSpot(s, p)).length;
      pitched += top.filter((p) => kindAt(s, p) === 'roof').length;
      // A ladder up a wall onto a one-storey roof: a plain ladder on the ground, a roof one can stand on above the wall beside it.
      ladders += everywhere(s).filter(
        (p) =>
          p.z === 0 &&
          kindAt(s, p) === 'ladder' &&
          !decorOf(s.decor?.[tileIndex(s, p.x, p.y)]) &&
          DIRS4.some((d) => kindAt(s, { x: p.x + d.x, y: p.y + d.y, z: 0 }) === 'wall' && isWalkable(kindAt(s, { x: p.x + d.x, y: p.y + d.y, z: 1 }))),
      ).length;
    }
    expect(decks).toBeGreaterThan(0);
    expect(parapets).toBeGreaterThan(0);
    expect(ladders).toBeGreaterThan(0);
    expect(pitched).toBeGreaterThan(0);
  });

  it('in a mission of the living one of their groups starts posted upstairs from difficulty 2, never the dead; none at difficulty 1', () => {
    // docs/ZOMBIS-PRIMERO.md: the living only hold some missions; in the dead's, everyone starts on the ground.
    for (const s of towns) expect(s.units.some((u) => u.team === 'alien' && u.pos.z > 0), s.mapId).toBe(false);
    const held = Array.from({ length: 16 }, (_, k) => createMission({ map: { kind: 'generated', biome: 'city', seed: 101 * (k + 1) }, slots: [0], rng: new Rng(k), kind: 'hack', living: true }));
    let posted = 0;
    for (const s of held) {
      const up = s.units.filter((u) => u.pos.z > 0);
      for (const u of up) expect(TEMPLATES[u.template].undead, `${s.mapId}: ${u.id}`).toBe(false);
      if (up.length) {
        posted++;
        expect(new Set(up.map((u) => u.podId)).size).toBe(1);
        expect(canStandOn(kindAt(s, up[0]!.pos))).toBe(true);
      }
    }
    expect(posted).toBeGreaterThan(held.length / 2);
    for (let k = 1; k <= 6; k++) {
      const easy = createMission({ map: { kind: 'generated', biome: 'city', seed: 101 * k }, slots: [0], rng: new Rng(k), kind: 'hack', difficulty: 1, living: true });
      expect(easy.units.some((u) => u.pos.z > 0)).toBe(false);
    }
  });

  it('the same seed gives the same storeys', () => {
    const again = createMission({ map: { kind: 'generated', biome: 'city', seed: 101 }, slots: [0], rng: new Rng(0), kind: 'hack' });
    expect(again.levels).toEqual(towns[0]!.levels);
    expect(again.tiles).toEqual(towns[0]!.tiles);
  });
});

// ---------------------------------------------------------------- step 6

/** How far for a walker (ai.ts farness): the tiles, and 3 for each storey between. */
const farness = (s: GameState, a: Pos, b: Pos) => Math.hypot(a.x - b.x, a.y - b.y) + 3 * Math.abs(heightAt(s, a) - heightAt(s, b));

describe('the enemy upstairs', () => {
  /** An active pod of `units`. */
  const active = (s: GameState, id: string, units: Unit[]) => {
    for (const u of units) u.podId = id;
    s.pods.push({ id, active: true, unitIds: units.map((u) => u.id), patrol: [], patrolIndex: 0 });
  };

  it('a zombie that heard something upstairs goes up the stairs to it, not to the floor under it', () => {
    const s = building();
    s.activeTeam = 'alien';
    // A soldier out of its sight, on the roof.
    addUnit(s, 's1', 'assault', 2, 1, { z: 2 });
    const z = addUnit(s, 'a1', 'zombie', 12, 3, { heard: at(5, 3, 1) });
    s.tiles[tileIndex(s, 7, 3)] = 'doorOpen';
    active(s, 'p1', [z]);
    const walked: Pos[] = [];
    for (let turn = 0; turn < 6 && z.heard; turn++) {
      const sim = new Sim(s, new Rng(turn), [0]);
      sim.runAlienTurn();
      for (const e of sim.events) if (e.t === 'moved' && e.unit === z.id) walked.push(...e.path);
    }
    expect(z.pos.z).toBe(1);
    // It got there, upstairs (and having found nobody, may wander off).
    expect(walked.some((p) => p.z === 1 && Math.hypot(p.x - 5, p.y - 3) <= 1.5)).toBe(true);
  });

  it('a zombie that heard a soldier go upstairs climbs after him and bites, never through the floor', () => {
    const s = building();
    s.activeTeam = 'alien';
    const soldier = addUnit(s, 's1', 'assault', 4, 3, { z: 1 });
    // Right under him, out of sight and out of reach: the floor is between them.
    const z = addUnit(s, 'a1', 'zombie', 3, 4);
    active(s, 'p1', [z]);
    z.heard = { ...soldier.pos };
    // Up the stairs, then the bite: this turn or the next (two actions, playtest 16/10 #21), always from upstairs.
    let bite: Extract<GameEvent, { t: 'shot' }> | undefined;
    for (let turn = 0; turn < 2 && !bite; turn++) {
      const sim = new Sim(s, new Rng(1), [0]);
      sim.runAlienTurn();
      bite = sim.events.find((e): e is Extract<GameEvent, { t: 'shot' }> => e.t === 'shot');
    }
    expect(z.pos.z).toBe(1);
    expect(bite).toMatchObject({ unit: z.id, target: soldier.id, ability: 'claw' });
    expect(bite!.from.z).toBe(1);
  });

  it('a living enemy hunting a soldier on another floor goes the long way round by the stairs, not to the spot over his head', () => {
    // A long two-storey hall, the stairs at its west end.
    const s = withLevels(stateFrom(['........................', '.######################.', '.#S...................#.', '.#....................#.', '.######################.', '........................']), [
      ['                        ', ' ###################### ', ' # ' + '.'.repeat(19) + '# ', ' #....................# ', ' ###################### ', '                        '],
    ]);
    s.activeTeam = 'alien';
    const soldier = addUnit(s, 's1', 'assault', 20, 3);
    const lancer = addUnit(s, 'a1', 'lancer', 20, 2, { z: 1 });
    active(s, 'p1', [lancer]);
    lancer.heard = { ...soldier.pos };
    lancer.ap = 2;
    const start = farness(s, lancer.pos, soldier.pos);
    new Sim(s, new Rng(1), [0]).runAlienTurn();
    // Off toward the stairs, though that is away from him for now.
    expect(lancer.pos.x).toBeLessThan(18);
    expect(start).toBeGreaterThan(0);
  });

  it('the living posted upstairs keep the high ground when they wake', () => {
    let kept = 0;
    let woke = 0;
    for (let seed = 1; seed <= 6; seed++) {
      const s = createMission({ map: { kind: 'handmade', id: 'torre' }, slots: [0], rng: new Rng(seed), difficulty: 2 });
      const cuervos = s.units.filter((u) => u.team === 'alien' && u.pos.z === 1);
      expect(cuervos).toHaveLength(2);
      // The squad in the street in front of their window.
      const squad = s.units.filter((u) => u.team === 'xcom');
      squad.forEach((u, k) => (u.pos = at(18 + (k % 3), 6 + Math.floor(k / 3) * 2)));
      s.concealed = false;
      const sim = new Sim(s, new Rng(seed), [0]);
      sim.activatePods([cuervos[0]!.podId!], { cause: 'sighted' });
      for (const u of s.units) if (u.team === 'alien') u.ap = 2;
      sim.runAlienTurn();
      woke++;
      if (cuervos.every((u) => !u.alive || u.pos.z >= 1)) kept++;
    }
    expect(kept).toBe(woke);
  });
});
