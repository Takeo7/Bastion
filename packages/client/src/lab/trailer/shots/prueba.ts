import * as THREE from 'three';
import type { TemplateId } from '@bastion/engine';
import { sound } from '../../../audio/bank';
import type { FigurePose, FigureRig } from '../../../game/figures';
import { IDLE_POSE } from '../../../game/models';
import { Renderer } from '../../../game/renderer';
import { SyntyFigure, syntyFigure } from '../../../game/syntyFigures';
import type { Shot } from '../types';

// Red's test of the capture, not a shot of the trailer: a dozen of the game's
// zombies shambling toward a camera that comes down to meet them, and sounds
// at known times (a moan at 0.5 s, shots at 1.5, 2.0 and 2.5 s, the Chillón
// at 3.5 s) to check that the take's track lines up with its frames.

const TEMPLATES: TemplateId[] = ['zombie', 'zombie', 'zombie', 'bloater', 'zombie', 'xenoid'];
/** Metres a second: about the game's zombie gait (gaits.ts, 1.02 s a tile). */
const SPEED = 0.95;

let renderer: Renderer | null = null;
const crowd: { rig: FigureRig; pose: FigurePose; from: THREE.Vector3 }[] = [];
let fired = 0;
const CUES: [number, string][] = [
  [0.5, 'zombie.moan'],
  [1.5, 'shot.rifle.1'],
  [2.0, 'shot.rifle.1'],
  [2.5, 'shot.rifle.1'],
  [3.5, 'shot.xenoid'],
];
let elapsed = 0;

export default {
  id: 'prueba',
  duration: 5,
  async setup(stage) {
    renderer = new Renderer(stage);
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(80, 80).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x6f6858, roughness: 0.95 }));
    ground.receiveShadow = true;
    renderer.scene.add(ground);
    for (let i = 0; i < 12; i++) {
      const rig = syntyFigure(TEMPLATES[i % TEMPLATES.length]!, '#ff4d4d', undefined, undefined, `prueba${i}`)!;
      const from = new THREE.Vector3((i % 4) * 1.6 - 2.4 + (Math.random() - 0.5) * 0.6, 0, -6 - Math.floor(i / 4) * 1.8 + (Math.random() - 0.5) * 0.6);
      rig.root.position.copy(from);
      renderer.scene.add(rig.root);
      crowd.push({ rig, pose: { ...IDLE_POSE, yaw: 0, phase: Math.random() * Math.PI * 2, run: 1, spin: 0, search: 0 }, from });
    }
    // Every figure dressed and with its clips before the first frame.
    for (let k = 0; k < 400 && !crowd.every((m) => !(m.rig instanceof SyntyFigure) || m.rig.ready); k++) await new Promise((r) => setTimeout(r, 25));
    renderer.onFrame((_dt, time) => {
      for (const m of crowd) {
        m.rig.root.position.z = m.from.z + SPEED * elapsed;
        m.rig.applyPose(m.pose, time);
      }
    });
  },
  frame(t) {
    elapsed = t;
    const cam = renderer!.camera;
    // From high and wide to low in front of them.
    const k = t / 5;
    cam.position.set(Math.sin(k * 0.6) * 3, 6 - 4.6 * k, 9 - 2 * k);
    cam.lookAt(0, 1.2 - 0.2 * k, -3 + 2 * k);
    while (fired < CUES.length && CUES[fired]![0] <= t) sound(CUES[fired++]![1]);
  },
  teardown() {
    renderer?.dispose();
  },
} satisfies Shot;
