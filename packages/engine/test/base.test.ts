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
  SIEGE_OPENING,
  SIEGE_GROWTH,
  siegeHeard,
  siegePlan,
  squadLimit,
  squadMember,
  upgradeCost,
  type CampaignCommand,
  type CampaignState,
} from '../src/campaign';
import { armedTiles, BASE_EDGE, bastionFootprint, bastionLot, FELL_COST, fellBlocker, fellTree, PIECES, pieceCells } from '../src/base';
import { abilitiesOf } from '../src/abilities';
import { defaultAppearance } from '../src/appearance';
import { sexOfName } from '../src/content';
import { bastionLevel } from '../src/progression';
import { Rng } from '../src/rng';
import { totalBars } from '../src/skills';
import { createMission } from '../src/setup';
import { BASE_SIEGE_TEXT, PIECE_TEXT } from '../src/lore';
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
    expect(c.base).toEqual({ works: [] });
    expect(c.siege).toMatchObject({ in: SIEGE_FIRST_IN, count: SIEGE_OPENING, held: 0 });
    // Straight after the first mission, and small (Ana and Arturo, 22/10).
    expect(SIEGE_FIRST_IN).toBe(1);
    expect(SIEGE_OPENING).toBeLessThan(SIEGE_FIRST);
    expect(c.siege.sides.length).toBeGreaterThanOrEqual(1);
    expect(c.siege.sides.length).toBeLessThanOrEqual(3);
    // Without a Sala de radio, only when.
    expect(siegeHeard(c)).toEqual({ in: SIEGE_FIRST_IN });
  });

  it('everything on the base goes to a vote with the other here, cheap or dear (18/10 #55); alone, at once; the Bastion level opens what it opens', () => {
    const c = createCampaign(both, new Rng(3));
    c.credits = 500;
    const bags = freeSpot(c, 'sandbags');
    run(c, { type: 'place', piece: 'sandbags', x: bags.x, y: bags.y, rot: 0 }, 0, both);
    expect(c.proposal?.text).toBe(`construir ${PIECE_TEXT.sandbags.a}`);
    expect(c.bastion.pieces.some((p) => p.piece === 'sandbags')).toBe(false);
    run(c, { type: 'vote', accept: true }, 1, both);
    expect(c.bastion.pieces.some((p) => p.piece === 'sandbags')).toBe(true);
    expect(c.credits).toBe(500 - PIECES.sandbags.cost.supplies);
    // Mending, setting again and taking down too.
    const placed = c.bastion.pieces.find((p) => p.piece === 'sandbags')!;
    placed.hp = 1;
    run(c, { type: 'mend', piece: placed.id }, 0, both);
    expect(c.proposal?.text).toBe(`reparar ${PIECE_TEXT.sandbags.the}`);
    run(c, { type: 'vote', accept: true }, 1, both);
    expect(placed.hp).toBe(PIECES.sandbags.hp);
    run(c, { type: 'dismantle', piece: placed.id }, 0, both);
    expect(c.proposal?.text).toBe(`desmontar ${PIECE_TEXT.sandbags.the}`);
    run(c, { type: 'vote', accept: false }, 1, both);
    expect(c.bastion.pieces).toContain(placed);
    // With the other away, it is done at once.
    run(c, { type: 'dismantle', piece: placed.id }, 0, [0]);
    expect(c.bastion.pieces).not.toContain(placed);
    // The shared supplies go to a vote too (Ana, 21/10 #31): a recruit for whoever proposes it, an accessory.
    for (const s of c.soldiers.slice(4)) s.alive = false;
    const soldiers = c.soldiers.length;
    run(c, { type: 'recruit' }, 1, both);
    expect(c.proposal?.action).toEqual({ type: 'recruit', for: 1 });
    expect(c.soldiers.length).toBe(soldiers);
    run(c, { type: 'vote', accept: true }, 0, both);
    expect(c.soldiers.length).toBe(soldiers + 1);
    expect(c.soldiers.at(-1)!.owner).toBe(1);
    run(c, { type: 'buy', item: 'smokeGrenade' }, 0, both);
    expect(c.inventory.smokeGrenade ?? 0).toBe(0);
    run(c, { type: 'vote', accept: true }, 1, both);
    expect(c.inventory.smokeGrenade).toBe(1);
    // The Sala de radio, from the caravan with its awning (Ana, 21/10 #36).
    const radio = freeSpot(c, 'radio');
    expect(() => run(c, { type: 'place', piece: 'radio', x: radio.x, y: radio.y, rot: 0 }, 0, both)).toThrow(/caravana con toldo/);
    c.level = 2;
    run(c, { type: 'place', piece: 'radio', x: radio.x, y: radio.y, rot: 0 }, 0, both);
    expect(c.proposal?.text).toBe('construir una Sala de radio');
    expect(proposalCost(c, c.proposal!.action)).toBe(PIECES.radio.cost.supplies);
    run(c, { type: 'vote', accept: true }, 1, both);
    expect(hasRadio(c)).toBe(true);
    expect(siegeHeard(c)).toEqual({ in: c.siege.in, count: c.siege.count, sides: c.siege.sides });
    const wall = freeSpot(c, 'wall');
    c.level = 1;
    expect(() => run(c, { type: 'place', piece: 'wall', x: wall.x, y: wall.y, rot: 0 }, 0, both)).toThrow(/caravana con toldo/);
    c.level = 2;
    c.parts = 0;
    expect(() => run(c, { type: 'place', piece: 'wall', x: wall.x, y: wall.y, rot: 0 }, 0, both)).toThrow(/piezas/);
  });

  it('a proposal that no longer goes through when accepted is dropped and said so, never left on both screens', () => {
    const c = createCampaign(both, new Rng(3));
    c.credits = 500;
    const bags = freeSpot(c, 'sandbags');
    run(c, { type: 'place', piece: 'sandbags', x: bags.x, y: bags.y, rot: 0 }, 0, both);
    c.credits = 0;
    run(c, { type: 'vote', accept: true }, 1, both);
    expect(c.proposal).toBeNull();
    expect(c.bastion.pieces.some((p) => p.piece === 'sandbags')).toBe(false);
    expect(c.log[0]!.text).toMatch(/^Propuesta aceptada, pero ya no se puede: construir/);
  });

  it('a row dragged is built at once, all or none, and to a vote when together it is dear (18/10 #35)', () => {
    const c = solo();
    const caravan = c.bastion.pieces[0]!;
    // A clear row of four below the Bastion's lot.
    const y = bastionLot(c.bastion)!.y + bastionLot(c.bastion)!.h + 1;
    const cells = [0, 1, 2, 3].map((i) => ({ x: caravan.x + i, y }));
    for (const t of cells) for (const i of [t.y * c.bastion.width + t.x]) c.bastion.trees = c.bastion.trees.filter((x) => x !== i);
    const credits = c.credits;
    run(c, { type: 'placeLine', piece: 'palisade', cells, rot: 0 });
    expect(c.bastion.pieces.filter((p) => p.piece === 'palisade').map((p) => [p.x, p.y])).toEqual(cells.map((t) => [t.x, t.y]));
    expect(c.credits).toBe(credits - 4 * PIECES.palisade.cost.supplies);
    // One in the way, and none goes up.
    const again = cells.map((t) => ({ x: t.x, y: t.y + 1 }));
    for (const t of again) c.bastion.trees = c.bastion.trees.filter((x) => x !== t.y * c.bastion.width + t.x);
    expect(() => run(c, { type: 'placeLine', piece: 'palisade', cells: [...again, cells[0]!], rot: 0 })).toThrow(/Ahí ya hay algo/);
    expect(c.bastion.pieces.filter((p) => p.piece === 'palisade')).toHaveLength(4);
    // Six together cost more than 30: with the partner here, a vote.
    c.players = [0, 1];
    const six = [0, 1, 2, 3, 4, 5].map((i) => ({ x: caravan.x + i, y: y + 2 }));
    for (const t of six) c.bastion.trees = c.bastion.trees.filter((x) => x !== t.y * c.bastion.width + t.x);
    run(c, { type: 'placeLine', piece: 'palisade', cells: six, rot: 0 }, 0, both);
    expect(c.proposal?.text).toBe('construir 6 × empalizada');
    run(c, { type: 'vote', accept: true }, 1, both);
    expect(c.bastion.pieces.filter((p) => p.piece === 'palisade')).toHaveLength(10);
  });

  it('felling costs and gives nothing; mending, setting again and taking down cost and give what they say', () => {
    const c = solo();
    // The forest past the clearing's edge is not felled (Ana, 22/10 #30): where felling stops, the clearing ends.
    const xy = (i: number) => ({ x: i % c.bastion.width, y: Math.floor(i / c.bastion.width) });
    const edge = c.bastion.trees.find((i) => xy(i).x < BASE_EDGE || xy(i).y < BASE_EDGE)!;
    expect(() => run(c, { type: 'fell', tiles: [xy(edge)] })).toThrow(/bosque/);
    const fellable = () => c.bastion.trees.filter((i) => !fellBlocker(c.bastion, xy(i).x, xy(i).y));
    const tree = fellable()[0]!;
    const credits = c.credits;
    run(c, { type: 'fell', tiles: [xy(tree)] });
    // Arturo, 18/10 #41: «solo cuesta recursos y no da nada».
    expect(c.credits).toBe(credits - FELL_COST);
    expect(c.bastion.trees).not.toContain(tree);
    // A tree listed twice is one tree, paid once.
    const twice = fellable()[0]!;
    const at = { x: twice % c.bastion.width, y: Math.floor(twice / c.bastion.width) };
    run(c, { type: 'fell', tiles: [at, at] });
    expect(c.credits).toBe(credits - 2 * FELL_COST);
    // Many at once, dear together: to a vote with the partner here.
    const many = fellable().slice(0, 12).map(xy);
    c.players = [0, 1];
    run(c, { type: 'fell', tiles: many }, 0, both);
    expect(c.proposal?.text).toBe('talar 12 árboles');
    run(c, { type: 'vote', accept: true }, 1, both);
    expect(c.credits).toBe(credits - 14 * FELL_COST);
    c.players = [0];
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
    // The plots of before are gone: their orders are refused like any order that doesn't exist.
    expect(() => run(c, { type: 'clear', plot: '3,5' } as unknown as CampaignCommand)).toThrow(/no existe/);
  });

  it('a building works by its facility, wherever it stands (Ana, 21/10 #41: nothing for being next to another); broken, it stops', () => {
    const c = solo();
    const far = raise(c, 'workshop');
    expect(shopPrice(c, 'smokeGrenade')).toBe(19);
    c.bastion.pieces = c.bastion.pieces.filter((p) => p !== far);
    const caravan = c.bastion.pieces[0]!;
    raise(c, 'workshop', 1, caravan);
    expect(shopPrice(c, 'smokeGrenade')).toBe(19);
    expect(recruitCost(c)).toBe(30);
    raise(c, 'training', 1, caravan);
    expect(recruitCost(c)).toBe(30);
    const infirmary = raise(c, 'infirmary');
    const rate = healRate(c);
    expect(rate).toBeGreaterThan(1);
    infirmary.hp = PIECES.infirmary.hp / 2;
    expect(healRate(c)).toBe(1);
  });

  it('the caravan sleeps the first eight; a barracón 2/3/4 beds', () => {
    const c = solo();
    expect(beds(c)).toBe(8);
    expect(() => run(c, { type: 'recruit' })).toThrow(/camas/);
    const away = raise(c, 'quarters');
    expect(beds(c)).toBe(10);
    away.level = 3;
    expect(beds(c)).toBe(12);
    run(c, { type: 'recruit' });
    expect(bedsFree(c)).toBe(beds(c) - 9);
  });

  it('a building rises a level with its facility\'s cost and the Bastion; the Sala de radio rises into communications and listening', () => {
    const c = solo();
    const shop = raise(c, 'workshop');
    expect(() => run(c, { type: 'raise', piece: shop.id })).toThrow(/la casa móvil\./);
    c.level = 3;
    const credits = c.credits;
    run(c, { type: 'raise', piece: shop.id });
    expect(shop.level).toBe(2);
    expect(c.credits).toBe(credits - upgradeCost('workshop', 2).cost);
    expect(shopPrice(c, 'smokeGrenade')).toBe(Math.round(25 * 0.65));
    const radio = raise(c, 'radio');
    expect(raiseCost(radio)).toEqual({ cost: FACILITIES.relay.cost, level: FACILITIES.relay.level });
    c.level = 7;
    const capacity = contactCapacity(c);
    run(c, { type: 'raise', piece: radio.id });
    expect(facilityLevel(c, 'relay')).toBe(1);
    expect(contactCapacity(c)).toBeGreaterThanOrEqual(capacity + 2);
    run(c, { type: 'raise', piece: radio.id });
    expect(facilityLevel(c, 'listening')).toBe(1);
    expect(listeningDelay(c)).toBeGreaterThanOrEqual(1);
    expect(raiseCost(radio)).toBe('Ya está al máximo.');
    expect(raiseCost(c.bastion.pieces[0]!)).toBe('Eso no se mejora.');
  });

  it('range, La Mula, the simulation room and training do what they say', () => {
    const c = solo();
    c.level = 8;
    const s0 = c.soldiers[0]!;
    const aim = squadMember(c, s0).mods.aim;
    const range = raise(c, 'range');
    expect(squadMember(c, s0).mods.aim).toBe(aim + 3);
    range.level = 3;
    expect(squadMember(c, s0).mods.aim).toBeGreaterThanOrEqual(aim + 10);
    raise(c, 'garage', 2);
    expect(garageCut(c).wounds).toBe(2);
    expect(squadLimit(c, [0])).toBeGreaterThan(0);
    // The squad's size is La Lata's now (18/10 #45): the training centre no longer adds to it.
    const limit = createCampaign(both, new Rng(1));
    expect(squadLimit(limit, both)).toBe(2);
    raise(limit, 'training');
    expect(squadLimit(limit, both)).toBe(2);
    // The Sala de mapas: the operations pay more suministros (the Bastion has no experience now: 18/10 #40).
    raise(c, 'simulation');
    run(c, { type: 'chooseMission', id: c.offers.find((o) => !o.story && o.reward.credits)!.id });
    const pay = activeOffer(c)!.reward.credits!;
    const credits = c.credits;
    const report = fly(c);
    expect(report.xp).toBe(0);
    expect(report.reward?.credits).toBe(Math.round(pay * 1.25));
    expect(c.credits).toBeGreaterThanOrEqual(credits + Math.round(pay * 1.25));
  });
});

describe('the sieges in the campaign (docs/FASE12.md §11.5)', () => {
  it('the clock counts missions; at 0 the siege is the next thing the base does, by every soldier fit to fight', () => {
    const c = solo();
    const lines: string[] = [];
    for (let i = 0; i < SIEGE_FIRST_IN; i++) {
      expect(activeOffer(c)?.siege).toBeUndefined();
      fly(c);
      lines.push(c.log[0]!.text);
    }
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

  it('the Bastion\'s building keeps the hits on all of it, past the caravan\'s own tiles', () => {
    const c = solo();
    c.level = 5;
    c.bastion.level = 5;
    c.siege.in = 0;
    fly(c);
    const caravan = c.bastion.pieces.find((p) => p.piece === 'caravan')!;
    const own = pieceCells('caravan', caravan.x, caravan.y, caravan.rot);
    const outer = bastionFootprint(c.bastion).find((t) => !own.some((o) => o.x === t.x && o.y === t.y))!;
    fly(c, 'victory', (s) => {
      s.breakables![outer.y * s.width + outer.x] = 5;
    });
    expect(caravan.hp).toBe(5);
  });

  it('nobody starts a siege on a turret, a trap or the wire, posted there before it was built or not', () => {
    const c = solo();
    c.siege.in = 0;
    fly(c);
    raise(c, 'turret');
    const spot = freeSpot(c, 'mine');
    fellTree(c.bastion, spot.x, spot.y);
    run(c, { type: 'post', soldier: c.squad[0]!, at: spot });
    expect(raise(c, 'mine')).toMatchObject({ x: spot.x, y: spot.y });
    const armed = armedTiles(c.bastion);
    expect(armed.size).toBe(2);
    const offer = activeOffer(c)!;
    const s = createMission({ map: offerMap(c, offer), siegePlan: siegePlan(c, offer)!, posts: c.siegePosts, slots: c.players, rng: new Rng(2), kind: offer.kind, squad: buildSquad(c), pods: offer.pods });
    const defenders = s.units.filter((u) => u.team === 'xcom' && !u.automaton);
    expect(defenders.length).toBe(c.squad.length);
    for (const u of defenders) expect(armed.has(u.pos.y * s.width + u.pos.x)).toBe(false);
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
    // Once someone can go out, waiting is a day at a time (Ana and Arturo, 22/10).
    expect(mustWait(c)).toBe(false);
    const now = c.day;
    run(c, { type: 'advance', ready: true });
    expect(c.day).toBe(now + 1);
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
  }, 20_000);

  it('each places their own defenders anywhere free on the clearing, and the siege begins with them there (18/10 #58)', () => {
    const c = createCampaign(both, new Rng(3));
    c.nextEventDay = 999;
    c.siege.in = 0;
    fly(c);
    expect(activeOffer(c)?.siege).toBe(true);
    const mine = c.soldiers.find((s) => s.owner === 0 && c.squad.includes(s.id))!;
    const theirs = c.soldiers.find((s) => s.owner === 1 && c.squad.includes(s.id))!;
    const lot = bastionLot(c.bastion)!;
    const spot = { x: lot.x - 3, y: lot.y - 3 };
    for (let x = spot.x - 1; x <= spot.x + 1; x++) fellTree(c.bastion, x, spot.y);
    expect(() => run(c, { type: 'post', soldier: theirs.id, at: spot }, 0, both)).toThrow('Solo puedes colocar a tus soldados.');
    expect(() => run(c, { type: 'post', soldier: mine.id, at: { x: 0, y: 5 } }, 0, both)).toThrow(/borde/);
    const wall = bastionFootprint(c.bastion)[0]!;
    expect(() => run(c, { type: 'post', soldier: mine.id, at: wall }, 0, both)).toThrow('Ahí no se puede estar.');
    run(c, { type: 'post', soldier: mine.id, at: spot }, 0, both);
    expect(() => run(c, { type: 'post', soldier: theirs.id, at: spot }, 1, both)).toThrow('Ahí ya hay alguien.');
    expect(c.siegePosts).toEqual({ [mine.id]: spot });
    const offer = activeOffer(c)!;
    const s = createMission({ map: offerMap(c, offer), siegePlan: siegePlan(c, offer)!, posts: c.siegePosts, slots: c.players, rng: new Rng(2), kind: offer.kind, squad: buildSquad(c), pods: offer.pods });
    expect(s.units.find((u) => u.campaignId === mine.id)!.pos).toMatchObject(spot);
    // The rest, by the Bastion, none on the posted tile.
    expect(s.units.filter((u) => u.team === 'xcom' && u.pos.x === spot.x && u.pos.y === spot.y)).toHaveLength(1);
    // Once the siege is over, the posts go.
    fly(c);
    expect(c.siegePosts).toBeUndefined();
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
  it('a sabotage mended slowly leaves a building without power, not working until it is mended', () => {
    const c = solo();
    const radio = raise(c, 'radio');
    expect(hasRadio(c)).toBe(true);
    c.event = 'sabotage';
    run(c, { type: 'answerEvent', option: 1 });
    expect(radio.hp).toBe(Math.floor(PIECES.radio.hp / 2));
    expect(hasRadio(c)).toBe(false);
    run(c, { type: 'mend', piece: radio.id });
    expect(hasRadio(c)).toBe(true);
  });

  it('contacting regions costs credits, grows the monthly payment and is capped by communications', () => {
    const c = solo();
    expect(contactCapacity(c)).toBe(2);
    run(c, { type: 'contact', region: 'oce' });
    expect(() => run(c, { type: 'contact', region: 'afr' })).toThrow(/Sala de radio/);
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
      c.level = 7;
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
    const offers = c.offers.length;
    run(c, { type: 'answerEvent', option: 0 }, 0, both);
    expect(c.proposal?.text).toMatch(/Interrogarlo/);
    run(c, { type: 'vote', accept: true }, 1, both);
    // What he knows: where to go, a new operation (18/10 #40).
    expect(c.offers.length).toBe(offers + 1);
    expect(c.event).toBeNull();
    expect(Object.keys(EVENTS).length).toBeGreaterThanOrEqual(8);
  });
});

describe('alien bodies', () => {
  it('are sold after a won mission, and left behind after a lost one', () => {
    const c = solo();
    // Two missions in a row, no siege between them.
    c.siege.in = 5;
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
    run(c, { type: 'vote', accept: true }, 1, both);
    run(c, { type: 'equip', soldier: mine.id, slot: 'utility', item: 'smokeGrenade' }, 0, both);
    expect(itemsFree(c, 'smokeGrenade')).toBe(0);
    expect(() => run(c, { type: 'equip', soldier: theirs.id, slot: 'utility', item: 'smokeGrenade' }, 1, both)).toThrow(/libres/);
    expect(() => run(c, { type: 'equip', soldier: theirs.id, slot: 'utility', item: 'firstAid' }, 0, both)).toThrow(/tus soldados/);
    expect(() => run(c, { type: 'buy', item: 'mindShield' }, 0, both)).toThrow(/albergue ampliado/);
    // The belt has two places for any accessory (docs/FASE9.md §4); special rounds go no more.
    run(c, { type: 'buy', item: 'smokeGrenade' }, 0, both);
    run(c, { type: 'vote', accept: true }, 1, both);
    run(c, { type: 'equip', soldier: mine.id, slot: 'ammo', item: 'smokeGrenade' }, 0, both);
    expect(itemsFree(c, 'smokeGrenade')).toBe(0);
    c.inventory.apRounds = 1;
    expect(() => run(c, { type: 'equip', soldier: mine.id, slot: 'ammo', item: 'apRounds' }, 0, both)).toThrow(/cinturón/);
  });

  it('equipment reaches the battlefield and is lost with its bearer', () => {
    const c = solo();
    const s = c.soldiers[0]!;
    c.level = 4;
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
    expect(s.appearance).toEqual(defaultAppearance(0, undefined, sexOfName(s.name), s.id));
  });
});
