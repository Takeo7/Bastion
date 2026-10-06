import { describe, expect, it } from 'vitest';
import { previewShot } from '../src/aim';
import {
  addPlayer,
  applyMissionResult,
  buildSquad,
  contactCost,
  createCampaign,
  nextSteps,
  soldierCap,
  DOOM_MAX,
  executeCampaignCommand,
  squadMember,
  type CampaignCommand,
  type CampaignState,
} from '../src/campaign';
import { armorTier, bastionLevel, LEVEL_XP, MAX_LEVEL, missionXp, nextTierBlocker, weaponTier } from '../src/progression';
import { Rng } from '../src/rng';
import { createMission } from '../src/setup';
import type { Slot } from '../src/types';

const both: Slot[] = [0, 1];

function run(c: CampaignState, cmd: CampaignCommand, slot: Slot = 0, connected: Slot[] = both) {
  const r = executeCampaignCommand(c, cmd, { slot, connected, rng: new Rng(7) });
  if (!r.ok) throw new Error(r.error);
  return r;
}

describe('campaign setup', () => {
  it('two players start with four soldiers each, credits, a level-1 Bastion, two operations and the first story chapter', () => {
    const c = createCampaign(both, new Rng(1));
    expect(c.soldiers.filter((s) => s.owner === 0)).toHaveLength(4);
    expect(c.soldiers.filter((s) => s.owner === 1)).toHaveLength(4);
    expect(c.offers.filter((o) => !o.story)).toHaveLength(2);
    expect(c.offers.filter((o) => o.story).map((o) => o.story)).toEqual([1]);
    expect(c.credits).toBeGreaterThan(0);
    expect(bastionLevel(c)).toBe(1);
  });

  it('a solo campaign gives all eight soldiers to the only player', () => {
    const c = createCampaign([0], new Rng(1));
    expect(c.soldiers.every((s) => s.owner === 0)).toBe(true);
    expect(c.soldiers).toHaveLength(8);
  });
});

describe('shared decisions', () => {
  it('contacting a region is proposed to the partner and happens when accepted', () => {
    const c = createCampaign(both, new Rng(1));
    const credits = c.credits;
    const cost = contactCost(c);
    const na = c.regions.find((r) => r.id === 'na')!;
    run(c, { type: 'contact', region: 'na' }, 0);
    expect(c.proposal?.by).toBe(0);
    expect(na.contacted).toBe(false);
    expect(executeCampaignCommand(c, { type: 'vote', accept: true }, { slot: 0, connected: both, rng: new Rng(1) }).ok).toBe(false);
    run(c, { type: 'vote', accept: true }, 1);
    expect(na.contacted).toBe(true);
    expect(c.credits).toBe(credits - cost);
    expect(c.proposal).toBeNull();
  });

  it('a rejected proposal changes nothing; alone, decisions apply directly', () => {
    const c = createCampaign(both, new Rng(1));
    run(c, { type: 'excavate', slot: 'r1c0' }, 1);
    run(c, { type: 'vote', accept: false }, 0);
    expect(c.base.construction).toBeNull();
    run(c, { type: 'excavate', slot: 'r1c0' }, 1, [1]);
    expect(c.base.construction?.slot).toBe('r1c0');
  });
});

describe('time', () => {
  it('only advances when everyone is ready, and stops when the works finish', () => {
    const c = createCampaign(both, new Rng(1));
    c.nextEventDay = 999;
    run(c, { type: 'excavate', slot: 'r1c0' }, 0, [0]);
    run(c, { type: 'advance', ready: true }, 0);
    expect(c.day).toBe(1);
    run(c, { type: 'advance', ready: true }, 1);
    expect(c.day).toBeGreaterThan(1);
    for (let i = 0; i < 8 && c.base.construction; i++) {
      run(c, { type: 'advance', ready: true }, 0);
      run(c, { type: 'advance', ready: true }, 1);
    }
    expect(c.base.slots.find((s) => s.id === 'r1c0')!.excavated).toBe(true);
    expect(c.advanceReady).toEqual([false, false]);
  });

  it('wounds heal day by day; the enemy project ends the campaign at its maximum', () => {
    const c = createCampaign([0], new Rng(1));
    c.nextEventDay = 999;
    c.soldiers[0]!.woundedDays = 2;
    run(c, { type: 'advance', ready: true }, 0, [0]);
    expect(c.soldiers[0]!.woundedDays).toBeLessThan(2);
    c.doom = DOOM_MAX - 1;
    for (let i = 0; i < 10 && !c.outcome; i++) run(c, { type: 'advance', ready: true }, 0, [0]);
    expect(c.outcome).toBe('defeat');
  });
});

describe('missions', () => {
  function ready() {
    const c = createCampaign(both, new Rng(2));
    const offer = c.offers[0]!;
    run(c, { type: 'chooseMission', id: offer.id }, 0, [0]);
    // Start from an empty squad to pick it by hand (it comes filled in: see "default squad").
    c.squad = [];
    const mine = c.soldiers.filter((s) => s.owner === 0);
    const theirs = c.soldiers.filter((s) => s.owner === 1);
    for (const s of mine.slice(0, 3)) run(c, { type: 'squad', soldier: s.id, on: true }, 0);
    for (const s of theirs.slice(0, 3)) run(c, { type: 'squad', soldier: s.id, on: true }, 1);
    return { c, offer, mine, theirs };
  }

  it('each player brings at most three of their own soldiers', () => {
    const { c, mine, theirs } = ready();
    expect(c.squad).toHaveLength(6);
    expect(executeCampaignCommand(c, { type: 'squad', soldier: mine[3]!.id, on: true }, { slot: 0, connected: both, rng: new Rng(1) }).ok).toBe(false);
    expect(executeCampaignCommand(c, { type: 'squad', soldier: theirs[3]!.id, on: true }, { slot: 0, connected: both, rng: new Rng(1) }).ok).toBe(false);
  });

  it('launches only when both are ready', () => {
    const { c } = ready();
    expect(run(c, { type: 'launch', ready: true }, 0).launch).toBeUndefined();
    expect(run(c, { type: 'launch', ready: true }, 1).launch).toBe(true);
  });

  it('results come back: deaths, wounds, experience, promotions and rewards', () => {
    const { c, offer } = ready();
    const squad = buildSquad(c);
    const mission = createMission({ map: offer.map, slots: both, rng: new Rng(3), kind: offer.kind, squad, pods: offer.pods, difficulty: offer.difficulty });
    const soldiers = mission.units.filter((u) => u.team === 'xcom');
    soldiers[0]!.alive = false;
    soldiers[0]!.hp = 0;
    soldiers[1]!.hp = soldiers[1]!.maxHp - 2;
    soldiers[2]!.kills = 6;
    mission.outcome = 'victory';
    mission.outcomeReason = 'objective';
    const credits = c.credits;
    const report = applyMissionResult(c, mission, new Rng(4));
    expect(report.victory).toBe(true);
    expect(report.squad).toHaveLength(6);
    expect(report.squad.find((e) => e.id === soldiers[0]!.campaignId)?.status).toBe('dead');
    expect(report.squad.find((e) => e.id === soldiers[1]!.campaignId)).toMatchObject({ status: 'wounded', woundedDays: 6, xpGained: 2 });
    expect(report.squad.find((e) => e.id === soldiers[2]!.campaignId)).toMatchObject({ kills: 6, xpGained: 8, promotedTo: 2 });
    expect(c.soldiers.find((s) => s.id === soldiers[0]!.campaignId)!.alive).toBe(false);
    expect(c.soldiers.find((s) => s.id === soldiers[1]!.campaignId)!.woundedDays).toBe(6);
    const veteran = c.soldiers.find((s) => s.id === soldiers[2]!.campaignId)!;
    expect(veteran.xp).toBe(8);
    expect(veteran.rank).toBe(2);
    expect(veteran.pendingTiers).toEqual([1, 2]);
    expect(c.credits).toBe(credits + (offer.reward.credits ?? 0) + report.salvage);
    // The Bastion learns from every mission: showing up, winning and the kills.
    expect(report.xp).toBe(missionXp(true, offer.difficulty, 6));
    expect(c.xp).toBe(report.xp);
    expect(c.offers.some((o) => o.id === offer.id)).toBe(false);
    expect(c.activeMission).toBeNull();
  });

  it('perks, ranks and weapon and armour tiers reach the battlefield', () => {
    const c = createCampaign([0], new Rng(5));
    const sniper = c.soldiers.find((s) => s.cls === 'sharpshooter')!;
    sniper.rank = 4;
    sniper.pendingTiers = [1, 2, 3, 4];
    run(c, { type: 'promote', soldier: sniper.id, perk: 'hawkEye' }, 0, [0]);
    // Level 4: magnetic weapons and plate armour, and a lieutenant may carry both.
    c.xp = LEVEL_XP[3]!;
    const member = squadMember(c, sniper);
    expect(member).toMatchObject({ weaponTier: 2, armorTier: 2 });
    expect(member.mods.aim).toBe(4 * 5 + 10);
    expect(member.mods.damage).toBe(2);
    expect(member.maxHp).toBe(5 + 2 + 2);

    const mission = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(1), squad: [member] });
    const shooter = mission.units.find((u) => u.campaignId === sniper.id)!;
    // a3 belongs to the central pod, standing in the open.
    const target = mission.units.find((u) => u.id === 'a3')!;
    shooter.pos = { x: target.pos.x, y: target.pos.y + 5 };
    const shot = previewShot(mission, shooter, target);
    expect(shot?.mods[0]).toEqual({ label: 'Puntería', value: 70 + 30 });
  });
});

describe('progression', () => {
  it('soldiers carry the best tier both their rank and the Bastion allow', () => {
    const c = createCampaign([0], new Rng(1));
    expect(weaponTier(c, { rank: 7 })).toBe(1);
    // Level 6: plasma weapons; predator armour still needs level 7.
    c.xp = LEVEL_XP[5]!;
    expect(weaponTier(c, { rank: 0 })).toBe(1);
    expect(weaponTier(c, { rank: 2 })).toBe(2);
    expect(weaponTier(c, { rank: 4 })).toBe(3);
    expect(armorTier(c, { rank: 4 })).toBe(2);
    expect(nextTierBlocker(c, { rank: 4 }, 'armor')).toMatch(/nivel 7 del Bastión/);
    expect(nextTierBlocker(c, { rank: 2 }, 'weapon')).toMatch(/rango Teniente/);
    expect(nextTierBlocker(c, { rank: 4 }, 'weapon')).toBeNull();
  });

  it('reaching the top level with every story chapter won reveals the final mission', () => {
    const c = createCampaign([0], new Rng(1));
    c.story.done = 7;
    const offer = c.offers[0]!;
    run(c, { type: 'chooseMission', id: offer.id }, 0, [0]);
    c.xp = LEVEL_XP[MAX_LEVEL - 1]! - 1;
    const mission = createMission({ map: offer.map, slots: [0], rng: new Rng(3), kind: offer.kind, pods: offer.pods });
    mission.outcome = 'defeat';
    const report = applyMissionResult(c, mission, new Rng(4));
    expect(report.levelFrom).toBe(MAX_LEVEL - 1);
    expect(report.levelTo).toBe(MAX_LEVEL);
    expect(c.offers.some((o) => o.final)).toBe(true);
  });
});

describe('helping the players', () => {
  it('a chosen mission comes with its squad: best ready soldiers, one of each class first', () => {
    const c = createCampaign(both, new Rng(2));
    const veteran = c.soldiers.find((s) => s.owner === 0 && s.cls === 'assault')!;
    veteran.rank = 3;
    c.soldiers.find((s) => s.owner === 1)!.woundedDays = 3;
    run(c, { type: 'chooseMission', id: c.offers[0]!.id }, 0, [0, 1]);
    run(c, { type: 'vote', accept: true }, 1, [0, 1]);
    const picked = c.squad.map((id) => c.soldiers.find((s) => s.id === id)!);
    expect(picked.filter((s) => s.owner === 0)).toHaveLength(3);
    expect(picked.filter((s) => s.owner === 1)).toHaveLength(3);
    expect(picked).toContain(veteran);
    expect(picked.every((s) => s.woundedDays === 0)).toBe(true);
    expect(new Set(picked.filter((s) => s.owner === 0).map((s) => s.cls)).size).toBe(3);
    // Adding someone already in it is fine; swapping is out then in.
    run(c, { type: 'squad', soldier: veteran.id, on: true }, 0, [0, 1]);
    expect(c.squad).toHaveLength(6);
  });

  it('alone, a commander keeps both kits; a partner who arrives later takes over the untouched rookies', () => {
    const c = createCampaign([0], new Rng(3));
    expect(soldierCap(c)).toBe(12);
    const veteran = c.soldiers[0]!;
    veteran.missions = 2;
    expect(addPlayer(c, 1, new Rng(1))).toBe(4);
    expect(c.players).toEqual([0, 1]);
    expect(c.soldiers.filter((s) => s.owner === 1)).toHaveLength(4);
    expect(veteran.owner).toBe(0);
    expect(soldierCap(c)).toBe(6);
    expect(addPlayer(c, 1, new Rng(1))).toBe(0);
  });

  it('what to do now: the most pressing thing first', () => {
    const c = createCampaign(both, new Rng(1));
    expect(nextSteps(c, 0)[0]).toMatchObject({ view: 'geoscape', label: 'Elige una operación', count: c.offers.length });
    const expiring = c.offers.find((o) => o.expiresDay !== null)!;
    expiring.expiresDay = c.day + 1;
    expect(nextSteps(c, 0)[0]).toMatchObject({ tone: 'urgent', label: `${expiring.name} caduca en 1 día`, go: { offer: expiring.id } });
    const promoted = c.soldiers.find((s) => s.owner === 1)!;
    promoted.pendingTiers = [1];
    expect(nextSteps(c, 1).some((x) => x.view === 'barracks' && x.go?.soldier === promoted.id)).toBe(true);
    expect(nextSteps(c, 0).some((x) => x.view === 'barracks' && x.label.startsWith('Ascenso'))).toBe(false);
    // Equipment only nags before a mission, and only for what can actually be fitted.
    expect(nextSteps(c, 0).some((x) => x.label.startsWith('Equipa'))).toBe(false);
    run(c, { type: 'chooseMission', id: c.offers[0]!.id }, 0, [0]);
    expect(nextSteps(c, 0)[0]).toMatchObject({ view: 'squad' });
    // Starting stock: 2 extra grenades and a medkit, all utility.
    expect(nextSteps(c, 0).find((x) => x.label.startsWith('Equipa'))?.count).toBe(3);
  });
});
