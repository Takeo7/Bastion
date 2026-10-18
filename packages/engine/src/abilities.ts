// Single source of truth for "can this unit use this ability, on what?".
// The server validates commands with it, the client greys out buttons with it
// and the AI picks targets with it.
import { adjacent, inMeleeReach, previewShot, teamSees } from './aim';
import {
  ABILITIES,
  attackWeapon,
  clipOf,
  conditionOf,
  firesPrimary,
  fullyLoaded,
  HACK_BASE,
  HACK_SPECIALIST,
  hasMod,
  hasPerk,
  isAttack,
  ITEMS,
  OFICIO_QUIET,
  PSI_BASE,
  roundsOf,
  TEMPLATES,
  THROWN,
  WEAPONS,
} from './content';
import { PERK_ABILITY } from './board';
import { hasAnyCover } from './cover';
import { canStandOn, DIRS4, DIRS8, distance, inBounds, kindAt, nodeOf, placesOn, samePos, sameSpace, sameTile, slabAt, toPos, unitAt } from './grid';
import { reachesToSearch, roomAt, roomSearched, searchable, searchableTiles, searchSpot } from './loot';
import { canSee, canSeeTile, isViewer, sightTo } from './los';
import { buildPath, computeReach, costPerAp } from './path';
import type { AbilityId, AttackAbility, BlastAbility, GameState, Place, Pos, Unit, Vec2 } from './types';

export function abilitiesOf(u: Unit): AbilityId[] {
  const list = [...TEMPLATES[u.template].abilities];
  // Arms of its own (docs/FASE8.md §A): everyone has a blade, and the pistol is a second gun only beside a primary.
  if (u.arms) {
    if (!list.includes('slash')) list.splice(list.indexOf('shoot') + 1, 0, 'slash');
    const pistol = list.indexOf('pistol');
    if (u.arms.primary && pistol < 0) list.splice(list.indexOf('slash') + 1, 0, 'pistol');
    if (!u.arms.primary && pistol >= 0) list.splice(pistol, 1);
  }
  // Nobody throws grenades without having carried some (docs/FASE9.md §4): the squad's come only from the belt.
  if (u.team === 'xcom' && u.charges.grenade === undefined && list.includes('grenade')) list.splice(list.indexOf('grenade'), 1);
  for (const granted of [...u.perks.map((id) => PERK_ABILITY[id]), ...u.gear.map((id) => ITEMS[id]?.ability)]) {
    if (granted && !list.includes(granted)) list.push(granted);
  }
  if (u.team === 'xcom' && !u.captive && TEMPLATES[u.template].team === 'xcom') {
    // Evacuating is the squad's, by the bus (the `evac` command, docs/PRUEBA-2026-10-14.md P1), not one soldier's ability.
    if (u.template !== 'vip') list.push('hack', 'search', 'searchRoom', 'openDoor', 'closeDoor');
    // Fase 10 (docs/FASE10.md §4): a stone is always at hand, and the blade can keep watch too.
    if (u.arms) {
      list.push('stone');
      if (list.includes('overwatch')) list.splice(list.indexOf('overwatch') + 1, 0, 'overwatchMelee');
    }
  }
  return list;
}

/** On the evac zone: it lies on the ground. */
export function inEvacZone(s: GameState, p: Pos): boolean {
  return p.z === 0 && s.mission.evacZone.some((z) => sameTile(z, p));
}

/** Hack from an adjacent tile, or from afar with the specialist's drone (needs sight of the terminal). */
export function canHackFrom(s: GameState, u: Unit, from: Pos = u.pos): boolean {
  const t = s.mission.terminal;
  if (!t) return false;
  if (adjacent(from, t) && from.z === t.z) return true;
  return u.template === 'specialist' && distance(from, t) <= (ABILITIES.hack.range ?? 0) && canSeeTile(s, from, t, sightTo(s, u, t));
}

export function hackChance(s: GameState, u: Unit): number {
  const bonus = (u.template === 'specialist' ? HACK_SPECIALIST : 0) + u.mods.hack + s.mission.hackBonus;
  return Math.max(5, Math.min(95, HACK_BASE + bonus));
}

/**
 * Chance that the Chillón's scream (or a psionic power) takes hold of
 * `target`; 0 = immune: machines, the dead, Temple 10 («Nervios de acero»),
 * Contracanto. Canto de calma adds +10 will per level to allies within 4
 * tiles (`s` needed).
 */
export function psiChance(target: Unit, s?: GameState): number {
  if (TEMPLATES[target.template].robotic || target.will >= 100 || target.controlledBy || (target.ward ?? 0) > 0 || target.mods.psiGuard >= 2) return 0;
  const calm = s
    ? Math.max(0, ...s.units.filter((a) => a.alive && a.team === target.team && a.mods.calmAura > 0 && distance(a.pos, target.pos) <= CALM_RANGE && sameSpace(s, a.pos, target.pos)).map((a) => a.mods.calmAura))
    : 0;
  const will = target.will + CALM_WILL * calm;
  return Math.max(10, Math.min(90, PSI_BASE - (will - 40)));
}

/** Canto de calma: reach and will per level. */
const CALM_RANGE = 4;
const CALM_WILL = 10;

/** Whether `p` lies inside a Silencio zone: no Cantor can sing there. */
export function inSilence(s: GameState, p: Place): boolean {
  return (s.silences ?? []).some((z) => distance(z.pos, p) <= z.radius && sameSpace(s, z.pos, p));
}

/** Enemies a song that strikes everyone in sight can reach (Tormenta, Lluvia de luz). */
export function songTargets(s: GameState, u: Unit, ability: 'velmarStorm' | 'lightRain'): Unit[] {
  if (ability === 'lightRain') return rangedTargets(s, u, 'lightRain');
  return s.units.filter((t) => hostile(u, t) && !t.objective && canSee(s, u, t));
}

/** Action points the ability needs right now (sniper shots need both; free actions need none). */
export function apNeeded(u: Unit, ability: AbilityId): number {
  const def = ABILITIES[ability];
  if (def.ap === 0) return 0;
  // A song's arrow is light, not the rifle: no sniper set-up (Flecha espectral, Lluvia de luz).
  return isAttack(ability) && !def.song ? Math.max(def.ap, attackWeapon(u, ability).apCost) : def.ap;
}

/** Whether using the ability spends every action left (perks can lift that). */
export function endsTurn(u: Unit, ability: AbilityId): boolean {
  if ((ability === 'grenade' || ability === 'launch') && hasPerk(u, 'salvo')) return false;
  return ABILITIES[ability].endsTurn;
}

/** Rounds the ability needs in the clip (attacks with a magazine need at least one). */
export function ammoNeeded(u: Unit, ability: AbilityId): number {
  const def = ABILITIES[ability];
  if (def.ammo) return def.ammo;
  if (ability === 'overwatch' || ability === 'killZone') return clipOf(u, 'shoot') > 0 ? 1 : 0;
  return isAttack(ability) && clipOf(u, ability) > 0 ? 1 : 0;
}

/** Tile abilities done with the hands beside the soldier, not thrown: a disoriented soldier can still do them. */
const BY_HAND: ReadonlySet<AbilityId> = new Set<AbilityId>(['search', 'searchRoom', 'openDoor', 'closeDoor', 'smash']);

/**
 * Doors beside `u` it can open (`door`) or close (`doorOpen`, with nobody in
 * the doorway), reaching them with the hands as when searching (loot.ts
 * reachesToSearch): beside it, on its level, not round a wall's corner.
 */
export function doorTiles(s: GameState, u: Unit, kind: 'door' | 'doorOpen'): Pos[] {
  return DIRS8.map((d) => ({ x: u.pos.x + d.x, y: u.pos.y + d.y, z: u.pos.z })).filter(
    (p) => kindAt(s, p) === kind && reachesToSearch(s, u.pos, p) && !(kind === 'doorOpen' && unitAt(s, p)),
  );
}

/** In a siege (docs/FASE12.md §4), a door still shut or a barricade still up, that the dead can break down. */
export function breakable(s: GameState, p: Pos): boolean {
  const kind = kindAt(s, p);
  return (kind === 'door' || kind === 'low') && (s.breakables?.[nodeOf(s, p)] ?? 0) > 0;
}

/** What one of the dead on `from` can beat on: beside it, reached as with the hands (doorTiles). */
export function breakablesBeside(s: GameState, from: Pos): Pos[] {
  if (!s.breakables) return [];
  return DIRS8.map((d) => ({ x: from.x + d.x, y: from.y + d.y, z: from.z })).filter((p) => inBounds(s, p.x, p.y) && breakable(s, p) && reachesToSearch(s, from, p));
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
  if (ammo > 0 && (isAttack(ability) ? roundsOf(u, ability) : u.ammo) < ammo) {
    if (u.mags === 0) return 'Sin munición y sin cargadores.';
    return ammo > 1 ? `Necesita ${ammo} balas en el cargador: recarga.` : 'Sin munición: recarga.';
  }
  if (u.disoriented && def.target === 'tile' && !def.teleport && !BY_HAND.has(ability) && ability !== 'stone') return 'Desorientado: no puede usar explosivos.';
  if (firesPrimary(u, ability) && conditionOf(u) <= 0) return 'Rota: no dispara. Se arregla en la Armería.';
  if (def.psi && inSilence(s, u.pos)) return 'Petardos: aquí no se puede gritar.';
  switch (ability) {
    case 'reload':
      if (fullyLoaded(u)) return u.arms?.primary ? 'Los cargadores están llenos.' : 'El cargador está lleno.';
      if (u.mags === 0) return 'No quedan cargadores.';
      break;
    case 'hunker':
      if (!hasAnyCover(s, u.pos)) return 'Necesita estar a cubierto.';
      break;
    case 'evac':
      if (!inEvacZone(s, u.pos)) return 'Solo desde la zona de evacuación.';
      break;
    case 'velmarStorm':
    case 'lightRain':
      if (!songTargets(s, u, ability).length) return 'Ningún enemigo a la vista.';
      break;
    case 'elbowRoom':
      if (!besideEnemies(s, u).length) return 'No tienes ningún enemigo al lado.';
      break;
    case 'hack':
      if (!s.mission.terminal) return 'No hay ningún equipo que arreglar en esta misión.';
      if (s.mission.objectiveDone) return 'El equipo ya está en marcha.';
      if (!canHackFrom(s, u)) return u.template === 'specialist' ? 'Acércate al equipo o ponlo a la vista del Grillo (10 casillas).' : 'Ponte junto al equipo.';
      break;
    case 'search':
      // A loot box beside the soldier (docs/FASE8.md §B). A full backpack doesn't stop it: what doesn't fit goes on the floor.
      if (!searchableTiles(s, u).length) {
        const done = DIRS8.some((d) => {
          const p = { x: u.pos.x + d.x, y: u.pos.y + d.y, z: u.pos.z };
          return searchSpot(s, p) !== null && reachesToSearch(s, u.pos, p);
        });
        return done ? 'Ya está abierta.' : 'No hay ninguna caja al lado.';
      }
      break;
    case 'searchRoom': {
      const room = roomAt(s, u.pos);
      if (!room) return 'Solo dentro de una habitación.';
      if (roomSearched(s, room)) return 'Esta habitación ya está registrada.';
      break;
    }
    case 'openDoor':
      if (!doorTiles(s, u, 'door').length) return 'No hay ninguna puerta cerrada a tu lado.';
      break;
    case 'closeDoor':
      if (!doorTiles(s, u, 'doorOpen').length) return 'No hay ninguna puerta abierta a tu lado.';
      break;
    case 'smash':
      if (!breakablesBeside(s, u.pos).length) return 'No hay ninguna puerta ni barricada a su lado.';
      break;
  }
  return null;
}

const hostile = (u: Unit, t: Unit) => t.alive && t.team !== u.team && !t.captive;

/** Enemies within reach of a blade without a step (Hacerse sitio, docs/FASE9.md). */
export function besideEnemies(s: GameState, u: Unit): Unit[] {
  return s.units.filter((t) => hostile(u, t) && inMeleeReach(s, u.pos, t.pos));
}

/** Enemies a ranged attack can hit right now. */
export function rangedTargets(s: GameState, u: Unit, ability: AttackAbility): Unit[] {
  return s.units.filter((t) => hostile(u, t) && previewShot(s, u, t, { ability }) !== null);
}

export interface MeleeOption {
  target: Unit;
  /** Tile to strike from (the attacker's own tile when already adjacent). */
  tile: Pos;
  path: Pos[];
}

/**
 * Enemies a melee ability can reach: only whoever is alongside, seen by the
 * team. Striking is striking and running is running (Arturo, 16/10): getting
 * there is a move of its own. `ability` stays for the callers.
 */
export function meleeTargets(s: GameState, u: Unit, _ability: AttackAbility): MeleeOption[] {
  return s.units.filter((t) => hostile(u, t) && teamSees(s, u.team, t) && inMeleeReach(s, u.pos, t.pos)).map((t) => ({ target: t, tile: u.pos, path: [] }));
}

/** Statuses Revival and Restoration remove. */
export function hasBadStatus(u: Unit): boolean {
  return u.stunned || u.disoriented > 0 || u.panicked;
}

/** Allies a support ability can target. */
export function allyTargets(s: GameState, u: Unit, ability: 'medkit' | 'aid' | 'revival' | 'firstAid' | 'layOnHands' | 'counterSong' | 'patchUp'): Unit[] {
  const range = ABILITIES[ability].range ?? Infinity;
  return s.units.filter((t) => {
    // Contracanto reaches a comrade the Cantor holds, who now stands with the enemy.
    const held = ability === 'counterSong' && !!t.controlledBy && TEMPLATES[t.template].team === u.team;
    if (!t.alive || (t.team !== u.team && !held) || t.captive || distance(u.pos, t.pos) > range) return false;
    // By hand, beside the ally: never through a floor (the Grillo flies wherever it likes).
    if (range <= 1.5 && !sameSpace(s, u.pos, t.pos)) return false;
    if (ability === 'medkit' || ability === 'firstAid') return t.hp < t.maxHp;
    if (ability === 'revival') return hasBadStatus(t);
    if (ability === 'layOnHands') return t.hp < t.maxHp || t.panicked || t.disoriented > 0;
    if (ability === 'counterSong') return true;
    // Apaños (docs/FASE9.md): a weapon in the hand that isn't as good as new, its own or a partner's alongside.
    if (ability === 'patchUp') return !!t.arms?.primary && conditionOf(t) < 100;
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
      canSeeTile(s, u.pos, t.pos, sightTo(s, u, t.pos)),
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
      return s.units.filter((t) => hostile(u, t) && distance(u.pos, t.pos) <= (def.range ?? 0) && canSee(s, u, t) && psiChance(t, s) > 0);
    case 'bolt':
      return s.units.filter((t) => hostile(u, t) && !t.objective && distance(u.pos, t.pos) <= (def.range ?? 0) && canSee(s, u, t));
    case 'huntersMark':
      return s.units.filter((t) => hostile(u, t) && !t.objective && t.huntedBy !== u.id && canSee(s, u, t));
    case 'layOnHands':
    case 'counterSong':
      return allyTargets(s, u, ability);
    case 'reanimate':
      return corpseTargets(s, u);
    case 'medkit':
    case 'aid':
    case 'revival':
    case 'firstAid':
    case 'patchUp':
      return allyTargets(s, u, ability);
  }
  if (def.target === 'melee' && isAttack(ability)) return meleeTargets(s, u, ability).map((o) => o.target);
  if (def.target === 'enemy' && isAttack(ability)) return rangedTargets(s, u, ability);
  return [];
}

/**
 * Explosives: target tile within range and seen by the squad (not necessarily
 * by the thrower). A teleport needs a free floor the singer sees itself.
 */
export function canTargetTile(s: GameState, u: Unit, ability: AbilityId, at: Place): boolean {
  const tile = toPos(at);
  const def = ABILITIES[ability];
  if (def.target !== 'tile' || !inBounds(s, tile.x, tile.y)) return false;
  if (distance(u.pos, tile) > abilityRange(u, ability)) return false;
  if (ability === 'search') return searchable(s, tile) && reachesToSearch(s, u.pos, tile);
  if (ability === 'openDoor' || ability === 'closeDoor') return doorTiles(s, u, ability === 'openDoor' ? 'door' : 'doorOpen').some((p) => samePos(p, tile));
  if (ability === 'smash') return breakablesBeside(s, u.pos).some((p) => samePos(p, tile));
  if (def.teleport) return !samePos(u.pos, tile) && canStandOn(kindAt(s, tile)) && !unitAt(s, tile) && canSeeTile(s, u.pos, tile, sightTo(s, u, tile));
  return s.units.some((o) => isViewer(o, u.team) && canSeeTile(s, o.pos, tile, sightTo(s, o, tile)));
}

/** Reach of a targeted ability for this unit (talents can lengthen grenade throws). */
export function abilityRange(u: Unit | undefined, ability: AbilityId): number {
  const base = ABILITIES[ability].range ?? 0;
  return THROWN.has(ability) ? base + (u?.mods.grenadeRange ?? 0) : base;
}

/** Blast radius of an area ability for this unit (perks can widen grenades). */
export function blastRadius(u: Unit | undefined, ability: AbilityId): number {
  const base = ABILITIES[ability].radius ?? 0;
  return ability === 'grenade' || ability === 'launch' ? base + (u?.mods.grenadeRadius ?? 0) : base;
}

/**
 * The places a blast on `at` reaches: within its radius, and on a map with
 * storeys only those in its space (grid.ts sameSpace: a floor stops it).
 */
export function blastTiles(s: GameState, ability: AbilityId, at: Place, thrower?: Unit): Pos[] {
  const center = toPos(at);
  const radius = blastRadius(thrower, ability);
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

/** Explosives that breach walls (docs/FASE7.md §4): the frag grenades, thrown, launched or going off on a vest. */
const BREACHING: ReadonlySet<AbilityId> = new Set<AbilityId>(['grenade', 'launch', 'missiles']);

/**
 * Walls a blast breaks (docs/FASE7.md §4), left as rubble: within its radius,
 * on the floor it went off on, and beside one of the tiles it reaches. None
 * for anything but the frag grenades.
 */
export function breachedWalls(s: GameState, ability: AbilityId, at: Place, thrower?: Unit): Pos[] {
  if (!BREACHING.has(ability)) return [];
  const center = toPos(at);
  const radius = blastRadius(thrower, ability);
  const reached = new Set(blastTiles(s, ability, at, thrower).filter((p) => kindAt(s, p) !== 'wall').map((p) => nodeOf(s, p)));
  const out: Pos[] = [];
  const r = Math.ceil(radius);
  for (let y = center.y - r; y <= center.y + r; y++) {
    for (let x = center.x - r; x <= center.x + r; x++) {
      const p = { x, y, z: center.z };
      if (!inBounds(s, x, y) || distance(center, p) > radius || kindAt(s, p) !== 'wall') continue;
      if (DIRS4.some((d) => inBounds(s, x + d.x, y + d.y) && reached.has(nodeOf(s, { x: x + d.x, y: y + d.y, z: center.z })))) out.push(p);
    }
  }
  return out;
}

/** Whether a blast on `at` breaks the slab it went off on (docs/FASE7.md §5): a frag grenade on a storey's floor or a roof. */
export function breaksFloor(s: GameState, ability: AbilityId, at: Place): boolean {
  const p = toPos(at);
  return BREACHING.has(ability) && p.z >= 1 && slabAt(s, p.x, p.y, p.z);
}

export function isBlast(ability: AbilityId): ability is BlastAbility {
  return ABILITIES[ability].target === 'tile';
}

/**
 * How far, in tiles, using the ability is heard (docs/FASE2.md, `NOISE` in
 * content.ts): an attack sounds like its weapon, a blast like its explosive,
 * unless the ability says otherwise; the Oficio is quieter with the Taller de
 * oficios by the listening post. Heard where a tile ability lands, or where
 * the user stands.
 */
export function noiseOf(s: GameState, u: Unit, ability: AbilityId): number {
  const def = ABILITIES[ability];
  let noise = def.noise;
  if (noise === undefined) {
    if (isAttack(ability)) noise = attackWeapon(u, ability).noise;
    else if (ability === 'suppression') noise = attackWeapon(u, 'shoot').noise;
    else noise = def.explosive ? WEAPONS[def.explosive].noise : 0;
  }
  // Sin hacer ruido (docs/FASE9.md): searching and opening boxes in silence. A silencer halves a shot's noise.
  if (u.mods.quiet && (ability === 'search' || ability === 'searchRoom')) return 0;
  if (ability === 'stone') noise += u.mods.stoneNoise;
  if (firesPrimary(u, ability) && hasMod(u, 'silencer')) noise = Math.ceil(noise / 2);
  return def.song && s.quietSongs ? Math.max(0, noise - OFICIO_QUIET) : noise;
}

/** Whether `p` lies inside a smoke cloud (on its floor). */
export function inSmoke(s: GameState, p: Place): boolean {
  return s.smoke.some((c) => distance(c.pos, p) <= c.radius && sameSpace(s, c.pos, p));
}
