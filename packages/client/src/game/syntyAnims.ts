import * as THREE from 'three';
import { loadAnimation } from './synty';
import { ANIMATIONS, IDLE_VARIATIONS, MOTIONS, SYNTY_BONES, type Motion } from './syntyManifest';

// Locomotion from «ANIMATION – Base Locomotion» on any Synty character. Each
// unit plays the clips on its own proxy of the pack's skeleton (one mixer,
// the clips shared) and reads back, bone by bone, how far each has turned in
// model space from the pack's T-pose. The figure puts that same turn on its
// own bones' T-pose, so the clips fit both Synty rigs (Apocalypse and City
// Zombies) whatever their bone axes, and the procedural layers (weapon,
// lean, hits, the dead's reach) go on top in the same terms. A unit standing
// idle now and then plays a variation from «ANIMATION – Idles» instead: it
// looks round or shifts its weight.

const N = SYNTY_BONES.length;
const HIPS = SYNTY_BONES.indexOf('Hips');

/** The clips a figure blends, in the order of its weights. */
export const MOTION_ORDER: readonly Motion[] = ['idle', 'crouchIdle', 'walk', 'run', 'crouchWalk'];

export type Build = 'masc' | 'femn';

/** One build's clips and the T-pose they are measured from, shared by every unit that moves with them. */
export interface AnimSet {
  /** The pack's skeleton, cloned as each unit's proxy. */
  template: THREE.Object3D;
  clips: THREE.AnimationClip[];
  /** Played once now and then in place of the idle. */
  variations: THREE.AnimationClip[];
  /** Inverse model-space rotation of each bone in the T-pose. */
  restInv: THREE.Quaternion[];
  /** Each bone's parent among SYNTY_BONES (−1: the hips, under the fixed root). */
  parent: number[];
  rootQ: THREE.Quaternion;
  rootM: THREE.Matrix4;
  /** The hips in the T-pose. */
  hips: THREE.Vector3;
}

const ready = new Map<Build, AnimSet>();
const loading = new Map<Build, Promise<AnimSet>>();

function prepare(tpose: THREE.Group, poseClip: THREE.AnimationClip, motionClips: THREE.AnimationClip[], variations: THREE.AnimationClip[]): AnimSet {
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
    restInv: bones.map((b) => (b ? b.getWorldQuaternion(new THREE.Quaternion()).invert() : new THREE.Quaternion())),
    parent,
    rootQ: root.getWorldQuaternion(new THREE.Quaternion()),
    rootM: root.matrixWorld.clone(),
    hips: hips.getWorldPosition(new THREE.Vector3()),
  };
}

/** A build's clips once loaded; null meanwhile (asking starts the load). */
export function animSet(build: Build): AnimSet | null {
  const set = ready.get(build);
  if (!set) void whenAnimSet(build).catch(() => undefined);
  return set ?? null;
}

export function whenAnimSet(build: Build): Promise<AnimSet> {
  let p = loading.get(build);
  if (!p) {
    const ids = [...MOTION_ORDER.map((m) => MOTIONS[build][m]), ...IDLE_VARIATIONS[build]];
    p = Promise.all([loadAnimation(ANIMATIONS.tpose.pack, 'tpose'), ...ids.map((id) => loadAnimation(ANIMATIONS[id].pack, id))]).then(([pose, ...clips]) => {
      const motions = clips.slice(0, MOTION_ORDER.length).map((c) => c.clip);
      const set = prepare(pose!.scene, pose!.clip, motions, clips.slice(MOTION_ORDER.length).map((c) => c.clip));
      ready.set(build, set);
      return set;
    });
    loading.set(build, p);
  }
  return p;
}

const v = new THREE.Vector3();
/** Seconds a variation takes to blend in and out, and to wait between two. */
const BLEND = 0.35;
const nextWait = () => 5 + Math.random() * 9;

/** One unit's locomotion: a proxy skeleton and its mixer. */
export class AnimDriver {
  private readonly mixer: THREE.AnimationMixer;
  private readonly actions: THREE.AnimationAction[];
  private readonly variations: THREE.AnimationAction[];
  private variation: THREE.AnimationAction | null = null;
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
    this.bones = SYNTY_BONES.map((n) => proxy.getObjectByName(n) ?? new THREE.Object3D());
  }

  /**
   * Plays `dt` seconds with the current weights and speeds. A `calm` unit
   * standing idle (the idle weight full) now and then plays a variation,
   * which takes its weight from the idle while it lasts.
   */
  update(dt: number, calm: boolean): void {
    const idle = this.weights[0]!;
    let v = 0;
    const current = this.variation;
    if (current) {
      const length = current.getClip().duration;
      if (!current.isRunning() || current.time >= length - 1e-3) {
        current.stop();
        this.variation = null;
        this.wait = nextWait();
      } else v = Math.min(1, current.time / BLEND, (length - current.time) / BLEND) * idle;
      current.setEffectiveWeight(v);
    } else if (calm && idle > 0.95 && this.variations.length) {
      this.wait -= dt;
      if (this.wait <= 0) {
        const next = this.variations[Math.floor(Math.random() * this.variations.length)]!;
        next.reset().setEffectiveWeight(0).play();
        this.variation = next;
      }
    } else this.wait = Math.max(this.wait, 2);
    for (let k = 0; k < this.actions.length; k++) {
      const a = this.actions[k]!;
      a.setEffectiveWeight(k === 0 ? idle - v : this.weights[k]!);
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
