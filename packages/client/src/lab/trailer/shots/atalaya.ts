import * as THREE from 'three';
import { BASE_SIZE, PLAYER_COLORS, type CampaignState } from '@bastion/engine';
import { sound } from '../../../audio/bank';
import { BastionSet, type LineupEntry } from '../../../strategy/bastion';
import { loadBaseKit } from '../../../strategy/baseKit';
import { Stage } from '../../../strategy/stage';
import { cameraPath, frames } from '../cine';
import type { Shot } from '../types';

// Shot 3 (docs/TRAILER.md, 0:13–0:20): Atalaya in the sierra. The base as the
// campaign has it (Gameplay's showcase game, .dev-snapshots/trailer/partidas/
// base.json): from high over the barriers and the yard, the bunker and the
// peaks behind, down to the hangar as the squad boards and La Lata drives out
// of its side gate (the base's own take-off), the camera turning after it.
// Lore's line over the compound from 1.0 s to 3.9 s (UI's); the bus out of the
// gate by 6.3 s, as UI cuts to black at 6.5 s for the music's silence.

const SAVES = import.meta.glob<{ default: { campaign: CampaignState } }>('../../../../../../.dev-snapshots/trailer/partidas/base.json');

/** Where the base's hangar stands (bastion.ts HANGAR: south of the compound), and its side gate out east. */
const PITCH = 5 + 1.4;
const HANGAR = new THREE.Vector3(0, 0, (BASE_SIZE * PITCH) / 2 + 9);
const GATE = HANGAR.clone().add(new THREE.Vector3(6.7, 0, -2.2));
/** When the squad starts boarding: the bus drives out of the gate some 2.5 s later, and at LAUNCH_SECONDS (3.4 s) the take-off is over and it is gone. */
const LAUNCH = 2.9;

let stage3d: Stage | null = null;
let set: BastionSet | null = null;
let move: ((t: number, camera: THREE.PerspectiveCamera) => void) | null = null;
let launched = false;

export default {
  id: 'atalaya',
  duration: 7,
  async setup(stage) {
    const save = SAVES['../../../../../../.dev-snapshots/trailer/partidas/base.json'];
    if (!save) throw new Error('Falta la partida de escaparate (.dev-snapshots/trailer/partidas/base.json: npx tsx tools/trailer/escaparate.ts).');
    const c = (await save()).default.campaign;
    stage3d = new Stage();
    // The game's music stays out (the trailer has its own); its sounds play.
    stage3d.soundtrack = false;
    stage3d.element.style.cssText = 'position: absolute; inset: 0';
    stage.append(stage3d.element);
    set = new BastionSet(() => undefined);
    stage3d.add('bastion', set);
    await loadBaseKit();
    set.setBase(c);
    set.setLabels(false);
    // The squad of the showcase game on the pads, in the players' colours.
    const squad = c.squad.map((id) => c.soldiers.find((s) => s.id === id)).filter((s) => !!s);
    set.setLineup(squad.slice(0, 4).map((s): LineupEntry => ({ template: 'rookie', color: PLAYER_COLORS[s.owner], appearance: s.appearance, seed: s.id })));
    await stage3d.show('bastion', set.overview(), true);
    await frames(90);
    move = cameraPath(
      [
        { t: 0, pos: [-15, 15, HANGAR.z + 12], at: [0, 1, 2], fov: 38 },
        { t: 2.8, pos: [4, 8.5, HANGAR.z + 15], at: [3, 1, HANGAR.z - 3], fov: 40 },
        { t: 5.2, pos: [GATE.x + 9.5, 2.0, GATE.z + 6.5], at: [GATE.x + 2.5, 1.4, GATE.z], fov: 44 },
        { t: 6.5, pos: [GATE.x + 11, 2.2, GATE.z + 7.0], at: [GATE.x + 5.5, 1.3, GATE.z - 0.2], fov: 44 },
        { t: 7, pos: [GATE.x + 11.5, 2.25, GATE.z + 7.1], at: [GATE.x + 6.5, 1.3, GATE.z - 0.3], fov: 44 },
      ],
      { in: false, out: true },
    );
    move(0, stage3d.camera);
  },
  frame(t) {
    if (!stage3d || !move) return;
    move(t, stage3d.camera);
    if (!launched && t >= LAUNCH) {
      launched = true;
      void set?.launch();
      // La Lata's engine as it starts and pulls out (the game's own sound).
      sound('truck.engine', { gain: 0.8, delay: 1.6 });
    }
  },
  teardown() {
    stage3d?.dispose();
  },
} satisfies Shot;
