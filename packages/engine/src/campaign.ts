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
import {
  ATTR_POINTS_PER_RANK,
  attrBlocker,
  attrPointsFree,
  baseAttrs,
  cleanTalents,
  leaningClass,
  learnTalent,
  soldierStats,
  soldierTemplate,
  specialize,
  SPECIALIZE_RANK,
  SPECIALIZE_TEXT,
  talentBlocker,
  TALENT_POINTS_PER_RANK,
  talentPointsFree,
  TALENTS,
  woundDays,
  type Attributes,
  type AttrId,
  type Talents,
} from './classes';
import { addMods, ITEMS, RANKS, rankForXp, SOLDIER_NAMES, TEMPLATES } from './content';
import {
  ARMOR_TIERS,
  armorTier,
  bastionLevel,
  hasTech,
  itemUnlocked,
  LEVEL_XP,
  levelForXp,
  missionXp,
  TECHS,
  type TechId,
  upgradeLevel,
  WEAPON_TIER_DAMAGE,
  weaponTier,
} from './progression';
import { briefingSignature, doomChangeText, doomSubject, missionBriefing, NWOSU_CHAPTER, PARTITURA_LEVEL, SIMON_CHAPTER, STORY, storyVip, type Ending, type SimonChoice } from './lore';
import { fill, QUESTS, RELATIVES, RUMOR_LINES, SUSPECTS, TRAITS, type QuestDecision, type QuestId, type QuestMission, type QuestOutcome, type TraitId } from './quests';
import type { BaseView } from './protocol';
import type { Rng } from './rng';
import { podPlan, type MapSource, type SquadMember } from './setup';
import type { Appearance, Biome, ClassId, EndReason, GameState, ItemId, ItemSlot, MissionKind, PerkId, Slot, TemplateId, Vec2 } from './types';

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
  | 'memorial';

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
    upgrades: ['Los reclutas llegan con rango de Soldado.', 'Los reclutas llegan con rango de Cabo. Ya saben agacharse.'],
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
    name: 'Sala de simulación',
    description: 'Las misiones dan un 25 % más de experiencia al Bastión.',
    upgrades: ['+40 % de experiencia en las misiones. Mapas de arena con los combates de verdad.', '+55 % de experiencia. Cintas de cada misión, fotograma a fotograma.'],
    cost: 100,
    days: 5,
    level: 3,
    unique: true,
  },
  garage: {
    id: 'garage',
    name: 'Taller de La Mula',
    description: 'Las misiones duran 1 día menos. La Mula vuelve antes, con el depósito lleno.',
    upgrades: ['1 día menos por misión, y las heridas curan 1 día antes: la Mula lleva camilla.', '2 días menos por misión y 2 días menos de heridas. La Mula ya no para a repostar.'],
    cost: 110,
    days: 5,
    level: 3,
    unique: true,
  },
  listening: {
    id: 'listening',
    name: 'Estación de escucha',
    description: 'El reloj enemigo avanza 2 días más tarde (+2 junto al centro de comunicaciones) y hay una misión más a la vista. Morse oye antes lo que viene.',
    upgrades: ['El reloj enemigo, 4 días más tarde (+2 junto a comunicaciones).', 'El reloj enemigo, 6 días más tarde (+2 junto a comunicaciones).'],
    cost: 120,
    days: 5,
    level: 4,
    unique: true,
  },
  echoes: {
    id: 'echoes',
    name: 'Taller de oficios',
    description: 'Abre la rama de Oficio de cada clase: sus dos primeras filas. Teo enseña lo que saben hacer sus manos.',
    upgrades: ['Abre las filas tercera y cuarta. Más herramientas, más maestros.', 'Abre el último oficio de cada rama. Llegan maestros de otros enclaves.'],
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
      '+10 de voluntad para todos. Y quien vuelve de una misión con bajas gana +1 de experiencia: por ellos. Teo graba cada nombre en latón.',
    ],
    cost: 40,
    days: 3,
    level: 1,
    unique: true,
  },
  relay: {
    id: 'relay',
    name: 'Centro de comunicaciones',
    description: '+2 regiones contactables.',
    upgrades: ['+3 regiones contactables por centro. Una antena más alta, en el pino más alto.', '+4 regiones por centro. La antena llega hasta el mar.'],
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
// or the ruins around. La Mula's hangar and airstrip lie outside the grid.

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
  research: 'Expediente',
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
  { facility: 'relay', next: 'command', text: '+1 región contactable. La antena, pegada a la sala de Morse, llega más lejos.' },
  { facility: 'training', next: 'barracks', text: 'Reclutar cuesta 20 § en vez de 30. Los novatos se forman en casa.' },
  { facility: 'quarters', next: 'barracks', text: 'Una cama más. El Cuartel presta la estufa.' },
  { facility: 'range', next: 'training', text: '+2 de puntería más. Del aula a la galería sin cambiarse de botas.' },
  { facility: 'listening', next: 'relay', text: 'El enemigo avanza 2 días más tarde aún. Dos antenas oyen más que una.' },
  { facility: 'echoes', next: 'listening', text: 'Los oficios no rompen la ocultación. Junto a la escucha se aprende a no hacer ruido.' },
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
  kind: 'clear' | 'build' | 'upgrade';
  facility: FacilityId | null;
  daysLeft: number;
  total: number;
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
  /** Marker on the globe, in degrees. */
  lat: number;
  lon: number;
  /** Credits it adds to every monthly payment once contacted. */
  income: number;
}

export const REGIONS: Record<RegionId, RegionDef> = {
  na: { id: 'na', name: 'Los Llanos de Ceniza', lat: 44, lon: -100, income: 35 },
  sa: { id: 'sa', name: 'Carboneda', lat: -15, lon: -60, income: 25 },
  weu: { id: 'weu', name: 'Altomonte', lat: 45, lon: 2, income: 30 },
  eeu: { id: 'eeu', name: 'Valdelobos', lat: 53, lon: 35, income: 25 },
  afr: { id: 'afr', name: 'Ciudad Varela', lat: 4, lon: 21, income: 25 },
  me: { id: 'me', name: 'Puerto Galena', lat: 28, lon: 48, income: 30 },
  eas: { id: 'eas', name: 'El Pantano Gris', lat: 35, lon: 115, income: 35 },
  oce: { id: 'oce', name: 'La Vega', lat: -25, lon: 135, income: 25 },
};

export const HOME_REGION: RegionId = 'weu';

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
          const s = addSoldier(c, neediestPlayer(c), null, rng);
          log(c, `${s.name} se une al Bastión (${TEMPLATES[soldierTemplate(s.cls)].name}).`, 'good');
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
      { label: 'No arriesgar a nadie', effect: 'El enemigo avanza (+1)', apply: (c) => (c.doom += 1) },
    ],
  },
  defector: {
    id: 'defector',
    title: 'Un desertor',
    text: 'Un Saqueador de los Cuervos quiere desertar y ofrece lo que sabe.',
    options: [
      { label: 'Interrogarlo', effect: '+12 de experiencia del Bastión', apply: (c, rng) => gainXp(c, 12, rng) },
      { label: 'Mandarlo a llevarse una horda lejos', effect: 'El enemigo retrocede (−1)', apply: (c) => (c.doom = Math.max(0, c.doom - 1)) },
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
    title: 'Simón Ferreira',
    text: 'Un desertor de los Cuervos llama a la puerta del Bastión. Se llama Simón Ferreira. Dice que sabe dónde tienen los Cuervos a la niña inmune, y cómo entrar sin que se enteren.',
    story: true,
    options: [
      {
        label: 'Confiar en él',
        effect: 'Simón os guiará por dentro: «Los inmunes» a la dificultad prevista',
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
    text: 'La Marea se aleja detrás de la sirena de La Mula y el campamento de la Congregación ha caído. Nwosu tiene en la mano las primeras dosis de la vacuna, y no hay para todos. Hay que decidir ahora, con las dos llaves.',
    story: true,
    options: [
      {
        label: 'Para todos',
        effect: 'Morse lee la fórmula por todas las frecuencias. La oyen los enclaves, y también los Cuervos.',
        apply: (c) => ((c.ending = 'break'), (c.outcome = 'victory')),
      },
      {
        label: 'Para la Red',
        effect: 'La Mula reparte las dosis enclave por enclave, solo a quienes estuvieron. La fórmula se queda en Atalaya, bajo dos llaves.',
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
/** Credits to give a soldier's aptitude and talent points back (half with a training centre). */
export const RETRAIN_COST = 30;
export const MAX_SOLDIERS_PER_PLAYER = 6;

/** Soldiers each player may keep: a commander alone runs both kits. */
export function soldierCap(c: Pick<CampaignState, 'players'>): number {
  return c.players.length > 1 ? MAX_SOLDIERS_PER_PLAYER : 2 * MAX_SOLDIERS_PER_PLAYER;
}
export const DOOM_MAX = 12;
const DOOM_INTERVAL = 12;
/**
 * Days a mission takes: the flight there and back, and recovering. They pass
 * like any other (wounds, works, the enemy clock), so missions are not free in
 * time (balance simulation, 2026-10-11: chaining them won the campaign in
 * under a month and left the base unbuilt).
 */
const MISSION_DAYS = 3;

/** Days a mission keeps the squad away, shortest and longest (the garage takes some off). */
export function missionDays(c: CampaignState): [number, number] {
  return [Math.max(1, MISSION_DAYS - garageCut(c).days), Math.max(1, MISSION_DAYS + 1 - garageCut(c).days)];
}
/** Least days between one story chapter won and the next one opening. */
const STORY_GAP = 8;
const FACILITY_DOOM_INTERVAL = 14;
const MAX_FACILITIES = 3;
const MAX_OFFERS = 3;
const MONTH = 30;
/** Monthly payment from the Council before region income. */
const BASE_INCOME = 60;
/** Credits for each alien body sold after a won mission. */
const BODY_VALUE: Partial<Record<TemplateId, number>> = { trooper: 2, officer: 4, lancer: 3, xenoid: 4, mec: 6, sectoid: 5 };

export interface Loadout {
  utility: ItemId | null;
  ammo: ItemId | null;
}

export interface CampaignSoldier {
  id: string;
  name: string;
  owner: Slot;
  /** Null for a recruit who hasn't specialised yet (classes.ts): it fights as a rookie. */
  cls: ClassId | null;
  xp: number;
  rank: number;
  kills: number;
  missions: number;
  /** Aptitudes (classes.ts): the class's base plus the points spent. */
  attrs: Attributes;
  /** Talent levels. Free points are never stored: they're what the rank gives minus what's spent. */
  talents: Talents;
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
}

export interface MissionReward {
  credits?: number;
  recruit?: ClassId;
  /** Slows the enemy project. */
  doom?: number;
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
}

export type ProposalAction =
  | { type: 'chooseMission'; id: string }
  | { type: 'clear'; plot: string }
  | { type: 'build'; plot: string; facility: FacilityId }
  | { type: 'upgrade'; plot: string }
  | { type: 'contact'; region: RegionId }
  | { type: 'event'; option: 0 | 1 }
  | { type: 'investigate'; id: string };

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
  cls: ClassId | null;
  status: 'ok' | 'wounded' | 'dead';
  woundedDays: number;
  kills: number;
  xpGained: number;
  /** Rank reached when the mission promoted the soldier. */
  promotedTo: number | null;
  /** Class taken on reaching Capitán in this mission. */
  specializedAs?: ClassId;
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

/** Bumped when the campaign shape changes; older campaigns go through `migrateCampaign`. */
export const CAMPAIGN_VERSION = 11;

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

export interface StoryState {
  /** Story chapters won, in order. */
  done: number;
  /** Answer to Simón's visit, which opens chapter 5. */
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

function soldierName(c: CampaignState, rng: Rng): string {
  const used = new Set(c.soldiers.map((s) => s.name));
  const free = SOLDIER_NAMES.filter((n) => !used.has(n));
  if (free.length) return rng.pick(free);
  return `${rng.pick(SOLDIER_NAMES)} ${c.soldiers.length + 1}`;
}

/** A new soldier: a veteran of a class, or a recruit (`cls` null) who specialises with the Rosa. */
function addSoldier(c: CampaignState, owner: Slot, cls: ClassId | null, rng: Rng): CampaignSoldier {
  const s: CampaignSoldier = {
    id: newId(c, 'c'),
    name: soldierName(c, rng),
    owner,
    cls,
    xp: 0,
    rank: 0,
    kills: 0,
    missions: 0,
    attrs: baseAttrs(cls),
    talents: {},
    woundedDays: 0,
    alive: true,
    loadout: { utility: null, ammo: null },
    nickname: '',
    appearance: defaultAppearance(cls, owner),
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

/** Bastion experience a mission's earned XP becomes: +25/40/55 % with simulation, 15 % more next to the Expediente. */
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

/** Days a mission takes off the flight (La Mula's workshop) and off each wound. */
export function garageCut(c: CampaignState): { days: number; wounds: number } {
  const level = facilityLevel(c, 'garage');
  return { days: level >= 3 ? 2 : level ? 1 : 0, wounds: level >= 3 ? 2 : level === 2 ? 1 : 0 };
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
  const used = c.soldiers.filter((s) => s.alive && (s.loadout.utility === id || s.loadout.ammo === id)).length;
  return (c.inventory[id] ?? 0) - used;
}

export function soldierReady(s: CampaignSoldier): boolean {
  return s.alive && s.woundedDays === 0;
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

const OPERATION_A = ['Ceniza', 'Hierro', 'Silencio', 'Tormenta', 'Vigilia', 'Escarcha', 'Brasa', 'Sombra', 'Alba', 'Trueno'];
const OPERATION_B = ['Rota', 'Callada', 'Lejana', 'Roja', 'Blanca', 'Final', 'Hueca', 'Firme', 'Negra', 'Dorada'];

/** Mission types offered by the resistance network, with their relative frequency. */
const OFFER_KINDS: [MissionKind, number][] = [
  ['elimination', 3],
  ['recovery', 2],
  ['hack', 2],
  ['rescue', 2],
  ['sabotage', 1],
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

/** The enemy groups of an operation, with what side quests left in its region. */
function regionPods(c: CampaignState, region: RegionId, difficulty: number): TemplateId[][] {
  const pods = podPlan(difficulty);
  const extra = regionEnemy(c, region);
  if (extra) pods[0]!.push(extra);
  for (let i = 0; i < veteranEnemies(c); i++) pods[(i + 1) % pods.length]!.push(difficulty >= 3 ? 'lancer' : 'trooper');
  return pods;
}

function makeOffer(c: CampaignState, rng: Rng, final = false, at?: RegionId): MissionOffer {
  const difficulty = final ? 4 : difficultyNow(c, rng);
  const kind: MissionKind = final ? 'elimination' : pickKind(rng);
  // Relays live in alien facilities; everything else happens in towns or the countryside.
  const biome: Biome = final || kind === 'sabotage' ? 'facility' : rng.pick(['city', 'city', 'wilds'] as const);
  const contacted = c.regions.filter((r) => r.contacted).map((r) => r.id);
  const region = at ?? (rng.int(10) < 7 && contacted.length ? rng.pick(contacted) : rng.pick(Object.keys(REGIONS) as RegionId[]));
  // Data and relays are worth more to the Council than a firefight.
  const bonus = kind === 'recovery' || kind === 'hack' || kind === 'sabotage' ? 20 : 0;
  const reward: MissionReward = final ? {} : { credits: 50 + difficulty * 25 + bonus + rng.int(4) * 5 };
  if (!final && (kind === 'rescue' || rng.int(5) === 0)) reward.recruit = rng.pick(['assault', 'grenadier', 'sharpshooter', 'specialist'] as const);
  if (!final && (kind === 'sabotage' || (kind === 'hack' && rng.int(3) === 0))) reward.doom = 1;
  return {
    id: newId(c, 'm'),
    name: final ? 'El campamento de la sierra' : `Operación ${rng.pick(OPERATION_A)} ${rng.pick(OPERATION_B)}`,
    kind,
    difficulty,
    expiresDay: final ? null : c.day + 6 + rng.int(4),
    reward,
    map: { kind: 'generated', biome, seed: rng.int(2 ** 31) },
    pods: regionPods(c, region, difficulty),
    final,
    region,
    facility: null,
    story: null,
    quest: null,
  };
}

/** Assault on a Nido (alien facility in the code): silencing its sirena sets the doom track back. */
function makeFacilityOffer(c: CampaignState, region: RegionId, rng: Rng): MissionOffer {
  const difficulty = Math.min(4, difficultyNow(c, rng) + 1);
  return {
    id: newId(c, 'm'),
    name: `Nido de ${REGIONS[region].name}`,
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
  };
}

/** The mission of a story chapter: fixed place and type, and it never expires. */
function makeStoryOffer(c: CampaignState, chapter: number, rng: Rng): MissionOffer {
  const def = STORY[chapter - 1]!;
  // Without Simón inside, the Archive is harder to crack.
  const difficulty = Math.min(4, def.difficulty + (chapter === SIMON_CHAPTER && c.story.simon === 'refused' ? 1 : 0));
  return {
    id: newId(c, 'm'),
    name: def.title,
    kind: def.kind,
    difficulty,
    expiresDay: null,
    reward: { ...def.reward },
    map: { kind: 'generated', biome: def.biome, seed: rng.int(2 ** 31) },
    pods: podPlan(difficulty),
    final: false,
    region: def.region,
    facility: null,
    story: chapter,
    quest: null,
  };
}

/**
 * Opens whatever the story has ready: the next chapter's mission once the
 * Bastion reaches its level, Simón's visit before chapter 5 and, with every
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
      log(c, `Expediente: nueva misión de historia, ${offer.name}.`, 'good');
    }
  }
  if (c.story.done >= STORY.length && hasTech(c, 'signal') && !c.offers.some((o) => o.final)) {
    c.offers.push(makeOffer(c, rng, true));
    log(c, '¡La Mula está lista! La misión final está disponible.', 'good');
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
  item?: string;
  vipName?: string;
  leader?: { template: TemplateId; name: string };
}

export function missionInfo(c: CampaignState, offer: MissionOffer): MissionInfo {
  if (offer.story) {
    return { kicker: `EXPEDIENTE · HOJA ${offer.story}`, item: STORY[offer.story - 1]!.item, vipName: storyVip(offer.story, c.story.simon) };
  }
  const q = questOf(c, offer);
  const step = q && QUESTS[q.id].steps[q.step];
  if (!q || step?.type !== 'mission') return {};
  return {
    kicker: `ENCARGO · ${QUESTS[q.id].title.toUpperCase()}`,
    item: step.item && fill(step.item, q.names),
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
    // Simón is a suspect if he was let in.
    const pool = [...SUSPECTS];
    const a = c.story.simon === 'trusted' ? 'Simón Ferreira' : pool.splice(rng.int(pool.length), 1)[0]!;
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
  const pods = regionPods(c, q.region, difficulty);
  // The named enemy needs a body of its kind on the field.
  if (step.leader && !pods.some((p) => p.includes(step.leader!.template))) pods[0] = [step.leader.template, ...pods[0]!];
  return {
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
  };
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
    const s = addSoldier(c, q.owner ?? neediestPlayer(c), null, rng);
    if (r.name) s.name = fill(r.name, q.names);
    if (r.rank) promoteTo(s, r.rank, r.cls);
    if (r.trait) s.traits.push(r.trait);
    log(c, `${s.name} se une al Bastión (${TEMPLATES[soldierTemplate(s.cls)].name}).`, 'good');
  }
  if (outcome.xp) gainXp(c, outcome.xp, rng);
  if (c.doom >= DOOM_MAX) c.outcome = 'defeat';
}

/** A newcomer who arrives with a rank: the points of every promotion wait to be spent. */
function promoteTo(s: CampaignSoldier, rank: number, cls?: ClassId): void {
  s.rank = rank;
  s.xp = Math.max(s.xp, RANKS[rank]!.xp);
  if (rank >= SPECIALIZE_RANK && !s.cls) specializeSoldier(s, cls ?? leaningClass(s));
}

/** A recruit reaching Capitán takes its class (classes.ts), and the look that goes with it if it had the default one. */
function specializeSoldier(s: CampaignSoldier, cls: ClassId): void {
  const wasDefault = JSON.stringify(s.appearance) === JSON.stringify(defaultAppearance(s.cls, s.owner));
  specialize(s, cls);
  if (wasDefault) s.appearance = defaultAppearance(cls, s.owner);
}

/** Level of the Taller de oficios, which opens the rows of the Oficio (0 = not built). */
export function echoesLevel(c: CampaignState): number {
  return facilityLevel(c, 'echoes');
}

/** Aptitude and talent points the soldier's rank gives and haven't been spent yet. */
export function pointsToSpend(s: CampaignSoldier): { attrs: number; talents: number } {
  return { attrs: attrPointsFree(s), talents: talentPointsFree(s) };
}

/** Starting over costs less with a training centre. */
export function retrainCost(c: CampaignState): number {
  return facilityCount(c, 'training') > 0 ? RETRAIN_COST / 2 : RETRAIN_COST;
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
  const relative = rng.pick(RELATIVES);
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
    log(c, `El rumor de ${name} era cierto: ${QUESTS[quest].title}.`, 'good');
    startQuest(c, quest, rng);
    return;
  }
  const roll = rng.int(100);
  if (roll < 40) {
    const offer = makeOffer(c, rng, false, rumor.region);
    offer.reward.credits = (offer.reward.credits ?? 0) + 25;
    c.offers.push(offer);
    log(c, `El rumor de ${name} era cierto: ${offer.name}, con mejor paga.`, 'good', alert);
  } else if (roll < 60) {
    const credits = 30 + rng.int(4) * 10;
    c.credits += credits;
    log(c, `El rumor de ${name} era un alijo de otros supervivientes: +${credits} suministros.`, 'good', alert);
  } else if (roll < 80) {
    log(c, `El rumor de ${name} era un Saqueador que hablaba demasiado. Lo que sabía vale experiencia.`, 'good', alert);
    gainXp(c, 6 + rng.int(5), rng);
  } else {
    log(c, `El rumor de ${name} era un bulo.`, 'info', alert);
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

export function createCampaign(players: Slot[], rng: Rng): CampaignState {
  const c: CampaignState = {
    version: CAMPAIGN_VERSION,
    day: 1,
    players: [...players],
    credits: 200,
    xp: 0,
    soldiers: [],
    inventory: { fragGrenade: 2, firstAid: 1 },
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
  for (let i = 0; i < 2 * START_KIT; i++) addSoldier(c, players.length > 1 ? players[i % 2]! : players[0]!, null, rng);
  c.offers.push(makeOffer(c, rng), makeOffer(c, rng));
  log(c, 'El Bastión entra en servicio. Elegid la primera misión: el combate hará subir de nivel al Bastión.', 'good');
  refreshNarrative(c, rng);
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
  /** One aptitude point. */
  | { type: 'train'; soldier: string; attr: AttrId }
  /** One talent level. */
  | { type: 'talent'; soldier: string; talent: PerkId }
  /** Gives every aptitude and talent point back, for credits. */
  | { type: 'retrain'; soldier: string }
  | { type: 'buy'; item: ItemId }
  | { type: 'equip'; soldier: string; slot: ItemSlot; item: ItemId | null }
  | { type: 'customize'; soldier: string; name: string; nickname: string; appearance: Appearance }
  | { type: 'advance'; ready: boolean }
  | { type: 'launch'; ready: boolean }
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

function joinSquad(c: CampaignState, soldier: CampaignSoldier, place: { evict?: string }): void {
  if (place.evict) {
    const out = c.soldiers.find((s) => s.id === place.evict)!;
    c.squad = c.squad.filter((id) => id !== place.evict);
    log(c, `${out.name} deja su hueco a ${soldier.name}.`);
  }
  c.squad.push(soldier.id);
}

/**
 * The squad a mission starts with: each connected player's best ready soldiers,
 * one of each class first (a balanced team), then by rank and experience.
 */
export function defaultSquad(c: CampaignState, connected: readonly Slot[]): string[] {
  const limit = squadLimit(c, connected);
  const squad: string[] = [];
  for (const slot of c.players.filter((p) => connected.includes(p))) {
    const ready = c.soldiers.filter((s) => s.owner === slot && soldierReady(s)).sort((a, b) => b.rank - a.rank || b.xp - a.xp);
    const picked: CampaignSoldier[] = [];
    for (const s of ready) if (picked.length < limit && !picked.some((p) => p.cls === s.cls)) picked.push(s);
    for (const s of ready) if (picked.length < limit && !picked.includes(s)) picked.push(s);
    squad.push(...picked.map((s) => s.id));
  }
  // A share left short (wounded, fallen) is filled with the other player's best soldiers left out.
  const spare = c.soldiers
    .filter((s) => connected.includes(s.owner) && c.players.includes(s.owner) && soldierReady(s) && !squad.includes(s.id))
    .sort((a, b) => b.rank - a.rank || b.xp - a.xp);
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
  for (const s of c.soldiers.filter((x) => x.owner !== slot && x.alive && x.rank === 0 && x.missions === 0).reverse()) {
    if (rookies.length >= LATE_KIT) break;
    if (soldierReady(s) && ready(s.owner) - rookies.filter((r) => r.owner === s.owner && soldierReady(r)).length <= KEEP_READY) continue;
    rookies.push(s);
  }
  // A squad being picked is redone for both by whoever calls this (see the server's bringPlayersIn).
  c.squad = c.squad.filter((id) => !rookies.some((s) => s.id === id));
  for (const s of rookies) {
    const wasDefault = JSON.stringify(s.appearance) === JSON.stringify(defaultAppearance(s.cls, s.owner));
    s.owner = slot;
    if (wasDefault) s.appearance = defaultAppearance(s.cls, slot);
    s.loadout = { utility: null, ammo: null };
  }
  const recruits: CampaignSoldier[] = [];
  for (let i = rookies.length; i < LATE_KIT; i++) recruits.push(addSoldier(c, slot, null, rng));
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

  const promotions = mine.filter((s) => {
    const free = pointsToSpend(s);
    return s.alive && free.attrs + free.talents > 0;
  });
  if (promotions.length) {
    steps.push({ view: 'barracks', label: promotions.length === 1 ? `Ascenso pendiente: ${promotions[0]!.name}` : 'Ascensos pendientes', count: promotions.length, tone: 'info', go: { soldier: promotions[0]!.id } });
  }

  if (!mission && shared && c.advanceReady[partner] && !c.advanceReady[slot]) steps.push({ view: 'geoscape', label: 'Tu compañero quiere escanear', tone: 'urgent' });

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

  if (c.regions.filter((r) => r.contacted).length < contactCapacity(c) && canAfford(c, contactCost(c))) steps.push({ view: 'geoscape', label: 'Contacta una región', tone: 'info' });

  if (!mission && !c.offers.length) steps.push({ view: 'geoscape', label: 'Escanea hasta que pase algo', tone: 'info' });

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
      return `escuchar el rumor de ${rumor ? REGIONS[rumor.region].name : '…'}`;
    }
  }
}

function validateAction(c: CampaignState, action: ProposalAction): string | null {
  switch (action.type) {
    case 'chooseMission':
      if (c.activeMission) return 'Ya hay una misión elegida.';
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
    case 'contact': {
      const region = c.regions.find((r) => r.id === action.region);
      if (!region || region.contacted) return 'Ya tenemos contacto con esa región.';
      if (c.regions.filter((r) => r.contacted).length >= contactCapacity(c)) return 'No podemos mantener más contactos: construid un centro de comunicaciones.';
      if (!canAfford(c, contactCost(c))) return 'No hay suministros suficientes.';
      return null;
    }
    case 'event':
      if (!eventDef(c)) return 'No hay ningún acontecimiento pendiente.';
      if (!eventOptionAvailable(c, action.option)) return 'No podemos permitírnoslo.';
      return null;
    case 'investigate':
      if (!c.rumors.some((r) => r.id === action.id)) return 'Ese rumor ya se ha enfriado.';
      if (c.listening === action.id) return 'Ya estamos escuchando ese rumor.';
      return null;
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
        log(c, `Ya podemos quemar el Nido de ${REGIONS[region.id].name}.`, 'good');
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
      log(c, `Escuchamos el rumor de ${REGIONS[rumor.region].name}: faltan ${rumor.daysLeft} días de escucha.`);
      break;
    }
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
  if (result.ok) syncPads(c);
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
        if (!soldierReady(s)) return { ok: false, error: `${s.name} no está en condiciones.` };
        const place = squadPlace(c, slot, ctx.connected);
        if (place.error) return { ok: false, error: place.error };
        joinSquad(c, s, place);
      }
      c.launchReady = [false, false];
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
      const s = addSoldier(c, slot, null, ctx.rng);
      // The training centre sends them in already trained: Soldado at level 2, Cabo at 3.
      const trained = Math.max(0, facilityLevel(c, 'training') - 1);
      if (trained) promoteTo(s, trained);
      log(c, `Reclutado: ${s.name} (${TEMPLATES.rookie.name}${trained ? `, ${RANKS[trained]!.name}` : ''}).`);
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
      s.appearance = normalizeAppearance(cmd.appearance);
      return { ok: true };
    }

    case 'train': {
      const s = c.soldiers.find((x) => x.id === cmd.soldier);
      if (!s || s.owner !== slot || !s.alive) return { ok: false, error: 'Solo puedes instruir a tus soldados.' };
      const blocked = attrBlocker(s, cmd.attr);
      if (blocked) return { ok: false, error: blocked };
      s.attrs[cmd.attr]++;
      return { ok: true };
    }

    case 'talent': {
      const s = c.soldiers.find((x) => x.id === cmd.soldier);
      if (!s || s.owner !== slot || !s.alive) return { ok: false, error: 'Solo puedes instruir a tus soldados.' };
      const blocked = talentBlocker(s, cmd.talent, echoesLevel(c));
      if (blocked) return { ok: false, error: blocked };
      const { specialized } = learnTalent(s, cmd.talent);
      if (s.talents[cmd.talent] === 1) log(c, `${s.name} aprende ${TALENTS[cmd.talent].name}.`, 'good');
      if (specialized) log(c, SPECIALIZE_TEXT[specialized].replace('{name}', s.name), 'good');
      return { ok: true };
    }

    case 'retrain': {
      const s = c.soldiers.find((x) => x.id === cmd.soldier);
      if (!s || s.owner !== slot || !s.alive) return { ok: false, error: 'Solo puedes reentrenar a tus soldados.' };
      const free = pointsToSpend(s);
      if (free.attrs === ATTR_POINTS_PER_RANK * s.rank && free.talents === TALENT_POINTS_PER_RANK * s.rank) return { ok: false, error: 'No hay nada que reentrenar.' };
      const cost = retrainCost(c);
      if (!canAfford(c, cost)) return { ok: false, error: 'No hay suministros suficientes.' };
      pay(c, cost);
      // Back to the centre of the rosa: a recruit with every point of the rank to spend.
      s.cls = null;
      s.attrs = baseAttrs(null);
      s.talents = {};
      log(c, `${s.name} vuelve a la instrucción: sin clase, con aptitudes y talentos por repartir.`);
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
        if (ITEMS[cmd.item].slot !== cmd.slot) return { ok: false, error: 'Ese objeto no va en esa ranura.' };
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

  const heal = healRate(c);
  for (const s of c.soldiers) {
    if (!s.alive || s.woundedDays === 0) continue;
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
      log(c, `La misión ${o.name} ha expirado. El enemigo avanza.`, 'bad');
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
    log(c, `El rumor de ${REGIONS[r.region].name} se ha enfriado.`);
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
    log(c, bastionLevel(c) >= PARTITURA_LEVEL ? `El Nido de ${REGIONS[r.id].name} acerca la Marea.` : `El Nido de ${REGIONS[r.id].name} hace crecer ${doomSubject(bastionLevel(c))}.`, 'bad');
    stop = true;
  }

  if (c.day >= c.nextFacilityDay) {
    c.nextFacilityDay = c.day + 20 + rng.int(7);
    const free = c.regions.filter((r) => !r.facility);
    if (c.regions.filter((r) => r.facility).length < MAX_FACILITIES && free.length) {
      const region = rng.pick(free);
      region.facility = { nextDoomDay: c.day + FACILITY_DOOM_INTERVAL };
      log(c, `Los zombis se juntan en un Nido en ${REGIONS[region.id].name}.`, 'bad');
      if (region.contacted) c.offers.push(makeFacilityOffer(c, region.id, rng));
      else log(c, `Contactad con ${REGIONS[region.id].name} para poder asaltarlo.`);
      stop = true;
    }
  }

  if (c.day >= c.nextSupplyDay) {
    c.nextSupplyDay = c.day + MONTH;
    const income = BASE_INCOME + c.regions.filter((r) => r.contacted).reduce((sum, r) => sum + regionIncome(c, r.id), 0);
    c.credits += income;
    log(c, `Llega el convoy de Altomonte: Ruth Halvorsen manda ${income} suministros.`, 'good');
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

/** Everything a soldier brings to a mission: rank, perks, accessories, and the weapon and armour tier they've earned. */
export function squadMember(c: CampaignState, s: CampaignSoldier): SquadMember {
  const gear = [s.loadout.utility, s.loadout.ammo].filter((g): g is ItemId => !!g);
  const base = soldierStats(s, gear);
  let mods = base.mods;
  let maxHp = base.maxHp;
  let armor = base.armor;
  const charges = base.charges;
  const weapon = weaponTier(c, s);
  const suit = armorTier(c, s);
  mods = addMods(mods, { damage: WEAPON_TIER_DAMAGE[weapon - 1]! });
  maxHp += ARMOR_TIERS[suit - 1]!.hp;
  armor += ARMOR_TIERS[suit - 1]!.armor;
  if (hasTech(c, 'plasmaGrenades')) mods = addMods(mods, { grenadeDamage: 2 });
  if (hasTech(c, 'nanoMedkit')) {
    mods = addMods(mods, { heal: 2 });
    if (charges.medkit !== undefined) charges.medkit += 1;
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
    rank: s.rank,
    perks: base.perks,
    mods,
    maxHp,
    hp: maxHp,
    charges,
    will: base.will + traits.reduce((sum, t) => sum + t.will, 0) + memorialWill(c),
    armor,
    gear,
  };
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

/**
 * Adds Bastion experience and announces every level reached. New levels can
 * open the next story chapter and, at the top, the final mission.
 */
export function gainXp(c: CampaignState, amount: number, rng: Rng): void {
  const before = bastionLevel(c);
  c.xp += amount;
  const after = bastionLevel(c);
  for (let level = before + 1; level <= after; level++) log(c, `El Bastión sube al nivel ${level}.`, 'good');
  refreshNarrative(c, rng);
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
  };

  // Abandoning counts as a retreat: whoever is still standing comes home.
  const fell = (u: GameState['units'][number]) => !u.alive && !u.evacuated && u.hp <= 0;
  // The Camposanto at level 3: whoever comes back from a mission with losses learns from it.
  const mourning = facilityLevel(c, 'memorial') >= 3 && mission.units.some((u) => u.campaignId && fell(u)) ? 1 : 0;
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
      cls: s.cls,
      status: 'ok',
      woundedDays: 0,
      kills: u.kills,
      xpGained: 0,
      promotedTo: null,
    };
    report.squad.push(entry);
    if (died) {
      s.alive = false;
      s.fellDay = report.day;
      entry.status = 'dead';
      if (facilityCount(c, 'memorial')) log(c, `${s.name} descansa en el Camposanto.`);
      // Equipment is lost with its bearer.
      for (const item of [s.loadout.utility, s.loadout.ammo]) if (item) c.inventory[item] = Math.max(0, (c.inventory[item] ?? 0) - 1);
      s.loadout = { utility: null, ammo: null };
      continue;
    }
    const lost = u.maxHp - u.hp;
    if (lost > 0) {
      s.woundedDays = Math.max(1, woundDays(lost, s.attrs) - garageCut(c).wounds);
      entry.status = 'wounded';
      entry.woundedDays = s.woundedDays;
    }
    entry.xpGained = 1 + (victory ? 1 : 0) + u.kills + mourning;
    s.xp += entry.xpGained;
    const rank = rankForXp(s.xp);
    if (rank > s.rank) {
      s.rank = rank;
      entry.promotedTo = rank;
      log(c, `${s.name} asciende a ${RANKS[rank]!.name}.`, 'good');
      if (rank >= SPECIALIZE_RANK && !s.cls) {
        specializeSoldier(s, leaningClass(s));
        entry.specializedAs = s.cls!;
        log(c, SPECIALIZE_TEXT[s.cls!].replace('{name}', s.name), 'good');
      }
    }
  }

  // Holding the field means the bodies come home, and the black market pays for them.
  if (victory) {
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
  }

  if (victory && offer) {
    const r = offer.reward;
    c.credits += r.credits ?? 0;
    if (r.doom) {
      c.doom = Math.max(0, c.doom - r.doom);
      report.doomChange = -r.doom;
    }
    if (r.recruit) {
      const s = addSoldier(c, neediestPlayer(c), null, rng);
      log(c, `${s.name} se une al Bastión como recluta.`, 'good');
    }
    if (offer.facility) {
      const region = c.regions.find((x) => x.id === offer.facility);
      if (region) region.facility = null;
      log(c, `Nido de ${REGIONS[offer.facility].name} quemado.`, 'good');
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
      log(c, 'La sirena de La Mula se lleva a la Marea.', 'good');
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
  // The flight back and the recovery. Not after the final mission: the campaign is being decided.
  if (!offer?.final) passDays(c, Math.max(1, MISSION_DAYS + rng.int(2) - garageCut(c).days), rng);
  else c.day++;
  return report;
}

// ---------------------------------------------------------------- migration

/**
 * Brings a campaign saved by an older version up to the current shape. Missing
 * fields get their defaults; offers get a procedural battlefield and a region.
 */
export function migrateCampaign(old: CampaignState): CampaignState {
  const c = JSON.parse(JSON.stringify(old)) as CampaignState;
  const loose = c as Partial<CampaignState>;
  const legacy = c as unknown as LegacyCampaign;
  let seed = c.nextId * 7919 + c.day;
  c.version = CAMPAIGN_VERSION;
  c.offers = c.offers.map((o) => {
    const prev = o as Partial<MissionOffer> & { pods: unknown; reward: MissionReward & LegacyCost };
    return {
      ...o,
      map: prev.map ?? { kind: 'generated', biome: o.final ? 'facility' : 'city', seed: seed++ },
      pods: Array.isArray(prev.pods) ? (prev.pods as TemplateId[][]) : podPlan(o.difficulty),
      region: prev.region ?? HOME_REGION,
      facility: prev.facility ?? null,
      story: prev.story ?? null,
      quest: prev.quest ?? null,
      reward: migrateReward(prev.reward),
    };
  });
  for (const s of c.soldiers) {
    s.loadout ??= { utility: null, ammo: null };
    s.nickname ??= '';
    s.appearance ??= defaultAppearance(s.cls, s.owner);
    s.traits ??= [];
  }
  loose.inventory ??= {};
  loose.base ??= freshBase();
  loose.regions ??= freshRegions();
  loose.nextFacilityDay ??= c.day + 5;
  loose.nextSupplyDay ??= c.day + MONTH;
  loose.nextEventDay ??= c.day + 4;
  loose.event ??= null;

  // Version 4: research and three resources gave way to Bastion levels and credits.
  if (legacy.resources) {
    const r = legacy.resources;
    c.credits = r.supplies + 2 * r.intel + 3 * r.alloys;
    // The Bastion has seen as much combat as its soldiers, and keeps what was already built or researched.
    const built = legacy.upgrades ?? [];
    const done = legacy.research?.done ?? [];
    const kept = Math.max(
      1,
      ...(['magWeapons', 'plateArmor', 'plasmaGrenades', 'nanoMedkit', 'predatorArmor'] as const).filter((id) => built.includes(id)).map((id) => TECHS[id].level),
      done.includes('alienEncryption') ? TECHS.encryption.level : 1,
      done.includes('signal') ? TECHS.signal.level : 1,
    );
    c.xp = Math.max(
      c.soldiers.reduce((sum, s) => sum + s.xp, 0),
      LEVEL_XP[kept - 1]!,
    );
    for (const slot of (c.base as unknown as LegacyBase).slots ?? []) if ((slot.facility as string) === 'lab') slot.facility = 'simulation';
    const legacyWork = (c.base as unknown as LegacyBase).construction;
    if (legacyWork && (legacyWork.facility as string) === 'lab') legacyWork.facility = 'simulation';
    delete legacy.resources;
    delete legacy.research;
    delete legacy.upgrades;
    delete legacy.corpses;
  }
  loose.credits ??= 0;
  loose.xp ??= 0;
  // Version 5: the story. An older campaign picks it up at the chapter of its level
  // (the earlier ones happened off screen); the next scan opens that chapter.
  if (!loose.story) {
    const done = c.offers.some((o) => o.final) ? STORY.length : Math.min(STORY.length, levelForXp(c.xp) - 1);
    c.story = { done, simon: done >= SIMON_CHAPTER ? 'trusted' : null };
  }
  loose.ending ??= null;
  // Version 6: side quests and rumours.
  loose.quests ??= [];
  loose.questLog ??= {};
  loose.questEvent ??= null;
  loose.rumors ??= [];
  loose.listening ??= null;
  loose.nextRumorDay ??= c.day + 3;
  loose.techs ??= [];
  loose.seen ??= {};
  if (c.lastReport) {
    const report = c.lastReport as MissionReport & { corpses?: unknown; reward: (MissionReward & LegacyCost) | null };
    delete report.corpses;
    report.reward = report.reward ? migrateReward(report.reward) : null;
    report.xp ??= 0;
    report.levelFrom ??= levelForXp(c.xp);
    report.levelTo ??= levelForXp(c.xp);
    report.bodies ??= 0;
    report.salvage ??= 0;
    report.story ??= null;
  }
  // Version 7: the base moves to the surface.
  if ((c.base as unknown as LegacyBase).slots) c.base = surfaceBase(c.base as unknown as LegacyBase);
  // Version 8: several works at once (the single `construction` becomes a list).
  const single = c.base as unknown as { construction?: Construction | null; works?: Construction[] };
  if (!single.works) {
    single.works = single.construction ? [single.construction] : [];
    delete single.construction;
  }
  // Versions 9 and 10: aptitudes and the Rosa. The perks of before don't fit it: those soldiers
  // get every talent point of their rank back (version 9 talents stay). Aptitudes start again
  // from the new bases, and everyone keeps the class they had.
  const beforeRosa = (old.version ?? 0) < 10;
  const retrained = c.soldiers.filter((s) => beforeRosa || !s.attrs);
  for (const s of c.soldiers as (CampaignSoldier & { perks?: unknown; pendingTiers?: unknown })[]) {
    if (beforeRosa || !s.attrs) {
      s.talents = s.attrs ? cleanTalents(s.talents) : {};
      s.attrs = baseAttrs(s.cls);
    }
    delete s.perks;
    delete s.pendingTiers;
  }
  if (retrained.some((s) => s.alive && s.rank > 0)) {
    log(c, 'Cada soldado tiene ahora cinco aptitudes y un sitio en la Rosa, el árbol de talentos de todos. Los puntos de sus ascensos esperan en el Cuartel.', 'good', {
      kicker: 'Cuartel',
      title: 'Nueva instrucción',
    });
  }
  // Version 11: the first player's colour goes from blue to violet; soldiers still in the old default follow it.
  for (const s of c.soldiers) {
    if (s.owner === 0 && JSON.stringify(s.appearance) === JSON.stringify(defaultAppearance(s.cls, 0, '#3fa9ff'))) s.appearance = defaultAppearance(s.cls, 0);
  }
  c.launchReady = [false, false];
  return c;
}

/** The underground base, up to version 6: a 3 × 3 grid of rooms dug one under another. */
interface LegacyBase {
  slots: { id: string; row: number; col: number; excavated: boolean; facility: FacilityId | null }[];
  construction: { slot: string; kind: 'excavate' | 'build'; facility: FacilityId | null; daysLeft: number; total: number } | null;
}

/**
 * Moves an underground base to the surface: every facility keeps working on a
 * plot next to the compound (nearest first), and the work in progress carries
 * on with the days it had left. A half-dug room becomes ground being cleared.
 */
function surfaceBase(old: LegacyBase): CampaignState['base'] {
  const c = { base: freshBase() } as CampaignState;
  const place = (facility: FacilityId): BasePlot => {
    for (;;) {
      const free = c.base.plots
        .filter((p) => !p.building && neighbours(c, p).some((q) => q.building))
        .sort((a, b) => plotRing(a) - plotRing(b) || a.y - b.y || a.x - b.x)[0]!;
      free.terrain = 'clear';
      if (!free.building) {
        free.building = facility;
        return free;
      }
    }
  };
  for (const slot of old.slots) if (slot.facility) place(slot.facility);
  const work = old.construction;
  if (work?.kind === 'build' && work.facility) {
    const plot = c.base.plots
      .filter((p) => p.terrain === 'clear' && !p.building && neighbours(c, p).some((q) => q.building))
      .sort((a, b) => plotRing(a) - plotRing(b))[0];
    if (plot) c.base.works = [{ plot: plot.id, kind: 'build', facility: work.facility, daysLeft: work.daysLeft, total: work.total }];
  } else if (work) {
    const plot = c.base.plots.filter((p) => !clearable(c, p)).sort((a, b) => plotRing(a) - plotRing(b))[0];
    if (plot) c.base.works = [{ plot: plot.id, kind: 'clear', facility: null, daysLeft: work.daysLeft, total: work.total }];
  }
  return c.base;
}

/** Shapes of version 3 and older, before credits and Bastion levels. */
interface LegacyCost {
  supplies?: number;
  intel?: number;
  alloys?: number;
}

interface LegacyCampaign {
  resources?: { supplies: number; intel: number; alloys: number };
  research?: { done: string[] };
  upgrades?: string[];
  corpses?: unknown;
}

function migrateReward(r: MissionReward & LegacyCost): MissionReward {
  if (r.credits !== undefined || (r.supplies === undefined && r.intel === undefined && r.alloys === undefined)) return r;
  const { supplies = 0, intel = 0, alloys = 0, ...rest } = r;
  return { ...rest, credits: supplies + 2 * intel + 3 * alloys };
}
