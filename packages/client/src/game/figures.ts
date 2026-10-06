import * as THREE from 'three';
import type { Appearance, BuildId, HeadId, Tier } from '@bastion/engine';
import type { UnitRig } from '../game/models';

// Articulated unit models: a real skeleton (pelvis, spine, neck, shoulders,
// elbows, hips, knees, ankles) dressed with primitive armour pieces. Arms
// reach the weapon with two-bone IK, legs bend to crouch and run, and the
// whole thing still satisfies the game's `UnitRig` contract (root, yaw,
// pivot, gunPivot, muzzle, flash, drone, hip, chest) so it can replace the
// placeholder models later. Animation goes through `applyPose`.
//
//   root → yaw → pivot (pelvis, at hip height)
//                 ├ hipL/R → kneeL/R → ankleL/R          (legs)
//                 └ spine → neck → head
//                         ├ shoulderL/R → elbowL/R        (arms)
//                         ├ gunPivot → weapon, grips, muzzle
//                         └ flash
//
// Bones hang along −Y from their joint; +Z is forward; the figure's right
// side is −X.

/** Pose values, the same ones the game's unit views animate. */
export interface FigurePose {
  crouch: number;
  aim: number;
  run: number;
  recoil: number;
  hit: number;
  flash: number;
  fall: number;
  lean: number;
  hop: number;
  yaw: number;
  phase: number;
}

export interface FigureRig extends UnitRig {
  applyPose(p: FigurePose, time: number): void;
}

export type FigureTemplate = 'assault' | 'grenadier' | 'sharpshooter' | 'specialist' | 'trooper' | 'officer' | 'lancer' | 'xenoid' | 'mec' | 'sectoid' | 'zombie' | 'relay' | 'vip';

/** Templates `buildFigure` models; anything else falls back to a generic soldier. */
export const FIGURE_TEMPLATES: readonly FigureTemplate[] = [
  'assault',
  'grenadier',
  'sharpshooter',
  'specialist',
  'trooper',
  'officer',
  'lancer',
  'xenoid',
  'mec',
  'sectoid',
  'zombie',
  'relay',
  'vip',
];

const DOWN = new THREE.Vector3(0, -1, 0);

// ------------------------------------------------------------- materials

const matCache = new Map<string, THREE.MeshStandardMaterial>();
function mat(color: THREE.ColorRepresentation, o: { glow?: THREE.ColorRepresentation; metal?: number; rough?: number } = {}): THREE.MeshStandardMaterial {
  const c = new THREE.Color(color);
  const key = `${c.getHex()}|${o.glow ?? ''}|${o.metal ?? ''}|${o.rough ?? ''}`;
  let m = matCache.get(key);
  if (!m) {
    m = new THREE.MeshStandardMaterial({
      color: c,
      emissive: o.glow ?? 0x000000,
      emissiveIntensity: o.glow !== undefined ? 1.8 : 0,
      metalness: o.metal ?? 0.15,
      roughness: o.rough ?? 0.6,
    });
    matCache.set(key, m);
  }
  return m;
}

interface Palette {
  suit: THREE.Material;
  armour: THREE.Material;
  plate: THREE.Material;
  dark: THREE.Material;
  visor: THREE.Material;
  metal: THREE.Material;
  skin: THREE.Material;
}

/** Visor material: the original cyan keeps its hand-picked glow, other colours get the same hue, deeper. */
function visorMat(color: THREE.ColorRepresentation): THREE.MeshStandardMaterial {
  const c = new THREE.Color(color);
  if (c.getHex() === 0x9fe3ff) return mat(c, { glow: 0x2a8fbf });
  const hsl = c.getHSL({ h: 0, s: 0, l: 0 }, THREE.SRGBColorSpace);
  return mat(c, {
    glow: new THREE.Color().setHSL(hsl.h, hsl.s * 0.64, hsl.l * 0.57, THREE.SRGBColorSpace).getHex(),
  });
}

/** Squad colours; a customized soldier (`look`) brings its own armour, plates and visor. */
function soldierPalette(accent: string, look?: Appearance): Palette {
  const armour = look ? new THREE.Color(look.primary) : new THREE.Color(accent).lerp(new THREE.Color(0x56605a), 0.55);
  return {
    suit: mat(0x30373a, { rough: 0.85 }),
    armour: mat(armour, { rough: 0.5 }),
    plate: mat(look?.secondary ?? accent, { rough: 0.4 }),
    dark: mat(0x1d2124, { rough: 0.7 }),
    visor: visorMat(look?.visor ?? 0x9fe3ff),
    metal: mat(0x2a2f33, { metal: 0.6, rough: 0.4 }),
    skin: mat(0xc99a7a, { rough: 0.8 }),
  };
}

function advent(plate: number, visor: number): Palette {
  return {
    suit: mat(0x1e1a1c, { rough: 0.8 }),
    armour: mat(0x4a3439, { rough: 0.45, metal: 0.2 }),
    plate: mat(plate, { rough: 0.4, metal: 0.25 }),
    dark: mat(0x141113, { rough: 0.7 }),
    visor: mat(new THREE.Color(visor).lerp(new THREE.Color(0xffffff), 0.3), {
      glow: visor,
    }),
    metal: mat(0x3a3f45, { metal: 0.6, rough: 0.35 }),
    skin: mat(0x8a9099, { rough: 0.45 }),
  };
}

// ------------------------------------------------------------- skeleton

interface Body {
  /** Gap from the pelvis pivot down to the hip joints. */
  hipDrop: number;
  hipX: number;
  thigh: number;
  shin: number;
  foot: number;
  spineY: number;
  shoulderX: number;
  shoulderY: number;
  neckY: number;
  upper: number;
  fore: number;
  /** Limb radius multiplier. */
  thick: number;
  /** Forward bend of the spine at rest (xenoid hunch). */
  hunch: number;
  /** Reverse-jointed legs (MEC). */
  digitigrade: boolean;
  /** Box limbs instead of round ones (machines). */
  boxy: boolean;
  scale: number;
}

const HUMAN: Body = {
  hipDrop: 0.04,
  hipX: 0.095,
  thigh: 0.36,
  shin: 0.35,
  foot: 0.075,
  spineY: 0.07,
  shoulderX: 0.215,
  shoulderY: 0.43,
  neckY: 0.48,
  upper: 0.29,
  fore: 0.27,
  thick: 1.15,
  hunch: 0,
  digitigrade: false,
  boxy: false,
  scale: 1,
};

/** Standing angles of the reverse-jointed legs. */
const DIGI = { thigh: -0.5, knee: 1.05 };

class Figure {
  readonly root = new THREE.Group();
  readonly yaw = new THREE.Group();
  readonly pivot = new THREE.Group();
  readonly spine = new THREE.Group();
  readonly neck = new THREE.Group();
  readonly head = new THREE.Group();
  readonly gunPivot = new THREE.Group();
  readonly muzzle = new THREE.Object3D();
  readonly joints: Record<'shoulderL' | 'shoulderR' | 'elbowL' | 'elbowR' | 'hipL' | 'hipR' | 'kneeL' | 'kneeR' | 'ankleL' | 'ankleR', THREE.Group>;
  flash!: THREE.Mesh;
  drone: THREE.Object3D | null = null;
  gripR: THREE.Object3D | null = null;
  gripL: THREE.Object3D | null = null;
  /** Cloth for the sharpshooter's hood. */
  hoodCloth: THREE.Material | null = null;
  /** Arms that belong to the weapon (MEC gun arm) are not solved. */
  freeR = true;
  /** Where the free left hand goes while aiming (sectoid's hand to the temple), in spine space. */
  leftAim: THREE.Vector3 | null = null;
  /** Free arms swing while running (unarmed VIP). */
  swingArms = false;
  /** Lurching, dragging gait and tilted head (zombie), 0–1. */
  shamble = 0;
  /** No breathing or stance (machines that stand still, like the relay). */
  still = false;
  /** Parts that spin on their own (relay ring). */
  spinners: { obj: THREE.Object3D; speed: number }[] = [];
  gunRest = new THREE.Vector3();
  chest = 1.2;
  /** Torso width multiplier, for armour added after the torso. */
  torsoWidth = 1;
  /** Weapon body and grip (soldier guns change with the weapon tier). */
  gunBody: THREE.Material;
  gunGrip: THREE.Material;

  constructor(
    readonly body: Body,
    readonly pal: Palette,
  ) {
    this.gunBody = pal.metal;
    this.gunGrip = pal.dark;
    const g = () => new THREE.Group();
    this.joints = {
      shoulderL: g(),
      shoulderR: g(),
      elbowL: g(),
      elbowR: g(),
      hipL: g(),
      hipR: g(),
      kneeL: g(),
      kneeR: g(),
      ankleL: g(),
      ankleR: g(),
    };
    const j = this.joints;
    this.root.add(this.yaw);
    this.yaw.add(this.pivot);
    this.yaw.scale.setScalar(body.scale);
    this.pivot.add(this.spine, j.hipL, j.hipR);
    this.spine.position.y = body.spineY;
    this.spine.add(this.neck, j.shoulderL, j.shoulderR, this.gunPivot);
    this.neck.position.y = body.neckY;
    this.neck.add(this.head);
    this.head.position.y = 0.1;
    j.shoulderL.position.set(body.shoulderX, body.shoulderY, 0);
    j.shoulderR.position.set(-body.shoulderX, body.shoulderY, 0);
    j.shoulderL.add(j.elbowL);
    j.shoulderR.add(j.elbowR);
    j.elbowL.position.y = -body.upper;
    j.elbowR.position.y = -body.upper;
    for (const [hip, knee, ankle, side] of [
      [j.hipL, j.kneeL, j.ankleL, 1],
      [j.hipR, j.kneeR, j.ankleR, -1],
    ] as const) {
      hip.position.set(side * body.hipX, -body.hipDrop, 0);
      hip.add(knee);
      knee.position.y = -body.thigh;
      knee.add(ankle);
      ankle.position.y = -body.shin;
    }
    this.gunPivot.add(this.muzzle);
    this.pivot.position.y = this.standingHeight(0, 0);
  }

  // ------------------------------------------------------------ dressing

  part(parent: THREE.Object3D, geometry: THREE.BufferGeometry, material: THREE.Material, x: number, y: number, z: number, rot?: [number, number, number]): THREE.Mesh {
    const m = new THREE.Mesh(geometry, material);
    m.position.set(x, y, z);
    if (rot) m.rotation.set(...rot);
    m.castShadow = true;
    m.receiveShadow = true;
    parent.add(m);
    return m;
  }

  box(parent: THREE.Object3D, w: number, h: number, d: number, material: THREE.Material, x: number, y: number, z: number, rot?: [number, number, number]): THREE.Mesh {
    return this.part(parent, new THREE.BoxGeometry(w, h, d), material, x, y, z, rot);
  }

  /** A bone segment hanging from its joint. */
  bone(parent: THREE.Object3D, rTop: number, rBottom: number, length: number, material: THREE.Material): THREE.Mesh {
    const t = this.body.thick;
    const geometry = this.body.boxy ? new THREE.BoxGeometry(rTop * 2 * t, length, rTop * 2.2 * t) : new THREE.CylinderGeometry(rTop * t, rBottom * t, length, 8);
    return this.part(parent, geometry.translate(0, -length / 2, 0), material, 0, 0, 0);
  }

  ball(parent: THREE.Object3D, r: number, material: THREE.Material, x: number, y: number, z: number, scale?: [number, number, number]): THREE.Mesh {
    const m = this.part(parent, new THREE.SphereGeometry(r, 14, 10), material, x, y, z);
    if (scale) m.scale.set(...scale);
    return m;
  }

  /** Default limbs: suit underneath, armour on thighs, knees, shins and forearms. */
  limbs(o: { kneePads?: boolean; gauntlets?: boolean } = {}): this {
    const { pal, body: b, joints: j } = this;
    for (const side of ['L', 'R'] as const) {
      const hip = j[`hip${side}`];
      const knee = j[`knee${side}`];
      const ankle = j[`ankle${side}`];
      this.bone(hip, 0.075, 0.062, b.thigh, pal.suit);
      this.box(hip, 0.1 * b.thick, b.thigh * 0.5, 0.04, pal.armour, 0, -b.thigh * 0.4, 0.06 * b.thick);
      this.bone(knee, 0.06, 0.05, b.shin, pal.suit);
      if (o.kneePads !== false) this.box(knee, 0.095 * b.thick, 0.085, 0.05, pal.plate, 0, -0.01, 0.06 * b.thick);
      this.box(knee, 0.08 * b.thick, b.shin * 0.55, 0.04, pal.armour, 0, -b.shin * 0.45, 0.05 * b.thick);
      this.box(ankle, 0.1 * b.thick, b.foot, 0.22, pal.dark, 0, -b.foot / 2, 0.05);
      const shoulder = j[`shoulder${side}`];
      const elbow = j[`elbow${side}`];
      this.bone(shoulder, 0.056, 0.048, b.upper, pal.suit);
      this.bone(elbow, 0.05, 0.043, b.fore, o.gauntlets === false ? pal.suit : pal.armour);
      this.box(elbow, 0.065, 0.08, 0.065, pal.dark, 0, -b.fore - 0.035, 0.005);
    }
    return this;
  }

  /** Pelvis, belly and chest with front and back plates. */
  torso(o: { width?: number; pads?: 'small' | 'big' | 'gold' | 'none' } = {}): this {
    const { pal, spine } = this;
    const w = o.width ?? 1;
    this.torsoWidth = w;
    this.box(this.pivot, 0.3 * w, 0.14, 0.2, pal.suit, 0, 0, 0);
    this.box(this.pivot, 0.32 * w, 0.045, 0.215, pal.dark, 0, 0.06, 0);
    this.box(spine, 0.27 * w, 0.17, 0.18, pal.suit, 0, 0.09, 0);
    this.box(spine, 0.38 * w, 0.28, 0.23, pal.suit, 0, 0.31, 0);
    this.box(spine, 0.33 * w, 0.22, 0.04, pal.plate, 0, 0.32, 0.12);
    this.box(spine, 0.31 * w, 0.22, 0.04, pal.armour, 0, 0.31, -0.12);
    this.box(spine, 0.22 * w, 0.08, 0.035, pal.armour, 0, 0.13, 0.1);
    this.part(spine, new THREE.CylinderGeometry(0.075, 0.09, 0.06, 10), pal.armour, 0, 0.46, 0);
    if (o.pads !== 'none') {
      const big = o.pads === 'big';
      const padMat = o.pads === 'gold' ? mat(0xc9a13a, { metal: 0.7, rough: 0.3 }) : pal.plate;
      for (const side of [1, -1]) {
        this.box(spine, big ? 0.19 : 0.15, big ? 0.1 : 0.07, big ? 0.22 : 0.17, padMat, side * (this.body.shoulderX + (big ? 0.02 : 0)), this.body.shoulderY + 0.04, 0, [
          0,
          0,
          -side * 0.25,
        ]);
      }
    }
    this.flash = this.part(
      spine,
      new THREE.CapsuleGeometry(0.22 * w, 0.5, 4, 12),
      new THREE.MeshBasicMaterial({
        color: 0xff3a2a,
        transparent: true,
        opacity: 0,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      }),
      0,
      0.28,
      0,
    );
    this.flash.castShadow = false;
    this.flash.visible = false;
    this.chest = this.standingHeight(0, 0) + this.body.spineY + 0.3;
    return this;
  }

  /** Soldier heads (the barracks customizer offers them all) and the ADVENT helmet. */
  helmet(kind: HeadId | 'advent'): this {
    const { pal, neck, head } = this;
    this.part(neck, new THREE.CylinderGeometry(0.045, 0.05, 0.09, 8), pal.suit, 0, 0.04, 0);
    if (kind === 'advent') {
      this.ball(head, 0.12, pal.armour, 0, 0.03, 0, [1, 1.1, 1.22]);
      this.box(head, 0.17, 0.035, 0.05, pal.visor, 0, 0.02, 0.125);
      this.box(head, 0.03, 0.05, 0.22, pal.plate, 0, 0.15, -0.01);
      return this;
    }
    // Under a mask there is no face: in voxels the smaller part wins shared cells and would show through.
    if (kind !== 'mask') this.ball(head, 0.1, pal.skin, 0, 0, 0.01);
    if (kind === 'hood') {
      const cloth = this.hoodCloth ?? pal.armour;
      this.ball(head, 0.14, cloth, 0, 0.035, -0.025, [1, 1.1, 1.18]);
      this.part(head, new THREE.ConeGeometry(0.1, 0.16, 8).rotateX(-1.9), cloth, 0, 0.06, -0.16);
      this.part(this.spine, new THREE.CylinderGeometry(0.12, 0.22, 0.14, 10, 1, true), cloth, 0, 0.5, -0.01);
      this.box(head, 0.16, 0.05, 0.04, pal.visor, 0, 0.0, 0.115);
      return this;
    }
    if (kind !== 'helmet') return this.openHead(kind);
    this.ball(head, 0.12, pal.armour, 0, 0.035, -0.005, [1, 0.92, 1.05]);
    this.box(head, 0.18, 0.05, 0.05, pal.visor, 0, 0.005, 0.105);
    this.box(head, 0.03, 0.09, 0.08, pal.plate, 0.12, 0.0, 0.0);
    this.box(head, 0.03, 0.09, 0.08, pal.plate, -0.12, 0.0, 0.0);
    return this;
  }

  /**
   * Heads that show the face (beret, cap, bare) or cover it in cloth (mask).
   * Sized for 1/16 voxels: one row of eyes, and lenses, badge and earpiece
   * at least a voxel each, so every head reads by silhouette and colour.
   */
  private openHead(kind: 'beret' | 'cap' | 'mask' | 'bare'): this {
    const { pal, head } = this;
    const eyes = mat(0x1d1a18, { rough: 0.7 });
    const hair = mat(0x3a2a20, { rough: 0.9 });
    switch (kind) {
      case 'beret':
        this.ball(head, 0.105, hair, 0, 0.03, -0.02, [1, 0.7, 1.02]);
        this.box(head, 0.11, 0.022, 0.02, eyes, 0, 0.005, 0.1);
        this.part(head, new THREE.CylinderGeometry(0.125, 0.12, 0.05, 14), pal.plate, 0.015, 0.085, -0.01, [0.1, 0, 0.28]);
        this.box(head, 0.035, 0.035, 0.02, mat(0xc9a13a, { metal: 0.7, rough: 0.3 }), 0.06, 0.08, 0.1);
        break;
      case 'cap':
        this.box(head, 0.11, 0.022, 0.02, eyes, 0, 0.005, 0.1);
        this.ball(head, 0.112, pal.plate, 0, 0.045, -0.01, [1, 0.62, 1.05]);
        this.box(head, 0.15, 0.02, 0.1, pal.plate, 0, 0.05, 0.1, [-0.12, 0, 0]);
        // Goggles pushed up on the cap.
        this.box(head, 0.2, 0.025, 0.2, pal.dark, 0, 0.085, -0.005);
        this.box(head, 0.05, 0.04, 0.03, pal.visor, 0.045, 0.09, 0.095);
        this.box(head, 0.05, 0.04, 0.03, pal.visor, -0.045, 0.09, 0.095);
        break;
      case 'mask':
        this.ball(head, 0.112, pal.dark, 0, 0.01, 0, [1, 1.08, 1.06]);
        this.box(head, 0.045, 0.04, 0.03, pal.visor, 0.042, 0.015, 0.1);
        this.box(head, 0.045, 0.04, 0.03, pal.visor, -0.042, 0.015, 0.1);
        this.part(head, new THREE.CylinderGeometry(0.035, 0.04, 0.07, 10).rotateX(Math.PI / 2), pal.metal, 0, -0.055, 0.11);
        break;
      case 'bare':
        this.ball(head, 0.106, hair, 0, 0.04, -0.018, [1, 0.66, 1.05]);
        this.box(head, 0.11, 0.022, 0.02, eyes, 0, 0.005, 0.1);
        // Radio earpiece with a status light in the visor colour.
        this.box(head, 0.03, 0.05, 0.05, pal.dark, 0.105, -0.005, 0);
        this.box(head, 0.02, 0.02, 0.02, pal.visor, 0.12, 0.0, 0.0);
        break;
    }
    return this;
  }

  /**
   * Armour tiers over the kevlar (tier 1): plates on the upper arms, outer
   * thighs and collar (2); then heavier pads and glowing seams in the visor
   * colour, like XCOM's powered armour (3). Every piece is at least a voxel.
   */
  armourTier(tier: Tier): this {
    if (tier < 2) return this;
    const { pal, spine, body: b, joints: j } = this;
    const w = this.torsoWidth;
    for (const [side, s] of [
      ['L', 1],
      ['R', -1],
    ] as const) {
      this.box(j[`shoulder${side}`], 0.085 * b.thick, b.upper * 0.5, 0.12, pal.armour, s * 0.025, -b.upper * 0.32, 0);
      this.box(j[`hip${side}`], 0.04, b.thigh * 0.4, 0.11 * b.thick, pal.armour, s * 0.07 * b.thick, -b.thigh * 0.3, 0);
    }
    this.part(spine, new THREE.CylinderGeometry(0.11, 0.13, 0.07, 12), pal.plate, 0, 0.45, 0);
    this.box(spine, 0.24 * w, 0.07, 0.045, pal.plate, 0, 0.17, 0.1);
    if (tier < 3) return this;
    for (const s of [1, -1]) {
      this.box(spine, 0.2, 0.12, 0.24, pal.armour, s * (b.shoulderX + 0.03), b.shoulderY + 0.1, 0, [0, 0, -s * 0.3]);
      this.box(spine, 0.03, 0.16, 0.03, pal.visor, s * 0.12 * w, 0.31, 0.145);
    }
    this.box(spine, 0.12 * w, 0.03, 0.03, pal.visor, 0, 0.4, 0.145);
    for (const side of ['L', 'R'] as const) {
      this.box(j[`knee${side}`], 0.04, 0.03, 0.03, pal.visor, 0, -0.01, 0.095 * b.thick);
      this.box(j[`elbow${side}`], 0.03, 0.08, 0.03, pal.visor, 0, -b.fore * 0.5, 0.055 * b.thick);
    }
    this.box(spine, 0.22 * w, 0.16, 0.08, pal.dark, 0, 0.14, -0.15);
    return this;
  }

  /** Weapon tier materials, set before the gun is built: conventional, magnetic (blue steel), plasma (white). */
  gunMaterials(tier: Tier): this {
    if (tier === 2) {
      this.gunBody = mat(0x4a5866, { metal: 0.6, rough: 0.35 });
      this.gunGrip = mat(0x22282e, { rough: 0.6 });
    } else if (tier === 3) {
      this.gunBody = mat(0xc9ccd1, { metal: 0.3, rough: 0.35 });
      this.gunGrip = mat(0x4a4f56, { rough: 0.5 });
    }
    return this;
  }

  /** Glowing parts of tier 2 and 3 guns along a barrel of `length`: cyan coils, or a violet chamber and emitter. */
  gunGlow(tier: Tier, length: number): this {
    if (tier === 2) {
      const coil = mat(0x9fe8ff, { glow: 0x1f9fe0 });
      for (const k of [0.55, 0.75]) this.box(this.gunPivot, 0.08, 0.12, 0.05, coil, 0, -0.04, length * k);
      this.box(this.gunPivot, 0.025, 0.025, length * 0.3, coil, 0, 0.025, length * 0.3);
    } else if (tier === 3) {
      const core = mat(0xe2c8ff, { glow: 0x9a5aff });
      this.box(this.gunPivot, 0.085, 0.06, length * 0.35, core, 0, -0.03, length * 0.32);
      this.box(this.gunPivot, 0.05, 0.05, 0.05, core, 0, -0.035, length - 0.02);
    }
    return this;
  }

  /** Weapon in the gun pivot; grips are where the hands go, `length` the muzzle. */
  weapon(at: [number, number, number], parts: (gun: THREE.Group) => void, length: number, gripR: [number, number, number] | null, gripL: [number, number, number] | null): this {
    this.gunPivot.position.set(...at);
    this.gunRest.set(...at);
    parts(this.gunPivot);
    this.muzzle.position.set(0, -0.04, length);
    if (gripR) {
      this.gripR = new THREE.Object3D();
      this.gripR.position.set(...gripR);
      this.gunPivot.add(this.gripR);
    }
    if (gripL) {
      this.gripL = new THREE.Object3D();
      this.gripL.position.set(...gripL);
      this.gunPivot.add(this.gripL);
    }
    return this;
  }

  withDrone(): this {
    const drone = new THREE.Group();
    const body = new THREE.Mesh(new THREE.OctahedronGeometry(0.13, 0), mat(0xb8c4cc, { metal: 0.6, rough: 0.3 }));
    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.16, 0.02, 6, 20).rotateX(Math.PI / 2), mat(0x7dffb0, { glow: 0x2fd97a }));
    body.castShadow = true;
    drone.add(body, ring);
    drone.position.set(0.5, 1.9, -0.15);
    this.yaw.add(drone);
    this.drone = drone;
    return this;
  }

  // ------------------------------------------------------------- posing

  /** Pelvis height that keeps the feet on the ground for the given leg angles. */
  standingHeight(thigh: number, knee: number): number {
    const b = this.body;
    const t = b.digitigrade ? thigh + DIGI.thigh : thigh;
    const k = b.digitigrade ? knee + DIGI.knee : knee;
    return b.hipDrop + b.thigh * Math.cos(t) + b.shin * Math.cos(t + k) + b.foot;
  }

  private setArm(shoulder: THREE.Group, elbow: THREE.Group, upperDir: THREE.Vector3, foreDir: THREE.Vector3): void {
    shoulder.quaternion.setFromUnitVectors(DOWN, upperDir);
    const local = foreDir.clone().applyQuaternion(shoulder.quaternion.clone().invert());
    elbow.quaternion.setFromUnitVectors(DOWN, local.normalize());
  }

  /** Two-bone IK in spine space: the hand reaches `target`, the elbow bends towards `pole`. */
  private reach(side: 'L' | 'R', target: THREE.Vector3, pole: THREE.Vector3): void {
    const b = this.body;
    const shoulder = this.joints[`shoulder${side}`];
    const elbow = this.joints[`elbow${side}`];
    const a = b.upper;
    const c = b.fore + 0.035;
    const d = target.clone().sub(shoulder.position);
    const len = THREE.MathUtils.clamp(d.length(), Math.abs(a - c) + 0.01, a + c - 0.001);
    const dir = d.normalize();
    const alpha = Math.acos(THREE.MathUtils.clamp((a * a + len * len - c * c) / (2 * a * len), -1, 1));
    const axis = dir.clone().cross(pole);
    if (axis.lengthSq() < 1e-6) axis.set(1, 0, 0);
    axis.normalize();
    const upperDir = dir.clone().applyAxisAngle(axis, alpha);
    const elbowPos = shoulder.position.clone().addScaledVector(upperDir, a);
    const foreDir = target.clone().sub(elbowPos).normalize();
    this.setArm(shoulder, elbow, upperDir, foreDir);
  }

  private rest(side: 'L' | 'R', swing: number): void {
    const s = side === 'L' ? 1 : -1;
    this.setArm(
      this.joints[`shoulder${side}`],
      this.joints[`elbow${side}`],
      new THREE.Vector3(s * 0.22, -1, 0.1 + swing).normalize(),
      new THREE.Vector3(s * 0.08, -1, 0.55 + swing).normalize(),
    );
  }

  applyPose(p: FigurePose, time: number): void {
    const b = this.body;
    const j = this.joints;
    const live = 1 - p.fall;

    // Legs: combat stance (left foot forward), squat when crouching,
    // alternate swing when running, straight when dead.
    const gait = this.shamble ? 6 : 11;
    const stride = p.run * Math.sin(time * gait + p.phase);
    const stance = this.still ? 0 : (1 - p.run) * (1 - p.crouch);
    const drag = 1 - this.shamble * 0.6;
    const thighL = (-1.15 * p.crouch + stride * 0.7 - 0.16 * stance) * live;
    const thighR = (-1.15 * p.crouch - stride * 0.7 * drag + 0.1 * stance) * live;
    const kneeL = (1.9 * p.crouch + p.run * Math.max(0, -stride) * 1.1) * live;
    const kneeR = (1.9 * p.crouch + p.run * Math.max(0, stride) * 1.1) * live;
    const legs: [THREE.Group, THREE.Group, THREE.Group, number, number][] = [
      [j.hipL, j.kneeL, j.ankleL, thighL, kneeL],
      [j.hipR, j.kneeR, j.ankleR, thighR, kneeR],
    ];
    for (const [hip, knee, ankle, t, k] of legs) {
      const tt = b.digitigrade ? t + DIGI.thigh : t;
      const kk = b.digitigrade ? k + DIGI.knee : k;
      hip.rotation.z = Math.sign(hip.position.x) * 0.07 * live;
      hip.rotation.x = tt;
      knee.rotation.x = kk;
      ankle.rotation.x = -(tt + kk);
    }
    const height = Math.max(this.standingHeight(thighL, kneeL), this.standingHeight(thighR, kneeR));
    this.pivot.position.y = THREE.MathUtils.lerp(height, 0.16, p.fall) + p.hop;
    this.pivot.rotation.x = -p.fall * (Math.PI / 2) + p.run * 0.12 * live;
    this.pivot.rotation.z = this.shamble * Math.sin(time * (p.run ? 3 : 1.3) + p.phase) * (0.05 + p.run * 0.08) * live;
    this.yaw.rotation.y = p.yaw;
    for (const sp of this.spinners) sp.obj.rotation.y = time * sp.speed;

    // Spine: crouch lean, throw wind-up, hit and recoil kicks, breathing.
    const breathe = this.still ? 0 : Math.sin(time * 2.1 + p.phase) * 0.015;
    const lean = b.hunch + 0.28 * p.crouch + p.lean - p.hit * 0.38 - p.recoil * 0.05 + breathe;
    this.spine.rotation.x = lean * live;
    this.neck.rotation.x = (-lean * 0.55 + p.hit * 0.25) * live;
    this.neck.rotation.z = this.shamble * 0.32 * live;
    this.spine.rotation.y = this.shamble * Math.sin(time * 1.7 + p.phase) * 0.1 * live;

    // Weapon: low ready at rest, level when aiming, whatever the spine lean.
    this.gunPivot.rotation.x = (1 - p.aim) * 0.42 - p.recoil * 0.18 - lean * live;
    this.gunPivot.position.copy(this.gunRest);
    this.gunPivot.position.z -= p.recoil * 0.06;
    this.gunPivot.updateMatrix();

    const throwing = Math.abs(p.lean) > 0.04 && live > 0.5;
    if (throwing) {
      // Overhead throw: back and up during the wind-up, forward at release.
      const t = THREE.MathUtils.clamp((p.lean + 0.35) / 0.65, 0, 1);
      this.setArm(
        j.shoulderR,
        j.elbowR,
        new THREE.Vector3(-0.2, 0.75, -0.6).lerp(new THREE.Vector3(-0.15, 0.4, 0.9), t).normalize(),
        new THREE.Vector3(-0.1, 0.95, -0.25).lerp(new THREE.Vector3(-0.1, 0.25, 0.95), t).normalize(),
      );
    } else if (this.gripR && this.freeR) {
      this.reach('R', this.gripR.position.clone().applyMatrix4(this.gunPivot.matrix), new THREE.Vector3(-0.7, -1, -0.35));
    } else if (this.freeR) this.rest('R', this.swingArms ? -stride * 0.5 : 0);
    if (this.gripL) this.reach('L', this.gripL.position.clone().applyMatrix4(this.gunPivot.matrix), new THREE.Vector3(0.7, -1, -0.2));
    else if (this.leftAim && p.aim > 0.3 && live > 0.5) this.reach('L', this.leftAim, new THREE.Vector3(1, -0.4, -0.6));
    else this.rest('L', stride * (this.swingArms ? 0.5 : 0.4));

    this.flash.visible = p.flash > 0.01;
    (this.flash.material as THREE.MeshBasicMaterial).opacity = p.flash * 0.4;
  }

  rig(): FigureRig {
    const fig = this;
    return {
      root: this.root,
      yaw: this.yaw,
      pivot: this.pivot,
      gunPivot: this.gunPivot,
      muzzle: this.muzzle,
      flash: this.flash,
      drone: this.drone,
      hip: this.standingHeight(0, 0),
      chest: this.chest * this.body.scale,
      applyPose: (p, t) => fig.applyPose(p, t),
    };
  }
}

// --------------------------------------------------------------- weapons

type Gun = (f: Figure, gun: THREE.Group) => void;

const rifle: Gun = (f, gun) => {
  f.box(gun, 0.06, 0.1, 0.48, f.gunBody, 0, -0.04, 0.2);
  f.box(gun, 0.05, 0.09, 0.17, f.gunGrip, 0, -0.06, -0.07);
  f.box(gun, 0.04, 0.13, 0.06, f.gunGrip, 0, -0.13, 0.17);
  f.part(gun, new THREE.CylinderGeometry(0.016, 0.016, 0.14, 6).rotateX(Math.PI / 2), f.gunBody, 0, -0.03, 0.5);
  f.box(gun, 0.03, 0.04, 0.1, f.gunGrip, 0, 0.03, 0.12);
};

const shotgun: Gun = (f, gun) => {
  f.box(gun, 0.075, 0.11, 0.36, f.gunBody, 0, -0.04, 0.15);
  f.box(gun, 0.06, 0.1, 0.16, f.gunGrip, 0, -0.06, -0.07);
  f.part(gun, new THREE.CylinderGeometry(0.028, 0.028, 0.16, 8).rotateX(Math.PI / 2), f.gunBody, 0, -0.02, 0.38);
  f.box(gun, 0.08, 0.06, 0.12, f.gunGrip, 0, -0.08, 0.27);
};

const cannon: Gun = (f, gun) => {
  f.box(gun, 0.12, 0.15, 0.5, f.gunBody, 0, -0.05, 0.2);
  f.part(gun, new THREE.CylinderGeometry(0.09, 0.09, 0.11, 12).rotateZ(Math.PI / 2), f.pal.armour, 0, -0.15, 0.12);
  f.part(gun, new THREE.CylinderGeometry(0.05, 0.05, 0.2, 10).rotateX(Math.PI / 2), f.gunBody, 0, -0.03, 0.5);
  f.box(gun, 0.08, 0.1, 0.16, f.gunGrip, 0, -0.07, -0.08);
};

const sniper: Gun = (f, gun) => {
  f.box(gun, 0.05, 0.09, 0.7, f.gunBody, 0, -0.04, 0.3);
  f.box(gun, 0.05, 0.1, 0.2, f.gunGrip, 0, -0.06, -0.09);
  f.part(gun, new THREE.CylinderGeometry(0.014, 0.014, 0.26, 6).rotateX(Math.PI / 2), f.gunBody, 0, -0.02, 0.76);
  f.part(gun, new THREE.CylinderGeometry(0.028, 0.028, 0.2, 8).rotateX(Math.PI / 2), f.gunGrip, 0, 0.04, 0.2);
};

const alienRifle: Gun = (f, gun) => {
  f.box(gun, 0.07, 0.11, 0.5, f.pal.metal, 0, -0.04, 0.2);
  f.box(gun, 0.075, 0.025, 0.3, f.pal.visor, 0, 0.025, 0.2);
  f.box(gun, 0.06, 0.09, 0.15, f.pal.dark, 0, -0.07, -0.06);
  f.box(gun, 0.05, 0.05, 0.14, f.pal.metal, 0, -0.03, 0.5, [0.15, 0, 0]);
};

// --------------------------------------------------------------- figures

/** Soldier builds: limb thickness, torso width and shoulder pads. */
const BUILD_SHAPES: Record<BuildId, { thick: number; width: number; pads: 'none' | 'small' | 'big' }> = {
  // Light armour: thinner limbs and torso, no shoulder pads (at 1/16 voxels less would not show).
  slim: { thick: 0.88, width: 0.86, pads: 'none' },
  normal: { thick: 1.15, width: 1.08, pads: 'small' },
  heavy: { thick: 1.3, width: 1.2, pads: 'big' },
};

type SoldierClass = 'assault' | 'grenadier' | 'sharpshooter' | 'specialist';

/** Equipment tiers a soldier carries (1 when absent): conventional / magnetic / plasma, kevlar / plates / predator. */
export interface Gear {
  weapon?: Tier;
  armor?: Tier;
}

/**
 * An XCOM soldier: class weapon and gear, plus a look. Without `look` it
 * wears the squad colour with the class's head and build (grenadiers heavy,
 * sharpshooters hooded), which is also what the engine's defaultAppearance gives.
 */
function soldier(accent: string, cls: SoldierClass, look?: Appearance, gear?: Gear): Figure {
  const pal = soldierPalette(accent, look);
  const shape = BUILD_SHAPES[look?.build ?? (cls === 'grenadier' ? 'heavy' : 'normal')];
  const headKind: HeadId = look?.head ?? (cls === 'sharpshooter' ? 'hood' : 'helmet');
  const cloth = mat(new THREE.Color(look?.secondary ?? accent).multiplyScalar(0.45), { rough: 0.9 });
  const f = new Figure({ ...HUMAN, thick: shape.thick }, pal);
  if (headKind === 'hood') f.hoodCloth = cloth;
  const weaponTier = gear?.weapon ?? 1;
  f.torso({ width: shape.width, pads: shape.pads })
    .limbs()
    .armourTier(gear?.armor ?? 1)
    .helmet(headKind)
    .gunMaterials(weaponTier);
  const spine = f.spine;
  switch (cls) {
    case 'assault':
      f.weapon([-0.08, 0.4, 0.13], (g) => shotgun(f, g), 0.46, [0, -0.08, 0.04], [0, -0.08, 0.27]);
      f.box(spine, 0.035, 0.66, 0.075, mat(0xcfd8dc, { metal: 0.9, rough: 0.2 }), 0.06, 0.34, -0.16, [0, 0, 0.6]);
      f.box(spine, 0.12, 0.03, 0.05, pal.dark, -0.13, 0.62, -0.16, [0, 0, 0.6]);
      break;
    case 'grenadier':
      f.weapon([-0.09, 0.38, 0.14], (g) => cannon(f, g), 0.62, [0, -0.11, 0.04], [0, -0.11, 0.3]);
      f.box(spine, 0.3, 0.27, 0.14, pal.armour, 0, 0.3, -0.2);
      for (let i = 0; i < 4; i++) f.ball(f.pivot, 0.035, mat(0x3b4a3a, { rough: 0.6 }), -0.12 + i * 0.08, 0.04, 0.12);
      break;
    case 'sharpshooter':
      f.weapon([-0.07, 0.41, 0.12], (g) => sniper(f, g), 0.9, [0, -0.08, 0.06], [0, -0.07, 0.34]);
      f.box(spine, 0.4, 0.62, 0.025, cloth, 0, 0.16, -0.15, [-0.08, 0, 0]);
      f.box(f.joints.hipR, 0.06, 0.13, 0.1, pal.dark, -0.07, -0.12, 0.02);
      break;
    case 'specialist':
      f.weapon([-0.075, 0.4, 0.13], (g) => rifle(f, g), 0.58, [0, -0.08, 0.06], [0, -0.07, 0.31]);
      f.box(spine, 0.26, 0.3, 0.12, pal.dark, 0, 0.3, -0.18);
      f.part(spine, new THREE.CylinderGeometry(0.008, 0.008, 0.36, 4), pal.metal, 0.08, 0.6, -0.2);
      f.withDrone();
      break;
  }
  f.gunGlow(weaponTier, f.muzzle.position.z);
  return f;
}

function trooper(kind: 'trooper' | 'officer' | 'lancer'): Figure {
  const officer = kind === 'officer';
  const lancer = kind === 'lancer';
  const pal = advent(lancer ? 0x5a1622 : 0x7a1f2b, officer ? 0xff9a2a : lancer ? 0x2ab8ff : 0xff2a2a);
  const body: Body = lancer
    ? {
        ...HUMAN,
        thigh: 0.39,
        shin: 0.38,
        upper: 0.31,
        fore: 0.29,
        thick: 0.85,
        scale: 1.04,
      }
    : { ...HUMAN, scale: officer ? 1.08 : 1.04 };
  const f = new Figure(body, pal);
  f.torso({
    width: lancer ? 0.9 : 1,
    pads: officer ? 'gold' : lancer ? 'none' : 'small',
  })
    .limbs({ kneePads: !lancer })
    .helmet('advent');
  if (officer) {
    f.box(f.head, 0.03, 0.09, 0.24, mat(0xc9a13a, { metal: 0.7, rough: 0.3 }), 0, 0.19, -0.02);
    f.box(f.spine, 0.38, 0.62, 0.025, mat(0x5a1622, { rough: 0.85 }), 0, 0.15, -0.15, [-0.08, 0, 0]);
  }
  if (lancer) {
    f.weapon(
      [-0.12, 0.3, 0.2],
      (g) => {
        f.part(g, new THREE.CylinderGeometry(0.03, 0.03, 0.8, 8).rotateX(Math.PI / 2), pal.metal, 0, 0, 0.32);
        f.ball(g, 0.06, pal.visor, 0, 0, 0.73);
      },
      0.76,
      [0, -0.02, 0.0],
      null,
    );
  } else {
    f.weapon([-0.075, 0.4, 0.13], (g) => alienRifle(f, g), 0.58, [0, -0.08, 0.06], [0, -0.07, 0.31]);
  }
  return f;
}

function xenoid(): Figure {
  const skin = mat(0x8a9099, { rough: 0.45 });
  const pal: Palette = {
    ...advent(0x3a3f45, 0x3dff6a),
    suit: skin,
    armour: skin,
    plate: mat(0x2a2e33, { rough: 0.7 }),
    skin,
  };
  const body: Body = {
    ...HUMAN,
    thigh: 0.31,
    shin: 0.3,
    upper: 0.31,
    fore: 0.31,
    shoulderX: 0.17,
    shoulderY: 0.33,
    neckY: 0.37,
    thick: 0.68,
    hunch: 0.18,
  };
  const f = new Figure(body, pal);
  const { spine, pivot, neck, head } = f;
  f.box(pivot, 0.2, 0.12, 0.15, skin, 0, 0, 0);
  f.box(spine, 0.18, 0.16, 0.13, skin, 0, 0.08, 0);
  f.box(spine, 0.27, 0.22, 0.17, skin, 0, 0.25, 0);
  f.box(spine, 0.29, 0.04, 0.18, pal.plate, 0, 0.2, 0);
  f.box(spine, 0.04, 0.24, 0.18, pal.plate, 0.06, 0.24, 0, [0, 0, 0.5]);
  f.flash = f.part(
    spine,
    new THREE.CapsuleGeometry(0.2, 0.5, 4, 12),
    new THREE.MeshBasicMaterial({
      color: 0xff3a2a,
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    }),
    0,
    0.25,
    0,
  );
  f.flash.castShadow = false;
  f.flash.visible = false;
  f.chest = f.standingHeight(0, 0) + body.spineY + 0.25;
  f.part(neck, new THREE.CylinderGeometry(0.035, 0.04, 0.12, 8), skin, 0, 0.05, 0);
  head.position.y = 0.17;
  f.ball(head, 0.19, skin, 0, 0.02, -0.03, [1, 1.08, 1.22]);
  for (const s of [1, -1]) f.ball(head, 0.05, mat(0x9dffb0, { glow: 0x3dff6a }), s * 0.075, 0.0, 0.16, [1.5, 0.8, 0.6]);
  for (const side of ['L', 'R'] as const) {
    const j = f.joints;
    f.bone(j[`hip${side}`], 0.07, 0.055, body.thigh, skin);
    f.bone(j[`knee${side}`], 0.05, 0.04, body.shin, skin);
    f.box(j[`ankle${side}`], 0.06, body.foot, 0.16, skin, 0, -body.foot / 2, 0.04);
    f.bone(j[`shoulder${side}`], 0.05, 0.04, body.upper, skin);
    f.bone(j[`elbow${side}`], 0.04, 0.035, body.fore, skin);
    for (let k = -1; k <= 1; k++) f.box(j[`elbow${side}`], 0.012, 0.08, 0.012, skin, k * 0.015, -body.fore - 0.04, 0.01);
  }
  f.weapon(
    [-0.11, 0.28, 0.2],
    (g) => {
      f.box(g, 0.05, 0.08, 0.2, pal.plate, 0, -0.03, 0.08);
      f.box(g, 0.055, 0.02, 0.12, pal.visor, 0, 0.015, 0.1);
    },
    0.2,
    [0, -0.07, 0.0],
    null,
  );
  return f;
}

function mec(): Figure {
  const hull = mat(0x5a2a30, { metal: 0.4, rough: 0.45 });
  const frame = mat(0x3a3d42, { metal: 0.5, rough: 0.5 });
  const pal: Palette = {
    ...advent(0x5a2a30, 0xff2a2a),
    suit: frame,
    armour: hull,
    plate: hull,
    metal: mat(0x2a2e33, { metal: 0.7, rough: 0.35 }),
  };
  const body: Body = {
    ...HUMAN,
    hipDrop: 0.06,
    hipX: 0.2,
    thigh: 0.42,
    shin: 0.42,
    foot: 0.09,
    spineY: 0.1,
    shoulderX: 0.42,
    shoulderY: 0.52,
    neckY: 0.66,
    upper: 0.32,
    fore: 0.3,
    thick: 1.2,
    digitigrade: true,
    boxy: true,
    scale: 1.12,
  };
  const f = new Figure(body, pal);
  const { spine, pivot, neck, head, joints: j } = f;
  f.box(pivot, 0.46, 0.2, 0.32, frame, 0, 0, 0);
  f.box(spine, 0.5, 0.2, 0.36, frame, 0, 0.1, 0);
  f.box(spine, 0.76, 0.5, 0.52, hull, 0, 0.4, 0);
  f.box(spine, 0.5, 0.3, 0.06, frame, 0, 0.4, 0.28);
  f.box(neck, 0.28, 0.16, 0.28, frame, 0, 0.02, 0.06);
  head.position.y = 0.06;
  f.box(head, 0.22, 0.06, 0.05, pal.visor, 0, 0.0, 0.21);
  // Missile pod on the left shoulder.
  f.box(spine, 0.26, 0.24, 0.36, frame, 0.44, 0.72, -0.02);
  for (let k = 0; k < 4; k++)
    f.part(spine, new THREE.CylinderGeometry(0.03, 0.03, 0.04, 8).rotateX(Math.PI / 2), pal.visor, 0.39 + (k % 2) * 0.1, 0.66 + Math.floor(k / 2) * 0.1, 0.17);
  f.flash = f.part(
    spine,
    new THREE.CapsuleGeometry(0.42, 0.5, 4, 12),
    new THREE.MeshBasicMaterial({
      color: 0xff3a2a,
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    }),
    0,
    0.4,
    0,
  );
  f.flash.castShadow = false;
  f.flash.visible = false;
  f.chest = f.standingHeight(0, 0) + body.spineY + 0.4;
  for (const side of ['L', 'R'] as const) {
    f.bone(j[`hip${side}`], 0.09, 0.09, body.thigh, frame);
    f.box(j[`knee${side}`], 0.2, 0.14, 0.2, hull, 0, 0, 0);
    f.bone(j[`knee${side}`], 0.07, 0.07, body.shin, frame);
    f.box(j[`ankle${side}`], 0.22, body.foot, 0.32, pal.metal, 0, -body.foot / 2, 0.06);
  }
  f.box(spine, 0.2, 0.2, 0.22, hull, 0.42, 0.52, 0);
  f.bone(j.shoulderL, 0.07, 0.07, body.upper, frame);
  f.bone(j.elbowL, 0.065, 0.065, body.fore, frame);
  f.box(j.elbowL, 0.14, 0.12, 0.14, pal.metal, 0, -body.fore - 0.05, 0.02);
  // The right arm is the gun.
  f.freeR = false;
  j.shoulderR.visible = false;
  f.weapon(
    [-0.44, 0.5, 0.0],
    (g) => {
      f.box(g, 0.24, 0.24, 0.26, hull, 0, 0, 0);
      f.box(g, 0.17, 0.19, 0.72, pal.metal, 0, -0.06, 0.4);
      f.part(g, new THREE.CylinderGeometry(0.05, 0.05, 0.16, 10).rotateX(Math.PI / 2), frame, 0, -0.06, 0.8);
    },
    0.88,
    null,
    null,
  );
  return f;
}

/** Hit flash on the torso, sized for each body. */
function flashOn(f: Figure, radius: number, length: number, y: number): void {
  f.flash = f.part(
    f.spine,
    new THREE.CapsuleGeometry(radius, length, 4, 12),
    new THREE.MeshBasicMaterial({
      color: 0xff3a2a,
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    }),
    0,
    y,
    0,
  );
  f.flash.castShadow = false;
  f.flash.visible = false;
}

/** Small grey psionic: oversized skull, violet eyes, a sidearm, the other hand to the temple when it casts. */
function sectoid(): Figure {
  const skin = mat(0xa0a5ae, { rough: 0.4 });
  const harness = mat(0x3a2a4a, { rough: 0.7 });
  const pal: Palette = {
    ...advent(0x4a3f5a, 0x9a3aff),
    suit: skin,
    armour: skin,
    plate: harness,
    skin,
  };
  const body: Body = {
    ...HUMAN,
    thigh: 0.27,
    shin: 0.26,
    foot: 0.06,
    upper: 0.27,
    fore: 0.27,
    shoulderX: 0.15,
    shoulderY: 0.29,
    neckY: 0.33,
    thick: 0.62,
    hunch: 0.1,
    scale: 0.92,
  };
  const f = new Figure(body, pal);
  const { spine, pivot, neck, head, joints: j } = f;
  f.box(pivot, 0.18, 0.11, 0.13, skin, 0, 0, 0);
  f.box(spine, 0.16, 0.14, 0.12, skin, 0, 0.07, 0);
  f.box(spine, 0.24, 0.2, 0.15, skin, 0, 0.22, 0);
  f.box(spine, 0.26, 0.035, 0.16, harness, 0, 0.17, 0);
  f.box(spine, 0.035, 0.22, 0.16, harness, -0.05, 0.22, 0, [0, 0, -0.5]);
  f.ball(spine, 0.03, mat(0xd29aff, { glow: 0x9a3aff }), 0, 0.27, 0.08);
  flashOn(f, 0.17, 0.4, 0.22);
  f.chest = f.standingHeight(0, 0) + body.spineY + 0.22;
  f.part(neck, new THREE.CylinderGeometry(0.03, 0.035, 0.12, 8), skin, 0, 0.05, 0);
  head.position.y = 0.16;
  f.ball(head, 0.24, mat(0xa7acb6, { rough: 0.35 }), 0, 0.04, -0.04, [1, 1.12, 1.2]);
  for (const s of [1, -1]) f.ball(head, 0.06, mat(0xd29aff, { glow: 0x9a3aff }), s * 0.085, -0.01, 0.2, [1.5, 0.85, 0.6]);
  for (const side of ['L', 'R'] as const) {
    f.bone(j[`hip${side}`], 0.07, 0.055, body.thigh, skin);
    f.bone(j[`knee${side}`], 0.05, 0.04, body.shin, skin);
    f.box(j[`ankle${side}`], 0.06, body.foot, 0.14, skin, 0, -body.foot / 2, 0.035);
    f.bone(j[`shoulder${side}`], 0.05, 0.04, body.upper, skin);
    f.bone(j[`elbow${side}`], 0.04, 0.035, body.fore, skin);
    for (let k = -1; k <= 1; k++) f.box(j[`elbow${side}`], 0.012, 0.07, 0.012, skin, k * 0.014, -body.fore - 0.035, 0.01);
  }
  f.weapon(
    [-0.1, 0.24, 0.18],
    (g) => {
      f.box(g, 0.05, 0.08, 0.22, mat(0x4a3f5a, { metal: 0.4, rough: 0.4 }), 0, -0.03, 0.08);
      f.box(g, 0.055, 0.02, 0.14, mat(0xd29aff, { glow: 0x9a3aff }), 0, 0.015, 0.1);
    },
    0.22,
    [0, -0.07, 0.0],
    null,
  );
  f.leftAim = new THREE.Vector3(0.13, body.neckY + 0.17, 0.08);
  return f;
}

/** Raised corpse: hunched, head lolling, arms reaching forward with claws, a dragging gait. */
function zombie(): Figure {
  const skin = mat(0x7c8a72, { rough: 0.9 });
  const rags = mat(0x3a3f36, { rough: 0.95 });
  const remnant = mat(0x56605a, { rough: 0.7 });
  const pal: Palette = {
    ...soldierPalette('#56605a'),
    suit: rags,
    armour: remnant,
    plate: remnant,
    dark: mat(0x23261f, { rough: 0.9 }),
    skin,
  };
  const body: Body = { ...HUMAN, thick: 1.0, hunch: 0.38 };
  const f = new Figure(body, pal);
  f.shamble = 1;
  const { spine, pivot, head, joints: j } = f;
  f.box(pivot, 0.29, 0.14, 0.2, rags, 0, 0, 0);
  f.box(spine, 0.26, 0.17, 0.18, skin, 0, 0.09, 0);
  f.box(spine, 0.37, 0.28, 0.22, rags, 0, 0.31, 0);
  f.box(spine, 0.22, 0.12, 0.04, skin, 0.05, 0.26, 0.11, [0, 0, 0.3]);
  f.box(spine, 0.15, 0.07, 0.17, remnant, -0.2, 0.47, 0, [0, 0, 0.45]);
  f.part(spine, new THREE.CylinderGeometry(0.075, 0.09, 0.06, 10), rags, 0, 0.46, 0);
  flashOn(f, 0.22, 0.5, 0.28);
  f.chest = f.standingHeight(0, 0) + body.spineY + 0.28;
  f.part(f.neck, new THREE.CylinderGeometry(0.045, 0.05, 0.09, 8), skin, 0, 0.04, 0);
  f.ball(head, 0.105, skin, 0, 0, 0.01, [1, 1.05, 1.05]);
  f.box(head, 0.1, 0.04, 0.06, skin, 0, -0.08, 0.05, [0.3, 0, 0]);
  for (const s of [1, -1]) f.ball(head, 0.022, mat(0xd29aff, { glow: 0x9a3aff }), s * 0.04, 0.02, 0.09);
  for (const side of ['L', 'R'] as const) {
    f.bone(j[`hip${side}`], 0.072, 0.06, body.thigh, rags);
    f.bone(j[`knee${side}`], 0.056, 0.046, body.shin, side === 'L' ? rags : skin);
    if (side === 'L') f.box(j.kneeL, 0.09, 0.08, 0.05, remnant, 0, -0.01, 0.06);
    f.box(j[`ankle${side}`], 0.1, body.foot, 0.21, pal.dark, 0, -body.foot / 2, 0.05);
    f.bone(j[`shoulder${side}`], 0.05, 0.044, body.upper, side === 'L' ? rags : skin);
    f.bone(j[`elbow${side}`], 0.045, 0.04, body.fore, skin);
    for (let k = -1; k <= 1; k++) f.box(j[`elbow${side}`], 0.014, 0.1, 0.014, mat(0xcfd2c4, { rough: 0.5 }), k * 0.018, -body.fore - 0.05, 0.02, [0.3, 0, 0]);
  }
  // No weapon: the "gun" pivot only carries where the hands reach (and claw out on a lunge).
  f.weapon([0, 0.34, 0.2], () => undefined, 0.5, [-0.13, -0.03, 0.38], [0.13, -0.03, 0.38]);
  return f;
}

/** Objective pylon: plinth, mast, dish, glowing core and a spinning ring. No legs, no weapon. */
function relay(): Figure {
  const dark = mat(0x2a1e26, { metal: 0.6, rough: 0.4 });
  const steel = mat(0x3a2a34, { metal: 0.5, rough: 0.4 });
  const glow = mat(0xff8aa0, { glow: 0xff2a5a });
  const pal: Palette = {
    ...advent(0x3a2a34, 0xff2a5a),
    suit: steel,
    armour: dark,
    plate: steel,
  };
  const body: Body = {
    ...HUMAN,
    hipDrop: 0,
    thigh: 0,
    shin: 0,
    foot: 0,
    spineY: 0.28,
    neckY: 1.9,
  };
  const f = new Figure(body, pal);
  f.still = true;
  f.freeR = false;
  const { spine, pivot } = f;
  f.part(pivot, new THREE.CylinderGeometry(0.4, 0.46, 0.28, 6), dark, 0, 0.14, 0);
  for (let k = 0; k < 3; k++) {
    const a = (k / 3) * Math.PI * 2 + 0.4;
    f.box(pivot, 0.08, 0.06, 0.5, steel, Math.cos(a) * 0.45, 0.03, Math.sin(a) * 0.45, [0, -a + Math.PI / 2, 0]);
  }
  f.part(spine, new THREE.CylinderGeometry(0.09, 0.16, 1.5, 6), steel, 0, 0.75, 0);
  for (const y of [0.35, 0.8, 1.2]) f.part(spine, new THREE.CylinderGeometry(0.2 - y * 0.06, 0.2 - y * 0.06, 0.05, 6), dark, 0, y, 0);
  f.part(spine, new THREE.CylinderGeometry(0.34, 0.06, 0.12, 12), dark, 0.12, 1.42, 0.08, [0.5, 0, -0.4]);
  f.part(spine, new THREE.OctahedronGeometry(0.17, 0), glow, 0, 1.72, 0).scale.set(1, 1.6, 1);
  f.ball(spine, 0.035, glow, 0, 1.98, 0);
  const ring = new THREE.Group();
  ring.position.y = 1.28;
  spine.add(ring);
  f.part(ring, new THREE.TorusGeometry(0.34, 0.025, 6, 32).rotateX(Math.PI / 2), mat(0xff6a8a, { glow: 0xff2a5a }), 0, 0, 0);
  f.spinners.push({ obj: ring, speed: 1.4 });
  flashOn(f, 0.24, 1.0, 0.85);
  f.chest = 1.3;
  // No weapon: a dummy pivot at the top for effects that expect a muzzle.
  f.weapon([0, 1.72, 0], () => undefined, 0, null, null);
  return f;
}

/** Unarmed civilian (resistance contact): long coat, scarf in the squad colour, briefcase. */
function vip(accent: string): Figure {
  const coat = mat(0x2e3440, { rough: 0.85 });
  const trousers = mat(0x2a2e36, { rough: 0.9 });
  const skin = mat(0xc89f84, { rough: 0.8 });
  const pal: Palette = {
    ...soldierPalette(accent),
    suit: coat,
    armour: coat,
    plate: coat,
    dark: mat(0x3a2a20, { rough: 0.6 }),
    skin,
  };
  const body: Body = { ...HUMAN, thick: 0.95 };
  const f = new Figure(body, pal);
  f.swingArms = true;
  const { spine, pivot, neck, head, joints: j } = f;
  f.box(pivot, 0.29, 0.14, 0.2, coat, 0, 0, 0);
  f.box(spine, 0.27, 0.17, 0.18, coat, 0, 0.09, 0);
  f.box(spine, 0.36, 0.29, 0.21, coat, 0, 0.31, 0);
  f.box(spine, 0.06, 0.26, 0.012, mat(0xd8d8d0, { rough: 0.8 }), 0, 0.33, 0.106);
  f.part(spine, new THREE.CylinderGeometry(0.085, 0.1, 0.07, 10), mat(accent, { rough: 0.8 }), 0, 0.46, 0);
  f.box(spine, 0.07, 0.2, 0.02, mat(accent, { rough: 0.8 }), 0.06, 0.34, 0.115, [0, 0, 0.1]);
  flashOn(f, 0.21, 0.5, 0.28);
  f.chest = f.standingHeight(0, 0) + body.spineY + 0.3;
  f.part(neck, new THREE.CylinderGeometry(0.045, 0.05, 0.09, 8), skin, 0, 0.04, 0);
  f.ball(head, 0.1, skin, 0, 0, 0.01);
  f.ball(head, 0.105, mat(0x3a2a20, { rough: 0.9 }), 0, 0.045, -0.015, [1, 0.62, 1.05]);
  f.box(head, 0.16, 0.025, 0.02, mat(0x1d2124, { metal: 0.4 }), 0, 0.01, 0.1);
  for (const side of ['L', 'R'] as const) {
    f.bone(j[`hip${side}`], 0.07, 0.06, body.thigh, trousers);
    // Coat tails move with the legs.
    f.box(j[`hip${side}`], 0.15, 0.36, 0.2, coat, 0, -0.17, -0.01);
    f.bone(j[`knee${side}`], 0.055, 0.047, body.shin, trousers);
    f.box(j[`ankle${side}`], 0.09, body.foot, 0.2, pal.dark, 0, -body.foot / 2, 0.05);
    f.bone(j[`shoulder${side}`], 0.055, 0.048, body.upper, coat);
    f.bone(j[`elbow${side}`], 0.05, 0.043, body.fore, coat);
    f.box(j[`elbow${side}`], 0.055, 0.075, 0.055, skin, 0, -body.fore - 0.035, 0.005);
  }
  f.box(j.elbowR, 0.07, 0.22, 0.3, mat(0x5a4030, { rough: 0.6 }), 0, -body.fore - 0.17, 0.02);
  f.box(j.elbowR, 0.02, 0.04, 0.1, mat(0x2a1e16), 0, -body.fore - 0.05, 0.02);
  // Unarmed: aiming raises nothing.
  f.weapon([0, 0.3, 0.1], () => undefined, 0.1, null, null);
  return f;
}

/**
 * Builds a figure in its rest pose. `look` customizes the four XCOM classes
 * (colours, head, build) and `gear` gives them their weapon and armour tiers;
 * every other template ignores both.
 */
export function buildFigure(template: string, accent: string, look?: Appearance, gear?: Gear): FigureRig {
  let f: Figure;
  switch (template as FigureTemplate) {
    case 'assault':
    case 'grenadier':
    case 'sharpshooter':
    case 'specialist':
      f = soldier(accent, template as SoldierClass, look, gear);
      break;
    case 'trooper':
    case 'officer':
    case 'lancer':
      f = trooper(template as 'trooper');
      break;
    case 'xenoid':
      f = xenoid();
      break;
    case 'mec':
      f = mec();
      break;
    case 'sectoid':
      f = sectoid();
      break;
    case 'zombie':
      f = zombie();
      break;
    case 'relay':
      f = relay();
      break;
    case 'vip':
      f = vip(accent);
      break;
    default:
      f = soldier(accent, 'specialist');
  }
  const rig = f.rig();
  rig.applyPose(
    {
      crouch: 0,
      aim: 0,
      run: 0,
      recoil: 0,
      hit: 0,
      flash: 0,
      fall: 0,
      lean: 0,
      hop: 0,
      yaw: 0,
      phase: 0,
    },
    0,
  );
  return rig;
}
