import * as THREE from 'three';
import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';
import { HEADS, partColor, type Appearance, type ArmorPart, type HeadId, type TemplateId, type Tier } from '@bastion/engine';
import type { FigurePose, FigureRig, Gear } from './figures';
import { UNIT_LAYER } from './post';
import { atlasMaterial, atlasOf, dyedMaterial, loadModel, modelNow, type DyedMaterial } from './synty';
import { AnimDriver, animSet, whenAnimSet, type Build } from './syntyAnims';
import {
  CHARACTER_SCALE,
  CHARACTERS,
  DEAD_TEMPLATES,
  HEAD_GEAR,
  HEADGEAR,
  PROPS,
  RELAY_PROP,
  SQUAD_BODIES,
  SURVIVOR_CHOICES,
  SYNTY_BONES,
  UNIT_BODIES,
  UNIT_WEAPONS,
  WEAPONS,
  type AtlasId,
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
// Every bone gets a rotation in model space on top of its rest (T) pose, in
// layers:
//  - locomotion from Synty's Base Locomotion clips (syntyAnims.ts), blended
//    by what the unit does: idle, crouched in cover, running, crouch-running;
//    the dead play the walk slowed and lurching. Until the clips load, the
//    legs bend procedurally;
//  - the upper body leans on top: hunch, hits, recoil, a throw's wind-up, a
//    scream;
//  - the arms: the right hand holds the weapon (hung from the hand bone)
//    where the pose puts the gun, low ready or aiming, and the left its
//    foregrip, both by two-bone IK; a reload takes the left hand to the
//    magazine and a pouch; the free arm throws; the dead reach out; the
//    unarmed keep the clip's arms;
//  - death: knees give way and the body goes down backwards or sideways.
// Model space: +Z forward, +Y up, the figure's left is +X.

const B = Object.fromEntries(SYNTY_BONES.map((n, i) => [n, i])) as Record<SyntyBone, number>;
const N = SYNTY_BONES.length;
/** Spine to shoulders, parents first. */
const UPPER_BODY = [B.Spine_01, B.Spine_02, B.Spine_03, B.Neck, B.Head, B.Clavicle_L, B.Clavicle_R, B.Shoulder_L, B.Shoulder_R];
const SIDES = [
  ['L', 1],
  ['R', -1],
] as const;
const SPINE = [B.Spine_01, B.Spine_02, B.Spine_03];
const CLAVICLES = [B.Clavicle_L, B.Clavicle_R];
const THIGHS = [B.UpperLeg_L, B.UpperLeg_R];
const SHINS = [B.LowerLeg_L, B.LowerLeg_R];
const ARM_L = [B.Shoulder_L, B.Elbow_L, B.Hand_L];
const ARM_R = [B.Shoulder_R, B.Elbow_R, B.Hand_R];
const ARMS = [...ARM_L, ...ARM_R];
/** Which way the dead fall: backwards, or to either side. */
const FALLS = [0, 0, 0.9, -0.9];

/**
 * How often a figure's bones are posed (level of detail; its facing, fall
 * and hit flash follow every frame regardless): doing something, every
 * frame; standing at ease or far from the camera, 30 times a second; off
 * screen, 8 times; hidden (the fog) or a body lying still, not at all.
 */
const CALM_EVERY = 1 / 30;
const UNSEEN_EVERY = 1 / 8;
/** Squared distance from the camera past which a figure counts as far. */
const FAR2 = 24 * 24;
/** The level of detail on or off (the horde lab compares both). */
export const SYNTY_LOD = { on: true };

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
  /** The Chillón: aiming is its scream. */
  scream: boolean;
  drone: boolean;
  /** A skin tone instead of Synty's one; unset keeps it. */
  skin?: string;
}

const BOUNDS = new THREE.Sphere(new THREE.Vector3(0, 0.6, 0), 1.2);

/**
 * The dyed material of everyone painted as Synty painted them (the dead, the
 * Cuervos): one per atlas, so a crowd draws without a material change per
 * body. A figure takes a material of its own only while a hit flashes it.
 */
const PLAIN = new Map<AtlasId, DyedMaterial>();

function plainMaterial(atlas: AtlasId): DyedMaterial {
  let m = PLAIN.get(atlas);
  if (!m) {
    PLAIN.set(atlas, (m = dyedMaterial(atlas)));
    m.userData.shared = true;
    m.userData.dyes.dyeRim.value = ENEMY_RIM;
  }
  return m;
}

/** The night rim (synty.ts dyeRim) of who has no owner's band: a trace, so the dead stay dark. */
const ENEMY_RIM = 0.22;

/** Puts a unit's meshes on the characters' layer (post.ts): seen and shadowed, left out of the occlusion's pre-pass. */
function onUnitLayer(o: THREE.Object3D): void {
  o.traverse((c) => c.layers.set(UNIT_LAYER));
}

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
  /** The figure moves its own weapon for a reload (no twirl from the unit view). */
  readonly ownsReload = true;
  /** How much taller than true size (CHARACTER_SCALE, and the character's own). */
  private scale = 1;

  private rest: Rest | null = null;
  private bones: (THREE.Object3D | null)[] = [];
  private material: DyedMaterial | null = null;
  /** For a figure on a plain material: its own, made for the first hit, and the meshes that swap to it. */
  private own: DyedMaterial | null = null;
  private readonly painted: THREE.Mesh[] = [];
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
  private anim: AnimDriver | null = null;
  /** Last pose time and facing, to advance the clips and to step round when turning on the spot. */
  private clock = 0;
  private lastYaw = 0;
  private readonly hipsShift = new THREE.Vector3();
  private readonly upperTurn = new THREE.Quaternion();
  private readonly neckTurn = new THREE.Quaternion();
  private readonly held = ARMS.map(() => new THREE.Quaternion());
  private readonly fallSide = FALLS[Math.floor(Math.random() * FALLS.length)]!;
  /** Drawn since the last pose (its body passed the camera's frustum), and how far it was then. */
  private seen = true;
  private far = false;
  /** Full poses in a row with the body lying still: after two its bones stay as they are. */
  private still = 0;
  /** This figure's share of its interval, so figures at one rate don't all pose on the same frame. */
  private readonly stagger = 0.85 + Math.random() * 0.3;
  /** Full poses so far: a skeleton sends its bones to the GPU again only after a new one (or a move). */
  private poses = 0;

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
    const c = CHARACTERS[spec.character] as CharacterAsset;
    this.scale = CHARACTER_SCALE * (c.scale ?? 1);
    // Each unit plays the clips from its own point, so a squad does not breathe in step.
    const build: Build = c.female ? 'femn' : 'masc';
    const set = animSet(build);
    if (set) this.anim = new AnimDriver(set, Math.random());
    else void whenAnimSet(build).then((s) => (this.anim = new AnimDriver(s, Math.random())), () => undefined);
    const w = spec.weapon && WEAPONS[spec.weapon];
    const h = spec.hat && HEADGEAR[spec.hat];
    const now = [modelNow(c.pack, 'chr', spec.character), w ? modelNow(w.pack, 'wep', spec.weapon!) : null, h ? modelNow(h.pack, 'hat', spec.hat!) : null] as const;
    if (now[0] && (!w || now[1]) && (!h || now[2])) this.fill(now[0], now[1] ?? null, now[2] ?? null);
    else {
      void Promise.all([loadModel(c.pack, 'chr', spec.character), w ? loadModel(w.pack, 'wep', spec.weapon!) : null, h ? loadModel(h.pack, 'hat', spec.hat!) : null])
        .then(([m, g, hat]) => this.fill(m, g, hat))
        .catch((e: unknown) => console.warn(`Synty: ${spec.character} could not be dressed`, e));
    }
  }

  /** Dressed and moving with its clips (both load in the background). */
  get ready(): boolean {
    return !!this.rest && !!this.anim;
  }

  /** Dresses the rig once its models are loaded. */
  private fill(source: THREE.Group, gun: THREE.Group | null, hat: THREE.Group | null): void {
    const rest = restOf(source);
    this.rest = rest;
    const k = this.scale;
    this.hip = rest.hip * k;
    this.chest = rest.chest * k;
    this.top = rest.top * k;
    const model = SkeletonUtils.clone(source);
    const spec = this.spec;
    let material: DyedMaterial | null = null;
    const plain = !spec.dyes && !spec.band && !spec.skin;
    model.traverse((o) => {
      if (!(o instanceof THREE.Mesh)) return;
      material ??= plain ? plainMaterial(atlasOf(o)) : dyedMaterial(atlasOf(o));
      o.material = material;
      this.painted.push(o);
      o.castShadow = true;
      if (o instanceof THREE.SkinnedMesh) {
        o.boundingSphere = BOUNDS;
        // Only called when the camera draws it (not for the shadow map): what the level of detail goes by.
        o.onBeforeRender = (_r, _s, camera) => {
          this.seen = true;
          this.far = camera.position.distanceToSquared(this.root.position) > FAR2;
        };
        // A figure at rest that hasn't moved keeps last frame's bone matrices: no recompute, no upload.
        const skeleton = o.skeleton;
        const send = skeleton.update.bind(skeleton);
        const at = new THREE.Matrix4();
        let sent = -1;
        skeleton.update = () => {
          if (sent === this.poses && at.equals(o.matrixWorld)) return;
          sent = this.poses;
          at.copy(o.matrixWorld);
          send();
        };
      }
      // A hat or helmet from the barracks replaces the survivor's own hair and hat.
      if (spec.hat && /^(hair|hat)/.test(o.name)) o.visible = false;
    });
    this.material = material;
    if (material) this.paint(material);
    this.bones = SYNTY_BONES.map((n) => model.getObjectByName(n) ?? null);
    onUnitLayer(model);
    this.body.add(model);
    this.body.scale.setScalar(k);
    this.body.position.y = -this.hip;
    this.pivot.position.y = this.hip;

    const head = this.bones[B.Head];
    if (hat && head) {
      const h = hat.clone();
      h.traverse((o) => {
        if (o instanceof THREE.Mesh && material) o.material = material;
      });
      // Drawn round the head joint in model axes; female heads are smaller.
      h.quaternion.copy(rest.q[B.Head]!).invert();
      if ((CHARACTERS[spec.character] as CharacterAsset).female) h.scale.setScalar(0.93);
      onUnitLayer(h);
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
      onUnitLayer(g);
      this.gunPivot.add(g);
      this.muzzle.position.set(...muzzle);
      // The hand holds the grip; its rotation in the aim pose is gripTurn · rest, so the gun is the inverse.
      this.socket.quaternion.copy(rest.gripTurn).multiply(rest.q[B.Hand_R]!).invert();
      this.socket.position.copy(GRIP).applyQuaternion(this.socket.quaternion);
      this.socket.add(this.gunPivot);
      hand.add(this.socket);
    } else {
      this.gunPivot.position.set(0, this.chest, 0.3);
    }
    if (this.drone) this.drone.position.set(-0.38, this.top + 0.25, -0.1);
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
    // By night the squad (and who it rescues) reads in the dark; the enemy only just; the radio's voices not at all.
    dyes.dyeRim.value = band ? 1 : this.spec.weapon || this.spec.dead ? ENEMY_RIM : 0;
    if (this.spec.skin) {
      dyes.dyeSkin.value.set(this.spec.skin);
      dyes.dyeSkinOn.value = 1;
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
    onUnitLayer(drone);
    drone.position.set(-0.38, 1.45, -0.1);
    this.yaw.add(drone);
    this.drone = drone;
  }

  // ---------------------------------------------------------------- pose

  applyPose(p: FigurePose, time: number): void {
    const fall = Math.min(1, p.fall);
    const live = 1 - fall;
    this.yaw.rotation.y = p.yaw;
    // The body goes down backwards or to one side; a fall past 1 is the bounce as it settles.
    this.pivot.position.y = lerp(this.hip, 0.14 * this.scale, p.fall) + p.hop;
    this.pivot.rotation.set(((-p.fall * Math.PI) / 2) * Math.cos(this.fallSide), 0, ((p.fall * Math.PI) / 2) * Math.sin(this.fallSide));
    this.flashTo(p.flash * 0.8);
    const r = this.rest;
    if (!r) {
      this.clock = time;
      return;
    }
    if (this.rests(p, time)) return;
    const dt = THREE.MathUtils.clamp(time - this.clock, 0, 0.25);
    this.clock = time;
    const { d, p: pos } = this;
    const dead = this.spec.dead;
    const shamble = dead ? 1 : 0;
    const anim = this.anim;

    // Legs and the spine underneath: the clips, or procedural legs until they load.
    if (anim) this.animate(p, dt);
    else this.legs(p, time);
    // Knees give way as the body goes down.
    const buckle = Math.sin(fall * Math.PI) * 0.7;
    if (buckle > 0.01) {
      for (let k = 0; k < 2; k++) {
        d[THIGHS[k]!]!.premultiply(rot(q1, X, -buckle));
        d[SHINS[k]!]!.premultiply(rot(q1, X, buckle * 0.5));
      }
    }

    // The upper body leans on top: the dead's hunch, throw wind-up, hits, recoil, breathing; a scream throws it back.
    const scream = this.spec.scream ? p.aim * live : 0;
    const search = dead ? 0 : (p.search ?? 0) * live;
    const breathe = anim || dead ? 0 : Math.sin(time * 2.1 + p.phase) * 0.015;
    const lean = ((dead ? 0.38 : 0) + (anim ? 0 : 0.28 * p.crouch) + p.lean - p.hit * 0.38 - p.recoil * 0.05 - scream * 0.5 + search * 0.45 + breathe) * live;
    const twist = shamble * Math.sin(time * 1.7 + p.phase) * 0.12 * live;
    // Without clips the hips already roll (legs()); with them the roll goes on here.
    const roll = anim ? shamble * Math.sin(time * (p.run ? 3 : 1.3) + p.phase) * (0.05 + p.run * 0.08) * live : 0;
    if (anim) d[B.Hips]!.premultiply(rot(q1, Z, roll));
    else for (let k = 0; k < SPINE.length; k++) d[SPINE[k]!]!.copy(d[B.Hips]!);
    this.tilt(B.Spine_01, roll, twist * 0.3, lean * 0.35);
    this.tilt(B.Spine_02, roll, twist * 0.6, lean * 0.7);
    const upper = this.tilt(B.Spine_03, roll, twist, lean);
    // Neck and head: they keep the eyes up as the spine leans; the dead's lolls, a screamer's goes back.
    const neck = rot(this.neckTurn, Z, shamble * 0.32 * live).multiply(rot(q2, X, (-lean * 0.55 + p.hit * 0.25 - scream * 0.6) * live)).premultiply(upper);
    if (!anim) d[B.Neck]!.copy(d[B.Hips]!);
    d[B.Neck]!.premultiply(neck);
    if (anim) d[B.Head]!.premultiply(neck);
    else d[B.Head]!.copy(d[B.Neck]!);
    for (let k = 0; k < CLAVICLES.length; k++) {
      const c = d[CLAVICLES[k]!]!;
      if (!anim) c.copy(d[B.Hips]!);
      c.premultiply(upper);
    }
    for (let k = 0; k < UPPER_BODY.length; k++) this.fk(UPPER_BODY[k]!);

    // Arms.
    const aim = p.aim;
    const throwing = Math.abs(p.lean) > 0.04 && live > 0.5 && !dead;
    if (live < 0.5) {
      // Fallen: arms limp along the body, the weapon lying in the hand.
      this.hang('L', 0);
      this.hang('R', 0);
    } else if (this.spec.weapon) {
      this.holdWeapon(p, lean);
      if (throwing) this.throwArm(p.lean);
      else if (search > 0.01) {
        // Rummaging: the gun stays low in the right hand, the left goes in among the things.
        const grip = tmpR.copy(this.fore ? this.leftHand(p) : this.p[B.Hand_L]!);
        this.reach('L', v3.lerpVectors(grip, this.rummage(1, time), search), v4.set(0.7, -1, 0.2));
      } else if (this.fore) this.reach('L', this.leftHand(p), v4.set(0.7, -1, -0.2));
      else if (anim) this.followArm('L', upper);
      else this.hang('L', p.run * Math.sin(time * 11 + p.phase) * 0.4);
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
      if (scream > 0.01) this.screamArms(scream);
    } else if (throwing) {
      this.throwArm(p.lean);
      if (anim) this.followArm('R', upper);
      else this.hang('R', 0);
    } else if (anim) {
      this.followArm('L', upper);
      this.followArm('R', upper);
      if (search > 0.01) {
        // Both hands in, blended from the clip's arms.
        for (let i = 0; i < ARMS.length; i++) this.held[i]!.copy(d[ARMS[i]!]!);
        this.reach('L', this.rummage(1, time), v4.set(0.7, -1, 0.2));
        this.reach('R', this.rummage(-1, time), v4.set(-0.7, -1, 0.2));
        for (let i = 0; i < ARMS.length; i++) d[ARMS[i]!]!.slerp(this.held[i]!, 1 - search);
      }
    } else {
      const swing = p.run * Math.sin(time * 11 + p.phase);
      this.hang('L', swing * 0.5);
      this.hang('R', -swing * 0.5);
    }
    this.write();
    this.poses++;
  }

  /** A hit's red flash; a figure on the plain material takes its own while it lasts. */
  private flashTo(k: number): void {
    const m = this.material;
    if (!m) return;
    if (!m.userData.shared) {
      m.userData.dyes.dyeFlash.value = k;
      return;
    }
    const lit = k > 0.005;
    if (lit && !this.own) {
      this.own = dyedMaterial(atlasOf(this.painted[0]!));
      this.own.userData.dyes.dyeRim.value = ENEMY_RIM;
    }
    const use = lit ? this.own! : m;
    if (this.painted[0]?.material !== use) for (const o of this.painted) o.material = use;
    if (lit) this.own!.userData.dyes.dyeFlash.value = k;
  }

  /** Level of detail: whether the bones can stay as they are this frame (see CALM_EVERY). */
  private rests(p: FigurePose, time: number): boolean {
    const seen = this.seen;
    this.seen = false;
    if (!SYNTY_LOD.on) return false;
    if (!this.root.visible) return true;
    // A body lying still keeps its pose until something moves it (a hit, getting up).
    if (p.fall >= 1 && p.hit < 0.01 && p.recoil < 0.01) {
      if (this.still >= 2) return true;
      this.still++;
      return false;
    }
    this.still = 0;
    const spin = p.spin ?? 0;
    const calm =
      this.far ||
      (p.run < 0.02 && p.aim < 0.02 && p.hit < 0.01 && p.recoil < 0.01 && !p.lean && !p.hop && !p.fall && !(p.search ?? 0) && (spin <= 0 || spin >= 1) && Math.abs(p.yaw - this.lastYaw) < 1e-3);
    const every = !seen ? UNSEEN_EVERY : calm ? CALM_EVERY : 0;
    return time - this.clock < every * this.stagger;
  }

  /** Blends the clips by what the unit does and reads their pose. */
  private animate(p: FigurePose, dt: number): void {
    const a = this.anim!;
    const r = this.rest!;
    const dead = this.spec.dead;
    const move = THREE.MathUtils.clamp(p.run, 0, 1);
    const crouch = THREE.MathUtils.clamp(p.crouch, 0, 1);
    // Turning on the spot: a few walking steps while the facing swings round.
    const turned = Math.abs(Math.atan2(Math.sin(p.yaw - this.lastYaw), Math.cos(p.yaw - this.lastYaw)));
    this.lastYaw = p.yaw;
    const turn = dt > 0 ? THREE.MathUtils.clamp((turned / dt) * 0.3, 0, 0.7) * (1 - move) * (1 - crouch) : 0;
    const w = a.weights;
    w[0] = (1 - move) * (1 - crouch) - turn;
    w[1] = (1 - move) * crouch;
    // The dead shamble: the walk, slowed when idle and lurching when they move (the roll and hunch on top).
    w[2] = (dead ? move * (1 - crouch) : 0) + turn;
    w[3] = dead ? 0 : move * (1 - crouch);
    w[4] = move * crouch;
    a.speeds[0] = dead ? 0.6 : 1;
    // Units cross a tile in a sixth of a second: the clips run fast to keep up.
    a.speeds[2] = dead ? 1.5 : 1.2;
    a.speeds[3] = 1.5;
    a.speeds[4] = 1.6;
    // A corpse stays as it fell. Only a unit at ease looks round now and then.
    if (p.fall < 1) a.update(dt, p.aim < 0.05 && p.hit < 0.01 && !(p.search ?? 0) && !dead);
    a.read(this.d, this.hipsShift);
    // The hips sway as the clip says; their height keeps the lower foot on the ground, whatever the
    // difference between the clip's legs and this character's.
    const hips = this.p[B.Hips]!.copy(this.hipsShift).multiplyScalar(r.hip).add(r.p[B.Hips]!);
    hips.y = r.p[B.Hips]!.y;
    const lowest = Math.min(this.ankle(B.UpperLeg_L, B.LowerLeg_L, B.Ankle_L), this.ankle(B.UpperLeg_R, B.LowerLeg_R, B.Ankle_R));
    hips.y += r.p[B.Ankle_L]!.y - lowest;
  }

  /** Procedural legs, as figures.ts (combat stance, squat, stride, the dead drag a foot), until the clips load. */
  private legs(p: FigurePose, time: number): void {
    const r = this.rest!;
    const { d, p: pos } = this;
    const dead = this.spec.dead;
    const shamble = dead ? 1 : 0;
    const live = 1 - Math.min(1, p.fall);
    const roll = shamble * Math.sin(time * (p.run ? 3 : 1.3) + p.phase) * (0.05 + p.run * 0.08) * live;
    rot(d[B.Hips]!, X, p.run * 0.12 * live).multiply(rot(q1, Z, roll));
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
  }

  /** Leans a spine bone on top of what it has (model space); returns the turn it got. */
  private tilt(i: number, roll: number, twist: number, lean: number): THREE.Quaternion {
    const t = rot(this.upperTurn, Z, roll).multiply(rot(q1, Y, twist)).multiply(rot(q2, X, lean));
    this.d[i]!.premultiply(t);
    return t;
  }

  /** An arm as the clip moves it, carried by the upper body's lean. */
  private followArm(side: 'L' | 'R', upper: THREE.Quaternion): void {
    const arm = side === 'L' ? ARM_L : ARM_R;
    for (let k = 0; k < arm.length; k++) this.d[arm[k]!]!.premultiply(upper);
  }

  /**
   * The weapon in the right hand: low ready at rest, shouldered when aiming,
   * level whatever the lean (as figures.ts); recoil kicks it, a reload tilts
   * it up and over, a melee weapon hangs and is raised to strike.
   */
  private holdWeapon(p: FigurePose, lean: number): void {
    const r = this.rest!;
    const { d, p: pos } = this;
    const sh = r.p[B.Shoulder_R]!;
    const aim = p.aim;
    const s3 = B.Spine_03;
    const reload = Math.sin(THREE.MathUtils.clamp(p.spin ?? 0, 0, 1) * Math.PI);
    if (this.melee) {
      v1.set(sh.x * 1.1, lerp(r.hip + 0.02, sh.y + 0.08, aim), lerp(sh.z + 0.08, sh.z + 0.18, aim));
      e1.set(lerp(1.15, -0.9, aim) - lean, 0, 0, 'YXZ');
    } else {
      v1.set(lerp(sh.x * 0.35, sh.x * 0.5, aim), lerp(sh.y - 0.27, sh.y - 0.09, aim) + reload * 0.08, lerp(sh.z + 0.22, sh.z + 0.19, aim));
      e1.set((1 - aim) * 0.42 - p.recoil * 0.18 - lean - reload * 0.5, (1 - aim) * 0.38 + reload * 0.2, reload * 0.9, 'YXZ');
    }
    this.gunQ.setFromEuler(e1);
    this.gunQ.premultiply(d[s3]!);
    this.gunP.copy(v1).sub(r.p[s3]!).applyQuaternion(d[s3]!).add(pos[s3]!);
    this.gunP.addScaledVector(v2.copy(Z).applyQuaternion(this.gunQ), -p.recoil * 0.05);
    // Right hand on the grip, holding the gun's turn.
    const hand = v3.copy(GRIP).applyQuaternion(this.gunQ).negate().add(this.gunP);
    this.reach('R', hand, v4.set(-0.7, -1, -0.35));
    d[B.Hand_R]!.copy(this.gunQ).multiply(r.gripTurn);
  }

  /** Where the left hand goes: the foregrip, or during a reload to the magazine, a pouch at the hip and back. */
  private leftHand(p: FigurePose): THREE.Vector3 {
    const grip = v3.set(0.03, -0.06, this.fore - 0.04).applyQuaternion(this.gunQ).add(this.gunP);
    const s = THREE.MathUtils.clamp(p.spin ?? 0, 0, 1);
    if (s <= 0 || s >= 1) return grip;
    const mag = v2.set(0.02, -0.15, this.fore * 0.5).applyQuaternion(this.gunQ).add(this.gunP);
    if (s < 0.3) return grip.lerp(mag, s / 0.3);
    if (s > 0.75) return grip.lerp(mag, (1 - s) / 0.25);
    const r = this.rest!;
    const pouch = v1.copy(r.p[B.Hips]!).add(v4.set(0.16, 0.02, 0.1)).sub(r.p[B.Hips]!).applyQuaternion(this.d[B.Hips]!).add(this.p[B.Hips]!);
    return grip.copy(mag).lerp(pouch, Math.sin(((s - 0.3) / 0.45) * Math.PI));
  }

  /** Where a hand rummages: low in front, among the furniture's things, moving about (model space). */
  private rummage(side: 1 | -1, time: number): THREE.Vector3 {
    const r = this.rest!;
    const t = time * 9 + (side > 0 ? 0 : 1.7);
    return v3
      .set(side * (0.1 + Math.sin(t) * 0.06), -r.hip * 0.35 + Math.sin(t * 1.4) * 0.03, 0.42 + Math.cos(t * 0.8) * 0.04)
      .applyQuaternion(this.d[B.Hips]!)
      .add(this.p[B.Hips]!);
  }

  /** The Chillón's scream: arms flung out and back, blended in by `k`. */
  private screamArms(k: number): void {
    const { d } = this;
    for (let i = 0; i < ARMS.length; i++) this.held[i]!.copy(d[ARMS[i]!]!);
    this.setArm('L', tmpU.set(0.85, -0.15, -0.4).normalize(), tmpF.set(0.8, 0.25, -0.35).normalize());
    this.setArm('R', tmpU.set(-0.85, -0.15, -0.4).normalize(), tmpF.set(-0.8, 0.25, -0.35).normalize());
    for (let i = 0; i < ARMS.length; i++) d[ARMS[i]!]!.slerp(this.held[i]!, 1 - k);
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
const tmpR = new THREE.Vector3();
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
    const show = (m: THREE.Group) => {
      const prop = m.clone();
      onUnitLayer(prop);
      this.pivot.add(prop);
    };
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
  // The barracks' «Personaje» if chosen, else one picked by the soldier's campaign id (the same everywhere).
  const chosen = squad ? SURVIVOR_CHOICES.find((c) => c.id === look?.character)?.id : undefined;
  const character = chosen ?? pick(bodies, seed ?? `${template}|${accent}|${JSON.stringify(look ?? null)}`);
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
    scream: template === 'sectoid',
    drone: template === 'specialist',
  });
}

/**
 * A radio voice for its portrait (strategy/portraits.ts): unarmed, in their
 * own clothes, with the helmet, torso, arms and legs colours given (null keeps
 * a piece as Synty painted it) and a skin tone of their own if given.
 */
export function voiceFigure(character: CharacterId, dyes: (string | null)[] | null = null, skin?: string): SyntyFigure {
  return new SyntyFigure({ character, weapon: null, hat: null, dyes, band: null, dead: false, scream: false, drone: false, skin });
}

/** Starts loading every model and atlas the units use, so the first battle does not wait. */
export function preloadSynty(): void {
  const used = new Set<CharacterId>([...Object.values(SQUAD_BODIES).flat(), ...Object.values(UNIT_BODIES).flat()]);
  for (const id of used) void loadModel(CHARACTERS[id].pack, 'chr', id).catch(() => undefined);
  for (const [id, w] of Object.entries(WEAPONS)) void loadModel(w.pack, 'wep', id).catch(() => undefined);
  for (const [id, h] of Object.entries(HEADGEAR)) void loadModel(h.pack, 'hat', id).catch(() => undefined);
  void loadModel(PROPS[RELAY_PROP].pack, 'prop', RELAY_PROP).catch(() => undefined);
  for (const build of ['masc', 'femn'] as const) animSet(build);
  atlasMaterial('apocalypse');
}
