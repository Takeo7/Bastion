// Campaign progression: the Bastion levels up with combat experience and each
// level unlocks technology, accessories and facilities. Weapons and armour are
// never bought: a soldier carries the best tier the Bastion has unlocked *and*
// their rank allows, so promotions show up as better guns.
import { ITEMS, RANKS, TEMPLATES, WEAPONS, type WeaponId } from './content';
import type { CampaignSoldier, CampaignState, FacilityId } from './campaign';
import type { ItemId, TemplateId } from './types';

export type TechId = 'magWeapons' | 'plateArmor' | 'plasmaGrenades' | 'encryption' | 'plasmaWeapons' | 'nanoMedkit' | 'predatorArmor' | 'signal';

export interface TechDef {
  id: TechId;
  name: string;
  description: string;
  /** Bastion level that unlocks it. */
  level: number;
}

export const TECHS: Record<TechId, TechDef> = {
  magWeapons: { id: 'magWeapons', name: 'Armas de bobina', description: 'Condensadores y cobre, sin un solo chip: armas de nivel 2 (+2 de daño) para los soldados desde Cabo.', level: 3 },
  plateArmor: { id: 'plateArmor', name: 'Armadura de placas', description: 'Armadura de nivel 2 (+2 de salud) para los soldados desde Cabo.', level: 4 },
  plasmaGrenades: { id: 'plasmaGrenades', name: 'Granadas de tesela', description: 'Teselas inestables en la carga: +2 de daño en todas las granadas.', level: 5 },
  encryption: { id: 'encryption', name: 'Oído', description: 'Escuchar a la Armonía sin que nos oiga: +20 a hackear y una región contactable más.', level: 5 },
  plasmaWeapons: { id: 'plasmaWeapons', name: 'Armas de plasma', description: 'Tecnología de Heraldo ensordecida: armas de nivel 3 (+4 de daño) para los soldados desde Teniente.', level: 6 },
  nanoMedkit: { id: 'nanoMedkit', name: 'Teselas médicas', description: 'Tejido impreso sin el Coro dentro: las curas sanan +2 y los botiquines tienen un uso más.', level: 6 },
  predatorArmor: { id: 'predatorArmor', name: 'Armadura de tesela', description: 'Armadura de nivel 3 (+4 de salud y +1 de blindaje) para los soldados desde Teniente.', level: 7 },
  signal: { id: 'signal', name: 'Las cintas de 1989', description: 'Localizan el Diapasón: desbloquea la misión final.', level: 8 },
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
 * more) and every alien the squad brought down.
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

export function baseWeaponTier(c: Pick<CampaignState, 'xp'>): Tier {
  return hasTech(c, 'plasmaWeapons') ? 3 : hasTech(c, 'magWeapons') ? 2 : 1;
}

export function baseArmorTier(c: Pick<CampaignState, 'xp'>): Tier {
  return hasTech(c, 'predatorArmor') ? 3 : hasTech(c, 'plateArmor') ? 2 : 1;
}

export function weaponTier(c: Pick<CampaignState, 'xp'>, s: Pick<CampaignSoldier, 'rank'>): Tier {
  return Math.min(baseWeaponTier(c), rankTier(s.rank)) as Tier;
}

export function armorTier(c: Pick<CampaignState, 'xp'>, s: Pick<CampaignSoldier, 'rank'>): Tier {
  return Math.min(baseArmorTier(c), rankTier(s.rank)) as Tier;
}

/** Extra primary-weapon damage per tier. */
export const WEAPON_TIER_DAMAGE = [0, 2, 4];

export const ARMOR_TIERS = [
  { name: 'Kevlar', hp: 0, armor: 0 },
  { name: 'Armadura de placas', hp: 2, armor: 0 },
  { name: 'Armadura de tesela', hp: 4, armor: 1 },
];

const TIER_NAMES: Partial<Record<WeaponId, [string, string]>> = {
  rifle: ['Fusil de bobina', 'Fusil de plasma'],
  shotgun: ['Escopeta de bobina', 'Escopeta de plasma'],
  cannon: ['Cañón de bobina', 'Cañón de plasma'],
  sniper: ['Fusil de precisión de bobina', 'Fusil de precisión de plasma'],
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
 * What stands between a soldier and the next tier, or null at the top:
 * the Bastion's technology or the soldier's rank.
 */
export function nextTierBlocker(c: Pick<CampaignState, 'xp'>, s: Pick<CampaignSoldier, 'rank'>, kind: 'weapon' | 'armor'): string | null {
  const tier = kind === 'weapon' ? weaponTier(c, s) : armorTier(c, s);
  if (tier >= 3) return null;
  const next = (tier + 1) as 2 | 3;
  const tech: TechId = kind === 'weapon' ? (next === 2 ? 'magWeapons' : 'plasmaWeapons') : next === 2 ? 'plateArmor' : 'predatorArmor';
  const needs: string[] = [];
  if (!hasTech(c, tech)) needs.push(`nivel ${TECHS[tech].level} del Bastión`);
  if (rankTier(s.rank) < next) needs.push(`rango ${RANKS[TIER_RANK[next - 1]]!.name}`);
  return `${TECHS[tech].name}: requiere ${needs.join(' y ')}`;
}

/** Everything a level brings, for the progression screen and level-up alerts. */
export function levelUnlocks(level: number, facilities: Record<FacilityId, { name: string; level: number }>): string[] {
  return [
    ...Object.values(TECHS).filter((t) => t.level === level).map((t) => t.name),
    ...Object.values(ITEMS).filter((i) => i.level === level).map((i) => i.name),
    ...Object.values(facilities).filter((f) => f.level === level).map((f) => f.name),
  ];
}
