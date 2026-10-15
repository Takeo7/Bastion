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
import { dressMedical } from './medicalDressing';
import { MAPS, parseMap, type MapDef } from './maps';
import { stepCost } from './path';
import { Rng } from './rng';
import { canStandOn, DIRS4, DIRS8, isWalkable } from './grid';
import { carPartner, decorCode, decorOf } from './decor';
import type { Biome, Decor, DecorKind, GameState, GroundKind, Level, MedicalRoom, MedicalUse, MedicalSite, MissionKind, Pos, Site, TileKind, Vec2 } from './types';

/** Side of every procedural map, in tiles. */
export const GEN_SIZE = 48;
/** Tiles reserved for the squad at the start. */
export const MAX_SQUAD = 8;
/** No pod starts closer than this to the middle of the squad. */
const POD_DISTANCE = 16;

const OPEN: GroundKind = 0;
const ROAD: GroundKind = 1;
const INDOOR: GroundKind = 2;
/** Asphalt that isn't a street (a forecourt, a yard, a car park off the road): no road markings. */
const YARD: GroundKind = 3;

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
  medical?: MedicalSite[];
  /** What each piece of cover is meant to be (procedural maps only; cosmetic). */
  decor?: Decor[];
  /** Storeys of the building each tile belongs to, 0 outside (procedural maps only; GameState.storeys). */
  storeys?: number[];
  /** The storeys above the ground (GameState.levels; docs/FASE6-PLAN.md). */
  levels?: Level[];
  /** A few places together on an upper floor, by a window and far from the squad, for a group of the living posted up there (decision 7). */
  perch?: Pos[];
  /** Where the map's street lights and floodlights stand (procedural maps only; GameState.lights). */
  lights?: Vec2[];
  spawns: Vec2[];
  /** Spawn tiles and patrol route of each pod, in the order the pod plan lists them (on the ground unless they say `z`). */
  pods: { tiles: (Vec2 & { z?: number })[]; patrol: (Vec2 & { z?: number })[] }[];
  /** Recovery data; also the default spot for other objectives on hand-made maps. */
  item: Vec2 | null;
  terminal: Vec2 | null;
  /** Where the relay (sabotage) or the VIP (rescue) stands. */
  objectiveSpot: Vec2 | null;
  evacZone: Vec2[];
  /** The bus parked beside the evac zone: its tiles (`high`, decor 'bus') and the way it drives off (DIRS4 index). */
  evacBus?: { tiles: Vec2[]; heading: number };
  /** The scenario, when not the biome's usual one (GenOptions.site). */
  site?: Site;
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
  /** Every walled building raised so far, for their storeys (storeysOf). */
  readonly buildings: Building[] = [];

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
  /** A town laid out as an old town (bent streets, uneven plots, small squares) rather than a grid: city() sets it. */
  organic?: boolean;
  /** An industrial estate (playtest 2026-10-14, #25): big blocks of warehouses and container yards, a gate with wire. */
  industrial?: boolean;
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
type Furnish = 'home' | 'shop' | 'office' | 'lab' | 'store' | 'barn' | 'bare' | 'clinic';

interface Building {
  outer: Rect;
  rooms: Rect[];
  /** What it was furnished as: it sets how tall it stands (storeysOf). */
  furnish: Furnish;
  /** Storeys when they aren't the furnishing's to say (a filling station's shop, a motel). */
  storeys?: number;
  /** Left in ruins (ruin): no roof, no storeys above. */
  ruined?: boolean;
  /** A flat roof though it is furnished as a home (a motel). */
  flat?: boolean;
  /** Clinical finish and damage, independent of furniture surviving a blast. */
  medicalWear?: number;
  medicalKind?: 'clinic' | 'ward' | 'hospital';
  medicalRooms?: MedicalRoom[];
  front?: number;
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

/** Clinical rooms have a purpose, doors and a shared circulation space. */
function medicalPlan(c: Canvas, b: Rect, front: number, rng: Rng, z: number, hospital: boolean, ward: boolean, doors: (number | null)[]): MedicalRoom[] {
  const inner = inset(b, 1), across = sideLen(inner, front), length = depthOf(inner, front);
  const rooms: MedicalRoom[] = [];
  const at = (u: number, v: number) => localPt(inner, front, u, v);
  const room = (u: number, v: number, w: number, h: number, use: MedicalUse) => rooms.push({ ...local(inner, front, u, v, w, h), z, use });
  const wall = (u: number, v: number) => { const p = at(u, v); c.force(p.x, p.y, 'wall'); };
  const door = (u: number, v: number, du: number, dv: number, wide = false) => {
    const p = at(u, v); c.force(p.x, p.y, wide ? 'floor' : 'door');
    for (const q of [p, at(u + du, v + dv), at(u - du, v - dv)]) c.reserve(q.x, q.y);
  };
  if (!hospital && !ward) {
    // A compact waiting/reception area, with two closed clinical rooms behind.
    const lobby = length >= 7 ? 3 : 2;
    room(0, 0, across, lobby, 'reception');
    for (let u = 0; u < across; u++) wall(u, lobby);
    const left = Math.floor((across - 1) / 2);
    for (let v = lobby + 1; v < length; v++) wall(left, v);
    room(0, lobby + 1, left, length - lobby - 1, ward ? 'ward' : 'consultation');
    room(left + 1, lobby + 1, across - left - 1, length - lobby - 1, ward ? 'treatment' : 'equipment');
    door(Math.floor(left / 2), lobby, 0, 1);
    door(left + 1 + Math.floor((across - left - 1) / 2), lobby, 0, 1);
  } else {
    // Hospital rooms are three tiles deep from the corridor, with a door for each.
    // Small infirmaries have a narrower corridor and combine reception with waiting.
    const cw = hospital ? 2 : 1;
    const twoWings = across >= 7;
    const left = twoWings ? Math.floor((across - cw - 2) / 2) : across - cw - 1;
    const cu = left + 1;
    const strips: [number, number, number][] = [[0, left, left]];
    if (twoWings) strips.push([cu + cw + 1, across - cu - cw - 1, cu + cw]);
    for (let v = 0; v < length; v++) {
      wall(left, v);
      if (twoWings) wall(cu + cw, v);
      for (let u = cu; u < cu + cw; u++) { const p = at(u, v); c.reserve(p.x, p.y); }
    }
    // Aligned partitions on every floor make real stairwells and short rooms.
    const count = Math.max(2, Math.floor((length + 1) / (hospital ? 4 : 3)));
    const total = length - count + 1;
    const sizes = Array.from({ length: count }, (_, k) => Math.floor(total / count) + (k < total % count ? 1 : 0));
    // In an infirmary, two rooms per wing are enough for waiting/triage,
    // treatment, observation and medical stores; avoid cramped one-tile rooms.
    if (!hospital && sizes.some((n) => n < 3) && length >= 7) {
      sizes.splice(0, sizes.length, Math.ceil((length - 1) / 2), Math.floor((length - 1) / 2));
    }
    const uses: MedicalUse[][] = !hospital
      ? [['waiting', 'ward', 'hygiene'], ['treatment', 'supplies', 'ward']]
      : z === 0
        ? [['reception', 'consultation', 'equipment', 'treatment'], ['waiting', 'consultation', 'hygiene', 'supplies']]
        : z === 1
          ? [['ward', 'nurses', 'ward', 'ward'], ['ward', 'treatment', 'hygiene', 'supplies']]
          : [['ward', 'nurses', 'ward', 'supplies'], ['ward', 'equipment', 'treatment', 'ward']];
    for (let side = 0; side < strips.length; side++) {
      const [u, width, wu] = strips[side]!;
      let v = 0;
      for (let k = 0; k < sizes.length; k++) {
        const depth = sizes[k]!;
        if (k) for (let x = u; x < u + width; x++) wall(x, v - 1);
        const use = hospital && k === sizes.length - 1 ? 'stairs' : uses[side]![k % uses[side]!.length]!;
        room(u, v, width, depth, use);
        const dv = v + Math.floor((depth - 1) / 2);
        door(wu, dv, 1, 0);
        // Reception and waiting open onto the corridor, rather than a private bedroom entrance.
        if ((use === 'waiting' || use === 'reception') && depth >= 3) door(wu, dv + 1, 1, 0, true);
        v += depth + 1;
      }
    }
    if (!z) {
      for (let u = cu; u < cu + cw; u++) {
        const p = localPt(b, front, u + 1, 0);
        if (opensOnto(c, p, front)) { opening(c, p, front, 'door'); doors[front] = u + 1; }
      }
    }
  }
  if (!z && !hospital && !ward) {
    // Put the entrance directly into reception; never into a consultation room.
    const u = Math.floor(across / 2) + 1;
    const p = localPt(b, front, u, 0);
    if (opensOnto(c, p, front)) { opening(c, p, front, 'door'); doors[front] = u; }
  }
  return rooms;
}

/** Furniture expresses each room's use and leaves its doorway and central aisle clear. */
function furnishMedical(c: Canvas, room: MedicalRoom, rng: Rng, care?: 'ward' | 'hospital'): void {
  if (care) { furnishCareRoom(c, room, rng, care); return; }
  const long = Math.max(room.w, room.h), short = Math.min(room.w, room.h), alongX = room.w >= room.h;
  const P = (a: number, b: number): Vec2 => alongX ? { x: room.x + a, y: room.y + b } : { x: room.x + b, y: room.y + a };
  const joined = () => {
    const floor = tilesIn(room).filter((p) => c.get(p.x, p.y) === 'floor');
    const seen = new Set<number>(), queue = floor.length ? [floor[0]!] : [];
    while (queue.length) {
      const p = queue.pop()!, i = c.i(p.x, p.y);
      if (seen.has(i) || !inRect(room, p) || c.get(p.x, p.y) !== 'floor') continue;
      seen.add(i);
      for (const d of DIRS4) queue.push(add(p, d));
    }
    return floor.length > 0 && floor.every((p) => seen.has(c.i(p.x, p.y)))
      && tilesIn(room).filter((p) => c.decor[c.i(p.x, p.y)]).every((p) => DIRS4.some((d) => seen.has(c.i(p.x + d.x, p.y + d.y))));
  };
  const place = (kind: DecorKind, points: Vec2[], high = false) => points.some((p) => {
    if (!put(c, p, high ? 'high' : 'low', [kind, inward(room, p)])) return false;
    if (joined()) return true;
    c.force(p.x, p.y, 'floor'); c.decor[c.i(p.x, p.y)] = 0;
    return false;
  });
  const edge = shuffle([P(0, 0), P(long - 1, 0), P(0, short - 1), P(long - 1, short - 1)], rng);
  const near = [P(Math.floor(long / 2), 0), P(Math.floor(long / 2), short - 1), ...edge];
  switch (room.use) {
    case 'reception':
      place('receptionDesk', edge);
      for (let a = 1; a < long - 1; a += 2) place('waitingSeats', [P(a, short - 1), P(a, 0)]);
      place('wheelchair', [...edge].reverse());
      break;
    case 'consultation':
      place('desk', edge);
      place('examTable', [...edge].reverse());
      place('medicalCabinet', near, true);
      place('medicalCart', edge);
      break;
    case 'treatment':
      place('examTable', edge);
      place('medicalCart', near);
      place('medicalCabinet', [...edge].reverse(), true);
      place('medicalMonitor', edge);
      break;
    case 'equipment':
      place('examTable', edge);
      place('medicalShelf', [...edge].reverse(), true);
      place('xray', near, true);
      place('medicalMonitor', edge);
      break;
    case 'ward':
      for (let a = 0; a < long; a += 2) {
        place('hospitalBed', [P(a, 0)]);
        if (short >= 5) place('hospitalBed', [P(a, short - 1)]);
      }
      place('medicalCart', near);
      place('medicalMonitor', [...edge].reverse());
      place('medicalShelf', edge, true);
      break;
    case 'nurses':
      place('receptionDesk', edge);
      place('medicalShelf', [...edge].reverse(), true);
      place('gurney', near);
      place('medicalCabinet', edge, true);
      break;
  }
}

/** Compact rooms: equipment is placed by function, with connected floor and a free approach to every object. */
function furnishCareRoom(c: Canvas, room: MedicalRoom, rng: Rng, care: 'ward' | 'hospital'): void {
  if (room.use === 'stairs') return;
  const long = Math.max(room.w, room.h), short = Math.min(room.w, room.h), alongX = room.w >= room.h;
  const P = (a: number, b: number): Vec2 => alongX ? { x: room.x + a, y: room.y + b } : { x: room.x + b, y: room.y + a };
  const cells = tilesIn(room);
  const corners = shuffle([P(0, 0), P(long - 1, 0), P(0, short - 1), P(long - 1, short - 1)], rng);
  const boundary = shuffle(cells.filter((p) => p.x === room.x || p.y === room.y || p.x === room.x + room.w - 1 || p.y === room.y + room.h - 1), rng);
  const positions = [...corners, ...boundary];
  const joined = () => {
    const floor = cells.filter((p) => c.get(p.x, p.y) === 'floor');
    const seen = new Set<number>(), queue = floor.length ? [floor[0]!] : [];
    while (queue.length) {
      const p = queue.pop()!, i = c.i(p.x, p.y);
      if (seen.has(i) || !inRect(room, p) || c.get(p.x, p.y) !== 'floor') continue;
      seen.add(i);
      for (const d of DIRS4) queue.push(add(p, d));
    }
    return floor.length > 0 && floor.every((p) => seen.has(c.i(p.x, p.y)))
      && cells.filter((p) => c.decor[c.i(p.x, p.y)]).every((p) => DIRS4.some((d) => seen.has(c.i(p.x + d.x, p.y + d.y))));
  };
  const high = new Set<DecorKind>(['medicalCabinet', 'medicalShelf', 'waterDispenser', 'xray']);
  const place = (kind: DecorKind, points = positions): Vec2 | null => {
    for (const p of points) {
      if (!put(c, p, high.has(kind) ? 'high' : 'low', [kind, inward(room, p)])) continue;
      if (joined()) return p;
      c.force(p.x, p.y, 'floor'); c.decor[c.i(p.x, p.y)] = 0;
    }
    return null;
  };
  switch (room.use) {
    case 'ward': {
      // One bed in 3×3; at most two in 3×4. Keep beds and bedside tables together.
      const beds = long >= 4 && short >= 3 ? 2 : 1;
      for (let k = 0; k < beds; k++) {
        const p = place('hospitalBed', k ? [P(long - 1, 0), P(long - 1, short - 1), ...corners] : [P(0, 0), ...corners]);
        if (p) place('bedsideTable', DIRS4.map((d) => add(p, d)).filter((q) => inRect(room, q)));
      }
      place('medicalMonitor');
      place('medicalCabinet');
      place('visitorChair');
      place('clinicalSink');
      place('clinicalBin');
      break;
    }
    case 'consultation':
      place('consultDesk'); place('examTable', [...corners].reverse()); place('clinicalSink');
      place('medicalCabinet'); place('visitorChair'); place('medicalCart'); place('clinicalBin');
      break;
    case 'treatment':
      place(rng.int(2) ? 'examTable' : 'gurney'); place('sterilizer'); place('clinicalSink'); place('medicalCart');
      place('medicalCabinet'); place('medicalMonitor'); place('clinicalBin');
      break;
    case 'equipment':
      place(rng.int(2) ? 'xray' : 'ultrasound'); place('examTable'); place('medicalMonitor');
      place('sterilizer'); place('medicalCabinet'); place('clinicalSink'); place('clinicalBin');
      break;
    case 'reception':
      place('receptionDesk'); place('waitingSeats'); place('medicalCabinet');
      place('waitingTable'); place('waterDispenser'); place('clinicalBin');
      break;
    case 'waiting':
      if (care === 'ward') place('receptionDesk');
      place('waitingSeats'); place('waitingSeats', [...corners].reverse()); place('waitingTable');
      place('waterDispenser'); place('wheelchair'); place('clinicalBin');
      break;
    case 'nurses':
      place('receptionDesk'); place('medicalCabinet'); place('medicalCart');
      place('medicalMonitor'); place('sterilizer'); place('clinicalBin');
      break;
    case 'supplies':
      place('medicalShelf'); place('medicalCabinet'); place('medicalCabinet');
      place('medicalCart'); place('sterilizer'); place('clinicalBin');
      break;
    case 'hygiene':
      place('toilet'); place('clinicalSink'); place('clinicalBin'); place('medicalCabinet');
      break;
  }
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
    case 'clinic':
      corner('low', 'hospitalBed');
      corner('high', 'medicalShelf');
      // Along the room's edge, with a central aisle. Different room shapes
      // and shuffled corners give consultation rooms and wards their own plan.
      if (short >= 3) {
        [P(mid, short - 1), P(mid, 0), P(mid - 1, short - 1), P(mid - 1, 0)]
          .some((p) => put(c, p, 'low', ['medicalCart', inward(room, p)]));
      }
      if (short >= 5) {
        corner('high', 'medicalCabinet');
        corner('low', rng.int(2) ? 'gurney' : 'wheelchair');
      }
      if (long >= 8 && short >= 5) {
        for (const a of [2, long - 3]) put(c, P(a, 0), 'low', ['hospitalBed', toward(0, 1)]);
      }
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
function building(c: Canvas, b: Rect, rng: Rng, plan: Plan, front: number, style: Style, furnish: Furnish, medicalKind: 'clinic' | 'ward' | 'hospital' = 'clinic'): Building {
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
  const medicalRooms = furnish === 'clinic' ? medicalPlan(c, b, front, rng, 0, medicalKind === 'hospital', medicalKind === 'ward', doors) : undefined;
  if (medicalRooms) {
    rooms.push(...medicalRooms);
  } else if (plan === 'corridor' && corridorPlan(c, b, front, rng, rooms, doors)) {
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
  if (medicalRooms) {
    medicalRooms.forEach((room) => furnishMedical(c, room, rng, medicalKind === 'clinic' ? undefined : medicalKind));
  } else if (plan === 'hall') {
    furnishHall(c, rooms[0]!, front, rng, furnish, office);
    if (office) furnishRoom(c, office, rng, 'office');
  } else {
    rooms.forEach((room, k) => furnishRoom(c, room, rng, furnish === 'shop' && k > 0 ? 'store' : furnish));
  }
  const raised: Building = { outer: b, rooms, furnish, ...(medicalRooms ? { medicalKind, medicalRooms, front } : {}) };
  c.buildings.push(raised);
  return raised;
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
  b.ruined = true;
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
  /** Streets that bend (an old town): the chance, and the most stretches a street has, by depth. */
  bend?: number[];
  bends?: number[];
}

const swapRect = (r: Rect): Rect => rect(r.y, r.x, r.h, r.w);

/** The shortest straight stretch of a street that bends: a building's length beside it. */
const STRETCH = 8;

/**
 * A street across `r` (north–south) that bends: `k` straight stretches, each
 * a step of two or three tiles sideways from the last, joined by short cross
 * streets. Mostly the steps go the same way, so the street runs on a slant
 * across the district; else back and forth. On each side, the land beside a
 * stretch is a block of its own (with the cross street's corner when the
 * street steps away from it). Null when `r` is too short or too narrow.
 */
function bentStreet(r: Rect, w: number, k: number, rng: Rng, rules: SplitRules): { parts: Rect[]; streets: Road[] } | null {
  const lo = r.x + rules.minBlock;
  const hi = r.x + r.w - rules.minBlock - w;
  const free = r.h - k * STRETCH - (k - 1) * w;
  if (k < 2 || hi - lo < 2 || free < 0) return null;
  // Where each stretch ends (its cross street starts): even stretches, the slack shared out at random.
  const ends: number[] = [];
  let y = r.y;
  let slack = free;
  for (let i = 0; i < k - 1; i++) {
    const extra = rng.int(Math.floor(slack / (k - i)) * 2 + 1);
    const take = Math.min(extra, slack);
    slack -= take;
    ends.push(y + STRETCH + take);
    y = ends[i]! + w;
  }
  const slant = rng.next() < 0.65;
  const dir = rng.int(2) === 0 ? -1 : 1;
  const xs = [clamp(Math.round((lo + hi) / 2 - (slant ? dir * (k - 1) * 1.25 : 0) + rng.range(-1, 1)), lo, hi)];
  for (let i = 1; i < k; i++) {
    const prev = xs[i - 1]!;
    const step = (slant ? dir : rng.int(2) === 0 ? -1 : 1) * rng.range(2, 3);
    let x = prev + step;
    if (x < lo || x > hi) x = prev - step;
    x = clamp(x, lo, hi);
    if (x === prev) x = prev + (prev < hi ? 1 : -1);
    xs.push(x);
  }
  const bottom = r.y + r.h;
  const right = r.x + r.w;
  const top = (i: number) => (i === 0 ? r.y : ends[i - 1]! + w);
  const end = (i: number) => (i === k - 1 ? bottom : ends[i]!);
  const streets: Road[] = [];
  const parts: Rect[] = [];
  for (let i = 0; i < k; i++) {
    const a = xs[i]!;
    streets.push({ r: rect(a, top(i), w, end(i) - top(i)), vertical: true });
    if (i < k - 1) streets.push({ r: rect(Math.min(a, xs[i + 1]!), ends[i]!, Math.abs(xs[i + 1]! - a) + w, w), vertical: false });
    // The corner of a cross street goes to the side the street steps away from.
    const leftFrom = i === 0 ? r.y : a < xs[i - 1]! ? ends[i - 1]! : top(i);
    const leftTo = i === k - 1 ? bottom : a < xs[i + 1]! ? ends[i]! + w : end(i);
    const rightFrom = i === 0 ? r.y : a > xs[i - 1]! ? ends[i - 1]! : top(i);
    const rightTo = i === k - 1 ? bottom : a > xs[i + 1]! ? ends[i]! + w : end(i);
    parts.push(rect(r.x, leftFrom, a - r.x, leftTo - leftFrom), rect(a + w, rightFrom, right - a - w, rightTo - rightFrom));
  }
  return { parts, streets };
}

/** One street across `r`, running north–south (the caller transposes for the other axis). */
function cutStreet(r: Rect, w: number, depth: number, rng: Rng, rules: SplitRules): { parts: Rect[]; streets: Road[] } {
  const most = rules.bends?.[Math.min(depth, rules.bends.length - 1)] ?? 0;
  if (most >= 2 && rng.next() < (rules.bend?.[Math.min(depth, rules.bend.length - 1)] ?? 0)) {
    const bent = bentStreet(r, w, rng.range(Math.max(2, most - 1), most), rng, rules);
    if (bent) return bent;
  }
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
 * each, in line with the street, never at a crossing. `more`: a further chance
 * of a wreck where new cover may stand (the second look: the captures' streets
 * are full of them).
 */
function parkCars(c: Canvas, roads: Road[], rng: Rng, chance: number, more = 0): void {
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
        const roll = rng.next();
        if (roll > chance + more) continue;
        const car = [at(lane, t), at(lane, t + 1)];
        // Past `chance`, the second look's extra wrecks: only where new cover may stand (roomForCover).
        if (roll > chance && !car.every((p) => roomForCover(c, p, 2))) continue;
        const kerb = [-1, 0, 1, 2].map((k) => at(curb, t + k));
        const clear = [at(lane, t - 1), at(lane, t + 2), at(inner, t), at(inner, t + 1)];
        if (!car.every((p) => c.free(p.x, p.y) && c.groundAt(p.x, p.y) === ROAD)) continue;
        if (kerb.some((p) => c.groundAt(p.x, p.y) === ROAD)) continue;
        // Where houses stand on the street, never across a door or a window.
        if (kerb.some((p) => ['door', 'window'].includes(c.get(p.x, p.y)))) continue;
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
      // Never in front of a door or a window (a way in), or right against a facade's opening.
      if (c.get(q.x, q.y) === 'door' || c.get(q.x, q.y) === 'window' || c.get(q.x, q.y) === 'floor' && c.keep[c.i(q.x, q.y)]) continue;
      put(c, p, kind, [what, s]);
    }
  }
}

/** `n` positions from `a` to `b` (inclusive) at an even spacing of about `step`, symmetric. */
function spaced(a: number, b: number, step: number): number[] {
  const n = Math.max(1, Math.round((b - a) / step));
  return Array.from({ length: n + 1 }, (_, k) => a + Math.round((k * (b - a)) / n));
}

/** The squad's corner (its spawns are the free tiles nearest it), and the corner the evacuation is carved in. */
const DEPLOY_AREA = 12;
const EVAC_AREA = 14;
/** Tiles along the map's edge kept clear, where the dead come in. */
const EDGE_CLEAR = 2;

/**
 * Whether new cover (a square's trees, a lot's rubble, the extra wrecks in
 * the streets: the second look against Synty's captures) may stand on `p`:
 * not in the squad's corner nor where the evacuation is carved, not by the
 * map's edge, and nothing but floor and cover within `clear` tiles (walls,
 * doors, windows, ladders). With 2, a tile always lies between it and a door
 * or the foot of the ladder a building may get later against its wall.
 */
function roomForCover(c: Canvas, p: Vec2, clear: number): boolean {
  if (p.x < DEPLOY_AREA && p.y < DEPLOY_AREA) return false;
  if (p.x >= c.width - EVAC_AREA && p.y < EVAC_AREA) return false;
  if (p.x < EDGE_CLEAR || p.y < EDGE_CLEAR || p.x >= c.width - EDGE_CLEAR || p.y >= c.height - EDGE_CLEAR) return false;
  for (let dy = -clear; dy <= clear; dy++) {
    for (let dx = -clear; dx <= clear; dx++) {
      const k = c.get(p.x + dx, p.y + dy);
      if (c.in(p.x + dx, p.y + dy) && k !== 'floor' && k !== 'low' && k !== 'high') return false;
    }
  }
  return true;
}

/**
 * What the second look added to a square or a car park that is neither the
 * squad's nor the objective's (the captures' squares are planted and
 * littered): on a square, two to four trees by its corners, a tile in from
 * its edges and a tile clear of the fountain; on either, a heap or two of
 * rubble, two tiles in from the edges (never on the row it opens onto the
 * street by). Placed by position (mix): it draws nothing from the generator's
 * rng, so the rest of a seed's layout only changes where it takes a tile.
 */
function plantAndLitter(c: Canvas, r: Rect, trees: boolean): void {
  const m = centre(r);
  const cx = Math.floor(m.x);
  const cy = Math.floor(m.y);
  if (trees && r.w >= 9 && r.h >= 9) {
    const want = 2 + (mix(r.x, r.y) % 3);
    const corners: Vec2[] = [
      { x: r.x + 1, y: r.y + 1 },
      { x: r.x + r.w - 2, y: r.y + 1 },
      { x: r.x + 1, y: r.y + r.h - 2 },
      { x: r.x + r.w - 2, y: r.y + r.h - 2 },
    ].sort((a, b) => mix(a.x, a.y) - mix(b.x, b.y));
    let planted = 0;
    for (const corner of corners) {
      if (planted >= want) break;
      // The corner's tile, or one a step or two further in (planters stand on some).
      const inward = { x: Math.sign(cx - corner.x), y: Math.sign(cy - corner.y) };
      for (const [u, v] of [[0, 0], [1, 0], [0, 1], [1, 1], [2, 0], [0, 2]] as const) {
        const p = { x: corner.x + inward.x * u, y: corner.y + inward.y * v };
        // The fountain spans the middle 3 × 3: a tile clear of it.
        if (Math.max(Math.abs(p.x - cx), Math.abs(p.y - cy)) < 3 || !roomForCover(c, p, 2)) continue;
        if (group(c, p, [[0, 0, 'high', ['tree']]])) {
          planted++;
          break;
        }
      }
    }
  }
  const heaps = 1 + (mix(r.y, r.x) % 2);
  const spots = tilesIn(rect(r.x + 2, r.y + 2, r.w - 4, r.h - 4)).sort((a, b) => mix(a.x, a.y) - mix(b.x, b.y));
  let n = 0;
  for (const p of spots) {
    if (n >= heaps) break;
    const pair = mix(p.y, p.x) % 2 === 0 && p.x + 1 < r.x + r.w - 2;
    if (!roomForCover(c, p, 2) || (pair && !roomForCover(c, add(p, { x: 1, y: 0 }), 2))) continue;
    if (group(c, p, pair ? [[0, 0, 'low', ['rubble']], [1, 0, 'low', ['rubble']]] : [[0, 0, 'low', ['rubble']]])) n++;
  }
}

/**
 * A square: a fountain with a statue in the middle, planters mirrored around
 * it on a regular grid and a kiosk or two by the edges; `extras`, its trees
 * and rubble (plantAndLitter), all but on the squad's and the objective's.
 */
function plaza(c: Canvas, r: Rect, rng: Rng, extras = true): void {
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
  if (extras) plantAndLitter(c, r, true);
}

/** Car park: bays across the lot facing an aisle, two rows when deep enough, and a booth; `extras`, its rubble (plantAndLitter), but on the objective's. */
function parking(c: Canvas, r: Rect, rng: Rng, front: number, extras = true): void {
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
  if (extras) plantAndLitter(c, r, false);
}

/** A row of houses or shops on the front of a lot, and another one on the back when the lot is deep. */
function rowOfBuildings(c: Canvas, lot: Rect, front: number, roads: boolean[], rng: Rng, style: Style, plan: Plan, furnish: Furnish): Building[] {
  const L = sideLen(lot, front);
  const D = depthOf(lot, front);
  const back = opposite(front);
  // A second row along the back when the lot is deep enough, back to back across an alley or the yards
  // between them. The houses take the depth there is: up to two tiles over the map's own in two rows, three in one.
  const two = D >= 2 * 7 + 1;
  const hd = two ? clamp(Math.floor((D - 1) / 2), 7, style.houseDepth + 2) : Math.min(D, style.houseDepth + 3);
  const plot = Math.min(style.house, L);
  if (plot < 7 || hd < 7) return [];
  const rows = two ? [front, back] : [front];
  // A single row of houses with room to spare stands back from the street behind a front garden.
  const setback = plan === 'rooms' && rows.length === 1 && D - hd >= 4 ? 2 : 0;
  const out: Building[] = [];
  const n = Math.max(1, Math.floor((L + style.gap) / (plot + style.gap)));
  // The row fills its side: what is left over widens the houses, up to two tiles each (four for a house on its own).
  const hw = plot + Math.min(n === 1 ? 4 : 2, Math.floor((L - (n * plot + (n - 1) * style.gap)) / n));
  for (const side of rows) {
    // In an old town every plot is its own width, with alleys of one or two between them.
    const widths = Array.from({ length: n }, () => (style.organic ? plot + rng.range(0, 3) : hw));
    const gaps = Array.from({ length: n - 1 }, () => (style.organic ? rng.range(1, 2) : style.gap));
    const sum = () => widths.reduce((a, b) => a + b, 0) + gaps.reduce((a, b) => a + b, 0);
    while (sum() > L) {
      const k = widths.indexOf(Math.max(...widths));
      const g = gaps.indexOf(Math.max(...gaps));
      if (widths[k]! > 7) widths[k]!--;
      else gaps[g]!--;
    }
    const spare = L - sum();
    let u = Math.floor(spare / 2) + (spare > 1 ? rng.range(-1, 1) : 0);
    u = clamp(u, 0, spare);
    // A long row keeps a garden in one of its middle plots now and then, and a house row may end in a corner shop.
    const garden = n >= 3 && rng.next() < 0.15 ? rng.range(1, n - 2) : -1;
    const shop = plan === 'rooms' && rng.next() < 0.3 ? rng.pick([0, n - 1]) : -1;
    for (let k = 0; k < n; k++) {
      const w = widths[k]!;
      // Not every house lines up with the next: one now and then stands back from the street (more, and further, in an
      // old town), on the room a single row has to spare behind it, or shallower.
      const room = rows.length === 1 ? D - hd : 0;
      const most = Math.min(style.organic ? 2 : 1, room + hd - 7);
      const back = !setback && most > 0 && rng.next() < (style.organic ? 0.5 : 0.15) ? rng.range(1, most) : 0;
      const b = local(lot, side, u, setback + back, w, hd - Math.max(0, back - room));
      if (k === garden) groupNear(c, centre(b), rng, [dressed(CORNER, 'bush', 'tree'), dressed(PAIR, 'bush', 'tree')], 1, 0, 6);
      else if (k === shop) out.push(building(c, local(lot, side, u, 0, w, hd), rng, 'shop', side, style, 'shop'));
      else {
        out.push(building(c, b, rng, plan, side, style, furnish));
        if (setback) frontGarden(c, lot, side, u, w, rng);
      }
      u += w + (gaps[k] ?? 0);
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
  // Deep enough to fill the lot (a shop floor and a stockroom), or two rows back to back across an alley.
  const two = D >= 2 * 7 + 1;
  const d = two ? clamp(Math.floor((D - 1) / 2), 7, 10) : clamp(D - rng.range(0, 1), 7, 12);
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
    // What is left of the row's room widens its shops, up to 9 tiles each.
    for (let extra = room - Math.max(7, total), k = 0; extra > 0 && widths.some((w) => w < 9); k = (k + 1) % widths.length) {
      if (widths[k]! < 9) {
        widths[k]!++;
        extra--;
      }
    }
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
    // An alley to the next row, a tile wide, now and then two.
    u = x + 1 + (rng.next() < 0.7 ? 1 : 2);
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
  // The shop is a kiosk: one storey.
  b.storeys = 1;
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
  const wing: Building = { outer: b, rooms, furnish: 'home', storeys: 1, flat: true };
  c.buildings.push(wing);
  return wing;
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

/** How wide the road along side `s` of `r` is (its narrowest stretch), 0 if none. */
function roadWidth(c: Canvas, r: Rect, s: number): number {
  const d = DIRS4[s]!;
  let width = Infinity;
  for (let u = 0; u < sideLen(r, s); u++) {
    let p = add(localPt(r, s, u, 0), d);
    let n = 0;
    while (c.in(p.x, p.y) && c.groundAt(p.x, p.y) === ROAD) {
      n++;
      p = add(p, d);
    }
    if (n) width = Math.min(width, n);
  }
  return width === Infinity ? 0 : width;
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

/**
 * A town block's buildable part, and the sides it keeps a sidewalk on: both
 * sides of an avenue; one side of a street three tiles wide in a grid (the
 * north or west one), one now and then in an old town; none on a lane (the
 * houses stand right on it) or on the map's edge. Against the block behind,
 * the east and south sides give up a tile for the alley between them, the
 * other block builds up to it.
 */
function townLot(c: Canvas, block: Rect, roads: boolean[], organic: boolean): { lot: Rect; walks: boolean[] } {
  let lot = block;
  const walks = [false, false, false, false];
  for (let s = 0; s < 4; s++) {
    const q = add(localPt(block, s, 0, 0), DIRS4[s]!);
    if (!c.in(q.x, q.y)) continue;
    const street = roads[s] ? roadWidth(c, block, s) : 0;
    const pave = !roads[s] ? s === 1 || s === 2 : street >= 4 || (street === 3 && (organic ? mix(block.x + s, block.y) % 3 === 0 : s === 1 || s === 2));
    if (!pave) continue;
    lot = cut(lot, s, 1);
    walks[s] = roads[s]!;
  }
  return { lot, walks };
}

/** A long lot is often split in two, back to back with an alley (`alley` wide, else 1–2) between them; each half is at least `min` long. */
function splitLot(lot: Rect, roads: boolean[], rng: Rng, min: number, chance: number, alley?: number): Lot[] {
  const alongX = lot.w >= lot.h;
  const span = alongX ? lot.w : lot.h;
  const gap = alley ?? rng.range(1, 2);
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

/** Piece `r` of lot `l`: it keeps the lot's roads on the sides it shares with it. */
function piece(l: Lot, r: Rect): Lot {
  const o = l.r;
  return { r, roads: [r.y === o.y && l.roads[0]!, r.x + r.w === o.x + o.w && l.roads[1]!, r.y + r.h === o.y + o.h && l.roads[2]!, r.x === o.x && l.roads[3]!] };
}

/**
 * The end of a lot nearest `to`, about `area` tiles (7 to 11 deep), and the
 * rest of it beyond a one-tile alley; null when the rest would be too small
 * to build on.
 */
function endOf(l: Lot, to: Vec2, area: number): [Lot, Lot] | null {
  const m = centre(l.r);
  const s = l.r.w >= l.r.h ? (to.x > m.x ? 1 : 3) : to.y > m.y ? 2 : 0;
  const across = sideLen(l.r, s);
  const depth = depthOf(l.r, s);
  const n = Math.min(clamp(Math.round(area / across), 7, 11), depth - 8);
  if (n < 7) return null;
  return [piece(l, local(l.r, s, 0, 0, across, n)), piece(l, local(l.r, s, 0, n + 1, across, depth - n - 1))];
}

/**
 * A square `yard` tiles across in a corner of a lot (0 north-west, then
 * clockwise: north-east, south-east, south-west), and the lots left round it
 * beyond a one-tile alley: the rest of its width beside the square and the
 * rest of its depth beyond it, when they are 7 or more across (else the
 * square takes that whole side).
 */
function cornerLots(lot: Lot, corner: number, yard: number): { square: Rect; lots: Lot[] } {
  const r = lot.r;
  const east = corner === 1 || corner === 2;
  const south = corner >= 2;
  const wide = r.w >= yard + 8;
  const deep = r.h >= yard + 8;
  const sw = wide ? yard : r.w;
  const sh = deep ? yard : r.h;
  const square = rect(east ? r.x + r.w - sw : r.x, south ? r.y + r.h - sh : r.y, sw, sh);
  const lots: Lot[] = [];
  if (wide) lots.push(piece(lot, rect(east ? r.x : r.x + yard + 1, r.y, r.w - yard - 1, r.h)));
  if (deep) lots.push(piece(lot, rect(square.x, south ? r.y : r.y + yard + 1, sw, r.h - yard - 1)));
  return { square, lots };
}

/** A small square on a corner of an old town's block: a fountain, or trees and a bench, and a bin by the kerb. */
function plazuela(c: Canvas, r: Rect, rng: Rng): void {
  const m = centre(r);
  if (rng.next() < 0.4) {
    const rim = (k: number): Look => ['fountainCorner', k];
    group(c, { x: Math.floor(m.x), y: Math.floor(m.y) }, [[0, 0, 'low', rim(0)], [1, 0, 'low', rim(1)], [0, 1, 'low', rim(3)], [1, 1, 'low', rim(2)]]);
  } else {
    groupNear(c, m, rng, [dressed(CORNER, 'bush', 'tree'), dressed(PAIR, 'planter', 'tree')], 1, 0, 6);
  }
  const side = rng.int(4);
  groupNear(c, localPt(r, side, Math.floor(sideLen(r, side) / 2), 1), rng, [dressed(PAIR, 'bin', 'kiosk'), [[0, 0, 'low', ['bin']]]], 1, 0, 4);
}

// -------------------------------------------------------------------- city

type Role = 'indoor' | 'open';
type CityProgram = 'houses' | 'shops' | 'highStreet' | 'office' | 'flats' | 'warehouse' | 'plaza' | 'parking' | 'gasStation' | 'motel' | 'site' | 'yard';

function weighted<T>(rng: Rng, options: [T, number][]): T {
  const total = options.reduce((s, [, w]) => s + w, 0);
  let roll = rng.next() * total;
  for (const [item, w] of options) {
    roll -= w;
    if (roll < 0) return item;
  }
  return options[options.length - 1]![0];
}

/** `used` counts the programs already placed: besides the squad's square and the objective, two open lots at most (a square, a car park, a site, a filling station, a motel), one of each. */
function cityProgram(lot: Rect, rng: Rng, role: Role | null, used: Map<CityProgram, number>, organic = false, industrial = false): CityProgram {
  const short = Math.min(lot.w, lot.h);
  const long = Math.max(lot.w, lot.h);
  if (industrial) return industrialProgram(lot, rng, role, used);
  if (role === 'indoor') return short >= 10 && rng.next() < 0.6 ? 'office' : rng.next() < 0.5 ? 'shops' : 'houses';
  if (role === 'open') return rng.next() < 0.55 ? 'plaza' : 'parking';
  // Open lots stay small and few: a big empty block reads as a hole in the city.
  const area = lot.w * lot.h;
  // Two at most on a map, besides the squad's square and the objective's.
  const few = (['plaza', 'parking', 'site', 'gasStation', 'motel'] as const).reduce((n, p) => n + (used.get(p) ?? 0), 0) < 2;
  const options: [CityProgram, number][] = [];
  if (few && !used.get('plaza') && area <= 130) options.push(['plaza', 0.35]);
  if (few && !used.get('parking') && area <= 160) options.push(['parking', 0.35]);
  // Smaller buildings, more of them (Synty's captures: rows of shops, a filling station, a motel, houses with gardens);
  // an old town is mostly houses and small shops, a few offices, flats and warehouses.
  const big = organic ? 0.45 : 1;
  if (short >= 7) options.push(['houses', organic ? 4 : 2.5], ['shops', 1], ['highStreet', organic ? 1.2 : 2.5]);
  if (short >= 10) options.push(['office', 2 * big]);
  if (short >= 9 && long >= 10) options.push(['flats', 2 * big]);
  if (short >= 12) options.push(['warehouse', 1.2 * big]);
  if (few && !used.get('gasStation') && short >= 10 && long >= 13 && area <= 180) options.push(['gasStation', 0.25]);
  if (few && !used.get('motel') && short >= 11 && long >= 13) options.push(['motel', 0.9]);
  if (few && !used.get('site') && short >= 8 && area <= 150) options.push(['site', 0.4]);
  if (!options.length) options.push(['parking', 1]);
  const pick = weighted(rng, options);
  used.set(pick, (used.get(pick) ?? 0) + 1);
  return pick;
}

/** An industrial estate's lot (playtest 2026-10-14, #25): mostly warehouses and container yards, a few offices and workshops. */
function industrialProgram(lot: Rect, rng: Rng, role: Role | null, used: Map<CityProgram, number>): CityProgram {
  const short = Math.min(lot.w, lot.h);
  if (role === 'indoor') return short >= 12 ? 'warehouse' : short >= 10 ? 'office' : 'shops';
  if (role === 'open') return 'yard';
  const options: [CityProgram, number][] = [];
  if (short >= 12) options.push(['warehouse', 3]);
  if (short >= 10) options.push(['office', 0.6]);
  if (short >= 6) options.push(['yard', 1.5]);
  if (short >= 7) options.push(['shops', 0.4]);
  if (!used.get('parking')) options.push(['parking', 0.4]);
  if (!used.get('site') && short >= 8) options.push(['site', 0.3]);
  if (!options.length) options.push(['parking', 1]);
  const pick = weighted(rng, options);
  used.set(pick, (used.get(pick) ?? 0) + 1);
  return pick;
}

/** What fills the rest of a lot beside or behind something else: a row of houses or shops; on an industrial estate, a yard. */
function filler(rng: Rng, style: Style): CityProgram {
  const houses = rng.next() < 0.5;
  return style.industrial ? 'yard' : houses ? 'houses' : 'highStreet';
}

/**
 * A container yard (playtest 2026-10-14, #25): asphalt, rows of shipping
 * containers lying along the street, two deep with a lane of two between rows
 * and a gap between neighbours, now and then one missing. Most are fenced
 * along the street with barbed wire, a gate of two in the middle between posts.
 */
function containerYard(c: Canvas, lot: Rect, front: number, rng: Rng): void {
  const L = sideLen(lot, front);
  const D = depthOf(lot, front);
  for (const p of tilesIn(lot)) if (c.in(p.x, p.y) && c.groundAt(p.x, p.y) === OPEN) c.ground[c.i(p.x, p.y)] = YARD;
  const fenced = L >= 8 && D >= 6 && rng.next() < 0.6;
  for (let v = fenced ? 2 : 1; v + 3 <= D; v += 4) {
    for (let u = 1; u + 6 <= L; u += 6) {
      if (rng.next() < 0.3) continue;
      const box = local(lot, front, u, v, 5, 2);
      // Drawn 2 × 5 with its doors north: turned to lie along the street, its doors to one end.
      const q = front % 2 === 0 ? (rng.int(2) === 0 ? 1 : 3) : rng.int(2) === 0 ? 0 : 2;
      const parts = turn(CONTAINER, q);
      const x0 = Math.min(...parts.map((t) => t[0]));
      const y0 = Math.min(...parts.map((t) => t[1]));
      group(c, { x: box.x - x0, y: box.y - y0 }, parts);
    }
  }
  if (!fenced) return;
  const gate = Math.floor(L / 2) - 1;
  for (let u = 0; u < L; u++) {
    if (u === gate || u === gate + 1) continue;
    const post = u === 0 || u === L - 1 || u === gate - 1 || u === gate + 2;
    put(c, localPt(lot, front, u, 0), post ? 'high' : 'low', [post ? 'gatePost' : 'wire', front]);
  }
}

/**
 * Makes room beside a building `w` wide on the front of a lot: when what is
 * left over along the front is wide enough, the building's part of the lot is
 * at one end and a row of houses or shops fills the rest, beyond a one-tile
 * alley. Returns the building's part (the whole lot if not) and that row.
 */
function alongside(c: Canvas, lot: Lot, front: number, w: number, rng: Rng, style: Style): { own: Rect; row: Building[] } {
  const L = sideLen(lot.r, front);
  const D = depthOf(lot.r, front);
  if (L - w - 1 < 7) return { own: lot.r, row: [] };
  const first = rng.int(2) === 0;
  const rest = piece(lot, local(lot.r, front, first ? w + 1 : 0, 0, L - w - 1, D));
  return { own: local(lot.r, front, first ? 0 : L - w, 0, w, D), row: cityLot(c, rest.r, filler(rng, style), rest.roads, rng, style) };
}

/** How deep and how long along the street the open programs go: bigger lots build up the rest, beyond an alley. */
const OPEN_SIZE: Partial<Record<CityProgram, [number, number]>> = { plaza: [10, 12], parking: [10, 14], site: [10, 12], gasStation: [11, 15], motel: [13, 15] };

/** Fills one city lot. Returns the walled buildings (the objective may go in one). */
function cityLot(c: Canvas, lot: Rect, program: CityProgram, roads: boolean[], rng: Rng, style: Style): Building[] {
  // An industrial estate has no homes: what would be houses or flats is a yard.
  if (style.industrial && (program === 'houses' || program === 'highStreet' || program === 'flats')) program = 'yard';
  const front = frontOf(c, lot, roads, rng);
  const L = sideLen(lot, front);
  const D = depthOf(lot, front);
  const open = OPEN_SIZE[program];
  if (open) {
    // Open ground stays small: past its depth the back of the lot is built up, past its length one end.
    const whole: Lot = { r: lot, roads };
    const [depth, span] = open;
    if (D - depth - 1 >= 7) {
      const ahead = piece(whole, local(lot, front, 0, 0, L, depth));
      const behind = piece(whole, local(lot, front, 0, depth + 1, L, D - depth - 1));
      const row = cityLot(c, behind.r, filler(rng, style), behind.roads, rng, style);
      return [...cityLot(c, ahead.r, program, ahead.roads, rng, style), ...row];
    }
    if (L - span - 1 >= 7) {
      const first = rng.int(2) === 0;
      const own = piece(whole, local(lot, front, first ? 0 : L - span, 0, span, D));
      const rest = piece(whole, local(lot, front, first ? span + 1 : 0, 0, L - span - 1, D));
      const row = cityLot(c, rest.r, filler(rng, style), rest.roads, rng, style);
      return [...cityLot(c, own.r, program, own.roads, rng, style), ...row];
    }
  }
  switch (program) {
    case 'office': {
      const w = Math.min(style.office, L);
      const d = Math.min(style.officeDepth, D);
      if (w < 10 || d < 10) return cityLot(c, lot, 'houses', roads, rng, style);
      const { own, row } = alongside(c, { r: lot, roads }, front, w, rng, style);
      const l = sideLen(own, front);
      const b = building(c, local(own, front, Math.floor((l - w) / 2), 0, w, d), rng, rng.next() < 0.65 ? 'corridor' : 'open', front, style, 'office');
      // Behind it a row of houses back to back, or a car park, or bins and crates out the back; planters and trees on the sides.
      const behind = piece({ r: lot, roads }, local(own, front, 0, d + 1, l, D - d - 1));
      if (D - d - 1 >= 7) row.push(...cityLot(c, behind.r, 'houses', behind.roads, rng, style));
      else if (D - d >= 6) parking(c, behind.r, rng, opposite(front));
      else if (D - d >= 3) groupNear(c, localPt(own, front, Math.floor(l / 2), d + 1), rng, [dressed(BARRICADE, 'bin'), dressed(PAIR, 'bin', 'crates')], 2);
      for (const u of [1, l - 2]) if (Math.floor((l - w) / 2) >= 3) groupNear(c, localPt(own, front, u, Math.floor(d / 2)), rng, [dressed(PAIR, 'planter', 'tree'), dressed(CORNER, 'planter', 'tree')], 1);
      return [b, ...row];
    }
    case 'warehouse': {
      const w = Math.min(style.hall, L);
      const d = Math.min(style.hall - 1, D);
      if (w < 12 || d < 12) return cityLot(c, lot, 'office', roads, rng, style);
      const { own, row } = alongside(c, { r: lot, roads }, front, w, rng, style);
      const l = sideLen(own, front);
      // The hall sits at the back, its loading bay onto a yard of containers and crates.
      const yard = D - d;
      const b = building(c, local(own, front, Math.floor((l - w) / 2), yard, w, d), rng, 'hall', front, style, 'store');
      if (yard >= 5) {
        const box = local(own, front, 1, 1, 2, 5);
        const fits = tilesIn(rect(box.x - 1, box.y - 1, box.w + 2, box.h + 2)).every((p) => c.free(p.x, p.y) || !c.in(p.x, p.y));
        if (yard >= 7 && fits && rng.next() < 0.6) platform(c, box, rng, 1);
        groupNear(c, localPt(own, front, l - 4, 2), rng, [dressed(CRATES, 'crates')], 1);
      } else if (yard >= 3) {
        groupNear(c, localPt(own, front, 2, 1), rng, [dressed(CRATES, 'crates'), dressed(BARRICADE, 'crates')], 1);
      }
      return [b, ...row];
    }
    case 'flats': {
      const w = clamp(L - 2, 8, 12);
      const d = clamp(D - 3, 7, 10);
      if (w > L || d > D) return cityLot(c, lot, 'houses', roads, rng, style);
      // A row beside them when the lot is long (the terrace then always looks onto the courtyard).
      const { own, row } = D - d >= 2 ? alongside(c, { r: lot, roads }, front, w + 2, rng, style) : { own: lot, row: [] };
      const l = sideLen(own, front);
      const u0 = Math.floor((l - w) / 2);
      const b = local(own, front, u0, 0, w, d);
      // The terrace looks onto the courtyard behind, or onto a side passage.
      const back = opposite(front);
      const terrace = D - d >= 2 ? back : u0 >= 2 ? (front + 1) % 4 : front;
      flats(c, b, rng, terrace);
      // A hedge and a tree in the courtyard.
      if (D - d >= 4) groupNear(c, localPt(own, front, Math.floor(l / 2) + rng.range(-3, 3), D - 2), rng, [dressed(BARRICADE, 'bush'), dressed(PAIR, 'bush', 'tree')], 1);
      return row;
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
    case 'yard':
      containerYard(c, lot, front, rng);
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

/**
 * City: blocks of houses, shops, offices, flats and warehouses between
 * streets, a square or a car park here and there. Half the towns are laid out
 * on a grid; the other half are old towns, with streets that bend and run on
 * a slant, smaller and uneven blocks, houses that don't line up and small
 * squares on the corners.
 */
function city(c: Canvas, rng: Rng, map: Style, role: Role, clinic = false, hospital = false, ward = false): Rect {
  const S = c.width;
  const organic = rng.next() < 0.5 && !map.industrial && !hospital;
  const style: Style = { ...map, organic };
  // Small blocks: an avenue, streets and lanes two tiles wide (on a lane the houses stand right on it).
  const plan = district(
    rect(0, 0, S, S),
    rng,
    hospital
      ? { minBlock: 19, maxBlock: 26, widths: [[3, 4], [3, 3], [2, 3]], jog: 0.1, deadEnd: 0.1, stop: [0, 0.2, 0.7, 1] }
      : organic
      ? { minBlock: 8, maxBlock: 18, widths: [[3, 4], [2, 3], [2, 3]], jog: 0.3, deadEnd: 0.35, stop: [0, 0.15, 0.55, 0.9], bend: [1, 0.6, 0.4], bends: [4, 3, 2] }
      : map.industrial
        ? // An industrial estate: big blocks and wide streets for the lorries.
          { minBlock: 13, maxBlock: 24, widths: [[4, 4], [3, 4], [3, 3]], jog: 0.2, deadEnd: 0.2, stop: [0, 0.3, 0.75, 0.95] }
        : { minBlock: 9, maxBlock: 20, widths: [[4, 4], [3, 3], [2, 2]], jog: 0.35, deadEnd: 0.3, stop: [0, 0.2, 0.6, 0.9] },
  );
  for (const road of plan.roads) c.paint(road.r, ROAD);
  const far = { x: S * 0.76, y: S * 0.76 };
  const spawnBlock = plan.blocks.find((b) => inRect(b, { x: 0, y: 0 }))!;
  // The objective's block: the one nearest the far corner, a big one (an office fits) if it is nearly as far.
  const objBlock =
    best(
      plan.blocks.filter((b) => b !== spawnBlock && Math.min(b.w, b.h) >= (hospital ? 19 : ward ? 11 : 9)),
      (b) => dist(centre(b), far) - (Math.min(b.w, b.h) >= (role === 'indoor' ? 12 : 11) ? 3 : 0),
    ) ?? best(plan.blocks, (b) => (b === spawnBlock ? Infinity : dist(centre(b), far)))!;
  let objective: Rect = objBlock;
  const used = new Map<CityProgram, number>();
  let squares = 0;
  for (const block of plan.blocks) {
    const roads = [0, 1, 2, 3].map((s) => facesRoad(c, block, s));
    const { lot, walks } = townLot(c, block, roads, organic);
    const lots: Lot[] = [];
    if (block === spawnBlock) {
      // The squad's corner: an open square, with buildings round it if the block is big (on a small block, a narrower square).
      const corner = cornerLots({ r: lot, roads }, 0, lot.w >= 16 || lot.h >= 16 ? 8 : 6);
      if (style.industrial) parking(c, corner.square, rng, 2, false);
      else plaza(c, corner.square, rng, false);
      lots.push(...corner.lots);
    } else if (block !== objBlock && inRect(block, { x: S - 1, y: 0 })) {
      // The other corner on the squad's side keeps a yard for the evac zone (generateLayout carves it there), a few crates in it.
      const corner = [6, 5].map((yard) => cornerLots({ r: lot, roads }, 1, yard)).find((k) => k.lots.length) ?? cornerLots({ r: lot, roads }, 1, 6);
      groupNear(c, centre(corner.square), rng, [dressed(CRATES, 'crates'), dressed(PAIR, 'crates')], 1, 0, 6);
      lots.push(...corner.lots);
    } else if (organic && block !== objBlock && squares < 2 && lot.w >= 13 && lot.h >= 13 && rng.next() < 0.5) {
      // An old town's small square, on the block's corner at a crossroads if it has one.
      const corners = [0, 1, 2, 3].filter((k) => roads[k] && roads[(k + 3) % 4]);
      const corner = cornerLots({ r: lot, roads }, corners.length ? rng.pick(corners) : rng.int(4), lot.w >= 14 && lot.h >= 14 ? 6 : 5);
      plazuela(c, corner.square, rng);
      for (const l of corner.lots) lots.push(...splitLot(l.r, l.roads, rng, 8, 0.5, 1));
      squares++;
    } else if (hospital && block === objBlock) {
      lots.push({ r: lot, roads });
    } else {
      lots.push(...splitLot(lot, roads, rng, 8, block === objBlock && role === 'indoor' ? 0 : 0.65, 1));
    }
    const objLot = block === objBlock ? best(lots, (l) => dist(centre(l.r), far)) : undefined;
    for (const l of lots) {
      if (l === objLot && role === 'open') {
        // The objective's square or car park: the far end of its lot, the rest built up.
        const [open, rest] = endOf(l, far, 120) ?? [l, null];
        const program = cityProgram(open.r, rng, role, used, false, style.industrial);
        if (program === 'yard') containerYard(c, open.r, frontOf(c, open.r, open.roads, rng), rng);
        else if (program === 'plaza') plaza(c, open.r, rng, false);
        else parking(c, open.r, rng, frontOf(c, open.r, open.roads, rng), false);
        if (rest) cityLot(c, rest.r, cityProgram(rest.r, rng, null, used, organic, style.industrial), rest.roads, rng, style);
        objective = open.r;
        continue;
      }
      const isObj = l === objLot;
      if (isObj && (clinic || hospital)) {
        const front = frontOf(c, l.r, l.roads, rng);
        const across = Math.min(sideLen(l.r, front), hospital ? 12 : 11);
        const depth = Math.min(depthOf(l.r, front), hospital ? 22 : ward ? 13 : 11);
        const footprint = local(l.r, front, Math.floor((sideLen(l.r, front) - across) / 2), 0, across, depth);
        const b = building(c, footprint, rng, 'rooms', front, style, 'clinic', hospital ? 'hospital' : ward ? 'ward' : 'clinic');
        b.storeys = hospital ? 3 : 1;
        b.medicalWear = hospital ? 0.85 : ward ? 0.45 : 0.08;
        objective = b.outer;
        continue;
      }
      const built = cityLot(c, l.r, cityProgram(l.r, rng, isObj ? role : null, used, organic, style.industrial), l.roads, rng, style);
      if (isObj) objective = best(built, (b) => dist(centre(b.outer), far))?.outer ?? l.r;
    }
    sidewalk(c, block, walks, style.industrial ? style.furniture * 3 : style.furniture, 'low');
  }
  // An industrial estate's streets are emptier: its cover is the containers.
  parkCars(c, plan.roads, rng, style.industrial ? 0.2 : 0.4, style.industrial ? 0.05 : 0.15);
  if (!style.industrial) checkpoint(c, plan.roads, rng);
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
 * tiles away (`maxGap` at most).
 */
function siteNear(c: Canvas, target: Vec2, lu: number, ld: number, toTrack: number[], rng: Rng, radius = 9, maxGap = 5): { r: Rect; front: number } | null {
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
        if (gap > maxGap) continue;
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
  woods(c, rng, objective ? centre(objective) : far);
  return objective ?? rect(Math.round(far.x) - 6, Math.round(far.y) - 6, 12, 12);
}

/** A wrecked lorry (2 × 4), drawn nose north. */
const TRUCK: Part[] = dressed(tilesIn(rect(0, 0, 2, 4)).map((p): Part => [p.x, p.y, 'high']), 'truck');
const LOG_PILES: Part[][] = [dressed([[0, 0, 'low'], [1, 0, 'low']], 'logPile'), dressed(tilesIn(rect(0, 0, 2, 2)).map((p): Part => [p.x, p.y, 'low']), 'logPile')];
const WATER_TOWER: Part[] = dressed(tilesIn(rect(0, 0, 2, 2)).map((p): Part => [p.x, p.y, 'high']), 'waterTower');
/** A shipping container (2 × 5), drawn doors north. */
const CONTAINER: Part[] = dressed(tilesIn(rect(0, 0, 2, 5)).map((p): Part => [p.x, p.y, 'high']), 'container');
/** A fallen wire fence, five tiles in a row. */
const WIRE: Part[] = dressed(tilesIn(rect(0, 0, 5, 1)).map((p): Part => [p.x, p.y, 'low']), 'wire');
/** A broken wooden fence: four posts and a gap. */
const BROKEN_FENCE: Part[] = dressed([[0, 0, 'low'], [1, 0, 'low'], [3, 0, 'low'], [4, 0, 'low']], 'fence');

/**
 * The service area's road: straight stretches and right-angle bends, from an
 * edge near the squad's corner (like trackLine) to one of the far edges, with
 * one or two bends. Returns its middle line, tile by tile.
 */
function roadLine(S: number, rng: Rng): Vec2[] {
  const fromTop = rng.int(2) === 0;
  const start = fromTop ? { x: rng.range(5, 9), y: 0 } : { x: 0, y: rng.range(5, 9) };
  const toBottom = rng.int(2) === 0;
  const end = toBottom ? { x: rng.range(Math.round(S * 0.55), S - 7), y: S - 1 } : { x: S - 1, y: rng.range(Math.round(S * 0.55), S - 7) };
  const turn = rng.range(Math.round(S * 0.3), Math.round(S * 0.55));
  // Down from the top (or right from the left) to the bend, then across, then on to the end.
  const corners =
    fromTop
      ? toBottom
        ? [start, { x: start.x, y: turn }, { x: end.x, y: turn }, end]
        : [start, { x: start.x, y: end.y }, end]
      : toBottom
        ? [start, { x: end.x, y: start.y }, end]
        : [start, { x: turn, y: start.y }, { x: turn, y: end.y }, end];
  const out: Vec2[] = [{ ...start }];
  for (let k = 1; k < corners.length; k++) {
    const to = corners[k]!;
    const p = { ...out[out.length - 1]! };
    while (p.x !== to.x || p.y !== to.y) {
      p.x += Math.sign(to.x - p.x);
      if (p.x === to.x) p.y += Math.sign(to.y - p.y);
      out.push({ ...p });
    }
  }
  return out;
}

/**
 * The service area on a forest road (playtest 2026-10-14, #25, after Synty's
 * Woodland Apocalypse): by a road through the pines, a filling station, a
 * diner and a motel, the water tower, a sawmill with its log piles and lorries,
 * the woodcutter's statue and the big signs. The cover is what such a place
 * has: lorries, log piles, pumps, fences. The objective is the diner (indoors)
 * or the forecourt (in the open).
 */
function roadside(c: Canvas, rng: Rng, style: Style, role: Role): Rect {
  const S = c.width;
  const track = roadLine(S, rng);
  // A real road, three tiles wide.
  const onTrack: number[] = [];
  for (const p of track) {
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        const x = p.x + dx;
        const y = p.y + dy;
        if (!c.in(x, y) || c.keep[c.i(x, y)] || c.ground[c.i(x, y)] === ROAD) continue;
        c.ground[c.i(x, y)] = ROAD;
        c.reserve(x, y);
        onTrack.push(c.i(x, y));
      }
    }
  }
  const toTrack = distanceField(c, onTrack);
  const at = (t: number) => track[Math.min(track.length - 1, Math.round((track.length - 1) * t))]!;
  // The first place along the road, in the order of `ts`, with room for it.
  const lot = (ts: number[], lu: number, ld: number, radius = 10, maxGap = 5) => {
    for (const t of ts) {
      const found = siteNear(c, at(t), lu, ld, toTrack, rng, radius, maxGap);
      if (found) return found;
    }
    return null;
  };
  // Forecourts, yards and car parks are asphalt.
  const pave = (r: Rect) => {
    for (const p of tilesIn(r)) if (c.in(p.x, p.y) && c.groundAt(p.x, p.y) === OPEN) c.ground[c.i(p.x, p.y)] = YARD;
  };
  let objective: Rect | null = null;
  // The filling station, its forecourt onto the road, and the woodcutter by it.
  const station = lot([0.62, 0.55, 0.7], 15, 11);
  if (station) {
    gasStation(c, station.r, station.front, rng, style);
    pave(station.r);
    if (role === 'open') objective = station.r;
    groupNear(c, add(centre(station.r), { x: rng.range(-8, 8), y: rng.range(-8, 8) }), rng, [[[0, 0, 'high', ['statue']]]], 2, 0, 10);
  }
  // The diner beside it: one storey, the counter and the tables.
  const dinerSite = lot([0.72, 0.66, 0.8], 9, 7);
  if (dinerSite) {
    const diner = building(c, dinerSite.r, rng, 'shop', dinerSite.front, style, 'shop');
    diner.storeys = 1;
    if (role === 'indoor') objective = diner.outer;
    groupNear(c, centre(dinerSite.r), rng, [WATER_TOWER], 9, 0, 30);
  }
  // The sawmill on the near stretch: the shed at the back, a yard of logs and lorries onto the road.
  const mill = lot([0.42, 0.5, 0.33, 0.22], 13, 16) ?? lot([0.42, 0.3, 0.55], 13, 13);
  if (mill) {
    const d = depthOf(mill.r, mill.front);
    const hd = d >= 16 ? 10 : 9;
    building(c, local(mill.r, mill.front, 0, d - hd, 13, hd), rng, 'hall', mill.front, style, 'store');
    const yard = local(mill.r, mill.front, 0, 0, 13, d - hd);
    pave(yard);
    for (let k = 0; k < 3; k++) groupNear(c, localPt(mill.r, mill.front, rng.range(1, 11), rng.range(1, Math.max(1, d - hd - 2))), rng, LOG_PILES, 2, 0, 8);
    groupNear(c, centre(yard), rng, [TRUCK], 3, 0, 12);
  }
  // The motel further on, its car park onto the road.
  // Off the road down a drive if there's no room on it.
  const motelSite = lot([0.84, 0.92, 0.76, 0.5, 0.3, 0.15], 15, 13, 12) ?? lot([0.84, 0.92, 0.6, 0.4, 0.2], 13, 12, 14, 9);
  if (motelSite) {
    const wings = motel(c, motelSite.r, motelSite.front, rng);
    pave(motelSite.r);
    if (role === 'indoor') objective ??= wings[0]?.outer ?? null;
  }
  // Behind the place, a broken fence and a fallen one.
  const behind = [motelSite, dinerSite, station, mill].find((l) => !!l);
  if (behind) {
    const back = localPt(behind.r, behind.front, Math.floor(sideLen(behind.r, behind.front) / 2), depthOf(behind.r, behind.front) + 3);
    groupNear(c, back, rng, [BROKEN_FENCE], 4, 0, 16);
    groupNear(c, back, rng, [WIRE], 6, 0, 16);
  }
  // Lorries left on the shoulder, and the big signs by the road.
  for (const t of [0.3, 0.78]) groupNear(c, at(t), rng, [TRUCK], 4, 0, 16);
  for (const t of [0.22, 0.55, 0.92]) {
    const near = tilesIn(rect(at(t).x - 4, at(t).y - 4, 9, 9)).filter((p) => c.in(p.x, p.y) && toTrack[c.i(p.x, p.y)] === 2 && c.free(p.x, p.y));
    const p = near.length ? rng.pick(near) : null;
    if (!p) continue;
    const towards = DIRS4.findIndex((d) => c.in(p.x + d.x, p.y + d.y) && toTrack[c.i(p.x + d.x, p.y + d.y)]! < 2);
    group(c, p, [[0, 0, 'high', ['sign', Math.max(0, towards)]]]);
  }
  // A cabin on the way, sometimes in ruins.
  const cabinSite = lot([0.25, 0.15], style.house, style.houseDepth);
  if (cabinSite) {
    const cabin = building(c, cabinSite.r, rng, 'rooms', cabinSite.front, style, 'home');
    if (rng.next() < 0.35) ruin(c, cabin, rng);
  }
  const centreAt = objective ? centre(objective) : at(0.7);
  woods(c, rng, centreAt, { hills: 1, outcrops: 4, logs: 3 });
  return objective ?? rect(Math.round(centreAt.x) - 6, Math.round(centreAt.y) - 6, 12, 12);
}

/**
 * The country around: hills, groves where the noise is high (kept away from
 * the squad, the evacuation and `objCentre`), rock outcrops and fallen logs.
 */
function woods(c: Canvas, rng: Rng, objCentre: Vec2, most = { hills: 3, outcrops: 8, logs: 4 }): void {
  const S = c.width;
  // Hills.
  let hills = 0;
  for (let k = 0; k < 40 && hills < most.hills; k++) if (hill(c, { x: rng.range(8, S - 9), y: rng.range(8, S - 9) }, rng)) hills++;
  // Groves where the noise is high, kept away from the squad, the evacuation and the objective.
  const big = valueNoise(S, 10, rng);
  const small = valueNoise(S, 4, rng);
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
  for (let k = 0; k < 160 && outcrops.length < most.outcrops; k++) {
    const p = { x: rng.range(2, S - 3), y: rng.range(2, S - 3) };
    if (value(p.x, p.y) > T - 0.06 || outcrops.some((o) => dist(o, p) < 7) || Math.hypot(p.x, p.y) < 6) continue;
    if (group(c, p, turn(rng.pick(OUTCROPS), rng.int(4)))) outcrops.push(p);
  }
  for (let k = 0, logs = 0; k < 80 && logs < most.logs; k++) {
    const p = { x: rng.range(2, S - 3), y: rng.range(2, S - 3) };
    const v = value(p.x, p.y);
    if (v < T - 0.1 || v > T - 0.02) continue;
    if (group(c, p, turn(dressed(rng.pick([BARRICADE, [[0, 0, 'low'], [1, 0, 'low']] as Part[]]), 'log'), rng.int(4)))) logs++;
  }
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

/** Loose cover the XCOM days left everywhere (playtest 2026-10-14, #25): crates, barricades, bins and planters out in the streets and yards. */
const CLUTTER: ReadonlySet<DecorKind> = new Set<DecorKind>(['crates', 'barricade', 'bin', 'planter']);
/** The share of those groups that stays: not all of them go. */
const CLUTTER_KEPT = 0.5;

/**
 * Takes away half of the loose clutter outdoors, a whole group at a time (the
 * pieces touching each other), on its own seed. Cars, trees, walls and what is
 * indoors or up on a deck stay.
 */
function thinClutter(c: Canvas, seed: number): void {
  const rng = new Rng(seed ^ 0x7468696e);
  const loose = (i: number) =>
    (c.tiles[i] === 'low' || c.tiles[i] === 'high') && c.ground[i] !== INDOOR && c.elev[i] === 0 && CLUTTER.has(decorOf(c.decor[i])?.kind ?? 'tree');
  const seen = new Set<number>();
  for (let i = 0; i < c.tiles.length; i++) {
    if (seen.has(i) || !loose(i)) continue;
    const group = [i];
    seen.add(i);
    for (let k = 0; k < group.length; k++) {
      const x = group[k]! % c.width;
      const y = Math.floor(group[k]! / c.width);
      for (const d of DIRS8) {
        if (!c.in(x + d.x, y + d.y)) continue;
        const j = c.i(x + d.x, y + d.y);
        if (!seen.has(j) && loose(j)) {
          seen.add(j);
          group.push(j);
        }
      }
    }
    if (rng.next() < CLUTTER_KEPT) continue;
    for (const j of group) {
      c.force(j % c.width, Math.floor(j / c.width), 'floor');
      c.decor[j] = 0;
    }
  }
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

/** Tiles of map for each special loot box, and the most a map has (docs/FASE8.md §B: three at most, by the map's size). */
const TILES_PER_BOX = 750;
const MAX_BOXES = 3;

/**
 * The special loot boxes (docs/FASE8.md §B): up to MAX_BOXES, one per
 * TILES_PER_BOX of map, each of a different kind, inside buildings, far from
 * the squad's corner and from each other, where they close no way (group).
 * On a seed of their own, so the rest of the layout doesn't move.
 */
function placeLootBoxes(c: Canvas, seed: number): void {
  const rng = new Rng(seed ^ 0x626f78);
  const count = Math.min(MAX_BOXES, Math.floor((c.width * c.height) / TILES_PER_BOX));
  const kinds = shuffle(['lootWeapons', 'lootMedical', 'lootAmmo', 'lootFood'] as DecorKind[], rng).slice(0, count);
  // Inside buildings first; where there aren't enough (the outskirts, a facility), out in yards and lots.
  const open = Array.from({ length: c.tiles.length }, (_, i) => ({ x: i % c.width, y: Math.floor(i / c.width) })).filter(
    (p) => c.get(p.x, p.y) === 'floor' && c.free(p.x, p.y) && Math.hypot(p.x, p.y) >= 14,
  );
  const spots = [...shuffle(open.filter((p) => c.groundAt(p.x, p.y) === INDOOR), rng), ...shuffle(open.filter((p) => c.groundAt(p.x, p.y) !== INDOOR), rng)];
  const placed: Vec2[] = [];
  for (const kind of kinds) {
    const spot = spots.find((p) => placed.every((q) => dist(p, q) >= 12) && group(c, p, [[0, 0, 'low', [kind]]]));
    if (spot) placed.push(spot);
  }
}

/** The bus: two tiles wide, eight long (a Town bus at the map's scale). */
const BUS_W = 2;
const BUS_L = 8;

/**
 * Parks the bus beside the 3 × 3 evac `zone` (docs/PRUEBA-2026-10-14.md P1):
 * along one of its sides, out in the open, its long side to the zone (the
 * doors), on open ground only. It is solid (`high`, decor 'bus') and drives
 * off along its length toward the nearer map edge. Null if no side has room.
 */
function parkBus(c: Canvas, zone: Vec2[], rng: Rng): { tiles: Vec2[]; heading: number } | null {
  const x0 = Math.min(...zone.map((p) => p.x));
  const y0 = Math.min(...zone.map((p) => p.y));
  // North, south (lying along x) and west, east (along y), beside the zone. Centred on it if it fits;
  // else slid along that side (a zone in a corner leaves no room on the map's side of it).
  const sides: { r: Rect; alongX: boolean; slide: number }[] = [];
  for (let slide = 0; slide <= BUS_L - 1; slide++) {
    for (const shift of slide ? [slide, -slide] : [0]) {
      const lead = Math.floor((BUS_L - 3) / 2) + shift;
      sides.push(
        { r: rect(x0 - lead, y0 - BUS_W, BUS_L, BUS_W), alongX: true, slide },
        { r: rect(x0 - lead, y0 + 3, BUS_L, BUS_W), alongX: true, slide },
        { r: rect(x0 - BUS_W, y0 - lead, BUS_W, BUS_L), alongX: false, slide },
        { r: rect(x0 + 3, y0 - lead, BUS_W, BUS_L), alongX: false, slide },
      );
    }
  }
  // The bus still has to touch the zone along at least three tiles (its doors open onto it).
  const touches = (o: { r: Rect; alongX: boolean }) => (o.alongX ? Math.min(o.r.x + o.r.w, x0 + 3) - Math.max(o.r.x, x0) : Math.min(o.r.y + o.r.h, y0 + 3) - Math.max(o.r.y, y0)) >= 3;
  const fits = (r: Rect) =>
    tilesIn(r).every((p) => c.in(p.x, p.y) && c.elevAt(p.x, p.y) === 0 && c.groundAt(p.x, p.y) !== INDOOR && c.free(p.x, p.y) && ['floor', 'low', 'high'].includes(c.get(p.x, p.y)!));
  const options = sides
    .filter((o) => touches(o) && fits(o.r))
    .map((o) => ({ ...o, cost: tilesIn(o.r).filter((p) => c.get(p.x, p.y) !== 'floor').length + o.slide * 0.3 + rng.next() * 0.5 }));
  const pick = best(options, (o) => o.cost);
  if (!pick) return null;
  const tiles = tilesIn(pick.r);
  for (const p of tiles) {
    c.force(p.x, p.y, 'high');
    c.dress(p, 'bus');
    c.reserve(p.x, p.y);
  }
  // Off toward the nearer end of the map along its length.
  const mid = centre(pick.r);
  const heading = pick.alongX ? (mid.x < c.width / 2 ? 3 : 1) : mid.y < c.height / 2 ? 0 : 2;
  return { tiles, heading };
}

// ------------------------------------------------------- storeys and lights

/**
 * How tall each building stands, in storeys (docs/ALTURA-Y-NOCHE.md), by the
 * rule the client drew them with: in town, shops and offices two or three,
 * some houses two; halls, kiosks, motels and everything out of town, one.
 * Every tile of a building (walls, doors, windows, the inside) gets its
 * storeys; a wall two buildings share, the taller one's. Rolled on a seed of
 * their own, so nothing else in the layout moves.
 */
function storeysOf(c: Canvas, heights: number[]): number[] {
  const out = new Array<number>(c.tiles.length).fill(0);
  c.buildings.forEach((b, j) => {
    for (const p of tilesIn(b.outer)) if (c.in(p.x, p.y)) out[c.i(p.x, p.y)] = Math.max(out[c.i(p.x, p.y)]!, heights[j]!);
  });
  return out;
}

/** How many storeys each building of c.buildings stands (storeysOf's rule), on its own seed. */
function buildingStoreys(c: Canvas, biome: Biome, seed: number): number[] {
  const rng = new Rng(seed ^ 0x5f0e75);
  return c.buildings.map((b) => {
    const roll = rng.next();
    return (
      b.storeys ??
      (biome !== 'city' ? 1 : b.furnish === 'shop' ? (roll < 0.55 ? 2 : 3) : b.furnish === 'office' ? (roll < 0.6 ? 2 : 3) : b.furnish === 'home' && roll < 0.3 ? 2 : 1)
    );
  });
}

// ------------------------------------------------------ storeys above the ground

// docs/FASE6-PLAN.md, step 5. Every building gets its storeys for real, on
// canvases of their own laid over the ground: walls, rooms, windows, furniture
// and stairs up from the floor below; on top, its roof. Houses and barns have
// a pitched roof nobody stands on (decision 2); the rest a flat one, with a
// parapet, reached by the stairs when the building has two or three storeys,
// and by a ladder up its side on about half of those of one (decision 1). A
// ruin has no roof. Drawn on a seed of its own, after everything else: the
// ground only changes where stairs and ladders go.

/** A storey above the ground while it is drawn: what stands on it, and where it has a slab at all. */
interface Storey {
  c: Canvas;
  slab: boolean[];
}

/** A pitched roof nobody stands on: houses and barns (decision 2). */
const pitched = (b: Building) => !b.flat && (b.furnish === 'home' || b.furnish === 'barn');

/** What the rooms of a building's upper storeys hold: a shop's stockroom, the rest as below. */
const UPSTAIRS: Record<Furnish, Furnish> = { home: 'home', shop: 'store', office: 'office', lab: 'lab', store: 'store', barn: 'barn', bare: 'bare', clinic: 'clinic' };

/** From one window to the next on an upper storey: shops and offices glazed, houses less. */
const upstairsWindows = (b: Building) => (b.furnish === 'home' ? 3 : 2);

/**
 * Windows along the outer walls of storey `lc` of building `b`, like windows()
 * on the ground: never at a corner, beside another opening or where a wall
 * meets it from inside, and never onto another building's storey (`occupied`).
 */
function upperWindows(lc: Canvas, b: Rect, every: number, occupied: boolean[]): void {
  for (let s = 0; s < 4; s++) {
    const n = sideLen(b, s);
    const anchor = Math.floor((n - 1) / 2);
    const back = DIRS4[opposite(s)]!;
    const lateral = DIRS4[(s + 1) % 4]!;
    for (let u = 2; u <= n - 3; u++) {
      if (Math.abs(u - anchor) % every !== 0) continue;
      const p = localPt(b, s, u, 0);
      const q = add(p, DIRS4[s]!);
      if (lc.get(p.x, p.y) !== 'wall' || lc.get(p.x + back.x, p.y + back.y) !== 'floor') continue;
      if (![add(p, lateral), { x: p.x - lateral.x, y: p.y - lateral.y }].every((r) => lc.get(r.x, r.y) === 'wall' || lc.get(r.x, r.y) === 'window')) continue;
      if (!lc.in(q.x, q.y) || occupied[lc.i(q.x, q.y)]) continue;
      lc.force(p.x, p.y, 'window');
    }
  }
}

/**
 * Whether every place one can stand on, on storey `st` within `r`, joins up
 * with `from` (orthogonal steps; windows are no way through).
 */
function storeyJoined(st: Storey, r: Rect, from: Vec2): boolean {
  const open = (p: Vec2) => inRect(r, p) && st.slab[st.c.i(p.x, p.y)]! && isWalkable(st.c.get(p.x, p.y)) && st.c.get(p.x, p.y) !== 'window';
  if (!open(from)) return false;
  const seen = new Set<number>([st.c.i(from.x, from.y)]);
  const queue = [from];
  while (queue.length) {
    const p = queue.pop()!;
    for (const d of DIRS4) {
      const q = add(p, d);
      if (!open(q) || seen.has(st.c.i(q.x, q.y))) continue;
      seen.add(st.c.i(q.x, q.y));
      queue.push(q);
    }
  }
  return tilesIn(r).every((p) => !open(p) || seen.has(st.c.i(p.x, p.y)));
}

/**
 * Clears the furniture that cuts storey `st` of building `r` in two, nearest
 * to what `from` reaches first, until every place one can stand on there joins
 * up again (furnishRoom keeps doorways clear, not narrow rooms).
 */
function unblockStorey(st: Storey, r: Rect, from: Vec2): void {
  for (let guard = 0; guard < 20 && !storeyJoined(st, r, from); guard++) {
    const blocked = tilesIn(inset(r, 1)).find((p) => {
      const k = st.c.get(p.x, p.y);
      return (k === 'low' || k === 'high') && DIRS4.some((d) => isWalkable(st.c.get(p.x + d.x, p.y + d.y)));
    });
    if (!blocked) return;
    st.c.force(blocked.x, blocked.y, 'floor');
    st.c.decor[st.c.i(blocked.x, blocked.y)] = 0;
  }
}

/** Clear only clinical cover at the edge of an isolated pocket, including after a stairwell is opened above it. */
function clearMedicalAisles(st: Storey, r: Rect, from: Vec2): void {
  const c = st.c, cells = tilesIn(inset(r, 1));
  const open = (p: Vec2) => inRect(r, p) && st.slab[c.i(p.x, p.y)] && isWalkable(c.get(p.x, p.y)) && c.get(p.x, p.y) !== 'window';
  for (let guard = 0; guard < r.w * r.h; guard++) {
    const seen = new Set<number>(), queue = [from];
    while (queue.length) {
      const p = queue.pop()!, i = c.i(p.x, p.y);
      if (seen.has(i) || !open(p)) continue;
      seen.add(i);
      for (const d of DIRS4) queue.push(add(p, d));
    }
    const isolated = cells.filter((p) => open(p) && !seen.has(c.i(p.x, p.y)));
    if (!isolated.length) return;
    const cover = cells.filter((p) => st.slab[c.i(p.x, p.y)] && ['low', 'high'].includes(c.get(p.x, p.y)) && DIRS4.some((d) => seen.has(c.i(p.x + d.x, p.y + d.y))));
    const p = best(cover, (p) => Math.min(...isolated.map((q) => Math.abs(p.x - q.x) + Math.abs(p.y - q.y))));
    if (!p) return;
    c.force(p.x, p.y, 'floor'); c.decor[c.i(p.x, p.y)] = 0;
  }
}

/**
 * Stairs up through building `r` from the floor below (`lower`: the ground
 * canvas, or a storey) to storey `upper`: away from walls on both floors,
 * with free floor behind its foot and at its landing, and a hole above the
 * flight (the stairwell). Never where the hole would cut that storey in two.
 * Returns the landing, or null if no place fits.
 */
function placeStairs(lower: Canvas, lowerSlab: boolean[] | null, upper: Storey, r: Rect, rng: Rng, avoid: Set<number>, zones?: Rect[]): Vec2 | null {
  const inner = inset(r, 1);
  const options: { at: Vec2; up: number; key: number }[] = [];
  for (const at of tilesIn(inner)) {
    if (zones?.length && !zones.some((r) => inRect(r, at))) continue;
    const i = lower.i(at.x, at.y);
    if (!lower.free(at.x, at.y) || lower.decor[i] || avoid.has(i) || (lowerSlab && !lowerSlab[i]) || upper.c.get(at.x, at.y) !== 'floor' || upper.c.keep[i]) continue;
    // A full tile between the flight and any wall, on both storeys.
    if (!DIRS4.every((d) => {
      const p = add(at, d);
      const j = lower.i(p.x, p.y);
      return inRect(inner, p) && ['floor', 'low', 'high'].includes(lower.get(p.x, p.y)) && (!lowerSlab || lowerSlab[j]) && upper.slab[j] && upper.c.get(p.x, p.y) === 'floor';
    })) continue;
    DIRS4.forEach((d, up) => {
      const landing = add(at, d);
      if (!inRect(inner, landing) || upper.c.get(landing.x, landing.y) !== 'floor' || upper.c.keep[upper.c.i(landing.x, landing.y)]) return;
      const back = { x: at.x - d.x, y: at.y - d.y };
      if (lower.get(back.x, back.y) !== 'floor' || lower.decor[lower.i(back.x, back.y)]) return;
      options.push({ at, up, key: rng.next() });
    });
  }
  options.sort((a, b) => a.key - b.key);
  for (const { at, up } of options) {
    const i = lower.i(at.x, at.y);
    const landing = add(at, DIRS4[up]!);
    upper.slab[i] = false;
    if (!storeyJoined(upper, r, landing)) {
      upper.slab[i] = true;
      continue;
    }
    lower.force(at.x, at.y, 'ladder');
    lower.dress(at, 'stairs', up);
    lower.reserve(at.x, at.y);
    const back = { x: at.x - DIRS4[up]!.x, y: at.y - DIRS4[up]!.y };
    lower.reserve(back.x, back.y);
    upper.c.reserve(at.x, at.y);
    for (const d of DIRS4) upper.c.reserve(at.x + d.x, at.y + d.y);
    return landing;
  }
  return null;
}

/**
 * A ladder up the side of building `r`'s one-storey flat roof: on open ground
 * the squad can reach, against a plain stretch of wall, clear of the doors.
 * The roof's parapet opens where it comes up. False if nowhere fits.
 */
function placeLadder(c: Canvas, roof: Storey, r: Rect, rng: Rng, avoid: Set<number>, reach: Set<number>): boolean {
  const spots: { wall: Vec2; foot: Vec2 }[] = [];
  for (let s = 0; s < 4; s++) {
    for (let u = 2; u <= sideLen(r, s) - 3; u++) {
      const wall = localPt(r, s, u, 0);
      const foot = add(wall, DIRS4[s]!);
      const lateral = DIRS4[(s + 1) % 4]!;
      const alongWall = [wall, add(wall, lateral), { x: wall.x - lateral.x, y: wall.y - lateral.y }];
      const i = c.i(foot.x, foot.y);
      if (!c.in(foot.x, foot.y) || !c.free(foot.x, foot.y) || c.decor[i] || c.groundAt(foot.x, foot.y) === INDOOR || !reach.has(i) || avoid.has(i)) continue;
      if (!alongWall.every((p) => c.get(p.x, p.y) === 'wall')) continue;
      if (DIRS8.some((d) => c.get(foot.x + d.x, foot.y + d.y) === 'door')) continue;
      spots.push({ wall, foot });
    }
  }
  const spot = spots.length ? rng.pick(spots) : null;
  if (!spot) return false;
  c.force(spot.foot.x, spot.foot.y, 'ladder');
  c.reserve(spot.foot.x, spot.foot.y);
  roof.c.force(spot.wall.x, spot.wall.y, 'floor');
  return true;
}

/**
 * The storeys above the ground of every building on `c` (`heights`: how many
 * each stands), bottom up, and where a group of the living could be posted
 * up there (`perch`: a few places together on an upper floor, far from
 * `squad`). `avoid`: ground tiles already taken (spawns, pods, objective, evac,
 * lights); `reach`: what the squad reaches on the ground.
 */
function upperFloors(c: Canvas, heights: number[], seed: number, avoid: Set<number>, reach: Set<number>, squad: Vec2): { storeys: Storey[]; perch: Pos[] } {
  const rng = new Rng(seed ^ 0x57a125);
  const area = c.width * c.height;
  const all = c.buildings
    .map((b, j) => ({ b, n: heights[j]!, walkable: false, reached: 0, landing: null as Vec2 | null, rooms: [] as Rect[] }))
    .filter((e) => !e.b.ruined && e.n >= 1);
  // Flat roofs: reached by the stairs over two or three storeys; over one, by a ladder half the time.
  for (const e of all) e.walkable = !pitched(e.b) && (e.n >= 2 || rng.next() < 0.5);
  const top = Math.max(0, ...all.map((e) => e.n));
  const storeys: Storey[] = Array.from({ length: top }, () => ({ c: new Canvas(c.width, c.height), slab: new Array<boolean>(area).fill(false) }));
  for (let k = 1; k <= top; k++) {
    const st = storeys[k - 1]!;
    const lc = st.c;
    const occupied = new Array<boolean>(area).fill(false);
    for (const e of all) if (e.n >= k) for (const p of tilesIn(e.b.outer)) if (c.in(p.x, p.y)) occupied[c.i(p.x, p.y)] = true;
    // The roofs of those that end here, then the walls of those that go on (a wall two share stands over the lower one's roof).
    for (const e of all.filter((x) => x.n === k)) {
      for (const p of tilesIn(e.b.outer)) {
        if (!c.in(p.x, p.y)) continue;
        lc.force(p.x, p.y, e.walkable ? 'floor' : 'roof');
        st.slab[c.i(p.x, p.y)] = true;
      }
    }
    const going = all.filter((x) => x.n > k);
    for (const e of going) {
      const r = e.b.outer;
      for (const p of tilesIn(r)) {
        if (!c.in(p.x, p.y)) continue;
        const edge = p.x === r.x || p.y === r.y || p.x === r.x + r.w - 1 || p.y === r.y + r.h - 1;
        lc.force(p.x, p.y, edge ? 'wall' : 'floor');
        lc.ground[c.i(p.x, p.y)] = INDOOR;
        st.slab[c.i(p.x, p.y)] = true;
      }
    }
    for (const e of going) {
      e.rooms = [];
      if (e.b.medicalKind) {
        e.rooms = medicalPlan(lc, e.b.outer, e.b.front!, rng, k, e.b.medicalKind === 'hospital', e.b.medicalKind === 'ward', [null, null, null, null]);
      } else splitRooms(lc, inset(e.b.outer, 1), rng, e.rooms, e.b.furnish === 'home' ? 6 : 8, e.b.furnish !== 'home');
      upperWindows(lc, e.b.outer, upstairsWindows(e.b), occupied.map((o, i) => o && !inRect(e.b.outer, { x: i % c.width, y: Math.floor(i / c.width) })));
    }
    // Up from the storey below: to each storey of those that go on, and to the flat roofs over two or three.
    const lower = k === 1 ? c : storeys[k - 2]!.c;
    const lowerSlab = k === 1 ? null : storeys[k - 2]!.slab;
    for (const e of all) {
      e.landing = null;
      // No way up to the storey below: none here either.
      if (!(e.n > k || (e.n === k && k >= 2 && e.walkable)) || e.reached !== k - 1) continue;
      e.landing = placeStairs(lower, lowerSlab, st, e.b.outer, rng, k === 1 ? avoid : new Set(), e.b.medicalKind === 'hospital' ? e.b.medicalRooms?.filter((r) => r.use === 'stairs') : undefined);
      if (e.landing) e.reached = k;
    }
    for (const e of going) {
      if (!e.landing) continue;
      if (e.b.medicalKind) {
        for (const room of e.rooms as MedicalRoom[]) furnishMedical(lc, room, rng, e.b.medicalKind === 'clinic' ? undefined : e.b.medicalKind);
        e.b.medicalRooms!.push(...e.rooms as MedicalRoom[]);
      } else for (const room of e.rooms) furnishRoom(lc, room, rng, UPSTAIRS[e.b.furnish]);
      if (e.b.medicalKind) clearMedicalAisles(st, e.b.outer, e.landing);
      else unblockStorey(st, e.b.outer, e.landing);
    }
    // A flat roof one walks on has a parapet round it (half cover); one nobody reaches is just a roof.
    for (const e of all.filter((x) => x.n === k && x.walkable)) {
      const reached = k === 1 || !!e.landing;
      const r = e.b.outer;
      for (const p of tilesIn(r)) {
        if (!c.in(p.x, p.y) || lc.get(p.x, p.y) !== 'floor') continue;
        const edge = p.x === r.x || p.y === r.y || p.x === r.x + r.w - 1 || p.y === r.y + r.h - 1;
        if (!reached) lc.force(p.x, p.y, 'roof');
        else if (edge) lc.force(p.x, p.y, 'low');
      }
      if (reached && k === 1 && !placeLadder(c, st, r, rng, avoid, reach)) {
        for (const p of tilesIn(r)) if (c.in(p.x, p.y) && st.slab[c.i(p.x, p.y)] && (lc.get(p.x, p.y) === 'floor' || lc.get(p.x, p.y) === 'low')) lc.force(p.x, p.y, 'roof');
      }
    }
    // Storeys nobody can reach (no room for stairs): only a roof over them.
    for (const e of going) {
      if (e.landing) continue;
      for (const p of tilesIn(e.b.outer)) if (c.in(p.x, p.y) && st.slab[c.i(p.x, p.y)]) lc.force(p.x, p.y, 'roof');
    }
  }
  // The spare circulation bay becomes a furnished waiting area if no flight or landing uses it.
  for (const e of all.filter((e) => e.b.medicalKind === 'hospital')) for (const r of e.b.medicalRooms ?? []) {
    if (r.use !== 'stairs') continue;
    const st = r.z ? storeys[r.z - 1] : null;
    const floor = st?.c ?? c;
    const active = tilesIn(r).some((p) => decorOf(floor.decor[c.i(p.x, p.y)])?.kind === 'stairs' || (st && !st.slab[c.i(p.x, p.y)]));
    if (!active) {
      if (!r.z) for (const p of tilesIn(r)) if (avoid.has(c.i(p.x, p.y))) floor.reserve(p.x, p.y);
      r.use = 'waiting'; furnishCareRoom(floor, r, rng, 'hospital');
    }
  }
  // Later stairwells can isolate a tile behind a bed on the floor below.
  for (const e of all.filter((e) => e.b.medicalKind)) for (let k = 1; k < e.n; k++) {
    const lower = k === 1 ? c : storeys[k - 2]!.c;
    const i = lower.decor.findIndex((d, i) => decorOf(d)?.kind === 'stairs' && inRect(e.b.outer, { x: i % c.width, y: Math.floor(i / c.width) }));
    if (i < 0) continue;
    const d = DIRS4[decorOf(lower.decor[i])!.facing]!;
    clearMedicalAisles(storeys[k - 1]!, e.b.outer, { x: i % c.width + d.x, y: Math.floor(i / c.width) + d.y });
  }
  // Somewhere up there for a group of the living to be posted (decision 7): far from the squad, by a window.
  const perch: Pos[] = [];
  const upstairs = shuffle(
    all.filter((e) => e.n >= 2 && e.reached >= 1 && dist(centre(e.b.outer), squad) >= POD_DISTANCE),
    rng,
  );
  for (const e of upstairs) {
    const st = storeys[0]!;
    const stand = (p: Vec2) => inRect(e.b.outer, p) && st.slab[c.i(p.x, p.y)]! && canStandOn(st.c.get(p.x, p.y)) && !st.c.keep[c.i(p.x, p.y)];
    const byWindow = tilesIn(inset(e.b.outer, 1)).filter((p) => stand(p) && DIRS4.some((d) => st.c.get(p.x + d.x, p.y + d.y) === 'window'));
    const anchor = byWindow.length ? rng.pick(byWindow) : null;
    if (!anchor) continue;
    const seen = new Set<number>([c.i(anchor.x, anchor.y)]);
    const queue = [anchor];
    while (queue.length && perch.length < 4) {
      const p = queue.shift()!;
      if (stand(p)) perch.push({ ...p, z: 1 });
      for (const d of DIRS4) {
        const q = add(p, d);
        if (seen.has(c.i(q.x, q.y)) || !inRect(e.b.outer, q) || !st.slab[c.i(q.x, q.y)] || !isWalkable(st.c.get(q.x, q.y)) || st.c.get(q.x, q.y) === 'window') continue;
        seen.add(c.i(q.x, q.y));
        queue.push(q);
      }
    }
    break;
  }
  return { storeys, perch };
}

/** How far apart the map's lights stand: street lights in town, floodlights in a facility, a lamp now and then along a track. */
const LIGHT_SPACING: Record<Biome, number> = { city: 8, facility: 9, wilds: 15 };

/**
 * Where the map's lights stand (docs/ALTURA-Y-NOCHE.md): on the pavement
 * beside a street (beside a track in the wilds), LIGHT_SPACING apart. At night
 * they light NIGHT.streetLight tiles round them. Picked on a seed of their own.
 */
function streetLights(c: Canvas, biome: Biome, seed: number): Vec2[] {
  const rng = new Rng(seed ^ 0x11647);
  const kerb = (p: Vec2) =>
    c.get(p.x, p.y) === 'floor' &&
    c.elevAt(p.x, p.y) === 0 &&
    c.groundAt(p.x, p.y) === OPEN &&
    DIRS4.some((d) => c.in(p.x + d.x, p.y + d.y) && c.groundAt(p.x + d.x, p.y + d.y) === ROAD);
  const out: Vec2[] = [];
  for (const p of shuffle(tilesIn(rect(0, 0, c.width, c.height)).filter(kerb), rng)) {
    if (out.every((q) => dist(p, q) >= LIGHT_SPACING[biome])) out.push(p);
  }
  return out;
}

const NAMES: Record<Biome, string[]> = {
  city: ['Distrito Portuario', 'Barrio Viejo', 'Centro Financiero', 'Barrio de la Estación', 'Ensanche', 'Mercado Central', 'Zona Universitaria'],
  wilds: ['Bosque de Lirio', 'Valle Hondo', 'Granjas del Sur', 'Pinar Quemado', 'Ruinas de la ermita', 'Las afueras'],
  facility: ['Conservera del Norte', 'Nave 14', 'Planta química', 'Almacén frigorífico', 'Fábrica de piensos'],
};

/** Names of the scenarios' maps (Lore's to change). */
const SITE_NAMES: Record<Site, string[]> = {
  roadside: ['Área de servicio del Pinar', 'El aserradero viejo', 'Motel Los Abetos', 'La gasolinera del alto'],
  industrial: ['Polígono Este', 'Los muelles de carga', 'Naves del Sur', 'Polígono Norte'],
};

export interface GenOptions {
  biome: Biome;
  kind: MissionKind;
  /** Size of each pod, in the order the pod plan lists them. */
  podSizes: number[];
  seed: number;
  /** A recovery whose story puts the item out in the open (playtest 2026-10-14, #27): on a square or a car park, not in a room. */
  itemOutdoors?: boolean;
  /** The opening's medicines belong inside Aldren's furnished clinic. */
  clinic?: boolean;
  /** A hospital objective, including the rescue of Dr Nwosu. */
  hospital?: boolean;
  /** Explicit medical locations; ordinary houses keep their existing identity. */
  medical?: 'clinic' | 'ward' | 'hospital';
  /** A scenario other than the biome's usual one (playtest 2026-10-14, #25): 'roadside' in the wilds, 'industrial' in a city. */
  site?: Site;
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
  const clinic = biome === 'city' && ((kind === 'recovery' && !!opts.clinic) || opts.medical === 'clinic' || opts.medical === 'ward');
  const hospital = biome === 'city' && ((kind === 'rescue' && !!opts.hospital) || opts.medical === 'hospital');
  const ward = opts.medical === 'ward';
  const outdoorItem = kind === 'recovery' && !!opts.itemOutdoors && !clinic;
  const role: Role = clinic || hospital || (!outdoorItem && (kind === 'recovery' || kind === 'rescue' || (kind === 'hack' && biome !== 'wilds'))) ? 'indoor' : 'open';

  // Keep the squad's corner clear.
  for (let y = 0; y < 5; y++) for (let x = 0; x < 5; x++) c.reserve(x, y);
  const style = pickStyle(rng);
  // A scenario only where it belongs: the service area in the wilds, the industrial estate in a city.
  const site = (opts.site === 'roadside' && biome === 'wilds') || (opts.site === 'industrial' && biome === 'city') ? opts.site : undefined;
  const compound = biome === 'facility' ? facility(c, rng, style, role) : null;
  const objRect =
    compound?.objective ??
    (biome === 'city' ? city(c, rng, { ...style, industrial: site === 'industrial' }, role, clinic, hospital, ward) : site === 'roadside' ? roadside(c, rng, style, role) : wilds(c, rng, style, role));
  thinClutter(c, opts.seed);
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
  // The bus waits beside it, drawn on its own seed so nothing else in the layout moves.
  const bus = evacZone.length ? parkBus(c, evacZone, new Rng(opts.seed ^ 0x6275)) : null;
  placeLootBoxes(c, opts.seed);

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
    // Tucked away indoors, or for an item out in the open (#27) the most open tile outdoors.
    const outside = outdoorItem ? objTiles.filter((p) => c.groundAt(p.x, p.y) !== INDOOR) : [];
    // Door and window tiles also carry INDOOR. The clinic's objective must be
    // on actual room floor, with a tile between it and the exterior walls.
    const medicineRoom = clinic ? c.buildings.find((b) => b.outer === objRect)?.medicalRooms?.find((r) => r.use === 'equipment' || r.use === 'treatment') : undefined;
    const clinicFloor = clinic ? objTiles.filter((p) => inRect(inset(objRect, 2), p) && c.get(p.x, p.y) === 'floor' && c.groundAt(p.x, p.y) === INDOOR) : objTiles;
    const inMedicineRoom = medicineRoom ? clinicFloor.filter((p) => inRect(medicineRoom, p)) : [];
    const inside = inMedicineRoom.length ? inMedicineRoom : clinicFloor;
    const spot = (outdoorItem && outside.length ? best(outside, (p) => enclosed(p)) : best(inside, (p) => -enclosed(p) - (c.groundAt(p.x, p.y) === INDOOR ? 2 : 0))) ?? null;
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

  // Pods: spread over the map, never near the squad. What the map is made of never depends on how many
  // there are (a night's extra group, three times the dead: playtest 2026-10-14): what goes after keeps off
  // only what was taken before them.
  const beforePods = new Set(taken);
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
  const heights = buildingStoreys(c, biome, opts.seed);
  const tall = storeysOf(c, heights);
  const lamps = streetLights(c, biome, opts.seed);
  // The storeys above the ground (docs/FASE6-PLAN.md), last: stairs and ladders keep off what is already placed.
  const placed = new Set<number>([...beforePods, ...lamps.map((p) => c.i(p.x, p.y)), ...[item, terminal].filter((p): p is Vec2 => !!p).map((p) => c.i(p.x, p.y))]);
  const upper = upperFloors(c, heights, opts.seed, placed, reach, squadCenter);

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
  const storeys = new Array<number>(S * S);
  for (let y = 0; y < S; y++) {
    for (let x = 0; x < S; x++) {
      const q = T({ x, y });
      const i = c.i(x, y);
      tiles[q.y * S + q.x] = c.tiles[i]!;
      elev[q.y * S + q.x] = c.elev[i]!;
      ground[q.y * S + q.x] = c.ground[i]!;
      decor[q.y * S + q.x] = Dc(c.decor[i]!);
      storeys[q.y * S + q.x] = tall[i]!;
    }
  }
  const levels: Level[] = upper.storeys.map((st) => {
    const out: Level = { tiles: new Array<TileKind | null>(S * S), decor: new Array<Decor>(S * S) };
    for (let y = 0; y < S; y++) {
      for (let x = 0; x < S; x++) {
        const q = T({ x, y });
        const i = c.i(x, y);
        out.tiles[q.y * S + q.x] = st.slab[i] ? st.c.tiles[i]! : null;
        out.decor![q.y * S + q.x] = st.slab[i] ? Dc(st.c.decor[i]!) : 0;
      }
    }
    if (out.decor!.every((d) => !d)) delete out.decor;
    return out;
  });
  const t = (p: Vec2 | null) => (p ? T(p) : null);
  const medical: MedicalSite[] = c.buildings.flatMap((b) => {
    if (b.medicalWear === undefined) return [];
    const a = T(b.outer);
    const z = T({ x: b.outer.x + b.outer.w - 1, y: b.outer.y + b.outer.h - 1 });
    return [{ x: Math.min(a.x, z.x), y: Math.min(a.y, z.y), w: Math.abs(a.x - z.x) + 1, h: Math.abs(a.y - z.y) + 1, wear: b.medicalWear, kind: b.medicalKind, rooms: b.medicalRooms?.map((r) => {
      const a = T(r), z = T({ x: r.x + r.w - 1, y: r.y + r.h - 1 });
      return { x: Math.min(a.x, z.x), y: Math.min(a.y, z.y), w: Math.abs(a.x - z.x) + 1, h: Math.abs(a.y - z.y) + 1, z: r.z, use: r.use };
    }) }];
  });

  for (const b of medical) b.dressing = dressMedical(b, S, tiles, decor, levels, opts.seed, [...spawns, item, terminal, objectiveSpot].filter((p): p is Vec2 => !!p).map(T));

  return {
    id: `gen-${site ?? biome}-${opts.seed}`,
    name: rng.pick(site ? SITE_NAMES[site] : NAMES[biome]),
    biome,
    ...(site ? { site } : {}),
    width: S,
    height: S,
    tiles,
    elev,
    ground,
    ...(medical.length ? { medical } : {}),
    decor,
    storeys,
    ...(levels.length ? { levels } : {}),
    ...(upper.perch.length ? { perch: upper.perch.map((p) => ({ ...T(p), z: p.z })) } : {}),
    lights: lamps.map(T),
    spawns: spawns.map(T),
    pods: pods.map((p) => ({ tiles: p.tiles.map(T), patrol: p.patrol.map(T) })),
    item: t(item),
    terminal: t(terminal),
    objectiveSpot: t(objectiveSpot),
    evacZone: evacZone.map(T),
    ...(bus ? { evacBus: { tiles: bus.tiles.map(T), heading: D(bus.heading) } } : {}),
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
    // A map with storeys (docs/FASE6-PLAN.md): the storeys, their stairs and how tall each building stands.
    ...(map.decor ? { decor: map.decor } : {}),
    ...(map.levels ? { levels: map.levels, storeys: map.storeys } : {}),
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
