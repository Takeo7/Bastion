// Fase 9, «Se aprende haciendo» (docs/FASE9.md). Four skills that rise with
// use, never with points: what each soldier does in a mission is counted
// (Unit.deeds, filled in by the events) and, if it comes back alive, turned
// into experience, all of it: no cap (Arturo, 16/10), long levels instead. At levels 2 and 4 of every
// skill the soldier picks one talent of a pair. The sum of the four levels is
// its veterancy: health, will and, from Veterano, a third action.
//
// The names and lines are Lore's (docs/FASE9.md, «Textos»).
import { addMods, ITEMS, TEMPLATES, ZERO_MODS } from './content';
import type { Rng } from './rng';
import type { AbilityId, Arms, ChargeId, ItemId, PerkId, TemplateId, UnitMods } from './types';

// ------------------------------------------------------------------ skills

export type SkillId = 'shooting' | 'melee' | 'medicine' | 'craft';
export const SKILL_IDS: SkillId[] = ['shooting', 'melee', 'medicine', 'craft'];

/** Experience in each skill (the level follows from it: skillLevel). */
export type Skills = Record<SkillId, number>;
export const NO_SKILLS: Skills = { shooting: 0, melee: 0, medicine: 0, craft: 0 };

export const SKILL_MAX = 5;
/**
 * Experience it takes to reach levels 1 to 5 (Arturo, 16/10: long levels, no
 * cap). A soldier who fights earns some 5 of Tiro a mission (a bot's 24
 * missions: median 5, one in four 8 or more): level 2 after about five, the
 * top after twenty-odd; a star, twice as fast.
 */
export const SKILL_XP = [10, 25, 50, 85, 130] as const;

export interface SkillDef {
  id: SkillId;
  name: string;
  short: string;
  /** What it is and how it rises. */
  line: string;
  /** What every level gives, and the extras. */
  gives: string;
}

export const SKILLS: Record<SkillId, SkillDef> = {
  shooting: { id: 'shooting', name: 'Tiro', short: 'TIR', line: 'Puntería con armas de fuego. Sube acertando y abatiendo con ellas.', gives: '+5 de puntería con armas de fuego por nivel. En el nivel 3, +10 de crítico con ellas.' },
  melee: { id: 'melee', name: 'Cuerpo a cuerpo', short: 'CaC', line: 'El cuchillo y el machete. Sube acertando y abatiendo de cerca.', gives: '+8 de puntería cuerpo a cuerpo por nivel. En los niveles 3 y 5, +1 de daño.' },
  medicine: { id: 'medicine', name: 'Medicina', short: 'MED', line: 'Curar a otros y a uno mismo. Sube curando y parando contagios; más si es a otro.', gives: 'En los niveles 2 y 4, las curas sanan +1. En el 5, el botiquín tiene un uso más.' },
  craft: { id: 'craft', name: 'Maña', short: 'MAÑ', line: 'Arreglar, forzar y los explosivos. Sube registrando, abriendo cajas, arreglando, apagando alarmas y con las granadas que alcanzan a alguien.', gives: '+10 a arreglar y forzar por nivel. En el nivel 2, una granada más; en el 4, +1 de daño de los explosivos.' },
};

export function skillLevel(xp: number): number {
  let level = 0;
  while (level < SKILL_MAX && xp >= SKILL_XP[level]!) level++;
  return level;
}

/** Experience the next level asks for; null at the top. */
export function nextLevelXp(xp: number): number | null {
  const level = skillLevel(xp);
  return level >= SKILL_MAX ? null : SKILL_XP[level]!;
}

export function skillLevels(skills: Skills): Record<SkillId, number> {
  return { shooting: skillLevel(skills.shooting), melee: skillLevel(skills.melee), medicine: skillLevel(skills.medicine), craft: skillLevel(skills.craft) };
}

/**
 * What the levels give in a fight. Grenades and the medkit's extra use only
 * count for whoever carries them (`grenades`, `medkit`).
 */
export function skillMods(skills: Skills, carries: { grenades?: boolean; medkit?: boolean } = {}): { mods: Partial<UnitMods>; charges: Partial<Record<ChargeId, number>> } {
  const l = skillLevels(skills);
  const steps = (level: number, at: number[]) => at.filter((n) => level >= n).length;
  const mods: Partial<UnitMods> = {
    gunAim: 5 * l.shooting,
    gunCrit: l.shooting >= 3 ? 10 : 0,
    meleeAim: 8 * l.melee,
    meleeDamage: steps(l.melee, [3, 5]),
    heal: steps(l.medicine, [2, 4]),
    hack: 10 * l.craft,
    grenadeDamage: l.craft >= 4 ? 1 : 0,
  };
  const charges: Partial<Record<ChargeId, number>> = {};
  if (carries.grenades && l.craft >= 2) charges.grenade = 1;
  if (carries.medkit && l.medicine >= 5) charges.firstAid = 1;
  return { mods, charges };
}

// ----------------------------------------------------------------- talents

export interface SkillTalent {
  id: PerkId;
  skill: SkillId;
  /** The level of the skill it opens at: one of each pair. */
  level: 2 | 4;
  name: string;
  line: string;
  mods?: Partial<UnitMods>;
  ability?: AbilityId;
  /** Uses per mission, for the ones that have them. */
  charges?: Partial<Record<ChargeId, number>>;
}

const talent = (id: PerkId, skill: SkillId, level: 2 | 4, name: string, line: string, extra: Partial<SkillTalent> = {}): SkillTalent => ({ id, skill, level, name, line, ...extra });

export const SKILL_TALENTS: Partial<Record<PerkId, SkillTalent>> = {
  aimedShot: talent('aimedShot', 'shooting', 2, 'A la cabeza', 'Un disparo con −25 de puntería que hace la mitad más de daño. Enfriamiento 3.', { ability: 'aimedShot' }),
  rapidFire: talent('rapidFire', 'shooting', 2, 'Ráfaga', 'Dos disparos seguidos, cada uno con −15 de puntería. Gasta dos balas. Enfriamiento 3.', { ability: 'rapidFire' }),
  killZone: talent('killZone', 'shooting', 4, 'Nadie pasa', 'Vigilancia que dispara a cada enemigo que se mueva a la vista, una vez a cada uno. Enfriamiento 4.', { ability: 'killZone' }),
  faceoff: talent('faceoff', 'shooting', 4, 'Cara a cara', 'Un disparo de pistola a cada enemigo a la vista. Enfriamiento 4.', { ability: 'faceoff' }),
  implacable: talent('implacable', 'melee', 2, 'Uno tras otro', 'Abatir con el Tajo devuelve una acción, una vez por turno.'),
  bladeCrit: talent('bladeCrit', 'melee', 2, 'Golpe certero', '+20 de crítico con el Tajo.', { mods: { meleeCrit: 20 } }),
  bladeMaster: talent('bladeMaster', 'melee', 4, 'Mano de machete', 'Con el machete, el Tajo hace +2 de daño y gana +15 de puntería.'),
  elbowRoom: talent('elbowRoom', 'melee', 4, 'Hacerse sitio', 'Un Tajo a cada enemigo que tenga al lado. Enfriamiento 3.', { ability: 'elbowRoom' }),
  fieldMedic: talent('fieldMedic', 'medicine', 2, 'Buen pulso', 'El botiquín y las medicinas curan +1.', { mods: { heal: 1 } }),
  revivalProtocol: talent('revivalProtocol', 'medicine', 2, 'Espabilar', 'Con una acción, quita a un compañero de al lado el aturdimiento, la desorientación o el pánico. Sin botiquín.', { ability: 'revival' }),
  lightHands: talent('lightHands', 'medicine', 4, 'Manos de enfermero', 'Las medicinas también paran el contagio de un mordido.'),
  restoration: talent('restoration', 'medicine', 4, 'Pasar visita', 'Cura 2 a cada compañero a 4 casillas o menos y les quita los estados alterados. Una vez por misión.', { ability: 'restoration', charges: { restoration: 1 } }),
  goodArm: talent('goodArm', 'craft', 2, 'Buen brazo', 'Las granadas llegan 3 casillas más lejos, y lleva una más en cada accesorio de granadas.', { mods: { grenadeRange: 3 } }),
  quiet: talent('quiet', 'craft', 2, 'Sin hacer ruido', 'Registrar, abrir cajas y forzar no hacen ruido.', { mods: { quiet: 1 } }),
  demolition: talent('demolition', 'craft', 4, 'Demolición', 'Los explosivos hacen +1 de daño y su radio crece una casilla.', { mods: { grenadeDamage: 1, grenadeRadius: 1 } }),
  patchUp: talent('patchUp', 'craft', 4, 'Apaños', 'Una vez por misión, con una acción, arregla 40 de estado del arma propia o de un compañero de al lado.', { ability: 'patchUp', charges: { patchUp: 1 } }),
};

/** The pairs of every skill, at levels 2 and 4 (one of each is picked). */
export const TALENT_PAIRS: Record<SkillId, [[PerkId, PerkId], [PerkId, PerkId]]> = {
  shooting: [['aimedShot', 'rapidFire'], ['killZone', 'faceoff']],
  melee: [['implacable', 'bladeCrit'], ['bladeMaster', 'elbowRoom']],
  medicine: [['fieldMedic', 'revivalProtocol'], ['lightHands', 'restoration']],
  craft: [['goodArm', 'quiet'], ['demolition', 'patchUp']],
};

/** The pair a talent belongs to, with the other one. */
export function pairOf(id: PerkId): { skill: SkillId; level: 2 | 4; pair: [PerkId, PerkId] } | null {
  const def = SKILL_TALENTS[id];
  if (!def) return null;
  return { skill: def.skill, level: def.level, pair: TALENT_PAIRS[def.skill][def.level === 2 ? 0 : 1] };
}

/** The pairs open to a soldier's levels and still without a pick: the choices waiting for it. */
export function pendingPicks(skills: Skills, picks: PerkId[]): { skill: SkillId; level: 2 | 4; pair: [PerkId, PerkId] }[] {
  const out: { skill: SkillId; level: 2 | 4; pair: [PerkId, PerkId] }[] = [];
  for (const skill of SKILL_IDS) {
    const level = skillLevel(skills[skill]);
    TALENT_PAIRS[skill].forEach((pair, k) => {
      const at = k === 0 ? 2 : 4;
      if (level >= at && !pair.some((id) => picks.includes(id))) out.push({ skill, level: at, pair });
    });
  }
  return out;
}

/**
 * Why a soldier with these skills can't hold this list of talents (what
 * «Guardar» sends), or null if it can: each one a talent of the skills, its
 * level reached, and one of each pair at most.
 */
export function picksBlocker(skills: Skills, picks: PerkId[]): string | null {
  const seen = new Set<string>();
  for (const id of picks) {
    const where = pairOf(id);
    if (!where) return 'Ese talento no existe.';
    const key = `${where.skill}${where.level}`;
    if (seen.has(key)) return `Solo uno de cada pareja: ${SKILL_TALENTS[where.pair[0]]!.name} o ${SKILL_TALENTS[where.pair[1]]!.name}.`;
    seen.add(key);
    if (skillLevel(skills[where.skill]) < where.level) return `${SKILL_TALENTS[id]!.name} pide ${SKILLS[where.skill].name} ${where.level}.`;
  }
  return null;
}

// --------------------------------------------------------------- veterancy

export interface VeterancyDef {
  name: string;
  /** For a woman: Novata, Fogueada… (Lore). */
  female: string;
  /** Sum of the four levels it starts at. */
  from: number;
  line: string;
}

export const VETERANCY: VeterancyDef[] = [
  { name: 'Novato', female: 'Novata', from: 0, line: 'Todavía no ha visto lo peor.' },
  { name: 'Fogueado', female: 'Fogueada', from: 3, line: 'Ya ha salido y ha vuelto.' },
  { name: 'Curtido', female: 'Curtida', from: 6, line: 'Sabe cuándo no disparar.' },
  { name: 'Veterano', female: 'Veterana', from: 10, line: 'Tercera acción por turno. Los demás le siguen sin que lo pida.' },
  { name: 'Superviviente', female: 'Superviviente', from: 14, line: 'Ha vuelto de todo.' },
];

/** From Veterano on, a third action per turn. */
export const THIRD_ACTION_AT = 10;
export const BASE_HP = 5;
/** +1 health every this many points of veterancy. */
export const HP_EVERY = 4;
export const BASE_WILL = 40;
export const WILL_PER_POINT = 3;
/** Aim of a soldier with no Tiro and no Cuerpo a cuerpo, before its weapon. */
export const BASE_AIM = 60;

/** The sum of the four levels, 0 to 20. */
export function veterancy(skills: Skills): number {
  return SKILL_IDS.reduce((sum, id) => sum + skillLevel(skills[id]), 0);
}

/** VETERANCY's index for a veterancy. */
export function veterancyRank(points: number): number {
  let rank = 0;
  while (rank + 1 < VETERANCY.length && points >= VETERANCY[rank + 1]!.from) rank++;
  return rank;
}

export const veteranHp = (points: number) => BASE_HP + Math.floor(points / HP_EVERY);
export const veteranWill = (points: number) => BASE_WILL + WILL_PER_POINT * points;
export const veteranAp = (points: number) => (points >= THIRD_ACTION_AT ? 3 : 2);

// ------------------------------------------------------------------- stats

/** Everything a soldier fights with, from its skills, its talents and what it carries. */
export interface FighterStats {
  template: TemplateId;
  mods: UnitMods;
  maxHp: number;
  charges: Partial<Record<ChargeId, number>>;
  will: number;
  armor: number;
  perks: PerkId[];
  /** VETERANCY's index (Unit.rank). */
  rank: number;
}

/**
 * A soldier's numbers (docs/FASE9.md §1–§4). Everyone fights as the same
 * template; grenades, the medkit and the rest come only from the belt (`gear`).
 */
export function fighterStats(skills: Skills, picks: PerkId[], gear: ItemId[] = [], arms?: Arms): FighterStats {
  const tpl = TEMPLATES.rookie;
  const points = veterancy(skills);
  const grenades = gear.filter((id) => ITEMS[id].charges?.grenade).length;
  const skill = skillMods(skills, { grenades: grenades > 0, medkit: gear.includes('firstAid') });
  let mods = addMods(addMods({ ...ZERO_MODS }, { aim: BASE_AIM - tpl.aim }), skill.mods);
  let maxHp = veteranHp(points);
  const charges: Partial<Record<ChargeId, number>> = {};
  const add = (extra: Partial<Record<ChargeId, number>> | undefined) => {
    for (const [c, n] of Object.entries(extra ?? {}) as [ChargeId, number][]) if (n) charges[c] = (charges[c] ?? 0) + n;
  };
  add(skill.charges);
  for (const id of picks) {
    const d = SKILL_TALENTS[id];
    if (!d) continue;
    if (d.mods) mods = addMods(mods, d.mods);
    add(d.charges);
  }
  // Mano de machete wants the machete; Buen brazo, one more grenade in every accessory of them.
  if (picks.includes('bladeMaster') && arms?.melee === 'machete') mods = addMods(mods, { meleeDamage: 2, meleeAim: 15 });
  if (picks.includes('goodArm')) add({ grenade: grenades });
  for (const item of gear.map((id) => ITEMS[id])) {
    if (item.mods) mods = addMods(mods, item.mods);
    maxHp += item.hp ?? 0;
    add(item.charges);
  }
  return { template: 'rookie', mods, maxHp, charges, will: veteranWill(points) + mods.will, armor: 0, perks: [...picks], rank: veterancyRank(points) };
}

// ---------------------------------------------------------------- learning

/** What a soldier did in a mission that teaches (Unit.deeds). */
export type DeedId = 'hit' | 'gunKill' | 'blow' | 'bladeKill' | 'healOther' | 'healSelf' | 'search' | 'fix' | 'blast';
export type Deeds = Partial<Record<DeedId, number>>;

/** The skill each deed teaches, and how much. */
export const DEED_XP: Record<DeedId, [SkillId, number]> = {
  hit: ['shooting', 1],
  gunKill: ['shooting', 1],
  blow: ['melee', 1],
  bladeKill: ['melee', 1],
  healOther: ['medicine', 2],
  healSelf: ['medicine', 1],
  search: ['craft', 1],
  fix: ['craft', 2],
  blast: ['craft', 1],
};

/** The experience a mission's deeds give, skill by skill; `bonus` (0.2 = 20 %) more of each, rounded up. */
export function learnedFrom(deeds: Deeds | undefined, bonus = 0): Skills {
  const out = { ...NO_SKILLS };
  for (const [deed, times] of Object.entries(deeds ?? {}) as [DeedId, number][]) {
    const [skill, xp] = DEED_XP[deed];
    out[skill] += xp * times;
  }
  if (bonus) for (const id of SKILL_IDS) out[id] += Math.ceil(out[id] * bonus);
  return out;
}

/** Skills plus a mission's deeds (and its `bonus`), and the levels that went up. */
export function learn(skills: Skills, deeds: Deeds | undefined, bonus = 0): { skills: Skills; gained: Skills; levels: { skill: SkillId; from: number; to: number }[] } {
  const gained = learnedFrom(deeds, bonus);
  const next = { ...skills };
  const levels: { skill: SkillId; from: number; to: number }[] = [];
  for (const id of SKILL_IDS) {
    const from = skillLevel(skills[id]);
    next[id] = skills[id] + gained[id];
    const to = skillLevel(next[id]);
    if (to > from) levels.push({ skill: id, from, to });
  }
  return { skills: next, gained, levels };
}

// ----------------------------------------------------------------- newcomers

/** A recruit (docs/FASE9.md §3): level 1 in one or two skills, at random, so no two are alike. */
export function recruitSkills(rng: Rng): Skills {
  const out = { ...NO_SKILLS };
  const first = rng.pick(SKILL_IDS);
  out[first] = SKILL_XP[0];
  if (rng.next() < 0.5) out[rng.pick(SKILL_IDS.filter((id) => id !== first))] = SKILL_XP[0];
  return out;
}

/** A skirmish veteran (docs/FASE9.md §6): every skill at 2 to 4, and a talent of every pair it has opened. */
export function veteranSkills(rng: Rng): { skills: Skills; picks: PerkId[] } {
  const skills = { ...NO_SKILLS };
  for (const id of SKILL_IDS) skills[id] = SKILL_XP[rng.range(2, 4) - 1]!;
  const picks = pendingPicks(skills, []).map((p) => rng.pick(p.pair));
  return { skills, picks };
}
