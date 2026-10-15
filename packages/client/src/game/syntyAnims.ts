import * as THREE from 'three';
import { loadAnimation } from './synty';
import { ANIMATIONS, DEAD_MOTIONS, DEATHS, HITS, IDLE_VARIATIONS, MOTIONS, STANCE_ORDER, STANCES, SYNTY_BONES, type Motion } from './syntyManifest';

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
const HIPS = SYNTY_BONES.indexOf('Hips');
/** The bones a hit reaction moves: from the spine up (the legs and hips keep what they do). */
const UPPER = new Set(['Spine_01', 'Spine_02', 'Spine_03', 'Neck', 'Head', 'Clavicle_L', 'Clavicle_R', 'Shoulder_L', 'Shoulder_R', 'Elbow_L', 'Elbow_R', 'Hand_L', 'Hand_R']);
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
  /** Inverse model-space rotation of each bone in the T-pose. */
  restInv: THREE.Quaternion[];
  /** Each bone's parent among SYNTY_BONES (−1: the hips, under the fixed root). */
  parent: number[];
  rootQ: THREE.Quaternion;
  rootM: THREE.Matrix4;
  /** The hips in the T-pose. */
  hips: THREE.Vector3;
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
    clips: motionClips,
    variations,
    stances,
    deaths,
    settles: deaths.map(settleOf),
    hits: hits.map((c) => new THREE.AnimationClip(c.name, c.duration, c.tracks.filter((t) => UPPER.has(t.name.split('.')[0]!)))),
    restInv: bones.map((b) => (b ? b.getWorldQuaternion(new THREE.Quaternion()).invert() : new THREE.Quaternion())),
    parent,
    rootQ: root.getWorldQuaternion(new THREE.Quaternion()),
    rootM: root.matrixWorld.clone(),
    hips: hips.getWorldPosition(new THREE.Vector3()),
  };
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
    const ids = [...motions, ...variations, ...STANCE_ORDER.map((s) => STANCES[build][s]), ...DEATHS[build].map((d) => d.id), ...HITS[build].map((h) => h.id)];
    p = Promise.all([loadAnimation(ANIMATIONS.tpose.pack, 'tpose'), ...ids.map((id) => loadAnimation(ANIMATIONS[id].pack, id))]).then(([pose, ...loaded]) => {
      const clips = loaded.map((c) => c.clip);
      const at = [MOTION_ORDER.length, MOTION_ORDER.length + variations.length, MOTION_ORDER.length + variations.length + STANCE_ORDER.length];
      at.push(at[2]! + DEATHS[build].length);
      const set = prepare(pose!.scene, pose!.clip, clips.slice(0, at[0]), clips.slice(at[0], at[1]), clips.slice(at[1], at[2]), clips.slice(at[2], at[3]), clips.slice(at[3]));
      ready.set(kit, set);
      return set;
    });
    loading.set(kit, p);
  }
  return p;
}

const v = new THREE.Vector3();
/** Seconds a variation takes to blend in and out, and to wait between two. */
const BLEND = 0.35;
/** Seconds the idle takes to ease from one stance to another. */
const STANCE_BLEND = 0.6;
const nextWait = () => 5 + Math.random() * 9;

/** One unit's locomotion: a proxy skeleton and its mixer. */
export class AnimDriver {
  private readonly mixer: THREE.AnimationMixer;
  private readonly actions: THREE.AnimationAction[];
  private readonly variations: THREE.AnimationAction[];
  private variation: THREE.AnimationAction | null = null;
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
  private wait = nextWait();
  private readonly bones: THREE.Object3D[];
  private readonly world = Array.from({ length: N }, () => new THREE.Quaternion());
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
    // Each starts playing when first stood in, so a crowd plays only the clips it uses.
    this.stances = set.stances.map((clip) => {
      const a = this.mixer.clipAction(clip);
      a.time = phase * clip.duration;
      a.setEffectiveWeight(0);
      return a;
    });
    this.held = new Float32Array(set.stances.length);
    this.deaths = set.deaths.map((clip) => {
      const a = this.mixer.clipAction(clip);
      a.setLoop(THREE.LoopOnce, 1);
      a.clampWhenFinished = true;
      return a;
    });
    this.hits = set.hits.map((clip) => this.mixer.clipAction(clip));
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
  update(dt: number, calm: boolean): void {
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
    let v = 0;
    const current = this.variation;
    if (current) {
      const length = current.getClip().duration;
      if (!current.isRunning() || current.time >= length - 1e-3) {
        current.stop();
        this.variation = null;
        this.wait = nextWait();
      } else v = Math.min(1, current.time / BLEND, (length - current.time) / BLEND) * idle * (1 - stood);
      current.setEffectiveWeight(v);
    } else if (calm && idle > 0.95 && !stood && this.variations.length) {
      this.wait -= dt;
      if (this.wait <= 0) {
        const next = this.variations[Math.floor(Math.random() * this.variations.length)]!;
        next.reset().setEffectiveWeight(0).play();
        this.variation = next;
      }
    } else this.wait = Math.max(this.wait, 2);
    for (let k = 0; k < this.actions.length; k++) {
      const a = this.actions[k]!;
      a.setEffectiveWeight(k === 0 ? idle * (1 - stood) - v : this.weights[k]!);
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
    for (let i = 0; i < N; i++) {
      const parent = set.parent[i]!;
      const w = world[i]!.copy(parent < 0 ? set.rootQ : world[parent]!).multiply(bones[i]!.quaternion);
      out[i]!.copy(w).multiply(set.restInv[i]!);
    }
    v.copy(bones[HIPS]!.position).applyMatrix4(set.rootM).sub(set.hips);
    hips.copy(v).divideScalar(set.hips.y);
  }
}
