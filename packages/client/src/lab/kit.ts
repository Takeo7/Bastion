import * as THREE from 'three';
import { COVER, type PropDef, type PropKind } from './layout';
import { rng } from './textures';

// Procedural props built from primitives. Every part asks the style for a
// material by *role* (brick, glass, alien glow...), so the same street can be
// rendered realistic, toon, voxel or holographic.

export type Role =
  | 'brick'
  | 'concrete'
  | 'concreteDark'
  | 'metal'
  | 'darkMetal'
  | 'paint'
  | 'paint2'
  | 'glass'
  | 'window'
  | 'wood'
  | 'woodDark'
  | 'fabric'
  | 'foliage'
  | 'bark'
  | 'soil'
  | 'rubber'
  | 'stripe'
  | 'lightWarm'
  | 'lightCool'
  | 'alienMetal'
  | 'alienGlow';

/** Style-independent description of a surface. */
export interface MatSpec {
  color: THREE.Color;
  emissive?: THREE.Color;
  emissiveIntensity?: number;
  metalness: number;
  roughness: number;
  opacity?: number;
  role?: Role;
  /** Belongs to an alien (team tint in the holographic style). */
  alien?: boolean;
}

type SpecInit = [color: number, metalness: number, roughness: number, extra?: Partial<Omit<MatSpec, 'color' | 'emissive'>> & { emissive?: number }];

const PALETTE: Record<Role, SpecInit> = {
  brick: [0x8a5a48, 0, 0.9],
  concrete: [0x9a968c, 0, 0.92],
  concreteDark: [0x6c6a64, 0, 0.95],
  metal: [0x7d8790, 0.7, 0.4],
  darkMetal: [0x30353b, 0.6, 0.5],
  paint: [0x2f5a78, 0.3, 0.45],
  paint2: [0x3f7a5a, 0.2, 0.55],
  glass: [0x1d2a33, 0.1, 0.08, { opacity: 0.85 }],
  window: [0x2a3138, 0.1, 0.1, { emissive: 0xffb36b, emissiveIntensity: 0.35 }],
  wood: [0x9a7448, 0, 0.8],
  woodDark: [0x5e4428, 0, 0.85],
  fabric: [0xa08f62, 0, 1],
  foliage: [0x4c7a3a, 0, 0.9],
  bark: [0x5a4130, 0, 0.95],
  soil: [0x3e2f24, 0, 1],
  rubber: [0x1c1d1f, 0, 0.9],
  stripe: [0xc9472f, 0.1, 0.6],
  lightWarm: [0xffe2b0, 0, 0.5, { emissive: 0xffc070, emissiveIntensity: 3 }],
  lightCool: [0xc8f1ff, 0, 0.5, { emissive: 0x7fe0ff, emissiveIntensity: 2.2 }],
  alienMetal: [0x3b2c3a, 0.6, 0.35, { alien: true }],
  alienGlow: [0xff7ad0, 0, 0.4, { emissive: 0xff3aa8, emissiveIntensity: 3, alien: true }],
};

export function baseSpec(role: Role): MatSpec {
  const [color, metalness, roughness, extra] = PALETTE[role];
  const { emissive, ...rest } = extra ?? {};
  return {
    color: new THREE.Color(color),
    emissive: emissive !== undefined ? new THREE.Color(emissive) : undefined,
    metalness,
    roughness,
    role,
    ...rest,
  };
}

/** Description of a unit material from the existing placeholder models. */
export function specFromMaterial(m: THREE.Material, alien: boolean): MatSpec | null {
  if (!(m instanceof THREE.MeshStandardMaterial)) return null;
  const glow = m.emissiveIntensity > 0 && m.emissive.getHex() !== 0;
  return {
    color: m.color.clone(),
    emissive: glow ? m.emissive.clone() : undefined,
    emissiveIntensity: glow ? m.emissiveIntensity : undefined,
    metalness: m.metalness,
    roughness: m.roughness,
    alien,
  };
}

export interface KitCtx {
  mat(role: Role): THREE.Material;
  /** Radial segments for round shapes (low for the low-poly look). */
  seg: number;
}

/** Light a style may attach (street lamps, alien relay...). */
export interface LightAnchor {
  color: number;
  intensity: number;
  distance: number;
  kind: 'spot' | 'point';
}

class Parts {
  readonly group = new THREE.Group();
  constructor(readonly k: KitCtx) {}

  add(geometry: THREE.BufferGeometry, role: Role, x: number, y: number, z: number, rot?: [number, number, number]): THREE.Mesh {
    const mesh = new THREE.Mesh(geometry, this.k.mat(role));
    mesh.position.set(x, y, z);
    if (rot) mesh.rotation.set(rot[0], rot[1], rot[2]);
    mesh.castShadow = role !== 'glass' && role !== 'window';
    mesh.receiveShadow = true;
    mesh.userData.role = role;
    this.group.add(mesh);
    return mesh;
  }

  /** Box resting on `y` (its bottom face). */
  box(w: number, h: number, d: number, role: Role, x: number, y: number, z: number, rotY = 0): THREE.Mesh {
    return this.add(new THREE.BoxGeometry(w, h, d), role, x, y + h / 2, z, [0, rotY, 0]);
  }

  cyl(rTop: number, rBottom: number, h: number, role: Role, x: number, y: number, z: number, seg = this.k.seg): THREE.Mesh {
    return this.add(new THREE.CylinderGeometry(rTop, rBottom, h, seg), role, x, y + h / 2, z);
  }

  blob(r: number, role: Role, x: number, y: number, z: number): THREE.Mesh {
    const g = this.k.seg <= 8 ? new THREE.IcosahedronGeometry(r, 0) : new THREE.IcosahedronGeometry(r, 2);
    return this.add(g, role, x, y, z);
  }

  anchor(x: number, y: number, z: number, light: LightAnchor): void {
    const o = new THREE.Object3D();
    o.position.set(x, y, z);
    o.userData.light = light;
    this.group.add(o);
  }
}

// ------------------------------------------------------------- building

function plinthAndCornice(p: Parts): void {
  p.box(1, 0.3, 1.04, 'concrete', 0, 0, 0);
  p.box(1, 0.2, 1.08, 'concrete', 0, 2.1, 0);
}

function wall(p: Parts, seed: number): void {
  plinthAndCornice(p);
  p.box(1, 1.8, 1, 'brick', 0, 0.3, 0);
  if (seed % 6 === 1) p.cyl(0.04, 0.04, 2.15, 'darkMetal', 0.38, 0, 0.55, 6);
  if (seed % 7 === 3) p.box(0.42, 0.56, 0.02, 'paint2', -0.1, 1.0, 0.51);
}

function windowWall(p: Parts): void {
  plinthAndCornice(p);
  p.box(1, 0.65, 1, 'brick', 0, 0.3, 0);
  p.box(0.2, 0.9, 1, 'brick', -0.4, 0.95, 0);
  p.box(0.2, 0.9, 1, 'brick', 0.4, 0.95, 0);
  p.box(1, 0.25, 1, 'brick', 0, 1.85, 0);
  p.box(0.6, 0.9, 0.05, 'window', 0, 0.95, 0.15);
  p.box(0.04, 0.9, 0.08, 'darkMetal', 0, 0.95, 0.17);
  p.box(0.76, 0.06, 0.24, 'concrete', 0, 0.92, 0.52);
}

function door(p: Parts): void {
  plinthAndCornice(p);
  p.box(0.2, 1.65, 1, 'brick', -0.4, 0.3, 0);
  p.box(0.2, 1.65, 1, 'brick', 0.4, 0.3, 0);
  p.box(1, 0.15, 1, 'brick', 0, 1.95, 0);
  p.box(0.6, 1.65, 0.08, 'darkMetal', 0, 0.3, 0.3);
  p.box(0.04, 0.5, 0.05, 'metal', 0.2, 1.0, 0.36);
  p.box(0.84, 0.08, 0.34, 'concrete', 0, 0, 0.62);
  p.box(0.22, 0.08, 0.12, 'lightWarm', 0, 1.98, 0.56);
  p.anchor(0, 1.9, 0.75, { color: 0xffb070, intensity: 4, distance: 4, kind: 'point' });
}

// ---------------------------------------------------------------- props

function van(p: Parts): void {
  const tilt = new THREE.Group();
  const outer = p.group;
  p.box(0.92, 0.75, 1.92, 'paint', 0, 0.2, 0);
  p.box(0.9, 0.45, 1.3, 'paint', 0, 0.95, -0.3);
  p.box(0.86, 0.36, 0.55, 'glass', 0, 0.95, 0.62);
  p.box(0.88, 0.05, 0.6, 'paint', 0, 1.31, 0.6);
  p.box(0.94, 0.12, 1.4, 'stripe', 0, 0.62, -0.25);
  p.box(0.94, 0.12, 0.08, 'metal', 0, 0.22, 0.98);
  p.box(0.94, 0.12, 0.08, 'metal', 0, 0.22, -0.98);
  p.box(0.18, 0.1, 0.03, 'lightWarm', -0.3, 0.55, 0.97);
  p.box(0.18, 0.1, 0.03, 'lightWarm', 0.3, 0.55, 0.97);
  for (const [x, z] of [[-0.44, 0.62], [0.44, 0.62], [-0.44, -0.62], [0.44, -0.62]] as const) {
    p.add(new THREE.CylinderGeometry(0.19, 0.19, 0.14, p.k.seg).rotateZ(Math.PI / 2), 'rubber', x, 0.19, z);
  }
  // Abandoned at an angle, slightly sunk on one side.
  tilt.add(...outer.children);
  tilt.rotation.set(0.0, 0.08, 0.035);
  outer.add(tilt);
}

function sandbags(p: Parts, seed: number): void {
  const rand = rng(seed + 40);
  const g = new THREE.CapsuleGeometry(0.15, 0.18, 3, p.k.seg).rotateX(Math.PI / 2).scale(1.2, 0.72, 1);
  for (let layer = 0; layer < 4; layer++) {
    const count = layer === 3 ? 3 : 4;
    const offset = layer % 2 ? 0.24 : 0;
    for (let i = 0; i < count; i++) {
      const z = -0.72 + i * 0.48 + offset;
      p.add(g, 'fabric', (rand() - 0.5) * 0.06, 0.12 + layer * 0.21, z, [0, (rand() - 0.5) * 0.25, (rand() - 0.5) * 0.08]);
    }
  }
}

function crate(p: Parts, size: number, x: number, y: number, z: number, rotY: number): void {
  const inner = new Parts(p.k);
  inner.box(size, size, size, 'wood', 0, 0, 0);
  const t = 0.05;
  for (const [cx, cz] of [[-1, -1], [1, -1], [-1, 1], [1, 1]] as const) {
    inner.box(t, size, t, 'woodDark', (cx * (size - t)) / 2 + cx * 0.008, 0, (cz * (size - t)) / 2 + cz * 0.008);
  }
  inner.box(size + 0.02, t, size + 0.02, 'woodDark', 0, 0, 0);
  inner.box(size + 0.02, t, size + 0.02, 'woodDark', 0, size - t, 0);
  inner.box(size * 0.95, t * 0.8, 0.02, 'woodDark', 0, size / 2, size / 2 + 0.005, 0).rotation.z = Math.atan2(size, size) * 0.9;
  inner.group.position.set(x, y, z);
  inner.group.rotation.y = rotY;
  p.group.add(inner.group);
}

function crates(p: Parts, seed: number): void {
  const rand = rng(seed + 90);
  crate(p, 0.66, -0.06, 0, 0.04, (rand() - 0.5) * 0.4);
  crate(p, 0.42, 0.08, 0.66, -0.04, (rand() - 0.5) * 0.8);
  crate(p, 0.36, 0.3, 0, -0.32, (rand() - 0.5) * 0.6);
}

function barrier(p: Parts): void {
  const s = new THREE.Shape();
  s.moveTo(-0.32, 0);
  s.lineTo(0.32, 0);
  s.lineTo(0.3, 0.1);
  s.lineTo(0.12, 0.3);
  s.lineTo(0.1, 0.82);
  s.lineTo(-0.1, 0.82);
  s.lineTo(-0.12, 0.3);
  s.lineTo(-0.3, 0.1);
  s.closePath();
  const g = new THREE.ExtrudeGeometry(s, { depth: 0.9, bevelEnabled: false });
  g.translate(0, 0, -0.45);
  p.add(g, 'concrete', 0, 0, -0.48);
  p.add(g, 'concrete', 0, 0, 0.48, [0, 0.06, 0]);
  for (const z of [-0.48, 0.48]) {
    p.box(0.215, 0.1, 0.5, 'stripe', 0, 0.62, z);
  }
}

function planter(p: Parts): void {
  p.box(0.86, 0.48, 0.86, 'concrete', 0, 0, 0);
  p.box(0.74, 0.04, 0.74, 'soil', 0, 0.46, 0);
  p.blob(0.32, 'foliage', -0.12, 0.72, 0.05);
  p.blob(0.26, 'foliage', 0.18, 0.66, -0.12);
  p.blob(0.22, 'foliage', 0.1, 0.84, 0.18);
}

function lamp(p: Parts): void {
  p.cyl(0.1, 0.13, 0.3, 'darkMetal', 0, 0, 0);
  p.cyl(0.045, 0.06, 3.4, 'darkMetal', 0, 0.3, 0, 8);
  p.box(0.06, 0.06, 0.95, 'darkMetal', 0, 3.6, 0.45);
  p.box(0.24, 0.09, 0.42, 'darkMetal', 0, 3.52, 0.95);
  p.box(0.18, 0.02, 0.34, 'lightWarm', 0, 3.5, 0.95);
  p.anchor(0, 3.42, 0.95, { color: 0xffbf73, intensity: 60, distance: 14, kind: 'spot' });
}

function hydrant(p: Parts): void {
  p.cyl(0.11, 0.13, 0.08, 'stripe', 0, 0, 0);
  p.cyl(0.085, 0.09, 0.42, 'stripe', 0, 0.08, 0);
  p.blob(0.09, 'stripe', 0, 0.5, 0);
  p.add(new THREE.CylinderGeometry(0.04, 0.04, 0.3, 8).rotateZ(Math.PI / 2), 'metal', 0, 0.36, 0);
}

function relay(p: Parts): void {
  p.cyl(0.44, 0.48, 0.24, 'alienMetal', 0, 0, 0, 6);
  p.cyl(0.17, 0.27, 1.25, 'alienMetal', 0, 0.24, 0, 6);
  const crystal = p.add(new THREE.OctahedronGeometry(0.26, 0), 'alienGlow', 0, 1.82, 0);
  crystal.scale.set(1, 1.9, 1);
  for (const [y, tilt] of [[0.95, 0.25], [1.3, -0.3]] as const) {
    p.add(new THREE.TorusGeometry(0.36, 0.028, 6, p.k.seg * 2).rotateX(Math.PI / 2), 'alienGlow', 0, y, 0, [tilt, 0, tilt * 0.5]);
  }
  for (let i = 0; i < 3; i++) {
    const a = (i / 3) * Math.PI * 2;
    p.box(0.1, 0.9, 0.1, 'alienMetal', Math.cos(a) * 0.36, 0.2, Math.sin(a) * 0.36, -a).rotation.z = 0.2;
  }
  p.anchor(0, 1.8, 0, { color: 0xff3aa8, intensity: 14, distance: 7, kind: 'point' });
}

function alienBarricade(p: Parts): void {
  for (const z of [-0.62, 0, 0.62]) {
    const plate = p.box(0.12, 0.86, 0.56, 'alienMetal', 0, 0, z);
    plate.rotation.z = -0.12;
    p.box(0.05, 0.05, 0.5, 'alienGlow', -0.1, 0.8, z).rotation.z = -0.12;
    p.box(0.34, 0.08, 0.1, 'alienMetal', 0.08, 0, z);
  }
}

function kiosk(p: Parts): void {
  p.box(0.82, 0.08, 0.82, 'concrete', 0, 0, 0);
  p.box(0.76, 1.55, 0.76, 'paint2', 0, 0.08, 0);
  p.box(0.56, 0.5, 0.04, 'glass', 0, 0.85, 0.39);
  p.box(0.6, 0.06, 0.12, 'darkMetal', 0, 0.82, 0.44);
  p.box(0.98, 0.08, 0.98, 'darkMetal', 0, 1.82, 0);
  p.box(0.84, 0.2, 0.84, 'lightCool', 0, 1.62, 0);
  p.box(0.4, 0.5, 0.02, 'paint', -0.1, 0.3, -0.39);
}

function tree(p: Parts, seed: number): void {
  const rand = rng(seed + 7);
  p.cyl(0.44, 0.46, 0.3, 'concrete', 0, 0, 0);
  p.cyl(0.38, 0.38, 0.02, 'soil', 0, 0.3, 0);
  p.cyl(0.06, 0.1, 1.6, 'bark', 0, 0.3, 0, 7);
  for (let i = 0; i < 5; i++) {
    const a = rand() * Math.PI * 2;
    const r = i === 0 ? 0 : 0.28;
    p.blob(0.38 + rand() * 0.18, 'foliage', Math.cos(a) * r, 1.95 + rand() * 0.45, Math.sin(a) * r);
  }
}

function rubble(p: Parts, seed: number): void {
  const rand = rng(seed + 300);
  for (let i = 0; i < 7; i++) {
    const s = 0.08 + rand() * 0.16;
    const m = p.box(s * (1 + rand()), s, s * (1 + rand()), i % 3 === 0 ? 'brick' : 'concreteDark', (rand() - 0.5) * 0.8, 0, (rand() - 0.5) * 0.8, rand() * 3);
    m.rotation.x = (rand() - 0.5) * 0.5;
  }
}

const BUILDERS: Record<PropKind, (p: Parts, seed: number) => void> = {
  wall,
  window: windowWall,
  door,
  car: van,
  sandbags,
  crates,
  barrier,
  planter,
  lamp,
  relay,
  alienBarricade,
  kiosk,
  tree,
  rubble,
  hydrant,
};

/** Builds a prop placed on its footprint. */
export function buildProp(def: PropDef, k: KitCtx): THREE.Group {
  const p = new Parts(k);
  BUILDERS[def.kind](p, def.seed ?? 0);
  p.group.position.set(def.x + (def.w ?? 1) / 2, 0, def.z + (def.d ?? 1) / 2);
  p.group.rotation.y = ((def.rot ?? 0) * Math.PI) / 2;
  p.group.userData.kind = def.kind;
  return p.group;
}

// ------------------------------------------------- current placeholder look

const BLOCKS = {
  wall: { size: [1, 2.3, 1], color: 0x66717c, rough: 0.85, metal: 0 },
  high: { size: [0.82, 1.9, 0.82], color: 0x3f7480, rough: 0.55, metal: 0.3 },
  low: { size: [0.86, 0.85, 0.86], color: 0x8f6c45, rough: 0.85, metal: 0 },
} as const;

/** The game's current look: one plain block per covered tile. */
export function buildBlocks(def: PropDef): THREE.Group | null {
  const cover = COVER[def.kind];
  if (cover === 'none') return null;
  const b = BLOCKS[cover];
  const group = new THREE.Group();
  const material = new THREE.MeshStandardMaterial({ color: b.color, roughness: b.rough, metalness: b.metal });
  for (let dx = 0; dx < (def.w ?? 1); dx++) {
    for (let dz = 0; dz < (def.d ?? 1); dz++) {
      const mesh = new THREE.Mesh(new THREE.BoxGeometry(...b.size), material);
      mesh.position.set(def.x + dx + 0.5, b.size[1] / 2, def.z + dz + 0.5);
      mesh.castShadow = mesh.receiveShadow = true;
      group.add(mesh);
    }
  }
  return group;
}
