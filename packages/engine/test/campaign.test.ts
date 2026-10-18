import { describe, expect, it } from 'vitest';
import { previewShot } from '../src/aim';
import { defaultAppearance } from '../src/appearance';
import {
  addPlayer,
  advanceTime,
  DAYS_PER_MISSION,
  canSpendPoints,
  craftBlocker,
  defaultSquad,
  learningBonus,
  openingMission,
  LATE_KIT,
  applyMissionResult,
  buildSquad,
  contactCost,
  createCampaign,
  nextSteps,
  soldierCap,
  squadPads,
  veteranEnemies,
  DOOM_MAX,
  executeCampaignCommand,
  squadMember,
  type CampaignCommand,
  type CampaignState,
  missionInfo,
  THAW_AFTER_STORY,
} from '../src/campaign';
import { abilitiesOf } from '../src/abilities';
import { armorTier, bastionLevel, LEVEL_XP, MAX_LEVEL, missionXp, nextTierBlocker, weaponTier } from '../src/progression';
import { BASE_AIM, barsCost, missionLines, NO_GROWTH, NO_SKILLS, randomBoard, totalBars, veteranWill } from '../src/skills';
import { Rng } from '../src/rng';
import { createMission } from '../src/setup';
import type { PerkId, Slot } from '../src/types';
import { freeSpot, raise } from './helpers';

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
    const vega = c.regions.find((r) => r.id === 'oce')!;
    run(c, { type: 'contact', region: 'oce' }, 0);
    expect(c.proposal?.by).toBe(0);
    expect(vega.contacted).toBe(false);
    expect(executeCampaignCommand(c, { type: 'vote', accept: true }, { slot: 0, connected: both, rng: new Rng(1) }).ok).toBe(false);
    run(c, { type: 'vote', accept: true }, 1);
    expect(vega.contacted).toBe(true);
    expect(c.credits).toBe(credits - cost);
    expect(c.proposal).toBeNull();
  });

  it('a rejected proposal changes nothing; alone, decisions apply directly', () => {
    // The Sala de radio is dear: it goes to a vote (docs/FASE12.md §11.7).
    const c = createCampaign(both, new Rng(1));
    const spot = freeSpot(c, 'radio');
    run(c, { type: 'place', piece: 'radio', x: spot.x, y: spot.y, rot: 0 }, 1);
    expect(c.proposal?.action).toEqual({ type: 'place', piece: 'radio', x: spot.x, y: spot.y, rot: 0 });
    run(c, { type: 'vote', accept: false }, 0);
    expect(c.bastion.pieces.some((p) => p.piece === 'radio')).toBe(false);
    run(c, { type: 'place', piece: 'radio', x: spot.x, y: spot.y, rot: 0 }, 1, [1]);
    expect(c.bastion.pieces.some((p) => p.piece === 'radio')).toBe(true);
  });
});

describe('time', () => {
  it('goes by with the missions: no waiting for news, and a «ready» taken back changes nothing', () => {
    const c = createCampaign(both, new Rng(1));
    expect(executeCampaignCommand(c, { type: 'advance', ready: true }, { slot: 0, connected: both, rng: new Rng(1) })).toEqual({ ok: false, error: 'Ya no se espera: el tiempo pasa con cada misión.' });
    run(c, { type: 'advance', ready: false }, 0);
    expect(c.day).toBe(1);
    const offer = c.offers.find((o) => !o.story)!;
    run(c, { type: 'chooseMission', id: offer.id }, 0, [0]);
    const mission = createMission({ map: offer.map, slots: both, rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    mission.outcome = 'victory';
    applyMissionResult(c, mission, new Rng(4));
    expect(c.day).toBe(1 + DAYS_PER_MISSION);
  });

  it('wounds heal day by day; the enemy project ends the campaign at its maximum', () => {
    const c = createCampaign([0], new Rng(1));
    c.nextEventDay = 999;
    c.soldiers[0]!.woundedDays = 2;
    advanceTime(c, new Rng(7));
    expect(c.soldiers[0]!.woundedDays).toBeLessThan(2);
    c.doom = DOOM_MAX - 1;
    for (let i = 0; i < 10 && !c.outcome; i++) advanceTime(c, new Rng(7 + i));
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
    // The caravan sleeps the eight starting soldiers: recruiting needs a barracón.
    raise(c, 'quarters');
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

  // Playtest 2026-10-13: a new campaign opens with a mission straight away.
  it('a new campaign opens with the first story operation chosen and the squad in the hangar', () => {
    const plain = createCampaign(both, new Rng(1));
    // On its own the campaign chooses nothing (older saves, tests): the server opens it.
    expect(plain.activeMission).toBeNull();
    const c = createCampaign(both, new Rng(1));
    openingMission(c);
    const first = c.offers.find((o) => o.story === 1)!;
    expect(first).toBeDefined();
    expect(c.activeMission).toBe(first.id);
    expect(c.squad).toEqual(defaultSquad(c, both));
    expect(c.squad.length).toBeGreaterThan(0);
    expect(c.launchReady).toEqual([false, false]);
    expect(Object.keys(c.pads ?? {}).sort()).toEqual([...c.squad].sort());
    expect(c.log.some((l) => /Primera salida: .* La escuadra espera en la cochera/.test(l.text))).toBe(true);
    expect(c.log.some((l) => /Elegid la primera misión/.test(l.text))).toBe(false);
    expect(nextSteps(c, 0)[0]?.label).not.toBe('Elige una operación');
    // Nothing else changes: the same offers, soldiers and day as a campaign opened by hand.
    expect(c.offers).toEqual(plain.offers);
    expect(c.soldiers).toEqual(plain.soldiers);
    expect(c.day).toBe(plain.day);
    // Ready to go: both press launch.
    expect(run(c, { type: 'launch', ready: true }, 0).launch).toBeUndefined();
    expect(run(c, { type: 'launch', ready: true }, 1).launch).toBe(true);
    // A campaign that already has a mission chosen is left as it is.
    const other = c.offers.find((o) => o.id !== first.id)!;
    const later = createCampaign(both, new Rng(1));
    later.activeMission = other.id;
    openingMission(later);
    expect(later.activeMission).toBe(other.id);
  });

  it('the opening mission can be swapped for another; choosing the same one again is refused', () => {
    const c = createCampaign(both, new Rng(1));
    openingMission(c);
    const first = c.activeMission!;
    const other = c.offers.find((o) => o.id !== first)!;
    expect(() => run(c, { type: 'chooseMission', id: first }, 0)).toThrow('Esa misión ya está elegida.');
    run(c, { type: 'chooseMission', id: other.id }, 0);
    run(c, { type: 'vote', accept: true }, 1);
    expect(c.activeMission).toBe(other.id);
  });

  it('launches only when both are ready', () => {
    const { c } = ready();
    expect(run(c, { type: 'launch', ready: true }, 0).launch).toBeUndefined();
    expect(run(c, { type: 'launch', ready: true }, 1).launch).toBe(true);
  });

  it('results come back: deaths, wounds, what each one learnt and rewards', () => {
    const { c, offer } = ready();
    const squad = buildSquad(c);
    const mission = createMission({ map: offer.map, slots: both, rng: new Rng(3), kind: offer.kind, squad, pods: offer.pods, difficulty: offer.difficulty });
    const soldiers = mission.units.filter((u) => u.team === 'xcom');
    soldiers[0]!.alive = false;
    soldiers[0]!.hp = 0;
    soldiers[0]!.deeds = { hit: 4 };
    soldiers[1]!.hp = soldiers[1]!.maxHp - 2;
    soldiers[2]!.kills = 6;
    soldiers[2]!.deeds = { shot: 7, hit: 5, gunKill: 6, healOther: 1 };
    mission.outcome = 'victory';
    mission.outcomeReason = 'objective';
    const credits = c.credits;
    const veteran = c.soldiers.find((s) => s.id === soldiers[2]!.campaignId)!;
    const before = { ...veteran.skills };
    const report = applyMissionResult(c, mission, new Rng(4));
    expect(report.victory).toBe(true);
    expect(report.squad).toHaveLength(6);
    expect(report.squad.find((e) => e.id === soldiers[0]!.campaignId)?.status).toBe('dead');
    expect(report.squad.find((e) => e.id === soldiers[1]!.campaignId)).toMatchObject({ status: 'wounded', woundedDays: 6 });
    // Eighteen of Disparo, all of them (no cap: Arturo, 16/10).
    expect(report.squad.find((e) => e.id === soldiers[2]!.campaignId)).toMatchObject({ kills: 6, learned: { ...NO_SKILLS, shooting: 18, medicine: 2 }, deeds: { shot: 7, hit: 5, gunKill: 6, healOther: 1 } });
    expect(c.soldiers.find((s) => s.id === soldiers[0]!.campaignId)!.alive).toBe(false);
    // The report says how bad it was, and a mission takes no days (docs/PRUEBA-2026-10-14.md P7): it is all still to heal.
    expect(c.soldiers.find((s) => s.id === soldiers[1]!.campaignId)!.woundedDays).toBe(6);
    expect(c.day).toBe(report.day);
    expect(veteran.skills).toEqual({ ...before, shooting: before.shooting + 18, medicine: before.medicine + 2 });
    expect(c.credits).toBe(credits + (offer.reward.credits ?? 0) + report.salvage);
    // The Bastion learns from every mission: showing up, winning and the kills.
    expect(report.xp).toBe(missionXp(true, offer.difficulty, 6));
    expect(c.xp).toBe(report.xp);
    expect(c.offers.some((o) => o.id === offer.id)).toBe(false);
    expect(c.activeMission).toBeNull();
  });

  it('nobody has a class: every recruit comes with a bar or two, and a bar filled is told with what it added (docs/FASE10.md)', () => {
    const { c, offer } = ready();
    expect(c.soldiers.every((s) => s.board.length === 0 && !('cls' in s))).toBe(true);
    expect(c.soldiers.every((s) => totalBars(s.skills) >= 1 && totalBars(s.skills) <= 2)).toBe(true);
    const mission = createMission({ map: offer.map, slots: both, rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    const unit = mission.units.find((u) => u.team === 'xcom')!;
    const s = c.soldiers.find((x) => x.id === unit.campaignId)!;
    s.skills = { ...NO_SKILLS, shooting: 22 };
    s.growth = { ...NO_GROWTH, shooting: 7 };
    unit.lines = missionLines(s.skills, s.growth);
    unit.deeds = { shot: 2, hit: 1 };
    mission.outcome = 'victory';
    mission.outcomeReason = 'objective';
    const report = applyMissionResult(c, mission, new Rng(4));
    const bar = report.squad.find((e) => e.id === s.id)!.bars!;
    expect(bar).toEqual([{ skill: 'shooting', filled: 2, growth: [s.growth.shooting - 7] }]);
    expect(c.log.some((l) => l.text === `${s.name} llena una barra de Disparo: +${s.growth.shooting - 7} % de puntería con armas de fuego. Tiene un punto de habilidad para el tablero.`)).toBe(true);
    // Two points: a cell of the first ring, then one of the second beside it (Disparo has the bar it asks for).
    expect(canSpendPoints(c, s)).toBe(true);
    run(c, { type: 'unlock', soldier: s.id, cell: 'steadyHand' }, s.owner);
    run(c, { type: 'unlock', soldier: s.id, cell: 'lightTrigger' }, s.owner);
    expect(s.board).toEqual(['steadyHand', 'lightTrigger']);
    expect(c.log[0]!.text).toBe(`${s.name} aprende Dedo ligero.`);
    expect(canSpendPoints(c, s)).toBe(false);
    expect(() => run(c, { type: 'unlock', soldier: s.id, cell: 'sidearm' }, s.owner)).toThrow(/No le quedan puntos/);
    expect(() => run(c, { type: 'unlock', soldier: s.id, cell: 'killZone' }, s.owner)).toThrow(/al lado/);
  });

  it('it is winter until the story says so: the missions go under snow (playtest 16/10)', () => {
    const { c, offer } = ready();
    expect(missionInfo(c, offer).snow).toBe(true);
    c.story.done = THAW_AFTER_STORY;
    expect(missionInfo(c, offer).snow).toBeUndefined();
    expect(createMission({ map: offer.map, slots: both, rng: new Rng(3), kind: offer.kind, snow: true }).snow).toBe(true);
  });

  it('the Taller de oficios makes a mission teach 20 % more for every level', () => {
    const { c, offer } = ready();
    expect(learningBonus(c)).toBe(0);
    raise(c, 'echoes', 2);
    expect(learningBonus(c)).toBeCloseTo(0.4);
    const mission = createMission({ map: offer.map, slots: both, rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    const unit = mission.units.find((u) => u.team === 'xcom')!;
    unit.deeds = { shot: 12 };
    mission.outcome = 'victory';
    const report = applyMissionResult(c, mission, new Rng(4));
    expect(report.squad.find((e) => e.id === unit.campaignId)!.learned!.shooting).toBe(12 + 5);
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

  it('bars, what they added, the board, veterancy and the armour the Taller made reach the battlefield', () => {
    const c = createCampaign([0], new Rng(5));
    const s = c.soldiers[0]!;
    // Three bars of Disparo and two of Cuerpo a cuerpo: five points; four spent, still Novato.
    s.skills = { ...NO_SKILLS, shooting: barsCost(3), melee: barsCost(2) };
    s.growth = { ...NO_GROWTH, shooting: 25, melee: 20 };
    for (const cell of ['steadyHand', 'lightTrigger', 'aimedShot', 'grip']) run(c, { type: 'unlock', soldier: s.id, cell }, 0, [0]);
    // Plate armour made by the Taller (docs/FASE4.md): everyone wears it, whatever their veterancy.
    c.xp = LEVEL_XP[3]!;
    c.crafted = ['armor2'];
    const member = squadMember(c, s);
    expect(member).toMatchObject({ weaponTier: 1, armorTier: 2, rank: 0, perks: ['aimedShot'] });
    // 60 grown 25 % is 75, and +3 of Pulso firme; 60 grown 20 % is 72, and +3 of Buen agarre.
    expect(member.mods).toMatchObject({ gunAim: 18, gunCrit: 5, meleeAim: 15, damage: 0 });
    expect(member.maxHp).toBe(5 + 2);
    expect(member.will).toBe(veteranWill(0));

    const mission = createMission({ map: { kind: 'handmade', id: 'plaza' }, slots: [0], rng: new Rng(1), squad: [member] });
    const shooter = mission.units.find((u) => u.campaignId === s.id)!;
    // a3 belongs to the central pod, standing in the open.
    const target = mission.units.find((u) => u.id === 'a3')!;
    shooter.pos = { x: target.pos.x, y: target.pos.y + 5, z: 0 };
    const shot = previewShot(mission, shooter, target)!;
    expect(shot.mods[0]).toEqual({ label: 'Puntería', value: BASE_AIM });
    expect(shot.mods).toContainEqual({ label: 'Tiro', value: 18 });
    expect(abilitiesOf(shooter)).toContain('aimedShot');
    // Nobody carries grenades without them on the belt.
    expect(abilitiesOf(shooter)).not.toContain('grenade');
  });
});

describe('progression', () => {
  it('weapons have no tiers; the armour the Taller makes is for everyone (docs/FASE9.md §5)', () => {
    const c = createCampaign([0], new Rng(1));
    const s = { id: 'x' };
    c.xp = LEVEL_XP[5]!;
    c.crafted = ['armor2'];
    expect(weaponTier(c, s)).toBe(1);
    expect(nextTierBlocker(c, s, 'weapon')).toBeNull();
    expect(armorTier(c, s)).toBe(2);
    expect(nextTierBlocker(c, s, 'armor')).toMatch(/nivel 7 del Bastión/);
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
  it('a chosen mission comes with its squad: the most seasoned ready soldiers first', () => {
    const c = createCampaign(both, new Rng(2));
    const mine = c.soldiers.filter((s) => s.owner === 0);
    const veteran = mine[mine.length - 1]!;
    veteran.skills = { ...NO_SKILLS, shooting: barsCost(4), melee: barsCost(4) };
    c.soldiers.find((s) => s.owner === 1)!.woundedDays = 3;
    run(c, { type: 'chooseMission', id: c.offers[0]!.id }, 0, [0, 1]);
    run(c, { type: 'vote', accept: true }, 1, [0, 1]);
    const picked = c.squad.map((id) => c.soldiers.find((s) => s.id === id)!);
    expect(picked.filter((s) => s.owner === 0)).toHaveLength(3);
    expect(picked.filter((s) => s.owner === 1)).toHaveLength(3);
    expect(picked).toContain(veteran);
    expect(picked.every((s) => s.woundedDays === 0)).toBe(true);
    expect(picked[0]).toBe(veteran);
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
    // Every recruit comes with a point or two to spend.
    const promoted = c.soldiers.find((s) => s.owner === 1)!;
    expect(nextSteps(c, 1).some((x) => x.view === 'barracks' && x.go?.soldier === promoted.id)).toBe(true);
    for (const s of c.soldiers.filter((x) => x.owner === 0)) s.board = randomBoard(s.skills, new Rng(1));
    expect(nextSteps(c, 0).some((x) => x.view === 'barracks' && x.label.startsWith('Puntos'))).toBe(false);
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
    // The caravan is full: first a barracón for the recruits to sleep in.
    expect(nextSteps(c, 0)[0]).toMatchObject({ view: 'facilities', label: 'Solo 1 soldado listo y sin camas: construye un barracón' });
    raise(c, 'quarters');
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

// Fase 10: every bar filled is a point for the board; the Cuartel asks until they are spent.
describe('points to spend', () => {
  const asks = (c: CampaignState) => nextSteps(c, 0).filter((x) => /^Puntos por gastar/.test(x.label));

  it('a point asks to be spent, and stops once it is', () => {
    const c = createCampaign([0], new Rng(1));
    const mine = c.soldiers.filter((x) => x.owner === 0);
    const s = mine[0]!;
    for (const x of mine) x.skills = { ...NO_SKILLS };
    expect(canSpendPoints(c, s)).toBe(false);
    expect(asks(c)).toEqual([]);
    s.skills.medicine = barsCost(1);
    expect(asks(c)).toEqual([expect.objectContaining({ label: `Puntos por gastar: ${s.name}`, count: 1, go: { soldier: s.id } })]);
    run(c, { type: 'unlock', soldier: s.id, cell: 'bandage' }, 0, [0]);
    expect(asks(c)).toEqual([]);
    expect(() => run(c, { type: 'unlock', soldier: s.id, cell: 'calmVoice' }, 0, [0])).toThrow(/No le quedan puntos/);
  });
});
