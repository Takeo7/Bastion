import {
  FELL_COST,
  actionPrice,
  bastionLevel,
  besideLine,
  besidePieces,
  BUILD_TEXT,
  buildCostLine,
  buildingKicker,
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

// Building on the clearing (docs/FASE12.md §11.7): the palette by kind, with
// what is locked by the Bastion's level in grey and what each thing costs in
// sight, and the card of a piece picked on the map, with what can be done to
// it. The pointer's side (the piece about to go down, felling) is clearing.ts.

/** The palette's tabs, in order. */
const KINDS: PieceKind[] = ['building', 'defence', 'trap'];

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

/** The palette's and the card's note while the other one is here: what is done is proposed (#55). */
export function voteNote(partner: string): string {
  return `Con ${partner} en la partida, todo se propone: se hace y se paga cuando diga que sí.`;
}

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
  kind: PieceKind;
  tool: ClearingTool;
  /** The partner, while connected (their name): everything goes to a vote (#55). */
  partner: string | null;
  /** This player's slot: whose proposal is waiting. */
  me: number;
}

export interface PaletteHandlers {
  kind(k: PieceKind): void;
  take(piece: PieceId): void;
  fell(): void;
  drop(): void;
}

/** The palette: the felling tool, the tabs, and each piece of the tab with its cost or why not. */
export function palette(v: PaletteView, on: PaletteHandlers): HTMLElement[] {
  const { c, tool } = v;
  const level = bastionLevel(c);
  const felling = tool.kind === 'fell';
  const inHand = tool.kind === 'place' ? tool.piece : null;
  const items = ORDER.filter((id) => PIECES[id].kind === v.kind && id !== 'caravan').map((id) => {
    const def = PIECES[id];
    const locked = def.level > level;
    const why = pieceBlocker(c, id);
    const state = locked ? '.locked' : why ? '.short' : '';
    return h(
      `button.piece-item${state}${inHand === id ? '.on' : ''}`,
      {
        'data-piece': id,
        title: `${PIECE_TEXT[id].line}${why ? `\n${why}` : ''}`,
        onclick: () => (inHand === id ? on.drop() : on.take(id)),
      },
      h('b.piece-name', {}, PIECE_TEXT[id].name),
      h('span.piece-cost', {}, locked ? `Nivel ${def.level}` : costLabel(def.cost)),
      h('small.piece-line', {}, locked ? needsLevelLine(def.level) : why && !/^Falta/.test(why) ? why : PIECE_TEXT[id].line),
    );
  });
  // Said once for the whole palette, not under every piece (#55); a vote waiting, in red, since nothing goes down meanwhile.
  const pending = !!v.partner && !!c.proposal;
  return ([
    h(
      'div.x-head.build-head',
      { title: BUILD_TEXT.paletteHelp },
      BUILD_TEXT.build,
      h(`button.x-btn.small.fell-tool${felling ? '.on' : ''}`, { title: `${BUILD_TEXT.fellTip} Arrastra un rectángulo sobre el pinar.`, onclick: () => (felling ? on.drop() : on.fell()) }, BUILD_TEXT.fell),
    ),
    v.partner ? h(`p.build-vote${pending ? '.x-why' : ''}`, {}, pending ? `${PENDING} ${c.proposal!.by === v.me ? `Espera a que ${v.partner} conteste.` : 'Contesta arriba.'}` : voteNote(v.partner)) : null,
    h(
      'div.build-tabs',
      {},
      ...KINDS.map((k) => h(`button.build-tab${v.kind === k ? '.on' : ''}`, { onclick: () => on.kind(k) }, BUILD_TEXT.tabs[k])),
    ),
    h('div.piece-list', {}, ...items),
    h(
      'p.build-keys',
      {},
      inHand ? `${holdingLine(inHand)}${LINE_PIECES.has(inHand) ? ' Arrastra para poner una fila.' : ''}` : felling ? BUILD_TEXT.felling : BUILD_TEXT.idle,
    ),
  ] as (HTMLElement | null)[]).filter((x): x is HTMLElement => !!x);
}

/** «15 §», «15 § · 1 pieza». */
function costLabel(cost: { supplies: number; parts?: number }): string {
  return cost.parts ? `${cost.supplies} § · ${cost.parts} ${cost.parts === 1 ? 'pieza' : 'piezas'}` : `${cost.supplies} §`;
}

export interface CardView {
  c: CampaignState;
  piece: BasePiece;
  /** «Desmontar» was pressed once: the second press does it. */
  dismantling: boolean;
  /** The partner, while connected (their name): every action goes to a vote (#55). */
  partner: string | null;
}

export interface CardHandlers {
  send(cmd: CampaignCommand): void;
  dismantle(ask: boolean): void;
  close(): void;
}

/** The card of a piece picked on the clearing: how it is, and mending, raising, setting again or taking it down. */
export function pieceCard(v: CardView, on: CardHandlers): HTMLElement[] {
  const { c, piece: p } = v;
  const def = PIECES[p.piece];
  const text = PIECE_TEXT[p.piece];
  const level = p.level ?? 1;
  const building = def.kind === 'building' && p.piece !== 'caravan';
  const out: (HTMLElement | null)[] = [
    h('div.alert-kicker', {}, building ? buildingKicker(level) : BUILD_TEXT.kinds[def.kind]),
    h('h2.x-title', {}, text.name, h('button.x-btn.small.piece-close', { title: 'Soltarla (Esc)', onclick: () => on.close() }, '✕')),
    h('p.x-muted', {}, text.line),
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
  const beside = besidePieces(c.bastion, p);
  if (def.kind === 'building' && beside.length) out.push(h('p.piece-beside', {}, besideLine([...new Set(beside.map((b) => PIECE_TEXT[b.piece].name))])));

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
    else {
      const why =
        bastionLevel(c) < next.level ? needsLevelLine(next.level) : !buildingWorks(p) ? PIECE_STATE.brokenLine : c.credits < next.cost ? `${buildShortLine(next.cost - c.credits)}.` : pending;
      actions.push(button(actionPrice('upgrade', { supplies: next.cost }), why, () => on.send({ type: 'raise', piece: p.id }), upgradeLine(p.piece, level + 1)));
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
        : button(actionPrice('dismantle', { supplies: back }), pending, () => on.dismantle(true), BUILD_TEXT.dismantleTip),
    );
  }
  out.push(h('div.piece-actions', {}, ...actions));
  if (v.partner && !pending && actions.length) out.push(h('p.build-vote', {}, voteNote(v.partner)));
  return out.filter((x): x is HTMLElement => !!x);
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
