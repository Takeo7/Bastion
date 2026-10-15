import * as THREE from 'three';
import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { HEADS, partColor, TEMPLATES, type Alarm, type Appearance, type ArmorPart, type Arms, type FoundWeaponId, type HeadId, type TemplateId, type Tier } from '@bastion/engine';
import { CROWD, type Baker, type Member } from './crowd';
import type { Armament, Blow, FigurePose, FigureRig, Gear } from './figures';
import { gaitsOf, type GaitMotion, type GaitStyle } from './gaits';
import { UNIT_LAYER } from './post';
import { atlasMaterial, atlasOf, dyedMaterial, loadModel, modelNow, type DyedMaterial } from './synty';
import { AnimDriver, animSet, MOTION_ORDER, whenAnimSet, type AnimSet, type Build } from './syntyAnims';
import {
  CHARACTER_SCALE,
  CHARACTERS,
  DEAD_TEMPLATES,
  DEATHS,
  HITS,
  HEAD_GEAR,
  HEADGEAR,
  PROPS,
  SQUAD_BODIES,
  STANCE_ORDER,
  SURVIVOR_CHOICES,
  SYNTY_BONES,
  SYNTY_SCALE,
  UNIT_BODIES,
  UNIT_WEAPONS,
  WEAPONS,
  type AtlasId,
  type CharacterAsset,
  type CharacterId,
  type Death,
  type HeadgearId,
  type PropId,
  type Hit,
  type Stance,
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
//  - death: one of Synty's death clips, the body falling away from the blow
//    (DEATHS), the weapon let go; until the clips load, the knees give way
//    and the body goes over backwards or sideways.
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
/** The moving clips' places in MOTION_ORDER. */
const WALK = MOTION_ORDER.indexOf('walk');
const RUN = MOTION_ORDER.indexOf('run');
const CROUCH_WALK = MOTION_ORDER.indexOf('crouchWalk');
/** Steps a second each clip takes at its own speed (Synty's clips, measured), so the hips roll once a stride. */
const CADENCE = MOTION_ORDER.map((m) => ({ idle: 0, crouchIdle: 0, walk: 1.9, run: 2.9, crouchWalk: 1.9, sprint: 3.3 })[m]);
/**
 * What a body adds to its gait over the clip (gaits.ts): how far the dead
 * lean forward, and how much their hips roll and their shoulders twist. The
 * common dead drag themselves hunched; the armoured stand straighter and
 * sway heavily from side to side; the bloated one wobbles; the runner goes bent.
 */
const STYLES: Record<NonNullable<GaitStyle> | 'none', { lean: number; roll: number; twist: number }> = {
  none: { lean: 0.38, roll: 1, twist: 1 },
  drag: { lean: 0.5, roll: 1.5, twist: 1.3 },
  heavy: { lean: 0.2, roll: 1.7, twist: 0.4 },
  wobble: { lean: 0.12, roll: 2.6, twist: 0.8 },
  hunched: { lean: 0.55, roll: 0.6, twist: 0.6 },
};

/** Which way the dead fall without a death clip: backwards, or to either side. */
const FALLS = [0, 0, 0.9, -0.9];

/** Figures that bake a body's loops for the crowd (crowd.ts), by its key, while they load: no one sees them. */
const bakers = new Map<string, SyntyFigure>();
/** Frames baked of a walk's loop and of a stance's. */
const WALK_FRAMES = 20;
const STANCE_FRAMES = 24;

/** How far off the way away from the blow a death's fall can be and still fit it (radians). */
const FITS = 0.9;
/** Seconds the weapon takes to drop from the hand to the ground, let go this far into the fall. */
const DROP_SECONDS = 0.4;
const LET_GO = 0.3;

const wrap = (a: number) => Math.atan2(Math.sin(a), Math.cos(a));

/**
 * Which of `deaths` a unit plays for `blow`: one that falls away from it
 * (for a critical, one to the head if any fits; at arm's length, doubling up
 * fits too), the nearest if none does, any for an unknown blow; and of those,
 * one with room to fall (Gameplay: no body through a wall), else the one that
 * takes least room. The same unit picks the same among those that fit.
 */
function deathFor(deaths: readonly Death[], blow: Blow, scale: number): number {
  const all = deaths.map((_, k) => k);
  const room = (k: number) => !blow.room || blow.room(deaths[k]!.falls, deaths[k]!.reach * SYNTY_SCALE * scale);
  const least = (ks: number[]) => ks.reduce((a, b) => (deaths[b]!.reach < deaths[a]!.reach ? b : a));
  let fits = all;
  if (blow.from !== null) {
    const away = blow.from + Math.PI;
    const off = (k: number) => Math.abs(wrap(deaths[k]!.falls - away));
    fits = all.filter((k) => off(k) < FITS || (blow.melee && deaths[k]!.close));
    if (blow.head && fits.some((k) => deaths[k]!.head)) fits = fits.filter((k) => deaths[k]!.head);
    if (!fits.length) fits = [all.reduce((a, b) => (off(b) < off(a) ? b : a))];
  }
  const roomy = fits.filter(room);
  if (roomy.length) return pick(roomy, blow.seed);
  const anywhere = all.filter(room);
  return least(anywhere.length ? anywhere : all);
}

/**
 * Which of `hits` a unit plays for `blow`: at arm's length (a blade, a bite)
 * it doubles up; else for a critical a hard one or one to the head, for the
 * rest one of the others: the one whose blow comes from nearest the way this
 * one does, any of them for an unknown blow.
 */
function hitFor(hits: readonly Hit[], blow: Blow, strength: number): number {
  const all = hits.map((_, k) => k);
  const close = all.find((k) => hits[k]!.close);
  if (blow.melee && close !== undefined) return close;
  const hard = strength > 1;
  const pool = all.filter((k) => (hard ? hits[k]!.hard || hits[k]!.head : !hits[k]!.hard));
  if (blow.from === null) return pick(pool, blow.seed);
  const from = blow.from;
  const off = (k: number) => Math.abs(wrap(hits[k]!.from - from));
  return pool.reduce((a, b) => (off(b) < off(a) ? b : a));
}

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
  /** Rotation that turns the T-posed left hand into one holding the same gun from under its handguard. */
  foreTurn: THREE.Quaternion;
  hip: number;
  chest: number;
  top: number;
}

const rests = new WeakMap<THREE.Object3D, Rest>();

const RIGHT_FINGERS = new THREE.Vector3(0.0, -0.55, 0.84).normalize();
const RIGHT_PALM = new THREE.Vector3(1, 0, 0);
/** Where a gun's grip sits from the right wrist, in the gun's frame. */
const GRIP = RIGHT_FINGERS.clone().multiplyScalar(0.05).addScaledVector(RIGHT_PALM, 0.03);
/**
 * The support hand under the handguard, in the gun's frame: the palm up
 * against it and turned to the gun's right, the fingers across and forward
 * round its far side (the gun's right is −X).
 */
const LEFT_PALM = new THREE.Vector3(-0.4, 0.92, 0).normalize();
const LEFT_FINGERS = new THREE.Vector3(-0.62, -0.1, 0.78).projectOnPlane(LEFT_PALM).normalize();
/** From the middle of the support palm back to the left wrist, in the gun's frame. */
const FORE_WRIST = LEFT_FINGERS.clone().multiplyScalar(-0.045).addScaledVector(LEFT_PALM, -0.018);

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
  const f0L = p[B.Hand_L]!.clone().sub(p[B.Elbow_L]!).normalize();
  const foreTurn = basis(LEFT_FINGERS, LEFT_PALM).multiply(basis(f0L, n0.clone().sub(f0L.clone().multiplyScalar(n0.dot(f0L))).normalize()).transpose());
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
    foreTurn: new THREE.Quaternion().setFromRotationMatrix(foreTurn),
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
const m1 = new THREE.Matrix4();
const m2 = new THREE.Matrix4();
const X = new THREE.Vector3(1, 0, 0);
const Y = new THREE.Vector3(0, 1, 0);
const Z = new THREE.Vector3(0, 0, 1);

/** A rotation about one axis. */
const rot = (out: THREE.Quaternion, axis: THREE.Vector3, angle: number) => out.setFromAxisAngle(axis, angle);
const lerp = THREE.MathUtils.lerp;

// ----------------------------------------------------------------- figures

type Carry = keyof Armament;
const CARRIES: Carry[] = ['weapon', 'sidearm', 'belt'];

/** The models of an armament, by where they are carried. */
type Guns = Partial<Record<Carry, { id: WeaponId; model: THREE.Group }>>;

/** An armament's models if all have loaded (asking starts the loads), else null. */
function gunsNow(a: Armament): Guns | null {
  const out: Guns = {};
  let all = true;
  for (const k of CARRIES) {
    const id = a[k];
    if (!id) continue;
    const model = modelNow(WEAPONS[id].pack, 'wep', id);
    if (model) out[k] = { id, model };
    else all = false;
  }
  return all ? out : null;
}

async function loadGuns(a: Armament): Promise<Guns> {
  const out: Guns = {};
  await Promise.all(CARRIES.map(async (k) => {
    const id = a[k];
    if (id) out[k] = { id, model: await loadModel(WEAPONS[id].pack, 'wep', id) };
  }));
  return out;
}

const sameArms = (a: Armament, b: Armament) => CARRIES.every((k) => (a[k] ?? null) === (b[k] ?? null));
const armsOf = (guns: Guns): Armament => ({ weapon: guns.weapon?.id ?? null, sidearm: guns.sidearm?.id ?? null, belt: guns.belt?.id ?? null });

/** The pistol's grip for the support hand (as WEAPONS fore: height, distance along the barrel): the left hand cups the right. */
const PISTOL_FORE: [number, number] = [-0.07, 0.01];

/** What a Synty unit is made of. */
interface Spec extends Armament {
  character: CharacterId;
  hat: HeadgearId | null;
  /** Barracks colours: helmet, torso, arms, legs; null for a piece left as Synty painted it. */
  dyes: (string | null)[] | null;
  /** The owner's band colour; null for none. */
  band: string | null;
  dead: boolean;
  /** The Chillón: aiming is its scream. */
  scream: boolean;
  drone: boolean;
  /** What the body adds to its gait (gaits.ts). */
  style?: GaitStyle;
  /** How it stands waiting (STANCES); unset, the plain idle. */
  stance?: Stance;
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
  /** Its weapons' models (cloned), which of them is in the hand, and where the others ride (hung from bones: stows). */
  private readonly carried: Partial<Record<Carry, THREE.Object3D>> = {};
  private drawn: Carry = 'weapon';
  private readonly stows: Partial<Record<Carry, THREE.Group>> = {};
  /** Something in the right hand. */
  private armed = false;
  private melee = false;
  private pistol = false;
  /** Foregrip distance along the barrel; 0 for one-handed weapons. */
  private fore = 0;
  /** Height of the support palm in the gun's frame (WEAPONS fore). */
  private foreY = 0;
  /** How far the upper body is bladed to the gun this pose (a turn about the vertical). */
  private blade = 0;
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
  /** What its moves play (MOTION_ORDER) and how fast (setGait, from the unit view's gaits.ts). */
  private gait = { motion: RUN, speed: 1.5 };
  private readonly build: Build;
  /** The character's model, dressed (null while it loads), and the atlas it is painted from. */
  private model: THREE.Object3D | null = null;
  private atlas: AtlasId | null = null;
  /** Its place in the crowd while it is there (crowd.ts), the loop it plays there, at what rate and from what phase. */
  private member: Member | null = null;
  private crowd = { loop: -1, rate: 0, phase: 0 };
  /** A figure that bakes for the crowd: it poses itself, always. */
  private baking = false;
  cutAway = false;
  /** The stance it stands in (STANCE_ORDER), −1 the plain idle. */
  private stance = -1;
  /** The death it plays (DEATHS[build]) and the seconds it takes; null standing. */
  private death: { k: number; seconds: number } | null = null;
  /** A death asked for before the clips loaded: taken once they have, the body lying. */
  private pending: Blow | null = null;
  /** The hit reaction it plays (HITS[build]), the seconds it takes and how much of the upper body it takes; null none. */
  private hit: { k: number; seconds: number; share: number; on: boolean } | null = null;
  /** The weapon let go: where it left the hand (in the yaw's frame), how it lies on the ground, and when. */
  private drop: { p: THREE.Vector3; q: THREE.Quaternion; flat: THREE.Quaternion; at: number } | null = null;

  constructor(private readonly spec: Spec) {
    if (spec.dead) this.gait.motion = WALK;
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
    this.build = build;
    this.stance = spec.stance ? STANCE_ORDER.indexOf(spec.stance) : -1;
    const set = animSet(build, spec.dead);
    if (set) this.anim = this.driver(set);
    else void whenAnimSet(build, spec.dead).then((s) => (this.anim = this.driver(s)), () => undefined);
    const h = spec.hat && HEADGEAR[spec.hat];
    const now = [modelNow(c.pack, 'chr', spec.character), h ? modelNow(h.pack, 'hat', spec.hat!) : null] as const;
    const guns = gunsNow(spec);
    if (now[0] && (!h || now[1]) && guns) this.fill(now[0], guns, now[1] ?? null);
    else {
      void Promise.all([loadModel(c.pack, 'chr', spec.character), loadGuns(spec), h ? loadModel(h.pack, 'hat', spec.hat!) : null])
        .then(([m, g, hat]) => this.fill(m, g, hat))
        .catch((e: unknown) => console.warn(`Synty: ${spec.character} could not be dressed`, e));
    }
  }

  /** Its clips' driver, standing as it should. */
  private driver(set: AnimSet): AnimDriver {
    const a = new AnimDriver(set, Math.random());
    a.setStance(this.stance, true);
    return a;
  }

  setStance(stance: Stance | null): void {
    this.stance = stance ? STANCE_ORDER.indexOf(stance) : -1;
    this.anim?.setStance(this.stance);
  }

  /** Dressed and moving with its clips (both load in the background). */
  get ready(): boolean {
    return !!this.rest && !!this.anim;
  }

  /** Dresses the rig once its models are loaded. */
  private fill(source: THREE.Group, guns: Guns, hat: THREE.Group | null): void {
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
      this.atlas ??= atlasOf(o);
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
    this.model = model;
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
    if (hand) {
      // The hand holds the grip; its rotation in the aim pose is gripTurn · rest, so the gun is the inverse.
      this.socket.quaternion.copy(rest.gripTurn).multiply(rest.q[B.Hand_R]!).invert();
      this.socket.position.copy(GRIP).applyQuaternion(this.socket.quaternion);
      hand.add(this.socket);
    }
    if (spec.weapon || spec.sidearm || spec.belt) this.hangStows(model, rest);
    this.equip(guns);
    // Armed again while its first weapons loaded (arm): those instead.
    if (!sameArms(armsOf(guns), spec)) this.arm({ ...spec }, true);
    if (this.drone) this.drone.position.set(-0.38, this.top + 0.25, -0.1);
  }

  /**
   * Where the weapons not in the hand ride, hung from their bones (placed in
   * model space at rest): the main gun slung across the back, the barrel up
   * over the right shoulder; the pistol in its holster on the right thigh,
   * the muzzle down; the blade in its sheath at the left hip, pointing down
   * and back.
   */
  private hangStows(model: THREE.Object3D, rest: Rest): void {
    model.updateMatrixWorld(true);
    const toModel = new THREE.Matrix4().copy(model.matrixWorld).invert();
    const hang = (k: Carry, bone: number, at: THREE.Vector3, along: THREE.Vector3, top: THREE.Vector3) => {
      const b = this.bones[bone];
      if (!b) return;
      const z = along.normalize();
      const x = new THREE.Vector3().crossVectors(top, z).normalize();
      const y = new THREE.Vector3().crossVectors(z, x);
      const place = new THREE.Matrix4().makeBasis(x, y, z).setPosition(at);
      const g = new THREE.Group();
      new THREE.Matrix4().multiplyMatrices(toModel, b.matrixWorld).invert().multiply(place).decompose(g.position, g.quaternion, g.scale);
      b.add(g);
      this.stows[k] = g;
    };
    const s3 = rest.p[B.Spine_03]!;
    const hipL = rest.p[B.UpperLeg_L]!;
    const hipR = rest.p[B.UpperLeg_R]!;
    hang('weapon', B.Spine_03, new THREE.Vector3(0, s3.y + 0.02, s3.z - 0.11), new THREE.Vector3(-0.45, 0.9, 0), new THREE.Vector3(0, 0, -1));
    hang('sidearm', B.UpperLeg_R, new THREE.Vector3(hipR.x - 0.075, hipR.y - 0.08, hipR.z + 0.01), new THREE.Vector3(0, -1, 0.1), new THREE.Vector3(0, 0, 1));
    hang('belt', B.Hips, new THREE.Vector3(hipL.x + 0.075, hipL.y + 0.03, hipL.z - 0.02), new THREE.Vector3(0.12, -1, -0.4), new THREE.Vector3(0, 0, 1));
  }

  /** Its weapons on it (fill, arm): the drawn one in the hand, the rest where they ride; what it carried before goes. */
  private equip(guns: Guns): void {
    for (const k of CARRIES) {
      this.carried[k]?.removeFromParent();
      delete this.carried[k];
      const g = guns[k];
      if (!g) continue;
      const m = g.model.clone();
      m.scale.setScalar((WEAPONS[g.id] as { size?: number }).size ?? 1);
      onUnitLayer(m);
      // Slung, the main gun hangs by its middle.
      m.updateMatrixWorld(true);
      const box = new THREE.Box3().setFromObject(m);
      m.userData.middle = (box.min.z + box.max.z) / 2;
      m.userData.muzzle = new THREE.Vector3(...((m.children[0]?.userData.muzzle as [number, number, number] | undefined) ?? [0, 0, 0.5])).multiply(m.scale);
      this.carried[k] = m;
    }
    if (!this.carried[this.drawn]) this.drawn = 'weapon';
    this.hold();
  }

  /** The drawn weapon into the hand and the others where they ride; how the hand holds it, and where its muzzle is. */
  private hold(): void {
    const held = this.carried[this.drawn];
    for (const k of CARRIES) {
      const m = this.carried[k];
      if (!m || m === held) continue;
      const stow = this.stows[k];
      if (!stow) {
        m.removeFromParent();
        continue;
      }
      stow.add(m);
      m.position.set(0, 0, k === 'weapon' ? -(m.userData.middle as number) : 0);
    }
    const id = held && this.spec[this.drawn];
    if (held && id && this.socket.parent) {
      const w = WEAPONS[id] as { melee?: boolean; pistol?: boolean; fore?: [number, number] };
      this.melee = !!w.melee;
      this.pistol = !!w.pistol;
      const fore = this.pistol ? PISTOL_FORE : this.melee ? undefined : w.fore;
      this.fore = fore?.[1] ?? 0;
      this.foreY = fore?.[0] ?? 0;
      held.position.set(0, 0, 0);
      this.gunPivot.add(held);
      this.muzzle.position.copy(held.userData.muzzle as THREE.Vector3);
      // (Let go while the body lies, it stays on the ground until it gets up: pickUp.)
      if (!this.drop) {
        this.socket.add(this.gunPivot);
        this.gunPivot.position.set(0, 0, 0);
        this.gunPivot.quaternion.identity();
      }
      this.armed = true;
    } else {
      // Unarmed: shots and effects start in front of the chest.
      this.melee = this.pistol = false;
      this.fore = this.foreY = 0;
      this.muzzle.position.set(0, 0, 0);
      this.yaw.add(this.gunPivot);
      this.gunPivot.position.set(0, this.chest, 0.3);
      this.gunPivot.quaternion.identity();
      this.armed = false;
    }
  }

  /** Which of its weapons is in the hand: the main one, the pistol from its holster or the blade from the belt (one it doesn't carry: the main one). */
  draw(which: Carry): void {
    const k = which !== 'weapon' && !this.spec[which] ? 'weapon' : which;
    if (k === this.drawn) return;
    this.drawn = k;
    if (this.rest) this.hold();
  }

  /** Other weapons (the engine's weaponEquipped): on it as soon as they load, the main one in the hand. */
  arm(a: Armament, again = false): void {
    if (!again && sameArms(a, this.spec)) return;
    this.spec.weapon = a.weapon;
    this.spec.sidearm = a.sidearm ?? null;
    this.spec.belt = a.belt ?? null;
    this.drawn = 'weapon';
    const put = (g: Guns) => {
      if (this.rest && sameArms(armsOf(g), this.spec)) this.equip(g);
    };
    const now = gunsNow(this.spec);
    if (now) put(now);
    else void loadGuns({ ...this.spec }).then(put, () => undefined);
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
    // A dead one at ease is drawn with the crowd (crowd.ts): nothing of its own to pose.
    if (this.crowdPose(p, time)) return;
    // One that died before the clips loaded takes its death now, lying.
    if (this.pending && this.anim && this.rest && p.fall >= 1) this.die(this.pending);
    // A death clip takes the body down itself, the pivot upright; without one the body goes over stiffly.
    const clip = this.death;
    const fall = clip ? 1 : Math.min(1, p.fall);
    const live = 1 - fall;
    this.yaw.rotation.y = p.yaw;
    if (clip) {
      this.pivot.position.y = this.hip + p.hop;
      this.pivot.rotation.set(0, 0, 0);
    } else {
      // The body goes down backwards or to one side; a fall past 1 is the bounce as it settles.
      this.pivot.position.y = lerp(this.hip, 0.14 * this.scale, p.fall) + p.hop;
      this.pivot.rotation.set(((-p.fall * Math.PI) / 2) * Math.cos(this.fallSide), 0, ((p.fall * Math.PI) / 2) * Math.sin(this.fallSide));
    }
    this.flashTo(p.flash * 0.8);
    const r = this.rest;
    if (!r) {
      this.clock = time;
      return;
    }
    if (this.rests(p, time)) return;
    // (Just out of the crowd, its clip is where the crowd's loop had got to: no time to catch up on.)
    const dt = this.clock === -Infinity ? 0 : THREE.MathUtils.clamp(time - this.clock, 0, 0.25);
    this.clock = time;
    const { d, p: pos } = this;
    const dead = this.spec.dead;
    const shamble = dead ? 1 : 0;
    const anim = this.anim;

    // Legs and the spine underneath: the clips, or procedural legs until they load.
    if (anim) this.animate(p, dt);
    else this.legs(p, time);
    const climb = (p.climb ?? 0) * live;
    if (climb > 0.5) this.climbLegs(time + p.phase);
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
    const style = STYLES[this.spec.style ?? 'none'];
    // A hit's knock-back is the clip's when there is one; a little of it stays, for the jolt.
    const knock = p.hit * (this.hit ? 0.12 : 0.38);
    const lean = ((dead ? style.lean : 0) + (anim ? 0 : 0.28 * p.crouch) + p.lean - knock - p.recoil * 0.05 - scream * 0.5 + search * 0.45 + breathe) * live;
    // A two-handed gun blades the upper body: the support shoulder comes forward so that arm bends under
    // the gun instead of reaching across the chest; the gun and the head keep facing ahead (holdWeapon, the neck).
    this.blade = this.fore && !dead ? -(0.22 + 0.24 * p.aim) * (this.pistol ? 0.45 : 1) * live : 0;
    // A melee stroke turns the living's upper body back and then across (the weapon goes with it); the dead grab instead.
    const swing = (p.swing ?? 0) * live;
    const grab = dead ? Math.abs(swing) : 0;
    const twist = shamble * this.sway(1.7, 1.7, 0, p, time) * 0.12 * style.twist * live + this.blade + (dead ? 0 : -swing * 0.75);
    // Without clips the hips already roll (legs()); with them the roll goes on here, once a stride as they move.
    const roll = anim ? shamble * this.sway(1.3, Math.PI * CADENCE[this.gait.motion], 0, p, time) * (0.05 + p.run * 0.08) * style.roll * live : 0;
    if (anim) d[B.Hips]!.premultiply(rot(q1, Z, roll));
    else for (let k = 0; k < SPINE.length; k++) d[SPINE[k]!]!.copy(d[B.Hips]!);
    this.tilt(B.Spine_01, roll, twist * 0.3, lean * 0.35);
    this.tilt(B.Spine_02, roll, twist * 0.6, lean * 0.7);
    const upper = this.tilt(B.Spine_03, roll, twist, lean);
    // Neck and head: they keep the eyes up as the spine leans; the dead's lolls, a screamer's goes back.
    // Aiming a gun the head comes down and over to the stock, the eye behind the sights.
    const sight = this.armed && !this.melee ? p.aim * live : 0;
    const neck = rot(this.neckTurn, Z, shamble * 0.32 * live - sight * 0.12)
      .multiply(rot(q2, X, (-lean * 0.55 + p.hit * 0.25 - scream * 0.6 + grab * 0.4) * live + sight * 0.16))
      .premultiply(upper)
      .premultiply(rot(q2, Y, -this.blade * 0.85));
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
    // (A blade's stroke leans too: the free arm stays out of it.)
    const throwing = Math.abs(p.lean) > 0.04 && live > 0.5 && !dead && !this.melee;
    if (clip) {
      // Dying: the clip's own arms, the hand letting go of the weapon (dropWeapon).
    } else if (live < 0.5) {
      // Fallen: arms limp along the body, the weapon lying flat along it in the hand (not pointing at the sky).
      this.hang('L', 0);
      this.hang('R', 0);
      if (this.armed) {
        const along = v1.set(0.12, -1, 0.15).normalize();
        const flat = v2.copy(Z).addScaledVector(along, -along.z).normalize();
        m1.makeBasis(v3.crossVectors(flat, along), flat, along);
        d[B.Hand_R]!.setFromRotationMatrix(m1).multiply(r.gripTurn);
      }
    } else if (climb > 0.5) {
      // Up a ladder: the free hand goes from rung to rung; the gun hand keeps the gun low, or climbs too.
      this.reach('L', this.rung(1, time + p.phase), v4.set(0.6, -1, -0.3));
      if (this.armed) this.holdWeapon(p, lean);
      else this.reach('R', this.rung(-1, time + p.phase), v4.set(-0.6, -1, -0.3));
    } else if (this.armed) {
      this.holdWeapon(p, lean);
      if (throwing) this.throwArm(p.lean);
      else if (search > 0.01) {
        // Rummaging: the gun stays low in the right hand, the left goes in among the things.
        const grip = tmpR.copy(this.fore ? this.leftHand(p) : this.p[B.Hand_L]!);
        this.reach('L', v3.lerpVectors(grip, this.rummage(1, time), search), v4.set(0.7, -1, 0.2));
      } else if (this.fore) this.support(p);
      else if (anim) this.followArm('L', upper);
      else this.hang('L', p.run * Math.sin(time * 11 + p.phase) * 0.4);
    } else if (dead) {
      // Arms out in front, swaying; raised further on a lunge.
      for (let k = 0; k < SIDES.length; k++) {
        const [side, s] = SIDES[k]!;
        const sway = this.sway(1.6, 1.6, s, p, time) * 0.05 * live;
        v3.set(s * 0.12 * (1 - 0.7 * grab), r.chest - 0.08 + aim * 0.12 + sway + grab * 0.05, r.p[B.Spine_03]!.z + 0.4 + aim * 0.08 + grab * 0.12)
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
    if (clip) this.dropWeapon(Math.min(1, p.fall) * clip.seconds);
  }

  // ------------------------------------------------------------- the crowd

  /** What the crowd knows this figure's body by: the same model, gait style and stance bake alike. */
  private get crowdKey(): string {
    return `${this.spec.character}|${this.spec.style ?? ''}|${this.stance}`;
  }

  /** Which of the crowd's loops the pose is, if it is one (0 shambling along, 1 standing); −1 if the figure has to pose it. */
  private calmLoop(p: FigurePose): number {
    const quiet =
      !this.death && !this.pending && !this.hit && p.fall <= 0 && p.hit < 0.01 && p.flash < 0.01 && p.aim < 0.02 && p.recoil < 0.01 && p.crouch < 0.02 && !p.lean && !p.hop && !(p.swing ?? 0) && !(p.react ?? 0) && !(p.climb ?? 0) && !(p.search ?? 0);
    if (!quiet) return -1;
    if (p.run > 0.98 && this.gait.motion === WALK) return 0;
    // Standing still, and not turning on the spot (that plays a few steps).
    if (p.run < 0.02 && Math.abs(wrap(p.yaw - this.lastYaw)) < 1e-3) return 1;
    return -1;
  }

  /** Into the crowd if calm and its body is baked (starting the bake if not); false to pose the figure as ever. */
  private crowdPose(p: FigurePose, time: number): boolean {
    const loop = this.spec.dead && !this.baking && this.anim && this.model && CROWD.enabled ? this.calmLoop(p) : -1;
    if (loop < 0) {
      this.leaveCrowd();
      return false;
    }
    const key = this.crowdKey;
    if (!CROWD.ready(key)) {
      this.prepareBake(key);
      return false;
    }
    this.member ??= CROWD.join(key);
    if (!this.member) return false;
    // Out of the scene, bones and all: nothing of it to draw or update.
    if (this.body.parent) this.pivot.remove(this.body);
    this.root.updateWorldMatrix(true, false);
    m1.copy(this.root.matrixWorld).multiply(m2.makeRotationY(p.yaw));
    // The loop plays on from where it is: from its own clip's point as it joins (or changes loop), from the
    // crowd's if only the rate changes.
    const rate = loop === 0 ? this.gait.speed : 1;
    const c = this.crowd;
    if (c.loop !== loop || c.rate !== rate) {
      const k = loop === 0 ? WALK : -1;
      const at = c.loop === loop ? this.crowdCycle() : this.anim!.cycle(k);
      const phase = at - (CROWD.clock * rate) / this.anim!.seconds(k);
      this.crowd = { loop, rate, phase: phase - Math.floor(phase) };
    }
    CROWD.set(this.member, m1, loop, this.crowd.phase, rate, this.root.visible && !this.cutAway);
    this.clock = time;
    this.lastYaw = p.yaw;
    return true;
  }

  /** How far through the loop it plays in the crowd it is now (any number: its fraction). */
  private crowdCycle(): number {
    const c = this.crowd;
    return (CROWD.clock * c.rate) / this.anim!.seconds(c.loop === 0 ? WALK : -1) + c.phase;
  }

  /** Out of the crowd: its own figure again, posed at once, its clip at the point the crowd's loop had got to. */
  private leaveCrowd(): void {
    if (!this.member) return;
    CROWD.leave(this.member);
    this.member = null;
    const c = this.crowd;
    if (c.loop >= 0) this.anim?.setCycle(c.loop === 0 ? WALK : -1, this.crowdCycle());
    c.loop = -1;
    if (!this.body.parent) this.pivot.add(this.body);
    this.clock = -Infinity;
  }

  /** A figure of this body of its own, unseen, to bake the crowd's loops from once it has loaded. */
  private prepareBake(key: string): void {
    const fig = bakers.get(key);
    if (!fig) {
      bakers.set(key, new SyntyFigure({ ...this.spec, drone: false }));
      return;
    }
    if (!fig.ready || !fig.model) return;
    bakers.delete(key);
    CROWD.bake(key, () => fig.baker());
  }

  /**
   * One of the dead's sways (the twist, the hips' roll, the arms), `w` radians
   * a second standing and `moving` on the move, `offset` along. With clips it
   * keeps to their loops, the stance's and the gait's (whole turns over each,
   * blended as the unit sets off and stops), as the crowd has them baked: a
   * body goes in and out of the crowd without a jolt.
   */
  private sway(w: number, moving: number, offset: number, p: FigurePose, time: number): number {
    const a = this.anim;
    if (!a) return Math.sin(time * (p.run > 0.05 ? moving : w) + p.phase + offset);
    const at = (k: number, w: number) => Math.sin(2 * Math.PI * Math.max(1, Math.round((w * a.seconds(k)) / (2 * Math.PI))) * a.cycle(k) + offset);
    const run = THREE.MathUtils.clamp(p.run, 0, 1);
    return lerp(run < 1 ? at(-1, w) : 0, run > 0 ? at(this.gait.motion, moving) : 0, run);
  }

  /**
   * The crowd's loops of this figure (crowd.ts Baker): its walk at speed 1 and
   * its stance, each posed frame by frame offline, its mesh skinned on the
   * CPU and read in its yaw's frame. Null until it has loaded.
   */
  private baker(): Baker | null {
    const a = this.anim;
    const model = this.model;
    let mesh: THREE.SkinnedMesh | null = null;
    model?.traverse((o) => {
      if (!mesh && o instanceof THREE.SkinnedMesh) mesh = o;
    });
    if (!a || !this.rest || !model || !mesh) return null;
    this.baking = true;
    const skinned: THREE.SkinnedMesh = mesh;
    const walk = a.set.clips[WALK]!;
    const stand = this.stance >= 0 ? a.set.stances[this.stance]! : a.set.clips[0]!;
    const loops = [
      { frames: WALK_FRAMES, seconds: walk.duration, run: 1 },
      { frames: STANCE_FRAMES, seconds: stand.duration, run: 0 },
    ];
    const source = skinned.geometry;
    const position = source.getAttribute('position') as THREE.BufferAttribute;
    const vertices = position.count;
    // What is drawn: the mesh's own geometry, its positions and normals read from the crowd's textures.
    const geometry = new THREE.BufferGeometry();
    for (const [name, attr] of Object.entries(source.attributes)) if (name !== 'normal' && !name.startsWith('skin')) geometry.setAttribute(name, attr);
    geometry.setIndex(source.index);
    // The frame's skinned positions, for its normals.
    const shape = new THREE.BufferGeometry();
    shape.setIndex(source.index);
    const atlas = this.atlas ?? atlasOf(skinned);
    const pose: FigurePose = { crouch: 0, aim: 0, run: 0, recoil: 0, hit: 0, flash: 0, fall: 0, lean: 0, hop: 0, yaw: 0, phase: 0, spin: 0, search: 0, climb: 0, swing: 0, react: 0 };
    const v = new THREE.Vector3();
    const toYaw = new THREE.Matrix4();
    this.setGait('walk', 1);
    return {
      vertices,
      geometry,
      loops: loops.map(({ frames, seconds }) => ({ frames, seconds })),
      material: () => {
        const m = dyedMaterial(atlas);
        m.userData.dyes.dyeRim.value = ENEMY_RIM;
        return m;
      },
      frame: (k, f, positions, normals) => {
        const l = loops[k]!;
        pose.run = l.run;
        this.seen = true;
        this.far = false;
        // Frame f is f / frames through the loop: its clip put there and posed as it is, no time going by
        // (the sways keep to the clip: sway).
        a.setCycle(k === 0 ? WALK : -1, f / l.frames);
        this.clock = 0;
        this.applyPose(pose, 0);
        this.root.updateMatrixWorld(true);
        skinned.skeleton.update();
        toYaw.copy(this.yaw.matrixWorld).invert().multiply(skinned.matrixWorld);
        for (let i = 0; i < vertices; i++) {
          v.fromBufferAttribute(position, i);
          skinned.applyBoneTransform(i, v);
          v.applyMatrix4(toYaw);
          positions[i * 3] = v.x;
          positions[i * 3 + 1] = v.y;
          positions[i * 3 + 2] = v.z;
        }
        shape.setAttribute('position', new THREE.BufferAttribute(positions, 3));
        shape.computeVertexNormals();
        normals.set((shape.getAttribute('normal') as THREE.BufferAttribute).array as Float32Array);
      },
      dispose: () => shape.dispose(),
    };
  }

  react(blow: Blow, strength: number): number | null {
    const a = this.anim;
    if (!a || !this.rest || this.death) return null;
    const k = hitFor(HITS[this.build], blow, strength);
    this.hit = { k, seconds: a.set.hits[k]!.duration, share: strength > 1 ? 1 : strength >= 1 ? 0.85 : 0.5, on: false };
    return this.hit.seconds;
  }

  die(blow: Blow): number | null {
    const a = this.anim;
    if (!a || !this.rest) {
      this.pending = blow;
      return null;
    }
    this.pending = null;
    const k = deathFor(DEATHS[this.build], blow, this.scale);
    this.death = { k, seconds: a.set.settles[k]! };
    return this.death.seconds;
  }

  /** On its feet again: the moving clips, and the weapon back in the hand. */
  rise(): void {
    this.death = null;
    this.pending = null;
    this.anim?.fallen(null);
    this.pickUp();
  }

  /** A weapon let go back in the hand. */
  private pickUp(): void {
    if (!this.drop) return;
    const g = this.gunPivot;
    this.socket.add(g);
    g.position.set(0, 0, 0);
    g.quaternion.identity();
    g.scale.setScalar(1);
    this.drop = null;
  }

  /**
   * The weapon, `t` seconds into the death: in the hand, then let go (LET_GO
   * of the fall) to drop and lie on its side, pointing the way it fell. Played
   * backwards (getting up), it comes back up into the hand.
   */
  private dropWeapon(t: number): void {
    const g = this.gunPivot;
    if (t < this.death!.seconds * LET_GO) {
      this.pickUp();
      return;
    }
    if (!this.drop) {
      // The unarmed have nothing in the hand.
      if (g.parent !== this.socket) return;
      g.updateWorldMatrix(true, false);
      this.yaw.attach(g);
      const ahead = v1.set(0, 0, 1).applyQuaternion(g.quaternion);
      const flat = new THREE.Quaternion().setFromEuler(e1.set(0, Math.atan2(ahead.x, ahead.z), Math.PI / 2, 'YXZ'));
      this.drop = { p: g.position.clone(), q: g.quaternion.clone(), flat, at: t };
    }
    const d = this.drop;
    const u = THREE.MathUtils.clamp((t - d.at) / DROP_SECONDS, 0, 1);
    g.position.set(d.p.x, lerp(d.p.y, 0.03, u * u), d.p.z);
    g.quaternion.slerpQuaternions(d.q, d.flat, u);
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
    // A figure baking for the crowd poses every frame it is asked to.
    if (!SYNTY_LOD.on || this.baking) return false;
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

  setGait(motion: GaitMotion, speed: number): void {
    this.gait = { motion: MOTION_ORDER.indexOf(motion), speed };
  }

  /** Blends the clips by what the unit does and reads their pose. */
  private animate(p: FigurePose, dt: number): void {
    const a = this.anim!;
    const r = this.rest!;
    if (this.death) {
      // Dying: the death alone, as far in as the fall; the hips go where it takes them, down to the ground.
      a.fallen(this.death.k, Math.min(1, p.fall) * this.death.seconds);
      a.read(this.d, this.hipsShift);
      this.p[B.Hips]!.copy(this.hipsShift).multiplyScalar(r.hip).add(r.p[B.Hips]!);
      this.lastYaw = p.yaw;
      return;
    }
    const dead = this.spec.dead;
    const move = THREE.MathUtils.clamp(p.run, 0, 1);
    const crouch = THREE.MathUtils.clamp(p.crouch, 0, 1);
    // Turning on the spot: a few walking steps while the facing swings round.
    const turned = Math.abs(Math.atan2(Math.sin(p.yaw - this.lastYaw), Math.cos(p.yaw - this.lastYaw)));
    this.lastYaw = p.yaw;
    const turn = dt > 0 ? THREE.MathUtils.clamp((turned / dt) * 0.3, 0, 0.7) * (1 - move) * (1 - crouch) : 0;
    const w = a.weights;
    const g = this.gait;
    w.fill(0);
    w[0] = (1 - move) * (1 - crouch) - turn;
    w[1] = (1 - move) * crouch;
    // A few steps of the walk while turning on the spot; moving, the gait's clip (gaits.ts) at the speed that
    // keeps its feet on the ground (the dead's lurch and hunch go on top).
    w[WALK] += turn;
    w[g.motion] += move * (1 - crouch);
    w[CROUCH_WALK] += move * crouch;
    a.speeds[0] = dead ? 0.6 : 1;
    a.speeds[WALK] = dead ? 0.9 : 1.2;
    a.speeds[CROUCH_WALK] = 1.6;
    a.speeds[g.motion] = g.speed;
    // A hit reaction over the upper body, eased in and out at its ends; over once `react` is back to 0.
    const h = this.hit;
    const react = p.react ?? 0;
    if (h && react > 0 && react < 1) {
      h.on = true;
      const t = react * h.seconds;
      a.react(h.k, t, h.share * Math.min(1, t / 0.06, (h.seconds - t) / 0.2));
    } else if (h && (h.on || react >= 1)) {
      a.react(null);
      this.hit = null;
    }
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

  /** Up a ladder: one knee comes up to the next rung as the other leg pushes, turn about. */
  private climbLegs(time: number): void {
    const r = this.rest!;
    const s = Math.sin(time * 7);
    for (let k = 0; k < 2; k++) {
      const lift = Math.max(0, k ? s : -s);
      const [thigh, shin, foot, ball] = k ? [B.UpperLeg_R, B.LowerLeg_R, B.Ankle_R, B.Ball_R] : [B.UpperLeg_L, B.LowerLeg_L, B.Ankle_L, B.Ball_L];
      this.leg(thigh, shin, foot, ball, -0.25 - 1.05 * lift, 0.3 + 1.4 * lift, k ? -0.05 : 0.05);
    }
    // The lower foot stands on its rung.
    const lowest = Math.min(this.ankle(B.UpperLeg_L, B.LowerLeg_L, B.Ankle_L), this.ankle(B.UpperLeg_R, B.LowerLeg_R, B.Ankle_R));
    this.p[B.Hips]!.y += r.p[B.Ankle_L]!.y - lowest;
  }

  /** Where a hand grips the ladder: a rung up in front, higher on its turn (model space). */
  private rung(side: 1 | -1, time: number): THREE.Vector3 {
    const sh = this.p[side > 0 ? B.Shoulder_L : B.Shoulder_R]!;
    return v3.copy(sh).add(v4.set(side * -0.04, 0.16 + 0.1 * Math.sin(time * 7 + (side > 0 ? Math.PI : 0)), 0.3));
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
    } else if (this.pistol) {
      // Low ready, the muzzle down ahead of the belly; aimed, both arms out and the sights at the eye.
      v1.set(lerp(sh.x * 0.45, sh.x * 0.15, aim), lerp(sh.y - 0.36, sh.y + 0.02, aim) + reload * 0.1, lerp(sh.z + 0.2, sh.z + 0.32, aim));
      e1.set((1 - aim) * 0.7 - p.recoil * 0.45 - reload * 0.6, (1 - aim) * 0.2 + reload * 0.2, reload * 0.9, 'YXZ');
    } else {
      // Shouldered, the grip comes up level with the shoulder: the stock in its pocket, the sights at the eye.
      v1.set(lerp(sh.x * 0.35, sh.x * 0.5, aim), lerp(sh.y - 0.27, sh.y - 0.02, aim) + reload * 0.08, lerp(sh.z + 0.22, sh.z + 0.19, aim));
      e1.set((1 - aim) * 0.42 - p.recoil * 0.18 - reload * 0.5, (1 - aim) * 0.38 + reload * 0.2, reload * 0.9, 'YXZ');
    }
    this.gunQ.setFromEuler(e1);
    if (this.melee) this.gunQ.premultiply(d[s3]!);
    else {
      // A gun stays level however the clip bends the spine (a crouch, a hunch): only the chest's heading turns it.
      const ahead = v2.copy(Z).applyQuaternion(d[s3]!);
      this.gunQ.premultiply(q1.setFromAxisAngle(Y, Math.atan2(ahead.x, ahead.z) - this.blade));
    }
    this.gunP.copy(v1).sub(r.p[s3]!).applyQuaternion(d[s3]!).add(pos[s3]!);
    this.gunP.addScaledVector(v2.copy(Z).applyQuaternion(this.gunQ), -p.recoil * 0.05);
    // Right hand on the grip, holding the gun's turn.
    const hand = v3.copy(GRIP).applyQuaternion(this.gunQ).negate().add(this.gunP);
    // The elbow hangs at low ready and comes out to the side when the stock is shouldered.
    this.reach('R', hand, v4.set(lerp(-0.7, -1, aim), lerp(-1, -0.45, aim), lerp(-0.35, -0.25, aim)));
    d[B.Hand_R]!.copy(this.gunQ).multiply(r.gripTurn);
  }

  /**
   * The support hand on the gun: the wrist to where the palm meets the
   * handguard (two-bone IK, the elbow under the gun), the hand turned to hold
   * it from below. During a reload it goes to the magazine and a pouch, and
   * turns back as it leaves the handguard.
   */
  private support(p: FigurePose): void {
    this.reach('L', this.leftHand(p), v4.set(lerp(0.7, 0.3, p.aim), -1, lerp(-0.2, 0.05, p.aim)));
    const s = THREE.MathUtils.clamp(p.spin ?? 0, 0, 1);
    const on = s <= 0 || s >= 1 ? 1 : Math.max(0, 1 - Math.min(s, 1 - s) / 0.12);
    if (on > 0) this.d[B.Hand_L]!.slerp(q1.copy(this.gunQ).multiply(this.rest!.foreTurn), on);
  }

  /** Where the left wrist goes: on the foregrip, or during a reload to the magazine, a pouch at the hip and back. */
  private leftHand(p: FigurePose): THREE.Vector3 {
    const grip = v3.set(0.012, this.foreY, this.fore).add(FORE_WRIST).applyQuaternion(this.gunQ).add(this.gunP);
    const s = THREE.MathUtils.clamp(p.spin ?? 0, 0, 1);
    if (s <= 0 || s >= 1) return grip;
    const mag = v2.set(0.02, -0.11, 0.03).applyQuaternion(this.gunQ).add(this.gunP);
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

// ------------------------------------------------------------------ the alarm

/**
 * How each alarm looks: its model (none for the factory's, made here: motor),
 * its longest side (tiles) and the quarter turns that point its nose along
 * +z; the pole the factory's stands on (tiles high); its lights, from the
 * middle of the model's top (x across, z along, y up), and their colours; how
 * many turns a second they go round, and the seconds they take to run down
 * once silenced (0: out at once, with a spark).
 */
interface SirenLook {
  prop: PropId | null;
  fit: number;
  turn: number;
  pole: number;
  lights: { x: number; y: number; z: number; color: number }[];
  spin: number;
  stop: number;
}

const SIRENS: Record<Alarm, SirenLook> = {
  police: { prop: 'sirenPolice', fit: 1.95, turn: 0, pole: 0, lights: [{ x: -0.11, y: -0.02, z: -0.12, color: 0x2f6bff }, { x: 0.11, y: -0.02, z: -0.12, color: 0xff2a2a }], spin: 1.6, stop: 0 },
  ambulance: { prop: 'sirenAmbulance', fit: 1.95, turn: 3, pole: 0, lights: [{ x: -0.33, y: -0.01, z: 0.27, color: 0x2f6bff }, { x: 0.33, y: -0.01, z: 0.27, color: 0xff2a2a }], spin: 1.4, stop: 0 },
  factory: { prop: null, fit: 0, turn: 0, pole: 2.1, lights: [{ x: 0, y: 0.04, z: 0, color: 0xff8a1a }], spin: 1.1, stop: 2.5 },
};

/** A sabotage's objective as the battle has it: its alarm, and where a car's nose points (radians about y; 0 along +z). */
export interface Siren {
  alarm: Alarm;
  heading: number;
}

/** A light's beam: white in the middle, gone at the edge (a texture made here). */
let beamTexture: THREE.Texture | null = null;
function beam(): THREE.Texture {
  if (beamTexture) return beamTexture;
  const c = document.createElement('canvas');
  c.width = c.height = 128;
  const g = c.getContext('2d')!;
  const r = g.createRadialGradient(64, 64, 0, 64, 64, 64);
  r.addColorStop(0, 'rgba(255,255,255,1)');
  r.addColorStop(0.35, 'rgba(255,255,255,0.45)');
  r.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = r;
  g.fillRect(0, 0, 128, 128);
  beamTexture = new THREE.CanvasTexture(c);
  return beamTexture;
}

/** Two fans of light either side of a lamp, flat, `radius` long: what goes round. */
function beams(color: number, radius: number, opacity: number): THREE.Mesh {
  const fan = (start: number) => new THREE.CircleGeometry(radius, 12, start, 0.7);
  const geometry = mergeGeometries([fan(-0.35), fan(Math.PI - 0.35)])!.rotateX(-Math.PI / 2);
  const material = new THREE.MeshBasicMaterial({ color, map: beam(), transparent: true, opacity, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, toneMapped: false });
  return new THREE.Mesh(geometry, material);
}

/**
 * The sabotage's objective (playtest 15/10 #14), as the mission's alarm says:
 * a patrol car or an ambulance with its lights going round, two tiles long
 * (the unit on the back tile, the nose over the one the map blocks for it,
 * `sirenCar`), or a factory's motor siren on a pole with its beacon. Each
 * light throws two fans that sweep the air and, wider and fainter, the
 * ground. Silenced (its fall), the cars' lights go out at once with a spark;
 * the motor runs down and its beacon fades with it (as Red's sound does:
 * sfx.ts SIREN_STOP). It never turns to face anyone, nor topples.
 */
class SirenRig implements FigureRig {
  readonly root = new THREE.Group();
  readonly yaw = new THREE.Group();
  readonly pivot = new THREE.Group();
  readonly gunPivot = new THREE.Group();
  readonly muzzle = new THREE.Object3D();
  readonly flash: THREE.Mesh;
  readonly drone = null;
  readonly hip = 0;
  readonly chest: number;
  readonly top: number;
  private readonly look: SirenLook;
  /** The model and its lights, the nose along +z (half a tile ahead for a car, over the tile it is given). */
  private readonly body = new THREE.Group();
  private readonly lamps: { group: THREE.Group; lens: THREE.Mesh; air: THREE.Mesh; ground: THREE.Mesh }[] = [];
  private readonly sparks: THREE.Points;
  private readonly flying: { v: THREE.Vector3; at: number }[] = [];
  private angle = Math.random() * Math.PI * 2;
  private clock = 0;
  /** When it was silenced (its clock); −1 sounding. */
  private silenced = -1;

  constructor(alarm: Alarm, heading: number) {
    const look = (this.look = SIRENS[alarm]);
    const car = !look.pole;
    this.root.add(this.yaw);
    this.yaw.add(this.pivot);
    this.yaw.rotation.y = heading;
    this.pivot.add(this.gunPivot, this.body);
    this.gunPivot.add(this.muzzle);
    if (car) this.body.position.z = 0.5;
    // The factory's pole and the box at its foot, ours.
    if (!car) {
      const steel = new THREE.MeshStandardMaterial({ color: 0x6d7378, metalness: 0.55, roughness: 0.5 });
      const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.06, look.pole, 8).translate(0, look.pole / 2, 0), steel);
      const box = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.3, 0.14).translate(0, 1.0, 0.09), new THREE.MeshStandardMaterial({ color: 0x9a8a3a, metalness: 0.2, roughness: 0.6 }));
      pole.castShadow = box.castShadow = true;
      this.body.add(pole, box);
    }
    this.chest = car ? 0.45 : look.pole;
    this.top = car ? 0.95 : look.pole + 0.5;
    this.gunPivot.position.set(0, this.chest, 0);
    // A hit's flash over the whole of it; sized once the model is in.
    this.flash = new THREE.Mesh(
      new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0),
      new THREE.MeshBasicMaterial({ color: 0xff3a2a, transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending }),
    );
    this.flash.visible = false;
    this.body.add(this.flash);
    this.sparks = new THREE.Points(
      new THREE.BufferGeometry().setAttribute('position', new THREE.BufferAttribute(new Float32Array(14 * 3), 3)),
      new THREE.PointsMaterial({ color: 0xffe2a8, size: 0.07, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false }),
    );
    this.sparks.visible = false;
    this.sparks.frustumCulled = false;
    this.body.add(this.sparks);
    const id = look.prop;
    if (!id) this.motor();
    else {
      const now = modelNow(PROPS[id].pack, 'prop', id);
      if (now) this.fill(now);
      else void loadModel(PROPS[id].pack, 'prop', id).then((m) => this.fill(m)).catch(() => undefined);
    }
    onUnitLayer(this.root);
  }

  /** The factory's motor siren, ours: a yellow drum across the pole's top, its rotor and grilles dark, the beacon on a stalk above it. */
  private motor(): void {
    const top = this.look.pole;
    const yellow = new THREE.MeshStandardMaterial({ color: 0xd8b238, metalness: 0.3, roughness: 0.55 });
    const dark = new THREE.MeshStandardMaterial({ color: 0x2b2e30, metalness: 0.5, roughness: 0.6 });
    const across = (r: number, l: number) => new THREE.CylinderGeometry(r, r, l, 16).rotateZ(Math.PI / 2).translate(0, top + 0.2, 0);
    const parts = [
      new THREE.Mesh(across(0.18, 0.46), yellow),
      new THREE.Mesh(across(0.195, 0.12), dark),
      ...[-1, 1].map((side) => new THREE.Mesh(new THREE.CircleGeometry(0.15, 16).rotateY((side * Math.PI) / 2).translate(side * 0.231, top + 0.2, 0), dark)),
      new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.05, 0.1).translate(0, top + 0.01, 0), dark),
      new THREE.Mesh(new THREE.CylinderGeometry(0.016, 0.016, 0.12, 6).translate(0, top + 0.44, 0), dark),
    ];
    for (const m of parts) m.castShadow = true;
    this.body.add(...parts);
    this.flash.scale.set(0.6, top + 0.6, 0.45);
    this.addLamps(top + 0.5);
  }

  /** The model in, fitted (its longest side `fit` tiles, its nose along +z, on the floor or the pole), and its lights on top. */
  private fill(source: THREE.Group): void {
    const look = this.look;
    const model = source.clone();
    const fitted = new THREE.Group();
    fitted.rotation.y = (look.turn * Math.PI) / 2;
    fitted.add(model);
    fitted.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(fitted);
    const size = box.getSize(new THREE.Vector3());
    const k = look.fit / Math.max(size.x, size.z);
    const centre = box.getCenter(new THREE.Vector3());
    fitted.scale.setScalar(k);
    fitted.position.set(-centre.x * k, look.pole - box.min.y * k, -centre.z * k);
    model.traverse((o) => {
      if (o instanceof THREE.Mesh) o.castShadow = o.receiveShadow = true;
    });
    this.body.add(fitted);
    this.flash.scale.set(size.x * k * 1.08, size.y * k * 1.08, size.z * k * 1.08);
    this.addLamps(look.pole + size.y * k);
    onUnitLayer(this.root);
    this.root.updateMatrixWorld(true);
  }

  /** Its lights, on its top at `height`. */
  private addLamps(height: number): void {
    for (const l of this.look.lights) {
      const group = new THREE.Group();
      group.position.set(l.x, height + l.y, l.z);
      const lens = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.055, 0.08, 10), new THREE.MeshBasicMaterial({ color: new THREE.Color(l.color).multiplyScalar(2.4), toneMapped: false }));
      const air = beams(l.color, 1.6, 0.55);
      const ground = beams(l.color, 2.6, 0.32);
      ground.position.y = 0.03 - group.position.y;
      group.add(lens, air, ground);
      this.body.add(group);
      this.lamps.push({ group, lens, air, ground });
    }
  }

  applyPose(p: FigurePose, time: number): void {
    const look = this.look;
    const dt = THREE.MathUtils.clamp(time - this.clock, 0, 0.1);
    this.clock = time;
    // Silenced from the first moment of its fall: the cars' lights out with a spark, the motor running down.
    if (p.fall > 0 && this.silenced < 0) {
      this.silenced = time;
      if (!look.stop) this.burst();
    } else if (p.fall <= 0) this.silenced = -1;
    const power = this.silenced < 0 ? 1 : look.stop ? Math.max(0, 1 - (time - this.silenced) / look.stop) : 0;
    this.angle += dt * look.spin * Math.PI * 2 * power;
    for (let i = 0; i < this.lamps.length; i++) {
      const l = this.lamps[i]!;
      l.group.visible = power > 0.01;
      // A bar's two lamps a quarter turn apart; each brightest as it faces the camera's way (towards +z, roughly).
      const turn = this.angle + i * Math.PI * 0.5;
      l.air.rotation.y = l.ground.rotation.y = turn;
      const glint = 0.55 + 0.45 * Math.abs(Math.cos(turn));
      (l.lens.material as THREE.MeshBasicMaterial).color.set(look.lights[i]!.color).multiplyScalar((0.6 + 1.8 * glint) * power);
      (l.air.material as THREE.MeshBasicMaterial).opacity = 0.55 * power;
      (l.ground.material as THREE.MeshBasicMaterial).opacity = 0.32 * power;
    }
    this.fly(dt);
    this.flash.visible = p.flash > 0.01;
    (this.flash.material as THREE.MeshBasicMaterial).opacity = p.flash * 0.4;
  }

  /** The spark as the lights cut out: a few bright bits off the bar, falling. */
  private burst(): void {
    const at = this.lamps[0]?.group.position ?? new THREE.Vector3(0, this.top, 0);
    const pos = this.sparks.geometry.getAttribute('position') as THREE.BufferAttribute;
    this.flying.length = 0;
    for (let i = 0; i < pos.count; i++) {
      pos.setXYZ(i, at.x * 0.5, at.y, at.z);
      const a = Math.random() * Math.PI * 2;
      this.flying.push({ v: new THREE.Vector3(Math.cos(a) * (0.6 + Math.random()), 1 + Math.random() * 1.4, Math.sin(a) * (0.6 + Math.random())), at: 0 });
    }
    pos.needsUpdate = true;
    this.sparks.visible = true;
    (this.sparks.material as THREE.PointsMaterial).opacity = 1;
  }

  private fly(dt: number): void {
    if (!this.sparks.visible) return;
    const pos = this.sparks.geometry.getAttribute('position') as THREE.BufferAttribute;
    let life = 0;
    for (let i = 0; i < this.flying.length; i++) {
      const f = this.flying[i]!;
      f.at += dt;
      f.v.y -= 6 * dt;
      pos.setXYZ(i, pos.getX(i) + f.v.x * dt, Math.max(0.02, pos.getY(i) + f.v.y * dt), pos.getZ(i) + f.v.z * dt);
      life = Math.max(life, f.at);
    }
    pos.needsUpdate = true;
    const m = this.sparks.material as THREE.PointsMaterial;
    m.opacity = Math.max(0, 1 - life / 0.5);
    if (m.opacity <= 0) this.sparks.visible = false;
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
/** The enemies still alive (the Cuervos): they stand squared up for a fight. */
const LIVING_ENEMIES = new Set<TemplateId>(['trooper', 'officer', 'lancer']);
const PIECES: ArmorPart[] = ['helmet', 'torso', 'arms', 'legs'];

/**
 * The Synty figure for a unit, or null if the manifest has none for it.
 * `seed` keeps the same survivor for the same soldier (else the look picks it).
 */
/**
 * A unit as a Synty character. `sex` is for who has no look of their own (a
 * VIP, an enemy leader: the engine's sexOfPerson); a soldier's is in the look.
 * `siren` is what a sabotage's objective is (SirenRig).
 */
export function syntyFigure(template: TemplateId, accent: string, look?: Appearance, gear?: Gear, seed?: string, sex?: 'f' | 'm', siren?: Siren): FigureRig | null {
  if (template === 'relay') return new SirenRig(siren?.alarm ?? 'factory', siren?.heading ?? 0);
  const squad = SQUAD.has(template);
  const head: HeadId = look?.head ?? (template === 'sharpshooter' ? 'hood' : 'helmet');
  const build = look?.build ?? (template === 'grenadier' ? 'heavy' : 'normal');
  const bodies = sameSex(squad ? SQUAD_BODIES[build] : UNIT_BODIES[template], look?.sex ?? sex, squad);
  if (!bodies?.length) return null;
  // The barracks' «Personaje» if chosen, else one picked by the soldier's campaign id (the same everywhere).
  const chosen = squad ? SURVIVOR_CHOICES.find((c) => c.id === look?.character)?.id : undefined;
  const character = chosen ?? pick(bodies, seed ?? `${template}|${accent}|${JSON.stringify(look ?? null)}`);
  const arms = armamentOf(template, (gear?.weapon ?? 1) as Tier, gear?.arms);
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
    ...arms,
    hat: squad ? HEAD_GEAR[head] : null,
    dyes,
    band: squad || template === 'vip' ? accent : null,
    dead: DEAD_TEMPLATES.has(template),
    scream: template === 'sectoid',
    style: gaitsOf(template).style,
    stance: DEAD_TEMPLATES.has(template) ? 'drunk' : LIVING_ENEMIES.has(template) ? 'aggressive' : undefined,
    drone: template === 'specialist',
  });
}

/** The model of each of the Armería's weapons (docs/FASE8.md §A), by the soldier's weapon tier (1–3). */
const FOUND_MODELS: Record<FoundWeaponId, WeaponId | [WeaponId, WeaponId, WeaponId]> = {
  assaultRifle: ['rifle', 'rifle2', 'rifle3'],
  pumpShotgun: 'shotgun',
  precisionRifle: ['sniper', 'sniper2', 'sniper2'],
  machineGun: 'machineGun',
  machete: 'machete',
  huntingRifle: 'boltRifle',
  sawnOff: 'shotgun',
  scopedRifle: 'sniper2',
  lightMachineGun: 'machineGun2',
};

/** The model of one of the Armería's weapons at a weapon tier (one on the floor: dropped.ts). */
export function foundModel(kind: FoundWeaponId, tier: Tier = 1): WeaponId {
  const m = FOUND_MODELS[kind];
  return typeof m === 'string' ? m : m[tier - 1]!;
}

/**
 * What a unit carries as models. A campaign soldier's own (Unit.arms): the
 * Armería's weapon in the hand, or the pistol without one; the pistol in its
 * holster beside a main gun; the machete or the knife at the belt. Anyone
 * else, its template's: its weapon by tier, the Francotirador's pistol beside
 * it, the Asalto's machete.
 */
export function armamentOf(template: TemplateId, tier: Tier, arms?: Arms | null): Armament {
  const of = (kind: FoundWeaponId) => foundModel(kind, tier);
  if (arms && SQUAD.has(template)) {
    return { weapon: arms.primary ? of(arms.primary) : 'pistol', sidearm: arms.primary ? 'pistol' : null, belt: arms.melee ? of(arms.melee) : 'knife' };
  }
  const t = TEMPLATES[template];
  return {
    weapon: UNIT_WEAPONS[template]?.[tier - 1] ?? null,
    sidearm: t.secondary === 'pistol' && t.weapon !== 'pistol' ? 'pistol' : null,
    belt: SQUAD.has(template) && t.meleeWeapon === 'sword' ? 'machete' : null,
  };
}

/**
 * The bodies someone picked at random can have: those of their sex in the
 * list. A soldier whose build has none of it (no heavy women) takes any
 * survivor of that sex; anyone else, the whole list; so does whoever's sex is
 * unknown.
 */
function sameSex(bodies: CharacterId[] | undefined, sex: 'f' | 'm' | undefined, squad: boolean): CharacterId[] | undefined {
  if (!sex || !bodies) return bodies;
  const of = (id: CharacterId) => !!(CHARACTERS[id] as CharacterAsset).female === (sex === 'f');
  const kept = bodies.filter(of);
  if (kept.length) return kept;
  return squad ? SURVIVOR_CHOICES.map((c) => c.id).filter(of) : bodies;
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
  for (const { prop } of Object.values(SIRENS)) if (prop) void loadModel(PROPS[prop].pack, 'prop', prop).catch(() => undefined);
  for (const build of ['masc', 'femn'] as const) {
    animSet(build);
    animSet(build, true);
  }
  atlasMaterial('apocalypse');
}
