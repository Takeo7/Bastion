import { describe, expect, it } from 'vitest';
import { decorOf } from '../src/decor';
import { DIRS4 } from '../src/grid';
import { generateLayout, TOWN_SIZE, type MissionLayout } from '../src/mapgen';
import { Rng } from '../src/rng';
import { Sim } from '../src/rules';
import { createMission, garrison, hordeWave, routeTurns, type MapSource } from '../src/setup';
import { AVENUE, STREET, townPlan } from '../src/townPlan';
import type { MissionKind, Vec2 } from '../src/types';

// docs/MAPAS.md, phase 3 and §5.10 (Arturo, 19/10: «que los mapas no sean cuadrados, que sean más orgánicos, con
// bordes con sentido»): a town of 64 on a plan of streets, what is played closed round by what makes sense, its ways in.

const OUT = 4;
const ROAD = 1;
const town = (seed: number, kind: MissionKind = 'recovery') => generateLayout({ biome: 'city', kind, podSizes: [3, 3, 3, 3], seed: seed * 7919, ...(kind === 'sabotage' ? { alarm: 'police' as const } : {}) });

/** Each tile of `l` in how many pieces of its frame. */
function covered(l: MissionLayout): Int32Array {
  const n = new Int32Array(l.width * l.height);
  for (const f of l.frame ?? []) for (let y = f.y; y < f.y + f.h; y++) for (let x = f.x; x < f.x + f.w; x++) n[y * l.width + x]!++;
  return n;
}

describe('the plan of a town (townPlan.ts)', () => {
  it('a grid has an avenue and streets of five; an old town, its square and a high street; most of the map is played', () => {
    for (let seed = 1; seed <= 20; seed++) {
      for (const old of [false, true]) {
        const p = townPlan(TOWN_SIZE, TOWN_SIZE, new Rng(seed), old);
        const label = `${old ? 'old' : 'grid'}/${seed}`;
        const avenue = p.roads.filter((r) => r.avenue);
        expect(avenue.length, label).toBeGreaterThan(0);
        for (const r of avenue) expect(r.vertical ? r.r.w : r.r.h, label).toBe(old ? STREET : AVENUE);
        if (!old) for (const r of p.roads.filter((x) => !x.avenue)) expect(r.vertical ? r.r.w : r.r.h, label).toBe(STREET);
        expect(!!p.square, label).toBe(old);
        const played = p.out.filter((o) => !o).length / p.out.length;
        expect(played, label).toBeGreaterThan(0.6);
        expect(played, label).toBeLessThan(0.92);
        expect(p.entries.length, label).toBeGreaterThanOrEqual(2);
        expect(p.entries.length, label).toBeLessThanOrEqual(4);
        expect(p.entries[0], label).toBe(p.squadEntry);
        // A seed is one plan.
        expect(townPlan(TOWN_SIZE, TOWN_SIZE, new Rng(seed), old), label).toEqual(p);
      }
    }
  });
});

describe('the ways in stay open', () => {
  it('where the squad comes in and every way in are never shut, whatever is closed beside them', () => {
    for (let seed = 0; seed < 200; seed++) {
      for (const [old, wide] of [[false, false], [true, false], [false, true]] as const) {
        const p = townPlan(wide ? 72 : TOWN_SIZE, TOWN_SIZE, new Rng(seed), old, wide);
        const label = `${seed}/${old ? 'old' : wide ? 'wide' : 'grid'}`;
        expect(p.out[p.squad.y * p.width + p.squad.x], label).toBe(false);
        for (const e of p.entries) {
          const { r } = e.road;
          for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) if (p.out[y * p.width + x]) expect.fail(`${label}: ${x},${y} shut`);
        }
      }
    }
  }, 30_000);
});

describe('a town on its plan (docs/MAPAS.md phase 3)', () => {
  it('its outline: every tile out of play a wall in exactly one rectangle of the frame, and none in play', () => {
    for (let seed = 1; seed <= 12; seed++) {
      const l = town(seed);
      const label = `${seed}`;
      expect(l.frame?.length, label).toBeGreaterThan(0);
      const n = covered(l);
      l.ground!.forEach((g, i) => {
        const at = `${label}: ${i % l.width},${Math.floor(i / l.width)}`;
        expect(n[i], at).toBe(g === OUT ? 1 : 0);
        if (g === OUT) expect(l.tiles[i], at).toBe('wall');
      });
      for (const f of l.frame!) {
        // A river, the railway, the motorway or a barrier runs along a side; a block shut up, a ruin or works stands so many storeys.
        expect(f.side !== undefined, `${label}: ${f.kind}`).toBe(['river', 'rail', 'highway', 'barrier'].includes(f.kind));
        if (['block', 'ruin', 'works'].includes(f.kind)) expect(f.storeys, `${label}: ${f.kind}`).toBeGreaterThan(0);
        if (f.storeys) for (let y = f.y; y < f.y + f.h; y++) for (let x = f.x; x < f.x + f.w; x++) expect(l.storeys![y * l.width + x], label).toBe(f.storeys);
      }
    }
  });

  it('nothing played by the edge but on its ways in, two to four streets the dead come in by', () => {
    for (let seed = 1; seed <= 12; seed++) {
      const l = town(seed);
      const W = l.width;
      const H = l.height;
      // The ways in: streets that reach the edge, each a run of road along it.
      const runs = new Set<string>();
      for (let i = 0; i < W * H; i++) {
        const x = i % W;
        const y = Math.floor(i / W);
        const edge = Math.min(x, y, W - 1 - x, H - 1 - y);
        if (l.ground![i] === OUT) continue;
        expect(edge, `${seed}: ${x},${y} played by the edge`).toBeGreaterThanOrEqual(l.ground![i] === ROAD ? 0 : 2);
        if (edge === 0) runs.add(x === 0 || x === W - 1 ? `${x}/${Math.floor(y / 8)}` : `${Math.floor(x / 8)}/${y}`);
      }
      expect(runs.size, `${seed}`).toBeGreaterThanOrEqual(2);
    }
  });

  it('the bus waits by the kerb, two by nine, facing its way out, the evacuation along its doors three by seven', () => {
    let along = 0;
    for (let seed = 1; seed <= 12; seed++) {
      for (const kind of ['recovery', 'sabotage', 'rescue'] as MissionKind[]) {
        const l = town(seed, kind);
        const label = `${kind}/${seed}`;
        if (l.evacZone.length !== 21) continue;
        along++;
        const bus = l.evacBus!;
        expect(bus.tiles.length, label).toBe(18);
        for (const p of bus.tiles) {
          expect(l.tiles[p.y * l.width + p.x], label).toBe('high');
          expect(decorOf(l.decor![p.y * l.width + p.x])?.kind, label).toBe('bus');
        }
        // Long the way it faces.
        const xs = bus.tiles.map((p) => p.x);
        const ys = bus.tiles.map((p) => p.y);
        const long = bus.heading % 2 === 0 ? Math.max(...ys) - Math.min(...ys) + 1 : Math.max(...xs) - Math.min(...xs) + 1;
        expect(long, label).toBe(9);
        // Every tile of the evacuation beside the bus or beside one that is, floor all of it.
        const isBus = (p: Vec2) => bus.tiles.some((q) => q.x === p.x && q.y === p.y);
        const near = (p: Vec2, d: number) => bus.tiles.some((q) => Math.abs(q.x - p.x) + Math.abs(q.y - p.y) <= d);
        for (const z of l.evacZone) {
          expect(isBus(z), label).toBe(false);
          expect(near(z, 3), label).toBe(true);
          expect(l.tiles[z.y * l.width + z.x], label).toBe('floor');
        }
        // Its way out, straight on to the edge, is road.
        const d = DIRS4[bus.heading]!;
        const nose = bus.tiles.reduce((a, p) => (p.x * d.x + p.y * d.y > a.x * d.x + a.y * d.y ? p : a));
        for (let p = { x: nose.x + d.x, y: nose.y + d.y }; p.x >= 0 && p.y >= 0 && p.x < l.width && p.y < l.height; p = { x: p.x + d.x, y: p.y + d.y }) {
          expect(l.ground![p.y * l.width + p.x], `${label}: its way out at ${p.x},${p.y}`).toBe(ROAD);
        }
      }
    }
    // Nearly always by the bus; a town with no room on its streets has the evacuation of before.
    expect(along).toBeGreaterThan(30);
  });

  it('the mission takes as many turns as its walk asks, and the groups of the dead grow with the map', () => {
    for (let seed = 1; seed <= 4; seed++) {
      const map = { kind: 'generated' as const, biome: 'city' as const, seed: seed * 7919 };
      const s = createMission({ map, slots: [0], rng: new Rng(seed), kind: 'recovery', difficulty: 2 });
      const l = generateLayout({ biome: 'city', kind: 'recovery', podSizes: [3], seed: seed * 7919 });
      expect(s.mission.turnsLeft).toBe(routeTurns('recovery', l.route!));
      // Eight groups at 48, eleven at 64 (docs/MAPAS.md §6) and a quarter more (20/10): thirteen; and the garrison, if the mission has one.
      // The lone dead of 22/10 (setup.ts LURKERS) come on top, in pods of their own (l1, l2…).
      expect(s.pods.filter((p) => !p.id.startsWith('l')).length).toBe(13 + (garrison('recovery', 2) ? 1 : 0));
      expect(s.frame).toEqual(l.frame);
    }
  });

  it("La Lata takes the place it needs: the 4×4 three tiles, its nose where the bus's was; what is behind it, ground again", () => {
    for (let seed = 1; seed <= 6; seed++) {
      const map = { kind: 'generated' as const, biome: 'city' as const, seed: seed * 7919 };
      const bus = createMission({ map, slots: [0], rng: new Rng(seed), kind: 'recovery', vehicle: 3 });
      const car = createMission({ map, slots: [0], rng: new Rng(seed), kind: 'recovery', vehicle: 0 });
      const full = bus.mission.evacBus!.tiles;
      const short = car.mission.evacBus!.tiles;
      expect(short.length).toBe(Math.min(6, full.length));
      // The nose stays: every tile kept was the bus's front.
      const d = DIRS4[car.mission.evacBus!.heading]!;
      const along = (p: Vec2) => p.x * d.x + p.y * d.y;
      expect(Math.max(...short.map(along))).toBe(Math.max(...full.map(along)));
      for (const p of full) {
        const kept = short.some((q) => q.x === p.x && q.y === p.y);
        expect(car.tiles[p.y * car.width + p.x]).toBe(kept ? 'high' : 'floor');
      }
    }
  });

  it('turned with the map: the frame and the bus follow the tiles', () => {
    const seen = new Set<string>();
    for (let seed = 1; seed <= 16; seed++) {
      const l = town(seed);
      const n = covered(l);
      // The outline is where the frame says, whichever way the map was turned.
      l.ground!.forEach((g, i) => expect(n[i] === 1, `${seed}`).toBe(g === OUT));
      if (l.evacBus) {
        const d = DIRS4[l.evacBus.heading]!;
        seen.add(`${d.x},${d.y}`);
      }
    }
    expect(seen.size).toBeGreaterThan(1);
  });
});

describe('the ways in through the outline (docs/MAPAS.md §5.10)', () => {
  it("the country's bus faces its way out: the track or the road on to the edge, open", () => {
    let along = 0;
    let buses = 0;
    for (const site of [undefined, 'roadside'] as const) {
      for (let seed = 1; seed <= 20; seed++) {
        const l = generateLayout({ biome: 'wilds', kind: 'recovery', podSizes: [3, 3, 3], seed: seed * 7919, ...(site ? { site } : {}) });
        const label = `${site ?? 'wilds'}/${seed}`;
        if (!l.evacBus) continue;
        buses++;
        // By its far end, along its doors; where there is no room there, the evacuation of before.
        if (l.evacZone.length !== 21) continue;
        along++;
        const d = DIRS4[l.evacBus.heading]!;
        const nose = l.evacBus.tiles.reduce((a, p) => (p.x * d.x + p.y * d.y > a.x * d.x + a.y * d.y ? p : a));
        for (let p = { x: nose.x + d.x, y: nose.y + d.y }; p.x >= 0 && p.y >= 0 && p.x < l.width && p.y < l.height; p = { x: p.x + d.x, y: p.y + d.y }) {
          expect(l.ground![p.y * l.width + p.x], `${label}: its way out at ${p.x},${p.y}`).toBe(ROAD);
          expect(l.tiles[p.y * l.width + p.x], `${label}: its way out at ${p.x},${p.y}`).toBe('floor');
        }
      }
    }
    expect(along).toBeGreaterThan(buses * 0.8);
  });

  it('every map shut round has room by an edge for the biggest wave of the dead', () => {
    const maps: [string, MapSource][] = [];
    for (let seed = 1; seed <= 5; seed++) {
      maps.push(['city', { kind: 'generated', biome: 'city', seed: seed * 7919 }]);
      maps.push(['industrial', { kind: 'generated', biome: 'city', site: 'industrial', seed: seed * 7919 }]);
      maps.push(['wilds', { kind: 'generated', biome: 'wilds', seed: seed * 7919 }]);
      maps.push(['roadside', { kind: 'generated', biome: 'wilds', site: 'roadside', seed: seed * 7919 }]);
      maps.push(['facility', { kind: 'generated', biome: 'facility', seed: seed * 7919 }]);
    }
    for (const [name, map] of maps) {
      const s = createMission({ map, slots: [0], rng: new Rng(1), kind: 'sabotage', difficulty: 4 });
      const label = `${name}/${'seed' in map ? map.seed : ''}`;
      s.mission.reinforcements = [{ id: 'r1', turn: 1, pos: null, units: hordeWave(4), landed: false, horde: true }];
      new Sim(s, new Rng(2), [0]).landReinforcements(1);
      expect(s.mission.reinforcements[0]!.landed, label).toBe(true);
      // All of it by the edge, in by a way in.
      for (const u of s.units.filter((x) => x.id.startsWith('r1'))) expect(Math.min(u.pos.x, u.pos.y, s.width - 1 - u.pos.x, s.height - 1 - u.pos.y), label).toBeLessThan(4);
    }
  });
});
