// The Bastion as a map (docs/FASE12.md §11): a clearing in the pines with a
// caravan, and what is built on it. Pure functions over the base's state: the
// campaign pays for them (campaign.ts), the siege fights over them (setup.ts).
import type { FacilityId } from './campaign';
import { NIGHT } from './content';
import { decorCode } from './decor';
import type { MissionLayout } from './mapgen';
import type { Rng } from './rng';
import type { Decor, DecorKind, GroundKind, TileKind, Vec2 } from './types';

export type PieceId =
  // The buildings: the facilities of before, with a place on the map (§11.3).
  | 'caravan'
  | 'radio'
  | 'quarters'
  | 'infirmary'
  | 'workshop'
  | 'garden'
  | 'quarantine'
  | 'memorial'
  | 'range'
  | 'training'
  | 'echoes'
  | 'garage'
  | 'simulation'
  // Defences built by hand (§11.4).
  | 'palisade'
  | 'wall'
  | 'gate'
  | 'sandbags'
  | 'logs'
  | 'tower'
  | 'floodlight'
  | 'wire'
  // And those that defend on their own, and are spent (§11.4).
  | 'mine'
  | 'spikes'
  | 'snare'
  | 'turret';

export type PieceKind = 'building' | 'defence' | 'trap';

/**
 * One tile of a piece, unrotated: what it is in a fight (a tile kind; `ladder`
 * for a tower's way up), and its height in storeys (a tower's deck).
 */
export interface PieceCell {
  dx: number;
  dy: number;
  tile: TileKind;
  elev?: number;
}

export interface PieceDef {
  id: PieceId;
  /** Provisional: the names are Lore's (lore.ts). */
  name: string;
  kind: PieceKind;
  /** Tiles across (x) and deep (y), unrotated. */
  size: [number, number];
  /** Every tile of it, unrotated; missing: all `tile`, on the ground. */
  cells?: PieceCell[];
  /** What it is in a fight, when every tile is the same. */
  tile?: TileKind;
  /** Suministros and piezas. */
  cost: { supplies: number; parts?: number };
  /** What it holds against the dead's blows (a Zombi takes off 3, SIEGE in content.ts). */
  hp: number;
  /** Bastion level it opens at. */
  level: number;
  /** Only one on the base. */
  unique?: boolean;
  /** A building: the facility whose effects it has (campaign.ts FACILITIES), by its level 1–3. */
  facility?: FacilityId;
  /** A trap or a turret: uses before it has to be set again (or reloaded), with piezas. */
  charges?: number;
}

const block = (w: number, h: number, tile: TileKind): PieceCell[] =>
  Array.from({ length: w * h }, (_, i) => ({ dx: i % w, dy: Math.floor(i / w), tile }));

/**
 * What can stand on the base (§11). A building is solid in a fight: nobody goes
 * in, it gives full cover and the dead only beat on it when it is in their way.
 * The costs of the buildings are the facilities' of before, so the campaign's
 * economy stays as it was; those of the defences, a first guess to tune by
 * playing. Sizes as drawn, from Arte's models (docs/MAPAS.md §4.9, phase 6);
 * a base built before keeps what stood at the size it had (BasePiece.size).
 */
export const PIECES: Record<PieceId, PieceDef> = {
  caravan: { id: 'caravan', name: 'Caravana', kind: 'building', size: [5, 2], tile: 'wall', cost: { supplies: 0 }, hp: 40, level: 1, unique: true },
  radio: { id: 'radio', name: 'Sala de radio', kind: 'building', size: [4, 4], tile: 'wall', cost: { supplies: 60 }, hp: 30, level: 1, unique: true, facility: 'relay' },
  quarters: { id: 'quarters', name: 'Barracón', kind: 'building', size: [6, 5], tile: 'wall', cost: { supplies: 40 }, hp: 30, level: 1, facility: 'quarters' },
  infirmary: { id: 'infirmary', name: 'Enfermería', kind: 'building', size: [6, 5], tile: 'wall', cost: { supplies: 60 }, hp: 30, level: 1, unique: true, facility: 'infirmary' },
  workshop: { id: 'workshop', name: 'Taller', kind: 'building', size: [5, 4], tile: 'wall', cost: { supplies: 85 }, hp: 30, level: 1, unique: true, facility: 'workshop' },
  garden: { id: 'garden', name: 'Huerto', kind: 'building', size: [7, 6], tile: 'low', cost: { supplies: 40 }, hp: 20, level: 1, facility: 'garden' },
  quarantine: { id: 'quarantine', name: 'Cuarentena', kind: 'building', size: [4, 4], tile: 'wall', cost: { supplies: 50 }, hp: 30, level: 1, unique: true, facility: 'quarantine' },
  memorial: { id: 'memorial', name: 'Camposanto', kind: 'building', size: [4, 4], tile: 'low', cost: { supplies: 40 }, hp: 20, level: 1, unique: true, facility: 'memorial' },
  range: { id: 'range', name: 'Campo de tiro', kind: 'building', size: [10, 3], tile: 'low', cost: { supplies: 90 }, hp: 20, level: 2, unique: true, facility: 'range' },
  training: { id: 'training', name: 'Centro de entrenamiento', kind: 'building', size: [5, 5], tile: 'wall', cost: { supplies: 140 }, hp: 30, level: 2, unique: true, facility: 'training' },
  simulation: { id: 'simulation', name: 'Sala de mapas', kind: 'building', size: [6, 6], tile: 'wall', cost: { supplies: 100 }, hp: 30, level: 3, unique: true, facility: 'simulation' },
  garage: { id: 'garage', name: 'Chapistería', kind: 'building', size: [7, 5], tile: 'wall', cost: { supplies: 110 }, hp: 30, level: 3, unique: true, facility: 'garage' },
  echoes: { id: 'echoes', name: 'Taller de oficios', kind: 'building', size: [4, 4], tile: 'wall', cost: { supplies: 130 }, hp: 30, level: 5, unique: true, facility: 'echoes' },
  palisade: { id: 'palisade', name: 'Empalizada', kind: 'defence', size: [1, 1], tile: 'wall', cost: { supplies: 6 }, hp: 12, level: 1 },
  wall: { id: 'wall', name: 'Muro', kind: 'defence', size: [1, 1], tile: 'wall', cost: { supplies: 15, parts: 1 }, hp: 36, level: 2 },
  gate: { id: 'gate', name: 'Puerta', kind: 'defence', size: [1, 1], tile: 'door', cost: { supplies: 10 }, hp: 18, level: 1 },
  sandbags: { id: 'sandbags', name: 'Barricada de sacos', kind: 'defence', size: [1, 1], tile: 'low', cost: { supplies: 6 }, hp: 15, level: 1 },
  // Lore (18/10): what is felled, stacked; «Cajas» are the marked loot boxes already.
  logs: { id: 'logs', name: 'Pila de troncos', kind: 'defence', size: [1, 1], tile: 'low', cost: { supplies: 3 }, hp: 9, level: 1 },
  tower: {
    id: 'tower',
    name: 'Torre de vigía',
    kind: 'defence',
    size: [3, 4],
    // The deck a storey up, three by three, and the ladder up to it in front, in the middle.
    cells: [...block(3, 3, 'floor').map((c) => ({ ...c, elev: 1 })), { dx: 1, dy: 3, tile: 'ladder' }],
    cost: { supplies: 25 },
    hp: 30,
    level: 1,
  },
  floodlight: { id: 'floodlight', name: 'Foco', kind: 'defence', size: [1, 1], tile: 'high', cost: { supplies: 12 }, hp: 6, level: 2 },
  wire: { id: 'wire', name: 'Alambre de espino', kind: 'defence', size: [1, 1], tile: 'floor', cost: { supplies: 5 }, hp: 6, level: 2 },
  mine: { id: 'mine', name: 'Mina', kind: 'trap', size: [1, 1], tile: 'floor', cost: { supplies: 5, parts: 1 }, hp: 3, level: 3, charges: 1 },
  spikes: { id: 'spikes', name: 'Foso con pinchos', kind: 'trap', size: [1, 1], tile: 'floor', cost: { supplies: 8 }, hp: 9, level: 2, charges: 3 },
  snare: { id: 'snare', name: 'Cepo', kind: 'trap', size: [1, 1], tile: 'floor', cost: { supplies: 6, parts: 1 }, hp: 6, level: 2, charges: 1 },
  turret: { id: 'turret', name: 'Torreta', kind: 'trap', size: [1, 1], tile: 'floor', cost: { supplies: 40, parts: 4 }, hp: 12, level: 4, unique: false, charges: 6 },
};

/** What each piece measured before the base grew (docs/MAPAS.md phase 6), unturned: what a base built then has standing. */
export const OLD_PIECE_SIZES: Partial<Record<PieceId, [number, number]>> = {
  caravan: [4, 2],
  radio: [3, 3],
  quarters: [4, 3],
  infirmary: [4, 3],
  workshop: [4, 3],
  garden: [4, 4],
  quarantine: [3, 3],
  memorial: [3, 3],
  range: [6, 2],
  training: [4, 3],
  simulation: [3, 3],
  garage: [4, 3],
  echoes: [3, 3],
  tower: [2, 3],
};

/** The watchtower as it was built before the base grew (docs/MAPAS.md phase 6): a deck of two by two, the ladder at a corner. */
const OLD_TOWER: PieceCell[] = [...block(2, 2, 'floor').map((c) => ({ ...c, elev: 1 })), { dx: 0, dy: 2, tile: 'ladder' }];

/** Something standing on the base: where, which way round, and how it is. */
export interface BasePiece {
  id: string;
  piece: PieceId;
  /** Its top-left tile once turned. */
  x: number;
  y: number;
  /** Quarter turns clockwise, 0–3. */
  rot: number;
  /**
   * Its size unturned, when not its kind's: one built before the base grew (docs/MAPAS.md phase 6), as it stood, until
   * it is improved or built again.
   */
  size?: [number, number];
  hp: number;
  /** A building's level, 1–3. */
  level?: number;
  /** A trap's or a turret's uses left; at 0, set again (or reloaded) with piezas. */
  charges?: number;
}

/**
 * The base's ground (§11.1): the clearing and the pines round it, and what is
 * built. `trees` are tile indices (y * width + x), in order.
 */
export interface BaseState {
  width: number;
  height: number;
  trees: number[];
  /** Where a pine was felled and nothing stands yet: a stump to draw, flat, no cover (tile indices, in the order felled). */
  felled?: number[];
  pieces: BasePiece[];
  nextPiece: number;
  /** The Bastion's level, as bought (campaign.ts `level`): what stands on its lot (bastionFootprint). Missing: 1. */
  level?: number;
}

/**
 * The Bastion's lot (Arturo, 18/10 #40: the caravan grows into a building
 * with the level): 9 × 9 round the caravan (docs/MAPAS.md §4.9), from two
 * tiles left and three up of it, where nothing else is built; what isn't the
 * building is its yard.
 */
export const BASTION_LOT = 9;
/** What the Bastion takes up at each stage (two levels each): the caravan, the mobile home, the barracón, the house (Arte; §4.9). */
export const BASTION_STAGES: readonly [number, number][] = [
  [5, 2],
  [7, 3],
  [7, 5],
  [7, 7],
];

/** The lot, from the caravan; null with no caravan. */
export function bastionLot(base: BaseState): { x: number; y: number; w: number; h: number } | null {
  const caravan = base.pieces.find((p) => p.piece === 'caravan');
  return caravan ? { x: caravan.x - 2, y: caravan.y - 3, w: BASTION_LOT, h: BASTION_LOT } : null;
}

/** The stage the Bastion's level shows (0 to 3). */
export function bastionStage(base: Pick<BaseState, 'level'>): number {
  return Math.min(BASTION_STAGES.length - 1, Math.max(0, Math.ceil((base.level ?? 1) / 2) - 1));
}

/** The tiles the Bastion's building stands on, at the middle of its lot (rounded down). */
export function bastionFootprint(base: BaseState): Vec2[] {
  const lot = bastionLot(base);
  if (!lot) return [];
  const [w, h] = BASTION_STAGES[bastionStage(base)]!;
  const x0 = lot.x + Math.floor((lot.w - w) / 2);
  const y0 = lot.y + Math.floor((lot.h - h) / 2);
  const cells: Vec2[] = [];
  for (let y = y0; y < y0 + h; y++) for (let x = x0; x < x0 + w; x++) cells.push({ x, y });
  return cells;
}

/**
 * What stands in the Bastion's yard at each stage (Arte's drawing of it,
 * client basePieces.ts drawBastion, on the lot of 9 × 9: docs/MAPAS.md phase
 * 6), by tile of its lot (x across, y deep, its front the last row): what is
 * always there, and what its second level adds.
 *   - the caravan: the fire with a chair either side, the wood; then its steps,
 *     the table and chairs under its awning by its side (two by two), the
 *     generator by its end;
 *   - the mobile home: the fire and its chairs along the front row, the wood;
 *     then the deck along its front and its steps;
 *   - the barracón: its steps, the fire and its chairs in the front corner, the
 *     wood; then the water tower, the solar panel and the generator up the free
 *     column, sandbags either side of its steps;
 *   - the house (its porch the model's own, inside its 7 × 7): the wood; then
 *     the floodlight in the other corner, sandbags across the front row either
 *     side of the way in, tank traps in the back corners (playtest 20/10 #13).
 */
const BASTION_YARD: { always: [number, number][]; more: [number, number][] }[] = [
  { always: [[5, 7], [4, 7], [6, 7], [0, 8]], more: [[5, 5], [0, 6], [1, 6], [0, 7], [1, 7], [7, 3]] },
  { always: [[5, 8], [4, 8], [6, 8], [0, 8]], more: [[3, 6], [4, 6], [5, 6], [6, 6], [7, 6], [2, 6]] },
  { always: [[4, 7], [7, 8], [6, 8], [8, 8], [0, 8]], more: [[8, 0], [8, 1], [8, 2], [8, 3], [3, 7], [5, 7]] },
  { always: [[0, 8]], more: [[8, 8], [1, 8], [2, 8], [6, 8], [7, 8], [0, 0], [8, 0]] },
];

/**
 * The tiles of the Bastion's yard that what is drawn there stands on (Arturo,
 * 19/10: «que ocupen»; BASTION_YARD), and those of the lot it shuts in, that
 * nobody could get to: in a siege, low cover nobody stands on (baseLayout).
 * The client draws the yard as it always has, and nothing else there.
 */
export function bastionYard(base: Pick<BaseState, 'pieces' | 'level'>): Vec2[] {
  const lot = bastionLot(base as BaseState);
  if (!lot) return [];
  const level = base.level ?? 1;
  const yard = BASTION_YARD[bastionStage(base)]!;
  const solid = new Set(bastionFootprint(base as BaseState).map((p) => `${p.x - lot.x},${p.y - lot.y}`));
  const things = [...yard.always, ...(level % 2 === 0 ? yard.more : [])].map(([x, y]) => `${x},${y}`);
  for (const t of things) solid.add(t);
  // What is left of the lot joins the clearing by its edge, or it is shut in.
  const open = new Set<string>();
  const queue: [number, number][] = [];
  for (let y = 0; y < lot.h; y++) {
    for (let x = 0; x < lot.w; x++) {
      if ((x === 0 || y === 0 || x === lot.w - 1 || y === lot.h - 1) && !solid.has(`${x},${y}`)) {
        open.add(`${x},${y}`);
        queue.push([x, y]);
      }
    }
  }
  while (queue.length) {
    const [x, y] = queue.pop()!;
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]] as const) {
      const k = `${x + dx},${y + dy}`;
      if (x + dx < 0 || y + dy < 0 || x + dx >= lot.w || y + dy >= lot.h || solid.has(k) || open.has(k)) continue;
      open.add(k);
      queue.push([x + dx, y + dy]);
    }
  }
  const out = new Set(things);
  for (let y = 0; y < lot.h; y++) for (let x = 0; x < lot.w; x++) if (!solid.has(`${x},${y}`) && !open.has(`${x},${y}`)) out.add(`${x},${y}`);
  return [...out].map((k) => {
    const [x, y] = k.split(',').map(Number) as [number, number];
    return { x: lot.x + x, y: lot.y + y };
  });
}

/** Whether (x, y) lies on the Bastion's lot. */
export function onBastionLot(base: BaseState, x: number, y: number): boolean {
  const lot = bastionLot(base);
  return !!lot && x >= lot.x && y >= lot.y && x < lot.x + lot.w && y < lot.y + lot.h;
}

/** The base's map, across and deep (docs/MAPAS.md §4.9: 56, a base built on 40 set in its middle). */
export const BASE_MAP = 56;
/** The dead come in by this band at the edge: nothing is built in it. */
export const BASE_EDGE = 2;
/** Suministros felling a tree costs; it gives nothing back (Arturo, 18/10 #41: «solo cuesta recursos y no da nada»). */
export const FELL_COST = 3;
/** Of a piece's cost, what dismantling it gives back. */
export const DISMANTLE_BACK = 0.5;

/** A piece's tiles once turned `rot` quarter turns clockwise and set at (x, y); `size`, one built at another (BasePiece.size). */
export function pieceCells(id: PieceId, x: number, y: number, rot: number, size?: [number, number]): (PieceCell & Vec2)[] {
  const def = PIECES[id];
  const [w, h] = size ?? def.size;
  const cells = size ? (id === 'tower' ? OLD_TOWER : block(w, h, def.tile ?? 'wall')) : (def.cells ?? block(w, h, def.tile ?? 'wall'));
  const r = ((rot % 4) + 4) % 4;
  return cells.map((c) => {
    // Turned about the piece's own box, then set with its top-left at (x, y).
    const [dx, dy] = r === 0 ? [c.dx, c.dy] : r === 1 ? [h - 1 - c.dy, c.dx] : r === 2 ? [w - 1 - c.dx, h - 1 - c.dy] : [c.dy, w - 1 - c.dx];
    return { ...c, dx, dy, x: x + dx, y: y + dy };
  });
}

/** The tiles a piece on the base stands on, at the size it was built (BasePiece.size). */
export function cellsOf(p: BasePiece): (PieceCell & Vec2)[] {
  return pieceCells(p.piece, p.x, p.y, p.rot, p.size);
}

/**
 * The tiles of the turrets, the traps and the wire still standing: ground (floor) on the map, but nobody
 * starts a siege on them (a defender on a turret's tile shared it with the turret).
 */
export function armedTiles(base: BaseState): Set<number> {
  const armed = new Set<number>();
  for (const p of base.pieces) {
    if (p.hp <= 0 || (PIECES[p.piece].kind !== 'trap' && p.piece !== 'wire')) continue;
    for (const c of cellsOf(p)) armed.add(c.y * base.width + c.x);
  }
  return armed;
}

/** The piece standing on a tile, if any. */
export function pieceAt(base: BaseState, x: number, y: number): BasePiece | undefined {
  return base.pieces.find((p) => cellsOf(p).some((c) => c.x === x && c.y === y));
}

export function treeAt(base: BaseState, x: number, y: number): boolean {
  return sortedHas(base.trees, y * base.width + x);
}

function sortedHas(list: number[], n: number): boolean {
  let lo = 0;
  let hi = list.length - 1;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    if (list[mid] === n) return true;
    if (list[mid]! < n) lo = mid + 1;
    else hi = mid - 1;
  }
  return false;
}

/**
 * Why `id` can't go at (x, y) turned `rot`, or null if it can: on the map but
 * off the edge band where the dead come in, on cleared ground (no tree) and
 * free, and only once if it is unique. Costs and the Bastion's level are the
 * campaign's to check.
 */
export function placeBlocker(base: BaseState, id: PieceId, x: number, y: number, rot: number): string | null {
  const def = PIECES[id];
  if (def.unique && base.pieces.some((p) => p.piece === id)) return 'Ya está en la base.';
  for (const c of pieceCells(id, x, y, rot)) {
    if (c.x < BASE_EDGE || c.y < BASE_EDGE || c.x >= base.width - BASE_EDGE || c.y >= base.height - BASE_EDGE) return 'Ahí no: es el borde del claro.';
    if (treeAt(base, c.x, c.y)) return 'Hay árboles: tala antes.';
    if (pieceAt(base, c.x, c.y)) return 'Ahí ya hay algo.';
    if (id !== 'caravan' && onBastionLot(base, c.x, c.y)) return 'Ahí no: ese sitio es para el Bastión, que crece con cada nivel.';
  }
  return null;
}

/** Puts `id` at (x, y) turned `rot`, whole (a building at level 1, a trap set). The caller has checked placeBlocker. */
export function placePiece(base: BaseState, id: PieceId, x: number, y: number, rot: number): BasePiece {
  const def = PIECES[id];
  const piece: BasePiece = {
    id: `b${base.nextPiece++}`,
    piece: id,
    x,
    y,
    rot: ((rot % 4) + 4) % 4,
    hp: def.hp,
    ...(def.kind === 'building' ? { level: 1 } : {}),
    ...(def.charges ? { charges: def.charges } : {}),
  };
  base.pieces.push(piece);
  // Built over, a stump is gone.
  if (base.felled?.length) {
    const under = new Set(pieceCells(id, x, y, rot).map((c) => c.y * base.width + c.x));
    base.felled = base.felled.filter((i) => !under.has(i));
  }
  return piece;
}

/**
 * Whether a piece built before the base grew (BasePiece.size) now fits at its kind's size where it stands, as it is
 * turned: what raising it makes it, if so (campaign.ts 'raise').
 */
export function grownFits(base: BaseState, p: BasePiece): boolean {
  if (!p.size) return false;
  return pieceCells(p.piece, p.x, p.y, p.rot).every((c) => {
    if (c.x < BASE_EDGE || c.y < BASE_EDGE || c.x >= base.width - BASE_EDGE || c.y >= base.height - BASE_EDGE || treeAt(base, c.x, c.y)) return false;
    const there = pieceAt(base, c.x, c.y);
    return (!there || there === p) && (p.piece === 'caravan' || !onBastionLot(base, c.x, c.y));
  });
}

/**
 * A base built on a smaller map, on one `size` across (docs/MAPAS.md phase 6, decided by Arturo 19/10: «con la base
 * guardada en el centro y lo construido con su tamaño»): all of it set in the middle, what stood at the size it was
 * built (BasePiece.size), pines round it as thick as freshBase grows them, the tracks out to each side clear. What
 * stands on the Bastion's bigger lot comes down: returned, for the caller to give back what it cost.
 */
export function growBase(base: BaseState, size: number, rng: Rng): BasePiece[] {
  const old = base.width;
  const o = Math.floor((size - old) / 2);
  if (o <= 0) return [];
  const shift = (i: number) => (Math.floor(i / old) + o) * size + (i % old) + o;
  const trees = new Set(base.trees.map(shift));
  const thickness = 0.5 + rng.next() * 0.25;
  const step = 6;
  const gw = Math.ceil(size / step) + 2;
  const grid = Array.from({ length: gw * gw }, () => rng.next());
  const smooth = (t: number) => t * t * (3 - 2 * t);
  const noise = (x: number, y: number) => {
    const fx = x / step;
    const fy = y / step;
    const gx = Math.floor(fx);
    const gy = Math.floor(fy);
    const tx = smooth(fx - gx);
    const ty = smooth(fy - gy);
    const at = (a: number, b: number) => grid[b * gw + a]!;
    return (at(gx, gy) * (1 - tx) + at(gx + 1, gy) * tx) * (1 - ty) + (at(gx, gy + 1) * (1 - tx) + at(gx + 1, gy + 1) * tx) * ty;
  };
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const inside = x >= o && y >= o && x < o + old && y < o + old;
      if (!inside && !onTrack(size, size, x, y) && noise(x, y) < thickness) trees.add(y * size + x);
    }
  }
  base.trees = [...trees].sort((a, b) => a - b);
  base.felled = base.felled?.map(shift);
  for (const p of base.pieces) {
    p.x += o;
    p.y += o;
    const was = OLD_PIECE_SIZES[p.piece];
    if (p.piece !== 'caravan' && was && (was[0] !== PIECES[p.piece].size[0] || was[1] !== PIECES[p.piece].size[1])) p.size = [...was];
  }
  base.width = size;
  base.height = size;
  const down = base.pieces.filter((p) => p.piece !== 'caravan' && cellsOf(p).some((t) => onBastionLot(base, t.x, t.y)));
  base.pieces = base.pieces.filter((p) => !down.includes(p));
  // Nothing grows on the Bastion's lot.
  if (bastionLot(base)) base.trees = base.trees.filter((i) => !onBastionLot(base, i % size, Math.floor(i / size)));
  return down;
}

/** Why the tree at (x, y) can't be felled, or null if it can. */
export function fellBlocker(base: BaseState, x: number, y: number): string | null {
  return treeAt(base, x, y) ? null : 'Ahí no hay ningún árbol.';
}

/** Fells the tree at (x, y): the ground is clear. 1 if there was one, else 0 (what it costs: FELL_COST each). */
export function fellTree(base: BaseState, x: number, y: number): number {
  const i = y * base.width + x;
  const at = base.trees.indexOf(i);
  if (at < 0) return 0;
  base.trees.splice(at, 1);
  (base.felled ??= []).push(i);
  return 1;
}

/** What mending `p` costs: its cost in proportion to what it has lost, a suministro at least. */
export function repairCost(p: BasePiece): { supplies: number; parts: number } {
  const def = PIECES[p.piece];
  const lost = 1 - p.hp / def.hp;
  if (lost <= 0) return { supplies: 0, parts: 0 };
  return { supplies: Math.max(1, Math.ceil(def.cost.supplies * lost)), parts: Math.ceil((def.cost.parts ?? 0) * lost) };
}

/** What setting a spent trap again (or reloading a turret) costs: piezas, one at least. */
export function rearmCost(p: BasePiece): number {
  const def = PIECES[p.piece];
  if (!def.charges || (p.charges ?? 0) >= def.charges) return 0;
  return Math.max(1, def.cost.parts ?? 1);
}

/** What dismantling `p` gives back: half its cost, if it is whole; the caravan never goes. */
export function dismantleBlocker(p: BasePiece): string | null {
  return p.piece === 'caravan' ? 'La caravana no se desmonta.' : null;
}

export function dismantleRefund(p: BasePiece): number {
  const def = PIECES[p.piece];
  return Math.floor(def.cost.supplies * DISMANTLE_BACK * (p.hp / def.hp));
}

/** Pieces touching `p` side by side (§11.3: «junto a» is next to it on the map). */
export function besidePieces(base: BaseState, p: BasePiece): BasePiece[] {
  // The caravan is its whole lot (nothing is built on it): beside the lot is beside the caravan.
  const caravan = base.pieces.find((q) => q.piece === 'caravan');
  const lot = bastionLot(base);
  const lotCells: Vec2[] = [];
  if (lot) for (let y = lot.y; y < lot.y + lot.h; y++) for (let x = lot.x; x < lot.x + lot.w; x++) lotCells.push({ x, y });
  const mine = p === caravan ? lotCells : cellsOf(p);
  const at = (x: number, y: number) => (caravan && onBastionLot(base, x, y) ? caravan : pieceAt(base, x, y));
  const out = new Set<BasePiece>();
  for (const c of mine) {
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]] as const) {
      const q = at(c.x + dx, c.y + dy);
      if (q && q !== p) out.add(q);
    }
  }
  return [...out];
}

/** On one of the base's tracks: a band three tiles wide through the middle, each way, out to every side (La Lata comes and goes by them). */
export function onTrack(width: number, height: number, x: number, y: number): boolean {
  return Math.abs(x - (width - 1) / 2) <= 1 || Math.abs(y - (height - 1) / 2) <= 1;
}

/**
 * A new base (§11.1): the caravan in the middle of a clearing in the pines.
 * Every campaign's ground is its own: sometimes thicker pines, sometimes more
 * clearings, though few big ones; the middle always clear round the caravan,
 * and a track out to each side, so the dead (and La Lata) can get in.
 */
export function freshBase(rng: Rng, size = BASE_MAP): BaseState {
  const width = size;
  const height = size;
  // Smooth noise: a coarse grid of random values, blended across each cell.
  const step = 6;
  const gw = Math.ceil(width / step) + 2;
  const grid = Array.from({ length: gw * gw }, () => rng.next());
  const at = (gx: number, gy: number) => grid[gy * gw + gx]!;
  const smooth = (t: number) => t * t * (3 - 2 * t);
  const noise = (x: number, y: number) => {
    const fx = x / step;
    const fy = y / step;
    const gx = Math.floor(fx);
    const gy = Math.floor(fy);
    const tx = smooth(fx - gx);
    const ty = smooth(fy - gy);
    const top = at(gx, gy) * (1 - tx) + at(gx + 1, gy) * tx;
    const bottom = at(gx, gy + 1) * (1 - tx) + at(gx + 1, gy + 1) * tx;
    return top * (1 - ty) + bottom * ty;
  };
  // How thick the pines are this time: between half and three quarters of the ground beyond the clearing.
  const thickness = 0.5 + rng.next() * 0.25;
  const cx = (width - 1) / 2;
  const cy = (height - 1) / 2;
  // A bigger clearing on a bigger map (docs/MAPAS.md §4.9: 12–16 tiles across from the middle, with its glades).
  const clearing = Math.round((6 + rng.int(3)) * (size / 40));
  const trees: number[] = [];
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const d = Math.hypot(x - cx, y - cy);
      if (d <= clearing || onTrack(width, height, x, y)) continue;
      // Thicker away from the clearing; the noise makes the stands and the glades.
      const fall = Math.min(1, (d - clearing) / 6);
      if (noise(x, y) < thickness * fall) trees.push(y * width + x);
    }
  }
  const base: BaseState = { width, height, trees, felled: [], pieces: [], nextPiece: 1 };
  const [w, h] = PIECES.caravan.size;
  placePiece(base, 'caravan', Math.floor(cx - w / 2) + 1, Math.floor(cy - h / 2) + 1, 0);
  return base;
}

// ------------------------------------------------------------- the siege

/** What each piece looks like when the client has nothing better (decor.ts); the base's own drawing is Arte's, from GameState.base. */
const PIECE_LOOK: Partial<Record<PieceId, DecorKind>> = { sandbags: 'barricade', logs: 'logPile', palisade: 'fence', wire: 'wire' };

/** Places round the caravan for the squad of a siege: every soldier both players can have. */
const BASE_DEFENDERS = 24;

/**
 * The base as a battlefield for its siege (docs/FASE12.md §11.5): the pines
 * standing, every piece on its tiles (a building solid, a tower's deck a
 * storey up), what each holds against the dead's blows, and the squad's
 * places round the caravan. Traps and turrets are set by the siege itself
 * (setup.ts), from GameState.base.
 */
export function baseLayout(base: BaseState): MissionLayout {
  const { width, height } = base;
  const tiles = new Array<TileKind>(width * height).fill('floor');
  const elev = new Array<number>(width * height).fill(0);
  const decor = new Array<Decor>(width * height).fill(0);
  // The tracks are trodden earth (a yard, ground.ts); the rest, open ground.
  const ground = Array.from({ length: width * height }, (_, i): GroundKind => (onTrack(width, height, i % width, Math.floor(i / width)) ? 3 : 0));
  for (const i of base.trees) {
    tiles[i] = 'high';
    decor[i] = decorCode('tree', i % 4);
  }
  const breakables: Record<number, number> = {};
  for (const p of base.pieces) {
    // What a siege broke and nobody has mended is a gap (the client draws its ruin from GameState.base).
    if (p.hp <= 0) continue;
    const look = PIECE_LOOK[p.piece];
    // The caravan stands for the Bastion: the building of its stage, solid, holding what the caravan holds.
    const cells: { x: number; y: number; tile: TileKind; elev?: number }[] = p.piece === 'caravan' ? bastionFootprint(base).map((t) => ({ ...t, tile: 'wall' as TileKind })) : cellsOf(p);
    for (const c of cells) {
      const i = c.y * width + c.x;
      tiles[i] = c.tile;
      elev[i] = c.elev ?? 0;
      if (look) decor[i] = decorCode(look, (p.rot + 1) % 4);
      // What stands in the way holds its piece's health; the ground of a trap or a deck doesn't.
      if (c.tile === 'wall' || c.tile === 'door' || c.tile === 'low' || c.tile === 'high') breakables[i] = p.hp;
    }
  }
  // What stands in its yard takes its tile (19/10): the fire, the chairs, the wood, the porch… low, and nobody starts there.
  if (base.pieces.some((p) => p.piece === 'caravan' && p.hp > 0)) {
    for (const p of bastionYard(base)) if (tiles[p.y * width + p.x] === 'floor') tiles[p.y * width + p.x] = 'low';
  }
  // The squad round the Bastion: the free tiles nearest the middle of its lot.
  const lot = bastionLot(base);
  const middle = lot ? { x: lot.x + lot.w / 2, y: lot.y + lot.h / 2 } : { x: width / 2, y: height / 2 };
  const armed = armedTiles(base);
  const spawns: Vec2[] = [];
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) if (tiles[y * width + x] === 'floor' && elev[y * width + x] === 0 && !armed.has(y * width + x)) spawns.push({ x, y });
  spawns.sort((a, b) => Math.hypot(a.x - middle.x, a.y - middle.y) - Math.hypot(b.x - middle.x, b.y - middle.y) || a.y - b.y || a.x - b.x);
  return {
    id: 'base',
    name: 'El Bastión',
    biome: 'wilds',
    width,
    height,
    tiles,
    elev,
    ground,
    decor,
    // Everyone fit defends (§11.5): room for both players' whole rosters.
    spawns: spawns.slice(0, BASE_DEFENDERS),
    pods: [],
    item: null,
    terminal: null,
    objectiveSpot: null,
    evacZone: [],
    siege: true,
    breakables,
    // The floodlights light the night twice as far as a street light (§11.4; a siege by night).
    lights: base.pieces.filter((p) => p.piece === 'floodlight' && p.hp > 0).map((p) => ({ x: p.x, y: p.y, radius: NIGHT.floodlight })),
  };
}
