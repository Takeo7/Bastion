import * as THREE from 'three';
import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';
import { HEADS, partColor, type Appearance, type ArmorPart, type HeadId, type TemplateId, type Tier } from '@bastion/engine';
import type { FigurePose, FigureRig, Gear } from './figures';
import { atlasMaterial, atlasOf, dyedMaterial, loadModel, modelNow, type DyedMaterial } from './synty';
import {
  CHARACTERS,
  DEAD_TEMPLATES,
  HEAD_GEAR,
  HEADGEAR,
  PROPS,
  RELAY_PROP,
  SQUAD_BODIES,
  SYNTY_BONES,
  UNIT_BODIES,
  UNIT_WEAPONS,
  WEAPONS,
  type CharacterAsset,
  type CharacterId,
  type HeadgearId,
  type SyntyBone,
  type WeaponId,
} from './syntyManifest';

// Units as Synty characters (syntyManifest.ts says who is who). They keep the
// rig contract of figures.ts (root, yaw, pivot, gunPivot, muzzle, flash,
// drone, hip, chest, applyPose), so the unit views animate them unchanged.
//
// There are no animation clips yet: applyPose turns the bones procedurally.
// Every bone gets a rotation in model space on top of its rest (T) pose; legs
// bend to crouch and run with the hips lowered to keep the feet down, the
// spine leans, and the arms reach with two-bone IK: the right hand holds the
// weapon (hung from the hand bone, as clips will want it) where the pose puts
// the gun, the left hand its foregrip. The dead hunch and reach out instead.
// Model space: +Z forward, +Y up, the figure's left is +X.

const B = Object.fromEntries(SYNTY_BONES.map((n, i) => [n, i])) as Record<SyntyBone, number>;
const N = SYNTY_BONES.length;
/** Spine to shoulders, parents first. */
const UPPER_BODY = [B.Spine_01, B.Spine_02, B.Spine_03, B.Neck, B.Head, B.Clavicle_L, B.Clavicle_R, B.Shoulder_L, B.Shoulder_R];
const SIDES = [
  ['L', 1],
  ['R', -1],
] as const;

// --------------------------------------------------------------- rest pose

/** The bind pose of a character model, in model space; shared by every clone. */
interface Rest {
  parent: number[];
  /** The armature node the hips hang from, inverted (identity as converted, but not assumed). */
  rootQ: THREE.Quaternion;
  rootP: THREE.Vector3;
  q: THREE.Quaternion[];
  p: THREE.Vector3[];
  /** Rotation that turns the T-posed right hand into one gripping a gun that points +Z. */
  gripTurn: THREE.Quaternion;
  hip: number;
  chest: number;
  top: number;
}

const rests = new WeakMap<THREE.Object3D, Rest>();

const RIGHT_FINGERS = new THREE.Vector3(0.0, -0.55, 0.84).normalize();
const RIGHT_PALM = new THREE.Vector3(1, 0, 0);
/** Where a gun's grip sits from the right wrist, in the gun's frame. */
const GRIP = RIGHT_FINGERS.clone().multiplyScalar(0.05).addScaledVector(RIGHT_PALM, 0.03);

function restOf(model: THREE.Object3D): Rest {
  let rest = rests.get(model);
  if (rest) return rest;
  model.updateMatrixWorld(true);
  const bones = SYNTY_BONES.map((n) => model.getObjectByName(n) ?? null);
  const q: THREE.Quaternion[] = [];
  const p: THREE.Vector3[] = [];
  const s = new THREE.Vector3();
  for (const b of bones) {
    const qi = new THREE.Quaternion();
    const pi = new THREE.Vector3();
    b?.matrixWorld.decompose(pi, qi, s);
    q.push(qi);
    p.push(pi);
  }
  const parent = bones.map((b) => {
    for (let o = b?.parent; o; o = o.parent) {
      const i = (SYNTY_BONES as readonly string[]).indexOf(o.name);
      if (i >= 0) return i;
    }
    return -1;
  });
  // The hand in its T-pose: fingers along the forearm, palm down.
  const f0 = p[B.Hand_R]!.clone().sub(p[B.Elbow_R]!).normalize();
  const n0 = new THREE.Vector3(0, -1, 0);
  const basis = (f: THREE.Vector3, n: THREE.Vector3) => new THREE.Matrix4().makeBasis(f, n, f.clone().cross(n).normalize());
  const turn = basis(RIGHT_FINGERS, RIGHT_PALM).multiply(basis(f0, n0.clone().sub(f0.clone().multiplyScalar(n0.dot(f0))).normalize()).transpose());
  const top = (model.getObjectByName('rig')?.userData.height as number | undefined) ?? 1.2;
  const rootQ = new THREE.Quaternion();
  const rootP = new THREE.Vector3();
  bones[B.Hips]?.parent?.matrixWorld.decompose(rootP, rootQ, s);
  rest = {
    parent,
    rootQ: rootQ.invert(),
    rootP,
    q,
    p,
    gripTurn: new THREE.Quaternion().setFromRotationMatrix(turn),
    hip: p[B.Hips]!.y,
    chest: p[B.Spine_03]!.y + 0.06,
    top,
  };
  rests.set(model, rest);
  return rest;
}

// ------------------------------------------------------------ scratch math

const v1 = new THREE.Vector3();
const v2 = new THREE.Vector3();
const v3 = new THREE.Vector3();
const v4 = new THREE.Vector3();
const q1 = new THREE.Quaternion();
const q2 = new THREE.Quaternion();
const e1 = new THREE.Euler();
const X = new THREE.Vector3(1, 0, 0);
const Y = new THREE.Vector3(0, 1, 0);
const Z = new THREE.Vector3(0, 0, 1);

/** A rotation about one axis. */
const rot = (out: THREE.Quaternion, axis: THREE.Vector3, angle: number) => out.setFromAxisAngle(axis, angle);
const lerp = THREE.MathUtils.lerp;

// ----------------------------------------------------------------- figures

/** What a Synty unit is made of. */
interface Spec {
  character: CharacterId;
  weapon: WeaponId | null;
  hat: HeadgearId | null;
  /** Barracks colours: helmet, torso, arms, legs; null for a piece left as Synty painted it. */
  dyes: (string | null)[] | null;
  /** The owner's band colour; null for none. */
  band: string | null;
  dead: boolean;
  drone: boolean;
}

const BOUNDS = new THREE.Sphere(new THREE.Vector3(0, 0.6, 0), 1.2);

export class SyntyFigure implements FigureRig {
  readonly root = new THREE.Group();
  readonly yaw = new THREE.Group();
  readonly pivot = new THREE.Group();
  /** Holds the model, its feet on the ground under the pivot. */
  private readonly body = new THREE.Group();
  readonly gunPivot = new THREE.Group();
  readonly muzzle = new THREE.Object3D();
  readonly flash = new THREE.Mesh();
  drone: THREE.Object3D | null = null;
  hip = 0.6;
  chest = 0.82;
  top = 1.2;

  private rest: Rest | null = null;
  private bones: (THREE.Object3D | null)[] = [];
  private material: DyedMaterial | null = null;
  /** Hand to weapon (fixed), so the gun lands where the pose puts it. */
  private readonly socket = new THREE.Group();
  private melee = false;
  /** Foregrip distance along the barrel; 0 for one-handed weapons. */
  private fore = 0;
  private readonly d = Array.from({ length: N }, () => new THREE.Quaternion());
  private readonly w = Array.from({ length: N }, () => new THREE.Quaternion());
  private readonly p = Array.from({ length: N }, () => new THREE.Vector3());
  private readonly gunQ = new THREE.Quaternion();
  private readonly gunP = new THREE.Vector3();

  constructor(private readonly spec: Spec) {
    this.root.add(this.yaw);
    this.yaw.add(this.pivot);
    this.pivot.add(this.body);
    this.flash.visible = false;
    this.gunPivot.add(this.muzzle);
    // Unarmed: shots and effects start in front of the chest.
    this.gunPivot.position.set(0, this.chest, 0.3);
    this.yaw.add(this.gunPivot);
    if (spec.drone) this.addDrone();
    const c = CHARACTERS[spec.character];
    const w = spec.weapon && WEAPONS[spec.weapon];
    const h = spec.hat && HEADGEAR[spec.hat];
    const now = [modelNow(c.pack, 'chr', spec.character), w ? modelNow(w.pack, 'wep', spec.weapon!) : null, h ? modelNow(h.pack, 'hat', spec.hat!) : null] as const;
    if (now[0] && (!w || now[1]) && (!h || now[2])) this.fill(now[0], now[1] ?? null, now[2] ?? null);
    else {
      void Promise.all([loadModel(c.pack, 'chr', spec.character), w ? loadModel(w.pack, 'wep', spec.weapon!) : null, h ? loadModel(h.pack, 'hat', spec.hat!) : null])
        .then(([m, g, hat]) => this.fill(m, g, hat))
        .catch(() => undefined);
    }
  }

  /** Dresses the rig once its models are loaded. */
  private fill(source: THREE.Group, gun: THREE.Group | null, hat: THREE.Group | null): void {
    const rest = restOf(source);
    this.rest = rest;
    this.hip = rest.hip;
    this.chest = rest.chest;
    this.top = rest.top;
    const model = SkeletonUtils.clone(source);
    const spec = this.spec;
    let material: DyedMaterial | null = null;
    model.traverse((o) => {
      if (!(o instanceof THREE.Mesh)) return;
      material ??= dyedMaterial(atlasOf(o));
      o.material = material;
      o.castShadow = true;
      if (o instanceof THREE.SkinnedMesh) o.boundingSphere = BOUNDS;
      // A hat or helmet from the barracks replaces the survivor's own hair and hat.
      if (spec.hat && /^(hair|hat)/.test(o.name)) o.visible = false;
    });
    this.material = material;
    if (material) this.paint(material);
    this.bones = SYNTY_BONES.map((n) => model.getObjectByName(n) ?? null);
    this.body.add(model);
    this.body.position.y = -rest.hip;
    this.pivot.position.y = rest.hip;

    const head = this.bones[B.Head];
    if (hat && head) {
      const h = hat.clone();
      h.traverse((o) => {
        if (o instanceof THREE.Mesh && material) o.material = material;
      });
      // Drawn round the head joint in model axes; female heads are smaller.
      h.quaternion.copy(rest.q[B.Head]!).invert();
      if ((CHARACTERS[spec.character] as CharacterAsset).female) h.scale.setScalar(0.93);
      head.add(h);
    }

    const hand = this.bones[B.Hand_R];
    if (gun && hand && spec.weapon) {
      const g = gun.clone();
      const muzzle = (g.children[0]?.userData.muzzle as [number, number, number] | undefined) ?? [0, 0, 0.5];
      this.melee = !!(WEAPONS[spec.weapon] as { melee?: boolean }).melee;
      this.fore = this.melee ? 0 : muzzle[2] * 0.45;
      this.yaw.remove(this.gunPivot);
      this.gunPivot.position.set(0, 0, 0);
      this.gunPivot.add(g);
      this.muzzle.position.set(...muzzle);
      // The hand holds the grip; its rotation in the aim pose is gripTurn · rest, so the gun is the inverse.
      this.socket.quaternion.copy(rest.gripTurn).multiply(rest.q[B.Hand_R]!).invert();
      this.socket.position.copy(GRIP).applyQuaternion(this.socket.quaternion);
      this.socket.add(this.gunPivot);
      hand.add(this.socket);
    } else {
      this.gunPivot.position.set(0, rest.chest, 0.3);
    }
    if (this.drone) this.drone.position.set(-0.38, rest.top + 0.25, -0.1);
  }

  /** Barracks colours on the cloth, the owner's band on the arms. */
  private paint(m: DyedMaterial): void {
    const dyes = m.userData.dyes;
    const { dyes: colours, band } = this.spec;
    colours?.forEach((c, i) => {
      if (!c) return;
      dyes.dyeColors.value[i + 1]!.set(c);
      dyes.dyeAmount.value.setComponent(i, 0.6);
    });
    if (band) {
      dyes.dyeColors.value[5]!.set(band);
      dyes.dyeBand.value = 1;
    }
  }

  private addDrone(): void {
    const drone = new THREE.Group();
    const body = new THREE.Mesh(new THREE.OctahedronGeometry(0.1, 0), new THREE.MeshStandardMaterial({ color: 0xb8c4cc, metalness: 0.6, roughness: 0.3 }));
    const ring = new THREE.Mesh(
      new THREE.TorusGeometry(0.13, 0.015, 6, 20).rotateX(Math.PI / 2),
      new THREE.MeshStandardMaterial({ color: 0x7dffb0, emissive: 0x2fd97a, emissiveIntensity: 1.8 }),
    );
    body.castShadow = true;
    drone.add(body, ring);
    drone.position.set(-0.38, 1.45, -0.1);
    this.yaw.add(drone);
    this.drone = drone;
  }

  // ---------------------------------------------------------------- pose

  applyPose(p: FigurePose, time: number): void {
    const live = 1 - p.fall;
    this.yaw.rotation.y = p.yaw;
    this.pivot.position.y = lerp(this.hip, 0.14, p.fall) + p.hop;
    this.pivot.rotation.x = (-p.fall * Math.PI) / 2;
    if (this.material) this.material.userData.dyes.dyeFlash.value = p.flash * 0.8;
    const r = this.rest;
    if (!r) return;
    const { d, p: pos } = this;
    const dead = this.spec.dead;
    const shamble = dead ? 1 : 0;

    // Hips: a forward lean when running, the dead roll as they lurch.
    const roll = shamble * Math.sin(time * (p.run ? 3 : 1.3) + p.phase) * (0.05 + p.run * 0.08) * live;
    rot(d[B.Hips]!, X, p.run * 0.12 * live).multiply(rot(q1, Z, roll));

    // Legs, as figures.ts: combat stance, squat, stride; the dead drag the right foot.
    const gait = dead ? 6 : 11;
    const stride = p.run * Math.sin(time * gait + p.phase);
    const stance = (1 - p.run) * (1 - p.crouch);
    const drag = 1 - shamble * 0.6;
    const thighL = (-1.15 * p.crouch + stride * 0.7 - 0.16 * stance) * live;
    const thighR = (-1.15 * p.crouch - stride * 0.7 * drag + 0.1 * stance) * live;
    const kneeL = (1.9 * p.crouch + p.run * Math.max(0, -stride) * 1.1) * live;
    const kneeR = (1.9 * p.crouch + p.run * Math.max(0, stride) * 1.1) * live;
    this.leg(B.UpperLeg_L, B.LowerLeg_L, B.Ankle_L, B.Ball_L, thighL, kneeL, 0.05 * live);
    this.leg(B.UpperLeg_R, B.LowerLeg_R, B.Ankle_R, B.Ball_R, thighR, kneeR, -0.05 * live);
    // The hips drop until the lower foot is on the ground again.
    pos[B.Hips]!.copy(r.p[B.Hips]!);
    const lowest = Math.min(this.ankle(B.UpperLeg_L, B.LowerLeg_L, B.Ankle_L), this.ankle(B.UpperLeg_R, B.LowerLeg_R, B.Ankle_R));
    pos[B.Hips]!.y += r.p[B.Ankle_L]!.y - lowest;

    // Spine: hunch, crouch, throw wind-up, hit and recoil kicks, breathing; the dead sway.
    const breathe = dead ? 0 : Math.sin(time * 2.1 + p.phase) * 0.015;
    const lean = ((dead ? 0.38 : 0) + 0.28 * p.crouch + p.lean - p.hit * 0.38 - p.recoil * 0.05 + breathe) * live;
    const twist = shamble * Math.sin(time * 1.7 + p.phase) * 0.12 * live;
    this.spine(B.Spine_01, twist * 0.3, lean * 0.35);
    this.spine(B.Spine_02, twist * 0.6, lean * 0.7);
    this.spine(B.Spine_03, twist, lean);
    d[B.Neck]!.copy(d[B.Spine_03]!).multiply(rot(q1, Z, shamble * 0.32 * live)).multiply(rot(q2, X, (-lean * 0.55 + p.hit * 0.25) * live));
    d[B.Head]!.copy(d[B.Neck]!);
    d[B.Clavicle_L]!.copy(d[B.Spine_03]!);
    d[B.Clavicle_R]!.copy(d[B.Spine_03]!);
    for (let k = 0; k < UPPER_BODY.length; k++) this.fk(UPPER_BODY[k]!);

    // Arms.
    const aim = p.aim;
    const throwing = Math.abs(p.lean) > 0.04 && live > 0.5;
    const sh = r.p[B.Shoulder_R]!;
    if (live < 0.5) {
      // Fallen: arms limp along the body, the weapon lying in the hand.
      this.hang('L', 0);
      this.hang('R', 0);
    } else if (this.spec.weapon) {
      // The gun: low ready at rest, shouldered when aiming, level whatever the lean (as figures.ts).
      const s3 = B.Spine_03;
      if (this.melee) {
        v1.set(sh.x * 1.1, lerp(r.hip + 0.02, sh.y + 0.08, aim), lerp(sh.z + 0.08, sh.z + 0.18, aim));
        e1.set(lerp(1.15, -0.9, aim) - lean, 0, 0);
      } else {
        v1.set(lerp(sh.x * 0.35, sh.x * 0.5, aim), lerp(sh.y - 0.27, sh.y - 0.09, aim), lerp(sh.z + 0.22, sh.z + 0.19, aim));
        e1.set((1 - aim) * 0.42 - p.recoil * 0.18 - lean, (1 - aim) * 0.38, 0, 'YXZ');
      }
      this.gunQ.setFromEuler(e1);
      this.gunQ.premultiply(d[s3]!);
      this.gunP.copy(v1).sub(r.p[s3]!).applyQuaternion(d[s3]!).add(pos[s3]!);
      this.gunP.addScaledVector(v2.copy(Z).applyQuaternion(this.gunQ), -p.recoil * 0.05);
      // Right hand on the grip, holding the gun's turn.
      const hand = v3.copy(GRIP).applyQuaternion(this.gunQ).negate().add(this.gunP);
      this.reach('R', hand, v4.set(-0.7, -1, -0.35));
      d[B.Hand_R]!.copy(this.gunQ).multiply(r.gripTurn);
      if (throwing && !dead) this.throwArm(p.lean);
      else if (this.fore) {
        const grip = v3.set(0.03, -0.06, this.fore - 0.04).applyQuaternion(this.gunQ).add(this.gunP);
        this.reach('L', grip, v4.set(0.7, -1, -0.2));
      } else this.hang('L', stride * 0.4);
    } else if (dead) {
      // Arms out in front, swaying; raised further on a lunge.
      for (let k = 0; k < SIDES.length; k++) {
        const [side, s] = SIDES[k]!;
        const sway = Math.sin(time * 1.6 + p.phase + s) * 0.05 * live;
        v3.set(s * 0.12, r.chest - 0.08 + aim * 0.12 + sway, r.p[B.Spine_03]!.z + 0.4 + aim * 0.08)
          .sub(r.p[B.Spine_03]!)
          .applyQuaternion(d[B.Spine_03]!)
          .add(pos[B.Spine_03]!);
        this.reach(side, v3, v4.set(s * 0.8, -0.7, -0.3));
      }
    } else if (throwing) {
      this.throwArm(p.lean);
      this.hang('R', 0);
    } else {
      this.hang('L', stride * 0.5);
      this.hang('R', -stride * 0.5);
    }
    this.write();
  }

  /** Model-space rotation of a spine bone: the hips' turn, a twist and a forward lean. */
  private spine(i: number, twist: number, lean: number): void {
    this.d[i]!.copy(this.d[B.Hips]!).multiply(rot(q1, Y, twist)).multiply(rot(q2, X, lean));
  }

  private leg(thigh: number, shin: number, foot: number, ball: number, t: number, k: number, splay: number): void {
    const d = this.d;
    d[thigh]!.copy(d[B.Hips]!).multiply(rot(q1, Z, splay)).multiply(rot(q2, X, t));
    d[shin]!.copy(d[B.Hips]!).multiply(rot(q1, Z, splay)).multiply(rot(q2, X, t + k));
    d[foot]!.copy(d[B.Hips]!);
    d[ball]!.copy(d[B.Hips]!);
  }

  /** Height of a posed ankle (the hips where they rest). */
  private ankle(thigh: number, shin: number, foot: number): number {
    this.fk(thigh);
    this.fk(shin);
    return this.fk(foot).y;
  }

  /** Posed position of bone i from its parent's (which must be posed already). */
  private fk(i: number): THREE.Vector3 {
    const r = this.rest!;
    const parent = r.parent[i]!;
    if (parent < 0) return this.p[i]!.copy(r.p[i]!);
    return this.p[i]!.copy(r.p[i]!).sub(r.p[parent]!).applyQuaternion(this.d[parent]!).add(this.p[parent]!);
  }

  /** Points the upper arm and forearm along two directions; the wrist stays straight. */
  private setArm(side: 'L' | 'R', upper: THREE.Vector3, fore: THREE.Vector3): void {
    const r = this.rest!;
    const sh = side === 'L' ? B.Shoulder_L : B.Shoulder_R;
    const el = side === 'L' ? B.Elbow_L : B.Elbow_R;
    const hd = side === 'L' ? B.Hand_L : B.Hand_R;
    const d = this.d;
    v1.copy(r.p[el]!).sub(r.p[sh]!).normalize();
    d[sh]!.setFromUnitVectors(v1, upper);
    v2.copy(r.p[hd]!).sub(r.p[el]!).normalize().applyQuaternion(d[sh]!);
    d[el]!.setFromUnitVectors(v2, fore).multiply(d[sh]!);
    d[hd]!.copy(d[el]!);
    this.fk(el);
    this.fk(hd);
  }

  /** Two-bone IK: the wrist reaches `target`, the elbow bends towards `pole` (model space). */
  private reach(side: 'L' | 'R', target: THREE.Vector3, pole: THREE.Vector3): void {
    const r = this.rest!;
    const sh = side === 'L' ? B.Shoulder_L : B.Shoulder_R;
    const el = side === 'L' ? B.Elbow_L : B.Elbow_R;
    const hd = side === 'L' ? B.Hand_L : B.Hand_R;
    const a = r.p[el]!.distanceTo(r.p[sh]!);
    const c = r.p[hd]!.distanceTo(r.p[el]!);
    const s = this.p[sh]!;
    const dir = v1.copy(target).sub(s);
    const len = THREE.MathUtils.clamp(dir.length(), Math.abs(a - c) + 0.01, a + c - 0.001);
    dir.normalize();
    const alpha = Math.acos(THREE.MathUtils.clamp((a * a + len * len - c * c) / (2 * a * len), -1, 1));
    const axis = v2.copy(dir).cross(pole);
    if (axis.lengthSq() < 1e-6) axis.set(1, 0, 0);
    axis.normalize();
    const upper = dir.applyAxisAngle(axis, alpha);
    const elbow = v2.copy(s).addScaledVector(upper, a);
    const fore = elbow.sub(target).negate().normalize();
    // setArm uses v1 and v2 itself: hand it copies.
    this.setArm(side, tmpU.copy(upper), tmpF.copy(fore));
  }

  /** An arm hanging at the side, swinging by `swing`. */
  private hang(side: 'L' | 'R', swing: number): void {
    const s = side === 'L' ? 1 : -1;
    this.setArm(side, tmpU.set(s * 0.16, -1, 0.06 + swing).normalize(), tmpF.set(s * 0.06, -1, 0.35 + swing).normalize());
  }

  /** Overhead throw with the free (left) arm: back and up in the wind-up, forward at the release. */
  private throwArm(lean: number): void {
    const t = THREE.MathUtils.clamp((lean + 0.35) / 0.65, 0, 1);
    this.setArm(
      'L',
      tmpU.set(0.2, 0.75, -0.6).lerp(v3.set(0.15, 0.4, 0.9), t).normalize(),
      tmpF.set(0.1, 0.95, -0.25).lerp(v3.set(0.1, 0.25, 0.95), t).normalize(),
    );
  }

  /** Model-space turns to bone-local rotations; the hips also move. */
  private write(): void {
    const r = this.rest!;
    const { d, w, bones } = this;
    for (let i = 0; i < N; i++) {
      w[i]!.copy(d[i]!).multiply(r.q[i]!);
      const bone = bones[i];
      if (!bone) continue;
      const parent = r.parent[i]!;
      if (parent < 0) bone.quaternion.copy(r.rootQ).multiply(w[i]!);
      else bone.quaternion.copy(w[parent]!).invert().multiply(w[i]!);
    }
    bones[B.Hips]?.position.copy(this.p[B.Hips]!).sub(r.rootP).applyQuaternion(r.rootQ);
  }
}

const tmpU = new THREE.Vector3();
const tmpF = new THREE.Vector3();

// ------------------------------------------------------------------ props

/** An objective that is a thing, not a person (the loudspeaker): it only turns, flashes and topples. */
class SyntyProp implements FigureRig {
  readonly root = new THREE.Group();
  readonly yaw = new THREE.Group();
  readonly pivot = new THREE.Group();
  readonly gunPivot = new THREE.Group();
  readonly muzzle = new THREE.Object3D();
  readonly flash: THREE.Mesh;
  readonly drone = null;
  readonly hip = 0;
  readonly chest = 1.0;
  readonly top = 1.6;

  constructor(id: keyof typeof PROPS) {
    const a = PROPS[id];
    this.root.add(this.yaw);
    this.yaw.add(this.pivot);
    this.gunPivot.position.set(0, 1.2, 0);
    this.gunPivot.add(this.muzzle);
    this.pivot.add(this.gunPivot);
    this.flash = new THREE.Mesh(
      new THREE.CylinderGeometry(0.35, 0.35, 1.6, 10).translate(0, 0.8, 0),
      new THREE.MeshBasicMaterial({ color: 0xff3a2a, transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending }),
    );
    this.flash.visible = false;
    this.pivot.add(this.flash);
    const show = (m: THREE.Group) => this.pivot.add(m.clone());
    const now = modelNow(a.pack, 'prop', id);
    if (now) show(now);
    else void loadModel(a.pack, 'prop', id).then(show).catch(() => undefined);
  }

  applyPose(p: FigurePose): void {
    this.yaw.rotation.y = p.yaw;
    this.pivot.rotation.x = (-p.fall * Math.PI) / 2.2;
    this.flash.visible = p.flash > 0.01;
    (this.flash.material as THREE.MeshBasicMaterial).opacity = p.flash * 0.4;
  }
}

// ----------------------------------------------------------------- choice

/** Stable pick from a list by a string (FNV-1a). */
function pick<T>(list: readonly T[], seed: string): T {
  let h = 0x811c9dc5;
  for (let i = 0; i < seed.length; i++) h = Math.imul(h ^ seed.charCodeAt(i), 0x01000193);
  return list[(h >>> 0) % list.length]!;
}

const SQUAD = new Set<TemplateId>(['rookie', 'assault', 'grenadier', 'sharpshooter', 'specialist']);
const PIECES: ArmorPart[] = ['helmet', 'torso', 'arms', 'legs'];

/**
 * The Synty figure for a unit, or null if the manifest has none for it.
 * `seed` keeps the same survivor for the same soldier (else the look picks it).
 */
export function syntyFigure(template: TemplateId, accent: string, look?: Appearance, gear?: Gear, seed?: string): FigureRig | null {
  if (template === 'relay') return new SyntyProp(RELAY_PROP);
  const squad = SQUAD.has(template);
  const head: HeadId = look?.head ?? (template === 'sharpshooter' ? 'hood' : 'helmet');
  const build = look?.build ?? (template === 'grenadier' ? 'heavy' : 'normal');
  const bodies = squad ? SQUAD_BODIES[build] : UNIT_BODIES[template];
  if (!bodies?.length) return null;
  const character = pick(bodies, seed ?? `${template}|${accent}|${JSON.stringify(look ?? null)}`);
  const tier = (gear?.weapon ?? 1) as Tier;
  const weapon = UNIT_WEAPONS[template]?.[tier - 1] ?? null;
  let dyes: Spec['dyes'] = null;
  if (squad) {
    // The helmet and the jacket take the armour colours; sleeves and trousers only a colour of their
    // own, so each survivor keeps their clothes and the owner's band shows on the arms.
    const armour = '#' + new THREE.Color(accent).lerp(new THREE.Color(0x56605a), 0.55).getHexString();
    const own = (part: ArmorPart) => {
      if (part === 'arms' || part === 'legs') return look?.[part] ?? null;
      return look ? partColor({ ...look, head }, part) : part === 'helmet' && HEADS[head].paint === 'trim' ? accent : armour;
    };
    dyes = PIECES.map(own);
  }
  return new SyntyFigure({
    character,
    weapon,
    hat: squad ? HEAD_GEAR[head] : null,
    dyes,
    band: squad || template === 'vip' ? accent : null,
    dead: DEAD_TEMPLATES.has(template),
    drone: template === 'specialist',
  });
}

/** Starts loading every model and atlas the units use, so the first battle does not wait. */
export function preloadSynty(): void {
  const used = new Set<CharacterId>([...Object.values(SQUAD_BODIES).flat(), ...Object.values(UNIT_BODIES).flat()]);
  for (const id of used) void loadModel(CHARACTERS[id].pack, 'chr', id).catch(() => undefined);
  for (const [id, w] of Object.entries(WEAPONS)) void loadModel(w.pack, 'wep', id).catch(() => undefined);
  for (const [id, h] of Object.entries(HEADGEAR)) void loadModel(h.pack, 'hat', id).catch(() => undefined);
  void loadModel(PROPS[RELAY_PROP].pack, 'prop', RELAY_PROP).catch(() => undefined);
  atlasMaterial('apocalypse');
}
