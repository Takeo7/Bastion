import * as THREE from 'three';
import { buildModel, type UnitRig } from '../game/models';
import { buildFigure, type FigureRig } from '../game/figures';
import type { ActorDef } from './layout';
import type { FxKind } from './stylekit';
import { glowTexture } from './textures';

// Units, effects and a looping skirmish for the style lab. Everything runs on
// the lab's own clock (not requestAnimationFrame) so it can be paused and
// stepped frame by frame for captures.

export class Clock {
  time = 0;
  private tasks: { start: number; seconds: number; fn: (t: number) => void; done: () => void }[] = [];

  tween(seconds: number, fn: (t: number) => void): Promise<void> {
    return new Promise((done) => this.tasks.push({ start: this.time, seconds, fn, done }));
  }

  wait(seconds: number): Promise<void> {
    return this.tween(seconds, () => {});
  }

  advance(dt: number): void {
    this.time += dt;
    const finished: (() => void)[] = [];
    this.tasks = this.tasks.filter((task) => {
      const t = Math.min(1, (this.time - task.start) / task.seconds);
      task.fn(t);
      if (t >= 1) finished.push(task.done);
      return t < 1;
    });
    for (const done of finished) done();
  }

  clear(): void {
    this.tasks = [];
  }
}

const ease = (t: number) => (t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2);

interface Pose {
  aim: number;
  recoil: number;
  hit: number;
  flash: number;
  lean: number;
  crouch: number;
  run: number;
  fall: number;
  hop: number;
  yaw: number;
  phase: number;
}

/** Which unit models to use: the articulated figures, or the game's current placeholders. */
export type ModelSet = 'figures' | 'placeholders';

export interface Actor {
  def: ActorDef;
  rig: UnitRig;
  pose: Pose;
}

/** Hook a style uses to restyle a freshly built rig (materials, voxels, outlines...). */
export type RigTransform = (rig: UnitRig, def: ActorDef) => void;

export class Actors {
  readonly group = new THREE.Group();
  readonly byId = new Map<string, Actor>();

  constructor(defs: ActorDef[], transform: RigTransform | null, models: ModelSet = 'figures') {
    for (const def of defs) {
      const rig = models === 'figures' ? buildFigure(def.template, def.color) : buildModel(def.template, def.color);
      transform?.(rig, def);
      rig.root.position.set(def.x + 0.5, 0, def.z + 0.5);
      const yaw = def.facing ?? (def.team === 'xcom' ? Math.PI / 2 : -Math.PI / 2);
      const pose: Pose = { aim: 0, recoil: 0, hit: 0, flash: 0, lean: 0, crouch: 0, run: 0, fall: 0, hop: 0, yaw, phase: Math.random() * 6 };
      const actor: Actor = { def, rig, pose };
      this.byId.set(def.id, actor);
      this.group.add(rig.root);
    }
    this.byId.get('assault')!.pose.crouch = 0.6;
  }

  /** Back to the opening stance (used when the skirmish restarts without a rebuild). */
  resetPoses(): void {
    for (const { def, pose, rig } of this.byId.values()) {
      rig.root.position.set(def.x + 0.5, 0, def.z + 0.5);
      pose.aim = pose.recoil = pose.hit = pose.flash = pose.lean = pose.run = pose.fall = pose.hop = 0;
      pose.crouch = def.id === 'assault' ? 0.6 : 0;
      pose.yaw = def.facing ?? (def.team === 'xcom' ? Math.PI / 2 : -Math.PI / 2);
    }
  }

  get(id: string): Actor {
    return this.byId.get(id)!;
  }

  tick(time: number): void {
    for (const { rig, pose: p } of this.byId.values()) {
      if ('applyPose' in rig) {
        (rig as FigureRig).applyPose(p, time);
        if (rig.drone) {
          rig.drone.position.y = 1.9 + Math.sin(time * 2.4 + p.phase) * 0.07;
          rig.drone.rotation.y = time * 1.6;
        }
        continue;
      }
      const breathe = Math.sin(time * 2.1 + p.phase) * 0.012;
      rig.yaw.rotation.y = p.yaw;
      rig.pivot.rotation.x = p.lean - p.hit * 0.38 - p.recoil * 0.05;
      rig.pivot.position.y = rig.hip - p.crouch * 0.2;
      rig.pivot.scale.y = (1 + breathe) * (1 - p.crouch * 0.12);
      rig.gunPivot.rotation.x = (1 - p.aim) * 0.6 - p.recoil * 0.18;
      rig.gunPivot.position.z = 0.06 - p.recoil * 0.09;
      rig.flash.visible = p.flash > 0.01;
      (rig.flash.material as THREE.MeshBasicMaterial).opacity = p.flash * 0.6;
      if (rig.drone) {
        rig.drone.position.y = 1.75 + Math.sin(time * 2.4 + p.phase) * 0.07;
        rig.drone.rotation.y = time * 1.6;
      }
    }
  }

  muzzle(id: string): THREE.Vector3 {
    const a = this.get(id);
    a.rig.root.updateMatrixWorld(true);
    return a.rig.muzzle.getWorldPosition(new THREE.Vector3());
  }

  chest(id: string): THREE.Vector3 {
    const a = this.get(id);
    return a.rig.root.position.clone().setY(a.rig.chest);
  }

  face(id: string, target: THREE.Vector3): void {
    const a = this.get(id);
    const d = target.clone().sub(a.rig.root.position);
    a.pose.yaw = Math.atan2(d.x, d.z);
  }
}

// ------------------------------------------------------------------- fx

interface Particle {
  obj: THREE.Object3D;
  material: THREE.Material & { opacity: number };
  velocity: THREE.Vector3;
  spin: THREE.Vector3;
  gravity: number;
  age: number;
  life: number;
  grow: number;
  base: number;
  opacity: number;
}

const streak = new THREE.CylinderGeometry(0.02, 0.02, 0.9, 5, 1, true).rotateX(Math.PI / 2);
const ball = new THREE.SphereGeometry(1, 20, 14);
const shell = new THREE.SphereGeometry(0.09, 10, 8);
const cube = new THREE.BoxGeometry(0.12, 0.12, 0.12);
const ring = new THREE.RingGeometry(0.85, 1, 48).rotateX(-Math.PI / 2);

/** Muzzle flashes, tracers, grenades and explosions. Two pooled lights avoid shader recompiles. */
export class LabFx {
  readonly group = new THREE.Group();
  /** Notified of notable moments (shots, grenades, blasts) for 2D overlays. */
  onEvent: ((kind: FxKind, at: THREE.Vector3) => void) | null = null;
  private readonly glow = glowTexture();
  private particles: Particle[] = [];
  private readonly lights: THREE.PointLight[] = [];

  constructor(
    private readonly clock: Clock,
    withLights: boolean,
    private readonly debrisMaterial: (color: number) => THREE.Material & { opacity: number },
  ) {
    if (withLights) {
      for (let i = 0; i < 2; i++) {
        const l = new THREE.PointLight(0xffffff, 0, 8, 2);
        this.lights.push(l);
        this.group.add(l);
      }
    }
  }

  /** Drops every effect in flight (their tweens die with the cleared clock). */
  reset(): void {
    this.particles = [];
    for (const child of [...this.group.children]) {
      if (!this.lights.includes(child as THREE.PointLight)) this.group.remove(child);
    }
    for (const l of this.lights) l.intensity = 0;
  }

  emit(kind: FxKind, at: THREE.Vector3): void {
    this.onEvent?.(kind, at.clone());
  }

  tick(dt: number): void {
    this.particles = this.particles.filter((p) => {
      p.age += dt;
      const t = p.age / p.life;
      if (t >= 1) {
        this.group.remove(p.obj);
        return false;
      }
      p.velocity.y -= p.gravity * dt;
      p.obj.position.addScaledVector(p.velocity, dt);
      if (p.gravity && p.obj.position.y < 0.06) {
        p.obj.position.y = 0.06;
        p.velocity.multiplyScalar(0.5);
        p.velocity.y *= -0.6;
      }
      p.obj.rotation.x += p.spin.x * dt;
      p.obj.rotation.y += p.spin.y * dt;
      p.obj.scale.setScalar(p.base * (1 + p.grow * t));
      p.material.opacity = p.opacity * (1 - t * t);
      return true;
    });
  }

  private spawn(obj: THREE.Object3D, material: Particle['material'], o: Partial<Particle> = {}): void {
    this.particles.push({
      obj,
      material,
      velocity: o.velocity ?? new THREE.Vector3(),
      spin: o.spin ?? new THREE.Vector3(),
      gravity: o.gravity ?? 0,
      age: 0,
      life: o.life ?? 0.5,
      grow: o.grow ?? 0,
      base: o.base ?? obj.scale.x,
      opacity: o.opacity ?? 1,
    });
    this.group.add(obj);
  }

  private sprite(color: number, scale: number, opacity = 1, additive = true): THREE.Sprite {
    const material = new THREE.SpriteMaterial({
      map: this.glow,
      color,
      transparent: true,
      opacity,
      blending: additive ? THREE.AdditiveBlending : THREE.NormalBlending,
      depthWrite: false,
    });
    const s = new THREE.Sprite(material);
    s.scale.setScalar(scale);
    return s;
  }

  private flashLight(at: THREE.Vector3, color: number, intensity: number, seconds: number, slot: number): void {
    const l = this.lights[slot];
    if (!l) return;
    l.color.set(color);
    l.position.copy(at);
    l.distance = intensity > 20 ? 12 : 6;
    void this.clock.tween(seconds, (t) => (l.intensity = intensity * (1 - t)));
  }

  muzzleFlash(at: THREE.Vector3, color: number): void {
    const s = this.sprite(color, 0.75);
    s.position.copy(at);
    this.spawn(s, s.material, { life: 0.09, grow: 0.6 });
    const core = this.sprite(0xffffff, 0.35);
    core.position.copy(at);
    this.spawn(core, core.material, { life: 0.06 });
    this.flashLight(at, color, 10, 0.09, 0);
  }

  async bullet(from: THREE.Vector3, to: THREE.Vector3, color: number): Promise<void> {
    const material = new THREE.MeshBasicMaterial({ color, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false });
    const m = new THREE.Mesh(streak, material);
    m.position.copy(from);
    m.lookAt(to);
    this.group.add(m);
    await this.clock.tween(Math.max(0.05, from.distanceTo(to) / 55), (t) => m.position.lerpVectors(from, to, t));
    this.group.remove(m);
  }

  sparks(at: THREE.Vector3, color: number, count = 10): void {
    for (let i = 0; i < count; i++) {
      const s = this.sprite(color, 0.13 + Math.random() * 0.08);
      s.position.copy(at);
      const dir = new THREE.Vector3(Math.random() - 0.5, Math.random() * 0.8, Math.random() - 0.5).normalize();
      this.spawn(s, s.material, { velocity: dir.multiplyScalar(2.5 + Math.random() * 3), gravity: 9, life: 0.3 + Math.random() * 0.25 });
    }
  }

  debris(at: THREE.Vector3, color: number, count: number, speed: number): void {
    for (let i = 0; i < count; i++) {
      const material = this.debrisMaterial(color);
      const piece = new THREE.Mesh(cube, material);
      piece.castShadow = true;
      piece.position.copy(at).setY(Math.max(0.3, at.y));
      const dir = new THREE.Vector3(Math.random() - 0.5, 0.6 + Math.random() * 0.9, Math.random() - 0.5).normalize();
      this.spawn(piece, material, {
        velocity: dir.multiplyScalar(speed * (0.6 + Math.random() * 0.6)),
        spin: new THREE.Vector3(Math.random() * 12, Math.random() * 12, 0),
        gravity: 14,
        life: 1.5 + Math.random() * 0.6,
        base: 0.6 + Math.random() * 1.2,
      });
    }
  }

  async grenade(from: THREE.Vector3, to: THREE.Vector3): Promise<void> {
    const m = new THREE.Mesh(shell, new THREE.MeshStandardMaterial({ color: 0x3b4a3a }));
    m.castShadow = true;
    this.group.add(m);
    const h = 1.5 + from.distanceTo(to) * 0.3;
    await this.clock.tween(0.55 + from.distanceTo(to) * 0.03, (t) => {
      m.position.lerpVectors(from, to, t);
      m.position.y += Math.sin(t * Math.PI) * h;
      m.rotation.x += 0.4;
    });
    this.group.remove(m);
  }

  explosion(at: THREE.Vector3, radius: number): Promise<void> {
    const fireMat = new THREE.MeshBasicMaterial({ color: 0xffb050, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false });
    const fire = new THREE.Mesh(ball, fireMat);
    fire.position.copy(at).setY(0.6);
    const shockMat = new THREE.MeshBasicMaterial({ color: 0xffd9a0, transparent: true, opacity: 0.8, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
    const shock = new THREE.Mesh(ring, shockMat);
    shock.position.copy(at).setY(0.05);
    this.group.add(fire, shock);
    this.flashLight(at.clone().setY(1.5), 0xff8a30, 45, 0.6, 1);
    this.debris(at, 0x2c2f33, 14, 7);
    for (let i = 0; i < 7; i++) {
      const smoke = this.sprite(0x3a3a3a, 1.2, 0.55, false);
      smoke.position.copy(at).add(new THREE.Vector3((Math.random() - 0.5) * radius, 0.4 + Math.random(), (Math.random() - 0.5) * radius));
      this.spawn(smoke, smoke.material, { velocity: new THREE.Vector3(0, 0.6 + Math.random() * 0.5, 0), life: 1.8 + Math.random(), grow: 2, opacity: 0.55 });
    }
    return this.clock
      .tween(0.6, (t) => {
        fire.scale.setScalar(0.3 + radius * Math.sqrt(t));
        fireMat.opacity = 0.95 * (1 - t);
        fireMat.color.setHSL(0.08 - t * 0.06, 1, 0.6 - t * 0.3);
        shock.scale.setScalar(0.5 + radius * 1.4 * t);
        shockMat.opacity = 0.8 * (1 - t);
      })
      .then(() => {
        this.group.remove(fire, shock);
      });
  }
}

// ------------------------------------------------------------- skirmish

async function dash(clock: Clock, actors: Actors, id: string, to: THREE.Vector3): Promise<void> {
  const a = actors.get(id);
  const from = a.rig.root.position.clone();
  actors.face(id, to);
  void clock.tween(0.15, (t) => (a.pose.run = Math.max(a.pose.run, t)));
  await clock.tween(from.distanceTo(to) / 3.4, (t) => a.rig.root.position.lerpVectors(from, to, t));
  void clock.tween(0.2, (t) => (a.pose.run = 1 - t));
}

/**
 * One beat of the looping skirmish (≈6 s). Actions overlap on purpose: at
 * t ≈ 1.45 s there is a muzzle flash, tracers and an explosion on screen.
 */
export async function skirmish(clock: Clock, actors: Actors, fx: LabFx, alive: () => boolean): Promise<void> {
  const XCOM = 0x9fe3ff;
  const ALIEN = 0xff5a4a;

  const burst = async (shooter: string, target: string, color: number, hit: boolean, missAt?: THREE.Vector3) => {
    const s = actors.get(shooter);
    const aimAt = actors.chest(target);
    actors.face(shooter, aimAt);
    await clock.tween(0.25, (t) => (s.pose.aim = ease(t)));
    fx.emit(s.def.team === 'xcom' ? 'xcomShot' : 'alienShot', actors.muzzle(shooter));
    for (let i = 0; i < 3; i++) {
      if (!alive()) return;
      const from = actors.muzzle(shooter);
      fx.muzzleFlash(from, color);
      void clock.tween(0.08, (t) => (s.pose.recoil = Math.sin(t * Math.PI)));
      const to = hit ? aimAt.clone().add(new THREE.Vector3(0, (Math.random() - 0.5) * 0.2, 0)) : (missAt ?? aimAt).clone();
      void fx.bullet(from, to, color).then(() => {
        if (!hit) fx.sparks(to, 0xffd27a, 6);
      });
      await clock.wait(0.12);
    }
    if (hit) {
      await clock.wait(0.08);
      const t = actors.get(target);
      fx.sparks(aimAt, color, 12);
      fx.emit('hit', aimAt);
      void clock.tween(0.4, (k) => {
        t.pose.hit = Math.sin(Math.min(1, k * 1.6) * Math.PI) * 0.6;
        t.pose.flash = 1 - k;
      });
    }
    await clock.wait(0.35);
    void clock.tween(0.35, (t) => (s.pose.aim = 1 - ease(t)));
  };

  const throwGrenade = async (thrower: string, at: THREE.Vector3) => {
    const a = actors.get(thrower);
    actors.face(thrower, at);
    await clock.tween(0.22, (t) => (a.pose.lean = -0.35 * ease(t)));
    await clock.tween(0.14, (t) => (a.pose.lean = -0.35 + 0.65 * t));
    void clock.tween(0.25, (t) => (a.pose.lean = 0.3 * (1 - t)));
    fx.emit('grenade', actors.chest(thrower).setY(1.6));
    await fx.grenade(actors.chest(thrower).setY(1.6), at);
    fx.emit('explosion', at.clone().setY(1));
    void fx.explosion(at, 2);
    for (const id of ['officer', 'trooper']) {
      const t = actors.get(id);
      void clock.tween(0.45, (k) => {
        t.pose.hit = Math.sin(Math.min(1, k * 1.6) * Math.PI) * 0.8;
        t.pose.flash = 1 - k;
      });
    }
  };

  const grenade = throwGrenade('grenadier', new THREE.Vector3(13.6, 0, 6.2));
  await clock.wait(0.95);
  if (!alive()) return;
  await burst('sharp', 'xenoid', XCOM, true);
  await grenade;
  await clock.wait(0.7);
  if (!alive()) return;
  await burst('mec', 'sharp', ALIEN, false, new THREE.Vector3(6.05, 1.0, 5.5));
  fx.debris(new THREE.Vector3(6.05, 1.0, 5.5), 0x2f5a78, 5, 3);
  await clock.wait(0.6);
  if (!alive()) return;
  await burst('trooper', 'assault', ALIEN, false, new THREE.Vector3(5.6, 0.75, 8.2));
  fx.debris(new THREE.Vector3(5.6, 0.75, 8.2), 0xa08f62, 6, 3);
  await clock.wait(0.5);
  if (!alive()) return;
  // The specialist dashes to the corner of the van and back: shows the run cycle.
  await dash(clock, actors, 'spec', new THREE.Vector3(6.7, 0, 3.4));
  await clock.wait(0.6);
  if (!alive()) return;
  await dash(clock, actors, 'spec', new THREE.Vector3(4.5, 0, 4.5));
  actors.get('spec').pose.yaw = Math.PI / 2;
  await clock.wait(0.6);
}
