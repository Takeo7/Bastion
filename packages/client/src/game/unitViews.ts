import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { coverAt, decorOf, PLAYER_COLORS, sexOfPerson, TEMPLATES, type GameState, type Place, type TemplateId, type Unit, type Vec2 } from '@bastion/engine';
import { STOREY, tileToWorld } from './coords';
import type { Blow, FigurePose } from './figures';
import { clipSpeed, CROUCHED, gaitsOf } from './gaits';
import { buildUnit, gearOf, IDLE_POSE, isFigure, type UnitRig } from './models';
import { easeInOut, timeRate, tween, wait } from './tween';

const ALIEN_COLOR = '#ff4d4d';
const POOL_COLOR = '#e8edf2';
/** Enemies that cast shadows at most (the nearest to the squad). */
const CROWD_SHADOWS = 24;
/** The time scale of hops, vaults and falls (walking keeps its own pace: gaits.ts). */
const HOP_SECONDS = 0.16;
/** Seconds a storey of ladder takes to climb. */
const LADDER_SECONDS = 1.2;
/** How high a window's sill is over its floor (the kits' windows), where a unit climbs to jump out. */
const SILL = 0.75;
/** Seconds a blow (a shot, a strike, a blast) still says which way the unit it came at falls if it dies. */
const BLOW_SECONDS = 8;

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
  /** Rummaging through furniture (rummage()). */
  search: number;
  /** Up a ladder (moveAlong). */
  climb: number;
  /** A melee stroke: −1 wound up, +1 followed through (lunge). */
  swing: number;
  /** How far into its hit reaction (hitReact), 0 to 1. */
  react: number;
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
  /** Casts a shadow (off for the dead, and for the far end of a big crowd). */
  shadow: boolean;
  /** Flag contents from the unit's state, and the hover line above them. */
  body: string;
  tag: string;
  /** Where it stands, with its storey (docs/FASE6.md): set by sync and by each step of a walk. */
  place: Place;
  /** Hidden by the buildings' cutaway (a storey above the one in view): body and rings out, the flag stays. */
  cut: boolean;
  /** What it is, for how it moves (gaits.ts). */
  template: TemplateId;
  /** The last blow at it (raiseWeapon, lunge, blastAt): where from, and when (performance.now). */
  blow: { from: THREE.Vector3; melee: boolean; head: boolean; at: number } | null;
  /** How it fell, and the seconds its death clip took (0 for the topple): for getting up again. */
  fell: { blow: Blow; seconds: number } | null;
}

/**
 * A line over an enemy's flag while the player aims a move or hovers it:
 * `aim` the hit chance against it, `sight` it would be in sight from the
 * hovered tile, `flank` in sight and flanked from there, `none` no line of fire.
 */
export interface EnemyTag {
  kind: 'aim' | 'sight' | 'flank' | 'none';
  text: string;
}

/** The pose handed to a figure each frame (one object for all, filled in place). */
const posed: FigurePose = { ...IDLE_POSE, spin: 0 };

/** The owner's ring: wide enough to tell whose soldier it is at a glance, whatever its armour. */
const ringGeometry = new THREE.RingGeometry(0.33, 0.46, 32).rotateX(-Math.PI / 2);

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

/** The battle's unit views on screen (one battle at a time): see unitPosNow. */
let onScreen: UnitViews | null = null;

/**
 * Where unit `id` stands on screen right now, mid-walk included, written into
 * `out` (nothing allocated); false if it has no view. Read-only, for other
 * views that follow the units (the buildings' cutaway, buildings.ts).
 */
export function unitPosNow(id: string, out: THREE.Vector3): boolean {
  return onScreen?.livePos(id, out) ?? false;
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
  /** Whether a place is hidden by the buildings' cutaway (MapView.isCutAway); set by the game. */
  private cutAway: ((p: Place) => boolean) | null = null;
  /** The state last shown, for what a step lands on (a window's sill). */
  private shown: GameState | null = null;
  /** The bodies' clock: it runs at the battle's speed (tween.ts timeRate), so clips and sways keep pace with the tweens. */
  private bodyTime = 0;

  constructor() {
    // Selection is white, never a player's colour (playtest: a selected soldier looked like Arturo's),
    // and a dark edge keeps it apart from the owner's ring just inside it.
    this.selection = new THREE.Mesh(
      new THREE.RingGeometry(0.55, 0.65, 40).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.95, depthWrite: false }),
    );
    const edge = new THREE.Mesh(
      new THREE.RingGeometry(0.49, 0.69, 40).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0.45, depthWrite: false }),
    );
    edge.position.y = -0.003;
    edge.renderOrder = 1;
    this.selection.renderOrder = 2;
    this.selection.add(edge);
    this.partnerSelection = new THREE.Mesh(
      new THREE.RingGeometry(0.5, 0.58, 40).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.75, depthWrite: false }),
    );
    this.selection.visible = false;
    this.partnerSelection.visible = false;
    this.group.add(this.selection, this.partnerSelection);
    onScreen = this;
  }

  /** Where unit `id` stands now (mid-walk included), into `out`; false if it has no view. */
  livePos(id: string, out: THREE.Vector3): boolean {
    const v = this.views.get(id);
    if (!v) return false;
    out.copy(v.rig.root.position);
    return true;
  }

  build(s: GameState): void {
    this.shown = s;
    for (const v of this.views.values()) {
      this.group.remove(v.rig.root);
      v.flag.element.remove();
    }
    this.views.clear();
    for (const u of s.units) this.create(u);
  }

  private create(u: Unit): UnitView {
    const color = unitColor(u);
    // The soldier's campaign id picks the same survivor as in the base; skirmish units go by name.
    // Who has no look (a VIP, an enemy leader) still gets a body of their sex, from their name.
    const sex = u.appearance ? undefined : sexOfPerson(u.name);
    const rig = buildUnit(u.template, color, u.appearance, gearOf(u.weaponTier, u.armorTier), u.campaignId ?? u.name, sex);
    const ring = new THREE.Mesh(
      ringGeometry,
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.95, depthWrite: false }),
    );
    ring.position.y = 0.03;

    const flagEl = document.createElement('div');
    flagEl.className = `uflag ${u.team === 'alien' ? 'alien' : 'xcom'}`;
    flagEl.style.setProperty('--accent', color);
    const flag = new CSS2DObject(flagEl);
    // Over the head: models that know their height say it; the rest are all about as tall.
    flag.position.set(0, rig.top !== undefined ? rig.top + 0.45 : 2.15, 0);
    flag.center.set(0.5, 1);

    rig.root.add(ring, flag);
    rig.root.position.copy(this.standAt(u.pos));
    const facing = u.team === 'alien' ? 0 : Math.PI;
    const pose: Pose = {
      crouch: 0, crouchTarget: 0, aim: 0, aimTarget: 0, aimFx: 0, run: 0, recoil: 0, hit: 0, flash: 0,
      fall: u.alive ? 0 : 1, lean: 0, hop: 0, spin: 0, search: 0, climb: 0, swing: 0, react: 0, yaw: facing, yawTarget: facing,
      phase: Math.random() * Math.PI * 2,
    };
    this.group.add(rig.root);

    const view: UnitView = {
      id: u.id, rig, ring, flag, flagEl, pose, animating: false, dead: !u.alive && !u.evacuated, droneAway: false, shadow: true, body: '', tag: '',
      place: { ...u.pos }, cut: false, template: u.template, blow: null, fell: null,
    };
    this.views.set(u.id, view);
    if (!u.alive) this.lieDown(view);
    flagEl.classList.toggle('named', u.id === this.selectedId);
    flagEl.classList.toggle('selected', u.id === this.selectedId);
    flag.renderOrder = u.id === this.selectedId ? 0 : -1;
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
    this.shown = s;
    const carrier = s.mission.item?.carrier ?? null;
    for (const u of s.units) {
      const v = this.views.get(u.id) ?? this.create(u);
      if (u.evacuated) {
        if (!v.animating) v.rig.root.visible = false;
        continue;
      }
      if (!v.animating && u.alive) v.rig.root.position.copy(this.standAt(u.pos));
      if (!v.animating) v.place = { ...u.pos };
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
      // Down to half its health or less, a soldier stands slumped.
      if (u.team === 'xcom' && isFigure(v.rig)) v.rig.setStance?.(u.alive && u.hp * 2 <= u.maxHp ? 'slumped' : null);
      if (u.alive) this.renderFlag(v, u, coverOf(u), carrier === u.id);
      if (!u.alive && !v.dead && !v.animating) {
        v.dead = true;
        v.pose.fall = 1;
        v.ring.visible = false;
        this.lieDown(v);
      }
      // Último verso brought it back (when the rise wasn't played, e.g. on a reload).
      if (u.alive && v.dead && !v.animating) this.standUp(v);
    }
    this.crowdShadows(s);
    this.placeRings();
  }

  /**
   * In a big crowd only the enemies nearest the squad cast shadows: the far
   * ones' are lost in the haze, and every shadow costs a draw call per frame.
   */
  private crowdShadows(s: GameState): void {
    const squad = new THREE.Vector3();
    let soldiers = 0;
    const enemies: { v: UnitView; d: number }[] = [];
    for (const u of s.units) {
      if (!u.alive) continue;
      const v = this.views.get(u.id);
      if (!v || v.dead) continue;
      if (u.team === 'xcom') {
        squad.add(v.rig.root.position);
        soldiers++;
      } else enemies.push({ v, d: 0 });
    }
    if (soldiers) squad.divideScalar(soldiers);
    for (const e of enemies) e.d = e.v.rig.root.position.distanceToSquared(squad);
    enemies.sort((a, b) => a.d - b.d);
    enemies.forEach((e, i) => this.castShadow(e.v, i < CROWD_SHADOWS));
  }

  private castShadow(v: UnitView, on: boolean): void {
    if (v.shadow === on) return;
    v.shadow = on;
    v.rig.root.traverse((o) => {
      if (o instanceof THREE.Mesh && o !== v.ring) o.castShadow = on;
    });
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
      u.infected ? '<em class="bite">MORDIDO</em>' : '',
      u.suppressedBy ? '<em class="stun">SUPRIMIDO</em>' : '',
      u.marked ? '<em class="aid">MARCADO</em>' : '',
      u.ruptured ? '<em class="stun">VULNERABLE</em>' : '',
      u.oath ? '<em class="song">JURAMENTO</em>' : '',
      u.ward ? '<em class="song">GUARDA</em>' : '',
      u.blessed ? '<em class="song">HIMNO</em>' : '',
      u.huntedBy ? '<em class="hunt">PRESA</em>' : '',
      carrier ? '<em class="data">DATOS</em>' : '',
      u.captive ? '<em class="data">CAUTIVO</em>' : '',
      u.objective ? '<em class="stun">OBJETIVO</em>' : '',
    ].join('');
    // Like XCOM 2, a soldier with a nickname goes by it in the field.
    const called = u.nickname ? `«${u.nickname}»` : u.name;
    const name = u.team === 'xcom' && u.template !== 'vip' ? `${TEMPLATES[u.template].short} · ${called}` : u.name;
    v.body = `<div class="uflag-name">${name}</div><div class="uflag-row">${coverIcon}<span class="hp">${pips}</span>${armor}${status}</div>`;
    this.paintFlag(v);
  }

  private paintFlag(v: UnitView): void {
    const html = v.tag + v.body;
    if (v.flagEl.innerHTML !== html) v.flagEl.innerHTML = html;
  }

  /** Hover lines over the enemies' flags (hit chances, sight and flanks); units left out lose theirs. */
  setTags(tags: Map<string, EnemyTag>): void {
    for (const v of this.views.values()) {
      const t = tags.get(v.id);
      const tag = t ? `<div class="uflag-tag ${t.kind}">${t.text}</div>` : '';
      if (tag === v.tag) continue;
      v.tag = tag;
      this.paintFlag(v);
    }
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

  /** Hides a unit for a moment (a song taking it apart and putting it back elsewhere). */
  setHidden(id: string, hidden: boolean): void {
    const v = this.views.get(id);
    if (!v) return;
    v.animating = hidden;
    v.rig.root.visible = !hidden;
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

  /**
   * Names are only shown for the selected and hovered units to keep flags
   * compact, and their flags come to the front: with the squad close
   * together the other flags go under them (UX review 3: a name covered its
   * neighbour's flag). The labels sort by renderOrder before distance
   * (CSS2DRenderer), so the rest go to −1 and these two stay at 0, where the
   * other labels (notes, finds, rising text) are.
   */
  private updateNames(): void {
    for (const v of this.views.values()) {
      const front = v.id === this.selectedId || v.id === this.hoveredId;
      v.flagEl.classList.toggle('named', front);
      v.flagEl.classList.toggle('selected', v.id === this.selectedId);
      v.flag.renderOrder = front ? 0 : -1;
    }
  }

  private placeRings(): void {
    const sel = this.selectedId ? this.views.get(this.selectedId) : undefined;
    this.selection.visible = !!sel && !sel.dead && !sel.cut;
    if (sel) this.selection.position.copy(sel.rig.root.position).setY(sel.rig.root.position.y + 0.035);
    const partner = this.partnerId ? this.views.get(this.partnerId) : undefined;
    this.partnerSelection.visible = !!partner && !partner.dead && !partner.cut && this.partnerId !== this.selectedId;
    if (partner) this.partnerSelection.position.copy(partner.rig.root.position).setY(partner.rig.root.position.y + 0.035);
  }

  // ------------------------------------------------------------- frame

  /** How to tell a place hidden by the buildings' cutaway (docs/FASE6.md): the game hands MapView.isCutAway. */
  setCutAway(test: (p: Place) => boolean): void {
    this.cutAway = test;
  }

  /**
   * A unit on a storey the cutaway hides (above the one in view) would float
   * over the cut: its body, ring and selection go, by render layers so its
   * own visibility stays as the game set it. Its flag stays, with the floor
   * on it («P1»): an enemy seen up there is still marked.
   */
  private setCut(v: UnitView, cut: boolean): void {
    if (v.cut === cut) return;
    v.cut = cut;
    v.rig.root.traverse((o) => {
      if (o === v.flag) return;
      if (cut) {
        o.userData.cutMask ??= o.layers.mask;
        o.layers.mask = 0;
      } else if (o.userData.cutMask !== undefined) {
        o.layers.mask = o.userData.cutMask as number;
        delete o.userData.cutMask;
      }
    });
    v.flagEl.classList.toggle('cut', cut);
    // The floor goes at the end of the flag's row (styles.css .uflag.cut), as a string for `content`.
    if (cut) v.flagEl.style.setProperty('--floor', `"▲ P${v.place.z ?? 0}"`);
    else v.flagEl.style.removeProperty('--floor');
  }

  tick(dt: number, time: number): void {
    const pulse = 1 + Math.sin(time * 5) * 0.06;
    this.selection.scale.setScalar(pulse);
    this.partnerSelection.scale.setScalar(2 - pulse);
    // Re-checked every frame: the cutaway follows the camera and the floor in view.
    if (this.cutAway) for (const v of this.views.values()) this.setCut(v, !!v.place.z && this.cutAway(v.place));
    this.placeRings();
    // Clips, sways and settling go at the battle's speed, like the tweens that move the bodies (their feet stay put).
    dt *= timeRate();
    time = this.bodyTime += dt;
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
    if (!isFigure(rig)) return;
    // Figures pose their own skeleton; the reload twirl is layered on top.
    // Rummaging crouches every figure, and they lean in and reach.
    posed.crouch = Math.max(p.crouch, p.search * 0.6);
    posed.aim = Math.max(p.aim, p.aimFx);
    posed.run = p.run;
    posed.recoil = p.recoil;
    posed.hit = p.hit;
    posed.flash = p.flash;
    posed.fall = p.fall;
    posed.lean = p.lean;
    posed.hop = p.hop;
    posed.yaw = p.yaw;
    posed.phase = p.phase;
    posed.spin = p.spin;
    posed.search = p.search;
    posed.climb = p.climb;
    posed.swing = p.swing;
    posed.react = p.react;
    rig.applyPose(posed, time);
    if (!rig.ownsReload) rig.gunPivot.rotation.z = p.spin * Math.PI * 2;
  }

  // -------------------------------------------------------- animations

  face(id: string, toward: THREE.Vector3): void {
    const v = this.views.get(id);
    if (!v) return;
    const a = angleTo(v.rig.root.position, toward);
    if (a !== null) v.pose.yawTarget = a;
  }

  /**
   * Walks unit `id` along `path`, a move of `ap` actions: at its template's
   * pace for one or for two (gaits.ts), crouched if it is. Resolves on arrival.
   */
  async moveAlong(id: string, path: Place[], onStep?: (p: THREE.Vector3) => void, ap = 1): Promise<void> {
    const v = this.views.get(id);
    if (!v || !path.length) return;
    v.animating = true;
    const p = v.pose;
    const gaits = gaitsOf(v.template);
    const gait = p.crouchTarget > 0.5 ? CROUCHED : ap >= 2 ? gaits.two : gaits.one;
    if (isFigure(v.rig)) v.rig.setGait?.(gait.motion, clipSpeed(v.template, gait));
    try {
      void tween(0.12, (t) => (p.run = Math.max(p.run, t)));
      for (const step of path) {
        const from = v.rig.root.position.clone();
        const to = this.standAt(step);
        this.face(id, to);
        const rise = to.y - from.y;
        if (this.stairsAt(step) || this.stairsAt(v.place)) {
          // On or off a building's stairs (a tile of flight between two floors): walked up or down its slope.
          await tween(gait.seconds * (Math.abs(rise) > 0.5 ? 1.6 : 1), (t) => v.rig.root.position.lerpVectors(from, to, t));
        } else if (Math.abs(rise) < 0.5 && this.windowAt(step)) {
          // Onto a window's sill (docs/FASE7.md): a crouched climb up to it. Out of it is the next step: down
          // to the ground beyond, or from a storey up, the fall.
          const sill = to.clone().setY(to.y + SILL);
          p.run = 0;
          await tween(HOP_SECONDS * 1.8, (t) => {
            v.rig.root.position.lerpVectors(from, sill, t);
            v.rig.root.position.y = from.y + (sill.y - from.y) * Math.min(1, t / 0.65) ** 0.6;
            p.crouch = Math.sin(t * Math.PI) * 0.7;
          });
          p.run = 1;
        } else if (rise > 0.5) {
          // Up a ladder: to the wall, climb, then onto the roof.
          const foot = from.clone().lerp(to, 0.4).setY(from.y);
          const top = foot.clone().setY(to.y);
          await tween(gait.seconds * 0.4, (t) => v.rig.root.position.lerpVectors(from, foot, t));
          p.run = 0;
          p.climb = 1;
          await tween(LADDER_SECONDS * (rise / STOREY), (t) => v.rig.root.position.lerpVectors(foot, top, t));
          p.climb = 0;
          p.run = 1;
          await tween(gait.seconds * 0.6, (t) => v.rig.root.position.lerpVectors(top, to, t));
        } else if (rise < -0.5) {
          // Drop off an edge or out of a window: a short hop out, then the fall, longer the higher it is
          // (off a roof one storey up, as it always was).
          await tween(HOP_SECONDS * (1.2 + 0.45 * -rise), (t) => {
            v.rig.root.position.lerpVectors(from, to, Math.min(1, t * 1.3));
            v.rig.root.position.y = from.y + rise * t * t + Math.sin(Math.min(1, t * 2) * Math.PI) * 0.35;
          });
          void tween(0.2, (t) => (p.crouch = Math.sin(t * Math.PI) * 0.8));
        } else {
          const seconds = gait.seconds * (from.distanceTo(to) > 1.2 ? 1.4 : 1);
          await tween(seconds, (t) => {
            v.rig.root.position.lerpVectors(from, to, t);
          });
        }
        // On its storey once there (docs/FASE6.md): the cutaway checks this.
        v.place = { ...step };
        onStep?.(v.rig.root.position);
      }
    } finally {
      v.animating = false;
      void tween(0.18, (t) => (p.run = 1 - t));
    }
  }

  /**
   * Falls through a broken floor to place `to` (docs/FASE7.md §5: the event
   * `fell`): no hop, the floor gives way under the unit, a short stagger
   * while it drops, and a crouch on landing. Resolves as it touches down.
   */
  async fall(id: string, to: Place): Promise<void> {
    const v = this.views.get(id);
    if (!v) return;
    v.animating = true;
    const p = v.pose;
    const from = v.rig.root.position.clone();
    const end = tileToWorld(to);
    const drop = Math.max(0.1, from.y - end.y);
    try {
      await tween(HOP_SECONDS * (1 + 0.5 * drop), (t) => {
        v.rig.root.position.lerpVectors(from, end, t);
        v.rig.root.position.y = from.y - drop * t * t;
        p.hit = Math.sin(t * Math.PI) * 0.6;
      });
      v.place = { ...to };
      void tween(0.25, (t) => (p.crouch = Math.sin(t * Math.PI) * 0.9));
    } finally {
      v.animating = false;
    }
  }

  /** A building's stairs on place `p` (a ladder tile dressed as stairs), in the state last shown. */
  private stairsAt(p: Place): boolean {
    const s = this.shown;
    if (!s || p.x < 0 || p.y < 0 || p.x >= s.width || p.y >= s.height) return false;
    const level = p.z ? s.levels?.[p.z - 1] : undefined;
    const i = p.y * s.width + p.x;
    return (p.z ? level?.tiles[i] : s.tiles[i]) === 'ladder' && decorOf((p.z ? level?.decor : s.decor)?.[i])?.kind === 'stairs';
  }

  /** Where a unit on place `p` stands: its tile's floor, or halfway up a building's stairs on them. */
  private standAt(p: Place): THREE.Vector3 {
    const at = tileToWorld(p);
    if (this.stairsAt(p)) at.y += STOREY / 2;
    return at;
  }

  /** Whether place `p` is a window (whole or broken) on its storey, in the state last shown. */
  private windowAt(p: Place): boolean {
    const s = this.shown;
    if (!s || p.x < 0 || p.y < 0 || p.x >= s.width || p.y >= s.height) return false;
    const tiles = p.z ? s.levels?.[p.z - 1]?.tiles : s.tiles;
    const t = tiles?.[p.y * s.width + p.x];
    return t === 'window' || t === 'windowBroken';
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
    this.struck(target, v.rig.root.position, false);
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

  /** A blow at whoever stands at `target`, from `from`: the way it falls if it dies of it (die). */
  private struck(target: THREE.Vector3, from: THREE.Vector3, melee: boolean): void {
    let hit: UnitView | null = null;
    let best = 0.6 * 0.6;
    for (const v of this.views.values()) {
      if (v.dead) continue;
      const d = v.rig.root.position.distanceToSquared(target);
      if (d < best) {
        best = d;
        hit = v;
      }
    }
    if (hit) hit.blow = { from: from.clone(), melee, head: false, at: performance.now() };
  }

  /** A blast at `at` (a grenade, a burst, a fireball): whoever it kills falls away from it. */
  blastAt(at: THREE.Vector3): void {
    const now = performance.now();
    for (const v of this.views.values()) {
      if (!v.dead && v.rig.root.position.distanceToSquared(at) < 6 * 6) v.blow = { from: at.clone(), melee: false, head: false, at: now };
    }
  }

  /** The blow `v` falls from, in its own frame (Blow), and where it has room to fall: not into a wall or high cover. */
  private blowAt(v: UnitView, from: THREE.Vector3 | null, head: boolean, melee: boolean): Blow {
    const way = from ? angleTo(v.rig.root.position, from) : null;
    const s = this.shown;
    const at = { x: v.place.x, y: v.place.y, z: v.place.z ?? 0 };
    const yaw = v.pose.yaw;
    // Every tile on the way, a quarter of a tile at a time out to where the head comes down, the shoulders' width across.
    const room = s && ((falls: number, reach: number) => {
      const dx = Math.sin(yaw + falls);
      const dy = Math.cos(yaw + falls);
      for (let d = 0.25; d < reach + 0.25; d += 0.25) {
        for (const side of [-0.2, 0, 0.2]) {
          const along = Math.min(d, reach);
          const tile = { x: Math.floor(at.x + 0.5 + dx * along + dy * side), y: Math.floor(at.y + 0.5 + dy * along - dx * side) };
          if ((tile.x !== at.x || tile.y !== at.y) && coverAt(s, at, tile) >= 2) return false;
        }
      }
      return true;
    });
    return { from: way === null ? null : wrapAngle(way - yaw), head, melee, seed: v.id, room: room || undefined };
  }

  /** A body already down when it comes into view (a reload, a death not played): its death's last pose. */
  private lieDown(v: UnitView): void {
    const blow = this.blowAt(v, null, false, false);
    v.fell = { blow, seconds: (isFigure(v.rig) && v.rig.die?.(blow)) || 0 };
  }

  /**
   * Knock-back and red flash; `strength` 1 for a normal hit, more for crits,
   * less for a graze. Figures with clips also play a hit reaction for the way
   * the blow came (syntyManifest.ts HITS), whose tail goes on after this resolves.
   */
  hitReact(id: string, strength = 1): Promise<void> {
    const v = this.views.get(id);
    if (!v) return Promise.resolve();
    // A critical is a shot to the head: if it kills, the unit falls as one.
    if (v.blow) v.blow.head = strength > 1;
    const last = v.blow && performance.now() - v.blow.at < BLOW_SECONDS * 1000 ? v.blow : null;
    const seconds = (isFigure(v.rig) && v.rig.react?.(this.blowAt(v, last?.from ?? null, strength > 1, !!last?.melee), strength)) || 0;
    if (seconds) void tween(seconds, (t) => (v.pose.react = t)).then(() => (v.pose.react = 0));
    return tween(0.4, (t) => {
      const k = Math.sin(Math.min(1, t * 1.6) * Math.PI);
      v.pose.hit = k * 0.6 * strength;
      v.pose.flash = 1 - t;
    }).then(() => {
      v.pose.hit = 0;
      v.pose.flash = 0;
    });
  }

  /**
   * Death: the figure's death clip played whole (syntyManifest.ts DEATHS),
   * the body falling away from `from`, else from the last blow at it (a shot,
   * a strike, a blast: raiseWeapon, lunge, blastAt), a shot to the head if
   * that was a critical; the stiff topple for a figure without clips.
   */
  async die(id: string, from?: THREE.Vector3): Promise<void> {
    const v = this.views.get(id);
    if (!v || v.dead) return;
    v.animating = true;
    v.flag.visible = false;
    v.pose.aimFx = 0;
    const last = v.blow && performance.now() - v.blow.at < BLOW_SECONDS * 1000 ? v.blow : null;
    const blow = this.blowAt(v, from ?? last?.from ?? null, !!last?.head, !from && !!last?.melee);
    const seconds = (isFigure(v.rig) && v.rig.die?.(blow)) || 0;
    v.fell = { blow, seconds };
    if (seconds) {
      await tween(seconds, (t) => {
        v.pose.fall = t;
        v.pose.flash = Math.max(0, 0.6 - (t * seconds) / 0.75);
      });
    } else {
      await tween(0.75, (t) => {
        v.pose.fall = t * t;
        v.pose.flash = Math.max(0, 0.6 - t);
      });
      // The body hits the ground, bounces a little and settles.
      await tween(0.3, (t) => (v.pose.fall = 1 - Math.sin(t * Math.PI) * 0.07 * (1 - t * 0.5)));
    }
    v.pose.fall = 1;
    v.pose.flash = 0;
    v.dead = true;
    v.ring.visible = false;
    v.rig.root.traverse((o) => {
      if (o instanceof THREE.Mesh) o.castShadow = false;
    });
    v.shadow = false;
    v.animating = false;
  }

  /** Último verso: the fallen soldier gets back on its feet. */
  async rise(id: string): Promise<void> {
    const v = this.views.get(id);
    if (!v || !v.dead) return;
    v.animating = true;
    // Its death played backwards, a little quicker; or the topple undone.
    const clip = v.fell?.seconds ?? 0;
    await tween(Math.max(0.6, clip * 0.7), (t) => {
      v.pose.fall = clip ? 1 - t : 1 - t * (2 - t);
      v.pose.flash = 0.5 * (1 - t);
    });
    this.standUp(v);
    v.animating = false;
  }

  private standUp(v: UnitView): void {
    v.dead = false;
    v.pose.fall = 0;
    if (isFigure(v.rig)) v.rig.rise?.();
    v.fell = null;
    v.pose.flash = 0;
    v.ring.visible = true;
    v.flag.visible = true;
    v.rig.root.traverse((o) => {
      if (o instanceof THREE.Mesh) o.castShadow = true;
    });
    v.shadow = true;
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
    // The arm well back before the throw (a wind-up one can see: Arturo, 2026-10-14).
    await tween(0.35, (t) => (v.pose.lean = -0.35 * easeInOut(t)));
    await tween(0.14, (t) => (v.pose.lean = -0.35 + 0.65 * t));
    void tween(0.25, (t) => (v.pose.lean = 0.3 * (1 - t)));
  }

  /** Melee strike: a quick lunge towards the target and back. */
  async lunge(id: string, target: THREE.Vector3): Promise<void> {
    const v = this.views.get(id);
    if (!v) return;
    this.face(id, target);
    const home = v.rig.root.position.clone();
    this.struck(target, home, true);
    const toward = target.clone().setY(home.y).sub(home).setLength(0.45);
    v.animating = true;
    // Wound up (the body turned back, the dead's arms open), the stroke across or the grab, and back (Arturo, 2026-10-14:
    // the Tajo cuts, the bite grabs).
    await tween(0.2, (t) => {
      v.pose.lean = -0.25 * t;
      v.pose.aimFx = t;
      v.pose.swing = -easeInOut(t);
    });
    await tween(0.14, (t) => {
      v.pose.lean = -0.25 + 0.75 * t;
      v.pose.swing = -1 + 2 * easeInOut(t);
      v.rig.root.position.copy(home).addScaledVector(toward, t);
    });
    void tween(0.35, (t) => {
      v.pose.lean = 0.5 * (1 - t);
      v.pose.aimFx = 1 - t;
      v.pose.swing = 1 - easeInOut(t);
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

  /**
   * Searching a piece of furniture at `at`: the unit turns to it, crouches,
   * leans in and rummages, and straightens up (0.85 s in all).
   */
  async rummage(id: string, at: THREE.Vector3): Promise<void> {
    const v = this.views.get(id);
    if (!v) return;
    this.face(id, at);
    await tween(0.22, (t) => (v.pose.search = easeInOut(t)));
    await wait(0.45);
    await tween(0.18, (t) => (v.pose.search = 1 - easeInOut(t)));
    v.pose.search = 0;
  }

  /** The Chillón's scream: head back and arms flung out (its aim pose), held, then let go. */
  async scream(id: string): Promise<void> {
    const v = this.views.get(id);
    if (!v) return;
    await tween(0.18, (t) => (v.pose.aimFx = easeInOut(t)));
    await wait(0.55);
    void tween(0.3, (t) => (v.pose.aimFx = 1 - easeInOut(t)));
  }

  /** The top of a unit's head, where a scream starts. */
  headPos(id: string): THREE.Vector3 {
    const v = this.views.get(id);
    return this.worldPos(id).add(new THREE.Vector3(0, (v?.rig.top ?? 1.8) - 0.15, 0));
  }

  /**
   * A unit that arrives (reinforcements, the horde): its view is made now, if
   * the state has not got it yet, and it comes on foot from `from` to its
   * tile, the dead staggering and the living running; the event that follows
   * puts it in the state where it already stands.
   */
  async arrive(u: Unit, from: THREE.Vector3, seconds: number): Promise<void> {
    const v = this.views.get(u.id) ?? this.create(u);
    const to = tileToWorld(u.pos);
    v.animating = true;
    v.flag.visible = false;
    v.rig.root.visible = true;
    v.rig.root.position.copy(from);
    this.face(u.id, to);
    v.pose.yaw = v.pose.yawTarget;
    v.pose.run = 1;
    await tween(seconds, (t) => v.rig.root.position.lerpVectors(from, to, t));
    void tween(0.2, (t) => (v.pose.run = 1 - t));
    v.animating = false;
  }

  /** A unit that gets up where it stands (arrivals far from any edge): from a crouch, out of the dust. */
  async getUp(u: Unit): Promise<void> {
    const v = this.views.get(u.id) ?? this.create(u);
    v.rig.root.position.copy(tileToWorld(u.pos));
    v.rig.root.visible = true;
    v.flag.visible = false;
    v.pose.crouch = 1;
    v.pose.crouchTarget = 0;
    await tween(0.5, (t) => (v.pose.crouch = 1 - easeInOut(t)));
  }

  /**
   * A corpse the Chillón's shriek raises: it twitches where it lies, then the
   * dead it has become (the event's new unit) gets up stiffly in its place.
   */
  async reanimate(corpseId: string, zombie: Unit): Promise<void> {
    const body = this.views.get(corpseId);
    if (body) {
      body.animating = true;
      for (let i = 0; i < 3; i++) await tween(0.13 + i * 0.03, (t) => (body.pose.hit = Math.sin(t * Math.PI) * 0.6));
      body.rig.root.visible = false;
      body.animating = false;
    }
    const v = this.views.get(zombie.id) ?? this.create(zombie);
    v.animating = true;
    v.rig.root.position.copy(tileToWorld(zombie.pos));
    v.rig.root.visible = true;
    v.flag.visible = false;
    if (body) v.pose.yaw = v.pose.yawTarget = body.pose.yaw;
    v.pose.fall = 1;
    // It gets up out of the body's death, played backwards (the same clip, as the body's seed picks it).
    const clip = (body?.fell && isFigure(v.rig) && v.rig.die?.(body.fell.blow)) || 0;
    await tween(Math.max(0.9, clip * 0.8), (t) => (v.pose.fall = clip ? 1 - t : 1 - easeInOut(t)));
    v.pose.fall = 0;
    if (isFigure(v.rig)) v.rig.rise?.();
    v.animating = false;
  }

  /** Startled hop when an alien pod notices the squad. */
  alert(id: string): Promise<void> {
    const v = this.views.get(id);
    if (!v) return Promise.resolve();
    return tween(0.35, (t) => (v.pose.hop = Math.sin(t * Math.PI) * 0.22));
  }
}
