import * as THREE from 'three';
import type { GameState, Vec2 } from '@bastion/engine';

/** World height of one storey (a wall is exactly one storey tall). */
export const STOREY = 2.3;

/** Floor elevations of the map currently shown, so every helper lands on rooftops. */
let terrain: { width: number; height: number; elev: number[] } | null = null;

export function setTerrain(s: GameState): void {
  terrain = { width: s.width, height: s.height, elev: s.elev };
}

/** World height of the floor of tile `p`. */
export function groundAt(p: Vec2): number {
  if (!terrain || p.x < 0 || p.y < 0 || p.x >= terrain.width || p.y >= terrain.height) return 0;
  return (terrain.elev[p.y * terrain.width + p.x] ?? 0) * STOREY;
}

/** Tile (x, y) maps to the world point at the centre of the tile, `height` above its floor (y up). */
export function tileToWorld(p: Vec2, height = 0): THREE.Vector3 {
  return new THREE.Vector3(p.x + 0.5, groundAt(p) + height, p.y + 0.5);
}

export function worldToTile(v: THREE.Vector3): Vec2 {
  return { x: Math.floor(v.x), y: Math.floor(v.z) };
}
