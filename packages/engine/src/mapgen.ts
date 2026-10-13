// Procedural battlefields.
//
// Every map is GEN_SIZE×GEN_SIZE and is laid out in a canonical frame: the squad
// starts in the top-left corner, the objective lies in the far (bottom-right)
// area and the evacuation zone near the top-right corner. The finished map is
// then mirrored and/or transposed at random, so the squad may start anywhere.
//
// City and facility maps are districts: a jittered binary split of the ground
// into blocks of varied sizes, with streets (or yards) 2–4 tiles wide between
// them that may jog sideways or end against a block. Each block holds one
// program: a row of houses or shops, an office, a block of flats, a warehouse,
// a plaza, a car park; labs, hangars, loading platforms, a reactor or storage
// rows inside a facility's perimeter. Wilds are open country shaped by noise:
// groves with clearings between them, rock outcrops, blob-shaped hills, fenced
// fields and a dirt track with a farmstead or ruins along it.
//
// Buildings have real rooms (at least 3×3, usually 4–6 tiles a side), a door
// on the street side and doors between rooms, and windows at a regular spacing.
// All the houses (offices, halls…) of one map share a scale. A final pass makes
// sure every walkable area can be reached (adding ladders or doors if needed),
// and only reachable tiles are used to place the squad, the pods and objectives.
import { MAPS, parseMap, type MapDef } from './maps';
import { stepCost } from './path';
import { Rng } from './rng';
import { canStandOn, DIRS4, DIRS8, isWalkable } from './grid';
import { carPartner, decorCode, decorOf } from './decor';
import type { Biome, Decor, DecorKind, GameState, GroundKind, MissionKind, TileKind, Vec2 } from './types';

/** Side of every procedural map, in tiles. */
export const GEN_SIZE = 48;
/** Tiles reserved for the squad at the start. */
export const MAX_SQUAD = 8;
/** No pod starts closer than this to the middle of the squad. */
const POD_DISTANCE = 16;

const OPEN: GroundKind = 0;
const ROAD: GroundKind = 1;
const INDOOR: GroundKind = 2;

/** Everything a mission needs from a map: terrain plus where things go. */
export interface MissionLayout {
  id: string;
  name: string;
  biome: Biome;
  width: number;
  height: number;
  tiles: TileKind[];
  elev: number[];
  /** What the floor of each tile is (procedural maps only). */
  ground?: GroundKind[];
  /** What each piece of cover is meant to be (procedural maps only; cosmetic). */
  decor?: Decor[];
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

// ------------------------------------------------------------------- rects

interface Rect {
  x: number;
  y: number;
  w: number;
  h: number;
}

const rect = (x: number, y: number, w: number, h: number): Rect => ({ x, y, w, h });
const inset = (r: Rect, n: number): Rect => rect(r.x + n, r.y + n, r.w - 2 * n, r.h - 2 * n);
const inRect = (r: Rect, p: Vec2) => p.x >= r.x && p.y >= r.y && p.x < r.x + r.w && p.y < r.y + r.h;
const centre = (r: Rect): Vec2 => ({ x: r.x + (r.w - 1) / 2, y: r.y + (r.h - 1) / 2 });
const clamp = (v: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, v));
const dist = (a: Vec2, b: Vec2) => Math.hypot(a.x - b.x, a.y - b.y);
const add = (a: Vec2, b: Vec2): Vec2 => ({ x: a.x + b.x, y: a.y + b.y });

// Sides are numbered like DIRS4 (N, E, S, W): DIRS4[s] points out of side s.
const opposite = (s: number) => (s + 2) % 4;
/** Length of side `s` of `r`. */
const sideLen = (r: Rect, s: number) => (s % 2 === 0 ? r.w : r.h);
/** Depth of `r` seen from side `s`. */
const depthOf = (r: Rect, s: number) => (s % 2 === 0 ? r.h : r.w);

/**
 * Rectangle inside `r` in the frame of side `s`: `u` runs along that side (the
 * same way for opposite sides) and `d` is the depth from it.
 */
function local(r: Rect, s: number, u: number, d: number, lu: number, ld: number): Rect {
  switch (s) {
    case 0:
      return rect(r.x + u, r.y + d, lu, ld);
    case 2:
      return rect(r.x + u, r.y + r.h - d - ld, lu, ld);
    case 3:
      return rect(r.x + d, r.y + u, ld, lu);
    default:
      return rect(r.x + r.w - d - ld, r.y + u, ld, lu);
  }
}

function localPt(r: Rect, s: number, u: number, d: number): Vec2 {
  const q = local(r, s, u, d, 1, 1);
  return { x: q.x, y: q.y };
}

/** `r` without its first `n` tiles from side `s`. */
const cut = (r: Rect, s: number, n: number): Rect => local(r, s, 0, n, sideLen(r, s), depthOf(r, s) - n);

function tilesIn(r: Rect): Vec2[] {
  const out: Vec2[] = [];
  for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) out.push({ x, y });
  return out;
}

function shuffle<T>(items: T[], rng: Rng): T[] {
  for (let i = items.length - 1; i > 0; i--) {
    const j = rng.int(i + 1);
    [items[i], items[j]] = [items[j]!, items[i]!];
  }
  return items;
}

/** The option with the lowest key; keys are computed once, so the result only depends on the rng. */
function best<T>(items: T[], key: (item: T) => number): T | undefined {
  let pick: T | undefined;
  let low = Infinity;
  for (const item of items) {
    const k = key(item);
    if (k < low) {
      low = k;
      pick = item;
    }
  }
  return pick;
}

// ------------------------------------------------------------------ canvas

/** Mutable terrain under construction. `keep` marks tiles that must stay free. */
class Canvas {
  readonly tiles: TileKind[];
  readonly elev: number[];
  readonly keep: boolean[];
  readonly ground: GroundKind[];
  readonly decor: Decor[];

  constructor(
    readonly width: number,
    readonly height: number,
  ) {
    this.tiles = new Array<TileKind>(width * height).fill('floor');
    this.elev = new Array<number>(width * height).fill(0);
    this.keep = new Array<boolean>(width * height).fill(false);
    this.ground = new Array<GroundKind>(width * height).fill(OPEN);
    this.decor = new Array<Decor>(width * height).fill(0);
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

  groundAt(x: number, y: number): GroundKind {
    return this.in(x, y) ? this.ground[this.i(x, y)]! : OPEN;
  }

  force(x: number, y: number, kind: TileKind, elev?: number): void {
    if (!this.in(x, y)) return;
    this.tiles[this.i(x, y)] = kind;
    if (elev !== undefined) this.elev[this.i(x, y)] = elev;
  }

  /** Notes what the object on `p` is meant to be, and which way it faces. */
  dress(p: Vec2, kind: DecorKind, facing = 0): void {
    if (this.in(p.x, p.y)) this.decor[this.i(p.x, p.y)] = decorCode(kind, facing);
  }

  paint(r: Rect, ground: GroundKind): void {
    for (const p of tilesIn(r)) if (this.in(p.x, p.y)) this.ground[this.i(p.x, p.y)] = ground;
  }

  reserve(x: number, y: number): void {
    if (this.in(x, y)) this.keep[this.i(x, y)] = true;
  }

  /** Free floor that may receive an object. */
  free(x: number, y: number, elev = 0): boolean {
    if (!this.in(x, y)) return false;
    const i = this.i(x, y);
    return this.tiles[i] === 'floor' && !this.keep[i] && this.elev[i] === elev;
  }

  /** Untouched open ground (no road, no building, nothing reserved) over all of `r`. */
  vacant(r: Rect): boolean {
    if (r.x < 0 || r.y < 0 || r.x + r.w > this.width || r.y + r.h > this.height) return false;
    return tilesIn(r).every((p) => this.free(p.x, p.y) && this.groundAt(p.x, p.y) === OPEN);
  }

  /** A view usable by the engine's movement rules. */
  asState(): GameState {
    return { width: this.width, height: this.height, tiles: this.tiles, elev: this.elev } as unknown as GameState;
  }
}

/** What an object is meant to be (decor) and which way it faces (a DIRS4 index, north by default). */
type Look = [DecorKind, number?];

/** Puts an object on free floor (furniture, fences, rows: no spacing rules). */
function put(c: Canvas, p: Vec2, kind: TileKind, look?: Look, elev = 0): boolean {
  if (!c.free(p.x, p.y, elev)) return false;
  c.force(p.x, p.y, kind);
  if (look) c.dress(p, look[0], look[1]);
  return true;
}

type Part = [number, number, TileKind, Look?];

/** `parts` turned by `q` quarter turns (north turns to east first, like DIRS4); their looks turn with them. */
const turn = (parts: Part[], q: number): Part[] =>
  parts.map(([x, y, k, look]) => {
    const [tx, ty] = q % 4 === 0 ? [x, y] : q % 4 === 1 ? [-y, x] : q % 4 === 2 ? [-x, -y] : [y, -x];
    return [tx, ty, k, look && [look[0], ((look[1] ?? 0) + q) % 4]];
  });

/** `parts` dressed as what they are meant to be, by kind of cover (`high` defaults to `low`). */
const dressed = (parts: Part[], low: DecorKind, high: DecorKind = low): Part[] => parts.map(([x, y, k]) => [x, y, k, [k === 'high' ? high : low]]);

/**
 * A stable number for a position, for cosmetic choices (which street furniture,
 * which room is a dining room) that must not draw from the generator's rng: the
 * layout of a seed stays the same whatever the map is dressed as.
 */
const mix = (x: number, y: number) => (((x * 73856093) ^ (y * 19349663)) >>> 0) % 997;

/**
 * Places a group of cover objects that works as one firing position, if every
 * tile is free and nothing else stands right around it, so groups never wall
 * off a passage. `parts` are offsets from `at`.
 */
function group(c: Canvas, at: Vec2, parts: Part[], elev = 0): boolean {
  const tiles = parts.map(([dx, dy, kind, look]) => ({ x: at.x + dx, y: at.y + dy, kind, look }));
  const own = (x: number, y: number) => tiles.some((t) => t.x === x && t.y === y);
  for (const t of tiles) {
    if (!c.free(t.x, t.y, elev)) return false;
    for (const d of DIRS8) {
      const x = t.x + d.x;
      const y = t.y + d.y;
      if (!c.in(x, y) || own(x, y)) continue;
      const k = c.get(x, y);
      if (c.elevAt(x, y) !== elev || (k !== 'floor' && k !== 'ladder')) return false;
    }
  }
  for (const t of tiles) {
    c.force(t.x, t.y, t.kind);
    if (t.look) c.dress(t, t.look[0], t.look[1]);
  }
  return true;
}

// Cover groups, drawn facing north (turned at random when placed).
const BARRICADE: Part[] = [[0, 0, 'low'], [1, 0, 'low'], [2, 0, 'low']];
const CORNER: Part[] = [[0, 0, 'high'], [1, 0, 'low'], [0, 1, 'low']];
const PAIR: Part[] = [[0, 0, 'high'], [1, 0, 'low']];
const CRATES: Part[] = [[0, 0, 'high'], [1, 0, 'low'], [0, 1, 'low'], [1, 1, 'low']];
const ROCKS: Part[][] = [
  [[0, 0, 'high'], [1, 0, 'low'], [-1, 1, 'low'], [0, 1, 'low']],
  [[0, 0, 'low'], [1, 0, 'low'], [1, 1, 'high'], [2, 1, 'low']],
  [[0, 0, 'low'], [1, 0, 'high'], [2, 0, 'low'], [1, 1, 'low'], [1, -1, 'low']],
  [[0, 0, 'low'], [1, 0, 'low'], [0, 1, 'low']],
];
const OUTCROPS = ROCKS.map((parts) => dressed(parts, 'rock'));
const RUBBLE = ROCKS.map((parts) => dressed(parts, 'rubble'));

/** Tries to drop one of `shapes` (any rotation) around `at`, within `spread` tiles. */
function groupNear(c: Canvas, at: Vec2, rng: Rng, shapes: Part[][], spread = 2, elev = 0, tries = 12): boolean {
  for (let k = 0; k < tries; k++) {
    const p = { x: Math.round(at.x) + rng.range(-spread, spread), y: Math.round(at.y) + rng.range(-spread, spread) };
    if (group(c, p, turn(rng.pick(shapes), rng.int(4)), elev)) return true;
  }
  return false;
}

// ------------------------------------------------------------------- style

/** One scale per map: all the houses (offices, halls…) of a map are alike. */
interface Style {
  /** Houses, shops and cabins: outer side along the street, and depth (7–9). */
  house: number;
  houseDepth: number;
  /** Alley between neighbouring houses. */
  gap: number;
  /** Offices and labs (10–14). */
  office: number;
  officeDepth: number;
  /** Warehouses, barns and hangars (12–16). */
  hall: number;
  /** From one window to the next along a wall. */
  windowEvery: number;
  /** From one planter (bin, bollard) to the next along a sidewalk. */
  furniture: number;
}

function pickStyle(rng: Rng): Style {
  return {
    house: rng.range(7, 9),
    houseDepth: rng.range(7, 9),
    gap: rng.range(1, 2),
    office: rng.range(11, 14),
    officeDepth: rng.range(10, 13),
    hall: rng.range(13, 16),
    windowEvery: rng.range(2, 3),
    furniture: rng.range(6, 8),
  };
}

// --------------------------------------------------------------- buildings

/** How the interior is divided. */
type Plan = 'rooms' | 'open' | 'corridor' | 'shop' | 'hall';
/** What stands inside. */
type Furnish = 'home' | 'shop' | 'office' | 'lab' | 'store' | 'barn' | 'bare';

interface Building {
  outer: Rect;
  rooms: Rect[];
}

/**
 * A wall across a building with a door (or, `width` 2, an opening) somewhere
 * along it; the tiles on both sides of the opening stay clear.
 */
function partition(c: Canvas, line: Vec2[], normal: Vec2, rng: Rng, width = 1): void {
  const at = rng.int(line.length - width + 1);
  line.forEach((p, k) => {
    const opening = k >= at && k < at + width;
    c.force(p.x, p.y, opening ? (width > 1 ? 'floor' : 'door') : 'wall');
    if (opening) {
      c.reserve(p.x, p.y);
      c.reserve(p.x + normal.x, p.y + normal.y);
      c.reserve(p.x - normal.x, p.y - normal.y);
    }
  });
}

/**
 * Splits an interior into rooms of at most `maxSide` tiles a side (never under
 * 3), a wall with a door between each two. A new wall never ends against an
 * existing door.
 */
function splitRooms(c: Canvas, r: Rect, rng: Rng, out: Rect[], maxSide: number, open: boolean): void {
  const vertical = r.w > r.h || (r.w === r.h && rng.int(2) === 0);
  const span = vertical ? r.w : r.h;
  if (span <= maxSide || span < 7) {
    out.push(r);
    return;
  }
  const options: number[] = [];
  for (let k = 3; k <= span - 4; k++) {
    const ends = vertical ? [{ x: r.x + k, y: r.y - 1 }, { x: r.x + k, y: r.y + r.h }] : [{ x: r.x - 1, y: r.y + k }, { x: r.x + r.w, y: r.y + k }];
    if (ends.every((e) => c.get(e.x, e.y) === 'wall')) options.push(k);
  }
  const k = best(options, (o) => Math.abs(o - (span - 1) / 2) + rng.next() * 1.5);
  if (k === undefined) {
    out.push(r);
    return;
  }
  const line = vertical ? tilesIn(rect(r.x + k, r.y, 1, r.h)) : tilesIn(rect(r.x, r.y + k, r.w, 1));
  partition(c, line, vertical ? DIRS4[1]! : DIRS4[2]!, rng, open ? 2 : 1);
  splitRooms(c, vertical ? rect(r.x, r.y, k, r.h) : rect(r.x, r.y, r.w, k), rng, out, maxSide, open);
  splitRooms(c, vertical ? rect(r.x + k + 1, r.y, r.w - k - 1, r.h) : rect(r.x, r.y + k + 1, r.w, r.h - k - 1), rng, out, maxSide, open);
}

/** Splits `length` tiles into rooms of 3–6 with a 1-tile wall between each two, as evenly as possible. */
function segments(length: number, rng: Rng): number[] {
  const n = Math.max(1, Math.ceil((length + 1) / 7));
  const total = length - (n - 1);
  const sizes = new Array<number>(n).fill(Math.floor(total / n));
  for (const k of shuffle([...sizes.keys()], rng).slice(0, total - sizes[0]! * n)) sizes[k]!++;
  return sizes;
}

/** Outside the wall tile `p` (out of side `s`) there is open ground to step on. */
function opensOnto(c: Canvas, p: Vec2, s: number): boolean {
  const q = add(p, DIRS4[s]!);
  return c.in(q.x, q.y) && c.elevAt(q.x, q.y) === 0 && (c.get(q.x, q.y) === 'floor' || c.get(q.x, q.y) === 'ladder') && c.groundAt(q.x, q.y) !== INDOOR;
}

/** Turns wall tile `p` (on side `s`) into `kind` and keeps both of its sides clear. */
function opening(c: Canvas, p: Vec2, s: number, kind: TileKind): void {
  const d = DIRS4[s]!;
  c.force(p.x, p.y, kind);
  c.reserve(p.x, p.y);
  c.reserve(p.x + d.x, p.y + d.y);
  c.reserve(p.x - d.x, p.y - d.y);
}

/**
 * Offices and labs: a corridor two tiles wide runs from the front wall to the
 * back one, with rooms off it on one or both sides. Every room has a door onto
 * the corridor, and the corridor a double door onto the street.
 */
function corridorPlan(c: Canvas, b: Rect, front: number, rng: Rng, rooms: Rect[], doors: (number | null)[]): boolean {
  const inner = inset(b, 1);
  const across = sideLen(inner, front);
  const length = depthOf(inner, front);
  if (across < 6 || length < 7) return false;
  const strips: [number, number][] = [];
  const walls: number[] = [];
  let cu: number;
  if (across >= 10) {
    const d1 = Math.floor((across - 4) / 2) + (across % 2 === 1 ? rng.int(2) : 0);
    strips.push([0, d1], [d1 + 4, across - d1 - 4]);
    walls.push(d1, d1 + 3);
    cu = d1 + 1;
  } else {
    const d = across - 3;
    const left = rng.int(2) === 0;
    strips.push(left ? [0, d] : [3, d]);
    walls.push(left ? d : 2);
    cu = left ? d + 1 : 0;
  }
  const at = (u: number, v: number) => localPt(inner, front, u, v);
  for (const u of walls) {
    for (let v = 0; v < length; v++) {
      const p = at(u, v);
      c.force(p.x, p.y, 'wall');
    }
  }
  for (let v = 0; v < length; v++) {
    for (const u of [cu, cu + 1]) {
      const p = at(u, v);
      c.reserve(p.x, p.y);
    }
  }
  for (const [su, sw] of strips) {
    const toCorridor = su < cu ? 1 : -1;
    const wallU = su < cu ? su + sw : su - 1;
    let v0 = 0;
    segments(length, rng).forEach((n, k) => {
      if (k > 0) {
        for (let u = su; u < su + sw; u++) {
          const p = at(u, v0 - 1);
          c.force(p.x, p.y, 'wall');
        }
      }
      rooms.push(local(inner, front, su, v0, sw, n));
      const dv = v0 + Math.floor((n - 1) / 2);
      const door = at(wallU, dv);
      c.force(door.x, door.y, 'door');
      for (const p of [door, at(wallU - toCorridor, dv), at(wallU + toCorridor, dv)]) c.reserve(p.x, p.y);
      v0 += n + 1;
    });
  }
  // Double door at the front end of the corridor, a single one at the back.
  const ends = [localPt(b, front, cu + 1, 0), localPt(b, front, cu + 2, 0)];
  if (ends.every((p) => opensOnto(c, p, front))) {
    for (const p of ends) opening(c, p, front, 'door');
    doors[front] = cu + 1;
  }
  const back = opposite(front);
  const rear = localPt(b, back, cu + 1 + rng.int(2), 0);
  if (rng.next() < 0.7 && opensOnto(c, rear, back)) {
    opening(c, rear, back, 'door');
    doors[back] = cu + 1;
  }
  return true;
}

/** Shops: the shop floor on the street, with a stockroom behind it when deep enough. */
function shopPlan(c: Canvas, b: Rect, front: number, rng: Rng, rooms: Rect[]): void {
  const inner = inset(b, 1);
  const across = sideLen(inner, front);
  const depth = depthOf(inner, front);
  if (depth < 7) {
    rooms.push(inner);
    return;
  }
  const line = Array.from({ length: across }, (_, u) => localPt(inner, front, u, depth - 4));
  partition(c, line, DIRS4[front]!, rng);
  rooms.push(local(inner, front, 0, 0, across, depth - 4), local(inner, front, 0, depth - 3, across, 3));
}

/** Warehouses, barns and hangars: one open hall, sometimes with a small office in a back corner. */
function hallPlan(c: Canvas, b: Rect, front: number, rng: Rng, rooms: Rect[]): Rect | null {
  const inner = inset(b, 1);
  const across = sideLen(inner, front);
  const depth = depthOf(inner, front);
  rooms.push(inner);
  if (across < 10 || depth < 10 || rng.next() < 0.4) return null;
  const right = rng.int(2) === 0;
  const u0 = right ? across - 4 : 0;
  const wallU = right ? across - 5 : 4;
  for (let v = depth - 4; v < depth; v++) {
    const p = localPt(inner, front, wallU, v);
    c.force(p.x, p.y, 'wall');
  }
  const line = Array.from({ length: 5 }, (_, k) => localPt(inner, front, Math.min(u0, wallU) + k, depth - 5));
  for (const p of line) c.force(p.x, p.y, 'wall');
  // The door is in the office's front wall, away from the corner post.
  const door = localPt(inner, front, u0 + 1 + rng.int(2), depth - 5);
  opening(c, door, front, 'door');
  const office = local(inner, front, u0, depth - 4, 4, 4);
  rooms.push(office);
  return office;
}

/**
 * Opens `width` adjacent tiles of side `s` (doors, or a gap for a loading bay),
 * as close to the middle as the rooms allow. Returns the index along the side.
 */
function entrance(c: Canvas, b: Rect, s: number, rng: Rng, width = 1, kind: TileKind = 'door'): number | null {
  const n = sideLen(b, s);
  const back = DIRS4[opposite(s)]!;
  const options: number[] = [];
  for (let u = 1; u + width <= n - 1; u++) {
    let ok = true;
    for (let k = 0; k < width && ok; k++) {
      const p = localPt(b, s, u + k, 0);
      ok = c.get(p.x, p.y) === 'wall' && c.get(p.x + back.x, p.y + back.y) === 'floor' && opensOnto(c, p, s);
    }
    if (ok) options.push(u);
  }
  const u = best(options, (o) => Math.abs(o + (width - 1) / 2 - (n - 1) / 2) + rng.next() * 1.2);
  if (u === undefined) return null;
  for (let k = 0; k < width; k++) opening(c, localPt(b, s, u + k, 0), s, kind);
  return u + Math.floor((width - 1) / 2);
}

/**
 * Windows every `every` tiles along each outer wall, counted from the door of
 * that side (or its middle), never at a corner, beside an opening or where a
 * partition meets the wall.
 */
function windows(c: Canvas, b: Rect, every: number, doors: (number | null)[]): void {
  for (let s = 0; s < 4; s++) {
    const n = sideLen(b, s);
    const anchor = doors[s] ?? Math.floor((n - 1) / 2);
    const back = DIRS4[opposite(s)]!;
    const lateral = DIRS4[(s + 1) % 4]!;
    for (let u = 2; u <= n - 3; u++) {
      const off = Math.abs(u - anchor);
      if (off % every !== 0 || (off === 0 && doors[s] !== null)) continue;
      const p = localPt(b, s, u, 0);
      const sides = [add(p, lateral), { x: p.x - lateral.x, y: p.y - lateral.y }];
      if (c.get(p.x, p.y) !== 'wall' || c.get(p.x + back.x, p.y + back.y) !== 'floor') continue;
      if (!sides.every((q) => c.get(q.x, q.y) === 'wall' || c.get(q.x, q.y) === 'window')) continue;
      const q = add(p, DIRS4[s]!);
      if (!c.in(q.x, q.y) || c.get(q.x, q.y) === 'wall') continue;
      c.force(p.x, p.y, 'window');
    }
  }
}

/**
 * The way an object at `p` against the walls of `room` faces: away from the
 * nearest wall, and in a corner away from the long wall (a bed's head, the back
 * of a wardrobe).
 */
function inward(room: Rect, p: Vec2): number {
  const gaps = [p.y - room.y, room.x + room.w - 1 - p.x, room.y + room.h - 1 - p.y, p.x - room.x];
  const longWall = (s: number) => (room.w >= room.h) === (s % 2 === 0);
  return opposite(best([0, 1, 2, 3], (s) => gaps[s]! * 2 + (longWall(s) ? 0 : 1))!);
}

/**
 * Furniture of one room, along its walls and in its middle; door approaches
 * stay clear. Wardrobes, shelves and racks are full cover, the rest low.
 */
function furnishRoom(c: Canvas, room: Rect, rng: Rng, kind: Furnish): void {
  const long = Math.max(room.w, room.h);
  const short = Math.min(room.w, room.h);
  const alongX = room.w >= room.h;
  // a runs along the long side, b across it.
  const P = (a: number, b: number): Vec2 => (alongX ? { x: room.x + a, y: room.y + b } : { x: room.x + b, y: room.y + a });
  /** The DIRS4 index of a step (da, db) in the room's frame. */
  const toward = (da: number, db: number) => DIRS4.findIndex((d) => d.x === (alongX ? da : db) && d.y === (alongX ? db : da));
  const corners = shuffle([P(0, 0), P(long - 1, 0), P(0, short - 1), P(long - 1, short - 1)], rng);
  const corner = (k: TileKind, what: DecorKind) => corners.some((p) => put(c, p, k, [what, inward(room, p)]));
  const mid = Math.floor(long / 2);
  switch (kind) {
    case 'home':
      corner('high', 'wardrobe');
      if (short >= 4) {
        // A living room (a sofa facing the table) or a dining room (a table two tiles long).
        const dining = mix(room.x, room.y) % 2 === 0;
        put(c, P(mid, Math.floor(short / 2)), 'low', ['table', toward(0, 1)]);
        if (long >= 5) put(c, P(mid - 1, Math.floor(short / 2)), 'low', dining ? ['table', toward(0, 1)] : ['sofa', toward(1, 0)]);
      } else {
        corner('low', 'bed');
      }
      break;
    case 'office':
      if (short >= 4) {
        // Rows of desks facing each other across the room.
        const rows = short >= 7 ? [2, short - 3] : [Math.floor(short / 2)];
        for (const b of rows) for (let a = 1; a < long - 1; a++) if (long < 7 || a !== mid) put(c, P(a, b), 'low', ['desk', toward(0, b === short - 3 && rows.length > 1 ? -1 : 1)]);
      } else {
        put(c, P(mid, short - 1), 'low', ['desk', toward(0, -1)]);
      }
      corner('high', 'cabinet');
      break;
    case 'lab': {
      const wall = rng.int(2) === 0 ? 0 : short - 1;
      for (let a = 0; a < long; a++) if (a % 3 !== 2) put(c, P(a, wall), 'high', ['rack', toward(0, wall === 0 ? 1 : -1)]);
      if (short >= 5) for (let a = mid - 1; a <= mid; a++) put(c, P(a, Math.floor(short / 2)), 'low', ['desk', toward(0, wall === 0 ? 1 : -1)]);
      break;
    }
    case 'shop':
      // A counter across the room and shelves against the side walls.
      if (short >= 4) for (let a = 1; a < Math.min(long - 1, 4); a++) put(c, P(a, short - 2), 'low', ['counter', toward(0, -1)]);
      for (let a = 0; a < long; a += 2) put(c, P(a, 0), 'high', ['shelf', toward(0, 1)]);
      break;
    case 'store':
      for (let a = 0; a < long; a += 2) put(c, P(a, short - 1), 'high', ['shelf', toward(0, -1)]);
      put(c, P(mid, 0), 'low', ['crates']);
      break;
    case 'barn':
      corner('low', 'hay');
      corner('low', 'hay');
      break;
    case 'bare':
      break;
  }
}

/**
 * Rows of shelving (or hay) along a hall's depth, two-tile aisles between them
 * and a cross aisle halfway down long halls.
 */
function furnishHall(c: Canvas, inner: Rect, front: number, rng: Rng, kind: Furnish, avoid: Rect | null): void {
  const across = sideLen(inner, front);
  const depth = depthOf(inner, front);
  const rows = Math.floor((across - 2) / 3);
  const start = Math.floor((across - (3 * (rows - 1) + 1)) / 2);
  const cross = depth > 9 ? Math.floor(depth / 2) : -9;
  const blocked = avoid ? rect(avoid.x - 1, avoid.y - 1, avoid.w + 2, avoid.h + 2) : null;
  for (let r = 0; r < rows; r++) {
    const u = start + 3 * r;
    for (let v = 2; v <= depth - 3; v++) {
      if (v === cross || v === cross - 1) continue;
      if (kind === 'barn' && v % 3 === 1) continue;
      const p = localPt(inner, front, u, v);
      if (blocked && inRect(blocked, p)) continue;
      // Rows run along the depth: each piece faces across its row.
      put(c, p, kind === 'barn' ? 'low' : rng.next() < 0.8 ? 'high' : 'low', [kind === 'barn' ? 'hay' : 'shelf', (front + 1) % 4]);
    }
  }
}

/**
 * Walled building on `b` (walls included) with its front on side `front`. The
 * interior is floored indoors, divided by `plan` and furnished; doors open on
 * the street side (another side if that one is blocked) and windows follow a
 * regular spacing.
 */
function building(c: Canvas, b: Rect, rng: Rng, plan: Plan, front: number, style: Style, furnish: Furnish): Building {
  for (let y = b.y; y < b.y + b.h; y++) {
    for (let x = b.x; x < b.x + b.w; x++) {
      const edge = x === b.x || y === b.y || x === b.x + b.w - 1 || y === b.y + b.h - 1;
      c.force(x, y, edge ? 'wall' : 'floor', 0);
      c.ground[c.i(x, y)] = INDOOR;
    }
  }
  const rooms: Rect[] = [];
  const doors: (number | null)[] = [null, null, null, null];
  let office: Rect | null = null;
  if (plan === 'corridor' && corridorPlan(c, b, front, rng, rooms, doors)) {
    // Rooms and doors are in place.
  } else if (plan === 'shop') {
    shopPlan(c, b, front, rng, rooms);
  } else if (plan === 'hall') {
    office = hallPlan(c, b, front, rng, rooms);
  } else {
    splitRooms(c, inset(b, 1), rng, rooms, plan === 'open' ? 8 : 6, plan === 'open');
  }
  const width = plan === 'hall' ? 3 : 1;
  const gap: TileKind = plan === 'hall' ? 'floor' : 'door';
  for (const s of [front, (front + 1) % 4, (front + 3) % 4, opposite(front)]) {
    if (doors.some((d) => d !== null)) break;
    doors[s] = entrance(c, b, s, rng, width, gap) ?? entrance(c, b, s, rng, 1);
  }
  // A back door for houses with more than one room.
  const back = opposite(front);
  if (plan !== 'corridor' && plan !== 'hall' && rooms.length > 1 && doors[back] === null && rng.next() < 0.45) doors[back] = entrance(c, b, back, rng);
  // Shops show their goods: a window every other tile.
  windows(c, b, plan === 'shop' ? 2 : style.windowEvery, doors);
  if (plan === 'hall') {
    furnishHall(c, rooms[0]!, front, rng, furnish, office);
    if (office) furnishRoom(c, office, rng, 'office');
  } else {
    rooms.forEach((room, k) => furnishRoom(c, room, rng, furnish === 'shop' && k > 0 ? 'store' : furnish));
  }
  return { outer: b, rooms };
}

/** A building left in ruins: stretches of wall knocked down, glass gone, rubble about. */
function ruin(c: Canvas, b: Building, rng: Rng): void {
  const { outer } = b;
  for (let s = 0; s < 4; s++) {
    const n = sideLen(outer, s);
    for (let k = rng.range(1, 2); k > 0; k--) {
      const len = rng.range(2, 3);
      const u0 = rng.range(1, Math.max(1, n - 1 - len));
      for (let u = u0; u < u0 + len && u < n - 1; u++) {
        const p = localPt(outer, s, u, 0);
        if (c.get(p.x, p.y) === 'wall') c.force(p.x, p.y, 'floor');
      }
    }
  }
  for (const p of tilesIn(outer)) {
    const i = c.i(p.x, p.y);
    if (c.tiles[i] === 'window') c.tiles[i] = 'windowBroken';
    if (c.tiles[i] === 'door') c.tiles[i] = 'floor';
    c.ground[i] = OPEN;
  }
  for (const room of b.rooms) groupNear(c, centre(room), rng, RUBBLE, 1, 0, 4);
  // Nothing grows inside.
  for (const p of tilesIn(inset(outer, 1))) c.reserve(p.x, p.y);
}

/**
 * Block of flats two storeys tall, stepped down to a one-storey terrace on side
 * `terrace`: a ladder climbs from the street to the terrace and another one from
 * the terrace to the roof. Both rooftops have a parapet with regular gaps.
 */
function flats(c: Canvas, b: Rect, rng: Rng, terrace: number): void {
  const n = sideLen(b, terrace);
  const depth = depthOf(b, terrace) >= 9 ? 3 : 2;
  const roof = cut(b, terrace, depth);
  for (const p of tilesIn(b)) {
    c.force(p.x, p.y, 'floor', inRect(roof, p) ? 2 : 1);
    c.ground[c.i(p.x, p.y)] = OPEN;
  }
  const out = DIRS4[terrace]!;
  // From the street up to the terrace, in the middle of its outer side.
  const feet = [0, -1, 1, -2, 2].map((k) => Math.floor(n / 2) + k);
  for (const u of feet) {
    const edge = localPt(b, terrace, u, 0);
    const foot = add(edge, out);
    if (!c.free(foot.x, foot.y)) continue;
    c.force(foot.x, foot.y, 'ladder');
    for (const p of [foot, add(foot, out), edge, localPt(b, terrace, u, 1)]) c.reserve(p.x, p.y);
    break;
  }
  // From the terrace up to the roof, near one end so the climb crosses the terrace.
  const u2 = rng.int(2) === 0 ? 1 : n - 2;
  const step = localPt(b, terrace, u2, depth - 1);
  c.force(step.x, step.y, 'ladder');
  for (const p of [step, localPt(b, terrace, u2, depth), localPt(b, terrace, u2, depth + 1)]) c.reserve(p.x, p.y);
  parapet(c, roof, 2, [0, 1, 2, 3]);
  parapet(c, local(b, terrace, 0, 0, n, depth), 1, [terrace, (terrace + 1) % 4, (terrace + 3) % 4]);
  // A water tank on the roof.
  groupNear(c, centre(roof), rng, [[[0, 0, 'high', ['tank']]]], 1, 2, 6);
}

/** Low parapet along the given sides of a rooftop, with a gap every four tiles. */
function parapet(c: Canvas, r: Rect, level: number, sides: number[]): void {
  for (const s of sides) {
    const n = sideLen(r, s);
    for (let u = 0; u < n; u++) if (u % 4 !== 2 || u === 0 || u === n - 1) put(c, localPt(r, s, u, 0), 'low', undefined, level);
  }
}

/** One-storey solid block (shipping container, loading platform) with a ladder at each end. */
function platform(c: Canvas, r: Rect, rng: Rng, ladders: number): void {
  for (const p of tilesIn(r)) {
    c.force(p.x, p.y, 'floor', 1);
    c.ground[c.i(p.x, p.y)] = OPEN;
  }
  const ends = r.w >= r.h ? [3, 1] : [0, 2];
  for (const s of shuffle(ends, rng).slice(0, ladders)) {
    const n = sideLen(r, s);
    for (const u of [Math.floor(n / 2), Math.floor(n / 2) - 1, 0, n - 1]) {
      const edge = localPt(r, s, u, 0);
      const foot = add(edge, DIRS4[s]!);
      if (!c.free(foot.x, foot.y)) continue;
      c.force(foot.x, foot.y, 'ladder');
      for (const p of [foot, add(foot, DIRS4[s]!), edge]) c.reserve(p.x, p.y);
      break;
    }
  }
}

// -------------------------------------------------------------- districts

interface Road {
  r: Rect;
  /** Runs north–south. */
  vertical: boolean;
}

interface SplitRules {
  minBlock: number;
  maxBlock: number;
  /** Street width range by depth of the split (the first split is the avenue). */
  widths: [number, number][];
  /** Chances that a street jogs sideways halfway, or ends against a block. */
  jog: number;
  deadEnd: number;
  /** Chance of keeping a block whole, by depth. */
  stop: number[];
}

const swapRect = (r: Rect): Rect => rect(r.y, r.x, r.h, r.w);

/** One street across `r`, running north–south (the caller transposes for the other axis). */
function cutStreet(r: Rect, w: number, depth: number, rng: Rng, rules: SplitRules): { parts: Rect[]; streets: Road[] } {
  const m = rules.minBlock;
  const lo = r.x + m;
  const hi = r.x + r.w - m - w;
  const a = clamp(Math.round((lo + hi) / 2 + (rng.next() - 0.5) * (hi - lo) * 0.9), lo, hi);
  const right = r.x + r.w;
  const bottom = r.y + r.h;
  const roll = rng.next();
  if (roll < rules.jog && r.h >= 2 * m + w && hi - lo >= 2) {
    // The street steps sideways halfway along, with a short cross street.
    const yj = clamp(r.y + Math.round((r.h - w) / 2 + (rng.next() - 0.5) * (r.h - 2 * m - w)), r.y + m, bottom - m - w);
    const b = clamp(a + (rng.int(2) === 0 ? -1 : 1) * rng.range(2, 4), lo, hi);
    if (Math.abs(b - a) >= 2) {
      const streets: Road[] = [
        { r: rect(a, r.y, w, yj - r.y), vertical: true },
        { r: rect(Math.min(a, b), yj, Math.abs(b - a) + w, w), vertical: false },
        { r: rect(b, yj + w, w, bottom - yj - w), vertical: true },
      ];
      const parts =
        a < b
          ? [rect(r.x, r.y, a - r.x, yj + w - r.y), rect(a + w, r.y, right - a - w, yj - r.y), rect(r.x, yj + w, b - r.x, bottom - yj - w), rect(b + w, yj, right - b - w, bottom - yj)]
          : [rect(r.x, r.y, a - r.x, yj - r.y), rect(a + w, r.y, right - a - w, yj + w - r.y), rect(r.x, yj, b - r.x, bottom - yj), rect(b + w, yj + w, right - b - w, bottom - yj - w)];
      return { parts, streets };
    }
  }
  if (depth > 0 && roll < rules.jog + rules.deadEnd && r.h >= 2 * m + 2) {
    // A dead end: the street stops against a block that spans the whole width.
    const len = rng.range(m + 1, r.h - m);
    if (rng.int(2) === 0) {
      return {
        streets: [{ r: rect(a, r.y, w, len), vertical: true }],
        parts: [rect(r.x, r.y, a - r.x, len), rect(a + w, r.y, right - a - w, len), rect(r.x, r.y + len, r.w, r.h - len)],
      };
    }
    return {
      streets: [{ r: rect(a, bottom - len, w, len), vertical: true }],
      parts: [rect(r.x, bottom - len, a - r.x, len), rect(a + w, bottom - len, right - a - w, len), rect(r.x, r.y, r.w, r.h - len)],
    };
  }
  return {
    streets: [{ r: rect(a, r.y, w, r.h), vertical: true }],
    parts: [rect(r.x, r.y, a - r.x, r.h), rect(a + w, r.y, right - a - w, r.h)],
  };
}

/**
 * Jittered binary split of `area` into blocks separated by streets: a wide
 * avenue first, narrower streets inside, some of them jogging or ending.
 */
function district(area: Rect, rng: Rng, rules: SplitRules): { blocks: Rect[]; roads: Road[] } {
  const blocks: Rect[] = [];
  const roads: Road[] = [];
  const split = (r: Rect, depth: number): void => {
    const [lo, hi] = rules.widths[Math.min(depth, rules.widths.length - 1)]!;
    const w = rng.range(lo, hi);
    const canX = r.w >= 2 * rules.minBlock + w;
    const canY = r.h >= 2 * rules.minBlock + w;
    const big = r.w > rules.maxBlock || r.h > rules.maxBlock;
    if ((!canX && !canY) || (!big && rng.next() < (rules.stop[depth] ?? 1))) {
      blocks.push(r);
      return;
    }
    const vertical = canX && (!canY || r.w > r.h || (r.w === r.h && rng.int(2) === 0));
    const result = cutStreet(vertical ? r : swapRect(r), w, depth, rng, rules);
    for (const s of result.streets) roads.push(vertical ? s : { r: swapRect(s.r), vertical: !s.vertical });
    for (const p of result.parts) split(vertical ? p : swapRect(p), depth + 1);
  };
  split(area, 0);
  return { blocks, roads };
}

/** Whether side `s` of `r` looks onto a road for most of its length. */
function facesRoad(c: Canvas, r: Rect, s: number): boolean {
  const d = DIRS4[s]!;
  const n = sideLen(r, s);
  let road = 0;
  for (let u = 0; u < n; u++) {
    const p = add(localPt(r, s, u, 0), d);
    if (c.groundAt(p.x, p.y) === ROAD && c.in(p.x, p.y)) road++;
  }
  return road * 2 >= n;
}

/** The side a lot shows to the street: its longest side on a road, else the one towards the map centre. */
function frontOf(c: Canvas, lot: Rect, roads: boolean[], rng: Rng): number {
  const sides = [0, 1, 2, 3].filter((s) => roads[s]);
  if (sides.length) return best(sides, (s) => -sideLen(lot, s) + rng.next() * 3)!;
  const mid = c.width / 2;
  const to = centre(lot);
  return Math.abs(mid - to.x) > Math.abs(mid - to.y) ? (mid > to.x ? 1 : 3) : mid > to.y ? 2 : 0;
}

/**
 * Cars parked along the curb of streets three or more tiles wide: two tiles
 * each, in line with the street, never at a crossing.
 */
function parkCars(c: Canvas, roads: Road[], rng: Rng, chance: number): void {
  for (const road of roads) {
    const width = road.vertical ? road.r.w : road.r.h;
    const length = road.vertical ? road.r.h : road.r.w;
    if (width < 3) continue;
    const at = (lane: number, t: number): Vec2 => (road.vertical ? { x: road.r.x + lane, y: road.r.y + t } : { x: road.r.x + t, y: road.r.y + lane });
    const lanes = width >= 4 ? [0, width - 1] : [rng.int(2) === 0 ? 0 : width - 1];
    for (const lane of lanes) {
      const curb = lane === 0 ? -1 : width;
      const inner = lane === 0 ? 1 : width - 2;
      // Cars keep to the right: on the far lane of a north–south street they face north, and so on.
      const facing = road.vertical ? (lane === 0 ? 2 : 0) : lane === 0 ? 3 : 1;
      const ahead = facing === 1 || facing === 2 ? 1 : 0;
      for (let t = 1 + rng.int(3); t + 2 < length; t++) {
        if (rng.next() > chance) continue;
        const car = [at(lane, t), at(lane, t + 1)];
        const kerb = [-1, 0, 1, 2].map((k) => at(curb, t + k));
        const clear = [at(lane, t - 1), at(lane, t + 2), at(inner, t), at(inner, t + 1)];
        if (!car.every((p) => c.free(p.x, p.y) && c.groundAt(p.x, p.y) === ROAD)) continue;
        if (kerb.some((p) => c.groundAt(p.x, p.y) === ROAD)) continue;
        if (!clear.every((p) => c.get(p.x, p.y) === 'floor')) continue;
        for (const p of car) c.force(p.x, p.y, 'low');
        c.dress(car[ahead]!, 'carFront', facing);
        c.dress(car[1 - ahead]!, 'carRear', facing);
        t += 2 + rng.int(3);
      }
    }
  }
}

/** Street trees, planters or bins (the same all along one side) at an even spacing along the sidewalks of a block. */
function sidewalk(c: Canvas, block: Rect, roads: boolean[], every: number, kind: TileKind): void {
  for (let s = 0; s < 4; s++) {
    if (!roads[s]) continue;
    const n = sideLen(block, s);
    const what = (['streetTree', 'planter', 'streetTree', 'bin'] as const)[mix(block.x + s, block.y) % 4]!;
    const first = 2 + Math.floor(((n - 5) % every) / 2);
    for (let u = first; u <= n - 3; u += every) {
      const p = localPt(block, s, u, 0);
      const q = localPt(block, s, u, 1);
      // Never in front of a door or right against a facade's opening.
      if (c.get(q.x, q.y) === 'door' || c.get(q.x, q.y) === 'floor' && c.keep[c.i(q.x, q.y)]) continue;
      put(c, p, kind, [what, s]);
    }
  }
}

/** `n` positions from `a` to `b` (inclusive) at an even spacing of about `step`, symmetric. */
function spaced(a: number, b: number, step: number): number[] {
  const n = Math.max(1, Math.round((b - a) / step));
  return Array.from({ length: n + 1 }, (_, k) => a + Math.round((k * (b - a)) / n));
}

/**
 * A square: a fountain with a statue in the middle, planters mirrored around
 * it on a regular grid and a kiosk or two by the edges.
 */
function plaza(c: Canvas, r: Rect, rng: Rng): void {
  const m = centre(r);
  const cx = Math.floor(m.x);
  const cy = Math.floor(m.y);
  // The basin's rim: an edge faces out of the fountain; a corner faces one of its
  // two outer sides, the other one on its left (the north-west corner faces north).
  const rim = (k: number): Look => ['fountainCorner', k];
  const edge = (k: number): Look => ['fountain', k];
  if (r.w >= 9 && r.h >= 9) {
    group(c, { x: cx - 1, y: cy - 1 }, [
      [0, 0, 'low', rim(0)],
      [1, 0, 'low', edge(0)],
      [2, 0, 'low', rim(1)],
      [0, 1, 'low', edge(3)],
      [1, 1, 'high', ['statue']],
      [2, 1, 'low', edge(1)],
      [0, 2, 'low', rim(3)],
      [1, 2, 'low', edge(2)],
      [2, 2, 'low', rim(2)],
    ]);
  } else if (r.w >= 6 && r.h >= 6) {
    group(c, { x: cx, y: cy }, [[0, 0, 'low', rim(0)], [1, 0, 'low', rim(1)], [0, 1, 'low', rim(3)], [1, 1, 'low', rim(2)]]);
  }
  if (r.w >= 8 && r.h >= 8) {
    const alongX = r.w >= r.h;
    const len = r.w >= 12 && r.h >= 12 ? 3 : 2;
    for (const x of spaced(r.x + 2, r.x + r.w - 3, 6)) {
      for (const y of spaced(r.y + 2, r.y + r.h - 3, 6)) {
        if (Math.abs(x - cx) <= 3 && Math.abs(y - cy) <= 3) continue;
        // Mirrored: planters on the far side grow back towards the middle.
        const x0 = alongX && x > cx ? x - len + 1 : x;
        const y0 = !alongX && y > cy ? y - len + 1 : y;
        group(c, { x: x0, y: y0 }, Array.from({ length: len }, (_, k): Part => (alongX ? [k, 0, 'low', ['planter', 0]] : [0, k, 'low', ['planter', 1]])));
      }
    }
  }
  const sides = shuffle([0, 1, 2, 3], rng).slice(0, rng.range(1, 2));
  for (const side of sides) {
    const p = localPt(r, side, Math.floor(sideLen(r, side) / 2) + rng.range(-1, 1), 1);
    group(c, p, turn(dressed(PAIR, 'bin', 'kiosk'), rng.int(4)));
  }
}

/** Car park: bays across the lot facing an aisle, two rows when deep enough, and a booth. */
function parking(c: Canvas, r: Rect, rng: Rng, front: number): void {
  const across = sideLen(r, front);
  const depth = depthOf(r, front);
  if (across < 5 || depth < 4) return;
  const rows = depth >= 10 ? [1, depth - 3] : [depth >= 6 ? 2 : 1];
  for (const v of rows) {
    // Nose out: the first row faces into the lot, the second one back towards the street.
    const first = v === rows[0];
    const facing = first ? opposite(front) : front;
    for (let u = 1; u < across - 1; u += 2) {
      if (rng.next() < 0.35) continue;
      const a = localPt(r, front, u, v);
      const b = localPt(r, front, u, v + 1);
      if (c.free(a.x, a.y) && c.free(b.x, b.y)) {
        c.force(a.x, a.y, 'low');
        c.force(b.x, b.y, 'low');
        c.dress(first ? b : a, 'carFront', facing);
        c.dress(first ? a : b, 'carRear', facing);
      }
    }
  }
  group(c, localPt(r, front, rng.int(2) === 0 ? 0 : across - 1, 0), [[0, 0, 'high', ['kiosk']]]);
}

/** A row of houses or shops on the front of a lot, and another one on the back when the lot is deep. */
function rowOfBuildings(c: Canvas, lot: Rect, front: number, roads: boolean[], rng: Rng, style: Style, plan: Plan, furnish: Furnish): Building[] {
  const L = sideLen(lot, front);
  const D = depthOf(lot, front);
  const hw = Math.min(style.house, L);
  const hd = Math.min(style.houseDepth, D);
  if (hw < 7 || hd < 7) return [];
  const back = opposite(front);
  // A second row along the back when the lot is deep enough (on a road, or onto the yards between them).
  const rows = D >= 2 * hd + 2 ? [front, back] : [front];
  // A single row of houses with room to spare stands back from the street behind a front garden.
  const setback = plan === 'rooms' && rows.length === 1 && D - hd >= 4 ? 2 : 0;
  const out: Building[] = [];
  const n = Math.max(1, Math.floor((L + style.gap) / (hw + style.gap)));
  const spare = L - (n * hw + (n - 1) * style.gap);
  for (const side of rows) {
    let u = Math.floor(spare / 2) + (spare > 1 ? rng.range(-1, 1) : 0);
    u = clamp(u, 0, spare);
    // A long row keeps a garden in one of its middle plots now and then, and a house row may end in a corner shop.
    const garden = n >= 3 && rng.next() < 0.3 ? rng.range(1, n - 2) : -1;
    const shop = plan === 'rooms' && rng.next() < 0.3 ? rng.pick([0, n - 1]) : -1;
    for (let k = 0; k < n; k++) {
      const b = local(lot, side, u, setback, hw, hd);
      if (k === garden) groupNear(c, centre(b), rng, [dressed(CORNER, 'bush', 'tree'), dressed(PAIR, 'bush', 'tree')], 1, 0, 6);
      else if (k === shop) out.push(building(c, local(lot, side, u, 0, hw, hd), rng, 'shop', side, style, 'shop'));
      else {
        out.push(building(c, b, rng, plan, side, style, furnish));
        if (setback) frontGarden(c, lot, side, u, hw, rng);
      }
      u += hw + style.gap;
    }
  }
  // Back yards: a fence down the middle between two rows (posts beside each gap), a tree or bins behind a single one.
  const yard = local(lot, front, 0, hd, L, D - hd * rows.length);
  if (rows.length === 2 && depthOf(yard, front) >= 3) {
    const v = Math.floor(depthOf(yard, front) / 2);
    for (let u = 0; u < L; u++) if (u % 6 !== 3) put(c, localPt(yard, front, u, v), 'low', [u % 6 === 2 || u % 6 === 4 ? 'gatePost' : 'fence', front]);
  } else if (depthOf(yard, front) >= 3) {
    for (let u = 2; u < L - 2; u += hw + style.gap) groupNear(c, localPt(yard, front, u + 2, 1), rng, [dressed(PAIR, 'bin', 'tree'), dressed(CORNER, 'bin', 'tree')], 1, 0, 4);
  }
  return out;
}

/**
 * A house's front garden (its plot `u`…`u + width` of the lot's side `side`,
 * two tiles deep): a fence along the pavement with a gate in front of the
 * door, and a bush or a tree by the fence.
 */
function frontGarden(c: Canvas, lot: Rect, side: number, u: number, width: number, rng: Rng): void {
  let door = -1;
  for (let k = 0; k < width; k++) {
    const p = localPt(lot, side, u + k, 2);
    if (c.get(p.x, p.y) === 'door') door = k;
  }
  if (door < 0) return;
  for (let k = 0; k < width; k++) {
    if (Math.abs(k - door) === 0) continue;
    put(c, localPt(lot, side, u + k, 0), 'low', [Math.abs(k - door) === 1 ? 'gatePost' : 'fence', side]);
  }
  // The path to the door stays clear; a bush in a corner of the garden.
  c.reserve(localPt(lot, side, u + door, 1).x, localPt(lot, side, u + door, 1).y);
  const corner = door > width / 2 ? 1 : width - 2;
  if (rng.next() < 0.7) put(c, localPt(lot, side, u + corner, 1), 'low', ['bush']);
}

/**
 * A high street: rows of narrow shops wall to wall along the front of the lot,
 * two or three to a row (a row is at most 16 tiles), an alley between rows.
 * Each shop is its own building with its own door on the street; the walls
 * they share have no openings.
 */
function highStreet(c: Canvas, lot: Rect, front: number, rng: Rng, style: Style): Building[] {
  const L = sideLen(lot, front);
  const D = depthOf(lot, front);
  if (L < 7 || D < 7) return [];
  // Deep enough to fill the lot (a shop floor and a stockroom), or two rows back to back onto the street or yard behind.
  const two = D >= 2 * 7 + 2;
  const d = two ? clamp(Math.floor((D - 1) / 2), 7, 10) : clamp(D - rng.range(0, 2), 7, 10);
  const out: Building[] = [];
  const rows = two ? [front, opposite(front)] : [front];
  for (const side of rows) out.push(...shopRow(c, lot, side, L, d, rng, style));
  return out;
}

/** One row of shops wall to wall along side `side` of the lot. */
function shopRow(c: Canvas, lot: Rect, front: number, L: number, d: number, rng: Rng, style: Style): Building[] {
  const D = depthOf(lot, front);
  const out: Building[] = [];
  let u = rng.int(2);
  while (u + 7 <= L) {
    const room = Math.min(16, L - u);
    // Shops 6 or 7 tiles wide (a counter fits), sharing a wall: a row of n is 1 + Σ(w − 1) long.
    const widths: number[] = [];
    let total = 1;
    for (let k = 0; k < 3; k++) {
      const w = rng.range(6, 7);
      if (total + w - 1 > room) break;
      widths.push(w);
      total += w - 1;
    }
    if (!widths.length) break;
    if (total < 7) widths[0] = 7;
    const shops: Rect[] = [];
    let x = u;
    for (const w of widths) {
      const b = local(lot, front, x, 0, w, d);
      out.push(building(c, b, rng, 'shop', front, style, 'shop'));
      shops.push(b);
      x += w - 1;
    }
    // Party walls: a window or a door between two shops is walled up again.
    for (let k = 1; k < shops.length; k++) {
      const a = shops[k - 1]!;
      const b = shops[k]!;
      const x0 = Math.max(a.x, b.x);
      const y0 = Math.max(a.y, b.y);
      const x1 = Math.min(a.x + a.w, b.x + b.w);
      const y1 = Math.min(a.y + a.h, b.y + b.h);
      for (let y = y0; y < y1; y++) for (let x2 = x0; x2 < x1; x2++) if (c.get(x2, y) !== 'wall') c.force(x2, y, 'wall');
    }
    // Bins and crates out the back.
    if (D - d >= 3) groupNear(c, localPt(lot, front, u + Math.floor((x - u) / 2), d + 1), rng, [dressed(PAIR, 'bin', 'crates'), dressed(BARRICADE, 'bin')], 2, 0, 4);
    u = x + 1 + rng.range(1, 2);
  }
  return out;
}

/**
 * A filling station: the shop at the back of the lot to one side, two pump
 * islands on the forecourt (pumps: `tank` hints, the client draws them as
 * pumps under a canopy), a car or two waiting at them.
 */
function gasStation(c: Canvas, lot: Rect, front: number, rng: Rng, style: Style): Building[] {
  const L = sideLen(lot, front);
  const D = depthOf(lot, front);
  if (L < 13 || D < 10) return [];
  const left = rng.int(2) === 0;
  const b = building(c, local(lot, front, left ? 0 : L - 7, D - 7, 7, 7), rng, 'shop', front, style, 'shop');
  // Islands across the forecourt, two tiles long, a lane either side.
  const first = left ? 9 : 2;
  for (const du of [0, 3]) {
    const u = first + du;
    if (u + 1 >= L) continue;
    for (const v of [2, 3]) put(c, localPt(lot, front, u, v), 'low', ['tank', (front + 1) % 4]);
    // A car at the pump now and then, in the lane beside it.
    if (rng.next() < 0.5) {
      const a = localPt(lot, front, u + 1, 2);
      const z = localPt(lot, front, u + 1, 3);
      if (c.free(a.x, a.y) && c.free(z.x, z.y)) {
        c.force(a.x, a.y, 'low');
        c.force(z.x, z.y, 'low');
        c.dress(a, 'carFront', front);
        c.dress(z, 'carRear', front);
      }
    }
  }
  return [b];
}

/**
 * A motel: one or two wings of three rooms in a row at the back of the lot,
 * every room with its own door and window onto the car park in front.
 */
function motel(c: Canvas, lot: Rect, front: number, rng: Rng): Building[] {
  const L = sideLen(lot, front);
  const D = depthOf(lot, front);
  if (L < 13 || D < 11) return [];
  const out: Building[] = [];
  const wings = L >= 28 ? 2 : 1;
  const gap = wings === 2 ? Math.min(L - 26, 4) : 0;
  let u = Math.floor((L - (13 * wings + gap)) / 2);
  for (let k = 0; k < wings; k++) {
    out.push(motelWing(c, local(lot, front, u, D - 7, 13, 7), front, rng));
    u += 13 + gap;
  }
  parking(c, local(lot, front, 0, 0, L, D - 8), rng, front);
  return out;
}

/** Three motel rooms side by side (13 × 7, walls included), each opening on side `front`. */
function motelWing(c: Canvas, b: Rect, front: number, rng: Rng): Building {
  for (let y = b.y; y < b.y + b.h; y++) {
    for (let x = b.x; x < b.x + b.w; x++) {
      const edge = x === b.x || y === b.y || x === b.x + b.w - 1 || y === b.y + b.h - 1;
      c.force(x, y, edge ? 'wall' : 'floor', 0);
      c.ground[c.i(x, y)] = INDOOR;
    }
  }
  const depth = depthOf(b, front);
  const rooms: Rect[] = [];
  for (let k = 0; k < 3; k++) {
    // The wall to the next room: no door, each room is let on its own.
    if (k > 0) for (let v = 1; v < depth - 1; v++) c.force(localPt(b, front, 4 * k, v).x, localPt(b, front, 4 * k, v).y, 'wall');
    const room = local(b, front, 1 + 4 * k, 1, 3, depth - 2);
    rooms.push(room);
    opening(c, localPt(b, front, 1 + 4 * k, 0), front, 'door');
    c.force(localPt(b, front, 3 + 4 * k, 0).x, localPt(b, front, 3 + 4 * k, 0).y, 'window');
    furnishRoom(c, room, rng, 'home');
  }
  return { outer: b, rooms };
}

/**
 * A building site: barriers along the street with a gate, stacks of bricks,
 * pallets and cement inside, a scaffold (a one-storey platform with a ladder)
 * and the workmen's portaloo.
 */
function site(c: Canvas, lot: Rect, front: number, rng: Rng): void {
  const L = sideLen(lot, front);
  const D = depthOf(lot, front);
  if (L < 8 || D < 8) return;
  const gate = rng.range(2, L - 4);
  for (let u = 0; u < L; u++) if (u < gate || u > gate + 1) put(c, localPt(lot, front, u, 0), 'low', ['barricade', front]);
  // The scaffold against the back of the lot, a ladder at one end.
  const box = local(lot, front, rng.range(1, Math.max(1, L - 6)), D - 3, 5, 2);
  const fits = tilesIn(rect(box.x - 1, box.y - 1, box.w + 2, box.h + 2)).every((p) => c.free(p.x, p.y) || !c.in(p.x, p.y));
  if (fits) platform(c, box, rng, 1);
  for (let k = 0; k < 3; k++) groupNear(c, localPt(lot, front, rng.range(2, L - 3), rng.range(2, D - 5)), rng, [dressed(PAIR, 'crates'), dressed(CRATES, 'crates')], 1, 0, 6);
  // The portaloo (the client draws a town's `pylon` as one).
  groupNear(c, localPt(lot, front, L - 2, 2), rng, [[[0, 0, 'high', ['pylon']]]], 1, 0, 6);
}

/**
 * A checkpoint across one street: barriers from kerb to kerb with a gap for
 * one car, tall blocks at the ends.
 */
function checkpoint(c: Canvas, roads: Road[], rng: Rng): void {
  const options = roads.filter((r) => (r.vertical ? r.r.w : r.r.h) >= 3 && (r.vertical ? r.r.h : r.r.w) >= 10);
  if (!options.length || rng.next() < 0.4) return;
  const road = rng.pick(options);
  const width = road.vertical ? road.r.w : road.r.h;
  const length = road.vertical ? road.r.h : road.r.w;
  const t = Math.floor(length / 2) + rng.range(-2, 2);
  const gap = rng.range(1, width - 2);
  const facing = road.vertical ? 0 : 3;
  for (let lane = 0; lane < width; lane++) {
    if (lane === gap) continue;
    const p = road.vertical ? { x: road.r.x + lane, y: road.r.y + t } : { x: road.r.x + t, y: road.r.y + lane };
    if (!c.free(p.x, p.y)) continue;
    const end = lane === 0 || lane === width - 1;
    c.force(p.x, p.y, end ? 'high' : 'low');
    c.dress(p, end ? 'crates' : 'barricade', facing);
  }
}

interface Lot {
  r: Rect;
  /** Sides that look onto a road. */
  roads: boolean[];
}

/**
 * The buildable part of a block: one tile less on every side but the map's
 * edge, a sidewalk along streets and half an alley against a block behind.
 */
function lotOf(c: Canvas, block: Rect): Rect {
  let lot = block;
  for (let s = 0; s < 4; s++) {
    const q = add(localPt(block, s, 0, 0), DIRS4[s]!);
    if (c.in(q.x, q.y)) lot = cut(lot, s, 1);
  }
  return lot;
}

/** A long lot is often split in two, back to back with an alley between them; each half is at least `min` long. */
function splitLot(lot: Rect, roads: boolean[], rng: Rng, min: number, chance: number): Lot[] {
  const alongX = lot.w >= lot.h;
  const span = alongX ? lot.w : lot.h;
  const gap = rng.range(1, 2);
  if (span < 2 * min + gap || Math.min(lot.w, lot.h) < 7 || rng.next() >= chance) return [{ r: lot, roads }];
  const k = clamp(Math.round((span - gap) / 2 + rng.range(-2, 2)), min, span - min - gap);
  if (alongX) {
    return [
      { r: rect(lot.x, lot.y, k, lot.h), roads: [roads[0]!, false, roads[2]!, roads[3]!] },
      { r: rect(lot.x + k + gap, lot.y, lot.w - k - gap, lot.h), roads: [roads[0]!, roads[1]!, roads[2]!, false] },
    ];
  }
  return [
    { r: rect(lot.x, lot.y, lot.w, k), roads: [roads[0]!, roads[1]!, false, roads[3]!] },
    { r: rect(lot.x, lot.y + k + gap, lot.w, lot.h - k - gap), roads: [false, roads[1]!, roads[2]!, roads[3]!] },
  ];
}

// -------------------------------------------------------------------- city

type Role = 'indoor' | 'open';
type CityProgram = 'houses' | 'shops' | 'highStreet' | 'office' | 'flats' | 'warehouse' | 'plaza' | 'parking' | 'gasStation' | 'motel' | 'site';

function weighted<T>(rng: Rng, options: [T, number][]): T {
  const total = options.reduce((s, [, w]) => s + w, 0);
  let roll = rng.next() * total;
  for (const [item, w] of options) {
    roll -= w;
    if (roll < 0) return item;
  }
  return options[options.length - 1]![0];
}

/** `used` counts the open lots already placed: besides the squad's square and the objective, one square and two car parks at most. */
function cityProgram(lot: Rect, rng: Rng, role: Role | null, used: Map<CityProgram, number>): CityProgram {
  const short = Math.min(lot.w, lot.h);
  const long = Math.max(lot.w, lot.h);
  if (role === 'indoor') return short >= 10 && rng.next() < 0.6 ? 'office' : rng.next() < 0.5 ? 'shops' : 'houses';
  if (role === 'open') return rng.next() < 0.55 ? 'plaza' : 'parking';
  // Open lots stay small and few: a big empty block reads as a hole in the city.
  const area = lot.w * lot.h;
  const options: [CityProgram, number][] = [];
  if (!used.get('plaza') && area <= 180) options.push(['plaza', 0.5]);
  if (!used.get('parking') && area <= 220) options.push(['parking', 0.5]);
  // Smaller buildings, more of them (Synty's captures: rows of shops, a filling station, a motel, houses with gardens).
  if (short >= 7) options.push(['houses', 2.5], ['shops', 1], ['highStreet', 2.5]);
  if (short >= 10) options.push(['office', 2]);
  if (short >= 9 && long >= 10) options.push(['flats', 2]);
  if (short >= 12) options.push(['warehouse', 1.2]);
  if (!used.get('gasStation') && short >= 10 && long >= 13) options.push(['gasStation', 0.25]);
  if (!used.get('motel') && short >= 11 && long >= 13) options.push(['motel', 0.9]);
  if (!used.get('site') && short >= 8 && area <= 260) options.push(['site', 0.5]);
  if (!options.length) options.push(['parking', 1]);
  const pick = weighted(rng, options);
  used.set(pick, (used.get(pick) ?? 0) + 1);
  return pick;
}

/** Fills one city lot. Returns the walled buildings (the objective may go in one). */
function cityLot(c: Canvas, lot: Rect, program: CityProgram, roads: boolean[], rng: Rng, style: Style): Building[] {
  const front = frontOf(c, lot, roads, rng);
  const L = sideLen(lot, front);
  const D = depthOf(lot, front);
  switch (program) {
    case 'office': {
      const w = Math.min(style.office, L);
      const d = Math.min(style.officeDepth, D);
      if (w < 10 || d < 10) return cityLot(c, lot, 'houses', roads, rng, style);
      const b = building(c, local(lot, front, Math.floor((L - w) / 2), 0, w, d), rng, rng.next() < 0.65 ? 'corridor' : 'open', front, style, 'office');
      // Bins and crates out the back, planters and trees on the sides.
      if (D - d >= 6) parking(c, local(lot, front, 0, d + 1, L, D - d - 1), rng, opposite(front));
      else if (D - d >= 3) groupNear(c, localPt(lot, front, Math.floor(L / 2), d + 1), rng, [dressed(BARRICADE, 'bin'), dressed(PAIR, 'bin', 'crates')], 2);
      for (const u of [1, L - 2]) if (Math.floor((L - w) / 2) >= 3) groupNear(c, localPt(lot, front, u, Math.floor(d / 2)), rng, [dressed(PAIR, 'planter', 'tree'), dressed(CORNER, 'planter', 'tree')], 1);
      return [b];
    }
    case 'warehouse': {
      const w = Math.min(style.hall, L);
      const d = Math.min(style.hall - 1, D);
      if (w < 12 || d < 12) return cityLot(c, lot, 'office', roads, rng, style);
      // The hall sits at the back, its loading bay onto a yard of containers and crates.
      const yard = D - d;
      const b = building(c, local(lot, front, Math.floor((L - w) / 2), yard, w, d), rng, 'hall', front, style, 'store');
      if (yard >= 5) {
        const box = local(lot, front, 1, 1, 2, 5);
        const fits = tilesIn(rect(box.x - 1, box.y - 1, box.w + 2, box.h + 2)).every((p) => c.free(p.x, p.y) || !c.in(p.x, p.y));
        if (yard >= 7 && fits && rng.next() < 0.6) platform(c, box, rng, 1);
        groupNear(c, localPt(lot, front, L - 4, 2), rng, [dressed(CRATES, 'crates')], 1);
      } else if (yard >= 3) {
        groupNear(c, localPt(lot, front, 2, 1), rng, [dressed(CRATES, 'crates'), dressed(BARRICADE, 'crates')], 1);
      }
      return [b];
    }
    case 'flats': {
      const w = clamp(L - 2, 8, 12);
      const d = clamp(D - 3, 7, 10);
      if (w > L || d > D) return cityLot(c, lot, 'houses', roads, rng, style);
      const u0 = Math.floor((L - w) / 2);
      const b = local(lot, front, u0, 0, w, d);
      // The terrace looks onto the courtyard behind, or onto a side passage.
      const back = opposite(front);
      const terrace = D - d >= 2 ? back : u0 >= 2 ? (front + 1) % 4 : front;
      flats(c, b, rng, terrace);
      // A hedge and a tree in the courtyard.
      if (D - d >= 4) groupNear(c, localPt(lot, front, Math.floor(L / 2) + rng.range(-3, 3), D - 2), rng, [dressed(BARRICADE, 'bush'), dressed(PAIR, 'bush', 'tree')], 1);
      return [];
    }
    case 'plaza':
      plaza(c, lot, rng);
      return [];
    case 'parking':
      parking(c, lot, rng, front);
      return [];
    case 'shops':
      return rowOfBuildings(c, lot, front, roads, rng, style, 'shop', 'shop');
    case 'highStreet': {
      const row = highStreet(c, lot, front, rng, style);
      return row.length ? row : cityLot(c, lot, 'houses', roads, rng, style);
    }
    case 'gasStation': {
      const station = gasStation(c, lot, front, rng, style);
      return station.length ? station : cityLot(c, lot, 'parking', roads, rng, style);
    }
    case 'motel': {
      const rooms = motel(c, lot, front, rng);
      return rooms.length ? rooms : cityLot(c, lot, 'houses', roads, rng, style);
    }
    case 'site':
      site(c, lot, front, rng);
      return [];
    case 'houses': {
      // Too small for the map's houses: a row of narrow shops, and only then a square.
      const row = rowOfBuildings(c, lot, front, roads, rng, style, 'rooms', 'home');
      if (row.length) return row;
      const shops = highStreet(c, lot, front, rng, style);
      if (!shops.length) plaza(c, lot, rng);
      return shops;
    }
  }
}

/** City: blocks of houses, shops, offices, flats and warehouses between streets, a square or a car park here and there. */
function city(c: Canvas, rng: Rng, style: Style, role: Role): Rect {
  const S = c.width;
  const plan = district(rect(0, 0, S, S), rng, { minBlock: 10, maxBlock: 24, widths: [[4, 4], [3, 3], [2, 3]], jog: 0.35, deadEnd: 0.25, stop: [0, 0.3, 0.7, 0.9] });
  for (const road of plan.roads) c.paint(road.r, ROAD);
  const far = { x: S * 0.76, y: S * 0.76 };
  const spawnBlock = plan.blocks.find((b) => inRect(b, { x: 0, y: 0 }))!;
  const objBlock =
    best(
      plan.blocks.filter((b) => b !== spawnBlock && Math.min(b.w, b.h) >= (role === 'indoor' ? 12 : 9)),
      (b) => dist(centre(b), far),
    ) ?? best(plan.blocks, (b) => (b === spawnBlock ? Infinity : dist(centre(b), far)))!;
  let objective: Rect = objBlock;
  const used = new Map<CityProgram, number>();
  for (const block of plan.blocks) {
    const roads = [0, 1, 2, 3].map((s) => facesRoad(c, block, s));
    const lot = lotOf(c, block);
    const lots: Lot[] = [];
    if (block === spawnBlock) {
      // The squad's corner: an open square, with buildings beyond it if the block is big.
      const yard = 10;
      if (lot.w >= yard + 9) {
        plaza(c, rect(lot.x, lot.y, yard, lot.h), rng);
        lots.push({ r: rect(lot.x + yard + 1, lot.y, lot.w - yard - 1, lot.h), roads: [roads[0]!, roads[1]!, roads[2]!, false] });
      } else if (lot.h >= yard + 9) {
        plaza(c, rect(lot.x, lot.y, lot.w, yard), rng);
        lots.push({ r: rect(lot.x, lot.y + yard + 1, lot.w, lot.h - yard - 1), roads: [false, roads[1]!, roads[2]!, roads[3]!] });
      } else {
        plaza(c, lot, rng);
      }
    } else {
      lots.push(...splitLot(lot, roads, rng, 8, block === objBlock && role === 'indoor' ? 0 : 0.65));
    }
    const objLot = block === objBlock ? best(lots, (l) => dist(centre(l.r), far)) : undefined;
    for (const l of lots) {
      const isObj = l === objLot;
      const built = cityLot(c, l.r, cityProgram(l.r, rng, isObj ? role : null, used), l.roads, rng, style);
      if (isObj) objective = best(built, (b) => dist(centre(b.outer), far))?.outer ?? l.r;
    }
    sidewalk(c, block, roads, style.furniture, 'low');
  }
  parkCars(c, plan.roads, rng, 0.4);
  checkpoint(c, plan.roads, rng);
  return objective;
}

// ---------------------------------------------------------------- facility

type FacilityProgram = 'lab' | 'hangar' | 'platforms' | 'storage' | 'reactor' | 'tanks';

/** `used` counts the programs already placed: one reactor and two storage yards at most, the rest buildings. */
function facilityProgram(lot: Rect, rng: Rng, role: Role | null, used: Map<FacilityProgram, number>): FacilityProgram {
  const short = Math.min(lot.w, lot.h);
  const count = (p: FacilityProgram) => used.get(p) ?? 0;
  let pick: FacilityProgram;
  if (role === 'indoor') pick = 'lab';
  else if (role === 'open') pick = count('reactor') === 0 && rng.next() < 0.5 ? 'reactor' : 'storage';
  else {
    const options: [FacilityProgram, number][] = [['tanks', 0.5]];
    if (count('storage') < 2) options.push(['storage', 0.9]);
    if (count('reactor') === 0) options.push(['reactor', 0.6]);
    if (short >= 10) options.push(['lab', 3]);
    if (short >= 12) options.push(['hangar', 2.5]);
    if (short >= 8) options.push(['platforms', 1]);
    pick = weighted(rng, options);
  }
  used.set(pick, count(pick) + 1);
  return pick;
}

/** Rows of stacked crates, tanks and crates with two-tile aisles, broken every few tiles. */
function storageRows(c: Canvas, r: Rect, front: number, rng: Rng): void {
  const across = sideLen(r, front);
  const depth = depthOf(r, front);
  for (let v = 1; v < depth - 1; v += 3) {
    const kind: TileKind = (v - 1) % 6 === 0 ? 'high' : 'low';
    const what: DecorKind = (v - 1) % 12 === 6 ? 'tank' : 'crates';
    const seg = rng.range(3, 4);
    for (let u = 1; u < across - 1; u++) if ((u - 1) % (seg + 2) < seg) put(c, localPt(r, front, u, v), kind, [what, front]);
  }
}

function facilityLot(c: Canvas, lot: Rect, program: FacilityProgram, roads: boolean[], rng: Rng, style: Style): Building[] {
  const front = frontOf(c, lot, roads, rng);
  const L = sideLen(lot, front);
  const D = depthOf(lot, front);
  switch (program) {
    case 'lab': {
      const w = Math.min(style.office + 1, L);
      const d = Math.min(style.officeDepth + 1, D);
      // A lot too small for a lab with a corridor still takes a small one of a couple of rooms.
      if (w < 7 || d < 7) return facilityLot(c, lot, 'storage', roads, rng, style);
      const plan: Plan = w >= 10 && d >= 10 ? 'corridor' : 'rooms';
      const b = building(c, local(lot, front, Math.floor((L - w) / 2), 0, w, d), rng, plan, front, style, 'lab');
      if (D - d >= 4) groupNear(c, localPt(lot, front, Math.floor(L / 2), d + 2), rng, [dressed(CRATES, 'crates'), dressed(CORNER, 'crates')], 2);
      return [b];
    }
    case 'hangar': {
      const w = Math.min(style.hall, L);
      const d = Math.min(style.hall, D);
      if (w < 12 || d < 12) return facilityLot(c, lot, 'lab', roads, rng, style);
      return [building(c, local(lot, front, Math.floor((L - w) / 2), 0, w, d), rng, 'hall', front, style, 'store')];
    }
    case 'platforms': {
      // Loading platforms one storey high, side by side across the lot, with crates on top and around.
      const len = Math.min(10, L - 2);
      const v0 = 2;
      for (let v = v0; v + 4 <= D - 1; v += 7) {
        const deck = local(lot, front, Math.floor((L - len) / 2), v, len, 4);
        platform(c, deck, rng, 2);
        groupNear(c, centre(deck), rng, [dressed(CRATES, 'crates'), dressed(PAIR, 'crates')], 1, 1, 6);
      }
      return [];
    }
    case 'storage':
      storageRows(c, lot, front, rng);
      return [];
    case 'reactor': {
      // A sealed core, pylons at its corners and conduits running out of it.
      const m = centre(lot);
      const core = rect(Math.floor(m.x) - 1, Math.floor(m.y) - 1, 3, 3);
      for (const p of tilesIn(core)) c.force(p.x, p.y, 'wall');
      for (const [dx, dy] of [[-2, -2], [4, -2], [-2, 4], [4, 4]] as const) put(c, { x: core.x + dx, y: core.y + dy }, 'high', ['pylon']);
      for (const s of shuffle([0, 1, 2, 3], rng).slice(0, 2)) {
        for (let k = 3; k < 3 + rng.range(3, 5); k++) put(c, add({ x: core.x + 1, y: core.y + 1 }, { x: DIRS4[s]!.x * k, y: DIRS4[s]!.y * k }), 'low', ['pipe', (s + 1) % 4]);
      }
      return [];
    }
    case 'tanks':
      for (let v = 1; v + 2 <= D - 1; v += 4) for (let u = 1; u + 2 <= L - 1; u += 4) group(c, localPt(lot, front, u, v), turn(dressed(CRATES, 'crates', 'tank'), rng.int(4)));
      return [];
  }
}

/** Pipes along a yard: a low run on one edge, broken every few tiles so it can be crossed. */
function conduits(c: Canvas, roads: Road[], rng: Rng): void {
  for (const road of roads) {
    const width = road.vertical ? road.r.w : road.r.h;
    const length = road.vertical ? road.r.h : road.r.w;
    if (width < 3 || length < 10 || rng.next() < 0.45) continue;
    const lane = rng.int(2) === 0 ? 0 : width - 1;
    const curb = lane === 0 ? -1 : width;
    const at = (l: number, t: number): Vec2 => (road.vertical ? { x: road.r.x + l, y: road.r.y + t } : { x: road.r.x + t, y: road.r.y + l });
    const seg = rng.range(4, 6);
    for (let t = 2; t < length - 2; t++) {
      const p = at(lane, t);
      const kerb = at(curb, t);
      if ((t - 2) % (seg + 2) >= seg || c.groundAt(kerb.x, kerb.y) === ROAD) continue;
      put(c, p, 'low', ['pipe', road.vertical ? 1 : 0]);
    }
  }
}

/** Rows and columns from here on lie inside a facility's fence, whatever the width of the apron (squad outside). */
const FACILITY_INSIDE = 7;

/**
 * Facility: a walled compound with gates. Half the time the squad starts
 * outside it, on the access apron by its corner; the other half it lands in a
 * yard inside, and the fence and its apron run along the far sides. Inside,
 * labs, hangars, platforms and storage yards between service roads, with
 * conduits along them; the evacuation waits by the far side.
 */
function facility(c: Canvas, rng: Rng, style: Style, role: Role): { objective: Rect; inside: boolean } {
  const S = c.width;
  const p = rng.range(FACILITY_INSIDE - 2, FACILITY_INSIDE - 1);
  const inside = rng.next() < 0.5;
  // The fence: two walls meeting at a corner, near the squad (outside) or far from it (inside).
  const line = inside ? S - 1 - p : p;
  const along = inside ? [0, line + 1] : [line, S];
  const apron = inside ? rect(line + 1, 0, S - line - 1, S) : rect(0, 0, p, S);
  c.paint(apron, ROAD);
  c.paint(inside ? rect(0, line + 1, S, S - line - 1) : rect(0, 0, S, p), ROAD);
  for (let k = along[0]!; k < along[1]!; k++) {
    c.force(k, line, 'wall');
    c.force(line, k, 'wall');
  }
  // Gates: one on each wall where the squad comes from, another one further along on one of them.
  const near = inside ? [line - 16, line - 9] : [p + 5, p + 11];
  const gates: [number, number][] = [
    [0, rng.range(near[0]!, near[1]!)],
    [3, rng.range(near[0]!, near[1]!)],
    [rng.pick([0, 3]), inside ? rng.range(6, 14) : rng.range(S - 16, S - 9)],
  ];
  const out = inside ? 1 : -1;
  for (const [side, at] of gates) {
    for (let k = 0; k < 3; k++) {
      const q = side === 0 ? { x: at + k, y: line } : { x: line, y: at + k };
      c.force(q.x, q.y, 'floor');
      c.paint(rect(q.x, q.y, 1, 1), ROAD);
      c.reserve(q.x, q.y);
      c.reserve(q.x + (side === 3 ? 1 : 0), q.y + (side === 0 ? 1 : 0));
      c.reserve(q.x - (side === 3 ? 1 : 0), q.y - (side === 0 ? 1 : 0));
    }
    // A guard post outside the gate: a booth behind a barrier.
    const post = side === 0 ? { x: at + rng.pick([-3, 4]), y: line + 2 * out } : { x: line + 2 * out, y: at + rng.pick([-3, 4]) };
    groupNear(c, post, rng, [dressed(PAIR, 'barricade', 'kiosk'), dressed(CORNER, 'barricade', 'kiosk')], 1);
  }
  // Trucks along the apron.
  const aprons: Road[] = inside
    ? [
        { r: rect(0, line + 1, line, S - line - 1), vertical: false },
        { r: rect(line + 1, 0, S - line - 1, line), vertical: true },
      ]
    : [
        { r: rect(p + 1, 0, S - p - 1, p), vertical: false },
        { r: rect(0, p + 1, p, S - p - 1), vertical: true },
      ];
  // The inner ring road runs along the wall.
  const ring = inside ? line - 1 : p + 1;
  const lo = inside ? 0 : p + 1;
  const span = inside ? line : S - p - 1;
  c.paint(rect(lo, ring, span, 1), ROAD);
  c.paint(rect(ring, lo, 1, span), ROAD);
  const area = inside ? rect(0, 0, line - 1, line - 1) : rect(p + 2, p + 2, S - p - 2, S - p - 2);
  const plan = district(area, rng, { minBlock: 10, maxBlock: 20, widths: [[4, 4], [3, 4], [3, 3]], jog: 0.2, deadEnd: 0.12, stop: [0, 0.3, 0.7] });
  for (const road of plan.roads) c.paint(road.r, ROAD);
  const far = { x: S * 0.76, y: S * 0.76 };
  // Landing inside, the squad's corner is an open yard.
  const spawnBlock = inside ? plan.blocks.find((b) => inRect(b, { x: 0, y: 0 })) : undefined;
  const objBlock =
    best(
      plan.blocks.filter((b) => b !== spawnBlock && Math.min(b.w, b.h) >= (role === 'indoor' ? 12 : 8)),
      (b) => dist(centre(b), far),
    ) ?? best(plan.blocks, (b) => (b === spawnBlock ? Infinity : dist(centre(b), far)))!;
  let objective: Rect = objBlock;
  const used = new Map<FacilityProgram, number>();
  for (const block of plan.blocks) {
    const roads = [0, 1, 2, 3].map((s) => facesRoad(c, block, s));
    let lots = splitLot(lotOf(c, block), roads, rng, 10, block === objBlock && role === 'indoor' ? 0 : 0.6);
    if (block === spawnBlock) {
      const yard = 10;
      const lot = lotOf(c, block);
      lots = lot.w >= yard + 9 ? [{ r: rect(lot.x + yard + 1, lot.y, lot.w - yard - 1, lot.h), roads: [roads[0]!, roads[1]!, roads[2]!, false] }] : lot.h >= yard + 9 ? [{ r: rect(lot.x, lot.y + yard + 1, lot.w, lot.h - yard - 1), roads: [false, roads[1]!, roads[2]!, roads[3]!] }] : [];
    }
    const objLot = block === objBlock ? best(lots, (l) => dist(centre(l.r), far)) : undefined;
    for (const l of lots) {
      const isObj = l === objLot;
      const built = facilityLot(c, l.r, facilityProgram(l.r, rng, isObj ? role : null, used), l.roads, rng, style);
      if (isObj) objective = best(built, (b) => dist(centre(b.outer), far))?.outer ?? l.r;
    }
  }
  conduits(c, plan.roads, rng);
  parkCars(c, aprons, rng, 0.25);
  return { objective, inside };
}

// ------------------------------------------------------------------- wilds

/** Smooth value noise in [0, 1]; `cell` is the feature size in tiles. */
function valueNoise(size: number, cell: number, rng: Rng): number[] {
  const g = Math.ceil(size / cell) + 2;
  const lattice = Array.from({ length: g * g }, () => rng.next());
  const ox = rng.next() * cell;
  const oy = rng.next() * cell;
  const smooth = (t: number) => t * t * (3 - 2 * t);
  const out = new Array<number>(size * size);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const fx = (x + ox) / cell;
      const fy = (y + oy) / cell;
      const ix = Math.floor(fx);
      const iy = Math.floor(fy);
      const tx = smooth(fx - ix);
      const ty = smooth(fy - iy);
      const v = (i: number, j: number) => lattice[(iy + j) * g + ix + i]!;
      const top = v(0, 0) + (v(1, 0) - v(0, 0)) * tx;
      const bottom = v(0, 1) + (v(1, 1) - v(0, 1)) * tx;
      out[y * size + x] = top + (bottom - top) * ty;
    }
  }
  return out;
}

/** Centre line of a dirt track from the squad's corner to the far side, bending twice on the way. */
function trackLine(S: number, rng: Rng): Vec2[] {
  const start = rng.int(2) === 0 ? { x: rng.range(5, 9), y: 0 } : { x: 0, y: rng.range(5, 9) };
  const end = rng.int(2) === 0 ? { x: rng.range(Math.round(S * 0.55), S - 7), y: S - 1 } : { x: S - 1, y: rng.range(Math.round(S * 0.55), S - 7) };
  const len = dist(start, end);
  const nx = -(end.y - start.y) / len;
  const ny = (end.x - start.x) / len;
  const sign = rng.int(2) === 0 ? 1 : -1;
  const pts = [start];
  [0.3, 0.65].forEach((t, k) => {
    const off = (k === 0 ? sign : -sign) * rng.range(3, 7);
    pts.push({ x: start.x + (end.x - start.x) * t + nx * off, y: start.y + (end.y - start.y) * t + ny * off });
  });
  pts.push(end);
  const out: Vec2[] = [];
  for (let k = 0; k < pts.length - 1; k++) {
    const p0 = pts[Math.max(0, k - 1)]!;
    const p1 = pts[k]!;
    const p2 = pts[k + 1]!;
    const p3 = pts[Math.min(pts.length - 1, k + 2)]!;
    for (let t = 0; t < 1; t += 0.05) {
      const t2 = t * t;
      const t3 = t2 * t;
      const f = (a: number, b: number, cc: number, d: number) => 0.5 * (2 * b + (-a + cc) * t + (2 * a - 5 * b + 4 * cc - d) * t2 + (-a + 3 * b - 3 * cc + d) * t3);
      const q = { x: clamp(Math.round(f(p0.x, p1.x, p2.x, p3.x)), 0, S - 1), y: clamp(Math.round(f(p0.y, p1.y, p2.y, p3.y)), 0, S - 1) };
      const last = out[out.length - 1];
      if (!last || last.x !== q.x || last.y !== q.y) out.push(q);
    }
  }
  out.push(end);
  return out;
}

/** Open, untouched ground over `r` and a margin around it. */
const roomFor = (c: Canvas, r: Rect, margin: number) => c.vacant(rect(r.x - margin, r.y - margin, r.w + 2 * margin, r.h + 2 * margin));

/** Distance (in steps) from every tile to the nearest tile of `from`. */
function distanceField(c: Canvas, from: Iterable<number>): number[] {
  const out = new Array<number>(c.tiles.length).fill(Infinity);
  const queue: number[] = [];
  for (const i of from) {
    out[i] = 0;
    queue.push(i);
  }
  for (let k = 0; k < queue.length; k++) {
    const i = queue[k]!;
    const x = i % c.width;
    const y = (i - x) / c.width;
    for (const d of DIRS4) {
      if (!c.in(x + d.x, y + d.y)) continue;
      const j = c.i(x + d.x, y + d.y);
      if (out[j]! <= out[i]! + 1) continue;
      out[j] = out[i]! + 1;
      queue.push(j);
    }
  }
  return out;
}

/**
 * The best vacant spot within `radius` of `target` for a building `lu` wide
 * (along its front) and `ld` deep whose front door looks onto the track a few
 * tiles away.
 */
function siteNear(c: Canvas, target: Vec2, lu: number, ld: number, toTrack: number[], rng: Rng, radius = 9): { r: Rect; front: number } | null {
  const options: { r: Rect; front: number; key: number }[] = [];
  for (let dy = -radius; dy <= radius; dy++) {
    for (let dx = -radius; dx <= radius; dx++) {
      for (let front = 0; front < 4; front++) {
        const w = front % 2 === 0 ? lu : ld;
        const h = front % 2 === 0 ? ld : lu;
        const r = rect(Math.round(target.x + dx - w / 2), Math.round(target.y + dy - h / 2), w, h);
        const door = add(localPt(r, front, Math.floor(lu / 2), 0), DIRS4[front]!);
        if (!c.in(door.x, door.y)) continue;
        const gap = toTrack[c.i(door.x, door.y)]!;
        if (gap > 5) continue;
        options.push({ r, front, key: Math.hypot(dx, dy) * 0.6 + gap + rng.next() });
      }
    }
  }
  options.sort((a, b) => a.key - b.key);
  return options.find((o) => roomFor(c, o.r, 1)) ?? null;
}

/** A fenced field with furrows and a couple of hay bales, its gate towards `gate`. */
function field(c: Canvas, r: Rect, rng: Rng, gate: number): void {
  const alongX = r.w >= r.h;
  for (const p of tilesIn(inset(r, 1))) if ((alongX ? p.y - r.y : p.x - r.x) % 2 === 0) c.ground[c.i(p.x, p.y)] = ROAD;
  const gates = [gate, ...(rng.next() < 0.5 ? [opposite(gate)] : [])];
  for (let s = 0; s < 4; s++) {
    const n = sideLen(r, s);
    const g = Math.floor(n / 2) + rng.range(-1, 0);
    const gated = gates.includes(s);
    for (let u = 0; u < n; u++) {
      if (gated && (u === g || u === g + 1)) continue;
      put(c, localPt(r, s, u, 0), 'low', [gated && (u === g - 1 || u === g + 2) ? 'gatePost' : 'fence', s]);
    }
  }
  if (r.w * r.h >= 80) groupNear(c, centre(r), rng, [[[0, 0, 'low', ['hay']], [1, 0, 'low', ['hay']]]], 2, 0, 6);
  for (const p of tilesIn(inset(r, 1))) c.reserve(p.x, p.y);
}

/** A hill one storey high whose outline follows a blob, climbed by ladders. */
function hill(c: Canvas, at: Vec2, rng: Rng): boolean {
  const circles = Array.from({ length: rng.range(3, 4) }, (_, k) => ({
    x: at.x + (k ? rng.range(-3, 3) : 0),
    y: at.y + (k ? rng.range(-3, 3) : 0),
    r: 1.8 + rng.next() * 1.8,
  }));
  const inBlob = (x: number, y: number) => circles.some((q) => Math.hypot(x - q.x, y - q.y) <= q.r);
  let blob = tilesIn(rect(at.x - 7, at.y - 7, 15, 15)).filter((p) => inBlob(p.x, p.y));
  // No spurs one tile wide.
  const has = (set: Vec2[], x: number, y: number) => set.some((q) => q.x === x && q.y === y);
  blob = blob.filter((p) => DIRS4.filter((d) => has(blob, p.x + d.x, p.y + d.y)).length >= 2);
  if (blob.length < 14) return false;
  for (const p of blob) for (const d of DIRS8) if (!c.free(p.x + d.x, p.y + d.y) || c.groundAt(p.x + d.x, p.y + d.y) !== OPEN) return false;
  for (const p of blob) c.force(p.x, p.y, 'floor', 1);
  // Ladders on the side facing the squad and on the far side.
  for (const target of [{ x: 0, y: 0 }, { x: c.width, y: c.height }]) {
    const feet: Vec2[] = [];
    for (const p of blob) {
      for (const d of DIRS4) {
        const f = add(p, d);
        const beyond = add(f, d);
        if (!has(blob, f.x, f.y) && c.free(f.x, f.y) && c.free(beyond.x, beyond.y)) feet.push(f);
      }
    }
    const foot = best(feet, (f) => dist(f, target) + rng.next() * 3);
    if (!foot) continue;
    c.force(foot.x, foot.y, 'ladder');
    c.reserve(foot.x, foot.y);
    for (const d of DIRS4) c.reserve(foot.x + d.x, foot.y + d.y);
  }
  groupNear(c, at, rng, OUTCROPS, 1, 1, 8);
  return true;
}

/**
 * Wilds: a dirt track from the squad's corner to the far side, a farmstead or
 * ruins beside it near the objective and a cabin halfway, fenced fields, hills,
 * groves with clearings between them and rock outcrops.
 */
function wilds(c: Canvas, rng: Rng, style: Style, role: Role): Rect {
  const S = c.width;
  const far = { x: S * 0.76, y: S * 0.76 };
  const track = trackLine(S, rng);
  const onTrack: number[] = [];
  for (const p of track) {
    for (const [dx, dy] of [[0, 0], [1, 0], [0, 1], [1, 1]] as const) {
      const x = p.x + dx;
      const y = p.y + dy;
      if (!c.in(x, y) || c.keep[c.i(x, y)]) continue;
      c.ground[c.i(x, y)] = ROAD;
      c.reserve(x, y);
      onTrack.push(c.i(x, y));
    }
  }
  const toTrack = distanceField(c, onTrack);
  const near = best(track, (p) => dist(p, far))!;
  const target = { x: (near.x + far.x) / 2, y: (near.y + far.y) / 2 };
  let objective: Rect | null = null;
  let farm: Rect | null = null;
  if (role === 'indoor') {
    // Farmstead: the farmhouse and the barn side by side, both onto the track, a farmyard between.
    const house = siteNear(c, target, style.house, style.houseDepth, toTrack, rng);
    if (house) {
      objective = building(c, house.r, rng, 'rooms', house.front, style, 'home').outer;
      const along = DIRS4[(house.front + 1) % 4]!;
      const bw = Math.min(style.hall, 14);
      const bd = Math.min(style.hall - 2, 12);
      const shift = Math.ceil(style.house / 2 + 3 + bw / 2);
      const m = centre(house.r);
      const behind = DIRS4[opposite(house.front)]!;
      const spots = [...shuffle([1, -1], rng).map((k) => ({ x: along.x * shift * k, y: along.y * shift * k })), { x: behind.x * (shift + 1), y: behind.y * (shift + 1) }];
      for (const off of spots) {
        const barn = siteNear(c, add(m, off), bw, bd, toTrack, rng, 6) ?? siteNear(c, add(m, off), 12, 10, toTrack, rng, 6);
        if (!barn) continue;
        building(c, barn.r, rng, 'hall', barn.front, style, 'barn');
        // Hay in the farmyard: a stack and a few bales.
        groupNear(c, { x: m.x + off.x / 2, y: m.y + off.y / 2 }, rng, [dressed(PAIR, 'hay'), dressed(BARRICADE, 'hay')], 2);
        break;
      }
      farm = objective;
    }
  } else {
    // Ruins of a farmhouse in a clearing, the objective in or around them.
    const site = siteNear(c, target, style.house + 2, style.houseDepth + 1, toTrack, rng);
    if (site) {
      ruin(c, building(c, site.r, rng, 'rooms', site.front, style, 'bare'), rng);
      objective = rect(site.r.x - 3, site.r.y - 3, site.r.w + 6, site.r.h + 6);
    }
  }
  // Halfway along: a cabin, sometimes in ruins.
  const halfway = track[Math.round(track.indexOf(near) * 0.45)]!;
  const cabinSite = siteNear(c, halfway, style.house, style.houseDepth, toTrack, rng, 7);
  if (cabinSite) {
    const cabin = building(c, cabinSite.r, rng, 'rooms', cabinSite.front, style, 'home');
    if (rng.next() < 0.35) ruin(c, cabin, rng);
    farm ??= cabin.outer;
  }
  // Fields near the farm, gates towards it, and one more out in the open.
  const fieldAt = [farm ? centre(farm) : far, { x: rng.range(14, S - 14), y: rng.range(14, S - 14) }];
  for (const at of fieldAt) {
    const w = rng.range(10, 13);
    const h = rng.range(7, 9);
    const options = tilesIn(rect(Math.round(at.x) - 14, Math.round(at.y) - 14, 29, 29))
      .map((p) => ({ r: rect(p.x - Math.floor(w / 2), p.y - Math.floor(h / 2), w, h), key: dist(p, at) + rng.next() * 4 }))
      .filter((o) => dist(centre(o.r), at) >= 8)
      .sort((a, b) => a.key - b.key);
    const spot = options.find((o) => roomFor(c, o.r, 1));
    if (!spot) continue;
    const m = centre(spot.r);
    const gate = Math.abs(at.x - m.x) > Math.abs(at.y - m.y) ? (at.x > m.x ? 1 : 3) : at.y > m.y ? 2 : 0;
    field(c, spot.r, rng, gate);
  }
  // Hills.
  let hills = 0;
  for (let k = 0; k < 40 && hills < 3; k++) if (hill(c, { x: rng.range(8, S - 9), y: rng.range(8, S - 9) }, rng)) hills++;
  // Groves where the noise is high, kept away from the squad, the evacuation and the objective.
  const big = valueNoise(S, 10, rng);
  const small = valueNoise(S, 4, rng);
  const objCentre = objective ? centre(objective) : far;
  const value = (x: number, y: number) => {
    let v = 0.75 * big[y * S + x]! + 0.25 * small[y * S + x]!;
    v -= 0.5 * Math.max(0, 1 - Math.hypot(x, y) / 13);
    v -= 0.5 * Math.max(0, 1 - Math.hypot(S - 1 - x, y) / 10);
    v -= 0.35 * Math.max(0, 1 - dist({ x, y }, objCentre) / 9);
    return v;
  };
  const land = tilesIn(rect(0, 0, S, S)).filter((p) => c.groundAt(p.x, p.y) === OPEN && c.get(p.x, p.y) === 'floor');
  const sorted = land.map((p) => value(p.x, p.y)).sort((a, b) => a - b);
  const T = sorted[Math.floor(sorted.length * 0.7)] ?? 0.6;
  for (const p of land) {
    const level = c.elevAt(p.x, p.y);
    if (!c.free(p.x, p.y, level)) continue;
    const v = value(p.x, p.y);
    const roll = rng.next();
    if (v > T) {
      // Dense trees with undergrowth, thicker towards the heart of the grove.
      if (roll < 0.42 + Math.min(0.3, (v - T) * 4)) {
        c.force(p.x, p.y, 'high');
        c.dress(p, 'tree');
      } else if (roll > 0.85) {
        c.force(p.x, p.y, 'low');
        c.dress(p, 'bush');
      }
    } else if (v > T - 0.025 && roll < 0.15) {
      c.force(p.x, p.y, 'high');
      c.dress(p, 'tree');
    }
  }
  // Rock outcrops and fallen logs out in the open.
  const outcrops: Vec2[] = [];
  for (let k = 0; k < 160 && outcrops.length < 8; k++) {
    const p = { x: rng.range(2, S - 3), y: rng.range(2, S - 3) };
    if (value(p.x, p.y) > T - 0.06 || outcrops.some((o) => dist(o, p) < 7) || Math.hypot(p.x, p.y) < 6) continue;
    if (group(c, p, turn(rng.pick(OUTCROPS), rng.int(4)))) outcrops.push(p);
  }
  for (let k = 0, logs = 0; k < 80 && logs < 4; k++) {
    const p = { x: rng.range(2, S - 3), y: rng.range(2, S - 3) };
    const v = value(p.x, p.y);
    if (v < T - 0.1 || v > T - 0.02) continue;
    if (group(c, p, turn(dressed(rng.pick([BARRICADE, [[0, 0, 'low'], [1, 0, 'low']] as Part[]]), 'log'), rng.int(4)))) logs++;
  }
  return objective ?? rect(Math.round(far.x) - 6, Math.round(far.y) - 6, 12, 12);
}

// ------------------------------------------------------------ connectivity

/** Walkable tiles reachable from `start`, following the movement rules. */
function flood(c: Canvas, start: Vec2, within?: (i: number) => boolean): Set<number> {
  const s = c.asState();
  const seen = new Set<number>([c.i(start.x, start.y)]);
  const queue = [start];
  while (queue.length) {
    const p = queue.pop()!;
    for (const d of DIRS8) {
      if (stepCost(s, p.x, p.y, d.x, d.y) === null) continue;
      const q = { x: p.x + d.x, y: p.y + d.y };
      const i = c.i(q.x, q.y);
      if (seen.has(i) || (within && !within(i))) continue;
      seen.add(i);
      queue.push(q);
    }
  }
  return seen;
}

/** One change that links tile `i` (out of reach) to the reachable area, applied at once; false if none. */
function linkTile(c: Canvas, i: number, reach: Set<number>): boolean {
  const x = i % c.width;
  const y = (i - x) / c.width;
  const level = c.elev[i]!;
  const reached = (p: Vec2) => c.in(p.x, p.y) && reach.has(c.i(p.x, p.y));
  for (const d of DIRS4) {
    const n = { x: x + d.x, y: y + d.y };
    if (!c.in(n.x, n.y)) continue;
    const kind = c.get(n.x, n.y);
    // A stranded rooftop gets a ladder from the floor one storey down.
    if (level > 0 && reached(n) && kind === 'floor' && c.elevAt(n.x, n.y) === level - 1) {
      c.force(n.x, n.y, 'ladder');
      return true;
    }
    if (c.elevAt(n.x, n.y) !== level || isWalkable(kind)) continue;
    // A blocking object (or a wall: a new door), one or two tiles thick.
    const beyond = add(n, d);
    if (reached(beyond) && c.elevAt(beyond.x, beyond.y) === level) {
      c.force(n.x, n.y, kind === 'wall' ? 'door' : 'floor');
      return true;
    }
    const further = add(beyond, d);
    const clearable = (k: TileKind) => k === 'low' || k === 'high';
    if (clearable(kind) && clearable(c.get(beyond.x, beyond.y)) && reached(further) && c.elevAt(beyond.x, beyond.y) === level && c.elevAt(further.x, further.y) === level) {
      c.force(n.x, n.y, 'floor');
      c.force(beyond.x, beyond.y, 'floor');
      return true;
    }
  }
  return false;
}

/**
 * Deep in a grove: clears the fewest objects (trees, rocks, crates) on one
 * level that open a way from `pocket` to the reachable area.
 */
function carve(c: Canvas, pocket: Set<number>, reach: Set<number>): boolean {
  const cost = new Map<number, number>();
  const prev = new Map<number, number>();
  const buckets: number[][] = [[...pocket]];
  for (const i of pocket) cost.set(i, 0);
  for (let k = 0; k < buckets.length; k++) {
    const bucket = buckets[k] ?? [];
    for (let n = 0; n < bucket.length; n++) {
      const i = bucket[n]!;
      if (cost.get(i) !== k) continue;
      const x = i % c.width;
      const y = (i - x) / c.width;
      for (const d of DIRS4) {
        if (!c.in(x + d.x, y + d.y)) continue;
        const j = c.i(x + d.x, y + d.y);
        if (c.elev[j] !== c.elev[i]) continue;
        const kind = c.tiles[j]!;
        if (reach.has(j) && isWalkable(kind)) {
          for (let t: number | undefined = i; t !== undefined && !pocket.has(t); t = prev.get(t)) if (!isWalkable(c.tiles[t]!)) c.tiles[t] = 'floor';
          return true;
        }
        const step = isWalkable(kind) ? 0 : kind === 'low' || kind === 'high' ? 1 : -1;
        if (step < 0 || (cost.get(j) ?? Infinity) <= k + step) continue;
        cost.set(j, k + step);
        prev.set(j, i);
        (buckets[k + step] ??= []).push(j);
      }
    }
  }
  return false;
}

/**
 * Opens up every walkable pocket the squad could not reach: a ladder for a
 * stranded rooftop, a cleared object or a new door for a sealed ground area,
 * a path cut through a thick grove. Each pass fixes every pocket it can.
 */
function connect(c: Canvas, start: Vec2): Set<number> {
  for (let pass = 0; pass < 30; pass++) {
    const reach = flood(c, start);
    const done = new Set<number>();
    let fixed = false;
    for (let i = 0; i < c.tiles.length; i++) {
      if (reach.has(i) || done.has(i) || !isWalkable(c.tiles[i]!)) continue;
      const x = i % c.width;
      const pocket = flood(c, { x, y: (i - x) / c.width }, (j) => !reach.has(j));
      for (const j of pocket) done.add(j);
      if ([...pocket].some((j) => linkTile(c, j, reach)) || carve(c, pocket, reach)) fixed = true;
    }
    if (!fixed) return reach;
  }
  return flood(c, start);
}

/**
 * Decor stays only on what is still cover (making room and opening ways
 * through clears some of it), and on cars only while both halves are there.
 */
function tidyDecor(c: Canvas): void {
  for (let i = 0; i < c.tiles.length; i++) if (c.tiles[i] !== 'low' && c.tiles[i] !== 'high') c.decor[i] = 0;
  const kind = (i: number) => decorOf(c.decor[i])?.kind;
  for (let i = 0; i < c.tiles.length; i++) if ((kind(i) === 'carFront' || kind(i) === 'carRear') && carPartner(c.decor, c.width, i) < 0) c.decor[i] = 0;
}

// ---------------------------------------------------------------- placing

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

/** Clears a 3×3 evacuation zone at ground level, outdoors, in `r`, as close to `corner` as it can. */
function carveEvac(c: Canvas, r: Rect, corner: Vec2, rng: Rng): Vec2[] {
  const options: { x: number; y: number; cost: number }[] = [];
  for (let y = r.y; y <= r.y + r.h - 3; y++) {
    for (let x = r.x; x <= r.x + r.w - 3; x++) {
      let cost = 0;
      for (let dy = 0; dy < 3 && cost < 99; dy++) {
        for (let dx = 0; dx < 3; dx++) {
          const kind = c.get(x + dx, y + dy);
          if (!c.in(x + dx, y + dy) || c.elevAt(x + dx, y + dy) !== 0 || c.groundAt(x + dx, y + dy) === INDOOR || (kind !== 'floor' && kind !== 'low' && kind !== 'high')) cost = 99;
          else if (kind !== 'floor') cost++;
        }
      }
      if (cost >= 99) continue;
      // Out in the open rather than squeezed between buildings.
      let cramped = 0;
      for (let dy = -1; dy <= 3; dy++) for (let dx = -1; dx <= 3; dx++) if (c.in(x + dx, y + dy) && !isWalkable(c.get(x + dx, y + dy))) cramped++;
      options.push({ x, y, cost: cost + cramped * 0.3 + dist({ x: x + 1, y: y + 1 }, corner) * 0.08 + rng.next() * 0.3 });
    }
  }
  const pick = best(options, (o) => o.cost);
  if (!pick) return [];
  const zone: Vec2[] = [];
  for (let dy = 0; dy < 3; dy++) {
    for (let dx = 0; dx < 3; dx++) {
      c.force(pick.x + dx, pick.y + dy, 'floor');
      c.reserve(pick.x + dx, pick.y + dy);
      zone.push({ x: pick.x + dx, y: pick.y + dy });
    }
  }
  return zone;
}

const NAMES: Record<Biome, string[]> = {
  city: ['Distrito Portuario', 'Barrio Viejo', 'Centro Financiero', 'Polígono Norte', 'Ensanche', 'Mercado Central', 'Zona Universitaria'],
  wilds: ['Bosque de Lirio', 'Valle Hondo', 'Granjas del Sur', 'Pinar Quemado', 'Ruinas de Santa Úrsula', 'Afueras de Robledo'],
  facility: ['Conservera del Norte', 'Nave 14', 'Planta química', 'Almacén frigorífico', 'Fábrica de piensos'],
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
  const S = GEN_SIZE;
  const c = new Canvas(S, S);
  // The finished map is mirrored and/or transposed, so the squad may start in any corner.
  const flipX = rng.int(2) === 1;
  const flipY = rng.int(2) === 1;
  const swap = rng.int(2) === 1;
  const needsEvac = kind !== 'elimination';
  const role: Role = kind === 'recovery' || kind === 'rescue' || (kind === 'hack' && biome !== 'wilds') ? 'indoor' : 'open';

  // Keep the squad's corner clear.
  for (let y = 0; y < 5; y++) for (let x = 0; x < 5; x++) c.reserve(x, y);
  const style = pickStyle(rng);
  const compound = biome === 'facility' ? facility(c, rng, style, role) : null;
  const objRect = compound?.objective ?? (biome === 'city' ? city(c, rng, style, role) : wilds(c, rng, style, role));
  // Something to take cover behind on the squad's first move: barricades and crates, rocks in the wilds.
  const posts = [BARRICADE, CORNER, PAIR].map((parts) => (biome === 'wilds' ? dressed(parts, 'rock') : dressed(parts, 'barricade', 'crates')));
  for (const at of [{ x: 8, y: 3 }, { x: 3, y: 8 }, { x: 8, y: 8 }]) groupNear(c, at, rng, posts, 1, 0, 5);

  let evacZone: Vec2[] = [];
  if (needsEvac) {
    // In a facility entered from the apron the evac is inside the fence (a yard by the far wall).
    const top = compound && !compound.inside ? FACILITY_INSIDE : 0;
    const corner = { x: S - 1, y: top };
    evacZone = carveEvac(c, rect(S - 14, top, 14, 14), corner, rng);
    if (!evacZone.length) evacZone = carveEvac(c, rect(S - 22, top, 22, 22), corner, rng);
  }

  const startTile = { x: 0, y: 0 };
  const reach = connect(c, startTile);
  const taken = new Set<number>();
  const standable = (p: Vec2) => c.in(p.x, p.y) && reach.has(c.i(p.x, p.y)) && canStandOn(c.get(p.x, p.y)) && !taken.has(c.i(p.x, p.y));

  // Squad: the tiles closest to the corner.
  const spawns = tilesIn(rect(0, 0, 12, 12))
    .filter((p) => standable(p) && c.elevAt(p.x, p.y) === 0)
    .sort((a, b) => dist(a, startTile) - dist(b, startTile) || a.y - b.y || a.x - b.x)
    .slice(0, MAX_SQUAD);
  for (const p of spawns) taken.add(c.i(p.x, p.y));
  const squadCenter = spawns.reduce((acc, p) => ({ x: acc.x + p.x / spawns.length, y: acc.y + p.y / spawns.length }), { x: 0, y: 0 });

  // Objective.
  const objTiles = shuffle(tilesIn(objRect).filter((p) => standable(p) && !evacZone.some((z) => z.x === p.x && z.y === p.y)), rng);
  const enclosed = (p: Vec2) => DIRS4.filter((d) => c.get(p.x + d.x, p.y + d.y) === 'wall').length;
  let item: Vec2 | null = null;
  let terminal: Vec2 | null = null;
  let objectiveSpot: Vec2 | null = null;
  if (kind === 'recovery' || kind === 'rescue') {
    const spot = best(objTiles, (p) => -enclosed(p) - (c.groundAt(p.x, p.y) === INDOOR ? 2 : 0)) ?? null;
    if (kind === 'recovery') item = spot;
    else objectiveSpot = spot;
  } else if (kind === 'sabotage') {
    objectiveSpot = objTiles.find((p) => c.elevAt(p.x, p.y) === 0 && DIRS8.filter((d) => standable({ x: p.x + d.x, y: p.y + d.y })).length >= 5) ?? objTiles[0] ?? null;
  } else if (kind === 'hack') {
    const indoorFirst = [...objTiles.filter((p) => c.groundAt(p.x, p.y) === INDOOR), ...objTiles.filter((p) => c.groundAt(p.x, p.y) !== INDOOR)];
    for (const p of indoorFirst) {
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

  // Pods: spread over the map, never near the squad.
  const anchors: Vec2[] = [];
  const pods: MissionLayout['pods'] = [];
  const candidates = shuffle(
    Array.from({ length: c.tiles.length }, (_, i) => ({ x: i % c.width, y: Math.floor(i / c.width) })).filter(
      (p) => standable(p) && dist(p, squadCenter) >= POD_DISTANCE,
    ),
    rng,
  );
  const spread = (p: Vec2, gap: number) => anchors.every((a) => dist(a, p) >= gap);
  for (const size of opts.podSizes) {
    let anchor = candidates.find((p) => standable(p) && spread(p, 11)) ?? candidates.find((p) => standable(p) && spread(p, 6)) ?? candidates.find(standable);
    if (!anchor) break;
    if (cluster(c, reach, anchor, size, taken).length < size) {
      anchor = candidates.find((p) => standable(p) && spread(p, 3) && cluster(c, reach, p, size, taken).length >= size);
      if (!anchor) break;
    }
    const members = cluster(c, reach, anchor, size, taken);
    for (const p of members) taken.add(c.i(p.x, p.y));
    anchors.push(anchor);
    const waypoints = shuffle(candidates.filter((p) => dist(p, anchor!) >= 7 && dist(p, squadCenter) >= 12), rng).slice(0, 2);
    pods.push({ tiles: members, patrol: [anchor, ...waypoints] });
  }

  tidyDecor(c);

  // Mirror/transpose everything into the final frame.
  const T = (p: Vec2): Vec2 => {
    const x = swap ? p.y : p.x;
    const y = swap ? p.x : p.y;
    return { x: flipX ? S - 1 - x : x, y: flipY ? S - 1 - y : y };
  };
  /** A direction (DIRS4 index) in the final frame. */
  const D = (f: number): number => {
    const d = DIRS4[f]!;
    const x = (swap ? d.y : d.x) * (flipX ? -1 : 1);
    const y = (swap ? d.x : d.y) * (flipY ? -1 : 1);
    return DIRS4.findIndex((q) => q.x === x && q.y === y);
  };
  /** A decor hint turned with the map. A mirror can't turn a corner piece: it faces its other side instead. */
  const Dc = (code: Decor): Decor => {
    const look = decorOf(code);
    if (!look) return 0;
    const f = D(look.facing);
    if (look.kind !== 'fountainCorner') return decorCode(look.kind, f);
    return decorCode(look.kind, D((look.facing + 3) % 4) === (f + 3) % 4 ? f : D((look.facing + 3) % 4));
  };
  const tiles = new Array<TileKind>(S * S);
  const elev = new Array<number>(S * S);
  const ground = new Array<GroundKind>(S * S);
  const decor = new Array<Decor>(S * S);
  for (let y = 0; y < S; y++) {
    for (let x = 0; x < S; x++) {
      const q = T({ x, y });
      const i = c.i(x, y);
      tiles[q.y * S + q.x] = c.tiles[i]!;
      elev[q.y * S + q.x] = c.elev[i]!;
      ground[q.y * S + q.x] = c.ground[i]!;
      decor[q.y * S + q.x] = Dc(c.decor[i]!);
    }
  }
  const t = (p: Vec2 | null) => (p ? T(p) : null);

  return {
    id: `gen-${biome}-${opts.seed}`,
    name: rng.pick(NAMES[biome]),
    biome,
    width: S,
    height: S,
    tiles,
    elev,
    ground,
    decor,
    spawns: spawns.map(T),
    pods: pods.map((p) => ({ tiles: p.tiles.map(T), patrol: p.patrol.map(T) })),
    item: t(item),
    terminal: t(terminal),
    objectiveSpot: t(objectiveSpot),
    evacZone: evacZone.map(T),
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
