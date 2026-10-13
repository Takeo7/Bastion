import { describe, expect, it } from 'vitest';
import {
  applyMissionResult,
  buildSquad,
  CAMPAIGN_VERSION,
  craftBlocker,
  createCampaign,
  dailyRations,
  executeCampaignCommand,
  HUNGRY_WILL,
  LA_VEGA_FOOD,
  LA_VEGA_FOOD_DAYS,
  migrateCampaign,
  plotById,
  squadMember,
  START_FOOD,
  type CampaignCommand,
  type CampaignState,
} from '../src/campaign';
import { specialize } from '../src/classes';
import { FOUND_WEAPONS } from '../src/content';
import { rollLoot } from '../src/loot';
import { CRAFTS, LEVEL_XP, weaponTier } from '../src/progression';
import { Rng } from '../src/rng';
import { createMission } from '../src/setup';
import type { DecorKind, Slot } from '../src/types';

// Phase 4 of the zombies: food, the Taller's work and found weapons (docs/FASE4.md).

const run = (c: CampaignState, cmd: CampaignCommand, slot: Slot = 0, connected: Slot[] = [0]) => executeCampaignCommand(c, cmd, { slot, connected, rng: new Rng(7) });

/** A campaign where nothing interrupts the days but what is being tested. */
function quiet(players: Slot[] = [0]): CampaignState {
  const c = createCampaign(players, new Rng(1));
  Object.assign(c, { nextEventDay: 999, nextMissionDay: 999, nextRumorDay: 999, nextFacilityDay: 999, nextSupplyDay: 999, nextDoomDay: 999, nextMedicineDay: 999, nextFoodDay: 999 });
  c.offers = c.offers.filter((o) => o.story);
  for (const o of c.offers) o.expiresDay = null;
  return c;
}

/** Days pass one at a time (an «advance» may run several when nothing happens). */
function days(c: CampaignState, n: number): void {
  const until = c.day + n;
  for (let i = 0; i < 4 * n && c.day < until; i++) {
    const at = c.day;
    // One day: everything else scheduled far away, so only the day ends the advance.
    c.nextEventDay = Math.min(c.nextEventDay, at + 1);
    run(c, { type: 'advance', ready: true });
    c.event = null;
    c.nextEventDay = 999;
  }
}

/** Out of `n` searches of `decor`, the share that found `what`. */
function share(decor: DecorKind, what: 'food' | 'parts' | 'weapon', n = 4000): number {
  let hits = 0;
  for (let seed = 1; seed <= n; seed++) if (rollLoot(decor, 2, new Rng(seed))?.[what] !== undefined) hits++;
  return hits / n;
}

describe('food', () => {
  it('the Bastion eats a ration a day for every 4 soldiers, rounded up', () => {
    const c = quiet();
    expect(c.food).toBe(START_FOOD);
    expect(dailyRations(c)).toBe(2);
    days(c, 3);
    expect(c.food).toBe(START_FOOD - 6);
    c.soldiers[0]!.alive = false;
    expect(dailyRations(c)).toBe(2);
    for (const s of c.soldiers.slice(1, 4)) s.alive = false;
    expect(dailyRations(c)).toBe(1);
  });

  it('with no rations, hunger: less will on missions and wounds heal every other day, until food comes', () => {
    const c = quiet();
    c.food = 1;
    const wounded = c.soldiers[0]!;
    days(c, 1);
    expect(c.hungry).toBe(true);
    expect(c.food).toBe(0);
    expect(c.log.some((l) => l.alert?.title === 'Hambre en el Bastión')).toBe(true);
    const fit = c.soldiers[1]!;
    const will = squadMember(c, fit).will;
    c.hungry = false;
    expect(squadMember(c, fit).will).toBe(will + HUNGRY_WILL);
    c.hungry = true;
    // Two days of healing take four.
    wounded.woundedDays = 2;
    days(c, 2);
    expect(wounded.woundedDays).toBe(1);
    days(c, 2);
    expect(wounded.woundedDays).toBe(0);
    // A Huerto brings rations: with enough for a day, the hunger ends.
    const plot = plotById(c, '4,4')!;
    plot.building = 'garden';
    plot.level = 3;
    days(c, 1);
    expect(c.hungry).toBe(false);
    expect(c.food).toBe(1);
  });

  it('the Huerto grows a ration a day for each level', () => {
    const c = quiet();
    const plot = plotById(c, '4,4')!;
    plot.building = 'garden';
    plot.level = 2;
    const food = c.food!;
    days(c, 4);
    expect(c.food).toBe(food + 4 * (2 - dailyRations(c)));
  });

  it('La Vega, an ally standing, sends 2 rations every 4 days', () => {
    const c = quiet();
    c.food = 100;
    const vega = c.regions.find((r) => r.id === 'oce')!;
    c.nextFoodDay = c.day + 1;
    vega.contacted = false;
    let food = c.food!;
    days(c, LA_VEGA_FOOD_DAYS);
    expect(c.food).toBe(food - LA_VEGA_FOOD_DAYS * dailyRations(c));
    vega.contacted = true;
    c.nextFoodDay = c.day + 1;
    food = c.food!;
    days(c, LA_VEGA_FOOD_DAYS);
    expect(c.food).toBe(food - LA_VEGA_FOOD_DAYS * dailyRations(c) + LA_VEGA_FOOD);
    vega.facility = { nextDoomDay: 999 };
    food = c.food!;
    days(c, LA_VEGA_FOOD_DAYS);
    expect(c.food).toBe(food - LA_VEGA_FOOD_DAYS * dailyRations(c));
  });

  it('searching finds rations: pantries, shops and stores most, car boots little', () => {
    for (const decor of ['wardrobe', 'shelf', 'crates'] as const) expect(share(decor, 'food')).toBeGreaterThan(share('carRear', 'food') * 2);
    for (let seed = 1; seed < 400; seed++) {
      const food = rollLoot('shelf', 2, new Rng(seed))?.food;
      if (food !== undefined) {
        expect(food).toBeGreaterThanOrEqual(2);
        expect(food).toBeLessThanOrEqual(4);
      }
    }
  });
});

describe('the Taller', () => {
  /** A campaign with a Taller at `level` and plenty to pay with. */
  function workshop(level: number, xp = LEVEL_XP[5]!) {
    const c = quiet();
    const plot = plotById(c, '4,4')!;
    plot.building = 'workshop';
    plot.level = level;
    Object.assign(c, { xp, credits: 1000, parts: 30 });
    return { c, plot };
  }

  it('making tier-2 weapons: Taller, Bastion level 3, 60 §, 4 parts and 3 days of a crew; then everyone fit carries them', () => {
    const none = quiet();
    expect(craftBlocker(none, 'weapons2')).toBe('Hace falta el Taller.');
    const { c, plot } = workshop(1, LEVEL_XP[1]!);
    expect(craftBlocker(c, 'weapons2')).toMatch(/nivel 3 del Bastión/);
    c.xp = LEVEL_XP[2]!;
    c.parts = 3;
    expect(craftBlocker(c, 'weapons2')).toBe('No hay piezas suficientes.');
    c.parts = 10;
    expect(run(c, { type: 'craft', project: 'weapons2' }).ok).toBe(true);
    expect(c.credits).toBe(1000 - CRAFTS.weapons2.cost);
    expect(c.parts).toBe(10 - CRAFTS.weapons2.parts);
    expect(c.base.works).toEqual([{ plot: plot.id, kind: 'craft', facility: 'workshop', daysLeft: 3, total: 3, project: 'weapons2' }]);
    expect(craftBlocker(c, 'weapons2')).toBe('Ya se está haciendo.');
    const cabo = c.soldiers[0]!;
    cabo.rank = 2;
    expect(weaponTier(c, cabo)).toBe(1);
    days(c, 3);
    expect(c.crafted).toEqual(['weapons2']);
    expect(weaponTier(c, cabo)).toBe(2);
    expect(weaponTier(c, { ...cabo, rank: 0 })).toBe(1);
  });

  it('tier 3 needs the Taller at level 2, its level of the Bastion and tier 2 made first; two players decide together', () => {
    const { c, plot } = workshop(1);
    expect(craftBlocker(c, 'weapons3')).toBe('Hace falta el Taller a nivel 2.');
    plot.level = 2;
    expect(craftBlocker(c, 'weapons3')).toMatch(/Antes hay que hacer/);
    expect(craftBlocker(c, 'armor3')).toMatch(/nivel 7 del Bastión/);
    c.crafted = ['weapons2'];
    expect(craftBlocker(c, 'weapons3')).toBeNull();
    const two = workshop(2);
    two.c.players = [0, 1];
    two.c.crafted = ['weapons2'];
    expect(run(two.c, { type: 'craft', project: 'weapons3' }, 0, [0, 1]).ok).toBe(true);
    expect(two.c.proposal?.action).toEqual({ type: 'craft', project: 'weapons3' });
    expect(two.c.base.works).toEqual([]);
    expect(run(two.c, { type: 'vote', accept: true }, 1, [0, 1]).ok).toBe(true);
    expect(two.c.base.works[0]).toMatchObject({ kind: 'craft', project: 'weapons3', total: 5 });
  });
});

describe('found weapons', () => {
  /** A campaign whose last mission brought home `kind`, carried by its first soldier. */
  function found(kind: keyof typeof FOUND_WEAPONS = 'huntingRifle') {
    const c = quiet([0, 1]);
    const offer = createCampaign([0, 1], new Rng(2)).offers.find((o) => !o.story)!;
    c.offers.push(offer);
    if (!run(c, { type: 'chooseMission', id: offer.id }, 0, [0]).ok) throw new Error('mission');
    const mission = createMission({ map: offer.map, slots: [0, 1], rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    const squad = mission.units.filter((u) => u.team === 'xcom');
    Object.assign(squad[0]!, { alive: false, evacuated: true, backpack: [{ weapon: kind }] });
    mission.outcome = 'victory';
    mission.outcomeReason = 'objective';
    const report = applyMissionResult(c, mission, new Rng(4));
    return { c, report };
  }

  it('rare: boots, stores and home wardrobes; never an office', () => {
    expect(share('carRear', 'weapon')).toBeCloseTo(0.04, 1);
    expect(share('crates', 'weapon')).toBeCloseTo(0.03, 1);
    expect(share('wardrobe', 'weapon')).toBeGreaterThan(0);
    expect(share('desk', 'weapon')).toBe(0);
  });

  it('brought home, it goes to the Armería; a soldier who carries that kind of weapon takes it, a tier above the rest', () => {
    const { c, report } = found();
    expect(report.loot?.weapons).toEqual(['huntingRifle']);
    expect(report.loot?.line).toMatch(/fusil de caza/);
    const w = c.foundWeapons![0]!;
    expect(w).toMatchObject({ kind: 'huntingRifle', holder: null });
    const rookie = c.soldiers.find((s) => s.owner === 0 && s.alive)!;
    rookie.rank = 2;
    // Not for a partner's soldier, nor for one with another kind of weapon.
    const theirs = c.soldiers.find((s) => s.owner === 1)!;
    expect(run(c, { type: 'assignWeapon', weapon: w.id, soldier: theirs.id }, 0).ok).toBe(false);
    const assault = c.soldiers.find((s) => s.owner === 0 && s.alive && s !== rookie)!;
    specialize(assault, 'assault');
    expect(run(c, { type: 'assignWeapon', weapon: w.id, soldier: assault.id }, 0)).toEqual({ ok: false, error: 'Es para quien lleva fusil de asalto.' });
    expect(weaponTier(c, rookie)).toBe(1);
    expect(run(c, { type: 'assignWeapon', weapon: w.id, soldier: rookie.id }, 0).ok).toBe(true);
    expect(weaponTier(c, rookie)).toBe(2);
    expect(squadMember(c, rookie)).toMatchObject({ weaponTier: 2, weaponName: 'Fusil de caza' });
    // Never past what the rank allows, nor past 3.
    expect(weaponTier(c, { ...rookie, rank: 0 })).toBe(1);
    c.crafted = ['weapons2', 'weapons3'];
    rookie.rank = 4;
    expect(weaponTier(c, rookie)).toBe(3);
    // The partner can't take it from them.
    expect(run(c, { type: 'assignWeapon', weapon: w.id, soldier: null }, 1).ok).toBe(false);
    expect(run(c, { type: 'assignWeapon', weapon: w.id, soldier: null }, 0).ok).toBe(true);
    expect(w.holder).toBeNull();
  });

  it('it is lost if its soldier falls', () => {
    const { c } = found();
    const w = c.foundWeapons![0]!;
    const rookie = c.soldiers.find((s) => s.owner === 0 && s.alive)!;
    run(c, { type: 'assignWeapon', weapon: w.id, soldier: rookie.id }, 0);
    const offer = createCampaign([0, 1], new Rng(5)).offers.find((o) => !o.story)!;
    c.offers.push(offer);
    run(c, { type: 'chooseMission', id: offer.id }, 0, [0]);
    c.squad = [rookie.id];
    const mission = createMission({ map: offer.map, slots: [0, 1], rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    const unit = mission.units.find((u) => u.campaignId === rookie.id)!;
    expect(unit.weaponName).toBe('Fusil de caza');
    Object.assign(unit, { alive: false, hp: 0 });
    mission.outcome = 'defeat';
    mission.outcomeReason = 'wiped';
    applyMissionResult(c, mission, new Rng(4));
    expect(rookie.alive).toBe(false);
    expect(c.foundWeapons).toEqual([]);
  });
});

describe('the campaign', () => {
  it('some operations pay rations or parts; Acorazados brought down give a part each', () => {
    const offers = Array.from({ length: 20 }, (_, i) => createCampaign([0], new Rng(i + 1)).offers).flat().filter((o) => !o.story);
    expect(offers.some((o) => o.reward.food)).toBe(true);
    expect(offers.some((o) => o.reward.parts)).toBe(true);
    const c = quiet();
    const offer = createCampaign([0], new Rng(9)).offers.find((o) => !o.story && o.kind !== 'scavenge')!;
    offer.reward = { credits: 10, food: 4, parts: 2 };
    c.offers.push(offer);
    run(c, { type: 'chooseMission', id: offer.id });
    const mission = createMission({ map: offer.map, slots: [0], rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: [['mec', 'mec'], ['zombie']], difficulty: offer.difficulty });
    for (const u of mission.units) if (u.template === 'mec') Object.assign(u, { alive: false, hp: 0 });
    mission.outcome = 'victory';
    mission.outcomeReason = 'objective';
    const food = c.food!;
    const report = applyMissionResult(c, mission, new Rng(4));
    expect(report.scrap).toBe(2);
    expect(c.parts).toBe(2 + 2);
    // The ration reward, less what the days back ate.
    expect(c.food! + (c.day - report.day) * dailyRations(c)).toBe(food + 4);
  });

  it('version 13: older campaigns start with a full pantry and keep the tiers their level gave them', () => {
    expect(CAMPAIGN_VERSION).toBe(13);
    const old = createCampaign([0], new Rng(1)) as CampaignState & Record<string, unknown>;
    for (const k of ['food', 'hungry', 'nextFoodDay', 'parts', 'crafted', 'foundWeapons']) delete old[k];
    (old as { version: number }).version = 12;
    old.xp = LEVEL_XP[5]!;
    const c = migrateCampaign(old);
    expect(c.version).toBe(13);
    expect(c).toMatchObject({ food: START_FOOD, hungry: false, parts: 0, foundWeapons: [] });
    expect(c.crafted).toEqual(['weapons2', 'weapons3', 'armor2']);
    expect(weaponTier(c, { id: 'x', rank: 4 })).toBe(3);
  });
});
