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
import { defaultAppearance, displayName, isAppearance, NAME_MAX, NICKNAME_MAX } from './appearance';
import { addMods, ITEMS, PERKS, RANKS, rankForXp, SOLDIER_NAMES, TEMPLATES, type PerkTier } from './content';
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
  WEAPON_TIER_DAMAGE,
  weaponTier,
} from './progression';
import { briefingSignature, doomChangeText, doomSubject, missionBriefing, NWOSU_CHAPTER, PARTITURA_LEVEL, SIMON_CHAPTER, STORY, storyVip, type Ending, type SimonChoice } from './lore';
import { fill, QUESTS, RELATIVES, RUMOR_LINES, SUSPECTS, TRAITS, type QuestDecision, type QuestId, type QuestMission, type QuestOutcome, type TraitId } from './quests';
import type { BaseView } from './protocol';
import type { Rng } from './rng';
import { podPlan, soldierStats, type MapSource, type SquadMember } from './setup';
import type { Appearance, Biome, ClassId, EndReason, GameState, ItemId, ItemSlot, MissionKind, PerkId, Slot, TemplateId, Vec2 } from './types';

// --------------------------------------------------------------- facilities

export type FacilityId = 'simulation' | 'workshop' | 'infirmary' | 'training' | 'relay';

export interface FacilityDef {
  id: FacilityId;
  name: string;
  description: string;
  /** Price in credits. */
  cost: number;
  days: number;
  /** Bastion level that unlocks it. */
  level: number;
  /** Only one can be built. */
  unique: boolean;
}

export const FACILITIES: Record<FacilityId, FacilityDef> = {
  workshop: { id: 'workshop', name: 'Taller', description: 'Los accesorios cuestan un 25 % menos.', cost: 85, days: 6, level: 1, unique: true },
  infirmary: { id: 'infirmary', name: 'Enfermería', description: 'Las heridas se curan el doble de rápido.', cost: 60, days: 6, level: 1, unique: true },
  training: { id: 'training', name: 'Centro de entrenamiento', description: 'Cada jugador puede llevar un soldado más a las misiones.', cost: 140, days: 10, level: 2, unique: true },
  simulation: { id: 'simulation', name: 'Sala de simulación', description: 'Las misiones dan un 25 % más de experiencia al Bastión.', cost: 100, days: 8, level: 3, unique: true },
  relay: { id: 'relay', name: 'Centro de comunicaciones', description: '+2 regiones contactables.', cost: 110, days: 8, level: 5, unique: false },
};

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
  research: 'Progreso',
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
  rocks: { name: 'Roca', description: 'Roca viva del Pirineo. Barrenos, carretillas y paciencia.', salvage: 0, days: 1, start: 'Empiezan los barrenos en {parcela}.' },
  forest: { name: 'Bosque', description: 'Pinar cerrado. La madera va a las estufas, que solo se encienden de noche.', salvage: 0, days: 0, start: 'Empieza la tala en {parcela}.' },
  ruins: {
    name: 'Ruinas',
    description: 'Barracones de la vieja estación de 1989. Se desmontan a mano; la chatarra se vende: +15 ₡.',
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
  { facility: 'infirmary', next: 'barracks', text: 'Las heridas curan 3× en vez de 2×. Nadie cruza la nieve con una herida abierta.' },
  { facility: 'workshop', next: 'engineering', text: 'Accesorios −40 % en vez de −25 %. Las piezas pasan por un ventanuco.' },
  { facility: 'simulation', next: 'research', text: '+40 % de experiencia en vez de +25 %. Teo corrige los ejercicios en caliente.' },
  { facility: 'relay', next: 'command', text: '+1 región contactable. La antena de válvulas, pegada a la sala de Morse, llega más lejos.' },
  { facility: 'training', next: 'barracks', text: 'Reclutar cuesta 20 ₡ en vez de 30. Los novatos se forman en casa.' },
];

export interface BasePlot {
  /** "x,y" */
  id: string;
  x: number;
  y: number;
  terrain: Terrain;
  building: BuildingId | null;
}

export interface Construction {
  plot: string;
  kind: 'clear' | 'build';
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
  na: { id: 'na', name: 'Norteamérica', lat: 44, lon: -100, income: 35 },
  sa: { id: 'sa', name: 'Sudamérica', lat: -15, lon: -60, income: 25 },
  weu: { id: 'weu', name: 'Europa Occidental', lat: 45, lon: 2, income: 30 },
  eeu: { id: 'eeu', name: 'Europa del Este', lat: 53, lon: 35, income: 25 },
  afr: { id: 'afr', name: 'África', lat: 4, lon: 21, income: 25 },
  me: { id: 'me', name: 'Oriente Medio', lat: 28, lon: 48, income: 30 },
  eas: { id: 'eas', name: 'Asia Oriental', lat: 35, lon: 115, income: 35 },
  oce: { id: 'oce', name: 'Oceanía', lat: -25, lon: 135, income: 25 },
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
    text: 'Unos contrabandistas han asaltado un tren de teselas de la Armonía y ofrecen lo que sacaron.',
    options: [
      {
        label: 'Comprárselo',
        effect: '−40 créditos; dos granadas extra para la armería',
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
    text: 'Familias huidas de una ciudad afinada piden asilo en el Bastión. Entre ellas hay antiguos soldados.',
    options: [
      {
        label: 'Acogerlas',
        effect: '−30 créditos; se une un recluta',
        available: (c) => c.credits >= 30,
        apply: (c, rng) => {
          credit(c, -30);
          const s = addSoldier(c, neediestPlayer(c), rng.pick(['assault', 'grenadier', 'sharpshooter', 'specialist'] as const), rng);
          log(c, `${s.name} se une al Bastión (${TEMPLATES[s.cls].name}).`, 'good');
        },
      },
      { label: 'Cambiarles provisiones por lo que saben', effect: '−10 créditos, +8 de experiencia del Bastión', apply: (c, rng) => (credit(c, -10), gainXp(c, 8, rng)) },
    ],
  },
  signal: {
    id: 'signal',
    title: 'Transmisión interceptada',
    text: 'Morse ha captado en onda corta una transmisión cifrada de la Armonía.',
    options: [
      { label: 'Descifrarla', effect: '+10 de experiencia del Bastión', apply: (c, rng) => gainXp(c, 10, rng) },
      { label: 'Venderla en el mercado negro', effect: '+30 créditos', apply: (c) => credit(c, 30) },
    ],
  },
  convoy: {
    id: 'convoy',
    title: 'Convoy emboscado',
    text: 'Un convoy de la resistencia ha caído en una emboscada de Recolectores cerca del Bastión.',
    options: [
      {
        label: 'Enviar a alguien a socorrerlo',
        effect: 'Un soldado al azar queda herido 4 días; +60 créditos',
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
    text: 'Un Afinado que ha perdido la nota quiere desertar y ofrece lo que sabe.',
    options: [
      { label: 'Interrogarlo', effect: '+12 de experiencia del Bastión', apply: (c, rng) => gainXp(c, 12, rng) },
      { label: 'Usarlo de cebo en un Órgano', effect: 'El enemigo retrocede (−1)', apply: (c) => (c.doom = Math.max(0, c.doom - 1)) },
    ],
  },
  blackMarket: {
    id: 'blackMarket',
    title: 'Mercado negro',
    text: 'Las Caravanas han llegado al mercado negro con género interesante.',
    options: [
      { label: 'Comprar datos tácticos', effect: '−40 créditos, +15 de experiencia del Bastión', available: (c) => c.credits >= 40, apply: (c, rng) => (credit(c, -40), gainXp(c, 15, rng)) },
      { label: 'Vender teselas sueltas', effect: '+35 créditos', apply: (c) => credit(c, 35) },
    ],
  },
  sabotage: {
    id: 'sabotage',
    title: 'Sabotaje en la base',
    text: 'Alguien ha cortado los cables de las válvulas del generador. Desde dentro.',
    options: [
      { label: 'Repararlo a toda prisa', effect: '−35 créditos', available: (c) => c.credits >= 35, apply: (c) => credit(c, -35) },
      {
        label: 'Repararlo con calma',
        effect: 'Las obras en curso se retrasan 4 días',
        apply: (c) => {
          if (c.base.construction) c.base.construction.daysLeft += 4;
        },
      },
    ],
  },
  scientist: {
    id: 'scientist',
    title: 'Una científica fugada',
    // Kept for campaigns saved with it pending; the story now tells it (chapter 2).
    story: true,
    text: 'Una científica huida de una Imprenta pide protección. Dice que la obligaban a escanear gente.',
    options: [
      {
        label: 'Darle asilo',
        effect: '−20 créditos, +12 de experiencia del Bastión',
        available: (c) => c.credits >= 20,
        apply: (c, rng) => (credit(c, -20), gainXp(c, 12, rng)),
      },
      { label: 'Ayudarla a cruzar la frontera', effect: '+15 créditos', apply: (c) => credit(c, 15) },
    ],
  },
  simon: {
    id: 'simon',
    title: 'Simón Ferreira',
    text: 'Un Afinado que ha perdido la nota llama a la puerta del Bastión. Se llama Simón Ferreira. Dice que sabe abrir un Archivo desde dentro y sacar a los escaneados que duermen en él.',
    story: true,
    options: [
      {
        label: 'Confiar en él',
        effect: 'Simón abrirá el Archivo desde dentro: «Los originales» a la dificultad prevista',
        apply: (c) => (c.story.simon = 'trusted'),
      },
      {
        label: 'Interrogarlo y echarlo',
        effect: '+12 de experiencia del Bastión; «Los originales» sin ayuda, más difícil',
        apply: (c, rng) => {
          c.story.simon = 'refused';
          gainXp(c, 12, rng);
        },
      },
    ],
  },
  finale: {
    id: 'finale',
    title: 'La Contranota',
    text: 'La Contranota suena en el Diapasón y el Coro empieza a cantarse entero, fuera de los chips. Teo tiene la mano en el interruptor de las bobinas. Hay que decidir ahora, con las dos llaves.',
    story: true,
    options: [
      {
        label: 'Romper el Diapasón',
        effect: 'El Coro muere entero. La Tierra queda libre.',
        apply: (c) => ((c.ending = 'break'), (c.outcome = 'victory')),
      },
      {
        label: 'Grabar el Coro en las bobinas',
        effect: 'El Coro sobrevive, preso y sordo, en una caja fuerte del Bastión.',
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
const FACILITY_DOOM_INTERVAL = 14;
const MAX_FACILITIES = 3;
const MAX_OFFERS = 3;
const MONTH = 30;
/** Monthly payment from the Council before region income. */
const BASE_INCOME = 60;
/** Credits for each alien body sold after a won mission. */
const BODY_VALUE: Partial<Record<TemplateId, number>> = { trooper: 4, officer: 8, lancer: 6, xenoid: 8, mec: 12, sectoid: 10 };

export interface Loadout {
  utility: ItemId | null;
  ammo: ItemId | null;
}

export interface CampaignSoldier {
  id: string;
  name: string;
  owner: Slot;
  cls: ClassId;
  xp: number;
  rank: number;
  kills: number;
  missions: number;
  perks: PerkId[];
  /** Ability tiers earned but not chosen yet. */
  pendingTiers: PerkTier[];
  woundedDays: number;
  alive: boolean;
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
  cls: ClassId;
  status: 'ok' | 'wounded' | 'dead';
  woundedDays: number;
  kills: number;
  xpGained: number;
  /** Rank reached when the mission promoted the soldier. */
  promotedTo: number | null;
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
export const CAMPAIGN_VERSION = 7;

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
  base: { plots: BasePlot[]; construction: Construction | null };
  regions: RegionState[];
  offers: MissionOffer[];
  activeMission: string | null;
  squad: string[];
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
}

// ------------------------------------------------------------------ helpers

export function canAfford(c: CampaignState, cost: number): boolean {
  return c.credits >= cost;
}

function pay(c: CampaignState, cost: number): void {
  c.credits -= cost;
}

export function costText(cost: number): string {
  return cost ? `${cost} créditos` : 'Gratis';
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

function addSoldier(c: CampaignState, owner: Slot, cls: ClassId, rng: Rng): CampaignSoldier {
  const s: CampaignSoldier = {
    id: newId(c, 'c'),
    name: soldierName(c, rng),
    owner,
    cls,
    xp: 0,
    rank: 0,
    kills: 0,
    missions: 0,
    perks: [],
    pendingTiers: [],
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

/** The workshop takes a quarter off accessories, 40 % next to the armoury. */
export function shopPrice(c: CampaignState, id: ItemId): number {
  const cost = ITEMS[id].cost;
  if (!facilityCount(c, 'workshop')) return cost;
  return Math.round(cost * (hasAdjacency(c, 'workshop') ? 0.6 : 0.75));
}

/** A recruit costs less with the training centre next to the barracks. */
export function recruitCost(c: CampaignState): number {
  return hasAdjacency(c, 'training') ? 20 : RECRUIT_COST;
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
  return 2 + 2 * facilityCount(c, 'relay') + (hasAdjacency(c, 'relay') ? 1 : 0) + (hasTech(c, 'encryption') ? 1 : 0) + quests;
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
  if (def.unique && (facilityCount(c, id) > 0 || c.base.construction?.facility === id)) return 'Solo se puede construir una.';
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

function difficultyNow(c: CampaignState, rng: Rng): number {
  return Math.min(4, 1 + Math.floor(c.day / 18) + (rng.int(4) === 0 ? 1 : 0));
}

/** The enemy groups of an operation, with what side quests left in its region. */
function regionPods(c: CampaignState, region: RegionId, difficulty: number): TemplateId[][] {
  const pods = podPlan(difficulty);
  const extra = regionEnemy(c, region);
  if (extra) pods[0]!.push(extra);
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
    name: final ? 'Asalto al Diapasón' : `Operación ${rng.pick(OPERATION_A)} ${rng.pick(OPERATION_B)}`,
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

/** Assault on an alien facility (an Órgano): destroying its relay sets the doom track back. */
function makeFacilityOffer(c: CampaignState, region: RegionId, rng: Rng): MissionOffer {
  const difficulty = Math.min(4, difficultyNow(c, rng) + 1);
  return {
    id: newId(c, 'm'),
    name: `Órgano de ${REGIONS[region].name}`,
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
  if (next <= STORY.length && bastionLevel(c) >= next) {
    if (next === SIMON_CHAPTER && !c.story.simon) {
      if (!c.event) {
        c.event = 'simon';
        log(c, 'Alguien llama a la puerta del Bastión.');
      }
    } else if (!c.offers.some((o) => o.story === next)) {
      const offer = makeStoryOffer(c, next, rng);
      c.offers.push(offer);
      log(c, `Expediente CORO: nueva misión de historia, ${offer.name}.`, 'good');
    }
  }
  if (c.story.done >= STORY.length && hasTech(c, 'signal') && !c.offers.some((o) => o.final)) {
    c.offers.push(makeOffer(c, rng, true));
    log(c, '¡Localizado el Diapasón! La misión final está disponible.', 'good');
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
    return { kicker: `EXPEDIENTE CORO · HOJA ${offer.story}`, item: STORY[offer.story - 1]!.item, vipName: storyVip(offer.story, c.story.simon) };
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
  if (outcome.delay && c.base.construction) c.base.construction.daysLeft += outcome.delay;
  const soldier = c.soldiers.find((s) => s.id === q.soldier && s.alive);
  if (outcome.trait && soldier && !soldier.traits.includes(outcome.trait)) soldier.traits.push(outcome.trait);
  if (outcome.recruit) {
    const r = outcome.recruit;
    const s = addSoldier(c, q.owner ?? neediestPlayer(c), r.cls ?? rng.pick(['assault', 'grenadier', 'sharpshooter', 'specialist'] as const), rng);
    if (r.name) s.name = fill(r.name, q.names);
    if (r.rank) promoteTo(s, r.rank);
    if (r.trait) s.traits.push(r.trait);
    log(c, `${s.name} se une al Bastión (${TEMPLATES[s.cls].name}).`, 'good');
  }
  if (outcome.xp) gainXp(c, outcome.xp, rng);
  if (c.doom >= DOOM_MAX) c.outcome = 'defeat';
}

/** A newcomer who arrives with a rank: its ability tiers wait to be chosen. */
function promoteTo(s: CampaignSoldier, rank: number): void {
  s.rank = rank;
  s.xp = Math.max(s.xp, RANKS[rank]!.xp);
  for (let r = 1; r <= rank; r++) {
    const tier = RANKS[r]?.perkTier;
    if (tier) s.pendingTiers.push(tier);
  }
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

/** After a mission, a soldier with a nickname may recognise someone among the Impresos. */
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
    log(c, `El rumor de ${name} era un alijo de la resistencia: +${credits} créditos.`, 'good', alert);
  } else if (roll < 80) {
    log(c, `El rumor de ${name} era un Afinado que hablaba demasiado. Lo que sabía vale experiencia.`, 'good', alert);
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
  return { plots, construction: null };
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
  const kits: ClassId[][] = [
    ['assault', 'sharpshooter', 'specialist', 'grenadier'],
    ['grenadier', 'assault', 'specialist', 'sharpshooter'],
  ];
  if (players.length > 1) {
    players.forEach((slot, i) => kits[i]!.forEach((cls) => addSoldier(c, slot, cls, rng)));
  } else {
    [...kits[0]!, ...kits[1]!].forEach((cls) => addSoldier(c, players[0]!, cls, rng));
  }
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
  | { type: 'contact'; region: RegionId }
  | { type: 'answerEvent'; option: 0 | 1 }
  | { type: 'investigate'; id: string }
  | { type: 'cancelMission' }
  | { type: 'squad'; soldier: string; on: boolean }
  /** `squad`: straight into the squad of the chosen mission (a free place in the hangar). */
  | { type: 'recruit'; cls: ClassId; squad?: boolean }
  | { type: 'promote'; soldier: string; perk: PerkId }
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

/**
 * Brings a player into a campaign started without them. They take over the
 * other commander's untouched rookies (never deployed, so nothing is lost), and
 * fresh recruits make up the rest of a kit. Returns how many soldiers they got.
 */
export function addPlayer(c: CampaignState, slot: Slot, rng: Rng): number {
  if (c.players.includes(slot)) return 0;
  c.players = [...c.players, slot].sort() as Slot[];
  const rookies = c.soldiers
    .filter((s) => s.owner !== slot && s.alive && s.rank === 0 && s.missions === 0)
    .reverse()
    .slice(0, LATE_KIT);
  // A squad being picked is redone for both by whoever calls this (see the server's bringPlayersIn).
  c.squad = c.squad.filter((id) => !rookies.some((s) => s.id === id));
  for (const s of rookies) {
    const wasDefault = JSON.stringify(s.appearance) === JSON.stringify(defaultAppearance(s.cls, s.owner));
    s.owner = slot;
    if (wasDefault) s.appearance = defaultAppearance(s.cls, slot);
    s.loadout = { utility: null, ammo: null };
  }
  const classes: ClassId[] = ['assault', 'grenadier', 'sharpshooter', 'specialist'];
  const given = [...rookies];
  for (let i = rookies.length; i < LATE_KIT; i++) {
    const have = c.soldiers.filter((s) => s.owner === slot && s.alive).map((s) => s.cls);
    given.push(addSoldier(c, slot, classes.find((cls) => !have.includes(cls)) ?? classes[i % classes.length]!, rng));
  }
  // Whoever was already there learns who changed hands.
  const names = given.map((s) => s.name);
  const list = names.length > 1 ? `${names.slice(0, -1).join(', ')} y ${names[names.length - 1]}` : (names[0] ?? '');
  log(c, `Llega el segundo comandante. Pasan a su mando: ${list}.`, 'good', { kicker: 'RELEVO', title: 'Llega el segundo comandante' });
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
  const short =
    ready < squadLimit(c, c.players) && mine.length < soldierCap(c) && canAfford(c, recruitCost(c))
      ? { view: 'barracks' as const, label: `${ready === 0 ? 'Ningún soldado listo' : `Solo ${ready} ${ready === 1 ? 'soldado listo' : 'soldados listos'}`}: recluta (${recruitCost(c)} ₡)`, tone: 'info' as const }
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

  const promotions = mine.filter((s) => s.pendingTiers.length);
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

  if (!c.base.construction) {
    // Something affordable to build on a free plot, else ground to clear next to the compound.
    const options = c.base.plots.flatMap((p) => Object.values(FACILITIES).filter((f) => !buildable(c, p, f.id) && canAfford(c, f.cost)).map((f) => ({ p, f })));
    const best = options.find((o) => adjacencyAt(c, o.p, o.f.id)) ?? options[0];
    const wild = c.base.plots
      .filter((p) => !clearable(c, p) && canAfford(c, clearCost(p).cost))
      .sort((a, b) => plotRing(a) - plotRing(b))[0];
    if (best) steps.push({ view: 'facilities', label: 'Construye junto a la base', tone: 'info', go: { plot: best.p.id } });
    else if (!c.base.plots.some((p) => p.terrain === 'clear' && !p.building) && wild) {
      steps.push({ view: 'facilities', label: 'Despeja terreno para crecer', tone: 'info', go: { plot: wild.id } });
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
      if (c.base.construction) return 'Ya hay una obra en marcha.';
      const blocker = clearable(c, plot);
      if (blocker) return blocker;
      if (!canAfford(c, clearCost(plot).cost)) return 'No hay créditos suficientes.';
      return null;
    }
    case 'build': {
      const plot = plotById(c, action.plot);
      if (!plot) return 'Esa parcela no existe.';
      if (c.base.construction) return 'Ya hay una obra en marcha.';
      const blocker = buildable(c, plot, action.facility);
      if (blocker) return blocker;
      if (!canAfford(c, FACILITIES[action.facility].cost)) return 'No hay créditos suficientes.';
      return null;
    }
    case 'contact': {
      const region = c.regions.find((r) => r.id === action.region);
      if (!region || region.contacted) return 'Ya tenemos contacto con esa región.';
      if (c.regions.filter((r) => r.contacted).length >= contactCapacity(c)) return 'No podemos mantener más contactos: construid un centro de comunicaciones.';
      if (!canAfford(c, contactCost(c))) return 'No hay créditos suficientes.';
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
      const { cost, days } = clearCost(plot);
      pay(c, cost);
      c.base.construction = { plot: plot.id, kind: 'clear', facility: null, daysLeft: days, total: days };
      if (plot.terrain !== 'clear') log(c, TERRAINS[plot.terrain].start.replace('{parcela}', plotName(plot)));
      break;
    }
    case 'build': {
      const plot = plotById(c, action.plot)!;
      const def = FACILITIES[action.facility];
      pay(c, def.cost);
      c.base.construction = { plot: plot.id, kind: 'build', facility: def.id, daysLeft: def.days, total: def.days };
      log(c, `Empiezan las obras: ${def.name}, en ${plotName(plot)}.`);
      break;
    }
    case 'contact': {
      pay(c, contactCost(c));
      const region = c.regions.find((r) => r.id === action.region)!;
      region.contacted = true;
      log(c, `Contacto establecido con ${REGIONS[region.id].name}.`, 'good');
      if (region.facility && !c.offers.some((o) => o.facility === region.id)) {
        c.offers.push(makeFacilityOffer(c, region.id, rng));
        log(c, `Ya podemos asaltar el Órgano de ${REGIONS[region.id].name}.`, 'good');
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
  if (c.outcome) return { ok: false, error: 'La campaña ha terminado.' };
  const { slot } = ctx;

  switch (cmd.type) {
    case 'chooseMission':
      return propose(c, { type: 'chooseMission', id: cmd.id }, ctx);
    case 'clear':
      return propose(c, { type: 'clear', plot: cmd.plot }, ctx);
    case 'build':
      return propose(c, { type: 'build', plot: cmd.plot, facility: cmd.facility }, ctx);
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
      if (!canAfford(c, recruitCost(c))) return { ok: false, error: 'No hay créditos suficientes.' };
      const place = cmd.squad ? (c.activeMission ? squadPlace(c, slot, ctx.connected) : { error: 'Primero elegid una misión.' }) : null;
      if (place?.error) return { ok: false, error: place.error };
      pay(c, recruitCost(c));
      const s = addSoldier(c, slot, cmd.cls, ctx.rng);
      log(c, `Reclutado: ${s.name} (${TEMPLATES[cmd.cls].name}).`);
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
      s.appearance = { ...cmd.appearance };
      return { ok: true };
    }

    case 'promote': {
      const s = c.soldiers.find((x) => x.id === cmd.soldier);
      if (!s || s.owner !== slot) return { ok: false, error: 'Solo puedes ascender a tus soldados.' };
      const perk = PERKS[cmd.perk];
      if (!perk || perk.cls !== s.cls || !s.pendingTiers.includes(perk.tier)) return { ok: false, error: 'Esa habilidad no está disponible.' };
      s.perks.push(perk.id);
      s.pendingTiers = s.pendingTiers.filter((t) => t !== perk.tier);
      log(c, `${s.name} aprende ${perk.name}.`, 'good');
      return { ok: true };
    }

    case 'buy': {
      const def = ITEMS[cmd.item];
      if (!itemUnlocked(c, cmd.item)) return { ok: false, error: `Se desbloquea con el nivel ${def.level} del Bastión.` };
      const cost = shopPrice(c, cmd.item);
      if (!canAfford(c, cost)) return { ok: false, error: 'No hay créditos suficientes.' };
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
    c.day++;
    let stop = false;

    const heal = facilityCount(c, 'infirmary') ? (hasAdjacency(c, 'infirmary') ? 3 : 2) : 1;
    for (const s of c.soldiers) {
      if (!s.alive || s.woundedDays === 0) continue;
      s.woundedDays = Math.max(0, s.woundedDays - heal);
      if (s.woundedDays === 0) log(c, `${s.name} se ha recuperado de sus heridas.`, 'good');
    }

    const work = c.base.construction;
    if (work && --work.daysLeft <= 0) {
      const plot = plotById(c, work.plot)!;
      if (work.kind === 'clear') {
        const { salvage } = clearCost(plot);
        const ruins = plot.terrain === 'ruins';
        plot.terrain = 'clear';
        c.credits += salvage;
        log(c, ruins ? `Ruinas desmontadas en ${plotName(plot)}: +${salvage} ₡ de chatarra.` : `Terreno despejado en ${plotName(plot)}: listo para construir.`, 'good');
      } else {
        plot.building = work.facility;
        const bonus = adjacencyAt(c, plot, work.facility!);
        log(
          c,
          bonus
            ? `${FACILITIES[work.facility!].name} y ${buildingName(bonus.next)}, pared con pared: ${bonus.text.split('.')[0]}.`
            : `Instalación terminada: ${FACILITIES[work.facility!].name}.`,
          'good',
        );
      }
      c.base.construction = null;
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
      if (c.offers.filter((o) => !o.final && !o.facility && !o.story).length < MAX_OFFERS) {
        const offer = makeOffer(c, rng);
        c.offers.push(offer);
        log(c, `Nueva misión en ${REGIONS[offer.region].name}: ${offer.name}.`);
        stop = true;
      }
    }

    if (c.day >= c.nextDoomDay) {
      c.nextDoomDay = c.day + DOOM_INTERVAL;
      c.doom++;
      log(c, doomChangeText(bastionLevel(c), 1), 'bad');
      stop = true;
    }

    for (const r of c.regions) {
      if (!r.facility || c.day < r.facility.nextDoomDay) continue;
      r.facility.nextDoomDay = c.day + FACILITY_DOOM_INTERVAL;
      c.doom++;
      log(c, bastionLevel(c) >= PARTITURA_LEVEL ? `El Órgano de ${REGIONS[r.id].name} escribe un compás.` : `El Órgano de ${REGIONS[r.id].name} hace avanzar ${doomSubject(bastionLevel(c))}.`, 'bad');
      stop = true;
    }

    if (c.day >= c.nextFacilityDay) {
      c.nextFacilityDay = c.day + 20 + rng.int(7);
      const free = c.regions.filter((r) => !r.facility);
      if (c.regions.filter((r) => r.facility).length < MAX_FACILITIES && free.length) {
        const region = rng.pick(free);
        region.facility = { nextDoomDay: c.day + FACILITY_DOOM_INTERVAL };
        log(c, `El Coro levanta un Órgano en ${REGIONS[region.id].name}.`, 'bad');
        if (region.contacted) c.offers.push(makeFacilityOffer(c, region.id, rng));
        else log(c, `Contactad con ${REGIONS[region.id].name} para poder asaltarlo.`);
        stop = true;
      }
    }

    if (c.day >= c.nextSupplyDay) {
      c.nextSupplyDay = c.day + MONTH;
      const income = BASE_INCOME + c.regions.filter((r) => r.contacted).reduce((sum, r) => sum + regionIncome(c, r.id), 0);
      c.credits += income;
      log(c, `Carta del Consejo: la consejera Halvorsen envía ${income} créditos.`, 'good');
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
      log(c, 'La Partitura se ha completado. El Coro canta.', 'bad');
      return;
    }
    if (stop) return;
  }
}

// ------------------------------------------------------------- deployment

/** Everything a soldier brings to a mission: rank, perks, accessories, and the weapon and armour tier they've earned. */
export function squadMember(c: CampaignState, s: CampaignSoldier): SquadMember {
  const gear = [s.loadout.utility, s.loadout.ammo].filter((g): g is ItemId => !!g);
  const base = soldierStats(s.cls, s.rank, s.perks, gear);
  let mods = base.mods;
  let maxHp = base.maxHp;
  let armor = 0;
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
  mods = addMods(mods, { aim: traits.reduce((sum, t) => sum + t.aim, 0) });
  return {
    campaignId: s.id,
    name: s.name,
    nickname: s.nickname,
    appearance: { ...s.appearance },
    weaponTier: weapon,
    armorTier: suit,
    owner: s.owner,
    template: s.cls,
    rank: s.rank,
    perks: [...s.perks],
    mods,
    maxHp,
    hp: maxHp,
    charges,
    will: base.will + traits.reduce((sum, t) => sum + t.will, 0),
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

  for (const u of mission.units) {
    const s = u.campaignId ? c.soldiers.find((x) => x.id === u.campaignId) : undefined;
    if (!s) continue;
    // Abandoning counts as a retreat: whoever is still standing comes home.
    const died = !u.alive && !u.evacuated && u.hp <= 0;
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
      entry.status = 'dead';
      // Equipment is lost with its bearer.
      for (const item of [s.loadout.utility, s.loadout.ammo]) if (item) c.inventory[item] = Math.max(0, (c.inventory[item] ?? 0) - 1);
      s.loadout = { utility: null, ammo: null };
      continue;
    }
    const lost = u.maxHp - u.hp;
    if (lost > 0) {
      s.woundedDays = 2 + lost * 2;
      entry.status = 'wounded';
      entry.woundedDays = s.woundedDays;
    }
    entry.xpGained = 1 + (victory ? 1 : 0) + u.kills;
    s.xp += entry.xpGained;
    const rank = rankForXp(s.xp);
    for (let r = s.rank + 1; r <= rank; r++) {
      const tier = RANKS[r]?.perkTier;
      if (tier) s.pendingTiers.push(tier);
    }
    if (rank > s.rank) {
      s.rank = rank;
      entry.promotedTo = rank;
      log(c, `${s.name} asciende a ${RANKS[rank]!.name}.`, 'good');
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
    if (report.bodies) log(c, `La morgue del Consejo paga ${report.salvage} créditos por ${report.bodies} cuerpos.`, 'good');
  }

  if (victory && offer) {
    const r = offer.reward;
    c.credits += r.credits ?? 0;
    if (r.doom) {
      c.doom = Math.max(0, c.doom - r.doom);
      report.doomChange = -r.doom;
    }
    if (r.recruit) {
      const s = addSoldier(c, neediestPlayer(c), r.recruit, rng);
      log(c, `${s.name} se une al Bastión (${TEMPLATES[r.recruit].name}).`, 'good');
    }
    if (offer.facility) {
      const region = c.regions.find((x) => x.id === offer.facility);
      if (region) region.facility = null;
      log(c, `Órgano de ${REGIONS[offer.facility].name} destruido.`, 'good');
    }
    log(c, `Misión cumplida: ${offer.name}.`, 'good');
    const quest = questOf(c, offer);
    if (quest) advanceQuest(c, quest, rng);
    if (offer.story && offer.story > c.story.done) {
      c.story.done = offer.story;
      log(c, STORY[offer.story - 1]!.after, 'good');
    }
    // The campaign ends when both keys decide what to do with the Coro.
    if (offer.final) {
      c.event = 'finale';
      log(c, 'La Contranota suena en el Diapasón.', 'good');
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
  report.xp = facilityCount(c, 'simulation') ? Math.round(earned * (hasAdjacency(c, 'simulation') ? 1.4 : 1.25)) : earned;
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
  c.day++;
  c.lastReport = report;
  maybeFaces(c, report, rng);
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
  const knownPerks = new Set(Object.keys(PERKS));
  for (const s of c.soldiers) {
    s.loadout ??= { utility: null, ammo: null };
    s.nickname ??= '';
    s.appearance ??= defaultAppearance(s.cls, s.owner);
    s.traits ??= [];
    // Perks that no longer exist are dropped; every tier up to the rank can be chosen again.
    s.perks = s.perks.filter((p) => knownPerks.has(p));
    const chosen = new Set(s.perks.map((p) => PERKS[p].tier));
    s.pendingTiers = [];
    for (let r = 1; r <= s.rank; r++) {
      const tier = RANKS[r]?.perkTier;
      if (tier && !chosen.has(tier)) s.pendingTiers.push(tier);
    }
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
    if (c.base.construction && (c.base.construction.facility as string) === 'lab') c.base.construction.facility = 'simulation';
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
    if (plot) c.base.construction = { plot: plot.id, kind: 'build', facility: work.facility, daysLeft: work.daysLeft, total: work.total };
  } else if (work) {
    const plot = c.base.plots.filter((p) => !clearable(c, p)).sort((a, b) => plotRing(a) - plotRing(b))[0];
    if (plot) c.base.construction = { plot: plot.id, kind: 'clear', facility: null, daysLeft: work.daysLeft, total: work.total };
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
