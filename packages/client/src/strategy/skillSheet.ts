import './skillSheet.css';
import {
  barLine,
  barsOf,
  BOARD_ABOUT,
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

/** A soldier's bars filled in each line, at a glance, each in its colour, for the lists of soldiers. */
export function skillDigits(skills: Skills): HTMLElement {
  return h(
    'span.sk-digits',
    { title: SKILL_IDS.map((id) => `${SKILLS[id].name}: ${barsOf(skills[id]).filled}`).join(' · ') },
    ...SKILL_IDS.map((id) => {
      const n = barsOf(skills[id]).filled;
      return h(`b.sk-${id}${n ? '' : '.none'}`, {}, String(n));
    }),
  );
}

/** The veterancy as the packs' rank badge (one per step past Novato, none for a Novato), its name on hover. */
export function veterancyBadge(board: readonly CellId[], female = false): HTMLElement {
  const spent = board.length;
  const rank = veterancyRank(spent);
  return h('span.rank', { title: `${vetName(rank, female)} · ${spent} ${spent === 1 ? 'punto gastado' : 'puntos gastados'} en el tablero` }, ...Array.from({ length: rank }, () => h('i')));
}

// ------------------------------------------------------------------ the sheet

/** A line's row: the bars filled, its name, a gauge of the bar being filled with its experience, and what the bars added. */
function barRow(s: SkillsOf, id: SkillId): HTMLElement {
  const def = SKILLS[id];
  const { filled, into, need } = barsOf(s.skills[id]);
  const grown = s.growth[id] ?? 0;
  return h(
    `div.sk-row.sk-${id}`,
    { title: `${def.line}\n${def.stat}: +${grown} % con ${filled === 1 ? 'la barra llena' : `las ${filled} barras llenas`}.` },
    h('b.sk-row-level', { title: 'Barras llenas' }, String(filled)),
    h('span.sk-row-name', {}, def.name),
    h('span.sk-row-gauge', {}, h('i', { style: { '--p': (into / need).toFixed(3) } })),
    h('small.sk-row-xp', {}, `${into}/${need}`),
    h(`small.sk-row-grown${grown ? '' : '.none'}`, { title: def.stat }, grown ? `+${grown} %` : '—'),
  );
}

/**
 * The veterancy (Arturo, 16/10): it rises with the points spent on the board,
 * a rank every RANK_STEP; its name and those points, the will it gives, and
 * how many more points on the board the next rank asks for.
 */
function veterancyEl(s: SkillsOf): HTMLElement {
  const spent = s.board.length;
  const rank = veterancyRank(spent);
  const def = VETERANCY[rank]!;
  const next = VETERANCY[rank + 1];
  const left = next ? next.from - spent : 0;
  return h(
    'div.sk-vet',
    { title: 'La veteranía sube con los puntos gastados en el tablero: un rango cada 5. Cada rango, +10 de voluntad. Desde Veterano, la tercera acción.' },
    h('div.sk-vet-head', {}, h('b', {}, vetName(rank, s.female)), h('span.sk-vet-points', {}, `${spent} ${spent === 1 ? 'punto' : 'puntos'} en el tablero`)),
    h('small.sk-vet-line', {}, def.line),
    h('small.sk-vet-gives', {}, `Voluntad ${veteranWill(rank)} · ${rank >= 3 ? 3 : 2} acciones por turno · ${totalBars(s.skills)} ${totalBars(s.skills) === 1 ? 'barra llena' : 'barras llenas'}`),
    next ? h('small.sk-vet-next', {}, `${left} ${left === 1 ? 'punto más' : 'puntos más'} en el tablero para ${vetName(rank + 1, s.female)}${rank + 1 === 3 ? ': la tercera acción' : ''}.`) : null,
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
      points ? `Tablero de habilidades · ${points === 1 ? 'un punto' : `${points} puntos`}` : 'Tablero de habilidades',
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

export interface TreePanelContext {
  s: SkillsOf;
  /** The viewer's own soldier, alive: it can unlock. */
  editable: boolean;
  /** Whose soldier, for «Solo Ana puede desbloquearla». */
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
  let board = boards.get(s.id);
  if (!board) boards.set(s.id, (board = new HexBoard((cell) => ctx.onUnlock(cell))));
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
      h(`span.hb-points${points ? '.some' : ''}`, { title: 'Se gana un punto cada vez que se llena una barra' }, h('b', {}, String(points)), points === 1 ? ' punto' : ' puntos'),
      h(
        'span.hb-lines',
        {},
        ...SKILL_IDS.map((id) => h(`span.hb-line.sk-${id}`, { title: `${SKILLS[id].name}: ${barsOf(s.skills[id]).filled} barras llenas` }, h('i'), `${SKILLS[id].short} ${barsOf(s.skills[id]).filled}`)),
      ),
    ),
    h('p.service-help', {}, ctx.editable ? 'Pasa el cursor por una casilla para leerla. Mantenla pulsada un segundo para desbloquearla. Arrastra para moverte y usa la rueda para acercarte.' : `Es de ${ctx.owner}: puedes leerlo, pero solo lo desbloquea quien lo lleva.`),
    board.el,
    h('div.alert-actions', {}, h('button.x-btn', { onclick: ctx.onClose }, 'Cerrar')),
  );
}
