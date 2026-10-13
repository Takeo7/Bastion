import { describe, expect, it } from 'vitest';
import { ARMOR_PART_IDS, defaultAppearance, isAppearance, normalizeAppearance, partColor } from '../src/appearance';
import { createCampaign, executeCampaignCommand, migrateCampaign, type CampaignCommand, type CampaignState } from '../src/campaign';
import { campaignCommandSchema } from '../src/protocol';
import { Rng } from '../src/rng';
import type { Appearance, Slot } from '../src/types';

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
    expect(isAppearance(defaultAppearance('sharpshooter', 1))).toBe(true);
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
    const look: Appearance = { ...old, helmet: '#ffffff', torso: '#1d2124', arms: '#3fa9ff', legs: '#6b7d5a' };
    run(c, { type: 'customize', soldier: s.id, name: s.name, nickname: '', appearance: look });
    expect(s.appearance).toEqual(look);

    // Reset to the default with the pieces sent along unchanged: what is stored compares equal to the default
    // look, so a promotion or a change of owner still restyles the soldier (the JSON checks in campaign.ts).
    const plain = defaultAppearance(s.cls, 0);
    const parts = Object.fromEntries(ARMOR_PART_IDS.map((part) => [part, partColor(plain, part)]));
    run(c, { type: 'customize', soldier: s.id, name: s.name, nickname: '', appearance: { ...plain, ...parts } });
    expect(JSON.stringify(s.appearance)).toBe(JSON.stringify(plain));

    expect(() => run(c, { type: 'customize', soldier: s.id, name: s.name, nickname: '', appearance: { ...plain, arms: 'blue' } })).toThrow(/aspecto/);
  });

  it('the protocol takes looks with or without pieces, and checks their colours', () => {
    const cmd = { type: 'customize', soldier: 's1', name: 'Ada', nickname: '' };
    expect(campaignCommandSchema.safeParse({ ...cmd, appearance: old }).success).toBe(true);
    expect(campaignCommandSchema.safeParse({ ...cmd, appearance: { ...old, helmet: '#ffffff', legs: '#000000' } }).success).toBe(true);
    expect(campaignCommandSchema.safeParse({ ...cmd, appearance: { ...old, arms: 'red' } }).success).toBe(false);
    expect(campaignCommandSchema.safeParse({ ...cmd, appearance: { ...old, torso: null } }).success).toBe(false);
  });

  it('saves keep their looks as they were, pieces or not', () => {
    const c = createCampaign([0], new Rng(4));
    const [a, b] = c.soldiers;
    a!.appearance = { ...old };
    b!.appearance = { ...old, arms: '#ffffff' };
    const m = migrateCampaign(JSON.parse(JSON.stringify(c)) as CampaignState);
    expect(m.soldiers[0]!.appearance).toEqual(old);
    expect(m.soldiers[1]!.appearance).toEqual({ ...old, arms: '#ffffff' });
  });
});

describe('the survivor model', () => {
  it('is an optional id the client resolves; the engine only checks its shape and keeps it', () => {
    const look: Appearance = { ...defaultAppearance(null, 0), character: 'hunter' };
    expect(isAppearance(look)).toBe(true);
    expect(isAppearance({ ...look, character: 'No vale!' })).toBe(false);
    expect(normalizeAppearance(look).character).toBe('hunter');
    expect(normalizeAppearance(defaultAppearance(null, 0))).not.toHaveProperty('character');
  });
});
