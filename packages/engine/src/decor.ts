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
