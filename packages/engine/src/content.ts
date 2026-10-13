// Gameplay data. Combat numbers mirror XCOM 2's defaults (XComGameCore.ini /
// XComGameData_*.ini) converted to tiles (1 tile = 1.5 m), so the game "feels"
// like the original before we start tuning our own values.
import type { AbilityId, AttackAbility, ChargeId, ClassId, ItemId, ItemSlot, PerkId, Team, TemplateId, Unit, UnitMods } from './types';

export const AP_PER_TURN = 2;

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
export const HORDE = { perTurn: 10, perNoise: 5, wave: 50, cap: 20 } as const;

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
  cannon: weapon({ id: 'cannon', name: 'Cañón', damage: 5, spread: 1, critDamage: 2, clip: 3, burst: 5, noise: 14 }),
  sniper: weapon({ id: 'sniper', name: 'Fusil de precisión', damage: 5, spread: 1, critDamage: 2, clip: 3, crit: 10, range: 'long', apCost: 2, burst: 1, noise: 16 }),
  pistol: weapon({ id: 'pistol', name: 'Pistola', damage: 2, plusOne: 50, critDamage: 1, burst: 1, noise: 8 }),
  sword: weapon({ id: 'sword', name: 'Espada', damage: 4, spread: 1, critDamage: 2, aim: 20, crit: 10, range: 'none', melee: true, burst: 1, noise: 1 }),
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
  slash: { id: 'slash', name: 'Tajo', description: 'Corre hasta un enemigo a tu alcance y atácalo con la espada. Ignora la cobertura.', target: 'melee', ap: 1, endsTurn: true, weapon: 'melee' },
  stunLance: { id: 'stunLance', name: 'Red y porra', description: 'Atrapa con la red y golpea con la porra: aturde.', target: 'melee', ap: 1, endsTurn: true, weapon: 'melee' },
  claw: { id: 'claw', name: 'Mordisco', description: 'Muerde cuerpo a cuerpo.', target: 'melee', ap: 1, endsTurn: true, weapon: 'melee' },
  overwatch: { id: 'overwatch', name: 'Vigilancia', description: 'Dispara al primer enemigo que se mueva a la vista.', target: 'self', ap: 1, endsTurn: true },
  hunker: { id: 'hunker', name: 'Agazaparse', description: '+30 de defensa y +50 de esquiva hasta tu próximo turno.', target: 'self', ap: 1, endsTurn: true },
  reload: { id: 'reload', name: 'Recargar', description: 'Llena el cargador.', target: 'self', ap: 1, endsTurn: false },
  grenade: { id: 'grenade', name: 'Granada', description: 'Lanza una granada: daña a todos en el área y destruye cobertura.', target: 'tile', ap: 1, endsTurn: true, charge: 'grenade', range: 10, radius: 2.5, explosive: 'fragGrenade' },
  launch: { id: 'launch', name: 'Lanzagranadas', description: 'Dispara una granada más lejos y con más radio.', target: 'tile', ap: 1, endsTurn: true, charge: 'grenade', range: 14, radius: 3.5, explosive: 'fragGrenade' },
  missiles: { id: 'missiles', name: 'Granada del chaleco', description: 'Le estalla una granada que aún cuelga del chaleco.', target: 'tile', ap: 1, endsTurn: true, charge: 'missiles', range: 15, radius: 2.5, explosive: 'microMissiles' },
  medkit: { id: 'medkit', name: 'Botiquín volante', description: 'El Grillo, el dron casero del especialista, cura 4 de salud a un aliado.', target: 'ally', ap: 1, endsTurn: false, charge: 'medkit', range: 15 },
  aid: { id: 'aid', name: 'Cobertura del Grillo', description: 'El Grillo protege a un aliado: +20 de defensa hasta vuestro próximo turno.', target: 'ally', ap: 1, endsTurn: false, range: 15 },
  evac: { id: 'evac', name: 'Evacuar', description: 'Sale de la misión desde la zona de evacuación.', target: 'self', ap: 1, endsTurn: true },
  hack: {
    id: 'hack',
    name: 'Arreglar',
    description: 'Arregla el equipo (una radio, un repetidor…) desde una casilla contigua; el Grillo, el dron casero del especialista, llega a 10 casillas. Si falla, salta la alarma y llegan refuerzos.',
    target: 'self',
    ap: 1,
    endsTurn: true,
    range: 10,
    noise: 2,
  },
  search: {
    id: 'search',
    name: 'Registrar',
    description: 'Registra un mueble, un coche o unas cajas a tu lado: suministros, algún accesorio y, a veces, medicinas. Hace un poco de ruido. La mochila lleva 3 cosas.',
    target: 'tile',
    ap: 1,
    endsTurn: false,
    range: 1.5,
    noise: 2,
  },
  runAndGun: { id: 'runAndGun', name: 'Correr y disparar', description: 'Acción gratuita: +1 acción este turno.', target: 'self', ap: 0, endsTurn: false, cooldown: 4 },
  rapidFire: { id: 'rapidFire', name: 'Fuego rápido', description: 'Dos disparos seguidos, cada uno con −15 de puntería. Gasta 2 balas.', target: 'enemy', ap: 1, endsTurn: false, cooldown: 3, weapon: 'primary', aim: -15, ammo: 2 },
  suppression: { id: 'suppression', name: 'Supresión', description: 'Inmoviliza al objetivo: −50 de puntería hasta tu próximo turno, y si se mueve, le disparas. Gasta 2 balas.', target: 'enemy', ap: 1, endsTurn: true, ammo: 2 },
  chainShot: { id: 'chainShot', name: 'Disparo en cadena', description: 'Disparo con −15 de puntería; si acierta, vuelves a disparar.', target: 'enemy', ap: 1, endsTurn: false, cooldown: 3, weapon: 'primary', aim: -15 },
  rupture: { id: 'rupture', name: 'Ruptura', description: 'Disparo con +3 de daño; el objetivo recibe +3 de daño de todos los ataques.', target: 'enemy', ap: 1, endsTurn: false, cooldown: 4, weapon: 'primary', damageBonus: 3 },
  aimedShot: { id: 'aimedShot', name: 'Ojo letal', description: 'Disparo con −25 de puntería y +50 % de daño.', target: 'enemy', ap: 1, endsTurn: false, cooldown: 3, weapon: 'primary', aim: -25, damageMult: 1.5 },
  lightningHands: { id: 'lightningHands', name: 'Manos rápidas', description: 'Acción gratuita: un disparo de pistola.', target: 'enemy', ap: 0, endsTurn: false, cooldown: 4, weapon: 'secondary' },
  faceoff: { id: 'faceoff', name: 'Cara a cara', description: 'Un disparo de pistola a cada enemigo que veas.', target: 'self', ap: 1, endsTurn: true, cooldown: 4, weapon: 'secondary' },
  killZone: { id: 'killZone', name: 'Zona letal', description: 'Vigilancia que dispara a cada enemigo que se mueva a la vista, una vez a cada uno.', target: 'self', ap: 1, endsTurn: true, cooldown: 4 },
  combatProtocol: { id: 'combatProtocol', name: 'Calambrazo', description: 'El Grillo electrocuta a un enemigo a la vista de la escuadra: 2 de daño (4 a Acorazados y Sirenas). Nunca falla e ignora el blindaje. Termina el turno.', target: 'enemy', ap: 1, endsTurn: true, charge: 'combatProtocol', range: 15, noise: 2 },
  revival: { id: 'revival', name: 'Espabilar', description: 'El Grillo libera a un aliado del aturdimiento, la desorientación y el pánico.', target: 'ally', ap: 1, endsTurn: false, charge: 'medkit', range: 15 },
  restoration: { id: 'restoration', name: 'Restauración', description: 'Cura 2 a todos los aliados a 6 casillas o menos y les quita los estados alterados.', target: 'self', ap: 1, endsTurn: true, cooldown: 5, range: 6 },
  discharge: { id: 'discharge', name: 'Descarga de batería', description: 'El Grillo descarga en una zona: 3 de daño a todos los que estén dentro, aliados incluidos.', target: 'tile', ap: 1, endsTurn: true, cooldown: 4, range: 10, radius: 1.5, explosive: 'discharge' },
  mindspin: { id: 'mindspin', name: 'Grito', description: 'El Chillón grita: desorienta o asusta a los soldados cercanos, y todo zombi a 16 casillas lo oye y viene.', target: 'enemy', ap: 1, endsTurn: true, cooldown: 3, range: 14, psi: true, noise: 16 },
  reanimate: { id: 'reanimate', name: 'Despertar a un muerto', description: 'El grito del Chillón despierta un cadáver, que se levanta como Zombi.', target: 'corpse', ap: 1, endsTurn: true, cooldown: 4, range: 12, psi: true, noise: 8 },
  burst: { id: 'burst', name: 'Reventón', description: 'Al morir o al llegar a un soldado revienta: 3 de daño a quien tenga al lado.', target: 'self', ap: 1, endsTurn: true, radius: 1.5, explosive: 'bloaterBurst' },
  smoke: { id: 'smoke', name: 'Granada de humo', description: 'Cubre una zona de humo durante 2 turnos: −20 a la puntería contra quien esté dentro.', target: 'tile', ap: 1, endsTurn: true, charge: 'smoke', range: 10, radius: 2, noise: 4 },
  flashbang: { id: 'flashbang', name: 'Granada aturdidora', description: 'Desorienta a los enemigos de la zona (−20 de puntería, sin explosivos). No hace daño.', target: 'tile', ap: 1, endsTurn: true, charge: 'flashbang', range: 10, radius: 2.5, noise: 18 },
  firstAid: { id: 'firstAid', name: 'Botiquín', description: 'Cura 4 de salud a un aliado contiguo (o a uno mismo).', target: 'ally', ap: 1, endsTurn: false, charge: 'firstAid', range: 1.5 },
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
  fragGrenade: { id: 'fragGrenade', name: 'Granada extra', description: '+1 granada de fragmentación.', slot: 'utility', cost: 20, level: 1, charges: { grenade: 1 } },
  smokeGrenade: { id: 'smokeGrenade', name: 'Granada de humo', description: 'Humo durante 2 turnos: −20 a la puntería contra quien esté dentro.', slot: 'utility', cost: 25, level: 1, charges: { smoke: 1 }, ability: 'smoke' },
  firstAid: { id: 'firstAid', name: 'Botiquín', description: 'Un uso: cura 4 a un aliado contiguo.', slot: 'utility', cost: 25, level: 1, charges: { firstAid: 1 }, ability: 'firstAid' },
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
  zombie: alien({ id: 'zombie', name: 'Zombi', short: 'ZOM', weapon: 'claws', meleeWeapon: 'claws', hp: 6, aim: 60, defense: -10, mobility: 4, sight: 14, detection: 6, will: 100, undead: true, abilities: ['claw'] }),
  xenoid: alien({ id: 'xenoid', name: 'Corredor', short: 'COR', weapon: 'xenoPistol', meleeWeapon: 'xenoPistol', hp: 5, aim: 60, mobility: 8, detection: 10, will: 100, undead: true, abilities: ['claw'] }),
  mec: alien({ id: 'mec', name: 'Acorazado', short: 'ACO', weapon: 'mecCannon', meleeWeapon: 'mecCannon', hp: 7, armor: 1, aim: 60, mobility: 4, sight: 14, detection: 6, robotic: true, will: 100, undead: true, abilities: ['claw'] }),
  /** The Chillón doesn't bite: it screams (mindspin, noise 16) and raises the dead. */
  sectoid: alien({ id: 'sectoid', name: 'Chillón', short: 'CHI', weapon: 'sectoidPistol', hp: 5, mobility: 5, will: 100, undead: true, abilities: ['mindspin', 'reanimate'] }),
  /** The Hinchado: slow; it bursts when it dies or reaches a soldier (rules.ts burst). */
  bloater: alien({ id: 'bloater', name: 'Hinchado', short: 'HIN', weapon: 'bloaterBurst', hp: 6, aim: 0, defense: -15, mobility: 3, sight: 12, detection: 5, will: 100, undead: true, abilities: ['burst'] }),
  // Objectives. The relay is an alien "unit" so every weapon can hit it; it never acts.
  relay: alien({ id: 'relay', name: 'Sirena', short: 'SIR', weapon: 'trooperRifle', hp: 8, armor: 1, aim: 0, mobility: 0, sight: 0, detection: 0, robotic: true, abilities: [] }),
  vip: soldier({ id: 'vip', name: 'VIP', short: 'VIP', weapon: 'pistol', hp: 4, aim: 50, will: 30, abilities: ['hunker'], charges: {} }),
};

/** Default kits: each player gets a mix, a solo player gets all six. */
export const SQUAD_CLASSES: ClassId[] = ['assault', 'sharpshooter', 'specialist', 'grenadier', 'assault', 'specialist'];

const ATTACKS: ReadonlySet<AbilityId> = new Set<AttackAbility>([
  'shoot', 'pistol', 'slash', 'stunLance', 'claw', 'rapidFire', 'chainShot', 'aimedShot', 'rupture', 'lightningHands', 'faceoff', 'spectralArrow', 'lightRain',
]);

/** Abilities that fire a weapon (one roll to hit per shot). */
export function isAttack(ability: AbilityId): ability is AttackAbility {
  return ATTACKS.has(ability);
}

/** Weapon used by an attack ability. */
export function attackWeapon(u: Unit, ability: AttackAbility = 'shoot'): WeaponDef {
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

export const SOLDIER_NAMES = [
  'Ana Ríos', 'Luis Varela', 'Marta Soler', 'Iker Ortega', 'Nora Campos', 'Hugo Lázaro',
  'Elena Vidal', 'Darío Pons', 'Sara Mena', 'Raúl Ibarra', 'Lucía Prat', 'Óscar Nieto',
  'Irene Galán', 'Pablo Rey', 'Clara Bosch', 'Adrián Gil', 'Julia Font', 'Marcos Vela',
];

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
};

export interface RankDef {
  name: string;
  /** Experience needed to reach this rank. */
  xp: number;
}

/**
 * XP: 1 per mission, +1 for a victory, +1 per kill. Every promotion brings
 * aptitude and talent points (classes.ts).
 */
export const RANKS: RankDef[] = [
  { name: 'Novato', xp: 0 },
  { name: 'Soldado', xp: 2 },
  { name: 'Cabo', xp: 5 },
  { name: 'Sargento', xp: 9 },
  { name: 'Teniente', xp: 14 },
  { name: 'Capitán', xp: 20 },
  { name: 'Comandante', xp: 27 },
  { name: 'Coronel', xp: 35 },
];

export function hasPerk(u: Pick<Unit, 'perks'>, id: PerkId): boolean {
  return u.perks.includes(id);
}

export function rankForXp(xp: number): number {
  let rank = 0;
  RANKS.forEach((r, i) => {
    if (xp >= r.xp) rank = i;
  });
  return rank;
}

export function addMods(a: UnitMods, b: Partial<UnitMods>): UnitMods {
  const out = { ...a };
  for (const [k, v] of Object.entries(b) as [keyof UnitMods, number][]) out[k] += v;
  return out;
}
