/**
 * Writes engine-cs/golden/net/launch.json: missions of the campaign launched as the host does (room.ts
 * launchCampaignMission), for the C# host's EngineCampaignRules.LaunchMission (Red) to make the same.
 *
 *   npx tsx engine-cs/golden/record-launch.ts
 *
 * From campaigns of golden/campaign/ with an operation chosen: a new one with one player and with two, a partner come
 * late, the squad of a siege with its posts, a siege at night and two campaigns saved by the 0.9 and 0.10 (version 25).
 * Each with its seed: the mission made and the Rng after. Texts go through text-fixes.ts fixDeep, as C# says them.
 */
import { readFileSync, writeFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';
import {
  activeOffer,
  ALARM_NAMES,
  buildSquad,
  campaignHackBonus,
  createMission,
  hasQuarantine,
  medicineCures,
  missionInfo,
  offerBriefing,
  offerMap,
  podsFor,
  Rng,
  siegePlan,
  vehicleOf,
  type CampaignState,
  type GameState,
} from '../../packages/engine/src/index';
import { fixDeep } from './text-fixes';

const here = dirname(fileURLToPath(import.meta.url));
const campaign = join(here, 'campaign');

/** room.ts launchCampaignMission, as it is there, but for the room's state. */
function launch(c: CampaignState, rng: Rng): GameState {
  const offer = activeOffer(c)!;
  const info = missionInfo(c, offer);
  const siege = siegePlan(c, offer);
  return createMission({
    // A siege is fought on the base as it stands now (docs/FASE12.md §11.5).
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

const states = (JSON.parse(readFileSync(join(campaign, 'states.json'), 'utf8')) as { states: { name: string; campaign: CampaignState }[] }).states;
const named = (name: string) => states.find((s) => s.name === name)!.campaign;
const saved = (file: string) => (JSON.parse(readFileSync(join(campaign, 'saves', file), 'utf8')) as { campaign: CampaignState }).campaign;

const sources: [string, CampaignState][] = [
  ['states.json#solo-new', named('solo-new')],
  ['states.json#duo-new', named('duo-new')],
  ['states.json#late-partner', named('late-partner')],
  ['states.json#siege-posts', named('siege-posts')],
  ['states.json#mission-5', named('mission-5')],
  ['saves/v25-093.json', saved('v25-093.json')],
  ['saves/v25.json', saved('v25.json')],
];

const missions = sources.map(([source, c], i) => {
  const before = JSON.stringify(c);
  const seed = 4100 + i * 37;
  const rng = new Rng(seed);
  const state = launch(c, rng);
  // Launching only reads the campaign: the room broadcasts it as it was.
  if (JSON.stringify(c) !== before) throw new Error(`${source}: launching changed the campaign`);
  return { source, seed, rng: rng.state, state: JSON.parse(JSON.stringify(state)) as GameState };
});

writeFileSync(join(here, 'net', 'launch.json'), JSON.stringify(fixDeep({ missions })) + '\n');
console.log(`net/launch.json: ${missions.length} misiones (${missions.map((m) => `${m.source} → ${m.state.mapName}`).join(', ')})`);
