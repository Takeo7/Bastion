import * as THREE from 'three';
import { inBounds, storeysAt, tileIndex, type GameState, type TileKind } from '@bastion/engine';
import { groundAt, STOREY } from './coords';

/**
 * Where the camera stands for the shot over the shoulder (the attack that kills, playtest 20/10 #11): behind the
 * attacker's head, to its right, on the line of fire carried back, so the attacker fills the left third from the
 * waist up and the target is in the middle. Never in a wall, a slab, a car or a window: it comes in towards the
 * head, goes over the other shoulder or follows the line of fire less steeply; and only where it sees the target.
 */

/**
 * How far from the attacker's head the camera stands (m), and the head over the chest the shot is aimed from (the
 * figures are ~1.4 m tall): 1.7 m behind it and 0.6 m to its right, at the first of TURNS.
 */
const REACH = 1.8;
const HEAD = 0.22;
/**
 * Round from straight behind the head (rad, + to its right), in this order: over the right shoulder, the left one,
 * nearer the head; then more beside it, for a back to a wall.
 */
const TURNS = [0.34, -0.34, 0.17, -0.17, 0.6, -0.6];
/** The attacker is kept this near the middle (rad, across): from beside it, the view turns towards it. */
const KEEP = THREE.MathUtils.degToRad(22);
/** Steepest it follows the line of fire up or down (rad): a roof shooting into the street. */
const STEEP = THREE.MathUtils.degToRad(40);
/** It keeps this far from anything solid (m), and comes no nearer the head than NEAREST (m) or there is no shot. */
const ROOM = 0.22;
const NEAREST = 0.85;
/** Anyone else's body, for the camera to keep out of (m): this wide round its tile's middle, this tall. */
const BODY = 0.3;
const BODY_TOP = 1.5;
/** Bodies further than this from the head (m) are left out at once. */
const BODY_NEAR = 3.5;
/** A window is seen through between its sill and its lintel (storeys over its floor), from the house's to the bunker's. */
const SILL = 0.38;
const LINTEL = 0.78;
/** The slabs, this thick under their top (m): a ceiling is solid that far down. */
const SLAB = 0.2;
/** Steps along the lines it checks (m), and how much of the line nearest the target it leaves out (its own cover). */
const STEP = 0.05;
const SIGHT_STEP = 0.1;
const SIGHT_SPARE = 0.6;

/** Height above its floor of what stands on a tile, for the camera (storeys): a window is a wall with a hole in it. */
const SOLID: Record<TileKind, number> = {
  floor: 0,
  ladder: 0,
  doorOpen: 0,
  low: 0.4,
  high: 0.8,
  console: 0.8,
  wall: 1,
  door: 1,
  window: 1,
  windowBroken: 1,
  // A roof nobody stands on: a pitched one fills the storey above its slab.
  roof: 1,
};

/**
 * What the camera may not see the target through (storeys): walls, doors and roofs all the way; cover only in the
 * half of the way nearer the camera (the attacker's, a parapet under a roof's edge), for the target's own only hides
 * part of it.
 */
const OPAQUE: Record<TileKind, number> = { ...SOLID, low: 0, high: 0, console: 0, window: 0, windowBroken: 0 };
const OPAQUE_NEAR: Record<TileKind, number> = { ...SOLID, window: 0, windowBroken: 0 };
/** On the line of fire carried back, less steep, or higher over it (m: over a parapet): [how steep, how much higher]. */
const RISES: readonly (readonly [number, number])[] = [[1, 0], [0.5, 0], [0, 0], [1, 0.5], [1, 1]];

export interface ShoulderShot {
  pos: THREE.Vector3;
  look: THREE.Vector3;
}

/**
 * The shot from `from` (the attacker's chest, where it strikes or shoots from) at `to` (the target's chest), or null
 * where there is no room for one: the camera stays as it is. Out of everyone's body but `skip`'s (the two of them).
 */
export function overShoulder(s: GameState, from: THREE.Vector3, to: THREE.Vector3, skip: readonly string[] = []): ShoulderShot | null {
  const flat = new THREE.Vector3(to.x - from.x, 0, to.z - from.z);
  const run = flat.length();
  if (run < 0.01) return null;
  flat.divideScalar(run);
  const right = new THREE.Vector3(-flat.z, 0, flat.x);
  const head = from.clone().setY(from.y + HEAD);
  const bodies: THREE.Vector3[] = [];
  for (const u of s.units) {
    if (!u.alive || skip.includes(u.id) || Math.hypot(u.pos.x + 0.5 - head.x, u.pos.y + 0.5 - head.z) > BODY_NEAR) continue;
    bodies.push(new THREE.Vector3(u.pos.x + 0.5, groundAt(u.pos), u.pos.y + 0.5));
  }
  const pitch = THREE.MathUtils.clamp(Math.atan2(to.y - head.y, run), -STEEP, STEEP);
  let best: { pos: THREE.Vector3; score: number } | null = null;
  for (let n = 0; n < TURNS.length; n++) {
    const turn = TURNS[n]!;
    for (const [tilt, lift] of RISES) {
      const p = pitch * tilt;
      const back = Math.cos(turn) * REACH;
      const want = head.clone().addScaledVector(flat, -Math.cos(p) * back).addScaledVector(right, Math.sin(turn) * REACH);
      want.y += lift - Math.sin(p) * back;
      const reach = head.distanceTo(want);
      const free = freeLength(s, bodies, head, want) / reach;
      if (free * reach < NEAREST) continue;
      const pos = head.clone().lerp(want, free);
      if (!sees(s, pos, to)) continue;
      // Room counts most; then the order of TURNS, the line of fire, and the least height over it.
      const score = free - n * 0.03 - (1 - tilt) * 0.02 - lift * 0.05;
      if (!best || score > best.score) best = { pos, score };
    }
  }
  if (!best) return null;
  const pos = best.pos;
  // From beside the attacker, the view turns from the target towards it, until it is KEEP from the middle.
  const ahead = Math.atan2(to.x - pos.x, to.z - pos.z);
  const off = THREE.MathUtils.euclideanModulo(Math.atan2(head.x - pos.x, head.z - pos.z) - ahead + Math.PI, Math.PI * 2) - Math.PI;
  const swing = Math.sign(off) * Math.max(0, Math.abs(off) - KEEP);
  const look = to.clone().sub(pos).applyAxisAngle(THREE.Object3D.DEFAULT_UP, swing).add(pos);
  return { pos, look };
}

/** How far from `a` towards `b` the camera can go with room round it (m), out of the walls and of `bodies` (their feet). */
function freeLength(s: GameState, bodies: readonly THREE.Vector3[], a: THREE.Vector3, b: THREE.Vector3): number {
  const length = a.distanceTo(b);
  const p = new THREE.Vector3();
  let free = 0;
  for (let d = STEP; d <= length + 1e-6; d += STEP) {
    p.lerpVectors(a, b, Math.min(1, d / length));
    if (crowded(s, p) || bodies.some((f) => Math.hypot(p.x - f.x, p.z - f.z) < BODY + ROOM && p.y > f.y - ROOM && p.y < f.y + BODY_TOP + ROOM)) break;
    free = Math.min(d, length);
  }
  return free;
}

/** Whether anything solid stands within ROOM of `p`. */
function crowded(s: GameState, p: THREE.Vector3): boolean {
  return solidAt(s, p.x, p.y, p.z, SOLID) || solidAt(s, p.x + ROOM, p.y, p.z, SOLID) || solidAt(s, p.x - ROOM, p.y, p.z, SOLID) ||
    solidAt(s, p.x, p.y, p.z + ROOM, SOLID) || solidAt(s, p.x, p.y, p.z - ROOM, SOLID) ||
    solidAt(s, p.x, p.y + ROOM, p.z, SOLID) || solidAt(s, p.x, p.y - ROOM, p.z, SOLID);
}

/** Whether the camera at `pos` sees the target's chest `to`: nothing opaque between (OPAQUE), its own cover left out. */
function sees(s: GameState, pos: THREE.Vector3, to: THREE.Vector3): boolean {
  const length = pos.distanceTo(to) - SIGHT_SPARE;
  const p = new THREE.Vector3();
  for (let d = SIGHT_STEP; d < length; d += SIGHT_STEP) {
    p.lerpVectors(pos, to, d / (length + SIGHT_SPARE));
    if (solidAt(s, p.x, p.y, p.z, d < (length + SIGHT_SPARE) / 2 ? OPAQUE_NEAR : OPAQUE)) return false;
  }
  return true;
}

/**
 * Whether world point (x, y, z) is inside something solid: below the ground, what stands on a tile up to its height
 * (on the ground, a wall up to its building's top), or a slab, a storey's floor and the ceiling under it. Out of the
 * map, everything is.
 */
function solidAt(s: GameState, x: number, y: number, z: number, heights: Record<TileKind, number>): boolean {
  const tx = Math.floor(x);
  const ty = Math.floor(z);
  if (!inBounds(s, tx, ty)) return true;
  const i = tileIndex(s, tx, ty);
  const h = y / STOREY;
  const base = s.elev[i] ?? 0;
  if (h < base) return true;
  const kind = s.tiles[i]!;
  // A block of the outline, or a building's wall with nothing on it upstairs, stands as tall as its building.
  const tall = kind === 'wall' && (s.levels?.[0]?.tiles[i] ?? null) === null ? Math.max(1, storeysAt(s, tx, ty)) : 1;
  if (h < base + heights[kind] * tall || framed(kind, h - base, heights)) return true;
  if (!s.levels) return false;
  // The storey this height is on, and the slab just above (its ceiling) or just below (its floor).
  const k = Math.floor(h);
  for (const level of [k, k + 1]) {
    const slab = level >= 1 ? s.levels[level - 1]?.tiles[i] ?? null : null;
    if (slab !== null && h > level - SLAB / STOREY && h < level + 0.02) return true;
  }
  const on = k >= 1 ? s.levels[k - 1]?.tiles[i] ?? null : null;
  return on !== null && (h < k + heights[on] || framed(on, h - k, heights));
}

/** Whether a height `up` storeys over its floor is in the wall round a window seen through (under its sill, over its lintel). */
function framed(kind: TileKind, up: number, heights: Record<TileKind, number>): boolean {
  return (kind === 'window' || kind === 'windowBroken') && heights[kind] === 0 && up < 1 && (up < SILL || up > LINTEL);
}
