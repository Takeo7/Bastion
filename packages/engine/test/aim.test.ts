import { describe, expect, it } from 'vitest';
import { previewShot, rollDamage, rollShot } from '../src/aim';
import { WEAPONS } from '../src/content';
import { Rng } from '../src/rng';
import { addUnit, stateFrom } from './helpers';

const open = () => stateFrom(Array.from({ length: 12 }, () => '.'.repeat(12)));

describe('hit chance (XCOM 2 formula)', () => {
  it('aim + range modifier against a flanked target', () => {
    const s = open();
    const shooter = addUnit(s, 's1', 'specialist', 0, 0);
    const target = addUnit(s, 'a1', 'trooper', 0, 10);
    const p = previewShot(s, shooter, target)!;
    // 65 aim + medium range at 10 tiles (+4); no cover -> flanked -> +50 crit.
    expect(p.hit).toBe(69);
    expect(p.flanked).toBe(true);
    expect(p.crit).toBe(50);
  });

  it('high cover subtracts 40 and removes the flank crit', () => {
    const s = open();
    s.tiles[9 * 12 + 0] = 'high';
    const shooter = addUnit(s, 's1', 'specialist', 0, 0);
    const target = addUnit(s, 'a1', 'trooper', 0, 10);
    // The high cover blocks the straight line; the shooter sees the target by its step-out.
    const p = previewShot(s, shooter, target)!;
    expect(p.cover).toBe(2);
    expect(p.flanked).toBe(false);
    expect(p.crit).toBe(0);
    expect(p.hit).toBeLessThan(40);
  });

  it('reaction shots lose 30% and cannot crit', () => {
    const s = open();
    const shooter = addUnit(s, 's1', 'specialist', 0, 0);
    const target = addUnit(s, 'a1', 'trooper', 0, 10);
    const p = previewShot(s, shooter, target, { reaction: true })!;
    expect(p.hit).toBe(69 - Math.floor(69 * 0.3));
    expect(p.crit).toBe(0);
  });

  it('hunkering adds defense and dodge (grazes)', () => {
    const s = open();
    s.tiles[9 * 12 + 1] = 'low';
    const shooter = addUnit(s, 'a1', 'trooper', 1, 0);
    const target = addUnit(s, 's1', 'specialist', 1, 10, { hunkered: true });
    const p = previewShot(s, shooter, target)!;
    expect(p.mods.find((m) => m.label === 'Agazapado')?.value).toBe(-30);
    expect(p.graze).toBeGreaterThan(0);
  });
});

describe('rolls', () => {
  it('rolls are deterministic per seed and damage stays in range', () => {
    const a = new Rng(42);
    const b = new Rng(42);
    for (let i = 0; i < 200; i++) {
      const p = { origin: { x: 0, y: 0 }, hit: 60, crit: 20, graze: 10, flanked: true, cover: 0 as const, distance: 5, squadsight: false, mods: [], critMods: [] };
      const r = rollShot(p, a);
      expect(r).toBe(rollShot(p, b));
      const d = rollDamage(WEAPONS.rifle, r, a);
      rollDamage(WEAPONS.rifle, r, b);
      if (r === 'miss') expect(d).toBe(0);
      if (r === 'hit') expect(d).toBeGreaterThanOrEqual(3);
      if (r === 'hit') expect(d).toBeLessThanOrEqual(5);
      if (r === 'crit') expect(d).toBeGreaterThanOrEqual(5);
    }
  });
});
