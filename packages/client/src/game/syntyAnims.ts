import * as THREE from 'three';
import { armedPace } from './gaits';
import { loadAnimation } from './synty';
import { ACTIONS, ANIMATIONS, ARMED_MOTIONS, CAMP_LOOPS, CAMP_MOVES, CAMP_PARTS, CAMP_SPANS, CAMP_VARIATIONS, DEAD_MOTIONS, DEATHS, FEED_CLIPS, GESTURE_ORDER, GESTURES, HITS, IDLE_VARIATIONS, MOTIONS, STANCE_ORDER, STANCES, SYNTY_BONES, type ActionId, type AnimationId, type ArmedKind, type Gesture, type Motion } from './syntyManifest';

// Locomotion from «ANIMATION – Base Locomotion» on any Synty character. Each
// unit plays the clips on its own proxy of the pack's skeleton (one mixer,
// the clips shared) and reads back, bone by bone, how far each has turned in
// model space from the pack's T-pose. The figure puts that same turn on its
// own bones' T-pose, so the clips fit both Synty rigs (Apocalypse and City
// Zombies) whatever their bone axes, and the procedural layers (weapon,
// lean, hits, the dead's reach) go on top in the same terms. A unit standing
// idle now and then plays a variation from «ANIMATION – Idles» instead: it
// looks round or shifts its weight; some stand in a stance of their own
// instead (STANCES: the dead sway, a wounded soldier slumps). The dead walk
// and run with Goblin Locomotion's clips (DEAD_MOTIONS). A hit plays one of
// «Bow Combat»'s and «Sword Combat»'s reactions (HITS) over the upper body;
// a unit that dies, one of their deaths (DEATHS) alone, posed at the moment
// of the fall the unit view says.

const N = SYNTY_BONES.length;
/** The bones the clips pose (the body's, ahead of the fingers in SYNTY_BONES); the fingers are each hand's (syntyFigures.ts hands). */
const BODY = SYNTY_BONES.indexOf('Thumb_01_L');
const FINGER = /^(Thumb|Index|Finger)_0\d_[LR]$/;

/** A clip without its finger tracks: the figure poses the fingers by what each hand does, and the mixer is spared them. */
function bodyOnly(clip: THREE.AnimationClip): THREE.AnimationClip {
  return new THREE.AnimationClip(clip.name, clip.duration, clip.tracks.filter((t) => !FINGER.test(t.name.split('.')[0]!)));
}
const HIPS = SYNTY_BONES.indexOf('Hips');
/** The bones a hit reaction moves: from the spine up (the legs and hips keep what they do). */
const UPPER = new Set(['Spine_01', 'Spine_02', 'Spine_03', 'Neck', 'Head', 'Clavicle_L', 'Clavicle_R', 'Shoulder_L', 'Shoulder_R', 'Elbow_L', 'Elbow_R', 'Hand_L', 'Hand_R']);
/** The neck and head: what looks round over a weapon's clip (looks). */
const HEAD = new Set(['Neck', 'Head']);
/** The upper body but the gun arm (a throw's clip: the right hand keeps the gun). */
const FREE = new Set([...UPPER].filter((b) => !b.endsWith('_R')));
/** The gun arm alone (the pistol at rest by the thigh: the rest of the body stands as it would without it). */
const GUN_ARM = new Set(['Clavicle_R', 'Shoulder_R', 'Elbow_R', 'Hand_R']);
/** Seconds a death takes to blend in from the pose the unit had. */
const DEATH_BLEND = 0.2;

/** The clips a figure blends, in the order of its weights. */
export const MOTION_ORDER: readonly Motion[] = ['idle', 'crouchIdle', 'walk', 'run', 'crouchWalk', 'sprint'];

export type Build = 'masc' | 'femn';

/** One build's clips and the T-pose they are measured from, shared by every unit that moves with them. */
export interface AnimSet {
  /** The pack's skeleton, cloned as each unit's proxy. */
  template: THREE.Object3D;
  clips: THREE.AnimationClip[];
  /** Played once now and then in place of the idle. */
  variations: THREE.AnimationClip[];
  /** Stood in instead of the idle (STANCE_ORDER). */
  stances: THREE.AnimationClip[];
  /** DEATHS, in order, and the seconds each takes until the body lies still. */
  deaths: THREE.AnimationClip[];
  settles: number[];
  /** HITS, in order, their upper body only. */
  hits: THREE.AnimationClip[];
  /**
   * ACTIONS, in order (the living's; none for the dead): whole; their upper body only (over the legs' clips); and that
   * without the right arm (a throw: the gun arm keeps the gun); and the right arm alone.
   */
  actions: THREE.AnimationClip[];
  actionsUpper: THREE.AnimationClip[];
  actionsFree: THREE.AnimationClip[];
  actionsArm: THREE.AnimationClip[];
  /** GESTURE_ORDER, in order (the living's), their upper body only: played once when asked, over the stance. */
  gestures: THREE.AnimationClip[];
  /** The variations that look round, the neck and head only: played now and then over a weapon's clip at rest. */
  looks: THREE.AnimationClip[];
  /**
   * Moving with a gun in the hands (ARMED_MOTIONS), in MOTION_ORDER: each motion's clip of its own (null: Synty's), how
   * much faster than Synty's it plays (armedPace), and whether the arms are the moving clip's (its own or Synty's).
   */
  armed: Record<ArmedKind, { clips: (THREE.AnimationClip | null)[]; pace: number[]; arms: boolean[] }>;
  /** Inverse model-space rotation of each bone in the T-pose. */
  restInv: THREE.Quaternion[];
  /** Each bone's parent among SYNTY_BONES (−1: the hips, under the fixed root). */
  parent: number[];
  rootQ: THREE.Quaternion;
  rootM: THREE.Matrix4;
  /** The hips in the T-pose. */
  hips: THREE.Vector3;
}

/**
 * One of the weapon's actions as AnimDriver.play takes it: which, how far in, its share of the body: whole, upper, that
 * but the gun arm ('free'), or the gun arm alone ('arm').
 */
export interface ActionPlay {
  id: ActionId;
  time: number;
  share: number;
  upper: boolean | 'free' | 'arm';
}

/** A build's clips, the living's or the dead's. */
type Kit = Build | `${Build}Dead`;

const ready = new Map<Kit, AnimSet>();
const loading = new Map<Kit, Promise<AnimSet>>();

function prepare(
  tpose: THREE.Group,
  poseClip: THREE.AnimationClip,
  motionClips: THREE.AnimationClip[],
  variations: THREE.AnimationClip[],
  stances: THREE.AnimationClip[],
  deaths: THREE.AnimationClip[],
  hits: THREE.AnimationClip[],
  actions: THREE.AnimationClip[],
  gestures: THREE.AnimationClip[],
  looking: boolean[],
  armedClips: Map<AnimationId, THREE.AnimationClip>,
): AnimSet {
  const proxy = tpose.clone();
  const mixer = new THREE.AnimationMixer(proxy);
  mixer.clipAction(poseClip).play();
  mixer.update(0);
  proxy.updateMatrixWorld(true);
  const bones = SYNTY_BONES.map((n) => proxy.getObjectByName(n));
  const names = SYNTY_BONES as readonly string[];
  const parent = bones.map((b) => {
    for (let o = b?.parent; o; o = o.parent) if (names.includes(o.name)) return names.indexOf(o.name);
    return -1;
  });
  const hips = bones[HIPS]!;
  const root = hips.parent!;
  mixer.stopAllAction();
  return {
    template: tpose,
    clips: motionClips.map(bodyOnly),
    variations: variations.map(bodyOnly),
    stances: stances.map(bodyOnly),
    deaths: deaths.map(bodyOnly),
    settles: deaths.map(settleOf),
    hits: hits.map((c) => new THREE.AnimationClip(c.name, c.duration, c.tracks.filter((t) => UPPER.has(t.name.split('.')[0]!)))),
    actions: actions.map(bodyOnly),
    actionsUpper: actions.map((c) => new THREE.AnimationClip(c.name, c.duration, c.tracks.filter((t) => UPPER.has(t.name.split('.')[0]!)))),
    actionsFree: actions.map((c) => new THREE.AnimationClip(c.name, c.duration, c.tracks.filter((t) => FREE.has(t.name.split('.')[0]!)))),
    actionsArm: actions.map((c) => new THREE.AnimationClip(c.name, c.duration, c.tracks.filter((t) => GUN_ARM.has(t.name.split('.')[0]!)))),
    gestures: gestures.map((c) => new THREE.AnimationClip(c.name, c.duration, c.tracks.filter((t) => UPPER.has(t.name.split('.')[0]!)))),
    looks: variations.filter((_, i) => looking[i]).map((c) => new THREE.AnimationClip(c.name, c.duration, c.tracks.filter((t) => HEAD.has(t.name.split('.')[0]!)))),
    armed: armedOf(armedClips),
    restInv: bones.map((b) => (b ? b.getWorldQuaternion(new THREE.Quaternion()).invert() : new THREE.Quaternion())),
    parent,
    rootQ: root.getWorldQuaternion(new THREE.Quaternion()),
    rootM: root.matrixWorld.clone(),
    hips: hips.getWorldPosition(new THREE.Vector3()),
  };
}

const ARMED_KINDS = Object.keys(ARMED_MOTIONS) as ArmedKind[];
/** Every clip ARMED_MOTIONS names (not `own`). */
const ARMED_IDS = [...new Set(ARMED_KINDS.flatMap((k) => Object.values(ARMED_MOTIONS[k]).filter((id): id is AnimationId => id !== 'own')))];

function armedOf(clips: Map<AnimationId, THREE.AnimationClip>): AnimSet['armed'] {
  const out = {} as AnimSet['armed'];
  for (const kind of ARMED_KINDS) {
    const of = MOTION_ORDER.map((m) => ARMED_MOTIONS[kind][m]);
    out[kind] = {
      // (The dead load none of these: their sets had none, and threw here, so their clips never came: 0.9.5.)
      clips: of.map((id) => {
        const clip = id && id !== 'own' ? clips.get(id) : undefined;
        return clip ? bodyOnly(clip) : null;
      }),
      pace: of.map((id, k) => (id && id !== 'own' && k > 1 ? armedPace(MOTION_ORDER[k] as Exclude<Motion, 'idle' | 'crouchIdle'>, id) : 1)),
      arms: of.map((id) => !!id),
    };
  }
  return out;
}

/** When a death's hips stop moving (under 0.3 % of their height a frame), and a beat to lie still. */
function settleOf(clip: THREE.AnimationClip): number {
  const track = clip.tracks.find((t) => t.name === 'Hips.position');
  if (!track) return clip.duration;
  const { times, values } = track;
  // In the root bone's frame, whatever its axes: the hips' distance from it is their height.
  const still = Math.hypot(values[0]!, values[1]!, values[2]!) * 0.003;
  let last = 0;
  for (let i = 1; i < times.length; i++) {
    const dx = values[i * 3]! - values[i * 3 - 3]!;
    const dy = values[i * 3 + 1]! - values[i * 3 - 2]!;
    const dz = values[i * 3 + 2]! - values[i * 3 - 1]!;
    if (Math.hypot(dx, dy, dz) > still) last = times[i]!;
  }
  return Math.min(clip.duration, last + 0.12);
}

/** A build's clips (`dead`: the dead's) once loaded; null meanwhile (asking starts the load). */
export function animSet(build: Build, dead = false): AnimSet | null {
  const set = ready.get(dead ? `${build}Dead` : build);
  if (!set) void whenAnimSet(build, dead).catch(() => undefined);
  return set ?? null;
}

export function whenAnimSet(build: Build, dead = false): Promise<AnimSet> {
  const kit: Kit = dead ? `${build}Dead` : build;
  let p = loading.get(kit);
  if (!p) {
    const variations = IDLE_VARIATIONS[build];
    const motions = MOTION_ORDER.map((m) => (dead && DEAD_MOTIONS[m]) || MOTIONS[build][m]);
    const actions = dead ? [] : ACTIONS;
    const gestures = dead ? [] : GESTURE_ORDER.map((g) => GESTURES[build][g]);
    const armed = dead ? [] : ARMED_IDS;
    const ids = [...motions, ...variations, ...STANCE_ORDER.map((s) => STANCES[build][s]), ...DEATHS[build].map((d) => d.id), ...HITS[build].map((h) => h.id), ...actions, ...gestures, ...armed];
    p = Promise.all([loadAnimation(ANIMATIONS.tpose.pack, 'tpose'), ...ids.map((id) => loadAnimation(ANIMATIONS[id].pack, id))]).then(([pose, ...loaded]) => {
      const clips = loaded.map((c) => c.clip);
      const at = [MOTION_ORDER.length, MOTION_ORDER.length + variations.length, MOTION_ORDER.length + variations.length + STANCE_ORDER.length];
      at.push(at[2]! + DEATHS[build].length);
      at.push(at[3]! + HITS[build].length);
      const acted = at[4]! + actions.length;
      const gestured = acted + gestures.length;
      const armedClips = new Map(armed.map((id, i) => [id, clips[gestured + i]!] as const));
      const set = prepare(pose!.scene, pose!.clip, clips.slice(0, at[0]), clips.slice(at[0], at[1]), clips.slice(at[1], at[2]), clips.slice(at[2], at[3]), clips.slice(at[3], at[4]), clips.slice(at[4], acted), clips.slice(acted, gestured), variations.map((id) => id.startsWith('look')), armedClips);
      ready.set(kit, set);
      return set;
    });
    loading.set(kit, p);
    // The dead's feeding, ready by the first kill (22/10 #21).
    if (dead) void whenFeedClips(build).catch(() => undefined);
  }
  return p;
}

/**
 * The camp's clips (CAMP_LOOPS, CAMP_VARIATIONS, CAMP_MOVES), a build's by id (and the parts of those that are more
 * than one thing: CAMP_PARTS); loaded apart, when first asked for (the Asentamiento only).
 */
const campReady = new Map<Build, Map<string, THREE.AnimationClip>>();
const campLoading = new Map<Build, Promise<Map<string, THREE.AnimationClip>>>();
/** The camp loops played back and forth (a part whose ends meet only so). */
const pingPong = new WeakSet<THREE.AnimationClip>();

/** `clip` from `from` to `to` seconds, its ends sampled where they fall between keys. */
function cut(clip: THREE.AnimationClip, from: number, to: number, name: string): THREE.AnimationClip {
  const tracks = clip.tracks.map((t) => {
    // (Each track's own interpolant: a quaternion's slerps. It is set at runtime, not in three's types.)
    const at = (t as unknown as { createInterpolant(): THREE.Interpolant }).createInterpolant();
    const times = [from, ...Array.from(t.times).filter((x) => x > from + 1e-4 && x < to - 1e-4), to];
    const size = t.getValueSize();
    const values = new Float32Array(times.length * size);
    times.forEach((x, i) => values.set(at.evaluate(x) as ArrayLike<number>, i * size));
    const Track = t.constructor as new (name: string, times: number[], values: Float32Array) => THREE.KeyframeTrack;
    return new Track(t.name, times.map((x) => x - from), values);
  });
  return new THREE.AnimationClip(name, to - from, tracks);
}

/** A build's camp clips once loaded; null meanwhile (asking starts the load). */
export function campClips(build: Build): Map<string, THREE.AnimationClip> | null {
  const got = campReady.get(build);
  if (!got) void whenCampClips(build).catch(() => undefined);
  return got ?? null;
}

export function whenCampClips(build: Build): Promise<Map<string, THREE.AnimationClip>> {
  let p = campLoading.get(build);
  if (!p) {
    const ids = [...new Set<AnimationId>([...Object.values(CAMP_LOOPS[build]), ...CAMP_VARIATIONS[build], ...Object.values(CAMP_MOVES[build]).filter((id): id is AnimationId => id in ANIMATIONS)])];
    p = Promise.all(ids.map((id) => loadAnimation(ANIMATIONS[id].pack, id))).then((loaded) => {
      const out = new Map<string, THREE.AnimationClip>(ids.map((id, i) => [id, bodyOnly(loaded[i]!.clip)] as const));
      for (const [id, parts] of Object.entries(CAMP_PARTS)) {
        const whole = out.get(id);
        if (!whole || !parts) continue;
        const loop = cut(whole, ...parts.loop, id);
        pingPong.add(loop);
        out.set(id, loop);
        out.set(`${id}In`, cut(whole, ...parts.into, `${id}In`));
        out.set(`${id}Out`, cut(whole, ...parts.out, `${id}Out`));
      }
      for (const [id, span] of Object.entries(CAMP_SPANS)) {
        const whole = out.get(id);
        if (whole && span) out.set(id, cut(whole, ...span, id));
      }
      campReady.set(build, out);
      return out;
    });
    campLoading.set(build, p);
  }
  return p;
}

/** The dead's feeding (FEED_CLIPS): the bite that kills, down on the knees, the eating stayed in (looped) and up again. */
export type FeedClips = Record<keyof typeof FEED_CLIPS, THREE.AnimationClip>;

const feedLoading = new Map<Build, Promise<FeedClips>>();
const feedReady = new Map<Build, FeedClips>();

/** A build's feeding clips once loaded; null meanwhile (asking starts the load: the dead's clips ask at once). */
export function feedClips(build: Build): FeedClips | null {
  const got = feedReady.get(build);
  if (!got) void whenFeedClips(build).catch(() => undefined);
  return got ?? null;
}

export function whenFeedClips(build: Build): Promise<FeedClips> {
  let p = feedLoading.get(build);
  if (!p) {
    const parts = Object.entries(FEED_CLIPS) as [keyof FeedClips, readonly [AnimationId, number, number]][];
    p = Promise.all(parts.map(([, [id]]) => loadAnimation(ANIMATIONS[id].pack, id))).then((loaded) => {
      const set = {} as FeedClips;
      parts.forEach(([part, [id, from, to]], k) => (set[part] = cut(bodyOnly(loaded[k]!.clip), from, to, `${id}:${part}`)));
      feedReady.set(build, set);
      return set;
    });
    feedLoading.set(build, p);
  }
  return p;
}

const v = new THREE.Vector3();
/** Seconds a variation takes to blend in and out, and to wait between two. */
const BLEND = 0.35;
/** Seconds a variation takes to go once the unit is no longer calm (it aims, it is hit, it searches). */
const CUT = 0.2;
/** Seconds the idle takes to ease from one stance to another. */
const STANCE_BLEND = 0.6;
/** Seconds moving with a gun in the hands takes to come in as it is drawn, or to go. */
const ARMED_BLEND = 0.3;
const nextWait = () => 5 + Math.random() * 9;

/** One unit's locomotion: a proxy skeleton and its mixer. */
export class AnimDriver {
  private readonly mixer: THREE.AnimationMixer;
  private readonly actions: THREE.AnimationAction[];
  private readonly variations: THREE.AnimationAction[];
  private variation: THREE.AnimationAction | null = null;
  /**
   * How much of the variation playing is still let in: none, quickly, once the unit is no longer calm. It played to
   * its end whatever came: a soldier shot with half her body turned to look behind her (playtest 21/10 #9).
   */
  private keep = 1;
  private readonly stances: THREE.AnimationAction[];
  /** How far each stance has taken the idle's place, easing to the one stood in. */
  private readonly held: Float32Array;
  /** The stance stood in (STANCE_ORDER); −1 the plain idle. */
  private stance = -1;
  private readonly deaths: THREE.AnimationAction[];
  private dying: THREE.AnimationAction | null = null;
  /** The death has blended in: the rest no longer count. */
  private down = false;
  private readonly hits: THREE.AnimationAction[];
  private hit: THREE.AnimationAction | null = null;
  /** The weapon's actions playing (ACTIONS), whole or over the upper body, each paused at the time it is set to. */
  private readonly acts: THREE.AnimationAction[];
  private readonly actsUpper: THREE.AnimationAction[];
  private readonly actsFree: THREE.AnimationAction[];
  private readonly actsArm: THREE.AnimationAction[];
  private readonly acting = new Set<THREE.AnimationAction>();
  /** The gestures (GESTURE_ORDER), and the one being made. */
  private readonly gestures: THREE.AnimationAction[];
  private gesturing: THREE.AnimationAction | null = null;
  private wait = nextWait();
  /** A camp clip stayed in instead of the idle (campLoop), and how far each one eased in or out has taken its place. */
  private loop: THREE.AnimationAction | null = null;
  private readonly loops = new Map<THREE.AnimationAction, number>();
  /** A camp move playing once over the rest, held at its end (campMove: sitting down, getting up), and its share. */
  private move: THREE.AnimationAction | null = null;
  private moved = 0;
  private moving = false;
  /** The move before one that followed it straight on (the bite, then down on the knees: 22/10 #21), easing out under it. */
  private before: { action: THREE.AnimationAction; share: number } | null = null;
  /** The camp's variations in the plain ones' place, while set (campVariations). */
  private camp: THREE.AnimationAction[] | null = null;
  /** A look round over a weapon's clip at rest (set.looks), and the seconds until the next. */
  private readonly looks: THREE.AnimationAction[];
  private looking: THREE.AnimationAction | null = null;
  private lookWait = nextWait();
  private readonly bones: THREE.Object3D[];
  private readonly world = Array.from({ length: N }, () => new THREE.Quaternion());
  /** Moving with a gun in the hands (set.armed): each motion's clip of its own, the gun now held, and how far each is let in. */
  private readonly armedActs: Record<ArmedKind, (THREE.AnimationAction | null)[]>;
  private armedKind: ArmedKind | null = null;
  private readonly armedHeld: Record<ArmedKind, number> = { rifle: 0, pistol: 0 };
  /** Blend weights (summing to 1) and playback speeds, in MOTION_ORDER. */
  readonly weights = new Float32Array(MOTION_ORDER.length);
  readonly speeds = new Float32Array(MOTION_ORDER.length).fill(1);

  constructor(
    readonly set: AnimSet,
    phase: number,
  ) {
    const proxy = set.template.clone();
    this.mixer = new THREE.AnimationMixer(proxy);
    this.actions = set.clips.map((clip) => {
      const a = this.mixer.clipAction(clip);
      a.play();
      a.time = phase * clip.duration;
      a.setEffectiveWeight(0);
      return a;
    });
    this.variations = set.variations.map((clip) => {
      const a = this.mixer.clipAction(clip);
      a.setLoop(THREE.LoopOnce, 1);
      a.setEffectiveWeight(0);
      return a;
    });
    this.armedActs = {} as Record<ArmedKind, (THREE.AnimationAction | null)[]>;
    for (const kind of ARMED_KINDS) {
      this.armedActs[kind] = set.armed[kind].clips.map((clip) => {
        if (!clip) return null;
        const a = this.mixer.clipAction(clip);
        a.play();
        a.time = phase * clip.duration;
        a.setEffectiveWeight(0);
        return a;
      });
    }
    // Each starts playing when first stood in, so a crowd plays only the clips it uses.
    this.stances = set.stances.map((clip) => {
      const a = this.mixer.clipAction(clip);
      a.time = phase * clip.duration;
      a.setEffectiveWeight(0);
      return a;
    });
    this.held = new Float32Array(set.stances.length);
    this.gestures = set.gestures.map((clip) => {
      const a = this.mixer.clipAction(clip);
      a.setLoop(THREE.LoopOnce, 1);
      a.setEffectiveWeight(0);
      return a;
    });
    this.looks = set.looks.map((clip) => {
      const a = this.mixer.clipAction(clip);
      a.setLoop(THREE.LoopOnce, 1);
      a.setEffectiveWeight(0);
      return a;
    });
    this.deaths = set.deaths.map((clip) => {
      const a = this.mixer.clipAction(clip);
      a.setLoop(THREE.LoopOnce, 1);
      a.clampWhenFinished = true;
      return a;
    });
    this.hits = set.hits.map((clip) => this.mixer.clipAction(clip));
    this.acts = set.actions.map((clip) => this.mixer.clipAction(clip));
    this.actsUpper = set.actionsUpper.map((clip) => this.mixer.clipAction(clip));
    this.actsFree = set.actionsFree.map((clip) => this.mixer.clipAction(clip));
    this.actsArm = set.actionsArm.map((clip) => this.mixer.clipAction(clip));
    this.bones = SYNTY_BONES.map((n) => proxy.getObjectByName(n) ?? new THREE.Object3D());
  }

  /**
   * Hit reaction `k` (set.hits) `time` seconds in, taking `share` (0 to 1)
   * of the upper body from what it does; null lets go of it.
   */
  react(k: number | null, time = 0, share = 0): void {
    const next = k === null ? null : this.hits[k]!;
    if (this.hit !== next) {
      this.hit?.stop();
      this.hit = next;
      if (next) {
        next.reset().play();
        next.paused = true;
      }
    }
    if (!next) return;
    next.time = time;
    // The mixer averages what plays by weight: against the rest's 1, this weight gives the reaction its share.
    const s = Math.min(0.95, share);
    next.setEffectiveWeight(s / (1 - s));
  }

  /**
   * The weapon's action `id` (ACTIONS) `time` seconds in (looped by the caller for one that loops), taking `share` (0
   * to 1) of what the rest does: of the whole body, or (`upper`) from the spine up only; null lets go of it.
   */
  act(id: ActionId | null, time = 0, share = 0, upper: ActionPlay['upper'] = true): void {
    this.play(id === null ? [] : [{ id, time, share, upper }]);
  }

  /**
   * The weapon's actions `plays` at once (an idle and an aim, blended), each `time` seconds in and taking its `share`
   * of the body, whole or from the spine up (the shares of all add to at most 1); the rest let go.
   */
  play(plays: readonly ActionPlay[]): void {
    let total = 0;
    for (const a of plays) total += a.share;
    total = Math.min(0.99, total);
    // Against the rest's 1, the actions' weights give them `total` of the pose between them.
    const scale = total > 0 ? total / (1 - total) / Math.max(1e-6, plays.reduce((n, a) => n + a.share, 0)) : 0;
    const now = new Set<THREE.AnimationAction>();
    for (const a of plays) {
      const k = ACTIONS.indexOf(a.id);
      const action = (a.upper === 'arm' ? this.actsArm : a.upper === 'free' ? this.actsFree : a.upper ? this.actsUpper : this.acts)[k];
      if (!action || a.share <= 0) continue;
      if (!this.acting.has(action)) {
        action.reset().play();
        action.paused = true;
      }
      now.add(action);
      action.time = Math.min(a.time, action.getClip().duration);
      action.setEffectiveWeight(a.share * scale);
    }
    for (const action of this.acting) if (!now.has(action)) action.stop();
    this.acting.clear();
    for (const action of now) this.acting.add(action);
  }

  /** Moves with gun `kind` in the hands from now on (ARMED_MOTIONS), eased in over `dt`; null, empty-handed (or a blade). */
  armedAs(kind: ArmedKind | null, dt: number): void {
    this.armedKind = kind;
    for (const k of ARMED_KINDS) this.armedHeld[k] = THREE.MathUtils.clamp(this.armedHeld[k] + (k === kind ? dt : -dt) / ARMED_BLEND, 0, 1);
  }

  /** How much of the arms the moving clips have now (set.armed `arms`), by the weights as set: the gun held as they hold it. */
  armedShare(): number {
    const kind = this.armedKind;
    if (!kind) return 0;
    const arms = this.set.armed[kind].arms;
    let share = 0;
    for (let k = 0; k < arms.length; k++) if (arms[k]) share += this.weights[k]!;
    return share * this.armedHeld[kind];
  }

  /** How long action `id` lasts. */
  actSeconds(id: ActionId): number {
    return this.set.actions[ACTIONS.indexOf(id)]?.duration ?? 0;
  }

  /** The looping clip `k` names: a moving one (MOTION_ORDER), or with −1 what is stood in (the stance, else the idle). */
  private looped(k: number): THREE.AnimationAction {
    return k >= 0 ? this.actions[k]! : (this.stances[this.stance] ?? this.actions[0]!);
  }

  /** How long looping clip `k` (see looped) lasts at its own speed. */
  seconds(k: number): number {
    return this.looped(k).getClip().duration;
  }

  /** How far through its loop clip `k` (see looped) is, 0 to 1. */
  cycle(k: number): number {
    const a = this.looped(k);
    const at = a.time / a.getClip().duration;
    return at - Math.floor(at);
  }

  /** Puts looping clip `k` (see looped) `at` (any number: its fraction) through its loop. */
  setCycle(k: number, at: number): void {
    const a = this.looped(k);
    a.time = (at - Math.floor(at)) * a.getClip().duration;
  }

  /** Makes gesture `g` once (GESTURES) over what it stands in, its upper body; one already being made goes on. Whether it began. */
  gesture(g: Gesture): boolean {
    const next = this.gestures[GESTURE_ORDER.indexOf(g)];
    if (!next || this.gesturing) return false;
    next.reset().setEffectiveWeight(0).play();
    this.gesturing = next;
    return true;
  }

  /** Whether a gesture is being made. */
  get busy(): boolean {
    return !!this.gesturing;
  }

  /** Stays in camp clip `clip` (campClips: CAMP_LOOPS) instead of the idle, or none; `now` without easing into it. */
  campLoop(clip: THREE.AnimationClip | null, now = false): void {
    const next = clip ? this.mixer.clipAction(clip) : null;
    if (next !== this.loop) {
      this.loop = next;
      if (next && !this.loops.has(next)) {
        next.reset().setEffectiveWeight(0).play();
        if (pingPong.has(clip!)) next.setLoop(THREE.LoopPingPong, Infinity);
        this.loops.set(next, 0);
      }
    }
    if (now) for (const a of this.loops.keys()) this.loops.set(a, a === next ? 1 : 0);
  }

  /**
   * Plays camp move `clip` once (CAMP_MOVES) over whatever it does, eased in, and holds its last pose until another
   * or none (eased out); how far through it is, 0 to 1 (1 also with none).
   */
  campMove(clip: THREE.AnimationClip | null, speed = 1): number {
    const next = clip ? this.mixer.clipAction(clip) : null;
    if (next && next !== this.move) {
      // One straight after another: the first eases out under the next as it comes in (no going back to the idle between).
      this.before?.action.stop();
      this.before = this.move && this.moving && this.moved > 0 ? { action: this.move, share: Math.min(0.97, this.moved) } : null;
      if (!this.before) this.move?.stop();
      next.reset().setLoop(THREE.LoopOnce, 1);
      next.clampWhenFinished = true;
      next.timeScale = speed;
      next.setEffectiveWeight(0).play();
      this.move = next;
      this.moved = 0;
    }
    this.moving = !!next;
    const m = this.move;
    return m && next ? Math.min(1, m.time / m.getClip().duration) : 1;
  }

  /** How much of the pose the camp move has now (campMove), 0 to 1. */
  get moveShare(): number {
    return this.move ? Math.min(0.97, this.moved) : 0;
  }

  /** Plays these (campClips: CAMP_VARIATIONS) now and then in the plain variations' place, or the plain ones again (null). */
  campVariations(clips: THREE.AnimationClip[] | null): void {
    this.camp = clips?.map((clip) => {
      const a = this.mixer.clipAction(clip);
      a.setLoop(THREE.LoopOnce, 1);
      return a;
    }) ?? null;
  }

  /** Stands in stance `k` (STANCE_ORDER) instead of the plain idle, or −1; `now` without easing into it. */
  setStance(k: number, now = false): void {
    if (k === this.stance) return;
    this.stance = k;
    const a = this.stances[k];
    if (a && !this.held[k]) a.play();
    if (now) this.held.forEach((_, i) => (this.held[i] = i === k ? 1 : 0));
  }

  /**
   * Death `k` (set.deaths) alone, posed `time` seconds in; null lets the
   * moving clips back (update sets their weights again).
   */
  fallen(k: number | null, time = 0): void {
    const next = k === null ? null : this.deaths[k]!;
    if (this.dying !== next) {
      this.dying?.stop();
      this.dying = next;
      this.down = false;
      if (next) {
        next.reset().play();
        next.paused = true;
      }
    }
    if (!next) return;
    // It blends in from the pose the unit had (what else played, held as it was); once in, it plays alone.
    const s = Math.min(1, time / DEATH_BLEND);
    if (s >= 1 && !this.down) {
      this.down = true;
      this.variation?.stop();
      this.variation = null;
      this.react(null);
      for (const a of this.actions) a.setEffectiveWeight(0);
      for (const a of this.stances) a.setEffectiveWeight(0);
    }
    next.setEffectiveWeight(this.down ? 1 : s / (1 - s));
    next.time = time;
    this.mixer.update(0);
  }

  /**
   * Plays `dt` seconds with the current weights and speeds. A `calm` unit
   * standing idle (the idle weight full) now and then plays a variation,
   * which takes its weight from the idle while it lasts.
   */
  update(dt: number, calm: boolean, look = false): void {
    const idle = this.weights[0]!;
    // The stance stood in takes the idle's place, the others give it back; those out of use stop.
    const ease = dt / STANCE_BLEND;
    let stood = 0;
    for (let k = 0; k < this.stances.length; k++) {
      const h = (this.held[k] = THREE.MathUtils.clamp(this.held[k]! + (k === this.stance ? ease : -ease), 0, 1));
      const a = this.stances[k]!;
      if (h > 0) a.setEffectiveWeight(idle * h);
      else if (a.isRunning()) a.stop();
      stood += h;
    }
    // A camp clip stayed in takes the idle's place too, the one before eased out.
    for (const [a, held] of this.loops) {
      const h = THREE.MathUtils.clamp(held + (a === this.loop ? ease : -ease), 0, 1);
      if (h > 0) {
        this.loops.set(a, h);
        a.setEffectiveWeight(idle * h);
      } else {
        a.stop();
        this.loops.delete(a);
      }
      stood += h;
    }
    // A camp move over everything (as a hit's share against the rest's 1), quickly in and out; one it took over from
    // eases out as it comes in.
    const b = this.before;
    if (b) {
      b.share = Math.max(0, b.share - dt / BLEND);
      if (b.share <= 0) {
        b.action.stop();
        this.before = null;
      } else b.action.setEffectiveWeight(b.share / (1 - b.share));
    }
    if (this.move) {
      this.moved = THREE.MathUtils.clamp(this.moved + (this.moving ? dt : -dt) / BLEND, 0, 1);
      const s = Math.min(0.97, this.moved);
      this.move.setEffectiveWeight(s / (1 - s));
      if (!this.moving && this.moved <= 0) {
        this.move.stop();
        this.move = null;
      }
    }
    // A gesture over the upper body, eased in and out (against the rest's 1, as a hit's share: react).
    const g = this.gesturing;
    if (g) {
      const length = g.getClip().duration;
      if (!g.isRunning() || g.time >= length - 1e-3) {
        g.stop();
        this.gesturing = null;
      } else {
        const s = Math.min(0.95, Math.min(1, g.time / BLEND, (length - g.time) / BLEND) * idle);
        g.setEffectiveWeight(s / (1 - s));
      }
    }
    let v = 0;
    const current = this.variation;
    if (current) {
      const length = current.getClip().duration;
      this.keep = THREE.MathUtils.clamp(this.keep + (calm ? dt : -dt) / CUT, 0, 1);
      if (!current.isRunning() || current.time >= length - 1e-3 || this.keep === 0) {
        current.stop();
        this.variation = null;
        this.wait = nextWait();
      } else v = Math.min(1, current.time / BLEND, (length - current.time) / BLEND) * idle * (1 - stood) * this.keep;
      current.setEffectiveWeight(v);
    } else if (calm && idle > 0.95 && !stood && !this.gesturing && !this.move && (this.camp ?? this.variations).length) {
      this.wait -= dt;
      if (this.wait <= 0) {
        const pool = this.camp ?? this.variations;
        const next = pool[Math.floor(Math.random() * pool.length)]!;
        next.reset().setEffectiveWeight(0).play();
        this.variation = next;
        this.keep = 1;
      }
    } else this.wait = Math.max(this.wait, 2);
    // Holding a weapon at rest (`look`: its clip plays, so no variation), now and then a look round, the head only.
    const l = this.looking;
    if (l) {
      const length = l.getClip().duration;
      if (!l.isRunning() || l.time >= length - 1e-3 || !look) {
        l.stop();
        this.looking = null;
        this.lookWait = nextWait();
      } else {
        const s = Math.min(0.9, Math.min(1, l.time / BLEND, (length - l.time) / BLEND));
        l.setEffectiveWeight(s / (1 - s));
      }
    } else if (look && this.looks.length) {
      this.lookWait -= dt;
      if (this.lookWait <= 0) {
        const next = this.looks[Math.floor(Math.random() * this.looks.length)]!;
        next.reset().setEffectiveWeight(0).play();
        this.looking = next;
      }
    }
    for (let k = 0; k < this.actions.length; k++) {
      const a = this.actions[k]!;
      let w = k === 0 ? idle * (1 - stood) - v : this.weights[k]!;
      // Moving with a gun: its own clip in Synty's place, at its own pace.
      for (let j = 0; j < ARMED_KINDS.length; j++) {
        const kind = ARMED_KINDS[j]!;
        const own = this.armedActs[kind][k];
        if (!own) continue;
        const share = this.weights[k]! * this.armedHeld[kind];
        own.setEffectiveWeight(share);
        own.timeScale = this.speeds[k]! * this.set.armed[kind].pace[k]!;
        w -= share;
      }
      a.setEffectiveWeight(Math.max(0, w));
      a.timeScale = this.speeds[k]!;
    }
    this.mixer.update(dt);
  }

  /**
   * Each bone's turn in model space from the T-pose (`out`), and how far the
   * hips have moved from theirs, in T-pose hip heights (`hips`).
   */
  read(out: THREE.Quaternion[], hips: THREE.Vector3): void {
    const { set, world, bones } = this;
    for (let i = 0; i < BODY; i++) {
      const parent = set.parent[i]!;
      const w = world[i]!.copy(parent < 0 ? set.rootQ : world[parent]!).multiply(bones[i]!.quaternion);
      out[i]!.copy(w).multiply(set.restInv[i]!);
    }
    v.copy(bones[HIPS]!.position).applyMatrix4(set.rootM).sub(set.hips);
    hips.copy(v).divideScalar(set.hips.y);
  }
}
