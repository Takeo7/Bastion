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
  riseSpot,
  type MeleeOption,
} from './abilities';
import { chooseScamperTile, frontFirst, onlyWalks, patrolPod, takeAlienTurn } from './ai';
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
  attackWeapon,
  FALL_DAMAGE,
  hasPerk,
  commandsFor,
  HOLO_AIM,
  HORDE,
  hostileTo,
  INFECTION,
  isAttack,
  ITEMS,
  loadedFor,
  MEDICINE_HEAL,
  MEDKIT_HEAL,
  STONE_DAMAGE,
  TRAPS,
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
import { objectAt } from './decor';
import { applyEvent, type GameEvent } from './events';
import { canStopAt, restingPlace } from './flights';
import {
  canStandOn,
  combatants,
  DIRS4,
  distance,
  getUnit,
  heightAt,
  inBounds,
  kindAt,
  nodeOf,
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
import { canSee, detectionTo, facingTo, inViewCone, sightOrigin, sightTo } from './los';
import { apForCost, buildPath, computeReach, costPerAp, maxMoveCost, onPatrol, type Reach } from './path';
import type { Command } from './protocol';
import type { Rng } from './rng';
import { hordeWave, makeUnit } from './setup';
import { pendingBars, rollGrowth } from './skills';
import type {
  AbilityId,
  AttackAbility,
  BlastAbility,
  ConcealReason,
  GameState,
  ItemId,
  Pod,
  PodCause,
  PsiEffect,
  Reinforcement,
  ShotResult,
  Slot,
  Pos,
  TileKind,
  Unit,
  Vec2,
} from './types';

/** Reinforcements drop this far (in tiles) from the nearest soldier. */
const DROP_MIN = 7;
const DROP_MAX = 13;
/** The horde comes in this close to the map's edge, and at least this far from every soldier (fallbacks after). */
const EDGE_BAND = 2;
const EDGE_GAP = [12, 8, 0] as const;
/** Nor this close to the evac zone. */
const EDGE_EVAC_GAP = 3;

/** What leaves the dead's ways as they were (Sim.ways): neither the ground nor where the living stand changes. */
const KEEPS_WAYS: ReadonlySet<GameEvent['t']> = new Set<GameEvent['t']>(['abilityUsed', 'noise', 'forgot', 'smashed', 'shot', 'damaged', 'infected', 'stunned', 'disoriented', 'psi', 'overwatch', 'trapSprung']);

/** How many more times in an enemy turn those waiting behind their own get to go on (Sim.runAlienTurn). */
const JAM_PASSES = 2;

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
 * The places of the whole object standing on place `p` (decor.ts objectAt: a car over three tiles, a bed over two,
 * a lorry's block), on its storey; `p` alone for anything on one tile.
 */
function objectPlaces(s: GameState, p: Pos): Pos[] {
  const decor = p.z ? s.levels?.[p.z - 1]?.decor : s.decor;
  const o = decor ? objectAt(decor, s.width, tileIndex(s, p.x, p.y)) : null;
  return o && o.tiles.length > 1 ? o.tiles.map((i) => ({ x: i % s.width, y: Math.floor(i / s.width), z: p.z })) : [p];
}

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
        if (canStopAt(s, q) && !unitAt(s, q) && !taken.has(nodeOf(s, q))) return q;
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
  /** Enemies left waiting behind their own this enemy turn (ai.ts walk): they go again once the others have moved. */
  readonly jammed = new Set<string>();
  /**
   * In a siege, where the dead get to from each place, their own aside (ai.ts breachPlan), while nothing changes
   * that would change it: what stands where (a door gives way), or the living (one falls). Their own moving doesn't.
   */
  readonly ways = new Map<number, Reach>();

  constructor(
    readonly s: GameState,
    readonly rng: Rng,
    readonly connected: readonly Slot[],
  ) {}

  emit(e: GameEvent): void {
    applyEvent(this.s, e);
    this.events.push(e);
    if (!this.ways.size || KEEPS_WAYS.has(e.t)) return;
    const mover = e.t === 'moved' ? getUnit(this.s, e.unit) : undefined;
    if (!(mover && TEMPLATES[mover.template].undead)) this.ways.clear();
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
      case 'revival':
        this.emit({ t: 'cleansed', unit: u.id, target: target!.id });
        break;
      case 'patchUp':
        this.emit({ t: 'patched', unit: u.id, target: target!.id, amount: Math.min(PATCH_UP, 100 - conditionOf(target!)) });
        break;
      case 'restoration':
        this.restoration(u);
        break;
      case 'grenade':
      case 'launch':
      case 'missiles':
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
        // What Curar has grown and the board's cells heal more (docs/FASE10.md); Primeros auxilios and the rest also shelter.
        // Entre los dos (board.ts): 2 more on the other player's soldiers.
        this.emit({ t: 'healed', unit: u.id, target: target!.id, amount: Math.min(MEDKIT_HEAL + u.mods.heal + (bridged(u, target!) ? 2 : 0), target!.maxHp - target!.hp) });
        if (u.mods.aid > target!.aided) this.emit({ t: 'aided', unit: u.id, target: target!.id, amount: u.mods.aid });
        break;
      case 'overwatch':
      case 'killZone':
        this.emit({ t: 'overwatch', unit: u.id, killZone: ability === 'killZone' });
        break;
      case 'overwatchMelee':
        // With Muro de acero it strikes everyone that comes alongside, once each, as Nadie pasa does with the gun.
        this.emit({ t: 'overwatch', unit: u.id, killZone: hasPerk(u, 'bladeWall'), melee: true });
        break;
      case 'stone':
        this.stone(u, tile!);
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
      case 'smash':
        this.smash(u, tile!);
        break;
    }
    // Then it is heard (docs/FASE2.md): where a tile ability lands, or where the user stands.
    // Whoever fell doing it made no more noise than what already sounded (the Hinchado sounds as it bursts).
    if (u.alive) this.makeNoise(tile ?? u.pos, noiseOf(this.s, u, ability), u);
  }

  /**
   * One of the dead beats on a door or a barricade (docs/FASE12.md §4): its
   * blow's damage off what it still holds. At nothing it gives way, and the
   * way in is open ground.
   */
  private smash(u: Unit, tile: Pos): void {
    const left = this.s.breakables?.[nodeOf(this.s, tile)] ?? 0;
    const amount = Math.min(left, attackWeapon(u, 'claw').damage);
    this.emit({ t: 'smashed', unit: u.id, tile: { ...tile }, amount, left: left - amount });
    if (left - amount <= 0) this.emit({ t: 'tileChanged', pos: { ...tile }, kind: 'floor' });
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
      // Segundo aliento: a blade kill heals 1.
      if (!target.alive && weapon.melee && hasPerk(attacker, 'secondWind') && attacker.alive && attacker.hp < attacker.maxHp) {
        this.emit({ t: 'healed', unit: attacker.id, target: attacker.id, amount: 1 });
      }
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
        hostileTo(o, attacker) &&
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
    // Off a flight of stairs, at its nearer end (flights.ts): somewhere one can stop to pick it up.
    if (this.s.mission.item?.carrier === u.id) this.emit({ t: 'itemDropped', pos: restingPlace(this.s, u.pos) });
    this.emit({ t: 'died', unit: u.id, by: source?.id });
    // A patrol car's or an ambulance's siren silenced: the car stays, over its tile as over its nose's (19/10), solid. Without
    // an alarm said (a skirmish, older saves), a car but in a facility, as the client draws it (unitViews.ts ALARM_BY_MAP).
    const alarm = this.s.mission.alarm;
    const car = alarm ? alarm !== 'factory' : this.s.biome !== 'facility';
    if (u.objective && this.s.mission.kind === 'sabotage' && car && canStandOn(kindAt(this.s, u.pos))) {
      // Its tile a part of the car beside it (docs/MAPAS.md, phase 1: the rest of its footprint), facing it; without one, the car's own.
      const decor = u.pos.z ? this.s.levels?.[u.pos.z - 1]?.decor : this.s.decor;
      const toCar = DIRS4.findIndex((d) => {
        const x = u.pos.x + d.x;
        const y = u.pos.y + d.y;
        return !!decor && inBounds(this.s, x, y) && objectAt(decor, this.s.width, tileIndex(this.s, x, y))?.kind === 'sirenCar';
      });
      this.emit({ t: 'tileChanged', pos: { ...u.pos }, kind: 'high', ...(toCar >= 0 ? { decor: 'part' as const, facing: toCar } : { decor: 'sirenCar' as const }) });
    }
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

  /**
   * A stone (docs/FASE10.md §4): heard where it lands (perform makes the
   * noise), and whoever stands there takes its damage. Only a hit gives the
   * squad away.
   */
  private stone(u: Unit, at: Pos): void {
    const hit = unitAt(this.s, at);
    const struck = hit?.alive && hit.id !== u.id && !hit.objective ? hit : undefined;
    this.emit({ t: 'stoneThrown', unit: u.id, target: { ...at }, hit: struck?.id ?? null });
    if (!struck) return;
    this.damage(struck, STONE_DAMAGE + u.mods.stoneDamage, 0, u);
    if (u.team === 'xcom') this.breakConcealment({ reason: 'attack', unit: u.id, by: struck.id });
    if (struck.team === 'alien') this.activatePodOf(struck, u);
    this.checkOutcome();
  }

  meleeAttack(u: Unit, option: MeleeOption, ability: AttackAbility): void {
    if (option.path.length) this.moveAlong(u, option.path, 0);
    const target = getUnit(this.s, option.target.id);
    if (!u.alive || !target?.alive || this.over || !inMeleeReach(this.s, u.pos, target.pos)) return;
    const result = this.attack(u, target, ability);
    if (ability === 'stunLance' && result && result !== 'miss' && target.alive) this.emit({ t: 'stunned', unit: target.id });
    // Golpe al oído (board.ts): a blade that strikes leaves the enemy reeling.
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
    const { bonus, shred } = blastMods(source, ability);
    for (const v of victims) {
      const before = v.hp;
      this.damage(v, rollDamage(weapon, 'hit', this.rng, bonus), shred, source);
      if (bursting && v.hp < before) this.infect(v, source, INFECTION.burst);
    }
    // The Hinchado's burst is only gas: it leaves the scenery alone.
    if (!bursting) {
      // An object over several tiles goes as one (a car, a bed, a lorry): all of it as the tile the blast reached.
      const done = new Set<number>();
      for (const p of tiles) {
        const kind = kindAt(this.s, p);
        for (const q of kind === 'low' || kind === 'high' ? objectPlaces(this.s, p) : [p]) {
          const node = nodeOf(this.s, q);
          if (done.has(node)) continue;
          done.add(node);
          const now = kindAt(this.s, q);
          const next = now && BLAST_RESULT[now];
          if (next) this.emit({ t: 'tileChanged', pos: q, kind: next });
        }
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
      // «Nervios de acero» keeps panic out («Serenidad» is out of its reach: psiChance).
      if (effect === 'panicked' && target.mods.psiGuard >= 1) effect = 'disoriented';
    }
    this.emit({ t: 'psi', unit: caster.id, target: target.id, success, effect, chance });
    this.checkOutcome();
  }

  /** The corpse gets up where it lies, or beside it if somebody stands there (riseSpot): never on top of anyone. */
  private reanimate(caster: Unit, corpse: Unit): void {
    const at = riseSpot(this.s, corpse);
    if (!at) return;
    const zombie = risenUnit(this.zombieId(), corpse, at, caster.podId);
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
   * One whose tile is taken rises beside it (riseSpot), or waits for a free one.
   */
  private riseInfected(): void {
    for (const corpse of this.s.units.filter((x) => !x.alive && !x.evacuated && x.infected && !x.reanimated)) {
      if (this.over) return;
      const at = riseSpot(this.s, corpse);
      if (!at) continue;
      const zombie = risenUnit(this.zombieId(), corpse, at, null);
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
    this.emit({ t: 'hacked', unit: u.id, success, chance });
    if (!success) this.raiseAlarm(u);
    else this.checkOutcome();
  }

  // ------------------------------------------------------------- movement

  /**
   * Walks the path tile by tile. Enemy overwatch, pod detection and picking up
   * the objective interrupt the walk, so a move may be emitted as several
   * `moved` segments. Nobody walks through anyone (Arturo, 19/10): one who came
   * into the way since the walk was planned (a pod scampering to cover) stops
   * it short of them, never in a window's frame nor on a flight of stairs
   * (flights.ts: nobody stops there, docs/MAPAS.md phase 2).
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

    const origin = { ...u.pos };
    const patrolling = onPatrol(this.s, u);
    /** Who a patrol walking up or down a flight of stairs saw: it stops once off it. */
    let sighted: Unit | undefined;
    for (let i = 0; i < path.length; i++) {
      const step = path[i]!;
      const blocker = unitAt(this.s, step);
      if (blocker && blocker !== u) {
        flush();
        // Halted in a window's frame or on a flight of stairs, where nobody stands: back the way it came, to the last
        // place it could have stopped on.
        if (u.alive && !canStopAt(this.s, u.pos)) {
          const back: Pos[] = [];
          for (let j = i - 2; j >= -1; j--) {
            const q = j >= 0 ? path[j]! : origin;
            back.push({ ...q });
            if (canStopAt(this.s, q)) break;
          }
          const end = back[back.length - 1];
          if (end && canStopAt(this.s, end) && !unitAt(this.s, end)) this.emit({ t: 'moved', unit: u.id, path: back, ap: 0 });
        }
        return;
      }

      // Doors swing open and windows shatter before the unit steps in (19/10: it walked into the shut door, then the leaf
      // swung through it): the walk so far, the door, then on through it.
      const kind = kindAt(this.s, step);
      if (kind === 'door' || kind === 'window') {
        flush();
        this.emit({ t: 'tileChanged', pos: step, kind: kind === 'door' ? 'doorOpen' : 'windowBroken' });
        this.makeNoise(step, kind === 'door' ? NOISE.door : NOISE.breach, u);
      }
      segment.push(step);

      // A trap of the base under one of the dead (docs/FASE12.md §11.4): it stops there.
      if (u.team === 'alien' && this.springTrap(u, step, flush)) return;

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
      // A patrolling pod that walks into sight of the squad stops and takes cover; on a flight of stairs, once off it.
      const spotted = sighted ?? (patrolling ? this.spottedFrom(u, step, facingTo(i ? path[i - 1]! : u.pos, step) ?? u.facing) : undefined);
      if (spotted && !canStopAt(this.s, step)) sighted = spotted;
      else if (spotted) {
        flush();
        this.activatePods([u.podId!], { cause: 'patrol', unit: spotted.id, by: u.id });
        return;
      }

      // Shadowstep: nobody gets a reaction shot at this unit.
      const shadow = hasPerk(u, 'shadowstep');
      const loaded = (o: Unit) => loadedFor(o, 'shoot');
      // Overwatch with the blade (docs/FASE10.md §4) strikes whoever comes alongside; the gun's, whoever moves in sight.
      const reactors = shadow
        ? []
        : this.s.units.filter(
            (o) =>
              o.alive &&
              hostileTo(o, u) &&
              o.overwatch &&
              !(o.killZone && o.reactedTo.includes(u.id)) &&
              (o.watchMelee ? inMeleeReach(this.s, o.pos, step) : loaded(o) && this.watches(o, step)),
          );
      const suppressors = shadow ? [] : this.s.units.filter((o) => o.alive && hostileTo(o, u) && o.suppressing === u.id && loaded(o));
      const noticing = u.team === 'xcom' ? this.podsNoticing(step) : [];
      if (!reactors.length && !suppressors.length && !noticing.length) continue;

      flush();
      for (const r of reactors) {
        if (!u.alive || this.over) break;
        if (r.alive && r.overwatch) this.attack(r, u, r.watchMelee ? 'slash' : 'shoot', true);
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

  /**
   * One of the dead steps on a trap that is set (docs/FASE12.md §11.4): a mine
   * blows up whoever is round it, the spikes hurt, a snare holds it (one action
   * less next turn). It stops on the tile, if it is still standing; true when
   * the walk ends there.
   */
  private springTrap(u: Unit, step: Pos, flush: () => void): boolean {
    const node = nodeOf(this.s, step);
    const trap = this.s.traps?.find((t) => t.node === node && t.charges > 0);
    if (!trap) return false;
    flush();
    this.emit({ t: 'trapSprung', trap: trap.piece, unit: u.id });
    if (trap.kind === 'mine') {
      this.makeNoise(step, WEAPONS.fragGrenade.noise);
      for (const v of this.s.units.filter((o) => o.alive && distance(o.pos, step) <= TRAPS.mineRadius && sameSpace(this.s, o.pos, step))) this.damage(v, TRAPS.mine, 0);
    } else if (trap.kind === 'spikes') this.damage(u, TRAPS.spikes, 0);
    else if (u.alive) this.emit({ t: 'stunned', unit: u.id });
    this.checkOutcome();
    return true;
  }

  /** Whether an overwatcher covers `step`: with its own sight. */
  private watches(o: Unit, step: Pos): boolean {
    return !!sightOrigin(this.s, o.pos, step, sightTo(this.s, o, step));
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

  /** The first soldier an alien standing on `p` would spot (`facing`: the dead see ahead of them, the way they walk). */
  spottedFrom(u: Unit, p: Pos, facing = u.facing): Unit | undefined {
    return combatants(this.s, 'xcom').find((soldier) => inViewCone(u, soldier.pos, p, facing) && sightOrigin(this.s, p, soldier.pos, this.noticeRange(u, soldier.pos)) !== null);
  }

  /** The member of `pod` that would notice an XCOM soldier standing on `p`, if any. */
  private noticer(pod: Pod, p: Pos): Unit | undefined {
    for (const id of pod.unitIds) {
      const m = getUnit(this.s, id);
      if (!m?.alive) continue;
      if (inViewCone(m, p) && sightOrigin(this.s, m.pos, p, this.noticeRange(m, p)) !== null) return m;
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
    // Woken by the dead (18/10 #47: they fight the living too), not by the squad: it stays hidden.
    if (first.cause !== 'attacked' || first.unit) this.breakConcealment({ reason: EXPOSURE[first.cause], unit: first.unit, by: first.by, ...(seen && spotter ? { range } : {}) });
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
   * away from the squad and the evac zone; a siege's wave, by the middle half
   * of its `side` (a DIRS4 index), off the corners, where it would be by two.
   */
  chooseDropZone(count: number, edge = false, side?: number): Pos[] | null {
    const s = this.s;
    const soldiers = combatants(s, 'xcom');
    if (!soldiers.length) return null;
    const free = (x: number, y: number) =>
      inBounds(s, x, y) && canStopAt(s, { x, y, z: 0 }) && (s.elev[tileIndex(s, x, y)] ?? 0) === 0 && !unitAt(s, { x, y, z: 0 });
    const sides = (x: number, y: number) => [y < EDGE_BAND, x >= s.width - EDGE_BAND, y >= s.height - EDGE_BAND, x < EDGE_BAND];
    const middle = (along: number, span: number) => along >= span / 4 && along < (span * 3) / 4;
    // In a siege, never in a glade the pines close off: only where the way to the squad is open, or opened by breaking what was built.
    const open = edge && s.mission.siege ? this.openToSquad() : null;
    const onEdge = (x: number, y: number) =>
      (side === undefined ? sides(x, y).some(Boolean) : !!sides(x, y)[side] && (side % 2 === 0 ? middle(x, s.width) : middle(y, s.height))) &&
      !s.mission.evacZone.some((z) => distance(z, { x, y }) < EDGE_EVAC_GAP) &&
      (!open || open.has(tileIndex(s, x, y)));
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

  /** The ground tiles joined to a soldier's by ground one can walk, or by what can be broken down (GameState.breakables). */
  private openToSquad(): Set<number> {
    const s = this.s;
    const seen = new Set<number>();
    const queue: Vec2[] = [];
    for (const o of combatants(s, 'xcom')) {
      if (seen.has(tileIndex(s, o.pos.x, o.pos.y))) continue;
      seen.add(tileIndex(s, o.pos.x, o.pos.y));
      queue.push(o.pos);
    }
    while (queue.length) {
      const p = queue.pop()!;
      for (const d of DIRS4) {
        const x = p.x + d.x;
        const y = p.y + d.y;
        const i = tileIndex(s, x, y);
        if (!inBounds(s, x, y) || seen.has(i) || (!canStandOn(tileAt(s, x, y)) && s.breakables?.[i] === undefined)) continue;
        seen.add(i);
        queue.push({ x, y });
      }
    }
    return seen;
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
   * playtest of 2026-10-13) lands where it was announced, already alert. A
   * siege's wave comes in by its side, alert, looking at the soldier nearest
   * (docs/FASE12.md §4): it goes for the soldiers (ai.ts takeZombieTurn).
   */
  landReinforcements(turn: number): void {
    const siege = this.s.mission.siege;
    for (const r of this.s.mission.reinforcements) {
      if (r.landed || (!r.horde && !r.pos) || r.turn > turn || this.over) continue;
      const zone = r.horde ? this.chooseDropZone(r.units.length, true, r.side) : this.chooseDropZoneAround(r.pos!, r.units.length);
      // No edge free today, or no room for all of them round a flare: they come next turn. Nobody lands on anyone.
      if (!zone || zone.length < r.units.length) continue;
      const at = zone[0]!;
      const pod: Pod = { id: `p${r.id}`, active: !r.horde || !!siege, unitIds: [], patrol: [], patrolIndex: 0 };
      const near = r.horde ? [...combatants(this.s, 'xcom')].sort((a, b) => distance(a.pos, at) - distance(b.pos, at))[0] : undefined;
      const goal = r.horde ? near?.pos : undefined;
      const units = r.units.map((template, i) => {
        const id = `${r.id}u${i + 1}`;
        pod.unitIds.push(id);
        const u = makeUnit(id, template, TEMPLATES[template].name, null, zone[i]!, pod.id);
        // They come in looking the way they come: to what they heard, or to the middle of the map (Arte, 17/10).
        const toward = goal ?? { x: (this.s.width - 1) / 2, y: (this.s.height - 1) / 2, z: 0 };
        const facing = facingTo(u.pos, toward) ?? u.facing;
        return { ...u, ...(facing !== undefined ? { facing } : {}), ...(goal && !siege ? { heard: { ...goal } } : {}) };
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
          if (inBounds(s, p.x, p.y) && canStopAt(s, p) && !unitAt(s, p) && !zone.some((z) => samePos(z, p))) zone.push(p);
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

    // A siege (docs/FASE12.md §4): the base falls with nobody left to defend it (turrets don't count); it holds once every wave is in and down.
    if (m.siege) {
      if (!squad.some((u) => !u.automaton)) end('defeat', 'wiped');
      else if (!hostiles.length && m.reinforcements.every((r) => r.landed)) end('victory', 'objective');
      return;
    }
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

  /**
   * The bus leaves: whoever is in it gets out (first), everyone else is lost;
   * then the mission is decided. `timeUp`: it leaves because time ran out
   * (playtest 18/10, #8); without the objective on board, the mission is lost
   * to the clock.
   */
  departEvac(timeUp = false): void {
    this.emit({ t: 'evacDeparted' });
    const squad = this.s.units.filter((o) => o.alive && o.team === 'xcom');
    // The bitten board only with a Cuarentena at home (playtest 2026-10-15, #13).
    const boards = (o: Unit) => inEvacZone(this.s, o.pos) && !(o.infected && this.s.mission.quarantine === false);
    for (const u of squad.filter(boards)) this.emit({ t: 'evacuated', unit: u.id });
    for (const u of squad.filter((o) => !boards(o))) this.emit({ t: 'leftBehind', unit: u.id });
    const m = this.s.mission;
    const won = this.s.units.some((u) => u.team === 'xcom' && u.evacuated) && (m.kind === 'scavenge' || m.objectiveDone);
    if (timeUp && !won) this.emit({ t: 'missionEnded', outcome: 'defeat', reason: 'timer' });
    else this.checkOutcome();
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
    // The base's turrets keep watch through the enemy's turn: each of the dead that moves in sight, once (docs/FASE12.md §11.4).
    for (const t of this.s.units.filter((x) => x.alive && x.automaton && loadedFor(x, 'shoot'))) this.emit({ t: 'overwatch', unit: t.id, killZone: true });
    // They rise now, but only act from the next turn on (they had no turn to begin).
    this.riseInfected();
    const active = this.s.units.filter((u) => u.alive && u.team === 'alien' && !u.objective && this.s.pods.find((p) => p.id === u.podId)?.active);
    // Whoever is going for the squad acts first, one by one; then the dead with nobody to reach, all together (playtest 2026-10-16),
    // the nearest to the living first: those behind follow them, instead of finding the way taken (nobody walks through anyone, 19/10).
    const walkers = frontFirst(this.s, active.filter((u) => onlyWalks(this.s, u)));
    const actors = [...active.filter((u) => !walkers.includes(u)), ...walkers].map((u) => u.id);
    this.jammed.clear();
    for (const id of actors) {
      const u = getUnit(this.s, id)!;
      if (this.over) return;
      if (u.alive) takeAlienTurn(this, u);
    }
    // Those who waited behind their own (nobody walks through anyone, 19/10) go on with what they have left, now that the
    // ones ahead have moved: again while somebody gets further.
    for (let pass = 0; pass < JAM_PASSES && this.jammed.size; pass++) {
      const waiting = actors.filter((id) => this.jammed.has(id));
      this.jammed.clear();
      const before = this.events.length;
      for (const id of waiting) {
        const u = getUnit(this.s, id)!;
        if (this.over) return;
        if (u.alive && u.ap > 0) takeAlienTurn(this, u);
      }
      if (this.events.length === before) break;
    }
    // What the squad did on the enemy's move (overwatch, the blade on guard) fills bars too.
    this.settleBars();
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
    // A siege is held once its last turn is over: those still out there fall back (docs/FASE12.md §4).
    const siege = this.s.mission.siege;
    if (siege && turn >= siege.hold) {
      this.emit({ t: 'missionEnded', outcome: 'victory', reason: 'objective' });
      return;
    }
    const left = this.s.mission.turnsLeft;
    if (left !== null && left - 1 <= 0 && !this.s.mission.objectiveDone) {
      // Time's up: La Lata leaves with whoever is in it (playtest 18/10, #8); the objective on board, it is a win.
      if (this.s.mission.evacZone.length) this.departEvac(true);
      else this.emit({ t: 'missionEnded', outcome: 'defeat', reason: 'timer' });
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
      case 'equip': {
        const loot = u.backpack?.[cmd.index!];
        if (loot?.weapon) this.emit({ t: 'weaponEquipped', unit: u.id, index: cmd.index! });
        else if (loot?.item && wearable(loot.item)) this.emit({ t: 'vestWorn', unit: u.id, index: cmd.index! });
        else this.emit({ t: 'beltEquipped', unit: u.id, index: cmd.index! });
        break;
      }
    }
  }

  /**
   * Bars filled by what was just done (docs/FASE10.md §2): each one rolls its
   * 5–10 % and its stat goes up there and then. After the whole action, so it
   * is seen entire first; a bar the mission's last blow fills rolls at home.
   */
  settleBars(): void {
    if (this.over) return;
    for (const u of this.s.units) {
      if (!u.lines || !u.alive || u.team !== 'xcom') continue;
      for (const bar of pendingBars(u.lines, u.deeds)) this.emit({ t: 'barFilled', unit: u.id, skill: bar.skill, filled: bar.filled, growth: rollGrowth(this.rng) });
    }
  }

  afterXcomAction(): void {
    this.settleBars();
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

/**
 * The zombie a corpse rises as. A soldier's keeps its look (playtest
 * 2026-10-16): `risenFrom` says whose, and the client draws that soldier dead.
 */
function risenUnit(id: string, corpse: Unit, at: Pos, podId: string | null): Unit {
  // It gets up looking where it fell (Arte, 17/10).
  const zombie = { ...makeUnit(id, 'zombie', risenName(corpse), null, at, podId), ...(corpse.facing !== undefined ? { facing: corpse.facing } : {}) };
  if (TEMPLATES[corpse.template].team !== 'xcom' || !corpse.appearance) return zombie;
  return { ...zombie, risenFrom: corpse.id, appearance: { ...corpse.appearance } };
}

/** What the thrower adds to an explosion: damage (grenade talents) and shred. */
function blastMods(u: Unit, ability: BlastAbility): { bonus: number; shred: number } {
  const weapon = WEAPONS[ABILITIES[ability].explosive!];
  const grenade = ability === 'grenade' || ability === 'launch';
  const bonus = grenade ? u.mods.grenadeDamage : 0;
  return { bonus, shred: weapon.shred + (grenade ? u.mods.grenadeShred : 0) };
}

/**
 * What a damaging ability would take off `target` (or, with none, its own
 * numbers, for the ability cards): attacks and explosives. `shot` is the
 * attack's preview against that target (flank and distance). Null for
 * abilities that do no damage.
 */
export function abilityDamage(_s: GameState, u: Unit, ability: AbilityId, target?: Unit, shot?: Pick<ShotPreview, 'flanked' | 'distance'>): DamagePreview | null {
  if (isAttack(ability)) return shotDamage(u, ability, target, shot);
  switch (ability) {
    case 'grenade':
    case 'launch':
    case 'missiles': {
      const { bonus, shred } = blastMods(u, ability);
      return { hit: rangeOn(target, damageRange(WEAPONS[ABILITIES[ability].explosive!], 'hit', bonus)), crit: null, graze: null, armor: target?.armor ?? 0, shred };
    }
  }
  return null;
}

// ------------------------------------------------------------- commands

/** Entre los dos (board.ts): the botiquín does more for the other player's soldiers. */
function bridged(u: Unit, target: Unit): boolean {
  return hasPerk(u, 'bridge') && u.owner !== null && target.owner !== null && u.owner !== target.owner;
}

function commandUnit(s: GameState, id: string, slot: Slot, connected: readonly Slot[]): Unit | string {
  const u = getUnit(s, id);
  if (!u || !u.alive) return 'Esa unidad no existe o está fuera de combate.';
  if (u.team !== 'xcom') return 'No puedes dar órdenes a esa unidad.';
  if (u.automaton) return 'Va sola: nadie le da órdenes.';
  if (!commandsFor(slot, u.owner, connected)) return 'Es de la escuadra del otro jugador.';
  if (u.ap <= 0) return 'Ese soldado no tiene acciones.';
  return u;
}

type ItemCommand = Extract<Command, { type: 'item' }>;

/**
 * Why a backpack order can't go through (docs/FASE8.md §C), or null. Dropping and taking are free, even with no
 * actions left; giving, using a medicine and equipping a weapon take one.
 */
function backpackRefusal(s: GameState, cmd: ItemCommand, slot: Slot, connected: readonly Slot[]): string | null {
  const u = getUnit(s, cmd.unit);
  if (!u || !u.alive) return 'Esa unidad no existe o está fuera de combate.';
  if (u.team !== 'xcom' || u.captive || u.objective || u.automaton) return 'No puedes dar órdenes a esa unidad.';
  if (!commandsFor(slot, u.owner, connected)) return 'Es de la escuadra del otro jugador.';
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
      // A vest found is put on there and then (playtest 16/10 #18); one at a time.
      if (loot.item && wearable(loot.item)) return u.worn?.length ? 'Ya lleva un chaleco puesto.' : null;
      // A belt thing goes on the belt, to use there and then (playtest 18/10 #5); what only gives, once.
      if (loot.item && beltable(loot.item)) return !ITEMS[loot.item].charges && u.gear.includes(loot.item) ? 'Ya lleva eso puesto.' : null;
      if (loot.item) return 'La munición se monta en el arma, en la Armería.';
      if (!loot.weapon) return 'Solo se equipan las armas, los chalecos y lo del cinturón.';
      if (!u.arms) return 'Las armas encontradas son para el Bastión.';
      return null;
  }
}

/** A found accessory that goes on the belt, out there (playtest 18/10 #5): grenades, the medkit, ear plugs; not ammunition, which is the weapon's. */
export function beltable(item: ItemId): boolean {
  const def = ITEMS[item];
  return def.slot === 'utility' && !wearable(item);
}

/** A found accessory worn over the clothes, out there: what only gives health (the Chaleco antibalas). */
export function wearable(item: ItemId): boolean {
  const def = ITEMS[item];
  return !!def.hp && !def.charges && !def.ability;
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
    case 'firstAid':
      return 'No está herido.';
    case 'revival':
      return 'No está aturdido, desorientado ni en pánico.';
    case 'patchUp':
      return target.arms?.primary ? 'Su arma está como nueva.' : 'No lleva arma que arreglar.';
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
    if (!commandsFor(ctx.slot, u.owner, ctx.connected)) return { ok: false, error: 'Es de la escuadra del otro jugador.' };
    if (!inEvacZone(s, u.pos)) return { ok: false, error: 'Solo desde el autobús, en la zona de evacuación.' };
    if (s.evacCall?.by === ctx.slot) return { ok: false, error: 'Ya has pedido evacuar: falta que conteste el otro jugador.' };
    if (u.infected && s.mission.quarantine === false) return { ok: false, error: 'Con un mordisco no se sube a La Lata sin una Cuarentena en el Bastión.' };
    sim.callEvac(ctx.slot, u);
    return { ok: true, events: sim.events };
  }

  if (cmd.type === 'pass') {
    if (!holds) return { ok: false, error: 'No tienes el mando.' };
    if (!ctx.connected.includes(partner)) return { ok: false, error: 'El otro jugador no está conectado.' };
    if (s.ready[partner]) return { ok: false, error: 'El otro jugador ya ha terminado el turno.' };
    if (!sim.hasActions(partner)) return { ok: false, error: 'Al otro jugador no le quedan acciones.' };
    sim.passCommand(partner);
    return { ok: true, events: sim.events };
  }

  if (!holds) return { ok: false, error: 'Tiene el mando el otro jugador: espera a que te lo ceda.' };
  // Turns go one soldier at a time (playtest 2026-10-15, #9): the one in play is finished before another of yours.
  const inPlay = (id: string): string | null => {
    const busy = s.activeUnit && s.activeUnit !== id ? getUnit(s, s.activeUnit) : undefined;
    return busy && busy.alive && busy.ap > 0 && busy.owner === ctx.slot && sim.alternates(partner) ? `Primero termina con ${busy.name}, o cede el turno.` : null;
  };
  if (cmd.type === 'item') {
    const refusal = backpackRefusal(s, cmd, ctx.slot, ctx.connected);
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
  const u = commandUnit(s, cmd.unit, ctx.slot, ctx.connected);
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

/**
 * `slot` wants to abandon the mission (playtest 16/10): with the partner
 * here it is asked first, as the bus is; alone, or the partner having asked
 * already, it ends now.
 */
export function callAbandon(s: GameState, slot: Slot, connected: readonly Slot[]): GameEvent[] {
  if (s.outcome) return [];
  const partner: Slot = slot === 0 ? 1 : 0;
  if (!connected.includes(partner) || s.abandonCall?.by === partner) return abandonMission(s);
  if (s.abandonCall?.by === slot) return [];
  const e: GameEvent = { t: 'abandonCalled', slot };
  applyEvent(s, e);
  return [e];
}

/** The partner answers a call to abandon: yes ends the mission, no keeps it going. */
export function answerAbandon(s: GameState, slot: Slot, accept: boolean): GameEvent[] {
  if (s.outcome || !s.abandonCall || s.abandonCall.by === slot) return [];
  if (accept) return abandonMission(s);
  const e: GameEvent = { t: 'abandonRefused', slot };
  applyEvent(s, e);
  return [e];
}
