// Procedural battlefields.
//
// A map is a 3×3 grid of 10×10 parcels separated by 3-tile streets (36×36).
// Every parcel is filled by a generator of the map's biome (an office, a block of
// flats with a reachable roof, a forest, a raised platform…). The squad starts in
// a corner parcel and the objective sits in the opposite one. A final pass makes
// sure every walkable area can be reached (adding ladders or doors if needed),
// and only reachable tiles are used to place the squad, the pods and objectives.
import { MAPS, parseMap, type MapDef } from './maps';
import { stepCost } from './path';
import { Rng } from './rng';
import { canStandOn, DIRS4, DIRS8, isWalkable } from './grid';
import type { Biome, GameState, MissionKind, TileKind, Vec2 } from './types';

export const PARCEL = 10;
export const STREET = 3;
const GRID = 3;
export const GEN_SIZE = GRID * PARCEL + (GRID - 1) * STREET;
/** Tiles reserved for the squad at the start. */
export const MAX_SQUAD = 8;

/** Everything a mission needs from a map: terrain plus where things go. */
export interface MissionLayout {
  id: string;
  name: string;
  biome: Biome;
  width: number;
  height: number;
  tiles: TileKind[];
  elev: number[];
  spawns: Vec2[];
  /** Spawn tiles and patrol route of each pod, in the order the pod plan lists them. */
  pods: { tiles: Vec2[]; patrol: Vec2[] }[];
  /** Recovery data; also the default spot for other objectives on hand-made maps. */
  item: Vec2 | null;
  terminal: Vec2 | null;
  /** Where the relay (sabotage) or the VIP (rescue) stands. */
  objectiveSpot: Vec2 | null;
  evacZone: Vec2[];
}

interface Rect {
  x: number;
  y: number;
  w: number;
  h: number;
}

// ------------------------------------------------------------------ canvas

/** Mutable terrain under construction. `keep` marks tiles that must stay free. */
class Canvas {
  readonly tiles: TileKind[];
  readonly elev: number[];
  readonly keep: boolean[];

  constructor(
    readonly width: number,
    readonly height: number,
  ) {
    this.tiles = new Array<TileKind>(width * height).fill('floor');
    this.elev = new Array<number>(width * height).fill(0);
    this.keep = new Array<boolean>(width * height).fill(false);
  }

  in(x: number, y: number): boolean {
    return x >= 0 && y >= 0 && x < this.width && y < this.height;
  }

  i(x: number, y: number): number {
    return y * this.width + x;
  }

  get(x: number, y: number): TileKind {
    return this.in(x, y) ? this.tiles[this.i(x, y)]! : 'wall';
  }

  elevAt(x: number, y: number): number {
    return this.in(x, y) ? this.elev[this.i(x, y)]! : 0;
  }

  force(x: number, y: number, kind: TileKind, elev?: number): void {
    if (!this.in(x, y)) return;
    this.tiles[this.i(x, y)] = kind;
    if (elev !== undefined) this.elev[this.i(x, y)] = elev;
  }

  reserve(x: number, y: number): void {
    if (this.in(x, y)) this.keep[this.i(x, y)] = true;
  }

  /** Free floor that may receive an object. */
  free(x: number, y: number, elev?: number): boolean {
    if (!this.in(x, y)) return false;
    const i = this.i(x, y);
    return this.tiles[i] === 'floor' && !this.keep[i] && (elev === undefined || this.elev[i] === elev);
  }

  /** A view usable by the engine's movement rules. */
  asState(): GameState {
    return { width: this.width, height: this.height, tiles: this.tiles, elev: this.elev } as unknown as GameState;
  }
}

/**
 * Places `count` objects of `kind` inside `r` (lines of `len` tiles), keeping a
 * free ring around each one so cover never walls off a passage.
 */
function scatter(c: Canvas, r: Rect, rng: Rng, count: number, kind: TileKind, len: [number, number] = [1, 1], elev = 0): void {
  let placed = 0;
  for (let attempt = 0; attempt < count * 14 && placed < count; attempt++) {
    const n = rng.range(len[0], len[1]);
    const horizontal = rng.int(2) === 0;
    const w = horizontal ? n : 1;
    const h = horizontal ? 1 : n;
    if (r.w < w || r.h < h) continue;
    const x0 = r.x + rng.int(r.w - w + 1);
    const y0 = r.y + rng.int(r.h - h + 1);
    let ok = true;
    for (let y = y0 - 1; y <= y0 + h && ok; y++) {
      for (let x = x0 - 1; x <= x0 + w && ok; x++) {
        const inside = x >= x0 && x < x0 + w && y >= y0 && y < y0 + h;
        if (inside) ok = c.free(x, y, elev);
        else if (c.in(x, y) && c.elevAt(x, y) === elev && c.get(x, y) !== 'floor' && c.get(x, y) !== 'ladder') ok = false;
      }
    }
    if (!ok) continue;
    for (let y = y0; y < y0 + h; y++) for (let x = x0; x < x0 + w; x++) c.force(x, y, kind);
    placed++;
  }
}

/** Random rectangle of the given size range inside `r`, leaving `margin` tiles all around. */
function innerRect(r: Rect, rng: Rng, w: [number, number], h: [number, number], margin = 1): Rect {
  const ww = Math.min(rng.range(w[0], w[1]), r.w - 2 * margin);
  const hh = Math.min(rng.range(h[0], h[1]), r.h - 2 * margin);
  return {
    x: r.x + margin + rng.int(r.w - 2 * margin - ww + 1),
    y: r.y + margin + rng.int(r.h - 2 * margin - hh + 1),
    w: ww,
    h: hh,
  };
}

/** Edge tiles of a rectangle with the outward direction, corners excluded. */
function edgeSlots(b: Rect): { p: Vec2; out: Vec2 }[] {
  const slots: { p: Vec2; out: Vec2 }[] = [];
  for (let x = b.x + 1; x < b.x + b.w - 1; x++) {
    slots.push({ p: { x, y: b.y }, out: { x: 0, y: -1 } });
    slots.push({ p: { x, y: b.y + b.h - 1 }, out: { x: 0, y: 1 } });
  }
  for (let y = b.y + 1; y < b.y + b.h - 1; y++) {
    slots.push({ p: { x: b.x, y }, out: { x: -1, y: 0 } });
    slots.push({ p: { x: b.x + b.w - 1, y }, out: { x: 1, y: 0 } });
  }
  return slots;
}

function shuffle<T>(items: T[], rng: Rng): T[] {
  for (let i = items.length - 1; i > 0; i--) {
    const j = rng.int(i + 1);
    [items[i], items[j]] = [items[j]!, items[i]!];
  }
  return items;
}

/** Walled building with doors and windows; the interior stays at ground level. */
function building(c: Canvas, b: Rect, rng: Rng, doors: number, windows: number): void {
  for (let y = b.y; y < b.y + b.h; y++) {
    for (let x = b.x; x < b.x + b.w; x++) {
      const edge = x === b.x || y === b.y || x === b.x + b.w - 1 || y === b.y + b.h - 1;
      c.force(x, y, edge ? 'wall' : 'floor', 0);
    }
  }
  const slots = shuffle(edgeSlots(b), rng);
  const openings: Vec2[] = [];
  const nearOpening = (p: Vec2) => openings.some((o) => Math.abs(o.x - p.x) + Math.abs(o.y - p.y) <= 1);
  const open = (count: number, kind: TileKind) => {
    for (const { p, out } of slots) {
      if (count <= 0) break;
      if (nearOpening(p) || c.get(p.x, p.y) !== 'wall') continue;
      c.force(p.x, p.y, kind);
      c.reserve(p.x + out.x, p.y + out.y);
      c.reserve(p.x - out.x, p.y - out.y);
      openings.push(p);
      count--;
    }
  };
  open(doors, 'door');
  open(windows, 'window');
}

/** Interior partition with a door, for buildings wide enough. */
function partition(c: Canvas, b: Rect, rng: Rng): void {
  const vertical = b.w >= b.h;
  const span = vertical ? b.w : b.h;
  if (span < 7) return;
  const at = rng.range(3, span - 4);
  const len = vertical ? b.h : b.w;
  const door = rng.range(1, len - 2);
  // Never wall off the inside of an outer door or window.
  for (let k = 1; k < len - 1; k++) {
    const x = vertical ? b.x + at : b.x + k;
    const y = vertical ? b.y + k : b.y + at;
    if (c.keep[c.i(x, y)]) return;
  }
  for (let k = 1; k < len - 1; k++) {
    const x = vertical ? b.x + at : b.x + k;
    const y = vertical ? b.y + k : b.y + at;
    if (k === door) {
      c.force(x, y, 'door');
      if (vertical) {
        c.reserve(x - 1, y);
        c.reserve(x + 1, y);
      } else {
        c.reserve(x, y - 1);
        c.reserve(x, y + 1);
      }
    } else {
      c.force(x, y, 'wall');
    }
  }
}

/** Solid raised block (rooftop at `level`) with a parapet and a ladder up. */
function raisedBlock(c: Canvas, b: Rect, rng: Rng, level: number, parapet: number, ladders = 1): void {
  for (let y = b.y; y < b.y + b.h; y++) for (let x = b.x; x < b.x + b.w; x++) c.force(x, y, 'floor', level);
  const slots = shuffle(edgeSlots(b), rng);
  let placed = 0;
  for (const { p, out } of slots) {
    if (placed >= ladders) break;
    const foot = { x: p.x + out.x, y: p.y + out.y };
    if (c.get(foot.x, foot.y) !== 'floor' || c.elevAt(foot.x, foot.y) !== level - 1) continue;
    c.force(foot.x, foot.y, 'ladder');
    c.reserve(foot.x, foot.y);
    c.reserve(foot.x + out.x, foot.y + out.y);
    c.reserve(p.x, p.y);
    c.reserve(p.x - out.x, p.y - out.y);
    placed++;
  }
  for (const { p } of edgeSlots(b)) if (rng.next() < parapet && c.free(p.x, p.y, level)) c.force(p.x, p.y, 'low');
}

// ---------------------------------------------------------------- parcels

type ParcelFn = (c: Canvas, r: Rect, rng: Rng) => void;

const CITY: Record<string, ParcelFn> = {
  office(c, r, rng) {
    const b = innerRect(r, rng, [6, 8], [6, 8]);
    building(c, b, rng, rng.range(1, 2), rng.range(2, 4));
    partition(c, b, rng);
    const inside = { x: b.x + 1, y: b.y + 1, w: b.w - 2, h: b.h - 2 };
    scatter(c, inside, rng, rng.range(2, 3), 'low');
    scatter(c, inside, rng, 1, 'high');
  },
  flats(c, r, rng) {
    const b = innerRect(r, rng, [5, 7], [4, 6]);
    raisedBlock(c, b, rng, 1, 0.55);
    if (b.w >= 5 && b.h >= 5 && rng.next() < 0.4) {
      const top = { x: b.x + 1 + rng.int(b.w - 4), y: b.y + 1 + rng.int(b.h - 4), w: 3, h: 3 };
      raisedBlock(c, top, rng, 2, 0.3);
    } else {
      scatter(c, { x: b.x + 1, y: b.y + 1, w: b.w - 2, h: b.h - 2 }, rng, rng.range(1, 2), 'high', [1, 1], 1);
    }
  },
  plaza(c, r, rng) {
    const inner = { x: r.x + 1, y: r.y + 1, w: r.w - 2, h: r.h - 2 };
    if (rng.next() < 0.5) {
      const fx = r.x + 4;
      const fy = r.y + 4;
      for (let y = fy; y < fy + 2; y++) for (let x = fx; x < fx + 2; x++) c.force(x, y, 'low');
    }
    scatter(c, inner, rng, rng.range(3, 4), 'high');
    scatter(c, inner, rng, rng.range(3, 5), 'low', [1, 2]);
  },
  parking(c, r, rng) {
    const inner = { x: r.x + 1, y: r.y + 1, w: r.w - 2, h: r.h - 2 };
    scatter(c, inner, rng, rng.range(4, 6), 'low', [2, 2]);
    scatter(c, inner, rng, rng.range(2, 3), 'high');
  },
  shop(c, r, rng) {
    const b = innerRect(r, rng, [5, 6], [5, 6]);
    building(c, b, rng, rng.range(1, 2), 3);
    scatter(c, { x: b.x + 1, y: b.y + 1, w: b.w - 2, h: b.h - 2 }, rng, 1, 'low');
    scatter(c, r, rng, 1, 'high', [2, 2]);
    scatter(c, r, rng, 2, 'low');
  },
  containers(c, r, rng) {
    const horizontal = rng.int(2) === 0;
    const b = innerRect(r, rng, horizontal ? [4, 5] : [2, 2], horizontal ? [2, 2] : [4, 5], 2);
    raisedBlock(c, b, rng, 1, 0);
    scatter(c, r, rng, rng.range(2, 3), 'high', [1, 2]);
    scatter(c, r, rng, 2, 'low');
  },
};

const WILDS: Record<string, ParcelFn> = {
  forest(c, r, rng) {
    scatter(c, r, rng, rng.range(10, 14), 'high');
    scatter(c, r, rng, rng.range(4, 6), 'low');
    scatter(c, r, rng, rng.range(1, 2), 'low', [2, 3]);
  },
  cabin(c, r, rng) {
    const b = innerRect(r, rng, [5, 6], [4, 5]);
    building(c, b, rng, 1, 2);
    scatter(c, { x: b.x + 1, y: b.y + 1, w: b.w - 2, h: b.h - 2 }, rng, 1, 'low');
    scatter(c, r, rng, rng.range(2, 3), 'low', [2, 3]);
    scatter(c, r, rng, rng.range(2, 4), 'high');
  },
  hill(c, r, rng) {
    const b = innerRect(r, rng, [5, 7], [5, 7]);
    raisedBlock(c, b, rng, 1, 0, rng.range(1, 2));
    for (const [x, y] of [[b.x, b.y], [b.x + b.w - 1, b.y], [b.x, b.y + b.h - 1], [b.x + b.w - 1, b.y + b.h - 1]] as const) {
      if (!c.keep[c.i(x, y)]) c.force(x, y, 'floor', 0);
    }
    const top = { x: b.x + 1, y: b.y + 1, w: b.w - 2, h: b.h - 2 };
    scatter(c, top, rng, rng.range(1, 3), 'high', [1, 1], 1);
    scatter(c, top, rng, rng.range(1, 2), 'low', [1, 1], 1);
    scatter(c, r, rng, rng.range(2, 4), 'high');
  },
  ruins(c, r, rng) {
    for (let k = 0; k < 3; k++) {
      const horizontal = rng.int(2) === 0;
      const len = rng.range(3, 5);
      const x0 = r.x + 1 + rng.int(r.w - 2 - (horizontal ? len : 1) + 1);
      const y0 = r.y + 1 + rng.int(r.h - 2 - (horizontal ? 1 : len) + 1);
      for (let n = 0; n < len; n++) {
        const x = horizontal ? x0 + n : x0;
        const y = horizontal ? y0 : y0 + n;
        if (c.free(x, y, 0)) c.force(x, y, n === Math.floor(len / 2) && rng.next() < 0.5 ? 'window' : 'wall');
      }
    }
    scatter(c, r, rng, rng.range(4, 6), 'low');
    scatter(c, r, rng, rng.range(1, 2), 'high');
  },
  farm(c, r, rng) {
    scatter(c, r, rng, rng.range(5, 7), 'low', [1, 2]);
    scatter(c, r, rng, 1, 'high', [2, 2]);
    scatter(c, r, rng, rng.range(1, 2), 'low', [3, 3]);
  },
};

const FACILITY: Record<string, ParcelFn> = {
  lab(c, r, rng) {
    const b = innerRect(r, rng, [7, 8], [6, 8]);
    building(c, b, rng, 2, rng.range(2, 4));
    const inside = { x: b.x + 1, y: b.y + 1, w: b.w - 2, h: b.h - 2 };
    scatter(c, inside, rng, rng.range(2, 3), 'high');
    scatter(c, inside, rng, 2, 'low', [1, 2]);
  },
  platform(c, r, rng) {
    const b = innerRect(r, rng, [5, 7], [4, 5]);
    raisedBlock(c, b, rng, 1, 0.45, 2);
    scatter(c, { x: b.x + 1, y: b.y + 1, w: b.w - 2, h: b.h - 2 }, rng, 1, 'high', [1, 1], 1);
    scatter(c, r, rng, 2, 'low');
  },
  storage(c, r, rng) {
    for (let row = r.y + 2; row < r.y + r.h - 1; row += 3) {
      for (let x = r.x + 1; x < r.x + r.w - 1; x++) {
        if ((x - r.x) % 4 === 0 || !c.free(x, row, 0)) continue;
        c.force(x, row, rng.next() < 0.4 ? 'high' : 'low');
      }
    }
  },
  reactor(c, r, rng) {
    const cx = r.x + 4;
    const cy = r.y + 4;
    for (let y = cy; y < cy + 2; y++) for (let x = cx; x < cx + 2; x++) c.force(x, y, 'wall');
    for (const [dx, dy] of [[-2, -2], [3, -2], [-2, 3], [3, 3]] as const) if (c.free(cx + dx, cy + dy, 0)) c.force(cx + dx, cy + dy, 'high');
    scatter(c, r, rng, rng.range(2, 3), 'low', [2, 3]);
  },
};

const PARCELS: Record<Biome, Record<string, ParcelFn>> = { city: CITY, wilds: WILDS, facility: FACILITY };

/** Parcel types that make good homes for each objective. */
const OBJECTIVE_PARCELS: Record<Biome, { indoor: string[]; open: string[] }> = {
  city: { indoor: ['office', 'shop'], open: ['plaza', 'parking'] },
  wilds: { indoor: ['cabin'], open: ['forest', 'farm'] },
  facility: { indoor: ['lab'], open: ['reactor', 'storage'] },
};

function streets(c: Canvas, biome: Biome, rng: Rng): void {
  const lanes: Rect[] = [];
  for (let k = 1; k < GRID; k++) {
    const at = k * (PARCEL + STREET) - STREET;
    lanes.push({ x: 0, y: at, w: GEN_SIZE, h: STREET }, { x: at, y: 0, w: STREET, h: GEN_SIZE });
  }
  for (const lane of lanes) {
    if (biome === 'city') {
      scatter(c, lane, rng, 3, 'low', [2, 2]);
      scatter(c, lane, rng, 1, 'low');
    } else if (biome === 'wilds') {
      scatter(c, lane, rng, 2, 'low');
      scatter(c, lane, rng, 2, 'high');
    } else {
      scatter(c, lane, rng, 3, 'low');
      scatter(c, lane, rng, 1, 'high');
    }
  }
}

// ------------------------------------------------------------ connectivity

/** Walkable tiles reachable from `start`, following the movement rules. */
function flood(c: Canvas, start: Vec2): Set<number> {
  const s = c.asState();
  const seen = new Set<number>([c.i(start.x, start.y)]);
  const queue = [start];
  while (queue.length) {
    const p = queue.pop()!;
    for (const d of DIRS8) {
      if (stepCost(s, p.x, p.y, d.x, d.y) === null) continue;
      const q = { x: p.x + d.x, y: p.y + d.y };
      const i = c.i(q.x, q.y);
      if (seen.has(i)) continue;
      seen.add(i);
      queue.push(q);
    }
  }
  return seen;
}

/**
 * Opens up every walkable pocket the squad could not reach: a ladder for a
 * stranded rooftop, a cleared object or a new door for a sealed ground area.
 */
function connect(c: Canvas, start: Vec2): Set<number> {
  for (let pass = 0; pass < 40; pass++) {
    const reach = flood(c, start);
    let fixed = false;
    for (let i = 0; i < c.tiles.length && !fixed; i++) {
      if (reach.has(i) || !isWalkable(c.tiles[i]!)) continue;
      const x = i % c.width;
      const y = (i - x) / c.width;
      const level = c.elev[i]!;
      for (const d of DIRS4) {
        const n = { x: x + d.x, y: y + d.y };
        if (!c.in(n.x, n.y)) continue;
        const ni = c.i(n.x, n.y);
        const kind = c.tiles[ni]!;
        if (level > 0 && reach.has(ni) && kind === 'floor' && c.elev[ni] === level - 1) {
          c.force(n.x, n.y, 'ladder');
          fixed = true;
          break;
        }
        if (kind === 'low' || kind === 'high' || kind === 'wall') {
          const beyond = { x: n.x + d.x, y: n.y + d.y };
          if (c.in(beyond.x, beyond.y) && reach.has(c.i(beyond.x, beyond.y)) && c.elev[ni] === level) {
            c.force(n.x, n.y, kind === 'wall' ? 'door' : 'floor');
            fixed = true;
            break;
          }
        }
      }
    }
    if (!fixed) return reach;
  }
  return flood(c, start);
}

// ---------------------------------------------------------------- placing

function parcelRect(bx: number, by: number): Rect {
  return { x: bx * (PARCEL + STREET), y: by * (PARCEL + STREET), w: PARCEL, h: PARCEL };
}

function tilesIn(r: Rect): Vec2[] {
  const out: Vec2[] = [];
  for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) out.push({ x, y });
  return out;
}

const dist = (a: Vec2, b: Vec2) => Math.hypot(a.x - b.x, a.y - b.y);

/** Standing tiles near `anchor` (breadth-first, same level), excluding `taken`. */
function cluster(c: Canvas, reach: Set<number>, anchor: Vec2, count: number, taken: Set<number>): Vec2[] {
  const level = c.elevAt(anchor.x, anchor.y);
  const out: Vec2[] = [];
  const seen = new Set<number>([c.i(anchor.x, anchor.y)]);
  const queue = [anchor];
  while (queue.length && out.length < count) {
    const p = queue.shift()!;
    const i = c.i(p.x, p.y);
    if (reach.has(i) && canStandOn(c.tiles[i]!) && !taken.has(i) && c.elev[i] === level) out.push(p);
    for (const d of DIRS8) {
      const q = { x: p.x + d.x, y: p.y + d.y };
      if (!c.in(q.x, q.y) || seen.has(c.i(q.x, q.y)) || dist(q, anchor) > 3) continue;
      seen.add(c.i(q.x, q.y));
      if (isWalkable(c.tiles[c.i(q.x, q.y)]!)) queue.push(q);
    }
  }
  return out;
}

/** Clears a 3×3 evacuation zone at ground level inside `r`. */
function carveEvac(c: Canvas, r: Rect, rng: Rng): Vec2[] {
  const options: { x: number; y: number; cost: number }[] = [];
  for (let y = r.y; y <= r.y + r.h - 3; y++) {
    for (let x = r.x; x <= r.x + r.w - 3; x++) {
      let cost = 0;
      for (let dy = 0; dy < 3 && cost < 99; dy++) {
        for (let dx = 0; dx < 3; dx++) {
          const kind = c.get(x + dx, y + dy);
          if (c.elevAt(x + dx, y + dy) !== 0 || (kind !== 'floor' && kind !== 'low' && kind !== 'high')) cost = 99;
          else if (kind !== 'floor') cost++;
        }
      }
      if (cost < 99) options.push({ x, y, cost: cost + rng.next() });
    }
  }
  options.sort((a, b) => a.cost - b.cost);
  const best = options[0];
  if (!best) return [];
  const zone: Vec2[] = [];
  for (let dy = 0; dy < 3; dy++) {
    for (let dx = 0; dx < 3; dx++) {
      c.force(best.x + dx, best.y + dy, 'floor');
      c.reserve(best.x + dx, best.y + dy);
      zone.push({ x: best.x + dx, y: best.y + dy });
    }
  }
  return zone;
}

const NAMES: Record<Biome, string[]> = {
  city: ['Distrito Portuario', 'Barrio Viejo', 'Centro Financiero', 'Polígono Norte', 'Ensanche', 'Mercado Central', 'Zona Universitaria'],
  wilds: ['Bosque de Lirio', 'Valle Hondo', 'Granjas del Sur', 'Pinar Quemado', 'Ruinas de Santa Úrsula', 'Afueras de Robledo'],
  facility: ['Imprenta 9', 'Imprenta 14', 'Archivo Norte', 'Sala de Escaneo', 'Imprenta Profunda'],
};

export interface GenOptions {
  biome: Biome;
  kind: MissionKind;
  /** Size of each pod, in the order the pod plan lists them. */
  podSizes: number[];
  seed: number;
}

/** Builds a full battlefield for a mission. Deterministic for a given seed. */
export function generateLayout(opts: GenOptions): MissionLayout {
  const rng = new Rng(opts.seed);
  const { biome, kind } = opts;
  const c = new Canvas(GEN_SIZE, GEN_SIZE);

  // Squad in one corner parcel, objective in the opposite one.
  const corner = rng.int(4);
  const spawnB = { x: corner % 2 === 0 ? 0 : GRID - 1, y: corner < 2 ? 0 : GRID - 1 };
  const objB = { x: GRID - 1 - spawnB.x, y: GRID - 1 - spawnB.y };
  const sideCorners = [
    { x: spawnB.x, y: objB.y },
    { x: objB.x, y: spawnB.y },
  ];
  const evacB = rng.pick(sideCorners);
  const needsEvac = kind !== 'elimination';
  const indoorObjective = kind === 'recovery' || kind === 'rescue';

  // Keep the squad's corner clear.
  const spawnRect = parcelRect(spawnB.x, spawnB.y);
  const cornerTile = { x: spawnB.x === 0 ? 0 : GEN_SIZE - 1, y: spawnB.y === 0 ? 0 : GEN_SIZE - 1 };
  for (let dy = 0; dy < 4; dy++) {
    for (let dx = 0; dx < 4; dx++) {
      c.reserve(cornerTile.x + (spawnB.x === 0 ? dx : -dx), cornerTile.y + (spawnB.y === 0 ? dy : -dy));
    }
  }

  const kinds = PARCELS[biome];
  const names = Object.keys(kinds);
  for (let by = 0; by < GRID; by++) {
    for (let bx = 0; bx < GRID; bx++) {
      const r = parcelRect(bx, by);
      if (bx === spawnB.x && by === spawnB.y) {
        scatter(c, r, rng, 3, 'low', [1, 2]);
        scatter(c, r, rng, 2, 'high');
        continue;
      }
      let type = rng.pick(names);
      if (bx === objB.x && by === objB.y) type = rng.pick(indoorObjective ? OBJECTIVE_PARCELS[biome].indoor : OBJECTIVE_PARCELS[biome].open);
      kinds[type]!(c, r, rng);
    }
  }
  streets(c, biome, rng);
  let evacZone: Vec2[] = [];
  if (needsEvac) {
    const r = parcelRect(evacB.x, evacB.y);
    evacZone = carveEvac(c, r, rng);
    if (!evacZone.length) {
      // Built-up parcel: use the street next to it.
      const x = Math.max(0, r.x - STREET);
      const y = Math.max(0, r.y - STREET);
      evacZone = carveEvac(c, { x, y, w: Math.min(GEN_SIZE, r.x + r.w + STREET) - x, h: Math.min(GEN_SIZE, r.y + r.h + STREET) - y }, rng);
    }
  }

  const startTile = { x: cornerTile.x, y: cornerTile.y };
  const reach = connect(c, startTile);
  const taken = new Set<number>();
  const standable = (p: Vec2) => reach.has(c.i(p.x, p.y)) && canStandOn(c.get(p.x, p.y)) && !taken.has(c.i(p.x, p.y));

  // Squad: the tiles closest to the corner.
  const spawns = tilesIn(spawnRect)
    .filter((p) => standable(p) && c.elevAt(p.x, p.y) === 0)
    .sort((a, b) => dist(a, startTile) - dist(b, startTile) || a.y - b.y || a.x - b.x)
    .slice(0, MAX_SQUAD);
  for (const p of spawns) taken.add(c.i(p.x, p.y));
  const squadCenter = spawns.reduce((acc, p) => ({ x: acc.x + p.x / spawns.length, y: acc.y + p.y / spawns.length }), { x: 0, y: 0 });

  // Objective.
  const objRect = parcelRect(objB.x, objB.y);
  const objTiles = shuffle(tilesIn(objRect).filter((p) => standable(p) && !evacZone.some((z) => z.x === p.x && z.y === p.y)), rng);
  const enclosed = (p: Vec2) => DIRS4.filter((d) => c.get(p.x + d.x, p.y + d.y) === 'wall').length;
  let item: Vec2 | null = null;
  let terminal: Vec2 | null = null;
  let objectiveSpot: Vec2 | null = null;
  if (kind === 'recovery' || kind === 'rescue') {
    const spot = [...objTiles].sort((a, b) => enclosed(b) - enclosed(a))[0] ?? null;
    if (kind === 'recovery') item = spot;
    else objectiveSpot = spot;
  } else if (kind === 'sabotage') {
    objectiveSpot = objTiles.find((p) => c.elevAt(p.x, p.y) === 0 && DIRS8.filter((d) => standable({ x: p.x + d.x, y: p.y + d.y })).length >= 5) ?? objTiles[0] ?? null;
  } else if (kind === 'hack') {
    for (const p of objTiles) {
      if (c.elevAt(p.x, p.y) !== 0 || DIRS8.filter((d) => standable({ x: p.x + d.x, y: p.y + d.y })).length < 3) continue;
      c.force(p.x, p.y, 'console');
      const after = flood(c, startTile);
      if ([...reach].every((i) => i === c.i(p.x, p.y) || after.has(i))) {
        terminal = p;
        reach.delete(c.i(p.x, p.y));
        break;
      }
      c.force(p.x, p.y, 'floor');
    }
  }
  if (objectiveSpot) taken.add(c.i(objectiveSpot.x, objectiveSpot.y));
  for (const z of evacZone) taken.add(c.i(z.x, z.y));

  // Pods: spread over the far parcels, never next to the squad.
  const anchors: Vec2[] = [];
  const pods: MissionLayout['pods'] = [];
  const candidates = shuffle(
    Array.from({ length: c.tiles.length }, (_, i) => ({ x: i % c.width, y: Math.floor(i / c.width) })).filter(
      (p) => standable(p) && dist(p, squadCenter) >= 14,
    ),
    rng,
  );
  const far = (p: Vec2, gap: number) => anchors.every((a) => dist(a, p) >= gap);
  for (const size of opts.podSizes) {
    let anchor = candidates.find((p) => standable(p) && far(p, 8)) ?? candidates.find((p) => standable(p) && far(p, 4)) ?? candidates.find(standable);
    if (!anchor) break;
    const tiles = cluster(c, reach, anchor, size, taken);
    if (tiles.length < size) {
      anchor = candidates.find((p) => standable(p) && far(p, 3) && cluster(c, reach, p, size, taken).length >= size);
      if (!anchor) break;
    }
    const members = cluster(c, reach, anchor, size, taken);
    for (const p of members) taken.add(c.i(p.x, p.y));
    anchors.push(anchor);
    const waypoints = shuffle(candidates.filter((p) => dist(p, anchor!) >= 7 && dist(p, squadCenter) >= 12), rng).slice(0, 2);
    pods.push({ tiles: members, patrol: [anchor, ...waypoints] });
  }

  return {
    id: `gen-${biome}-${opts.seed}`,
    name: rng.pick(NAMES[biome]),
    biome,
    width: c.width,
    height: c.height,
    tiles: c.tiles,
    elev: c.elev,
    spawns,
    pods,
    item,
    terminal,
    objectiveSpot,
    evacZone,
  };
}

/** Layout of a hand-made map; objectives go on its 'D' spot. */
export function handmadeLayout(def: MapDef, kind: MissionKind): MissionLayout {
  const map = parseMap(def);
  const tiles = [...map.tiles];
  const spot = map.item;
  const objective = kind !== 'elimination' && spot !== null;
  if (kind === 'hack' && spot) tiles[spot.y * map.width + spot.x] = 'console';
  return {
    id: def.id,
    name: def.name,
    biome: def.biome,
    width: map.width,
    height: map.height,
    tiles,
    elev: map.elev,
    spawns: map.spawns,
    pods: Object.keys(def.pods)
      .sort()
      .map((key) => ({ tiles: map.podSpawns[key] ?? [], patrol: [] })),
    item: kind === 'recovery' ? spot : null,
    terminal: kind === 'hack' ? spot : null,
    objectiveSpot: objective && (kind === 'sabotage' || kind === 'rescue') ? spot : null,
    evacZone: objective ? map.evacZone : [],
  };
}

export function handmadeMap(id: string): MapDef | undefined {
  return MAPS[id];
}
