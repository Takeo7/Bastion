import * as THREE from 'three';
import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { HEADS, partColor, TEMPLATES, WORN_BELOW, type Alarm, type Appearance, type ArmorPart, type Arms, type FoundWeaponId, type HeadId, type TemplateId, type Tier } from '@bastion/engine';
import { ARM_BINS, bulkOf, depthIn, easeShoulders, type Bulk } from './bulk';
import { CROWD, type Baker, type Member } from './crowd';
import type { Armament, Blow, Carried, FigurePose, FigureRig, Gear } from './figures';
import { gaitsOf, type GaitMotion, type GaitStyle } from './gaits';
import { UNIT_LAYER } from './post';
import { atlasMaterial, atlasOf, dyedMaterial, loadModel, modelNow, type DyedMaterial } from './synty';
import { AnimDriver, animSet, campClips, MOTION_ORDER, whenAnimSet, type ActionPlay, type AnimSet, type Build } from './syntyAnims';
import {
  CAMP_FREE,
  CAMP_LOOPS,
  CAMP_MOVES,
  CAMP_VARIATIONS,
  CHARACTER_SCALE,
  CHARACTERS,
  DEAD_TEMPLATES,
  DEATHS,
  FOOTPRINT_FIT,
  HITS,
  PROPS,
  RIFLE_RESTS,
  STANCE_ORDER,
  SYNTY_BONES,
  SYNTY_SCALE,
  UNIT_BODIES,
  UNIT_WEAPONS,
  WEAPON_PARTS,
  WEAPONS,
  type ActionId,
  type AtlasId,
  type CampLoop,
  type CampMove,
  type CharacterAsset,
  type CharacterId,
  type Death,
  type PropAsset,
  type PropId,
  type Hit,
  type Gesture,
  type Stance,
  type SyntyBone,
  type WeaponAsset,
  type WeaponId,
  type WeaponPartId,
} from './syntyManifest';
import { CIVIL_OUTFITS, dressOf, loadWardrobe, preloadWardrobe, wardrobeNow, type Dress } from './wardrobe';
import { graphics, onGraphics } from '../settings';

// Units as Synty characters (syntyManifest.ts says who is who). They keep the
// rig contract of figures.ts (root, yaw, pivot, gunPivot, muzzle, flash,
// hip, chest, applyPose), so the unit views animate them unchanged.
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
/** The bones a body's bulk is measured by (bulk.ts). */
const BULK_BONES = {
  hips: B.Hips,
  spine: [B.Spine_01, B.Spine_02, B.Spine_03],
  thighs: [B.UpperLeg_L, B.UpperLeg_R] as [number, number],
  shins: [B.LowerLeg_L, B.LowerLeg_R] as [number, number],
  ankles: [B.Ankle_L, B.Ankle_R] as [number, number],
  clavicles: [B.Clavicle_L, B.Clavicle_R] as [number, number],
  shoulders: [B.Shoulder_L, B.Shoulder_R] as [number, number],
  elbows: [B.Elbow_L, B.Elbow_R] as [number, number],
  hands: [B.Hand_L, B.Hand_R] as [number, number],
};
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
 * takes least room. The same unit picks the same among those that fit. With
 * room for none, the crumple (the one that takes least), `cramped`: pulled in
 * to lie within its own tile (die).
 */
function deathFor(deaths: readonly Death[], blow: Blow, scale: number): { k: number; cramped: boolean } {
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
  if (roomy.length) return { k: pick(roomy, blow.seed), cramped: false };
  const anywhere = all.filter(room);
  return anywhere.length ? { k: least(anywhere), cramped: false } : { k: least(all), cramped: true };
}

/** A cramped death (deathFor) lies with its head this far from its tile's middle at most (tiles): inside the tile. */
const CRAMPED_REACH = 0.42;

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
/**
 * The fingers posed by what each hand does, and the arms kept out of the chest (the weapons lab compares with and
 * without); and out of the gun in the hand (`gun`: the arms' search, `fit`: a clip's gun turned about the wrist, `swing`:
 * a clip's right elbow swung, `slide`: an aimed gun slid ahead; 22/10), the rifle at rest on `rest` for everyone (null,
 * each on their own: Spec.rest); `aim`, aimed, the body turned and a pistol levelled for the barrel to point ahead
 * (aimTurn); `move`, moving with a gun in the hands its clips (ARMED_MOTIONS).
 */
export const SYNTY_HANDS = { fingers: true, elbows: true, gun: true, fit: true, swing: true, slide: true, rest: null as ActionId | null, aim: true, move: true };

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
  /** The fingers of each hand the model has (some have none): their joints and the axis each bends on, in its own frame. */
  fingers: FingerChain[];
  /** The body as its own mesh fills it at rest (its coat and kit and all), part by part: the arms and what it carries keep out of it. */
  bulk: Bulk | null;
  /** Its arms' length, shoulder to wrist, against POLYGON's (REF_ARM): Sidekick's are shorter, and a rifle is held nearer. */
  reach: number;
}

/** One of a hand's fingers (Synty's: the thumb, the index, the other three as one), from the knuckle out. */
interface FingerChain {
  side: 'L' | 'R';
  kind: 'thumb' | 'index' | 'finger';
  joints: number[];
  /** The axis each joint curls on towards the palm, in that joint's rest frame. */
  axes: THREE.Vector3[];
}

const rests = new WeakMap<THREE.Object3D, Rest>();

const RIGHT_FINGERS = new THREE.Vector3(0.0, -0.55, 0.84).normalize();
/** The palm's way in the T-pose (down): the fingers curl towards it. */
const PALM = new THREE.Vector3(0, -1, 0);

/**
 * What a hand does with its fingers (radians at each joint, from the knuckle out; the thumb has three): relaxed at
 * the side, round a gun's grip with the index along the trigger, closed on a handle, cradling a handguard, a fist,
 * a dead one's claw, open to take something.
 */
type HandPose = 'relaxed' | 'trigger' | 'grip' | 'support' | 'fist' | 'claw' | 'open';
export const HAND_POSES: Record<HandPose, { thumb: number[]; index: number[]; finger: number[] }> = {
  relaxed: { thumb: [0.1, 0.15, 0.1], index: [0.25, 0.35, 0.25, 0], finger: [0.4, 0.5, 0.35, 0] },
  trigger: { thumb: [0.7, 0.55, 0.4], index: [0.45, 0.35, 0.2, 0], finger: [1.2, 1.35, 0.9, 0] },
  grip: { thumb: [0.9, 0.6, 0.45], index: [1.15, 1.3, 0.85, 0], finger: [1.25, 1.35, 0.9, 0] },
  support: { thumb: [0.75, 0.5, 0.35], index: [1.0, 0.9, 0.6, 0], finger: [1.1, 1.0, 0.7, 0] },
  fist: { thumb: [0.6, 0.7, 0.5], index: [1.4, 1.5, 1.1, 0], finger: [1.45, 1.55, 1.1, 0] },
  claw: { thumb: [0.25, 0.3, 0.4], index: [0.15, 0.9, 0.8, 0], finger: [0.25, 1.0, 0.85, 0] },
  open: { thumb: [0, 0.05, 0.05], index: [0.05, 0.08, 0.05, 0], finger: [0.1, 0.12, 0.08, 0] },
};
const FLAT = [0, 0, 0, 0];
/** How fast a hand's fingers go to a new pose (per second, eased). */
const HAND_EASE = 14;
/** Seconds a figure takes to go out of the camera's way, or back (veil). */
const VEIL_SECONDS = 0.2;
/**
 * Out of the body (playtest 20/10 #1, and #2: the old steps jumped): how deep a point may touch it (skin on skin), how
 * far an elbow swings round its shoulder–hand line and an arm out to the side at most and by what steps it looks, and
 * how fast either eases there (per second). Where along an arm it is looked at (upper arm, forearm).
 */
const TOUCH = 0.012;
const SWING_STEP = Math.PI / 30;
const SWING_MAX = Math.PI / 2;
const SPREAD_STEP = Math.PI / 45;
const SPREAD_MAX = Math.PI / 4;
/** The pistol at rest by the thigh, its arm turned out at most this far to keep it out of the thigh (spreadPistol). */
const PISTOL_SPREAD_MAX = Math.PI / 9;
/** How fast the body turns to bring an aimed barrel round to where it faces (aimTurn), a second. */
const AIM_TURN_EASE = 10;
const CLEAR_EASE = 12;
/** How fast any of those moves may go at most (radians, or metres for a push, a second): never a jump from one frame to the next. */
const CLEAR_TURN = 1.5;
const CLEAR_SLIDE = 0.25;
/** After a search that found nothing clear, how many poses go by before the next (the last goal kept meanwhile). */
const SEARCH_REST = 10;
/**
 * A rifle at low ready, from the right shoulder (model units, for an arm REF_ARM long; nearer for shorter arms): out
 * across (a share of the shoulder's side), down, ahead, and at least `clear` ahead of the torso's front (a belly,
 * a vest's pouches: the support arm comes round it); and how far it is tipped down and turned across the body.
 * A torso that stands out more (its front `wide`, from the one to the other) holds it higher and flatter
 * (downWide, tipWide): the support arm over the belly, not into it (Sidekick's heavy builds, playtest 21/10).
 */
export const LOW_READY = { across: 0.15, down: 0.22, ahead: 0.22, tip: 0.42, turn: 0.9, clear: 0.06, wide: [0.1, 0.15], downWide: 0.1, tipWide: 0.25 };
/** POLYGON's soldiers' arm, shoulder to wrist (the hold above was made for it). */
const REF_ARM = 0.407;
/** Crouched at low ready, the weapon held this much higher and further ahead: over the knees, not into the thigh come up under it. */
const CROUCH_LIFT = 0.11;
const CROUCH_AHEAD = 0.05;
/**
 * A blade, held by the hip, comes up and ahead further, over the knee, and levels at the front as on guard (the thighs
 * come up and out round a hand by the hip: the blade went into the thigh, the hand into the holstered pistol).
 */
const BLADE_CROUCH_LIFT = 0.25;
const BLADE_CROUCH_AHEAD = 0.22;
const BLADE_CROUCH_LEVEL = 1.85;
/**
 * A support arm that can't be got out of the body on the weapon however its elbow swings (a thick vest's armhole at
 * low ready, playtest 20/10 #1): it lets go and hangs at its side, the weapon in the right hand; back on it once it
 * would be well clear, and always to aim. How clear, and how fast it goes either way (a second).
 */
const LET_GO_SPARE = 0.02;
/** How deep the arm must still be for that (its points are rounder than its mesh: a touch is not reason to let go). */
const LET_GO_DEEP = 0.045;
const LET_GO_EASE = 3;
/** Aimed (it can't let go then), the support hand slides back along the handguard instead, this far at most (m). */
const SLIDE_BACK = 0.15;
/**
 * An arm near full stretch (two-bone IK): the last of its reach is eased, so the elbow bends in smoothly as the hand
 * comes back in instead of snapping (playtest 20/10 #2: a pistol lowered); the hand may fall this short at most.
 */
const SOFT_REACH = 0.025;
/**
 * How far the line from the shoulder to the hand may come to its pole (the sine between them) before the elbow's
 * bend leans on the bend it had (a hand right by its shoulder, the pole along the arm: the bend would flip, a jump).
 */
const POLE_SURE = 0.35;
const ARM_UPPER = [0.45, 0.7, 1];
/** What a swing of the elbow costs against how deep it leaves the arm (m a radian), when no swing clears it. */
const SWING_COST = 0.03;

const ARM_FORE = [0.33, 0.66, 1];
/**
 * Where along an arm the gun in the hand is kept out of (armIn, inGun): the upper arm from near the shoulder (a stock
 * set to it went in); on the hand that holds the gun, the forearm's half by the elbow (the stock through it): by the
 * wrist it is beside the gun however the elbow swings, and the search found nothing clear there.
 */
const ARM_UPPER_GUN = [0.2, 0.45, 0.7, 1];
const GUN_ROOM = 0.012;
/** The forearm by the wrist (fitGun), and the grip's foot it keeps out of (in the gun's frame: under the bore, by the grip). */
const ARM_CUFF = [0.6, 0.75, 0.88];
const GRIP_FOOT = { z0: -0.07, z1: 0.03, top: -0.03 };
const ARM_FORE_GUN = [0.05, 0.2, 0.35];
/** The left forearm's, under the handguard to its middle (past the magazine). */
const ARM_FORE_GUN_L = [0.05, 0.2, 0.35, 0.5];
/**
 * The weapon in the hand out of the body: how far it tips up about the grip, is pushed ahead and is lifted at most
 * (crouched, the thigh comes up under the hand: ahead is along it), the ways it tries (cheapest first: a tip dearest,
 * the muzzle swung up across the body looks wrong), and how many points along it are looked at.
 */
const GUN_TIPS = [0, 1, 2, 3, 4, 5, 6].map((k) => (k * Math.PI) / 24);
const GUN_PUSHES = [0, 0.03, 0.06, 0.09, 0.12];
const GUN_LIFTS = [0, 0.04, 0.08, 0.12];
const gunCost = (w: { tip: number; push: number; lift: number }) => (w.tip / (Math.PI / 4)) * 2 + w.push / 0.12 + w.lift / 0.12;
const GUN_WAYS = GUN_TIPS.flatMap((tip) => GUN_PUSHES.flatMap((push) => GUN_LIFTS.map((lift) => ({ tip, push, lift })))).sort((a, b) => gunCost(a) - gunCost(b));
const GUN_POINTS = 6;
/**
 * A clip's gun turned about the wrist out of the right arm (fitGun): how far it tips (about its side) and turns (about
 * its up) at most, by what steps, the ways it tries (the least turned first), and what a radian of it costs against
 * how deep it leaves the arm when none clears it.
 */
const FIT_TIP = 0;
const FIT_YAW = 0.5;
const FIT_STEP = 0.05;
const fitCost = (w: { tip: number; yaw: number }) => Math.abs(w.tip) * 2 + Math.abs(w.yaw);
const FIT_COST = 0.02;
/** Where a thing held at the camp sits in the right fist (the gun socket's frame: y along the grip, z ahead), metres. */
const ITEM_AT = new THREE.Vector3(0, 0.02, 0);
// Turned most, tipped a little (tipped further, the stock went up into the upper arm, and the measure was worse).
const FIT_WAYS = Array.from({ length: 9 }, (_, i) => (i - 4) * FIT_STEP)
  .flatMap((tip) => Array.from({ length: 21 }, (_, j) => ({ tip, yaw: (j - 10) * FIT_STEP })))
  .sort((a, b) => fitCost(a) - fitCost(b));
/** How far ahead an aimed gun slides along itself at most out of the upper arm (slideGun), by what steps. */
const SLIDE_MAX = 0.12;
const SLIDE_STEP = 0.02;
const SLIDE_UPPER = [0.1, 0.2, 0.35, 0.5, 0.7, 1];
const SLIDE_ROOM = 0.012;
/** How far a clip's right elbow swings at most out of the gun and the body (radians: further, it stood out like a wing). */
const CLIP_SWING = 0.7;
/** Which way a weapon at rest on the body is moved out of it: the slung gun back, the holster and the sheath out to the side. */
/**
 * The blade's sheath on the belt (hangStows): round from the left side towards the back (22/10: at the side, the left
 * arm hanging by it went through it), how much room it keeps from the body, and which way it points (down and back).
 */
const BELT_OUT = new THREE.Vector3(0.62, 0, -0.78).normalize();
const BELT_ROOM = 0.025;
const BELT_ALONG = new THREE.Vector3(0.1, -1, -0.45);
/** How far below the hip the pistol's holster rides on the right thigh. */
const HOLSTER_DROP = 0.15;
/**
 * The gun slung on the back crouched (slingOnBack): where it rests on the back, how far out to either side of the
 * spine, up from the chest's bone and down from the small of the back's (the right shoulder blade, the left of the
 * small of the back), and the room it keeps from the body (metres).
 */
const SLING_SIDE = 0.06;
const SLING_UP = 0.06;
const SLING_DOWN = 0.02;
const SLING_ROOM = 0.004;
/** How much of the back's bow under the gun its middle goes into (the rest its ends stand off). */
const SLING_SINK = 0.65;
/** How near the line through the back its skin is looked for (metres either way, across and up it). */
const SLING_NEAR = 0.035;
const STOW_AWAY: Record<Carry, THREE.Vector3> = { weapon: new THREE.Vector3(0, 0, -1), sidearm: new THREE.Vector3(-1, 0, 0), belt: BELT_OUT };
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

/**
 * A node whose world matrix only passes a change on to its children when it moved: the figure's model, under the
 * yaw and the pivot that are set at every pose. Its bones then make their matrices again only when written.
 */
function stillUnlessMoved(node: THREE.Object3D): void {
  const last = new THREE.Matrix4();
  node.updateMatrixWorld = function (force?: boolean): void {
    let moved = false;
    if (this.matrixWorldNeedsUpdate || force) {
      if (this.parent) this.matrixWorld.multiplyMatrices(this.parent.matrixWorld, this.matrix);
      else this.matrixWorld.copy(this.matrix);
      this.matrixWorldNeedsUpdate = false;
      moved = !this.matrixWorld.equals(last);
      if (moved) last.copy(this.matrixWorld);
    }
    for (const child of this.children) child.updateMatrixWorld(moved);
  };
}

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
  // The fingers, as the T-pose has them (palm down): each joint curls about the line across it and the palm's normal.
  const fingers: FingerChain[] = [];
  for (const side of ['L', 'R'] as const) {
    for (const [kind, count] of [['thumb', 3], ['index', 4], ['finger', 4]] as const) {
      const name = kind === 'thumb' ? 'Thumb' : kind === 'index' ? 'Index' : 'Finger';
      // As many joints as it has from the knuckle out (Sidekick's fingers have three, no tip: SIDEKICK).
      const joints: number[] = [];
      for (let j = 0; j < count && bones[B[`${name}_0${j + 1}_${side}` as SyntyBone]]; j++) joints.push(B[`${name}_0${j + 1}_${side}` as SyntyBone]);
      if (joints.length < 2) continue;
      // The hand's length (from the wrist to the knuckles), for the thumb: it closes across the palm towards the
      // fingers' tips (22/10 #3: about the palm's line alone a Sidekick thumb, hanging down from the hand in the T-pose,
      // only turned on itself and stood out straight).
      const knuckle = bones[B[`Index_01_${side}` as SyntyBone]] ? p[B[`Index_01_${side}` as SyntyBone]]! : null;
      const lengthways = knuckle ? knuckle.clone().sub(p[side === 'L' ? B.Hand_L : B.Hand_R]!).normalize() : null;
      const axes = joints.map((j, k) => {
        const next = joints[k + 1];
        const from = next !== undefined ? p[j]! : p[joints[k - 1]!]!;
        const to = next !== undefined ? p[next]! : p[j]!;
        const along = to.clone().sub(from).normalize();
        if (kind === 'thumb' && lengthways) {
          const towards = PALM.clone().add(lengthways).normalize();
          const axis = along.clone().cross(towards).normalize();
          return axis.applyQuaternion(q[j]!.clone().invert());
        }
        return along.cross(PALM).normalize().applyQuaternion(q[j]!.clone().invert());
      });
      fingers.push({ side, kind, joints, axes });
    }
  }
  const byName = (name: string) => (B as Record<string, number>)[name];
  model.userData.eased = easeShoulders(model, p, BULK_BONES, byName);
  rest = {
    bulk: bulkOf(model, p, BULK_BONES, byName),
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
    fingers,
    reach: (p[B.Shoulder_L]!.distanceTo(p[B.Elbow_L]!) + p[B.Elbow_L]!.distanceTo(p[B.Hand_L]!)) / REF_ARM,
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
/** For the fingers (hands): a joint's turn in the model, its rest turn from the one before, its curl, an inverse. */
const fq = new THREE.Quaternion();
/** For what is kept out of the body (inBody and its users). */
const bq = new THREE.Vector3();
const bp = new THREE.Vector3();
const sa = new THREE.Vector3();
const sf = new THREE.Vector3();
const se = new THREE.Vector3();
const sh2 = new THREE.Vector3();
const gq = new THREE.Quaternion();
const gi = new THREE.Quaternion();
const gl = new THREE.Vector3();
const ce = new THREE.Vector3();
const fq0 = new THREE.Quaternion();
const fq1 = new THREE.Quaternion();
const fq2 = new THREE.Quaternion();
const fp = new THREE.Vector3();
const fh = new THREE.Vector3();
const fp2 = new THREE.Vector3();
const hq1 = new THREE.Quaternion();
const hq2 = new THREE.Quaternion();
const fq3 = new THREE.Vector3();

const gx = new THREE.Vector3();
const gp = new THREE.Vector3();
const sq = new THREE.Quaternion();
const lq = new THREE.Quaternion();
const cq = new THREE.Quaternion();
const iq = new THREE.Quaternion();
const e1 = new THREE.Euler();
const m1 = new THREE.Matrix4();
/**
 * The pistol at rest (direct; Arturo, 22/10 #3: not both arms out at the ready while nobody aims): the end of its clip
 * (pistolRest: aimed, then lowered to the holster's idle), the pistol in the right hand by the thigh, the left arm
 * free; eased forth and back over that stretch (seconds into it), a breath. (From 5.8 s: before it the hand is still
 * coming down, 7 cm in 0.3 s, and going back over it the arm hopped every 3.4 s: 22/10 #8, measured on our bodies.)
 */
const PISTOL_REST = { from: 5.8, to: 7.0 };
/**
 * The throws' clips (direct), timed to the game's: the clip's moment the hand lets go (`release`, measured on it) at
 * the game's (`lets`, seconds into the throw: unitViews throwMotion, tossStone), `rate` times as fast; over by `end`.
 * `after`: from that moment on, the clip from another of its moments at another rate (a stab held out, then drawn
 * back: the drawing back).
 */
const THROWS = {
  grenade: { clip: 'tossGrenade', release: 1.8, lets: 0.49, rate: 1.4, end: 0.8 },
  stone: { clip: 'throwStone', release: 2.4, lets: 0.24, rate: 1.4, end: 0.5 },
  // A blade's stroke (unitViews lunge: wound up 0.2 s, the stroke 0.14 s, back 0.35 s): the knife's stab, the machete's swing.
  knife: { clip: 'knifeStab', release: 0.42, lets: 0.34, rate: 1.3, end: 0.75, after: { from: 1.0, rate: 0.75 } },
  machete: { clip: 'macheteSwing', release: 1.0, lets: 0.34, rate: 2.0, end: 0.75, after: { from: 1.0, rate: 1.7 } },
} as const satisfies Record<string, { clip: ActionId; release: number; lets: number; rate: number; end: number; after?: { from: number; rate: number } }>;

/** Where a throw's or a stroke's clip is `t` seconds into it (THROWS). */
function throwClipTime(k: keyof typeof THROWS, t: number): number {
  const w: (typeof THROWS)[keyof typeof THROWS] & { after?: { from: number; rate: number } } = THROWS[k];
  if (t >= w.lets && w.after) return w.after.from + (t - w.lets) * w.after.rate;
  return w.release + (t - w.lets) * w.rate;
}

/** For the gun as a clip's hands hold it (clipHold). */
const hq = new THREE.Quaternion();
const hr = new THREE.Quaternion();
const m2 = new THREE.Matrix4();
const sl2 = new THREE.Vector3();
const sl3 = new THREE.Vector3();
const sl4 = new THREE.Vector3();
const sl5 = new THREE.Vector3();
const sl6 = new THREE.Vector3();
const sl7 = new THREE.Vector3();
const sl8 = new THREE.Vector3();
const sm1 = new THREE.Matrix4();
const sm2 = new THREE.Matrix4();
const X = new THREE.Vector3(1, 0, 0);
const Y = new THREE.Vector3(0, 1, 0);
const Z = new THREE.Vector3(0, 0, 1);

/** A rotation about one axis. */
const rot = (out: THREE.Quaternion, axis: THREE.Vector3, angle: number) => out.setFromAxisAngle(axis, angle);
const lerp = THREE.MathUtils.lerp;

// ----------------------------------------------------------------- figures

type Carry = Carried;
const CARRIES: Carry[] = ['weapon', 'sidearm', 'belt'];

/** The models of an armament, by where they are carried. */
type Guns = Partial<Record<Carry, { id: WeaponId; model: THREE.Group }>>;
/** A weapon along its own length (its frame): from the butt (z0) to the muzzle (z1), the line down its middle (x, y), how thick it is round it, and how far its stock and magazine go below that and its sights above (lo, hi). */
interface Span {
  z0: number;
  z1: number;
  x: number;
  y: number;
  r: number;
  lo: number;
  hi: number;
}

/**
 * A gun's shape along it, for the arms to keep out of (inGun; Arturo, 22/10: the rifle at rest went through the
 * forearms): slice by slice along its barrel (z, its own frame), its middle across (x), how high and low it goes and
 * half its width.
 */
interface Profile {
  z0: number;
  step: number;
  slices: { x: number; lo: number; hi: number; r: number }[];
}
/** How many slices a gun's profile has. */
const PROFILE_SLICES = 18;

function profileOf(m: THREE.Object3D, box: THREE.Box3): Profile {
  const step = (box.max.z - box.min.z) / PROFILE_SLICES || 1;
  const acc = Array.from({ length: PROFILE_SLICES }, () => ({ x0: Infinity, x1: -Infinity, lo: Infinity, hi: -Infinity }));
  const v = new THREE.Vector3();
  m.traverse((o) => {
    if (!(o instanceof THREE.Mesh)) return;
    const g = o.geometry.getAttribute('position') as THREE.BufferAttribute;
    for (let i = 0; i < g.count; i++) {
      v.fromBufferAttribute(g, i).applyMatrix4(o.matrixWorld);
      const k = THREE.MathUtils.clamp(Math.floor((v.z - box.min.z) / step), 0, PROFILE_SLICES - 1);
      const a = acc[k]!;
      a.x0 = Math.min(a.x0, v.x);
      a.x1 = Math.max(a.x1, v.x);
      a.lo = Math.min(a.lo, v.y);
      a.hi = Math.max(a.hi, v.y);
    }
  });
  return {
    z0: box.min.z,
    step,
    slices: acc.map((a) => (a.x0 > a.x1 ? { x: 0, lo: 0, hi: 0, r: 0 } : { x: (a.x0 + a.x1) / 2, lo: a.lo, hi: a.hi, r: (a.x1 - a.x0) / 2 })),
  };
}

/**
 * What shows each improvement on a gun (docs/FASE9.md §5): a sight on the
 * receiver (a red dot on the short guns and the machine guns, a telescopic
 * one on the rest) and the home-made silencer at the muzzle. The stock and
 * the larger magazine have nothing of their own to show.
 */
const SCOPE: WeaponPartId = 'scope7';
const REFLEX: WeaponPartId = 'scope2';
const SILENCER: WeaponPartId = 'silencer2';
/** How far along the receiver a sight sits (the gun's frame: the grip at 0, the barrel along +z). */
const SIGHT_Z = 0.07;

/** The parts the main gun's improvements need. */
function partsOf(a: Armament): WeaponPartId[] {
  const mods = a.weapon ? (a.mods ?? []) : [];
  const out: WeaponPartId[] = [];
  if (mods.includes('scope')) out.push((WEAPONS[a.weapon!] as WeaponAsset).reflex ? REFLEX : SCOPE);
  if (mods.includes('silencer')) out.push(SILENCER);
  return out;
}

/** An armament's models if all have loaded, the parts on its main gun too (asking starts the loads), else null. */
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
  for (const id of partsOf(a)) if (!modelNow(WEAPON_PARTS[id].pack, 'wep', id)) all = false;
  return all ? out : null;
}

/**
 * The back's skin at rest where a gun slung on it lies (slingOnBack), from the body's mesh as bound: the hindmost of it
 * near the line straight back from a point by the right shoulder blade, the middle of the back and the left of the
 * small of the back (SLING_SIDE, SLING_UP, SLING_DOWN), each from its bone (Spine_03, Spine_02, Spine_01). Null with no
 * body mesh.
 */
function backSkin(model: THREE.Object3D, toModel: THREE.Matrix4, rest: Rest): { top: THREE.Vector3; mid: THREE.Vector3; foot: THREE.Vector3 } | null {
  const want = [
    { bone: B.Spine_03, x: -SLING_SIDE, y: SLING_UP },
    { bone: B.Spine_02, x: 0, y: 0 },
    { bone: B.Spine_01, x: SLING_SIDE, y: -SLING_DOWN },
  ].map((w) => ({ ...w, at: rest.p[w.bone]!.clone().add(new THREE.Vector3(w.x, w.y, 0)), z: Infinity }));
  const v = new THREE.Vector3();
  let found = false;
  model.traverse((o) => {
    if (!(o instanceof THREE.SkinnedMesh) || o.name !== 'body') return;
    found = true;
    const m = new THREE.Matrix4().multiplyMatrices(toModel, o.matrixWorld);
    const pos = o.geometry.getAttribute('position');
    for (let i = 0; i < pos.count; i++) {
      o.getVertexPosition(i, v).applyMatrix4(m);
      for (const w of want) if (Math.abs(v.x - w.at.x) < SLING_NEAR && Math.abs(v.y - w.at.y) < SLING_NEAR && v.z < w.z) w.z = v.z;
    }
  });
  if (!found || want.some((w) => w.z === Infinity)) return null;
  const off = (w: (typeof want)[number]) => new THREE.Vector3(w.at.x, w.at.y, w.z).sub(rest.p[w.bone]!);
  return { top: off(want[0]!), mid: off(want[1]!), foot: off(want[2]!) };
}

async function loadGuns(a: Armament): Promise<Guns> {
  const out: Guns = {};
  await Promise.all([
    ...CARRIES.map(async (k) => {
      const id = a[k];
      if (id) out[k] = { id, model: await loadModel(WEAPONS[id].pack, 'wep', id) };
    }),
    ...partsOf(a).map((id) => loadModel(WEAPON_PARTS[id].pack, 'wep', id)),
  ]);
  return out;
}

/** The same models; and the same improvements and wear on the main gun. */
const sameArms = (a: Armament, b: Armament) => CARRIES.every((k) => (a[k] ?? null) === (b[k] ?? null));
const wearOf = (c: number | undefined) => ((c ?? 100) <= 0 ? 2 : (c ?? 100) < WORN_BELOW ? 1 : 0);
const sameFit = (a: Armament, b: Armament) => (a.mods ?? []).join() === (b.mods ?? []).join() && wearOf(a.condition) === wearOf(b.condition);

/** A gun worn (below WORN_BELOW) or broken (at 0): its atlas darker and duller, a material of its own per atlas and state. */
const wornLooks = new Map<string, THREE.MeshStandardMaterial>();
function wornMaterial(atlas: AtlasId, broken: boolean): THREE.MeshStandardMaterial {
  const key = `${atlas}|${broken}`;
  let m = wornLooks.get(key);
  if (!m) {
    m = atlasMaterial(atlas).clone();
    m.color.setHex(broken ? 0x5f5850 : 0x80776c);
    m.roughness = 0.95;
    wornLooks.set(key, m);
  }
  return m;
}

/** Duct tape round a broken gun. */
const TAPE = new THREE.MeshStandardMaterial({ color: 0x9a9890, roughness: 0.7, metalness: 0.1 });

/** A gun's cross-section about z, in its own frame (for the tape round it): by weapon and place, once. */
const sections = new Map<string, THREE.Box3>();
function sectionOf(id: WeaponId, gun: THREE.Object3D, z: number): THREE.Box3 {
  const key = `${id}|${z.toFixed(3)}`;
  let box = sections.get(key);
  if (box) return box;
  box = new THREE.Box3();
  gun.updateMatrixWorld(true);
  const toGun = new THREE.Matrix4().copy(gun.matrixWorld).invert();
  const v = new THREE.Vector3();
  gun.traverse((o) => {
    if (!(o instanceof THREE.Mesh)) return;
    const at = new THREE.Matrix4().multiplyMatrices(toGun, o.matrixWorld);
    const p = o.geometry.getAttribute('position') as THREE.BufferAttribute;
    for (let i = 0; i < p.count; i++) {
      v.fromBufferAttribute(p, i).applyMatrix4(at);
      if (Math.abs(v.z - z) < 0.012) box!.expandByPoint(v);
    }
  });
  sections.set(key, box);
  return box;
}
const armsOf = (guns: Guns): Armament => ({ weapon: guns.weapon?.id ?? null, sidearm: guns.sidearm?.id ?? null, belt: guns.belt?.id ?? null });

/** The pistol's grip for the support hand (as WEAPONS fore: height, distance along the barrel): the left hand cups the right. */
const PISTOL_FORE: [number, number] = [-0.07, 0.01];

/** What a Synty unit is made of. */
interface Spec extends Armament {
  /** Who it is: a character of the manifest, or a soldier put together from the wardrobe (`dress`: wardrobe.ts). */
  character?: CharacterId;
  dress?: Dress;
  /** Barracks colours: helmet, torso, arms, legs; null for a piece left as Synty painted it. */
  dyes: (string | null)[] | null;
  /** The owner's band colour; null for none. */
  band: string | null;
  dead: boolean;
  /** The Chillón: aiming is its scream. */
  scream: boolean;
  /** What the body adds to its gait (gaits.ts). */
  style?: GaitStyle;
  /** How it stands waiting (STANCES); unset, the plain idle. */
  stance?: Stance;
  /** How it waits with a rifle (RIFLE_RESTS); unset, the first. */
  rest?: ActionId;
  /** A skin tone instead of Synty's one; unset keeps it. */
  skin?: string;
  /** Always drawn by its own figure, never by the crowd (whose bodies have no clothes' colours nor hats): a risen soldier. */
  own?: boolean;
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

/**
 * Whether soldiers wear their players' colours (Ajustes, «Ropa con el color del jugador»: Arturo, 21/10): off, each
 * in their clothes' own colours and without the band, the circle under them telling whose they are. The soldiers on
 * screen are painted again when it changes.
 */
let squadTint = graphics().squadTint;
const tinted = new Set<WeakRef<SyntyFigure>>();
onGraphics((g) => {
  if (g.squadTint === squadTint) return;
  squadTint = g.squadTint;
  for (const ref of tinted) {
    const f = ref.deref();
    if (f) f.repaint();
    else tinted.delete(ref);
  }
});

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
  /** All its weapons on it, none in the hand (sling). */
  private slung = false;
  /** At the camp (camp): what it stays in there, whether it is there, and whether that is still to reach its driver (`campNow`: without easing). */
  private campLoop: CampLoop | null = null;
  private campOn = false;
  private campDirty = false;
  private campNow = false;
  private campMoving = false;
  /** What it holds in the right hand at the camp (holdItem), its model there once loaded. */
  private itemId: PropId | null = null;
  private item: THREE.Object3D | null = null;
  /** Kept upright (a cup), else as the fist turns it (a ration bar). */
  private itemUpright = false;
  /** How far each finger joint is curled now (radians), easing to its hand's pose (hands). */
  private readonly curl = new Float32Array(N);
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
  /**
   * A weapon's action from Mixamo (ACTIONS) over the clips, `time` seconds in, taking `share` of the body, whole or
   * (`upper`) from the spine up; `raw`, with the procedural layers off (the weapons lab's look at a clip alone).
   */
  action: { id: ActionId; time: number; share: number; upper: boolean | 'free'; raw?: boolean } | null = null;
  /** The weapon's actions playing this pose (direct), how much of the arms are theirs (over 0.5, all), and their clock. */
  private readonly plays: ActionPlay[] = [];
  private clipped = 0;
  /** How much of the pistol's clips is its rest (direct): the left hand is free then (hands). */
  private pistolRest = 0;
  /**
   * The weapon's clips' clock (direct), and how fast it runs for this one: from a time and at a rate of its own, so a
   * row of them made at once doesn't all do the same thing together (22/10 #7: the squad's row at La Lata).
   */
  private actClock = Math.random() * 600;
  /** How far the body is turned about its feet for the barrel to point where it faces (aimTurn). */
  private aimYaw = 0;
  /** In a camp move whose hips go as high as its clip has them (CAMP_FREE), or easing out of one. */
  private freeMove = false;
  private readonly actRate = 0.9 + Math.random() * 0.2;
  /** How it waits with a rifle if told (restAs), else its own (Spec.rest). */
  private restId: ActionId | null = null;
  /** A throw playing its clip (direct): a grenade's, a stone's; seconds since it began; and whether its clip has the arms. */
  private throwKind: keyof typeof THROWS | null = null;
  private throwT = 0;
  private thrown = false;
  /**
   * The gun's turn in the right hand as the clips hold it (clipHold), in the hand's terms: learnt while both hands
   * are on it (the barrel along them), kept as it is otherwise, as a gun held fast in the hand (a reload, a throw).
   */
  private readonly clipGrip = new THREE.Quaternion();
  private clipGripKnown = false;
  /** A blade's stroke playing its clip (strike). */
  private struck = false;
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
  /** The death it plays (DEATHS[build]), the seconds it takes and how far it is drawn in (model space: a cramped death); null standing. */
  private death: { k: number; seconds: number; tuck: { x: number; z: number } } | null = null;
  /** A death asked for before the clips loaded: taken once they have, the body lying. */
  private pending: Blow | null = null;
  /** The hit reaction it plays (HITS[build]), the seconds it takes and how much of the upper body it takes; null none. */
  private hit: { k: number; seconds: number; share: number; on: boolean } | null = null;
  /** The weapon let go: where it left the hand (in the yaw's frame), how it lies on the ground, and when. */
  private drop: { p: THREE.Vector3; q: THREE.Quaternion; flat: THREE.Quaternion; at: number } | null = null;
  /** Seconds since the last pose: what the moves out of the body ease by. */
  private dt = 0;
  /**
   * How much of it is drawn (1 all of it), and how much is to be: coming into sight (fadeIn) or out of the camera's
   * way (veil); how fast it goes (a second), and when it last went on.
   */
  private fade = 1;
  private fadeGoal = 1;
  private fadeRate = 0;
  private fadeClock = -Infinity;
  /** The bulk's bones turned back to rest as posed now (inBody), each part's middle where it is now, and for which pose. */
  private readonly back = Array.from({ length: N }, () => new THREE.Quaternion());
  private readonly middles: THREE.Vector3[] = [];
  private backed = -1;
  /** What each search last settled on (left elbow, right elbow, left arm, right arm, the weapon), and the poses to wait before the next. */
  private readonly goals = [0, 0, 0, 0];
  private readonly gunGoal = { tip: 0, push: 0, lift: 0 };
  private readonly waits = [0, 0, 0, 0, 0, 0, 0];
  /** How far a clip's gun is turned about the wrist out of the right arm now (fitGun), and the search's last goal. */
  private readonly gunFit = { tip: 0, yaw: 0 };
  /** How far ahead along itself a clip's gun is slid now, aimed (slideGun). */
  private slideOut = 0;
  private readonly fitGoal = { tip: 0, yaw: 0 };
  /** The axis each elbow last bent about (reach): what it keeps to when its pole is no help. */
  private readonly bends = [new THREE.Vector3(), new THREE.Vector3()];
  /** Each elbow's swing round its shoulder–hand line out of the body (swingElbow), each arm's turn out to the side (spreadArm): left, right. */
  private readonly swing = [0, 0];
  private readonly spread = [0, 0];
  /** The right arm's turn out to the side with the pistol at rest by the thigh, out of it (spreadPistol), and its search's last goal. */
  private pistolOut = 0;
  private pistolGoal = 0;
  /** How far the weapon in the hand is tipped up about its grip and pushed ahead out of the body now (clearGun). */
  private readonly gunFix = { tip: 0, push: 0, lift: 0 };
  /** How far the support hand has let go of the weapon to hang at the side (letGo): 0 on it, 1 hanging; and, aimed, slid back along it (0 to 1 of SLIDE_BACK). */
  private letGo = 0;
  private slid = 0;
  /** Where the drawn weapon goes in its own frame: from its butt to its muzzle, the line down its middle and its girth (hold). */
  private gunSpan: Span | null = null;
  /** The gun in the hand's shape along it (inGun), a firearm's only. */
  private gunProfile: Profile | null = null;
  /** Where each weapon at rest on the body rides, in the model at rest (hangStows): what it is moved out of the body from. */
  private readonly stowRest: Partial<Record<Carry, THREE.Matrix4>> = {};
  /** And how each sits on its bone there, and the bone's own measure in the model (slingOnBack moves the slung gun from it). */
  /** Where the back's skin is at rest, for a gun slung on it to lie on (backSkin): from the chest's, the middle's and the small of the back's bones. */
  private slingBack: { top: THREE.Vector3; mid: THREE.Vector3; foot: THREE.Vector3 } | null = null;
  private readonly stowOn: Partial<Record<Carry, { at: THREE.Vector3; turn: THREE.Quaternion; scale: THREE.Vector3; bone: THREE.Vector3 }>> = {};

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
    const c = spec.dress ? null : (CHARACTERS[spec.character!] as CharacterAsset);
    this.scale = CHARACTER_SCALE * (c?.scale ?? 1);
    // Each unit plays the clips from its own point, so a squad does not breathe in step.
    const build: Build = (spec.dress ? spec.dress.sex === 'f' : c?.female) ? 'femn' : 'masc';
    this.build = build;
    this.stance = spec.stance ? STANCE_ORDER.indexOf(spec.stance) : -1;
    const set = animSet(build, spec.dead);
    if (set) this.anim = this.driver(set);
    else void whenAnimSet(build, spec.dead).then((s) => (this.anim = this.driver(s)), () => undefined);
    const guns = gunsNow(spec);
    if (spec.dress) {
      const dress = spec.dress;
      const now = wardrobeNow(dress);
      if (now && guns) this.fill(now, guns);
      else {
        void Promise.all([loadWardrobe(dress), loadGuns(spec)])
          .then(([m, g]) => this.fill(m, g))
          .catch((e: unknown) => console.warn(`Synty: a ${dress.outfit} could not be dressed`, e));
      }
      return;
    }
    const now = modelNow(c!.pack, 'chr', spec.character!);
    if (now && guns) this.fill(now, guns);
    else {
      void Promise.all([loadModel(c!.pack, 'chr', spec.character!), loadGuns(spec)])
        .then(([m, g]) => this.fill(m, g))
        .catch((e: unknown) => console.warn(`Synty: ${spec.character} could not be dressed`, e));
    }
  }

  /** Its clips' driver, standing as it should. */
  private driver(set: AnimSet): AnimDriver {
    const a = new AnimDriver(set, Math.random());
    a.setStance(this.stance, true);
    this.campDirty = true;
    return a;
  }

  /** In a camp clip now (sat, knelt, leant, drinking), or playing one into it or out of it. */
  private get atCamp(): boolean {
    return this.campOn && (this.campLoop !== null || this.campMoving);
  }

  camp(loop: CampLoop | null, on = true, now = false): void {
    if (loop === this.campLoop && on === this.campOn) return;
    this.campLoop = on ? loop : null;
    this.campOn = on;
    this.campDirty = true;
    this.campNow = now;
  }

  campMove(move: CampMove | null, speed = 1): number {
    this.campMoving = move !== null;
    // (Kept on once let go of, until the move has eased out: its hips back on the feet as it goes.)
    if (move) this.freeMove = CAMP_FREE.has(move);
    const a = this.anim;
    const clips = campClips(this.build);
    if (!a || !clips) return 1;
    return a.campMove(move ? (clips.get(CAMP_MOVES[this.build][move]) ?? null) : null, speed);
  }

  /**
   * What it holds at the camp kept upright if it is to be (holdItem), facing as it does (a cup is held level, whatever
   * way the fist is turned: along the grip, it lay along the forearm). By the hand's turn as last drawn: a frame
   * behind, unseen.
   */
  private uprightItem(): void {
    const item = this.item;
    if (!item?.parent || !this.itemUpright) return;
    item.parent.getWorldQuaternion(hq1);
    this.root.getWorldQuaternion(hq2);
    item.quaternion.copy(hq1.invert()).multiply(hq2);
  }

  restAs(k: number): void {
    this.restId = RIFLE_RESTS[((k % RIFLE_RESTS.length) + RIFLE_RESTS.length) % RIFLE_RESTS.length]!;
  }

  holdItem(id: PropId | null, upright = false): void {
    this.itemUpright = upright;
    if (id === this.itemId) return;
    this.itemId = id;
    this.item?.removeFromParent();
    this.item = null;
    if (!id) return;
    const a = PROPS[id] as PropAsset;
    const put = (model: THREE.Group) => {
      if (this.itemId !== id) return;
      // In the fist as a gun's grip is (its socket): upright along the grip, its middle where the palm closes.
      const m = model.clone();
      const box = new THREE.Box3().setFromObject(model);
      m.position.copy(box.getCenter(new THREE.Vector3())).negate().add(ITEM_AT);
      const g = new THREE.Group();
      g.add(m);
      this.socket.add(g);
      this.item = g;
    };
    const now = modelNow(a.pack, 'prop', id);
    if (now) put(now);
    else void loadModel(a.pack, 'prop', id).then(put, () => undefined);
  }

  /** The camp as asked (camp) onto the clips' driver, once they and the camp's clips are in. */
  private applyCamp(a: AnimDriver): void {
    const clips = this.campOn ? campClips(this.build) : null;
    if (this.campOn && !clips) return;
    a.campLoop(clips && this.campLoop ? (clips.get(CAMP_LOOPS[this.build][this.campLoop]) ?? null) : null, this.campNow);
    a.campVariations(clips ? CAMP_VARIATIONS[this.build].map((id) => clips.get(id)).filter((c) => !!c) : null);
    this.campDirty = this.campNow = false;
  }

  setStance(stance: Stance | null): void {
    this.stance = stance ? STANCE_ORDER.indexOf(stance) : -1;
    this.anim?.setStance(this.stance);
  }

  gesture(g: Gesture): boolean {
    return this.anim?.gesture(g) ?? false;
  }

  sling(on: boolean): void {
    if (on === this.slung) return;
    this.slung = on;
    if (this.rest) this.hold();
  }

  /** Dressed and moving with its clips (both load in the background). */
  get ready(): boolean {
    return !!this.rest && !!this.anim;
  }

  /** Dresses the rig once its models are loaded. */
  private fill(source: THREE.Group, guns: Guns): void {
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
    // A soldier from the wardrobe is painted from a palette of their own.
    const palette = source.userData.palette as THREE.Texture | undefined;
    model.traverse((o) => {
      if (!(o instanceof THREE.Mesh)) return;
      this.atlas ??= atlasOf(o);
      material ??= plain && !palette ? plainMaterial(atlasOf(o)) : dyedMaterial(atlasOf(o), palette);
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
    });
    this.material = material;
    if (material) this.paint(material);
    if (spec.dress) tinted.add(new WeakRef(this));
    this.bones = SYNTY_BONES.map((n) => model.getObjectByName(n) ?? null);
    // Each bone's matrix is made when write() poses it, not every frame, and the model's own nodes never move: a figure
    // posed less often (far off, at rest, out of sight: SYNTY_LOD) costs nothing in between, whatever its bones (the
    // fingers doubled them). What hangs from the bones later (a hat, the gun) goes on as ever.
    model.traverse((o) => {
      o.updateMatrix();
      o.matrixAutoUpdate = false;
    });
    stillUnlessMoved(model);
    onUnitLayer(model);
    this.body.add(model);
    this.model = model;
    this.body.scale.setScalar(k);
    this.body.position.y = -this.hip;
    this.pivot.position.y = this.hip;

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
  }

  /**
   * Where the weapons not in the hand ride, hung from their bones (placed in
   * model space at rest): the main gun slung across the back, the barrel up
   * over the right shoulder; the pistol in its holster down the right
   * thigh, the muzzle down; the blade in its sheath behind the left hip, on the
   * belt as near the body as it stays clear of it, pointing down and back.
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
      const onBone = new THREE.Matrix4().multiplyMatrices(toModel, b.matrixWorld);
      onBone.clone().invert().multiply(place).decompose(g.position, g.quaternion, g.scale);
      b.add(g);
      this.stows[k] = g;
      this.stowRest[k] = place;
      this.stowOn[k] = { at: g.position.clone(), turn: g.quaternion.clone(), scale: g.scale.clone(), bone: new THREE.Vector3().setFromMatrixScale(onBone) };
    };
    this.slingBack = backSkin(model, toModel, rest);
    const s3 = rest.p[B.Spine_03]!;
    const hipL = rest.p[B.UpperLeg_L]!;
    const hipR = rest.p[B.UpperLeg_R]!;
    hang('weapon', B.Spine_03, new THREE.Vector3(0, s3.y + 0.02, s3.z - 0.11), new THREE.Vector3(-0.45, 0.9, 0), new THREE.Vector3(0, 0, -1));
    // (Down the thigh, its grip under a hand at the hip, 22/10: a blade's hand by the hip went into it.)
    hang('sidearm', B.UpperLeg_R, new THREE.Vector3(hipR.x - 0.075, hipR.y - HOLSTER_DROP, hipR.z + 0.01), new THREE.Vector3(0, -1, 0.1), new THREE.Vector3(0, 0, 1));
    const hips = rest.p[B.Hips]!;
    const belt = new THREE.Vector3(hips.x, hipL.y + 0.03, hips.z);
    const parts = rest.bulk?.parts;
    if (parts) {
      const inBody = () => parts.reduce((worst, g) => Math.max(worst, depthIn(g, belt, BELT_ROOM)), -1);
      for (let k = 0; k < 60 && inBody() > 0; k++) belt.addScaledVector(BELT_OUT, 0.005);
    } else belt.addScaledVector(BELT_OUT, 0.13);
    const along = BELT_ALONG.clone().normalize();
    hang('belt', B.Hips, belt, along, new THREE.Vector3().crossVectors(along, BELT_OUT));
  }

  /** Its weapons on it (fill, arm): the drawn one in the hand, the rest where they ride; what it carried before goes. */
  private equip(guns: Guns): void {
    for (const k of CARRIES) {
      this.carried[k]?.removeFromParent();
      delete this.carried[k];
      const g = guns[k];
      if (!g) continue;
      const m = g.model.clone();
      const w = WEAPONS[g.id] as WeaponAsset;
      m.scale.setScalar(w.size ?? 1);
      // Slung, the main gun hangs by its middle.
      m.updateMatrixWorld(true);
      const box = new THREE.Box3().setFromObject(m);
      m.userData.middle = (box.min.z + box.max.z) / 2;
      const size = box.getSize(new THREE.Vector3());
      const centre = box.getCenter(new THREE.Vector3());
      const r = Math.min(size.x, size.y) / 2;
      m.userData.span = { z0: box.min.z, z1: box.max.z, x: centre.x, y: centre.y, r, lo: Math.min(0, box.min.y + r - centre.y), hi: Math.max(0, box.max.y - r - centre.y) } satisfies Span;
      m.userData.profile = profileOf(m, box);
      const muzzle = new THREE.Vector3(...(w.muzzle ?? (m.children[0]?.userData.muzzle as [number, number, number] | undefined) ?? [0, 0, 0.5]));
      if (k === 'weapon') this.mount(m, g.id, muzzle);
      m.userData.muzzle = muzzle.multiply(m.scale);
      onUnitLayer(m);
      this.carried[k] = m;
    }
    if (!this.carried[this.drawn]) this.drawn = 'weapon';
    this.hold();
  }

  /**
   * On the main gun, what is mounted on it and how worn it is (spec.mods,
   * condition): the sight on the receiver, the silencer at the muzzle (moved
   * to its end: the shots come out there); a worn gun darker, a broken one
   * darker still with tape round it either side of the support hand.
   */
  private mount(gun: THREE.Group, id: WeaponId, muzzle: THREE.Vector3): void {
    const w = WEAPONS[id] as WeaponAsset;
    for (const part of partsOf(this.spec)) {
      const model = modelNow(WEAPON_PARTS[part].pack, 'wep', part);
      if (!model) continue;
      const p = model.clone();
      if (part === SILENCER) {
        p.position.copy(muzzle);
        muzzle.z += new THREE.Box3().setFromObject(model).max.z;
      } else p.position.set(0, w.sight ?? 0.06, SIGHT_Z);
      gun.add(p);
    }
    const condition = this.spec.condition ?? 100;
    if (condition >= WORN_BELOW) return;
    const broken = condition <= 0;
    gun.traverse((o) => {
      if (o instanceof THREE.Mesh) o.material = wornMaterial(atlasOf(o), broken);
    });
    if (!broken) return;
    const along = w.fore?.[1] ?? muzzle.z * 0.5;
    for (const z of [along - 0.075, along + 0.075]) {
      const s = sectionOf(id, gun, z);
      if (s.isEmpty()) continue;
      const size = s.getSize(new THREE.Vector3());
      const band = new THREE.Mesh(new THREE.BoxGeometry(size.x * 1.15 + 0.006, size.y * 1.15 + 0.006, 0.024), TAPE);
      band.position.set((s.min.x + s.max.x) / 2, (s.min.y + s.max.y) / 2, z);
      band.castShadow = true;
      gun.add(band);
    }
  }

  /**
   * Aimed, the body turned about its feet for the barrel to point where it faces, at what it shoots (22/10 #14).
   * Mixamo's aims turn the hips and the chest back over them; over the legs' clips, without the hips' turn, the barrel
   * was 41° off to the left standing, 26–29° knelt, 57–60° with the pistol. By the gun's turn as posed (the body's own
   * frame: its turn doesn't change it), eased.
   */
  private aimTurn(p: FigurePose, dt: number, live: number): void {
    const gun = SYNTY_HANDS.aim && this.armed && !this.melee && !this.thrown && !this.struck && !(p.swing ?? 0);
    const aim = gun ? THREE.MathUtils.smoothstep(p.aim, 0, 1) * live : 0;
    let goal = 0;
    if (aim > 0.001) {
      v1.copy(Z).applyQuaternion(this.gunQ);
      goal = -Math.atan2(v1.x, v1.z) * aim;
    }
    this.aimYaw += (goal - this.aimYaw) * Math.min(1, dt * AIM_TURN_EASE);
    this.body.rotation.y = this.aimYaw;
  }

  /**
   * The gun slung on the back as the back bends (22/10, Gameplay: crouched, held rigid by the chest's bone, its barrel
   * stood up over the head; turned across, it floated off the back): crouched (or crouch-walking), diagonal from the
   * right shoulder blade to the left hip as the back is now (the body's girths: bulk.ts), pressed to it, out of it
   * where it bows; standing, as it rides (hangStows); in between, eased by the crouch.
   */
  private slingOnBack(p: FigurePose): void {
    const g = this.stows.weapon;
    const on = this.stowOn.weapon;
    if (!g || !on) return;
    const m = g.children[0];
    const span = m?.userData.span as Span | undefined;
    const crouch = THREE.MathUtils.clamp(p.crouch, 0, 1);
    g.position.copy(on.at);
    g.quaternion.copy(on.turn);
    g.scale.copy(on.scale);
    if (crouch < 0.01 || !m || !span || !this.bodyAware || !this.rest?.bulk) return;
    const back = this.slingBack;
    if (!back) return;
    const { d, p: pos } = this;
    for (const i of SPINE) this.fk(i);
    const s3 = B.Spine_03;
    // Where it lies on the back as the back is now: the back's skin at the right shoulder blade, the middle and the left
    // of the small of the back as they are at rest (hangStows), carried by their bones; the gun's line from the low one
    // to the high one, out of the middle one where the back bows under it, the gun's half-thickness off the skin.
    const top = sl4.copy(back.top).applyQuaternion(d[s3]!).add(pos[s3]!);
    const foot = sl5.copy(back.foot).applyQuaternion(d[B.Spine_01]!).add(pos[B.Spine_01]!);
    const bow = sl3.copy(back.mid).applyQuaternion(d[B.Spine_02]!).add(pos[B.Spine_02]!);
    const along = sl6.copy(top).sub(foot).normalize();
    const out = sl7.copy(Z).applyQuaternion(d[B.Spine_02]!).negate();
    out.addScaledVector(along, -out.dot(along)).normalize();
    const mid = sl8.copy(top).add(foot).multiplyScalar(0.5);
    // (Straight on a back bowed under it, it would touch at the bow and stand off at both ends: the bow is shared out,
    // its middle a little into the back, under the gun itself where nothing shows it, its ends nearer the back.)
    const under = sl2.copy(bow).sub(mid);
    mid.addScaledVector(out, Math.max(0, under.dot(out)) * (1 - SLING_SINK) + span.r + SLING_ROOM);
    // Pressed so: along it, on its side (its flat against the back, its top up the slope and the magazine down it: on
    // its magazine it stood off the back); the group where the gun's middle (at its model's offset in it, but along)
    // comes to `mid`.
    const side = sl2.crossVectors(out, along).normalize();
    if (side.y < 0) side.negate();
    sq.setFromRotationMatrix(m1.makeBasis(sl4.crossVectors(side, along), side, along));
    const hug = sl3.set(m.position.x, m.position.y, 0).applyQuaternion(sq).negate().add(mid);
    // As it rides now on its bone, in the model; eased from that by the crouch; back onto the bone.
    const bone = sm1.compose(pos[s3]!, lq.copy(d[s3]!).multiply(this.rest!.q[s3]!), on.bone);
    sm2.compose(on.at, on.turn, on.scale).premultiply(bone).decompose(sl5, cq, sl4);
    sl5.lerp(hug, crouch);
    cq.slerp(sq, crouch);
    sm2.compose(sl5, cq, sl4.set(1, 1, 1)).premultiply(bone.invert()).decompose(g.position, g.quaternion, g.scale);
  }

  /** The drawn weapon into the hand and the others where they ride; how the hand holds it, and where its muzzle is. */
  private hold(): void {
    const held = this.slung ? undefined : this.carried[this.drawn];
    // A pistol as the only gun rides in the holster, not slung across the back as a rifle does (22/10 #10: at the base,
    // weapons all on them, it hung on the back).
    const main = this.spec.weapon;
    const pistolOnly = !!main && !!(WEAPONS[main] as { pistol?: boolean }).pistol && !this.carried.sidearm;
    for (const k of CARRIES) {
      const m = this.carried[k];
      if (!m || m === held) continue;
      const ride: Carry = k === 'weapon' && pistolOnly ? 'sidearm' : k;
      const stow = this.stows[ride];
      if (!stow) {
        m.removeFromParent();
        continue;
      }
      stow.add(m);
      m.position.set(0, 0, ride === 'weapon' ? -(m.userData.middle as number) : 0);
      this.outOfBody(ride, m);
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
      this.gunSpan = (held.userData.span as Span | undefined) ?? null;
      this.gunProfile = this.melee ? null : ((held.userData.profile as Profile | undefined) ?? null);
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
      this.gunSpan = null;
      this.gunProfile = null;
      this.muzzle.position.set(0, 0, 0);
      this.yaw.add(this.gunPivot);
      this.gunPivot.position.set(0, this.chest, 0.3);
      this.gunPivot.quaternion.identity();
      this.armed = false;
    }
  }

  /**
   * How far ahead of its feet the muzzle of the long gun in its hands comes when shouldered (tiles): the grip
   * where holdWeapon puts it aimed, and the muzzle along the gun from there. Null unarmed, for a pistol or a blade.
   */
  aimReach(): number | null {
    const r = this.rest;
    if (!r || !this.armed || this.melee || this.pistol) return null;
    return (r.p[B.Shoulder_R]!.z + 0.19 + this.muzzle.position.z) * this.scale;
  }

  /**
   * Coming into sight (unitViews: a dead one out of the fog, playtest 20/10 #10): drawn in over `seconds`, dithered
   * (dyedMaterial's dyeFade), out of the crowd meanwhile and casting no shadow until it is all there.
   */
  fadeIn(seconds: number): void {
    this.fade = 0;
    this.fadeGoal = 1;
    this.fadeRate = 1 / Math.max(0.05, seconds);
    this.fadeClock = -Infinity;
  }

  /**
   * Out of the camera's way (unitViews: between it and the shot over a shoulder, playtest 20/10 #11): dithered out
   * over VEIL_SECONDS, or back in; what it carries goes halfway through.
   */
  veil(on: boolean): void {
    const goal = on ? 0 : 1;
    if (goal === this.fadeGoal) return;
    this.fadeGoal = goal;
    this.fadeRate = 1 / VEIL_SECONDS;
    this.fadeClock = -Infinity;
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
    if (!again && sameArms(a, this.spec) && sameFit(a, this.spec)) return;
    this.spec.weapon = a.weapon;
    this.spec.sidearm = a.sidearm ?? null;
    this.spec.belt = a.belt ?? null;
    this.spec.mods = a.mods;
    this.spec.condition = a.condition;
    this.drawn = 'weapon';
    const put = (g: Guns) => {
      if (this.rest && sameArms(armsOf(g), this.spec)) this.equip(g);
    };
    const now = gunsNow(this.spec);
    if (now) put(now);
    else void loadGuns({ ...this.spec }).then(put, () => undefined);
  }

  /** Its colours again (squadTint changed). */
  repaint(): void {
    if (this.material) this.paint(this.material);
  }

  /** Barracks colours on the cloth, the owner's band on the arms (a soldier from the wardrobe: only with squadTint). */
  private paint(m: DyedMaterial): void {
    const dyes = m.userData.dyes;
    const { dyes: colours, band } = this.spec;
    const tint = !this.spec.dress || squadTint;
    dyes.dyeAmount.value.set(0, 0, 0, 0);
    if (tint) {
      colours?.forEach((c, i) => {
        if (!c) return;
        dyes.dyeColors.value[i + 1]!.set(c);
        dyes.dyeAmount.value.setComponent(i, 0.6);
      });
    }
    dyes.dyeBand.value = 0;
    if (band && tint) {
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

  // ---------------------------------------------------------------- pose

  applyPose(p: FigurePose, time: number): void {
    if (this.fade !== this.fadeGoal) {
      const step = this.fadeClock === -Infinity ? 0 : THREE.MathUtils.clamp(time - this.fadeClock, 0, 0.25) * this.fadeRate;
      this.fade = this.fadeGoal > this.fade ? Math.min(this.fadeGoal, Math.max(0.001, this.fade + step)) : Math.max(this.fadeGoal, this.fade - step);
      this.fadeClock = time;
      for (const o of this.painted) o.castShadow = this.fade >= 1;
      // Its weapons have no dither of their own: they go, and come back, halfway.
      for (const k of CARRIES) if (this.carried[k]) this.carried[k]!.visible = this.fade >= 0.5;
    }
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
    this.uprightItem();
    const r = this.rest;
    if (!r) {
      this.clock = time;
      return;
    }
    if (this.rests(p, time)) return;
    // (Just out of the crowd, its clip is where the crowd's loop had got to: no time to catch up on.)
    const dt = this.clock === -Infinity ? 0 : THREE.MathUtils.clamp(time - this.clock, 0, 0.25);
    this.clock = time;
    this.dt = dt;
    const { d, p: pos } = this;
    const dead = this.spec.dead;
    const shamble = dead ? 1 : 0;
    const anim = this.anim;

    // Legs and the spine underneath: the clips, or procedural legs until they load.
    if (anim) this.animate(p, dt);
    else this.legs(p, time);
    if (anim && this.action?.raw) {
      // The clips alone (the weapons lab): every bone as they turn it, the weapon in the hand as it hangs there.
      for (let i = 0; i < N; i++) if (i !== B.Hips) this.fk(i);
      this.write();
      this.poses++;
      return;
    }
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
    // (A throw's clip leans the body itself: direct.)
    const lean = ((dead ? style.lean : 0) + (anim ? 0 : 0.28 * p.crouch) + (this.thrown || this.struck ? 0 : p.lean) - knock - p.recoil * 0.05 - scream * 0.5 + search * 0.45 + breathe) * live;
    // A two-handed gun blades the upper body: the support shoulder comes forward so that arm bends under
    // the gun instead of reaching across the chest; the gun and the head keep facing ahead (holdWeapon, the neck).
    // A pistol, hardly: it is held square to the target, both arms out.
    // (The weapon's clips blade it themselves: direct.)
    this.blade = this.fore && !dead && this.clipped <= 0.5 && !this.thrown ? -(0.22 + 0.24 * p.aim) * (this.pistol ? 0.15 : 1) * live : 0;
    // A melee stroke turns the living's upper body back and then across (the weapon goes with it); the dead grab instead.
    const swing = (p.swing ?? 0) * live;
    const grab = dead ? Math.abs(swing) : 0;
    const twist = shamble * this.sway(1.7, 1.7, 0, p, time) * 0.12 * style.twist * live + this.blade + (dead || this.struck ? 0 : -swing * 0.75);
    // Without clips the hips already roll (legs()); with them the roll goes on here, once a stride as they move.
    const roll = anim ? shamble * this.sway(1.3, Math.PI * CADENCE[this.gait.motion], 0, p, time) * (0.05 + p.run * 0.08) * style.roll * live : 0;
    if (anim) d[B.Hips]!.premultiply(rot(q1, Z, roll));
    else for (let k = 0; k < SPINE.length; k++) d[SPINE[k]!]!.copy(d[B.Hips]!);
    this.tilt(B.Spine_01, roll, twist * 0.3, lean * 0.35);
    this.tilt(B.Spine_02, roll, twist * 0.6, lean * 0.7);
    const upper = this.tilt(B.Spine_03, roll, twist, lean);
    // Neck and head: they keep the eyes up as the spine leans; the dead's lolls, a screamer's goes back.
    // Aiming a gun the head comes down and over to the stock, the eye behind the sights.
    const sight = this.armed && !this.melee && this.clipped <= 0.5 ? p.aim * live : 0;
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
      if (this.thrown) {
        // The throw's clip has the free arm.
      } else if (throwing) this.throwArm(p.lean);
      else if (search > 0.01) {
        // Rummaging: the gun stays low in the right hand, the left goes in among the things.
        const grip = tmpR.copy(this.fore ? this.leftHand(p) : this.p[B.Hand_L]!);
        this.reach('L', v3.lerpVectors(grip, this.rummage(1, time), search), v4.set(0.7, -1, 0.2));
      } else if (this.fore && this.pistol && this.clipped > 0.5) {
        // The pistol's clips have both hands round its grip already: the left stays theirs, never let go (21/10 #1).
        for (let k = 0; k < ARM_L.length; k++) this.fk(ARM_L[k]!);
        this.letGo = 0;
      } else if (this.fore) {
        this.support(p);
        // A clip's gun is where its own left hand holds it, the arm already clear of the body: the hand kept on the
        // handguard, closed round it (22/10 #3: let go, it hung open by it, holding nothing).
        if (this.clipped > 0.5) this.letGo = this.slid = 0;
        else this.letGoIf(p, upper, anim !== null, time);
      } else if (anim) this.followArm('L', upper);
      else this.hang('L', p.run * Math.sin(time * 11 + p.phase) * 0.4);
      if (!throwing && !(search > 0.01) && !this.fore) this.spreadArm('L');
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
      this.spreadArm('R');
    } else if (anim) {
      this.followArm('L', upper);
      this.followArm('R', upper);
      // (Sat, knelt or leant at the camp, or getting into it or out, the arms are the clip's: on the thighs, round the
      // knees, they were taken for arms in the body and swung out, now and then: 22/10 #11.)
      if (!this.atCamp) {
        this.spreadArm('L');
        this.spreadArm('R');
      }
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
      this.spreadArm('L');
      this.spreadArm('R');
    }
    // A stone tossed with the free hand, over whatever that arm was doing.
    const toss = (p.toss ?? 0) * live;
    if (toss > 0 && !dead && !this.thrown) this.tossArm(toss);
    this.hands(p, dt, live, climb, search, throwing, toss);
    this.slingOnBack(p);
    this.aimTurn(p, dt, live);
    this.write();
    this.poses++;
    if (clip) this.dropWeapon(Math.min(1, p.fall) * clip.seconds);
  }

  // ------------------------------------------------------------- the crowd

  /** What the crowd knows this figure's body by: the same model, gait style and stance bake alike. */
  private get crowdKey(): string {
    return `${this.spec.character ?? ''}|${this.spec.style ?? ''}|${this.stance}`;
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
    const loop = this.spec.dead && !this.spec.own && !this.baking && this.anim && this.model && CROWD.enabled && this.fade >= 1 ? this.calmLoop(p) : -1;
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
      bakers.set(key, new SyntyFigure({ ...this.spec }));
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
    const pose: FigurePose = { crouch: 0, aim: 0, run: 0, recoil: 0, hit: 0, flash: 0, fall: 0, lean: 0, hop: 0, yaw: 0, phase: 0, spin: 0, search: 0, climb: 0, swing: 0, react: 0, toss: 0 };
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
    const { k, cramped } = deathFor(DEATHS[this.build], blow, this.scale);
    // With room for no fall, the body is drawn in towards where it stood: its head comes down inside its tile.
    const death = DEATHS[this.build][k]!;
    const tuck = cramped ? Math.max(0, death.reach * SYNTY_SCALE - CRAMPED_REACH / this.scale) : 0;
    this.death = { k, seconds: a.set.settles[k]!, tuck: { x: -Math.sin(death.falls) * tuck, z: -Math.cos(death.falls) * tuck } };
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
      m.userData.dyes.dyeFade.value = this.fade;
      return;
    }
    // A flash, or fading in: on a material of its own (the shared one is everyone's).
    const lit = k > 0.005 || this.fade < 1;
    if (lit && !this.own) {
      this.own = dyedMaterial(atlasOf(this.painted[0]!));
      this.own.userData.dyes.dyeRim.value = ENEMY_RIM;
    }
    const use = lit ? this.own! : m;
    if (this.painted[0]?.material !== use) for (const o of this.painted) o.material = use;
    if (lit) {
      this.own!.userData.dyes.dyeFlash.value = k;
      this.own!.userData.dyes.dyeFade.value = this.fade;
    }
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
      (p.run < 0.02 && p.aim < 0.02 && p.hit < 0.01 && p.recoil < 0.01 && !p.lean && !p.hop && !p.fall && !(p.search ?? 0) && !(p.toss ?? 0) && (spin <= 0 || spin >= 1) && Math.abs(p.yaw - this.lastYaw) < 1e-3);
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
      const tuck = this.death.tuck;
      if (tuck.x || tuck.z) {
        const f = Math.min(1, p.fall);
        const k = 1 - (1 - f) * (1 - f);
        this.p[B.Hips]!.x += tuck.x * k;
        this.p[B.Hips]!.z += tuck.z * k;
      }
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
    // Moving with a gun in the hands, the clips that hold it (ARMED_MOTIONS: 22/10 #15).
    a.armedAs(!SYNTY_HANDS.move || dead || this.death || !this.armed || this.melee || !this.fore ? null : this.pistol ? 'pistol' : 'rifle', dt);
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
    this.actClock += dt * this.actRate;
    const plays = this.action ? [this.action] : this.direct(p);
    if (this.action) this.clipped = this.action.share;
    a.play(plays);
    // A corpse stays as it fell. Only a unit at ease looks round now and then (not over the weapon's clips).
    if (this.campDirty) this.applyCamp(a);
    const still = p.aim < 0.05 && p.hit < 0.01 && !(p.search ?? 0) && !dead;
    if (p.fall < 1) a.update(dt, !plays.length && still, plays.length > 0 && still && p.run < 0.05 && !this.thrown && !this.struck);
    a.read(this.d, this.hipsShift);
    // The hips sway as the clip says; their height keeps the lower foot on the ground, whatever the
    // difference between the clip's legs and this character's.
    const hips = this.p[B.Hips]!.copy(this.hipsShift).multiplyScalar(r.hip).add(r.p[B.Hips]!);
    hips.y = r.p[B.Hips]!.y;
    const lowest = Math.min(this.ankle(B.UpperLeg_L, B.LowerLeg_L, B.Ankle_L), this.ankle(B.UpperLeg_R, B.LowerLeg_R, B.Ankle_R));
    hips.y += r.p[B.Ankle_L]!.y - lowest;
    // In a move that leaves the ground (CAMP_FREE: into a car, up a step), as high as the clip has them.
    if (this.freeMove) {
      if (!this.campMoving && a.moveShare <= 0) this.freeMove = false;
      else hips.y = lerp(hips.y, r.p[B.Hips]!.y + this.hipsShift.y * r.hip, a.moveShare);
    }
  }

  /**
   * The weapon's actions (Mixamo, docs/MIXAMO.md; Arturo 21/10, notes 1, 6, 7) for what the unit does: a two-handed
   * gun or the pistol in both hands held at rest or aimed, kneeling with a rifle, a reload. None for the dead, a
   * blade, a climb, a throw, a search, a strike or a fall: those stay procedural. Their shares together say how much
   * of the arms are the clips' (clipped: over a half, holdWeapon takes the gun as their hands hold it).
   */
  private direct(p: FigurePose): ActionPlay[] {
    const out = this.plays;
    out.length = 0;
    this.clipped = 0;
    this.pistolRest = 0;
    this.thrown = false;
    this.struck = false;
    const a = this.anim;
    if (a && !this.spec.dead && this.armed && this.melee && !this.death) return this.strike(p, a);
    if (!a || this.spec.dead || !this.armed || this.melee || !this.fore || this.death) return out;
    const live = 1 - Math.min(1, p.fall);
    if (live <= 0 || (p.climb ?? 0) > 0.01 || (p.search ?? 0) > 0.01 || Math.abs(p.swing ?? 0) > 0.01) return out;
    // A throw: a grenade's wind-up (the lean back: throwMotion), a stone's toss (tossStone). Its clip, the free
    // (left) hand's, runs so that it lets go when the game does; the gun stays in the right hand meanwhile.
    const clock = this.actClock;
    const loop = (id: ActionId) => clock % Math.max(0.01, a.actSeconds(id));
    function add(id: ActionId, share: number, upper: boolean | 'free', time = loop(id)): void {
      if (share > 0.001) out.push({ id, time, share, upper });
    }
    const toss = p.toss ?? 0;
    if (!this.throwKind && (p.lean < -0.02 || toss > 0)) {
      this.throwKind = toss > 0 ? 'stone' : 'grenade';
      this.throwT = 0;
    } else if (this.throwKind) this.throwT += this.dt;
    if (this.throwKind) {
      const t = THROWS[this.throwKind];
      if (this.throwT > t.end && Math.abs(p.lean) < 0.02 && toss <= 0) this.throwKind = null;
      else {
        // The clip has the body and the throwing arm; the gun arm keeps the gun low as ever (holdWeapon's own).
        const share = live * THREE.MathUtils.clamp(Math.min(this.throwT / 0.08, (t.end - this.throwT) / 0.15), 0, 1);
        add(t.clip, share, 'free', THREE.MathUtils.clamp(throwClipTime(this.throwKind, this.throwT), 0, a.actSeconds(t.clip)));
        this.thrown = share > 0.5;
        return out;
      }
    }
    if (Math.abs(p.lean) > 0.04 || toss > 0) return out;
    const aim = THREE.MathUtils.clamp(p.aim, 0, 1);
    const crouch = THREE.MathUtils.clamp(p.crouch, 0, 1) * (1 - THREE.MathUtils.clamp(p.run, 0, 1));
    // A reload (spin 0 to 1) eases in and out over its ends, the clip's own hands on the magazine meanwhile.
    const spin = THREE.MathUtils.clamp(p.spin ?? 0, 0, 1);
    const reload = !this.pistol && spin > 0 && spin < 1 ? Math.min(1, spin / 0.12, (1 - spin) / 0.12) : 0;
    const held = live * (1 - reload);
    // Moving, the arms the moving clips' (ARMED_MOTIONS) as far as they have them: the gun at rest gives way to them.
    const moving = a.armedShare();
    if (this.pistol) {
      // At rest in the right hand by the thigh, the left arm free (PISTOL_REST); aimed in both hands; crouched the same
      // over the crouch's legs.
      const span = PISTOL_REST.to - PISTOL_REST.from;
      this.pistolRest = held * (1 - aim) * (1 - moving);
      add('pistolRest', this.pistolRest, true, PISTOL_REST.from + span * (0.5 - 0.5 * Math.cos((Math.PI * clock) / span)));
      add('pistolAim', held * aim, true);
    } else {
      // Standing (or moving) the rifle at low ready or shouldered over the legs' clips; kneeling, the whole body.
      // Each their own way of waiting (RIFLE_RESTS); moving, all the first (one looking about would turn as they walk).
      const rest = SYNTY_HANDS.rest ?? this.restId ?? this.spec.rest ?? RIFLE_RESTS[0];
      const stand = held * (1 - crouch) * (1 - aim) * (1 - moving);
      const run = THREE.MathUtils.clamp(p.run, 0, 1);
      add(rest, rest === RIFLE_RESTS[0] ? stand : stand * (1 - run), true);
      if (rest !== RIFLE_RESTS[0]) add(RIFLE_RESTS[0], stand * run, true);
      add('rifleAim', held * (1 - crouch) * aim, true);
      add('rifleKneel', held * crouch * (1 - aim), false);
      add('rifleKneelAim', held * crouch * aim, false);
      add('rifleReload', live * reload, true, spin * a.actSeconds('rifleReload'));
    }
    for (const play of out) this.clipped += play.share;
    this.clipped += moving * (1 - aim) * held;
    return out;
  }

  /**
   * A blade's stroke (the pose's swing: unitViews lunge) with its clip over the upper body, the knife's stab or the
   * machete's swing, timed so it lands when the game's does; the blade in the clip's hand (clipHold).
   */
  private strike(p: FigurePose, a: AnimDriver): ActionPlay[] {
    const out = this.plays;
    const swing = p.swing ?? 0;
    const live = 1 - Math.min(1, p.fall);
    const blade = this.drawn === 'belt' ? this.spec.belt : this.spec.weapon;
    const kind = String(blade ?? '').includes('knife') ? 'knife' : 'machete';
    if (this.throwKind !== kind && Math.abs(swing) > 0.01) {
      this.throwKind = kind;
      this.throwT = 0;
    } else if (this.throwKind === kind) this.throwT += this.dt;
    if (this.throwKind !== kind || live <= 0) return out;
    const t = THROWS[kind];
    if (this.throwT > t.end && Math.abs(swing) < 0.01) {
      this.throwKind = null;
      return out;
    }
    const share = live * THREE.MathUtils.clamp(Math.min(this.throwT / 0.06, (t.end - this.throwT) / 0.15), 0, 1);
    if (share > 0.001) out.push({ id: t.clip, time: THREE.MathUtils.clamp(throwClipTime(kind, this.throwT), 0, a.actSeconds(t.clip)), share, upper: true });
    this.clipped = share;
    this.struck = share > 0.5;
    return out;
  }

  /**
   * The gun as the clips' hands hold it (direct): fast in the right hand (clipGrip), the right arm the clip's; a
   * blade as the hand holds it.
   */
  private clipHold(aim = 0, crouch = 0): void {
    const r = this.rest!;
    const { d, p: pos } = this;
    for (let k = 0; k < ARMS.length; k++) this.fk(ARMS[k]!);
    hq.copy(d[B.Hand_R]!).multiply(hr.copy(r.gripTurn).invert());
    // Both hands on it (no reload): a rifle's barrel from the right hand to the left; a pistol's, both
    // hands round its grip, along the forearm. What that turn is in the hand's terms is learnt, a little a frame.
    const steady = !this.melee && this.clipped > 0.9 && !this.plays.some((a) => a.id === 'rifleReload');
    if (steady) {
      const toward = this.pistol ? v1.copy(pos[B.Hand_R]!).sub(pos[B.Elbow_R]!) : v1.copy(pos[B.Hand_L]!).sub(pos[B.Hand_R]!);
      if (toward.lengthSq() > 1e-6) {
        hr.setFromUnitVectors(v2.copy(Z).applyQuaternion(hq), toward.normalize()).multiply(hq);
        hr.premultiply(q1.copy(hq).invert());
        if (this.clipGripKnown) this.clipGrip.slerp(hr, 0.2);
        else this.clipGrip.copy(hr);
        this.clipGripKnown = true;
      }
    }
    if (this.clipGripKnown && !this.melee) hq.multiply(this.clipGrip);
    this.gunQ.copy(hq);
    this.gunP.copy(GRIP).applyQuaternion(this.gunQ).add(pos[B.Hand_R]!);
    if (!this.melee && this.gunProfile) this.fitGun(1 - THREE.MathUtils.clamp(aim, 0, 1));
    // Aimed, the gun slid ahead along itself out of the upper arm, the right hand reaching after it (22/10: our arms
    // shorter than the clip's X Bot's, the stock went back past the shoulder into the arm).
    if (!this.melee && !this.pistol && this.gunProfile && SYNTY_HANDS.slide && this.slideGun(aim)) {
      const wrist = fp.copy(GRIP).applyQuaternion(this.gunQ).negate().add(this.gunP);
      this.reach('R', wrist, fh.copy(pos[B.Elbow_R]!).sub(pos[B.Shoulder_R]!));
      d[B.Hand_R]!.copy(this.gunQ).multiply(r.gripTurn);
      return;
    }
    if (this.pistol && this.pistolRest > 0.5) this.spreadPistol();
    else this.pistolOut = this.ease(this.pistolOut, 0);
    // The right arm out of the gun and the body (22/10: the rifle at rest through the forearm and the upper arm, the
    // clip's arm made for Mixamo's thinner one): the elbow swung round the shoulder–hand line, the hand and the gun
    // where they are.
    if (!this.melee && SYNTY_HANDS.swing) {
      const s = pos[B.Shoulder_R]!;
      const h = pos[B.Hand_R]!;
      // (Standing at rest it swings little: further, it stood out like a wing; aimed or kneeling, the elbow comes up.)
      const max = lerp(CLIP_SWING, SWING_MAX, THREE.MathUtils.clamp(Math.max(aim, crouch), 0, 1));
      if (this.swingElbow('R', s, h, ce.copy(pos[B.Elbow_R]!), max)) {
        sq.setFromAxisAngle(sa.copy(h).sub(s).normalize(), this.swing[1]!);
        d[B.Shoulder_R]!.premultiply(sq);
        d[B.Elbow_R]!.premultiply(sq);
        this.fk(B.Elbow_R);
      }
    }
    // Aimed, a pistol held level, the wrist bent down to it (22/10 #14: «Idle With Aimed Pistol» has it by the face,
    // along the raised forearm, 20° up; knelt, 10–16°).
    if (this.pistol && aim > 0 && SYNTY_HANDS.aim) {
      v1.copy(Z).applyQuaternion(this.gunQ);
      v2.set(v1.x, v1.y * (1 - THREE.MathUtils.clamp(aim, 0, 1)), v1.z).normalize();
      this.gunQ.premultiply(sq.setFromUnitVectors(v1, v2));
      this.gunP.copy(GRIP).applyQuaternion(this.gunQ).add(pos[B.Hand_R]!);
    }
    d[B.Hand_R]!.copy(this.gunQ).multiply(r.gripTurn);
  }

  /**
   * The pistol at rest by the thigh (PISTOL_REST) out of it (22/10: the muzzle in a heavy build's thigh): the right
   * arm turned out at the shoulder, about the chest's forward line, the least that clears it (spreadArm's way), the gun
   * with the hand; back in as far as it stays clear; eased.
   */
  private spreadPistol(): void {
    const span = this.gunSpan;
    if (!span || !this.bodyAware || !this.rest!.bulk || !SYNTY_HANDS.gun) {
      this.pistolOut = 0;
      return;
    }
    const { d, p: pos } = this;
    const s = pos[B.Shoulder_R]!;
    const axis = sa.copy(Z).applyQuaternion(d[B.Spine_03]!);
    const depth = (angle: number) => {
      sq.setFromAxisAngle(axis, -angle);
      gq.copy(sq).multiply(this.gunQ);
      fp.copy(this.gunP).sub(s).applyQuaternion(sq).add(s);
      let worst = -1;
      for (let k = 0; k < GUN_POINTS; k++) {
        for (const y of [0, span.lo, span.hi]) {
          gp.set(span.x, span.y + y, lerp(span.z0, span.z1, k / (GUN_POINTS - 1))).applyQuaternion(gq).add(fp);
          worst = Math.max(worst, this.inBody(gp, span.r));
        }
      }
      return worst;
    };
    const now = this.pistolOut;
    let goal = now;
    let least = depth(now);
    if (least > TOUCH && this.waits[6]! > 0) {
      this.waits[6]!--;
      goal = this.pistolGoal;
    } else if (least > TOUCH) {
      let clear = false;
      for (let a = now + SPREAD_STEP; a <= PISTOL_SPREAD_MAX + 1e-6; a += SPREAD_STEP) {
        const dd = depth(a);
        if (dd <= TOUCH) {
          goal = a;
          clear = true;
          break;
        }
        if (dd < least) {
          least = dd;
          goal = a;
        }
      }
      this.pistolGoal = goal;
      this.waits[6] = clear ? 0 : SEARCH_REST;
    } else if (now) {
      const a = Math.max(0, now - SPREAD_STEP);
      if (depth(a) <= TOUCH) goal = a;
    }
    this.pistolOut = this.ease(now, goal);
    if (!this.pistolOut) return;
    sq.setFromAxisAngle(axis, -this.pistolOut);
    d[B.Shoulder_R]!.premultiply(sq);
    d[B.Elbow_R]!.premultiply(sq);
    this.fk(B.Elbow_R);
    this.fk(B.Hand_R);
    this.gunQ.premultiply(sq);
    this.gunP.sub(s).applyQuaternion(sq).add(s);
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
    if (this.clipped > 0.5) {
      this.clipHold(p.aim, THREE.MathUtils.clamp(p.crouch, 0, 1) * (1 - THREE.MathUtils.clamp(p.run, 0, 1)));
      return;
    }
    const r = this.rest!;
    const { d, p: pos } = this;
    const sh = r.p[B.Shoulder_R]!;
    const aim = p.aim;
    const s3 = B.Spine_03;
    const reload = Math.sin(THREE.MathUtils.clamp(p.spin ?? 0, 0, 1) * Math.PI);
    if (this.melee) {
      // (By the hip, a little out and ahead of the holstered pistol's grip, 22/10: the hand was in it, the thigh swung
      // up into it walking.)
      v1.set(sh.x * lerp(1.25, 1.1, aim), lerp(r.hip + 0.02, sh.y + 0.08, aim), lerp(sh.z + 0.14, sh.z + 0.18, aim));
      e1.set(lerp(1.15, -0.9, aim) - lean - THREE.MathUtils.clamp(p.crouch, 0, 1) * (1 - aim) * BLADE_CROUCH_LEVEL, 0, 0, 'YXZ');
    } else if (this.pistol) {
      // Low ready, both hands on it ahead of the belly, the muzzle at the ground in front; aimed (playtest 16/10
      // #3), square to the target with both arms almost straight, the gun in the middle and its sights at the eye.
      v1.set(lerp(sh.x * 0.06, 0, aim), lerp(sh.y - 0.22, sh.y + 0.16, aim) + reload * 0.1, lerp(sh.z + 0.3, sh.z + 0.5, aim));
      e1.set((1 - aim) * 0.75 - p.recoil * 0.45 - reload * 0.6, reload * 0.2, reload * 0.9, 'YXZ');
    } else {
      // Shouldered, the grip comes up level with the shoulder: the stock in its pocket, the sights at the eye. At low
      // ready nearer the shorter the arms (LOW_READY), so the support hand reaches the handguard.
      const k = Math.min(1, r.reach);
      const low = LOW_READY;
      const front = r.bulk?.front ?? sh.z;
      const wide = THREE.MathUtils.clamp((front - low.wide[0]!) / (low.wide[1]! - low.wide[0]!), 0, 1);
      const ahead = Math.max(sh.z + low.ahead * k, front + low.clear);
      const down = lerp(low.down, low.downWide, wide) * k;
      v1.set(lerp(sh.x * low.across, sh.x * 0.5, aim), lerp(sh.y - down, sh.y - 0.02, aim) + reload * 0.08, lerp(ahead, sh.z + 0.19, aim));
      e1.set((1 - aim) * lerp(low.tip, low.tipWide, wide) - p.recoil * 0.18 - reload * 0.5, (1 - aim) * low.turn + reload * 0.2, reload * 0.9, 'YXZ');
    }
    // While the free hand throws (direct: the throw's clip turns the chest round), the rifle tucked against the body:
    // at the hip, pressed to it, the muzzle at the ground, following the hips, not the chest (21/10, Gameplay).
    const tucked = this.thrown && !this.melee && !this.pistol;
    if (tucked) {
      v1.set(sh.x * 0.45, r.hip + 0.1, sh.z + 0.14);
      e1.set(1.05, 0.55, 0, 'YXZ');
    }
    // Crouched at low ready: over the knees (a blade too, 22/10: by the hip it went into the thigh, the hand into the
    // pistol's holster).
    if (!tucked) {
      const low = THREE.MathUtils.clamp(p.crouch, 0, 1) * (1 - aim);
      v1.y += low * (this.melee ? BLADE_CROUCH_LIFT : CROUCH_LIFT);
      v1.z += low * (this.melee ? BLADE_CROUCH_AHEAD : CROUCH_AHEAD);
    }
    this.gunQ.setFromEuler(e1);
    if (this.melee) this.gunQ.premultiply(d[s3]!);
    else if (!this.pistol) {
      // A gun stays level however the clip bends the spine (a crouch, a hunch): only the chest's heading turns it
      // (tucked for a throw, the hips').
      const ahead = v2.copy(Z).applyQuaternion(d[tucked ? B.Hips : s3]!);
      this.gunQ.premultiply(q1.setFromAxisAngle(Y, Math.atan2(ahead.x, ahead.z) - this.blade));
    }
    // A pistol keeps to the body's own front, not the chest's: the clips turn the chest as for a rifle, and
    // took the pistol over to the right shoulder, out of the left hand's reach. It follows the chest up and down;
    // and turns with it while the body throws or strikes (leant back or twisted round, the arm went through it).
    if (this.pistol) {
      const turn = THREE.MathUtils.clamp(Math.max(Math.abs(p.lean) / 0.3, Math.abs(p.swing ?? 0)), 0, 1);
      if (turn > 0) {
        q2.identity().slerp(d[s3]!, turn);
        this.gunQ.premultiply(q2);
        this.gunP.copy(v1).sub(r.p[s3]!).applyQuaternion(q2).add(pos[s3]!);
      } else this.gunP.copy(v1).sub(r.p[s3]!).add(pos[s3]!);
    } else if (tucked) this.gunP.copy(v1).sub(r.p[B.Hips]!).applyQuaternion(d[B.Hips]!).add(pos[B.Hips]!);
    else this.gunP.copy(v1).sub(r.p[s3]!).applyQuaternion(d[s3]!).add(pos[s3]!);
    this.gunP.addScaledVector(v2.copy(Z).applyQuaternion(this.gunQ), -p.recoil * 0.05);
    this.clearGun();
    // Right hand on the grip, holding the gun's turn.
    const hand = v3.copy(GRIP).applyQuaternion(this.gunQ).negate().add(this.gunP);
    // The elbow hangs at low ready and comes out to the side when the stock is shouldered; a pistol's stays down.
    if (this.pistol) this.reach('R', hand, v4.set(-0.55, -1, lerp(-0.3, -0.1, aim)));
    else this.reach('R', hand, v4.set(lerp(-0.7, -1, aim), lerp(-1, -0.45, aim), lerp(-0.35, -0.25, aim)));
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
    const grip = v3.set(0.012, this.foreY, this.fore - this.slid * SLIDE_BACK).add(FORE_WRIST).applyQuaternion(this.gunQ).add(this.gunP);
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
    const full = a + c;
    let len = dir.length();
    if (len > full - SOFT_REACH) len = full - SOFT_REACH * Math.exp(-(len - (full - SOFT_REACH)) / SOFT_REACH);
    len = THREE.MathUtils.clamp(len, Math.abs(a - c) + 0.01, full - 0.001);
    dir.normalize();
    const alpha = Math.acos(THREE.MathUtils.clamp((a * a + len * len - c * c) / (2 * a * len), -1, 1));
    const axis = v2.copy(dir).cross(pole);
    // Near the pole's line the bend goes over to the one it had (that, square to the arm now), not flipping.
    const sure = Math.min(1, axis.length() / Math.max(1e-6, pole.length()) / POLE_SURE);
    const bend = this.bends[side === 'L' ? 0 : 1]!;
    if (sure < 1 && bend.lengthSq() > 0.5) {
      tmpR.copy(bend).addScaledVector(dir, -bend.dot(dir));
      if (tmpR.lengthSq() > 1e-6) axis.normalize().multiplyScalar(sure).addScaledVector(tmpR.normalize(), 1 - sure);
    }
    if (axis.lengthSq() < 1e-6) axis.set(1, 0, 0);
    axis.normalize();
    bend.copy(axis);
    const upper = dir.applyAxisAngle(axis, alpha);
    const elbow = v2.copy(s).addScaledVector(upper, a);
    // Out of the body: the elbow swung round the shoulder–hand line, the hand where it is.
    if (this.swingElbow(side, s, target, elbow)) upper.copy(elbow).sub(s).normalize();
    const fore = elbow.sub(target).negate().normalize();
    // setArm uses v1 and v2 itself: hand it copies.
    this.setArm(side, tmpU.copy(upper), tmpF.copy(fore));
  }

  /** How far out the upper arm or the forearm on side `i` (0 left) goes round its bone at `f` along it (Bulk's bins). */
  private armOut(i: number, upper: boolean, f: number): number {
    const b = this.rest!.bulk!;
    const along = upper ? b.upperAlong[i]! : b.foreAlong[i]!;
    return along[THREE.MathUtils.clamp(Math.floor(f * ARM_BINS), 0, ARM_BINS - 1)]!;
  }

  /** How deep a ball of `radius` at `at` (model space, as posed now) is in the gun in the hand (its Profile); ≤ 0 clear. */
  private inGun(at: THREE.Vector3, radius: number, part?: { z0: number; z1: number; top: number }): number {
    const g = this.gunProfile!;
    // (A little room kept: a sleeve's folds and a gun's sling go past the outlines measured.)
    radius += GUN_ROOM;
    const l = gl.copy(at).sub(this.gunP).applyQuaternion(gi.copy(this.gunQ).invert());
    const k = Math.floor((l.z - g.z0) / g.step);
    let worst = -1;
    for (let n = k - 1; n <= k + 1; n++) {
      const s0 = g.slices[n];
      if (!s0 || s0.r <= 0) continue;
      // Only a part of it (`part`: between z0 and z1 along it, below top): the grip's foot.
      const zc = g.z0 + (n + 0.5) * g.step;
      if (part && (zc < part.z0 || zc > part.z1 || s0.lo > part.top)) continue;
      const s = part ? { ...s0, hi: Math.min(s0.hi, part.top) } : s0;
      // The slice as a wall of its width: from its top to its bottom (in by its half-width), its middle across.
      const a = s.lo + s.r;
      const b = s.hi - s.r;
      const y = a <= b ? THREE.MathUtils.clamp(l.y, a, b) : (s.lo + s.hi) / 2;
      const z = THREE.MathUtils.clamp(l.z, g.z0 + n * g.step, g.z0 + (n + 1) * g.step);
      worst = Math.max(worst, s.r + radius - Math.hypot(l.x - s.x, l.y - y, l.z - z));
    }
    return worst;
  }

  /** How deep a ball of `radius` at `at` (model space, as posed now) is in the body: the deepest of its parts (bulk.ts); ≤ 0 clear. */
  private inBody(at: THREE.Vector3, radius: number): number {
    const r = this.rest!;
    const parts = r.bulk!.parts;
    if (this.backed !== this.poses) {
      for (let k = 0; k < parts.length; k++) {
        const g = parts[k]!;
        this.back[g.bone]!.copy(this.d[g.bone]!).invert();
        (this.middles[k] ??= new THREE.Vector3()).copy(g.centre).sub(r.p[g.bone]!).applyQuaternion(this.d[g.bone]!).add(this.p[g.bone]!);
      }
      this.backed = this.poses;
    }
    let worst = -1;
    for (let k = 0; k < parts.length; k++) {
      const g = parts[k]!;
      // Far from the part as it is now: past it at once; else where it would be at rest, by the part's bone.
      const near = g.reach + radius;
      if (at.distanceToSquared(this.middles[k]!) > near * near) continue;
      bq.copy(at).sub(this.p[g.bone]!).applyQuaternion(this.back[g.bone]!).add(r.p[g.bone]!);
      worst = Math.max(worst, depthIn(g, bq, radius));
    }
    return worst;
  }

  /**
   * How deep the arm on side `i` (0 left) is in the body with its shoulder at s, its elbow at e and its wrist at h:
   * the worst of a few points along it (`from`: past the first of the upper arm's, by the shoulder, where sleeve and
   * chest always meet).
   */
  private armIn(i: number, s: THREE.Vector3, e: THREE.Vector3, h: THREE.Vector3, from = 0): number {
    const b = this.rest!.bulk!;
    let worst = this.inBody(h, b.hand);
    for (let k = from; k < ARM_UPPER.length; k++) worst = Math.max(worst, this.inBody(bp.lerpVectors(s, e, ARM_UPPER[k]!), b.upper[i]!));
    for (const f of ARM_FORE) worst = Math.max(worst, this.inBody(bp.lerpVectors(e, h, f), b.fore[i]!));
    // And out of the gun in the hand (22/10: the rifle at rest through the forearms), but where a hand holds it: its
    // wrist and the hand itself (the right one always; the left while it supports it).
    if (this.gunProfile && this.armed && SYNTY_HANDS.gun) {
      const holds = i === 1 || (this.fore > 0 && this.letGo < 0.5 && this.pistolRest < 0.5);
      let gun = -1;
      for (const f of ARM_UPPER_GUN) gun = Math.max(gun, this.inGun(bp.lerpVectors(s, e, f), this.armOut(i, true, f)));
      for (const f of holds ? (i === 0 ? ARM_FORE_GUN_L : ARM_FORE_GUN) : ARM_FORE) gun = Math.max(gun, this.inGun(bp.lerpVectors(e, h, f), this.armOut(i, false, f)));
      if (!holds) gun = Math.max(gun, this.inGun(h, b.hand));
      // The right forearm by the wrist out of the grip's foot below the hand (the forearm turned to the hand's line).
      if (i === 1) for (const f of ARM_CUFF) gun = Math.max(gun, this.inGun(bp.lerpVectors(e, h, f), this.armOut(1, false, f), GRIP_FOOT));
      // Where nothing keeps the arm out of a wide body, the gun through it still counts on top (it shows more): the
      // search then takes a swing clear of the gun at least.
      worst = Math.max(worst, gun) + Math.max(0, gun - TOUCH);
    }
    return worst;
  }

  /** Whether the figure keeps out of its own body (not the dead, nor in the weapons lab with the elbows off: as it was before, to compare). */
  private get bodyAware(): boolean {
    return !!this.rest?.bulk && !this.spec.dead && SYNTY_HANDS.elbows;
  }

  /** Eased from `now` towards `goal` over this pose's seconds, at `rate` a second at most (at once on a first pose). */
  private ease(now: number, goal: number, rate = CLEAR_TURN): number {
    if (this.dt <= 0) return goal;
    const step = (goal - now) * (1 - Math.exp(-this.dt * CLEAR_EASE));
    return now + THREE.MathUtils.clamp(step, -rate * this.dt, rate * this.dt);
  }

  /**
   * Keeps an arm reaching for something out of its own body (an elbow through the chest or a coat, a forearm across
   * it): its elbow swung round the line from the shoulder to the hand, the hand where it is. From the swing it has,
   * to the nearest that is clear (none: the shallowest), and back towards none as far as that stays clear; eased,
   * so it never jumps (playtest 20/10 #2). True if the elbow moved.
   */
  private swingElbow(side: 'L' | 'R', s: THREE.Vector3, h: THREE.Vector3, elbow: THREE.Vector3, max = SWING_MAX): boolean {
    const i = side === 'L' ? 0 : 1;
    if (!this.bodyAware) {
      this.swing[i] = 0;
      return false;
    }
    const axis = sa.copy(h).sub(s).normalize();
    const from = sf.copy(elbow).sub(s);
    const at = (angle: number) => se.copy(from).applyAxisAngle(axis, angle).add(s);
    const depth = (angle: number) => this.armIn(i, s, at(angle), h);
    const now = this.swing[i]!;
    let goal = now;
    const deep = depth(now);
    let least = deep + Math.abs(now) * SWING_COST;
    if (deep > TOUCH && this.waits[i]! > 0) {
      // Nothing was clear a moment ago: towards what was best then.
      this.waits[i]!--;
      goal = this.goals[i]!;
    } else if (deep > TOUCH) {
      let clear = false;
      const first = now < 0 ? -1 : 1;
      search: for (let k = 1; k * SWING_STEP <= max * 2; k++) {
        for (const sign of [first, -first]) {
          const a = now + sign * k * SWING_STEP;
          if (Math.abs(a) > max) continue;
          const dd = depth(a);
          if (dd <= TOUCH) {
            goal = a;
            clear = true;
            break search;
          }
          // None clear: the shallowest, for what the swing costs.
          if (dd + Math.abs(a) * SWING_COST < least) {
            least = dd + Math.abs(a) * SWING_COST;
            goal = a;
          }
        }
      }
      this.goals[i] = goal;
      this.waits[i] = clear ? 0 : SEARCH_REST;
    } else if (now) {
      const a = now - Math.sign(now) * Math.min(Math.abs(now), SWING_STEP);
      if (depth(a) <= TOUCH) goal = a;
    }
    this.swing[i] = this.ease(now, goal);
    if (!this.swing[i]) return false;
    elbow.copy(at(this.swing[i]!));
    return true;
  }

  /**
   * An arm placed with no hand to reach (hanging at the side, as the clip swings it): turned out to the side at the
   * shoulder, about the chest's forward line, as far as it takes to be out of the body (a coat, a wide chest); back in
   * as far as it stays clear; eased.
   */
  private spreadArm(side: 'L' | 'R'): void {
    const i = side === 'L' ? 0 : 1;
    if (!this.bodyAware) {
      this.spread[i] = 0;
      return;
    }
    const [sh, el, hd] = side === 'L' ? ARM_L : ARM_R;
    // (The clip's arms come as turns only.)
    this.fk(el!);
    this.fk(hd!);
    const s = this.p[sh!]!;
    const axis = sa.copy(Z).applyQuaternion(this.d[B.Spine_03]!);
    const out = side === 'L' ? 1 : -1;
    const e0 = sf.copy(this.p[el!]!).sub(s);
    const h0 = sh2.copy(this.p[hd!]!).sub(s);
    const depth = (angle: number) => {
      sq.setFromAxisAngle(axis, out * angle);
      const e = se.copy(e0).applyQuaternion(sq).add(s);
      return this.armIn(i, s, e, gp.copy(h0).applyQuaternion(sq).add(s));
    };
    const now = this.spread[i]!;
    let goal = now;
    let least = depth(now);
    if (least > TOUCH && this.waits[2 + i]! > 0) {
      this.waits[2 + i]!--;
      goal = this.goals[2 + i]!;
    } else if (least > TOUCH) {
      let clear = false;
      for (let a = now + SPREAD_STEP; a <= SPREAD_MAX + 1e-6; a += SPREAD_STEP) {
        const dd = depth(a);
        if (dd <= TOUCH) {
          goal = a;
          clear = true;
          break;
        }
        if (dd < least) {
          least = dd;
          goal = a;
        }
      }
      this.goals[2 + i] = goal;
      this.waits[2 + i] = clear ? 0 : SEARCH_REST;
    } else if (now) {
      const a = Math.max(0, now - SPREAD_STEP);
      if (depth(a) <= TOUCH) goal = a;
    }
    this.spread[i] = this.ease(now, goal);
    if (!this.spread[i]) return;
    sq.setFromAxisAngle(axis, out * this.spread[i]!);
    for (const b of side === 'L' ? ARM_L : ARM_R) this.d[b]!.premultiply(sq);
    this.fk(el!);
    this.fk(hd!);
  }

  /**
   * Aimed, how far ahead along itself the gun slides out of the right upper arm (SLIDE_STEP at a time, up to
   * SLIDE_MAX), eased, `aim` of it; whether it is slid at all (the hand then reaches after it: clipHold).
   */
  private slideGun(aim: number): boolean {
    const b = this.rest!.bulk;
    const now = this.slideOut;
    let goal = 0;
    if (b && this.bodyAware && aim > 0.05) {
      const pos = this.p;
      const s = pos[B.Shoulder_R]!;
      const ahead = fq3.set(0, 0, 1).applyQuaternion(this.gunQ);
      const p0 = fp2.copy(this.gunP);
      // The least slid that clears it; none: the one that leaves it shallowest.
      let least = Infinity;
      for (let push = 0; push <= SLIDE_MAX + 1e-6; push += SLIDE_STEP) {
        this.gunP.copy(p0).addScaledVector(ahead, push);
        // The upper arm as the clip has it (judged on the arm as it would reach after, the forearm went into the gun
        // instead: measured, worse).
        // (From closer to the shoulder, and a little more room: a coat's shoulders are wider than the arm's measure.)
        const e = pos[B.Elbow_R]!;
        let worst = -1;
        for (const f of SLIDE_UPPER) worst = Math.max(worst, this.inGun(fp.lerpVectors(s, e, f), this.armOut(1, true, f) + SLIDE_ROOM));
        if (worst <= TOUCH) {
          goal = push;
          break;
        }
        if (worst + push * 0.1 < least) {
          least = worst + push * 0.1;
          goal = push;
        }
      }
      this.gunP.copy(p0);
      goal *= THREE.MathUtils.clamp(aim, 0, 1);
    }
    this.slideOut = this.ease(now, goal, CLEAR_SLIDE);
    if (this.slideOut <= 1e-4) return false;
    this.gunP.addScaledVector(fq3.set(0, 0, 1).applyQuaternion(this.gunQ), this.slideOut);
    return true;
  }

  /**
   * A clip's gun turned about the wrist out of the right arm (22/10: a rifle's stock at rest through the forearm, the
   * sleeve thicker than the clip's X Bot's): tipped and turned a little, the cheapest that clears it (FIT_WAYS), back
   * towards none as far as that stays clear; eased; `free` of it (none aimed: the shot goes where it points).
   */
  private fitGun(free: number): void {
    const fit = this.gunFit;
    const b = this.rest!.bulk;
    if (!b || !this.bodyAware || free <= 0 || !SYNTY_HANDS.fit) {
      fit.tip = this.ease(fit.tip, 0);
      fit.yaw = this.ease(fit.yaw, 0);
    } else {
      const pos = this.p;
      const wrist = pos[B.Hand_R]!;
      const s = pos[B.Shoulder_R]!;
      const e = pos[B.Elbow_R]!;
      const q0 = fq0.copy(this.gunQ);
      // The left arm as it will reach for the handguard where the gun turns it (support): from its elbow as the clip has
      // it to the handguard's grip.
      const sL = pos[B.Shoulder_L]!;
      const eL = pos[B.Elbow_L]!;
      const supports = this.fore > 0 && !this.pistol;
      const depth = (w: { tip: number; yaw: number }) => {
        this.gunQ.copy(q0).multiply(rot(fq1, X, w.tip)).multiply(rot(fq2, Y, w.yaw));
        this.gunP.copy(GRIP).applyQuaternion(this.gunQ).add(wrist);
        let worst = -1;
        for (const f of ARM_FORE_GUN) worst = Math.max(worst, this.inGun(fp.lerpVectors(e, wrist, f), this.armOut(1, false, f)));
        for (const f of ARM_UPPER_GUN) worst = Math.max(worst, this.inGun(fp.lerpVectors(s, e, f), this.armOut(1, true, f)));
        // The grip's foot below the hand out of the sleeve's cuff (the wrist bent the way the gun turns).
        for (const f of ARM_CUFF) worst = Math.max(worst, this.inGun(fp.lerpVectors(e, wrist, f), this.armOut(1, false, f), GRIP_FOOT));
        if (supports) {
          const hand = fh.set(0.012, this.foreY, this.fore).add(FORE_WRIST).applyQuaternion(this.gunQ).add(this.gunP);
          for (const f of ARM_FORE_GUN_L) worst = Math.max(worst, this.inGun(fp.lerpVectors(eL, hand, f), this.armOut(0, false, f)));
          for (const f of ARM_UPPER_GUN) worst = Math.max(worst, this.inGun(fp.lerpVectors(sL, eL, f), this.armOut(0, true, f)));
        }
        return worst;
      };
      const allowed = (w: { tip: number; yaw: number }) => Math.abs(w.tip) <= FIT_TIP * free + 1e-6 && Math.abs(w.yaw) <= FIT_YAW * free + 1e-6;
      let goal = { tip: fit.tip, yaw: fit.yaw };
      let least = depth(fit);
      if (least > TOUCH && this.waits[5]! > 0) {
        this.waits[5]!--;
        goal = { ...this.fitGoal };
      } else if (least > TOUCH) {
        let clear = false;
        for (const way of FIT_WAYS) {
          if (!allowed(way)) continue;
          const dd = depth(way) + fitCost(way) * FIT_COST;
          if (dd - fitCost(way) * FIT_COST <= TOUCH) {
            goal = way;
            clear = true;
            break;
          }
          if (dd < least) {
            least = dd;
            goal = way;
          }
        }
        Object.assign(this.fitGoal, goal);
        this.waits[5] = clear ? 0 : SEARCH_REST;
      } else if (fit.tip || fit.yaw) {
        const back = { tip: fit.tip - Math.sign(fit.tip) * Math.min(Math.abs(fit.tip), FIT_STEP / 2), yaw: fit.yaw - Math.sign(fit.yaw) * Math.min(Math.abs(fit.yaw), FIT_STEP / 2) };
        if (depth(back) <= TOUCH) goal = back;
      }
      if (!allowed(goal)) goal = { tip: THREE.MathUtils.clamp(goal.tip, -FIT_TIP * free, FIT_TIP * free), yaw: THREE.MathUtils.clamp(goal.yaw, -FIT_YAW * free, FIT_YAW * free) };
      fit.tip = this.ease(fit.tip, goal.tip);
      fit.yaw = this.ease(fit.yaw, goal.yaw);
      this.gunQ.copy(q0);
    }
    if (fit.tip || fit.yaw) {
      this.gunQ.multiply(rot(fq1, X, fit.tip)).multiply(rot(fq2, Y, fit.yaw));
      this.gunP.copy(GRIP).applyQuaternion(this.gunQ).add(this.p[B.Hand_R]!);
    }
  }

  /**
   * The weapon in the hand out of the body (at low ready across a coat or a hip, crouched over the knees): tipped up
   * about its grip and pushed ahead, the cheapest way that clears it, from the way it has; eased. Its hands go to
   * it after (holdWeapon).
   */
  private clearGun(): void {
    const span = this.gunSpan;
    const fix = this.gunFix;
    if (!span || !this.bodyAware) {
      fix.tip = fix.push = fix.lift = 0;
      return;
    }
    const ahead = gx.copy(Z).applyQuaternion(this.d[B.Spine_03]!).setY(0).normalize();
    const depth = (w: { tip: number; push: number; lift: number }) => {
      gq.copy(this.gunQ).multiply(rot(sq, X, -w.tip));
      // The hand on its grip goes where it goes (a crouch brings the knee up to it).
      gp.copy(GRIP).applyQuaternion(gq).negate().add(this.gunP).addScaledVector(ahead, w.push);
      gp.y += w.lift;
      let worst = this.inBody(gp, this.rest!.bulk!.hand);
      for (let k = 0; k < GUN_POINTS; k++) {
        for (const y of [0, span.lo, span.hi]) {
          gp.set(span.x, span.y + y, lerp(span.z0, span.z1, k / (GUN_POINTS - 1))).applyQuaternion(gq).add(this.gunP).addScaledVector(ahead, w.push);
          gp.y += w.lift;
          worst = Math.max(worst, this.inBody(gp, span.r));
        }
      }
      return worst;
    };
    let goal = { tip: fix.tip, push: fix.push, lift: fix.lift };
    let least = depth(fix);
    if (least > TOUCH && this.waits[4]! > 0) {
      this.waits[4]!--;
      goal = this.gunGoal;
    } else if (least > TOUCH) {
      let clear = false;
      for (const way of GUN_WAYS) {
        // A blade is only pushed ahead: tipped or lifted, its raise passed the hand by its own shoulder (the elbow
        // whipped round).
        if (this.melee && (way.tip || way.lift)) continue;
        const dd = depth(way);
        if (dd <= TOUCH) {
          goal = way;
          clear = true;
          break;
        }
        if (dd < least) {
          least = dd;
          goal = way;
        }
      }
      Object.assign(this.gunGoal, goal);
      this.waits[4] = clear ? 0 : SEARCH_REST;
    } else if (fix.tip || fix.push || fix.lift) {
      // Back towards how the pose holds it, as far as that stays clear.
      const back = { tip: Math.max(0, fix.tip - GUN_TIPS[1]! / 2), push: Math.max(0, fix.push - GUN_PUSHES[1]! / 2), lift: Math.max(0, fix.lift - GUN_LIFTS[1]! / 2) };
      if (depth(back) <= TOUCH) goal = back;
    }
    fix.tip = this.ease(fix.tip, goal.tip);
    fix.push = this.ease(fix.push, goal.push, CLEAR_SLIDE);
    fix.lift = this.ease(fix.lift, goal.lift, CLEAR_SLIDE);
    if (fix.tip) this.gunQ.multiply(rot(sq, X, -fix.tip));
    if (fix.push) this.gunP.addScaledVector(ahead, fix.push);
    this.gunP.y += fix.lift;
  }

  /**
   * After the support hand has gone to the weapon: if its arm is still in the body however its elbow swings, it lets
   * go, eased, and hangs at its side as an arm with nothing to hold (the clip's, or hanging), turned out of the body;
   * it takes the weapon again once it could be well clear there, and always to aim, to reload or crouched.
   */
  private letGoIf(p: FigurePose, upper: THREE.Quaternion, clip: boolean, time: number): void {
    if (!this.bodyAware) {
      this.letGo = this.slid = 0;
      return;
    }
    const pos = this.p;
    const deep = this.armIn(0, pos[B.Shoulder_L]!, pos[B.Elbow_L]!, pos[B.Hand_L]!, 1);
    // (Crouched, a hanging arm would only go into the knee.)
    const needed = p.aim > 0.3 || (p.spin ?? 0) > 0 || p.crouch > 0.3;
    const goal = needed ? 0 : deep > LET_GO_DEEP ? 1 : deep < -LET_GO_SPARE ? 0 : this.letGo;
    const step = (now: number, to: number) => (this.dt > 0 ? now + THREE.MathUtils.clamp(to - now, -LET_GO_EASE * this.dt, LET_GO_EASE * this.dt) : to);
    this.letGo = step(this.letGo, goal);
    // Aimed: slid back along the handguard while the arm is in the body, forward again once it is well clear.
    const slide = p.aim > 0.3 ? (deep > TOUCH ? 1 : deep < -LET_GO_SPARE ? 0 : this.slid) : 0;
    this.slid = step(this.slid, slide);
    if (this.letGo <= 0) return;
    // The arm on the weapon kept, the free arm made, and the one blended into the other.
    const d = this.d;
    for (let i = 0; i < ARM_L.length; i++) this.held[i]!.copy(d[ARM_L[i]!]!);
    if (clip) this.followArm('L', upper);
    else this.hang('L', p.run * Math.sin(time * 11 + p.phase) * 0.4);
    this.spreadArm('L');
    const k = THREE.MathUtils.smoothstep(this.letGo, 0, 1);
    for (let i = 0; i < ARM_L.length; i++) d[ARM_L[i]!]!.slerp(this.held[i]!, 1 - k);
    this.fk(B.Elbow_L);
    this.fk(B.Hand_L);
  }

  /**
   * A weapon at rest on the body (slung on the back, in its holster, in its sheath) out of it: moved away from the body
   * (STOW_AWAY) as far as it takes, at rest, where it rides (stowRest). Once each time it is put there.
   */
  private outOfBody(k: Carry, m: THREE.Object3D): void {
    const place = this.stowRest[k];
    const parts = this.rest?.bulk?.parts;
    const span = m.userData.span as Span | undefined;
    if (!place || !parts || !span || !SYNTY_HANDS.elbows) return;
    const away = STOW_AWAY[k].clone().applyMatrix3(new THREE.Matrix3().setFromMatrix4(place).transpose());
    const start = m.position.clone();
    const point = new THREE.Vector3();
    for (let t = 0; t <= 0.2; t += 0.01) {
      let worst = -1;
      for (let i = 0; i < GUN_POINTS; i++) {
        for (const y of [0, span.lo, span.hi]) {
          point.set(span.x, span.y + y, lerp(span.z0, span.z1, i / (GUN_POINTS - 1))).add(start).addScaledVector(away, t).applyMatrix4(place);
          for (const g of parts) worst = Math.max(worst, depthIn(g, point, span.r));
        }
      }
      m.position.copy(start).addScaledVector(away, t);
      if (worst <= TOUCH) return;
    }
  }

  /** An arm hanging at the side, swinging by `swing`. */
  private hang(side: 'L' | 'R', swing: number): void {
    const s = side === 'L' ? 1 : -1;
    this.setArm(side, tmpU.set(s * 0.16, -1, 0.06 + swing).normalize(), tmpF.set(s * 0.06, -1, 0.35 + swing).normalize());
  }

  /** Overhead throw with the free (left) arm: back and up in the wind-up, forward at the release. */
  /**
   * A stone tossed underhand with the left hand, the gun kept in the right
   * (docs/FASE10.md §4: short and one-handed, unlike the grenade's overhand):
   * the arm back and low, then swung through to ahead at shoulder height
   * (the release about 0.6 in); blended in and out of what the arm was doing.
   */
  private tossArm(s: number): void {
    const d = this.d;
    for (let i = 0; i < ARM_L.length; i++) this.held[i]!.copy(d[ARM_L[i]!]!);
    const back = Math.min(1, s / 0.35);
    const through = THREE.MathUtils.clamp((s - 0.35) / 0.35, 0, 1);
    const upper = tmpU.set(0.12, -1, 0.05).lerp(v3.set(0.18, -0.85, -0.5), back).lerp(v4.set(0.1, -0.15, 0.98), through).normalize();
    const fore = tmpF.set(0.1, -1, 0.1).lerp(v3.set(0.15, -0.8, -0.55), back).lerp(v4.set(0.05, 0.05, 1), through).normalize();
    this.setArm('L', upper, fore);
    const w = Math.min(1, s / 0.12, (1 - s) / 0.2);
    for (let i = 0; i < ARM_L.length; i++) d[ARM_L[i]!]!.slerp(this.held[i]!, 1 - w);
  }

  private throwArm(lean: number): void {
    const t = THREE.MathUtils.clamp((lean + 0.35) / 0.65, 0, 1);
    this.setArm(
      'L',
      tmpU.set(0.2, 0.75, -0.6).lerp(v3.set(0.15, 0.4, 0.9), t).normalize(),
      tmpF.set(0.1, 0.95, -0.25).lerp(v3.set(0.1, 0.25, 0.95), t).normalize(),
    );
  }

  /** Model-space turns to bone-local rotations; the hips also move. */
  /**
   * The fingers of both hands as each is busy now (HAND_POSES): round the gun's grip with the index along the
   * trigger, cradling the handguard, closed on a blade's handle, a rung or a stone, open to take something, a dead
   * one's claws, else at ease; eased to it, over the hand where the arm left it.
   */
  private hands(p: FigurePose, dt: number, live: number, climb: number, search: number, throwing: boolean, toss: number): void {
    const r = this.rest!;
    if (!r.fingers.length) return;
    const dead = this.spec.dead;
    const fallen = live < 0.5;
    const right: HandPose = fallen ? (this.armed ? 'grip' : 'relaxed') : dead ? 'claw' : this.armed ? (this.melee ? 'grip' : 'trigger') : climb > 0.5 ? 'grip' : search > 0.3 ? 'open' : 'relaxed';
    // The support hand closed under a rifle's handguard, round the right on a pistol's grip (22/10 #3: open, they held
    // nothing); a pistol at rest is in the right hand alone.
    const supports = this.armed && this.fore && this.letGo < 0.5 && this.pistolRest < 0.5;
    const left: HandPose = fallen ? 'relaxed' : dead ? 'claw' : toss > 0 || throwing ? 'fist' : climb > 0.5 || (p.spin ?? 0) > 0.01 ? 'grip' : search > 0.3 ? 'open' : supports ? (this.pistol ? 'support' : 'grip') : 'relaxed';
    const k = dt > 0 ? 1 - Math.exp(-dt * HAND_EASE) : 1;
    const d = this.d;
    for (const chain of r.fingers) {
      // (Off in the weapons lab: straight, as the hand would be without them.)
      const goal = SYNTY_HANDS.fingers ? HAND_POSES[chain.side === 'L' ? left : right][chain.kind] : FLAT;
      const hand = chain.side === 'L' ? B.Hand_L : B.Hand_R;
      // The hand as it ended up, then each joint out from it: its rest turn from the one before, and its curl.
      fq.copy(d[hand]!).multiply(r.q[hand]!);
      let before = r.q[hand]!;
      for (let n = 0; n < chain.joints.length; n++) {
        const j = chain.joints[n]!;
        this.curl[j] = this.curl[j]! + (goal[n]! - this.curl[j]!) * k;
        lq.copy(before).invert().multiply(r.q[j]!);
        fq.multiply(lq).multiply(cq.setFromAxisAngle(chain.axes[n]!, this.curl[j]!));
        d[j]!.copy(fq).multiply(iq.copy(r.q[j]!).invert());
        before = r.q[j]!;
      }
    }
  }

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
      if (i !== B.Hips) bone.updateMatrix();
    }
    const hips = bones[B.Hips];
    if (hips) {
      hips.position.copy(this.p[B.Hips]!).sub(r.rootP).applyQuaternion(r.rootQ);
      hips.updateMatrix();
    }
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

/**
 * A sabotage's objective as the battle has it: its alarm, and where a car's nose points (radians about y; 0 along
 * +z); how many tiles long the car is, counting the siren's own (2 before docs/MAPAS.md phase 1: 3 a patrol car, 4
 * an ambulance), and how far its middle lies to its right of the siren's tile (an ambulance is two tiles wide).
 */
export interface Siren {
  alarm: Alarm;
  heading: number;
  length?: number;
  side?: number;
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
 * a patrol car or an ambulance with its lights going round, over the tiles
 * the map blocks for it (the unit on a back tile, the rest `sirenCar` and its
 * parts: two tiles long before, three or four now, at its size beside a
 * survivor, FOOTPRINT_FIT), or a factory's motor siren on a pole with its beacon. Each
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
  /** Its longest side, in tiles (a car's: as long as its tiles let it be). */
  private readonly fit: number;

  constructor(alarm: Alarm, heading: number, length = 2, side = 0) {
    const look = (this.look = SIRENS[alarm]);
    const car = !look.pole;
    this.fit = car && length > 2 && look.prop ? Math.min(FOOTPRINT_FIT[look.prop]?.fit ?? look.fit, length - 0.05) : look.fit;
    this.root.add(this.yaw);
    this.yaw.add(this.pivot);
    this.yaw.rotation.y = heading;
    this.pivot.add(this.gunPivot, this.body);
    this.gunPivot.add(this.muzzle);
    // Over the middle of its tiles: ahead of the siren's tile, and to one side for one two wide.
    if (car) this.body.position.set(side, 0, (length - 1) / 2);
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
    const k = this.fit / Math.max(size.x, size.z);
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

  /** Its lights, on its top at `height` (on a car bigger than the two tiles it had, as far apart as it is bigger). */
  private addLamps(height: number): void {
    const k = this.look.fit ? this.fit / this.look.fit : 1;
    for (const l of this.look.lights) {
      const group = new THREE.Group();
      group.position.set(l.x * k, height + l.y, l.z * k);
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

/** Where the turret's parts sit on one another (game units, as converted), and how much it is scaled to its tile. */
const TURRET = { swivel: 1.0 / 1.5, gun: 1.32 / 1.5, drum: [0.1 / 1.5, 1.62 / 1.5] as const, muzzle: 1.23 / 1.5, scale: 0.78 };

/**
 * The base's turret (docs/FASE12.md §11.4), a unit of its own: Apocalypse's
 * AA gun on its tripod, the gun turning on the swivel to where it aims and
 * kicking back as it fires; knocked over once it is destroyed. Its muzzle is
 * where the unit views' shots start.
 */
class TurretRig implements FigureRig {
  readonly root = new THREE.Group();
  readonly yaw = new THREE.Group();
  readonly pivot = new THREE.Group();
  readonly gunPivot = new THREE.Group();
  readonly muzzle = new THREE.Object3D();
  readonly flash: THREE.Mesh;
  readonly hip = 0;
  readonly chest = TURRET.gun * TURRET.scale;
  readonly top = (TURRET.drum[1] + 0.2) * TURRET.scale;
  /** The tripod, which stays put; the swivel and the gun go round with `yaw`. */
  private readonly stand = new THREE.Group();

  constructor() {
    const k = TURRET.scale;
    this.root.add(this.stand, this.yaw);
    this.stand.scale.setScalar(k);
    this.yaw.add(this.pivot);
    this.pivot.scale.setScalar(k);
    this.pivot.add(this.gunPivot);
    this.gunPivot.position.y = TURRET.gun;
    // The gun's barrels run along the model's +x: turned to +z, the way a unit faces.
    this.gunPivot.rotation.y = -Math.PI / 2;
    this.muzzle.position.set(TURRET.muzzle, 0, 0);
    this.gunPivot.add(this.muzzle);
    this.flash = new THREE.Mesh(
      new THREE.BoxGeometry(1.1, 1.3, 1.1).translate(0, 0.65, 0),
      new THREE.MeshBasicMaterial({ color: 0xff3a2a, transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending }),
    );
    this.flash.visible = false;
    this.root.add(this.flash);
    this.part('turretTripod', this.stand, 0, 0);
    this.part('turretSwivel', this.pivot, 0, TURRET.swivel);
    this.part('turretGun', this.gunPivot, 0, 0);
    this.part('turretDrum', this.gunPivot, TURRET.drum[0], TURRET.drum[1] - TURRET.gun);
    onUnitLayer(this.root);
  }

  /** One of its parts into `into`, at (x, y) there, as soon as its model is in. */
  private part(id: 'turretTripod' | 'turretSwivel' | 'turretGun' | 'turretDrum', into: THREE.Group, x: number, y: number): void {
    const put = (source: THREE.Group) => {
      const model = source.clone();
      model.position.set(x, y, 0);
      model.traverse((o) => {
        if (o instanceof THREE.Mesh) o.castShadow = o.receiveShadow = true;
      });
      into.add(model);
      onUnitLayer(this.root);
    };
    const now = modelNow(PROPS[id].pack, 'prop', id);
    if (now) put(now);
    else void loadModel(PROPS[id].pack, 'prop', id).then(put).catch(() => undefined);
  }

  applyPose(p: FigurePose, _time: number): void {
    this.yaw.rotation.y = p.yaw;
    // The kick of a burst, the barrels up a touch as it aims.
    this.gunPivot.position.x = -p.recoil * 0.09;
    this.gunPivot.rotation.z = -p.aim * 0.06 + p.recoil * 0.05;
    // Destroyed: knocked over on its side, the gun off its swivel.
    this.root.rotation.z = p.fall * 1.25;
    this.root.position.y = -p.fall * 0.05;
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
  if (template === 'relay') return new SirenRig(siren?.alarm ?? 'factory', siren?.heading ?? 0, siren?.length, siren?.side);
  if (template === 'turret') return new TurretRig();
  // One of the dead with a look of its own is a soldier risen (playtest 16/10 #7: Unit.risenFrom): their body,
  // clothes and helmet as they fell, the skin gone grey, the dead's walk; no band, no weapons.
  const risen = DEAD_TEMPLATES.has(template) && !!look;
  const squad = SQUAD.has(template) || risen;
  // Without a look of its own (older saves), the plain one: the class no longer picks it (docs/FASE9.md).
  const head: HeadId = look?.head ?? 'helmet';
  const key = seed ?? `${template}|${accent}|${JSON.stringify(look ?? null)}`;
  // A soldier (or one risen) from the wardrobe: their clothes (the barracks' «Personaje», else picked by their
  // campaign id, the same everywhere), what is on their head and their build by their look; their face and hair by
  // their id.
  // (Who is rescued too, in a civilian's winter clothes.)
  const dress = squad ? dressOf(look, key, look?.sex ?? sex, { risen }) : template === 'vip' ? dressOf(undefined, key, sex, { pool: CIVIL_OUTFITS, head: 'bare' }) : undefined;
  const bodies = dress ? null : sameSex(UNIT_BODIES[template], look?.sex ?? sex);
  if (!dress && !bodies?.length) return null;
  const character = bodies ? pick(bodies, key) : undefined;
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
    dress,
    ...arms,
    dyes,
    band: (squad && !risen) || template === 'vip' ? accent : null,
    dead: DEAD_TEMPLATES.has(template),
    // (A risen soldier's grey skin is in their palette.)
    ...(risen ? { own: true } : {}),
    scream: template === 'sectoid',
    style: gaitsOf(template).style,
    stance: DEAD_TEMPLATES.has(template) ? 'drunk' : LIVING_ENEMIES.has(template) ? 'aggressive' : undefined,
    // (Each the same everywhere, by who they are.)
    rest: pick(RIFLE_RESTS, `${key}|rest`),
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
 * Armería's weapon in the hand, or the pistol without one, with what is
 * mounted on it and its wear; the pistol in its holster beside a main gun;
 * the machete or the knife at the belt. Anyone
 * else, its template's: its weapon by tier, the Francotirador's pistol beside
 * it, the Asalto's machete.
 */
export function armamentOf(template: TemplateId, tier: Tier, arms?: Arms | null): Armament {
  const of = (kind: FoundWeaponId) => foundModel(kind, tier);
  if (arms && SQUAD.has(template)) {
    const fit = arms.primary ? { mods: arms.mods ?? [], condition: arms.condition } : {};
    return { weapon: arms.primary ? of(arms.primary) : 'pistol', sidearm: arms.primary ? 'pistol' : null, belt: arms.melee ? of(arms.melee) : 'knife', ...fit };
  }
  const t = TEMPLATES[template];
  return {
    weapon: UNIT_WEAPONS[template]?.[tier - 1] ?? null,
    sidearm: t.secondary === 'pistol' && t.weapon !== 'pistol' ? 'pistol' : null,
    belt: SQUAD.has(template) && t.meleeWeapon === 'sword' ? 'machete' : null,
  };
}

/** The bodies someone picked at random can have: those of their sex in the list, or the whole list (none of it, or their sex unknown). */
function sameSex(bodies: CharacterId[] | undefined, sex: 'f' | 'm' | undefined): CharacterId[] | undefined {
  if (!sex || !bodies) return bodies;
  const kept = bodies.filter((id) => !!(CHARACTERS[id] as CharacterAsset).female === (sex === 'f'));
  return kept.length ? kept : bodies;
}

/**
 * A radio voice for its portrait (strategy/portraits.ts): unarmed, in their
 * own clothes, with the helmet, torso, arms and legs colours given (null keeps
 * a piece as Synty painted it) and a skin tone of their own if given.
 */
export function voiceFigure(character: CharacterId, dyes: (string | null)[] | null = null, skin?: string): SyntyFigure {
  return new SyntyFigure({ character, weapon: null, dyes, band: null, dead: false, scream: false, skin });
}

/** Starts loading every model and atlas the units use, so the first battle does not wait. */
export function preloadSynty(): void {
  const used = new Set<CharacterId>(Object.values(UNIT_BODIES).flat());
  for (const id of used) void loadModel(CHARACTERS[id].pack, 'chr', id).catch(() => undefined);
  preloadWardrobe();
  for (const [id, w] of Object.entries(WEAPONS)) void loadModel(w.pack, 'wep', id).catch(() => undefined);
  for (const { prop } of Object.values(SIRENS)) if (prop) void loadModel(PROPS[prop].pack, 'prop', prop).catch(() => undefined);
  for (const build of ['masc', 'femn'] as const) {
    animSet(build);
    animSet(build, true);
  }
  atlasMaterial('apocalypse');
}
