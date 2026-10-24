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
  fellBlocker,
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
/**
 * EXTRA_GAMES=<folder> EXTRA_SEEDS=<n>: only whole campaigns, n of them by seed, into that folder (outside git): a wider
 * check than the recorded ones, for CampaignGamesTests with CAMPAIGN_GAMES=<folder>.
 */
const extra = process.env.EXTRA_GAMES;

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

if (!extra) writeFileSync(join(out, 'states.json'), JSON.stringify({ states }) + '\n');
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
if (!extra) writeFileSync(join(out, 'tables.json'), JSON.stringify({ regions: REGIONS, roads: ROADS, facilities: FACILITIES, constants, ids }) + '\n');

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
if (!extra) writeFileSync(join(out, 'commands.json'), JSON.stringify({ commands }) + '\n');
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
if (!extra) writeFileSync(join(out, 'reads.json'), JSON.stringify(fixDeep({ fixed, states: sources.map((s) => ({ source: s.source, reads: reads(s.campaign) })) })) + '\n');
console.log(`campaign/reads.json: ${sources.length} estados`);

// ------------------------------------------------------------------- whole campaigns

/*
 * games/*.json (the rules, third part): whole campaigns played by a bot, operation by operation, as the room plays them:
 * a command with its seat and who is connected (the result, ok or why not), a mission over (createMission with an Rng
 * of its own, ended without playing it, then applyMissionResult with the campaign's), a partner who comes late, the
 * time going on, a save migrated; and now and then a look at what the screens read (nextSteps of both seats, the
 * squad going out, what the operation names). One Rng for the campaign the whole game, as the room has; the bot's
 * choices and the missions draw on others. Each operation keeps the campaign's Rng after it and how the campaign
 * changed (`delta`: the keys that changed; a list of the same length by its items, `$items`; the log by what came in
 * front, `$unshift`), through fixDeep. CampaignGamesTests plays the same in C# and compares the campaign after each.
 */

import { applyMissionResult as foldMission, advanceTime, migrateCampaign, nextSteps, FOUND_WEAPONS, TEMPLATES, WEAPON_MODS as MODS, ITEMS as ITEM_DEFS, BOARD as BOARD_CELLS_LIST, ENDING_TITLES, epilogue, bastionLevel, raiseCost as raiseCostOf, rearmCost, repairCost } from '../../packages/engine/src/index';
import type { GameState as Mission, Loot } from '../../packages/engine/src/index';

/** How `now` differs from `prev` (plain JSON campaigns, key by key). */
function campaignDelta(prev: Record<string, any>, now: Record<string, any>): Record<string, unknown> {
  const d: Record<string, unknown> = {};
  for (const k of new Set([...Object.keys(prev), ...Object.keys(now)])) {
    const a = prev[k];
    const b = now[k];
    if (canonOf(a) === canonOf(b)) continue;
    if (k === 'log' && Array.isArray(a) && Array.isArray(b)) {
      // What came in front, the old ones pushed back (and the last ones gone past 40).
      let n = 0;
      while (n <= b.length && canonOf(b.slice(n)) !== canonOf(a.slice(0, b.length - n))) n++;
      if (n <= b.length) {
        d.log = { $unshift: b.slice(0, n), length: b.length };
        continue;
      }
    }
    if (Array.isArray(a) && Array.isArray(b) && a.length === b.length) {
      const items: Record<string, unknown> = {};
      b.forEach((v, i) => {
        if (canonOf(a[i]) !== canonOf(v)) items[i] = v ?? null;
      });
      d[k] = { $items: items };
      continue;
    }
    d[k] = b ?? null;
  }
  return d;
}

/** JSON with its keys sorted and nulls left out, to tell whether two values are the same. */
function canonOf(v: unknown): string {
  if (v === null || v === undefined) return 'null';
  if (Array.isArray(v)) return `[${v.map(canonOf).join(',')}]`;
  if (typeof v === 'object') {
    const o = v as Record<string, unknown>;
    return `{${Object.keys(o).filter((k) => o[k] !== undefined && o[k] !== null).sort().map((k) => `${JSON.stringify(k)}:${canonOf(o[k])}`).join(',')}}`;
  }
  return JSON.stringify(v);
}

/** What applyMissionResult reads of a mission over: the squad whole; of the rest, what it is and how it ended. */
function stripMission(s: Mission): Record<string, unknown> {
  const units = s.units.map((u) =>
    TEMPLATES[u.template].team === 'xcom' || u.campaignId
      ? u
      : { id: u.id, template: u.template, team: u.team, owner: u.owner, pos: u.pos, hp: u.hp, maxHp: u.maxHp, alive: u.alive, evacuated: u.evacuated, objective: u.objective, kills: u.kills, ...(u.automaton ? { automaton: u.automaton, ammo: u.ammo } : {}) },
  );
  return json({
    mapId: s.mapId, mapName: s.mapName, biome: s.biome, width: s.width, height: s.height, tiles: [], elev: [], units, pods: [], mission: s.mission,
    turn: s.turn, activeTeam: s.activeTeam, concealed: s.concealed, ready: s.ready, smoke: [], outcome: s.outcome, outcomeReason: s.outcomeReason,
    ...(s.base ? { base: s.base } : {}), ...(s.breakables ? { breakables: s.breakables } : {}), ...(s.traps ? { traps: s.traps } : {}),
  });
}

const UTILITY = (Object.keys(ITEM_DEFS) as engine.ItemId[]).filter((id) => ITEM_DEFS[id].slot === 'utility');
const FOUND = Object.keys(FOUND_WEAPONS) as engine.FoundWeaponId[];
const PRIMARY = FOUND.filter((k) => FOUND_WEAPONS[k].slot === 'primary');
const MOD_IDS = Object.keys(MODS) as engine.WeaponModId[];

/** Ends a mission without playing it, as the bot decides: won or not, and how each soldier came out of it. */
function endMissionBy(s: Mission, bot: Rng): void {
  const siege = !!s.mission.siege;
  const won = bot.int(30) >= (siege ? 3 : 1);
  const squad = s.units.filter((u) => u.campaignId);
  if (won) {
    for (const u of s.units) if (u.team === 'alien' && !u.automaton) ((u.alive = false), (u.hp = 0));
    s.outcome = 'victory';
    s.outcomeReason = 'objective';
  } else {
    s.outcome = 'defeat';
    s.outcomeReason = bot.pick(['wiped', 'abandoned', 'timer'] as const);
  }
  for (const u of squad) {
    const deeds: Record<string, number> = {};
    for (const k of ['shot', 'hit', 'gunKill', 'swing', 'blow', 'throw', 'search', 'healOther', 'run'] as const) {
      const n = bot.int(5) - 1;
      if (n > 0) deeds[k] = n;
    }
    u.deeds = deeds as engine.Deeds;
    u.kills = bot.int(3);
    if (bot.int(4) === 0) u.hp = Math.max(1, u.maxHp - 1 - bot.int(3));
    if (bot.int(8) === 0) u.infected = true;
    const fate = bot.int(40);
    if ((!won && s.outcomeReason === 'wiped') || fate === 0) ((u.alive = false), (u.hp = 0));
    else if (fate === 1) u.lost = true;
    else if (fate === 2) ((u.evacuated = true), (u.alive = false));
    if (bot.int(3) === 0) {
      const pack: Loot[] = [];
      for (let i = bot.int(4); i > 0; i--) {
        const k = bot.int(7);
        if (k === 0) pack.push({ supplies: 5 + bot.int(20) });
        else if (k === 1) pack.push({ item: bot.pick(UTILITY) });
        else if (k === 2) pack.push({ medicines: 1 });
        else if (k === 3) pack.push({ food: 1 + bot.int(3) });
        else if (k === 4) pack.push({ parts: 1 + bot.int(2) });
        else if (k === 5) pack.push({ weapon: bot.pick(FOUND), ...(bot.int(2) ? { condition: 40 + bot.int(60) } : {}), ...(bot.int(3) === 0 ? { mods: [bot.pick(MOD_IDS)] } : {}) });
        else pack.push({ mod: bot.pick(MOD_IDS) });
      }
      u.backpack = pack;
    }
    // A find taken up instead of its own, which comes back in the backpack (docs/FASE8.md §C).
    if (u.arms?.primaryId && bot.int(8) === 0) {
      // Carried home in the backpack, or left out there (and lost).
      if (bot.int(2) === 0) (u.backpack ??= []).push({ weapon: u.arms.primary!, weaponId: u.arms.primaryId });
      u.arms = { ...u.arms, primary: bot.pick(PRIMARY) };
      delete u.arms.primaryId;
      delete u.arms.condition;
      delete u.arms.mods;
    } else if (u.arms?.primaryId) u.arms.condition = Math.max(0, (u.arms.condition ?? 100) - bot.int(40));
  }
  if (siege) {
    // What the dead broke down: a few doors and barricades, the traps gone off, the turrets as they ended.
    s.breakables ??= {};
    for (const node of Object.keys(s.breakables).slice(0, 6)) if (bot.int(2) === 0) s.breakables[Number(node)] = bot.int(3) === 0 ? 0 : Math.max(0, s.breakables[Number(node)]! - 1 - bot.int(4));
    for (const t of s.traps ?? []) if (bot.int(2) === 0) t.charges = Math.max(0, t.charges - 1);
    for (const u of s.units) {
      if (!u.automaton) continue;
      if (bot.int(3) === 0) u.hp = Math.max(0, u.hp - 2);
      if (bot.int(2) === 0) u.ammo = Math.max(0, u.ammo - 2);
    }
  }
}

/** Ends a mission as the story bot's help has it: won (or `won` false: abandoned, everyone home), nobody hurt or bitten. */
function endMissionHelped(s: Mission, bot: Rng, won: boolean): void {
  if (won) {
    for (const u of s.units) if (u.team === 'alien' && !u.automaton) ((u.alive = false), (u.hp = 0));
    s.outcome = 'victory';
    s.outcomeReason = 'objective';
  } else {
    s.outcome = 'defeat';
    s.outcomeReason = 'abandoned';
  }
  for (const u of s.units.filter((x) => x.campaignId)) {
    u.deeds = { shot: 1 + bot.int(4), hit: bot.int(3), search: bot.int(2) } as engine.Deeds;
    u.kills = bot.int(3);
    if (bot.int(4) === 0) u.backpack = [{ supplies: 10 + bot.int(20) }, { parts: 1 + bot.int(2) }];
  }
  // A siege spends the traps and the turrets' rounds, and beats a few doors.
  for (const t of s.traps ?? []) t.charges = Math.max(0, t.charges - 1 - bot.int(2));
  for (const u of s.units) if (u.automaton) u.ammo = Math.max(0, u.ammo - 1 - bot.int(3));
  if (s.breakables) for (const node of Object.keys(s.breakables).slice(0, 3)) s.breakables[Number(node)] = Math.max(1, s.breakables[Number(node)]! - 1);
}

interface GamePlan {
  name: string;
  seed: number;
  /** A new campaign of these seats, or a saved one (golden/campaign/…), migrated if it is older. */
  start: { create: Slot[] } | { save: string };
  missions: number;
  /** After this many missions the second player comes (a solo campaign). */
  lateJoin?: number;
  /** One in this many rounds the partner is away. */
  away?: number;
  /** Plays for the end: no waiting by choice, and the answers that hold the Marea back. */
  careful?: boolean;
  /**
   * Plays the story to its end, helped in the field (every mission won, nobody hurt): the Bastion up as soon as it can
   * be, the Nidos burnt, the Sala de radio up to its Estación de escucha, traps set and set again after each siege, and
   * the vaccine answered with `ending` (0 for everyone, 1 for those who kept their deals). `loseFinal`: the final mission
   * lost once before it is won.
   */
  story?: { ending: 0 | 1; loseFinal?: boolean };
}

const GAMES: GamePlan[] = [
  { name: 'duo-a', seed: 101, start: { create: [0, 1] }, missions: 60 },
  { name: 'duo-b', seed: 202, start: { create: [0, 1] }, missions: 30, away: 4 },
  { name: 'duo-c', seed: 303, start: { create: [0, 1] }, missions: 30 },
  { name: 'duo-d', seed: 606, start: { create: [0, 1] }, missions: 30, away: 2 },
  { name: 'solo-a', seed: 404, start: { create: [0] }, missions: 25 },
  { name: 'solo-late', seed: 505, start: { create: [0] }, missions: 25, lateJoin: 3 },
  { name: 'solo-early', seed: 515, start: { create: [0] }, missions: 20, lateJoin: 0 },
  { name: 'sample', seed: 707, start: { save: 'muestra.json' }, missions: 15 },
  { name: 'from-v20', seed: 808, start: { save: 'saves/v20.json' }, missions: 15 },
  { name: 'from-v23', seed: 909, start: { save: 'saves/v23-migrada.json' }, missions: 15 },
  { name: 'duo-long', seed: 111, start: { create: [0, 1] }, missions: 80, careful: true },
  { name: 'solo-long', seed: 222, start: { create: [0] }, missions: 80, careful: true },
  { name: 'final-break', seed: 333, start: { create: [0, 1] }, missions: 80, story: { ending: 0, loseFinal: true } },
  { name: 'final-record', seed: 445, start: { create: [0, 1] }, missions: 80, story: { ending: 1 } },
];

function playGame(plan: GamePlan): void {
  const rng = new Rng(plan.seed);
  const bot = new Rng(plan.seed * 31 + 7);
  let c: CampaignState;
  if ('create' in plan.start) c = createCampaign(plan.start.create, rng);
  else {
    c = fixDeep(JSON.parse(readFileSync(join(out, plan.start.save), 'utf8')).campaign as CampaignState);
    // UI's sample was written by hand: its Taller's work lacks what the type has, given as C# reads it missing.
    for (const w of c.base.works) Object.assign(w, { plot: w.plot ?? '', kind: w.kind ?? 'craft', total: w.total ?? 0 });
  }
  const campaign0 = fixDeep(json(c));
  const rng0 = rng.state;
  let prev = campaign0 as Record<string, any>;
  const ops: Record<string, unknown>[] = [];
  const record = (op: Record<string, unknown>) => {
    const now = fixDeep(json(c)) as Record<string, any>;
    ops.push({ ...op, rng: rng.state, delta: campaignDelta(prev, now) });
    prev = now;
  };
  const cmd = (slot: Slot, connected: Slot[], command: CampaignCommand) => {
    const r = executeCampaignCommand(c, command, { slot, connected, rng });
    record({ op: 'cmd', slot, connected, cmd: command, result: fixDeep(r) });
    return r;
  };
  const look = () => {
    const offer = activeOffer(c);
    record({
      op: 'look',
      reads: fixDeep(json({
        next: [nextSteps(c, 0, 'Ana'), nextSteps(c, 1)],
        ...(offer ? { squad: buildSquad(c), info: missionInfo(c, offer), briefing: offerBriefing(c, offer), signature: offerSignature(c, offer) } : {}),
      })),
    });
  };

  if ('save' in plan.start && (c.version as number) < 25) {
    c = migrateCampaign(c);
    record({ op: 'migrate' });
  }
  if ('create' in plan.start) {
    openingMission(c);
    record({ op: 'opening' });
  }

  let flown = 0;
  let lostFinal = false;
  /** A round of the story bot (GamePlan.story): one thing done, the most pressing first. */
  const storyRound = (story: NonNullable<GamePlan['story']>) => {
    const connected: Slot[] = c.players.length > 1 ? [0, 1] : [0];
    const me: Slot = bot.pick(connected);
    const siege = !!activeOffer(c)?.siege;
    if (c.proposal) {
      cmd(c.proposal.by === 0 ? 1 : 0, connected, { type: 'vote', accept: true });
      return;
    }
    if (c.event) {
      const holds = c.event === 'finale' ? story.ending : c.event === 'convoy' && eventOptionAvailable(c, 0) ? 0 : c.event === 'defector' ? 1 : null;
      cmd(me, connected, { type: 'answerEvent', option: holds ?? (eventOptionAvailable(c, 0) ? 0 : 1) });
      return;
    }
    // Something refused just now: on to the missions, not the same thing again.
    const last = ops[ops.length - 1];
    const refused = !!last && last.op === 'cmd' && !(last.result as { ok: boolean }).ok;
    if (!siege && !refused) {
      if (!levelUpBlocker(c)) {
        cmd(me, connected, { type: 'levelUp' });
        return;
      }
      const nest = c.regions.find((r) => r.facility && !contactBlocker(c, r.id));
      if (nest) {
        cmd(me, connected, { type: 'contact', region: nest.id });
        return;
      }
      const radio = c.bastion.pieces.find((p) => p.piece === 'radio');
      const spot = radio ? null : spotFor(c, 'radio');
      if (spot && bastionLevel(c) >= PIECES.radio.level && c.credits >= PIECES.radio.cost.supplies + 60) {
        cmd(me, connected, { type: 'place', piece: 'radio', x: spot.x, y: spot.y, rot: 0 });
        return;
      }
      const up = radio && raiseCostOf(radio);
      if (radio && up && typeof up !== 'string' && bastionLevel(c) >= up.level && c.credits >= up.cost + 80) {
        cmd(me, connected, { type: 'raise', piece: radio.id });
        return;
      }
      // Traps by the Bastion, and set again once a siege has spent them.
      const traps = c.bastion.pieces.filter((p) => PIECES[p.piece].kind === 'trap');
      const trap: PieceId = bastionLevel(c) >= 3 && (c.parts ?? 0) >= 1 ? 'mine' : 'spikes';
      const at = traps.length < 4 && bastionLevel(c) >= PIECES[trap].level && c.credits >= 60 ? spotFor(c, trap) : null;
      if (at) {
        cmd(me, connected, { type: 'place', piece: trap, x: at.x, y: at.y, rot: 0 });
        return;
      }
      const spent = c.bastion.pieces.find((p) => rearmCost(p) > 0 && (c.parts ?? 0) >= rearmCost(p));
      if (spent) {
        cmd(me, connected, { type: 'rearm', piece: spent.id });
        return;
      }
      const broken = c.bastion.pieces.find((p) => p.hp < PIECES[p.piece].hp && c.credits >= repairCost(p).supplies + 60 && (c.parts ?? 0) >= (repairCost(p).parts ?? 0));
      if (broken) {
        cmd(me, connected, { type: 'mend', piece: broken.id });
        return;
      }
    }
    const offer = activeOffer(c);
    if (!offer) {
      if (mustWait(c)) {
        for (const slot of connected) cmd(slot, connected, { type: 'advance', ready: true });
        return;
      }
      // The end first; then a Nido, the story, what sets the Marea back; then any.
      const best = (o: engine.MissionOffer) => (o.final ? 100 : 0) + (o.facility ? 30 : 0) + (o.story ? 20 : 0) + (o.reward.doom ?? 0) * 5;
      cmd(me, connected, { type: 'chooseMission', id: [...c.offers].sort((a, b) => best(b) - best(a))[0]!.id });
      return;
    }
    let launch = false;
    for (const slot of connected) if (cmd(slot, connected, { type: 'launch', ready: true }).ok) launch ||= !!(ops[ops.length - 1]!.result as { launch?: boolean }).launch;
    if (!launch) {
      if (!c.squad.length) {
        c.squad = defaultSquad(c, connected);
        syncPads(c);
        record({ op: 'defaultSquad', connected });
      }
      return;
    }
    look();
    const mission = launchMission(c, new Rng(plan.seed * 1000 + flown));
    const lose = !!story.loseFinal && offer.final && !lostFinal;
    if (lose) lostFinal = true;
    endMissionHelped(mission, bot, !lose);
    const stripped = stripMission(mission);
    foldMission(c, mission, rng);
    record({ op: 'mission', mission: stripped });
    flown++;
  };
  for (let round = 0; round < 3000 && flown < plan.missions && !c.outcome; round++) {
    if (plan.story) {
      storyRound(plan.story);
      continue;
    }
    if (plan.lateJoin !== undefined && flown >= plan.lateJoin && !c.players.includes(1)) {
      const got = addPlayer(c, 1, rng);
      // The room's bringPlayersIn: a squad being picked is redone for both.
      if (c.activeMission && !activeOffer(c)?.siege) {
        c.squad = defaultSquad(c, both);
        syncPads(c);
      }
      record({ op: 'addPlayer', slot: 1, got });
    }
    const two = c.players.length > 1;
    const connected: Slot[] = two && !(plan.away && bot.int(plan.away) === 0) ? [0, 1] : [0];
    const me: Slot = connected.length > 1 ? bot.pick(connected) : 0;
    const partner: Slot = me === 0 ? 1 : 0;
    const mine = c.soldiers.filter((s) => s.owner === me && s.alive);

    // A proposal waits: the other one answers it (and now and then says no).
    if (c.proposal) {
      const by = c.proposal.by;
      const voter: Slot = by === 0 ? 1 : 0;
      if (connected.includes(voter)) cmd(voter, connected, { type: 'vote', accept: bot.int(6) !== 0 });
      else cmd(by, connected, { type: 'withdraw' });
      continue;
    }
    // A treatment one proposed: the other key.
    if (c.treatment && connected.length > 1) {
      const other = connected.find((slot) => !c.treatment!.ready[slot]);
      if (other !== undefined) {
        cmd(other, connected, { type: 'treat', soldier: c.treatment.soldier, ready: true });
        continue;
      }
    }
    if (c.event) {
      // Careful, the answers that hold the Marea back: someone to the convoy, the deserter to lead a horde away.
      const holds = plan.careful ? (c.event === 'convoy' && eventOptionAvailable(c, 0) ? 0 : c.event === 'defector' ? 1 : null) : null;
      const option = holds ?? (eventOptionAvailable(c, 0) && (bot.int(3) !== 0 || !eventOptionAvailable(c, 1)) ? 0 : 1);
      cmd(me, connected, { type: 'answerEvent', option });
      continue;
    }

    // What a commander does at the base, a few things a round.
    for (let k = bot.int(3); k > 0; k--) {
      const what = bot.int(22);
      const s = mine.length ? bot.pick(mine) : undefined;
      const weapons = c.foundWeapons ?? [];
      const pieces = c.bastion.pieces;
      if (what === 0) cmd(me, connected, { type: 'buy', item: bot.pick(UTILITY) });
      else if (what === 1 && s) cmd(me, connected, { type: 'equip', soldier: s.id, slot: bot.pick(['utility', 'ammo'] as const), item: bot.int(4) ? bot.pick(UTILITY) : null });
      else if (what === 2 && s) cmd(me, connected, { type: 'customize', soldier: s.id, name: bot.int(5) ? s.name : `  ${s.name}  `, nickname: bot.pick(['', 'Rayo', 'Tejón', 'La Sombra']), appearance: { ...s.appearance, ...(bot.int(2) ? { helmet: '#445566' } : {}), ...(bot.int(3) === 0 ? { head: 'cap' as const } : {}) } });
      else if (what === 3 && s) {
        const cell = BOARD_CELLS_LIST.filter((x) => !unlockBlocker(s.skills, s.board, x.id))[0]?.id ?? bot.pick(BOARD_CELLS_LIST).id;
        cmd(me, connected, { type: 'unlock', soldier: s.id, cell });
      } else if (what === 4 && weapons.length) cmd(me, connected, { type: 'assignWeapon', weapon: bot.pick(weapons).id, soldier: s && bot.int(4) ? s.id : null });
      else if (what === 5 && weapons.length) cmd(me, connected, { type: 'repair', weapon: bot.pick(weapons).id });
      else if (what === 6 && weapons.length) cmd(me, connected, { type: 'mod', weapon: bot.pick(weapons).id, mod: bot.pick(MOD_IDS), on: bot.int(3) !== 0 });
      else if (what === 7) {
        const hurt = mine.filter((x) => x.woundedDays > 0);
        if (hurt.length) cmd(me, connected, { type: 'medicate', soldier: bot.pick(hurt).id });
      } else if (what === 8) {
        const bitten = c.soldiers.filter((x) => x.alive && x.infection);
        if (bitten.length) cmd(me, connected, { type: 'treat', soldier: bot.pick(bitten).id, ready: bot.int(5) !== 0 });
      } else if (what === 9) cmd(me, connected, { type: 'recruit', ...(bot.int(2) ? { squad: true } : {}) });
      else if (what === 10) cmd(me, connected, { type: 'levelUp' });
      else if (what === 11) {
        const zone = (Object.keys(REGIONS) as RegionId[]).filter((id) => !contactBlocker(c, id));
        cmd(me, connected, { type: 'contact', region: zone.length ? bot.pick(zone) : bot.pick(Object.keys(REGIONS) as RegionId[]) });
      } else if (what === 12 && c.rumors.length) cmd(me, connected, { type: 'investigate', id: bot.pick(c.rumors).id });
      else if (what === 13) {
        const project = (Object.keys(CRAFTS) as CraftId[]).find((id) => !craftBlocker(c, id)) ?? bot.pick(Object.keys(CRAFTS) as CraftId[]);
        cmd(me, connected, { type: 'craft', project });
      } else if (what === 14) {
        const id = bot.pick(['palisade', 'sandbags', 'quarters', 'workshop', 'radio', 'garden', 'infirmary', 'mine', 'spikes', 'quarantine', 'tower', 'floodlight', 'turret', 'memorial'] as PieceId[]);
        const spot = spotFor(c, id);
        if (spot) cmd(me, connected, { type: 'place', piece: id, x: spot.x, y: spot.y, rot: 0 });
      } else if (what === 15 && pieces.length) cmd(me, connected, { type: 'raise', piece: bot.pick(pieces).id });
      else if (what === 16 && pieces.length) {
        // What a siege left: beaten pieces to mend, traps and turrets to set again; or any.
        const worn = pieces.filter((p) => p.hp < PIECES[p.piece].hp || (p.charges ?? 0) < (PIECES[p.piece].charges ?? 0));
        const p = worn.length && bot.int(4) ? bot.pick(worn) : bot.pick(pieces);
        cmd(me, connected, { type: p.hp < PIECES[p.piece].hp ? 'mend' : 'rearm', piece: p.id });
      }
      else if (what === 17 && pieces.length > 1 && bot.int(4) === 0) cmd(me, connected, { type: 'dismantle', piece: bot.pick(pieces.filter((p) => p.piece !== 'caravan')).id });
      else if (what === 18) {
        const trees = c.bastion.trees.filter((t) => !fellBlocker(c.bastion, t % c.bastion.width, Math.floor(t / c.bastion.width)));
        if (trees.length) {
          const t = bot.pick(trees);
          const tiles = [{ x: t % c.bastion.width, y: Math.floor(t / c.bastion.width) }];
          if (bot.int(2)) tiles.push({ ...tiles[0]! });
          cmd(me, connected, { type: 'fell', tiles });
        }
      } else if (what === 19) {
        const spot = spotFor(c, 'palisade');
        if (spot) cmd(me, connected, { type: 'placeLine', piece: 'palisade', cells: [{ x: spot.x, y: spot.y }, { x: spot.x + 1, y: spot.y }, { x: spot.x + 2, y: spot.y }], rot: 0 });
      } else if (what === 20 && c.activeMission && s) cmd(me, connected, { type: 'squad', soldier: s.id, on: bot.int(2) === 0 });
      else if (what === 21) {
        // Orders refused: a vote with nothing to vote, another's soldier, a launch with nothing chosen.
        const wrong: CampaignCommand[] = [{ type: 'vote', accept: true }, { type: 'withdraw' }, { type: 'unlock', soldier: c.soldiers.find((x) => x.owner === partner)?.id ?? 'c1', cell: BOARD_CELLS_LIST[1]!.id }, { type: 'post', soldier: 'c1', at: { x: 0, y: 0 } }, { type: 'cancelMission' }];
        cmd(me, connected, bot.pick(wrong));
      }
      if (c.proposal || c.outcome) break;
    }
    if (c.proposal || c.event || c.outcome) continue;
    // The Bastion up a level as soon as it can be bought; a Nido's zone contacted, so it can be burnt.
    if (!levelUpBlocker(c) && !activeOffer(c)?.siege) {
      cmd(me, connected, { type: 'levelUp' });
      continue;
    }
    const nest = c.regions.find((r) => r.facility && !contactBlocker(c, r.id));
    if (nest && !activeOffer(c)?.siege) {
      cmd(me, connected, { type: 'contact', region: nest.id });
      continue;
    }
    if (bot.int(10) === 0) look();

    // The siege's posts, before it begins.
    const offer = activeOffer(c);
    if (offer?.siege && bot.int(2) === 0) {
      const defender = c.squad.map((id) => c.soldiers.find((s) => s.id === id)!).find((s) => connected.includes(s.owner));
      if (defender) {
        const at = bot.int(5) === 0 ? null : postFor(c, defender.id);
        cmd(defender.owner, connected, { type: 'post', soldier: defender.id, at });
      }
    }
    if (!offer) {
      if (mustWait(c) || (!plan.careful && bot.int(8) === 0)) {
        for (const slot of connected) cmd(slot, connected, { type: 'advance', ready: true });
        continue;
      }
      // The Marea high, what sets it back first (a Nido, a sabotage); then the side quests and the story; now and then any.
      const best = (o: engine.MissionOffer) => (c.doom >= 3 ? (o.reward.doom ?? 0) * 10 : 0) + (o.final ? 50 : 0) + (o.story ? 5 : 0) + (o.quest ? 4 : 0);
      const pick = bot.int(5) === 0 ? bot.pick(c.offers) : [...c.offers].sort((a, b) => best(b) - best(a))[0]!;
      cmd(me, connected, { type: 'chooseMission', id: pick.id });
      continue;
    }
    if (bot.int(15) === 0 && !offer.siege) {
      cmd(me, connected, { type: 'cancelMission' });
      continue;
    }
    // Both keys, and the mission goes.
    let launch = false;
    for (const slot of connected) if (cmd(slot, connected, { type: 'launch', ready: true }).ok) launch ||= !!(ops[ops.length - 1]!.result as { launch?: boolean }).launch;
    if (!launch) {
      // An empty squad (all of it hurt or gone since it was picked): the room picks it again, as bringPlayersIn does.
      if (!c.squad.length) {
        c.squad = defaultSquad(c, connected);
        syncPads(c);
        record({ op: 'defaultSquad', connected });
      }
      continue;
    }
    look();
    const mission = launchMission(c, new Rng(plan.seed * 1000 + flown));
    endMissionBy(mission, bot);
    const stripped = stripMission(mission);
    foldMission(c, mission, rng);
    record({ op: 'mission', mission: stripped });
    flown++;
    if (!plan.careful && bot.int(6) === 0 && !c.outcome && !c.event) {
      advanceTime(c, rng);
      record({ op: 'advanceTime' });
    }
  }
  // How it ended: the title and the last page (lore.ts ENDING_TITLES, epilogue).
  if (c.outcome) record({ op: 'end', reads: fixDeep({ title: c.ending ? ENDING_TITLES[c.ending] : null, epilogue: epilogue(c.outcome, c.ending, c.doom >= DOOM_MAX) }) });
  const folder = extra ?? join(out, 'games');
  mkdirSync(folder, { recursive: true });
  writeFileSync(join(folder, `${plan.name}.json`), JSON.stringify({ name: plan.name, seed: plan.seed, start: plan.start, campaign: campaign0, rng: rng0, ops }) + '\n');
  console.log(`campaign/games/${plan.name}.json: ${ops.length} operaciones, ${flown} misiones, ${c.outcome ?? 'sin terminar'} (día ${c.day}, nivel ${c.level})`);
}

if (extra) {
  const starts: GamePlan['start'][] = [{ create: [0, 1] }, { create: [0] }, { save: 'muestra.json' }, { save: 'saves/v20.json' }, { save: 'saves/v22.json' }, { save: 'saves/v24.json' }, { save: 'saves/v25.json' }];
  for (let i = 0; i < Number(process.env.EXTRA_SEEDS ?? 20); i++) {
    const seed = 10000 + i * 37;
    playGame({ name: `extra-${seed}`, seed, start: starts[i % starts.length]!, missions: 40, ...(i % 4 === 1 ? { lateJoin: i % 3 } : {}), ...(i % 5 === 2 ? { away: 3 } : {}), ...(i % 3 === 0 ? { careful: true } : {}) });
  }
} else for (const plan of GAMES) playGame(plan);
