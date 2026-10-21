import { describe, expect, it } from 'vitest';
import { ARMOR_PART_IDS, defaultAppearance, isAppearance, normalizeAppearance, partColor } from '../src/appearance';
import { addPlayer, CAMPAIGN_VERSION, createCampaign, sexOfPerson, executeCampaignCommand, migrateCampaign, type CampaignCommand, type CampaignState } from '../src/campaign';
import { SOLDIER_NAMES, sexOfName } from '../src/content';
import { campaignCommandSchema } from '../src/protocol';
import { Rng } from '../src/rng';
import { createMission } from '../src/setup';
import type { Appearance, ClassId, Slot } from '../src/types';

function run(c: CampaignState, cmd: CampaignCommand, slot: Slot = 0) {
  const r = executeCampaignCommand(c, cmd, { slot, connected: [0], rng: new Rng(11) });
  if (!r.ok) throw new Error(r.error);
  return r;
}

/** A look from before armour pieces could be painted. */
const old: Appearance = { primary: '#8f2f2f', secondary: '#c9a13a', visor: '#7dffb0', head: 'helmet', build: 'slim' };

describe('armour pieces', () => {
  it('an old look is still valid and every piece wears what it wore before', () => {
    expect(isAppearance(old)).toBe(true);
    expect(isAppearance(defaultAppearance(1))).toBe(true);
    for (const part of ['helmet', 'torso', 'arms', 'legs'] as const) expect(partColor(old, part)).toBe(old.primary);
    // Berets, caps and hoods were painted in the trim; masks and bare heads have nothing to paint.
    for (const head of ['hood', 'beret', 'cap'] as const) expect(partColor({ ...old, head }, 'helmet')).toBe(old.secondary);
    expect(partColor({ ...old, head: 'hood' }, 'torso')).toBe(old.primary);
  });

  it('pieces take their own colour, and a bad one makes the look invalid', () => {
    const look: Appearance = { ...old, helmet: '#ffffff', arms: '#2F6B4F' };
    expect(isAppearance(look)).toBe(true);
    expect(partColor(look, 'helmet')).toBe('#ffffff');
    expect(partColor(look, 'arms')).toBe('#2F6B4F');
    expect(partColor(look, 'legs')).toBe(old.primary);
    expect(partColor({ ...look, head: 'beret' }, 'helmet')).toBe('#ffffff');
    expect(isAppearance({ ...old, legs: 'red' })).toBe(false);
    expect(isAppearance({ ...old, torso: '#12345' })).toBe(false);
    expect(isAppearance({ ...old, helmet: null as unknown as string })).toBe(false);
    expect(isAppearance({ ...old, head: 'toString' as Appearance['head'] })).toBe(false);
  });

  it('a stored look keeps only the pieces that differ from what they would follow', () => {
    const look = { ...old, helmet: '#8F2F2F', torso: '#ffffff', legs: undefined, extra: 'x' } as Appearance;
    expect(normalizeAppearance(look)).toEqual({ ...old, torso: '#ffffff' });
    expect(Object.keys(normalizeAppearance(look))).toEqual(['primary', 'secondary', 'visor', 'head', 'build', 'torso']);
    // A hood follows the trim, so a hood in the armour colour is a choice worth keeping.
    expect(normalizeAppearance({ ...old, head: 'hood', helmet: old.primary })).toEqual({ ...old, head: 'hood', helmet: old.primary });
    expect(normalizeAppearance({ ...old, head: 'hood', helmet: old.secondary })).toEqual({ ...old, head: 'hood' });
  });

  it('the barracks stores the pieces, and an untouched look is still the default one', () => {
    const c = createCampaign([0], new Rng(4));
    const s = c.soldiers.find((x) => x.owner === 0)!;
    const sex = s.appearance.sex;
    const look: Appearance = { ...old, helmet: '#ffffff', torso: '#1d2124', arms: '#3fa9ff', legs: '#6b7d5a' };
    run(c, { type: 'customize', soldier: s.id, name: s.name, nickname: '', appearance: look });
    expect(s.appearance).toEqual({ ...look, sex });

    // Reset to the default with the pieces sent along unchanged (and no sex, like the barracks' button): what is
    // stored compares equal to the default look, so a promotion or a change of owner still restyles the soldier
    // (the JSON checks in campaign.ts).
    const plain = defaultAppearance(0);
    const parts = Object.fromEntries(ARMOR_PART_IDS.map((part) => [part, partColor(plain, part)]));
    run(c, { type: 'customize', soldier: s.id, name: s.name, nickname: '', appearance: { ...plain, ...parts } });
    expect(JSON.stringify(s.appearance)).toBe(JSON.stringify(defaultAppearance(0, undefined, sex)));

    expect(() => run(c, { type: 'customize', soldier: s.id, name: s.name, nickname: '', appearance: { ...plain, arms: 'blue' } })).toThrow(/aspecto/);
  });

  it('the protocol takes looks with or without pieces, and checks their colours', () => {
    const cmd = { type: 'customize', soldier: 's1', name: 'Ada', nickname: '' };
    expect(campaignCommandSchema.safeParse({ ...cmd, appearance: old }).success).toBe(true);
    expect(campaignCommandSchema.safeParse({ ...cmd, appearance: { ...old, helmet: '#ffffff', legs: '#000000' } }).success).toBe(true);
    expect(campaignCommandSchema.safeParse({ ...cmd, appearance: { ...old, arms: 'red' } }).success).toBe(false);
    expect(campaignCommandSchema.safeParse({ ...cmd, appearance: { ...old, torso: null } }).success).toBe(false);
  });

});

describe('the survivor model', () => {
  it('is an optional id the client resolves; the engine only checks its shape and keeps it', () => {
    const look: Appearance = { ...defaultAppearance(0), character: 'hunter' };
    expect(isAppearance(look)).toBe(true);
    expect(isAppearance({ ...look, character: 'No vale!' })).toBe(false);
    expect(normalizeAppearance(look).character).toBe('hunter');
    expect(normalizeAppearance(defaultAppearance(0))).not.toHaveProperty('character');
  });
});

describe("the soldier's sex (playtest 2026-10-13)", () => {
  const sexOf = (name: string) => SOLDIER_NAMES.find((n) => n.name === name)?.sex;

  it('every name has one, and soldiers and recruits get the sex of their name', () => {
    expect(SOLDIER_NAMES.every((n) => n.sex === 'f' || n.sex === 'm')).toBe(true);
    expect(SOLDIER_NAMES.filter((n) => n.sex === 'f')).toHaveLength(SOLDIER_NAMES.length / 2);
    expect(new Set(SOLDIER_NAMES.map((n) => n.name)).size).toBe(SOLDIER_NAMES.length);
    expect(sexOf('Chioma Okafor')).toBe('f');
    expect(sexOf('Kofi Mensah')).toBe('m');
    const c = createCampaign([0, 1], new Rng(4));
    for (const s of c.soldiers) expect(s.appearance.sex).toBe(sexOf(s.name));
    const solo = createCampaign([0], new Rng(4));
    for (const s of solo.soldiers.slice(2)) s.alive = false;
    run(solo, { type: 'recruit' });
    const recruit = solo.soldiers.at(-1)!;
    expect(recruit.appearance).toEqual(defaultAppearance(0, undefined, sexOf(recruit.name), recruit.id));
    // Written right after the build, like normalizeAppearance stores it, so default looks still compare equal.
    expect(Object.keys(recruit.appearance)).toEqual(['primary', 'secondary', 'visor', 'head', 'build', 'sex']);
    expect(Object.keys(normalizeAppearance({ ...recruit.appearance, character: 'hunter', legs: '#ffffff' }))).toEqual([
      'primary', 'secondary', 'visor', 'head', 'build', 'sex', 'legs', 'character',
    ]);
  });

  it('people who join by name take theirs: the story\'s, a relative\'s, a soldier\'s', () => {
    expect(sexOfPerson('Vera Lindqvist')).toBe('f');
    expect(sexOfPerson('Dr. Femi Adebayo')).toBe('m');
    expect(sexOfPerson('Agente Claire Dubois')).toBe('f');
    expect(sexOfPerson('Kenji Okafor')).toBe('m');
    expect(sexOfPerson('Mariam Rey')).toBe('f');
    expect(sexOfPerson('Hiro Sato')).toBe('m');
    expect(sexOfPerson('Nadie Conocido')).toBeUndefined();
  });

  it('is part of a valid look, and the protocol takes it', () => {
    expect(isAppearance({ ...old, sex: 'f' })).toBe(true);
    expect(isAppearance({ ...old, sex: 'm' })).toBe(true);
    expect(isAppearance({ ...old, sex: 'x' as 'f' })).toBe(false);
    expect(defaultAppearance(0)).not.toHaveProperty('sex');
    const cmd = { type: 'customize', soldier: 's1', name: 'Ada', nickname: '' };
    expect(campaignCommandSchema.safeParse({ ...cmd, appearance: { ...old, sex: 'm' } }).success).toBe(true);
    expect(campaignCommandSchema.safeParse({ ...cmd, appearance: { ...old, sex: 'x' } }).success).toBe(false);
  });

  it('customizing keeps it unless the look sends another, and a change of owner keeps it too', () => {
    const c = createCampaign([0], new Rng(4));
    const s = c.soldiers.find((x) => x.owner === 0)!;
    const sex = s.appearance.sex!;
    run(c, { type: 'customize', soldier: s.id, name: 'Otro Nombre', nickname: '', appearance: old });
    expect(s.appearance.sex).toBe(sex);
    const other = sex === 'f' ? 'm' : 'f';
    run(c, { type: 'customize', soldier: s.id, name: s.name, nickname: '', appearance: { ...old, sex: other } });
    expect(s.appearance.sex).toBe(other);

    // Untouched rookies who pass to a partner arriving late take their colours, and keep their sex.
    const d = createCampaign([0], new Rng(3));
    addPlayer(d, 1, new Rng(1));
    const moved = d.soldiers.filter((x) => x.owner === 1);
    expect(moved.length).toBeGreaterThan(0);
    for (const m of moved) expect(m.appearance).toEqual(defaultAppearance(1, undefined, sexOfName(m.name), m.id));
  });


  it('skirmish squads carry it too', () => {
    const mission = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0, 1], rng: new Rng(1) });
    const squad = mission.units.filter((u) => u.team === 'xcom');
    expect(squad.length).toBeGreaterThan(0);
    for (const u of squad) {
      expect(u.appearance?.sex).toBe(sexOf(u.name));
      expect(u.appearance).toEqual(defaultAppearance(u.owner!, undefined, sexOf(u.name), u.name));
    }
  });

  it('every soldier starts with a head and a build of its own, and a campaign of before gets them for the untouched ones (docs/SIDEKICK.md)', () => {
    const c = createCampaign([0, 1], new Rng(5));
    expect(new Set(c.soldiers.map((s) => s.appearance.head)).size).toBeGreaterThan(1);
    expect(new Set(c.soldiers.map((s) => s.appearance.build)).size).toBeGreaterThan(1);
    // A campaign of 0.8: everyone in the helmet and the normal build, but one customized.
    const old = JSON.parse(JSON.stringify(c)) as CampaignState;
    old.version = 23 as CampaignState['version'];
    for (const s of old.soldiers) s.appearance = defaultAppearance(s.owner, undefined, s.appearance.sex);
    const custom = old.soldiers[0]!;
    custom.appearance = { ...custom.appearance, head: 'helmet', build: 'heavy' };
    const m = migrateCampaign(old);
    expect(m.soldiers[0]!.appearance).toEqual(custom.appearance);
    for (const s of m.soldiers.slice(1)) expect(s.appearance).toEqual(defaultAppearance(s.owner, undefined, s.appearance.sex, s.id));
  });
});
