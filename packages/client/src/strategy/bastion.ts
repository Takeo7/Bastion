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
import { voxelizeStatic, type StaticVoxelOptions } from '../game/voxel';
import type { Shot, StageSet } from './stage';

// The Bastion on the surface, seen from above at an angle: a 7×7 grid of
// plots in the Pyrenees snow, the bunker in the middle, the command rooms
// around it and the installations the players build next to them, joined by
// walkways. Buildings are roofless dioramas (low walls, a door to the south),
// so their insides read from above and the close-ups still work. The hangar
// with La Mula, the Bastion's tandem-rotor transport, and its apron stand south
// of the compound, outside the grid.

/** Side of a plot, and the strip between two plots where the walkways go. */
const PLOT = 5;
const GAP = 1.4;
const PITCH = PLOT + GAP;
const HALF = (BASE_SIZE - 1) / 2;
/** Low walls: rooms are seen from above. */
const WALL_H = 1.4;
/** Reference height the facility props were made for (a 3.2 m room); they are scaled down to fit. */
const ROOM_H = 3.2;
const PROP_SCALE = 0.82;
const COMPOUND = BASE_SIZE * PITCH;
/** The hangar, south of the compound, its open front facing the camera. */
const HANGAR = new THREE.Vector3(0, 0, COMPOUND / 2 + 9);
/** Inside of the hangar: width (x), depth (z) and the height of its sliding roof. */
const HANGAR_W = 13;
const HANGAR_D = 8;
const HANGAR_H = 4.3;
/** La Mula stands at the back of the hangar, the squad lines up at the front with a gap between the two commanders' halves. */
const SHIP_Z = -1.7;
const PAD_Z = 1.6;
const PAD_STEP = 1.22;
const PAD_SPLIT = 0.7;
/** The side door's ramp, in the hangar's space: where it meets the floor, and the hatch above it inside the hull. */
const RAMP_FOOT = new THREE.Vector3(0, 0.15, SHIP_Z + 2.25);
const HATCH = new THREE.Vector3(0, 0.7, SHIP_Z + 0.3);
/** Take-off timing: the squad boards one after another, then the ship lifts. */
const BOARD_SECONDS = 1.1;
const BOARD_STAGGER = 0.05;
/** The ramp's turn about its hinge: down to the floor, and up against the door. */
const RAMP_DOWN = Math.asin(0.55 / 1.32);
const RAMP_UP = -Math.PI / 2 + 0.04;
export const LAUNCH_SECONDS = 3.4;
const LINEUP = 8;

/** Rooms with a fixed purpose: each opens one strategy screen. */
export type FixedRoom = FixedBuildingId;

const mats = new Map<string, THREE.Material>();
function mat(color: number, opts: { emissive?: number; intensity?: number; metal?: number; rough?: number; opacity?: number } = {}): THREE.Material {
  const key = JSON.stringify([color, opts]);
  let m = mats.get(key);
  if (!m) {
    m = new THREE.MeshStandardMaterial({
      color,
      emissive: opts.emissive ?? 0,
      emissiveIntensity: opts.intensity ?? (opts.emissive ? 1.2 : 0),
      metalness: opts.metal ?? 0.2,
      roughness: opts.rough ?? 0.7,
      transparent: opts.opacity !== undefined,
      opacity: opts.opacity ?? 1,
    });
    mats.set(key, m);
  }
  return m;
}

function box(w: number, h: number, d: number, material: THREE.Material, x = 0, y = 0, z = 0): THREE.Mesh {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material);
  mesh.position.set(x, y + h / 2, z);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  return mesh;
}

function cylinder(r: number, h: number, material: THREE.Material, x = 0, y = 0, z = 0): THREE.Mesh {
  const mesh = new THREE.Mesh(new THREE.CylinderGeometry(r, r, h, 14), material);
  mesh.position.set(x, y + h / 2, z);
  mesh.castShadow = true;
  return mesh;
}

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

// ------------------------------------------------------------- palette

const SNOW = 0xd2dbe2;
const GRAVEL = 0x8f8a7c;
const DIRT = 0x7a6a50;
const CONCRETE = 0x8c9094;
const WALL = 0x5d6670;
const PINE = 0x2f4a32;
const TRUNK = 0x5a4030;

const STEEL = 0x9aa4ad;
const TIMBER = 0xa07a4a;

/** A double bunk: steel posts, two mattresses with army blankets and pillows. */
function bunk(g: THREE.Group, x: number, z: number): void {
  for (const dx of [-0.38, 0.38]) for (const dz of [-0.85, 0.85]) g.add(box(0.07, 2, 0.07, mat(0x56606a, { metal: 0.5 }), x + dx, 0, z + dz));
  for (const y of [0.35, 1.25]) {
    g.add(box(0.76, 0.14, 1.7, mat(0x5a6248), x, y, z));
    g.add(box(0.6, 0.1, 0.3, mat(0xd8d4c8), x, y + 0.14, z - 0.65));
  }
}

/** A lattice mast with crossbars on top and a lamp blinking at the tip. */
function mast(g: THREE.Group, x: number, z: number, h: number): void {
  const steel = mat(STEEL, { metal: 0.6 });
  for (const dx of [-0.16, 0.16]) g.add(box(0.07, h, 0.07, steel, x + dx, 0, z));
  for (let y = 0.45; y < h - 0.2; y += 0.55) g.add(box(0.38, 0.05, 0.06, steel, x, y, z));
  g.add(box(1.3, 0.05, 0.05, steel, x, h - 0.45, z), box(0.05, 0.05, 1.0, steel, x, h - 0.9, z));
  const light = box(0.18, 0.18, 0.18, mat(0x2a1a00, { emissive: 0xffc93a, intensity: 2.5 }), x, h, z);
  light.userData.blink = x + z;
  g.add(light);
}

/** A wheelbarrow: green tray, a wheel in front, handles behind. */
function wheelbarrow(g: THREE.Group, x: number, z: number): void {
  g.add(box(0.6, 0.3, 0.85, mat(0x4f6a3a, { metal: 0.3 }), x, 0.28, z));
  const wheel = new THREE.Mesh(new THREE.CylinderGeometry(0.2, 0.2, 0.1, 12), mat(0x1e2022));
  wheel.rotation.z = Math.PI / 2;
  wheel.position.set(x, 0.2, z + 0.55);
  g.add(wheel);
  for (const dx of [-0.22, 0.22]) g.add(box(0.05, 0.05, 0.7, mat(0x6a5038), x + dx, 0.45, z - 0.65), box(0.05, 0.28, 0.05, mat(0x56606a), x + dx, 0, z - 0.3));
}

/**
 * Props that make each facility recognisable at a glance. Levels 2 and 3 add
 * to them (more bunks and targets, a hoist, a second antenna, a mixer).
 */
/**
 * The Camposanto: no building, a fenced patch of snow with rows of pine
 * crosses; a bench and a lamp at level 2, and Teo's brass plaques at level 3.
 */
function memorialProps(level: number): THREE.Group {
  const g = new THREE.Group();
  const pine = mat(0x8a6a44);
  const dark = mat(0x5a4630);
  const half = PLOT / 2 - 0.3;
  // A low picket fence, open at the front.
  for (let t = -half; t <= half + 0.01; t += 0.5) {
    g.add(box(0.1, 0.55, 0.1, dark, t, 0, -half), box(0.1, 0.55, 0.1, dark, -half, 0, t), box(0.1, 0.55, 0.1, dark, half, 0, t));
  }
  for (const y of [0.18, 0.42]) g.add(box(PLOT - 0.6, 0.06, 0.06, dark, 0, y, -half), box(0.06, 0.06, PLOT - 0.6, dark, -half, y, 0), box(0.06, 0.06, PLOT - 0.6, dark, half, y, 0));
  // Rows of crosses on little mounds of snow.
  const rows = level >= 3 ? 4 : level >= 2 ? 3 : 2;
  for (let r = 0; r < rows; r++) {
    for (let i = 0; i < 5; i++) {
      const x = -1.8 + i * 0.9;
      const z = -1.6 + r * 0.95;
      g.add(box(0.5, 0.12, 0.7, mat(0xe8eef2), x, 0, z + 0.15));
      g.add(box(0.1, 0.75, 0.1, pine, x, 0.05, z), box(0.42, 0.09, 0.1, pine, x, 0.52, z));
    }
  }
  if (level >= 2) {
    g.add(box(1.2, 0.08, 0.35, dark, 1.6, 0.35, 2.0), box(0.08, 0.35, 0.3, dark, 1.1, 0, 2.0), box(0.08, 0.35, 0.3, dark, 2.1, 0, 2.0));
    g.add(box(0.08, 1.6, 0.08, mat(STEEL, { metal: 0.6 }), -2.1, 0, 2.0), box(0.2, 0.2, 0.2, mat(0x2a1a00, { emissive: 0xffc93a, intensity: 1.6 }), -2.1, 1.6, 2.0));
  }
  if (level >= 3) {
    // The brass plaques along the back fence.
    g.add(box(4.2, 0.9, 0.12, mat(0x6a6460), 0, 0, -half + 0.2));
    for (let i = 0; i < 6; i++) g.add(box(0.5, 0.3, 0.04, mat(0xb88a3a, { metal: 0.8, rough: 0.35 }), -1.6 + i * 0.64, 0.35, -half + 0.28));
  }
  return g;
}

function facilityProps(id: FacilityId, level = 1): THREE.Group {
  if ((id as string) === 'echoes') return echoesProps(level);
  const g = new THREE.Group();
  switch (id) {
    case 'simulation':
      // Simulator pods facing their screens, around a holographic battlefield.
      for (const x of [-2, 2]) {
        g.add(box(1.0, 0.5, 1.3, mat(0x3a444e, { metal: 0.5 }), x, 0, -0.4), box(1.0, 0.9, 0.2, mat(0x2a3036, { metal: 0.4 }), x, 0.5, -1.0));
        g.add(box(1.1, 0.6, 0.05, mat(0x0a1a2a, { emissive: 0x3fb8ff, intensity: 1.6 }), x, 1.3, -1.5));
      }
      g.add(cylinder(0.7, 0.6, mat(0x2a3036, { metal: 0.5 }), 0, 0, -0.6));
      {
        const holo = box(1.1, 0.5, 1.1, mat(0x3fa9ff, { emissive: 0x3fa9ff, intensity: 1.2, opacity: 0.4 }), 0, 0.65, -0.6);
        holo.userData.pulse = true;
        g.add(holo);
      }
      break;
    case 'workshop': {
      g.add(box(2.6, 0.9, 1.1, mat(0x5a5048, { metal: 0.5 }), -1.2, 0, -0.8));
      g.add(cylinder(0.25, 1.4, mat(0xffa630, { metal: 0.6 }), 1.6, 0, -0.9));
      const arm = box(0.2, 1.4, 0.2, mat(0xffa630, { metal: 0.6 }));
      arm.position.set(1.6, 1.6, -0.9);
      arm.userData.swing = true;
      g.add(arm);
      for (const [x, z] of [[2.4, 0.6], [-2.4, 0.8]] as const) g.add(box(0.8, 0.8, 0.8, mat(0x8f6c45), x, 0, z));
      g.add(box(0.15, 0.15, 0.15, mat(0xffd28a, { emissive: 0xff9a2a, intensity: 2 }), -1.2, 0.95, -0.8));
      break;
    }
    case 'infirmary': {
      for (const x of [-2, 0, 2]) {
        g.add(box(1.2, 0.5, 2, mat(0xe8edf2), x, 0, -0.4));
        g.add(box(1.1, 0.1, 0.5, mat(0xbfefff), x, 0.5, -1.1));
      }
      const cross = new THREE.Group();
      cross.add(box(0.7, 0.2, 0.05, mat(0x0a2a14, { emissive: 0x4dff9a, intensity: 2 })));
      cross.add(box(0.2, 0.7, 0.05, mat(0x0a2a14, { emissive: 0x4dff9a, intensity: 2 }), 0, -0.25, 0));
      cross.position.set(0, 2.2, -1.9);
      cross.userData.pulse = true;
      g.add(cross);
      break;
    }
    case 'training':
      g.add(box(4.6, 0.05, 2.6, mat(0x3a5a7a)));
      for (const x of [-2, 0.2, 2.2]) {
        const target = new THREE.Mesh(new THREE.CylinderGeometry(0.45, 0.45, 0.08, 20), mat(0xe8edf2));
        target.rotation.x = Math.PI / 2;
        target.position.set(x, 1.3, -1.7);
        const eye = new THREE.Mesh(new THREE.CylinderGeometry(0.2, 0.2, 0.1, 16), mat(0xd02a2a));
        eye.rotation.x = Math.PI / 2;
        eye.position.set(x, 1.3, -1.65);
        g.add(target, eye, box(0.08, 1.3, 0.08, mat(0x5a5048), x, 0, -1.75));
      }
      g.add(cylinder(0.25, 1.4, mat(0x8a96a3), -0.9, 0, 0.6));
      break;
    case 'relay': {
      const dish = new THREE.Mesh(new THREE.SphereGeometry(1.1, 20, 12, 0, Math.PI * 2, 0, Math.PI / 3), mat(0xd8dee4, { metal: 0.6, rough: 0.3 }));
      dish.rotation.x = -Math.PI / 2.4;
      dish.position.set(-1, 1.5, -0.6);
      dish.userData.spinY = true;
      g.add(dish, cylinder(0.12, 1.4, mat(0x8a96a3), -1, 0, -0.6));
      for (const x of [1.2, 2.3]) {
        g.add(box(0.8, 2.2, 0.8, mat(0x2a3036, { metal: 0.5 }), x, 0, -1.2));
        const led = box(0.6, 0.06, 0.05, mat(0x0a1a2a, { emissive: 0x3fa9ff, intensity: 2 }), x, 1.6, -0.79);
        led.userData.blink = x;
        g.add(led);
      }
      break;
    }
    case 'quarters': {
      // Bunks along the back (two, three or four by level), a footlocker at each, a stove lent by the barracks.
      const xs = [-1.95, -0.65, 0.65, 1.95].slice(0, level + 1);
      for (const x of xs) {
        bunk(g, x, -1.3);
        g.add(box(0.6, 0.35, 0.4, mat(0x4f5d3c), x, 0, -0.1));
      }
      g.add(cylinder(0.32, 0.8, mat(0x2a2e33, { metal: 0.6 }), -2.0, 0, 1.7), cylinder(0.07, 1.6, mat(0x2a2e33, { metal: 0.6 }), -2.0, 0.8, 1.7));
      const fire = box(0.3, 0.2, 0.05, mat(0x2a1000, { emissive: 0xff8a3a, intensity: 2.2 }), -2.0, 0.25, 2.03);
      fire.userData.pulse = true;
      g.add(fire);
      g.add(box(0.9, 0.6, 0.6, mat(0x6a5038), 1.7, 0, 1.6), box(0.15, 0.25, 0.15, mat(0x2a1a00, { emissive: 0xffc93a, intensity: 1.8 }), 1.9, 0.6, 1.6));
      break;
    }
    case 'range': {
      // A lane dug between pine logs: an earth berm at the back, silhouettes, a firing line of sandbags, the range flag.
      g.add(box(4.8, 0.06, 4.4, mat(DIRT), 0, 0, 0.2));
      g.add(box(4.8, 1.2, 0.9, mat(0x6a5a42), 0, 0, -2.0));
      for (const y of [0.3, 0.75]) g.add(box(4.8, 0.3, 0.3, mat(TRUNK), 0, y, -1.45));
      for (const x of [-2.35, 2.35]) for (const y of [0, 0.32]) g.add(box(0.3, 0.3, 4.0, mat(TRUNK), x, y, 0.1));
      const n = level + 1;
      for (let i = 0; i < n; i++) {
        const x = n === 1 ? 0 : -1.7 + (3.4 * i) / (n - 1);
        g.add(box(0.08, 0.6, 0.08, mat(0x5a5048), x, 0, -1.15));
        g.add(box(0.55, 0.75, 0.05, mat(0xd8c8a0), x, 0.6, -1.15), box(0.3, 0.3, 0.05, mat(0xd8c8a0), x, 1.35, -1.15));
        g.add(box(0.16, 0.16, 0.06, mat(0xb3261e), x, 0.95, -1.12));
      }
      for (let i = 0; i < 7; i++) g.add(box(0.55, 0.3, 0.35, mat(0xa89a70, { rough: 1 }), -1.75 + i * 0.58, 0, 1.3));
      for (let i = 0; i < 6; i++) g.add(box(0.55, 0.3, 0.35, mat(0xb8aa80, { rough: 1 }), -1.45 + i * 0.58, 0.3, 1.3));
      g.add(cylinder(0.05, 2.6, mat(STEEL), 2.2, 0, 2.1), box(0.6, 0.35, 0.04, mat(0xb3261e), 2.52, 2.2, 2.1));
      g.add(box(0.6, 0.35, 0.4, mat(0x4f5d3c), -2.0, 0, 2.2), box(0.5, 0.3, 0.35, mat(0x4f5d3c), -1.4, 0, 2.3));
      break;
    }
    case 'garage': {
      // La Mula's workshop: an engine on its stand, a tool wall, tyres, drums, a welder's spark.
      g.add(box(2.6, 0.03, 2.0, mat(0x2e3236), 0, 0, 0.2));
      g.add(box(1.2, 0.5, 0.9, mat(0x3a444e, { metal: 0.6 }), 0, 0, 0.2), box(1.0, 0.7, 0.8, mat(0x56606a, { metal: 0.7 }), 0, 0.5, 0.2));
      for (const x of [-0.3, 0, 0.3]) g.add(cylinder(0.1, 0.22, mat(0xffa630, { metal: 0.5 }), x, 1.2, 0.2));
      g.add(box(2.4, 1.3, 0.08, mat(0x6a5038), -0.7, 0.4, -2.3));
      for (const [x, y] of [
        [-1.6, 1.3],
        [-1.1, 1.0],
        [-0.6, 1.4],
        [-0.1, 1.1],
        [0.3, 1.35],
      ] as const)
        g.add(box(0.12, 0.4, 0.06, mat(0x2a2e33, { metal: 0.6 }), x, y, -2.24));
      for (const y of [0, 0.27, 0.54]) g.add(cylinder(0.45, 0.25, mat(0x1e2022), 2.0, y, -1.6));
      for (const [x, c] of [
        [-2.0, 0x8f2f2f],
        [-1.4, 0x4f5d3c],
      ] as const)
        g.add(cylinder(0.3, 0.9, mat(c, { metal: 0.4 }), x, 0, 1.9));
      const spark = box(0.12, 0.12, 0.12, mat(0x2a1a00, { emissive: 0xffa020, intensity: 3 }), 0.65, 0.95, 0.65);
      spark.userData.blink = 2.1;
      g.add(spark);
      if (level >= 2) {
        // A hoist over the engine.
        const orange = mat(0xffa630, { metal: 0.6 });
        for (const x of [-1.25, 1.25]) g.add(box(0.12, 2.6, 0.12, orange, x, 0, 0.2));
        g.add(box(2.6, 0.14, 0.14, orange, 0, 2.6, 0.2), box(0.04, 0.9, 0.04, mat(0x2a2e33), 0, 1.7, 0.2), box(0.2, 0.12, 0.12, mat(0x2a2e33), 0, 1.6, 0.2));
      }
      if (level >= 3) g.add(box(1.2, 0.15, 0.9, mat(0x8f6c45), 1.8, 0, 0.7), box(0.9, 0.6, 0.7, mat(0x56606a, { metal: 0.7 }), 1.8, 0.15, 0.7));
      break;
    }
    case 'listening': {
      // A radio desk with valve sets glowing, a telegraph key, a chair, and the mast you see from across the base.
      g.add(box(3.2, 0.8, 0.8, mat(0x5a4632), -0.6, 0, -1.9));
      for (const x of [-1.8, -0.9, 0]) {
        g.add(box(0.75, 0.6, 0.55, mat(0x3a3530), x, 0.8, -2.0));
        const dial = box(0.25, 0.15, 0.04, mat(0x2a1a00, { emissive: 0xffb04a, intensity: 1.8 }), x, 1.05, -1.71);
        dial.userData.pulse = true;
        g.add(dial);
        for (const dx of [-0.15, 0.15]) g.add(cylinder(0.06, 0.18, mat(0x3a1a00, { emissive: 0xff8a3a, intensity: 2 }), x + dx, 1.4, -2.05));
      }
      g.add(box(0.3, 0.06, 0.15, mat(0x8a6a3a, { metal: 0.6 }), 0.6, 0.8, -1.7));
      g.add(box(0.5, 0.45, 0.5, mat(0x3a444e), -0.9, 0, -0.9), box(0.5, 0.6, 0.08, mat(0x3a444e), -0.9, 0.45, -0.67));
      mast(g, 1.9, -1.7, 5.6);
      if (level >= 2) mast(g, -2.1, 1.7, 4.3);
      if (level >= 3) {
        const dish = new THREE.Mesh(new THREE.SphereGeometry(0.9, 20, 12, 0, Math.PI * 2, 0, Math.PI / 3), mat(0xd8dee4, { metal: 0.6, rough: 0.3 }));
        dish.rotation.x = -Math.PI / 2.4;
        dish.position.set(1.4, 1.3, 1.4);
        dish.userData.spinY = true;
        g.add(dish, cylinder(0.1, 1.2, mat(STEEL), 1.4, 0, 1.4));
      }
      break;
    }
    case 'crew': {
      // The works brigade's yard: stacked timber, wheelbarrows, shovels, hard hats; a pulley tripod and a mixer as it grows.
      for (let layer = 0; layer < 3; layer++) for (let k = 0; k < 3; k++) g.add(box(2.2, 0.16, 0.32, mat(layer % 2 ? 0x8f6c45 : TIMBER), -0.9, layer * 0.17, -2.1 + k * 0.36));
      wheelbarrow(g, 1.3, 0.6);
      if (level >= 2) wheelbarrow(g, 0.4, 1.4);
      g.add(box(0.1, 1.4, 1.6, mat(0x6a5038), -2.45, 0, 0.6));
      for (const z of [0.1, 0.5, 0.9, 1.2]) g.add(box(0.06, 1.2, 0.06, mat(0x6a5038), -2.3, 0.2, z), box(0.05, 0.3, 0.22, mat(0x56606a, { metal: 0.6 }), -2.3, 0.05, z));
      g.add(box(1.2, 0.5, 0.45, mat(0x6a5038), 1.6, 0, -1.9));
      for (const x of [1.25, 1.6, 1.95]) g.add(box(0.28, 0.14, 0.28, mat(0xffc93a), x, 0.5, -1.9));
      if (level >= 2) {
        // A pulley tripod with a crate hanging from it.
        const leg = mat(0x6a5038);
        for (let k = 0; k < 3; k++) {
          const a = (k / 3) * Math.PI * 2;
          const l = new THREE.Mesh(new THREE.BoxGeometry(0.08, 2.6, 0.08), leg);
          l.position.set(-0.6 + Math.cos(a) * 0.5, 1.25, 0.8 + Math.sin(a) * 0.5);
          l.rotation.set(Math.sin(a) * 0.22, 0, -Math.cos(a) * 0.22);
          l.castShadow = true;
          g.add(l);
        }
        g.add(box(0.03, 1.0, 0.03, mat(0x2a2e33), -0.6, 1.4, 0.8), box(0.5, 0.45, 0.5, mat(0x8f6c45), -0.6, 0.9, 0.8));
      }
      if (level >= 3) {
        // A cement mixer.
        const drum = new THREE.Mesh(new THREE.CylinderGeometry(0.35, 0.45, 0.8, 14), mat(0xffa630, { metal: 0.4 }));
        drum.rotation.x = 0.7;
        drum.position.set(-1.6, 0.85, 1.9);
        drum.castShadow = true;
        g.add(drum, box(0.7, 0.45, 0.5, mat(0x56606a, { metal: 0.5 }), -1.6, 0, 1.9));
      }
      break;
    }
  }
  return g;
}

/** The Coro's colours: the glow of anything sung (Velmar's runes, the master's tape). */
const CORO_GREEN = 0x6dffb0;
const CORO_CYAN = 0x6ad8ff;

/**
 * The Cámara de Ecos ('echoes'): a sheet-metal shed lined with copper mesh (a
 * Faraday cage: the Coro must not hear what is sung inside), shelves of tape
 * reels, a valve amplifier, a brass speaker horn, chairs with headphones, and
 * Velmar itself: a tapestry and a standing stone whose runes glow when it
 * sings. Level 2 adds reels and a second horn; level 3 the master's tape,
 * glowing on a lectern.
 */
function echoesProps(level: number): THREE.Group {
  const g = new THREE.Group();
  const copper = mat(0xb87333, { metal: 0.7, rough: 0.4 });
  // Copper mesh over the back and side walls.
  for (let x = -2.4; x <= 2.41; x += 0.4) g.add(box(0.07, 2.1, 0.07, copper, x, 0, -2.45));
  for (const y of [0.5, 1.1, 1.7]) g.add(box(4.9, 0.07, 0.07, copper, 0, y, -2.45));
  for (const sx of [-1, 1]) {
    for (let z = -2.2; z <= 2.2; z += 0.45) g.add(box(0.07, 2.1, 0.07, copper, sx * 2.65, 0, z));
    for (const y of [0.5, 1.1, 1.7]) g.add(box(0.07, 0.07, 4.6, copper, sx * 2.65, y, 0));
  }
  // Tape shelves along the left wall: reels standing on edge.
  const shelves = level >= 2 ? [-1.9, -0.6, 0.7] : [-1.4, -0.1];
  for (const z of shelves) {
    g.add(box(0.5, 1.9, 1.1, mat(0x4a3a2a), -2.2, 0, z));
    for (const y of [0.25, 0.85, 1.45])
      for (const dz of [-0.3, 0.05, 0.4]) {
        const reel = new THREE.Mesh(new THREE.CylinderGeometry(0.2, 0.2, 0.07, 14), mat(dz > 0.2 ? 0xb08a5a : 0xaab3ba, { metal: 0.4 }));
        reel.rotation.z = Math.PI / 2;
        reel.position.set(-1.92, y + 0.22, z + dz);
        reel.castShadow = true;
        g.add(reel);
      }
  }
  // The valve amplifier against the back wall, valves glowing on top.
  g.add(box(1.8, 0.9, 0.7, mat(0x3a3530), 0.2, 0, -2.0));
  for (let i = 0; i < 6; i++) g.add(cylinder(0.08, 0.26, mat(0x3a1a00, { emissive: 0xff8a3a, intensity: 2 }), -0.5 + i * 0.28, 0.9, -2.0));
  const meter = box(0.5, 0.25, 0.04, mat(0x0a2a14, { emissive: CORO_GREEN, intensity: 1.4 }), 0.2, 0.45, -1.63);
  meter.userData.pulse = true;
  g.add(meter);
  // Brass horns on stands, aimed at the chairs.
  const horn = (x: number, z: number, turn: number) => {
    const cone = new THREE.Mesh(new THREE.CylinderGeometry(0.5, 0.1, 1.0, 18, 1, true), mat(0xc9a13a, { metal: 0.8, rough: 0.3 }));
    cone.rotation.set(Math.PI / 2, 0, turn);
    cone.position.set(x, 1.7, z);
    cone.castShadow = true;
    g.add(cone, cylinder(0.06, 1.7, mat(0x2a2e33, { metal: 0.6 }), x, 0, z));
  };
  horn(1.9, -1.6, -0.6);
  if (level >= 2) horn(1.9, 1.5, -2.4);
  // Chairs with headphones in the middle: here the soldiers listen.
  for (const [x, z] of [
    [-0.6, 0.3],
    [0.5, 0.6],
  ] as const) {
    g.add(box(0.55, 0.45, 0.55, mat(0x5a4632), x, 0, z), box(0.55, 0.6, 0.08, mat(0x5a4632), x, 0.45, z + 0.24));
    g.add(box(0.35, 0.12, 0.12, mat(0x1d2124), x, 0.5, z - 0.05));
  }
  // Velmar: a tapestry on the back wall and a standing stone with glowing runes.
  g.add(box(1.1, 1.5, 0.05, mat(0x3a2a5a), -1.3, 0.5, -2.35), box(1.1, 0.12, 0.06, mat(0xc9a13a, { metal: 0.6 }), -1.3, 1.95, -2.34));
  g.add(box(0.5, 0.5, 0.06, mat(0xc9a13a, { metal: 0.5 }), -1.3, 1.0, -2.32));
  g.add(box(0.7, 1.8, 0.5, mat(0x8a8f86, { rough: 1 }), 1.2, 0, 0.2), box(0.5, 0.3, 0.4, mat(0x8a8f86, { rough: 1 }), 1.2, 1.8, 0.2));
  for (const [y, w] of [
    [0.5, 0.4],
    [0.9, 0.25],
    [1.3, 0.4],
  ] as const) {
    const rune = box(w, 0.08, 0.04, mat(0x0a2a1a, { emissive: CORO_GREEN, intensity: 2 }), 1.2, y, 0.46);
    rune.userData.pulse = true;
    g.add(rune);
  }
  if (level >= 3) {
    // The master's tape on a lectern, glowing.
    g.add(box(0.5, 1.0, 0.4, mat(0x4a3a2a), -0.1, 0, 1.6), box(0.6, 0.06, 0.45, mat(0x4a3a2a), -0.1, 1.0, 1.6));
    const tape = new THREE.Mesh(new THREE.CylinderGeometry(0.2, 0.2, 0.08, 14), mat(0x0a2a2a, { emissive: CORO_CYAN, intensity: 2.2 }));
    tape.position.set(-0.1, 1.12, 1.6);
    tape.userData.pulse = true;
    g.add(tape);
  }
  return g;
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

/** Three lamps on the front wall: as many lit as the facility's level. */
function levelPips(level: number): THREE.Group {
  const g = new THREE.Group();
  const z = PLOT / 2 - 0.125;
  for (let i = 0; i < 3; i++) {
    const on = i < level;
    g.add(box(0.32, 0.12, 0.22, on ? mat(0x2a1a00, { emissive: 0xffc93a, intensity: 2 }) : mat(0x2a3036), 1.25 + i * 0.45, WALL_H, z));
  }
  return g;
}

/** Scaffolding around a facility being raised a level: poles, rails, planks, a hazard stripe and a lamp. */
function scaffoldProps(progress: number): THREE.Group {
  const g = new THREE.Group();
  const steel = mat(STEEL, { metal: 0.6 });
  const half = PLOT / 2 + 0.25;
  const h = ROOM_H * 0.85;
  for (const [x, z] of [
    [-half, -half],
    [0, -half],
    [half, -half],
    [-half, 0],
    [half, 0],
    [-half, half],
    [half, half],
  ] as const)
    g.add(box(0.08, h, 0.08, steel, x, 0, z));
  for (const y of [1.2, 2.2]) {
    g.add(box(half * 2, 0.07, 0.07, steel, 0, y, -half));
    for (const x of [-half, half]) g.add(box(0.07, 0.07, half * 2, steel, x, y, 0));
  }
  g.add(box(half * 2, 0.06, 0.45, mat(TIMBER), 0, 2.2, -half + 0.25));
  for (const x of [-half + 0.25, half - 0.25]) g.add(box(0.45, 0.06, half * 2, mat(TIMBER), x, 2.2, 0));
  const p = Math.max(0.05, progress);
  g.add(box(5.6 * p, 0.12, 0.12, mat(0x2a2a10, { emissive: 0xffc93a, intensity: 1.6 }), -2.8 + 2.8 * p, 0.02, half + 0.15));
  const lamp = box(0.2, 0.2, 0.2, mat(0x2a1a00, { emissive: 0xffa020, intensity: 2.5 }), half, h, half);
  lamp.userData.blink = 0;
  g.add(lamp);
  return g;
}

/** What each command room holds: the holo table, the lab tubes, the forge, the lockers. */
function fixedProps(id: FixedBuildingId): THREE.Group {
  const g = new THREE.Group();
  switch (id) {
    case 'command': {
      g.add(box(2.2, 0.9, 1.4, mat(0x2a3036, { metal: 0.4 }), 0, 0, -0.4));
      const holo = new THREE.Mesh(new THREE.SphereGeometry(0.55, 16, 12), mat(0x3fa9ff, { emissive: 0x3fa9ff, intensity: 1.2, opacity: 0.5 }));
      holo.position.set(0, 1.7, -0.4);
      holo.userData.spinY = true;
      g.add(holo);
      for (const x of [-1.6, 1.6]) g.add(box(0.7, 1.1, 0.5, mat(0x1a2430, { metal: 0.4 }), x, 0, -1.4), box(0.6, 0.4, 0.05, mat(0x0a1a2a, { emissive: 0x3fb8ff, intensity: 1.4 }), x, 1.2, -1.15));
      break;
    }
    case 'research':
      g.add(box(2.4, 0.85, 0.9, mat(0xb8c2cc, { metal: 0.4 }), -0.4, 0, -0.9));
      g.add(box(1.4, 0.6, 0.05, mat(0x0a1a2a, { emissive: 0x3fb8ff, intensity: 1.6 }), -0.4, 1.15, -1.3));
      for (const x of [-1.6, 1.5]) {
        const tube = cylinder(0.32, 1.9, mat(0x6dff9a, { emissive: 0x2fd97a, intensity: 0.9, opacity: 0.55 }), x, 0, 0.4);
        tube.userData.pulse = true;
        g.add(tube);
      }
      break;
    case 'engineering': {
      // Workbench with a rifle on it, a welding spark, a robot arm and parts shelves.
      g.add(box(2.2, 0.9, 1.1, mat(0x5a5048, { metal: 0.5 }), -0.5, 0, -0.6));
      g.add(box(1.1, 0.12, 0.18, mat(0x2a3036, { metal: 0.7 }), -0.7, 0.9, -0.5), box(0.3, 0.2, 0.12, mat(0x3a444e, { metal: 0.6 }), -0.2, 0.9, -0.5));
      const spark = box(0.12, 0.12, 0.12, mat(0x2a1a00, { emissive: 0xffa020, intensity: 3 }), -1.2, 0.95, -0.4);
      spark.userData.blink = 1.3;
      g.add(spark);
      g.add(cylinder(0.22, 1.2, mat(0xffa630, { metal: 0.6 }), 1.0, 0, -0.5));
      const arm = box(0.18, 1.2, 0.18, mat(0xffa630, { metal: 0.6 }));
      arm.position.set(1.0, 1.4, -0.5);
      arm.userData.swing = true;
      g.add(arm);
      for (const y of [0.5, 1.3]) {
        g.add(box(1.0, 0.06, 0.5, mat(0x3a444e, { metal: 0.5 }), 1.55, y, -1.65));
        g.add(box(0.3, 0.3, 0.3, mat(0x8f6c45), 1.35, y + 0.06, -1.65), box(0.25, 0.2, 0.3, mat(0x56606a, { metal: 0.5 }), 1.8, y + 0.06, -1.65));
      }
      break;
    }
    case 'barracks':
      for (const x of [-1.6, -0.6, 0.6, 1.6]) g.add(box(0.8, 1.6, 0.45, mat(0x4a5560, { metal: 0.4 }), x, 0, -1.7));
      // Bunks along the side walls.
      for (const x of [-2.1, 2.1]) g.add(box(0.7, 0.4, 1.8, mat(0x6a5a48), x, 0, 0.4), box(0.6, 0.12, 1.6, mat(0xc8c0b0), x, 0.4, 0.4));
      break;
  }
  return g;
}

/** Scaffolding, crates and a hazard stripe: a facility being built. */
function constructionProps(progress: number): THREE.Group {
  const g = new THREE.Group();
  const steel = mat(0x9aa4ad, { metal: 0.6 });
  for (const x of [-2.4, -0.8, 0.8, 2.4]) g.add(box(0.08, ROOM_H * 0.7, 0.08, steel, x, 0, -1.2));
  for (const y of [0.9, 1.9]) g.add(box(5, 0.08, 0.08, steel, 0, y, -1.2));
  // The walls rise with the work.
  const p = Math.max(0.05, progress);
  g.add(box(5.4, WALL_H * p, 0.25, mat(WALL), 0, 0, -2.4));
  g.add(box(5.6 * p, 0.12, 0.12, mat(0x2a2a10, { emissive: 0xffc93a, intensity: 1.6 }), -2.8 + 2.8 * p, 0.02, 2.2));
  g.add(box(0.9, 0.7, 0.9, mat(0x8f6c45), 1.8, 0, 0.6), box(0.7, 0.5, 0.7, mat(0x8f6c45), 2.6, 0, 0.9));
  const lamp = box(0.2, 0.2, 0.2, mat(0x2a1a00, { emissive: 0xffa020, intensity: 2.5 }), -2.4, ROOM_H * 0.7, -1.2);
  lamp.userData.blink = 0;
  g.add(lamp);
  return g;
}

// ------------------------------------------------------------- terrain

/** A pine, snow on its shoulders. */
function pine(scale: number): THREE.Group {
  const g = new THREE.Group();
  g.add(cylinder(0.12 * scale, 0.7 * scale, mat(TRUNK)));
  for (const [y, r, h] of [[0.5, 0.75, 1.1], [1.2, 0.58, 0.95], [1.8, 0.4, 0.8]] as const) {
    const cone = new THREE.Mesh(new THREE.ConeGeometry(r * scale, h * scale, 8), mat(PINE, { rough: 0.9 }));
    cone.position.y = (y + h / 2) * scale;
    cone.castShadow = true;
    g.add(cone);
  }
  const cap = new THREE.Mesh(new THREE.ConeGeometry(0.22 * scale, 0.35 * scale, 8), mat(SNOW, { rough: 1 }));
  cap.position.y = 2.45 * scale;
  g.add(cap);
  return g;
}

/** Rocks, pines or the ruins of the old 1989 station on an uncleared plot (variant 0–2). */
function terrainProps(terrain: BasePlot['terrain'], variant: number): THREE.Group {
  const g = new THREE.Group();
  const r = (k: number) => rand(variant, terrain.length, k);
  switch (terrain) {
    case 'rocks': {
      g.add(box(PLOT, 0.12, PLOT, mat(0x8a8478, { rough: 1 })));
      for (let i = 0; i < 7; i++) {
        const size = 0.45 + r(i) * 0.9;
        const rock = new THREE.Mesh(new THREE.DodecahedronGeometry(size, 0), mat(i % 3 ? 0x6e6a64 : 0x5a5650, { rough: 1 }));
        rock.position.set((r(i + 10) - 0.5) * PLOT * 0.8, size * 0.55, (r(i + 20) - 0.5) * PLOT * 0.8);
        rock.rotation.set(r(i + 30) * 3, r(i + 40) * 3, 0);
        rock.castShadow = true;
        g.add(rock);
        if (size > 0.9) {
          const snow = new THREE.Mesh(new THREE.DodecahedronGeometry(size * 0.55, 0), mat(SNOW, { rough: 1 }));
          snow.position.copy(rock.position).add(new THREE.Vector3(0, size * 0.55, 0));
          g.add(snow);
        }
      }
      break;
    }
    case 'forest': {
      g.add(box(PLOT, 0.1, PLOT, mat(0x56603e, { rough: 1 })));
      for (let i = 0; i < 6; i++) {
        const tree = pine(0.75 + r(i) * 0.6);
        tree.position.set((r(i + 10) - 0.5) * PLOT * 0.8, 0.1, (r(i + 20) - 0.5) * PLOT * 0.8);
        g.add(tree);
      }
      break;
    }
    case 'ruins': {
      g.add(box(PLOT, 0.14, PLOT, mat(0x77756e, { rough: 1 })));
      // Broken walls of the old barracks, a collapsed roof beam, rusty drums.
      g.add(box(3.6, 1.2 + r(1) * 0.6, 0.3, mat(0x9a9488), -0.3, 0.14, -1.6), box(0.3, 0.9, 2.2, mat(0x9a9488), 1.6, 0.14, -0.6));
      g.add(box(1.4, 0.5, 0.3, mat(0x9a9488), -1.6, 0.14, 1.2));
      const beam = box(3.4, 0.2, 0.25, mat(0x5a4a38), 0.2, 0.6, 0.2);
      beam.rotation.z = 0.25;
      g.add(beam);
      for (let i = 0; i < 3; i++) g.add(cylinder(0.25, 0.6, mat(0x8a4a2a, { metal: 0.4 }), 1.4 - i * 0.55, 0.14, 1.6 - (i % 2) * 0.5));
      for (let i = 0; i < 5; i++) g.add(box(0.4 + r(i) * 0.4, 0.25, 0.35, mat(0x8a8478), (r(i + 5) - 0.5) * 3.6, 0.14, (r(i + 9) - 0.2) * 2));
      break;
    }
    case 'clear':
      // A levelled plot, staked out for building.
      g.add(box(PLOT, 0.08, PLOT, mat(DIRT, { rough: 1 })));
      for (const [x, z] of [[-1, -1], [1, -1], [-1, 1], [1, 1]] as const) {
        g.add(box(0.1, 0.6, 0.1, mat(0x8a6a40), x * (PLOT / 2 - 0.3), 0, z * (PLOT / 2 - 0.3)));
        g.add(box(0.14, 0.14, 0.14, mat(0x2a2a10, { emissive: 0xffc93a, intensity: 1.2 }), x * (PLOT / 2 - 0.3), 0.6, z * (PLOT / 2 - 0.3)));
      }
      break;
  }
  return g;
}

/** Clearing a plot: an orange bulldozer working and a pile of spoil. */
function clearingProps(): THREE.Group {
  const g = new THREE.Group();
  const dozer = new THREE.Group();
  dozer.add(box(1.6, 0.6, 1.0, mat(0xffa630, { metal: 0.4 }), 0, 0.25, 0), box(0.8, 0.6, 0.9, mat(0xffa630, { metal: 0.4 }), -0.3, 0.85, 0));
  dozer.add(box(0.6, 0.4, 0.05, mat(0x0a1a2a, { emissive: 0xbfe6ff, intensity: 0.6 }), -0.3, 1.05, 0.46));
  dozer.add(box(0.15, 0.6, 1.4, mat(0x5a5a52, { metal: 0.6 }), 1.0, 0.05, 0));
  for (const z of [-0.55, 0.55]) dozer.add(box(1.8, 0.35, 0.25, mat(0x2a2a28), 0, 0, z));
  const beacon = box(0.14, 0.14, 0.14, mat(0x2a1a00, { emissive: 0xffa020, intensity: 2.5 }), -0.3, 1.45, 0);
  beacon.userData.blink = 2;
  dozer.add(beacon);
  dozer.position.set(-0.6, 0.12, 1.4);
  dozer.rotation.y = -0.3;
  g.add(dozer);
  const spoil = new THREE.Mesh(new THREE.ConeGeometry(0.9, 0.8, 8), mat(DIRT, { rough: 1 }));
  spoil.position.set(1.6, 0.5, 1.6);
  g.add(spoil);
  return g;
}

// ------------------------------------------------------------- rooms

/** A roofless room on a concrete slab: low walls and a door to the south (towards the camera). */
function roomShell(wall: number, trim: number, thick = 0.25, height = WALL_H): THREE.Group {
  const g = new THREE.Group();
  const half = PLOT / 2;
  const door = 1.6;
  g.add(box(PLOT, 0.2, PLOT, mat(CONCRETE), 0, -0.08, 0));
  g.add(box(PLOT - 0.3, 0.04, PLOT - 0.3, mat(0x4a525c), 0, 0.12, 0));
  g.add(box(PLOT, height, thick, mat(wall), 0, 0, -half + thick / 2));
  for (const x of [-1, 1]) g.add(box(thick, height, PLOT, mat(wall), x * (half - thick / 2), 0, 0));
  const side = (PLOT - door) / 2;
  for (const x of [-1, 1]) g.add(box(side, height, thick, mat(wall), x * (door / 2 + side / 2), 0, half - thick / 2));
  // A coloured band on the wall tops tells the room's kind at a glance.
  g.add(box(PLOT, 0.08, thick + 0.04, mat(trim), 0, height, -half + thick / 2));
  for (const x of [-1, 1]) g.add(box(thick + 0.04, 0.08, PLOT, mat(trim), x * (half - thick / 2), height, 0));
  return g;
}

/** The bunker: thicker, taller walls, sandbags at the door, radar and masts on its corners. */
function bunkerExtras(): THREE.Group {
  const g = new THREE.Group();
  const half = PLOT / 2;
  for (let i = 0; i < 6; i++) g.add(box(0.5, 0.3, 0.35, mat(0xa89a70, { rough: 1 }), -1.4 + i * 0.56 + (i > 2 ? 0.9 : 0) - 0.45, 0, half + 0.45));
  const radar = new THREE.Mesh(new THREE.SphereGeometry(0.9, 20, 12, 0, Math.PI * 2, 0, Math.PI / 3), mat(0xd8dee4, { metal: 0.6, rough: 0.3 }));
  radar.rotation.x = -Math.PI / 2.5;
  // On the front corners: the bunker is never seen close up, but the rooms behind it are.
  radar.position.set(half - 0.6, 3.2, half - 0.6);
  radar.userData.spinY = true;
  g.add(radar, cylinder(0.1, 3, mat(0x8a96a3), half - 0.6, 0, half - 0.6));
  for (const [x, h] of [[-half + 0.4, 4.2], [-half + 1.1, 3]] as const) {
    g.add(cylinder(0.06, h, mat(0x9aa4ad, { metal: 0.6 }), x, 0, half - 0.4));
    const light = box(0.18, 0.18, 0.18, mat(0x2a0a0a, { emissive: 0xff3a3a, intensity: 2.5 }), x, h, half - 0.4);
    light.userData.blink = x;
    g.add(light);
  }
  return g;
}

const TRIM: Record<FixedBuildingId, number> = { command: 0xb3261e, research: 0x2fd97a, engineering: 0xffa630, barracks: 0x3fa9ff };

/**
 * Voxel art (M5): parts that tick() animates by transform or visibility are
 * voxelized one by one and keep their userData; the ship's engine glows (their
 * material is animated), its rotors' blur, hotspots and the roof doors stay as
 * they are.
 */
const VOXEL_SET: StaticVoxelOptions = {
  animated: ['spinY', 'spinX', 'swing', 'pulse', 'blink', 'showcase'],
  keep: ['engine', 'hotspot', 'roofSide', 'blur'],
};

/** Plot contents are voxelized once per kind and then cloned, so rebuilding the base stays cheap. */
const voxelTemplates = new Map<string, THREE.Group>();
function voxelProps(key: string, build: () => THREE.Group): THREE.Group {
  let template = voxelTemplates.get(key);
  if (!template) {
    template = build();
    voxelizeStatic(template, VOXEL_SET);
    voxelTemplates.set(key, template);
  }
  return template.clone();
}

/** A room with its props, scaled into the plot. */
function room(shell: () => THREE.Group, props: () => THREE.Group): THREE.Group {
  const g = new THREE.Group();
  g.add(shell());
  const inside = props();
  inside.scale.setScalar(PROP_SCALE);
  inside.position.set(0, 0.14, -0.2);
  g.add(inside);
  return g;
}

/** Invisible box over a plot: what the pointer actually hits. */
function hotspot(id: string, origin: THREE.Vector3): THREE.Mesh {
  const hit = new THREE.Mesh(new THREE.BoxGeometry(PLOT, 2.4, PLOT), new THREE.MeshBasicMaterial({ visible: false }));
  hit.position.copy(origin).add(new THREE.Vector3(0, 1.2, 0));
  hit.userData.hotspot = id;
  return hit;
}

export interface LineupEntry {
  template: TemplateId;
  color: string;
  appearance?: Appearance;
  gear?: Gear;
  /** Debrief pose: wounded soldiers slump, the fallen leave a memorial. */
  status?: 'ok' | 'wounded' | 'dead';
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
  private readonly frame: THREE.LineSegments;
  private readonly hoverFrame: THREE.LineSegments;
  private readonly partnerMarker = new THREE.Group();
  private readonly ship = new THREE.Group();
  private readonly rigs: UnitRig[] = [];
  private readonly roofDoors: THREE.Object3D[] = [];
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

  constructor(private readonly onPick: (hotspot: string) => void) {
    // A clear winter afternoon in the Pyrenees.
    this.scene.background = new THREE.Color(0xa9c4da);
    this.scene.fog = new THREE.FogExp2(0xc6d4de, 0.0065);
    this.scene.add(new THREE.HemisphereLight(0xdce8ff, 0x8a8070, 1.05));
    const sun = new THREE.DirectionalLight(0xfff0dc, 2.3);
    sun.position.set(-26, 42, 30);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    sun.shadow.bias = -0.0004;
    sun.shadow.normalBias = 0.03;
    Object.assign(sun.shadow.camera, { left: -40, right: 40, top: 40, bottom: -40, near: 1, far: 140 });
    this.scene.add(sun, sun.target);
    this.buildShell();
    voxelizeStatic(this.scene, VOXEL_SET);
    this.scene.add(this.woods());
    this.scene.add(this.grid, this.lineup, this.showcase, this.partnerMarker);
    const square = (color: number) =>
      new THREE.LineSegments(new THREE.EdgesGeometry(new THREE.BoxGeometry(PLOT + 0.4, 0.06, PLOT + 0.4)), new THREE.LineBasicMaterial({ color, transparent: true }));
    this.frame = square(0x54d1ff);
    this.hoverFrame = square(0xffffff);
    this.frame.visible = this.hoverFrame.visible = false;
    this.scene.add(this.frame, this.hoverFrame);
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
    const fit = new THREE.Box3(new THREE.Vector3(-edge, 0, -edge), new THREE.Vector3(edge, WALL_H, edge));
    return { pos: new THREE.Vector3(0, 46, 34), target, fov: 40, fit, zoom: { min: 0.35, reach: COMPOUND / 2 } };
  }

  /** Low and wide on the hangar, looking up at the roof La Mula leaves through. */
  takeoff(): Shot {
    const target = HANGAR.clone().add(new THREE.Vector3(0, 3.2, SHIP_Z));
    return { pos: HANGAR.clone().add(new THREE.Vector3(2.4, 0.9, 9.4)), target, fov: 56 };
  }

  /** The soldier on the barracks pedestal, seen through its door. */
  armory(): Shot {
    // High enough to look over the low front wall.
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

  /** Snowfield, the compound's gravel and fence, watchtowers, the road and the hangar with the dropship. */
  private buildShell(): void {
    // Snow all round, with pines and the mountains to the north.
    this.scene.add(box(260, 0.6, 260, mat(SNOW, { rough: 1 }), 0, -0.7, 0));
    this.scene.add(box(COMPOUND + 3, 0.12, COMPOUND + 3, mat(GRAVEL, { rough: 1 }), 0, -0.2, 0));
    for (let i = 0; i < 7; i++) {
      const h = 18 + rand(i, 7, 3) * 22;
      const peak = new THREE.Group();
      const body = new THREE.Mesh(new THREE.ConeGeometry(h * 0.9, h, 6), mat(0x6e747c, { rough: 1 }));
      body.position.y = h / 2;
      // The snow sits a little proud of the rock: a cap with the peak's own slope lay in its faces and fought them.
      const cap = new THREE.Mesh(new THREE.ConeGeometry(h * 0.375, h * 0.4, 6), mat(SNOW, { rough: 1 }));
      cap.position.y = h * 0.8 + h * 0.012;
      peak.add(body, cap);
      peak.position.set(-110 + i * 36 + rand(i, 1, 1) * 10, -0.5, -110 - rand(i, 2, 2) * 25);
      this.scene.add(peak);
    }

    // Concrete barriers round the compound, a gate to the south, a watchtower at each corner.
    const edge = COMPOUND / 2 + 1.2;
    for (let t = -edge; t < edge; t += 2.4) {
      for (const [x, z, along] of [[t + 1.2, -edge, 'x'], [-edge, t + 1.2, 'z'], [edge, t + 1.2, 'z'], [t + 1.2, edge, 'x']] as const) {
        if (z === edge && Math.abs(x) < 4) continue;
        const barrier = along === 'x' ? box(2.1, 0.7, 0.45, mat(0xb4b2aa), x, -0.15, z) : box(0.45, 0.7, 2.1, mat(0xb4b2aa), x, -0.15, z);
        this.scene.add(barrier);
      }
    }
    for (const sx of [-1, 1]) {
      for (const sz of [-1, 1]) {
        const tower = new THREE.Group();
        for (const [px, pz] of [[-0.6, -0.6], [0.6, -0.6], [-0.6, 0.6], [0.6, 0.6]] as const) tower.add(box(0.14, 4, 0.14, mat(0x5a4a38), px, 0, pz));
        tower.add(box(1.8, 0.15, 1.8, mat(0x6a5a48), 0, 4, 0), box(1.8, 0.6, 0.1, mat(0x6a5a48), 0, 4.15, 0.85), box(2.1, 0.15, 2.1, mat(0x4a525c), 0, 5.3, 0));
        const lamp = box(0.25, 0.2, 0.25, mat(0x2a2a10, { emissive: 0xfff0b0, intensity: 1.6 }), 0, 4.8, 0.7);
        tower.add(lamp);
        tower.position.set(sx * (edge + 0.6), -0.15, sz * (edge + 0.6));
        this.scene.add(tower);
      }
    }
    // Road from the gate to the hangar's apron.
    this.scene.add(box(4, 0.1, HANGAR.z - edge, mat(0x4a4e52, { rough: 0.9 }), 0, -0.18, edge + (HANGAR.z - edge) / 2));
    this.scene.add(box(18, 0.12, 9, mat(0x55595d, { rough: 0.9 }), HANGAR.x, -0.18, HANGAR.z + 1));

    // The hangar: a shed open to the south, its roof in two halves that slide apart for take-off.
    const hangar = new THREE.Group();
    const hw = HANGAR_W / 2;
    const hd = HANGAR_D / 2;
    hangar.add(box(HANGAR_W + 0.4, 0.15, HANGAR_D, mat(0x56606a), 0, 0, 0));
    hangar.add(box(HANGAR_W + 0.4, HANGAR_H, 0.2, mat(0x5d6670), 0, 0, -hd));
    for (const x of [-1, 1]) hangar.add(box(0.2, HANGAR_H, HANGAR_D, mat(0x6d7782), x * (hw + 0.1), 0, 0));
    // Ribs on the back wall and a lit band under the eaves, so the ship stands out against it.
    for (const x of [-4.8, -1.6, 1.6, 4.8]) hangar.add(box(0.3, HANGAR_H, 0.16, mat(0x4a525c), x, 0, -hd + 0.18));
    hangar.add(box(HANGAR_W - 0.6, 0.12, 0.06, mat(0x2a2a10, { emissive: 0xfff0b0, intensity: 1.1 }), 0, HANGAR_H - 0.55, -hd + 0.13));
    for (const side of [-1, 1]) {
      const door = box(hw, 0.2, HANGAR_D, mat(0x6d7782, { opacity: 0.25 }), (side * hw) / 2, HANGAR_H, 0);
      door.userData.roofSide = side;
      this.roofDoors.push(door);
      hangar.add(door);
    }
    for (const x of [-4, 0, 4]) {
      const lamp = new THREE.SpotLight(0xdfefff, 30, 13, 0.75, 0.5, 1.5);
      lamp.position.set(x, HANGAR_H - 0.3, 0.6);
      lamp.target.position.set(x, 0, 0.2);
      hangar.add(lamp, lamp.target);
    }
    // Floor paint: the ship's parking box and the hazard stripe along the open front.
    const paint = mat(0x2a2a10, { emissive: 0xffc93a, intensity: 0.5 });
    for (const z of [-1, 1]) hangar.add(box(10.2, 0.02, 0.1, paint, 0, 0.15, SHIP_Z + z * 1.6));
    for (const x of [-1, 1]) hangar.add(box(0.1, 0.02, 3.2, paint, x * 5.1, 0.15, SHIP_Z));
    hangar.add(box(HANGAR_W - 0.4, 0.02, 0.18, mat(0x2a2a10, { emissive: 0xffc93a, intensity: 0.8 }), 0, 0.16, hd - 0.3));
    // Stores in the back corners, clear of the ship and the lineup: fuel drums, crates, a tool trolley.
    for (const [x, z] of [[-5.9, -3.4], [-5.3, -3.5], [-5.9, -2.8]] as const) hangar.add(cylinder(0.28, 0.85, mat(0x4f5d3c, { metal: 0.4 }), x, 0.15, z));
    hangar.add(box(0.9, 0.7, 0.8, mat(0x8f6c45), 5.7, 0.15, -3.3), box(0.7, 0.55, 0.6, mat(0x8f6c45), 5.75, 0.85, -3.3), box(0.8, 0.6, 0.7, mat(0x8f6c45), 5.8, 0.15, -2.4));
    this.buildShip();
    this.ship.position.set(0, 0.15, SHIP_Z);
    hangar.add(this.ship);
    hangar.position.copy(HANGAR);
    const hangarHit = new THREE.Mesh(new THREE.BoxGeometry(HANGAR_W, HANGAR_H, HANGAR_D), new THREE.MeshBasicMaterial({ visible: false }));
    hangarHit.position.copy(HANGAR).add(new THREE.Vector3(0, HANGAR_H / 2, 0));
    hangarHit.userData.hotspot = 'hangar';
    this.hotspots.push(hangarHit);
    const hangarLabel = label('Hangar', 'room-label fixed');
    hangarLabel.position.copy(HANGAR).add(new THREE.Vector3(0, HANGAR_H + 0.5, hd));
    this.scene.add(hangar, hangarHit, hangarLabel);

    // The barracks' pedestal for the soldier being inspected (the room around it comes with the plots).
    const pedestal = cylinder(0.55, 0.25, mat(0x2a3036, { metal: 0.5 }), 0, 0, 0);
    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.56, 0.03, 6, 32).rotateX(Math.PI / 2), mat(0x0a1a2a, { emissive: 0x54d1ff, intensity: 1.8 }));
    ring.position.y = 0.26;
    const stand = new THREE.Group();
    stand.add(pedestal, ring);
    stand.position.copy(this.pedestalPos());
    this.scene.add(stand);
    this.showcase.position.copy(this.pedestalPos()).add(new THREE.Vector3(0, 0.25, 0));
  }

  /** Pines all round the compound (already voxel clones: added after the scene is voxelized). */
  private woods(): THREE.Group {
    const g = new THREE.Group();
    for (let i = 0; i < 46; i++) {
      const a = rand(i, 3, 1) * Math.PI * 2;
      const d = COMPOUND / 2 + 8 + rand(i, 5, 2) * 34;
      const x = Math.cos(a) * d;
      const z = Math.sin(a) * d;
      // Keep the road and the hangar clear.
      if (z > COMPOUND / 2 && Math.abs(x) < 16) continue;
      const tree = voxelProps(`pine:${i % 4}`, () => pine(1.1 + (i % 4) * 0.25));
      tree.position.set(x, -0.1, z);
      g.add(tree);
    }
    return g;
  }

  /**
   * La Mula, the Bastion's transport (LORE: a propeller transport with
   * mechanical controls): a boxy tandem-rotor in olive drab, nose to the west,
   * its side door and ramp facing the squad. The rotors and the ramp are
   * groups of their own, so they keep moving once the ship is voxelized.
   */
  private buildShip(): void {
    const s = this.ship;
    const olive = mat(0x6f7a55, { metal: 0.3, rough: 0.6 });
    const dark = mat(0x4c553a, { metal: 0.3, rough: 0.6 });
    const glass = mat(0x14232b, { metal: 0.6, rough: 0.2 });
    const steel = mat(0x2a2e33, { metal: 0.6 });
    const yellow = mat(0xffc93a, { rough: 0.5 });
    // Fuselage (cabin floor at 0.55), a pale belly, and fuel sponsons along the sides.
    s.add(box(7.2, 1.75, 1.9, olive, 0, 0.45, 0));
    s.add(box(6.9, 0.14, 1.7, mat(0x8a8f80, { metal: 0.3 }), 0, 0.34, 0));
    for (const z of [-1, 1]) s.add(box(3.2, 0.55, 0.3, dark, 0.9, 0.5, z * 1.1));
    // The nose: a lower cockpit with its windscreen, and a row of cabin portholes.
    s.add(box(1.5, 1.4, 1.7, olive, -4.3, 0.55, 0));
    s.add(box(0.06, 0.55, 1.4, glass, -5.06, 1.2, 0));
    for (const z of [-1, 1]) {
      s.add(box(0.9, 0.45, 0.06, glass, -4.3, 1.25, z * 0.86));
      for (const x of [-2.6, -1.6, 1.4, 2.3]) s.add(box(0.4, 0.34, 0.06, glass, x, 1.5, z * 0.96));
    }
    // The side door, framed in yellow so it reads where the squad boards.
    s.add(box(1.25, 1.35, 0.06, mat(0x1d2124), 0, 0.55, 0.96));
    s.add(box(1.45, 0.09, 0.08, yellow, 0, 1.9, 0.98));
    for (const x of [-1, 1]) s.add(box(0.09, 1.35, 0.08, yellow, x * 0.68, 0.55, 0.98));
    // Tail: the rear pylon lifts the aft rotor above the forward one, with an engine on each flank.
    s.add(box(1.9, 1.0, 1.2, olive, 2.9, 2.2, 0));
    s.add(box(1.0, 1.45, 1.6, olive, 4.0, 0.7, 0));
    s.add(box(0.3, 1.77, 1.93, yellow, 3.35, 0.44, 0));
    s.add(box(1.1, 0.45, 0.9, olive, -3.3, 2.2, 0));
    s.add(cylinder(0.1, 0.4, steel, -3.3, 2.6, 0), cylinder(0.1, 0.4, steel, 2.9, 3.15, 0));
    for (const z of [-1, 1]) {
      const engine = new THREE.Mesh(new THREE.CylinderGeometry(0.32, 0.32, 1.6, 12), dark);
      engine.rotation.z = Math.PI / 2;
      engine.position.set(2.75, 2.55, z * 0.85);
      engine.castShadow = true;
      const glow = new THREE.Mesh(new THREE.CircleGeometry(0.24, 14), mat(0x2a1000, { emissive: 0xff9a3a, intensity: 0.2 }));
      glow.position.set(3.57, 2.55, z * 0.85);
      glow.rotation.y = Math.PI / 2;
      glow.userData.engine = true;
      s.add(engine, glow);
    }
    // Landing gear.
    for (const x of [-3.4, 2.6]) {
      for (const z of [-0.75, 0.75]) {
        const wheel = new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.22, 0.16, 12), mat(0x1e2022));
        wheel.rotation.x = Math.PI / 2;
        wheel.position.set(x, 0.22, z);
        s.add(wheel, box(0.12, 0.25, 0.12, steel, x, 0.3, z * 0.85));
      }
    }
    const tail = box(0.14, 0.14, 0.14, mat(0x2a0a0a, { emissive: 0xff3a3a, intensity: 2.5 }), 4.52, 1.9, 0);
    tail.userData.blink = 1;
    s.add(tail);
    // Two three-bladed rotors, still while parked; they spin up for take-off.
    for (const [x, y, dir] of [
      [-3.3, 3.0, 1],
      [2.9, 3.55, -1],
    ] as const) {
      const rotor = new THREE.Group();
      rotor.position.set(x, y, 0);
      rotor.add(cylinder(0.24, 0.16, steel, 0, -0.04, 0));
      for (let k = 0; k < 3; k++) {
        const a = (dir > 0 ? Math.PI : 0) + (k * Math.PI * 2) / 3;
        for (const [len, at, m] of [
          [1.75, 0.95, steel],
          [0.3, 1.97, yellow],
        ] as const) {
          const blade = box(len, 0.05, 0.24, m, 0, 0.02, 0);
          blade.rotation.y = a;
          blade.position.x = Math.cos(a) * at;
          blade.position.z = -Math.sin(a) * at;
          rotor.add(blade);
        }
      }
      // The blur of the blades at speed, faded in as they spin up.
      const blur = new THREE.Mesh(new THREE.CircleGeometry(2.1, 32).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0x1d2124, transparent: true, opacity: 0, depthWrite: false }));
      blur.position.y = 0.05;
      blur.userData.blur = true;
      rotor.add(blur);
      rotor.userData.rotor = dir;
      s.add(rotor);
    }
    // The ramp, hinged at the foot of the door: down while parked, up once the squad is aboard.
    const ramp = new THREE.Group();
    ramp.position.set(0, 0.55, 0.96);
    ramp.add(box(1.2, 0.07, 1.32, dark, 0, -0.07, 0.66));
    for (const x of [-0.55, 0.55]) ramp.add(box(0.06, 0.1, 1.3, yellow, x, -0.02, 0.66));
    ramp.rotation.x = RAMP_DOWN;
    ramp.userData.ramp = true;
    s.add(ramp);
  }

  // ------------------------------------------------------------ update

  /** Rebuilds the plots, their walkways and labels when the base changes. */
  setBase(c: CampaignState): void {
    const key = JSON.stringify(c.base);
    if (key === this.gridKey) return;
    this.gridKey = key;
    this.grid.traverse((o) => {
      if (o instanceof CSS2DObject) o.element.remove();
    });
    this.grid.clear();
    this.labels.clear();
    for (const h of [...this.hotspots]) if (h.userData.hotspot !== 'hangar') this.hotspots.splice(this.hotspots.indexOf(h), 1);
    for (const plot of c.base.plots) {
      const o = plotCentre(plot.x, plot.y);
      const g = new THREE.Group();
      g.position.copy(o);
      const work = c.base.works.find((w) => w.plot === plot.id);
      const busy = !!work;
      const fixed = plot.building && plot.building in FIXED_BUILDINGS ? (plot.building as FixedBuildingId) : null;
      let title: string;
      let full: string | null = null;
      let kind = '';
      if (fixed) {
        g.add(
          voxelProps(`fixed:${fixed}`, () =>
            room(
              () => {
                const shell = roomShell(fixed === 'command' ? 0x6a6a62 : WALL, TRIM[fixed], fixed === 'command' ? 0.5 : 0.25, fixed === 'command' ? 1.8 : WALL_H);
                if (fixed === 'command') shell.add(bunkerExtras());
                return shell;
              },
              () => fixedProps(fixed),
            ),
          ),
        );
        // The bunker's label is short over the map (it sits between two others); the panels give the full name.
        title = fixed === 'command' ? 'Búnker' : FIXED_NAMES[fixed];
        kind = ' command';
      } else if (plot.building) {
        const facility = plot.building as FacilityId;
        const level = plot.level ?? 1;
        g.add(
          voxelProps(`facility:${facility}:${level}`, () => {
            // The Camposanto is open ground, not a room.
            if (facility === 'memorial') return memorialProps(level);
            // The Cámara de Ecos is a galvanised shed with the Coro's green on its walls; the rest, the usual rooms.
            const echoes = (facility as string) === 'echoes';
            const r = room(() => (echoes ? roomShell(0x7d848c, CORO_GREEN) : roomShell(0x6a7480, 0xd8dee4)), () => facilityProps(facility, level));
            r.add(levelPips(level));
            return r;
          }),
        );
        // A building the engine does not describe yet (art can land before its rules) still gets a label.
        full = FACILITIES[facility]?.name ?? (facility === ('echoes' as FacilityId) ? 'Cámara de Ecos' : facility);
        title = SHORT_NAMES[facility] ?? full;
        kind = ' built';
        // Being raised a level: scaffolding over the building, which keeps working.
        if (busy && work!.kind === 'upgrade') {
          const progress = 1 - work!.daysLeft / work!.total;
          g.add(voxelProps(`upgrade:${Math.round(progress * 4)}`, () => scaffoldProps(progress)));
          title = `${title} · mejora · ${work!.daysLeft} d`;
          kind = ' busy';
        }
      } else if (busy && work!.kind === 'build' && work!.facility) {
        const progress = 1 - work!.daysLeft / work!.total;
        g.add(voxelProps('terrain:clear:0', () => terrainProps('clear', 0)));
        g.add(voxelProps(`build:${Math.round(progress * 6)}`, () => constructionProps(progress)));
        title = `${FACILITIES[work!.facility].name} · ${work!.daysLeft} d`;
        kind = ' busy';
      } else {
        const variant = (plot.x * 7 + plot.y * 13) % 3;
        g.add(voxelProps(`terrain:${plot.terrain}:${variant}`, () => terrainProps(plot.terrain, variant)));
        if (busy) {
          g.add(voxelProps('clearing', clearingProps));
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
      tag.position.set(0, (fixed === 'command' ? 1.8 : WALL_H) + 0.5, PLOT / 2 - 0.2);
      g.add(tag);
      this.grid.add(g);
      this.labels.set(fixed ?? `plot:${plot.id}`, tag.element);
      const hit = hotspot(fixed ?? `plot:${plot.id}`, o);
      this.grid.add(hit);
      this.hotspots.push(hit);
    }

    // Walkways between buildings side by side; the ones that give a bonus carry a lit strip.
    const lit = new Set(adjacencyBonuses(c).flatMap((b) => [`${b.plot}|${b.next}`, `${b.next}|${b.plot}`]));
    for (const [a, b] of walkways(c)) {
      const from = plotCentre(a.x, a.y);
      const to = plotCentre(b.x, b.y);
      const mid = from.clone().add(to).multiplyScalar(0.5);
      const alongX = a.y === b.y;
      const way = new THREE.Group();
      const len = GAP + 0.6;
      way.add(alongX ? box(len, 0.16, 1.7, mat(0xa8aaa4)) : box(1.7, 0.16, len, mat(0xa8aaa4)));
      for (const s of [-1, 1]) way.add(alongX ? box(len, 0.32, 0.1, mat(0x6a7480), 0, 0.16, s * 0.8) : box(0.1, 0.32, len, mat(0x6a7480), s * 0.8, 0.16, 0));
      if (lit.has(`${a.id}|${b.id}`)) {
        const strip = alongX ? box(len, 0.06, 0.24, mat(0x0a2a1a, { emissive: 0x4dffb0, intensity: 1.8 }), 0, 0.16, 0) : box(0.24, 0.06, len, mat(0x0a2a1a, { emissive: 0x4dffb0, intensity: 1.8 }), 0, 0.16, 0);
        way.add(strip);
      }
      way.position.copy(mid).add(new THREE.Vector3(0, -0.06, 0));
      voxelizeStatic(way, VOXEL_SET);
      this.grid.add(way);
    }
  }

  /** Building labels only make sense in the overview shots; close-ups hide them. */
  setLabels(on: boolean): void {
    this.scene.traverse((o) => {
      if (o instanceof CSS2DObject && !o.element.classList.contains('partner-tag')) o.visible = on;
    });
  }

  /** Highlights a plot (Instalaciones) or nothing. Takes "x,y" or "plot:x,y". */
  select(plot: string | null): void {
    this.frame.visible = !!plot;
    if (plot) this.frame.position.copy(this.roomCentre(plot)).add(new THREE.Vector3(0, 0.08, 0));
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
    const pos = room === 'hangar' ? HANGAR.clone().add(new THREE.Vector3(0, HANGAR_H + 1.0, HANGAR_D / 2)) : this.roomCentre(room).add(new THREE.Vector3(0, WALL_H + 1.6, 0));
    tag.position.copy(pos);
    this.partnerMarker.add(tag);
  }

  /** Soldiers standing on the launch pad (squad select and debrief). */
  setLineup(entries: (LineupEntry | null)[]): void {
    const key = JSON.stringify(entries);
    if (key === this.lineupKey) return;
    this.lineupKey = key;
    this.lineup.clear();
    this.rigs.length = 0;
    const shown = entries.slice(0, LINEUP);
    // A lane painted from the ship's ramp to the open front, between the two commanders' halves.
    if (shown.length > 1) {
      const half = Math.ceil(shown.length / 2);
      const x = (this.padPosition(half - 1, shown.length).x + this.padPosition(half, shown.length).x) / 2;
      const paint = mat(0x2a2a10, { emissive: 0xffc93a, intensity: 0.7 });
      for (let k = 0; k < 6; k++) {
        const dash = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.02, 0.32), paint);
        dash.position.set(x, HANGAR.y + 0.16, HANGAR.z + RAMP_FOOT.z + 0.35 + k * 0.55);
        this.lineup.add(dash);
      }
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
      const rig = buildUnit(entry.template, entry.color, entry.appearance, entry.gear);
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
   * of the hangar, clear of the ship, split in two halves (the first
   * ceil(count / 2) pads and the rest, one per commander) with a gap between.
   */
  padPosition(i: number, count: number): THREE.Vector3 {
    const split = count > 1 ? (i < Math.ceil(count / 2) ? -PAD_SPLIT / 2 : PAD_SPLIT / 2) : 0;
    return HANGAR.clone().add(new THREE.Vector3((i - (count - 1) / 2) * PAD_STEP + split, 0.15, PAD_Z));
  }

  /** The soldier being inspected in the barracks. */
  setShowcase(template: TemplateId | null, color: string, appearance?: Appearance, gear?: Gear): void {
    const key = `${template}:${color}:${JSON.stringify(appearance ?? null)}:${JSON.stringify(gear ?? null)}`;
    if (key === this.showcaseKey) return;
    this.showcaseKey = key;
    // A restyled soldier keeps turning from where the previous one was.
    const turned = this.showcaseRig?.root.rotation.y ?? 0;
    this.showcase.clear();
    this.showcaseRig = null;
    if (!template) return;
    const rig = buildUnit(template, color, appearance, gear);
    rig.root.rotation.y = turned;
    rig.root.userData.showcase = true;
    this.showcase.add(rig.root);
    this.showcaseRig = rig;
  }

  /** Dropship take-off: the squad boards, then the ship lifts off; resolves once it has left. */
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
    if (onPlot) this.hoverFrame.position.copy(this.roomCentre(hot!)).add(new THREE.Vector3(0, 0.1, 0));
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
    this.scene.traverse((o) => {
      const d = o.userData;
      if (d.spinY) o.rotation.y = time * 0.8;
      if (d.spinX) o.rotation.x = time * 12;
      if (d.swing) o.rotation.z = 0.8 + Math.sin(time * 1.5) * 0.5;
      if (d.pulse) o.scale.setScalar(1 + Math.sin(time * 2.5) * 0.04);
      if (d.blink !== undefined) o.visible = Math.sin(time * 3 + d.blink) > -0.3;
      if (d.showcase) o.rotation.y += dt * 0.35;
    });
    (this.frame.material as THREE.LineBasicMaterial).color.setHSL(0.55, 1, 0.55 + Math.sin(time * 5) * 0.15);
    (this.hoverFrame.material as THREE.LineBasicMaterial).opacity = 0.5 + Math.sin(time * 6) * 0.2;
    // Take-off: the rotors spin up and the squad runs up the ramp in two files, one from each half.
    const since = this.launching ? (performance.now() - this.launching) / 1000 : -1;
    const boarded = BOARD_SECONDS + BOARD_STAGGER * Math.max(0, this.rigs.length - 1);
    if (since >= 0) {
      this.rigs.forEach((rig, i) => {
        const k = THREE.MathUtils.clamp((since - i * BOARD_STAGGER) / BOARD_SECONDS, 0, 1);
        const from = rig.root.userData.from as THREE.Vector3;
        const lane = THREE.MathUtils.clamp(from.x - HANGAR.x, -1, 1) * 0.28;
        const foot = HANGAR.clone().add(RAMP_FOOT).add(new THREE.Vector3(lane, 0, 0));
        const hatch = HANGAR.clone().add(HATCH).add(new THREE.Vector3(lane * 0.5, 0, 0));
        // Three quarters of the time to reach the foot of the ramp, the rest to climb it into the hull.
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
    // The ramp closes once all are aboard, the roof slides open and La Mula lifts away, nose down.
    const spin = since >= 0 ? 26 * Math.min(1, since / 1.5) ** 2 : 0;
    const closing = since >= 0 ? easeInOut(THREE.MathUtils.clamp((since - boarded - 0.05) / 0.4, 0, 1)) : 0;
    this.ship.traverse((o) => {
      const d = o.userData;
      if (d.engine) ((o as THREE.Mesh).material as THREE.MeshStandardMaterial).emissiveIntensity = since >= 0 ? 2.2 + Math.sin(time * 40) * 0.6 : 0.15;
      if (d.rotor) o.rotation.y += dt * spin * d.rotor;
      if (d.blur) ((o as THREE.Mesh).material as THREE.MeshBasicMaterial).opacity = 0.32 * Math.min(1, spin / 18);
      if (d.ramp) o.rotation.x = RAMP_DOWN + (RAMP_UP - RAMP_DOWN) * closing;
    });
    const open = since >= 0 ? Math.min(1, Math.max(0, (since - 0.4) / 1.2)) : 0;
    for (const door of this.roofDoors) door.position.x = door.userData.roofSide * (HANGAR_W / 4 + easeInOut(open) * (HANGAR_W / 2 + 0.6));
    const lift = since - Math.max(BOARD_SECONDS + 0.3, boarded + 0.12);
    this.ship.position.y = 0.15 + (lift > 0 ? lift ** 2 * 4.2 : 0);
    this.ship.rotation.z = lift > 0 ? Math.min(0.1, lift * 0.08) : 0;
  }
}
