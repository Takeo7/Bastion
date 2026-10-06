import { describe, expect, it } from 'vitest';
import {
  advanceTime,
  applyMissionResult,
  contactCapacity,
  createCampaign,
  eventDef,
  executeCampaignCommand,
  gainXp,
  migrateCampaign,
  missionInfo,
  regionIncome,
  squadMember,
  type CampaignCommand,
  type CampaignState,
  type MissionOffer,
  type RegionId,
} from '../src/campaign';
import { hasTech, LEVEL_XP } from '../src/progression';
import { QUESTS } from '../src/quests';
import { Rng } from '../src/rng';
import { createMission } from '../src/setup';
import type { Slot } from '../src/types';

function run(c: CampaignState, cmd: CampaignCommand, slot: Slot = 0, connected: Slot[] = [0]) {
  const r = executeCampaignCommand(c, cmd, { slot, connected, rng: new Rng(5) });
  if (!r.ok) throw new Error(r.error);
  return r;
}

/** A solo campaign with no random events and no rumours of its own. */
function solo(seed = 3): CampaignState {
  const c = createCampaign([0], new Rng(seed));
  c.nextEventDay = 999;
  c.nextRumorDay = 999;
  return c;
}

function advanceUntil(c: CampaignState, done: () => boolean, seed = 1) {
  for (let i = 0; i < 30 && !done(); i++) {
    c.event = null;
    advanceTime(c, new Rng(seed + i));
  }
  expect(done()).toBe(true);
}

/** Flies `offer` and brings it back with `outcome`. */
function fly(c: CampaignState, offer: MissionOffer, outcome: 'victory' | 'defeat') {
  run(c, { type: 'chooseMission', id: offer.id });
  const info = missionInfo(c, offer);
  const mission = createMission({ map: offer.map, slots: [0], rng: new Rng(2), kind: offer.kind, pods: offer.pods, vipName: info.vipName, leader: info.leader });
  mission.outcome = outcome;
  applyMissionResult(c, mission, new Rng(1));
  return mission;
}

const questOffer = (c: CampaignState, id: string) => c.offers.find((o) => o.quest === id)!;

/** Puts a rumour in `region`, listens to it and scans until it resolves. */
function listen(c: CampaignState, region: RegionId) {
  c.regions.find((r) => r.id === region)!.contacted = true;
  c.rumors = [{ id: 'r99', region, text: 'algo', expiresDay: c.day + 10, daysLeft: 2 }];
  run(c, { type: 'investigate', id: 'r99' });
  expect(c.listening).toBe('r99');
  advanceTime(c, new Rng(1));
  expect(c.rumors).toHaveLength(0);
  expect(c.listening).toBeNull();
}

describe('rumours', () => {
  it('appear while scanning, with a pop-up, and cool down if nobody listens', () => {
    const c = solo();
    c.nextRumorDay = c.day + 1;
    advanceTime(c, new Rng(1));
    expect(c.rumors).toHaveLength(1);
    expect(c.log[0]!.alert?.kicker).toBe('RUMOR');
    const rumor = c.rumors[0]!;
    advanceUntil(c, () => c.day > rumor.expiresDay && !c.rumors.some((r) => r.id === rumor.id));
  });

  it("listening to one in a contacted region opens that region's side quest", () => {
    const c = solo();
    listen(c, 'weu');
    expect(c.quests.map((q) => q.id)).toEqual(['bells']);
    expect(c.event).toBe('quest');
    expect(eventDef(c)?.title).toBe(QUESTS.bells.title);
  });
});

describe('side quests', () => {
  it('a decision leads to an operation, and winning it pays off for good', () => {
    const c = solo();
    listen(c, 'weu');
    run(c, { type: 'answerEvent', option: 0 });
    const offer = questOffer(c, 'bells');
    expect(offer).toMatchObject({ kind: 'sabotage', region: 'weu' });
    expect(offer.expiresDay).toBe(c.day + 8);
    const flashbangs = c.inventory.flashbang ?? 0;
    fly(c, offer, 'victory');
    expect(c.quests).toHaveLength(0);
    expect(c.questLog.bells?.outcome).toBe('success');
    expect(c.inventory.flashbang).toBe(flashbangs + 2);
    expect(regionIncome(c, 'weu')).toBe(40);
  });

  it('an operation left to expire loses the quest with its consequence', () => {
    const c = solo();
    listen(c, 'na');
    const offer = questOffer(c, 'route50');
    expect(missionInfo(c, offer).vipName).toBe('Lola Haskins');
    advanceUntil(c, () => !!c.questLog.route50);
    expect(c.questLog.route50?.outcome).toBe('failure');
    expect(regionIncome(c, 'na')).toBe(25);
  });

  it('Ibarra leads her own squad, and capturing her brings her back scarred', () => {
    const c = solo();
    c.story.done = 1;
    gainXp(c, LEVEL_XP[2]! - c.xp, new Rng(1));
    fly(c, questOffer(c, 'ibarra'), 'victory');
    const hunt = questOffer(c, 'ibarra');
    expect(hunt.kind).toBe('elimination');
    const mission = fly(c, hunt, 'victory');
    expect(mission.units.find((u) => u.team === 'alien' && u.name === 'Elena Ibarra')?.template).toBe('officer');
    expect(eventDef(c)?.title).toBe('Elena Ibarra');
    run(c, { type: 'answerEvent', option: 0 });
    const ibarra = c.soldiers.find((s) => s.name === 'Elena Ibarra')!;
    expect(ibarra).toMatchObject({ cls: 'sharpshooter', rank: 3, traits: ['scarred'] });
    expect(ibarra.pendingTiers.length).toBeGreaterThan(0);
    expect(squadMember(c, ibarra).will).toBeLessThan(squadMember(c, { ...ibarra, traits: [] }).will);
  });

  it('the other Amara brings medical tesserae before their level', () => {
    const c = solo();
    c.story.done = 2;
    c.regions.find((r) => r.id === 'afr')!.contacted = true;
    advanceTime(c, new Rng(1));
    expect(c.questEvent).toBe('amara');
    expect(hasTech(c, 'nanoMedkit')).toBe(false);
    run(c, { type: 'answerEvent', option: 0 });
    fly(c, questOffer(c, 'amara'), 'victory');
    run(c, { type: 'answerEvent', option: 0 });
    expect(c.questLog.amara?.outcome).toBe('rescued');
    expect(hasTech(c, 'nanoMedkit')).toBe(true);
  });

  it('the numbers station adds a contact', () => {
    const c = solo();
    c.day = 20;
    gainXp(c, LEVEL_XP[1]! - c.xp, new Rng(1));
    const before = contactCapacity(c);
    fly(c, questOffer(c, 'numbers'), 'victory');
    fly(c, questOffer(c, 'numbers'), 'victory');
    expect(contactCapacity(c)).toBe(before + 1);
  });

  it('the mole: watching someone gives a clue, and only the right accusation pays', () => {
    const accuse = (right: boolean) => {
      const c = solo();
      c.seen.sabotage = 2;
      c.credits = 100;
      advanceTime(c, new Rng(1));
      expect(c.questEvent).toBe('mole');
      const secret = c.quests[0]!.secret;
      run(c, { type: 'answerEvent', option: 0 });
      // The clue opens the second decision.
      expect(eventDef(c)?.text).toContain(secret === 0 ? 'antena vieja' : 'no se mueve');
      const doom = c.doom;
      run(c, { type: 'answerEvent', option: (right ? secret : 1 - secret) as 0 | 1 });
      return { c, doom };
    };
    const right = accuse(true);
    expect(right.c.questLog.mole?.outcome).toBe('success');
    expect(right.c.doom).toBe(right.doom - 1);
    const wrong = accuse(false);
    expect(wrong.c.questLog.mole?.outcome).toBe('failure');
    expect(wrong.c.credits).toBe(40);
  });

  it('turning down a soldier who saw a relative leaves a grudge', () => {
    for (let seed = 0; seed < 60; seed++) {
      const c = solo(seed);
      gainXp(c, LEVEL_XP[1]! - c.xp, new Rng(1));
      c.quests = [];
      c.offers = c.offers.filter((o) => !o.quest);
      for (const s of c.soldiers) s.nickname = 'Mula';
      run(c, { type: 'chooseMission', id: c.offers[0]!.id });
      for (const s of c.soldiers.slice(0, 4)) run(c, { type: 'squad', soldier: s.id, on: true });
      const mission = createMission({ map: c.offers[0]!.map, slots: [0], rng: new Rng(2), kind: c.offers[0]!.kind, squad: c.squad.map((id) => squadMember(c, c.soldiers.find((s) => s.id === id)!)) });
      mission.outcome = 'victory';
      applyMissionResult(c, mission, new Rng(seed));
      const faces = c.quests.find((q) => q.id === 'faces');
      if (!faces) continue;
      expect(c.questEvent).toBe('faces');
      expect(eventDef(c)?.text).toContain(faces.names.relative!);
      const soldier = c.soldiers.find((s) => s.id === faces.soldier)!;
      const aim = squadMember(c, soldier).mods.aim;
      run(c, { type: 'answerEvent', option: 1 });
      expect(soldier.traits).toEqual(['grudge']);
      expect(squadMember(c, soldier).mods.aim).toBe(aim + 10);
      return;
    }
    throw new Error('No soldier recognised anyone in 60 campaigns');
  });

  it('an older campaign gets empty quests and rumours', () => {
    const c = createCampaign([0], new Rng(1)) as unknown as Record<string, unknown> & CampaignState;
    for (const key of ['quests', 'questLog', 'questEvent', 'rumors', 'listening', 'nextRumorDay', 'techs', 'seen']) delete c[key];
    for (const s of c.soldiers) delete (s as Partial<typeof s>).traits;
    c.version = 5 as CampaignState['version'];
    const m = migrateCampaign(c);
    expect(m).toMatchObject({ quests: [], questLog: {}, questEvent: null, rumors: [], listening: null, techs: [], seen: {} });
    expect(m.nextRumorDay).toBeGreaterThan(m.day);
    expect(m.soldiers.every((s) => Array.isArray(s.traits))).toBe(true);
  });
});
