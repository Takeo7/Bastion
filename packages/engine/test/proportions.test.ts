import { describe, expect, it } from 'vitest';
import { anchorOf, decorOf, FOOTPRINT, objectAt } from '../src/decor';
import { reachableFrom } from '../src/furnishing';
import { canStandOn, DIRS4, DIRS8, isWalkable } from '../src/grid';
import { roomAt } from '../src/loot';
import type { MissionLayout } from '../src/mapgen';
import { stepCost } from '../src/path';
import { missionLayout, podPlan, type MapSource } from '../src/setup';
import type { Alarm, Decor, DecorKind, GameState, MissionKind, TileKind, Vec2 } from '../src/types';

// docs/MAPAS.md, phase 0: the proportions of the generated maps as a test, the net the later phases rebuild them
// under. Seeded maps of each type, through the entry the server uses (setup.ts missionLayout → mapgen.ts
// generateLayout). Phase 1: what stands over several tiles (a car 1 × 3, an ambulance 2 × 4, a bed 1 × 2…) is
// whole, of its footprint, and closes no way; every place one can stand on is reached still; a seed is one map.

interface MapType {
  name: string;
  map: (seed: number) => MapSource;
  kind: MissionKind;
  hospital?: boolean;
  alarm?: Alarm;
  /** The share of the ground's tiles that is cover, today (low, high). */
  cover: [number, number];
}

const TYPES: MapType[] = [
  { name: 'city sabotage', map: (seed) => ({ kind: 'generated', biome: 'city', seed }), kind: 'sabotage', alarm: 'police', cover: [0.05, 0.18] },
  { name: 'city recovery', map: (seed) => ({ kind: 'generated', biome: 'city', seed }), kind: 'recovery', cover: [0.05, 0.18] },
  { name: 'industrial', map: (seed) => ({ kind: 'generated', biome: 'city', seed, site: 'industrial' }), kind: 'sabotage', alarm: 'ambulance', cover: [0.06, 0.2] },
  { name: 'clinic', map: (seed) => ({ kind: 'generated', biome: 'city', seed, medical: 'clinic' }), kind: 'recovery', cover: [0.05, 0.18] },
  { name: 'ward', map: (seed) => ({ kind: 'generated', biome: 'city', seed, medical: 'ward' }), kind: 'recovery', cover: [0.05, 0.18] },
  { name: 'hospital', map: (seed) => ({ kind: 'generated', biome: 'city', seed, medical: 'hospital' }), kind: 'rescue', hospital: true, cover: [0.05, 0.18] },
  { name: 'wilds', map: (seed) => ({ kind: 'generated', biome: 'wilds', seed }), kind: 'hack', cover: [0.15, 0.4] },
  { name: 'roadside', map: (seed) => ({ kind: 'generated', biome: 'wilds', seed, site: 'roadside' }), kind: 'recovery', cover: [0.12, 0.35] },
  { name: 'facility', map: (seed) => ({ kind: 'generated', biome: 'facility', seed }), kind: 'sabotage', alarm: 'ambulance', cover: [0.05, 0.2] },
];
const SEEDS = [3, 1201, 20011, 77017];
const PODS = podPlan(2).map((p) => p.length);

const layoutOf = (t: MapType, seed: number) => missionLayout(t.map(seed), t.kind, PODS, false, false, !!t.hospital, t.alarm);
const MAPS = TYPES.flatMap((t) => SEEDS.map((seed) => ({ t, seed, label: `${t.name}/${seed}`, l: layoutOf(t, seed) })));

/** Each level of a layout: its tiles and decor, and z. */
const layersOf = (l: MissionLayout) => [
  { z: 0, tiles: l.tiles as readonly (TileKind | null)[], decor: (l.decor ?? []) as readonly Decor[] },
  ...(l.levels ?? []).map((v, k) => ({ z: k + 1, tiles: v.tiles, decor: (v.decor ?? []) as readonly Decor[] })),
];

/**
 * What each object over several tiles may take (decor.ts FOOTPRINT), as [wide, deep]: across the way it faces and
 * along it. A patrol car's or an ambulance's counts the siren's tile; a lorry's block is 2 × 6 where it fits, 2 × 4
 * where not, and a barn's tractor 2 × 3; a siren's car squeezed in where no other fits keeps the two tiles of before.
 */
const SHAPES: Partial<Record<DecorKind, (readonly [number, number])[]>> = {
  carFront: [FOOTPRINT.car],
  sirenCar: [FOOTPRINT.patrolCar, FOOTPRINT.ambulance, [1, 2]],
  truck: [FOOTPRINT.truck, FOOTPRINT.truckShort, FOOTPRINT.tractor],
  container: [FOOTPRINT.container],
  bed: [FOOTPRINT.bed, FOOTPRINT.doubleBed],
  sofa: [FOOTPRINT.sofa],
  bathtub: [FOOTPRINT.bathtub],
  table: [FOOTPRINT.table],
  hospitalBed: [FOOTPRINT.hospitalBed],
  gurney: [FOOTPRINT.gurney],
};

/** Furniture over several tiles, indoors: what never stands by a way through a wall or up a storey. */
const FURNITURE = new Set<DecorKind>(['bed', 'sofa', 'bathtub', 'table', 'hospitalBed', 'gurney']);
const WAYS = new Set<TileKind | null>(['door', 'doorOpen', 'window', 'windowBroken', 'ladder']);
/** Where a building's flight of stairs on tile j of `layer` is walked onto from: the tile behind it (it climbs the way it faces). */
const footOf = (layer: { decor: readonly Decor[] }, width: number, j: number): number | null => {
  const look = decorOf(layer.decor[j]);
  if (look?.kind !== 'stairs') return null;
  const d = DIRS4[look.facing]!;
  return j - d.y * width - d.x;
};

describe('the proportions of the generated maps (docs/MAPAS.md)', () => {
  it('phase 1: every object over several tiles is whole, of its footprint and on cover of one kind', () => {
    const counts = new Map<string, number>();
    for (const { l, label } of MAPS) {
      for (const layer of layersOf(l)) {
        layer.decor.forEach((code, i) => {
          const look = decorOf(code);
          if (!look) return;
          const at = `${label}: ${look.kind} at ${i % l.width},${Math.floor(i / l.width)},${layer.z}`;
          // A part leads to its anchor, on cover of the anchor's kind.
          if (look.kind === 'part') {
            const a = anchorOf(layer.decor, l.width, i);
            expect(a, `${at}: a part of nothing`).toBeGreaterThanOrEqual(0);
            expect(layer.tiles[i], at).toBe(layer.tiles[a]);
            return;
          }
          const shapes = SHAPES[look.kind];
          if (!shapes) return;
          const o = objectAt(layer.decor, l.width, i)!;
          if (o.anchor !== i) return;
          const tiles = [...o.tiles];
          // The siren's own tile is the rest of its car (rules.ts damage makes it a part when it falls silent).
          if (look.kind === 'sirenCar' && l.objectiveSpot) tiles.push(l.objectiveSpot.y * l.width + l.objectiveSpot.x);
          const xs = tiles.map((t) => t % l.width);
          const ys = tiles.map((t) => Math.floor(t / l.width));
          const w = Math.max(...xs) - Math.min(...xs) + 1;
          const h = Math.max(...ys) - Math.min(...ys) + 1;
          expect(tiles.length, `${at}: not a rectangle (${w} × ${h})`).toBe(w * h);
          // [wide, deep] by the way it faces: deep along it.
          const along = look.facing % 2 === 0 ? h : w;
          const across = look.facing % 2 === 0 ? w : h;
          expect(shapes.some(([a, b]) => a === across && b === along), `${at}: ${across} × ${along}`).toBe(true);
          for (const t of o.tiles) expect(['low', 'high'], at).toContain(layer.tiles[t]);
          expect(new Set(o.tiles.map((t) => layer.tiles[t])).size, `${at}: of two kinds of cover`).toBe(1);
          const key = `${look.kind} ${across}×${along}`;
          counts.set(key, (counts.get(key) ?? 0) + 1);
        });
      }
    }
    // What phase 1 made bigger is there, at its new size.
    for (const key of ['carFront 1×3', 'sirenCar 1×3', 'sirenCar 2×4', 'truck 2×6', 'container 2×5', 'bed 1×2', 'bed 2×2', 'sofa 2×1', 'bathtub 2×1', 'table 2×1', 'hospitalBed 1×2', 'gurney 1×2']) {
      expect(counts.get(key) ?? 0, key).toBeGreaterThan(0);
    }
  });

  it('phase 1: furniture over several tiles stands clear of doors, windows, ladders and stairs, and every room stays one', () => {
    for (const { l, label } of MAPS) {
      const s = l as unknown as GameState;
      for (const layer of layersOf(l)) {
        layer.decor.forEach((code, i) => {
          const o = decorOf(code)?.kind === 'part' ? null : objectAt(layer.decor, l.width, i);
          if (!o || o.anchor !== i || o.tiles.length < 2 || !FURNITURE.has(o.kind)) return;
          for (const t of o.tiles) {
            const p = { x: t % l.width, y: Math.floor(t / l.width) };
            for (const d of DIRS4) {
              const j = (p.y + d.y) * l.width + p.x + d.x;
              // A flight of stairs put in later may stand beside it (mapgen.ts placeStairs), never on the tile one climbs it from.
              const foot = footOf(layer, l.width, j);
              if (foot !== null) expect(foot, `${label}: ${o.kind} at the foot of the stairs at ${p.x},${p.y},${layer.z}`).not.toBe(t);
              else expect(WAYS.has(layer.tiles[j] ?? null), `${label}: ${o.kind} by a way at ${p.x},${p.y},${layer.z}`).toBe(false);
            }
          }
        });
      }
      // A house's or a clinic's room: all its floor one room (loot.ts roomAt), round its furniture.
      const rooms = [...(l.homes ?? []).flatMap((h) => h.rooms), ...(l.medical ?? []).flatMap((m) => (m.rooms ?? []).filter((r) => r.use !== 'stairs'))];
      for (const room of rooms) {
        const layer = room.z ? l.levels![room.z - 1]! : { tiles: l.tiles };
        const ids = new Set<number | undefined>();
        for (let y = room.y; y < room.y + room.h; y++) {
          for (let x = room.x; x < room.x + room.w; x++) {
            const kind = layer.tiles[y * l.width + x] ?? null;
            if (isWalkable(kind) && kind !== 'door' && kind !== 'doorOpen' && kind !== 'window' && kind !== 'windowBroken') ids.add(roomAt(s, { x, y, z: room.z })?.id);
          }
        }
        expect(ids.size, `${label}: room ${room.x},${room.y},${room.z} cut in two`).toBeLessThanOrEqual(1);
      }
    }
  });

  it('every place one can stand on, on every storey, is reached from where the squad starts', () => {
    for (const { l, label } of MAPS) {
      const reach = reachableFrom({ width: l.width, height: l.height, tiles: l.tiles, elev: l.elev, decor: l.decor ?? [], levels: l.levels ?? [] }, { ...l.spawns[0]!, z: 0 });
      const area = l.width * l.height;
      for (const layer of layersOf(l)) {
        layer.tiles.forEach((kind, i) => {
          if (canStandOn(kind) && !reach.has(layer.z * area + i)) expect.fail(`${label}: ${i % l.width},${Math.floor(i / l.width)},${layer.z} out of reach`);
        });
      }
    }
  });

  it('a seed is one map', () => {
    for (const t of TYPES) expect(layoutOf(t, SEEDS[1]!), t.name).toEqual(MAPS.find((m) => m.t === t && m.seed === SEEDS[1])!.l);
  });

  it("today's figures: rooms of 3 × 3 or more, the ground's share of cover, the walk to the objective", () => {
    for (const { t, l, label } of MAPS) {
      // (A small clinic's waiting room in front is a lobby two tiles deep.)
      for (const room of [...(l.homes ?? []).flatMap((h) => h.rooms), ...(l.medical ?? []).flatMap((m) => m.rooms ?? [])]) {
        expect(Math.min(room.w, room.h), `${label}: ${room.use} ${room.w} × ${room.h}`).toBeGreaterThanOrEqual(room.use === 'reception' ? 2 : 3);
      }
      const cover = l.tiles.filter((k) => k === 'low' || k === 'high').length / l.tiles.length;
      expect(cover, `${label}: cover`).toBeGreaterThanOrEqual(t.cover[0]);
      expect(cover, `${label}: cover`).toBeLessThanOrEqual(t.cover[1]);
      // On the ground, step by step (the movement rules), from the squad's first tile to the objective.
      const goal = l.item ?? l.terminal ?? l.objectiveSpot;
      if (!goal) continue;
      const s = { width: l.width, height: l.height, tiles: l.tiles, elev: l.elev } as unknown as GameState;
      const steps = new Map<number, number>([[l.spawns[0]!.y * l.width + l.spawns[0]!.x, 0]]);
      const queue: Vec2[] = [l.spawns[0]!];
      for (let k = 0; k < queue.length; k++) {
        const p = queue[k]!;
        const n = steps.get(p.y * l.width + p.x)!;
        for (const d of DIRS8) {
          const q = { x: p.x + d.x, y: p.y + d.y };
          const near = Math.max(Math.abs(q.x - goal.x), Math.abs(q.y - goal.y)) === 0;
          if ((!near && stepCost(s, p.x, p.y, d.x, d.y) === null) || steps.has(q.y * l.width + q.x)) continue;
          if (q.x < 0 || q.y < 0 || q.x >= l.width || q.y >= l.height) continue;
          steps.set(q.y * l.width + q.x, n + 1);
          queue.push(q);
        }
      }
      const walk = steps.get(goal.y * l.width + goal.x);
      expect(walk, `${label}: the objective out of reach`).toBeDefined();
      expect(walk!, `${label}: walk`).toBeGreaterThanOrEqual(20);
      expect(walk!, `${label}: walk`).toBeLessThanOrEqual(90);
    }
  });
});
