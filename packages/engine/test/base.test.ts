import { describe, expect, it } from 'vitest';
import {
  applyMissionResult,
  buildSquad,
  contactCapacity,
  createCampaign,
  EVENTS,
  executeCampaignCommand,
  itemsFree,
  CAMPAIGN_VERSION,
  migrateCampaign,
  shopPrice,
  squadLimit,
  squadMember,
  type CampaignCommand,
  type CampaignState,
} from '../src/campaign';
import { abilitiesOf } from '../src/abilities';
import { defaultAppearance } from '../src/appearance';
import { bastionLevel, LEVEL_XP, missionXp } from '../src/progression';
import { Rng } from '../src/rng';
import { createMission } from '../src/setup';
import type { Appearance, Slot } from '../src/types';

const both: Slot[] = [0, 1];

function run(c: CampaignState, cmd: CampaignCommand, slot: Slot = 0, connected: Slot[] = [0]) {
  const r = executeCampaignCommand(c, cmd, { slot, connected, rng: new Rng(11) });
  if (!r.ok) throw new Error(r.error);
  return r;
}

function solo(): CampaignState {
  const c = createCampaign([0], new Rng(3));
  c.nextEventDay = 999;
  c.credits = 2000;
  return c;
}

/** Advances until `done` holds (answering no events), with a guard. */
function advanceUntil(c: CampaignState, done: () => boolean) {
  for (let i = 0; i < 30 && !done(); i++) run(c, { type: 'advance', ready: true });
}

describe('facilities', () => {
  it('rooms below the surface must be excavated before building; one project at a time', () => {
    const c = solo();
    expect(() => run(c, { type: 'build', slot: 'r1c0', facility: 'infirmary' })).toThrow(/excavar/);
    expect(() => run(c, { type: 'excavate', slot: 'r2c0' })).toThrow(/encima/);
    run(c, { type: 'excavate', slot: 'r1c0' });
    expect(() => run(c, { type: 'build', slot: 'r0c0', facility: 'infirmary' })).toThrow(/obra/);
    advanceUntil(c, () => !c.base.construction);
    expect(c.base.slots.find((s) => s.id === 'r1c0')!.excavated).toBe(true);
  });

  it('the workshop makes accessories cheaper; facilities unlock with the Bastion level', () => {
    const c = solo();
    run(c, { type: 'build', slot: 'r0c0', facility: 'workshop' });
    advanceUntil(c, () => !c.base.construction);
    expect(c.base.slots[0]!.facility).toBe('workshop');
    expect(shopPrice(c, 'smokeGrenade')).toBe(19);
    expect(() => run(c, { type: 'build', slot: 'r0c2', facility: 'workshop' })).toThrow(/Solo/);
    expect(() => run(c, { type: 'build', slot: 'r0c1', facility: 'simulation' })).toThrow(/nivel 3/);
  });

  it('the simulation room makes missions worth more experience', () => {
    const c = solo();
    c.xp = LEVEL_XP[2]!;
    run(c, { type: 'build', slot: 'r0c1', facility: 'simulation' });
    advanceUntil(c, () => !c.base.construction);
    const offer = c.offers[0]!;
    run(c, { type: 'chooseMission', id: offer.id });
    const mission = createMission({ map: offer.map, slots: [0], rng: new Rng(2), kind: offer.kind, pods: offer.pods });
    mission.outcome = 'victory';
    const report = applyMissionResult(c, mission, new Rng(1));
    expect(report.xp).toBe(Math.round(missionXp(true, offer.difficulty, 0) * 1.25));
  });

  it('the training centre lets each player bring one more soldier', () => {
    const c = createCampaign(both, new Rng(1));
    expect(squadLimit(c, both)).toBe(3);
    c.base.slots[0]!.facility = 'training';
    expect(squadLimit(c, both)).toBe(4);
  });
});

describe('the world', () => {
  it('contacting regions costs credits, grows the monthly payment and is capped by communications', () => {
    const c = solo();
    expect(contactCapacity(c)).toBe(2);
    run(c, { type: 'contact', region: 'na' });
    expect(() => run(c, { type: 'contact', region: 'afr' })).toThrow(/comunicaciones/);
    const credits = c.credits;
    c.nextSupplyDay = c.day + 1;
    run(c, { type: 'advance', ready: true });
    expect(c.credits).toBe(credits + 60 + 30 + 35);
  });

  it('alien facilities push the project until a squad destroys them', () => {
    const c = solo();
    c.nextFacilityDay = c.day + 1;
    run(c, { type: 'advance', ready: true });
    const region = c.regions.find((r) => r.facility)!;
    expect(region).toBeDefined();
    if (!region.contacted) {
      expect(c.offers.some((o) => o.facility)).toBe(false);
      c.credits = 2000;
      if (c.regions.filter((r) => r.contacted).length >= contactCapacity(c)) c.base.slots[0]!.facility = 'relay';
      run(c, { type: 'contact', region: region.id });
    }
    const assault = c.offers.find((o) => o.facility === region.id)!;
    expect(assault.kind).toBe('sabotage');
    expect(assault.expiresDay).toBeNull();
    run(c, { type: 'chooseMission', id: assault.id });
    const doom = c.doom;
    const mission = createMission({ map: assault.map, slots: [0], rng: new Rng(2), kind: assault.kind, pods: assault.pods });
    mission.outcome = 'victory';
    applyMissionResult(c, mission, new Rng(1));
    expect(region.facility).toBeNull();
    expect(c.doom).toBe(doom - 2);
  });

  it('events stop time until both answer, and the answer does what it says', () => {
    const c = createCampaign(both, new Rng(5));
    c.nextEventDay = 2;
    run(c, { type: 'advance', ready: true }, 0, both);
    run(c, { type: 'advance', ready: true }, 1, both);
    expect(c.event).not.toBeNull();
    expect(() => run(c, { type: 'advance', ready: true }, 0, both)).toThrow(/acontecimiento/);
    // Nor can the squad take off with the event unanswered.
    const { activeMission, squad } = c;
    c.activeMission = 'pending';
    c.squad = [c.soldiers[0]!.id];
    expect(() => run(c, { type: 'launch', ready: true }, 0, both)).toThrow(/acontecimiento/);
    Object.assign(c, { activeMission, squad });
    c.event = 'defector';
    const xp = c.xp;
    run(c, { type: 'answerEvent', option: 0 }, 0, both);
    expect(c.proposal?.text).toMatch(/Interrogarlo/);
    run(c, { type: 'vote', accept: true }, 1, both);
    expect(c.xp).toBe(xp + 12);
    expect(c.event).toBeNull();
    expect(Object.keys(EVENTS).length).toBeGreaterThanOrEqual(8);
  });
});

describe('alien bodies', () => {
  it('are sold after a won mission, and left behind after a lost one', () => {
    const c = solo();
    const offer = c.offers[0]!;
    run(c, { type: 'chooseMission', id: offer.id });
    const mission = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(2), pods: [['sectoid', 'trooper']] });
    for (const u of mission.units) if (u.team === 'alien') u.alive = false;
    mission.outcome = 'victory';
    const credits = c.credits;
    const report = applyMissionResult(c, mission, new Rng(1));
    expect(report).toMatchObject({ bodies: 2, salvage: 10 + 4 });
    expect(c.credits).toBe(credits + (offer.reward.credits ?? 0) + 14);

    run(c, { type: 'chooseMission', id: c.offers[0]!.id });
    mission.outcome = 'defeat';
    expect(applyMissionResult(c, mission, new Rng(1))).toMatchObject({ bodies: 0, salvage: 0 });
  });
});

describe('equipment', () => {
  it('accessories are bought into a shared stock and each player equips their own soldiers', () => {
    const c = createCampaign(both, new Rng(1));
    c.credits = 500;
    const mine = c.soldiers.find((s) => s.owner === 0)!;
    const theirs = c.soldiers.find((s) => s.owner === 1)!;
    run(c, { type: 'buy', item: 'smokeGrenade' }, 0, both);
    run(c, { type: 'equip', soldier: mine.id, slot: 'utility', item: 'smokeGrenade' }, 0, both);
    expect(itemsFree(c, 'smokeGrenade')).toBe(0);
    expect(() => run(c, { type: 'equip', soldier: theirs.id, slot: 'utility', item: 'smokeGrenade' }, 1, both)).toThrow(/libres/);
    expect(() => run(c, { type: 'equip', soldier: theirs.id, slot: 'utility', item: 'firstAid' }, 0, both)).toThrow(/tus soldados/);
    expect(() => run(c, { type: 'buy', item: 'mindShield' }, 0, both)).toThrow(/nivel 6/);
    expect(() => run(c, { type: 'equip', soldier: mine.id, slot: 'ammo', item: 'smokeGrenade' }, 0, both)).toThrow(/ranura/);
  });

  it('equipment reaches the battlefield and is lost with its bearer', () => {
    const c = solo();
    const s = c.soldiers.find((x) => x.cls === 'assault')!;
    c.xp = LEVEL_XP[3]!;
    run(c, { type: 'buy', item: 'apRounds' });
    run(c, { type: 'buy', item: 'smokeGrenade' });
    run(c, { type: 'equip', soldier: s.id, slot: 'ammo', item: 'apRounds' });
    run(c, { type: 'equip', soldier: s.id, slot: 'utility', item: 'smokeGrenade' });
    const member = squadMember(c, s);
    expect(member.mods.pierce).toBe(1);
    expect(member.charges.smoke).toBe(1);
    run(c, { type: 'chooseMission', id: c.offers[0]!.id });
    run(c, { type: 'squad', soldier: s.id, on: true });
    const mission = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(1), squad: buildSquad(c) });
    const unit = mission.units.find((u) => u.campaignId === s.id)!;
    expect(abilitiesOf(unit)).toContain('smoke');
    unit.alive = false;
    unit.hp = 0;
    mission.outcome = 'defeat';
    applyMissionResult(c, mission, new Rng(1));
    expect(c.inventory.apRounds ?? 0).toBe(0);
    expect(c.inventory.smokeGrenade ?? 0).toBe(0);
  });
});

describe('migration', () => {
  it('an old campaign gets a base, regions, loadouts and its perks re-tiered', () => {
    const c = createCampaign([0], new Rng(1)) as unknown as Record<string, unknown>;
    for (const key of ['base', 'regions', 'inventory', 'credits', 'xp', 'nextFacilityDay', 'nextSupplyDay', 'nextEventDay', 'event']) delete c[key];
    // Version 3 and older: three resources, research and engineering upgrades.
    Object.assign(c, { resources: { supplies: 100, intel: 20, alloys: 10 }, research: { done: ['alienEncryption'] }, upgrades: ['magWeapons'], corpses: ['sectoid'] });
    const soldiers = c.soldiers as { rank: number; perks: string[]; loadout?: unknown }[];
    soldiers[0]!.rank = 2;
    soldiers[0]!.perks = ['pointBlank', 'elusive'];
    delete soldiers[0]!.loadout;
    delete (soldiers[0] as Record<string, unknown>).appearance;
    delete (soldiers[0] as Record<string, unknown>).nickname;
    const m = migrateCampaign(c as unknown as CampaignState);
    expect(m.base.slots).toHaveLength(9);
    expect(m.regions.find((r) => r.id === 'weu')!.contacted).toBe(true);
    expect(m.soldiers[0]!.perks).toEqual(['pointBlank']);
    expect(m.soldiers[0]!.pendingTiers).toEqual([2]);
    expect(m.soldiers[0]!.loadout).toEqual({ utility: null, ammo: null });
    // Soldiers from before customization keep their old look.
    expect(m.soldiers[0]!.appearance).toEqual(defaultAppearance(m.soldiers[0]!.cls, 0));
    expect(m.soldiers[0]!.nickname).toBe('');
    expect(m.version).toBe(CAMPAIGN_VERSION);
    // Resources become credits, and the Bastion keeps what was already researched.
    expect(m.credits).toBe(100 + 2 * 20 + 3 * 10);
    expect(bastionLevel(m)).toBe(5);
    expect(m).not.toHaveProperty('research');
    expect(m).not.toHaveProperty('resources');
  });
});

describe('customization', () => {
  const look: Appearance = { primary: '#8f2f2f', secondary: '#c9a13a', visor: '#7dffb0', head: 'beret', build: 'slim' };

  it('players restyle their own soldiers, and the look reaches the mission', () => {
    const c = createCampaign(both, new Rng(4));
    const mine = c.soldiers.find((s) => s.owner === 0)!;
    const theirs = c.soldiers.find((s) => s.owner === 1)!;
    run(c, { type: 'customize', soldier: mine.id, name: '  Ada Ríos ', nickname: 'Halcón', appearance: look }, 0, both);
    expect(mine).toMatchObject({ name: 'Ada Ríos', nickname: 'Halcón', appearance: look });
    // Personal: no proposal for the partner to vote on.
    expect(c.proposal).toBeNull();
    expect(() => run(c, { type: 'customize', soldier: theirs.id, name: 'X', nickname: '', appearance: look }, 0, both)).toThrow(/tus soldados/);
    expect(() => run(c, { type: 'customize', soldier: mine.id, name: '   ', nickname: '', appearance: look }, 0, both)).toThrow(/nombre/);
    expect(() => run(c, { type: 'customize', soldier: mine.id, name: 'Ada', nickname: 'x'.repeat(17), appearance: look }, 0, both)).toThrow(/apodo/);
    expect(() => run(c, { type: 'customize', soldier: mine.id, name: 'Ada', nickname: '', appearance: { ...look, primary: 'red' } }, 0, both)).toThrow(/aspecto/);

    c.squad = [mine.id];
    const mission = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0, 1], rng: new Rng(1), squad: buildSquad(c) });
    const unit = mission.units.find((u) => u.campaignId === mine.id)!;
    expect(unit).toMatchObject({ name: 'Ada Ríos', nickname: 'Halcón', appearance: look });
  });

  it('the default look is the armour soldiers had before customization', () => {
    // The client used to mix the player colour into grey with THREE.Color.lerp (linear space).
    expect(defaultAppearance('assault', 0).primary).toBe('#4d87bd');
    expect(defaultAppearance('assault', 1).primary).toBe('#bc854b');
  });

  it('new recruits start with their class look in their player colours', () => {
    const c = solo();
    for (const s of c.soldiers.slice(2)) s.alive = false;
    run(c, { type: 'recruit', cls: 'sharpshooter' });
    const s = c.soldiers.at(-1)!;
    expect(s.appearance).toEqual(defaultAppearance('sharpshooter', 0));
    expect(s.appearance.head).toBe('hood');
  });
});
