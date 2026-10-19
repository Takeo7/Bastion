import { decorCode, decorOf, objectAt } from './decor';
import { longFlightTile } from './flights';
import { DIRS4, DIRS8, isWalkable } from './grid';
import { stepsFrom } from './path';
import { Rng } from './rng';
import type { Decor, DecorKind, GameState, GroundKind, HomeSite, Level, Pos, TileKind, Vec2 } from './types';

// What stands on the floor takes its tile (Arturo, 19/10: «que ocupen»):
// anything drawn with a body to it (above the knee, or that one would walk
// round) stands where nobody walks or stops. The generator decides every such
// thing here, last, over the finished map: the armchairs, coffee tables,
// lamps, plants, bedside tables, towel rails and coat stands of the houses;
// a café's tables, a bookshop's or a library's reading tables, a grocer's
// drinks fridge; a barn's tools and wheelbarrow; the hydrants and mailboxes
// by the kerbs. Each is decor (decor.ts) on a `low` tile (the fridge `high`,
// as a kitchen's is), and the client only draws what is here
// (homeFurnishing.ts, dressing.ts). Flat things stay the client's and stay
// walkable: rugs, papers, stains, blood, small litter, the dead lying.
//
// Nothing goes where it would close a way: never by a door, a ladder or
// stairs, a window one may vault or a stair's landing, nor where anybody is
// placed, and only where its neighbours stay joined round it (keepsWays): so
// every room stays one, and everything reached before is reached still. A
// room keeps most of its floor (MAX_SHARE). On a seed of its own: the rest of
// the layout doesn't move.

/** A map being furnished, as the layout has it (its final frame): the ground and the storeys above it. */
export interface FurnishMap {
  width: number;
  height: number;
  tiles: TileKind[];
  elev: number[];
  decor: Decor[];
  levels: Level[];
  ground?: GroundKind[];
}

/** One level of the map: what stands on each tile, and what it is meant to be. */
interface Layer {
  tiles: (TileKind | null)[];
  decor: Decor[];
}

/** A building with furniture of its trade (the layout's, in its final frame): its bounds, walls included. */
export interface FurnishSite {
  kind: 'shop' | 'hall' | 'barn';
  x: number;
  y: number;
  w: number;
  h: number;
}

/** The furniture furnishing.ts puts on tiles of its own (the client draws each, as its own: homeFurnishing.ts, dressing.ts). */
export const FURNITURE_OF_ITS_OWN: ReadonlySet<DecorKind> = new Set<DecorKind>([
  'armchair',
  'coffeeTable',
  'floorLamp',
  'pottedPlant',
  'nightstand',
  'diningChair',
  'towelRack',
  'coatRack',
  'cafeTable',
  'readingTable',
  'drinksFridge',
  'wheelbarrow',
  'tools',
  'hydrant',
  'mailbox',
  'strewn',
]);

/** The most of a room's floor its furniture takes (playtest 12/10: rooms are 3 × 3 or more, never mazes). */
const MAX_SHARE = 0.4;
/** How far round a tile keepsWays looks for the way round it. */
const RADIUS = 4;
/** A kerb's hydrant or mailbox: the chance on each kerb tile, and how far from another (or a street light), by steps. */
const KERB_CHANCE = 0.035;
const KERB_GAP = 2;

/** What a door, a ladder, the stairs or a window is, and the tiles round them stay clear of. */
const WAYS: ReadonlySet<TileKind | null> = new Set<TileKind | null>(['door', 'doorOpen', 'ladder', 'window', 'windowBroken']);
/** What one walks across inside a room (loot.ts roomFloor): its floor and its stairs, not a door nor a window. */
const PASS: ReadonlySet<TileKind | null> = new Set<TileKind | null>(['floor', 'ladder']);

/**
 * Whether a blocking object on `p` of `layer` (on the ground, `elev`) keeps
 * every way across it: its open neighbours stay joined round it, within
 * RADIUS tiles on its floor, by floor (not through a door nor a window). Then
 * whatever was reached before is reached still, by the movement rules (a
 * diagonal past it needs both sides open: they are two of its neighbours) and
 * the rooms stay one (loot.ts roomAt). `p` may be the tiles of an object over
 * several (decor.ts objectTiles): the open neighbours of all of them stay
 * joined round the whole of it. A flight of stairs of several tiles (with the
 * layer's `decor`) is no way round: nobody steps off its sides.
 */
export function keepsWays(layer: { tiles: readonly (TileKind | null)[]; decor?: readonly Decor[] }, width: number, height: number, p: Vec2 | readonly Vec2[], elev?: readonly number[]): boolean {
  const at = (x: number, y: number) => y * width + x;
  const own: readonly Vec2[] = 'x' in p ? [p] : p;
  const first = own[0]!;
  const level = elev?.[at(first.x, first.y)] ?? 0;
  const taken = new Set(own.map((q) => at(q.x, q.y)));
  const x0 = Math.min(...own.map((q) => q.x)) - RADIUS;
  const x1 = Math.max(...own.map((q) => q.x)) + RADIUS;
  const y0 = Math.min(...own.map((q) => q.y)) - RADIUS;
  const y1 = Math.max(...own.map((q) => q.y)) + RADIUS;
  const pass = (x: number, y: number) =>
    x >= 0 && y >= 0 && x < width && y < height && x >= x0 && x <= x1 && y >= y0 && y <= y1 && !taken.has(at(x, y)) && PASS.has(layer.tiles[at(x, y)] ?? null) && (elev?.[at(x, y)] ?? 0) === level &&
    !longFlightTile(layer.tiles, layer.decor, width, at(x, y));
  const near: Vec2[] = [];
  for (const q of own) {
    for (const d of DIRS4) {
      const n = { x: q.x + d.x, y: q.y + d.y };
      if (pass(n.x, n.y) && !near.some((o) => o.x === n.x && o.y === n.y)) near.push(n);
    }
  }
  if (near.length <= 1) return true;
  const seen = new Set<number>([at(near[0]!.x, near[0]!.y)]);
  const queue = [near[0]!];
  while (queue.length) {
    const q = queue.pop()!;
    for (const d of DIRS4) {
      const x = q.x + d.x;
      const y = q.y + d.y;
      if (!pass(x, y) || seen.has(at(x, y))) continue;
      seen.add(at(x, y));
      queue.push({ x, y });
    }
  }
  return near.every((q) => seen.has(at(q.x, q.y)));
}

/**
 * The flood fill of each storey (the ground first): every walkable tile
 * (doors, stairs, ladders and windows too) by the piece of that storey it is
 * joined to, side by side and on the ground at its height; -1 for the rest,
 * and for a flight of stairs of several tiles (nobody steps off its sides).
 * The ways between storeys (stairs, ladders, drops) start and end on tiles
 * nothing is ever put on or beside (Furnisher.free), so pieces joined as
 * before mean every place reached as before.
 */
export function storeyPieces(map: FurnishMap): Int32Array[] {
  const { width: w, height: h } = map;
  return [map.tiles, ...map.levels.map((l) => l.tiles)].map((tiles, z) => {
    const piece = new Int32Array(w * h).fill(-1);
    const level = (i: number) => (z ? 0 : (map.elev[i] ?? 0));
    const decor = z ? map.levels[z - 1]!.decor : map.decor;
    const open = (i: number) => isWalkable(tiles[i] ?? null) && !longFlightTile(tiles, decor, w, i);
    let n = 0;
    for (let start = 0; start < w * h; start++) {
      if (piece[start] !== -1 || !open(start)) continue;
      piece[start] = n;
      const queue = [start];
      while (queue.length) {
        const i = queue.pop()!;
        const x = i % w;
        for (const j of [x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, i - w, i + w]) {
          if (j < 0 || j >= w * h || piece[j] !== -1 || !open(j) || level(j) !== level(i)) continue;
          piece[j] = n;
          queue.push(j);
        }
      }
      n++;
    }
    return piece;
  });
}

/** Whether what was joined (`before`, storeyPieces) is joined still (`after`), wherever it can still be walked. */
export function joinedAsBefore(before: Int32Array[], after: Int32Array[]): boolean {
  return before.every((was, z) => {
    const now = after[z]!;
    const into = new Map<number, number>();
    for (let i = 0; i < was.length; i++) {
      if (was[i]! < 0 || now[i]! < 0) continue;
      const seen = into.get(was[i]!);
      if (seen === undefined) into.set(was[i]!, now[i]!);
      else if (seen !== now[i]) return false;
    }
    return true;
  });
}

/** Every place the movement rules reach from `from` (path.ts stepsFrom), by node (grid.ts nodeOf), nobody in the way. */
export function reachableFrom(map: FurnishMap, from: Pos): Set<number> {
  const s = { width: map.width, height: map.height, tiles: map.tiles, elev: map.elev, decor: map.decor, ...(map.levels.length ? { levels: map.levels } : {}) } as unknown as GameState;
  const node = (p: Pos) => (p.z * map.height + p.y) * map.width + p.x;
  const seen = new Set<number>([node(from)]);
  const queue: Pos[] = [from];
  while (queue.length) {
    const p = queue.pop()!;
    for (const d of DIRS8) {
      for (const { to } of stepsFrom(s, p, d.x, d.y)) {
        const n = node(to);
        if (seen.has(n)) continue;
        seen.add(n);
        queue.push(to);
      }
    }
  }
  return seen;
}

/**
 * The look of a building as the client works it out (buildings.ts `look`,
 * from its first corner): the same number, so a shop is the trade its
 * shelves show (a bookshop's are books).
 */
export function buildingLook(x: number, y: number): number {
  let h = Math.imul(x + 1, 374761393) + Math.imul(y + 7, 668265263) + Math.imul(5 + 3, 2147483647);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

/** A shop's trade by its look (the client's homeFurnishing.ts shopTrade, the same split). */
export function tradeOf(look: number): 'books' | 'cafe' | 'grocer' {
  return look < 0.3 ? 'books' : look < 0.55 ? 'cafe' : 'grocer';
}

/** A change to undo: what stood on a place before an object was put there. */
interface Change {
  z: number;
  i: number;
  kind: TileKind | null;
  decor: Decor;
}

/**
 * Puts the things that take a tile on a map, keeping its ways (keepsWays),
 * clear of the places in `reserved` (where somebody starts, the objective,
 * what lies on the floor) and of every way through a door, up a ladder or
 * stairs, out of a window and off a stair's landing. Every change is kept, to
 * be undone (undo) if the map lost a way after all.
 */
export class Furnisher {
  private readonly changes: Change[] = [];
  private readonly reserved: Set<string>;
  private readonly landings = new Set<string>();

  constructor(
    readonly map: FurnishMap,
    reserved: readonly (Vec2 & { z?: number })[],
  ) {
    this.reserved = new Set(reserved.map((p) => `${p.x},${p.y},${p.z ?? 0}`));
    // A building's stairs land on the tile they face, a storey up.
    for (let z = 0; z < map.levels.length; z++) {
      const layer = this.layer(z);
      if (!layer) continue;
      for (let i = 0; i < layer.tiles.length; i++) {
        const look = layer.tiles[i] === 'ladder' ? decorOf(layer.decor[i]) : null;
        if (look?.kind !== 'stairs') continue;
        const d = DIRS4[look.facing]!;
        this.landings.add(`${(i % map.width) + d.x},${Math.floor(i / map.width) + d.y},${z + 1}`);
      }
    }
  }

  /** Level `z` (0 the ground), its decor made if it had none. */
  layer(z: number): Layer | null {
    if (!z) return { tiles: this.map.tiles, decor: this.map.decor };
    const level = this.map.levels[z - 1];
    if (!level) return null;
    level.decor ??= new Array<Decor>(this.map.width * this.map.height).fill(0);
    return { tiles: level.tiles, decor: level.decor };
  }

  tileAt(z: number, x: number, y: number): TileKind | null {
    if (x < 0 || y < 0 || x >= this.map.width || y >= this.map.height) return null;
    return this.layer(z)?.tiles[y * this.map.width + x] ?? null;
  }

  lookAt(z: number, x: number, y: number): { kind: DecorKind; facing: number } | null {
    if (x < 0 || y < 0 || x >= this.map.width || y >= this.map.height) return null;
    return decorOf(this.layer(z)?.decor[y * this.map.width + x]);
  }

  /** What the object standing on (x, y) of level z is: for another tile of one over several (a part), its anchor's look. */
  objectAt(z: number, x: number, y: number): { kind: DecorKind; facing: number } | null {
    const o = this.object(z, x, y);
    return o && { kind: o.kind, facing: o.facing };
  }

  /** The tiles of the object standing on p of level z (decor.ts objectAt: all of one over several), p alone for none. */
  tilesOf(z: number, p: Vec2): Vec2[] {
    const tiles = this.object(z, p.x, p.y)?.tiles ?? [p.y * this.map.width + p.x];
    return tiles.map((i) => ({ x: i % this.map.width, y: Math.floor(i / this.map.width) }));
  }

  private object(z: number, x: number, y: number): ReturnType<typeof objectAt> {
    const layer = this.layer(z);
    if (!layer || x < 0 || y < 0 || x >= this.map.width || y >= this.map.height) return null;
    return objectAt(layer.decor, this.map.width, y * this.map.width + x);
  }

  /** Whether an object may go on (x, y) of level z: plain floor, nothing on it nor anybody's, off every way through. */
  free(z: number, p: Vec2): boolean {
    if (this.tileAt(z, p.x, p.y) !== 'floor' || this.lookAt(z, p.x, p.y)) return false;
    const key = `${p.x},${p.y},${z}`;
    if (this.reserved.has(key) || this.landings.has(key)) return false;
    // Beside a door, a ladder or the stairs, a window, or a stairwell's open air upstairs.
    return !DIRS4.some((d) => {
      const x = p.x + d.x;
      const y = p.y + d.y;
      if (x < 0 || y < 0 || x >= this.map.width || y >= this.map.height) return false;
      const kind = this.tileAt(z, x, y);
      return WAYS.has(kind) || (z > 0 && kind === null);
    });
  }

  /** Puts `look` on (x, y) of level z, as `kind` cover, if it may go there and keeps every way; false if not. */
  put(z: number, p: Vec2, kind: 'low' | 'high', look: DecorKind, facing = 0): boolean {
    const layer = this.layer(z);
    if (!layer || !this.free(z, p) || !keepsWays(layer, this.map.width, this.map.height, p, z ? undefined : this.map.elev)) return false;
    this.block(z, p, kind, look, facing);
    return true;
  }

  /** Blocks a floor tile for what lies there and stands up (homeDressing, medicalAbandonment), on the same terms as put. */
  readonly strew = (p: Pos): boolean => this.put(p.z, p, 'low', 'strewn');

  private block(z: number, p: Vec2, kind: TileKind, look: DecorKind, facing: number): void {
    const layer = this.layer(z)!;
    const i = p.y * this.map.width + p.x;
    this.changes.push({ z, i, kind: layer.tiles[i] ?? null, decor: layer.decor[i] ?? 0 });
    layer.tiles[i] = kind;
    layer.decor[i] = decorCode(look, facing);
  }

  /** Takes everything put back off, last first. */
  undo(): void {
    for (const c of this.changes.reverse()) {
      const layer = this.layer(c.z)!;
      layer.tiles[c.i] = c.kind;
      layer.decor[c.i] = c.decor;
    }
    this.changes.length = 0;
  }
}

const step = (p: Vec2, f: number): Vec2 => ({ x: p.x + DIRS4[f]!.x, y: p.y + DIRS4[f]!.y });

function shuffled<T>(xs: T[], rng: Rng): T[] {
  for (let i = xs.length - 1; i > 0; i--) {
    const j = rng.int(i + 1);
    [xs[i], xs[j]] = [xs[j]!, xs[i]!];
  }
  return xs;
}

/**
 * A house's rooms, by what they are (playtest 18/10, #36, now on tiles of
 * their own): in the living room an armchair across the table from the sofa
 * (or a coffee table before it) and another at its side, a standard lamp and
 * a plant in the corners; a plant in the kitchen's; a bedside table by each
 * bed and a chair or a plant in a corner of the bedroom; a towel rail on the
 * bathroom's wall; a plant in the study; a coat stand in the hall, and a
 * plant. Each only where it fits (Furnisher.put) and while the room keeps
 * most of its floor.
 */
function furnishHome(f: Furnisher, home: HomeSite, rng: Rng): void {
  for (const room of home.rooms) {
    const z = room.z;
    if (!f.layer(z)) continue;
    const inRoom = (p: Vec2) => p.x >= room.x && p.y >= room.y && p.x < room.x + room.w && p.y < room.y + room.h;
    const cells: Vec2[] = [];
    for (let y = room.y; y < room.y + room.h; y++) for (let x = room.x; x < room.x + room.w; x++) cells.push({ x, y });
    const taken = cells.filter((p) => ['low', 'high'].includes(f.tileAt(z, p.x, p.y) ?? '')).length;
    let budget = Math.floor(room.w * room.h * MAX_SHARE) - taken;
    const put = (p: Vec2, look: DecorKind, facing = 0, kind: 'low' | 'high' = 'low') => {
      if (budget <= 0 || !inRoom(p) || !f.put(z, p, kind, look, facing)) return false;
      budget--;
      return true;
    };
    const things = cells.flatMap((p) => {
      const look = f.lookAt(z, p.x, p.y);
      return look ? [{ p, ...look }] : [];
    });
    const of = (kind: DecorKind) => things.filter((t) => t.kind === kind);
    /** The way into the room's middle from p (furnishRoom's inward). */
    const inward = (p: Vec2) => {
      const cx = room.x + (room.w - 1) / 2;
      const cy = room.y + (room.h - 1) / 2;
      return Math.abs(cx - p.x) > Math.abs(cy - p.y) ? (cx > p.x ? 1 : 3) : cy > p.y ? 2 : 0;
    };
    const corners = shuffled(
      [
        { x: room.x, y: room.y },
        { x: room.x + room.w - 1, y: room.y },
        { x: room.x, y: room.y + room.h - 1 },
        { x: room.x + room.w - 1, y: room.y + room.h - 1 },
      ],
      rng,
    );
    const corner = (look: DecorKind) => corners.some((c) => put(c, look, inward(c)));
    /** The way to the wall beside p, if one is; -1 if none. */
    const wallBy = (p: Vec2) => DIRS4.findIndex((d) => f.tileAt(z, p.x + d.x, p.y + d.y) === 'wall');
    switch (room.use) {
      case 'living': {
        for (const sofa of of('sofa')) {
          const ahead = step(sofa.p, sofa.facing);
          const table = f.objectAt(z, ahead.x, ahead.y)?.kind === 'table';
          // Across the table, facing the sofa; or a coffee table before it.
          if (table) put(step(ahead, sofa.facing), 'armchair', (sofa.facing + 2) % 4);
          else put(ahead, 'coffeeTable', sofa.facing);
          // At the side of the table (or of the sofa), turned to it.
          for (const side of [(sofa.facing + 1) % 4, (sofa.facing + 3) % 4]) if (put(step(table ? ahead : sofa.p, side), 'armchair', (side + 2) % 4)) break;
        }
        corner('floorLamp');
        corner('pottedPlant');
        break;
      }
      case 'kitchen':
        corner('pottedPlant');
        break;
      case 'bedroom':
        // By its head: beside the anchor, or beside the other tile of the head of a double bed (docs/MAPAS.md, phase 1).
        for (const bed of of('bed')) {
          const d = DIRS4[bed.facing]!;
          const head = f.tilesOf(z, bed.p).filter((q) => (q.x - bed.p.x) * d.x + (q.y - bed.p.y) * d.y === 0);
          head.some((q) => [(bed.facing + 1) % 4, (bed.facing + 3) % 4].some((side) => put(step(q, side), 'nightstand', bed.facing)));
        }
        corner(rng.next() < 0.5 ? 'diningChair' : 'pottedPlant');
        break;
      case 'bath': {
        const spots = [...corners, ...(room.w > 2 ? [{ x: room.x + 1, y: room.y }] : [])].filter((p) => wallBy(p) >= 0);
        spots.some((p) => put(p, 'towelRack', (wallBy(p) + 2) % 4));
        break;
      }
      case 'study':
        corner('pottedPlant');
        break;
      case 'hall':
        corners.filter((p) => wallBy(p) >= 0).some((p) => put(p, 'coatRack', (wallBy(p) + 2) % 4));
        corner('pottedPlant');
        break;
    }
  }
}

/**
 * A shop's floor by its trade (tradeOf, as the client shows it): a
 * bookshop's reading table, a café's tables (their chairs on their tiles), a
 * grocer's drinks fridge against a wall; a library's reading tables. Tables
 * stand out on the open floor, with free floor all round them.
 */
function furnishShop(f: Furnisher, site: FurnishSite, rng: Rng): void {
  const look = buildingLook(site.x, site.y);
  if (site.kind === 'hall' && look >= 0.3) return;
  const trade = site.kind === 'hall' ? 'books' : tradeOf(look);
  const inner: Vec2[] = [];
  for (let y = site.y + 1; y < site.y + site.h - 1; y++) for (let x = site.x + 1; x < site.x + site.w - 1; x++) inner.push({ x, y });
  const area = inner.length;
  const order = shuffled([...inner], rng);
  /** Open floor all round (a table and its chairs stand clear of walls and furniture). */
  const open = (p: Vec2) => {
    for (const d of [{ x: 0, y: 0 }, ...DIRS8]) if (f.tileAt(0, p.x + d.x, p.y + d.y) !== 'floor' || f.lookAt(0, p.x + d.x, p.y + d.y)) return false;
    return true;
  };
  const tables = (max: number, look: DecorKind) => {
    let n = 0;
    for (const p of order) {
      if (n >= max) break;
      if (open(p) && f.put(0, p, 'low', look, rng.int(4))) n++;
    }
  };
  if (site.kind === 'hall') tables(Math.max(2, Math.floor(area / 18)), 'readingTable');
  else if (trade === 'books') tables(1, 'readingTable');
  else if (trade === 'cafe') tables(Math.max(1, Math.floor(area / 12)), 'cafeTable');
  else {
    // The drinks fridge against a wall, its door to the room.
    for (const p of order) {
      const wall = DIRS4.findIndex((d) => f.tileAt(0, p.x + d.x, p.y + d.y) === 'wall');
      if (wall >= 0 && f.put(0, p, 'high', 'drinksFridge', (wall + 2) % 4)) break;
    }
  }
}

/** A barn's floor (playtest 18/10, #53): its tools leant against the walls, three at most, and a wheelbarrow out on the floor. */
function furnishBarn(f: Furnisher, site: FurnishSite, rng: Rng): void {
  const inner: Vec2[] = [];
  for (let y = site.y + 1; y < site.y + site.h - 1; y++) for (let x = site.x + 1; x < site.x + site.w - 1; x++) inner.push({ x, y });
  let tools = 0;
  let barrow = rng.next() < 0.8;
  for (const p of shuffled(inner, rng)) {
    const wall = DIRS4.findIndex((d) => f.tileAt(0, p.x + d.x, p.y + d.y) === 'wall');
    if (wall >= 0 && tools < 3) {
      if (f.put(0, p, 'low', 'tools', wall)) tools++;
    } else if (wall < 0 && barrow && f.put(0, p, 'low', 'wheelbarrow', rng.int(4))) barrow = false;
  }
}

/**
 * A town's kerbs (the streets' dressing, dressing.ts): now and then a fire
 * hydrant or a mailbox on the pavement by the street, apart from each other
 * and from the street lights (`lamps`), facing the street.
 */
function furnishKerbs(f: Furnisher, lamps: readonly Vec2[], rng: Rng): void {
  const { width: w, height: h, ground } = f.map;
  if (!ground) return;
  const placed: Vec2[] = [...lamps];
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = y * w + x;
      if (ground[i] !== 0 || (f.map.elev[i] ?? 0) !== 0) continue;
      const street = DIRS4.findIndex((d) => {
        const nx = x + d.x;
        const ny = y + d.y;
        return nx >= 0 && ny >= 0 && nx < w && ny < h && ground[ny * w + nx] === 1;
      });
      if (street < 0 || rng.next() >= KERB_CHANCE) continue;
      if (placed.some((q) => Math.abs(q.x - x) + Math.abs(q.y - y) <= KERB_GAP)) continue;
      if (f.put(0, { x, y }, 'low', rng.next() < 0.6 ? 'hydrant' : 'mailbox', street)) placed.push({ x, y });
    }
  }
}

/**
 * Furnishes a finished map (its final frame): the houses' rooms, the shops,
 * libraries and barns, and in town the kerbs. Returns the Furnisher, whose
 * `strew` the dressing that stands up uses after (homeDressing,
 * medicalAbandonment), and which undoes it all if the map lost a way.
 */
export function furnishMap(map: FurnishMap, homes: readonly HomeSite[], sites: readonly FurnishSite[], seed: number, reserved: readonly (Vec2 & { z?: number })[], kerbs: { lamps: readonly Vec2[] } | null): Furnisher {
  const f = new Furnisher(map, reserved);
  const rng = new Rng((seed ^ 0x6675726e) >>> 0);
  for (const home of homes) furnishHome(f, home, rng);
  for (const site of sites) {
    if (site.kind === 'barn') furnishBarn(f, site, rng);
    else furnishShop(f, site, rng);
  }
  if (kerbs) furnishKerbs(f, kerbs.lamps, rng);
  return f;
}
