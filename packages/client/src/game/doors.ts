import * as THREE from 'three';
import type { UvRect } from './batch';
import { timeRate } from './tween';

// Door leaves (buildings.ts): kept out of the walls' merged meshes, one
// instanced mesh per kind of leaf for the whole map, so a door swings open
// and shut on its hinge, a quarter turn into the building (Gameplay,
// 2026-10-14), instead of being there at once. Each leaf carries what its
// wall does for the cutaway (wallCutPatch reads the instance's matrix), and
// how far open it is lives on across the walls' rebuilds: the rebuild a door
// opening sets off finds it shut, and it swings from there.

/** Seconds a door takes to swing open or shut. */
const SWING_SECONDS = 0.45;

/** What a door leaf carries for its wall's material (wallCutPatch), one value per instance. */
export interface LeafConsts {
  aCut: readonly number[];
  aLevel: readonly number[];
  aOuter: readonly number[];
  aInner: readonly number[];
  aAtlas: readonly number[];
}

/** One door leaf, as a rebuild of the walls places it. */
export interface LeafPlace {
  /** The kind of leaf (its piece and part): one instanced mesh each. */
  kind: string;
  geometry: THREE.BufferGeometry;
  material: THREE.Material;
  /** Where its own atlas sits in the packed texture (mapAtlas.ts), if it is packed. */
  uv: UvRect | undefined;
  /** The piece's place on the map, and the leaf in the piece's frame (thickened as its wall). */
  piece: THREE.Matrix4;
  local: THREE.Matrix4;
  /** The hinge in the piece's frame, thickened too. */
  hinge: THREE.Vector3;
  consts: LeafConsts;
  open: boolean;
}

interface Leaf extends LeafPlace {
  tile: number;
  /** How far open, 0 to 1, easing towards `open`. */
  swing: number;
  /** Put again by the rebuild going on (the ones it went over and didn't are gone). */
  kept: boolean;
}

const CONSTS: [keyof LeafConsts, number][] = [
  ['aCut', 4],
  ['aLevel', 1],
  ['aOuter', 3],
  ['aInner', 3],
  ['aAtlas', 1],
];

const m1 = new THREE.Matrix4();
const m2 = new THREE.Matrix4();

export class DoorLeaves {
  readonly group = new THREE.Group();
  private readonly leaves = new Map<string, Leaf>();
  private meshes: { mesh: THREE.InstancedMesh; leaves: Leaf[] }[] = [];
  /** Each kind's geometry, its UVs moved into the packed texture: what each commit's meshes copy. */
  private readonly bases = new Map<string, THREE.BufferGeometry>();
  /** Some leaf is between shut and open, or on its way. */
  private moving = false;

  /** A rebuild of the tiles marked in `area` (null: every one): their leaves are put again, the others stay. */
  begin(area: Uint8Array | null): void {
    for (const leaf of this.leaves.values()) if (!area || area[leaf.tile]) leaf.kept = false;
  }

  /** The leaf `key` (its storey, tile and place among the tile's pieces) of tile `tile`, as the walls are now. */
  put(key: string, tile: number, place: LeafPlace): void {
    const was = this.leaves.get(key);
    const swing = was && was.kind === place.kind ? was.swing : place.open ? 1 : 0;
    this.leaves.set(key, { ...place, tile, swing, kept: true });
    if (swing !== (place.open ? 1 : 0)) this.moving = true;
  }

  /** The rebuild done: the leaves it didn't put again go, and the meshes are made again. */
  commit(): void {
    for (const [key, leaf] of this.leaves) if (!leaf.kept) this.leaves.delete(key);
    for (const { mesh } of this.meshes) mesh.geometry.dispose();
    this.group.clear();
    this.meshes = [];
    const kinds = new Map<string, Leaf[]>();
    for (const leaf of this.leaves.values()) {
      let list = kinds.get(leaf.kind);
      if (!list) kinds.set(leaf.kind, (list = []));
      list.push(leaf);
    }
    for (const [kind, leaves] of kinds) {
      const geometry = this.base(kind, leaves[0]!).clone();
      for (const [name, size] of CONSTS) {
        const values = new Float32Array(leaves.length * size);
        leaves.forEach((leaf, k) => values.set(leaf.consts[name], k * size));
        geometry.setAttribute(name, new THREE.InstancedBufferAttribute(values, size));
      }
      const mesh = new THREE.InstancedMesh(geometry, leaves[0]!.material, leaves.length);
      mesh.castShadow = true;
      mesh.receiveShadow = true;
      leaves.forEach((leaf, k) => mesh.setMatrixAt(k, this.place(leaf)));
      mesh.computeBoundingSphere();
      this.group.add(mesh);
      this.meshes.push({ mesh, leaves });
    }
  }

  /** Swings the doors that are opening or shutting, at the battle's speed (tween.ts timeRate). */
  update(dt: number): void {
    if (!this.moving) return;
    const step = (dt * timeRate()) / SWING_SECONDS;
    let moving = false;
    for (const { mesh, leaves } of this.meshes) {
      let touched = false;
      leaves.forEach((leaf, k) => {
        const goal = leaf.open ? 1 : 0;
        if (leaf.swing === goal) return;
        leaf.swing = goal > leaf.swing ? Math.min(goal, leaf.swing + step) : Math.max(goal, leaf.swing - step);
        mesh.setMatrixAt(k, this.place(leaf));
        touched = true;
        if (leaf.swing !== goal) moving = true;
      });
      if (touched) mesh.instanceMatrix.needsUpdate = true;
    }
    this.moving = moving;
  }

  /** A leaf's matrix: its piece's place, turned on the hinge as far as it is open (eased at both ends). */
  private place(leaf: Leaf): THREE.Matrix4 {
    const s = leaf.swing;
    const turn = ((s * s * (3 - 2 * s)) * Math.PI) / 2;
    const h = leaf.hinge;
    return m1
      .copy(leaf.piece)
      .multiply(m2.makeTranslation(h.x, 0, h.z))
      .multiply(m2.makeRotationY(turn))
      .multiply(m2.makeTranslation(-h.x, 0, -h.z))
      .multiply(leaf.local);
  }

  /** A kind's geometry with plain attributes and its UVs in the packed texture, made once. */
  private base(kind: string, leaf: Leaf): THREE.BufferGeometry {
    let g = this.bases.get(kind);
    if (g) return g;
    const src = leaf.geometry;
    const read = (name: string, size: number) => {
      const a = src.getAttribute(name);
      if (!a) return null;
      const out = new Float32Array(a.count * size);
      for (let i = 0; i < a.count; i++) for (let k = 0; k < size; k++) out[i * size + k] = a.getComponent(i, k);
      return out;
    };
    g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(read('position', 3)!, 3));
    const normal = read('normal', 3);
    if (normal) g.setAttribute('normal', new THREE.BufferAttribute(normal, 3));
    const uv = read('uv', 2);
    if (uv) {
      if (leaf.uv) {
        const [ox, oy, sx, sy] = leaf.uv;
        for (let i = 0; i < uv.length; i += 2) {
          uv[i] = ox + uv[i]! * sx;
          uv[i + 1] = oy + uv[i + 1]! * sy;
        }
      }
      g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
    }
    if (src.index) g.setIndex(Array.from({ length: src.index.count }, (_, i) => src.index!.getX(i)));
    this.bases.set(kind, g);
    return g;
  }

  dispose(): void {
    for (const { mesh } of this.meshes) mesh.geometry.dispose();
    for (const g of this.bases.values()) g.dispose();
    this.group.clear();
  }
}
