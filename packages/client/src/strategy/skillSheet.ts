import './skillSheet.css';
import {
  barLine,
  barReward,
  barsOf,
  BOARD_ABOUT,
  boardRole,
  rankReward,
  RANK_STEP,
  THIRD_ACTION_RANK,
  displayName,
  learnedLine,
  pointsLeft,
  SKILL_IDS,
  SKILLS,
  totalBars,
  veterancyRank,
  VETERANCY,
  veteranWill,
  type CampaignSoldier,
  type CellId,
  type Growth,
  type ReportSoldier,
  type SkillId,
  type Skills,
} from '@bastion/engine';
import { h } from '../ui/dom';
import { HexBoard } from './hexBoard';

// What the Cuartel shows of a soldier's experience (docs/FASE10.md, «Lo tuyo»
// of UI): the six bars, each with how far into the next one it is, how many it
// has filled and what they have added to its stat; the veterancy; the points
// for the board; and the hex board itself in its panel (hexBoard.ts), where
// cells are bought with the campaign's `unlock`. And what a mission taught,
// for the debrief's cards.

/** What the sheet, the board and the lists take of a soldier. */
export interface SkillsOf {
  id: string;
  name: string;
  skills: Skills;
  growth: Growth;
  board: CellId[];
  /** A woman: the veterancy's names go in the feminine. */
  female?: boolean;
}

/** What the sheet and the board take of a campaign soldier. */
export function skillsOf(s: Pick<CampaignSoldier, 'id' | 'name' | 'nickname' | 'skills' | 'growth' | 'board' | 'appearance'>): SkillsOf {
  return { id: s.id, name: displayName(s.name, s.nickname), skills: s.skills, growth: s.growth, board: s.board, female: s.appearance?.sex === 'f' };
}

/** A step of the veterancy by its index, in the feminine for a woman (Lore). */
export const vetName = (rank: number, female = false) => (female ? VETERANCY[rank]!.female : VETERANCY[rank]!.name);

// ------------------------------------------------------------------ the lists

/**
 * A soldier's role (playtest 16/10 #14): a dot in the colour of the line it
 * has bought most cells of on its board (board.ts boardRole); a hollow one
 * before it has bought any. No figures by the names.
 */
export function roleDot(board: readonly CellId[]): HTMLElement {
  const role = boardRole(board);
  return h(`span.role-dot${role ? `.sk-${role}` : '.none'}`, { title: role ? `Rol: ${SKILLS[role].name}` : 'Sin rol todavía: no ha seleccionado nada en el tablero' });
}

/** The veterancy as the packs' rank badge (one per step past Novato, none for a Novato), its name on hover. */
export function veterancyBadge(board: readonly CellId[], female = false): HTMLElement {
  const spent = board.length;
  const rank = veterancyRank(spent);
  return h('span.rank', { title: `${vetName(rank, female)} · ${spent} ${spent === 1 ? 'punto gastado' : 'puntos gastados'} en el tablero` }, ...Array.from({ length: rank }, () => h('i')));
}

// ------------------------------------------------------------------ the sheet

/** A bar in segments (playtest 16/10 #15): as many as `need`, the first `into` of them full; no figures. */
function segments(need: number, into: number): HTMLElement {
  return h('span.segs', { 'aria-label': `${into} de ${need}` }, ...Array.from({ length: need }, (_, i) => h(`i${i < into ? '.on' : ''}`)));
}

/**
 * A line's row: its name (and what its bars have added to its stat, small),
 * the bar being filled in segments, and at its end what filling it gives
 * (Lore's barReward, #16).
 */
function barRow(s: SkillsOf, id: SkillId): HTMLElement {
  const def = SKILLS[id];
  const { into, need } = barsOf(s.skills[id]);
  const grown = s.growth[id] ?? 0;
  const reward = barReward(id);
  return h(
    `div.sk-row.sk-${id}`,
    { title: def.line },
    h('span.sk-row-name', {}, h('i.sk-row-dot'), def.name, grown ? h('small', { title: def.stat }, ` +${grown} %`) : null),
    segments(need, into),
    h('small.sk-row-gives', { title: reward.long }, reward.short),
  );
}

/**
 * The veterancy as a bar (Arturo, 16/10: a rank every RANK_STEP points spent
 * on the board): its name, the points of this rank in segments, and at its
 * end what the next rank gives (Lore's rankReward); full at the top.
 */
export function veterancyBar(board: readonly CellId[], female = false): HTMLElement {
  const spent = board.length;
  const rank = veterancyRank(spent);
  const reward = rankReward(rank, female ? 'f' : 'm');
  const into = reward ? spent - VETERANCY[rank]!.from : RANK_STEP;
  return h(
    'div.sk-vet-bar',
    { title: 'La veteranía sube con los puntos gastados en el tablero: un rango cada 5.' },
    h('b', {}, vetName(rank, female)),
    segments(RANK_STEP, into),
    h('small.sk-row-gives', { title: reward?.long ?? 'No hay rango más alto.' }, reward?.short ?? 'Rango máximo'),
  );
}

/** The veterancy on the sheet: its bar, its line, and what it gives now. */
function veterancyEl(s: SkillsOf): HTMLElement {
  const rank = veterancyRank(s.board.length);
  return h(
    'div.sk-vet',
    {},
    veterancyBar(s.board, s.female),
    h('small.sk-vet-line', {}, VETERANCY[rank]!.line),
    h('small.sk-vet-gives', {}, `Voluntad ${veteranWill(rank)} · ${rank >= THIRD_ACTION_RANK ? 3 : 2} acciones por turno`),
  );
}

/**
 * The experience block of a soldier's sheet in the Cuartel: the six bars, the
 * veterancy and the button to the board (`onOpen`; it calls for attention
 * while there are points to spend, if the soldier is the viewer's).
 */
export function skillsBlock(s: SkillsOf, mine: boolean, onOpen: () => void): HTMLElement[] {
  const points = pointsLeft(s.skills, s.board);
  return [
    h('div.x-group', {}, 'Experiencia'),
    h('div.sk-rows', {}, ...SKILL_IDS.map((id) => barRow(s, id))),
    veterancyEl(s),
    h(
      `button.x-btn${points && mine ? '.primary.waiting' : ''}`,
      { onclick: onOpen },
      points ? `Tablero de habilidades · ${points === 1 ? 'un punto' : `${points} puntos`} por gastar` : 'Tablero de habilidades',
    ),
  ];
}

// ------------------------------------------------------------------ the report

/**
 * What a mission taught a soldier, for its card in the debrief: a tag per
 * line it learnt in («+5 DIS», Lore's whole line in its title), a badge per
 * line whose bars filled with what they added («▲ DISPARO +7 %»), and the
 * points they give.
 */
export function learnedTags(e: Pick<ReportSoldier, 'name' | 'learned' | 'deeds' | 'bars'>): HTMLElement[] {
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
  for (const bar of e.bars ?? []) {
    const percent = bar.growth.reduce((a, b) => a + b, 0);
    out.push(h(`small.promo.sk-up.sk-${bar.skill}`, { title: barLine(e.name, bar.skill, bar.growth) }, `▲ ${SKILLS[bar.skill].name.toUpperCase()} +${percent} %`));
  }
  const points = (e.bars ?? []).reduce((n, b) => n + b.growth.length, 0);
  if (points) out.push(h('small.sk-talent-note', {}, points === 1 ? 'Un punto para el tablero' : `${points} puntos para el tablero`));
  return out;
}

/** The bars a mission filled, in Lore's words, for the debrief's head: «Ana llena una barra de Disparo: +7 %…». */
export function barLines(squad: Pick<ReportSoldier, 'name' | 'status' | 'bars'>[]): string[] {
  return squad.filter((e) => e.status !== 'dead' && e.status !== 'lost').flatMap((e) => (e.bars ?? []).map((b) => barLine(e.name, b.skill, b.growth)));
}

// ------------------------------------------------------------------ the board

/** Each soldier's board, kept while the screen redraws (its camera, its wave, the cell on its card). */
const boards = new Map<string, HexBoard>();
/**
 * The `onUnlock` of the panel drawn last for each soldier. The board keeps the
 * one it was built with, so a board first opened from the battle sheet (which
 * buys nothing) bought nothing at the base until the app restarted (18/10, #38, #48).
 */
const unlocks = new Map<string, (cell: CellId) => void>();

export interface TreePanelContext {
  s: SkillsOf;
  /** The viewer's own soldier, alive: it can unlock. */
  editable: boolean;
  /** Whose soldier, for «Solo Ana puede seleccionarla». */
  owner: string;
  /** Its bust (portraits.ts), by its name; the frame stays empty until it is drawn. */
  portrait: Promise<string> | null;
  /** The campaign's `unlock`. */
  onUnlock: (cell: CellId) => void;
  onClose: () => void;
}

/**
 * The «Tablero de habilidades» panel: the soldier, its points and its six
 * lines' bars, the hex board with its card, and «Cerrar». The board is the
 * same element across redraws (hexBoard.ts), so nothing it is doing is lost.
 */
export function treePanel(ctx: TreePanelContext): HTMLElement {
  const { s } = ctx;
  unlocks.set(s.id, ctx.onUnlock);
  let board = boards.get(s.id);
  if (!board) boards.set(s.id, (board = new HexBoard((cell) => unlocks.get(s.id)?.(cell))));
  board.update({ skills: s.skills, board: s.board, editable: ctx.editable, owner: ctx.owner, describe: (id) => BOARD_ABOUT[id] });
  const points = pointsLeft(s.skills, s.board);
  const rank = veterancyRank(s.board.length);
  const bust = h('span.hb-bust');
  void ctx.portrait?.then((url) => bust.style.setProperty('--portrait', `url('${url}')`)).catch(() => undefined);
  return h(
    'div.x-panel.wide.service.sm-host',
    {},
    h('div.alert-kicker', {}, `TABLERO DE HABILIDADES · ${vetName(rank, s.female).toUpperCase()}`),
    h(
      'div.hb-head',
      {},
      bust,
      h('h2.x-title', {}, s.name),
      veterancyBar(s.board, s.female),
      h(`span.hb-points${points ? '.some' : ''}`, { title: 'Un punto de habilidad cada vez que se llena una barra' }, h('b', {}, String(points)), points === 1 ? ' punto por gastar' : ' puntos por gastar'),
    ),
    board.el,
    h('div.alert-actions', {}, h('button.x-btn', { onclick: ctx.onClose }, 'Cerrar')),
  );
}
