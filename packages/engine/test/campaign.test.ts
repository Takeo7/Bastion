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
  squadLimit,
  squadMax,
  offerBriefing,
  type MissionOffer,
  levelUpBlocker,
  migrateCampaign,
  nextLevel,
  applyMissionResult,
  buildSquad,
  contactCost,
  createCampaign,
  nextSteps,
  soldierCap,
  squadPads,
  syncPads,
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
import { armorTier, bastionLevel, LEVEL_COST, LEVEL_MISSIONS, MAX_LEVEL, nextTierBlocker, weaponTier } from '../src/progression';
import { BASE_AIM, barsCost, missionLines, NO_GROWTH, NO_SKILLS, pointsLeft, randomBoard, totalBars, unlockBlocker, veteranWill } from '../src/skills';
import { BOARD_CELLS, type CellId } from '../src/board';
import { BASE_MAP, bastionLot, cellsOf, freshBase, onTrack, PIECES, placePiece, treeAt } from '../src/base';
import { applyEvent } from '../src/events';
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
    c.level = 2;
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
  it('goes by with the missions, and a day at a time waiting at the Bastión, both agreeing (Ana and Arturo, 22/10)', () => {
    const c = createCampaign(both, new Rng(1));
    c.nextEventDay = 999;
    // One alone waits for the other; a «ready» taken back changes nothing.
    run(c, { type: 'advance', ready: true }, 0);
    expect(c.day).toBe(1);
    run(c, { type: 'advance', ready: false }, 0);
    run(c, { type: 'advance', ready: true }, 1);
    expect(c.day).toBe(1);
    // Both: a day goes by, and what it ate is in the log.
    const food = c.food!;
    run(c, { type: 'advance', ready: true }, 0);
    expect(c.day).toBe(2);
    expect(c.advanceReady).toEqual([false, false]);
    expect(c.food).toBeLessThan(food);
    expect(c.log.some((l) => l.text.startsWith('Día 2: se comen'))).toBe(true);
    const offer = c.offers.find((o) => !o.story)!;
    run(c, { type: 'chooseMission', id: offer.id }, 0, [0]);
    const mission = createMission({ map: offer.map, slots: both, rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    mission.outcome = 'victory';
    applyMissionResult(c, mission, new Rng(4));
    expect(c.day).toBe(2 + DAYS_PER_MISSION);
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
  /** Two commanders with a squad of six (La Lata a furgoneta: 18/10 #45), three each. */
  function ready() {
    const c = createCampaign(both, new Rng(2));
    c.vehicle = 1;
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

  it('each their own part: a part the other leaves empty stays empty, and nobody touches the other\'s (Arturo, 18/10 #43)', () => {
    const c = createCampaign(both, new Rng(2));
    c.vehicle = 1;
    const theirs = c.soldiers.filter((s) => s.owner === 1);
    // Player 1 has a single soldier fit to fight.
    for (const s of theirs.slice(1)) s.woundedDays = 5;
    run(c, { type: 'chooseMission', id: c.offers[0]!.id }, 0, both);
    run(c, { type: 'vote', accept: true }, 1, both);
    const count = (slot: Slot) => c.squad.filter((id) => c.soldiers.find((s) => s.id === id)!.owner === slot).length;
    expect([count(0), count(1)]).toEqual([3, 1]);
    const out = c.soldiers.find((s) => s.owner === 0 && !c.squad.includes(s.id) && s.woundedDays === 0)!;
    expect(() => run(c, { type: 'squad', soldier: out.id, on: true }, 0, both)).toThrow('Máximo 3 soldados por jugador.');
    // The other's soldiers are theirs, here or not.
    expect(() => run(c, { type: 'squad', soldier: theirs[0]!.id, on: false }, 0, both)).toThrow('Solo puedes elegir a tus soldados.');
    expect(() => run(c, { type: 'squad', soldier: theirs[0]!.id, on: false }, 0, [0])).toThrow('Solo puedes elegir a tus soldados.');
    // Alone in the room, one fills La Lata with their own.
    run(c, { type: 'squad', soldier: out.id, on: true }, 0, [0]);
    expect(count(0)).toBe(4);
  });

  it('La Lata takes as many as its vehicle: 4, 6, 7 and 8, half each when both are here (18/10 #45)', () => {
    const c = createCampaign(both, new Rng(2));
    expect(c.vehicle).toBe(0);
    expect([0, 1, 2, 3].map((v) => ((c.vehicle = v), [squadMax(c), squadLimit(c, both), squadLimit(c, [0])]))).toEqual([
      [4, 2, 4],
      [6, 3, 6],
      [7, 4, 7],
      [8, 4, 8],
    ]);
  });

  it('a better vehicle turns up now and then; won, La Lata is it (18/10 #45)', () => {
    let found: MissionOffer | undefined;
    let c = createCampaign([0], new Rng(1));
    for (let seed = 1; seed <= 40 && !found; seed++) {
      c = createCampaign([0], new Rng(seed));
      Object.assign(c, { nextEventDay: 999, nextRumorDay: 999, missionsFlown: 2, offers: [] });
      for (let i = 0; i < 12 && !found; i++) {
        advanceTime(c, new Rng(seed * 100 + i));
        c.event = null;
        found = c.offers.find((o) => o.reward.vehicle);
        // Room for more operations.
        if (!found) c.offers = [];
      }
    }
    expect(found).toMatchObject({ kind: 'elimination', reward: { vehicle: 1 } });
    expect(found!.name).toMatch(/^La furgoneta /);
    expect(offerBriefing(c, found!)).toMatch(/una furgoneta/);
    // Not before its missions, and one at a time.
    const early = createCampaign([0], new Rng(1));
    Object.assign(early, { nextEventDay: 999, nextRumorDay: 999, offers: [] });
    for (let i = 0; i < 30; i++) {
      advanceTime(early, new Rng(i));
      early.event = null;
      expect(early.offers.some((o) => o.reward.vehicle)).toBe(false);
      early.offers = [];
    }
    // Won: La Lata grows, and the squad with it.
    run(c, { type: 'chooseMission', id: found!.id }, 0, [0]);
    const s = createMission({ map: found!.map, slots: [0], rng: new Rng(3), kind: found!.kind, pods: found!.pods });
    s.outcome = 'victory';
    applyMissionResult(c, s, new Rng(4));
    expect(c.vehicle).toBe(1);
    expect(squadMax(c)).toBe(6);
    expect(c.log.some((l) => l.text === 'La Lata ya es una furgoneta: caben seis.')).toBe(true);
  });

  it('a campaign from before 22 keeps its six seats, and what stood on the Bastion\'s lot comes down with its cost back', () => {
    const c = createCampaign([0], new Rng(1));
    const lot = bastionLot(c.bastion)!;
    const old = { ...JSON.parse(JSON.stringify(c)), version: 21 } as CampaignState;
    delete old.vehicle;
    const credits = old.credits;
    placePiece(old.bastion, 'sandbags', lot.x, lot.y, 0);
    const now = migrateCampaign(old);
    expect(now.vehicle).toBe(1);
    expect(now.bastion.pieces.map((p) => p.piece)).toEqual(['caravan']);
    expect(now.credits).toBe(credits + PIECES.sandbags.cost.supplies);
    expect(now.bastion.level).toBe(1);
  });

  it('a base built on the map of 40 grows to 56: in the middle, at the size it was built, pines round it (docs/MAPAS.md phase 6)', () => {
    const c = createCampaign(both, new Rng(6));
    const old = { ...JSON.parse(JSON.stringify(c)), version: 22 } as CampaignState;
    old.bastion = freshBase(new Rng(6), 40);
    const caravan = old.bastion.pieces[0]!;
    // A barracón as it was built then (4 × 3), and sandbags where the Bastion's lot now reaches.
    const hut = { id: 'b9', piece: 'quarters' as const, x: caravan.x + 9, y: caravan.y, rot: 0, hp: 30, level: 1 };
    const bags = { id: 'b10', piece: 'sandbags' as const, x: caravan.x - 2, y: caravan.y + 1, rot: 0, hp: 15 };
    old.bastion.pieces.push(hut, bags);
    old.bastion.trees = old.bastion.trees.filter((i) => ![...Array(4).keys()].some((dx) => [0, 1, 2].some((dy) => i === (hut.y + dy) * 40 + hut.x + dx)));
    old.siegePosts = { s1: { x: 5, y: 6 } };
    const credits = old.credits;
    const now = migrateCampaign(old);
    const base = now.bastion;
    expect([base.width, base.height]).toEqual([BASE_MAP, BASE_MAP]);
    const kept = base.pieces.find((p) => p.id === 'b9')!;
    expect([kept.x, kept.y, kept.size]).toEqual([hut.x + 8, hut.y + 8, [4, 3]]);
    expect(cellsOf(kept)).toHaveLength(12);
    // What stood on the bigger lot comes down, all its cost back.
    expect(base.pieces.some((p) => p.id === 'b10')).toBe(false);
    expect(now.credits).toBe(credits + PIECES.sandbags.cost.supplies);
    expect(now.siegePosts).toEqual({ s1: { x: 13, y: 14 } });
    // Pines round the old ground, none on what is built nor on the tracks; the caravan still in the middle.
    expect(base.trees.filter((i) => i % BASE_MAP < 8).length).toBeGreaterThan(100);
    for (const p of base.pieces) for (const t of cellsOf(p)) expect(treeAt(base, t.x, t.y)).toBe(false);
    for (const i of base.trees) expect(onTrack(BASE_MAP, BASE_MAP, i % BASE_MAP, Math.floor(i / BASE_MAP))).toBe(false);
    const lot = bastionLot(base)!;
    expect(Math.abs(lot.x + lot.w / 2 - BASE_MAP / 2)).toBeLessThan(4);
    // Raised, it takes its size if it fits where it stands.
    now.credits = 1000;
    now.level = 3;
    for (let y = kept.y; y < kept.y + 6; y++) for (let x = kept.x; x < kept.x + 6; x++) base.trees = base.trees.filter((i) => i !== y * BASE_MAP + x);
    run(now, { type: 'raise', piece: kept.id }, 0, [0]);
    expect(kept.size).toBeUndefined();
    expect(cellsOf(kept)).toHaveLength(30);
  });

  it('a fallen soldier leaves its backpack, weapons and whole belt where it fell; brought home, its weapon carries on (18/10 #49)', () => {
    const { c, offer } = ready();
    const dead = c.soldiers.find((s) => c.squad.includes(s.id))!;
    c.foundWeapons = [...(c.foundWeapons ?? []), { id: 'w99', kind: 'assaultRifle', holder: dead.id }];
    dead.loadout.utility = 'firstAid';
    const s = createMission({ map: offer.map, slots: both, rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    const u = s.units.find((x) => x.campaignId === dead.id)!;
    u.backpack = [{ supplies: 5 }];
    applyEvent(s, { t: 'died', unit: u.id });
    const left = s.dropped!.filter((d) => d.from === u.id);
    expect(left.every((d) => d.pos.x === u.pos.x && d.pos.y === u.pos.y)).toBe(true);
    expect(left.map((d) => d.loot)).toEqual([{ supplies: 5 }, expect.objectContaining({ weapon: 'assaultRifle', weaponId: 'w99' }), { item: 'firstAid' }]);
    expect(u.backpack).toEqual([]);
    // Another picks the rifle up and comes home with it: it stays in the Armería, theirs.
    const other = s.units.find((x) => x.team === 'xcom' && x.alive && x.campaignId)!;
    applyEvent(s, { t: 'lootTaken', unit: other.id, id: left[1]!.id });
    s.outcome = 'victory';
    applyMissionResult(c, s, new Rng(1));
    expect(c.foundWeapons!.find((w) => w.id === 'w99')).toBeDefined();
    expect(c.log.some((l) => /se pierde su fusil/.test(l.text))).toBe(false);
  });

  it('skills are only for their own commander, the other here or not (18/10 #42)', () => {
    const c = createCampaign(both, new Rng(2));
    const theirs = c.soldiers.find((s) => s.owner === 1 && pointsLeft(s.skills, s.board) > 0)!;
    const cell = Object.keys(BOARD_CELLS).find((id) => unlockBlocker(theirs.skills, theirs.board, id as CellId) === null)! as CellId;
    for (const connected of [both, [0] as Slot[]]) {
      expect(() => run(c, { type: 'unlock', soldier: theirs.id, cell }, 0, connected)).toThrow('Solo puedes elegir las habilidades de tus soldados.');
    }
    run(c, { type: 'unlock', soldier: theirs.id, cell }, 1, both);
    expect(theirs.board).toEqual([cell]);
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
    expect(c.log.some((l) => /Primera salida: .* La escuadra espera junto a La Lata/.test(l.text))).toBe(true);
    expect(c.log.some((l) => /Elegid la primera misión/.test(l.text))).toBe(false);
    expect(nextSteps(c, 0)[0]?.label).not.toBe('Elige una misión');
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
    // Not about the sieges: the first one far off (it comes straight after the first mission: Ana, 22/10).
    c.siege.in = 99;
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
    expect(report.squad.find((e) => e.id === soldiers[1]!.campaignId)).toMatchObject({ status: 'wounded', woundedDays: 2 });
    // Eighteen of Disparo, all of them (no cap: Arturo, 16/10).
    expect(report.squad.find((e) => e.id === soldiers[2]!.campaignId)).toMatchObject({ kills: 6, learned: { ...NO_SKILLS, shooting: 18, medicine: 2 }, deeds: { shot: 7, hit: 5, gunKill: 6, healOther: 1 } });
    expect(c.soldiers.find((s) => s.id === soldiers[0]!.campaignId)!.alive).toBe(false);
    // The report says how bad it was, and a mission takes no days (docs/PRUEBA-2026-10-14.md P7): it is all still to heal.
    expect(c.soldiers.find((s) => s.id === soldiers[1]!.campaignId)!.woundedDays).toBe(2);
    expect(c.day).toBe(report.day);
    expect(veteran.skills).toEqual({ ...before, shooting: before.shooting + 18, medicine: before.medicine + 2 });
    expect(c.credits).toBe(credits + (offer.reward.credits ?? 0) + report.salvage);
    // The Bastion's level is bought now (18/10 #40): a mission brings it no experience.
    expect(report.xp).toBe(0);
    expect(c.xp).toBe(0);
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
    c.level = 4;
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
    c.level = 6;
    c.crafted = ['armor2'];
    expect(weaponTier(c, s)).toBe(1);
    expect(nextTierBlocker(c, s, 'weapon')).toBeNull();
    expect(armorTier(c, s)).toBe(2);
    expect(nextTierBlocker(c, s, 'armor')).toMatch(/requiere la casa y/);
  });

  it('the Bastion goes up a level when it is bought, after enough missions (Arturo, 18/10 #40)', () => {
    const c = createCampaign([0, 1], new Rng(1));
    expect(bastionLevel(c)).toBe(1);
    expect(nextLevel(c)).toEqual({ level: 2, cost: LEVEL_COST[1], missions: LEVEL_MISSIONS[1], flown: 0 });
    c.credits = 5000;
    expect(() => run(c, { type: 'levelUp' }, 0, [0, 1])).toThrow(`Para la caravana con toldo hacen falta ${LEVEL_MISSIONS[1]} misiones hechas: lleváis 0.`);
    c.missionsFlown = LEVEL_MISSIONS[1];
    // Always together: a proposal, paid when the partner says yes.
    run(c, { type: 'levelUp' }, 0, [0, 1]);
    expect(c.proposal?.text).toBe('subir el Bastión a la caravana con toldo');
    run(c, { type: 'vote', accept: true }, 1, [0, 1]);
    expect(bastionLevel(c)).toBe(2);
    expect(c.credits).toBe(5000 - LEVEL_COST[1]!);
    expect(c.log.some((l) => l.text === 'El Bastión ya es una caravana con toldo.')).toBe(true);
    // Short of suministros, it waits.
    c.missionsFlown = 99;
    c.credits = LEVEL_COST[2]! - 1;
    expect(levelUpBlocker(c)).toMatch(/suministros/);
    c.level = MAX_LEVEL;
    expect(nextLevel(c)).toBeNull();
    expect(levelUpBlocker(c)).toBe('El Bastión ya está en su nivel más alto.');
  });

  it('a campaign from before keeps its level, and its operations stop expiring', () => {
    const c = createCampaign([0], new Rng(1));
    const old = { ...JSON.parse(JSON.stringify(c)), version: 20, xp: 55 } as CampaignState;
    delete old.level;
    old.offers[0]!.expiresDay = old.day + 4;
    const now = migrateCampaign(old);
    expect(bastionLevel(now)).toBe(4);
    expect(now.offers.every((o) => o.expiresDay === null)).toBe(true);
  });

  it('buying the top level with every story chapter won reveals the final mission', () => {
    const c = createCampaign([0], new Rng(1));
    c.story.done = 7;
    c.level = MAX_LEVEL - 1;
    Object.assign(c, { credits: 5000, missionsFlown: 99 });
    run(c, { type: 'levelUp' }, 0, [0]);
    expect(bastionLevel(c)).toBe(MAX_LEVEL);
    expect(c.offers.some((o) => o.final)).toBe(true);
  });
});

describe('helping the players', () => {
  it('a chosen mission comes with its squad: the most seasoned ready soldiers first', () => {
    const c = createCampaign(both, new Rng(2));
    c.vehicle = 1;
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
    c.vehicle = 1;
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

  it("a pad kept on the other one's half is let go: each stands among their own (Ana, 23/10 #19)", () => {
    const owner = (c: CampaignState) => (id: string | null) => c.soldiers.find((s) => s.id === id)?.owner;
    // A squad picked across both halves (the partner away) stays where it stood, then each goes to their own half.
    const c = createCampaign(both, new Rng(2));
    c.vehicle = 1;
    run(c, { type: 'chooseMission', id: c.offers[0]!.id }, 0);
    run(c, { type: 'vote', accept: true }, 1);
    const ids = squadPads(c) as string[];
    c.pads = Object.fromEntries(ids.map((id, i) => [id, (i + 3) % 6]));
    expect(squadPads(c).map(owner(c))).toEqual([0, 0, 0, 1, 1, 1]);
    // Alone, the whole hangar; then a partner arrives, takes over rookies, and the squad is picked again.
    const d = createCampaign([0], new Rng(3));
    d.vehicle = 1;
    run(d, { type: 'chooseMission', id: d.offers[0]!.id }, 0, [0]);
    syncPads(d);
    expect(squadPads(d).every((id) => owner(d)(id) === 0)).toBe(true);
    addPlayer(d, 1, new Rng(1));
    // Half each (more than half, and the rest would stand on the other half, as ever).
    for (const id of d.squad.filter((x) => owner(d)(x) === 0).slice(3)) run(d, { type: 'squad', soldier: id, on: false }, 0);
    for (const s of d.soldiers.filter((x) => x.owner === 1 && !d.squad.includes(x.id)).slice(0, 2)) run(d, { type: 'squad', soldier: s.id, on: true }, 1);
    const pads = squadPads(d);
    pads.forEach((id, i) => {
      if (id) expect(owner(d)(id), `pad ${i}`).toBe(i < 3 ? 0 : 1);
    });
    expect(pads.filter((id) => owner(d)(id) === 1)).toHaveLength(2);
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
    // Not about the sieges: the first one far off (it comes straight after the first mission: Ana, 22/10).
    c.siege.in = 99;
    expect(nextSteps(c, 0)[0]).toMatchObject({ view: 'geoscape', label: 'Elige una misión', count: c.offers.length });
    // Operations don't expire (18/10 #39).
    expect(c.offers.every((o) => o.expiresDay === null)).toBe(true);
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
    // Not about the sieges: the first one far off (it comes straight after the first mission: Ana, 22/10).
    c.siege.in = 99;
    const mine = c.soldiers.filter((s) => s.owner === 0);
    for (const s of mine.slice(1)) s.woundedDays = 8;
    // The caravan is full: first a barracón for the recruits to sleep in.
    expect(nextSteps(c, 0)[0]).toMatchObject({ view: 'facilities', label: 'Solo 1 soldado listo y sin camas: construye un barracón' });
    raise(c, 'quarters');
    expect(nextSteps(c, 0)[0]).toMatchObject({ view: 'barracks', label: 'Solo 1 soldado listo: recluta (30 §)' });
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
