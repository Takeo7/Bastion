/**
 * Writes engine-cs/golden/campaign/: campaigns made by the TypeScript engine (campaign.ts), for engine-cs/tests to read
 * and repeat in C# (docs/UNITY.md, hito 2).
 *
 *   npx tsx engine-cs/golden/record-campaign.ts
 *
 * states.json (the types, first part of the campaign's port): named states of campaigns made with createCampaign,
 * openingMission, addPlayer and executeCampaignCommand. Their missions are launched as the server does (room.ts
 * launchCampaignMission) and ended without being played: the enemies down, one soldier hurt, one bitten, one fallen and
 * a backpack of finds, or the whole squad down; then applyMissionResult folds them in. The ending, which a few
 * missions don't reach, is the engine's own: the finale event's answer (EVENTS.finale). CampaignJsonTests reads each
 * state into the C# CampaignState and writes it again.
 *
 * tables.json: the tables, constants and ids the C# has of campaign.ts so far (Campaign/Campaign.cs, Types/), as
 * TypeScript has them. commands.json: one command of each kind, as campaignCommandSchema takes it.
 *
 * reads.json (what only reads, second part): every reading of campaign.ts that changes nothing (beds, prices, the
 * Marea, contacts, the Taller and the Armería, the squad and its pads, what an operation names and its briefing, what a
 * soldier takes to a mission…) over each state of states.json, UI's sample and the saves of version 25, and the ones
 * that read no campaign, over all they take. CampaignReadsTests works out the same in C# and compares it whole. Texts
 * go through text-fixes.ts fixDeep, as C# says them.
 */
import { mkdirSync, readFileSync, writeFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';
import {
  activeOffer,
  baseViewSchema,
  BUNKER_BEDS,
  CAMPAIGN_VERSION,
  campaignCommandSchema,
  DAYS_PER_MISSION,
  DOOM_MAX,
  EATERS_PER_RATION,
  ENCLAVE_DAYS,
  ENCLAVES,
  FACILITIES,
  FRESH_CAMPAIGN_VERSION,
  GARDEN_RATIONS,
  HOME_REGION,
  HUNGRY_WILL,
  INFIRMARY_TURN_DAYS,
  LA_VEGA_FOOD,
  LA_VEGA_FOOD_DAYS,
  LATE_KIT,
  MAREA_SOURCE,
  MAREA_THRESHOLDS,
  MAX_SOLDIERS_PER_PLAYER,
  MEDICATE_COST,
  MEDICATE_DAYS,
  OLD_DAYS_PER_MISSION,
  QUESTS,
  RECRUIT_COST,
  REWARD_FOOD,
  REWARD_PARTS,
  ROADS,
  SIEGE_EVERY,
  SIEGE_FIRST,
  SIEGE_FIRST_IN,
  SIEGE_GROWTH,
  SIEGE_NIGHT,
  SIEGE_OPENING,
  START_FOOD,
  START_MEDICINES,
  THAW_AFTER_STORY,
  TRAITS,
  TURN_DAYS,
  TURNED_CAUSE,
  VEHICLE_FROM,
  VEHICLE_SEATS,
  addPlayer,
  ALARM_NAMES,
  applyMissionResult,
  BASE_EDGE,
  BOARD,
  buildSquad,
  campaignHackBonus,
  contactBlocker,
  craftBlocker,
  CRAFTS,
  createCampaign,
  createMission,
  defaultAppearance,
  defaultSquad,
  eventOptionAvailable,
  EVENTS,
  executeCampaignCommand,
  hasQuarantine,
  medicineCures,
  missionInfo,
  offerBriefing,
  offerMap,
  openingMission,
  PIECES,
  placeBlocker,
  podsFor,
  postBlocker,
  REGIONS,
  Rng,
  siegePlan,
  syncPads,
  unlockBlocker,
  vehicleOf,
  type Appearance,
  type CampaignCommand,
  type CampaignState,
  type CraftId,
  type GameState,
  type PieceId,
  type RegionId,
  type Slot,
} from '../../packages/engine/src/index';
import * as engine from '../../packages/engine/src/index';
import { fixDeep } from './text-fixes';

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, 'campaign');
mkdirSync(out, { recursive: true });

const both: Slot[] = [0, 1];
const json = <T>(x: T): T => JSON.parse(JSON.stringify(x)) as T;

const states: { name: string; campaign: CampaignState }[] = [];
function keep(name: string, c: CampaignState): void {
  states.push({ name, campaign: json(c) });
}

function attempt(c: CampaignState, cmd: CampaignCommand, slot: Slot, connected: readonly Slot[], rng: Rng) {
  return executeCampaignCommand(c, cmd, { slot, connected, rng });
}

function run(c: CampaignState, cmd: CampaignCommand, slot: Slot, connected: readonly Slot[], rng: Rng) {
  const r = attempt(c, cmd, slot, connected, rng);
  if (!r.ok) throw new Error(`${cmd.type}: ${r.error}`);
  return r;
}

// ------------------------------------------------------------------- missions

/** The mission of the operation chosen, as room.ts launchCampaignMission makes it. */
function launchMission(c: CampaignState, rng: Rng): GameState {
  const offer = activeOffer(c)!;
  const info = missionInfo(c, offer);
  const siege = siegePlan(c, offer);
  return createMission({
    map: offerMap(c, offer),
    ...(siege ? { siegePlan: siege } : {}),
    vehicle: vehicleOf(c),
    ...(siege && c.siegePosts ? { posts: c.siegePosts } : {}),
    slots: c.players,
    rng,
    kind: offer.kind,
    night: offer.night,
    living: offer.living,
    squad: buildSquad(c),
    pods: podsFor(offer.map, offer.pods),
    difficulty: offer.difficulty,
    hackBonus: campaignHackBonus(c),
    campaignMission: { id: offer.id, name: offer.name, ...(offer.story ? { story: offer.story } : {}), ...(info.kicker ? { kicker: info.kicker } : {}), ...(info.item ? { item: info.item } : {}), briefing: offerBriefing(c, offer) },
    vipName: info.vipName,
    ...(info.alarm ? { alarm: info.alarm, objectiveName: ALARM_NAMES[info.alarm] } : {}),
    leader: info.leader,
    itemOutdoors: info.itemOutdoors,
    medicineCures: medicineCures(c),
    quarantine: hasQuarantine(c),
    snow: info.snow,
  });
}

/**
 * Ends a mission without playing it. Won: every enemy down, and the first soldier killed two and brings a backpack of
 * finds; `hard`, the second comes back hurt, the third bitten and the fourth fell. Lost: abandoned, everyone home.
 */
function endMission(s: GameState, won: boolean, hard: boolean): void {
  const squad = s.units.filter((u) => u.campaignId);
  if (won) {
    for (const u of s.units) if (u.team === 'alien') ((u.alive = false), (u.hp = 0));
    squad.forEach((u, i) => {
      u.deeds = i === 0 ? { shot: 4, hit: 3, gunKill: 2 } : i === 1 ? { swing: 2, blow: 1 } : { search: 2, run: 1 };
      if (i === 0) {
        u.kills = 2;
        u.backpack = [{ supplies: 12 }, { item: 'firstAid' }, { weapon: 'pumpShotgun' }, { mod: 'scope' }, { food: 2 }, { parts: 1 }, { medicines: 1 }];
      }
      if (hard && i === 1) u.hp = Math.max(1, u.maxHp - 2);
      if (hard && i === 2) u.infected = true;
      if (hard && i === 3) ((u.alive = false), (u.hp = 0));
    });
    s.outcome = 'victory';
    s.outcomeReason = 'objective';
  } else {
    s.outcome = 'defeat';
    s.outcomeReason = 'abandoned';
  }
}

/** The first tile of the base where `soldier` may stand when the siege begins. */
function postFor(c: CampaignState, soldier: string): { x: number; y: number } | null {
  for (let y = BASE_EDGE; y < c.bastion.height - BASE_EDGE; y++) {
    for (let x = BASE_EDGE; x < c.bastion.width - BASE_EDGE; x++) if (!postBlocker(c, 0, soldier, { x, y })) return { x, y };
  }
  return null;
}

/** The free spot for a piece nearest the caravan, as placeBlocker allows (record-step3.ts build). */
function spotFor(c: CampaignState, id: PieceId): { x: number; y: number } | null {
  const caravan = c.bastion.pieces.find((p) => p.piece === 'caravan')!;
  const [w, h] = PIECES[id].size;
  const spots: { x: number; y: number; d: number }[] = [];
  for (let y = BASE_EDGE; y <= c.bastion.height - BASE_EDGE - h; y++) {
    for (let x = BASE_EDGE; x <= c.bastion.width - BASE_EDGE - w; x++) spots.push({ x, y, d: Math.abs(x - caravan.x - 6) + Math.abs(y - caravan.y - 4) });
  }
  spots.sort((a, b) => a.d - b.d || a.y - b.y || a.x - b.x);
  return spots.find((s) => !placeBlocker(c.bastion, id, s.x, s.y, 0)) ?? null;
}

// ------------------------------------------------------------------- the states

// A new campaign, alone and with two; a partner who comes late.
{
  const solo = createCampaign([0], new Rng(11));
  openingMission(solo);
  keep('solo-new', solo);
}
const duo = createCampaign(both, new Rng(12));
openingMission(duo);
keep('duo-new', duo);
{
  const rng = new Rng(13);
  const late = createCampaign([0], rng);
  openingMission(late);
  addPlayer(late, 1, rng);
  late.squad = defaultSquad(late, both);
  syncPads(late);
  keep('late-partner', late);
}

// Proposals waiting for the partner's vote, one of each kind a new campaign allows.
{
  const rng = new Rng(14);
  const region = (Object.keys(REGIONS) as RegionId[]).find((id) => !contactBlocker(duo, id))!;
  const other = duo.offers.find((o) => o.id !== duo.activeMission)!;
  const spot = spotFor(duo, 'palisade')!;
  const proposals: [string, CampaignCommand][] = [
    ['contact', { type: 'contact', region }],
    ['choose', { type: 'chooseMission', id: other.id }],
    ['recruit', { type: 'recruit' }],
    ['buy', { type: 'buy', item: 'fragGrenade' }],
    ['place', { type: 'place', piece: 'palisade', x: spot.x, y: spot.y, rot: 0 }],
  ];
  for (const [name, cmd] of proposals) {
    const c = json(duo);
    // A recruit needs a free bed: the eight of a new campaign fill the caravan's, so one of them has fallen.
    if (cmd.type === 'recruit') Object.assign(c.soldiers.find((s) => s.owner === 1)!, { alive: false, fellDay: c.day });
    run(c, cmd, 1, both, rng);
    keep(`proposal-${name}`, c);
  }
}

// A campaign of two played from one seat: fourteen missions, two of them hard and one lost, side quests' and the story's
// first; events answered, a treatment proposed, the siege's posts, a soldier equipped and customized, a talent bought,
// the Taller built and at work, a day waited, the Bastion up a level when it can, zones contacted, rumours listened to.
{
  const rng = new Rng(15);
  const c = createCampaign(both, rng);
  openingMission(c);
  const one: Slot[] = [0];
  const seen = new Set<string>();
  const once = (name: string) => {
    if (seen.has(name)) return;
    seen.add(name);
    keep(name, c);
  };
  for (let i = 0; i < 14 && !c.outcome; i++) {
    for (let k = 0; c.event && k < 4; k++) {
      once(c.event === 'quest' ? 'quest-decision' : 'event');
      run(c, { type: 'answerEvent', option: eventOptionAvailable(c, 0) ? 0 : 1 }, 0, one, rng);
    }
    const bitten = c.soldiers.find((s) => s.alive && s.infection);
    if (bitten && !seen.has('treatment')) {
      // Two keys: the partner's, then (not connected) never: the treatment waits.
      run(c, { type: 'treat', soldier: bitten.id, ready: true }, 1, both, rng);
      once('treatment');
      run(c, { type: 'treat', soldier: bitten.id, ready: false }, 1, both, rng);
    }
    if (i === 2) {
      run(c, { type: 'buy', item: 'fragGrenade' }, 0, one, rng);
      const mine = c.soldiers.find((s) => s.alive && s.owner === 0)!;
      run(c, { type: 'equip', soldier: mine.id, slot: 'utility', item: 'fragGrenade' }, 0, one, rng);
      const look = { ...defaultAppearance(0, undefined, mine.appearance.sex, mine.id), helmet: '#334455', torso: '#556677', character: 'hunter' };
      run(c, { type: 'customize', soldier: mine.id, name: mine.name, nickname: 'Rayo', appearance: look }, 0, one, rng);
      const cell = c.soldiers.filter((s) => s.alive && s.owner === 0).flatMap((s) => BOARD.filter((x) => !unlockBlocker(s.skills, s.board, x.id)).map((x) => ({ s, id: x.id })))[0];
      if (cell) run(c, { type: 'unlock', soldier: cell.s.id, cell: cell.id }, 0, one, rng);
    }
    // The Taller on the base, and something for it to make when it can.
    if (!c.bastion.pieces.some((p) => p.piece === 'workshop') && !activeOffer(c)?.siege) {
      const spot = spotFor(c, 'workshop');
      if (spot) attempt(c, { type: 'place', piece: 'workshop', x: spot.x, y: spot.y, rot: 0 }, 0, one, rng);
    }
    if (!c.base.works.length) {
      const project = (Object.keys(CRAFTS) as CraftId[]).find((id) => !craftBlocker(c, id));
      if (project) {
        run(c, { type: 'craft', project }, 0, one, rng);
        once('taller-at-work');
      }
    }
    if (i === 4 && !activeOffer(c)?.siege) {
      run(c, { type: 'advance', ready: true }, 0, one, rng);
      once('waited');
    }
    // The Bastion up a level when it can (the story opens a chapter a level), a zone contacted, a rumour listened to.
    if (!activeOffer(c)?.siege) {
      if (attempt(c, { type: 'levelUp' }, 0, one, rng).ok) once('levelled');
      const zone = (Object.keys(REGIONS) as RegionId[]).find((id) => !contactBlocker(c, id));
      if (zone) attempt(c, { type: 'contact', region: zone }, 0, one, rng);
      if (c.listening === null && c.rumors.length) attempt(c, { type: 'investigate', id: c.rumors[0]!.id }, 0, one, rng);
    }
    // A side quest's operation first, then the story's, then any.
    const pick = c.offers.find((o) => o.quest && !o.siege) ?? c.offers.find((o) => o.story && !o.siege) ?? c.offers.find((o) => !o.siege)!;
    if (!c.activeMission) run(c, { type: 'chooseMission', id: pick.id }, 0, one, rng);
    if (activeOffer(c)?.siege && !seen.has('siege-posts')) {
      const at = postFor(c, c.squad[0]!);
      if (at) run(c, { type: 'post', soldier: c.squad[0]!, at }, 0, one, rng);
      once('siege-posts');
    }
    // Both players' soldiers, as the squad the server picks with both connected.
    if (!activeOffer(c)?.siege) c.squad = defaultSquad(c, both);
    syncPads(c);
    run(c, { type: 'launch', ready: true }, 0, one, rng);
    const mission = launchMission(c, rng);
    endMission(mission, i !== 6, i === 0 || i === 3);
    applyMissionResult(c, mission, rng);
    keep(`mission-${i + 1}`, c);
  }
  // How it ends: the vaccine for everyone.
  const end = json(c);
  end.event = 'finale';
  EVENTS.finale.options[0].apply(end, rng);
  end.event = null;
  keep('ending', end);
}

writeFileSync(join(out, 'states.json'), JSON.stringify({ states }) + '\n');
console.log(`campaign/states.json: ${states.length} estados (${states.map((s) => s.name).join(', ')})`);

// ------------------------------------------------------------------- tables and commands

const constants = {
  BUNKER_BEDS, HOME_REGION, MAREA_THRESHOLDS, MAREA_SOURCE, RECRUIT_COST, MAX_SOLDIERS_PER_PLAYER, DOOM_MAX, OLD_DAYS_PER_MISSION,
  FRESH_CAMPAIGN_VERSION, CAMPAIGN_VERSION, START_MEDICINES, TURN_DAYS, INFIRMARY_TURN_DAYS, MEDICATE_COST, MEDICATE_DAYS, ENCLAVES,
  ENCLAVE_DAYS, TURNED_CAUSE, START_FOOD, EATERS_PER_RATION, LA_VEGA_FOOD, LA_VEGA_FOOD_DAYS, GARDEN_RATIONS, HUNGRY_WILL, REWARD_FOOD,
  REWARD_PARTS, THAW_AFTER_STORY, SIEGE_FIRST_IN, SIEGE_EVERY, SIEGE_OPENING, SIEGE_FIRST, SIEGE_GROWTH, DAYS_PER_MISSION, SIEGE_NIGHT,
  VEHICLE_SEATS, VEHICLE_FROM, LATE_KIT,
};
const ids = {
  regions: Object.keys(REGIONS),
  facilities: Object.keys(FACILITIES),
  events: [...Object.keys(EVENTS), 'quest'],
  quests: Object.keys(QUESTS),
  traits: Object.keys(TRAITS),
  baseViews: baseViewSchema.options,
};
writeFileSync(join(out, 'tables.json'), JSON.stringify({ regions: REGIONS, roads: ROADS, facilities: FACILITIES, constants, ids }) + '\n');

const painted: Appearance = { primary: '#826cbd', secondary: '#a678ff', visor: '#9fe3ff', head: 'cap', build: 'normal', sex: 'f', helmet: '#334455', character: 'hunter' };
const commands: CampaignCommand[] = [
  { type: 'place', piece: 'palisade', x: 20, y: 21, rot: 1 },
  { type: 'fell', tiles: [{ x: 3, y: 4 }, { x: 4, y: 4 }] },
  { type: 'mend', piece: 'b3' },
  { type: 'raise', piece: 'b2' },
  { type: 'placeLine', piece: 'wall', cells: [{ x: 10, y: 10 }, { x: 11, y: 10 }], rot: 0 },
  { type: 'levelUp' },
  { type: 'post', soldier: 'c1', at: { x: 30, y: 31 } },
  { type: 'post', soldier: 'c1', at: null },
  { type: 'rearm', piece: 'b7' },
  { type: 'dismantle', piece: 'b8' },
  { type: 'chooseMission', id: 'm4' },
  { type: 'contact', region: 'oce' },
  { type: 'answerEvent', option: 1 },
  { type: 'investigate', id: 'r2' },
  { type: 'cancelMission' },
  { type: 'squad', soldier: 'c3', on: false },
  { type: 'recruit' },
  { type: 'recruit', squad: true },
  { type: 'unlock', soldier: 'c2', cell: BOARD[1]!.id },
  { type: 'repair', weapon: 'w100' },
  { type: 'mod', weapon: 'w100', mod: 'scope', on: true },
  { type: 'buy', item: 'fragGrenade' },
  { type: 'equip', soldier: 'c1', slot: 'utility', item: 'firstAid' },
  { type: 'equip', soldier: 'c1', slot: 'ammo', item: null },
  { type: 'customize', soldier: 'c1', name: 'Rafael Costa', nickname: 'Rayo', appearance: painted },
  { type: 'advance', ready: true },
  { type: 'launch', ready: false },
  { type: 'treat', soldier: 'c5', ready: true },
  { type: 'medicate', soldier: 'c6' },
  { type: 'craft', project: 'ammo1' },
  { type: 'assignWeapon', weapon: 'w101', soldier: 'c2' },
  { type: 'assignWeapon', weapon: 'w101', soldier: null },
  { type: 'vote', accept: true },
  { type: 'withdraw' },
];
// As the server takes them off the wire: one the schema refuses would stop here.
for (const cmd of commands) campaignCommandSchema.parse(cmd);
writeFileSync(join(out, 'commands.json'), JSON.stringify({ commands }) + '\n');
console.log(`campaign/tables.json, campaign/commands.json: ${commands.length} órdenes`);

// ------------------------------------------------------------------- what only reads

const {
  beds, bedsFree, buildingWorks, byNight, canAfford, canSpendPoints, campaignOutdated, contactCapacity, contactCost, convoyIn, convoyIncome,
  costText, dailyRations, facilityCount, facilityLevel, floodedZones, gardenPerMission, gardenYield, garageCut, hasRadio, healRate,
  itemsFree, ITEMS, learningBonus, levelUpBlocker, listeningDelay, mareaThreshold, memorialWill, missionInfo: infoOf,
  modBlocker, mustWait, nextLevel, nightFor, offerSignature, offerVip, oldDaysToNow, proposalCost, QUESTS: QUEST_DEFS, questAvailable,
  raiseCost, rangeAim, rationsPerMission, recruitCost, regionIncome, repairBlocker, repairParts, rewardMultiplier, roadHops, roadRoute,
  sexOfPerson, shopPrice, siegeHeard, soldierArms, soldierCap, soldierReady, squadLimit, squadMax, squadPads, squadPlace, treatCost,
  turnDay, underMarea, upgradeCost, veteranEnemies, veterancyName, weaponLabel, WEAPON_MODS, wintry, workCrews, workDays, squadMember,
  NAMED_SEX, RELATIVES, SOLDIER_NAMES,
} = engine;

const conns: Slot[][] = [[0], [0, 1]];
const tilesTried = [{ x: 0, y: 0 }, { x: 2, y: 2 }, { x: 27, y: 27 }, { x: 30, y: 31 }, { x: 12, y: 40 }, { x: 44, y: 10 }];

/** Every kind of shared decision, with what a state has to give it a price; the tables' ids sorted, as C# sorts them. */
function actionsFor(c: CampaignState): engine.ProposalAction[] {
  const pieces = c.bastion.pieces.map((p) => p.id);
  return [
    ...(Object.keys(PIECES) as PieceId[]).sort().map((piece): engine.ProposalAction => ({ type: 'place', piece, x: 0, y: 0, rot: 0 })),
    ...[...pieces, 'zz'].map((piece): engine.ProposalAction => ({ type: 'raise', piece })),
    ...[...pieces, 'zz'].map((piece): engine.ProposalAction => ({ type: 'mend', piece })),
    { type: 'placeLine', piece: 'palisade', cells: [{ x: 1, y: 1 }, { x: 2, y: 1 }, { x: 3, y: 1 }], rot: 0 },
    { type: 'fell', tiles: [{ x: 1, y: 1 }, { x: 2, y: 2 }] },
    { type: 'rearm', piece: 'b1' },
    { type: 'dismantle', piece: 'b1' },
    { type: 'levelUp' },
    { type: 'contact', region: 'oce' },
    ...(Object.keys(CRAFTS) as CraftId[]).sort().map((project): engine.ProposalAction => ({ type: 'craft', project })),
    { type: 'recruit', for: 0 },
    ...(Object.keys(ITEMS) as engine.ItemId[]).sort().map((item): engine.ProposalAction => ({ type: 'buy', item })),
    { type: 'chooseMission', id: 'm1' },
    { type: 'event', option: 0 },
    { type: 'investigate', id: 'r1' },
  ];
}

function reads(c: CampaignState) {
  const siege = !!activeOffer(c)?.siege;
  const posts = Object.values(c.siegePosts ?? {});
  return {
    soldierCap: soldierCap(c), beds: beds(c), bedsFree: bedsFree(c), healRate: healRate(c), rewardMultiplier: rewardMultiplier(c),
    memorialWill: memorialWill(c), rangeAim: rangeAim(c), garageCut: garageCut(c), listeningDelay: listeningDelay(c), workCrews: workCrews(c),
    workDays: [1, 2, 3, 4, 5, 6, 7, 8].map((d) => workDays(c, d)), mustWait: mustWait(c), treatCost: treatCost(c), hasQuarantine: hasQuarantine(c),
    dailyRations: dailyRations(c), gardenYield: gardenYield(c), convoyIn: convoyIn(c), convoyIncome: convoyIncome(c),
    rationsPerMission: rationsPerMission(c), gardenPerMission: gardenPerMission(c), floodedZones: floodedZones(c), contactCost: contactCost(c),
    contactCapacity: contactCapacity(c), hasRadio: hasRadio(c), recruitCost: recruitCost(c), wintry: wintry(c), veteranEnemies: veteranEnemies(c),
    learningBonus: learningBonus(c), siegeHeard: siegeHeard(c), vehicleOf: vehicleOf(c), squadMax: squadMax(c),
    squadLimit: conns.map((k) => squadLimit(c, k)), defaultSquad: conns.map((k) => defaultSquad(c, k)), squadPads: squadPads(c),
    squadPlace: both.flatMap((slot) => conns.map((k) => squadPlace(c, slot, k))), medicineCures: medicineCures(c),
    activeOffer: activeOffer(c)?.id ?? null, campaignHackBonus: campaignHackBonus(c), nextLevel: nextLevel(c), levelUpBlocker: levelUpBlocker(c),
    canAfford: [0, 50, 100, 1000].map((k) => canAfford(c, k)), buildSquad: buildSquad(c).map((m) => m.campaignId),
    facilities: Object.fromEntries(Object.keys(FACILITIES).map((id) => [id, [facilityCount(c, id as engine.FacilityId), facilityLevel(c, id as engine.FacilityId)]])),
    items: Object.fromEntries(Object.keys(ITEMS).map((id) => [id, [shopPrice(c, id as engine.ItemId), itemsFree(c, id as engine.ItemId)]])),
    regions: Object.fromEntries((Object.keys(REGIONS) as RegionId[]).map((id) => [id, [underMarea(c, id), contactBlocker(c, id), regionIncome(c, id)]])),
    crafts: Object.fromEntries((Object.keys(CRAFTS) as CraftId[]).map((id) => [id, craftBlocker(c, id)])),
    quests: Object.fromEntries((Object.keys(QUEST_DEFS) as engine.QuestId[]).map((id) => [id, questAvailable(c, id)])),
    soldiers: c.soldiers.map((s) => ({
      id: s.id,
      ready: soldierReady(s),
      turnDay: turnDay(c, s),
      weaponLabel: weaponLabel(c, s),
      arms: soldierArms(c, s),
      veterancy: veterancyName(s),
      canSpend: canSpendPoints(c, s),
      sex: sexOfPerson(s.name) ?? null,
      member: s.alive ? squadMember(c, s) : null,
      post: [null, ...(siege ? [...tilesTried, ...posts] : [])].map((at) => postBlocker(c, s.owner, s.id, at)),
    })),
    weapons: (c.foundWeapons ?? []).map((w) => ({
      id: w.id,
      repairParts: repairParts(w),
      repair: [repairBlocker(c, 0, w.id), repairBlocker(c, 1, w.id, [0, 1]), repairBlocker(c, 1, w.id, [1])],
      mods: Object.fromEntries(Object.keys(WEAPON_MODS).map((m) => [m, [modBlocker(c, 0, w.id, m as engine.WeaponModId, true), modBlocker(c, 0, w.id, m as engine.WeaponModId, false)]])),
    })),
    pieces: c.bastion.pieces.map((p) => ({ id: p.id, works: buildingWorks(p), raise: raiseCost(p) })),
    offers: c.offers.map((o) => {
      const map = offerMap(c, o);
      const night = byNight(o);
      return {
        id: o.id,
        info: infoOf(c, o),
        vip: offerVip(c, o) ?? null,
        briefing: offerBriefing(c, o),
        signature: offerSignature(c, o),
        map: map.kind === 'base' ? { kind: 'base', sameBase: map.base === c.bastion } : map,
        siege: siegePlan(c, o) ?? null,
        night: [night === o, night.night ?? null, night.reward],
        nightFor: o.map.kind === 'generated' ? nightFor(o.map.seed) : null,
      };
    }),
    proposalCosts: actionsFor(c).map((a) => proposalCost(c, a)),
  };
}

const sources: { source: string; campaign: CampaignState }[] = [
  ...states.map((s) => ({ source: `states.json#${s.name}`, campaign: s.campaign })),
  ...['muestra.json', 'saves/v25.json', 'saves/v25-093.json'].map((f) => ({ source: f, campaign: JSON.parse(readFileSync(join(out, f), 'utf8')).campaign as CampaignState })),
];
const zones = Object.keys(REGIONS) as RegionId[];
const people = [...SOLDIER_NAMES.map((n) => n.name), ...Object.keys(NAMED_SEX), ...RELATIVES.map((r) => `${r.name} Costa`), 'Nadie Sabe', 'Xq'];
const fixed = {
  upgradeCost: Object.fromEntries(Object.keys(FACILITIES).map((id) => [id, [upgradeCost(id as engine.FacilityId, 2), upgradeCost(id as engine.FacilityId, 3)]])),
  roads: Object.fromEntries(zones.map((a) => [a, Object.fromEntries(zones.map((b) => [b, [roadRoute(a, b), roadHops(a, b)]]))])),
  mareaThreshold: Object.fromEntries(zones.map((id) => [id, mareaThreshold(id)])),
  floodedZones: Array.from({ length: 13 }, (_, doom) => floodedZones({ doom })),
  oldDaysToNow: Array.from({ length: 14 }, (_, i) => oldDaysToNow(i - 1)),
  costText: [0, 1, 35, 120].map(costText),
  campaignOutdated: Array.from({ length: 9 }, (_, i) => campaignOutdated({ version: 18 + i })),
  nightFor: [...Array.from({ length: 100 }, (_, i) => i), 123456789, 2 ** 31 - 1, 16683818, 2029091731].map((seed) => [seed, nightFor(seed)]),
  sexOfPerson: people.map((n) => [n, sexOfPerson(n) ?? null]),
};
writeFileSync(join(out, 'reads.json'), JSON.stringify(fixDeep({ fixed, states: sources.map((s) => ({ source: s.source, reads: reads(s.campaign) })) })) + '\n');
console.log(`campaign/reads.json: ${sources.length} estados`);
