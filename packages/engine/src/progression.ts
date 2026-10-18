// Campaign progression: the Bastion levels up with combat experience and each
// level unlocks technology, accessories and facilities. Weapons and armour are
// never bought: the Bastion's level allows each tier, the Taller makes it
// (CRAFTS, docs/FASE4.md), and a soldier carries the best tier made *and*
// their rank allows, so promotions show up as better guns. A found weapon
// lifts one soldier a tier above the rest.
import { PIECES } from './base';
import { FOUND_WEAPONS, ITEMS, WEAPONS, type WeaponId } from './content';
import type { CampaignSoldier, CampaignState, FacilityId, FoundWeapon } from './campaign';
import type { FoundWeaponId, ItemId, TemplateId, WeaponModId } from './types';

export type TechId = 'magWeapons' | 'plateArmor' | 'plasmaGrenades' | 'encryption' | 'plasmaWeapons' | 'nanoMedkit' | 'predatorArmor' | 'signal';

export interface TechDef {
  id: TechId;
  name: string;
  description: string;
  /** Bastion level that unlocks it. */
  level: number;
}

export const TECHS: Record<TechId, TechDef> = {
  magWeapons: { id: 'magWeapons', name: 'Culatas y silenciadores', description: 'Teo puede hacer culatas y silenciadores en el Taller, con piezas, para montarlos en la Armería.', level: 3 },
  plateArmor: { id: 'plateArmor', name: 'Armadura de placas', description: 'El Taller puede hacer armadura de nivel 2 (+2 de salud), con piezas: para todos.', level: 4 },
  plasmaGrenades: { id: 'plasmaGrenades', name: 'Granadas de clavos', description: 'Clavos y tornillos en la carga: +2 de daño en todas las granadas.', level: 5 },
  encryption: { id: 'encryption', name: 'Escáner de radio', description: 'Oír las frecuencias de los Cuervos sin que nos oigan: +20 a arreglar equipos y una zona contactable más.', level: 5 },
  plasmaWeapons: { id: 'plasmaWeapons', name: 'Miras y cargadores ampliados', description: 'Lo que quedaba en los cuarteles sirve de modelo: el Taller puede hacer miras y cargadores ampliados, con piezas, para montarlos en la Armería.', level: 6 },
  nanoMedkit: { id: 'nanoMedkit', name: 'Antibióticos', description: 'Amoxicilina y suturas de verdad: las curas sanan +2 y los botiquines tienen un uso más.', level: 6 },
  predatorArmor: { id: 'predatorArmor', name: 'Equipo antidisturbios', description: 'El Taller, a nivel 2, puede hacer armadura de nivel 3 (+4 de salud y +1 de blindaje) para todos.', level: 7 },
  signal: { id: 'signal', name: 'La sirena en La Lata', description: 'La sirena de los Cuervos, atornillada al techo de La Lata para llevarse a la Marea: desbloquea la misión final.', level: 8 },
};

/** Bastion experience needed for each level (index 0 is level 1). */
export const LEVEL_XP = [0, 12, 30, 55, 85, 120, 160, 205];
export const MAX_LEVEL = LEVEL_XP.length;

export function levelForXp(xp: number): number {
  let level = 1;
  while (level < MAX_LEVEL && xp >= LEVEL_XP[level]!) level++;
  return level;
}

/**
 * What raising the Bastion to each level asks (Arturo, 18/10 #40: «se va
 * upgradeando a mano, comprando»; «suministros y misiones»): the suministros
 * it costs, and the missions flown before it can be. Index 0 is level 1.
 */
export const LEVEL_COST = [0, 100, 160, 240, 330, 440, 560, 700];
export const LEVEL_MISSIONS = [0, 2, 4, 6, 9, 12, 16, 20];

/** The Bastion's level: bought since campaign version 21 (`level`); from its experience before. */
export function bastionLevel(c: Pick<CampaignState, 'xp' | 'level'>): number {
  return c.level ?? levelForXp(c.xp);
}

/** Unlocked by the Bastion's level, or granted earlier by a side quest (`CampaignState.techs`). */
export function hasTech(c: Pick<CampaignState, 'xp' | 'level'> & { techs?: readonly TechId[] }, id: TechId): boolean {
  return !!c.techs?.includes(id) || bastionLevel(c) >= TECHS[id].level;
}

export function itemUnlocked(c: Pick<CampaignState, 'xp' | 'level'>, id: ItemId): boolean {
  return bastionLevel(c) >= ITEMS[id].level;
}

/**
 * Bastion experience from one mission: showing up, winning (harder is worth
 * more) and every enemy the squad brought down.
 */
export function missionXp(victory: boolean, difficulty: number, kills: number): number {
  return 2 + (victory ? 3 + difficulty : 0) + kills;
}

// ------------------------------------------------------------------ tiers

/** The armour's tiers (ARMOR_TIERS); weapons have none since Fase 9 (docs/FASE9.md §5). */
export type Tier = 1 | 2 | 3;

// ----------------------------------------------------------------- crafts

export type CraftId =
  | 'armor2'
  | 'armor3'
  | 'makeRifle'
  | 'makeShotgun'
  | 'makeSniper'
  | 'makeMachineGun'
  | 'makeMachete'
  | 'ammo1'
  | 'ammo2'
  | 'ammo3'
  | 'makeStock'
  | 'makeSilencer'
  | 'makeScope'
  | 'makeExtMag';

export interface CraftDef {
  id: CraftId;
  name: string;
  /** As the register tells it, with its article. */
  the: string;
  /** An armour tier for everyone, a weapon for the Armería (made again and again), one more spare magazine for everyone (docs/FASE8.md §A), or an improvement for a weapon (docs/FASE9.md §5). */
  kind: 'armor' | 'arms' | 'ammo' | 'mod';
  /** The tier it makes ('armor'). */
  tier?: 2 | 3;
  /** The technology (the Bastion's level) that allows it. */
  tech: TechId | null;
  /** The Bastion's level it needs, when no technology says it. */
  level?: number;
  /** The weapon it makes ('arms'). */
  makes?: FoundWeaponId;
  /** The improvement it makes ('mod'), for the Armería. */
  makesMod?: WeaponModId;
  /** Level of the Taller it needs. */
  workshop: number;
  /** What has to be made before it. */
  after: CraftId | null;
  /** Suministros, parts and days of work (a crew's). */
  cost: number;
  parts: number;
  days: number;
}

/** What the Taller makes (docs/FASE4.md): tiers 2 and 3 of weapons and armour, for everyone. Names are placeholders for Lore. */
export const CRAFTS: Record<CraftId, CraftDef> = {
  armor2: { id: 'armor2', name: 'Armadura de nivel 2', the: 'la armadura de nivel 2', kind: 'armor', tier: 2, tech: 'plateArmor', workshop: 1, after: null, cost: 60, parts: 4, days: 3 },
  armor3: { id: 'armor3', name: 'Armadura de nivel 3', the: 'la armadura de nivel 3', kind: 'armor', tier: 3, tech: 'predatorArmor', workshop: 2, after: 'armor2', cost: 120, parts: 8, days: 5 },
  // The weapons from zero (docs/FASE8.md §A): one at a time, for the Armería.
  makeRifle: { id: 'makeRifle', name: 'Fusil de asalto', the: 'un fusil de asalto', kind: 'arms', makes: 'assaultRifle', tech: null, workshop: 1, after: null, cost: 30, parts: 3, days: 2 },
  makeShotgun: { id: 'makeShotgun', name: 'Escopeta', the: 'una escopeta', kind: 'arms', makes: 'pumpShotgun', tech: null, workshop: 1, after: null, cost: 30, parts: 3, days: 2 },
  makeMachete: { id: 'makeMachete', name: 'Machete', the: 'un machete', kind: 'arms', makes: 'machete', tech: null, level: 2, workshop: 1, after: null, cost: 15, parts: 2, days: 1 },
  makeSniper: { id: 'makeSniper', name: 'Fusil de precisión', the: 'un fusil de precisión', kind: 'arms', makes: 'precisionRifle', tech: null, level: 3, workshop: 1, after: null, cost: 45, parts: 5, days: 3 },
  makeMachineGun: { id: 'makeMachineGun', name: 'Ametralladora', the: 'una ametralladora', kind: 'arms', makes: 'machineGun', tech: null, level: 4, workshop: 1, after: null, cost: 45, parts: 5, days: 3 },
  // Munition: each one more spare magazine for every soldier who sets out (docs/FASE8.md §A).
  ammo1: { id: 'ammo1', name: 'Primeros cargadores', the: 'la primera tanda de cargadores', kind: 'ammo', tech: null, workshop: 1, after: null, cost: 25, parts: 2, days: 2 },
  ammo2: { id: 'ammo2', name: 'Más cargadores', the: 'la segunda tanda de cargadores', kind: 'ammo', tech: null, level: 3, workshop: 1, after: 'ammo1', cost: 40, parts: 4, days: 3 },
  ammo3: { id: 'ammo3', name: 'Cargadores de sobra', the: 'la tercera tanda de cargadores', kind: 'ammo', tech: null, level: 5, workshop: 2, after: 'ammo2', cost: 60, parts: 6, days: 4 },
  // Improvements for the weapons (docs/FASE9.md §5), one at a time: «Armas de taller» and «Armas militares» open them.
  makeStock: { id: 'makeStock', name: 'Culata', the: 'una culata', kind: 'mod', makesMod: 'stock', tech: 'magWeapons', workshop: 1, after: null, cost: 20, parts: 2, days: 1 },
  makeSilencer: { id: 'makeSilencer', name: 'Silenciador', the: 'un silenciador', kind: 'mod', makesMod: 'silencer', tech: 'magWeapons', workshop: 1, after: null, cost: 20, parts: 2, days: 1 },
  makeScope: { id: 'makeScope', name: 'Mira', the: 'una mira', kind: 'mod', makesMod: 'scope', tech: 'plasmaWeapons', workshop: 1, after: null, cost: 30, parts: 3, days: 2 },
  makeExtMag: { id: 'makeExtMag', name: 'Cargador ampliado', the: 'un cargador ampliado', kind: 'mod', makesMod: 'extMag', tech: 'plasmaWeapons', workshop: 1, after: null, cost: 30, parts: 3, days: 2 },
};

/** What the Bastion's level brings to the Armería (docs/FASE8.md §A), by the level reached. */
export const LEVEL_ARMS: Partial<Record<number, FoundWeaponId[]>> = {
  2: ['assaultRifle', 'pumpShotgun'],
  3: ['precisionRifle', 'machete'],
  4: ['machineGun'],
};

/** Spare magazines every soldier sets out with: one per munition the Taller has made. */
export function spareMags(c: Armoury): number {
  return (['ammo1', 'ammo2', 'ammo3'] as const).filter((id) => hasCrafted(c, id)).length;
}

/** What the Armería holds: the Taller's work and the weapons found. */
type Armoury = { crafted?: readonly CraftId[]; foundWeapons?: readonly FoundWeapon[] };

export function hasCrafted(c: Armoury, id: CraftId): boolean {
  return !!c.crafted?.includes(id);
}

/** The project that makes armour of `tier`. */
export function craftFor(tier: 2 | 3): CraftId {
  return tier === 2 ? 'armor2' : 'armor3';
}

export function baseArmorTier(c: Armoury): Tier {
  return hasCrafted(c, 'armor3') ? 3 : hasCrafted(c, 'armor2') ? 2 : 1;
}

/** The Armería's weapon a soldier carries in the hand (docs/FASE8.md §A), if any. */
export function foundWeaponOf(c: Armoury, s: Pick<CampaignSoldier, 'id'>): FoundWeapon | undefined {
  return c.foundWeapons?.find((w) => w.holder === s.id && FOUND_WEAPONS[w.kind].slot === 'primary');
}

/** The machete a soldier carries at the belt, if any. */
export function meleeWeaponOf(c: Armoury, s: Pick<CampaignSoldier, 'id'>): FoundWeapon | undefined {
  return c.foundWeapons?.find((w) => w.holder === s.id && FOUND_WEAPONS[w.kind].slot === 'melee');
}

/** Weapons have no tiers (docs/FASE9.md §5): every weapon is what it is. The client still draws by it. */
export function weaponTier(_c?: Armoury, _s?: Pick<CampaignSoldier, 'id'>): Tier {
  return 1;
}

/** The armour the Taller has made is for everyone (docs/FASE9.md §5). */
export function armorTier(c: Armoury, _s?: Pick<CampaignSoldier, 'id'>): Tier {
  return baseArmorTier(c);
}

export const ARMOR_TIERS = [
  { name: 'Ropa reforzada', hp: 0, armor: 0 },
  { name: 'Armadura de placas', hp: 2, armor: 0 },
  { name: 'Equipo antidisturbios', hp: 4, armor: 1 },
];

/** A weapon by its name (the tier is from before Fase 9 and changes nothing). */
export function weaponName(id: WeaponId, _tier = 1): string {
  return WEAPONS[id].name;
}

/**
 * What stands between a soldier and the next tier, or null at the top: the
 * Taller's work (and the Bastion's level that allows it). Only armour has
 * tiers now (docs/FASE9.md §5).
 */
export function nextTierBlocker(c: Pick<CampaignState, 'xp' | 'level'> & Armoury & { techs?: readonly TechId[] }, _s: Pick<CampaignSoldier, 'id'>, kind: 'weapon' | 'armor'): string | null {
  // No weapon tiers (docs/FASE9.md §5); the armour, for everyone.
  if (kind === 'weapon') return null;
  const tier = armorTier(c);
  if (tier >= 3) return null;
  const next = (tier + 1) as 2 | 3;
  const needs: string[] = [];
  const craft = CRAFTS[craftFor(next)];
  if (craft && !hasCrafted(c, craft.id)) {
    if (craft.tech && !hasTech(c, craft.tech)) needs.push(`nivel ${TECHS[craft.tech].level} del Bastión`);
    needs.push('fabricarlas en el Taller');
  }
  return `${craft?.name ?? `Nivel ${next}`}: requiere ${needs.join(' y ')}`;
}

/** Bastion level that lets a facility unlocked at `base` rise to level `to` (2 or 3). */
export function upgradeLevel(base: number, to: number): number {
  return Math.min(8, base + (to <= 2 ? 2 : 4));
}

/** What the base can build (docs/FASE12.md §11), by the Bastion level that opens it: every piece but the caravan; `building`, it rises levels. */
export const BASE_UNLOCKS: Record<string, { name: string; level: number; building: boolean }> = Object.fromEntries(
  Object.values(PIECES)
    .filter((p) => p.id !== 'caravan')
    .map((p) => [p.id, { name: p.name, level: p.level, building: p.kind === 'building' }]),
);

/** Everything a level brings, for the progression screen and level-up alerts: by default, the new base's pieces. */
export function levelUnlocks(level: number, facilities: Record<string, { name: string; level: number; building?: boolean }> = BASE_UNLOCKS): string[] {
  return [
    ...Object.values(TECHS).filter((t) => t.level === level).map((t) => t.name),
    ...Object.values(ITEMS).filter((i) => i.level === level).map((i) => i.name),
    ...Object.values(facilities).filter((f) => f.level === level).map((f) => f.name),
    ...Object.values(facilities)
      .filter((f) => f.building !== false)
      .flatMap((f) => [2, 3].filter((to) => upgradeLevel(f.level, to) === level).map((to) => `${f.name}, nivel ${to}`)),
  ];
}
