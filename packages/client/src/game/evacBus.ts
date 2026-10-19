import * as THREE from 'three';
import { loadKitModels } from '../strategy/baseKit';
import { buildLata, doorAt, lataModels, type Mula } from '../strategy/mula';
import { NIGHT } from './night';
import { SYNTY_SCALE } from './syntyManifest';
import { tween } from './tween';

// La Mula at a mission's evacuation (docs/PRUEBA-2026-10-14.md P1; UI asked
// for it): the same bus as in the base's hangar (strategy/mula.ts), parked
// where the squad gets out, its doors open to them and its lights off; when
// it goes (the engine's evacDeparted) its doors shut, its lights come on and
// it pulls away, gathering speed, its wheels turning, until it is gone.
// Before it is the bus, La Lata is the campaign's vehicle (GameState.vehicle,
// playtest 18/10 #45): the 4×4, the van or the lorry, which do the same.

/** The bus on the map: a metre of it as the map's props are (SYNTY_SCALE). */
const SCALE = SYNTY_SCALE;
/** Seconds the doors take to shut, and the bus to pull away and be gone. */
const SHUT_SECONDS = 0.35;
const AWAY_SECONDS = 2.2;
/** Seconds it takes to back in to where it parks (its way in and round took 3.1). */
const ARRIVE_SECONDS = 2.6;
/** Seconds it takes to fade in or out where it stands, when its track is built over. */
const FADE_SECONDS = 0.9;

export class EvacBus {
  readonly root = new THREE.Group();
  private mula: Mula | null = null;
  private readonly ready: Promise<void>;

  /** La Lata as `vehicle` (0 4×4, 1 van, 2 lorry; 3 or none, the bus). */
  constructor(readonly vehicle = 3) {
    this.root.visible = false;
    this.ready = loadKitModels(lataModels(vehicle)).then(() => {
      const mula = buildLata(vehicle);
      if (!mula) return;
      mula.body.scale.setScalar(SCALE);
      for (const d of mula.doors) doorAt(d, 1);
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
      for (const d of this.mula.doors) doorAt(d, 1);
    }
  }

  /** How long the bus is, in the map's tiles (once built; 7 until then). */
  get length(): number {
    return this.mula ? this.mula.size.z * SCALE : 7;
  }

  /** How wide, in tiles (once built; 1.7 until then). */
  get width(): number {
    return this.mula ? this.mula.size.x * SCALE : 1.7;
  }

  /** Where one gets in, in its parent's space: the middle of each doorway, on the ground (once built; its doors' side until then). */
  doorways(): THREE.Vector3[] {
    this.root.updateMatrix();
    const m = this.mula;
    if (!m) return [new THREE.Vector3(-0.9, 0, 0).applyMatrix4(this.root.matrix)];
    m.body.updateMatrix();
    return m.doorCentres.map((c) => new THREE.Vector3(c.x, 0, c.z).applyMatrix4(m.body.matrix).applyMatrix4(this.root.matrix));
  }

  /** Resolves once it is built (its size and doors known). */
  loaded(): Promise<void> {
    return this.ready;
  }

  /**
   * Back from a mission to where it is parked (the base's clearing, strategy/clearingArt.ts): backed in along
   * its track from `distance` tiles out, on the line it is parked on (the one it leaves by, nose first), slowing
   * to a stop, nose out, ready to go again; its lights off and its doors open at the end. With `fade` (its way
   * in is built over: clearingArt's route) it fades in where it stands. Resolves once it stands.
   */
  async arrive(distance = 14, fade = false): Promise<void> {
    await this.ready;
    const m = this.mula;
    if (!m) return;
    const park = this.root.position.clone();
    const heading = this.root.rotation.y;
    const out = new THREE.Vector3(Math.sin(heading), 0, Math.cos(heading));
    this.root.visible = true;
    for (const d of m.doors) doorAt(d, 0);
    m.lights.emissiveIntensity = 0.2 + (NIGHT.value > 0.5 ? 1.3 : 2.6);
    if (fade) await this.fade(0, 1);
    else {
      // In backwards, slowing as it comes, its wheels turning back.
      let last = distance;
      await tween(ARRIVE_SECONDS, (t) => {
        const along = distance * (1 - t) * (1 - t);
        this.root.position.copy(park).addScaledVector(out, along);
        for (const w of m.wheels) w.rotation.x -= (last - along) / (m.wheelRadius * SCALE);
        last = along;
      });
    }
    this.root.position.copy(park);
    await tween(SHUT_SECONDS, (t) => {
      for (const d of m.doors) doorAt(d, t);
      m.lights.emissiveIntensity = 0.2 + (1 - t) * (NIGHT.value > 0.5 ? 1.3 : 2.6);
    });
  }

  /**
   * See-through from opacity `from` to `to` (1, solid) where it stands: on copies of its materials meanwhile
   * (the kit's are shared with every other model of it), the kit's back once done; hidden at 0.
   */
  private async fade(from: number, to: number): Promise<void> {
    const m = this.mula;
    if (!m) return;
    const copies = new Map<THREE.Material, THREE.Material>();
    const own = (mat: THREE.Material) => {
      let c = copies.get(mat);
      if (!c) {
        c = mat.clone();
        c.onBeforeCompile = mat.onBeforeCompile;
        c.customProgramCacheKey = mat.customProgramCacheKey;
        c.transparent = true;
        c.opacity = from;
        copies.set(mat, c);
      }
      return c;
    };
    const swapped: [THREE.Mesh, THREE.Material | THREE.Material[]][] = [];
    m.body.traverse((o) => {
      if (!(o instanceof THREE.Mesh)) return;
      swapped.push([o, o.material]);
      o.material = Array.isArray(o.material) ? o.material.map(own) : own(o.material);
    });
    await tween(FADE_SECONDS, (t) => {
      for (const c of copies.values()) c.opacity = from + (to - from) * t;
    });
    if (to <= 0) this.root.visible = false;
    for (const [o, mat] of swapped) o.material = mat;
    for (const c of copies.values()) c.dispose();
  }

  /**
   * Its doors shut, its lights on, then away along its nose `distance` tiles, gathering speed; resolves once it
   * is gone. With `fade` (its way out is built over: clearingArt's route) it fades away where it stands.
   */
  async depart(distance = 12, fade = false): Promise<void> {
    await this.ready;
    const m = this.mula;
    if (!m || !this.root.visible) return;
    await tween(SHUT_SECONDS, (t) => {
      for (const d of m.doors) doorAt(d, 1 - t);
      // By night they would dazzle (Arturo 17/10): half as bright.
      m.lights.emissiveIntensity = 0.2 + t * (NIGHT.value > 0.5 ? 1.3 : 2.6);
    });
    if (fade) return this.fade(1, 0);
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
    // Gone, it is back where it parks (out of sight): the way it comes back to (arrive).
    this.root.position.copy(from);
  }
}
