import * as THREE from 'three';

const PITCH = THREE.MathUtils.degToRad(52);
const MIN_DISTANCE = 9;
const MAX_DISTANCE = 42;

/**
 * XCOM-style tactical camera: fixed pitch, yaw in 90° steps (Q/E),
 * WASD / right-drag panning, wheel zoom, smooth focusing and screen shake.
 *
 * `epoch` increases whenever the player takes the camera (pan, rotate or
 * picking a soldier). Cinematic moves remember the epoch they started with and
 * stop as soon as it changes, so the camera never fights the player.
 */
export class CameraRig {
  epoch = 0;
  private target = new THREE.Vector3();
  private desiredTarget = new THREE.Vector3();
  private yaw = Math.PI / 4;
  private desiredYaw = Math.PI / 4;
  private distance = 24;
  private desiredDistance = 24;
  /** Player's zoom saved while a cinematic zooms in; null when none is active. */
  private savedDistance: number | null = null;
  private shakeStrength = 0;
  private shakeTime = 0;
  private readonly keys = new Set<string>();
  private bounds = new THREE.Box2(new THREE.Vector2(0, 0), new THREE.Vector2(30, 30));

  constructor(private readonly camera: THREE.PerspectiveCamera) {
    window.addEventListener('keydown', (e) => {
      if (isTyping(e)) return;
      this.keys.add(e.code);
      if (e.code === 'KeyQ') this.rotate(-1);
      if (e.code === 'KeyE') this.rotate(1);
    });
    window.addEventListener('keyup', (e) => this.keys.delete(e.code));
    window.addEventListener('blur', () => this.keys.clear());
  }

  setBounds(width: number, height: number): void {
    this.bounds = new THREE.Box2(new THREE.Vector2(0, 0), new THREE.Vector2(width, height));
  }

  /** The player explicitly took the camera: cancels any cinematic in progress. */
  takeControl(): void {
    this.epoch++;
    this.savedDistance = null;
  }

  /** Looks at `p`; the target follows the floor height so rooftops stay centred. */
  focus(p: THREE.Vector3, instant = false): void {
    this.desiredTarget.set(p.x, Math.max(0, p.y), p.z);
    this.clampTarget();
    if (instant) this.target.copy(this.desiredTarget);
  }

  rotate(dir: number): void {
    this.takeControl();
    this.desiredYaw += (dir * Math.PI) / 2;
  }

  zoom(deltaY: number): void {
    this.savedDistance = null;
    this.desiredDistance = THREE.MathUtils.clamp(this.desiredDistance * (1 + deltaY * 0.001), MIN_DISTANCE, MAX_DISTANCE);
  }

  /** Temporarily moves closer for an action shot; `restoreZoom` goes back. */
  cinematicZoom(distance: number): void {
    if (this.savedDistance === null) this.savedDistance = this.desiredDistance;
    this.desiredDistance = THREE.MathUtils.clamp(Math.min(distance, this.savedDistance), MIN_DISTANCE, MAX_DISTANCE);
  }

  restoreZoom(): void {
    if (this.savedDistance === null) return;
    this.desiredDistance = this.savedDistance;
    this.savedDistance = null;
  }

  shake(strength: number, seconds: number): void {
    this.shakeStrength = Math.max(this.shakeStrength, strength);
    this.shakeTime = Math.max(this.shakeTime, seconds);
  }

  /** Pans by a screen-space drag in pixels. */
  panPixels(dx: number, dy: number): void {
    this.takeControl();
    const scale = this.distance * 0.0016;
    this.pan(-dx * scale, -dy * scale);
  }

  private pan(right: number, forward: number): void {
    const fwd = new THREE.Vector3(-Math.sin(this.yaw), 0, -Math.cos(this.yaw));
    const rgt = new THREE.Vector3(-fwd.z, 0, fwd.x);
    this.desiredTarget.addScaledVector(rgt, right).addScaledVector(fwd, forward);
    this.clampTarget();
  }

  private clampTarget(): void {
    this.desiredTarget.x = THREE.MathUtils.clamp(this.desiredTarget.x, this.bounds.min.x, this.bounds.max.x);
    this.desiredTarget.z = THREE.MathUtils.clamp(this.desiredTarget.z, this.bounds.min.y, this.bounds.max.y);
  }

  update(dt: number): void {
    const speed = 16 * dt * (this.distance / 24);
    let right = 0;
    let forward = 0;
    if (this.keys.has('KeyW') || this.keys.has('ArrowUp')) forward += speed;
    if (this.keys.has('KeyS') || this.keys.has('ArrowDown')) forward -= speed;
    if (this.keys.has('KeyD') || this.keys.has('ArrowRight')) right += speed;
    if (this.keys.has('KeyA') || this.keys.has('ArrowLeft')) right -= speed;
    if (right || forward) {
      this.takeControl();
      this.pan(right, forward);
    }

    const k = 1 - Math.exp(-dt * 7);
    this.target.lerp(this.desiredTarget, k);
    this.yaw += (this.desiredYaw - this.yaw) * k;
    this.distance += (this.desiredDistance - this.distance) * k;

    const horizontal = Math.cos(PITCH) * this.distance;
    this.camera.position.set(
      this.target.x + Math.sin(this.yaw) * horizontal,
      Math.sin(PITCH) * this.distance,
      this.target.z + Math.cos(this.yaw) * horizontal,
    );
    this.camera.lookAt(this.target);

    if (this.shakeTime > 0) {
      this.shakeTime = Math.max(0, this.shakeTime - dt);
      const s = this.shakeStrength * Math.min(1, this.shakeTime * 4);
      this.camera.position.add(new THREE.Vector3((Math.random() - 0.5) * s, (Math.random() - 0.5) * s, (Math.random() - 0.5) * s));
      if (this.shakeTime === 0) this.shakeStrength = 0;
    }
  }
}

export function isTyping(e: KeyboardEvent): boolean {
  const el = e.target as HTMLElement | null;
  return !!el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.isContentEditable);
}
