import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { BASE_COLS, BASE_ROWS, FACILITIES, type Appearance, type CampaignState, type FacilityId, type TemplateId } from '@bastion/engine';
import { buildUnit, IDLE_POSE, isFigure, type UnitRig } from '../game/models';
import type { Gear } from '../game/figures';
import { easeInOut } from '../game/tween';
import { voxelizeStatic, type StaticVoxelOptions } from '../game/voxel';
import type { Shot, StageSet } from './stage';

const ROOM_W = 6;
const ROOM_H = 3.2;
const ROOM_D = 4;
const GAP = 0.6;
/** Ground level: the fixed rooms sit just under it, the dug rooms further down. */
const SURFACE = ROOM_H + GAP;
const GRID_W = BASE_COLS * ROOM_W + (BASE_COLS - 1) * GAP;
/** The four command rooms share the width of the facility grid. */
const FIXED_W = (GRID_W - 3 * GAP) / 4;
const HANGAR_X = -6;
/** Take-off timing: the squad boards, then the ship lifts. */
const BOARD_SECONDS = 1.1;
export const LAUNCH_SECONDS = 3.4;
const LINEUP = 8;

/** Rooms with a fixed purpose: each opens one strategy screen. */
export type FixedRoom = 'command' | 'research' | 'engineering' | 'barracks';
const FIXED: { id: FixedRoom; name: string }[] = [
  { id: 'command', name: 'Mando' },
  { id: 'research', name: 'Progreso' },
  { id: 'engineering', name: 'Armería' },
  { id: 'barracks', name: 'Cuartel' },
];

const mats = new Map<string, THREE.Material>();
/** `front` wins depth ties: for surfaces that lie flush on the rock (room floors and ceilings). */
function mat(
  color: number,
  opts: { emissive?: number; intensity?: number; metal?: number; rough?: number; opacity?: number; front?: boolean } = {},
): THREE.Material {
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
      polygonOffset: !!opts.front,
      polygonOffsetFactor: opts.front ? -1 : 0,
      polygonOffsetUnits: opts.front ? -4 : 0,
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

/** Floor centre of a dug room (row 0 is the first level under the command rooms). */
function slotOrigin(row: number, col: number): THREE.Vector3 {
  return new THREE.Vector3((col - (BASE_COLS - 1) / 2) * (ROOM_W + GAP), -(row + 1) * (ROOM_H + GAP), 0);
}

function fixedOrigin(index: number): THREE.Vector3 {
  return new THREE.Vector3(-GRID_W / 2 + FIXED_W / 2 + index * (FIXED_W + GAP), 0, 0);
}

/** Props that make each facility recognisable at a glance. */
function facilityProps(id: FacilityId): THREE.Group {
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
  }
  return g;
}

/** Scaffolding, crates and a hazard stripe: a facility being built. */
function constructionProps(progress: number): THREE.Group {
  const g = new THREE.Group();
  const steel = mat(0x9aa4ad, { metal: 0.6 });
  for (const x of [-2.4, -0.8, 0.8, 2.4]) g.add(box(0.08, ROOM_H * 0.85, 0.08, steel, x, 0, -1.2));
  for (const y of [0.9, 1.9]) g.add(box(5, 0.08, 0.08, steel, 0, y, -1.2));
  const p = Math.max(0.05, progress);
  g.add(box(5.6 * p, 0.12, 0.12, mat(0x2a2a10, { emissive: 0xffc93a, intensity: 1.6 }), -2.8 + 2.8 * p, 0.02, 1.6));
  g.add(box(0.9, 0.7, 0.9, mat(0x8f6c45), 1.8, 0, 0.6), box(0.7, 0.5, 0.7, mat(0x8f6c45), 2.6, 0, 0.9));
  const lamp = box(0.2, 0.2, 0.2, mat(0x2a1a00, { emissive: 0xffa020, intensity: 2.5 }), -2.6, ROOM_H * 0.8, 1);
  lamp.userData.blink = 0;
  g.add(lamp);
  return g;
}

/** Bare rock with loose boulders (and a drill when it is being dug out). */
function rockProps(digging: boolean, seed: number): THREE.Group {
  const g = new THREE.Group();
  g.add(box(ROOM_W, ROOM_H, ROOM_D, mat(0x3a332c, { rough: 1 })));
  for (let i = 0; i < 4; i++) {
    const r = ((seed * 13 + i * 7) % 10) / 10;
    const b = new THREE.Mesh(new THREE.DodecahedronGeometry(0.3 + r * 0.3, 0), mat(0x4a4038, { rough: 1 }));
    b.position.set((r - 0.5) * ROOM_W * 0.8 + i * 0.3, 0.3, ROOM_D / 2 + 0.2);
    g.add(b);
  }
  if (digging) {
    const drill = new THREE.Group();
    drill.add(box(1.2, 0.8, 1, mat(0xffa630, { metal: 0.5 })));
    const bit = new THREE.Mesh(new THREE.ConeGeometry(0.35, 1.2, 10), mat(0xb8c2cc, { metal: 0.8 }));
    bit.rotation.z = Math.PI / 2;
    bit.position.set(-1.1, 0.45, 0);
    bit.userData.spinX = true;
    drill.add(bit);
    drill.position.set(0.5, 0, ROOM_D / 2 + 0.6);
    g.add(drill);
  }
  return g;
}

/** Floor, back wall, side walls, ceiling and a light: a room cut into the rock. */
function roomShell(width: number, wallColor: number): THREE.Group {
  const g = new THREE.Group();
  // Floor and ceiling lie flush on the rock storeys: they must win the depth tie.
  g.add(box(width, 0.15, ROOM_D, mat(0x4a525c, { front: true }), 0, -0.15, 0));
  g.add(box(width, ROOM_H, 0.15, mat(wallColor), 0, 0, -ROOM_D / 2));
  for (const x of [-width / 2, width / 2]) g.add(box(0.15, ROOM_H, ROOM_D, mat(wallColor), x, 0, 0));
  g.add(box(width, 0.15, ROOM_D, mat(0x2a3036, { front: true }), 0, ROOM_H, 0));
  const light = new THREE.PointLight(0xdfefff, 6, 7, 2);
  light.position.set(0, ROOM_H - 0.4, 0.5);
  g.add(light);
  return g;
}

/**
 * Voxel art (M5): parts that tick() animates by transform or visibility are
 * voxelized one by one and keep their userData; the ship's engine glows (their
 * material is animated), hotspots and the roof doors stay as they are.
 */
const VOXEL_SET: StaticVoxelOptions = {
  animated: ['spinY', 'spinX', 'swing', 'pulse', 'blink', 'showcase'],
  keep: ['engine', 'hotspot', 'roofSide'],
};

/** Room props are voxelized once per kind and then cloned, so rebuilding the base stays cheap. */
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

/** Invisible box covering a room: what the pointer actually hits. */
function hotspot(id: string, width: number, origin: THREE.Vector3): THREE.Mesh {
  const hit = new THREE.Mesh(new THREE.BoxGeometry(width, ROOM_H, ROOM_D), new THREE.MeshBasicMaterial({ visible: false }));
  hit.position.copy(origin).add(new THREE.Vector3(0, ROOM_H / 2, 0));
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
 * The Bastion in cross-section, like the Avenger: hangar on the surface, the
 * four command rooms below it and a 3×3 grid of rooms dug into the rock. Every
 * room is a hotspot that opens its screen; the hangar holds the squad lineup
 * and the barracks a pedestal for the soldier being inspected.
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

  constructor(private readonly onPick: (hotspot: string) => void) {
    this.scene.background = new THREE.Color(0x070c11);
    this.scene.add(new THREE.HemisphereLight(0xc4dcff, 0x2a241e, 1.2));
    const key = new THREE.DirectionalLight(0xfff0dc, 1.6);
    key.position.set(8, 16, 16);
    key.castShadow = true;
    key.shadow.mapSize.set(1024, 1024);
    Object.assign(key.shadow.camera, { left: -18, right: 18, top: 10, bottom: -18 });
    this.scene.add(key);
    this.buildShell();
    voxelizeStatic(this.scene, VOXEL_SET);
    this.scene.add(this.grid, this.lineup, this.showcase, this.partnerMarker);
    const edges = (w: number, color: number) =>
      new THREE.LineSegments(new THREE.EdgesGeometry(new THREE.BoxGeometry(w + 0.1, ROOM_H + 0.1, 0.1)), new THREE.LineBasicMaterial({ color, transparent: true }));
    this.frame = edges(ROOM_W, 0x54d1ff);
    this.hoverFrame = edges(ROOM_W, 0xffffff);
    this.frame.visible = this.hoverFrame.visible = false;
    this.scene.add(this.frame, this.hoverFrame);
  }

  // ------------------------------------------------------------- shots

  overview(): Shot {
    const target = new THREE.Vector3(-3.4, -2.2, 0);
    return { pos: target.clone().add(new THREE.Vector3(-3, 3.8, 34)), target };
  }

  /** Close-up of a command room (or a dug room), seen from the front. */
  room(id: FixedRoom | string): Shot {
    const centre = this.roomCentre(id);
    const target = centre.clone().add(new THREE.Vector3(0, 1.3, 0));
    return { pos: target.clone().add(new THREE.Vector3(0.6, 1.1, 8.8)), target };
  }

  /** The squad lined up in the hangar, in front of the dropship. */
  hangar(): Shot {
    const target = new THREE.Vector3(HANGAR_X, SURFACE + 1.05, 0.9);
    return { pos: new THREE.Vector3(HANGAR_X, SURFACE + 1.9, 10.2), target, fov: 42 };
  }

  /** The whole facility grid, framed to the right of the side panel. */
  facilities(): Shot {
    const target = new THREE.Vector3(-4.8, -6.2, 0);
    return { pos: target.clone().add(new THREE.Vector3(0, 1.4, 27)), target };
  }

  /** Low and wide on the hangar, looking up at the roof the dropship leaves through. */
  takeoff(): Shot {
    const target = new THREE.Vector3(HANGAR_X, SURFACE + 2.8, -1.2);
    return { pos: new THREE.Vector3(HANGAR_X + 2.2, SURFACE + 0.8, 8.6), target, fov: 56 };
  }

  /** The soldier on the barracks pedestal, framed between the side panels. */
  armory(): Shot {
    const p = this.pedestalPos();
    const target = p.clone().add(new THREE.Vector3(0, 1.15, 0));
    return { pos: target.clone().add(new THREE.Vector3(0, 0.3, 5.3)), target, fov: 38 };
  }

  private roomCentre(id: string): THREE.Vector3 {
    const fixed = FIXED.findIndex((f) => f.id === id);
    if (fixed >= 0) return fixedOrigin(fixed);
    const m = /^r(\d)c(\d)$/.exec(id.replace('slot:', ''));
    return m ? slotOrigin(Number(m[1]), Number(m[2])) : new THREE.Vector3();
  }

  private pedestalPos(): THREE.Vector3 {
    return fixedOrigin(3).add(new THREE.Vector3(0, 0, 0.6));
  }

  // ------------------------------------------------------------- shell

  private buildShell(): void {
    const rock = mat(0x2a241f, { rough: 1 });
    const width = GRID_W + 8;
    const bottom = -(BASE_ROWS + 1) * (ROOM_H + GAP) + ROOM_H - 2.5;
    this.scene.add(box(width, SURFACE - bottom, 1, rock, 0, bottom, -ROOM_D / 2 - 0.5));
    const half = GRID_W / 2;
    const side = (width - GRID_W) / 2;
    for (let row = -1; row <= BASE_ROWS; row++) {
      const top = row === -1 ? 0 : slotOrigin(row, 0).y;
      if (row === BASE_ROWS) {
        this.scene.add(box(width, top + ROOM_H - bottom, ROOM_D, rock, 0, bottom, 0));
        continue;
      }
      this.scene.add(box(width, GAP, ROOM_D, rock, 0, top - GAP, 0));
      this.scene.add(box(side, ROOM_H, ROOM_D, rock, -half - side / 2, top, 0), box(side, ROOM_H, ROOM_D, rock, half + side / 2, top, 0));
      if (row === -1) {
        for (let i = 1; i < 4; i++) this.scene.add(box(GAP, ROOM_H, ROOM_D, rock, fixedOrigin(i).x - FIXED_W / 2 - GAP / 2, top, 0));
      } else {
        for (let col = 1; col < BASE_COLS; col++) this.scene.add(box(GAP, ROOM_H, ROOM_D, rock, slotOrigin(row, col).x - (ROOM_W + GAP) / 2, top, 0));
      }
    }

    // Surface: ground, hangar with the dropship, comms mast and radar.
    this.scene.add(box(width + 20, GAP, ROOM_D + 14, mat(0x3d4a33, { rough: 1 }), 0, SURFACE - GAP, 0));
    const hangar = new THREE.Group();
    hangar.add(box(12, 0.15, 6, mat(0x56606a), 0, 0, 0));
    hangar.add(box(12, 3.4, 0.2, mat(0x4a525c), 0, 0, -3));
    for (const x of [-6, 6]) hangar.add(box(0.2, 3.4, 6, mat(0x6d7782), x, 0, 0));
    // Roof doors: they slide apart for take-off.
    for (const side of [-1, 1]) {
      const door = box(6, 0.2, 6, mat(0x6d7782, { opacity: 0.25 }), side * 3, 3.4, 0);
      door.userData.roofSide = side;
      this.roofDoors.push(door);
      hangar.add(door);
    }
    for (const x of [-4, 0, 4]) {
      const lamp = new THREE.SpotLight(0xdfefff, 30, 12, 0.7, 0.5, 1.5);
      lamp.position.set(x, 3.2, 1);
      lamp.target.position.set(x, 0, 1);
      hangar.add(lamp, lamp.target);
    }
    // Hazard stripes along the edge of the launch pad.
    hangar.add(box(11.6, 0.02, 0.18, mat(0x2a2a10, { emissive: 0xffc93a, intensity: 0.8 }), 0, 0.16, 2.7));
    this.buildShip();
    this.ship.position.set(0, 0.15, -1.2);
    hangar.add(this.ship);
    hangar.position.set(HANGAR_X, SURFACE, 0);
    const hangarHit = new THREE.Mesh(new THREE.BoxGeometry(12, 3.4, 6), new THREE.MeshBasicMaterial({ visible: false }));
    hangarHit.position.set(HANGAR_X, SURFACE + 1.7, 0);
    hangarHit.userData.hotspot = 'hangar';
    this.hotspots.push(hangarHit);
    const hangarLabel = label('Hangar', 'room-label fixed');
    hangarLabel.position.set(HANGAR_X, SURFACE + 3.9, 3);
    this.scene.add(hangar, hangarHit, hangarLabel);
    for (const [x, h] of [[6, 4.5], [8.4, 3]] as const) {
      this.scene.add(cylinder(0.07, h, mat(0x9aa4ad, { metal: 0.6 }), x, SURFACE, -1));
      const light = box(0.2, 0.2, 0.2, mat(0x2a0a0a, { emissive: 0xff3a3a, intensity: 2.5 }), x, SURFACE + h, -1);
      light.userData.blink = x;
      this.scene.add(light);
    }
    const radar = new THREE.Mesh(new THREE.SphereGeometry(1, 20, 12, 0, Math.PI * 2, 0, Math.PI / 3), mat(0xd8dee4, { metal: 0.6, rough: 0.3 }));
    radar.rotation.x = -Math.PI / 2.5;
    radar.position.set(3.4, SURFACE + 1.6, -0.5);
    radar.userData.spinY = true;
    this.scene.add(radar, cylinder(0.1, 1.2, mat(0x8a96a3), 3.4, SURFACE, -0.5));

    // Stars over the surface.
    const stars: number[] = [];
    for (let i = 0; i < 500; i++) stars.push((Math.random() - 0.5) * 160, SURFACE + 8 + Math.random() * 50, -30 - Math.random() * 40);
    this.scene.add(new THREE.Points(new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(stars, 3)), new THREE.PointsMaterial({ color: 0x9fb8cc, size: 0.25 })));

    // The four command rooms.
    FIXED.forEach((room, i) => {
      const o = fixedOrigin(i);
      const g = roomShell(FIXED_W, 0x223040);
      g.position.copy(o);
      g.add(this.fixedProps(room.id));
      const tag = label(room.name, 'room-label command');
      tag.position.set(0, ROOM_H + 0.1, ROOM_D / 2);
      g.add(tag);
      this.scene.add(g);
      const hit = hotspot(room.id, FIXED_W, o);
      this.scene.add(hit);
      this.hotspots.push(hit);
    });
    const pedestal = cylinder(0.55, 0.25, mat(0x2a3036, { metal: 0.5 }), 0, 0, 0);
    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.56, 0.03, 6, 32).rotateX(Math.PI / 2), mat(0x0a1a2a, { emissive: 0x54d1ff, intensity: 1.8 }));
    ring.position.y = 0.26;
    const stand = new THREE.Group();
    stand.add(pedestal, ring);
    stand.position.copy(this.pedestalPos());
    this.scene.add(stand);
    this.showcase.position.copy(this.pedestalPos()).add(new THREE.Vector3(0, 0.25, 0));
  }

  private fixedProps(id: FixedRoom): THREE.Group {
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
        for (const y of [0.5, 1.3, 2.1]) {
          g.add(box(1.0, 0.06, 0.5, mat(0x3a444e, { metal: 0.5 }), 1.55, y, -1.65));
          g.add(box(0.3, 0.3, 0.3, mat(0x8f6c45), 1.35, y + 0.06, -1.65), box(0.25, 0.2, 0.3, mat(0x56606a, { metal: 0.5 }), 1.8, y + 0.06, -1.65));
        }
        const forge = new THREE.PointLight(0xffa040, 4, 4, 2);
        forge.position.set(-1, 1.4, 0);
        g.add(forge);
        break;
      }
      case 'barracks':
        for (const x of [-1.6, -0.6, 0.6, 1.6]) g.add(box(0.8, 1.9, 0.45, mat(0x4a5560, { metal: 0.4 }), x, 0, -1.65));
        break;
    }
    return g;
  }

  private buildShip(): void {
    const hull = mat(0x4a5560, { metal: 0.5, rough: 0.45 });
    this.ship.add(box(5.2, 1.1, 1.6, hull, 0, 0.5, 0));
    this.ship.add(box(1.4, 0.4, 5.4, mat(0x3a444e, { metal: 0.5 }), 0.5, 0.9, 0));
    this.ship.add(box(0.8, 0.45, 1.0, mat(0x0a1a2a, { emissive: 0x3fb8ff, intensity: 1.3 }), -2.6, 0.9, 0));
    for (const z of [-2.4, 2.4]) {
      const engine = cylinder(0.32, 0.9, mat(0x2a3036, { metal: 0.6 }), 0.5, 0.7, z);
      engine.rotation.z = Math.PI / 2;
      const glow = new THREE.Mesh(new THREE.CircleGeometry(0.26, 16), mat(0x0a1a2a, { emissive: 0x6ad8ff, intensity: 0.6 }));
      glow.position.set(1.0, 1.15, z);
      glow.rotation.y = Math.PI / 2;
      glow.userData.engine = true;
      this.ship.add(engine, glow);
    }
  }

  // ------------------------------------------------------------ update

  /** Rebuilds the dug rooms when the base changes. */
  setBase(c: CampaignState): void {
    const key = JSON.stringify(c.base);
    if (key === this.gridKey) return;
    this.gridKey = key;
    this.grid.traverse((o) => {
      if (o instanceof CSS2DObject) o.element.remove();
    });
    this.grid.clear();
    for (const h of [...this.hotspots]) if (String(h.userData.hotspot).startsWith('slot:')) this.hotspots.splice(this.hotspots.indexOf(h), 1);
    const work = c.base.construction;
    for (const slot of c.base.slots) {
      const o = slotOrigin(slot.row, slot.col);
      const g = new THREE.Group();
      g.position.copy(o);
      const building = work?.slot === slot.id;
      let title: string;
      if (!slot.excavated) {
        const seed = slot.row * 3 + slot.col;
        g.add(voxelProps(`rock:${building}:${seed}`, () => rockProps(building, seed)));
        title = building ? `Excavando · ${work!.daysLeft} d` : 'Roca';
      } else {
        const shell = roomShell(ROOM_W, slot.facility ? 0x2a3a4a : 0x2a3036);
        voxelizeStatic(shell, VOXEL_SET);
        g.add(shell);
        if (slot.facility) {
          const facility = slot.facility;
          g.add(voxelProps(`facility:${facility}`, () => facilityProps(facility)));
          title = FACILITIES[slot.facility].name;
        } else if (building && work?.facility) {
          g.add(voxelProps(`build:${work.daysLeft}/${work.total}`, () => constructionProps(1 - work.daysLeft / work.total)));
          title = `${FACILITIES[work.facility].name} · ${work.daysLeft} d`;
        } else {
          title = 'Sala vacía';
        }
      }
      // rock / empty: the screens show these only where they can be acted on (Instalaciones).
      const kind = !slot.excavated ? ' rock' : !slot.facility && !building ? ' empty' : '';
      const tag = label(title, `room-label${slot.facility ? ' built' : ''}${building ? ' busy' : ''}${building ? '' : kind}`);
      tag.position.set(0, ROOM_H + 0.1, ROOM_D / 2);
      g.add(tag);
      this.grid.add(g);
      const hit = hotspot(`slot:${slot.id}`, ROOM_W, o);
      this.grid.add(hit);
      this.hotspots.push(hit);
    }
  }

  /** Room labels only make sense in the overview shots; close-ups hide them. */
  setLabels(on: boolean): void {
    this.scene.traverse((o) => {
      if (o instanceof CSS2DObject && !o.element.classList.contains('partner-tag')) o.visible = on;
    });
  }

  /** Highlights a dug room (facilities screen) or nothing. */
  select(slot: string | null): void {
    this.frame.visible = !!slot;
    if (slot) this.frame.position.copy(this.roomCentre(slot)).add(new THREE.Vector3(0, ROOM_H / 2, ROOM_D / 2 + 0.05));
  }

  /** Coloured marker over the room the partner is looking at. */
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
    const pos = room === 'hangar' ? new THREE.Vector3(HANGAR_X, SURFACE + 4.4, 3) : this.roomCentre(room).add(new THREE.Vector3(0, ROOM_H + 0.7, ROOM_D / 2));
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
    shown.forEach((entry, i) => {
      const x = this.padPosition(i, shown.length).x;
      const pad = new THREE.Mesh(new THREE.RingGeometry(0.42, 0.5, 28).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: entry ? new THREE.Color(entry.color) : 0x3a4652, transparent: true, opacity: 0.8 }));
      pad.position.set(x, SURFACE + 0.17, 1.2);
      this.lineup.add(pad);
      if (!entry) return;
      if (entry.status === 'dead') {
        // A memorial: a dark plinth with a candle-like glow.
        this.lineup.add(box(0.5, 0.9, 0.3, mat(0x1a1d22, { metal: 0.3 }), x, SURFACE + 0.15, 1.2));
        this.lineup.add(box(0.12, 0.12, 0.12, mat(0x2a1a00, { emissive: 0xffb050, intensity: 2.2 }), x, SURFACE + 1.06, 1.2));
        return;
      }
      const rig = buildUnit(entry.template, entry.color, entry.appearance, entry.gear);
      rig.root.position.set(x, SURFACE + 0.15, 1.2);
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

  /** Where soldier `i` of a lineup of `count` stands (on the launch pad, centred). */
  padPosition(i: number, count: number): THREE.Vector3 {
    return new THREE.Vector3(HANGAR_X + (i - (count - 1) / 2) * 1.3, SURFACE + 0.15, 1.2);
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

  hover(hot: string | null): void {
    const slot = hot?.startsWith('slot:') ? hot.slice(5) : null;
    const fixed = FIXED.findIndex((f) => f.id === hot);
    this.hoverFrame.visible = !!slot || fixed >= 0;
    if (slot) {
      this.hoverFrame.scale.x = 1;
      this.hoverFrame.position.copy(this.roomCentre(slot)).add(new THREE.Vector3(0, ROOM_H / 2, ROOM_D / 2 + 0.06));
    } else if (fixed >= 0) {
      this.hoverFrame.scale.x = FIXED_W / ROOM_W;
      this.hoverFrame.position.copy(fixedOrigin(fixed)).add(new THREE.Vector3(0, ROOM_H / 2, ROOM_D / 2 + 0.06));
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
    // Take-off: the squad walks into the ship, the engines flare, and it leaves through the roof.
    const since = this.launching ? (performance.now() - this.launching) / 1000 : -1;
    if (since >= 0) {
      const door = new THREE.Vector3(HANGAR_X, SURFACE + 0.15, -0.6);
      this.rigs.forEach((rig, i) => {
        const k = Math.min(1, Math.max(0, (since - i * 0.08) / BOARD_SECONDS));
        const from = rig.root.userData.from as THREE.Vector3;
        rig.root.position.lerpVectors(from, door, k * k);
        if (k > 0) rig.root.userData.yaw = Math.atan2(door.x - from.x, door.z - from.z);
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
    this.ship.traverse((o) => {
      if (o.userData.engine) ((o as THREE.Mesh).material as THREE.MeshStandardMaterial).emissiveIntensity = since >= 0 ? 3 + Math.sin(time * 40) : 0.6;
    });
    const open = since >= 0 ? Math.min(1, Math.max(0, (since - 0.4) / 1.2)) : 0;
    for (const door of this.roofDoors) door.position.x = door.userData.roofSide * (3 + easeInOut(open) * 5.4);
    const lift = since - BOARD_SECONDS - 0.3;
    this.ship.position.y = 0.15 + (lift > 0 ? Math.pow(lift, 2) * 3.2 : Math.sin(time * 1.2) * 0.04);
    this.ship.rotation.x = lift > 0 ? Math.min(0.12, lift * 0.1) : 0;
  }
}
