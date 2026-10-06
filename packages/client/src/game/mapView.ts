import * as THREE from 'three';
import { DIRS4, type Biome, type GameState, type TileKind } from '@bastion/engine';
import { STOREY } from './coords';
import { propModel, propVariants, type PropSet } from './props';
import { voxelSurface } from './voxel';

const FOG_FACTOR = 0.5;
/** Unseen tiles fade towards a cool slate, so in daylight they read as "unknown", not as mud. */
const FOG_TINT = new THREE.Color(0x26303c);
const FOG_TINT_AMOUNT = 0.3;

interface Palette {
  ground: number;
  street: number;
  roof: number;
  facade: number;
  wall: number;
  high: number[];
  low: number[];
  door: number;
  frame: number;
  glass: number;
  ladder: number;
  accent: number;
}

// Daylight voxel palettes (M5): paving, asphalt, sandstone and brick in the
// city; grass, dirt and stone in the wilds; concrete and steel in facilities.
const PALETTES: Record<Biome, Palette> = {
  city: {
    ground: 0x9a9384,
    street: 0x585d63,
    roof: 0x7d7a74,
    facade: 0xa08a72,
    wall: 0x8a5a48,
    high: [0x3f7480, 0x7a4a3a, 0x4d6a3e],
    low: [0x8f6c45, 0x6a2f30, 0x3a5a7a, 0x7c7f84],
    door: 0x6a4a30,
    frame: 0x3a3f45,
    glass: 0x9fd8ff,
    ladder: 0xd0a43a,
    accent: 0x54d1ff,
  },
  wilds: {
    ground: 0x6a8a45,
    street: 0x9a8460,
    roof: 0x7a6a50,
    facade: 0x8a7458,
    wall: 0x9a8668,
    high: [0x2f5a32, 0x3a6a36, 0x2a4f2e],
    low: [0x7c7c74, 0x6e6a60, 0x8a7a5a],
    door: 0x6a4a30,
    frame: 0x3a2c22,
    glass: 0xaedcf0,
    ladder: 0x8a6a48,
    accent: 0x9dffb0,
  },
  facility: {
    ground: 0x6a7078,
    street: 0x4a4f56,
    roof: 0x5a6068,
    facade: 0x7a8088,
    wall: 0x8a9098,
    high: [0x3a4a5a, 0x4a3a52, 0x334a4a],
    low: [0x4a4f58, 0x52485a, 0x3e4c52],
    door: 0x9a3a4a,
    frame: 0x2a2f36,
    glass: 0x7affd0,
    ladder: 0x9aa4ad,
    accent: 0xff4a6a,
  },
};

const unitBox = new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0);

/** Which voxel props stand on each kind of cover, per biome. */
const PROP_SETS: Record<Biome, { low: PropSet; high: PropSet }> = {
  city: { low: 'cityLow', high: 'cityHigh' },
  wilds: { low: 'wildsLow', high: 'wildsHigh' },
  facility: { low: 'facilityLow', high: 'facilityHigh' },
};

/** Instanced meshes of one shape, remembering which tile each instance belongs to (for fog). */
class Layer {
  private matrices: THREE.Matrix4[] = [];
  private colors: THREE.Color[] = [];
  private tiles: number[] = [];
  mesh: THREE.InstancedMesh | null = null;

  constructor(
    private readonly geometry: THREE.BufferGeometry,
    private readonly material: THREE.Material,
    private readonly shadows = true,
  ) {}

  add(tile: number, matrix: THREE.Matrix4, color: number | THREE.Color): void {
    this.tiles.push(tile);
    this.matrices.push(matrix);
    this.colors.push(new THREE.Color(color));
  }

  build(group: THREE.Group): void {
    if (!this.matrices.length) return;
    const mesh = new THREE.InstancedMesh(this.geometry, this.material, this.matrices.length);
    mesh.castShadow = this.shadows;
    mesh.receiveShadow = true;
    this.matrices.forEach((m, i) => {
      mesh.setMatrixAt(i, m);
      mesh.setColorAt(i, this.colors[i]!);
    });
    this.mesh = mesh;
    group.add(mesh);
  }

  fog(mask: boolean[], factor: number): void {
    if (!this.mesh) return;
    const c = new THREE.Color();
    this.tiles.forEach((tile, i) => {
      c.copy(this.colors[i]!);
      if (!mask[tile]) c.multiplyScalar(factor).lerp(FOG_TINT, FOG_TINT_AMOUNT);
      this.mesh!.setColorAt(i, c);
    });
    this.mesh.instanceColor!.needsUpdate = true;
  }

  dispose(): void {
    this.mesh?.dispose();
  }
}

/** Matrix for a box of size (sx, sy, sz) standing at (x, y, z), turned `yaw` radians. */
function placed(x: number, y: number, z: number, sx: number, sy: number, sz: number, yaw = 0): THREE.Matrix4 {
  return new THREE.Matrix4().compose(
    new THREE.Vector3(x, y, z),
    new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), yaw),
    new THREE.Vector3(sx, sy, sz),
  );
}

/** Stable pseudo-random value in [0, 1) per tile, for variety without state. */
function hash(x: number, y: number, salt = 0): number {
  return ((((x + salt * 31) * 73856093) ^ (y * 19349663) ^ (salt * 83492791)) >>> 0) % 1000 / 1000;
}

const isWallLike = (k: TileKind) => k === 'wall' || k === 'door' || k === 'doorOpen' || k === 'window' || k === 'windowBroken';

/**
 * Terrain: floors at every elevation, raised blocks, walls with their doors and
 * windows, ladders and cover objects, all tinted by the biome and the fog of war.
 * Voxel art (M5): surfaces are painted in voxel cells, cover is a voxel prop
 * (crates, sandbags, kiosks, trees, server racks…) and everything sits on the
 * grid in quarter turns.
 */
export class MapView {
  readonly group = new THREE.Group();
  private layers: Layer[] = [];
  private ladders = new THREE.Group();
  private visibility: boolean[] | null = null;
  private width = 0;
  private materials: THREE.Material[] = [];
  private geometries: THREE.BufferGeometry[] = [];

  build(s: GameState): void {
    this.width = s.width;
    this.rebuild(s);
    const points: number[] = [];
    // Grid lines only on the ground floor; rooftops get their own outline from the slabs.
    for (let x = 0; x <= s.width; x++) points.push(x, 0.004, 0, x, 0.004, s.height);
    for (let y = 0; y <= s.height; y++) points.push(0, 0.004, y, s.width, 0.004, y);
    const grid = new THREE.LineSegments(
      new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(points, 3)),
      new THREE.LineBasicMaterial({ color: 0x7fb7d6, transparent: true, opacity: 0.07 }),
    );
    grid.name = 'grid';
    this.group.add(grid);
  }

  /** Recreates every mesh from the state (called on build and whenever a tile changes). */
  rebuild(s: GameState): void {
    for (const layer of this.layers) layer.dispose();
    for (const m of this.materials) m.dispose();
    for (const g of this.geometries) g.dispose();
    this.layers = [];
    this.materials = [];
    this.geometries = [];
    for (const child of [...this.group.children]) if (child.name !== 'grid') this.group.remove(child);
    this.ladders = new THREE.Group();

    const pal = PALETTES[s.biome] ?? PALETTES.city;
    const std = (opts: THREE.MeshStandardMaterialParameters = {}, jitter?: number) => {
      const m = voxelSurface(new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.85, ...opts }), { storey: STOREY, jitter });
      this.materials.push(m);
      return m;
    };
    const layer = (geometry: THREE.BufferGeometry, material: THREE.Material, shadows = true) => {
      const l = new Layer(geometry, material, shadows);
      this.layers.push(l);
      return l;
    };
    const slabs = layer(new THREE.BoxGeometry(1, 0.1, 1), std({ roughness: 0.95 }, 0.24), false);
    // Raised buildings: their facades get a row of windows per storey.
    const columnMaterial = voxelSurface(new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.8 }), { storey: STOREY, windows: true });
    this.materials.push(columnMaterial);
    const columns = layer(unitBox, columnMaterial);
    const walls = layer(unitBox, std({ roughness: 0.8 }));
    // Plinth and cornice: the bands that make a block read as a building wall.
    const trims = layer(unitBox, std({ roughness: 0.9 }));
    const lows = layer(unitBox, std({ roughness: 0.8 }));
    const doors = layer(unitBox, std({ roughness: 0.6 }));
    const frames = layer(unitBox, std({ roughness: 0.7, metalness: 0.3 }));
    const glassMaterial = new THREE.MeshStandardMaterial({ color: 0xffffff, transparent: true, opacity: 0.5, roughness: 0.1, metalness: 0.4, emissive: new THREE.Color(pal.glass), emissiveIntensity: 0.25 });
    this.materials.push(glassMaterial);
    const glass = layer(unitBox, glassMaterial, false);

    // One instanced layer per prop variant (lit voxels + glowing voxels), created on first use.
    const propLayers = new Map<string, { lit: Layer | null; glow: Layer | null }>();
    const addProp = (tile: number, set: PropSet, x: number, y: number, z: number, pick: number, turn: number) => {
      const variant = Math.floor(pick * propVariants(set));
      const key = `${set}:${variant}`;
      let entry = propLayers.get(key);
      if (!entry) {
        const model = propModel(set, variant);
        const lit = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.85, vertexColors: true });
        const glow = new THREE.MeshBasicMaterial({ color: 0xffffff, vertexColors: true });
        this.materials.push(lit, glow);
        entry = { lit: model.lit ? layer(model.lit, lit) : null, glow: model.glow ? layer(model.glow, glow, false) : null };
        propLayers.set(key, entry);
      }
      entry.lit?.add(tile, placed(x, y, z, 1, 1, 1, turn * (Math.PI / 2)), 0xffffff);
      entry.glow?.add(tile, placed(x, y, z, 1, 1, 1, turn * (Math.PI / 2)), 0xffffff);
    };
    const props = PROP_SETS[s.biome] ?? PROP_SETS.city;

    const generated = s.mapId.startsWith('gen-');
    const street = (x: number, y: number) => generated && (x % 13 >= 10 || y % 13 >= 10);
    const at = (x: number, y: number): TileKind => (x < 0 || y < 0 || x >= s.width || y >= s.height ? 'floor' : s.tiles[y * s.width + x]!);

    for (let y = 0; y < s.height; y++) {
      for (let x = 0; x < s.width; x++) {
        const i = y * s.width + x;
        const kind = s.tiles[i]!;
        const e = s.elev[i] ?? 0;
        const base = e * STOREY;
        const cx = x + 0.5;
        const cz = y + 0.5;
        const jitter = (hash(x, y) - 0.5) * 0.05;

        // Floor slab on top, and the solid block under a rooftop.
        const ground = new THREE.Color(e > 0 ? pal.roof : street(x, y) ? pal.street : pal.ground).offsetHSL(0, 0, jitter);
        slabs.add(i, placed(cx, base - 0.05, cz, 1, 1, 1), ground);
        if (e > 0) columns.add(i, placed(cx, 0, cz, 1, base - 0.1, 1), new THREE.Color(pal.facade).offsetHSL(0, 0, jitter));

        // Doors and windows run along the wall they belong to.
        const alongX = isWallLike(at(x - 1, y)) || isWallLike(at(x + 1, y));
        const yaw = alongX ? 0 : Math.PI / 2;
        const v = hash(x, y, 1);
        switch (kind) {
          case 'wall':
            walls.add(i, placed(cx, base, cz, 1, STOREY, 1), new THREE.Color(pal.wall).offsetHSL(0, 0, jitter));
            trims.add(i, placed(cx, base, cz, 1.03, 0.3, 1.03), new THREE.Color(pal.facade).offsetHSL(0, 0, -0.08));
            trims.add(i, placed(cx, base + STOREY - 0.14, cz, 1.05, 0.14, 1.05), new THREE.Color(pal.facade).offsetHSL(0, 0, 0.05));
            break;
          case 'high':
            addProp(i, props.high, cx, base, cz, v, Math.floor(hash(x, y, 2) * 4));
            break;
          case 'low':
            // On a rooftop, low cover is the parapet along the edge.
            if (e > 0 && s.biome !== 'wilds') lows.add(i, placed(cx, base, cz, 1, 0.5, 1), new THREE.Color(pal.facade).offsetHSL(0, 0, 0.06));
            else addProp(i, props.low, cx, base, cz, v, Math.floor(hash(x, y, 2) * 4));
            break;
          // No lintels: seen from the tactical camera, doors and windows must read as gaps in the wall.
          case 'door':
          case 'doorOpen': {
            const h = STOREY * 0.88;
            frames.add(i, placed(cx + (alongX ? -0.47 : 0), base, cz + (alongX ? 0 : -0.47), alongX ? 0.06 : 1, h, alongX ? 1 : 0.06), pal.frame);
            if (kind === 'door') doors.add(i, placed(cx, base, cz, 0.9, h, 0.1, yaw), pal.door);
            // An open door has swung 90° on its hinge, into the room.
            else if (alongX) doors.add(i, placed(cx - 0.42, base, cz + 0.45, 0.9, h, 0.1, Math.PI / 2), pal.door);
            else doors.add(i, placed(cx + 0.45, base, cz - 0.42, 0.9, h, 0.1, 0), pal.door);
            break;
          }
          case 'window':
          case 'windowBroken': {
            frames.add(i, placed(cx, base, cz, 1, 0.8, 1), pal.wall);
            trims.add(i, placed(cx, base, cz, 1.03, 0.3, 1.03), new THREE.Color(pal.facade).offsetHSL(0, 0, -0.08));
            if (kind === 'window') glass.add(i, placed(cx, base + 0.8, cz, 0.98, STOREY - 1.1, 0.14, yaw), pal.glass);
            break;
          }
          case 'console':
            addProp(i, 'console', cx, base, cz, 0, alongX ? 0 : 1);
            break;
          case 'ladder':
            this.addLadder(s, x, y, pal);
            break;
          case 'floor':
            break;
        }
      }
    }

    for (const l of this.layers) l.build(this.group);
    this.group.add(this.ladders, this.surroundings(s, pal));
    if (this.visibility) this.setVisibility(this.visibility);
  }

  /** Ground beyond the playable area, so the map sits in a landscape instead of floating. */
  private surroundings(s: GameState, pal: Palette): THREE.Mesh {
    const material = voxelSurface(new THREE.MeshStandardMaterial({ color: new THREE.Color(pal.street).multiplyScalar(0.7), roughness: 0.95 }));
    this.materials.push(material);
    const margin = 160;
    const geometry = new THREE.PlaneGeometry(s.width + margin, s.height + margin).rotateX(-Math.PI / 2);
    this.geometries.push(geometry);
    const ground = new THREE.Mesh(geometry, material);
    ground.position.set(s.width / 2, -0.12, s.height / 2);
    ground.receiveShadow = true;
    return ground;
  }

  /** Rails and rungs against the face of the neighbour one storey up. */
  private addLadder(s: GameState, x: number, y: number, pal: Palette): void {
    const e = s.elev[y * s.width + x] ?? 0;
    const material = voxelSurface(new THREE.MeshStandardMaterial({ color: pal.ladder, roughness: 0.5, metalness: 0.4 }));
    this.materials.push(material);
    for (const d of DIRS4) {
      const nx = x + d.x;
      const ny = y + d.y;
      if (nx < 0 || ny < 0 || nx >= s.width || ny >= s.height || (s.elev[ny * s.width + nx] ?? 0) !== e + 1) continue;
      const ladder = new THREE.Group();
      const rail = new THREE.BoxGeometry(0.06, STOREY + 0.3, 0.06);
      for (const side of [-0.22, 0.22]) {
        const r = new THREE.Mesh(rail, material);
        r.position.set(side, (STOREY + 0.3) / 2, 0);
        r.castShadow = true;
        ladder.add(r);
      }
      for (let k = 1; k <= 6; k++) {
        const rung = new THREE.Mesh(new THREE.BoxGeometry(0.44, 0.04, 0.05), material);
        rung.position.set(0, (k * STOREY) / 6.5, 0);
        ladder.add(rung);
      }
      ladder.position.set(x + 0.5 + d.x * 0.42, e * STOREY, y + 0.5 + d.y * 0.42);
      ladder.rotation.y = d.x !== 0 ? Math.PI / 2 : 0;
      this.ladders.add(ladder);
    }
  }

  /** Darkens tiles the squad cannot currently see. */
  setVisibility(mask: boolean[]): void {
    this.visibility = mask;
    for (const l of this.layers) l.fog(mask, FOG_FACTOR);
  }

  isVisible(x: number, y: number): boolean {
    return this.visibility?.[y * this.width + x] ?? true;
  }
}
