import { describe, expect, it } from 'vitest';
import { abilityBlocker, noiseOf } from '../src/abilities';
import { previewShot } from '../src/aim';
import { applyMissionResult, buildSquad, createCampaign, executeCampaignCommand, repairBlocker, squadMember, type CampaignCommand, type CampaignState } from '../src/campaign';
import { clipOf, FOUND_WEAPONS } from '../src/content';
import { applyEvent, type GameEvent } from '../src/events';
import { rollBox } from '../src/loot';
import { Rng } from '../src/rng';
import { executeCommand } from '../src/rules';
import { createMission } from '../src/setup';
import type { Arms, Slot } from '../src/types';
import { addUnit, stateFrom } from './helpers';

// Fase 9 §5 (docs/FASE9.md): weapons wear, mend with parts, and take improvements.

const field = () => stateFrom(Array.from({ length: 12 }, () => '.'.repeat(12)));
const armed = (primary: Arms['primary'], extra: Partial<Arms> = {}): Arms => ({ primary, melee: null, ...extra });
const shot = (unit: string, target: string, ability: 'shoot' | 'pistol' | 'slash'): GameEvent => ({ t: 'shot', unit, target, ability, from: { x: 1, y: 1, z: 0 }, result: 'miss', chance: 50, roll: 90, reaction: false });

describe('weapons wear', () => {
  it('every shot of the weapon in the hand wears it: two for the precision rifle; the pistol and the blade, nothing', () => {
    const s = field();
    const rifle = addUnit(s, 's1', 'rookie', 1, 1, { owner: 0, arms: armed('assaultRifle'), ammo: 4, pistolAmmo: 6 });
    const sniper = addUnit(s, 's2', 'rookie', 1, 2, { owner: 0, arms: armed('precisionRifle', { condition: 50 }), ammo: 4 });
    addUnit(s, 'z1', 'zombie', 8, 8);
    for (const e of [shot('s1', 'z1', 'shoot'), shot('s1', 'z1', 'pistol'), shot('s1', 'z1', 'slash'), shot('s2', 'z1', 'shoot')]) applyEvent(s, e);
    expect(rifle.arms!.condition).toBe(99);
    expect(sniper.arms!.condition).toBe(48);
  });

  it('below 30 it is «Gastada», −10 of aim; at 0, «Rota»: it neither fires nor keeps watch', () => {
    const s = field();
    const u = addUnit(s, 's1', 'rookie', 1, 1, { owner: 0, arms: armed('assaultRifle', { condition: 29 }), ammo: 4 });
    const z = addUnit(s, 'z1', 'zombie', 5, 1);
    expect(previewShot(s, u, z)!.mods).toContainEqual({ label: 'Gastada', value: -10 });
    u.arms!.condition = 0;
    expect(abilityBlocker(s, u, 'shoot')).toBe('Rota: no dispara. Se arregla en la Armería.');
    expect(abilityBlocker(s, u, 'overwatch')).toBe('Rota: no dispara. Se arregla en la Armería.');
    expect(abilityBlocker(s, u, 'slash')).toBeNull();
  });

  it("Apaños mends 40 of a partner's weapon alongside, once a mission", () => {
    const s = field();
    addUnit(s, 's1', 'rookie', 1, 1, { owner: 0, perks: ['patchUp'], charges: { patchUp: 1 } });
    const mate = addUnit(s, 's2', 'rookie', 2, 1, { owner: 0, arms: armed('assaultRifle', { condition: 20 }) });
    addUnit(s, 'z1', 'zombie', 11, 11);
    const ctx = { slot: 0 as Slot, connected: [0 as Slot], rng: new Rng(1) };
    const r = executeCommand(s, { type: 'ability', unit: 's1', ability: 'patchUp', target: 's2' }, ctx);
    if (!r.ok) throw new Error(r.error);
    expect(mate.arms!.condition).toBe(60);
    expect(executeCommand(s, { type: 'ability', unit: 's1', ability: 'patchUp', target: 's2' }, ctx).ok).toBe(false);
  });
});

describe('improvements', () => {
  it('a scope aims, a stock steadies on watch, a bigger magazine holds two more, a silencer halves the noise', () => {
    const s = field();
    const plain = addUnit(s, 's1', 'rookie', 1, 1, { owner: 0, arms: armed('assaultRifle'), ammo: 4 });
    const fitted = addUnit(s, 's2', 'rookie', 1, 3, { owner: 0, arms: armed('assaultRifle', { mods: ['scope', 'stock'] }), ammo: 4 });
    const quiet = addUnit(s, 's3', 'rookie', 1, 5, { owner: 0, arms: armed('assaultRifle', { mods: ['extMag', 'silencer'] }), ammo: 4 });
    const z = addUnit(s, 'z1', 'zombie', 6, 3);
    expect(previewShot(s, fitted, z)!.mods).toContainEqual({ label: 'Mira', value: 10 });
    expect(previewShot(s, fitted, z)!.mods).not.toContainEqual({ label: 'Culata', value: 10 });
    expect(previewShot(s, fitted, z, { reaction: true })!.mods).toContainEqual({ label: 'Culata', value: 10 });
    expect(clipOf(quiet, 'shoot')).toBe(clipOf(plain, 'shoot') + 2);
    expect(noiseOf(s, quiet, 'shoot')).toBe(Math.ceil(noiseOf(s, plain, 'shoot') / 2));
  });

  it('a good weapon found comes with its improvement, and any one somewhat worn', () => {
    for (let seed = 1; seed <= 40; seed++) {
      const loot = rollBox('lootWeapons', new Rng(seed), false)!;
      if (loot.mod) continue;
      expect(loot.condition).toBeGreaterThanOrEqual(40);
      expect(loot.condition).toBeLessThanOrEqual(90);
      const def = FOUND_WEAPONS[loot.weapon!];
      expect(loot.mods ?? []).toEqual(def.mod ? [def.mod] : []);
    }
  });
});

describe('the Armería', () => {
  const run = (c: CampaignState, cmd: CampaignCommand, slot: Slot = 0) => {
    const r = executeCampaignCommand(c, cmd, { slot, connected: [0], rng: new Rng(1) });
    if (!r.ok) throw new Error(r.error);
  };

  it('what the shots took comes home with the weapon; parts mend it, 25 each, as far as they go', () => {
    const c = createCampaign([0], new Rng(1));
    const s = c.soldiers[0]!;
    c.foundWeapons = [{ id: 'w1', kind: 'assaultRifle', holder: s.id }];
    c.activeMission = c.offers[0]!.id;
    c.squad = [s.id];
    const mission = createMission({ map: c.offers[0]!.map, slots: [0], rng: new Rng(2), kind: c.offers[0]!.kind, squad: buildSquad(c) });
    const unit = mission.units.find((u) => u.campaignId === s.id)!;
    expect(unit.arms).toMatchObject({ primary: 'assaultRifle', primaryId: 'w1' });
    unit.arms!.condition = 40;
    mission.outcome = 'victory';
    applyMissionResult(c, mission, new Rng(3));
    const w = c.foundWeapons.find((x) => x.id === 'w1')!;
    expect(w.condition).toBe(40);
    expect(squadMember(c, s).arms).toMatchObject({ condition: 40 });
    c.parts = 1;
    run(c, { type: 'repair', weapon: 'w1' });
    expect([w.condition, c.parts]).toEqual([65, 0]);
    expect(repairBlocker(c, 0, 'w1')).toBe('No hay piezas.');
    c.parts = 5;
    run(c, { type: 'repair', weapon: 'w1' });
    expect([w.condition, c.parts]).toEqual([undefined, 3]);
    expect(repairBlocker(c, 0, 'w1')).toBe('Está como nueva.');
  });

  it('improvements found wait in the Armería, two at most on a weapon, and come off again', () => {
    const c = createCampaign([0], new Rng(1));
    c.foundWeapons = [{ id: 'w1', kind: 'assaultRifle', holder: null }];
    c.weaponMods = { scope: 1, stock: 1, silencer: 1 };
    run(c, { type: 'mod', weapon: 'w1', mod: 'scope', on: true });
    run(c, { type: 'mod', weapon: 'w1', mod: 'stock', on: true });
    expect(() => run(c, { type: 'mod', weapon: 'w1', mod: 'silencer', on: true })).toThrow(/quita una antes/);
    run(c, { type: 'mod', weapon: 'w1', mod: 'scope', on: false });
    expect(() => run(c, { type: 'mod', weapon: 'w1', mod: 'extMag', on: true })).toThrow(/No queda ningún cargador ampliado/);
    expect(c.foundWeapons[0]!.mods).toEqual(['stock']);
    expect(c.weaponMods).toEqual({ scope: 1, stock: 0, silencer: 1 });
  });
});
