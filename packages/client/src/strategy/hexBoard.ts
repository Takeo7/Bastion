import './hexBoard.css';
import {
  BOARD,
  BOARD_CELLS,
  boardNeighbors,
  CELL_KINDS,
  cellState,
  CORE,
  pointsLeft,
  SECTOR_LINES,
  SKILLS,
  unlockBlocker,
  withinReach,
  type BoardCell,
  type CellId,
  type Skills,
} from '@bastion/engine';
import { append, clear, h } from '../ui/dom';
import { boardIconSvg } from './boardIcons';

// The hex board of Fase 10 (docs/FASE10.md §6, Arturo's prototype of 16/10):
// 169 hexagonal cells, the core in the middle and 168 skills in seven rings,
// six sectors of 28 in the lines' colours, inside a round frame of lines. A
// cell is unlocked (its family's colour, its icon sharp), open (beside one
// unlocked: dull, its icon grey) or locked (dimmer still, no icon).
//
// - Consulting: the pointer (or the keyboard's focus, or a tap) shows the
//   cell's card beside the board; it stays when the pointer leaves, and never
//   buys or changes how a cell looks.
// - Unlocking: holding a cell down for a second, an indicator running round
//   its outline; letting go early, leaving the cell, starting to drag, Escape
//   or losing the focus cancel it. A short click only consults. Done, it
//   bounces and its outline pulses once; the neighbours open.
// - The wave: the cells near the pointer rise with the distance, face and icon
//   together, and settle slowly when it leaves.
// - Moving: dragging pans, the wheel zooms towards the pointer (two fingers
//   pinch), within limits; it opens with the whole tree framed.
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
/** The frame's rings, past the outermost cells (the farthest centres lie 364 out), and its outer edge. */
const FRAME = [404, 416];
const OUTER = 446;
/** The view's half-size in board units, to frame it whole. */
const HALF = 470;

const HOLD_MS = 1000;
/** Pointer travel (px) that turns a press into a drag. */
const DRAG_PX = 6;
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
  /** The camera: pan (px from the middle) and zoom (px per board unit). */
  private px = 0;
  private py = 0;
  private z = 1;
  private framed = false;
  private drag: { id: number; x: number; y: number; px: number; py: number; moved: boolean } | null = null;
  private pinch: { d: number; z: number } | null = null;
  private readonly touches = new Map<number, { x: number; y: number }>();
  /** The pointer over the board, in board units (null outside). */
  private pointer: { x: number; y: number } | null = null;
  private frame = 0;

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

  /** The round frame: concentric rings, the six spokes between the sectors, and marks all round. */
  private frameEl(): SVGGElement {
    const g = svgEl('g', { class: 'hb-frame', 'aria-hidden': 'true' });
    for (const r of FRAME) g.append(svgEl('circle', { class: 'hb-ring', cx: 0, cy: 0, r }));
    g.append(svgEl('circle', { class: 'hb-ring outer', cx: 0, cy: 0, r: OUTER }));
    // The spokes where each sector starts (the rings' corners, 30° either side of the vertical), out to the edge.
    for (let i = 0; i < 6; i++) {
      const a = (Math.PI / 180) * (60 * i - 120);
      g.append(svgEl('line', { class: 'hb-spoke', x1: (392 * Math.cos(a)).toFixed(1), y1: (392 * Math.sin(a)).toFixed(1), x2: ((OUTER + 22) * Math.cos(a)).toFixed(1), y2: ((OUTER + 22) * Math.sin(a)).toFixed(1) }));
    }
    // Marks every 5°, longer every 15° and at each sector's middle a notch in its colour.
    for (let deg = 0; deg < 360; deg += 5) {
      const a = (Math.PI / 180) * (deg - 90);
      const long = deg % 15 === 0;
      const r0 = OUTER - (long ? 12 : 6);
      g.append(svgEl('line', { class: `hb-tick${long ? ' long' : ''}`, x1: (r0 * Math.cos(a)).toFixed(1), y1: (r0 * Math.sin(a)).toFixed(1), x2: (OUTER * Math.cos(a)).toFixed(1), y2: (OUTER * Math.sin(a)).toFixed(1) }));
    }
    SECTOR_LINES.forEach((line, i) => {
      // At the sector's own middle: the mean of its cells' directions.
      const own = BOARD.filter((c) => c.sector === i).map(centre);
      const a = Math.atan2(own.reduce((sum, p) => sum + p.y, 0), own.reduce((sum, p) => sum + p.x, 0));
      const p = { x: (OUTER + 14) * Math.cos(a), y: (OUTER + 14) * Math.sin(a) };
      g.append(svgEl('path', { class: `hb-notch sk-${line}`, d: `M${(p.x - 9 * Math.cos(a + Math.PI / 2)).toFixed(1)} ${(p.y - 9 * Math.sin(a + Math.PI / 2)).toFixed(1)}L${(p.x + 9 * Math.cos(a + Math.PI / 2)).toFixed(1)} ${(p.y + 9 * Math.sin(a + Math.PI / 2)).toFixed(1)}L${((OUTER + 2) * Math.cos(a)).toFixed(1)} ${((OUTER + 2) * Math.sin(a)).toFixed(1)}Z` }));
    });
    return g;
  }

  private cellsEl(): SVGGElement {
    const g = svgEl('g', { class: 'hb-cells' });
    const face = hexPoints(SIZE - GAP / 2);
    // Outer cells first: the inner ones, drawn later, lie over a rising neighbour's shadow.
    for (const cell of [...BOARD].sort((a, b) => b.ring - a.ring)) {
      const { x, y } = centre(cell);
      const icon = svgEl('g', { class: 'hx-icon', transform: cell.kind === 'core' ? 'translate(-15 -15) scale(1.25)' : 'translate(-12 -12)' });
      icon.innerHTML = boardIconSvg(cell.id);
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

  /** A cell's look from the soldier's board: unlocked, open or locked, and its label for a screen reader. */
  private paint(v: CellView): void {
    const d = this.data!;
    const state = cellState(d.board, v.cell.id);
    v.g.classList.remove('unlocked', 'open', 'locked');
    v.g.classList.add(state);
    v.g.setAttribute('aria-label', `${v.cell.name}: ${state === 'unlocked' ? 'desbloqueada' : state === 'open' ? 'disponible' : 'bloqueada'}`);
  }

  // ---------------------------------------------------------------- the card

  /** The card of `id`: icon, name, kind and family, Lore's line, the effect, what it asks and where it stands. */
  private showCard(id: CellId): void {
    const d = this.data;
    const cell = BOARD_CELLS[id];
    if (!d || !cell) return;
    this.shown = id;
    for (const v of this.cells.values()) v.g.classList.toggle('shown', v.cell.id === id);
    const state = cellState(d.board, id);
    const line = cell.line;
    clear(this.card);
    this.card.className = `hb-card sk-${line ?? 'core'} ${state}`;
    const icon = h('span.hb-card-icon');
    icon.innerHTML = `<svg viewBox="0 0 24 24" aria-hidden="true">${boardIconSvg(id)}</svg>`;
    const lore = d.describe?.(id);
    const kind = cell.kind === 'core' ? CELL_KINDS.core : `${CELL_KINDS[cell.kind]} · ${SKILLS[line!].name}`;
    append(
      this.card,
      h('div.hb-card-head', {}, icon, h('div', {}, h('b.hb-card-name', {}, cell.name), h('small.hb-card-kind', {}, kind))),
      lore ? h('p.hb-card-lore', {}, lore) : null,
      h('p.hb-card-effect', {}, cell.effect),
      id === CORE ? null : this.needs(cell),
      h(`p.hb-card-status.${this.statusTone(id)}`, {}, this.status(id)),
    );
  }

  /** What a cell asks for (beside one it has, and a point), each with whether the soldier has it. */
  private needs(cell: BoardCell): HTMLElement {
    const d = this.data!;
    const points = pointsLeft(d.skills, d.board);
    const owned = d.board.includes(cell.id);
    const row = (ok: boolean, text: string) => h(`li.${ok ? 'ok' : 'miss'}`, {}, text);
    return h(
      'ul.hb-card-needs',
      {},
      row(owned || withinReach(d.board, cell.id), 'Al lado de una que ya tenga'),
      owned ? null : row(points > 0, `Un punto (le ${points === 1 ? 'queda 1' : `quedan ${points}`})`),
    );
  }

  private status(id: CellId): string {
    const d = this.data!;
    if (id === CORE) return 'Todo se abre desde aquí.';
    if (d.board.includes(id)) return 'Desbloqueada.';
    const why = unlockBlocker(d.skills, d.board, id);
    if (why) return why;
    return d.editable ? 'Mantén pulsada la casilla un segundo para desbloquearla.' : `Disponible. Solo ${d.owner} puede desbloquearla.`;
  }

  private statusTone(id: CellId): 'done' | 'go' | 'no' {
    const d = this.data!;
    if (id === CORE || d.board.includes(id)) return 'done';
    return !unlockBlocker(d.skills, d.board, id) && d.editable ? 'go' : 'no';
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
    const timer = window.setTimeout(() => this.completeHold(), HOLD_MS);
    this.hold = { id, timer, pointer, x, y };
  }

  /** Let go, left the cell, a drag, Escape, the focus gone: nothing is bought. */
  private cancelHold(): void {
    if (!this.hold) return;
    window.clearTimeout(this.hold.timer);
    this.cells.get(this.hold.id)?.g.classList.remove('holding');
    this.hold = null;
  }

  private completeHold(): void {
    const hold = this.hold;
    if (!hold) return;
    this.hold = null;
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
    if (!this.framed) {
      this.z = this.fitZoom();
      this.px = this.py = 0;
      this.framed = true;
    }
    this.clamp();
    this.svg.setAttribute('viewBox', `0 0 ${w} ${hh}`);
    this.cam.setAttribute('transform', `translate(${(w / 2 + this.px).toFixed(1)} ${(hh / 2 + this.py).toFixed(1)}) scale(${this.z.toFixed(4)})`);
  }

  /** Zooms by `k` keeping the board point under (sx, sy) (px in the view) where it is. */
  private zoomAt(k: number, sx: number, sy: number): void {
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
      this.touches.set(e.pointerId, at);
      try {
        v.setPointerCapture(e.pointerId);
      } catch {
        /* a pointer the browser no longer tracks: the press still counts */
      }
      if (this.touches.size === 2) {
        // Two fingers: a pinch, never a hold.
        this.cancelHold();
        const [a, b] = [...this.touches.values()];
        this.pinch = { d: Math.hypot(a!.x - b!.x, a!.y - b!.y), z: this.z };
        this.drag = null;
        return;
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
      if (e.pointerType === 'mouse' || this.touches.has(e.pointerId)) this.pointer = this.toBoard(at.x, at.y);
      if (this.touches.has(e.pointerId)) this.touches.set(e.pointerId, at);
      if (this.pinch && this.touches.size === 2) {
        const [a, b] = [...this.touches.values()];
        const d = Math.hypot(a!.x - b!.x, a!.y - b!.y);
        this.zoomAt((this.pinch.z * (d / this.pinch.d)) / this.z, (a!.x + b!.x) / 2, (a!.y + b!.y) / 2);
        return;
      }
      const drag = this.drag;
      if (drag && drag.id === e.pointerId) {
        if (!drag.moved && Math.hypot(at.x - drag.x, at.y - drag.y) > DRAG_PX) {
          drag.moved = true;
          // Dragging is moving the board, never buying.
          this.cancelHold();
          v.classList.add('dragging');
        }
        if (drag.moved) {
          this.px = drag.px + (at.x - drag.x);
          this.py = drag.py + (at.y - drag.y);
          this.apply();
        }
      }
      // A hold whose pointer has left its cell is let go.
      if (this.hold && this.hold.pointer === e.pointerId) {
        const over = document.elementFromPoint(e.clientX, e.clientY);
        if (this.cellAt(over) !== this.hold.id) this.cancelHold();
      }
      // Hovering consults (a mouse only: a finger consults by tapping).
      if (e.pointerType === 'mouse' && !this.drag?.moved) {
        const id = this.cellAt(e.target);
        if (id && id !== this.shown) this.showCard(id);
      }
      this.wave();
    });
    const end = (e: PointerEvent) => {
      this.touches.delete(e.pointerId);
      if (this.touches.size < 2) this.pinch = null;
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
