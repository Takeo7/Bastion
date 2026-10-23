import * as THREE from 'three';
import { PANELS } from './stage';

// The Asentamiento walked about in first person (playtest 22/10 #43: a test, to
// see whether it is liked): WASD or the arrows to walk, the mouse to look (the
// pointer held by the view), Shift to run, Esc to come out. The eyes at a
// person's height; what can't be stood on stops it (ClearingArt.blocked: the
// pines, the buildings' walls, the defences, the clearing's edge), sliding along.

/** Eyes this high (tiles: 1.6 m), a body this wide round them (0.45 m: no nose against a wall), and how fast it walks and runs (tiles a second: 1.4 and 3.5 m/s). */
const EYES = 1.07;
const RADIUS = 0.3;
const WALK = 0.93;
const RUN = 2.3;
/** The view's field (degrees) while walking, and how far the mouse turns it (radians a pixel). */
const FOV = 70;
const TURN = 0.0022;
/** The head's bob as it walks: how high (tiles) and how many a tile walked. */
const BOB = 0.02;
const BOB_RATE = 2.2;

export class WalkAbout {
  private yaw = 0;
  private pitch = 0;
  private bob = 0;
  private readonly at = new THREE.Vector2();
  private readonly keys = new Set<string>();
  private readonly was: { pos: THREE.Vector3; quat: THREE.Quaternion; fov: number };
  private readonly hint = document.createElement('div');
  /** The screen's panels over the view (stage.ts PANELS), out of sight while it walks, and how each was. */
  private readonly hidden: { el: HTMLElement; was: string }[] = [];

  /**
   * On at once, from `from` (the ground the camera looked at) facing `yaw` (radians, as the view looked); `blocked`, the
   * tiles it can't stand on, and `solids` what else stops it (x, z boxes: La Lata); `done`, called when it comes out
   * by itself (Esc, the pointer let go).
   */
  constructor(
    private readonly camera: THREE.PerspectiveCamera,
    private readonly element: HTMLElement,
    private readonly blocked: (x: number, y: number) => boolean,
    private readonly solids: readonly THREE.Box2[],
    private readonly done: () => void,
    from: THREE.Vector3,
    yaw: number,
  ) {
    this.was = { pos: camera.position.clone(), quat: camera.quaternion.clone(), fov: camera.fov };
    this.at.copy(this.free(from.x, from.z));
    this.yaw = yaw;
    this.camera.fov = FOV;
    this.camera.updateProjectionMatrix();
    this.hint.textContent = 'WASD: andar · Ratón: mirar · Mayús: correr · Esc: salir';
    Object.assign(this.hint.style, {
      position: 'absolute', left: '50%', bottom: '18px', transform: 'translateX(-50%)', padding: '6px 12px',
      background: 'rgba(10, 12, 14, 0.7)', color: '#e8e4dc', font: '14px sans-serif', borderRadius: '4px', pointerEvents: 'none', zIndex: '5',
    });
    this.element.append(this.hint);
    for (const el of document.querySelectorAll<HTMLElement>(PANELS)) {
      this.hidden.push({ el, was: el.style.visibility });
      el.style.visibility = 'hidden';
    }
    // (Refused where there is no window to hold it in: the keys still walk.)
    void Promise.resolve(this.element.querySelector('canvas')?.requestPointerLock()).catch(() => undefined);
    document.addEventListener('mousemove', this.onMouse);
    document.addEventListener('pointerlockchange', this.onLock);
    window.addEventListener('keydown', this.onKeyDown, true);
    window.addEventListener('keyup', this.onKeyUp, true);
    window.addEventListener('blur', this.onBlur);
  }

  /** Out of it: the camera back as it was, the pointer let go. */
  dispose(): void {
    document.removeEventListener('mousemove', this.onMouse);
    document.removeEventListener('pointerlockchange', this.onLock);
    window.removeEventListener('keydown', this.onKeyDown, true);
    window.removeEventListener('keyup', this.onKeyUp, true);
    window.removeEventListener('blur', this.onBlur);
    if (document.pointerLockElement) document.exitPointerLock();
    this.hint.remove();
    for (const { el, was } of this.hidden) el.style.visibility = was;
    this.camera.position.copy(this.was.pos);
    this.camera.quaternion.copy(this.was.quat);
    this.camera.fov = this.was.fov;
    this.camera.updateProjectionMatrix();
  }

  tick(dt: number): void {
    const k = this.keys;
    const ahead = (k.has('KeyW') || k.has('ArrowUp') ? 1 : 0) - (k.has('KeyS') || k.has('ArrowDown') ? 1 : 0);
    const side = (k.has('KeyD') || k.has('ArrowRight') ? 1 : 0) - (k.has('KeyA') || k.has('ArrowLeft') ? 1 : 0);
    if (ahead || side) {
      const speed = (k.has('ShiftLeft') || k.has('ShiftRight') ? RUN : WALK) * Math.min(dt, 0.1);
      // Ahead is where the view looks, along the ground (the camera's -z turned by yaw).
      const fx = -Math.sin(this.yaw);
      const fz = -Math.cos(this.yaw);
      const n = Math.hypot(ahead, side);
      const dx = ((fx * ahead - fz * side) / n) * speed;
      const dz = ((fz * ahead + fx * side) / n) * speed;
      // Each way apart, so it slides along what stops it.
      if (!this.hits(this.at.x + dx, this.at.y)) this.at.x += dx;
      if (!this.hits(this.at.x, this.at.y + dz)) this.at.y += dz;
      this.bob += Math.hypot(dx, dz) * BOB_RATE * Math.PI * 2;
    }
    this.camera.position.set(this.at.x, EYES + Math.sin(this.bob) * BOB, this.at.y);
    this.camera.quaternion.setFromEuler(new THREE.Euler(this.pitch, this.yaw, 0, 'YXZ'));
  }

  /** Whether a body standing at x, z (tiles) would be in something it can't stand on. */
  private hits(x: number, z: number): boolean {
    for (let ty = Math.floor(z - RADIUS); ty <= Math.floor(z + RADIUS); ty++) {
      for (let tx = Math.floor(x - RADIUS); tx <= Math.floor(x + RADIUS); tx++) {
        if (!this.blocked(tx, ty)) continue;
        // The tile's nearest point to the body's middle, inside the body?
        const nx = THREE.MathUtils.clamp(x, tx, tx + 1);
        const nz = THREE.MathUtils.clamp(z, ty, ty + 1);
        if ((nx - x) ** 2 + (nz - z) ** 2 < RADIUS * RADIUS) return true;
      }
    }
    for (const b of this.solids) {
      const nx = THREE.MathUtils.clamp(x, b.min.x, b.max.x);
      const nz = THREE.MathUtils.clamp(z, b.min.y, b.max.y);
      if ((nx - x) ** 2 + (nz - z) ** 2 < RADIUS * RADIUS) return true;
    }
    return false;
  }

  /** The middle of the nearest tile it can stand on to x, z (tiles), out to a few tiles; where it was asked if none. */
  private free(x: number, z: number): THREE.Vector2 {
    const cx = Math.floor(x);
    const cz = Math.floor(z);
    for (let r = 0; r < 8; r++) {
      for (let dz = -r; dz <= r; dz++) {
        for (let dx = -r; dx <= r; dx++) {
          if (Math.max(Math.abs(dx), Math.abs(dz)) !== r || this.hits(cx + dx + 0.5, cz + dz + 0.5)) continue;
          return new THREE.Vector2(cx + dx + 0.5, cz + dz + 0.5);
        }
      }
    }
    return new THREE.Vector2(x, z);
  }

  private readonly onMouse = (e: MouseEvent): void => {
    if (!document.pointerLockElement) return;
    this.yaw -= e.movementX * TURN;
    this.pitch = THREE.MathUtils.clamp(this.pitch - e.movementY * TURN, -1.3, 1.3);
  };

  /** The pointer let go (Esc does that by itself): out. */
  private readonly onLock = (): void => {
    if (!document.pointerLockElement) this.done();
  };

  private readonly onKeyDown = (e: KeyboardEvent): void => {
    if (e.code === 'Escape') {
      // Out of the walk only (not the base's menu as well).
      e.stopPropagation();
      e.preventDefault();
      this.done();
      return;
    }
    this.keys.add(e.code);
    // Its keys are its own while it walks (the base's camera and shortcuts would take them too).
    if (/^(Key[WASD]|Arrow|Shift)/.test(e.code)) e.stopPropagation();
  };

  private readonly onKeyUp = (e: KeyboardEvent): void => {
    this.keys.delete(e.code);
  };

  private readonly onBlur = (): void => {
    this.keys.clear();
  };
}
