import * as THREE from 'three';
import { createMission, Rng, type GameState } from '@bastion/engine';
import { backdrop, cameraPath, longestStreet, mapScene, type MapScene, type Street } from '../cine';
import type { Shot } from '../types';

// Shot 1 (docs/TRAILER.md, 0:00–0:06): a street of the dead city at dawn, empty,
// the camera low and creeping down it; Lore's line over its lower left from
// 1.3 s to 5.7 s (UI's), so what happens keeps to the middle and the right.
// The street is the longest of a generated city (`?mapa=N` tries another
// seed); shot 2 (horda.ts) is the same street, the dead coming up it.

const params = new URLSearchParams(location.search);
/** The city the opening is shot in. */
export const STREET_SEED = Number(params.get('mapa') ?? 5);

/** The city, without anyone in it. */
export function streetState(): GameState {
  const state = createMission({ map: { kind: 'generated', biome: 'city', seed: STREET_SEED }, slots: [0], rng: new Rng(STREET_SEED), kind: 'elimination', difficulty: 2 });
  state.units = [];
  return state;
}

/** The street's line in world units: where it starts, its direction, its length, and the side vector (to its right as one looks down it). */
export function streetLine(s: Street): { start: THREE.Vector3; dir: THREE.Vector3; side: THREE.Vector3; length: number; width: number } {
  const horizontal = s.from.y === s.to.y;
  const start = horizontal ? new THREE.Vector3(s.from.x, 0, s.from.y + s.width / 2) : new THREE.Vector3(s.from.x + s.width / 2, 0, s.from.y);
  const dir = horizontal ? new THREE.Vector3(1, 0, 0) : new THREE.Vector3(0, 0, 1);
  const length = horizontal ? s.to.x - s.from.x + 1 : s.to.y - s.from.y + 1;
  return { start, dir, side: new THREE.Vector3(-dir.z, 0, dir.x), length, width: s.width };
}

let scene: MapScene | null = null;
let move: ((t: number, camera: THREE.PerspectiveCamera) => void) | null = null;

export default {
  id: 'calle',
  duration: 6,
  async setup(stage) {
    const state = streetState();
    scene = await mapScene(stage, state, 'dawn');
    // The city beyond the end of the street, and a fire's smoke over it, closing the horizon.
    backdrop(scene.renderer.scene, state, [[48, 125]], true);
    const line = streetLine(longestStreet(state)!);
    // Low, a little off the middle of the street, creeping down it; the aim far down it, at a body's height.
    const at = (along: number, across: number, up: number) => line.start.clone().addScaledVector(line.dir, along).addScaledVector(line.side, across).setY(up).toArray();
    move = cameraPath(
      [
        { t: 0, pos: at(3, -0.6, 0.7), at: at(24, -0.1, 1.0), fov: 36 },
        { t: 6, pos: at(6.2, -0.5, 0.8), at: at(27, -0.15, 1.05), fov: 34 },
      ],
      { in: false, out: false },
    );
  },
  frame(t) {
    move?.(t, scene!.renderer.camera);
  },
  teardown() {
    scene?.renderer.dispose();
  },
} satisfies Shot;
