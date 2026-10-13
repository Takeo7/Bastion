import * as THREE from 'three';
import {
  ABILITIES,
  abilitiesOf,
  abilityBlocker,
  abilityDamage,
  abilityTargets,
  apForCost,
  apNeeded,
  applyEvent,
  attackWeapon,
  abilityRange,
  BACKPACK_SLOTS,
  BAD_LUCK_AIM,
  BAD_LUCK_MISSES,
  blastRadius,
  blastTiles,
  broughtBack,
  buildPath,
  canTargetTile,
  cloneState,
  computeReach,
  costPerAp,
  coverAgainst,
  coverSides,
  destinations,
  detectionTo,
  DIRS8,
  ENEMY_FILES,
  FOUND_WEAPONS,
  distance,
  endsTurn,
  getUnit,
  hackChance,
  hasAnyCover,
  hasPerk,
  HORDE,
  isAttack,
  isLit,
  ITEM_FINDS,
  lootSummary,
  lootTotals,
  maxMoveCost,
  MEDKIT_HEAL,
  meleeTargets,
  MISSION_NAMES,
  NIGHT,
  NIGHT_LABEL,
  NOISE,
  noiseOf,
  PLAYER_COLORS,
  previewShot,
  rangedTargets,
  reachesToSearch,
  sameTile,
  SCAVENGE,
  SEARCH_SPOTS,
  searchableTiles,
  searchLine,
  searchSpot,
  sightOrigin,
  sightTo,
  songTargets,
  STORM_TARGETS,
  STORY,
  STORY_RADIO,
  teamVisibility,
  tileAt,
  TEMPLATES,
  unitAt,
  visibleEnemies,
  wasSearched,
  type AbilityId,
  type AttackAbility,
  type Command,
  type DamagePreview,
  type DamageRange,
  type DecorKind,
  type EndReason,
  type GameEvent,
  type GameState,
  type Loot,
  type LootTotals,
  type MissionKind,
  type PingKind,
  type PlayerInfo,
  type Reach,
  type ShotResult,
  type SkirmishMap,
  type Slot,
  type TemplateId,
  type Unit,
  type Vec2,
} from '@bastion/engine';
import type { Net, NetStatus } from '../net';
import { clear } from '../ui/dom';
import { Hud, type AbilityView, type HordeView, type MissionView, type PackSlot, type PackView, type TargetView, type TurnOwner } from '../ui/hud';
import { CameraRig, isTyping } from './camera';
import { setTerrain, STOREY, tileToWorld, worldToTile } from './coords';
import { Fx } from './fx';
import { MapView } from './mapView';
import { Overlays, type MoveTile, type SpotKind } from './overlays';
import { Renderer } from './renderer';
import { Sfx } from './sfx';
import { SONG, SongFx, SongMarks } from './songFx';
import { setTimeScale, wait } from './tween';
import { UnitViews, type EnemyTag } from './unitViews';
import { EdgeMarks, type EdgeMark } from './edgeMarks';
import { CombatTips } from './combatTips';
import { playArrival } from './arrivals';
import { graphics } from '../settings';

/** `self`: a self ability pressed once, waiting for the second press (or Enter) that uses it. */
type Mode = { kind: 'move' } | { kind: 'target'; ability: AbilityId } | { kind: 'tile'; ability: AbilityId } | { kind: 'self'; ability: AbilityId };

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
  searched: 0.7,
  infected: 0.75,
  moved: 0.12,
};

/**
 * Night missions (docs/ALTURA-Y-NOCHE.md §2), as the HUD explains them; the
 * lines are placeholders for Lore. `NIGHT_RULES` goes in the «De noche» tag's
 * tooltip and the log, `NIGHT_RADIO` is Morse on the first turn, `LIGHT_LINES`
 * what a Bengala or a fire lights up.
 */
const NIGHT_RULES = `De noche: los vivos ven ${NIGHT.sightCut} casillas menos (${NIGHT.minSight} como mínimo) y os detectan a la mitad de distancia. A la luz de una farola, una bengala o un fuego os ven como de día.`;
const NIGHT_RADIO = 'Noche cerrada. Se ve poco: id pegados a las farolas o llevad bengalas.';
const LIGHT_LINES = { bolt: 'La bengala ilumina la calle', fire: 'El fuego ilumina la zona' } as const;

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

/** Players with a soldier that still has actions. */
function busySlots(s: GameState): Set<Slot> {
  const busy = new Set<Slot>();
  for (const u of s.units) if (u.alive && u.team === 'xcom' && u.ap > 0 && u.owner !== null) busy.add(u.owner);
  return busy;
}

/**
 * Where a throw aimed at `tile` lands: the tile itself if it is in range, else
 * the farthest tile in range on the way there (as in XCOM, the aim stops at
 * the edge of the circle instead of losing the preview).
 */
function throwTile(u: Unit, ability: AbilityId, tile: Vec2): Vec2 {
  const range = abilityRange(u, ability);
  const far = distance(u.pos, tile);
  if (far <= range) return tile;
  for (let k = range / far; k > 0; k -= 0.01) {
    const p = { x: Math.round(u.pos.x + (tile.x - u.pos.x) * k), y: Math.round(u.pos.y + (tile.y - u.pos.y) * k) };
    if (distance(u.pos, p) <= range) return p;
  }
  return u.pos;
}

/** "las cintas" → "Las cintas". */
function capitalize(text: string): string {
  return text[0]!.toUpperCase() + text.slice(1);
}

/** What using an ability costs, in the cards' words, from the rules the server charges by. */
function costText(u: Unit, id: AbilityId): string {
  const need = apNeeded(u, id);
  if (need === 0) return 'Gratis';
  if (endsTurn(u, id)) return 'Termina el turno';
  return need === 1 ? '1 acción' : `${need} acciones`;
}

/** What is left of an ability: its cooldown and its uses. */
function limitsText(u: Unit, id: AbilityId): string | undefined {
  const def = ABILITIES[id];
  const left = u.cooldowns[id] ?? 0;
  const parts: string[] = [];
  if (left > 0) parts.push(`Disponible en ${left} ${left === 1 ? 'turno' : 'turnos'}`);
  else if (def.cooldown) parts.push(`Recarga en ${def.cooldown} turnos`);
  if (def.charge) parts.push(`Usos: ${u.charges[def.charge] ?? 0}`);
  return parts.join(' · ') || undefined;
}

/** «4–6», or «4» when it can't vary. */
function span(r: DamageRange): string {
  return r.min === r.max ? String(r.min) : `${r.min}–${r.max}`;
}

/** A critical next to the hit: «crítico +2» when it adds the same to every roll, else its own range. */
function critText(hit: DamageRange, crit: DamageRange): string {
  const extra = crit.min - hit.min;
  return extra === crit.max - hit.max ? `crítico +${extra}` : `crítico ${span(crit)}`;
}

/** How a damage figure spreads, after «Daño 4–6»: per shot, over an area, on several enemies. */
const DAMAGE_EACH: Partial<Record<AbilityId, string>> = {
  rapidFire: ' por disparo',
  faceoff: ' a cada uno',
  lightRain: ' por flecha',
  velmarStorm: ` a hasta ${STORM_TARGETS} enemigos`,
  grenade: ' en el área',
  launch: ' en el área',
  missiles: ' en el área',
  discharge: ' en el área',
  fireball: ' en el área',
  combatProtocol: ' (+2 a Acorazados y Sirenas)',
};

/** The hover card's damage line, from the rules' own numbers: «Daño 4–6 · crítico +2». */
function damageText(s: GameState, u: Unit, id: AbilityId): string | undefined {
  // Vigilancia fires the rifle at whoever moves: a reaction shot, which only crits with the talent.
  const watch = id === 'overwatch' || id === 'killZone';
  const d = abilityDamage(s, u, watch ? 'shoot' : id);
  if (!d) return undefined;
  const crit = d.crit && (!watch || hasPerk(u, 'guardCrit')) ? d.crit : null;
  return [
    `Daño ${span(d.hit)}${watch ? ' por disparo' : DAMAGE_EACH[id] ?? ''}`,
    crit ? critText(d.hit, crit) : '',
    d.shred ? `destroza ${d.shred} de blindaje` : '',
    d.ignoresArmor ? 'ignora el blindaje' : '',
  ]
    .filter(Boolean)
    .join(' · ');
}

/**
 * How far using it is heard, for the cards (docs/FASE2.md): «ruido 12», or
 * «sin ruido» for a silent attack or trade. Vigilancia is heard when it fires.
 */
function noiseText(s: GameState, u: Unit, id: AbilityId): string | undefined {
  if (id === 'overwatch' || id === 'killZone') {
    const n = attackWeapon(u, 'shoot').noise;
    return n ? `ruido ${n} al disparar` : undefined;
  }
  const n = noiseOf(s, u, id);
  if (n > 0) return `ruido ${n}`;
  return isAttack(id) || ABILITIES[id].song ? 'sin ruido' : undefined;
}

/** What a move sounds like (NOISE in content.ts): running, a door, a window's glass. Null when it is quiet. */
function moveNoise(s: GameState, path: Vec2[], dash: boolean): string | null {
  const parts: [number, string][] = [];
  if (dash) parts.push([NOISE.dash, 'corre']);
  if (path.some((p) => tileAt(s, p.x, p.y) === 'window')) parts.push([NOISE.breach, 'rompe un cristal']);
  if (path.some((p) => tileAt(s, p.x, p.y) === 'door')) parts.push([NOISE.door, 'abre una puerta']);
  if (!parts.length) return null;
  parts.sort((a, b) => b[0] - a[0]);
  return `Ruido ${parts[0]![0]}: ${parts.map((p) => p[1]).join(', ')}`;
}

/**
 * What a search turned up, short, to float over the furniture: «+12 §», «Un
 * botiquín», «Medicinas», «+2 ◍» (rations), «+1 ⚙» (parts), a found weapon's
 * name, or «Nada».
 */
function findText(loot: Loot | null): string {
  const parts: string[] = [];
  if (loot?.supplies) parts.push(`+${loot.supplies} §`);
  if (loot?.item) parts.push(capitalize(ITEM_FINDS[loot.item]));
  if (loot?.medicines) parts.push('Medicinas');
  if (loot?.food) parts.push(`+${loot.food} ◍`);
  if (loot?.parts) parts.push(`+${loot.parts} ⚙`);
  if (loot?.weapon) parts.push(FOUND_WEAPONS[loot.weapon].name);
  return parts.join(' · ') || 'Nada';
}

/** «4 raciones», «una pieza»: rations and parts (docs/FASE4.md) as the log and the haul say them. Placeholder words for Lore. */
function rations(n: number): string {
  return n === 1 ? 'una ración' : `${n} raciones`;
}

function partsOf(n: number): string {
  return n === 1 ? 'una pieza' : `${n} piezas`;
}

function joinAnd(parts: string[]): string {
  return parts.length > 1 ? `${parts.slice(0, -1).join(', ')} y ${parts[parts.length - 1]}` : (parts[0] ?? '');
}

/**
 * The log line for a search. Lore's `searchLine` names supplies, accessories
 * and medicines; rations, parts and found weapons (docs/FASE4.md) are said
 * here until it does.
 */
function findLine(who: string, decor: DecorKind, loot: Loot | null, seed: number): string {
  if (!loot?.food && !loot?.parts && !loot?.weapon) return searchLine(who, decor, loot, seed);
  const what = [loot.food ? rations(loot.food) : '', loot.parts ? partsOf(loot.parts) : '', loot.weapon ? FOUND_WEAPONS[loot.weapon].find : ''].filter(Boolean);
  return `${who} registra ${SEARCH_SPOTS[decor]?.name ?? 'el mueble'}: ${joinAnd(what)}.`;
}

/**
 * What a squad carries or brought home, in one line: Lore's `lootSummary`
 * («El botín: +34 §, un botiquín y 2 medicinas.») with the rations, parts and
 * weapons of docs/FASE4.md added. Null with nothing.
 */
function haulText(t: LootTotals): string | null {
  const extra = [t.food ? rations(t.food) : '', t.parts ? partsOf(t.parts) : '', ...t.weapons.map((w) => FOUND_WEAPONS[w].find)].filter(Boolean);
  const base = lootSummary(t.supplies, t.items, t.medicines);
  if (!extra.length) return base;
  const before = [t.supplies ? `+${t.supplies} §` : '', ...t.items.map((id) => ITEM_FINDS[id]), t.medicines ? (t.medicines === 1 ? '1 medicina' : `${t.medicines} medicinas`) : ''].filter(Boolean);
  return `El botín: ${joinAnd([...before, ...extra])}.`;
}

/** The weapon an attack is made with, by name: a found weapon carried as the main one keeps its own («Fusil de caza», docs/FASE4.md). */
function weaponLabel(u: Unit, ability: AttackAbility): string {
  const weapon = attackWeapon(u, ability);
  return u.weaponName && weapon.id === TEMPLATES[u.template].weapon ? u.weaponName : weapon.name;
}

/** A backpack's things, one slot each, for the sheet. */
function packSlots(loot: readonly Loot[]): PackSlot[] {
  return loot.map((l): PackSlot => {
    if (l.item) return { kind: 'item', item: l.item };
    if (l.weapon) return { kind: 'weapon', weapon: l.weapon };
    if (l.medicines) return { kind: 'medicines', amount: l.medicines };
    if (l.food) return { kind: 'food', amount: l.food };
    if (l.parts) return { kind: 'parts', amount: l.parts };
    return { kind: 'supplies', amount: l.supplies ?? 0 };
  });
}

/** Which edge of the map a tile is nearest to, as the radio says it (the map's top row is the north). */
function edgeName(s: GameState, p: Vec2): string {
  const gaps: [number, string][] = [
    [p.y, 'el norte'],
    [s.height - 1 - p.y, 'el sur'],
    [p.x, 'el oeste'],
    [s.width - 1 - p.x, 'el este'],
  ];
  return gaps.sort((a, b) => a[0] - b[0])[0]![1];
}

/** «3 zombis», «un enemigo»: the dead by their name, the living (or a mix) as enemies. */
function heardCount(units: Unit[]): string {
  const dead = units.every((u) => TEMPLATES[u.template].undead);
  if (units.length === 1) return dead ? 'un zombi' : 'un enemigo';
  return `${units.length} ${dead ? 'zombis' : 'enemigos'}`;
}

/** Whether what `d` takes off kills `t`: surely, maybe, or only with a critical. */
function lethality(d: DamagePreview, t: Unit): string | undefined {
  if (d.hit.min >= t.hp) return 'Lo mata';
  if (d.hit.max >= t.hp) return 'Puede matarlo';
  if (d.crit && d.crit.max >= t.hp) return 'Un crítico lo mata';
  return undefined;
}

/** What armour does to a hit on `t`: soaks, is pierced, is stripped. */
function armorNotes(d: DamagePreview, t: Unit): string[] {
  const notes: string[] = [];
  if (d.ignoresArmor && t.armor) notes.push('Ignora su blindaje');
  else if (d.armor) notes.push(`Blindaje ${d.armor}: −${d.armor} por impacto`);
  if (!d.ignoresArmor && t.armor > d.armor) notes.push(`Perfora ${t.armor - d.armor} de blindaje`);
  if (d.shred && t.armor) notes.push(`Destroza ${Math.min(d.shred, t.armor)} de blindaje`);
  if (t.ruptured) notes.push(`Ruptura: +${t.ruptured} a cada impacto`);
  return notes;
}

/** Enemies whose name is feminine: «una Sirena». */
const FEMININE: Partial<Record<TemplateId, true>> = { relay: true };

/** "Saqueador" → "un Saqueador", "Sirena" → "una Sirena". */
function anEnemy(u: Unit | undefined): string {
  return u ? `${FEMININE[u.template] ? 'una' : 'un'} ${TEMPLATES[u.template].name}` : 'un enemigo';
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
  /** Arrows to the objective and the evacuation when they are off screen. */
  private readonly edge = new EdgeMarks((m) => this.lookAt(m.pos));
  /** Which mark the O key centres next. */
  private markCycle = 0;
  /** A key moment the partner just caused, until it fades or the player looks (UX-REVISION-2 #36). */
  private moment: { pos: Vec2; who: string } | null = null;
  private momentTimer = 0;
  /** The partner's soldier that acted last: where a broken concealment happened. */
  private partnerLast: string | null = null;
  /** How the squad lost its concealment, once it has: the contact that follows says who saw whom. */
  private exposure: Extract<GameEvent, { t: 'concealmentBroken' }> | null = null;
  private readonly sfx = new Sfx();
  /** The Oficio: its light tiles, and what stays on the field (Petardos, ¡Aguantad!). */
  private readonly songFx = new SongFx();
  private readonly songMarks = new SongMarks();
  private overlays!: Overlays;
  private readonly hud: Hud;
  /** Morse's first-time warnings (UX-REVISION-2 #30, UX/UI and Lore). */
  private readonly tips: CombatTips;

  private state!: GameState;
  private view!: GameState;
  private queue: QueuedEvent[] = [];
  private current: QueuedEvent | null = null;
  private playing = false;
  /** Events from our own commands still waiting to be animated. */
  private myPending = 0;
  /** The pause owed once our own action has played (HOLD_AFTER of its last event that isn't its noise). */
  private holdAfter = 0.2;
  private visibleAliens = new Set<string>();
  private lastShot: { target: string; result: ShotResult } | null = null;
  /** Last ability activated, so its follow-up events can be staged (e.g. Restoration's heals). */
  private lastUsed: { unit: string; ability: AbilityId } | null = null;

  private selected: string | null = null;
  private mode: Mode = { kind: 'move' };
  private targetId: string | null = null;
  private hoverTile: Vec2 | null = null;
  private reachCache: { key: string; reach: Reach; tiles: MoveTile[] } | null = null;
  /** Moves planned without the command, by unit: mine and the partner's (UX-REVISION-2 #35). */
  private plans = new Map<string, { slot: Slot; tile: Vec2 }>();
  private awaitingReply = false;
  private lastPresence = '';
  private presenceTimer = 0;
  private lastPing = 0;
  /** End turn pressed once with soldiers still able to act: the next press ends it. */
  private endArmed = false;
  private endArmTimer = 0;
  /** `unit|x,y` of a move that would break concealment, clicked once: the next click on it moves. */
  private concealWarn: string | null = null;
  /** The frame-rate guard's notice has been shown. */
  private qualityNoticed = false;
  /** The map has furniture to search (hand-made maps have none: Registrar is left off the bar). */
  private hasSpots = false;
  /** With Registrar armed, the furniture it would search (Tab or a click picks another). */
  private searchPick: Vec2 | null = null;
  /** A bitten soldier of ours has risen in this mission already: the stamp only comes the first time. */
  private risenSeen = false;

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
    this.sfx.attach(this.renderer.camera, () => this.rig.focusPoint);
    this.renderer.scene.add(this.mapView.group, this.units.group, this.fx.group, this.songFx.group, this.songMarks.group);
    this.mapView.watch(this.renderer.camera, () => ({ selected: this.selected, hover: this.hoverTile, target: this.targetId }));
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
    // Above the 3D labels, under the HUD panels.
    this.root.insertBefore(this.edge.root, this.hud.root);
    this.tips = new CombatTips(this.hud.root, this.mySlot);

    this.disposers.push(this.renderer.onFrame((dt, time) => this.frame(dt, time)));
    this.renderer.onQualityLowered = () => this.qualityLowered();
    this.bindInput();
    this.loadSnapshot(snapshot);
  }

  /** The frame-rate guard took quality off: said once, quietly (the details are in Ajustes). */
  private qualityLowered(): void {
    if (this.qualityNoticed) return;
    this.qualityNoticed = true;
    this.hud.toast('Calidad ajustada para ir más fluido.');
    this.hud.log('Calidad ajustada para ir más fluido (Menú → Ajustes).');
  }

  /** Advances camera, poses and particles; called by the render loop. */
  frame(dt: number, time: number): void {
    this.rig.update(dt);
    this.units.tick(dt, time);
    this.fx.tick(dt);
    this.songFx.tick(dt);
    this.overlays?.tick(time);
    this.mapView.tick(dt);
    const from = this.selected ? getUnit(this.view, this.selected) : undefined;
    this.edge.update(this.renderer.camera, from?.alive ? from.pos : null);
  }

  // ================================================================ network

  loadSnapshot(s: GameState): void {
    this.plans.clear();
    this.exposure = null;
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
    this.hasSpots = false;
    for (let i = 0; i < s.width * s.height && !this.hasSpots; i++) this.hasSpots = searchSpot(s, { x: i % s.width, y: Math.floor(i / s.width) }) !== null;
    this.mapView.build(this.view);
    // Night missions (docs/ALTURA-Y-NOCHE.md): the sky and the sun; a day mission after a night one turns them back.
    this.renderer.setNight(!!this.view.mission.night);
    this.units.build(this.view);
    if (!s.outcome) this.sfx.start(s.biome, !s.concealed);
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
      s.campaignMission?.kicker ?? (chapter ? `EXPEDIENTE · HOJA ${chapter}` : s.campaignMission ? 'OPERACIÓN EN CURSO' : 'ESCARAMUZA'),
      s.campaignMission?.name ?? MISSION_NAMES[s.mission.kind],
      [s.mapName, s.mission.night ? NIGHT_LABEL : '', mission.objective, mission.turnsLeft !== null ? `${mission.turnsLeft} turnos` : ''].filter(Boolean),
    );
    // The briefing stays 4.2 s and fades for 0.8: no warning plays over it.
    this.tips.intro(5000, s);
    this.storyRadio('start');
    // A night mission: Morse says so once the briefing is gone, and the log keeps the rules.
    if (s.mission.night) {
      this.tips.story(NIGHT_RADIO);
      this.hud.log(NIGHT_RULES, '#2a4a7a');
    }
    void this.openingShot();
  }

  /**
   * Opening shot (UX-REVISION-2 #32). The squad lands in a corner of the map:
   * centred on it, most of the screen was empty ground off the map. Frame it
   * with the map (or the enemies already in sight) in front of it instead,
   * and, while the briefing is up, pass over the objective and come back, as
   * XCOM 2 does. Any camera input from the player ends it.
   */
  private async openingShot(): Promise<void> {
    const v = this.view;
    const squad = v.units.filter((u) => u.alive && u.team === 'xcom' && !u.captive && !u.objective);
    if (!squad.length) return;
    const centre = (ps: Vec2[]) => ({ x: ps.reduce((a, p) => a + p.x, 0) / ps.length, y: ps.reduce((a, p) => a + p.y, 0) / ps.length });
    const here = centre(squad.map((u) => u.pos));
    const seen = v.units.filter((a) => a.alive && a.team === 'alien' && this.visibleAliens.has(a.id));
    const toward = seen.length ? centre(seen.map((a) => a.pos)) : { x: v.width / 2, y: v.height / 2 };
    // At most six tiles towards it, so the squad stays well inside the frame.
    const gap = distance(here, toward);
    const k = gap > 0 ? Math.min(6, gap / 2) / gap : 0;
    const home = tileToWorld({ x: Math.round(here.x + (toward.x - here.x) * k), y: Math.round(here.y + (toward.y - here.y) * k) });
    this.rig.focus(home, true);
    const epoch = this.rig.epoch;
    const goal = this.missionMarks()[0];
    if (!goal || distance(here, goal.pos) < 8) return;
    await wait(0.9);
    if (this.rig.epoch !== epoch) return;
    this.rig.focus(tileToWorld(goal.pos));
    await wait(2.1);
    if (this.rig.epoch !== epoch) return;
    this.rig.focus(home);
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

  /** The partner planned a move (or withdrew one, `tile: null`). */
  onPlan(slot: Slot, unit: string, tile: Vec2 | null): void {
    if (slot === this.mySlot) return;
    if (tile) this.plans.set(unit, { slot, tile });
    else this.plans.delete(unit);
    this.drawPlans();
    const u = getUnit(this.state, unit);
    if (tile && u) this.hud.log(`${this.playerName(slot)} planea mover a ${u.name}`, PLAYER_COLORS[slot]);
  }

  onPing(slot: Slot, tile: Vec2, kind: PingKind): void {
    this.fx.ping(tileToWorld(tile), PLAYER_COLORS[slot], kind === 'enemy');
    this.sfx.command();
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

  /**
   * The partner sent the squad back to the base: the report says «Volviendo al
   * Bastión…» too. It shows only if the base is built a frame later (main.ts).
   */
  returningToBase(): void {
    this.hud.returning();
  }

  destroy(): void {
    window.clearTimeout(this.endArmTimer);
    window.clearTimeout(this.momentTimer);
    this.tips.destroy();
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
      const epoch = this.rig.epoch;
      try {
        await this.play(item);
      } catch (err) {
        console.error('[anim]', err);
      }
      this.current = null;
      const secured = this.view.mission.objectiveDone;
      const seen = this.visibleAliens;
      applyEvent(this.view, item.e);
      const e = item.e;
      if (e.t === 'turnBegan') this.plans.clear();
      else if (e.t === 'moved' || e.t === 'died' || e.t === 'evacuated') this.plans.delete(e.unit);
      if (!secured && this.view.mission.objectiveDone) this.storyRadio('objective');
      if (item.e.t === 'tileChanged') this.mapView.rebuild(this.view);
      this.refreshView();
      this.refreshStatus();
      this.tips.event(item.e, this.view);
      try {
        await this.announceSighted(item, seen, epoch);
      } catch (err) {
        console.error('[anim]', err);
      }
      // The noise that follows an action doesn't set the pause after it: what was done does.
      if (item.by === this.mySlot && e.t !== 'noise') this.holdAfter = HOLD_AFTER[e.t] ?? 0.2;
      if (item.by === this.mySlot && --this.myPending === 0) {
        // Let the player see the outcome before the selection moves on.
        await wait(this.holdAfter);
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
   * Enemies that have just come into the squad's sight (`before`: who was in
   * sight until this event) get a callout over them and a line in the log, so
   * nobody «appears out of nowhere» (playtest). In the alien turn the camera
   * holds on the newcomer long enough to read it, unless the player took the
   * camera during the event (`epoch`); after our own action there is a short
   * pause before control comes back, and the partner's actions only get the
   * callout. Contacts, drops and raised corpses have their own show.
   */
  private async announceSighted(item: QueuedEvent, before: ReadonlySet<string>, epoch: number): Promise<void> {
    const e = item.e;
    if (e.t === 'podActivated' || e.t === 'spawned' || e.t === 'reanimated' || e.t === 'missionEnded') return;
    const v = this.view;
    const fresh = v.units.filter((a) => a.alive && this.visibleAliens.has(a.id) && !before.has(a.id) && !a.objective && TEMPLATES[a.template].team === 'alien');
    if (!fresh.length) return;
    const alert = (a: Unit) => !!v.pods.find((p) => p.id === a.podId)?.active;
    for (const a of fresh) {
      const name = TEMPLATES[a.template].name.toUpperCase();
      this.fx.floatText(this.units.chestPos(a.id), alert(a) ? `¡APARECE UN ${name}!` : `A LA VISTA: ${name}`, 'callout');
    }
    const names = fresh.map((a) => a.name).join(', ');
    // A pod that hasn't noticed the squad yet: the chance to set up an ambush.
    const unaware = !fresh.some(alert);
    this.hud.log(unaware ? `A la vista: ${names}${v.concealed ? ' (aún no os han visto)' : ''}` : fresh.length === 1 ? `¡Aparece ${anEnemy(fresh[0])}!` : `¡Aparecen: ${names}!`, '#ff6b5a');
    if (v.activeTeam === 'alien') {
      if (this.rig.epoch === epoch) this.rig.focus(this.units.worldPos(fresh[0]!.id));
      await wait(0.9);
    } else if (item.by === this.mySlot) {
      await wait(0.5);
    }
  }

  /**
   * Animates one event against the `view` state (before the event is applied).
   * The camera follows our own actions and the alien turn, never the partner's,
   * and stops following as soon as the player takes the camera.
   */
  private async play(item: QueuedEvent): Promise<void> {
    const { e, by } = item;
    if (by !== null && by !== this.mySlot) this.notePartner(e, by);
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
          if (steps++ % 2 === 0) this.sfx.step(u, p);
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
        if (e.mitigated) {
          this.fx.floatText(chest.clone().setY(chest.y - 0.45), `BLINDAJE −${e.mitigated}`, 'info');
          this.sfx.impact('armor', chest);
        }
        if (e.shred) this.fx.floatText(chest.clone().setY(chest.y + 0.45), 'BLINDAJE DAÑADO', 'warn');
        if (crit && cam()) this.rig.shake(0.18, 0.3);
        await this.units.hitReact(target.id, crit ? 1.5 : e.amount ? 1 : 0.4);
        return;
      }
      case 'died': {
        const u = getUnit(v, e.unit)!;
        this.hud.log(u.team === 'xcom' ? `${u.name} ha caído en combate` : `${u.name} eliminado`, u.team === 'xcom' ? '#ff6b5a' : '#54d1ff');
        if (!this.shownToSquad(u)) return;
        this.sfx.death(u, this.units.chestPos(u.id));
        // The Hinchado bursts as it falls: the blast (the next event) doesn't wait for the body.
        if (TEMPLATES[u.template].abilities.includes('burst')) {
          void this.units.die(u.id);
          await wait(0.15);
          return;
        }
        await this.units.die(u.id);
        await wait(0.2);
        return;
      }
      case 'stunned': {
        const u = getUnit(v, e.unit)!;
        this.hud.log(`${u.name} queda aturdido`, '#6ad8ff');
        if (!this.shownToSquad(u)) return;
        this.fx.sparks(this.units.chestPos(u.id), 0x6ad8ff, 14);
        this.sfx.stun(this.units.chestPos(u.id));
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
        if (e.t === 'hunker') this.sfx.hunker(this.units.worldPos(u.id));
        else this.sfx.overwatch(this.units.chestPos(u.id));
        await wait(0.45);
        return;
      }
      case 'abilityUsed': {
        this.lastUsed = { unit: e.unit, ability: e.ability };
        const u = getUnit(v, e.unit)!;
        if (e.ability === 'mindspin' && this.shownToSquad(u)) {
          // The Chillón's scream (docs/FASE2.md): what it does to the soldier comes with the `psi` that follows.
          look(this.units.worldPos(u.id));
          this.units.forceVisible(u.id);
          void this.units.scream(u.id);
          this.fx.scream(this.units.headPos(u.id));
          this.fx.floatText(this.units.chestPos(u.id), '¡GRITA!', 'callout');
          this.sfx.psi(this.units.chestPos(u.id));
          this.hud.log(`¡${capitalize(anEnemy(u))} grita!`, '#e8d8c0');
          await wait(cam() ? 0.7 : 0.3);
          return;
        }
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
        this.sfx.drone(home);
        await this.fx.droneFlight(home, dest);
        this.fx.sparks(this.units.chestPos(target.id), 0x8ad8ff, 18);
        this.sfx.stun(this.units.chestPos(target.id));
        this.hud.log(`${u.name}: calambrazo contra ${target.name}`, '#54d1ff');
        void this.fx.droneFlight(dest, this.units.dronePos(u.id)).then(() => this.units.setDroneAway(u.id, false));
        return;
      }
      // The Oficio: the effects proper are Arte's; this shows what happened.
      case 'sung': {
        const u = getUnit(v, e.unit)!;
        this.hud.log(`${u.name}: ${ABILITIES[e.ability].name}`, '#1f8a5c');
        if (!this.shownToSquad(u)) return;
        look(this.units.worldPos(u.id));
        this.fx.floatText(this.units.chestPos(u.id), ABILITIES[e.ability].name.toUpperCase(), 'info');
        await this.playSong(u, e);
        return;
      }
      case 'teleported': {
        // The body comes apart in light tiles and gathers again where the song sends it.
        const u = getUnit(v, e.unit)!;
        if (!this.shownToSquad(u)) return;
        const from = tileToWorld(e.from);
        const to = tileToWorld(e.to);
        this.sfx.song('vanish', from);
        void this.songFx.vanish(from);
        await wait(0.15);
        this.units.setHidden(u.id, true);
        await this.units.slide(u.id, to, 0.01);
        await wait(0.2);
        look(to);
        this.sfx.song('appear', to);
        await this.songFx.appear(to);
        this.units.setHidden(u.id, false);
        await wait(0.15);
        return;
      }
      case 'revived': {
        const u = getUnit(v, e.unit)!;
        this.hud.log(`${u.name} vuelve en pie`, '#6dff9a');
        look(this.units.worldPos(u.id));
        await this.units.rise(u.id);
        this.fx.floatText(this.units.chestPos(u.id), 'EN PIE', 'heal');
        await wait(0.3);
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
          this.sfx.shot(u, attackWeapon(u), from);
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
        this.hud.log(`${u.name} queda desorientado`, '#e8d8c0');
        await wait(0.15);
        return;
      }
      case 'marked': {
        const target = getUnit(v, e.unit)!;
        if (this.shownToSquad(target)) this.fx.floatText(this.units.chestPos(target.id).add(new THREE.Vector3(0, 0.4, 0)), 'MARCADO', 'info');
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
        // No second shriek: the scream ('mindspin') was just heard; this is what it does to the soldier.
        await this.fx.psiBeam(this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.35, 0)), this.units.chestPos(target.id));
        const texts: Record<string, string> = { disoriented: 'DESORIENTADO', panicked: 'PÁNICO', controlled: 'FUERA DE SÍ' };
        const text = e.effect ? texts[e.effect]! : 'RESISTE';
        this.fx.floatText(this.units.chestPos(target.id), text, e.success ? 'crit' : 'info');
        this.hud.log(e.success ? `${target.name}: ${text.toLowerCase()} (${e.chance}%)` : `${target.name} resiste el grito (${e.chance}%)`, e.success ? '#e8d8c0' : undefined);
        if (e.effect === 'controlled') this.hud.banner('¡FUERA DE SÍ!', 'alien');
        await wait(0.7);
        return;
      }
      case 'released': {
        const u = getUnit(v, e.unit)!;
        this.fx.sparks(this.units.chestPos(u.id), 0xf0e4d4, 14);
        this.fx.floatText(this.units.chestPos(u.id), 'LIBERADO', 'heal');
        this.hud.log(`${u.name} vuelve en sí`, '#6dff9a');
        await wait(0.5);
        return;
      }
      case 'reanimated': {
        // No `unit`: an infected soldier rising by itself (docs/FASE3.md), with no Chillón to shriek.
        const u = e.unit ? getUnit(v, e.unit) : undefined;
        const corpse = getUnit(v, e.corpse)!;
        const at = tileToWorld(corpse.pos, 0.5);
        look(at);
        if (u) this.units.forceVisible(u.id);
        // A fallen soldier of ours: the camera settles on the body a moment before it moves.
        if (!u && cam()) await wait(0.6);
        // The Chillón shrieks; the dead answer with a moan.
        if (u) this.sfx.psi(this.units.chestPos(u.id));
        this.sfx.moan(at);
        if (u) await this.fx.psiBeam(this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.35, 0)), at);
        this.fx.evacBeam(tileToWorld(corpse.pos));
        await this.units.reanimate(corpse.id, e.zombie);
        this.fx.floatText(tileToWorld(e.zombie.pos, 0.5), 'SE LEVANTA', 'crit');
        this.hud.log(u ? `El grito de un Chillón levanta el cuerpo de ${corpse.name}` : `Mordieron a ${corpse.name}: se levanta`, '#e8d8c0');
        // The first of the squad to rise gets the stamp (placeholder text for Lore).
        if (!u && !this.risenSeen) {
          this.risenSeen = true;
          this.hud.banner('¡SE LEVANTA!', 'alien', `Mordieron a ${corpse.name}: ya no es de los vuestros`);
        }
        await wait(u ? 0.6 : 1);
        return;
      }
      case 'infected': {
        // Bitten (docs/FASE3.md): nothing changes in the fight, but if it falls it rises, and at the base it turns.
        const u = getUnit(v, e.unit)!;
        this.hud.log(`Han mordido a ${u.name}`, '#ff6b5a');
        if (!this.shownToSquad(u)) return;
        this.fx.floatText(this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.5, 0)), 'MORDIDO', 'alert');
        await wait(0.5);
        return;
      }
      case 'reload': {
        const u = getUnit(v, e.unit)!;
        if (!this.shownToSquad(u)) return;
        look(this.units.worldPos(u.id));
        this.fx.floatText(this.units.chestPos(u.id), 'RECARGANDO', 'info');
        this.sfx.reload(this.units.chestPos(u.id));
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
          this.sfx.heal(this.units.chestPos(target.id));
          this.fx.floatText(this.units.chestPos(target.id), `+${e.amount}`, 'heal');
          this.hud.log(`${target.name} recupera ${e.amount}`, '#6dff9a');
          await wait(0.2);
          return;
        }
        look(this.units.worldPos(u.id).lerp(this.units.worldPos(target.id), 0.5));
        const home = this.units.dronePos(u.id);
        const dest = this.units.chestPos(target.id).add(new THREE.Vector3(0, 0.9, 0));
        this.units.setDroneAway(u.id, true);
        this.sfx.drone(home);
        await this.fx.droneFlight(home, dest);
        if (e.t === 'healed') {
          this.fx.heal(this.units.chestPos(target.id));
          this.sfx.heal(this.units.chestPos(target.id));
          this.fx.floatText(this.units.chestPos(target.id), `+${e.amount}`, 'heal');
          this.hud.log(`${u.name} cura ${e.amount} a ${target.name}`, '#6dff9a');
        } else {
          this.fx.shield(this.units.chestPos(target.id));
          this.sfx.shield(this.units.chestPos(target.id));
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
        this.sfx.beam(this.units.worldPos(u.id));
        await this.units.evacuate(u.id);
        return;
      }
      case 'itemPicked': {
        const u = getUnit(v, e.unit)!;
        this.hud.log(`${u.name} recupera los datos`, '#6dff9a');
        this.fx.floatText(this.units.chestPos(u.id), 'DATOS RECUPERADOS', 'heal');
        this.sfx.data();
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
          if (this.mapView.isVisible(e.pos.x, e.pos.y)) this.sfx.door(tileToWorld(e.pos, 1));
          return;
        }
        if (e.kind === 'windowBroken') {
          // Glass that breaks where no soldier sees is neither seen nor heard (playtest).
          if (!this.mapView.isVisible(e.pos.x, e.pos.y)) return;
          this.fx.debris(tileToWorld(e.pos, 1.3), 0x9fd8ff, 10);
          this.sfx.glass(tileToWorld(e.pos, 1.3));
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
        this.sfx.hack(screen);
        this.fx.sparks(screen, e.success ? 0x4dff9a : 0xff4a3a, 16);
        await wait(0.5);
        this.fx.floatText(screen, e.success ? 'ARREGLADO' : 'NO ARRANCA', e.success ? 'heal' : 'warn');
        this.hud.log(`${u.name} ${e.success ? 'arregla el equipo' : 'no consigue arreglarlo'} (${e.chance}%)`, e.success ? '#6dff9a' : '#ffc93a');
        this.sfx.hacked(e.success, screen);
        if (e.success) this.hud.banner('TERMINAL HACKEADO', 'xcom');
        else this.hud.banner('¡ALARMA!', 'alien');
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
        this.sfx.rescue();
        await wait(0.6);
        return;
      }
      case 'reinforcementsIncoming': {
        if (!e.r.pos) return;
        const at = tileToWorld(e.r.pos);
        if (e.r.horde) {
          // The Saqueo's horde (docs/FASE2.md): said, and marked at the edge, but the camera stays with the player.
          const where = edgeName(v, e.r.pos);
          this.hud.banner('MÁS ZOMBIS EN CAMINO', 'alien', `Llegan por ${where} al final del próximo turno enemigo`);
          this.hud.log(`${e.r.units.length === 1 ? 'Viene un zombi más' : `Vienen ${e.r.units.length} zombis más`} por ${where}: llegan al final del próximo turno enemigo. Cuanto más ruido, antes vienen.`, '#ff6b5a');
          await wait(0.6);
          return;
        }
        look(at);
        this.hud.banner('REFUERZOS EN CAMINO', 'alien');
        this.hud.log('Una bengala roja marca el sitio: llegan más al final del próximo turno enemigo', '#ff6b5a');
        this.sfx.flare(at);
        await wait(cam() ? 0.9 : 0.3);
        return;
      }
      case 'spawned': {
        const spots = e.units.map((u) => tileToWorld(u.pos));
        const seen = e.units.filter((u) => this.mapView.isVisible(u.pos.x, u.pos.y));
        const wave = v.mission.reinforcements.find((r) => r.id === e.reinforcement);
        if (wave?.horde) {
          // They walk in by the edge, on the prowl: no capsules, no beams.
          const where = edgeName(v, wave.pos ?? e.units[0]?.pos ?? { x: 0, y: 0 });
          this.hud.log(`La horda llega por ${where}: ${e.units.map((u) => u.name).join(', ')}`, '#ff6b5a');
          this.hud.banner('¡LA HORDA!', 'alien', `${e.units.length === 1 ? 'Ha llegado un zombi' : `Han llegado ${e.units.length} zombis`} por ${where}${seen.length ? '' : ', fuera de vuestra vista'}`);
          if (!seen.length) return;
          look(tileToWorld(seen[0]!.pos));
          await playArrival(this.units, this.fx, this.sfx, v, seen);
          for (const u of seen) this.fx.floatText(tileToWorld(u.pos, 1), `¡APARECE UN ${TEMPLATES[u.template].name.toUpperCase()}!`, 'callout');
          await wait(cam() ? 0.8 : 0.3);
          return;
        }
        this.hud.log(`Refuerzos: ${e.units.map((u) => u.name).join(', ')}`, '#ff6b5a');
        // Nobody drops in unannounced: the stamp says how many, and where if the squad sees it.
        this.hud.banner('¡REFUERZOS!', 'alien', `${e.units.length === 1 ? 'Ha llegado un enemigo' : `Han llegado ${e.units.length} enemigos`}${seen.length ? '' : ' fuera de vuestra vista'}`);
        if (!seen.length) return;
        look(spots[0]!);
        // They come in on foot from the edge, or get up out of the dust: no capsules (arrivals.ts).
        await playArrival(this.units, this.fx, this.sfx, v, seen);
        for (const u of seen) this.fx.floatText(tileToWorld(u.pos, 1), `¡APARECE UN ${TEMPLATES[u.template].name.toUpperCase()}!`, 'callout');
        await wait(cam() ? 0.8 : 0.2);
        return;
      }
      case 'patrol':
      case 'forgot':
      // A light going out fades on the map (MapView draws the lights from the state): nothing to say.
      case 'lightOff':
        return;
      case 'lightOn': {
        // Only a Bengala or a fire lights up during a mission (the street lights come with the map).
        const flare = this.lastUsed?.ability === 'bolt';
        this.hud.log(flare ? LIGHT_LINES.bolt : LIGHT_LINES.fire, '#ffc93a');
        return;
      }
      case 'noise':
        this.playNoise(e);
        return;
      case 'searched': {
        const u = getUnit(v, e.unit)!;
        const at = tileToWorld(e.tile, 0.6);
        const decor = searchSpot(v, e.tile);
        look(this.units.worldPos(u.id).lerp(at, 0.5));
        this.sfx.search(at);
        await this.units.rummage(u.id, at);
        // A found weapon is the rare one: its own, more striking slip.
        this.overlays.find(e.tile, findText(e.loot), !e.loot ? 'empty' : e.loot.weapon ? 'rare' : 'loot');
        if (e.loot) this.sfx.found(at);
        // The seed comes from the event, so both players read the same line.
        if (decor) this.hud.log(findLine(u.name, decor, e.loot, e.tile.x * 7 + e.tile.y * 13 + v.turn), e.loot?.weapon ? '#ffd23a' : e.loot ? '#6dff9a' : undefined);
        await wait(0.5);
        return;
      }
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
        this.sfx.contact();
        const how = this.contactCause(e);
        this.hud.banner('¡CONTACTO!', 'alien', how);
        this.hud.log(`Contacto: ${members.map((m) => m.name).join(', ')} · ${how}`, '#ff6b5a');
        await wait(0.9);
        return;
      }
      case 'concealmentBroken': {
        // Who gave the squad away and how (playtest: «no he entendido por qué me han visto»).
        this.exposure = e;
        this.sfx.reveal();
        const why = this.exposureText(e);
        this.hud.banner('OCULTACIÓN ROTA', 'warn', why);
        this.hud.log(`Ocultación rota: ${why}`, '#ffc93a');
        const spotter = e.by ? getUnit(v, e.by) : undefined;
        if (spotter?.alive && spotter.team === 'alien' && (e.reason === 'spotted' || e.reason === 'patrol')) {
          // A glimpse of whoever saw them, even from the fog.
          this.units.forceVisible(spotter.id);
          this.units.face(spotter.id, e.unit ? this.units.worldPos(e.unit) : this.closestSoldier(spotter.pos));
          this.fx.floatText(this.units.chestPos(spotter.id), 'OS HA VISTO', 'callout');
          look(this.units.worldPos(spotter.id));
          await wait(cam() ? 1.4 : 0.6);
          return;
        }
        await wait(0.8);
        return;
      }
      case 'turnBegan': {
        if (e.team === 'xcom') this.rig.restoreZoom();
        this.sfx.turn(e.team === 'alien', !v.concealed);
        // Whose turn opens: mine, the partner's (they hold the command) or the enemy's.
        const partner = e.team === 'xcom' && !!this.partnerHere() && e.command !== undefined && e.command !== this.mySlot;
        this.hud.banner(e.team === 'alien' ? 'TURNO ENEMIGO' : partner ? `TURNO DE ${this.playerName(e.command).toUpperCase()}` : 'TU TURNO', e.team === 'xcom' ? 'xcom' : 'alien');
        await wait(1);
        return;
      }
      case 'commandPassed': {
        this.sfx.command();
        if (this.partnerHere()) {
          if (e.to === this.mySlot) this.hud.banner('TU TURNO', 'xcom', 'Tienes el mando: das tú las órdenes');
          else this.hud.banner(`TURNO DE ${this.playerName(e.to).toUpperCase()}`, 'xcom', this.state.ready[this.mySlot] ? 'Has terminado: juega tu compañero' : 'Mientras, puedes apuntar y planear');
        }
        this.hud.log(e.to === this.mySlot ? 'Tienes el mando' : `${this.playerName(e.to)} tiene el mando`, PLAYER_COLORS[e.to]);
        return;
      }
      case 'missionEnded':
        this.sfx.outcome(e.outcome === 'victory');
        await wait(0.8);
        this.showOutcome(e.outcome, e.reason);
        return;
      case 'ready':
        return;
    }
  }

  /**
   * An Oficio ability's effect (songFx.ts) and sound, landing before the
   * events that follow it (damage, healing, the leap…): the flare, the
   * molotov, the bolts, the bandage.
   */
  private async playSong(u: Unit, e: Extract<GameEvent, { t: 'sung' }>): Promise<void> {
    const v = this.view;
    const hands = this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.25, 0));
    const targets = e.targets.map((id) => getUnit(v, id)).filter((t): t is Unit => !!t);
    const first = targets[0];
    const chest = (t: Unit) => this.units.chestPos(t.id);
    const feet = (t: Unit) => this.units.worldPos(t.id);
    if (first && first.id !== u.id) this.units.face(u.id, feet(first));
    switch (e.ability) {
      case 'bolt':
        this.sfx.song('bolt', hands);
        if (first) await this.songFx.bolt(hands, chest(first));
        return;
      case 'velmarStorm':
        this.sfx.song('storm', hands);
        targets.forEach((t, i) => this.sfx.song('thunder', chest(t), 0.2 + i * 0.18));
        await this.songFx.storm(targets.map(chest));
        return;
      case 'aegis':
      case 'hymn':
        this.sfx.song('aura', hands);
        this.songFx.aura(feet(u), ABILITIES[e.ability].range ?? 1.5, targets.map(feet), e.ability === 'aegis' ? SONG.cyan : SONG.green);
        await wait(0.6);
        return;
      case 'velmarOath':
        this.sfx.song('aura', hands);
        this.songFx.aura(feet(u), 4, [feet(u)], SONG.gold);
        await wait(0.6);
        return;
      case 'layOnHands':
        this.sfx.song('heal', hands);
        if (first) this.songFx.heal(feet(first));
        await wait(0.4);
        return;
      case 'silence':
        this.sfx.song('dome', e.tile ? tileToWorld(e.tile) : hands);
        if (e.tile) await this.songFx.dome(tileToWorld(e.tile), ABILITIES.silence.radius ?? 2);
        return;
      case 'huntersMark':
        this.sfx.song('mark', first ? chest(first) : hands);
        if (first) this.songFx.mark(chest(first));
        await wait(0.5);
        return;
      case 'counterSong':
        this.sfx.song('cleanse', first ? chest(first) : hands);
        if (first) await this.songFx.cleanse(feet(first));
        return;
      case 'lastVerse':
        if (first && !first.alive) {
          this.sfx.song('revive', feet(first));
          await this.songFx.revive(feet(first));
        } else {
          this.sfx.song('heal', hands);
          this.songFx.aura(feet(u), ABILITIES.lastVerse.range ?? 6, targets.map(feet), SONG.green);
          await wait(0.6);
        }
        return;
      case 'lightRain':
        // The arrows themselves come with the shots that follow.
        this.sfx.song('voice', hands);
        this.songFx.rise(feet(u), SONG.cyan, 14);
        await wait(0.35);
        return;
      default:
        // Salto de eco, Paso de sombra (the teleport comes next) and anything new.
        this.sfx.song('voice', hands);
        this.songFx.rise(feet(u), SONG.green, 10);
        await wait(0.2);
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
      this.sfx.melee('swing', this.units.chestPos(shooter.id));
      await this.units.lunge(shooter.id, targetPos);
      if (hit) {
        const stun = e.ability === 'stunLance';
        this.fx.slash(chest, stun ? 0x6ad8ff : 0xe8f6ff);
        if (e.ability === 'claw') this.fx.bite(chest, shooterPos);
        this.sfx.melee(stun ? 'stun' : e.ability === 'claw' ? 'claw' : 'sword', chest);
        this.sfx.impact(this.sfx.body(target), chest, e.result === 'crit');
      } else {
        this.showMiss(e, shooter, target, chest);
      }
      await wait(0.25);
      return;
    }

    const stepOut = !sameTile(e.from, shooter.pos);
    if (stepOut) await this.units.slide(shooter.id, tileToWorld(e.from), 0.22, false);
    await this.units.raiseWeapon(shooter.id, targetPos);
    await wait(cam() ? 0.25 : 0.08);

    const heavy = weapon.apCost > 1;
    // Flecha perforante and Lluvia de flechas: crossbow bolts instead of rounds.
    const sung = !!ABILITIES[e.ability]?.song;
    const pellets = weapon.range === 'short' ? 6 : 0;
    const rounds = Math.max(1, weapon.burst);
    for (let i = 0; i < rounds; i++) {
      const from = this.units.muzzlePos(shooter.id);
      this.units.recoil(shooter.id);
      if (sung) this.sfx.song('arrow', from);
      else {
        this.fx.muzzleFlash(from, color);
        this.sfx.shot(shooter, weapon, from);
      }
      if (heavy && cam()) this.rig.shake(0.08, 0.15);
      const last = i === rounds - 1;
      const shots = pellets || 1;
      for (let p = 0; p < shots; p++) {
        const dest = hit ? chest.clone().add(jitter(pellets ? 0.6 : 0.25)) : missPoint(from, chest, i + p, pellets ? 1.6 : 1);
        void (sung ? this.songFx.arrow(from, dest) : this.fx.bullet(from, dest, color, heavy ? 45 : 60, heavy)).then(() => {
          this.fx.sparks(dest, hit ? 0xff7a5a : 0xffd28a, pellets ? 3 : hit ? 8 : 5);
          if (last && p === 0) this.sfx.impact(hit ? this.sfx.body(target) : 'miss', dest, e.result === 'crit');
        });
      }
      await wait(weapon.burst > 3 ? 0.08 : 0.11);
    }
    await wait(heavy ? 0.25 : 0.15);

    if (!hit) {
      this.showMiss(e, shooter, target, chest);
      await wait(0.35);
    }
    // Lower the weapon (and step back into cover) once the outcome has been seen.
    void wait(1.1).then(() => {
      this.units.lowerWeapon(shooter.id);
      if (stepOut) void this.units.slide(shooter.id, tileToWorld(shooter.pos), 0.25);
    });
  }

  /**
   * A miss, with the die (playtest: «he fallado con un 93 %»): short over the
   * target, whole in the log. A player's miss also says what bad luck adds to
   * that player's next shot (aim.ts badLuckAim).
   */
  private showMiss(e: ShotEvent, shooter: Unit, target: Unit, chest: THREE.Vector3): void {
    this.fx.floatText(chest, e.roll ? `FALLO · SACÓ ${e.roll}` : 'FALLO', 'miss');
    this.hud.log(e.roll ? `${shooter.name} falla a ${target.name}: sacó ${e.roll} y necesitaba ${e.chance} o menos` : `${shooter.name} falla a ${target.name} (${e.chance}%)`);
    const owner = shooter.team === 'xcom' && !shooter.controlledBy ? shooter.owner : null;
    if (owner === null) return;
    const bonus = BAD_LUCK_AIM * Math.min(BAD_LUCK_MISSES, (this.view.missStreak?.[owner] ?? 0) + 1);
    const whose = owner === this.mySlot ? 'tu próximo disparo' : `el próximo disparo de ${this.playerName(owner)}`;
    this.hud.log(`Mala racha: ${whose} tendrá +${bonus} de puntería`, PLAYER_COLORS[owner]);
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
    if (e.ability === 'burst') {
      // The Hinchado bursts where it stood (docs/FASE2.md): nothing thrown, a blast of gas and worse.
      if (!this.mapView.isVisible(e.target.x, e.target.y)) return;
      look(to);
      this.fx.floatText(tileToWorld(e.target, 0.4), '¡REVIENTA!', 'callout');
      this.hud.log(`¡${capitalize(anEnemy(u))} revienta!`, '#ff6b5a');
      this.sfx.explosion(to);
      if (cam()) this.rig.shake(0.35, 0.5);
      void this.fx.explosion(to, def.radius ?? 1.5);
      this.fx.burst(to, def.radius ?? 1.5);
      await wait(0.55);
      return;
    }
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
      this.sfx.drone(home);
      await this.fx.droneFlight(home, to.clone().add(new THREE.Vector3(0, 1.4, 0)));
      this.sfx.explosion(to);
      if (cam()) this.rig.shake(0.25, 0.4);
      void this.fx.explosion(to, def.radius ?? 1.5, true);
      void this.fx.droneFlight(to.clone().add(new THREE.Vector3(0, 1.4, 0)), this.units.dronePos(u.id)).then(() => this.units.setDroneAway(u.id, false));
      await wait(0.4);
      return;
    } else if (e.ability === 'missiles') {
      const from = this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.5, 0));
      const spots = [to, ...Array.from({ length: 3 }, () => to.clone().add(new THREE.Vector3((Math.random() - 0.5) * 2, 0, (Math.random() - 0.5) * 2)))];
      this.sfx.missile(from);
      await this.fx.missiles(from, spots);
    } else if (e.ability === 'fireball') {
      // Cóctel molotov: a lobbed burning bottle and a sheet of flame (songFx.ts).
      const hands = this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.3, 0));
      this.fx.floatText(this.units.chestPos(u.id), def.name.toUpperCase(), 'info');
      this.sfx.song('fire', hands);
      await this.songFx.fireball(hands, to, blastRadius(u, e.ability) || 2.5);
      if (cam()) this.rig.shake(0.4, 0.5);
      await wait(0.4);
      return;
    } else if (e.ability === 'launch') {
      await this.units.raiseWeapon(u.id, to);
      const from = this.units.muzzlePos(u.id);
      this.units.recoil(u.id);
      this.fx.muzzleFlash(from, 0xffc080);
      this.sfx.launch(from);
      await this.fx.grenadeArc(from, to);
      this.sfx.land(to);
      this.units.lowerWeapon(u.id);
    } else {
      this.sfx.throw(this.units.chestPos(u.id));
      await this.units.throwMotion(u.id);
      await this.fx.grenadeArc(this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.45, 0)), to);
      this.sfx.land(to);
    }
    if (e.ability === 'smoke') {
      this.sfx.explosion(to, 'smoke');
      this.fx.puffs(to.clone().setY(0.2), 0xd8d6d0, 12, { size: 0.6, spread: 1.2, rise: 0.4, out: 0.8, life: 3, grow: 1.6 });
      await wait(0.3);
      return;
    }
    if (e.ability === 'flashbang') {
      this.sfx.explosion(to, 'flash');
      this.fx.muzzleFlash(to.clone().setY(to.y + 0.6), 0xffffff);
      this.fx.sparks(to.clone().setY(to.y + 0.6), 0xffffff, 24);
      if (cam()) this.rig.shake(0.15, 0.3);
      await wait(0.4);
      return;
    }
    this.sfx.explosion(to);
    if (cam()) this.rig.shake(0.45, 0.6);
    void this.fx.explosion(to, blastRadius(u, e.ability) || 2.5);
    await wait(0.4);
  }

  /**
   * A noise (docs/FASE2.md): a ring of its radius spreading from where it
   * sounded, for the squad's own and for what the squad sees make one (the
   * scream). Enemies out of sight that heard the squad and only now come to
   * look get «Os han oído»: a stamp and a line, never the camera.
   */
  private playNoise(e: Extract<GameEvent, { t: 'noise' }>): void {
    const v = this.view;
    const by = e.by ? getUnit(v, e.by) : undefined;
    const ours = by?.team === 'xcom';
    const scream = !!by && this.lastUsed?.unit === by.id && this.lastUsed.ability === 'mindspin';
    if (ours || this.mapView.isVisible(e.tile.x, e.tile.y)) this.overlays.noise(e.tile, e.radius, ours ? 0xffc93a : scream ? 0xc58cff : 0xff6b5a);
    // Only who starts coming now: one already on its way to an earlier noise was told about then.
    const fresh = e.heard.map((id) => getUnit(v, id)).filter((a): a is Unit => !!a?.alive && !a.heard && !this.visibleAliens.has(a.id));
    if (!fresh.length || !by) return;
    if (!ours) {
      if (scream && this.visibleAliens.has(by.id)) this.hud.log(`El grito se oye a ${e.radius} casillas: ${heardCount(fresh)} ${fresh.length === 1 ? 'acude' : 'acuden'}`, '#e8d8c0');
      return;
    }
    // A throw (Petardos, a grenade) draws them to where it landed, not to the squad.
    const away = distance(by.pos, e.tile) > 1.5;
    const one = fresh.length === 1;
    const text = `${heardCount(fresh)} ${away ? (one ? 'va hacia donde sonó' : 'van hacia donde sonó') : one ? 'viene hacia aquí' : 'vienen hacia aquí'}`;
    this.hud.banner('OS HAN OÍDO', 'warn', capitalize(text));
    this.hud.log(`Os han oído: ${text}`, '#ffc93a');
    this.tips.heard();
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
    // What came home (docs/FASE2.md): only what a soldier got out with, or stood with at the end.
    const haul = broughtBack(s);
    const loot = haulText(haul);
    const success: Record<typeof s.mission.kind, string> = {
      elimination: 'Todos los hostiles abatidos',
      recovery: `${capitalize(this.recoveryItem())} a salvo`,
      sabotage: 'Sirena callada',
      hack: 'Equipo en marcha',
      rescue: vip ? `${vip}, a salvo` : 'VIP extraído',
      scavenge: loot ? SCAVENGE.success : SCAVENGE.emptyHanded,
    };
    const titles: Record<EndReason, string> = {
      objective: success[s.mission.kind],
      wiped: evacuated ? 'La escuadra se retiró sin cumplir el objetivo' : 'Escuadra eliminada',
      timer: 'Se agotó el tiempo',
      abandoned: 'Misión abandonada',
      objectiveLost: vip ? `${vip} ha muerto` : 'El VIP ha muerto',
    };
    // One short line: why it ended. A skirmish has no debrief after, so it adds the count.
    const why = reason ? titles[reason] : '';
    const count = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;
    // A skirmish also says what came home (a campaign's debrief has it, from report.loot): «Botín a salvo: +26 §».
    const found = !s.campaignMission && outcome === 'victory' && loot ? loot.replace(/\.$/, '') : '';
    const head = found && reason === 'objective' && s.mission.kind === 'scavenge' ? `${why}: ${found.replace(/^El botín: /, '')}` : [why, found].filter(Boolean).join(' · ');
    const line = s.campaignMission ? why : [head, count(killed, 'abatido', 'abatidos'), lost ? count(lost, 'baja', 'bajas') : 'sin bajas'].filter(Boolean).join(' · ');
    this.hud.showOutcome(outcome, line, !!s.campaignMission);
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
      // How far the soldier sees it: shorter at night, unless it stands in the light (los.ts sightTo).
      if (selected?.alive && sightOrigin(this.view, selected.pos, u.pos, sightTo(this.view, selected, u.pos))) {
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
    this.songMarks.setSilences((this.view.silences ?? []).map((z) => ({ at: tileToWorld(z.pos), radius: z.radius })));
    this.songMarks.setOaths(this.view.units.filter((u) => u.alive && (u.oath ?? 0) > 0).map((u) => tileToWorld(u.pos)));
    this.drawPlans();
    this.drawDetection();
    this.edge.set(this.marks());
  }

  /**
   * While the squad is concealed and a soldier is selected, every enemy in
   * sight shows its detection radius: how close a soldier can end a move
   * without being noticed (playtest: «no he entendido por qué me han visto»).
   * At night the ring is the one in the dark, half as wide (los.ts
   * detectionTo); a lit tile is noticed from the whole day radius, and the
   * move range frames those tiles in red.
   */
  private drawDetection(): void {
    const v = this.view;
    const show = v.concealed && !v.outcome && !!this.selectedUnit();
    const watchers = show ? v.units.filter((a) => a.alive && a.team === 'alien' && this.visibleAliens.has(a.id) && TEMPLATES[a.template].detection > 0) : [];
    this.overlays.setDetection(watchers.map((a) => ({ pos: a.pos, radius: this.darkDetection(a) })));
  }

  /** How close a soldier out of the light can get to `a` unseen: detectionTo's range on an unlit tile. */
  private darkDetection(a: Unit): number {
    const day = TEMPLATES[a.template].detection;
    return this.view.mission.night ? day * NIGHT.detection : day;
  }

  private marks(): EdgeMark[] {
    const marks = this.missionMarks();
    // Where the Saqueo's next wave comes in, until it does.
    const wave = this.view.mission.reinforcements.find((r) => r.horde && r.pos && !r.landed);
    if (wave) marks.push({ kind: 'alert', label: 'Zombis', pos: wave.pos! });
    return this.moment ? [...marks, { kind: 'alert', label: this.moment.who, pos: this.moment.pos }] : marks;
  }

  /**
   * The camera never follows the partner, but the moments that matter (a
   * contact, a soldier down, concealment broken, the objective) get a notice
   * with «Ver (F)» and an arrow at the screen edge towards them (#36).
   */
  private notePartner(e: GameEvent, by: Slot): void {
    const v = this.view;
    const actor = unitOf(e);
    if (actor && getUnit(v, actor)?.team === 'xcom') this.partnerLast = actor;
    const who = this.playerName(by);
    const at = (id: string | null) => (id ? getUnit(v, id)?.pos : undefined);
    let pos: Vec2 | undefined;
    let text: string;
    switch (e.t) {
      case 'podActivated': {
        const pod = v.pods.find((p) => p.id === e.pod);
        const first = pod?.unitIds.map((id) => getUnit(v, id)).find((m) => m?.alive);
        pos = first?.pos;
        text = `${who}: ¡contacto!`;
        break;
      }
      case 'concealmentBroken': {
        // Where it happened: the enemy that saw the squad, else the soldier who gave it away.
        const spotter = e.by ? getUnit(v, e.by) : undefined;
        pos = spotter?.team === 'alien' ? spotter.pos : at(e.unit ?? this.partnerLast);
        text = `${who} ha roto la ocultación`;
        break;
      }
      case 'died': {
        const u = getUnit(v, e.unit);
        if (u?.team === 'xcom') text = `${u.name} ha caído`;
        else if (u?.objective) text = `${who} ha callado la sirena`;
        else return;
        pos = u.pos;
        break;
      }
      case 'itemPicked':
        pos = at(e.unit);
        text = `${who} ha recuperado los datos`;
        break;
      case 'hacked':
        if (!e.success) return;
        pos = at(e.unit);
        text = `${who}: equipo en marcha`;
        break;
      default:
        return;
    }
    if (!pos) return;
    this.moment = { pos: { ...pos }, who };
    this.hud.notice(text, PLAYER_COLORS[by], () => this.seeMoment());
    window.clearTimeout(this.momentTimer);
    this.momentTimer = window.setTimeout(() => this.clearMoment(), 8000);
    this.edge.set(this.marks());
  }

  /** Why the squad was found, in plain words: who saw whom and from how far, or what gave it away. */
  private exposureText(e: Extract<GameEvent, { t: 'concealmentBroken' }>): string {
    const v = this.view;
    const soldier = e.unit ? getUnit(v, e.unit) : undefined;
    const alien = e.by ? getUnit(v, e.by) : undefined;
    const who = soldier?.name ?? 'un soldado';
    const range = e.range ? ` (${e.range} casillas)` : '';
    switch (e.reason) {
      case 'spotted': {
        // From the fog: the squad couldn't have seen it coming, and should know.
        const unseen = alien && !this.visibleAliens.has(alien.id) ? ' que no veíais' : '';
        return `${capitalize(anEnemy(alien))}${unseen} ha visto a ${who}: entró en su radio de detección${range}.`;
      }
      case 'patrol':
        return `${capitalize(anEnemy(alien))} de patrulla se ha acercado hasta tener a ${who} en su radio de detección${range}.`;
      case 'attack':
        return `${capitalize(who)} ha atacado${alien ? ` a ${anEnemy(alien)}` : ''}: el combate os delata.`;
      case 'explosive':
        return `La explosión de ${who} se ha oído en toda la zona.`;
      case 'song':
        return `${capitalize(who)} ha hecho ruido: se oye desde lejos.`;
      case 'alarm':
        return `${soldier ? `${who} no ha podido arreglarlo` : 'El arreglo ha fallado'}: ha saltado la alarma.`;
      case 'reinforcements':
        return 'Los refuerzos llegan alertados: saben dónde estáis.';
      default:
        return 'El enemigo sabe dónde estáis.';
    }
  }

  /** Who found whom in a contact: they saw the squad, the squad came upon them, they were attacked or heard it. */
  private contactCause(e: Extract<GameEvent, { t: 'podActivated' }>): string {
    const soldier = e.unit ? getUnit(this.view, e.unit) : undefined;
    const who = soldier?.name ?? 'la escuadra';
    switch (e.cause) {
      case 'sighted':
        // The concealment that just broke over this very sighting: they saw the squad first.
        if (this.exposure?.reason === 'spotted' && this.exposure.by === e.by && this.exposure.unit === e.unit) return `Os han visto: han descubierto a ${who}`;
        return `${capitalize(who)} los ha avistado`;
      case 'patrol':
        return `Una patrulla ha dado con ${who}`;
      case 'attacked':
        return soldier ? `Responden al ataque de ${who}` : 'Responden al ataque';
      case 'noise':
        return 'Han oído la explosión';
      default:
        return 'Os han localizado';
    }
  }

  private seeMoment(): void {
    if (this.moment) this.lookAt(this.moment.pos);
    this.clearMoment();
  }

  private clearMoment(): void {
    window.clearTimeout(this.momentTimer);
    this.moment = null;
    this.hud.notice(null);
    this.edge.set(this.marks());
  }

  /**
   * Where the squad has to go next: the objective while it is pending (the
   * item on the ground, the relay, the terminal, the captive), otherwise the
   * evacuation zone if the mission has one.
   */
  private missionMarks(): EdgeMark[] {
    const s = this.view;
    const m = s.mission;
    let goal: Vec2 | null = null;
    if (!m.objectiveDone) {
      if (m.kind === 'recovery' && m.item && !m.item.carrier && !m.item.evacuated) goal = m.item.pos;
      if (m.kind === 'hack') goal = m.terminal;
      if (m.kind === 'sabotage' || m.kind === 'rescue') {
        const target = m.objectiveUnit ? getUnit(s, m.objectiveUnit) : undefined;
        if (target?.alive && (m.kind === 'sabotage' || target.captive)) goal = target.pos;
      }
    }
    if (goal) return [{ kind: 'objective', label: 'Objetivo', pos: goal }];
    if (!m.evacZone.length) return [];
    const cx = m.evacZone.reduce((sum, t) => sum + t.x, 0) / m.evacZone.length;
    const cy = m.evacZone.reduce((sum, t) => sum + t.y, 0) / m.evacZone.length;
    const centre = m.evacZone.reduce((best, t) => (distance(t, { x: cx, y: cy }) < distance(best, { x: cx, y: cy }) ? t : best));
    return [{ kind: 'evac', label: 'Evacuación', pos: centre }];
  }

  private showEnemyFile(u: Unit): void {
    const file = ENEMY_FILES[u.template];
    if (!file) return;
    const t = TEMPLATES[u.template];
    const stats = [`Salud ${u.hp}/${u.maxHp}`, u.armor ? `Blindaje ${u.armor}` : '', `Puntería ${t.aim}`, t.defense ? `Defensa ${t.defense}` : ''];
    // Its own name: one of ours, risen (docs/FASE3.md), keeps it («Zombi · Clara Bosch»).
    this.hud.enemyFile(u.name, stats.filter(Boolean).join(' · '), file);
  }

  /** The player asked to look somewhere (O, or an edge arrow): it ends any camera pass. */
  private lookAt(pos: Vec2): void {
    this.rig.takeControl();
    this.rig.focus(tileToWorld(pos));
  }

  // ============================================================ interaction

  // ================================================== planning without the command

  /** Plans a move for `u` (or withdraws it, clicking the same tile again); the partner sees it. */
  private togglePlan(u: Unit, tile: Vec2): void {
    const current = this.plans.get(u.id);
    const withdraw = !!current && sameTile(current.tile, tile);
    if (withdraw) this.plans.delete(u.id);
    else this.plans.set(u.id, { slot: this.mySlot, tile });
    this.net.send({ t: 'plan', unit: u.id, tile: withdraw ? null : tile });
    this.drawPlans();
    this.hud.toast(withdraw ? 'Plan retirado.' : `Plan para ${u.name}: lo ve ${this.playerName(this.state.command)}. Otro clic ahí lo quita.`);
  }

  /** Draws every plan along the path it would take now; a plan that no longer fits is dropped. */
  private drawPlans(): void {
    const list: { from: Vec2; path: Vec2[]; color: string }[] = [];
    for (const [id, plan] of this.plans) {
      const u = getUnit(this.state, id);
      const reach = u?.alive ? computeReach(this.state, u, maxMoveCost(u)) : null;
      if (!u || !reach?.cost.has(plan.tile.y * this.state.width + plan.tile.x)) {
        this.plans.delete(id);
        continue;
      }
      const path = buildPath(this.state, u, reach, plan.tile);
      if (path?.length) list.push({ from: u.pos, path, color: PLAYER_COLORS[plan.slot] });
    }
    this.overlays.setPlans(list);
  }

  /** Why an order cannot go out yet: the partner holds the command. */
  private commandLock(): string {
    return `Tiene el mando ${this.playerName(this.state.command)}: puedes apuntar y planear, pero no dar la orden.`;
  }

  /** The confirm button's text while planning (null with the command). */
  private lockLabel(): string | undefined {
    return this.hasCommand() ? undefined : `MANDO: ${this.playerName(this.state.command).toUpperCase()}`;
  }

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
      // The rules' own range (rules.ts noticeRange): halved at night, unless the tile is lit.
      detected: watchers.some((a) => sightOrigin(this.state, a.pos, d.pos, detectionTo(this.state, a, d.pos)) !== null),
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
    if (this.searching()) {
      this.cycleSearch(dir);
      return;
    }
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
    if (this.mode.kind === 'self') {
      this.confirmSelf();
      return;
    }
    const u = this.selectedUnit();
    if (!u || this.mode.kind !== 'target' || !this.targetId) return;
    // Without the command, aiming only shows the odds.
    if (!this.hasCommand()) {
      this.hud.toast(this.commandLock());
      return;
    }
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
        // Registrar is armed like a self ability (first press), on the furniture beside the soldier.
        if (id === 'search') {
          if (this.mode.kind === 'tile' && this.mode.ability === id) this.confirmSearch();
          else this.armSearch();
          return;
        }
        this.mode = this.mode.kind === 'tile' && this.mode.ability === id ? { kind: 'move' } : { kind: 'tile', ability: id };
        this.refreshInteractive();
        return;
      case 'self':
        // Nothing fires on the first press (playtest: «sin arriesgarme a clicar y que se activen»):
        // it is armed and explained; the second press, Enter or the panel's button uses it.
        if (this.mode.kind === 'self' && this.mode.ability === id) this.confirmSelf();
        else {
          this.mode = { kind: 'self', ability: id };
          this.targetId = null;
          this.concealWarn = null;
          this.refreshInteractive();
        }
    }
  }

  // ============================================================ Registrar

  private searching(): boolean {
    return this.mode.kind === 'tile' && this.mode.ability === 'search';
  }

  /** Furniture the selected soldier can search from where it stands. */
  private searchTiles(): Vec2[] {
    const u = this.selectedUnit();
    return u ? searchableTiles(this.state, u) : [];
  }

  /** The furniture Registrar would search now: the one picked (pointer, Tab or click), else the first within reach. */
  private searchTarget(): Vec2 | null {
    const tiles = this.searchTiles();
    const pick = this.searchPick;
    return (pick && tiles.find((t) => sameTile(t, pick))) ?? tiles[0] ?? null;
  }

  /** Arms Registrar (on `tile`, if given): the furniture gets marked and the panel says which one it searches. */
  private armSearch(tile?: Vec2): void {
    this.mode = { kind: 'tile', ability: 'search' };
    this.targetId = null;
    this.concealWarn = null;
    this.searchPick = tile ?? this.searchTarget();
    this.refreshInteractive();
  }

  /** Tab with Registrar armed: the next piece of furniture within reach. */
  private cycleSearch(dir: 1 | -1): void {
    const tiles = this.searchTiles();
    if (tiles.length < 2) return;
    const current = this.searchTarget();
    const i = tiles.findIndex((t) => current && sameTile(t, current));
    this.searchPick = tiles[(i + dir + tiles.length) % tiles.length]!;
    this.refreshInteractive();
  }

  private confirmSearch(tile = this.searchTarget()): void {
    const u = this.selectedUnit();
    if (!u || !this.searching() || !this.canAct()) return;
    if (!this.hasCommand()) {
      this.hud.toast(this.commandLock());
      return;
    }
    if (!tile) {
      this.hud.toast('No hay nada que registrar al lado.');
      return;
    }
    this.send({ type: 'ability', unit: u.id, ability: 'search', tile });
    this.cancelMode();
  }

  /** The armed Registrar's panel: «Registrar el maletero», what it costs and sounds like, the room left. */
  private searchPanel(u: Unit): TargetView | null {
    const tile = this.searchTarget();
    const decor = tile ? searchSpot(this.state, tile) : null;
    if (!tile || !decor) return null;
    const def = ABILITIES.search;
    const name = SEARCH_SPOTS[decor]?.name ?? 'el mueble';
    const room = BACKPACK_SLOTS - (u.backpack?.length ?? 0);
    const others = this.searchTiles().length - 1;
    return {
      kind: 'self',
      title: `${def.name} ${name}`,
      action: def.name,
      locked: this.lockLabel(),
      cost: costText(u, 'search'),
      noise: noiseText(this.state, u, 'search'),
      effect: def.description,
      detail: `Mochila: ${room === 1 ? 'queda 1 hueco' : `quedan ${room} huecos`} de ${BACKPACK_SLOTS}`,
      hint: `Otra vez, Espacio o clic en el mueble: registrar${others ? ` · Tab: ${others === 1 ? 'el otro' : 'otro'} mueble` : ''} · Esc: cancelar`,
    };
  }

  /** Uses the armed self ability. */
  private confirmSelf(): void {
    const u = this.selectedUnit();
    if (!u || this.mode.kind !== 'self') return;
    if (!this.hasCommand()) {
      this.hud.toast(this.commandLock());
      return;
    }
    this.send({ type: 'ability', unit: u.id, ability: this.mode.ability });
    this.cancelMode();
  }

  /**
   * Space and Enter use what is armed (playtest: Vigilancia armed, and Space
   * aimed a shot instead): a self ability goes, an aimed one fires at its
   * target, a throw lands where the cursor is. With nothing armed, Space aims
   * the first usable attack.
   */
  private confirmOrAttack(): void {
    switch (this.mode.kind) {
      case 'self':
        this.confirmSelf();
        return;
      case 'target':
        this.confirmTarget();
        return;
      case 'tile':
        if (this.searching()) this.confirmSearch();
        else if (this.hoverTile) this.throwAt(this.hoverTile);
        else this.hud.toast('Señala con el ratón dónde lanzarla.');
        return;
    }
    const views = this.abilities();
    const usable = PRIMARY_ATTACKS.map((id) => views.find((v) => v.id === id)).find((v) => v?.enabled);
    if (usable) this.useAbility(usable.id);
    else this.hud.toast(views.find((v) => v.id === 'shoot')?.reason ?? 'No puede atacar ahora.');
  }

  private abilityList(u: Unit): AbilityId[] {
    const m = this.state.mission;
    return abilitiesOf(u).filter(
      (id) => (id !== 'evac' || m.evacZone.length > 0) && (id !== 'hack' || (!!m.terminal && !m.objectiveDone)) && (id !== 'search' || this.hasSpots),
    );
  }

  private abilities(): AbilityView[] {
    const u = this.selectedUnit();
    if (!u || !this.isMine(u)) return [];
    return this.abilityList(u).map((id, i) => {
      const def = ABILITIES[id];
      const charge = def.charge ? u.charges[def.charge] ?? 0 : undefined;
      let reason: string | null = !this.canAct()
        ? 'No es vuestro turno.'
        : abilityBlocker(this.state, u, id);
      if (!reason && (def.target === 'enemy' || def.target === 'melee' || def.target === 'ally') && !this.targetOptions(id).length) {
        reason = def.target === 'ally' ? 'No hay aliados válidos.' : def.target === 'melee' ? 'Ningún enemigo a su alcance.' : 'Sin enemigos a la vista.';
      }
      const active = this.mode.kind !== 'move' && this.mode.ability === id;
      const cooldown = u.cooldowns[id] ?? 0;
      return {
        id,
        label: id === 'hack' ? `${def.name} · ${hackChance(this.state, u)}%` : def.name,
        hotkey: i < 9 ? (id === 'shoot' ? `${i + 1} · Esp` : String(i + 1)) : i === 9 ? '0' : '',
        enabled: !reason,
        reason: reason ?? undefined,
        name: def.name,
        cost: costText(u, id),
        limits: limitsText(u, id),
        damage: damageText(this.state, u, id),
        noise: noiseText(this.state, u, id),
        description: def.description,
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
    if (this.mode.kind === 'tile' || this.mode.kind === 'self') {
      const u = this.selectedUnit();
      if (!u || !this.canAct() || abilityBlocker(this.state, u, this.mode.ability)) this.cancelModeSilently();
    }
    this.drawDetection();
    // Without the command the range is still shown, fainter: it is for planning (#35).
    const r = this.mode.kind === 'move' && !this.isBusy(this.selected) ? this.reach() : null;
    this.overlays.showMoveRange(r?.tiles ?? [], !this.hasCommand());
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
    if (line) {
      this.hud.log(`Morse · ${line}`, '#1d4fb0');
      this.tips.story(line);
    }
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
    let objectiveTip: string | undefined;
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
        objective = m.objectiveDone ? `Sirena callada: ${after}` : 'Callad la sirena';
        break;
      case 'hack':
        objective = m.objectiveDone ? `Equipo en marcha: ${after}` : 'Arreglad el equipo (junto a él, o con el Grillo del especialista)';
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
      case 'scavenge': {
        // What the squad carries counts once it is out (or standing at the end); a fallen soldier's is lost.
        const n = s.units.filter((u) => u.team === 'xcom' && (u.alive || u.evacuated)).reduce((sum, u) => sum + (u.backpack?.length ?? 0), 0);
        objective = n ? SCAVENGE.carrying.replace('{n}', String(n)) : SCAVENGE.objective;
        if (n) objectiveTip = SCAVENGE.carryingTip.replace('{n}', String(n));
        break;
      }
    }
    const incoming = m.reinforcements.filter((r) => r.pos && !r.landed);
    const alert = !incoming.length ? null : incoming.every((r) => r.horde) ? 'ZOMBIS EN CAMINO' : 'REFUERZOS EN CAMINO';
    return { kind: m.kind, objective, objectiveTip, turnsLeft: m.turnsLeft, alert, area: s.mapName, horde: m.horde?.length ? this.hordeView() : null };
  }

  /**
   * The Saqueo's horde clock (HORDE in content.ts, Sim.callHorde): every squad
   * turn and every noise winds it up, and a wave is called at the end of the
   * alien turn once it reaches the next `wave` points. The bar is how far it
   * is from the next one; full, the wave is called this very turn.
   */
  private hordeView(): HordeView {
    const s = this.view;
    const m = s.mission;
    const sent = m.reinforcements.filter((r) => r.horde).length;
    const points = s.turn * HORDE.perTurn + (m.noise ?? 0);
    const left = (sent + 1) * HORDE.wave - points;
    const fill = Math.max(0, Math.min(1, 1 - left / HORDE.wave));
    const turns = Math.ceil(left / HORDE.perTurn);
    const title =
      left <= 0
        ? 'Horda: la próxima oleada sale al final de este turno'
        : turns <= 1
          ? 'Horda: la próxima oleada sale al final del próximo turno, o de este si hacéis ruido'
          : `Horda: la próxima oleada, en unos ${turns} turnos; antes cuanto más ruido hagáis`;
    return { fill, title };
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
    if (panel?.kind === 'attack') this.tips.shotPanel();

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
    const sharing = !!this.partnerHere() && v.activeTeam === 'xcom' && !v.outcome;
    const holder = v.command === undefined || !this.players.some((p) => p.slot === v.command && p.connected) ? this.mySlot : v.command;
    // Whose turn it is, unmistakably (playtest): mine, the partner's while they hold the command, or the enemy's.
    const owner: TurnOwner | null = v.outcome
      ? null
      : v.activeTeam === 'alien'
        ? { kind: 'enemy' }
        : sharing && holder !== this.mySlot
          ? { kind: 'partner', name: this.playerName(holder), color: PLAYER_COLORS[holder] }
          : { kind: 'mine', color: PLAYER_COLORS[this.mySlot] };
    this.hud.setTurn(v.turn, owner, v.concealed, v.mission.night ? { label: NIGHT_LABEL, rules: NIGHT_RULES } : null);
    this.hud.setMission(this.missionView());
    this.hud.setPlayers(this.players, v.ready, this.mySlot, busySlots(v), sharing ? holder : null);
    this.hud.setSquad(
      v.units.filter((u) => u.team === 'xcom' || u.controlledBy),
      this.mySlot,
      this.selected,
      { 0: PLAYER_COLORS[0], 1: PLAYER_COLORS[1] },
      v.mission.item?.carrier ?? null,
    );
    const shown = this.selected ? getUnit(v, this.selected) : undefined;
    this.hud.setSelected(shown?.alive ? shown : null, shown?.alive ? this.packView(shown) : null);
    this.tips.state(v, this.visibleAliens, sharing);
  }

  /** The soldier's backpack and the squad's haul for the sheet: where there is anything to search, or it carries something. */
  private packView(u: Unit): PackView | null {
    const v = this.view;
    if (TEMPLATES[u.template].team !== 'xcom' || u.template === 'vip' || (!this.hasSpots && !u.backpack?.length)) return null;
    const squad = lootTotals(v.units.filter((o) => o.team === 'xcom' && (o.alive || o.evacuated)).flatMap((o) => o.backpack ?? []));
    return {
      slots: packSlots(u.backpack ?? []),
      size: BACKPACK_SLOTS,
      squad: {
        supplies: squad.supplies,
        items: squad.items.length,
        medicines: squad.medicines,
        food: squad.food,
        parts: squad.parts,
        weapons: squad.weapons.length,
        text: haulText(squad) ?? 'La escuadra aún no lleva nada.',
      },
    };
  }

  /** Builds the target panel and draws the aim line for the current target. */
  private targetPanel(): TargetView | null {
    const u = this.selectedUnit();
    if (u && this.searching()) {
      this.overlays.showAim(null, null);
      return this.searchPanel(u);
    }
    if (u && this.mode.kind === 'self') {
      const id = this.mode.ability;
      const def = ABILITIES[id];
      this.overlays.showAim(null, null);
      return { kind: 'self', title: def.name, action: def.name, locked: this.lockLabel(), cost: costText(u, id), limits: limitsText(u, id), effect: def.description, detail: this.selfDetail(u, id), noise: noiseText(this.state, u, id) };
    }
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
      // Bengala and the Grillo's protocol never miss: the panel is all about their damage.
      const dmg = friendly ? null : abilityDamage(this.state, u, ability, target);
      const effects: Partial<Record<AbilityId, string>> = {
        medkit: `Cura +${Math.min(MEDKIT_HEAL + u.mods.heal, target.maxHp - target.hp)} (salud ${target.hp}/${target.maxHp})`,
        aid: `+${20 + u.mods.aid} de defensa hasta vuestro próximo turno`,
        revival: 'Quita el aturdimiento, la desorientación y el pánico',
        suppression: '−50 de puntería al objetivo hasta tu próximo turno; si se mueve, le disparas',
        combatProtocol: 'Nunca falla',
        bolt: 'Nunca falla e ignora la cobertura',
      };
      const damage = dmg ? { hit: span(dmg.hit), notes: armorNotes(dmg, target), kill: lethality(dmg, target) } : undefined;
      const noise = noiseOf(this.state, u, ability);
      return { kind: 'support', title: def.name, action: def.name, locked: this.lockLabel(), targetName: target.name, effect: effects[ability] ?? def.description, damage, index, total: list.length, noise: noise ? `Ruido ${noise}` : undefined };
    }
    const attack = ability as AttackAbility;
    const shot = previewShot(this.state, u, target, { ability: attack, from: option.tile });
    if (!shot) {
      this.overlays.showAim(null, null);
      return null;
    }
    this.overlays.showAim(shot.origin, target.pos);
    const dmg = abilityDamage(this.state, u, attack, target, shot)!;
    const loud = noiseOf(this.state, u, attack);
    return {
      kind: 'attack',
      title: `${def.name} · ${weaponLabel(u, attack)}`,
      action: def.name,
      targetName: target.name,
      hit: shot.hit,
      crit: Math.min(shot.crit, shot.hit),
      graze: shot.graze,
      mods: shot.mods,
      critMods: shot.critMods,
      damage: {
        hit: span(dmg.hit),
        crit: dmg.crit && shot.crit > 0 ? span(dmg.crit) : undefined,
        graze: dmg.graze && shot.graze > 0 ? span(dmg.graze) : undefined,
        notes: armorNotes(dmg, target),
        kill: lethality(shot.crit > 0 ? dmg : { ...dmg, crit: null }, target),
      },
      index,
      total: list.length,
      note: ATTACK_NOTES[attack],
      locked: this.lockLabel(),
      noise: loud ? `Ruido ${loud}` : 'Sin ruido',
    };
  }

  /** What an armed self ability would do right now: whom it reaches, the odds, the clip. */
  private selfDetail(u: Unit, id: AbilityId): string | undefined {
    const s = this.state;
    const enemies = (n: number) => (n ? `${n} ${n === 1 ? 'enemigo' : 'enemigos'} a su alcance` : 'Ningún enemigo a su alcance');
    switch (id) {
      case 'faceoff':
        return enemies(rangedTargets(s, u, 'faceoff').filter((t) => this.visibleAliens.has(t.id)).length);
      case 'lightRain':
      case 'velmarStorm':
        return enemies(songTargets(s, u, id).filter((t) => this.visibleAliens.has(t.id)).length);
      case 'hack':
        return `Probabilidad: ${hackChance(s, u)} %`;
      case 'reload':
        return `Cargador: ${u.ammo}/${attackWeapon(u, 'shoot').clip}`;
    }
    return undefined;
  }

  private updateHover(): void {
    const tile = this.hoverTile;
    const u = this.selectedUnit();
    this.overlays.showPath(null, null);
    this.overlays.showCover(null);
    this.overlays.showBlast(null, [], false);
    this.overlays.showRange(null);
    this.overlays.setSpots([]);
    this.overlays.setNote(null);
    this.overlays.setHover(tile);
    const hovered = tile ? unitAt(this.view, tile) : undefined;
    this.units.setHovered(hovered && (hovered.team === 'xcom' || this.visibleAliens.has(hovered.id)) ? hovered.id : null);
    this.units.setTags(this.enemyTags(u, tile));
    if (!u || !this.canAct() || this.isBusy(u.id)) return;

    // Melee: show the run to the strike position.
    const option = this.currentTarget();
    if (option?.path?.length) this.overlays.showPath(u.pos, option.path);
    // Aiming at night from a lit tile (where it strikes from, or where it stands): seen from far.
    if (option) {
      const from = option.tile ?? u.pos;
      const lit = this.litNote(from);
      if (lit) this.overlays.setNote(from, lit);
    }

    if (this.searching()) {
      // The furniture within reach is marked; the pointer on one picks it, and the panel follows.
      const pick = this.searchPick;
      if (tile && this.searchTiles().some((t) => sameTile(t, tile)) && !(pick && sameTile(pick, tile))) {
        this.searchPick = tile;
        this.hud.setTarget(this.targetPanel());
      }
      this.overlays.setSpots(this.spotsAround(u, this.searchTarget()));
      return;
    }
    if (this.mode.kind === 'tile') {
      const ability = this.mode.ability;
      this.overlays.showRange(u.pos, abilityRange(u, ability));
      if (!tile) return;
      const center = throwTile(u, ability, tile);
      const area = blastTiles(this.state, ability, center, u);
      const allies = area.filter((t) => unitAt(this.state, t)?.team === 'xcom');
      this.overlays.showBlast(center, area, canTargetTile(this.state, u, ability, center), allies);
      this.overlays.setHover(null);
      const loud = noiseOf(this.state, u, ability);
      // A jump lands the soldier there: at night, whether it is in the light.
      const note = [loud ? `Ruido ${loud}` : '', ABILITIES[ability].teleport ? this.litNote(center) : ''].filter(Boolean).join(' · ');
      if (note) this.overlays.setNote(center, note);
      return;
    }
    if (this.mode.kind === 'self') {
      // A song or Restoration reaches the allies around: the circle says how far.
      const range = ABILITIES[this.mode.ability].range;
      if (range && this.mode.ability !== 'hack') this.overlays.showRange(u.pos, range);
      return;
    }
    if (!tile) return;
    if (this.mode.kind !== 'move') return;

    const occupant = unitAt(this.state, tile);
    if (occupant?.team === 'alien' && this.visibleAliens.has(occupant.id)) {
      this.overlays.setHover(tile, 0xff5a5a);
      return;
    }
    // Only furniture the squad has seen: the fog keeps what is in an unexplored house.
    const decor = this.hasSpots && this.mapView.isExplored(tile.x, tile.y) ? searchSpot(this.state, tile) : null;
    if (decor) {
      this.hoverFurniture(u, tile, decor);
      return;
    }
    const r = this.reach();
    const dest = r?.tiles.find((t) => sameTile(t.pos, tile));
    if (!r || !dest) return;
    const path = buildPath(this.state, u, r.reach, tile);
    this.overlays.showPath(u.pos, path, dest.dash);
    this.overlays.showCover(tile, coverSides(this.state, tile));
    this.overlays.setHover(tile, dest.detected ? 0xff4a4a : dest.dash ? 0xffc93a : 0x9fe8ff);
    // Running, a door or a window is heard (docs/FASE2.md): said under the destination.
    // And at night, whether it ends in the light.
    const note = [moveNoise(this.state, path ?? [], dest.dash), this.litNote(tile)].filter(Boolean).join(' · ');
    if (note) this.overlays.setNote(tile, note);
    if (dest.detected) this.tips.redTile();
  }

  /** At night a tile in the light is seen from as far as by day (los.ts sightTo, detectionTo): the previews say so. */
  private litNote(tile: Vec2): string {
    return this.state.mission.night && isLit(this.state, tile) ? 'A la luz: os ven de lejos' : '';
  }

  /** The furniture beside `u` that Registrar reaches: to search, the one it would search, or searched already. */
  private spotsAround(u: Unit, pick: Vec2 | null): { pos: Vec2; kind: SpotKind }[] {
    return DIRS8.map((d) => ({ x: u.pos.x + d.x, y: u.pos.y + d.y }))
      .filter((p) => searchSpot(this.state, p) !== null && reachesToSearch(this.state, u.pos, p))
      .map((p) => ({ pos: p, kind: wasSearched(this.state, p) ? 'done' : pick && sameTile(pick, p) ? 'pick' : 'open' }));
  }

  /** The pointer on a piece of furniture: marked, named, and whether it can be searched (a click arms Registrar). */
  private hoverFurniture(u: Unit, tile: Vec2, decor: DecorKind): void {
    const name = capitalize(SEARCH_SPOTS[decor]?.name ?? 'el mueble');
    if (wasSearched(this.state, tile)) {
      this.overlays.setSpots([{ pos: tile, kind: 'done' }]);
      this.overlays.setNote(tile, `${name} · ya se ha registrado`);
      return;
    }
    const near = this.searchTiles().some((t) => sameTile(t, tile));
    const blocked = near ? abilityBlocker(this.state, u, 'search') : null;
    this.overlays.setSpots([{ pos: tile, kind: near && !blocked ? 'pick' : 'open' }]);
    this.overlays.setNote(tile, !near ? `${name} · se puede registrar` : blocked ? `${name} · ${blocked.replace(/\.$/, '').toLowerCase()}` : `${name} · clic: registrar`);
  }

  /**
   * Hit chances over the enemies' flags, as in XCOM 2. Over an enemy: the
   * chance to hit it from where the soldier stands. Over a destination: every
   * known enemy the soldier would see from there, yellow if flanked, with the
   * chance when there would still be an action left to shoot. Only to look:
   * it needs no command, so the partner who waits can plan too.
   */
  private enemyTags(u: Unit | undefined, tile: Vec2 | null): Map<string, EnemyTag> {
    const tags = new Map<string, EnemyTag>();
    if (u && tile && this.mode.kind === 'tile' && !this.searching() && this.canAct() && this.isMine(u) && !this.isBusy(u.id)) return this.blastTags(u, this.mode.ability, tile);
    if (!u || !tile || this.mode.kind !== 'move' || !this.canAct() || !this.isMine(u) || this.isBusy(u.id)) return tags;
    if (!abilitiesOf(u).includes('shoot')) return tags;
    const occupant = unitAt(this.state, tile);
    if (occupant) {
      if (occupant.team !== 'alien' || !this.visibleAliens.has(occupant.id)) return tags;
      const shot = previewShot(this.state, u, occupant, { ability: 'shoot' });
      tags.set(
        occupant.id,
        shot
          ? { kind: shot.flanked ? 'flank' : 'aim', text: `${shot.hit} % · crít. ${Math.min(shot.crit, shot.hit)} %` }
          : { kind: 'none', text: 'Sin línea de tiro' },
      );
      return tags;
    }
    const dest = this.reach()?.tiles.find((t) => sameTile(t.pos, tile));
    if (!dest) return tags;
    const canShoot = !dest.dash && u.ap - 1 >= apNeeded(u, 'shoot');
    for (const a of this.state.units) {
      if (!a.alive || a.team !== 'alien' || !this.visibleAliens.has(a.id)) continue;
      const shot = previewShot(this.state, u, a, { ability: 'shoot', from: tile });
      if (!shot) continue;
      const chance = canShoot ? `${shot.hit} %` : '';
      tags.set(a.id, shot.flanked ? { kind: 'flank', text: chance ? `Flanco · ${chance}` : 'Flanco' } : { kind: 'sight', text: chance || 'A la vista' });
    }
    return tags;
  }

  /**
   * Aiming a grenade or a fireball: what it would take off everyone caught,
   * over their flags (playtest: «no sé dónde está el daño que hago»). Enemies
   * in pen, with whether it kills them; our own in warning yellow.
   */
  private blastTags(u: Unit, ability: AbilityId, tile: Vec2): Map<string, EnemyTag> {
    const tags = new Map<string, EnemyTag>();
    const center = throwTile(u, ability, tile);
    if (!canTargetTile(this.state, u, ability, center)) return tags;
    // Controlled detonation leaves the grenadier's squad out of the blast.
    const grenade = ability === 'grenade' || ability === 'launch';
    const spared = (t: Unit) => grenade && hasPerk(u, 'safeBlast') && t.team === u.team;
    for (const p of blastTiles(this.state, ability, center, u)) {
      const t = unitAt(this.state, p);
      if (!t || spared(t) || (t.team === 'alien' && !this.visibleAliens.has(t.id))) continue;
      const d = abilityDamage(this.state, u, ability, t);
      if (!d) return tags;
      const text = `Daño ${span(d.hit)}`;
      if (t.team === 'xcom') tags.set(t.id, { kind: 'flank', text: `¡Aliado! ${text}` });
      else tags.set(t.id, { kind: 'aim', text: [text, lethality(d, t)].filter(Boolean).join(' · ') });
    }
    return tags;
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
        if (!this.drag.moved) {
          // A right click (not a drag) on a known enemy opens its file; elsewhere it backs out of aiming.
          const enemy = this.mode.kind === 'move' && this.hoverTile ? unitAt(this.view, this.hoverTile) : undefined;
          if (enemy?.team === 'alien' && this.visibleAliens.has(enemy.id)) this.showEnemyFile(enemy);
          else this.cancelMode();
        }
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
          if (this.mode.kind === 'target' || this.searching()) this.cycleTarget(e.shiftKey ? -1 : 1);
          else this.cycleUnit(e.shiftKey ? -1 : 1);
          break;
        case 'Enter':
        case 'NumpadEnter':
        case 'Space':
          // The menu and the modals keep their own buttons.
          if (this.hud.covered) break;
          // A card or button still focused from a click mustn't take the key too: it would go twice.
          e.preventDefault();
          (document.activeElement as HTMLElement | null)?.blur?.();
          this.confirmOrAttack();
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
          // With a partner's moment on screen, F goes there; otherwise to the soldier.
          if (this.moment) {
            this.seeMoment();
            break;
          }
          const u = this.selectedUnit();
          if (u) this.rig.focus(tileToWorld(u.pos));
          break;
        }
        case 'KeyO': {
          const marks = this.missionMarks();
          if (!marks.length) this.hud.toast('Esta misión no tiene un punto al que ir: eliminad a los hostiles.');
          else this.lookAt(marks[this.markCycle++ % marks.length]!.pos);
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

  /** Throws (or casts) the area ability armed at `tile`, or as far toward it as it reaches. */
  private throwAt(tile: Vec2): void {
    const u = this.selectedUnit();
    if (!u || this.mode.kind !== 'tile' || !this.canAct()) return;
    if (!this.hasCommand()) {
      this.hud.toast(this.commandLock());
      return;
    }
    const center = throwTile(u, this.mode.ability, tile);
    if (canTargetTile(this.state, u, this.mode.ability, center)) {
      this.send({ type: 'ability', unit: u.id, ability: this.mode.ability, tile: center });
      this.cancelMode();
    } else {
      this.hud.toast('La escuadra no ve esa zona.');
    }
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
    // Without the command a click plans instead of ordering (#35): aiming and areas only preview.
    const planning = !this.hasCommand();

    // Registrar armed: a click on marked furniture searches it, anywhere else backs out.
    if (this.searching()) {
      if (this.searchTiles().some((t) => sameTile(t, tile))) this.confirmSearch(tile);
      else this.cancelMode();
      return;
    }
    if (this.mode.kind === 'tile') {
      this.throwAt(tile);
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
    // A click on the ground backs out of aiming, or of an armed self ability.
    if (this.mode.kind === 'target' || this.mode.kind === 'self') {
      this.cancelMode();
      return;
    }
    // Furniture beside the soldier: the click arms Registrar on it (the next click, or Space, searches).
    if (this.searchTiles().some((t) => sameTile(t, tile))) {
      const view = this.abilities().find((a) => a.id === 'search');
      if (view?.enabled) this.armSearch(tile);
      else if (view?.reason) this.hud.toast(view.reason);
      return;
    }
    const dest = this.reach()?.tiles.find((t) => sameTile(t.pos, tile));
    if (dest) {
      const cost = this.reachCache!.reach.cost.get(tile.y * this.state.width + tile.x)!;
      if (apForCost(u, cost) > u.ap) return;
      if (planning) {
        this.togglePlan(u, tile);
        return;
      }
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
