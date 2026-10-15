import { describe, expect, it } from 'vitest';
import { decorOf } from '../src/decor';
import { cloneState } from '../src/events';
import { DIRS4 } from '../src/grid';
import { Rng } from '../src/rng';
import { createMission } from '../src/setup';

const corpses = new Set(['corpseCivilian', 'corpseStaff', 'coveredCorpse', 'coveredBed']);
const mission = (medical: 'clinic' | 'ward' | 'hospital', seed: number) => createMission({
  map: { kind: 'generated', biome: 'city', seed, medical }, slots: [0], rng: new Rng(seed), kind: medical === 'hospital' ? 'rescue' : 'recovery',
});

describe('medical atmosphere', () => {
  it('gives every centre seeded bodies, including centres with five and ten, without adding units or cover', () => {
    const counts = new Set<number>(), kinds = new Set<string>();
    for (const medical of ['clinic', 'ward', 'hospital'] as const) for (let seed = 0; seed < 48; seed++) {
      const s = mission(medical, seed * 7919), b = s.medical![0]!, details = b.dressing!;
      const count = details.filter((d) => corpses.has(d.kind)).length;
      expect(count, `${medical}/${seed}`).toBeGreaterThanOrEqual(1);
      expect(count).toBeLessThanOrEqual(10); counts.add(count);
      const occupied = new Set<string>();
      for (const d of details) {
        kinds.add(d.kind);
        expect(d.x).toBeGreaterThan(b.x); expect(d.x).toBeLessThan(b.x + b.w - 1);
        expect(d.y).toBeGreaterThan(b.y); expect(d.y).toBeLessThan(b.y + b.h - 1);
        const st = d.z ? s.levels![d.z - 1]! : s, i = d.y * s.width + d.x;
        if (corpses.has(d.kind)) {
          const key = `${d.x},${d.y},${d.z}`; expect(occupied.has(key)).toBe(false); occupied.add(key);
          if (d.kind === 'coveredBed') expect(decorOf(st.decor?.[i])?.kind).toBe('hospitalBed');
          else expect(st.tiles[i]).toBe('floor');
        }
        if (d.kind === 'privacyScreen' || d.kind === 'bareBed') expect(['hospitalBed', 'examTable', 'gurney']).toContain(decorOf(st.decor?.[i])?.kind);
        if (d.kind === 'wallCross' || d.kind === 'cautionSign' || d.kind === 'safeSign') {
          const f = DIRS4[d.facing]!;
          expect(st.tiles[(d.y - f.y) * s.width + d.x - f.x]).toBe('wall');
        }
      }
      expect(cloneState(s).medical).toEqual(s.medical);
      expect(s.units.every((u) => !u.id.startsWith('corpse'))).toBe(true);
    }
    expect(counts.has(1) && counts.has(5) && counts.has(10)).toBe(true);
    for (const kind of ['bareBed', 'coveredBed', 'privacyScreen', 'medicalSupplies', 'luggage', 'books', 'crutches', 'wallCross', 'cautionSign', 'safeSign']) expect(kinds.has(kind), kind).toBe(true);
  }, 30_000);

  it('keeps dressing stable when replayed or saved and keeps it out of ordinary maps', () => {
    for (const medical of ['clinic', 'ward', 'hospital'] as const) {
      const s = mission(medical, 209458);
      expect(mission(medical, 209458).medical).toEqual(s.medical);
      expect(JSON.parse(JSON.stringify(s)).medical).toEqual(s.medical);
      const old = cloneState(s); delete old.medical![0]!.dressing;
      expect(cloneState(old).medical![0]!.dressing).toBeUndefined();
    }
    const plain = createMission({ map: { kind: 'generated', biome: 'city', seed: 13 }, slots: [0], rng: new Rng(13), kind: 'elimination' });
    expect(plain.medical).toBeUndefined();
  });
});
