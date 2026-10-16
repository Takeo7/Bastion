// Fase 10, experience bars (docs/FASE10.md, Arturo 16/10). Six lines that
// fill with what each soldier does in a mission (Unit.deeds, counted by the
// events) and is added if it comes back alive. A bar has no levels: when it
// fills it starts again, every time longer, and gives a skill point (spent on
// the hex board, board.ts) and 5–10 % more, at random, of its line's stat.
// Veterancy goes up with the points spent on the board (Arturo, 16/10).
import { BOARD, BOARD_CELLS, type CellId, CORE, withinReach } from './board';
import { addMods, HACK_BASE, ITEMS, MEDKIT_HEAL, TEMPLATES, ZERO_MODS } from './content';
import type { Rng } from './rng';
import type { Arms, ChargeId, ItemId, PerkId, TemplateId, UnitMods } from './types';

// ------------------------------------------------------------------- lines

export type SkillId = 'shooting' | 'melee' | 'throwing' | 'medicine' | 'searching' | 'fitness';
export const SKILL_IDS: SkillId[] = ['shooting', 'melee', 'throwing', 'medicine', 'searching', 'fitness'];

/** Experience earned in each line, all of it (the bars follow from it: barsOf). */
export type Skills = Record<SkillId, number>;
export const NO_SKILLS: Skills = { shooting: 0, melee: 0, throwing: 0, medicine: 0, searching: 0, fitness: 0 };

/** What each line's filled bars have added to its stat, in percent (rolled as they fill). */
export type Growth = Record<SkillId, number>;
export const NO_GROWTH: Growth = { ...NO_SKILLS };

export interface SkillDef {
  id: SkillId;
  name: string;
  short: string;
  /** What fills it. */
  line: string;
  /** The stat it raises. */
  stat: string;
}

export const SKILLS: Record<SkillId, SkillDef> = {
  shooting: { id: 'shooting', name: 'Disparo', short: 'DIS', line: 'Se llena disparando, más si se acierta y más si se abate.', stat: 'Puntería con armas de fuego' },
  melee: { id: 'melee', name: 'Cuerpo a cuerpo', short: 'CaC', line: 'Se llena golpeando con la hoja, más si se acierta y más si se abate.', stat: 'Puntería cuerpo a cuerpo' },
  throwing: { id: 'throwing', name: 'Lanzar', short: 'LAN', line: 'Se llena lanzando piedras y granadas, más si alcanzan a alguien.', stat: 'Alcance al lanzar' },
  medicine: { id: 'medicine', name: 'Curar', short: 'CUR', line: 'Se llena curando con el botiquín o con medicinas; más si es a otro.', stat: 'Curación' },
  searching: { id: 'searching', name: 'Investigar', short: 'INV', line: 'Se llena registrando habitaciones, abriendo cajas, arreglando equipos y apagando alarmas.', stat: 'Arreglar' },
  fitness: { id: 'fitness', name: 'Forma física', short: 'FOR', line: 'Se llena agazapándose y corriendo (moverse con las dos acciones).', stat: 'Salud' },
};

/** The first bar takes BAR_FIRST, and every next one BAR_STEP more. */
export const BAR_FIRST = 10;
export const BAR_STEP = 5;
/** What a bar adds to its line's stat when it fills, in percent, at random between these. */
export const BAR_GROWTH = [5, 10] as const;

/** Experience the `n`-th bar of a line takes (0 is the first). */
export const barCost = (n: number) => BAR_FIRST + BAR_STEP * n;

/** Experience it takes to fill the first `n` bars of a line. */
export const barsCost = (n: number) => BAR_FIRST * n + (BAR_STEP * n * (n - 1)) / 2;

/** A line's bars filled, and how far into the next one it is. */
export function barsOf(xp: number): { filled: number; into: number; need: number } {
  let filled = 0;
  while (xp >= barsCost(filled + 1)) filled++;
  return { filled, into: xp - barsCost(filled), need: barCost(filled) };
}

/** Bars filled in every line. */
export function filledBars(skills: Skills): Record<SkillId, number> {
  const out = { ...NO_SKILLS };
  for (const id of SKILL_IDS) out[id] = barsOf(skills[id]).filled;
  return out;
}

/** What a bar adds when it fills: 5 to 10 % (BAR_GROWTH), at random. */
export function rollGrowth(rng: Rng): number {
  return rng.range(BAR_GROWTH[0], BAR_GROWTH[1]);
}

// --------------------------------------------------------------- veterancy

export interface VeterancyDef {
  name: string;
  /** For a woman: Novata, Fogueada… (Lore). */
  female: string;
  /** Skill points spent on the board it starts at (a step every RANK_STEP). */
  from: number;
  line: string;
}

export const VETERANCY: VeterancyDef[] = [
  { name: 'Novato', female: 'Novata', from: 0, line: 'Todavía no ha visto lo peor.' },
  { name: 'Fogueado', female: 'Fogueada', from: 5, line: 'Ya ha salido y ha vuelto.' },
  { name: 'Curtido', female: 'Curtida', from: 10, line: 'Sabe cuándo no disparar.' },
  { name: 'Veterano', female: 'Veterana', from: 15, line: 'Tercera acción por turno. Los demás le siguen sin que lo pida.' },
  { name: 'Superviviente', female: 'Superviviente', from: 20, line: 'Ha vuelto de todo.' },
];

/** A rank more for every so many points spent on the board, and this much will with each (Arturo, 16/10). */
export const RANK_STEP = 5;
export const WILL_PER_RANK = 10;

export const BASE_HP = 5;
export const BASE_WILL = 40;
/** Aim with a firearm or a blade before what the lines add, and before the weapon. */
export const BASE_AIM = 60;
/** How far a throw reaches before what Lanzar adds (the grenade's range). */
export const THROW_RANGE = 10;

/** Bars filled among the six lines: the skill points earned. */
export function totalBars(skills: Skills): number {
  return SKILL_IDS.reduce((sum, id) => sum + barsOf(skills[id]).filled, 0);
}

/** VETERANCY's index for the points spent on the board; from Veterano on, a third action (content.ts THIRD_ACTION_RANK). */
export function veterancyRank(spent: number): number {
  let rank = 0;
  while (rank + 1 < VETERANCY.length && spent >= VETERANCY[rank + 1]!.from) rank++;
  return rank;
}

/** Will for a rank: 40, and 10 more with each. */
export const veteranWill = (rank: number) => BASE_WILL + WILL_PER_RANK * rank;

// ------------------------------------------------------------------- stats

/** `base` with `percent` more, rounded: a stat after what its line has added. */
export const grown = (base: number, percent: number) => Math.round(base * (1 + percent / 100));

/** Everything a soldier fights with, from its lines, its talents and what it carries. */
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
 * A soldier's numbers (docs/FASE10.md): each line's stat with what its bars
 * added (`growth`), the skills unlocked on the board, and what it carries.
 * Everyone fights as the same template; grenades, the medkit and the rest
 * come only from the belt.
 */
export function fighterStats(skills: Skills, growth: Growth, board: readonly CellId[], gear: ItemId[] = [], arms?: Arms): FighterStats {
  const tpl = TEMPLATES.rookie;
  let mods = addMods(
    { ...ZERO_MODS },
    {
      aim: BASE_AIM - tpl.aim,
      gunAim: grown(BASE_AIM, growth.shooting) - BASE_AIM,
      meleeAim: grown(BASE_AIM, growth.melee) - BASE_AIM,
      grenadeRange: grown(THROW_RANGE, growth.throwing) - THROW_RANGE,
      heal: grown(MEDKIT_HEAL, growth.medicine) - MEDKIT_HEAL,
      hack: grown(HACK_BASE, growth.searching) - HACK_BASE,
    },
  );
  let maxHp = grown(BASE_HP, growth.fitness);
  let armor = 0;
  const perks: PerkId[] = [];
  const charges: Partial<Record<ChargeId, number>> = {};
  const add = (extra: Partial<Record<ChargeId, number>> | undefined) => {
    for (const [c, n] of Object.entries(extra ?? {}) as [ChargeId, number][]) if (n) charges[c] = (charges[c] ?? 0) + n;
  };
  const cells = board.map((id) => BOARD_CELLS[id]).filter((c) => !!c);
  for (const c of cells) {
    if (c.mods) mods = addMods(mods, c.mods);
    maxHp += c.hp ?? 0;
    armor += c.armor ?? 0;
    if (c.perk && !perks.includes(c.perk)) perks.push(c.perk);
    add(c.charges);
  }
  for (const item of gear.map((id) => ITEMS[id])) {
    if (item.mods) mods = addMods(mods, item.mods);
    maxHp += item.hp ?? 0;
    add(item.charges);
  }
  // Mano de machete wants the machete; Buen brazo, one more grenade in every accessory of them.
  if (perks.includes('bladeMaster') && arms?.melee === 'machete') mods = addMods(mods, { meleeDamage: 2, meleeAim: 15 });
  if (perks.includes('goodArm')) add({ grenade: gear.filter((id) => ITEMS[id].charges?.grenade).length });
  // One more use of what it carries: nothing if it carries none.
  for (const c of cells) for (const [id, n] of Object.entries(c.spare ?? {}) as [ChargeId, number][]) if (charges[id] !== undefined) charges[id] += n;
  const rank = veterancyRank(cells.length);
  return { template: 'rookie', mods, maxHp, charges, will: veteranWill(rank) + mods.will, armor, perks, rank };
}

// -------------------------------------------------------------- the board

/** Skill points: one for every bar filled, less the cells unlocked on the board. */
export function pointsLeft(skills: Skills, board: readonly CellId[]): number {
  return Math.max(0, totalBars(skills) - board.length);
}

/**
 * Why a soldier can't unlock `id` now (docs/FASE10.md §6), or null: beside the
 * core or a cell it has, and a point to spend. Nothing else (Arturo, 16/10).
 */
export function unlockBlocker(skills: Skills, board: readonly CellId[], id: CellId): string | null {
  if (!BOARD_CELLS[id]) return 'Esa casilla no existe.';
  if (id === CORE || board.includes(id)) return 'Ya la tiene.';
  if (!withinReach(board, id)) return 'Tiene que estar al lado de una que ya tenga.';
  if (pointsLeft(skills, board) < 1) return 'No le quedan puntos: se gana uno cada vez que se llena una barra.';
  return null;
}

/** A cell's state for a soldier: unlocked, beside one it has (to be bought, if it can), or out of reach. */
export function cellState(board: readonly CellId[], id: CellId): 'unlocked' | 'open' | 'locked' {
  if (id === CORE || board.includes(id)) return 'unlocked';
  return withinReach(board, id) ? 'open' : 'locked';
}

// ---------------------------------------------------------------- learning

/** What a soldier did in a mission that teaches (Unit.deeds). */
export type DeedId = 'shot' | 'hit' | 'gunKill' | 'swing' | 'blow' | 'bladeKill' | 'throw' | 'throwHit' | 'healOther' | 'healSelf' | 'search' | 'fix' | 'hunker' | 'run';
export type Deeds = Partial<Record<DeedId, number>>;

/** The line each deed fills, and how much (docs/FASE10.md §3). */
export const DEED_XP: Record<DeedId, [SkillId, number]> = {
  shot: ['shooting', 1],
  hit: ['shooting', 1],
  gunKill: ['shooting', 1],
  swing: ['melee', 1],
  blow: ['melee', 1],
  bladeKill: ['melee', 1],
  throw: ['throwing', 1],
  throwHit: ['throwing', 1],
  healOther: ['medicine', 2],
  healSelf: ['medicine', 1],
  search: ['searching', 2],
  fix: ['searching', 3],
  hunker: ['fitness', 1],
  run: ['fitness', 1],
};

/** The experience a mission's deeds give, line by line; `bonus` (0.2 = 20 %) more of each, rounded up. */
export function learnedFrom(deeds: Deeds | undefined, bonus = 0): Skills {
  const out = { ...NO_SKILLS };
  for (const [deed, times] of Object.entries(deeds ?? {}) as [DeedId, number][]) {
    const [skill, xp] = DEED_XP[deed];
    out[skill] += xp * times;
  }
  if (bonus) for (const id of SKILL_IDS) out[id] += Math.ceil(out[id] * bonus);
  return out;
}

/** A bar that filled on coming back: its line, how many it has now, and what each one added (percent). */
export interface BarFilled {
  skill: SkillId;
  filled: number;
  growth: number[];
}

/**
 * A mission's deeds (and its `bonus`) added to a soldier's lines: the bars
 * that filled, each with its stat rolled up 5–10 % (`rng`), and a skill point
 * for each.
 */
export function learn(skills: Skills, growth: Growth, deeds: Deeds | undefined, rng: Rng, bonus = 0): { skills: Skills; growth: Growth; gained: Skills; bars: BarFilled[]; points: number } {
  const gained = learnedFrom(deeds, bonus);
  const next = { ...skills };
  const grew = { ...growth };
  const bars: BarFilled[] = [];
  let points = 0;
  for (const id of SKILL_IDS) {
    const from = barsOf(skills[id]).filled;
    next[id] = skills[id] + gained[id];
    const to = barsOf(next[id]).filled;
    if (to <= from) continue;
    const rolls = Array.from({ length: to - from }, () => rollGrowth(rng));
    grew[id] += rolls.reduce((a, b) => a + b, 0);
    points += rolls.length;
    bars.push({ skill: id, filled: to, growth: rolls });
  }
  return { skills: next, growth: grew, gained, bars, points };
}

// ----------------------------------------------------------------- newcomers

/** Lines with `bars` filled, each with its growth rolled. */
function seasoned(rng: Rng, bars: Record<SkillId, number>): { skills: Skills; growth: Growth } {
  const skills = { ...NO_SKILLS };
  const growth = { ...NO_GROWTH };
  for (const id of SKILL_IDS) {
    skills[id] = barsCost(bars[id]);
    for (let k = 0; k < bars[id]; k++) growth[id] += rollGrowth(rng);
  }
  return { skills, growth };
}

/** A recruit (docs/FASE10.md §5): a bar filled in one or two lines, at random, so no two are alike. */
export function recruitSkills(rng: Rng): { skills: Skills; growth: Growth } {
  const bars = { ...NO_SKILLS };
  const first = rng.pick(SKILL_IDS);
  bars[first] = 1;
  if (rng.next() < 0.5) bars[rng.pick(SKILL_IDS.filter((id) => id !== first))] = 1;
  return seasoned(rng, bars);
}

/** One more bar in a line at random, with its growth (a recruit trained in the base, a newcomer who has been out). */
export function oneMoreBar(skills: Skills, growth: Growth, rng: Rng): { skills: Skills; growth: Growth } {
  const id = rng.pick(SKILL_IDS);
  const filled = barsOf(skills[id]).filled + 1;
  return { skills: { ...skills, [id]: Math.max(skills[id], barsCost(filled)) }, growth: { ...growth, [id]: growth[id] + rollGrowth(rng) } };
}

/** A skirmish veteran (docs/FASE10.md §7): two to five bars in every line, and its points spent on the board at random. */
export function veteranSkills(rng: Rng): { skills: Skills; growth: Growth; board: CellId[] } {
  const bars = { ...NO_SKILLS };
  for (const id of SKILL_IDS) bars[id] = rng.range(2, 5);
  const lines = seasoned(rng, bars);
  return { ...lines, board: randomBoard(lines.skills, rng) };
}

/** Every point spent, a cell at a time, on whatever the board allows (a skirmish veteran's). */
export function randomBoard(skills: Skills, rng: Rng): CellId[] {
  const board: CellId[] = [];
  for (;;) {
    const open = BOARD.filter((c) => unlockBlocker(skills, board, c.id) === null);
    if (!open.length) return board;
    board.push(rng.pick(open).id);
  }
}
