import * as THREE from 'three';
import { GAME_VOXEL, voxelize } from './voxel';

// Voxel props for the battlefield (M5 art). Each tile of cover gets a real
// object instead of a plain block: crates, sandbags, barriers, kiosks, trees,
// server racks… Every prop fits one tile (centred at the origin, standing on
// y = 0), low cover reaches about 0.9 and high cover about 1.9, so cover keeps
// reading at a glance. Props are built from simple parts, voxelized once and
// then instanced by the map; their colours live in vertex colours, so the
// map's per-instance colour is left for the fog of war.

/** Material roles, as in the art lab's kit: colour, and whether it glows. */
const ROLES = {
  brick: [0x8a5a48, false],
  concrete: [0x9a968c, false],
  concreteDark: [0x6c6a64, false],
  metal: [0x7d8790, false],
  darkMetal: [0x30353b, false],
  paint: [0x2f5a78, false],
  paintGreen: [0x3d6b45, false],
  paintRed: [0x9a3a2e, false],
  paintYellow: [0xc9a23a, false],
  wood: [0x9a7448, false],
  woodDark: [0x5e4428, false],
  fabric: [0xa08f62, false],
  fabricDark: [0x7f704a, false],
  foliage: [0x4c7a3a, false],
  foliageDark: [0x35602e, false],
  foliageLight: [0x6a9a45, false],
  bark: [0x5a4130, false],
  soil: [0x3e2f24, false],
  rubber: [0x1c1d1f, false],
  stripe: [0xc9472f, false],
  rock: [0x7c7c74, false],
  rockDark: [0x5e5c56, false],
  glass: [0x2f4656, false],
  lightWarm: [0xffc070, true],
  lightCool: [0x7fe0ff, true],
  screen: [0x54d1ff, true],
  alienMetal: [0x3b2c3a, false],
  alienGlow: [0xff3aa8, true],
  carWhite: [0xd6d3ca, false],
  paintOrange: [0xc0652c, false],
  cardboard: [0xb08a58, false],
  straw: [0xd4b25a, false],
  strawDark: [0xa88a3c, false],
  water: [0x4a86a8, false],
  linen: [0xe2dccc, false],
  blanket: [0x56709a, false],
  leather: [0x7a4632, false],
  bronze: [0x557a66, false],
} as const;

type Role = keyof typeof ROLES;

/** Brings a coordinate on (or past) the tile's edge just inside it: its voxels then end exactly on the edge. */
const clampEdge = (v: number) => Math.max(-0.499, Math.min(0.498, v));

/** Tiny builder for props made of boxes, cylinders and blobs. */
class Parts {
  readonly group = new THREE.Group();
  private readonly materials = new Map<Role, THREE.Material>();

  constructor(readonly seed: number) {}

  private mat(role: Role): THREE.Material {
    let m = this.materials.get(role);
    if (!m) {
      const [color, glow] = ROLES[role];
      m = glow ? new THREE.MeshStandardMaterial({ color, emissive: color, emissiveIntensity: 1.6 }) : new THREE.MeshStandardMaterial({ color });
      this.materials.set(role, m);
    }
    return m;
  }

  add(geometry: THREE.BufferGeometry, role: Role, x: number, y: number, z: number, rot?: [number, number, number]): THREE.Mesh {
    const mesh = new THREE.Mesh(geometry, this.mat(role));
    mesh.position.set(x, y, z);
    if (rot) mesh.rotation.set(rot[0], rot[1], rot[2]);
    this.group.add(mesh);
    return mesh;
  }

  /** Box standing on `y`. */
  box(w: number, h: number, d: number, role: Role, x: number, y: number, z: number, rotY = 0): THREE.Mesh {
    return this.add(new THREE.BoxGeometry(w, h, d), role, x, y + h / 2, z, [0, rotY, 0]);
  }

  /**
   * Box standing on `y` from x0 to x1 and z0 to z1. A side on the tile's edge
   * (±0.5) is kept a hair inside, so the voxels end exactly on it and a row of
   * props (shelves, desks, a car's halves) joins without overlapping.
   */
  span(x0: number, x1: number, h: number, z0: number, z1: number, role: Role, y: number): THREE.Mesh {
    const [a, b, c, d] = [clampEdge(x0), clampEdge(x1), clampEdge(z0), clampEdge(z1)];
    return this.box(b - a, h, d - c, role, (a + b) / 2, y, (c + d) / 2);
  }

  /** Cylinder standing on `y`. */
  cyl(rTop: number, rBottom: number, h: number, role: Role, x: number, y: number, z: number, seg = 12): THREE.Mesh {
    return this.add(new THREE.CylinderGeometry(rTop, rBottom, h, seg), role, x, y + h / 2, z);
  }

  blob(r: number, role: Role, x: number, y: number, z: number): THREE.Mesh {
    return this.add(new THREE.IcosahedronGeometry(r, 1), role, x, y, z);
  }

  /** Stable pseudo-random numbers per prop variant. */
  rand(): () => number {
    let s = this.seed * 9301 + 49297;
    return () => {
      s = (s * 9301 + 49297) % 233280;
      return s / 233280;
    };
  }
}

// ----------------------------------------------------------------- city

function crate(p: Parts, size: number, x: number, y: number, z: number, role: Role = 'wood'): void {
  p.box(size, size, size, role, x, y, z);
  const t = 0.05;
  const trim: Role = role === 'wood' ? 'woodDark' : 'darkMetal';
  p.box(size + 0.02, t, size + 0.02, trim, x, y, z);
  p.box(size + 0.02, t, size + 0.02, trim, x, y + size - t, z);
  for (const [cx, cz] of [[-1, -1], [1, -1], [-1, 1], [1, 1]] as const) p.box(t, size, t, trim, x + (cx * (size - t)) / 2, y, z + (cz * (size - t)) / 2);
}

function crates(p: Parts): void {
  crate(p, 0.6, -0.12, 0, 0.08);
  crate(p, 0.36, -0.1, 0.6, 0.1);
  crate(p, 0.34, 0.26, 0, -0.26);
}

function sandbags(p: Parts): void {
  const bag = new THREE.CapsuleGeometry(0.12, 0.08, 3, 10).rotateZ(Math.PI / 2).scale(1, 0.72, 1.3);
  for (let layer = 0; layer < 4; layer++) {
    const offset = layer % 2 ? 0.15 : 0;
    for (let i = 0; i < (layer % 2 ? 2 : 3); i++) p.add(bag, layer % 2 ? 'fabric' : 'fabricDark', -0.3 + i * 0.3 + offset, 0.1 + layer * 0.2, 0);
  }
}

function barrier(p: Parts): void {
  const s = new THREE.Shape();
  s.moveTo(-0.32, 0);
  s.lineTo(0.32, 0);
  s.lineTo(0.3, 0.1);
  s.lineTo(0.12, 0.3);
  s.lineTo(0.1, 0.86);
  s.lineTo(-0.1, 0.86);
  s.lineTo(-0.12, 0.3);
  s.lineTo(-0.3, 0.1);
  s.closePath();
  const g = new THREE.ExtrudeGeometry(s, { depth: 0.94, bevelEnabled: false }).translate(0, 0, -0.47).rotateY(Math.PI / 2);
  p.add(g, 'concrete', 0, 0, 0);
  p.box(0.94, 0.1, 0.22, 'stripe', 0, 0.64, 0);
}

function planter(p: Parts): void {
  p.box(0.86, 0.46, 0.86, 'concrete', 0, 0, 0);
  p.box(0.74, 0.04, 0.74, 'soil', 0, 0.44, 0);
  p.blob(0.3, 'foliage', -0.12, 0.7, 0.05);
  p.blob(0.24, 'foliageLight', 0.18, 0.64, -0.12);
  p.blob(0.2, 'foliageDark', 0.1, 0.82, 0.18);
}

function dumpster(p: Parts): void {
  p.box(0.86, 0.72, 0.6, 'paintGreen', 0, 0.1, 0);
  p.box(0.9, 0.06, 0.64, 'darkMetal', 0, 0.82, -0.02);
  for (const x of [-0.36, 0.36]) p.cyl(0.06, 0.06, 0.1, 'rubber', x, 0, 0.2, 8);
  p.box(0.3, 0.12, 0.02, 'paintYellow', 0, 0.5, 0.31);
}

function kiosk(p: Parts): void {
  p.box(0.82, 0.08, 0.82, 'concrete', 0, 0, 0);
  p.box(0.76, 1.55, 0.76, 'paintRed', 0, 0.08, 0);
  p.box(0.56, 0.5, 0.04, 'glass', 0, 0.85, 0.39);
  p.box(0.98, 0.08, 0.98, 'darkMetal', 0, 1.82, 0);
  p.box(0.84, 0.18, 0.84, 'lightCool', 0, 1.64, 0);
}

function containers(p: Parts): void {
  p.box(0.9, 0.9, 0.9, 'paint', 0, 0, 0);
  for (const x of [-0.3, -0.1, 0.1, 0.3]) p.box(0.04, 0.84, 0.92, 'darkMetal', x, 0.03, 0);
  p.box(0.8, 0.9, 0.8, 'paintYellow', 0.02, 0.9, 0.02);
  p.box(0.82, 0.05, 0.82, 'darkMetal', 0.02, 1.76, 0.02);
}

function pillar(p: Parts): void {
  p.box(0.7, 0.2, 0.7, 'concreteDark', 0, 0, 0);
  p.box(0.56, 1.6, 0.56, 'concrete', 0, 0.2, 0);
  p.box(0.66, 0.12, 0.66, 'concreteDark', 0, 1.8, 0);
  p.box(0.4, 0.6, 0.02, 'paintYellow', 0, 0.8, 0.29);
  p.box(0.3, 0.2, 0.025, 'stripe', 0, 1.1, 0.29);
}

function vending(p: Parts): void {
  p.box(0.74, 1.82, 0.6, 'paintRed', 0, 0, 0);
  p.box(0.5, 1.1, 0.03, 'glass', -0.06, 0.55, 0.3);
  p.box(0.12, 0.5, 0.03, 'lightWarm', 0.26, 1.0, 0.3);
  p.box(0.5, 0.14, 0.03, 'darkMetal', -0.06, 0.25, 0.3);
}

// ---------------------------------------------------------------- wilds

function rock(p: Parts): void {
  const rand = p.rand();
  p.add(new THREE.DodecahedronGeometry(0.46, 0), 'rock', -0.05, 0.36, 0.02, [rand(), rand(), rand()]).scale.set(1, 0.95, 0.95);
  p.add(new THREE.DodecahedronGeometry(0.24, 0), 'rockDark', 0.26, 0.2, -0.22, [rand(), rand(), 0]);
}

function logs(p: Parts): void {
  const log = new THREE.CylinderGeometry(0.15, 0.15, 0.92, 10).rotateX(Math.PI / 2);
  p.add(log, 'bark', -0.17, 0.15, 0);
  p.add(log, 'bark', 0.17, 0.15, 0);
  p.add(log, 'bark', 0, 0.42, 0);
  for (const [x, y] of [[-0.17, 0.15], [0.17, 0.15], [0, 0.42]] as const) p.add(new THREE.CylinderGeometry(0.12, 0.12, 0.94, 10).rotateX(Math.PI / 2), 'wood', x, y, 0);
}

function bush(p: Parts): void {
  p.blob(0.34, 'foliageDark', -0.1, 0.32, 0.05);
  p.blob(0.3, 'foliage', 0.2, 0.36, -0.1);
  p.blob(0.24, 'foliageLight', 0.02, 0.6, 0.12);
}

function tree(p: Parts): void {
  const rand = p.rand();
  p.cyl(0.1, 0.15, 1.1, 'bark', 0, 0, 0, 8);
  for (let i = 0; i < 5; i++) {
    const a = rand() * Math.PI * 2;
    const r = i === 0 ? 0 : 0.24;
    p.blob(0.34 + rand() * 0.14, i % 2 ? 'foliage' : 'foliageDark', Math.cos(a) * r, 1.35 + rand() * 0.4, Math.sin(a) * r);
  }
}

function pine(p: Parts): void {
  p.cyl(0.08, 0.12, 0.5, 'bark', 0, 0, 0, 8);
  for (const [y, r, h] of [[0.4, 0.46, 0.8], [0.9, 0.36, 0.7], [1.35, 0.24, 0.6]] as const) p.add(new THREE.ConeGeometry(r, h, 10), 'foliageDark', 0, y + h / 2, 0);
}

function spire(p: Parts): void {
  p.add(new THREE.CylinderGeometry(0.12, 0.38, 1.9, 6), 'rock', 0, 0.95, 0, [0, 0.4, 0.06]);
  p.add(new THREE.DodecahedronGeometry(0.26, 0), 'rockDark', 0.24, 0.2, 0.2);
}

// ------------------------------------------------------------- facility

function techCrate(p: Parts): void {
  crate(p, 0.62, -0.08, 0, 0.06, 'metal');
  p.box(0.64, 0.06, 0.02, 'stripe', -0.08, 0.3, 0.38);
  crate(p, 0.34, 0.2, 0.62, -0.08, 'metal');
}

function techBarrier(p: Parts): void {
  p.box(0.94, 0.7, 0.3, 'darkMetal', 0, 0, 0);
  p.box(0.9, 0.06, 0.32, 'lightCool', 0, 0.6, 0);
  p.box(0.94, 0.14, 0.36, 'metal', 0, 0.7, 0);
}

function generator(p: Parts): void {
  p.box(0.86, 0.6, 0.6, 'metal', 0, 0, 0);
  p.cyl(0.18, 0.18, 0.3, 'darkMetal', -0.2, 0.6, 0, 10);
  p.box(0.3, 0.1, 0.02, 'lightWarm', 0.2, 0.4, 0.31);
  p.box(0.86, 0.06, 0.62, 'stripe', 0, 0.1, 0);
}

function rack(p: Parts): void {
  p.box(0.74, 1.86, 0.6, 'darkMetal', 0, 0, 0);
  for (let i = 0; i < 6; i++) {
    p.box(0.6, 0.18, 0.02, 'metal', 0, 0.2 + i * 0.27, 0.3);
    p.box(0.06, 0.04, 0.025, i % 2 ? 'screen' : 'lightWarm', 0.22, 0.3 + i * 0.27, 0.31);
  }
}

function tank(p: Parts): void {
  p.cyl(0.4, 0.4, 1.6, 'metal', 0, 0, 0, 14);
  p.add(new THREE.SphereGeometry(0.4, 14, 8, 0, Math.PI * 2, 0, Math.PI / 2), 'metal', 0, 1.6, 0);
  p.cyl(0.42, 0.42, 0.08, 'stripe', 0, 0.9, 0, 14);
  p.box(0.1, 1.6, 0.1, 'darkMetal', 0.38, 0, 0.1);
}

function pylon(p: Parts): void {
  p.cyl(0.36, 0.42, 0.2, 'alienMetal', 0, 0, 0, 6);
  p.cyl(0.12, 0.22, 1.5, 'alienMetal', 0, 0.2, 0, 6);
  p.add(new THREE.OctahedronGeometry(0.22, 0), 'alienGlow', 0, 1.9, 0).scale.set(1, 1.4, 1);
}

function consoleDesk(p: Parts): void {
  p.box(0.76, 0.78, 0.5, 'darkMetal', 0, 0, 0);
  p.box(0.8, 0.05, 0.56, 'metal', 0, 0.78, 0);
  p.box(0.6, 0.4, 0.06, 'darkMetal', 0, 0.83, -0.12, 0);
  p.box(0.52, 0.32, 0.02, 'screen', 0, 0.87, -0.08);
  p.box(0.4, 0.04, 0.16, 'lightCool', 0, 0.83, 0.12);
}

// ----------------------------------------------------------------- decor
//
// Props for cover that the map generator dressed as something (decor hints).
// Like the others they face north (−z); those that come in lines (shelves,
// desks, counters, pipes, a fence's rails) run along x from edge to edge, so a
// row of them joins into one piece.

/**
 * Half of a parked car, its nose to the north. The car is built whole along z
 * in its own frame (nose at −0.97, tail at 0.97, the seam between its two
 * tiles at 0) and each half keeps what lies on its side of the seam, so the
 * halves join without a step.
 */
function carHalf(p: Parts, paint: Role, front: boolean): void {
  const shift = front ? 0.5 : -0.5;
  const box = (w: number, h: number, z0: number, z1: number, role: Role, y: number, x = 0) => {
    const a = clampEdge(Math.max(z0, front ? -1 : 0) + shift);
    const b = clampEdge(Math.min(z1, front ? 0 : 1) + shift);
    if (b > a) p.box(w, h, b - a, role, x, y, (a + b) / 2);
  };
  const wheel = new THREE.CylinderGeometry(0.17, 0.17, 0.12, 12).rotateZ(Math.PI / 2);
  for (const x of [-0.4, 0.4]) p.add(wheel, 'rubber', x, 0.17, (front ? -0.6 : 0.6) + shift);
  box(0.8, 0.4, -0.94, 0.94, paint, 0.14);
  box(0.82, 0.14, -0.97, -0.9, 'darkMetal', 0.14);
  box(0.82, 0.14, 0.9, 0.97, 'darkMetal', 0.14);
  box(0.3, 0.1, -0.96, -0.93, 'darkMetal', 0.34);
  for (const x of [-0.28, 0.28]) {
    box(0.14, 0.08, -0.96, -0.93, 'linen', 0.42, x);
    box(0.12, 0.08, 0.93, 0.96, 'stripe', 0.42, x);
  }
  // Cabin: windows all round under the roof, pillars between them.
  box(0.72, 0.34, -0.32, 0.5, 'glass', 0.54);
  box(0.74, 0.08, -0.24, 0.44, paint, 0.88);
  for (const x of [-0.34, 0.34]) for (const [z0, z1] of [[-0.32, -0.25], [0.04, 0.1], [0.44, 0.5]] as const) box(0.06, 0.34, z0, z1, paint, 0.54, x);
}

const CAR_PAINTS: Role[] = ['paint', 'paintRed', 'carWhite', 'paintGreen'];

/** Fence post, in the middle of every fence tile; rails run from it to each fence beside it. */
function fencePost(p: Parts): void {
  p.box(0.12, 0.9, 0.12, 'woodDark', 0, 0, 0);
  p.box(0.16, 0.04, 0.16, 'woodDark', 0, 0.9, 0);
}

/** Farm fence: two rails from the post to the north edge (turned towards each neighbour). */
function fenceRails(p: Parts): void {
  for (const y of [0.3, 0.62]) p.span(-0.04, 0.04, 0.1, -0.5, 0, 'wood', y);
}

/** Garden fence: a panel of boards from the post to the north edge. */
function fenceBoards(p: Parts): void {
  for (let k = 0; k < 4; k++) p.span(-0.035, 0.035, 0.84 - (k % 2) * 0.08, -0.5 + k * 0.125, -0.375 + k * 0.125, k % 2 ? 'woodDark' : 'wood', 0.02);
}

/** Stone pillar beside the gap of a gate; fences join it like a post. */
function gatePost(p: Parts): void {
  p.box(0.3, 0.86, 0.3, 'rock', 0, 0, 0);
  p.box(0.36, 0.08, 0.36, 'rockDark', 0, 0.86, 0);
}

/** A round hay bale lying along x, net bands round it. */
function hayBale(p: Parts): void {
  const roll = (r: number, len: number) => new THREE.CylinderGeometry(r, r, len, 16).rotateZ(Math.PI / 2);
  p.add(roll(0.4, 0.82), 'straw', 0, 0.4, 0);
  for (const x of [-0.2, 0.2]) p.add(roll(0.41, 0.05), 'strawDark', x, 0.4, 0);
  for (const x of [-0.41, 0.41]) p.add(roll(0.26, 0.02), 'strawDark', x, 0.4, 0);
}

/** A stack of square bales, crossed layer on layer, one missing on top. */
function hayStack(p: Parts): void {
  for (let layer = 0; layer < 4; layer++) {
    const across = layer % 2 === 1;
    for (const k of [-1, 1]) {
      if (layer === 3 && k === 1) continue;
      const x = across ? k * 0.21 : 0;
      const z = across ? 0 : k * 0.21;
      const y = layer * 0.44;
      p.box(across ? 0.4 : 0.84, 0.42, across ? 0.84 : 0.4, 'straw', x, y, z);
      for (const t of [-0.2, 0.2]) p.box(across ? 0.41 : 0.04, 0.43, across ? 0.04 : 0.41, 'strawDark', x + (across ? 0 : t), y, z + (across ? t : 0));
    }
  }
}

/** Pallet racking: uprights, beams and decks at `levels` with goods on them, up to `top`. */
function racking(p: Parts, levels: number[], top: number): void {
  const rand = p.rand();
  for (const x of [-0.46, 0.46]) for (const z of [-0.34, 0.34]) p.box(0.06, top, 0.06, 'paint', x, 0, z);
  for (const z of [-0.34, 0.34]) p.span(-0.5, 0.5, 0.06, z - 0.03, z + 0.03, 'paintOrange', top - 0.06);
  for (const y of levels) {
    for (const z of [-0.34, 0.34]) p.span(-0.5, 0.5, 0.06, z - 0.03, z + 0.03, 'paintOrange', y);
    p.span(-0.5, 0.5, 0.03, -0.34, 0.34, 'woodDark', y + 0.03);
    for (let x = -0.42; ; ) {
      const w = 0.2 + rand() * 0.16;
      if (x + w > 0.44) break;
      p.box(w - 0.03, 0.18 + rand() * 0.22, 0.48 + rand() * 0.12, rand() < 0.75 ? 'cardboard' : 'wood', x + w / 2, y + 0.06, (rand() - 0.5) * 0.06);
      x += w;
    }
  }
}

function shelfHigh(p: Parts): void {
  racking(p, [0.04, 0.66, 1.28], 1.9);
}

function shelfLow(p: Parts): void {
  racking(p, [0.04, 0.46], 0.9);
}

/** Shop counter, the customers' side to the north, a till on it. */
function counter(p: Parts): void {
  p.span(-0.5, 0.5, 0.76, -0.18, 0.3, 'woodDark', 0);
  p.span(-0.5, 0.5, 0.06, -0.26, 0.34, 'wood', 0.76);
  p.span(-0.5, 0.5, 0.08, -0.2, -0.18, 'paint', 0.5);
  p.box(0.22, 0.14, 0.18, 'darkMetal', 0.2, 0.82, 0.1);
  p.box(0.16, 0.06, 0.02, 'screen', 0.2, 0.88, 0.0);
}

/** Table with a chair on each long side; two side by side make one long table. */
function table(p: Parts): void {
  p.span(-0.5, 0.5, 0.06, -0.3, 0.3, 'wood', 0.62);
  for (const x of [-0.42, 0.42]) for (const z of [-0.24, 0.24]) p.box(0.06, 0.62, 0.06, 'woodDark', x, 0, z);
  for (const s of [-1, 1]) {
    p.box(0.34, 0.05, 0.2, 'woodDark', 0, 0.34, s * 0.36);
    for (const x of [-0.14, 0.14]) p.box(0.04, 0.34, 0.04, 'woodDark', x, 0, s * 0.36);
    p.box(0.34, 0.42, 0.05, 'woodDark', 0, 0.36, s * 0.46);
  }
}

/** Sofa, its seat to the north. */
function sofa(p: Parts): void {
  for (const x of [-0.36, 0.36]) for (const z of [-0.22, 0.28]) p.box(0.06, 0.06, 0.06, 'darkMetal', x, 0, z);
  p.box(0.86, 0.3, 0.6, 'leather', 0, 0.06, 0.03);
  p.box(0.86, 0.38, 0.16, 'leather', 0, 0.36, 0.25);
  for (const x of [-0.37, 0.37]) p.box(0.12, 0.18, 0.6, 'leather', x, 0.36, 0.03);
  for (const x of [-0.15, 0.15]) p.box(0.29, 0.08, 0.42, 'fabric', x, 0.36, -0.04);
}

/** Bed, its head against the wall to the south and its foot to the north. */
function bed(p: Parts): void {
  for (const x of [-0.3, 0.3]) for (const z of [-0.4, 0.4]) p.box(0.06, 0.08, 0.06, 'woodDark', x, 0, z);
  p.box(0.68, 0.22, 0.9, 'woodDark', 0, 0.08, 0);
  p.box(0.64, 0.12, 0.86, 'linen', 0, 0.3, 0);
  p.box(0.66, 0.06, 0.58, 'blanket', 0, 0.38, -0.14);
  p.box(0.42, 0.08, 0.18, 'linen', 0, 0.42, 0.3);
  p.box(0.72, 0.68, 0.06, 'woodDark', 0, 0, 0.45);
}

/** Office desk: the chair to the north, the screen facing it; desks side by side make one bench. */
function desk(p: Parts): void {
  p.span(-0.5, 0.5, 0.05, -0.14, 0.42, 'wood', 0.66);
  for (const x of [-0.44, 0.44]) p.box(0.06, 0.66, 0.5, 'darkMetal', x, 0, 0.14);
  p.span(-0.5, 0.5, 0.36, 0.36, 0.4, 'darkMetal', 0.28);
  p.box(0.06, 0.08, 0.06, 'darkMetal', 0, 0.71, 0.3);
  p.box(0.38, 0.26, 0.05, 'darkMetal', 0, 0.78, 0.3);
  p.box(0.32, 0.2, 0.02, 'screen', 0, 0.81, 0.27);
  // The chair.
  p.cyl(0.04, 0.04, 0.36, 'darkMetal', 0, 0, -0.32, 6);
  p.box(0.36, 0.06, 0.32, 'fabricDark', 0, 0.36, -0.32);
  p.box(0.36, 0.34, 0.06, 'fabricDark', 0, 0.42, -0.46);
}

/** Lab bench: a steel top on cabinets, an instrument and a screen. */
function labBench(p: Parts): void {
  p.span(-0.5, 0.5, 0.62, -0.1, 0.4, 'metal', 0);
  p.span(-0.5, 0.5, 0.06, -0.18, 0.44, 'darkMetal', 0.62);
  for (const x of [-0.25, 0.25]) p.box(0.3, 0.03, 0.02, 'darkMetal', x, 0.48, -0.11);
  p.box(0.28, 0.26, 0.24, 'darkMetal', -0.2, 0.68, 0.18);
  p.box(0.12, 0.06, 0.02, 'lightCool', -0.2, 0.84, 0.05);
  p.box(0.32, 0.24, 0.05, 'darkMetal', 0.22, 0.68, 0.32);
  p.box(0.28, 0.18, 0.02, 'screen', 0.22, 0.71, 0.29);
}

/** Wardrobe, its back against the wall to the south. */
function wardrobe(p: Parts): void {
  p.box(0.86, 0.1, 0.52, 'woodDark', 0, 0, 0.2);
  p.box(0.82, 1.74, 0.5, 'wood', 0, 0.1, 0.2);
  p.box(0.88, 0.06, 0.54, 'woodDark', 0, 1.84, 0.2);
  p.box(0.02, 1.6, 0.02, 'woodDark', 0, 0.16, -0.06);
  for (const x of [-0.06, 0.06]) p.box(0.03, 0.18, 0.03, 'darkMetal', x, 0.95, -0.07);
}

/** Two steel filing cabinets, their backs against the wall to the south. */
function cabinet(p: Parts): void {
  for (const x of [-0.21, 0.21]) {
    p.box(0.4, 1.8, 0.5, 'metal', x, 0, 0.2);
    for (let k = 0; k < 4; k++) p.box(0.24, 0.04, 0.02, 'darkMetal', x, 0.3 + k * 0.42, -0.06);
  }
}

/** A run of pipes on a concrete sleeper. */
function pipeRun(p: Parts): void {
  const pipe = (r: number, y: number, z: number, role: Role) => p.add(new THREE.CylinderGeometry(r, r, 0.997, 12).rotateZ(Math.PI / 2), role, -0.0005, y, z);
  p.box(0.18, 0.3, 0.78, 'concreteDark', 0, 0, 0.04);
  pipe(0.22, 0.52, -0.12, 'metal');
  pipe(0.13, 0.43, 0.26, 'paintYellow');
  p.add(new THREE.CylinderGeometry(0.26, 0.26, 0.06, 12).rotateZ(Math.PI / 2), 'darkMetal', 0, 0.52, -0.12);
}

/** Two wheelie bins, lids shut. */
function bins(p: Parts): void {
  for (const [x, role] of [[-0.21, 'paintGreen'], [0.21, 'paint']] as const) {
    p.box(0.38, 0.72, 0.46, role, x, 0.08, 0);
    p.box(0.4, 0.06, 0.5, 'darkMetal', x, 0.8, -0.01);
    p.add(new THREE.CylinderGeometry(0.08, 0.08, 0.36, 8).rotateZ(Math.PI / 2), 'rubber', x, 0.08, 0.18);
  }
}

/** Street tree in a raised pit: the pit is the cover, the crown stays above head height. */
function streetTree(p: Parts): void {
  const rand = p.rand();
  p.box(0.82, 0.46, 0.82, 'concrete', 0, 0, 0);
  p.box(0.68, 0.04, 0.68, 'soil', 0, 0.44, 0);
  p.cyl(0.06, 0.09, 1.4, 'bark', 0, 0.46, 0, 8);
  for (let i = 0; i < 4; i++) {
    const a = rand() * Math.PI * 2;
    const r = i === 0 ? 0 : 0.16;
    p.blob(0.26 + rand() * 0.08, i % 2 ? 'foliageLight' : 'foliage', Math.cos(a) * r, 1.86 + rand() * 0.24, Math.sin(a) * r);
  }
}

/** Edge of a fountain's basin: the stone rim on its outer (north) side, water behind it. */
function fountainEdge(p: Parts): void {
  p.span(-0.5, 0.5, 0.5, -0.5, -0.26, 'concrete', 0);
  p.span(-0.5, 0.5, 0.08, -0.5, -0.22, 'concreteDark', 0.5);
  p.span(-0.5, 0.5, 0.36, -0.26, 0.5, 'concreteDark', 0);
  p.span(-0.5, 0.5, 0.06, -0.26, 0.5, 'water', 0.36);
}

/** Corner of a fountain's basin: the rim on its north and west sides. */
function fountainCorner(p: Parts): void {
  p.span(-0.5, 0.5, 0.5, -0.5, -0.26, 'concrete', 0);
  p.span(-0.5, -0.26, 0.5, -0.26, 0.5, 'concrete', 0);
  p.span(-0.5, 0.5, 0.08, -0.5, -0.22, 'concreteDark', 0.5);
  p.span(-0.5, -0.22, 0.08, -0.22, 0.5, 'concreteDark', 0.5);
  p.span(-0.26, 0.5, 0.36, -0.26, 0.5, 'concreteDark', 0);
  p.span(-0.26, 0.5, 0.06, -0.26, 0.5, 'water', 0.36);
}

/** The figure in the middle of a fountain, on a pedestal standing in the water. */
function statue(p: Parts): void {
  p.span(-0.5, 0.5, 0.36, -0.5, 0.5, 'concreteDark', 0);
  p.span(-0.5, 0.5, 0.06, -0.5, 0.5, 'water', 0.36);
  p.box(0.44, 0.66, 0.44, 'concrete', 0, 0.36, 0);
  p.box(0.52, 0.08, 0.52, 'concreteDark', 0, 1.0, 0);
  p.box(0.24, 0.36, 0.16, 'bronze', 0, 1.08, 0);
  p.box(0.28, 0.36, 0.18, 'bronze', 0, 1.44, 0);
  p.box(0.08, 0.34, 0.08, 'bronze', 0.18, 1.72, 0);
  p.blob(0.11, 'bronze', 0, 1.9, 0);
}

/** A heap of broken masonry. */
function rubbleHeap(p: Parts): void {
  const rand = p.rand();
  p.add(new THREE.DodecahedronGeometry(0.36, 0), 'rock', -0.06, 0.22, 0.04, [rand(), rand(), rand()]).scale.set(1.2, 0.75, 1);
  for (let i = 0; i < 5; i++) {
    const a = rand() * Math.PI * 2;
    p.add(new THREE.BoxGeometry(0.22, 0.14, 0.18), i % 2 ? 'concrete' : 'rockDark', Math.cos(a) * 0.28, 0.08 + rand() * 0.36, Math.sin(a) * 0.28, [rand(), rand() * 3, rand()]);
  }
}

/** A stub of wall still standing, broken off at the top, rubble at its foot. */
function wallStub(p: Parts): void {
  p.box(0.86, 1.24, 0.3, 'rock', 0, 0, 0);
  p.box(0.48, 0.42, 0.3, 'rock', -0.19, 1.24, 0);
  p.box(0.22, 0.22, 0.3, 'rock', 0.2, 1.24, 0);
  p.box(0.2, 0.18, 0.3, 'rockDark', -0.32, 1.66, 0);
  p.add(new THREE.DodecahedronGeometry(0.2, 0), 'rockDark', 0.2, 0.1, -0.28);
  p.add(new THREE.BoxGeometry(0.2, 0.12, 0.16), 'concrete', -0.22, 0.06, -0.3, [0, 0.6, 0.2]);
}

/** The facility's server rack (its lights look south) turned to face north like the decor props. */
function serverRack(p: Parts): void {
  rack(p);
  p.group.rotation.y = Math.PI;
}

/** Fallen logs lying along x. */
function logRow(p: Parts): void {
  logs(p);
  p.group.rotation.y = Math.PI / 2;
}

// ------------------------------------------------------------- catalogue

const SETS = {
  cityLow: [crates, sandbags, barrier, planter, dumpster],
  cityHigh: [kiosk, containers, pillar, vending],
  wildsLow: [rock, logs, bush, rock],
  wildsHigh: [tree, pine, tree, spire],
  facilityLow: [techCrate, techBarrier, generator],
  facilityHigh: [rack, tank, pylon, rack],
  console: [consoleDesk],
  // Decor: what mapView draws for each hint (one variant, or a few to vary).
  carFront: CAR_PAINTS.map((paint) => (p: Parts) => carHalf(p, paint, true)),
  carRear: CAR_PAINTS.map((paint) => (p: Parts) => carHalf(p, paint, false)),
  planter: [planter],
  bin: [bins],
  streetTree: [streetTree],
  kiosk: [kiosk],
  fountain: [fountainEdge],
  fountainCorner: [fountainCorner],
  statue: [statue],
  fencePost: [fencePost],
  fenceRails: [fenceRails],
  fenceBoards: [fenceBoards],
  gatePost: [gatePost],
  hayBale: [hayBale],
  hayStack: [hayStack],
  shelfLow: [shelfLow],
  shelfHigh: [shelfHigh],
  counter: [counter],
  table: [table],
  sofa: [sofa],
  bed: [bed],
  desk: [desk],
  labBench: [labBench],
  wardrobe: [wardrobe],
  cabinet: [cabinet],
  rack: [serverRack],
  crates: [crates],
  techCrate: [techCrate],
  containers: [containers],
  barricade: [sandbags, barrier],
  sandbags: [sandbags],
  techBarrier: [techBarrier],
  pipe: [pipeRun],
  tank: [tank],
  pylon: [pylon],
  tree: [tree, tree],
  forest: [tree, pine, tree],
  bush: [bush],
  rock: [rock, rock],
  spire: [spire],
  logs: [logRow],
  rubble: [rubbleHeap, rubbleHeap],
  wallStub: [wallStub],
} satisfies Record<string, ((p: Parts) => void)[]>;

export type PropSet = keyof typeof SETS;

export interface PropModel {
  /** Lit voxels (vertex colours); null if the prop has none. */
  lit: THREE.BufferGeometry | null;
  /** Glowing voxels, drawn unlit. */
  glow: THREE.BufferGeometry | null;
}

const cache = new Map<string, PropModel>();

export function propVariants(set: PropSet): number {
  return SETS[set].length;
}

/** The voxel model of one prop variant, built on first use and shared. */
export function propModel(set: PropSet, variant: number): PropModel {
  const key = `${set}:${variant}`;
  const cached = cache.get(key);
  if (cached) return cached;
  const parts = new Parts(variant + 1);
  SETS[set][variant % SETS[set].length]!(parts);
  parts.group.updateMatrixWorld(true);
  const sources: { mesh: THREE.Mesh; matrix: THREE.Matrix4 }[] = [];
  parts.group.traverse((o) => {
    if (o instanceof THREE.Mesh) sources.push({ mesh: o, matrix: o.matrixWorld.clone() });
  });
  const model = voxelize(sources, { size: GAME_VOXEL, jitter: 0.12 });
  const meshes = model.children.filter((c): c is THREE.Mesh => c instanceof THREE.Mesh);
  const out: PropModel = {
    lit: meshes.find((m) => m.material instanceof THREE.MeshStandardMaterial)?.geometry ?? null,
    glow: meshes.find((m) => m.material instanceof THREE.MeshBasicMaterial)?.geometry ?? null,
  };
  cache.set(key, out);
  return out;
}
