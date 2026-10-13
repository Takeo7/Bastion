import { describe, expect, it } from 'vitest';
import {
  applyMissionResult,
  buildSquad,
  CAMPAIGN_VERSION,
  createCampaign,
  defaultSquad,
  ENCLAVE_DAYS,
  executeCampaignCommand,
  migrateCampaign,
  plotById,
  soldierReady,
  START_MEDICINES,
  treatCost,
  turnDay,
  TURN_DAYS,
  type CampaignCommand,
  type CampaignSoldier,
  type CampaignState,
} from '../src/campaign';
import { INFECTION, TEMPLATES } from '../src/content';
import { applyEvent, cloneState, type GameEvent } from '../src/events';
import { Rng } from '../src/rng';
import { risenName, Sim } from '../src/rules';
import { createMission } from '../src/setup';
import type { GameState, Pod, Slot, TemplateId, Unit } from '../src/types';
import { addUnit, stateFrom } from './helpers';

// Phase 3 of the zombies: infection and medicines (docs/FASE3.md).

const field = () => stateFrom(Array.from({ length: 12 }, () => '.'.repeat(12)));
const of = <T extends GameEvent['t']>(events: GameEvent[], t: T) => events.filter((e): e is Extract<GameEvent, { t: T }> => e.t === t);

function pod(s: GameState, id: string, active: boolean, units: Unit[]): Pod {
  const p: Pod = { id, active, unitIds: units.map((u) => u.id), patrol: [], patrolIndex: 0 };
  for (const u of units) u.podId = id;
  s.pods.push(p);
  return p;
}

/** Out of `n` bites that drew blood, how many infected. */
function biteRate(template: TemplateId, n: number, extra: Partial<Unit> = {}): number {
  let hurt = 0;
  let infected = 0;
  for (let seed = 1; hurt < n; seed++) {
    const s = field();
    s.activeTeam = 'alien';
    const soldier = addUnit(s, 's1', 'rookie', 5, 5, { hp: 99, maxHp: 99, ...extra });
    const zombie = addUnit(s, 'a1', template, 6, 5);
    const run = new Sim(s, new Rng(seed), [0]);
    run.attack(zombie, soldier, 'claw');
    if (soldier.hp === 99) continue;
    hurt++;
    if (of(run.events, 'infected').length) infected++;
  }
  return infected / n;
}

describe('infection in combat', () => {
  it('a bite that draws blood infects by who bites; a Chaleco antibalas takes 10 off', () => {
    expect(INFECTION.bite).toEqual({ zombie: 25, xenoid: 20, mec: 35 });
    expect(biteRate('zombie', 1500)).toBeCloseTo(0.25, 1);
    expect(biteRate('xenoid', 1500)).toBeCloseTo(0.2, 1);
    expect(biteRate('mec', 1500)).toBeCloseTo(0.35, 1);
    expect(biteRate('zombie', 1500, { gear: ['nanoVest'] })).toBeCloseTo(0.15, 1);
  });

  it('no blood, no infection: a miss, or a bite the armour stops; and only the living of the squad', () => {
    for (let seed = 1; seed < 80; seed++) {
      const s = field();
      const soldier = addUnit(s, 's1', 'rookie', 5, 5, { armor: 20 });
      const zombie = addUnit(s, 'a1', 'zombie', 6, 5);
      const run = new Sim(s, new Rng(seed), [0]);
      run.attack(zombie, soldier, 'claw');
      expect(of(run.events, 'infected')).toEqual([]);
    }
  });

  it('the event marks the soldier for the rest of the mission; the botiquín heals wounds, not bites', () => {
    const s = field();
    const soldier = addUnit(s, 's1', 'rookie', 5, 5, { hp: 2, maxHp: 6, charges: { firstAid: 1 }, gear: ['firstAid'] });
    applyEvent(s, { t: 'infected', unit: 's1', by: 'a1' });
    expect(soldier.infected).toBe(true);
    new Sim(s, new Rng(1), [0]).perform(soldier, 'firstAid', { target: soldier });
    expect(soldier.hp).toBe(6);
    expect(soldier.infected).toBe(true);
  });

  it('a Hinchado’s burst infects half of those it hurts', () => {
    let hurt = 0;
    let infected = 0;
    for (let seed = 1; seed < 800; seed++) {
      const s = field();
      const soldier = addUnit(s, 's1', 'rookie', 5, 5, { hp: 99, maxHp: 99 });
      const bloater = addUnit(s, 'a1', 'bloater', 6, 5, { hp: 1 });
      const run = new Sim(s, new Rng(seed), [0]);
      run.damage(bloater, 5, 0);
      if (soldier.hp < 99) hurt++;
      if (of(run.events, 'infected').some((e) => e.unit === 's1' && e.by === 'a1')) infected++;
    }
    expect(hurt).toBe(799);
    expect(infected / hurt).toBeCloseTo(INFECTION.burst / 100, 1);
  });

  it('an infected soldier who falls rises as the zombies’ turn begins, and acts from the next', () => {
    const s = field();
    const fallen = addUnit(s, 's1', 'rookie', 5, 5, { infected: true });
    const clean = addUnit(s, 's2', 'rookie', 7, 5);
    const out = addUnit(s, 's3', 'rookie', 0, 0, { infected: true, alive: false, evacuated: true });
    addUnit(s, 's4', 'rookie', 0, 11, { hp: 99, maxHp: 99 });
    const z = addUnit(s, 'a1', 'zombie', 11, 11);
    pod(s, 'p1', false, [z]);
    const run = new Sim(s, new Rng(1), [0]);
    run.damage(fallen, 99, 0, z);
    run.damage(clean, 99, 0, z);
    const client = cloneState(s);
    const turn = new Sim(s, new Rng(2), [0]);
    turn.runAlienTurn();
    const risen = of(turn.events, 'reanimated');
    expect(risen).toHaveLength(1);
    expect(risen[0]).toMatchObject({ corpse: 's1', pod: { active: true } });
    expect(risen[0]!.unit).toBeUndefined();
    const zombie = s.units.find((u) => u.id === risen[0]!.zombie.id)!;
    // The soldier keeps its name; anyone else rises as a Zombi.
    expect(zombie).toMatchObject({ template: 'zombie', name: 'Zombi · s1', alive: true, pos: { x: 5, y: 5 } });
    expect(risenName(addUnit(cloneState(s), 'a9', 'trooper', 0, 0))).toBe(TEMPLATES.zombie.name);
    expect(s.pods.find((p) => p.id === zombie.podId)?.unitIds).toEqual([zombie.id]);
    expect(fallen.reanimated).toBe(true);
    expect(of(turn.events, 'moved').some((e) => e.unit === zombie.id)).toBe(false);
    expect(out.reanimated).toBe(false);
    // Once only, and the clients see the same.
    const next = new Sim(s, new Rng(3), [0]);
    next.runAlienTurn();
    expect(of(next.events, 'reanimated')).toEqual([]);
    for (const e of turn.events) applyEvent(client, e);
    for (const e of next.events) applyEvent(client, e);
    expect(client).toEqual(s);
  });
});

describe('infection at the base', () => {
  const run = (c: CampaignState, cmd: CampaignCommand, slot: Slot = 0, connected: Slot[] = [0, 1]) => executeCampaignCommand(c, cmd, { slot, connected, rng: new Rng(7) });

  /** A campaign whose last mission brought `bitten` soldiers home infected. */
  function bitten(count = 1, players: Slot[] = [0, 1]) {
    const c = createCampaign(players, new Rng(2));
    // Nothing else interrupts the days.
    Object.assign(c, { nextEventDay: 999, nextMissionDay: 999, nextRumorDay: 999, nextFacilityDay: 999, nextSupplyDay: 999, nextDoomDay: 999 });
    const offer = c.offers[0]!;
    if (!run(c, { type: 'chooseMission', id: offer.id }, 0, [players[0]!]).ok) throw new Error('mission');
    const mission = createMission({ map: offer.map, slots: players, rng: new Rng(3), kind: offer.kind, squad: buildSquad(c), pods: offer.pods, difficulty: offer.difficulty });
    const squad = mission.units.filter((u) => u.team === 'xcom');
    for (const u of squad.slice(0, count)) u.infected = true;
    // A bitten soldier who fell stays fallen (in the field, the corpse rose).
    Object.assign(squad[count]!, { infected: true, alive: false, hp: 0 });
    mission.outcome = 'victory';
    mission.outcomeReason = 'objective';
    const report = applyMissionResult(c, mission, new Rng(4));
    const sick = squad.slice(0, count).map((u) => c.soldiers.find((s) => s.id === u.campaignId)!);
    return { c, report, sick, fallen: c.soldiers.find((s) => s.id === squad[count]!.campaignId)! };
  }

  it('a soldier back with a bite has a fever: three days, five with an Enfermería, and no deploying', () => {
    const { c, report, sick, fallen } = bitten();
    const [s] = sick as [CampaignSoldier];
    expect(report.infected).toEqual([s.id]);
    expect(s.infection).toEqual({ since: c.day });
    expect(fallen.alive).toBe(false);
    expect(fallen.infection).toBeUndefined();
    expect(turnDay(c, s)).toBe(c.day + TURN_DAYS);
    plotById(c, '4,4')!.building = 'infirmary';
    expect(turnDay(c, s)).toBe(c.day + TURN_DAYS + 2);
    expect(soldierReady(s)).toBe(false);
    expect(defaultSquad(c, [0, 1])).not.toContain(s.id);
  });

  it('untreated, it turns on its day: one more of the Caídos', () => {
    const { c, sick } = bitten();
    const [s] = sick as [CampaignSoldier];
    const day = turnDay(c, s)!;
    for (let i = 0; i < 10 && s.alive; i++) run(c, { type: 'advance', ready: true }, 0, [0]);
    expect(s.alive).toBe(false);
    expect(c.day).toBe(day);
    expect(s).toMatchObject({ fellDay: day, fellCause: 'turned' });
    expect(s.infection).toBeUndefined();
    expect(c.log.some((l) => l.alert && /se ha convertido/.test(l.alert.title))).toBe(true);
  });

  it('treating takes both keys (alone, one) and 2 ✚, 1 with the Enfermería at level 2', () => {
    const { c, sick } = bitten(2);
    const [a, b] = sick as [CampaignSoldier, CampaignSoldier];
    c.medicines = 5;
    expect(run(c, { type: 'treat', soldier: a.id, ready: true }, 0).ok).toBe(true);
    expect(c.treatment).toEqual({ soldier: a.id, ready: [true, false] });
    expect(a.infection).toBeDefined();
    // Another proposal waits for this one.
    expect(run(c, { type: 'treat', soldier: b.id, ready: true }, 1).ok).toBe(false);
    expect(run(c, { type: 'treat', soldier: a.id, ready: true }, 1).ok).toBe(true);
    expect(a.infection).toBeUndefined();
    expect(c.medicines).toBe(3);
    expect(c.treatment).toBeNull();
    // The partner can turn it down.
    run(c, { type: 'treat', soldier: b.id, ready: true }, 0);
    run(c, { type: 'treat', soldier: b.id, ready: false }, 1);
    expect(c.treatment).toBeNull();
    expect(b.infection).toBeDefined();
    // With the Enfermería at level 2, one ✚; and a player alone needs no second key.
    const plot = plotById(c, '4,4')!;
    plot.building = 'infirmary';
    plot.level = 2;
    expect(treatCost(c)).toBe(1);
    expect(run(c, { type: 'treat', soldier: b.id, ready: true }, 0, [0]).ok).toBe(true);
    expect(b.infection).toBeUndefined();
    expect(c.medicines).toBe(2);
    expect(soldierReady(b)).toBe(b.woundedDays === 0);
  });

  it('no medicines, no treatment', () => {
    const { c, sick } = bitten(1, [0]);
    c.medicines = 1;
    const r = run(c, { type: 'treat', soldier: sick[0]!.id, ready: true }, 0, [0]);
    expect(r).toEqual({ ok: false, error: 'No hay medicinas suficientes.' });
    expect(sick[0]!.infection).toBeDefined();
  });

  it('a bitten soldier cannot join the squad', () => {
    const { c, sick } = bitten();
    const s = sick[0]!;
    const offer = c.offers.find((o) => !o.story) ?? c.offers[0]!;
    run(c, { type: 'chooseMission', id: offer.id }, 0, [0]);
    c.squad = [];
    const r = run(c, { type: 'squad', soldier: s.id, on: true }, s.owner, [s.owner]);
    expect(r.ok).toBe(false);
    expect(r.ok ? '' : r.error).toMatch(/fiebre/);
  });

  it('medicines heal wounds: 1 ✚ for 3 days fewer', () => {
    const c = createCampaign([0], new Rng(1));
    expect(c.medicines).toBe(START_MEDICINES);
    const s = c.soldiers[0]!;
    expect(run(c, { type: 'medicate', soldier: s.id }, 0, [0]).ok).toBe(false);
    s.woundedDays = 5;
    expect(run(c, { type: 'medicate', soldier: s.id }, 0, [0]).ok).toBe(true);
    expect(s.woundedDays).toBe(2);
    expect(c.medicines).toBe(START_MEDICINES - 1);
    c.medicines = 0;
    expect(run(c, { type: 'medicate', soldier: s.id }, 0, [0])).toEqual({ ok: false, error: 'No hay medicinas.' });
  });

  it('La Vega and Corran send medicines every few days while they are allies and stand; some operations pay them too', () => {
    const c = createCampaign([0], new Rng(1));
    // No Marea to get in the way (docs/FASE5.md): the activity stays put.
    Object.assign(c, { nextEventDay: 999, nextMissionDay: 999, nextRumorDay: 999, nextFacilityDay: 999, nextSupplyDay: 999, nextDoomDay: 999 });
    /** Advances to day `until` at least; what the enclaves sent meanwhile, convoy by convoy. */
    const wait = (until: number) => {
      // The log is newest first (and short): what's new is what comes before the entry on top now.
      const mark = c.log[0];
      const start = c.medicines!;
      for (let i = 0; i < 60 && c.day < until; i++) run(c, { type: 'advance', ready: true }, 0, [0]);
      const fresh = mark && c.log.includes(mark) ? c.log.slice(0, c.log.indexOf(mark)) : c.log;
      const convoys = fresh.filter((l) => /medicinas: \+/.test(l.text)).map((l) => Number(/\+(\d+) ✚/.exec(l.text)![1]));
      expect(c.medicines! - start).toBe(convoys.reduce((a, b) => a + b, 0));
      return convoys;
    };
    // Not contacted yet: no allies, no medicines.
    for (const r of c.regions) if (r.id === 'oce' || r.id === 'afr') r.contacted = false;
    expect(wait(1 + 2 * ENCLAVE_DAYS)).toEqual([]);
    // Contacted: each sends 1 ✚ every few days.
    for (const r of c.regions) if (r.id === 'oce' || r.id === 'afr') r.contacted = true;
    const both = wait(c.day + 2 * ENCLAVE_DAYS);
    expect(both.length).toBeGreaterThanOrEqual(1);
    expect(both.every((n) => n === 2)).toBe(true);
    // A Nido in La Vega: only Corran still sends.
    c.regions.find((r) => r.id === 'oce')!.facility = { nextDoomDay: 999 };
    const one = wait(c.day + 2 * ENCLAVE_DAYS);
    expect(one.length).toBeGreaterThanOrEqual(1);
    expect(one.every((n) => n === 1)).toBe(true);
    // Recoveries always pay a ✚; some others do too.
    const offers = Array.from({ length: 12 }, (_, i) => createCampaign([0], new Rng(i + 1)).offers).flat().filter((o) => !o.story);
    for (const o of offers) if (o.kind === 'recovery') expect(o.reward.medicines).toBe(1);
    expect(offers.some((o) => o.kind !== 'recovery' && o.reward.medicines)).toBe(true);
  });

  it('an older campaign comes in with its medicines and nobody bitten', () => {
    const old = createCampaign([0, 1], new Rng(1)) as CampaignState & Record<string, unknown>;
    old.medicines = 3;
    delete old.nextMedicineDay;
    delete old.treatment;
    (old as { version: number }).version = 11;
    const c = migrateCampaign(old);
    expect(c.version).toBe(CAMPAIGN_VERSION);
    expect(CAMPAIGN_VERSION).toBeGreaterThanOrEqual(12);
    expect(c.medicines).toBe(3);
    expect(c.nextMedicineDay).toBe(c.day + ENCLAVE_DAYS);
    expect(c.treatment).toBeNull();
    expect(c.soldiers.some((s) => s.infection)).toBe(false);
    expect(TEMPLATES.zombie.undead).toBe(true);
  });
});
