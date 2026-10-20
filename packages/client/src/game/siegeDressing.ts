import * as THREE from 'three';
import { cellsOf, MAPS, nodeOf, PIECES, SIEGE, type GameState } from '@bastion/engine';
import { RAISED_TOWER } from './buildings';

// A siege's base dressed (docs/FASE12.md §10, El Bastión): how worn each of
// its doors and barricades is (what it holds left against what it held), what
// each tile was when the map was made (a gate or a barricade that has given
// way is open ground now, and leaves its remains), and where its watchtowers
// stand. The drawing is mapView's, buildings.ts' and doors.ts'.

/** How worn a door or barricade is: 0 whole, 1 knocked about (two thirds or less of what it holds), 2 about to go (a third or less). */
export type Wear = 0 | 1 | 2;

export function wearStage(left: number, full: number): Wear {
  return left * 3 <= full ? 2 : left * 3 <= full * 2 ? 1 : 0;
}

/** What tile (x, y) was when the hand-made map was drawn (its legend: '+' a door, 'b' a barricade), or null. */
export function madeAs(s: GameState, x: number, y: number): string | null {
  return MAPS[s.mapId]?.rows[y]?.[x] ?? null;
}

/**
 * Each ground tile with a door or barricade standing, and its wear (GameState.breakables, by node). One that has
 * given way stays there at 0, but its tile is open ground now: it is the map's remains (madeAs), not worn. On the
 * new base (docs/FASE12.md §11), against the health of the piece the tile is part of.
 */
export function wearOf(s: GameState): Map<number, Wear> {
  const out = new Map<number, Wear>();
  if (!s.breakables) return out;
  const area = s.width * s.height;
  const full = new Map<number, number>();
  for (const p of s.base?.pieces ?? []) for (const c of cellsOf(p)) full.set(c.y * s.width + c.x, PIECES[p.piece].hp);
  for (let i = 0; i < area; i++) {
    const left = s.breakables[nodeOf(s, { x: i % s.width, y: Math.floor(i / s.width), z: 0 })];
    if (left === undefined || s.tiles[i] === 'floor') continue;
    const door = s.tiles[i] === 'door' || s.tiles[i] === 'doorOpen';
    out.set(i, wearStage(left, full.get(i) ?? (door ? SIEGE.door : SIEGE.barricade)));
  }
  return out;
}

/** The way into the base from tile (x, y) on its wall: towards the middle of the map (unit vector, on the ground). */
export function inwards(s: GameState, x: number, y: number): { x: number; z: number } {
  const dx = s.width / 2 - (x + 0.5);
  const dz = s.height / 2 - (y + 0.5);
  return Math.abs(dx) > Math.abs(dz) ? { x: Math.sign(dx), z: 0 } : { x: 0, z: Math.sign(dz) };
}

/** A watchtower: the middle of its deck's tiles, the tile it is placed on (for a rebuild's area), and its turn. */
export interface Tower {
  x: number;
  z: number;
  tile: number;
  /** Turn about y putting the guard tower's open side (where its stairs were, −z) towards its ladder. */
  yaw: number;
}

/** A siege's watchtowers: each group of RAISED_TOWER tiles, turned to the ladder beside it. */
export function towersOf(s: GameState, raised: Uint8Array): Tower[] {
  const { width: w, height: h } = s;
  const seen = new Uint8Array(w * h);
  const out: Tower[] = [];
  for (let start = 0; start < w * h; start++) {
    if (seen[start] || raised[start] !== RAISED_TOWER) continue;
    const tiles = [start];
    seen[start] = 1;
    for (let k = 0; k < tiles.length; k++) {
      const x = tiles[k]! % w;
      const y = (tiles[k]! - x) / w;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]] as const) {
        const nx = x + dx;
        const ny = y + dy;
        const j = ny * w + nx;
        if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[j] || raised[j] !== RAISED_TOWER) continue;
        seen[j] = 1;
        tiles.push(j);
      }
    }
    const cx = tiles.reduce((a, i) => a + (i % w), 0) / tiles.length + 0.5;
    const cz = tiles.reduce((a, i) => a + Math.floor(i / w), 0) / tiles.length + 0.5;
    // The ladder up to it: the nearest ladder tile to its middle.
    let best: { x: number; z: number } | null = null;
    for (let i = 0; i < w * h; i++) {
      if (s.tiles[i] !== 'ladder') continue;
      const lx = (i % w) + 0.5;
      const lz = Math.floor(i / w) + 0.5;
      if (!best || Math.hypot(lx - cx, lz - cz) < Math.hypot(best.x - cx, best.z - cz)) best = { x: lx, z: lz };
    }
    const d = best ? new THREE.Vector2(best.x - cx, best.z - cz) : new THREE.Vector2(0, -1);
    // Square to the deck: the ladder's side.
    const side = Math.abs(d.x) > Math.abs(d.y) ? { x: Math.sign(d.x), z: 0 } : { x: 0, z: Math.sign(d.y) };
    const middle = tiles.reduce((a, i) => (Math.hypot((i % w) + 0.5 - cx, Math.floor(i / w) + 0.5 - cz) < Math.hypot((a % w) + 0.5 - cx, Math.floor(a / w) + 0.5 - cz) ? i : a), tiles[0]!);
    out.push({ x: cx, z: cz, tile: middle, yaw: Math.atan2(-side.x, -side.z) });
  }
  return out;
}
