import { describe, expect, it } from 'vitest';
import {
  advanceTime,
  applyMissionResult,
  createCampaign,
  eventDef,
  executeCampaignCommand,
  gainXp,
  migrateCampaign,
  missionInfo,
  offerVip,
  type CampaignCommand,
  type CampaignState,
  type MissionOffer,
} from '../src/campaign';
import { SIMON_CHAPTER, STORY } from '../src/lore';
import { LEVEL_XP, MAX_LEVEL } from '../src/progression';
import { Rng } from '../src/rng';
import { createMission, normalizeMission } from '../src/setup';
import type { Slot } from '../src/types';

function run(c: CampaignState, cmd: CampaignCommand, slot: Slot = 0, connected: Slot[] = [0]) {
  const r = executeCampaignCommand(c, cmd, { slot, connected, rng: new Rng(5) });
  if (!r.ok) throw new Error(r.error);
  return r;
}

function solo(): CampaignState {
  const c = createCampaign([0], new Rng(3));
  c.nextEventDay = 999;
  return c;
}

const chapter = (c: CampaignState, n: number) => c.offers.find((o) => o.story === n);

/** Flies `offer` and brings it back with `outcome`; `deadAliens` lie on the field. */
function fly(c: CampaignState, offer: MissionOffer, outcome: 'victory' | 'defeat', deadAliens = false) {
  run(c, { type: 'chooseMission', id: offer.id });
  const mission = createMission({ map: offer.map, slots: [0], rng: new Rng(2), kind: offer.kind, pods: offer.pods });
  if (deadAliens) for (const u of mission.units) if (u.team === 'alien' && !u.objective) u.alive = false;
  mission.outcome = outcome;
  return applyMissionResult(c, mission, new Rng(1));
}

describe('story', () => {
  it('a saved campaign of Fase 10 keeps its soldiers\' bars, growth and board', () => {
    const old = solo();
    const s = old.soldiers[0]!;
    s.board = ['steadyHand'];
    const updated = migrateCampaign(old);
    expect(updated.soldiers[0]).toMatchObject({ skills: s.skills, growth: s.growth, board: ['steadyHand'] });
  });


  it('updates the presentation of a saved opening battle without resetting the battle', () => {
    const state = createMission({ map: { kind: 'generated', biome: 'city', seed: 2 }, slots: [0], rng: new Rng(2), campaignMission: { id: 'opening', story: 1, name: 'Primer invierno', kicker: 'DIARIO · HOJA 1', briefing: 'Old report.' } });
    const expected = JSON.parse(JSON.stringify(state)) as typeof state;
    expected.campaignMission = { id: 'opening', story: 1, name: STORY[0]!.title, briefing: STORY[0]!.briefing };
    expect(normalizeMission(state)).toEqual(expected);
  });

  it('chapter 1 is there from the start, never expires and survives a defeat', () => {
    const c = solo();
    const first = chapter(c, 1)!;
    expect(first).toMatchObject({ name: STORY[0]!.title, kind: 'recovery', region: 'weu', expiresDay: null });
    fly(c, first, 'defeat');
    expect(chapter(c, 1)).toBeDefined();
    expect(c.story.done).toBe(0);
  });

  it('winning a chapter opens the next one once the Bastion reaches its level', () => {
    const c = solo();
    const report = fly(c, chapter(c, 1)!, 'victory');
    expect(report.story).toBe(1);
    expect(c.story.done).toBe(1);
    expect(chapter(c, 1)).toBeUndefined();
    expect(chapter(c, 2)).toBeUndefined();
    gainXp(c, LEVEL_XP[1]! - c.xp, new Rng(1));
    // Chapters don't chain back to back: the next one waits some days after the last win.
    expect(chapter(c, 2)).toBeUndefined();
    for (let i = 0; i < 10 && !chapter(c, 2); i++) run(c, { type: 'advance', ready: true });
    expect(c.day).toBeGreaterThanOrEqual(c.story.nextDay!);
    expect(chapter(c, 2)).toMatchObject({ kind: 'rescue', region: 'afr', expiresDay: null });
    expect(missionInfo(c, chapter(c, 2)!).kicker).toBe('DIARIO · HOJA 2');
    expect(offerVip(c, chapter(c, 2)!)).toBe('Dra. Amara Nwosu');
  });

  it('Simão knocks before chapter 5; turning him away makes it harder and changes who waits inside', () => {
    const answer = (option: 0 | 1) => {
      const c = solo();
      c.story.done = SIMON_CHAPTER - 1;
      gainXp(c, LEVEL_XP[SIMON_CHAPTER - 1]! - c.xp, new Rng(1));
      expect(c.event).toBe('simon');
      expect(chapter(c, SIMON_CHAPTER)).toBeUndefined();
      const xp = c.xp;
      run(c, { type: 'answerEvent', option });
      return { c, offer: chapter(c, SIMON_CHAPTER)!, xp };
    };
    const trusted = answer(0);
    expect(trusted.c.story.simon).toBe('trusted');
    expect(trusted.offer.difficulty).toBe(STORY[SIMON_CHAPTER - 1]!.difficulty);
    expect(offerVip(trusted.c, trusted.offer)).toBe('Simão Ferreira');
    const refused = answer(1);
    expect(refused.c.story.simon).toBe('refused');
    expect(refused.c.xp).toBe(refused.xp + 12);
    expect(refused.offer.difficulty).toBe(STORY[SIMON_CHAPTER - 1]!.difficulty + 1);
    expect(offerVip(refused.c, refused.offer)).toBe('Noor Haddad');
  });

  it('the rescue VIP of a chapter reaches the battlefield with their name', () => {
    const c = solo();
    c.story.done = 6;
    gainXp(c, LEVEL_XP[6]! - c.xp, new Rng(1));
    const offer = chapter(c, 7)!;
    const mission = createMission({ map: offer.map, slots: [0], rng: new Rng(2), kind: offer.kind, pods: offer.pods, vipName: offerVip(c, offer) });
    expect(mission.units.find((u) => u.template === 'vip')?.name).toBe('Celia Marsh');
  });

  it('Dr Nwosu makes the bodies worth a quarter more', () => {
    const salvage = (done: number) => {
      const c = solo();
      c.story.done = done;
      return fly(c, c.offers.find((o) => !o.story)!, 'victory', true).salvage;
    };
    const before = salvage(1);
    expect(before).toBeGreaterThan(0);
    expect(salvage(2)).toBe(Math.round(before * 1.25));
  });

  it('the final mission needs every chapter, and ends with a decision taken with both keys', () => {
    const c = createCampaign([0, 1], new Rng(4));
    c.nextEventDay = 999;
    c.story.done = 6;
    c.story.simon = 'trusted';
    gainXp(c, LEVEL_XP[MAX_LEVEL - 1]! - c.xp, new Rng(1));
    expect(c.offers.some((o) => o.final)).toBe(false);
    fly(c, chapter(c, 7)!, 'victory');
    const final = c.offers.find((o) => o.final)!;
    expect(final).toBeDefined();
    fly(c, final, 'victory');
    expect(c.event).toBe('finale');
    expect(c.outcome).toBeNull();
    // One commander proposes, the other turns the second key.
    run(c, { type: 'answerEvent', option: 1 }, 0, [0, 1]);
    expect(c.outcome).toBeNull();
    run(c, { type: 'vote', accept: true }, 1, [0, 1]);
    expect(c.ending).toBe('record');
    expect(c.outcome).toBe('victory');
  });

  it('random events never bring a story event', () => {
    for (let seed = 0; seed < 40; seed++) {
      const c = solo();
      c.nextEventDay = c.day + 1;
      advanceTime(c, new Rng(seed));
      if (c.event) expect(eventDef(c)?.story).toBeUndefined();
    }
  });

});
