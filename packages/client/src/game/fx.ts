import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { tween, wait } from './tween';

/** Soft radial glow used for flashes, sparks and smoke. */
function glowTexture(): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const g = c.getContext('2d')!;
  const grad = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  grad.addColorStop(0, 'rgba(255,255,255,1)');
  grad.addColorStop(0.35, 'rgba(255,255,255,0.6)');
  grad.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = grad;
  g.fillRect(0, 0, 64, 64);
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

interface Particle {
  obj: THREE.Object3D;
  material: THREE.Material & { opacity: number };
  velocity: THREE.Vector3;
  spin: THREE.Vector3;
  gravity: number;
  age: number;
  life: number;
  grow: number;
  base: THREE.Vector3;
  opacity: number;
}

const streakGeometry = new THREE.CylinderGeometry(0.018, 0.018, 0.9, 5, 1, true).rotateX(Math.PI / 2);
const heavyStreakGeometry = new THREE.CylinderGeometry(0.04, 0.04, 1.6, 6, 1, true).rotateX(Math.PI / 2);
const slashGeometry = new THREE.RingGeometry(0.45, 0.6, 24, 1, 0, Math.PI * 0.85);
const bubbleGeometry = new THREE.SphereGeometry(0.85, 20, 14);
const missileGeometry = new THREE.CylinderGeometry(0.04, 0.05, 0.32, 6).rotateX(Math.PI / 2);
const beamGeometry = new THREE.CylinderGeometry(0.5, 0.5, 14, 20, 1, true);
const pingRingGeometry = new THREE.RingGeometry(0.42, 0.55, 32).rotateX(-Math.PI / 2);
const droneBodyGeometry = new THREE.OctahedronGeometry(0.13, 0);
const droneRingGeometry = new THREE.TorusGeometry(0.16, 0.02, 6, 20).rotateX(Math.PI / 2);
const blastGeometry = new THREE.SphereGeometry(1, 24, 16);
const shellGeometry = new THREE.SphereGeometry(0.09, 10, 8);
const debrisGeometry = new THREE.BoxGeometry(0.12, 0.12, 0.12);
const shockGeometry = new THREE.RingGeometry(0.85, 1, 48).rotateX(-Math.PI / 2);

/** Transient visual effects. Particles advance in `tick`, driven by the render loop. */
export class Fx {
  readonly group = new THREE.Group();
  private readonly glow = glowTexture();
  private particles: Particle[] = [];

  tick(dt: number): void {
    if (!this.particles.length) return;
    this.particles = this.particles.filter((p) => {
      p.age += dt;
      const t = p.age / p.life;
      if (t >= 1) {
        this.group.remove(p.obj);
        p.material.dispose();
        return false;
      }
      p.velocity.y -= p.gravity * dt;
      p.obj.position.addScaledVector(p.velocity, dt);
      if (p.gravity && p.obj.position.y < 0.06) {
        p.obj.position.y = 0.06;
        p.velocity.y *= -0.35;
        p.velocity.x *= 0.55;
        p.velocity.z *= 0.55;
      }
      p.obj.rotation.x += p.spin.x * dt;
      p.obj.rotation.y += p.spin.y * dt;
      p.obj.scale.copy(p.base).multiplyScalar(1 + p.grow * t);
      p.material.opacity = p.opacity * (1 - t * t);
      return true;
    });
  }

  private spawn(
    obj: THREE.Object3D,
    material: Particle['material'],
    opts: Partial<Omit<Particle, 'obj' | 'material' | 'base'>> & { baseScale?: number },
  ): void {
    const p: Particle = {
      obj,
      material,
      velocity: opts.velocity ?? new THREE.Vector3(),
      spin: opts.spin ?? new THREE.Vector3(),
      gravity: opts.gravity ?? 0,
      age: 0,
      life: opts.life ?? 0.5,
      grow: opts.grow ?? 0,
      base: opts.baseScale !== undefined ? new THREE.Vector3().setScalar(opts.baseScale) : obj.scale.clone(),
      opacity: opts.opacity ?? 1,
    };
    this.particles.push(p);
    this.group.add(obj);
  }

  private glowSprite(color: number, scale: number, opacity = 1): THREE.Sprite {
    const material = new THREE.SpriteMaterial({ map: this.glow, color, transparent: true, opacity, blending: THREE.AdditiveBlending, depthWrite: false });
    const sprite = new THREE.Sprite(material);
    sprite.scale.setScalar(scale);
    return sprite;
  }

  // -------------------------------------------------------------- weapons

  muzzleFlash(at: THREE.Vector3, color: number): void {
    const sprite = this.glowSprite(color, 0.7);
    sprite.position.copy(at);
    this.spawn(sprite, sprite.material, { life: 0.09, grow: 0.6 });
    const core = this.glowSprite(0xffffff, 0.35);
    core.position.copy(at);
    this.spawn(core, core.material, { life: 0.06 });
    const light = new THREE.PointLight(color, 9, 6, 2);
    light.position.copy(at);
    this.group.add(light);
    void tween(0.08, (t) => (light.intensity = 9 * (1 - t))).then(() => this.group.remove(light));
  }

  /** A bright streak travelling from `from` to `to`; `heavy` for sniper rounds. */
  async bullet(from: THREE.Vector3, to: THREE.Vector3, color: number, speed = 60, heavy = false): Promise<void> {
    const material = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 1, blending: THREE.AdditiveBlending, depthWrite: false });
    const streak = new THREE.Mesh(heavy ? heavyStreakGeometry : streakGeometry, material);
    streak.position.copy(from);
    streak.lookAt(to);
    this.group.add(streak);
    const dist = from.distanceTo(to);
    await tween(Math.max(0.05, dist / speed), (t) => streak.position.lerpVectors(from, to, t));
    this.group.remove(streak);
    material.dispose();
  }

  sparks(at: THREE.Vector3, color: number, count = 9): void {
    for (let i = 0; i < count; i++) {
      const s = this.glowSprite(color, 0.12 + Math.random() * 0.08);
      s.position.copy(at);
      const dir = new THREE.Vector3(Math.random() - 0.5, Math.random() * 0.8, Math.random() - 0.5).normalize();
      this.spawn(s, s.material, { velocity: dir.multiplyScalar(2.5 + Math.random() * 3), gravity: 9, life: 0.3 + Math.random() * 0.25 });
    }
    const puff = this.glowSprite(color, 0.5, 0.8);
    puff.position.copy(at);
    this.spawn(puff, puff.material, { life: 0.15, grow: 1 });
  }

  /** Blade arc flashing across the target. */
  slash(at: THREE.Vector3, color: number): void {
    const material = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 1, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
    const arc = new THREE.Mesh(slashGeometry, material);
    arc.position.copy(at);
    arc.rotation.set(Math.random() * 0.6 - 0.3, Math.random() * Math.PI, Math.random() * Math.PI);
    this.spawn(arc, material, { life: 0.28, grow: 0.6 });
    this.sparks(at, color, 10);
  }

  /** Green motes rising from a healed unit. */
  heal(at: THREE.Vector3): void {
    for (let i = 0; i < 14; i++) {
      const s = this.glowSprite(0x6dff9a, 0.16);
      s.position.copy(at).add(new THREE.Vector3((Math.random() - 0.5) * 0.7, (Math.random() - 0.5) * 0.9, (Math.random() - 0.5) * 0.7));
      this.spawn(s, s.material, { velocity: new THREE.Vector3(0, 0.8 + Math.random() * 0.8, 0), life: 0.9 + Math.random() * 0.5 });
    }
  }

  /** Brief protective bubble (Aid Protocol). */
  shield(at: THREE.Vector3): void {
    const material = new THREE.MeshBasicMaterial({ color: 0x54d1ff, transparent: true, opacity: 0.35, blending: THREE.AdditiveBlending, depthWrite: false });
    const bubble = new THREE.Mesh(bubbleGeometry, material);
    bubble.position.copy(at);
    this.spawn(bubble, material, { life: 1.1, grow: 0.25, opacity: 0.35 });
  }

  /** The support drone flying from one point to another along a small arc. */
  async droneFlight(from: THREE.Vector3, to: THREE.Vector3): Promise<void> {
    const drone = new THREE.Group();
    drone.add(
      new THREE.Mesh(droneBodyGeometry, new THREE.MeshStandardMaterial({ color: 0xb8c4cc, metalness: 0.6, roughness: 0.3 })),
      new THREE.Mesh(droneRingGeometry, new THREE.MeshStandardMaterial({ color: 0x7dffb0, emissive: 0x2fd97a, emissiveIntensity: 1.6 })),
    );
    const light = new THREE.PointLight(0x7dffb0, 3, 3, 2);
    drone.add(light);
    this.group.add(drone);
    const seconds = 0.35 + from.distanceTo(to) * 0.03;
    await tween(seconds, (t) => {
      drone.position.lerpVectors(from, to, t);
      drone.position.y += Math.sin(t * Math.PI) * 0.8;
      drone.rotation.y += 0.3;
    });
    this.group.remove(drone);
  }

  /** A volley of small missiles converging on `targets`; resolves when the last one lands. */
  async missiles(from: THREE.Vector3, targets: THREE.Vector3[]): Promise<void> {
    const flights = targets.map(async (to, i) => {
      await wait(i * 0.09);
      const material = new THREE.MeshStandardMaterial({ color: 0x9aa3ad, emissive: 0xff7a2a, emissiveIntensity: 0.6 });
      const missile = new THREE.Mesh(missileGeometry, material);
      this.group.add(missile);
      const height = 2.5 + from.distanceTo(to) * 0.2;
      let last = from.clone();
      await tween(0.7, (t) => {
        missile.position.lerpVectors(from, to, t);
        missile.position.y += Math.sin(t * Math.PI) * height;
        missile.lookAt(missile.position.clone().add(missile.position.clone().sub(last)));
        if (missile.position.distanceTo(last) > 0.4) {
          const puff = this.glowSprite(0x777777, 0.3, 0.5);
          puff.position.copy(missile.position);
          this.spawn(puff, puff.material, { life: 0.6, grow: 1.5, opacity: 0.5 });
        }
        last = missile.position.clone();
      });
      this.group.remove(missile);
      material.dispose();
    });
    await Promise.all(flights);
  }

  /** Column of light where a soldier is picked up. */
  evacBeam(at: THREE.Vector3): void {
    const material = new THREE.MeshBasicMaterial({ color: 0x8fffc0, transparent: true, opacity: 0.45, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
    const beam = new THREE.Mesh(beamGeometry, material);
    beam.position.copy(at).setY(at.y + 7);
    this.spawn(beam, material, { life: 1.6, opacity: 0.45 });
  }

  /** Player marker: a coloured beam and pulsing rings for a few seconds. */
  ping(at: THREE.Vector3, color: string, enemy: boolean): void {
    const c = new THREE.Color(enemy ? '#ff4a4a' : color);
    const beamMat = new THREE.MeshBasicMaterial({ color: c, transparent: true, opacity: 0.5, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
    const beam = new THREE.Mesh(beamGeometry, beamMat);
    beam.scale.set(0.12, 0.35, 0.12);
    beam.position.copy(at).setY(at.y + 2.45);
    this.spawn(beam, beamMat, { life: 3.5, opacity: 0.5 });
    for (let i = 0; i < 3; i++) {
      void wait(i * 0.45).then(() => {
        const ringMat = new THREE.MeshBasicMaterial({ color: c, transparent: true, opacity: 0.9, depthWrite: false });
        const ring = new THREE.Mesh(pingRingGeometry, ringMat);
        ring.position.copy(at).setY(at.y + 0.06);
        this.spawn(ring, ringMat, { life: 1.1, grow: 1.6, opacity: 0.9 });
      });
    }
  }

  // ----------------------------------------------------------- explosives

  async grenadeArc(from: THREE.Vector3, to: THREE.Vector3): Promise<void> {
    const shell = new THREE.Mesh(shellGeometry, new THREE.MeshStandardMaterial({ color: 0x3b4a3a }));
    shell.castShadow = true;
    this.group.add(shell);
    const height = 1.5 + from.distanceTo(to) * 0.3;
    await tween(0.55 + from.distanceTo(to) * 0.03, (t) => {
      shell.position.lerpVectors(from, to, t);
      shell.position.y += Math.sin(t * Math.PI) * height;
      shell.rotation.x += 0.4;
    });
    this.group.remove(shell);
  }

  /** Fireball (or an electric burst for the drone's discharge) with shockwave, debris and smoke. */
  explosion(at: THREE.Vector3, radius: number, electric = false): Promise<void> {
    const fireMat = new THREE.MeshBasicMaterial({ color: electric ? 0x8ad8ff : 0xffb050, transparent: true, opacity: 0.95, blending: THREE.AdditiveBlending, depthWrite: false });
    const fire = new THREE.Mesh(blastGeometry, fireMat);
    fire.position.copy(at).setY(at.y + 0.45);
    const light = new THREE.PointLight(electric ? 0x5ac8ff : 0xff8a30, 45, radius * 6, 2);
    light.position.copy(at).setY(at.y + 1.35);
    const shockMat = new THREE.MeshBasicMaterial({ color: electric ? 0xbfefff : 0xffd9a0, transparent: true, opacity: 0.8, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide });
    const shock = new THREE.Mesh(shockGeometry, shockMat);
    shock.position.copy(at).setY(at.y - 0.1);
    this.group.add(fire, light, shock);

    if (electric) {
      for (let i = 0; i < 3; i++) this.sparks(at.clone().add(new THREE.Vector3((Math.random() - 0.5) * radius, 0.6, (Math.random() - 0.5) * radius)), 0x8ad8ff, 10);
    } else {
      for (let i = 0; i < 14; i++) this.debrisPiece(at, i % 3 === 0 ? 0x5a4a3a : 0x2c2f33, 5 + Math.random() * 4);
    }
    for (let i = 0; i < (electric ? 0 : 7); i++) {
      const smoke = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.glow, color: 0x3a3a3a, transparent: true, opacity: 0.55, depthWrite: false }));
      smoke.position.copy(at).add(new THREE.Vector3((Math.random() - 0.5) * radius, 0.4 + Math.random(), (Math.random() - 0.5) * radius));
      smoke.scale.setScalar(1.2);
      this.spawn(smoke, smoke.material, { velocity: new THREE.Vector3(0, 0.6 + Math.random() * 0.5, 0), life: 1.8 + Math.random(), grow: 2, opacity: 0.55 });
    }

    return tween(0.6, (t) => {
      fire.scale.setScalar(0.3 + radius * Math.sqrt(t));
      fireMat.opacity = 0.95 * (1 - t);
      if (!electric) fireMat.color.setHSL(0.08 - t * 0.06, 1, 0.6 - t * 0.3);
      light.intensity = 45 * (1 - t);
      shock.scale.setScalar(0.5 + radius * 1.4 * t);
      shockMat.opacity = 0.8 * (1 - t);
    }).then(() => {
      this.group.remove(fire, light, shock);
      fireMat.dispose();
      shockMat.dispose();
    });
  }

  /** Chunks flying out of a destroyed cover block. */
  debris(at: THREE.Vector3, color: number, count = 8): void {
    for (let i = 0; i < count; i++) this.debrisPiece(at, color, 3 + Math.random() * 2);
  }

  private debrisPiece(at: THREE.Vector3, color: number, speed: number): void {
    const material = new THREE.MeshStandardMaterial({ color, transparent: true, opacity: 1 });
    const piece = new THREE.Mesh(debrisGeometry, material);
    piece.castShadow = true;
    piece.position.copy(at).setY(Math.max(0.3, at.y));
    const dir = new THREE.Vector3(Math.random() - 0.5, 0.6 + Math.random() * 0.9, Math.random() - 0.5).normalize();
    this.spawn(piece, material, {
      velocity: dir.multiplyScalar(speed),
      spin: new THREE.Vector3(Math.random() * 12, Math.random() * 12, 0),
      gravity: 14,
      life: 1.4 + Math.random() * 0.6,
      baseScale: 0.6 + Math.random() * 1.2,
    });
  }

  /** Psionic link: a flickering violet beam from the caster to its victim. */
  async psiBeam(from: THREE.Vector3, to: THREE.Vector3, color = 0xb05aff): Promise<void> {
    const material = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.85, blending: THREE.AdditiveBlending, depthWrite: false });
    const length = from.distanceTo(to);
    const beam = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, length, 6, 1, true).rotateX(Math.PI / 2), material);
    beam.position.copy(from).lerp(to, 0.5);
    beam.lookAt(to);
    this.group.add(beam);
    const glow = this.glowSprite(color, 1.2);
    glow.position.copy(to);
    this.spawn(glow, glow.material, { life: 0.9, grow: 1.2 });
    await tween(0.9, (t) => {
      material.opacity = (0.5 + Math.random() * 0.5) * (1 - t * 0.6);
      beam.scale.set(1 + Math.sin(t * 40) * 0.5, 1 + Math.sin(t * 40) * 0.5, 1);
    });
    this.group.remove(beam);
    beam.geometry.dispose();
    material.dispose();
    this.sparks(to, color, 12);
  }

  // ----------------------------------------------------------------- text

  /** Rising text above a world point ("−4", "FALLO", "VIGILANCIA"...). */
  floatText(at: THREE.Vector3, text: string, kind: 'damage' | 'crit' | 'miss' | 'info' | 'warn' | 'alert' | 'heal' = 'info'): void {
    const el = document.createElement('div');
    el.className = `float-text ${kind}`;
    el.textContent = text;
    const label = new CSS2DObject(el);
    label.position.copy(at).setY(at.y + 1.4);
    this.group.add(label);
    void wait(1.6).then(() => {
      this.group.remove(label);
      el.remove();
    });
  }
}
