import './skillMatrix.css';
import {
  SKILL_IDS,
  SKILL_MAX,
  SKILL_TALENTS,
  SKILL_XP,
  SKILLS,
  skillLevel,
  skillMods,
  TALENT_PAIRS,
  veterancy,
  veterancyRank,
  VETERANCY,
  type AbilityId,
  type PerkId,
  type SkillId,
  type Skills,
  type SkillTalent,
} from '@bastion/engine';
import { append, clear, h } from '../ui/dom';
import { abilityIcon } from '../ui/icons';

// The «Árbol de habilidades» of Fase 9 (docs/FASE9.md §2), after the «Prepping
// Matrix» poster: the soldier's bust at the centre, four trunks of a colour
// each (the skills) and five rings round them (the levels). Each trunk starts
// in a plate with the skill's name, its level and how far it is to the next;
// it is lit as far as the soldier's experience goes, and on every ring it has
// a knot with the level's number. On rings 2 and 4 it forks into the pair of
// talents of that level, one of which the soldier keeps. Nothing is written
// outside the cells (playtest 15/10 #6): every name is inside its plate.
//
// The rules are the engine's (skills.ts): skillLevel for the levels and the
// light, TALENT_PAIRS for the forks, skillMods for what each level gives.
// A pick is a draft until «Guardar» (skillSheet.ts keeps it); a saved pick
// is for good. The tree is small enough to be seen whole: the board scales it
// to its window, nothing to pan or zoom.

export interface MatrixContext {
  /** Whose tree; its bust goes at the centre once drawn (portraits.ts), never a stand-in. */
  id: string;
  portrait: Promise<string> | null;
  skills: Skills;
  /** The talents as saved. */
  saved: PerkId[];
  /** The talents picked in this draft, one per pair still open. */
  draft: PerkId[];
  /** The viewer may pick: its own soldier, alive. */
  editable: boolean;
  /** A woman: the veterancy's names go in the feminine (VETERANCY[].female). */
  female?: boolean;
  onPick: (id: PerkId) => void;
  onUnpick: (id: PerkId) => void;
}

// ------------------------------------------------------------------ the rules

/** A step of the veterancy by its index, in the feminine for a woman (Lore). */
export const vetName = (rank: number, female = false) => (female ? VETERANCY[rank]!.female : VETERANCY[rank]!.name);

/** Experience a level starts at: 0 for level 0, then SKILL_XP. */
export const levelXp = (level: number) => (level <= 0 ? 0 : SKILL_XP[level - 1]!);

/** How far a skill is from its level to the next, 0–1 (1 at the top). */
export function levelProgress(xp: number): number {
  const level = skillLevel(xp);
  if (level >= SKILL_MAX) return 1;
  return (xp - levelXp(level)) / (levelXp(level + 1) - levelXp(level));
}

/** The pair a talent is in, and its mate. */
export function mateOf(id: PerkId): PerkId {
  const def = SKILL_TALENTS[id]!;
  const pair = TALENT_PAIRS[def.skill][def.level === 2 ? 0 : 1];
  return pair[0] === id ? pair[1] : pair[0];
}

/**
 * A talent's place for this soldier:
 * - picked: saved, for good;
 * - passed: its mate was saved;
 * - locked: its skill hasn't reached its level;
 * - drafted: picked in this draft;
 * - other: its mate is picked in this draft (a click swaps them);
 * - open: its level reached and its pair still without a pick.
 */
export type CellState = 'picked' | 'passed' | 'locked' | 'drafted' | 'other' | 'open';

export function cellState(skills: Skills, saved: PerkId[], draft: PerkId[], id: PerkId): CellState {
  const def = SKILL_TALENTS[id]!;
  const mate = mateOf(id);
  if (saved.includes(id)) return 'picked';
  if (saved.includes(mate)) return 'passed';
  if (skillLevel(skills[def.skill]) < def.level) return 'locked';
  if (draft.includes(id)) return 'drafted';
  if (draft.includes(mate)) return 'other';
  return 'open';
}

/** What a level of a skill gives on its own, in words: what skillMods adds from the level before. */
export function levelGifts(skill: SkillId, level: number): string[] {
  const at = (l: number) => {
    const skills = { shooting: 0, melee: 0, medicine: 0, craft: 0, [skill]: levelXp(l) } as Skills;
    return skillMods(skills, { grenades: true, medkit: true });
  };
  const now = at(level);
  const before = at(level - 1);
  const out: string[] = [];
  for (const [key, words] of MOD_WORDS) {
    const d = (now.mods[key] ?? 0) - (before.mods[key] ?? 0);
    if (d) out.push(key === 'heal' ? `Las curas sanan +${d}` : `+${d} ${words}`);
  }
  if ((now.charges.grenade ?? 0) > (before.charges.grenade ?? 0)) out.push('Una granada más en cada accesorio de granadas');
  if ((now.charges.firstAid ?? 0) > (before.charges.firstAid ?? 0)) out.push('Un uso más en cada botiquín');
  return out;
}

const MOD_WORDS: [keyof ReturnType<typeof skillMods>['mods'], string][] = [
  ['gunAim', 'de puntería con armas de fuego'],
  ['gunCrit', 'de crítico con armas de fuego'],
  ['meleeAim', 'de puntería cuerpo a cuerpo'],
  ['meleeDamage', 'de daño cuerpo a cuerpo'],
  ['heal', ''],
  ['hack', 'a arreglar y forzar'],
  ['grenadeDamage', 'de daño de los explosivos'],
];

// ------------------------------------------------------------------ icons

/**
 * Each talent's icon: its combat button's if it gives one (the HUD's, so it
 * is known on sight), otherwise a line icon of what it touches. The theme
 * covers both with the packs' silhouettes (synty-icons.css, data-talent).
 */
const PASSIVE_ICON: Partial<Record<PerkId, AbilityId>> = {
  implacable: 'runAndGun',
  bladeCrit: 'slash',
  bladeMaster: 'slash',
  fieldMedic: 'medkit',
  lightHands: 'aid',
  goodArm: 'grenade',
  quiet: 'hunker',
  demolition: 'grenade',
};

export function talentIcon(def: SkillTalent, extra = ''): HTMLElement {
  const icon = def.ability ?? PASSIVE_ICON[def.id] ?? 'shoot';
  return h(`span.sm-disc${def.ability ? '.ab' : ''}${extra}`, { 'data-talent': def.id, 'data-ability': def.ability ?? null }, abilityIcon(icon));
}

// ------------------------------------------------------------------ the shape

interface Pt {
  x: number;
  y: number;
}

/** A point `r` from the centre at `deg`, the round layout drawn wider than tall to fill the window. */
const SX = 1.15;
const SY = 0.88;
const pol = (r: number, deg: number): Pt => ({ x: r * SX * Math.cos((deg * Math.PI) / 180), y: r * SY * Math.sin((deg * Math.PI) / 180) });
const f = (n: number) => (Math.round(n * 10) / 10).toString();

/** The trunks: the fighting skills above, the others below; the guns and the medicine on the left. */
const ANGLE: Record<SkillId, number> = { shooting: -150, melee: -30, craft: 30, medicine: 150 };
/** Where the skill's plate sits, and the rings of the levels, from the centre (before the stretch). */
const R_ROOT = 150;
const R_FIRST = 268;
const STEP = 76;
const ringR = (level: number) => (level <= 0 ? R_ROOT : R_FIRST + STEP * (level - 1));
/** How far either side of its trunk each pair of talents sits on its ring, in degrees. */
const SPREAD: Record<2 | 4, number> = { 2: 16, 4: 12 };
const MEDAL = 78;
const CELL_W = 164;
const CELL_H = 50;
const ROOT_W = 188;
const ROOT_H = 58;
const KNOT = 17;

interface TalentGeo {
  id: PerkId;
  skill: SkillId;
  level: 2 | 4;
  p: Pt;
  /** The knot it forks from. */
  from: Pt;
}

interface Geo {
  roots: Record<SkillId, Pt>;
  knots: { skill: SkillId; level: number; p: Pt }[];
  talents: TalentGeo[];
  box: { x0: number; y0: number; x1: number; y1: number };
}

let cached: Geo | null = null;

function geo(): Geo {
  if (cached) return cached;
  const roots = {} as Record<SkillId, Pt>;
  const knots: Geo['knots'] = [];
  const talents: TalentGeo[] = [];
  for (const skill of SKILL_IDS) {
    const a = ANGLE[skill];
    // On its trunk, pushed out sideways so its inner edge clears the medal.
    const root = pol(R_ROOT, a);
    roots[skill] = { x: Math.sign(root.x) * Math.max(Math.abs(root.x), MEDAL + 22 + ROOT_W / 2), y: root.y };
    for (let level = 1; level <= SKILL_MAX; level++) knots.push({ skill, level, p: pol(ringR(level), a) });
    TALENT_PAIRS[skill].forEach((pair, k) => {
      const level = k === 0 ? 2 : 4;
      const r = ringR(level);
      // The first of the pair on the outer side (away from the middle line), the second on the inner.
      const sides = [a - SPREAD[level], a + SPREAD[level]].sort((p, q) => Math.abs(Math.sin((q * Math.PI) / 180)) - Math.abs(Math.sin((p * Math.PI) / 180)));
      pair.forEach((id, i) => talents.push({ id, skill, level, p: pol(r, sides[i]!), from: pol(r, a) }));
    });
  }
  const box = { x0: Infinity, y0: Infinity, x1: -Infinity, y1: -Infinity };
  const grow = (p: Pt, w: number, hh: number) => {
    box.x0 = Math.min(box.x0, p.x - w / 2);
    box.x1 = Math.max(box.x1, p.x + w / 2);
    box.y0 = Math.min(box.y0, p.y - hh / 2);
    box.y1 = Math.max(box.y1, p.y + hh / 2);
  };
  for (const t of talents) grow(t.p, CELL_W + 16, CELL_H + 16);
  for (const k of knots) grow(k.p, KNOT * 2 + 24, KNOT * 2 + 24);
  for (const skill of SKILL_IDS) grow(roots[skill], ROOT_W + 16, ROOT_H + 16);
  grow({ x: 0, y: 0 }, MEDAL * 2 + 30, MEDAL * 2 + 30);
  cached = { roots, knots, talents, box };
  return cached;
}

// ------------------------------------------------------------------ drawing

const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/** A trunk as a shape thinning from `w0` to `w1` along the straight line from `a` to `b`. */
function taper(a: Pt, b: Pt, w0: number, w1: number): string {
  const len = Math.hypot(b.x - a.x, b.y - a.y) || 1;
  const n = { x: -(b.y - a.y) / len, y: (b.x - a.x) / len };
  const pts = [
    { x: a.x + (n.x * w0) / 2, y: a.y + (n.y * w0) / 2 },
    { x: b.x + (n.x * w1) / 2, y: b.y + (n.y * w1) / 2 },
    { x: b.x - (n.x * w1) / 2, y: b.y - (n.y * w1) / 2 },
    { x: a.x - (n.x * w0) / 2, y: a.y - (n.y * w0) / 2 },
  ];
  return `M${pts.map((p) => `${f(p.x)} ${f(p.y)}`).join('L')}Z`;
}

/** The structure under the cells: the rings, the trunks lit as far as the experience goes, the forks to the talents. */
function structure(ctx: MatrixContext, G: Geo): SVGSVGElement {
  const parts: string[] = [];
  for (let level = 1; level <= SKILL_MAX; level++) parts.push(`<ellipse class="sm-ring" cx="0" cy="0" rx="${f(ringR(level) * SX)}" ry="${f(ringR(level) * SY)}"/>`);
  const r0 = MEDAL - 10;
  const r1 = ringR(SKILL_MAX);
  const width = (r: number) => 30 - (16 * (r - r0)) / (r1 - r0);
  for (const skill of SKILL_IDS) {
    const a = ANGLE[skill];
    const xp = ctx.skills[skill];
    const level = skillLevel(xp);
    // Lit up to the root's plate always, then as far as the experience reaches towards the next ring.
    const reach = level >= SKILL_MAX ? r1 : ringR(level) + (ringR(level + 1) - ringR(level)) * levelProgress(xp);
    parts.push(`<g class="sk-${skill}">`);
    parts.push(`<path class="sm-trunk" d="${taper(pol(r0, a), pol(r1, a), width(r0), width(r1))}"/>`);
    parts.push(`<path class="sm-trunk lit" d="${taper(pol(r0, a), pol(reach, a), width(r0), width(reach))}"/>`);
    for (const t of G.talents.filter((x) => x.skill === skill)) {
      const st = cellState(ctx.skills, ctx.saved, ctx.draft, t.id);
      // A fork that bends out from the trunk's knot to the talent's plate.
      const mid = { x: (t.from.x + t.p.x) / 2, y: (t.from.y + t.p.y) / 2 };
      const out = Math.hypot(mid.x, mid.y) || 1;
      const bend = { x: mid.x + (mid.x / out) * 12, y: mid.y + (mid.y / out) * 12 };
      parts.push(`<path class="sm-fork ${st}" d="M${f(t.from.x)} ${f(t.from.y)}Q${f(bend.x)} ${f(bend.y)} ${f(t.p.x)} ${f(t.p.y)}"/>`);
    }
    parts.push('</g>');
  }
  const { box } = G;
  const w = box.x1 - box.x0;
  const hh = box.y1 - box.y0;
  const t = document.createElement('template');
  t.innerHTML = `<svg class="sm-svg" xmlns="http://www.w3.org/2000/svg" viewBox="${f(box.x0)} ${f(box.y0)} ${f(w)} ${f(hh)}" width="${f(w)}" height="${f(hh)}" style="left:${f(box.x0)}px;top:${f(box.y0)}px" aria-hidden="true">${parts.join('')}</svg>`;
  return t.content.firstElementChild as SVGSVGElement;
}

/** The soldier in the middle: their own bust, and their veterancy on a band. */
function medalEl(ctx: MatrixContext): HTMLElement {
  const points = veterancy(ctx.skills);
  const face = h('span.sm-face');
  void ctx.portrait?.then((url) => face.style.setProperty('--portrait', `url('${url}')`)).catch(() => undefined);
  return h('div.sm-medal', {}, face, h('span.sm-band', {}, `${vetName(veterancyRank(points), ctx.female)} · ${points}`));
}

/** Shows a card for `key` by `at`, or hides it. */
type Inspect = (key: string | null, at?: HTMLElement) => void;

/** A skill's plate at the root of its trunk: its name, its level and the experience to the next. */
function rootEl(ctx: MatrixContext, skill: SkillId, p: Pt, inspect: Inspect): HTMLElement {
  const xp = ctx.skills[skill];
  const level = skillLevel(xp);
  const next = level < SKILL_MAX ? levelXp(level + 1) : null;
  return h(
    `button.sm-root.sk-${skill}`,
    {
      type: 'button',
      style: { left: `${f(p.x)}px`, top: `${f(p.y)}px` },
      'aria-label': `${SKILLS[skill].name} ${level}${next ? `, ${xp} de ${next}` : ', al máximo'}`,
      onmouseenter: (e: Event) => inspect(`skill:${skill}`, e.currentTarget as HTMLElement),
      onmouseleave: () => inspect(null),
      onfocus: (e: Event) => inspect(`skill:${skill}`, e.currentTarget as HTMLElement),
      onblur: () => inspect(null),
    },
    h('b.sm-level', {}, String(level)),
    h('span.sm-root-name', {}, SKILLS[skill].name),
    h('span.sm-xp', {}, h('i', { style: { '--p': levelProgress(xp).toFixed(3) } }), h('small', {}, next ? `${xp}/${next}` : 'Al máximo')),
  );
}

/** A ring's knot on a trunk: the level's number, lit once reached. */
function knotEl(ctx: MatrixContext, skill: SkillId, level: number, p: Pt, inspect: Inspect): HTMLElement {
  const reached = skillLevel(ctx.skills[skill]) >= level;
  const pending = (level === 2 || level === 4) && reached && TALENT_PAIRS[skill][level === 2 ? 0 : 1].every((id) => !ctx.saved.includes(id));
  return h(
    `span.sm-knot.sk-${skill}${reached ? '.reached' : ''}${pending ? '.pending' : ''}`,
    {
      style: { left: `${f(p.x)}px`, top: `${f(p.y)}px` },
      tabindex: '-1',
      onmouseenter: (e: Event) => inspect(`level:${skill}:${level}`, e.currentTarget as HTMLElement),
      onmouseleave: () => inspect(null),
    },
    String(level),
  );
}

/** What a talent's plate says under its name: its place, in a word or two. */
function stateWord(st: CellState, def: SkillTalent, editable: boolean): string {
  switch (st) {
    case 'picked':
      return 'Elegido';
    case 'passed':
    case 'other':
      return 'No elegido';
    case 'locked':
      // On its skill's trunk and in its colour: the level says the rest.
      return `Se abre en el nivel ${def.level}`;
    case 'drafted':
      return 'Sin guardar';
    case 'open':
      return editable ? 'Por elegir' : 'Sin elegir';
  }
}

/** A talent: a plate with its icon and name; its look says its state (cellState), and so does its last line. */
function talentEl(ctx: MatrixContext, t: TalentGeo, inspect: Inspect): HTMLElement[] {
  const def = SKILL_TALENTS[t.id]!;
  const st = cellState(ctx.skills, ctx.saved, ctx.draft, t.id);
  const live = ctx.editable && (st === 'open' || st === 'other' || st === 'drafted');
  const plate = h(
    `button.sm-cell.sk-${t.skill}.${st}${def.ability ? '.ab' : ''}${live ? '.live' : ''}`,
    {
      type: 'button',
      style: { left: `${f(t.p.x)}px`, top: `${f(t.p.y)}px` },
      'aria-disabled': live ? 'false' : 'true',
      'aria-pressed': st === 'picked' || st === 'drafted' ? 'true' : 'false',
      'aria-label': `${def.name}: ${stateWord(st, def, ctx.editable)}`,
      onmouseenter: (e: Event) => inspect(`talent:${t.id}`, e.currentTarget as HTMLElement),
      onmouseleave: () => inspect(null),
      onfocus: (e: Event) => inspect(`talent:${t.id}`, e.currentTarget as HTMLElement),
      onblur: () => inspect(null),
      onclick: () => {
        if (!live) return;
        if (st === 'drafted') ctx.onUnpick(t.id);
        else ctx.onPick(t.id);
      },
    },
    talentIcon(def),
    h('span.sm-cell-text', {}, h('b', {}, def.name), h('small', {}, stateWord(st, def, ctx.editable))),
  );
  if (!(ctx.editable && st === 'drafted')) return [plate];
  // Picked in this draft: taken back in plain sight, not only with a second click (playtest 15/10 #6).
  const undo = h(
    'button.sm-undo',
    {
      type: 'button',
      title: `Quitar ${def.name}`,
      'aria-label': `Quitar ${def.name}`,
      style: { left: `${f(t.p.x + CELL_W / 2 - 4)}px`, top: `${f(t.p.y - CELL_H / 2 + 4)}px` },
      onclick: (e: Event) => {
        e.stopPropagation();
        ctx.onUnpick(t.id);
      },
    },
    '−',
  );
  return [plate, undo];
}

// ------------------------------------------------------------------ the card

/**
 * The card of what is under the pointer (or the keyboard's focus), by it, over
 * the sheet: what it is, what it gives, what it lacks and what a click does.
 */
function card(ctx: MatrixContext): { el: HTMLElement; show: Inspect } {
  const el = h('div.sm-tip', { role: 'tooltip' });
  const show: Inspect = (key, at) => {
    if (!key || !at) {
      el.remove();
      return;
    }
    clear(el);
    const [kind, a, b] = key.split(':') as [string, string, string | undefined];
    if (kind === 'skill') fillSkill(el, ctx, a as SkillId);
    else if (kind === 'level') fillLevel(el, ctx, a as SkillId, Number(b));
    else fillTalent(el, ctx, a as PerkId);
    place(el, at);
  };
  return { el, show };
}

function fillSkill(el: HTMLElement, ctx: MatrixContext, skill: SkillId): void {
  const xp = ctx.skills[skill];
  const level = skillLevel(xp);
  const def = SKILLS[skill];
  el.className = `sm-tip sk-${skill}`;
  append(
    el,
    h('div.x-group', {}, 'Habilidad'),
    h('b.sm-tip-name', {}, `${def.name} ${level}`),
    h('p.sm-tip-line', {}, def.line),
    h(
      'p.sm-tip-status',
      {},
      level >= SKILL_MAX ? `Al máximo: ${xp} de experiencia.` : `${xp} de experiencia; el nivel ${level + 1} pide ${levelXp(level + 1)}.`,
    ),
    h(
      'ol.sm-tip-levels',
      {},
      ...Array.from({ length: SKILL_MAX }, (_, i) => {
        const gifts = levelGifts(skill, i + 1);
        const pair = i === 1 || i === 3 ? TALENT_PAIRS[skill][i === 1 ? 0 : 1].map((id) => SKILL_TALENTS[id]!.name).join(' o ') : null;
        const text = [...gifts, pair ? `Un talento: ${pair}` : null].filter(Boolean).join(' · ') || 'Solo cuenta para la veteranía';
        return h(`li${i < level ? '.got' : ''}`, {}, h('b', {}, String(i + 1)), text);
      }),
    ),
  );
}

function fillLevel(el: HTMLElement, ctx: MatrixContext, skill: SkillId, level: number): void {
  const xp = ctx.skills[skill];
  const reached = skillLevel(xp) >= level;
  const gifts = levelGifts(skill, level);
  el.className = `sm-tip sk-${skill}`;
  append(
    el,
    h('div.x-group', {}, 'Nivel'),
    h('b.sm-tip-name', {}, `${SKILLS[skill].name} ${level}`),
    gifts.length ? h('ul.sm-tip-gifts', {}, ...gifts.map((g) => h('li', {}, g))) : h('p.sm-tip-line', {}, 'No da nada por sí solo; cuenta para la veteranía.'),
    level === 2 || level === 4 ? h('p.sm-tip-line', {}, 'Y uno de los dos talentos de este anillo.') : null,
    h(`p.sm-tip-status${reached ? '.done' : ''}`, {}, reached ? 'Alcanzado.' : `Pide ${levelXp(level)} de experiencia; lleva ${xp}.`),
  );
}

function fillTalent(el: HTMLElement, ctx: MatrixContext, id: PerkId): void {
  const def = SKILL_TALENTS[id]!;
  const mate = SKILL_TALENTS[mateOf(id)]!;
  const st = cellState(ctx.skills, ctx.saved, ctx.draft, id);
  const xp = ctx.skills[def.skill];
  const status: Record<CellState, [string, string]> = {
    picked: ['done', 'Elegido. Ya es suyo.'],
    passed: ['', `Eligió ${mate.name}.`],
    locked: ['', `Se elige con ${SKILLS[def.skill].name} ${def.level}: le faltan ${levelXp(def.level) - xp} de experiencia.`],
    drafted: ['open', ctx.editable ? 'Elegido en este borrador. Cuenta al pulsar «Guardar»; el «−» lo quita.' : 'Elegido en un borrador.'],
    other: ['open', ctx.editable ? `Ahora va ${mate.name}. Clic: cambiar a este.` : `Va ${mate.name}.`],
    open: ['open', ctx.editable ? 'Clic: elegirlo. No cuenta hasta «Guardar».' : 'Todavía sin elegir.'],
  };
  el.className = `sm-tip sk-${def.skill}`;
  append(
    el,
    h('div.x-group', {}, `${SKILLS[def.skill].name} ${def.level} · uno de dos`),
    h(
      'div.sm-tip-head',
      {},
      talentIcon(def, st === 'picked' || st === 'drafted' ? '.on' : ''),
      h('div', {}, h('b.sm-tip-name', {}, def.name), h('small.sm-tip-kind', {}, def.ability ? 'Acción: un botón más en combate' : 'Siempre activo')),
    ),
    h('p.sm-tip-line', {}, def.line),
    h('p.sm-tip-mate', {}, `La otra: ${mate.name}.`),
    h(`p.sm-tip-status.${status[st][0] || 'shut'}`, {}, status[st][1]),
  );
}

/** By its cell, inside the sheet: to its right if there is room, else to its left; never past the sheet's edges. */
function place(el: HTMLElement, at: HTMLElement): void {
  const host = at.closest<HTMLElement>('.x-panel') ?? at.closest<HTMLElement>('.sm-host') ?? at.parentElement!;
  if (el.parentElement !== host) host.append(el);
  const box = host.getBoundingClientRect();
  const node = at.getBoundingClientRect();
  // The sheet may be zoomed (--ui-scale): screen pixels back to its own.
  const scale = box.width / host.offsetWidth || 1;
  const w = el.offsetWidth * scale;
  const tall = el.offsetHeight * scale;
  const right = node.right + 12 + w <= box.right - 8;
  const x = right ? node.right + 12 : Math.max(box.left + 8, node.left - 12 - w);
  const y = Math.min(Math.max(box.top + 8, node.top + node.height / 2 - tall / 2), box.bottom - 8 - tall);
  el.style.left = `${(x - box.left) / scale + host.scrollLeft}px`;
  el.style.top = `${(y - box.top) / scale + host.scrollTop}px`;
}

// ------------------------------------------------------------------ the tree

/** Board pixels per tree unit at most: on a big screen it doesn't grow past easy reading. */
const MAX_SCALE = 1.2;

/** The tree in its board, scaled to fit it whole. */
export function skillMatrix(ctx: MatrixContext): HTMLElement {
  const G = geo();
  const tip = card(ctx);
  const inspect = tip.show;
  const world = h('div.sm-world');
  world.append(structure(ctx, G));
  for (const skill of SKILL_IDS) world.append(rootEl(ctx, skill, G.roots[skill], inspect));
  for (const k of G.knots) world.append(knotEl(ctx, k.skill, k.level, k.p, inspect));
  for (const t of G.talents) world.append(...talentEl(ctx, t, inspect));
  world.append(h('div.sm-medal-at', {}, medalEl(ctx)));

  const board = h('div.sm-board', { onmouseleave: () => inspect(null) }, world);
  const fit = () => {
    const w = board.clientWidth;
    const hh = board.clientHeight;
    if (!w || !hh) return false;
    const { box } = G;
    const s = Math.min(MAX_SCALE, (w - 16) / (box.x1 - box.x0), (hh - 16) / (box.y1 - box.y0));
    const cx = (box.x0 + box.x1) / 2;
    const cy = (box.y0 + box.y1) / 2;
    world.style.transform = `translate(${f(w / 2 - cx * s)}px, ${f(hh / 2 - cy * s)}px) scale(${s.toFixed(4)})`;
    return true;
  };
  // Fitted once the board has its size (it is built off the page), and again if the window changes.
  const settle = (tries = 0) => {
    if (fit() || tries > 30) return;
    requestAnimationFrame(() => settle(tries + 1));
  };
  requestAnimationFrame(() => settle());
  if (typeof ResizeObserver !== 'undefined') {
    const watch = new ResizeObserver(() => {
      if (!board.isConnected) return watch.disconnect();
      fit();
    });
    watch.observe(board);
  }
  return board;
}
