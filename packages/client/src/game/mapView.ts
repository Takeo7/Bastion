import * as THREE from 'three';
import { carPartner, decorOf, DIRS4, litMask, type Biome, type DecorKind, type GameState, type TileKind } from '@bastion/engine';
import { graphics } from '../settings';
import { StaticBatch, type Dirty, type UvRect } from './batch';
import { atlasRect, mapAtlasTexture } from './mapAtlas';
import { BuildingsView, piecesLoaded, preloadPieces, RAISED_FLATS, RAISED_PLATFORM, syntyPiece, type Focus } from './buildings';
import { STOREY } from './coords';
import { dressEdges, dressStreets } from './dressing';
import { NIGHT, NightLights, type Glow } from './night';
import { beforeNextFrame, shadowsChanged } from './renderer';
import { ExploredMemory } from './explored';
import { FogOfWar } from './fogOfWar';
import { GroundMap, scatterCover } from './ground';
import { contactPatch, GroundOcclusion } from './groundAO';
import { atlasTexture } from './synty';
import type { AtlasId, PropId, PropSet } from './syntyManifest';
import { preloadSyntyProps, syntyProp, syntyPropsLoaded, syntySet } from './syntyProps';

/**
 * The colours the ground shader leaves alone: the sides of the floor slabs
 * (open ground, street, indoor floor, rooftop) and the ground beyond the map.
 */
interface Palette {
  ground: number;
  street: number;
  floor: number;
  roof: number;
}

const PALETTES: Record<Biome, Palette> = {
  city: { ground: 0x9a9384, street: 0x585d63, floor: 0x8a705a, roof: 0x7d7a74 },
  wilds: { ground: 0x6a8a45, street: 0x9a8460, floor: 0x86705a, roof: 0x7a6a50 },
  facility: { ground: 0x6a7078, street: 0x4a4f56, floor: 0x7f8892, roof: 0x5a6068 },
};

// Synty props and building pieces load in the background from the start, so the first map finds them ready.
preloadSyntyProps();
preloadPieces();

/** How much ground cover (tufts, papers…) each level of Ajustes → Efectos scatters. */
const COVER_DENSITY = { high: 1, medium: 0.6, low: 0.3 } as const;

/** Which props stand on each kind of cover, per biome (when the generator gave no hint). */
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
      return low ? 'carFront' : null;
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
    case 'tank':
      // In town a low one is a filling station's pump.
      return low ? (biome === 'city' ? 'pump' : null) : kind;
    case 'pylon':
      // In town, a building site's portaloo.
      return low ? null : biome === 'city' ? 'portaloo' : kind;
    case 'kiosk':
    case 'statue':
    case 'wardrobe':
    case 'cabinet':
    case 'rack':
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

/**
 * Tiles a side of the chunks the map's static geometry is merged in (batch.ts):
 * a tile changing (a door, a wall blown up, furniture searched) merges its
 * chunk again, not the map. A material still draws once, over all its chunks.
 */
const CHUNK = 8;

/** What a map keeps from one rebuild to the next: the merged geometry, its materials, the floor slab, the ground beyond. */
interface Kit {
  batch: StaticBatch;
  slab: THREE.BufferGeometry;
  ground: THREE.MeshStandardMaterial;
  /** Props' materials: 'packed' for the packed texture's packs, else by pack. */
  atlas: Map<AtlasId | 'packed', THREE.Material>;
  surroundings: THREE.Mesh;
}

/** One kind of placed geometry (a prop's part, the floor slabs): its instances go to the map's batch. */
class Layer {
  constructor(
    private readonly batch: StaticBatch,
    private readonly geometry: THREE.BufferGeometry,
    private readonly material: THREE.Material,
    private readonly shadows = true,
    /** Its instances have colours of their own (the slabs' sides): the material reads them per vertex. */
    private readonly tinted = false,
    /** It darkens the ground round it (groundAO.ts). */
    private readonly occlude = shadows,
    /** Where its atlas sits in the packed texture (mapAtlas.ts), if it's packed. */
    private readonly uv: UvRect | null = null,
  ) {}

  add(matrix: THREE.Matrix4, color: number | THREE.Color = 0xffffff): void {
    const c = this.tinted ? new THREE.Color(color) : null;
    this.batch.add(this.geometry, this.material, matrix, this.shadows, c ? { color: [c.r, c.g, c.b] } : undefined, this.occlude, this.uv ?? undefined);
  }
}

/** Props that darken no ground: slim (poles, wires, a sign) or overhead (a tree's crown would stand in for the ground under it). */
const NO_OCCLUSION = new Set<PropId>(['streetLight', 'powerPole', 'powerLine', 'stopSign', 'tree', 'treePine', 'treeDead', 'streetTree']);

/** Matrix for a box of size (sx, sy, sz) standing at (x, y, z), turned `yaw` radians. */
function placed(x: number, y: number, z: number, sx: number, sy: number, sz: number, yaw = 0): THREE.Matrix4 {
  return new THREE.Matrix4().compose(
    new THREE.Vector3(x, y, z),
    new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), yaw),
    new THREE.Vector3(sx, sy, sz),
  );
}

/** The rectangle covering both (either may be null: nothing changed there). */
function union(a: Dirty | null, b: Dirty | null): Dirty | null {
  if (!a || !b) return a ?? b;
  return { x0: Math.min(a.x0, b.x0), y0: Math.min(a.y0, b.y0), x1: Math.max(a.x1, b.x1), y1: Math.max(a.y1, b.y1) };
}

/** Stable pseudo-random value in [0, 1) per tile, for variety without state. */
function hash(x: number, y: number, salt = 0): number {
  return ((((x + salt * 31) * 73856093) ^ (y * 19349663) ^ (salt * 83492791)) >>> 0) % 1000 / 1000;
}

const isWallLike = (k: TileKind) => k === 'wall' || k === 'door' || k === 'doorOpen' || k === 'window' || k === 'windowBroken';

/**
 * Terrain in Synty POLYGON: the ground as continuous terrain with small props
 * scattered over it (ground.ts), the buildings with their walls, doors,
 * windows, roofs, raised blocks and ladders (buildings.ts), and on each tile
 * of cover a Synty prop: what the generator meant it to be on procedural maps
 * (a parked car, a fence, a shelf…), else crates, barricades, trees… at
 * random. No grid lines: those only come with the overlays.
 *
 * Fog of war (fogOfWar.ts): tiles never seen this mission lie under a dark
 * mist, tiles seen before keep a remembered look, and the squad's shared
 * sight (both players' soldiers) clears it as they move.
 */
export class MapView {
  readonly group = new THREE.Group();
  private readonly fog = new FogOfWar();
  private ground: GroundMap | null = null;
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
  /** Some Synty props or pieces were still loading at the last rebuild (left out): rebuild when they arrive. */
  private waitingForProps = false;
  /** The buildings in Synty pieces, with their cutaway; made with the map (build). */
  private buildings: BuildingsView | null = null;
  /** The night's lamps, headlights and lit windows (night.ts); hidden by day. */
  private night: NightLights | null = null;
  /** Kept from one rebuild to the next (made by build). */
  private kit: Kit | null = null;
  /** The lights (ids) the lit tiles were last worked out for. */
  private lightsSeen = '';
  /** Where the night's lamps and headlights go, as of the last rebuild. */
  private nightPlan: { lamps: Glow[]; headlights: Glow[] } | null = null;
  /** The tiles and searches the last rebuild drew (what changed since is all a rebuild redoes); null: redo everything. */
  private drawn: { tiles: TileKind[]; searched: string } | null = null;
  /** The contact occlusion baked over the map (groundAO.ts), and which bake is the latest. */
  private ao: GroundOcclusion | null = null;
  /** Where the contact occlusion is to be baked again (the next frame does it). */
  private aoDirty: Dirty | null = null;
  /** How many tiles had been searched at the last rebuild: a search redraws the map (the furniture opens). */
  private searchedSeen = 0;
  /** The camera and what the player looks at, for the cutaway (game.ts). */
  private camera: THREE.Camera | null = null;
  private focus: (() => Focus) | null = null;

  build(s: GameState): void {
    this.width = s.width;
    this.explored.begin(s);
    this.shown = s;
    this.visibility = null;
    this.fresh = true;
    this.fog.resize(s.width, s.height);
    this.fog.set([], this.explored.tiles, true);
    this.fog.setFloors(s.elev);
    this.ground?.dispose();
    this.ground = new GroundMap(s);
    this.buildings?.dispose();
    this.ao?.dispose();
    this.ao = new GroundOcclusion(s.width, s.height);
    this.buildings = new BuildingsView(s, this.fog, this.ao);
    this.disposeKit();
    this.kit = this.newKit(s, this.ground, this.ao);
    this.aoDirty = null;
    this.drawn = null;
    // A wall tile's outer half is the street's: the ground shader paints it so, the building's floor covers the rest.
    const plan = this.buildings.plan;
    for (let i = 0; i < plan.owner.length; i++) if (plan.owner[i]! >= 0 && !plan.inside[i]) this.ground.outdoors(i);
    this.rebuild(s);
    // A new map: all of it (its texture starts empty, which would read as hidden everywhere).
    this.queueBake({ x0: 0, y0: 0, x1: s.width, y1: s.height });
    // A night mission (docs/ALTURA-Y-NOCHE.md): the night's look and its lights (the game sets the renderer's).
    this.setNight(!!s.mission.night);
  }

  /**
   * The camera and what the player is looking at (the selected soldier, the
   * cursor, the target), for the buildings' cutaway. Called once by the game.
   */
  watch(camera: THREE.Camera, focus: () => Focus): void {
    this.camera = camera;
    this.focus = focus;
  }

  /** Every material of the map obeys the fog of war; the map disposes of it with the map. */
  private own<T extends THREE.Material>(m: T): T {
    this.materials.push(this.fog.patch(m));
    return m;
  }

  private newKit(s: GameState, ground: GroundMap, ao: GroundOcclusion): Kit {
    const groundMaterial = ground.material();
    contactPatch(groundMaterial, ao);
    const slab = new THREE.BoxGeometry(1, 0.1, 1);
    this.geometries.push(slab);
    return { batch: new StaticBatch(CHUNK), slab, ground: this.own(groundMaterial), atlas: new Map(), surroundings: this.surroundings(s, PALETTES[s.biome] ?? PALETTES.city, ground) };
  }

  private disposeKit(): void {
    this.kit?.batch.dispose();
    for (const m of this.materials) m.dispose();
    for (const g of this.geometries) g.dispose();
    this.materials = [];
    this.geometries = [];
    this.kit = null;
  }

  /**
   * Places everything from the state again (on build and whenever a tile
   * changes); only what changed is merged again (batch.ts), and the contact
   * occlusion is baked again only there.
   */
  rebuild(s: GameState): void {
    this.group.clear();
    const ground = this.ground ?? (this.ground = new GroundMap(s));
    const ao = this.ao ?? (this.ao = new GroundOcclusion(s.width, s.height));
    const buildings = this.buildings ?? (this.buildings = new BuildingsView(s, this.fog, ao));
    const kit = this.kit ?? (this.kit = this.newKit(s, ground, ao));

    const pal = PALETTES[s.biome] ?? PALETTES.city;
    const batch = kit.batch;
    // Only round what changed since the last rebuild (a door, a blast, a search): the chunks it touches are placed
    // and merged again, the rest stays as it is. `area` marks the tiles to place (whatever lands in those chunks).
    const redo = this.changed(s, batch);
    const area = redo && this.areaOf(s, redo, batch.chunk);
    batch.begin(redo);
    const layer = (geometry: THREE.BufferGeometry, material: THREE.Material, shadows = true) => new Layer(batch, geometry, material, shadows);
    // Floors: the ground shader paints their tops as continuous terrain; the colour per tile is for their sides.
    const slabs = new Layer(batch, kit.slab, kit.ground, false, true, true);
    const hills = s.biome === 'wilds';

    // Synty props: their meshes, each on its pack's atlas.
    const syntyLayers = new Map<string, Layer[]>();
    /** The props' material: one for every pack in the packed texture (mapAtlas.ts: a draw for all of them), else the pack's own. */
    const atlasMaterial = (id: AtlasId) => {
      const key = atlasRect(id) ? 'packed' : id;
      let m = kit.atlas.get(key);
      if (!m) kit.atlas.set(key, (m = this.own(new THREE.MeshStandardMaterial({ map: key === 'packed' ? mapAtlasTexture() : atlasTexture(id), roughness: 0.85 }))));
      return m;
    };
    /** A layer for one part of a prop, its UVs moved into its atlas's place. */
    const partLayer = (part: { geometry: THREE.BufferGeometry; atlas: AtlasId }, shadows = true, occlude = shadows) =>
      new Layer(batch, part.geometry, atlasMaterial(part.atlas), shadows, false, occlude, atlasRect(part.atlas));
    /** The Synty prop `id` at `matrix`; false while it loads. */
    const addSyntyId = (id: PropId, matrix: THREE.Matrix4, shadows = true): boolean => {
      const prop = syntyProp(id);
      if (!prop) {
        this.waitingForProps = true;
        return false;
      }
      const key = shadows ? id : `${id}:flat`;
      let layers = syntyLayers.get(key);
      const occlude = shadows && !NO_OCCLUSION.has(id);
      if (!layers) syntyLayers.set(key, (layers = prop.map((part) => partLayer(part, shadows, occlude))));
      prop.forEach((part, k) => layers[k]!.add(matrix.clone().multiply(part.matrix), 0xffffff));
      return true;
    };
    /** One of the set's Synty props (`pick` in [0, 1) chooses), at `matrix`; false while it loads. */
    const addSynty = (set: PropSet, pick: number, matrix: THREE.Matrix4): boolean => {
      const ids = syntySet(set);
      return addSyntyId(ids[Math.min(ids.length - 1, Math.floor(pick * ids.length))]!, matrix);
    };
    const searched = new Set(s.searched ?? []);
    /** Some wrecks still have a battery: their headlights on, by night. */
    const headlights: Glow[] = [];
    // The list's own length (tick compares it): a tile listed twice must not redraw the map every frame.
    this.searchedSeen = s.searched?.length ?? 0;
    /**
     * A wardrobe someone has searched: its doors swung wide and its drawers
     * pulled out (Town's wardrobe, its parts apart); false while it loads.
     */
    const openWardrobe = (x: number, base: number, z: number, front: { x: number; y: number }): boolean => {
      const parts = syntyPiece('wardrobeOpen');
      if (!parts) {
        this.waitingForProps = true;
        return false;
      }
      const at = new THREE.Matrix4().compose(new THREE.Vector3(x, base, z), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), Math.atan2(front.x, front.y)), new THREE.Vector3(1, 1, 1));
      parts.forEach((part, k) => {
        const box = (part.geometry.boundingBox ?? (part.geometry.computeBoundingBox(), part.geometry.boundingBox!)).clone().applyMatrix4(part.matrix);
        let local = part.matrix;
        const swing = (hx: number, angle: number) =>
          new THREE.Matrix4()
            .makeTranslation(hx, 0, box.max.z)
            .multiply(new THREE.Matrix4().makeRotationY(angle))
            .multiply(new THREE.Matrix4().makeTranslation(-hx, 0, -box.max.z))
            .multiply(part.matrix);
        if (part.name.endsWith('Door_01')) local = swing(box.min.x, -1.9);
        else if (part.name.endsWith('Door_02')) local = swing(box.max.x, 1.9);
        else if (part.name.includes('Drawer')) local = new THREE.Matrix4().makeTranslation(0, 0, 0.1 + hash(k, Math.round(x * 7 + z), 3) * 0.18).multiply(part.matrix);
        const key = `wardrobeOpen:${part.name}`;
        let layers = syntyLayers.get(key);
        if (!layers) syntyLayers.set(key, (layers = [partLayer(part)]));
        layers[0]!.add(at.clone().multiply(local), 0xffffff);
      });
      return true;
    };
    /**
     * What a search leaves in front of a piece of furniture (`front` the way
     * it faces): drawers pulled out and dropped, papers, clothes, opened boxes.
     */
    const ransack = (kind: DecorKind, x: number, y: number, base: number, front: { x: number; y: number }) => {
      const r = (k: number) => hash(x, y, 60 + k);
      const spot = (along: number, out: number) => {
        const side = { x: -front.y, y: front.x };
        return new THREE.Vector3(x + 0.5 + front.x * out + side.x * along, base + 0.003, y + 0.5 + front.y * out + side.y * along);
      };
      const drop = (id: PropId, along: number, out: number, k: number, shadows = false) =>
        addSyntyId(id, new THREE.Matrix4().compose(spot(along, out), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), r(k) * Math.PI * 2), new THREE.Vector3(1, 1, 1)), shadows);
      switch (kind) {
        case 'desk':
        case 'cabinet':
        case 'counter':
          drop('drawer', (r(1) - 0.5) * 0.4, 0.55, 2, true);
          drop('papers', (r(3) - 0.5) * 0.5, 0.6, 4);
          break;
        case 'wardrobe':
        case 'bed':
          drop('laundry', (r(1) - 0.5) * 0.4, 0.55, 2);
          if (kind === 'bed') drop('papers', (r(3) - 0.5) * 0.5, 0.5, 4);
          break;
        case 'shelf':
        case 'rack':
          drop('cardboard', (r(1) - 0.5) * 0.4, 0.55, 2, true);
          drop('papers', (r(3) - 0.5) * 0.5, 0.6, 4);
          break;
        case 'crates':
          drop('crateLid', (r(1) - 0.5) * 0.3, 0.62, 2);
          break;
        case 'table':
          drop('papers', (r(3) - 0.5) * 0.5, 0.5, 4);
          break;
        default:
          break;
      }
    };
    /** `wiggle` takes a prop off the grid: 1 a nudge and a few degrees, 2 any turn at all (see WIGGLE). */
    const addProp = (tile: number, set: PropSet, x: number, y: number, z: number, pick: number, turn: number, wiggle: 0 | 1 | 2 = 0) => {
      const tx = tile % s.width;
      const ty = Math.floor(tile / s.width);
      const shift = wiggle === 2 ? 0.07 : wiggle ? 0.035 : 0;
      const yaw = wiggle === 2 ? hash(tx, ty, 7) * Math.PI * 2 : turn * (Math.PI / 2) + (wiggle ? (hash(tx, ty, 6) - 0.5) * 0.16 : 0);
      addSynty(set, pick, placed(x + (hash(tx, ty, 4) - 0.5) * 2 * shift, y, z + (hash(tx, ty, 5) - 0.5) * 2 * shift, 1, 1, 1, yaw));
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
        // A car is one model over both tiles, drawn only while both stand: the front tile draws it, centred between the two.
        const j = carPartner(decor, s.width, i);
        if (j < 0 || s.tiles[j] !== 'low') return false;
        if (look.kind === 'carRear') return true;
        pick = hash(x, y, 1);
        const d = DIRS4[look.facing]!;
        const at = placed(cx - d.x * 0.5, base, cz - d.y * 0.5, 1, 1, 1, turn * (Math.PI / 2));
        // Searched (its boot or its glovebox), it's the one with the boot lid up.
        if (searched.has(i) || searched.has(j)) addSyntyId('carOpen', at);
        else addSynty('carFront', pick, at);
        if (hash(x, y, 9) < 0.25) headlights.push({ x: cx + d.x * 0.42, y: base + 0.32, z: cz + d.y * 0.42, dx: d.x, dz: d.y });
        return true;
      }
      // Searched furniture: opened, its contents on the floor in front of it.
      if (searched.has(i)) {
        const front = FACING.has(look.kind) ? DIRS4[look.facing]! : DIRS4[Math.floor(hash(x, y, 2) * 4)]!;
        const opened =
          look.kind === 'wardrobe' && kind === 'high'
            ? openWardrobe(cx, base, cz, front)
            : look.kind === 'crates' && kind === 'low'
              ? addSyntyId('crateOpen', placed(cx, base, cz, 1, 1, 1, turn * (Math.PI / 2)))
              : false;
        ransack(look.kind, x, y, base, front);
        if (opened) return true;
      }
      // A fallen tree is a row of logs: it keeps to the grid.
      addProp(i, set, cx, base, cz, pick, turn, look.kind === 'tree' && kind === 'low' ? 1 : (WIGGLE[look.kind] ?? 0));
      // A chair at each desk, pushed back a little (cosmetic: it stands on the floor in front).
      if (look.kind === 'desk' && kind === 'low') {
        const f = DIRS4[look.facing]!;
        const yaw = Math.atan2(f.x, f.y) + Math.PI + (hash(x, y, 8) - 0.5) * 0.6;
        addSyntyId('officeChair', placed(cx + f.x * 0.62, base, cz + f.y * 0.62, 1, 1, 1, yaw));
      }
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
        if (area && !area[i]) continue;
        const kind = s.tiles[i]!;
        const e = s.elev[i] ?? 0;
        const base = e * STOREY;
        const cx = x + 0.5;
        const cz = y + 0.5;

        // Floor slab on top. Only its sides take this colour (see ground.ts). buildings.ts draws every
        // wall, the floors inside, and raised ground's sides (flats, platforms, hills) and tops (but a hill's, which is ground).
        const floor = e > 0 ? (hills ? pal.ground : pal.roof) : [pal.ground, pal.street, pal.floor][ground.kinds[i]!] ?? pal.ground;
        const raised = buildings.plan.raised[i] ?? 0;
        const covered = !!buildings.plan.floor[i] || raised === RAISED_FLATS || raised === RAISED_PLATFORM;
        if (!covered) slabs.add(placed(cx, base - 0.05, cz, 1, 1, 1), floor);
        if (isWallLike(kind)) continue;

        const v = hash(x, y, 1);
        switch (kind) {
          case 'high':
            if (!dressed(i, x, y, kind, base)) addProp(i, props.high, cx, base, cz, v, Math.floor(hash(x, y, 2) * 4), randomWiggle);
            break;
          case 'low':
            // On a block of flats, low cover is the parapet along the roof's edge (buildings.ts), unless it is something else.
            if (dressed(i, x, y, kind, base)) break;
            if (raised === RAISED_FLATS) break;
            addProp(i, props.low, cx, base, cz, v, Math.floor(hash(x, y, 2) * 4), randomWiggle);
            break;
          case 'console': {
            // Its back to the wall beside it.
            const alongX = isWallLike(at(x - 1, y)) || isWallLike(at(x + 1, y));
            addProp(i, 'console', cx, base, cz, 0, alongX ? 0 : 1);
            break;
          }
          default:
            break;
        }
      }
    }

    // The streets' dressing (street lights, poles and wires, bags, weeds…): cosmetic, merged with the rest.
    const lamps: Glow[] = [];
    // The map's own lights (generated maps): a street light on each.
    const fixed = s.lights ? s.lights.filter((l) => l.turns === undefined).map((l) => l.tile) : null;
    for (const d of dressStreets(s, ground, COVER_DENSITY[graphics().effects], fixed)) {
      addSyntyId(d.id, d.matrix, d.cast);
      if (d.id !== 'streetLight') continue;
      // The lamp hangs at the end of the arm (its +x), over the road.
      const e = d.matrix.elements;
      lamps.push({ x: e[12]! + e[0]! * 1.45, y: 4.05, z: e[14]! + e[2]! * 1.45, dx: e[0]!, dz: e[2]! });
    }

    // Beyond the edge: wrecks on the streets that leave the map (off the baked occlusion: they darken no ground).
    for (const d of dressEdges(s, ground)) {
      const prop = syntyProp(d.id);
      if (!prop) {
        this.waitingForProps = true;
        continue;
      }
      let layers = syntyLayers.get(`edge:${d.id}`);
      if (!layers) syntyLayers.set(`edge:${d.id}`, (layers = prop.map((part) => partLayer(part, true, false))));
      prop.forEach((part, k) => layers[k]!.add(d.matrix.clone().multiply(part.matrix)));
    }

    // Ground cover (tufts, papers, leaves, drains…), scattered off the grid; no shadows.
    const coverLayers = new Map<string, Layer[]>();
    for (const { id, matrix } of scatterCover(s, ground, COVER_DENSITY[graphics().effects])) {
      const prop = syntyProp(id);
      if (!prop) {
        this.waitingForProps = true;
        continue;
      }
      let layers = coverLayers.get(id);
      if (!layers) coverLayers.set(id, (layers = prop.map((part) => partLayer(part, false))));
      prop.forEach((part, k) => layers[k]!.add(matrix.clone().multiply(part.matrix), 0xffffff));
    }

    let dirty = batch.commit();
    this.group.add(batch.group);
    if (!buildings.buildWalls(s, redo, area)) this.waitingForProps = true;
    dirty = union(dirty, buildings.dirty);
    this.group.add(buildings.group, buildings.walls);
    // What's drawn now, for the next rebuild to compare with; with props still loading, the next one redoes everything.
    this.drawn = { tiles: s.tiles.slice(), searched: (s.searched ?? []).join(',') };
    if (this.waitingForProps) {
      this.waitingForProps = false;
      this.drawn = null;
      void Promise.all([syntyPropsLoaded(), piecesLoaded()]).then(() => {
        if (this.shown === s) this.rebuild(s);
      });
    }
    this.group.add(kit.surroundings);
    // The night's lights are made when it's night (by day a rebuild needn't pay for them); a partial rebuild keeps them.
    if (!redo) {
      this.night?.dispose();
      this.night = null;
      this.nightPlan = { lamps, headlights };
      if (NIGHT.value > 0.5) this.lightNight();
    } else if (this.night) this.group.add(this.night.group);
    // What the lights reach may have changed (a door, a wall): the lit tiles again, and the night's lights if remade.
    if (NIGHT.value > 0.5) this.lightsChanged(s);
    if (!dirty) return;
    shadowsChanged();
    this.queueBake(dirty);
  }

  /**
   * The chunks to place again: those within two tiles of a tile that changed
   * or was searched since the last rebuild (what stands there may reach into
   * a neighbour: a fence's rails, a car's other half, a wall's corner post).
   * Null: everything (a new map, props that were loading).
   */
  private changed(s: GameState, batch: StaticBatch): Set<string> | null {
    const drawn = this.drawn;
    if (!drawn || drawn.tiles.length !== s.tiles.length) return null;
    const touched: number[] = [];
    for (let i = 0; i < s.tiles.length; i++) if (s.tiles[i] !== drawn.tiles[i]) touched.push(i);
    const searched = s.searched ?? [];
    if (searched.join(',') !== drawn.searched) {
      const before = new Set(drawn.searched ? drawn.searched.split(',').map(Number) : []);
      for (const i of searched) if (!before.has(i)) touched.push(i);
    }
    const chunks = new Set<string>();
    for (const i of touched) {
      const x = i % s.width;
      const y = (i - x) / s.width;
      for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) chunks.add(batch.chunkAt(x + dx + 0.5, y + dy + 0.5));
    }
    return chunks;
  }

  /** The tiles whose things may land in `chunks`: theirs, and a tile round them. */
  private areaOf(s: GameState, chunks: Set<string>, size: number): Uint8Array {
    const area = new Uint8Array(s.width * s.height);
    for (const key of chunks) {
      const [cx, cz] = key.split(',').map(Number) as [number, number];
      for (let y = Math.max(0, cz * size - 1); y < Math.min(s.height, (cz + 1) * size + 1); y++) {
        for (let x = Math.max(0, cx * size - 1); x < Math.min(s.width, (cx + 1) * size + 1); x++) area[y * s.width + x] = 1;
      }
    }
    return area;
  }

  /** The contact occlusion baked again in `region` before the next frame (one bake for all the rebuilds till then). */
  private queueBake(region: Dirty): void {
    const { ao, kit, buildings } = this;
    if (!ao || !kit || !buildings) return;
    const pending = this.aoDirty;
    this.aoDirty = union(this.aoDirty, region);
    if (pending) return;
    beforeNextFrame((gl) => {
      // Another map since: its own bake is queued.
      if (this.ao !== ao || this.kit !== kit) return;
      const todo = this.aoDirty;
      this.aoDirty = null;
      if (todo) ao.bake(gl, [...kit.batch.occluderGeometries(), ...buildings.occluders()], todo);
    });
  }

  /**
   * Ground beyond the playable area, painted by the ground shader as the
   * edge tiles go on (streets carry on out of the map); the fog of war turns
   * it into the haze a few tiles out.
   */
  private surroundings(s: GameState, pal: Palette, map?: GroundMap): THREE.Mesh {
    const material = this.fog.patch(map ? map.material() : new THREE.MeshStandardMaterial({ color: new THREE.Color(pal.street).multiplyScalar(0.7), roughness: 0.95 }));
    material.vertexColors = false;
    this.materials.push(material);
    const margin = 160;
    const geometry = new THREE.PlaneGeometry(s.width + margin, s.height + margin).rotateX(-Math.PI / 2);
    this.geometries.push(geometry);
    const ground = new THREE.Mesh(geometry, material);
    // Just under the floor slabs: hidden by them on the map, the ground itself beyond it.
    ground.position.set(s.width / 2, -0.01, s.height / 2);
    ground.receiveShadow = true;
    ground.matrixAutoUpdate = false;
    ground.updateMatrix();
    return ground;
  }

  /** Day or night: the fog's tints, the street lights' heads, the real lights, the lit tiles. */
  setNight(on: boolean): void {
    NIGHT.value = on ? 1 : 0;
    this.fog.setNight(on);
    if (on && !this.night) this.lightNight();
    this.night?.setVisible(on);
    this.lightsSeen = '';
    if (this.shown) this.lightsChanged(this.shown);
  }

  private lightNight(): void {
    if (!this.nightPlan) return;
    this.night = new NightLights(this.nightPlan.lamps, this.nightPlan.headlights, this.fog);
    this.group.add(this.night.group);
  }

  /**
   * The engine's lights changed (a Bengala, a fire, one gone out) or what
   * they reach did (a rebuild: a door opened): the lit tiles are the
   * engine's lit mask (its light stops at walls), and the night's Bengalas
   * and fires follow the state.
   */
  private lightsChanged(s: GameState): void {
    const night = NIGHT.value > 0.5;
    this.fog.setLit(night ? litMask(s) : []);
    this.night?.setBurning(s.lights ?? [], (x, y) => (s.elev[y * s.width + x] ?? 0) * STOREY);
  }

  /** What the squad sees now: it is remembered as explored, and the fog fades to match. */
  setVisibility(mask: boolean[]): void {
    this.visibility = mask;
    this.explored.see(mask, this.shown?.seq ?? 0);
    this.fog.set(mask, this.explored.tiles, this.fresh);
    this.fresh = false;
  }

  /** Advances the fog's fades and its drifting mist, and the buildings' cutaway; called every frame. */
  tick(dt: number): void {
    this.fog.update(dt);
    // A search opened a piece of furniture: redraw (searches are few, a rebuild is cheap).
    if (this.shown && (this.shown.searched?.length ?? 0) !== this.searchedSeen) this.rebuild(this.shown);
    // Lights on and off (lightOn, lightOff: the game applies them to the state it shows).
    if (this.shown && NIGHT.value > 0.5) {
      const lights = this.shown.lights ?? [];
      let key = '';
      for (const l of lights) key += `${l.id},`;
      if (key !== this.lightsSeen) {
        this.lightsSeen = key;
        this.lightsChanged(this.shown);
      }
    }
    this.night?.update(dt, this.camera);
    if (this.buildings && this.shown) this.buildings.update(dt, this.shown, this.camera, this.focus?.() ?? null, this.visibility);
  }

  isVisible(x: number, y: number): boolean {
    return this.visibility?.[y * this.width + x] ?? true;
  }

  isExplored(x: number, y: number): boolean {
    return this.explored.tiles[y * this.width + x] === 1;
  }
}
