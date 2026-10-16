import { describe, expect, it } from 'vitest';
import { byNight, createCampaign, nightFor, type MissionOffer } from '../src/campaign';
import { NIGHT, TEMPLATES } from '../src/content';
import { decorOf } from '../src/decor';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { BODY_HEIGHT, elevAt, EYE_HEIGHT, posOf, sightTop } from '../src/grid';
import { rollLoot } from '../src/loot';
import { canSee, canSeeTile, detectionTo, isLit, lightTiles, lineClear, litMask, MAX_WINDOWS_SEEN_THROUGH, nightSight, sightOf, sightTo, teamVisibility } from '../src/los';
import { generateLayout } from '../src/mapgen';
import { Rng } from '../src/rng';
import { Sim } from '../src/rules';
import { createMission, nightPod } from '../src/setup';
import type { GameState, Pod, Pos, Unit, Vec2 } from '../src/types';
import { addUnit, stateFrom } from './helpers';

// Building height and night missions (docs/ALTURA-Y-NOCHE.md).

const sim = (s: GameState, seed = 1) => new Sim(s, new Rng(seed), [0]);
const of = <T extends GameEvent['t']>(events: GameEvent[], t: T) => events.filter((e): e is Extract<GameEvent, { t: T }> => e.t === t);

/** Storeys for every tile of a rectangle (a building's walls and inside). */
function raise(s: GameState, x0: number, y0: number, x1: number, y1: number, n: number): void {
  s.storeys ??= new Array<number>(s.width * s.height).fill(0);
  for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) s.storeys[y * s.width + x] = n;
}

function pod(s: GameState, id: string, active: boolean, units: Unit[]): Pod {
  const p: Pod = { id, active, unitIds: units.map((u) => u.id), patrol: [], patrolIndex: 0 };
  for (const u of units) u.podId = id;
  s.pods.push(p);
  return p;
}

/** lineClear as it was before storeys existed (commit 3655d07), to hold today's results against; with today's window rule (MAX_WINDOWS_SEEN_THROUGH). */
function lineClearBefore(s: GameState, a: Vec2, b: Vec2, ha = elevAt(s, a.x, a.y) + EYE_HEIGHT, hb = elevAt(s, b.x, b.y) + BODY_HEIGHT): boolean {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const nx = Math.abs(dx);
  const ny = Math.abs(dy);
  const sx = Math.sign(dx);
  const sy = Math.sign(dy);
  const len2 = dx * dx + dy * dy || 1;
  const lineAt = (x: number, y: number) => ha + (hb - ha) * Math.min(1, Math.max(0, ((x - a.x) * dx + (y - a.y) * dy) / len2));
  const blocks = (x: number, y: number) => sightTop(s, x, y) > lineAt(x, y) + 1e-6;
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
    if (x === b.x && y === b.y) return true;
    if (blocks(x, y)) return false;
    const i = y * s.width + x;
    if ((s.tiles[i] === 'window' || s.tiles[i] === 'windowBroken') && lineAt(x, y) < (s.elev[i] ?? 0) + 1 && ++windows > MAX_WINDOWS_SEEN_THROUGH) return false;
  }
  return true;
}

describe('tall buildings', () => {
  // A shop (walls x 6–10, y 1–5) between a one-storey rooftop at (2,3) and the street beyond.
  const street = () =>
    stateFrom(
      ['....................', '......#####.........', '......#...#.........', '......#...#.........', '......#...#.........', '......#####.........', '....................'],
      ['', '', '', '..1', '', '', ''],
    );

  it('from a house’s rooftop nobody sees over a three-storey shop', () => {
    const s = street();
    const watcher = addUnit(s, 's1', 'rookie', 2, 3);
    const target = addUnit(s, 'a1', 'trooper', 16, 3);
    // One storey (or no storeys at all): over the roof, as ever.
    expect(canSee(s, watcher, target)).toBe(true);
    raise(s, 6, 1, 10, 5, 1);
    expect(canSee(s, watcher, target)).toBe(true);
    raise(s, 6, 1, 10, 5, 3);
    expect(canSee(s, watcher, target)).toBe(false);
  });

  it('through a ground-floor window, someone on the ground sees inside; from a rooftop, nobody does', () => {
    const rows = ['............', '..#####.....', '..#...#.....', '..=...#.....', '..#...#.....', '..#####.....', '............'];
    const ground = stateFrom(rows);
    raise(ground, 2, 1, 6, 5, 3);
    const inside = { x: 4, y: 3, z: 0 };
    expect(canSeeTile(ground, { x: 0, y: 3, z: 0 }, inside, 18)).toBe(true);
    // The same wall without the window: nothing.
    const blind = stateFrom(rows.map((r) => r.replace('=', '#')));
    raise(blind, 2, 1, 6, 5, 3);
    expect(canSeeTile(blind, { x: 0, y: 3, z: 0 }, inside, 18)).toBe(false);
    // From a one-storey rooftop beside it, the window shows only the wall above it.
    const roof = stateFrom(rows, ['', '', '', '1']);
    expect(lineClear(roof, { x: 0, y: 3 }, inside)).toBe(true);
    raise(roof, 2, 1, 6, 5, 3);
    expect(lineClear(roof, { x: 0, y: 3 }, inside)).toBe(false);
    // And no line crosses its upper storeys, even an open one over the ground floor's furniture.
    expect(lineClear(roof, { x: 0, y: 3 }, { x: 10, y: 3 }, 1.7, 1.5)).toBe(false);
  });

  it('playtest 2026-10-13: from the street, a window, whole or broken, shows the room and lifts its fog', () => {
    const rows = ['............', '..#####.....', '..#...#.....', '..=...#.....', '..#...#.....', '..#####.....', '............'];
    const room = (seen: boolean[]) => {
      const out: string[] = [];
      for (let y = 2; y <= 4; y++) for (let x = 3; x <= 5; x++) if (seen[y * 12 + x]) out.push(`${x},${y}`);
      return out;
    };
    for (const glass of ['window', 'windowBroken'] as const) {
      for (const storeys of [1, 2, 3]) {
        const s = stateFrom(rows);
        s.tiles[3 * 12 + 2] = glass;
        raise(s, 2, 1, 6, 5, storeys);
        // Two steps back: a wedge of the room behind the glass.
        const watcher = addUnit(s, 's1', 'rookie', 0, 3);
        const label = `${glass}, ${storeys} storeys`;
        expect(room(teamVisibility(s, 'xcom')), label).toEqual(['4,2', '5,2', '3,3', '4,3', '5,3', '4,4', '5,4']);
        // Whoever is in there is seen too.
        const inside = addUnit(s, 'a1', 'zombie', 5, 4);
        expect(canSee(s, watcher, inside), label).toBe(true);
        // Right at the glass: the whole room.
        watcher.pos = { x: 1, y: 3, z: 0 };
        expect(room(teamVisibility(s, 'xcom')), label).toHaveLength(9);
      }
    }
    // The same wall without the window: the room stays in the fog.
    const blind = stateFrom(rows.map((r) => r.replace('=', '#')));
    raise(blind, 2, 1, 6, 5, 1);
    addUnit(blind, 's1', 'rookie', 1, 3);
    expect(room(teamVisibility(blind, 'xcom'))).toEqual([]);
  });

  it('maps without storeys see exactly as before; from the ground, so do one-storey buildings', () => {
    const layout = generateLayout({ biome: 'city', kind: 'elimination', podSizes: [2, 2, 3], seed: 7 });
    const old = createMission({ map: { kind: 'generated', biome: 'city', seed: 7 }, slots: [0], rng: new Rng(1) });
    expect(old.storeys).toEqual(layout.storeys);
    const tall = old.storeys!.some((n) => n >= 2);
    expect(tall).toBe(true);
    // Without their storeys above the ground (docs/FASE6-PLAN.md): the model of tall buildings this tests.
    delete old.levels;
    const flat = cloneState(old);
    delete flat.storeys;
    const low = cloneState(old);
    low.storeys = low.storeys!.map((n) => Math.min(n, 1));
    // Rooftops and the street, from every height a unit has.
    const rng = new Rng(3);
    let differs = 0;
    for (let k = 0; k < 4000; k++) {
      const a = { x: rng.int(48), y: rng.int(48) };
      const b = { x: rng.int(48), y: rng.int(48) };
      const ha = elevAt(old, a.x, a.y) + rng.pick([EYE_HEIGHT, 1.7, 2.7]);
      const before = lineClearBefore(old, a, b, ha);
      expect(lineClear(flat, a, b, ha)).toBe(before);
      // From the street to the street, nothing reaches a one-storey roof: the same as before.
      if (!elevAt(old, a.x, a.y) && !elevAt(old, b.x, b.y)) expect(lineClear(low, a, b)).toBe(lineClearBefore(old, a, b));
      if (lineClear(old, a, b, ha) !== before) differs++;
    }
    // With the real storeys, some lines over the town now stop at the tall buildings.
    expect(differs).toBeGreaterThan(0);
  }, 60_000);

  it('every building has a roof: from a raised platform nobody sees into a house, but over it they do', () => {
    // A one-storey house (walls x 4–8, y 1–5) with a window on its west wall, a platform west of it.
    const rows = ['...............', '....#####......', '....#...#......', '....=...#......', '....#...#......', '....#####......', '...............'];
    const s = stateFrom(rows, ['', '', '11', '11']);
    const room = { x: 7, y: 2 };
    // Without storeys, as before: down over the wall into the room.
    expect(lineClear(s, { x: 0, y: 2 }, room)).toBe(true);
    expect(lineClear(s, { x: 1, y: 2 }, room)).toBe(true);
    raise(s, 4, 1, 8, 5, 1);
    expect(lineClear(s, { x: 0, y: 2 }, room)).toBe(false);
    expect(lineClear(s, { x: 1, y: 2 }, room)).toBe(false);
    // Looking out of the room at the platform is the same line backwards.
    expect(lineClear(s, room, { x: 1, y: 2 }, 0.7, 1.55)).toBe(false);
    // Over the roof to the street beyond, as before.
    expect(lineClear(s, { x: 1, y: 2 }, { x: 13, y: 2 }, 1.7, 0.55)).toBe(lineClearBefore(s, { x: 1, y: 2 }, { x: 13, y: 2 }, 1.7, 0.55));
    // A window is still a window, from the platform beside it or from the ground.
    expect(lineClear(s, { x: 0, y: 3 }, { x: 6, y: 3 })).toBe(true);
    const street = stateFrom(rows);
    raise(street, 4, 1, 8, 5, 1);
    expect(lineClear(street, { x: 1, y: 3 }, { x: 6, y: 3 })).toBe(true);
  });

  it('the generator sets the storeys of each building, by its use and by its seed', () => {
    const town = (seed: number) => createMission({ map: { kind: 'generated', biome: 'city', seed }, slots: [0], rng: new Rng(1) });
    const counts = [0, 0, 0, 0];
    const desks = new Set<number>();
    for (let seed = 1; seed <= 6; seed++) {
      const s = town(seed);
      expect(town(seed).storeys).toEqual(s.storeys);
      for (let i = 0; i < s.tiles.length; i++) {
        const n = s.storeys![i]!;
        counts[n]!++;
        // Inside a building, or its walls; nothing out in the street.
        if (n) expect(s.ground![i] === 2 || ['wall', 'door', 'window', 'windowBroken', 'doorOpen', 'floor'].includes(s.tiles[i]!)).toBe(true);
        else expect(s.ground![i]).not.toBe(2);
        // Offices stand two or three storeys (a hall's office, one); buildings stand on the ground.
        if (decorOf(s.decor![i])?.kind === 'desk') desks.add(n);
        if (n) expect(s.elev[i]).toBe(0);
      }
    }
    expect(counts[1]).toBeGreaterThan(0);
    expect(counts[2]).toBeGreaterThan(0);
    expect(counts[3]).toBeGreaterThan(0);
    expect([...desks].some((n) => n >= 2)).toBe(true);
    // Out of town, everything is one storey; at a factory, but its offices, which may have two (playtest 15/10, #17).
    for (const biome of ['wilds', 'facility'] as const) {
      const s = createMission({ map: { kind: 'generated', biome, seed: 4 }, slots: [0], rng: new Rng(1) });
      expect(Math.max(...s.storeys!)).toBeLessThanOrEqual(biome === 'wilds' ? 1 : 2);
    }
    // Hand-made maps have none: they see as before.
    expect(createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(1) }).storeys).toBeUndefined();
  }, 60_000);
});

describe('night', () => {
  /** An open field at night, with one street light at (2,15). */
  const dark = () => {
    const s = stateFrom(Array.from({ length: 20 }, () => '.'.repeat(30)));
    s.mission.night = true;
    s.lights = [{ id: 'l1', tile: { x: 2, y: 15, z: 0 }, radius: NIGHT.streetLight }];
    return s;
  };

  it('the living see 8 tiles less (6 at least), unless what they look at is lit; the dead see as ever', () => {
    const s = dark();
    const soldier = addUnit(s, 's1', 'rookie', 2, 2);
    const near = addUnit(s, 'a1', 'trooper', 2, 11);
    const far = addUnit(s, 'a2', 'trooper', 2, 14);
    expect(sightOf(soldier)).toBe(18);
    expect(nightSight(soldier)).toBe(10);
    expect(canSee(s, soldier, near)).toBe(true);
    // Twelve tiles off in the dark: unseen; in the street light's circle: seen.
    far.pos = { x: 12, y: 14, z: 0 };
    expect(canSee(s, soldier, far)).toBe(false);
    far.pos = { x: 2, y: 14, z: 0 };
    expect(isLit(s, far.pos)).toBe(true);
    expect(canSee(s, soldier, far)).toBe(true);
    // Short-sighted to begin with: never under 6.
    expect(nightSight({ ...soldier, mods: { ...soldier.mods, sight: -8 } })).toBe(NIGHT.minSight);
    // The dead hunt by ear, and see as far as by day.
    const zombie = addUnit(s, 'a3', 'zombie', 20, 2);
    expect(sightTo(s, zombie, { x: 20, y: 15 })).toBe(TEMPLATES.zombie.sight);
    // The squad's view of the map is the same: dark tiles only from close by.
    const seen = teamVisibility(s, 'xcom');
    expect(seen[11 * 30 + 2]).toBe(true);
    expect(seen[13 * 30 + 12]).toBe(false);
    expect(seen[15 * 30 + 2]).toBe(true);
  });

  it('a light reaches only what it sees: walls and roofs keep it out', () => {
    // A garden wall (x 6), an alley (x 7) and a one-storey house (x 8–12) with a window at (8,3).
    const s = stateFrom(['..............', '......#.#####.', '......#.#...#.', '......#.=...#.', '......#.#...#.', '......#.#####.', '..............']);
    s.mission.night = true;
    raise(s, 8, 1, 12, 5, 1);
    const at = (x: number, y: number) => y * 14 + x;
    // A street lamp at (5,2): the pavement round it is lit, and from up high it shines over the garden wall.
    const lamp = lightTiles(s, { x: 5, y: 2, z: 0 }, NIGHT.streetLight, NIGHT.lampHeight);
    s.lights = [{ id: 'l1', tile: { x: 5, y: 2, z: 0 }, radius: NIGHT.streetLight, tiles: lamp }];
    expect(isLit(s, { x: 4, y: 2 })).toBe(true);
    expect(isLit(s, { x: 5, y: 4 })).toBe(true);
    expect(isLit(s, { x: 7, y: 2 })).toBe(true);
    // A Bengala burning on the ground doesn't: the wall keeps its light in.
    const low = lightTiles(s, { x: 5, y: 2, z: 0 }, NIGHT.flare.radius, NIGHT.flare.height);
    expect(low).toContain(at(3, 2));
    expect(low).not.toContain(at(7, 2));
    // In the alley: a lamp's light stops at the house's roof, but a Bengala on the ground lights the room through the window.
    expect(lightTiles(s, { x: 7, y: 3, z: 0 }, NIGHT.streetLight, NIGHT.lampHeight)).not.toContain(at(9, 3));
    const flare = lightTiles(s, { x: 7, y: 3, z: 0 }, NIGHT.flare.radius, NIGHT.flare.height);
    expect(flare).toContain(at(9, 3));
    expect(flare).toContain(at(7, 5));
    expect(flare).not.toContain(at(9, 2));
    // A map's lamps are worked out when the mission is made, and the mask matches isLit.
    const night = createMission({ map: { kind: 'generated', biome: 'city', seed: 21 }, slots: [0], rng: new Rng(1), night: true });
    const mask = litMask(night);
    for (const l of night.lights!) {
      expect(l.tiles!.length).toBeGreaterThan(0);
      for (const i of l.tiles!) expect(isLit(night, posOf(night, i))).toBe(mask[i]);
    }
  });

  it('by day nothing changes, street lights or not', () => {
    const s = dark();
    s.mission.night = false;
    const soldier = addUnit(s, 's1', 'rookie', 2, 2);
    const zombie = addUnit(s, 'a1', 'zombie', 2, 8);
    expect(sightTo(s, soldier, { x: 20, y: 18 })).toBe(18);
    expect(detectionTo(s, zombie, soldier.pos)).toBe(TEMPLATES.zombie.detection);
    delete s.mission.night;
    expect(sightTo(s, soldier, { x: 20, y: 18 })).toBe(18);
  });

  it('a hidden squad is noticed from half as far, unless it stands in the light', () => {
    const run = (night: boolean, at: Pos) => {
      const s = dark();
      s.mission.night = night;
      s.concealed = true;
      addUnit(s, 's1', 'rookie', 25, 2);
      const scout = addUnit(s, 's2', 'rookie', 25, 3);
      const z = addUnit(s, 'a1', 'zombie', at.x, at.y + 4);
      pod(s, 'p1', false, [z]);
      expect(detectionTo(s, z, at)).toBe(night && !isLit(s, at) ? 3 : 6);
      const r = sim(s);
      r.moveAlong(scout, [at], 0);
      return s.pods[0]!.active;
    };
    // Four tiles from a zombie (detection 6): noticed by day, not at night...
    expect(run(false, { x: 10, y: 5, z: 0 })).toBe(true);
    expect(run(true, { x: 10, y: 5, z: 0 })).toBe(false);
    // ...unless the soldier walks under a street light.
    expect(run(true, { x: 2, y: 13, z: 0 })).toBe(true);
  });

  it('a Bengala lights its target and round it for two turns, then burns out; by day it lights nothing', () => {
    const s = dark();
    const u = addUnit(s, 's1', 'grenadier', 2, 2);
    addUnit(s, 's2', 'rookie', 3, 2);
    const z = addUnit(s, 'a1', 'zombie', 2, 12, { hp: 20, maxHp: 20 });
    pod(s, 'p1', true, [z]);
    const run = sim(s);
    run.perform(u, 'bolt', { target: z });
    const on = of(run.events, 'lightOn');
    expect(on).toEqual([{ t: 'lightOn', light: { id: expect.any(String), tile: { x: 2, y: 12, z: 0 }, radius: 3, turns: 2, tiles: expect.any(Array) } }]);
    const id = on[0]!.light.id;
    expect(isLit(s, { x: 4, y: 13 })).toBe(true);
    // Still lit through the next squad turn...
    const first = sim(s, 2);
    first.runAlienTurn();
    expect(of(first.events, 'lightOff')).toEqual([]);
    expect(s.lights!.find((l) => l.id === id)?.turns).toBe(1);
    // ...and out at the start of the one after.
    const second = sim(s, 3);
    second.runAlienTurn();
    expect(of(second.events, 'lightOff')).toEqual([{ t: 'lightOff', id }]);
    expect(s.lights!.map((l) => l.id)).toEqual(['l1']);

    const day = dark();
    day.mission.night = false;
    const v = addUnit(day, 's1', 'grenadier', 2, 2);
    const w = addUnit(day, 'a1', 'zombie', 2, 12, { hp: 20, maxHp: 20 });
    const lit = sim(day);
    lit.perform(v, 'bolt', { target: w });
    expect(of(lit.events, 'lightOn')).toEqual([]);
  });

  it('fire lights where it burns: the molotov and the bidón', () => {
    const s = dark();
    const u = addUnit(s, 's1', 'grenadier', 2, 2, { charges: { fireball: 1, velmarStorm: 1 } });
    const z = addUnit(s, 'a1', 'zombie', 2, 9, { hp: 30, maxHp: 30 });
    const run = sim(s);
    run.perform(u, 'fireball', { tile: { x: 2, y: 9, z: 0 } });
    expect(of(run.events, 'lightOn')[0]!.light).toMatchObject({ tile: { x: 2, y: 9 }, radius: NIGHT.fire.radius, turns: NIGHT.fire.turns });
    u.ap = 2;
    const storm = sim(s, 2);
    storm.perform(u, 'velmarStorm');
    expect(of(storm.events, 'lightOn').map((e) => e.light.tile)).toEqual([z.pos]);
  });

  it('a night mission: one more group of the dead, its street lights, and the same map', () => {
    const map = { kind: 'generated' as const, biome: 'city' as const, seed: 21 };
    const day = createMission({ map, slots: [0], rng: new Rng(1), kind: 'elimination', difficulty: 2 });
    const night = createMission({ map, slots: [0], rng: new Rng(1), kind: 'elimination', difficulty: 2, night: true });
    expect(day.mission.night).toBe(false);
    expect(night.mission.night).toBe(true);
    expect(night.pods.length).toBe(day.pods.length + 1);
    const extra = night.pods[night.pods.length - 1]!.unitIds.map((id) => night.units.find((u) => u.id === id)!.template);
    expect(extra).toEqual(nightPod(2));
    expect(night.tiles).toEqual(day.tiles);
    expect(night.lights!.length).toBeGreaterThan(4);
    for (const l of night.lights!) {
      expect(l.radius).toBe(NIGHT.streetLight);
      expect(l.turns).toBeUndefined();
      expect(night.tiles[l.tile.y * 48 + l.tile.x]).toBe('floor');
    }
  });

  it('more suministros at night: in what turns up and in what the operation pays', () => {
    for (let seed = 1; seed < 200; seed++) {
      const by = rollLoot('shelf', 2, new Rng(seed));
      const dusk = rollLoot('shelf', 2, new Rng(seed), true);
      if (by?.supplies) expect(dusk).toEqual({ supplies: Math.round(by.supplies * NIGHT.supplies) });
      else expect(dusk).toEqual(by);
    }
    const seeds = Array.from({ length: 300 }, (_, i) => i * 7919 + 13);
    const nights = seeds.filter(nightFor);
    expect(nights.length / seeds.length).toBeGreaterThan(0.25);
    expect(nights.length / seeds.length).toBeLessThan(0.42);
    const offer = (seed: number, extra: Partial<MissionOffer> = {}): MissionOffer => ({
      id: 'm1',
      name: 'Operación',
      kind: 'elimination',
      difficulty: 2,
      expiresDay: 9,
      reward: { credits: 100, recruit: 'assault' },
      map: { kind: 'generated', biome: 'city', seed },
      pods: [],
      final: false,
      region: 'na',
      facility: null,
      story: null,
      quest: null,
      ...extra,
    });
    expect(byNight(offer(nights[0]!))).toMatchObject({ night: true, reward: { credits: 150, recruit: 'assault' } });
    expect(byNight(offer(seeds.find((x) => !nightFor(x))!))).toEqual(offer(seeds.find((x) => !nightFor(x))!));
    // Story chapters and the final mission are flown by day.
    expect(byNight(offer(nights[0]!, { story: 2 })).night).toBeUndefined();
    expect(byNight(offer(nights[0]!, { final: true })).night).toBeUndefined();
    // In a campaign, every operation's night is its seed's.
    for (let seed = 1; seed < 12; seed++) {
      for (const o of createCampaign([0], new Rng(seed)).offers) expect(!!o.night).toBe(!o.story && !o.final && nightFor((o.map as { seed: number }).seed));
    }
  });

  it('the Saqueo’s horde comes a turn sooner at night', () => {
    const first = (night: boolean) => {
      const s = stateFrom(Array.from({ length: 40 }, () => '.'.repeat(40)));
      s.mission.kind = 'scavenge';
      s.mission.evacZone = [{ x: 39, y: 39 }];
      s.mission.horde = ['zombie', 'zombie'];
      s.mission.night = night;
      addUnit(s, 's1', 'rookie', 20, 20);
      for (let turn = 1; turn < 10; turn++) {
        const r = sim(s, turn);
        r.runAlienTurn();
        const wave = of(r.events, 'reinforcementsIncoming').find((e) => !e.r.trickle);
        if (wave) return turn;
      }
      return null;
    };
    expect(first(true)).toBe(first(false)! - NIGHT.hordeHead);
  });

  it('clients replaying a night end with the host state', () => {
    const host = createMission({ map: { kind: 'generated', biome: 'city', seed: 33 }, slots: [0], rng: new Rng(1), night: true });
    const client = cloneState(host);
    const run = sim(host, 4);
    const u = host.units.find((x) => x.team === 'xcom')!;
    const z = host.units.find((x) => x.team === 'alien')!;
    run.perform(u, 'bolt', { target: z });
    run.runAlienTurn();
    run.runAlienTurn();
    for (const e of run.events) applyEvent(client, e);
    expect(of(run.events, 'lightOn')).toHaveLength(1);
    expect(of(run.events, 'lightOff')).toHaveLength(1);
    expect(client).toEqual(host);
  });
});
