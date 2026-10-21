import { DIRS4 } from './grid';
import type { Decor, DecorKind } from './types';

// Decor hints: what the generator meant each piece of cover to be (a parked
// car, a fence, a warehouse shelf…) and which way it faces, packed in one
// small number per tile so the layer weighs no more than the ground layer.
//
// `facing` is a DIRS4 index: the way the object's front looks (a car's nose, a
// sofa's seat, the doors of a wardrobe). Objects that come in lines (fences,
// shelf rows, pipes, a counter) face across their line, so a line running
// east–west faces north or south.
//
// An object over several tiles (docs/MAPAS.md, phase 1: a car 1 × 3, a bed
// 1 × 2) says what it is on one of them, its anchor; each of its other tiles
// is a `part` facing the step towards the anchor (objectTiles). A mission
// saved before has none: each object is on one tile, a car on two halves
// (carPartner), a lorry or a container a block of its kind (BLOCK_KINDS).
//
// The codes are kept in saved missions: add new kinds at the end, never
// renumber.

const IDS: Record<DecorKind, number> = {
  carFront: 1,
  carRear: 2,
  planter: 3,
  bin: 4,
  streetTree: 5,
  kiosk: 6,
  fountain: 7,
  fountainCorner: 8,
  statue: 9,
  fence: 10,
  gatePost: 11,
  hay: 12,
  shelf: 13,
  counter: 14,
  table: 15,
  sofa: 16,
  bed: 17,
  desk: 18,
  wardrobe: 19,
  cabinet: 20,
  rack: 21,
  crates: 22,
  barricade: 23,
  pipe: 24,
  tank: 25,
  pylon: 26,
  tree: 27,
  bush: 28,
  rock: 29,
  log: 30,
  rubble: 31,
  stairs: 32,
  breach: 33,
  lootWeapons: 34,
  lootMedical: 35,
  lootAmmo: 36,
  lootFood: 37,
  bus: 38,
  truck: 39,
  logPile: 40,
  waterTower: 41,
  container: 42,
  sign: 43,
  wire: 44,
  sirenCar: 45,
  pallets: 46,
  drums: 47,
  forklift: 48,
  machine: 49,
  silo: 50,
  // The medical centres (PR #1, Ana): numbered after the factory's of the 0.3.0, which saved missions keep.
  hospitalBed: 51,
  medicalShelf: 52,
  gurney: 53,
  medicalCabinet: 54,
  medicalCart: 55,
  wheelchair: 56,
  receptionDesk: 57,
  waitingSeats: 58,
  examTable: 59,
  medicalMonitor: 60,
  xray: 61,
  bedsideTable: 62,
  visitorChair: 63,
  clinicalSink: 64,
  sterilizer: 65,
  ultrasound: 66,
  waitingTable: 67,
  waterDispenser: 68,
  toilet: 69,
  clinicalBin: 70,
  consultDesk: 71,
  // The squares (playtest 2026-10-18, #12).
  bench: 72,
  // A house's rooms (playtest 2026-10-18, #15).
  fridge: 73,
  stove: 74,
  bathtub: 75,
  washbasin: 76,
  tvStand: 77,
  wc: 78,
  // What stands on the floor, on tiles of its own (Arturo, 19/10: «que ocupen»; furnishing.ts).
  armchair: 79,
  coffeeTable: 80,
  floorLamp: 81,
  pottedPlant: 82,
  nightstand: 83,
  diningChair: 84,
  towelRack: 85,
  coatRack: 86,
  cafeTable: 87,
  readingTable: 88,
  drinksFridge: 89,
  wheelbarrow: 90,
  tools: 91,
  hydrant: 92,
  mailbox: 93,
  strewn: 94,
  // Objects over several tiles (docs/MAPAS.md, phase 1): every tile but the anchor.
  part: 95,
  // A block's back gardens (docs/MAPAS.md §5.4, phase 4).
  shed: 96,
  washingLine: 97,
  vegPatch: 98,
  // A room's windows barricaded from inside (playtest 21/10, #20).
  windowBarricade: 99,
};

const KINDS: DecorKind[] = [];
for (const [kind, id] of Object.entries(IDS)) KINDS[id] = kind as DecorKind;

/** Packs a hint; `facing` is a DIRS4 index. */
export function decorCode(kind: DecorKind, facing = 0): Decor {
  return IDS[kind] * 4 + (((facing % 4) + 4) % 4);
}

/** Unpacks a hint; null for none (0, missing, or a kind this version doesn't know). */
export function decorOf(code: Decor | undefined): { kind: DecorKind; facing: number } | null {
  if (!code) return null;
  const kind = KINDS[code >> 2];
  return kind ? { kind, facing: code & 3 } : null;
}

/**
 * For half of a parked car, the tile of its other half (the front is the half
 * the car faces, the rear the one behind it); -1 if `i` is not half of a whole car.
 */
export function carPartner(decor: readonly Decor[], width: number, i: number): number {
  const look = decorOf(decor[i]);
  if (!look || (look.kind !== 'carFront' && look.kind !== 'carRear')) return -1;
  const d = DIRS4[look.facing]!;
  const k = look.kind === 'carFront' ? -1 : 1;
  const x = (i % width) + d.x * k;
  const y = Math.floor(i / width) + d.y * k;
  if (x < 0 || y < 0 || x >= width || y * width >= decor.length) return -1;
  const j = y * width + x;
  const other = decorOf(decor[j]);
  return other && other.facing === look.facing && other.kind === (look.kind === 'carFront' ? 'carRear' : 'carFront') ? j : -1;
}

/**
 * How many tiles each of the generator's objects over several takes
 * (docs/MAPAS.md §4.5–4.6), as [wide, deep]: wide across the way it faces,
 * deep along it. A car's anchor is its nose; a patrol car's or an
 * ambulance's tiles count the one its siren stands on; a lorry is 2 × 6 where
 * it fits and 2 × 4 where not; a van is for the car parks of phase 3.
 */
export const FOOTPRINT = {
  car: [1, 3],
  patrolCar: [1, 3],
  ambulance: [2, 4],
  van: [2, 4],
  tractor: [2, 3],
  truck: [2, 6],
  truckShort: [2, 4],
  container: [2, 5],
  bus: [2, 5],
  bed: [1, 2],
  doubleBed: [2, 2],
  sofa: [2, 1],
  bathtub: [2, 1],
  table: [2, 1],
  hospitalBed: [1, 2],
  gurney: [1, 2],
  shed: [2, 3],
  washingLine: [1, 3],
  vegPatch: [2, 3],
} as const satisfies Record<string, readonly [number, number]>;

/**
 * Objects saved as blocks, every tile of one carrying its kind (playtest
 * 14/10, #25): a lorry (a barn's tractor too), a container, a water tower, a
 * silo, a log pile, a square's bench. The generator never lets two touch, so
 * one is the tiles of its kind joined to each other.
 */
export const BLOCK_KINDS: ReadonlySet<DecorKind> = new Set<DecorKind>(['truck', 'container', 'waterTower', 'silo', 'logPile', 'bench']);

/** The longest way from a part to its anchor (the biggest object, a lorry of 2 × 6, takes 6). */
const REACH = 16;

/** The tile next to tile i, `f` a DIRS4 index; -1 off the map. */
function next(width: number, size: number, i: number, f: number): number {
  const d = DIRS4[f]!;
  const x = (i % width) + d.x;
  const y = Math.floor(i / width) + d.y;
  return x < 0 || y < 0 || x >= width || y * width >= size ? -1 : y * width + x;
}

/** The anchor of the object tile i is a part of (i itself for anything else dressed); -1 for nothing, or a part that leads nowhere. */
export function anchorOf(decor: readonly Decor[], width: number, i: number): number {
  let j = i;
  for (let n = 0; n < REACH && j >= 0; n++) {
    const look = decorOf(decor[j]);
    if (!look) return -1;
    if (look.kind !== 'part') return j;
    j = next(width, decor.length, j, look.facing);
  }
  return -1;
}

/** The tiles of the object anchored on tile `a`: the anchor, then every part that leads to it, nearest first. */
export function objectTiles(decor: readonly Decor[], width: number, a: number): number[] {
  const out = [a];
  for (let k = 0; k < out.length; k++) {
    for (let f = 0; f < 4; f++) {
      const j = next(width, decor.length, out[k]!, f);
      const look = j >= 0 ? decorOf(decor[j]) : null;
      // A part beside it that steps onto it.
      if (look?.kind === 'part' && look.facing === (f + 2) % 4 && !out.includes(j)) out.push(j);
    }
  }
  return out;
}

/**
 * The whole object tile i is (part of), in any of the forms maps have had: an
 * anchor and its parts; a car on two halves (carPartner); a block of its kind
 * (BLOCK_KINDS). Its kind and facing, its anchor and its tiles (the anchor
 * first). Null where nothing is dressed, or for a part that leads nowhere.
 */
export function objectAt(decor: readonly Decor[], width: number, i: number): { kind: DecorKind; facing: number; anchor: number; tiles: number[] } | null {
  const anchor = anchorOf(decor, width, i);
  if (anchor < 0) return null;
  const look = decorOf(decor[anchor])!;
  const tiles = objectTiles(decor, width, anchor);
  if (tiles.length > 1) return { ...look, anchor, tiles };
  if (look.kind === 'carFront' || look.kind === 'carRear') {
    const other = carPartner(decor, width, anchor);
    if (other >= 0) return look.kind === 'carFront' ? { ...look, anchor, tiles: [anchor, other] } : { ...decorOf(decor[other])!, anchor: other, tiles: [other, anchor] };
  }
  if (BLOCK_KINDS.has(look.kind)) {
    const block = [anchor];
    for (let k = 0; k < block.length; k++) {
      for (let f = 0; f < 4; f++) {
        const j = next(width, decor.length, block[k]!, f);
        if (j >= 0 && decorOf(decor[j])?.kind === look.kind && !block.includes(j)) block.push(j);
      }
    }
    block.sort((a, b) => a - b);
    return { kind: look.kind, facing: look.facing, anchor: block[0]!, tiles: block };
  }
  return { ...look, anchor, tiles };
}
