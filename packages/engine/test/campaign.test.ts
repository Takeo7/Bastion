import { describe, expect, it } from 'vitest';
import { previewShot } from '../src/aim';
import { specialize, VETERAN_AIM } from '../src/classes';
import {
  addPlayer,
  LATE_KIT,
  applyMissionResult,
  buildSquad,
  contactCost,
  createCampaign,
  nextSteps,
  pointsToSpend,
  soldierCap,
  squadPads,
  veteranEnemies,
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
    run(c, { type: 'clear', plot: '3,5' }, 1);
    run(c, { type: 'vote', accept: false }, 0);
    expect(c.base.works).toEqual([]);
    run(c, { type: 'clear', plot: '3,5' }, 1, [1]);
    expect(c.base.works[0]?.plot).toBe('3,5');
  });
});

describe('time', () => {
  it('only advances when everyone is ready, and stops when the works finish', () => {
    const c = createCampaign(both, new Rng(1));
    c.nextEventDay = 999;
    run(c, { type: 'clear', plot: '3,5' }, 0, [0]);
    run(c, { type: 'advance', ready: true }, 0);
    expect(c.day).toBe(1);
    run(c, { type: 'advance', ready: true }, 1);
    expect(c.day).toBeGreaterThan(1);
    for (let i = 0; i < 8 && c.base.works.length; i++) {
      run(c, { type: 'advance', ready: true }, 0);
      run(c, { type: 'advance', ready: true }, 1);
    }
    expect(c.base.plots.find((p) => p.id === '3,5')!.terrain).toBe('clear');
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

  it('a share the partner cannot fill is lent to the other player, and comes back when they can', () => {
    const c = createCampaign(both, new Rng(2));
    c.credits = 500;
    // The bunker sleeps the eight starting soldiers: recruiting needs a barracón.
    c.base.plots.find((p) => p.id === '2,4')!.building = 'quarters';
    const theirs = c.soldiers.filter((s) => s.owner === 1);
    // Player 1 has a single soldier fit to fight.
    for (const s of theirs.slice(1)) s.woundedDays = 5;
    run(c, { type: 'chooseMission', id: c.offers[0]!.id }, 0, both);
    run(c, { type: 'vote', accept: true }, 1, both);
    const count = (slot: Slot) => c.squad.filter((id) => c.soldiers.find((s) => s.id === id)!.owner === slot).length;
    // The squad comes as full as it can: player 0's fourth soldier takes a place player 1 can't fill.
    expect([count(0), count(1)]).toEqual([4, 1]);
    // Recruiting from a free place in the hangar goes straight into the squad.
    run(c, { type: 'recruit', squad: true }, 0, both);
    expect([count(0), count(1)]).toEqual([5, 1]);
    // Player 1's wounded soldier heals: the lent place comes back, and the last of player 0's lent soldiers leaves.
    theirs[1]!.woundedDays = 0;
    run(c, { type: 'squad', soldier: theirs[1]!.id, on: true }, 1, both);
    expect([count(0), count(1)]).toEqual([4, 2]);
    // While player 1 still has someone ready to bring, their share is theirs.
    run(c, { type: 'squad', soldier: theirs[1]!.id, on: false }, 1, both);
    const out = c.soldiers.find((s) => s.owner === 0 && !c.squad.includes(s.id))!;
    expect(() => run(c, { type: 'squad', soldier: out.id, on: true }, 0, both)).toThrow(/compañero/);
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
    // The report says how bad it was; the flight back and the recovery already healed some of it.
    const wounded = c.soldiers.find((s) => s.id === soldiers[1]!.campaignId)!.woundedDays;
    expect(wounded).toBeGreaterThan(0);
    expect(wounded).toBeLessThan(6);
    // A mission takes days: three or four go by.
    expect(c.day - report.day).toBeGreaterThanOrEqual(3);
    expect(c.day - report.day).toBeLessThanOrEqual(4);
    const veteran = c.soldiers.find((s) => s.id === soldiers[2]!.campaignId)!;
    expect(veteran.xp).toBe(8);
    expect(veteran.rank).toBe(2);
    expect(pointsToSpend(veteran)).toEqual({ attrs: 4, talents: 4 });
    expect(c.credits).toBe(credits + (offer.reward.credits ?? 0) + report.salvage);
    // The Bastion learns from every mission: showing up, winning and the kills.
    expect(report.xp).toBe(missionXp(true, offer.difficulty, 6));
    expect(c.xp).toBe(report.xp);
    expect(c.offers.some((o) => o.id === offer.id)).toBe(false);
    expect(c.activeMission).toBeNull();
  });

  it('everyone starts as a recruit; reaching Capitán makes them the class they were taught most', () => {
    const { c, offer } = ready();
    expect(c.soldiers.every((s) => s.cls === null)).toBe(true);
    const mission = createMission({ map: offer.map, slots: both, rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    const unit = mission.units.find((u) => u.team === 'xcom')!;
    const s = c.soldiers.find((x) => x.id === unit.campaignId)!;
    s.rank = 4;
    s.xp = 19;
    s.talents = { demolition: 2, bandolier: 1, runner: 1 };
    mission.outcome = 'victory';
    mission.outcomeReason = 'objective';
    const report = applyMissionResult(c, mission, new Rng(4));
    expect(report.squad.find((e) => e.id === s.id)).toMatchObject({ promotedTo: 5, specializedAs: 'grenadier' });
    expect(s.cls).toBe('grenadier');
    expect(c.log.some((l) => /se especializa como Granadero/.test(l.text))).toBe(true);
    expect(pointsToSpend(s)).toEqual({ attrs: 10, talents: 6 });
  });

  it('the Coro sends a little more with every few missions flown', () => {
    const { c, offer } = ready();
    expect(veteranEnemies(c)).toBe(0);
    const mission = createMission({ map: offer.map, slots: both, rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    mission.outcome = 'victory';
    applyMissionResult(c, mission, new Rng(4));
    expect(c.missionsFlown).toBe(1);
    c.missionsFlown = 4;
    expect(veteranEnemies(c)).toBe(1);
    c.missionsFlown = 40;
    expect(veteranEnemies(c)).toBe(2);
  });

  it('aptitudes, talents, ranks and weapon and armour tiers reach the battlefield', () => {
    const c = createCampaign([0], new Rng(5));
    const sniper = c.soldiers[0]!;
    specialize(sniper, 'sharpshooter');
    sniper.rank = 4;
    run(c, { type: 'train', soldier: sniper.id, attr: 'pul' }, 0, [0]);
    run(c, { type: 'talent', soldier: sniper.id, talent: 'hawkEye' }, 0, [0]);
    run(c, { type: 'talent', soldier: sniper.id, talent: 'hawkEye' }, 0, [0]);
    expect(sniper.attrs.pul).toBe(7);
    expect(pointsToSpend(sniper)).toEqual({ attrs: 7, talents: 6 });
    // Level 4: magnetic weapons and plate armour, and a lieutenant may carry both.
    c.xp = LEVEL_XP[3]!;
    const member = squadMember(c, sniper);
    expect(member).toMatchObject({ weaponTier: 2, armorTier: 2 });
    // Pulso 7 (and its +10 critical), four promotions of veterancy and Ojo de halcón 2/2.
    expect(member.mods.aim).toBe(50 + 3 * 7 - 70 + 4 * VETERAN_AIM + 10);
    expect(member.mods.crit).toBe(10);
    expect(member.mods.damage).toBe(2);
    // Aguante 5 and plate armour.
    expect(member.maxHp).toBe(5 + 2);
    expect(member.perks).toEqual(['hawkEye']);

    const mission = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(1), squad: [member] });
    const shooter = mission.units.find((u) => u.campaignId === sniper.id)!;
    // a3 belongs to the central pod, standing in the open.
    const target = mission.units.find((u) => u.id === 'a3')!;
    shooter.pos = { x: target.pos.x, y: target.pos.y + 5 };
    const shot = previewShot(mission, shooter, target);
    expect(shot?.mods[0]).toEqual({ label: 'Puntería', value: 70 + member.mods.aim });
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
    const mine = c.soldiers.filter((s) => s.owner === 0);
    (['assault', 'grenadier', 'sharpshooter'] as const).forEach((cls, i) => specialize(mine[i]!, cls));
    const veteran = mine[0]!;
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

  it('in the hangar each commander has a half, and nobody moves when others come and go', () => {
    const c = createCampaign(both, new Rng(2));
    run(c, { type: 'chooseMission', id: c.offers[0]!.id }, 0);
    run(c, { type: 'vote', accept: true }, 1);
    const owner = (id: string | null) => c.soldiers.find((s) => s.id === id)?.owner;
    const before = squadPads(c);
    expect(before.map(owner)).toEqual([0, 0, 0, 1, 1, 1]);
    // The first commander takes out their second soldier: that pad is left free, the rest stay put.
    run(c, { type: 'squad', soldier: before[1]!, on: false }, 0);
    expect(squadPads(c)).toEqual([before[0], null, before[2], before[3], before[4], before[5]]);
    // The partner swaps one of theirs: the newcomer takes the pad that was freed in their half.
    const spare = c.soldiers.find((s) => s.owner === 1 && !c.squad.includes(s.id))!;
    run(c, { type: 'squad', soldier: before[4]!, on: false }, 1);
    run(c, { type: 'squad', soldier: spare.id, on: true }, 1);
    expect(squadPads(c)).toEqual([before[0], null, before[2], before[3], spare.id, before[5]]);
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

  it('a partner who arrives never takes the reserves the first commander needs: fresh recruits instead', () => {
    const c = createCampaign([0], new Rng(3));
    // After a hard mission only three soldiers are fit, and they have never fought.
    c.soldiers.forEach((s, i) => (s.woundedDays = i < 5 ? 6 : 0));
    const fit = c.soldiers.filter((s) => s.woundedDays === 0).map((s) => s.id);
    addPlayer(c, 1, new Rng(1));
    // The fit three stay; the wounded rookies may change hands.
    expect(c.soldiers.filter((s) => fit.includes(s.id)).every((s) => s.owner === 0)).toBe(true);
    expect(c.soldiers.filter((s) => s.owner === 1)).toHaveLength(LATE_KIT);
    expect(c.log.some((l) => /Llega el segundo comandante/.test(l.text))).toBe(true);
    // Three fit rookies are all the first commander has (the veterans are wounded): they stay, and the partner gets recruits.
    const d = createCampaign([0], new Rng(3));
    d.soldiers.slice(3).forEach((s) => ((s.missions = 1), (s.woundedDays = 6)));
    addPlayer(d, 1, new Rng(1));
    expect(d.soldiers.slice(0, 8).every((s) => s.owner === 0)).toBe(true);
    expect(d.soldiers.filter((s) => s.owner === 1)).toHaveLength(LATE_KIT);
    expect(d.log.some((l) => /Se alistan a sus órdenes/.test(l.text))).toBe(true);
  });

  it('what to do now: the most pressing thing first', () => {
    const c = createCampaign(both, new Rng(1));
    expect(nextSteps(c, 0)[0]).toMatchObject({ view: 'geoscape', label: 'Elige una operación', count: c.offers.length });
    const expiring = c.offers.find((o) => o.expiresDay !== null)!;
    expiring.expiresDay = c.day + 1;
    expect(nextSteps(c, 0)[0]).toMatchObject({ tone: 'urgent', label: `${expiring.name} caduca en 1 día`, go: { offer: expiring.id } });
    const promoted = c.soldiers.find((s) => s.owner === 1)!;
    promoted.rank = 1;
    expect(nextSteps(c, 1).some((x) => x.view === 'barracks' && x.go?.soldier === promoted.id)).toBe(true);
    expect(nextSteps(c, 0).some((x) => x.view === 'barracks' && x.label.startsWith('Ascenso'))).toBe(false);
    // Equipment only nags before a mission, and only for what can actually be fitted.
    expect(nextSteps(c, 0).some((x) => x.label.startsWith('Equipa'))).toBe(false);
    run(c, { type: 'chooseMission', id: c.offers[0]!.id }, 0, [0]);
    expect(nextSteps(c, 0)[0]).toMatchObject({ view: 'squad' });
    // Starting stock: 2 extra grenades and a medkit, all utility.
    expect(nextSteps(c, 0).find((x) => x.label.startsWith('Equipa'))?.count).toBe(3);
  });

  it('after a bad mission, too few ready soldiers puts recruiting before the next operation', () => {
    const c = createCampaign([0], new Rng(1));
    const mine = c.soldiers.filter((s) => s.owner === 0);
    for (const s of mine.slice(1)) s.woundedDays = 8;
    // The bunker is full: first a barracón for the recruits to sleep in.
    expect(nextSteps(c, 0)[0]).toMatchObject({ view: 'facilities', label: 'Solo 1 soldado listo y sin camas: construye un barracón' });
    c.base.plots.find((p) => p.id === '2,4')!.building = 'quarters';
    expect(nextSteps(c, 0)[0]).toMatchObject({ view: 'barracks', label: 'Solo 1 soldado listo: recluta (30 §)' });
    // An operation about to expire still comes first.
    c.offers.find((o) => o.expiresDay !== null)!.expiresDay = c.day + 1;
    expect(nextSteps(c, 0)[0]).toMatchObject({ tone: 'urgent' });
    expect(nextSteps(c, 0)[1]).toMatchObject({ view: 'barracks' });
    // Without the credits (or at the cap) there is nothing to do about it here.
    c.credits = 0;
    expect(nextSteps(c, 0).some((x) => x.label.includes('recluta'))).toBe(false);
  });
});
