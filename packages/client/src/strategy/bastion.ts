import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import {
  adjacencyBonuses,
  BASE_SIZE,
  FACILITIES,
  FIXED_BUILDINGS,
  FIXED_NAMES,
  TERRAINS,
  walkways,
  type Appearance,
  type BasePlot,
  type CampaignState,
  type FacilityId,
  type FixedBuildingId,
  type TemplateId,
} from '@bastion/engine';
import { buildUnit, IDLE_POSE, isFigure, type UnitRig } from '../game/models';
import type { Gear } from '../game/figures';
import { easeInOut } from '../game/tween';
import { terrainFile, type BaseModelId } from '../game/syntyManifest';
import { baseTexture, KitBatch, kitMesh, kitModel, loadBaseKit, type Put } from './baseKit';
import type { Shot, StageSet } from './stage';

// The Bastion on the surface, seen from above at an angle: Atalaya, an old
// civil-defence station high in the snow, in Synty's low-poly models. A 7×7
// grid of plots inside a ring of concrete barriers: the bunker's entrance in
// the middle, the command buildings round it and the installations the
// players build next to them (tents, sheds, a radio tower…), joined by board
// walkways. The pines, crags and ruins of the uncleared plots, the woods and
// the range beyond, a dam up the valley. South of the compound, outside the
// grid, the hangar with La Mula, an armoured city bus, and the squad lined up
// in front of it; it leaves through the hangar's side gate.
//
// Every model is loaded once (baseKit.ts) and drawn instanced: a plot is
// placements in a batch, rebuilt whenever the base changes.

/** Side of a plot, and the strip between two plots where the walkways go. */
const PLOT = 5;
const GAP = 1.4;
const PITCH = PLOT + GAP;
const HALF = (BASE_SIZE - 1) / 2;
const COMPOUND = BASE_SIZE * PITCH;
/** Where the compound's barriers stand. */
const EDGE = COMPOUND / 2 + 1.2;
/** How high a plot's name floats, and the tallest of its buildings (for fitting shots). */
const LABEL_Y = 2.4;
const ROOF_Y = 2.8;
/** The hangar, south of the compound, its open front facing the camera. */
const HANGAR = new THREE.Vector3(0, 0, COMPOUND / 2 + 9);
/** Inside of the hangar: width (x), depth (z) and the height of its roof. */
const HANGAR_W = 13.33;
const HANGAR_D = 8;
const HANGAR_H = 4.15;
/** The hangar's floor stands this high. */
const FLOOR = 0.15;
/**
 * La Mula stands at the back of the hangar, nose east (towards the side gate
 * it leaves by), its doors towards the squad lined up at the front, with a
 * gap between the two commanders' halves.
 */
const BUS_X = -0.4;
const BUS_Z = -2.2;
const BUS_SCALE = 1.1;
const PAD_Z = 1.6;
const PAD_STEP = 1.22;
const PAD_SPLIT = 0.7;
/** The side gate's opening in the east wall (z, about BUS_Z) and how high it is. */
const GATE_HALF = 1.65;
const GATE_H = 3.0;
/** Take-off timing: the squad boards one after another, the doors close, the gate rolls up and the bus drives out. */
const BOARD_SECONDS = 0.95;
const BOARD_STAGGER = 0.045;
const DRIVE_ACCEL = 13;
export const LAUNCH_SECONDS = 3.4;
const LINEUP = 8;

/** Rooms with a fixed purpose: each opens one strategy screen. */
export type FixedRoom = FixedBuildingId;

/**
 * The turn that puts each model's front (its door, its open side, its face)
 * towards +z, the south and the camera. Models not listed already face it.
 */
const FACE: Partial<Record<BaseModelId, number>> = {
  bunker: Math.PI,
  armyTent: Math.PI,
  openTent: Math.PI,
  garageShed: Math.PI,
  brickHut: -Math.PI / 2,
  workbench: -Math.PI / 2,
  blackboard: -Math.PI / 2,
  target: -Math.PI / 2,
  targetBoard: -Math.PI / 2,
  bottleRange: -Math.PI / 2,
  gramophone: -Math.PI / 2,
  wallLamp: -Math.PI / 2,
};

// ------------------------------------------------------------- primitives

const mats = new Map<string, THREE.Material>();
function mat(color: number, opts: { emissive?: number; intensity?: number; rough?: number; metal?: number } = {}): THREE.Material {
  const key = JSON.stringify([color, opts]);
  let m = mats.get(key);
  if (!m) {
    m = new THREE.MeshStandardMaterial({
      color,
      emissive: opts.emissive ?? 0,
      emissiveIntensity: opts.intensity ?? (opts.emissive ? 1.6 : 0),
      roughness: opts.rough ?? 0.85,
      metalness: opts.metal ?? 0,
    });
    mats.set(key, m);
  }
  return m;
}

/** A unit box standing on y = 0: primitives scale and place it. */
const UNIT = new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0);

function boxAt(w: number, h: number, d: number, x: number, y: number, z: number, yaw = 0): THREE.Matrix4 {
  return new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), yaw), new THREE.Vector3(w, h, d));
}

function box(w: number, h: number, d: number, material: THREE.Material, x = 0, y = 0, z = 0): THREE.Mesh {
  const mesh = new THREE.Mesh(UNIT, material);
  mesh.scale.set(w, h, d);
  mesh.position.set(x, y, z);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  return mesh;
}

/** Furniture and gear that stays its own colour: it stands under a roof, or snow on it would only read as a white box. */
const BARE = new Set<BaseModelId>([
  'solarPanel', 'workbench', 'longTable', 'batteryTable', 'fuelTable', 'toolShelf', 'bunkBed', 'medShelf', 'gurney', 'hospitalBed', 'ivStand', 'radioSet',
  'blackboard', 'foldingChair', 'campChair', 'ammoCrate', 'toolbox', 'vice', 'anvil', 'medKit', 'gramophone', 'speaker', 'deconShower', 'bottleRange',
]);

/** Glowing bits: lamps, level pips, the walkways' lights. */
const GLOW = {
  amber: () => mat(0x2a1a00, { emissive: 0xffc93a, intensity: 2.2 }),
  red: () => mat(0x2a0a0a, { emissive: 0xff3a3a, intensity: 2.6 }),
  green: () => mat(0x0a2a1a, { emissive: 0x4dffb0, intensity: 2 }),
  white: () => mat(0x2a2a20, { emissive: 0xfff0c8, intensity: 1.8 }),
  fire: () => mat(0x2a1000, { emissive: 0xff8a2a, intensity: 2.6 }),
  coro: () => mat(0x0a2a1a, { emissive: 0x6dffb0, intensity: 1.8 }),
  pip: () => mat(0x2a1a00, { emissive: 0xffc93a, intensity: 1.1 }),
  off: () => mat(0x2a3036),
};
type Glow = keyof typeof GLOW;

let snowPad: THREE.Material | null = null;
let dirtPad: THREE.Material | null = null;

function groundTexture(): THREE.Texture {
  const t = new THREE.TextureLoader().load(`${import.meta.env.BASE_URL}${terrainFile('dirt')}`);
  t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = 4;
  return t;
}
const PAD_MAT = {
  concrete: () => mat(0x8d9094, { rough: 0.95 }),
  /** Levelled earth (the combat maps' dirt). */
  dirt: () => (dirtPad ??= new THREE.MeshStandardMaterial({ map: groundTexture(), color: 0xc8b8a0, roughness: 1 })),
  /** Untouched snow on the plots not cleared yet. */
  snow: () => (snowPad ??= new THREE.MeshStandardMaterial({ map: baseTexture('snowGround', 1), color: 0xe8edf2, roughness: 1 })),
};

function label(text: string, className: string): CSS2DObject {
  const el = document.createElement('div');
  el.className = className;
  el.textContent = text;
  return new CSS2DObject(el);
}

/** Ground centre of plot (x, y); y grows towards the camera (south). */
function plotCentre(x: number, y: number): THREE.Vector3 {
  return new THREE.Vector3((x - HALF) * PITCH, 0, (y - HALF) * PITCH);
}

/** A small deterministic random in [0, 1) for a plot and a draw. */
function rand(x: number, y: number, k: number): number {
  const h = Math.sin(x * 127.1 + y * 311.7 + k * 74.7) * 43758.5453;
  return h - Math.floor(h);
}

/** Ground height: flat round the compound, the hangar and its roads; hills beyond, rising towards the range. */
function groundHeight(x: number, z: number): number {
  const dx = Math.max(0, Math.abs(x) - 34);
  const dz = Math.max(0, z > 0 ? z - 44 : -z - 30);
  let d = Math.hypot(dx, dz);
  // The road east from the hangar's gate stays level.
  const road = Math.abs(z - (HANGAR.z + BUS_Z));
  if (x > 0 && road < 7) d *= Math.max(0, (road - 3) / 4);
  if (d <= 0) return 0;
  const bumps = Math.sin(x * 0.071) * Math.cos(z * 0.063) + Math.sin((x + z) * 0.031) * 0.8;
  const t = Math.min(1, d / 45);
  return 13 * t * t * (3 - 2 * t) + Math.max(0, d - 45) * 0.1 + bumps * Math.min(1, d / 12) * 2.4;
}

/** The dam across the gorge up the valley, and how it is turned. */
const DAM = new THREE.Vector3(76, 0, -122);
const DAM_YAW = -0.6;

// ------------------------------------------------------------- animation

/** Things the tick moves: lamps that blink, flames and glows that pulse, aerials that turn. */
class Fx {
  readonly blink: { o: THREE.Object3D; phase: number }[] = [];
  readonly pulse: { o: THREE.Object3D; phase: number }[] = [];
  readonly spin: { o: THREE.Object3D; speed: number }[] = [];

  clear(): void {
    this.blink.length = this.pulse.length = this.spin.length = 0;
  }

  tick(time: number): void {
    for (const { o, phase } of this.blink) o.visible = Math.sin(time * 3 + phase) > -0.3;
    for (const { o, phase } of this.pulse) o.scale.setScalar(1 + Math.sin(time * 6 + phase) * 0.12);
    for (const { o, speed } of this.spin) o.rotation.y = time * speed;
  }
}

/**
 * One plot (or the hangar, or the land round the compound) being dressed:
 * models and primitives into the batch, under the plot's origin; lamps that
 * blink and flames into `fx`, as meshes of their own.
 */
class Site {
  private readonly origin: THREE.Matrix4;

  constructor(
    private readonly batch: KitBatch,
    private readonly at: THREE.Vector3,
    private readonly group: THREE.Group,
    private readonly fx: Fx,
    yaw = 0,
  ) {
    this.origin = new THREE.Matrix4().compose(at, new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), yaw), new THREE.Vector3(1, 1, 1));
  }

  /** A model, turned so its front looks south when `yaw` is 0. */
  put(id: BaseModelId, o: Put = {}): void {
    this.batch.put(id, { ...o, yaw: (o.yaw ?? 0) + (FACE[id] ?? 0), finish: o.finish ?? (BARE.has(id) ? 'plain' : 'snow') }, this.origin);
  }

  /** A plain box (a pad, a post, a lamp), instanced with the others of its kind. */
  box(kind: string, material: THREE.Material, w: number, h: number, d: number, x: number, y: number, z: number, yaw = 0, shadow = true): void {
    this.batch.prim(kind, UNIT, material, boxAt(w, h, d, x, y, z, yaw).premultiply(this.origin), shadow);
  }

  glow(kind: Glow, x: number, y: number, z: number, size = 0.14): void {
    this.box(`glow:${kind}`, GLOW[kind](), size, size, size, x, y, z, 0, false);
  }

  /** The plot's ground: a concrete pad for buildings, levelled dirt for a cleared plot. */
  pad(kind: keyof typeof PAD_MAT): void {
    this.box(`pad:${kind}`, PAD_MAT[kind](), PLOT, 0.06, PLOT, 0, -0.02, 0, 0, false);
  }

  /** A lamp of its own that blinks (beacons, aviation lights). */
  beacon(kind: Glow, x: number, y: number, z: number, phase = 0, size = 0.16): void {
    const lamp = box(size, size, size, GLOW[kind](), this.at.x + x, this.at.y + y, this.at.z + z);
    lamp.castShadow = false;
    this.group.add(lamp);
    this.fx.blink.push({ o: lamp, phase });
  }

  /** A flame (a fire pit, a stove): a glowing cone that flickers. */
  fire(x: number, y: number, z: number, size = 0.3, kind: Glow = 'fire'): void {
    const flame = new THREE.Mesh(new THREE.ConeGeometry(size * 0.6, size * 1.4, 6).translate(0, size * 0.7, 0), GLOW[kind]());
    flame.position.set(this.at.x + x, this.at.y + y, this.at.z + z);
    this.group.add(flame);
    this.fx.pulse.push({ o: flame, phase: x * 3 + z });
  }

  /** A model of its own that turns (a radar, a dish). */
  spinner(id: BaseModelId, x: number, y: number, z: number, s = 1, speed = 0.8): void {
    const g = kitMesh(id);
    g.scale.setScalar(s);
    const pivot = new THREE.Group();
    pivot.position.set(this.at.x + x, this.at.y + y, this.at.z + z);
    pivot.add(g);
    this.group.add(pivot);
    this.fx.spin.push({ o: pivot, speed });
  }
}

// ------------------------------------------------------------- the plots

/** Lamps on a little sign at the plot's front corner: as many lit as the facility's level. */
function levelPips(k: Site, level: number): void {
  const x = PLOT / 2 - 0.35;
  const z = PLOT / 2 - 0.25;
  k.box('pipPost', mat(0x3a3f45), 0.08, 0.7, 0.08, x, 0, z);
  k.box('pipBoard', mat(0x2a2f35), 0.62, 0.22, 0.08, x, 0.62, z);
  for (let i = 0; i < 3; i++) k.glow(i < level ? 'pip' : 'off', x - 0.2 + i * 0.2, 0.84, z, 0.11);
}

/** The bunker: the concrete entrance of the refuge underground, sandbags at its mouth, a mast and floodlights. */
function bunkerArt(k: Site): void {
  k.put('bunker', { s: 0.66, z: -0.35 });
  for (const x of [-1.55, 1.55]) k.put('sandbagWall', { x, z: 2.05, yaw: Math.PI / 2, s: 0.75 });
  k.put('mast', { x: -2.0, z: -2.0, s: 0.78 });
  k.beacon('red', -2.0, 4.35, -2.0, 0.4);
  k.put('floodlights', { x: 2.05, z: 1.55, s: 0.55, yaw: -0.6 });
  k.put('hatch', { x: 1.75, z: -1.85, s: 0.6 });
  k.beacon('red', 1.75, 0.35, -1.85, 2.1, 0.12);
}

/** The Cuartel: a lean-to with bunks and lockers, the pedestal under its roof. */
function barracksArt(k: Site): void {
  k.put('leanTo', { s: 1.35, z: -0.55 });
  for (const x of [-1.35, 1.35]) k.put('bunkBed', { x, z: -1.6, yaw: Math.PI / 2, s: 0.95 });
  k.put('toolShelf', { x: 0, z: -1.8, s: 0.9 });
  k.put('ammoCrate', { x: 2.0, z: 1.7 });
  k.put('ammoCrate', { x: 2.05, z: 1.7, y: 0.2, yaw: 0.4 });
  k.put('supplyCrate', { x: -2.0, z: 1.75, yaw: 0.3 });
}

/** The Expediente: the doctor's trailer with a dish on its roof, a desk and a blackboard outside. */
function researchArt(k: Site): void {
  k.put('trailer', { fit: 4.7, z: -0.95 });
  k.spinner('dish', -1.4, 1.95, -1.0, 1.2, 0.35);
  k.put('blackboard', { x: 1.7, z: 1.35, s: 1.1 });
  k.put('longTable', { x: -0.7, z: 1.55, yaw: Math.PI / 2 });
  k.put('radioSet', { x: -1.2, z: 1.55, y: 0.47 });
  k.put('medKit', { x: -0.3, z: 1.6, y: 0.47 });
  k.put('foldingChair', { x: -0.7, z: 2.15, yaw: Math.PI, s: 0.8 });
  k.put('powerGenerator', { x: 2.0, z: -2.0, yaw: 0.4 });
}

/** The Armería: a brick hut with a workbench, an anvil and crates of ammunition out front. */
function engineeringArt(k: Site): void {
  k.put('brickHut', { s: 0.95, z: -0.9 });
  k.put('workbench', { x: -1.45, z: 1.35, s: 0.9 });
  k.put('vice', { x: -1.25, z: 1.25, y: 0.6 });
  k.put('anvil', { x: 0.35, z: 1.5 });
  k.put('ammoCrate', { x: 1.5, z: 1.3 });
  k.put('ammoCrate', { x: 1.85, z: 1.45, yaw: 0.5 });
  k.put('bigCrate', { x: 2.0, z: 2.0, s: 0.8 });
  k.beacon('amber', -0.6, 0.62, 1.35, 1.1, 0.1);
}

type Art = (k: Site, level: number) => void;

/**
 * What each installation looks like: a recognisable Synty building or set,
 * with more round it (or a bigger one) at levels 2 and 3.
 */
const FACILITY_ART: Record<string, Art> = {
  // Barracón: army tents, a fire pit, washing out to dry.
  quarters: (k, level) => {
    k.put('armyTent', { x: -0.5, z: -0.55, s: 0.9 });
    k.put('firePit', { x: 1.6, z: 1.6, s: 0.9 });
    k.fire(1.6, 0.1, 1.6, 0.28);
    k.put('woodPile', { x: 1.9, z: 0.45, yaw: Math.PI / 2 });
    if (level >= 2) {
      k.put('fieldTent', { x: 1.95, z: -1.25, yaw: Math.PI / 2, s: 0.82 });
      k.put('washingLine', { x: -1.55, z: 1.7, s: 0.75 });
    }
    if (level >= 3) {
      k.put('fieldTent', { x: -0.3, z: 1.95, s: 0.8 });
      for (const [x, z, yaw] of [
        [1.0, 2.0, 0.6],
        [2.2, 2.05, -0.5],
      ] as const)
        k.put('campChair', { x, z, yaw: yaw + Math.PI });
    }
  },
  // Enfermería: a quarantine tent, drips and stretchers; a decontamination shower and the red cross later.
  infirmary: (k, level) => {
    k.put('quarantineTent', { fit: 4.6, z: -0.7 });
    k.put('ivStand', { x: -1.9, z: 1.5 });
    k.put('medKit', { x: -1.3, z: 1.6, yaw: 0.4 });
    if (level >= 2) {
      k.put('gurney', { x: 0.3, z: 1.75 });
      k.put('medShelf', { x: -2.05, z: -2.2, s: 0.9 });
    }
    if (level >= 3) {
      k.put('deconShower', { x: 1.85, z: 1.7, s: 0.85 });
      k.put('ivStand', { x: -0.7, z: 1.4 });
      // The red cross over the door.
      k.box('cross', GLOW.red(), 0.12, 0.5, 0.05, 0, 1.25, 0.95, 0, false);
      k.box('cross', GLOW.red(), 0.5, 0.12, 0.05, 0, 1.44, 0.95, 0, false);
    }
  },
  // Taller: a shipping container for stores, a workbench and tyres; a second container stacked on top at level 3.
  workshop: (k, level) => {
    k.put('shipContainer', { z: -1.45 });
    k.put('workbench', { x: -1.3, z: 0.75, s: 0.9 });
    k.put('vice', { x: -1.1, z: 0.65, y: 0.6 });
    k.put('toolbox', { x: -0.5, z: 0.95 });
    k.put('tyres', { x: 1.75, z: 1.4 });
    if (level >= 2) {
      k.put('powerGenerator', { x: 1.75, z: 0.1, yaw: -0.3 });
      k.put('batteryTable', { x: 0.2, z: 1.75 });
      k.beacon('amber', -1.5, 0.62, 0.75, 1.6, 0.1);
    }
    if (level >= 3) {
      k.put('shipContainer', { z: -1.5, y: 1.73, yaw: 0.06, s: 0.97 });
      k.put('floodlights', { x: -1.9, z: 2.0, s: 0.5, yaw: 0.5 });
      k.put('fuelTable', { x: 1.0, z: 2.1, s: 0.7, yaw: Math.PI / 2 });
    }
  },
  // Centro de entrenamiento: an obstacle course of tyres, beams and sandbags; a climbing frame and a lookout tower later.
  training: (k, level) => {
    k.put('tyreWall', { z: -1.8, yaw: Math.PI / 2 });
    k.put('balanceBeam', { x: -1.0, z: 0.2 });
    for (const z of [0.0, 1.5]) k.put('sandbagWall', { x: 1.5, z, yaw: Math.PI / 2, s: 0.7 });
    k.put('supplyCrate', { x: -2.0, z: 2.0 });
    if (level >= 2) k.put('climbFrame', { x: -1.3, z: 1.6, s: 0.9 });
    if (level >= 3) k.put('lookout', { x: 1.6, z: -1.2, s: 0.72 });
  },
  // Campo de tiro: targets in front of a snow berm, a firing line of sandbags, bottles on drums.
  range: (k, level) => {
    k.put('drift', { z: -2.05, s: 2.4, sx: 1.7, sy: 1.3, finish: 'snowbank' });
    const n = level + 1;
    for (let i = 0; i < n; i++) k.put(i % 2 ? 'targetBoard' : 'target', { x: n === 1 ? 0 : -1.6 + (3.2 * i) / (n - 1), z: -1.25 });
    for (const x of [-1.3, 0, 1.3]) k.put('sandbagWall', { x, z: 1.75, yaw: Math.PI / 2, s: 0.8 });
    k.put('ammoCrate', { x: 2.05, z: 2.2 });
    if (level >= 2) k.put('bottleRange', { x: -2.0, z: 0.1, s: 0.8 });
    if (level >= 3) k.put('floodlights', { x: 2.0, z: 0.4, s: 0.5, yaw: Math.PI * 0.85 });
  },
  // Brigada de obras: scaffolding, pallets of blocks, a wheelbarrow and cones; a site hut and a tractor as it grows.
  crew: (k, level) => {
    k.put('scaffold', { z: -1.6 });
    k.put('scaffoldBoards', { z: -1.6, y: 1.9, s: 0.95 });
    k.put('blockPallet', { x: 1.7, z: 0.35 });
    k.put('blockPallet', { x: 1.7, z: 1.5, yaw: 0.2 });
    k.put('wheelbarrow', { x: -1.5, z: 1.95, yaw: 0.3 });
    for (const [x, z] of [
      [0.2, 1.9],
      [0.5, 2.1],
      [0.0, 2.2],
    ] as const)
      k.put('cinderblock', { x, z, yaw: x * 3 });
    k.put('bucket', { x: -0.4, z: 0.8 });
    k.put('roadCone', { x: -2.15, z: 2.2 });
    k.put('roadCone', { x: 2.2, z: 2.25 });
    if (level >= 2) {
      k.put('shackFrame', { x: -1.65, z: -0.1, s: 0.85 });
      k.put('shackTarp', { x: -1.65, z: -0.1, y: 0.85 * 0.8, s: 0.85 });
      k.put('pallet', { x: 0.4, z: 0.4 });
      k.put('pallet', { x: 0.4, z: 0.4, y: 0.16, yaw: 0.15 });
    }
    if (level >= 3) k.put('tractor', { x: 0.1, z: 0.8, yaw: -1.3, s: 0.9 });
  },
  // Sala de simulación: an open army tent with long tables, chairs and a blackboard.
  simulation: (k, level) => {
    k.put('openTent', { fit: 4.8, z: -0.1 });
    const tables = level + 1;
    for (let i = 0; i < tables; i++) {
      const x = -1.4 + (i % 2) * 2.0;
      const z = -0.9 + Math.floor(i / 2) * 1.6;
      k.put('longTable', { x, z, yaw: Math.PI / 2 });
      k.put('foldingChair', { x, z: z + 0.6, yaw: Math.PI, s: 0.8 });
    }
    k.put('blackboard', { x: 1.6, z: -1.3, s: 1.1, yaw: 0.3 });
    k.put('radioSet', { x: -1.7, z: -0.9, y: 0.47 });
    if (level >= 3) k.put('powerGenerator', { x: 2.05, z: 2.05, yaw: 0.3 });
  },
  // Taller de La Mula: a garage with its door to the south, tyres and drums; a car being stripped and a fuel pump later.
  garage: (k, level) => {
    k.put('garageShed', { s: 0.74, x: -0.65, z: -0.4 });
    k.put('tyres', { x: 1.85, z: 1.55, yaw: Math.PI / 2 });
    k.put('oilDrum', { x: 1.95, z: -1.9 });
    k.put('oilDrum', { x: 1.45, z: -2.05 });
    k.put('jerrycan', { x: 1.9, z: -1.3, yaw: 0.6 });
    if (level >= 2) k.put('rustyCar', { x: 1.75, z: -0.15, yaw: Math.PI / 2, s: 0.85 });
    if (level >= 3) {
      k.put('fuelPump', { x: -2.0, z: 2.0, s: 0.9 });
      k.put('batteryTable', { x: 0.6, z: 2.0, s: 0.85 });
    }
  },
  // Estación de escucha: a lattice mast, a radio desk under a tarp; a second aerial and dishes later.
  listening: (k, level) => {
    k.put('mast', { x: 1.45, z: -1.35, s: 0.88 });
    k.beacon('red', 1.45, 0.88 * 5.5, -1.35, 0.9);
    k.put('shackFrame', { x: -1.05, z: 0.1, s: 0.9 });
    k.put('shackTarp', { x: -1.05, z: 0.1, y: 0.9 * 0.8, s: 0.9 });
    k.put('longTable', { x: -1.05, z: 0.1 });
    k.put('radioSet', { x: -1.05, z: -0.2, y: 0.47, yaw: Math.PI / 2 });
    k.glow('amber', -1.05, 0.62, 0.35, 0.08);
    k.put('campChair', { x: -0.45, z: 0.3, yaw: -Math.PI / 2 });
    if (level >= 2) k.put('aerial', { x: -1.85, z: -1.85, s: 0.75 });
    if (level >= 3) {
      k.spinner('dish', 1.7, 0, 1.3, 1.6, 0.3);
      k.put('solarPanel', { x: 0.4, z: 2.0, yaw: Math.PI / 2 });
    }
  },
  // Centro de comunicaciones: a radio tower with its turning radar; solar panels and floodlights later.
  relay: (k, level) => {
    k.put('radioTower', { x: 0.2, z: -0.5 });
    k.spinner('radioTowerTop', 0.2, 4.5, -0.5, 1, 0.6);
    k.beacon('red', 0.2, 5.5, -0.5, 1.7);
    k.put('powerBoxes', { x: -2.1, z: -1.8 });
    k.put('transformer', { x: 1.75, z: 1.45, s: 0.7 });
    k.put('powerGenerator', { x: -1.55, z: 1.5, yaw: 0.2 });
    if (level >= 2) for (const x of [-0.5, 0.6]) k.put('solarPanel', { x, z: 2.0, yaw: Math.PI / 2 });
    if (level >= 3) {
      k.put('floodlights', { x: 1.9, z: -1.8, s: 0.5, yaw: 2.4 });
      k.spinner('dish', -1.8, 0, -0.3, 1.5, -0.4);
    }
  },
  // Taller de oficios: a craft shed with a workbench and wood stacked; a lean-to and the Canto's gramophone later.
  echoes: (k, level) => {
    k.put('gardenShed', { s: 1.3, x: -1.25, z: -1.05 });
    k.put('workbench', { x: 1.05, z: -1.4, s: 0.9 });
    k.put('woodPile', { x: 1.85, z: 1.1, yaw: Math.PI / 2 });
    k.put('woodPile', { x: 1.85, z: 1.85, yaw: Math.PI / 2 });
    k.put('anvil', { x: 0.2, z: 0.6 });
    if (level >= 2) k.put('leanTo', { x: 1.15, z: -1.0, s: 0.75 });
    if (level >= 3) {
      k.put('gramophone', { x: -1.5, z: 1.3 });
      k.glow('coro', -1.32, 0.9, 1.3, 0.1);
      for (const x of [-2.15, -0.85]) k.put('speaker', { x, z: 1.0 });
      k.put('campfire', { x: 0.1, z: 1.8 });
      k.fire(0.1, 0.05, 1.8, 0.24, 'coro');
    }
  },
  // Camposanto: a fenced patch of snow with rows of crosses; a bench and a lamp, and stone crosses later.
  memorial: (k, level) => {
    const half = PLOT / 2 - 0.25;
    for (let t = -half + 0.84; t < half; t += 1.68) {
      k.put('picketFence', { x: t, z: -half, yaw: Math.PI / 2, sy: 0.42 });
      k.put('picketFence', { x: -half, z: t, sy: 0.42 });
      k.put('picketFence', { x: half, z: t, sy: 0.42 });
    }
    const rows = level >= 3 ? 4 : level >= 2 ? 3 : 2;
    for (let r = 0; r < rows; r++) {
      for (let i = 0; i < 4; i++) {
        const x = -1.5 + i * 1.0;
        const z = -1.5 + r * 1.0;
        k.put('drift', { x, z: z + 0.25, s: 0.55, finish: 'snowbank' });
        k.put(r === 3 ? 'stoneCross' : (i + r) % 3 ? 'cross' : 'graveStick', { x, z, s: r === 3 ? 2.2 : (i + r) % 3 ? 2.6 : 1.2 });
      }
    }
    if (level >= 2) {
      k.put('bench', { x: 1.3, z: 2.05 });
      k.put('lampPost', { x: -1.9, z: 2.0, s: 0.55 });
      k.glow('amber', -1.9, 1.75, 2.0, 0.16);
    }
    if (level >= 3) for (const x of [-1.0, 0.0, 1.0]) k.glow('fire', x, 0.15, 2.1, 0.07);
  },
};

/** Rocks, pines or the ruins of the old station on an uncleared plot (variant 0–2). */
function terrainArt(k: Site, terrain: BasePlot['terrain'], variant: number, px: number, py: number): void {
  const r = (n: number) => rand(px, py, n);
  const spot = (n: number, spread = 0.75) => ({ x: (r(n) - 0.5) * PLOT * spread, z: (r(n + 50) - 0.5) * PLOT * spread });
  if (terrain !== 'clear') k.pad('snow');
  switch (terrain) {
    case 'rocks': {
      const crags: BaseModelId[] = ['crag', 'crag2', 'crag3', 'crag4'];
      k.put(crags[variant]!, { ...spot(1, 0.3), yaw: r(2) * 6.3, s: 0.75 + r(3) * 0.25, finish: 'stone' });
      k.put(crags[(variant + 2) % 4]!, { ...spot(4, 0.6), yaw: r(5) * 6.3, s: 0.45 + r(6) * 0.2, finish: 'stone' });
      for (let i = 0; i < 4; i++) k.put('stone', { ...spot(10 + i, 0.85), yaw: r(20 + i) * 6.3, s: 0.6 + r(30 + i) * 0.7, finish: 'stone' });
      k.put('drift', { ...spot(40, 0.8), yaw: r(41) * 6.3, s: 1.2, finish: 'snowbank' });
      break;
    }
    case 'forest': {
      const kinds: BaseModelId[] = ['pine', 'pineSmall', 'pineTall', 'pine', 'pineSmall', 'pine'];
      const n = 5 + variant;
      for (let i = 0; i < n; i++) {
        const a = (i / n) * Math.PI * 2 + r(i) * 0.8;
        const d = i === 0 ? 0 : 1.2 + r(i + 7) * 0.9;
        k.put(kinds[(i + variant) % kinds.length]!, { x: Math.cos(a) * d, z: Math.sin(a) * d, yaw: r(i + 3) * 6.3, s: 0.7 + r(i + 11) * 0.35 });
      }
      k.put('stump', { ...spot(60), s: 1.2 });
      k.put('drift', { ...spot(61), yaw: r(62) * 6.3, s: 1.1, finish: 'snowbank' });
      break;
    }
    case 'ruins': {
      // What is left of the old station's huts: broken brick walls, rubble, a burnt heap, a wreck.
      k.put('brokenWall', { x: -1.6, z: -0.6, yaw: r(1) * 0.4 });
      k.put('brokenWall', { x: 0.2, z: -1.9, yaw: Math.PI / 2 + r(2) * 0.4, s: 0.9 });
      k.put('brickPile', { x: -0.9, z: 0.9, yaw: r(3) * 6.3 });
      k.put('brickPile', { x: 1.4, z: -0.9, yaw: r(4) * 6.3, s: 0.8 });
      k.put('rubbleHeap', { x: 0.6, z: 0.5, yaw: r(5) * 6.3, s: 0.9 });
      if (variant === 0) k.put('burntHeap', { x: 0.7, z: 1.4, yaw: r(6) * 6.3, s: 0.5 });
      if (variant === 1) k.put('rustyCar', { x: 0.9, z: 1.3, yaw: 0.3 + r(7), s: 0.75 });
      if (variant === 2) {
        k.put('shackFrame', { x: 1.3, z: 0.9, yaw: r(8) * 0.6, s: 0.85 });
        k.put('junkHeap', { x: -0.3, z: 1.6, s: 0.4, yaw: r(9) * 6.3 });
      }
      k.put('drift', { ...spot(70), yaw: r(71) * 6.3, s: 1.0, finish: 'snowbank' });
      break;
    }
    case 'clear':
      // Levelled, and staked out for building.
      k.pad('dirt');
      for (const [x, z] of [
        [-1, -1],
        [1, -1],
        [-1, 1],
        [1, 1],
      ] as const)
        k.put('roadCone', { x: x * (PLOT / 2 - 0.3), z: z * (PLOT / 2 - 0.3), s: 0.85 });
      break;
  }
}

/** Clearing a plot: a tractor working, a heap of spoil and a beacon. */
function clearingArt(k: Site): void {
  k.put('tractor', { x: -0.7, z: 1.1, yaw: -0.9 });
  k.put('rubbleHeap', { x: 1.3, z: 1.4, s: 0.8 });
  k.beacon('amber', -0.7, 1.55, 1.1, 2);
}

/** A facility going up: its slab rising with the work, scaffolding, pallets of blocks, cones and a progress strip. */
function constructionArt(k: Site, progress: number): void {
  const p = Math.max(0.08, progress);
  k.pad('dirt');
  k.put('slab', { z: -0.3, s: 1.3, sy: p });
  k.put('scaffold', { z: -1.9, s: 0.95 });
  k.put('blockPallet', { x: 1.75, z: 1.4 });
  k.put('pallet', { x: -1.8, z: 1.7 });
  k.put('roadCone', { x: -2.15, z: 2.2 });
  k.put('roadCone', { x: 2.2, z: 2.25 });
  progressStrip(k, progress);
  k.beacon('amber', -2.2, 2.0, -1.9, 0);
}

/** Being raised a level: scaffolding over the building, which keeps working. */
function upgradeArt(k: Site, progress: number): void {
  k.put('scaffold', { z: -2.1, s: 1.05 });
  k.put('scaffold', { x: -2.15, yaw: Math.PI / 2, s: 1.0 });
  k.put('scaffoldBoards', { z: -2.1, y: 2.0, s: 1.0 });
  progressStrip(k, progress);
  k.beacon('amber', 2.2, 2.2, -2.2, 0);
}

/** A strip of light along the plot's front, as long as the work is done. */
function progressStrip(k: Site, progress: number): void {
  const p = Math.max(0.05, progress);
  k.box('progress', GLOW.amber(), (PLOT - 0.4) * p, 0.06, 0.1, -(PLOT - 0.4) / 2 + ((PLOT - 0.4) * p) / 2, 0.02, PLOT / 2 - 0.05, 0, false);
}

/** Names over the map, short where the full one would run into the neighbours' (the panels and the tooltip give it whole). */
const SHORT_NAMES: Partial<Record<FacilityId, string>> = {
  training: 'Entrenamiento',
  relay: 'Comunicaciones',
  simulation: 'Simulación',
  garage: 'La Mula',
  listening: 'Escucha',
  crew: 'Brigada',
};

/** Invisible box over a plot: what the pointer actually hits. */
function hotspot(id: string, origin: THREE.Vector3): THREE.Mesh {
  const hit = new THREE.Mesh(new THREE.BoxGeometry(PLOT, 2.4, PLOT), new THREE.MeshBasicMaterial({ visible: false }));
  hit.position.copy(origin).add(new THREE.Vector3(0, 1.2, 0));
  hit.userData.hotspot = id;
  return hit;
}

/** A square frame round a plot, flat on the ground: the selected or the hovered one. */
function plotFrame(color: number): THREE.Group {
  const g = new THREE.Group();
  const m = new THREE.MeshBasicMaterial({ color, transparent: true, toneMapped: false });
  const side = PLOT + 0.5;
  const w = 0.16;
  for (const [x, z, sx, sz] of [
    [0, -side / 2, side + w, w],
    [0, side / 2, side + w, w],
    [-side / 2, 0, w, side],
    [side / 2, 0, w, side],
  ] as const) {
    const bar = new THREE.Mesh(new THREE.BoxGeometry(sx, 0.05, sz), m);
    bar.position.set(x, 0.05, z);
    g.add(bar);
  }
  return g;
}

export interface LineupEntry {
  template: TemplateId;
  color: string;
  appearance?: Appearance;
  gear?: Gear;
  /** Debrief pose: wounded soldiers slump, the fallen leave a memorial. */
  status?: 'ok' | 'wounded' | 'dead';
  /** Picks the same Synty survivor everywhere for this soldier (their campaign id). */
  seed?: string;
}

/** The bus's moving parts, found by their mesh names once it has loaded. */
interface BusRig {
  root: THREE.Group;
  doors: { leaf: THREE.Object3D; shut: THREE.Vector3; open: THREE.Vector3 }[];
  wheels: THREE.Object3D[];
  wheelRadius: number;
  lights: THREE.MeshStandardMaterial;
  /** Where the squad boards, in the hangar's space: the floor outside each door and a point inside. */
  boarding: { foot: THREE.Vector3; inside: THREE.Vector3 }[];
}

/**
 * The Bastion as a compound on the surface. Every building and plot is a
 * hotspot that opens its screen (`command`, `research`, `engineering`,
 * `barracks`, `plot:x,y`, `hangar`); the hangar holds the squad lineup and
 * the barracks a pedestal for the soldier being inspected.
 */
export class BastionSet implements StageSet {
  readonly scene = new THREE.Scene();
  readonly ambientOcclusion = true;
  private readonly hotspots: THREE.Object3D[] = [];
  private readonly grid = new THREE.Group();
  private readonly lineup = new THREE.Group();
  private readonly showcase = new THREE.Group();
  private readonly frame: THREE.Group;
  private readonly hoverFrame: THREE.Group;
  private readonly partnerMarker = new THREE.Group();
  /** Lamps, flames and aerials of the land round the compound and of the plots. */
  private readonly shellFx = new Fx();
  private readonly gridFx = new Fx();
  private bus: BusRig | null = null;
  private gate: THREE.Object3D | null = null;
  private boom: THREE.Object3D | null = null;
  private readonly rigs: UnitRig[] = [];
  private ready = false;
  private base: CampaignState | null = null;
  private gridKey = '';
  private lineupKey = '';
  private showcaseKey = '';
  private showcaseRig: UnitRig | null = null;
  private partnerKey = '';
  private launching = 0;
  /** Each plot's label, by hotspot id: terrain labels only show on the hovered or selected plot. */
  private readonly labels = new Map<string, HTMLElement>();
  private hovered: string | null = null;
  private selected: string | null = null;
  /** Whether the names show (overview shots): plots rebuilt later (the models load late) follow it. */
  private labelsOn = true;

  constructor(private readonly onPick: (hotspot: string) => void) {
    // A clear winter afternoon in the mountains.
    this.scene.background = new THREE.Color(0x92b4d4);
    this.scene.fog = new THREE.FogExp2(0xbccbd8, 0.0034);
    this.scene.add(new THREE.HemisphereLight(0xd6e2f5, 0x7a7468, 0.95));
    const sun = new THREE.DirectionalLight(0xfff0dc, 1.9);
    sun.position.set(-26, 42, 30);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    sun.shadow.bias = -0.0004;
    sun.shadow.normalBias = 0.03;
    Object.assign(sun.shadow.camera, { left: -42, right: 42, top: 42, bottom: -42, near: 1, far: 150 });
    this.scene.add(sun, sun.target);
    this.scene.add(this.grid, this.lineup, this.showcase, this.partnerMarker);
    this.frame = plotFrame(0x54d1ff);
    this.hoverFrame = plotFrame(0xffc93a);
    this.frame.visible = this.hoverFrame.visible = false;
    this.scene.add(this.frame, this.hoverFrame);
    // The hangar's hotspot and label, and the pedestal, need no models.
    const hangarHit = new THREE.Mesh(new THREE.BoxGeometry(HANGAR_W, HANGAR_H, HANGAR_D), new THREE.MeshBasicMaterial({ visible: false }));
    hangarHit.position.copy(HANGAR).add(new THREE.Vector3(0, HANGAR_H / 2, 0));
    hangarHit.userData.hotspot = 'hangar';
    this.hotspots.push(hangarHit);
    const hangarLabel = label('Hangar', 'room-label fixed');
    hangarLabel.position.copy(HANGAR).add(new THREE.Vector3(0, HANGAR_H + 0.5, HANGAR_D / 2));
    this.scene.add(hangarHit, hangarLabel);
    this.buildPedestal();
    this.buildGround();
    void loadBaseKit().then(() => {
      this.ready = true;
      this.buildShell();
      if (this.base) {
        this.gridKey = '';
        this.setBase(this.base);
      }
      // The boarding lane depends on the bus's doors.
      const entries = this.lineupKey;
      this.lineupKey = '';
      if (entries) this.setLineup(JSON.parse(entries) as (LineupEntry | null)[]);
    });
  }

  // ------------------------------------------------------------- shots

  /** The whole compound from above at an angle, the hangar at the bottom; the wheel zooms in. */
  overview(): Shot {
    const target = new THREE.Vector3(0, 0, 9);
    return { pos: target.clone().add(new THREE.Vector3(0, 58, 44)), target, fov: 40, zoom: { min: 0.4, reach: COMPOUND / 2 } };
  }

  /** Close-up of a building (or a plot), from above its door. */
  room(id: FixedRoom | string): Shot {
    const target = this.roomCentre(id).add(new THREE.Vector3(0, 0.6, 0.4));
    return { pos: target.clone().add(new THREE.Vector3(0, 9.6, 6.4)), target, fov: 40 };
  }

  /** The squad lined up in the hangar, La Mula behind them; far enough back that a full lineup fits a narrow screen too. */
  hangar(): Shot {
    const aspect = Math.max(1, window.innerWidth / Math.max(1, window.innerHeight));
    const back = Math.max(9.2, 5.5 / (Math.tan(THREE.MathUtils.degToRad(21)) * aspect));
    const target = HANGAR.clone().add(new THREE.Vector3(0, 1.5, 0.3));
    return { pos: HANGAR.clone().add(new THREE.Vector3(0, 2.75, PAD_Z + back)), target, fov: 42 };
  }

  /**
   * The grid of plots, closer than the overview: the whole compound (to its
   * barriers) framed between the panels, and the wheel zooms in on a corner.
   */
  facilities(): Shot {
    const target = new THREE.Vector3(0, 0, 0);
    const edge = COMPOUND / 2 + 1.5;
    const fit = new THREE.Box3(new THREE.Vector3(-edge, 0, -edge), new THREE.Vector3(edge, ROOF_Y * 0.5, edge));
    return { pos: new THREE.Vector3(0, 46, 34), target, fov: 40, fit, zoom: { min: 0.35, reach: COMPOUND / 2 } };
  }

  /** Low on the apron, south-east of the hangar: the squad boards and La Mula drives out of the side gate past the camera. */
  takeoff(): Shot {
    const target = HANGAR.clone().add(new THREE.Vector3(4.0, 1.3, BUS_Z + 0.8));
    return { pos: HANGAR.clone().add(new THREE.Vector3(12.5, 3.2, 14)), target, fov: 52 };
  }

  /** The soldier on the barracks pedestal, seen under the lean-to's roof. */
  armory(): Shot {
    const target = this.pedestalPos().add(new THREE.Vector3(0, 1.0, 0));
    return { pos: target.clone().add(new THREE.Vector3(0, 2.4, 4.3)), target, fov: 38 };
  }

  private roomCentre(id: string): THREE.Vector3 {
    if (id in FIXED_BUILDINGS) {
      const p = FIXED_BUILDINGS[id as FixedBuildingId];
      return plotCentre(p.x, p.y);
    }
    const m = /^(?:plot:)?(\d+),(\d+)$/.exec(id);
    return m ? plotCentre(Number(m[1]), Number(m[2])) : new THREE.Vector3();
  }

  private pedestalPos(): THREE.Vector3 {
    const p = FIXED_BUILDINGS.barracks;
    return plotCentre(p.x, p.y).add(new THREE.Vector3(0, 0.16, 0.7));
  }

  // ------------------------------------------------------------- shell

  /** The barracks' pedestal for the soldier being inspected (the lean-to round it comes with the plots). */
  private buildPedestal(): void {
    const top = this.pedestalPos().y + 0.25;
    const pedestal = new THREE.Mesh(new THREE.CylinderGeometry(0.55, 0.6, top, 20), mat(0x2a3036, { metal: 0.5, rough: 0.5 }));
    pedestal.position.y = top / 2;
    pedestal.castShadow = pedestal.receiveShadow = true;
    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.56, 0.03, 6, 32).rotateX(Math.PI / 2), mat(0x0a1a2a, { emissive: 0x54d1ff, intensity: 1.1 }));
    ring.position.y = top + 0.01;
    const stand = new THREE.Group();
    stand.add(pedestal, ring);
    stand.position.copy(this.pedestalPos()).setY(0);
    this.scene.add(stand);
    this.showcase.position.copy(this.pedestalPos()).add(new THREE.Vector3(0, 0.25, 0));
  }

  /** Snow all round, rolling up into hills; the compound's trodden yard and the roads (no models: shown at once). */
  private buildGround(): void {
    const size = 420;
    const geo = new THREE.PlaneGeometry(size, size, 84, 84).rotateX(-Math.PI / 2);
    const pos = geo.getAttribute('position');
    for (let i = 0; i < pos.count; i++) pos.setY(i, groundHeight(pos.getX(i), pos.getZ(i)) - 0.02);
    geo.computeVertexNormals();
    const snow = new THREE.Mesh(geo, new THREE.MeshStandardMaterial({ map: baseTexture('snowGround', 48), color: 0xe2e8ee, roughness: 1 }));
    snow.receiveShadow = true;
    this.scene.add(snow);
    const trodden = (w: number, d: number, x: number, z: number, tint: number, repeat: number) => {
      const t = baseTexture('snowGround', 1);
      t.repeat.set((w / size) * repeat, (d / size) * repeat);
      const m = new THREE.Mesh(new THREE.PlaneGeometry(w, d).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ map: t, color: tint, roughness: 1, polygonOffset: true, polygonOffsetFactor: -1 }));
      m.position.set(x, 0.005, z);
      m.receiveShadow = true;
      this.scene.add(m);
    };
    // The yard inside the barriers, trodden grey; the road from the gate to the hangar and on east, ploughed darker.
    trodden(COMPOUND + 3, COMPOUND + 3, 0, 0, 0x99978f, 160);
    trodden(4.5, HANGAR.z - EDGE, 0, EDGE + (HANGAR.z - EDGE) / 2, 0x85837d, 160);
    trodden(22, 13, HANGAR.x, HANGAR.z + 1.5, 0x8c8a84, 160);
    trodden(110, 4.6, HANGAR.x + HANGAR_W / 2 + 55, HANGAR.z + BUS_Z, 0x85837d, 160);
  }

  /** The perimeter, the woods, the range and the dam, the hangar and La Mula (once the models have loaded). */
  private buildShell(): void {
    const batch = new KitBatch();
    const shell = new THREE.Group();
    const land = new Site(batch, new THREE.Vector3(), shell, this.shellFx);
    // What stands beyond the sun's shadow box goes in a batch of its own that casts none (an instanced mesh is culled whole).
    const farBatch = new KitBatch();
    const far = new Site(farBatch, new THREE.Vector3(), shell, this.shellFx);

    // Concrete barriers round the compound, a gate to the south, a lookout at each corner.
    for (let t = -EDGE + 0.8; t < EDGE; t += 1.6) {
      if (Math.abs(t) > 2.6) land.put('jersey', { x: t, z: EDGE });
      land.put('jersey', { x: t, z: -EDGE });
      land.put('jersey', { x: -EDGE, z: t, yaw: Math.PI / 2 });
      land.put('jersey', { x: EDGE, z: t, yaw: Math.PI / 2 });
    }
    for (const sx of [-1, 1])
      for (const sz of [-1, 1]) {
        land.put('lookout', { x: sx * (EDGE + 1.6), z: sz * (EDGE + 1.6), yaw: sx * sz > 0 ? 0 : Math.PI / 2 });
        land.beacon('white', sx * (EDGE + 1.6), 3.95, sz * (EDGE + 1.6), sx + sz * 2, 0.2);
      }
    for (const x of [-3.1, 3.1]) {
      land.put('lampPost', { x, z: EDGE + 0.6, s: 0.8 });
      land.glow('white', x, 2.5, EDGE + 0.6, 0.18);
    }
    land.put('sandbagArc', { x: -5.5, z: EDGE + 1.8, yaw: Math.PI / 2, s: 0.8 });

    // The water tower and the station's stores outside the barriers, pylons carrying the line up the valley.
    land.put('waterTower', { x: -EDGE - 7.5, z: -6, s: 0.85 });
    land.put('drums', { x: -EDGE - 4.5, z: -1.5 });
    land.put('bigCrate', { x: -EDGE - 5.5, z: -3.5, yaw: 0.4 });
    for (const [x, z, yaw] of [
      [EDGE + 14, -12, 0.3],
      [EDGE + 30, -46, 0.45],
      [EDGE + 48, -82, 0.5],
    ] as const)
      far.put('pylon', { x, z, y: groundHeight(x, z) - 0.5, yaw, s: 0.55 });

    this.buildWoods(land, far);
    this.buildRange(far);
    this.buildDam(shell, far);
    this.buildHangar(batch, shell);

    shell.add(batch.build(), farBatch.build(false));
    this.scene.add(shell);
  }

  /** Pines on the hills all round, crags among them, keeping the compound, the hangar and the roads clear. */
  private buildWoods(land: Site, far: Site): void {
    const near = (x: number, z: number) => (Math.max(Math.abs(x), Math.abs(z - 4)) < 44 ? land : far);
    const kinds: BaseModelId[] = ['pine', 'pineSmall', 'pineTall', 'pine', 'pineDead', 'pineSmall'];
    for (let i = 0; i < 170; i++) {
      const a = rand(i, 3, 1) * Math.PI * 2;
      const d = EDGE + 9 + rand(i, 5, 2) ** 0.8 * 75;
      const x = Math.cos(a) * d;
      const z = Math.sin(a) * d;
      if (z > EDGE && Math.abs(x) < 20 && z < HANGAR.z + 14) continue;
      if (x > 0 && Math.abs(z - (HANGAR.z + BUS_Z)) < 6) continue;
      if (Math.abs(x + EDGE + 6) < 6 && Math.abs(z + 4) < 8) continue;
      near(x, z).put(kinds[i % kinds.length]!, { x, z, y: groundHeight(x, z) - 0.1, yaw: rand(i, 7, 3) * 6.3, s: 0.9 + rand(i, 9, 4) * 0.6 });
    }
    const crags: BaseModelId[] = ['crag', 'crag2', 'crag3', 'crag4'];
    for (let i = 0; i < 26; i++) {
      const a = rand(i, 13, 1) * Math.PI * 2;
      const d = EDGE + 12 + rand(i, 15, 2) * 60;
      const x = Math.cos(a) * d;
      const z = Math.sin(a) * d;
      if (z > EDGE && Math.abs(x) < 20 && z < HANGAR.z + 14) continue;
      if (x > 0 && Math.abs(z - (HANGAR.z + BUS_Z)) < 6) continue;
      near(x, z).put(crags[i % 4]!, { x, z, y: groundHeight(x, z) - 0.3, yaw: rand(i, 17, 3) * 6.3, s: 0.8 + rand(i, 19, 4) * 1.2, finish: 'stone' });
    }
  }

  /** The Pyrenees behind: snowy ranges all round, highest to the north. */
  private buildRange(land: Site): void {
    const ranges: BaseModelId[] = ['range1', 'range2', 'range3', 'range4'];
    for (let i = 0; i < 16; i++) {
      const a = -Math.PI * 1.05 + (i / 15) * Math.PI * 2.1 + rand(i, 21, 1) * 0.2;
      const d = 150 + rand(i, 23, 2) * 40;
      const north = Math.max(0, -Math.sin(a));
      const [x, z] = [Math.cos(a) * d, Math.sin(a) * d];
      // The dam's gorge stays open.
      if (Math.hypot(x - DAM.x, z - DAM.z) < 45) continue;
      land.put(ranges[i % 4]!, { x, z, y: groundHeight(x, z) - 4, yaw: rand(i, 25, 3) * 6.3, s: 6 + north * 4 + rand(i, 27, 4) * 2, sy: 1.4 + north * 1.2, finish: 'mountain' });
    }
  }

  /** The dam up the valley to the north-east: a curved concrete wall across a gorge, the reservoir frozen behind it. */
  private buildDam(shell: THREE.Group, land: Site): void {
    const concrete = mat(0x9a9c9c, { rough: 0.9 });
    const dam = new THREE.Group();
    const r = 46;
    for (let i = 0; i < 16; i++) {
      const a = -0.55 + (i / 15) * 1.1;
      const block = box(6.4, 24, 4, concrete, Math.sin(a) * r, -4, -Math.cos(a) * r + r);
      block.rotation.y = -a;
      dam.add(block);
      if (i % 3 === 0) {
        const buttress = box(1.2, 26, 4.6, mat(0x86888a), Math.sin(a) * r, -4, -Math.cos(a) * r + r);
        buttress.rotation.y = -a;
        dam.add(buttress);
      }
    }
    const ice = new THREE.Mesh(new THREE.CircleGeometry(60, 24).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0xdfe9f2, roughness: 0.4 }));
    ice.position.set(0, 17.5, -30);
    dam.add(ice);
    dam.position.copy(DAM).setY(groundHeight(DAM.x, DAM.z) - 3);
    dam.rotation.y = DAM_YAW;
    shell.add(dam);
    // The gorge's sides: two ranges against the dam's ends.
    for (const side of [-1, 1]) {
      const end = new THREE.Vector3(side * 40, 0, 6).applyAxisAngle(new THREE.Vector3(0, 1, 0), DAM_YAW).add(DAM);
      land.put(side < 0 ? 'range2' : 'range3', { x: end.x, z: end.z, y: dam.position.y - 2, yaw: side * 0.7, s: side < 0 ? 4.2 : 5.4, sy: 2.6, finish: 'mountain' });
    }
  }

  /**
   * The hangar: concrete walls from the bunker kit and a corrugated roof,
   * open to the south; a side gate in the east wall rolls up for La Mula.
   */
  private buildHangar(batch: KitBatch, shell: THREE.Group): void {
    const k = new Site(batch, HANGAR, shell, this.shellFx);
    const hw = HANGAR_W / 2;
    const hd = HANGAR_D / 2;
    k.box('hangarFloor', mat(0x6c7176, { rough: 0.9 }), HANGAR_W + 0.6, FLOOR, HANGAR_D + 0.3, 0, 0, 0, 0, false);
    // Back wall: four kit walls in two rows, their ribbed face inside.
    for (const y of [FLOOR, FLOOR + 2]) {
      for (let i = 0; i < 4; i++) k.put('hangarWall', { x: -hw + 1.667 + i * 3.333, y, z: -hd, yaw: -Math.PI / 2 });
      // West wall, three shorter pieces a row.
      for (let i = 0; i < 3; i++) k.put('hangarWall', { x: -hw, y, z: -hd + HANGAR_D / 6 + (i * HANGAR_D) / 3, sz: HANGAR_D / 3 / 3.333 });
    }
    // East wall: solid south of the gate, a lintel over it.
    const gateN = BUS_Z - GATE_HALF;
    const gateS = BUS_Z + GATE_HALF;
    const solid = hd - gateS;
    for (const y of [FLOOR, FLOOR + 2]) k.put('hangarWall', { x: hw, y, z: gateS + solid / 2, yaw: Math.PI, sz: solid / 3.333 });
    k.put('hangarWall', { x: hw, y: FLOOR + GATE_H, z: (gateN + gateS) / 2 - 0.15, yaw: Math.PI, sz: (gateS - gateN + 0.6) / 3.333, sy: (HANGAR_H - FLOOR - GATE_H) / 2 });
    k.put('hangarPillar', { x: hw, z: -hd, sy: 1.95 });
    // A lamp over the gate outside, and hazard stripes on its jambs.
    k.put('wallLamp', { x: hw + 0.2, y: GATE_H + 0.35, z: BUS_Z, yaw: Math.PI / 2, s: 1.6 });
    k.glow('white', hw + 0.35, GATE_H + 0.25, BUS_Z, 0.22);
    for (const z of [BUS_Z - GATE_HALF - 0.05, BUS_Z + GATE_HALF + 0.05])
      for (let y = 0; y < 5; y++) k.box('gateStripe', y % 2 ? mat(0x1d1f22) : mat(0xe0a020), 0.06, GATE_H / 5, 0.12, hw + 0.13, FLOOR + (y * GATE_H) / 5, z, 0, false);
    // Pillars along the open front and a fascia over it.
    for (const x of [-hw, hw]) k.put('hangarPillar', { x, z: hd, sy: 1.95 });
    for (let i = 0; i < 4; i++) k.put('hangarWall', { x: -hw + 1.667 + i * 3.333, y: HANGAR_H - 0.75, z: hd, yaw: Math.PI / 2, sy: 0.38 });
    // The roof, corrugated sheets on top.
    for (let i = 0; i < 8; i++) for (let j = 0; j < 5; j++) k.put('hangarRoof', { x: -hw + 0.833 + i * 1.667, y: HANGAR_H, z: -hd + 0.833 + j * 1.667 });
    // On the roof: solar panels in a row and an aerial.
    for (let i = 0; i < 4; i++) k.put('solarPanel', { x: -4.6 + i * 1.5, y: HANGAR_H + 0.17, z: -1.6, yaw: Math.PI / 2, s: 1.1 });
    k.put('aerial', { x: 4.6, y: HANGAR_H + 0.17, z: -2.4, s: 0.7 });
    k.put('powerBoxes', { x: 3.4, y: HANGAR_H + 0.17, z: -3.6, yaw: Math.PI / 2, s: 0.6 });
    // Lamps on the back wall, a lit band under the eaves.
    for (const x of [-4.5, 0, 4.5]) {
      k.put('wallLamp', { x, y: 3.2, z: -hd + 0.2, s: 1.6 });
      k.glow('white', x, 3.1, -hd + 0.35, 0.2);
    }
    k.box('eaves', mat(0x2a2a20, { emissive: 0xfff0c8, intensity: 0.7 }), HANGAR_W - 0.8, 0.06, 0.05, 0, HANGAR_H - 0.85, hd - 0.12, 0, false);
    for (const x of [-3.5, 3.5]) {
      const lamp = new THREE.SpotLight(0xdfefff, 30, 13, 0.8, 0.5, 1.5);
      lamp.position.set(HANGAR.x + x, HANGAR.y + HANGAR_H - 0.3, HANGAR.z + 0.6);
      lamp.target.position.set(HANGAR.x + x, 0, HANGAR.z + 0.2);
      shell.add(lamp, lamp.target);
    }
    // Floor paint: the bus's box and a hazard stripe along the open front.
    const paint = mat(0x2a2a10, { emissive: 0xffc93a, intensity: 0.5 });
    for (const z of [-1, 1]) k.box('paint', paint, 9.6, 0.02, 0.1, BUS_X, FLOOR, BUS_Z + z * 1.55, 0, false);
    k.box('paint', paint, 0.1, 0.02, 3.1, BUS_X - 4.8, FLOOR, BUS_Z, 0, false);
    k.box('paint', mat(0x2a2a10, { emissive: 0xffc93a, intensity: 0.8 }), HANGAR_W - 0.4, 0.02, 0.18, 0, FLOOR + 0.005, hd - 0.3, 0, false);
    // Stores in the west corners, clear of the bus and the lineup.
    k.put('drums', { x: -hw + 0.8, y: FLOOR, z: -hd + 0.9 });
    k.put('toolShelf', { x: -hw + 0.45, y: FLOOR, z: -0.6, yaw: Math.PI / 2, finish: 'plain' });
    k.put('jerrycan', { x: -hw + 0.5, y: FLOOR, z: 0.4 });
    k.put('jerrycan', { x: -hw + 0.75, y: FLOOR, z: 0.55, yaw: 0.7 });
    k.put('tyres', { x: hw - 0.8, y: FLOOR, z: hd - 1.3, yaw: Math.PI / 2, finish: 'plain' });
    // Outside: a snowmobile and a sled by the west wall, floodlights on the apron.
    k.put('snowmobile', { x: -hw - 1.6, z: -1.0, yaw: 0.25 });
    k.put('sled', { x: -hw - 1.5, z: 1.2, yaw: 0.6 });
    k.put('floodlights', { x: -hw - 1.2, z: hd + 2.0, s: 0.6, yaw: 0.9 });

    // The side gate: two leaves hung from the lintel that roll up together.
    const gate = new THREE.Group();
    const leafSize = kitModel('hangarGate')?.size ?? new THREE.Vector3(0.23, 3.5, 1.97);
    for (const side of [-1, 1]) {
      const leaf = kitMesh('hangarGate', 'plain');
      leaf.scale.set(1, GATE_H / leafSize.y, GATE_HALF / leafSize.z);
      leaf.position.set(0, -GATE_H, (side * GATE_HALF) / 2);
      gate.add(leaf);
    }
    gate.position.set(HANGAR.x + hw, FLOOR + GATE_H, HANGAR.z + BUS_Z);
    shell.add(gate);
    this.gate = gate;

    // The barrier on the road east, where the station ends.
    const boomAt = new THREE.Vector3(HANGAR.x + hw + 11, 0, HANGAR.z + BUS_Z);
    new Site(batch, boomAt, shell, this.shellFx).put('boomPost', { yaw: 0 });
    const arm = kitMesh('boomArm');
    const armSize = kitModel('boomArm')?.size ?? new THREE.Vector3(0.15, 0.12, 6.6);
    const posts = kitModel('boomPost')?.size.z ?? 5.5;
    // The arm's hinge is near its +z end (the counterweight beyond it): on the north post.
    const pivot = new THREE.Group();
    pivot.position.copy(boomAt).add(new THREE.Vector3(0, 0.72, -posts / 2 + 0.05));
    arm.position.set(0, 0, armSize.z / 2 - 0.82);
    arm.rotation.y = Math.PI;
    pivot.add(arm);
    shell.add(pivot);
    this.boom = pivot;

    this.buildBus(shell);
  }

  /**
   * La Mula (LORE: an armoured city bus): POLYGON Town's bus with steel plates
   * bolted over its windows, slits to see through, plates on the roof. Its
   * doors and wheels are parts of their own, so they move.
   */
  private buildBus(shell: THREE.Group): void {
    const model = kitModel('bus');
    if (!model) return;
    const root = new THREE.Group();
    const body = new THREE.Group();
    // Its nose is +z and its doors on its −x side as it comes: turned, nose east and doors south.
    body.rotation.y = Math.PI / 2;
    body.scale.setScalar(BUS_SCALE);
    root.add(body);
    const doors: BusRig['doors'] = [];
    const wheels: THREE.Object3D[] = [];
    let wheelRadius = 0.35;
    const doorCentres: THREE.Vector3[] = [];
    for (const part of kitMesh('bus', 'plain').children.slice()) {
      const mesh = part as THREE.Mesh;
      mesh.geometry.computeBoundingBox();
      const bb = mesh.geometry.boundingBox!;
      const centre = bb.getCenter(new THREE.Vector3());
      if (/wheel/i.test(mesh.name)) {
        // Turned about its own axle.
        const hub = new THREE.Group();
        hub.position.copy(centre);
        const spun = mesh.clone();
        spun.geometry = mesh.geometry.clone().translate(-centre.x, -centre.y, -centre.z);
        hub.add(spun);
        body.add(hub);
        wheels.push(hub);
        wheelRadius = (bb.max.y - bb.min.y) / 2;
      } else if (/door/i.test(mesh.name)) {
        const leaf = new THREE.Group();
        leaf.add(mesh);
        body.add(leaf);
        const width = bb.max.z - bb.min.z;
        // A leaf folds inside, along the bus away from its doorway's middle.
        const along = Number(/Door_0(\d)/.exec(mesh.name)?.[1] ?? 1) % 2 ? 1 : -1;
        doors.push({ leaf, shut: new THREE.Vector3(), open: new THREE.Vector3(0.14, 0, along * width * 0.85) });
        if (!/glass/i.test(mesh.name)) doorCentres.push(centre);
      } else body.add(mesh);
    }
    // (A leaf's glass is a mesh of its own: its name gives it the same offset, so both move alike.)
    // Steel plates over the windows, both sides, leaving the doorways open.
    const glass = model.parts.find((p) => /^SM_Veh_Bus_Glass/i.test(p.name));
    const band = new THREE.Box3();
    glass?.geometry.computeBoundingBox();
    if (glass?.geometry.boundingBox) band.copy(glass.geometry.boundingBox);
    else band.set(new THREE.Vector3(-1.1, 1.0, -3.6), new THREE.Vector3(1.1, 1.8, 3.4));
    const plates = new KitBatch();
    const windowY = (band.min.y + band.max.y) / 2 + 0.05;
    const plateH = (kitModel('plateSlit')?.size.y ?? 0.45) * 1.15;
    const sy = Math.max(1, (band.max.y - band.min.y) / plateH);
    const halfW = model.size.x / 2 + 0.02;
    const inDoorway = (z: number) => doorCentres.some((c) => Math.abs(c.z - z) < 0.75);
    for (let z = band.min.z + 0.35; z < band.max.z - 0.2; z += 0.68) {
      for (const side of [-1, 1]) {
        if (side < 0 && inDoorway(z)) continue;
        plates.put(Math.round(z * 3) % 3 ? 'plate' : 'plateSlit', { x: side * halfW, y: windowY - 0.25 * sy, z, s: 1.15, sy, finish: 'plain' });
      }
    }
    // The windscreen half covered, a slit to drive by; plates on the roof.
    const nose = model.size.z / 2 + 0.03;
    for (const x of [-0.55, 0.55]) plates.put('plateSlit', { x, y: windowY - 0.35, z: nose - 0.15, yaw: Math.PI / 2, s: 1.3, finish: 'plain' });
    for (let z = -2.6; z <= 2.6; z += 1.3) plates.put('plateRoof', { x: (Math.round(z) % 2) * 0.2, y: model.size.y + 0.01, z, yaw: 0.05 * z, s: 1.1 });
    body.add(plates.build());
    // Headlights, off while parked.
    const lights = new THREE.MeshStandardMaterial({ color: 0x2a2a20, emissive: 0xfff2c0, emissiveIntensity: 0.2 });
    for (const x of [-0.75, 0.75]) {
      const lamp = new THREE.Mesh(UNIT, lights);
      lamp.scale.set(0.28, 0.14, 0.05);
      lamp.position.set(x, 0.55, nose + 0.01);
      body.add(lamp);
    }
    root.position.set(HANGAR.x + BUS_X, FLOOR, HANGAR.z + BUS_Z);
    shell.add(root);
    root.updateMatrixWorld(true);
    // Boarding points, in the hangar's space: in front of each doorway on the floor, and just inside it.
    const boarding = doorCentres.map((c) => {
      const outside = body.localToWorld(new THREE.Vector3(-model.size.x / 2 - 0.55, 0, c.z)).sub(HANGAR);
      const inside = body.localToWorld(new THREE.Vector3(-model.size.x / 2 + 0.5, 0.45, c.z)).sub(HANGAR);
      return { foot: outside.setY(FLOOR), inside };
    });
    for (const d of doors) d.shut.copy(d.leaf.position);
    this.bus = { root, doors, wheels, wheelRadius: wheelRadius * BUS_SCALE, lights, boarding };
  }

  // ------------------------------------------------------------ update

  /** Rebuilds the plots, their walkways and labels when the base changes. */
  setBase(c: CampaignState): void {
    this.base = c;
    const key = JSON.stringify(c.base);
    if (key === this.gridKey) return;
    if (!this.ready) return;
    this.gridKey = key;
    this.grid.traverse((o) => {
      if (o instanceof CSS2DObject) o.element.remove();
    });
    this.grid.clear();
    this.gridFx.clear();
    this.labels.clear();
    for (const h of [...this.hotspots]) if (h.userData.hotspot !== 'hangar') this.hotspots.splice(this.hotspots.indexOf(h), 1);
    const batch = new KitBatch();
    for (const plot of c.base.plots) {
      const o = plotCentre(plot.x, plot.y);
      const k = new Site(batch, o, this.grid, this.gridFx);
      const work = c.base.works.find((w) => w.plot === plot.id);
      const busy = !!work;
      const fixed = plot.building && plot.building in FIXED_BUILDINGS ? (plot.building as FixedBuildingId) : null;
      let title: string;
      let full: string | null = null;
      let kind = '';
      if (fixed) {
        k.pad('concrete');
        ({ command: bunkerArt, barracks: barracksArt, research: researchArt, engineering: engineeringArt })[fixed](k);
        // The bunker's label is short over the map (it sits between two others); the panels give the full name.
        title = fixed === 'command' ? 'Búnker' : FIXED_NAMES[fixed];
        kind = ' command';
      } else if (plot.building) {
        const facility = plot.building as FacilityId;
        const level = plot.level ?? 1;
        // The Camposanto is open ground, not a pad.
        if (facility !== 'memorial') k.pad('concrete');
        FACILITY_ART[facility]?.(k, level);
        levelPips(k, level);
        // A building the engine does not describe yet (art can land before its rules) still gets a label.
        full = FACILITIES[facility]?.name ?? facility;
        title = SHORT_NAMES[facility] ?? full;
        kind = ' built';
        // Being raised a level: scaffolding over the building, which keeps working.
        if (busy && work!.kind === 'upgrade') {
          upgradeArt(k, 1 - work!.daysLeft / work!.total);
          title = `${title} · mejora · ${work!.daysLeft} d`;
          kind = ' busy';
        }
      } else if (busy && work!.kind === 'build' && work!.facility) {
        constructionArt(k, 1 - work!.daysLeft / work!.total);
        title = `${FACILITIES[work!.facility].name} · ${work!.daysLeft} d`;
        kind = ' busy';
      } else {
        const variant = (plot.x * 7 + plot.y * 13) % 3;
        terrainArt(k, plot.terrain, variant, plot.x, plot.y);
        if (busy) {
          clearingArt(k);
          title = `Despejando · ${work!.daysLeft} d`;
          kind = ' busy';
        } else {
          title = plot.terrain === 'clear' ? 'Libre' : TERRAINS[plot.terrain].name;
          // rock / empty: the screens show these only where they can be acted on (Instalaciones).
          kind = plot.terrain === 'clear' ? ' empty' : ' rock';
        }
      }
      // Every other column's label sits a line lower, so neighbours in a row never overlap when zoomed out.
      const tag = label(title, `room-label${kind}${plot.x % 2 === 0 ? ' low' : ''}`);
      if (fixed) tag.element.title = FIXED_NAMES[fixed];
      else if (full) tag.element.title = full;
      tag.position.copy(o).add(new THREE.Vector3(0, LABEL_Y, PLOT / 2 - 0.2));
      tag.visible = this.labelsOn;
      this.grid.add(tag);
      this.labels.set(fixed ?? `plot:${plot.id}`, tag.element);
      const hit = hotspot(fixed ?? `plot:${plot.id}`, o);
      this.grid.add(hit);
      this.hotspots.push(hit);
    }

    // Board walkways between buildings side by side; the ones that give a bonus carry a string of green lamps.
    const lit = new Set(adjacencyBonuses(c).flatMap((b) => [`${b.plot}|${b.next}`, `${b.next}|${b.plot}`]));
    for (const [a, b] of walkways(c)) {
      const mid = plotCentre(a.x, a.y).add(plotCentre(b.x, b.y)).multiplyScalar(0.5);
      const alongX = a.y === b.y;
      const way = new Site(batch, mid, this.grid, this.gridFx, alongX ? 0 : Math.PI / 2);
      way.put('boardwalk', { y: 0.02, sx: (GAP + 0.7) / 2.35, sz: 1.05, finish: 'plain' });
      for (const s of [-1, 1]) way.box('wayKerb', mat(0x5a4a3a), GAP + 0.7, 0.1, 0.08, 0, 0.02, s * 0.66);
      if (lit.has(`${a.id}|${b.id}`)) for (const t of [-0.6, 0, 0.6]) way.glow('green', t, 0.14, 0.7, 0.1);
    }
    this.grid.add(batch.build());
    this.markLabels();
  }

  /** Building labels only make sense in the overview shots; close-ups hide them. */
  setLabels(on: boolean): void {
    this.labelsOn = on;
    this.scene.traverse((o) => {
      if (o instanceof CSS2DObject && !o.element.classList.contains('partner-tag')) o.visible = on;
    });
  }

  /** Highlights a plot (Instalaciones) or nothing. Takes "x,y" or "plot:x,y". */
  select(plot: string | null): void {
    this.frame.visible = !!plot;
    if (plot) this.frame.position.copy(this.roomCentre(plot));
    this.selected = plot ? (plot.includes(',') && !plot.startsWith('plot:') ? `plot:${plot}` : plot) : null;
    this.markLabels();
  }

  /** Coloured marker over the building the partner is looking at. */
  setPartner(room: string | null, color: string, name: string): void {
    const key = `${room}:${color}:${name}`;
    if (key === this.partnerKey) return;
    this.partnerKey = key;
    this.partnerMarker.traverse((o) => {
      if (o instanceof CSS2DObject) o.element.remove();
    });
    this.partnerMarker.clear();
    if (!room) return;
    const tag = label(`${name} está aquí`, 'partner-tag');
    tag.element.style.setProperty('--accent', color);
    const pos = room === 'hangar' ? HANGAR.clone().add(new THREE.Vector3(0, HANGAR_H + 1.0, HANGAR_D / 2)) : this.roomCentre(room).add(new THREE.Vector3(0, LABEL_Y + 1.2, 0));
    tag.position.copy(pos);
    this.partnerMarker.add(tag);
  }

  /** Soldiers standing on the launch pads (squad select and debrief). */
  setLineup(entries: (LineupEntry | null)[]): void {
    const key = JSON.stringify(entries);
    if (key === this.lineupKey) return;
    this.lineupKey = key;
    this.lineup.clear();
    this.rigs.length = 0;
    const shown = entries.slice(0, LINEUP);
    // A lane painted from the bus's doorway to the open front, between the two commanders' halves.
    if (shown.length > 1) {
      const half = Math.ceil(shown.length / 2);
      const x = (this.padPosition(half - 1, shown.length).x + this.padPosition(half, shown.length).x) / 2;
      const paint = mat(0x2a2a10, { emissive: 0xffc93a, intensity: 0.7 });
      const from = HANGAR.z + BUS_Z + 1.55;
      for (let k = 0; k < 6; k++) this.lineup.add(Object.assign(box(0.1, 0.02, 0.32, paint, x, HANGAR.y + FLOOR, from + 0.2 + k * 0.55), { castShadow: false }));
    }
    shown.forEach((entry, i) => {
      const at = this.padPosition(i, shown.length);
      const pad = new THREE.Mesh(new THREE.RingGeometry(0.42, 0.5, 28).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: entry ? new THREE.Color(entry.color) : 0x3a4652, transparent: true, opacity: 0.8 }));
      pad.position.copy(at).add(new THREE.Vector3(0, 0.02, 0));
      this.lineup.add(pad);
      if (!entry) return;
      if (entry.status === 'dead') {
        // A memorial: a dark plinth with a candle-like glow.
        this.lineup.add(box(0.5, 0.9, 0.3, mat(0x1a1d22, { metal: 0.3 }), at.x, at.y, at.z));
        this.lineup.add(box(0.12, 0.12, 0.12, mat(0x2a1a00, { emissive: 0xffb050, intensity: 2.2 }), at.x, at.y + 0.91, at.z));
        return;
      }
      const rig = buildUnit(entry.template, entry.color, entry.appearance, entry.gear, entry.seed);
      rig.root.position.copy(at);
      // Pose state, applied every frame in tick().
      Object.assign(rig.root.userData, { yaw: 0.15 - i * 0.04, phase: i * 0.7, wounded: entry.status === 'wounded', running: 0 });
      if (entry.status === 'wounded' && !isFigure(rig)) {
        rig.pivot.rotation.x = 0.35;
        rig.pivot.position.y = rig.hip - 0.12;
      }
      this.rigs.push(rig);
      this.lineup.add(rig.root);
    });
  }

  /**
   * Where soldier `i` of a lineup of `count` stands: in a row across the front
   * of the hangar, clear of the bus, split in two halves (the first
   * ceil(count / 2) pads and the rest, one per commander) with a gap between.
   */
  padPosition(i: number, count: number): THREE.Vector3 {
    const split = count > 1 ? (i < Math.ceil(count / 2) ? -PAD_SPLIT / 2 : PAD_SPLIT / 2) : 0;
    return HANGAR.clone().add(new THREE.Vector3((i - (count - 1) / 2) * PAD_STEP + split, FLOOR, PAD_Z));
  }

  /** The soldier being inspected in the barracks. */
  setShowcase(template: TemplateId | null, color: string, appearance?: Appearance, gear?: Gear, seed?: string): void {
    const key = `${template}:${color}:${JSON.stringify(appearance ?? null)}:${JSON.stringify(gear ?? null)}:${seed ?? ''}`;
    if (key === this.showcaseKey) return;
    this.showcaseKey = key;
    // A restyled soldier keeps turning from where the previous one was.
    const turned = this.showcaseRig?.root.rotation.y ?? 0;
    this.showcase.clear();
    this.showcaseRig = null;
    if (!template) return;
    const rig = buildUnit(template, color, appearance, gear, seed);
    rig.root.rotation.y = turned;
    this.showcase.add(rig.root);
    this.showcaseRig = rig;
  }

  /** Take-off: the squad boards La Mula and it drives out of the side gate; resolves once it has left. */
  launch(): Promise<void> {
    this.launching = performance.now();
    for (const rig of this.rigs) rig.root.userData.from = rig.root.position.clone();
    return new Promise((resolve) => setTimeout(resolve, LAUNCH_SECONDS * 1000));
  }

  // ------------------------------------------------------------- stage

  hits(): THREE.Object3D[] {
    return this.hotspots;
  }

  pick(hot: string): void {
    this.onPick(hot);
  }

  /** Told when the pointer moves onto or off a plot (the facilities list lights the same one). */
  onHover: ((hot: string | null) => void) | null = null;

  hover(hot: string | null): void {
    const onPlot = !!hot && hot !== 'hangar';
    this.hoverFrame.visible = onPlot;
    if (onPlot) this.hoverFrame.position.copy(this.roomCentre(hot!)).add(new THREE.Vector3(0, 0.01, 0));
    const was = this.hovered;
    this.hovered = onPlot ? hot : null;
    this.markLabels();
    if (this.hovered !== was) this.onHover?.(this.hovered);
  }

  /** Terrain and free plots name themselves only when pointed at or selected (49 labels were noise). */
  private markLabels(): void {
    for (const [id, el] of this.labels) {
      el.classList.toggle('hovered', id === this.hovered);
      el.classList.toggle('selected', id === this.selected);
    }
  }

  tick(dt: number, time: number): void {
    this.shellFx.tick(time);
    this.gridFx.tick(time);
    if (this.showcaseRig) this.showcaseRig.root.rotation.y += dt * 0.35;
    for (const bar of this.frame.children) ((bar as THREE.Mesh).material as THREE.MeshBasicMaterial).color.setHSL(0.55, 1, 0.55 + Math.sin(time * 5) * 0.15);
    ((this.hoverFrame.children[0] as THREE.Mesh).material as THREE.MeshBasicMaterial).opacity = 0.65 + Math.sin(time * 6) * 0.25;
    // Take-off: the squad runs to the doors in two files and climbs in.
    const since = this.launching ? (performance.now() - this.launching) / 1000 : -1;
    const boarded = BOARD_SECONDS + BOARD_STAGGER * Math.max(0, this.rigs.length - 1);
    const boarding = this.bus?.boarding ?? [];
    if (since >= 0) {
      this.rigs.forEach((rig, i) => {
        const k = THREE.MathUtils.clamp((since - i * BOARD_STAGGER) / BOARD_SECONDS, 0, 1);
        const from = rig.root.userData.from as THREE.Vector3;
        // The nearest doorway; two files, one from each half.
        const door = boarding.reduce<{ foot: THREE.Vector3; inside: THREE.Vector3 } | null>((best, d) => (!best || Math.abs(d.foot.x + HANGAR.x - from.x) < Math.abs(best.foot.x + HANGAR.x - from.x) ? d : best), null);
        const lane = (i % 2 ? 1 : -1) * 0.18;
        const foot = HANGAR.clone().add(door?.foot ?? new THREE.Vector3(BUS_X, FLOOR, BUS_Z + 1.9)).add(new THREE.Vector3(lane, 0, 0));
        const hatch = HANGAR.clone().add(door?.inside ?? new THREE.Vector3(BUS_X, 0.6, BUS_Z + 0.6)).add(new THREE.Vector3(lane * 0.5, 0, 0));
        // Three quarters of the time to reach the doorway, the rest to climb in.
        const climb = Math.max(0, (k - 0.75) / 0.25);
        if (climb > 0) rig.root.position.lerpVectors(foot, hatch, climb);
        else rig.root.position.lerpVectors(from, foot, k / 0.75);
        const [a, b] = climb > 0 ? [foot, hatch] : [from, foot];
        if (k > 0) rig.root.userData.yaw = Math.atan2(b.x - a.x, b.z - a.z);
        rig.root.userData.running = k > 0 && k < 1 ? 1 : 0;
        rig.root.visible = k < 1;
      });
    }
    // Soldiers breathe in line; figures also limp when wounded and run to board.
    for (const rig of this.rigs) {
      const d = rig.root.userData;
      if (isFigure(rig)) {
        rig.applyPose({ ...IDLE_POSE, yaw: d.yaw, phase: d.phase, run: d.running, crouch: d.wounded && !d.running ? 0.4 : 0 }, time);
      } else {
        rig.yaw.rotation.y = d.yaw;
        rig.pivot.scale.y = 1 + Math.sin(time * 2.1 + d.phase) * 0.012;
      }
    }
    if (this.showcaseRig && isFigure(this.showcaseRig)) this.showcaseRig.applyPose(IDLE_POSE, time);
    this.tickDeparture(since, boarded);
  }

  /** The doors close once all are aboard, the gate rolls up, the barrier lifts and La Mula drives off east. */
  private tickDeparture(since: number, boarded: number): void {
    const bus = this.bus;
    if (!bus) return;
    const closing = since >= 0 ? easeInOut(THREE.MathUtils.clamp((since - boarded - 0.02) / 0.25, 0, 1)) : 0;
    // Parked with its doors open, waiting for the squad.
    for (const d of bus.doors) d.leaf.position.copy(d.shut).add(d.open.clone().multiplyScalar(1 - closing));
    const rise = since >= 0 ? easeInOut(THREE.MathUtils.clamp((since - 0.2) / 1.0, 0, 1)) : 0;
    if (this.gate) this.gate.scale.y = 1 - rise * 0.94;
    const t = since - (boarded + 0.22);
    const travel = t > 0 ? 0.5 * DRIVE_ACCEL * t * t : 0;
    bus.root.position.x = HANGAR.x + BUS_X + travel;
    bus.root.position.y = FLOOR + (t > 0 ? Math.sin(t * 22) * 0.012 : 0);
    for (const w of bus.wheels) w.rotation.x = travel / bus.wheelRadius;
    bus.lights.emissiveIntensity = since >= 0 ? 2.4 : 0.2;
    if (this.boom) this.boom.rotation.x = since >= 0 ? -easeInOut(THREE.MathUtils.clamp((since - 1.2) / 0.8, 0, 1)) * 1.35 : 0;
  }
}
