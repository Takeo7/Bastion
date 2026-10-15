import { describe, expect, it } from 'vitest';
import { decorOf } from '../src/decor';
import { cloneState } from '../src/events';
import { canStandOn, DIRS4, kindAt, nodeOf, posOf } from '../src/grid';
import { computeReach, stepBetween } from '../src/path';
import { Rng } from '../src/rng';
import { createMission } from '../src/setup';
import type { MedicalSite } from '../src/types';

const inside = (b: MedicalSite, x: number, y: number) => x >= b.x && y >= b.y && x < b.x + b.w && y < b.y + b.h;
const kinds = new Set(['hospitalBed', 'medicalShelf', 'gurney', 'medicalCabinet', 'medicalCart', 'wheelchair', 'receptionDesk', 'waitingSeats', 'examTable', 'medicalMonitor', 'xray']);

describe('medical locations', () => {
  it('scopes clinical furniture and wear to explicit locations, with varied plans', () => {
    const plans = new Set<string>();
    const furniture = new Set<string>();
    for (const medical of ['clinic', 'ward', 'hospital'] as const) for (let seed = 0; seed < 24; seed++) {
      const s = createMission({ map: { kind: 'generated', biome: 'city', seed: seed * 104729, medical }, slots: [0], rng: new Rng(seed), kind: medical === 'hospital' ? 'rescue' : 'recovery' });
      expect(s.medical).toHaveLength(1);
      const b = s.medical![0]!;
      expect(b.wear).toBe(medical === 'clinic' ? 0.08 : medical === 'ward' ? 0.45 : 0.85);
      for (const ds of [s.decor, ...(s.levels ?? []).map((l) => l.decor)]) for (let i = 0; i < (ds?.length ?? 0); i++) {
        const k = decorOf(ds![i])?.kind;
        if (!k || !kinds.has(k)) continue;
        furniture.add(k);
        expect(inside(b, i % s.width, Math.floor(i / s.width)), `${medical}/${seed}/${k}`).toBe(true);
      }
      plans.add(s.decor!.filter((d) => kinds.has(decorOf(d)?.kind ?? '')).join(','));
      expect(cloneState(s).medical).toEqual(s.medical);
    }
    expect(plans.size).toBeGreaterThan(12);
    expect([...kinds].every((k) => furniture.has(k))).toBe(true);
  }, 30_000);

  it('keeps every hospital floor, stair foot and landing reachable with furniture in place', () => {
    for (let seed = 0; seed < 64; seed++) {
      const s = createMission({ map: { kind: 'generated', biome: 'city', seed: seed * 7919 }, slots: [0, 1], rng: new Rng(seed), kind: 'rescue', campaignMission: { id: 'doctor', name: 'La doctora', story: 2 } });
      const b = s.medical![0]!;
      const label = `hospital/${seed}`;
      const scout = s.units.find((u) => u.team === 'xcom')!;
      const reach = computeReach({ ...s, units: [scout] }, scout, 100_000);
      const reached = (x: number, y: number, z: number) => reach.cost.has(nodeOf(s, { x, y, z }));
      for (let z = 0; z < 3; z++) {
        let floors = 0;
        let stairs = 0;
        const tiles = z ? s.levels![z - 1]!.tiles : s.tiles;
        const decor = z ? s.levels![z - 1]!.decor : s.decor;
        for (let i = 0; i < tiles.length; i++) {
          const p = { ...posOf(s, i), z };
          if (!inside(b, p.x, p.y)) continue;
          const t = tiles[i];
          if (t && canStandOn(t)) {
            floors++;
            expect(reached(p.x, p.y, z), `${label}/p${z}/${i}`).toBe(true);
          }
          const d = decorOf(decor?.[i]);
          if (t !== 'ladder' || d?.kind !== 'stairs') continue;
          stairs++;
          const dir = DIRS4[d.facing]!;
          const foot = { x: p.x - dir.x, y: p.y - dir.y, z };
          const landing = { x: p.x + dir.x, y: p.y + dir.y, z: z + 1 };
          expect(kindAt(s, foot), label).toBe('floor');
          expect(kindAt(s, landing), label).toBe('floor');
          expect(stepBetween(s, foot, p), label).not.toBeNull();
          expect(stepBetween(s, p, landing), label).not.toBeNull();
          expect(stepBetween(s, landing, p), label).not.toBeNull();
        }
        expect(floors, `${label}/p${z}`).toBeGreaterThan(0);
        if (z < 2) expect(stairs, `${label}/p${z}`).toBeGreaterThan(0);
      }
      const vip = Object.values(s.units).find((u) => u.captive)!;
      expect(reached(vip.pos.x, vip.pos.y, vip.pos.z), label).toBe(true);
    }
  }, 30_000);

  it('gives compact clinics separate reception, consultation and equipment rooms', () => {
    for (let seed = 0; seed < 80; seed++) {
      const s = createMission({ map: { kind: 'generated', biome: 'city', seed: seed * 104729, medical: 'clinic' }, slots: [0], rng: new Rng(seed), kind: 'recovery' });
      const b = s.medical![0]!;
      expect(Math.min(b.w, b.h)).toBeLessThanOrEqual(11);
      expect(Math.max(b.w, b.h)).toBeLessThanOrEqual(11);
      expect(b.rooms?.map((r) => r.use)).toEqual(['reception', 'consultation', 'equipment']);
      const scout = s.units.find((u) => u.team === 'xcom')!;
      const reach = computeReach({ ...s, units: [scout] }, scout, 100_000);
      for (const r of b.rooms!) {
        const furniture: string[] = [];
        let reached = 0, doors = 0;
        for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) {
          const i = y * s.width + x, d = decorOf(s.decor?.[i]);
          if (d) furniture.push(d.kind);
          if (kindAt(s, { x, y, z: 0 }) === 'floor') { expect(reach.cost.has(i), `clinic/${seed}/${r.use}`).toBe(true); reached++; }
          for (const v of DIRS4) if (kindAt(s, { x: x + v.x, y: y + v.y, z: 0 }) === 'door') doors++;
        }
        expect(reached).toBeGreaterThan(0);
        expect(doors).toBeGreaterThan(0);
        expect(furniture).toContain(r.use === 'reception' ? 'receptionDesk' : 'examTable');
        if (r.use === 'equipment') expect(furniture).toContain('xray');
      }
    }
  }, 30_000);

  it('restricts large hospitals to cities and gives their floors distinct functions', () => {
    for (const biome of ['city', 'wilds', 'facility'] as const) for (let seed = 0; seed < 16; seed++) {
      const s = createMission({ map: { kind: 'generated', biome, seed: seed * 7919, medical: 'hospital' }, slots: [0], rng: new Rng(seed), kind: 'rescue' });
      if (biome !== 'city') { expect(s.medical).toBeUndefined(); continue; }
      const b = s.medical![0]!;
      expect(Math.min(b.w, b.h)).toBeGreaterThanOrEqual(13);
      for (let z = 0; z < 3; z++) {
        const uses = b.rooms!.filter((r) => r.z === z).map((r) => r.use);
        expect(uses).toContain(z === 0 ? 'reception' : 'ward');
        expect(uses).toContain(z === 1 ? 'nurses' : 'equipment');
      }
    }
  }, 30_000);

  it('leaves ordinary maps without medical finishes or furniture', () => {
    for (const biome of ['city', 'wilds', 'facility'] as const) {
      const s = createMission({ map: { kind: 'generated', biome, seed: 7 }, slots: [0], rng: new Rng(1) });
      expect(s.medical).toBeUndefined();
      for (const ds of [s.decor, ...(s.levels ?? []).map((l) => l.decor)]) expect(ds?.some((d) => kinds.has(decorOf(d)?.kind ?? '')) ?? false).toBe(false);
    }
  });
});
