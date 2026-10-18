import './hexBoard.css';
import {
  BOARD,
  BOARD_CELLS,
  boardNeighbors,
  CELL_KINDS,
  cellState,
  CORE,
  pointsLeft,
  SKILLS,
  unlockBlocker,
  type BoardCell,
  type CellId,
  type Skills,
} from '@bastion/engine';
import { uiSound } from '../audio/ui';
import { append, clear, h } from '../ui/dom';
import { boardIconStyle, boardIconSvg } from './boardIcons';

// The hex board of Fase 10 (docs/FASE10.md §6, Arturo's prototype of 16/10):
// 169 hexagonal cells, the core in the middle and 168 skills in seven rings,
// six sectors of 28 in the lines' colours, inside one ring. In Arturo's words
// (16/10) a cell is «seleccionada» (bought, `unlocked` in the engine: its
// family's colour, its icon sharp), «desbloqueada» (`open`, beside a bought
// one: dull, its icon grey) or «bloqueada» (`locked`: dimmer still, no icon).
//
// - Consulting: the pointer (or the keyboard's focus, or a tap) on a selected
//   or unlocked cell shows its card beside the board; it stays when the
//   pointer leaves, and never buys or changes how a cell looks. A locked cell
//   shows nothing.
// - Selecting: holding a cell down for a second, an indicator running round
//   its outline; letting go early, straying well off the cell, Escape or
//   losing the focus cancel it (the pointer may creep a little, and the wave
//   holds still meanwhile). A short click only consults. Done, it
//   bounces and its outline pulses once; the neighbours open.
// - The wave: the cells near the pointer rise with the distance, face and icon
//   together, and settle slowly when it leaves.
// - Moving: dragging pans, the wheel zooms towards the pointer (a trackpad's
//   pinch comes as the wheel), within limits; it opens with the whole tree framed.
// - Keyboard: the arrows go from cell to cell (and consult); holding Enter or
//   Space unlocks. Reduced motion: no wave, no bounce.
//
// The rules are the engine's (skills.ts cellState, unlockBlocker, pointsLeft;
// board.ts: beside one it has, and a point, Arturo 16/10); the order goes out through `onUnlock` (the campaign's
// `unlock`). The cards' line of the world is Lore's (`describe`).

export interface HexBoardData {
  skills: Skills;
  board: readonly CellId[];
  /** The viewer's own soldier, alive: it may unlock. */
  editable: boolean;
  /** Whose board, for the card when it isn't the viewer's. */
  owner: string;
  /** Lore's line of the world for a cell, when there is one. */
  describe?: (id: CellId) => string | undefined;
}

// ------------------------------------------------------------------ geometry

/** A cell's circumradius in board units, and the gap left between neighbours. */
const SIZE = 30;
const GAP = 3.2;
const SQ3 = Math.sqrt(3);
/** Pointy-top hexes; the first sector (Disparo) on top, the rest clockwise. */
const centre = (c: Pick<BoardCell, 'q' | 'r'>) => ({ x: SQ3 * SIZE * (c.q + c.r / 2), y: -1.5 * SIZE * c.r });
const hexPoints = (radius: number) =>
  Array.from({ length: 6 }, (_, i) => {
    const a = (Math.PI / 180) * (60 * i - 90);
    return `${(radius * Math.cos(a)).toFixed(2)},${(radius * Math.sin(a)).toFixed(2)}`;
  }).join(' ');
/** The frame's ring, past the outermost cells (the farthest centres lie 364 out). */
const OUTER = 424;
/** The view's half-size in board units, to frame it whole. */
const HALF = 440;

const HOLD_MS = 1000;
/** Pointer travel (px) that turns a press into a drag. */
const DRAG_PX = 10;
/**
 * How far from its cell's centre (board units) the pointer may stray while it
 * holds it: the cell and a margin round it. A finger pressing a trackpad
 * creeps a few px, and 6 px turned the hold into a drag (Arturo, 16/10 #36).
 */
const HOLD_REACH = SIZE * 1.15;
/** The wave: how high a cell rises (board units), how far it reaches (in cells' radii). */
const LIFT = 7;
const REACH = 3.4;

const reduced = () => typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;
const NS = 'http://www.w3.org/2000/svg';

function svgEl<K extends keyof SVGElementTagNameMap>(tag: K, attrs: Record<string, string | number> = {}, ...children: (SVGElement | null)[]): SVGElementTagNameMap[K] {
  const el = document.createElementNS(NS, tag);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, String(v));
  for (const c of children) if (c) el.append(c);
  return el;
}

interface CellView {
  cell: BoardCell;
  x: number;
  y: number;
  g: SVGGElement;
  lift: SVGGElement;
  pop: SVGGElement;
  /** The wave's height now and where it is going. */
  at: number;
  to: number;
}

type Hold = { id: CellId; timer: number; pointer: number | null; x: number; y: number } | null;

export class HexBoard {
  readonly el: HTMLElement;
  private readonly view: HTMLElement;
  private readonly svg: SVGSVGElement;
  private readonly cam: SVGGElement;
  private readonly card: HTMLElement;
  private readonly cells = new Map<CellId, CellView>();
  private data: HexBoardData | null = null;
  /** The cell on the card (the last one consulted) and the one with the keyboard's focus. */
  private shown: CellId = CORE;
  private focused: CellId = CORE;
  private hold: Hold = null;
  /** Red's rising tone while a cell is held (audio/ui.ts cellHold): stopped if it is let go. */
  private holdSound: (() => void) | null = null;
  /** The camera: pan (px from the middle) and zoom (px per board unit). */
  private px = 0;
  private py = 0;
  private z = 1;
  private framed = false;
  /** The view's size the board was last framed for; a new size frames it again until the player moves it. */
  private framedFor = '';
  private moved = false;
  private drag: { id: number; x: number; y: number; px: number; py: number; moved: boolean } | null = null;
  /** The pointer over the board, in board units (null outside). */
  private pointer: { x: number; y: number } | null = null;
  private frame = 0;
  /** The cell under the mouse, for its tick once on entering. */
  private hovered: CellId | null = null;

  constructor(private readonly onUnlock: (id: CellId) => void) {
    this.svg = svgEl('svg', { class: 'hb-svg', role: 'group', 'aria-label': 'Tablero de habilidades' });
    this.cam = svgEl('g', { class: 'hb-cam' });
    this.cam.append(this.frameEl(), this.cellsEl());
    this.svg.append(this.cam);
    this.view = h('div.hb-view', { tabindex: '-1' }, this.svg);
    this.card = h('aside.hb-card', { 'aria-live': 'polite' });
    this.el = h('div.hb', {}, this.view, this.card);
    this.wire();
  }

  /** New data (the soldier as it is now): every cell's state, the card, and the bounce of what was just bought. */
  update(data: HexBoardData): void {
    const before = this.data?.board;
    this.data = data;
    for (const v of this.cells.values()) this.paint(v);
    if (before) for (const id of data.board) if (!before.includes(id)) this.celebrate(id);
    this.showCard(this.shown);
  }

  // ---------------------------------------------------------------- drawing

  /** The round frame (Arturo, 16/10: one ring, nothing outside it): the ring, the six spokes between the sectors, and marks all round inside it. */
  private frameEl(): SVGGElement {
    const g = svgEl('g', { class: 'hb-frame', 'aria-hidden': 'true' });
    g.append(svgEl('circle', { class: 'hb-ring', cx: 0, cy: 0, r: OUTER }));
    // The spokes where each sector starts (the rings' corners, 30° either side of the vertical), out to the ring.
    for (let i = 0; i < 6; i++) {
      const a = (Math.PI / 180) * (60 * i - 120);
      g.append(svgEl('line', { class: 'hb-spoke', x1: (392 * Math.cos(a)).toFixed(1), y1: (392 * Math.sin(a)).toFixed(1), x2: (OUTER * Math.cos(a)).toFixed(1), y2: (OUTER * Math.sin(a)).toFixed(1) }));
    }
    // Marks every 5°, longer every 15°.
    for (let deg = 0; deg < 360; deg += 5) {
      const a = (Math.PI / 180) * (deg - 90);
      const long = deg % 15 === 0;
      const r0 = OUTER - (long ? 12 : 6);
      g.append(svgEl('line', { class: `hb-tick${long ? ' long' : ''}`, x1: (r0 * Math.cos(a)).toFixed(1), y1: (r0 * Math.sin(a)).toFixed(1), x2: (OUTER * Math.cos(a)).toFixed(1), y2: (OUTER * Math.sin(a)).toFixed(1) }));
    }
    return g;
  }

  private cellsEl(): SVGGElement {
    const g = svgEl('g', { class: 'hb-cells' });
    const face = hexPoints(SIZE - GAP / 2);
    // Outer cells first: the inner ones, drawn later, lie over a rising neighbour's shadow.
    for (const cell of [...BOARD].sort((a, b) => b.ring - a.ring)) {
      const { x, y } = centre(cell);
      const icon = svgEl('g', { class: 'hx-icon' });
      icon.innerHTML = boardIconSvg(cell.id, cell.kind === 'core' ? 34 : 27);
      const pop = svgEl(
        'g',
        { class: 'hx-pop' },
        svgEl('polygon', { class: 'hx-face', points: face }),
        svgEl('polygon', { class: 'hx-edge', points: face }),
        icon,
        svgEl('polygon', { class: 'hx-hold', points: face, pathLength: 100 }),
        svgEl('polygon', { class: 'hx-pulse', points: face }),
      );
      const lift = svgEl('g', { class: 'hx-lift' }, pop);
      const cellG = svgEl(
        'g',
        { class: `hx sk-${cell.line ?? 'core'} kind-${cell.kind}`, transform: `translate(${x.toFixed(1)} ${y.toFixed(1)})`, 'data-id': cell.id, tabindex: cell.id === CORE ? 0 : -1, role: 'button' },
        svgEl('polygon', { class: 'hx-shadow', points: face }),
        lift,
      );
      g.append(cellG);
      this.cells.set(cell.id, { cell, x, y, g: cellG, lift, pop, at: 0, to: 0 });
    }
    return g;
  }

  /** A cell's look from the soldier's board: selected, unlocked or locked, and its label for a screen reader (a locked one, no name: it shows none). */
  private paint(v: CellView): void {
    const d = this.data!;
    const state = cellState(d.board, v.cell.id);
    v.g.classList.remove('unlocked', 'open', 'locked');
    v.g.classList.add(state);
    v.g.setAttribute('aria-label', state === 'locked' ? 'Casilla bloqueada' : `${v.cell.name}: ${state === 'unlocked' ? 'seleccionada' : 'desbloqueada'}`);
  }

  /** Whether `id` has a card to show: a selected or unlocked cell; a locked one, nothing (Arturo, 16/10). */
  private readable(id: CellId): boolean {
    return !!this.data && cellState(this.data.board, id) !== 'locked';
  }

  // ---------------------------------------------------------------- the card

  /** The card of `id`: icon, name, kind and family, Lore's line, the effect, what it asks and where it stands; a locked cell keeps the last card. */
  private showCard(id: CellId): void {
    const d = this.data;
    const cell = BOARD_CELLS[id];
    if (!d || !cell) return;
    if (!this.readable(id)) id = this.readable(this.shown) ? this.shown : CORE;
    const shownCell = BOARD_CELLS[id]!;
    this.shown = id;
    for (const v of this.cells.values()) v.g.classList.toggle('shown', v.cell.id === id);
    const state = cellState(d.board, id);
    const line = shownCell.line;
    clear(this.card);
    this.card.className = `hb-card sk-${line ?? 'core'} ${state}`;
    const icon = h('span.hb-card-icon', {}, h('i', { style: boardIconStyle(id, id === CORE ? 'core' : state === 'unlocked' ? 'dark' : 'grey') }));
    const lore = d.describe?.(id);
    const kind = shownCell.kind === 'core' ? CELL_KINDS.core : `${CELL_KINDS[shownCell.kind]} · ${SKILLS[line!].name}`;
    append(
      this.card,
      h('div.hb-card-head', {}, icon, h('div', {}, h('b.hb-card-name', {}, shownCell.name), h('small.hb-card-kind', {}, kind))),
      lore ? h('p.hb-card-lore', {}, lore) : null,
      h('p.hb-card-effect', {}, shownCell.effect),
      // No rules on the card (Arturo, 16/10 #37): at most, on an unlocked cell of one's own, that there are no points.
      state === 'open' && d.editable && pointsLeft(d.skills, d.board) < 1 ? h('p.hb-card-note', {}, 'Sin puntos por gastar') : null,
    );
  }

  // ---------------------------------------------------------------- holding

  /** Whether `id` can be bought now by the viewer. */
  private buyable(id: CellId): boolean {
    const d = this.data;
    return !!d && d.editable && unlockBlocker(d.skills, d.board, id) === null;
  }

  /** A press on `id` (pointer or key): it consults at once, and if it can be bought, the hold begins. */
  private startHold(id: CellId, pointer: number | null, x = 0, y = 0): void {
    this.cancelHold();
    this.showCard(id);
    if (!this.buyable(id)) return;
    const v = this.cells.get(id)!;
    v.g.classList.add('holding');
    this.holdSound = uiSound('cellHold');
    const timer = window.setTimeout(() => this.completeHold(), HOLD_MS);
    this.hold = { id, timer, pointer, x, y };
  }

  /** Let go, left the cell, a drag, Escape, the focus gone: nothing is bought. */
  private cancelHold(): void {
    if (!this.hold) return;
    this.holdSound?.();
    this.holdSound = null;
    window.clearTimeout(this.hold.timer);
    this.cells.get(this.hold.id)?.g.classList.remove('holding');
    this.hold = null;
  }

  private completeHold(): void {
    const hold = this.hold;
    if (!hold) return;
    this.hold = null;
    // Its tone lasts the hold exactly and ends on its own as the cell clicks in.
    this.holdSound = null;
    this.cells.get(hold.id)?.g.classList.remove('holding');
    // Checked again: the soldier may have changed while it was held.
    if (this.buyable(hold.id)) this.onUnlock(hold.id);
  }

  /** Bought: a small bounce and a pulse round its outline; its look and its neighbours' come with the data. */
  private celebrate(id: CellId): void {
    const v = this.cells.get(id);
    if (!v) return;
    v.g.classList.remove('bought');
    void v.g.getBoundingClientRect();
    v.g.classList.add('bought');
    uiSound('cellBuy');
    window.setTimeout(() => v.g.classList.remove('bought'), 900);
  }

  // ---------------------------------------------------------------- camera

  private size(): { w: number; h: number } {
    return { w: this.view.clientWidth, h: this.view.clientHeight };
  }

  private fitZoom(): number {
    const { w, h: hh } = this.size();
    return Math.min(w, hh) / (2 * HALF);
  }

  /** The camera within its limits: from the whole tree to four times closer, the tree never out of the view. */
  private clamp(): void {
    const fit = this.fitZoom();
    this.z = Math.min(fit * 4, Math.max(fit * 0.9, this.z));
    const { w, h: hh } = this.size();
    const mx = Math.max(0, OUTER * this.z - w * 0.3);
    const my = Math.max(0, OUTER * this.z - hh * 0.3);
    this.px = Math.min(mx, Math.max(-mx, this.px));
    this.py = Math.min(my, Math.max(-my, this.py));
  }

  private apply(): void {
    const { w, h: hh } = this.size();
    if (!w || !hh) return;
    const size = `${w}x${hh}`;
    if (!this.framed || (size !== this.framedFor && !this.moved)) {
      this.z = this.fitZoom();
      this.px = this.py = 0;
      this.framed = true;
      this.framedFor = size;
    }
    this.clamp();
    this.svg.setAttribute('viewBox', `0 0 ${w} ${hh}`);
    this.cam.setAttribute('transform', `translate(${(w / 2 + this.px).toFixed(1)} ${(hh / 2 + this.py).toFixed(1)}) scale(${this.z.toFixed(4)})`);
  }

  /** Zooms by `k` keeping the board point under (sx, sy) (px in the view) where it is. */
  private zoomAt(k: number, sx: number, sy: number): void {
    this.moved = true;
    const { w, h: hh } = this.size();
    const bx = (sx - w / 2 - this.px) / this.z;
    const by = (sy - hh / 2 - this.py) / this.z;
    const fit = this.fitZoom();
    const z = Math.min(fit * 4, Math.max(fit * 0.9, this.z * k));
    this.px = sx - w / 2 - bx * z;
    this.py = sy - hh / 2 - by * z;
    this.z = z;
    this.apply();
  }

  /** The view's px to board units. */
  private toBoard(sx: number, sy: number): { x: number; y: number } {
    const { w, h: hh } = this.size();
    return { x: (sx - w / 2 - this.px) / this.z, y: (sy - hh / 2 - this.py) / this.z };
  }

  private local(e: { clientX: number; clientY: number }): { x: number; y: number } {
    const r = this.view.getBoundingClientRect();
    // The sheet may be zoomed (--ui-scale): screen px back to the view's own.
    const k = r.width / (this.view.clientWidth || 1) || 1;
    return { x: (e.clientX - r.left) / k, y: (e.clientY - r.top) / k };
  }

  // ---------------------------------------------------------------- the wave

  /** Every frame while something moves: each cell eases towards its height under the pointer. */
  private tick = (): void => {
    this.frame = 0;
    const p = this.pointer;
    let moving = false;
    const reach = SIZE * REACH;
    for (const v of this.cells.values()) {
      const dist = p ? Math.hypot(p.x - v.x, p.y - v.y) : Infinity;
      v.to = p && dist < reach ? LIFT * (1 - dist / reach) ** 2 : 0;
      // Quick to rise, slow to settle when the pointer has left the tree.
      const k = v.to > v.at ? 0.35 : p ? 0.2 : 0.06;
      const next = v.at + (v.to - v.at) * k;
      if (Math.abs(next - v.at) > 0.02 || Math.abs(v.to - next) > 0.02) moving = true;
      if (Math.abs(next - v.at) > 0.001) {
        v.at = Math.abs(next) < 0.01 && !v.to ? 0 : next;
        v.lift.setAttribute('transform', v.at ? `translate(0 ${(-v.at).toFixed(2)})` : '');
        v.g.classList.toggle('raised', v.at > 0.5);
      }
    }
    if (moving) this.frame = requestAnimationFrame(this.tick);
  };

  private wave(): void {
    if (reduced()) return;
    if (!this.frame) this.frame = requestAnimationFrame(this.tick);
  }

  // ---------------------------------------------------------------- input

  private cellAt(target: EventTarget | null): CellId | null {
    const g = (target as Element | null)?.closest?.('.hx');
    return (g?.getAttribute('data-id') as CellId | null) ?? null;
  }

  private wire(): void {
    const v = this.view;
    v.addEventListener('pointerdown', (e) => {
      if (e.pointerType === 'mouse' && e.button !== 0) return;
      const at = this.local(e);
      try {
        v.setPointerCapture(e.pointerId);
      } catch {
        /* a pointer the browser no longer tracks: the press still counts */
      }
      this.drag = { id: e.pointerId, x: at.x, y: at.y, px: this.px, py: this.py, moved: false };
      const id = this.cellAt(e.target);
      if (id) {
        this.focus(id, false);
        this.startHold(id, e.pointerId, at.x, at.y);
      }
    });
    v.addEventListener('pointermove', (e) => {
      const at = this.local(e);
      const holding = this.hold && this.hold.pointer === e.pointerId ? this.hold : null;
      // While a cell is held the wave stays as it is: nothing moves under the pointer.
      if (!holding) this.pointer = this.toBoard(at.x, at.y);
      // A hold lets go once the pointer is well off its cell, measured on the board (not by what the
      // wave has moved under it); until then the press never becomes a drag.
      if (holding) {
        const p = this.toBoard(at.x, at.y);
        const c = this.cells.get(holding.id)!;
        if (Math.hypot(p.x - c.x, p.y - c.y) > HOLD_REACH) this.cancelHold();
      }
      const drag = this.drag;
      if (drag && drag.id === e.pointerId) {
        if (!drag.moved && !this.hold && Math.hypot(at.x - drag.x, at.y - drag.y) > DRAG_PX) {
          drag.moved = true;
          // Dragging is moving the board, never buying.
          this.cancelHold();
          v.classList.add('dragging');
        }
        if (drag.moved) {
          this.moved = true;
          this.px = drag.px + (at.x - drag.x);
          this.py = drag.py + (at.y - drag.y);
          this.apply();
        }
      }
      // Hovering consults (not while holding).
      if (e.pointerType === 'mouse' && !this.drag?.moved && !this.hold) {
        const id = this.cellAt(e.target);
        if (id && id !== this.hovered && this.readable(id)) {
          uiSound('cellHover');
          this.showCard(id);
        }
        this.hovered = id;
      }
      this.wave();
    });
    const end = (e: PointerEvent) => {
      if (this.hold?.pointer === e.pointerId) this.cancelHold();
      if (this.drag?.id === e.pointerId) {
        this.drag = null;
        v.classList.remove('dragging');
      }
      if (e.pointerType !== 'mouse') {
        this.pointer = null;
        this.wave();
      }
    };
    v.addEventListener('pointerup', end);
    v.addEventListener('pointercancel', end);
    v.addEventListener('pointerleave', (e) => {
      if (e.pointerType === 'mouse') {
        this.pointer = null;
        this.wave();
      }
    });
    v.addEventListener(
      'wheel',
      (e) => {
        e.preventDefault();
        const at = this.local(e);
        this.zoomAt(Math.exp(-e.deltaY * 0.0015), at.x, at.y);
        this.pointer = this.toBoard(at.x, at.y);
        this.wave();
      },
      { passive: false },
    );
    // No menu or native drag to cut a press short.
    v.addEventListener('contextmenu', (e) => e.preventDefault());
    v.addEventListener('dragstart', (e) => e.preventDefault());
    v.addEventListener('keydown', (e) => this.key(e));
    // A cell given the focus any way (a click, the tab key): the arrows go on from it, and it is consulted.
    v.addEventListener('focusin', (e) => {
      const id = this.cellAt(e.target);
      if (!id || id === this.focused) return;
      this.cells.get(this.focused)?.g.setAttribute('tabindex', '-1');
      this.focused = id;
      this.cells.get(id)!.g.setAttribute('tabindex', '0');
      this.showCard(id);
    });
    v.addEventListener('keyup', (e) => {
      if ((e.key === 'Enter' || e.key === ' ') && this.hold?.pointer === null) this.cancelHold();
    });
    // The focus gone (another window, another field): a hold never finishes unseen.
    v.addEventListener('focusout', (e) => {
      if (!v.contains(e.relatedTarget as Node | null)) this.cancelHold();
    });
    window.addEventListener('blur', () => this.cancelHold());
    if (typeof ResizeObserver !== 'undefined') {
      const watch = new ResizeObserver(() => {
        if (!this.el.isConnected) return;
        this.apply();
      });
      watch.observe(v);
    }
    requestAnimationFrame(() => this.apply());
  }

  /** The keyboard: arrows to the neighbour that way (consulting it), Enter or Space held to unlock, Escape to let go. */
  private key(e: KeyboardEvent): void {
    if (e.key === 'Escape') {
      if (this.hold) {
        e.preventDefault();
        e.stopPropagation();
        this.cancelHold();
      }
      return;
    }
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      if (!e.repeat && !this.hold) this.startHold(this.focused, null);
      return;
    }
    const dir: Record<string, [number, number]> = { ArrowRight: [1, 0], ArrowLeft: [-1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] };
    const d = dir[e.key];
    if (!d) return;
    e.preventDefault();
    this.cancelHold();
    const from = this.cells.get(this.focused)!;
    // The neighbour whose direction is closest to the arrow's.
    let best: CellId | null = null;
    let score = 0.3;
    for (const n of boardNeighbors(this.focused)) {
      const v = this.cells.get(n)!;
      const dx = v.x - from.x;
      const dy = v.y - from.y;
      const s = (dx * d[0] + dy * d[1]) / Math.hypot(dx, dy);
      if (s > score) {
        score = s;
        best = n;
      }
    }
    if (best) this.focus(best, true);
  }

  /** Moves the keyboard's focus to `id` (the roving tab stop), consulting it; `show`: the camera keeps it in view. */
  private focus(id: CellId, show: boolean): void {
    const prev = this.cells.get(this.focused);
    prev?.g.setAttribute('tabindex', '-1');
    this.focused = id;
    const v = this.cells.get(id)!;
    v.g.setAttribute('tabindex', '0');
    v.g.focus({ preventScroll: true });
    this.showCard(id);
    if (!show) return;
    // Into view if the keyboard has walked it off the edge.
    const { w, h: hh } = this.size();
    const sx = w / 2 + this.px + v.x * this.z;
    const sy = hh / 2 + this.py + v.y * this.z;
    const m = SIZE * this.z * 2;
    if (sx < m) this.px += m - sx;
    if (sx > w - m) this.px -= sx - (w - m);
    if (sy < m) this.py += m - sy;
    if (sy > hh - m) this.py -= sy - (hh - m);
    this.apply();
  }
}
