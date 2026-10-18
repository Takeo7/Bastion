import * as THREE from 'three';
import { kitModel } from '../strategy/baseKit';
import { carPartner, decorOf, DIRS4, floorLevels, litMask, type Biome, type DecorKind, type GameState, type Place, type Site, type TileKind } from '@bastion/engine';
import { graphics } from '../settings';
import { StaticBatch, type Dirty, type UvRect } from './batch';
import { atlasRect, mapAtlasTexture } from './mapAtlas';
import { BuildingsView, floorFadePatch, piecesLoaded, preloadPieces, RAISED_FLATS, RAISED_PLATFORM, RAISED_TOWER, syntyPiece, type Focus } from './buildings';
import { STOREY } from './coords';
import { dressEdges, dressStreets } from './dressing';
import { NIGHT, NightLights, type Glow } from './night';
import { unitPosNow } from './unitViews';
import { beforeNextFrame, shadowsChanged } from './renderer';
import { ExploredMemory } from './explored';
import { FogOfWar } from './fogOfWar';
import { GroundMap, scatterCover } from './ground';
import { inwards, madeAs, towersOf, wearOf, type Wear } from './siegeDressing';
import { DRAWN, drawPiece, pieceKey, pieceMap, pieceWear, terrainOf, type PieceMap } from './basePieces';
import { SnowCover, snowyMission, snowTexture } from './snow';
import { DroppedView } from './dropped';
import { LOOT_KINDS, LootMarks, type LootKind, type LootSpot } from './lootMarks';
import { EvacBus } from './evacBus';
import { medicalDetails, medicalWallPatch } from './medicalDetails';
import { medicalFurniture } from './medicalFurniture';
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
function decorSet(kind: DecorKind, tile: 'low' | 'high', biome: Biome, site?: Site): PropSet | null {
  const low = tile === 'low';
  switch (kind) {
    case 'carFront':
    case 'carRear':
      return low ? 'carFront' : null;
    case 'planter':
    case 'bin':
    case 'streetTree':
    case 'bench':
    case 'stove':
    case 'bathtub':
    case 'washbasin':
    case 'wc':
    case 'tvStand':
    case 'counter':
    case 'table':
    case 'sofa':
    case 'bed':
    case 'hospitalBed':
    case 'gurney':
    case 'pipe':
    case 'bush':
      return low ? kind : null;
    case 'tank':
      // In town and at the forest stop a low one is a filling station's pump.
      return low ? (biome === 'city' || site === 'roadside' ? 'pump' : null) : kind;
    case 'pylon':
      // In town, a building site's portaloo.
      return low ? null : biome === 'city' ? 'portaloo' : kind;
    case 'fountain':
    case 'fountainCorner':
      // Its basin is drawn whole (dressed); what is left of one shot up, rubble.
      return low ? 'rubble' : null;
    case 'kiosk':
    case 'statue':
    case 'fridge':
    case 'wardrobe':
    case 'cabinet':
    case 'medicalShelf':
    case 'rack':
      return low ? null : kind;
    case 'fence':
      // By the forest road, broken boards (no rails).
      return low ? (site === 'roadside' ? 'woodFence' : 'fencePost') : null;
    case 'gatePost':
      return low ? 'gatePost' : null;
    case 'hay':
      return low ? 'hayBale' : 'hayStack';
    case 'shelf':
      return low ? 'shelfLow' : 'shelfHigh';
    case 'desk':
      return low ? (biome === 'facility' ? 'labBench' : 'desk') : null;
    case 'crates':
      // High ones are stacked (dressed).
      return low ? (biome === 'facility' ? 'techCrate' : 'crates') : 'crates';
    case 'barricade':
      return low ? (biome === 'facility' ? 'techBarrier' : biome === 'wilds' ? 'sandbags' : 'barricade') : null;
    case 'tree':
      return low ? 'logs' : site === 'roadside' ? 'pines' : biome === 'wilds' ? 'forest' : 'tree';
    case 'rock':
      return low ? 'rock' : 'spire';
    case 'log':
      return low ? 'logs' : null;
    case 'rubble':
      return low ? 'rubble' : 'wallStub';
    case 'breach':
      // A wall blown down (docs/FASE7.md §4): drawn by the walls and rubbleOf (rebuild), its rubble here only as a fallback.
      return low ? 'rubble' : null;
    case 'bus':
      // The bus by the evac zone (docs/PRUEBA-2026-10-14.md P1): drawn whole by Arte, never as props.
      return null;
    case 'lootWeapons':
    case 'lootMedical':
    case 'lootAmmo':
    case 'lootFood':
      // The special loot boxes (docs/FASE8.md §B): each its own (mapView lootBox), with its mark (lootMarks.ts).
      return kind;
    case 'stairs':
      // A building's stairs are a ladder tile, never cover (docs/FASE6-PLAN.md).
      return null;
    case 'medicalCabinet':
    case 'medicalCart':
    case 'receptionDesk':
    case 'waitingSeats':
    case 'examTable':
    case 'medicalMonitor':
    case 'xray':
    case 'bedsideTable':
    case 'visitorChair':
    case 'clinicalSink':
    case 'sterilizer':
    case 'ultrasound':
    case 'waitingTable':
    case 'waterDispenser':
    case 'toilet':
    case 'clinicalBin':
    case 'consultDesk':
    case 'wheelchair':
      // Own clinical furniture is drawn directly, outside Synty's prop sets.
      return null;
    case 'truck':
    case 'waterTower':
    case 'container':
      // Drawn whole over their block while it stands (block); what a blast leaves of one is rubble.
      return low ? 'rubble' : null;
    case 'logPile':
      return low ? 'logPile' : null;
    case 'sign':
      return low ? null : 'roadSign';
    case 'wire':
      // A fallen chain-link fence by the road; razor wire at an industrial gate.
      return low ? (biome === 'city' ? 'razorWire' : 'chainFence') : null;
    // The factory (playtest 15/10, #14 and #17). Pallets are stacked by `dressed`, higher for high cover.
    case 'pallets':
      return 'pallets';
    case 'drums':
      return low ? 'drums' : null;
    case 'forklift':
      // No forklift in the packs: the generator puts pallets and drums where one would stand.
      return null;
    case 'machine':
      return low ? null : 'machine';
    case 'sirenCar':
      // Under the siren's car (syntyFigures.ts SirenRig draws it): nothing of its own, rubble if blown down.
      return low ? 'rubble' : null;
    case 'silo':
      // Drawn whole over its 2×2 block (block); what a blast leaves of one is rubble.
      return low ? 'rubble' : null;
  }
}

/** Hints whose prop turns the way the generator said; the others are turned at random, as before. */
const MEDICAL_FLAT = new Set(['bloodPool', 'bloodDrops', 'floorGrime', 'scatteredPapers']);
const MEDICAL_SCATTER = new Set([...MEDICAL_FLAT, 'medicalWaste', 'overturnedChair', 'fallenIV', 'spilledSupplies', 'rumpledLinen']);
const FACING = new Set<DecorKind>(['carFront', 'carRear', 'fountain', 'fountainCorner', 'fence', 'gatePost', 'hay', 'shelf', 'counter', 'table', 'sofa', 'bed', 'hospitalBed', 'medicalShelf', 'gurney', 'medicalCart', 'medicalCabinet', 'wheelchair', 'receptionDesk', 'waitingSeats', 'examTable', 'medicalMonitor', 'xray', 'bedsideTable', 'visitorChair', 'clinicalSink', 'sterilizer', 'ultrasound', 'waitingTable', 'waterDispenser', 'toilet', 'clinicalBin', 'consultDesk', 'desk', 'wardrobe', 'cabinet', 'rack', 'pipe', 'barricade', 'log', 'sign', 'wire', 'pallets', 'drums', 'machine', 'bench', 'fridge', 'stove', 'bathtub', 'washbasin', 'wc', 'tvStand']);

/**
 * The scenarios' things that stand over several tiles (#25), and what each of
 * their tiles is: a lorry (2×4), a container (2×5), a water tower (2×2, its
 * legs), a factory's silo (2×2), a log pile (1×2 or 2×2); and a square's park
 * bench (1×2). The generator never lets two touch, so a block is the tiles of
 * its kind joined to each other.
 */
const BLOCKS: Partial<Record<DecorKind, TileKind>> = { truck: 'high', container: 'high', waterTower: 'high', silo: 'high', logPile: 'low', bench: 'low' };

/** A square's fountain (playtest 18/10, #12), one block of these: its rim, and the statue in the middle of a big one. */
const FOUNTAIN: Partial<Record<DecorKind, TileKind>> = { fountain: 'low', fountainCorner: 'low', statue: 'high' };
/** The kind a block is joined by: a fountain's pieces are one. */
const blockKind = (kind: DecorKind | undefined): DecorKind | undefined => (kind && FOUNTAIN[kind] ? 'fountain' : kind);

/** A block: its tiles' bounds, the way it faces, whether all of it still stands, and the tile that draws it (under its middle). */
interface Block {
  x0: number;
  y0: number;
  w: number;
  h: number;
  facing: number;
  whole: boolean;
  anchor: number;
}

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
  snowbank: THREE.MeshStandardMaterial | null;
  clinical: THREE.MeshStandardMaterial | null;
  clinicalWall: THREE.MeshStandardMaterial | null;
  clinicalDirt: THREE.MeshStandardMaterial | null;
}

/** One kind of placed geometry (a prop's part, the floor slabs): its instances go to the map's batch. */
/** Where the props being placed stand: a building (-1 none) and its storey. */
interface FloorTag {
  building: number;
  level: number;
}

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
    /** The building and storey what's added now stands on (aFloor, buildings.ts: floorFadePatch), read as each is added. */
    private readonly floor: FloorTag | null = null,
  ) {}

  add(matrix: THREE.Matrix4, color: number | THREE.Color = 0xffffff): void {
    const c = this.tinted ? new THREE.Color(color) : null;
    const consts: Record<string, number[]> | undefined = c ? { color: [c.r, c.g, c.b] } : this.floor ? { aFloor: [this.floor.building + 1, this.floor.level] } : undefined;
    this.batch.add(this.geometry, this.material, matrix, this.shadows, consts, this.occlude, this.uv ?? undefined);
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
  private snow: SnowCover | null = null;
  private snowReady = false;
  private visibility: boolean[] | null = null;
  /** What the squad sees as the engine has it (in a siege, visibility is all of it; the cutaway goes by this). */
  private sight: boolean[] | null = null;
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
  private drawn: { tiles: TileKind[]; levels: (TileKind | null)[][]; searched: string; wear: Map<number, Wear>; pieces: Map<string, { key: string; tiles: number[] }> } | null = null;
  /** A siege's doors' and barricades' wear as last drawn (siegeDressing.ts), as a key. */
  private wearSeen = '';
  /** The contact occlusion baked over the map (groundAO.ts), and which bake is the latest. */
  private ao: GroundOcclusion | null = null;
  /** Where the contact occlusion is to be baked again (the next frame does it). */
  private aoDirty: Dirty | null = null;
  /** How many tiles had been searched at the last rebuild: a search redraws the map (the furniture opens). */
  private searchedSeen = 0;
  /** The special loot boxes as last drawn, by place (a partial rebuild finds again only those in its area), and their marks. */
  private readonly loot = new Map<number, LootSpot>();
  readonly lootMarks = new LootMarks();
  /** What the squad left on the floor (GameState.dropped). */
  private readonly dropped = new DroppedView();
  /** The canopies over the filling stations' pumps, the ground they roof over, and how much of each shows (canopies). */
  private readonly canopyGroup = new THREE.Group();
  private canopies: { group: THREE.Group; material: THREE.MeshStandardMaterial; x0: number; z0: number; x1: number; z1: number; shown: number }[] = [];
  private canopyMaterials: THREE.MeshStandardMaterial[] = [];
  /** La Mula by the evacuation (evacBus.ts), on maps whose mission has its bay (mission.evacBus). */
  private bus: EvacBus | null = null;
  /** How far La Mula drives to be gone (placeBus); null without one. */
  private busWay: number | null = null;
  /** The camera and what the player looks at, for the cutaway (game.ts). */
  private camera: THREE.Camera | null = null;
  private focus: (() => Focus) | null = null;
  /** The storey the fog shows (the active one when it was last drawn). */
  private fogFloor = 0;
  /** The new base's pieces (basePieces.ts), on its siege's map and on its own screen; null on any other. */
  private pieces: PieceMap | null = null;
  /** The map as buildings.ts and ground.ts see it: without the pieces basePieces draws (terrainOf), as of the last rebuild. */
  private terrain: GameState | null = null;

  build(s: GameState): void {
    this.width = s.width;
    this.canopyMaterials = [];
    this.explored.begin(s);
    this.shown = s;
    this.visibility = null;
    this.fresh = true;
    this.fog.resize(s.width, s.height);
    this.fogFloor = 0;
    this.fog.set([], this.explored.tiles, true);
    this.fog.setFloors(s.elev);
    this.pieces = pieceMap(s);
    this.terrain = this.pieces ? terrainOf(s, this.pieces) : s;
    this.ground?.dispose();
    this.ground = new GroundMap(this.terrain);
    this.buildings?.dispose();
    this.ao?.dispose();
    this.ao = new GroundOcclusion(s.width, s.height);
    this.snow?.dispose();
    this.snow = snowyMission(s) ? new SnowCover(s) : null;
    this.snowReady = false;
    this.buildings = new BuildingsView(this.terrain, this.fog, this.ao, this.snow);
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
    return {
      batch: new StaticBatch(CHUNK), slab, ground: this.own(groundMaterial), atlas: new Map(), clinical: null, clinicalWall: null, clinicalDirt: null,
      surroundings: this.surroundings(s, PALETTES[s.biome] ?? PALETTES.city, ground),
      snowbank: this.snow ? this.own(new THREE.MeshStandardMaterial({ map: snowTexture(), color: 0xe3f0ff, roughness: 0.98 })) : null,
    };
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
    // On the base's own screen pieces come and go, and change (basePieces.ts).
    const pieces = (this.pieces = pieceMap(s));
    const terrain = (this.terrain = pieces ? terrainOf(s, pieces) : s);
    const ground = this.ground ?? (this.ground = new GroundMap(terrain));
    const ao = this.ao ?? (this.ao = new GroundOcclusion(s.width, s.height));
    const buildings = this.buildings ?? (this.buildings = new BuildingsView(terrain, this.fog, ao, this.snow));
    const kit = this.kit ?? (this.kit = this.newKit(s, ground, ao));

    const pal = PALETTES[s.biome] ?? PALETTES.city;
    const batch = kit.batch;
    // Only round what changed since the last rebuild (a door, a blast, a search): the chunks it touches are placed
    // and merged again, the rest stays as it is. `area` marks the tiles to place (whatever lands in those chunks).
    const redo = this.changed(s, batch);
    const area = redo && this.areaOf(s, redo, batch.chunk);
    batch.begin(redo);
    for (const place of this.loot.keys()) if (!area || area[place % (s.width * s.height)]) this.loot.delete(place);
    const layer = (geometry: THREE.BufferGeometry, material: THREE.Material, shadows = true) => new Layer(batch, geometry, material, shadows);
    // Floors: the ground shader paints their tops as continuous terrain; the colour per tile is for their sides.
    const slabs = new Layer(batch, kit.slab, kit.ground, false, true, true);
    const hills = s.biome === 'wilds';

    // Synty props: their meshes, each on its pack's atlas.
    const syntyLayers = new Map<string, Layer[]>();
    /**
     * The props' material: one for every pack in the packed texture (mapAtlas.ts: a draw for all of them), else the
     * pack's own. What stands on a building's storey fades with the floors above the one in view.
     */
    const atlasMaterial = (id: AtlasId) => {
      const key = atlasRect(id) ? 'packed' : id;
      let m = kit.atlas.get(key);
      if (!m) {
        m = new THREE.MeshStandardMaterial({ map: key === 'packed' ? mapAtlasTexture() : atlasTexture(id), roughness: 0.85 });
        this.snow?.patch(m);
        this.own(m);
        floorFadePatch(m, buildings.cutaway);
        kit.atlas.set(key, m);
      }
      return m;
    };
    /** Where the props being placed stand (a building's storey: they fade with it; else out of doors). */
    const floor: FloorTag = { building: -1, level: 0 };
    /** A layer for one part of a prop, its UVs moved into its atlas's place. */
    const partLayer = (part: { geometry: THREE.BufferGeometry; atlas: AtlasId }, shadows = true, occlude = shadows) =>
      new Layer(batch, part.geometry, atlasMaterial(part.atlas), shadows, false, occlude, atlasRect(part.atlas), floor);
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
    const bedVariants = new Set((s.medical ?? []).flatMap((b) => (b.dressing ?? [])
      .filter((d) => d.kind === 'bareBed' || d.kind === 'coveredBed').map((d) => d.z * s.width * s.height + d.y * s.width + d.x)));
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
    /** How high prop `id` stands as placed (its parts' highest point), for what is set on it. */
    const topOf = (id: PropId): number => {
      let top = 0;
      for (const part of syntyProp(id) ?? []) {
        const box = (part.geometry.boundingBox ?? (part.geometry.computeBoundingBox(), part.geometry.boundingBox!)).clone().applyMatrix4(part.matrix);
        top = Math.max(top, box.max.y);
      }
      return top || 0.3;
    };
    /** How big a prop is as placed (its parts' box), for what is fitted into a space (basePieces.ts). */
    const sizeOf = (id: PropId): THREE.Vector3 => {
      const box = new THREE.Box3();
      for (const part of syntyProp(id) ?? []) box.union((part.geometry.boundingBox ?? (part.geometry.computeBoundingBox(), part.geometry.boundingBox!)).clone().applyMatrix4(part.matrix));
      return box.isEmpty() ? new THREE.Vector3(1, 1, 1) : box.getSize(new THREE.Vector3());
    };
    /**
     * A special loot box (docs/FASE8.md §B) on tile i, turned `yaw`: each kind
     * its own shape and colour from afar, its lid or what is in it on top;
     * opened (searched), the lid on the floor beside it and some of what was in
     * it about. Its mark (lootMarks.ts) hangs over it.
     */
    const lootBox = (kind: LootKind, i: number, x: number, base: number, z: number, yaw: number, open: boolean) => {
      const c = Math.cos(yaw);
      const sn = Math.sin(yaw);
      const r = (k: number) => hash(x, z, 70 + k);
      /** A prop at (dx, dz) from the tile's middle in the box's own frame, `dy` up, turned `turn` more. */
      const put = (id: PropId, dx: number, dz: number, dy = 0, turn = 0, shadows = true) =>
        addSyntyId(id, placed(x + dx * c + dz * sn, base + dy, z - dx * sn + dz * c, 1, 1, 1, yaw + turn), shadows);
      const main: Record<LootKind, PropId> = { lootWeapons: 'lootWeapons', lootMedical: 'lootMedical', lootAmmo: 'lootAmmo', lootFood: 'lootFood' };
      const box = main[kind];
      put(box, 0, 0);
      const top = topOf(box);
      switch (kind) {
        case 'lootWeapons':
          if (open) put('lootWeaponsLid', 0.05, 0.5, 0, 0.25 + r(1) * 0.3, false);
          else put('lootWeaponsLid', 0, 0, top * 0.98);
          break;
        case 'lootMedical':
          if (open) {
            put('lootMedicalKit', -0.25, 0.5, 0, r(1) * 2);
            put('lootPills', 0.2, 0.45, 0, r(2) * 6, false);
            put('lootPills', 0.32, 0.58, 0, r(3) * 6, false);
          } else put('lootMedicalKit', 0, 0, top, 0.2);
          break;
        case 'lootAmmo':
          if (open) {
            put('lootAmmoLid', 0.1, 0.5, 0, 0.3 + r(1) * 0.3, false);
            put('lootAmmoCanOpen', -0.3, 0.52, 0, r(2) * 3);
          } else {
            put('lootAmmoLid', 0, 0, top * 0.98);
            put('lootAmmoCan', 0, 0, top + 0.02, 0.4);
          }
          break;
        case 'lootFood':
          if (open) {
            put('lootFoodTop', -0.2, 0.52, 0, r(1) * 2);
            put('lootCan', 0.18, 0.45, 0, r(2) * 6, false);
            put('lootCan2', 0.3, 0.58, 0, r(3) * 6, false);
            put('lootRice', 0.05, 0.62, 0, r(4) * 6, false);
          } else put('lootFoodTop', 0.04, -0.03, top, 0.35);
          break;
      }
      this.loot.set(placeBase + i, { place: placeBase + i, kind, at: new THREE.Vector3(x, base + top + 0.95, z), open });
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
    /** The storey being dressed: its decor and tiles, and where its places start among the map's (searched lists places). */
    let decorNow = decor;
    let tilesNow: readonly (TileKind | null)[] = s.tiles;
    let placeBase = 0;
    /** A fence or gate post still standing: the fences beside it run their rails to it. */
    const fenceAt = (x: number, y: number) => {
      const kind = at(x, y) === 'low' ? decorOf(decor[y * s.width + x])?.kind : undefined;
      return kind === 'fence' || kind === 'gatePost';
    };
    const rails: PropSet = s.biome === 'city' ? 'fenceBoards' : 'fenceRails';
    /** The blocks found so far, by place (every tile of one points at it). */
    const blocks = new Map<number, Block>();
    /** The block tile i (of the storey being dressed) belongs to: the tiles of its kind joined to it. */
    const blockOf = (i: number, kind: DecorKind): Block => {
      const known = blocks.get(placeBase + i);
      if (known) return known;
      const tiles = [i];
      const seen = new Set(tiles);
      for (let k = 0; k < tiles.length; k++) {
        const tx = tiles[k]! % s.width;
        const ty = Math.floor(tiles[k]! / s.width);
        for (const d of DIRS4) {
          const nx = tx + d.x;
          const ny = ty + d.y;
          if (nx < 0 || ny < 0 || nx >= s.width || ny >= s.height) continue;
          const j = ny * s.width + nx;
          if (!seen.has(j) && blockKind(decorOf(decorNow[j])?.kind) === blockKind(kind)) {
            seen.add(j);
            tiles.push(j);
          }
        }
      }
      const xs = tiles.map((t) => t % s.width);
      const ys = tiles.map((t) => Math.floor(t / s.width));
      const x0 = Math.min(...xs);
      const y0 = Math.min(...ys);
      const w = Math.max(...xs) - x0 + 1;
      const h = Math.max(...ys) - y0 + 1;
      const b: Block = {
        x0,
        y0,
        w,
        h,
        facing: decorOf(decorNow[i])!.facing,
        whole: tiles.every((t) => {
          const part = decorOf(decorNow[t])!.kind;
          return tilesNow[t] === (BLOCKS[part] ?? FOUNTAIN[part]);
        }),
        anchor: Math.floor(y0 + h / 2) * s.width + Math.floor(x0 + w / 2),
      };
      for (const t of tiles) blocks.set(placeBase + t, b);
      return b;
    };
    /** A block's model over its middle: a lorry with its nose and a container with its doors the way it faces, a pile's logs along it. */
    const drawBlock = (b: Block, kind: DecorKind, base: number) => {
      const cx = b.x0 + b.w / 2;
      const cz = b.y0 + b.h / 2;
      const r = hash(b.x0, b.y0, 31);
      const yaw = turnTo(b.facing) * (Math.PI / 2);
      switch (kind) {
        case 'truck':
          addSynty('truck', r, placed(cx, base, cz, 1, 1, 1, yaw));
          break;
        case 'container':
          addSynty('container', r, placed(cx, base, cz, 1, 1, 1, yaw));
          break;
        case 'waterTower':
          addSyntyId('waterTower', placed(cx, base, cz, 1, 1, 1, Math.floor(r * 4) * (Math.PI / 2)));
          break;
        case 'silo':
          // Its ladder any way round.
          addSynty('silo', r, placed(cx, base, cz, 1, 1, 1, Math.floor(hash(b.x0, b.y0, 32) * 4) * (Math.PI / 2)));
          break;
        case 'bench':
          // Along its two tiles, its seat the way it faces; a broken one now and then.
          addSyntyId(r < 0.3 ? 'parkBenchBroken' : 'parkBench', placed(cx, base, cz, 1, 1, 1, yaw));
          break;
        case 'logPile':
          // Its logs (the model's z) along a long pile, narrowed to its tile; a square one whole. Half a survivor high or so.
          if (b.w !== b.h) addSyntyId('logPile', placed(cx, base, cz, 0.62, 0.75, 1, b.w > b.h ? Math.PI / 2 : 0));
          else addSyntyId('logPile', placed(cx, base, cz, 1, 0.75, 1, Math.floor(r * 4) * (Math.PI / 2)));
          break;
      }
    };
    /**
     * Draws the cover on tile i as its decor hint says, if it has one for what
     * the tile is now; false to fall back to the biome's random props.
     */
    const dressed = (i: number, x: number, y: number, kind: 'low' | 'high', base: number): boolean => {
      const look = decorOf(decorNow[i]);
      // The bus's bay: La Mula stands on it whole (placeBus), nothing else.
      if (look?.kind === 'bus') return true;
      // The siren's car's nose: the car is the siren's own (SirenRig), nothing else here.
      if (look?.kind === 'sirenCar' && kind === 'high') return true;
      // A lorry, a container, a water tower, a log pile: one model over its block while all of it stands.
      if (look && BLOCKS[look.kind]) {
        const b = blockOf(i, look.kind);
        if (b.whole) {
          if (i === b.anchor) drawBlock(b, look.kind, base);
          return true;
        }
      }
      // By the forest road, the statue is the lumberjack on his plinth.
      if (look?.kind === 'statue' && kind === 'high' && s.site === 'roadside') {
        const yaw = Math.floor(hash(x, y, 2) * 4) * (Math.PI / 2);
        addSyntyId('lumberjackBase', placed(x + 0.5, base, y + 0.5, 1, 1, 1, yaw));
        addSyntyId('lumberjack', placed(x + 0.5, base + topOf('lumberjackBase'), y + 0.5, 1, 1, 1, yaw));
        return true;
      }
      // A square's fountain: the basin whole over its 3 × 3 (the statue its top) or its 2 × 2, while all of it stands.
      if (look && FOUNTAIN[look.kind]) {
        const b = blockOf(i, look.kind);
        if (b.whole && b.w === b.h && b.w >= 2) {
          if (i === b.anchor) addSyntyId('fountainWhole', placed(b.x0 + b.w / 2, base, b.y0 + b.h / 2, b.w / 3, b.w / 3, b.w / 3, 0));
          return true;
        }
      }
      if (look?.kind === 'hospitalBed' && kind === 'low' && bedVariants.has(placeBase + i)) return true;
      const clinical = look && medicalFurniture(look.kind, hash(x, y, 73 + floor.level) < 0.6);
      if (clinical && (look.kind === 'medicalCabinet' || look.kind === 'xray' || look.kind === 'waterDispenser' ? kind === 'high' : kind === 'low')) {
        if (!kit.clinical) {
          kit.clinical = this.own(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.8 }));
          floorFadePatch(kit.clinical, buildings.cutaway);
        }
        const wear = buildings.plan.buildings[floor.building]?.medicalWear ?? 0;
        const yaw = turnTo(look.facing) * Math.PI / 2 + (hash(x, y, 19) - 0.5) * wear * 0.7;
        const place = placed(x + 0.5, base, y + 0.5, 1, 1, 1, yaw);
        for (const part of clinical) batch.add(part.geometry, kit.clinical, place.clone().multiply(part.matrix), true, {
          color: [part.color.r, part.color.g, part.color.b], aFloor: [floor.building + 1, floor.level],
        }, true);
        if (look.kind === 'medicalCart') addSyntyId('clinicalKit', place.clone().multiply(new THREE.Matrix4().makeTranslation(-0.08, 0.59, -0.03)));
        return true;
      }
      const set = look && decorSet(look.kind, kind, s.biome, s.site);
      if (!look || !set) return false;
      const cx = x + 0.5;
      const cz = y + 0.5;
      const turn = FACING.has(look.kind) ? turnTo(look.facing) : Math.floor(hash(x, y, 2) * 4);
      let pick = hash(x, y, 1);
      if (look.kind === 'carFront' || look.kind === 'carRear') {
        // A car is one model over both tiles, drawn only while both stand: the front tile draws it, centred between the two.
        const j = carPartner(decorNow, s.width, i);
        if (j < 0 || tilesNow[j] !== 'low') return false;
        if (look.kind === 'carRear') return true;
        pick = hash(x, y, 1);
        const d = DIRS4[look.facing]!;
        const at = placed(cx - d.x * 0.5, base, cz - d.y * 0.5, 1, 1, 1, turn * (Math.PI / 2));
        // Searched (its boot or its glovebox), it's the one with the boot lid up.
        if (searched.has(placeBase + i) || searched.has(placeBase + j)) addSyntyId('carOpen', at);
        else addSynty('carFront', pick, at);
        if (hash(x, y, 9) < 0.25) headlights.push({ x: cx + d.x * 0.42, y: base + 0.32, z: cz + d.y * 0.42, dx: d.x, dz: d.y });
        return true;
      }
      if (LOOT_KINDS.has(look.kind)) {
        lootBox(look.kind as LootKind, i, cx, base, cz, turn * (Math.PI / 2), searched.has(placeBase + i));
        return true;
      }
      // Searched furniture: opened, its contents on the floor in front of it.
      if (searched.has(placeBase + i)) {
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
      // Pallets stacked, three for low cover and six for high, each a little askew; a machine in the hall, the genset stood taller.
      if (look.kind === 'pallets') {
        const n = kind === 'high' ? 6 : 3;
        for (let k = 0; k < n; k++) {
          const r = (salt: number) => hash(x, y, 40 + k * 3 + salt) - 0.5;
          addSyntyId('pallet', placed(cx + r(0) * 0.06, base + k * 0.19, cz + r(1) * 0.06, 1, 1, 1, turn * (Math.PI / 2) + r(2) * 0.12));
        }
        return true;
      }
      // High crates: a big one with a smaller one on it, askew.
      if (look.kind === 'crates' && kind === 'high') {
        const yaw = turn * (Math.PI / 2);
        addSyntyId('crates', placed(cx, base, cz, 1, 1, 1, yaw));
        addSyntyId('crate', placed(cx + (hash(x, y, 11) - 0.5) * 0.12, base + topOf('crates'), cz + (hash(x, y, 12) - 0.5) * 0.12, 1, 1, 1, yaw + (hash(x, y, 13) - 0.5) * 0.6));
        return true;
      }
      // A house's television on its stand; a wardrobe or a cabinet knocked over on its face (low cover, #15).
      if (look.kind === 'tvStand' && kind === 'low') {
        const yaw = turn * (Math.PI / 2);
        addSyntyId('tvStand', placed(cx, base, cz, 1, 1, 1, yaw));
        addSyntyId('television', placed(cx, base + topOf('tvStand'), cz, 1, 1, 1, yaw + (hash(x, y, 16) - 0.5) * 0.2));
        return true;
      }
      if ((look.kind === 'wardrobe' || look.kind === 'cabinet') && kind === 'low') {
        const d = DIRS4[look.facing]!;
        const m = new THREE.Matrix4().compose(
          new THREE.Vector3(cx + d.x * 0.35, base + 0.28, cz + d.y * 0.35),
          new THREE.Quaternion().setFromEuler(new THREE.Euler(Math.PI / 2, turn * (Math.PI / 2) + (hash(x, y, 17) - 0.5) * 0.3, 0, 'YXZ')),
          new THREE.Vector3(1, 1, 1),
        );
        addSynty(look.kind === 'wardrobe' ? 'wardrobe' : 'cabinet', pick, m);
        return true;
      }
      if (look.kind === 'machine' && kind === 'high') {
        addSyntyId('machine', placed(cx, base, cz, 1, 1.9, 1, turn * (Math.PI / 2)));
        return true;
      }
      // The base's pinar (docs/FASE12.md §11.1): pines of all sizes, off the grid, a dead one now and then.
      if (s.base && look.kind === 'tree' && kind === 'high') {
        const k = 0.8 + hash(x, y, 14) * 0.45;
        addSynty('basePines', pick, placed(cx + (hash(x, y, 15) - 0.5) * 0.45, base, cz + (hash(x, y, 16) - 0.5) * 0.45, k, k * (0.9 + hash(x, y, 17) * 0.25), k, hash(x, y, 18) * Math.PI * 2));
        return true;
      }
      // A fallen tree is a row of logs: it keeps to the grid.
      addProp(i, set, cx, base, cz, pick, turn, look.kind === 'tree' && kind === 'low' ? 1 : (WIGGLE[look.kind] ?? 0));
      if (look.kind === 'hospitalBed' && kind === 'low') {
        const yaw = turn * Math.PI / 2;
        const at = placed(cx, base, cz, 1, 1, 1, yaw);
        // The slim drip stays within this bed's cover tile.
        addSyntyId('ivStand', at.multiply(new THREE.Matrix4().makeTranslation(0.31, 0, 0.22)), true);
      }
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

    /**
     * What a blast left of the wall on tile (x, y) of storey `level`: a heap
     * of rubble either side of its stump, blown out of the line of the wall,
     * and gravel about it (the heaps are the low cover; the gravel is flat).
     */
    const rubbleOf = (x: number, y: number, level: number, base: number, tiles: readonly (TileKind | null)[]) => {
      const wallish = (tx: number, ty: number) =>
        tx >= 0 && ty >= 0 && tx < s.width && ty < s.height && (isWallLike(tiles[ty * s.width + tx] ?? 'floor') || buildings.plan.breached(ty * s.width + tx, level));
      // The wall runs along x when the walls beside it do; the heaps fall across it.
      const across = wallish(x - 1, y) || wallish(x + 1, y) ? { x: 0, y: 1 } : { x: 1, y: 0 };
      for (const side of [-1, 1]) {
        const r = (salt: number) => hash(x, y, 60 + level * 7 + (side + 1) * 3 + salt);
        const px = x + 0.5 + across.x * side * 0.34 + across.y * (r(0) - 0.5) * 0.4;
        const pz = y + 0.5 + across.y * side * 0.34 + across.x * (r(0) - 0.5) * 0.4;
        addSyntyId(r(1) < 0.5 ? 'rubble1' : 'rubble2', placed(px, base, pz, 0.62, 0.55, 0.62, r(2) * Math.PI * 2));
        addSyntyId('pebbles', placed(px + across.x * side * 0.25, base + 0.01, pz + across.y * side * 0.25, 1, 1, 1, r(3) * Math.PI * 2), false);
      }
    };

    // A siege's base (docs/FASE12.md §10): its barricades' sandbags as worn as they are, what is left of a gate or a
    // barricade that has given way (the map as it was made says which), and its watchtowers.
    const siege = !!s.mission.siege;
    const wear = siege ? wearOf(s) : new Map<number, Wear>();
    /** A barricade's sandbags: lower and spilt as it wears (Wear), knocked flat and spread round once it has given way (3). */
    const sandbags = (i: number, x: number, y: number, base: number, stage: number, yaw?: number) => {
      const r = (salt: number) => hash(x, y, 80 + salt);
      const facing = decorOf(decor[i])?.facing ?? 0;
      addSyntyId('sandbags', placed(x + 0.5, base, y + 0.5, 1, [1, 0.78, 0.5, 0.2][stage]!, 1, (yaw ?? (facing ? Math.PI / 2 : 0)) + (r(0) - 0.5) * 0.08 * stage));
      // The bags knocked off: most out where the dead strike, a few in.
      const into = inwards(s, x, y);
      const loose = [0, 2, 4, 7][stage]!;
      for (let k = 0; k < loose; k++) {
        const out = r(10 + k) < 0.7 ? -1 : 1;
        const d = 0.32 + r(20 + k) * (stage === 3 ? 0.55 : 0.3);
        const t = (r(30 + k) - 0.5) * 0.9;
        const px = x + 0.5 + into.x * out * d + into.z * t;
        const pz = y + 0.5 + into.z * out * d + into.x * t;
        addSyntyId('sandbag', placed(px, base + (k > 3 ? 0.05 : 0.01), pz, 1, 1, 1, r(40 + k) * Math.PI * 2), false);
      }
    };
    /** A gate or a door given way: its leaf down flat, fallen in, the foot on its line, and grit off the posts. */
    const fallenDoor = (i: number, x: number, y: number, base: number) => {
      const r = (salt: number) => hash(x, y, 90 + salt);
      const into = inwards(s, x, y);
      const big = buildings.plan.owner[i]! < 0 ? 1 : 0.8;
      const m = new THREE.Matrix4().compose(
        new THREE.Vector3(x + 0.5 + into.x * 0.06, base + 0.09, y + 0.5 + into.z * 0.06),
        new THREE.Quaternion().setFromEuler(new THREE.Euler(-Math.PI / 2 + 0.05, Math.atan2(-into.x, -into.z) + (r(0) - 0.5) * 0.5, (r(1) - 0.5) * 0.08, 'YXZ')),
        new THREE.Vector3(big, big, big),
      );
      addSyntyId('gateLeafDown', m);
      for (const side of [-1, 1]) {
        const px = x + 0.5 + into.z * side * 0.42 + into.x * (r(2 + side) - 0.3) * 0.3;
        const pz = y + 0.5 + into.x * side * 0.42 + into.z * (r(4 + side) - 0.3) * 0.3;
        addSyntyId('pebbles', placed(px, base + 0.01, pz, 1, 1, 1, r(6 + side) * Math.PI * 2), false);
      }
    };
    if (siege) for (const t of towersOf(s, buildings.plan.raised)) if (!area || area[t.tile]) addSyntyId('guardTower', placed(t.x, 0, t.z, 1, 1, 1, t.yaw));
    // The new base's pieces (basePieces.ts), each from the tile under its middle; its sandbags as a siege's barricade's.
    // Where a pine was felled, its stump (flat: no cover) till something is built over it.
    if (pieces) {
      for (const i of s.base?.felled ?? []) {
        if ((area && !area[i]) || s.tiles[i] !== 'floor' || pieces.at[i]! >= 0) continue;
        const x = i % s.width;
        const y = (i - x) / s.width;
        const k = 0.6 + hash(x, y, 21) * 0.35;
        addSyntyId('bStump', placed(x + 0.5 + (hash(x, y, 22) - 0.5) * 0.4, 0, y + 0.5 + (hash(x, y, 23) - 0.5) * 0.4, k, k, k, hash(x, y, 24) * Math.PI * 2));
      }
      const art = { put: (id: PropId, m: THREE.Matrix4, shadows = true) => void addSyntyId(id, m, shadows), top: topOf, size: sizeOf };
      for (const spot of pieces.spots) {
        if (!DRAWN.has(spot.piece.piece) || (area && !area[spot.anchor])) continue;
        const worn = pieceWear(s, spot);
        if (spot.piece.piece === 'sandbags') sandbags(spot.anchor, spot.piece.x, spot.piece.y, 0, worn, spot.piece.rot % 2 ? 0 : Math.PI / 2);
        else drawPiece(s, pieces, spot, worn, art);
      }
    }

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
        // Under a watchtower's deck, the ground.
        if (raised === RAISED_TOWER) slabs.add(placed(cx, -0.05, cz, 1, 1, 1), pal.ground);
        else if (!covered) slabs.add(placed(cx, base - 0.05, cz, 1, 1, 1), floor);
        // A siege's door knocked about: grit and flakes at its foot, out where the dead strike it.
        if (siege && (kind === 'door' || kind === 'doorOpen') && wear.get(i)) {
          const into = inwards(s, x, y);
          const stage = wear.get(i)!;
          for (let k = 0; k < stage + 1; k++) {
            const r = (salt: number) => hash(x, y, 100 + k * 4 + salt);
            const t = (r(0) - 0.5) * 0.8;
            addSyntyId('pebbles', placed(cx - into.x * (0.35 + r(1) * 0.25) + into.z * t, base + 0.01, cz - into.z * (0.35 + r(1) * 0.25) + into.x * t, 1, 1, 1, r(2) * Math.PI * 2), false);
          }
          if (stage === 2) addSyntyId('rubble1', placed(cx - into.x * 0.45, base, cz - into.z * 0.45, 0.4, 0.35, 0.4, hash(x, y, 120) * Math.PI * 2));
        }
        if (isWallLike(kind)) continue;
        // The base's pieces are drawn whole above.
        if (pieces && pieces.at[i]! >= 0 && DRAWN.has(pieces.spots[pieces.at[i]!]!.piece.piece)) continue;
        if (siege && kind === 'floor' && e === 0) {
          const was = madeAs(s, x, y);
          if (was === 'b') sandbags(i, x, y, base, 3);
          else if (was === '+') fallenDoor(i, x, y, base);
        }

        const v = hash(x, y, 1);
        switch (kind) {
          case 'high':
            if (!dressed(i, x, y, kind, base)) addProp(i, props.high, cx, base, cz, v, Math.floor(hash(x, y, 2) * 4), randomWiggle);
            break;
          case 'low':
            if (siege && madeAs(s, x, y) === 'b') {
              sandbags(i, x, y, base, wear.get(i) ?? 0);
              break;
            }
            // A wall blown down (docs/FASE7.md §4): its rubble, over the stump the walls draw (buildings.ts).
            if (buildings.plan.breached(i, 0)) {
              rubbleOf(x, y, 0, base, s.tiles);
              break;
            }
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

    // Each storey's furniture and cover (GameState.levels), as on the ground; it fades with the floors above the one in view.
    s.levels?.forEach((storey, k) => {
      const level = k + 1;
      const base = level * STOREY;
      decorNow = storey.decor ?? [];
      tilesNow = storey.tiles;
      placeBase = level * s.width * s.height;
      floor.level = level;
      for (let y = 0; y < s.height; y++) {
        for (let x = 0; x < s.width; x++) {
          const i = y * s.width + x;
          if (area && !area[i]) continue;
          const kind = storey.tiles[i];
          if (kind !== 'low' && kind !== 'high') continue;
          const b = buildings.plan.owner[i]!;
          const bd = b >= 0 ? buildings.plan.buildings[b] : undefined;
          // Low cover round the edge of a roof one walks on is its parapet (buildings.ts).
          if (kind === 'low' && bd?.walkableRoof && level === bd.storeys && !decorOf(decorNow[i])) continue;
          floor.building = b;
          if (kind === 'low' && buildings.plan.breached(i, level)) {
            rubbleOf(x, y, level, base, storey.tiles);
            continue;
          }
          if (dressed(i, x, y, kind, base)) continue;
          // Cover with nothing to say what it is, indoors: shelves and crates (out of doors, the biome's props).
          const v = hash(x, y, 1 + level);
          const set = (b >= 0 ? decorSet(kind === 'high' ? 'shelf' : 'crates', kind, s.biome) : null) ?? (kind === 'high' ? props.high : props.low);
          addProp(i, set, x + 0.5, base, y + 0.5, v, Math.floor(hash(x, y, 2 + level) * 4), b >= 0 ? 1 : randomWiggle);
        }
      }
    });
    // Seeded clinical scenery: stored with the mission, nonblocking, and
    // removed visually if its floor, bed, furniture or supporting wall is destroyed.
    // And the houses' (playtest 18/10, #15): the same details, the bodies, blood and mess left in them.
    for (const b of [...(s.medical ?? []), ...(s.homes ?? [])]) for (const d of b.dressing ?? []) {
      const i = d.y * s.width + d.x;
      if (area && !area[i]) continue;
      const st = d.z ? s.levels?.[d.z - 1] : { tiles: s.tiles, decor: s.decor };
      const tile = st?.tiles[i], look = decorOf(st?.decor?.[i]);
      if (!tile) continue;
      const isBed = d.kind === 'bareBed' || d.kind === 'coveredBed';
      const isBody = d.kind === 'corpseCivilian' || d.kind === 'corpseStaff' || d.kind === 'coveredCorpse';
      const isWall = d.kind === 'wallCross' || d.kind === 'cautionSign' || d.kind === 'safeSign' || d.kind === 'wallBlood';
      const scatter = MEDICAL_SCATTER.has(d.kind), flat = MEDICAL_FLAT.has(d.kind);
      if (isBed && (tile !== 'low' || look?.kind !== 'hospitalBed')) continue;
      if (isBody && tile !== 'floor') continue;
      if (scatter && tile !== 'floor' && tile !== 'low' && tile !== 'high') continue;
      if (!scatter && !isBed && !isBody && !isWall && tile !== 'low' && tile !== 'high') continue;
      const normal = DIRS4[d.facing]!;
      const wallX = d.x - normal.x, wallY = d.y - normal.y;
      if (isWall && st?.tiles[wallY * s.width + wallX] !== 'wall') continue;
      floor.building = buildings.plan.owner[i]!; floor.level = d.z;
      const at = placed(d.x + 0.5, d.z * STOREY + d.height, d.y + 0.5, 1, 1, 1, turnTo(d.facing) * Math.PI / 2);
      if (d.kind === 'corpseCivilian' || d.kind === 'corpseStaff') { addSyntyId(d.kind, at); continue; }
      if (!kit.clinical) {
        kit.clinical = this.own(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.8 }));
        floorFadePatch(kit.clinical, buildings.cutaway);
      }
      if (d.kind === 'floorGrime' && !kit.clinicalDirt) {
        kit.clinicalDirt = this.own(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 1, transparent: true, opacity: 0.24, depthWrite: false }));
        floorFadePatch(kit.clinicalDirt, buildings.cutaway);
      }
      if (isWall && !kit.clinicalWall) {
        kit.clinicalWall = this.own(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.9 }));
        floorFadePatch(kit.clinicalWall, buildings.cutaway); medicalWallPatch(kit.clinicalWall, buildings.cutaway);
      }
      const outside = wallX <= b.x || wallY <= b.y || wallX >= b.x + b.w - 1 || wallY >= b.y + b.h - 1;
      for (const part of medicalDetails(d.kind, d.variant)) batch.add(part.geometry, isWall ? kit.clinicalWall! : d.kind === 'floorGrime' ? kit.clinicalDirt! : kit.clinical, at.clone().multiply(part.matrix), !flat && !isWall, {
        color: [part.color.r, part.color.g, part.color.b], aFloor: [floor.building + 1, floor.level],
        ...(isWall ? { aMedicalWall: outside ? [-normal.x, -normal.y] : [0, 0] } : {}),
      }, !flat && !isWall);
      if (d.kind === 'coveredBed') addSyntyId('ivStand', at.clone().multiply(new THREE.Matrix4().makeTranslation(-0.31, 0, 0.22)));
    }
    decorNow = decor;
    tilesNow = s.tiles;
    placeBase = 0;

    // Under a slab blown through (docs/FASE7.md §5): what came down with it, on the floor below. Flat: no cover.
    for (let k = 1; k <= (s.levels?.length ?? 0); k++) {
      for (let i = 0; i < s.width * s.height; i++) {
        if ((area && !area[i]) || !buildings.plan.holed(i, k)) continue;
        const x = i % s.width;
        const y = (i - x) / s.width;
        const base = (k - 1) * STOREY + (k === 1 ? (s.elev[i] ?? 0) * STOREY : 0);
        floor.building = buildings.plan.owner[i]!;
        floor.level = k - 1;
        for (let n = 0; n < 3; n++) {
          const r = (salt: number) => hash(x, y, 40 + n * 3 + salt);
          addSyntyId('pebbles', placed(x + 0.2 + r(0) * 0.6, base + 0.01, y + 0.2 + r(1) * 0.6, 1, 1, 1, r(2) * Math.PI * 2), false);
        }
        addSyntyId('rubble1', placed(x + 0.5, base, y + 0.5, 0.45, 0.35, 0.45, hash(x, y, 49) * Math.PI * 2), false);
      }
    }
    floor.building = -1;
    floor.level = 0;

    // The streets' dressing (street lights, poles and wires, bags, weeds…): cosmetic, merged with the rest.
    const lamps: Glow[] = [];
    // The map's own lights (generated maps): a street light on each. The new base's are its floodlights (below).
    const fixed = pieces ? [] : s.lights ? s.lights.filter((l) => l.turns === undefined).map((l) => l.tile) : null;
    for (const d of dressStreets(s, ground, COVER_DENSITY[graphics().effects], fixed)) {
      addSyntyId(d.id, d.matrix, d.cast);
      if (d.id !== 'streetLight') continue;
      // The lamp hangs at the end of the arm (its +x), over the road.
      const e = d.matrix.elements;
      lamps.push({ x: e[12]! + e[0]! * 1.45, y: 4.05, z: e[14]! + e[2]! * 1.45, dx: e[0]!, dz: e[2]! });
    }
    // A floodlight's lamps at the top of its mast (basePieces.ts), while it stands.
    if (pieces) {
      for (const spot of pieces.spots) {
        if (spot.piece.piece === 'floodlight' && pieceWear(s, spot) < 3) lamps.push({ x: spot.x, y: 2.95, z: spot.z, dx: Math.sin(spot.yaw), dz: Math.cos(spot.yaw), power: 2 });
      }
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
      // Nothing grows through the base's buildings, nor through what is left of them.
      if (pieces) {
        const k = pieces.at[Math.floor(matrix.elements[14]!) * s.width + Math.floor(matrix.elements[12]!)] ?? -1;
        if (k >= 0 && pieces.spots[k]!.def.kind === 'building') continue;
      }
      const prop = syntyProp(id);
      if (!prop) {
        this.waitingForProps = true;
        continue;
      }
      let layers = coverLayers.get(id);
      if (!layers) coverLayers.set(id, (layers = prop.map((part) => partLayer(part, false))));
      prop.forEach((part, k) => layers[k]!.add(matrix.clone().multiply(part.matrix), 0xffffff));
    }

    if (this.snow) {
      const drift = kitModel('drift');
      if (!drift) {
        // The shared kit caches failures too: only wait once, then keep the textured snow.
        if (!this.snowReady) {
          this.snowReady = true;
          void this.snow.ready.then(() => {
            if (this.shown === s) { this.drawn = null; this.rebuild(s); }
          });
        }
      } else {
        const m = kit.snowbank!;
        for (const matrix of this.snow.drifts(buildings.plan)) {
          const e = matrix.elements;
          if (area && !area[Math.floor(e[14]!) * s.width + Math.floor(e[12]!)]) continue;
          for (const part of drift.parts) layer(part.geometry, m, false).add(matrix);
        }
      }
    }

    // A filling station's pumps (decor 'tank', low): an island under each pair, a canopy over each station's.
    const pumps: number[] = [];
    for (let i = 0; i < s.tiles.length; i++) if (s.tiles[i] === 'low' && decorOf(decor[i])?.kind === 'tank' && (s.elev[i] ?? 0) === 0) pumps.push(i);
    for (const i of pumps) {
      const x = i % s.width;
      const y = Math.floor(i / s.width);
      // The pair's first tile (west or north) draws the island between the two.
      const east = pumps.includes(i + 1) && x + 1 < s.width;
      const south = pumps.includes(i + s.width);
      if ((!east && !south) || (area && !area[i])) continue;
      addSyntyId('pumpIsland', placed(x + (east ? 1 : 0.5), 0, y + (south ? 1 : 0.5), 0.55, 0.3, 0.55, east ? Math.PI / 2 : 0), false);
    }
    this.placeCanopies(s, pumps);

    let dirty = batch.commit();
    this.group.add(batch.group);
    this.lootMarks.set([...this.loot.values()]);
    this.group.add(this.lootMarks.group, this.dropped.group, this.canopyGroup);
    this.markLoot();
    this.placeBus(s);
    if (!buildings.buildWalls(terrain, redo, area)) this.waitingForProps = true;
    dirty = union(dirty, buildings.dirty);
    this.group.add(buildings.group, buildings.walls);
    // What's drawn now, for the next rebuild to compare with; with props still loading, the next one redoes everything.
    this.drawn = { tiles: s.tiles.slice(), levels: (s.levels ?? []).map((l) => l.tiles.slice()), searched: (s.searched ?? []).join(','), wear, pieces: this.pieceKeys(s) };
    buildings.wearDoors(wear);
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
    if (!drawn || drawn.tiles.length !== s.tiles.length || drawn.levels.length !== (s.levels?.length ?? 0)) return null;
    const touched: number[] = [];
    for (let i = 0; i < s.tiles.length; i++) if (s.tiles[i] !== drawn.tiles[i]) touched.push(i);
    // A door opened, a window broken upstairs (tileChanged with its storey).
    s.levels?.forEach((l, k) => {
      for (let i = 0; i < l.tiles.length; i++) if (l.tiles[i] !== drawn.levels[k]![i]) touched.push(i);
    });
    // A siege's door or barricade worn a stage further: the sandbags, the grit at a door's foot.
    if (s.mission.siege) for (const [i, stage] of wearOf(s)) if (drawn.wear.get(i) !== stage) touched.push(i);
    const searched = s.searched ?? [];
    if (searched.join(',') !== drawn.searched) {
      const before = new Set(drawn.searched ? drawn.searched.split(',').map(Number) : []);
      for (const i of searched) if (!before.has(i)) touched.push(i);
    }
    // A piece put, taken away or changed (its level, its wear, a trap spent): all its tiles.
    const keys = this.pieceKeys(s);
    for (const [id, now] of keys) if (drawn.pieces.get(id)?.key !== now.key) touched.push(...now.tiles);
    for (const [id, was] of drawn.pieces) if (keys.get(id)?.key !== was.key) touched.push(...was.tiles);
    // A piece's tile changed: the piece is drawn from its middle.
    if (this.pieces) for (const place of [...touched]) {
      const k = this.pieces.at[place % (s.width * s.height)] ?? -1;
      if (k >= 0) touched.push(this.pieces.spots[k]!.anchor);
    }
    const chunks = new Set<string>();
    for (const place of touched) {
      // Searches list places: upstairs ones come after the ground's.
      const i = place % (s.width * s.height);
      const x = i % s.width;
      const y = (i - x) / s.width;
      for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) chunks.add(batch.chunkAt(x + dx + 0.5, y + dy + 0.5));
    }
    return chunks;
  }

  /** Each of the base's pieces as drawn (basePieces.ts pieceKey), and its tiles. */
  private pieceKeys(s: GameState): Map<string, { key: string; tiles: number[] }> {
    const out = new Map<string, { key: string; tiles: number[] }>();
    const map = pieceMap(s);
    for (const spot of map?.spots ?? []) out.set(spot.piece.id, { key: pieceKey(s, spot), tiles: [spot.anchor, ...spot.cells.map((c) => c.y * s.width + c.x)] });
    return out;
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
    this.fog.setNight(on, !!this.snow);
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
    this.fog.setLit(night ? this.onFloor(s, litMask(s)) : []);
    this.night?.setBurning(s.lights ?? [], (x, y) => (s.elev[y * s.width + x] ?? 0) * STOREY);
  }

  /** Buildings whose inside the squad sees through a window or a door lose their roof (off in the lab, where all is in sight). */
  seeInside = true;

  /** A blow on a siege's door or barricade at `tile` (the 'smashed' event, docs/FASE12.md §10): a gate shakes on its hinges. */
  knock(tile: Place): void {
    if (this.shown) this.buildings?.knock(tile.y * this.shown.width + tile.x);
  }

  /**
   * What the squad sees now (a mask by place: the ground's tiles first, then
   * each storey's: teamVisibility): it is remembered as explored, and the fog
   * fades to match on the floor in view.
   */
  setVisibility(mask: boolean[]): void {
    // A siege is fought at home (playtest 18/10, #18): no fog, nothing unexplored; all of the base in sight. What the
    // squad really sees still opens the buildings it looks into (the cutaway), or every roof would go.
    this.sight = mask;
    if (this.shown?.mission.siege) mask = mask.map(() => true);
    this.visibility = mask;
    this.explored.see(mask, this.shown?.seq ?? 0);
    this.refog();
    this.fresh = false;
    this.markLoot();
  }

  /**
   * La Mula by the evacuation zone, its doors to it, unless it has gone
   * (mission.evacDone): on its bay (mission.evacBus), its nose to the edge it
   * leaves by; on a map without one, just off the edge nearest the zone and
   * along it, its nose to the nearer corner.
   */
  private placeBus(s: GameState): void {
    const zoneTiles = s.mission.evacZone;
    if (!zoneTiles.length) {
      if (this.bus) this.bus.root.visible = false;
      this.busWay = null;
      return;
    }
    const bus = (this.bus ??= new EvacBus());
    const mid = (tiles: readonly { x: number; y: number }[]) =>
      new THREE.Vector3(tiles.reduce((a, t) => a + t.x + 0.5, 0) / tiles.length, 0, tiles.reduce((a, t) => a + t.y + 0.5, 0) / tiles.length);
    const zone = mid(zoneTiles);
    const bay = s.mission.evacBus;
    let at: THREE.Vector3;
    let d: { x: number; y: number };
    if (bay?.tiles.length) {
      at = mid(bay.tiles);
      d = DIRS4[bay.heading] ?? DIRS4[0]!;
    } else {
      // Off the nearest edge, a little more than half its width out, along it.
      const edges = [
        { gap: zone.z, at: new THREE.Vector3(zone.x, 0, -1.4), along: zone.x < s.width / 2 ? -1 : 1, x: true },
        { gap: s.height - zone.z, at: new THREE.Vector3(zone.x, 0, s.height + 1.4), along: zone.x < s.width / 2 ? -1 : 1, x: true },
        { gap: zone.x, at: new THREE.Vector3(-1.4, 0, zone.z), along: zone.z < s.height / 2 ? -1 : 1, x: false },
        { gap: s.width - zone.x, at: new THREE.Vector3(s.width + 1.4, 0, zone.z), along: zone.z < s.height / 2 ? -1 : 1, x: false },
      ];
      const e = edges.reduce((a, b) => (b.gap < a.gap ? b : a));
      at = e.at;
      d = e.x ? { x: e.along, y: 0 } : { x: 0, y: e.along };
    }
    bus.park(at, Math.atan2(d.x, d.y), zone, !s.mission.evacDone);
    // How far it drives to be gone: past the map's edge (or its corner) the way it heads, its whole length.
    const ahead = d.x > 0 ? s.width - at.x : d.x < 0 ? at.x : d.y > 0 ? s.height - at.z : at.z;
    this.busWay = Math.max(0, ahead) + bus.length / 2 + 1;
    this.group.add(bus.root);
  }

  /**
   * A canopy over each filling station's pumps (those within 3 tiles of each
   * other are one station's), its long side along their row, sized to them
   * and the lanes beside them; now and then a burnt one. Drawn on its own, so
   * it can fade (canopies' tick).
   */
  private placeCanopies(s: GameState, pumps: number[]): void {
    this.canopyGroup.clear();
    this.canopies = [];
    const left = new Set(pumps);
    let k = 0;
    while (left.size) {
      const first = left.values().next().value as number;
      const station = [first];
      left.delete(first);
      for (let n = 0; n < station.length; n++) {
        const ax = station[n]! % s.width;
        const ay = Math.floor(station[n]! / s.width);
        for (const j of [...left]) {
          if (Math.max(Math.abs((j % s.width) - ax), Math.abs(Math.floor(j / s.width) - ay)) > 3) continue;
          left.delete(j);
          station.push(j);
        }
      }
      const xs = station.map((i) => i % s.width);
      const ys = station.map((i) => Math.floor(i / s.width));
      const x0 = Math.min(...xs);
      const x1 = Math.max(...xs) + 1;
      const z0 = Math.min(...ys);
      const z1 = Math.max(...ys) + 1;
      const alongX = x1 - x0 > z1 - z0;
      // The model is 8.9 long and 4.7 wide: the row and a lane at each end, never more than three quarters of it.
      const scale = THREE.MathUtils.clamp((Math.max(x1 - x0, z1 - z0) + 2) / 8.9, 0.55, 0.75);
      const parts = syntyProp(hash(x0, z0, 77) < 0.3 ? 'pumpCanopyBurnt' : 'pumpCanopy');
      if (!parts) {
        this.waitingForProps = true;
        continue;
      }
      const material = (this.canopyMaterials[k] ??= this.own(new THREE.MeshStandardMaterial({ map: atlasTexture('apocalypse'), roughness: 0.85 })));
      k++;
      const group = new THREE.Group();
      for (const part of parts) {
        const mesh = new THREE.Mesh(part.geometry, material);
        mesh.matrixAutoUpdate = false;
        mesh.matrix.copy(part.matrix);
        mesh.castShadow = true;
        group.add(mesh);
      }
      group.position.set((x0 + x1) / 2, 0, (z0 + z1) / 2);
      group.rotation.y = alongX ? Math.PI / 2 : 0;
      group.scale.setScalar(scale);
      this.canopyGroup.add(group);
      const halfW = (4.7 * scale) / 2;
      const halfL = (8.9 * scale) / 2;
      const [hx, hz] = alongX ? [halfL, halfW] : [halfW, halfL];
      const cx = (x0 + x1) / 2;
      const cz = (z0 + z1) / 2;
      this.canopies.push({ group, material, x0: cx - hx, z0: cz - hz, x1: cx + hx, z1: cz + hz, shown: 1 });
    }
  }

  /**
   * A canopy fades away while what the player looks at (the selected soldier,
   * the target, the tile under the cursor) is under it, as a building's roof
   * does: the camera never hides the fight.
   */
  private fadeCanopies(dt: number): void {
    if (!this.canopies.length) return;
    const focus = this.focus?.() ?? null;
    const points: THREE.Vector3[] = [];
    const p = new THREE.Vector3();
    if (focus?.selected && unitPosNow(focus.selected, p)) points.push(p.clone());
    if (focus?.target && unitPosNow(focus.target, p)) points.push(p.clone());
    if (focus?.hover) points.push(new THREE.Vector3(focus.hover.x + 0.5, 0, focus.hover.y + 0.5));
    for (const c of this.canopies) {
      const under = points.some((q) => q.x > c.x0 - 0.5 && q.x < c.x1 + 0.5 && q.z > c.z0 - 0.5 && q.z < c.z1 + 0.5);
      const to = under ? 0.15 : 1;
      if (c.shown === to) continue;
      c.shown = to > c.shown ? Math.min(to, c.shown + dt / 0.3) : Math.max(to, c.shown - dt / 0.3);
      c.material.opacity = c.shown;
      const faded = c.shown < 1;
      if (c.material.transparent !== faded) {
        c.material.transparent = faded;
        c.material.depthWrite = !faded;
        c.material.needsUpdate = true;
      }
    }
  }

  /** La Mula goes (the engine's evacDeparted): doors, lights, and away along its heading off the map; resolves once it is gone. */
  busDeparts(): Promise<void> {
    return this.bus && this.busWay !== null ? this.bus.depart(this.busWay) : Promise.resolve();
  }

  /** The loot boxes' marks: those seen and still shut, but not on a storey the cutaway hides. */
  private markLoot(): void {
    const s = this.shown;
    if (!s) return;
    const area = s.width * s.height;
    this.lootMarks.update(this.explored.tiles, (place) => {
      const i = place % area;
      return this.isCutAway({ x: i % s.width, y: Math.floor(i / s.width), z: Math.floor(place / area) });
    });
  }

  /** The storey in view (the active one: game.ts). */
  private activeFloor(): number {
    return this.focus?.()?.level ?? 0;
  }

  /**
   * A mask by place shown on the floor in view, tile by tile (the engine's
   * floorView): on each tile, its value on the highest level at or below the
   * floor in view that has a slab there, else the ground's.
   */
  private onFloor(s: GameState, mask: ArrayLike<boolean | number>): boolean[] {
    const area = s.width * s.height;
    return floorLevels(s, this.fogFloor).map((z, i) => !!mask[z * area + i]);
  }

  /** The fog for the floor in view: what is in sight and explored there, and the height of each tile's floor (for the night's light). */
  private refog(): void {
    const s = this.shown;
    if (!s) return;
    this.fogFloor = this.activeFloor();
    const area = s.width * s.height;
    const levels = floorLevels(s, this.fogFloor);
    this.fog.set(this.onFloor(s, this.visibility ?? []), Uint8Array.from(levels, (z, i) => this.explored.tiles[z * area + i] ?? 0), this.fresh);
    this.fog.setFloors(levels.map((z, i) => (z ? z : (s.elev[i] ?? 0))));
    if (NIGHT.value > 0.5) this.fog.setLit(this.onFloor(s, litMask(s)));
  }

  /**
   * Whether place `p` is hidden by the buildings' cutaway (a storey above the
   * one its building shows): units there aren't to be drawn.
   */
  isCutAway(p: Place): boolean {
    return this.buildings?.cutaway.hides(p) ?? false;
  }

  /** Whether the squad sees place `p` (on the ground without `z`). */
  isVisibleAt(p: Place): boolean {
    const s = this.shown;
    if (!s) return true;
    return this.visibility?.[((p.z ?? 0) * s.height + p.y) * s.width + p.x] ?? true;
  }

  /** Advances the fog's fades and its drifting mist, and the buildings' cutaway; called every frame. */
  tick(dt: number): void {
    // Another storey in view: the fog shows what is seen there.
    if (this.shown && this.visibility && this.activeFloor() !== this.fogFloor) this.refog();
    this.fog.update(dt);
    // A search opened a piece of furniture: redraw (searches are few, a rebuild is cheap).
    if (this.shown && (this.shown.searched?.length ?? 0) !== this.searchedSeen) this.rebuild(this.shown);
    // A siege's door or barricade struck to another stage of wear (the game applies 'smashed' to the state it shows):
    // a door's leaf wears where it hangs (doors.ts); the sandbags and the grit are placed again.
    if (this.shown?.mission.siege) {
      const wear = wearOf(this.shown);
      let key = '';
      for (const [i, stage] of wear) key += `${i}:${stage},`;
      if (key !== this.wearSeen) {
        this.wearSeen = key;
        this.buildings?.wearDoors(wear);
        if ([...wear].some(([i, stage]) => this.drawn?.wear.get(i) !== stage)) this.rebuild(this.shown);
      }
    }
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
    if (this.buildings && this.shown) this.buildings.update(dt, this.terrain ?? this.shown, this.camera, this.focus?.() ?? null, this.sight, this.seeInside);
    // The cutaway moves with the camera: a mark upstairs goes with its storey, and so does what lies on its floor.
    this.markLoot();
    if (this.shown) this.dropped.update(this.shown, (p) => this.isCutAway(p));
    this.fadeCanopies(dt);
  }

  isVisible(x: number, y: number): boolean {
    return this.visibility?.[y * this.width + x] ?? true;
  }

  isExplored(x: number, y: number): boolean {
    return this.explored.tiles[y * this.width + x] === 1;
  }
}
