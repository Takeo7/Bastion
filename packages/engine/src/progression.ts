// Campaign progression: the Bastion levels up with combat experience and each
// level unlocks technology, accessories and facilities. Weapons and armour are
// never bought: the Bastion's level allows each tier, the Taller makes it
// (CRAFTS, docs/FASE4.md), and a soldier carries the best tier made *and*
// their rank allows, so promotions show up as better guns. A found weapon
// lifts one soldier a tier above the rest.
import { FOUND_WEAPONS, ITEMS, RANKS, TEMPLATES, WEAPONS, type WeaponId } from './content';
import type { CampaignSoldier, CampaignState, FacilityId, FoundWeapon } from './campaign';
import type { FoundWeaponId, ItemId, TemplateId } from './types';

export type TechId = 'magWeapons' | 'plateArmor' | 'plasmaGrenades' | 'encryption' | 'plasmaWeapons' | 'nanoMedkit' | 'predatorArmor' | 'signal';

export interface TechDef {
  id: TechId;
  name: string;
  description: string;
  /** Bastion level that unlocks it. */
  level: number;
}

export const TECHS: Record<TechId, TechDef> = {
  magWeapons: { id: 'magWeapons', name: 'Armas de taller', description: 'Teo puede poner a punto armas de nivel 2 (+2 de daño) en el Taller, con piezas: para los soldados desde Cabo.', level: 3 },
  plateArmor: { id: 'plateArmor', name: 'Armadura de placas', description: 'El Taller puede hacer armadura de nivel 2 (+2 de salud), con piezas: para los soldados desde Cabo.', level: 4 },
  plasmaGrenades: { id: 'plasmaGrenades', name: 'Granadas de clavos', description: 'Clavos y tornillos en la carga: +2 de daño en todas las granadas.', level: 5 },
  encryption: { id: 'encryption', name: 'Escáner de radio', description: 'Oír las frecuencias de los Cuervos sin que nos oigan: +20 a arreglar equipos y una zona contactable más.', level: 5 },
  plasmaWeapons: { id: 'plasmaWeapons', name: 'Armas militares', description: 'Lo que quedaba en los cuarteles: el Taller, a nivel 2, puede dejarlas limpias y con munición. Armas de nivel 3 (+4 de daño) para los soldados desde Teniente.', level: 6 },
  nanoMedkit: { id: 'nanoMedkit', name: 'Antibióticos', description: 'Amoxicilina y suturas de verdad: las curas sanan +2 y los botiquines tienen un uso más.', level: 6 },
  predatorArmor: { id: 'predatorArmor', name: 'Equipo antidisturbios', description: 'El Taller, a nivel 2, puede hacer armadura de nivel 3 (+4 de salud y +1 de blindaje) para los soldados desde Teniente.', level: 7 },
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

export function bastionLevel(c: Pick<CampaignState, 'xp'>): number {
  return levelForXp(c.xp);
}

/** Unlocked by the Bastion's level, or granted earlier by a side quest (`CampaignState.techs`). */
export function hasTech(c: Pick<CampaignState, 'xp'> & { techs?: readonly TechId[] }, id: TechId): boolean {
  return !!c.techs?.includes(id) || bastionLevel(c) >= TECHS[id].level;
}

export function itemUnlocked(c: Pick<CampaignState, 'xp'>, id: ItemId): boolean {
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

export type Tier = 1 | 2 | 3;

/** Lowest rank that may carry each tier (Novato, Cabo, Teniente). */
export const TIER_RANK = [0, 2, 4] as const;

export function rankTier(rank: number): Tier {
  return rank >= TIER_RANK[2] ? 3 : rank >= TIER_RANK[1] ? 2 : 1;
}

// ----------------------------------------------------------------- crafts

export type CraftId =
  | 'weapons2'
  | 'weapons3'
  | 'armor2'
  | 'armor3'
  | 'makeRifle'
  | 'makeShotgun'
  | 'makeSniper'
  | 'makeMachineGun'
  | 'makeMachete'
  | 'ammo1'
  | 'ammo2'
  | 'ammo3';

export interface CraftDef {
  id: CraftId;
  name: string;
  /** As the register tells it, with its article. */
  the: string;
  /** A tier for everyone, a weapon for the Armería (made again and again), or one more spare magazine for everyone (docs/FASE8.md §A). */
  kind: 'weapon' | 'armor' | 'arms' | 'ammo';
  /** The tier it makes ('weapon' and 'armor'). */
  tier?: 2 | 3;
  /** The technology (the Bastion's level) that allows it. */
  tech: TechId | null;
  /** The Bastion's level it needs, when no technology says it. */
  level?: number;
  /** The weapon it makes ('arms'). */
  makes?: FoundWeaponId;
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
  weapons2: { id: 'weapons2', name: 'Armas de nivel 2', the: 'las armas de nivel 2', kind: 'weapon', tier: 2, tech: 'magWeapons', workshop: 1, after: null, cost: 60, parts: 4, days: 3 },
  weapons3: { id: 'weapons3', name: 'Armas de nivel 3', the: 'las armas de nivel 3', kind: 'weapon', tier: 3, tech: 'plasmaWeapons', workshop: 2, after: 'weapons2', cost: 120, parts: 8, days: 5 },
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

/** The project of `kind` that makes `tier`. */
export function craftFor(kind: 'weapon' | 'armor', tier: 2 | 3): CraftId {
  return kind === 'weapon' ? (tier === 2 ? 'weapons2' : 'weapons3') : tier === 2 ? 'armor2' : 'armor3';
}

/** The tier the Taller has made for everyone. */
export function baseWeaponTier(c: Armoury): Tier {
  return hasCrafted(c, 'weapons3') ? 3 : hasCrafted(c, 'weapons2') ? 2 : 1;
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

/** A soldier's weapon tier: what the Taller made, one more with a good weapon, never past 3 nor what the rank allows. */
export function weaponTier(c: Armoury, s: Pick<CampaignSoldier, 'rank' | 'id'>): Tier {
  const found = foundWeaponOf(c, s);
  return Math.min(3, baseWeaponTier(c) + (found && FOUND_WEAPONS[found.kind].better ? 1 : 0), rankTier(s.rank)) as Tier;
}

export function armorTier(c: Armoury, s: Pick<CampaignSoldier, 'rank'>): Tier {
  return Math.min(baseArmorTier(c), rankTier(s.rank)) as Tier;
}

/** Extra primary-weapon damage per tier. */
export const WEAPON_TIER_DAMAGE = [0, 2, 4];

export const ARMOR_TIERS = [
  { name: 'Ropa reforzada', hp: 0, armor: 0 },
  { name: 'Armadura de placas', hp: 2, armor: 0 },
  { name: 'Equipo antidisturbios', hp: 4, armor: 1 },
];

const TIER_NAMES: Partial<Record<WeaponId, [string, string]>> = {
  rifle: ['Fusil reforzado', 'Fusil militar'],
  shotgun: ['Escopeta reforzada', 'Escopeta militar'],
  cannon: ['Ametralladora reforzada', 'Ametralladora militar'],
  sniper: ['Fusil de precisión reforzado', 'Fusil de precisión militar'],
};

export function weaponName(id: WeaponId, tier: number = 1): string {
  const names = TIER_NAMES[id];
  return tier > 1 && names ? names[tier - 2]! : WEAPONS[id].name;
}

/** The class weapon a soldier carries at `tier`. */
export function soldierWeaponName(template: TemplateId, tier: number = 1): string {
  return weaponName(TEMPLATES[template].weapon, tier);
}

/**
 * What stands between a soldier and the next tier, or null at the top: the
 * Taller's work (and the Bastion's level that allows it) or the soldier's
 * rank. A found weapon already counts one tier.
 */
export function nextTierBlocker(c: Pick<CampaignState, 'xp'> & Armoury & { techs?: readonly TechId[] }, s: Pick<CampaignSoldier, 'rank' | 'id'>, kind: 'weapon' | 'armor'): string | null {
  const tier = kind === 'weapon' ? weaponTier(c, s) : armorTier(c, s);
  if (tier >= 3) return null;
  const next = (tier + 1) as 2 | 3;
  const made = next - (kind === 'weapon' && foundWeaponOf(c, s) ? 1 : 0);
  const needs: string[] = [];
  const craft = made >= 2 ? CRAFTS[craftFor(kind, made as 2 | 3)] : null;
  if (craft && !hasCrafted(c, craft.id)) {
    if (craft.tech && !hasTech(c, craft.tech)) needs.push(`nivel ${TECHS[craft.tech].level} del Bastión`);
    needs.push('fabricarlas en el Taller');
  }
  if (rankTier(s.rank) < next) needs.push(`rango ${RANKS[TIER_RANK[next - 1]]!.name}`);
  return `${craft?.name ?? `Nivel ${next}`}: requiere ${needs.join(' y ')}`;
}

/** Bastion level that lets a facility unlocked at `base` rise to level `to` (2 or 3). */
export function upgradeLevel(base: number, to: number): number {
  return Math.min(8, base + (to <= 2 ? 2 : 4));
}

/** Everything a level brings, for the progression screen and level-up alerts. */
export function levelUnlocks(level: number, facilities: Record<FacilityId, { name: string; level: number }>): string[] {
  return [
    ...Object.values(TECHS).filter((t) => t.level === level).map((t) => t.name),
    ...Object.values(ITEMS).filter((i) => i.level === level).map((i) => i.name),
    ...Object.values(facilities).filter((f) => f.level === level).map((f) => f.name),
    ...Object.values(facilities).flatMap((f) => [2, 3].filter((to) => upgradeLevel(f.level, to) === level).map((to) => `${f.name}, nivel ${to}`)),
  ];
}
