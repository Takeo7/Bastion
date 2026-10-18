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
 * playing.
 */
export const PIECES: Record<PieceId, PieceDef> = {
  caravan: { id: 'caravan', name: 'Caravana', kind: 'building', size: [4, 2], tile: 'wall', cost: { supplies: 0 }, hp: 40, level: 1, unique: true },
  radio: { id: 'radio', name: 'Sala de radio', kind: 'building', size: [3, 3], tile: 'wall', cost: { supplies: 60 }, hp: 30, level: 1, unique: true, facility: 'relay' },
  quarters: { id: 'quarters', name: 'Barracón', kind: 'building', size: [4, 3], tile: 'wall', cost: { supplies: 40 }, hp: 30, level: 1, facility: 'quarters' },
  infirmary: { id: 'infirmary', name: 'Enfermería', kind: 'building', size: [4, 3], tile: 'wall', cost: { supplies: 60 }, hp: 30, level: 1, unique: true, facility: 'infirmary' },
  workshop: { id: 'workshop', name: 'Taller', kind: 'building', size: [4, 3], tile: 'wall', cost: { supplies: 85 }, hp: 30, level: 1, unique: true, facility: 'workshop' },
  garden: { id: 'garden', name: 'Huerto', kind: 'building', size: [4, 4], tile: 'low', cost: { supplies: 40 }, hp: 20, level: 1, unique: true, facility: 'garden' },
  quarantine: { id: 'quarantine', name: 'Cuarentena', kind: 'building', size: [3, 3], tile: 'wall', cost: { supplies: 50 }, hp: 30, level: 1, unique: true, facility: 'quarantine' },
  memorial: { id: 'memorial', name: 'Camposanto', kind: 'building', size: [3, 3], tile: 'low', cost: { supplies: 40 }, hp: 20, level: 1, unique: true, facility: 'memorial' },
  range: { id: 'range', name: 'Campo de tiro', kind: 'building', size: [6, 2], tile: 'low', cost: { supplies: 90 }, hp: 20, level: 2, unique: true, facility: 'range' },
  training: { id: 'training', name: 'Centro de entrenamiento', kind: 'building', size: [4, 3], tile: 'wall', cost: { supplies: 140 }, hp: 30, level: 2, unique: true, facility: 'training' },
  simulation: { id: 'simulation', name: 'Sala de mapas', kind: 'building', size: [3, 3], tile: 'wall', cost: { supplies: 100 }, hp: 30, level: 3, unique: true, facility: 'simulation' },
  garage: { id: 'garage', name: 'Chapistería', kind: 'building', size: [4, 3], tile: 'wall', cost: { supplies: 110 }, hp: 30, level: 3, unique: true, facility: 'garage' },
  echoes: { id: 'echoes', name: 'Taller de oficios', kind: 'building', size: [3, 3], tile: 'wall', cost: { supplies: 130 }, hp: 30, level: 5, unique: true, facility: 'echoes' },
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
    size: [2, 3],
    // The deck a storey up, and the ladder up to it in front.
    cells: [
      { dx: 0, dy: 0, tile: 'floor', elev: 1 },
      { dx: 1, dy: 0, tile: 'floor', elev: 1 },
      { dx: 0, dy: 1, tile: 'floor', elev: 1 },
      { dx: 1, dy: 1, tile: 'floor', elev: 1 },
      { dx: 0, dy: 2, tile: 'ladder' },
    ],
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

/** Something standing on the base: where, which way round, and how it is. */
export interface BasePiece {
  id: string;
  piece: PieceId;
  /** Its top-left tile once turned. */
  x: number;
  y: number;
  /** Quarter turns clockwise, 0–3. */
  rot: number;
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
 * with the level): 6 × 6 round the caravan, from one tile left and two up of
 * it, where nothing else is built; what isn't the building is its yard.
 */
export const BASTION_LOT = 6;
/** What the Bastion takes up at each stage (two levels each): the caravan, the mobile home, the barracón, the house (Arte). */
export const BASTION_STAGES: readonly [number, number][] = [
  [4, 2],
  [6, 3],
  [5, 4],
  [6, 5],
];

/** The lot, from the caravan; null with no caravan. */
export function bastionLot(base: BaseState): { x: number; y: number; w: number; h: number } | null {
  const caravan = base.pieces.find((p) => p.piece === 'caravan');
  return caravan ? { x: caravan.x - 1, y: caravan.y - 2, w: BASTION_LOT, h: BASTION_LOT } : null;
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

/** Whether (x, y) lies on the Bastion's lot. */
export function onBastionLot(base: BaseState, x: number, y: number): boolean {
  const lot = bastionLot(base);
  return !!lot && x >= lot.x && y >= lot.y && x < lot.x + lot.w && y < lot.y + lot.h;
}

/** The base's map, across and deep. */
export const BASE_MAP = 40;
/** The dead come in by this band at the edge: nothing is built in it. */
export const BASE_EDGE = 2;
/** Suministros felling a tree costs; it gives nothing back (Arturo, 18/10 #41: «solo cuesta recursos y no da nada»). */
export const FELL_COST = 3;
/** Of a piece's cost, what dismantling it gives back. */
export const DISMANTLE_BACK = 0.5;

/** A piece's tiles once turned `rot` quarter turns clockwise and set at (x, y). */
export function pieceCells(id: PieceId, x: number, y: number, rot: number): (PieceCell & Vec2)[] {
  const def = PIECES[id];
  const [w, h] = def.size;
  const cells = def.cells ?? block(w, h, def.tile ?? 'wall');
  const r = ((rot % 4) + 4) % 4;
  return cells.map((c) => {
    // Turned about the piece's own box, then set with its top-left at (x, y).
    const [dx, dy] = r === 0 ? [c.dx, c.dy] : r === 1 ? [h - 1 - c.dy, c.dx] : r === 2 ? [w - 1 - c.dx, h - 1 - c.dy] : [c.dy, w - 1 - c.dx];
    return { ...c, dx, dy, x: x + dx, y: y + dy };
  });
}

/** The piece standing on a tile, if any. */
export function pieceAt(base: BaseState, x: number, y: number): BasePiece | undefined {
  return base.pieces.find((p) => pieceCells(p.piece, p.x, p.y, p.rot).some((c) => c.x === x && c.y === y));
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
  const mine = p === caravan ? lotCells : pieceCells(p.piece, p.x, p.y, p.rot);
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
  const clearing = 6 + rng.int(3);
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
    const cells: { x: number; y: number; tile: TileKind; elev?: number }[] = p.piece === 'caravan' ? bastionFootprint(base).map((t) => ({ ...t, tile: 'wall' as TileKind })) : pieceCells(p.piece, p.x, p.y, p.rot);
    for (const c of cells) {
      const i = c.y * width + c.x;
      tiles[i] = c.tile;
      elev[i] = c.elev ?? 0;
      if (look) decor[i] = decorCode(look, (p.rot + 1) % 4);
      // What stands in the way holds its piece's health; the ground of a trap or a deck doesn't.
      if (c.tile === 'wall' || c.tile === 'door' || c.tile === 'low' || c.tile === 'high') breakables[i] = p.hp;
    }
  }
  // The squad round the Bastion: the free tiles nearest the middle of its lot.
  const lot = bastionLot(base);
  const middle = lot ? { x: lot.x + lot.w / 2, y: lot.y + lot.h / 2 } : { x: width / 2, y: height / 2 };
  const spawns: Vec2[] = [];
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) if (tiles[y * width + x] === 'floor' && elev[y * width + x] === 0) spawns.push({ x, y });
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
