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

/** Inactive pods within this distance of an explosion hear it and activate. */
export const EXPLOSION_ALERT_RADIUS = 8;
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
  | 'discharge';

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
  ...w,
});

export const WEAPONS: Record<WeaponId, WeaponDef> = {
  rifle: weapon({ id: 'rifle', name: 'Fusil de asalto', damage: 4, spread: 1, critDamage: 2, clip: 4 }),
  shotgun: weapon({ id: 'shotgun', name: 'Escopeta', damage: 5, spread: 1, critDamage: 3, clip: 4, crit: 10, range: 'short', burst: 1 }),
  cannon: weapon({ id: 'cannon', name: 'Cañón', damage: 5, spread: 1, critDamage: 2, clip: 3, burst: 5 }),
  sniper: weapon({ id: 'sniper', name: 'Fusil de precisión', damage: 5, spread: 1, critDamage: 2, clip: 3, crit: 10, range: 'long', apCost: 2, burst: 1 }),
  pistol: weapon({ id: 'pistol', name: 'Pistola', damage: 2, plusOne: 50, critDamage: 1, burst: 1 }),
  sword: weapon({ id: 'sword', name: 'Espada', damage: 4, spread: 1, critDamage: 2, aim: 20, crit: 10, range: 'none', melee: true, burst: 1 }),
  trooperRifle: weapon({ id: 'trooperRifle', name: 'Fusil de Impreso', damage: 3, critDamage: 1, clip: 3 }),
  officerRifle: weapon({ id: 'officerRifle', name: 'Fusil de Afinado', damage: 3, plusOne: 50, critDamage: 2, clip: 3 }),
  xenoPistol: weapon({ id: 'xenoPistol', name: 'Arma de Heraldo', damage: 3, plusOne: 25, critDamage: 2, clip: 3 }),
  lancerRifle: weapon({ id: 'lancerRifle', name: 'Carabina', damage: 3, plusOne: 50, critDamage: 2, clip: 3 }),
  stunLance: weapon({ id: 'stunLance', name: 'Lanza aturdidora', damage: 3, spread: 1, critDamage: 2, range: 'none', melee: true, burst: 1 }),
  mecCannon: weapon({ id: 'mecCannon', name: 'Cañón de Gólem', damage: 5, spread: 1, critDamage: 2, clip: 4, shred: 1, burst: 4 }),
  sectoidPistol: weapon({ id: 'sectoidPistol', name: 'Pistola de Cantor', damage: 3, plusOne: 50, critDamage: 1, clip: 3, burst: 1 }),
  claws: weapon({ id: 'claws', name: 'Garras', damage: 4, spread: 1, critDamage: 2, aim: 10, range: 'none', melee: true, burst: 1 }),
  fragGrenade: weapon({ id: 'fragGrenade', name: 'Granada de fragmentación', damage: 3, plusOne: 20, shred: 1, burst: 0 }),
  microMissiles: weapon({ id: 'microMissiles', name: 'Micromisiles', damage: 3, shred: 1, burst: 0 }),
  droneZap: weapon({ id: 'droneZap', name: 'Descarga del Grillo', damage: 2, burst: 0 }),
  discharge: weapon({ id: 'discharge', name: 'Descarga de condensador', damage: 3, burst: 0 }),
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
}

export const ABILITIES: Record<AbilityId, AbilityDef> = {
  shoot: { id: 'shoot', name: 'Disparar', description: 'Dispara el arma principal. Termina el turno.', target: 'enemy', ap: 1, endsTurn: true, weapon: 'primary' },
  pistol: { id: 'pistol', name: 'Pistola', description: 'Dispara la pistola: munición infinita. Termina el turno.', target: 'enemy', ap: 1, endsTurn: true, weapon: 'secondary' },
  slash: { id: 'slash', name: 'Tajo', description: 'Corre hasta un enemigo a tu alcance y atácalo con la espada. Ignora la cobertura.', target: 'melee', ap: 1, endsTurn: true, weapon: 'melee' },
  stunLance: { id: 'stunLance', name: 'Lanza aturdidora', description: 'Ataque cuerpo a cuerpo que aturde.', target: 'melee', ap: 1, endsTurn: true, weapon: 'melee' },
  claw: { id: 'claw', name: 'Garras', description: 'Ataque cuerpo a cuerpo.', target: 'melee', ap: 1, endsTurn: true, weapon: 'melee' },
  overwatch: { id: 'overwatch', name: 'Vigilancia', description: 'Dispara al primer enemigo que se mueva a la vista.', target: 'self', ap: 1, endsTurn: true },
  hunker: { id: 'hunker', name: 'Agazaparse', description: '+30 de defensa y +50 de esquiva hasta tu próximo turno.', target: 'self', ap: 1, endsTurn: true },
  reload: { id: 'reload', name: 'Recargar', description: 'Llena el cargador.', target: 'self', ap: 1, endsTurn: false },
  grenade: { id: 'grenade', name: 'Granada', description: 'Lanza una granada: daña a todos en el área y destruye cobertura.', target: 'tile', ap: 1, endsTurn: true, charge: 'grenade', range: 10, radius: 2.5, explosive: 'fragGrenade' },
  launch: { id: 'launch', name: 'Lanzagranadas', description: 'Dispara una granada más lejos y con más radio.', target: 'tile', ap: 1, endsTurn: true, charge: 'grenade', range: 14, radius: 3.5, explosive: 'fragGrenade' },
  missiles: { id: 'missiles', name: 'Micromisiles', description: 'Andanada explosiva.', target: 'tile', ap: 1, endsTurn: true, charge: 'missiles', range: 15, radius: 2.5, explosive: 'microMissiles' },
  medkit: { id: 'medkit', name: 'Protocolo médico', description: 'El Grillo, el dron del especialista, cura 4 de salud a un aliado.', target: 'ally', ap: 1, endsTurn: false, charge: 'medkit', range: 15 },
  aid: { id: 'aid', name: 'Protocolo de ayuda', description: 'El Grillo protege a un aliado: +20 de defensa hasta vuestro próximo turno.', target: 'ally', ap: 1, endsTurn: false, range: 15 },
  evac: { id: 'evac', name: 'Evacuar', description: 'Sale de la misión desde la zona de evacuación.', target: 'self', ap: 1, endsTurn: true },
  hack: {
    id: 'hack',
    name: 'Hackear',
    description: 'Piratea el terminal desde una casilla contigua; el Grillo, el dron del especialista, llega a 10 casillas. Si falla, salta la alarma y llegan refuerzos.',
    target: 'self',
    ap: 1,
    endsTurn: true,
    range: 10,
  },
  runAndGun: { id: 'runAndGun', name: 'Correr y disparar', description: 'Acción gratuita: +1 acción este turno.', target: 'self', ap: 0, endsTurn: false, cooldown: 4 },
  rapidFire: { id: 'rapidFire', name: 'Fuego rápido', description: 'Dos disparos seguidos, cada uno con −15 de puntería. Gasta 2 balas.', target: 'enemy', ap: 1, endsTurn: true, cooldown: 3, weapon: 'primary', aim: -15, ammo: 2 },
  suppression: { id: 'suppression', name: 'Supresión', description: 'Inmoviliza al objetivo: −50 de puntería hasta tu próximo turno, y si se mueve, le disparas. Gasta 2 balas.', target: 'enemy', ap: 1, endsTurn: true, ammo: 2 },
  chainShot: { id: 'chainShot', name: 'Disparo en cadena', description: 'Disparo con −15 de puntería; si acierta, vuelves a disparar.', target: 'enemy', ap: 1, endsTurn: true, cooldown: 3, weapon: 'primary', aim: -15 },
  rupture: { id: 'rupture', name: 'Ruptura', description: 'Disparo con +3 de daño; el objetivo recibe +3 de daño de todos los ataques.', target: 'enemy', ap: 1, endsTurn: true, cooldown: 4, weapon: 'primary', damageBonus: 3 },
  aimedShot: { id: 'aimedShot', name: 'Ojo letal', description: 'Disparo con −25 de puntería y +50 % de daño.', target: 'enemy', ap: 1, endsTurn: true, cooldown: 3, weapon: 'primary', aim: -25, damageMult: 1.5 },
  lightningHands: { id: 'lightningHands', name: 'Manos rápidas', description: 'Acción gratuita: un disparo de pistola.', target: 'enemy', ap: 0, endsTurn: false, cooldown: 4, weapon: 'secondary' },
  faceoff: { id: 'faceoff', name: 'Cara a cara', description: 'Un disparo de pistola a cada enemigo que veas.', target: 'self', ap: 1, endsTurn: true, cooldown: 4, weapon: 'secondary' },
  killZone: { id: 'killZone', name: 'Zona letal', description: 'Vigilancia que dispara a cada enemigo que se mueva a la vista, una vez a cada uno.', target: 'self', ap: 1, endsTurn: true, cooldown: 4 },
  combatProtocol: { id: 'combatProtocol', name: 'Protocolo de combate', description: 'El Grillo electrocuta a un enemigo a la vista de la escuadra: 2 de daño (4 a robots). Nunca falla e ignora el blindaje.', target: 'enemy', ap: 1, endsTurn: false, charge: 'combatProtocol', range: 15 },
  revival: { id: 'revival', name: 'Protocolo de reanimación', description: 'El Grillo libera a un aliado del aturdimiento, la desorientación y el pánico.', target: 'ally', ap: 1, endsTurn: false, charge: 'medkit', range: 15 },
  restoration: { id: 'restoration', name: 'Restauración', description: 'Cura 2 a todos los aliados a 6 casillas o menos y les quita los estados alterados.', target: 'self', ap: 1, endsTurn: true, cooldown: 5, range: 6 },
  discharge: { id: 'discharge', name: 'Descarga de condensador', description: 'El Grillo descarga en una zona: 3 de daño a todos los que estén dentro, aliados incluidos.', target: 'tile', ap: 1, endsTurn: true, cooldown: 4, range: 10, radius: 1.5, explosive: 'discharge' },
  mindspin: { id: 'mindspin', name: 'Canto', description: 'El Cantor canta dentro de la cabeza de un soldado: lo desorienta, lo aterroriza o lo controla.', target: 'enemy', ap: 1, endsTurn: true, cooldown: 3, range: 14, psi: true },
  reanimate: { id: 'reanimate', name: 'Levantar un Hueco', description: 'El Cantor canta en un cadáver y lo levanta como Hueco.', target: 'corpse', ap: 1, endsTurn: true, cooldown: 4, range: 12, psi: true },
  smoke: { id: 'smoke', name: 'Granada de humo', description: 'Cubre una zona de humo durante 2 turnos: −20 a la puntería contra quien esté dentro.', target: 'tile', ap: 1, endsTurn: true, charge: 'smoke', range: 10, radius: 2 },
  flashbang: { id: 'flashbang', name: 'Granada aturdidora', description: 'Desorienta a los enemigos de la zona (−20 de puntería, sin explosivos). No hace daño.', target: 'tile', ap: 1, endsTurn: true, charge: 'flashbang', range: 10, radius: 2.5 },
  firstAid: { id: 'firstAid', name: 'Botiquín', description: 'Cura 4 de salud a un aliado contiguo (o a uno mismo).', target: 'ally', ap: 1, endsTurn: false, charge: 'firstAid', range: 1.5 },
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
  nanoVest: { id: 'nanoVest', name: 'Nanochaleco', description: '+2 de salud.', slot: 'utility', cost: 50, level: 4, hp: 2 },
  apRounds: { id: 'apRounds', name: 'Munición perforante', description: 'El arma principal ignora 1 punto de blindaje.', slot: 'ammo', cost: 55, level: 4, mods: { pierce: 1 } },
  mindShield: { id: 'mindShield', name: 'Zumbador', description: 'Una nota disonante detrás de la oreja: inmune a los ataques psiónicos.', slot: 'utility', cost: 75, level: 6, mods: { will: 100 } },
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
  specialist: soldier({
    id: 'specialist', name: 'Especialista', short: 'ESP', weapon: 'rifle',
    abilities: ['shoot', 'medkit', 'aid', 'overwatch', 'grenade', 'reload', 'hunker'], charges: { grenade: 1, medkit: 2 },
  }),
  trooper: alien({ id: 'trooper', name: 'Impreso', short: 'IMP', weapon: 'trooperRifle', abilities: ['shoot', 'overwatch', 'reload', 'grenade'], charges: { grenade: 1 } }),
  officer: alien({ id: 'officer', name: 'Afinado', short: 'AFI', weapon: 'officerRifle', hp: 6, abilities: ['shoot', 'overwatch', 'reload'] }),
  xenoid: alien({ id: 'xenoid', name: 'Heraldo', short: 'HER', weapon: 'xenoPistol', hp: 7, aim: 70, detection: 10, abilities: ['shoot', 'overwatch', 'reload'] }),
  lancer: alien({ id: 'lancer', name: 'Recolector', short: 'REC', weapon: 'lancerRifle', meleeWeapon: 'stunLance', hp: 4, abilities: ['stunLance', 'shoot', 'reload'] }),
  mec: alien({ id: 'mec', name: 'Gólem', short: 'GOL', weapon: 'mecCannon', hp: 7, armor: 1, aim: 70, robotic: true, abilities: ['shoot', 'missiles', 'overwatch', 'reload'], charges: { missiles: 1 } }),
  sectoid: alien({ id: 'sectoid', name: 'Cantor', short: 'CAN', weapon: 'sectoidPistol', hp: 5, will: 80, abilities: ['shoot', 'mindspin', 'reanimate', 'reload'] }),
  zombie: alien({ id: 'zombie', name: 'Hueco', short: 'HUE', weapon: 'claws', meleeWeapon: 'claws', hp: 6, aim: 60, defense: -10, mobility: 4, sight: 14, detection: 6, will: 100, abilities: ['claw'] }),
  // Objectives. The relay is an alien "unit" so every weapon can hit it; it never acts.
  relay: alien({ id: 'relay', name: 'Retransmisor', short: 'RET', weapon: 'trooperRifle', hp: 8, armor: 1, aim: 0, mobility: 0, sight: 0, detection: 0, robotic: true, abilities: [] }),
  vip: soldier({ id: 'vip', name: 'VIP', short: 'VIP', weapon: 'pistol', hp: 4, aim: 50, will: 30, abilities: ['hunker'], charges: {} }),
};

/** Default kits: each player gets a mix, a solo player gets all six. */
export const SQUAD_CLASSES: ClassId[] = ['assault', 'sharpshooter', 'specialist', 'grenadier', 'assault', 'specialist'];

const ATTACKS: ReadonlySet<AbilityId> = new Set<AttackAbility>([
  'shoot', 'pistol', 'slash', 'stunLance', 'claw', 'rapidFire', 'chainShot', 'aimedShot', 'rupture', 'lightningHands', 'faceoff',
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

export const PLAYER_COLORS: [string, string] = ['#3fa9ff', '#ffa630'];

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
};

export type PerkTier = 1 | 2 | 3 | 4 | 5;

export interface RankDef {
  name: string;
  /** Experience needed to reach this rank. */
  xp: number;
  /** Ability tier unlocked when reaching this rank. */
  perkTier?: PerkTier;
}

/** XP: 1 per mission, +1 for a victory, +1 per kill. One ability choice per rank up to captain. */
export const RANKS: RankDef[] = [
  { name: 'Novato', xp: 0 },
  { name: 'Soldado', xp: 2, perkTier: 1 },
  { name: 'Cabo', xp: 5, perkTier: 2 },
  { name: 'Sargento', xp: 9, perkTier: 3 },
  { name: 'Teniente', xp: 14, perkTier: 4 },
  { name: 'Capitán', xp: 20, perkTier: 5 },
  { name: 'Comandante', xp: 27 },
  { name: 'Coronel', xp: 35 },
];

/** Every rank above rookie adds aim and will; some add health. */
export const RANK_AIM = 5;
export const RANK_WILL = 4;
export const RANK_HP: Record<number, number> = { 2: 1, 4: 1, 6: 1 };

export interface PerkDef {
  id: PerkId;
  name: string;
  description: string;
  cls: ClassId;
  tier: PerkTier;
  mods?: Partial<UnitMods>;
  hp?: number;
  charges?: Partial<Record<ChargeId, number>>;
  /** Active ability the perk grants. */
  ability?: AbilityId;
}

const perk = (cls: ClassId, tier: PerkTier, id: PerkId, name: string, description: string, extra: Partial<PerkDef> = {}): PerkDef => ({
  id,
  name,
  description,
  cls,
  tier,
  ...extra,
});

export const PERKS: Record<PerkId, PerkDef> = {
  // Assault
  pointBlank: perk('assault', 1, 'pointBlank', 'Quemarropa', '+15 de puntería y +10 de crítico a 4 casillas o menos.', { mods: { closeRangeAim: 15, closeRangeCrit: 10 } }),
  keenBlade: perk('assault', 1, 'keenBlade', 'Hoja afilada', 'El Tajo hace +2 de daño.', { mods: { meleeDamage: 2 } }),
  shadowstep: perk('assault', 2, 'shadowstep', 'Paso sombrío', 'Moverse nunca provoca fuego de reacción.'),
  runner: perk('assault', 2, 'runner', 'Corredor', '+1 casilla de movimiento por acción.', { mods: { mobility: 1 } }),
  runAndGun: perk('assault', 3, 'runAndGun', 'Correr y disparar', 'Acción gratuita: +1 acción este turno (enfriamiento 4).', { ability: 'runAndGun' }),
  implacable: perk('assault', 3, 'implacable', 'Implacable', 'Matar con el Tajo devuelve una acción (una vez por turno).'),
  rapidFire: perk('assault', 4, 'rapidFire', 'Fuego rápido', 'Dos disparos seguidos con −15 de puntería (enfriamiento 3).', { ability: 'rapidFire' }),
  toughSkin: perk('assault', 4, 'toughSkin', 'Piel dura', '+2 de salud.', { hp: 2 }),
  bladeMaster: perk('assault', 5, 'bladeMaster', 'Maestro de la espada', 'El Tajo hace +2 de daño y gana +15 de puntería.', { mods: { meleeDamage: 2, meleeAim: 15 } }),
  hunter: perk('assault', 5, 'hunter', 'Cazador', '+2 de daño contra objetivos flanqueados.', { mods: { flankDamage: 2 } }),
  // Grenadier
  demolition: perk('grenadier', 1, 'demolition', 'Demolición', 'Las granadas hacen +1 de daño.', { mods: { grenadeDamage: 1 } }),
  heavyFire: perk('grenadier', 1, 'heavyFire', 'Fuego pesado', 'El cañón hace +1 de daño.', { mods: { damage: 1 } }),
  shredder: perk('grenadier', 2, 'shredder', 'Destrozador', 'Los disparos del cañón destrozan 1 punto de blindaje.', { mods: { shred: 1 } }),
  bigBore: perk('grenadier', 2, 'bigBore', 'Gran calibre', 'Las granadas tienen +1 de radio.', { mods: { grenadeRadius: 1 } }),
  suppression: perk('grenadier', 3, 'suppression', 'Supresión', 'Inmoviliza a un enemigo: −50 de puntería, y si se mueve le disparas.', { ability: 'suppression' }),
  holoTargeting: perk('grenadier', 3, 'holoTargeting', 'Holo-objetivo', 'Cada disparo del cañón marca al objetivo: +15 de puntería para tus aliados.'),
  salvo: perk('grenadier', 4, 'salvo', 'Salva', 'Lanzar granadas ya no termina el turno.'),
  armoured: perk('grenadier', 4, 'armoured', 'Blindado', '+2 de salud.', { hp: 2 }),
  chainShot: perk('grenadier', 5, 'chainShot', 'Disparo en cadena', 'Disparo con −15; si acierta, disparas otra vez (enfriamiento 3).', { ability: 'chainShot' }),
  rupture: perk('grenadier', 5, 'rupture', 'Ruptura', 'Disparo con +3 de daño que deja al objetivo recibiendo +3 de daño (enfriamiento 4).', { ability: 'rupture' }),
  // Sharpshooter
  deadeye: perk('sharpshooter', 1, 'deadeye', 'Precisión letal', '+10 de crítico.', { mods: { crit: 10 } }),
  gunslinger: perk('sharpshooter', 1, 'gunslinger', 'Pistolero', '+15 de puntería con la pistola.', { mods: { pistolAim: 15 } }),
  longWatch: perk('sharpshooter', 2, 'longWatch', 'Vigía lejano', 'La vigilancia dispara a todo lo que vea la escuadra (visión de escuadra).'),
  quickdraw: perk('sharpshooter', 2, 'quickdraw', 'Desenfunde', 'Disparar la pistola ya no termina el turno.'),
  aimedShot: perk('sharpshooter', 3, 'aimedShot', 'Ojo letal', 'Disparo con −25 de puntería y +50 % de daño (enfriamiento 3).', { ability: 'aimedShot' }),
  lightningHands: perk('sharpshooter', 3, 'lightningHands', 'Manos rápidas', 'Acción gratuita: un disparo de pistola (enfriamiento 4).', { ability: 'lightningHands' }),
  deathFromAbove: perk('sharpshooter', 4, 'deathFromAbove', 'Muerte desde arriba', 'Matar desde más altura devuelve una acción (una vez por turno).'),
  hawkEye: perk('sharpshooter', 4, 'hawkEye', 'Ojo de halcón', '+10 de puntería.', { mods: { aim: 10 } }),
  faceoff: perk('sharpshooter', 5, 'faceoff', 'Cara a cara', 'Un disparo de pistola a cada enemigo a la vista (enfriamiento 4).', { ability: 'faceoff' }),
  killZone: perk('sharpshooter', 5, 'killZone', 'Zona letal', 'Vigilancia que dispara a cada enemigo que se mueva, una vez a cada uno (enfriamiento 4).', { ability: 'killZone' }),
  // Specialist
  fieldMedic: perk('specialist', 1, 'fieldMedic', 'Médico de campo', '+1 uso del botiquín y cura +2.', { mods: { heal: 2 }, charges: { medkit: 1 } }),
  guardian: perk('specialist', 1, 'guardian', 'Protector', 'El Protocolo de ayuda da +10 de defensa extra.', { mods: { aid: 10 } }),
  coveringFire: perk('specialist', 2, 'coveringFire', 'Fuego de cobertura', 'La vigilancia también dispara a quien ataque, no solo a quien se mueva.'),
  combatProtocol: perk('specialist', 2, 'combatProtocol', 'Protocolo de combate', 'El Grillo electrocuta a un enemigo: 2 de daño (4 a robots), nunca falla. 2 usos.', { ability: 'combatProtocol', charges: { combatProtocol: 2 } }),
  threatAssessment: perk('specialist', 3, 'threatAssessment', 'Evaluación de amenazas', 'El Protocolo de ayuda también pone al aliado en vigilancia.'),
  revivalProtocol: perk('specialist', 3, 'revivalProtocol', 'Protocolo de reanimación', 'El Grillo libera a un aliado del aturdimiento, la desorientación y el pánico (usa el botiquín).', { ability: 'revival' }),
  slippery: perk('specialist', 4, 'slippery', 'Escurridizo', '+10 de defensa.', { mods: { defense: 10 } }),
  marksman: perk('specialist', 4, 'marksman', 'Tirador', '+10 de puntería.', { mods: { aim: 10 } }),
  restoration: perk('specialist', 5, 'restoration', 'Restauración', 'Cura 2 a todos los aliados cercanos y les quita los estados alterados (enfriamiento 5).', { ability: 'restoration' }),
  capacitorDischarge: perk('specialist', 5, 'capacitorDischarge', 'Descarga de condensador', 'El Grillo descarga en una zona: 3 de daño a todos (enfriamiento 4).', { ability: 'discharge' }),
};

export function perksFor(cls: ClassId, tier: PerkTier): PerkDef[] {
  return Object.values(PERKS).filter((p) => p.cls === cls && p.tier === tier);
}

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
