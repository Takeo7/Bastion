import {
  abilityBlocker,
  abilityTargets,
  apNeeded,
  blastTiles,
  canTargetTile,
  endsTurn,
  hackChance,
  hasBadStatus,
  meleeTargets,
  psiChance,
  rangedTargets,
  type MeleeOption,
} from './abilities';
import { chooseScamperTile, patrolPod, takeAlienTurn } from './ai';
import { adjacent, inMeleeReach, previewShot, rollDamage, rollShot, shotDamageMods } from './aim';
import {
  ABILITIES,
  AID_DEFENSE,
  attackWeapon,
  EXPLOSION_ALERT_RADIUS,
  hasPerk,
  HOLO_AIM,
  isAttack,
  MEDKIT_HEAL,
  RESTORATION_HEAL,
  RUPTURE_DAMAGE,
  SMOKE_TURNS,
  TEMPLATES,
  WEAPONS,
} from './content';
import { applyEvent, type GameEvent } from './events';
import { canStandOn, combatants, distance, elevAt, getUnit, inBounds, sameTile, tileAt, tileIndex, unitAt } from './grid';
import { canSee, isViewer, sightOrigin } from './los';
import { apForCost, buildPath, computeReach, costPerAp, maxMoveCost } from './path';
import type { Command } from './protocol';
import type { Rng } from './rng';
import { makeUnit } from './setup';
import type {
  AbilityId,
  AttackAbility,
  BlastAbility,
  GameState,
  Pod,
  PsiEffect,
  Reinforcement,
  ShotResult,
  Slot,
  TemplateId,
  TileKind,
  Unit,
  Vec2,
} from './types';

/** Reinforcements drop this far (in tiles) from the nearest soldier. */
const DROP_MIN = 7;
const DROP_MAX = 13;

/** What an explosion leaves of each kind of tile (null: unaffected). */
const BLAST_RESULT: Partial<Record<TileKind, TileKind>> = {
  low: 'floor',
  high: 'low',
  door: 'doorOpen',
  window: 'windowBroken',
};

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
  perform(u: Unit, ability: AbilityId, args: { target?: Unit; tile?: Vec2; melee?: MeleeOption } = {}): void {
    const { target, tile, melee } = args;
    this.pay(u, ability);
    const loaded = () => attackWeapon(u, 'shoot').clip === 0 || u.ammo > 0;
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
      case 'suppression':
        this.emit({ t: 'suppressed', unit: u.id, target: target!.id });
        if (u.team === 'xcom') this.breakConcealment();
        this.activatePodOf(target!);
        break;
      case 'medkit':
        this.emit({ t: 'healed', unit: u.id, target: target!.id, amount: Math.min(MEDKIT_HEAL + u.mods.heal, target!.maxHp - target!.hp) });
        break;
      case 'aid':
        this.emit({ t: 'aided', unit: u.id, target: target!.id, amount: AID_DEFENSE + u.mods.aid });
        if (hasPerk(u, 'threatAssessment') && !target!.overwatch && (attackWeapon(target!, 'shoot').clip === 0 || target!.ammo > 0)) {
          this.emit({ t: 'overwatch', unit: target!.id, killZone: false });
        }
        break;
      case 'revival':
        this.emit({ t: 'cleansed', unit: u.id, target: target!.id });
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
        this.emit({ t: 'healed', unit: u.id, target: target!.id, amount: Math.min(MEDKIT_HEAL, target!.maxHp - target!.hp) });
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
      case 'evac':
        this.emit({ t: 'evacuated', unit: u.id });
        break;
      case 'hack':
        this.hack(u);
        break;
      case 'runAndGun':
        break;
      case 'mindspin':
        this.psionic(u, target!);
        break;
      case 'reanimate':
        this.reanimate(u, target!);
        break;
    }
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
    const result = rollShot(preview, this.rng);
    this.emit({
      t: 'shot',
      unit: attacker.id,
      target: target.id,
      ability,
      from: preview.origin,
      result,
      chance: preview.hit,
      reaction,
    });
    const weapon = attackWeapon(attacker, ability);
    const primary = (ABILITIES[ability].weapon ?? 'primary') === 'primary' && !weapon.melee;
    if (primary && hasPerk(attacker, 'holoTargeting') && target.alive) this.emit({ t: 'marked', unit: target.id, amount: HOLO_AIM });
    if (result !== 'miss') {
      const dm = shotDamageMods(attacker, ability, preview.flanked);
      this.damage(target, rollDamage(weapon, result, this.rng, dm.bonus, dm.mult), dm.shred, attacker, dm.pierce);
      if (ability === 'rupture' && target.alive) this.emit({ t: 'ruptured', unit: target.id, amount: RUPTURE_DAMAGE });
      if (!target.alive && !reaction) this.afterKill(attacker, target, ability, preview.origin);
    }
    if (attacker.team === 'xcom' && !attacker.controlledBy) this.breakConcealment();
    if (target.team === 'alien') this.activatePodOf(target);
    this.checkOutcome();
    return result;
  }

  /** Implacable and Death From Above give an action back after a kill, once per turn. */
  private afterKill(killer: Unit, victim: Unit, ability: AttackAbility, from: Vec2): void {
    if (killer.refunded || !killer.alive) return;
    const melee = attackWeapon(killer, ability).melee;
    if (melee && hasPerk(killer, 'implacable')) this.emit({ t: 'refund', unit: killer.id, reason: 'implacable' });
    else if (!melee && hasPerk(killer, 'deathFromAbove') && elevAt(this.s, from.x, from.y) > elevAt(this.s, victim.pos.x, victim.pos.y)) {
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
        (attackWeapon(o, 'shoot').clip === 0 || o.ammo > 0) &&
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
    const total = raw + u.ruptured;
    const mitigated = Math.min(Math.max(0, u.armor - pierce), total);
    this.emit({ t: 'damaged', unit: u.id, amount: total - mitigated, mitigated, shred: Math.min(shred, u.armor) });
    if (u.hp > 0) return;
    if (this.s.mission.item?.carrier === u.id) this.emit({ t: 'itemDropped', pos: { ...u.pos } });
    this.emit({ t: 'died', unit: u.id, by: source?.id });
    // A sectoid's death frees the minds it held.
    for (const v of this.s.units) if (v.alive && v.controlledBy === u.id) this.emit({ t: 'released', unit: v.id });
  }

  meleeAttack(u: Unit, option: MeleeOption, ability: AttackAbility): void {
    if (option.path.length) this.moveAlong(u, option.path, 0);
    const target = getUnit(this.s, option.target.id);
    if (!u.alive || !target?.alive || this.over || !inMeleeReach(this.s, u.pos, target.pos)) return;
    const result = this.attack(u, target, ability);
    if (ability === 'stunLance' && result && result !== 'miss' && target.alive) this.emit({ t: 'stunned', unit: target.id });
  }

  explode(source: Unit, center: Vec2, ability: BlastAbility): void {
    this.coveringFire(source);
    if (!source.alive || this.over) return;
    const weapon = WEAPONS[ABILITIES[ability].explosive!];
    this.emit({ t: 'explosive', unit: source.id, ability, target: center });
    const tiles = blastTiles(this.s, ability, center, source);
    const victims = tiles.map((p) => unitAt(this.s, p)).filter((u): u is Unit => !!u);
    const bonus = ability === 'grenade' || ability === 'launch' ? source.mods.grenadeDamage : 0;
    for (const v of victims) this.damage(v, rollDamage(weapon, 'hit', this.rng, bonus), weapon.shred, source);
    // The drone's discharge is pure electricity: it leaves the scenery alone.
    if (ability !== 'discharge') {
      for (const p of tiles) {
        const next = BLAST_RESULT[tileAt(this.s, p.x, p.y)];
        if (next) this.emit({ t: 'tileChanged', pos: p, kind: next });
      }
    }
    if (source.team === 'xcom') this.breakConcealment();
    const alerted = new Set<string>();
    for (const v of victims) if (v.podId) alerted.add(v.podId);
    for (const pod of this.s.pods) {
      if (pod.active) continue;
      const near = pod.unitIds.some((id) => {
        const m = getUnit(this.s, id);
        return m?.alive && distance(m.pos, center) <= EXPLOSION_ALERT_RADIUS;
      });
      if (near) alerted.add(pod.id);
    }
    this.activatePods([...alerted]);
    this.checkOutcome();
  }

  /** Disorients every enemy in the area; no damage, the scenery is untouched. */
  private flashbang(u: Unit, center: Vec2): void {
    this.emit({ t: 'explosive', unit: u.id, ability: 'flashbang', target: center });
    const hit = blastTiles(this.s, 'flashbang', center, u)
      .map((p) => unitAt(this.s, p))
      .filter((v): v is Unit => !!v && v.team !== u.team && !TEMPLATES[v.template].robotic && !v.objective);
    for (const v of hit) this.emit({ t: 'disoriented', unit: v.id });
    if (u.team === 'xcom') this.breakConcealment();
    this.activatePods([...new Set(hit.map((v) => v.podId).filter((p): p is string => !!p))]);
  }

  /** Combat Protocol: never misses, ignores armour, hurts machines more. */
  private zap(u: Unit, target: Unit): void {
    this.emit({ t: 'zapped', unit: u.id, target: target.id });
    const dmg = WEAPONS.droneZap.damage + (TEMPLATES[target.template].robotic ? 2 : 0);
    this.damage(target, dmg, 0, u, Infinity);
    if (u.team === 'xcom') this.breakConcealment();
    if (target.team === 'alien') this.activatePodOf(target);
    this.checkOutcome();
  }

  private restoration(u: Unit): void {
    const range = ABILITIES.restoration.range ?? 0;
    for (const a of this.s.units) {
      if (!a.alive || a.team !== u.team || a.captive || distance(a.pos, u.pos) > range) continue;
      const heal = Math.min(RESTORATION_HEAL, a.maxHp - a.hp);
      if (heal > 0) this.emit({ t: 'healed', unit: u.id, target: a.id, amount: heal });
      if (hasBadStatus(a)) this.emit({ t: 'cleansed', unit: u.id, target: a.id });
    }
  }

  /** Mindspin: will resists; a hit disorients, panics or (rarely) takes control. */
  private psionic(caster: Unit, target: Unit): void {
    this.coveringFire(caster);
    if (!caster.alive || !target.alive || this.over) return;
    const chance = psiChance(target);
    const success = this.rng.int(100) < chance;
    let effect: PsiEffect | null = null;
    if (success) {
      const roll = this.rng.int(100);
      effect = roll < 50 ? 'disoriented' : roll < 80 ? 'panicked' : 'controlled';
    }
    this.emit({ t: 'psi', unit: caster.id, target: target.id, success, effect, chance });
    this.checkOutcome();
  }

  private reanimate(caster: Unit, corpse: Unit): void {
    const n = this.s.units.filter((x) => x.template === 'zombie').length + 1;
    const zombie = makeUnit(`z${n}`, 'zombie', TEMPLATES.zombie.name, null, corpse.pos, caster.podId);
    this.emit({ t: 'reanimated', unit: caster.id, corpse: corpse.id, zombie });
  }

  private hack(u: Unit): void {
    const chance = hackChance(this.s, u);
    const success = this.rng.int(100) < chance;
    this.emit({ t: 'hacked', unit: u.id, success, chance, remote: !adjacent(u.pos, this.s.mission.terminal!) });
    if (!success) this.raiseAlarm(alarmSquad(this.s));
    else this.checkOutcome();
  }

  // ------------------------------------------------------------- movement

  /**
   * Walks the path tile by tile. Enemy overwatch, pod detection and picking up
   * the objective interrupt the walk, so a move may be emitted as several
   * `moved` segments.
   */
  moveAlong(u: Unit, path: Vec2[], ap: number): void {
    let segment: Vec2[] = [];
    let pendingAp = ap;
    const flush = () => {
      if (!segment.length) return;
      this.emit({ t: 'moved', unit: u.id, path: segment, ap: pendingAp });
      pendingAp = 0;
      segment = [];
    };

    const patrolling = u.team === 'alien' && !!u.podId && !this.s.pods.find((p) => p.id === u.podId)?.active;
    for (let i = 0; i < path.length; i++) {
      const step = path[i]!;
      const blocker = unitAt(this.s, step);
      if (blocker && blocker !== u && (blocker.team !== u.team || i === path.length - 1)) break;
      segment.push(step);

      // Doors swing open and windows shatter as the unit goes through.
      const kind = tileAt(this.s, step.x, step.y);
      if (kind === 'door' || kind === 'window') {
        flush();
        this.emit({ t: 'tileChanged', pos: step, kind: kind === 'door' ? 'doorOpen' : 'windowBroken' });
      }
      if (blocker) continue; // passing through an ally: nothing can interrupt here

      const item = this.s.mission.item;
      if (u.team === 'xcom' && item && !item.carrier && item.pos && sameTile(item.pos, step)) {
        flush();
        this.emit({ t: 'itemPicked', unit: u.id });
      }
      if (u.team === 'xcom' && !u.captive) {
        const vip = this.s.units.find((o) => o.alive && o.captive && adjacent(o.pos, step));
        if (vip) {
          flush();
          this.emit({ t: 'vipRescued', unit: vip.id, by: u.id, owner: u.owner });
        }
      }
      // A patrolling pod that walks into sight of the squad stops and takes cover.
      if (patrolling && this.podSpotsSquad(u, step)) {
        flush();
        this.activatePods([u.podId!]);
        return;
      }

      // Shadowstep: nobody gets a reaction shot at this unit.
      const shadow = hasPerk(u, 'shadowstep');
      const loaded = (o: Unit) => attackWeapon(o, 'shoot').clip === 0 || o.ammo > 0;
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
      if (noticing.length && !this.over) this.activatePods(noticing);
      if (!u.alive || this.over) return;
    }
    flush();
  }

  /** Whether an overwatcher covers `step`: own sight, or the squad's with Long Watch. */
  private watches(o: Unit, step: Vec2): boolean {
    const sight = TEMPLATES[o.template].sight;
    if (sightOrigin(this.s, o.pos, step, sight)) return true;
    if (!hasPerk(o, 'longWatch') || !TEMPLATES[o.template].squadsight) return false;
    const spotted = this.s.units.some((v) => isViewer(v, o.team) && sightOrigin(this.s, v.pos, step, TEMPLATES[v.template].sight));
    return spotted && sightOrigin(this.s, o.pos, step, Infinity) !== null;
  }

  // ------------------------------------------------------- pods & stealth

  /** Whether a patrolling alien standing on `p` spots any soldier (detection range while the squad is concealed). */
  podSpotsSquad(u: Unit, p: Vec2): boolean {
    const t = TEMPLATES[u.template];
    const range = this.s.concealed ? t.detection : t.sight;
    return combatants(this.s, 'xcom').some((soldier) => sightOrigin(this.s, p, soldier.pos, range) !== null);
  }

  /** Inactive pods that would notice an XCOM soldier standing on `p`. */
  podsNoticing(p: Vec2): string[] {
    const found: string[] = [];
    for (const pod of this.s.pods) {
      if (pod.active) continue;
      const notices = pod.unitIds.some((id) => {
        const m = getUnit(this.s, id);
        if (!m?.alive) return false;
        const t = TEMPLATES[m.template];
        return sightOrigin(this.s, m.pos, p, this.s.concealed ? t.detection : t.sight) !== null;
      });
      if (notices) found.push(pod.id);
    }
    return found;
  }

  breakConcealment(): void {
    if (this.s.concealed) this.emit({ t: 'concealmentBroken' });
  }

  activatePodOf(u: Unit): void {
    if (u.podId) this.activatePods([u.podId]);
  }

  /** Activates pods and lets every member "scamper" to cover, XCOM 2 style. */
  activatePods(podIds: string[]): void {
    const pods = this.s.pods.filter((p) => podIds.includes(p.id) && !p.active);
    if (!pods.length) return;
    for (const pod of pods) this.emit({ t: 'podActivated', pod: pod.id });
    this.breakConcealment();
    for (const pod of pods) this.scamper(pod.unitIds);
  }

  /** Free move to the best nearby cover, without spending actions. */
  scamper(unitIds: string[]): void {
    for (const id of unitIds) {
      const m = getUnit(this.s, id);
      if (!m?.alive || this.over) continue;
      const dest = chooseScamperTile(this.s, m);
      if (!dest) continue;
      const path = buildPath(this.s, m, computeReach(this.s, m, costPerAp(m)), dest);
      if (path) this.moveAlong(m, path, 0);
    }
  }

  // -------------------------------------------------------- reinforcements

  /** Picks open tiles for a drop: not too close to the squad, not too far, on the ground. */
  chooseDropZone(count: number): Vec2[] | null {
    const s = this.s;
    const soldiers = combatants(s, 'xcom');
    if (!soldiers.length) return null;
    const free = (x: number, y: number) =>
      inBounds(s, x, y) && canStandOn(tileAt(s, x, y)) && (s.elev[tileIndex(s, x, y)] ?? 0) === 0 && !unitAt(s, { x, y });
    for (const [lo, hi] of [[DROP_MIN, DROP_MAX], [4, 20], [2, 60]] as const) {
      const candidates: Vec2[] = [];
      for (let y = 0; y < s.height; y++) {
        for (let x = 0; x < s.width; x++) {
          if (!free(x, y)) continue;
          const d = Math.min(...soldiers.map((o) => distance(o.pos, { x, y })));
          if (d >= lo && d <= hi) candidates.push({ x, y });
        }
      }
      while (candidates.length) {
        const anchor = candidates.splice(this.rng.int(candidates.length), 1)[0]!;
        const zone = [anchor];
        for (let r = 1; r <= 2 && zone.length < count; r++) {
          for (let dy = -r; dy <= r && zone.length < count; dy++) {
            for (let dx = -r; dx <= r && zone.length < count; dx++) {
              const p = { x: anchor.x + dx, y: anchor.y + dy };
              if (free(p.x, p.y) && !zone.some((z) => sameTile(z, p))) zone.push(p);
            }
          }
        }
        if (zone.length >= count) return zone;
      }
    }
    return null;
  }

  /** Raises the flare for every reinforcement due at the end of alien turn `turn`. */
  announceReinforcements(turn: number): void {
    for (const r of this.s.mission.reinforcements) {
      if (r.landed || r.pos || r.turn !== turn) continue;
      const zone = this.chooseDropZone(r.units.length);
      if (zone) this.emit({ t: 'reinforcementsIncoming', r: { ...r, pos: zone[0]! } });
    }
  }

  /** The alarm went off (failed hack): a drop at the end of this alien turn. */
  raiseAlarm(units: TemplateId[]): void {
    this.breakConcealment();
    const zone = this.chooseDropZone(units.length);
    if (!zone) return;
    const id = `r${this.s.mission.reinforcements.length + 1}`;
    const r: Reinforcement = { id, turn: this.s.turn, pos: zone[0]!, units, landed: false };
    this.emit({ t: 'reinforcementsIncoming', r });
  }

  /** Drops every announced reinforcement that is due, already alert. */
  landReinforcements(turn: number): void {
    for (const r of this.s.mission.reinforcements) {
      if (r.landed || !r.pos || r.turn > turn || this.over) continue;
      const zone = this.chooseDropZoneAround(r.pos, r.units.length);
      const pod: Pod = { id: `p${r.id}`, active: true, unitIds: [], patrol: [], patrolIndex: 0 };
      const units = r.units.map((template, i) => {
        const id = `${r.id}u${i + 1}`;
        pod.unitIds.push(id);
        return makeUnit(id, template, TEMPLATES[template].name, null, zone[i] ?? r.pos!, pod.id);
      });
      this.emit({ t: 'spawned', units, pod, reinforcement: r.id });
      this.breakConcealment();
      this.scamper(pod.unitIds);
    }
  }

  /** Free tiles around a landing point (the flare tile first). */
  private chooseDropZoneAround(center: Vec2, count: number): Vec2[] {
    const s = this.s;
    const zone: Vec2[] = [];
    for (let r = 0; r <= 3 && zone.length < count; r++) {
      for (let dy = -r; dy <= r && zone.length < count; dy++) {
        for (let dx = -r; dx <= r && zone.length < count; dx++) {
          if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
          const p = { x: center.x + dx, y: center.y + dy };
          if (inBounds(s, p.x, p.y) && canStandOn(tileAt(s, p.x, p.y)) && !unitAt(s, p) && !zone.some((z) => sameTile(z, p))) zone.push(p);
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
    const end = (outcome: 'victory' | 'defeat', reason: 'objective' | 'wiped' | 'objectiveLost') =>
      this.emit({ t: 'missionEnded', outcome, reason });

    if (m.kind === 'elimination') {
      if (!hostiles.length) end('victory', 'objective');
      else if (!squad.length) end('defeat', 'wiped');
      return;
    }
    const target = m.objectiveUnit ? getUnit(s, m.objectiveUnit) : undefined;
    if (m.kind === 'rescue' && target && !target.alive && !target.evacuated) {
      end('defeat', 'objectiveLost');
      return;
    }
    const secured =
      m.objectiveDone ||
      (!hostiles.length && m.kind === 'recovery' && !!m.item?.carrier) ||
      (!hostiles.length && m.kind === 'rescue' && !!target?.alive && !target.captive);
    const someoneOut = s.units.some((u) => u.team === 'xcom' && u.evacuated);
    if (secured && (!hostiles.length || (!squad.length && someoneOut))) end('victory', 'objective');
    else if (!squad.length) end('defeat', 'wiped');
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
    const actors = this.s.units
      .filter((u) => u.alive && u.team === 'alien' && !u.objective && this.s.pods.find((p) => p.id === u.podId)?.active)
      .map((u) => u.id);
    for (const id of actors) {
      const u = getUnit(this.s, id)!;
      if (this.over) return;
      if (u.alive) takeAlienTurn(this, u);
    }
    for (const pod of this.s.pods.filter((p) => !p.active && p.patrol.length)) {
      if (this.over) return;
      patrolPod(this, pod);
    }
    if (this.over) return;
    this.landReinforcements(turn);
    if (this.over) return;
    this.announceReinforcements(turn + 1);
    // Mind control wears off with time.
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
    this.emit({ t: 'turnBegan', team: 'xcom', turn: turn + 1, command: this.openingCommander(turn + 1) });
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

  afterXcomAction(): void {
    if (this.over) return;
    for (const u of combatants(this.s, 'xcom')) {
      const noticing = this.podsNoticing(u.pos);
      if (noticing.length) this.activatePods(noticing);
      if (this.over) return;
    }
    this.checkOutcome();
    this.maybeEndXcomTurn();
    this.autoPassCommand();
  }
}

/** Tiles within `radius` of `center` (kept for UI previews of the basic grenade). */
export function tilesInRadius(s: GameState, center: Vec2, radius: number): Vec2[] {
  const out: Vec2[] = [];
  const r = Math.ceil(radius);
  for (let y = center.y - r; y <= center.y + r; y++) {
    for (let x = center.x - r; x <= center.x + r; x++) {
      if (inBounds(s, x, y) && distance(center, { x, y }) <= radius) out.push({ x, y });
    }
  }
  return out;
}

// ------------------------------------------------------------- commands

function commandUnit(s: GameState, id: string, slot: Slot): Unit | string {
  const u = getUnit(s, id);
  if (!u || !u.alive) return 'Esa unidad no existe o está fuera de combate.';
  if (u.team !== 'xcom') return 'No puedes dar órdenes a esa unidad.';
  if (u.owner !== null && u.owner !== slot) return 'Ese soldado pertenece a tu compañero.';
  if (u.ap <= 0) return 'Ese soldado no tiene acciones.';
  return u;
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

  if (cmd.type === 'pass') {
    if (!holds) return { ok: false, error: 'No tienes el mando.' };
    if (!ctx.connected.includes(partner)) return { ok: false, error: 'Tu compañero no está conectado.' };
    if (s.ready[partner]) return { ok: false, error: 'Tu compañero ya ha terminado el turno.' };
    sim.passCommand(partner);
    return { ok: true, events: sim.events };
  }

  if (!holds) return { ok: false, error: 'Tiene el mando tu compañero: espera a que te lo ceda.' };
  const u = commandUnit(s, cmd.unit, ctx.slot);
  if (typeof u === 'string') return { ok: false, error: u };
  sim.passCommand(ctx.slot);

  if (cmd.type === 'move') {
    const reach = computeReach(s, u, maxMoveCost(u));
    const path = buildPath(s, u, reach, cmd.to);
    if (!path) return { ok: false, error: 'No puede llegar ahí.' };
    const cost = reach.cost.get(tileIndex(s, cmd.to.x, cmd.to.y))!;
    const ap = apForCost(u, cost);
    if (ap > u.ap) return { ok: false, error: 'No le quedan acciones para llegar ahí.' };
    sim.moveAlong(u, path, ap);
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
        return { ok: false, error: def.target === 'ally' ? 'Aliado no válido.' : 'Objetivo fuera de alcance o sin línea de visión.' };
      }
      sim.perform(u, cmd.ability, { target });
      break;
    case 'melee': {
      const option = isAttack(cmd.ability) ? meleeTargets(s, u, cmd.ability).find((o) => o.target.id === cmd.target) : undefined;
      if (!option) return { ok: false, error: 'No puede alcanzar a ese enemigo este turno.' };
      sim.perform(u, cmd.ability, { melee: option });
      break;
    }
    case 'tile':
      if (!cmd.tile || !canTargetTile(s, u, cmd.ability, cmd.tile)) return { ok: false, error: 'Fuera de alcance o fuera de la vista de la escuadra.' };
      sim.perform(u, cmd.ability, { tile: cmd.tile });
      break;
    case 'self':
      sim.perform(u, cmd.ability);
      break;
  }

  sim.afterXcomAction();
  return { ok: true, events: sim.events };
}

/** Who answers a failed hack: stronger pods later in the mission. */
function alarmSquad(s: GameState): TemplateId[] {
  return s.turn >= 6 ? ['officer', 'trooper', 'lancer'] : ['trooper', 'trooper'];
}

/** Re-evaluates the end of turn, e.g. after a player disconnects. */
export function syncTurn(s: GameState, ctx: Omit<CommandContext, 'slot'>): GameEvent[] {
  const sim = new Sim(s, ctx.rng, ctx.connected);
  sim.maybeEndXcomTurn();
  // A holder who left hands the command to whoever is still here.
  const holder = s.command;
  const here = ctx.connected[0];
  if (s.activeTeam === 'xcom' && !s.outcome && here !== undefined && (holder === undefined || !ctx.connected.includes(holder))) sim.passCommand(here);
  return sim.events;
}

/** Ends the mission early (either player can abandon). */
export function abandonMission(s: GameState): GameEvent[] {
  if (s.outcome) return [];
  const e: GameEvent = { t: 'missionEnded', outcome: 'defeat', reason: 'abandoned' };
  applyEvent(s, e);
  return [e];
}
