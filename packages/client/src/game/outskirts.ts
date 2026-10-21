import { DIRS4, type GameState } from '@bastion/engine';
import * as THREE from 'three';
import { STOREY } from './coords';
import { FLOOR, type GroundMap } from './ground';

// What lies beyond a map's edge, in the dark (playtest 21/10 #17, Ana: so that the town seems to go on and the
// cut doesn't show): a town's blocks, two rows of them (houses under a pitched roof, taller blocks behind), an
// industrial estate's or a factory's sheds, the wild's trees. Simple and low: boxes, gables and cones, one mesh.
// The fog of war draws them as the dark beyond the edge (fogOfWar.ts: their outline a little lighter higher up)
// and, far out, into the haze, so from low down they stand against the sky. Along each side the streets and
// tracks that leave the map go on between them, and so does La Lata's way out.

/** How far out they go (tiles past the edge), and the gap left at the edge itself (the edge's own dressing). */
const REACH = 22;
const GAP = 1.5;

function hash(x: number, y: number, salt: number): number {
  return ((((x + salt * 31) * 73856093) ^ (y * 19349663) ^ (salt * 83492791)) >>> 0) % 1000 / 1000;
}

/** Flat-shaded triangles, gathered. */
class Shapes {
  readonly position: number[] = [];
  readonly normal: number[] = [];

  private tri(a: THREE.Vector3, b: THREE.Vector3, c: THREE.Vector3): void {
    const n = new THREE.Vector3().subVectors(b, a).cross(new THREE.Vector3().subVectors(c, a)).normalize();
    for (const p of [a, b, c]) {
      this.position.push(p.x, p.y, p.z);
      this.normal.push(n.x, n.y, n.z);
    }
  }

  private quad(a: THREE.Vector3, b: THREE.Vector3, c: THREE.Vector3, d: THREE.Vector3): void {
    this.tri(a, b, c);
    this.tri(a, c, d);
  }

  /** A box from (x0, z0) to (x1, z1), `h` high (no floor: nothing sees it). */
  box(x0: number, z0: number, x1: number, z1: number, h: number, y = 0): void {
    const V = (x: number, yy: number, z: number) => new THREE.Vector3(x, yy, z);
    const t = y + h;
    this.quad(V(x0, t, z0), V(x0, t, z1), V(x1, t, z1), V(x1, t, z0));
    this.quad(V(x0, y, z1), V(x1, y, z1), V(x1, t, z1), V(x0, t, z1));
    this.quad(V(x1, y, z0), V(x0, y, z0), V(x0, t, z0), V(x1, t, z0));
    this.quad(V(x1, y, z1), V(x1, y, z0), V(x1, t, z0), V(x1, t, z1));
    this.quad(V(x0, y, z0), V(x0, y, z1), V(x0, t, z1), V(x0, t, z0));
  }

  /** A pitched roof over the box (x0, z0)–(x1, z1) at height `y`, `rise` high, its ridge along the longer side. */
  gable(x0: number, z0: number, x1: number, z1: number, y: number, rise: number): void {
    const V = (x: number, yy: number, z: number) => new THREE.Vector3(x, yy, z);
    const top = y + rise;
    if (x1 - x0 >= z1 - z0) {
      const zm = (z0 + z1) / 2;
      this.quad(V(x0, y, z1), V(x1, y, z1), V(x1, top, zm), V(x0, top, zm));
      this.quad(V(x1, y, z0), V(x0, y, z0), V(x0, top, zm), V(x1, top, zm));
      this.tri(V(x0, y, z0), V(x0, y, z1), V(x0, top, zm));
      this.tri(V(x1, y, z1), V(x1, y, z0), V(x1, top, zm));
    } else {
      const xm = (x0 + x1) / 2;
      this.quad(V(x1, y, z0), V(x1, y, z1), V(xm, top, z1), V(xm, top, z0));
      this.quad(V(x0, y, z1), V(x0, y, z0), V(xm, top, z0), V(xm, top, z1));
      this.tri(V(x0, y, z0), V(x1, y, z0), V(xm, top, z0));
      this.tri(V(x1, y, z1), V(x0, y, z1), V(xm, top, z1));
    }
  }

  /** A low-poly tree: a trunk and a cone of `sides` faces, `h` high in all. */
  tree(x: number, z: number, h: number, r: number, sides = 6): void {
    const trunk = h * 0.22;
    this.box(x - r * 0.12, z - r * 0.12, x + r * 0.12, z + r * 0.12, trunk);
    const tip = new THREE.Vector3(x, h, z);
    for (let k = 0; k < sides; k++) {
      const a = (k / sides) * Math.PI * 2;
      const b = ((k + 1) / sides) * Math.PI * 2;
      this.tri(new THREE.Vector3(x + Math.cos(b) * r, trunk, z + Math.sin(b) * r), new THREE.Vector3(x + Math.cos(a) * r, trunk, z + Math.sin(a) * r), tip);
    }
  }

  geometry(): THREE.BufferGeometry {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(this.position, 3));
    g.setAttribute('normal', new THREE.Float32BufferAttribute(this.normal, 3));
    g.computeBoundingSphere();
    return g;
  }
}

/** One side of the map: how to go along it (k) and out from it (d tiles past the edge) in the map's coordinates. */
interface Side {
  n: number;
  /** The point `k` along the side, `d` out from its edge. */
  at(k: number, d: number): { x: number; z: number };
  /** The map tile at the edge for `k` (in the map), to see what leaves it there. */
  tile(k: number): { x: number; y: number } | null;
  /** How far before 0 and past n it runs on (the corners: taken by the sides along x). */
  from: number;
  to: number;
}

/**
 * The outskirts of the map of `s` (null for the base, whose clearing has its own woods): a mesh of simple shapes
 * beyond its edges, out to REACH tiles. `ground` says where its streets and tracks leave it.
 */
export function outskirtsGeometry(s: GameState, ground: GroundMap): THREE.BufferGeometry | null {
  if (s.base) return null;
  const { width: w, height: h } = s;
  const shapes = new Shapes();
  const industrial = s.biome === 'facility' || s.site === 'industrial';
  const town = s.biome === 'city' && !industrial;
  const wild = s.biome === 'wilds';
  // La Lata's way out: its side and the tiles along it (with one more either side) stay open.
  const bus = s.mission.evacBus;
  const busSide = bus?.tiles.length ? (DIRS4[bus.heading] ?? null) : null;
  const sides: (Side & { out: { x: number; y: number } })[] = [
    { n: w, out: { x: 0, y: -1 }, at: (k, d) => ({ x: k, z: -d }), tile: (k) => (k >= 0 && k < w ? { x: k, y: 0 } : null), from: -REACH, to: w + REACH },
    { n: h, out: { x: 1, y: 0 }, at: (k, d) => ({ x: w + d, z: k }), tile: (k) => (k >= 0 && k < h ? { x: w - 1, y: k } : null), from: 0, to: h },
    { n: w, out: { x: 0, y: 1 }, at: (k, d) => ({ x: k, z: h + d }), tile: (k) => (k >= 0 && k < w ? { x: k, y: h - 1 } : null), from: -REACH, to: w + REACH },
    { n: h, out: { x: -1, y: 0 }, at: (k, d) => ({ x: -d, z: k }), tile: (k) => (k >= 0 && k < h ? { x: 0, y: k } : null), from: 0, to: h },
  ];
  for (const [si, side] of sides.entries()) {
    // What stays open along the side: where a street or a track leaves the map, and La Lata's way.
    const open = (k: number): boolean => {
      const t = side.tile(Math.floor(k));
      if (!t) return false;
      if (ground.kinds[t.y * w + t.x] === FLOOR.STREET) return true;
      if (busSide && busSide.x === side.out.x && busSide.y === side.out.y)
        for (const b of bus!.tiles) if (Math.abs((side.out.x ? b.y : b.x) - Math.floor(k)) <= 1) return true;
      return false;
    };
    const r = (k: number, salt: number) => hash(si * 997 + Math.floor(k), Math.floor(k) * 7 + si, salt);
    if (wild) {
      // The wood goes on: trees on a jittered grid, thicker further out, the tracks' ways between them.
      for (let k = side.from; k < side.to; k += 1.7) {
        for (let d = GAP + 0.5; d < REACH; d += 1.7) {
          const jk = k + (r(k + d * 13, 1) - 0.5) * 1.3;
          if (open(jk) || open(jk - 1) || open(jk + 1)) continue;
          if (r(k + d * 13, 2) < 0.25 - d / REACH * 0.2) continue;
          const p = side.at(jk, d + (r(k + d * 13, 3) - 0.5) * 1.1);
          const tall = 3 + r(k + d * 13, 4) * 3.5;
          shapes.tree(p.x, p.z, tall, 0.8 + r(k + d * 13, 5) * 0.6);
        }
      }
      continue;
    }
    // Rows of blocks along the side, broken where a way leaves the map and by alleys between them: a front row
    // past the edge and a taller one behind it, across a street.
    const rows = industrial ? [{ d0: GAP + 1, d1: GAP + 9 }, { d0: GAP + 12, d1: GAP + 20 }] : [{ d0: GAP + 0.5, d1: GAP + 6.5 }, { d0: GAP + 9, d1: GAP + 17 }];
    for (const [ri, row] of rows.entries()) {
      let k = side.from;
      while (k < side.to) {
        if (open(k)) {
          k += 1;
          continue;
        }
        // A block as long as there is room before the next way out, alleys between its buildings.
        let len = (industrial ? 9 : 5) + r(k, 10 + ri) * (industrial ? 7 : 5);
        for (let q = 1; q < len; q++) {
          if (open(k + q)) {
            len = Math.max(1, q - 1);
            break;
          }
        }
        const a = side.at(k, row.d0);
        const b = side.at(k + len, row.d1 - r(k, 12 + ri) * 2);
        const x0 = Math.min(a.x, b.x);
        const x1 = Math.max(a.x, b.x);
        const z0 = Math.min(a.z, b.z);
        const z1 = Math.max(a.z, b.z);
        if (industrial) {
          // A shed: one or two storeys, flat or with a low pitch.
          const tall = STOREY * (1 + r(k, 14 + ri) * 1.2);
          shapes.box(x0, z0, x1, z1, tall);
          if (r(k, 16 + ri) < 0.5) shapes.gable(x0, z0, x1, z1, tall, 0.9);
        } else {
          // In town: a house under a pitched roof in front, flats behind (two to five storeys, flat-topped).
          const storeys = ri === 0 ? 1 + Math.floor(r(k, 14) * 2.4) : 2 + Math.floor(r(k, 15) * 3.5);
          const tall = storeys * STOREY;
          shapes.box(x0, z0, x1, z1, tall);
          if (town && (ri === 0 || storeys <= 2)) shapes.gable(x0, z0, x1, z1, tall, 1 + r(k, 16) * 0.8);
        }
        k += len + 1 + r(k, 18 + ri) * 1.5;
      }
    }
  }
  return shapes.position.length ? shapes.geometry() : null;
}
