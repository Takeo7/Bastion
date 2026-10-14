import * as THREE from 'three';
import { DIRS4, type GameState, type TileKind, type Vec2 } from '@bastion/engine';
import { STOREY } from './coords';
import { FLOOR, type GroundMap } from './ground';
import type { PropId } from './syntyManifest';

// The street's dressing (reference block: Synty's captures are «very
// dressed»): what a town left behind, placed from the map and always the
// same for a map. Cosmetic only, and drawn merged with the rest of the map
// (batch.ts), so it costs no draw calls. Nothing here may read as cover:
//  - tall things are slim and stand by the kerb, near a tile's corner, where
//    nobody stands: street lights over the road, power poles with their wires
//    along straight streets, a stop sign at a block's corner, now and then a
//    hydrant or a mailbox;
//  - what lies about is flat: bags and tyres against the façades, a pallet,
//    a bike, weeds in the cracks of the paving and in the gutters.
// Piles of rubbish big enough to hide behind are cover (PROP_SETS: cityLow).

export interface Dressing {
  id: PropId;
  matrix: THREE.Matrix4;
  /** Casts a shadow (the flat things don't: they'd only cost). */
  cast: boolean;
}

/** Stable pseudo-random value in [0, 1). */
function hash(x: number, y: number, salt: number): number {
  let h = Math.imul(x + 11, 374761393) + Math.imul(y + 5, 668265263) + Math.imul(salt + 17, 2147483647);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

/** Tiles between street lights along a kerb, and between power poles (a wire's length). */
const LIGHT_EVERY = 7;
const POLE_EVERY = 10;

const up = new THREE.Vector3(0, 1, 0);
const q = new THREE.Quaternion();

/** A prop at (x, y, z), its own +x turned towards (dx, dz), `scale` along its +x (a stretched wire). */
function placed(id: PropId, x: number, y: number, z: number, dx: number, dz: number, cast: boolean, scale = 1, size = 1): Dressing {
  q.setFromAxisAngle(up, Math.atan2(-dz, dx));
  return { id, cast, matrix: new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), q, new THREE.Vector3(scale * size, size, size)) };
}

/**
 * The dressing of a map's streets and pavements (towns and facilities; the
 * wilds have their grass and only their lamps). The street lights stand where
 * the engine says (`lamps`: the map's own lights, which light tiles at night),
 * else every few tiles along the kerbs (older saves).
 */
export function dressStreets(s: GameState, ground: GroundMap, density = 1, lamps: readonly Vec2[] | null = null): Dressing[] {
  const out: Dressing[] = [];
  const { width: w, height: h } = s;
  // The engine's lights: a street light on each, near the kerb, its arm over the road.
  for (const p of lamps ?? []) {
    const k = DIRS4.findIndex((d) => {
      const x = p.x + d.x;
      const y = p.y + d.y;
      return x >= 0 && y >= 0 && x < w && y < h && ground.kinds[y * w + x] === FLOOR.STREET;
    });
    const d = DIRS4[k < 0 ? 0 : k]!;
    const along = { x: -d.y, y: d.x };
    out.push(placed('streetLight', p.x + 0.5 + d.x * 0.4 + along.x * 0.4, (s.elev[p.y * w + p.x] ?? 0) * STOREY, p.y + 0.5 + d.y * 0.4 + along.y * 0.4, d.x, d.y, true));
  }
  if (s.biome === 'wilds') return out;
  const kind = (x: number, y: number) => (x < 0 || y < 0 || x >= w || y >= h ? -1 : ground.kinds[y * w + x]!);
  const tile = (x: number, y: number): TileKind | null => (x < 0 || y < 0 || x >= w || y >= h ? null : s.tiles[y * w + x]!);
  const level = (x: number, y: number) => s.elev[y * w + x] ?? 0;
  const free = (x: number, y: number) => tile(x, y) === 'floor' && level(x, y) === 0;
  const street = (x: number, y: number) => kind(x, y) === FLOOR.STREET;
  const paving = (x: number, y: number) => kind(x, y) === FLOOR.OPEN && !ground.lawn(y * w + x);
  const wallish = (x: number, y: number) => {
    const t = tile(x, y);
    return t === 'wall' || t === 'window' || t === 'windowBroken' || t === 'door' || t === 'doorOpen';
  };
  const taken: { x: number; y: number }[] = [...(lamps ?? [])];
  const near = (x: number, y: number, r: number) => taken.some((p) => Math.abs(p.x - x) + Math.abs(p.y - y) < r);

  // Power poles: along straight kerbs over a wire and a bit long, on about half of them, wired pole to pole.
  for (const [dx, dy] of [
    [1, 0],
    [0, 1],
  ] as const) {
    const lines = dx ? h : w;
    const steps = dx ? w : h;
    for (const sign of [-1, 1]) {
      // The street on this side of the kerb.
      const side = dx ? { x: 0, y: sign } : { x: sign, y: 0 };
      for (let line = 0; line < lines; line++) {
        let run: { x: number; y: number }[] = [];
        const flush = () => {
          if (run.length >= POLE_EVERY + 3 && hash(run[0]!.x, run[0]!.y, 3 + sign) < 0.5) {
            let last: THREE.Vector3 | null = null;
            for (let k = 1; k < run.length - 1; k += POLE_EVERY) {
              const t = run[k]!;
              const p = new THREE.Vector3(t.x + 0.5 + side.x * 0.4 - dx * 0.4, 0, t.y + 0.5 + side.y * 0.4 - dy * 0.4);
              out.push(placed('powerPole', p.x, 0, p.z, dx, dy, true));
              if (last) out.push(placed('powerLine', last.x, 0, last.z, dx, dy, false, p.distanceTo(last) / POLE_EVERY));
              last = p;
              taken.push(t);
            }
          }
          run = [];
        };
        for (let k = 0; k < steps; k++) {
          const x = dx ? k : line;
          const y = dx ? line : k;
          if (paving(x, y) && free(x, y) && street(x + side.x, y + side.y)) run.push({ x, y });
          else flush();
        }
        flush();
      }
    }
  }

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (!free(x, y)) continue;
      const r = (salt: number) => hash(x, y, salt);
      const cx = x + 0.5;
      const cz = y + 0.5;
      if (paving(x, y)) {
        const toStreet = DIRS4.filter((d) => street(x + d.x, y + d.y));
        if (toStreet.length) {
          const d = toStreet[0]!;
          const along = { x: -d.y, y: d.x };
          // A block's corner (the street on two sides): a stop sign.
          if (toStreet.length >= 2 && r(1) < 0.45 && !near(x, y, 3)) {
            const e = toStreet[1]!;
            out.push(placed('stopSign', cx + (d.x + e.x) * 0.38, 0, cz + (d.y + e.y) * 0.38, d.x + e.x, d.y + e.y, true));
            taken.push({ x, y });
            continue;
          }
          // Street lights along the kerb, the arm over the road (where the engine places none).
          if (!lamps && r(2) < 1 / LIGHT_EVERY + 0.04 && !near(x, y, LIGHT_EVERY - 2)) {
            out.push(placed('streetLight', cx + d.x * 0.4 + along.x * 0.4, 0, cz + d.y * 0.4 + along.y * 0.4, d.x, d.y, true));
            taken.push({ x, y });
            continue;
          }
          if (r(3) < 0.035 * density && !near(x, y, 2)) {
            out.push(placed(r(4) < 0.6 ? 'hydrant' : 'mailbox', cx + d.x * 0.36 - along.x * 0.36, 0, cz + d.y * 0.36 - along.y * 0.36, -d.x, -d.y, true));
            taken.push({ x, y });
            continue;
          }
        }
        // Against a façade: a few bags, rubble fallen from it, a tyre or a pallet, now and then.
        const toWall = DIRS4.filter((d) => wallish(x + d.x, y + d.y));
        if (toWall.length && r(5) < 0.22 * density) {
          const d = toWall[0]!;
          const along = { x: -d.y, y: d.x };
          const n = 1 + Math.floor(r(6) * 3);
          for (let k = 0; k < n; k++) {
            const u = (r(10 + k) - 0.5) * 0.7;
            const pick = r(20 + k);
            const id: PropId = pick < 0.55 ? (['trashBag', 'trashBag2', 'trashBag3'] as const)[Math.floor(r(30 + k) * 3)]! : pick < 0.78 ? 'pebbles' : r(40 + k) < 0.5 ? 'tire' : 'pallet';
            const yaw = r(50 + k) * Math.PI * 2;
            out.push(placed(id, cx + d.x * 0.3 + along.x * u, 0, cz + d.y * 0.3 + along.y * u, Math.cos(yaw), Math.sin(yaw), id !== 'pallet' && id !== 'tire' && id !== 'pebbles', 1, 0.8 + r(60 + k) * 0.3));
          }
          continue;
        }
      }
      // Weeds in the cracks of the paving and in the gutters; a bike or a tyre in the road.
      if ((paving(x, y) || street(x, y)) && r(7) < 0.05 * density) {
        const yaw = r(8) * Math.PI * 2;
        const id: PropId = r(9) < 0.75 ? 'tuftLow' : 'tuft';
        out.push(placed(id, cx + (r(11) - 0.5) * 0.6, 0, cz + (r(12) - 0.5) * 0.6, Math.cos(yaw), Math.sin(yaw), false, 1, 0.7 + r(13) * 0.5));
      } else if (street(x, y) && r(14) < 0.012 * density) {
        const yaw = r(15) * Math.PI * 2;
        out.push(placed(r(16) < 0.5 ? 'bike' : 'tire', cx + (r(17) - 0.5) * 0.5, 0, cz + (r(18) - 0.5) * 0.5, Math.cos(yaw), Math.sin(yaw), true));
      }
    }
  }
  return out;
}

/**
 * Beyond the map's edge, where nobody walks: the streets that leave the map
 * go on with what was left on them, a wrecked bus or a van or a car, along
 * the street and a few tiles out (the fog fades it into the haze).
 */
export function dressEdges(s: GameState, ground: GroundMap): Dressing[] {
  if (s.biome !== 'city') return [];
  const out: Dressing[] = [];
  const { width: w, height: h } = s;
  const street = (x: number, y: number) => ground.kinds[y * w + x] === FLOOR.STREET;
  // Each side: along it, the runs of street tiles at the edge (a street leaving the map).
  const sides = [
    { n: w, at: (k: number) => ({ x: k, y: 0 }), out: { x: 0, y: -1 } },
    { n: h, at: (k: number) => ({ x: w - 1, y: k }), out: { x: 1, y: 0 } },
    { n: w, at: (k: number) => ({ x: k, y: h - 1 }), out: { x: 0, y: 1 } },
    { n: h, at: (k: number) => ({ x: 0, y: k }), out: { x: -1, y: 0 } },
  ];
  for (const side of sides) {
    let start = -1;
    for (let k = 0; k <= side.n; k++) {
      const p = k < side.n ? side.at(k) : null;
      if (p && street(p.x, p.y)) {
        if (start < 0) start = k;
        continue;
      }
      if (start >= 0 && k - start >= 2) {
        const a = side.at(start);
        const b = side.at(k - 1);
        const r = (salt: number) => hash(a.x * 3 + side.out.x, a.y * 3 + side.out.y, salt);
        if (r(1) < 0.75) {
          // In one lane, nose out or in, a few tiles beyond the edge.
          const lane = r(2) < 0.5 ? 0.25 : 0.75;
          const cx = a.x + 0.5 + (b.x - a.x) * lane + side.out.x * (3.5 + r(3) * 2);
          const cz = a.y + 0.5 + (b.y - a.y) * lane + side.out.y * (3.5 + r(3) * 2);
          const id: PropId = r(4) < 0.4 ? 'bus' : r(4) < 0.75 ? 'van' : 'carRusted';
          const flip = r(5) < 0.5 ? 1 : -1;
          // Their length runs along +x: turned along the street, a little askew.
          const yaw = r(6) * 0.3 - 0.15;
          const dx = side.out.x * flip * Math.cos(yaw) - side.out.y * Math.sin(yaw);
          const dz = side.out.y * flip * Math.cos(yaw) + side.out.x * Math.sin(yaw);
          out.push(placed(id, cx, 0, cz, dx, dz, true));
        }
      }
      start = -1;
    }
  }
  return out;
}
