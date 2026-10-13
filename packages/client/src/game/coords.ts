import * as THREE from 'three';
import type { GameState, Place, Vec2 } from '@bastion/engine';

/** World height of one storey (a wall is exactly one storey tall). */
export const STOREY = 2.3;

/** Floor elevations of the map currently shown, so every helper lands on rooftops and on a building's storeys. */
let terrain: { width: number; height: number; elev: number[] } | null = null;

export function setTerrain(s: GameState): void {
  terrain = { width: s.width, height: s.height, elev: s.elev };
}

/**
 * World height of the floor of place `p`: on the ground (no `z`, or 0) the
 * tile's elevation; on storey `z` of a building (docs/FASE6-PLAN.md), that
 * storey's slab (buildings stand on ground at elevation 0: heightAt).
 */
export function groundAt(p: Place): number {
  if (p.z) return p.z * STOREY;
  if (!terrain || p.x < 0 || p.y < 0 || p.x >= terrain.width || p.y >= terrain.height) return 0;
  return (terrain.elev[p.y * terrain.width + p.x] ?? 0) * STOREY;
}

/** Place `p` maps to the world point at the centre of its tile, `height` above its floor (y up). */
export function tileToWorld(p: Place, height = 0): THREE.Vector3 {
  return new THREE.Vector3(p.x + 0.5, groundAt(p) + height, p.y + 0.5);
}

export function worldToTile(v: THREE.Vector3): Vec2 {
  return { x: Math.floor(v.x), y: Math.floor(v.z) };
}
