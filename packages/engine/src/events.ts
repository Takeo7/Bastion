import { AP_PER_TURN, attackWeapon, CONTROL_TURNS, TEMPLATES, WEAPONS } from './content';
import { getUnit, tileIndex } from './grid';
import type {
  AbilityId,
  AttackAbility,
  BlastAbility,
  ChargeId,
  EndReason,
  GameState,
  Outcome,
  Pod,
  PsiEffect,
  Reinforcement,
  ShotResult,
  Slot,
  Team,
  TileKind,
  Unit,
  Vec2,
} from './types';

/**
 * Everything that changes the game state is an event. The host produces them
 * (rolling dice), and every client replays them with the same reducer, so all
 * copies of the state stay identical without sending full snapshots.
 */
export type GameEvent =
  | { t: 'turnBegan'; team: Team; turn: number; command?: Slot }
  /** The command (el mando) changes hands. */
  | { t: 'commandPassed'; to: Slot }
  | { t: 'moved'; unit: string; path: Vec2[]; ap: number }
  /** An ability was activated: pays its actions (negative = gains), cooldown and charge. */
  | { t: 'abilityUsed'; unit: string; ability: AbilityId; ap: number; cooldown: number; charge: ChargeId | null }
  | {
      t: 'shot';
      unit: string;
      target: string;
      ability: AttackAbility;
      from: Vec2;
      result: ShotResult;
      chance: number;
      reaction: boolean;
    }
  /** `amount` is what got through armour; `mitigated` what the armour stopped. */
  | { t: 'damaged'; unit: string; amount: number; mitigated: number; shred: number }
  /** `by`: the unit whose attack or explosive dealt the killing blow. */
  | { t: 'died'; unit: string; by?: string }
  | { t: 'stunned'; unit: string }
  | { t: 'overwatch'; unit: string; killZone: boolean }
  | { t: 'hunker'; unit: string }
  | { t: 'reload'; unit: string }
  | { t: 'explosive'; unit: string; ability: BlastAbility; target: Vec2 }
  | { t: 'healed'; unit: string; target: string; amount: number }
  | { t: 'aided'; unit: string; target: string; amount: number }
  /** Combat Protocol: the drone shocks a target (never misses). */
  | { t: 'zapped'; unit: string; target: string }
  /** Revival / Restoration: stun, disorientation and panic are gone. */
  | { t: 'cleansed'; unit: string; target: string }
  | { t: 'refund'; unit: string; reason: 'implacable' | 'deathFromAbove' }
  | { t: 'suppressed'; unit: string; target: string }
  /** The suppressor lets go (it acted, moved, died or its turn came). */
  | { t: 'suppressionEnded'; unit: string }
  | { t: 'marked'; unit: string; amount: number }
  | { t: 'ruptured'; unit: string; amount: number }
  | { t: 'psi'; unit: string; target: string; success: boolean; effect: PsiEffect | null; chance: number }
  /** Mind control ends: the soldier fights for XCOM again. */
  | { t: 'released'; unit: string }
  | { t: 'controlTick'; unit: string }
  | { t: 'reanimated'; unit: string; corpse: string; zombie: Unit }
  | { t: 'smokeDeployed'; unit: string; pos: Vec2; radius: number; turns: number }
  /** Flashbang: disoriented until the end of its own next turn. */
  | { t: 'disoriented'; unit: string }
  | { t: 'evacuated'; unit: string }
  | { t: 'itemPicked'; unit: string }
  | { t: 'itemDropped'; pos: Vec2 }
  | { t: 'tileChanged'; pos: Vec2; kind: TileKind }
  | { t: 'podActivated'; pod: string }
  | { t: 'concealmentBroken' }
  | { t: 'ready'; slot: Slot; ready: boolean }
  | { t: 'missionEnded'; outcome: Outcome; reason: EndReason }
  /** Hack attempt on the objective terminal; `remote` when the drone did it. */
  | { t: 'hacked'; unit: string; success: boolean; chance: number; remote: boolean }
  /** A soldier reached the VIP, who now follows that player's orders. */
  | { t: 'vipRescued'; unit: string; by: string; owner: Slot | null }
  /** Flare: reinforcements will land here at the end of the next alien turn. */
  | { t: 'reinforcementsIncoming'; r: Reinforcement }
  /** New units on the battlefield (reinforcements), already formed. */
  | { t: 'spawned'; units: Unit[]; pod: Pod; reinforcement: string | null }
  /** An inactive pod heads for its next patrol waypoint. */
  | { t: 'patrol'; pod: string; index: number };

function unit(s: GameState, id: string) {
  const u = getUnit(s, id);
  if (!u) throw new Error(`Unknown unit ${id}`);
  return u;
}

/** Lets go of whoever `u` was suppressing. */
function endSuppression(s: GameState, u: Unit): void {
  if (!u.suppressing) return;
  const target = getUnit(s, u.suppressing);
  if (target?.suppressedBy === u.id) target.suppressedBy = null;
  u.suppressing = null;
}

function clearStatuses(u: Unit): void {
  u.stunned = false;
  u.disoriented = 0;
  u.panicked = false;
}

export function applyEvent(s: GameState, e: GameEvent): void {
  s.seq++;
  switch (e.t) {
    case 'turnBegan': {
      s.activeTeam = e.team;
      s.turn = e.turn;
      if (e.team === 'xcom') {
        s.ready = [false, false];
        if (e.command !== undefined) s.command = e.command;
        if (s.mission.turnsLeft !== null && e.turn > 1) s.mission.turnsLeft--;
        // Holo marks last for the rest of the squad's turn and the alien turn after it.
        for (const u of s.units) u.marked = 0;
        for (const cloud of s.smoke) cloud.turns--;
        s.smoke = s.smoke.filter((cloud) => cloud.turns > 0);
      }
      for (const u of s.units) {
        if (!u.alive) continue;
        if (u.team !== e.team) {
          // Their turn just ended: disorientation wears off.
          if (u.disoriented > 0) u.disoriented--;
          continue;
        }
        u.ap = u.captive || u.objective ? 0 : u.stunned ? 1 : AP_PER_TURN;
        u.stunned = false;
        u.overwatch = false;
        u.killZone = false;
        u.reactedTo = [];
        u.hunkered = false;
        u.aided = 0;
        u.refunded = false;
        endSuppression(s, u);
        for (const id of Object.keys(u.cooldowns) as AbilityId[]) {
          const left = (u.cooldowns[id] ?? 0) - 1;
          if (left > 0) u.cooldowns[id] = left;
          else delete u.cooldowns[id];
        }
        if (u.panicked) {
          // Cowers for the whole turn.
          u.ap = 0;
          u.hunkered = true;
          u.panicked = false;
        }
      }
      break;
    }
    case 'moved': {
      const u = unit(s, e.unit);
      const last = e.path[e.path.length - 1];
      if (last) u.pos = { ...last };
      u.ap = Math.max(0, u.ap - e.ap);
      endSuppression(s, u);
      break;
    }
    case 'abilityUsed': {
      const u = unit(s, e.unit);
      u.ap = Math.max(0, u.ap - e.ap);
      if (e.cooldown > 0) u.cooldowns[e.ability] = e.cooldown;
      if (e.charge) u.charges[e.charge] = Math.max(0, (u.charges[e.charge] ?? 0) - 1);
      if (e.ability !== 'overwatch' && e.ability !== 'killZone' && e.ap > 0) endSuppression(s, u);
      break;
    }
    case 'shot': {
      const u = unit(s, e.unit);
      if (attackWeapon(u, e.ability).clip > 0) u.ammo = Math.max(0, u.ammo - 1);
      if (e.reaction && u.overwatch) {
        if (u.killZone) u.reactedTo.push(e.target);
        else u.overwatch = false;
      }
      break;
    }
    case 'damaged': {
      const u = unit(s, e.unit);
      u.hp = Math.max(0, u.hp - e.amount);
      u.armor = Math.max(0, u.armor - e.shred);
      break;
    }
    case 'died': {
      const u = unit(s, e.unit);
      u.alive = false;
      u.hp = 0;
      u.ap = 0;
      u.overwatch = false;
      u.hunkered = false;
      u.aided = 0;
      endSuppression(s, u);
      if (u.suppressedBy) {
        const by = getUnit(s, u.suppressedBy);
        if (by) by.suppressing = null;
        u.suppressedBy = null;
      }
      if (e.by) {
        const killer = getUnit(s, e.by);
        if (killer && killer.team !== u.team) killer.kills++;
      }
      if (s.mission.kind === 'sabotage' && s.mission.objectiveUnit === u.id) completeObjective(s);
      break;
    }
    case 'stunned':
      unit(s, e.unit).stunned = true;
      break;
    case 'overwatch': {
      const u = unit(s, e.unit);
      u.overwatch = true;
      u.killZone = e.killZone;
      u.reactedTo = [];
      break;
    }
    case 'hunker':
      unit(s, e.unit).hunkered = true;
      break;
    case 'reload': {
      const u = unit(s, e.unit);
      u.ammo = WEAPONS[TEMPLATES[u.template].weapon].clip;
      break;
    }
    case 'explosive':
      break;
    case 'healed': {
      const target = unit(s, e.target);
      target.hp = Math.min(target.maxHp, target.hp + e.amount);
      break;
    }
    case 'aided':
      unit(s, e.target).aided = e.amount;
      break;
    case 'zapped':
      break;
    case 'cleansed':
      clearStatuses(unit(s, e.target));
      break;
    case 'refund': {
      const u = unit(s, e.unit);
      u.ap += 1;
      u.refunded = true;
      break;
    }
    case 'suppressed': {
      const u = unit(s, e.unit);
      const target = unit(s, e.target);
      u.suppressing = target.id;
      target.suppressedBy = u.id;
      u.ammo = Math.max(0, u.ammo - 2);
      break;
    }
    case 'suppressionEnded':
      endSuppression(s, unit(s, e.unit));
      break;
    case 'marked':
      unit(s, e.unit).marked = e.amount;
      break;
    case 'ruptured': {
      const u = unit(s, e.unit);
      u.ruptured = Math.max(u.ruptured, e.amount);
      break;
    }
    case 'psi': {
      if (!e.success) break;
      const target = unit(s, e.target);
      if (e.effect === 'disoriented') target.disoriented = 1;
      else if (e.effect === 'panicked') target.panicked = true;
      else if (e.effect === 'controlled') {
        const caster = unit(s, e.unit);
        target.team = 'alien';
        target.controlledBy = caster.id;
        target.controlTurns = CONTROL_TURNS;
        target.podId = caster.podId;
        target.ap = 0;
        target.overwatch = false;
        target.hunkered = false;
        endSuppression(s, target);
      }
      break;
    }
    case 'controlTick': {
      const u = unit(s, e.unit);
      u.controlTurns = Math.max(0, u.controlTurns - 1);
      break;
    }
    case 'released': {
      const u = unit(s, e.unit);
      u.team = 'xcom';
      u.controlledBy = null;
      u.controlTurns = 0;
      u.podId = null;
      u.ap = 0;
      u.overwatch = false;
      break;
    }
    case 'reanimated': {
      unit(s, e.corpse).reanimated = true;
      const z = JSON.parse(JSON.stringify(e.zombie)) as Unit;
      s.units.push(z);
      const pod = s.pods.find((p) => p.id === z.podId);
      if (pod) pod.unitIds.push(z.id);
      break;
    }
    case 'smokeDeployed':
      s.smoke.push({ pos: { ...e.pos }, radius: e.radius, turns: e.turns });
      break;
    case 'disoriented':
      unit(s, e.unit).disoriented = 1;
      break;
    case 'evacuated': {
      const u = unit(s, e.unit);
      u.alive = false;
      u.evacuated = true;
      u.ap = 0;
      u.overwatch = false;
      endSuppression(s, u);
      const item = s.mission.item;
      if (item?.carrier === u.id) {
        item.carrier = null;
        item.evacuated = true;
        completeObjective(s);
      }
      if (s.mission.kind === 'rescue' && s.mission.objectiveUnit === u.id) completeObjective(s);
      break;
    }
    case 'itemPicked': {
      const item = s.mission.item;
      if (item) {
        item.carrier = e.unit;
        item.pos = null;
      }
      break;
    }
    case 'itemDropped': {
      const item = s.mission.item;
      if (item) {
        item.carrier = null;
        item.pos = { ...e.pos };
      }
      break;
    }
    case 'tileChanged':
      s.tiles[tileIndex(s, e.pos.x, e.pos.y)] = e.kind;
      break;
    case 'podActivated': {
      const pod = s.pods.find((p) => p.id === e.pod);
      if (pod) pod.active = true;
      break;
    }
    case 'concealmentBroken':
      s.concealed = false;
      break;
    case 'commandPassed':
      s.command = e.to;
      break;
    case 'ready':
      s.ready[e.slot] = e.ready;
      break;
    case 'missionEnded':
      s.outcome = e.outcome;
      s.outcomeReason = e.reason;
      break;
    case 'hacked':
      if (e.success) completeObjective(s);
      break;
    case 'vipRescued': {
      const vip = unit(s, e.unit);
      vip.captive = false;
      vip.owner = e.owner;
      break;
    }
    case 'reinforcementsIncoming': {
      const list = s.mission.reinforcements;
      const i = list.findIndex((r) => r.id === e.r.id);
      const copy = { ...e.r, pos: e.r.pos ? { ...e.r.pos } : null, units: [...e.r.units] };
      if (i >= 0) list[i] = copy;
      else list.push(copy);
      break;
    }
    case 'spawned': {
      for (const u of e.units) s.units.push(JSON.parse(JSON.stringify(u)) as Unit);
      s.pods.push(JSON.parse(JSON.stringify(e.pod)) as Pod);
      const r = s.mission.reinforcements.find((x) => x.id === e.reinforcement);
      if (r) r.landed = true;
      break;
    }
    case 'patrol': {
      const pod = s.pods.find((p) => p.id === e.pod);
      if (pod) pod.patrolIndex = e.index;
      break;
    }
  }
}

/** The mission's objective is secured: the timer stops. */
function completeObjective(s: GameState): void {
  s.mission.objectiveDone = true;
  s.mission.turnsLeft = null;
}

/** The state is plain JSON by design, so a JSON round-trip is a faithful deep copy. */
export function cloneState(s: GameState): GameState {
  return JSON.parse(JSON.stringify(s)) as GameState;
}
