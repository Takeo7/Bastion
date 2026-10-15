import * as THREE from 'three';
import { canStandOn, type TemplateId } from '@bastion/engine';
import { sound } from '../../../audio/bank';
import type { FigurePose, FigureRig } from '../../../game/figures';
import { clipSpeed, gaitsOf } from '../../../game/gaits';
import { IDLE_POSE } from '../../../game/models';
import { SyntyFigure, syntyFigure } from '../../../game/syntyFigures';
import { backdrop, cameraPath, longestStreet, mapScene, type MapScene } from '../cine';
import type { Shot } from '../types';
import { streetLine, streetState } from './calle';

// Shot 2 (docs/TRAILER.md, 0:06–0:13): the street of shot 1 (calle.ts), and up
// it, out of the haze at its far end, the horde: 45 of the dead walking
// towards the camera at the game's own gait, unhurried, as the camera backs
// away low in front of them. Lore's line over the lower left from 1.6 s to
// 6.3 s (UI's).

/** Who comes up the street: mostly the common dead, a few armoured and bloated among them. */
const CROWD: TemplateId[] = [...Array<TemplateId>(38).fill('zombie'), 'mec', 'mec', 'mec', 'mec', 'bloater', 'bloater', 'bloater'];

/** A fixed shuffle, the same take after take whatever Math.random's seed. */
const hash = (n: number) => {
  const x = Math.sin(n * 127.1 + 311.7) * 43758.5453;
  return x - Math.floor(x);
};

interface Walker {
  rig: FigureRig;
  pose: FigurePose;
  /** Where it starts (world), which way it walks, and how fast (world units a second). */
  from: THREE.Vector3;
  dir: THREE.Vector3;
  speed: number;
  sway: number;
}

/** What the horde sounds like (the game's own sounds): moans far and near, the drag of their feet, and the pan of each. */
const CUES: [number, string, number, number][] = [
  [0.4, 'zombie.moan', 0.5, -0.3],
  [1.3, 'step.drag', 0.45, 0.1],
  [1.9, 'zombie.moan', 0.6, 0.35],
  [2.6, 'step.drag', 0.55, -0.15],
  [3.2, 'zombie.moan', 0.75, -0.1],
  [3.9, 'step.drag', 0.65, 0.2],
  [4.5, 'zombie.moan', 0.85, 0.25],
  [5.2, 'step.drag', 0.75, -0.2],
  [5.7, 'zombie.moan', 0.95, -0.35],
  [6.4, 'zombie.moan', 1.0, 0.1],
];
let cue = 0;

let scene: MapScene | null = null;
let move: ((t: number, camera: THREE.PerspectiveCamera) => void) | null = null;
const walkers: Walker[] = [];
let elapsed = 0;

export default {
  id: 'horda',
  duration: 7,
  async setup(stage) {
    const state = streetState();
    scene = await mapScene(stage, state, 'dawn');
    // The city beyond the end of the street, and a fire's smoke over it, closing the horizon.
    backdrop(scene.renderer.scene, state, [[48, 125]], true);
    const line = streetLine(longestStreet(state)!);
    const standable = (p: THREE.Vector3) => {
      const x = Math.floor(p.x);
      const y = Math.floor(p.z);
      return x >= 0 && y >= 0 && x < state.width && y < state.height && canStandOn(state.tiles[y * state.width + x]!);
    };
    // Rows of five or six across the street and its pavements, ragged, the front ones out of the haze first.
    const toward = line.dir.clone().negate();
    let placed = 0;
    for (let k = 0; placed < CROWD.length && k < 4000; k++) {
      const template = CROWD[placed]!;
      const row = Math.floor(placed / 5.5);
      const along = 21.5 + row * 1.35 + hash(k * 3 + 1) * 0.9;
      const across = (hash(k * 7 + 2) - 0.5) * (line.width + 1.6);
      const from = line.start.clone().addScaledVector(line.dir, along).addScaledVector(line.side, across);
      const gait = gaitsOf(template).one;
      const speed = (1 / gait.seconds) * (0.92 + hash(k * 11 + 3) * 0.14);
      // Its whole way in the shot standable (no car, no wall in it), and not on top of another.
      let clear = true;
      for (let s = 0; clear && s <= 8; s++) clear = standable(from.clone().addScaledVector(toward, (speed * 7 * s) / 8));
      if (!clear || walkers.some((w) => w.from.distanceTo(from) < 0.62)) continue;
      const rig = syntyFigure(template, '#ff4d4d', undefined, undefined, `horda${placed}`);
      if (!rig) continue;
      rig.setGait?.(gait.motion, clipSpeed(template, gait));
      rig.root.position.copy(from);
      scene.renderer.scene.add(rig.root);
      const yaw = Math.atan2(toward.x, toward.z);
      walkers.push({ rig, pose: { ...IDLE_POSE, yaw, phase: hash(k * 13 + 4) * Math.PI * 2, run: 1, spin: 0, search: 0 }, from, dir: toward, speed, sway: hash(k * 17 + 5) });
      placed++;
    }
    // Every body dressed and with its clips before the first frame.
    for (let n = 0; n < 600 && !walkers.every((w) => !(w.rig instanceof SyntyFigure) || w.rig.ready); n++) await new Promise((r) => setTimeout(r, 25));
    scene.renderer.onFrame((_dt, time) => {
      for (const w of walkers) {
        // Straight on, with a slow drift side to side.
        const drift = Math.sin(elapsed * 0.6 + w.sway * 6) * 0.12;
        w.rig.root.position.copy(w.from).addScaledVector(w.dir, w.speed * elapsed).addScaledVector(line.side, drift);
        w.pose.yaw = Math.atan2(w.dir.x, w.dir.z) + Math.cos(elapsed * 0.6 + w.sway * 6) * 0.08;
        w.rig.applyPose(w.pose, time);
      }
    });
    // Low in front of them; as they come, it rises a little, and the rows behind show.
    const at = (along: number, across: number, up: number) => line.start.clone().addScaledVector(line.dir, along).addScaledVector(line.side, across).setY(up).toArray();
    move = cameraPath([
      { t: 0, pos: at(7.4, -0.15, 0.6), at: at(24, 0, 1.1), fov: 24 },
      { t: 7, pos: at(8.1, -0.05, 1.2), at: at(16, 0, 0.95), fov: 33 },
    ]);
  },
  frame(t) {
    elapsed = t;
    while (cue < CUES.length && CUES[cue]![0] <= t) {
      const [, name, gain, pan] = CUES[cue++]!;
      sound(name, { gain, pan });
    }
    move?.(t, scene!.renderer.camera);
  },
  teardown() {
    scene?.renderer.dispose();
  },
} satisfies Shot;
