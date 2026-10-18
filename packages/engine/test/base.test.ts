import { describe, expect, it } from 'vitest';
import {
  activeOffer,
  advanceTime,
  applyMissionResult,
  beds,
  bedsFree,
  buildSquad,
  CAMPAIGN_VERSION,
  contactCapacity,
  createCampaign,
  EVENTS,
  executeCampaignCommand,
  FACILITIES,
  facilityLevel,
  garageCut,
  hasRadio,
  healRate,
  itemsFree,
  listeningDelay,
  migrateCampaign,
  mustWait,
  nextSteps,
  offerBriefing,
  offerMap,
  proposalCost,
  raiseCost,
  recruitCost,
  roadRoute,
  shopPrice,
  SIEGE_EVERY,
  SIEGE_FIRST,
  SIEGE_FIRST_IN,
  SIEGE_GROWTH,
  siegeHeard,
  siegePlan,
  squadLimit,
  squadMember,
  upgradeCost,
  VOTE_ABOVE,
  type CampaignCommand,
  type CampaignState,
} from '../src/campaign';
import { PIECES, pieceCells, TREE_SUPPLIES } from '../src/base';
import { abilitiesOf } from '../src/abilities';
import { defaultAppearance } from '../src/appearance';
import { sexOfName } from '../src/content';
import { bastionLevel, LEVEL_XP, missionXp } from '../src/progression';
import { Rng } from '../src/rng';
import { totalBars } from '../src/skills';
import { createMission } from '../src/setup';
import { BASE_SIEGE_TEXT } from '../src/lore';
import type { Appearance, GameState, Slot } from '../src/types';
import { freeSpot, raise } from './helpers';

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

/** A mission flown to `outcome` (nothing else happening in it), and its report. */
function fly(c: CampaignState, outcome: 'victory' | 'defeat' = 'victory', edit?: (s: GameState) => void) {
  if (!c.activeMission) run(c, { type: 'chooseMission', id: c.offers.find((o) => !o.story)!.id });
  const offer = activeOffer(c)!;
  const plan = siegePlan(c, offer);
  const s = createMission({ map: offerMap(c, offer), ...(plan ? { siegePlan: plan } : {}), slots: c.players, rng: new Rng(2), kind: offer.kind, squad: buildSquad(c), pods: offer.pods });
  edit?.(s);
  s.outcome = outcome;
  s.outcomeReason = outcome === 'victory' ? 'objective' : 'wiped';
  return applyMissionResult(c, s, new Rng(1));
}

describe('the base on the map (docs/FASE12.md §11)', () => {
  it('a new campaign starts with only the caravan in a clearing, and the first siege a few missions off', () => {
    const c = createCampaign([0], new Rng(3));
    expect(c.version).toBe(CAMPAIGN_VERSION);
    expect(c.bastion.pieces.map((p) => p.piece)).toEqual(['caravan']);
    expect(c.base.plots).toEqual([]);
    expect(c.siege).toMatchObject({ in: SIEGE_FIRST_IN, count: SIEGE_FIRST, held: 0 });
    expect(c.siege.sides.length).toBeGreaterThanOrEqual(1);
    expect(c.siege.sides.length).toBeLessThanOrEqual(3);
    // Without a Sala de radio, only when.
    expect(siegeHeard(c)).toEqual({ in: SIEGE_FIRST_IN });
  });

  it('each one builds the cheap at once; the dear goes to a vote; the Bastion level opens what it opens', () => {
    const c = createCampaign(both, new Rng(3));
    c.credits = 500;
    const bags = freeSpot(c, 'sandbags');
    run(c, { type: 'place', piece: 'sandbags', x: bags.x, y: bags.y, rot: 0 }, 0, both);
    expect(c.bastion.pieces.some((p) => p.piece === 'sandbags')).toBe(true);
    expect(c.credits).toBe(500 - PIECES.sandbags.cost.supplies);
    expect(PIECES.radio.cost.supplies).toBeGreaterThan(VOTE_ABOVE);
    const radio = freeSpot(c, 'radio');
    run(c, { type: 'place', piece: 'radio', x: radio.x, y: radio.y, rot: 0 }, 0, both);
    expect(c.proposal?.text).toBe('construir una Sala de radio');
    expect(proposalCost(c, c.proposal!.action)).toBe(PIECES.radio.cost.supplies);
    run(c, { type: 'vote', accept: true }, 1, both);
    expect(hasRadio(c)).toBe(true);
    expect(siegeHeard(c)).toEqual({ in: c.siege.in, count: c.siege.count, sides: c.siege.sides });
    const wall = freeSpot(c, 'wall');
    expect(() => run(c, { type: 'place', piece: 'wall', x: wall.x, y: wall.y, rot: 0 }, 0, both)).toThrow(/nivel 2/);
    c.xp = LEVEL_XP[1]!;
    c.parts = 0;
    expect(() => run(c, { type: 'place', piece: 'wall', x: wall.x, y: wall.y, rot: 0 }, 0, both)).toThrow(/piezas/);
  });

  it('felling gives suministros; mending, setting again and taking down cost and give what they say', () => {
    const c = solo();
    const tree = c.bastion.trees[0]!;
    const credits = c.credits;
    run(c, { type: 'fell', tiles: [{ x: tree % c.bastion.width, y: Math.floor(tree / c.bastion.width) }] });
    expect(c.credits).toBe(credits + TREE_SUPPLIES);
    expect(c.bastion.trees).not.toContain(tree);
    const gate = raise(c, 'gate');
    expect(() => run(c, { type: 'mend', piece: gate.id })).toThrow(/No le falta nada/);
    gate.hp = 9;
    run(c, { type: 'mend', piece: gate.id });
    expect(gate.hp).toBe(PIECES.gate.hp);
    const mine = raise(c, 'mine');
    mine.charges = 0;
    c.parts = 1;
    run(c, { type: 'rearm', piece: mine.id });
    expect([mine.charges, c.parts]).toEqual([1, 0]);
    const before = c.credits;
    run(c, { type: 'dismantle', piece: gate.id });
    expect(c.credits).toBe(before + Math.floor(PIECES.gate.cost.supplies / 2));
    expect(() => run(c, { type: 'dismantle', piece: c.bastion.pieces[0]!.id })).toThrow(/caravana/);
    // The plots of before are gone.
    expect(() => run(c, { type: 'clear', plot: '3,5' })).toThrow(/base de antes/);
  });

  it('a building works by its facility, better next to its partner (the caravan is the Cuartel and the Armería); broken, it stops', () => {
    const c = solo();
    const far = raise(c, 'workshop');
    expect(shopPrice(c, 'smokeGrenade')).toBe(19);
    c.bastion.pieces = c.bastion.pieces.filter((p) => p !== far);
    const caravan = c.bastion.pieces[0]!;
    raise(c, 'workshop', 1, caravan);
    expect(shopPrice(c, 'smokeGrenade')).toBe(15);
    expect(recruitCost(c)).toBe(30);
    raise(c, 'training', 1, caravan);
    expect(recruitCost(c)).toBe(20);
    const infirmary = raise(c, 'infirmary');
    const rate = healRate(c);
    expect(rate).toBeGreaterThan(1);
    infirmary.hp = PIECES.infirmary.hp / 2;
    expect(healRate(c)).toBe(1);
  });

  it('the caravan sleeps the first eight; a barracón 2/3/4 beds, one more next to the caravan', () => {
    const c = solo();
    expect(beds(c)).toBe(8);
    expect(() => run(c, { type: 'recruit' })).toThrow(/camas/);
    const away = raise(c, 'quarters');
    const beside = c.bastion.pieces.some((p) => p.piece === 'caravan' && p.x <= away.x + 4 && away.x <= p.x + 4 && Math.abs(p.y - away.y) <= 3);
    expect(beds(c)).toBe(10 + (beside ? 1 : 0));
    away.level = 3;
    expect(beds(c)).toBe(12 + (beside ? 1 : 0));
    run(c, { type: 'recruit' });
    expect(bedsFree(c)).toBe(beds(c) - 9);
  });

  it('a building rises a level with its facility\'s cost and the Bastion; the Sala de radio rises into communications and listening', () => {
    const c = solo();
    const shop = raise(c, 'workshop');
    expect(() => run(c, { type: 'raise', piece: shop.id })).toThrow(/nivel 3/);
    c.xp = LEVEL_XP[2]!;
    const credits = c.credits;
    run(c, { type: 'raise', piece: shop.id });
    expect(shop.level).toBe(2);
    expect(c.credits).toBe(credits - upgradeCost('workshop', 2).cost);
    expect(shopPrice(c, 'smokeGrenade')).toBe(Math.round(25 * 0.65));
    const radio = raise(c, 'radio');
    expect(raiseCost(radio)).toEqual({ cost: FACILITIES.relay.cost, level: FACILITIES.relay.level });
    c.xp = LEVEL_XP[6]!;
    const capacity = contactCapacity(c);
    run(c, { type: 'raise', piece: radio.id });
    expect(facilityLevel(c, 'relay')).toBe(1);
    expect(contactCapacity(c)).toBeGreaterThanOrEqual(capacity + 2);
    run(c, { type: 'raise', piece: radio.id });
    expect(facilityLevel(c, 'listening')).toBe(1);
    expect(listeningDelay(c)).toBeGreaterThanOrEqual(2);
    expect(raiseCost(radio)).toBe('Ya está al máximo.');
    expect(raiseCost(c.bastion.pieces[0]!)).toBe('Eso no se mejora.');
  });

  it('range, La Mula, the simulation room and training do what they say', () => {
    const c = solo();
    c.xp = LEVEL_XP[7]!;
    const s0 = c.soldiers[0]!;
    const aim = squadMember(c, s0).mods.aim;
    const range = raise(c, 'range');
    expect(squadMember(c, s0).mods.aim).toBe(aim + 3);
    range.level = 3;
    expect(squadMember(c, s0).mods.aim).toBeGreaterThanOrEqual(aim + 10);
    raise(c, 'garage', 2);
    expect(garageCut(c).wounds).toBe(2);
    expect(squadLimit(c, [0])).toBeGreaterThan(0);
    const limit = createCampaign(both, new Rng(1));
    expect(squadLimit(limit, both)).toBe(3);
    raise(limit, 'training');
    expect(squadLimit(limit, both)).toBe(4);
    raise(c, 'simulation');
    const report = fly(c);
    expect(report.xp).toBe(Math.round(missionXp(true, report.reward ? activeDifficulty(report) : 1, 0) * 1.25));
  });
});

/** The difficulty a report's mission was flown at: the tests fly ordinary offers, whose difficulty the XP needs. */
function activeDifficulty(report: { xp: number }): number {
  return [1, 2, 3, 4].find((d) => Math.round(missionXp(true, d, 0) * 1.25) === report.xp) ?? 1;
}

describe('the sieges in the campaign (docs/FASE12.md §11.5)', () => {
  it('the clock counts missions; at 0 the siege is the next thing the base does, by every soldier fit to fight', () => {
    const c = solo();
    const lines: string[] = [];
    for (let i = 0; i < SIEGE_FIRST_IN; i++) {
      expect(activeOffer(c)?.siege).toBeUndefined();
      fly(c);
      lines.push(c.log[0]!.text);
    }
    expect(lines[0]).toMatch(/Viene un asedio: llegará dentro de 2 misiones/);
    const siege = activeOffer(c)!;
    expect(siege).toMatchObject({ siege: true, name: 'Asedio', expiresDay: null });
    expect(c.squad.sort()).toEqual(c.soldiers.filter((s) => s.alive && s.woundedDays === 0 && !s.infection).map((s) => s.id).sort());
    // Nothing else to choose, nobody to leave out, no calling it off.
    expect(() => run(c, { type: 'chooseMission', id: c.offers.find((o) => !o.siege)!.id })).toThrow(/asedio/);
    expect(() => run(c, { type: 'cancelMission' })).toThrow(/asedio/);
    expect(() => run(c, { type: 'squad', soldier: c.squad[0]!, on: false })).toThrow(/sanos/);
    // Fought on the base as it stands, with the clock's count.
    expect(offerMap(c, siege)).toEqual({ kind: 'base', base: c.bastion });
    expect(siegePlan(c, siege)).toEqual({ count: c.siege.count, sides: c.siege.sides });
  });

  it('held: what broke stays broken, the traps spent, and the next one comes bigger', () => {
    const c = solo();
    c.siege.in = 0;
    fly(c);
    expect(activeOffer(c)?.siege).toBe(true);
    const palisade = raise(c, 'palisade');
    const mine = raise(c, 'mine');
    const report = fly(c, 'victory', (s) => {
      const [cell] = pieceCells('palisade', palisade.x, palisade.y, palisade.rot);
      s.breakables![cell!.y * s.width + cell!.x] = 4;
      s.traps!.find((t) => t.piece === mine.id)!.charges = 0;
    });
    expect(report.siege).toBe(true);
    expect([palisade.hp, mine.charges]).toEqual([4, 0]);
    expect(report.damage).toEqual({ broken: 0, hurt: 1, spent: 1 });
    expect(c.siege).toMatchObject({ in: SIEGE_EVERY, held: 1, count: SIEGE_FIRST + SIEGE_GROWTH + c.doom });
    // What to mend and rearm, right after the operations (18/10 test: it fell off the end of the agenda).
    c.parts = 10;
    const labels = nextSteps(c, 0).filter((x) => x.tone === 'info').map((x) => x.label);
    const at = labels.indexOf('Repara la empalizada');
    expect(at).toBeGreaterThanOrEqual(0);
    expect(at).toBeLessThanOrEqual(1);
    expect(labels[at + 1]).toBe('Repón la mina');
  });

  it('fallen with someone left: another clearing, the stores lost, the soldiers keep what they carry', () => {
    const c = solo();
    c.siege.in = 0;
    fly(c);
    // One soldier stayed in, wounded: the rest defend and fall.
    const home = c.soldiers[0]!;
    home.woundedDays = 20;
    c.squad = c.squad.filter((id) => id !== home.id);
    raise(c, 'radio');
    const credits = c.credits;
    expect(credits).toBeGreaterThan(0);
    const report = fly(c, 'defeat', (s) => {
      for (const u of s.units.filter((x) => x.team === 'xcom' && !x.automaton)) Object.assign(u, { alive: false, hp: 0 });
    });
    expect(report.fallen).toBe(true);
    expect(c.outcome).toBeNull();
    expect(c.bastion.pieces.map((p) => p.piece)).toEqual(['caravan']);
    expect([c.credits, c.parts]).toEqual([0, 0]);
    expect(home.alive).toBe(true);
    expect(c.log.some((l) => l.alert?.title === 'El Bastión ha caído')).toBe(true);
    // Only the wounded left and nothing to recruit with: time would stand still, so one waits for them to heal.
    expect(mustWait(c)).toBe(true);
    expect(nextSteps(c, 0)).toContainEqual({ view: 'barracks', label: 'Nadie puede salir: esperad a que se curen', tone: 'urgent' });
    const day = c.day;
    // (The Marea, which goes on meanwhile, held back: it is not what is tested here.)
    c.nextDoomDay = 999;
    run(c, { type: 'advance', ready: true });
    // Until someone has healed, or enough suministros have come in to recruit.
    expect(home.woundedDays === 0 || c.credits >= recruitCost(c)).toBe(true);
    expect(c.day).toBeGreaterThan(day);
    // Once someone can go out, there is no waiting again.
    expect(mustWait(c)).toBe(false);
    expect(() => run(c, { type: 'advance', ready: true })).toThrow(/Ya no se espera/);
  });

  it('one siege in three by night, never the first', () => {
    const nights = [0, 1].map((held) => {
      let n = 0;
      for (let seed = 1; seed <= 60; seed++) {
        const c = solo();
        Object.assign(c.siege, { in: 1, held });
        run(c, { type: 'chooseMission', id: c.offers.find((o) => !o.story)!.id });
        const s = createMission({ map: offerMap(c, activeOffer(c)!), slots: c.players, rng: new Rng(2), squad: buildSquad(c), pods: activeOffer(c)!.pods });
        Object.assign(s, { outcome: 'victory', outcomeReason: 'objective' });
        applyMissionResult(c, s, new Rng(seed));
        const siege = activeOffer(c)!;
        expect(siege.siege).toBe(true);
        // Lore's line for the dark, after the rest.
        expect(offerBriefing(c, siege).endsWith(BASE_SIEGE_TEXT.night)).toBe(!!siege.night);
        if (siege.night) n++;
      }
      return n;
    });
    expect(nights[0]).toBe(0);
    expect(nights[1]).toBeGreaterThan(10);
    expect(nights[1]).toBeLessThan(30);
  });

  it('fallen with nobody left: the campaign is lost', () => {
    const c = solo();
    c.siege.in = 0;
    fly(c);
    fly(c, 'defeat', (s) => {
      for (const u of s.units.filter((x) => x.team === 'xcom')) Object.assign(u, { alive: false, hp: 0 });
    });
    expect(c.soldiers.some((s) => s.alive)).toBe(false);
    expect(c.outcome).toBe('defeat');
  });
});

describe('the Camposanto', () => {
  it('keeps the fallen with the day they fell, steadies everyone, and at level 3 the survivors learn from the losses', () => {
    const c = solo();
    const memorial = raise(c, 'memorial');
    const put = (level: number) => (memorial.level = level);
    const s0 = c.soldiers[0]!;
    put(1);
    const will = squadMember(c, s0).will;
    put(2);
    expect(squadMember(c, s0).will).toBe(will + 5);
    put(3);
    expect(squadMember(c, s0).will).toBe(will + 10);
    const offer = c.offers[0]!;
    run(c, { type: 'chooseMission', id: offer.id });
    const mission = createMission({ map: offer.map, slots: [0], rng: new Rng(2), kind: offer.kind, squad: buildSquad(c), pods: offer.pods });
    const [fallen, survivor] = mission.units.filter((u) => u.team === 'xcom');
    fallen!.alive = false;
    fallen!.hp = 0;
    survivor!.deeds = { hit: 2 };
    mission.outcome = 'victory';
    const report = applyMissionResult(c, mission, new Rng(1));
    const dead = c.soldiers.find((s) => s.id === fallen!.campaignId)!;
    expect(dead).toMatchObject({ alive: false, fellDay: report.day });
    expect(c.log.some((l) => l.text === `${dead.name} descansa en el Camposanto.`)).toBe(true);
    // What it did, and one more of it for the fallen.
    expect(report.squad.find((e) => e.id === survivor!.campaignId)!.learned).toMatchObject({ shooting: 3 });
  });
});

describe('the world', () => {
  it('contacting regions costs credits, grows the monthly payment and is capped by communications', () => {
    const c = solo();
    expect(contactCapacity(c)).toBe(2);
    run(c, { type: 'contact', region: 'oce' });
    expect(() => run(c, { type: 'contact', region: 'afr' })).toThrow(/comunicaciones/);
    const credits = c.credits;
    c.nextSupplyDay = c.day + 1;
    advanceTime(c, new Rng(11));
    expect(c.credits).toBe(credits + 60 + 30 + 25);
  });

  it('alien facilities push the project until a squad destroys them', () => {
    const c = solo();
    // Low activity, held there: no zone under the Marea to get in the way of the contacts.
    c.doom = 2;
    c.nextDoomDay = 999;
    c.nextFacilityDay = c.day + 1;
    advanceTime(c, new Rng(11));
    const region = c.regions.find((r) => r.facility)!;
    expect(region).toBeDefined();
    if (!region.contacted) {
      expect(c.offers.some((o) => o.facility)).toBe(false);
      c.credits = 2000;
      c.xp = LEVEL_XP[6]!;
      raise(c, 'radio', 3);
      // Contacts go by road: every zone on the way first.
      for (const id of roadRoute('weu', region.id).slice(1)) if (!c.regions.find((r) => r.id === id)!.contacted) run(c, { type: 'contact', region: id });
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
    expect(c.doom).toBeLessThanOrEqual(doom - 1);
  });

  it('events stop the clock until both answer, and the answer does what it says', () => {
    const c = createCampaign(both, new Rng(5));
    c.nextEventDay = 2;
    advanceTime(c, new Rng(11));
    expect(c.event).not.toBeNull();
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
  it('a proposal says what it will cost, paid only when the partner accepts (playtest 2026-10-16)', () => {
    const c = createCampaign(both, new Rng(1));
    c.credits = 500;
    // A barracón is dear: a vote (docs/FASE12.md §11.7).
    const spot = freeSpot(c, 'quarters');
    run(c, { type: 'place', piece: 'quarters', x: spot.x, y: spot.y, rot: 0 }, 0, both);
    expect(c.credits).toBe(500);
    expect(proposalCost(c, c.proposal!.action)).toBe(PIECES.quarters.cost.supplies);
    run(c, { type: 'vote', accept: true }, 1, both);
    expect(c.credits).toBe(500 - PIECES.quarters.cost.supplies);
    expect(proposalCost(c, { type: 'chooseMission', id: 'x' })).toBe(0);
  });

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
    // The belt has two places for any accessory (docs/FASE9.md §4); special rounds go no more.
    run(c, { type: 'buy', item: 'smokeGrenade' }, 0, both);
    run(c, { type: 'equip', soldier: mine.id, slot: 'ammo', item: 'smokeGrenade' }, 0, both);
    expect(itemsFree(c, 'smokeGrenade')).toBe(0);
    c.inventory.apRounds = 1;
    expect(() => run(c, { type: 'equip', soldier: mine.id, slot: 'ammo', item: 'apRounds' }, 0, both)).toThrow(/cinturón/);
  });

  it('equipment reaches the battlefield and is lost with its bearer', () => {
    const c = solo();
    const s = c.soldiers[0]!;
    c.xp = LEVEL_XP[3]!;
    run(c, { type: 'buy', item: 'fragGrenade' });
    run(c, { type: 'buy', item: 'smokeGrenade' });
    run(c, { type: 'equip', soldier: s.id, slot: 'ammo', item: 'fragGrenade' });
    run(c, { type: 'equip', soldier: s.id, slot: 'utility', item: 'smokeGrenade' });
    const grenades = c.inventory.fragGrenade!;
    const member = squadMember(c, s);
    expect(member.charges.grenade).toBe(2);
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
    expect(c.inventory.fragGrenade).toBe(grenades - 1);
    expect(c.inventory.smokeGrenade ?? 0).toBe(0);
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
    expect(defaultAppearance(0).primary).toBe('#826cbd');
    expect(defaultAppearance(1).primary).toBe('#bc854b');
  });

  it('new recruits come without a class, with a recruit look in their player colours', () => {
    const c = solo();
    for (const s of c.soldiers.slice(2)) s.alive = false;
    run(c, { type: 'recruit' });
    const s = c.soldiers.at(-1)!;
    expect(s.board).toEqual([]);
    expect(s.appearance).toEqual(defaultAppearance(0, undefined, sexOfName(s.name)));
    expect(s.appearance.head).toBe('helmet');
  });
});
