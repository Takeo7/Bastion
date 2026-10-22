import {
  FELL_COST,
  actionPrice,
  bastionLevel,
  bastionLevelName,
  bastionLevelThe,
  LEVEL_TEXT,
  levelUpBlocker,
  levelUpLine,
  nextLevel,
  BUILD_TEXT,
  buildCostLine,
  buildingWorks,
  buildShortLine,
  dismantleBack,
  holdingLine,
  holdsLine,
  needsLevelLine,
  placeHint,
  upgradeLine,
  usesLine,
  dismantleBlocker,
  dismantleRefund,
  PIECE_STATE,
  PIECE_TEXT,
  PIECES,
  pieceFigure,
  pieceFigureShort,
  placeBlocker,
  raiseCost,
  rearmCost,
  repairCost,
  type BasePiece,
  type CampaignCommand,
  type CampaignState,
  type PieceId,
  type PieceKind,
  type Vec2,
} from '@bastion/engine';
import { h } from '../ui/dom';
import type { ClearingTool } from './clearing';
import { markNew } from './fresh';
import { pieceIcon } from './pieceIcons';

// Building on the clearing (docs/FASE12.md §11.7): the palette by kind, with
// only what the Bastion's level allows (playtest 20/10 #13: what isn't had yet
// isn't shown; what a level brings says «Nuevo») and what each thing costs in
// sight, and the card of a piece picked on the map, with what can be done to
// it. The pointer's side (the piece about to go down, felling) is clearing.ts.

/** Order within a tab: as the engine lists them (PIECES), which is cheapest-first within each kind more or less. */
const ORDER = Object.keys(PIECES) as PieceId[];

/** What the base has of each: suministros (credits) and piezas. */
function short(c: CampaignState, cost: { supplies: number; parts?: number }): { supplies: number; parts: number } {
  return { supplies: Math.max(0, cost.supplies - c.credits), parts: Math.max(0, (cost.parts ?? 0) - (c.parts ?? 0)) };
}

/**
 * With the other one connected everything on the base goes to a vote (Arturo, 18/10 #55: «todo lo de la base»),
 * one at a time: while one waits for its yes or no, nothing else can be proposed (campaign.ts propose).
 */
export const PENDING = 'Ya hay una propuesta pendiente.';

/** Why `piece` can't be taken up at all (before choosing where): the level, only one, what it costs; a vote waiting. */
export function pieceBlocker(c: CampaignState, piece: PieceId, voting = false): string | null {
  if (voting && c.proposal) return PENDING;
  const def = PIECES[piece];
  if (def.level > bastionLevel(c)) return needsLevelLine(def.level);
  if (def.unique && c.bastion.pieces.some((p) => p.piece === piece)) return 'Ya está en la base.';
  const s = short(c, def.cost);
  if (s.supplies || s.parts) return `${buildShortLine(s.supplies, s.parts)}.`;
  return null;
}

/** Why `piece` can't go at (x, y) turned `rot`: the place first (what the pointer shows), then the rest. */
export function placeBlockerFor(c: CampaignState, piece: PieceId, x: number, y: number, rot: number, voting = false): string | null {
  return placeBlocker(c.bastion, piece, x, y, rot) ?? pieceBlocker(c, piece, voting);
}

/** What goes down in a line by dragging (playtest 18/10 #35): what is built a tile at a time along a wall. */
export const LINE_PIECES: ReadonlySet<PieceId> = new Set<PieceId>(['palisade', 'wall', 'sandbags', 'logs', 'wire']);

/** Why a whole line of `piece` can't go down (all or nothing, as the engine's placeLine): a tile, the level, what it all costs. */
export function lineBlockerFor(c: CampaignState, piece: PieceId, cells: Vec2[], rot: number, voting = false): string | null {
  for (const cell of cells) {
    const why = placeBlocker(c.bastion, piece, cell.x, cell.y, rot);
    if (why) return why;
  }
  if (voting && c.proposal) return PENDING;
  const def = PIECES[piece];
  if (def.level > bastionLevel(c)) return needsLevelLine(def.level);
  const s = short(c, { supplies: def.cost.supplies * cells.length, parts: (def.cost.parts ?? 0) * cells.length });
  return s.supplies || s.parts ? `${buildShortLine(s.supplies, s.parts)}.` : null;
}

/** The line by the pointer while dragging one: how many, what they cost, and that it is proposed (#55); or why not. */
export function lineLine(c: CampaignState, piece: PieceId, cells: Vec2[], partnerHere: boolean, blocked: string | null): string {
  const def = PIECES[piece];
  const cost = { supplies: def.cost.supplies * cells.length, ...(def.cost.parts ? { parts: def.cost.parts * cells.length } : {}) };
  const name = `${PIECE_TEXT[piece].name} × ${cells.length}`;
  return blocked ? `${name} · ${blocked}` : `${name} · ${buildCostLine(cost)}${partnerHere ? ` · ${BUILD_TEXT.vote}` : ''}`;
}

/** Why `trees` trees can't be felled (#41: felling costs FELL_COST each), or null; a vote waiting (#55). */
export function fellBlockerFor(c: CampaignState, trees: number, voting = false): string | null {
  if (voting && c.proposal) return PENDING;
  const s = short(c, { supplies: FELL_COST * trees });
  return s.supplies ? `${buildShortLine(s.supplies)}.` : null;
}

/** The line by the pointer with a piece in hand: what it costs and that it is proposed (#55), or why it doesn't fit. */
export function placeLine(c: CampaignState, piece: PieceId, partnerHere: boolean, blocked: string | null): string {
  const def = PIECES[piece];
  return placeHint(piece, def.cost, partnerHere, blocked ?? undefined);
}

export interface PaletteView {
  c: CampaignState;
  tool: ClearingTool;
  /** The partner, while connected (their name): everything goes to a vote (#55). */
  partner: string | null;
  /** This player's slot: whose proposal is waiting. */
  me: number;
  /** Whether what `key` names came with a level and this player hasn't looked at it yet (fresh.ts). */
  isNew?(key: string): boolean;
}

export interface PaletteHandlers {
  take(piece: PieceId): void;
  fell(): void;
  drop(): void;
  /** The pointer went over something new: its «Nuevo» goes. */
  seen?(key: string): void;
}

/** The palette's panels under Talar, in order (Ana, 21/10 #40): what is built, then what defends, the automatic ones too. */
const GROUPS: { title: string; kinds: PieceKind[] }[] = [
  { title: BUILD_TEXT.tabs.building, kinds: ['building'] },
  { title: BUILD_TEXT.tabs.defence, kinds: ['defence', 'trap'] },
];

/** Every piece the palette can show at `level` (the caravan is never built), for their pictures (pieceIcons.ts). */
export function palettePieces(level: number): PieceId[] {
  return ORDER.filter((id) => id !== 'caravan' && PIECES[id].level <= level);
}

/**
 * The palette (Ana, 21/10 #35, #40): three panels one over the other, each its own steel sheet. Talar; then the
 * buildings, and the defences with the automatic ones, each a grid of pictures with the name and the cost under,
 * and what it does on hover. Only what the Bastion's level allows (#13); a unique one already up, ticked; one the
 * stores can't pay, its cost in red.
 */
export function palette(v: PaletteView, on: PaletteHandlers): HTMLElement[][] {
  const { c, tool } = v;
  const level = bastionLevel(c);
  const felling = tool.kind === 'fell';
  const inHand = tool.kind === 'place' ? tool.piece : null;
  const tile = (id: PieceId) => {
    const def = PIECES[id];
    const built = !!def.unique && c.bastion.pieces.some((p) => p.piece === id);
    const why = built ? null : pieceBlocker(c, id);
    const pic = pieceIcon(id);
    const item = h(
      `button.piece-tile${built ? '.built' : why ? '.short' : ''}${inHand === id ? '.on' : ''}`,
      {
        'data-piece': id,
        disabled: built,
        title: [PIECE_TEXT[id].line, why ?? ''].filter(Boolean).join('\n'),
        onclick: () => (inHand === id ? on.drop() : on.take(id)),
      },
      h('span.piece-pic', {}, pic ? h('img', { src: pic, alt: '' }) : h('i.piece-pic-wait')),
      h('b.piece-name', {}, PIECE_TEXT[id].name),
      built ? h('span.piece-cost.built', {}, '✓') : costEl(def.cost),
    );
    return markNew(item, !!v.isNew?.(`piece:${id}`), () => on.seen?.(`piece:${id}`));
  };
  // A proposal waiting (with the other one here): nothing else goes down meanwhile, said in red over the grids.
  const pending = !!v.partner && !!c.proposal ? h('p.build-vote.x-why', {}, `${PENDING} ${c.proposal!.by === v.me ? `Espera a que ${v.partner} conteste.` : 'Contesta arriba.'}`) : null;
  const keys = (kinds: PieceKind[]) => (inHand && kinds.includes(PIECES[inHand].kind) ? h('p.build-keys', {}, `${holdingLine(inHand)}${LINE_PIECES.has(inHand) ? ' Arrastra para poner una fila.' : ''}`) : null);
  const fell: (HTMLElement | null)[] = [
    h(
      `button.fell-tile${felling ? '.on' : ''}`,
      { title: BUILD_TEXT.felling, onclick: () => (felling ? on.drop() : on.fell()) },
      h('span.fell-ico', { 'aria-hidden': 'true' }),
      h('b', {}, BUILD_TEXT.fell),
      costEl({ supplies: FELL_COST }, 'por árbol'),
    ),
    felling ? h('p.build-keys', {}, BUILD_TEXT.felling) : null,
  ];
  const groups = GROUPS.map(({ title, kinds }, i): (HTMLElement | null)[] => {
    const ids = palettePieces(level).filter((id) => kinds.includes(PIECES[id].kind));
    return [h('div.x-head', {}, title), i === 0 ? pending : null, h('div.piece-grid', {}, ...ids.map(tile)), keys(kinds)];
  });
  return [fell, ...groups].map((parts) => parts.filter((x): x is HTMLElement => !!x));
}

/** A cost as the stores show it: «40 §», and the parts after with their icon; `per`, small after it. */
function costEl(cost: { supplies: number; parts?: number }, per = ''): HTMLElement {
  return h(
    'span.piece-cost',
    {},
    h('span.cost-supplies', {}, `${cost.supplies}`),
    cost.parts ? h('span.cost-parts', { title: cost.parts === 1 ? '1 pieza' : `${cost.parts} piezas` }, `${cost.parts}`) : null,
    per ? h('small', {}, per) : null,
  );
}

export interface CardView {
  c: CampaignState;
  piece: BasePiece;
  /** «Desmontar» was pressed once: the second press does it. */
  dismantling: boolean;
  /** The partner, while connected (their name): every action goes to a vote (#55). */
  partner: string | null;
  /** Whether what `key` names came with a level and this player hasn't looked at it yet (fresh.ts). */
  isNew?(key: string): boolean;
}

export interface CardHandlers {
  send(cmd: CampaignCommand): void;
  dismantle(ask: boolean): void;
  close(): void;
  /** The pointer went over something new: its «Nuevo» goes. */
  seen?(key: string): void;
}

/**
 * The card of a piece picked on the clearing: what it gives (Ana, 21/10 #42: the figure first, big, and the levels
 * under it), how it stands, and mending, raising, setting again or taking it down. No kicker, no «Junto a», no
 * notes on what the buttons already say (#39, #41).
 */
export function pieceCard(v: CardView, on: CardHandlers): HTMLElement[] {
  const { c, piece: p } = v;
  const def = PIECES[p.piece];
  const text = PIECE_TEXT[p.piece];
  const level = p.level ?? 1;
  const building = def.kind === 'building' && p.piece !== 'caravan';
  const figure = building ? pieceFigure(p.piece, level) : null;
  const out: (HTMLElement | null)[] = [
    // The Bastion itself by what it is now (Lore, 20/10: «Caravana», «Casa móvil»…).
    h('h2.x-title', {}, p.piece === 'caravan' ? bastionLevelName(bastionLevel(c)) : text.name, h('button.x-btn.small.piece-close', { title: 'Soltarla (Esc)', onclick: () => on.close() }, '✕')),
    figure ? h('p.piece-figure', {}, figure) : h('p.x-muted', {}, text.line),
    building ? ladder(p.piece, level) : null,
    building && text.levels?.[level - 1] ? h('p.piece-level-line', {}, text.levels[level - 1]) : null,
  ];

  // How it stands: what it holds, its uses.
  const hurt = p.hp < def.hp;
  out.push(
    h(
      `div.piece-hp${p.hp * 2 <= def.hp ? '.low' : ''}`,
      { title: 'Lo que aguanta los golpes de los muertos' },
      h('i', {}, h('b', { style: { width: `${Math.round((p.hp / def.hp) * 100)}%` } })),
      h('em', {}, holdsLine(p.hp, def.hp)),
    ),
  );
  if (def.charges) out.push(h('p.piece-uses', {}, usesLine(p.charges ?? 0, def.charges)));
  const state = def.kind === 'building' && !buildingWorks(p) ? PIECE_STATE.brokenLine : def.charges && !(p.charges ?? 0) ? (p.piece === 'turret' ? PIECE_STATE.silentLine : PIECE_STATE.spentLine) : null;
  if (state) out.push(h('p.x-why', {}, state));

  // What can be done to it: nothing while a vote waits (#55), said on each.
  const actions: (HTMLElement | null)[] = [];
  const pending = v.partner && c.proposal ? PENDING : '';
  if (hurt) {
    const cost = repairCost(p);
    const s = short(c, cost);
    const why = pending || (s.supplies || s.parts ? `${buildShortLine(s.supplies, s.parts)}.` : '');
    actions.push(button(actionPrice('repair', cost), why, () => on.send({ type: 'mend', piece: p.id })));
  }
  if (building) {
    const next = raiseCost(p);
    if (typeof next === 'string') actions.push(button(BUILD_TEXT.upgrade, next, () => {}));
    // An upgrade the Bastion's level doesn't allow yet isn't shown (#13); one a level has just brought says «Nuevo».
    else if (bastionLevel(c) >= next.level) {
      const why = !buildingWorks(p) ? PIECE_STATE.brokenLine : c.credits < next.cost ? `${buildShortLine(next.cost - c.credits)}.` : pending;
      const key = `raise:${p.piece}:${level + 1}`;
      actions.push(markNew(button(actionPrice('upgrade', { supplies: next.cost }), why, () => on.send({ type: 'raise', piece: p.id }), upgradeLine(p.piece, level + 1)), !!v.isNew?.(key), () => on.seen?.(key)));
    }
  }
  // The Bastion itself goes up here (Arturo, 20/10: Campaña is gone): to what it becomes by name, never what that
  // brings (the alert says it, in icons); what is missing under the button; a vote with the partner, as the rest.
  if (p.piece === 'caravan') {
    const up = nextLevel(c);
    if (!up) out.push(h('p.x-muted', {}, LEVEL_TEXT.top));
    else {
      const why = pending || (levelUpBlocker(c) ?? '');
      const progress = `${up.flown} de ${up.missions} misiones para ${bastionLevelThe(up.level)}`;
      actions.push(button(`Subir a ${bastionLevelThe(up.level)} · ${up.cost} §`, why, () => on.send({ type: 'levelUp' }), progress, levelUpLine(up.level, up.cost)));
    }
  }
  if (def.charges && (p.charges ?? 0) < def.charges) {
    const parts = rearmCost(p);
    const why = pending || ((c.parts ?? 0) < parts ? `${buildShortLine(0, parts - (c.parts ?? 0))}.` : '');
    actions.push(button(actionPrice('rearm', { parts }), why, () => on.send({ type: 'rearm', piece: p.id })));
  }
  const cant = dismantleBlocker(p);
  if (!cant) {
    const back = dismantleRefund(p);
    actions.push(
      v.dismantling
        ? h(
            'div.piece-confirm',
            {},
            h('span', {}, `${BUILD_TEXT.dismantleAsk} ${dismantleBack(back)}`),
            h('button.x-btn.small.bad', { onclick: () => on.send({ type: 'dismantle', piece: p.id }) }, BUILD_TEXT.dismantleYes),
            h('button.x-btn.small', { onclick: () => on.dismantle(false) }, BUILD_TEXT.dismantleNo),
          )
        : button(actionPrice('dismantle', { supplies: back }), pending, () => on.dismantle(true)),
    );
  }
  out.push(h('div.piece-actions', {}, ...actions));
  return out.filter((x): x is HTMLElement => !!x);
}

/** A building's levels in a row, what each gives, the one it is at lit (#42): «Nivel 1 +5 · Nivel 2 +10 · Nivel 3 +15». */
function ladder(piece: PieceId, level: number): HTMLElement | null {
  const top = PIECE_TEXT[piece].levels?.length ?? 3;
  const steps = Array.from({ length: top }, (_, i) => ({ n: i + 1, figure: pieceFigureShort(piece, i + 1) }));
  if (!steps.some((s) => s.figure)) return null;
  return h(
    'div.piece-ladder',
    {},
    ...steps.map((s) => h(`span.piece-step${s.n === level ? '.now' : s.n < level ? '.past' : ''}`, {}, h('small', {}, `Nivel ${s.n}`), h('b', {}, s.figure ?? '—'))),
  );
}

/** An action of the card: off with its reason under it when it can't be done; `tip` under it otherwise, `title` on hover. */
function button(label: string, why: string, act: () => void, tip = '', title = tip): HTMLElement {
  return h(
    'div.piece-action',
    {},
    h('button.x-btn', { disabled: !!why, title, onclick: act }, label),
    why ? h('small.x-why', {}, why) : tip ? h('small.piece-tip', {}, tip) : null,
  );
}
