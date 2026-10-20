import * as THREE from 'three';
import { decorOf, DIRS4, type GameState, type TileKind, type Vec2 } from '@bastion/engine';
import { STOREY } from './coords';
import { FLOOR, type GroundMap } from './ground';
import type { PropId } from './syntyManifest';

// The street's dressing (reference block: Synty's captures are «very
// dressed»): what a town left behind, placed from the map and always the
// same for a map. Cosmetic only, and drawn merged with the rest of the map
// (batch.ts), so it costs no draw calls. Nothing here may read as cover:
//  - tall things are slim and stand by the kerb, near a tile's corner, where
//    nobody stands: street lights over the road, power poles with their wires
//    along straight streets, a stop sign at a block's corner;
//  - what lies about is flat or small, below the knee: bags and tyres
//    against the façades, a pallet, a bike, weeds in the cracks of the
//    paving and in the gutters.
// Piles of rubbish big enough to hide behind are cover (PROP_SETS: cityLow).
// The hydrants and mailboxes stand on tiles of their own, where the engine
// puts them (furnishing.ts, Arturo 19/10: «que ocupen»): drawn here, by the
// kerb as before, while their tile stands.

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
export function placed(id: PropId, x: number, y: number, z: number, dx: number, dz: number, cast: boolean, scale = 1, size = 1): Dressing {
  q.setFromAxisAngle(up, Math.atan2(-dz, dx));
  return { id, cast, matrix: new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), q, new THREE.Vector3(scale * size, size, size)) };
}

/**
 * Where the tall things by a kerb stand, from the middle of their tile (tiles): towards the street `KERB_OUT`,
 * along it `KERB_ALONG`. A tile's corner (0.4, 0.4) put them on the line of a diagonal step (0.14 from the
 * corner, none from the line); here they are 0.18 or more from every line a step between tile middles takes
 * (straight or diagonal), about as far as anything on open ground can be (the most is 0.21).
 */
const KERB_OUT = 0.47;
const KERB_ALONG = 0.21;

/**
 * Half the size of the props the map's edge is shut with (tiles, along their own x and z, as syntyProps fits
 * them: measured on the models), so that none of them reaches over the edge tile, where units walk.
 */
const HALF: Partial<Record<PropId, readonly [number, number]>> = {
  rubble1: [0.475, 0.402], rubble2: [0.475, 0.423], razorWire: [0.5, 0.316], trashPile: [0.343, 0.525], tirePile: [0.261, 0.5],
  bush: [0.45, 0.237], boulder: [0.475, 0.665], rock: [0.443, 0.475], cliff: [0.2, 0.7], cliff2: [0.237, 0.7], log: [0.412, 0.5],
  sheetFence: [0.878, 0.124], sheetFence2: [0.836, 0.113], concreteFence: [0.833, 0.083], milBarrierTall: [0.203, 0.5],
  container: [0.988, 2.425], containerBlue: [0.988, 2.425], containerOrange: [0.988, 2.425], debrisPile: [2.704, 1.266],
  debrisPile2: [2.099, 1.252], bus: [3.809, 1.079], sandbags: [0.5, 0.279], barricade1: [0.5, 0.063], carWreck: [0.41, 0.975],
  carRusted: [0.41, 0.975], tire: [0.275, 0.275],
};
/** What stands past the edge keeps this much clear of the edge tile (tiles). */
const EDGE_CLEAR = 0.02;

/**
 * How far past the middle of an edge tile (along `out`) prop `id` has to stand, placed with its own +x
 * towards (dx, dz), `scale` along it and `size` all over, to keep off that tile: its outer side (0.5), and as
 * far again as the prop reaches that way.
 */
function clearOf(id: PropId, dx: number, dz: number, out: Vec2, scale = 1, size = 1): number {
  const half = HALF[id];
  if (!half) return 0;
  const l = Math.hypot(dx, dz) || 1;
  // Its own x is (dx, dz); its own z, (-dz, dx) (placed's turn).
  const along = Math.abs((dx * out.x + dz * out.y) / l);
  const across = Math.abs((-dz * out.x + dx * out.y) / l);
  return 0.5 + (half[0] * scale * along + half[1] * across) * size + EDGE_CLEAR;
}

/**
 * The dressing of a map's streets and pavements (towns and facilities; the
 * wilds have their grass and only their lamps). The street lights stand where
 * the engine says (`lamps`: the map's own lights, which light tiles at night),
 * else every few tiles along the kerbs (older saves). With `area` (a rebuild
 * of a few chunks: mapView), only what stands on its tiles (each thing stands
 * on the tile it is placed from), the same as the whole map gives there: the
 * spacing of the poles, stop signs and street lights is still worked out over
 * the whole map, the rest only there.
 */
export function dressStreets(s: GameState, ground: GroundMap, density = 1, lamps: readonly Vec2[] | null = null, area: Uint8Array | null = null): Dressing[] {
  const out: Dressing[] = [];
  const { width: w, height: h } = s;
  const dress = (x: number, y: number) => !area || area[y * w + x] !== 0;
  // The engine's lights: a street light on each, near the kerb, its arm over the road.
  for (const p of lamps ?? []) {
    if (!dress(p.x, p.y)) continue;
    const k = DIRS4.findIndex((d) => {
      const x = p.x + d.x;
      const y = p.y + d.y;
      return x >= 0 && y >= 0 && x < w && y < h && ground.kinds[y * w + x] === FLOOR.STREET;
    });
    const d = DIRS4[k < 0 ? 0 : k]!;
    const along = { x: -d.y, y: d.x };
    out.push(placed('streetLight', p.x + 0.5 + d.x * KERB_OUT + along.x * KERB_ALONG, (s.elev[p.y * w + p.x] ?? 0) * STOREY, p.y + 0.5 + d.y * KERB_OUT + along.y * KERB_ALONG, d.x, d.y, true));
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
  /** Tiles a tall thing stands on (lamps, poles, hydrants, stop signs): the next keeps its distance. */
  const taken = new Uint8Array(w * h);
  const take = (x: number, y: number) => void (taken[y * w + x] = 1);
  for (const p of lamps ?? []) take(p.x, p.y);
  /** Something taken less than `r` tiles away, walking (on the map: all of them stand on it). */
  const near = (x: number, y: number, r: number) => {
    for (let dy = 1 - r; dy < r; dy++) {
      const ty = y + dy;
      if (ty < 0 || ty >= h) continue;
      const reach = r - 1 - Math.abs(dy);
      for (let tx = Math.max(0, x - reach); tx <= Math.min(w - 1, x + reach); tx++) if (taken[ty * w + tx]) return true;
    }
    return false;
  };

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
            let lastTile = run[0]!;
            for (let k = 1; k < run.length - 1; k += POLE_EVERY) {
              const t = run[k]!;
              const p = new THREE.Vector3(t.x + 0.5 + side.x * KERB_OUT - dx * KERB_ALONG, 0, t.y + 0.5 + side.y * KERB_OUT - dy * KERB_ALONG);
              if (dress(t.x, t.y)) out.push(placed('powerPole', p.x, 0, p.z, dx, dy, true));
              // The wire hangs from the pole before.
              if (last && dress(lastTile.x, lastTile.y)) out.push(placed('powerLine', last.x, 0, last.z, dx, dy, false, p.distanceTo(last) / POLE_EVERY));
              last = p;
              lastTile = t;
              take(t.x, t.y);
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

  // The engine's hydrants and mailboxes, on the pavement by the street: towards the kerb in the middle of their tile,
  // a little narrowed (19/10: by its corner, it stood in the way of a step past it on the diagonal).
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const look = tile(x, y) === 'low' ? decorOf(s.decor?.[y * w + x]) : null;
      if (look?.kind !== 'hydrant' && look?.kind !== 'mailbox') continue;
      const d = DIRS4[look.facing]!;
      if (dress(x, y)) out.push(placed(look.kind, x + 0.5 + d.x * 0.18, 0, y + 0.5 + d.y * 0.18, -d.x, -d.y, true, 1, 0.85));
      take(x, y);
    }
  }

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (!free(x, y)) continue;
      // Off the area, only where the stop signs and street lights go (each keeps the next ones away).
      const here = dress(x, y);
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
            if (here) out.push(placed('stopSign', cx + d.x * KERB_OUT + e.x * KERB_ALONG, 0, cz + d.y * KERB_OUT + e.y * KERB_ALONG, d.x + e.x, d.y + e.y, true));
            take(x, y);
            continue;
          }
          // Street lights along the kerb, the arm over the road (where the engine places none).
          if (!lamps && r(2) < 1 / LIGHT_EVERY + 0.04 && !near(x, y, LIGHT_EVERY - 2)) {
            if (here) out.push(placed('streetLight', cx + d.x * KERB_OUT + along.x * KERB_ALONG, 0, cz + d.y * KERB_OUT + along.y * KERB_ALONG, d.x, d.y, true));
            take(x, y);
            continue;
          }
        }
        if (!here) continue;
        // Against a façade: a few bags, rubble fallen from it, a tyre or a pallet, now and then. Small, below the knee
        // (19/10): the low bags only, at most their own size; the tall one would stand in the way where one walks.
        const toWall = DIRS4.filter((d) => wallish(x + d.x, y + d.y));
        if (toWall.length && r(5) < 0.22 * density) {
          const d = toWall[0]!;
          const along = { x: -d.y, y: d.x };
          const n = 1 + Math.floor(r(6) * 3);
          for (let k = 0; k < n; k++) {
            const u = (r(10 + k) - 0.5) * 0.7;
            const pick = r(20 + k);
            const id: PropId = pick < 0.55 ? (['trashBag', 'trashBag2'] as const)[Math.floor(r(30 + k) * 2)]! : pick < 0.78 ? 'pebbles' : r(40 + k) < 0.5 ? 'tire' : 'pallet';
            const yaw = r(50 + k) * Math.PI * 2;
            out.push(placed(id, cx + d.x * 0.3 + along.x * u, 0, cz + d.y * 0.3 + along.y * u, Math.cos(yaw), Math.sin(yaw), id !== 'pallet' && id !== 'tire' && id !== 'pebbles', 1, 0.75 + r(60 + k) * 0.25));
          }
          continue;
        }
      }
      if (!here) continue;
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
 * What shuts a street where it leaves the map, from tile `a` to tile `b`
 * along the edge (both at the edge), just past it (`out`): picked by `r`.
 */
export function shut(a: Vec2, b: Vec2, out: Vec2, kind: number, r: (salt: number) => number): Dressing[] {
  const list: Dressing[] = [];
  const along = { x: Math.sign(b.x - a.x), y: Math.sign(b.y - a.y) };
  const width = Math.abs(b.x - a.x) + Math.abs(b.y - a.y) + 1;
  // Half a tile past the edge's outer side.
  const at = (t: number, beyond = 1.1) => ({ x: a.x + 0.5 + along.x * t + out.x * beyond, z: a.y + 0.5 + along.y * t + out.y * beyond });
  const mid = (width - 1) / 2;
  if (kind === 0) {
    // A checkpoint: Military's T-walls across the way, a tyre or two by its ends.
    for (let t = 0; t < width; t++) {
      const p = at(t, 1.05 + (r(20 + t) - 0.5) * 0.12);
      // Their length (the model's z) along the edge.
      list.push(placed('milBarrierTall', p.x, 0, p.z, -along.y + (r(30 + t) - 0.5) * 0.08, along.x + (r(30 + t) - 0.5) * 0.08, true));
    }
    for (const t of [-0.8, width - 0.2]) {
      const p = at(t, 1.4);
      list.push(placed('tire', p.x, 0, p.z, 1, 0, true));
    }
  } else if (kind === 1) {
    // A bus across the street, sandbags where it doesn't reach.
    const p = at(mid, Math.max(1.6, clearOf('bus', along.x, along.y, out)));
    list.push(placed('bus', p.x, 0, p.z, along.x, along.y, true));
    for (const t of [-0.6, width - 0.4]) {
      const q = at(t, 0.9);
      if (width > 6) list.push(placed('sandbags', q.x, 0, q.z, along.x, along.y, true));
    }
  } else {
    // Burnt-out cars nose to tail across it, planks nailed between them (off the edge tile: they reached 0.27 over it).
    for (let t = 0.5; t < width; t += 2.6) {
      const flip = r(50 + t) < 0.5 ? 1 : -1;
      const id: PropId = r(60 + t) < 0.5 ? 'carWreck' : 'carRusted';
      const p = at(Math.min(t + 0.5, width - 1), Math.max(1.35 + (r(40 + t) - 0.5) * 0.3, clearOf(id, along.x, along.y, out)));
      list.push(placed(id, p.x, 0, p.z, along.x * flip, along.y * flip, true));
    }
    for (let t = 0; t < width; t += 2.6) {
      const p = at(t, 0.85);
      list.push(placed('barricade1', p.x, 0, p.z, along.x, along.y, true));
    }
  }
  return list;
}

/**
 * Along an open stretch of the map's edge (pavement, a square, a yard), from
 * `a` to `b`, what keeps anybody from going on (playtest 18/10, #32): the
 * heaps of a block that came down, what is left of it behind them; a
 * hoarding of sheet metal; a precast wall; a row of T-walls; shipping
 * containers end to end. Right past the edge, so it reads as the map's end;
 * `ends` tiles more at each end (a corner's square, or a little over the
 * next stretch).
 */
export function fenceOff(a: Vec2, b: Vec2, out: Vec2, kind: number, ends: [number, number], r: (salt: number) => number, look: EdgeLook = { wired: false, pines: false }): Dressing[] {
  const list: Dressing[] = [];
  const along = { x: Math.sign(b.x - a.x) || Math.abs(out.y), y: Math.sign(b.y - a.y) || Math.abs(out.x) };
  const from = -0.5 - ends[0];
  const length = Math.abs(b.x - a.x) + Math.abs(b.y - a.y) + 1 + ends[0] + ends[1];
  const at = (t: number, beyond: number) => ({ x: a.x + 0.5 + along.x * t + out.x * beyond, z: a.y + 0.5 + along.y * t + out.y * beyond });
  // Pieces `size` tiles long from one end to the other, as many as cover it, a little closer where it doesn't divide.
  const row = (size: number, put: (t: number, i: number) => void) => {
    const n = Math.max(1, Math.ceil(length / size - 0.15));
    for (let i = 0; i < n; i++) put(from + (length / n) * (i + 0.5), i);
  };
  const turn = (salt: number, by: number) => (r(salt) - 0.5) * by;
  // Nothing reaches over the edge tile, where units walk (they stood 0.25 to 0.3 past its middle: inside it).
  const off = (id: PropId, beyond: number, dx: number, dz: number, scale = 1, size = 1) => Math.max(beyond, clearOf(id, dx, dz, out, scale, size));
  if (kind === 0) {
    // A block come down: its heaps along the edge, a wall of it still up, the whole ruin where there is room.
    row(4.6, (t, i) => {
      const flip = r(80 + i) < 0.5 ? 1 : -1;
      const id: PropId = r(90 + i) < 0.5 ? 'debrisPile' : 'debrisPile2';
      const [hx, hz] = [along.x * flip - out.x * turn(100 + i, 0.3), along.y * flip - out.y * turn(100 + i, 0.3)];
      const p = at(t, off(id, 1.9 + turn(70 + i, 0.4), hx, hz));
      const heap = placed(id, p.x, -0.05, p.z, hx, hz, true);
      // Lower than they come: a storey's worth of rubble, not a hill of it.
      heap.matrix.multiply(new THREE.Matrix4().makeScale(1, 0.7, 1));
      list.push(heap);
    });
    if (length >= 8) {
      const p = at(from + length * (0.35 + r(110) * 0.3), 4.2);
      list.push(placed('ruinPile', p.x, 0, p.z, along.x, along.y, true));
    }
    row(3.1, (t, i) => {
      if (r(120 + i) < 0.5) return;
      const id: PropId = r(140 + i) < 0.5 ? 'rubble1' : 'rubble2';
      const [rx, rz] = [Math.cos(r(150 + i) * 6.3), Math.sin(r(150 + i) * 6.3)];
      const p = at(t + turn(130 + i, 1), off(id, 0.65, rx, rz));
      list.push(placed(id, p.x, 0, p.z, rx, rz, true));
    });
  } else if (kind === 1) {
    // A hoarding of sheet metal, put up in a hurry: its panels a little askew.
    row(1.7, (t, i) => {
      const p = at(t, 0.8 + turn(160 + i, 0.12));
      const k = turn(170 + i, 0.1);
      list.push(placed(r(180 + i) < 0.5 ? 'sheetFence' : 'sheetFence2', p.x, 0, p.z, along.x - out.x * k, along.y - out.y * k, true));
    });
  } else if (kind === 2) {
    // A precast wall, panel by panel, rubbish at its foot; a facility's with coils of razor wire before it. The
    // wire and the rubbish clear of the edge tile, the wall behind them (it stood at 0.75, they at 0.3 and 0.25).
    const wire = clearOf('razorWire', along.x, along.y, out, 1.62);
    // The rubbish with its length along the wall (it stood across it).
    const heap = clearOf('trashPile', out.x, out.y, out);
    const front = look.wired ? wire + (wire - 0.5 - EDGE_CLEAR) : heap + (heap - 0.5 - EDGE_CLEAR);
    const wall = Math.max(0.75, front + (HALF.concreteFence![1] + EDGE_CLEAR));
    row(1.62, (t, i) => {
      // Its origin is at its left end.
      const p = at(t - 0.83, wall);
      list.push(placed('concreteFence', p.x, 0, p.z, along.x, along.y, true));
      if (look.wired) {
        const q = at(t, wire);
        list.push(placed('razorWire', q.x, 0, q.z, along.x, along.y, false, 1.62));
      } else if (r(190 + i) < 0.3) {
        const id: PropId = r(210 + i) < 0.5 ? 'trashPile' : 'tirePile';
        const q = at(t + turn(200 + i, 0.6), clearOf(id, out.x, out.y, out));
        list.push(placed(id, q.x, 0, q.z, out.x, out.y, true));
      }
    });
  } else if (kind === 3) {
    // Military's T-walls, a cordon.
    row(1, (t, i) => {
      const p = at(t, 0.9 + turn(220 + i, 0.1));
      list.push(placed('milBarrierTall', p.x, 0, p.z, -along.y + turn(230 + i, 0.08), along.x + turn(230 + i, 0.08), true));
    });
  } else if (kind === 5) {
    // The wood closing in: two rows of trees, the second between the first's, brush at their feet.
    const trees: PropId[] = look.pines ? ['pineLarge', 'pineLarge2', 'treePine'] : ['tree', 'treePine', 'tree', 'pineLarge'];
    row(1.15, (t, i) => {
      for (const [k, beyond, cast] of [[0, 0.75, true], [1, 1.95, false]] as const) {
        const p = at(t + k * 0.55 + turn(270 + i * 2 + k, 0.4), beyond + turn(280 + i * 2 + k, 0.3));
        const a = r(290 + i * 2 + k) * 6.3;
        list.push(placed(trees[Math.floor(r(300 + i * 2 + k) * trees.length)]!, p.x, 0, p.z, Math.cos(a), Math.sin(a), cast, 1, 0.9 + r(310 + i * 2 + k) * 0.35));
      }
      if (r(320 + i) < 0.55) {
        const size = 0.85 + r(340 + i) * 0.3;
        const q = at(t + turn(330 + i, 0.8), off('bush', 0.3, Math.cos(i * 2.1), Math.sin(i * 2.1), 1, size));
        list.push(placed('bush', q.x, 0, q.z, Math.cos(i * 2.1), Math.sin(i * 2.1), false, 1, size));
      }
    });
  } else if (kind === 6) {
    // A rock face, boulders before it, a few trees above.
    row(1.3, (t, i) => {
      const a = (r(370 + i) - 0.5) * 0.5;
      const id: PropId = r(380 + i) < 0.5 ? 'cliff' : 'cliff2';
      const [cx, cz] = [along.x * Math.cos(a) - out.x * Math.sin(a), along.y * Math.cos(a) - out.y * Math.sin(a)];
      const size = 1 + r(390 + i) * 0.35;
      const p = at(t + turn(350 + i, 0.3), off(id, 0.95 + turn(360 + i, 0.25), cx, cz, 1, size));
      list.push(placed(id, p.x, 0, p.z, cx, cz, true, 1, size));
      if (r(400 + i) < 0.45) {
        const rock: PropId = r(420 + i) < 0.5 ? 'boulder' : 'rock';
        const big = 0.8 + r(430 + i) * 0.4;
        const q = at(t + turn(410 + i, 0.9), off(rock, 0.3, Math.cos(i * 1.7), Math.sin(i * 1.7), 1, big));
        list.push(placed(rock, q.x, 0, q.z, Math.cos(i * 1.7), Math.sin(i * 1.7), true, 1, big));
      }
      if (r(440 + i) < 0.5) {
        const q = at(t + turn(450 + i, 0.8), 2.3);
        list.push(placed(look.pines ? 'pineLarge' : 'treePine', q.x, 0, q.z, 1, 0, false, 1, 0.9 + r(460 + i) * 0.3));
      }
    });
  } else if (kind === 7) {
    // Trees felled across the way, trunk over trunk, the wood behind them.
    row(2.2, (t, i) => {
      let behind = 0;
      for (let k = 0; k < 2; k++) {
        const a = (r(480 + i * 2 + k) - 0.5) * 0.6;
        const [lx, lz] = [along.x * Math.cos(a) - out.x * Math.sin(a), along.y * Math.cos(a) - out.y * Math.sin(a)];
        // The second over the first, a little further out.
        const beyond = Math.max(0.6 + k * 0.45, clearOf('log', lx, lz, out, 2.4), behind);
        behind = beyond + 0.45;
        const p = at(t + turn(470 + i * 2 + k, 0.5), beyond);
        list.push(placed('log', p.x, 0.12 * k, p.z, lx, lz, true, 2.4));
      }
      const q = at(t + 0.6, 1.8 + turn(490 + i, 0.3));
      const a = r(500 + i) * 6.3;
      list.push(placed(look.pines ? 'pineLarge' : 'tree', q.x, 0, q.z, Math.cos(a), Math.sin(a), false, 1, 0.95 + r(510 + i) * 0.3));
    });
  } else {
    // Shipping containers end to end, dragged there.
    const colours: PropId[] = ['container', 'containerBlue', 'containerOrange'];
    row(4.85, (t, i) => {
      const k = turn(250 + i, 0.06);
      const id = colours[Math.floor(r(260 + i) * 3)]!;
      // Their length (the model's z) along the edge.
      const [dx, dz] = [-along.y + out.y * k, along.x - out.x * k];
      const p = at(t, off(id, 1.45 + turn(240 + i, 0.2), dx, dz));
      list.push(placed(id, p.x, 0, p.z, dx, dz, true));
    });
  }
  return list;
}

/** How a biome's edges are walled off: a facility's with razor wire, the forest stop's woods of pines. */
export interface EdgeLook {
  wired: boolean;
  pines: boolean;
}

/**
 * Beyond the map's edge, where nobody walks, whatever keeps anybody from
 * going on (playtest 18/10, #32: «que se vea por qué no se puede pasar»).
 * Each street that leaves the map is shut right past the edge, from one side
 * to the other, its pavements too: a checkpoint's T-walls, a bus across it
 * with sandbags at its ends, or burnt-out cars with plank barricades between
 * them; further out, what was left on the street, a wrecked bus or a van or a
 * car, along it (the fog fades it into the haze). The open stretches between
 * (pavements, squares, yards) are walled off (fenceOff), ten tiles at most of
 * one kind. Both in turn round the map, so that no two are alike. Out of
 * town (Arturo's note is for the whole game): a facility's edge is its
 * perimeter (a precast wall with razor wire, T-walls, containers, sheet
 * metal); the wilds' is the wood closing in, a rock face, and trees felled
 * across the tracks.
 */
export function dressEdges(s: GameState, ground: GroundMap): Dressing[] {
  // The base's clearing goes on into its pines, and the dead come in by its tracks; an outline (docs/MAPAS.md §5.10)
  // shuts the map itself, and its entrances stay open to the edge (frameArt.ts).
  if (s.base || s.frame?.length) return [];
  const city = s.biome === 'city';
  const wilds = s.biome === 'wilds';
  const look: EdgeLook = { wired: s.biome === 'facility', pines: s.site === 'roadside' };
  const palette = city ? [0, 1, 2, 3, 4] : wilds ? [5, 6, 5, 5, 6] : [2, 3, 4, 2, 1];
  const out: Dressing[] = [];
  const { width: w, height: h } = s;
  // La Lata's way out (mission.evacBus; the engine keeps it clear, mapgen.ts parkBus): the edge it drives off by stays
  // open where it goes, a tile more either side (19/10: it drove through the closure).
  const lane = new Set<number>();
  const bus = s.mission.evacBus;
  if (bus?.tiles.length) {
    const d = DIRS4[bus.heading] ?? DIRS4[0]!;
    for (const t of bus.tiles) {
      for (const k of [-1, 0, 1]) {
        const x = d.x ? (d.x > 0 ? w - 1 : 0) : t.x + k;
        const y = d.y ? (d.y > 0 ? h - 1 : 0) : t.y + k;
        if (x >= 0 && y >= 0 && x < w && y < h) lane.add(y * w + x);
      }
    }
  }
  // Only a town's streets are shut by a closure across them; elsewhere a track is walled off with the rest.
  const street = (x: number, y: number) => city && ground.kinds[y * w + x] === FLOOR.STREET && !lane.has(y * w + x);
  const track = (x: number, y: number) => ground.kinds[y * w + x] === FLOOR.STREET;
  let closures = Math.floor(hash(w, h, 7) * 3);
  let fences = Math.floor(hash(w, h, 8) * palette.length);
  // Each side: along it, the runs of street tiles at the edge (a street leaving the map). The top and the
  // bottom wall off the corners' squares too.
  const sides = [
    { n: w, at: (k: number) => ({ x: k, y: 0 }), out: { x: 0, y: -1 }, corners: 1 },
    { n: h, at: (k: number) => ({ x: w - 1, y: k }), out: { x: 1, y: 0 }, corners: 0 },
    { n: w, at: (k: number) => ({ x: k, y: h - 1 }), out: { x: 0, y: 1 }, corners: 1 },
    { n: h, at: (k: number) => ({ x: 0, y: k }), out: { x: -1, y: 0 }, corners: 0 },
  ];
  for (const side of sides) {
    // What the streets' closures take of this side; the rest, if open, is walled off after.
    const shutOff = new Array<boolean>(side.n).fill(false);
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
        // The way shut, its pavements too (up to two tiles of open ground either side of the street).
        const pave = (q: number) => {
          const p = q >= 0 && q < side.n ? side.at(q) : null;
          return !!p && s.tiles[p.y * w + p.x] === 'floor' && ground.kinds[p.y * w + p.x] === FLOOR.OPEN && !lane.has(p.y * w + p.x);
        };
        let from = start;
        let to = k - 1;
        while (from > start - 2 && pave(from - 1)) from--;
        while (to < k + 1 && pave(to + 1)) to++;
        for (let q = from; q <= to; q++) shutOff[q] = true;
        out.push(...shut(side.at(from), side.at(to), side.out, closures++ % 3, r));
        if (r(1) < 0.75) {
          // In one lane, nose out or in, a few tiles beyond the closure (its middle half its length further).
          const lane = r(2) < 0.5 ? 0.25 : 0.75;
          const id: PropId = r(4) < 0.4 ? 'bus' : r(4) < 0.75 ? 'van' : 'carRusted';
          const beyond = 3.4 + (id === 'bus' ? 5.5 : id === 'van' ? 2.6 : 2.2) + r(3) * 2;
          const cx = a.x + 0.5 + (b.x - a.x) * lane + side.out.x * beyond;
          const cz = a.y + 0.5 + (b.y - a.y) * lane + side.out.y * beyond;
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
    // The open stretches left: anything but a building's wall (or what stands inside one) that no closure took.
    const open = (k: number) => {
      const p = side.at(k);
      const i = p.y * w + p.x;
      return !shutOff[k] && !lane.has(i) && s.tiles[i] !== 'wall' && ground.kinds[i] !== FLOOR.INDOOR && ground.kinds[i] !== FLOOR.ROOF;
    };
    start = -1;
    for (let k = 0; k <= side.n; k++) {
      if (k < side.n && open(k)) {
        if (start < 0) start = k;
        continue;
      }
      if (start >= 0) {
        // In the wilds, each track that runs out (and a tile either side) is felled across, apart from the rest.
        const parts: { lo: number; hi: number; felled: boolean }[] = [];
        for (let q = start; q < k; ) {
          const near = (x: number) => wilds && [x - 1, x, x + 1].some((m) => m >= start && m < k && track(side.at(m).x, side.at(m).y));
          const felled = near(q);
          let e = q;
          while (e + 1 < k && near(e + 1) === felled) e++;
          parts.push({ lo: q, hi: e, felled });
          q = e + 1;
        }
        for (const part of parts) {
          // Ten tiles at most of one kind: a long stretch in even pieces.
          const length = part.hi - part.lo + 1;
          const pieces = Math.ceil(length / 10);
          for (let j = 0; j < pieces; j++) {
            const lo = part.lo + Math.round((length * j) / pieces);
            const hi = part.lo + Math.round((length * (j + 1)) / pieces) - 1;
            const a = side.at(lo);
            const r = (salt: number) => hash(a.x * 3 + side.out.x, a.y * 3 + side.out.y, salt + 300);
            // A little over its ends, so that nothing shows between it and what shuts the next stretch.
            const ends: [number, number] = [lo === 0 ? side.corners : 0.25, hi === side.n - 1 ? side.corners : 0.25];
            out.push(...fenceOff(a, side.at(hi), side.out, part.felled ? 7 : palette[fences++ % palette.length]!, ends, r, look));
          }
        }
      }
      start = -1;
    }
  }
  return out;
}
