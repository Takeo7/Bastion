import * as THREE from 'three';
import { GeoscapeSet } from '../../../strategy/geoscape';
import { Stage } from '../../../strategy/stage';
import { frames } from '../cine';
import type { Shot } from '../types';

// Shot 6 (docs/TRAILER.md, 0:44–0:48): the campaign's valley, the Marea coming
// up it. The game's own map (strategy/geoscape.ts: Arturo's painting, the
// Marea in its shader), slowly closing in from the whole valley on La Vega
// and the south while the zombi activity it is drawn at climbs from the plain
// to the middle of the valley: the violet edge running up the roads, Los
// Llanos, Corran and Galen going under. Its names and markers are HTML, which
// the take does not see: the map alone. Then UI's screens (pantallas).

/** The activity the Marea is drawn at, from the shot's start to its end (MAREA_THRESHOLDS: the plain falls at 4, the cities at 6, the middle at 9). */
const LEVEL: [number, number] = [4.2, 8.6];

let stage3d: Stage | null = null;
let valley: GeoscapeSet | null = null;

/** The set's insides the shot drives (private there). */
interface Inside {
  level: number;
  levelGoal: number;
}

export default {
  id: 'valle',
  duration: 4,
  async setup(stage) {
    stage3d = new Stage();
    stage3d.soundtrack = false;
    stage3d.element.style.cssText = 'position: absolute; inset: 0';
    stage.append(stage3d.element);
    valley = new GeoscapeSet(() => undefined);
    stage3d.add('geo', valley);
    await stage3d.show('geo', valley.shot(), true);
    // The painting in, and its mipmaps made.
    valley.look(0, 0, 1);
    await frames(90);
  },
  frame(t) {
    if (!valley) return;
    const k = THREE.MathUtils.smoothstep(t, 0, 4);
    valley.look(THREE.MathUtils.lerp(0, -0.5, k), THREE.MathUtils.lerp(0, 1.4, k), THREE.MathUtils.lerp(1, 1.5, k));
    const inside = valley as unknown as Inside;
    inside.level = inside.levelGoal = THREE.MathUtils.lerp(LEVEL[0], LEVEL[1], THREE.MathUtils.smoothstep(t, 0.3, 3.8));
  },
  teardown() {
    stage3d?.dispose();
  },
} satisfies Shot;
