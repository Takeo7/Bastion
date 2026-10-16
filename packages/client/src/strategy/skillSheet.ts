import {
  displayName,
  learnedLine,
  levelUpLine,
  pendingPicks,
  picksBlocker,
  SKILL_IDS,
  SKILL_MAX,
  SKILL_TALENTS,
  SKILLS,
  skillLevel,
  veteranAp,
  veteranHp,
  veterancy,
  veterancyRank,
  VETERANCY,
  veteranWill,
  type CampaignSoldier,
  type PerkId,
  type ReportSoldier,
  type Skills,
} from '@bastion/engine';
import './skillSheet.css';
import { uiSound } from '../audio/ui';
import { h } from '../ui/dom';
import { cellState, levelProgress, levelXp, mateOf, skillMatrix, vetName } from './skillMatrix';

// What the Cuartel shows of a soldier's skills (docs/FASE9.md §8, «la ficha»):
// the four skills with their level and the experience to the next, the
// veterancy and what its next step brings, the talents kept and those waiting
// for a pick; and the «Árbol de habilidades» panel, where the pairs are picked
// as a draft and sent whole with «Guardar» (the engine's `picks` command,
// checked first with picksBlocker). The tree itself is skillMatrix.ts.

/** What the sheet and the tree take of a campaign soldier. */
export function skillsOf(s: Pick<CampaignSoldier, 'id' | 'name' | 'nickname' | 'skills' | 'picks' | 'appearance'>): SkillsOf {
  return { id: s.id, name: displayName(s.name, s.nickname), skills: s.skills, picks: s.picks, female: s.appearance?.sex === 'f' };
}

/** What both take of a soldier. */
export interface SkillsOf {
  id: string;
  name: string;
  skills: Skills;
  picks: PerkId[];
  /** A woman: the veterancy's names go in the feminine. */
  female?: boolean;
}

// ------------------------------------------------------------------ the lists

/** A soldier's four levels at a glance, each in its skill's colour, for the lists of soldiers. */
export function skillDigits(skills: Skills): HTMLElement {
  return h(
    'span.sk-digits',
    { title: SKILL_IDS.map((id) => `${SKILLS[id].name} ${skillLevel(skills[id])}`).join(' · ') },
    ...SKILL_IDS.map((id) => h(`b.sk-${id}${skillLevel(skills[id]) ? '' : '.none'}`, {}, String(skillLevel(skills[id])))),
  );
}

/** The veterancy as the packs' rank badge (one per step past Novato, none for a Novato), its name on hover. */
export function veterancyBadge(skills: Skills, female = false): HTMLElement {
  const points = veterancy(skills);
  const rank = veterancyRank(points);
  const title = `${vetName(rank, female)} · ${points} de veteranía`;
  return h('span.rank', { title }, ...Array.from({ length: rank }, () => h('i')));
}

// ------------------------------------------------------------------ the sheet

/** «Tiro 2 · 11/15» with its gauge, for the soldier's sheet. */
function skillRow(skills: Skills, skill: (typeof SKILL_IDS)[number]): HTMLElement {
  const xp = skills[skill];
  const level = skillLevel(xp);
  const def = SKILLS[skill];
  return h(
    `div.sk-row.sk-${skill}`,
    { title: `${def.line}\n${def.gives}` },
    h('b.sk-row-level', {}, String(level)),
    h('span.sk-row-name', {}, def.name),
    h('span.sk-row-gauge', {}, h('i', { style: { '--p': levelProgress(xp).toFixed(3) } })),
    h('small.sk-row-xp', {}, level >= SKILL_MAX ? 'Al máximo' : `${xp}/${levelXp(level + 1)}`),
  );
}

/** The veterancy: its name and sum, what it gives now, and what the next step brings. */
function veterancyEl(skills: Skills, female = false): HTMLElement {
  const points = veterancy(skills);
  const rank = veterancyRank(points);
  const def = VETERANCY[rank]!;
  const next = VETERANCY[rank + 1];
  const gives = `Salud ${veteranHp(points)} · Voluntad ${veteranWill(points)} · ${veteranAp(points)} acciones por turno`;
  return h(
    'div.sk-vet',
    { title: 'La veteranía es la suma de los niveles de las cuatro habilidades. Salud: 5, y +1 cada 4 puntos. Voluntad: 40, y +3 por punto. Desde Veterano, la tercera acción.' },
    h('div.sk-vet-head', {}, h('b', {}, vetName(rank, female)), h('span.sk-vet-points', {}, `${points} de veteranía`)),
    h('small.sk-vet-line', {}, def.line),
    h('small.sk-vet-gives', {}, gives),
    next ? h('small.sk-vet-next', {}, `${next.from - points} más para ${vetName(rank + 1, female)}${next.from === 10 ? ': la tercera acción' : ''}.`) : null,
  );
}

/**
 * The skills block of a soldier's sheet in the Cuartel: skills, veterancy,
 * talents and the button to the tree (`onOpen`; it calls for attention while a
 * pair waits, if the soldier is the viewer's).
 */
export function skillsBlock(s: SkillsOf, mine: boolean, onOpen: () => void): HTMLElement[] {
  const pending = pendingPicks(s.skills, s.picks);
  const picked = s.picks.map((id) => SKILL_TALENTS[id]).filter((d) => !!d);
  return [
    h('div.x-group', {}, 'Habilidades'),
    h('div.sk-rows', {}, ...SKILL_IDS.map((id) => skillRow(s.skills, id))),
    veterancyEl(s.skills, s.female),
    h('div.x-group', {}, 'Talentos'),
    picked.length || pending.length
      ? h(
          'p.x-chips',
          {},
          ...picked.map((d) => h(`span.x-chip.sk-chip.sk-${d.skill}`, { title: d.line }, d.name)),
          ...pending.map((p) =>
            h(
              'span.x-chip.warn',
              { title: `${SKILL_TALENTS[p.pair[0]]!.name} o ${SKILL_TALENTS[p.pair[1]]!.name}` },
              `Por elegir: ${SKILLS[p.skill].name} ${p.level}`,
            ),
          ),
        )
      : h('p.x-muted', {}, 'Se elige un talento en los niveles 2 y 4 de cada habilidad.'),
    h(
      `button.x-btn${pending.length && mine ? '.primary.waiting' : ''}`,
      { onclick: onOpen },
      pending.length && mine ? `Árbol de habilidades · ${pending.length === 1 ? 'un talento' : `${pending.length} talentos`} por elegir` : 'Árbol de habilidades',
    ),
  ];
}

// ------------------------------------------------------------------ the report

/**
 * What a mission taught a soldier, for its card in the debrief: a tag per
 * skill it learnt in («+3 TIR», Lore's whole line in its title) and a badge
 * per level gone up («▲ TIRO 3»), saying when it opens a talent to pick.
 */
export function learnedTags(e: Pick<ReportSoldier, 'learned' | 'deeds' | 'levels'>): HTMLElement[] {
  const gained = SKILL_IDS.filter((id) => (e.learned?.[id] ?? 0) > 0);
  const out: HTMLElement[] = [];
  if (gained.length)
    out.push(
      h(
        'span.sk-learned',
        {},
        ...gained.map((id) => h(`span.sk-tag.sk-${id}`, { title: learnedLine(id, e.learned![id], e.deeds ?? {}) }, `+${e.learned![id]} ${SKILLS[id].short}`)),
      ),
    );
  for (const up of e.levels ?? []) out.push(h(`small.promo.sk-up.sk-${up.skill}`, {}, `▲ ${SKILLS[up.skill].name.toUpperCase()} ${up.to}`));
  // A level 2 or 4 reached: a talent waits in the Cuartel.
  if ((e.levels ?? []).some((up) => [2, 4].some((at) => up.from < at && up.to >= at))) out.push(h('small.sk-talent-note', {}, 'Talento por elegir'));
  return out;
}

/** The levels a mission brought, in Lore's words, for the debrief's head: «Ana Ríos sube a Tiro 3.». */
export function levelUpLines(squad: Pick<ReportSoldier, 'name' | 'status' | 'levels'>[]): string[] {
  return squad.filter((e) => e.status !== 'dead' && e.status !== 'lost').flatMap((e) => (e.levels ?? []).map((up) => levelUpLine(e.name, up.skill, up.to)));
}

// ------------------------------------------------------------------ the tree

/** Each soldier's draft: the picks laid on the tree, kept while the screen redraws, sent only with «Guardar». */
const drafts = new Map<string, PerkId[]>();
/** «GUARDADO» stamped on a soldier's panel until then (performance.now()). */
const stamps = new Map<string, number>();

/** The draft as it still stands: each pick on a pair its levels open and nobody has saved a pick on. */
function draftOf(s: SkillsOf): PerkId[] {
  return (drafts.get(s.id) ?? []).filter((id) => {
    const st = cellState(s.skills, s.picks, [], id);
    return st === 'open';
  });
}

export interface TreePanelContext {
  s: SkillsOf;
  /** The viewer's own soldier, alive: it can pick. */
  editable: boolean;
  /** Its bust, for the centre. */
  portrait: Promise<string> | null;
  /** «Guardar»: the whole list, the saved ones and this draft's (the `picks` command). */
  onSave: (picks: PerkId[]) => void;
  onClose: () => void;
  /** A line on the screen's toasts. */
  toast?: (text: string) => void;
}

/**
 * The «Árbol de habilidades» panel: the soldier, what waits, the tree and
 * «Guardar». It redraws itself as the draft changes; a redraw of the whole
 * screen keeps the draft (drafts).
 */
export function treePanel(ctx: TreePanelContext): HTMLElement {
  const host = h('div.x-panel.wide.service.sm-host');
  const draw = () => {
    const { s, editable } = ctx;
    const draft = draftOf(s);
    const pending = pendingPicks(s.skills, s.picks);
    const waiting = pending.filter((p) => !p.pair.some((id) => draft.includes(id)));
    const all = [...s.picks, ...draft];
    const blocked = draft.length ? picksBlocker(s.skills, all) : null;
    const setDraft = (next: PerkId[]) => {
      drafts.set(s.id, next);
      draw();
    };
    const tree = skillMatrix({
      id: s.id,
      portrait: ctx.portrait,
      skills: s.skills,
      saved: s.picks,
      draft,
      editable,
      female: s.female,
      // One per pair: picking a talent lets go of its mate.
      onPick: (id) => setDraft([...draft.filter((x) => x !== mateOf(id)), id]),
      onUnpick: (id) => setDraft(draft.filter((x) => x !== id)),
    });
    const save = () => {
      if (!draft.length || blocked) return;
      // The draft stays until the soldier comes back with it saved (draftOf drops what is saved), or for another go if refused.
      ctx.onSave(all);
      stamps.set(s.id, performance.now() + 2600);
      uiSound('stamp');
      ctx.toast?.(`Guardado para ${s.name}: ${draft.map((id) => SKILL_TALENTS[id]!.name).join(', ')}.`);
      draw();
      window.setTimeout(draw, 2700);
    };
    const close = () => {
      drafts.delete(s.id);
      ctx.onClose();
    };
    const stamped = (stamps.get(s.id) ?? 0) > performance.now();
    let help: string | null = null;
    if (editable && pending.length) {
      help = waiting.length
        ? `${waiting.length === 1 ? 'Tiene un talento por elegir' : `Tiene ${waiting.length} talentos por elegir`}: ${waiting.map((p) => `${SKILLS[p.skill].name} ${p.level}`).join(', ')}. Uno de cada pareja, para siempre; nada cuenta hasta que pulses «Guardar».`
        : 'Todo elegido. Pulsa «Guardar» para apuntarlo; después ya no se cambia.';
    }
    host.replaceChildren(
      ...[
        stamped ? h('div.saved-stamp', {}, 'GUARDADO') : null,
        h('div.alert-kicker', {}, `ÁRBOL DE HABILIDADES · ${vetName(veterancyRank(veterancy(s.skills)), s.female).toUpperCase()}`),
        h('h2.x-title', {}, s.name),
        help ? h('p.service-help', {}, help) : null,
        tree,
        blocked ? h('p.x-why', {}, blocked) : null,
        h(
          'div.alert-actions',
          {},
          editable && pending.length ? h('button.x-btn.primary', { disabled: !draft.length || !!blocked, onclick: save }, draft.length ? `Guardar (${draft.length})` : 'Guardar') : null,
          editable && draft.length ? h('button.x-btn', { onclick: () => setDraft([]) }, 'Deshacer todo') : null,
          h('button.x-btn', { onclick: close }, draft.length ? 'Cerrar sin guardar' : 'Cerrar'),
        ),
      ].filter((x): x is HTMLElement => !!x),
    );
  };
  draw();
  return host;
}
