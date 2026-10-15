import * as THREE from 'three';
import { loadKitModels } from '../strategy/baseKit';
import { buildMula, MULA_MODELS, type Mula } from '../strategy/mula';
import { SYNTY_SCALE } from './syntyManifest';
import { tween } from './tween';

// La Mula at a mission's evacuation (docs/PRUEBA-2026-10-14.md P1; UI asked
// for it): the same bus as in the base's hangar (strategy/mula.ts), parked
// where the squad gets out, its doors open to them and its lights off; when
// it goes (the engine's evacDeparted) its doors shut, its lights come on and
// it pulls away, gathering speed, its wheels turning, until it is gone.

/** The bus on the map: a metre of it as the map's props are (SYNTY_SCALE). */
const SCALE = SYNTY_SCALE;
/** Seconds the doors take to shut, and the bus to pull away and be gone. */
const SHUT_SECONDS = 0.35;
const AWAY_SECONDS = 2.2;

export class EvacBus {
  readonly root = new THREE.Group();
  private mula: Mula | null = null;
  private readonly ready: Promise<void>;

  constructor() {
    this.root.visible = false;
    this.ready = loadKitModels(MULA_MODELS).then(() => {
      const mula = buildMula();
      if (!mula) return;
      mula.body.scale.setScalar(SCALE);
      for (const d of mula.doors) d.leaf.position.copy(d.open);
      mula.body.traverse((o) => {
        if (o instanceof THREE.Mesh) {
          o.castShadow = true;
          o.receiveShadow = true;
        }
      });
      this.root.add(mula.body);
      this.mula = mula;
    });
  }

  /**
   * Parked at `at` (the map's world), its nose the way `heading` (radians
   * from +z, as the units' yaw), its doors on the side towards `doorsTo`
   * (mirrored if they'd face away), shown or not.
   */
  park(at: THREE.Vector3, heading: number, doorsTo: THREE.Vector3, shown = true): void {
    this.root.position.copy(at);
    this.root.rotation.set(0, heading, 0);
    // The doors are on the bus's −x side: (−cos, 0, sin) of its heading in the world.
    const doors = new THREE.Vector3(-Math.cos(heading), 0, Math.sin(heading));
    this.root.scale.set(doors.dot(doorsTo.clone().sub(at)) < 0 ? -1 : 1, 1, 1);
    this.root.visible = shown;
    if (this.mula) {
      this.mula.lights.emissiveIntensity = 0.2;
      for (const d of this.mula.doors) d.leaf.position.copy(d.open);
    }
  }

  /** How long the bus is, in the map's tiles (once built; 7 until then). */
  get length(): number {
    return this.mula ? this.mula.size.z * SCALE : 7;
  }

  /** Its doors shut, its lights on, then away along its nose `distance` tiles, gathering speed; resolves once it is gone. */
  async depart(distance = 12): Promise<void> {
    await this.ready;
    const m = this.mula;
    if (!m || !this.root.visible) return;
    await tween(SHUT_SECONDS, (t) => {
      for (const d of m.doors) d.leaf.position.lerpVectors(d.open, d.shut, t);
      m.lights.emissiveIntensity = 0.2 + t * 2.6;
    });
    const from = this.root.position.clone();
    const ahead = new THREE.Vector3(Math.sin(this.root.rotation.y), 0, Math.cos(this.root.rotation.y));
    let last = 0;
    await tween(AWAY_SECONDS, (t) => {
      // From standing, faster and faster.
      const along = distance * t * t;
      this.root.position.copy(from).addScaledVector(ahead, along);
      const turned = (along - last) / (m.wheelRadius * SCALE);
      last = along;
      for (const w of m.wheels) w.rotation.x += turned;
    });
    this.root.visible = false;
  }
}
