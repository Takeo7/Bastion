import * as THREE from 'three';
import { carPartner, decorOf, DIRS4, type Biome, type DecorKind, type GameState, type TileKind } from '@bastion/engine';
import { graphics } from '../settings';
import { STOREY } from './coords';
import { ExploredMemory } from './explored';
import { FogOfWar } from './fogOfWar';
import { GroundMap, scatterDetails } from './ground';
import { propModel, propVariants, type PropSet } from './props';
import { atlasTexture } from './synty';
import type { AtlasId } from './syntyManifest';
import { preloadSyntyProps, syntyProp, syntyPropsLoaded, syntySet } from './syntyProps';
import { voxelSurface } from './voxel';

interface Palette {
  ground: number;
  street: number;
  /** Floor inside walled buildings (seen from above: they have no roof). */
  floor: number;
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
    floor: 0x8a705a,
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
    floor: 0x86705a,
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
    floor: 0x7f8892,
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

// Synty props load in the background from the start, so the first map finds them ready.
if (graphics().models === 'synty') preloadSyntyProps();

/** Which voxel props stand on each kind of cover, per biome. */
const PROP_SETS: Record<Biome, { low: PropSet; high: PropSet }> = {
  city: { low: 'cityLow', high: 'cityHigh' },
  wilds: { low: 'wildsLow', high: 'wildsHigh' },
  facility: { low: 'facilityLow', high: 'facilityHigh' },
};

/**
 * The prop that draws a decor hint on cover of kind `tile` (procedural maps),
 * or null if the hint has none for it: something blown up from full to low
 * cover falls back to the biome's random props, unless it has a low version
 * (a fallen tree, a shelf knocked down to its lower levels).
 */
function decorSet(kind: DecorKind, tile: 'low' | 'high', biome: Biome): PropSet | null {
  const low = tile === 'low';
  switch (kind) {
    case 'carFront':
    case 'carRear':
    case 'planter':
    case 'bin':
    case 'streetTree':
    case 'fountain':
    case 'fountainCorner':
    case 'counter':
    case 'table':
    case 'sofa':
    case 'bed':
    case 'pipe':
    case 'bush':
      return low ? kind : null;
    case 'kiosk':
    case 'statue':
    case 'wardrobe':
    case 'cabinet':
    case 'rack':
    case 'tank':
    case 'pylon':
      return low ? null : kind;
    case 'fence':
      return low ? 'fencePost' : null;
    case 'gatePost':
      return low ? 'gatePost' : null;
    case 'hay':
      return low ? 'hayBale' : 'hayStack';
    case 'shelf':
      return low ? 'shelfLow' : 'shelfHigh';
    case 'desk':
      return low ? (biome === 'facility' ? 'labBench' : 'desk') : null;
    case 'crates':
      return low ? (biome === 'facility' ? 'techCrate' : 'crates') : 'containers';
    case 'barricade':
      return low ? (biome === 'facility' ? 'techBarrier' : biome === 'wilds' ? 'sandbags' : 'barricade') : null;
    case 'tree':
      return low ? 'logs' : biome === 'wilds' ? 'forest' : 'tree';
    case 'rock':
      return low ? 'rock' : 'spire';
    case 'log':
      return low ? 'logs' : null;
    case 'rubble':
      return low ? 'rubble' : 'wallStub';
  }
}

/** Hints whose prop turns the way the generator said; the others are turned at random, as before. */
const FACING = new Set<DecorKind>(['carFront', 'carRear', 'fountain', 'fountainCorner', 'fence', 'gatePost', 'hay', 'shelf', 'counter', 'table', 'sofa', 'bed', 'desk', 'wardrobe', 'cabinet', 'rack', 'pipe', 'barricade', 'log']);

/**
 * How far off the grid a prop may sit (second playtest: «demasiado
 * cuadriculados»): free-standing things are nudged and turned a few degrees,
 * trees, bushes, rocks and rubble any way at all. Rows (fences, shelves, a
 * car's halves) and furniture against walls stay as they are, and nothing
 * moves far enough to look like it stands on another tile.
 */
const WIGGLE: Partial<Record<DecorKind, 1 | 2>> = {
  planter: 1,
  bin: 1,
  streetTree: 1,
  kiosk: 1,
  statue: 1,
  crates: 1,
  tank: 1,
  pylon: 1,
  tree: 2,
  bush: 2,
  rock: 2,
  rubble: 2,
};

/** Quarter turns (counter-clockwise from above, as `placed` turns) that make a prop modelled facing north face DIRS4[f]. */
const turnTo = (f: number) => (4 - f) % 4;

/** Instanced meshes of one shape. */
class Layer {
  private matrices: THREE.Matrix4[] = [];
  private colors: THREE.Color[] = [];
  mesh: THREE.InstancedMesh | null = null;

  constructor(
    private readonly geometry: THREE.BufferGeometry,
    private readonly material: THREE.Material,
    private readonly shadows = true,
  ) {}

  add(matrix: THREE.Matrix4, color: number | THREE.Color): void {
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
 * (what the generator meant it to be on procedural maps: a parked car, a fence,
 * a shelf…; else crates, sandbags, kiosks, trees, server racks… at random).
 * The ground reads as continuous terrain with small details scattered over it
 * (ground.ts); no grid lines, those only come with the overlays.
 *
 * Fog of war (fogOfWar.ts): tiles never seen this mission lie under a dark
 * mist, tiles seen before keep a remembered look, and the squad's shared
 * sight (both players' soldiers) clears it as they move.
 */
export class MapView {
  readonly group = new THREE.Group();
  private readonly fog = new FogOfWar();
  private ground: GroundMap | null = null;
  private layers: Layer[] = [];
  private ladders = new THREE.Group();
  private visibility: boolean[] | null = null;
  private width = 0;
  private materials: THREE.Material[] = [];
  private geometries: THREE.BufferGeometry[] = [];
  /** What the squad has seen this mission (shared sight: both players' soldiers). */
  readonly explored = new ExploredMemory();
  /** The state shown: the game applies events to it in place, so its seq is current. */
  private shown: GameState | null = null;
  /** The next sight update is the first of a mission: no fade. */
  private fresh = true;
  /** Some Synty props were still loading at the last rebuild (voxel ones stood in): rebuild when they arrive. */
  private waitingForProps = false;

  build(s: GameState): void {
    this.width = s.width;
    this.explored.begin(s);
    this.shown = s;
    this.visibility = null;
    this.fresh = true;
    this.fog.resize(s.width, s.height);
    this.fog.set([], this.explored.tiles, true);
    this.ground?.dispose();
    this.ground = new GroundMap(s);
    this.rebuild(s);
  }

  /** Recreates every mesh from the state (called on build and whenever a tile changes). */
  rebuild(s: GameState): void {
    for (const layer of this.layers) layer.dispose();
    for (const m of this.materials) m.dispose();
    for (const g of this.geometries) g.dispose();
    this.layers = [];
    this.materials = [];
    this.geometries = [];
    this.group.clear();
    this.ladders = new THREE.Group();
    const ground = this.ground ?? (this.ground = new GroundMap(s));

    const pal = PALETTES[s.biome] ?? PALETTES.city;
    /** Every material of the map obeys the fog of war. */
    const own = <T extends THREE.Material>(m: T): T => {
      this.materials.push(this.fog.patch(m));
      return m;
    };
    const std = (opts: THREE.MeshStandardMaterialParameters = {}, jitter?: number) =>
      own(voxelSurface(new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.85, ...opts }), { storey: STOREY, jitter }));
    const layer = (geometry: THREE.BufferGeometry, material: THREE.Material, shadows = true) => {
      const l = new Layer(geometry, material, shadows);
      this.layers.push(l);
      return l;
    };
    // Floors: the ground shader paints their tops as continuous terrain; the colour per tile is for their sides.
    const groundMaterial = ground.material(voxelSurface(new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.95 }), { storey: STOREY, jitter: 0.1 }));
    const slabs = layer(new THREE.BoxGeometry(1, 0.1, 1), own(groundMaterial), false);
    // Raised buildings: their facades get a row of windows per storey.
    // In the wilds a raised tile is a hill: bare earth with grass on top, no windows.
    const hills = s.biome === 'wilds';
    const columnMaterial = own(voxelSurface(new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: hills ? 0.95 : 0.8 }), { storey: STOREY, windows: !hills }));
    const columns = layer(unitBox, columnMaterial);
    const walls = layer(unitBox, std({ roughness: 0.8 }));
    // Plinth and cornice: the bands that make a block read as a building wall.
    const trims = layer(unitBox, std({ roughness: 0.9 }));
    const lows = layer(unitBox, std({ roughness: 0.8 }));
    const doors = layer(unitBox, std({ roughness: 0.6 }));
    const frames = layer(unitBox, std({ roughness: 0.7, metalness: 0.3 }));
    const glassMaterial = own(new THREE.MeshStandardMaterial({ color: 0xffffff, transparent: true, opacity: 0.5, roughness: 0.1, metalness: 0.4, emissive: new THREE.Color(pal.glass), emissiveIntensity: 0.25 }));
    const glass = layer(unitBox, glassMaterial, false);

    // One instanced layer per prop variant (lit voxels + glowing voxels), created on first use.
    const propLayers = new Map<string, { lit: Layer | null; glow: Layer | null }>();
    // Synty props (Ajustes → Modelos): one instanced layer per mesh of each prop, on its pack's atlas.
    const synty = graphics().models === 'synty';
    const syntyLayers = new Map<string, Layer[]>();
    const atlasMaterials = new Map<AtlasId, THREE.Material>();
    const atlasMaterial = (id: AtlasId) => {
      let m = atlasMaterials.get(id);
      if (!m) atlasMaterials.set(id, (m = own(new THREE.MeshStandardMaterial({ map: atlasTexture(id), roughness: 0.85 }))));
      return m;
    };
    /** A Synty prop for the set, or false (none in the manifest, or still loading: the voxel one stands in meanwhile). */
    const addSynty = (set: PropSet, pick: number, matrix: THREE.Matrix4): boolean => {
      const ids = synty ? syntySet(set) : undefined;
      if (!ids?.length) return false;
      const id = ids[Math.min(ids.length - 1, Math.floor(pick * ids.length))]!;
      const prop = syntyProp(id);
      if (!prop) {
        this.waitingForProps = true;
        return false;
      }
      let layers = syntyLayers.get(id);
      if (!layers) syntyLayers.set(id, (layers = prop.map((part) => layer(part.geometry, atlasMaterial(part.atlas)))));
      prop.forEach((part, k) => layers[k]!.add(matrix.clone().multiply(part.matrix), 0xffffff));
      return true;
    };
    /** `wiggle` takes a prop off the grid: 1 a nudge and a few degrees, 2 any turn at all (see WIGGLE). */
    const addProp = (tile: number, set: PropSet, x: number, y: number, z: number, pick: number, turn: number, wiggle: 0 | 1 | 2 = 0) => {
      const tx = tile % s.width;
      const ty = Math.floor(tile / s.width);
      const shift = wiggle === 2 ? 0.07 : wiggle ? 0.035 : 0;
      const yaw = wiggle === 2 ? hash(tx, ty, 7) * Math.PI * 2 : turn * (Math.PI / 2) + (wiggle ? (hash(tx, ty, 6) - 0.5) * 0.16 : 0);
      const matrix = placed(x + (hash(tx, ty, 4) - 0.5) * 2 * shift, y, z + (hash(tx, ty, 5) - 0.5) * 2 * shift, 1, 1, 1, yaw);
      if (addSynty(set, pick, matrix)) return;
      const variant = Math.floor(pick * propVariants(set));
      const key = `${set}:${variant}`;
      let entry = propLayers.get(key);
      if (!entry) {
        const model = propModel(set, variant);
        const lit = own(new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.85, vertexColors: true }));
        const glow = own(new THREE.MeshBasicMaterial({ color: 0xffffff, vertexColors: true }));
        entry = { lit: model.lit ? layer(model.lit, lit) : null, glow: model.glow ? layer(model.glow, glow, false) : null };
        propLayers.set(key, entry);
      }
      entry.lit?.add(matrix, 0xffffff);
      entry.glow?.add(matrix, 0xffffff);
    };
    const props = PROP_SETS[s.biome] ?? PROP_SETS.city;
    /** The biome's random props: in the wilds, rocks, bushes and trees can turn any way. */
    const randomWiggle = s.biome === 'wilds' ? 2 : 1;

    const at = (x: number, y: number): TileKind => (x < 0 || y < 0 || x >= s.width || y >= s.height ? 'floor' : s.tiles[y * s.width + x]!);
    const decor = s.decor ?? [];
    /** A fence or gate post still standing: the fences beside it run their rails to it. */
    const fenceAt = (x: number, y: number) => {
      const kind = at(x, y) === 'low' ? decorOf(decor[y * s.width + x])?.kind : undefined;
      return kind === 'fence' || kind === 'gatePost';
    };
    const rails: PropSet = s.biome === 'city' ? 'fenceBoards' : 'fenceRails';
    /**
     * Draws the cover on tile i as its decor hint says, if it has one for what
     * the tile is now; false to fall back to the biome's random props.
     */
    const dressed = (i: number, x: number, y: number, kind: 'low' | 'high', base: number): boolean => {
      const look = decorOf(decor[i]);
      const set = look && decorSet(look.kind, kind, s.biome);
      if (!look || !set) return false;
      const cx = x + 0.5;
      const cz = y + 0.5;
      const turn = FACING.has(look.kind) ? turnTo(look.facing) : Math.floor(hash(x, y, 2) * 4);
      let pick = hash(x, y, 1);
      if (look.kind === 'carFront' || look.kind === 'carRear') {
        // Half a car is drawn only while the other half stands; both halves take the colour of the front one.
        const j = carPartner(decor, s.width, i);
        if (j < 0 || s.tiles[j] !== 'low') return false;
        const front = look.kind === 'carFront' ? i : j;
        pick = hash(front % s.width, Math.floor(front / s.width), 1);
        // A Synty car is one model over both tiles: the front tile draws it, centred between the two.
        const whole = synty ? syntySet('carFront') : undefined;
        if (whole?.every((id) => syntyProp(id))) {
          if (look.kind === 'carRear') return true;
          const d = DIRS4[look.facing]!;
          const at = placed(cx - d.x * 0.5, base, cz - d.y * 0.5, 1, 1, 1, turn * (Math.PI / 2));
          if (addSynty('carFront', pick, at)) return true;
        }
      }
      // A fallen tree is a row of logs: it keeps to the grid.
      addProp(i, set, cx, base, cz, pick, turn, look.kind === 'tree' && kind === 'low' ? 1 : (WIGGLE[look.kind] ?? 0));
      if (look.kind === 'fence' || look.kind === 'gatePost') {
        DIRS4.forEach((d, k) => {
          if (fenceAt(x + d.x, y + d.y)) addProp(i, rails, cx, base, cz, 0, turnTo(k));
        });
      }
      return true;
    };

    for (let y = 0; y < s.height; y++) {
      for (let x = 0; x < s.width; x++) {
        const i = y * s.width + x;
        const kind = s.tiles[i]!;
        const e = s.elev[i] ?? 0;
        const base = e * STOREY;
        const cx = x + 0.5;
        const cz = y + 0.5;
        const jitter = (hash(x, y) - 0.5) * 0.05;

        // Floor slab on top, and the solid block under a rooftop. Only the slab's sides take this colour (see ground.ts).
        const floor = e > 0 ? (hills ? pal.ground : pal.roof) : [pal.ground, pal.street, pal.floor][ground.kinds[i]!] ?? pal.ground;
        slabs.add(placed(cx, base - 0.05, cz, 1, 1, 1), floor);
        if (e > 0) columns.add(placed(cx, 0, cz, 1, base - 0.1, 1), new THREE.Color(hills ? pal.street : pal.facade).offsetHSL(0, 0, hills ? jitter - 0.08 : jitter));

        // Doors and windows run along the wall they belong to.
        const alongX = isWallLike(at(x - 1, y)) || isWallLike(at(x + 1, y));
        const yaw = alongX ? 0 : Math.PI / 2;
        const v = hash(x, y, 1);
        switch (kind) {
          case 'wall':
            walls.add(placed(cx, base, cz, 1, STOREY, 1), new THREE.Color(pal.wall).offsetHSL(0, 0, jitter));
            trims.add(placed(cx, base, cz, 1.03, 0.3, 1.03), new THREE.Color(pal.facade).offsetHSL(0, 0, -0.08));
            // The cornice stands a little proud of the wall's top (and of a rooftop beside it): flush, the two faces fought.
            trims.add(placed(cx, base + STOREY - 0.14, cz, 1.05, 0.17, 1.05), new THREE.Color(pal.facade).offsetHSL(0, 0, 0.05));
            break;
          case 'high':
            if (!dressed(i, x, y, kind, base)) addProp(i, props.high, cx, base, cz, v, Math.floor(hash(x, y, 2) * 4), randomWiggle);
            break;
          case 'low':
            // On a rooftop, low cover is the parapet along the edge (unless it is something else, crates on a platform).
            if (dressed(i, x, y, kind, base)) break;
            if (e > 0 && s.biome !== 'wilds') lows.add(placed(cx, base, cz, 1, 0.5, 1), new THREE.Color(pal.facade).offsetHSL(0, 0, 0.06));
            else addProp(i, props.low, cx, base, cz, v, Math.floor(hash(x, y, 2) * 4), randomWiggle);
            break;
          // No lintels: seen from the tactical camera, doors and windows must read as gaps in the wall.
          case 'door':
          case 'doorOpen': {
            const h = STOREY * 0.88;
            frames.add(placed(cx + (alongX ? -0.47 : 0), base, cz + (alongX ? 0 : -0.47), alongX ? 0.06 : 1, h, alongX ? 1 : 0.06), pal.frame);
            if (kind === 'door') doors.add(placed(cx, base, cz, 0.9, h, 0.1, yaw), pal.door);
            // An open door has swung 90° on its hinge, into the room.
            else if (alongX) doors.add(placed(cx - 0.42, base, cz + 0.45, 0.9, h, 0.1, Math.PI / 2), pal.door);
            else doors.add(placed(cx + 0.45, base, cz - 0.42, 0.9, h, 0.1, 0), pal.door);
            break;
          }
          case 'window':
          case 'windowBroken': {
            frames.add(placed(cx, base, cz, 1, 0.8, 1), pal.wall);
            trims.add(placed(cx, base, cz, 1.03, 0.3, 1.03), new THREE.Color(pal.facade).offsetHSL(0, 0, -0.08));
            if (kind === 'window') glass.add(placed(cx, base + 0.8, cz, 0.98, STOREY - 1.1, 0.14, yaw), pal.glass);
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
    if (this.waitingForProps) {
      this.waitingForProps = false;
      void syntyPropsLoaded().then(() => {
        if (this.shown === s) this.rebuild(s);
      });
    }
    const details = own(new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.9, vertexColors: true }));
    this.group.add(this.ladders, this.surroundings(s, pal), scatterDetails(s, ground, details));
  }

  /** Ground beyond the playable area; the fog of war turns it into the mist a few tiles out. */
  private surroundings(s: GameState, pal: Palette): THREE.Mesh {
    const material = this.fog.patch(voxelSurface(new THREE.MeshStandardMaterial({ color: new THREE.Color(pal.street).multiplyScalar(0.7), roughness: 0.95 })));
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
    const material = this.fog.patch(voxelSurface(new THREE.MeshStandardMaterial({ color: pal.ladder, roughness: 0.5, metalness: 0.4 })));
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

  /** What the squad sees now: it is remembered as explored, and the fog fades to match. */
  setVisibility(mask: boolean[]): void {
    this.visibility = mask;
    this.explored.see(mask, this.shown?.seq ?? 0);
    this.fog.set(mask, this.explored.tiles, this.fresh);
    this.fresh = false;
  }

  /** Advances the fog's fades and its drifting mist; called every frame. */
  tick(dt: number): void {
    this.fog.update(dt);
  }

  isVisible(x: number, y: number): boolean {
    return this.visibility?.[y * this.width + x] ?? true;
  }

  isExplored(x: number, y: number): boolean {
    return this.explored.tiles[y * this.width + x] === 1;
  }
}
