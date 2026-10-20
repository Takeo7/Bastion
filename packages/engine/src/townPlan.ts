// Town plans (docs/MAPAS.md §5, phase 3): where a town's streets run, the outline that closes the map round what is
// played (§5.10) and the ways in.
//
// A grid town is a net of streets 26–34 tiles apart, one of them the avenue (7 wide; the others 5), some of them
// stepping sideways at a crossing. An old town grows from its square: three or four streets 3–4 wide leave it, each
// bending every 8–14 tiles, a ring goes round it and alleys of 2 sew the biggest blocks. The map stays a rectangle,
// but what is played has a shape of its own: along the edges, blocks shut up, fenced lots, ruins and works close it
// at a depth of their own, and at most once a river, the railway, the motorway or a barrier runs along a side. Two to
// four streets stay open to the edge: the squad comes in by one, the dead by all of them. What is left is cut into
// rectangles, the blocks the town's programs build on.
import { Rng } from './rng';
import type { FrameKind, FramePiece, Vec2 } from './types';

export interface PlanRect {
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface PlanRoad {
  r: PlanRect;
  /** Runs north–south. */
  vertical: boolean;
  /** The avenue, the map's one wide street (the bus waits on it). */
  avenue?: boolean;
}

/** A street open to the map's edge: the squad comes in by one, the dead by all of them. */
export interface Entry {
  /** The map's side it leaves by (a DIRS4 index: 0 north, 1 east, 2 south, 3 west). */
  side: number;
  road: PlanRoad;
}

export interface TownPlan {
  width: number;
  height: number;
  roads: PlanRoad[];
  /** Out of play, by tile (docs/MAPAS.md §5.10). */
  out: boolean[];
  frame: FramePiece[];
  /** The ground left to build on, in rectangles: each a block for the town's programs. */
  blocks: PlanRect[];
  /** An old town's square, open ground in the middle of it. */
  square: PlanRect | null;
  entries: Entry[];
  /** The squad's way in (one of `entries`) and the tile on it, a little in from the edge, it gathers round. */
  squadEntry: Entry;
  squad: Vec2;
}

/** Street widths (the roadway; the pavements are the blocks'): the avenue, a grid's street, an old town's, an alley. */
export const AVENUE = 7;
export const STREET = 5;
const OLD_STREET = [3, 4] as const;
const ALLEY = 2;
/** A grid's streets, centre to centre (docs/MAPAS.md §5.1: 26–34): blocks deep enough for a courtyard now and then (§4.1). */
const SPACING = [26, 34] as const;
/** A grid's with big blocks (a hospital's: 34 apart). */
const WIDE_SPACING = [32, 36] as const;
/** An industrial estate's (docs/MAPAS.md §4.1: blocks of 24–36, streets for the lorries). */
const ESTATE_SPACING = [34, 40] as const;
/** The first street from the map's edge: the blocks along the edge keep some ground past the outline. */
const FIRST = [13, 19] as const;
/** Nothing is played this close to the map's edge but on the ways in (rules.ts EDGE_BAND: where the dead come in). */
const EDGE_SHUT = 2;
/** The narrowest a block may be; thinner leftovers are open ground. */
const MIN_BLOCK = 5;
/**
 * What stays of a block the outline cuts into, at least: a row of buildings seven deep and its pavement (Arte, 20/10:
 * a strip of five or six holds nothing); less, and it is closed whole. Only where that leaves too little to play, a
 * back wall and a narrow yard instead.
 */
const MIN_KEPT = 9;
/** The deepest a block along the edge is closed whole; deeper ones keep their front. */
const WHOLE_MAX = 12;
/** The least share of the map to play: closed deeper than that, the edge is closed again by back walls only. */
const PLAYED_LEAST = 0.65;

const rect = (x: number, y: number, w: number, h: number): PlanRect => ({ x, y, w, h });
const DIRS: readonly Vec2[] = [
  { x: 0, y: -1 },
  { x: 1, y: 0 },
  { x: 0, y: 1 },
  { x: -1, y: 0 },
];

function weighted<T>(rng: Rng, options: readonly (readonly [T, number])[]): T {
  const total = options.reduce((a, [, w]) => a + w, 0);
  let roll = rng.next() * total;
  for (const [v, w] of options) if ((roll -= w) < 0) return v;
  return options[options.length - 1]![0];
}

/** Street centre lines across `span`: the first FIRST in, then every `spacing`, none closer than FIRST to the far edge. */
function axes(span: number, rng: Rng, spacing: readonly [number, number]): number[] {
  const out: number[] = [];
  for (let a = rng.range(FIRST[0], FIRST[1]); a <= span - FIRST[0]; a += rng.range(spacing[0], spacing[1])) out.push(a);
  return out;
}

/**
 * A grid town's streets: the axes both ways, the one nearest the middle the avenue. Each street is cut at its
 * crossings, and a street that isn't the avenue may step one to three tiles sideways at one (§5.1: «calles que se
 * desplazan 1–3 casillas en un cruce»). Every stretch reaches across the streets it meets, so they join.
 */
function gridRoads(W: number, H: number, rng: Rng, wide: boolean, estate = false): PlanRoad[] {
  const spacing = estate ? ESTATE_SPACING : wide ? WIDE_SPACING : SPACING;
  const xs = axes(W, rng, spacing);
  const ys = axes(H, rng, spacing);
  const lines = [...xs.map((a) => ({ vertical: true, a, off: Math.abs(a - W / 2) })), ...ys.map((a) => ({ vertical: false, a, off: Math.abs(a - H / 2) }))];
  let avenue = lines[0];
  let low = Infinity;
  for (const l of lines) {
    const score = l.off + rng.next() * 8;
    if (score < low) [low, avenue] = [score, l];
  }
  const widthOf = (l: { vertical: boolean; a: number }) => (l === avenue ? AVENUE : STREET);
  const roads: PlanRoad[] = [];
  for (const line of lines) {
    const w = widthOf(line);
    const span = line.vertical ? H : W;
    const across = lines.filter((l) => l.vertical !== line.vertical).sort((a, b) => a.a - b.a);
    let shift = 0;
    for (let k = 0; k <= across.length; k++) {
      const before = across[k - 1];
      const after = across[k];
      // A step at the crossing this stretch starts from.
      if (before && line !== avenue && rng.next() < 0.3) shift = Math.max(-3, Math.min(3, shift + (rng.int(2) ? 1 : -1) * rng.range(1, 3)));
      const from = before ? before.a - Math.floor(widthOf(before) / 2) : 0;
      const to = after ? after.a - Math.floor(widthOf(after) / 2) + widthOf(after) : span;
      const at = line.a - Math.floor(w / 2) + shift;
      roads.push({ r: line.vertical ? rect(at, from, w, to - from) : rect(from, at, to - from, w), vertical: line.vertical, ...(line === avenue ? { avenue: true } : {}) });
    }
  }
  return roads;
}

/**
 * An old town's streets: three or four leave its square, by sides of their own, and run out to the map's edge,
 * stepping two or three tiles sideways every 8–14 (the same way, mostly, so the street runs on a slant); a ring goes
 * round the square, some way off, stepping halfway along each side. The first is the high street, straight and five
 * wide, the town's avenue (the bus waits on it). The square itself, open ground.
 */
function oldTownRoads(W: number, H: number, rng: Rng): { roads: PlanRoad[]; square: PlanRect } {
  const sw = rng.range(12, 16);
  const sh = rng.range(12, 16);
  const square = rect(Math.round(W / 2 - sw / 2) + rng.range(-4, 4), Math.round(H / 2 - sh / 2) + rng.range(-4, 4), sw, sh);
  const roads: PlanRoad[] = [];
  const sides = [0, 1, 2, 3];
  for (let i = sides.length - 1; i > 0; i--) {
    const j = rng.int(i + 1);
    [sides[i], sides[j]] = [sides[j]!, sides[i]!];
  }
  // The high street first, out to the east or the south: away from where the squad comes in (the north or the west),
  // so the bus has its way out.
  const away = sides.findIndex((s) => s === 1 || s === 2);
  [sides[0], sides[away]] = [sides[away]!, sides[0]!];
  for (const [n, side] of sides.slice(0, rng.range(3, 4)).entries()) {
    const high = n === 0;
    const w = high ? STREET : rng.pick(OLD_STREET);
    const vertical = side % 2 === 0;
    const d = DIRS[side]!;
    const len = vertical ? square.w : square.h;
    // Where along the square's side it leaves, clear of its corners; and the way it drifts.
    let u = (vertical ? square.x : square.y) + rng.range(2, Math.max(2, len - w - 2));
    const drift = rng.int(2) ? 1 : -1;
    // From the square's side out to the edge, stretch by stretch.
    let t = side === 0 ? square.y : side === 1 ? square.x + square.w : side === 2 ? square.y + square.h : square.x;
    const edge = side === 0 ? 0 : side === 1 ? W : side === 2 ? H : 0;
    for (;;) {
      const stretch = rng.range(8, 14);
      const left = Math.abs(edge - t);
      const run = high || left - stretch < 6 ? left : stretch;
      const a = d.x + d.y > 0 ? t : t - run;
      roads.push({ r: vertical ? rect(u, a, w, run) : rect(a, u, run, w), vertical, ...(high ? { avenue: true } : {}) });
      t += (d.x + d.y) * run;
      if (run === left) break;
      // The step: a short cross street, as wide as the street, the stretch after it shifted.
      const step = (rng.next() < 0.75 ? drift : -drift) * rng.range(2, 3);
      const lo = Math.min(u, u + step);
      const hi = Math.max(u, u + step) + w;
      const max = vertical ? W : H;
      if (lo < 4 || hi > max - 4) continue;
      const b = d.x + d.y > 0 ? t : t - w;
      roads.push({ r: vertical ? rect(lo, b, hi - lo, w) : rect(b, lo, w, hi - lo), vertical: !vertical });
      u += step;
    }
  }
  // The ring, 9–12 tiles off the square, three wide; it steps halfway along a side now and then.
  const gap = rng.range(9, 12);
  const ring = rect(square.x - gap - 3, square.y - gap - 3, square.w + 2 * gap + 6, square.h + 2 * gap + 6);
  for (const side of [0, 1, 2, 3]) {
    const vertical = side % 2 === 1;
    const along = vertical ? ring.h : ring.w;
    const fixed = side === 0 ? ring.y : side === 1 ? ring.x + ring.w - 3 : side === 2 ? ring.y + ring.h - 3 : ring.x;
    const start = vertical ? ring.y : ring.x;
    const step = rng.next() < 0.4 ? (rng.int(2) ? 2 : -2) : 0;
    const half = Math.floor(along / 2);
    for (const [from, to, off] of [[start, start + half + 3, 0], [start + half, start + along, step]] as const) {
      roads.push({ r: vertical ? rect(fixed + off, from, 3, to - from) : rect(from, fixed + off, to - from, 3), vertical });
    }
    if (step) roads.push({ r: vertical ? rect(Math.min(fixed, fixed + step), start + half, 3 + Math.abs(step), 3) : rect(start + half, Math.min(fixed, fixed + step), 3, 3 + Math.abs(step)), vertical: !vertical });
  }
  return { roads: roads.map((road) => ({ ...road, r: clip(road.r, W, H) })).filter((road) => road.r.w > 0 && road.r.h > 0), square };
}

function clip(r: PlanRect, W: number, H: number): PlanRect {
  const x = Math.max(0, r.x);
  const y = Math.max(0, r.y);
  return rect(x, y, Math.min(W, r.x + r.w) - x, Math.min(H, r.y + r.h) - y);
}

/** The biggest rectangle of tiles where `mask` holds (row-major, W × H), at least `min` a side, or null. */
function largestRect(mask: Uint8Array, W: number, H: number, min: number): PlanRect | null {
  const heights = new Int32Array(W);
  let found: PlanRect | null = null;
  let area = 0;
  const stack: number[] = [];
  for (let y = 0; y < H; y++) {
    for (let x = 0; x < W; x++) heights[x] = mask[y * W + x] ? heights[x]! + 1 : 0;
    stack.length = 0;
    for (let x = 0; x <= W; x++) {
      const h = x < W ? heights[x]! : 0;
      while (stack.length && heights[stack[stack.length - 1]!]! >= h) {
        const top = stack.pop()!;
        const height = heights[top]!;
        const left = stack.length ? stack[stack.length - 1]! + 1 : 0;
        const width = x - left;
        if (height * width > area && height >= min && width >= min) {
          area = height * width;
          found = rect(left, y - height + 1, width, height);
        }
      }
      stack.push(x);
    }
  }
  return found;
}

/** `mask` cut into rectangles, the biggest first, down to `min` a side (what is left stays in `mask`). */
export function rectsOf(mask: Uint8Array, W: number, H: number, min: number): PlanRect[] {
  const out: PlanRect[] = [];
  for (let r = largestRect(mask, W, H, min); r; r = largestRect(mask, W, H, min)) {
    out.push(r);
    for (let y = r.y; y < r.y + r.h; y++) mask.fill(0, y * W + r.x, y * W + r.x + r.w);
  }
  return out;
}

/** The roads' ends on the map's edge: each stretch that touches a side, by that side. */
function roadEnds(roads: PlanRoad[], W: number, H: number): Entry[] {
  const ends: Entry[] = [];
  for (const road of roads) {
    const { r } = road;
    if (road.vertical) {
      if (r.y === 0) ends.push({ side: 0, road });
      if (r.y + r.h === H) ends.push({ side: 2, road });
    } else {
      if (r.x === 0) ends.push({ side: 3, road });
      if (r.x + r.w === W) ends.push({ side: 1, road });
    }
  }
  return ends;
}

/** The middle of an end of a road on the map's edge, on that edge. */
function endAt(e: Entry, W: number, H: number): Vec2 {
  const { r } = e.road;
  const mx = r.x + Math.floor(r.w / 2);
  const my = r.y + Math.floor(r.h / 2);
  return e.side === 0 ? { x: mx, y: 0 } : e.side === 1 ? { x: W - 1, y: my } : e.side === 2 ? { x: mx, y: H - 1 } : { x: 0, y: my };
}

/** Kinds of closed ground along a town's edge, and how often each. */
const CLOSED: readonly (readonly [FrameKind, number])[] = [
  ['block', 50],
  ['lot', 20],
  ['ruin', 15],
  ['works', 15],
];
/** What runs along one side, at most once a map, and how often each. */
const LINEAR: readonly (readonly [FrameKind, number])[] = [
  ['river', 35],
  ['rail', 25],
  ['highway', 20],
  ['barrier', 20],
];
/** An industrial estate's: container yards and fenced lots, works, a few sheds shut up; the railway or the motorway. */
const ESTATE_CLOSED: readonly (readonly [FrameKind, number])[] = [
  ['containers', 35],
  ['lot', 25],
  ['works', 20],
  ['block', 20],
];
const ESTATE_LINEAR: readonly (readonly [FrameKind, number])[] = [
  ['rail', 45],
  ['highway', 35],
  ['barrier', 20],
];

/**
 * The plan of a town of W × H: a grid's or an old town's streets, the outline round what is played, the ways in, and
 * the blocks left to build on. The squad comes in from the north or the west, near the north-west corner (the map is
 * turned at random afterwards).
 */
export function townPlan(W: number, H: number, rng: Rng, old: boolean, wide = false, estate = false): TownPlan {
  const { roads, square } = old ? oldTownRoads(W, H, rng) : { roads: gridRoads(W, H, rng, wide, estate), square: null };
  const N = W * H;
  const road = new Uint8Array(N);
  for (const { r } of roads) for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) road[y * W + x] = 1;

  // ---------------------------------------------------------------- the ways in
  const ends = roadEnds(roads, W, H);
  const near = (e: Entry) => {
    const p = endAt(e, W, H);
    return p.x + p.y;
  };
  // The squad's: on the north or the west side, nearest the corner (an avenue's, only if there is no other).
  const squadEntry = [...ends].filter((e) => e.side === 0 || e.side === 3).sort((a, b) => Number(!!a.road.avenue) - Number(!!b.road.avenue) || near(a) - near(b))[0] ?? ends[0]!;
  const sp = endAt(squadEntry, W, H);
  // A river, the railway, the motorway or a barrier along one side (never the squad's).
  const wanted = rng.next() < (old ? 0.35 : 0.5) ? weighted(rng, estate ? ESTATE_LINEAR : LINEAR) : null;
  // Never the squad's side, nor where the avenue goes out (the bus's way).
  const avenueSides = new Set(ends.filter((e) => e.road.avenue && e !== squadEntry).map((e) => e.side));
  const lineSides = [0, 1, 2, 3].filter((s) => s !== squadEntry.side && !avenueSides.has(s));
  const lineSide = wanted && lineSides.length ? rng.pick(lineSides) : -1;
  const linear = lineSide >= 0 ? wanted : null;
  const crosses = (e: Entry) => e.side === lineSide && linear !== 'barrier';
  // The others: far from the squad's and from each other, the avenue's far end first (the bus leaves by it).
  const apart = (a: Entry, b: Entry) => {
    const p = endAt(a, W, H);
    const q = endAt(b, W, H);
    return Math.abs(p.x - q.x) + Math.abs(p.y - q.y);
  };
  const others = ends
    .filter((e) => e !== squadEntry && !crosses(e) && apart(e, squadEntry) >= 0.6 * Math.max(W, H))
    .sort((a, b) => Number(!!b.road.avenue) - Number(!!a.road.avenue) || apart(b, squadEntry) - apart(a, squadEntry) + rng.next() * 8);
  const entries: Entry[] = [squadEntry];
  const want = rng.range(2, 4);
  for (const e of others) {
    if (entries.length >= want) break;
    if (entries.every((o) => apart(o, e) >= 16)) entries.push(e);
  }
  // The avenue always goes out somewhere (the bus leaves by it): its end furthest from the squad's, if none is in.
  if (!entries.some((e) => e.road.avenue)) {
    const out = ends.filter((e) => e.road.avenue && e !== squadEntry && !crosses(e) && endAt(e, W, H).x + endAt(e, W, H).y !== sp.x + sp.y);
    const far = out.sort((a, b) => apart(b, squadEntry) - apart(a, squadEntry))[0];
    if (far) entries.splice(entries.length >= 4 ? entries.length - 1 : entries.length, entries.length >= 4 ? 1 : 0, far);
  }

  // ------------------------------------------------------------- the outline
  // Each tile out of play gets a kind (an index into kinds) and storeys; 0 is in play.
  const kinds: FrameKind[] = [];
  const kindOf = new Int16Array(N).fill(-1);
  const storeysOf = new Uint8Array(N);
  const close = (x: number, y: number, kind: number, storeys: number) => {
    if (x < 0 || y < 0 || x >= W || y >= H) return;
    const i = y * W + x;
    if (kindOf[i]! >= 0) return;
    kindOf[i] = kind;
    storeysOf[i] = storeys;
  };
  const kindIndex = (k: FrameKind) => {
    const i = kinds.indexOf(k);
    return i >= 0 ? i : kinds.push(k) - 1;
  };
  /** Depth in from side `s` of tile (x, y). */
  const depth = (s: number, x: number, y: number) => (s === 0 ? y : s === 1 ? W - 1 - x : s === 2 ? H - 1 - y : x);
  /** The tile `d` in from side `s`, `u` along it. */
  const at = (s: number, u: number, d: number): Vec2 => (s === 0 ? { x: u, y: d } : s === 1 ? { x: W - 1 - d, y: u } : s === 2 ? { x: u, y: H - 1 - d } : { x: d, y: u });
  const along = (s: number) => (s % 2 === 0 ? W : H);
  const isEntry = (x: number, y: number) => entries.some((e) => inside(e.road.r, x, y));

  // The linear piece: from the edge to two tiles short of the first street along that side (a quay, a pavement),
  // in stretches of 10–16 each a tile or two shallower now and then; every street that crosses it ends there.
  if (linear) {
    const k = kindIndex(linear);
    const parallel = roads.filter((r) => r.vertical === (lineSide % 2 === 1) && !crossesSide(r, lineSide, W, H));
    const gapTo = (u: number) => {
      let g = Infinity;
      for (const r of parallel) {
        const p = at(lineSide, u, 0);
        const spans = lineSide % 2 === 0 ? inRange(p.x, r.r.x, r.r.w) : inRange(p.y, r.r.y, r.r.h);
        if (!spans) continue;
        const d = lineSide === 0 ? r.r.y : lineSide === 1 ? W - r.r.x - r.r.w : lineSide === 2 ? H - r.r.y - r.r.h : r.r.x;
        if (d >= 0) g = Math.min(g, d);
      }
      return g;
    };
    const storeys = linear === 'highway' ? 2 : linear === 'rail' ? 1 : 0;
    for (let u = 0; u < along(lineSide); ) {
      const run = rng.range(10, 16);
      const trim = rng.next() < 0.4 ? rng.range(1, 2) : 0;
      // With no street near that side, a band of its own depth.
      const own = rng.range(8, 11);
      for (let v = u; v < Math.min(along(lineSide), u + run); v++) {
        const g = gapTo(v);
        const d = Math.min(g, Math.max(6, (g <= 20 ? g - 2 : own) - trim));
        for (let t = 0; t < d; t++) {
          const p = at(lineSide, v, t);
          if (linear === 'barrier' && isEntry(p.x, p.y)) continue;
          close(p.x, p.y, k, storeys);
        }
      }
      u += run;
    }
  }

  // Blocks shut up, lots, ruins and works along the other sides: in runs between the streets that meet the edge, a
  // depth of their own (a back wall two or three in, half a block, or all of it), the corners from both sides.
  // Far from the squad's way in, the blocks keep their ground (only a back wall): the objective is out there.
  const farFrom = (p: Vec2) => Math.abs(p.x - sp.x) + Math.abs(p.y - sp.y) > 0.9 * Math.max(W, H);
  // Should that leave too little to play (PLAYED_LEAST), the edge is closed again by back walls only.
  const banded = { kinds: kindOf.slice(), storeys: storeysOf.slice() };
  for (let pass = 0; pass < 2; pass++) {
    if (pass) {
      const played = kindOf.reduce((n, k) => n + (k < 0 ? 1 : 0), 0) / N;
      if (played >= PLAYED_LEAST) break;
      kindOf.set(banded.kinds);
      storeysOf.set(banded.storeys);
    }
    closeEdges(pass === 1);
  }
  function closeEdges(thinOnly: boolean): void {
    for (const s of [0, 1, 2, 3]) {
      if (s === lineSide) continue;
      let u = 0;
      while (u < along(s)) {
        const p = at(s, u, 0);
        if (road[p.y * W + p.x]) {
          u++;
          continue;
        }
        // A run of block along the edge, to the next street or 8–16 long.
        let end = u;
        const most = u + rng.range(8, 16);
        while (end < along(s) && end < most && !road[at(s, end, 0).y * W + at(s, end, 0).x]) end++;
        // Stop short of a street's end only where the rest is too short to stand alone.
        while (end < along(s) && !road[at(s, end, 0).y * W + at(s, end, 0).x] && end - most < 6 && runLeft(s, end) < 6) end++;
        const style = thinOnly || farFrom(at(s, Math.floor((u + end) / 2), 0)) ? 'thin' : weighted(rng, [['thin', 40], ['half', 40], ['whole', 20]] as const);
        const thin = rng.range(EDGE_SHUT, 3);
        const wanted = style === 'thin' ? thin : style === 'half' ? rng.range(4, 6) : Infinity;
        const k = kindIndex(weighted(rng, estate ? ESTATE_CLOSED : CLOSED));
        const kind = kinds[k]!;
        const storeys = kind === 'block' ? rng.range(2, 4) : kind === 'ruin' ? rng.range(1, 3) : kind === 'works' ? rng.range(1, 2) : 0;
        for (let v = u; v < end; v++) {
          // How deep the block runs here: to the next street in.
          let room = 0;
          while (room < Math.max(W, H)) {
            const q = at(s, v, room);
            if (q.x < 0 || q.y < 0 || q.x >= W || q.y >= H || road[q.y * W + q.x]) break;
            room++;
          }
          // Closed whole if so meant (a block not too deep); else what was meant if enough is left in front, else a back
          // wall and a narrow yard; what is too thin even for that, closed.
          const kept = thinOnly ? 4 : MIN_KEPT;
          const d = style === 'whole' && room <= WHOLE_MAX ? room : room - wanted >= kept ? wanted : room - thin >= kept ? thin : room;
          for (let t = 0; t < d; t++) {
            const q = at(s, v, t);
            close(q.x, q.y, k, storeys);
          }
        }
        u = end;
      }
    }
  }
  function runLeft(s: number, from: number): number {
    let n = 0;
    while (from + n < along(s) && !road[at(s, from + n, 0).y * W + at(s, from + n, 0).x]) n++;
    return n;
  }

  // A street that meets the edge and isn't a way in: shut as deep as the blocks beside it are (a barricade there, no
  // dead end between walls), the street going on beyond it.
  const street = kindIndex('street');
  for (const e of ends) {
    if (entries.includes(e) || crosses(e)) continue;
    const { r } = e.road;
    const width = e.side % 2 === 0 ? r.w : r.h;
    const first = e.side % 2 === 0 ? r.x : r.y;
    // How deep its neighbours are shut, just beside it.
    const shutAt = (u: number) => {
      let d = 0;
      while (d < Math.max(W, H)) {
        const q = at(e.side, u, d);
        if (q.x < 0 || q.y < 0 || q.x >= W || q.y >= H || kindOf[q.y * W + q.x]! < 0) break;
        d++;
      }
      return d;
    };
    // No further than its own stretch, short of the street it meets (a whole row of blocks shut beside it is no
    // reason to shut the street across the map), and never a way in.
    const sides = [shutAt(first - 1), shutAt(first + width)].filter((d) => d > 0);
    const span = e.side % 2 === 0 ? r.h : r.w;
    const d = Math.max(EDGE_SHUT, Math.min(Math.max(EDGE_SHUT, ...sides), span - STREET - 1));
    for (let u = first; u < first + width; u++) {
      for (let t = 0; t < d; t++) {
        const q = at(e.side, u, t);
        if (road[q.y * W + q.x] && !isEntry(q.x, q.y)) close(q.x, q.y, street, 0);
      }
    }
  }
  // Nothing played on the edge but the ways in: any tile left there is shut with what is next to it.
  for (let pass = 0; pass < EDGE_SHUT; pass++) {
    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        const i = y * W + x;
        if (kindOf[i]! >= 0 || isEntry(x, y)) continue;
        if (Math.min(x, y, W - 1 - x, H - 1 - y) >= EDGE_SHUT) continue;
        const by = DIRS.map((d) => ({ x: x + d.x, y: y + d.y })).find((q) => q.x >= 0 && q.y >= 0 && q.x < W && q.y < H && kindOf[q.y * W + q.x]! >= 0);
        if (by) close(x, y, kindOf[by.y * W + by.x]!, storeysOf[by.y * W + by.x]!);
        else if (pass === EDGE_SHUT - 1) close(x, y, kindIndex(road[i] ? 'street' : 'lot'), 0);
      }
    }
  }

  // The outline in rectangles, one piece per tile: by kind and storeys.
  const out = Array.from({ length: N }, (_, i) => kindOf[i]! >= 0);
  const frame: FramePiece[] = [];
  const keys = new Set<number>();
  for (let i = 0; i < N; i++) if (kindOf[i]! >= 0) keys.add(kindOf[i]! * 256 + storeysOf[i]!);
  for (const key of [...keys].sort((a, b) => a - b)) {
    const mask = new Uint8Array(N);
    for (let i = 0; i < N; i++) if (kindOf[i]! >= 0 && kindOf[i]! * 256 + storeysOf[i]! === key) mask[i] = 1;
    const kind = kinds[Math.floor(key / 256)]!;
    const storeys = key % 256;
    for (const r of rectsOf(mask, W, H, 1)) {
      frame.push({ kind, ...r, ...(kind === linear ? { side: lineSide } : {}), ...(storeys ? { storeys } : {}) });
    }
  }

  // --------------------------------------------------------------- the blocks
  // A street the outline shut is shut: what is left of the roads is played.
  const kept = roads.filter((r) => !shut(r.r, out, W));
  const free = new Uint8Array(N);
  for (let i = 0; i < N; i++) free[i] = !out[i] && !road[i] ? 1 : 0;
  if (square) for (let y = square.y; y < square.y + square.h; y++) for (let x = square.x; x < square.x + square.w; x++) free[y * W + x] = 0;
  // An old town's biggest blocks sewn through by an alley, across their length.
  if (old) {
    for (const b of rectsOf(free.slice(), W, H, MIN_BLOCK)) {
      const long = Math.max(b.w, b.h);
      if (long < 26 || Math.min(b.w, b.h) < 10) continue;
      const vertical = b.w > b.h;
      const a = (vertical ? b.x : b.y) + Math.floor(long / 2) + rng.range(-3, 3);
      const alley: PlanRoad = { r: vertical ? rect(a, b.y, ALLEY, b.h) : rect(b.x, a, b.w, ALLEY), vertical };
      kept.push(alley);
      for (let y = alley.r.y; y < alley.r.y + alley.r.h; y++) for (let x = alley.r.x; x < alley.r.x + alley.r.w; x++) free[y * W + x] = 0;
    }
  }
  const blocks = rectsOf(free, W, H, MIN_BLOCK);

  // Where the squad gathers: on its street, four tiles in from the edge.
  const squad = squadEntry.side === 0 ? { x: sp.x, y: 4 } : { x: 4, y: sp.y };
  return { width: W, height: H, roads: kept, out, frame, blocks, square, entries, squadEntry, squad };
}

const inRange = (v: number, from: number, n: number) => v >= from && v < from + n;
const inside = (r: PlanRect, x: number, y: number) => inRange(x, r.x, r.w) && inRange(y, r.y, r.h);

/** Whether a road runs along (not across) side `s`'s edge: a street parallel to it never crosses it. */
function crossesSide(road: PlanRoad, s: number, W: number, H: number): boolean {
  const { r } = road;
  return s === 0 ? r.y === 0 && road.vertical : s === 1 ? r.x + r.w === W && !road.vertical : s === 2 ? r.y + r.h === H && road.vertical : r.x === 0 && !road.vertical;
}

/** Whether all of a road's rectangle is out of play. */
function shut(r: PlanRect, out: boolean[], W: number): boolean {
  for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) if (!out[y * W + x]) return false;
  return true;
}
