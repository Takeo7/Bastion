import * as THREE from 'three';

const PITCH = THREE.MathUtils.degToRad(52);
const MIN_DISTANCE = 9;
const MAX_DISTANCE = 42;
/**
 * Seconds into the shot over the shoulder and back out, and from one such shot to another (Arturo, 23/10 #5: «más
 * suaves»): each eased at both ends, never a cut.
 */
const CLOSE_IN = 0.8;
const CLOSE_OUT = 0.9;
const CLOSE_SWAP = 0.7;
/**
 * The camera sent somewhere (focus, a cinematic's zoom) gets there on a critically damped spring: it sets off gently,
 * slows down before it arrives and never overshoots (#5: no jumps nor lurches). The player's own panning and zoom keep
 * the quick follow they had.
 */
const SPRING = 5.5;
const smoother = (t: number) => t * t * t * (t * (t * 6 - 15) + 10);

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
  /** The shot over a shoulder, where it stands and what it looks at; kept while the camera goes back out of it. */
  private close: { pos: THREE.Vector3; look: THREE.Vector3 } | null = null;
  /** The shot it was in when another came (closeShot over closeShot), blended away over CLOSE_SWAP. */
  private closeFrom: { pos: THREE.Vector3; look: THREE.Vector3 } | null = null;
  private swap = 1;
  private closeOn = false;
  /** How far into it: 0 the rig's own view, 1 the shot (time-based; eased both ways where it is used). */
  private closeness = 0;
  /** Sent there (focus, cinematicZoom), not moved by the player: the spring takes it (SPRING). */
  private springing = false;
  private zoomSpring = false;
  private readonly targetVel = new THREE.Vector3();
  private distanceVel = 0;
  private bounds = new THREE.Box2(new THREE.Vector2(0, 0), new THREE.Vector2(30, 30));

  /** `enabled`: whether the keys move it now (the base's clearing, strategy/clearing.ts, only while it is shown). */
  constructor(
    private readonly camera: THREE.PerspectiveCamera,
    private readonly enabled: () => boolean = () => true,
  ) {
    window.addEventListener('keydown', this.onKeyDown);
    window.addEventListener('keyup', this.onKeyUp);
    window.addEventListener('blur', this.onBlur);
  }

  private readonly onKeyDown = (e: KeyboardEvent): void => {
    if (isTyping(e) || !this.enabled()) return;
    this.keys.add(e.code);
    if (e.code === 'KeyQ') this.rotate(-1);
    if (e.code === 'KeyE') this.rotate(1);
  };
  private readonly onKeyUp = (e: KeyboardEvent): void => void this.keys.delete(e.code);
  private readonly onBlur = (): void => this.keys.clear();

  /** Lets go of the keys (a screen that goes away). */
  dispose(): void {
    window.removeEventListener('keydown', this.onKeyDown);
    window.removeEventListener('keyup', this.onKeyUp);
    window.removeEventListener('blur', this.onBlur);
  }

  /** Which way it looks round, in quarter turns from where it starts (0–3): a piece's turn on screen follows it. */
  get quarter(): number {
    return (((Math.round((this.desiredYaw - Math.PI / 4) / (Math.PI / 2)) % 4) + 4) % 4);
  }

  /** Where it may look: the map, or within it `play` (what is played on a map with an outline: outline.ts playBounds). */
  setBounds(width: number, height: number, play?: { x0: number; y0: number; x1: number; y1: number } | null): void {
    this.bounds = play ? new THREE.Box2(new THREE.Vector2(play.x0, play.y0), new THREE.Vector2(play.x1, play.y1)) : new THREE.Box2(new THREE.Vector2(0, 0), new THREE.Vector2(width, height));
  }

  /** Where the camera looks (the sound hears from here). */
  get focusPoint(): THREE.Vector3 {
    return this.target;
  }

  /** The player explicitly took the camera: cancels any cinematic in progress. */
  takeControl(): void {
    this.epoch++;
    this.takenAt = performance.now();
    this.savedDistance = null;
    this.closeOn = false;
    this.springing = false;
    this.targetVel.set(0, 0, 0);
  }

  /**
   * A third-person shot (Arturo, 20/10 #11, the attack that kills): from `pos`, over the attacker's shoulder,
   * looking at `look` (shotCam.ts overShoulder works both out). The rig's own view stays where it was, and
   * `endCloseShot` (or the player taking the camera) goes back to it.
   */
  closeShot(pos: THREE.Vector3, look: THREE.Vector3): void {
    // Already in one (another target, the body fallen): from where it is now to the new one, not a cut.
    if (this.close && this.closeness > 0.05) this.closeFrom = this.shownClose();
    else this.closeFrom = null;
    this.swap = this.closeFrom ? 0 : 1;
    this.close = { pos: pos.clone(), look: look.clone() };
    this.closeOn = true;
  }

  /** The shot over the shoulder as shown now: between the last one and the new while they swap. */
  private shownClose(): { pos: THREE.Vector3; look: THREE.Vector3 } {
    const to = this.close!;
    if (!this.closeFrom || this.swap >= 1) return { pos: to.pos.clone(), look: to.look.clone() };
    const k = smoother(this.swap);
    return { pos: this.closeFrom.pos.clone().lerp(to.pos, k), look: this.closeFrom.look.clone().lerp(to.look, k) };
  }

  endCloseShot(): void {
    this.closeOn = false;
  }

  /** Whether it is in the shot over a shoulder, or on its way into it (the renderer fades its occlusion for it). */
  get closeUp(): boolean {
    return this.closeOn;
  }

  /** When the player last took the camera (performance.now): the enemy turn leaves it alone a while after (docs/UX-RITMO.md). */
  takenAt = -Infinity;

  /** It has reached where it was last sent, look and zoom, give or take a few centimetres. */
  get settled(): boolean {
    return this.target.distanceTo(this.desiredTarget) < 0.15 && Math.abs(this.distance - this.desiredDistance) < 0.3;
  }

  /** Looks at `p`; the target follows the floor height so rooftops stay centred. */
  focus(p: THREE.Vector3, instant = false): void {
    this.desiredTarget.set(p.x, Math.max(0, p.y), p.z);
    this.clampTarget();
    this.springing = !instant;
    if (instant) {
      this.target.copy(this.desiredTarget);
      this.targetVel.set(0, 0, 0);
    }
  }

  rotate(dir: number): void {
    this.takeControl();
    this.desiredYaw += (dir * Math.PI) / 2;
  }

  /** How far the player has drawn the camera back: 0 as close as it goes, 1 as far (where the cinematics leave it after). */
  get zoomFraction(): number {
    return ((this.savedDistance ?? this.desiredDistance) - MIN_DISTANCE) / (MAX_DISTANCE - MIN_DISTANCE);
  }

  zoom(deltaY: number): void {
    this.savedDistance = null;
    this.zoomSpring = false;
    this.distanceVel = 0;
    this.desiredDistance = THREE.MathUtils.clamp(this.desiredDistance * (1 + deltaY * 0.001), MIN_DISTANCE, MAX_DISTANCE);
  }

  /** Temporarily moves closer for an action shot; `restoreZoom` goes back. */
  cinematicZoom(distance: number): void {
    this.zoomSpring = true;
    if (this.savedDistance === null) this.savedDistance = this.desiredDistance;
    this.desiredDistance = THREE.MathUtils.clamp(Math.min(distance, this.savedDistance), MIN_DISTANCE, MAX_DISTANCE);
  }

  restoreZoom(): void {
    if (this.savedDistance === null) return;
    this.zoomSpring = true;
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
    this.springing = false;
    this.targetVel.set(0, 0, 0);
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
    // Sent somewhere: on the spring, in small steps so it stays steady at a low frame rate. The player's: as before.
    if (this.springing) {
      const steps = Math.max(1, Math.ceil(dt / (1 / 120)));
      const h = dt / steps;
      const d = new THREE.Vector3();
      for (let i = 0; i < steps; i++) {
        d.subVectors(this.desiredTarget, this.target).multiplyScalar(SPRING * SPRING).addScaledVector(this.targetVel, -2 * SPRING);
        this.targetVel.addScaledVector(d, h);
        this.target.addScaledVector(this.targetVel, h);
      }
      if (this.target.distanceTo(this.desiredTarget) < 0.005 && this.targetVel.lengthSq() < 1e-4) {
        this.target.copy(this.desiredTarget);
        this.targetVel.set(0, 0, 0);
      }
    } else this.target.lerp(this.desiredTarget, k);
    this.yaw += (this.desiredYaw - this.yaw) * k;
    if (this.zoomSpring) {
      const steps = Math.max(1, Math.ceil(dt / (1 / 120)));
      const h = dt / steps;
      for (let i = 0; i < steps; i++) {
        this.distanceVel += (SPRING * SPRING * (this.desiredDistance - this.distance) - 2 * SPRING * this.distanceVel) * h;
        this.distance += this.distanceVel * h;
      }
    } else this.distance += (this.desiredDistance - this.distance) * k;

    const horizontal = Math.cos(PITCH) * this.distance;
    this.camera.position.set(
      this.target.x + Math.sin(this.yaw) * horizontal,
      Math.sin(PITCH) * this.distance,
      this.target.z + Math.cos(this.yaw) * horizontal,
    );
    // Into the shot over a shoulder, or back out of it: between the two views, over a set time, eased at both ends;
    // and from one such shot to the next the same way.
    this.closeness = this.closeOn ? Math.min(1, this.closeness + dt / CLOSE_IN) : Math.max(0, this.closeness - dt / CLOSE_OUT);
    if (this.swap < 1) this.swap = Math.min(1, this.swap + dt / CLOSE_SWAP);
    if (this.close && (this.closeOn || this.closeness > 0)) {
      const w = smoother(this.closeness);
      const shot = this.shownClose();
      this.camera.position.lerp(shot.pos, w);
      this.camera.lookAt(this.target.clone().lerp(shot.look, w));
    } else {
      this.closeness = 0;
      this.close = null;
      this.closeFrom = null;
      this.camera.lookAt(this.target);
    }

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
