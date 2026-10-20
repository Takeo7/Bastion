import * as THREE from 'three';
import { decorOf, DIRS4, objectAt, type DecorKind, type GameState, type HomeRoom, type HomeSite, type TileKind, type Vec2 } from '@bastion/engine';
import { STOREY } from './coords';
import type { PropId } from './syntyManifest';

// A house's rooms furnished beyond their cover (playtest 18/10, #36: «más
// cariño al amueblar… que se sienta una ciudad o un pueblo de verdad»): what
// the generator put in each room (GameState.homes, its decor) gets what goes
// with it. The sofa its rug and an armchair across the table, a lamp and a
// plant in the corners; chairs round the kitchen table, the bread bin on the
// counter; the bedside table and its lamp by the bed, a rug before it; the
// desk its books and lamp; a rug and the towels in the bathroom, a mat
// before the toilet in the aseo; the coats by the door in the hall. One style of each for a house, by where it
// stands; in a wrecked one the chairs are over and the lamp is down.
// What stands up takes a tile of its own, and the engine picks it (Arturo,
// 19/10: «que ocupen»; engine furnishing.ts): the armchairs, the coffee
// table, the lamps, the plants, the bedside tables, the chair in a corner,
// the towel rail and the coat stand are drawn where its decor says, while
// their tile stands. Only what lies flat (the rugs) or on the furniture (a
// lamp, books, the bread bin) is picked here, on clear floor; chairs round a
// table are tucked in on its own tile. A mission saved before has none of
// the engine's: its rooms only show less. The bed, the sofa, the table and
// the bath take two tiles or four (docs/MAPAS.md, phase 1): what goes with
// them goes round the whole of them.

export interface Furnishing {
  id: PropId;
  matrix: THREE.Matrix4;
  /** Its tile and storey (for the cutaway, and to drop it if its floor goes). */
  x: number;
  y: number;
  z: number;
  /** Stands on the floor (a rug, a chair) or on the furniture of its tile (a lamp on the bedside table). */
  cast: boolean;
}

/** Stable pseudo-random value in [0, 1). */
function hash(x: number, y: number, salt: number): number {
  let h = Math.imul(x + 31, 374761393) + Math.imul(y + 7, 668265263) + Math.imul(salt + 13, 2147483647);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

/** Quarter turns (counter-clockwise from above) that make a prop modelled facing north face DIRS4[f]. */
const turnTo = (f: number) => (4 - f) % 4;

/** How high the top of what stands on a tile is, for what is put on it (tiles). */
const TOP: Partial<Record<DecorKind, number>> = { table: 0.57, desk: 0.5, counter: 0.6, stove: 0.6 };
const BEDSIDE_TOP = 0.3;
/** How far from a table's middle the chairs tucked in round it sit, on its own tile (and the stools at a counter). */
const TUCKED = 0.3;

const WAYS = new Set<TileKind>(['door', 'doorOpen', 'ladder']);

/** Cover still standing: what the engine put there, and no blast took. */
const standing = (tile: TileKind | null | undefined) => tile === 'low' || tile === 'high';

export function furnishHomes(s: GameState): Furnishing[] {
  const out: Furnishing[] = [];
  for (const home of s.homes ?? []) furnishHome(s, home, out);
  return out;
}

function furnishHome(s: GameState, home: HomeSite, out: Furnishing[]): void {
  const w = s.width;
  // One style for the whole house.
  const style = (salt: number, n: number) => Math.floor(hash(home.x, home.y, salt) * n);
  const chair: PropId = style(1, 2) ? 'diningChair2' : 'diningChair';
  const rug: PropId = (['rug', 'rug2', 'rug3'] as const)[style(2, 3)]!;
  const lamp: PropId = style(3, 2) ? 'floorLamp2' : 'floorLamp';
  const small: PropId = style(4, 2) ? 'tableLamp2' : 'tableLamp';
  const plants: PropId[] = ['pottedPlant', 'pottedPlant2', 'pottedPlant3'];
  const wrecked = home.wear > 0.6;
  const used = new Set<string>();
  for (const room of home.rooms) {
    const layer = room.z ? s.levels?.[room.z - 1] : { tiles: s.tiles, decor: s.decor };
    if (!layer) continue;
    const tileAt = (x: number, y: number) => (x < 0 || y < 0 || x >= w || y >= s.height ? null : layer.tiles[y * w + x]);
    const inRoom = (p: Vec2) => p.x >= room.x && p.y >= room.y && p.x < room.x + room.w && p.y < room.y + room.h;
    const key = (p: Vec2) => `${p.x},${p.y},${room.z}`;
    /** Clear floor in the room, off the ways through its doors and up its ladders, nothing put there yet: for what lies flat. */
    const clear = (p: Vec2) =>
      inRoom(p) && tileAt(p.x, p.y) === 'floor' && !decorOf(layer.decor?.[p.y * w + p.x]) && !used.has(key(p)) && !DIRS4.some((d) => WAYS.has(tileAt(p.x + d.x, p.y + d.y) ?? 'floor'));
    const base = room.z * STOREY + (room.z ? 0 : (s.elev[room.y * w + room.x] ?? 0) * STOREY);
    const r = (p: Vec2, salt: number) => hash(p.x * 3 + room.z, p.y * 5, salt);
    /** `id` on tile p, its front to DIRS4[f] (a turn more `turn`), off the tile's middle by (du, dv) along x and y, `y` up. */
    const put = (id: PropId, p: Vec2, f: number, o: { du?: number; dv?: number; y?: number; turn?: number; k?: number; over?: number; cast?: boolean } = {}) => {
      const m = new THREE.Matrix4().compose(
        new THREE.Vector3(p.x + 0.5 + (o.du ?? 0), base + (o.y ?? 0), p.y + 0.5 + (o.dv ?? 0)),
        new THREE.Quaternion().setFromEuler(new THREE.Euler(0, (turnTo(f) * Math.PI) / 2 + (o.turn ?? 0), o.over ?? 0, 'YXZ')),
        new THREE.Vector3(o.k ?? 1, o.k ?? 1, o.k ?? 1),
      );
      out.push({ id, matrix: m, x: p.x, y: p.y, z: room.z, cast: o.cast ?? true });
      if (!o.y) used.add(key(p));
    };
    /** Knocked over in a wrecked house (now and then): on its side. */
    const over = (p: Vec2, salt: number) => (wrecked && r(p, salt) < 0.45 ? Math.PI / 2 : 0);
    const things: { p: Vec2; kind: DecorKind; facing: number }[] = [];
    for (let y = room.y; y < room.y + room.h; y++) {
      for (let x = room.x; x < room.x + room.w; x++) {
        const look = decorOf(layer.decor?.[y * w + x]);
        if (look && standing(tileAt(x, y))) things.push({ p: { x, y }, ...look });
      }
    }
    const of = (kind: DecorKind) => things.filter((t) => t.kind === kind);
    const step = (p: Vec2, f: number): Vec2 => ({ x: p.x + DIRS4[f]!.x, y: p.y + DIRS4[f]!.y });
    /** The tiles of the piece anchored on p (several for a bed, a sofa, a table, a bath: docs/MAPAS.md, phase 1). */
    const tilesOf = (p: Vec2): Vec2[] => (layer.decor ? objectAt(layer.decor, w, p.y * w + p.x)?.tiles ?? [p.y * w + p.x] : [p.y * w + p.x]).map((i) => ({ x: i % w, y: Math.floor(i / w) }));
    /**
     * Before a piece facing f, at its middle: the tile ahead of its front row nearest the anchor's, and how far off
     * that tile's middle the piece's middle lies across (a sofa or a bed two wide).
     */
    const before = (p: Vec2, f: number) => {
      const own = tilesOf(p);
      const d = DIRS4[f]!;
      const depth = Math.max(...own.map((q) => (q.x - p.x) * d.x + (q.y - p.y) * d.y)) + 1;
      const mid = { x: own.reduce((a, q) => a + q.x, 0) / own.length - p.x, y: own.reduce((a, q) => a + q.y, 0) / own.length - p.y };
      return { at: { x: p.x + d.x * depth, y: p.y + d.y * depth }, du: d.x ? 0 : mid.x, dv: d.y ? 0 : mid.y };
    };
    /** Pushed into the corner of the room it stands in (along no wall, not at all). */
    const cornered = (p: Vec2, by = 0.15) => ({
      du: p.x === room.x ? -by : p.x === room.x + room.w - 1 ? by : 0,
      dv: p.y === room.y ? -by : p.y === room.y + room.h - 1 ? by : 0,
    });
    /**
     * Chairs round a table, tucked in on its own tiles on their sides facing open floor (three at most, four round
     * one of two tiles), facing it; over in a wrecked house.
     */
    const roundTable = (t: Vec2) => {
      const own = tilesOf(t);
      let n = 0;
      for (const q of own) {
        for (let f = 0; f < 4 && n < (own.length > 1 ? 4 : 3); f++) {
          const side = step(q, f);
          if (!inRoom(side) || tileAt(side.x, side.y) !== 'floor') continue;
          put(chair, q, (f + 2) % 4, { du: DIRS4[f]!.x * TUCKED, dv: DIRS4[f]!.y * TUCKED, y: 0.001, over: over(side, 70 + f), turn: (r(side, 80) - 0.5) * 0.3 });
          n++;
        }
      }
    };
    // What the engine put in the room on tiles of their own, each as it was drawn.
    for (const t of things) {
      switch (t.kind) {
        case 'armchair':
          put(wrecked ? 'armchairWorn' : 'armchair', t.p, t.facing, { over: over(t.p, 20) });
          break;
        case 'coffeeTable':
          put('coffeeTable', t.p, t.facing);
          break;
        case 'floorLamp':
          put(lamp, t.p, t.facing, { ...cornered(t.p), over: over(t.p, 31) });
          break;
        case 'pottedPlant': {
          const salt = room.use === 'kitchen' ? 11 : room.use === 'bedroom' ? 10 : room.use === 'study' ? 8 : room.use === 'hall' ? 9 : 5;
          put(plants[style(salt, 3)]!, t.p, t.facing, { ...cornered(t.p), k: room.use === 'kitchen' || room.use === 'hall' ? 0.9 : 1 });
          break;
        }
        case 'diningChair':
          put(chair, t.p, t.facing, cornered(t.p));
          break;
        case 'nightstand': {
          // Against the bed's side, towards its head; its lamp on it (in a wrecked house, now and then).
          const bed = DIRS4.findIndex((d) => !!layer.decor && objectAt(layer.decor, w, (t.p.y + d.y) * w + t.p.x + d.x)?.kind === 'bed');
          const side = bed >= 0 ? (bed + 2) % 4 : (t.facing + 1) % 4;
          const at = { du: -DIRS4[side]!.x * 0.25 - DIRS4[t.facing]!.x * 0.3, dv: -DIRS4[side]!.y * 0.25 - DIRS4[t.facing]!.y * 0.3 };
          put('bedsideTable', t.p, t.facing, at);
          if (!wrecked || r(t.p, 40) < 0.5) put(small, t.p, t.facing, { ...at, y: BEDSIDE_TOP, cast: false });
          break;
        }
        case 'towelRack': {
          const wall = DIRS4[(t.facing + 2) % 4]!;
          put('towelRack', t.p, t.facing, { du: wall.x * 0.3, dv: wall.y * 0.3 });
          break;
        }
        case 'coatRack':
          put('coatRack', t.p, t.facing, { ...cornered(t.p, 0.2), over: over(t.p, 60) });
          break;
      }
    }
    switch (room.use) {
      case 'living': {
        // The sofa's rug before it (under the coffee table, if one stands there), at its middle.
        for (const sofa of of('sofa')) {
          const ahead = before(sofa.p, sofa.facing);
          if (inRoom(ahead.at)) put(rug, ahead.at, sofa.facing, { du: ahead.du - DIRS4[sofa.facing]!.x * 0.45, dv: ahead.dv - DIRS4[sofa.facing]!.y * 0.45, y: 0.001, cast: false });
        }
        for (const table of of('table')) if (!of('sofa').length) roundTable(table.p);
        break;
      }
      case 'kitchen': {
        for (const table of of('table')) roundTable(table.p);
        // On the counter: the bread bin, or the coffee machine.
        const counter = of('counter')[0];
        if (counter) put(style(6, 2) ? 'breadBin' : 'coffeeMachine', counter.p, counter.facing, { y: TOP.counter, du: -DIRS4[counter.facing]!.x * 0.2, dv: -DIRS4[counter.facing]!.y * 0.2 });
        break;
      }
      case 'bedroom': {
        // A rug at the foot of the bed.
        for (const bed of of('bed')) {
          const foot = before(bed.p, bed.facing);
          if (clear(foot.at)) put('rug3', foot.at, bed.facing, { du: foot.du - DIRS4[bed.facing]!.x * 0.2, dv: foot.dv - DIRS4[bed.facing]!.y * 0.2, cast: false, k: 0.8 });
        }
        break;
      }
      case 'bath': {
        // The bath rug beside the tub (by its anchor first).
        const tub = of('bathtub')[0];
        const spots = tub ? tilesOf(tub.p).flatMap((q) => [0, 1, 2, 3].map((f) => ({ p: step(q, f), f }))) : [];
        const spot = spots.find((o) => clear(o.p));
        if (spot) put('bathRug', spot.p, spot.f, { cast: false });
        break;
      }
      case 'toilet': {
        // The mat before the toilet.
        const wc = of('wc')[0];
        const ahead = wc && step(wc.p, wc.facing);
        if (ahead && clear(ahead)) put('bathRug', ahead, wc.facing, { cast: false, k: 0.8 });
        break;
      }
      case 'study': {
        // (Its chair is the map's, as every desk's.)
        for (const desk of of('desk')) {
          put(small, desk.p, desk.facing, { y: TOP.desk, du: 0.25, cast: false });
          put(style(7, 2) ? 'bookPile2' : 'bookPile', desk.p, desk.facing, { y: TOP.desk, du: -0.2, turn: 0.3, cast: false });
        }
        const middle = { x: room.x + Math.floor(room.w / 2), y: room.y + Math.floor(room.h / 2) };
        if (clear(middle)) put(rug, middle, 0, { cast: false, k: 0.9 });
        break;
      }
    }
  }
}

/** A building as the client's plan has it (buildings.ts): its bounds, walls included, what it is and its looks. */
export interface ShopSite {
  x0: number;
  y0: number;
  x1: number;
  y1: number;
  kind: string;
  look: number;
}

/** A shop's trade by its looks (mapView's shelves go by the same: a bookshop's are books). */
export function shopTrade(look: number): 'books' | 'cafe' | 'grocer' {
  return look < 0.3 ? 'books' : look < 0.55 ? 'cafe' : 'grocer';
}

/**
 * A shop's trade as its floor shows it: by what the engine furnished it with
 * (furnishing.ts: a café's tables, a reading table, a drinks fridge), else by
 * its looks, as the engine picked it from the same number.
 */
export function shopTradeIn(s: GameState, b: ShopSite): 'books' | 'cafe' | 'grocer' {
  for (let y = b.y0; y <= b.y1; y++) {
    for (let x = b.x0; x <= b.x1; x++) {
      const kind = decorOf(s.decor?.[y * s.width + x])?.kind;
      if (kind === 'cafeTable') return 'cafe';
      if (kind === 'readingTable') return 'books';
      if (kind === 'drinksFridge') return 'grocer';
    }
  }
  return shopTrade(b.look);
}

/**
 * The shops dressed as their trade (playtest 18/10, #36), on their ground
 * floor: a bookshop's books on its counter; a café's stools at the bar,
 * tucked in under it, and the coffee machine on it; a grocer's cooler and
 * boxes of stock by the shelves. Upstairs, the stock room's boxes. Small
 * things, on clear floor or on the furniture. The tables and the drinks
 * fridge stand on tiles of their own (furnishOwnTiles).
 */
export function furnishShops(s: GameState, sites: readonly ShopSite[]): Furnishing[] {
  const out: Furnishing[] = [];
  const w = s.width;
  for (const b of sites) {
    const library = b.kind === 'hall' && b.look < 0.3;
    if (b.kind !== 'shop' && !library) continue;
    const trade = library ? 'books' : shopTradeIn(s, b);
    for (let z = 0; z <= (s.levels?.length ?? 0); z++) {
      const layer = z ? s.levels?.[z - 1] : { tiles: s.tiles, decor: s.decor };
      if (!layer) continue;
      const tileAt = (x: number, y: number) => (x < b.x0 || y < b.y0 || x > b.x1 || y > b.y1 ? null : layer.tiles[y * w + x]);
      const lookAt = (x: number, y: number) => decorOf(layer.decor?.[y * w + x]);
      const used = new Set<number>();
      const clear = (x: number, y: number) =>
        tileAt(x, y) === 'floor' && !lookAt(x, y) && !used.has(y * w + x) && !DIRS4.some((d) => WAYS.has(tileAt(x + d.x, y + d.y) ?? 'floor'));
      const base = z * STOREY;
      const put = (id: PropId, x: number, y: number, yaw: number, o: { du?: number; dv?: number; y?: number; k?: number; cast?: boolean } = {}) => {
        const m = new THREE.Matrix4().compose(
          new THREE.Vector3(x + 0.5 + (o.du ?? 0), base + (o.y ?? 0), y + 0.5 + (o.dv ?? 0)),
          new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), yaw),
          new THREE.Vector3(o.k ?? 1, o.k ?? 1, o.k ?? 1),
        );
        out.push({ id, matrix: m, x, y, z, cast: o.cast ?? true });
        if (!o.y) used.add(y * w + x);
      };
      const r = (x: number, y: number, salt: number) => hash(x * 3 + z, y * 5, salt);
      const counters: { x: number; y: number; f: number }[] = [];
      const shelves: { x: number; y: number; f: number }[] = [];
      for (let y = b.y0; y <= b.y1; y++) {
        for (let x = b.x0; x <= b.x1; x++) {
          const look = lookAt(x, y);
          if (!look || !standing(tileAt(x, y))) continue;
          if (look.kind === 'counter') counters.push({ x, y, f: look.facing });
          if (look.kind === 'shelf') shelves.push({ x, y, f: look.facing });
        }
      }
      const yawOf = (f: number) => (turnTo(f) * Math.PI) / 2;
      if (z > 0) {
        // The stock room: boxes by the shelves.
        for (const sh of shelves) {
          const p = { x: sh.x + DIRS4[sh.f]!.x, y: sh.y + DIRS4[sh.f]!.y };
          if (clear(p.x, p.y) && r(p.x, p.y, 1) < 0.3) put('cardboard', p.x, p.y, r(p.x, p.y, 2) * 6.3, { du: -DIRS4[sh.f]!.x * 0.2, dv: -DIRS4[sh.f]!.y * 0.2 });
        }
        continue;
      }
      switch (trade) {
        case 'books':
          counters.forEach((c, k) => {
            if (k % 2 === 0) put(k % 4 ? 'bookPile3' : 'bookPile', c.x, c.y, yawOf(c.f) + r(c.x, c.y, 4) * 0.6, { y: TOP.counter, cast: false });
          });
          break;
        case 'cafe':
          // The stools at the bar, tucked in under it on its tile.
          counters.forEach((c, k) => {
            put('barStool', c.x, c.y, yawOf(c.f), { du: DIRS4[c.f]!.x * TUCKED, dv: DIRS4[c.f]!.y * TUCKED, y: 0.001 });
            if (k === 1) put('coffeeMachine', c.x, c.y, yawOf(c.f), { y: TOP.counter, cast: false });
          });
          break;
        case 'grocer': {
          // The cooler by the counter, stock by the shelves.
          const c = counters[0];
          if (c) {
            const p = DIRS4.map((d) => ({ x: c.x + d.x, y: c.y + d.y })).find((q) => clear(q.x, q.y));
            if (p) put('drinksCooler', p.x, p.y, yawOf(c.f));
          }
          for (const sh of shelves) {
            const p = { x: sh.x + DIRS4[sh.f]!.x, y: sh.y + DIRS4[sh.f]!.y };
            if (clear(p.x, p.y) && r(p.x, p.y, 6) < 0.25) put('cardboard', p.x, p.y, r(p.x, p.y, 7) * 6.3, { du: -DIRS4[sh.f]!.x * 0.25, dv: -DIRS4[sh.f]!.y * 0.25 });
          }
          break;
        }
      }
    }
  }
  return out;
}

/**
 * What the engine stood on tiles of their own in shops, libraries and barns
 * (furnishing.ts), wherever they are and while they stand, whatever the
 * client makes of the building: a café's tables with their two chairs, a
 * reading table with its chair, tucked in on its tile; a grocer's drinks
 * fridge against its wall; a barn's tools leant against its wall and its
 * wheelbarrow.
 */
export function furnishOwnTiles(s: GameState): Furnishing[] {
  const out: Furnishing[] = [];
  const w = s.width;
  let tools = 0;
  for (let z = 0; z <= (s.levels?.length ?? 0); z++) {
    const layer = z ? s.levels?.[z - 1] : { tiles: s.tiles, decor: s.decor };
    if (!layer?.decor) continue;
    for (let i = 0; i < layer.tiles.length; i++) {
      const look = decorOf(layer.decor[i]);
      if (!look || !standing(layer.tiles[i])) continue;
      const x = i % w;
      const y = (i - x) / w;
      const r = (salt: number) => hash(x * 3 + z, y * 5, salt);
      const base = z * STOREY + (z ? 0 : (s.elev[i] ?? 0) * STOREY);
      const put = (id: PropId, yaw: number, o: { du?: number; dv?: number; y?: number; lean?: number } = {}) =>
        out.push({
          id,
          matrix: new THREE.Matrix4().compose(
            new THREE.Vector3(x + 0.5 + (o.du ?? 0), base + (o.y ?? 0), y + 0.5 + (o.dv ?? 0)),
            new THREE.Quaternion().setFromEuler(new THREE.Euler(o.lean ?? 0, yaw, 0, 'YXZ')),
            new THREE.Vector3(1, 1, 1),
          ),
          x,
          y,
          z,
          cast: true,
        });
      switch (look.kind) {
        case 'cafeTable':
        case 'readingTable': {
          const cafe = look.kind === 'cafeTable';
          const turn = r(3) * 0.6;
          put(cafe ? 'cafeTable' : 'coffeeTable', turn);
          for (let k = 0; k < (cafe ? 2 : 1); k++) {
            const a = turn + k * Math.PI + Math.PI / 2;
            put(cafe ? 'cafeChair' : 'diningChair', a + Math.PI, { du: Math.sin(a) * TUCKED, dv: Math.cos(a) * TUCKED, y: 0.001 });
          }
          break;
        }
        case 'drinksFridge': {
          const wall = DIRS4[(look.facing + 2) % 4]!;
          put('drinksFridge', (turnTo(look.facing) * Math.PI) / 2, { du: wall.x * 0.2, dv: wall.y * 0.2 });
          break;
        }
        case 'tools': {
          // Leant against the wall, its handle up to it.
          const d = DIRS4[look.facing]!;
          put((['pitchfork', 'rake', 'spade'] as const)[tools++ % 3]!, (turnTo(look.facing) * Math.PI) / 2 + (r(2) - 0.5) * 0.4, { du: d.x * 0.36, dv: d.y * 0.36, lean: -0.22 });
          break;
        }
        case 'wheelbarrow':
          put('wheelbarrow', r(4) * 6.3);
          break;
      }
    }
  }
  return out;
}

/** A barn's floor (playtest 18/10, #53): buckets on clear ground (its tools and wheelbarrow, furnishOwnTiles). */
export function furnishBarns(s: GameState, sites: readonly ShopSite[]): Furnishing[] {
  const out: Furnishing[] = [];
  const w = s.width;
  for (const b of sites) {
    if (b.kind !== 'barn') continue;
    const tileAt = (x: number, y: number) => (x < b.x0 || y < b.y0 || x > b.x1 || y > b.y1 ? null : s.tiles[y * w + x]);
    const used = new Set<number>();
    const clear = (x: number, y: number) =>
      tileAt(x, y) === 'floor' && !decorOf(s.decor?.[y * w + x]) && !used.has(y * w + x) && !DIRS4.some((d) => WAYS.has(tileAt(x + d.x, y + d.y) ?? 'floor'));
    const put = (id: PropId, x: number, y: number, yaw: number, o: { du?: number; dv?: number; lean?: number; cast?: boolean } = {}) => {
      out.push({
        id,
        matrix: new THREE.Matrix4().compose(
          new THREE.Vector3(x + 0.5 + (o.du ?? 0), (s.elev[y * w + x] ?? 0) * STOREY, y + 0.5 + (o.dv ?? 0)),
          new THREE.Quaternion().setFromEuler(new THREE.Euler(o.lean ?? 0, yaw, 0, 'YXZ')),
          new THREE.Vector3(1, 1, 1),
        ),
        x,
        y,
        z: 0,
        cast: o.cast ?? true,
      });
      used.add(y * w + x);
    };
    for (let y = b.y0 + 1; y < b.y1; y++) {
      for (let x = b.x0 + 1; x < b.x1; x++) {
        const r = (salt: number) => hash(x, y, salt);
        if (clear(x, y) && r(5) < 0.1) put('bucket', x, y, r(6) * 6.3, { du: (r(7) - 0.5) * 0.5, dv: (r(8) - 0.5) * 0.5 });
      }
    }
  }
  return out;
}

/** The room of a house a tile is in, if any (a home's shelves are its bookcases). */
export function homeRoomAt(s: GameState, x: number, y: number, z: number): HomeRoom | null {
  for (const home of s.homes ?? []) {
    if (x < home.x || y < home.y || x >= home.x + home.w || y >= home.y + home.h) continue;
    for (const room of home.rooms) if (room.z === z && x >= room.x && y >= room.y && x < room.x + room.w && y < room.y + room.h) return room;
  }
  return null;
}
