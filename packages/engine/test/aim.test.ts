import { describe, expect, it } from 'vitest';
import { damageRange, previewShot, resolveShot, rollDamage, rollShot, shotDamage } from '../src/aim';
import { WEAPONS } from '../src/content';
import { applyEvent, type GameEvent } from '../src/events';
import { Rng } from '../src/rng';
import { abilityDamage } from '../src/rules';
import type { GameState, ShotResult, Unit } from '../src/types';
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
    expect(p.mods.find((m) => m.label === 'Cubriéndose')?.value).toBe(-30);
    expect(p.graze).toBeGreaterThan(0);
  });
});

describe('rolls', () => {
  it('rolls are deterministic per seed and damage stays in range', () => {
    const a = new Rng(42);
    const b = new Rng(42);
    for (let i = 0; i < 200; i++) {
      const p = { origin: { x: 0, y: 0, z: 0 }, hit: 60, crit: 20, graze: 10, flanked: true, cover: 0 as const, distance: 5, squadsight: false, mods: [], critMods: [] };
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

describe('the die on screen', () => {
  it('the roll runs 1–100 and hits exactly when it is at most the chance', () => {
    const rng = new Rng(7);
    const p = { origin: { x: 0, y: 0, z: 0 }, hit: 93, crit: 10, graze: 0, flanked: false, cover: 0 as const, distance: 5, squadsight: false, mods: [], critMods: [] };
    for (let i = 0; i < 300; i++) {
      const { result, roll } = resolveShot(p, rng);
      expect(roll).toBeGreaterThanOrEqual(1);
      expect(roll).toBeLessThanOrEqual(100);
      expect(result === 'miss').toBe(roll > 93);
      if (result === 'crit') expect(roll).toBeLessThanOrEqual(10);
    }
  });

  it('gives the same result as rollShot for the same seed', () => {
    const p = { origin: { x: 0, y: 0, z: 0 }, hit: 55, crit: 15, graze: 10, flanked: false, cover: 0 as const, distance: 5, squadsight: false, mods: [], critMods: [] };
    const a = new Rng(3);
    const b = new Rng(3);
    for (let i = 0; i < 100; i++) expect(resolveShot(p, a).result).toBe(rollShot(p, b));
  });
});

describe('bad luck (aim assist)', () => {
  const shot = (s: GameState, unit: Unit, target: Unit, result: ShotResult): void => {
    const e: GameEvent = { t: 'shot', unit: unit.id, target: target.id, ability: 'shoot', from: unit.pos, result, chance: 70, roll: result === 'miss' ? 90 : 10, reaction: false };
    applyEvent(s, e);
  };
  const luck = (s: GameState, shooter: Unit, target: Unit) => previewShot(s, shooter, target)!.mods.find((m) => m.label === 'Mala racha')?.value ?? 0;

  it('each miss in a row gives that player +10, up to +20, and a hit clears it', () => {
    const s = open();
    const mine = addUnit(s, 's1', 'specialist', 0, 0, { owner: 0 });
    const partner = addUnit(s, 's2', 'specialist', 2, 0, { owner: 1 });
    const alien = addUnit(s, 'a1', 'trooper', 0, 10);
    const base = previewShot(s, mine, alien)!.hit;
    shot(s, mine, alien, 'miss');
    expect(luck(s, mine, alien)).toBe(10);
    expect(previewShot(s, mine, alien)!.hit).toBe(Math.min(100, base + 10));
    // Per player: the partner's odds don't move.
    expect(luck(s, partner, alien)).toBe(0);
    shot(s, mine, alien, 'miss');
    shot(s, mine, alien, 'miss');
    expect(luck(s, mine, alien)).toBe(20);
    shot(s, mine, alien, 'graze');
    expect(luck(s, mine, alien)).toBe(0);
  });

  it('counts any of that player\'s soldiers, never the aliens', () => {
    const s = open();
    const a = addUnit(s, 's1', 'specialist', 0, 0, { owner: 0 });
    const b = addUnit(s, 's2', 'sharpshooter', 2, 0, { owner: 0 });
    const alien = addUnit(s, 'a1', 'trooper', 0, 10);
    shot(s, a, alien, 'miss');
    expect(luck(s, b, alien)).toBe(10);
    shot(s, alien, a, 'miss');
    shot(s, alien, a, 'miss');
    expect(luck(s, alien, a)).toBe(0);
    expect(luck(s, b, alien)).toBe(10);
  });
});

describe('damage on screen', () => {
  it('the range shown is exactly what the rolls give', () => {
    for (const weapon of [WEAPONS.rifle, WEAPONS.pistol, WEAPONS.fragGrenade, WEAPONS.sniper]) {
      for (const result of ['hit', 'crit', 'graze'] as const) {
        for (const [bonus, mult, critBonus] of [[0, 1, 0], [2, 1.5, 1]] as const) {
          const range = damageRange(weapon, result, bonus, mult, critBonus);
          const rng = new Rng(7);
          const rolls = Array.from({ length: 400 }, () => rollDamage(weapon, result, rng, bonus, mult, critBonus));
          expect([Math.min(...rolls), Math.max(...rolls)]).toEqual([range.min, range.max]);
        }
      }
    }
  });

  it('against a target: its armour soaks, Ruptura adds, the shred is named', () => {
    const s = open();
    const shooter = addUnit(s, 's1', 'specialist', 0, 0);
    const target = addUnit(s, 'a1', 'mec', 0, 10, { ruptured: 3 });
    const d = shotDamage(shooter, 'shoot', target, previewShot(s, shooter, target)!);
    // Rifle 3–5 (+2 on a crit), +3 from Ruptura, −1 for the Gólem's armour.
    expect(d.hit).toEqual({ min: 5, max: 7 });
    expect(d.crit).toEqual({ min: 7, max: 9 });
    expect(d.armor).toBe(1);
    // Without a target, the weapon's own numbers.
    expect(shotDamage(shooter, 'shoot').hit).toEqual({ min: 3, max: 5 });
  });

  it('grenades, songs and the Grillo use the same numbers as the rules', () => {
    const s = open();
    const grenadier = addUnit(s, 's1', 'grenadier', 0, 0);
    grenadier.mods = { ...grenadier.mods, grenadeDamage: 2, songDamage: 1 };
    const mec = addUnit(s, 'a1', 'mec', 0, 10);
    // Frag 3 (+1 one time in five) +2, shreds 1; the Gólem's armour takes 1.
    expect(abilityDamage(s, grenadier, 'grenade')).toMatchObject({ hit: { min: 5, max: 6 }, shred: 1, crit: null });
    expect(abilityDamage(s, grenadier, 'grenade', mec)!.hit).toEqual({ min: 4, max: 5 });
    expect(abilityDamage(s, grenadier, 'bolt', mec)!.hit).toEqual({ min: 3, max: 3 });
    expect(abilityDamage(s, grenadier, 'combatProtocol', mec)).toMatchObject({ hit: { min: 4, max: 4 }, ignoresArmor: true });
    expect(abilityDamage(s, grenadier, 'overwatch')).toBeNull();
  });
});
