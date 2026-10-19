import { describe, expect, it } from 'vitest';
import { baseLayout, bastionLot, bastionYard, freshBase, placePiece } from '../src/base';
import { decorOf } from '../src/decor';
import { FURNITURE_OF_ITS_OWN, keepsWays, reachableFrom } from '../src/furnishing';
import { DIRS4, isWalkable } from '../src/grid';
import { roomAt } from '../src/loot';
import { generateLayout, type GenOptions, type MissionLayout } from '../src/mapgen';
import { Rng } from '../src/rng';
import { createMission } from '../src/setup';
import type { GameState, TileKind, Vec2 } from '../src/types';

// Arturo, 19/10: «que ocupen». What is drawn standing (an armchair, a lamp,
// a plant, a café's table, a hydrant, a chair over) stands on a tile nobody
// walks through or stops on; what lies flat stays walkable. And never where
// it would close a way.

const MAPS: [string, (seed: number) => GenOptions][] = [
  ['city', (seed) => ({ biome: 'city', kind: 'recovery', podSizes: [3, 3, 3], seed })],
  ['city hack', (seed) => ({ biome: 'city', kind: 'hack', podSizes: [3, 3], seed })],
  ['clinic', (seed) => ({ biome: 'city', kind: 'recovery', podSizes: [3, 3], seed, medical: 'clinic' })],
  ['hospital', (seed) => ({ biome: 'city', kind: 'rescue', podSizes: [3, 3], seed, medical: 'hospital' })],
  ['wilds', (seed) => ({ biome: 'wilds', kind: 'scavenge', podSizes: [3, 3], seed })],
  ['roadside', (seed) => ({ biome: 'wilds', kind: 'scavenge', podSizes: [3, 3], seed, site: 'roadside' })],
  ['facility', (seed) => ({ biome: 'facility', kind: 'sabotage', podSizes: [3, 3], seed })],
];
const SEEDS = [11, 2027, 40961, 77003, 123457, 999331];
const WAYS = new Set<TileKind | null>(['door', 'doorOpen', 'ladder', 'window', 'windowBroken']);

/** Each level of a layout: its tiles and decor, z. */
const layersOf = (l: MissionLayout) => [{ z: 0, tiles: l.tiles as (TileKind | null)[], decor: l.decor ?? [] }, ...(l.levels ?? []).map((v, k) => ({ z: k + 1, tiles: v.tiles, decor: v.decor ?? [] }))];

describe('what stands up takes its tile (19/10)', () => {
  it('stands on cover of its own, never by a door, a ladder, the stairs or a window, nor where anybody starts', () => {
    let seen = 0;
    for (const [name, opts] of MAPS) {
      for (const seed of SEEDS) {
        const l = generateLayout(opts(seed));
        const label = `${name}/${seed}`;
        const taken = new Set([...l.spawns, ...l.pods.flatMap((p) => p.tiles), l.item, l.objectiveSpot].filter((p): p is Vec2 => !!p).map((p) => `${p.x},${p.y}`));
        for (const layer of layersOf(l)) {
          layer.tiles.forEach((kind, i) => {
            const look = decorOf(layer.decor[i]);
            if (!look || !FURNITURE_OF_ITS_OWN.has(look.kind)) return;
            seen++;
            const p = { x: i % l.width, y: Math.floor(i / l.width) };
            expect(kind, `${label}: ${look.kind} at ${p.x},${p.y},${layer.z}`).toBe(look.kind === 'drinksFridge' ? 'high' : 'low');
            for (const d of DIRS4) expect(WAYS.has(layer.tiles[(p.y + d.y) * l.width + p.x + d.x] ?? null), `${label}: ${look.kind} by a way at ${p.x},${p.y},${layer.z}`).toBe(false);
            if (!layer.z) expect(taken.has(`${p.x},${p.y}`), `${label}: on somebody at ${p.x},${p.y}`).toBe(false);
          });
        }
        // And every place one reached without it (its tiles floor again) is reached still, from where the squad starts.
        const map = (strip: boolean) => {
          const own = (k: TileKind | null, d: number | undefined) => strip && FURNITURE_OF_ITS_OWN.has(decorOf(d)?.kind ?? 'bus');
          return {
            width: l.width,
            height: l.height,
            tiles: l.tiles.map((k, i) => (own(k, l.decor?.[i]) ? 'floor' : k)),
            elev: l.elev,
            decor: l.decor ?? [],
            levels: (l.levels ?? []).map((v) => ({ ...v, tiles: v.tiles.map((k, i) => (own(k, v.decor?.[i]) ? 'floor' : k)) })),
          };
        };
        const before = reachableFrom(map(true), { ...l.spawns[0]!, z: 0 });
        const after = reachableFrom(map(false), { ...l.spawns[0]!, z: 0 });
        const area = l.width * l.height;
        for (const n of before) {
          const z = Math.floor(n / area);
          const i = n - z * area;
          const layer = layersOf(l)[z]!;
          if (FURNITURE_OF_ITS_OWN.has(decorOf(layer.decor[i])?.kind ?? 'bus')) continue;
          expect(after.has(n), `${label}: ${i % l.width},${Math.floor(i / l.width)},${z} cut off`).toBe(true);
        }
      }
    }
    expect(seen).toBeGreaterThan(200);
  }, 60_000);

  it('keepsWays: an object goes only where what is round it stays joined round it', () => {
    // A corridor a tile wide: never in it; a room: anywhere but where it would cut it in two.
    const tiles = (rows: string[]) => ({ tiles: rows.join('').split('').map((c): TileKind => (c === '#' ? 'wall' : c === 'h' ? 'low' : 'floor')) });
    const corridor = tiles(['#####', '#...#', '#####']);
    expect(keepsWays(corridor, 5, 3, { x: 2, y: 1 })).toBe(false);
    expect(keepsWays(corridor, 5, 3, { x: 1, y: 1 })).toBe(true);
    const room = tiles(['#####', '#...#', '#...#', '#...#', '#####']);
    expect(keepsWays(room, 5, 5, { x: 2, y: 2 })).toBe(true);
    const half = tiles(['#####', '#.h.#', '#...#', '#.h.#', '#####']);
    expect(keepsWays(half, 5, 5, { x: 2, y: 2 })).toBe(false);
    expect(keepsWays(half, 5, 5, { x: 1, y: 1 })).toBe(true);
  });

  it('a house keeps its rooms whole and most of their floor, and its furniture is there', () => {
    const kinds = new Set<string>();
    for (const seed of SEEDS) {
      const s = createMission({ map: { kind: 'generated', biome: 'city', seed }, slots: [0], rng: new Rng(seed), kind: 'recovery' });
      for (const home of s.homes ?? []) {
        for (const room of home.rooms) {
          const layer = room.z ? s.levels![room.z - 1]! : { tiles: s.tiles, decor: s.decor ?? [] };
          const cells: Vec2[] = [];
          for (let y = room.y; y < room.y + room.h; y++) for (let x = room.x; x < room.x + room.w; x++) cells.push({ x, y });
          const at = (p: Vec2) => p.y * s.width + p.x;
          const own = cells.filter((p) => {
            const kind = decorOf(layer.decor?.[at(p)])?.kind;
            if (kind) kinds.add(kind);
            return !!kind && FURNITURE_OF_ITS_OWN.has(kind) && kind !== 'strewn';
          }).length;
          const blocked = cells.filter((p) => ['low', 'high'].includes(layer.tiles[at(p)] ?? '')).length;
          const strewn = cells.filter((p) => decorOf(layer.decor?.[at(p)])?.kind === 'strewn').length;
          if (own) expect(blocked - strewn, `${seed}: room ${room.x},${room.y},${room.z}`).toBeLessThanOrEqual(Math.floor(room.w * room.h * 0.4));
          // One room still: all its floor joined, round the furniture.
          const ids = new Set(cells.filter((p) => isWalkable(layer.tiles[at(p)] ?? null) && layer.tiles[at(p)] !== 'door' && layer.tiles[at(p)] !== 'doorOpen').map((p) => roomAt(s as GameState, { ...p, z: room.z })?.id));
          expect(ids.size, `${seed}: room ${room.x},${room.y},${room.z} split`).toBeLessThanOrEqual(1);
        }
      }
    }
    for (const k of ['armchair', 'floorLamp', 'pottedPlant', 'nightstand', 'diningChair', 'towelRack']) expect(kinds.has(k), k).toBe(true);
  }, 60_000);

  it('a chair over, a fallen drip or bags left packed stand on a tile of their own; the dead lie on the floor', () => {
    const upright = new Set(['overturnedChair', 'fallenIV', 'luggage']);
    let count = 0;
    for (const medical of ['clinic', 'ward', 'hospital', undefined] as const) {
      for (const seed of SEEDS) {
        const s = createMission({ map: { kind: 'generated', biome: 'city', seed, ...(medical ? { medical } : {}) }, slots: [0], rng: new Rng(seed), kind: medical === 'hospital' ? 'rescue' : 'recovery' });
        for (const site of [...(s.medical ?? []), ...(s.homes ?? [])]) {
          for (const d of site.dressing ?? []) {
            const layer = d.z ? s.levels![d.z - 1]! : { tiles: s.tiles, decor: s.decor ?? [] };
            const i = d.y * s.width + d.x;
            const look = decorOf(layer.decor?.[i])?.kind;
            if (upright.has(d.kind)) {
              count++;
              expect(layer.tiles[i], `${seed}: ${d.kind}`).toBe('low');
              // On a bedside table (the clinics' bags), or on its own tile.
              expect(look === 'strewn' || look === 'bedsideTable', `${seed}: ${d.kind} on ${look}`).toBe(true);
            }
            if (d.kind === 'corpseCivilian' || d.kind === 'corpseStaff' || d.kind === 'coveredCorpse') expect(layer.tiles[i]).toBe('floor');
          }
        }
      }
    }
    expect(count).toBeGreaterThan(10);
  }, 60_000);

  it('a hydrant or a mailbox stands by the kerb, on the pavement, facing the street', () => {
    let count = 0;
    for (const seed of SEEDS) {
      const l = generateLayout({ biome: 'city', kind: 'elimination', podSizes: [3, 3], seed });
      l.tiles.forEach((kind, i) => {
        const look = decorOf(l.decor?.[i]);
        if (look?.kind !== 'hydrant' && look?.kind !== 'mailbox') return;
        count++;
        const d = DIRS4[look.facing]!;
        const x = i % l.width;
        const y = Math.floor(i / l.width);
        expect(kind).toBe('low');
        expect(l.ground![i]).toBe(0);
        expect(l.ground![(y + d.y) * l.width + x + d.x]).toBe(1);
      });
    }
    expect(count).toBeGreaterThan(5);
  });

  it('the same seed furnishes the same; a mission saved before keeps its tiles', () => {
    const a = generateLayout({ biome: 'city', kind: 'recovery', podSizes: [3, 3], seed: 2027 });
    const b = generateLayout({ biome: 'city', kind: 'recovery', podSizes: [3, 3], seed: 2027 });
    expect(a.tiles).toEqual(b.tiles);
    expect(a.decor).toEqual(b.decor);
    expect(a.levels).toEqual(b.levels);
  });
});

describe('La Lata leaves by an open way (19/10)', () => {
  it('nothing stands between the bus and the edge it drives off by', () => {
    let buses = 0;
    for (const [name, opts] of MAPS) {
      for (const seed of SEEDS) {
        const l = generateLayout({ ...opts(seed), kind: 'recovery' });
        const bus = l.evacBus;
        if (!bus) continue;
        buses++;
        const d = DIRS4[bus.heading]!;
        for (const t of bus.tiles) {
          for (let p = { x: t.x + d.x, y: t.y + d.y }; p.x >= 0 && p.y >= 0 && p.x < l.width && p.y < l.height; p = { x: p.x + d.x, y: p.y + d.y }) {
            const i = p.y * l.width + p.x;
            if (decorOf(l.decor?.[i])?.kind === 'bus') continue;
            expect(['low', 'high'].includes(l.tiles[i]!), `${name}/${seed}: ${l.tiles[i]} at ${p.x},${p.y}`).toBe(false);
          }
        }
      }
    }
    expect(buses).toBeGreaterThan(20);
  }, 60_000);
});

describe("the Bastion's yard (19/10)", () => {
  it('what is drawn in its yard is low cover nobody starts on, and no free tile is shut in among it', () => {
    for (const level of [1, 2, 3, 4, 5, 6, 7, 8]) {
      const base = freshBase(new Rng(level));
      base.level = level;
      const layout = baseLayout(base);
      const yard = bastionYard(base);
      expect(yard.length, `level ${level}`).toBeGreaterThan(0);
      for (const p of yard) {
        expect(layout.tiles[p.y * base.width + p.x], `level ${level}: ${p.x},${p.y}`).toBe('low');
        expect(layout.spawns.some((q) => q.x === p.x && q.y === p.y)).toBe(false);
      }
      expect(layout.spawns.length).toBe(24);
      // Every free tile of the lot joins the clearing.
      const lot = bastionLot(base)!;
      const at = (x: number, y: number) => y * base.width + x;
      const open = new Set<number>([at(lot.x - 1, lot.y - 1)]);
      const queue = [{ x: lot.x - 1, y: lot.y - 1 }];
      while (queue.length) {
        const p = queue.pop()!;
        for (const d of DIRS4) {
          const q = { x: p.x + d.x, y: p.y + d.y };
          if (q.x < lot.x - 2 || q.y < lot.y - 2 || q.x > lot.x + lot.w + 1 || q.y > lot.y + lot.h + 1 || open.has(at(q.x, q.y)) || layout.tiles[at(q.x, q.y)] !== 'floor') continue;
          open.add(at(q.x, q.y));
          queue.push(q);
        }
      }
      for (let y = lot.y; y < lot.y + lot.h; y++) for (let x = lot.x; x < lot.x + lot.w; x++) if (layout.tiles[at(x, y)] === 'floor') expect(open.has(at(x, y)), `level ${level}: ${x},${y} shut in`).toBe(true);
    }
  });

  it('nobody is posted on it, and a siege puts nobody there', () => {
    const base = freshBase(new Rng(3));
    base.level = 2;
    placePiece(base, 'palisade', 3, 3, 0);
    const s = createMission({ map: { kind: 'base', base }, slots: [0], rng: new Rng(3) });
    const yard = new Set(bastionYard(base).map((p) => p.y * base.width + p.x));
    for (const u of s.units) expect(yard.has(u.pos.y * s.width + u.pos.x)).toBe(false);
  });
});
