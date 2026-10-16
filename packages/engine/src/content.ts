// Gameplay data. Combat numbers mirror XCOM 2's defaults (XComGameCore.ini /
// XComGameData_*.ini) converted to tiles (1 tile = 1.5 m), so the game "feels"
// like the original before we start tuning our own values.
import type { AbilityId, AttackAbility, ChargeId, ClassId, FoundWeaponId, ItemId, ItemSlot, PerkId, Team, TemplateId, Unit, UnitMods, WeaponModId } from './types';

export const AP_PER_TURN = 2;

/** Unit.rank from which a soldier has a third action: Veterano (skills.ts VETERANCY, docs/FASE9.md §3). */
export const THIRD_ACTION_RANK = 3;

/** Actions a unit starts its turn with: two, three for a soldier from Veterano on. */
export function apPerTurn(u: Pick<Unit, 'team' | 'rank'>): number {
  return u.team === 'xcom' && u.rank >= THIRD_ACTION_RANK ? AP_PER_TURN + 1 : AP_PER_TURN;
}

/** Spare magazines a soldier takes into a mission for the primary weapon (docs/FASE7.md): reloading spends one. */
export const SPARE_MAGS = 3;

/** Damage per storey to whoever falls through a floor a grenade broke (docs/FASE7.md §5); armour doesn't stop it. */
export const FALL_DAMAGE = 1;

export const LOW_COVER_BONUS = 20;
export const HIGH_COVER_BONUS = 40;
export const HUNKER_DEFENSE = 30;
export const HUNKER_DODGE = 50;
export const AID_DEFENSE = 20;
/** Reaction shots lose this fraction of their hit chance (XCOM 2 REACTION_FINALMOD). */
export const REACTION_PENALTY = 0.3;
export const GRAZE_DAMAGE_MULT = 0.5;
/** Squadsight: aim lost per tile beyond the shooter's own sight, and crit penalty. */
export const SQUADSIGHT_DISTANCE_MOD = -2;
export const SQUADSIGHT_CRIT_MOD = -10;
export const MEDKIT_HEAL = 4;
/** Shooting down from a higher level (XCOM 2 height advantage). */
export const HEIGHT_ADVANTAGE_AIM = 20;
/**
 * Cover applies when the attacker is within this angle of the cover's facing.
 * Wider angles mean fewer flanks. Tuning value, not taken from XCOM.
 */
export const COVER_MAX_ANGLE_DEG = 70;

/**
 * Noise (docs/FASE2.md): how far, in tiles, an action is heard. Every enemy
 * within it that sees no soldier goes to look (`Unit.heard`); noise never
 * wakes a pod nor breaks concealment by itself, being seen does. Weapons and
 * abilities carry their own (`noise`); these are the actions that aren't one.
 */
export const NOISE = {
  /** Opening a closed door: quiet. */
  door: 1,
  /** Going through a window's glass. */
  breach: 6,
  /** A move that spends both actions. */
  dash: 3,
  /** A failed hack: the alarm blares at the terminal and draws a horde in by the edges. */
  alarm: 16,
} as const;
/** The Taller de oficios by the listening post: the Oficio is this much quieter (never below 0). */
export const OFICIO_QUIET = 2;

/**
 * The Saqueo's horde (docs/FASE2.md): each squad turn winds its clock up by
 * `perTurn` points and every noise the squad makes by its radius / `perNoise`
 * (hordePoints); every `wave` points a wave comes in by the edges. One a turn
 * at most, and none while `cap` enemies are already on the field. A quiet
 * squad sees one every 5 turns; a firefight (four rifle shots a turn) one
 * every 3. Tuned with a headless Saqueo against a scripted squad.
 */
/** `trickle`: the dead that come in every turn whatever happens (playtest 2026-10-15, #11), by difficulty (1–4); twice that while a sabotage's alarm sounds. */
export const HORDE = { perTurn: 10, perNoise: 5, wave: 50, cap: 60, trickle: [0, 1, 2, 2, 3] } as const;

/**
 * Night missions (docs/ALTURA-Y-NOCHE.md). The living see `sightCut` tiles
 * less (never under `minSight`) and the dead as ever, but whoever stands in
 * the light is seen from as far as by day; the enemy detects the hidden squad
 * at `detection` of its range, unless lit. Lights: the map's street lights,
 * the Bengala's flare and fire, for `turns` squad turns. One more group of
 * the dead, the Saqueo's horde `hordeHead` turns sooner, and `supplies`
 * times the suministros found and paid. A light reaches only what it sees
 * from its `height` (storeys above its floor): a street lamp from high up, a
 * flare or a fire from the ground.
 */
export const NIGHT = {
  sightCut: 8,
  minSight: 6,
  detection: 0.5,
  streetLight: 2,
  lampHeight: 2,
  flare: { radius: 3, turns: 2, height: 0.5 },
  fire: { radius: 2, turns: 2, height: 0.5 },
  hordeHead: 1,
  supplies: 1.5,
} as const;

/**
 * Infection (docs/FASE3.md): out of 100, the chance that a zombie's melee hit
 * that draws blood infects, by who bites, and a Hinchado's burst to each it
 * hurts; a Chaleco antibalas takes `vest` off.
 */
export const INFECTION = {
  bite: { zombie: 25, xenoid: 20, mec: 35 } as Partial<Record<TemplateId, number>>,
  burst: 50,
  vest: 10,
};

/** What a noise of that radius made by the squad adds to the horde's clock. */
export function hordePoints(radius: number): number {
  return Math.floor(radius / HORDE.perNoise);
}
/** XCOM turns available to recover the objective on a recovery mission. */
export const RECOVERY_TURNS = 12;

/** Aim modifier by tile distance (index = floor(distance)); XCOM 2 conventional tables. */
export const RANGE_TABLES = {
  short: [0, 40, 35, 32, 28, 23, 19, 16, 12, 6, 2, 0, -2, -4, -7, -10, -12, -15, -17, -18, -18, -19, -19, -21, -25, -30],
  medium: [0, 20, 19, 17, 16, 13, 11, 9, 7, 5, 4, 3, 2, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
  long: [0, -30, -27, -24, -21, -18, -15, -12, -9, -6, -3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
  none: [0],
} as const;

export type RangeTable = keyof typeof RANGE_TABLES;

export function rangeModifier(table: RangeTable, distance: number): number {
  const values = RANGE_TABLES[table];
  return values[Math.min(Math.floor(distance), values.length - 1)]!;
}


export type WeaponId =
  | 'rifle'
  | 'shotgun'
  | 'cannon'
  | 'sniper'
  | 'pistol'
  | 'sword'
  | 'knife'
  | 'trooperRifle'
  | 'officerRifle'
  | 'xenoPistol'
  | 'lancerRifle'
  | 'stunLance'
  | 'mecCannon'
  | 'sectoidPistol'
  | 'claws'
  | 'fragGrenade'
  | 'microMissiles'
  | 'droneZap'
  | 'discharge'
  | 'songFire'
  | 'bloaterBurst';

export interface WeaponDef {
  id: WeaponId;
  name: string;
  damage: number;
  spread: number;
  /** Percent chance of +1 damage. */
  plusOne: number;
  critDamage: number;
  /** 0 means the weapon never needs ammo (pistols, blades, explosives). */
  clip: number;
  aim: number;
  crit: number;
  range: RangeTable;
  /** Armour removed per hit. */
  shred: number;
  /** Action points needed to fire (sniper rifles need both). */
  apCost: number;
  melee: boolean;
  /** Rounds shown per shot — purely visual. */
  burst: number;
  /** Noise radius in tiles of an attack or a blast with it (docs/FASE2.md). */
  noise: number;
}

const weapon = (w: Partial<WeaponDef> & Pick<WeaponDef, 'id' | 'name' | 'damage'>): WeaponDef => ({
  spread: 0,
  plusOne: 0,
  critDamage: 0,
  clip: 0,
  aim: 0,
  crit: 0,
  range: 'medium',
  shred: 0,
  apCost: 1,
  melee: false,
  burst: 3,
  noise: 0,
  ...w,
});

export const WEAPONS: Record<WeaponId, WeaponDef> = {
  rifle: weapon({ id: 'rifle', name: 'Fusil de asalto', damage: 4, spread: 1, critDamage: 2, clip: 4, noise: 12 }),
  shotgun: weapon({ id: 'shotgun', name: 'Escopeta', damage: 5, spread: 1, critDamage: 3, clip: 4, crit: 10, range: 'short', burst: 1, noise: 10 }),
  cannon: weapon({ id: 'cannon', name: 'Ametralladora', damage: 5, spread: 1, critDamage: 2, clip: 3, burst: 5, noise: 14 }),
  sniper: weapon({ id: 'sniper', name: 'Fusil de precisión', damage: 5, spread: 1, critDamage: 2, clip: 3, crit: 10, range: 'long', apCost: 2, burst: 1, noise: 16 }),
  pistol: weapon({ id: 'pistol', name: 'Pistola', damage: 2, plusOne: 50, critDamage: 1, burst: 1, noise: 8 }),
  sword: weapon({ id: 'sword', name: 'Machete', damage: 4, spread: 1, critDamage: 2, aim: 20, crit: 10, range: 'none', melee: true, burst: 1, noise: 1 }),
  /** Everyone's blade in the campaign (docs/FASE8.md §A), until a machete. */
  knife: weapon({ id: 'knife', name: 'Cuchillo', damage: 3, spread: 1, critDamage: 1, aim: 10, range: 'none', melee: true, burst: 1, noise: 1 }),
  trooperRifle: weapon({ id: 'trooperRifle', name: 'Fusil de caza', damage: 3, critDamage: 1, clip: 3, noise: 12 }),
  officerRifle: weapon({ id: 'officerRifle', name: 'Fusil automático', damage: 3, plusOne: 50, critDamage: 2, clip: 3, noise: 14 }),
  lancerRifle: weapon({ id: 'lancerRifle', name: 'Carabina', damage: 3, plusOne: 50, critDamage: 2, clip: 3, noise: 12 }),
  stunLance: weapon({ id: 'stunLance', name: 'Red y porra', damage: 3, spread: 1, critDamage: 2, range: 'none', melee: true, burst: 1, noise: 1 }),
  // The zombies' bodies (docs/FASE2.md): no guns. The ids are the XCOM 2 ones they replaced.
  claws: weapon({ id: 'claws', name: 'Mordisco', damage: 3, spread: 1, critDamage: 2, aim: 10, range: 'none', melee: true, burst: 1 }),
  xenoPistol: weapon({ id: 'xenoPistol', name: 'Zarpazo', damage: 2, spread: 1, critDamage: 2, aim: 10, range: 'none', melee: true, burst: 1 }),
  mecCannon: weapon({ id: 'mecCannon', name: 'Embestida', damage: 4, spread: 1, critDamage: 2, shred: 1, range: 'none', melee: true, burst: 1 }),
  /** The Chillón doesn't bite: it only screams (mindspin). */
  sectoidPistol: weapon({ id: 'sectoidPistol', name: 'Chillido', damage: 0, range: 'none', burst: 0 }),
  bloaterBurst: weapon({ id: 'bloaterBurst', name: 'Reventón', damage: 3, burst: 0, noise: 6 }),
  fragGrenade: weapon({ id: 'fragGrenade', name: 'Granada de fragmentación', damage: 3, plusOne: 20, shred: 1, burst: 0, noise: 18 }),
  microMissiles: weapon({ id: 'microMissiles', name: 'Micromisiles', damage: 3, shred: 1, burst: 0, noise: 18 }),
  droneZap: weapon({ id: 'droneZap', name: 'Descarga del Grillo', damage: 2, burst: 0, noise: 2 }),
  discharge: weapon({ id: 'discharge', name: 'Descarga de batería', damage: 3, burst: 0, noise: 4 }),
  songFire: weapon({ id: 'songFire', name: 'Cóctel molotov', damage: 4, burst: 0, noise: 10 }),
};

// ---------------------------------------------------------------- abilities

export type AbilityTarget = 'enemy' | 'melee' | 'ally' | 'tile' | 'self' | 'corpse';

export interface AbilityDef {
  id: AbilityId;
  name: string;
  description: string;
  target: AbilityTarget;
  /** Action points it needs; 0 = free action (the unit still needs actions left). */
  ap: number;
  endsTurn: boolean;
  charge?: ChargeId;
  /** Turns before it can be used again. */
  cooldown?: number;
  /** Tiles, for tile, ally, corpse and psionic abilities. */
  range?: number;
  /** Blast radius in tiles. */
  radius?: number;
  /** Weapon whose damage an area ability uses. */
  explosive?: WeaponId;
  /** Attack variants: which weapon fires and how the shot changes. */
  weapon?: 'primary' | 'secondary' | 'melee';
  aim?: number;
  damageMult?: number;
  damageBonus?: number;
  /** Rounds it needs in the clip. */
  ammo?: number;
  /** Psionic: the victim's will resists it. */
  psi?: boolean;
  /** The Oficio: learnt in the Taller de oficios; quieter with it by the listening post (OFICIO_QUIET). */
  song?: boolean;
  /** The target counts as flanked: cover doesn't count (Flecha perforante). */
  ignoreCover?: boolean;
  /** Tile ability that moves the user there (Salto de eco, Paso de sombra). */
  teleport?: boolean;
  /**
   * Noise radius in tiles when it isn't its weapon's (docs/FASE2.md; see
   * abilities.ts noiseOf). Heard where it lands for tile abilities, where the
   * user stands for the rest.
   */
  noise?: number;
}

export const ABILITIES: Record<AbilityId, AbilityDef> = {
  shoot: { id: 'shoot', name: 'Disparar', description: 'Dispara el arma principal. Gasta una acción; el fusil de precisión, las dos.', target: 'enemy', ap: 1, endsTurn: false, weapon: 'primary' },
  pistol: { id: 'pistol', name: 'Pistola', description: 'Dispara la pistola: munición infinita. Gasta una acción.', target: 'enemy', ap: 1, endsTurn: false, weapon: 'secondary' },
  slash: { id: 'slash', name: 'Tajo', description: 'Corre hasta un enemigo a tu alcance y atácalo con el machete. Ignora la cobertura.', target: 'melee', ap: 1, endsTurn: true, weapon: 'melee' },
  stunLance: { id: 'stunLance', name: 'Red y porra', description: 'Atrapa con la red y golpea con la porra: aturde.', target: 'melee', ap: 1, endsTurn: true, weapon: 'melee' },
  claw: { id: 'claw', name: 'Mordisco', description: 'Muerde cuerpo a cuerpo.', target: 'melee', ap: 1, endsTurn: true, weapon: 'melee' },
  overwatch: { id: 'overwatch', name: 'Vigilancia', description: 'Dispara al primer enemigo que se mueva a la vista.', target: 'self', ap: 1, endsTurn: true },
  hunker: { id: 'hunker', name: 'Agazaparse', description: '+30 de defensa y +50 de esquiva hasta tu próximo turno.', target: 'self', ap: 1, endsTurn: true },
  reload: { id: 'reload', name: 'Recargar', description: 'Mete un cargador lleno y gasta uno de repuesto: primero en el arma principal y, si está llena, en la pistola. Los de repuesto los hace el Taller; registrando aparecen más.', target: 'self', ap: 1, endsTurn: false },
  grenade: { id: 'grenade', name: 'Granada', description: 'Lanza una granada: daña a todos en el área, destruye cobertura y abre boquetes en las paredes. Arriba hunde el suelo donde cae, y quien esté encima cae.', target: 'tile', ap: 1, endsTurn: true, charge: 'grenade', range: 10, radius: 2.5, explosive: 'fragGrenade' },
  launch: { id: 'launch', name: 'Lanzagranadas', description: 'Dispara una granada más lejos y con más radio. También abre boquetes en las paredes y, arriba, hunde el suelo donde cae.', target: 'tile', ap: 1, endsTurn: true, charge: 'grenade', range: 14, radius: 3.5, explosive: 'fragGrenade' },
  missiles: { id: 'missiles', name: 'Granada del chaleco', description: 'Le estalla una granada que aún cuelga del chaleco. También abre boquetes en las paredes y, arriba, hunde el suelo.', target: 'tile', ap: 1, endsTurn: true, charge: 'missiles', range: 15, radius: 2.5, explosive: 'microMissiles' },
  medkit: { id: 'medkit', name: 'Botiquín volante', description: 'El Grillo, el dron casero del especialista, cura 4 de salud a un aliado.', target: 'ally', ap: 1, endsTurn: false, charge: 'medkit', range: 15 },
  aid: { id: 'aid', name: 'Cobertura del Grillo', description: 'El Grillo protege a un aliado: +20 de defensa hasta vuestro próximo turno.', target: 'ally', ap: 1, endsTurn: false, range: 15 },
  evac: { id: 'evac', name: 'Evacuar', description: 'Desde La Lata, pide salir. Si tu compañero acepta, salen los que estén dentro y los de fuera se pierden. En la Limpieza es una retirada: la misión se pierde.', target: 'self', ap: 0, endsTurn: false },
  hack: {
    id: 'hack',
    name: 'Arreglar',
    description: 'Arregla el equipo (una radio, un repetidor…) desde una casilla contigua; el Grillo, el dron casero del especialista, llega a 10 casillas. Si falla, salta la alarma: se oye lejos y atrae a los muertos por los bordes.',
    target: 'self',
    ap: 1,
    endsTurn: true,
    range: 10,
    noise: 2,
  },
  // Lore's texts (docs/FASE8.md §B).
  search: {
    id: 'search',
    name: 'Abrir la caja',
    description: 'Abre una caja marcada que tengas al lado. Da siempre lo suyo: un arma, una o dos medicinas, dos cargadores o de 3 a 5 raciones. Hace un poco de ruido.',
    target: 'tile',
    ap: 1,
    endsTurn: false,
    range: 1.5,
    noise: 2,
  },
  searchRoom: {
    id: 'searchRoom',
    name: 'Registrar la habitación',
    description: 'Registra la habitación en la que estás. Sale lo que haya según el sitio: suministros, comida, piezas, medicinas, algún cargador y, a veces, un arma. Cada habitación se registra una vez, para toda la escuadra. Hace un poco de ruido.',
    target: 'self',
    ap: 1,
    endsTurn: false,
    noise: 2,
  },
  // A door beside the soldier, without moving: free, and as quiet as walking through it (NOISE.door). Texts are placeholders for Lore.
  openDoor: { id: 'openDoor', name: 'Abrir la puerta', description: 'Abre una puerta cerrada que tengas al lado. No gasta acciones.', target: 'tile', ap: 0, endsTurn: false, range: 1.5, noise: 1 },
  closeDoor: { id: 'closeDoor', name: 'Cerrar la puerta', description: 'Cierra una puerta abierta que tengas al lado, si no hay nadie en ella. No gasta acciones.', target: 'tile', ap: 0, endsTurn: false, range: 1.5, noise: 1 },
  runAndGun: { id: 'runAndGun', name: 'Correr y disparar', description: 'Acción gratuita: +1 acción este turno.', target: 'self', ap: 0, endsTurn: false, cooldown: 4 },
  rapidFire: { id: 'rapidFire', name: 'Ráfaga', description: 'Dos disparos seguidos, cada uno con −15 de puntería. Gasta dos balas.', target: 'enemy', ap: 1, endsTurn: false, cooldown: 3, weapon: 'primary', aim: -15, ammo: 2 },
  suppression: { id: 'suppression', name: 'Supresión', description: 'Inmoviliza al objetivo: −50 de puntería hasta tu próximo turno, y si se mueve, le disparas. Gasta 2 balas.', target: 'enemy', ap: 1, endsTurn: true, ammo: 2 },
  chainShot: { id: 'chainShot', name: 'Disparo en cadena', description: 'Disparo con −15 de puntería; si acierta, vuelves a disparar.', target: 'enemy', ap: 1, endsTurn: false, cooldown: 3, weapon: 'primary', aim: -15 },
  rupture: { id: 'rupture', name: 'Ruptura', description: 'Disparo con +3 de daño; el objetivo recibe +3 de daño de todos los ataques.', target: 'enemy', ap: 1, endsTurn: false, cooldown: 4, weapon: 'primary', damageBonus: 3 },
  aimedShot: { id: 'aimedShot', name: 'A la cabeza', description: 'Un disparo con −25 de puntería que hace la mitad más de daño.', target: 'enemy', ap: 1, endsTurn: false, cooldown: 3, weapon: 'primary', aim: -25, damageMult: 1.5 },
  lightningHands: { id: 'lightningHands', name: 'Manos rápidas', description: 'Acción gratuita: un disparo de pistola.', target: 'enemy', ap: 0, endsTurn: false, cooldown: 4, weapon: 'secondary' },
  faceoff: { id: 'faceoff', name: 'Cara a cara', description: 'Un disparo de pistola a cada enemigo que veas.', target: 'self', ap: 1, endsTurn: true, cooldown: 4, weapon: 'secondary' },
  killZone: { id: 'killZone', name: 'Nadie pasa', description: 'Vigilancia que dispara a cada enemigo que se mueva a la vista, una vez a cada uno.', target: 'self', ap: 1, endsTurn: true, cooldown: 4 },
  combatProtocol: { id: 'combatProtocol', name: 'Calambrazo', description: 'El Grillo electrocuta a un enemigo a la vista de la escuadra: 2 de daño (4 a Acorazados y alarmas). Nunca falla e ignora el blindaje. Termina el turno.', target: 'enemy', ap: 1, endsTurn: true, charge: 'combatProtocol', range: 15, noise: 2 },
  // Fase 9 (skills.ts): Espabilar without the Grillo's medkit, Pasar visita once a mission.
  revival: { id: 'revival', name: 'Espabilar', description: 'Quita a un compañero de al lado el aturdimiento, la desorientación o el pánico. Sin botiquín.', target: 'ally', ap: 1, endsTurn: false, range: 1.5 },
  restoration: { id: 'restoration', name: 'Pasar visita', description: 'Cura 2 a cada compañero a 4 casillas o menos y les quita los estados alterados. Una vez por misión.', target: 'self', ap: 1, endsTurn: true, charge: 'restoration', range: 4 },
  elbowRoom: { id: 'elbowRoom', name: 'Hacerse sitio', description: 'Un Tajo a cada enemigo que tengas al lado.', target: 'self', ap: 1, endsTurn: true, cooldown: 3, weapon: 'melee' },
  patchUp: { id: 'patchUp', name: 'Apaños', description: 'Arregla 40 de estado del arma propia o de un compañero de al lado. Una vez por misión.', target: 'ally', ap: 1, endsTurn: false, charge: 'patchUp', range: 1.5 },
  discharge: { id: 'discharge', name: 'Descarga de batería', description: 'El Grillo descarga en una zona: 3 de daño a todos los que estén dentro, aliados incluidos.', target: 'tile', ap: 1, endsTurn: true, cooldown: 4, range: 10, radius: 1.5, explosive: 'discharge' },
  mindspin: { id: 'mindspin', name: 'Grito', description: 'El Chillón grita: desorienta o asusta a los soldados cercanos, y todo zombi a 16 casillas lo oye y viene.', target: 'enemy', ap: 1, endsTurn: true, cooldown: 3, range: 14, psi: true, noise: 16 },
  reanimate: { id: 'reanimate', name: 'Despertar a un muerto', description: 'El grito del Chillón despierta un cadáver, que se levanta como Zombi.', target: 'corpse', ap: 1, endsTurn: true, cooldown: 4, range: 12, psi: true, noise: 8 },
  burst: { id: 'burst', name: 'Reventón', description: 'Al morir o al llegar a un soldado revienta: 3 de daño a quien tenga al lado.', target: 'self', ap: 1, endsTurn: true, radius: 1.5, explosive: 'bloaterBurst' },
  smoke: { id: 'smoke', name: 'Granada de humo', description: 'Cubre una zona de humo durante 2 turnos: −20 a la puntería contra quien esté dentro.', target: 'tile', ap: 1, endsTurn: true, charge: 'smoke', range: 10, radius: 2, noise: 4 },
  flashbang: { id: 'flashbang', name: 'Granada aturdidora', description: 'Desorienta a los enemigos de la zona (−20 de puntería, sin explosivos). No hace daño.', target: 'tile', ap: 1, endsTurn: true, charge: 'flashbang', range: 10, radius: 2.5, noise: 18 },
  firstAid: { id: 'firstAid', name: 'Botiquín', description: 'Cura 4 de salud a uno mismo o a un compañero de al lado.', target: 'ally', ap: 1, endsTurn: false, charge: 'firstAid', range: 1.5 },
  // The Oficio: learnt in the Taller de oficios. Each makes its own noise; none gives the squad away by itself.
  aegis: { id: 'aegis', name: 'Escudo antidisturbios', description: 'Tú y los aliados contiguos ganáis +20 de defensa hasta vuestro próximo turno.', target: 'self', ap: 1, endsTurn: false, cooldown: 3, song: true, noise: 0 },
  echoStep: { id: 'echoStep', name: 'Saltar por la ventana', description: 'Saltas a una casilla libre a la vista, a 6 o menos, por encima de lo que haya en medio. No termina el turno.', target: 'tile', ap: 1, endsTurn: false, cooldown: 4, range: 6, radius: 0, teleport: true, song: true, noise: 1 },
  layOnHands: { id: 'layOnHands', name: 'Vendaje rápido', description: 'Cura 3 a un aliado contiguo o a ti mismo y le quita el pánico y la desorientación.', target: 'ally', ap: 1, endsTurn: false, charge: 'layOnHands', range: 1.5, song: true, noise: 0 },
  velmarOath: { id: 'velmarOath', name: '¡Aguantad!', description: 'Durante 2 turnos, los aliados a 4 casillas no pueden entrar en pánico ni caer bajo el grito del Chillón, y tú recibes la mitad del daño.', target: 'self', ap: 1, endsTurn: false, charge: 'velmarOath', range: 4, song: true, noise: 2 },
  bolt: { id: 'bolt', name: 'Bengala', description: 'Una bengala encendida a un enemigo a la vista: 3 de daño. Nunca falla e ignora la cobertura, pero no el blindaje.', target: 'enemy', ap: 1, endsTurn: true, cooldown: 2, range: 20, song: true, noise: 6 },
  silence: {
    id: 'silence',
    name: 'Petardos',
    description: 'Una traca a 10 casillas o menos: suena donde cae (ruido 12) y atrae a los zombis hacia allí. Los enemigos en radio 2 quedan desorientados y los Chillones no pueden gritar durante 2 turnos.',
    target: 'tile',
    ap: 1,
    endsTurn: true,
    charge: 'silence',
    range: 10,
    radius: 2,
    song: true,
    noise: 12,
  },
  fireball: { id: 'fireball', name: 'Cóctel molotov', description: '4 de daño en radio 2,5 y destruye la cobertura. Daña también a los tuyos.', target: 'tile', ap: 1, endsTurn: true, charge: 'fireball', range: 12, radius: 2.5, explosive: 'songFire', song: true },
  velmarStorm: { id: 'velmarStorm', name: 'El bidón', description: 'Haces estallar un bidón de gasolina y el fuego alcanza hasta 3 enemigos a la vista: 4 de daño a cada uno. Nunca falla.', target: 'self', ap: 1, endsTurn: true, charge: 'velmarStorm', song: true, noise: 18 },
  huntersMark: { id: 'huntersMark', name: 'Rastreo', description: 'Tus disparos al enemigo rastreado tienen +15 de puntería y +15 de crítico hasta que muera.', target: 'enemy', ap: 1, endsTurn: false, cooldown: 3, range: 30, song: true, noise: 0 },
  spectralArrow: { id: 'spectralArrow', name: 'Flecha perforante', description: 'Disparo que atraviesa la cobertura: el objetivo cuenta como flanqueado, con −10 de puntería.', target: 'enemy', ap: 1, endsTurn: false, cooldown: 3, weapon: 'primary', aim: -10, ignoreCover: true, song: true, noise: 1 },
  shadowStep: { id: 'shadowStep', name: 'Encaramarse', description: 'Trepas o saltas a una casilla a la vista, a 8 o menos, por encima de lo que haya en medio. Si está más alta, ganas +10 de puntería este turno.', target: 'tile', ap: 1, endsTurn: false, cooldown: 4, range: 8, radius: 0, teleport: true, song: true, noise: 1 },
  lightRain: { id: 'lightRain', name: 'Lluvia de flechas', description: 'Una flecha perforante a cada enemigo a la vista, con −15 de puntería.', target: 'self', ap: 1, endsTurn: true, charge: 'lightRain', weapon: 'primary', aim: -15, ignoreCover: true, song: true, noise: 1 },
  counterSong: { id: 'counterSong', name: 'Sedante', description: 'Libera a un aliado del grito del Chillón, el pánico y la desorientación, y lo hace inmune al grito durante 2 turnos.', target: 'ally', ap: 1, endsTurn: false, cooldown: 3, range: 10, song: true, noise: 0 },
  hymn: { id: 'hymn', name: 'Arenga', description: 'Los aliados a 6 casillas o menos ganan +10 de puntería y +10 de defensa hasta vuestro próximo turno.', target: 'self', ap: 1, endsTurn: false, cooldown: 4, range: 6, song: true, noise: 2 },
  lastVerse: { id: 'lastVerse', name: 'Desfibrilador', description: 'Un soldado caído este turno vuelve en pie con 3 de salud. Si no hay ninguno, cura 4 a todos los aliados a 6 casillas.', target: 'self', ap: 1, endsTurn: true, charge: 'lastVerse', range: 6, song: true, noise: 0 },
};

/** Base chance of hacking the objective terminal; specialists and rank add to it. */
export const HACK_BASE = 55;
export const HACK_SPECIALIST = 25;
export const HACK_PER_RANK = 3;

/** Mindspin: success chance is PSI_BASE − (will − 40), then the effect is rolled. */
export const PSI_BASE = 70;
export const DISORIENTED_AIM = 20;
export const SUPPRESSED_AIM = 50;
export const HOLO_AIM = 15;
export const RUPTURE_DAMAGE = 3;
/** Turns a mind-controlled soldier fights for the aliens (ends early if the sectoid dies). */
export const CONTROL_TURNS = 2;
export const RESTORATION_HEAL = 2;
export const SMOKE_DEFENSE = 20;
export const SMOKE_TURNS = 2;

// ------------------------------------------------------------------ items

/** Things a soldier can carry back from searching (docs/FASE2.md): a full backpack can't search. */
export const BACKPACK_SLOTS = 3;
/** What a medicine heals (docs/FASE8.md §C): only health; stopping a bite's contagion is unlocked. */
export const MEDICINE_HEAL = 3;

/** Rounds in a pistol's clip when it counts them (docs/FASE8.md §A: the campaign's soldiers; in a skirmish it never runs out). */
export const PISTOL_CLIP = 6;

export interface FoundWeaponDef {
  id: FoundWeaponId;
  /** As the soldier's card reads it («Fusil de caza · nivel 3»). */
  name: string;
  /** As it reads when found, with its article. */
  find: string;
  /** The weapon it fires as. */
  weapon: WeaponId;
  /** In the hand (fusil, escopeta, fusil de precisión, ametralladora) or at the belt (the machete). */
  slot: 'primary' | 'melee';
  /** A good one: it comes with an improvement mounted (`mod`, docs/FASE9.md §5). */
  better: boolean;
  mod?: WeaponModId;
  /** How often it turns up in a weapons box or a search, against the others (0: never). */
  weight: number;
}

/**
 * The Armería's weapons (docs/FASE8.md §A): the campaign's soldiers start with a
 * knife and a pistol, and these are found, made in the Taller or brought by the
 * Bastion's level. Anyone may carry any of them. The good ones (docs/FASE4.md)
 * fight a tier above. Names are placeholders for Lore.
 */
export const FOUND_WEAPONS: Record<FoundWeaponId, FoundWeaponDef> = {
  assaultRifle: { id: 'assaultRifle', name: 'Fusil de asalto', find: 'un fusil de asalto', weapon: 'rifle', slot: 'primary', better: false, weight: 8 },
  pumpShotgun: { id: 'pumpShotgun', name: 'Escopeta', find: 'una escopeta', weapon: 'shotgun', slot: 'primary', better: false, weight: 6 },
  precisionRifle: { id: 'precisionRifle', name: 'Fusil de precisión', find: 'un fusil de precisión', weapon: 'sniper', slot: 'primary', better: false, weight: 3 },
  machineGun: { id: 'machineGun', name: 'Ametralladora', find: 'una ametralladora', weapon: 'cannon', slot: 'primary', better: false, weight: 3 },
  machete: { id: 'machete', name: 'Machete', find: 'un machete', weapon: 'sword', slot: 'melee', better: false, weight: 0 },
  huntingRifle: { id: 'huntingRifle', name: 'Fusil de cerrojo', find: 'un fusil de cerrojo bien engrasado', weapon: 'rifle', slot: 'primary', better: true, mod: 'stock', weight: 3 },
  sawnOff: { id: 'sawnOff', name: 'Escopeta recortada', find: 'una escopeta recortada', weapon: 'shotgun', slot: 'primary', better: true, mod: 'extMag', weight: 2 },
  scopedRifle: { id: 'scopedRifle', name: 'Fusil con mira', find: 'un fusil con mira telescópica', weapon: 'sniper', slot: 'primary', better: true, mod: 'scope', weight: 1 },
  lightMachineGun: { id: 'lightMachineGun', name: 'Ametralladora ligera', find: 'una ametralladora ligera con su cinta', weapon: 'cannon', slot: 'primary', better: true, mod: 'extMag', weight: 1 },
};

// ------------------------------------------------------- condition and mods

/** A weapon's improvements (docs/FASE9.md §5; names and lines, Lore's). */
export const WEAPON_MODS: Record<WeaponModId, { id: WeaponModId; name: string; line: string; none: string }> = {
  scope: { id: 'scope', name: 'Mira', line: '+10 de puntería.', none: 'ninguna mira' },
  stock: { id: 'stock', name: 'Culata', line: '+10 de puntería en vigilancia: el arma apoyada no tiembla.', none: 'ninguna culata' },
  extMag: { id: 'extMag', name: 'Cargador ampliado', line: 'Dos balas más en cada cargador.', none: 'ningún cargador ampliado' },
  silencer: { id: 'silencer', name: 'Silenciador', line: 'El disparo hace la mitad de ruido. Casero: un tubo, arandelas y cinta.', none: 'ningún silenciador' },
};
export const MOD_SLOTS = 2;
export const MOD_AIM = 10;
export const EXT_MAG = 2;
/** Below this a weapon is «Gastada»: −WORN_AIM aim. At 0, «Rota»: it doesn't fire. */
export const WORN_BELOW = 30;
export const WORN_AIM = 10;
/** The Armería mends this much condition for every part. */
export const REPAIR_PER_PART = 25;
/** What Apaños mends in the field (docs/FASE9.md). */
export const PATCH_UP = 40;

/** What a shot takes off a weapon's condition: the precision rifle two, the rest one (the machine gun, one per burst). */
export function wearOf(weapon: WeaponId): number {
  return weapon === 'sniper' ? 2 : 1;
}

/** Whether `ability` fires the primary weapon a soldier carries (not the pistol beside it, not a blade). */
export function firesPrimary(u: Pick<Unit, 'arms'>, ability: AbilityId): boolean {
  return !!u.arms?.primary && (ABILITIES[ability].weapon ?? 'primary') === 'primary' && (isAttack(ability) || ability === 'overwatch' || ability === 'killZone' || ability === 'suppression');
}

/** The condition of the primary weapon a soldier carries (100 for none, or none told). */
export function conditionOf(u: Pick<Unit, 'arms'>): number {
  return u.arms?.primary ? (u.arms.condition ?? 100) : 100;
}

export function hasMod(u: Pick<Unit, 'arms'>, mod: WeaponModId): boolean {
  return !!u.arms?.primary && !!u.arms.mods?.includes(mod);
}

/** The Armería's plain weapon of each class weapon: what an older campaign's soldiers keep (docs/FASE8.md §A). */
export const PLAIN_WEAPON: Partial<Record<WeaponId, FoundWeaponId>> = {
  rifle: 'assaultRifle',
  shotgun: 'pumpShotgun',
  sniper: 'precisionRifle',
  cannon: 'machineGun',
  sword: 'machete',
};

export interface ItemDef {
  id: ItemId;
  name: string;
  description: string;
  slot: ItemSlot;
  /** Price in credits. */
  cost: number;
  /** Bastion level that unlocks it in the armoury. */
  level: number;
  /** Uses it adds in a mission. */
  charges?: Partial<Record<ChargeId, number>>;
  /** Ability the item grants. */
  ability?: AbilityId;
  mods?: Partial<UnitMods>;
  hp?: number;
}

export const ITEMS: Record<ItemId, ItemDef> = {
  // The belt (docs/FASE9.md §4): two of these; ammunition goes into the weapons' mods instead.
  fragGrenade: { id: 'fragGrenade', name: 'Granadas', description: 'Dos granadas de fragmentación, en el cinturón. Quien no las lleva, no puede lanzar.', slot: 'utility', cost: 20, level: 1, charges: { grenade: 2 } },
  smokeGrenade: { id: 'smokeGrenade', name: 'Granada de humo', description: 'Humo durante 2 turnos: −20 a la puntería contra quien esté dentro.', slot: 'utility', cost: 25, level: 1, charges: { smoke: 1 }, ability: 'smoke' },
  firstAid: { id: 'firstAid', name: 'Botiquín', description: 'Dos usos: cura 4 a uno mismo o a un compañero de al lado.', slot: 'utility', cost: 25, level: 1, charges: { firstAid: 2 }, ability: 'firstAid' },
  flashbang: { id: 'flashbang', name: 'Granada aturdidora', description: 'Desorienta a los enemigos de la zona.', slot: 'utility', cost: 30, level: 2, charges: { flashbang: 1 }, ability: 'flashbang' },
  tracerRounds: { id: 'tracerRounds', name: 'Munición trazadora', description: '+10 de puntería.', slot: 'ammo', cost: 35, level: 2, mods: { aim: 10 } },
  nanoVest: { id: 'nanoVest', name: 'Chaleco antibalas', description: '+2 de salud.', slot: 'utility', cost: 50, level: 4, hp: 2 },
  apRounds: { id: 'apRounds', name: 'Munición perforante', description: 'El arma principal ignora 1 punto de blindaje.', slot: 'ammo', cost: 55, level: 4, mods: { pierce: 1 } },
  mindShield: { id: 'mindShield', name: 'Tapones', description: 'Cera en los oídos: el grito del Chillón no te afecta.', slot: 'utility', cost: 75, level: 6, mods: { will: 100 } },
};

// ---------------------------------------------------------------- templates

export interface UnitTemplate {
  id: TemplateId;
  name: string;
  /** Short label for compact UI. */
  short: string;
  team: Team;
  hp: number;
  armor: number;
  aim: number;
  defense: number;
  dodge: number;
  /** Tiles per action point (XCOM 2 mobility 12 m / 1.5). */
  mobility: number;
  /** Sight radius in tiles (27 m / 1.5). */
  sight: number;
  /** While XCOM is concealed, this unit only notices soldiers this close (12 m / 1.5). */
  detection: number;
  crit: number;
  flankCrit: number;
  weapon: WeaponId;
  /** Pistol for snipers. */
  secondary?: WeaponId;
  /** Blade, lance or claws. */
  meleeWeapon?: WeaponId;
  abilities: AbilityId[];
  charges: Partial<Record<ChargeId, number>>;
  squadsight: boolean;
  will: number;
  /** Machines: immune to psionics, can't be raised, take more from Combat Protocol. */
  robotic: boolean;
  /** Zombies (docs/FASE2.md): melee only, never take cover, the horde's AI (ai.ts). */
  undead: boolean;
}

const soldier = (t: Pick<UnitTemplate, 'id' | 'name' | 'short' | 'weapon' | 'abilities' | 'charges'> & Partial<UnitTemplate>): UnitTemplate => ({
  team: 'xcom',
  hp: 5,
  armor: 0,
  aim: 65,
  defense: 0,
  dodge: 0,
  mobility: 8,
  sight: 18,
  detection: 0,
  crit: 0,
  flankCrit: 50,
  squadsight: false,
  will: 40,
  robotic: false,
  undead: false,
  ...t,
});

const alien = (t: Pick<UnitTemplate, 'id' | 'name' | 'short' | 'weapon' | 'abilities'> & Partial<UnitTemplate>): UnitTemplate => ({
  team: 'alien',
  hp: 3,
  armor: 0,
  aim: 65,
  defense: 0,
  dodge: 0,
  mobility: 8,
  sight: 18,
  detection: 8,
  crit: 0,
  flankCrit: 33,
  charges: {},
  squadsight: false,
  will: 60,
  robotic: false,
  undead: false,
  ...t,
});

export const TEMPLATES: Record<TemplateId, UnitTemplate> = {
  assault: soldier({
    id: 'assault', name: 'Asalto', short: 'ASL', weapon: 'shotgun', meleeWeapon: 'sword',
    abilities: ['shoot', 'slash', 'overwatch', 'grenade', 'reload', 'hunker'], charges: { grenade: 1 },
  }),
  grenadier: soldier({
    id: 'grenadier', name: 'Granadero', short: 'GRN', weapon: 'cannon', hp: 6,
    abilities: ['shoot', 'launch', 'overwatch', 'reload', 'hunker'], charges: { grenade: 2 },
  }),
  sharpshooter: soldier({
    id: 'sharpshooter', name: 'Francotirador', short: 'FRT', weapon: 'sniper', secondary: 'pistol', aim: 70, squadsight: true,
    abilities: ['shoot', 'pistol', 'overwatch', 'grenade', 'reload', 'hunker'], charges: { grenade: 1 },
  }),
  // A recruit with no class yet: rifle, a grenade and the basic actions (classes.ts specialises them).
  rookie: soldier({
    id: 'rookie', name: 'Recluta', short: 'RCL', weapon: 'rifle',
    abilities: ['shoot', 'overwatch', 'grenade', 'reload', 'hunker'], charges: { grenade: 1 },
  }),
  specialist: soldier({
    id: 'specialist', name: 'Especialista', short: 'ESP', weapon: 'rifle',
    abilities: ['shoot', 'medkit', 'aid', 'overwatch', 'grenade', 'reload', 'hunker'], charges: { grenade: 1, medkit: 2 },
  }),
  // The living: they take cover and shoot (ai.ts takeAlienTurn), and hear the noise.
  trooper: alien({ id: 'trooper', name: 'Saqueador', short: 'SAQ', weapon: 'trooperRifle', abilities: ['shoot', 'overwatch', 'reload', 'grenade'], charges: { grenade: 1 } }),
  officer: alien({ id: 'officer', name: 'Cabecilla', short: 'CAB', weapon: 'officerRifle', hp: 6, abilities: ['shoot', 'overwatch', 'reload'] }),
  lancer: alien({ id: 'lancer', name: 'Cazador', short: 'CAZ', weapon: 'lancerRifle', meleeWeapon: 'stunLance', hp: 4, abilities: ['stunLance', 'shoot', 'reload'] }),
  // The dead (docs/FASE2.md): claws and teeth, no cover, the horde's AI (ai.ts takeZombieTurn).
  // Harder to put down (playtest 2026-10-15, #12): two more points of health each.
  zombie: alien({ id: 'zombie', name: 'Zombi', short: 'ZOM', weapon: 'claws', meleeWeapon: 'claws', hp: 8, aim: 60, defense: -10, mobility: 4, sight: 14, detection: 6, will: 100, undead: true, abilities: ['claw'] }),
  xenoid: alien({ id: 'xenoid', name: 'Corredor', short: 'COR', weapon: 'xenoPistol', meleeWeapon: 'xenoPistol', hp: 7, aim: 60, mobility: 8, detection: 10, will: 100, undead: true, abilities: ['claw'] }),
  mec: alien({ id: 'mec', name: 'Acorazado', short: 'ACO', weapon: 'mecCannon', meleeWeapon: 'mecCannon', hp: 9, armor: 1, aim: 60, mobility: 4, sight: 14, detection: 6, robotic: true, will: 100, undead: true, abilities: ['claw'] }),
  /** The Chillón doesn't bite: it screams (mindspin, noise 16) and raises the dead. */
  sectoid: alien({ id: 'sectoid', name: 'Chillón', short: 'CHI', weapon: 'sectoidPistol', hp: 7, mobility: 5, will: 100, undead: true, abilities: ['mindspin', 'reanimate'] }),
  /** The Hinchado: slow; it bursts when it dies or reaches a soldier (rules.ts burst). */
  bloater: alien({ id: 'bloater', name: 'Hinchado', short: 'HIN', weapon: 'bloaterBurst', hp: 8, aim: 0, defense: -15, mobility: 3, sight: 12, detection: 5, will: 100, undead: true, abilities: ['burst'] }),
  // Objectives. The relay is an alien "unit" so every weapon can hit it; it never acts.
  relay: alien({ id: 'relay', name: 'Alarma', short: 'ALA', weapon: 'trooperRifle', hp: 8, armor: 1, aim: 0, mobility: 0, sight: 0, detection: 0, robotic: true, abilities: [] }),
  vip: soldier({ id: 'vip', name: 'VIP', short: 'VIP', weapon: 'pistol', hp: 4, aim: 50, will: 30, abilities: ['hunker'], charges: {} }),
};

/** Default kits: each player gets a mix, a solo player gets all six. */
/** Soldiers in a skirmish squad. */
export const SQUAD_SIZE = 6;

const ATTACKS: ReadonlySet<AbilityId> = new Set<AttackAbility>([
  'shoot', 'pistol', 'slash', 'stunLance', 'claw', 'rapidFire', 'chainShot', 'aimedShot', 'rupture', 'lightningHands', 'faceoff', 'spectralArrow', 'lightRain',
]);

/** Abilities that fire a weapon (one roll to hit per shot). */
export function isAttack(ability: AbilityId): ability is AttackAbility {
  return ATTACKS.has(ability);
}

/** Weapon used by an attack ability. A campaign soldier's are its own (Unit.arms): the primary, or the pistol without one; the knife without a machete. */
export function attackWeapon(u: Unit, ability: AttackAbility = 'shoot'): WeaponDef {
  if (u.arms) {
    switch (ABILITIES[ability].weapon) {
      case 'secondary':
        return WEAPONS.pistol;
      case 'melee':
        return WEAPONS[u.arms.melee ? FOUND_WEAPONS[u.arms.melee].weapon : 'knife'];
      default:
        return WEAPONS[u.arms.primary ? FOUND_WEAPONS[u.arms.primary].weapon : 'pistol'];
    }
  }
  const t = TEMPLATES[u.template];
  switch (ABILITIES[ability].weapon) {
    case 'secondary':
      return WEAPONS[t.secondary ?? t.weapon];
    case 'melee':
      return WEAPONS[t.meleeWeapon ?? t.weapon];
    default:
      return WEAPONS[t.weapon];
  }
}

/** Rounds in a full clip of the weapon `ability` fires; 0, it never runs out. A campaign soldier's pistol counts them (docs/FASE8.md §A). */
export function clipOf(u: Unit, ability: AttackAbility = 'shoot'): number {
  const w = attackWeapon(u, ability);
  if (u.arms && w.id === 'pistol') return PISTOL_CLIP;
  return w.clip + (firesPrimary(u, ability) && hasMod(u, 'extMag') ? EXT_MAG : 0);
}

/** Whether `ability` fires the pistol carried beside a primary weapon: it has its own rounds (Unit.pistolAmmo). */
export function firesSidearm(u: Unit, ability: AttackAbility): boolean {
  return !!u.arms?.primary && attackWeapon(u, ability).id === 'pistol';
}

/** Rounds left for `ability`'s weapon. */
export function roundsOf(u: Unit, ability: AttackAbility = 'shoot'): number {
  return firesSidearm(u, ability) ? (u.pistolAmmo ?? 0) : u.ammo;
}

/** Whether `ability` can fire now: its weapon never runs out, or it has a round left. */
export function loadedFor(u: Unit, ability: AttackAbility = 'shoot'): boolean {
  // A broken weapon doesn't fire (docs/FASE9.md §5), loaded or not.
  if (firesPrimary(u, ability) && conditionOf(u) <= 0) return false;
  return clipOf(u, ability) === 0 || roundsOf(u, ability) > 0;
}

/** Reloading: the main gun if it's short of a full clip, otherwise the pistol carried beside it (docs/FASE8.md §A). */
export function reloadWeapon(u: Unit): void {
  const main = clipOf(u, 'shoot');
  if (u.ammo < main || !u.arms?.primary) u.ammo = main;
  else u.pistolAmmo = PISTOL_CLIP;
}

/** Whether every gun the soldier carries is full. */
export function fullyLoaded(u: Unit): boolean {
  return u.ammo >= clipOf(u, 'shoot') && (!u.arms?.primary || (u.pistolAmmo ?? 0) >= PISTOL_CLIP);
}

/** Squadsight: the class's, and with arms of its own only with a fusil de precisión in hand (docs/FASE8.md §A). */
export function hasSquadsight(u: Unit): boolean {
  return TEMPLATES[u.template].squadsight && (!u.arms || attackWeapon(u, 'shoot').id === 'sniper');
}

/**
 * Soldiers' names (Lore, 2026-10-13: people from everywhere, half women and
 * half men), each with the sex the client picks a model of (Appearance.sex).
 */
export const SOLDIER_NAMES: { name: string; sex: 'f' | 'm' }[] = [
  { name: 'Chioma Okafor', sex: 'f' }, { name: 'Kofi Mensah', sex: 'm' }, { name: 'Yuki Tanaka', sex: 'f' },
  { name: 'Jonas Becker', sex: 'm' }, { name: 'Leila Mansour', sex: 'f' }, { name: 'Arjun Mehta', sex: 'm' },
  { name: 'Sofia Marin', sex: 'f' }, { name: 'Dmitri Volkov', sex: 'm' }, { name: 'Priya Nair', sex: 'f' },
  { name: 'Mateo Rojas', sex: 'm' }, { name: 'Anja Kowalska', sex: 'f' }, { name: 'Hiro Sato', sex: 'm' },
  { name: 'Elena Petrova', sex: 'f' }, { name: 'Omar Saleh', sex: 'm' }, { name: 'Samira Benali', sex: 'f' },
  { name: "Liam O'Brien", sex: 'm' }, { name: 'Maya Cohen', sex: 'f' }, { name: 'Pierre Lambert', sex: 'm' },
  { name: 'Chiara Rossi', sex: 'f' }, { name: 'Ivan Horvat', sex: 'm' }, { name: 'Freya Lund', sex: 'f' },
  { name: 'Rafael Costa', sex: 'm' }, { name: 'Ana Ríos', sex: 'f' }, { name: 'Erik Nilsson', sex: 'm' },
  { name: 'Zainab Bello', sex: 'f' }, { name: 'Samuel Adeyemi', sex: 'm' }, { name: 'Hana Novak', sex: 'f' },
  { name: 'Wei Zhang', sex: 'm' }, { name: 'Lucía Ortega', sex: 'f' }, { name: 'Luca Bianchi', sex: 'm' },
  { name: 'Ingrid Berg', sex: 'f' }, { name: 'Marcus Reid', sex: 'm' }, { name: 'Mariana Duarte', sex: 'f' },
  { name: 'Tariq Rahimi', sex: 'm' }, { name: 'Aisha Diallo', sex: 'f' }, { name: 'Pablo Rey', sex: 'm' },
];

/**
 * Names older saves may hold, for sexOfName: the soldiers before 2026-10-13,
 * those renamed on 2026-10-14 (they clashed with someone else's), and the
 * relative of «Caras conocidas» retired that day, matched by first name.
 */
const OLD_SOLDIER_NAMES: { name: string; sex: 'f' | 'm' }[] = [
  { name: 'Ana Ríos', sex: 'f' }, { name: 'Luis Varela', sex: 'm' }, { name: 'Marta Soler', sex: 'f' },
  { name: 'Iker Ortega', sex: 'm' }, { name: 'Nora Campos', sex: 'f' }, { name: 'Hugo Lázaro', sex: 'm' },
  { name: 'Elena Vidal', sex: 'f' }, { name: 'Darío Pons', sex: 'm' }, { name: 'Sara Mena', sex: 'f' },
  { name: 'Raúl Ibarra', sex: 'm' }, { name: 'Lucía Prat', sex: 'f' }, { name: 'Óscar Nieto', sex: 'm' },
  { name: 'Irene Galán', sex: 'f' }, { name: 'Pablo Rey', sex: 'm' }, { name: 'Clara Bosch', sex: 'f' },
  { name: 'Adrián Gil', sex: 'm' }, { name: 'Julia Font', sex: 'f' }, { name: 'Marcos Vela', sex: 'm' },
  { name: 'Amara Okafor', sex: 'f' }, { name: 'Anja Kowalski', sex: 'f' }, { name: 'Nadia Benali', sex: 'f' },
  { name: 'Pierre Dubois', sex: 'm' }, { name: 'Mariana Costa', sex: 'f' }, { name: 'Tariq Aziz', sex: 'm' },
  { name: 'Kenji', sex: 'm' },
];

/**
 * The sex a name goes with: the whole name in SOLDIER_NAMES or the old list,
 * or else its first word against their first names; undefined when nothing
 * matches (a name the players chose).
 */
export function sexOfName(name: string): 'f' | 'm' | undefined {
  const all = [...SOLDIER_NAMES, ...OLD_SOLDIER_NAMES];
  const whole = all.find((n) => n.name === name);
  if (whole) return whole.sex;
  const first = name.trim().split(/\s+/)[0];
  return all.find((n) => n.name.split(' ')[0] === first)?.sex;
}

// Violet and orange: apart from the interfaces' own blues and cyans, and from the aliens' red.
export const PLAYER_COLORS: [string, string] = ['#a678ff', '#ffa630'];

// ------------------------------------------------------------ ranks & perks

export const ZERO_MODS: UnitMods = {
  aim: 0,
  crit: 0,
  defense: 0,
  mobility: 0,
  damage: 0,
  meleeDamage: 0,
  pistolAim: 0,
  closeRangeAim: 0,
  closeRangeCrit: 0,
  grenadeDamage: 0,
  grenadeRadius: 0,
  heal: 0,
  aid: 0,
  meleeAim: 0,
  flankDamage: 0,
  shred: 0,
  will: 0,
  pierce: 0,
  dodge: 0,
  critDamage: 0,
  meleeCrit: 0,
  pistolDamage: 0,
  pistolCrit: 0,
  closeDamage: 0,
  heightAim: 0,
  reactionKeep: 0,
  reactionDefense: 0,
  watchDefense: 0,
  squadsightKeep: 0,
  pairAim: 0,
  pairDefense: 0,
  spotterAim: 0,
  grenadeShred: 0,
  grenadeRange: 0,
  holoAim: 0,
  suppressAim: 0,
  protocolDamage: 0,
  hack: 0,
  psiGuard: 0,
  singer: 0,
  slashDisorient: 0,
  songDamage: 0,
  sight: 0,
  smokeSight: 0,
  calmAura: 0,
  gunAim: 0,
  gunCrit: 0,
  quiet: 0,
};

export function hasPerk(u: Pick<Unit, 'perks'>, id: PerkId): boolean {
  return u.perks.includes(id);
}

export function addMods(a: UnitMods, b: Partial<UnitMods>): UnitMods {
  const out = { ...a };
  for (const [k, v] of Object.entries(b) as [keyof UnitMods, number][]) out[k] += v;
  return out;
}
