import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { PLAYER_COLORS, TEMPLATES, type GameState, type Unit, type Vec2 } from '@bastion/engine';
import { tileToWorld } from './coords';
import { buildUnit, gearOf, isFigure, type UnitRig } from './models';
import { easeInOut, tween } from './tween';

const ALIEN_COLOR = '#ff4d4d';
const POOL_COLOR = '#e8edf2';
/** Seconds per orthogonal tile while moving. */
const STEP_SECONDS = 0.16;

/**
 * Pose values combined every frame into the rig transform.
 * `*Target` values are persistent states (hunkered, overwatch) that the pose
 * eases towards; the rest are driven by one-shot animations.
 */
interface Pose {
  crouch: number;
  crouchTarget: number;
  aim: number;
  aimTarget: number;
  aimFx: number;
  run: number;
  recoil: number;
  hit: number;
  flash: number;
  fall: number;
  lean: number;
  hop: number;
  spin: number;
  yaw: number;
  yawTarget: number;
  phase: number;
}

interface UnitView {
  id: string;
  rig: UnitRig;
  ring: THREE.Mesh;
  flag: CSS2DObject;
  flagEl: HTMLDivElement;
  pose: Pose;
  /** True while an animation owns the position; sync() leaves it alone. */
  animating: boolean;
  dead: boolean;
  /** The support drone is away on a mission. */
  droneAway: boolean;
}

const ringGeometry = new THREE.RingGeometry(0.36, 0.44, 32).rotateX(-Math.PI / 2);

export function unitColor(u: Unit): string {
  if (u.team === 'alien') return ALIEN_COLOR;
  return u.owner === null ? POOL_COLOR : PLAYER_COLORS[u.owner];
}

function angleTo(from: THREE.Vector3, to: THREE.Vector3): number | null {
  const dx = to.x - from.x;
  const dz = to.z - from.z;
  return dx || dz ? Math.atan2(dx, dz) : null;
}

function wrapAngle(a: number): number {
  return Math.atan2(Math.sin(a), Math.cos(a));
}

/** 3D bodies, base rings, floating flags and procedural animation for every unit. */
export class UnitViews {
  readonly group = new THREE.Group();
  private views = new Map<string, UnitView>();
  private readonly selection: THREE.Mesh;
  private readonly partnerSelection: THREE.Mesh;
  private selectedId: string | null = null;
  private hoveredId: string | null = null;
  private partnerId: string | null = null;

  constructor() {
    this.selection = new THREE.Mesh(
      new THREE.RingGeometry(0.46, 0.56, 40).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.9, depthWrite: false }),
    );
    this.partnerSelection = new THREE.Mesh(
      new THREE.RingGeometry(0.5, 0.58, 40).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.75, depthWrite: false }),
    );
    this.selection.visible = false;
    this.partnerSelection.visible = false;
    this.group.add(this.selection, this.partnerSelection);
  }

  build(s: GameState): void {
    for (const v of this.views.values()) {
      this.group.remove(v.rig.root);
      v.flag.element.remove();
    }
    this.views.clear();
    for (const u of s.units) this.create(u);
  }

  private create(u: Unit): UnitView {
    const color = unitColor(u);
    const rig = buildUnit(u.template, color, u.appearance, gearOf(u.weaponTier, u.armorTier));
    const ring = new THREE.Mesh(
      ringGeometry,
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.85, depthWrite: false }),
    );
    ring.position.y = 0.03;

    const flagEl = document.createElement('div');
    flagEl.className = `uflag ${u.team === 'alien' ? 'alien' : 'xcom'}`;
    flagEl.style.setProperty('--accent', color);
    const flag = new CSS2DObject(flagEl);
    flag.position.set(0, 2.15, 0);
    flag.center.set(0.5, 1);

    rig.root.add(ring, flag);
    rig.root.position.copy(tileToWorld(u.pos));
    const facing = u.team === 'alien' ? 0 : Math.PI;
    const pose: Pose = {
      crouch: 0, crouchTarget: 0, aim: 0, aimTarget: 0, aimFx: 0, run: 0, recoil: 0, hit: 0, flash: 0,
      fall: u.alive ? 0 : 1, lean: 0, hop: 0, spin: 0, yaw: facing, yawTarget: facing,
      phase: Math.random() * Math.PI * 2,
    };
    this.group.add(rig.root);

    const view: UnitView = { id: u.id, rig, ring, flag, flagEl, pose, animating: false, dead: !u.alive && !u.evacuated, droneAway: false };
    this.views.set(u.id, view);
    flagEl.classList.toggle('named', u.id === this.selectedId);
    if (!u.alive) {
      ring.visible = false;
      flag.visible = false;
    }
    if (u.evacuated) rig.root.visible = false;
    this.apply(view, 0);
    return view;
  }

  /** Brings flags, visibility, persistent poses and idle positions in line with the shown state. */
  sync(s: GameState, visibleAliens: Set<string>, coverOf: (u: Unit) => 0 | 1 | 2 | null): void {
    const carrier = s.mission.item?.carrier ?? null;
    for (const u of s.units) {
      const v = this.views.get(u.id) ?? this.create(u);
      if (u.evacuated) {
        if (!v.animating) v.rig.root.visible = false;
        continue;
      }
      if (!v.animating && u.alive) v.rig.root.position.copy(tileToWorld(u.pos));
      // A corpse raised by a sectoid stands up as a new unit: hide the body.
      if (u.reanimated && v.dead) {
        v.rig.root.visible = false;
        continue;
      }
      // Mind control turns rings and flags hostile.
      const color = unitColor(u);
      (v.ring.material as THREE.MeshBasicMaterial).color.set(color);
      v.flagEl.style.setProperty('--accent', color);
      v.flagEl.classList.toggle('alien', u.team === 'alien');
      v.flagEl.classList.toggle('xcom', u.team === 'xcom');
      const shown = u.team === 'xcom' || u.objective || visibleAliens.has(u.id) || (v.dead && v.rig.root.visible);
      v.rig.root.visible = shown;
      v.flag.visible = shown && u.alive;
      v.pose.crouchTarget = u.hunkered || u.captive ? 1 : 0;
      v.pose.aimTarget = u.overwatch ? 1 : 0;
      if (u.alive) this.renderFlag(v, u, coverOf(u), carrier === u.id);
      if (!u.alive && !v.dead && !v.animating) {
        v.dead = true;
        v.pose.fall = 1;
        v.ring.visible = false;
      }
    }
    this.placeRings();
  }

  private renderFlag(v: UnitView, u: Unit, cover: 0 | 1 | 2 | null, carrier: boolean): void {
    const pips = Array.from({ length: u.maxHp }, (_, i) => `<i class="${i < u.hp ? 'on' : ''}"></i>`).join('');
    const armor = u.armor ? `<span class="armor">${'<i></i>'.repeat(u.armor)}</span>` : '';
    const coverIcon =
      cover === null ? '' : `<b class="cover c${cover}" title="${cover === 2 ? 'Cobertura alta' : cover === 1 ? 'Cobertura baja' : 'Flanqueado'}"></b>`;
    const status = [
      u.controlledBy ? '<em class="psi">CONTROLADO</em>' : '',
      u.overwatch ? `<em class="ow">${u.killZone ? 'ZONA LETAL' : 'VIGILANCIA'}</em>` : '',
      u.hunkered ? '<em class="hk">AGAZAPADO</em>' : '',
      u.aided ? '<em class="aid">PROTEGIDO</em>' : '',
      u.stunned ? '<em class="stun">ATURDIDO</em>' : '',
      u.disoriented ? '<em class="psi">DESORIENTADO</em>' : '',
      u.panicked ? '<em class="psi">PÁNICO</em>' : '',
      u.suppressedBy ? '<em class="stun">SUPRIMIDO</em>' : '',
      u.marked ? '<em class="aid">MARCADO</em>' : '',
      u.ruptured ? '<em class="stun">VULNERABLE</em>' : '',
      carrier ? '<em class="data">DATOS</em>' : '',
      u.captive ? '<em class="data">CAUTIVO</em>' : '',
      u.objective ? '<em class="stun">OBJETIVO</em>' : '',
    ].join('');
    // Like XCOM 2, a soldier with a nickname goes by it in the field.
    const called = u.nickname ? `«${u.nickname}»` : u.name;
    const name = u.team === 'xcom' && u.template !== 'vip' ? `${TEMPLATES[u.template].short} · ${called}` : u.name;
    const html = `<div class="uflag-name">${name}</div><div class="uflag-row">${coverIcon}<span class="hp">${pips}</span>${armor}${status}</div>`;
    if (v.flagEl.innerHTML !== html) v.flagEl.innerHTML = html;
  }

  has(id: string): boolean {
    return this.views.has(id);
  }

  worldPos(id: string): THREE.Vector3 {
    return this.views.get(id)?.rig.root.position.clone() ?? new THREE.Vector3();
  }

  /** Chest height, where shots aim and damage numbers appear. */
  chestPos(id: string): THREE.Vector3 {
    const v = this.views.get(id);
    return this.worldPos(id).add(new THREE.Vector3(0, v?.rig.chest ?? 1.0, 0));
  }

  dronePos(id: string): THREE.Vector3 {
    const v = this.views.get(id);
    if (!v?.rig.drone) return this.chestPos(id).add(new THREE.Vector3(0, 0.8, 0));
    v.rig.root.updateMatrixWorld(true);
    return v.rig.drone.getWorldPosition(new THREE.Vector3());
  }

  setDroneAway(id: string, away: boolean): void {
    const v = this.views.get(id);
    if (!v?.rig.drone) return;
    v.droneAway = away;
    v.rig.drone.visible = !away;
  }

  muzzlePos(id: string): THREE.Vector3 {
    const v = this.views.get(id);
    if (!v) return new THREE.Vector3();
    v.rig.root.updateMatrixWorld(true);
    return v.rig.muzzle.getWorldPosition(new THREE.Vector3());
  }

  forceVisible(id: string): void {
    const v = this.views.get(id);
    if (v) v.rig.root.visible = true;
  }

  // ---------------------------------------------------------- selection

  setSelected(id: string | null): void {
    this.selectedId = id;
    this.placeRings();
    this.updateNames();
  }

  setHovered(id: string | null): void {
    if (this.hoveredId === id) return;
    this.hoveredId = id;
    this.updateNames();
  }

  setPartnerSelected(id: string | null, color: string): void {
    this.partnerId = id;
    (this.partnerSelection.material as THREE.MeshBasicMaterial).color.set(color);
    this.placeRings();
  }

  /** Names are only shown for the selected and hovered units to keep flags compact. */
  private updateNames(): void {
    for (const v of this.views.values()) {
      v.flagEl.classList.toggle('named', v.id === this.selectedId || v.id === this.hoveredId);
    }
  }

  private placeRings(): void {
    const sel = this.selectedId ? this.views.get(this.selectedId) : undefined;
    this.selection.visible = !!sel && !sel.dead;
    if (sel) this.selection.position.copy(sel.rig.root.position).setY(sel.rig.root.position.y + 0.035);
    const partner = this.partnerId ? this.views.get(this.partnerId) : undefined;
    this.partnerSelection.visible = !!partner && !partner.dead && this.partnerId !== this.selectedId;
    if (partner) this.partnerSelection.position.copy(partner.rig.root.position).setY(partner.rig.root.position.y + 0.035);
  }

  // ------------------------------------------------------------- frame

  tick(dt: number, time: number): void {
    const pulse = 1 + Math.sin(time * 5) * 0.06;
    this.selection.scale.setScalar(pulse);
    this.partnerSelection.scale.setScalar(2 - pulse);
    this.placeRings();
    for (const v of this.views.values()) {
      const p = v.pose;
      const k = 1 - Math.exp(-dt * 9);
      p.crouch += (p.crouchTarget - p.crouch) * k;
      p.aim += (p.aimTarget - p.aim) * k;
      p.yaw += wrapAngle(p.yawTarget - p.yaw) * (1 - Math.exp(-dt * 14));
      this.apply(v, time);
      if (v.rig.drone && !v.droneAway) {
        const d = v.rig.drone;
        d.userData.baseY ??= d.position.y;
        d.position.y = d.userData.baseY + Math.sin(time * 2.4 + p.phase) * 0.07;
        v.rig.drone.rotation.y += dt * 1.6;
      }
    }
  }

  private apply(v: UnitView, time: number): void {
    const { rig, pose: p } = v;
    if (isFigure(rig)) {
      // Articulated figures pose their own skeleton; the reload twirl is layered on top.
      rig.applyPose({ ...p, aim: Math.max(p.aim, p.aimFx) }, time);
      rig.gunPivot.rotation.z = p.spin * Math.PI * 2;
      return;
    }
    const alive = !v.dead;
    const breathe = alive ? Math.sin(time * 2.1 + p.phase) * 0.012 : 0;
    const bob = p.run * Math.abs(Math.sin(time * 15 + p.phase)) * 0.07;
    rig.yaw.rotation.y = p.yaw;
    rig.pivot.rotation.x = p.run * 0.22 + p.lean - p.hit * 0.38 - p.recoil * 0.05 - p.fall * (Math.PI / 2);
    rig.pivot.position.y = rig.hip - p.crouch * 0.2 + bob + p.hop - p.fall * (rig.hip - 0.27);
    rig.pivot.scale.y = (1 + breathe) * (1 - p.crouch * 0.12);
    const aim = Math.max(p.aim, p.aimFx);
    rig.gunPivot.rotation.x = (1 - aim) * 0.6 - p.recoil * 0.18;
    rig.gunPivot.rotation.z = p.spin * Math.PI * 2;
    rig.gunPivot.position.z = 0.06 - p.recoil * 0.09;
    rig.flash.visible = p.flash > 0.01;
    (rig.flash.material as THREE.MeshBasicMaterial).opacity = p.flash * 0.6;
  }

  // -------------------------------------------------------- animations

  face(id: string, toward: THREE.Vector3): void {
    const v = this.views.get(id);
    if (!v) return;
    const a = angleTo(v.rig.root.position, toward);
    if (a !== null) v.pose.yawTarget = a;
  }

  async moveAlong(id: string, path: Vec2[], onStep?: (p: THREE.Vector3) => void): Promise<void> {
    const v = this.views.get(id);
    if (!v || !path.length) return;
    v.animating = true;
    const p = v.pose;
    try {
      void tween(0.12, (t) => (p.run = Math.max(p.run, t)));
      for (const step of path) {
        const from = v.rig.root.position.clone();
        const to = tileToWorld(step);
        this.face(id, to);
        const rise = to.y - from.y;
        if (rise > 0.5) {
          // Up a ladder: to the wall, climb, then onto the roof.
          const foot = from.clone().lerp(to, 0.4).setY(from.y);
          const top = foot.clone().setY(to.y);
          await tween(STEP_SECONDS * 0.6, (t) => v.rig.root.position.lerpVectors(from, foot, t));
          p.run = 0.3;
          await tween(STEP_SECONDS * 4, (t) => v.rig.root.position.lerpVectors(foot, top, t));
          p.run = 1;
          await tween(STEP_SECONDS * 0.6, (t) => v.rig.root.position.lerpVectors(top, to, t));
        } else if (rise < -0.5) {
          // Drop off an edge: a short hop out, then the fall.
          await tween(STEP_SECONDS * 2.2, (t) => {
            v.rig.root.position.lerpVectors(from, to, Math.min(1, t * 1.3));
            v.rig.root.position.y = from.y + rise * t * t + Math.sin(Math.min(1, t * 2) * Math.PI) * 0.35;
          });
          void tween(0.2, (t) => (p.crouch = Math.sin(t * Math.PI) * 0.8));
        } else {
          const seconds = STEP_SECONDS * (from.distanceTo(to) > 1.2 ? 1.4 : 1);
          await tween(seconds, (t) => {
            v.rig.root.position.lerpVectors(from, to, t);
          });
        }
        onStep?.(v.rig.root.position);
      }
    } finally {
      v.animating = false;
      void tween(0.18, (t) => (p.run = 1 - t));
    }
  }

  /**
   * Slides to a nearby point (step-out to shoot around a corner). With
   * `release = false` the unit stays where it is until a later slide releases it.
   */
  async slide(id: string, to: THREE.Vector3, seconds = 0.2, release = true): Promise<void> {
    const v = this.views.get(id);
    if (!v) return;
    v.animating = true;
    const from = v.rig.root.position.clone();
    await tween(seconds, (t) => v.rig.root.position.lerpVectors(from, to, easeInOut(t)));
    if (release) v.animating = false;
  }

  async raiseWeapon(id: string, target: THREE.Vector3): Promise<void> {
    const v = this.views.get(id);
    if (!v) return;
    this.face(id, target);
    await tween(0.28, (t) => (v.pose.aimFx = easeInOut(t)));
  }

  lowerWeapon(id: string): void {
    const v = this.views.get(id);
    if (v) void tween(0.35, (t) => (v.pose.aimFx = 1 - easeInOut(t)));
  }

  recoil(id: string): void {
    const v = this.views.get(id);
    if (v) void tween(0.08, (t) => (v.pose.recoil = Math.sin(t * Math.PI)));
  }

  /** Knock-back and red flash; `strength` 1 for a normal hit, more for crits. */
  hitReact(id: string, strength = 1): Promise<void> {
    const v = this.views.get(id);
    if (!v) return Promise.resolve();
    return tween(0.4, (t) => {
      const k = Math.sin(Math.min(1, t * 1.6) * Math.PI);
      v.pose.hit = k * 0.6 * strength;
      v.pose.flash = 1 - t;
    }).then(() => {
      v.pose.hit = 0;
      v.pose.flash = 0;
    });
  }

  async die(id: string): Promise<void> {
    const v = this.views.get(id);
    if (!v || v.dead) return;
    v.animating = true;
    v.flag.visible = false;
    v.pose.aimFx = 0;
    await tween(0.75, (t) => {
      v.pose.fall = t * t;
      v.pose.flash = Math.max(0, 0.6 - t);
    });
    v.pose.fall = 1;
    v.pose.flash = 0;
    v.dead = true;
    v.ring.visible = false;
    v.rig.root.traverse((o) => {
      if (o instanceof THREE.Mesh) o.castShadow = false;
    });
    v.animating = false;
  }

  async reload(id: string): Promise<void> {
    const v = this.views.get(id);
    if (!v) return;
    await tween(0.6, (t) => {
      v.pose.spin = easeInOut(t);
      v.pose.aimFx = Math.sin(t * Math.PI) * 0.4;
    });
    v.pose.spin = 0;
    v.pose.aimFx = 0;
  }

  /** Wind-up and release of a throw; resolves at the release point. */
  async throwMotion(id: string): Promise<void> {
    const v = this.views.get(id);
    if (!v) return;
    await tween(0.22, (t) => (v.pose.lean = -0.35 * easeInOut(t)));
    await tween(0.14, (t) => (v.pose.lean = -0.35 + 0.65 * t));
    void tween(0.25, (t) => (v.pose.lean = 0.3 * (1 - t)));
  }

  /** Melee strike: a quick lunge towards the target and back. */
  async lunge(id: string, target: THREE.Vector3): Promise<void> {
    const v = this.views.get(id);
    if (!v) return;
    this.face(id, target);
    const home = v.rig.root.position.clone();
    const toward = target.clone().setY(home.y).sub(home).setLength(0.45);
    v.animating = true;
    await tween(0.16, (t) => {
      v.pose.lean = -0.25 * t;
      v.pose.aimFx = t;
    });
    await tween(0.1, (t) => {
      v.pose.lean = -0.25 + 0.75 * t;
      v.rig.root.position.copy(home).addScaledVector(toward, t);
    });
    void tween(0.3, (t) => {
      v.pose.lean = 0.5 * (1 - t);
      v.pose.aimFx = 1 - t;
      v.rig.root.position.copy(home).addScaledVector(toward, 1 - t);
    }).then(() => (v.animating = false));
  }

  /** Lifted out by the dropship: rises into the sky and disappears. */
  async evacuate(id: string): Promise<void> {
    const v = this.views.get(id);
    if (!v) return;
    v.animating = true;
    v.flag.visible = false;
    v.ring.visible = false;
    const start = v.rig.root.position.clone();
    await tween(1.1, (t) => {
      v.rig.root.position.copy(start).setY(start.y + t * t * 7);
      v.rig.yaw.rotation.z = Math.sin(t * 6) * 0.05;
    });
    v.rig.root.visible = false;
    v.animating = false;
  }

  /** Startled hop when an alien pod notices the squad. */
  alert(id: string): Promise<void> {
    const v = this.views.get(id);
    if (!v) return Promise.resolve();
    return tween(0.35, (t) => (v.pose.hop = Math.sin(t * Math.PI) * 0.22));
  }
}
