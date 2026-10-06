import * as THREE from 'three';
import {
  ABILITIES,
  abilitiesOf,
  abilityBlocker,
  abilityTargets,
  apForCost,
  applyEvent,
  attackWeapon,
  blastTiles,
  buildPath,
  canTargetTile,
  cloneState,
  computeReach,
  costPerAp,
  coverAgainst,
  coverSides,
  destinations,
  getUnit,
  hackChance,
  hasAnyCover,
  isAttack,
  maxMoveCost,
  MEDKIT_HEAL,
  meleeTargets,
  MISSION_NAMES,
  PLAYER_COLORS,
  previewShot,
  rangedTargets,
  sameTile,
  sightOrigin,
  STORY,
  STORY_RADIO,
  teamVisibility,
  tileAt,
  TEMPLATES,
  unitAt,
  visibleEnemies,
  type AbilityId,
  type AttackAbility,
  type Command,
  type EndReason,
  type GameEvent,
  type GameState,
  type MissionKind,
  type PingKind,
  type PlayerInfo,
  type Reach,
  type ShotResult,
  type SkirmishMap,
  type Slot,
  type Unit,
  type Vec2,
} from '@bastion/engine';
import type { Net, NetStatus } from '../net';
import { clear } from '../ui/dom';
import { Hud, type AbilityView, type MissionView, type TargetView } from '../ui/hud';
import { CameraRig, isTyping } from './camera';
import { setTerrain, STOREY, tileToWorld, worldToTile } from './coords';
import { Fx } from './fx';
import { MapView } from './mapView';
import { Overlays, type MoveTile } from './overlays';
import { Renderer } from './renderer';
import { Sfx } from './sfx';
import { setTimeScale, wait } from './tween';
import { UnitViews } from './unitViews';

type Mode = { kind: 'move' } | { kind: 'target'; ability: AbilityId } | { kind: 'tile'; ability: AbilityId };

interface QueuedEvent {
  e: GameEvent;
  by: Slot | null;
  /** Camera epoch when the batch arrived; own-action cinematics stop once the player takes the camera. */
  epoch: number;
}

/** A selectable target for the current ability. */
interface TargetOption {
  unit: Unit;
  /** Melee: tile the attacker will strike from, and the path to it. */
  tile?: Vec2;
  path?: Vec2[];
}

type ShotEvent = Extract<GameEvent, { t: 'shot' }>;

/** Pause after the last event of one of our own actions before handing control back. */
const HOLD_AFTER: Partial<Record<GameEvent['t'], number>> = {
  damaged: 0.75,
  died: 0.6,
  shot: 0.6,
  stunned: 0.6,
  tileChanged: 0.6,
  explosive: 0.6,
  hacked: 0.8,
  vipRescued: 0.5,
  zapped: 0.6,
  suppressed: 0.6,
  cleansed: 0.4,
  refund: 0.4,
  abilityUsed: 0.3,
  healed: 0.5,
  aided: 0.5,
  evacuated: 0.4,
  moved: 0.12,
};

/** What attack variants do besides the odds shown in the panel. */
const ATTACK_NOTES: Partial<Record<AttackAbility, string>> = {
  rapidFire: 'Dos disparos seguidos',
  chainShot: 'Si acierta, dispara otra vez',
  aimedShot: '+50 % de daño',
  rupture: '+3 de daño; el objetivo recibirá +3 de todos los ataques',
  lightningHands: 'No gasta acciones',
};

/** Abilities whose name pops up over the unit when used. */
const SHOUTED: ReadonlySet<AbilityId> = new Set<AbilityId>(['runAndGun', 'rapidFire', 'chainShot', 'rupture', 'aimedShot', 'lightningHands', 'faceoff', 'restoration']);

/** Order used by the Space key: the first usable attack. */
const PRIMARY_ATTACKS: AbilityId[] = ['shoot', 'pistol', 'slash'];

/** "las cintas" → "Las cintas". */
/** Players with a soldier that still has actions. */
function busySlots(s: GameState): Set<Slot> {
  const busy = new Set<Slot>();
  for (const u of s.units) if (u.alive && u.team === 'xcom' && u.ap > 0 && u.owner !== null) busy.add(u.owner);
  return busy;
}

function capitalize(text: string): string {
  return text[0]!.toUpperCase() + text.slice(1);
}

function unitOf(e: GameEvent): string | undefined {
  return 'unit' in e ? e.unit : undefined;
}

/** Where a missed round ends up: past the target, off to one side. */
function missPoint(from: THREE.Vector3, chest: THREE.Vector3, i: number, spread = 1): THREE.Vector3 {
  const dir = chest.clone().sub(from).setY(0).normalize();
  const side = new THREE.Vector3(-dir.z, 0, dir.x);
  return chest
    .clone()
    .addScaledVector(side, (i % 2 ? 1 : -1) * (0.45 + Math.random() * 0.45) * spread)
    .addScaledVector(dir, 2 + Math.random() * 2)
    .setY(0.25 + Math.random() * 1.3);
}

function jitter(amount: number): THREE.Vector3 {
  return new THREE.Vector3((Math.random() - 0.5) * amount, (Math.random() - 0.5) * amount, (Math.random() - 0.5) * amount);
}

/**
 * Client-side mission controller.
 *
 * Two copies of the game state are kept:
 *  - `state`: the latest authoritative state (all received events applied).
 *    Input, previews and the HUD read from it, so players can plan while
 *    animations are still playing.
 *  - `view`: what the 3D scene currently shows. Events are applied to it one by
 *    one as their animations finish.
 */
export class Game {
  private readonly root: HTMLDivElement;
  private readonly renderer: Renderer;
  private readonly rig: CameraRig;
  private readonly mapView = new MapView();
  private readonly units = new UnitViews();
  private readonly fx = new Fx();
  private readonly sfx = new Sfx();
  private overlays!: Overlays;
  private readonly hud: Hud;

  private state!: GameState;
  private view!: GameState;
  private queue: QueuedEvent[] = [];
  private current: QueuedEvent | null = null;
  private playing = false;
  /** Events from our own commands still waiting to be animated. */
  private myPending = 0;
  private visibleAliens = new Set<string>();
  private lastShot: { target: string; result: ShotResult } | null = null;
  /** Last ability activated, so its follow-up events can be staged (e.g. Restoration's heals). */
  private lastUsed: { unit: string; ability: AbilityId } | null = null;

  private selected: string | null = null;
  private mode: Mode = { kind: 'move' };
  private targetId: string | null = null;
  private hoverTile: Vec2 | null = null;
  private reachCache: { key: string; reach: Reach; tiles: MoveTile[] } | null = null;
  private awaitingReply = false;
  private lastPresence = '';
  private presenceTimer = 0;
  private lastPing = 0;
  /** End turn pressed once with soldiers still able to act: the next press ends it. */
  private endArmed = false;
  private endArmTimer = 0;
  /** `unit|x,y` of a move that would break concealment, clicked once: the next click on it moves. */
  private concealWarn: string | null = null;

  private readonly raycaster = new THREE.Raycaster();
  private drag: { x: number; y: number; moved: boolean } | null = null;
  private readonly disposers: (() => void)[] = [];

  constructor(
    container: HTMLElement,
    private readonly net: Net,
    private readonly mySlot: Slot,
    private players: PlayerInfo[],
    snapshot: GameState,
  ) {
    this.root = document.createElement('div');
    this.root.className = 'game';
    container.append(this.root);
    this.renderer = new Renderer(this.root);
    this.rig = new CameraRig(this.renderer.camera);
    this.renderer.scene.add(this.mapView.group, this.units.group, this.fx.group);
    this.hud = new Hud(this.root, {
      onAbility: (id) => this.useAbility(id),
      onEndTurn: () => this.toggleReady(),
      onSelectUnit: (id) => this.select(id, true),
      onConfirmTarget: () => this.confirmTarget(),
      onCancel: () => this.cancelMode(),
      onCycleTarget: (dir) => this.cycleTarget(dir),
      onNewMission: (kind?: MissionKind, map?: SkirmishMap) => this.net.send({ t: 'newMission', mission: kind, map }),
      onReturnToBase: () => this.net.send({ t: 'returnToBase' }),
      onBackToMenu: () => this.net.send({ t: 'backToMenu' }),
      onAbandon: () => this.net.send({ t: 'abandon' }),
      onPassCommand: () => this.passCommand(),
      onToggleSound: () => !this.sfx.toggleMute(),
      soundOn: () => !this.sfx.muted,
    });

    this.disposers.push(this.renderer.onFrame((dt, time) => this.frame(dt, time)));
    this.bindInput();
    this.loadSnapshot(snapshot);
  }

  /** Advances camera, poses and particles; called by the render loop. */
  frame(dt: number, time: number): void {
    this.rig.update(dt);
    this.units.tick(dt, time);
    this.fx.tick(dt);
    this.overlays?.tick(time);
  }

  // ================================================================ network

  loadSnapshot(s: GameState): void {
    this.state = cloneState(s);
    this.view = cloneState(s);
    this.queue = [];
    this.myPending = 0;
    this.reachCache = null;
    this.awaitingReply = false;

    if (!this.overlays || this.overlays.group.parent === null) {
      this.overlays = new Overlays(s.width * s.height);
      this.renderer.scene.add(this.overlays.group);
    }
    this.renderer.fitMap(s.width, s.height);
    this.rig.setBounds(s.width, s.height);
    setTerrain(this.view);
    this.mapView.build(this.view);
    this.units.build(this.view);
    this.selected = null;
    this.mode = { kind: 'move' };
    this.showOutcome();
    this.refreshView();
    this.autoSelect();
    const focus = this.selected ? getUnit(this.state, this.selected) : this.state.units.find((u) => u.team === 'xcom');
    if (focus) this.rig.focus(tileToWorld(focus.pos), true);
    this.refreshInteractive();
  }

  /** The arrival briefing, XCOM-style: operation, area and objective. */
  intro(): void {
    const s = this.state;
    const mission = this.missionView();
    const chapter = s.campaignMission?.story;
    this.hud.intro(
      s.campaignMission?.kicker ?? (chapter ? `EXPEDIENTE CORO · HOJA ${chapter}` : s.campaignMission ? 'OPERACIÓN EN CURSO' : 'ESCARAMUZA'),
      s.campaignMission?.name ?? MISSION_NAMES[s.mission.kind],
      [s.mapName, mission.objective, mission.turnsLeft !== null ? `${mission.turnsLeft} turnos` : ''].filter(Boolean),
    );
    this.storyRadio('start');
  }

  onEvents(events: GameEvent[], seq: number, by: Slot | null): void {
    for (const e of events) applyEvent(this.state, e);
    if (this.state.seq !== seq) {
      console.warn(`[sync] Desfase de eventos (${this.state.seq} ≠ ${seq}); pidiendo estado completo.`);
      this.net.send({ t: 'sync' });
      return;
    }
    if (by === this.mySlot) {
      this.awaitingReply = false;
      this.myPending += events.length;
    }
    this.reachCache = null;
    const epoch = this.rig.epoch;
    this.queue.push(...events.map((e) => ({ e, by, epoch })));
    this.refreshInteractive();
    void this.pump();
  }

  onRejected(message: string): void {
    this.awaitingReply = false;
    this.hud.toast(message);
  }

  onPresence(slot: Slot, unit: string | null, tile: Vec2 | null): void {
    if (slot === this.mySlot) return;
    const color = PLAYER_COLORS[slot];
    this.units.setPartnerSelected(unit, color);
    this.overlays.setPartnerHover(tile, color);
  }

  onPing(slot: Slot, tile: Vec2, kind: PingKind): void {
    this.fx.ping(tileToWorld(tile), PLAYER_COLORS[slot], kind === 'enemy');
    this.sfx.ui();
    if (slot !== this.mySlot) {
      const who = this.players.find((p) => p.slot === slot)?.name ?? 'Tu compañero';
      this.hud.log(`${who} marca ${kind === 'enemy' ? 'un enemigo' : 'una posición'}`, PLAYER_COLORS[slot]);
    }
  }

  setPlayers(players: PlayerInfo[]): void {
    this.players = players;
    this.refreshHud();
  }

  setConnection(status: NetStatus): void {
    this.hud.setConnection(status);
  }

  toast(message: string): void {
    this.hud.toast(message);
  }

  destroy(): void {
    window.clearTimeout(this.endArmTimer);
    for (const d of this.disposers) d();
    this.renderer.dispose();
    clear(this.root);
    this.root.remove();
  }

  private send(cmd: Command): void {
    if (this.awaitingReply && cmd.type !== 'endTurn') return;
    if (cmd.type !== 'endTurn') this.disarmEndTurn();
    this.awaitingReply = cmd.type !== 'endTurn';
    this.net.send({ t: 'cmd', cmd });
    window.setTimeout(() => (this.awaitingReply = false), 1500);
  }

  private ping(tile: Vec2): void {
    const now = performance.now();
    if (now - this.lastPing < 400) return;
    this.lastPing = now;
    const occupant = unitAt(this.state, tile);
    const kind: PingKind = occupant?.team === 'alien' && this.visibleAliens.has(occupant.id) ? 'enemy' : 'point';
    this.net.send({ t: 'ping', tile, kind });
  }

  // ============================================================== animation

  private async pump(): Promise<void> {
    if (this.playing) return;
    this.playing = true;
    while (this.queue.length) {
      setTimeScale(this.queue.length > 24 ? 2.5 : this.queue.length > 12 ? 1.6 : 1);
      const item = this.queue.shift()!;
      this.current = item;
      try {
        await this.play(item);
      } catch (err) {
        console.error('[anim]', err);
      }
      this.current = null;
      const secured = this.view.mission.objectiveDone;
      applyEvent(this.view, item.e);
      if (!secured && this.view.mission.objectiveDone) this.storyRadio('objective');
      if (item.e.t === 'tileChanged') this.mapView.rebuild(this.view);
      this.refreshView();
      this.refreshStatus();
      if (item.by === this.mySlot && --this.myPending === 0) {
        // Let the player see the outcome before the selection moves on.
        await wait(HOLD_AFTER[item.e.t] ?? 0.2);
        this.rig.restoreZoom();
        this.refreshInteractive();
      }
    }
    setTimeScale(1);
    this.playing = false;
    this.refreshInteractive();
  }

  /** Whether `id` still has events waiting to be animated. */
  private isBusy(id: string | null): boolean {
    if (!id) return false;
    return (this.current !== null && unitOf(this.current.e) === id) || this.queue.some((q) => unitOf(q.e) === id);
  }

  private shownToSquad(u: Unit): boolean {
    return u.team === 'xcom' || this.visibleAliens.has(u.id);
  }

  /**
   * Animates one event against the `view` state (before the event is applied).
   * The camera follows our own actions and the alien turn, never the partner's,
   * and stops following as soon as the player takes the camera.
   */
  private async play(item: QueuedEvent): Promise<void> {
    const { e, by } = item;
    const v = this.view;
    const alienPhase = v.activeTeam === 'alien';
    const cinematic = by === this.mySlot || alienPhase;
    const epoch = alienPhase ? this.rig.epoch : item.epoch;
    const cam = () => cinematic && this.rig.epoch === epoch;
    const look = (p: THREE.Vector3) => {
      if (cam()) this.rig.focus(p);
    };

    switch (e.t) {
      case 'moved': {
        const u = getUnit(v, e.unit)!;
        const seen = this.shownToSquad(u) || e.path.some((p) => this.mapView.isVisible(p.x, p.y));
        if (!seen) return;
        this.units.forceVisible(u.id);
        look(this.units.worldPos(u.id));
        let steps = 0;
        await this.units.moveAlong(u.id, e.path, (p) => {
          look(p);
          if (steps++ % 2 === 0) this.sfx.step();
        });
        return;
      }
      case 'shot':
        return this.playAttack(e, cam, look);
      case 'damaged': {
        const target = getUnit(v, e.unit)!;
        const shot = this.lastShot?.target === e.unit ? this.lastShot.result : null;
        const crit = shot === 'crit';
        if (e.amount) {
          this.hud.log(`${target.name} recibe ${e.amount} de daño${crit ? ' (crítico)' : ''}`, target.team === 'alien' ? '#54d1ff' : '#ff6b5a');
        }
        if (!this.shownToSquad(target)) return;
        const chest = this.units.chestPos(target.id);
        if (e.amount) {
          const label = crit ? `CRÍTICO −${e.amount}` : shot === 'graze' ? `ROZADURA −${e.amount}` : `−${e.amount}`;
          this.fx.floatText(chest, label, crit ? 'crit' : 'damage');
        }
        if (e.mitigated) this.fx.floatText(chest.clone().setY(chest.y - 0.45), `BLINDAJE −${e.mitigated}`, 'info');
        if (e.shred) this.fx.floatText(chest.clone().setY(chest.y + 0.45), 'BLINDAJE DAÑADO', 'warn');
        if (crit && cam()) this.rig.shake(0.18, 0.3);
        await this.units.hitReact(target.id, crit ? 1.5 : e.amount ? 1 : 0.4);
        return;
      }
      case 'died': {
        const u = getUnit(v, e.unit)!;
        this.hud.log(u.team === 'xcom' ? `${u.name} ha caído en combate` : `${u.name} eliminado`, u.team === 'xcom' ? '#ff6b5a' : '#54d1ff');
        if (!this.shownToSquad(u)) return;
        await this.units.die(u.id);
        await wait(0.2);
        return;
      }
      case 'stunned': {
        const u = getUnit(v, e.unit)!;
        this.hud.log(`${u.name} queda aturdido`, '#6ad8ff');
        if (!this.shownToSquad(u)) return;
        this.fx.sparks(this.units.chestPos(u.id), 0x6ad8ff, 14);
        this.fx.floatText(this.units.chestPos(u.id), 'ATURDIDO', 'warn');
        await wait(0.5);
        return;
      }
      case 'overwatch':
      case 'hunker': {
        const u = getUnit(v, e.unit)!;
        if (!this.shownToSquad(u)) return;
        look(this.units.worldPos(u.id));
        const text = e.t === 'hunker' ? 'AGAZAPADO' : e.killZone ? 'ZONA LETAL' : 'VIGILANCIA';
        this.fx.floatText(this.units.chestPos(u.id), text, 'info');
        this.sfx.ui();
        await wait(0.45);
        return;
      }
      case 'abilityUsed': {
        this.lastUsed = { unit: e.unit, ability: e.ability };
        const u = getUnit(v, e.unit)!;
        if (!SHOUTED.has(e.ability) || !this.shownToSquad(u)) return;
        look(this.units.worldPos(u.id));
        this.fx.floatText(this.units.chestPos(u.id), ABILITIES[e.ability].name.toUpperCase(), 'alert');
        this.hud.log(`${u.name}: ${ABILITIES[e.ability].name}`);
        await wait(0.45);
        return;
      }
      case 'zapped': {
        const u = getUnit(v, e.unit)!;
        const target = getUnit(v, e.target)!;
        look(this.units.worldPos(u.id).lerp(this.units.worldPos(target.id), 0.5));
        const home = this.units.dronePos(u.id);
        const dest = this.units.chestPos(target.id).add(new THREE.Vector3(0, 0.7, 0));
        this.units.setDroneAway(u.id, true);
        this.sfx.ui();
        await this.fx.droneFlight(home, dest);
        this.fx.sparks(this.units.chestPos(target.id), 0x8ad8ff, 18);
        this.sfx.impact();
        this.hud.log(`${u.name}: protocolo de combate contra ${target.name}`, '#54d1ff');
        void this.fx.droneFlight(dest, this.units.dronePos(u.id)).then(() => this.units.setDroneAway(u.id, false));
        return;
      }
      case 'cleansed': {
        const target = getUnit(v, e.target)!;
        this.fx.heal(this.units.chestPos(target.id));
        this.fx.floatText(this.units.chestPos(target.id), 'RECUPERADO', 'heal');
        this.hud.log(`${target.name} se recupera`, '#6dff9a');
        await wait(0.3);
        return;
      }
      case 'refund': {
        const u = getUnit(v, e.unit)!;
        this.fx.floatText(this.units.chestPos(u.id), e.reason === 'implacable' ? 'IMPLACABLE · +1 ACCIÓN' : 'MUERTE DESDE ARRIBA · +1 ACCIÓN', 'heal');
        this.hud.log(`${u.name} recupera una acción`, '#6dff9a');
        await wait(0.4);
        return;
      }
      case 'suppressed': {
        const u = getUnit(v, e.unit)!;
        const target = getUnit(v, e.target)!;
        look(this.units.worldPos(u.id).lerp(this.units.worldPos(target.id), 0.5));
        const chest = this.units.chestPos(target.id);
        await this.units.raiseWeapon(u.id, chest);
        for (let i = 0; i < 8; i++) {
          const from = this.units.muzzlePos(u.id);
          this.units.recoil(u.id);
          this.fx.muzzleFlash(from, 0x9fe8ff);
          this.sfx.shot(false);
          const dest = missPoint(from, chest, i, 1.2);
          void this.fx.bullet(from, dest, 0x9fe8ff).then(() => this.fx.sparks(dest, 0xffd28a, 3));
          await wait(0.07);
        }
        this.fx.floatText(chest, 'SUPRIMIDO', 'warn');
        this.hud.log(`${u.name} suprime a ${target.name}`, '#ffc93a');
        void wait(0.8).then(() => this.units.lowerWeapon(u.id));
        return;
      }
      case 'suppressionEnded':
      case 'controlTick':
      case 'smokeDeployed':
        return;
      case 'disoriented': {
        const u = getUnit(v, e.unit)!;
        if (this.shownToSquad(u)) this.fx.floatText(this.units.chestPos(u.id), 'DESORIENTADO', 'crit');
        this.hud.log(`${u.name} queda desorientado`, '#d29aff');
        await wait(0.15);
        return;
      }
      case 'marked': {
        const target = getUnit(v, e.unit)!;
        if (this.shownToSquad(target)) this.fx.floatText(this.units.chestPos(target.id).add(new THREE.Vector3(0, 0.4, 0)), 'HOLO-OBJETIVO', 'info');
        return;
      }
      case 'ruptured': {
        const target = getUnit(v, e.unit)!;
        if (this.shownToSquad(target)) this.fx.floatText(this.units.chestPos(target.id).add(new THREE.Vector3(0, 0.4, 0)), 'RUPTURA', 'warn');
        return;
      }
      case 'psi': {
        const u = getUnit(v, e.unit)!;
        const target = getUnit(v, e.target)!;
        look(this.units.worldPos(u.id).lerp(this.units.worldPos(target.id), 0.5));
        this.units.forceVisible(u.id);
        this.units.face(u.id, this.units.worldPos(target.id));
        this.sfx.alert();
        await this.fx.psiBeam(this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.35, 0)), this.units.chestPos(target.id));
        const texts: Record<string, string> = { disoriented: 'DESORIENTADO', panicked: 'PÁNICO', controlled: 'CONTROL MENTAL' };
        const text = e.effect ? texts[e.effect]! : 'RESISTE';
        this.fx.floatText(this.units.chestPos(target.id), text, e.success ? 'crit' : 'info');
        this.hud.log(e.success ? `${target.name}: ${text.toLowerCase()} (${e.chance}%)` : `${target.name} resiste el ataque psiónico (${e.chance}%)`, e.success ? '#d29aff' : undefined);
        if (e.effect === 'controlled') this.hud.banner('¡CONTROL MENTAL!', 'alien');
        await wait(0.7);
        return;
      }
      case 'released': {
        const u = getUnit(v, e.unit)!;
        this.fx.sparks(this.units.chestPos(u.id), 0xd29aff, 14);
        this.fx.floatText(this.units.chestPos(u.id), 'LIBERADO', 'heal');
        this.hud.log(`${u.name} vuelve a ser dueño de su mente`, '#6dff9a');
        await wait(0.5);
        return;
      }
      case 'reanimated': {
        const u = getUnit(v, e.unit)!;
        const corpse = getUnit(v, e.corpse)!;
        const at = tileToWorld(corpse.pos, 0.5);
        look(at);
        this.units.forceVisible(u.id);
        await this.fx.psiBeam(this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.35, 0)), at);
        this.fx.evacBeam(tileToWorld(corpse.pos));
        this.fx.floatText(at, 'REANIMADO', 'crit');
        this.hud.log(`Un sectoide levanta el cuerpo de ${corpse.name}`, '#d29aff');
        await wait(0.6);
        return;
      }
      case 'reload': {
        const u = getUnit(v, e.unit)!;
        if (!this.shownToSquad(u)) return;
        look(this.units.worldPos(u.id));
        this.fx.floatText(this.units.chestPos(u.id), 'RECARGANDO', 'info');
        this.sfx.reload();
        await this.units.reload(u.id);
        return;
      }
      case 'explosive':
        return this.playExplosive(e, cam, look);
      case 'healed':
      case 'aided': {
        const u = getUnit(v, e.unit)!;
        const target = getUnit(v, e.target)!;
        if (e.t === 'healed' && this.lastUsed?.unit === u.id && this.lastUsed.ability === 'restoration') {
          // Restoration heals everyone at once: no drone trips.
          this.fx.heal(this.units.chestPos(target.id));
          this.fx.floatText(this.units.chestPos(target.id), `+${e.amount}`, 'heal');
          this.hud.log(`${target.name} recupera ${e.amount}`, '#6dff9a');
          await wait(0.2);
          return;
        }
        look(this.units.worldPos(u.id).lerp(this.units.worldPos(target.id), 0.5));
        const home = this.units.dronePos(u.id);
        const dest = this.units.chestPos(target.id).add(new THREE.Vector3(0, 0.9, 0));
        this.units.setDroneAway(u.id, true);
        this.sfx.ui();
        await this.fx.droneFlight(home, dest);
        if (e.t === 'healed') {
          this.fx.heal(this.units.chestPos(target.id));
          this.fx.floatText(this.units.chestPos(target.id), `+${e.amount}`, 'heal');
          this.hud.log(`${u.name} cura ${e.amount} a ${target.name}`, '#6dff9a');
        } else {
          this.fx.shield(this.units.chestPos(target.id));
          this.fx.floatText(this.units.chestPos(target.id), '+20 DEFENSA', 'info');
          this.hud.log(`${u.name} protege a ${target.name}`, '#54d1ff');
        }
        await wait(0.45);
        await this.fx.droneFlight(dest, this.units.dronePos(u.id));
        this.units.setDroneAway(u.id, false);
        return;
      }
      case 'evacuated': {
        const u = getUnit(v, e.unit)!;
        look(this.units.worldPos(u.id));
        this.hud.log(`${u.name} evacuado`, '#6dff9a');
        this.fx.evacBeam(this.units.worldPos(u.id));
        this.sfx.throwWhoosh();
        await this.units.evacuate(u.id);
        return;
      }
      case 'itemPicked': {
        const u = getUnit(v, e.unit)!;
        this.hud.log(`${u.name} recupera los datos`, '#6dff9a');
        this.fx.floatText(this.units.chestPos(u.id), 'DATOS RECUPERADOS', 'heal');
        this.sfx.alert();
        await wait(0.6);
        return;
      }
      case 'itemDropped':
        this.hud.log('¡Los datos han caído al suelo!', '#ffc93a');
        this.fx.floatText(tileToWorld(e.pos, 1), 'DATOS PERDIDOS', 'warn');
        await wait(0.4);
        return;
      case 'tileChanged': {
        const before = tileAt(v, e.pos.x, e.pos.y);
        if (e.kind === 'doorOpen' && before === 'door') {
          if (this.mapView.isVisible(e.pos.x, e.pos.y)) this.sfx.reload();
          return;
        }
        if (e.kind === 'windowBroken') {
          this.fx.debris(tileToWorld(e.pos, 1.3), 0x9fd8ff, 10);
          this.sfx.impact();
          return;
        }
        this.fx.debris(tileToWorld(e.pos, before === 'high' ? 1.1 : 0.45), before === 'low' ? 0x8f6c45 : 0x3f7480, 7);
        return;
      }
      case 'hacked': {
        const u = getUnit(v, e.unit)!;
        const terminal = v.mission.terminal;
        if (!terminal) return;
        const screen = tileToWorld(terminal, 1.3);
        look(this.units.worldPos(u.id).lerp(screen, 0.5));
        if (e.remote) {
          const home = this.units.dronePos(u.id);
          this.units.setDroneAway(u.id, true);
          await this.fx.droneFlight(home, screen.clone().add(new THREE.Vector3(0, 0.6, 0)));
        } else {
          this.units.face(u.id, screen);
          await this.units.raiseWeapon(u.id, screen);
        }
        this.sfx.ui();
        this.fx.sparks(screen, e.success ? 0x4dff9a : 0xff4a3a, 16);
        await wait(0.5);
        this.fx.floatText(screen, e.success ? 'HACKEO COMPLETADO' : 'HACKEO FALLIDO', e.success ? 'heal' : 'warn');
        this.hud.log(`${u.name} ${e.success ? 'hackea el terminal' : 'falla el hackeo'} (${e.chance}%)`, e.success ? '#6dff9a' : '#ffc93a');
        if (e.success) this.hud.banner('TERMINAL HACKEADO', 'xcom');
        else {
          this.sfx.alert();
          this.hud.banner('¡ALARMA!', 'alien');
        }
        if (e.remote) {
          await this.fx.droneFlight(screen.clone().add(new THREE.Vector3(0, 0.6, 0)), this.units.dronePos(u.id));
          this.units.setDroneAway(u.id, false);
        } else {
          this.units.lowerWeapon(u.id);
        }
        return;
      }
      case 'vipRescued': {
        const vip = getUnit(v, e.unit)!;
        look(this.units.worldPos(vip.id));
        this.fx.floatText(this.units.chestPos(vip.id), 'VIP RESCATADO', 'heal');
        this.hud.log(`${vip.name} se une a la escuadra`, '#6dff9a');
        this.sfx.alert();
        await wait(0.6);
        return;
      }
      case 'reinforcementsIncoming': {
        if (!e.r.pos) return;
        const at = tileToWorld(e.r.pos);
        look(at);
        this.hud.banner('REFUERZOS EN CAMINO', 'alien');
        this.hud.log('Bengala roja: un dirigible soltará cápsulas de impresión al final del próximo turno enemigo', '#ff6b5a');
        this.sfx.alert();
        await wait(cam() ? 0.9 : 0.3);
        return;
      }
      case 'spawned': {
        const spots = e.units.map((u) => tileToWorld(u.pos));
        const seen = e.units.some((u) => this.mapView.isVisible(u.pos.x, u.pos.y));
        this.hud.log(`Cápsulas de impresión: ${e.units.map((u) => u.name).join(', ')}`, '#ff6b5a');
        if (!seen) return;
        look(spots[0]!);
        this.sfx.throwWhoosh();
        for (const p of spots) this.fx.evacBeam(p);
        await wait(0.9);
        return;
      }
      case 'patrol':
        return;
      case 'podActivated': {
        const pod = v.pods.find((p) => p.id === e.pod);
        const members = pod?.unitIds.map((id) => getUnit(v, id)!).filter((m) => m.alive) ?? [];
        if (!members.length) return;
        const center = members.reduce((acc, m) => acc.add(tileToWorld(m.pos)), new THREE.Vector3()).divideScalar(members.length);
        look(center);
        if (cam()) await wait(0.35);
        for (const m of members) {
          this.units.forceVisible(m.id);
          this.units.face(m.id, this.closestSoldier(m.pos));
          this.fx.floatText(this.units.chestPos(m.id), '!', 'alert');
          void this.units.alert(m.id);
        }
        this.sfx.alert();
        this.hud.banner('¡CONTACTO!', 'alien');
        this.hud.log(`Contacto: ${members.map((m) => m.name).join(', ')}`, '#ff6b5a');
        await wait(0.9);
        return;
      }
      case 'concealmentBroken':
        this.hud.banner('OCULTACIÓN ROTA', 'warn');
        await wait(0.6);
        return;
      case 'turnBegan': {
        if (e.team === 'xcom') this.rig.restoreZoom();
        this.sfx.turn(e.team === 'alien');
        // With a partner, the banner says who opens the turn.
        const opener = e.team === 'xcom' && this.partnerHere() && e.command !== undefined ? (e.command === this.mySlot ? ' · TIENES EL MANDO' : ` · MANDO: ${this.playerName(e.command).toUpperCase()}`) : '';
        this.hud.banner(e.team === 'xcom' ? `TURNO ${e.turn}${opener}` : 'TURNO ENEMIGO', e.team === 'xcom' ? 'xcom' : 'alien');
        await wait(1);
        return;
      }
      case 'commandPassed': {
        this.sfx.ui();
        if (e.to === this.mySlot) this.hud.banner('TIENES EL MANDO', 'xcom');
        this.hud.log(e.to === this.mySlot ? 'Tienes el mando' : `${this.playerName(e.to)} tiene el mando`, PLAYER_COLORS[e.to]);
        return;
      }
      case 'missionEnded':
        await wait(0.8);
        this.showOutcome(e.outcome, e.reason);
        return;
      case 'ready':
        return;
    }
  }

  /** Melee lunge, or aim + burst for ranged weapons; then hit or miss feedback. */
  private async playAttack(e: ShotEvent, cam: () => boolean, look: (p: THREE.Vector3) => void): Promise<void> {
    const v = this.view;
    const shooter = getUnit(v, e.unit)!;
    const target = getUnit(v, e.target)!;
    this.lastShot = { target: target.id, result: e.result };
    if (!this.shownToSquad(shooter) && !this.shownToSquad(target)) return;

    const weapon = attackWeapon(shooter, e.ability);
    const shooterPos = this.units.worldPos(shooter.id);
    const targetPos = this.units.worldPos(target.id);
    const chest = this.units.chestPos(target.id);
    const hit = e.result !== 'miss';
    const color = shooter.team === 'xcom' ? 0x9fe8ff : 0xff6b5a;
    if (cam()) {
      look(shooterPos.clone().lerp(targetPos, 0.5));
      this.rig.cinematicZoom(THREE.MathUtils.clamp(shooterPos.distanceTo(targetPos) * 1.1 + 9, 13, 24));
    }
    if (e.reaction) {
      this.fx.floatText(this.units.chestPos(shooter.id), 'FUEGO DE REACCIÓN', 'warn');
      this.sfx.alert();
    }

    if (weapon.melee) {
      await wait(cam() ? 0.2 : 0.05);
      this.sfx.throwWhoosh();
      await this.units.lunge(shooter.id, targetPos);
      if (hit) {
        const stun = e.ability === 'stunLance';
        this.fx.slash(chest, stun ? 0x6ad8ff : 0xe8f6ff);
        this.sfx.impact();
      } else {
        this.fx.floatText(chest, 'FALLO', 'miss');
        this.hud.log(`${shooter.name} falla a ${target.name} (${e.chance}%)`);
      }
      await wait(0.25);
      return;
    }

    const stepOut = !sameTile(e.from, shooter.pos);
    if (stepOut) await this.units.slide(shooter.id, tileToWorld(e.from), 0.22, false);
    await this.units.raiseWeapon(shooter.id, targetPos);
    await wait(cam() ? 0.25 : 0.08);

    const heavy = weapon.apCost > 1;
    const pellets = weapon.range === 'short' ? 6 : 0;
    const rounds = Math.max(1, weapon.burst);
    for (let i = 0; i < rounds; i++) {
      const from = this.units.muzzlePos(shooter.id);
      this.units.recoil(shooter.id);
      this.fx.muzzleFlash(from, color);
      this.sfx.shot(shooter.team === 'alien');
      if (heavy && cam()) this.rig.shake(0.08, 0.15);
      const last = i === rounds - 1;
      const shots = pellets || 1;
      for (let p = 0; p < shots; p++) {
        const dest = hit ? chest.clone().add(jitter(pellets ? 0.6 : 0.25)) : missPoint(from, chest, i + p, pellets ? 1.6 : 1);
        void this.fx.bullet(from, dest, color, heavy ? 45 : 60, heavy).then(() => {
          this.fx.sparks(dest, hit ? 0xff7a5a : 0xffd28a, pellets ? 3 : hit ? 8 : 5);
          if (hit && last && p === 0) this.sfx.impact();
        });
      }
      await wait(weapon.burst > 3 ? 0.08 : 0.11);
    }
    await wait(heavy ? 0.25 : 0.15);

    if (!hit) {
      this.fx.floatText(chest, 'FALLO', 'miss');
      this.hud.log(`${shooter.name} falla a ${target.name} (${e.chance}%)`);
      await wait(0.35);
    }
    // Lower the weapon (and step back into cover) once the outcome has been seen.
    void wait(1.1).then(() => {
      this.units.lowerWeapon(shooter.id);
      if (stepOut) void this.units.slide(shooter.id, tileToWorld(shooter.pos), 0.25);
    });
  }

  private async playExplosive(
    e: Extract<GameEvent, { t: 'explosive' }>,
    cam: () => boolean,
    look: (p: THREE.Vector3) => void,
  ): Promise<void> {
    this.lastShot = null;
    const u = getUnit(this.view, e.unit)!;
    const def = ABILITIES[e.ability];
    const to = tileToWorld(e.target, 0.15);
    this.hud.log(`${u.name}: ${def.name.toLowerCase()}`);
    if (cam()) {
      look(this.units.worldPos(u.id).lerp(to, 0.5));
      this.rig.cinematicZoom(18);
    }
    this.units.face(u.id, to);
    await wait(0.25);
    if (e.ability === 'discharge') {
      const home = this.units.dronePos(u.id);
      this.units.setDroneAway(u.id, true);
      this.sfx.ui();
      await this.fx.droneFlight(home, to.clone().add(new THREE.Vector3(0, 1.4, 0)));
      this.sfx.explosion();
      if (cam()) this.rig.shake(0.25, 0.4);
      void this.fx.explosion(to, def.radius ?? 1.5, true);
      void this.fx.droneFlight(to.clone().add(new THREE.Vector3(0, 1.4, 0)), this.units.dronePos(u.id)).then(() => this.units.setDroneAway(u.id, false));
      await wait(0.4);
      return;
    } else if (e.ability === 'missiles') {
      const from = this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.5, 0));
      const spots = [to, ...Array.from({ length: 3 }, () => to.clone().add(new THREE.Vector3((Math.random() - 0.5) * 2, 0, (Math.random() - 0.5) * 2)))];
      this.sfx.throwWhoosh();
      await this.fx.missiles(from, spots);
    } else if (e.ability === 'launch') {
      await this.units.raiseWeapon(u.id, to);
      const from = this.units.muzzlePos(u.id);
      this.units.recoil(u.id);
      this.fx.muzzleFlash(from, 0xffc080);
      this.sfx.throwWhoosh();
      await this.fx.grenadeArc(from, to);
      this.units.lowerWeapon(u.id);
    } else {
      this.sfx.throwWhoosh();
      await this.units.throwMotion(u.id);
      await this.fx.grenadeArc(this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.45, 0)), to);
    }
    if (e.ability === 'smoke') {
      this.sfx.throwWhoosh();
      this.fx.sparks(to, 0xdfe6ec, 14);
      await wait(0.3);
      return;
    }
    if (e.ability === 'flashbang') {
      this.sfx.impact();
      this.fx.muzzleFlash(to.clone().setY(to.y + 0.6), 0xffffff);
      this.fx.sparks(to.clone().setY(to.y + 0.6), 0xffffff, 24);
      if (cam()) this.rig.shake(0.15, 0.3);
      await wait(0.4);
      return;
    }
    this.sfx.explosion();
    if (cam()) this.rig.shake(0.45, 0.6);
    void this.fx.explosion(to, def.radius ?? 2.5);
    await wait(0.4);
  }

  private closestSoldier(p: Vec2): THREE.Vector3 {
    const soldiers = this.view.units.filter((u) => u.alive && u.team === 'xcom');
    const best = soldiers.sort((a, b) => Math.hypot(a.pos.x - p.x, a.pos.y - p.y) - Math.hypot(b.pos.x - p.x, b.pos.y - p.y))[0];
    return best ? tileToWorld(best.pos) : tileToWorld(p);
  }

  private showOutcome(outcome = this.state.outcome, reason: EndReason | null = this.state.outcomeReason): void {
    if (!outcome) {
      this.hud.showOutcome(null);
      return;
    }
    const s = this.state;
    const lost = s.units.filter((u) => u.team === 'xcom' && !u.alive && !u.evacuated).length;
    const evacuated = s.units.filter((u) => u.evacuated).length;
    const killed = s.units.filter((u) => u.team === 'alien' && !u.alive).length;
    const vip = s.mission.kind === 'rescue' && s.mission.objectiveUnit ? getUnit(s, s.mission.objectiveUnit)?.name : undefined;
    const success: Record<typeof s.mission.kind, string> = {
      elimination: 'Todos los hostiles abatidos',
      recovery: `${capitalize(this.recoveryItem())} a salvo`,
      sabotage: 'Retransmisor destruido',
      hack: 'Terminal hackeado',
      rescue: vip ? `${vip}, a salvo` : 'VIP extraído',
    };
    const titles: Record<EndReason, string> = {
      objective: success[s.mission.kind],
      wiped: evacuated ? 'La escuadra se retiró sin cumplir el objetivo' : 'Escuadra eliminada',
      timer: 'Se agotó el tiempo',
      abandoned: 'Misión abandonada',
      objectiveLost: vip ? `${vip} ha muerto` : 'El VIP ha muerto',
    };
    const parts = [`Turnos: ${s.turn}`, `Enemigos abatidos: ${killed}`, `Bajas propias: ${lost}`];
    if (s.mission.kind !== 'elimination') parts.push(`Evacuados: ${evacuated}`);
    const title = [s.campaignMission?.name, reason ? titles[reason] : ''].filter(Boolean).join(' · ');
    this.hud.showOutcome(outcome, title, parts.join(' · '), !!s.campaignMission);
  }

  /** Re-derives fog, alien visibility, flags and mission markers from the shown state. */
  private refreshView(): void {
    this.mapView.setVisibility(teamVisibility(this.view, 'xcom'));
    this.visibleAliens = new Set(visibleEnemies(this.view, 'xcom').map((u) => u.id));
    const watchers = this.view.units.filter((a) => a.alive && a.team === 'alien' && this.visibleAliens.has(a.id));
    const selected = this.selected ? getUnit(this.view, this.selected) : undefined;
    this.units.sync(this.view, this.visibleAliens, (u) => {
      if (u.team === 'xcom') {
        // With no enemy in sight nobody flanks anyone: the shield only shows cover, never the red "flanked".
        if (!watchers.length) return hasAnyCover(this.view, u.pos) ? (Math.max(...coverSides(this.view, u.pos)) as 0 | 1 | 2) : null;
        return Math.min(...watchers.map((a) => coverAgainst(this.view, u.pos, a.pos))) as 0 | 1 | 2;
      }
      if (selected?.alive && sightOrigin(this.view, selected.pos, u.pos, TEMPLATES[selected.template].sight)) {
        return coverAgainst(this.view, u.pos, selected.pos);
      }
      return null;
    });
    const m = this.view.mission;
    this.overlays.showEvacZone(m.evacZone);
    this.overlays.setItem(m.item?.pos ?? null);
    const beacons: { pos: Vec2; color: number }[] = [];
    if (m.terminal && !m.objectiveDone) beacons.push({ pos: m.terminal, color: 0x54d1ff });
    const objective = m.objectiveUnit ? getUnit(this.view, m.objectiveUnit) : undefined;
    if (objective?.alive && !m.objectiveDone) beacons.push({ pos: objective.pos, color: objective.team === 'alien' ? 0xff4a6a : 0x4dff9a });
    this.overlays.setBeacons(beacons);
    this.overlays.setFlares(m.reinforcements.filter((r) => r.pos && !r.landed).map((r) => r.pos!));
    this.overlays.setSmoke(this.view.smoke);
  }

  // ============================================================ interaction

  private isMine(u: Unit): boolean {
    return u.team === 'xcom' && (u.owner === this.mySlot || u.owner === null);
  }

  /** Orders are accepted once the alien turn has finished playing on screen, not just on the server. */
  private canAct(): boolean {
    return this.state.activeTeam === 'xcom' && this.view.activeTeam === 'xcom' && !this.state.outcome;
  }

  /** Whether this player holds the command (el mando); a holder who left blocks nobody. */
  private hasCommand(): boolean {
    const holder = this.state.command;
    return holder === undefined || holder === this.mySlot || !this.players.some((p) => p.slot === holder && p.connected);
  }

  private playerName(slot: Slot | undefined): string {
    return this.players.find((p) => p.slot === slot)?.name ?? 'tu compañero';
  }

  /** The partner, when connected: only then is there a command to share. */
  private partnerHere(): PlayerInfo | undefined {
    return this.players.find((p) => p.slot !== this.mySlot && p.connected);
  }

  private passCommand(): void {
    if (!this.canAct()) return;
    if (!this.hasCommand()) {
      this.hud.toast(`Tiene el mando ${this.playerName(this.state.command)}.`);
      return;
    }
    const partner = this.partnerHere();
    if (!partner) this.hud.toast('Tu compañero no está conectado.');
    else if (this.state.ready[partner.slot]) this.hud.toast(`${partner.name} ya ha terminado el turno.`);
    else if (!busySlots(this.state).has(partner.slot)) this.hud.toast(`A ${partner.name} no le quedan acciones.`);
    else this.send({ type: 'pass' });
  }

  private selectedUnit(): Unit | undefined {
    const u = this.selected ? getUnit(this.state, this.selected) : undefined;
    return u?.alive ? u : undefined;
  }

  /** Player-driven selection: takes the camera away from any cinematic. */
  private select(id: string | null, focus = false): void {
    this.rig.takeControl();
    if (this.selected !== id) {
      this.selected = id;
      this.mode = { kind: 'move' };
      this.targetId = null;
      this.concealWarn = null;
    }
    this.units.setSelected(id);
    const u = this.selectedUnit();
    if (u && focus) this.rig.focus(tileToWorld(u.pos));
    this.refreshInteractive();
    this.sendPresence();
  }

  /** Keeps a valid selection: the current unit if it can still act, else the next one that can. */
  private autoSelect(): void {
    const current = this.selectedUnit();
    // Keep the acting soldier selected until its action has finished playing.
    if (current && this.myPending > 0) return;
    if (current && this.isMine(current) && (current.ap > 0 || !this.canAct())) return;
    const next = this.myUnits().find((u) => u.ap > 0) ?? (current && this.isMine(current) ? current : this.myUnits()[0]);
    this.selected = next?.id ?? null;
    this.units.setSelected(this.selected);
    if (next && current && next.id !== current.id && this.canAct()) this.rig.focus(tileToWorld(next.pos));
  }

  private myUnits(): Unit[] {
    return this.state.units.filter((u) => u.alive && this.isMine(u));
  }

  private cycleUnit(dir: 1 | -1): void {
    const list = this.myUnits().filter((u) => u.ap > 0 || !this.canAct());
    if (!list.length) return;
    const i = list.findIndex((u) => u.id === this.selected);
    const next = list[(i + dir + list.length) % list.length]!;
    this.select(next.id, true);
  }

  private reach(): { reach: Reach; tiles: MoveTile[] } | null {
    const u = this.selectedUnit();
    if (!u || !this.canAct() || !this.isMine(u) || u.ap <= 0) return null;
    const key = `${u.id}:${this.state.seq}`;
    if (this.reachCache?.key === key) return this.reachCache;
    const reach = computeReach(this.state, u, maxMoveCost(u));
    const perAp = costPerAp(u);
    const watchers = this.state.concealed
      ? this.state.units.filter((a) => a.alive && a.team === 'alien' && this.visibleAliens.has(a.id))
      : [];
    const tiles = destinations(this.state, u, reach).map((d) => ({
      pos: d.pos,
      dash: d.cost > perAp,
      detected: watchers.some((a) => sightOrigin(this.state, a.pos, d.pos, TEMPLATES[a.template].detection) !== null),
    }));
    this.reachCache = { key, reach, tiles };
    return this.reachCache;
  }

  /** Valid targets for a targeted ability, best first. */
  private targetOptions(ability: AbilityId): TargetOption[] {
    const u = this.selectedUnit();
    if (!u) return [];
    const s = this.state;
    const def = ABILITIES[ability];
    if (def.target === 'melee' && isAttack(ability)) {
      return meleeTargets(s, u, ability)
        .map((o) => ({ unit: o.target, tile: o.tile, path: o.path, hit: previewShot(s, u, o.target, { ability, from: o.tile })?.hit ?? 0 }))
        .sort((a, b) => b.hit - a.hit);
    }
    if (def.target === 'enemy' && isAttack(ability)) {
      return rangedTargets(s, u, ability)
        .filter((t) => this.visibleAliens.has(t.id))
        .map((t) => ({ unit: t, hit: previewShot(s, u, t, { ability })!.hit }))
        .sort((a, b) => b.hit - a.hit)
        .map(({ unit }) => ({ unit }));
    }
    if (def.target === 'enemy') return abilityTargets(s, u, ability).filter((t) => this.visibleAliens.has(t.id)).map((unit) => ({ unit }));
    if (def.target === 'ally') {
      return abilityTargets(s, u, ability)
        .sort((a, b) => a.hp / a.maxHp - b.hp / b.maxHp)
        .map((unit) => ({ unit }));
    }
    return [];
  }

  private currentTarget(): TargetOption | undefined {
    if (this.mode.kind !== 'target') return undefined;
    return this.targetOptions(this.mode.ability).find((o) => o.unit.id === this.targetId);
  }

  private enterTarget(ability: AbilityId, id?: string): void {
    const list = this.targetOptions(ability);
    if (!list.length) {
      this.hud.toast(ABILITIES[ability].target === 'ally' ? 'No hay aliados válidos.' : 'No hay enemigos a su alcance.');
      return;
    }
    this.mode = { kind: 'target', ability };
    this.targetId = id && list.some((t) => t.unit.id === id) ? id : list[0]!.unit.id;
    this.focusTarget();
    this.refreshInteractive();
  }

  private cycleTarget(dir: 1 | -1): void {
    if (this.mode.kind !== 'target') return;
    const list = this.targetOptions(this.mode.ability);
    if (!list.length) return;
    const i = list.findIndex((t) => t.unit.id === this.targetId);
    this.targetId = list[(i + dir + list.length) % list.length]!.unit.id;
    this.focusTarget();
    this.refreshInteractive();
  }

  private focusTarget(): void {
    const u = this.selectedUnit();
    const t = this.targetId ? getUnit(this.state, this.targetId) : undefined;
    if (u && t) this.rig.focus(tileToWorld(u.pos).lerp(tileToWorld(t.pos), 0.5));
  }

  private confirmTarget(): void {
    const u = this.selectedUnit();
    if (!u || this.mode.kind !== 'target' || !this.targetId) return;
    this.send({ type: 'ability', unit: u.id, ability: this.mode.ability, target: this.targetId });
    this.cancelMode();
  }

  private cancelMode(): void {
    this.mode = { kind: 'move' };
    this.targetId = null;
    this.concealWarn = null;
    this.refreshInteractive();
  }

  private useAbility(id: AbilityId): void {
    const u = this.selectedUnit();
    if (!u || !this.canAct()) return;
    const view = this.abilities().find((a) => a.id === id);
    if (!view?.enabled) {
      if (view?.reason) this.hud.toast(view.reason);
      return;
    }
    switch (ABILITIES[id].target) {
      case 'enemy':
      case 'melee':
      case 'ally':
      case 'corpse':
        if (this.mode.kind === 'target' && this.mode.ability === id) this.confirmTarget();
        else this.enterTarget(id);
        return;
      case 'tile':
        this.mode = this.mode.kind === 'tile' && this.mode.ability === id ? { kind: 'move' } : { kind: 'tile', ability: id };
        this.refreshInteractive();
        return;
      case 'self':
        this.send({ type: 'ability', unit: u.id, ability: id });
        this.cancelMode();
    }
  }

  /** Space: confirm the current target, or aim with the first usable attack. */
  private primaryAttack(): void {
    if (this.mode.kind === 'target') {
      this.confirmTarget();
      return;
    }
    const views = this.abilities();
    const usable = PRIMARY_ATTACKS.map((id) => views.find((v) => v.id === id)).find((v) => v?.enabled);
    if (usable) this.useAbility(usable.id);
    else this.hud.toast(views.find((v) => v.id === 'shoot')?.reason ?? 'No puede atacar ahora.');
  }

  private abilityList(u: Unit): AbilityId[] {
    const m = this.state.mission;
    return abilitiesOf(u).filter((id) => (id !== 'evac' || m.evacZone.length > 0) && (id !== 'hack' || (!!m.terminal && !m.objectiveDone)));
  }

  private abilities(): AbilityView[] {
    const u = this.selectedUnit();
    if (!u || !this.isMine(u)) return [];
    return this.abilityList(u).map((id, i) => {
      const def = ABILITIES[id];
      const charge = def.charge ? u.charges[def.charge] ?? 0 : undefined;
      let reason: string | null = !this.canAct()
        ? 'No es vuestro turno.'
        : !this.hasCommand()
          ? `Tiene el mando ${this.playerName(this.state.command)}.`
          : abilityBlocker(this.state, u, id);
      if (!reason && (def.target === 'enemy' || def.target === 'melee' || def.target === 'ally') && !this.targetOptions(id).length) {
        reason = def.target === 'ally' ? 'No hay aliados válidos.' : def.target === 'melee' ? 'Ningún enemigo a su alcance.' : 'Sin enemigos a la vista.';
      }
      const active = (this.mode.kind === 'target' || this.mode.kind === 'tile') && this.mode.ability === id;
      const cooldown = u.cooldowns[id] ?? 0;
      return {
        id,
        label: id === 'hack' ? `${def.name} · ${hackChance(this.state, u)}%` : def.name,
        hotkey: i < 9 ? (id === 'shoot' ? `${i + 1} · Esp` : String(i + 1)) : i === 9 ? '0' : '',
        enabled: !reason,
        reason: reason ? `${def.description} — ${reason}` : def.description,
        active,
        count: charge,
        cooldown: cooldown || undefined,
        free: def.ap === 0,
        depleted: charge === 0 || (isAttack(id) && attackWeapon(u, id).clip > 0 && u.ammo <= 0),
      };
    });
  }

  private toggleReady(): void {
    if (!this.canAct()) return;
    const ready = this.state.ready[this.mySlot];
    if (!this.hasCommand() && !ready) {
      this.hud.toast(`Tiene el mando ${this.playerName(this.state.command)}.`);
      return;
    }
    // Ending the turn with soldiers that can still act asks first (Backspace is easy to hit by mistake).
    const idle = this.idleSoldiers();
    if (!ready && idle && !this.endArmed) {
      this.armEndTurn(idle);
      return;
    }
    this.disarmEndTurn();
    this.send({ type: 'endTurn', ready: !ready });
  }

  /** My soldiers that still have actions this turn. */
  private idleSoldiers(): number {
    return this.myUnits().filter((u) => u.ap > 0).length;
  }

  private armEndTurn(idle: number): void {
    this.endArmed = true;
    window.clearTimeout(this.endArmTimer);
    this.endArmTimer = window.setTimeout(() => this.disarmEndTurn(), 4000);
    const partner = this.partnerHere();
    const next = partner && !this.state.ready[partner.slot] ? `el mando pasa a ${partner.name}` : 'juega el enemigo';
    this.hud.toast(`${idle === 1 ? 'Un soldado tiene' : `${idle} soldados tienen`} acciones. Pulsa otra vez para terminar: ${next}.`);
    this.refreshHud();
  }

  private disarmEndTurn(): void {
    if (!this.endArmed) return;
    this.endArmed = false;
    window.clearTimeout(this.endArmTimer);
    this.refreshHud();
  }

  // ================================================================ refresh

  /** Recomputes everything that depends on the latest state and the local selection. */
  private refreshInteractive(): void {
    this.autoSelect();
    if (this.mode.kind === 'target' && !this.currentTarget()) this.cancelModeSilently();
    if (this.mode.kind === 'tile') {
      const u = this.selectedUnit();
      if (!u || abilityBlocker(this.state, u, this.mode.ability)) this.cancelModeSilently();
    }
    const r = this.mode.kind === 'move' && this.hasCommand() && !this.isBusy(this.selected) ? this.reach() : null;
    this.overlays.showMoveRange(r?.tiles ?? []);
    this.updateHover();
    this.refreshHud();
  }

  private cancelModeSilently(): void {
    this.mode = { kind: 'move' };
    this.targetId = null;
  }

  /** Morse on the radio in story chapters: when the squad lands and when the objective is done. */
  private storyRadio(moment: 'start' | 'objective'): void {
    const chapter = this.state.campaignMission?.story;
    const line = chapter ? STORY_RADIO[chapter - 1]?.[moment] : undefined;
    if (line) this.hud.log(`Morse · ${line}`, '#1d4fb0');
  }

  /** What a recovery carries out: story chapters and side quests name it ("las cintas"). */
  private recoveryItem(): string {
    const cm = this.state.campaignMission;
    return cm?.item || (cm?.story && STORY[cm.story - 1]?.item) || 'los datos';
  }

  private missionView(): MissionView {
    const s = this.view;
    const m = s.mission;
    const after = 'eliminad a los hostiles o evacuad';
    let objective = 'Eliminad a todos los hostiles';
    switch (m.kind) {
      case 'recovery': {
        const carrier = m.item?.carrier ? getUnit(s, m.item.carrier) : undefined;
        const item = this.recoveryItem();
        objective = m.item?.evacuated
          ? `${capitalize(item)} a salvo: evacuad al resto`
          : carrier
            ? `Llevad ${item} (${carrier.name}) a la zona de evacuación`
            : `Recuperad ${item} y evacuad`;
        break;
      }
      case 'sabotage':
        objective = m.objectiveDone ? `Retransmisor destruido: ${after}` : 'Derribad el retransmisor';
        break;
      case 'hack':
        objective = m.objectiveDone ? `Terminal hackeado: ${after}` : 'Hackead el terminal (junto a él, o con el Grillo del especialista)';
        break;
      case 'rescue': {
        const vip = m.objectiveUnit ? getUnit(s, m.objectiveUnit) : undefined;
        objective = m.objectiveDone
          ? `${vip?.name ?? 'El VIP'} a salvo: evacuad al resto`
          : vip?.captive
            ? `Llegad hasta ${vip.name}`
            : `Escoltad a ${vip?.name ?? 'el VIP'} hasta la zona de evacuación`;
        break;
      }
    }
    const incoming = m.reinforcements.some((r) => r.pos && !r.landed);
    return { kind: m.kind, objective, turnsLeft: m.turnsLeft, alert: incoming ? 'REFUERZOS EN CAMINO' : null, area: s.mapName };
  }

  private refreshHud(): void {
    this.refreshStatus();
    const s = this.state;
    const busy = busySlots(s);
    const partner = this.partnerHere();
    const sharing = !!partner && s.activeTeam === 'xcom' && !s.outcome;
    const mine = this.hasCommand();
    this.hud.setAbilities(this.abilities());

    const panel = this.targetPanel();
    this.hud.setTarget(panel);

    const ready = s.ready[this.mySlot];
    const waiting = partner && busy.has(partner.slot) && !s.ready[partner.slot] ? partner.name : null;
    const idle = this.idleSoldiers();
    const mode = !this.canAct() ? 'disabled' : ready ? 'ready' : !mine ? 'waiting' : this.endArmed && idle ? 'confirm' : 'act';
    const detail = mode === 'confirm' ? `${idle} ${idle === 1 ? 'soldado' : 'soldados'} con acciones` : !mine && !ready ? this.playerName(s.command) : waiting;
    this.hud.setEndTurn(mode, detail);
    this.hud.setCommand(sharing && this.canAct() ? { mine, holder: this.playerName(s.command), color: PLAYER_COLORS[s.command ?? this.mySlot], canPass: mine && !s.ready[partner!.slot] && busySlots(s).has(partner!.slot) } : null);
  }

  /**
   * What the player reads about the fight (turn, objective, players, squad, the
   * selected soldier) is painted from `view`, what has been animated so far: a
   * death or a new turn must not show before it is seen. Orders and their
   * checks (abilities, end turn, the command) stay on `state`.
   */
  private refreshStatus(): void {
    const v = this.view;
    this.hud.setTurn(v.turn, v.activeTeam, v.concealed);
    this.hud.setMission(this.missionView());
    const sharing = !!this.partnerHere() && v.activeTeam === 'xcom' && !v.outcome;
    const holder = v.command === undefined || !this.players.some((p) => p.slot === v.command && p.connected) ? this.mySlot : v.command;
    this.hud.setPlayers(this.players, v.ready, this.mySlot, busySlots(v), sharing ? holder : null);
    this.hud.setSquad(
      v.units.filter((u) => u.team === 'xcom' || u.controlledBy),
      this.mySlot,
      this.selected,
      { 0: PLAYER_COLORS[0], 1: PLAYER_COLORS[1] },
      v.mission.item?.carrier ?? null,
    );
    const shown = this.selected ? getUnit(v, this.selected) : undefined;
    this.hud.setSelected(shown?.alive ? shown : null);
  }

  /** Builds the target panel and draws the aim line for the current target. */
  private targetPanel(): TargetView | null {
    const u = this.selectedUnit();
    const option = this.currentTarget();
    if (!u || !option || this.mode.kind !== 'target') {
      this.overlays.showAim(null, null);
      return null;
    }
    const ability = this.mode.ability;
    const def = ABILITIES[ability];
    const list = this.targetOptions(ability);
    const index = list.findIndex((t) => t.unit.id === option.unit.id);
    const target = option.unit;
    if (def.target === 'ally' || !isAttack(ability)) {
      const friendly = def.target === 'ally';
      this.overlays.showAim(u.pos, target.pos, friendly);
      const effects: Partial<Record<AbilityId, string>> = {
        medkit: `Cura +${Math.min(MEDKIT_HEAL + u.mods.heal, target.maxHp - target.hp)} (salud ${target.hp}/${target.maxHp})`,
        aid: `+${20 + u.mods.aid} de defensa hasta vuestro próximo turno`,
        revival: 'Quita el aturdimiento, la desorientación y el pánico',
        suppression: '−50 de puntería al objetivo hasta tu próximo turno; si se mueve, le disparas',
        combatProtocol: `${TEMPLATES[target.template].robotic ? 4 : 2} de daño. Nunca falla e ignora el blindaje`,
      };
      return { kind: 'support', title: def.name, action: def.name, targetName: target.name, effect: effects[ability] ?? def.description, index, total: list.length };
    }
    const attack = ability as AttackAbility;
    const shot = previewShot(this.state, u, target, { ability: attack, from: option.tile });
    if (!shot) {
      this.overlays.showAim(null, null);
      return null;
    }
    this.overlays.showAim(shot.origin, target.pos);
    return {
      kind: 'attack',
      title: `${def.name} · ${attackWeapon(u, attack).name}`,
      action: def.name,
      targetName: target.name,
      hit: shot.hit,
      crit: Math.min(shot.crit, shot.hit),
      graze: shot.graze,
      mods: shot.mods,
      critMods: shot.critMods,
      index,
      total: list.length,
      note: ATTACK_NOTES[attack],
    };
  }

  private updateHover(): void {
    const tile = this.hoverTile;
    const u = this.selectedUnit();
    this.overlays.showPath(null, null);
    this.overlays.showCover(null);
    this.overlays.showBlast(null, [], false);
    this.overlays.setHover(tile);
    const hovered = tile ? unitAt(this.view, tile) : undefined;
    this.units.setHovered(hovered && (hovered.team === 'xcom' || this.visibleAliens.has(hovered.id)) ? hovered.id : null);
    if (!u || !this.canAct() || this.isBusy(u.id)) return;

    // Melee: show the run to the strike position.
    const option = this.currentTarget();
    if (option?.path?.length) this.overlays.showPath(u.pos, option.path);
    if (!tile) return;

    if (this.mode.kind === 'tile') {
      const ability = this.mode.ability;
      const valid = canTargetTile(this.state, u, ability, tile);
      const area = blastTiles(this.state, ability, tile);
      const allies = area.filter((t) => unitAt(this.state, t)?.team === 'xcom');
      this.overlays.showBlast(tile, area, valid, allies);
      this.overlays.setHover(null);
      return;
    }
    if (this.mode.kind !== 'move') return;

    const occupant = unitAt(this.state, tile);
    if (occupant?.team === 'alien' && this.visibleAliens.has(occupant.id)) {
      this.overlays.setHover(tile, 0xff5a5a);
      return;
    }
    const r = this.reach();
    const dest = r?.tiles.find((t) => sameTile(t.pos, tile));
    if (!r || !dest) return;
    const path = buildPath(this.state, u, r.reach, tile);
    this.overlays.showPath(u.pos, path, dest.dash);
    this.overlays.showCover(tile, coverSides(this.state, tile));
    this.overlays.setHover(tile, dest.detected ? 0xff4a4a : dest.dash ? 0xffc93a : 0x9fe8ff);
  }

  private sendPresence(): void {
    const key = `${this.selected}|${this.hoverTile?.x},${this.hoverTile?.y}`;
    if (key === this.lastPresence) return;
    this.lastPresence = key;
    window.clearTimeout(this.presenceTimer);
    this.presenceTimer = window.setTimeout(
      () => this.net.send({ t: 'presence', unit: this.selected, tile: this.hoverTile }),
      60,
    );
  }

  // ================================================================== input

  private bindInput(): void {
    const canvas = this.renderer.gl.domElement;
    const pointer = new THREE.Vector2();

    const tileAtPointer = (e: PointerEvent | MouseEvent): Vec2 | null => {
      const rect = canvas.getBoundingClientRect();
      pointer.set(((e.clientX - rect.left) / rect.width) * 2 - 1, -((e.clientY - rect.top) / rect.height) * 2 + 1);
      this.raycaster.setFromCamera(pointer, this.renderer.camera);
      return this.pickTile(this.raycaster.ray);
    };

    canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    canvas.addEventListener('pointerdown', (e) => {
      if (e.button === 2) this.drag = { x: e.clientX, y: e.clientY, moved: false };
      if (e.button === 1) {
        e.preventDefault();
        const t = tileAtPointer(e);
        if (t) this.ping(t);
      }
    });
    window.addEventListener('pointerup', (e) => {
      if (e.button === 2 && this.drag) {
        if (!this.drag.moved) this.cancelMode();
        this.drag = null;
      }
    });
    canvas.addEventListener('pointermove', (e) => {
      if (this.drag) {
        const dx = e.clientX - this.drag.x;
        const dy = e.clientY - this.drag.y;
        if (Math.abs(dx) + Math.abs(dy) > 3) this.drag.moved = true;
        this.rig.panPixels(dx, dy);
        this.drag.x = e.clientX;
        this.drag.y = e.clientY;
        return;
      }
      const t = tileAtPointer(e);
      if (t?.x === this.hoverTile?.x && t?.y === this.hoverTile?.y) return;
      this.hoverTile = t;
      this.updateHover();
      this.sendPresence();
    });
    canvas.addEventListener('pointerleave', () => {
      this.hoverTile = null;
      this.updateHover();
      this.sendPresence();
    });
    canvas.addEventListener('click', (e) => {
      const t = tileAtPointer(e);
      if (t) this.clickTile(t);
    });
    canvas.addEventListener('wheel', (e) => {
      e.preventDefault();
      this.rig.zoom(e.deltaY);
    }, { passive: false });

    const onKey = (e: KeyboardEvent) => {
      if (isTyping(e)) return;
      if (/^Digit[0-9]$/.test(e.code)) {
        const u = this.selectedUnit();
        const n = Number(e.code.slice(5));
        const id = u ? this.abilityList(u)[n === 0 ? 9 : n - 1] : undefined;
        if (id) this.useAbility(id);
        return;
      }
      switch (e.code) {
        case 'Tab':
          e.preventDefault();
          if (this.mode.kind === 'target') this.cycleTarget(e.shiftKey ? -1 : 1);
          else this.cycleUnit(e.shiftKey ? -1 : 1);
          break;
        case 'Enter':
        case 'NumpadEnter':
          if (this.mode.kind === 'target') {
            e.preventDefault();
            this.confirmTarget();
          }
          break;
        case 'Space':
          e.preventDefault();
          (document.activeElement as HTMLElement | null)?.blur?.();
          this.primaryAttack();
          break;
        case 'Escape':
          // Esc backs out of aiming or of a pending confirmation first; otherwise it opens the menu.
          if (this.mode.kind !== 'move') this.cancelMode();
          else if (this.endArmed || this.concealWarn) {
            this.concealWarn = null;
            this.disarmEndTurn();
          } else this.hud.toggleMenu();
          break;
        case 'KeyC':
          this.passCommand();
          break;
        case 'Backspace':
          e.preventDefault();
          this.toggleReady();
          break;
        case 'KeyG':
          if (this.hoverTile) this.ping(this.hoverTile);
          break;
        case 'KeyM':
          this.hud.toast(this.sfx.toggleMute() ? 'Sonido desactivado (M)' : 'Sonido activado (M)');
          break;
        case 'KeyF': {
          const u = this.selectedUnit();
          if (u) this.rig.focus(tileToWorld(u.pos));
          break;
        }
      }
    };
    window.addEventListener('keydown', onKey);
    this.disposers.push(() => window.removeEventListener('keydown', onKey));
  }

  /**
   * First floor the ray meets, rooftops included. Walls and cover are ignored,
   * so the cursor always lands on a tile a unit could stand on.
   */
  private pickTile(ray: THREE.Ray): Vec2 | null {
    const s = this.state;
    const top = (Math.max(0, ...s.elev) + 1) * STOREY;
    if (ray.direction.y >= 0) return null;
    let t = ray.origin.y > top ? (ray.origin.y - top) / -ray.direction.y : 0;
    const end = (ray.origin.y + 0.5) / -ray.direction.y;
    const p = new THREE.Vector3();
    for (; t <= end; t += 0.04) {
      ray.at(t, p);
      const tile = worldToTile(p);
      if (tile.x < 0 || tile.y < 0 || tile.x >= s.width || tile.y >= s.height) continue;
      if (p.y <= (s.elev[tile.y * s.width + tile.x] ?? 0) * STOREY) return tile;
    }
    return null;
  }

  private clickTile(tile: Vec2): void {
    const occupant = unitAt(this.state, tile);
    const u = this.selectedUnit();

    // Ally targeting (medkit, aid): clicking a soldier picks them.
    if (this.mode.kind === 'target' && ABILITIES[this.mode.ability].target === 'ally' && occupant?.team === 'xcom') {
      if (this.targetId === occupant.id) this.confirmTarget();
      else if (this.targetOptions(this.mode.ability).some((o) => o.unit.id === occupant.id)) {
        this.targetId = occupant.id;
        this.refreshInteractive();
      }
      return;
    }
    if (occupant?.team === 'xcom') {
      if (this.isMine(occupant)) this.select(occupant.id);
      else this.hud.toast(`${occupant.name} es de tu compañero.`);
      return;
    }
    if (!this.canAct() || !u) return;
    if (!this.hasCommand()) {
      this.hud.toast(`Tiene el mando ${this.playerName(this.state.command)}: espera a que te lo ceda.`);
      return;
    }

    if (this.mode.kind === 'tile') {
      if (canTargetTile(this.state, u, this.mode.ability, tile)) {
        this.send({ type: 'ability', unit: u.id, ability: this.mode.ability, tile });
        this.cancelMode();
      } else {
        this.hud.toast('Fuera de alcance o fuera de la vista de la escuadra.');
      }
      return;
    }
    if (occupant?.team === 'alien' && this.visibleAliens.has(occupant.id)) {
      if (this.mode.kind === 'target' && this.targetId === occupant.id) {
        this.confirmTarget();
        return;
      }
      if (this.mode.kind === 'target' && this.targetOptions(this.mode.ability).some((o) => o.unit.id === occupant.id)) {
        this.targetId = occupant.id;
        this.refreshInteractive();
        return;
      }
      const attack = PRIMARY_ATTACKS.find((id) => this.abilities().find((a) => a.id === id)?.enabled && this.targetOptions(id).some((o) => o.unit.id === occupant.id));
      if (attack) this.enterTarget(attack, occupant.id);
      else this.hud.toast('No puede atacar a ese enemigo desde aquí.');
      return;
    }
    if (this.mode.kind === 'target') {
      this.cancelMode();
      return;
    }
    const dest = this.reach()?.tiles.find((t) => sameTile(t.pos, tile));
    if (dest) {
      const cost = this.reachCache!.reach.cost.get(tile.y * this.state.width + tile.x)!;
      if (apForCost(u, cost) > u.ap) return;
      // Breaking concealment cannot be undone: the first click on such a tile only warns.
      const key = `${u.id}|${tile.x},${tile.y}`;
      if (dest.detected && this.concealWarn !== key) {
        this.concealWarn = key;
        this.hud.toast('Desde ahí os ven: se rompe la ocultación. Haz clic otra vez para mover.');
        return;
      }
      this.concealWarn = null;
      this.send({ type: 'move', unit: u.id, to: tile });
    }
  }
}
