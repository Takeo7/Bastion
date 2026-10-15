import * as THREE from 'three';
import { canStandOn, type GameState } from '@bastion/engine';
import { LocalBattle, loadPlan, spot } from '../sala';
import { backdrop, type V3 } from '../cine';
import type { Shot } from '../types';

// The cover (docs/TRAILER.md, «Portada»): not a shot of the trailer but its
// still, 3840×2160 (`&ss=2`). From Gameplay's state «portada» (a port
// district, a flat roof two storeys up), rearranged for the picture, as no
// rule is played: the squad along the roof's south edge, aiming down into the
// wide road below, where the dead fill it coming from the east; La Lata down
// the road beyond them, its zone moved there. At dusk: the sun low in the west
// behind the camera, which stands above and behind the four, looking east down
// the road: them in the foreground, the horde gold in the low sun, the sky at
// the top free for UI's logo. `__portada.save()` sends it to the
// receiver as portada/keyart.png.

/** The squad on the roof's south edge, two storeys up (the parapet in row 20). */
const SQUAD: [number, number][] = [
  [20, 19],
  [21, 19],
  [22, 19],
  [23, 19],
];
/** La Lata's zone: further down the road, beyond the dead. */
const EVAC = { x: 40, y: 21 };

/** A fixed scatter (the same picture every time). */
const hash = (n: number) => {
  const x = Math.sin(n * 91.7 + 13.1) * 43758.5453;
  return x - Math.floor(x);
};

function rearrange(s: GameState): void {
  const w = s.width;
  const open = (x: number, y: number) => x >= 0 && y >= 0 && x < w && y < s.height && canStandOn(s.tiles[y * w + x]!) && !s.elev[y * w + x];
  const squad = s.units.filter((u) => u.team === 'xcom');
  squad.forEach((u, i) => (u.pos = { x: SQUAD[i]![0], y: SQUAD[i]![1], z: 2 }));
  s.mission.evacZone = [];
  for (let dy = 0; dy < 3; dy++) for (let dx = 0; dx < 3; dx++) s.mission.evacZone.push({ x: EVAC.x + dx, y: EVAC.y + dy });
  // The dead in the road (rows 21–24), thick under the roof and thinning out east towards La Lata; none in its zone.
  const taken = new Set<number>();
  const dead = s.units.filter((u) => u.team !== 'xcom');
  let k = 0;
  for (const u of dead) {
    for (; k < 5000; k++) {
      const x = Math.round(21 + Math.pow(hash(k * 2 + 1), 1.4) * 16);
      const y = 21 + Math.floor(hash(k * 2 + 2) * 4);
      const inZone = x >= EVAC.x - 1 && x <= EVAC.x + 3 && y >= EVAC.y - 1 && y <= EVAC.y + 3;
      if (!open(x, y) || inZone || taken.has(y * w + x)) continue;
      taken.add(y * w + x);
      u.pos = { x, y, z: 0 };
      u.alive = true;
      k++;
      break;
    }
  }
}

let battle: LocalBattle | null = null;

/** The camera: above and behind the squad, looking east down the road, the sky over it. */
const CAMERA: { pos: V3; at: V3; fov: number } = { pos: [16.2, 7.6, 21.5], at: [42, 3.2, 22.4], fov: 40 };

export default {
  id: 'portada',
  duration: 1,
  async setup(stage) {
    const plan = await loadPlan('portada');
    rearrange(plan.state);
    battle = new LocalBattle(stage, plan);
    battle.reveal = true;
    await battle.ready();
    battle.renderer.setTime('dusk');
    // The skyline beyond the end of the road, and a fire's smoke over it on the right, clear of the logo.
    backdrop(battle.renderer.scene, plan.state, [[150, 58]]);
    // The specialist's drone, a green halo at this distance, stays out of the picture.
    battle.renderer.scene.traverse((o) => {
      const m = (o as THREE.Mesh).material as THREE.MeshStandardMaterial | undefined;
      if (m?.emissive?.getHex() === 0x2fd97a && o.parent) o.parent.visible = false;
    });
    for (const u of plan.state.units) if (u.team !== 'xcom') battle.face(u.id, spot(22, 19, 4.6));
    await Promise.all(plan.state.units.filter((u) => u.team === 'xcom').map((u, i) => battle!.aim(u.id, spot(25 + i * 1.5, 22.5))));
    const save = async (name = 'portada/keyart.png') => {
      const r = battle!.renderer;
      r.render(0);
      const blob = await new Promise<Blob>((done) => r.gl.domElement.toBlob((b) => done(b!), 'image/png'));
      await fetch(`${new URLSearchParams(location.search).get('receptor') ?? ''}/__trailer/file?name=${name}`, { method: 'POST', body: blob });
      return `${name} · ${r.gl.domElement.width}×${r.gl.domElement.height}`;
    };
    Object.assign(window, { __portada: { battle, save, camera: CAMERA } });
  },
  frame() {
    const c = battle!.renderer.camera;
    c.position.set(...CAMERA.pos);
    c.lookAt(new THREE.Vector3(...CAMERA.at));
    if (c.fov !== CAMERA.fov) {
      c.fov = CAMERA.fov;
      c.updateProjectionMatrix();
    }
  },
  teardown() {
    battle?.dispose();
  },
} satisfies Shot;
