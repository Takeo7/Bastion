// The two showcase games for the trailer's captures (docs/TRAILER.md): a campaign halfway through (day 20,
// the Bastion at level 3, the Marea halfway, eight soldiers of the four classes with full trees, an Armería
// with weapons of every kind, the Huerto, the Taller making something and a wounded soldier in the
// Enfermería), and one of its missions by day at the service area, the squad by a building, one soldier with
// a full backpack and something on the floor beside them, the dead in sight but not on top of them.
//
// npx tsx packages/client/tools/trailer/escaparate.ts <folder> [tokenAna] [tokenArturo]
// Writes base.json and mision.json there, server saves (packages/server/src/persistence.ts) for --save.
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  autoBuild,
  buildSquad,
  campaignHackBonus,
  canSee,
  canStandOn,
  createCampaign,
  createMission,
  floodedZones,
  gainXp,
  LEVEL_XP,
  medicineCures,
  missionInfo,
  missionName,
  offerBriefing,
  RANKS,
  REGIONS,
  Rng,
  type CampaignState,
  type ClassId,
  type FoundWeaponId,
  type GameState,
  type PerkId,
  type Pos,
  type Slot,
} from '../../../engine/src/index';

const [out = '.dev-snapshots/trailer/partidas', tokenAna = 'bc901f00-6353-4f9e-b957-3528d26f54cd', tokenArturo = '6ce6529a-76be-4bea-9bb9-b17edeba8079'] = process.argv.slice(2);
const rng = new Rng(20261015);
const c = createCampaign([0, 1], rng);

// --------------------------------------------------------------- the base

// Day 20: every pending date moves with it.
const shift = 19;
c.day += shift;
const dates = c as unknown as Record<string, unknown>;
for (const key of Object.keys(dates)) if (/^next\w*Day$/.test(key) && typeof dates[key] === 'number') (dates[key] as number) += shift;
for (const o of c.offers) if (o.expiresDay !== null) o.expiresDay += shift;

// The Bastion at level 3 (the level's weapons come with it), and some of everything in the stores.
gainXp(c, LEVEL_XP[2]! + 8 - c.xp, rng);
Object.assign(c, { credits: 340, parts: 9, medicines: 5, food: 22 });

// The Marea halfway, three zones down; Volkren in contact by road besides Aldren (the two the network holds).
c.doom = 6;
c.flooded = floodedZones(c);
for (const r of c.regions) if (r.id === 'eeu') r.contacted = true;

// Eight soldiers: six with a class, at Capitán and Comandante, and two recruits, with their trees learnt.
const builds: [ClassId, number, boolean][] = [
  ['assault', 6, true],
  ['sharpshooter', 6, true],
  ['specialist', 5, true],
  ['grenadier', 5, true],
  ['assault', 5, true],
  ['specialist', 6, true],
  ['sharpshooter', 3, false],
  ['grenadier', 2, false],
];
c.soldiers.forEach((s, i) => {
  const [cls, rank, classed] = builds[i]!;
  const b = autoBuild(cls, rank, rng);
  Object.assign(s, { cls: classed ? cls : null, rank, xp: RANKS[rank]!.xp + 1, attrs: b.attrs, talents: b.talents, missions: rank + 2, kills: 2 * rank + (i % 3) });
});
// The Oficio for one of them: a trade or two of the Taller de oficios.
const crafty = c.soldiers[5]!;
crafty.talents = { ...crafty.talents, songBlade: 1, layOnHands: 1 } as Record<PerkId, number>;
// One wounded in the Enfermería.
c.soldiers[4]!.woundedDays = 3;

// Buildings: Huerto, Taller (making a shotgun), Enfermería at level 2, Chapistería, the Taller de oficios and a barracks.
const free = c.base.plots.filter((p) => p.terrain === 'clear' && !p.building);
// On clear ground; past it, a plot of ruins or woods cleared as the crews would have.
const plot = () => free.shift() ?? Object.assign(c.base.plots.find((p) => !p.building && p.terrain !== 'clear')!, { terrain: 'clear' as const });
const build = (id: 'garden' | 'workshop' | 'infirmary' | 'garage' | 'echoes' | 'quarters', level: number) => Object.assign(plot(), { building: id, level });
build('garden', 2);
const workshop = build('workshop', 2);
build('infirmary', 2);
build('garage', 1);
build('echoes', 1);
// A barracks: room to recruit, so the Cuartel shows no warning.
build('quarters', 1);
c.crafted = ['weapons2', 'ammo1', 'ammo2'];
c.base.works.push({ plot: workshop.id, kind: 'craft', facility: 'workshop', daysLeft: 1, total: 2, project: 'makeShotgun' });

// The Armería: every kind of weapon, most of them carried, a good one or two, some on the rack.
const arms: [FoundWeaponId, number | null][] = [
  ['pumpShotgun', 0],
  ['precisionRifle', 1],
  ['assaultRifle', 2],
  ['lightMachineGun', 3],
  ['sawnOff', 4],
  ['assaultRifle', 5],
  ['machete', 0],
  ['machete', 4],
  ['huntingRifle', null],
  ['machineGun', null],
  ['scopedRifle', null],
];
c.foundWeapons = arms.map(([kind, who], k) => ({ id: `w${100 + k}`, kind, holder: who === null ? null : c.soldiers[who]!.id }));

// Nothing pending that covers the screen.
Object.assign(c, { proposal: null, event: null, questEvent: null, treatment: null, advanceReady: [false, false], launchReady: [false, false] });
// Who goes out: six fit soldiers.
c.squad = c.soldiers.filter((s) => s.woundedDays === 0).slice(0, 6).map((s) => s.id);

const seats = [
  { slot: 0 as Slot, name: 'Ana', token: tokenAna },
  { slot: 1 as Slot, name: 'Arturo', token: tokenArturo },
];
const save = (phase: 'base' | 'mission', campaign: CampaignState, state: GameState | null) => ({
  version: 6,
  seats,
  phase,
  mode: 'campaign',
  state,
  campaign,
  resultApplied: false,
  rng: rng.state,
  missionKind: state?.mission.kind ?? 'recovery',
  skirmishMap: 'city',
  skirmishVeterans: false,
});

mkdirSync(out, { recursive: true });
writeFileSync(join(out, 'base.json'), JSON.stringify(save('base', c, null)));

// ------------------------------------------------------------ the mission

// A recovery at the service area on the forest road, by day.
const offer = c.offers.find((o) => !o.story && !o.final) ?? c.offers[0]!;
Object.assign(offer, { kind: 'recovery', map: { kind: 'generated', biome: 'wilds', seed: 5, site: 'roadside' }, night: false });
offer.name = missionName('wilds', REGIONS[offer.region].name, 5, 'roadside');
const m = JSON.parse(JSON.stringify(c)) as CampaignState;
m.activeMission = offer.id;
// As the room launches it (packages/server/src/room.ts launchCampaignMission), so it reads as the operation.
const info = missionInfo(m, offer);
const s = createMission({
  map: offer.map,
  slots: [0, 1],
  rng: new Rng(7),
  kind: 'recovery',
  squad: buildSquad(m),
  pods: offer.pods,
  difficulty: offer.difficulty,
  hackBonus: campaignHackBonus(m),
  campaignMission: { id: offer.id, name: offer.name, ...(info.kicker ? { kicker: info.kicker } : {}), ...(info.item ? { item: info.item } : {}), briefing: offerBriefing(m, offer) },
  itemOutdoors: info.itemOutdoors,
  medicineCures: medicineCures(m),
});

const at = (p: { x: number; y: number }) => p.y * s.width + p.x;
const free2 = (p: Pos) => p.x >= 0 && p.y >= 0 && p.x < s.width && p.y < s.height && canStandOn(s.tiles[at(p)]!) && !s.units.some((u) => u.alive && u.pos.x === p.x && u.pos.y === p.y && u.pos.z === p.z);
const outdoors = (p: Pos) => (s.ground?.[at(p)] ?? 0) !== 2;
const nextTo = (p: Pos, g: number) => [-1, 0, 1].some((dy) => [-1, 0, 1].some((dx) => s.ground?.[at({ x: p.x + dx, y: p.y + dy })] === g));

// The squad: six free outdoor tiles in a 3 × 2 block on a yard by a building's wall.
const squad = s.units.filter((u) => u.team === 'xcom');
let spot: Pos[] | null = null;
for (let y = 2; y < s.height - 3 && !spot; y++) {
  for (let x = 2; x < s.width - 4 && !spot; x++) {
    const block = [0, 1].flatMap((dy) => [0, 1, 2].map((dx) => ({ x: x + dx, y: y + dy, z: 0 })));
    if (block.every((p) => free2(p) && outdoors(p) && s.ground?.[at(p)] === 3) && block.some((p) => nextTo(p, 2))) spot = block;
  }
}
if (!spot) throw new Error('No yard by a building');
squad.forEach((u, i) => (u.pos = { ...spot![i % spot!.length]! }));

// One soldier with a full backpack, and something on the floor beside them.
const carrier = squad[0]!;
carrier.backpack = [{ medicines: 2 }, { weapon: 'huntingRifle' }, { food: 4 }];
const floor = [-1, 0, 1].flatMap((dy) => [-1, 0, 1].map((dx) => ({ x: carrier.pos.x + dx, y: carrier.pos.y + dy, z: 0 }))).find((p) => free2(p) && outdoors(p));
if (floor) {
  s.dropped = [{ id: 'l1', pos: floor, loot: { parts: 2 } }];
  s.droppedCount = 1;
}

// The dead in sight, 8–12 tiles off, asleep: one pod moved where a soldier sees it.
const centre = { x: spot[0]!.x + 1, y: spot[0]!.y };
const pod = s.pods.find((p) => p.unitIds.length >= 3) ?? s.pods[0]!;
const zombies = pod.unitIds.map((id) => s.units.find((u) => u.id === id)!);
const ring: Pos[] = [];
for (let y = 0; y < s.height; y++) for (let x = 0; x < s.width; x++) {
  const d = Math.hypot(x - centre.x, y - centre.y);
  if (d >= 8 && d <= 12 && x > 1 && y > 1 && x < s.width - 2 && y < s.height - 2) ring.push({ x, y, z: 0 });
}
// Nearest to ten tiles off first.
ring.sort((a, b) => Math.abs(Math.hypot(a.x - centre.x, a.y - centre.y) - 10) - Math.abs(Math.hypot(b.x - centre.x, b.y - centre.y) - 10));
const seen = (p: Pos) => squad.some((u) => canSee(s, u, { ...zombies[0]!, pos: p }));
let placed = false;
for (const p of ring) {
  const group = [p, { ...p, x: p.x + 1 }, { ...p, y: p.y + 1 }, { ...p, x: p.x + 1, y: p.y + 1 }, { ...p, x: p.x - 1 }];
  if (!group.every((q) => free2(q) && outdoors(q)) || !seen(p)) continue;
  zombies.forEach((z, i) => (z.pos = { ...group[i % group.length]! }));
  placed = true;
  break;
}
if (!placed) throw new Error('No place in sight for the dead');
// Everyone else stays where the map put them; nobody right on top of the squad.
for (const u of s.units.filter((x) => x.team === 'alien' && !zombies.includes(x))) {
  if (Math.min(...squad.map((q) => Math.hypot(q.pos.x - u.pos.x, q.pos.y - u.pos.y))) < 8) u.alive = false;
}
s.units = s.units.filter((u) => u.alive);
// Seen already: the dead's sight doesn't paint half the frame red.
s.concealed = false;

// Where UI framed the backpack's capture: on the road, in the light, the dead to the left (trees cover the yard).
const framed: Record<string, [number, number]> = {
  'Pierre Lambert': [12, 14],
  'Hana Novak': [13, 14],
  'Chiara Rossi': [14, 14],
  'Dmitri Volkov': [12, 15],
  'Aisha Diallo': [13, 15],
  'Maya Cohen': [14, 15],
};
for (const u of squad) {
  const p = framed[u.name];
  if (p) u.pos = { x: p[0], y: p[1], z: 0 };
}
if (s.dropped?.length) s.dropped[0]!.pos = { x: 12, y: 13, z: 0 };

writeFileSync(join(out, 'mision.json'), JSON.stringify(save('mission', m, s)));
console.log(`base.json: day ${c.day}, level xp ${c.xp}, doom ${c.doom}, flooded ${c.flooded!.join(', ')}, soldiers ${c.soldiers.map((x) => `${x.name} (${x.cls ?? 'recluta'} ${x.rank})`).join('; ')}`);
console.log(`mision.json: ${s.mapName}, squad at ${spot.map((p) => `${p.x},${p.y}`).join(' ')}, dead at ${zombies.map((z) => `${z.pos.x},${z.pos.y}`).join(' ')}, floor ${floor ? `${floor.x},${floor.y}` : 'none'}`);
