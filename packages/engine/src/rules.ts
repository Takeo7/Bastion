import {
  abilityBlocker,
  abilityTargets,
  apNeeded,
  besideEnemies,
  blastTiles,
  breachedWalls,
  breaksFloor,
  canTargetTile,
  endsTurn,
  hackChance,
  hasBadStatus,
  inEvacZone,
  meleeTargets,
  noiseOf,
  psiChance,
  rangedTargets,
  songTargets,
  type MeleeOption,
} from './abilities';
import { chooseScamperTile, onlyWalks, patrolPod, takeAlienTurn } from './ai';
import {
  adjacent,
  damageRange,
  damageTaken,
  inMeleeReach,
  previewShot,
  rangeOn,
  resolveShot,
  rollDamage,
  shotDamage,
  shotDamageMods,
  type DamagePreview,
  type ShotPreview,
} from './aim';
import {
  ABILITIES,
  AID_DEFENSE,
  attackWeapon,
  FALL_DAMAGE,
  hasPerk,
  hasSquadsight,
  HOLO_AIM,
  HORDE,
  INFECTION,
  isAttack,
  loadedFor,
  MEDICINE_HEAL,
  MEDKIT_HEAL,
  PATCH_UP,
  conditionOf,
  NIGHT,
  NOISE,
  RESTORATION_HEAL,
  RUPTURE_DAMAGE,
  SMOKE_TURNS,
  TEMPLATES,
  WEAPONS,
} from './content';
import { applyEvent, type GameEvent } from './events';
import {
  canStandOn,
  combatants,
  DIRS8,
  distance,
  getUnit,
  heightAt,
  inBounds,
  kindAt,
  nodeOf,
  placesOn,
  samePos,
  sameSpace,
  slabAt,
  sameTile,
  storeysApart,
  tileAt,
  tileIndex,
  toPos,
  unitAt,
} from './grid';
import { packSlots, rollBox, rollRoom, roomAt, searchSpot } from './loot';
import { canSee, detectionTo, isViewer, lightTiles, sightOrigin, sightTo } from './los';
import { apForCost, buildPath, computeReach, costPerAp, maxMoveCost, onPatrol } from './path';
import type { Command } from './protocol';
import type { Rng } from './rng';
import { hordeWave, makeUnit } from './setup';
import type {
  AbilityId,
  AttackAbility,
  BlastAbility,
  ConcealReason,
  GameState,
  Pod,
  PodCause,
  PsiEffect,
  Reinforcement,
  ShotResult,
  Slot,
  Pos,
  TileKind,
  Unit,
} from './types';

// Velmar's Canto (docs/LORE.md, «El Canto de Velmar»).
/** Égida: defence for the singer and the allies beside it. */
const AEGIS_DEFENSE = 20;
/** Himno: aim and defence for the allies around. */
const HYMN_BONUS = 10;
/** Paso de sombra to higher ground: aim for the rest of the turn. */
const SHADOW_AIM = 10;
/** Juramento, Contracanto and Silencio last two of the squad's turns. */
const SONG_TURNS = 2;
const BOLT_DAMAGE = 3;
const STORM_DAMAGE = 4;
/** Tormenta de Velmar strikes this many of the enemies in sight, the weakest first. */
export const STORM_TARGETS = 3;
const LAY_ON_HANDS = 3;
/** Último verso: health of whoever stands again, or healing for those around. */
const LAST_VERSE_HP = 3;
const LAST_VERSE_HEAL = 4;

/** Reinforcements drop this far (in tiles) from the nearest soldier. */
const DROP_MIN = 7;
const DROP_MAX = 13;
/** The horde comes in this close to the map's edge, and at least this far from every soldier (fallbacks after). */
const EDGE_BAND = 2;
const EDGE_GAP = [12, 8, 0] as const;
/** Nor this close to the evac zone. */
const EDGE_EVAC_GAP = 3;

/** The Chillón's scream (docs/FASE2.md): out of 100, this many disorient and the rest panic. It never takes control. */
const SCREAM_DISORIENT = 60;

/** What an explosion leaves of each kind of tile (null: unaffected). */
const BLAST_RESULT: Partial<Record<TileKind, TileKind>> = {
  low: 'floor',
  high: 'low',
  door: 'doorOpen',
  window: 'windowBroken',
};

/**
 * Where someone (or something) on storey place `p` lands when its slab gives
 * way (docs/FASE7.md §5): on the next slab down in that column, or the ground,
 * on `p`'s own tile if one can stand there, else the nearest free place to
 * stand at that height (two tiles away at most). Null if there is none.
 */
function landingBelow(s: GameState, p: Pos, taken: ReadonlySet<number>): Pos | null {
  let z = p.z - 1;
  while (z > 0 && !slabAt(s, p.x, p.y, z)) z--;
  for (let r = 0; r <= 2; r++) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== r || !inBounds(s, p.x + dx, p.y + dy)) continue;
        const q = { x: p.x + dx, y: p.y + dy, z };
        if (canStandOn(kindAt(s, q)) && !unitAt(s, q) && !taken.has(nodeOf(s, q))) return q;
      }
    }
  }
  return null;
}

/** Why pods come alive (podActivated), and who was involved. */
export interface Activation {
  cause: PodCause;
  /** The soldier they saw, or who attacked them. */
  unit?: string;
  /** The alien that saw the soldier, or that was attacked. */
  by?: string;
}

/** What gave the squad away: a concealmentBroken event without its tag. */
export type Exposure = Omit<Extract<GameEvent, { t: 'concealmentBroken' }>, 't'>;

const EXPOSURE: Record<PodCause, ConcealReason> = { sighted: 'spotted', patrol: 'patrol', attacked: 'attack', noise: 'explosive' };

export interface CommandContext {
  slot: Slot;
  /** Seats that count for the end of turn; dropped players stop counting after a grace period. */
  connected: readonly Slot[];
  rng: Rng;
}

export type CommandResult = { ok: true; events: GameEvent[] } | { ok: false; error: string };

/**
 * Mutable simulation over a GameState. Every change goes through `emit`, which
 * applies the event and records it for broadcasting.
 */
export class Sim {
  readonly events: GameEvent[] = [];
  /**
   * Team-wide limits for the alien turn (XCOM 2's global cooldowns): one
   * explosive and one Mindspin per turn, so a pod can't stack area attacks.
   */
  alienBudget = { explosives: 1, psi: 1 };

  constructor(
    readonly s: GameState,
    readonly rng: Rng,
    readonly connected: readonly Slot[],
  ) {}

  emit(e: GameEvent): void {
    applyEvent(this.s, e);
    this.events.push(e);
  }

  get over(): boolean {
    return this.s.outcome !== null;
  }

  // ------------------------------------------------------------- abilities

  /** Pays for an ability: actions (all of them if it ends the turn), cooldown and charge. */
  pay(u: Unit, ability: AbilityId): void {
    const def = ABILITIES[ability];
    const need = apNeeded(u, ability);
    const ap = ability === 'runAndGun' ? -1 : endsTurn(u, ability) ? Math.max(u.ap, need) : need;
    this.emit({ t: 'abilityUsed', unit: u.id, ability, ap, cooldown: def.cooldown ?? 0, charge: def.charge ?? null });
  }

  /**
   * Runs an ability that has already been validated. Player commands and the
   * alien AI both come through here, so every rule applies to both sides.
   */
  perform(u: Unit, ability: AbilityId, args: { target?: Unit; tile?: Pos; melee?: MeleeOption } = {}): void {
    const { target, tile, melee } = args;
    this.pay(u, ability);
    const loaded = () => loadedFor(u, 'shoot');
    switch (ability) {
      case 'shoot':
      case 'pistol':
      case 'aimedShot':
      case 'rupture':
      case 'lightningHands':
        this.attack(u, target!, ability);
        break;
      case 'rapidFire':
        this.attack(u, target!, ability);
        if (u.alive && target!.alive && !this.over && loaded()) this.attack(u, target!, ability);
        break;
      case 'chainShot': {
        const first = this.attack(u, target!, ability);
        if (first && first !== 'miss' && u.alive && target!.alive && !this.over && loaded()) this.attack(u, target!, ability);
        break;
      }
      case 'faceoff':
        for (const t of rangedTargets(this.s, u, 'faceoff')) {
          if (!u.alive || this.over) break;
          if (t.alive) this.attack(u, t, 'faceoff');
        }
        break;
      case 'slash':
      case 'stunLance':
      case 'claw':
        this.meleeAttack(u, melee!, ability);
        break;
      case 'elbowRoom':
        // Hacerse sitio (docs/FASE9.md): a Slash at every enemy alongside, without a step.
        for (const t of besideEnemies(this.s, u)) {
          if (!u.alive || this.over) break;
          if (t.alive) this.meleeAttack(u, { target: t, tile: u.pos, path: [] }, 'slash');
        }
        break;
      case 'suppression':
        this.emit({ t: 'suppressed', unit: u.id, target: target!.id });
        if (u.team === 'xcom') this.breakConcealment({ reason: 'attack', unit: u.id, by: target!.id });
        this.activatePodOf(target!, u);
        break;
      case 'medkit':
        this.emit({ t: 'healed', unit: u.id, target: target!.id, amount: Math.min(MEDKIT_HEAL + u.mods.heal + (bridged(u, target!) ? 2 : 0), target!.maxHp - target!.hp) });
        break;
      case 'aid':
        this.emit({ t: 'aided', unit: u.id, target: target!.id, amount: AID_DEFENSE + u.mods.aid + (bridged(u, target!) ? 10 : 0) });
        if (hasPerk(u, 'threatAssessment') && !target!.overwatch && loadedFor(target!, 'shoot')) {
          this.emit({ t: 'overwatch', unit: target!.id, killZone: false });
        }
        break;
      case 'revival':
        this.emit({ t: 'cleansed', unit: u.id, target: target!.id });
        break;
      case 'patchUp':
        this.emit({ t: 'patched', unit: u.id, target: target!.id, amount: Math.min(PATCH_UP, 100 - conditionOf(target!)) });
        break;
      case 'restoration':
        this.restoration(u);
        break;
      case 'combatProtocol':
        this.zap(u, target!);
        break;
      case 'grenade':
      case 'launch':
      case 'missiles':
      case 'discharge':
        this.explode(u, tile!, ability);
        break;
      case 'smoke':
        this.emit({ t: 'explosive', unit: u.id, ability, target: tile! });
        this.emit({ t: 'smokeDeployed', unit: u.id, pos: tile!, radius: ABILITIES.smoke.radius ?? 2, turns: SMOKE_TURNS });
        break;
      case 'flashbang':
        this.flashbang(u, tile!);
        break;
      case 'firstAid':
        // Medicina 2 and 4, and Buen pulso, heal more (docs/FASE9.md).
        this.emit({ t: 'healed', unit: u.id, target: target!.id, amount: Math.min(MEDKIT_HEAL + u.mods.heal, target!.maxHp - target!.hp) });
        break;
      case 'overwatch':
      case 'killZone':
        this.emit({ t: 'overwatch', unit: u.id, killZone: ability === 'killZone' });
        break;
      case 'hunker':
        this.emit({ t: 'hunker', unit: u.id });
        break;
      case 'reload':
        this.emit({ t: 'reload', unit: u.id });
        break;
      case 'hack':
        this.hack(u);
        break;
      case 'search':
        this.search(u, tile!);
        break;
      case 'searchRoom':
        this.searchRoom(u);
        break;
      case 'openDoor':
      case 'closeDoor':
        this.emit({ t: 'tileChanged', pos: { ...tile! }, kind: ability === 'openDoor' ? 'doorOpen' : 'door' });
        break;
      case 'runAndGun':
        break;
      case 'mindspin':
        this.psionic(u, target!);
        break;
      case 'reanimate':
        this.reanimate(u, target!);
        break;
      case 'burst':
        this.burst(u);
        break;
      // Velmar's Canto.
      case 'aegis':
        this.bless(u, ability, this.alliesWithin(u, 1.5), AEGIS_DEFENSE, 0);
        break;
      case 'hymn':
        this.bless(u, ability, this.alliesWithin(u, ABILITIES.hymn.range ?? 0), HYMN_BONUS, HYMN_BONUS);
        break;
      case 'echoStep':
      case 'shadowStep':
        this.teleport(u, tile!, ability);
        break;
      case 'layOnHands':
        this.emit({ t: 'sung', unit: u.id, ability, targets: [target!.id] });
        if (target!.hp < target!.maxHp) this.emit({ t: 'healed', unit: u.id, target: target!.id, amount: Math.min(LAY_ON_HANDS + u.mods.heal, target!.maxHp - target!.hp) });
        if (target!.panicked || target!.disoriented > 0) this.emit({ t: 'cleansed', unit: u.id, target: target!.id });
        break;
      case 'velmarOath':
        this.emit({ t: 'sung', unit: u.id, ability, targets: [u.id] });
        this.emit({ t: 'oath', unit: u.id, turns: SONG_TURNS });
        break;
      case 'bolt':
        this.emit({ t: 'sung', unit: u.id, ability, targets: [target!.id] });
        this.damage(target!, BOLT_DAMAGE + u.mods.songDamage + this.duet(u), 0, u);
        if (target!.team !== u.team) this.activatePodOf(target!, u);
        // At night the flare lights whoever it struck, and round them.
        this.light(target!.pos, NIGHT.flare);
        this.checkOutcome();
        break;
      case 'velmarStorm': {
        const struck = songTargets(this.s, u, 'velmarStorm').sort((a, b) => a.hp - b.hp).slice(0, STORM_TARGETS);
        this.emit({ t: 'sung', unit: u.id, ability, targets: struck.map((t) => t.id) });
        for (const t of struck) {
          this.damage(t, STORM_DAMAGE + u.mods.songDamage + this.duet(u), 0, u);
          this.activatePodOf(t, u);
          this.light(t.pos, NIGHT.fire);
        }
        this.checkOutcome();
        break;
      }
      case 'silence':
        this.silence(u, tile!);
        break;
      case 'fireball':
        this.explode(u, tile!, 'fireball');
        if (u.alive) this.light(tile!, NIGHT.fire);
        break;
      case 'huntersMark':
        this.emit({ t: 'sung', unit: u.id, ability, targets: [target!.id] });
        this.emit({ t: 'hunted', unit: target!.id, by: u.id });
        break;
      case 'spectralArrow':
        this.attack(u, target!, 'spectralArrow');
        break;
      case 'lightRain':
        this.emit({ t: 'sung', unit: u.id, ability, targets: songTargets(this.s, u, 'lightRain').map((t) => t.id) });
        for (const t of songTargets(this.s, u, 'lightRain')) {
          if (!u.alive || this.over) break;
          if (t.alive) this.attack(u, t, 'lightRain');
        }
        break;
      case 'counterSong':
        this.emit({ t: 'sung', unit: u.id, ability, targets: [target!.id] });
        if (target!.controlledBy) this.emit({ t: 'released', unit: target!.id });
        if (hasBadStatus(target!)) this.emit({ t: 'cleansed', unit: u.id, target: target!.id });
        this.emit({ t: 'warded', unit: target!.id, turns: SONG_TURNS });
        break;
      case 'lastVerse':
        this.lastVerse(u);
        break;
    }
    // Then it is heard (docs/FASE2.md): where a tile ability lands, or where the user stands.
    // Whoever fell doing it made no more noise than what already sounded (the Hinchado sounds as it bursts).
    if (u.alive) this.makeNoise(tile ?? u.pos, noiseOf(this.s, u, ability), u);
  }

  // ------------------------------------------------------------- the Canto

  /** A Bengala or a fire lights what it sees round `tile` for `turns` of the squad's turns: at night only, by day nothing changes. */
  private light(tile: Pos, { radius, turns, height }: { radius: number; turns: number; height: number }): void {
    if (!this.s.mission.night || this.over) return;
    this.emit({ t: 'lightOn', light: { id: `f${this.s.seq}`, tile: { ...tile }, radius, turns, tiles: lightTiles(this.s, tile, radius, height) } });
  }

  /** «A dos voces»: one more for a song with a partner's singer near. */
  duet(u: Unit): number {
    return duet(this.s, u);
  }

  /** The singer and its allies within `range` (on the battlefield, able to act). */
  private alliesWithin(u: Unit, range: number): Unit[] {
    return this.s.units.filter((a) => a.alive && a.team === u.team && !a.captive && !a.objective && distance(a.pos, u.pos) <= range && sameSpace(this.s, a.pos, u.pos));
  }

  /** Égida and Himno: defence (and aim) until the allies' next turn. */
  private bless(u: Unit, ability: AbilityId, allies: Unit[], defense: number, aim: number): void {
    this.emit({ t: 'sung', unit: u.id, ability, targets: allies.map((a) => a.id) });
    for (const a of allies) {
      if (defense > a.aided) this.emit({ t: 'aided', unit: u.id, target: a.id, amount: defense });
      if (aim) this.emit({ t: 'blessed', unit: a.id, aim });
    }
  }

  /** Salto de eco and Paso de sombra: gone from here, there at once. No overwatch sees the jump. */
  private teleport(u: Unit, to: Pos, ability: AbilityId): void {
    const from = { ...u.pos };
    this.emit({ t: 'sung', unit: u.id, ability, targets: [u.id], tile: to });
    this.emit({ t: 'teleported', unit: u.id, from, to: { ...to } });
    if (ability === 'shadowStep' && heightAt(this.s, to) > heightAt(this.s, from)) this.emit({ t: 'blessed', unit: u.id, aim: SHADOW_AIM });
    const noticing = u.team === 'xcom' ? this.podsNoticing(to) : [];
    if (noticing.length) this.activatePods(noticing, this.sighting(noticing, u, to));
  }

  /** Silencio: the Cantores in the zone can't sing for two turns, and every enemy there is disoriented. */
  private silence(u: Unit, center: Pos): void {
    const radius = (ABILITIES.silence.radius ?? 2) + this.duet(u);
    const hit = this.s.units.filter(
      (v) => v.alive && v.team !== u.team && !v.objective && !TEMPLATES[v.template].robotic && distance(v.pos, center) <= radius && sameSpace(this.s, v.pos, center),
    );
    this.emit({ t: 'sung', unit: u.id, ability: 'silence', targets: hit.map((v) => v.id), tile: center });
    this.emit({ t: 'silenced', pos: { ...center }, radius, turns: SONG_TURNS });
    for (const v of hit) this.emit({ t: 'disoriented', unit: v.id });
    this.activatePods([...new Set(hit.map((v) => v.podId).filter((p): p is string => !!p))], { cause: 'attacked', unit: u.id, by: hit[0]?.id });
  }

  /** Último verso: whoever fell this turn stands again; if nobody did, everyone near is healed. */
  private lastVerse(u: Unit): void {
    const fallen = this.s.units.find(
      (a) => !a.alive && !a.evacuated && !a.reanimated && a.fellTurn === this.s.turn && TEMPLATES[a.template].team === u.team && a.template !== 'vip',
    );
    if (fallen) {
      this.emit({ t: 'sung', unit: u.id, ability: 'lastVerse', targets: [fallen.id] });
      this.emit({ t: 'revived', unit: fallen.id, hp: LAST_VERSE_HP });
      return;
    }
    const near = this.alliesWithin(u, ABILITIES.lastVerse.range ?? 0).filter((a) => a.hp < a.maxHp);
    this.emit({ t: 'sung', unit: u.id, ability: 'lastVerse', targets: near.map((a) => a.id) });
    for (const a of near) this.emit({ t: 'healed', unit: u.id, target: a.id, amount: Math.min(LAST_VERSE_HEAL + u.mods.heal, a.maxHp - a.hp) });
  }

  /** Juramento de Velmar: some ally within 4 tiles (or the unit itself) keeps the oath. */
  private underOath(u: Unit): boolean {
    return this.s.units.some((a) => a.alive && a.team === u.team && (a.oath ?? 0) > 0 && distance(a.pos, u.pos) <= (ABILITIES.velmarOath.range ?? 0) && sameSpace(this.s, a.pos, u.pos));
  }

  // ---------------------------------------------------------------- combat

  /**
   * One shot (or blade strike): roll, damage and its side effects. Covering
   * Fire overwatchers react before the attack goes off.
   */
  attack(attacker: Unit, target: Unit, ability: AttackAbility, reaction = false): ShotResult | null {
    if (!reaction) this.coveringFire(attacker);
    if (!attacker.alive || !target.alive || this.over) return null;
    const preview = previewShot(this.s, attacker, target, { ability, reaction });
    if (!preview) return null;
    const { result, roll } = resolveShot(preview, this.rng);
    this.emit({
      t: 'shot',
      unit: attacker.id,
      target: target.id,
      ability,
      from: preview.origin,
      result,
      chance: preview.hit,
      roll,
      reaction,
    });
    const weapon = attackWeapon(attacker, ability);
    const primary = (ABILITIES[ability].weapon ?? 'primary') === 'primary' && !weapon.melee;
    if (primary && hasPerk(attacker, 'holoTargeting') && target.alive) this.emit({ t: 'marked', unit: target.id, amount: HOLO_AIM + attacker.mods.holoAim });
    if (result !== 'miss') {
      const dm = shotDamageMods(attacker, ability, preview.flanked, preview.distance);
      const before = target.hp;
      this.damage(target, rollDamage(weapon, result, this.rng, dm.bonus, dm.mult, dm.critBonus), dm.shred, attacker, dm.pierce);
      // A zombie's bite that draws blood may infect.
      if (weapon.melee && TEMPLATES[attacker.template].undead && target.hp < before) this.infect(target, attacker, INFECTION.bite[attacker.template] ?? 0);
      if (ability === 'rupture' && target.alive) this.emit({ t: 'ruptured', unit: target.id, amount: RUPTURE_DAMAGE });
      if (!target.alive && !reaction) this.afterKill(attacker, target, ability, preview.origin);
    }
    if (attacker.team === 'xcom' && !attacker.controlledBy) this.breakConcealment({ reason: 'attack', unit: attacker.id, by: target.id });
    if (target.team === 'alien') this.activatePodOf(target, attacker);
    this.checkOutcome();
    // Shots on someone else's move (overwatch, suppression, covering fire) are heard on their own; the rest with their ability.
    if (reaction && attacker.alive) this.makeNoise(preview.origin, weapon.noise, attacker);
    return result;
  }

  /** Implacable and Death From Above give an action back after a kill, once per turn. */
  private afterKill(killer: Unit, victim: Unit, ability: AttackAbility, from: Pos): void {
    if (killer.refunded || !killer.alive) return;
    const melee = attackWeapon(killer, ability).melee;
    if (melee && hasPerk(killer, 'implacable')) this.emit({ t: 'refund', unit: killer.id, reason: 'implacable' });
    else if (!melee && hasPerk(killer, 'deathFromAbove') && heightAt(this.s, from) > heightAt(this.s, victim.pos)) {
      this.emit({ t: 'refund', unit: killer.id, reason: 'deathFromAbove' });
    }
  }

  /** Covering Fire: overwatchers with the perk shoot at anyone who attacks. */
  private coveringFire(attacker: Unit): void {
    const watchers = this.s.units.filter(
      (o) =>
        o.alive &&
        o.team !== attacker.team &&
        o.overwatch &&
        hasPerk(o, 'coveringFire') &&
        !(o.killZone && o.reactedTo.includes(attacker.id)) &&
        loadedFor(o, 'shoot') &&
        canSee(this.s, o, attacker),
    );
    for (const o of watchers) {
      if (!attacker.alive || this.over) break;
      this.attack(o, attacker, 'shoot', true);
    }
  }

  /**
   * Armour absorbs up to its value per hit (unless the attack pierces it), then
   * shred wears it down. Rupture adds to every hit.
   */
  damage(u: Unit, raw: number, shred: number, source?: Unit, pierce = 0): void {
    if (!u.alive) return;
    const { total, mitigated } = damageTaken(u, raw, pierce);
    this.emit({ t: 'damaged', unit: u.id, amount: total - mitigated, mitigated, shred: Math.min(shred, u.armor) });
    if (u.hp > 0) return;
    if (this.s.mission.item?.carrier === u.id) this.emit({ t: 'itemDropped', pos: { ...u.pos } });
    this.emit({ t: 'died', unit: u.id, by: source?.id });
    // A sectoid's death frees the minds it held.
    for (const v of this.s.units) if (v.alive && v.controlledBy === u.id) this.emit({ t: 'released', unit: v.id });
    if (TEMPLATES[u.template].abilities.includes('burst')) this.burst(u);
  }

  /**
   * The Hinchado bursts where it stands (docs/FASE2.md), when it dies or of its
   * own will beside a soldier: it is gone, and everyone beside it takes the blast.
   */
  burst(u: Unit): void {
    if (u.alive) this.emit({ t: 'died', unit: u.id });
    this.explode(u, u.pos, 'burst');
    this.makeNoise(u.pos, WEAPONS.bloaterBurst.noise, u);
  }

  meleeAttack(u: Unit, option: MeleeOption, ability: AttackAbility): void {
    if (option.path.length) this.moveAlong(u, option.path, 0);
    const target = getUnit(this.s, option.target.id);
    if (!u.alive || !target?.alive || this.over || !inMeleeReach(this.s, u.pos, target.pos)) return;
    const result = this.attack(u, target, ability);
    if (ability === 'stunLance' && result && result !== 'miss' && target.alive) this.emit({ t: 'stunned', unit: target.id });
    // Hoja cantada: the singing blade leaves the enemy reeling.
    if (ability === 'slash' && u.mods.slashDisorient && result && result !== 'miss' && target.alive && !TEMPLATES[target.template].robotic) this.emit({ t: 'disoriented', unit: target.id });
  }

  explode(source: Unit, center: Pos, ability: BlastAbility): void {
    // The Hinchado's burst is its death: nobody gets to fire first, and it goes off without it.
    const bursting = ability === 'burst';
    if (!bursting) this.coveringFire(source);
    if ((!source.alive && !bursting) || this.over) return;
    const weapon = WEAPONS[ABILITIES[ability].explosive!];
    this.emit({ t: 'explosive', unit: source.id, ability, target: center });
    const tiles = blastTiles(this.s, ability, center, source);
    const grenade = ability === 'grenade' || ability === 'launch';
    // Controlled detonation: the grenadier's own squad is left out of the blast.
    const spared = (u: Unit) => grenade && hasPerk(source, 'safeBlast') && u.team === source.team;
    const victims = tiles.map((p) => unitAt(this.s, p)).filter((u): u is Unit => !!u && !spared(u));
    const { bonus, shred } = blastMods(this.s, source, ability);
    for (const v of victims) {
      const before = v.hp;
      this.damage(v, rollDamage(weapon, 'hit', this.rng, bonus), shred, source);
      if (bursting && v.hp < before) this.infect(v, source, INFECTION.burst);
    }
    // The drone's discharge is pure electricity and the Hinchado's burst only gas: they leave the scenery alone.
    if (ability !== 'discharge' && !bursting) {
      for (const p of tiles) {
        const kind = kindAt(this.s, p);
        const next = kind && BLAST_RESULT[kind];
        if (next) this.emit({ t: 'tileChanged', pos: p, kind: next });
      }
      // The frag grenades breach the walls they reach (docs/FASE7.md §4): rubble, low cover, on that floor only.
      for (const p of breachedWalls(this.s, ability, center, source)) this.emit({ t: 'tileChanged', pos: p, kind: 'low', decor: 'breach' });
      // Upstairs, the slab it went off on gives way (docs/FASE7.md §5).
      if (breaksFloor(this.s, ability, center)) this.breakFloor(center);
    }
    if (source.team === 'xcom') this.breakConcealment({ reason: 'explosive', unit: source.id });
    // Pods caught in the blast were attacked; the rest only hear it (the noise, after the action).
    const hit = new Set<string>();
    for (const v of victims) if (v.podId) hit.add(v.podId);
    this.activatePods([...hit], { cause: 'attacked', unit: source.team === 'xcom' ? source.id : undefined });
    this.checkOutcome();
  }

  /**
   * A grenade breaks the slab of storey place `p` (docs/FASE7.md §5): whoever
   * stands on it falls to the floor below (the nearest free place to stand
   * there), with FALL_DAMAGE a storey through the armour, and the objective
   * lying on it drops too. If someone would have nowhere to land, the floor
   * holds.
   */
  private breakFloor(p: Pos): void {
    const taken = new Set<number>();
    const land = () => {
      const to = landingBelow(this.s, p, taken);
      if (to) taken.add(nodeOf(this.s, to));
      return to;
    };
    const falling = this.s.units.filter((u) => u.alive && samePos(u.pos, p)).map((u) => ({ u, to: land() }));
    const item = this.s.mission.item;
    const lying = item && !item.carrier && item.pos && samePos(toPos(item.pos), p) ? land() : null;
    if (falling.some((f) => !f.to) || (item && !item.carrier && item.pos && samePos(toPos(item.pos), p) && !lying)) return;
    this.emit({ t: 'floorBroken', pos: { ...p } });
    if (lying) this.emit({ t: 'itemDropped', pos: lying });
    for (const { u, to } of falling) {
      this.emit({ t: 'fell', unit: u.id, from: { ...p }, to: to! });
      this.damage(u, FALL_DAMAGE * Math.max(1, p.z - heightAt(this.s, to!)), 0, undefined, u.armor);
    }
  }

  /** Disorients every enemy in the area; no damage, the scenery is untouched. */
  private flashbang(u: Unit, center: Pos): void {
    this.emit({ t: 'explosive', unit: u.id, ability: 'flashbang', target: center });
    const hit = blastTiles(this.s, 'flashbang', center, u)
      .map((p) => unitAt(this.s, p))
      .filter((v): v is Unit => !!v && v.team !== u.team && !TEMPLATES[v.template].robotic && !v.objective);
    for (const v of hit) this.emit({ t: 'disoriented', unit: v.id });
    if (u.team === 'xcom') this.breakConcealment({ reason: 'explosive', unit: u.id });
    this.activatePods([...new Set(hit.map((v) => v.podId).filter((p): p is string => !!p))], { cause: 'attacked', unit: u.id, by: hit[0]?.id });
  }

  /** Combat Protocol: never misses, ignores armour, hurts machines more. */
  private zap(u: Unit, target: Unit): void {
    this.emit({ t: 'zapped', unit: u.id, target: target.id });
    const dmg = WEAPONS.droneZap.damage + (TEMPLATES[target.template].robotic ? 2 : 0) + u.mods.protocolDamage;
    this.damage(target, dmg, 0, u, Infinity);
    if (u.team === 'xcom') this.breakConcealment({ reason: 'attack', unit: u.id, by: target.id });
    if (target.team === 'alien') this.activatePodOf(target, u);
    this.checkOutcome();
  }

  private restoration(u: Unit): void {
    const range = ABILITIES.restoration.range ?? 0;
    for (const a of this.s.units) {
      if (!a.alive || a.team !== u.team || a.captive || distance(a.pos, u.pos) > range || !sameSpace(this.s, a.pos, u.pos)) continue;
      const heal = Math.min(RESTORATION_HEAL, a.maxHp - a.hp);
      if (heal > 0) this.emit({ t: 'healed', unit: u.id, target: a.id, amount: heal });
      if (hasBadStatus(a)) this.emit({ t: 'cleansed', unit: u.id, target: a.id });
    }
  }

  /** Mindspin: will resists; a hit disorients, panics or (rarely) takes control. */
  private psionic(caster: Unit, target: Unit): void {
    this.coveringFire(caster);
    if (!caster.alive || !target.alive || this.over) return;
    const chance = psiChance(target, this.s);
    const success = this.rng.int(100) < chance;
    let effect: PsiEffect | null = null;
    if (success) {
      // The scream confuses or frightens; it never takes control (docs/FASE2.md).
      effect = this.rng.int(100) < SCREAM_DISORIENT ? 'disoriented' : 'panicked';
      // Temple 7 keeps panic out (Temple 10 is out of its reach: psiChance), and so does Juramento de Velmar.
      if (effect === 'panicked' && (target.mods.psiGuard >= 1 || this.underOath(target))) effect = 'disoriented';
    }
    this.emit({ t: 'psi', unit: caster.id, target: target.id, success, effect, chance });
    this.checkOutcome();
  }

  private reanimate(caster: Unit, corpse: Unit): void {
    const zombie = makeUnit(this.zombieId(), 'zombie', risenName(corpse), null, corpse.pos, caster.podId);
    this.emit({ t: 'reanimated', unit: caster.id, corpse: corpse.id, zombie });
  }

  /** A fresh id for a zombie that rises (`z` and a number no unit has used). */
  private zombieId(): string {
    return `z${this.s.units.filter((x) => x.template === 'zombie').length + 1}`;
  }

  /**
   * A bite (or a Hinchado's burst) that drew blood infects with `chance` out
   * of 100, a Chaleco antibalas takes INFECTION.vest off (docs/FASE3.md). Only
   * the squad's flesh and blood; once infected, it stays so.
   */
  private infect(u: Unit, by: Unit, chance: number): void {
    const t = TEMPLATES[u.template];
    if (u.infected || chance <= 0 || t.team !== 'xcom' || t.robotic || t.undead || u.objective) return;
    const vest = u.gear.includes('nanoVest') ? INFECTION.vest : 0;
    if (this.rng.int(100) < chance - vest) this.emit({ t: 'infected', unit: u.id, by: by.id });
  }

  /**
   * The infected fallen rise as the zombies' turn begins (docs/FASE3.md), as
   * the Chillón's corpses do but by themselves: each a group of its own, alert.
   * One whose tile is taken rises beside it, or waits for a free one.
   */
  private riseInfected(): void {
    for (const corpse of this.s.units.filter((x) => !x.alive && !x.evacuated && x.infected && !x.reanimated)) {
      if (this.over) return;
      const at = [corpse.pos, ...DIRS8.map((d) => ({ x: corpse.pos.x + d.x, y: corpse.pos.y + d.y, z: corpse.pos.z }))].find(
        (p) => inBounds(this.s, p.x, p.y) && canStandOn(kindAt(this.s, p)) && heightAt(this.s, p) === heightAt(this.s, corpse.pos) && !unitAt(this.s, p),
      );
      if (!at) continue;
      const zombie = makeUnit(this.zombieId(), 'zombie', risenName(corpse), null, at, null);
      const pod: Pod = { id: `p${zombie.id}`, active: true, unitIds: [], patrol: [], patrolIndex: 0 };
      this.emit({ t: 'reanimated', corpse: corpse.id, zombie: { ...zombie, podId: pod.id }, pod });
    }
  }

  /** Searches the furniture on `tile` (docs/FASE2.md): what turns up goes into the backpack. */
  /** Opens the loot box on `tile` (docs/FASE8.md §B): always what it is marked for. */
  private search(u: Unit, tile: Pos): void {
    const box = searchSpot(this.s, tile);
    const loot = box ? rollBox(box, this.rng, u.mags !== undefined) : null;
    this.emit({ t: 'searched', unit: u.id, tile: { ...tile }, loot });
  }

  /** Searches the room `u` stands in, once for the squad (docs/FASE8.md §B). */
  private searchRoom(u: Unit): void {
    const room = roomAt(this.s, u.pos);
    if (!room) return;
    const loot = rollRoom(room, this.s.mission.difficulty ?? 2, this.rng, !!this.s.mission.night, u.mags !== undefined);
    this.emit({ t: 'roomSearched', unit: u.id, room: room.id, loot });
  }

  private hack(u: Unit): void {
    const chance = hackChance(this.s, u);
    const success = this.rng.int(100) < chance;
    this.emit({ t: 'hacked', unit: u.id, success, chance, remote: !adjacent(u.pos, this.s.mission.terminal!) });
    if (!success) this.raiseAlarm(u);
    else this.checkOutcome();
  }

  // ------------------------------------------------------------- movement

  /**
   * Walks the path tile by tile. Enemy overwatch, pod detection and picking up
   * the objective interrupt the walk, so a move may be emitted as several
   * `moved` segments.
   */
  moveAlong(u: Unit, path: Pos[], ap: number): void {
    let segment: Pos[] = [];
    let pendingAp = ap;
    const flush = () => {
      if (!segment.length) return;
      this.emit({ t: 'moved', unit: u.id, path: segment, ap: pendingAp });
      pendingAp = 0;
      segment = [];
    };

    const patrolling = onPatrol(this.s, u);
    for (let i = 0; i < path.length; i++) {
      const step = path[i]!;
      const blocker = unitAt(this.s, step);
      if (blocker && blocker !== u && (blocker.team !== u.team || i === path.length - 1)) break;
      segment.push(step);

      // Doors swing open and windows shatter as the unit goes through.
      const kind = kindAt(this.s, step);
      if (kind === 'door' || kind === 'window') {
        flush();
        this.emit({ t: 'tileChanged', pos: step, kind: kind === 'door' ? 'doorOpen' : 'windowBroken' });
        this.makeNoise(step, kind === 'door' ? NOISE.door : NOISE.breach, u);
      }
      if (blocker) continue; // passing through an ally: nothing can interrupt here

      const item = this.s.mission.item;
      if (u.team === 'xcom' && item && !item.carrier && item.pos && samePos(item.pos, step)) {
        flush();
        this.emit({ t: 'itemPicked', unit: u.id });
      }
      if (u.team === 'xcom' && !u.captive) {
        // Beside the VIP, on its level (a floor between them keeps them apart).
        const vip = this.s.units.find((o) => o.alive && o.captive && adjacent(o.pos, step) && o.pos.z === step.z);
        if (vip) {
          flush();
          this.emit({ t: 'vipRescued', unit: vip.id, by: u.id, owner: u.owner });
        }
      }
      // A patrolling pod that walks into sight of the squad stops and takes cover.
      const spotted = patrolling ? this.spottedFrom(u, step) : undefined;
      if (spotted) {
        flush();
        this.activatePods([u.podId!], { cause: 'patrol', unit: spotted.id, by: u.id });
        return;
      }

      // Shadowstep: nobody gets a reaction shot at this unit.
      const shadow = hasPerk(u, 'shadowstep');
      const loaded = (o: Unit) => loadedFor(o, 'shoot');
      const reactors = shadow
        ? []
        : this.s.units.filter(
            (o) => o.alive && o.team !== u.team && o.overwatch && !(o.killZone && o.reactedTo.includes(u.id)) && loaded(o) && this.watches(o, step),
          );
      const suppressors = shadow ? [] : this.s.units.filter((o) => o.alive && o.team !== u.team && o.suppressing === u.id && loaded(o));
      const noticing = u.team === 'xcom' ? this.podsNoticing(step) : [];
      if (!reactors.length && !suppressors.length && !noticing.length) continue;

      flush();
      for (const r of reactors) {
        if (!u.alive || this.over) break;
        if (r.alive && r.overwatch) this.attack(r, u, 'shoot', true);
      }
      for (const r of suppressors) {
        if (!u.alive || this.over) break;
        if (!r.alive || r.suppressing !== u.id) continue;
        this.attack(r, u, 'shoot', true);
        this.emit({ t: 'suppressionEnded', unit: r.id });
      }
      if (noticing.length && !this.over) this.activatePods(noticing, this.sighting(noticing, u, step));
      if (!u.alive || this.over) return;
    }
    flush();
  }

  /** Whether an overwatcher covers `step`: own sight, or the squad's with Long Watch. */
  private watches(o: Unit, step: Pos): boolean {
    if (sightOrigin(this.s, o.pos, step, sightTo(this.s, o, step))) return true;
    if (!hasPerk(o, 'longWatch') || !hasSquadsight(o)) return false;
    const spotted = this.s.units.some((v) => isViewer(v, o.team) && sightOrigin(this.s, v.pos, step, sightTo(this.s, v, step)));
    return spotted && sightOrigin(this.s, o.pos, step, Infinity) !== null;
  }

  // ------------------------------------------------------- pods & stealth

  /** Whether a patrolling alien standing on `p` spots any soldier (detection range while the squad is concealed). */
  podSpotsSquad(u: Unit, p: Pos): boolean {
    return this.spottedFrom(u, p) !== undefined;
  }

  /** How far `u` notices a soldier on `p`: its detection while the squad is hidden, its sight after (both shorter at night, unless lit). */
  private noticeRange(u: Unit, p: Pos): number {
    return this.s.concealed ? detectionTo(this.s, u, p) : sightTo(this.s, u, p);
  }

  /** The first soldier an alien standing on `p` would spot. */
  spottedFrom(u: Unit, p: Pos): Unit | undefined {
    return combatants(this.s, 'xcom').find((soldier) => sightOrigin(this.s, p, soldier.pos, this.noticeRange(u, soldier.pos)) !== null);
  }

  /** The member of `pod` that would notice an XCOM soldier standing on `p`, if any. */
  private noticer(pod: Pod, p: Pos): Unit | undefined {
    for (const id of pod.unitIds) {
      const m = getUnit(this.s, id);
      if (!m?.alive) continue;
      if (sightOrigin(this.s, m.pos, p, this.noticeRange(m, p)) !== null) return m;
    }
    return undefined;
  }

  /** Inactive pods that would notice an XCOM soldier standing on `p`. */
  podsNoticing(p: Pos): string[] {
    return this.s.pods.filter((pod) => !pod.active && this.noticer(pod, p)).map((pod) => pod.id);
  }

  /** `soldier`, standing on `p`, has been seen by these pods: by whom, for the players. */
  private sighting(podIds: string[], soldier: Unit, p: Pos): Activation {
    const pod = this.s.pods.find((x) => podIds.includes(x.id));
    return { cause: 'sighted', unit: soldier.id, by: pod && this.noticer(pod, p)?.id };
  }

  breakConcealment(why: Exposure): void {
    if (this.s.concealed) this.emit({ t: 'concealmentBroken', ...why });
  }

  // ----------------------------------------------------------------- noise

  /**
   * A noise of `radius` tiles at `tile` (docs/FASE2.md): every enemy within it
   * that isn't already fighting a soldier in sight remembers the spot and goes
   * to look, inactive pods too, without waking up. It never breaks concealment.
   * The one who made it, and its pod, know what it was.
   */
  makeNoise(tile: Pos, radius: number, by?: Unit): void {
    if (radius <= 0 || this.over) return;
    const heard = this.s.units
      .filter(
        (o) =>
          o.alive &&
          o.team === 'alien' &&
          !o.objective &&
          !o.controlledBy &&
          o.id !== by?.id &&
          !(by?.podId && o.podId === by.podId) &&
          // A storey between them is one more tile to carry (decision 3 in FASE6-PLAN.md).
          distance(o.pos, tile) + storeysApart(this.s, o.pos, tile) <= radius &&
          !this.engaged(o),
      )
      .map((o) => o.id);
    this.emit({ t: 'noise', tile: { ...tile }, radius, by: by?.id, heard });
  }

  /** An alert enemy with a soldier in sight: a noise elsewhere doesn't turn its head. */
  private engaged(o: Unit): boolean {
    if (!this.s.pods.some((p) => p.id === o.podId && p.active)) return false;
    return combatants(this.s, 'xcom').some((e) => canSee(this.s, o, e));
  }

  /** `u` was attacked (by `attacker`): its pod comes alive. */
  activatePodOf(u: Unit, attacker?: Unit): void {
    if (u.podId) this.activatePods([u.podId], { cause: 'attacked', unit: attacker?.team === 'xcom' ? attacker.id : undefined, by: u.id });
  }

  /**
   * Activates pods and lets every member "scamper" to cover, XCOM 2 style. If
   * that gives the squad away, the reason goes out first, then the contact.
   */
  activatePods(podIds: string[], why: Activation | ((pod: Pod) => Activation)): void {
    const pods = this.s.pods.filter((p) => podIds.includes(p.id) && !p.active);
    if (!pods.length) return;
    const whyOf = (pod: Pod) => (typeof why === 'function' ? why(pod) : why);
    const first = whyOf(pods[0]!);
    const spotter = first.by ? getUnit(this.s, first.by) : undefined;
    const spotted = first.unit ? getUnit(this.s, first.unit) : undefined;
    const seen = first.cause === 'sighted' || first.cause === 'patrol';
    const range = spotter && spotted ? detectionTo(this.s, spotter, spotted.pos) : spotter && TEMPLATES[spotter.template].detection;
    this.breakConcealment({ reason: EXPOSURE[first.cause], unit: first.unit, by: first.by, ...(seen && spotter ? { range } : {}) });
    for (const pod of pods) this.emit({ t: 'podActivated', pod: pod.id, ...whyOf(pod) });
    for (const pod of pods) this.scamper(pod.unitIds);
  }

  /** Free move to the best nearby cover, without spending actions. The dead don't take cover. */
  scamper(unitIds: string[]): void {
    for (const id of unitIds) {
      const m = getUnit(this.s, id);
      if (!m?.alive || this.over || TEMPLATES[m.template].undead) continue;
      const dest = chooseScamperTile(this.s, m);
      if (!dest) continue;
      const path = buildPath(this.s, m, computeReach(this.s, m, costPerAp(m)), dest);
      if (path) this.moveAlong(m, path, 0);
    }
  }

  // -------------------------------------------------------- reinforcements

  /**
   * Picks open tiles for a drop: not too close to the squad, not too far, on
   * the ground. The horde (`edge`) comes in by the map's edges instead, well
   * away from the squad and the evac zone.
   */
  chooseDropZone(count: number, edge = false): Pos[] | null {
    const s = this.s;
    const soldiers = combatants(s, 'xcom');
    if (!soldiers.length) return null;
    const free = (x: number, y: number) =>
      inBounds(s, x, y) && canStandOn(tileAt(s, x, y)) && (s.elev[tileIndex(s, x, y)] ?? 0) === 0 && !unitAt(s, { x, y, z: 0 });
    const onEdge = (x: number, y: number) =>
      (x < EDGE_BAND || y < EDGE_BAND || x >= s.width - EDGE_BAND || y >= s.height - EDGE_BAND) &&
      !s.mission.evacZone.some((z) => distance(z, { x, y }) < EDGE_EVAC_GAP);
    const bands = edge ? EDGE_GAP.map((lo) => [lo, Infinity] as const) : ([[DROP_MIN, DROP_MAX], [4, 20], [2, 60]] as const);
    for (const [lo, hi] of bands) {
      const candidates: Pos[] = [];
      for (let y = 0; y < s.height; y++) {
        for (let x = 0; x < s.width; x++) {
          if (!free(x, y) || (edge && !onEdge(x, y))) continue;
          const d = Math.min(...soldiers.map((o) => distance(o.pos, { x, y })));
          if (d >= lo && d <= hi) candidates.push({ x, y, z: 0 });
        }
      }
      while (candidates.length) {
        const anchor = candidates.splice(this.rng.int(candidates.length), 1)[0]!;
        const zone = [anchor];
        for (let r = 1; r <= 2 && zone.length < count; r++) {
          for (let dy = -r; dy <= r && zone.length < count; dy++) {
            for (let dx = -r; dx <= r && zone.length < count; dx++) {
              const p = { x: anchor.x + dx, y: anchor.y + dy, z: 0 };
              // The horde stays by the edge, all of it.
              if (free(p.x, p.y) && (!edge || onEdge(p.x, p.y)) && !zone.some((z) => sameTile(z, p))) zone.push(p);
            }
          }
        }
        if (zone.length >= count) return zone;
      }
    }
    return null;
  }

  /**
   * Raises the flare for every reinforcement due at the end of alien turn
   * `turn`: only drops from missions saved before the playtest of 2026-10-13.
   * The horde comes in by an edge, with no flare.
   */
  announceReinforcements(turn: number): void {
    for (const r of this.s.mission.reinforcements) {
      if (r.landed || r.pos || r.horde || r.turn !== turn) continue;
      const zone = this.chooseDropZone(r.units.length);
      if (zone) this.emit({ t: 'reinforcementsIncoming', r: { ...r, pos: zone[0]! } });
    }
  }

  /**
   * The alarm went off (`hacker` failed the hack): it gives the squad away,
   * the terminal blares (NOISE.alarm), and a horde drawn by it comes in by an
   * edge at the end of this enemy turn (playtest 2026-10-13: nobody drops in
   * from nowhere).
   */
  raiseAlarm(hacker?: Unit): void {
    this.breakConcealment({ reason: 'alarm', unit: hacker?.id });
    const terminal = this.s.mission.terminal;
    if (terminal) this.makeNoise(terminal, NOISE.alarm, hacker);
    const id = `r${this.s.mission.reinforcements.length + 1}`;
    const r: Reinforcement = { id, turn: this.s.turn, pos: null, units: hordeWave(this.s.mission.difficulty ?? 2), landed: false, horde: true };
    this.emit({ t: 'reinforcementsIncoming', r });
  }

  /**
   * Brings in every arrival that is due. The horde comes in by the map's
   * edge, where it lands, on the prowl: an inactive pod heading for the
   * soldier nearest to it, which only wakes up (and gives the squad away) on
   * seeing someone. A drop with a flare (only in missions saved before the
   * playtest of 2026-10-13) lands where it was announced, already alert.
   */
  landReinforcements(turn: number): void {
    for (const r of this.s.mission.reinforcements) {
      if (r.landed || (!r.horde && !r.pos) || r.turn > turn || this.over) continue;
      const zone = r.horde ? this.chooseDropZone(r.units.length, true) : this.chooseDropZoneAround(r.pos!, r.units.length);
      // No edge free today: they come next turn.
      if (!zone?.length) continue;
      const at = zone[0]!;
      const pod: Pod = { id: `p${r.id}`, active: !r.horde, unitIds: [], patrol: [], patrolIndex: 0 };
      const near = r.horde ? [...combatants(this.s, 'xcom')].sort((a, b) => distance(a.pos, at) - distance(b.pos, at))[0] : undefined;
      const units = r.units.map((template, i) => {
        const id = `${r.id}u${i + 1}`;
        pod.unitIds.push(id);
        const u = makeUnit(id, template, TEMPLATES[template].name, null, zone[i] ?? at, pod.id);
        return near ? { ...u, heard: { ...near.pos } } : u;
      });
      this.emit({ t: 'spawned', units, pod, reinforcement: r.id });
      if (r.horde) continue;
      this.breakConcealment({ reason: 'reinforcements', by: units[0]?.id });
      this.scamper(pod.unitIds);
    }
  }

  /**
   * Saqueo (docs/FASE2.md): the horde's clock winds up with every turn and
   * every noise the squad makes (HORDE in content.ts). When a wave is due, it
   * is announced for the end of the next alien turn, by the map's edge; one a
   * turn at most, and none while the field is already full.
   */
  callHorde(turn: number): void {
    const m = this.s.mission;
    if (!m.horde?.length || combatants(this.s, 'alien').length >= HORDE.cap) return;
    // A few every turn, always (playtest 2026-10-15, #11); twice as many while a sabotage's alarm still sounds (#14).
    const alarm = m.kind === 'sabotage' && !m.objectiveDone ? 2 : 1;
    const few = HORDE.trickle[Math.max(1, Math.min(4, m.difficulty ?? 2))]! * alarm;
    if (few) {
      const r: Reinforcement = { id: `r${m.reinforcements.length + 1}`, turn: turn + 1, pos: null, units: Array.from({ length: few }, () => 'zombie' as const), landed: false, horde: true, trickle: true };
      this.emit({ t: 'reinforcementsIncoming', r });
    }
    // And a whole wave now and then, sooner the more noise the squad makes.
    const sent = this.s.mission.reinforcements.filter((r) => r.horde && !r.trickle).length;
    // At night the dead are already on their way: the clock runs a turn ahead.
    const due = Math.floor(((turn + (m.night ? NIGHT.hordeHead : 0)) * HORDE.perTurn + (m.noise ?? 0)) / HORDE.wave);
    if (due <= sent) return;
    // Heard coming: they're in by an edge at the end of the next enemy turn, and nobody knows where.
    const r: Reinforcement = { id: `r${this.s.mission.reinforcements.length + 1}`, turn: turn + 1, pos: null, units: [...m.horde], landed: false, horde: true };
    this.emit({ t: 'reinforcementsIncoming', r });
  }

  /** Free tiles around a landing point (the flare tile first). */
  private chooseDropZoneAround(center: Pos, count: number): Pos[] {
    const s = this.s;
    const zone: Pos[] = [];
    for (let r = 0; r <= 3 && zone.length < count; r++) {
      for (let dy = -r; dy <= r && zone.length < count; dy++) {
        for (let dx = -r; dx <= r && zone.length < count; dx++) {
          if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
          const p = { x: center.x + dx, y: center.y + dy, z: center.z };
          if (inBounds(s, p.x, p.y) && canStandOn(kindAt(s, p)) && !unitAt(s, p) && !zone.some((z) => samePos(z, p))) zone.push(p);
        }
      }
    }
    return zone;
  }

  // ------------------------------------------------------------ turn flow

  /**
   * Elimination ends when one side is gone. Objective missions are won once the
   * objective is secured and then either the hostiles are dead or the squad has
   * left the map (with someone evacuated).
   */
  checkOutcome(): void {
    if (this.over) return;
    const s = this.s;
    const m = s.mission;
    const squad = combatants(s, 'xcom');
    const hostiles = combatants(s, 'alien');
    const end = (outcome: 'victory' | 'defeat', reason: 'objective' | 'wiped' | 'objectiveLost' | 'abandoned') =>
      this.emit({ t: 'missionEnded', outcome, reason });

    // The Limpieza is won clearing the map; leaving it by the bus is a retreat.
    if (m.kind === 'elimination') {
      if (!hostiles.length) end('victory', 'objective');
      else if (m.evacDone) end('defeat', 'abandoned');
      else if (!squad.length) end('defeat', 'wiped');
      return;
    }
    const target = m.objectiveUnit ? getUnit(s, m.objectiveUnit) : undefined;
    if (m.kind === 'rescue' && target && !target.alive && !target.evacuated) {
      end('defeat', 'objectiveLost');
      return;
    }
    // Every other mission is won only back on the bus (docs/PRUEBA-2026-10-14.md P1), with the
    // objective done (the item or the VIP on it); a Saqueo, by just getting somebody out.
    if (m.evacDone) {
      const someoneOut = s.units.some((u) => u.team === 'xcom' && u.evacuated);
      const won = someoneOut && (m.kind === 'scavenge' || m.objectiveDone);
      end(won ? 'victory' : 'defeat', won ? 'objective' : 'abandoned');
      return;
    }
    if (!squad.length) end('defeat', 'wiped');
  }

  /**
   * A player asks to evacuate from `u`, in the bus (docs/PRUEBA-2026-10-14.md
   * P1). With the partner on the field and connected it waits for their yes;
   * alone, or answering the partner's own call, the bus leaves now.
   */
  callEvac(slot: Slot, u: Unit): void {
    const partner: Slot = slot === 0 ? 1 : 0;
    const partnerOnField = this.connected.includes(partner) && this.s.units.some((o) => o.alive && o.team === 'xcom' && o.owner === partner);
    if (!partnerOnField || this.s.evacCall?.by === partner) this.departEvac();
    else this.emit({ t: 'evacCalled', slot, unit: u.id });
  }

  /** The bus leaves: whoever is in it gets out (first), everyone else is lost; then the mission is decided. */
  departEvac(): void {
    this.emit({ t: 'evacDeparted' });
    const squad = this.s.units.filter((o) => o.alive && o.team === 'xcom');
    // The bitten board only with a Cuarentena at home (playtest 2026-10-15, #13).
    const boards = (o: Unit) => inEvacZone(this.s, o.pos) && !(o.infected && this.s.mission.quarantine === false);
    for (const u of squad.filter(boards)) this.emit({ t: 'evacuated', unit: u.id });
    for (const u of squad.filter((o) => !boards(o))) this.emit({ t: 'leftBehind', unit: u.id });
    this.checkOutcome();
  }

  /** Ends the XCOM turn once every player with soldiers able to act is ready. */
  maybeEndXcomTurn(): void {
    if (this.over || this.s.activeTeam !== 'xcom') return;
    const pending = new Set<Slot | 'pool'>();
    for (const u of combatants(this.s, 'xcom')) if (u.ap > 0) pending.add(u.owner ?? 'pool');
    const allConnectedReady = this.connected.every((slot) => this.s.ready[slot]);
    const done = [...pending].every((k) =>
      k === 'pool' ? allConnectedReady : this.s.ready[k] || !this.connected.includes(k),
    );
    if (done) this.runAlienTurn();
  }

  /**
   * Active aliens act, inactive pods patrol, due reinforcements land, and the
   * flares for the next drop go up so the squad gets a turn to prepare.
   */
  runAlienTurn(): void {
    const turn = this.s.turn;
    this.alienBudget = { explosives: 1, psi: 1 };
    this.emit({ t: 'turnBegan', team: 'alien', turn });
    // They rise now, but only act from the next turn on (they had no turn to begin).
    this.riseInfected();
    const active = this.s.units.filter((u) => u.alive && u.team === 'alien' && !u.objective && this.s.pods.find((p) => p.id === u.podId)?.active);
    // The dead with nobody to reach this turn walk first, each group in one go; then everyone else, in order (docs/UX-RITMO.md).
    const walkers = active.filter((u) => onlyWalks(this.s, u));
    const actors = [...walkers, ...active.filter((u) => !walkers.includes(u))].map((u) => u.id);
    for (const id of actors) {
      const u = getUnit(this.s, id)!;
      if (this.over) return;
      if (u.alive) takeAlienTurn(this, u);
    }
    // Pods still unaware walk their round, or go to look where they heard something.
    for (const pod of this.s.pods.filter((p) => !p.active && (p.patrol.length || p.unitIds.some((id) => getUnit(this.s, id)?.heard)))) {
      if (this.over) return;
      patrolPod(this, pod);
    }
    if (this.over) return;
    this.landReinforcements(turn);
    if (this.over) return;
    this.callHorde(turn);
    this.announceReinforcements(turn + 1);
    // Mind control wears off with time. Nothing takes control any more (the scream never does): only missions saved before.
    for (const u of this.s.units.filter((x) => x.alive && x.controlledBy)) {
      this.emit(u.controlTurns <= 1 ? { t: 'released', unit: u.id } : { t: 'controlTick', unit: u.id });
    }
    this.checkOutcome();
    if (this.over) return;
    const left = this.s.mission.turnsLeft;
    if (left !== null && left - 1 <= 0 && !this.s.mission.objectiveDone) {
      this.emit({ t: 'missionEnded', outcome: 'defeat', reason: 'timer' });
      return;
    }
    // Bengalas and fires on their last turn burn out; the rest burn down as the squad's turn begins.
    for (const l of [...(this.s.lights ?? [])]) if (l.turns !== undefined && l.turns <= 1) this.emit({ t: 'lightOff', id: l.id });
    this.emit({ t: 'turnBegan', team: 'xcom', turn: turn + 1, command: this.openingCommander(turn + 1) });
    // The bitten weaken: one point of health at every turn of the squad (playtest 2026-10-15, #13), armour or not.
    for (const u of this.s.units.filter((x) => x.alive && x.team === 'xcom' && x.infected)) this.damage(u, 1, 0, undefined, 99);
    this.checkOutcome();
  }

  /** Players take turns opening the squad's turn, skipping one who isn't here or has nobody left. */
  openingCommander(turn: number): Slot {
    const first: Slot = turn % 2 === 1 ? 0 : 1;
    const order: Slot[] = [first, first === 0 ? 1 : 0];
    const fielded = (slot: Slot) => combatants(this.s, 'xcom').some((u) => u.owner === slot);
    return order.find((slot) => this.connected.includes(slot) && fielded(slot)) ?? order.find((slot) => this.connected.includes(slot)) ?? first;
  }

  /** Whether `slot` has a soldier (or a shared unit) that can still act. */
  hasActions(slot: Slot): boolean {
    return combatants(this.s, 'xcom').some((u) => u.ap > 0 && (u.owner === slot || u.owner === null));
  }

  /** Whether turns go one soldier at a time (playtest 2026-10-15, #9): the partner is here, not done, and has someone to move. */
  alternates(partner: Slot): boolean {
    return this.canReceiveCommand(partner) && this.hasActions(partner);
  }

  /**
   * After a soldier's order (#9): while it has actions it is the one in play;
   * once it has none (or is down, or gone), the partner plays one of theirs.
   */
  afterSoldier(u: Unit, apBefore: number): void {
    if (this.over || this.s.activeTeam !== 'xcom' || u.owner === null) return;
    const live = getUnit(this.s, u.id);
    if (live && live.alive && !live.evacuated && live.ap > 0) {
      if (live.ap < apBefore && this.s.activeUnit !== live.id) this.emit({ t: 'unitActive', unit: live.id });
      return;
    }
    if (this.s.activeUnit) this.emit({ t: 'unitActive', unit: null });
    const partner: Slot = u.owner === 0 ? 1 : 0;
    if (this.s.command === u.owner && this.alternates(partner)) this.passCommand(partner);
  }

  /** The partner can take the command: connected and not done with the turn. */
  canReceiveCommand(slot: Slot): boolean {
    return this.connected.includes(slot) && !this.s.ready[slot];
  }

  passCommand(to: Slot): void {
    if (this.s.command !== to) this.emit({ t: 'commandPassed', to });
  }

  /**
   * A holder with nothing left to do hands the command over by itself, so the
   * partner isn't left waiting for a click.
   */
  autoPassCommand(): void {
    if (this.over || this.s.activeTeam !== 'xcom') return;
    const holder = this.s.command;
    if (holder === undefined) return;
    const partner: Slot = holder === 0 ? 1 : 0;
    if (!this.hasActions(holder) && this.canReceiveCommand(partner) && this.hasActions(partner)) this.passCommand(partner);
  }

  /** A backpack order already checked (backpackRefusal). */
  useBackpack(cmd: ItemCommand): void {
    const u = getUnit(this.s, cmd.unit)!;
    switch (cmd.action) {
      case 'drop':
        this.emit({ t: 'lootDropped', unit: u.id, index: cmd.index! });
        break;
      case 'take':
        this.emit({ t: 'lootTaken', unit: u.id, id: cmd.id! });
        break;
      case 'give':
        this.emit({ t: 'lootGiven', unit: u.id, to: cmd.to!, index: cmd.index! });
        break;
      case 'use': {
        const target = cmd.to ? getUnit(this.s, cmd.to)! : u;
        this.emit({ t: 'medicineUsed', unit: u.id, target: target.id, index: cmd.index! });
        const amount = Math.min(MEDICINE_HEAL + u.mods.heal, target.maxHp - target.hp);
        if (amount > 0) this.emit({ t: 'healed', unit: u.id, target: target.id, amount });
        if (target.infected && medicineStopsBite(this.s, u)) this.emit({ t: 'cured', unit: target.id, by: u.id });
        break;
      }
      case 'equip':
        this.emit({ t: 'weaponEquipped', unit: u.id, index: cmd.index! });
        break;
    }
  }

  afterXcomAction(): void {
    if (this.over) return;
    for (const u of combatants(this.s, 'xcom')) {
      const noticing = this.podsNoticing(u.pos);
      if (noticing.length) this.activatePods(noticing, this.sighting(noticing, u, u.pos));
      if (this.over) return;
    }
    this.checkOutcome();
    this.maybeEndXcomTurn();
    this.autoPassCommand();
  }
}

/** What a corpse is called once it rises: a soldier keeps its name («Zombi · Clara Bosch»), anyone else is a Zombi. */
export function risenName(corpse: Unit): string {
  return TEMPLATES[corpse.template].team === 'xcom' ? `${TEMPLATES.zombie.name} · ${corpse.name}` : TEMPLATES.zombie.name;
}

/** «A dos voces»: a partner's singer within 2 tiles adds one more to a song. */
export function duet(s: GameState, u: Unit): number {
  const partner = s.units.some(
    (a) =>
      a.alive && a.id !== u.id && a.team === u.team && a.owner !== null && u.owner !== null && a.owner !== u.owner && a.mods.singer > 0 && distance(a.pos, u.pos) <= 2 && sameSpace(s, a.pos, u.pos),
  );
  return partner ? 1 : 0;
}

/** What the thrower adds to an explosion: damage (grenade talents, a song's strength) and shred. */
function blastMods(s: GameState, u: Unit, ability: BlastAbility): { bonus: number; shred: number } {
  const weapon = WEAPONS[ABILITIES[ability].explosive!];
  const grenade = ability === 'grenade' || ability === 'launch';
  const bonus = grenade ? u.mods.grenadeDamage : ability === 'fireball' ? u.mods.songDamage + duet(s, u) : 0;
  return { bonus, shred: weapon.shred + (grenade ? u.mods.grenadeShred : 0) };
}

/**
 * What a damaging ability would take off `target` (or, with none, its own
 * numbers, for the ability cards): attacks, explosives, Velmar's songs and the
 * Grillo's Calambrazo. `shot` is the attack's preview against that
 * target (flank and distance). Null for abilities that do no damage.
 */
export function abilityDamage(s: GameState, u: Unit, ability: AbilityId, target?: Unit, shot?: Pick<ShotPreview, 'flanked' | 'distance'>): DamagePreview | null {
  if (isAttack(ability)) return shotDamage(u, ability, target, shot);
  const flat = (raw: number, pierce = 0): DamagePreview => ({
    hit: rangeOn(target, { min: raw, max: raw }, pierce),
    crit: null,
    graze: null,
    armor: target ? Math.max(0, target.armor - pierce) : 0,
    shred: 0,
    ignoresArmor: pierce === Infinity,
  });
  switch (ability) {
    case 'bolt':
      return flat(BOLT_DAMAGE + u.mods.songDamage + duet(s, u));
    case 'velmarStorm':
      return flat(STORM_DAMAGE + u.mods.songDamage + duet(s, u));
    case 'combatProtocol':
      return flat(WEAPONS.droneZap.damage + (target && TEMPLATES[target.template].robotic ? 2 : 0) + u.mods.protocolDamage, Infinity);
    case 'grenade':
    case 'launch':
    case 'missiles':
    case 'discharge':
    case 'fireball': {
      const { bonus, shred } = blastMods(s, u, ability);
      return { hit: rangeOn(target, damageRange(WEAPONS[ABILITIES[ability].explosive!], 'hit', bonus)), crit: null, graze: null, armor: target?.armor ?? 0, shred, ignoresArmor: false };
    }
  }
  return null;
}

/** Places within `radius` of `center`, on its floor (kept for UI previews of the basic grenade; see blastTiles). */
export function tilesInRadius(s: GameState, center: Pos, radius: number): Pos[] {
  const out: Pos[] = [];
  const r = Math.ceil(radius);
  for (let y = center.y - r; y <= center.y + r; y++) {
    for (let x = center.x - r; x <= center.x + r; x++) {
      if (!inBounds(s, x, y) || distance(center, { x, y }) > radius) continue;
      if (!s.levels) out.push({ x, y, z: 0 });
      else for (const p of placesOn(s, x, y)) if (sameSpace(s, center, p)) out.push(p);
    }
  }
  return out;
}

// ------------------------------------------------------------- commands

/** Puente: the specialist's drone does more for the other player's soldiers. */
function bridged(u: Unit, target: Unit): boolean {
  return hasPerk(u, 'bridge') && u.owner !== null && target.owner !== null && u.owner !== target.owner;
}

function commandUnit(s: GameState, id: string, slot: Slot): Unit | string {
  const u = getUnit(s, id);
  if (!u || !u.alive) return 'Esa unidad no existe o está fuera de combate.';
  if (u.team !== 'xcom') return 'No puedes dar órdenes a esa unidad.';
  if (u.owner !== null && u.owner !== slot) return 'Ese soldado pertenece a tu compañero.';
  if (u.ap <= 0) return 'Ese soldado no tiene acciones.';
  return u;
}

type ItemCommand = Extract<Command, { type: 'item' }>;

/**
 * Why a backpack order can't go through (docs/FASE8.md §C), or null. Dropping and taking are free, even with no
 * actions left; giving, using a medicine and equipping a weapon take one.
 */
function backpackRefusal(s: GameState, cmd: ItemCommand, slot: Slot): string | null {
  const u = getUnit(s, cmd.unit);
  if (!u || !u.alive) return 'Esa unidad no existe o está fuera de combate.';
  if (u.team !== 'xcom' || u.captive || u.objective) return 'No puedes dar órdenes a esa unidad.';
  if (u.owner !== null && u.owner !== slot) return 'Ese soldado pertenece a tu compañero.';
  if (u.controlledBy || u.panicked) return 'Ese soldado no responde.';
  const free = cmd.action === 'drop' || cmd.action === 'take';
  if (!free && u.ap <= 0) return 'Ese soldado no tiene acciones.';
  if (cmd.action === 'take') {
    const left = s.dropped?.find((d) => d.id === cmd.id);
    if (!left || !samePos(left.pos, u.pos)) return 'Aquí no hay nada que coger.';
    if (!left.loot.mags && (u.backpack?.length ?? 0) >= packSlots(u)) return 'La mochila está llena: suelta algo antes.';
    return null;
  }
  const loot = cmd.index === undefined ? undefined : u.backpack?.[cmd.index];
  if (!loot) return 'No lleva eso en la mochila.';
  switch (cmd.action) {
    case 'drop':
      return null;
    case 'give': {
      const to = cmd.to ? getUnit(s, cmd.to) : undefined;
      if (!to || !to.alive || to.team !== 'xcom' || to.captive || to.objective || to.id === u.id) return 'Solo a un compañero.';
      if (!inMeleeReach(s, u.pos, to.pos)) return 'Tiene que estar a tu lado.';
      if ((to.backpack?.length ?? 0) >= packSlots(to)) return 'No le cabe: lleva la mochila llena.';
      return null;
    }
    case 'use': {
      if (!loot.medicines) return 'Solo se usan las medicinas.';
      const to = cmd.to ? getUnit(s, cmd.to) : u;
      if (!to || !to.alive || to.team !== 'xcom' || to.objective) return 'Solo en ti o en un compañero.';
      if (to !== u && !inMeleeReach(s, u.pos, to.pos)) return 'Tiene que estar a tu lado.';
      if (to.hp >= to.maxHp && !(to.infected && medicineStopsBite(s, u))) return to === u ? 'No estás herido.' : 'No está herido.';
      return null;
    }
    case 'equip':
      if (!loot.weapon) return 'Solo se equipan las armas.';
      if (!u.arms) return 'Las armas encontradas son para el Bastión.';
      return null;
  }
}

/** Whether `u`'s medicines also stop a bite's contagion (docs/FASE8.md §C): the Enfermería for everyone, or the support talent. */
export function medicineStopsBite(s: GameState, u: Unit): boolean {
  return !!s.mission?.medicineCures || hasPerk(u, 'lightHands');
}

/** Why a support ability can't go to that ally: the reason, not just "no". */
function allyRefusal(u: Unit, ability: AbilityId, target: Unit | undefined): string {
  if (!target || !target.alive) return 'Aliado no válido.';
  if (distance(u.pos, target.pos) > (ABILITIES[ability].range ?? Infinity)) return ABILITIES[ability].range && ABILITIES[ability].range! <= 1.5 ? 'Tiene que estar a tu lado.' : 'Está demasiado lejos.';
  switch (ability) {
    case 'medkit':
    case 'firstAid':
      return 'No está herido.';
    case 'revival':
      return 'No está aturdido, desorientado ni en pánico.';
    case 'patchUp':
      return target.arms?.primary ? 'Su arma está como nueva.' : 'No lleva arma que arreglar.';
    case 'layOnHands':
      return 'No está herido ni afectado.';
    case 'aid':
      return target.id === u.id ? 'No puedes protegerte a ti mismo.' : 'Ya está protegido.';
  }
  return 'Aliado no válido.';
}

export function executeCommand(s: GameState, cmd: Command, ctx: CommandContext): CommandResult {
  if (s.outcome) return { ok: false, error: 'La misión ha terminado.' };
  if (s.activeTeam !== 'xcom') return { ok: false, error: 'Espera a que termine el turno enemigo.' };

  const sim = new Sim(s, ctx.rng, ctx.connected);

  const partner: Slot = ctx.slot === 0 ? 1 : 0;
  // Whoever holds the command acts; if it's held by nobody here, the first to act takes it.
  const holds = s.command === ctx.slot || s.command === undefined || !ctx.connected.includes(s.command);

  if (cmd.type === 'endTurn') {
    const turn = s.turn;
    if (s.ready[ctx.slot] !== cmd.ready) sim.emit({ t: 'ready', slot: ctx.slot, ready: cmd.ready });
    sim.maybeEndXcomTurn();
    // Done for this turn (and it goes on): the partner, if still playing, gets the command.
    const goesOn = s.turn === turn && s.activeTeam === 'xcom' && !s.outcome;
    if (cmd.ready && holds && goesOn && sim.canReceiveCommand(partner)) sim.passCommand(partner);
    return { ok: true, events: sim.events };
  }

  // Evacuating (docs/PRUEBA-2026-10-14.md P1): either player calls it from a soldier in the bus, the
  // other answers; neither needs the command, nor actions left.
  if (cmd.type === 'evacAnswer') {
    if (!s.evacCall || s.evacCall.by === ctx.slot) return { ok: false, error: 'No hay ninguna petición de evacuar que contestar.' };
    if (cmd.accept) sim.departEvac();
    else sim.emit({ t: 'evacRefused', slot: ctx.slot });
    return { ok: true, events: sim.events };
  }
  if (cmd.type === 'evac') {
    const u = getUnit(s, cmd.unit);
    if (!u || !u.alive || u.team !== 'xcom' || u.captive) return { ok: false, error: 'Esa unidad no puede evacuar.' };
    if (u.owner !== null && u.owner !== ctx.slot) return { ok: false, error: 'Ese soldado pertenece a tu compañero.' };
    if (!inEvacZone(s, u.pos)) return { ok: false, error: 'Solo desde el autobús, en la zona de evacuación.' };
    if (s.evacCall?.by === ctx.slot) return { ok: false, error: 'Ya has pedido evacuar: falta que conteste tu compañero.' };
    if (u.infected && s.mission.quarantine === false) return { ok: false, error: 'Con un mordisco no se sube a La Lata sin una Cuarentena en el Bastión.' };
    sim.callEvac(ctx.slot, u);
    return { ok: true, events: sim.events };
  }

  if (cmd.type === 'pass') {
    if (!holds) return { ok: false, error: 'No tienes el mando.' };
    if (!ctx.connected.includes(partner)) return { ok: false, error: 'Tu compañero no está conectado.' };
    if (s.ready[partner]) return { ok: false, error: 'Tu compañero ya ha terminado el turno.' };
    if (!sim.hasActions(partner)) return { ok: false, error: 'A tu compañero no le quedan acciones.' };
    sim.passCommand(partner);
    return { ok: true, events: sim.events };
  }

  if (!holds) return { ok: false, error: 'Tiene el mando tu compañero: espera a que te lo ceda.' };
  // Turns go one soldier at a time (playtest 2026-10-15, #9): the one in play is finished before another of yours.
  const inPlay = (id: string): string | null => {
    const busy = s.activeUnit && s.activeUnit !== id ? getUnit(s, s.activeUnit) : undefined;
    return busy && busy.alive && busy.ap > 0 && busy.owner === ctx.slot && sim.alternates(partner) ? `Primero termina con ${busy.name}, o cede el turno.` : null;
  };
  if (cmd.type === 'item') {
    const refusal = backpackRefusal(s, cmd, ctx.slot);
    if (refusal) return { ok: false, error: refusal };
    const free = cmd.action === 'drop' || cmd.action === 'take';
    const busy = free ? null : inPlay(cmd.unit);
    if (busy) return { ok: false, error: busy };
    const holder = getUnit(s, cmd.unit)!;
    const apBefore = holder.ap;
    sim.passCommand(ctx.slot);
    sim.useBackpack(cmd);
    if (!free) sim.afterSoldier(holder, apBefore);
    sim.afterXcomAction();
    return { ok: true, events: sim.events };
  }
  const u = commandUnit(s, cmd.unit, ctx.slot);
  if (typeof u === 'string') return { ok: false, error: u };
  const busy = inPlay(u.id);
  if (busy) return { ok: false, error: busy };
  const apBefore = u.ap;
  // The command changes hands only once the order is sure to go through: a refused one changes nothing
  // (it used to pass first, and the host kept a holder the partner never heard of).

  if (cmd.type === 'move') {
    // A place without its level is on the ground (docs/FASE6-PLAN.md).
    const to = toPos(cmd.to);
    const reach = computeReach(s, u, maxMoveCost(u));
    const path = buildPath(s, u, reach, to);
    if (!path) return { ok: false, error: 'No puede llegar ahí.' };
    const cost = reach.cost.get(nodeOf(s, to))!;
    const ap = apForCost(u, cost);
    if (ap > u.ap) return { ok: false, error: 'No le quedan acciones para llegar ahí.' };
    sim.passCommand(ctx.slot);
    sim.moveAlong(u, path, ap);
    // Running is heard (docs/FASE2.md).
    if (ap >= 2 && u.alive) sim.makeNoise(u.pos, NOISE.dash, u);
    sim.afterSoldier(u, apBefore);
    sim.afterXcomAction();
    return { ok: true, events: sim.events };
  }

  const blocker = abilityBlocker(s, u, cmd.ability);
  if (blocker) return { ok: false, error: blocker };
  const def = ABILITIES[cmd.ability];
  const target = cmd.target ? getUnit(s, cmd.target) : undefined;

  switch (def.target) {
    case 'enemy':
    case 'ally':
    case 'corpse':
      if (!target || !abilityTargets(s, u, cmd.ability).includes(target)) {
        return { ok: false, error: def.target === 'ally' ? allyRefusal(u, cmd.ability, target) : 'Objetivo fuera de alcance o sin línea de visión.' };
      }
      sim.passCommand(ctx.slot);
      sim.perform(u, cmd.ability, { target });
      break;
    case 'melee': {
      const option = isAttack(cmd.ability) ? meleeTargets(s, u, cmd.ability).find((o) => o.target.id === cmd.target) : undefined;
      if (!option) return { ok: false, error: 'No puede alcanzar a ese enemigo este turno.' };
      sim.passCommand(ctx.slot);
      sim.perform(u, cmd.ability, { melee: option });
      break;
    }
    case 'tile': {
      const tile = cmd.tile && toPos(cmd.tile);
      if (!tile || !canTargetTile(s, u, cmd.ability, tile)) {
        const why: Partial<Record<AbilityId, string>> = { search: 'No hay ninguna caja que abrir ahí.', openDoor: 'Esa casilla no es una puerta cerrada contigua.', closeDoor: 'Esa casilla no es una puerta abierta, libre y contigua.' };
        return { ok: false, error: why[cmd.ability] ?? 'Fuera de alcance o fuera de la vista de la escuadra.' };
      }
      sim.passCommand(ctx.slot);
      sim.perform(u, cmd.ability, { tile });
      break;
    }
    case 'self':
      sim.passCommand(ctx.slot);
      sim.perform(u, cmd.ability);
      break;
  }

  sim.afterSoldier(u, apBefore);
  sim.afterXcomAction();
  return { ok: true, events: sim.events };
}


/** Re-evaluates the end of turn, e.g. after a player disconnects. */
export function syncTurn(s: GameState, ctx: Omit<CommandContext, 'slot'>): GameEvent[] {
  const sim = new Sim(s, ctx.rng, ctx.connected);
  // A call to evacuate whose answerer has gone: the bus leaves (docs/PRUEBA-2026-10-14.md P1).
  if (s.evacCall && s.activeTeam === 'xcom' && !s.outcome && !ctx.connected.includes(s.evacCall.by === 0 ? 1 : 0)) sim.departEvac();
  sim.maybeEndXcomTurn();
  // A holder who left hands the command to whoever is still here.
  const holder = s.command;
  const here = ctx.connected[0];
  if (s.activeTeam === 'xcom' && !s.outcome && here !== undefined && (holder === undefined || !ctx.connected.includes(holder))) sim.passCommand(here);
  return sim.events;
}

/**
 * Whether the bus leaving now wins the mission (docs/PRUEBA-2026-10-14.md P1),
 * for the prompt: a Limpieza is never won by leaving; a Saqueo, by anyone on
 * board; the rest, with the objective done, or its item or VIP on board.
 */
export function evacWins(s: GameState): boolean {
  const m = s.mission;
  if (m.kind === 'elimination') return false;
  const aboard = s.units.filter((u) => u.alive && u.team === 'xcom' && inEvacZone(s, u.pos));
  if (!aboard.length) return false;
  if (m.kind === 'scavenge' || m.objectiveDone) return true;
  if (m.kind === 'recovery') return !!m.item?.carrier && aboard.some((u) => u.id === m.item!.carrier);
  if (m.kind === 'rescue') return !!m.objectiveUnit && aboard.some((u) => u.id === m.objectiveUnit);
  return false;
}

/** Ends the mission early (either player can abandon). */
export function abandonMission(s: GameState): GameEvent[] {
  if (s.outcome) return [];
  const e: GameEvent = { t: 'missionEnded', outcome: 'defeat', reason: 'abandoned' };
  applyEvent(s, e);
  return [e];
}
