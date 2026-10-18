import * as THREE from 'three';
import { decorOf, DIRS4, type DecorKind, type GameState, type HomeRoom, type HomeSite, type TileKind, type Vec2 } from '@bastion/engine';
import { STOREY } from './coords';
import type { PropId } from './syntyManifest';

// A house's rooms furnished beyond their cover (playtest 18/10, #36: «más
// cariño al amueblar… que se sienta una ciudad o un pueblo de verdad»): what
// the generator put in each room (GameState.homes, its decor) gets what goes
// with it. The sofa its rug and an armchair across the table, a lamp and a
// plant in the corners; chairs round the kitchen table, the bread bin on the
// counter; the bedside table and its lamp by the bed, a rug before it; the
// desk its books and lamp; a rug and the towels in the bathroom; the
// coats by the door in the hall. One style of each for a house, by where it
// stands; in a wrecked one the chairs are over and the lamp is down.
// Cosmetic only, as the rest of the map's dressing: never cover, never on a
// way through a door or up a ladder, only where the floor is clear.

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

const WAYS = new Set<TileKind>(['door', 'doorOpen', 'ladder']);

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
    /** Clear floor in the room, off the ways through its doors and up its ladders, nothing put there yet. */
    const clear = (p: Vec2) =>
      inRoom(p) && tileAt(p.x, p.y) === 'floor' && !decorOf(layer.decor?.[p.y * w + p.x]) && !used.has(key(p)) && !DIRS4.some((d) => WAYS.has(tileAt(p.x + d.x, p.y + d.y) ?? 'floor'));
    const wallBy = (p: Vec2) => DIRS4.findIndex((d) => tileAt(p.x + d.x, p.y + d.y) === 'wall');
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
        if (look) things.push({ p: { x, y }, ...look });
      }
    }
    const of = (kind: DecorKind) => things.filter((t) => t.kind === kind);
    const step = (p: Vec2, f: number): Vec2 => ({ x: p.x + DIRS4[f]!.x, y: p.y + DIRS4[f]!.y });
    const corners = [
      { x: room.x, y: room.y },
      { x: room.x + room.w - 1, y: room.y },
      { x: room.x, y: room.y + room.h - 1 },
      { x: room.x + room.w - 1, y: room.y + room.h - 1 },
    ];
    /** The middle of the room's open floor nearest p's way. */
    const inward = (p: Vec2) => {
      const cx = room.x + (room.w - 1) / 2;
      const cy = room.y + (room.h - 1) / 2;
      return Math.abs(cx - p.x) > Math.abs(cy - p.y) ? (cx > p.x ? 1 : 3) : cy > p.y ? 2 : 0;
    };
    const cornerThing = (id: PropId, salt: number, k = 1) => {
      const c = corners.filter(clear).sort((a, b) => r(a, salt) - r(b, salt))[0];
      if (c) put(id, c, inward(c), { du: (c.x === room.x ? -0.15 : 0.15), dv: (c.y === room.y ? -0.15 : 0.15), k, over: id.startsWith('floorLamp') ? over(c, salt + 1) : 0 });
    };
    /** Chairs round a table, on its clear sides (three at most), facing it; over in a wrecked house. */
    const roundTable = (t: Vec2) => {
      let n = 0;
      for (let f = 0; f < 4 && n < 3; f++) {
        const seat = step(t, f);
        if (!clear(seat)) continue;
        put(chair, seat, (f + 2) % 4, { du: -DIRS4[f]!.x * 0.18, dv: -DIRS4[f]!.y * 0.18, over: over(seat, 70 + f), turn: (r(seat, 80) - 0.5) * 0.3 });
        n++;
      }
    };
    switch (room.use) {
      case 'living': {
        // The sofa's rug, the table (or the television) before it, an armchair across from it.
        for (const sofa of of('sofa')) {
          const ahead = step(sofa.p, sofa.facing);
          if (inRoom(ahead)) put(rug, ahead, sofa.facing, { du: -DIRS4[sofa.facing]!.x * 0.45, dv: -DIRS4[sofa.facing]!.y * 0.45, cast: false });
          const beyond = step(ahead, sofa.facing);
          const table = decorOf(layer.decor?.[ahead.y * w + ahead.x])?.kind === 'table';
          if (table && clear(beyond)) put(wrecked ? 'armchairWorn' : 'armchair', beyond, (sofa.facing + 2) % 4, { over: over(beyond, 20) });
          else if (clear(ahead)) put('coffeeTable', ahead, sofa.facing, { dv: 0 });
          // An armchair at the side of the table (or of the sofa), turned to it.
          for (const side of [(sofa.facing + 1) % 4, (sofa.facing + 3) % 4]) {
            const by = step(table ? ahead : sofa.p, side);
            if (!clear(by)) continue;
            put(wrecked ? 'armchairWorn' : 'armchair', by, (side + 2) % 4, { turn: table ? 0 : -0.5, over: over(by, 21) });
            break;
          }
        }
        for (const table of of('table')) if (!of('sofa').length) roundTable(table.p);
        cornerThing(lamp, 30);
        cornerThing(plants[style(5, 3)]!, 31);
        break;
      }
      case 'kitchen': {
        for (const table of of('table')) roundTable(table.p);
        cornerThing(plants[style(11, 3)]!, 44, 0.9);
        // On the counter: the bread bin, or the coffee machine.
        const counter = of('counter')[0];
        if (counter) put(style(6, 2) ? 'breadBin' : 'coffeeMachine', counter.p, counter.facing, { y: TOP.counter, du: -DIRS4[counter.facing]!.x * 0.2, dv: -DIRS4[counter.facing]!.y * 0.2 });
        break;
      }
      case 'bedroom': {
        for (const bed of of('bed')) {
          // The bedside table beside the bed, its lamp on it; a rug before it.
          for (const side of [(bed.facing + 1) % 4, (bed.facing + 3) % 4]) {
            const by = step(bed.p, side);
            if (!clear(by)) continue;
            put('bedsideTable', by, bed.facing, { du: -DIRS4[side]!.x * 0.25 - DIRS4[bed.facing]!.x * 0.3, dv: -DIRS4[side]!.y * 0.25 - DIRS4[bed.facing]!.y * 0.3 });
            if (!wrecked || r(by, 40) < 0.5) put(small, by, bed.facing, { du: -DIRS4[side]!.x * 0.25 - DIRS4[bed.facing]!.x * 0.3, dv: -DIRS4[side]!.y * 0.25 - DIRS4[bed.facing]!.y * 0.3, y: BEDSIDE_TOP, cast: false });
            break;
          }
          const foot = step(bed.p, bed.facing);
          if (clear(foot)) put('rug3', foot, bed.facing, { du: -DIRS4[bed.facing]!.x * 0.2, dv: -DIRS4[bed.facing]!.y * 0.2, cast: false, k: 0.8 });
        }
        if (r(corners[0]!, 41) < 0.5) cornerThing(chair, 42);
        else cornerThing(plants[style(10, 3)]!, 43);
        break;
      }
      case 'bath': {
        const tub = of('bathtub')[0];
        const by = tub ? DIRS4.map((_, f) => step(tub.p, f)).find(clear) : undefined;
        if (by && tub) put('bathRug', by, inward(by), { cast: false });
        const spot = [...corners, ...room.w > 2 ? [{ x: room.x + 1, y: room.y }] : []].find((p) => clear(p) && wallBy(p) >= 0);
        if (spot) put('towelRack', spot, (wallBy(spot) + 2) % 4, { du: DIRS4[wallBy(spot)]!.x * 0.3, dv: DIRS4[wallBy(spot)]!.y * 0.3 });
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
        cornerThing(plants[style(8, 3)]!, 51);
        break;
      }
      case 'hall': {
        // The coats by the door.
        const spot = corners.find((p) => clear(p) && wallBy(p) >= 0);
        if (spot) put('coatRack', spot, (wallBy(spot) + 2) % 4, { du: (spot.x === room.x ? -0.2 : 0.2), dv: (spot.y === room.y ? -0.2 : 0.2), over: over(spot, 60) });
        cornerThing(plants[style(9, 3)]!, 61, 0.9);
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
 * The shops dressed as their trade (playtest 18/10, #36), on their ground
 * floor: a bookshop's books on its counter and a reading corner; a café's
 * stools at the bar, the coffee machine on it and its tables with their chairs
 * out on the open floor; a grocer's drinks fridge against a wall, the cooler,
 * boxes of stock by the shelves. A library (a hall of bookcases) its reading
 * tables. Upstairs, the stock room's boxes. Cosmetic, on clear floor only.
 */
export function furnishShops(s: GameState, sites: readonly ShopSite[]): Furnishing[] {
  const out: Furnishing[] = [];
  const w = s.width;
  for (const b of sites) {
    const library = b.kind === 'hall' && b.look < 0.3;
    if (b.kind !== 'shop' && !library) continue;
    const trade = library ? 'books' : shopTrade(b.look);
    for (let z = 0; z <= (s.levels?.length ?? 0); z++) {
      const layer = z ? s.levels?.[z - 1] : { tiles: s.tiles, decor: s.decor };
      if (!layer) continue;
      const tileAt = (x: number, y: number) => (x < b.x0 || y < b.y0 || x > b.x1 || y > b.y1 ? null : layer.tiles[y * w + x]);
      const lookAt = (x: number, y: number) => decorOf(layer.decor?.[y * w + x]);
      const used = new Set<number>();
      const clear = (x: number, y: number) =>
        tileAt(x, y) === 'floor' && !lookAt(x, y) && !used.has(y * w + x) && !DIRS4.some((d) => WAYS.has(tileAt(x + d.x, y + d.y) ?? 'floor'));
      /** Open floor all round (a table and its chairs stand clear of walls and furniture). */
      const open = (x: number, y: number) => {
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) if (tileAt(x + dx, y + dy) !== 'floor' || lookAt(x + dx, y + dy)) return false;
        return clear(x, y);
      };
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
          if (look?.kind === 'counter') counters.push({ x, y, f: look.facing });
          if (look?.kind === 'shelf') shelves.push({ x, y, f: look.facing });
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
      // Tables out on the open floor, every few tiles: a café's with two chairs, a bookshop's reading table with one.
      const tables = (max: number, chairs: number) => {
        let n = 0;
        for (let y = b.y0 + 1; y < b.y1 && n < max; y++) {
          for (let x = b.x0 + 1; x < b.x1 && n < max; x++) {
            if ((x + 2 * y) % 4 !== Math.floor(b.look * 4) || !open(x, y)) continue;
            const turn = r(x, y, 3) * 0.6;
            put(trade === 'cafe' ? 'cafeTable' : 'coffeeTable', x, y, turn);
            for (let k = 0; k < chairs; k++) {
              const a = turn + k * Math.PI + Math.PI / 2;
              put(trade === 'cafe' ? 'cafeChair' : 'diningChair', x, y, a + Math.PI, { du: Math.sin(a) * 0.42, dv: Math.cos(a) * 0.42, y: 0.001 });
            }
            used.add(y * w + x + 1);
            used.add(y * w + x - 1);
            n++;
          }
        }
      };
      const area = (b.x1 - b.x0 - 1) * (b.y1 - b.y0 - 1);
      switch (trade) {
        case 'books':
          counters.forEach((c, k) => {
            if (k % 2 === 0) put(k % 4 ? 'bookPile3' : 'bookPile', c.x, c.y, yawOf(c.f) + r(c.x, c.y, 4) * 0.6, { y: TOP.counter, cast: false });
          });
          tables(library ? Math.max(2, Math.floor(area / 18)) : 1, 1);
          break;
        case 'cafe':
          counters.forEach((c, k) => {
            const p = { x: c.x + DIRS4[c.f]!.x, y: c.y + DIRS4[c.f]!.y };
            if (clear(p.x, p.y)) put('barStool', p.x, p.y, yawOf(c.f), { du: -DIRS4[c.f]!.x * 0.25, dv: -DIRS4[c.f]!.y * 0.25 });
            if (k === 1) put('coffeeMachine', c.x, c.y, yawOf(c.f), { y: TOP.counter, cast: false });
          });
          tables(Math.max(1, Math.floor(area / 12)), 2);
          break;
        case 'grocer': {
          // The drinks fridge against a wall, the cooler by the counter, stock by the shelves.
          let fridge = false;
          for (let y = b.y0 + 1; y < b.y1 && !fridge; y++) {
            for (let x = b.x0 + 1; x < b.x1 && !fridge; x++) {
              const wall = DIRS4.findIndex((d) => tileAt(x + d.x, y + d.y) === 'wall');
              if (wall < 0 || !clear(x, y) || r(x, y, 5) < 0.6) continue;
              put('drinksFridge', x, y, yawOf((wall + 2) % 4), { du: DIRS4[wall]!.x * 0.2, dv: DIRS4[wall]!.y * 0.2 });
              fridge = true;
            }
          }
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
 * A barn's floor (playtest 18/10, #53): the tools leaning against its
 * walls, a wheelbarrow and buckets, on clear ground. Cosmetic.
 */
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
    let tools = 0;
    let barrow = false;
    for (let y = b.y0 + 1; y < b.y1; y++) {
      for (let x = b.x0 + 1; x < b.x1; x++) {
        if (!clear(x, y)) continue;
        const r = (salt: number) => hash(x, y, salt);
        const wall = DIRS4.findIndex((d) => tileAt(x + d.x, y + d.y) === 'wall');
        if (wall >= 0 && tools < 3 && r(1) < 0.3) {
          // Leant against the wall, its handle up to it.
          const d = DIRS4[wall]!;
          put((['pitchfork', 'rake', 'spade'] as const)[tools]!, x, y, (turnTo(wall) * Math.PI) / 2 + (r(2) - 0.5) * 0.4, { du: d.x * 0.36, dv: d.y * 0.36, lean: -0.22 });
          tools++;
          continue;
        }
        if (wall < 0 && !barrow && r(3) < 0.2) {
          put('wheelbarrow', x, y, r(4) * 6.3);
          barrow = true;
          continue;
        }
        if (r(5) < 0.1) put('bucket', x, y, r(6) * 6.3, { du: (r(7) - 0.5) * 0.5, dv: (r(8) - 0.5) * 0.5 });
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
