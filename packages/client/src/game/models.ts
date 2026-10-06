import * as THREE from 'three';
import type { Appearance, TemplateId, Tier } from '@bastion/engine';
import { buildFigure, FIGURE_TEMPLATES, type FigurePose, type FigureRig, type Gear } from './figures';
import { voxelizeUnit } from './voxel';

// Placeholder models built from primitives, with a minimal "skeleton" so the
// animation code can pose them:
//
//   root    position on the ground (moved by the unit view)
//   └ yaw   facing; +Z is forward
//     ├ pivot   hips: lean, crouch, fall and hit reactions rotate/translate this
//     │ ├ body parts
//     │ ├ flash      red overlay shown when hit
//     │ └ gunPivot   shoulder: aim (rotation.x) and recoil (position.z)
//     │   ├ weapon
//     │   └ muzzle   where tracers and flashes start
//     └ drone   specialist's support drone, hovering over the shoulder

export interface UnitRig {
  root: THREE.Group;
  yaw: THREE.Group;
  pivot: THREE.Group;
  gunPivot: THREE.Group;
  muzzle: THREE.Object3D;
  flash: THREE.Mesh;
  drone: THREE.Object3D | null;
  /** Hip height; the pivot rests here when standing. */
  hip: number;
  /** Height of the chest, for aiming and damage numbers. */
  chest: number;
}

const geo = {
  body: new THREE.CapsuleGeometry(0.26, 0.72, 4, 12),
  slimBody: new THREE.CapsuleGeometry(0.2, 0.8, 4, 12),
  flash: new THREE.CapsuleGeometry(0.31, 0.8, 4, 12),
  head: new THREE.SphereGeometry(0.19, 16, 12),
  bigHead: new THREE.SphereGeometry(0.3, 18, 14),
  plate: new THREE.BoxGeometry(0.42, 0.34, 0.2),
  visor: new THREE.BoxGeometry(0.26, 0.07, 0.08),
  pad: new THREE.BoxGeometry(0.18, 0.08, 0.22),
  bigPad: new THREE.BoxGeometry(0.24, 0.12, 0.28),
  eye: new THREE.SphereGeometry(0.05, 8, 6),
  rifle: new THREE.BoxGeometry(0.08, 0.1, 0.62),
  shotgun: new THREE.BoxGeometry(0.11, 0.13, 0.5),
  cannon: new THREE.BoxGeometry(0.15, 0.17, 0.72),
  drum: new THREE.CylinderGeometry(0.1, 0.1, 0.12, 12).rotateZ(Math.PI / 2),
  sniper: new THREE.BoxGeometry(0.06, 0.08, 0.98),
  scope: new THREE.CylinderGeometry(0.035, 0.035, 0.22, 8).rotateX(Math.PI / 2),
  blade: new THREE.BoxGeometry(0.04, 0.75, 0.08),
  baton: new THREE.CylinderGeometry(0.03, 0.03, 0.8, 8).rotateX(Math.PI / 2),
  batonTip: new THREE.SphereGeometry(0.06, 10, 8),
  droneBody: new THREE.OctahedronGeometry(0.13, 0),
  droneRing: new THREE.TorusGeometry(0.16, 0.02, 6, 20).rotateX(Math.PI / 2),
  mecBody: new THREE.BoxGeometry(0.86, 0.8, 0.62),
  mecHead: new THREE.BoxGeometry(0.34, 0.22, 0.34),
  mecLeg: new THREE.BoxGeometry(0.22, 0.75, 0.3),
  mecPod: new THREE.BoxGeometry(0.3, 0.26, 0.4),
  mecGun: new THREE.BoxGeometry(0.16, 0.18, 0.8),
  pylonBase: new THREE.CylinderGeometry(0.42, 0.55, 0.55, 8),
  pylonShaft: new THREE.CylinderGeometry(0.16, 0.28, 1.5, 8),
  pylonCore: new THREE.OctahedronGeometry(0.3, 0),
  pylonRing: new THREE.TorusGeometry(0.45, 0.03, 6, 28).rotateX(Math.PI / 2),
  coat: new THREE.CapsuleGeometry(0.24, 0.7, 4, 12),
  sectoidBody: new THREE.CapsuleGeometry(0.17, 0.55, 4, 10),
  sectoidHead: new THREE.SphereGeometry(0.27, 18, 14).scale(1, 1.15, 1.05),
  sectoidEye: new THREE.SphereGeometry(0.07, 10, 8).scale(1.3, 0.8, 0.6),
  zombieBody: new THREE.CapsuleGeometry(0.25, 0.62, 4, 10),
  claw: new THREE.ConeGeometry(0.05, 0.28, 6).rotateX(Math.PI / 2),
  briefcase: new THREE.BoxGeometry(0.08, 0.26, 0.36),
};

const matCache = new Map<string, THREE.Material>();
function mat(color: number | string, opts: { emissive?: number; roughness?: number; metalness?: number } = {}): THREE.Material {
  const key = `${color}|${opts.emissive ?? ''}|${opts.roughness ?? ''}|${opts.metalness ?? ''}`;
  let m = matCache.get(key);
  if (!m) {
    m = new THREE.MeshStandardMaterial({
      color,
      emissive: opts.emissive ?? 0x000000,
      emissiveIntensity: opts.emissive ? 1.6 : 0,
      roughness: opts.roughness ?? 0.6,
      metalness: opts.metalness ?? 0.1,
    });
    matCache.set(key, m);
  }
  return m;
}

const GUNMETAL = () => mat(0x1d2124, { metalness: 0.5 });

interface RigOptions {
  hip?: number;
  shoulderY: number;
  chest?: number;
  scale?: number;
}

class Builder {
  readonly rig: UnitRig;

  constructor(opts: RigOptions) {
    const hip = opts.hip ?? 0.55;
    const root = new THREE.Group();
    const yaw = new THREE.Group();
    const pivot = new THREE.Group();
    pivot.position.y = hip;
    const gunPivot = new THREE.Group();
    gunPivot.position.set(0.24, opts.shoulderY - hip, 0.06);
    const muzzle = new THREE.Object3D();
    gunPivot.add(muzzle);
    const flash = new THREE.Mesh(
      geo.flash,
      new THREE.MeshBasicMaterial({ color: 0xff3a2a, transparent: true, opacity: 0, depthWrite: false }),
    );
    flash.position.y = (opts.chest ?? 1.0) - 0.34 - hip;
    if (opts.chest && opts.chest > 1.1) flash.scale.setScalar(1.5);
    flash.visible = false;
    pivot.add(gunPivot, flash);
    yaw.add(pivot);
    root.add(yaw);
    yaw.scale.setScalar(opts.scale ?? 1);
    this.rig = { root, yaw, pivot, gunPivot, muzzle, flash, drone: null, hip, chest: opts.chest ?? 1.0 };
  }

  /** Body part; `y` is measured from the ground and converted to hip space. */
  part(geometry: THREE.BufferGeometry, material: THREE.Material, x: number, y: number, z: number, rotation?: THREE.Euler): this {
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.set(x, y - this.rig.hip, z);
    if (rotation) mesh.rotation.copy(rotation);
    mesh.castShadow = true;
    this.rig.pivot.add(mesh);
    return this;
  }

  /** Weapon part in shoulder space; the muzzle goes at `length`. */
  weapon(parts: [THREE.BufferGeometry, THREE.Material, number, number, number][], length: number): this {
    for (const [g, m, x, y, z] of parts) {
      const mesh = new THREE.Mesh(g, m);
      mesh.position.set(x, y, z);
      mesh.castShadow = true;
      this.rig.gunPivot.add(mesh);
    }
    this.rig.muzzle.position.set(0, -0.05, length);
    return this;
  }

  drone(): this {
    const drone = new THREE.Group();
    const body = new THREE.Mesh(geo.droneBody, mat(0xb8c4cc, { metalness: 0.6, roughness: 0.3 }));
    const ring = new THREE.Mesh(geo.droneRing, mat(0x7dffb0, { emissive: 0x2fd97a }));
    body.castShadow = true;
    drone.add(body, ring);
    drone.position.set(-0.45, 1.75, -0.1);
    this.rig.yaw.add(drone);
    this.rig.drone = drone;
    return this;
  }
}

function soldierBody(b: Builder, accent: string, bulky = false): Builder {
  const armour = '#' + new THREE.Color(accent).lerp(new THREE.Color(0x56605a), 0.55).getHexString();
  b.part(geo.body, mat(armour), 0, 0.62, 0)
    .part(geo.plate, mat(accent, { roughness: 0.4 }), 0, 0.85, 0.12)
    .part(geo.plate, mat(accent, { roughness: 0.4 }), 0, 0.85, -0.12)
    .part(geo.head, mat(0x48524c), 0, 1.38, 0)
    .part(geo.visor, mat(0x9fe3ff, { emissive: 0x2a8fbf }), 0, 1.4, 0.16);
  if (bulky) {
    b.part(geo.bigPad, mat(accent, { roughness: 0.4 }), -0.3, 1.1, 0).part(geo.bigPad, mat(accent, { roughness: 0.4 }), 0.3, 1.1, 0);
  }
  return b;
}

export function buildModel(template: TemplateId, accent: string): UnitRig {
  switch (template) {
    case 'assault':
      return soldierBody(new Builder({ shoulderY: 0.95 }), accent)
        .part(geo.blade, mat(0xcfd8dc, { metalness: 0.9, roughness: 0.2 }), -0.12, 1.0, -0.24, new THREE.Euler(0.2, 0, 0.6))
        .weapon([[geo.shotgun, GUNMETAL(), 0, -0.05, 0.2]], 0.48).rig;
    case 'grenadier':
      return soldierBody(new Builder({ shoulderY: 0.95 }), accent, true).weapon(
        [
          [geo.cannon, GUNMETAL(), 0, -0.05, 0.26],
          [geo.drum, mat(0x3a4048, { metalness: 0.6 }), 0, -0.16, 0.12],
        ],
        0.64,
      ).rig;
    case 'sharpshooter':
      return soldierBody(new Builder({ shoulderY: 0.95 }), accent).weapon(
        [
          [geo.sniper, GUNMETAL(), 0, -0.05, 0.36],
          [geo.scope, mat(0x2a3036, { metalness: 0.7 }), 0, 0.03, 0.22],
        ],
        0.86,
      ).rig;
    case 'specialist':
      return soldierBody(new Builder({ shoulderY: 0.95 }), accent).weapon([[geo.rifle, GUNMETAL(), 0, -0.05, 0.24]], 0.58).drone().rig;
    case 'trooper':
      return new Builder({ shoulderY: 0.95 })
        .part(geo.body, mat(0x4a3439), 0, 0.62, 0)
        .part(geo.plate, mat(0x7a1f2b), 0, 0.85, 0.12)
        .part(geo.head, mat(0x1a1517), 0, 1.38, 0)
        .part(geo.visor, mat(0xff4a4a, { emissive: 0xff2a2a }), 0, 1.4, 0.16)
        .weapon([[geo.rifle, mat(0x3a3f45, { metalness: 0.6 }), 0, -0.05, 0.24]], 0.58).rig;
    case 'officer':
      return new Builder({ shoulderY: 1.0, scale: 1.08 })
        .part(geo.body, mat(0x4a3439), 0, 0.66, 0)
        .part(geo.plate, mat(0x7a1f2b), 0, 0.9, 0.12)
        .part(geo.head, mat(0x1a1517), 0, 1.44, 0)
        .part(geo.visor, mat(0xffc04a, { emissive: 0xff9a2a }), 0, 1.46, 0.16)
        .part(geo.pad, mat(0xc9a13a, { metalness: 0.7, roughness: 0.3 }), -0.28, 1.1, 0)
        .part(geo.pad, mat(0xc9a13a, { metalness: 0.7, roughness: 0.3 }), 0.28, 1.1, 0)
        .weapon([[geo.rifle, mat(0x3a3f45, { metalness: 0.6 }), 0, -0.05, 0.24]], 0.58).rig;
    case 'xenoid':
      return new Builder({ shoulderY: 0.9 })
        .part(geo.slimBody, mat(0x7d838c, { roughness: 0.4 }), 0, 0.62, 0)
        .part(geo.bigHead, mat(0x9aa1ab, { roughness: 0.35 }), 0, 1.4, 0.02)
        .part(geo.eye, mat(0x9dffb0, { emissive: 0x3dff6a }), -0.11, 1.42, 0.25)
        .part(geo.eye, mat(0x9dffb0, { emissive: 0x3dff6a }), 0.11, 1.42, 0.25)
        .weapon([[geo.rifle, mat(0x4b5f52, { metalness: 0.4 }), 0, -0.05, 0.22]], 0.54).rig;
    case 'lancer':
      return new Builder({ shoulderY: 0.98, scale: 1.04 })
        .part(geo.slimBody, mat(0x3a2a30), 0, 0.64, 0)
        .part(geo.plate, mat(0x5a1622), 0, 0.88, 0.1)
        .part(geo.head, mat(0x1a1517), 0, 1.4, 0)
        .part(geo.visor, mat(0x6ad8ff, { emissive: 0x2ab8ff }), 0, 1.42, 0.16)
        .weapon(
          [
            [geo.baton, mat(0x2a2e33, { metalness: 0.7 }), 0, -0.05, 0.36],
            [geo.batonTip, mat(0x8ae8ff, { emissive: 0x3ac8ff }), 0, -0.05, 0.76],
          ],
          0.78,
        ).rig;
    case 'sectoid':
      // Small grey psionic with an oversized head and violet eyes.
      return new Builder({ shoulderY: 0.82, chest: 0.85, scale: 0.92 })
        .part(geo.sectoidBody, mat(0x8a8f99, { roughness: 0.45 }), 0, 0.55, 0)
        .part(geo.sectoidHead, mat(0xa7acb6, { roughness: 0.35 }), 0, 1.25, 0.02)
        .part(geo.sectoidEye, mat(0xd29aff, { emissive: 0x9a3aff }), -0.11, 1.28, 0.22)
        .part(geo.sectoidEye, mat(0xd29aff, { emissive: 0x9a3aff }), 0.11, 1.28, 0.22)
        .weapon([[geo.shotgun, mat(0x4a3f5a, { metalness: 0.4 }), 0, -0.05, 0.18]], 0.44).rig;
    case 'zombie':
      // Hunched, grey-green body with claws instead of a weapon.
      return new Builder({ hip: 0.5, shoulderY: 0.88, chest: 0.95 })
        .part(geo.zombieBody, mat(0x5d6b58, { roughness: 0.9 }), 0, 0.6, 0.06, new THREE.Euler(0.35, 0, 0))
        .part(geo.head, mat(0x7c8a72, { roughness: 0.9 }), 0, 1.16, 0.28)
        .part(geo.eye, mat(0xd29aff, { emissive: 0x9a3aff }), -0.07, 1.2, 0.44)
        .part(geo.eye, mat(0xd29aff, { emissive: 0x9a3aff }), 0.07, 1.2, 0.44)
        .weapon(
          [
            [geo.claw, mat(0xcfd2c4), 0.04, -0.05, 0.18],
            [geo.claw, mat(0xcfd2c4), -0.04, -0.05, 0.2],
          ],
          0.3,
        ).rig;
    case 'relay':
      // Alien relay: a dark pylon with a glowing core and a floating ring.
      return new Builder({ hip: 0.5, shoulderY: 1.4, chest: 1.3 })
        .part(geo.pylonBase, mat(0x2a1e26, { metalness: 0.6, roughness: 0.4 }), 0, 0.28, 0)
        .part(geo.pylonShaft, mat(0x3a2a34, { metalness: 0.5, roughness: 0.4 }), 0, 1.1, 0)
        .part(geo.pylonCore, mat(0xff8aa0, { emissive: 0xff2a5a }), 0, 2.05, 0)
        .part(geo.pylonRing, mat(0xff6a8a, { emissive: 0xff2a5a }), 0, 1.6, 0).rig;
    case 'vip':
      // Civilian in a long coat, carrying a briefcase instead of a weapon.
      return new Builder({ shoulderY: 0.9 })
        .part(geo.coat, mat(0x2e3440), 0, 0.62, 0)
        .part(geo.head, mat(0xc89f84), 0, 1.38, 0)
        .part(geo.plate, mat(accent, { roughness: 0.5 }), 0, 0.9, 0.12)
        .weapon([[geo.briefcase, mat(0x5a4030), 0, -0.2, 0]], 0.2).rig;
    case 'mec':
      return new Builder({ hip: 0.8, shoulderY: 1.45, chest: 1.3, scale: 1.05 })
        .part(geo.mecLeg, mat(0x3a3d42, { metalness: 0.5 }), -0.22, 0.38, 0)
        .part(geo.mecLeg, mat(0x3a3d42, { metalness: 0.5 }), 0.22, 0.38, 0)
        .part(geo.mecBody, mat(0x5a2a30, { metalness: 0.4, roughness: 0.45 }), 0, 1.2, 0)
        .part(geo.mecHead, mat(0x2a2c30, { metalness: 0.5 }), 0, 1.72, 0.05)
        .part(geo.visor, mat(0xff4a4a, { emissive: 0xff2a2a }), 0, 1.73, 0.23)
        .part(geo.mecPod, mat(0x42464c, { metalness: 0.6 }), -0.5, 1.5, -0.05)
        .weapon([[geo.mecGun, mat(0x2a2e33, { metalness: 0.7 }), 0.08, -0.05, 0.3]], 0.72).rig;
  }
}

/** Templates with an articulated figure (figures.ts); the rest keep the placeholder model. */
const FIGURES = new Set<string>(FIGURE_TEMPLATES);

/** The model a unit is shown with: an articulated figure when there is one, in voxels (M5 art). */
export function buildUnit(template: TemplateId, accent: string, appearance?: Appearance, gear?: Gear): UnitRig | FigureRig {
  const rig = FIGURES.has(template) ? buildFigure(template, accent, appearance, gear) : buildModel(template, accent);
  voxelizeUnit(rig);
  return rig;
}

/** Weapon and armour tier as figures.ts wants them (1–3; anything else is tier 1). */
export function gearOf(weaponTier?: number, armorTier?: number): Gear {
  const tier = (n?: number) => Math.min(3, Math.max(1, Math.round(n ?? 1))) as Tier;
  return { weapon: tier(weaponTier), armor: tier(armorTier) };
}

export function isFigure(rig: UnitRig): rig is FigureRig {
  return 'applyPose' in rig;
}

/** A neutral standing pose for figures, to spread overrides onto. */
export const IDLE_POSE: FigurePose = { crouch: 0, aim: 0, run: 0, recoil: 0, hit: 0, flash: 0, fall: 0, lean: 0, hop: 0, yaw: 0, phase: 0 };
