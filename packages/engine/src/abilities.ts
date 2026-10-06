// Single source of truth for "can this unit use this ability, on what?".
// The server validates commands with it, the client greys out buttons with it
// and the AI picks targets with it.
import { adjacent, inMeleeReach, previewShot, teamSees } from './aim';
import {
  ABILITIES,
  attackWeapon,
  HACK_BASE,
  HACK_PER_RANK,
  HACK_SPECIALIST,
  hasPerk,
  isAttack,
  ITEMS,
  PERKS,
  PSI_BASE,
  TEMPLATES,
} from './content';
import { hasAnyCover } from './cover';
import { DIRS8, distance, inBounds, sameTile } from './grid';
import { canSee, canSeeTile, isViewer } from './los';
import { buildPath, computeReach, costPerAp } from './path';
import type { AbilityId, AttackAbility, BlastAbility, GameState, Unit, Vec2 } from './types';

export function abilitiesOf(u: Unit): AbilityId[] {
  const list = [...TEMPLATES[u.template].abilities];
  for (const granted of [...u.perks.map((id) => PERKS[id]?.ability), ...u.gear.map((id) => ITEMS[id]?.ability)]) {
    if (granted && !list.includes(granted)) list.push(granted);
  }
  if (u.team === 'xcom' && !u.captive && TEMPLATES[u.template].team === 'xcom') {
    list.push('evac');
    if (u.template !== 'vip') list.push('hack');
  }
  return list;
}

export function inEvacZone(s: GameState, p: Vec2): boolean {
  return s.mission.evacZone.some((z) => sameTile(z, p));
}

/** Hack from an adjacent tile, or from afar with the specialist's drone (needs sight of the terminal). */
export function canHackFrom(s: GameState, u: Unit, from: Vec2 = u.pos): boolean {
  const t = s.mission.terminal;
  if (!t) return false;
  if (adjacent(from, t)) return true;
  return u.template === 'specialist' && distance(from, t) <= (ABILITIES.hack.range ?? 0) && canSeeTile(s, from, t, TEMPLATES[u.template].sight);
}

export function hackChance(s: GameState, u: Unit): number {
  const bonus = (u.template === 'specialist' ? HACK_SPECIALIST : 0) + u.rank * HACK_PER_RANK + s.mission.hackBonus;
  return Math.max(5, Math.min(95, HACK_BASE + bonus));
}

/** Chance that a psionic attack takes hold of `target` (0 = immune). */
export function psiChance(target: Unit): number {
  if (TEMPLATES[target.template].robotic || target.will >= 100 || target.controlledBy) return 0;
  return Math.max(10, Math.min(90, PSI_BASE - (target.will - 40)));
}

/** Action points the ability needs right now (sniper shots need both; free actions need none). */
export function apNeeded(u: Unit, ability: AbilityId): number {
  const def = ABILITIES[ability];
  if (def.ap === 0) return 0;
  return isAttack(ability) ? Math.max(def.ap, attackWeapon(u, ability).apCost) : def.ap;
}

/** Whether using the ability spends every action left (perks can lift that). */
export function endsTurn(u: Unit, ability: AbilityId): boolean {
  if ((ability === 'grenade' || ability === 'launch') && hasPerk(u, 'salvo')) return false;
  if (ability === 'pistol' && hasPerk(u, 'quickdraw')) return false;
  return ABILITIES[ability].endsTurn;
}

/** Rounds the ability needs in the clip (attacks with a magazine need at least one). */
export function ammoNeeded(u: Unit, ability: AbilityId): number {
  const def = ABILITIES[ability];
  if (def.ammo) return def.ammo;
  if (ability === 'overwatch' || ability === 'killZone') return attackWeapon(u, 'shoot').clip > 0 ? 1 : 0;
  return isAttack(ability) && attackWeapon(u, ability).clip > 0 ? 1 : 0;
}

/** Why the ability cannot be used right now (ignoring targets), or null if it can. */
export function abilityBlocker(s: GameState, u: Unit, ability: AbilityId): string | null {
  if (!abilitiesOf(u).includes(ability)) return 'No tiene esa habilidad.';
  const def = ABILITIES[ability];
  const need = apNeeded(u, ability);
  if (u.ap <= 0) return 'Sin acciones.';
  if (u.ap < need) return need === 2 ? 'Necesita 2 acciones: no puede moverse y disparar en el mismo turno.' : 'Sin acciones.';
  const cooldown = u.cooldowns[ability] ?? 0;
  if (cooldown > 0) return `Disponible en ${cooldown} ${cooldown === 1 ? 'turno' : 'turnos'}.`;
  if (def.charge && !((u.charges[def.charge] ?? 0) > 0)) return 'No quedan usos.';
  const ammo = ammoNeeded(u, ability);
  if (ammo > 0 && u.ammo < ammo) return ammo > 1 ? `Necesita ${ammo} balas en el cargador: recarga.` : 'Sin munición: recarga.';
  if (u.disoriented && def.target === 'tile') return 'Desorientado: no puede usar explosivos.';
  switch (ability) {
    case 'reload':
      if (u.ammo >= attackWeapon(u, 'shoot').clip) return 'El cargador está lleno.';
      break;
    case 'hunker':
      if (!hasAnyCover(s, u.pos)) return 'Necesita estar a cubierto.';
      break;
    case 'evac':
      if (!inEvacZone(s, u.pos)) return 'Solo desde la zona de evacuación.';
      break;
    case 'hack':
      if (!s.mission.terminal) return 'No hay ningún terminal en esta misión.';
      if (s.mission.objectiveDone) return 'El terminal ya está hackeado.';
      if (!canHackFrom(s, u)) return u.template === 'specialist' ? 'Acércate al terminal o ponlo a la vista del dron (10 casillas).' : 'Ponte junto al terminal.';
      break;
  }
  return null;
}

const hostile = (u: Unit, t: Unit) => t.alive && t.team !== u.team && !t.captive;

/** Enemies a ranged attack can hit right now. */
export function rangedTargets(s: GameState, u: Unit, ability: AttackAbility): Unit[] {
  return s.units.filter((t) => hostile(u, t) && previewShot(s, u, t, { ability }) !== null);
}

export interface MeleeOption {
  target: Unit;
  /** Tile to strike from (the attacker's own tile when already adjacent). */
  tile: Vec2;
  path: Vec2[];
}

/** Enemies a melee ability can reach this action: within the blue move range, seen by the team. */
export function meleeTargets(s: GameState, u: Unit, ability: AttackAbility): MeleeOption[] {
  const reach = computeReach(s, u, costPerAp(u));
  const options: MeleeOption[] = [];
  for (const t of s.units) {
    if (!hostile(u, t) || !teamSees(s, u.team, t)) continue;
    if (inMeleeReach(s, u.pos, t.pos)) {
      options.push({ target: t, tile: u.pos, path: [] });
      continue;
    }
    let best: MeleeOption | null = null;
    let bestCost = Infinity;
    for (const d of DIRS8) {
      const tile = { x: t.pos.x + d.x, y: t.pos.y + d.y };
      if (!inBounds(s, tile.x, tile.y)) continue;
      const cost = reach.cost.get(tile.y * s.width + tile.x);
      if (cost === undefined || cost >= bestCost) continue;
      const path = buildPath(s, u, reach, tile);
      if (!path) continue;
      best = { target: t, tile, path };
      bestCost = cost;
    }
    if (best && previewShot(s, u, t, { ability, from: best.tile })) options.push(best);
  }
  return options;
}

/** Statuses Revival and Restoration remove. */
export function hasBadStatus(u: Unit): boolean {
  return u.stunned || u.disoriented > 0 || u.panicked;
}

/** Allies a support ability can target. */
export function allyTargets(s: GameState, u: Unit, ability: 'medkit' | 'aid' | 'revival' | 'firstAid'): Unit[] {
  const range = ABILITIES[ability].range ?? Infinity;
  return s.units.filter((t) => {
    if (!t.alive || t.team !== u.team || t.captive || distance(u.pos, t.pos) > range) return false;
    if (ability === 'medkit' || ability === 'firstAid') return t.hp < t.maxHp;
    if (ability === 'revival') return hasBadStatus(t);
    return t.id !== u.id && !t.aided;
  });
}

/** Corpses a sectoid can raise: in range and in sight, flesh and blood. */
export function corpseTargets(s: GameState, u: Unit): Unit[] {
  const range = ABILITIES.reanimate.range ?? 0;
  return s.units.filter(
    (t) =>
      !t.alive &&
      !t.evacuated &&
      !t.reanimated &&
      !t.objective &&
      !TEMPLATES[t.template].robotic &&
      t.template !== 'zombie' &&
      distance(u.pos, t.pos) <= range &&
      canSeeTile(s, u.pos, t.pos, TEMPLATES[u.template].sight),
  );
}

/** Every valid unit target of a targeted ability (attacks, support, psionics, corpses). */
export function abilityTargets(s: GameState, u: Unit, ability: AbilityId): Unit[] {
  const def = ABILITIES[ability];
  switch (ability) {
    case 'suppression':
      return rangedTargets(s, u, 'shoot');
    case 'combatProtocol':
      return s.units.filter((t) => hostile(u, t) && distance(u.pos, t.pos) <= (def.range ?? 0) && teamSees(s, u.team, t));
    case 'mindspin':
      return s.units.filter((t) => hostile(u, t) && distance(u.pos, t.pos) <= (def.range ?? 0) && canSee(s, u, t) && psiChance(t) > 0);
    case 'reanimate':
      return corpseTargets(s, u);
    case 'medkit':
    case 'aid':
    case 'revival':
    case 'firstAid':
      return allyTargets(s, u, ability);
  }
  if (def.target === 'melee' && isAttack(ability)) return meleeTargets(s, u, ability).map((o) => o.target);
  if (def.target === 'enemy' && isAttack(ability)) return rangedTargets(s, u, ability);
  return [];
}

/** Explosives: target tile within range and seen by the squad (not necessarily by the thrower). */
export function canTargetTile(s: GameState, u: Unit, ability: AbilityId, tile: Vec2): boolean {
  const def = ABILITIES[ability];
  if (def.target !== 'tile' || !inBounds(s, tile.x, tile.y)) return false;
  if (distance(u.pos, tile) > (def.range ?? 0)) return false;
  return s.units.some((o) => isViewer(o, u.team) && canSeeTile(s, o.pos, tile, TEMPLATES[o.template].sight));
}

/** Blast radius of an area ability for this unit (perks can widen grenades). */
export function blastRadius(u: Unit | undefined, ability: AbilityId): number {
  const base = ABILITIES[ability].radius ?? 0;
  return ability === 'grenade' || ability === 'launch' ? base + (u?.mods.grenadeRadius ?? 0) : base;
}

export function blastTiles(s: GameState, ability: AbilityId, center: Vec2, thrower?: Unit): Vec2[] {
  const radius = blastRadius(thrower, ability);
  const out: Vec2[] = [];
  const r = Math.ceil(radius);
  for (let y = center.y - r; y <= center.y + r; y++) {
    for (let x = center.x - r; x <= center.x + r; x++) {
      if (inBounds(s, x, y) && distance(center, { x, y }) <= radius) out.push({ x, y });
    }
  }
  return out;
}

export function isBlast(ability: AbilityId): ability is BlastAbility {
  return ABILITIES[ability].target === 'tile';
}

/** Whether `p` lies inside a smoke cloud. */
export function inSmoke(s: GameState, p: Vec2): boolean {
  return s.smoke.some((c) => distance(c.pos, p) <= c.radius);
}
