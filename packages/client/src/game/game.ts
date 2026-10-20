import * as THREE from 'three';
import { post } from '../report';
import { snowyMission } from './snow';
import {
  ABILITIES,
  abilitiesOf,
  abilityBlocker,
  abilityDamage,
  abilityTargets,
  apForCost,
  apNeeded,
  applyEvent,
  RT_STEP,
  RT_TICK,
  attackWeapon,
  abilityRange,
  BACKPACK_SLOTS,
  BAD_LUCK_AIM,
  BAD_LUCK_MISSES,
  boxTitle,
  blastRadius,
  blastTiles,
  breachedWalls,
  breachLine,
  breaksFloor,
  broughtBack,
  buildPath,
  canTargetTile,
  cloneState,
  commandsFor,
  computeReach,
  costPerAp,
  coverAgainst,
  coverSides,
  destinations,
  detectionTo,
  DIRS8,
  doorTiles,
  ENEMY_FILES,
  DEFAULT_ITEM,
  fallLine,
  CURED_FLOAT,
  findLabel,
  firesSidearm,
  FOUND_WEAPONS,
  apPerTurn,
  displayName,
  BOARD,
  VETERANCY,
  WEAPONS,
  weaponName,
  type CampaignState,
  lootDroppedLine,
  lootGivenLine,
  lootName,
  fallenPackLine,
  fallenPackMark,
  fallenPackTakenLine,
  lootTakenLine,
  medicineLine,
  packFullLine,
  weaponEquippedLine,
  itemDroppedFloat,
  itemDroppedLine,
  itemObjective,
  alarmDoneLine,
  alarmObjective,
  itemPartnerLine,
  itemTag,
  itemTakenFloat,
  itemTakenLine,
  distance,
  endsTurn,
  EVAC_TEXT,
  BITE_FLOAT,
  BITE_NO_BOARD,
  biteLossLine,
  biteNote,
  ABANDON_TEXT,
  ITEM_FINDS,
  ITEMS,
  abandonAsk,
  abandonBarNotice,
  abandonRefusedLine,
  EVAC_BAR,
  evacAsk,
  evacBarNotice,
  evacClock,
  evacDepartLine,
  evacPrompt,
  evacRefusedLine,
  evacWaiting,
  evacWins,
  proposalClock,
  getUnit,
  hackChance,
  hasAnyCover,
  hasPerk,
  HORDE,
  breakableTip,
  GIVE_WAY_FLOAT,
  breachMark,
  giveWayLine,
  siegeObjective,
  waveDueLine,
  waveDueShort,
  waveEntersLine,
  posOf,
  SIEGE,
  SIEGE_TEXT,
  BELT_TEXT,
  beltLine,
  planBlockedLine,
  planFollowedLine,
  TIME_TEXT,
  turnsLeftWarning,
  siegeObjectiveTip,
  inEvacZone,
  inMeleeReach,
  isAttack,
  isLit,
  leftBehindLine,
  lootSummary,
  lootTotals,
  maxMoveCost,
  meleeTargets,
  MISSION_NAMES,
  NIGHT,
  NIGHT_LABEL,
  NIGHT_TEXT,
  LIVING_TIP,
  litNote,
  NOISE,
  noiseOf,
  OBJECTIVE_DONE,
  PLAYER_COLORS,
  previewShot,
  rangedTargets,
  reachesToSearch,
  roomAt,
  roomSearchLine,
  barLine,
  barFillFloat,
  nodeOf,
  heightAt,
  kindAt,
  toPos,
  canStandOn,
  SCAVENGE,
  SEARCH_SPOTS,
  searchableTiles,
  RUBBLE_TEXT,
  samePos,
  searchLine,
  searchSpot,
  inViewCone,
  sightOf,
  sightOrigin,
  sightTo,
  STORY,
  teamVisibility,
  TEMPLATES,
  unitAt,
  vipObjective,
  visibleEnemies,
  wasSearched,
  type AbilityId,
  type AttackAbility,
  type Command,
  type DamagePreview,
  type DamageRange,
  type DecorKind,
  type EndReason,
  type EvacSituation,
  type GameEvent,
  type GameState,
  type Loot,
  type Place,
  type Pos,
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
import { deadlineLeft } from '../ui/deadline';
import { clear } from '../ui/dom';
import { Hud, isMenu, type AbilityView, type BarActionId, type HordeView, type MenuId, type ItemOrder, type MissionView, type PackSlot, type PackView, type TargetView, type TurnOwner } from '../ui/hud';
import { CameraRig, isTyping } from './camera';
import { setTerrain, STOREY, tileToWorld, worldToTile } from './coords';
import { Fx } from './fx';
import { piecesLoaded } from './buildings';
import { MapView } from './mapView';
import { playBounds } from './outline';
import { Overlays, type MoveTile, type SpotKind } from './overlays';
import { cycleTileLook } from './tileLooks';
import { Renderer } from './renderer';
import { Sfx } from './sfx';
import { syntyPropsLoaded } from './syntyProps';
import { setCombatRate, setHurry, timeRate, wait } from './tween';
import { UnitViews, type EnemyTag } from './unitViews';
import { EdgeMarks, type EdgeMark } from './edgeMarks';
import { playArrival } from './arrivals';
import { planHasted, type Walker, type WalkPlan } from './walkPlan';
import { graphics } from '../settings';
import { soldierPortrait } from '../strategy/portraits';
import { skillsOf, treePanel } from '../strategy/skillSheet';

/** `self`: a self ability pressed once, waiting for the second press (or Enter) that uses it. */
type Mode = { kind: 'move' } | { kind: 'target'; ability: AbilityId } | { kind: 'tile'; ability: AbilityId } | { kind: 'self'; ability: AbilityId };

interface QueuedEvent {
  e: GameEvent;
  by: Slot | null;
  /** Camera epoch when the batch arrived; own-action cinematics stop once the player takes the camera. */
  epoch: number;
}

/** A selectable target for the current ability. */
/** What an enemy sees (Overlays.setVision): its tiles in its cone and line of sight, and those it would notice a hidden soldier on. */
interface Vision {
  pos: Place;
  facing?: number;
  range: number;
  seen: Place[];
  noticed: Place[];
}

interface TargetOption {
  unit: Unit;
  /** Melee: the tile it strikes from, where it stands (a blade never runs to its target, Arturo 16/10 #38). */
  tile?: Vec2;
}

type ShotEvent = Extract<GameEvent, { t: 'shot' }>;

/** Pause after the last event of one of our own actions before handing control back. */
const HOLD_AFTER: Partial<Record<GameEvent['t'], number>> = {
  // docs/UX-RITMO.md: the number stays a second, a miss most of one.
  damaged: 1,
  died: 0.6,
  shot: 0.8,
  stunned: 0.6,
  tileChanged: 0.6,
  explosive: 0.6,
  hacked: 0.8,
  vipRescued: 0.5,
  suppressed: 0.6,
  cleansed: 0.4,
  refund: 0.4,
  abilityUsed: 0.3,
  healed: 0.5,
  aided: 0.5,
  evacuated: 0.4,
  searched: 0.7,
  infected: 0.75,
  moved: 0.25,
};

/**
 * Night missions (docs/ALTURA-Y-NOCHE.md §2), as the HUD explains them (Lore's
 * NIGHT_TEXT.rules; playtest 17/10 #50: «la luz no está muy explicada»): in the
 * «De noche» tag's tooltip, the log, the first night's card and the controls.
 */
const NIGHT_RULES = NIGHT_TEXT.rules.join('\n');
/** Drawn back this far (CameraRig.zoomFraction), the wheel shows the top storey with its roofs; this close, the floor of the soldier looked at (#1). */
const ROOFS_FAR = 0.72;
const FLOOR_NEAR = 0.35;
/** How long after the player last moved the camera the turn's changes leave it alone (#47: «que nunca pelee con el jugador»). */
const TURN_CAMERA_QUIET_MS = 4000;
/** The first night is explained once, on a card (per browser). */
const NIGHT_SEEN_KEY = 'bastion.seenNight';

/**
 * The loading veil (UX-REVISION-3 #1) waits at most this long for the map's
 * models: a partner downloading them slowly gets the battle as it is, and
 * the map fills in as they arrive. Then at most this long for the shaders.
 */
const VEIL_MODELS_MS = 15000;
const VEIL_SHADERS_MS = 4000;

/** An animation that throws is skipped and the turn goes on; the host's red.log hears of it, from the partner too. */
function animFailed(err: unknown): void {
  console.error('[anim]', err);
  post(`anim: ${err instanceof Error ? `${err.message} · ${err.stack?.split('\n')[1]?.trim() ?? ''}` : String(err)}`);
}

/** The server's reconnection grace (room.ts RECONNECT_GRACE_MS): a partner who dropped this recently still counts as here. */
const RECONNECT_GRACE_MS = 30_000;
/** Its longer grace for a partner whose window is minimized or whose laptop sleeps (room.ts AWAY_GRACE_MS). */
const AWAY_GRACE_MS = 5 * 60_000;

/**
 * The enemy turn's pace (docs/UX-RITMO.md; Arturo, 14/10: «el turno de la IA va
 * muy rápido»), in seconds at «Normal» (Ajustes' «Velocidad del combate» and the
 * space bar scale them, with everything else, through tween.ts). Before an
 * enemy (or a group of the dead) acts in sight, the camera gets there and stays
 * still SETTLE; the result of an attack stays READ_HIT (READ_MISS for a miss)
 * before anything else; between one enemy and the next, at least BETWEEN.
 */
const SETTLE = 0.35;
const READ_HIT = 0.9;
const READ_MISS = 0.7;
const BETWEEN = 0.5;
/** Within this of where the camera looks, the next enemy needs no camera move: it never goes back and forth. */
const NEAR_ENOUGH = 5;
/** The log's colour for enemies fighting each other (18/10 #47): neither ours nor against us. */
const FEUD_COLOR = '#e8d8c0';
/** Real time: the events with nothing to play (the clock, what each one does and is told: the HUD reads them). */
const RT_QUIET: ReadonlySet<GameEvent['t']> = new Set<GameEvent['t']>(['rtClock', 'rtDoing', 'rtOrder', 'rtPaused', 'moved']);
/** Real time: what a soldier is doing, on its card (provisional, for Lore). */
const RT_TEXT = {
  idle: 'Sin órdenes',
  walk: 'Andando',
  aim: 'Apuntando',
  reload: 'Recargando',
  wait: 'Esperando',
  waitWhy: 'No tiene línea de tiro, o alguien le tapa el paso.',
};
/**
 * The dead with nobody in reach walk this many times quicker than their gait
 * (playtest 16/10 #6: «que anden más rápido, que es muy lento»), all at once,
 * each one's two actions as one stretch. With Arte's gaits of 16/10 (a fifth
 * quicker), a common zombie's walk comes to 0.66 s a tile (it was 1.02).
 */
const WALKERS_PACE = 1.25;
/** A shooter stepping out onto a tile someone (or something) takes leans this far towards it instead (tiles). */
const LEAN_OUT = 0.35;
/** After the player takes the camera, the enemy turn leaves it to them this long (ms): it never fights them. */
const PLAYER_CAMERA_MS = 2500;
/**
 * The bar's menus (docs/FASE10.md §4): their members in order, and which of
 * them to offer. Investigar offers the room, and the box whenever there is one
 * beside (`crate`), off with its reason when it can't be opened now (playtest
 * 16/10 #13: it only showed when it could).
 */
const MENUS: { id: MenuId; name: string; icon: AbilityId; members: AbilityId[]; shows: (v: AbilityView, crate: boolean) => boolean }[] = [
  { id: 'menu:watch', name: 'Vigilancia', icon: 'overwatch', members: ['overwatch', 'overwatchMelee'], shows: () => true },
  { id: 'menu:throw', name: 'Lanzar', icon: 'stone', members: ['stone', 'grenade', 'smoke', 'flashbang'], shows: () => true },
  { id: 'menu:investigate', name: 'Investigar', icon: 'searchRoom', members: ['searchRoom', 'search'], shows: (v, crate) => v.id === 'searchRoom' || crate },
];

/** How a menu's choices read inside it (docs/FASE10.md §4): what the menu already says is left out. */
const MENU_LABELS: Partial<Record<AbilityId, string>> = {
  overwatch: 'A distancia',
  overwatchMelee: 'Cuerpo a cuerpo',
  stone: 'Piedra',
  grenade: 'Granada',
  smoke: 'Humo',
  flashbang: 'Aturdidora',
  searchRoom: 'Habitación',
  search: 'Caja',
};

/** What follows an enemy's action and belongs to it: it doesn't end the action, nor start the next. */
const FOLLOW_UP: ReadonlySet<GameEvent['t']> = new Set<GameEvent['t']>(['damaged', 'died', 'infected', 'stunned', 'disoriented', 'noise', 'tileChanged', 'floorBroken', 'fell', 'itemDropped']);

/** Done by hand on a tile beside the soldier: Registrar, and opening or closing a door (playtest 13/10 #2). */
type HandAbility = 'search' | 'openDoor' | 'closeDoor';
const HAND: ReadonlySet<AbilityId> = new Set<AbilityId>(['search', 'openDoor', 'closeDoor']);

/** What attack variants do besides the odds shown in the panel. */
const ATTACK_NOTES: Partial<Record<AttackAbility, string>> = {
  rapidFire: 'Dos disparos seguidos',
  chainShot: 'Si acierta, dispara otra vez',
  aimedShot: '+50 % de daño',
  rupture: '+3 de daño; el objetivo recibirá +3 de todos los ataques',
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
function throwTile(u: Unit, ability: AbilityId, tile: Place): Place {
  const range = abilityRange(u, ability);
  const far = distance(u.pos, tile);
  if (far <= range) return tile;
  for (let k = range / far; k > 0; k -= 0.01) {
    // On the floor that was aimed at (docs/FASE6.md).
    const p = { x: Math.round(u.pos.x + (tile.x - u.pos.x) * k), y: Math.round(u.pos.y + (tile.y - u.pos.y) * k), z: tile.z };
    if (distance(u.pos, p) <= range) return p;
  }
  return u.pos;
}

/** Same tile on the same level (docs/FASE6.md): a place without `z` is on the ground. */
function samePlace(a: Place, b: Place): boolean {
  return a.x === b.x && a.y === b.y && (a.z ?? 0) === (b.z ?? 0);
}

/** The highest storey of the map anyone can stand on (docs/FASE6.md): 0 without storeys. */
function topLevel(s: GameState): number {
  const levels = s.levels ?? [];
  for (let k = levels.length; k >= 1; k--) if (levels[k - 1]!.tiles.some((t) => t !== null && canStandOn(t))) return k;
  return 0;
}

/** "las cintas" → "Las cintas". */
function capitalize(text: string): string {
  return text[0]!.toUpperCase() + text.slice(1);
}

/**
 * What using an ability costs, from the rules the server charges by (with the
 * weapon it carries: the precision rifle's two), in one wording on every
 * button (playtest 17/10 #52): «1 acción», «2 acciones», «Sin acción»,
 * «Acaba el turno».
 */
function costText(u: Unit, id: AbilityId): string {
  const need = apNeeded(u, id);
  if (need === 0) return 'Sin acción';
  if (endsTurn(u, id)) return 'Acaba el turno';
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
  grenade: ' en el área',
  launch: ' en el área',
  missiles: ' en el área',
};

/** The hover card's damage line, from the rules' own numbers: «Daño 4–6 · crítico +2». */
function damageText(s: GameState, u: Unit, id: AbilityId): string | undefined {
  const value = damageValue(s, u, id);
  return value ? `Daño ${value}` : undefined;
}

/** The damage alone, for the cards' figures (#18): «4–6 por disparo · crítico +2». */
function damageValue(s: GameState, u: Unit, id: AbilityId): string | undefined {
  // Vigilancia fires the rifle at whoever moves: a reaction shot, which only crits with the talent.
  const watch = id === 'overwatch' || id === 'killZone';
  const d = abilityDamage(s, u, watch ? 'shoot' : id);
  if (!d) return undefined;
  const crit = d.crit && (!watch || hasPerk(u, 'guardCrit')) ? d.crit : null;
  return [`${span(d.hit)}${watch ? ' por disparo' : DAMAGE_EACH[id] ?? ''}`, crit ? critText(d.hit, crit) : '', d.shred ? `destroza ${d.shred} de blindaje` : '']
    .filter(Boolean)
    .join(' · ');
}

/** Where a weapon hits best, by its range (content.ts RangeTable), for the cards. */
const RANGE_NAMES: Record<string, string> = { short: 'corto', medium: 'medio', long: 'largo', none: 'cuerpo a cuerpo' };

/**
 * An ability's figures on its card (playtest 19/10 #18: «siempre igual: la misma forma, el mismo color,
 * alineado igual»), each a name and its value, in this order and only those that apply: actions, reach,
 * damage, noise, uses, recharge.
 */
function abilityStats(s: GameState, u: Unit, id: AbilityId): [string, string][] {
  const def = ABILITIES[id];
  const need = apNeeded(u, id);
  const watch = id === 'overwatch' || id === 'killZone';
  const weapon = isAttack(id) ? attackWeapon(u, id) : watch ? attackWeapon(u, 'shoot') : null;
  const reach = weapon ? RANGE_NAMES[weapon.range] : abilityRange(u, id) ? `${abilityRange(u, id)} casillas` : '';
  const noise = watch ? attackWeapon(u, 'shoot').noise : noiseOf(s, u, id);
  const left = u.cooldowns[id] ?? 0;
  const stats: [string, string | number | undefined][] = [
    ['Acciones', need === 0 ? 'ninguna' : endsTurn(u, id) ? 'acaba el turno' : need],
    ['Alcance', reach],
    ['Daño', damageValue(s, u, id)],
    // In tiles, as on the weapons' sheet (Lore, 19/10).
    ['Ruido', noise > 0 ? `${noise} casillas${watch ? ' al disparar' : ''}` : isAttack(id) ? 'ninguno' : undefined],
    ['Usos', def.charge ? (u.charges[def.charge] ?? 0) : undefined],
    // Ready, how often; waiting, when (Lore: not to mix them up).
    ['Recarga', left > 0 ? `lista en ${left} ${left === 1 ? 'turno' : 'turnos'}` : def.cooldown ? `cada ${def.cooldown} turnos` : undefined],
  ];
  return stats.filter((x): x is [string, string | number] => x[1] !== undefined && x[1] !== '').map(([k, v]) => [k, String(v)]);
}

/**
 * How far using it is heard, for the cards (docs/FASE2.md): «ruido 12», or
 * «sin ruido» for a silent attack. Vigilancia is heard when it fires.
 */
function noiseText(s: GameState, u: Unit, id: AbilityId): string | undefined {
  if (id === 'overwatch' || id === 'killZone') {
    const n = attackWeapon(u, 'shoot').noise;
    return n ? `ruido ${n} al disparar` : undefined;
  }
  const n = noiseOf(s, u, id);
  if (n > 0) return `ruido ${n}`;
  return isAttack(id) ? 'sin ruido' : undefined;
}

/** What a move sounds like (NOISE in content.ts): running, a door, a window's glass. Null when it is quiet. */
function moveNoise(s: GameState, path: Place[], dash: boolean): string | null {
  const parts: [number, string][] = [];
  // Each step on its own floor (docs/FASE6.md).
  const kinds = path.map((p) => kindAt(s, toPos(p)));
  if (dash) parts.push([NOISE.dash, 'corre']);
  if (kinds.includes('window')) parts.push([NOISE.breach, 'rompe un cristal']);
  if (kinds.includes('door')) parts.push([NOISE.door, 'abre una puerta']);
  if (!parts.length) return null;
  parts.sort((a, b) => b[0] - a[0]);
  return `Ruido ${parts[0]![0]}: ${parts.map((p) => p[1]).join(', ')}`;
}

/** What a squad carries or brought home, in Lore's words (`lootSummary`): «El botín: +34 §, un botiquín y 3 raciones.», or null. */
function haulText(t: LootTotals): string | null {
  return lootSummary(t.supplies, t.items, t.medicines, t.food, t.parts, t.weapons);
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

/** What a blast does to each one caught in it, played together (Game.playBlast). */
const BLAST_RESULTS = new Set<GameEvent['t']>(['damaged', 'died', 'infected', 'stunned']);

/**
 * Whether `a` shoots: an attack on an enemy with a weapon that isn't for close quarters. Only then
 * does cover count against it: a bite or a blow ignores cover and never flanks (aim.ts), and most
 * enemies now are the dead (docs/ZOMBIS-PRIMERO.md). The Chillón screams and the Hinchado bursts.
 */
function shootsFromRange(a: Unit): boolean {
  return abilitiesOf(a).some((id) => ABILITIES[id].target === 'enemy' && !!ABILITIES[id].weapon && !attackWeapon(a, id as AttackAbility).melee);
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

/** A siege's door or barricade at `p` (docs/FASE12.md §10): which it is, and what it holds whole. */
function breakableOf(s: GameState, p: Vec2 & { z?: number }): { kind: 'door' | 'barricade'; full: number } {
  const k = kindAt(s, { x: p.x, y: p.y, z: p.z ?? 0 });
  return k === 'door' || k === 'doorOpen' ? { kind: 'door', full: SIEGE.door } : { kind: 'barricade', full: SIEGE.barricade };
}

/** The side of the base a tile on its wall stands on (DIRS4: 0 north, 1 east, 2 south, 3 west): the nearest edge of the map. */
function sideOf(s: GameState, p: Vec2): number {
  const gaps = [p.y, s.width - 1 - p.x, s.height - 1 - p.y, p.x];
  return gaps.indexOf(Math.min(...gaps));
}

/** A siege's waves in order, each once (one comes in groups that share its turn): its turn and whether it is in. */
function siegeWaves(s: GameState): { turn: number; landed: boolean }[] {
  const waves = new Map<number, { turn: number; landed: boolean }>();
  for (const r of s.mission.reinforcements) {
    const w = waves.get(r.turn) ?? { turn: r.turn, landed: true };
    w.landed &&= r.landed;
    waves.set(r.turn, w);
  }
  return [...waves.values()].sort((a, b) => a.turn - b.turn);
}

/** «3 zombis», «un enemigo»: the dead by their name, the living (or a mix) as enemies. */
function heardCount(units: Unit[]): string {
  const dead = units.every((u) => TEMPLATES[u.template].undead);
  if (units.length === 1) return dead ? 'un zombi' : 'un enemigo';
  return `${units.length} ${dead ? 'zombis' : 'enemigos'}`;
}

/** Whether what `d` takes off kills `t`: surely, or only with a critical. */
function lethality(d: DamagePreview, t: Unit): string | undefined {
  // Not «Puede matarlo» (playtest 19/10 #20): only what is sure, or what a critical does.
  if (d.hit.min >= t.hp) return 'Lo mata';
  if (d.hit.max < t.hp && d.crit && d.crit.max >= t.hp) return 'Un crítico lo mata';
  return undefined;
}

/** What armour does to a hit on `t`: soaks, is pierced, is stripped. */
function armorNotes(d: DamagePreview, t: Unit): string[] {
  const notes: string[] = [];
  if (d.armor) notes.push(`Blindaje ${d.armor}: −${d.armor} por impacto`);
  if (t.armor > d.armor) notes.push(`Perfora ${t.armor - d.armor} de blindaje`);
  if (d.shred && t.armor) notes.push(`Destroza ${Math.min(d.shred, t.armor)} de blindaje`);
  if (t.ruptured) notes.push(`Ruptura: +${t.ruptured} a cada impacto`);
  return notes;
}

/** Enemies whose name is feminine: «una Alarma». */
const FEMININE: Partial<Record<TemplateId, true>> = { relay: true };

/** "Saqueador" → "un Saqueador", "Alarma" → "una Alarma". */
function anEnemy(u: Unit | undefined): string {
  return u ? `${FEMININE[u.template] ? 'una' : 'un'} ${TEMPLATES[u.template].name}` : 'un enemigo';
}

/** A pod woken by the other enemies (the dead against the living, 18/10 #47): attacked, and not by the squad. */
function wokenByFoes(e: Extract<GameEvent, { t: 'podActivated' }>): boolean {
  return e.cause === 'attacked' && !e.unit;
}

function unitOf(e: GameEvent): string | undefined {
  return 'unit' in e ? (e.unit ?? undefined) : undefined;
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
  /** One of ours that has just walked into La Mula: once the queue has played, leave or wait (P1). */
  /** The squad's turn has just begun: a `damaged` on a bitten soldier now is the bite's toll (#13). */
  private biteTick = false;
  /** «Evacuar» pressed once, for this soldier: the second press asks (playtest 15/10 #10). */
  private evacArmed: string | null = null;
  private evacArmTimer = 0;
  /** A fallen soldier's rucksack being taken (takeAll): who takes, and what is left to take. */
  private packQueue: { unit: string; ids: string[] } | null = null;
  /** A swap under way (docs/FASE8.md §C): once the slot is dropped, this is picked up from the floor. */
  private swapTake: { unit: string; id: string } | null = null;
  /** The next `healed` is a medicine's: its line already says how much. */
  private medicineHeal = false;
  /** The next `cured` is a medicine's: its line already says so. */
  private medicineCure = false;
  /** When each player's connection dropped (setPlayers): within the server's grace they still count as here. */
  private readonly droppedAt = new Map<Slot, number>();
  /** The partner gone whose soldiers this player commands too (commandChanged), by name. */
  private commandingFor: string | null = null;
  /** How the squad lost its concealment, once it has: the contact that follows says who saw whom. */
  private exposure: Extract<GameEvent, { t: 'concealmentBroken' }> | null = null;
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
  /** The pause owed once our own action has played (HOLD_AFTER of its last event that isn't its noise). */
  private holdAfter = 0.2;
  private visibleAliens = new Set<string>();
  private lastShot: { target: string; result: ShotResult } | null = null;
  /** Who made the last attack or blast played: whether a hurt enemy was hurt by another (18/10 #47). */
  private lastAttacker: string | null = null;
  /** Last ability activated, so its follow-up events can be staged (e.g. Restoration's heals). */
  private lastUsed: { unit: string; ability: AbilityId } | null = null;

  private selected: string | null = null;
  /** The command and the soldier in play as autoSelect last saw them: a change picks the one in play (#9). */
  private lastCommand: Slot | undefined;
  private lastInPlay: string | undefined;
  private mode: Mode = { kind: 'move' };
  private targetId: string | null = null;
  private hoverTile: Pos | null = null;
  /**
   * The active floor (docs/FASE6.md): the cursor picks on it and below it,
   * the range and the previews are drawn on it, and the buildings are cut
   * away above it. It follows the selected soldier; PageUp/PageDown, the
   * HUD's arrows or Shift and the wheel change it.
   */
  private level = 0;
  private maxLevel = 0;
  /** Soldier and level the floor last followed (`id@z`): a change sets the floor again. */
  private levelFor: string | null = null;
  /** When the wheel last changed the floor. */
  private levelWheel = 0;
  /** Drawn back past the top storey with the wheel: the buildings keep their roofs (wheelLevel, buildings.ts Focus.roofs). */
  private roofs = false;
  /** A floor picked by hand (keys, Shift+wheel, the HUD's arrows): the wheel leaves it until it turns again (wheelLevel). */
  private levelByHand = false;
  private reachCache: { key: string; reach: Reach; tiles: MoveTile[] } | null = null;
  /** The last enemy's field of view worked out (visionOf), until it or the state moves. */
  private visionCache: { key: string; v: Vision } | null = null;
  /** The campaign, for a soldier's board on its sheet (setCampaign). */
  private campaign: CampaignState | null = null;
  /** The command has just come to this player: once the events are seen, the planned routes are walked (followPlan). */
  private planDue = false;
  /**
   * Walking one's plans with the command (playtest 18/10 #24): one after another, each move seen whole, until
   * none is left that can go. `planSent`, the one last ordered (if it is still there next time, the engine
   * refused it); `planSkip`, those already said not to reach (said once, kept, passed over).
   */
  private following = false;
  private planSent: string | null = null;
  private readonly planSkip = new Set<string>();
  /** La Lata left by itself as time ran out (playtest 18/10 #8): the outcome says so. */
  private departedOnTime = false;
  /** Mayús held over the map: a click signals (playtest 19/10 #14), and the cursor says so. */
  private signalKey = false;
  /** Real time (docs/TIEMPO-REAL.md): the host's clock as last heard, and when (performance.now), for the HUD. */
  private rtHeard = { clock: 0, at: 0 };
  /** Real time: when (mission clock) each unit started what it does now, for its card's bar. */
  private readonly rtSince = new Map<string, number>();
  /** Real time: the picked soldier's ways out, for the pointer's route (rtHover), until it moves or the state does. */
  private rtReach: { key: string; reach: ReturnType<typeof computeReach> } | null = null;
  /** Real time: the way each of one's own goes, worked out once per where it stands and where it goes. */
  private readonly rtRoutes = new Map<string, { key: string; steps: Place[] }>();
  /** A soldier with no actions left that the player picked to look at (select, autoSelect). */
  private looking: string | null = null;
  /** The enemy whose field of view is shown (under the pointer, or aimed at), and whether a dead one was turning last frame. */
  private watched: string | null = null;
  private wasTurning = false;
  /** Moves planned without the command, by unit: mine and the partner's (UX-REVISION-2 #35). */
  private plans = new Map<string, { slot: Slot; tile: Place }>();
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
  /** With Registrar or a door armed, the tile it would act on (Tab or a click picks another). */
  private handPick: Vec2 | null = null;
  /** A repair just failed: the horde that follows comes for the alarm, and says so. */
  private alarmRaised = false;
  /** A bitten soldier of ours has risen in this mission already: the stamp only comes the first time. */
  private risenSeen = false;
  /** The enemy last shown acting in this enemy turn (a unit id, or `walkers` for the dead walking in one run): the next one is shown anew. */
  private lastActor: string | null = null;
  /** The result of the enemy attack just played, still to be read (seconds), before anything new starts. */
  private resultHold = 0;
  /** A group of the dead is walking together: no single walker takes the camera. */
  private grouped = false;
  /** While a group of the dead walks (playGroup): when each of them stands and where it swerves, so none walks through another. */
  private groupPlans: Map<string, WalkPlan> | null = null;
  /** The mission's item just went down with a broken floor: its own drop line would say it twice. */
  private itemFell = false;
  /** The framing of the last group of the dead shown walking: who was in it and what it took in. */
  private groupFrame: { ids: Set<string>; centre: THREE.Vector3; radius: number } | null = null;
  /** Resolves when the loading veil lifts (UX-REVISION-3 #1); null when it is down. */
  private veiled: Promise<void> | null = null;
  private gone = false;

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
    this.renderer.scene.add(this.mapView.group, this.units.group, this.fx.group);
    // Units on a storey the cutaway hides lose their body (their flag stays, with the floor: unitViews).
    this.units.setCutAway((p) => this.mapView.isCutAway(p));
    // `level`: the active floor, for the buildings' cutaway.
    this.mapView.watch(this.renderer.camera, () => {
      const focus = { selected: this.selected, hover: this.hoverTile, target: this.targetId, level: this.level, roofs: this.roofs && this.level === this.maxLevel };
      return focus;
    });
    this.hud = new Hud(this.root, {
      onAbility: (id) => this.useAbility(id),
      onEndTurn: () => this.toggleReady(),
      onSelectUnit: (id) => this.select(id, true),
      onSheet: (id) => this.openSheet(id),
      onConfirmTarget: () => this.confirmTarget(),
      onCancel: () => this.cancelMode(),
      onCycleTarget: (dir) => this.cycleTarget(dir),
      onNewMission: (kind?: MissionKind, map?: SkirmishMap) => this.net.send({ t: 'newMission', mission: kind, map }),
      onReturnToBase: () => this.net.send({ t: 'returnToBase' }),
      onBackToMenu: () => this.net.send({ t: 'backToMenu' }),
      onAbandon: () => this.net.send({ t: 'abandon' }),
      abandonState: () => ({ partner: this.onlinePartner()?.name ?? null, waiting: this.state.abandonCall?.by === this.mySlot }),
      leaveState: () => ({ partner: this.onlinePartner()?.name ?? null, campaign: !!this.state.campaignMission }),
      onPassCommand: () => this.passCommand(),
      onWatchPartner: () => this.watchPartner(),
      onGoToSoldier: () => this.goToSoldier(),
      onMenu: () => this.hud.setAbilities(this.abilities()),
      onItem: (o) => this.itemOrder(o),
      onTakeAll: (ids) => this.takeAll(ids),
      onSwap: (take, drop) => this.swapItem(take, drop),
      onToggleSound: () => !this.sfx.toggleMute(),
      onFloor: (dir) => this.stepLevel(dir),
      soundOn: () => !this.sfx.muted,
    });
    // Above the 3D labels, under the HUD panels.
    this.root.insertBefore(this.edge.root, this.hud.root);

    this.disposers.push(this.renderer.onFrame((dt, time) => this.frame(dt, time)));
    // «Velocidad del combate» (docs/UX-RITMO.md), live; the base and the menus always play at normal speed.
    this.disposers.push(() => (setCombatRate(1), setHurry(false)));
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
    // The battle's speed (tween.ts timeRate): the bodies follow it in unitViews; the effects here, and the camera while
    // events play, so it arrives in step with them (the player's own panning keeps its speed).
    const rate = timeRate();
    this.rig.update(this.playing ? dt * rate : dt);
    this.units.tick(dt, time);
    this.fx.tick(dt * rate);
    this.overlays?.tick(time);
    if (this.overlays && this.view) this.followTurns();
    this.mapView.tick(dt);
    if (this.view?.realtime) this.hud.tickRealtime(this.rtNow());
    const from = this.selected ? getUnit(this.view, this.selected) : undefined;
    this.edge.update(this.renderer.camera, from?.alive ? from.pos : null);
  }

  // ================================================================ network

  loadSnapshot(s: GameState): void {
    // A battle built from nothing, or a new mission on the same screen, goes under the loading veil.
    const fresh = !this.view || s.seq === 0;
    if (fresh) this.departedOnTime = false;
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
      this.overlays.setPlanColor(PLAYER_COLORS[this.mySlot]);
      this.overlays.lift = (p) => this.units.stairLift(p);
      this.renderer.scene.add(this.overlays.group);
    }
    this.overlays.setNight(!!s.mission.night);
    this.renderer.fitMap(s.width, s.height);
    this.rig.setBounds(s.width, s.height, playBounds(s));
    setTerrain(this.view);
    this.maxLevel = topLevel(s);
    this.level = 0;
    this.levelFor = null;
    this.hud.setFloor(this.maxLevel ? { level: 0, max: this.maxLevel } : null);
    this.hasSpots = false;
    for (let i = 0; i < s.width * s.height && !this.hasSpots; i++) this.hasSpots = searchSpot(s, { x: i % s.width, y: Math.floor(i / s.width) }) !== null;
    this.mapView.build(this.view);
    // What the carrier carries is named (playtest 14/10 #2: it said «DATOS»).
    this.hud.itemTag = this.units.itemTag = itemTag(this.recoveryItem());
    // Night missions (docs/ALTURA-Y-NOCHE.md): the sky and the sun; a day mission after a night one turns them back.
    this.renderer.setNight(!!this.view.mission.night, snowyMission(this.view));
    this.units.build(this.view);
    if (!s.outcome) this.sfx.start(s.biome, !s.concealed);
    this.sfx.siren(this.view);
    // What a bite does here (playtest 15/10 #13): it weakens every turn, and without a Cuarentena it can't board.
    this.hud.setBiteNote(biteNote(s.mission.quarantine !== false));
    this.selected = null;
    this.mode = { kind: 'move' };
    this.showOutcome();
    this.refreshView();
    this.autoSelect();
    const focus = this.selected ? getUnit(this.state, this.selected) : this.state.units.find((u) => u.team === 'xcom');
    if (focus) this.rig.focus(tileToWorld(focus.pos), true);
    this.refreshInteractive();
    // Playing alone, with the partner's soldiers on the field: said from the start.
    this.commandChanged();
    if (fresh) this.raiseVeil();
  }

  /** The mission's name on its card and veil: the operation's, «Asedio» (docs/FASE12.md), or what kind of fight it is. */
  private missionName(): string {
    const s = this.state;
    return s.campaignMission?.name ?? (s.mission.siege ? SIEGE_TEXT.name : MISSION_NAMES[s.mission.kind]);
  }

  /** The briefing's kicker: the operation's, the story chapter's, or what kind of fight it is. */
  private kicker(): string {
    const cm = this.state.campaignMission;
    const chapter = cm?.story;
    if (chapter === 1) return '';
    return cm?.kicker ?? (chapter ? `DIARIO · HOJA ${chapter}` : cm ? 'OPERACIÓN EN CURSO' : 'ESCARAMUZA');
  }

  /**
   * UX-REVISION-3 #1: the battle has just been built, and its first frame
   * still has to compile every shader (850 ms on the Mac); a partner may be
   * downloading the models too. The veil stays up until the models are in,
   * the shaders compiled and two frames drawn; the briefing waits for it.
   */
  private raiseVeil(): void {
    const s = this.state;
    const lift = this.hud.veil(this.kicker(), this.missionName(), s.mapName);
    const timer = (ms: number) => new Promise<void>((done) => window.setTimeout(done, ms));
    const frames = (n: number) =>
      new Promise<void>((done) => {
        const off = this.renderer.onFrame(() => {
          if (--n > 0) return;
          off();
          done();
        });
      });
    const veiled = Promise.race([Promise.all([syntyPropsLoaded(), piecesLoaded()]), timer(VEIL_MODELS_MS)])
      // The map has rebuilt with its models by now (mapView waits on the same loads).
      .then(() => (this.gone ? undefined : Promise.race([this.compileShaders(Date.now() + VEIL_SHADERS_MS).catch(() => undefined), timer(VEIL_SHADERS_MS)])))
      .then(() => (this.gone ? undefined : Promise.race([frames(2), timer(1000)])))
      .then(() => {
        if (this.veiled === veiled) this.veiled = null;
        lift();
      });
    this.veiled = veiled;
  }

  /**
   * The scene's materials compiled behind the veil, and again with one and two lights more while there is time:
   * a shot's flash or a bang adds a light for a moment (fx.ts), and a count of lights not seen yet compiled
   * every lit material on the spot, a stall at the first shot of each mission.
   */
  private async compileShaders(until: number): Promise<void> {
    const { gl, scene, camera } = this.renderer;
    await gl.compileAsync(scene, camera);
    const extra: THREE.PointLight[] = [];
    try {
      for (let i = 0; i < 2 && !this.gone && Date.now() < until; i++) {
        extra.push(new THREE.PointLight(0xffffff, 0));
        scene.add(extra[i]!);
        await gl.compileAsync(scene, camera);
      }
    } finally {
      scene.remove(...extra);
    }
  }

  /** The arrival briefing, XCOM-style: operation, area and objective. */
  intro(): void {
    // Nothing plays over the loading veil: the briefing's time would run out on a black screen.
    if (this.veiled) {
      void this.veiled.then(() => !this.gone && this.intro());
      return;
    }
    // Over before the veil lifted: the outcome is what there is to see.
    if (this.state.outcome) return;
    const s = this.state;
    const mission = this.missionView();
    // What the mission is for, said at the start and not only in the strip (playtest 13/10 #7): the objective,
    // the turns under it, and the operation's briefing whole, as the squad read it (playtest 19/10 #12).
    const cm = s.campaignMission;
    // A siege's, Lore's: how they come and what loses it.
    const briefing = cm && 'briefing' in cm && typeof cm.briefing === 'string' ? cm.briefing : s.mission.siege ? SIEGE_TEXT.briefing : undefined;
    const closed = this.hud.intro(
      // By night, said with the kind: the grey line under the name is gone (#12).
      `${this.kicker()}${s.mission.night ? ` · ${NIGHT_LABEL.toUpperCase()}` : ''}`,
      this.missionName(),
      mission.objective,
      mission.turnsLeft !== null ? `${mission.turnsLeft} turnos` : undefined,
      briefing,
    );
    // Real time: «Empezar» sets the clock going (the mission starts stopped: docs/TIEMPO-REAL.md §6b).
    if (s.realtime) void closed.then(() => this.state.realtime?.paused && this.rtPause(false));
    // With the living among the dead (18/10 #47), the log says they fight each other.
    if (s.units.some((u) => u.alive && u.team === 'alien' && !u.objective && !TEMPLATES[u.template].undead)) this.hud.log(LIVING_TIP, FEUD_COLOR);
    // A night mission: the log keeps the rules. No radio in the middle of a mission (playtest 15/10 #8).
    if (s.mission.night) {
      this.hud.log(NIGHT_TEXT.rules.join(' '), '#2a4a7a');
      // The first night ever, a card with how the light works, once the arrival card is closed (#50).
      let seen = true;
      try {
        seen = localStorage.getItem(NIGHT_SEEN_KEY) === '1';
        localStorage.setItem(NIGHT_SEEN_KEY, '1');
      } catch {
        /* storage unavailable: no card rather than one every night */
      }
      if (!seen) void closed.then(() => this.hud.nightCard(NIGHT_TEXT.label, NIGHT_TEXT.rules));
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
    // Keep Brena's snow in view throughout its presentation, rather than panning into unexplored fog.
    if (snowyMission(v)) return;
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
    this.reachCache = null;
    // Real time (docs/TIEMPO-REAL.md): no queue, each tick's events shown as they come.
    if (this.state.realtime) {
      if (by === this.mySlot) this.awaitingReply = false;
      this.realtimeBatch(events, by);
      return;
    }
    if (by === this.mySlot) {
      this.awaitingReply = false;
      this.myPending += events.length;
    }
    const epoch = this.rig.epoch;
    this.queue.push(...events.map((e) => ({ e, by, epoch })));
    this.refreshInteractive();
    void this.pump();
  }

  onRejected(message: string): void {
    this.awaitingReply = false;
    this.hud.toast(message);
  }

  onPresence(slot: Slot, unit: string | null, tile: Place | null): void {
    if (slot === this.mySlot) return;
    const color = PLAYER_COLORS[slot];
    this.units.setPartnerSelected(unit, color);
    this.overlays.setPartnerHover(tile, color);
  }

  /** The partner planned a move (or withdrew one, `tile: null`). */
  onPlan(slot: Slot, unit: string, tile: Place | null): void {
    if (slot === this.mySlot) return;
    // A changed plan counts from when it changed (#24): out, and in again at the end.
    this.plans.delete(unit);
    if (tile) this.plans.set(unit, { slot, tile });
    this.drawPlans();
    const u = getUnit(this.state, unit);
    if (tile && u) this.hud.log(`${this.playerName(slot)} planea mover a ${u.name}`, PLAYER_COLORS[slot]);
  }

  onPing(slot: Slot, tile: Place, kind: PingKind): void {
    this.fx.ping(tileToWorld(tile), PLAYER_COLORS[slot], kind === 'enemy');
    // Whose, over it, for 4 s (playtest 18/10 #7: «que se quede 3–5 s»).
    this.overlays.pingTag(tile, this.playerName(slot), PLAYER_COLORS[slot]);
    this.sfx.mark(tileToWorld(tile));
    if (slot !== this.mySlot) {
      const who = this.players.find((p) => p.slot === slot)?.name ?? 'El otro jugador';
      this.hud.log(`${who} marca ${kind === 'enemy' ? 'un enemigo' : 'una posición'}`, PLAYER_COLORS[slot]);
    }
  }

  /** The server's clock on the call to leave changed (PRUEBA-2026-10-14 #28): the bar's answer says it. */
  refreshDeadline(): void {
    this.refreshHud();
  }

  setPlayers(players: PlayerInfo[]): void {
    const now = performance.now();
    for (const p of players) {
      if (p.connected) this.droppedAt.delete(p.slot);
      else if (this.players.find((q) => q.slot === p.slot)?.connected) {
        this.droppedAt.set(p.slot, now);
        // Past the server's grace their soldiers take this player's orders; no message says so.
        window.setTimeout(() => this.gone || this.commandChanged(), (p.away ? AWAY_GRACE_MS : RECONNECT_GRACE_MS) + 200);
      }
    }
    this.players = players;
    if (!this.commandChanged()) this.refreshHud();
  }

  /**
   * The partner's soldiers come under this player's orders (the partner is
   * gone) or go back to them: said once, and the selection and the HUD
   * redrawn. Whether it changed.
   */
  private commandChanged(): boolean {
    const gone = this.partnerGone()?.name ?? null;
    if (gone === this.commandingFor) return false;
    const before = this.commandingFor;
    this.commandingFor = gone;
    if (gone) this.hud.toast(`${gone} no está: mandas también sus soldados.`);
    else if (before) this.hud.toast(`${before} ha vuelto: cada uno manda en lo suyo.`);
    this.refreshInteractive();
    return true;
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
    this.gone = true;
    window.clearTimeout(this.endArmTimer);
    window.clearTimeout(this.momentTimer);
    for (const d of this.disposers) d();
    this.sfx.siren(null);
    this.rig.dispose();
    this.hud.destroy();
    this.renderer.dispose();
    clear(this.root);
    this.root.remove();
  }

  private send(cmd: Command): void {
    // Real time: orders go one after another, a soldier each, with no wait for the last one's answer (Arte, 20/10).
    if (this.state.realtime) {
      this.net.send({ t: 'cmd', cmd });
      return;
    }
    if (this.awaitingReply && cmd.type !== 'endTurn') return;
    if (cmd.type !== 'endTurn') this.disarmEndTurn();
    this.awaitingReply = cmd.type !== 'endTurn';
    this.net.send({ t: 'cmd', cmd });
    window.setTimeout(() => (this.awaitingReply = false), 1500);
  }

  /** The campaign as last heard (main.ts), for a soldier's board on its sheet (openSheet); none in a skirmish. */
  setCampaign(c: CampaignState | null): void {
    this.campaign = c;
  }

  /**
   * «Hoja» (playtest 18/10 #23): a soldier's sheet over the battle, only to
   * look at (no action spent, no command passed): its figures, what it
   * carries and its skills, and in a campaign its board, as in the Cuartel.
   */
  private openSheet(id: string): void {
    const u = getUnit(this.view, id);
    if (!u || u.team !== 'xcom') return;
    const t = TEMPLATES[u.template];
    const soldier = u.template === 'rookie';
    const step = VETERANCY[u.rank];
    const rank = soldier && step ? (u.appearance?.sex === 'f' ? step.female : step.name) : t.name;
    const whose = u.owner !== null && u.owner !== this.mySlot ? { name: this.playerName(u.owner), color: PLAYER_COLORS[u.owner] } : null;
    const stats: [string, string, string?][] = [
      ['Salud', `${u.hp} de ${u.maxHp}`],
      ['Acciones', `${u.ap} de ${apPerTurn(u)}`],
      ['Puntería con armas de fuego', String(t.aim + u.mods.aim + u.mods.gunAim)],
      ['Puntería cuerpo a cuerpo', String(t.aim + u.mods.aim + u.mods.meleeAim)],
      ['Voluntad', String(u.will)],
    ];
    if (u.armor) stats.push(['Blindaje', String(u.armor)]);
    // As the soldier's panel says it (hud.ts setSelected): what is in the hand, the pistol beside it, the blade.
    const arms = u.arms;
    const weapon = u.weaponName ?? (arms ? (arms.primary ? FOUND_WEAPONS[arms.primary].name : WEAPONS.pistol.name) : weaponName(t.weapon, u.weaponTier ?? 1));
    const extras = arms ? [arms.primary ? WEAPONS.pistol.name : '', arms.melee ? FOUND_WEAPONS[arms.melee].name : WEAPONS.knife.name] : [t.secondary ? WEAPONS[t.secondary].name : '', t.meleeWeapon ? WEAPONS[t.meleeWeapon].name : ''];
    const belt = u.gear.map((g) => {
      const charge = Object.keys(ITEMS[g].charges ?? {})[0] as keyof typeof u.charges | undefined;
      return charge ? `${ITEMS[g].name} (${u.charges[charge] ?? 0})` : ITEMS[g].name;
    });
    const carries: [string, string][] = [['Armas', [weapon, ...extras].filter(Boolean).join(' · ')], ['Cinturón', belt.join(' · ') || 'Nada']];
    if (u.worn?.length) carries.push(['Puesto', u.worn.map((w) => ITEMS[w].name).join(' · ')]);
    carries.push(['Mochila', (u.backpack ?? []).map((l) => lootName(l)).join(' · ') || 'Vacía']);
    const skills = u.perks.map((p) => BOARD.find((c) => c.perk === p)?.name).filter((n): n is string => !!n);
    const cs = u.campaignId ? this.campaign?.soldiers.find((x) => x.id === u.campaignId) : undefined;
    const board = cs
      ? treePanel({ s: skillsOf(cs), editable: false, owner: this.playerName(cs.owner), portrait: soldierPortrait('rookie', PLAYER_COLORS[cs.owner], cs.appearance, cs.id), onUnlock: () => undefined, onClose: () => void this.hud.closeModal() })
      : null;
    this.hud.soldierSheet({ name: displayName(u.name, u.nickname), rank, whose, stats, carries, skills }, board);
  }

  /**
   * The map's cursor (playtest 19/10 #5, styles.css): the brackets with Mayús held (a click signals, #14), the
   * crosshair on an enemy in sight while there is a soldier to give orders to, the yellow arrow on another of
   * this player's soldiers (a click picks it), else the arrow.
   */
  /** Mayús pressed or let go (#14): the map's cursor says a click signals. */
  private holdSignal(on: boolean): void {
    if (this.signalKey === on) return;
    this.signalKey = on;
    this.updateCursor(this.hoverTile ? unitAt(this.view, this.hoverTile) : undefined);
  }

  private updateCursor(hovered?: Unit): void {
    const u = this.selectedUnit();
    const enemy = !!hovered?.alive && hovered.team === 'alien' && this.visibleAliens.has(hovered.id);
    const kind = this.signalKey ? 'signal' : enemy && u && this.canAct() ? 'aim' : hovered?.alive && hovered.id !== u?.id && this.commands(hovered) ? 'hot' : 'arrow';
    this.renderer.gl.domElement.style.cursor = `var(--cursor-${kind})`;
  }

  private ping(tile: Place): void {
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
    // No speed-up for a backlog (docs/UX-RITMO.md): the enemy turn arrives whole, and it played at ×2.5.
    while (this.queue.length) {
      // The enemy turn plays at the player's chosen speed (playtest 18/10 #6); the squad's own, at its own pace.
      setCombatRate(this.view.activeTeam === 'alien' ? graphics().enemySpeed : 1);
      const item = this.queue.shift()!;
      // The dead that only walk this turn come last (rules.ts runAlienTurn), and walk all at once, under one framing.
      const group = this.walkingWith(item);
      if (group.length) {
        await this.playGroup([item, ...group]);
        continue;
      }
      // Arrivals, or contacts, of the same moment play together too (playtest 18/10 #6).
      const moment = this.sameMoment(item);
      if (moment.length) {
        await this.playTogether([item, ...moment]);
        continue;
      }
      await this.beforeActing(item);
      this.current = item;
      const epoch = this.rig.epoch;
      try {
        await this.play(item);
      } catch (err) {
        animFailed(err);
      }
      this.current = null;
      this.noteResult(item);
      await this.settle(item, epoch);
      // What a blast does to everyone in it plays at once (playtest 15/10 #16).
      if (item.e.t === 'explosive') {
        const blast = this.blastResults();
        if (blast.length) await this.playBlast(blast);
      }
    }
    // The last enemy attack of the turn is read too, before the turn changes hands.
    if (this.resultHold) {
      const hold = this.resultHold;
      this.resultHold = 0;
      await wait(hold);
    }
    this.playing = false;
    this.refreshInteractive();
    if (this.planDue) {
      this.planDue = false;
      this.following = true;
      this.planSent = null;
      this.planSkip.clear();
    }
    if (this.following) this.followPlan();
  }

  /** What an animated event leaves behind: the view, the fog, the HUD, the sightings, and the pause after one's own action. */
  private async settle(item: QueuedEvent, epoch: number): Promise<void> {
    const seen = this.visibleAliens;
    const call = this.view.evacCall;
    const floorBefore = new Set((this.view.dropped ?? []).map((d) => d.id));
    applyEvent(this.view, item.e);
    const e = item.e;
    // The next thing of a rucksack being taken (takeAll), while it fits and is still there.
    if (e.t === 'lootTaken' && this.packQueue?.unit === e.unit) {
      const q = this.packQueue;
      const u = getUnit(this.view, e.unit);
      const next = q.ids.find((id) => this.view.dropped?.some((d) => d.id === id));
      q.ids = q.ids.filter((id) => id !== next);
      if (u && next && (u.backpack?.length ?? 0) < (u.packSlots ?? BACKPACK_SLOTS)) this.send({ type: 'item', unit: u.id, action: 'take', id: next });
      else this.packQueue = null;
    }
    // A soldier falls and their rucksack stays on the floor (#49), said once.
    if (e.t === 'died' && (this.view.dropped ?? []).some((d) => d.from === e.unit && !floorBefore.has(d.id))) {
      const fallen = getUnit(this.view, e.unit);
      if (fallen) this.hud.log(fallenPackLine(fallen.name), '#ff8a78');
    }
    // A find with the backpack full stays on the floor (docs/FASE8.md §C): say so, with «Cambiar».
    if ((e.t === 'searched' || e.t === 'roomSearched') && item.by === this.mySlot && e.loot) {
      const u = getUnit(this.view, e.unit);
      const left = (this.view.dropped ?? []).find((d) => !floorBefore.has(d.id) && u && samePos(d.pos, u.pos));
      if (u && left) this.hud.packOffer(packFullLine(left.loot), () => this.openSwap(u.id, left.id));
    }
    // The second half of a swap: the slot is on the floor, now the find comes up.
    if (e.t === 'lootDropped' && item.by === this.mySlot && this.swapTake?.unit === e.unit) {
      const { unit, id } = this.swapTake;
      this.swapTake = null;
      if (this.view.dropped?.some((d) => d.id === id)) this.send({ type: 'item', unit, action: 'take', id });
    }
    // A call nobody answered lapses with the squad's turn.
    if (e.t === 'turnBegan' && call && !this.view.evacCall) this.hud.log(EVAC_TEXT.lapsed, '#ffc93a');
    // Plans (playtest 18/10 #3): the partner's go as a turn begins; one's own last until the command comes to
    // this player, who then walks one (followPlan), or until the squad's next turn opens with the partner's.
    if (e.t === 'turnBegan') {
      const mine = e.team === 'xcom' && (e.command ?? this.mySlot) === this.mySlot;
      for (const [id, plan] of this.plans) if (plan.slot !== this.mySlot || (e.team === 'xcom' && !mine)) this.plans.delete(id);
      this.following = false;
      if (mine) this.planDue = true;
    } else if (e.t === 'commandPassed' && e.to === this.mySlot) this.planDue = true;
    else if (e.t === 'moved' || e.t === 'died' || e.t === 'evacuated') {
      this.plans.delete(e.unit);
      // The one ordered went: not to be taken for refused next time.
      if (this.planSent?.startsWith(`${e.unit}:`)) this.planSent = null;
    }
    if (item.e.t === 'tileChanged' || item.e.t === 'floorBroken') this.mapView.rebuild(this.view);
    this.refreshView();
    // A weapon taken in hand changes on the figure as the view syncs (unitViews, 0.7 s): seen whole before what follows.
    if (e.t === 'weaponEquipped') await wait(0.7);
    this.refreshStatus();
    try {
      await this.announceSighted(item, seen, epoch);
    } catch (err) {
      animFailed(err);
    }
    // The noise that follows an action doesn't set the pause after it: what was done does.
    if (item.by === this.mySlot && e.t !== 'noise') this.holdAfter = HOLD_AFTER[e.t] ?? 0.2;
    if (item.by === this.mySlot && --this.myPending === 0) {
      // Let the player see the outcome before the selection moves on.
      await wait(this.holdAfter);
      this.rig.restoreZoom();
      this.refreshInteractive();
    }
    if (e.t === 'turnBegan') {
      this.lastActor = null;
      this.groupFrame = null;
      // The space bar hurries the enemy turn only.
      if (e.team === 'xcom') setHurry(false);
      // The end-turn button says whose turn it is (and, in the enemy's, that the space bar hurries it).
      this.refreshHud();
    }
  }

  /** The enemy turn is on screen: the space bar hurries it, and the end-turn button says so. */
  private enemyTurnPlaying(): boolean {
    return this.view.activeTeam === 'alien' && !this.view.outcome;
  }

  /** Whether anything of the enemy turn now queued will be seen: something done in sight, or to one of ours. */
  private enemyTurnShows(): boolean {
    for (const { e } of this.queue) {
      if (e.t === 'turnBegan') return false;
      if (e.t === 'reinforcementsIncoming' || e.t === 'missionEnded' || e.t === 'concealmentBroken') return true;
      // An attack on one of ours, or on whoever the squad sees (an enemy on another: 18/10 #47).
      if (e.t === 'shot') {
        const target = getUnit(this.view, e.target);
        if (target && this.shownToSquad(target)) return true;
      }
      const id = unitOf(e);
      const u = id ? getUnit(this.view, id) : undefined;
      if (u && (u.team === 'xcom' || this.shownToSquad(u) || (e.t === 'moved' && e.path.some((p) => this.mapView.isVisible(p.x, p.y))))) return true;
    }
    return false;
  }

  /**
   * The enemy that `e` makes act in sight, in the enemy turn: the unit that
   * moves, attacks, shouts or throws, if the squad sees it (or a step of its
   * path). Null for anything else.
   */
  private enemyActor(e: GameEvent): Unit | null {
    const v = this.view;
    if (v.activeTeam !== 'alien' || FOLLOW_UP.has(e.t)) return null;
    const id = unitOf(e);
    const u = id ? getUnit(v, id) : undefined;
    if (!u || u.team !== 'alien' || !u.alive) return null;
    const seen = this.shownToSquad(u) || (e.t === 'moved' && e.path.some((p) => this.mapView.isVisible(p.x, p.y)));
    return seen ? u : null;
  }

  /**
   * docs/UX-RITMO.md, before an event plays: the result of the last enemy
   * attack is read first; and an enemy that starts acting in sight is shown
   * before it does (a pause after the previous one, the camera to it unless
   * it is near or the player has the camera, and a still moment).
   */
  private async beforeActing(item: QueuedEvent): Promise<void> {
    if (FOLLOW_UP.has(item.e.t)) return;
    const hold = this.resultHold;
    this.resultHold = 0;
    const actor = this.enemyActor(item.e);
    if (!actor || actor.id === this.lastActor) {
      if (hold) await wait(hold);
      return;
    }
    const first = this.lastActor === null;
    this.lastActor = actor.id;
    // One that was in the last group's framing (often one of the group, now going to bite) is in view
    // already: no new presentation, only what is left of reading the last result (Gameplay, 14/10).
    if (this.inGroupFrame(actor)) {
      if (hold) await wait(hold);
      return;
    }
    this.groupFrame = null;
    await wait(Math.max(first ? 0 : BETWEEN, hold));
    await this.showEnemy([this.units.worldPos(actor.id)]);
  }

  /** Whether `u` was in the last group's framing and the camera is still there. */
  private inGroupFrame(u: Unit): boolean {
    const f = this.groupFrame;
    if (!f) return false;
    const at = this.units.worldPos(u.id);
    const reach = Math.max(NEAR_ENOUGH, f.radius + 1);
    return (f.ids.has(u.id) || at.distanceTo(f.centre) <= reach) && at.distanceTo(this.rig.focusPoint) <= reach;
  }

  /** The camera to where `points` are (unless near, or the player has it), there, and a still moment. */
  private async showEnemy(points: THREE.Vector3[]): Promise<void> {
    const centre = new THREE.Box3().setFromPoints(points).getCenter(new THREE.Vector3());
    const theirs = performance.now() - this.rig.takenAt < PLAYER_CAMERA_MS;
    if (!theirs && centre.distanceTo(this.rig.focusPoint) > NEAR_ENOUGH) {
      this.rig.focus(centre);
      for (let i = 0; i < 24 && !this.rig.settled; i++) await wait(0.05);
    }
    await wait(SETTLE);
  }

  /** An enemy attack just played: its result is to be read before the next thing starts. */
  private noteResult(item: QueuedEvent): void {
    const e = item.e;
    if (this.view.activeTeam !== 'alien') return;
    if (e.t === 'shot') {
      const shooter = getUnit(this.view, e.unit);
      const target = getUnit(this.view, e.target);
      if (shooter?.team === 'alien' && (this.shownToSquad(shooter) || (!!target && this.shownToSquad(target)))) this.resultHold = e.result === 'miss' ? READ_MISS : READ_HIT;
    } else if ((e.t === 'explosive' || e.t === 'psi') && this.enemyActor(e)) this.resultHold = READ_HIT;
  }

  /**
   * The arrivals (`spawned`) or the contacts (`podActivated`) that follow
   * `item` one after another, of its own kind: they happen at the same moment,
   * so they are seen at once (playtest 18/10 #6, like the walkers).
   */
  private sameMoment(item: QueuedEvent): QueuedEvent[] {
    const kind = item.e.t;
    if (kind !== 'spawned' && kind !== 'podActivated') return [];
    let n = 0;
    while (n < this.queue.length && this.queue[n]!.e.t === kind) n++;
    return this.queue.splice(0, n);
  }

  /** A moment's arrivals or contacts at once: one framing over all of them in sight, then each plays at the same time. */
  private async playTogether(all: QueuedEvent[]): Promise<void> {
    const v = this.view;
    const points = all.flatMap((q) => {
      const e = q.e;
      if (e.t === 'spawned') return e.units.filter((u) => this.mapView.isVisible(u.pos.x, u.pos.y)).map((u) => tileToWorld(u.pos));
      if (e.t !== 'podActivated') return [];
      const pod = v.pods.find((p) => p.id === e.pod);
      const feud = wokenByFoes(e);
      return (pod?.unitIds ?? []).map((id) => getUnit(v, id)).filter((m): m is Unit => !!m?.alive && (!feud || this.shownToSquad(m))).map((m) => tileToWorld(m.pos));
    });
    if (points.length && (v.activeTeam === 'alien' || all.some((q) => q.by === this.mySlot))) await this.showEnemy(points);
    const epoch = this.rig.epoch;
    this.current = all[0]!;
    this.grouped = true;
    await Promise.all(all.map((q) => this.play(q).catch((err) => animFailed(err))));
    this.grouped = false;
    this.current = null;
    for (const q of all) {
      this.noteResult(q);
      await this.settle(q, epoch);
    }
  }

  /** Whether `e` is a walk of one of the dead in the enemy turn (they only bite: docs/ZOMBIS-PRIMERO.md). */
  private deadWalker(e: GameEvent): boolean {
    if (e.t !== 'moved' || this.view.activeTeam !== 'alien') return false;
    const u = getUnit(this.view, e.unit);
    return !!u && u.team === 'alien' && u.alive && !shootsFromRange(u);
  }

  /**
   * The walks of the dead right after `item`'s, whatever their group, taken
   * off the queue (none if it walks alone). The engine moves the dead with
   * nobody in reach after those going for the squad (ai.ts onlyWalks; playtest
   * 16/10 #6), so theirs come in one run at the end of the turn.
   */
  private walkingWith(item: QueuedEvent): QueuedEvent[] {
    if (!this.deadWalker(item.e)) return [];
    // A door one of them opens on its way (its tileChanged and its noise) stays in the run: it split
    // the dead into one framing per door, walking in a row (playtest 17/10 #46).
    let n = 0;
    let last = 0;
    while (n < this.queue.length) {
      const e = this.queue[n]!.e;
      if (this.deadWalker(e)) last = ++n;
      else if (e.t === 'tileChanged' || e.t === 'noise') n++;
      else break;
    }
    return this.queue.splice(0, last);
  }

  /**
   * The dead walking in one run (docs/UX-RITMO.md; playtest 16/10 #6): one
   * framing over where they are and where they go, a still moment, and they
   * all walk at once, each one's actions as one stretch, WALKERS_PACE times
   * quicker; the attacks came before, one by one.
   */
  private async playGroup(all: QueuedEvent[]): Promise<void> {
    const v = this.view;
    // The doors they open go first, at once (no ring nor pause each: the walk is what is seen).
    const doors = all.filter((q) => q.e.t !== 'moved');
    const items = all.filter((q) => q.e.t === 'moved');
    const shown = items.filter((q) => this.enemyActor(q.e));
    const hold = this.resultHold;
    this.resultHold = 0;
    if (shown.length) {
      const first = this.lastActor === null;
      this.lastActor = 'walkers';
      await wait(Math.max(first ? 0 : BETWEEN, hold));
      const points = shown.flatMap((q) => {
        const e = q.e as Extract<GameEvent, { t: 'moved' }>;
        return [this.units.worldPos(e.unit), tileToWorld(e.path[e.path.length - 1] ?? getUnit(v, e.unit)!.pos)];
      });
      const box = new THREE.Box3().setFromPoints(points);
      this.groupFrame = { ids: new Set(items.map((q) => unitOf(q.e)!)), centre: box.getCenter(new THREE.Vector3()), radius: box.getSize(new THREE.Vector3()).length() / 2 };
      await this.showEnemy(points);
    } else if (hold) await wait(hold);
    const epoch = this.rig.epoch;
    for (const q of doors) await this.settle(q, epoch);
    // Each one's walks as one: its two actions a single stretch at the dash's gait, no stop between.
    const walks = new Map<string, QueuedEvent>();
    for (const q of items) {
      const e = q.e as Extract<GameEvent, { t: 'moved' }>;
      const had = walks.get(e.unit)?.e as typeof e | undefined;
      walks.set(e.unit, had ? { ...q, e: { ...e, path: [...had.path, ...e.path], ap: had.ap + e.ap } } : q);
    }
    this.groupPlans = this.planGroup([...walks.values()]);
    this.current = items[0]!;
    this.grouped = true;
    await Promise.all([...walks.values()].map((q) => this.play(q).catch((err) => animFailed(err))));
    this.grouped = false;
    this.groupPlans = null;
    this.current = null;
    for (const q of items) await this.settle(q, epoch);
  }

  /**
   * When each of a group's walkers sets off and where it stands a moment (walkPlan.ts): the engine planned
   * them one after another (in `walks`' order), each from where the ones before ended, and they are played
   * at once, at their own paces; so nobody walks into a tile another still holds or one planned before it
   * is still to go through. Only the walks the squad sees (the others are not played).
   */
  private planGroup(walks: QueuedEvent[]): Map<string, WalkPlan> {
    const v = this.view;
    const ids: string[] = [];
    const walkers: Walker[] = [];
    const paced: ((pace: number) => number[])[] = [];
    for (const q of walks) {
      const e = q.e as Extract<GameEvent, { t: 'moved' }>;
      const u = getUnit(v, e.unit);
      if (!u || !e.path.length || !(this.shownToSquad(u) || e.path.some((p) => this.mapView.isVisible(p.x, p.y)))) continue;
      const tiles = [toPos(u.pos), ...e.path.map(toPos)];
      ids.push(u.id);
      paced.push((pace) => this.units.stepSeconds(u.id, u.pos, e.path, e.ap, pace));
      walkers.push({
        tiles: tiles.map((p) => nodeOf(v, p)),
        steps: this.units.stepSeconds(u.id, u.pos, e.path, e.ap, WALKERS_PACE),
        corners: tiles.map((p, k) => {
          const next = tiles[k + 1];
          return next && next.x !== p.x && next.y !== p.y && next.z === p.z ? ([nodeOf(v, { x: next.x, y: p.y, z: p.z }), nodeOf(v, { x: p.x, y: next.y, z: p.z })] as const) : null;
        }),
      });
    }
    // Quicker if they would take too long all together (planHasted): each step as walk() plays it at that pace.
    const plans = planHasted((haste) => (haste === 1 ? walkers : walkers.map((w, i) => ({ ...w, steps: paced[i]!(WALKERS_PACE * haste) }))));
    return new Map(ids.map((id, i) => [id, plans[i]!]));
  }

  /**
   * What a blast leaves on everyone it caught, right after its `explosive`
   * (rules.ts explode: each victim's hit, fall and bite in turn), taken off
   * the queue; none unless there are at least two.
   */
  private blastResults(): QueuedEvent[] {
    let n = 0;
    while (n < this.queue.length && BLAST_RESULTS.has(this.queue[n]!.e.t)) n++;
    return n > 1 ? this.queue.splice(0, n) : [];
  }

  /** A blast's results at once: everyone it caught is hit together, then all who die fall together. */
  private async playBlast(items: QueuedEvent[]): Promise<void> {
    const epoch = this.rig.epoch;
    this.grouped = true;
    for (const phase of [(t: string) => t !== 'died', (t: string) => t === 'died']) {
      const these = items.filter((q) => phase(q.e.t));
      if (!these.length) continue;
      this.current = these[0]!;
      await Promise.all(these.map((q) => this.play(q).catch((err) => animFailed(err))));
    }
    this.grouped = false;
    this.current = null;
    for (const q of items) {
      this.noteResult(q);
      await this.settle(q, epoch);
    }
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
    // Right after the squad's turn begins come the bites' tolls (rules.ts: a `damaged` for each bitten, and a fall).
    this.biteTick = e.t === 'turnBegan' ? e.team === 'xcom' : this.biteTick && (e.t === 'damaged' || e.t === 'died');
    if (by !== null && by !== this.mySlot) this.notePartner(e, by);
    const v = this.view;
    const alienPhase = v.activeTeam === 'alien';
    // Real time: the camera is the player's alone (docs/TIEMPO-REAL.md §5).
    const cinematic = !v.realtime && (by === this.mySlot || alienPhase);
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
        // In a group of the dead (playGroup), the framing is the group's: no walker takes the camera.
        if (!this.grouped) look(this.units.worldPos(u.id));
        let steps = 0;
        // The move's cost picks the gait (Arte's gaits.ts): a dash runs, a blue move walks.
        await this.units.moveAlong(
          u.id,
          e.path,
          (p) => {
            if (!this.grouped) look(p);
            if (steps++ % 2 === 0) this.sfx.step(u, p);
          },
          e.ap,
          this.grouped ? WALKERS_PACE * (this.groupPlans?.get(u.id)?.haste ?? 1) : 1,
          this.groupPlans?.get(u.id)?.waits,
          this.groupPlans?.get(u.id)?.passes.slice(1),
        );
        return;
      }
      case 'shot':
        return this.playAttack(e, cam, look);
      case 'damaged': {
        const target = getUnit(v, e.unit)!;
        // The bite's toll as the squad's turn begins (playtest 15/10 #13): said as such, not as a hit.
        const bite = this.biteTick && target.infected;
        const shot = bite ? null : this.lastShot?.target === e.unit ? this.lastShot.result : null;
        const crit = shot === 'crit';
        // Enemies fighting each other (18/10 #47): only what the squad sees of it.
        const from = this.lastAttacker ? getUnit(v, this.lastAttacker) : undefined;
        const feud = target.team === 'alien' && from?.team === 'alien';
        if (feud && !this.shownToSquad(target)) return;
        // Real time: an enemy's hurt is seen on its bar and over it, not in the log (Gameplay, 20/10).
        if (e.amount && !(v.realtime && target.team === 'alien')) {
          this.hud.log(bite ? biteLossLine(target.name) : `${target.name} recibe ${e.amount} de daño${crit ? ' (crítico)' : ''}`, feud ? FEUD_COLOR : target.team === 'alien' ? '#54d1ff' : '#ff6b5a');
        }
        if (!this.shownToSquad(target)) return;
        const chest = this.units.chestPos(target.id);
        if (e.amount) {
          const label = bite ? BITE_FLOAT : crit ? `CRÍTICO −${e.amount}` : shot === 'graze' ? `ROZADURA −${e.amount}` : `−${e.amount}`;
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
        // One enemy at another's hands (18/10 #47): said where the squad sees it, as what it is.
        const killer = e.by ? getUnit(v, e.by) : undefined;
        if (u.team === 'alien' && killer?.team === 'alien') {
          if (!this.shownToSquad(u) && !this.shownToSquad(killer)) return;
          this.hud.log(`${capitalize(anEnemy(killer))} acaba con ${anEnemy(u)}.`, FEUD_COLOR);
        } else this.hud.log(u.team === 'xcom' ? `${u.name} ha caído en combate` : `${u.name} eliminado`, u.team === 'xcom' ? '#ff6b5a' : '#54d1ff');
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
      case 'barFilled': {
        // A bar filled out there (playtest 16/10 #10): its stat is already up (Unit.lines); seen over the soldier,
        // the line's name and what it added on a bar that fills, and Lore's line in the log.
        const u = getUnit(v, e.unit)!;
        this.hud.log(barLine(u.name, e.skill, [e.growth]), '#6dff9a');
        if (!this.shownToSquad(u)) return;
        look(this.units.worldPos(u.id));
        this.fx.floatBar(this.units.chestPos(u.id), barFillFloat(e.skill, e.growth), `sk-${e.skill}`);
        await wait(1.1);
        return;
      }
      case 'stoneThrown': {
        // A stone (docs/FASE10.md §4): thrown underhand (Arte's tossStone), its low arc and its knock where it lands or
        // on whom it hits (fx.stone), and its sound out and in (Red's sfx). The `damaged` that follows a hit is its own.
        const u = getUnit(v, e.unit)!;
        if (!this.shownToSquad(u) && !this.mapView.isVisible(e.target.x, e.target.y)) return;
        const to = tileToWorld(e.target);
        const struck = e.hit ? getUnit(v, e.hit) : undefined;
        look(this.units.worldPos(u.id).lerp(to, 0.5));
        const hand = await this.units.tossStone(u.id, to);
        this.sfx.stoneThrow(hand);
        await this.fx.stone(hand, struck ? this.units.chestPos(struck.id) : to, !!struck);
        this.sfx.stoneLand(to, !!struck);
        return;
      }
      case 'overwatch':
      case 'hunker': {
        const u = getUnit(v, e.unit)!;
        if (!this.shownToSquad(u)) return;
        look(this.units.worldPos(u.id));
        // The blade's watch (docs/FASE10.md §4) is said as such: on guard, not watching with a gun.
        const text = e.t === 'hunker' ? 'CUBRIÉNDOSE' : e.killZone ? 'ZONA LETAL' : e.melee ? 'HOJA EN ALTO' : 'VIGILANCIA';
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
        const aim = await this.units.raiseWeapon(u.id, chest);
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
        void wait(0.8).then(() => this.units.lowerWeapon(u.id, aim));
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
        // Out of sight it costs no time (docs/UX-RITMO.md): the body is just up when it comes into view.
        if (!this.mapView.isVisible(corpse.pos.x, corpse.pos.y) && !(u && this.shownToSquad(u))) return;
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
        this.fx.floatText(this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.5, 0)), '¡MORDISCO!', 'alert');
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
        // A medicine from the backpack (docs/FASE8.md §C), like Restoration, heals at once.
        const medicine = e.t === 'healed' && this.medicineHeal;
        if (medicine) this.medicineHeal = false;
        if (e.t === 'healed' && (medicine || (this.lastUsed?.unit === u.id && this.lastUsed.ability === 'restoration'))) {
          // Restoration heals everyone at once. A medicine's line already says how much.
          this.fx.heal(this.units.chestPos(target.id));
          this.sfx.heal(this.units.chestPos(target.id));
          this.fx.floatText(this.units.chestPos(target.id), `+${e.amount}`, 'heal');
          if (!medicine) this.hud.log(`${target.name} recupera ${e.amount}`, '#6dff9a');
          await wait(0.2);
          return;
        }
        look(this.units.worldPos(u.id).lerp(this.units.worldPos(target.id), 0.5));
        await wait(0.3);
        if (e.t === 'healed') {
          this.fx.heal(this.units.chestPos(target.id));
          this.sfx.heal(this.units.chestPos(target.id));
          this.fx.floatText(this.units.chestPos(target.id), `+${e.amount}`, 'heal');
          this.hud.log(`${u.name} cura ${e.amount} a ${target.name}`, '#6dff9a');
        } else {
          this.fx.shield(this.units.chestPos(target.id));
          this.sfx.shield(this.units.chestPos(target.id));
          this.fx.floatText(this.units.chestPos(target.id), `+${e.amount} DEFENSA`, 'info');
          this.hud.log(`${u.name} protege a ${target.name}`, '#54d1ff');
        }
        await wait(0.45);
        return;
      }
      // The backpack (docs/FASE8.md §C). Lore's words to come; these say what happened, plainly.
      case 'roomSearched': {
        const u = getUnit(v, e.unit)!;
        const at = this.units.worldPos(u.id);
        look(at);
        this.sfx.search(at);
        await this.units.rummage(u.id, at.clone().add(new THREE.Vector3(0.6, 0.4, 0)));
        this.overlays.find(u.pos, findLabel(e.loot), !e.loot ? 'empty' : e.loot.weapon ? 'rare' : 'loot');
        if (e.loot) this.sfx.found(at);
        // The seed comes from the event, so both players read the same line.
        const spot = roomAt(v, u.pos)?.spot ?? 'home';
        this.hud.log(roomSearchLine(u.name, spot, e.loot, e.room * 7 + v.turn), e.loot?.weapon ? '#ffd23a' : e.loot ? '#6dff9a' : undefined);
        await wait(0.5);
        return;
      }
      case 'lootDropped': {
        const u = getUnit(v, e.unit)!;
        const loot = u.backpack?.[e.index];
        if (loot) this.hud.log(lootDroppedLine(u.name, loot));
        await wait(0.15);
        return;
      }
      case 'lootTaken': {
        const u = getUnit(v, e.unit)!;
        const left = v.dropped?.find((d) => d.id === e.id);
        if (left) {
          this.fx.floatText(this.units.chestPos(u.id), findLabel(left.loot), 'info');
          // A fallen soldier's (#49): whose it was, in Lore's words.
          const fallen = left.from ? getUnit(v, left.from) : undefined;
          this.hud.log(fallen ? fallenPackTakenLine(u.name.split(' ')[0]!, fallen.name, lootName(left.loot)) : lootTakenLine(u.name, left.loot), '#6dff9a');
        }
        await wait(0.3);
        return;
      }
      case 'lootGiven': {
        const u = getUnit(v, e.unit)!;
        const to = getUnit(v, e.to)!;
        const loot = u.backpack?.[e.index];
        this.units.face(u.id, this.units.worldPos(to.id));
        this.units.face(to.id, this.units.worldPos(u.id));
        look(this.units.worldPos(u.id).lerp(this.units.worldPos(to.id), 0.5));
        if (loot) {
          this.fx.floatText(this.units.chestPos(to.id), findLabel(loot), 'info');
          this.hud.log(lootGivenLine(u.name, to.name, loot), '#6dff9a');
        }
        await wait(0.6);
        return;
      }
      case 'medicineUsed': {
        const u = getUnit(v, e.unit)!;
        const target = getUnit(v, e.target)!;
        if (target.id !== u.id) this.units.face(u.id, this.units.worldPos(target.id));
        look(this.units.worldPos(target.id));
        // One line for the whole cure (Lore): what the `healed` and `cured` that follow it do.
        const after = this.queue.slice(0, 3).map((q) => q.e);
        const healed = after.find((x): x is Extract<GameEvent, { t: 'healed' }> => x.t === 'healed' && x.target === target.id)?.amount ?? 0;
        const cured = after.some((x) => x.t === 'cured' && x.unit === target.id);
        this.hud.log(medicineLine(u.name, target.id === u.id ? null : target.name, healed, cured), '#6dff9a');
        this.medicineHeal = true;
        this.medicineCure = cured;
        await wait(0.3);
        return;
      }
      case 'cured': {
        const u = getUnit(v, e.unit)!;
        this.fx.floatText(this.units.chestPos(u.id), CURED_FLOAT, 'heal');
        // A medicine's cure is already in its line.
        if (!this.medicineCure) this.hud.log(`${u.name}: ${CURED_FLOAT.toLowerCase()}`, '#6dff9a');
        this.medicineCure = false;
        await wait(0.5);
        return;
      }
      case 'weaponEquipped': {
        const u = getUnit(v, e.unit)!;
        const loot = u.backpack?.[e.index];
        look(this.units.worldPos(u.id));
        if (loot?.weapon) {
          this.fx.floatText(this.units.chestPos(u.id), FOUND_WEAPONS[loot.weapon].name.toUpperCase(), 'callout');
          this.hud.log(weaponEquippedLine(u.name, loot.weapon), '#6dff9a');
        }
        this.sfx.reload(this.units.worldPos(u.id));
        // The swap itself is seen after (settle waits for unitViews' 0.7 s).
        await wait(0.3);
        return;
      }
      // A vest found, put on out there (playtest 16/10): its health at once, seen on the soldier.
      case 'vestWorn': {
        const u = getUnit(v, e.unit)!;
        const item = u.backpack?.[e.index]?.item;
        look(this.units.worldPos(u.id));
        if (item) {
          const hp = ITEMS[item].hp ?? 0;
          this.fx.floatText(this.units.chestPos(u.id), `+${hp} SALUD`, 'heal');
          this.hud.log(`${u.name} se pone ${ITEM_FINDS[item]}: +${hp} de salud.`, '#6dff9a');
        }
        await wait(0.6);
        return;
      }
      // A found grenade, smoke, medkit or ear plugs on the belt (playtest 18/10 #5): ready to use at once.
      case 'beltEquipped': {
        const u = getUnit(v, e.unit)!;
        const item = u.backpack?.[e.index]?.item;
        if (!item) return;
        this.hud.log(beltLine(u.name, item), '#6dff9a');
        if (!this.shownToSquad(u)) return;
        look(this.units.worldPos(u.id));
        this.fx.floatText(this.units.chestPos(u.id), BELT_TEXT.float, 'info');
        await wait(0.5);
        return;
      }
      // Leaving in La Mula (docs/PRUEBA-2026-10-14.md P1): one asks, the other answers, and the bus goes.
      case 'evacCalled': {
        const ask = evacAsk(this.playerName(e.slot), this.evacSituation(v));
        this.hud.log(ask.title, PLAYER_COLORS[e.slot]);
        // No window over the field (playtest 15/10 #10): the answer waits on the bar.
        if (e.slot !== this.mySlot) this.hud.toast(evacBarNotice(this.playerName(e.slot)));
        return;
      }
      case 'evacRefused': {
        const line = evacRefusedLine(this.playerName(e.slot));
        this.hud.log(line, PLAYER_COLORS[e.slot]);
        if (e.slot !== this.mySlot) this.hud.toast(line);
        return;
      }
      // Abandoning with the partner there (playtest 16/10): as with the bus, one asks and the other answers on the bar.
      case 'abandonCalled': {
        const ask = abandonAsk(this.playerName(e.slot));
        this.hud.log(ask, PLAYER_COLORS[e.slot]);
        if (e.slot !== this.mySlot) this.hud.toast(abandonBarNotice(this.playerName(e.slot)));
        return;
      }
      case 'abandonRefused': {
        const line = abandonRefusedLine(this.playerName(e.slot));
        this.hud.log(line, PLAYER_COLORS[e.slot]);
        if (e.slot !== this.mySlot) this.hud.toast(line);
        return;
      }
      case 'evacDeparted': {
        // Both players see it go (whoever said yes): the camera to the bus, everyone in it boards (the
        // `evacuated` that follow), and then it pulls away (Arte's mapView.busDeparts).
        const aboard = v.units.filter((u) => u.alive && u.team === 'xcom' && inEvacZone(v, u.pos));
        // Leaving in the enemy turn is leaving on time (playtest 18/10 #8): nobody asked for it.
        if (v.activeTeam === 'alien') {
          this.departedOnTime = true;
          this.hud.log(TIME_TEXT.departs, '#ff6b5a');
        }
        this.hud.log(evacDepartLine(aboard.map((u) => u.name)), '#6dff9a');
        const bus = this.busTile(v);
        if (!bus) return;
        this.rig.focus(tileToWorld(bus));
        this.rig.cinematicZoom(16);
        await wait(0.5);
        if (this.queue[0]?.e.t !== 'evacuated') await this.busLeaves(bus);
        return;
      }
      case 'evacuated': {
        // Into the bus: a puff of dust where they stood, and out of sight at once (Arte's units.board), not when the
        // event is applied: after the last one, the bus goes, and nobody is left standing outside (playtest 16/10 #2).
        this.fx.dust(this.units.worldPos(e.unit), 4);
        await wait(0.18);
        this.units.board(e.unit);
        const bus = this.busTile(v);
        if (bus && this.queue[0]?.e.t !== 'evacuated') await this.busLeaves(bus);
        return;
      }
      case 'leftBehind': {
        const u = getUnit(v, e.unit)!;
        this.fx.floatText(this.units.chestPos(u.id), 'SE QUEDA FUERA', 'callout');
        this.hud.log(leftBehindLine(u.name), '#ff8a78');
        await wait(0.6);
        return;
      }
      case 'itemPicked': {
        // Picking it up shows (playtest 14/10 #2): the camera on the soldier, a callout, the log; the objective then names who has it.
        const u = getUnit(v, e.unit)!;
        const item = this.recoveryItem();
        look(this.units.worldPos(u.id));
        this.hud.log(itemTakenLine(u.name, item), '#6dff9a');
        this.fx.floatText(this.units.chestPos(u.id), itemTakenFloat(item), 'callout');
        this.sfx.data();
        await wait(0.9);
        return;
      }
      case 'itemDropped': {
        const item = this.recoveryItem();
        this.fx.floatText(tileToWorld(e.pos, 1), itemDroppedFloat(item), 'warn');
        // Down with a broken floor, RUBBLE_TEXT.item has said it already.
        if (!this.itemFell) this.hud.log(itemDroppedLine(item), '#ffc93a');
        this.itemFell = false;
        await wait(0.4);
        return;
      }
      case 'smashed': {
        // A siege (docs/FASE12.md §10): one of the dead beats on a door or a barricade. Seen, it lunges at
        // it, the blow lands with its damage over it and its sound, and if it gives way, the board says so.
        const u = getUnit(v, e.unit)!;
        const b = breakableOf(v, e.tile);
        const at = tileToWorld(e.tile, b.kind === 'door' ? 1.1 : 0.6);
        const gone = e.left <= 0;
        // What gives way is known even unseen: a breach in the wall is what matters (its line and marks).
        if (gone) this.noteBreach(e.tile, b.kind);
        // The blows, like breaking glass: where no soldier sees them, they are neither seen nor heard.
        if (!this.shownToSquad(u) && !this.mapView.isVisible(e.tile.x, e.tile.y)) return;
        this.units.forceVisible(u.id);
        look(at);
        // Arte's: the lunge resolves as the blow lands (it steps back by itself); the gate shakes, and sparks or earth fly.
        await this.units.smash(u.id, e.tile);
        this.mapView.knock(e.tile);
        this.fx.smash(at, b.kind, gone);
        this.sfx.smash(at, b.kind, gone);
        this.fx.floatText(at, `−${e.amount}`, 'damage');
        if (gone) {
          await wait(0.2);
          this.fx.floatText(tileToWorld(e.tile, 1.6), GIVE_WAY_FLOAT[b.kind], 'warn');
        }
        await wait(cam() ? 0.35 : 0.15);
        return;
      }
      case 'tileChanged': {
        // On its own floor (docs/FASE6.md): a door upstairs opens upstairs.
        const before = kindAt(v, e.pos);
        // A siege's barricade blown away by a grenade, not by blows: a breach all the same, said once (the blows' in 'smashed', which
        // leaves it at 0 before this). A door a grenade opens stays a door: it can be shut again.
        if ((v.breakables?.[nodeOf(v, e.pos)] ?? 0) > 0 && e.kind !== 'door' && e.kind !== 'doorOpen' && e.kind !== 'low') this.noteBreach(e.pos, breakableOf(v, e.pos).kind);
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
        // A siege's door broken down (docs/FASE12.md §10): the blow that felled it showed it (fx.smash), and the map draws the gap.
        if (before === 'door' || before === 'doorOpen') return;
        // A silenced siren's car stays as it stood, solid now (rules.ts damage: its tile the car's, a part of it): its own
        // lights go out, nothing bursts.
        if (e.decor === 'sirenCar' || e.decor === 'part') return;
        // A breached wall (docs/FASE7.md §4) bursts higher and in brick and plaster, not in the cover's colours.
        if (before === 'wall') {
          this.fx.debris(tileToWorld(e.pos, 1.2), 0x9a8a78, 12);
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
        this.units.face(u.id, screen);
        await this.units.raiseWeapon(u.id, screen);
        this.sfx.hack(screen);
        this.fx.sparks(screen, e.success ? 0x4dff9a : 0xff4a3a, 16);
        await wait(0.5);
        this.fx.floatText(screen, e.success ? 'ARREGLADO' : 'NO ARRANCA', e.success ? 'heal' : 'warn');
        this.hud.log(`${u.name} ${e.success ? 'arregla el equipo' : 'no consigue arreglarlo'} (${e.chance}%)`, e.success ? '#6dff9a' : '#ffc93a');
        this.sfx.hacked(e.success, screen);
        if (e.success) this.hud.banner('EQUIPO ARREGLADO', 'xcom');
        else this.hud.banner('¡ALARMA!', 'alien');
        // A failed repair sets the alarm off: the horde it brings says so (reinforcementsIncoming).
        if (!e.success) this.alarmRaised = true;
        this.units.lowerWeapon(u.id);
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
        if (e.r.horde) {
          // A horde (playtest 13/10 #1): the dead come in by an edge, and nobody knows which; the camera stays with the player.
          // The alarm's comes this very enemy turn, the Saqueo's the next one.
          const when = e.r.turn <= v.turn ? 'al final de este turno enemigo' : 'al final del próximo turno enemigo';
          const alarm = this.alarmRaised;
          this.alarmRaised = false;
          if (alarm) this.hud.banner('LA ALARMA SE OYE LEJOS', 'alien', `Llegan muertos por un borde ${when}`);
          else this.hud.banner('MÁS ZOMBIS EN CAMINO', 'alien', `Llegan por un borde ${when}`);
          const many = e.r.units.length === 1 ? 'Viene un zombi' : `Vienen ${e.r.units.length} zombis`;
          this.hud.log(alarm ? `La alarma se oye lejos. ${many} por un borde: llegan ${when}.` : `${many} más por un borde: llegan ${when}. Cuanto más ruido, antes vienen.`, '#ff6b5a');
          await wait(alarm ? 1 : 0.6);
          return;
        }
        // A drop under a flare: only missions saved before the playtest of 13/10.
        if (!e.r.pos) return;
        const at = tileToWorld(e.r.pos);
        look(at);
        this.hud.banner('VIENEN MÁS', 'alien');
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
          // A siege's wave says which of how many, once (it comes in groups), in Lore's words.
          const waves = v.mission.siege ? siegeWaves(v) : null;
          const first = !waves || v.mission.reinforcements.find((r) => r.turn === wave.turn)?.id === wave.id;
          // Without a Sala de radio it isn't known where they come from (18/10 #20): which wave, not the side.
          const line = waves ? waveEntersLine(waves.findIndex((w) => w.turn === wave.turn) + 1, waves.length) : `La horda llega por ${where}:`;
          // A siege's line is whole (who comes is «zombis» all through); a horde's names them.
          if (waves) {
            if (first) this.hud.log(line, '#ff6b5a');
          } else this.hud.log(`${line} ${e.units.map((u) => u.name).join(', ')}`, '#ff6b5a');
          // The banner counts the whole wave, all its groups.
          const n = waves ? v.mission.reinforcements.filter((r) => r.turn === wave.turn).reduce((sum, r) => sum + r.units.length, 0) : e.units.length;
          // A siege's wave comes in on every side (18/10 #20): no side on its stamp.
          // «Muertos», not «zombis»: a wave brings Hinchados and the rest among them (Gameplay, 18/10).
          if (first) this.hud.banner(waves ? '¡OLEADA!' : '¡LA HORDA!', 'alien', `${n === 1 ? 'Ha llegado un muerto' : `Han llegado ${n} muertos`}${waves ? '' : ` por ${where}`}${seen.length ? '' : ', fuera de vuestra vista'}`);
          if (!seen.length) return;
          if (!this.grouped) look(tileToWorld(seen[0]!.pos));
          await playArrival(this.units, this.fx, this.sfx, v, seen);
          for (const u of seen) this.fx.floatText(tileToWorld(u.pos, 1), `¡APARECE UN ${TEMPLATES[u.template].name.toUpperCase()}!`, 'callout');
          await wait(cam() ? 0.8 : 0.3);
          return;
        }
        this.hud.log(`Llegan más: ${e.units.map((u) => u.name).join(', ')}`, '#ff6b5a');
        // Nobody drops in unannounced: the stamp says how many, and where if the squad sees it.
        this.hud.banner('¡LLEGAN MÁS!', 'alien', `${e.units.length === 1 ? 'Ha llegado un enemigo' : `Han llegado ${e.units.length} enemigos`}${seen.length ? '' : ' fuera de vuestra vista'}`);
        if (!seen.length) return;
        if (!this.grouped) look(spots[0]!);
        // They come in on foot from the edge, or get up out of the dust: no capsules (arrivals.ts).
        await playArrival(this.units, this.fx, this.sfx, v, seen);
        for (const u of seen) this.fx.floatText(tileToWorld(u.pos, 1), `¡APARECE UN ${TEMPLATES[u.template].name.toUpperCase()}!`, 'callout');
        await wait(cam() ? 0.8 : 0.2);
        return;
      }
      case 'patrol':
      case 'forgot':
      // A light going on or out shows on the map (MapView draws the lights from the state): nothing to say.
      case 'lightOn':
      case 'lightOff':
        return;
      case 'noise':
        this.playNoise(e);
        return;
      case 'floorBroken': {
        // docs/FASE7.md §5: the slab gives way where the grenade went off; the hole opens with the rebuild after it.
        if (!this.mapView.isVisible(e.pos.x, e.pos.y)) return;
        const at = tileToWorld(e.pos, 0.1);
        look(at);
        this.fx.debris(at, 0x8f6c45, 14);
        if (cam()) this.rig.shake(0.2, 0.3);
        this.hud.log(RUBBLE_TEXT.floor, '#ffb070');
        // What the squad came for, lying there, goes down with it (its itemDropped follows).
        const item = v.mission.item;
        if (item && !item.carrier && item.pos && samePos(toPos(item.pos), e.pos)) {
          this.hud.log(RUBBLE_TEXT.item, '#ffd23a');
          this.itemFell = true;
        }
        await wait(cam() ? 0.45 : 0.2);
        return;
      }
      case 'fell': {
        // Down with the floor (docs/FASE7.md §5): the camera holds until the body lands; the damage follows as `damaged`.
        const u = getUnit(v, e.unit)!;
        // An enemy falling out of sight is neither seen nor told.
        if (!this.shownToSquad(u) && !this.mapView.isVisible(e.to.x, e.to.y)) return;
        this.hud.log(fallLine(u.name, Math.max(1, e.from.z - heightAt(v, e.to))), u.team === 'alien' ? '#54d1ff' : '#ff6b5a');
        const to = tileToWorld(e.to);
        look(this.units.worldPos(u.id).lerp(to, 0.5));
        this.fx.floatText(this.units.chestPos(u.id), RUBBLE_TEXT.fallFloat, 'alert');
        // Arte's fall resolves as it touches down.
        await this.units.fall(u.id, e.to);
        this.sfx.land(to);
        await wait(cam() ? 0.35 : 0.15);
        return;
      }
      case 'searched': {
        const u = getUnit(v, e.unit)!;
        const at = tileToWorld(e.tile, 0.6);
        const decor = searchSpot(v, e.tile);
        look(this.units.worldPos(u.id).lerp(at, 0.5));
        this.sfx.search(at);
        await this.units.rummage(u.id, at);
        // A found weapon is the rare one: its own, more striking slip.
        this.overlays.find(e.tile, findLabel(e.loot), !e.loot ? 'empty' : e.loot.weapon ? 'rare' : 'loot');
        if (e.loot) this.sfx.found(at);
        // The seed comes from the event, so both players read the same line.
        if (decor) this.hud.log(searchLine(u.name, decor, e.loot, e.tile.x * 7 + e.tile.y * 13 + v.turn), e.loot?.weapon ? '#ffd23a' : e.loot ? '#6dff9a' : undefined);
        await wait(0.5);
        return;
      }
      case 'podActivated': {
        const pod = v.pods.find((p) => p.id === e.pod);
        const all = pod?.unitIds.map((id) => getUnit(v, id)!).filter((m) => m.alive) ?? [];
        // Woken by the other enemies (18/10 #47), not by the squad: no contact, and only those in sight are seen.
        const feud = wokenByFoes(e);
        const members = feud ? all.filter((m) => this.shownToSquad(m)) : all;
        if (!members.length) return;
        if (feud) {
          for (const m of members) {
            this.fx.floatText(this.units.chestPos(m.id), '!', 'alert');
            void this.units.alert(m.id);
          }
          this.hud.log(`Vivos y muertos se pelean: ${members.map((m) => m.name).join(', ')}`, FEUD_COLOR);
          await wait(0.5);
          return;
        }
        const center = members.reduce((acc, m) => acc.add(tileToWorld(m.pos)), new THREE.Vector3()).divideScalar(members.length);
        // With others of the same moment (playTogether), the framing is theirs together.
        if (!this.grouped) {
          look(center);
          if (cam()) await wait(0.35);
        }
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
        // An enemy turn nobody will see opens without its banner (docs/UX-RITMO.md): «TU TURNO» says enough.
        if (e.team === 'alien' && !this.enemyTurnShows()) return;
        this.sfx.turn(e.team === 'alien', !v.concealed);
        // Whose turn opens: mine, the partner's (they hold the command) or the enemy's.
        const partner = e.team === 'xcom' && !!this.partnerHere() && e.command !== undefined && e.command !== this.mySlot;
        this.hud.banner(e.team === 'alien' ? 'TURNO ENEMIGO' : partner ? `TURNO DE ${this.playerName(e.command).toUpperCase()}` : 'TU TURNO', e.team === 'xcom' ? 'xcom' : 'alien');
        // The squad's turn: the camera to the soldier who plays first (#47).
        if (e.team === 'xcom') this.turnCamera(this.turnSoldier(v, e.command ?? this.mySlot));
        // Time running out (playtest 18/10 #8, #9, Lore's words): with 5 turns left, and on the last, after which
        // La Lata leaves by itself. Not in a siege, nor once the objective is done (the clock is gone then).
        const left = v.mission.turnsLeft !== null && e.turn > 1 ? v.mission.turnsLeft - 1 : v.mission.turnsLeft;
        if (e.team === 'xcom' && !v.mission.siege && !v.mission.objectiveDone && (left === 5 || left === 1)) {
          const line = left === 1 ? TIME_TEXT.lastTurn : turnsLeftWarning(left);
          this.hud.log(line, '#ff6b5a');
          this.hud.toast(line);
        }
        await wait(1);
        return;
      }
      case 'commandPassed': {
        this.sfx.command();
        if (this.partnerHere()) {
          if (e.to === this.mySlot) this.hud.banner('TU TURNO', 'xcom', 'Tienes el mando: das tú las órdenes');
          else this.hud.banner(`TURNO DE ${this.playerName(e.to).toUpperCase()}`, 'xcom', this.state.ready[this.mySlot] ? `Has terminado: juega ${this.playerName(e.to)}` : 'Mientras, puedes apuntar y planear');
        }
        // To the soldier who plays now: the one in play of whoever takes the command, or their first with actions (#47).
        this.turnCamera(this.turnSoldier(v, e.to));
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

  /** Melee lunge, or aim + burst for ranged weapons; then hit or miss feedback. */
  private async playAttack(e: ShotEvent, cam: () => boolean, look: (p: THREE.Vector3) => void): Promise<void> {
    const v = this.view;
    const shooter = getUnit(v, e.unit)!;
    const target = getUnit(v, e.target)!;
    this.lastShot = { target: target.id, result: e.result };
    this.lastAttacker = shooter.id;
    if (!this.shownToSquad(shooter) && !this.shownToSquad(target)) return;

    const weapon = attackWeapon(shooter, e.ability);
    const shooterPos = this.units.worldPos(shooter.id);
    const targetPos = this.units.worldPos(target.id);
    const chest = this.units.chestPos(target.id);
    const hit = e.result !== 'miss';
    const color = shooter.team === 'xcom' ? 0x9fe8ff : 0xff6b5a;
    if (v.realtime) return this.rtAttack(e, shooter, target, color);
    if (cam()) {
      look(shooterPos.clone().lerp(targetPos, 0.5));
      this.rig.cinematicZoom(THREE.MathUtils.clamp(shooterPos.distanceTo(targetPos) * 1.1 + 9, 13, 24));
    }
    if (e.reaction) {
      this.fx.floatText(this.units.chestPos(shooter.id), weapon.melee ? 'TAJO DE REACCIÓN' : 'FUEGO DE REACCIÓN', 'warn');
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

    // Out from behind the cover to shoot (los.ts peekOrigins), onto the tile beside; with someone or something
    // there (the rules don't ask), it leans out towards it instead of stepping into it.
    const stepOut = !samePlace(e.from, shooter.pos);
    if (stepOut) {
      const beside = tileToWorld(e.from);
      const out = this.units.clearFor(e.from, shooter.id) ? beside : shooterPos.clone().add(beside.sub(shooterPos).setY(0).setLength(LEAN_OUT));
      await this.units.slide(shooter.id, out, 0.22, false);
    }
    const aim = await this.units.raiseWeapon(shooter.id, targetPos, firesSidearm(shooter, e.ability));
    await wait(cam() ? 0.25 : 0.08);

    const heavy = weapon.apCost > 1;
    const pellets = weapon.range === 'short' ? 6 : 0;
    const rounds = Math.max(1, weapon.burst);
    for (let i = 0; i < rounds; i++) {
      const from = this.units.muzzlePos(shooter.id);
      this.units.recoil(shooter.id);
      this.fx.muzzleFlash(from, color);
      this.sfx.shot(shooter, weapon, from);
      if (heavy && cam()) this.rig.shake(0.08, 0.15);
      const last = i === rounds - 1;
      const shots = pellets || 1;
      for (let p = 0; p < shots; p++) {
        const dest = hit ? chest.clone().add(jitter(pellets ? 0.6 : 0.25)) : missPoint(from, chest, i + p, pellets ? 1.6 : 1);
        void this.fx.bullet(from, dest, color, heavy ? 45 : 60, heavy).then(() => {
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
      this.units.lowerWeapon(shooter.id, aim);
      if (stepOut) void this.units.slide(shooter.id, tileToWorld(shooter.pos), 0.25);
    });
  }

  /**
   * Real time (docs/TIEMPO-REAL.md §6b): only the shot itself, at once, for the pose is the aim's (Arte's
   * rtDoing: the gun up from the aim's start, no stepping out of cover) and the blow's wind-up ends on it.
   */
  private async rtAttack(e: ShotEvent, shooter: Unit, target: Unit, color: number): Promise<void> {
    const weapon = attackWeapon(shooter, e.ability);
    const chest = this.units.chestPos(target.id);
    const hit = e.result !== 'miss';
    if (weapon.melee) {
      if (hit) {
        this.fx.slash(chest, 0xe8f6ff);
        if (e.ability === 'claw') this.fx.bite(chest, this.units.worldPos(shooter.id));
        this.sfx.melee(e.ability === 'claw' ? 'claw' : 'sword', chest);
        this.sfx.impact(this.sfx.body(target), chest, e.result === 'crit');
      } else this.showMiss(e, shooter, target, chest);
      return;
    }
    const heavy = weapon.apCost > 1;
    const pellets = weapon.range === 'short' ? 6 : 0;
    const rounds = Math.max(1, weapon.burst);
    for (let i = 0; i < rounds; i++) {
      const from = this.units.muzzlePos(shooter.id);
      this.units.recoil(shooter.id);
      this.fx.muzzleFlash(from, color);
      this.sfx.shot(shooter, weapon, from);
      const last = i === rounds - 1;
      for (let p = 0; p < (pellets || 1); p++) {
        const dest = hit ? chest.clone().add(jitter(pellets ? 0.6 : 0.25)) : missPoint(from, chest, i + p, pellets ? 1.6 : 1);
        void this.fx.bullet(from, dest, color, heavy ? 45 : 60, heavy).then(() => {
          this.fx.sparks(dest, hit ? 0xff7a5a : 0xffd28a, pellets ? 3 : hit ? 8 : 5);
          if (last && p === 0) this.sfx.impact(hit ? this.sfx.body(target) : 'miss', dest, e.result === 'crit');
        });
      }
      if (!last) await wait(weapon.burst > 3 ? 0.08 : 0.11);
    }
    if (!hit) this.showMiss(e, shooter, target, chest);
  }

  /**
   * A miss, with the die (playtest: «he fallado con un 93 %»): short over the
   * target, whole in the log. A player's miss also says what bad luck adds to
   * that player's next shot (aim.ts badLuckAim).
   */
  private showMiss(e: ShotEvent, shooter: Unit, target: Unit, chest: THREE.Vector3): void {
    this.fx.floatText(chest, e.roll ? `FALLO · SACÓ ${e.roll}` : 'FALLO', 'miss');
    // Real time: the log keeps what changes the mission, not every roll (Gameplay, 20/10).
    if (this.view.realtime) return;
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
    this.lastAttacker = u.id;
    const def = ABILITIES[e.ability];
    const to = tileToWorld(e.target, 0.15);
    // An enemy's blast nobody sees, thrown from out of sight, costs no time (docs/UX-RITMO.md).
    if (u.team === 'alien' && !this.shownToSquad(u) && !this.mapView.isVisible(e.target.x, e.target.y)) return;
    // Whoever dies in it falls away from the blast (Arte's deaths, unitViews).
    this.units.blastAt(to);
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
    if (e.ability === 'missiles') {
      const from = this.units.chestPos(u.id).add(new THREE.Vector3(0, 0.5, 0));
      const spots = [to, ...Array.from({ length: 3 }, () => to.clone().add(new THREE.Vector3((Math.random() - 0.5) * 2, 0, (Math.random() - 0.5) * 2)))];
      this.sfx.missile(from);
      await this.fx.missiles(from, spots);
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
    // docs/FASE7.md §4: the walls it breaks, said once for the whole blast; each comes down with its tileChanged.
    const walls = breachedWalls(this.view, e.ability, e.target, u).length;
    if (walls && this.mapView.isVisible(e.target.x, e.target.y)) this.hud.log(breachLine(walls), '#ffb070');
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
    // The alarm's own noise: its horde's stamp follows and says it all; here only the line.
    if (!this.alarmRaised) this.hud.banner('OS HAN OÍDO', 'warn', capitalize(text));
    this.hud.log(`Os han oído: ${text}`, '#ffc93a');
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
    // The mission over before its briefing was closed (rejoining mid-mission, the partner abandoning): it goes, so the outcome is seen.
    this.hud.closeIntro(true);
    const s = this.state;
    const lost = s.units.filter((u) => u.team === 'xcom' && !u.alive && !u.evacuated && !u.lost).length;
    const left = s.units.filter((u) => u.lost).length;
    const evacuated = s.units.filter((u) => u.evacuated).length;
    const killed = s.units.filter((u) => u.team === 'alien' && !u.alive).length;
    const vip = s.mission.kind === 'rescue' && s.mission.objectiveUnit ? getUnit(s, s.mission.objectiveUnit)?.name : undefined;
    // What came home (docs/FASE2.md): only what a soldier got out with, or stood with at the end.
    const haul = broughtBack(s);
    const loot = haulText(haul);
    const success: Record<typeof s.mission.kind, string> = {
      elimination: 'Todos los hostiles abatidos',
      recovery: `${capitalize(this.recoveryItem())} a salvo`,
      sabotage: 'Ya no suena',
      hack: 'Equipo en marcha',
      rescue: vip ? `${vip}, a salvo` : 'VIP extraído',
      scavenge: loot ? SCAVENGE.success : SCAVENGE.emptyHanded,
    };
    const titles: Record<EndReason, string> = {
      // Won as La Lata left on time, the objective aboard.
      objective: this.departedOnTime ? TIME_TEXT.wonOnTime : success[s.mission.kind],
      wiped: evacuated ? 'La escuadra se retiró sin cumplir el objetivo' : 'Escuadra eliminada',
      // Time ran out (playtest 18/10 #8): La Lata left with somebody aboard but not the objective, with nobody, or there was none.
      timer: !s.mission.evacZone.length ? TIME_TEXT.timeUp : s.units.some((u) => u.team === 'xcom' && u.evacuated) ? TIME_TEXT.lostOnTime : TIME_TEXT.leftEmpty,
      // Gone in La Mula too early (P1): a Limpieza is a retreat; the rest, out without the objective.
      abandoned: s.mission.evacDone ? (s.mission.kind === 'elimination' ? EVAC_TEXT.retreat : EVAC_TEXT.withoutObjective) : 'Misión abandonada',
      objectiveLost: vip ? `${vip} ha muerto` : 'El VIP ha muerto',
    };
    // A siege (docs/FASE12.md §10, Lore's words): the Bastión holds, with dead still out or none standing; or it falls.
    if (s.mission.siege) {
      const standing = s.units.some((u) => u.alive && u.team === 'alien');
      Object.assign(titles, { objective: standing ? SIEGE_TEXT.held : SIEGE_TEXT.cleared, wiped: SIEGE_TEXT.wiped });
    }
    // One short line: why it ended. A skirmish has no debrief after, so it adds the count.
    const why = reason ? titles[reason] : '';
    const count = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;
    // A skirmish also says what came home (a campaign's debrief has it, from report.loot): «Botín a salvo: +26 §».
    const found = !s.campaignMission && outcome === 'victory' && loot ? loot.replace(/\.$/, '') : '';
    const head = found && reason === 'objective' && s.mission.kind === 'scavenge' ? `${why}: ${found.replace(/^El botín: /, '')}` : [why, found].filter(Boolean).join(' · ');
    const line = s.campaignMission
      ? why
      : [head, count(killed, 'abatido', 'abatidos'), lost ? count(lost, 'baja', 'bajas') : 'sin bajas', left ? (left === 1 ? '1 se quedó fuera' : `${left} se quedaron fuera`) : ''].filter(Boolean).join(' · ');
    this.hud.showOutcome(outcome, line, !!s.campaignMission, !!s.mission.siege);
  }

  /** Re-derives fog, alien visibility, flags and mission markers from the shown state. */
  private refreshView(): void {
    this.mapView.setVisibility(teamVisibility(this.view, 'xcom'));
    // In a siege the base has no fog (playtest 18/10 #18): by day every enemy is in sight. By night the ground
    // still is, but the dead in the dark are not, as on any night (Arturo, 18/10: the floodlights are for that).
    const allSeen = this.view.mission.siege && !this.view.mission.night;
    this.visibleAliens = new Set((allSeen ? this.view.units.filter((u) => u.alive && u.team === 'alien') : visibleEnemies(this.view, 'xcom')).map((u) => u.id));
    // Only those that shoot: cover means nothing against the dead, who come to bite (docs/ZOMBIS-PRIMERO.md).
    const watchers = this.view.units.filter((a) => a.alive && a.team === 'alien' && this.visibleAliens.has(a.id) && shootsFromRange(a));
    const selected = this.selected ? getUnit(this.view, this.selected) : undefined;
    this.units.sync(this.view, this.visibleAliens, (u) => {
      if (u.team === 'xcom') {
        // With no shooter in sight nobody flanks anyone: the shield only shows cover, never the red "flanked".
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
    // A siege's breaches, an orange beam each (the dead go for the squad now: no heart to mark, 18/10 #17).
    for (const pos of this.breaches()) beacons.push({ pos, color: 0xff8a2a });
    this.overlays.setBeacons(beacons);
    this.overlays.setFlares(m.reinforcements.filter((r) => r.pos && !r.landed).map((r) => r.pos!));
    this.overlays.setBreakables(this.breakables());
    // Where a fallen soldier's rucksack lies (#49): «MOCHILA DE CHIARA», once per tile and soldier.
    const packs = new Map<string, { pos: Place; label: string }>();
    for (const d of this.view.dropped ?? []) {
      const who = d.from ? getUnit(this.view, d.from) : undefined;
      if (who) packs.set(`${d.from}:${d.pos.x},${d.pos.y},${d.pos.z ?? 0}`, { pos: d.pos, label: fallenPackMark(who.name) });
    }
    this.overlays.setFallenPacks([...packs.values()]);
    this.overlays.setSmoke(this.view.smoke);
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
    // Not one up on a storey the cutaway hides: its ring would float over the cut (its flag says where it is).
    const watchers = show ? v.units.filter((a) => a.alive && a.team === 'alien' && this.visibleAliens.has(a.id) && TEMPLATES[a.template].detection > 0 && !this.mapView.isCutAway(a.pos)) : [];
    // The enemies notice in their cone (and at arm's length all round): the ring is a keyhole (Arturo, 17/10; the living too, 18/10 #46).
    this.overlays.setDetection(watchers.map((a) => ({ pos: a.pos, radius: this.darkDetection(a), facing: this.lookOf(a) })));
  }

  /**
   * What enemy `a` sees from where it stands: the tiles on its floor in its
   * cone (los.ts inViewCone: the enemies see VIEW_CONE ahead and AWARE_RANGE
   * all round, dead or living) and in its line of sight, as far as it sees there (sightTo, shorter
   * in the dark); and, while the squad is hidden, those it would notice a
   * soldier on (detectionTo). Worked out once per enemy and state.
   */
  private visionOf(a: Unit): Vision {
    const s = this.view;
    const key = `${a.id}:${s.seq}:${s.concealed}:${a.pos.x},${a.pos.y},${a.pos.z ?? 0}:${a.facing}`;
    if (this.visionCache?.key === key) return this.visionCache.v;
    const range = sightOf(a);
    const r = Math.ceil(range);
    const seen: Place[] = [];
    const noticed: Place[] = [];
    for (let y = Math.max(0, a.pos.y - r); y <= Math.min(s.height - 1, a.pos.y + r); y++) {
      for (let x = Math.max(0, a.pos.x - r); x <= Math.min(s.width - 1, a.pos.x + r); x++) {
        if (x === a.pos.x && y === a.pos.y) continue;
        const p = { x, y, z: a.pos.z };
        if (!canStandOn(kindAt(s, p)) || !inViewCone(a, p) || sightOrigin(s, a.pos, p, sightTo(s, a, p)) === null) continue;
        seen.push(p);
        if (s.concealed && distance(a.pos, p) <= detectionTo(s, a, p)) noticed.push(p);
      }
    }
    const v: Vision = { pos: a.pos, facing: a.facing, range, seen, noticed };
    this.visionCache = { key, v };
    return v;
  }

  /**
   * Where an enemy looks right now, for its cone's edge: its model's turn
   * (Arte's unitViews.facingNow, 0.6 s on a noise), so the cone turns with it
   * and never ahead of it; the engine's facing at rest. Without one, none.
   */
  private lookOf(a: Unit): number | undefined {
    if (a.team === 'xcom' || a.facing === undefined) return undefined;
    const now = this.units.facingNow(a.id);
    return Math.round((now ?? a.facing) * 100) / 100;
  }

  /** Every frame: while an enemy in sight is turning, its cones are drawn again as it goes; once more when it stops. */
  private followTurns(): void {
    const turning = this.view.units.some((a) => {
      if (!a.alive || a.team !== 'alien' || a.facing === undefined || !this.visibleAliens.has(a.id)) return false;
      const now = this.units.facingNow(a.id);
      return now !== null && Math.abs(Math.atan2(Math.sin(now - a.facing), Math.cos(now - a.facing))) > 0.01;
    });
    if (!turning && !this.wasTurning) return;
    this.wasTurning = turning;
    this.drawDetection();
    const watched = this.watched ? getUnit(this.view, this.watched) : undefined;
    if (watched?.alive) this.overlays.setVision({ ...this.visionOf(watched), facing: this.lookOf(watched) });
  }

  /** How close a soldier out of the light can get to `a` unseen: detectionTo's range on an unlit tile. */
  private darkDetection(a: Unit): number {
    const day = TEMPLATES[a.template].detection;
    return this.view.mission.night ? day * NIGHT.detection : day;
  }

  private marks(): EdgeMark[] {
    const marks = this.missionMarks();
    // Its breaches, one mark a side (a gate gives way leaf by leaf, a barricade tile by tile).
    const sides = new Map<number, Place[]>();
    for (const pos of this.breaches()) {
      const side = sideOf(this.view, pos);
      sides.set(side, [...(sides.get(side) ?? []), pos]);
    }
    for (const [side, ps] of sides) {
      const pos = { x: Math.round(ps.reduce((n, p) => n + p.x, 0) / ps.length), y: Math.round(ps.reduce((n, p) => n + p.y, 0) / ps.length) };
      marks.push({ kind: 'alert', label: breachMark(side), pos });
    }
    return this.moment ? [...marks, { kind: 'alert', label: this.moment.who, pos: this.moment.pos }] : marks;
  }

  /**
   * A siege's doors and barricades that gave way (GameState.breakables at 0),
   * seen or not (Gameplay, 18/10: a breach is what the player must know):
   * their marks on the board and at the screen's edge. In the state, so a
   * reload or a player joining midway sees them too.
   */
  private breaches(): Place[] {
    const s = this.view;
    return Object.entries(s.breakables ?? {})
      .filter(([, left]) => left <= 0)
      .map(([node]) => posOf(s, Number(node)));
  }

  /** A siege's door or barricade gave way at `tile`, as it happens: the log says which and where, seen or not (the marks: breaches). */
  private noteBreach(tile: Place, kind: 'door' | 'barricade'): void {
    this.hud.log(giveWayLine(kind, sideOf(this.view, tile)), '#ff6b5a');
  }

  /** What a siege's door or barricade at `tile` still holds, in Lore's words («Puerta: aguanta 12 de 18»); '' elsewhere, or out of sight. */
  private breakableNote(tile: Place): string {
    const left = this.view.breakables?.[nodeOf(this.view, { x: tile.x, y: tile.y, z: tile.z ?? 0 })];
    if (!left || !this.mapView.isVisible(tile.x, tile.y)) return '';
    const b = breakableOf(this.view, tile);
    return breakableTip(b.kind, left, b.full);
  }

  /** A siege's doors and barricades still standing (GameState.breakables) that the squad sees: where, which, and what they hold. */
  private breakables(): { pos: Place; kind: 'door' | 'barricade'; left: number; full: number }[] {
    const s = this.view;
    return Object.entries(s.breakables ?? {})
      .map(([node, left]) => ({ pos: posOf(s, Number(node)), left }))
      .filter((b) => b.left > 0 && this.mapView.isVisible(b.pos.x, b.pos.y))
      .map((b) => ({ ...b, ...breakableOf(s, b.pos) }));
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
        if (wokenByFoes(e)) return;
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
        // Lore's line without its full stop, like the partner's other notices.
        else if (u?.objective) text = alarmDoneLine(who, v.mission.alarm).replace(/\.$/, '');
        else return;
        pos = u.pos;
        break;
      }
      case 'itemPicked':
        pos = at(e.unit);
        text = itemPartnerLine(who, this.recoveryItem());
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
      case 'alarm':
        return `${soldier ? `${who} no ha podido arreglarlo` : 'El arreglo ha fallado'}: ha saltado la alarma.`;
      case 'reinforcements':
        return 'Los que llegan vienen alertados: saben dónde estáis.';
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

  /**
   * «Ver» (playtest 16/10 #8): with the partner holding the command, the
   * camera goes to what they are doing: their soldier in play, else the last
   * one they moved; on its floor.
   */
  private watchPartner(): void {
    const s = this.state;
    const inPlay = s.activeUnit ? getUnit(this.view, s.activeUnit) : undefined;
    const id = inPlay && inPlay.owner !== this.mySlot ? inPlay.id : this.partnerLast;
    const u = id ? getUnit(this.view, id) : undefined;
    if (!u?.alive) {
      this.hud.toast(`Todavía no hay nada que ver: ${this.playerName(s.command)} no ha movido a nadie.`);
      return;
    }
    this.setLevel(u.pos.z ?? 0);
    this.rig.takeControl();
    this.rig.focus(this.units.worldPos(u.id));
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
    const bus = this.evacCentre(s);
    return bus ? [{ kind: 'evac', label: 'Evacuación', pos: bus }] : [];
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

  /**
   * Plans a move for `u` (or withdraws it, clicking the same tile again); the partner sees it. A plan
   * changed counts from when it changed (#24: they are walked in that order): out, and in at the end.
   */
  private togglePlan(u: Unit, tile: Place): void {
    const current = this.plans.get(u.id);
    if (current && samePlace(current.tile, tile)) return this.withdrawPlan(u);
    this.plans.delete(u.id);
    this.plans.set(u.id, { slot: this.mySlot, tile });
    this.net.send({ t: 'plan', unit: u.id, tile });
    this.drawPlans();
    this.hud.toast(`Plan para ${u.name}: lo ve ${this.playerName(this.state.command)}. Otro clic ahí lo quita.`);
  }

  /** Takes `u`'s plan away (the same tile again, or a right click while planning: #25); the partner sees it go. */
  private withdrawPlan(u: Unit): void {
    this.plans.delete(u.id);
    this.net.send({ t: 'plan', unit: u.id, tile: null });
    this.drawPlans();
    this.hud.toast('Plan retirado.');
  }

  /**
   * Draws every plan along the path it would take now; one it no longer
   * reaches keeps its ring alone (playtest 18/10 #3: it stays, marked). One's
   * own while the partner holds the command, in one's colour dimmed, like
   * the rest of the preview (#49; 18/10 #4: grey didn't show); theirs, in
   * their colour.
   */
  // ============================================================ real time

  /**
   * Real time (docs/TIEMPO-REAL.md §6b): a tick's events, applied as they come and shown at once, nothing waiting
   * for anything (each step lasts until its `until`; shots, hits and deaths at once, «cada acción se ve entera»
   * does not hold here) and the camera left alone.
   */
  private realtimeBatch(events: GameEvent[], by: Slot | null): void {
    const v = this.view;
    const clock = events.find((e): e is Extract<GameEvent, { t: 'rtClock' }> => e.t === 'rtClock')?.clock ?? v.realtime?.clock ?? 0;
    let rebuild = false;
    for (const e of events) {
      if (e.t === 'moved') this.rtStep(e, events, clock);
      else if (e.t === 'rtDoing') {
        const target = e.target ? getUnit(v, e.target) : undefined;
        this.units.rtDoing(e.unit, e.doing, Math.max(0, (e.until - clock) / 1000), target ? this.units.worldPos(target.id) : null);
        this.rtSince.set(e.unit, clock);
      } else if (!RT_QUIET.has(e.t)) void this.play({ e, by, epoch: this.rig.epoch }).catch((err) => console.error('[anim]', err));
      if (e.t === 'tileChanged' || e.t === 'floorBroken') rebuild = true;
      // The pause, in the log too: who stopped the clock or set it going (Lore's words).
      if (e.t === 'rtPaused') {
        const who = e.slot !== null ? this.playerName(e.slot) : null;
        this.hud.log(who ? `${who} ${e.paused ? 'pausa' : 'reanuda'} la misión.` : e.paused ? 'Pausa.' : 'En marcha.', '#f2c230');
      }
      applyEvent(v, e);
    }
    this.rtHeard = { clock, at: performance.now() };
    if (rebuild) this.mapView.rebuild(v);
    this.refreshView();
    this.refreshStatus();
    this.refreshHud();
    this.drawOrders();
    this.updateHover();
  }

  /** A step (`moved`, one tile), walked so it ends when its `rtDoing walk` of the same tick says; the hidden aren't drawn. */
  private rtStep(e: Extract<GameEvent, { t: 'moved' }>, events: GameEvent[], clock: number): void {
    const u = getUnit(this.view, e.unit);
    if (!u || !(this.shownToSquad(u) || e.path.some((p) => this.mapView.isVisible(p.x, p.y)))) return;
    const walk = events.find((x): x is Extract<GameEvent, { t: 'rtDoing' }> => x.t === 'rtDoing' && x.unit === e.unit && x.doing === 'walk');
    const seconds = walk ? Math.max(0.05, (walk.until - clock) / 1000) : RT_STEP / 1000;
    this.units.forceVisible(u.id);
    void this.units.rtStep(u.id, e.path, seconds);
  }

  /** The mission's clock now, between two of the host's ticks; still while paused. */
  private rtNow(): number {
    const rt = this.view.realtime;
    if (!rt) return 0;
    if (rt.paused || this.view.outcome) return this.rtHeard.clock;
    return this.rtHeard.clock + Math.min(RT_TICK * 2, performance.now() - this.rtHeard.at);
  }

  /** What each of the squad does, for its card (#3 of the step): a word, and the bar of an aim or a reload. */
  private rtCard(u: Unit): { text: string; title?: string; from?: number; until?: number } | null {
    const rt = this.view.realtime;
    if (!rt || !u.alive || u.evacuated) return null;
    const me = rt.units[u.id];
    if (!me) return { text: RT_TEXT.idle };
    const target = me.target ? getUnit(this.view, me.target) : undefined;
    const timed = me.doing === 'aim' || me.doing === 'reload';
    const bar = timed ? { from: this.rtSince.get(u.id) ?? rt.clock, until: me.until } : {};
    switch (me.doing) {
      case 'walk':
        return { text: RT_TEXT.walk };
      case 'aim':
        // Lore's: the kind's name, so the longest («Apuntando: Acorazado») still fits.
        return { text: target ? `${RT_TEXT.aim}: ${TEMPLATES[target.template].name}` : RT_TEXT.aim, ...bar };
      case 'reload':
        return { text: RT_TEXT.reload, ...bar };
      case 'wait':
        return { text: RT_TEXT.wait, title: RT_TEXT.waitWhy };
      default:
        return { text: me.order.kind === 'move' ? RT_TEXT.walk : RT_TEXT.idle };
    }
  }

  /**
   * One's own soldiers' standing orders on the ground (#2 of the step): the way to where each goes, in their
   * colour; a line to whom each attacks, in red.
   */
  private drawOrders(): void {
    const rt = this.view.realtime;
    if (!rt) return;
    const list: { from: Vec2; path: Vec2[]; color: string }[] = [];
    for (const u of this.myUnits()) {
      const order = rt.units[u.id]?.order;
      if (order?.kind === 'move') {
        const key = `${u.pos.x},${u.pos.y},${u.pos.z ?? 0}>${order.to.x},${order.to.y},${order.to.z}`;
        let path = this.rtRoutes.get(u.id);
        if (path?.key !== key) {
          const reach = computeReach(this.view, u, 100_000, u.pos, () => true);
          path = { key, steps: buildPath(this.view, u, reach, order.to) ?? [order.to] };
          this.rtRoutes.set(u.id, path);
        }
        list.push({ from: u.pos, path: path.steps, color: PLAYER_COLORS[u.owner ?? this.mySlot] });
      } else if (order?.kind === 'attack') {
        const target = getUnit(this.view, order.target);
        if (target?.alive) list.push({ from: u.pos, path: [target.pos], color: '#ff5a4a' });
      }
    }
    this.overlays.setPlans(list);
  }

  /**
   * The pointer in real time (#2 of the step): no range nor actions; over an enemy in sight, the line to it from the
   * picked soldier; over a tile, the way there.
   */
  private rtHover(u: Unit | undefined, tile: Place | null, hovered: Unit | undefined): void {
    this.overlays.showAim(null, null);
    if (!u?.alive || !tile || !this.commands(u) || this.view.outcome) return;
    if (hovered?.alive && hovered.team === 'alien' && this.visibleAliens.has(hovered.id)) {
      this.overlays.showAim(u.pos, hovered.pos);
      return;
    }
    if (hovered?.alive) return;
    const key = `${u.id}@${u.pos.x},${u.pos.y},${u.pos.z ?? 0}#${this.view.seq}`;
    if (this.rtReach?.key !== key) this.rtReach = { key, reach: computeReach(this.view, u, 100_000, u.pos, () => true) };
    const path = buildPath(this.view, u, this.rtReach.reach, tile);
    if (path?.length) this.overlays.showPath(u.pos, path);
  }

  /** A click on the map in real time (#2 of the step): one of one's own picks it; an enemy in sight, attack; a tile, go. */
  private rtClick(tile: Place): void {
    const occupant = unitAt(this.view, tile);
    if (occupant?.alive && occupant.team === 'xcom' && this.commands(occupant)) return this.select(occupant.id);
    const u = this.selectedUnit();
    if (!u?.alive || !this.commands(u) || this.view.outcome) return;
    if (occupant?.alive && occupant.team === 'alien' && this.visibleAliens.has(occupant.id)) this.send({ type: 'rtOrder', unit: u.id, target: occupant.id });
    else this.send({ type: 'rtOrder', unit: u.id, to: { x: tile.x, y: tile.y, z: tile.z ?? 0 } });
  }

  /** The picked soldier stops and stays (the right click, S). */
  private rtStand(): void {
    const u = this.selectedUnit();
    if (u?.alive && this.commands(u) && !this.view.outcome) this.send({ type: 'rtOrder', unit: u.id });
  }

  /** The clock stopped or going again (the space bar, «Empezar»). */
  private rtPause(paused = !this.state.realtime?.paused): void {
    if (this.state.realtime && !this.state.outcome) this.send({ type: 'rtPause', paused });
  }

  private drawPlans(): void {
    const list: { from: Vec2; path: Vec2[]; color: string }[] = [];
    for (const [id, plan] of this.plans) {
      const u = getUnit(this.state, id);
      if (!u?.alive) {
        this.plans.delete(id);
        continue;
      }
      const color = plan.slot === this.mySlot && !this.hasCommand() ? this.overlays.planHex : PLAYER_COLORS[plan.slot];
      const reach = computeReach(this.state, u, maxMoveCost(u));
      const path = reach.cost.has(nodeOf(this.state, toPos(plan.tile))) ? buildPath(this.state, u, reach, plan.tile) : null;
      list.push(path?.length ? { from: u.pos, path, color } : { from: plan.tile, path: [plan.tile], color });
    }
    this.overlays.setPlans(list);
  }

  /**
   * With the command (playtest 18/10 #3, «Seguir las rutas planeadas» in
   * Ajustes), one's planned routes are walked in the order they were planned
   * (#24), each with one ordinary move the engine checks as any other, and
   * the next once that one has been seen whole. One soldier at a time still
   * (15/10 #9): while another of one's own is in play and the partner still
   * plays, the next waits, quietly, and goes as soon as it can. One that no
   * longer reaches its tile keeps its plan, said once, and the next goes.
   */
  private followPlan(): void {
    if (!graphics().autoPlan || !this.hasCommand()) {
      this.following = false;
      return;
    }
    if (!this.canAct()) return;
    for (const [id, plan] of [...this.plans]) {
      if (plan.slot !== this.mySlot) continue;
      const u = getUnit(this.state, id);
      if (!u?.alive || u.ap <= 0) continue;
      const key = `${id}:${plan.tile.x},${plan.tile.y},${plan.tile.z ?? 0}`;
      if (this.planSkip.has(key)) continue;
      // Another of one's own still in play: this one waits, as it is, and so do those after it.
      if (this.waitingFor(u.id)) return;
      // Ordered last time and still here: the engine refused it. Passed over, as one that doesn't reach.
      const refused = this.planSent === key;
      const reach = computeReach(this.state, u, maxMoveCost(u));
      if (refused || !reach.cost.has(nodeOf(this.state, toPos(plan.tile)))) {
        this.planSkip.add(key);
        const line = planBlockedLine(u.name);
        this.hud.log(line, PLAYER_COLORS[this.mySlot]);
        this.hud.toast(line);
        this.drawPlans();
        continue;
      }
      if (this.selected !== u.id) this.select(u.id, true);
      this.hud.log(planFollowedLine(u.name), PLAYER_COLORS[this.mySlot]);
      this.planSent = key;
      this.send({ type: 'move', unit: u.id, to: plan.tile });
      return;
    }
    this.following = false;
  }

  /** Why an order cannot go out yet: the partner holds the command. */
  private commandLock(): string {
    return `Tiene el mando ${this.playerName(this.state.command)}: puedes apuntar y planear, pero no dar la orden.`;
  }

  /** The confirm button's text while planning (null with the command). */
  private lockLabel(): string | undefined {
    return this.hasCommand() ? undefined : `MANDO: ${this.playerName(this.state.command).toUpperCase()}`;
  }

  /**
   * Whether this player gives orders to `u` (rules.ts commandsFor): its own, the shared ones, and the
   * partner's while the partner is gone (Arturo, 17/10: «si juego solo, debería poder usarlo solo»).
   */
  private commands(u: Unit): boolean {
    // A siege's turret fires on its own (docs/FASE12.md §11.4): nobody picks it or gives it orders.
    return u.team === 'xcom' && !u.automaton && commandsFor(this.mySlot, u.owner, this.activeSlots());
  }

  /**
   * The players who count as here (the server's activeSlots, room.ts): connected, or dropped within
   * its grace, longer from another app. Past it, their soldiers take the other player's orders.
   */
  private activeSlots(): Slot[] {
    const now = performance.now();
    return this.players.filter((p) => p.connected || now - (this.droppedAt.get(p.slot) ?? -Infinity) < (p.away ? AWAY_GRACE_MS : RECONNECT_GRACE_MS)).map((p) => p.slot);
  }

  /** The partner who is gone and has soldiers on the field: this player commands them too. */
  private partnerGone(): PlayerInfo | undefined {
    const active = this.activeSlots();
    return this.players.find((p) => p.slot !== this.mySlot && !active.includes(p.slot) && this.state.units.some((u) => u.alive && u.team === 'xcom' && u.owner === p.slot));
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
    return this.players.find((p) => p.slot === slot)?.name ?? 'el otro jugador';
  }

  /**
   * Turns go one soldier at a time (playtest 15/10 #9; rules.ts, `inPlay`): while one of mine is in play with
   * actions and the partner can still play, another of mine waits. The host's own answer, or null.
   */
  private waitingFor(id: string): string | null {
    const s = this.state;
    const busy = s.activeUnit && s.activeUnit !== id ? getUnit(s, s.activeUnit) : undefined;
    if (!busy?.alive || busy.ap <= 0 || busy.owner !== this.mySlot || !this.hasCommand()) return null;
    const partner = this.partnerHere();
    if (!partner || s.ready[partner.slot] || !busySlots(s).has(partner.slot)) return null;
    return `Primero termina con ${busy.name}, o cede el turno.`;
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
    if (!partner) this.hud.toast('El otro jugador no está conectado.');
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
    // Picked with no actions left: only to look (autoSelect leaves it); it doesn't change who is in play or the command.
    this.looking = u && u.ap <= 0 && this.canAct() ? u.id : null;
    if (u && focus) this.rig.focus(tileToWorld(u.pos));
    // Picking another of one's own while one is in play is allowed, to look: it says why it can't act yet.
    const waiting = u && this.commands(u) && u.ap > 0 ? this.waitingFor(u.id) : null;
    if (waiting) this.hud.toast(waiting);
    this.refreshInteractive();
    this.sendPresence();
  }

  /** Keeps a valid selection: the current unit if it can still act, else the next one that can. */
  private autoSelect(): void {
    const current = this.selectedUnit();
    // Real time: no turns to follow; one stays picked until it falls, and the camera is left alone.
    if (this.state.realtime) {
      if (current?.alive && this.commands(current)) return;
      this.selected = this.myUnits()[0]?.id ?? null;
      this.units.setSelected(this.selected);
      return;
    }
    // Keep the acting soldier selected until its action has finished playing.
    if (current && this.myPending > 0) return;
    // The one in play, if it is mine (playtest 15/10 #9): picked once, as it comes into play or the
    // command comes back to it; one picked afterwards to look at stays picked.
    const playing = this.state.activeUnit ? getUnit(this.state, this.state.activeUnit) : undefined;
    const mineInPlay = playing?.alive && playing.ap > 0 && this.commands(playing) ? playing : undefined;
    const turned = this.state.command !== this.lastCommand || this.state.activeUnit !== this.lastInPlay;
    this.lastCommand = this.state.command;
    this.lastInPlay = this.state.activeUnit;
    if (mineInPlay && turned && this.canAct() && this.hasCommand()) {
      this.looking = null;
      if (current?.id === mineInPlay.id) return;
      this.selected = mineInPlay.id;
      this.units.setSelected(this.selected);
      this.turnCamera(mineInPlay);
      return;
    }
    // The partner's soldier comes into play: the camera to it too, if wanted (#47).
    if (turned && playing?.alive && !this.commands(playing) && playing.team === 'xcom') this.turnCamera(playing);
    // One picked with no actions left, to look at its sheet and backpack (playtest 18/10 #2): it stays until another is picked.
    if (current && current.id === this.looking) {
      if (current.alive && current.ap <= 0) return;
      this.looking = null;
    }
    if (current && this.commands(current) && (current.ap > 0 || !this.canAct())) return;
    const next = mineInPlay ?? this.myUnits().find((u) => u.ap > 0) ?? (current && this.commands(current) ? current : this.myUnits()[0]);
    this.selected = next?.id ?? null;
    this.units.setSelected(this.selected);
    if (next && current && next.id !== current.id && this.canAct()) this.turnCamera(next);
  }

  /** Who plays first for `slot`: its soldier in play, else the one picked (one's own), else its first with actions. */
  private turnSoldier(v: GameState, slot: Slot): Unit | undefined {
    const inPlay = v.activeUnit ? getUnit(v, v.activeUnit) : undefined;
    if (inPlay?.alive && inPlay.owner === slot) return inPlay;
    const picked = slot === this.mySlot ? this.selectedUnit() : undefined;
    if (picked?.alive && picked.ap > 0) return picked;
    return v.units.find((u) => u.alive && u.team === 'xcom' && u.owner === slot && u.ap > 0);
  }

  /**
   * The camera to the soldier whose turn it is (playtest 17/10 #47): as the
   * turn begins, the command passes or a soldier comes into play, if the HUD's
   * switch says so; never fighting the player (not if they moved the camera
   * in the last few seconds).
   */
  private turnCamera(u: Unit | undefined): void {
    if (!u?.alive || !this.hud.followsTurn || performance.now() - this.rig.takenAt < TURN_CAMERA_QUIET_MS) return;
    this.setLevel(u.pos.z ?? 0);
    this.rig.focus(this.units.worldPos(u.id));
  }

  /** The camera to the soldier whose turn it is, and it picked (F, «Ir a…»: playtest 19/10 #15); asked, so at once. */
  private goToSoldier(): void {
    const u = this.turnSoldier(this.view, this.mySlot) ?? this.selectedUnit();
    if (!u?.alive) return;
    if (u.id !== this.selected && this.commands(u)) this.select(u.id);
    this.setLevel(u.pos.z ?? 0);
    this.rig.focus(this.units.worldPos(u.id));
  }

  private myUnits(): Unit[] {
    return this.state.units.filter((u) => u.alive && this.commands(u));
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
    if (!u || !this.canAct() || !this.commands(u) || u.ap <= 0 || this.waitingFor(u.id)) return null;
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
      detected: watchers.some((a) => inViewCone(a, d.pos) && sightOrigin(this.state, a.pos, d.pos, detectionTo(this.state, a, d.pos)) !== null),
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
        .map((o) => ({ unit: o.target, tile: o.tile, hit: previewShot(s, u, o.target, { ability, from: o.tile })?.hit ?? 0 }))
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
    if (this.handArmed()) {
      this.cycleHand(dir);
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
    // The armed panel's button for Registrar or a door (playtest 2026-10-13 #11: it did nothing, only Space searched).
    if (this.handArmed()) {
      this.confirmHand();
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

  private useAbility(id: BarActionId): void {
    if (id === 'evac' || id === 'evacAccept' || id === 'evacRefuse') return this.useEvac(id);
    if (id === 'abandon' || id === 'abandonAccept' || id === 'abandonRefuse') {
      const call = this.state.abandonCall;
      if (id !== 'abandon' && call && call.by !== this.mySlot && !this.state.outcome) this.net.send({ t: 'abandonAnswer', accept: id === 'abandonAccept' });
      return;
    }
    const u = this.selectedUnit();
    if (!u || !this.canAct()) return;
    if (isMenu(id)) return;
    const view = this.barView(id);
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
        // By hand (Registrar, a door): armed like a self ability, on the tiles beside the soldier.
        // A single door is opened or closed at once: free, and it can be undone.
        if (HAND.has(id)) {
          const hand = id as HandAbility;
          const tiles = this.handTiles(hand);
          if (this.handArmed() === hand) this.confirmHand();
          else if (tiles.length === 1) this.useDoor(hand, tiles[0]!);
          // Several within reach: armed, to pick one; a click on it searches.
          else this.armHand(hand);
          return;
        }
        this.mode = this.mode.kind === 'tile' && this.mode.ability === id ? { kind: 'move' } : { kind: 'tile', ability: id };
        this.refreshInteractive();
        return;
      case 'self':
        // The first press does it (playtest 19/10 #17: «hoy, ponerse en vigilancia son tres clics»): what it
        // does is on the card's tooltip, and there is nothing to aim.
        this.mode = { kind: 'self', ability: id };
        this.targetId = null;
        this.concealWarn = null;
        this.confirmSelf();
    }
  }

  // ============================================================ by hand

  /** Registrar, or a door opened or closed (playtest 13/10 #2), armed on the tiles beside the soldier. */
  private handArmed(): HandAbility | null {
    return this.mode.kind === 'tile' && HAND.has(this.mode.ability) ? (this.mode.ability as HandAbility) : null;
  }

  /** The tiles beside the selected soldier that `id` works on: furniture to search, closed or open doors. */
  private handTiles(id: HandAbility | null = this.handArmed()): Vec2[] {
    const u = this.selectedUnit();
    if (!u || !id) return [];
    return id === 'search' ? searchableTiles(this.state, u) : doorTiles(this.state, u, id === 'openDoor' ? 'door' : 'doorOpen');
  }

  /** The tile the armed ability would act on: the one picked (pointer, Tab or click), else the first within reach. */
  private handTarget(): Vec2 | null {
    const tiles = this.handTiles();
    const pick = this.handPick;
    return (pick && tiles.find((t) => samePlace(t, pick))) ?? tiles[0] ?? null;
  }

  /** Arms `id` (on `tile`, if given): the tiles get marked and the panel says which one it acts on. */
  private armHand(id: HandAbility, tile?: Vec2): void {
    this.mode = { kind: 'tile', ability: id };
    this.targetId = null;
    this.concealWarn = null;
    this.handPick = tile ?? this.handTarget();
    this.refreshInteractive();
  }

  /** Tab with it armed: the next tile within reach. */
  private cycleHand(dir: 1 | -1): void {
    const tiles = this.handTiles();
    if (tiles.length < 2) return;
    const current = this.handTarget();
    const i = tiles.findIndex((t) => current && samePlace(t, current));
    this.handPick = tiles[(i + dir + tiles.length) % tiles.length]!;
    this.refreshInteractive();
  }

  private confirmHand(tile = this.handTarget()): void {
    const u = this.selectedUnit();
    const id = this.handArmed();
    if (!u || !id || !this.canAct()) return;
    if (!this.hasCommand()) {
      this.hud.toast(this.commandLock());
      return;
    }
    if (!tile) {
      this.hud.toast(id === 'search' ? 'No hay nada que registrar al lado.' : 'No hay ninguna puerta a tu lado.');
      return;
    }
    this.send({ type: 'ability', unit: u.id, ability: id, tile });
    this.cancelMode();
  }

  /**
   * By hand on `tile`, at once: a door opened or closed (free, and it can be undone), or furniture searched
   * (playtest 18/10 #29: pressing Registrar, or clicking the box, is enough; no second «Abrir»).
   */
  private useDoor(id: HandAbility, tile: Vec2): void {
    const u = this.selectedUnit();
    if (!u || !this.canAct()) return;
    if (!this.hasCommand()) {
      this.hud.toast(this.commandLock());
      return;
    }
    this.send({ type: 'ability', unit: u.id, ability: id, tile });
    this.cancelMode();
  }

  /** The armed panel: «Registrar el maletero» (with the room left in the backpack), «Abrir la puerta». */
  private handPanel(u: Unit): TargetView | null {
    const id = this.handArmed();
    const tile = this.handTarget();
    if (!id || !tile) return null;
    const def = ABILITIES[id];
    const others = this.handTiles().length - 1;
    const base = { kind: 'self' as const, action: def.name, locked: this.lockLabel(), cost: costText(u, id), noise: noiseText(this.state, u, id), effect: def.description };
    if (id !== 'search') {
      const what = id === 'openDoor' ? 'abrir' : 'cerrar';
      return { ...base, title: def.name, hint: `Otra vez, Espacio o clic en la puerta: ${what}${others ? ` · Tab: ${others === 1 ? 'la otra' : 'otra'} puerta` : ''} · Esc: cancelar` };
    }
    const decor = searchSpot(this.state, tile);
    if (!decor) return null;
    const slots = u.packSlots ?? BACKPACK_SLOTS;
    const room = slots - (u.backpack?.length ?? 0);
    return {
      ...base,
      title: boxTitle(decor),
      detail: `Mochila: ${room === 1 ? 'queda 1 hueco' : `quedan ${room} huecos`} de ${slots}`,
      hint: `Otra vez, Espacio o clic en la caja: abrir${others ? ` · Tab: ${others === 1 ? 'la otra' : 'otra'} caja` : ''} · Esc: cancelar`,
    };
  }

  /** Uses the armed self ability. */
  private confirmSelf(): void {
    const u = this.selectedUnit();
    if (!u || this.mode.kind !== 'self') return;
    if (!this.hasCommand()) {
      this.hud.toast(this.commandLock());
      this.cancelMode();
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
        if (this.handArmed()) this.confirmHand();
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
    // A door's button only while there is one to open or close beside the soldier (they come last, so no hotkey moves).
    const door = (id: AbilityId) => (id !== 'openDoor' && id !== 'closeDoor') || doorTiles(this.state, u, id === 'openDoor' ? 'door' : 'doorOpen').length > 0;
    // Leaving is the bar's own action (evacActions), for any of ours in La Lata: never the soldier's.
    return abilitiesOf(u).filter((id) => id !== 'evac' && (id !== 'hack' || (!!m.terminal && !m.objectiveDone)) && (id !== 'search' || this.hasSpots) && door(id));
  }

  private abilities(): AbilityView[] {
    const u = this.selectedUnit();
    const evac = [...this.abandonActions(), ...this.evacActions(u)];
    if (!u || !this.commands(u)) return evac;
    return [...this.withMenus(this.soldierAbilities(u), u), ...evac];
  }

  /**
   * The actions with a menu (docs/FASE10.md §4) folded into one button each,
   * where the first of them was: Lanzar (the stone, and the grenades, smoke or
   * flashbangs it carries), Vigilancia (from afar or with the blade) and
   * Investigar (the room; the box too when there is one beside). The keys are
   * numbered again along the folded bar.
   */
  private withMenus(views: AbilityView[], u: Unit): AbilityView[] {
    const out: AbilityView[] = [];
    const done = new Set<MenuId>();
    const crate = this.crateBeside(u);
    for (const v of views) {
      const menu = MENUS.find((m) => m.members.includes(v.id as AbilityId));
      if (!menu) {
        out.push(v);
        continue;
      }
      if (done.has(menu.id)) continue;
      done.add(menu.id);
      const options = menu.members
        .map((id) => views.find((x) => x.id === id))
        .filter((x): x is AbilityView => !!x && menu.shows(x, crate))
        .map((o) => ({ ...o, label: MENU_LABELS[o.id as AbilityId] ?? o.label }));
      if (!options.length) continue;
      const usable = options.filter((o) => o.enabled);
      out.push({
        ...options[0]!,
        id: menu.id,
        label: menu.name,
        name: menu.name,
        icon: menu.icon,
        enabled: usable.length > 0,
        reason: usable.length ? undefined : options[0]!.reason,
        description: options.length > 1 ? `${menu.name}: ${options.map((o) => o.name.toLowerCase()).join(', ')}. Pulsa para elegir.` : options[0]!.description,
        // With a choice to make, each choice's card has its own figures (#18): none on the menu's.
        ...(options.length > 1 ? { stats: [], cost: '', limits: undefined, damage: undefined, noise: undefined } : {}),
        active: options.some((o) => o.active),
        count: undefined,
        cooldown: undefined,
        depleted: options.every((o) => o.depleted),
        options,
      });
    }
    // Lanzar always right after Vigilancia, whatever the soldier carries: the same key for everyone.
    const throwAt = out.findIndex((v) => v.id === 'menu:throw');
    const watchAt = out.findIndex((v) => v.id === 'menu:watch');
    if (throwAt >= 0 && watchAt >= 0 && throwAt !== watchAt + 1) out.splice(watchAt + 1, 0, ...out.splice(throwAt, 1));
    return out.map((v, i) => ({ ...v, hotkey: i < 9 ? (v.id === 'shoot' ? `${i + 1} · Esp` : String(i + 1)) : i === 9 ? '0' : '' }));
  }

  /** A box beside the soldier, opened or not, that its hands reach (loot.ts searchSpot, reachesToSearch). */
  private crateBeside(u: Unit): boolean {
    const s = this.state;
    for (let dx = -1; dx <= 1; dx++) {
      for (let dy = -1; dy <= 1; dy++) {
        if (!dx && !dy) continue;
        const p = { x: u.pos.x + dx, y: u.pos.y + dy, z: u.pos.z };
        if (searchSpot(s, p) && reachesToSearch(s, u.pos, p)) return true;
      }
    }
    return false;
  }

  /** A view of the bar by its id, inside a menu too. */
  private barView(id: BarActionId): AbilityView | undefined {
    for (const v of this.abilities()) {
      if (v.id === id) return v;
      const o = v.options?.find((x) => x.id === id);
      if (o) return o;
    }
    return undefined;
  }

  private soldierAbilities(u: Unit): AbilityView[] {
    return this.abilityList(u).map((id, i) => {
      const def = ABILITIES[id];
      const charge = def.charge ? u.charges[def.charge] ?? 0 : undefined;
      let reason: string | null = !this.canAct()
        ? 'No es vuestro turno.'
        : (this.waitingFor(u.id) ?? abilityBlocker(this.state, u, id));
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
        // An attack's card names the weapon it fires: its cost and its noise are that weapon's.
        name: isAttack(id) ? `${def.name} · ${weaponLabel(u, id as AttackAbility)}` : def.name,
        cost: costText(u, id),
        limits: limitsText(u, id),
        damage: damageText(this.state, u, id),
        noise: noiseText(this.state, u, id),
        stats: abilityStats(this.state, u, id),
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
    this.followLevel();
    // Without the command the range is still shown, fainter: it is for planning (#35). Only the active floor's.
    // Real time: no range (nothing counts actions); the pointer shows the way (rtHover).
    const r = this.mode.kind === 'move' && !this.isBusy(this.selected) && !this.view.realtime ? this.reach() : null;
    this.overlays.showMoveRange((r?.tiles ?? []).filter((t) => ((t.pos as Pos).z ?? 0) === this.level), !this.hasCommand());
    this.updateHover();
    this.refreshHud();
  }

  /** The floor follows the selected soldier: when it is picked, and when it climbs or comes down. */
  private followLevel(): void {
    const u = this.selectedUnit();
    const key = u ? `${u.id}@${u.pos.z}` : null;
    if (key === this.levelFor) return;
    this.levelFor = key;
    if (u) this.setLevel(u.pos.z, false);
  }

  /** One floor up (1) or down (-1), by key, wheel or the HUD's arrows: by hand. */
  private stepLevel(dir: 1 | -1): void {
    if (!this.maxLevel) return;
    this.levelByHand = true;
    this.roofs = false;
    this.setLevel(this.level + dir);
  }

  /**
   * The floor follows the wheel (Arturo, 18/10 #1): drawn back, up to the top
   * storey with the roofs on; close in, down to the floor of the soldier
   * looked at (the one picked, if near the middle of the view, else the
   * nearest there). Only on maps with storeys, and not over a floor picked by
   * hand until the wheel turns again (the camera never fights the player).
   */
  private wheelLevel(): void {
    if (!this.maxLevel || this.levelByHand) return;
    const k = this.rig.zoomFraction;
    if (k >= ROOFS_FAR) {
      this.roofs = true;
      this.setLevel(this.maxLevel);
      return;
    }
    if (k < ROOFS_FAR - 0.1) this.roofs = false;
    if (k > FLOOR_NEAR) return;
    const at = this.rig.focusPoint;
    const off = (u: Unit) => Math.hypot(u.pos.x + 0.5 - at.x, u.pos.y + 0.5 - at.z);
    const picked = this.selectedUnit();
    const around = this.view.units.filter((u) => u.alive && (u.team === 'xcom' || this.visibleAliens.has(u.id)) && off(u) < 6).sort((a, b) => off(a) - off(b));
    const looked = picked && off(picked) < 8 ? picked : around[0];
    if (looked) this.setLevel(looked.pos.z ?? 0);
  }

  /**
   * Makes `k` the active floor: the camera rises or drops to it, and the
   * cursor, the range and the previews move to it. `refresh` false: called
   * from inside a refresh, which goes on to draw them.
   */
  private setLevel(k: number, refresh = true): void {
    const level = Math.max(0, Math.min(this.maxLevel, k));
    this.hud.setFloor(this.maxLevel ? { level, max: this.maxLevel } : null);
    if (level === this.level) return;
    this.level = level;
    const at = this.rig.focusPoint;
    this.rig.focus(new THREE.Vector3(at.x, level * STOREY, at.z));
    // The tile under the pointer is now looked for on the new floor.
    this.hoverTile = null;
    if (refresh) this.refreshInteractive();
  }

  private cancelModeSilently(): void {
    this.mode = { kind: 'move' };
    this.targetId = null;
  }

  // ============================================================ evacuation

  /** The evacuation zone's middle tile (where La Mula waits), or null on a map without one. */
  private evacCentre(s: GameState): Vec2 | null {
    const zone = s.mission.evacZone;
    if (!zone.length) return null;
    const mid = { x: zone.reduce((sum, t) => sum + t.x, 0) / zone.length, y: zone.reduce((sum, t) => sum + t.y, 0) / zone.length };
    return zone.reduce((best, t) => (distance(t, mid) < distance(best, mid) ? t : best));
  }

  /** Where the bus stands: the middle of its bay (mission.evacBus), else the evacuation zone's (it waits just off the edge by it). */
  private busTile(s: GameState): Vec2 | null {
    const bay = s.mission.evacBus?.tiles;
    if (!bay?.length) return this.evacCentre(s);
    const mid = { x: bay.reduce((sum, t) => sum + t.x, 0) / bay.length, y: bay.reduce((sum, t) => sum + t.y, 0) / bay.length };
    return bay.reduce((best, t) => (distance(t, mid) < distance(best, mid) ? t : best));
  }

  /** The bus pulls away with its engine (doors, lights, off the map: mapView.busDeparts), seen whole before the report. */
  private async busLeaves(at: Vec2): Promise<void> {
    this.sfx.vehicle(tileToWorld(at));
    await this.mapView.busDeparts();
  }

  /** What leaving now costs, for Lore's words: a Limpieza, the objective, and who would stay outside. */
  private evacSituation(s: GameState): EvacSituation {
    return {
      clearing: s.mission.kind === 'elimination',
      ready: evacWins(s),
      // Outside, and the bitten with no Cuarentena to go to (playtest 15/10 #13): they stay too.
      left: s.units.filter((u) => u.alive && u.team === 'xcom' && (!inEvacZone(s, u.pos) || (u.infected && s.mission.quarantine === false))).map((u) => u.name),
    };
  }

  /** Leaving loses the mission or somebody: the button to leave is red. */
  private evacCostly(o: EvacSituation): boolean {
    return o.clearing || !o.ready || o.left.length > 0;
  }

  /**
   * The partner whose yes leaving needs (Sim.callEvac): still on the field,
   * and here or within the server's grace (activeSlots). Past it, the bus
   * leaves alone.
   */
  private evacPartner(): PlayerInfo | undefined {
    const active = this.activeSlots();
    const p = this.players.find((q) => q.slot !== this.mySlot && active.includes(q.slot));
    return p && this.state.units.some((u) => u.alive && u.team === 'xcom' && u.owner === p.slot) ? p : undefined;
  }

  /** The partner connected now: the one who has to say yes to abandoning (rules.ts callAbandon). */
  private onlinePartner(): PlayerInfo | undefined {
    return this.players.find((p) => p.slot !== this.mySlot && p.connected);
  }

  /**
   * A call to abandon on the bar (playtest 16/10), like the bus's: the
   * partner's «Abandonar» and «Seguir», on whatever turn it is; this player's
   * own call, off and waiting, with the time left before it goes through.
   */
  private abandonActions(): AbilityView[] {
    const call = this.state.abandonCall;
    if (!call || this.state.outcome) return [];
    const left = deadlineLeft('abandon');
    const view = (id: BarActionId, name: string, description: string, extra: Partial<AbilityView> = {}): AbilityView => ({
      id,
      label: name,
      hotkey: '',
      enabled: true,
      name,
      cost: 'Sin acción',
      description,
      free: true,
      ...extra,
    });
    if (call.by !== this.mySlot) {
      const ask = abandonAsk(this.playerName(call.by));
      const limits = left ? proposalClock(left, false) : undefined;
      return [view('abandonAccept', ABANDON_TEXT.accept, `${ask}. ${ABANDON_TEXT.stakes}`, { limits }), view('abandonRefuse', ABANDON_TEXT.refuse, `${ask}. Seguís en la misión.`, { limits })];
    }
    const waiting = evacWaiting(this.onlinePartner()?.name ?? 'el otro jugador');
    return [view('abandon', ABANDON_TEXT.accept, waiting, { enabled: false, reason: left ? proposalClock(left, true) : waiting })];
  }

  /** Our soldiers in La Mula, on the state orders go by. */
  private mineAboard(): Unit[] {
    const s = this.state;
    return s.units.filter((u) => u.alive && this.commands(u) && inEvacZone(s, u.pos));
  }

  /**
   * The evacuation on the action bar (playtest 15/10 #10), with no window over the field: «Evacuar» for a
   * soldier of ours in La Lata (pressed twice: the first says what leaving costs), the wait while ours
   * waits, and the partner's «Aceptar evacuación» and «Esperar» while theirs does.
   */
  private evacActions(u: Unit | undefined): AbilityView[] {
    const s = this.state;
    if (!this.canAct() || !s.mission.evacZone.length) return [];
    const call = s.evacCall;
    const o = this.evacSituation(s);
    const left = deadlineLeft('evac');
    const view = (id: BarActionId, name: string, description: string, extra: Partial<AbilityView> = {}): AbilityView => ({
      id,
      label: name,
      hotkey: '',
      enabled: true,
      name,
      cost: 'Sin acción',
      description,
      free: true,
      ...extra,
    });
    if (call && call.by !== this.mySlot) {
      const ask = evacAsk(this.playerName(call.by), o);
      const limits = left ? evacClock(left, false) : undefined;
      return [view('evacAccept', EVAC_BAR.accept, ask.text, { limits }), view('evacRefuse', EVAC_BAR.wait, EVAC_BAR.stay, { limits })];
    }
    // Asking costs no action: whichever of ours is in La Lata asks, the one picked if it is, any other if not
    // (a soldier with no actions left is not kept picked); one who can board before a bitten one who can't.
    const inside = this.mineAboard().sort((a, b) => Number(a.id !== u?.id) - Number(b.id !== u?.id));
    const boards = (o: Unit) => !(o.infected && s.mission.quarantine === false);
    const aboard = inside.find(boards) ?? inside[0];
    if (call?.by === this.mySlot) {
      const partner = this.evacPartner();
      const name = s.mission.kind === 'elimination' ? 'Retirarse' : 'Evacuar';
      return [view('evac', name, evacWaiting(partner?.name ?? 'el otro jugador'), { enabled: false, reason: left ? evacClock(left, true) : evacWaiting(partner?.name ?? 'el otro jugador') })];
    }
    if (!aboard) return [];
    if (aboard.infected && s.mission.quarantine === false) return [view('evac', s.mission.kind === 'elimination' ? 'Retirarse' : 'Evacuar', BITE_NO_BOARD, { enabled: false, reason: BITE_NO_BOARD })];
    const p = evacPrompt(o, this.evacPartner()?.name ?? null);
    const armed = this.evacArmed === aboard.id;
    return [view('evac', armed ? `Confirmar: ${p.go}` : p.go, `${p.title}. ${p.text}`, { active: armed, cost: this.evacPartner() ? `Lo acepta ${this.onlinePartner()?.name ?? 'el otro jugador'}` : 'Termina la misión' })];
  }

  /** The bar's evacuation, pressed: arm then ask (ours), or answer (the partner's). */
  private useEvac(id: 'evac' | 'evacAccept' | 'evacRefuse'): void {
    if (id !== 'evac') {
      if (this.state.evacCall && this.state.evacCall.by !== this.mySlot) this.send({ type: 'evacAnswer', accept: id === 'evacAccept' });
      return;
    }
    // The same soldier the bar offered (evacActions).
    const s = this.state;
    const sel = this.selectedUnit();
    const inside = this.mineAboard().sort((a, b) => Number(a.id !== sel?.id) - Number(b.id !== sel?.id));
    const u = inside.find((o) => !(o.infected && s.mission.quarantine === false));
    if (!u || s.evacCall) return;
    if (this.evacArmed !== u.id) {
      // The first press only says what leaving costs: it can't be undone.
      this.evacArmed = u.id;
      window.clearTimeout(this.evacArmTimer);
      this.evacArmTimer = window.setTimeout(() => {
        this.evacArmed = null;
        this.refreshHud();
      }, 5000);
      const p = evacPrompt(this.evacSituation(this.state), this.evacPartner()?.name ?? null);
      this.hud.toast(`${p.text} Pulsa otra vez «${p.go}».`);
      this.refreshHud();
      return;
    }
    this.evacArmed = null;
    window.clearTimeout(this.evacArmTimer);
    this.send({ type: 'evac', unit: u.id });
  }

  /** What a recovery carries out: story chapters and side quests name it ("las cintas"). */
  private recoveryItem(): string {
    const cm = this.state.campaignMission;
    return cm?.item || (cm?.story && STORY[cm.story - 1]?.item) || DEFAULT_ITEM;
  }

  private missionView(): MissionView {
    const s = this.view;
    const m = s.mission;
    if (m.siege) return this.siegeView(m.siege.hold);
    let objective = 'Eliminad a todos los hostiles';
    let objectiveTip: string | undefined;
    switch (m.kind) {
      case 'recovery': {
        const carrier = m.item?.carrier ? getUnit(s, m.item.carrier) : undefined;
        const item = this.recoveryItem();
        // Lore's words for each moment of the item (playtest 14/10 #23).
        objective = m.item?.evacuated
          ? itemObjective(item, 'out')
          : carrier?.alive && inEvacZone(s, carrier.pos)
            ? itemObjective(item, 'aboard')
            : carrier
              ? itemObjective(item, 'carried', carrier.name)
              : itemObjective(item, 'loose');
        break;
      }
      case 'sabotage':
        objective = m.objectiveDone ? OBJECTIVE_DONE.sabotage! : alarmObjective(m.alarm);
        break;
      case 'hack':
        objective = m.objectiveDone ? OBJECTIVE_DONE.hack! : 'Arreglad el equipo (junto a él)';
        break;
      case 'rescue': {
        const vip = m.objectiveUnit ? getUnit(s, m.objectiveUnit) : undefined;
        const who = vip?.name ?? 'el VIP';
        objective = m.objectiveDone
          ? vipObjective(who, 'out')
          : vip?.alive && !vip.captive && inEvacZone(s, vip.pos)
            ? vipObjective(who, 'aboard')
            : vip?.captive
              ? vipObjective(who, 'captive')
              : vipObjective(who, 'loose');
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
    // The hordes on the way are not said on the objective's card (playtest 19/10 #20: «fuera "Zombis: llegan este
    // turno"»): their banner said it, and the Saqueo's gauge counts them down. A flare drop (older saves), still.
    const alert = m.reinforcements.some((r) => !r.landed && !r.horde && r.pos) ? 'VIENEN MÁS' : null;
    return { kind: m.kind, objective, objectiveTip, turnsLeft: m.turnsLeft, alert, area: s.mapName, horde: m.horde?.length ? this.hordeView() : null };
  }

  /**
   * A siege's strip (docs/FASE12.md §10, Lore's words): «Asedio», hold to the
   * last turn, the turns left (a count towards the win, not a threat) and
   * when the next wave comes, not where (18/10 #20); or that none are coming
   * any more.
   */
  private siegeView(hold: number): MissionView {
    const s = this.view;
    const next = siegeWaves(s).find((w) => !w.landed);
    const wave = next
      ? { text: `OLEADA · ${waveDueShort(next.turn, s.turn).toUpperCase()}`, tip: waveDueLine(next.turn, s.turn), urgent: next.turn <= s.turn }
      : { text: SIEGE_TEXT.noMoreWaves.toUpperCase(), tip: SIEGE_TEXT.noMoreWaves, urgent: false };
    return {
      kind: s.mission.kind,
      label: SIEGE_TEXT.name,
      objective: siegeObjective(hold),
      objectiveTip: siegeObjectiveTip(hold),
      turnsLeft: Math.max(0, hold - s.turn + 1),
      hold: true,
      alert: null,
      area: s.mapName,
      horde: null,
      wave,
    };
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
    // At night the dead are already on their way: the clock runs a turn ahead (Sim.callHorde).
    const points = (s.turn + (m.night ? NIGHT.hordeHead : 0)) * HORDE.perTurn + (m.noise ?? 0);
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

    const ready = s.ready[this.mySlot];
    const waiting = partner && busy.has(partner.slot) && !s.ready[partner.slot] ? partner.name : null;
    const idle = this.idleSoldiers();
    const mode = this.enemyTurnPlaying() ? 'enemy' : !this.canAct() ? 'disabled' : ready ? 'ready' : !mine ? 'waiting' : this.endArmed && idle ? 'confirm' : 'act';
    const detail = mode === 'confirm' ? `${idle} ${idle === 1 ? 'soldado' : 'soldados'} con acciones` : !mine && !ready ? this.playerName(s.command) : waiting;
    this.hud.setEndTurn(mode, detail);
    this.hud.setCommand(sharing && this.canAct() ? { mine, holder: this.playerName(s.command), color: PLAYER_COLORS[s.command ?? this.mySlot], canPass: mine && !s.ready[partner!.slot] && busySlots(s).has(partner!.slot) } : null);
    // «Ir a …» (#15): while this player gives the orders and someone still has actions.
    const next = this.canAct() && mine && !ready && s.activeTeam === 'xcom' ? this.turnSoldier(s, this.mySlot) : undefined;
    this.hud.setGoTo(next?.name ?? null);
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
    // One soldier at a time with a partner (playtest 15/10 #9): the one in play, by the label and on its card.
    const playing = sharing && v.activeUnit ? getUnit(v, v.activeUnit) : undefined;
    const inPlay = playing?.alive && playing.ap > 0 ? playing : undefined;
    // Real time: the mission's clock in place of the turn (docs/TIEMPO-REAL.md).
    if (v.realtime) this.hud.setRealtime({ paused: v.realtime.paused, concealed: v.concealed, night: v.mission.night ? { label: NIGHT_LABEL, rules: NIGHT_RULES } : null });
    else {
      this.hud.setRealtime(null);
      this.hud.setTurn(v.turn, owner, v.concealed, v.mission.night ? { label: NIGHT_LABEL, rules: NIGHT_RULES } : null, inPlay?.name ?? null);
    }
    this.hud.setMission(this.missionView());
    this.hud.setPlayers(this.players, v.ready, this.mySlot, busySlots(v), sharing ? holder : null);
    this.hud.setSquad(
      // A siege's turrets too, for their health and their rounds (Gameplay, 18/10); not to be picked (commands).
      v.units.filter((u) => u.team === 'xcom' || u.controlledBy),
      this.mySlot,
      this.selected,
      { 0: PLAYER_COLORS[0], 1: PLAYER_COLORS[1] },
      v.mission.item?.carrier ?? null,
      inPlay?.id ?? null,
      (u) => this.commands(u),
      v.realtime ? (u) => this.rtCard(u) : undefined,
    );
    const shown = this.selected ? getUnit(v, this.selected) : undefined;
    // A partner's soldier on the sheet says whose it is, in their colour (picked to look, or to command while they're gone).
    const whose = shown?.team === 'xcom' && shown.owner !== null && shown.owner !== this.mySlot ? { name: this.playerName(shown.owner), color: PLAYER_COLORS[shown.owner] } : null;
    this.hud.setSelected(shown?.alive ? shown : null, shown?.alive ? this.packView(shown) : null, whose);
  }

  /** The soldier's backpack and the squad's haul for the sheet: where there is anything to search, or it carries something. */
  private packView(u: Unit): PackView | null {
    const v = this.view;
    // Also with nothing carried on a map with nothing to search, if something lies on its tile (a fallen soldier's rucksack: #49).
    const underfoot = (v.dropped ?? []).some((d) => samePos(d.pos, u.pos));
    if (TEMPLATES[u.template].team !== 'xcom' || u.template === 'vip' || (!this.hasSpots && !u.backpack?.length && !underfoot)) return null;
    const squad = lootTotals(v.units.filter((o) => o.team === 'xcom' && (o.alive || o.evacuated)).flatMap((o) => o.backpack ?? []));
    const mine = this.commands(u) && u.alive && this.canAct();
    return {
      slots: packSlots(u.backpack ?? []),
      size: u.packSlots ?? BACKPACK_SLOTS,
      live: mine,
      labels: (u.backpack ?? []).map((l) => findLabel(l)),
      names: (u.backpack ?? []).map((l) => lootName(l)),
      near: v.units.filter((o) => o.id !== u.id && o.alive && o.team === 'xcom' && !o.captive && o.template !== 'vip' && inMeleeReach(v, u.pos, o.pos)).map((o) => ({ id: o.id, name: o.name })),
      floor: (v.dropped ?? [])
        .filter((d) => samePos(d.pos, u.pos))
        .map((d) => ({ id: d.id, label: findLabel(d.loot), name: lootName(d.loot), kind: packSlots([d.loot])[0]!.kind, from: d.from ? getUnit(v, d.from)?.name : undefined })),
      worn: u.worn ?? [],
      gear: u.gear,
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

  // ============================================================ backpack

  /** An order on the selected soldier's backpack or tile (docs/FASE8.md §C); the engine says why not, if not. */
  private itemOrder(o: ItemOrder): void {
    const u = this.selectedUnit();
    if (!u || !this.canAct()) return;
    this.send({ type: 'item', unit: u.id, ...o });
    this.refreshStatus();
  }

  /**
   * A fallen soldier's rucksack taken (#49): one thing after another, each once the last is in (settle),
   * while the backpack has room.
   */
  private takeAll(ids: string[]): void {
    const u = this.selectedUnit();
    if (!u || !this.canAct() || !ids.length) return;
    this.packQueue = { unit: u.id, ids: ids.slice(1) };
    this.send({ type: 'item', unit: u.id, action: 'take', id: ids[0]! });
    this.refreshStatus();
  }

  /** The backpack full: leave slot `drop` on the floor, then pick up `take` (settle sends the second half). */
  private swapItem(take: string, drop: number): void {
    const u = this.selectedUnit();
    if (!u || !this.canAct()) return;
    this.swapTake = { unit: u.id, id: take };
    this.send({ type: 'item', unit: u.id, action: 'drop', index: drop });
  }

  /** The notice's «Cambiar»: the soldier on the sheet, its backpack asking what to leave for the find. */
  private openSwap(unit: string, id: string): void {
    if (this.selected !== unit) this.select(unit, true);
    this.hud.openSwap(unit, id);
    this.refreshStatus();
  }

  /** Builds the target panel and draws the aim line for the current target. */
  private targetPanel(): TargetView | null {
    const u = this.selectedUnit();
    if (u && this.handArmed()) {
      this.overlays.showAim(null, null);
      return this.handPanel(u);
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
      const dmg = friendly ? null : abilityDamage(this.state, u, ability, target);
      const effects: Partial<Record<AbilityId, string>> = {
        revival: 'Quita el aturdimiento, la desorientación y el pánico',
        suppression: '−50 de puntería al objetivo hasta tu próximo turno; si se mueve, le disparas',
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
      case 'hack':
        return `Probabilidad: ${hackChance(s, u)} %`;
      case 'reload':
        // The spares too (docs/FASE7.md): «3 de repuesto», «sin repuesto».
        return `Cargador: ${u.ammo}/${attackWeapon(u, 'shoot').clip}${u.mags === undefined ? '' : u.mags ? ` · ${u.mags} de repuesto` : ' · sin repuesto'}`;
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
    this.overlays.setRangeFocus(null);
    this.overlays.setHover(tile);
    // At night, a lit tile says what the light does there, and how long it lasts (#50; a move or an aim says theirs below);
    // in a siege, a door or a barricade what it still holds (docs/FASE12.md §10).
    if (tile) {
      const notes = [this.view.mission.night && isLit(this.view, tile) ? litNote(this.lightTurns(tile)) : '', this.breakableNote(tile)].filter(Boolean);
      if (notes.length) this.overlays.setNote(tile, notes.join(' · '));
    }
    const hovered = tile ? unitAt(this.view, tile) : undefined;
    this.updateCursor(hovered);
    this.units.setHovered(hovered && (hovered.team === 'xcom' || this.visibleAliens.has(hovered.id)) ? hovered.id : null);
    this.units.setTags(this.enemyTags(u, tile));
    // An enemy under the pointer, or the one aimed at: what it sees (Arturo, 17/10: the dead see in a cone).
    const watched = hovered && hovered.team === 'alien' && this.visibleAliens.has(hovered.id) ? hovered : this.currentTarget()?.unit;
    const shown = watched?.alive && watched.team === 'alien' && !this.view.outcome && !this.mapView.isCutAway(watched.pos) ? watched : null;
    this.watched = shown?.id ?? null;
    this.overlays.setVision(shown ? { ...this.visionOf(shown), facing: this.lookOf(shown) } : null);
    if (this.view.realtime) return this.rtHover(u, tile, hovered);
    if (!u || !this.canAct() || this.isBusy(u.id)) return;

    const option = this.currentTarget();
    // Aiming at night from a lit tile (where it strikes from, or where it stands): seen from far.
    if (option) {
      const from = option.tile ?? u.pos;
      const lit = this.litNote(from);
      if (lit) this.overlays.setNote(from, lit);
    }

    const hand = this.handArmed();
    if (hand) {
      // The furniture or doors within reach are marked; the pointer on one picks it, and the panel follows.
      const pick = this.handPick;
      if (tile && this.handTiles().some((t) => samePlace(t, tile)) && !(pick && samePlace(pick, tile))) {
        this.handPick = tile;
        this.hud.setTarget(this.targetPanel());
      }
      const target = this.handTarget();
      if (hand === 'search') this.overlays.setSpots(this.spotsAround(u, target));
      else this.overlays.setSpots(this.handTiles().map((p) => ({ pos: p, kind: target && samePlace(target, p) ? 'pick' : 'open' })), false);
      return;
    }
    if (this.mode.kind === 'tile') {
      const ability = this.mode.ability;
      this.overlays.showRange(u.pos, abilityRange(u, ability));
      if (!tile) return;
      const center = throwTile(u, ability, tile);
      const area = blastTiles(this.state, ability, center, u);
      const allies = area.filter((t) => unitAt(this.state, t)?.team === 'xcom');
      // docs/FASE7.md §4–5: the walls a frag grenade breaches are framed, and the note says so, and whether the floor goes.
      const walls = breachedWalls(this.state, ability, center, u);
      this.overlays.showBlast(center, area, canTargetTile(this.state, u, ability, center), allies, walls);
      this.overlays.setHover(null);
      const loud = noiseOf(this.state, u, ability);
      const rubble = [walls.length ? (walls.length === 1 ? 'Rompe una pared' : `Rompe ${walls.length} paredes`) : '', breaksFloor(this.state, ability, center) ? 'Hunde el suelo' : ''];
      const note = [loud ? `Ruido ${loud}` : '', ...rubble].filter(Boolean).join(' · ');
      if (note) this.overlays.setNote(center, note);
      return;
    }
    if (this.mode.kind === 'self') {
      // Restoration reaches the allies around: the circle says how far.
      const range = ABILITIES[this.mode.ability].range;
      if (range) this.overlays.showRange(u.pos, range);
      return;
    }
    if (!tile) return;
    if (this.mode.kind !== 'move') return;
    // The tiles' squares only round the pointer (playtest 13/10 #13: no grid over the whole board).
    this.overlays.setRangeFocus(tile);

    const occupant = unitAt(this.state, tile);
    if (occupant?.team === 'alien' && this.visibleAliens.has(occupant.id)) {
      this.overlays.setHover(tile, 0xff5a5a);
      return;
    }
    // A closed door beside the soldier: the click opens it, for free.
    if (this.handTiles('openDoor').some((t) => samePlace(t, tile))) {
      this.overlays.setSpots([{ pos: tile, kind: 'pick' }], false);
      this.overlays.setNote(tile, 'Puerta · clic: abrir (no gasta acciones)');
      return;
    }
    // Only furniture the squad has seen: the fog keeps what is in an unexplored house.
    const decor = this.hasSpots && this.mapView.isExplored(tile.x, tile.y) ? searchSpot(this.state, tile) : null;
    if (decor) {
      this.hoverFurniture(u, tile, decor);
      return;
    }
    const r = this.reach();
    const dest = r?.tiles.find((t) => samePlace(t.pos, tile));
    if (!r || !dest) return;
    const path = buildPath(this.state, u, r.reach, tile);
    this.overlays.showPath(u.pos, path, dest.dash);
    this.overlays.showCover(tile, coverSides(this.state, tile));
    this.overlays.setHover(tile, dest.detected ? 0xff4a4a : this.overlays.ringColor(dest.dash));
    // Running, a door or a window is heard (docs/FASE2.md): said under the destination.
    // And at night, whether it ends in the light.
    const note = [moveNoise(this.state, path ?? [], dest.dash), this.litNote(tile)].filter(Boolean).join(' · ');
    if (note) this.overlays.setNote(tile, note);
  }

  /** At night a tile in the light is seen from as far as by day (los.ts sightTo, detectionTo): the previews say so. */
  /** The turns left of the light on `p`: a Bengala's or a fire's; none for a street lamp, which never goes out. */
  private lightTurns(p: Place): number | undefined {
    const s = this.view;
    const node = nodeOf(s, toPos(p));
    let lasting: number | undefined;
    for (const l of s.lights ?? []) {
      if (!(l.tiles ? l.tiles.includes(node) : distance(l.tile, p) <= l.radius)) continue;
      if (l.turns === undefined) return undefined;
      lasting = Math.max(lasting ?? 0, l.turns);
    }
    return lasting;
  }

  private litNote(tile: Vec2): string {
    return this.state.mission.night && isLit(this.state, tile) ? 'A la luz: os ven de lejos' : '';
  }

  /** The furniture beside `u` that Registrar reaches: to search, the one it would search, or searched already. */
  private spotsAround(u: Unit, pick: Vec2 | null): { pos: Vec2; kind: SpotKind }[] {
    return DIRS8.map((d) => ({ x: u.pos.x + d.x, y: u.pos.y + d.y, z: u.pos.z }))
      .filter((p) => searchSpot(this.state, p) !== null && reachesToSearch(this.state, u.pos, p))
      .map((p) => ({ pos: p, kind: wasSearched(this.state, p) ? 'done' : pick && samePlace(pick, p) ? 'pick' : 'open' }));
  }

  /** The pointer on a special box (docs/FASE8.md §B): marked, named, and whether it can be opened (a click arms Abrir). */
  private hoverFurniture(u: Unit, tile: Vec2, decor: DecorKind): void {
    const name = capitalize(SEARCH_SPOTS[decor]?.name ?? 'la caja');
    if (wasSearched(this.state, tile)) {
      this.overlays.setSpots([{ pos: tile, kind: 'done' }]);
      this.overlays.setNote(tile, `${name} · ya está abierta`);
      return;
    }
    const near = this.handTiles('search').some((t) => samePlace(t, tile));
    const blocked = near ? abilityBlocker(this.state, u, 'search') : null;
    this.overlays.setSpots([{ pos: tile, kind: near && !blocked ? 'pick' : 'open' }]);
    this.overlays.setNote(tile, !near ? `${name} · se puede abrir` : blocked ? `${name} · ${blocked.replace(/\.$/, '').toLowerCase()}` : `${name} · clic: abrir`);
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
    if (u && tile && this.mode.kind === 'tile' && !this.handArmed() && this.canAct() && this.commands(u) && !this.isBusy(u.id)) return this.blastTags(u, this.mode.ability, tile);
    if (!u || !tile || this.mode.kind !== 'move' || !this.canAct() || !this.commands(u) || this.isBusy(u.id)) return tags;
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
    const dest = this.reach()?.tiles.find((t) => samePlace(t.pos, tile));
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
    const key = `${this.selected}|${this.hoverTile?.x},${this.hoverTile?.y},${this.hoverTile?.z}`;
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

    const tileAtPointer = (e: PointerEvent | MouseEvent): Pos | null => {
      const rect = canvas.getBoundingClientRect();
      pointer.set(((e.clientX - rect.left) / rect.width) * 2 - 1, -((e.clientY - rect.top) / rect.height) * 2 + 1);
      this.raycaster.setFromCamera(pointer, this.renderer.camera);
      // A body under the pointer is its tile: the ground alone took a click on a zombie's head for the tile behind it (#51).
      return this.pickUnit(this.raycaster.ray) ?? this.pickTile(this.raycaster.ray);
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
    const onUp = (e: PointerEvent): void => {
      if (e.button === 2 && this.drag) {
        // Real time: the right click stops the picked soldier where it is (docs/TIEMPO-REAL.md §6b).
        if (!this.drag.moved && this.state.realtime) this.rtStand();
        else if (!this.drag.moved) {
          // A right click (not a drag) on a known enemy opens its file; planning, it takes the picked soldier's
          // plan away (playtest 18/10 #25); elsewhere it backs out of aiming.
          const enemy = this.mode.kind === 'move' && this.hoverTile ? unitAt(this.view, this.hoverTile) : undefined;
          const planned = this.selectedUnit();
          if (enemy?.team === 'alien' && this.visibleAliens.has(enemy.id)) this.showEnemyFile(enemy);
          else if (this.mode.kind === 'move' && !this.hasCommand() && planned && this.plans.get(planned.id)?.slot === this.mySlot) this.withdrawPlan(planned);
          else this.cancelMode();
        }
        this.drag = null;
      }
    };
    // On the window, so a drag let go off the canvas ends too; taken off with the game, which it kept alive.
    window.addEventListener('pointerup', onUp);
    this.disposers.push(() => window.removeEventListener('pointerup', onUp));
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
      if (t?.x === this.hoverTile?.x && t?.y === this.hoverTile?.y && t?.z === this.hoverTile?.z) return;
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
      // Mayús+click signals (playtest 19/10 #14: no button), for a trackpad with no middle click too.
      if (e.shiftKey) {
        if (t) this.ping(t);
        return;
      }
      if (t) this.clickTile(t);
    });

    canvas.addEventListener('wheel', (e) => {
      e.preventDefault();
      // Shift or Alt and the wheel: the floor (Shift turns the wheel sideways on a Mac, hence deltaX).
      if (e.shiftKey || e.altKey) {
        const d = e.deltaY || e.deltaX;
        // A trackpad sends a burst of these: one floor per gesture.
        const now = performance.now();
        if (d && now - this.levelWheel > 300) this.stepLevel(d < 0 ? 1 : -1);
        if (d) this.levelWheel = now;
        return;
      }
      this.rig.zoom(e.deltaY);
      // The wheel takes the floor back from a hand-picked one (#1).
      this.levelByHand = false;
      this.wheelLevel();
    }, { passive: false });

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Shift') this.holdSignal(true);
      if (isTyping(e)) return;
      // The mission's briefing stays until it is closed (playtest 14/10 #3): Enter, Space or Esc close it.
      if (this.hud.introOpen && (e.code === 'Enter' || e.code === 'NumpadEnter' || e.code === 'Space' || e.code === 'Escape')) {
        e.preventDefault();
        this.hud.closeIntro();
        return;
      }
      // Real time (docs/TIEMPO-REAL.md §6b): the space bar stops the clock or lets it go on, X stops the picked one,
      // 1–8 pick one's soldiers; what belongs to turns (abilities, ending the turn, the command) waits for a later step.
      if (this.state.realtime && !this.hud.covered) {
        if (e.code === 'Space') {
          e.preventDefault();
          this.rtPause();
          return;
        }
        if (e.code === 'KeyX') return this.rtStand();
        if (/^Digit[1-8]$/.test(e.code)) {
          const u = this.myUnits()[Number(e.code.slice(5)) - 1];
          if (u) this.select(u.id);
          return;
        }
        if (e.code === 'Tab' || e.code === 'Enter' || e.code === 'NumpadEnter' || e.code === 'Backspace' || e.code === 'KeyC' || /^Digit/.test(e.code)) return;
      }
      // In the enemy turn the space bar, held, plays it in «Rápida» (docs/UX-RITMO.md); it gives no orders then.
      if (e.code === 'Space' && this.enemyTurnPlaying() && !this.hud.covered) {
        e.preventDefault();
        setHurry(true);
        return;
      }
      if (/^Digit[0-9]$/.test(e.code)) {
        const n = Number(e.code.slice(5));
        // A menu open on the bar: the numbers pick its choices.
        if (this.hud.abilityMenuOpen) {
          this.hud.pickMenu(n);
          return;
        }
        const v = this.abilities()[n === 0 ? 9 : n - 1];
        if (!v || v.id.startsWith('evac')) return;
        if (v.options && v.options.length > 1) {
          if (v.enabled) this.hud.toggleBarMenu(v.id as MenuId);
          else if (v.reason) this.hud.toast(v.reason);
        } else this.useAbility(v.options?.[0]?.id ?? v.id);
        return;
      }
      switch (e.code) {
        case 'Tab':
          e.preventDefault();
          if (this.mode.kind === 'target' || this.handArmed()) this.cycleTarget(e.shiftKey ? -1 : 1);
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
          // Esc closes a menu of the bar, then backs out of aiming or of a pending confirmation; otherwise it opens the menu.
          if (this.hud.closeAbilityMenu()) break;
          // A soldier's sheet or an enemy's file goes first.
          if (this.hud.closeModal()) break;
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
        case 'PageUp':
          e.preventDefault();
          this.stepLevel(1);
          break;
        case 'PageDown':
          e.preventDefault();
          this.stepLevel(-1);
          break;
        case 'KeyH':
          if (this.selected) this.openSheet(this.selected);
          break;
        case 'KeyG':
          if (this.hoverTile) this.ping(this.hoverTile);
          break;
        case 'KeyM':
          this.hud.toast(this.sfx.toggleMute() ? 'Sonido desactivado (M)' : 'Sonido activado (M)');
          break;
        case 'KeyF': {
          // With a partner's moment on screen, F goes there; with the partner holding the command, to what they do; otherwise to the soldier.
          if (this.moment) {
            this.seeMoment();
            break;
          }
          if (this.partnerHere() && !this.hasCommand() && this.canAct()) {
            this.watchPartner();
            break;
          }
          this.goToSoldier();
          break;
        }
        case 'F8': {
          // The tiles' looks Arturo chooses from (playtest 18/10 #14), one after another; kept in this browser.
          const look = cycleTileLook();
          this.overlays.setLook(look);
          // The pointer's ring takes the look's colours.
          this.updateHover();
          this.hud.toast(`Casillas: ${look.name}. ${look.line}`);
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
    // Let go (or the window loses the keyboard), and the chosen speed is back.
    const onKeyUp = (e: KeyboardEvent) => {
      if (e.code === 'Space') setHurry(false);
      if (e.key === 'Shift') this.holdSignal(false);
    };
    const onBlur = () => {
      setHurry(false);
      this.holdSignal(false);
    };
    window.addEventListener('keyup', onKeyUp);
    window.addEventListener('blur', onBlur);
    this.disposers.push(() => window.removeEventListener('keyup', onKeyUp), () => window.removeEventListener('blur', onBlur));
  }

  /**
   * The unit whose body the pointer's ray goes through, nearest first: a
   * soldier or an enemy in sight, as an upright cylinder where its model
   * stands now (unitViews). Its tile, so a click on its body picks it.
   */
  private pickUnit(ray: THREE.Ray): Pos | null {
    if (ray.direction.y >= 0) return null;
    const RADIUS = 0.42;
    const HEIGHT = 1.8;
    let best: { t: number; pos: Pos } | null = null;
    const p = new THREE.Vector3();
    for (const u of this.view.units) {
      if (!u.alive || u.evacuated || (u.team !== 'xcom' && !this.visibleAliens.has(u.id)) || this.mapView.isCutAway(u.pos)) continue;
      const base = this.units.worldPos(u.id);
      // Where the ray crosses the body's height, sampled from its head down to its feet.
      const t0 = (ray.origin.y - (base.y + HEIGHT)) / -ray.direction.y;
      const t1 = (ray.origin.y - base.y) / -ray.direction.y;
      for (let k = 0; k <= 8; k++) {
        const t = t0 + ((t1 - t0) * k) / 8;
        ray.at(t, p);
        if (Math.hypot(p.x - base.x, p.z - base.z) <= RADIUS && (!best || t < best.t)) {
          best = { t, pos: { ...u.pos, z: u.pos.z ?? 0 } };
          break;
        }
      }
    }
    return best?.pos ?? null;
  }

  /**
   * First floor the ray meets, rooftops included. Walls and cover are ignored,
   * so the cursor always lands on a tile a unit could stand on.
   */
  private pickTile(ray: THREE.Ray): Pos | null {
    const s = this.state;
    const top = (Math.max(0, this.level, ...s.elev) + 1) * STOREY;
    if (ray.direction.y >= 0) return null;
    let t = ray.origin.y > top ? (ray.origin.y - top) / -ray.direction.y : 0;
    const end = (ray.origin.y + 0.5) / -ray.direction.y;
    const p = new THREE.Vector3();
    for (; t <= end; t += 0.04) {
      ray.at(t, p);
      const tile = worldToTile(p);
      if (tile.x < 0 || tile.y < 0 || tile.x >= s.width || tile.y >= s.height) continue;
      // The floors of the active storey and those below it (above it the buildings are cut away), highest first.
      const i = tile.y * s.width + tile.x;
      for (let z = Math.min(this.level, s.levels?.length ?? 0); z >= 1; z--) {
        if ((s.levels?.[z - 1]?.tiles[i] ?? null) !== null && p.y <= z * STOREY) return { ...tile, z };
      }
      if (p.y <= (s.elev[i] ?? 0) * STOREY) return { ...tile, z: 0 };
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
    if (this.state.realtime) return this.rtClick(tile as Place);
    const occupant = unitAt(this.state, tile);
    const u = this.selectedUnit();

    // Ally targeting (the Botiquín, Espabilar, Apaños): clicking a soldier picks them.
    if (this.mode.kind === 'target' && ABILITIES[this.mode.ability].target === 'ally' && occupant?.team === 'xcom') {
      if (this.targetId === occupant.id) this.confirmTarget();
      else if (this.targetOptions(this.mode.ability).some((o) => o.unit.id === occupant.id)) {
        this.targetId = occupant.id;
        this.refreshInteractive();
      }
      return;
    }
    // With an area armed, a soldier's tile is a place to throw at like any other: the preview already
    // warns «¡Aliado!», and a grenade upstairs may be meant to sink the floor under one (docs/FASE7.md §5).
    if (occupant?.team === 'xcom' && this.mode.kind !== 'tile') {
      // A turret is nobody's to pick.
      if (occupant.automaton) return;
      if (this.commands(occupant)) this.select(occupant.id);
      else this.hud.toast(`${occupant.name} es de la escuadra de ${occupant.owner !== null && occupant.owner !== undefined ? this.playerName(occupant.owner) : 'el otro jugador'}.`);
      return;
    }
    if (!this.canAct() || !u) return;
    const waiting = this.waitingFor(u.id);
    if (waiting) {
      this.hud.toast(waiting);
      return;
    }
    // Without the command a click plans instead of ordering (#35): aiming and areas only preview.
    const planning = !this.hasCommand();

    // Registrar armed: a click on marked furniture searches it, anywhere else backs out.
    if (this.handArmed()) {
      if (this.handTiles().some((t) => samePlace(t, tile))) this.confirmHand(tile);
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
    // Furniture beside the soldier: the click searches it (18/10 #29).
    // A closed door beside the soldier: the click opens it (free; playtest 13/10 #2).
    if (this.handTiles('openDoor').some((t) => samePlace(t, tile))) {
      const view = this.abilities().find((a) => a.id === 'openDoor');
      if (view?.enabled) this.useDoor('openDoor', tile);
      else if (view?.reason) this.hud.toast(view.reason);
      return;
    }
    if (this.handTiles('search').some((t) => samePlace(t, tile))) {
      const view = this.abilities().find((a) => a.id === 'search');
      if (view?.enabled) this.useDoor('search', tile);
      else if (view?.reason) this.hud.toast(view.reason);
      return;
    }
    const dest = this.reach()?.tiles.find((t) => samePlace(t.pos, tile));
    if (dest) {
      const cost = this.reachCache!.reach.cost.get(nodeOf(this.state, toPos(tile)))!;
      if (apForCost(u, cost) > u.ap) return;
      if (planning) {
        this.togglePlan(u, tile);
        return;
      }
      // Breaking concealment cannot be undone: the first click on such a tile only warns.
      const key = `${u.id}|${tile.x},${tile.y}`;
      if (dest.detected && this.concealWarn !== key) {
        this.concealWarn = key;
        this.hud.toast(`Desde ahí os ven: se rompe la ocultación. Haz clic otra vez para mover.`);
        return;
      }
      this.concealWarn = null;
      this.send({ type: 'move', unit: u.id, to: tile });
    }
  }
}
