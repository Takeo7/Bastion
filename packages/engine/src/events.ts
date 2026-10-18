import { BAD_LUCK_MISSES } from './aim';
import { ABILITIES, addMods, apPerTurn, attackWeapon, conditionOf, firesPrimary, wearOf, clipOf, CONTROL_TURNS, firesSidearm, FOUND_WEAPONS, hordePoints, ITEMS, reloadWeapon, TEMPLATES, THROWN, WEAPONS } from './content';
import { decorCode } from './decor';
import { getUnit, nodeOf, tileIndex } from './grid';
import { facingTo } from './los';
import { fallenGear, packSlots } from './loot';
import { growthDelta, type DeedId, type SkillId } from './skills';
import type {
  AbilityId,
  AttackAbility,
  BlastAbility,
  ChargeId,
  ConcealReason,
  Decor,
  DecorKind,
  EndReason,
  FoundWeaponId,
  GameState,
  Light,
  Loot,
  Outcome,
  Pod,
  PodCause,
  PsiEffect,
  Reinforcement,
  ShotResult,
  Slot,
  Team,
  TileKind,
  Pos,
  Unit,
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
  /** The soldier in play changes (playtest 2026-10-15, #9); null, none. */
  | { t: 'unitActive'; unit: string | null }
  | { t: 'moved'; unit: string; path: Pos[]; ap: number }
  /** An ability was activated: pays its actions (negative = gains), cooldown and charge. */
  | { t: 'abilityUsed'; unit: string; ability: AbilityId; ap: number; cooldown: number; charge: ChargeId | null }
  | {
      t: 'shot';
      unit: string;
      target: string;
      ability: AttackAbility;
      from: Pos;
      result: ShotResult;
      chance: number;
      /** The d100, 1–100: it hits when `roll ≤ chance`. */
      roll: number;
      reaction: boolean;
    }
  /** `amount` is what got through armour; `mitigated` what the armour stopped. */
  | { t: 'damaged'; unit: string; amount: number; mitigated: number; shred: number }
  /** `by`: the unit whose attack or explosive dealt the killing blow. */
  | { t: 'died'; unit: string; by?: string }
  | { t: 'stunned'; unit: string }
  /** `melee`: with the blade (docs/FASE10.md §4), it strikes whoever comes alongside. */
  | { t: 'overwatch'; unit: string; killZone: boolean; melee?: boolean }
  | { t: 'hunker'; unit: string }
  /** A stone thrown at `target` (docs/FASE10.md §4); `hit`, who was standing there. */
  | { t: 'stoneThrown'; unit: string; target: Pos; hit: string | null }
  /** A bar filled out there (docs/FASE10.md §2): its line's stat goes up `growth` percent at once. `filled`, the bars of that line now. */
  | { t: 'barFilled'; unit: string; skill: SkillId; filled: number; growth: number }
  | { t: 'reload'; unit: string }
  | { t: 'explosive'; unit: string; ability: BlastAbility; target: Pos }
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
  /** Mind control ends: the soldier fights for XCOM again. Nothing takes control any more: kept for missions saved before. */
  | { t: 'released'; unit: string }
  | { t: 'controlTick'; unit: string }
  /**
   * A corpse rises as a zombie: raised by a Chillón (`unit`), or by itself, an
   * infected soldier fallen in the fight (docs/FASE3.md): then `pod` is its own new group.
   */
  | { t: 'reanimated'; unit?: string; corpse: string; zombie: Unit; pod?: Pod }
  /** Bitten and infected (`by`: the zombie, or the Hinchado that burst). */
  | { t: 'infected'; unit: string; by: string }
  | { t: 'smokeDeployed'; unit: string; pos: Pos; radius: number; turns: number }
  /** Flashbang: disoriented until the end of its own next turn. */
  | { t: 'disoriented'; unit: string }
  | { t: 'evacuated'; unit: string }
  | { t: 'itemPicked'; unit: string }
  | { t: 'itemDropped'; pos: Pos }
  /** A player asks to evacuate, from `unit` in the bus; the partner answers (docs/PRUEBA-2026-10-14.md P1). */
  | { t: 'evacCalled'; slot: Slot; unit: string }
  /** The partner said no: everyone stays. */
  | { t: 'evacRefused'; slot: Slot }
  /** A player asks to abandon the mission; the partner answers (playtest 16/10), as with the bus. */
  | { t: 'abandonCalled'; slot: Slot }
  | { t: 'abandonRefused'; slot: Slot }
  /** The bus leaves: `evacuated` for whoever is in it and `leftBehind` for the rest follow. */
  | { t: 'evacDeparted' }
  /** Not in the bus when it left: lost (docs/PRUEBA-2026-10-14.md P1). */
  | { t: 'leftBehind'; unit: string }
  /** `decor`: what it is to look like from now on (a breached wall); without it, the decor stays. */
  | { t: 'tileChanged'; pos: Pos; kind: TileKind; decor?: DecorKind }
  /** A trap of the base goes off under one of the dead (docs/FASE12.md §11.4): one use less. Its harm follows (`damaged`, `stunned`). */
  | { t: 'trapSprung'; trap: string; unit: string }
  /** One of the dead beats on a door or a barricade (docs/FASE12.md §4): `left`, what it still holds; at 0 a `tileChanged` follows, it gave way. */
  | { t: 'smashed'; unit: string; tile: Pos; amount: number; left: number }
  /** A storey's slab gives way under a grenade (docs/FASE7.md §5): the place is open air. */
  | { t: 'floorBroken'; pos: Pos }
  /** Down through a broken floor onto `to` (docs/FASE7.md §5); the damage comes after, as `damaged`. */
  | { t: 'fell'; unit: string; from: Pos; to: Pos }
  /** `unit`: the soldier they saw (or who attacked them); `by`: the alien that saw it. */
  | { t: 'podActivated'; pod: string; cause: PodCause; unit?: string; by?: string }
  /**
   * Who gave the squad away and how: `unit` is the soldier seen (or who fired,
   * sang or hacked), `by` the alien that saw it, `range` its detection in tiles.
   */
  | { t: 'concealmentBroken'; reason: ConcealReason; unit?: string; by?: string; range?: number }
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
  | { t: 'patrol'; pod: string; index: number }
  // Noise and searching (docs/FASE2.md).
  /**
   * Something `by` did was heard `radius` tiles around `tile`. `heard`: the
   * enemies that now go to look (Unit.heard). The squad's noise also winds up
   * the Saqueo's horde.
   */
  | { t: 'noise'; tile: Pos; radius: number; by?: string; heard: string[] }
  /** It reached what it heard and found nobody: it stops looking for it. */
  | { t: 'forgot'; unit: string }
  /** A soldier searched the furniture on `tile`; `loot` went into the backpack (null: nothing). */
  | { t: 'searched'; unit: string; tile: Pos; loot: Loot | null }
  /** A room searched, by its id (loot.ts roomAt; docs/FASE8.md §B); what it turned up, as `searched`. */
  | { t: 'roomSearched'; unit: string; room: number; loot: Loot | null }
  /** The backpack (docs/FASE8.md §C): a thing left on the soldier's tile, free; it becomes `dropped` l<n>. */
  | { t: 'lootDropped'; unit: string; index: number }
  /** A thing picked up from the soldier's tile (GameState.dropped), free. */
  | { t: 'lootTaken'; unit: string; id: string }
  /** A thing handed to a partner next to the soldier, for an action. */
  | { t: 'lootGiven'; unit: string; to: string; index: number }
  /** A medicine used from the backpack, for an action; the cure itself comes as `healed` and `cured`. */
  | { t: 'medicineUsed'; unit: string; target: string; index: number }
  /** A weapon from the backpack taken in hand (or at the belt), for an action: the one it replaces goes into the backpack, or to the floor. */
  | { t: 'weaponEquipped'; unit: string; index: number }
  /** A vest from the backpack put on (playtest 16/10 #18): its health at once, the new points full. */
  | { t: 'vestWorn'; unit: string; index: number }
  /** A belt thing from the backpack put on the belt (playtest 18/10 #5), for an action: its uses, its ability and what it gives, at once. */
  | { t: 'beltEquipped'; unit: string; index: number }
  /** A bite's contagion stopped (docs/FASE8.md §C). */
  | { t: 'cured'; unit: string; by: string }
  /** Apaños (docs/FASE9.md): `amount` of condition back to the weapon in `target`'s hand. */
  | { t: 'patched'; unit: string; target: string; amount: number }
  // Night (docs/ALTURA-Y-NOCHE.md).
  /** A light goes on at night (a Bengala, a fire): whoever stands in it is seen from as far as by day. */
  | { t: 'lightOn'; light: Light }
  /** A Bengala or a fire burns out. */
  | { t: 'lightOff'; id: string }
  // Velmar's Canto.
  /** A song is sung (for the effects): on whom, or where. */
  | { t: 'sung'; unit: string; ability: AbilityId; targets: string[]; tile?: Pos }
  /** Salto de eco, Paso de sombra. */
  | { t: 'teleported'; unit: string; from: Pos; to: Pos }
  /** Juramento de Velmar: turns it lasts. */
  | { t: 'oath'; unit: string; turns: number }
  /** Contracanto: turns of immunity to the Cantores. */
  | { t: 'warded'; unit: string; turns: number }
  /** Himno, Paso de sombra: aim until the unit's next turn. */
  | { t: 'blessed'; unit: string; aim: number }
  /** Marca del cazador: `by` gets +15 aim and crit against `unit` until it dies. */
  | { t: 'hunted'; unit: string; by: string }
  /** Último verso: back on its feet. */
  | { t: 'revived'; unit: string; hp: number }
  /** Silencio: a zone where the Cantores can't sing. */
  | { t: 'silenced'; pos: Pos; radius: number; turns: number };

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

/** Counts something a soldier of the squad did that teaches (Fase 9, skills.ts). */
function deed(s: GameState, id: string, what: DeedId): void {
  const u = getUnit(s, id);
  if (!u || u.team !== 'xcom' || u.owner === null || u.captive) return;
  const deeds = (u.deeds ??= {});
  deeds[what] = (deeds[what] ?? 0) + 1;
}

/** What was thrown last reached an enemy `u`: it teaches once (docs/FASE10.md §3). */
function throwLanded(s: GameState, u: Unit): void {
  const blow = s.blow;
  if (blow?.kind !== 'blast' || blow.landed || getUnit(s, blow.unit)?.team === u.team) return;
  blow.landed = true;
  deed(s, blow.unit, 'throwHit');
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
      // A call to evacuate nobody answered lapses with the squad's turn.
      delete s.evacCall;
      if (e.team === 'xcom') {
        s.ready = [false, false];
        delete s.activeUnit;
        if (e.command !== undefined) s.command = e.command;
        if (s.mission.turnsLeft !== null && e.turn > 1) s.mission.turnsLeft--;
        // Holo marks last for the rest of the squad's turn and the alien turn after it.
        for (const u of s.units) u.marked = 0;
        for (const cloud of s.smoke) cloud.turns--;
        s.smoke = s.smoke.filter((cloud) => cloud.turns > 0);
        for (const zone of s.silences ?? []) zone.turns--;
        if (s.silences) s.silences = s.silences.filter((zone) => zone.turns > 0);
        // Bengalas and fires burn down (the last turn goes out with a lightOff before this).
        for (const light of s.lights ?? []) if (light.turns !== undefined) light.turns--;
      }
      for (const u of s.units) {
        if (!u.alive) continue;
        if (u.team !== e.team) {
          // Their turn just ended: disorientation wears off.
          if (u.disoriented > 0) u.disoriented--;
          continue;
        }
        // A turret has no turn of its own: it keeps watch in the enemy's (docs/FASE12.md §11.4).
        u.ap = u.captive || u.objective || u.automaton ? 0 : u.stunned ? 1 : apPerTurn(u);
        u.stunned = false;
        u.overwatch = false;
        u.killZone = false;
        u.reactedTo = [];
        u.hunkered = false;
        u.aided = 0;
        u.refunded = false;
        // Songs that last a number of the unit's own turns.
        if (u.blessed) u.blessed = 0;
        if (u.oath) u.oath--;
        if (u.ward) u.ward--;
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
      // It looks the way it walked (los.ts inViewCone: the dead see ahead of them).
      const before = e.path.length > 1 ? e.path[e.path.length - 2]! : u.pos;
      if (last) u.facing = facingTo(before, last) ?? u.facing;
      if (last) u.pos = { ...last };
      u.ap = Math.max(0, u.ap - e.ap);
      endSuppression(s, u);
      // Running, with both actions, teaches Forma física (docs/FASE10.md §3).
      if (e.ap >= 2) deed(s, u.id, 'run');
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
      const aim = getUnit(s, e.target);
      if (aim) u.facing = facingTo(u.pos, aim.pos) ?? u.facing;
      if (firesSidearm(u, e.ability)) u.pistolAmmo = Math.max(0, (u.pistolAmmo ?? 0) - 1);
      else if (clipOf(u, e.ability) > 0) u.ammo = Math.max(0, u.ammo - 1);
      if (e.reaction && u.overwatch) {
        if (u.killZone) u.reactedTo.push(e.target);
        else u.overwatch = false;
      }
      // Bad luck: a player's misses in a row add up (aim.ts badLuckAim), a hit clears them.
      if (u.team === 'xcom' && u.owner !== null && !u.controlledBy) {
        const streak = (s.missStreak ??= [0, 0]);
        streak[u.owner] = e.result === 'miss' ? Math.min(BAD_LUCK_MISSES, streak[u.owner] + 1) : 0;
      }
      // Every shot wears the weapon in the hand (docs/FASE9.md §5); half with Mantenimiento.
      if (firesPrimary(u, e.ability)) u.arms!.condition = Math.max(0, conditionOf(u) - wearOf(attackWeapon(u, e.ability).id) / (u.mods.care ? 2 : 1));
      const blade = ABILITIES[e.ability].weapon === 'melee' || attackWeapon(u, e.ability).melee === true;
      s.blow = { unit: u.id, kind: blade ? 'blade' : 'gun' };
      // Each one at an enemy teaches, and more if it lands (docs/FASE10.md §3).
      if (getUnit(s, e.target)?.team !== u.team) {
        deed(s, u.id, blade ? 'swing' : 'shot');
        if (e.result !== 'miss') deed(s, u.id, blade ? 'blow' : 'hit');
      }
      break;
    }
    case 'damaged': {
      const u = unit(s, e.unit);
      u.hp = Math.max(0, u.hp - e.amount);
      u.armor = Math.max(0, u.armor - e.shred);
      throwLanded(s, u);
      break;
    }
    case 'died': {
      const u = unit(s, e.unit);
      u.alive = false;
      u.hp = 0;
      u.fellTurn = s.turn;
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
        if (killer && killer.team !== u.team) {
          killer.kills++;
          // Silencing the alarm teaches Maña, however it went; the rest, the weapon that killed.
          if (u.objective) deed(s, killer.id, 'fix');
          else if (s.blow?.unit === killer.id && s.blow.kind !== 'blast') deed(s, killer.id, s.blow.kind === 'blade' ? 'bladeKill' : 'gunKill');
        }
      }
      if (s.mission.kind === 'sabotage' && s.mission.objectiveUnit === u.id) completeObjective(s);
      // A soldier leaves what it carried where it fell, for another to take (Arturo, 18/10 #49).
      if (u.team === 'xcom' && !u.automaton && !u.objective && !u.captive) {
        for (const loot of fallenGear(u)) leaveOnFloor(s, u.pos, loot, u.id);
        u.backpack = [];
      }
      break;
    }
    case 'stunned':
      unit(s, e.unit).stunned = true;
      break;
    case 'overwatch': {
      const u = unit(s, e.unit);
      u.overwatch = true;
      u.killZone = e.killZone;
      u.watchMelee = !!e.melee;
      u.reactedTo = [];
      break;
    }
    case 'hunker':
      unit(s, e.unit).hunkered = true;
      deed(s, e.unit, 'hunker');
      break;
    case 'stoneThrown':
      s.blow = { unit: e.unit, kind: 'blast' };
      deed(s, e.unit, 'throw');
      break;
    case 'barFilled': {
      const u = unit(s, e.unit);
      if (!u.lines) break;
      const before = u.lines.growth[e.skill];
      u.lines.growth[e.skill] = before + e.growth;
      (u.lines.rolls[e.skill] ??= []).push(e.growth);
      // Its stat, at once: health too, the new points full.
      const delta = growthDelta(e.skill, before, before + e.growth);
      u.mods = addMods(u.mods, delta.mods);
      u.maxHp += delta.hp;
      u.hp += delta.hp;
      break;
    }
    case 'reload': {
      const u = unit(s, e.unit);
      reloadWeapon(u);
      if (u.mags !== undefined) u.mags = Math.max(0, u.mags - 1);
      break;
    }
    case 'explosive':
      s.blow = { unit: e.unit, kind: 'blast' };
      if (THROWN.has(e.ability)) deed(s, e.unit, 'throw');
      break;
    case 'healed': {
      const target = unit(s, e.target);
      target.hp = Math.min(target.maxHp, target.hp + e.amount);
      if (e.amount > 0) deed(s, e.unit, e.unit === e.target ? 'healSelf' : 'healOther');
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
      // Only in missions saved before the zombies: the scream never takes control.
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
      if (e.pod) s.pods.push(JSON.parse(JSON.stringify(e.pod)) as Pod);
      const pod = s.pods.find((p) => p.id === z.podId);
      if (pod) pod.unitIds.push(z.id);
      break;
    }
    case 'infected':
      unit(s, e.unit).infected = true;
      break;
    case 'smokeDeployed':
      s.smoke.push({ pos: { ...e.pos }, radius: e.radius, turns: e.turns });
      break;
    case 'sung':
      break;
    case 'teleported':
      unit(s, e.unit).pos = { ...e.to };
      break;
    case 'oath':
      unit(s, e.unit).oath = e.turns;
      break;
    case 'warded':
      unit(s, e.unit).ward = e.turns;
      break;
    case 'blessed': {
      const u = unit(s, e.unit);
      u.blessed = Math.max(u.blessed ?? 0, e.aim);
      break;
    }
    case 'hunted':
      unit(s, e.unit).huntedBy = e.by;
      break;
    case 'revived': {
      const u = unit(s, e.unit);
      u.alive = true;
      u.hp = Math.min(u.maxHp, e.hp);
      u.ap = 0;
      u.fellTurn = undefined;
      break;
    }
    case 'silenced':
      (s.silences ??= []).push({ pos: { ...e.pos }, radius: e.radius, turns: e.turns });
      break;
    case 'disoriented':
      unit(s, e.unit).disoriented = 1;
      throwLanded(s, unit(s, e.unit));
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
    case 'evacCalled':
      s.evacCall = { by: e.slot, unit: e.unit };
      break;
    case 'evacRefused':
      delete s.evacCall;
      break;
    case 'abandonCalled':
      s.abandonCall = { by: e.slot };
      break;
    case 'abandonRefused':
      delete s.abandonCall;
      break;
    case 'evacDeparted':
      delete s.evacCall;
      s.mission.evacDone = true;
      break;
    case 'leftBehind': {
      const u = unit(s, e.unit);
      u.alive = false;
      u.lost = true;
      u.ap = 0;
      u.overwatch = false;
      endSuppression(s, u);
      // What it carried stays with it.
      if (s.mission.item?.carrier === u.id) s.mission.item.carrier = null;
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
    case 'floorBroken': {
      const level = e.pos.z > 0 ? s.levels?.[e.pos.z - 1] : undefined;
      if (level) level.tiles[tileIndex(s, e.pos.x, e.pos.y)] = null;
      break;
    }
    case 'fell': {
      const u = unit(s, e.unit);
      u.pos = { ...e.to };
      u.overwatch = false;
      u.hunkered = false;
      break;
    }
    case 'tileChanged': {
      // On the ground, or on a storey's slab (docs/FASE6-PLAN.md).
      const tiles = e.pos.z === 0 ? s.tiles : s.levels?.[e.pos.z - 1]?.tiles;
      if (tiles) tiles[tileIndex(s, e.pos.x, e.pos.y)] = e.kind;
      // A door or barricade of a siege that is gone (a blast took it): a breach.
      const holds = s.breakables;
      const node = nodeOf(s, e.pos);
      if (holds && node in holds && e.kind !== 'door' && e.kind !== 'doorOpen' && e.kind !== 'low') holds[node] = 0;
      if (e.decor) {
        // A breached wall keeps its look through a reload (docs/FASE7.md §4).
        const level = e.pos.z === 0 ? s : s.levels?.[e.pos.z - 1];
        if (level) (level.decor ??= new Array<Decor>(s.width * s.height).fill(0))[tileIndex(s, e.pos.x, e.pos.y)] = decorCode(e.decor);
      }
      break;
    }
    case 'trapSprung': {
      const trap = s.traps?.find((t) => t.piece === e.trap);
      if (trap) trap.charges = Math.max(0, trap.charges - 1);
      break;
    }
    case 'smashed': {
      const u = unit(s, e.unit);
      u.facing = facingTo(u.pos, e.tile) ?? u.facing;
      if (s.breakables) s.breakables[nodeOf(s, e.tile)] = Math.max(0, e.left);
      break;
    }
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
      delete s.activeUnit;
      break;
    case 'unitActive':
      if (e.unit) s.activeUnit = e.unit;
      else delete s.activeUnit;
      break;
    case 'ready':
      s.ready[e.slot] = e.ready;
      break;
    case 'missionEnded':
      s.outcome = e.outcome;
      s.outcomeReason = e.reason;
      break;
    case 'hacked':
      if (e.success) {
        completeObjective(s);
        deed(s, e.unit, 'fix');
      }
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
    case 'noise': {
      // Whoever hears it turns to look.
      for (const id of e.heard) {
        const u = unit(s, id);
        u.heard = { ...e.tile };
        u.facing = facingTo(u.pos, e.tile) ?? u.facing;
      }
      const by = e.by ? getUnit(s, e.by) : undefined;
      if (by?.team === 'xcom') s.mission.noise = (s.mission.noise ?? 0) + hordePoints(e.radius);
      break;
    }
    case 'forgot':
      unit(s, e.unit).heard = null;
      break;
    case 'searched': {
      (s.searched ??= []).push(nodeOf(s, e.tile));
      if (e.loot) stow(s, unit(s, e.unit), e.loot);
      deed(s, e.unit, 'search');
      break;
    }
    case 'lootDropped': {
      const u = unit(s, e.unit);
      const [loot] = u.backpack!.splice(e.index, 1);
      leaveOnFloor(s, u.pos, loot);
      break;
    }
    case 'lootTaken': {
      const at = s.dropped!.findIndex((d) => d.id === e.id);
      const [left] = s.dropped!.splice(at, 1);
      stow(s, unit(s, e.unit), left.loot);
      break;
    }
    case 'lootGiven': {
      const u = unit(s, e.unit);
      u.ap = Math.max(0, u.ap - 1);
      const [loot] = u.backpack!.splice(e.index, 1);
      stow(s, unit(s, e.to), loot);
      break;
    }
    case 'medicineUsed': {
      const u = unit(s, e.unit);
      u.ap = Math.max(0, u.ap - 1);
      const loot = u.backpack![e.index];
      loot.medicines = (loot.medicines ?? 1) - 1;
      if (loot.medicines <= 0) {
        delete loot.medicines;
        if (!Object.keys(loot).length) u.backpack!.splice(e.index, 1);
      }
      break;
    }
    case 'patched': {
      const target = unit(s, e.target);
      if (target.arms?.primary) target.arms.condition = Math.min(100, conditionOf(target) + e.amount);
      break;
    }
    case 'cured':
      unit(s, e.unit).infected = false;
      deed(s, e.by, e.by === e.unit ? 'healSelf' : 'healOther');
      break;
    case 'weaponEquipped': {
      const u = unit(s, e.unit);
      u.ap = Math.max(0, u.ap - 1);
      const [loot] = u.backpack!.splice(e.index, 1);
      equipWeapon(s, u, loot);
      break;
    }
    case 'vestWorn': {
      const u = unit(s, e.unit);
      u.ap = Math.max(0, u.ap - 1);
      const [loot] = u.backpack!.splice(e.index, 1);
      const hp = ITEMS[loot!.item!].hp ?? 0;
      u.maxHp += hp;
      u.hp += hp;
      (u.worn ??= []).push(loot!.item!);
      break;
    }
    case 'beltEquipped': {
      const u = unit(s, e.unit);
      u.ap = Math.max(0, u.ap - 1);
      const [loot] = u.backpack!.splice(e.index, 1);
      const def = ITEMS[loot!.item!];
      u.gear.push(def.id);
      for (const [charge, n] of Object.entries(def.charges ?? {}) as [ChargeId, number][]) u.charges[charge] = (u.charges[charge] ?? 0) + n;
      if (def.mods) u.mods = addMods(u.mods, def.mods);
      (u.belted ??= []).push(def.id);
      break;
    }
    case 'roomSearched': {
      (s.roomsSearched ??= []).push(e.room);
      if (e.loot) stow(s, unit(s, e.unit), e.loot);
      deed(s, e.unit, 'search');
      break;
    }
    case 'lightOn':
      (s.lights ??= []).push({ ...e.light, tile: { ...e.light.tile }, ...(e.light.tiles ? { tiles: [...e.light.tiles] } : {}) });
      break;
    case 'lightOff':
      if (s.lights) s.lights = s.lights.filter((l) => l.id !== e.id);
      break;
  }
}

/**
 * Where a find goes (docs/FASE7.md, FASE8.md §C): magazines straight to the
 * spares; anything else into the backpack, or onto the floor at the finder's
 * feet when it is full, to be swapped for something there.
 */
/**
 * A weapon in hand (docs/FASE8.md §C), loaded: the one it replaces goes into the backpack (or to the floor), with
 * its Armería id if it was the soldier's own. A new main gun leaves the pistol beside it with the rounds it had.
 */
/** A weapon from the backpack in hand (or at the belt); the one it replaces goes into the backpack with its condition and mods. */
function equipWeapon(s: GameState, u: Unit, loot: Loot): void {
  const kind = loot.weapon!;
  const id = loot.weaponId;
  const arms = u.arms!;
  const melee = FOUND_WEAPONS[kind].slot === 'melee';
  const old = melee ? arms.melee : arms.primary;
  const oldId = melee ? arms.meleeId : arms.primaryId;
  const kept: Loot = melee ? {} : { ...(arms.condition !== undefined ? { condition: arms.condition } : {}), ...(arms.mods?.length ? { mods: [...arms.mods] } : {}) };
  if (melee) {
    arms.melee = kind;
    if (id) arms.meleeId = id;
    else delete arms.meleeId;
  } else {
    if (!arms.primary) u.pistolAmmo = u.ammo;
    arms.primary = kind;
    if (id) arms.primaryId = id;
    else delete arms.primaryId;
    if (loot.condition !== undefined) arms.condition = loot.condition;
    else delete arms.condition;
    if (loot.mods?.length) arms.mods = [...loot.mods];
    else delete arms.mods;
    u.ammo = clipOf(u, 'shoot');
    u.weaponName = FOUND_WEAPONS[kind].name;
  }
  if (old) stow(s, u, { weapon: old, ...(oldId ? { weaponId: oldId } : {}), ...kept });
}

function stow(s: GameState, u: Unit, loot: Loot): void {
  if (loot.mags) {
    u.mags = (u.mags ?? 0) + loot.mags;
    return;
  }
  if ((u.backpack?.length ?? 0) < packSlots(u)) (u.backpack ??= []).push({ ...loot });
  else leaveOnFloor(s, u.pos, loot);
}

/** Something left on the floor of `pos`, with the next id. */
function leaveOnFloor(s: GameState, pos: Pos, loot: Loot, from?: string): void {
  s.droppedCount = (s.droppedCount ?? 0) + 1;
  (s.dropped ??= []).push({ id: `l${s.droppedCount}`, pos: { ...pos }, loot: { ...loot }, ...(from ? { from } : {}) });
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
