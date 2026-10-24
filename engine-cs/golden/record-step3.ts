/**
 * Writes engine-cs/golden/step3/*.json: missions made by the TypeScript engine's createMission (setup.ts), and maps by
 * its generateLayout (mapgen.ts), baseLayout (base.ts) and handmadeLayout, for engine-cs/tests/Step3GoldenTests.cs to
 * make again in C# and compare whole (docs/UNITY.md §3, hito 1 paso 3).
 *
 *   npx tsx engine-cs/golden/record-step3.ts
 *
 * Every biome and every kind of mission, every scenario the generator has (the town, the industrial estate, the clinic,
 * the ward and the hospital, the outskirts and the service area, the factory), night and day, difficulties 1 to 4,
 * veterans or not, one seat or two, the living's missions, La Lata's sizes, a campaign squad, every hand-made map, and
 * sieges of a base built with base.ts. Each case keeps createMission's options (without the Rng: `rngSeed` is its seed)
 * and the state it made; each map, the options and the layout.
 */
import { mkdirSync, writeFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';
import { defaultAppearance } from '../../packages/engine/src/appearance';
import { baseLayout, BASE_EDGE, fellTree, freshBase, PIECES, placeBlocker, placePiece, type BaseState, type PieceId } from '../../packages/engine/src/base';
import { ZERO_MODS } from '../../packages/engine/src/content';
import { generateLayout, handmadeLayout, type GenOptions } from '../../packages/engine/src/mapgen';
import { MAPS } from '../../packages/engine/src/maps';
import { Rng } from '../../packages/engine/src/rng';
import { createMission, type MapSource, type MissionOptions, type SquadMember } from '../../packages/engine/src/setup';
import { missionLines, recruitSkills } from '../../packages/engine/src/skills';
import type { Biome, MissionKind, Site } from '../../packages/engine/src/types';

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, 'step3');
mkdirSync(out, { recursive: true });

type Options = Omit<MissionOptions, 'rng'>;
interface Case {
  name: string;
  rngSeed: number;
  options: Options;
}

const KINDS: MissionKind[] = ['elimination', 'recovery', 'sabotage', 'hack', 'rescue', 'scavenge'];
const gen = (biome: Biome, seed: number, site?: Site, medical?: 'clinic' | 'ward' | 'hospital'): MapSource => ({
  kind: 'generated',
  biome,
  seed,
  ...(site ? { site } : {}),
  ...(medical ? { medical } : {}),
});
const two = [0, 1] as const;

// ------------------------------------------------------------------- a base to besiege

/** Where a piece goes on a base: the free spot nearest the caravan, as placeBlocker allows. */
function build(base: BaseState, id: PieceId, rot = 0): void {
  const caravan = base.pieces.find((p) => p.piece === 'caravan')!;
  const [w, h] = PIECES[id].size;
  const spots: { x: number; y: number; d: number }[] = [];
  for (let y = BASE_EDGE; y <= base.height - BASE_EDGE - h; y++) {
    for (let x = BASE_EDGE; x <= base.width - BASE_EDGE - w; x++) spots.push({ x, y, d: Math.abs(x - caravan.x - 6) + Math.abs(y - caravan.y - 4) });
  }
  spots.sort((a, b) => a.d - b.d || a.y - b.y || a.x - b.x);
  const spot = spots.find((s) => !placeBlocker(base, id, s.x, s.y, rot));
  if (spot) placePiece(base, id, spot.x, spot.y, rot);
}

function builtBase(seed: number, level: number, pieces: PieceId[]): BaseState {
  const base = freshBase(new Rng(seed));
  base.level = level;
  for (const [k, id] of pieces.entries()) build(base, id, k % 4);
  // A few pines felled by the clearing's edge: stumps on the map.
  let felled = 0;
  for (const t of [...base.trees]) {
    if (felled >= 3) break;
    const x = t % base.width;
    const y = Math.floor(t / base.width);
    if (x < BASE_EDGE + 2 || y < BASE_EDGE + 2) continue;
    fellTree(base, x, y);
    felled++;
  }
  // One turret worn, one trap spent: what the siege inherits.
  const turret = base.pieces.find((p) => p.piece === 'turret');
  if (turret) turret.hp = 7;
  const mine = base.pieces.find((p) => p.piece === 'mine');
  if (mine) mine.charges = 0;
  return base;
}

// ------------------------------------------------------------------- a campaign squad

function squad(rng: Rng, n: number, slots: readonly number[]): SquadMember[] {
  const templates = ['rookie', 'assault', 'grenadier', 'sharpshooter', 'specialist'] as const;
  return Array.from({ length: n }, (_, i) => {
    const { skills, growth } = recruitSkills(rng);
    const owner = slots[i % slots.length]!;
    return {
      campaignId: `c${i + 1}`,
      name: `Soldado ${i + 1}`,
      ...(i % 2 ? { nickname: `Mote ${i}` } : {}),
      appearance: defaultAppearance(owner as 0 | 1, undefined, i % 2 ? 'f' : 'm', `Soldado ${i + 1}`),
      weaponTier: 1 + (i % 3),
      armorTier: 1 + (i % 2),
      ...(i === 1 ? { weaponName: 'La escopeta de Ana' } : {}),
      owner: owner as 0 | 1,
      template: templates[i % templates.length]!,
      rank: i % 4,
      perks: i === 2 ? ['aimedShot', 'serenity'] : [],
      mods: { ...ZERO_MODS, aim: i * 2 },
      maxHp: 6 + i,
      hp: 5 + i,
      charges: i % 2 ? { grenade: 2 } : {},
      will: 40 + i * 5,
      armor: i % 2,
      gear: i % 2 ? ['fragGrenade', 'firstAid'] : ['smokeGrenade'],
      ...(i === 3 ? { packSlots: 5 } : {}),
      ...(i % 3 === 0 ? { arms: { primary: i % 2 ? 'pumpShotgun' : 'assaultRifle', melee: i === 0 ? 'machete' : null } } : {}),
      ...(i % 3 === 1 ? { mags: 1 + i } : {}),
      ...(i !== 4 ? { lines: missionLines(skills, growth, i * 0.1) } : {}),
    } as SquadMember;
  });
}

// ------------------------------------------------------------------- the cases

const groups: Record<string, Case[]> = { city: [], town: [], wilds: [], facility: [], handmade: [], base: [], campaign: [] };

// The town (a city but the industrial estate): every kind, over seeds, difficulties, nights, veterans and seats.
const cityPlan: [MissionKind, number, Partial<Options>][] = [
  ['elimination', 1, { difficulty: 1 }],
  ['elimination', 2, { night: true }],
  ['elimination', 3, { difficulty: 3, veterans: true }],
  ['elimination', 4, { difficulty: 4, slots: [0] }],
  ['recovery', 5, {}],
  ['recovery', 6, { itemOutdoors: true, difficulty: 1 }],
  ['recovery', 7, { night: true, difficulty: 3 }],
  ['sabotage', 8, { alarm: 'police' }],
  ['sabotage', 9, { alarm: 'ambulance', difficulty: 4, objectiveName: 'El coche patrulla' }],
  ['sabotage', 10, { slots: [1] }],
  ['hack', 11, { hackBonus: 10 }],
  ['hack', 12, { night: true, veterans: true }],
  ['rescue', 13, {}],
  ['rescue', 14, { vipName: 'Dra. Amara Nwosu', difficulty: 3 }],
  ['scavenge', 15, { difficulty: 1 }],
  ['scavenge', 16, { night: true, difficulty: 4, veterans: true }],
];
for (const [kind, seed, more] of cityPlan) groups.city!.push({ name: `city-${seed}-${kind}`, rngSeed: seed, options: { map: gen('city', seed), slots: [...two], kind, ...more } });

// The other towns: the industrial estate, the clinic, the ward, the hospital; the opening chapters' clinic and hospital.
for (const [i, kind] of KINDS.entries()) {
  const seed = 17 + i;
  groups.town!.push({ name: `industrial-${seed}-${kind}`, rngSeed: seed, options: { map: gen('city', seed, 'industrial'), slots: [...two], kind, ...(kind === 'sabotage' ? { alarm: 'factory' } : {}), ...(i % 2 ? { night: true } : {}) } });
}
groups.town!.push({ name: 'clinic-23-recovery', rngSeed: 23, options: { map: gen('city', 23, undefined, 'clinic'), slots: [...two], kind: 'recovery' } });
groups.town!.push({ name: 'ward-24-elimination', rngSeed: 24, options: { map: gen('city', 24, undefined, 'ward'), slots: [...two], kind: 'elimination', difficulty: 3 } });
groups.town!.push({ name: 'hospital-25-rescue', rngSeed: 25, options: { map: gen('city', 25, undefined, 'hospital'), slots: [...two], kind: 'rescue' } });
groups.town!.push({ name: 'hospital-26-hack', rngSeed: 26, options: { map: gen('city', 26, undefined, 'hospital'), slots: [0], kind: 'hack', night: true } });
groups.town!.push({ name: 'story1-27-recovery', rngSeed: 27, options: { map: gen('city', 27), slots: [...two], kind: 'recovery', difficulty: 1, campaignMission: { id: 'story-1', name: 'El consultorio de Bielsa', story: 1 }, snow: true } });
groups.town!.push({ name: 'story2-28-rescue', rngSeed: 28, options: { map: gen('city', 28), slots: [...two], kind: 'rescue', difficulty: 1, campaignMission: { id: 'story-2', name: 'La doctora', story: 2, item: 'nada' }, vipName: 'Dra. Amara Nwosu' } });
groups.town!.push({ name: 'living-29-elimination', rngSeed: 29, options: { map: gen('city', 29), slots: [...two], kind: 'elimination', living: true, difficulty: 3 } });
groups.town!.push({ name: 'vehicle0-30-recovery', rngSeed: 30, options: { map: gen('city', 30), slots: [...two], kind: 'recovery', vehicle: 0 } });
groups.town!.push({ name: 'vehicle2-31-rescue', rngSeed: 31, options: { map: gen('city', 31), slots: [...two], kind: 'rescue', vehicle: 2, night: true } });
groups.town!.push({ name: 'industrial-site-wrong-32', rngSeed: 32, options: { map: gen('city', 32, 'roadside'), slots: [...two], kind: 'hack' } });

// The country: the outskirts and the service area, every kind.
for (const [i, kind] of KINDS.entries()) {
  const seed = 33 + i;
  groups.wilds!.push({ name: `wilds-${seed}-${kind}`, rngSeed: seed, options: { map: gen('wilds', seed), slots: [...two], kind, difficulty: 1 + (i % 4), ...(i === 2 ? { night: true } : {}), ...(i === 4 ? { veterans: true } : {}) } });
}
for (const [i, kind] of KINDS.entries()) {
  const seed = 39 + i;
  groups.wilds!.push({ name: `roadside-${seed}-${kind}`, rngSeed: seed, options: { map: gen('wilds', seed, 'roadside'), slots: i === 3 ? [0] : [...two], kind, ...(i === 5 ? { night: true, difficulty: 4 } : {}), ...(kind === 'sabotage' ? { alarm: 'police' } : {}) } });
}
groups.wilds!.push({ name: 'wilds-45-living-hack', rngSeed: 45, options: { map: gen('wilds', 45), slots: [...two], kind: 'hack', living: true, difficulty: 2 } });
groups.wilds!.push({ name: 'wilds-46-industrial-wrong', rngSeed: 46, options: { map: gen('wilds', 46, 'industrial'), slots: [...two], kind: 'elimination', night: true, difficulty: 3 } });

// The factory, every kind.
for (const [i, kind] of KINDS.entries()) {
  const seed = 51 + i;
  groups.facility!.push({ name: `facility-${seed}-${kind}`, rngSeed: seed, options: { map: gen('facility', seed), slots: i === 1 ? [0] : [...two], kind, difficulty: 4 - (i % 4), ...(i === 0 || i === 3 ? { night: true } : {}), ...(i === 5 ? { veterans: true } : {}), ...(kind === 'sabotage' ? { alarm: 'factory' } : {}) } });
}
groups.facility!.push({ name: 'facility-57-ambulance', rngSeed: 57, options: { map: gen('facility', 57), slots: [...two], kind: 'sabotage', alarm: 'ambulance' } });
groups.facility!.push({ name: 'facility-58-living-rescue', rngSeed: 58, options: { map: gen('facility', 58), slots: [...two], kind: 'rescue', living: true, difficulty: 4 } });
groups.facility!.push({ name: 'facility-59-pods', rngSeed: 59, options: { map: gen('facility', 59), slots: [...two], kind: 'elimination', pods: [['zombie'], ['trooper', 'officer'], ['mec', 'zombie', 'zombie']], squadSize: 3, turns: 9 } });

// Every hand-made map, every kind.
for (const id of Object.keys(MAPS)) {
  for (const [i, kind] of KINDS.entries()) {
    groups.handmade!.push({ name: `${id}-${kind}`, rngSeed: 60 + i, options: { map: { kind: 'handmade', id }, slots: i % 2 ? [0] : [...two], kind, difficulty: 1 + (i % 4), ...(i === 3 ? { night: true, veterans: true } : {}) } });
  }
}

// The campaign's base under siege (docs/FASE12.md §11), built with base.ts.
const DEFENCES: PieceId[] = ['radio', 'quarters', 'palisade', 'palisade', 'wall', 'gate', 'sandbags', 'logs', 'tower', 'floodlight', 'wire', 'wire', 'mine', 'spikes', 'snare', 'turret', 'turret'];
{
  const base = builtBase(70, 4, DEFENCES);
  const members = squad(new Rng(70), 6, two);
  // Posts: one on a free tile, one on a turret's (back to the spawns), one off the map's floor.
  const turret = base.pieces.find((p) => p.piece === 'turret')!;
  const caravan = base.pieces.find((p) => p.piece === 'caravan')!;
  const posts = { c1: { x: caravan.x + 4, y: caravan.y + 5 }, c2: { x: turret.x, y: turret.y }, c3: { x: 0, y: 0 } };
  groups.base!.push({ name: 'base-70-siege', rngSeed: 70, options: { map: { kind: 'base', base }, slots: [...two], difficulty: 2, squad: members, posts } });
  groups.base!.push({ name: 'base-70-plan', rngSeed: 71, options: { map: { kind: 'base', base }, slots: [...two], difficulty: 3, squad: members, siegePlan: { count: 34, sides: [0, 2] }, night: true } });
  groups.base!.push({ name: 'base-70-skirmish', rngSeed: 72, options: { map: { kind: 'base', base }, slots: [0], difficulty: 4, veterans: true, kind: 'recovery' } });
}
{
  const base = builtBase(73, 1, ['radio', 'palisade', 'turret', 'wire']);
  groups.base!.push({ name: 'base-73-small', rngSeed: 73, options: { map: { kind: 'base', base }, slots: [...two], difficulty: 1, squad: squad(new Rng(73), 4, two), siegePlan: { count: 12, sides: [] } } });
}

// A campaign's operation: its squad, its name, its rules.
{
  const members = squad(new Rng(80), 5, two);
  groups.campaign!.push({
    name: 'campaign-80-recovery',
    rngSeed: 80,
    options: {
      map: gen('city', 80),
      slots: [...two],
      kind: 'recovery',
      squad: members,
      difficulty: 3,
      turns: 14,
      hackBonus: 5,
      campaignMission: { id: 'op-80', name: 'La farmacia', kicker: 'Operación', item: 'los antibióticos', briefing: 'Entrad y salid.' },
      medicineCures: true,
      quarantine: false,
      snow: true,
      leader: { template: 'zombie', name: 'El Grande' },
      vehicle: 1,
      realtime: true,
    },
  });
  groups.campaign!.push({ name: 'campaign-81-hack', rngSeed: 81, options: { map: gen('wilds', 81, 'roadside'), slots: [0], kind: 'hack', squad: squad(new Rng(81), 4, [0]), quarantine: true, vehicle: 3, living: true, leader: { template: 'officer', name: 'Cuervo Jefe' } } });
  groups.campaign!.push({ name: 'campaign-82-sabotage', rngSeed: 82, options: { map: gen('facility', 82), slots: [...two], kind: 'sabotage', squad: squad(new Rng(82), 6, two), alarm: 'factory', objectiveName: 'La alarma', squadSize: 4 } });
}

// ------------------------------------------------------------------- the maps by themselves

const layouts: { name: string; opts: GenOptions }[] = [];
const genOpts: [string, GenOptions][] = [
  ['town-90-elimination', { biome: 'city', kind: 'elimination', podSizes: [3, 3, 4, 3], seed: 90 }],
  ['town-91-recovery', { biome: 'city', kind: 'recovery', podSizes: [3, 4], seed: 91, itemOutdoors: true }],
  ['town-92-sabotage', { biome: 'city', kind: 'sabotage', podSizes: [4, 4, 4], seed: 92, alarm: 'police' }],
  ['town-93-hack', { biome: 'city', kind: 'hack', podSizes: [], seed: 93 }],
  ['town-94-rescue', { biome: 'city', kind: 'rescue', podSizes: [5, 2, 3], seed: 94, hospital: true }],
  ['town-95-clinic', { biome: 'city', kind: 'recovery', podSizes: [3, 3], seed: 95, clinic: true }],
  ['town-96-ward', { biome: 'city', kind: 'elimination', podSizes: [3, 3, 3], seed: 96, medical: 'ward' }],
  ['industrial-97-sabotage', { biome: 'city', kind: 'sabotage', podSizes: [3, 3, 3], seed: 97, site: 'industrial', alarm: 'ambulance' }],
  ['wilds-98-recovery', { biome: 'wilds', kind: 'recovery', podSizes: [3, 3, 4], seed: 98 }],
  ['roadside-99-rescue', { biome: 'wilds', kind: 'rescue', podSizes: [4, 4], seed: 99, site: 'roadside' }],
  ['facility-100-hack', { biome: 'facility', kind: 'hack', podSizes: [3, 5, 3, 3], seed: 100 }],
  ['facility-101-sabotage', { biome: 'facility', kind: 'sabotage', podSizes: [3], seed: 101, alarm: 'factory' }],
];
for (const [name, opts] of genOpts) layouts.push({ name, opts });

// ------------------------------------------------------------------- writing

/** Plain JSON (undefined fields gone, as JSON.stringify leaves them). */
const plain = <T>(x: T): T => JSON.parse(JSON.stringify(x));

let total = 0;
for (const [group, cases] of Object.entries(groups)) {
  const recorded = cases.map((c) => {
    const options = plain(c.options);
    const state = createMission({ ...plain(c.options), rng: new Rng(c.rngSeed) } as MissionOptions);
    return { name: c.name, rngSeed: c.rngSeed, options, state };
  });
  const text = JSON.stringify({ cases: recorded });
  writeFileSync(join(out, `${group}.json`), text);
  total += text.length;
  console.log(group, recorded.length, `${(text.length / 1024 / 1024).toFixed(1)} MB`);
}
{
  const recorded = [
    ...layouts.map(({ name, opts }) => ({ name, api: 'generateLayout', opts, layout: generateLayout(opts) })),
    ...Object.values(MAPS).flatMap((def) => (['elimination', 'hack', 'rescue'] as MissionKind[]).map((kind) => ({ name: `${def.id}-${kind}`, api: 'handmadeLayout', id: def.id, kind, layout: handmadeLayout(def, kind) }))),
    ...[70, 73, 74].map((seed) => {
      const base = seed === 74 ? freshBase(new Rng(seed), 40) : builtBase(seed, seed === 70 ? 4 : 1, seed === 70 ? DEFENCES : ['radio', 'palisade', 'turret', 'wire']);
      return { name: `base-${seed}`, api: 'baseLayout', base: plain(base), layout: baseLayout(base) };
    }),
  ];
  const text = JSON.stringify({ layouts: plain(recorded) });
  writeFileSync(join(out, 'layouts.json'), text);
  total += text.length;
  console.log('layouts', recorded.length, `${(text.length / 1024 / 1024).toFixed(1)} MB`);
}
console.log(`${(total / 1024 / 1024).toFixed(1)} MB in all`);
