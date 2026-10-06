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
} as const;

type Role = keyof typeof ROLES;

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

// ------------------------------------------------------------- catalogue

export type PropSet = 'cityLow' | 'cityHigh' | 'wildsLow' | 'wildsHigh' | 'facilityLow' | 'facilityHigh' | 'console';

const SETS: Record<PropSet, ((p: Parts) => void)[]> = {
  cityLow: [crates, sandbags, barrier, planter, dumpster],
  cityHigh: [kiosk, containers, pillar, vending],
  wildsLow: [rock, logs, bush, rock],
  wildsHigh: [tree, pine, tree, spire],
  facilityLow: [techCrate, techBarrier, generator],
  facilityHigh: [rack, tank, pylon, rack],
  console: [consoleDesk],
};

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
