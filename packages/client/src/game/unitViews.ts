import * as THREE from 'three';
import { CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { BITE_TAG, canStandOn, coverAt, decorOf, DIRS4, flightAt, kindAt, objectAt, PLAYER_COLORS, sexOfPerson, type Alarm, type Biome, type DecorKind, type GameState, type Place, type TemplateId, type Unit, type Vec2 } from '@bastion/engine';
import { pieceMap, TOWER_LADDER } from './basePieces';
import { groundAt, STOREY, tileToWorld } from './coords';
import type { Blow, FigurePose, Gear } from './figures';
import { clipSpeed, CROUCHED, gaitsOf, type Gait } from './gaits';
import { CROWD } from './crowd';
import { buildUnit, gearOf, IDLE_POSE, isFigure, type UnitRig } from './models';
import { armamentOf, type Siren } from './syntyFigures';
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
/**
 * How far out of a window's wall (tiles from its middle) a unit vaulting it stays crouched: its head under
 * the frame's top (1.84 over the floor; standing on the sill, upright, it came to 2.1).
 */
const UNDER_LINTEL = 0.35;
/** Seconds a blow (a shot, a strike, a blast) still says which way the unit it came at falls if it dies. */
const BLOW_SECONDS = 8;
/**
 * A building's stairs (buildings.ts stairs; docs/MAPAS.md, phase 2): a flight of several tiles has
 * FLIGHT_STEPS treads a tile, from the back edge of its lowest tile up to the landing a storey higher in front
 * of its top; one of a single tile (of before) STAIR_STEPS across it. A unit's feet go up the line half a riser
 * over the treads' front edges (flightOf, flightHeight, stairWay and walk's 'stairs' step).
 */
const STAIR_STEPS = 9;
const FLIGHT_STEPS = 6;
/** On the way onto the stairs from beside them or round the corner: the foot of the flight, behind its first tread, and the corner's point (tiles from the stairs' middle). */
const STAIR_FOOT = 0.7;
/** A climber's middle stands this far behind the rungs (tiles): its hands reach them, its chest clear (rung: syntyFigures.ts). */
const CLIMB_GAP = 0.2;
/** Where a climber's chest is over its feet, where the rungs it holds are (tiles). */
const CLIMB_CHEST = 0.9;
/**
 * A building's ladder (buildings.ts): upright against the tile one storey up, this far from the ladder tile's
 * middle towards it.
 */
const LADDER_RUNGS = 0.44;
/** Aimed at one close by, the muzzle stays this far short of its middle (its body's front, and a little), the body stepped back this much at most (tiles). */
const CLOSE_GAP = 0.3;
const CLOSE_BACK = 0.3;
/** How far to one side of a tile's middle a walker goes by another there (walkPlan.ts passes), in tiles. */
const SWERVE = 0.24;
/** A melee lunge goes this far at most, and stops this far short of the target's middle (an adjacent one: 0.72 apart). */
const LUNGE = 0.45;
const LUNGE_SHORT = 0.72;
/** What stands on a floor tile and takes it whole (furniture, crates, machines): no body steps out onto it nor falls across it. */
const BULKY = new Set<DecorKind>([
  'shelf', 'counter', 'table', 'sofa', 'bed', 'desk', 'wardrobe', 'cabinet', 'rack', 'crates', 'barricade', 'tank', 'machine', 'forklift',
  'drums', 'pallets', 'hospitalBed', 'medicalShelf', 'gurney', 'medicalCabinet', 'medicalCart', 'receptionDesk', 'waitingSeats', 'examTable',
  'xray', 'bedsideTable', 'clinicalSink', 'sterilizer', 'ultrasound', 'waitingTable', 'waterDispenser', 'toilet', 'consultDesk', 'bench', 'fridge',
  'stove', 'bathtub', 'washbasin', 'tvStand', 'wc', 'lootWeapons', 'lootMedical', 'lootAmmo', 'lootFood', 'statue', 'kiosk', 'planter', 'bin',
]);

/** A building's flight of stairs as a walk sees it (engine flights.ts): the way it climbs, how many tiles, its lowest, and which tile a place is. */
interface FlightView {
  d: Vec2;
  n: number;
  low: Place;
  step: number;
}

/** One step of a walk as walk() plays it: what kind of step, and the seconds it takes. */
type StepKind = 'flat' | 'stairs' | 'sill' | 'offSill' | 'ladderUp' | 'ladderDown' | 'drop';

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
  /** Through a stone's toss (tossStone), 0 to 1. */
  toss: number;
  /** Aiming at one this close (tiles; 0 none): at point blank the body steps back from it with a long gun (raiseWeapon, apply). */
  close: number;
  yaw: number;
  yawTarget: number;
  /** How fast the yaw eases to its target: brisk for a step or a strike, slower for a turn on the spot. */
  turn: number;
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
  /** A campaign soldier's own weapons as last shown (Unit.arms: armsKey), to see one taken in hand. */
  arms: string;
  /** Its last walk (moveAlong), which the next waits for, and how many it has set off on. */
  walk: Promise<void> | null;
  walks: number;
  /** How many aims it has taken (raiseWeapon): a lowering meant for an earlier one leaves a later one be. */
  aims: number;
  /** On guard with the blade (Vigilancia cuerpo a cuerpo: Unit.watchMelee), as last shown. */
  guard: boolean;
}

/** How high the blade is held on guard (the pose's aim): up in front, its point a little up. */
const GUARD_AIM = 0.72;
/** The yaw's ease for steps, strikes and aims (face()). */
const TURN = 14;
/** A dead one turning where it stands to where the engine says it looks (a noise, the start): about half a second. */
const TURN_ON_SPOT = 5;

/** A sabotage's alarm by the map, where the mission does not say (skirmishes, older saves): as sfx.ts picks its sound. */
const ALARM_BY_MAP: Record<Biome, Alarm> = { city: 'police', wilds: 'ambulance', facility: 'factory' };
/** The four ways round a tile. */
const SIDES: [number, number][] = [[1, 0], [0, 1], [-1, 0], [0, -1]];

/** What a campaign soldier carries, as a key: its weapon in the hand and at the belt. */
const armsKey = (u: Unit) => (u.arms ? `${u.arms.primary}|${u.arms.melee}` : '');

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

/** A model's yaw (atan2(dx, dz), a model looks along +z) for the engine's Unit.facing (atan2(dy, dx) in tiles; y is +z). */
function yawOf(facing: number): number {
  return wrapAngle(Math.PI / 2 - facing);
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
  /** The mission item's tag over its carrier (lore.ts itemTag: «MEDICINAS»); set by the game (UI, playtest 14/10 #2). */
  itemTag = 'CARGA';
  private views = new Map<string, UnitView>();
  private readonly selection: THREE.Mesh;
  private readonly partnerSelection: THREE.Mesh;
  private selectedId: string | null = null;
  /** How strong a hit's red flash shows (1 in battle; the trailer's close shots soften it: lab/trailer/sala.ts). */
  flashScale = 1;
  private hoveredId: string | null = null;
  private partnerId: string | null = null;
  /** Whether a place is hidden by the buildings' cutaway (MapView.isCutAway); set by the game. */
  private cutAway: ((p: Place) => boolean) | null = null;
  /** The state last shown, for what a step lands on (a window's sill). */
  private shown: GameState | null = null;
  /** The bodies' clock: it runs at the battle's speed (tween.ts timeRate), so clips and sways keep pace with the tweens. */
  private bodyTime = 0;

  constructor() {
    // Selection is white, never a player's colour (playtest: a selected soldier looked like Arturo's). One ring
    // and nothing else round the soldier (playtest 18/10 #28): crisp, in three arcs that turn slowly (tick), on a
    // thin dark edge that keeps it apart from the ground, snow included.
    const arcs = [0, 1, 2].map((i) => new THREE.RingGeometry(0.5, 0.58, 24, 1, (i * 2 * Math.PI) / 3, (Math.PI * 2) / 3 - 0.35).rotateX(-Math.PI / 2));
    this.selection = new THREE.Mesh(mergeGeometries(arcs)!, new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.95, depthWrite: false }));
    const edge = new THREE.Mesh(
      new THREE.RingGeometry(0.47, 0.61, 48).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0.35, depthWrite: false }),
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
    this.group.add(this.selection, this.partnerSelection, CROWD.group);
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
    // A new battle: a crowd of its own (its bodies are baked again as they come).
    CROWD.clear();
    for (const u of s.units) this.create(u);
  }

  private create(u: Unit): UnitView {
    const color = unitColor(u);
    // The soldier's campaign id picks the same survivor as in the base; skirmish soldiers go by name. The
    // enemy go by their id: they share their template's name («Zombi»), and by it every one was the same body.
    // Who has no look (a VIP, an enemy leader) still gets a body of their sex, from their name.
    const sex = u.appearance ? undefined : sexOfPerson(u.name);
    // A campaign soldier's own weapons (docs/FASE8.md §A); anyone else's are its template's.
    const gear: Gear = { ...gearOf(u.weaponTier, u.armorTier), arms: u.arms ?? null };
    // A soldier risen as one of the dead (Unit.risenFrom, its look theirs) keeps the body they had: picked as theirs was.
    const fallen = u.risenFrom ? this.shown?.units.find((x) => x.id === u.risenFrom) : undefined;
    const seed = fallen ? (fallen.campaignId ?? fallen.name) : (u.campaignId ?? (u.team === 'xcom' ? u.name : u.id));
    const rig = buildUnit(u.template, color, u.appearance, gear, seed, sex, u.template === 'relay' ? this.sirenOf(u) : undefined);
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
    // Where the engine says it looks (the dead from the start: their view cone); without that, the squad faces north.
    const facing = u.facing !== undefined ? yawOf(u.facing) : u.team === 'alien' ? 0 : Math.PI;
    const pose: Pose = {
      crouch: 0, crouchTarget: 0, aim: 0, aimTarget: 0, aimFx: 0, run: 0, recoil: 0, hit: 0, flash: 0,
      fall: u.alive || u.lost ? 0 : 1, lean: 0, hop: 0, spin: 0, search: 0, climb: 0, swing: 0, react: 0, toss: 0, close: 0, yaw: facing, yawTarget: facing, turn: TURN,
      phase: Math.random() * Math.PI * 2,
    };
    this.group.add(rig.root);

    const view: UnitView = {
      id: u.id, rig, ring, flag, flagEl, pose, animating: false, dead: !u.alive && !u.evacuated && !u.lost, shadow: true, body: '', tag: '',
      place: { ...u.pos }, cut: false, template: u.template, blow: null, fell: null, arms: armsKey(u), walk: null, walks: 0, aims: 0, guard: false,
    };
    this.views.set(u.id, view);
    // Left outside when La Mula went (sync): standing, not dead.
    if (!u.alive && !u.lost) this.lieDown(view);
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
      // Left outside when La Mula went (docs/PRUEBA-2026-10-14.md P1): no longer one of the squad, but standing, not dead.
      if (u.lost) {
        v.flag.visible = false;
        v.ring.visible = false;
        continue;
      }
      if (!v.animating && u.alive) v.rig.root.position.copy(this.standAt(u.pos));
      if (!v.animating) v.place = { ...u.pos };
      // A corpse raised by a sectoid stands up as a new unit: hide the body.
      if (u.reanimated && v.dead) {
        v.rig.root.visible = false;
        continue;
      }
      // Mind control turns rings and flags hostile. The squad's own go without (18/10 #28): only the one picked has a ring.
      const color = unitColor(u);
      (v.ring.material as THREE.MeshBasicMaterial).color.set(color);
      (v.ring.material as THREE.MeshBasicMaterial).visible = u.team !== 'xcom';
      v.flagEl.style.setProperty('--accent', color);
      v.flagEl.classList.toggle('alien', u.team === 'alien');
      v.flagEl.classList.toggle('xcom', u.team === 'xcom');
      const shown = u.team === 'xcom' || u.objective || visibleAliens.has(u.id) || (v.dead && v.rig.root.visible);
      v.rig.root.visible = shown;
      v.flag.visible = shown && u.alive;
      v.pose.crouchTarget = u.hunkered || u.captive ? 1 : 0;
      // The enemies see in a cone (los.ts inViewCone, the living too: 18/10 #46): standing still, the model looks
      // where the engine says, turning on the spot when that changes (a noise heard); a walk or a bite ends on the same angle.
      if (u.alive && !v.animating && u.facing !== undefined && u.team !== 'xcom') {
        const yaw = yawOf(u.facing);
        if (Math.abs(wrapAngle(yaw - v.pose.yawTarget)) > 0.01) {
          v.pose.yawTarget = yaw;
          v.pose.turn = TURN_ON_SPOT;
        }
      }
      // On watch: the gun up; with the blade (docs/FASE10.md §4: Unit.watchMelee), drawn from the belt and held on guard.
      const guard = !!u.overwatch && !!u.watchMelee && u.alive;
      v.pose.aimTarget = u.overwatch ? (guard ? GUARD_AIM : 1) : 0;
      if (guard !== v.guard && !v.animating && isFigure(v.rig)) {
        v.guard = guard;
        v.rig.draw?.(guard ? 'belt' : 'weapon');
      }
      // Down to half its health or less, a soldier stands slumped.
      if (u.team === 'xcom' && isFigure(v.rig)) v.rig.setStance?.(u.alive && u.hp * 2 <= u.maxHp ? 'slumped' : null);
      // A weapon from the backpack taken in hand (the engine's weaponEquipped).
      if (u.alive && armsKey(u) !== v.arms) {
        v.arms = armsKey(u);
        void this.equip(v, u);
      }
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
      u.overwatch ? `<em class="ow">${u.killZone ? 'ZONA LETAL' : u.watchMelee ? 'HOJA EN ALTO' : 'VIGILANCIA'}</em>` : '',
      u.hunkered ? '<em class="hk">CUBRIÉNDOSE</em>' : '',
      u.aided ? '<em class="aid">PROTEGIDO</em>' : '',
      u.stunned ? '<em class="stun">ATURDIDO</em>' : '',
      u.disoriented ? '<em class="psi">DESORIENTADO</em>' : '',
      u.panicked ? '<em class="psi">PÁNICO</em>' : '',
      u.infected ? `<em class="bite">${BITE_TAG}</em>` : '',
      u.suppressedBy ? '<em class="stun">SUPRIMIDO</em>' : '',
      u.marked ? '<em class="aid">MARCADO</em>' : '',
      u.ruptured ? '<em class="stun">VULNERABLE</em>' : '',
      carrier ? `<em class="data">${this.itemTag}</em>` : '',
      u.captive ? '<em class="data">CAUTIVO</em>' : '',
      u.objective ? '<em class="stun">OBJETIVO</em>' : '',
    ].join('');
    // Like XCOM 2, a soldier with a nickname goes by it in the field.
    const called = u.nickname ? `«${u.nickname}»` : u.name;
    // Fase 9: no classes, so no class code before the name (UI).
    const name = u.team === 'xcom' && u.template !== 'vip' ? called : u.name;
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

  /**
   * Into La Lata (the event `evacuated`): out of sight at once, its ring and
   * flag with it, so the last one aboard is not left standing outside as the
   * bus pulls away (playtest 16/10 #2: sync hides them, but only once the
   * event is applied, after the bus has gone).
   */
  board(id: string): void {
    const v = this.views.get(id);
    if (v) v.rig.root.visible = false;
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
    if (isFigure(v.rig)) v.rig.cutAway = cut;
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
    // The picked one's ring turns (18/10 #28: «en movimiento»); the partner's still breathes.
    this.selection.rotation.y = -time * 0.9;
    this.partnerSelection.scale.setScalar(2 - pulse);
    // Re-checked every frame: the cutaway follows the camera and the floor in view.
    if (this.cutAway) for (const v of this.views.values()) this.setCut(v, !!v.place.z && this.cutAway(v.place));
    this.placeRings();
    // Clips, sways and settling go at the battle's speed, like the tweens that move the bodies (their feet stay put).
    dt *= timeRate();
    time = this.bodyTime += dt;
    CROWD.tick(dt);
    for (const v of this.views.values()) {
      const p = v.pose;
      const k = 1 - Math.exp(-dt * 9);
      p.crouch += (p.crouchTarget - p.crouch) * k;
      p.aim += (p.aimTarget - p.aim) * k;
      p.yaw += wrapAngle(p.yawTarget - p.yaw) * (1 - Math.exp(-dt * p.turn));
      this.apply(v, time);
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
    posed.flash = p.flash * this.flashScale;
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
    posed.toss = p.toss;
    rig.applyPose(posed, time);
    // At point blank (raiseWeapon) it steps back as it shoulders a long gun, its muzzle short of whoever it aims at
    // (a machine gun reached 0.86 of a tile ahead, into one beside): the body, not the unit's place, moves.
    const back = p.close > 0 ? Math.min(CLOSE_BACK, Math.max(0, (rig.aimReach?.() ?? 0) - (p.close - CLOSE_GAP))) * Math.max(p.aim, p.aimFx) : 0;
    if (back > 0 || rig.yaw.position.x || rig.yaw.position.z) rig.yaw.position.set(-Math.sin(p.yaw) * back, 0, -Math.cos(p.yaw) * back);
    if (!rig.ownsReload) rig.gunPivot.rotation.z = p.spin * Math.PI * 2;
  }

  // -------------------------------------------------------- animations

  face(id: string, toward: THREE.Vector3): void {
    const v = this.views.get(id);
    if (!v) return;
    const a = angleTo(v.rig.root.position, toward);
    if (a !== null) v.pose.yawTarget = a;
    v.pose.turn = TURN;
  }

  /**
   * Where unit `id`'s model looks right now, in the engine's terms (Unit.facing: atan2(dy, dx) in tiles):
   * the same as the engine at rest, in between while it turns. For drawing the view cone with the model.
   */
  facingNow(id: string): number | null {
    const v = this.views.get(id);
    return v ? wrapAngle(Math.PI / 2 - v.pose.yaw) : null;
  }

  /**
   * Walks unit `id` along `path`, a move of `ap` actions: at its template's
   * pace for one or for two (gaits.ts), crouched if it is. Resolves on arrival.
   * A unit's walks go one after the other, even when they are played at once
   * (a group of the dead walking together, where one may move twice: game.ts
   * playGroup; playtest 15/10 #19, two walks pulling a body back and forth):
   * the next sets off from where the last ends, without stopping. `pace`: how
   * many times quicker than its gait, the clip keeping pace (the enemy turn's
   * walkers, all at once: game.ts, playtest 16/10 #6). `waits`: seconds it
   * stands before each step, so that walks played at once don't go through
   * one another, and `passes`, the tiles of the path where it goes by another
   * all the same, swerving round them (walkPlan.ts).
   */
  async moveAlong(id: string, path: Place[], onStep?: (p: THREE.Vector3) => void, ap = 1, pace = 1, waits?: readonly number[], passes?: readonly boolean[]): Promise<void> {
    const v = this.views.get(id);
    if (!v || !path.length) return;
    const before = v.walk;
    const walk = (async () => {
      await before?.catch(() => undefined);
      await this.walk(v, id, path, onStep, ap, pace, waits, passes);
    })();
    v.walk = walk;
    try {
      await walk;
    } finally {
      if (v.walk === walk) v.walk = null;
    }
  }

  /** The gait of unit `v`'s walk of `ap` actions, `pace` times quicker. */
  private gaitFor(v: UnitView, ap: number, pace: number): Gait {
    const gaits = gaitsOf(v.template);
    const base = v.pose.crouchTarget > 0.5 ? CROUCHED : ap >= 2 ? gaits.two : gaits.one;
    return pace === 1 ? base : { ...base, seconds: base.seconds / pace };
  }

  /**
   * The seconds each step of unit `id`'s walk along `path` from `start` takes, as walk() plays it (for walks
   * planned together: walkPlan.ts); empty without its view.
   */
  stepSeconds(id: string, start: Place, path: readonly Place[], ap = 1, pace = 1): number[] {
    const v = this.views.get(id);
    if (!v) return [];
    const gait = this.gaitFor(v, ap, pace);
    let from = start;
    return path.map((to) => {
      const seconds = this.stepOf(from, to, gait).seconds;
      from = to;
      return seconds;
    });
  }

  /** What the step from `from` to `to` is, and the seconds it takes at `gait`. */
  private stepOf(from: Place, to: Place, gait: Gait): { kind: StepKind; seconds: number } {
    const fromY = this.standAt(from).y + (this.windowAt(from) ? SILL : 0);
    const rise = this.standAt(to).y - fromY;
    const g = gait.seconds;
    const flight = this.flightOf(to) ?? this.flightOf(from);
    // A tile of a flight of several, up or down it: a little slower than on the flat.
    if (flight && flight.n > 1) return { kind: 'stairs', seconds: g * 1.25 };
    if (flight) {
      // Round the corner from beside the flight is longer than straight up it: a little longer to walk.
      const way = this.stairWay(from, to);
      let length = 0;
      for (let k = 1; k < way.length; k++) length += Math.hypot(way[k]!.x - way[k - 1]!.x, way[k]!.z - way[k - 1]!.z);
      return { kind: 'stairs', seconds: g * (Math.abs(rise) > 0.5 ? 1.6 : 1) * Math.max(1, length / 1.6) };
    }
    if (Math.abs(rise) < 0.5 && this.windowAt(to)) return { kind: 'sill', seconds: HOP_SECONDS * 1.8 };
    if (this.windowAt(from)) return { kind: 'offSill', seconds: HOP_SECONDS * (1.2 + 0.45 * Math.max(0, -rise)) };
    if (rise > 0.5) return { kind: 'ladderUp', seconds: g + LADDER_SECONDS * (rise / STOREY) };
    if (rise < -0.5 && this.ladderDown(from, to, rise)) return { kind: 'ladderDown', seconds: g + LADDER_SECONDS * (-rise / STOREY) };
    if (rise < -0.5) return { kind: 'drop', seconds: HOP_SECONDS * (1.2 + 0.45 * -rise) };
    return { kind: 'flat', seconds: g * (from.x !== to.x && from.y !== to.y ? 1.4 : 1) };
  }

  /** Down a storey from `from` onto a ladder (not a building's stairs): climbed down, not jumped (path.ts stepBetween's COST_CLIMB). */
  private ladderDown(from: Place, to: Place, rise: number): boolean {
    const s = this.shown;
    if (!s || Math.abs(rise + STOREY) > 0.3 || Math.abs(to.x - from.x) + Math.abs(to.y - from.y) !== 1) return false;
    return kindAt(s, { x: to.x, y: to.y, z: to.z ?? 0 }) === 'ladder' && !this.flightOf(to);
  }

  private async walk(v: UnitView, id: string, path: Place[], onStep: ((p: THREE.Vector3) => void) | undefined, ap: number, pace = 1, waits?: readonly number[], passes?: readonly boolean[]): Promise<void> {
    v.animating = true;
    const walks = ++v.walks;
    const p = v.pose;
    const gait = this.gaitFor(v, ap, pace);
    if (isFigure(v.rig)) v.rig.setGait?.(gait.motion, clipSpeed(v.template, gait));
    const pos = v.rig.root.position;
    const swerves = passes?.some(Boolean) ? this.swerves(v.place, path, passes, gait) : null;
    /** Its own crouch (hunkered or not) while a window's vault holds it down, from the sill to the ground beyond. */
    let vaulting: number | null = null;
    try {
      for (let k = 0; k < path.length; k++) {
        const step = path[k]!;
        // Its turn to go there (walkPlan.ts): it stands, turned to it, until then.
        const hold = waits?.[k] ?? 0;
        if (hold > 0.02) {
          this.face(id, this.standAt(step));
          const from = p.run;
          await tween(hold, (t) => (p.run = from * (1 - Math.min(1, (t * hold) / 0.15))));
        }
        if (k === 0 || hold > 0.02) void tween(0.12, (t) => (p.run = Math.max(p.run, t)));
        const from = pos.clone();
        const to = this.standAt(step);
        const { kind, seconds } = this.stepOf(v.place, step, gait);
        this.face(id, to);
        const rise = to.y - from.y;
        if (kind === 'stairs') {
          // On, along or off a building's stairs: up or down the line of its treads, the feet on them; onto a flight of
          // one (of before) by its foot (or off by it) when coming from beside it, never across the banisters.
          const way = this.stairWay(v.place, step);
          way[0]!.copy(from);
          const lengths = way.slice(1).map((q, i) => Math.hypot(q.x - way[i]!.x, q.z - way[i]!.z));
          const total = lengths.reduce((a, b) => a + b, 0) || 1;
          let leg = -1;
          await tween(seconds, (t) => {
            let left = t * total;
            let i = 0;
            while (i < lengths.length - 1 && left > lengths[i]!) left -= lengths[i++]!;
            const a = way[i]!;
            const b = way[i + 1]!;
            pos.lerpVectors(a, b, Math.min(1, left / (lengths[i]! || 1)));
            // Turned to where it goes a few tenths of a tile on (round the foot of the flight, it turns with the way).
            if (i !== leg && i + 1 < way.length - 1) {
              leg = i;
              this.face(id, way[Math.min(way.length - 1, i + 3)]!);
            }
          });
        } else if (kind === 'sill') {
          // Onto a window's sill (docs/FASE7.md): a crouched climb up to it, down low by the time the head comes
          // under the frame's top and staying so on the sill. Out of it is the next step.
          const sill = to.clone().setY(to.y + SILL);
          vaulting ??= p.crouchTarget;
          p.crouchTarget = 1;
          p.run = 0;
          await tween(seconds, (t) => {
            pos.lerpVectors(from, sill, t);
            pos.y = from.y + (sill.y - from.y) * Math.min(1, t / 0.65) ** 0.6;
            p.crouch = Math.max(p.crouch, easeInOut(Math.min(1, t / 0.6)));
          });
          p.crouch = 1;
        } else if (kind === 'offSill') {
          // Off the sill, still crouched through the frame, then down to the ground beyond (or the fall, from a
          // storey up): no hop up into the lintel.
          const crouch = vaulting ?? p.crouchTarget;
          vaulting = null;
          p.crouchTarget = 1;
          p.run = 0;
          const out = UNDER_LINTEL / Math.max(0.5, Math.hypot(to.x - from.x, to.z - from.z));
          await tween(seconds, (t) => {
            const along = Math.min(1, t * 1.3);
            pos.lerpVectors(from, to, along);
            const down = THREE.MathUtils.clamp((along - out) / (1 - out), 0, 1);
            pos.y = from.y + rise * down * down;
            p.crouch = along < out ? 1 : 1 - 0.6 * easeInOut(down);
          });
          p.crouchTarget = crouch;
          p.run = 1;
          void tween(0.2, (t) => (p.crouch = Math.max(p.crouchTarget, 0.4 + Math.sin(t * Math.PI) * 0.4 - 0.4 * t)));
        } else if (kind === 'ladderUp' || kind === 'ladderDown') {
          await this.climbLadder(v, id, kind === 'ladderUp' ? v.place : step, kind === 'ladderUp' ? step : v.place, kind === 'ladderUp', from, to, gait);
        } else if (kind === 'drop') {
          // Drop off an edge: a short hop out, then the fall, longer the higher it is (off a roof one storey up, as it always was).
          await tween(seconds, (t) => {
            pos.lerpVectors(from, to, Math.min(1, t * 1.3));
            pos.y = from.y + rise * t * t + Math.sin(Math.min(1, t * 2) * Math.PI) * 0.35;
          });
          void tween(0.2, (t) => (p.crouch = Math.sin(t * Math.PI) * 0.8));
        } else {
          // Going by another there (walkPlan.ts): to one side of the tile's middle.
          const swerve = swerves?.[k];
          if (swerve) to.add(swerve);
          await tween(seconds, (t) => {
            pos.lerpVectors(from, to, t);
          });
        }
        // On its storey once there (docs/FASE6.md): the cutaway checks this.
        v.place = { ...step };
        onStep?.(pos);
      }
    } finally {
      v.animating = false;
      if (vaulting !== null) p.crouchTarget = vaulting;
      // Down to a stand, unless it has set off again meanwhile.
      void tween(0.18, (t) => {
        if (v.walks === walks) p.run = 1 - t;
      });
    }
  }

  /**
   * Where a walk along `path` from `start` goes by another (`passes`, by path tile): SWERVE to the right of
   * its way through that tile, if the steps in and out of it are plain ones (two going opposite ways pass on
   * opposite sides); never on its last tile, where it stands.
   */
  private swerves(start: Place, path: readonly Place[], passes: readonly boolean[], gait: Gait): (THREE.Vector3 | null)[] {
    return path.map((tile, k) => {
      const next = path[k + 1];
      if (!passes[k] || !next) return null;
      const prev = k ? path[k - 1]! : start;
      if (this.stepOf(prev, tile, gait).kind !== 'flat' || this.stepOf(tile, next, gait).kind !== 'flat') return null;
      const dx = next.x - prev.x;
      const dz = next.y - prev.y;
      const length = Math.hypot(dx, dz);
      return length ? new THREE.Vector3((-dz / length) * SWERVE, 0, (dx / length) * SWERVE) : null;
    });
  }

  /**
   * Up a ladder from tile `low` to `high` (`up`), or down it: walked to it, climbed facing the rungs with
   * the body CLIMB_GAP behind them all the way (a building's ladder upright, the siege tower's leant on
   * its deck: TOWER_LADDER), and off at the top onto the floor, or at the foot to the ladder's tile.
   */
  private async climbLadder(v: UnitView, id: string, low: Place, high: Place, up: boolean, from: THREE.Vector3, to: THREE.Vector3, gait: Gait): Promise<void> {
    const p = v.pose;
    const pos = v.rig.root.position;
    const base = tileToWorld(low);
    const dir = new THREE.Vector3(high.x - low.x, 0, high.y - low.y).normalize();
    const height = Math.abs(to.y - from.y);
    const rungs = this.rungsOf(low);
    /** Where the climber's middle is with its feet `h` over the ladder's foot. */
    const at = (h: number, out = new THREE.Vector3()) => out.copy(base).addScaledVector(dir, rungs(h + CLIMB_CHEST) - CLIMB_GAP).setY(base.y + h);
    const foot = at(0);
    const top = at(height);
    const climb = (a: number, b: number) =>
      tween(LADDER_SECONDS * (height / STOREY), (t) => {
        at(a + (b - a) * t, pos);
      });
    if (up) {
      await tween(gait.seconds * 0.4, (t) => pos.lerpVectors(from, foot, t));
      this.face(id, foot.clone().add(dir));
      p.run = 0;
      p.climb = 1;
      await climb(0, height);
      p.climb = 0;
      p.run = 1;
      this.face(id, to);
      await tween(gait.seconds * 0.6, (t) => pos.lerpVectors(top, to, t));
      return;
    }
    // Down: to the top of it, turned round to face the rungs, down them, and off at the foot.
    await tween(gait.seconds * 0.6, (t) => pos.lerpVectors(from, top, t));
    this.face(id, top.clone().add(dir));
    p.run = 0;
    p.climb = 1;
    await climb(height, 0);
    p.climb = 0;
    p.run = 1;
    this.face(id, to);
    await tween(gait.seconds * 0.4, (t) => pos.lerpVectors(foot, to, t));
  }

  /** How far the rungs of the ladder on tile `p` stand from its middle, towards where it climbs, `h` tiles up its foot. */
  private rungsOf(p: Place): (h: number) => number {
    const s = this.shown;
    // The siege tower's (basePieces.ts): leant from its foot onto the deck's edge.
    if (s?.base && !p.z) {
      const map = pieceMap(s);
      const k = map?.at[p.y * s.width + p.x] ?? -1;
      if (k >= 0 && map!.spots[k]!.piece.piece === 'tower') return (h) => TOWER_LADDER.foot + TOWER_LADDER.lean * Math.min(h, TOWER_LADDER.top);
    }
    return () => LADDER_RUNGS;
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

  /**
   * What a sabotage's objective is in this battle (its alarm; sfx.ts picks its
   * sound the same way) and where a car's nose points: at the tile the map
   * blocks for it (sirenCar), else along the first tile beside it one could
   * stand on; how long the car is and how far to one side its middle lies, by
   * the tiles it takes (docs/MAPAS.md, phase 1: the nose's and its parts).
   */
  private sirenOf(u: Unit): Siren {
    const s = this.shown!;
    const alarm = s.mission.alarm ?? ALARM_BY_MAP[s.biome];
    const at = ([dx, dy]: [number, number]) => ({ x: u.pos.x + dx, y: u.pos.y + dy, z: 0 });
    const inside = (p: Place) => p.x >= 0 && p.y >= 0 && p.x < s.width && p.y < s.height;
    const car = SIDES.find((d) => inside(at(d)) && decorOf(s.decor?.[at(d).y * s.width + at(d).x])?.kind === 'sirenCar');
    const way = car ?? SIDES.find((d) => inside(at(d)) && canStandOn(kindAt(s, at(d)))) ?? SIDES[0]!;
    const siren: Siren = { alarm, heading: Math.atan2(way[0], way[1]) };
    const nose = car && at(car);
    const tiles = nose && s.decor ? (objectAt(s.decor, s.width, nose.y * s.width + nose.x)?.tiles ?? []) : [];
    if (tiles.length > 1) {
      // Along the way it points, and across it to its right (the rig's +x).
      const off = [...tiles.map((i) => ({ x: (i % s.width) - u.pos.x, y: Math.floor(i / s.width) - u.pos.y })), { x: 0, y: 0 }];
      siren.length = Math.max(...off.map((o) => o.x * way[0] + o.y * way[1])) + 1;
      siren.side = off.reduce((a, o) => a + o.x * way[1] - o.y * way[0], 0) / off.length;
    }
    return siren;
  }

  /** The flight of stairs place `p` is a tile of (a building's: engine flights.ts), in the state last shown; null if none. */
  private flightOf(p: Place): FlightView | null {
    const s = this.shown;
    if (!s || p.x < 0 || p.y < 0 || p.x >= s.width || p.y >= s.height) return null;
    const on = flightAt(s, { x: p.x, y: p.y, z: p.z ?? 0 });
    return on && { d: DIRS4[on.flight.up]!, n: on.flight.tiles.length, low: { ...on.flight.tiles[0]!, z: on.flight.z }, step: on.step };
  }

  /**
   * How high the feet are over the floor of a flight of `n` tiles, `a` tiles up it from the back edge of its lowest
   * tile: half a riser over the treads' front edges, from its foot up to its landing's floor.
   */
  private static flightHeight(a: number, n: number): number {
    const steps = n === 1 ? STAIR_STEPS : FLIGHT_STEPS * n;
    return THREE.MathUtils.clamp((a / n) * STOREY + STOREY / (2 * steps), 0, STOREY);
  }

  /** How high over its tile's floor a unit on place `p` stands: on the treads of a flight of stairs, else 0 (the path's line rides them too). */
  stairLift(p: Place): number {
    const f = this.flightOf(p);
    return f ? UnitViews.flightHeight(f.step + 0.5, f.n) : 0;
  }

  /** Where a unit on place `p` stands: its tile's floor, or on the treads of a building's stairs over its tile's middle. */
  private standAt(p: Place): THREE.Vector3 {
    const at = tileToWorld(p);
    const f = this.flightOf(p);
    if (f) at.y += UnitViews.flightHeight(f.step + 0.5, f.n);
    return at;
  }

  /**
   * The way of a step onto, along or off a building's stairs (from `from` to `to`, one of them on the stairs), as
   * points to walk through, each at the height of the treads under it. A flight of several tiles: straight up or
   * down its line, from its foot or its landing (the rules allow no other way on or off). A flight of one (of
   * before): straight up or down it from its foot or its landing; from beside it or the corner behind, round to the
   * foot first (and off the same way), never across a banister. Where the corner is no floor to pass by, straight
   * (the old way).
   */
  private stairWay(from: Place, to: Place): THREE.Vector3[] {
    const onStairs = this.flightOf(to) ? to : from;
    const other = onStairs === to ? from : to;
    const f = this.flightOf(onStairs)!;
    const d = f.d;
    const floor = groundAt({ x: f.low.x, y: f.low.y, z: f.low.z ?? 0 });
    if (f.n > 1) {
      /** How far up the flight a place's middle is, from the back edge of its lowest tile. */
      const up = (p: Place) => (p.x - f.low.x) * d.x + (p.y - f.low.y) * d.y + 0.5;
      const [a0, a1] = [up(from), up(to)];
      const [p0, p1] = [tileToWorld(from), tileToWorld(to)];
      // Every tenth of a tile, so that the feet follow the treads; the foot's floor and the landing's as they are.
      const n = Math.max(1, Math.ceil(Math.abs(a1 - a0) / 0.1));
      const out = Array.from({ length: n + 1 }, (_, i) => new THREE.Vector3().lerpVectors(p0, p1, i / n).setY(floor + UnitViews.flightHeight(a0 + ((a1 - a0) * i) / n, f.n)));
      out[0]!.y = this.standAt(from).y;
      out[n]!.y = this.standAt(to).y;
      return out;
    }
    const side = { x: -d.y, y: d.x };
    const mid = tileToWorld(onStairs);
    const rel = { x: other.x - onStairs.x, y: other.y - onStairs.y };
    const u = rel.x * d.x + rel.y * d.y;
    const v = rel.x * side.x + rel.y * side.y;
    const sameFloor = (other.z ?? 0) === (onStairs.z ?? 0);
    // From the floor in front of the flight's high end (the rules allow it): straight on, as it was.
    const underTop = sameFloor && u > 0;
    /** A point `a` along the climb and `b` across from the stairs' middle, at the height of what is under it. */
    const point = (a: number, b: number) => {
      const p = new THREE.Vector3(mid.x + d.x * a + side.x * b, floor, mid.z + d.y * a + side.y * b);
      if (underTop) p.y = floor + UnitViews.flightHeight(0.5, 1) * THREE.MathUtils.clamp(1 - a, 0, 1);
      else if (Math.abs(b) <= 0.5 + 1e-6 && a >= -0.6) p.y = floor + UnitViews.flightHeight(a + 0.5, 1);
      return p;
    };
    const way: [number, number][] = [[u, v]];
    // From beside the flight or the corner behind it: round by its foot.
    if (v !== 0 && sameFloor && u <= 0) {
      const sv = Math.sign(v);
      const corner = { x: onStairs.x - d.x + side.x * sv, y: onStairs.y - d.y + side.y * sv, z: onStairs.z ?? 0 };
      const s = this.shown;
      const cornerFree = !!s && (u === -1 || (canStandOn(kindAt(s, corner)) && groundAt(corner) === floor && !this.flightOf(corner)));
      const footKind = s ? kindAt(s, { x: onStairs.x - d.x, y: onStairs.y - d.y, z: onStairs.z ?? 0 }) : null;
      if (cornerFree && canStandOn(footKind)) {
        if (u === 0) way.push([-STAIR_FOOT, STAIR_FOOT * sv]);
        way.push([-STAIR_FOOT, 0]);
      }
    }
    way.push([0, 0]);
    if (onStairs === from) way.reverse();
    // Every tenth of a tile, so that the feet follow the treads.
    const out: THREE.Vector3[] = [point(way[0]![0], way[0]![1])];
    for (let k = 1; k < way.length; k++) {
      const [a0, b0] = way[k - 1]!;
      const [a1, b1] = way[k]!;
      const n = Math.max(1, Math.ceil(Math.hypot(a1 - a0, b1 - b0) / 0.1));
      for (let i = 1; i <= n; i++) out.push(point(a0 + ((a1 - a0) * i) / n, b0 + ((b1 - b0) * i) / n));
    }
    // The landing (a storey up) and any floor off the flight: the points there on that floor.
    const end = onStairs === from ? out[out.length - 1]! : out[0]!;
    end.y = this.standAt(other).y;
    return out;
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

  /**
   * Up to aim at `target`; `sidearm`, the pistol carried beside a main gun (firesSidearm), drawn from its
   * holster first. The aim's number, for lowering this one only (lowerWeapon).
   */
  async raiseWeapon(id: string, target: THREE.Vector3, sidearm = false): Promise<number> {
    const v = this.views.get(id);
    if (!v) return 0;
    const aim = ++v.aims;
    this.face(id, target);
    if (isFigure(v.rig)) v.rig.draw?.(sidearm ? 'sidearm' : 'weapon');
    this.struck(target, v.rig.root.position, false);
    // How close it is: at point blank the figure pulls a long gun back (syntyFigures.ts holdWeapon).
    v.pose.close = Math.hypot(target.x - v.rig.root.position.x, target.z - v.rig.root.position.z);
    await tween(0.28, (t) => (v.pose.aimFx = easeInOut(t)));
    return aim;
  }

  /**
   * Down from aiming; a pistol drawn from its holster goes back once lowered. `aim` (raiseWeapon's), only
   * that one: lowered a while after (game.ts), not one taken since, nor its step back at point blank.
   */
  lowerWeapon(id: string, aim?: number): void {
    const v = this.views.get(id);
    if (!v || (aim !== undefined && aim !== v.aims)) return;
    const own = v.aims;
    void tween(0.35, (t) => {
      if (v.aims === own) v.pose.aimFx = 1 - easeInOut(t);
    }).then(() => {
      if (v.aims !== own) return;
      v.pose.close = 0;
      if (isFigure(v.rig)) v.rig.draw?.('weapon');
    });
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

  /**
   * Whether a body could take place `p` (stepping out to shoot there, a fall across it): a floor at the
   * height of `level` (none, or a lower one, is an edge, a hole or a stairwell), not a stairs' flight, a
   * ladder, a closed door or a window, nothing bulky standing on it, nobody alive on it but `except`, and
   * with `bodies`, no body lying there either.
   */
  clearFor(p: Place, except: string | null, bodies = false, level: Place = p): boolean {
    const s = this.shown;
    if (!s || p.x < 0 || p.y < 0 || p.x >= s.width || p.y >= s.height) return false;
    const z = p.z ?? 0;
    const kind = kindAt(s, { x: p.x, y: p.y, z });
    if (!canStandOn(kind) || kind === 'door' || kind === 'ladder') return false;
    const i = p.y * s.width + p.x;
    if (groundAt({ x: p.x, y: p.y, z }) !== groundAt({ x: level.x, y: level.y, z: level.z ?? 0 })) return false;
    const look = decorOf((z ? s.levels?.[z - 1]?.decor : s.decor)?.[i]);
    if (look && BULKY.has(look.kind)) return false;
    const here = (q: Place) => q.x === p.x && q.y === p.y && (q.z ?? 0) === z;
    for (const u of s.units) if (u.alive && u.id !== except && !u.evacuated && here(u.pos)) return false;
    if (bodies) for (const o of this.views.values()) if (o.dead && o.id !== except && o.rig.root.visible && here(o.place)) return false;
    return true;
  }

  /**
   * The blow `v` falls from, in its own frame (Blow), and where it has room to fall: not into a wall or any
   * cover, nor onto anyone standing or lying there (its killer's tile too), furniture, the stairs or off an
   * edge (a roof's, a platform's, a stairwell).
   */
  private blowAt(v: UnitView, from: THREE.Vector3 | null, head: boolean, melee: boolean): Blow {
    const way = from ? angleTo(v.rig.root.position, from) : null;
    const s = this.shown;
    const at = { x: v.place.x, y: v.place.y, z: v.place.z ?? 0 };
    const yaw = v.pose.yaw;
    const checked = new Map<number, boolean>();
    // Every tile on the way, a quarter of a tile at a time out to where the head comes down, the shoulders' width across.
    const room = s && ((falls: number, reach: number) => {
      const dx = Math.sin(yaw + falls);
      const dy = Math.cos(yaw + falls);
      for (let d = 0.25; d < reach + 0.25; d += 0.25) {
        for (const side of [-0.2, 0, 0.2]) {
          const along = Math.min(d, reach);
          const tile = { x: Math.floor(at.x + 0.5 + dx * along + dy * side), y: Math.floor(at.y + 0.5 + dy * along - dx * side), z: at.z };
          if (tile.x === at.x && tile.y === at.y) continue;
          const key = tile.y * s.width + tile.x;
          let ok = checked.get(key);
          if (ok === undefined) checked.set(key, (ok = coverAt(s, at, tile) === 0 && this.clearFor(tile, v.id, true, at)));
          if (!ok) return false;
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
    // Its aim gone, and with it a lowering still to come and the step back at point blank.
    v.aims++;
    v.pose.aimFx = 0;
    v.pose.close = 0;
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

  /**
   * A weapon taken in hand from the backpack: the one it held turned over
   * and down as a reload's, the left hand back to the pack, and the new one
   * comes up in its place (0.7 s).
   */
  private async equip(v: UnitView, u: Unit): Promise<void> {
    const rig = v.rig;
    if (!isFigure(rig) || !rig.arm) return;
    const arms = armamentOf(u.template, gearOf(u.weaponTier).weapon!, u.arms);
    let swapped = false;
    await tween(0.7, (t) => {
      v.pose.spin = easeInOut(t);
      if (t >= 0.5 && !swapped) {
        swapped = true;
        rig.arm!(arms);
      }
    });
    v.pose.spin = 0;
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

  /**
   * A stone tossed at `to` (docs/FASE10.md §4): turned to it, the free arm
   * back and swung through underhand. Resolves as it leaves the hand, with
   * where the hand is then (for fx.stone).
   */
  async tossStone(id: string, to: THREE.Vector3): Promise<THREE.Vector3> {
    const v = this.views.get(id);
    if (!v) return to.clone();
    this.face(id, to);
    await wait(0.12);
    await tween(0.24, (t) => (v.pose.toss = 0.6 * easeInOut(t)));
    v.rig.root.updateMatrixWorld(true);
    const hand = v.rig.root.getObjectByName('Hand_L')?.getWorldPosition(new THREE.Vector3()) ?? this.chestPos(id);
    void tween(0.22, (t) => (v.pose.toss = 0.6 + 0.4 * t)).then(() => (v.pose.toss = 0));
    return hand;
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

  /**
   * Melee strike: a quick lunge towards the target and back, stopping LUNGE_SHORT from its middle (one
   * beside: 0.28 of a tile, it went 0.45 and stood 0.55 from it), LUNGE at most (one on the diagonal).
   */
  async lunge(id: string, target: THREE.Vector3): Promise<void> {
    const v = this.views.get(id);
    if (!v) return;
    this.face(id, target);
    const home = v.rig.root.position.clone();
    this.struck(target, home, true);
    const gap = Math.hypot(target.x - home.x, target.z - home.z);
    const toward = target.clone().setY(home.y).sub(home).setLength(THREE.MathUtils.clamp(gap - LUNGE_SHORT, 0, LUNGE));
    v.animating = true;
    // The knife or the machete from the belt (a soldier's Tajo); whoever has none strikes with what it holds.
    const rig = isFigure(v.rig) ? v.rig : null;
    rig?.draw?.('belt');
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
    }).then(() => {
      v.animating = false;
      rig?.draw?.('weapon');
    });
  }

  /**
   * A blow on a siege's door or barricade at `tile` (the 'smashed' event,
   * docs/FASE12.md §10): the dead one lunges at it with its arms. Resolves as
   * the blow lands, 0.34 s on; it steps back in 0.35 s more on its own.
   */
  async smash(id: string, tile: Place): Promise<void> {
    await this.lunge(id, this.standAt(tile));
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
   * the state has not got it yet, and it comes on foot along `way` (from the
   * map's edge through each tile to its own: arrivals.ts), each leg taking
   * `seconds[k]` after standing `waits[k]` (walkPlan.ts), to one side of the
   * points where it goes by another (`passes`, as a walk's swerves), the dead
   * staggering and the living running; the event that follows puts it in the
   * state where it already stands.
   */
  async arrive(u: Unit, way: THREE.Vector3[], seconds: readonly number[], waits: readonly number[] = [], passes: readonly boolean[] = []): Promise<void> {
    const v = this.views.get(u.id) ?? this.create(u);
    const pos = v.rig.root.position;
    v.animating = true;
    v.flag.visible = false;
    v.rig.root.visible = true;
    pos.copy(way[0]!);
    if (way[1]) this.face(u.id, way[1]);
    v.pose.yaw = v.pose.yawTarget;
    v.pose.run = 1;
    // Its feet at the pace it walks in at (a tile in its straight legs' time).
    const tile = seconds.find((t, k) => k > 0 && t > 0) ?? seconds[0] ?? 0.4;
    if (isFigure(v.rig) && tile > 0) {
      const gait = { motion: gaitsOf(v.template).one.motion, seconds: tile };
      v.rig.setGait?.(gait.motion, clipSpeed(v.template, gait));
    }
    // SWERVE to the right of its way through a point it goes by another on, on the flat; never where it stops.
    const points = way.map((p, k) => {
      const prev = way[k - 1];
      const next = way[k + 1];
      if (!passes[k] || !prev || !next || Math.abs(prev.y - p.y) > 0.05 || Math.abs(next.y - p.y) > 0.05) return p;
      const dx = next.x - prev.x;
      const dz = next.z - prev.z;
      const length = Math.hypot(dx, dz);
      return length ? p.clone().add(new THREE.Vector3((-dz / length) * SWERVE, 0, (dx / length) * SWERVE)) : p;
    });
    for (let k = 1; k < points.length; k++) {
      const hold = waits[k - 1] ?? 0;
      if (hold > 0.02) {
        await tween(hold, (t) => (v.pose.run = 1 - Math.min(1, (t * hold) / 0.15)));
        void tween(0.12, (t) => (v.pose.run = Math.max(v.pose.run, t)));
      }
      const a = points[k - 1]!;
      const b = points[k]!;
      this.face(u.id, b);
      await tween(seconds[k - 1] ?? 0.3, (t) => pos.lerpVectors(a, b, t));
    }
    v.place = { ...u.pos };
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
