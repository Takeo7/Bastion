// Hit chance follows XCOM 2's X2AbilityToHitCalc_StandardAim:
// aim + weapon aim + range − defense − cover, reaction shots lose 30 %,
// flanking adds crit, crit and graze are carved out of the hit band.
// Melee ignores cover and range; squadsight shots lose aim beyond sight range.
import {
  ABILITIES,
  attackWeapon,
  DISORIENTED_AIM,
  GRAZE_DAMAGE_MULT,
  HEIGHT_ADVANTAGE_AIM,
  HIGH_COVER_BONUS,
  HUNKER_DEFENSE,
  HUNKER_DODGE,
  LOW_COVER_BONUS,
  REACTION_PENALTY,
  rangeModifier,
  SQUADSIGHT_CRIT_MOD,
  SMOKE_DEFENSE,
  SQUADSIGHT_DISTANCE_MOD,
  SUPPRESSED_AIM,
  TEMPLATES,
  type WeaponDef,
} from './content';
import { coverAgainst } from './cover';
import { distance, elevAt } from './grid';
import { canSee, isViewer, sightOrigin } from './los';
import type { Rng } from './rng';
import type { AttackAbility, CoverLevel, GameState, ShotResult, Unit, Vec2 } from './types';

export interface Modifier {
  label: string;
  value: number;
}

export interface ShotPreview {
  /** Tile the attacker strikes from (own tile, step-out tile or melee position). */
  origin: Vec2;
  hit: number;
  crit: number;
  graze: number;
  flanked: boolean;
  cover: CoverLevel;
  distance: number;
  squadsight: boolean;
  mods: Modifier[];
  critMods: Modifier[];
}

export interface ShotOptions {
  ability?: AttackAbility;
  reaction?: boolean;
  /** Evaluate as if the attacker stood here (AI planning, melee approach). */
  from?: Vec2;
}

const clamp = (v: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, v));

/** Melee reach: the eight surrounding tiles. */
export function adjacent(a: Vec2, b: Vec2): boolean {
  return Math.max(Math.abs(a.x - b.x), Math.abs(a.y - b.y)) === 1;
}

/** Adjacent and on the same level: a blade can't reach someone on the roof above. */
export function inMeleeReach(s: GameState, a: Vec2, b: Vec2): boolean {
  return adjacent(a, b) && elevAt(s, a.x, a.y) === elevAt(s, b.x, b.y);
}

/** Whether any living member of `team` can see `target`. */
export function teamSees(s: GameState, team: Unit['team'], target: Unit): boolean {
  return s.units.some((u) => isViewer(u, team) && canSee(s, u, target));
}

/** Null when the attack is impossible (no line of sight, or not adjacent for melee). */
export function previewShot(s: GameState, shooter: Unit, target: Unit, opts: ShotOptions = {}): ShotPreview | null {
  const ability = opts.ability ?? 'shoot';
  const from = opts.from ?? shooter.pos;
  const st = TEMPLATES[shooter.template];
  const tt = TEMPLATES[target.template];
  const weapon = attackWeapon(shooter, ability);

  const def = ABILITIES[ability];
  const primary = (def.weapon ?? 'primary') === 'primary';
  let origin: Vec2 | null;
  let squadsight = false;
  if (weapon.melee) {
    origin = inMeleeReach(s, from, target.pos) ? from : null;
  } else {
    origin = sightOrigin(s, from, target.pos, st.sight);
    if (!origin && primary && st.squadsight && teamSees(s, shooter.team, target)) {
      origin = sightOrigin(s, from, target.pos, Infinity);
      squadsight = origin !== null;
    }
  }
  if (!origin) return null;

  const dist = distance(origin, target.pos);
  const cover = weapon.melee ? 0 : coverAgainst(s, target.pos, origin);
  const flanked = !weapon.melee && cover === 0;

  const sm = shooter.mods;
  const mods: Modifier[] = [{ label: 'Puntería', value: st.aim + sm.aim }];
  if (weapon.aim) mods.push({ label: weapon.melee ? 'Arma cuerpo a cuerpo' : 'Arma', value: weapon.aim });
  if (def.aim) mods.push({ label: def.name, value: def.aim });
  if (weapon.melee && sm.meleeAim) mods.push({ label: 'Maestro de la espada', value: sm.meleeAim });
  if (shooter.disoriented) mods.push({ label: 'Desorientado', value: -DISORIENTED_AIM });
  if (shooter.suppressedBy) mods.push({ label: 'Suprimido', value: -SUPPRESSED_AIM });
  if (target.marked && shooter.team !== target.team) mods.push({ label: 'Holo-objetivo', value: target.marked });
  if (!weapon.melee) {
    const range = rangeModifier(weapon.range, dist);
    if (range) mods.push({ label: 'Alcance', value: range });
  }
  if (!weapon.melee && elevAt(s, origin.x, origin.y) > elevAt(s, target.pos.x, target.pos.y)) {
    mods.push({ label: 'Ventaja de altura', value: HEIGHT_ADVANTAGE_AIM });
  }
  if (def.weapon === 'secondary' && sm.pistolAim) mods.push({ label: 'Pistolero', value: sm.pistolAim });
  const close = !weapon.melee && dist <= 4;
  if (close && sm.closeRangeAim) mods.push({ label: 'Quemarropa', value: sm.closeRangeAim });
  if (squadsight) {
    const beyond = Math.max(0, Math.floor(dist - st.sight));
    if (beyond) mods.push({ label: 'Visión de escuadra', value: SQUADSIGHT_DISTANCE_MOD * beyond });
  }
  const defense = tt.defense + target.mods.defense;
  if (defense) mods.push({ label: 'Defensa', value: -defense });
  if (cover === 1) mods.push({ label: 'Cobertura baja', value: -LOW_COVER_BONUS });
  if (cover === 2) mods.push({ label: 'Cobertura alta', value: -HIGH_COVER_BONUS });
  if (target.hunkered) mods.push({ label: 'Agazapado', value: -HUNKER_DEFENSE });
  if (target.aided) mods.push({ label: 'Protocolo de ayuda', value: -target.aided });
  if (!weapon.melee && s.smoke.some((c) => distance(c.pos, target.pos) <= c.radius)) mods.push({ label: 'Humo', value: -SMOKE_DEFENSE });

  let hit = mods.reduce((sum, m) => sum + m.value, 0);
  if (opts.reaction) {
    const penalty = -Math.floor(Math.max(hit, 0) * REACTION_PENALTY);
    if (penalty) mods.push({ label: 'Fuego de reacción', value: penalty });
    hit += penalty;
  }
  hit = clamp(hit, 0, 100);

  const critMods: Modifier[] = [];
  if (!opts.reaction) {
    if (st.crit) critMods.push({ label: 'Base', value: st.crit });
    if (sm.crit) critMods.push({ label: 'Habilidad', value: sm.crit });
    if (weapon.crit) critMods.push({ label: 'Arma', value: weapon.crit });
    if (close && sm.closeRangeCrit) critMods.push({ label: 'Quemarropa', value: sm.closeRangeCrit });
    if (flanked && st.flankCrit) critMods.push({ label: 'Flanqueo', value: st.flankCrit });
    if (squadsight) critMods.push({ label: 'Visión de escuadra', value: SQUADSIGHT_CRIT_MOD });
  }
  const crit = clamp(critMods.reduce((sum, m) => sum + m.value, 0), 0, 100);

  const dodge = tt.dodge + (target.hunkered ? HUNKER_DODGE : 0);
  const graze = hit < 100 ? Math.round((dodge / 100) * hit) : 0;

  return { origin, hit, crit, graze, flanked, cover, distance: dist, squadsight, mods, critMods };
}

/** Single d100 roll; crit and graze bands sit inside the hit band. */
export function rollShot(p: ShotPreview, rng: Rng): ShotResult {
  const roll = rng.int(100);
  if (roll >= p.hit) return 'miss';
  const crit = Math.min(p.crit, p.hit);
  if (roll < crit) return 'crit';
  const graze = Math.min(p.graze, p.hit - crit);
  if (roll < crit + graze) return 'graze';
  return 'hit';
}

export function rollDamage(weapon: WeaponDef, result: ShotResult, rng: Rng, bonus = 0, mult = 1): number {
  if (result === 'miss') return 0;
  let dmg = weapon.damage + bonus + (weapon.spread ? rng.range(-weapon.spread, weapon.spread) : 0);
  if (weapon.plusOne && rng.int(100) < weapon.plusOne) dmg += 1;
  if (result === 'crit') dmg += weapon.critDamage;
  if (mult !== 1) dmg = Math.ceil(dmg * mult);
  if (result === 'graze') dmg = Math.round(dmg * GRAZE_DAMAGE_MULT);
  return Math.max(1, dmg);
}

/** Extra damage, multiplier and shred a shot gets from the ability and the shooter's perks. */
export function shotDamageMods(shooter: Unit, ability: AttackAbility, flanked: boolean): { bonus: number; mult: number; shred: number; pierce: number } {
  const def = ABILITIES[ability];
  const weapon = attackWeapon(shooter, ability);
  const primary = (def.weapon ?? 'primary') === 'primary';
  const m = shooter.mods;
  const bonus = (weapon.melee ? m.meleeDamage : primary ? m.damage : 0) + (def.damageBonus ?? 0) + (flanked ? m.flankDamage : 0);
  const gun = primary && !weapon.melee;
  return { bonus, mult: def.damageMult ?? 1, shred: weapon.shred + (gun ? m.shred : 0), pierce: gun ? m.pierce : 0 };
}

export function weaponOf(u: Unit): WeaponDef {
  return attackWeapon(u, 'shoot');
}
