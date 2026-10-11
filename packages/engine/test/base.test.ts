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
  adjacencyBonuses,
  BASE_SIZE,
  clearable,
  clearCost,
  migrateCampaign,
  neighbours,
  plotAt,
  plotById,
  recruitCost,
  walkways,
  beds,
  bedsFree,
  upgradeCost,
  FACILITIES,
  listeningDelay,
  shopPrice,
  squadLimit,
  squadMember,
  type CampaignCommand,
  type CampaignState,
  type FacilityId,
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

describe('the compound', () => {
  it('starts as the bunker and three buildings, with cleared ground around and the wild beyond', () => {
    const c = solo();
    expect(plotAt(c, 3, 3)!.building).toBe('command');
    expect(plotAt(c, 2, 3)!.building).toBe('barracks');
    expect(plotAt(c, 3, 2)!.building).toBe('research');
    expect(plotAt(c, 4, 3)!.building).toBe('engineering');
    // The four diagonals and the side towards the airstrip are free to build on.
    const free = c.base.plots.filter((p) => p.terrain === 'clear' && !p.building).map((p) => p.id);
    expect(free.sort()).toEqual(['2,2', '2,4', '3,4', '4,2', '4,4']);
    expect(c.base.plots.filter((p) => p.terrain === 'clear')).toHaveLength(9);
    // Built pairs wall to wall get a walkway: the bunker to each of its three buildings.
    expect(walkways(c)).toHaveLength(3);
  });

  it('grows by clearing ground next to cleared ground, and builds next to buildings; two crews, three with the brigade', () => {
    const c = solo();
    expect(() => run(c, { type: 'build', plot: '0,0', facility: 'infirmary' })).toThrow(/despejar/);
    expect(() => run(c, { type: 'clear', plot: '0,0' })).toThrow(/a su lado/);
    const credits = c.credits;
    run(c, { type: 'clear', plot: '3,5' });
    expect(c.credits).toBe(credits - clearCost(plotById(c, '3,5')!).cost);
    expect(() => run(c, { type: 'clear', plot: '3,5' })).toThrow(/esa parcela/);
    run(c, { type: 'build', plot: '2,2', facility: 'infirmary' });
    expect(() => run(c, { type: 'build', plot: '4,2', facility: 'workshop' })).toThrow(/cuadrillas/);
    plotById(c, '4,4')!.building = 'crew';
    run(c, { type: 'build', plot: '4,2', facility: 'workshop' });
    expect(c.base.works).toHaveLength(3);
    advanceUntil(c, () => !c.base.works.length);
    expect(plotById(c, '3,5')!.terrain).toBe('clear');
    // Cleared, but nothing built beside it yet.
    expect(() => run(c, { type: 'build', plot: '3,5', facility: 'infirmary' })).toThrow(/pegado/);
  });

  it('ruins pay back in scrap when cleared', () => {
    const c = solo();
    const ruins = c.base.plots.find((p) => p.terrain === 'ruins' && !clearable(c, p))!;
    const before = c.credits;
    run(c, { type: 'clear', plot: ruins.id });
    advanceUntil(c, () => !c.base.works.length);
    expect(c.credits).toBe(before - clearCost(ruins).cost + 15);
  });

  it('the workshop makes accessories cheaper, more so next to the armoury; facilities unlock with the Bastion level', () => {
    const c = solo();
    run(c, { type: 'build', plot: '2,2', facility: 'workshop' });
    advanceUntil(c, () => !c.base.works.length);
    expect(plotById(c, '2,2')!.building).toBe('workshop');
    expect(shopPrice(c, 'smokeGrenade')).toBe(19);
    expect(() => run(c, { type: 'build', plot: '4,4', facility: 'workshop' })).toThrow(/Solo/);
    expect(() => run(c, { type: 'build', plot: '4,4', facility: 'simulation' })).toThrow(/nivel 3/);
    // Moved next to the armoury (4,3): 40 % off.
    plotById(c, '2,2')!.building = null;
    plotById(c, '4,4')!.building = 'workshop';
    expect(adjacencyBonuses(c).map((b) => [b.plot, b.next])).toEqual([['4,4', '4,3']]);
    expect(shopPrice(c, 'smokeGrenade')).toBe(15);
  });

  it('wall to wall bonuses: infirmary and barracks, training and barracks, relay and the bunker', () => {
    const c = solo();
    expect(recruitCost(c)).toBe(30);
    plotById(c, '2,4')!.building = 'training';
    expect(recruitCost(c)).toBe(20);
    const capacity = contactCapacity(c);
    plotById(c, '4,4')!.building = 'relay';
    expect(contactCapacity(c)).toBe(capacity + 2);
    plotById(c, '4,4')!.building = null;
    plotById(c, '3,4')!.building = 'relay';
    expect(contactCapacity(c)).toBe(capacity + 3);
    plotById(c, '2,2')!.building = 'infirmary';
    const s = c.soldiers[0]!;
    s.woundedDays = 9;
    run(c, { type: 'advance', ready: true });
    expect(s.woundedDays).toBeLessThanOrEqual(9 - 3);
  });

  it('the simulation room makes missions worth more experience', () => {
    const c = solo();
    c.xp = LEVEL_XP[2]!;
    run(c, { type: 'build', plot: '4,4', facility: 'simulation' });
    advanceUntil(c, () => !c.base.works.length);
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
    plotById(c, '2,2')!.building = 'training';
    expect(squadLimit(c, both)).toBe(4);
  });
});

describe('buildings and their levels', () => {
  it('the bunker sleeps the first eight; recruiting needs a barracón (2/3/4 beds, one more by the Cuartel)', () => {
    const c = solo();
    expect(beds(c)).toBe(8);
    expect(() => run(c, { type: 'recruit', cls: 'assault' })).toThrow(/camas/);
    plotById(c, '4,4')!.building = 'quarters';
    expect(beds(c)).toBe(10);
    plotById(c, '4,4')!.level = 3;
    expect(beds(c)).toBe(12);
    plotById(c, '2,4')!.building = 'quarters';
    expect(beds(c)).toBe(12 + 3);
    run(c, { type: 'recruit', cls: 'assault' });
    expect(bedsFree(c)).toBe(15 - 9);
  });

  it('a facility rises to levels 2 and 3 with the Bastion, one work at a time', () => {
    const c = solo();
    run(c, { type: 'build', plot: '2,2', facility: 'workshop' });
    advanceUntil(c, () => !c.base.works.length);
    expect(plotById(c, '2,2')!.level).toBe(1);
    expect(() => run(c, { type: 'upgrade', plot: '2,2' })).toThrow(/nivel 3/);
    c.xp = LEVEL_XP[2]!;
    const credits = c.credits;
    run(c, { type: 'upgrade', plot: '2,2' });
    expect(c.credits).toBe(credits - upgradeCost('workshop', 2).cost);
    advanceUntil(c, () => !c.base.works.length);
    expect(plotById(c, '2,2')!.level).toBe(2);
    expect(shopPrice(c, 'smokeGrenade')).toBe(Math.round(25 * 0.65));
    c.xp = LEVEL_XP[4]!;
    run(c, { type: 'upgrade', plot: '2,2' });
    advanceUntil(c, () => !c.base.works.length);
    expect(() => run(c, { type: 'upgrade', plot: '2,2' })).toThrow(/máximo/);
    expect(shopPrice(c, 'smokeGrenade')).toBe(Math.round(25 * 0.55));
  });

  it('range, works brigade, listening post, La Mula and training do what they say', () => {
    const c = solo();
    c.xp = LEVEL_XP[7]!;
    const put = (id: string, building: FacilityId, level = 1) => Object.assign(plotById(c, id)!, { terrain: 'clear', building, level });
    // Shooting range: aim for every soldier, two more next to the training centre.
    const s0 = c.soldiers[0]!;
    const aim = squadMember(c, s0).mods.aim;
    put('2,4', 'range');
    expect(squadMember(c, s0).mods.aim).toBe(aim + 3);
    put('2,4', 'range', 3);
    expect(squadMember(c, s0).mods.aim).toBe(aim + 10);
    put('3,4', 'training', 3);
    expect(squadMember(c, s0).mods.aim).toBe(aim + 12);
    // Training at level 3: recruits arrive as Cabo, with their two promotions to choose.
    put('1,3', 'quarters');
    run(c, { type: 'recruit', cls: 'assault' });
    const recruit = c.soldiers[c.soldiers.length - 1]!;
    expect(recruit.rank).toBe(2);
    expect(recruit.pendingTiers).toEqual([1, 2]);
    // Works brigade: a quarter off the days of every work.
    put('4,2', 'crew');
    run(c, { type: 'build', plot: '4,4', facility: 'infirmary' });
    expect(c.base.works[0]!.total).toBe(Math.round(FACILITIES.infirmary.days * 0.75));
    c.base.works = [];
    // Listening post: the enemy clock steps later.
    put('2,2', 'listening', 2);
    expect(listeningDelay(c)).toBe(4);
    c.nextDoomDay = c.day + 1;
    run(c, { type: 'advance', ready: true });
    expect(c.nextDoomDay - c.day).toBe(12 + 4);
    // La Mula's workshop: a mission takes a day less.
    put('5,3', 'garage');
    const offer = c.offers[0]!;
    run(c, { type: 'chooseMission', id: offer.id });
    const mission = createMission({ map: offer.map, slots: [0], rng: new Rng(2), kind: offer.kind, pods: offer.pods });
    mission.outcome = 'victory';
    const report = applyMissionResult(c, mission, new Rng(1));
    expect(c.day - report.day).toBeGreaterThanOrEqual(2);
    expect(c.day - report.day).toBeLessThanOrEqual(3);
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
      if (c.regions.filter((r) => r.contacted).length >= contactCapacity(c)) plotById(c, '2,2')!.building = 'relay';
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
    expect(report).toMatchObject({ bodies: 2, salvage: 5 + 2 });
    expect(c.credits).toBe(credits + (offer.reward.credits ?? 0) + 7);

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
    expect(m.base.plots).toHaveLength(BASE_SIZE * BASE_SIZE);
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

  it('an underground base moves to the surface: facilities keep working and the work carries on', () => {
    const c = createCampaign([0], new Rng(1));
    const slots = [0, 1, 2].flatMap((row) => [0, 1, 2].map((col) => ({ id: `r${row}c${col}`, row, col, excavated: row < 2, facility: null as string | null })));
    slots[0]!.facility = 'workshop';
    slots[1]!.facility = 'relay';
    slots[3]!.facility = 'relay';
    (c as unknown as { base: unknown }).base = { slots, construction: { slot: 'r1c1', kind: 'build', facility: 'infirmary', daysLeft: 4, total: 6 } };
    (c as { version: number }).version = 6;
    const m = migrateCampaign(c);
    expect(m.base.plots).toHaveLength(BASE_SIZE * BASE_SIZE);
    expect(m.base.plots.filter((p) => p.building === 'workshop')).toHaveLength(1);
    expect(m.base.plots.filter((p) => p.building === 'relay')).toHaveLength(2);
    // Every facility stands next to another building, on cleared ground.
    for (const p of m.base.plots.filter((x) => x.building)) {
      expect(p.terrain).toBe('clear');
      if (p.building !== 'command') expect(neighbours(m, p).some((q) => q.building)).toBe(true);
    }
    const work = m.base.works[0]!;
    expect(work).toMatchObject({ kind: 'build', facility: 'infirmary', daysLeft: 4, total: 6 });
    const site = plotById(m, work.plot)!;
    expect(site).toMatchObject({ terrain: 'clear', building: null });
    expect(neighbours(m, site).some((q) => q.building)).toBe(true);
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
