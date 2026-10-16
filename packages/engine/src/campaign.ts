// Strategy layer: a shared base run by two players at once.
//
// Co-op rules:
//  - Credits and the Bastion level are shared. Each player owns, promotes, equips
//    and recruits their own soldiers.
//  - Shared decisions (building, contacting regions, which mission to take, how to
//    answer an event) are proposals the partner accepts; a player alone in the base
//    decides directly.
//  - Progress comes from combat: missions give the Bastion experience, and its
//    levels unlock technology, accessories and facilities (progression.ts).
//  - Time only advances, and a mission only launches, when every connected player is ready.
//  - The story (lore.ts) opens a chapter per Bastion level; side quests (quests.ts)
//    chain operations and decisions, and rumours on the geoscape lead to some of them.
//
// The campaign is small and changes rarely, so the server broadcasts it whole after
// every command instead of streaming events like the tactical layer.
import { defaultAppearance, displayName, isAppearance, NAME_MAX, NICKNAME_MAX, normalizeAppearance } from './appearance';
import { addMods, BACKPACK_SLOTS, conditionOf, FOUND_WEAPONS, MOD_SLOTS, WORN_BELOW, PLAIN_WEAPON, REPAIR_PER_PART, WEAPON_MODS, WEAPONS, ITEMS, NIGHT, sexOfName, SOLDIER_NAMES, TEMPLATES } from './content';
import {
  ARMOR_TIERS,
  armorTier,
  bastionLevel,
  CRAFTS,
  type CraftId,
  foundWeaponOf,
  hasCrafted,
  LEVEL_ARMS,
  meleeWeaponOf,
  hasTech,
  itemUnlocked,
  LEVEL_XP,
  levelForXp,
  missionXp,
  spareMags,
  TECHS,
  type TechId,
  upgradeLevel,
  weaponName,
  weaponTier,
} from './progression';
import { broughtBack, type CarriedWeapon, type LootTotals } from './loot';
import { fighterStats, learn, missionLines, NO_GROWTH, NO_SKILLS, oneMoreBar, recruitSkills, totalBars, unlockBlocker, veterancyRank, VETERANCY, type BarFilled, type Deeds, type Growth, type Skills } from './skills';
import { BOARD_CELLS, type CellId } from './board';
import {
  barLine,
  wornLine,
  briefingSignature,
  doomChangeText,
  doomSubject,
  floodedContactReason,
  floodedListLabel,
  lootSummary,
  mareaAlert,
  missionBriefing,
  missionName,
  NAMED_SEX,
  noRoadReason,
  NWOSU_CHAPTER,
  ofZone,
  PARTITURA_LEVEL,
  recoveryItem,
  recoveryOutdoors,
  SIMON_CHAPTER,
  STORY,
  storyVip,
  type Ending,
  type SimonChoice,
} from './lore';
import { fill, QUESTS, RELATIVES, RUMOR_LINES, SUSPECTS, TRAITS, type QuestDecision, type QuestId, type QuestMission, type QuestOutcome, type TraitId } from './quests';
import type { BaseView } from './protocol';
import type { Rng } from './rng';
import { missionPods, podPlan, VIP_NAMES, type MapSource, type SquadMember } from './setup';
import type { Alarm, Appearance, Arms, Biome, ClassId, EndReason, FoundWeaponId, GameState, ItemId, ItemSlot, MissionKind, PerkId, Site, Slot, TemplateId, Vec2, WeaponModId } from './types';

// --------------------------------------------------------------- facilities

export type FacilityId =
  | 'simulation'
  | 'workshop'
  | 'infirmary'
  | 'training'
  | 'relay'
  | 'quarters'
  | 'range'
  | 'garage'
  | 'listening'
  | 'crew'
  | 'echoes'
  | 'memorial'
  | 'garden'
  | 'quarantine';

export interface FacilityDef {
  id: FacilityId;
  name: string;
  /** What it does at level 1. */
  description: string;
  /** What levels 2 and 3 bring. */
  upgrades: [string, string];
  /** Price in credits. */
  cost: number;
  days: number;
  /** Bastion level that unlocks it. */
  level: number;
  /** Only one can be built. */
  unique: boolean;
}

// Works are short (4–6 days): with one project at a time and ten buildings with three
// levels, the balance simulation (2026-10-11) left most of the base unbuilt at the
// longer times, with credits piling up.
export const FACILITIES: Record<FacilityId, FacilityDef> = {
  workshop: {
    id: 'workshop',
    name: 'Taller',
    description: 'Los accesorios cuestan un 25 % menos.',
    upgrades: ['Accesorios −35 %. Un torno de verdad.', 'Accesorios −45 %. Teo deja de quejarse.'],
    cost: 85,
    days: 4,
    level: 1,
    unique: true,
  },
  infirmary: {
    id: 'infirmary',
    name: 'Enfermería',
    description: 'Las heridas se curan el doble de rápido.',
    upgrades: ['Las heridas curan el triple de rápido. Agua caliente en la sala de curas.', 'Las heridas curan el cuádruple de rápido. Un quirófano de verdad, con lámpara y generador propio.'],
    cost: 60,
    days: 4,
    level: 1,
    unique: true,
  },
  quarters: {
    id: 'quarters',
    name: 'Barracón',
    description: '+2 camas para soldados (+1 junto al Cuartel). Literas de madera y una estufa que solo se enciende de noche.',
    upgrades: ['+3 camas (+1 junto al Cuartel). Un segundo piso de literas.', '+4 camas (+1 junto al Cuartel). Mantas de verdad.'],
    cost: 40,
    days: 2,
    level: 1,
    unique: false,
  },
  training: {
    id: 'training',
    name: 'Centro de entrenamiento',
    description: 'Cada jugador puede llevar un soldado más a las misiones.',
    upgrades: ['Los reclutas llegan con un nivel más en una habilidad. Una semana de prácticas antes de salir.', 'Los reclutas llegan con dos niveles más. Dos semanas, y alguien que ya ha vuelto de fuera les enseña.'],
    cost: 140,
    days: 6,
    level: 2,
    unique: true,
  },
  range: {
    id: 'range',
    name: 'Campo de tiro',
    description: '+3 de puntería para todos los soldados (+2 junto al centro de entrenamiento). Dianas de cartón en una trinchera del pinar.',
    upgrades: ['+6 de puntería (+2 junto al entrenamiento). Siluetas de zombi a cincuenta metros.', '+10 de puntería (+2 junto al entrenamiento). Blancos móviles, con una polea de Teo.'],
    cost: 90,
    days: 4,
    level: 2,
    unique: true,
  },
  crew: {
    id: 'crew',
    name: 'Brigada de obras',
    description: 'Obras y desbroces un 25 % más rápidos. Carretillas, poleas y manos de sobra.',
    upgrades: ['Obras y desbroces un 40 % más rápidos.', 'Obras y desbroces un 50 % más rápidos.'],
    cost: 70,
    days: 3,
    level: 2,
    unique: true,
  },
  simulation: {
    id: 'simulation',
    name: 'Sala de mapas',
    description: 'Las misiones dan un 25 % más de experiencia al Bastión.',
    upgrades: ['+40 % de experiencia en las misiones. Mapas de arena con los combates de verdad.', '+55 % de experiencia. Cintas de cada misión, fotograma a fotograma.'],
    cost: 100,
    days: 5,
    level: 3,
    unique: true,
  },
  garage: {
    id: 'garage',
    name: 'Chapistería',
    description: 'Las heridas curan 1 día antes, nunca en menos de 1. La Lata lleva camilla.',
    upgrades: ['Las heridas curan 2 días antes. Mantas y un botiquín fijo atrás.', 'Las heridas curan 3 días antes. La Lata ya no para a repostar con heridos dentro.'],
    cost: 110,
    days: 5,
    level: 3,
    unique: true,
  },
  listening: {
    id: 'listening',
    name: 'Estación de escucha',
    description: 'Los muertos avanzan 2 días más tarde (+2 junto al centro de comunicaciones) y hay una misión más a la vista. Morse oye antes lo que viene.',
    upgrades: ['Los muertos, 4 días más tarde (+2 junto a comunicaciones).', 'Los muertos, 6 días más tarde (+2 junto a comunicaciones).'],
    cost: 120,
    days: 5,
    level: 4,
    unique: true,
  },
  echoes: {
    id: 'echoes',
    name: 'Taller de oficios',
    description: 'Lo que se aprende fuera, aquí se repasa: cada misión llena las barras un 20 % más.',
    upgrades: ['Un 40 % más. Más herramientas, y Nwosu repasa las curas.', 'Un 60 % más. Gente de otros enclaves enseña lo suyo.'],
    cost: 130,
    days: 6,
    level: 5,
    unique: true,
  },
  // Asked for in the second playtest: the fallen leave the Cuartel's list. Texts by Lore.
  memorial: {
    id: 'memorial',
    name: 'Camposanto',
    description: 'Los caídos descansan aquí y salen de la lista del Cuartel; sus nombres se guardan. Cruces de pino bajo la nieve, una por cada uno.',
    upgrades: [
      '+5 de voluntad para todos los soldados. Saben que, si caen, alguien los recordará.',
      '+10 de voluntad para todos. Y quien vuelve de una misión en la que cayó alguien de la escuadra aprende un 20 % más: por ellos. Teo graba cada nombre en latón.',
    ],
    cost: 40,
    days: 3,
    level: 1,
    unique: true,
  },
  // Food (docs/FASE4.md): costs and days like a barracón. Texts are placeholders for Lore.
  // The bitten's own building (playtest 2026-10-15, #13): without it none of them boards La Lata.
  quarantine: {
    id: 'quarantine',
    name: 'Cuarentena',
    description: 'Una nave aparte, con cerrojo por fuera. Sin ella, un mordido no sube a La Lata.',
    upgrades: ['Los mordidos tardan un día más en convertirse. Mantas, agua y alguien de guardia en la puerta.', 'Los mordidos tardan dos días más en convertirse. Una estufa y una radio para las noches largas.'],
    cost: 50,
    days: 3,
    level: 1,
    unique: true,
  },
  garden: {
    id: 'garden',
    name: 'Huerto',
    description: '+1 ración al día. Bancales bajo plástico y bidones para el agua de lluvia.',
    upgrades: ['+2 raciones al día. Un invernadero de verdad, con estufa.', '+3 raciones al día. Riego por goteo y semillas de La Vega.'],
    cost: 40,
    days: 2,
    level: 1,
    unique: true,
  },
  relay: {
    id: 'relay',
    name: 'Centro de comunicaciones',
    description: '+2 zonas contactables.',
    upgrades: ['+3 zonas contactables por centro. Una antena más alta, en el pino más alto.', '+4 zonas por centro. La antena llega hasta el mar.'],
    cost: 110,
    days: 5,
    level: 5,
    unique: false,
  },
};

/** Price, days and Bastion level of raising a facility to `to` (2 or 3). */
export function upgradeCost(id: FacilityId, to: number): { cost: number; days: number; level: number } {
  const def = FACILITIES[id];
  return to <= 2 ? { cost: def.cost, days: def.days, level: upgradeLevel(def.level, 2) } : { cost: Math.round(def.cost * 1.5), days: def.days + 2, level: upgradeLevel(def.level, 3) };
}

/** The 8 starting soldiers sleep in the bunker; recruits need a barracón. */
export const BUNKER_BEDS = 8;

// ----------------------------------------------------------- the compound

// Atalaya on the surface (the user's redesign, 2026-10-11): from above, the
// 1989 bunker in the middle of a square grid of plots. New buildings go next
// to what is already built and are joined by walkways; some pairs side by
// side work better (adjacency). Growing means clearing the rock, the forest
// or the ruins around. La Lata's garage lies outside the grid.

/** Side of the square grid of plots. */
export const BASE_SIZE = 7;
/** The command bunker, in the middle. */
export const BUNKER: Readonly<Vec2> = { x: 3, y: 3 };

/** Buildings that come with the bunker and open the strategy screens; they can't be moved. */
export type FixedBuildingId = 'command' | 'barracks' | 'research' | 'engineering';
export type BuildingId = FixedBuildingId | FacilityId;

/** Where the fixed buildings stand: the bunker and three around it (the side to the airstrip stays free). */
export const FIXED_BUILDINGS: Readonly<Record<FixedBuildingId, Vec2>> = {
  command: BUNKER,
  barracks: { x: 2, y: 3 },
  research: { x: 3, y: 2 },
  engineering: { x: 4, y: 3 },
};

export const FIXED_NAMES: Record<FixedBuildingId, string> = {
  command: 'Búnker de mando',
  barracks: 'Cuartel',
  research: 'Sala de radio',
  engineering: 'Armería',
};

export type Terrain = 'clear' | 'rocks' | 'forest' | 'ruins';

export interface TerrainDef {
  name: string;
  description: string;
  /** Credits recovered when it is cleared. */
  salvage: number;
  /** Extra days to clear it. */
  days: number;
  /** Log line when the work starts; `{parcela}` is the plot's name. */
  start: string;
}

export const TERRAINS: Record<Exclude<Terrain, 'clear'>, TerrainDef> = {
  rocks: { name: 'Roca', description: 'Roca viva de la sierra. Barrenos, carretillas y paciencia.', salvage: 0, days: 1, start: 'Empiezan los barrenos en {parcela}.' },
  forest: { name: 'Bosque', description: 'Pinar cerrado. La madera va a las estufas, que solo se encienden de noche.', salvage: 0, days: 0, start: 'Empieza la tala en {parcela}.' },
  ruins: {
    name: 'Ruinas',
    description: 'Casas de antes de la Pandemia. Se desmontan a mano y lo que sirve se guarda: +15 §.',
    salvage: 15,
    days: 0,
    start: 'Empieza el desmontaje de las ruinas de {parcela}.',
  },
};

/** A facility works better next to a certain building: wall to wall. */
export interface AdjacencyDef {
  facility: FacilityId;
  next: BuildingId;
  /** Effect first, then a touch of the world. */
  text: string;
}

export const ADJACENCY: AdjacencyDef[] = [
  { facility: 'infirmary', next: 'barracks', text: 'Las heridas curan un grado más rápido (×3 a nivel 1). Nadie cruza la nieve con una herida abierta.' },
  { facility: 'workshop', next: 'engineering', text: 'Accesorios un 15 % más baratos aún (−40 % a nivel 1). Las piezas pasan por un ventanuco.' },
  { facility: 'simulation', next: 'research', text: '+15 % de experiencia más (+40 % a nivel 1). Teo corrige los ejercicios en caliente.' },
  { facility: 'relay', next: 'command', text: '+1 zona contactable. La antena, pegada a la sala de Morse, llega más lejos.' },
  { facility: 'training', next: 'barracks', text: 'Reclutar cuesta 20 § en vez de 30. Los novatos se forman en casa.' },
  { facility: 'quarters', next: 'barracks', text: 'Una cama más. El Cuartel presta la estufa.' },
  { facility: 'range', next: 'training', text: '+2 de puntería más. Del aula a la galería sin cambiarse de botas.' },
  { facility: 'listening', next: 'relay', text: 'Los muertos avanzan 2 días más tarde aún. Dos antenas oyen más que una.' },
  { facility: 'echoes', next: 'listening', text: 'Los oficios hacen menos ruido (−2). Junto a la escucha se aprende a no hacer ruido.' },
];

export interface BasePlot {
  /** "x,y" */
  id: string;
  x: number;
  y: number;
  terrain: Terrain;
  building: BuildingId | null;
  /** A facility's level, 1–3 (missing in saves from before upgrades: 1). */
  level?: number;
}

export interface Construction {
  plot: string;
  /** `craft`: the Taller makes a tier of weapons or armour (docs/FASE4.md), on its own plot. */
  kind: 'clear' | 'build' | 'upgrade' | 'craft';
  facility: FacilityId | null;
  daysLeft: number;
  total: number;
  /** What a `craft` work makes. */
  project?: CraftId;
}

const DIRS: readonly Vec2[] = [
  { x: 1, y: 0 },
  { x: -1, y: 0 },
  { x: 0, y: 1 },
  { x: 0, y: -1 },
];

export function plotAt(c: CampaignState, x: number, y: number): BasePlot | undefined {
  return x < 0 || y < 0 || x >= BASE_SIZE || y >= BASE_SIZE ? undefined : c.base.plots[y * BASE_SIZE + x];
}

export function plotById(c: CampaignState, id: string): BasePlot | undefined {
  return c.base.plots.find((p) => p.id === id);
}

/** The plots wall to wall with `p` (no diagonals). */
export function neighbours(c: CampaignState, p: Vec2): BasePlot[] {
  return DIRS.map((d) => plotAt(c, p.x + d.x, p.y + d.y)).filter((q): q is BasePlot => !!q);
}

/** Rings out from the bunker: 1 around it, 3 at the edge. */
export function plotRing(p: Vec2): number {
  return Math.max(Math.abs(p.x - BUNKER.x), Math.abs(p.y - BUNKER.y));
}

/** "la parcela C2": columns A–G from west to east, rows 1–7 from north to south. */
export function plotName(p: Vec2): string {
  return `la parcela ${'ABCDEFG'[p.x] ?? '?'}${p.y + 1}`;
}

export function buildingName(id: BuildingId): string {
  return id in FIXED_NAMES ? FIXED_NAMES[id as FixedBuildingId] : FACILITIES[id as FacilityId].name;
}

/** Clearing a plot: farther from the bunker costs more; the terrain adds days and maybe scrap. */
export function clearCost(p: BasePlot): { cost: number; days: number; salvage: number } {
  const terrain = p.terrain === 'clear' ? null : TERRAINS[p.terrain];
  const ring = plotRing(p);
  return { cost: ring <= 2 ? 25 : 40, days: (ring <= 2 ? 3 : 5) + (terrain?.days ?? 0), salvage: terrain?.salvage ?? 0 };
}

/**
 * What grows around the bunker, the same for every campaign: a hash of the
 * plot, so both players (and old saves) see the same ground.
 */
function terrainAt(x: number, y: number): Terrain {
  if (plotRing({ x, y }) <= 1) return 'clear';
  const h = ((x * 73856093) ^ (y * 19349663)) >>> 0;
  const roll = h % 10;
  return roll < 4 ? 'forest' : roll < 7 ? 'rocks' : 'ruins';
}

/** Why the plot can't be cleared, or null if it can. */
export function clearable(c: CampaignState, p: BasePlot): string | null {
  if (p.terrain === 'clear') return 'Ese terreno ya está despejado.';
  if (!neighbours(c, p).some((q) => q.terrain === 'clear')) return 'Hay que despejar antes una parcela a su lado.';
  return null;
}

/** Built pairs wall to wall: they get a walkway. */
export function walkways(c: CampaignState): [BasePlot, BasePlot][] {
  const pairs: [BasePlot, BasePlot][] = [];
  for (const p of c.base.plots) {
    if (!p.building) continue;
    for (const d of [DIRS[0]!, DIRS[2]!]) {
      const q = plotAt(c, p.x + d.x, p.y + d.y);
      if (q?.building) pairs.push([p, q]);
    }
  }
  return pairs;
}

/** The adjacency bonus a facility would get standing on `p`, if any. */
export function adjacencyAt(c: CampaignState, p: Vec2, facility: FacilityId): AdjacencyDef | null {
  const def = ADJACENCY.find((a) => a.facility === facility);
  return def && neighbours(c, p).some((q) => q.building === def.next) ? def : null;
}

/** Adjacency bonuses in force: the facility's plot, its partner's plot and what it does. */
export function adjacencyBonuses(c: CampaignState): { plot: string; next: string; def: AdjacencyDef }[] {
  const out: { plot: string; next: string; def: AdjacencyDef }[] = [];
  for (const p of c.base.plots) {
    const def = ADJACENCY.find((a) => a.facility === p.building);
    const partner = def && neighbours(c, p).find((q) => q.building === def.next);
    if (def && partner) out.push({ plot: p.id, next: partner.id, def });
  }
  return out;
}

/** Whether some built `facility` has its adjacency bonus. */
export function hasAdjacency(c: CampaignState, facility: FacilityId): boolean {
  return c.base.plots.some((p) => p.building === facility && !!adjacencyAt(c, p, facility));
}

// ------------------------------------------------------------------ regions

export type RegionId = 'na' | 'sa' | 'weu' | 'eeu' | 'afr' | 'me' | 'eas' | 'oce';

export interface RegionDef {
  id: RegionId;
  name: string;
  /** Where it lies on the valley's map (docs/FASE5.md): x west to east, z north (the sierra) to south (the plain), in map units. */
  x: number;
  z: number;
  /** The zones a road joins it to (ROADS). */
  roads: RegionId[];
  /** Credits it adds to every monthly payment once contacted. */
  income: number;
}

// The valley of Kessra (docs/FASE5.md): where each zone lies on the valley's map (Arturo's, 15/10:
// x −15…15 and z −10…10, 51,2 of its pixels a unit) and the roads between them, the same ones La Lata
// drives. Brena (Atalaya's zone) is up in the north-west; the Marea comes up from Los Llanos de
// Ceniza in the south.
export const REGIONS: Record<RegionId, RegionDef> = {
  na: { id: 'na', name: 'Los Llanos de Ceniza', x: 2.87, z: 6.95, roads: ['afr', 'me'], income: 35 },
  sa: { id: 'sa', name: 'Carvik', x: -11.48, z: -0.98, roads: ['weu', 'afr'], income: 25 },
  weu: { id: 'weu', name: 'Brena', x: -6.11, z: -5.86, roads: ['oce', 'sa', 'eeu'], income: 30 },
  eeu: { id: 'eeu', name: 'Velsa', x: 12.64, z: -8.28, roads: ['weu', 'eas'], income: 25 },
  afr: { id: 'afr', name: 'Corran', x: -6.56, z: 4.02, roads: ['oce', 'na', 'sa'], income: 25 },
  me: { id: 'me', name: 'Galen', x: 10.82, z: -2.23, roads: ['oce', 'na'], income: 30 },
  eas: { id: 'eas', name: 'El Pantano Gris', x: 3.79, z: -6.25, roads: ['eeu', 'oce'], income: 35 },
  oce: { id: 'oce', name: 'La Vega', x: -0.39, z: -0.63, roads: ['weu', 'eas', 'afr', 'me'], income: 25 },
};

/** Every road once, as a pair of zones (the map draws them). */
export const ROADS: [RegionId, RegionId][] = (Object.keys(REGIONS) as RegionId[]).flatMap((a) => REGIONS[a].roads.filter((b) => a < b).map((b): [RegionId, RegionId] => [a, b]));

/** The shortest way by road from `a` to `b`, both included ([a] when they are the same); La Lata drives it. Ties go by the order of each zone's roads. */
export function roadRoute(a: RegionId, b: RegionId): RegionId[] {
  const prev = new Map<RegionId, RegionId | null>([[a, null]]);
  const queue: RegionId[] = [a];
  for (let k = 0; k < queue.length && !prev.has(b); k++) {
    for (const n of REGIONS[queue[k]!].roads) {
      if (prev.has(n)) continue;
      prev.set(n, queue[k]!);
      queue.push(n);
    }
  }
  const route: RegionId[] = [];
  for (let at: RegionId | null | undefined = b; at; at = prev.get(at)) route.unshift(at);
  return route;
}

/** Road stretches on the shortest way from `a` to `b`. */
export function roadHops(a: RegionId, b: RegionId): number {
  return roadRoute(a, b).length - 1;
}

export const HOME_REGION: RegionId = 'weu';

// ----------------------------------------------------------------- the Marea

/**
 * Zombie activity (`c.doom`) at which each zone falls under the Marea, by the
 * road stretches between it and Los Llanos, where it comes up from (docs/FASE5.md).
 * Brena never falls: at DOOM_MAX the campaign is lost.
 */
export const MAREA_THRESHOLDS = [4, 6, 9, 10, 11];
export const MAREA_SOURCE: RegionId = 'na';
/** Nidos gather under the Marea or beside it this many times as often as elsewhere. */
const NIDO_TIDE_WEIGHT = 3;

/** The activity at which `id` falls under the Marea; null for Brena. */
export function mareaThreshold(id: RegionId): number | null {
  if (id === HOME_REGION) return null;
  return MAREA_THRESHOLDS[Math.min(roadHops(MAREA_SOURCE, id), MAREA_THRESHOLDS.length - 1)]!;
}

/** Under the Marea: its enclave sends nothing, its missions bring one more group of the dead and it can't be newly contacted. It recedes when the activity drops. */
export function underMarea(c: Pick<CampaignState, 'doom'>, id: RegionId): boolean {
  const t = mareaThreshold(id);
  return t !== null && c.doom >= t;
}

export function floodedZones(c: Pick<CampaignState, 'doom'>): RegionId[] {
  return (Object.keys(REGIONS) as RegionId[]).filter((id) => underMarea(c, id));
}

/** The group of the dead a mission under the Marea adds. */
function tidePod(difficulty: number): TemplateId[] {
  return difficulty >= 3 ? ['zombie', 'zombie', 'xenoid'] : ['zombie', 'zombie'];
}

/** A mission whose zone is under the Marea: one more group of the dead (the last); out of it, back as it was. */
function tide(c: CampaignState, o: MissionOffer): void {
  if (o.final) return;
  const flooded = underMarea(c, o.region);
  if (flooded && !o.tide) {
    o.pods = [...o.pods, tidePod(o.difficulty)];
    o.tide = true;
  } else if (!flooded && o.tide) {
    o.pods = o.pods.slice(0, -1);
    delete o.tide;
  }
}

/**
 * Looks where the Marea is now (it follows the activity): an alert for every
 * zone it reaches or frees, and the missions there made harder or not.
 * Whether any zone changed.
 */
function syncMarea(c: CampaignState): boolean {
  const now = floodedZones(c);
  const before = c.flooded ?? [];
  // Before the Bastion's level 4 nobody knows the word «Marea»: Lore's lines say it as they can.
  const alert = (id: RegionId, reached: boolean) => {
    const a = mareaAlert(bastionLevel(c), REGIONS[id].name, reached);
    log(c, a.text, reached ? 'bad' : 'good', { kicker: a.kicker, title: a.title });
  };
  for (const id of now.filter((x) => !before.includes(x))) alert(id, true);
  for (const id of before.filter((x) => !now.includes(x))) alert(id, false);
  c.flooded = now;
  for (const o of c.offers) tide(c, o);
  return now.length !== before.length || now.some((x) => !before.includes(x));
}

/** Zones a road joins to Brena or to a zone already contacted: the radio reaches as far as the people do. */
function roadReachable(c: CampaignState, id: RegionId): boolean {
  return REGIONS[id].roads.some((n) => n === HOME_REGION || c.regions.find((r) => r.id === n)?.contacted);
}

/** Why `id` can't be contacted now, or null (docs/FASE5.md: only by road, and never under the Marea). */
export function contactBlocker(c: CampaignState, id: RegionId): string | null {
  const region = c.regions.find((r) => r.id === id);
  if (!region || region.contacted) return 'Ya tenemos contacto con esa zona.';
  if (underMarea(c, id)) return floodedContactReason(bastionLevel(c), REGIONS[id].name);
  if (!roadReachable(c, id)) {
    // The zones on the way that can be contacted now; failing those, the first one on the road there.
    const next = REGIONS[id].roads.filter((n) => !c.regions.find((r) => r.id === n)?.contacted && roadReachable(c, n) && !underMarea(c, n));
    const route = roadRoute(HOME_REGION, id).find((n) => !c.regions.find((r) => r.id === n)?.contacted);
    return noRoadReason((next.length ? next : route ? [route] : []).map((n) => REGIONS[n].name));
  }
  if (c.regions.filter((r) => r.contacted).length >= contactCapacity(c)) return 'No podemos mantener más contactos: construid un centro de comunicaciones.';
  if (!canAfford(c, contactCost(c))) return 'No hay suministros suficientes.';
  return null;
}

export interface RegionState {
  id: RegionId;
  contacted: boolean;
  /** Alien facility: pushes the project forward until destroyed. */
  facility: { nextDoomDay: number } | null;
}

// ------------------------------------------------------------------- events

/** `quest`: the current decision of a side quest (`CampaignState.questEvent`), built by `eventDef`. */
export type CampaignEventId = 'smugglers' | 'refugees' | 'signal' | 'convoy' | 'defector' | 'blackMarket' | 'sabotage' | 'scientist' | 'simon' | 'finale' | 'quest';

interface EventOption {
  label: string;
  effect: string;
  available?: (c: CampaignState) => boolean;
  apply: (c: CampaignState, rng: Rng) => void;
}

export interface EventDef {
  id: CampaignEventId;
  title: string;
  text: string;
  /** Story events only come when the story calls them, never at random. */
  story?: true;
  options: [EventOption, EventOption];
}

const credit = (c: CampaignState, amount: number) => {
  c.credits += amount;
};

export const EVENTS: Record<Exclude<CampaignEventId, 'quest'>, EventDef> = {
  smugglers: {
    id: 'smugglers',
    title: 'Contrabandistas',
    text: 'Unos contrabandistas han vaciado un cuartel abandonado y ofrecen lo que sacaron.',
    options: [
      {
        label: 'Comprárselo',
        effect: '−40 suministros; dos granadas extra para la armería',
        available: (c) => c.credits >= 40,
        apply: (c) => {
          credit(c, -40);
          c.inventory.fragGrenade = (c.inventory.fragGrenade ?? 0) + 2;
        },
      },
      { label: 'Rechazar la oferta', effect: 'Sin efecto', apply: () => {} },
    ],
  },
  refugees: {
    id: 'refugees',
    title: 'Refugiados',
    text: 'Familias huidas de un enclave caído piden asilo en el Bastión. Entre ellas hay antiguos soldados.',
    options: [
      {
        label: 'Acogerlas',
        effect: '−30 suministros; se une un recluta',
        available: (c) => c.credits >= 30,
        apply: (c, rng) => {
          credit(c, -30);
          const s = addSoldier(c, neediestPlayer(c), rng);
          log(c, `${s.name} se une al Bastión (${veterancyName(s)}).`, 'good');
        },
      },
      { label: 'Cambiarles provisiones por lo que saben', effect: '−10 suministros, +8 de experiencia del Bastión', apply: (c, rng) => (credit(c, -10), gainXp(c, 8, rng)) },
    ],
  },
  signal: {
    id: 'signal',
    title: 'Transmisión interceptada',
    text: 'Morse ha captado en onda corta una transmisión cifrada de los Cuervos.',
    options: [
      { label: 'Descifrarla', effect: '+10 de experiencia del Bastión', apply: (c, rng) => gainXp(c, 10, rng) },
      { label: 'Venderla en el mercado negro', effect: '+30 suministros', apply: (c) => credit(c, 30) },
    ],
  },
  convoy: {
    id: 'convoy',
    title: 'Convoy emboscado',
    text: 'Un convoy de un enclave aliado ha caído en una emboscada de los Cazadores cerca del Bastión.',
    options: [
      {
        label: 'Enviar a alguien a socorrerlo',
        effect: 'Un soldado al azar queda herido 4 días; +60 suministros',
        available: (c) => c.soldiers.some((s) => soldierReady(s)),
        apply: (c, rng) => {
          const s = rng.pick(c.soldiers.filter((x) => soldierReady(x)));
          s.woundedDays = 4;
          credit(c, 60);
          log(c, `${s.name} vuelve herido del rescate del convoy.`, 'bad');
        },
      },
      { label: 'No arriesgar a nadie', effect: 'Los muertos avanzan (+1)', apply: (c) => (c.doom += 1) },
    ],
  },
  defector: {
    id: 'defector',
    title: 'Un desertor',
    text: 'Un Saqueador de los Cuervos quiere desertar y ofrece lo que sabe.',
    options: [
      { label: 'Interrogarlo', effect: '+12 de experiencia del Bastión', apply: (c, rng) => gainXp(c, 12, rng) },
      { label: 'Mandarlo a llevarse una horda lejos', effect: 'Los muertos retroceden (−1)', apply: (c) => (c.doom = Math.max(0, c.doom - 1)) },
    ],
  },
  blackMarket: {
    id: 'blackMarket',
    title: 'Mercado negro',
    text: 'Los Camioneros han montado mercado en la carretera, con género interesante.',
    options: [
      { label: 'Comprar mapas de la zona', effect: '−40 suministros, +15 de experiencia del Bastión', available: (c) => c.credits >= 40, apply: (c, rng) => (credit(c, -40), gainXp(c, 15, rng)) },
      { label: 'Venderles la munición que sobra', effect: '+35 suministros', apply: (c) => credit(c, 35) },
    ],
  },
  sabotage: {
    id: 'sabotage',
    title: 'Sabotaje en la base',
    text: 'Alguien ha cortado los cables del generador. Desde dentro.',
    options: [
      { label: 'Repararlo a toda prisa', effect: '−35 suministros', available: (c) => c.credits >= 35, apply: (c) => credit(c, -35) },
      {
        label: 'Repararlo con calma',
        effect: 'Las obras en curso se retrasan 4 días',
        apply: (c) => {
          for (const w of c.base.works) w.daysLeft += 4;
        },
      },
    ],
  },
  scientist: {
    id: 'scientist',
    title: 'Una científica fugada',
    // Kept for campaigns saved with it pending; the story now tells it (chapter 2).
    story: true,
    text: 'Una científica huida de un laboratorio de la Congregación pide protección. Dice que la obligaban a probar curas en gente.',
    options: [
      {
        label: 'Darle asilo',
        effect: '−20 suministros, +12 de experiencia del Bastión',
        available: (c) => c.credits >= 20,
        apply: (c, rng) => (credit(c, -20), gainXp(c, 12, rng)),
      },
      { label: 'Llevarla con otro enclave', effect: '+15 suministros', apply: (c) => credit(c, 15) },
    ],
  },
  simon: {
    id: 'simon',
    title: 'Simão Ferreira',
    text: 'Un desertor de los Cuervos llama a la puerta del Bastión. Se llama Simão Ferreira. Dice que sabe dónde tienen los Cuervos a la niña inmune, y cómo entrar sin que se enteren.',
    story: true,
    options: [
      {
        label: 'Confiar en él',
        effect: 'Simão os guiará por dentro: «Los inmunes» a la dificultad prevista',
        apply: (c) => (c.story.simon = 'trusted'),
      },
      {
        label: 'Interrogarlo y echarlo',
        effect: '+12 de experiencia del Bastión; «Los inmunes» sin ayuda, más difícil',
        apply: (c, rng) => {
          c.story.simon = 'refused';
          gainXp(c, 12, rng);
        },
      },
    ],
  },
  finale: {
    id: 'finale',
    title: 'La vacuna',
    text: 'La Marea se aleja detrás de la sirena de La Lata y el campamento de la Congregación ha caído. Nwosu tiene en la mano las primeras dosis de la vacuna, y no hay para todos. Hay que decidir ahora.',
    story: true,
    options: [
      {
        label: 'Para todos',
        effect: 'Morse lee la fórmula por todas las frecuencias. La oyen los enclaves, y también los Cuervos.',
        apply: (c) => ((c.ending = 'break'), (c.outcome = 'victory')),
      },
      {
        label: 'Para la Red',
        effect: 'La Lata reparte las dosis enclave por enclave, solo a quienes estuvieron. La fórmula se queda en Atalaya, bajo llave.',
        apply: (c) => ((c.ending = 'record'), (c.outcome = 'victory')),
      },
    ],
  },
};

/** The pending event, side-quest decisions included. */
export function eventDef(c: CampaignState): EventDef | null {
  if (!c.event) return null;
  if (c.event !== 'quest') return EVENTS[c.event];
  const q = c.quests.find((x) => x.id === c.questEvent);
  const step = q && QUESTS[q.id].steps[q.step];
  if (!q || step?.type !== 'event') return null;
  const option = (i: 0 | 1): EventOption => ({
    label: fill(step.options[i].label, q.names),
    effect: fill(step.options[i].effect, q.names),
    apply: (cc, rng) => answerQuest(cc, i, rng),
  });
  return { id: 'quest', title: fill(step.title, q.names), text: fill(step.text, q.names), story: true, options: [option(0), option(1)] };
}

export function eventOptionAvailable(c: CampaignState, option: 0 | 1): boolean {
  const def = eventDef(c)?.options[option];
  return !!def && (!def.available || def.available(c));
}

// -------------------------------------------------------------------- state

export const RECRUIT_COST = 30;
export const MAX_SOLDIERS_PER_PLAYER = 6;

/** Soldiers each player may keep: a commander alone runs both kits. */
export function soldierCap(c: Pick<CampaignState, 'players'>): number {
  return c.players.length > 1 ? MAX_SOLDIERS_PER_PLAYER : 2 * MAX_SOLDIERS_PER_PLAYER;
}
export const DOOM_MAX = 12;
const DOOM_INTERVAL = 12;
// A mission takes no days (playtest 2026-10-14, docs/PRUEBA-2026-10-14.md P7):
// time passes waiting for news, not flying out. Before, La Lata's trip took
// 2 days and one more a road stretch (docs/FASE5.md).
/** Least days between one story chapter won and the next one opening. */
const STORY_GAP = 8;
const FACILITY_DOOM_INTERVAL = 14;
const MAX_FACILITIES = 3;
const MAX_OFFERS = 3;
const MONTH = 30;
/** Monthly payment from the Council before region income. */
const BASE_INCOME = 60;
/** Credits for each alien body sold after a won mission. */
const BODY_VALUE: Partial<Record<TemplateId, number>> = { trooper: 2, officer: 4, lancer: 3, xenoid: 4, mec: 6, sectoid: 5, bloater: 3 };

export interface Loadout {
  utility: ItemId | null;
  ammo: ItemId | null;
}

export interface CampaignSoldier {
  id: string;
  name: string;
  owner: Slot;
  kills: number;
  missions: number;
  /** Fase 10 (skills.ts): experience in each line, from what it does in the missions; the bars follow from it. */
  skills: Skills;
  /** What each line's filled bars added to its stat, in percent. */
  growth: Growth;
  /** The cells unlocked on the hex board (board.ts), in the order they were bought; the core isn't listed. */
  board: CellId[];
  woundedDays: number;
  alive: boolean;
  /** Campaign day of the mission the soldier fell in (the Camposanto lists it). */
  fellDay?: number;
  loadout: Loadout;
  /** Shown in quotes next to the name; empty for none. */
  nickname: string;
  appearance: Appearance;
  /** Left by side quests, for good (quests.ts TRAITS). */
  traits: TraitId[];
  /** Came back from a mission bitten (docs/FASE3.md): turns on `turnDay` unless treated. Can't deploy meanwhile. */
  infection?: { since: number };
  /** Why the fallen fell, when it wasn't in combat: 'turned', the infection won (TURNED_CAUSE). */
  fellCause?: 'turned';
}

export interface MissionReward {
  credits?: number;
  recruit?: ClassId;
  /** Slows the enemy project. */
  doom?: number;
  /** Medicines (✚, docs/FASE3.md). */
  medicines?: number;
  /** Rations and parts (docs/FASE4.md). */
  food?: number;
  parts?: number;
}

export interface MissionOffer {
  id: string;
  name: string;
  kind: MissionKind;
  difficulty: number;
  /** Last day it can be launched; null for the final mission and facility assaults. */
  expiresDay: number | null;
  reward: MissionReward;
  /** Procedural battlefield (biome + seed), so both players fight on the same map. */
  map: MapSource;
  pods: TemplateId[][];
  final: boolean;
  region: RegionId;
  /** Assault on the alien facility of this region. */
  facility: RegionId | null;
  /** Story chapter (1–7, see lore.ts STORY); stays until won. */
  story: number | null;
  /** Side quest whose current step this is; losing it or letting it expire fails the quest. */
  quest: QuestId | null;
  /** Flown at night (byNight): its reward already counts the night's half as much again. Missing: by day. */
  night?: boolean;
  /** Its zone is under the Marea (docs/FASE5.md): one more group of the dead, the last of `pods`; gone when the zone is freed. */
  tide?: true;
  /** The living hold it (docs/ZOMBIS-PRIMERO.md): their bands among the dead, and their garrison. Missing: the dead's alone. */
  living?: true;
  /** An ordinary rescue: who is held (playtest 2026-10-15, #20), so the report and the card can name them. */
  vip?: string;
  /** A sabotage: what makes the noise (#14). */
  alarm?: Alarm;
}

export type ProposalAction =
  | { type: 'chooseMission'; id: string }
  | { type: 'clear'; plot: string }
  | { type: 'build'; plot: string; facility: FacilityId }
  | { type: 'upgrade'; plot: string }
  | { type: 'contact'; region: RegionId }
  | { type: 'event'; option: 0 | 1 }
  | { type: 'investigate'; id: string }
  | { type: 'craft'; project: CraftId };

export interface Proposal {
  by: Slot;
  action: ProposalAction;
  text: string;
}

/** One line of the debrief: how a soldier of the squad came out of the mission. */
export interface ReportSoldier {
  id: string;
  name: string;
  owner: Slot;
  /** `lost`: left behind when the bus went (docs/PRUEBA-2026-10-14.md P1). */
  status: 'ok' | 'wounded' | 'dead' | 'lost';
  woundedDays: number;
  kills: number;
  /** Fase 10 (skills.ts): what the mission taught, line by line, what it did, and the bars that filled (a point each). */
  learned?: Skills;
  deeds?: Deeds;
  bars?: BarFilled[];
}

export interface MissionReport {
  /** Campaign day the mission was flown. */
  day: number;
  missionName: string;
  victory: boolean;
  reason: EndReason;
  squad: ReportSoldier[];
  reward: MissionReward | null;
  doomChange: number;
  /** Bastion experience earned, and the levels before and after. */
  xp: number;
  levelFrom: number;
  levelTo: number;
  /** Alien bodies sold and what they fetched. */
  bodies: number;
  salvage: number;
  /** Story chapter the mission belonged to. */
  story: number | null;
  /** What the squad brought back from searching (docs/FASE2.md), already added; missing in older reports. */
  loot?: MissionLoot;
  /** It was a night mission (docs/ALTURA-Y-NOCHE.md). */
  night?: boolean;
  /** Soldiers (campaign ids) who came back bitten (docs/FASE3.md); missing in older reports. */
  infected?: string[];
  /** Parts stripped from the Acorazados brought down (docs/FASE4.md), already added. */
  scrap?: number;
}

/** The haul of a mission: suministros (to the credits), accessories (to the inventory), medicines, rations, parts and found weapons. */
export interface MissionLoot {
  supplies: number;
  items: ItemId[];
  medicines: number;
  /** Missing in reports from before phase 4. */
  food?: number;
  parts?: number;
  weapons?: FoundWeaponId[];
  /** The debrief's line (lootLine), or null when nothing came back. */
  line: string | null;
}

export interface LogEntry {
  day: number;
  text: string;
  tone: 'info' | 'good' | 'bad';
  /** Also shown to the players as a pop-up with this kicker and title; `rumor` lets it offer to listen. */
  alert?: { kicker: string; title: string; rumor?: string };
}

/** A side quest under way. */
export interface QuestState {
  id: QuestId;
  /** Index of the current step in `QUESTS[id].steps`. */
  step: number;
  region: RegionId;
  /** Personal quests belong to one commander. */
  owner: Slot | null;
  /** Campaign soldier the quest is about. */
  soldier: string | null;
  /** Values for the placeholders of its texts. */
  names: Record<string, string>;
  /** Hidden option of its 'guess' decisions (0 or 1). */
  secret: number;
}

/** Something heard in a region; listening to it for a few days tells what it was. */
export interface Rumor {
  id: string;
  region: RegionId;
  text: string;
  /** Gone after this day unless the Bastion is listening to it. */
  expiresDay: number;
  /** Days of listening still needed. */
  daysLeft: number;
}

/**
 * Campaigns from before Fase 10 (docs/FASE10.md) aren't carried over: the
 * progression is another. The host puts such a save aside and starts anew.
 */
export const FRESH_CAMPAIGN_VERSION = 19;

/** A saved campaign the current progression can't take (campaignOutdated): the host keeps it aside. */
export function campaignOutdated(c: { version: number }): boolean {
  return c.version < FRESH_CAMPAIGN_VERSION;
}

/** Bumped when the campaign shape changes; older campaigns go through `migrateCampaign`. */
export const CAMPAIGN_VERSION = 19;

export interface CampaignState {
  version: typeof CAMPAIGN_VERSION;
  day: number;
  /** Seats that started the campaign. */
  players: Slot[];
  /** The only currency: builds, buys accessories, recruits and contacts regions. */
  credits: number;
  /** Bastion experience from combat; its level unlocks technology (progression.ts). */
  xp: number;
  soldiers: CampaignSoldier[];
  /** Accessories bought: copies owned (equipped ones included). */
  inventory: Partial<Record<ItemId, number>>;
  /** Medicines (✚): from searching, some rewards and the enclaves; they treat the infected and heal wounds (docs/FASE3.md). */
  medicines?: number;
  /** Next day the enclaves send medicines (ENCLAVES). */
  nextMedicineDay?: number;
  /** A treatment one commander has proposed, waiting for both keys (like launching); null for none. */
  treatment?: { soldier: string; ready: [boolean, boolean] } | null;
  // Food and the Taller (docs/FASE4.md); missing before version 13.
  /** Rations: the Bastion eats every day (dailyRations). */
  food?: number;
  /** No rations for the day: −10 will on missions and wounds heal half as fast, until there are again. */
  hungry?: boolean;
  /** Next day La Vega sends rations. */
  nextFoodDay?: number;
  /** Parts, for the Taller's work. */
  parts?: number;
  /** What the Taller has made for everyone (CRAFTS). */
  crafted?: CraftId[];
  /** Good weapons found, kept in the Armería: each for one soldier at most. */
  foundWeapons?: FoundWeapon[];
  /** Loose improvements for the weapons, waiting in the Armería to be mounted (docs/FASE9.md §5). */
  weaponMods?: Partial<Record<WeaponModId, number>>;
  /** Zones under the Marea when it was last looked at (syncMarea), for the alerts; missing before version 14. */
  flooded?: RegionId[];
  /** `works`: the projects going on, one per crew (two, three with the works brigade). */
  base: { plots: BasePlot[]; works: Construction[] };
  regions: RegionState[];
  offers: MissionOffer[];
  activeMission: string | null;
  squad: string[];
  /** Missions flown so far: the operations get a little harder with them. */
  missionsFlown?: number;
  /** Hangar pad of each squad member (squadPads): nobody moves when others come and go. */
  pads?: Record<string, number>;
  doom: number;
  nextMissionDay: number;
  nextDoomDay: number;
  nextFacilityDay: number;
  nextSupplyDay: number;
  nextEventDay: number;
  /** Event waiting for an answer; time stands still until then. */
  event: CampaignEventId | null;
  advanceReady: [boolean, boolean];
  launchReady: [boolean, boolean];
  proposal: Proposal | null;
  log: LogEntry[];
  lastReport: MissionReport | null;
  outcome: 'victory' | 'defeat' | null;
  /** How the final mission was resolved, with both keys. */
  ending: Ending | null;
  story: StoryState;
  quests: QuestState[];
  /** How each finished side quest ended, and when. */
  questLog: Partial<Record<QuestId, { outcome: string; day: number }>>;
  /** Side quest whose decision is the pending event (`event: 'quest'`). */
  questEvent: QuestId | null;
  rumors: Rumor[];
  /** Rumour the Bastion is listening to while time passes. */
  listening: string | null;
  nextRumorDay: number;
  /** Technology granted by side quests before its level. */
  techs: TechId[];
  /** How many times each event has come up. */
  seen: Partial<Record<CampaignEventId, number>>;
  /** Tells campaigns apart in each browser (scenes already played); missing in campaigns from before it. */
  uid?: string;
  nextId: number;
}

/** A weapon found searching (docs/FASE4.md), in the Armería. */
export interface FoundWeapon {
  id: string;
  kind: FoundWeaponId;
  /** The soldier who carries it; null, on the rack. Lost with them if they fall. */
  holder: string | null;
  /** 0 to 100 (docs/FASE9.md §5); absent, 100. Below 30 it is «Gastada»; at 0, «Rota». */
  condition?: number;
  /** What is mounted on it, MOD_SLOTS at most. */
  mods?: WeaponModId[];
}

export interface StoryState {
  /** Story chapters won, in order. */
  done: number;
  /** Answer to Simão's visit, which opens chapter 5. */
  simon: SimonChoice;
  /** The next chapter can't open before this day: chapters don't chain back to back. */
  nextDay?: number;
}

// ------------------------------------------------------------------ helpers

export function canAfford(c: CampaignState, cost: number): boolean {
  return c.credits >= cost;
}

function pay(c: CampaignState, cost: number): void {
  c.credits -= cost;
}

export function costText(cost: number): string {
  return cost ? `${cost} §` : 'Gratis';
}

function log(c: CampaignState, text: string, tone: LogEntry['tone'] = 'info', alert?: LogEntry['alert']): void {
  c.log.unshift(alert ? { day: c.day, text, tone, alert } : { day: c.day, text, tone });
  c.log.length = Math.min(c.log.length, 40);
}

function newId(c: CampaignState, prefix: string): string {
  return `${prefix}${++c.nextId}`;
}

/**
 * The sex of someone who joins by name: the story's people (lore.ts NAMED_SEX),
 * a relative by first name (quests.ts RELATIVES), or else a soldier's name.
 */
export function sexOfPerson(name: string): 'f' | 'm' | undefined {
  return NAMED_SEX[name] ?? RELATIVES.find((r) => r.name === name.split(' ')[0])?.sex ?? sexOfName(name);
}

/** A name nobody in the campaign has yet, with the sex it goes with. */
function soldierName(c: CampaignState, rng: Rng): { name: string; sex: 'f' | 'm' } {
  const used = new Set(c.soldiers.map((s) => s.name));
  const free = SOLDIER_NAMES.filter((n) => !used.has(n.name));
  if (free.length) return rng.pick(free);
  const any = rng.pick(SOLDIER_NAMES);
  return { name: `${any.name} ${c.soldiers.length + 1}`, sex: any.sex };
}

/**
 * A new soldier (docs/FASE10.md): no class, a bar filled in one or two lines
 * at random, so no two are alike, and its points still to spend.
 */
function addSoldier(c: CampaignState, owner: Slot, rng: Rng): CampaignSoldier {
  const { name, sex } = soldierName(c, rng);
  const s: CampaignSoldier = {
    id: newId(c, 'c'),
    name,
    owner,
    kills: 0,
    missions: 0,
    ...recruitSkills(rng),
    board: [],
    woundedDays: 0,
    alive: true,
    loadout: { utility: null, ammo: null },
    nickname: '',
    appearance: defaultAppearance(owner, undefined, sex),
    traits: [],
  };
  c.soldiers.push(s);
  return s;
}

/** The player with the fewest soldiers gets newcomers. */
function neediestPlayer(c: CampaignState): Slot {
  const owners = c.players.map((slot) => ({ slot, count: c.soldiers.filter((s) => s.owner === slot && s.alive).length }));
  return owners.sort((a, b) => a.count - b.count)[0]!.slot;
}

export function facilityCount(c: CampaignState, id: FacilityId): number {
  return c.base.plots.filter((p) => p.building === id).length;
}

/** Level of a facility: its best copy's (0 when not built). */
export function facilityLevel(c: CampaignState, id: FacilityId): number {
  return Math.max(0, ...c.base.plots.filter((p) => p.building === id).map((p) => p.level ?? 1));
}

/** The workshop takes 25/35/45 % off accessories, 15 % more next to the armoury. */
export function shopPrice(c: CampaignState, id: ItemId): number {
  const cost = ITEMS[id].cost;
  const level = facilityLevel(c, 'workshop');
  if (!level) return cost;
  const off = 0.25 + 0.1 * (level - 1) + (hasAdjacency(c, 'workshop') ? 0.15 : 0);
  return Math.round(cost * (1 - off));
}

/** A recruit costs less with the training centre next to the barracks. */
export function recruitCost(c: CampaignState): number {
  return hasAdjacency(c, 'training') ? 20 : RECRUIT_COST;
}

/** Beds in the Bastion: the bunker's, and each barracón's (2/3/4, one more next to the Cuartel). */
export function beds(c: CampaignState): number {
  return (
    BUNKER_BEDS +
    c.base.plots
      .filter((p) => p.building === 'quarters')
      .reduce((sum, p) => sum + 1 + (p.level ?? 1) + (adjacencyAt(c, p, 'quarters') ? 1 : 0), 0)
  );
}

/** Beds nobody sleeps in: recruiting needs one. */
export function bedsFree(c: CampaignState): number {
  return beds(c) - c.soldiers.filter((s) => s.alive).length;
}

/** Wound days healed each day: 1, and 2/3/4 with the infirmary (one more next to the Cuartel). */
export function healRate(c: CampaignState): number {
  const level = facilityLevel(c, 'infirmary');
  return level ? 1 + level + (hasAdjacency(c, 'infirmary') ? 1 : 0) : 1;
}

/** Bastion experience a mission's earned XP becomes: +25/40/55 % with simulation, 15 % more next to the Diario. */
export function xpMultiplier(c: CampaignState): number {
  const level = facilityLevel(c, 'simulation');
  return level ? 1.25 + 0.15 * (level - 1) + (hasAdjacency(c, 'simulation') ? 0.15 : 0) : 1;
}

/** Aim every soldier gets from the shooting range: 3/6/10, two more next to the training centre. */
/** Will the Camposanto gives every soldier: +5 at level 2, +10 at level 3. */
export function memorialWill(c: CampaignState): number {
  return [0, 0, 5, 10][facilityLevel(c, 'memorial')]!;
}

export function rangeAim(c: CampaignState): number {
  const level = facilityLevel(c, 'range');
  return level ? [3, 6, 10][level - 1]! + (hasAdjacency(c, 'range') ? 2 : 0) : 0;
}

/**
 * Days La Lata's workshop (the Chapistería) takes off each wound (its stretcher): one a level.
 * Missions take no days any more (docs/PRUEBA-2026-10-14.md P7), so that is
 * what it does from level 1; it also gives a backpack slot (docs/FASE8.md).
 */
export function garageCut(c: CampaignState): { wounds: number } {
  return { wounds: facilityLevel(c, 'garage') };
}

/** Extra days between the enemy clock's steps: 2 per level of the listening post, 2 more next to the relay. */
export function listeningDelay(c: CampaignState): number {
  const level = facilityLevel(c, 'listening');
  return level ? 2 * level + (hasAdjacency(c, 'listening') ? 2 : 0) : 0;
}

/** Days a work takes with the works brigade (−25/40/50 %), at least one. */
export function workDays(c: CampaignState, days: number): number {
  const level = facilityLevel(c, 'crew');
  return level ? Math.max(1, Math.round(days * (1 - [0.25, 0.4, 0.5][level - 1]!))) : days;
}

/** Works that can go on at once: two crews, three with the works brigade. */
export function workCrews(c: CampaignState): number {
  return 2 + (facilityLevel(c, 'crew') ? 1 : 0);
}

/** Why no new work can start on `plot`: every crew busy, or that plot already in works. */
export function worksBlocker(c: CampaignState, plot: BasePlot): string | null {
  if (c.base.works.some((w) => w.plot === plot.id)) return 'Ya hay obras en esa parcela.';
  if (c.base.works.length >= workCrews(c)) return `Todas las cuadrillas están ocupadas (${workCrews(c)}).`;
  return null;
}

/** Why the facility on `plot` can't be raised a level, or null if it can. */
export function upgradable(c: CampaignState, plot: BasePlot): string | null {
  const id = plot.building as FacilityId;
  if (!plot.building || !(plot.building in FACILITIES)) return 'Aquí no hay nada que mejorar.';
  const level = plot.level ?? 1;
  if (level >= 3) return 'Ya está al máximo.';
  const next = upgradeCost(id, level + 1);
  if (bastionLevel(c) < next.level) return `Requiere nivel ${next.level} del Bastión.`;
  return null;
}

/** Copies not equipped by any living soldier. */
export function itemsFree(c: CampaignState, id: ItemId): number {
  const used = c.soldiers.filter((s) => s.alive).reduce((n, s) => n + (s.loadout.utility === id ? 1 : 0) + (s.loadout.ammo === id ? 1 : 0), 0);
  return (c.inventory[id] ?? 0) - used;
}

/** Fit to deploy: alive, healed, and not running a fever from a bite. */
export function soldierReady(s: CampaignSoldier): boolean {
  return s.alive && s.woundedDays === 0 && !s.infection;
}

// ------------------------------------------------------------ infection

/** Medicines a campaign starts with. */
export const START_MEDICINES = 2;
/** Days a bitten soldier lasts at the base before turning; the Enfermería adds INFIRMARY_TURN_DAYS. */
export const TURN_DAYS = 3;
export const INFIRMARY_TURN_DAYS = 2;
/** What curing a wound with medicines takes and gives: 1 ✚ for 3 recovery days fewer. */
export const MEDICATE_COST = 1;
export const MEDICATE_DAYS = 3;
/** The enclaves that send medicines while they are allies (contacted) and stand (no Nido in their region), 1 ✚ each every ENCLAVE_DAYS. */
export const ENCLAVES: RegionId[] = ['oce', 'afr'];
export const ENCLAVE_DAYS = 6;
/** The cause the Caídos read for a soldier who turned (placeholder for Lore). */
export const TURNED_CAUSE = 'Se convirtió';

/** Medicines a treatment takes: 2, or 1 with the Enfermería at level 2 or more. */
export function treatCost(c: CampaignState): number {
  return facilityLevel(c, 'infirmary') >= 2 ? 1 : 2;
}

/** The day a bitten soldier turns, unless treated: 3 days after coming back, 5 with an Enfermería, and one more for each level of the Cuarentena past the first. */
export function turnDay(c: CampaignState, s: CampaignSoldier): number | null {
  if (!s.infection) return null;
  return s.infection.since + TURN_DAYS + (facilityCount(c, 'infirmary') ? INFIRMARY_TURN_DAYS : 0) + Math.max(0, facilityLevel(c, 'quarantine') - 1);
}

/** Whether the bitten may board La Lata (playtest 2026-10-15, #13): only with a Cuarentena to take them to. */
export function hasQuarantine(c: CampaignState): boolean {
  return facilityCount(c, 'quarantine') > 0;
}

/** An enclave that is an ally (contacted) and stands (no Nido in its region). */
function enclaveStands(c: CampaignState, id: RegionId): boolean {
  const r = c.regions.find((x) => x.id === id);
  return !!r?.contacted && !r.facility && !underMarea(c, id);
}

/** Enclaves that send medicines (ENCLAVES). */
function livingEnclaves(c: CampaignState): RegionId[] {
  return ENCLAVES.filter((id) => enclaveStands(c, id));
}

// ------------------------------------------------------------------- food

/** Rations a campaign starts with (docs/FASE4.md). */
export const START_FOOD = 16;
/** The Bastion eats a ration a day for every 4 living soldiers, rounded up. */
export const EATERS_PER_RATION = 4;
/** La Vega, an ally standing, sends 2 rations every 4 days (besides its medicines). */
export const LA_VEGA_FOOD = 2;
export const LA_VEGA_FOOD_DAYS = 4;
/** Will every soldier goes on a mission without, while the Bastion is hungry. */
export const HUNGRY_WILL = 10;

/** Rations the Bastion eats a day. */
export function dailyRations(c: Pick<CampaignState, 'soldiers'>): number {
  return Math.ceil(c.soldiers.filter((s) => s.alive).length / EATERS_PER_RATION);
}

/** Rations the Huerto grows a day: one a level. */
export function gardenYield(c: CampaignState): number {
  return facilityLevel(c, 'garden');
}

/** Rations come in; with enough for a day again, the hunger is over. */
function addFood(c: CampaignState, n: number): void {
  if (n <= 0) return;
  c.food = (c.food ?? 0) + n;
  if (c.hungry && c.food >= dailyRations(c)) {
    c.hungry = false;
    log(c, 'Vuelve a haber raciones: se acabó el hambre.', 'good');
  }
}

// ---------------------------------------------------------- the Taller

/** The Taller's plot, where its work is done. */
function workshopPlot(c: CampaignState): BasePlot | undefined {
  return c.base.plots.find((p) => p.building === 'workshop');
}

/** Why the Taller can't start making `id` now, or null (docs/FASE4.md). */
export function craftBlocker(c: CampaignState, id: CraftId): string | null {
  const def = CRAFTS[id];
  // A weapon or an improvement for the Armería is made again and again (docs/FASE8.md §A).
  if (def.kind !== 'arms' && def.kind !== 'mod' && hasCrafted(c, id)) return 'Ya está hecho.';
  if (c.base.works.some((w) => w.project === id)) return 'Ya se está haciendo.';
  const plot = workshopPlot(c);
  if (!plot) return 'Hace falta el Taller.';
  if ((plot.level ?? 1) < def.workshop) return `Hace falta el Taller a nivel ${def.workshop}.`;
  if (def.tech && !hasTech(c, def.tech)) return `Hace falta el nivel ${TECHS[def.tech].level} del Bastión.`;
  if (def.level && bastionLevel(c) < def.level) return `Hace falta el nivel ${def.level} del Bastión.`;
  if (def.after && !hasCrafted(c, def.after)) return `Antes hay que hacer ${CRAFTS[def.after].the}.`;
  const busy = worksBlocker(c, plot);
  if (busy) return busy;
  if (!canAfford(c, def.cost)) return 'No hay suministros suficientes.';
  if ((c.parts ?? 0) < def.parts) return 'No hay piezas suficientes.';
  return null;
}

/** What a soldier's card reads for the weapon in hand: a good one by its name, a plain one by its tier's; without, the pistol. */
export function weaponLabel(c: CampaignState, s: CampaignSoldier): string {
  const found = foundWeaponOf(c, s);
  if (!found) return WEAPONS.pistol.name;
  return FOUND_WEAPONS[found.kind].name;
}

/** What a soldier carries from the Armería (docs/FASE8.md §A): the weapon in hand, if any, and the machete or the knife. */
export function soldierArms(c: CampaignState, s: Pick<CampaignSoldier, 'id'>): Arms {
  const primary = foundWeaponOf(c, s);
  const melee = meleeWeaponOf(c, s);
  return {
    primary: primary?.kind ?? null,
    ...(primary ? { primaryId: primary.id } : {}),
    ...(primary?.condition !== undefined ? { condition: primary.condition } : {}),
    ...(primary?.mods?.length ? { mods: [...primary.mods] } : {}),
    melee: melee?.kind ?? null,
    ...(melee ? { meleeId: melee.id } : {}),
  };
}

// --------------------------------------------- the Armería (docs/FASE9.md §5)

/** A weapon of the Armería a commander may work on: on the rack, or carried by one of theirs. */
function weaponBlocker(c: CampaignState, slot: Slot, id: string): string | null {
  const w = (c.foundWeapons ?? []).find((x) => x.id === id);
  if (!w) return 'Esa arma no está en la Armería.';
  const holder = w.holder ? c.soldiers.find((s) => s.id === w.holder) : undefined;
  if (holder && holder.owner !== slot) return `La lleva ${holder.name}, de tu compañero.`;
  return null;
}

/** Parts it takes to leave a weapon as new: one every REPAIR_PER_PART of condition missing. */
export function repairParts(w: Pick<FoundWeapon, 'condition'>): number {
  return Math.ceil((100 - (w.condition ?? 100)) / REPAIR_PER_PART);
}

/** Why a weapon can't be mended now, or null: the parts there are mend what they can. */
export function repairBlocker(c: CampaignState, slot: Slot, id: string): string | null {
  const blocked = weaponBlocker(c, slot, id);
  if (blocked) return blocked;
  const w = c.foundWeapons!.find((x) => x.id === id)!;
  if (FOUND_WEAPONS[w.kind].slot !== 'primary' || !repairParts(w)) return 'Está como nueva.';
  if (!(c.parts ?? 0)) return 'No hay piezas.';
  return null;
}

/** Why `mod` can't go on (`on`) or come off that weapon, or null. */
export function modBlocker(c: CampaignState, slot: Slot, id: string, mod: WeaponModId, on: boolean): string | null {
  const blocked = weaponBlocker(c, slot, id);
  if (blocked) return blocked;
  const w = c.foundWeapons!.find((x) => x.id === id)!;
  const mounted = w.mods ?? [];
  if (!on) return mounted.includes(mod) ? null : 'No la lleva puesta.';
  if (FOUND_WEAPONS[w.kind].slot !== 'primary') return 'Solo las armas de fuego llevan mejoras.';
  if (mounted.includes(mod)) return 'Ya la lleva puesta.';
  if (mounted.length >= MOD_SLOTS) return `Lleva ya ${MOD_SLOTS} mejoras: quita una antes.`;
  if (!(c.weaponMods?.[mod] ?? 0)) return `No queda ${WEAPON_MODS[mod].none} en la Armería.`;
  return null;
}

/** A weapon's condition and mods as they came back (docs/FASE9.md §5), for its FoundWeapon. */
function wornAs(w: { condition?: number; mods?: WeaponModId[] }): Pick<FoundWeapon, 'condition' | 'mods'> {
  return { ...(w.condition !== undefined ? { condition: w.condition } : {}), ...(w.mods?.length ? { mods: [...w.mods] } : {}) };
}

/** A bitten soldier whose days ran out turns: one more of the Caídos (docs/FASE3.md). Gear stays in the base. */
function turn(c: CampaignState, s: CampaignSoldier): void {
  s.alive = false;
  s.fellDay = c.day;
  s.fellCause = 'turned';
  delete s.infection;
  s.loadout = { utility: null, ammo: null };
  for (const w of c.foundWeapons ?? []) if (w.holder === s.id) w.holder = null;
  c.squad = c.squad.filter((id) => id !== s.id);
  if (c.treatment?.soldier === s.id) c.treatment = null;
  log(c, `${s.name} no ha superado la fiebre y se ha convertido.${facilityCount(c, 'memorial') ? ' Su nombre ya está en el Camposanto.' : ''}`, 'bad', {
    kicker: 'Enfermería',
    title: `${s.name} se ha convertido`,
  });
}

export function contactCapacity(c: CampaignState): number {
  const quests = questOutcomes(c).reduce((sum, q) => sum + (q.outcome.contacts ?? 0), 0);
  const relays = c.base.plots.filter((p) => p.building === 'relay').reduce((sum, p) => sum + 1 + (p.level ?? 1), 0);
  return 2 + relays + (hasAdjacency(c, 'relay') ? 1 : 0) + (hasTech(c, 'encryption') ? 1 : 0) + quests;
}

export function contactCost(c: CampaignState): number {
  return 30 + 20 * c.regions.filter((r) => r.contacted).length;
}

/** Why `id` can't be built on `plot`, or null if it can. */
export function buildable(c: CampaignState, plot: BasePlot, id: FacilityId): string | null {
  const def = FACILITIES[id];
  if (plot.terrain !== 'clear') return 'Hay que despejar el terreno primero.';
  if (plot.building) return 'Ya hay un edificio aquí.';
  if (!neighbours(c, plot).some((q) => q.building)) return 'Tiene que estar pegado a otro edificio.';
  if (bastionLevel(c) < def.level) return `Requiere nivel ${def.level} del Bastión.`;
  if (def.unique && (facilityCount(c, id) > 0 || c.base.works.some((w) => w.kind === 'build' && w.facility === id))) return 'Solo se puede construir una.';
  return null;
}

// ----------------------------------------------------------------- missions

/** Mission types offered by the resistance network, with their relative frequency. */
const OFFER_KINDS: [MissionKind, number][] = [
  ['elimination', 3],
  ['recovery', 2],
  ['hack', 2],
  ['rescue', 2],
  ['sabotage', 1],
  ['scavenge', 2],
];

function pickKind(rng: Rng): MissionKind {
  const total = OFFER_KINDS.reduce((sum, [, w]) => sum + w, 0);
  let roll = rng.int(total);
  for (const [kind, w] of OFFER_KINDS) {
    if (roll < w) return kind;
    roll -= w;
  }
  return 'elimination';
}

/** Difficulty of a new operation: it grows with the days and, a little, with the missions flown (second playtest). */
function difficultyNow(c: CampaignState, rng: Rng): number {
  return Math.min(4, 1 + Math.floor(c.day / 18 + (c.missionsFlown ?? 0) / 10) + (rng.int(4) === 0 ? 1 : 0));
}

/** Every 4 missions flown the enemy sends one more to an operation, up to 2. */
export function veteranEnemies(c: Pick<CampaignState, 'missionsFlown'>): number {
  return Math.min(2, Math.floor((c.missionsFlown ?? 0) / 4));
}

/**
 * The enemy groups of an operation, with what side quests left in its region
 * (missionPods: a Saqueo has the dead scattered about; a mission of the
 * living, their bands among the dead). The veterans' extra bodies are of
 * whoever holds it.
 */
function regionPods(c: CampaignState, region: RegionId, difficulty: number, kind: MissionKind, living = false): TemplateId[][] {
  const pods = missionPods(difficulty, kind, living);
  const extra = regionEnemy(c, region);
  if (extra) pods[0]!.push(extra);
  const veteran: TemplateId = kind === 'scavenge' ? 'zombie' : living ? (difficulty >= 3 ? 'lancer' : 'trooper') : difficulty >= 3 ? 'xenoid' : 'zombie';
  for (let i = 0; i < veteranEnemies(c); i++) pods[(i + 1) % pods.length]!.push(veteran);
  return pods;
}

function makeOffer(c: CampaignState, rng: Rng, final = false, at?: RegionId): MissionOffer {
  const difficulty = final ? 4 : difficultyNow(c, rng);
  const kind: MissionKind = final ? 'elimination' : pickKind(rng);
  // Relays live in alien facilities, Saqueos in towns; everything else happens in towns or the countryside.
  const biome: Biome = final || kind === 'sabotage' ? 'facility' : kind === 'scavenge' ? 'city' : rng.pick(['city', 'city', 'wilds'] as const);
  const contacted = c.regions.filter((r) => r.contacted).map((r) => r.id);
  const region = at ?? (rng.int(10) < 7 && contacted.length ? rng.pick(contacted) : rng.pick(Object.keys(REGIONS) as RegionId[]));
  // Data and relays are worth more to the Council than a firefight; a Saqueo pays in what it brings back.
  const bonus = kind === 'recovery' || kind === 'hack' || kind === 'sabotage' ? 20 : kind === 'scavenge' ? -30 : 0;
  const reward: MissionReward = final ? {} : { credits: 50 + difficulty * 25 + bonus + rng.int(4) * 5 };
  if (!final && (kind === 'rescue' || rng.int(5) === 0)) reward.recruit = rng.pick(['assault', 'grenadier', 'sharpshooter', 'specialist'] as const);
  if (!final && (kind === 'sabotage' || (kind === 'hack' && rng.int(3) === 0))) reward.doom = 1;
  const seed = rng.int(2 ** 31);
  // Who a rescue is for and what a sabotage silences, chosen now so the report can say it (playtest 2026-10-15, #14, #20).
  const vip = kind === 'rescue' && !final ? VIP_NAMES[seed % VIP_NAMES.length] : undefined;
  const alarm: Alarm | undefined = kind === 'sabotage' ? (['factory', 'factory', 'police', 'ambulance'] as const)[seed % 4] : undefined;
  // The scenarios (playtest 2026-10-14, #25), by the map's seed: half the countryside is a service area on the
  // forest road, a third of the towns an industrial estate.
  const site: Site | undefined = final ? undefined : biome === 'wilds' && seed % 2 === 0 ? 'roadside' : biome === 'city' && seed % 3 === 0 ? 'industrial' : undefined;
  // An ordinary rescue is someone the Cuervos hold, and the last mission is the Congregación's camp (docs/ZOMBIS-PRIMERO.md); every other operation is the dead's.
  const living = final || kind === 'rescue';
  return byNight(withProvisions(withMedicines({
    id: newId(c, 'm'),
    // An ordinary operation is named for where it goes (Lore): «El colegio de Corran».
    name: final ? 'El campamento de la sierra' : missionName(biome, REGIONS[region].name, seed, site),
    kind,
    difficulty,
    expiresDay: final ? null : c.day + 6 + rng.int(4),
    reward,
    map: { kind: 'generated', biome, seed, ...(site ? { site } : {}) },
    pods: regionPods(c, region, difficulty, kind, living),
    final,
    region,
    facility: null,
    story: null,
    quest: null,
    ...(living ? { living: true as const } : {}),
    ...(vip ? { vip } : {}),
    ...(alarm ? { alarm } : {}),
  })));
}

/** Rations and parts some operations pay (docs/FASE4.md), by seed: one in three pays rations, one in four parts. */
export const REWARD_FOOD = 4;
export const REWARD_PARTS = 2;

function withProvisions(o: MissionOffer): MissionOffer {
  if (o.final || o.map.kind !== 'generated') return o;
  const roll = Math.imul(o.map.seed ^ 0x666f6f64, 0x2c1b3c6d) >>> 0;
  const food = roll % 3 === 0 ? { food: REWARD_FOOD } : {};
  const parts = (roll >>> 8) % 4 === 0 ? { parts: REWARD_PARTS } : {};
  return { ...o, reward: { ...o.reward, ...food, ...parts } };
}

/** Some operations pay medicines too (docs/FASE3.md): the recoveries (a pharmacy, a courier's bag) and one in four of the rest, by seed. */
function withMedicines(o: MissionOffer): MissionOffer {
  if (o.final || o.map.kind !== 'generated') return o;
  const lucky = (Math.imul(o.map.seed ^ 0x6d656473, 0x27d4eb2d) >>> 0) % 4 === 0;
  return o.kind === 'recovery' || lucky ? { ...o, reward: { ...o.reward, medicines: 1 } } : o;
}

/** About one operation in three is flown at night (docs/ALTURA-Y-NOCHE.md), as its battlefield's seed says. */
export function nightFor(seed: number): boolean {
  return (Math.imul(seed ^ 0x9e3779b9, 0x85ebca6b) >>> 0) % 3 === 0;
}

/**
 * An offer whose seed says night becomes a night mission, paying half as much
 * again in suministros (NIGHT.supplies). Story chapters and the final mission
 * are flown by day.
 */
export function byNight(o: MissionOffer): MissionOffer {
  if (o.final || o.story || o.map.kind !== 'generated' || !nightFor(o.map.seed)) return o;
  const credits = o.reward.credits ? { credits: Math.round(o.reward.credits * NIGHT.supplies) } : {};
  return { ...o, night: true, reward: { ...o.reward, ...credits } };
}

/** Assault on a Nido (alien facility in the code): silencing its sirena sets the doom track back. */
function makeFacilityOffer(c: CampaignState, region: RegionId, rng: Rng): MissionOffer {
  const difficulty = Math.min(4, difficultyNow(c, rng) + 1);
  return byNight({
    id: newId(c, 'm'),
    name: `Nido ${ofZone(REGIONS[region].name)}`,
    kind: 'sabotage',
    difficulty,
    expiresDay: null,
    reward: { credits: 120 + difficulty * 20, doom: 2 },
    map: { kind: 'generated', biome: 'facility', seed: rng.int(2 ** 31) },
    pods: podPlan(difficulty),
    final: false,
    region,
    facility: region,
    story: null,
    quest: null,
    // A Nido's noise is a factory's alarm (Lore, #14).
    alarm: 'factory',
  });
}

/** The mission of a story chapter: fixed place and type, and it never expires. */
function makeStoryOffer(c: CampaignState, chapter: number, rng: Rng): MissionOffer {
  const def = STORY[chapter - 1]!;
  // Without Simão inside, the Archive is harder to crack.
  const difficulty = Math.min(4, def.difficulty + (chapter === SIMON_CHAPTER && c.story.simon === 'refused' ? 1 : 0));
  return {
    id: newId(c, 'm'),
    name: def.title,
    kind: def.kind,
    difficulty,
    expiresDay: null,
    reward: { ...def.reward },
    map: { kind: 'generated', biome: def.biome, seed: rng.int(2 ** 31) },
    pods: missionPods(difficulty, def.kind, def.living),
    final: false,
    region: def.region,
    facility: null,
    story: chapter,
    quest: null,
    ...(def.living ? { living: true as const } : {}),
    // The Cuervos take their sirens from patrol cars (Lore, #14).
    ...(def.kind === 'sabotage' ? { alarm: 'police' as const } : {}),
  };
}

/**
 * Opens whatever the story has ready: the next chapter's mission once the
 * Bastion reaches its level, Simão's visit before chapter 5 and, with every
 * chapter won and the top level reached, the final mission.
 */
function refreshStory(c: CampaignState, rng: Rng): void {
  if (c.outcome) return;
  const next = c.story.done + 1;
  if (next <= STORY.length && bastionLevel(c) >= next && c.day >= (c.story.nextDay ?? 0)) {
    if (next === SIMON_CHAPTER && !c.story.simon) {
      if (!c.event) {
        c.event = 'simon';
        log(c, 'Alguien llama a la puerta del Bastión.');
      }
    } else if (!c.offers.some((o) => o.story === next)) {
      const offer = makeStoryOffer(c, next, rng);
      c.offers.push(offer);
      log(c, `Nueva misión de historia: ${offer.name}.`, 'good');
    }
  }
  if (c.story.done >= STORY.length && hasTech(c, 'signal') && !c.offers.some((o) => o.final)) {
    c.offers.push(makeOffer(c, rng, true));
    log(c, '¡La Lata está lista! La misión final está disponible.', 'good');
  }
}

/** Opens the story's next beats and the side quests whose time has come. */
function refreshNarrative(c: CampaignState, rng: Rng): void {
  refreshStory(c, rng);
  refreshQuests(c, rng);
}

/** What the battlefield of an operation names: its kicker, the object to recover, the VIP and an enemy leader. */
export interface MissionInfo {
  kicker?: string;
  /** A sabotage's alarm (#14). */
  alarm?: Alarm;
  item?: string;
  /** Its story puts the item out in the open (playtest 2026-10-14, #27). */
  itemOutdoors?: boolean;
  vipName?: string;
  leader?: { template: TemplateId; name: string };
  /** Still winter (playtest 16/10): the mission under snow. */
  snow?: boolean;
}

/**
 * Story chapters won after which the winter is over and the missions lose
 * their snow (playtest 16/10): three, so it ends with the sirena of chapter 3;
 * the Marea comes with the thaw, and chapter 4 finds it out.
 */
export const THAW_AFTER_STORY = 3;

/** Whether it is still winter in the campaign: until the story says it is over. */
export function wintry(c: Pick<CampaignState, 'story'>): boolean {
  return c.story.done < THAW_AFTER_STORY;
}

export function missionInfo(c: CampaignState, offer: MissionOffer): MissionInfo {
  const info = ownInfo(c, offer);
  // An ordinary rescue's hostage and a sabotage's alarm, chosen with the offer (playtest 2026-10-15, #14, #20).
  return { ...info, ...(info.vipName || !offer.vip ? {} : { vipName: offer.vip }), ...(offer.alarm ? { alarm: offer.alarm } : {}), ...(wintry(c) ? { snow: true } : {}) };
}

/** What the story, a side quest or an ordinary Botín say of an operation. */
function ownInfo(c: CampaignState, offer: MissionOffer): MissionInfo {
  if (offer.story) {
    return { ...(offer.story === 1 ? {} : { kicker: `DIARIO · HOJA ${offer.story}` }), item: STORY[offer.story - 1]!.item, vipName: storyVip(offer.story, c.story.simon) };
  }
  const q = questOf(c, offer);
  const step = q && QUESTS[q.id].steps[q.step];
  // An ordinary Botín carries out what its report speaks of (docs/PRUEBA-2026-10-14.md #2).
  if (!q || step?.type !== 'mission') {
    const item = recoveryItem(offer);
    return item ? { item, ...(recoveryOutdoors(offer) ? { itemOutdoors: true } : {}) } : {};
  }
  return {
    kicker: `ENCARGO · ${QUESTS[q.id].title.toUpperCase()}`,
    item: step.item && fill(step.item, q.names),
    ...(step.outdoors ? { itemOutdoors: true } : {}),
    vipName: step.vip && fill(step.vip, q.names),
    leader: step.leader,
  };
}

/** Who waits at the objective of a rescue: story chapters and side quests have their own. */
export function offerVip(c: CampaignState, offer: MissionOffer): string | undefined {
  return missionInfo(c, offer).vipName;
}

/** Morse's report for an operation; side quests write their own. */
export function offerBriefing(c: CampaignState, offer: MissionOffer): string {
  const q = questOf(c, offer);
  const step = q && QUESTS[q.id].steps[q.step];
  if (q && step?.type === 'mission') return fill(step.briefing, q.names);
  return missionBriefing(offer, bastionLevel(c), c.story.simon);
}

/** Who signs an operation's report. */
export function offerSignature(c: CampaignState, offer: MissionOffer): string {
  const q = questOf(c, offer);
  return q ? QUESTS[q.id].signature : briefingSignature(offer);
}

// ----------------------------------------------------------- side quests

/** Side quests under way at once. */
const MAX_QUESTS = 3;
const MAX_RUMORS = 2;
/** Days a rumour waits for someone to listen. */
const RUMOR_LIFE = 10;

function questOf(c: CampaignState, offer: MissionOffer): QuestState | undefined {
  return offer.quest ? c.quests.find((q) => q.id === offer.quest) : undefined;
}

/** Not under way, and never finished or finished long enough ago to come back. */
export function questAvailable(c: CampaignState, id: QuestId): boolean {
  if (c.quests.some((q) => q.id === id)) return false;
  const done = c.questLog[id];
  if (!done) return true;
  const retry = QUESTS[id].outcomes[done.outcome]?.retry;
  return retry !== undefined && c.day >= done.day + retry;
}

/** How the finished side quests ended. */
function questOutcomes(c: CampaignState): { id: QuestId; outcome: QuestOutcome }[] {
  return (Object.keys(c.questLog) as QuestId[]).map((id) => ({ id, outcome: QUESTS[id].outcomes[c.questLog[id]!.outcome] ?? QUESTS[id].outcomes.failure }));
}

/** A region's monthly credits once contacted, with what side quests added or took away. */
export function regionIncome(c: CampaignState, id: RegionId): number {
  const extra = questOutcomes(c).filter((q) => QUESTS[q.id].region === id).reduce((sum, q) => sum + (q.outcome.income ?? 0), 0);
  return Math.max(0, REGIONS[id].income + extra);
}

/** Enemy that side quests left in every operation of a region. */
function regionEnemy(c: CampaignState, id: RegionId): TemplateId | undefined {
  return questOutcomes(c).find((q) => QUESTS[q.id].region === id && q.outcome.enemy)?.outcome.enemy;
}

/** The regional side quest a rumour in `region` can lead to. */
function rumorQuest(c: CampaignState, region: RegionId): QuestId | undefined {
  if (!c.regions.find((r) => r.id === region)?.contacted) return undefined;
  return (Object.keys(QUESTS) as QuestId[]).find((id) => QUESTS[id].rumor && QUESTS[id].region === region && questAvailable(c, id));
}

function startQuest(c: CampaignState, id: QuestId, rng: Rng, extra: Partial<QuestState> = {}): void {
  const def = QUESTS[id];
  const contacted = c.regions.filter((r) => r.contacted).map((r) => r.id);
  const q: QuestState = {
    id,
    step: 0,
    region: def.region ?? (contacted.length ? rng.pick(contacted) : HOME_REGION),
    owner: null,
    soldier: null,
    names: {},
    secret: rng.int(2),
    ...extra,
  };
  if (id === 'mole') {
    // Simão is a suspect if he was let in.
    const pool = [...SUSPECTS];
    const a = c.story.simon === 'trusted' ? 'Simão Ferreira' : pool.splice(rng.int(pool.length), 1)[0]!;
    const b = pool[rng.int(pool.length)]!;
    q.names = { a, b, culprit: q.secret === 0 ? a : b };
  }
  c.quests.push(q);
  delete c.questLog[id];
  log(c, fill(def.hook, q.names));
  enterStep(c, q, rng);
}

/** Puts the quest's current step in play: an operation on the geoscape or a decision. */
function enterStep(c: CampaignState, q: QuestState, rng: Rng): void {
  const def = QUESTS[q.id];
  const step = def.steps[q.step]!;
  if (step.type === 'event') {
    showQuestEvent(c);
    return;
  }
  const offer = makeQuestOffer(c, q, step, rng);
  c.offers.push(offer);
  const intro = q.step === 0 ? `${fill(def.hook, q.names)} ` : '';
  log(c, `${intro}${fill(step.briefing, q.names)}`, 'info', { kicker: `ENCARGO · ${def.title.toUpperCase()}`, title: offer.name });
}

function makeQuestOffer(c: CampaignState, q: QuestState, step: QuestMission, rng: Rng): MissionOffer {
  const difficulty = Math.max(1, Math.min(4, difficultyNow(c, rng) + (step.difficulty ?? 0)));
  // The living's, if the step says so or its named enemy is one of them (docs/ZOMBIS-PRIMERO.md).
  const living = !!step.living || (!!step.leader && !TEMPLATES[step.leader.template].undead);
  const pods = regionPods(c, q.region, difficulty, step.kind, living);
  // The named enemy needs a body of its kind on the field.
  if (step.leader && !pods.some((p) => p.includes(step.leader!.template))) pods[0] = [step.leader.template, ...pods[0]!];
  return byNight({
    id: newId(c, 'm'),
    name: fill(step.name, q.names),
    kind: step.kind,
    difficulty,
    expiresDay: c.day + step.days,
    reward: { ...step.reward },
    map: { kind: 'generated', biome: step.biome, seed: rng.int(2 ** 31) },
    pods,
    final: false,
    region: q.region,
    facility: null,
    story: null,
    quest: q.id,
    ...(living ? { living: true as const } : {}),
    ...(step.kind === 'sabotage' ? { alarm: 'police' as const } : {}),
  });
}

/** A quest waiting at a decision becomes the pending event when nothing else is. */
function showQuestEvent(c: CampaignState): void {
  if (c.event === 'quest' && !eventDef(c)) c.event = null;
  if (c.event) return;
  const q = c.quests.find((x) => QUESTS[x.id].steps[x.step]?.type === 'event');
  if (!q) return;
  c.event = 'quest';
  c.questEvent = q.id;
}

function advanceQuest(c: CampaignState, q: QuestState, rng: Rng): void {
  q.step++;
  if (q.step >= QUESTS[q.id].steps.length) finishQuest(c, q, 'success', rng);
  else enterStep(c, q, rng);
}

/** The players' answer to a quest's decision. */
function answerQuest(c: CampaignState, option: 0 | 1, rng: Rng): void {
  const q = c.quests.find((x) => x.id === c.questEvent);
  if (!q) return;
  const def = QUESTS[q.id];
  const choice = (def.steps[q.step] as QuestDecision).options[option];
  if (choice.clue && def.clues) {
    // What the watch showed opens the next decision.
    const name = option === 0 ? q.names.a! : q.names.b!;
    q.names.clue = fill(option === q.secret ? def.clues.guilty : def.clues.innocent, { ...q.names, name });
    log(c, q.names.clue);
  }
  if (choice.then === 'next') advanceQuest(c, q, rng);
  else if (choice.then === 'guess') finishQuest(c, q, option === q.secret ? 'success' : 'failure', rng);
  else finishQuest(c, q, choice.then, rng);
}

function finishQuest(c: CampaignState, q: QuestState, key: string, rng: Rng): void {
  const def = QUESTS[q.id];
  const outcome = def.outcomes[key] ?? def.outcomes.failure;
  c.quests = c.quests.filter((x) => x.id !== q.id);
  c.offers = c.offers.filter((o) => o.quest !== q.id || o.id === c.activeMission);
  if (c.questEvent === q.id) c.questEvent = null;
  c.questLog[q.id] = { outcome: key, day: c.day };
  log(c, fill(outcome.text, q.names), outcome.tone, { kicker: outcome.tone === 'good' ? 'ENCARGO CUMPLIDO' : 'ENCARGO CERRADO', title: def.title });

  if (outcome.credits) c.credits = Math.max(0, c.credits + outcome.credits);
  for (const [item, n] of Object.entries(outcome.items ?? {}) as [ItemId, number][]) c.inventory[item] = (c.inventory[item] ?? 0) + n;
  if (outcome.doom) c.doom = Math.max(0, c.doom + outcome.doom);
  if (outcome.tech && !c.techs.includes(outcome.tech)) c.techs.push(outcome.tech);
  if (outcome.delay) for (const w of c.base.works) w.daysLeft += outcome.delay;
  const soldier = c.soldiers.find((s) => s.id === q.soldier && s.alive);
  if (outcome.trait && soldier && !soldier.traits.includes(outcome.trait)) soldier.traits.push(outcome.trait);
  if (outcome.recruit) {
    const r = outcome.recruit;
    const s = addSoldier(c, q.owner ?? neediestPlayer(c), rng);
    if (r.name) {
      s.name = fill(r.name, q.names);
      s.appearance = defaultAppearance(s.owner, undefined, sexOfPerson(s.name));
    }
    if (r.rank) seasoned(s, r.rank, rng);
    if (r.trait) s.traits.push(r.trait);
    log(c, `${s.name} se une al Bastión (${veterancyName(s)}).`, 'good');
  }
  if (outcome.xp) gainXp(c, outcome.xp, rng);
  if (c.doom >= DOOM_MAX) c.outcome = 'defeat';
}

/** A newcomer who has been out there before: `steps` bars more, in a line at random each. */
function seasoned(s: CampaignSoldier, steps: number, rng: Rng): void {
  for (let k = 0; k < steps; k++) Object.assign(s, oneMoreBar(s.skills, s.growth, rng));
}

/** A soldier's veterancy, by name (Novato, Fogueado…). */
export function veterancyName(s: Pick<CampaignSoldier, 'board'> & { appearance?: Pick<Appearance, 'sex'> }): string {
  const def = VETERANCY[veterancyRank(s.board.length)]!;
  return s.appearance?.sex === 'f' ? def.female : def.name;
}

/**
 * How much more a mission teaches (skills.ts learn): 20 % for every level of
 * the Taller de oficios (docs/FASE9.md), Teo going over what was done out there.
 */
export function learningBonus(c: CampaignState): number {
  return LEARNING_BONUS * facilityLevel(c, 'echoes');
}

/** Of what a mission teaches, this much more for each level of the Taller de oficios, and with the Camposanto at 3 after losses. */
const LEARNING_BONUS = 0.2;

/**
 * Whether a soldier has a point it can spend now on the board (docs/FASE10.md
 * §6): a cell beside its own whose line's bars allow it.
 */
export function canSpendPoints(_c: CampaignState, s: CampaignSoldier): boolean {
  return Object.keys(BOARD_CELLS).some((id) => unlockBlocker(s.skills, s.board, id) === null);
}

/** Side quests with their own trigger; at most one starts at a time. */
function refreshQuests(c: CampaignState, rng: Rng): void {
  if (c.outcome) return;
  showQuestEvent(c);
  if (c.quests.length >= MAX_QUESTS) return;
  const level = bastionLevel(c);
  const contacted = (id: RegionId) => !!c.regions.find((r) => r.id === id)?.contacted;
  const due: [QuestId, boolean][] = [
    ['mole', (c.seen.sabotage ?? 0) >= 2],
    ['ibarra', level >= 3 && c.story.done >= 1],
    ['amara', c.story.done >= NWOSU_CHAPTER && contacted('afr')],
    ['numbers', level >= 2 && c.day >= 20],
  ];
  const next = due.find(([id, ready]) => ready && questAvailable(c, id));
  if (next) startQuest(c, next[0], rng);
}

/** After a mission, a soldier with a nickname may hear of someone among the Cuervos' prisoners. */
function maybeFaces(c: CampaignState, report: MissionReport, rng: Rng): void {
  if (c.outcome || c.quests.length >= MAX_QUESTS || bastionLevel(c) < 2 || !questAvailable(c, 'faces') || rng.int(4) !== 0) return;
  const seen = report.squad.map((e) => c.soldiers.find((s) => s.id === e.id)).filter((s): s is CampaignSoldier => !!s && s.alive && !!s.nickname);
  if (!seen.length) return;
  const s = rng.pick(seen);
  // Never someone with the soldier's own first name (an old save's, or one the players chose).
  const first = s.name.split(' ')[0];
  const relative = rng.pick(RELATIVES.filter((r) => r.name !== first));
  const surname = s.name.split(' ').slice(1).join(' ') || s.name;
  startQuest(c, 'faces', rng, {
    owner: s.owner,
    soldier: s.id,
    names: { soldier: displayName(s.name, s.nickname), relative: `${relative.name} ${surname}`, relation: relative.relation },
  });
}

// ----------------------------------------------------------------- rumours

function newRumor(c: CampaignState, rng: Rng): void {
  const all = (Object.keys(REGIONS) as RegionId[]).filter((id) => !c.rumors.some((r) => r.region === id));
  const contacted = all.filter((id) => c.regions.find((r) => r.id === id)?.contacted);
  const withQuest = contacted.filter((id) => rumorQuest(c, id));
  // Rumours lean towards regions with something waiting, and towards the network.
  const pool = withQuest.length && rng.int(2) === 0 ? withQuest : contacted.length && rng.int(10) < 6 ? contacted : all;
  if (!pool.length) return;
  const region = rng.pick(pool);
  const rumor: Rumor = { id: newId(c, 'r'), region, text: rng.pick(RUMOR_LINES), expiresDay: c.day + RUMOR_LIFE, daysLeft: 3 + rng.int(3) };
  c.rumors.push(rumor);
  log(c, `Se habla de ${rumor.text}. Si lo escuchamos unos días, sabremos qué hay de cierto.`, 'info', { kicker: 'RUMOR', title: REGIONS[region].name, rumor: rumor.id });
}

/** What a rumour turns out to be once the Bastion has listened to it. */
function resolveRumor(c: CampaignState, rumor: Rumor, rng: Rng): void {
  c.rumors = c.rumors.filter((r) => r.id !== rumor.id);
  c.listening = null;
  const name = REGIONS[rumor.region].name;
  const alert = { kicker: 'RUMOR', title: name };
  const quest = rumorQuest(c, rumor.region);
  if (quest && c.quests.length < MAX_QUESTS) {
    // The quest's own decision or operation tells the rest.
    log(c, `El rumor ${ofZone(name)} era cierto: ${QUESTS[quest].title}.`, 'good');
    startQuest(c, quest, rng);
    return;
  }
  const roll = rng.int(100);
  if (roll < 40) {
    const offer = makeOffer(c, rng, false, rumor.region);
    offer.reward.credits = (offer.reward.credits ?? 0) + 25;
    c.offers.push(offer);
    log(c, `El rumor ${ofZone(name)} era cierto: ${offer.name}, con mejor paga.`, 'good', alert);
  } else if (roll < 60) {
    const credits = 30 + rng.int(4) * 10;
    c.credits += credits;
    log(c, `El rumor ${ofZone(name)} era un alijo de otros supervivientes: +${credits} suministros.`, 'good', alert);
  } else if (roll < 80) {
    log(c, `El rumor ${ofZone(name)} era un Saqueador que hablaba demasiado. Lo que sabía vale experiencia.`, 'good', alert);
    gainXp(c, 6 + rng.int(5), rng);
  } else {
    log(c, `El rumor ${ofZone(name)} era un bulo.`, 'info', alert);
  }
}

// ------------------------------------------------------------------- create

/** The bunker and its three buildings, cleared ground around them and the wild beyond. */
function freshBase(): CampaignState['base'] {
  const plots: BasePlot[] = [];
  for (let y = 0; y < BASE_SIZE; y++) {
    for (let x = 0; x < BASE_SIZE; x++) {
      const fixed = (Object.keys(FIXED_BUILDINGS) as FixedBuildingId[]).find((id) => FIXED_BUILDINGS[id].x === x && FIXED_BUILDINGS[id].y === y);
      plots.push({ id: `${x},${y}`, x, y, terrain: terrainAt(x, y), building: fixed ?? null });
    }
  }
  return { plots, works: [] };
}

function freshRegions(): RegionState[] {
  return (Object.keys(REGIONS) as RegionId[]).map((id) => ({ id, contacted: id === HOME_REGION, facility: null }));
}

/** The base's first line, and the same once the opening operation is chosen (placeholders for Lore). */
const WELCOME = 'El Bastión entra en servicio. Elegid la primera misión: el combate hará subir de nivel al Bastión.';
const WELCOME_OPENING = 'Primera salida: las medicinas del consultorio de Brena. La escuadra espera en la cochera.';

/**
 * A new campaign opens with a mission (playtest 2026-10-13): the first story
 * operation is chosen and its squad picked, waiting in the hangar; the base
 * and its needs come after. The server calls it on a campaign it has just
 * created (createCampaign itself leaves the choice open).
 */
export function openingMission(c: CampaignState): void {
  const first = c.offers.find((o) => o.story === 1) ?? c.offers[0];
  if (!first || c.activeMission) return;
  c.activeMission = first.id;
  c.squad = defaultSquad(c, c.players);
  c.launchReady = [false, false];
  syncPads(c);
  const welcome = c.log.find((l) => l.text === WELCOME);
  if (welcome) welcome.text = WELCOME_OPENING;
}

export function createCampaign(players: Slot[], rng: Rng): CampaignState {
  const c: CampaignState = {
    version: CAMPAIGN_VERSION,
    day: 1,
    players: [...players],
    credits: 200,
    xp: 0,
    soldiers: [],
    inventory: { fragGrenade: 2, firstAid: 1 },
    medicines: START_MEDICINES,
    nextMedicineDay: 1 + ENCLAVE_DAYS,
    treatment: null,
    food: START_FOOD,
    hungry: false,
    nextFoodDay: 1 + LA_VEGA_FOOD_DAYS,
    parts: 0,
    crafted: [],
    foundWeapons: [],
    flooded: [],
    base: freshBase(),
    regions: freshRegions(),
    offers: [],
    activeMission: null,
    squad: [],
    doom: 2,
    nextMissionDay: 6,
    nextDoomDay: 1 + DOOM_INTERVAL,
    nextFacilityDay: 9,
    nextSupplyDay: 1 + MONTH,
    nextEventDay: 5,
    event: null,
    advanceReady: [false, false],
    launchReady: [false, false],
    proposal: null,
    log: [],
    lastReport: null,
    outcome: null,
    ending: null,
    story: { done: 0, simon: null },
    quests: [],
    questLog: {},
    questEvent: null,
    rumors: [],
    listening: null,
    nextRumorDay: 4,
    techs: [],
    seen: {},
    nextId: 0,
  };
  // Everyone starts as a recruit: classes come at Capitán.
  for (let i = 0; i < 2 * START_KIT; i++) addSoldier(c, players.length > 1 ? players[i % 2]! : players[0]!, rng);
  c.offers.push(makeOffer(c, rng), makeOffer(c, rng));
  log(c, WELCOME, 'good');
  refreshNarrative(c, rng);
  syncMarea(c);
  c.uid = rng.int(2 ** 31).toString(36);
  return c;
}

// ----------------------------------------------------------------- commands

export type CampaignCommand =
  | { type: 'chooseMission'; id: string }
  | { type: 'clear'; plot: string }
  | { type: 'build'; plot: string; facility: FacilityId }
  | { type: 'upgrade'; plot: string }
  | { type: 'contact'; region: RegionId }
  | { type: 'answerEvent'; option: 0 | 1 }
  | { type: 'investigate'; id: string }
  | { type: 'cancelMission' }
  | { type: 'squad'; soldier: string; on: boolean }
  /** `squad`: straight into the squad of the chosen mission (a free place in the hangar). */
  /** Recruits come without a class: they specialise with their first talent points. */
  | { type: 'recruit'; squad?: boolean }
  /** Fase 10: a skill of the hex board bought with a point (skills.ts unlockBlocker). */
  | { type: 'unlock'; soldier: string; cell: CellId }
  /** Fase 9 (docs/FASE9.md §5): mend a weapon with parts, as far as they go; mount or take off an improvement. */
  | { type: 'repair'; weapon: string }
  | { type: 'mod'; weapon: string; mod: WeaponModId; on: boolean }
  | { type: 'buy'; item: ItemId }
  | { type: 'equip'; soldier: string; slot: ItemSlot; item: ItemId | null }
  | { type: 'customize'; soldier: string; name: string; nickname: string; appearance: Appearance }
  | { type: 'advance'; ready: boolean }
  | { type: 'launch'; ready: boolean }
  /** Treat a bitten soldier: two keys, like launching (docs/FASE3.md). */
  | { type: 'treat'; soldier: string; ready: boolean }
  /** 1 ✚ for 3 days fewer of a wound. */
  | { type: 'medicate'; soldier: string }
  /** The Taller makes a tier of weapons or armour (docs/FASE4.md): a shared decision, like building. */
  | { type: 'craft'; project: CraftId }
  /** A found weapon from the Armería to one of your soldiers (null: back on the rack). */
  | { type: 'assignWeapon'; weapon: string; soldier: string | null }
  | { type: 'vote'; accept: boolean }
  | { type: 'withdraw' };

export interface CampaignContext {
  slot: Slot;
  connected: readonly Slot[];
  rng: Rng;
}

export type CampaignResult = { ok: true; launch?: boolean } | { ok: false; error: string };

/** Max soldiers each player may bring: half the squad (one more with the training centre), all of it alone. */
export function squadLimit(c: CampaignState, connected: readonly Slot[]): number {
  const bonus = facilityCount(c, 'training') > 0 ? 1 : 0;
  return c.players.length > 1 && connected.length > 1 ? 3 + bonus : 6 + 2 * bonus;
}

export function squadMax(c: CampaignState): number {
  return facilityCount(c, 'training') > 0 ? 8 : 6;
}

function squadCount(c: CampaignState, slot: Slot): number {
  return c.squad.filter((id) => c.soldiers.find((s) => s.id === id)?.owner === slot).length;
}

/**
 * Whether `slot` can put one more soldier in the squad (UX-REVISION-2 #39).
 * Each player has a share; a share the partner can't fill (no ready soldier
 * left out) can be taken by the other. A place lent that way comes back when
 * its owner wants it: `evict` is the soldier that leaves to make room.
 */
export function squadPlace(c: CampaignState, slot: Slot, connected: readonly Slot[]): { error: string | null; evict?: string } {
  const limit = squadLimit(c, connected);
  const mine = squadCount(c, slot);
  if (c.squad.length >= squadMax(c)) {
    const lent = mine < limit ? [...c.squad].reverse().find((id) => {
      const owner = c.soldiers.find((s) => s.id === id)?.owner;
      return owner !== undefined && owner !== slot && squadCount(c, owner) > limit;
    }) : undefined;
    return lent ? { error: null, evict: lent } : { error: 'La escuadra está completa.' };
  }
  if (mine < limit) return { error: null };
  const partner = c.players.find((p) => p !== slot);
  const spare =
    partner !== undefined &&
    connected.includes(partner) &&
    squadCount(c, partner) < limit &&
    c.soldiers.some((s) => s.owner === partner && soldierReady(s) && !c.squad.includes(s.id));
  return spare ? { error: `Máximo ${limit} soldados por jugador: tu compañero aún puede completar su parte.` } : { error: null };
}

/** The more veterancy (and then missions) first. */
const seasonedFirst = (a: CampaignSoldier, b: CampaignSoldier) => b.board.length - a.board.length || totalBars(b.skills) - totalBars(a.skills) || b.missions - a.missions;

function joinSquad(c: CampaignState, soldier: CampaignSoldier, place: { evict?: string }): void {
  if (place.evict) {
    const out = c.soldiers.find((s) => s.id === place.evict)!;
    c.squad = c.squad.filter((id) => id !== place.evict);
    log(c, `${out.name} deja su hueco a ${soldier.name}.`);
  }
  c.squad.push(soldier.id);
}

/**
 * The squad a mission starts with: each connected player's ready soldiers,
 * the most seasoned first (docs/FASE9.md: veterancy, then missions flown).
 */
export function defaultSquad(c: CampaignState, connected: readonly Slot[]): string[] {
  const limit = squadLimit(c, connected);
  const squad: string[] = [];
  for (const slot of c.players.filter((p) => connected.includes(p))) {
    const ready = c.soldiers.filter((s) => s.owner === slot && soldierReady(s)).sort(seasonedFirst);
    squad.push(...ready.slice(0, limit).map((s) => s.id));
  }
  // A share left short (wounded, fallen) is filled with the other player's best soldiers left out.
  const spare = c.soldiers
    .filter((s) => connected.includes(s.owner) && c.players.includes(s.owner) && soldierReady(s) && !squad.includes(s.id))
    .sort(seasonedFirst);
  for (const s of spare) if (squad.length < squadMax(c)) squad.push(s.id);
  return squad.slice(0, squadMax(c));
}

/** Soldiers a partner who joins a campaign started without them gets to command. */
export const LATE_KIT = 4;
/** Soldiers each player starts with. */
const START_KIT = 4;
/** Ready soldiers a commander keeps when a partner arrives: a squad share. */
const KEEP_READY = 3;

/**
 * Brings a player into a campaign started without them. They take over the
 * other commander's untouched rookies (never deployed, so nothing is lost), and
 * fresh recruits make up the rest of a kit. Returns how many soldiers they got.
 */
export function addPlayer(c: CampaignState, slot: Slot, rng: Rng): number {
  if (c.players.includes(slot)) return 0;
  c.players = [...c.players, slot].sort() as Slot[];
  // Untouched rookies change hands, but never the reserves the commander already there
  // needs: they keep at least a squad share of ready soldiers (UX-REVISION-2 #37).
  // Whatever is missing comes as fresh recruits.
  const ready = (owner: Slot) => c.soldiers.filter((s) => s.owner === owner && soldierReady(s)).length;
  const rookies: CampaignSoldier[] = [];
  for (const s of c.soldiers.filter((x) => x.owner !== slot && x.alive && x.missions === 0).reverse()) {
    if (rookies.length >= LATE_KIT) break;
    if (soldierReady(s) && ready(s.owner) - rookies.filter((r) => r.owner === s.owner && soldierReady(r)).length <= KEEP_READY) continue;
    rookies.push(s);
  }
  // A squad being picked is redone for both by whoever calls this (see the server's bringPlayersIn).
  c.squad = c.squad.filter((id) => !rookies.some((s) => s.id === id));
  for (const s of rookies) {
    const sex = s.appearance.sex;
    const wasDefault = JSON.stringify(s.appearance) === JSON.stringify(defaultAppearance(s.owner, undefined, sex));
    s.owner = slot;
    if (wasDefault) s.appearance = defaultAppearance(slot, undefined, sex);
    s.loadout = { utility: null, ammo: null };
  }
  const recruits: CampaignSoldier[] = [];
  for (let i = rookies.length; i < LATE_KIT; i++) recruits.push(addSoldier(c, slot, rng));
  // Whoever was already there learns who changed hands, and who joined.
  const list = (names: string[]) => (names.length > 1 ? `${names.slice(0, -1).join(', ')} y ${names[names.length - 1]}` : (names[0] ?? ''));
  const moved = rookies.length ? `Pasan a su mando: ${list(rookies.map((s) => s.name))}.` : '';
  const joined = recruits.length ? `${rookies.length ? 'Además, se alistan' : 'Se alistan a sus órdenes'}: ${list(recruits.map((s) => s.name))}.` : '';
  log(c, ['Llega el segundo comandante.', moved, joined].filter(Boolean).join(' '), 'good', { kicker: 'RELEVO', title: 'Llega el segundo comandante' });
  return LATE_KIT;
}

// ------------------------------------------------------------- next steps

/** Something a player has pending in the base, for the "Ahora" block and the nav counters. */
export interface NextStep {
  view: BaseView;
  label: string;
  count?: number;
  tone: 'urgent' | 'info';
  go?: { offer?: string; soldier?: string; plot?: string; region?: RegionId; rumor?: string };
}

/**
 * What `slot` has pending, most important first: the first item is "what to do
 * now". Shared matters (operations, building, rumours) and personal ones
 * (promotions, equipment) mixed by urgency.
 */
export function nextSteps(c: CampaignState, slot: Slot): NextStep[] {
  if (c.outcome) return [];
  const steps: NextStep[] = [];
  const partner: Slot = slot === 0 ? 1 : 0;
  const shared = c.players.length > 1;

  if (c.proposal && c.proposal.by !== slot) steps.push({ view: 'hub', label: `Tu compañero propone ${c.proposal.text}`, tone: 'urgent' });
  const event = eventDef(c);
  if (event) steps.push({ view: 'hub', label: `Decidid: ${event.title}`, tone: 'urgent' });

  const mission = activeOffer(c);
  const mine = c.soldiers.filter((s) => s.owner === slot && s.alive);

  // Short of soldiers for a full share of the squad (after a bad mission): recruiting comes
  // before choosing an operation, or the squad flies half empty.
  const ready = mine.filter(soldierReady).length;
  // With no bed free, the answer is a barracón first.
  const fewReady = `${ready === 0 ? 'Ningún soldado listo' : `Solo ${ready} ${ready === 1 ? 'soldado listo' : 'soldados listos'}`}`;
  const needed = ready < squadLimit(c, c.players) && mine.length < soldierCap(c);
  const short =
    needed && bedsFree(c) > 0 && canAfford(c, recruitCost(c))
      ? { view: 'barracks' as const, label: `${fewReady}: recluta (${recruitCost(c)} §)`, tone: 'info' as const }
      : needed && bedsFree(c) <= 0 && c.base.works.length < workCrews(c) && canAfford(c, FACILITIES.quarters.cost)
        ? { view: 'facilities' as const, label: `${fewReady} y sin camas: construye un barracón`, tone: 'info' as const }
        : null;
  if (short && !mission) steps.push(short);

  if (mission) {
    const partnerReady = shared && c.launchReady[partner] && !c.launchReady[slot];
    steps.push({
      view: 'squad',
      label: partnerReady ? `Tu compañero está listo para despegar: ${mission.name}` : `Revisa la escuadra y despega: ${mission.name}`,
      tone: partnerReady ? 'urgent' : 'info',
      go: { offer: mission.id },
    });
    if (short) steps.push(short);
  } else if (c.offers.length) {
    // Expiring soon first; then story chapters, which move the campaign on.
    const left = (o: MissionOffer) => (o.expiresDay === null ? Infinity : o.expiresDay - c.day);
    const first = [...c.offers].sort((a, b) => left(a) - left(b) || Number(!!b.story) - Number(!!a.story))[0]!;
    const soon = left(first) <= 2;
    steps.push({
      view: 'geoscape',
      label: soon ? `${first.name} caduca ${left(first) <= 0 ? 'hoy' : `en ${left(first)} ${left(first) === 1 ? 'día' : 'días'}`}` : 'Elige una operación',
      count: c.offers.length,
      tone: soon ? 'urgent' : 'info',
      go: { offer: first.id },
    });
  }

  const promotions = mine.filter((s) => s.alive && canSpendPoints(c, s));
  if (promotions.length) {
    steps.push({ view: 'barracks', label: promotions.length === 1 ? `Puntos por gastar: ${promotions[0]!.name}` : 'Puntos por gastar', count: promotions.length, tone: 'info', go: { soldier: promotions[0]!.id } });
  }

  if (!mission && shared && c.advanceReady[partner] && !c.advanceReady[slot]) steps.push({ view: 'geoscape', label: 'Tu compañero quiere esperar noticias', tone: 'urgent' });

  // The Marea (docs/FASE5.md): the zones it covers send nothing and their missions are harder.
  const flooded = floodedZones(c);
  if (flooded.length) steps.push({ view: 'geoscape', label: floodedListLabel(bastionLevel(c), flooded.map((id) => REGIONS[id].name)), tone: 'info' });

  // Food (docs/FASE4.md): hunger, or the pantry running low.
  const days = Math.floor((c.food ?? 0) / Math.max(1, dailyRations(c)));
  if (c.hungry) steps.push({ view: 'facilities', label: 'Pasáis hambre: −10 de voluntad y heridas lentas. Buscad raciones o un Huerto', tone: 'urgent' });
  else if (days < 3) steps.push({ view: 'facilities', label: `Quedan raciones para ${days} ${days === 1 ? 'día' : 'días'}`, tone: 'info' });
  // The Taller has something within reach to make.
  const craft = (Object.keys(CRAFTS) as CraftId[]).find((id) => !craftBlocker(c, id));
  if (craft) steps.push({ view: 'engineering', label: `El Taller puede hacer ${CRAFTS[craft].the}`, tone: 'info' });

  // The bitten (docs/FASE3.md): the partner's treatment to confirm, and every fever with its deadline.
  const treating = c.treatment && c.soldiers.find((s) => s.id === c.treatment!.soldier);
  if (treating && shared && c.treatment!.ready[partner] && !c.treatment!.ready[slot]) {
    steps.push({ view: 'barracks', label: `Tu compañero quiere tratar a ${treating.name} (${treatCost(c)} ✚)`, tone: 'urgent', go: { soldier: treating.id } });
  }
  for (const s of c.soldiers.filter((x) => x.alive && x.infection).sort((a, b) => turnDay(c, a)! - turnDay(c, b)!)) {
    const left = turnDay(c, s)! - c.day;
    steps.push({ view: 'barracks', label: `${s.name} se convierte ${left <= 1 ? 'mañana' : `en ${left} días`}`, tone: 'urgent', go: { soldier: s.id } });
  }

  if (c.listening === null && c.rumors.length) {
    const rumor = [...c.rumors].sort((a, b) => a.expiresDay - b.expiresDay)[0]!;
    steps.push({ view: 'geoscape', label: 'Escucha un rumor', count: c.rumors.length, tone: 'info', go: { region: rumor.region, rumor: rumor.id } });
  }

  // Before a mission: accessories on the shelf while someone in your squad has an empty slot.
  if (mission) {
    const free = (k: ItemSlot) => Object.values(ITEMS).filter((i) => i.slot === k).reduce((n, i) => n + Math.max(0, itemsFree(c, i.id)), 0);
    const going = mine.filter((s) => c.squad.includes(s.id));
    const fillable = (['utility', 'ammo'] as const).reduce((n, k) => n + Math.min(free(k), going.filter((s) => !s.loadout[k]).length), 0);
    const first = going.find((s) => (['utility', 'ammo'] as const).some((k) => !s.loadout[k] && free(k) > 0));
    if (fillable && first) steps.push({ view: 'barracks', label: 'Equipa a tu escuadra con lo del almacén', count: fillable, tone: 'info', go: { soldier: first.id } });
  }

  if (c.base.works.length < workCrews(c)) {
    // A crew is free: something affordable to build on a free plot, else ground to clear next to the compound.
    const options = c.base.plots.flatMap((p) => Object.values(FACILITIES).filter((f) => !buildable(c, p, f.id) && canAfford(c, f.cost)).map((f) => ({ p, f })));
    const best = options.find((o) => adjacencyAt(c, o.p, o.f.id)) ?? options[0];
    const wild = c.base.plots
      .filter((p) => !clearable(c, p) && canAfford(c, clearCost(p).cost))
      .sort((a, b) => plotRing(a) - plotRing(b))[0];
    // A level up for what is already built, when it can be paid.
    const raise = c.base.plots.find((p) => !upgradable(c, p) && canAfford(c, upgradeCost(p.building as FacilityId, (p.level ?? 1) + 1).cost));
    if (best) steps.push({ view: 'facilities', label: 'Construye junto a la base', tone: 'info', go: { plot: best.p.id } });
    else if (!c.base.plots.some((p) => p.terrain === 'clear' && !p.building) && wild) {
      steps.push({ view: 'facilities', label: 'Despeja terreno para crecer', tone: 'info', go: { plot: wild.id } });
    } else if (raise) {
      steps.push({ view: 'facilities', label: `Mejora ${FACILITIES[raise.building as FacilityId].name}`, tone: 'info', go: { plot: raise.id } });
    }
  }

  if (c.regions.filter((r) => r.contacted).length < contactCapacity(c) && canAfford(c, contactCost(c))) steps.push({ view: 'geoscape', label: 'Contacta una zona', tone: 'info' });

  if (!mission && !c.offers.length) steps.push({ view: 'geoscape', label: 'Espera noticias hasta que pase algo', tone: 'info' });

  // Urgent first, keeping the order above within each tone.
  return [...steps.filter((x) => x.tone === 'urgent'), ...steps.filter((x) => x.tone === 'info')];
}


function proposalText(c: CampaignState, action: ProposalAction): string {
  switch (action.type) {
    case 'chooseMission':
      return `ir a la misión ${c.offers.find((o) => o.id === action.id)?.name ?? ''}`;
    case 'clear': {
      const plot = plotById(c, action.plot)!;
      return `despejar ${plotName(plot)}${plot.terrain === 'clear' ? '' : ` (${TERRAINS[plot.terrain].name.toLowerCase()})`}`;
    }
    case 'build':
      return `construir ${FACILITIES[action.facility].name} en ${plotName(plotById(c, action.plot)!)}`;
    case 'upgrade': {
      const plot = plotById(c, action.plot)!;
      return `mejorar ${FACILITIES[plot.building as FacilityId].name} a nivel ${(plot.level ?? 1) + 1}`;
    }
    case 'contact':
      return `contactar con ${REGIONS[action.region].name}`;
    case 'event':
      return `responder «${eventDef(c)?.options[action.option].label ?? ''}»`;
    case 'investigate': {
      const rumor = c.rumors.find((r) => r.id === action.id);
      return `escuchar el rumor ${rumor ? ofZone(REGIONS[rumor.region].name) : 'de …'}`;
    }
    case 'craft':
      return `hacer en el Taller: ${CRAFTS[action.project].name.toLowerCase()}`;
  }
}

function validateAction(c: CampaignState, action: ProposalAction): string | null {
  switch (action.type) {
    case 'chooseMission':
      // Another operation may take the place of the one chosen, until it takes off.
      if (c.activeMission === action.id) return 'Esa misión ya está elegida.';
      if (!c.offers.some((o) => o.id === action.id)) return 'Esa misión ya no está disponible.';
      return null;
    case 'clear': {
      const plot = plotById(c, action.plot);
      if (!plot) return 'Esa parcela no existe.';
      const busy = worksBlocker(c, plot);
      if (busy) return busy;
      const blocker = clearable(c, plot);
      if (blocker) return blocker;
      if (!canAfford(c, clearCost(plot).cost)) return 'No hay suministros suficientes.';
      return null;
    }
    case 'build': {
      const plot = plotById(c, action.plot);
      if (!plot) return 'Esa parcela no existe.';
      const busy = worksBlocker(c, plot);
      if (busy) return busy;
      const blocker = buildable(c, plot, action.facility);
      if (blocker) return blocker;
      if (!canAfford(c, FACILITIES[action.facility].cost)) return 'No hay suministros suficientes.';
      return null;
    }
    case 'upgrade': {
      const plot = plotById(c, action.plot);
      if (!plot) return 'Esa parcela no existe.';
      const busy = worksBlocker(c, plot);
      if (busy) return busy;
      const blocker = upgradable(c, plot);
      if (blocker) return blocker;
      if (!canAfford(c, upgradeCost(plot.building as FacilityId, (plot.level ?? 1) + 1).cost)) return 'No hay suministros suficientes.';
      return null;
    }
    case 'contact':
      return contactBlocker(c, action.region);
    case 'event':
      if (!eventDef(c)) return 'No hay ningún acontecimiento pendiente.';
      if (!eventOptionAvailable(c, action.option)) return 'No podemos permitírnoslo.';
      return null;
    case 'investigate':
      if (!c.rumors.some((r) => r.id === action.id)) return 'Ese rumor ya se ha enfriado.';
      if (c.listening === action.id) return 'Ya estamos escuchando ese rumor.';
      return null;
    case 'craft':
      return craftBlocker(c, action.project);
  }
}

function applyAction(c: CampaignState, action: ProposalAction, ctx: CampaignContext): void {
  const { rng } = ctx;
  switch (action.type) {
    case 'chooseMission': {
      c.activeMission = action.id;
      // The squad comes ready, like in XCOM: players only swap who they want.
      c.squad = defaultSquad(c, ctx.connected);
      c.launchReady = [false, false];
      log(c, `Misión elegida: ${c.offers.find((o) => o.id === action.id)!.name}. Revisad la escuadra.`);
      break;
    }
    case 'clear': {
      const plot = plotById(c, action.plot)!;
      const { cost } = clearCost(plot);
      const days = workDays(c, clearCost(plot).days);
      pay(c, cost);
      c.base.works.push({ plot: plot.id, kind: 'clear', facility: null, daysLeft: days, total: days });
      if (plot.terrain !== 'clear') log(c, TERRAINS[plot.terrain].start.replace('{parcela}', plotName(plot)));
      break;
    }
    case 'build': {
      const plot = plotById(c, action.plot)!;
      const def = FACILITIES[action.facility];
      const days = workDays(c, def.days);
      pay(c, def.cost);
      c.base.works.push({ plot: plot.id, kind: 'build', facility: def.id, daysLeft: days, total: days });
      log(c, `Empiezan las obras: ${def.name}, en ${plotName(plot)}.`);
      break;
    }
    case 'upgrade': {
      const plot = plotById(c, action.plot)!;
      const id = plot.building as FacilityId;
      const next = upgradeCost(id, (plot.level ?? 1) + 1);
      const days = workDays(c, next.days);
      pay(c, next.cost);
      c.base.works.push({ plot: plot.id, kind: 'upgrade', facility: id, daysLeft: days, total: days });
      log(c, `Empiezan las obras: ${FACILITIES[id].name} a nivel ${(plot.level ?? 1) + 1}.`);
      break;
    }
    case 'contact': {
      pay(c, contactCost(c));
      const region = c.regions.find((r) => r.id === action.region)!;
      region.contacted = true;
      log(c, `Contacto establecido con ${REGIONS[region.id].name}.`, 'good');
      if (region.facility && !c.offers.some((o) => o.facility === region.id)) {
        c.offers.push(makeFacilityOffer(c, region.id, rng));
        log(c, `Ya podemos quemar el Nido ${ofZone(REGIONS[region.id].name)}.`, 'good');
      }
      break;
    }
    case 'event': {
      const def = eventDef(c)!;
      const option = def.options[action.option];
      option.apply(c, rng);
      log(c, `${def.title}: ${option.label[0]!.toLowerCase()}${option.label.slice(1)}.`);
      // A side quest may have moved on to its next decision: refreshNarrative shows it.
      c.event = null;
      c.questEvent = null;
      if (c.doom >= DOOM_MAX) c.outcome = 'defeat';
      refreshNarrative(c, rng);
      break;
    }
    case 'investigate': {
      const rumor = c.rumors.find((r) => r.id === action.id)!;
      c.listening = rumor.id;
      log(c, `Escuchamos el rumor ${ofZone(REGIONS[rumor.region].name)}: faltan ${rumor.daysLeft} días de escucha.`);
      break;
    }
    case 'craft': {
      // A work like any other: a crew at the Taller, paid in suministros and parts.
      const def = CRAFTS[action.project];
      const plot = workshopPlot(c)!;
      const days = workDays(c, def.days);
      pay(c, def.cost);
      c.parts = (c.parts ?? 0) - def.parts;
      c.base.works.push({ plot: plot.id, kind: 'craft', facility: 'workshop', daysLeft: days, total: days, project: def.id });
      log(c, `El Taller se pone con ${def.the}: ${days} días.`);
      break;
    }
  }
}

/**
 * The suministros a shared decision costs, paid only when the partner accepts
 * (applyAction): what the counter shows as committed meanwhile (playtest 2026-10-16).
 */
export function proposalCost(c: CampaignState, action: ProposalAction): number {
  switch (action.type) {
    case 'clear': {
      const plot = plotById(c, action.plot);
      return plot ? clearCost(plot).cost : 0;
    }
    case 'build':
      return FACILITIES[action.facility].cost;
    case 'upgrade': {
      const plot = plotById(c, action.plot);
      return plot?.building ? upgradeCost(plot.building as FacilityId, (plot.level ?? 1) + 1).cost : 0;
    }
    case 'contact':
      return contactCost(c);
    case 'craft':
      return CRAFTS[action.project].cost;
    default:
      return 0;
  }
}

/** Shared decisions: applied directly when alone, otherwise proposed to the partner. */
function propose(c: CampaignState, action: ProposalAction, ctx: CampaignContext): CampaignResult {
  const error = validateAction(c, action);
  if (error) return { ok: false, error };
  const partnerHere = ctx.connected.some((s) => s !== ctx.slot) && c.players.length > 1;
  if (!partnerHere) {
    applyAction(c, action, ctx);
    return { ok: true };
  }
  if (c.proposal) return { ok: false, error: 'Ya hay una propuesta pendiente.' };
  c.proposal = { by: ctx.slot, action, text: proposalText(c, action) };
  return { ok: true };
}

function everyoneReady(ready: [boolean, boolean], connected: readonly Slot[]): boolean {
  return connected.length > 0 && connected.every((s) => ready[s]);
}

export function executeCampaignCommand(c: CampaignState, cmd: CampaignCommand, ctx: CampaignContext): CampaignResult {
  const result = runCampaignCommand(c, cmd, ctx);
  if (result.ok) {
    syncPads(c);
    // An answer to an event, a Nido burnt: the activity may have moved the Marea.
    syncMarea(c);
  }
  return result;
}

/**
 * The hangar pads of the squad, `squadMax` long. Each commander has a half of
 * their own (the first player the left one) and keeps each soldier on the pad
 * it took; a place lent by the partner is taken from the middle of their half.
 * Pure: both commanders work out the same pads from the same campaign.
 */
export function squadPads(c: CampaignState): (string | null)[] {
  const max = squadMax(c);
  const out: (string | null)[] = Array.from({ length: max }, () => null);
  const kept = c.pads ?? {};
  for (const id of c.squad) {
    const pad = kept[id];
    if (pad !== undefined && pad < max && out[pad] === null) out[pad] = id;
  }
  const half = Math.ceil(max / 2);
  const all = Array.from({ length: max }, (_, i) => i);
  const left = all.slice(0, half);
  const right = all.slice(half);
  for (const id of c.squad) {
    if (out.includes(id)) continue;
    const owner = c.soldiers.find((s) => s.id === id)?.owner;
    const order = c.players.length < 2 ? all : owner === c.players[0] ? [...left, ...right] : [...right, ...[...left].reverse()];
    const pad = order.find((i) => out[i] === null);
    if (pad !== undefined) out[pad] = id;
  }
  return out;
}

/** Writes down the pads the squad stands on now, so they stay put. */
export function syncPads(c: CampaignState): void {
  const pads: Record<string, number> = {};
  squadPads(c).forEach((id, i) => {
    if (id) pads[id] = i;
  });
  c.pads = pads;
}

function runCampaignCommand(c: CampaignState, cmd: CampaignCommand, ctx: CampaignContext): CampaignResult {
  if (c.outcome) return { ok: false, error: 'La campaña ha terminado.' };
  const { slot } = ctx;

  switch (cmd.type) {
    case 'chooseMission':
      return propose(c, { type: 'chooseMission', id: cmd.id }, ctx);
    case 'clear':
      return propose(c, { type: 'clear', plot: cmd.plot }, ctx);
    case 'build':
      return propose(c, { type: 'build', plot: cmd.plot, facility: cmd.facility }, ctx);
    case 'upgrade':
      return propose(c, { type: 'upgrade', plot: cmd.plot }, ctx);
    case 'contact':
      return propose(c, { type: 'contact', region: cmd.region }, ctx);
    case 'answerEvent':
      return propose(c, { type: 'event', option: cmd.option }, ctx);
    case 'investigate':
      return propose(c, { type: 'investigate', id: cmd.id }, ctx);
    case 'craft':
      return propose(c, { type: 'craft', project: cmd.project }, ctx);

    case 'assignWeapon': {
      // Personal, like equipping: each commander arms their own soldiers from the Armería. Anyone takes any
      // weapon (docs/FASE8.md §A): one in the hand, one at the belt.
      const w = (c.foundWeapons ?? []).find((x) => x.id === cmd.weapon);
      if (!w) return { ok: false, error: 'Esa arma no está en la Armería.' };
      const holder = w.holder ? c.soldiers.find((s) => s.id === w.holder) : undefined;
      if (holder && holder.owner !== slot) return { ok: false, error: `La lleva ${holder.name}, de tu compañero.` };
      if (cmd.soldier === null) {
        w.holder = null;
        return { ok: true };
      }
      const s = c.soldiers.find((x) => x.id === cmd.soldier && x.alive);
      if (!s || s.owner !== slot) return { ok: false, error: 'Solo puedes armar a tus soldados.' };
      const hand = FOUND_WEAPONS[w.kind].slot;
      for (const other of c.foundWeapons ?? []) if (other.holder === s.id && FOUND_WEAPONS[other.kind].slot === hand) other.holder = null;
      w.holder = s.id;
      return { ok: true };
    }

    case 'repair': {
      const w = (c.foundWeapons ?? []).find((x) => x.id === cmd.weapon);
      const blocked = repairBlocker(c, slot, cmd.weapon);
      if (blocked || !w) return { ok: false, error: blocked ?? 'Esa arma no está en la Armería.' };
      const parts = Math.min(c.parts ?? 0, repairParts(w));
      c.parts = (c.parts ?? 0) - parts;
      w.condition = Math.min(100, (w.condition ?? 100) + parts * REPAIR_PER_PART);
      if (w.condition >= 100) delete w.condition;
      return { ok: true };
    }

    case 'mod': {
      const w = (c.foundWeapons ?? []).find((x) => x.id === cmd.weapon);
      const blocked = modBlocker(c, slot, cmd.weapon, cmd.mod, cmd.on);
      if (blocked || !w) return { ok: false, error: blocked ?? 'Esa arma no está en la Armería.' };
      const stock = (c.weaponMods ??= {});
      if (cmd.on) {
        w.mods = [...(w.mods ?? []), cmd.mod];
        stock[cmd.mod] = (stock[cmd.mod] ?? 0) - 1;
      } else {
        w.mods = (w.mods ?? []).filter((m) => m !== cmd.mod);
        if (!w.mods.length) delete w.mods;
        stock[cmd.mod] = (stock[cmd.mod] ?? 0) + 1;
      }
      return { ok: true };
    }

    case 'vote': {
      const p = c.proposal;
      if (!p) return { ok: false, error: 'No hay ninguna propuesta.' };
      if (p.by === slot) return { ok: false, error: 'No puedes votar tu propia propuesta.' };
      c.proposal = null;
      if (!cmd.accept) {
        log(c, `Propuesta rechazada: ${p.text}.`, 'bad');
        return { ok: true };
      }
      const error = validateAction(c, p.action);
      if (error) return { ok: false, error };
      applyAction(c, p.action, ctx);
      return { ok: true };
    }

    case 'withdraw':
      if (c.proposal?.by !== slot) return { ok: false, error: 'No tienes ninguna propuesta pendiente.' };
      c.proposal = null;
      return { ok: true };

    case 'cancelMission':
      if (!c.activeMission) return { ok: false, error: 'No hay misión elegida.' };
      c.activeMission = null;
      c.squad = [];
      c.launchReady = [false, false];
      log(c, 'Misión cancelada.');
      return { ok: true };

    case 'squad': {
      if (!c.activeMission) return { ok: false, error: 'Primero elegid una misión.' };
      const s = c.soldiers.find((x) => x.id === cmd.soldier);
      if (!s || s.owner !== slot) return { ok: false, error: 'Solo puedes elegir a tus soldados.' };
      if (!cmd.on) {
        c.squad = c.squad.filter((id) => id !== s.id);
      } else if (!c.squad.includes(s.id)) {
        if (s.infection) return { ok: false, error: `${s.name} tiene fiebre: hay que tratarle antes.` };
        if (!soldierReady(s)) return { ok: false, error: `${s.name} no está en condiciones.` };
        const place = squadPlace(c, slot, ctx.connected);
        if (place.error) return { ok: false, error: place.error };
        joinSquad(c, s, place);
      }
      c.launchReady = [false, false];
      return { ok: true };
    }

    case 'treat': {
      // Two keys, like launching: one proposes, the other confirms (alone, one is enough).
      const s = c.soldiers.find((x) => x.id === cmd.soldier && x.alive);
      if (!s?.infection) return { ok: false, error: 'No está contagiado.' };
      if (!cmd.ready) {
        if (c.treatment?.soldier === s.id) c.treatment = null;
        return { ok: true };
      }
      if ((c.medicines ?? 0) < treatCost(c)) return { ok: false, error: 'No hay medicinas suficientes.' };
      if (c.treatment && c.treatment.soldier !== s.id) return { ok: false, error: 'Ya hay otro tratamiento propuesto: confirmadlo o rechazadlo antes.' };
      c.treatment ??= { soldier: s.id, ready: [false, false] };
      c.treatment.ready[slot] = true;
      if (everyoneReady(c.treatment.ready, ctx.connected)) {
        c.medicines = (c.medicines ?? 0) - treatCost(c);
        delete s.infection;
        c.treatment = null;
        log(c, `${s.name} recibe el tratamiento: le baja la fiebre.`, 'good');
      }
      return { ok: true };
    }

    case 'medicate': {
      const s = c.soldiers.find((x) => x.id === cmd.soldier && x.alive);
      if (!s || s.woundedDays <= 0) return { ok: false, error: 'No está herido.' };
      if ((c.medicines ?? 0) < MEDICATE_COST) return { ok: false, error: 'No hay medicinas.' };
      c.medicines = (c.medicines ?? 0) - MEDICATE_COST;
      s.woundedDays = Math.max(0, s.woundedDays - MEDICATE_DAYS);
      log(c, s.woundedDays ? `${s.name} recibe medicinas: ${MEDICATE_DAYS} días menos de recuperación.` : `${s.name} recibe medicinas y se ha recuperado.`, 'good');
      return { ok: true };
    }

    case 'launch': {
      if (!c.activeMission) return { ok: false, error: 'Primero elegid una misión.' };
      if (cmd.ready && !c.squad.length) return { ok: false, error: 'La escuadra está vacía.' };
      if (cmd.ready && c.event) return { ok: false, error: 'Antes hay que decidir qué hacer con el acontecimiento.' };
      c.launchReady[slot] = cmd.ready;
      if (cmd.ready && everyoneReady(c.launchReady, ctx.connected)) {
        c.launchReady = [false, false];
        return { ok: true, launch: true };
      }
      return { ok: true };
    }

    case 'advance': {
      if (c.activeMission) return { ok: false, error: 'Hay una misión elegida: lanzadla o cancelad.' };
      if (c.event) return { ok: false, error: 'Antes hay que decidir qué hacer con el acontecimiento.' };
      c.advanceReady[slot] = cmd.ready;
      if (cmd.ready && everyoneReady(c.advanceReady, ctx.connected)) {
        c.advanceReady = [false, false];
        advanceTime(c, ctx.rng);
      }
      return { ok: true };
    }

    case 'recruit': {
      const mine = c.soldiers.filter((s) => s.owner === slot && s.alive).length;
      if (mine >= soldierCap(c)) return { ok: false, error: `Máximo ${soldierCap(c)} soldados por jugador.` };
      if (bedsFree(c) <= 0) return { ok: false, error: 'Para reclutar más soldados, construid un barracón: no quedan camas.' };
      if (!canAfford(c, recruitCost(c))) return { ok: false, error: 'No hay suministros suficientes.' };
      const place = cmd.squad ? (c.activeMission ? squadPlace(c, slot, ctx.connected) : { error: 'Primero elegid una misión.' }) : null;
      if (place?.error) return { ok: false, error: place.error };
      pay(c, recruitCost(c));
      const s = addSoldier(c, slot, ctx.rng);
      // The training centre sends them in with something more learnt: a level at its level 2, two at 3.
      const trained = Math.max(0, facilityLevel(c, 'training') - 1);
      if (trained) seasoned(s, trained, ctx.rng);
      log(c, `Llega a la escuadra: ${s.name} (${veterancyName(s)}).`);
      if (place) {
        joinSquad(c, s, place);
        c.launchReady = [false, false];
      }
      return { ok: true };
    }

    case 'customize': {
      const s = c.soldiers.find((x) => x.id === cmd.soldier);
      if (!s || s.owner !== slot || !s.alive) return { ok: false, error: 'Solo puedes personalizar a tus soldados.' };
      const name = cmd.name.trim();
      const nickname = cmd.nickname.trim();
      if (!name || name.length > NAME_MAX) return { ok: false, error: `El nombre debe tener entre 1 y ${NAME_MAX} letras.` };
      if (nickname.length > NICKNAME_MAX) return { ok: false, error: `El apodo puede tener hasta ${NICKNAME_MAX} letras.` };
      if (!isAppearance(cmd.appearance)) return { ok: false, error: 'Ese aspecto no es válido.' };
      s.name = name;
      s.nickname = nickname;
      // The sex comes with the name: a look sent without it keeps the soldier's.
      s.appearance = normalizeAppearance({ ...cmd.appearance, sex: cmd.appearance.sex ?? s.appearance.sex });
      return { ok: true };
    }

    case 'unlock': {
      const s = c.soldiers.find((x) => x.id === cmd.soldier);
      if (!s || s.owner !== slot || !s.alive) return { ok: false, error: 'Solo puedes elegir las habilidades de tus soldados.' };
      const blocked = unlockBlocker(s.skills, s.board, cmd.cell);
      if (blocked) return { ok: false, error: blocked };
      s.board.push(cmd.cell);
      log(c, `${s.name} aprende ${BOARD_CELLS[cmd.cell]!.name}.`, 'good');
      return { ok: true };
    }

    case 'buy': {
      const def = ITEMS[cmd.item];
      if (!itemUnlocked(c, cmd.item)) return { ok: false, error: `Se desbloquea con el nivel ${def.level} del Bastión.` };
      const cost = shopPrice(c, cmd.item);
      if (!canAfford(c, cost)) return { ok: false, error: 'No hay suministros suficientes.' };
      pay(c, cost);
      c.inventory[cmd.item] = (c.inventory[cmd.item] ?? 0) + 1;
      log(c, `Comprado: ${def.name}.`);
      return { ok: true };
    }

    case 'equip': {
      const s = c.soldiers.find((x) => x.id === cmd.soldier);
      if (!s || s.owner !== slot || !s.alive) return { ok: false, error: 'Solo puedes equipar a tus soldados.' };
      if (cmd.item) {
        // The belt (docs/FASE9.md §4): two places for any accessory; the special rounds are the weapons' mods now.
        if (ITEMS[cmd.item].slot !== 'utility') return { ok: false, error: 'Eso no va en el cinturón.' };
        if (s.loadout[cmd.slot] !== cmd.item && itemsFree(c, cmd.item) <= 0) return { ok: false, error: 'No quedan unidades libres de ese objeto.' };
      }
      s.loadout[cmd.slot] = cmd.item;
      return { ok: true };
    }
  }
}

// --------------------------------------------------------------------- time

/** Advances day by day until something worth stopping for happens. */
export function advanceTime(c: CampaignState, rng: Rng): void {
  // Catches up with story beats that had to wait (another event was pending).
  refreshNarrative(c, rng);
  for (let i = 0; i < 40 && !c.outcome && !c.event; i++) {
    if (passDay(c, rng)) return;
  }
}

/** Lets `days` go by whatever happens on them (a mission's flight and recovery). */
function passDays(c: CampaignState, days: number, rng: Rng): void {
  for (let i = 0; i < days && !c.outcome; i++) passDay(c, rng);
}

/** One day in the Bastion. True when something happened that should stop the clock. */
function passDay(c: CampaignState, rng: Rng): boolean {
  c.day++;
  let stop = false;

  // Hungry, wounds heal only every other day.
  const heal = c.hungry && c.day % 2 === 1 ? 0 : healRate(c);
  for (const s of c.soldiers) {
    if (!s.alive || s.woundedDays === 0 || !heal) continue;
    s.woundedDays = Math.max(0, s.woundedDays - heal);
    if (s.woundedDays === 0) log(c, `${s.name} se ha recuperado de sus heridas.`, 'good');
  }

  for (const work of [...c.base.works]) {
    if (--work.daysLeft > 0) continue;
    const plot = plotById(c, work.plot)!;
    if (work.kind === 'clear') {
      const { salvage } = clearCost(plot);
      const ruins = plot.terrain === 'ruins';
      plot.terrain = 'clear';
      c.credits += salvage;
      log(c, ruins ? `Ruinas desmontadas en ${plotName(plot)}: +${salvage} § en materiales.` : `Terreno despejado en ${plotName(plot)}: listo para construir.`, 'good');
    } else if (work.kind === 'craft') {
      const def = CRAFTS[work.project!];
      if (def.makes) {
        (c.foundWeapons ??= []).push({ id: newId(c, 'w'), kind: def.makes, holder: null });
        log(c, `El Taller ha terminado ${def.the}. Ya está en la Armería.`, 'good');
      } else if (def.makesMod) {
        (c.weaponMods ??= {})[def.makesMod] = (c.weaponMods?.[def.makesMod] ?? 0) + 1;
        log(c, `El Taller ha terminado ${def.the}. Ya está en la Armería, para montar en un arma.`, 'good');
      } else {
        (c.crafted ??= []).push(def.id);
        if (def.kind === 'ammo') log(c, `El Taller ha terminado ${def.the}: un cargador de repuesto más para cada soldado que salga.`, 'good');
        else log(c, `El Taller ha terminado: ${def.name.toLowerCase()} para todos (cada uno, hasta lo que permita su rango).`, 'good');
      }
    } else if (work.kind === 'upgrade') {
      plot.level = (plot.level ?? 1) + 1;
      log(c, `${FACILITIES[work.facility!].name}, nivel ${plot.level}: ${FACILITIES[work.facility!].upgrades[plot.level - 2]!.split('.')[0]}.`, 'good');
    } else {
      plot.building = work.facility;
      plot.level = 1;
      const bonus = adjacencyAt(c, plot, work.facility!);
      log(
        c,
        bonus
          ? `${FACILITIES[work.facility!].name} y ${buildingName(bonus.next)}, pared con pared: ${bonus.text.split('.')[0]}.`
          : `Instalación terminada: ${FACILITIES[work.facility!].name}.`,
        'good',
      );
    }
    c.base.works = c.base.works.filter((w) => w !== work);
    stop = true;
  }

  for (const o of [...c.offers]) {
    if (o.expiresDay === null || o.expiresDay >= c.day) continue;
    c.offers = c.offers.filter((x) => x.id !== o.id);
    const q = questOf(c, o);
    if (q) {
      // A side quest's operation that expires loses the quest instead.
      log(c, `La misión ${o.name} ha expirado.`, 'bad');
      finishQuest(c, q, 'failure', rng);
    } else {
      c.doom++;
      log(c, `La misión ${o.name} ha expirado. ${doomChangeText(bastionLevel(c), 1)}`, 'bad');
    }
    stop = true;
  }

  const listened = c.rumors.find((r) => r.id === c.listening);
  if (listened && --listened.daysLeft <= 0) {
    resolveRumor(c, listened, rng);
    stop = true;
  }
  for (const r of c.rumors.filter((x) => x.id !== c.listening && x.expiresDay < c.day)) {
    c.rumors = c.rumors.filter((x) => x.id !== r.id);
    log(c, `El rumor ${ofZone(REGIONS[r.region].name)} se ha enfriado.`);
  }
  if (c.day >= c.nextRumorDay) {
    c.nextRumorDay = c.day + 7 + rng.int(5);
    if (c.rumors.length < MAX_RUMORS) {
      newRumor(c, rng);
      stop = true;
    }
  }

  if (c.day >= c.nextMissionDay) {
    c.nextMissionDay = c.day + 5 + rng.int(3);
    if (c.offers.filter((o) => !o.final && !o.facility && !o.story).length < MAX_OFFERS + (facilityLevel(c, 'listening') ? 1 : 0)) {
      const offer = makeOffer(c, rng);
      c.offers.push(offer);
      log(c, `Nueva misión en ${REGIONS[offer.region].name}: ${offer.name}.`);
      stop = true;
    }
  }

  if (c.day >= c.nextDoomDay) {
    c.nextDoomDay = c.day + DOOM_INTERVAL + listeningDelay(c);
    c.doom++;
    log(c, doomChangeText(bastionLevel(c), 1), 'bad');
    stop = true;
  }

  for (const r of c.regions) {
    if (!r.facility || c.day < r.facility.nextDoomDay) continue;
    r.facility.nextDoomDay = c.day + FACILITY_DOOM_INTERVAL;
    c.doom++;
    log(c, bastionLevel(c) >= PARTITURA_LEVEL ? `El Nido ${ofZone(REGIONS[r.id].name)} acerca la Marea.` : `El Nido ${ofZone(REGIONS[r.id].name)} hace crecer ${doomSubject(bastionLevel(c))}.`, 'bad');
    stop = true;
  }

  if (c.day >= c.nextFacilityDay) {
    c.nextFacilityDay = c.day + 20 + rng.int(7);
    const free = c.regions.filter((r) => !r.facility);
    if (c.regions.filter((r) => r.facility).length < MAX_FACILITIES && free.length) {
      // Nidos gather under the Marea or beside it, three times as often as elsewhere.
      const weight = (r: RegionState) => (underMarea(c, r.id) || REGIONS[r.id].roads.some((n) => underMarea(c, n)) ? NIDO_TIDE_WEIGHT : 1);
      let roll = rng.int(free.reduce((n, r) => n + weight(r), 0));
      const region = free.find((r) => (roll -= weight(r)) < 0)!;
      region.facility = { nextDoomDay: c.day + FACILITY_DOOM_INTERVAL };
      log(c, `Los zombis se juntan en un Nido en ${REGIONS[region.id].name}.`, 'bad');
      if (region.contacted) c.offers.push(makeFacilityOffer(c, region.id, rng));
      else log(c, `Contactad con ${REGIONS[region.id].name} para poder asaltarlo.`);
      stop = true;
    }
  }

  // The bitten whose days ran out turn.
  for (const s of c.soldiers) {
    const day = s.alive ? turnDay(c, s) : null;
    if (day === null || c.day < day) continue;
    turn(c, s);
    stop = true;
  }

  // Food (docs/FASE4.md): the Huerto grows, La Vega sends, and the Bastion eats.
  addFood(c, gardenYield(c));
  if (c.day >= (c.nextFoodDay ?? 0)) {
    c.nextFoodDay = c.day + LA_VEGA_FOOD_DAYS;
    if (enclaveStands(c, 'oce')) {
      addFood(c, LA_VEGA_FOOD);
      log(c, `${REGIONS.oce.name} manda comida: +${LA_VEGA_FOOD} raciones.`, 'good');
    }
  }
  const meal = dailyRations(c);
  if ((c.food ?? 0) >= meal) {
    c.food = (c.food ?? 0) - meal;
  } else {
    c.food = 0;
    if (!c.hungry) {
      c.hungry = true;
      log(c, 'No quedan raciones: el Bastión pasa hambre. Hasta que vuelva a haberlas, −10 de voluntad en las misiones y las heridas curan más despacio.', 'bad', {
        kicker: 'Despensa',
        title: 'Hambre en el Bastión',
      });
      stop = true;
    }
  }

  // The enclaves that still stand send medicines.
  if (c.day >= (c.nextMedicineDay ?? 0)) {
    c.nextMedicineDay = c.day + ENCLAVE_DAYS;
    const enclaves = livingEnclaves(c);
    if (enclaves.length) {
      c.medicines = (c.medicines ?? 0) + enclaves.length;
      log(c, `${enclaves.map((id) => REGIONS[id].name).join(' y ')} ${enclaves.length > 1 ? 'mandan' : 'manda'} medicinas: +${enclaves.length} ✚.`, 'good');
    }
  }

  if (c.day >= c.nextSupplyDay) {
    c.nextSupplyDay = c.day + MONTH;
    // A zone under the Marea sends nothing.
    const income = BASE_INCOME + c.regions.filter((r) => r.contacted && !underMarea(c, r.id)).reduce((sum, r) => sum + regionIncome(c, r.id), 0);
    c.credits += income;
    log(c, `Llega el convoy de Brena: Ruth Halvorsen manda ${income} suministros.`, 'good');
    stop = true;
  }

  if (c.day >= c.nextEventDay) {
    c.nextEventDay = c.day + 9 + rng.int(5);
    const id = rng.pick((Object.keys(EVENTS) as Exclude<CampaignEventId, 'quest'>[]).filter((x) => !EVENTS[x].story));
    c.event = id;
    c.seen[id] = (c.seen[id] ?? 0) + 1;
    log(c, `Acontecimiento: ${EVENTS[id].title}.`);
    stop = true;
  }

  if (syncMarea(c)) stop = true;
  if (c.doom >= DOOM_MAX) {
    c.outcome = 'defeat';
    log(c, 'La Marea ha llegado a Atalaya.', 'bad');
    return true;
  }
  // A story chapter whose wait is over opens today.
  const offers = c.offers.length;
  refreshNarrative(c, rng);
  return stop || c.offers.length > offers || !!c.event;
}

// ------------------------------------------------------------- deployment

/** Days off a wound costs: these, and as many again per point of health lost. */
const WOUND_DAYS = 2;

/**
 * Everything a soldier brings to a mission (docs/FASE9.md): its skills,
 * talents and veterancy, its belt, its weapons and the armour the Taller made.
 */
export function squadMember(c: CampaignState, s: CampaignSoldier): SquadMember {
  const gear = [s.loadout.utility, s.loadout.ammo].filter((g): g is ItemId => !!g);
  const arms = soldierArms(c, s);
  const base = fighterStats(s.skills, s.growth, s.board, gear, arms);
  let mods = base.mods;
  let maxHp = base.maxHp;
  let armor = base.armor;
  const charges = base.charges;
  const weapon = weaponTier(c, s);
  const suit = armorTier(c);
  maxHp += ARMOR_TIERS[suit - 1]!.hp;
  armor += ARMOR_TIERS[suit - 1]!.armor;
  if (hasTech(c, 'plasmaGrenades')) mods = addMods(mods, { grenadeDamage: 2 });
  if (hasTech(c, 'nanoMedkit')) {
    mods = addMods(mods, { heal: 2 });
    // The belt's Botiquín (the Grillo's flying medkit is gone with the classes).
    if (charges.firstAid !== undefined) charges.firstAid += 1;
  }
  const traits = s.traits.map((t) => TRAITS[t]);
  mods = addMods(mods, { aim: traits.reduce((sum, t) => sum + t.aim, 0) + rangeAim(c) });
  return {
    campaignId: s.id,
    name: s.name,
    nickname: s.nickname,
    appearance: { ...s.appearance },
    weaponTier: weapon,
    armorTier: suit,
    owner: s.owner,
    template: base.template,
    rank: base.rank,
    perks: base.perks,
    mods,
    maxHp,
    hp: maxHp,
    charges,
    // Hungry, nobody fights as well (docs/FASE4.md).
    will: base.will + traits.reduce((sum, t) => sum + t.will, 0) + memorialWill(c) - (c.hungry ? HUNGRY_WILL : 0),
    armor,
    gear,
    weaponName: weaponLabel(c, s),
    arms,
    mags: spareMags(c),
    // The Chapistería: one more backpack slot (docs/FASE8.md §C).
    ...(facilityLevel(c, 'garage') ? { packSlots: BACKPACK_SLOTS + 1 } : {}),
    // Its bars fill out there, with what the Taller de oficios adds (docs/FASE10.md §2).
    lines: missionLines(s.skills, s.growth, learningBonus(c)),
  };
}

/** Medicines also stop a bite's contagion in the missions (docs/FASE8.md §C): the Enfermería from level 2. */
export function medicineCures(c: CampaignState): boolean {
  return facilityLevel(c, 'infirmary') >= 2;
}

export function activeOffer(c: CampaignState): MissionOffer | undefined {
  return c.offers.find((o) => o.id === c.activeMission);
}

export function buildSquad(c: CampaignState): SquadMember[] {
  return c.squad
    .map((id) => c.soldiers.find((s) => s.id === id))
    .filter((s): s is CampaignSoldier => !!s && soldierReady(s))
    .map((s) => squadMember(c, s));
}

/** Campaign bonus to hacking (alien encryption). */
export function campaignHackBonus(c: CampaignState): number {
  return hasTech(c, 'encryption') ? 20 : 0;
}

/** «a, b y c». */
function listOf(parts: string[]): string {
  return parts.length > 1 ? `${parts.slice(0, -1).join(', ')} y ${parts[parts.length - 1]}` : (parts[0] ?? '');
}

/**
 * Adds Bastion experience and announces every level reached. New levels can
 * open the next story chapter and, at the top, the final mission.
 */
export function gainXp(c: CampaignState, amount: number, rng: Rng): void {
  const before = bastionLevel(c);
  c.xp += amount;
  const after = bastionLevel(c);
  for (let level = before + 1; level <= after; level++) {
    log(c, `El Bastión sube al nivel ${level}.`, 'good');
    // Weapons the level brings to the Armería (docs/FASE8.md §A).
    const arms = LEVEL_ARMS[level] ?? [];
    for (const kind of arms) (c.foundWeapons ??= []).push({ id: newId(c, 'w'), kind, holder: null });
    if (arms.length) log(c, `A la Armería ${arms.length === 1 ? 'llega' : 'llegan'} ${listOf(arms.map((k) => FOUND_WEAPONS[k].find))}.`, 'good');
  }
  refreshNarrative(c, rng);
}

/** The debrief's loot line (lore.ts lootSummary), or null when nothing came back. */
function lootLine(h: LootTotals): string | null {
  return lootSummary(h.supplies, h.items, h.medicines, h.food, h.parts, h.weapons);
}

/** Folds a finished tactical mission back into the campaign. */
export function applyMissionResult(c: CampaignState, mission: GameState, rng: Rng): MissionReport {
  const offer = activeOffer(c);
  const victory = mission.outcome === 'victory';
  c.missionsFlown = (c.missionsFlown ?? 0) + 1;
  const report: MissionReport = {
    day: c.day,
    missionName: offer?.name ?? 'Misión',
    victory,
    reason: mission.outcomeReason ?? 'wiped',
    squad: [],
    reward: victory && offer ? offer.reward : null,
    doomChange: 0,
    xp: 0,
    levelFrom: bastionLevel(c),
    levelTo: bastionLevel(c),
    bodies: 0,
    salvage: 0,
    story: offer?.story ?? null,
    ...(mission.mission.night ? { night: true } : {}),
  };

  // Abandoning counts as a retreat: whoever is still standing comes home.
  // Down, or left behind when the bus went (docs/PRUEBA-2026-10-14.md P1): either way, gone.
  const fell = (u: GameState['units'][number]) => (!u.alive && !u.evacuated && u.hp <= 0) || !!u.lost;
  // The Camposanto at level 3: whoever comes back from a mission with losses learns from it.
  const mourning = facilityLevel(c, 'memorial') >= 3 && mission.units.some((u) => u.campaignId && fell(u)) ? 1 : 0;
  // Weapons that changed hands in the mission (docs/FASE8.md §C): what a soldier comes home holding is theirs;
  // their own, swapped for a find, comes back only in a backpack. Armería id → whose it was.
  const swapped = new Map<string, string>();
  for (const u of mission.units) {
    const s = u.campaignId ? c.soldiers.find((x) => x.id === u.campaignId) : undefined;
    if (!s) continue;
    const died = fell(u);
    s.missions++;
    s.kills += u.kills;
    const entry: ReportSoldier = {
      id: s.id,
      name: s.name,
      owner: s.owner,
      status: 'ok',
      woundedDays: 0,
      kills: u.kills,
    };
    report.squad.push(entry);
    if (died) {
      s.alive = false;
      s.fellDay = report.day;
      entry.status = u.lost ? 'lost' : 'dead';
      if (u.lost) log(c, u.infected && !hasQuarantine(c) ? `${s.name} se quedó fuera: llevaba un mordisco, y sin Cuarentena no se sube a La Lata.` : `${s.name} se quedó fuera cuando salió La Lata.`, 'bad');
      else if (facilityCount(c, 'memorial')) log(c, `${s.name} descansa en el Camposanto.`);
      // Equipment is lost with its bearer, the Armería's weapons too.
      for (const item of [s.loadout.utility, s.loadout.ammo]) if (item) c.inventory[item] = Math.max(0, (c.inventory[item] ?? 0) - 1);
      s.loadout = { utility: null, ammo: null };
      for (const carried of (c.foundWeapons ?? []).filter((w) => w.holder === s.id)) {
        c.foundWeapons = c.foundWeapons!.filter((w) => w !== carried);
        log(c, `Con ${s.name} se pierde su ${FOUND_WEAPONS[carried.kind].name.toLowerCase()}.`, 'bad');
      }
      continue;
    }
    if (u.arms) {
      for (const hand of ['primary', 'melee'] as const) {
        const kind = hand === 'primary' ? u.arms.primary : u.arms.melee;
        const id = hand === 'primary' ? u.arms.primaryId : u.arms.meleeId;
        const before = hand === 'primary' ? foundWeaponOf(c, s) : meleeWeaponOf(c, s);
        if (before && before.id !== id) {
          before.holder = null;
          swapped.set(before.id, s.name);
        }
        if (kind && !id) (c.foundWeapons ??= []).push({ id: newId(c, 'w'), kind, holder: s.id, ...(hand === 'primary' ? wornAs(u.arms) : {}) });
      }
      // What the shots took off the weapon in the hand (docs/FASE9.md §5) comes home with it, and is told when it shows.
      const own = u.arms.primaryId ? c.foundWeapons?.find((w) => w.id === u.arms!.primaryId) : undefined;
      if (own) Object.assign(own, wornAs(u.arms));
      if (u.arms.primary && conditionOf(u) < WORN_BELOW) log(c, wornLine(s.name, u.arms.primary, conditionOf(u) <= 0), 'bad');
    }
    const lost = u.maxHp - u.hp;
    if (lost > 0) {
      s.woundedDays = Math.max(1, WOUND_DAYS + lost * WOUND_DAYS - garageCut(c).wounds);
      entry.status = 'wounded';
      entry.woundedDays = s.woundedDays;
    }
    // What it did teaches it (docs/FASE10.md §3): more with the Taller de oficios, and the Camposanto at 3 after losses.
    // The bars filled out there keep what they rolled; the Camposanto's share, known only now, may fill more.
    const taught = learn(s.skills, s.growth, u.deeds, rng, learningBonus(c) + (mourning ? LEARNING_BONUS : 0), u.lines);
    for (const bar of taught.bars) log(c, barLine(s.name, bar.skill, bar.growth), 'good');
    entry.learned = taught.gained;
    entry.deeds = { ...(u.deeds ?? {}) };
    entry.bars = taught.bars;
    s.skills = taught.skills;
    s.growth = taught.growth;
  }

  // What the squad searched and brought back (docs/FASE2.md): a fallen soldier's is lost.
  const haul = broughtBack(mission);
  report.loot = { ...haul, line: lootLine(haul) };
  c.credits += haul.supplies;
  for (const item of haul.items) c.inventory[item] = (c.inventory[item] ?? 0) + 1;
  c.medicines = (c.medicines ?? 0) + haul.medicines;
  addFood(c, haul.food);
  c.parts = (c.parts ?? 0) + haul.parts;
  // Found weapons go to the Armería, for a soldier to take; a soldier's own, swapped for a find, goes back to the rack.
  for (const found of haul.arms ?? haul.weapons.map((kind): CarriedWeapon => ({ kind }))) (c.foundWeapons ??= []).push({ id: newId(c, 'w'), kind: found.kind, holder: null, ...wornAs(found) });
  for (const back of haul.returned ?? []) {
    swapped.delete(back.id);
    const home = c.foundWeapons?.find((w) => w.id === back.id);
    if (home) Object.assign(home, wornAs(back));
    else (c.foundWeapons ??= []).push({ id: back.id, kind: back.kind, holder: null, ...wornAs(back) });
  }
  for (const mod of haul.mods ?? []) (c.weaponMods ??= {})[mod] = (c.weaponMods?.[mod] ?? 0) + 1;
  if (report.loot.line) log(c, report.loot.line, 'good');
  for (const [id, name] of swapped) {
    const left = c.foundWeapons!.find((w) => w.id === id)!;
    c.foundWeapons = c.foundWeapons!.filter((w) => w !== left);
    log(c, `${name} vuelve sin su ${FOUND_WEAPONS[left.kind].name.toLowerCase()}: se quedó en la misión.`, 'bad');
  }

  // Holding the field means the bodies come home, and the black market pays for them (not from a Saqueo: you leave by the evac).
  if (victory && mission.mission.kind !== 'scavenge') {
    for (const u of mission.units) {
      const value = BODY_VALUE[u.template];
      if (u.alive || u.objective || !value) continue;
      report.bodies++;
      report.salvage += value;
    }
    // Dr Nwosu knows which tesserae are worth something.
    if (c.story.done >= NWOSU_CHAPTER) report.salvage = Math.round(report.salvage * 1.25);
    c.credits += report.salvage;
    if (report.bodies) log(c, `Registráis ${report.bodies} cuerpos: +${report.salvage} suministros.`, 'good');
    // The Acorazados' plates and hinges: a part each (docs/FASE4.md).
    report.scrap = mission.units.filter((u) => u.template === 'mec' && !u.alive).length;
    c.parts = (c.parts ?? 0) + report.scrap;
    if (report.scrap) {
      log(c, report.scrap === 1 ? 'Le quitáis el chaleco y el casco a un Acorazado: +1 pieza.' : `Les quitáis el chaleco y el casco a ${report.scrap} Acorazados: +${report.scrap} piezas.`, 'good');
    }
  }

  if (victory && offer) {
    const r = offer.reward;
    c.credits += r.credits ?? 0;
    c.medicines = (c.medicines ?? 0) + (r.medicines ?? 0);
    addFood(c, r.food ?? 0);
    c.parts = (c.parts ?? 0) + (r.parts ?? 0);
    if (r.doom) {
      c.doom = Math.max(0, c.doom - r.doom);
      report.doomChange = -r.doom;
    }
    if (r.recruit) {
      const s = addSoldier(c, neediestPlayer(c), rng);
      log(c, `${s.name} se une al Bastión como recluta.`, 'good');
    }
    if (offer.facility) {
      const region = c.regions.find((x) => x.id === offer.facility);
      if (region) region.facility = null;
      log(c, `Nido ${ofZone(REGIONS[offer.facility].name)} quemado.`, 'good');
    }
    log(c, `Misión cumplida: ${offer.name}.`, 'good');
    const quest = questOf(c, offer);
    if (quest) advanceQuest(c, quest, rng);
    if (offer.story && offer.story > c.story.done) {
      c.story.done = offer.story;
      c.story.nextDay = c.day + STORY_GAP;
      log(c, STORY[offer.story - 1]!.after, 'good');
    }
    // The campaign ends when both keys decide who gets the vaccine.
    if (offer.final) {
      c.event = 'finale';
      log(c, 'La sirena de La Lata se lleva a la Marea.', 'good');
    }
  } else {
    c.doom++;
    report.doomChange = 1;
    log(c, `Misión fracasada: ${offer?.name ?? ''}.`, 'bad');
    const quest = offer && questOf(c, offer);
    if (quest) finishQuest(c, quest, 'failure', rng);
    if (c.doom >= DOOM_MAX) c.outcome = 'defeat';
  }
  for (const e of report.squad) if (e.status === 'dead') log(c, `${e.name} ha caído en combate.`, 'bad');

  // Combat is what makes the Bastion grow.
  const kills = report.squad.reduce((sum, e) => sum + e.kills, 0);
  const earned = missionXp(victory, offer?.difficulty ?? 1, kills);
  report.xp = Math.round(earned * xpMultiplier(c));
  gainXp(c, report.xp, rng);
  report.levelTo = bastionLevel(c);

  if (!c.soldiers.some((s) => s.alive)) {
    c.outcome = 'defeat';
    log(c, 'No quedan soldados. El Bastión ha caído.', 'bad');
  }

  // Ordinary offers are gone either way; facility assaults, story chapters and the final mission stay until won.
  if (offer && ((!offer.final && !offer.facility && !offer.story) || victory)) c.offers = c.offers.filter((o) => o.id !== offer.id);
  c.activeMission = null;
  c.squad = [];
  c.launchReady = [false, false];
  c.lastReport = report;
  maybeFaces(c, report, rng);
  // No days pass on a mission (docs/PRUEBA-2026-10-14.md P7): the clock moves while waiting for news.
  // The bitten come home with a fever: their days count from their arrival (docs/FASE3.md).
  report.infected = [];
  for (const u of mission.units) {
    const s = u.infected && !fell(u) ? c.soldiers.find((x) => x.id === u.campaignId && x.alive) : undefined;
    if (!s) continue;
    s.infection = { since: c.day };
    report.infected.push(s.id);
    log(c, `${s.name} vuelve con un mordisco: tiene fiebre.`, 'bad');
  }
  syncMarea(c);
  return report;
}

// ---------------------------------------------------------------- migration

/**
 * Brings a campaign saved by an earlier version of the current progression
 * up to the current shape. Campaigns from before Fase 10 aren't carried over
 * (campaignOutdated, docs/FASE10.md): the host keeps them aside instead.
 */
export function migrateCampaign(old: CampaignState): CampaignState {
  if (campaignOutdated(old)) throw new Error(`Campaña de la versión ${old.version}: de antes de la 0.4, no se convierte.`);
  const c = JSON.parse(JSON.stringify(old)) as CampaignState;
  c.version = CAMPAIGN_VERSION;
  for (const s of c.soldiers) {
    s.skills ??= { ...NO_SKILLS };
    s.growth ??= { ...NO_GROWTH };
    s.board ??= [];
  }
  c.launchReady = [false, false];
  return c;
}
