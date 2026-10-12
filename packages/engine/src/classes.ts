// Soldier growth: five aptitudes the player raises at each promotion, and one
// talent tree shared by everyone, «la Rosa». Its centre is open to all; each of
// its four axes leads to a class (three branches plus the Canto of Velmar).
// Every soldier starts as a recruit with no class and may learn from two axes,
// up to their third row. On reaching Capitán (SPECIALIZE_RANK) the soldier
// becomes the class of the axis it was taught most (aptitudes break ties), for
// good until a retrain: its weapons, its own abilities, the last two rows of
// its axis and its two aptitudes able to reach 10. Talents that need a class's
// equipment can be learnt earlier, but sleep until the soldier carries it.
//
// Every promotion gives ATTR_POINTS_PER_RANK aptitude points and
// TALENT_POINTS_PER_RANK talent points. Free points are never stored: they're
// what the rank allows minus what has been spent, so a retrain only has to
// reset the choices.
import { addMods, ITEMS, RANKS, TEMPLATES, ZERO_MODS } from './content';
import type { Rng } from './rng';
import type { AbilityId, ChargeId, ClassId, ItemId, PerkId, TemplateId, UnitMods } from './types';

// ---------------------------------------------------------------- aptitudes

export type AttrId = 'pul' | 'agu' | 'ref' | 'tem' | 'ofi';
export type Attributes = Record<AttrId, number>;
/** Talent → level reached (absent = not taken). */
export type Talents = Partial<Record<PerkId, number>>;

/** What growth needs to know about a soldier (a CampaignSoldier is one). `cls` null = recruit. */
export interface Build {
  cls: ClassId | null;
  rank: number;
  attrs: Attributes;
  talents: Talents;
}

export const ATTR_IDS: AttrId[] = ['pul', 'agu', 'ref', 'tem', 'ofi'];
export const ATTR_MAX = 10;
/** Cap of every aptitude that isn't one of the soldier's class. */
export const ATTR_CAP = 8;
export const ATTR_POINTS_PER_RANK = 2;
export const TALENT_POINTS_PER_RANK = 2;
/** What every promotion still adds on its own (veterancy). Hacking keeps HACK_PER_RANK. */
export const VETERAN_AIM = 2;
export const VETERAN_WILL = 2;
/** Aptitude values that bring an extra (ATTRS[id].thresholds). */
export const ATTR_THRESHOLDS = [7, 10] as const;

export interface AttrDef {
  id: AttrId;
  name: string;
  short: string;
  /** What each point does. */
  effect: string;
  /** Extras at 7 and at 10. */
  thresholds: [string, string];
}

export const ATTRS: Record<AttrId, AttrDef> = {
  pul: { id: 'pul', name: 'Pulso', short: 'PUL', effect: '+3 de puntería por punto.', thresholds: ['+10 de crítico.', '+1 de daño en los críticos.'] },
  agu: { id: 'agu', name: 'Aguante', short: 'AGU', effect: '+1 de salud cada 2 puntos.', thresholds: ['Las heridas curan un 30 % antes.', '+1 de blindaje.'] },
  ref: { id: 'ref', name: 'Reflejos', short: 'REF', effect: '+2 de defensa por punto por encima de 3.', thresholds: ['+1 casilla de movimiento.', 'Los disparos de reacción contra él tienen −20.'] },
  tem: { id: 'tem', name: 'Temple', short: 'TEM', effect: '+6 de voluntad por punto.', thresholds: ['No entra en pánico.', 'Sordo al Coro: el Cantor no puede controlarlo.'] },
  ofi: { id: 'ofi', name: 'Oficio', short: 'OFI', effect: '+5 de hackeo por punto por encima de 5.', thresholds: ['+1 granada.', '+1 de curación y +1 de daño con granadas.'] },
};

/** Every recruit starts here (22 points). */
export const RECRUIT_ATTRS: Attributes = { pul: 5, agu: 5, ref: 5, tem: 4, ofi: 3 };

/** Each class's own aptitudes: +1 each when the soldier specialises, and they reach ATTR_MAX. */
export const CLASS_TOP: Record<ClassId, [AttrId, AttrId]> = {
  assault: ['ref', 'agu'],
  grenadier: ['agu', 'ofi'],
  sharpshooter: ['pul', 'tem'],
  specialist: ['ofi', 'ref'],
};

/** The recruit's aptitudes, plus the class's +1 + 1 once specialised. */
export function baseAttrs(cls: ClassId | null): Attributes {
  const out = { ...RECRUIT_ATTRS };
  if (cls) for (const id of CLASS_TOP[cls]) out[id]++;
  return out;
}

export function attrCap(cls: ClassId | null, id: AttrId): number {
  return cls && CLASS_TOP[cls].includes(id) ? ATTR_MAX : ATTR_CAP;
}

const total = (values: Partial<Record<string, number>>) => Object.values(values).reduce((sum: number, v) => sum + (v ?? 0), 0);

export function attrPointsFree(b: Pick<Build, 'cls' | 'rank' | 'attrs'>): number {
  return ATTR_POINTS_PER_RANK * b.rank - (total(b.attrs) - total(baseAttrs(b.cls)));
}

/** Why the aptitude can't go up one point, or null if it can. */
export function attrBlocker(b: Pick<Build, 'cls' | 'rank' | 'attrs'>, id: AttrId): string | null {
  if (attrPointsFree(b) <= 0) return 'No quedan puntos de aptitud: cada ascenso da 2.';
  const cap = attrCap(b.cls, id);
  if (b.attrs[id] >= cap) return b.cls || cap === ATTR_MAX ? `${ATTRS[id].name} ya está en su tope (${cap}).` : `${ATTRS[id].name} ya está en el tope de un recluta (${cap}): especialízate para subirla a 10.`;
  return null;
}

// ------------------------------------------------------------------ the rosa

/** The four axes, in the order they sit around the rosa (neighbours share a side). */
export const AXES: ClassId[] = ['assault', 'grenadier', 'sharpshooter', 'specialist'];

/** Talent points the branch needs above a row before the row opens. */
export const ROW_GATES = [0, 2, 4, 7, 10] as const;
/** The Canto has one talent per row: each row opens once every row above has one point. */
export const SONG_GATES = [0, 1, 2, 3, 4] as const;
/** Canto rows each level of the Cámara de Ecos opens (level 0 = not built). */
export const SONG_ROWS_OPEN = [0, 2, 4, 5] as const;
/** A secondary axis stops above this row; the last two are the main axis's. */
export const SECOND_AXIS_ROWS = 3;
/** Rank that turns a recruit into the class it leans to (Capitán). */
export const SPECIALIZE_RANK = 5;
export const SPECIALIZE_RANK_NAME = RANKS[SPECIALIZE_RANK]!.name;
/** A retrained soldier past SPECIALIZE_RANK specialises again with this many points in an axis's ordinary branches. */
export const SPECIALIZE_POINTS = 2;

export interface BranchDef {
  name: string;
  /** What the branch is about, in a few words. */
  focus: string;
  /** The Canto of Velmar: opens with the Cámara de Ecos. */
  song?: boolean;
}

export const BRANCHES: Record<ClassId, [BranchDef, BranchDef, BranchDef, BranchDef]> = {
  assault: [
    { name: 'Hoja', focus: 'El Tajo y el cuerpo a cuerpo' },
    { name: 'Escopeta', focus: 'Corta distancia y flancos' },
    { name: 'Paso', focus: 'Movimiento y sigilo' },
    { name: 'Juramento', focus: 'Canto de paladín: cuerpo a cuerpo que protege', song: true },
  ],
  grenadier: [
    { name: 'Demolición', focus: 'Granadas y explosivos' },
    { name: 'Artillería', focus: 'El cañón y el blindaje' },
    { name: 'Control', focus: 'Supresión y marcas' },
    { name: 'Evocación', focus: 'Canto de mago de batalla', song: true },
  ],
  sharpshooter: [
    { name: 'Tirador', focus: 'Fusil y distancia' },
    { name: 'Pistolero', focus: 'Pistola y movimiento' },
    { name: 'Vigía', focus: 'Vigilancia y altura' },
    { name: 'Ojo de Velmar', focus: 'Canto de explorador arcano', song: true },
  ],
  specialist: [
    { name: 'Médico', focus: 'Botiquín y reanimación' },
    { name: 'Grillo', focus: 'El dron como arma' },
    { name: 'Enlace', focus: 'Ayuda y cooperación' },
    { name: 'Voz', focus: 'Canto de clérigo y bardo', song: true },
  ],
};

/** The centre of the rosa. */
export const CENTER_NAME = 'Recluta';

/** Equipment only one class carries: talents that need it stay with that class's soldiers. */
export type Kit = 'blade' | 'shotgun' | 'cannon' | 'rifle' | 'pistol' | 'grillo';

export const KIT_CLASS: Record<Kit, ClassId> = {
  blade: 'assault',
  shotgun: 'assault',
  cannon: 'grenadier',
  rifle: 'sharpshooter',
  pistol: 'sharpshooter',
  grillo: 'specialist',
};

export const KIT_NAMES: Record<Kit, string> = {
  blade: 'la espada del Asalto',
  shotgun: 'la escopeta del Asalto',
  cannon: 'el cañón del Granadero',
  rifle: 'el fusil de precisión del Francotirador',
  pistol: 'la pistola del Francotirador',
  grillo: 'el Grillo del Especialista',
};

/** Specialising, as the register tells it. `{name}` is the soldier's. */
export const SPECIALIZE_TEXT: Record<ClassId, string> = {
  assault: '{name} se especializa como Asalto. Teo le entrega una escopeta y una hoja.',
  grenadier: '{name} se especializa como Granadero. Le toca el cañón, y las granadas.',
  sharpshooter: '{name} se especializa como Francotirador. Teo le ajusta la mira de un fusil de precisión.',
  specialist: '{name} se especializa como Especialista. El Grillo ya tiene dueño.',
};

export const RECRUIT_DESCRIPTION = 'Todavía sin clase. Lleva fusil y aprende lo que le enseñen. Al llegar a Capitán se especializa en el eje de la Rosa en que más haya aprendido.';

/** Combat template a soldier fights with. */
export function soldierTemplate(cls: ClassId | null): TemplateId {
  return cls ?? 'rookie';
}

export interface TalentDef {
  id: PerkId;
  name: string;
  /** Per level when `max` > 1. */
  description: string;
  /** Axis of the rosa; null for the centre (open to everyone). */
  axis: ClassId | null;
  /** 3 = the axis's Canto. */
  branch: 0 | 1 | 2 | 3;
  row: 0 | 1 | 2 | 3 | 4;
  /** Levels it can take (one talent point each). */
  max: number;
  /** Per level. */
  mods?: Partial<UnitMods>;
  /** Once, on top, from the first level. */
  onceMods?: Partial<UnitMods>;
  /** Once, on top, when the last level is taken. */
  maxMods?: Partial<UnitMods>;
  hp?: number;
  charges?: Partial<Record<ChargeId, number>>;
  /** Active ability it grants. */
  ability?: AbilityId;
  /** Minimum aptitude. */
  req?: [AttrId, number];
  /** Talent that must be taken first. */
  needs?: PerkId;
  /** "Llave doble": only works next to the partner's soldiers. */
  coop?: boolean;
  /** Equipment it works with: only for soldiers of that class. */
  kit?: Kit;
  /** A song of Velmar (Canto branch). */
  song?: boolean;
}

type Spec = Omit<TalentDef, 'id' | 'axis' | 'branch' | 'row' | 'max' | 'name' | 'description'>;
const t = (axis: ClassId | null, branch: 0 | 1 | 2 | 3, row: 0 | 1 | 2 | 3 | 4, id: PerkId, max: number, name: string, description: string, extra: Spec = {}): TalentDef => ({
  id,
  name,
  description,
  axis,
  branch,
  row,
  max,
  ...(branch === 3 && axis ? { song: true } : {}),
  ...extra,
});

export const TALENTS: Record<PerkId, TalentDef> = {
  // Centre · Recluta
  drill: t(null, 0, 0, 'drill', 2, 'Instrucción de tiro', '+5 de puntería por nivel.', { mods: { aim: 5 } }),
  fitness: t(null, 0, 0, 'fitness', 2, 'Forma física', '+1 de salud por nivel.', { hp: 1 }),
  coolHead: t(null, 0, 0, 'coolHead', 1, 'Cabeza fría', '+8 de voluntad.', { mods: { will: 8 } }),
  spareGrenade: t(null, 0, 0, 'spareGrenade', 1, 'Granada de más', '+1 granada.', { charges: { grenade: 1 } }),
  // Assault · Hoja
  keenBlade: t('assault', 0, 0, 'keenBlade', 2, 'Hoja afilada', '+1 de daño con el Tajo por nivel.', { mods: { meleeDamage: 1 }, kit: 'blade' }),
  steadyHand: t('assault', 0, 0, 'steadyHand', 2, 'Mano firme', '+5 de puntería con el Tajo por nivel.', { mods: { meleeAim: 5 }, kit: 'blade' }),
  implacable: t('assault', 0, 1, 'implacable', 1, 'Implacable', 'Matar con el Tajo devuelve una acción (una vez por turno).', { kit: 'blade' }),
  bladeCrit: t('assault', 0, 1, 'bladeCrit', 2, 'Golpe certero', '+10 de crítico con el Tajo por nivel.', { mods: { meleeCrit: 10 }, kit: 'blade' }),
  toughSkin: t('assault', 0, 2, 'toughSkin', 2, 'Piel dura', '+1 de salud por nivel.', { hp: 1 }),
  instinct: t('assault', 0, 3, 'instinct', 2, 'Instinto', '+5 de defensa por nivel.', { mods: { defense: 5 } }),
  bladeMaster: t('assault', 0, 4, 'bladeMaster', 1, 'Maestro de la espada', 'El Tajo hace +2 de daño y gana +15 de puntería.', { mods: { meleeDamage: 2, meleeAim: 15 }, req: ['agu', 7], kit: 'blade' }),
  // Assault · Escopeta
  pointBlank: t('assault', 1, 0, 'pointBlank', 3, 'Quemarropa', '+5 de puntería y +4 de crítico a 4 casillas o menos, por nivel.', { mods: { closeRangeAim: 5, closeRangeCrit: 4 } }),
  slug: t('assault', 1, 0, 'slug', 1, 'Perdigón grueso', '+1 de daño con la escopeta.', { mods: { damage: 1 }, kit: 'shotgun' }),
  hunter: t('assault', 1, 1, 'hunter', 2, 'Cazador', '+1 de daño contra objetivos flanqueados por nivel.', { mods: { flankDamage: 1 } }),
  sideBySide: t('assault', 1, 1, 'sideBySide', 1, 'Codo con codo', '+15 de puntería si un soldado de tu compañero está a 4 casillas o menos.', { mods: { pairAim: 15 }, coop: true }),
  buckshot: t('assault', 1, 2, 'buckshot', 1, 'Postas', 'Tus disparos destrozan 1 de blindaje.', { mods: { shred: 1 } }),
  relentless: t('assault', 1, 2, 'relentless', 2, 'Sin tregua', '+5 de crítico por nivel.', { mods: { crit: 5 } }),
  closeKill: t('assault', 1, 3, 'closeKill', 2, 'Bocajarro', '+1 de daño a 4 casillas o menos por nivel.', { mods: { closeDamage: 1 } }),
  rapidFire: t('assault', 1, 4, 'rapidFire', 1, 'Fuego rápido', 'Dos disparos seguidos con −15 de puntería (enfriamiento 3).', { ability: 'rapidFire', req: ['pul', 7] }),
  // Assault · Paso
  runner: t('assault', 2, 0, 'runner', 1, 'Corredor', '+1 casilla de movimiento por acción.', { mods: { mobility: 1 } }),
  light: t('assault', 2, 0, 'light', 2, 'Ligero', '+5 de defensa por nivel.', { mods: { defense: 5 } }),
  shadowstep: t('assault', 2, 1, 'shadowstep', 1, 'Paso sombrío', 'Moverse nunca provoca fuego de reacción.'),
  feline: t('assault', 2, 1, 'feline', 2, 'Reflejos felinos', 'Los disparos de reacción contra ti tienen −10 por nivel.', { mods: { reactionDefense: 10 } }),
  evasion: t('assault', 2, 2, 'evasion', 2, 'Esquiva', '+10 de esquiva por nivel: más impactos se quedan en roce.', { mods: { dodge: 10 } }),
  scoutEye: t('assault', 2, 2, 'scoutEye', 2, 'Ojo de explorador', '+5 de puntería por nivel.', { mods: { aim: 5 } }),
  untouchable: t('assault', 2, 3, 'untouchable', 2, 'Inalcanzable', '+5 de defensa y +5 de esquiva por nivel.', { mods: { defense: 5, dodge: 5 } }),
  runAndGun: t('assault', 2, 4, 'runAndGun', 1, 'Correr y disparar', 'Acción gratuita: +1 acción este turno (enfriamiento 4).', { ability: 'runAndGun', req: ['ref', 8] }),
  // Assault · Juramento (Canto)
  songBlade: t('assault', 3, 0, 'songBlade', 2, 'Hoja cantada', 'El Tajo hace +1 de daño por nivel. En el nivel 2, además, desorienta al enemigo si acierta.', { mods: { meleeDamage: 1 }, maxMods: { slashDisorient: 1 } }),
  aegis: t('assault', 3, 1, 'aegis', 1, 'Égida', 'Tú y los aliados contiguos ganáis +20 de defensa hasta vuestro próximo turno (enfriamiento 3).', { ability: 'aegis' }),
  echoStep: t('assault', 3, 2, 'echoStep', 1, 'Salto de eco', 'Desapareces y apareces en una casilla libre a la vista, a 6 o menos. Cuesta 1 acción y no termina el turno (enfriamiento 4).', { ability: 'echoStep' }),
  layOnHands: t('assault', 3, 3, 'layOnHands', 1, 'Imposición de manos', 'Cura 3 a un aliado contiguo o a ti mismo y le quita el pánico y la desorientación. 2 usos.', { ability: 'layOnHands', charges: { layOnHands: 2 } }),
  velmarOath: t('assault', 3, 4, 'velmarOath', 1, 'Juramento de Velmar', 'Una vez por misión: durante 2 turnos, los aliados a 4 casillas no pueden entrar en pánico ni ser controlados, y tú recibes la mitad del daño.', { ability: 'velmarOath', charges: { velmarOath: 1 }, req: ['tem', 7] }),
  // Grenadier · Demolición
  demolition: t('grenadier', 0, 0, 'demolition', 2, 'Demolición', '+1 de daño de las granadas por nivel.', { mods: { grenadeDamage: 1 } }),
  bandolier: t('grenadier', 0, 0, 'bandolier', 2, 'Cinturón', '+1 granada por nivel.', { charges: { grenade: 1 } }),
  bigBore: t('grenadier', 0, 1, 'bigBore', 1, 'Gran calibre', 'Las granadas tienen +1 de radio.', { mods: { grenadeRadius: 1 } }),
  shrapnel: t('grenadier', 0, 1, 'shrapnel', 2, 'Metralla', 'Las granadas destrozan 1 de blindaje más por nivel.', { mods: { grenadeShred: 1 } }),
  longArm: t('grenadier', 0, 2, 'longArm', 2, 'Brazo largo', '+2 casillas de alcance de las granadas por nivel.', { mods: { grenadeRange: 2 } }),
  safeBlast: t('grenadier', 0, 2, 'safeBlast', 1, 'Detonación controlada', 'Tus granadas no hieren a la escuadra.'),
  blastWave: t('grenadier', 0, 3, 'blastWave', 1, 'Onda expansiva', 'Las granadas tienen +1 de radio más.', { mods: { grenadeRadius: 1 } }),
  salvo: t('grenadier', 0, 4, 'salvo', 1, 'Salva', 'Lanzar granadas ya no termina el turno.', { req: ['ofi', 6] }),
  // Grenadier · Artillería
  heavyFire: t('grenadier', 1, 0, 'heavyFire', 1, 'Fuego pesado', 'El cañón hace +1 de daño.', { mods: { damage: 1 }, kit: 'cannon' }),
  steadyCannon: t('grenadier', 1, 0, 'steadyCannon', 2, 'Apoyo firme', '+5 de puntería por nivel.', { mods: { aim: 5 } }),
  shredder: t('grenadier', 1, 1, 'shredder', 1, 'Destrozador', 'Los disparos del cañón destrozan 1 de blindaje.', { mods: { shred: 1 }, kit: 'cannon' }),
  apCannon: t('grenadier', 1, 1, 'apCannon', 2, 'Perforante', 'Los disparos del cañón ignoran 1 de blindaje por nivel.', { mods: { pierce: 1 }, kit: 'cannon' }),
  heavyCrit: t('grenadier', 1, 2, 'heavyCrit', 2, 'Calibre grueso', '+5 de crítico por nivel.', { mods: { crit: 5 } }),
  tripod: t('grenadier', 1, 2, 'tripod', 1, 'Trípode', 'Los disparos de vigilancia pierden un 15 % menos de puntería.', { mods: { reactionKeep: 15 } }),
  armoured: t('grenadier', 1, 3, 'armoured', 2, 'Blindado', '+1 de salud por nivel.', { hp: 1 }),
  chainShot: t('grenadier', 1, 4, 'chainShot', 1, 'Disparo en cadena', 'Disparo con −15; si acierta, disparas otra vez (enfriamiento 3).', { ability: 'chainShot', req: ['pul', 7] }),
  // Grenadier · Control
  holoTargeting: t('grenadier', 2, 0, 'holoTargeting', 1, 'Holo-objetivo', 'Cada disparo del cañón marca al objetivo: +15 de puntería para tus aliados.', { kit: 'cannon' }),
  marker: t('grenadier', 2, 0, 'marker', 2, 'Marcador', 'El holo-objetivo da +5 de puntería más por nivel.', { mods: { holoAim: 5 }, needs: 'holoTargeting', kit: 'cannon' }),
  suppression: t('grenadier', 2, 1, 'suppression', 1, 'Supresión', 'Inmoviliza a un enemigo: −50 de puntería, y si se mueve le disparas.', { ability: 'suppression' }),
  bulwark: t('grenadier', 2, 1, 'bulwark', 1, 'Parapeto', '+15 de defensa si un soldado de tu compañero está a 4 casillas o menos.', { mods: { pairDefense: 15 }, coop: true }),
  suppressor: t('grenadier', 2, 2, 'suppressor', 2, 'Supresor', 'Tu Supresión quita 10 de puntería más por nivel.', { mods: { suppressAim: 10 }, needs: 'suppression' }),
  ironNerves: t('grenadier', 2, 2, 'ironNerves', 2, 'Nervios de acero', '+8 de voluntad por nivel.', { mods: { will: 8 } }),
  dugIn: t('grenadier', 2, 3, 'dugIn', 2, 'Atrincherado', '+5 de defensa por nivel.', { mods: { defense: 5 } }),
  rupture: t('grenadier', 2, 4, 'rupture', 1, 'Ruptura', 'Disparo con +3 de daño que deja al objetivo recibiendo +3 de daño (enfriamiento 4).', { ability: 'rupture', req: ['tem', 6] }),
  // Grenadier · Evocación (Canto)
  bolt: t('grenadier', 3, 0, 'bolt', 1, 'Saeta', 'Un proyectil de luz a un enemigo a la vista: 3 de daño. Nunca falla e ignora la cobertura, pero no el blindaje (enfriamiento 2).', { ability: 'bolt' }),
  silence: t('grenadier', 3, 1, 'silence', 1, 'Silencio', 'Zona de radio 2 a 10 casillas: los enemigos dentro quedan desorientados y los Cantores no pueden cantar durante 2 turnos. 2 cargas.', { ability: 'silence', charges: { silence: 2 } }),
  fireball: t('grenadier', 3, 2, 'fireball', 1, 'Bola de fuego', '4 de daño en radio 2,5 a 12 casillas, y destruye la cobertura. Daña también a los tuyos. 2 cargas.', { ability: 'fireball', charges: { fireball: 2 } }),
  highFlame: t('grenadier', 3, 3, 'highFlame', 2, 'Llama alta', 'Saeta, Bola de fuego y Tormenta hacen +1 de daño por nivel.', { mods: { songDamage: 1 } }),
  velmarStorm: t('grenadier', 3, 4, 'velmarStorm', 1, 'Tormenta de Velmar', 'Una vez por misión: rayos sobre hasta 3 enemigos a la vista, 4 de daño a cada uno. Nunca fallan.', { ability: 'velmarStorm', charges: { velmarStorm: 1 }, req: ['tem', 7] }),
  // Sharpshooter · Tirador
  deadeye: t('sharpshooter', 0, 0, 'deadeye', 3, 'Precisión letal', '+4 de crítico por nivel.', { mods: { crit: 4 } }),
  hawkEye: t('sharpshooter', 0, 0, 'hawkEye', 2, 'Ojo de halcón', '+5 de puntería por nivel.', { mods: { aim: 5 } }),
  farsight: t('sharpshooter', 0, 1, 'farsight', 1, 'Visión lejana', 'La visión de escuadra quita 1 de puntería por casilla, no 2.', { mods: { squadsightKeep: 1 }, kit: 'rifle' }),
  caliber: t('sharpshooter', 0, 1, 'caliber', 1, 'Calibre', 'El fusil hace +1 de daño.', { mods: { damage: 1 }, kit: 'rifle' }),
  aimedShot: t('sharpshooter', 0, 2, 'aimedShot', 1, 'Ojo letal', 'Disparo con −25 de puntería y +50 % de daño (enfriamiento 3).', { ability: 'aimedShot', req: ['pul', 7] }),
  armorRounds: t('sharpshooter', 0, 2, 'armorRounds', 2, 'Bala blindada', 'El fusil ignora 1 de blindaje por nivel.', { mods: { pierce: 1 }, kit: 'rifle' }),
  deathFromAbove: t('sharpshooter', 0, 3, 'deathFromAbove', 1, 'Muerte desde arriba', 'Matar desde más altura devuelve una acción (una vez por turno).'),
  serenity: t('sharpshooter', 0, 4, 'serenity', 1, 'Serenidad', 'Tus disparos de fusil nunca se quedan en roce y los críticos hacen +2 de daño.', { mods: { critDamage: 2 }, req: ['tem', 7], kit: 'rifle' }),
  // Sharpshooter · Pistolero
  gunslinger: t('sharpshooter', 1, 0, 'gunslinger', 3, 'Pistolero', '+5 de puntería con la pistola por nivel.', { mods: { pistolAim: 5 }, kit: 'pistol' }),
  quickdraw: t('sharpshooter', 1, 0, 'quickdraw', 1, 'Desenfunde', '+10 de puntería y +10 de crítico con la pistola.', { mods: { pistolAim: 10, pistolCrit: 10 }, kit: 'pistol' }),
  lightningHands: t('sharpshooter', 1, 1, 'lightningHands', 1, 'Manos rápidas', 'Acción gratuita: un disparo de pistola (enfriamiento 4).', { ability: 'lightningHands', kit: 'pistol' }),
  magnum: t('sharpshooter', 1, 1, 'magnum', 2, 'Calibre .45', '+1 de daño con la pistola por nivel.', { mods: { pistolDamage: 1 }, kit: 'pistol' }),
  dancer: t('sharpshooter', 1, 2, 'dancer', 2, 'Bailarín', '+5 de defensa por nivel.', { mods: { defense: 5 }, req: ['ref', 6] }),
  holster: t('sharpshooter', 1, 2, 'holster', 1, 'Funda rápida', '+1 casilla de movimiento por acción.', { mods: { mobility: 1 } }),
  fineTrigger: t('sharpshooter', 1, 3, 'fineTrigger', 2, 'Gatillo fino', '+10 de crítico con la pistola por nivel.', { mods: { pistolCrit: 10 }, kit: 'pistol' }),
  faceoff: t('sharpshooter', 1, 4, 'faceoff', 1, 'Cara a cara', 'Un disparo de pistola a cada enemigo a la vista (enfriamiento 4).', { ability: 'faceoff', req: ['ref', 7], kit: 'pistol' }),
  // Sharpshooter · Vigía
  longWatch: t('sharpshooter', 2, 0, 'longWatch', 1, 'Vigía lejano', 'La vigilancia dispara a todo lo que vea la escuadra (visión de escuadra).', { kit: 'rifle' }),
  patience: t('sharpshooter', 2, 0, 'patience', 2, 'Paciencia', 'Los disparos de vigilancia pierden un 10 % menos de puntería por nivel.', { mods: { reactionKeep: 10 } }),
  nest: t('sharpshooter', 2, 1, 'nest', 2, 'Nido', '+5 de puntería por nivel al disparar desde más altura.', { mods: { heightAim: 5 } }),
  crossfire: t('sharpshooter', 2, 1, 'crossfire', 1, 'Fuego cruzado', '+15 de puntería contra enemigos a 4 casillas o menos de un soldado de tu compañero.', { mods: { spotterAim: 15 }, coop: true }),
  post: t('sharpshooter', 2, 2, 'post', 2, 'Puesto fijo', '+5 de defensa por nivel mientras vigilas.', { mods: { watchDefense: 5 } }),
  coldBlood: t('sharpshooter', 2, 2, 'coldBlood', 2, 'Sangre fría', '+8 de voluntad por nivel.', { mods: { will: 8 } }),
  guardCrit: t('sharpshooter', 2, 3, 'guardCrit', 1, 'Tirador de guardia', 'Los disparos de vigilancia pueden ser críticos.'),
  killZone: t('sharpshooter', 2, 4, 'killZone', 1, 'Zona letal', 'Vigilancia que dispara a cada enemigo que se mueva, una vez a cada uno (enfriamiento 4).', { ability: 'killZone' }),
  // Sharpshooter · Ojo de Velmar (Canto)
  huntersMark: t('sharpshooter', 3, 0, 'huntersMark', 1, 'Marca del cazador', 'El enemigo marcado recibe +15 de puntería y +15 de crítico en todos tus disparos hasta que muera (enfriamiento 3).', { ability: 'huntersMark' }),
  spectralArrow: t('sharpshooter', 3, 1, 'spectralArrow', 1, 'Flecha espectral', 'Disparo que atraviesa la cobertura: el objetivo cuenta como flanqueado, con −10 de puntería (enfriamiento 3).', { ability: 'spectralArrow' }),
  shadowStep: t('sharpshooter', 3, 2, 'shadowStep', 1, 'Paso de sombra', 'Te teletransportas a una casilla a la vista, a 8 o menos. Si está más alta, ganas +10 de puntería este turno (enfriamiento 4).', { ability: 'shadowStep' }),
  windEyes: t('sharpshooter', 3, 3, 'windEyes', 2, 'Ojos del viento', '+2 casillas de visión por nivel, y ves a través del humo.', { mods: { sight: 2 }, onceMods: { smokeSight: 1 } }),
  lightRain: t('sharpshooter', 3, 4, 'lightRain', 1, 'Lluvia de luz', 'Una vez por misión: una flecha espectral a cada enemigo a la vista, con −15 de puntería.', { ability: 'lightRain', charges: { lightRain: 1 }, req: ['tem', 7] }),
  // Specialist · Médico
  fieldMedic: t('specialist', 0, 0, 'fieldMedic', 2, 'Médico de campo', 'El botiquín cura +1 por nivel.', { mods: { heal: 1 }, kit: 'grillo' }),
  bigKit: t('specialist', 0, 0, 'bigKit', 2, 'Botiquín grande', '+1 uso del botiquín por nivel.', { charges: { medkit: 1 }, kit: 'grillo' }),
  revivalProtocol: t('specialist', 0, 1, 'revivalProtocol', 1, 'Protocolo de reanimación', 'El Grillo libera a un aliado del aturdimiento, la desorientación y el pánico (usa el botiquín).', { ability: 'revival', kit: 'grillo' }),
  vocation: t('specialist', 0, 1, 'vocation', 2, 'Vocación', '+1 de salud por nivel.', { hp: 1 }),
  slippery: t('specialist', 0, 2, 'slippery', 2, 'Escurridizo', '+5 de defensa por nivel.', { mods: { defense: 5 } }),
  surgeon: t('specialist', 0, 3, 'surgeon', 2, 'Pulso de cirujano', '+5 de puntería por nivel.', { mods: { aim: 5 } }),
  restoration: t('specialist', 0, 4, 'restoration', 1, 'Restauración', 'Cura 2 a todos los aliados cercanos y les quita los estados alterados (enfriamiento 5).', { ability: 'restoration', req: ['tem', 6], kit: 'grillo' }),
  // Specialist · Grillo
  combatProtocol: t('specialist', 1, 0, 'combatProtocol', 1, 'Protocolo de combate', 'El Grillo electrocuta a un enemigo: 2 de daño (4 a robots), nunca falla. 2 usos.', { ability: 'combatProtocol', charges: { combatProtocol: 2 }, kit: 'grillo' }),
  marksman: t('specialist', 1, 0, 'marksman', 2, 'Tirador', '+5 de puntería por nivel.', { mods: { aim: 5 } }),
  overcharge: t('specialist', 1, 1, 'overcharge', 2, 'Sobrecarga', 'El Protocolo de combate hace +1 de daño por nivel.', { mods: { protocolDamage: 1 }, needs: 'combatProtocol', kit: 'grillo' }),
  battery: t('specialist', 1, 1, 'battery', 2, 'Batería extra', '+1 uso del Protocolo de combate por nivel.', { charges: { combatProtocol: 1 }, needs: 'combatProtocol', kit: 'grillo' }),
  intrusion: t('specialist', 1, 2, 'intrusion', 2, 'Intrusión', '+10 de hackeo por nivel.', { mods: { hack: 10 } }),
  tunedRifle: t('specialist', 1, 3, 'tunedRifle', 2, 'Rifle afinado', '+5 de crítico por nivel.', { mods: { crit: 5 } }),
  capacitorDischarge: t('specialist', 1, 4, 'capacitorDischarge', 1, 'Descarga de condensador', 'El Grillo descarga en una zona: 3 de daño a todos (enfriamiento 4).', { ability: 'discharge', req: ['ofi', 7], kit: 'grillo' }),
  // Specialist · Enlace
  guardian: t('specialist', 2, 0, 'guardian', 3, 'Protector', 'El Protocolo de ayuda da +5 de defensa más por nivel.', { mods: { aid: 5 }, kit: 'grillo' }),
  coveringFire: t('specialist', 2, 0, 'coveringFire', 1, 'Fuego de cobertura', 'La vigilancia también dispara a quien ataque, no solo a quien se mueva.'),
  bridge: t('specialist', 2, 1, 'bridge', 1, 'Puente', 'Con los soldados de tu compañero, el botiquín cura +2 y el Protocolo de ayuda da +10 de defensa.', { coop: true, kit: 'grillo' }),
  calm: t('specialist', 2, 1, 'calm', 2, 'Calma', '+8 de voluntad por nivel.', { mods: { will: 8 } }),
  mutualCover: t('specialist', 2, 2, 'mutualCover', 2, 'Cobertura mutua', '+5 de defensa por nivel.', { mods: { defense: 5 } }),
  instructor: t('specialist', 2, 3, 'instructor', 2, 'Instructor', '+5 de puntería por nivel.', { mods: { aim: 5 } }),
  threatAssessment: t('specialist', 2, 4, 'threatAssessment', 1, 'Evaluación de amenazas', 'El Protocolo de ayuda también pone al aliado en vigilancia.', { req: ['tem', 6], kit: 'grillo' }),
  // Specialist · Voz (Canto)
  calmSong: t('specialist', 3, 0, 'calmSong', 2, 'Canto de calma', '+10 de voluntad por nivel a los aliados a 4 casillas o menos.', { mods: { calmAura: 1 } }),
  counterSong: t('specialist', 3, 1, 'counterSong', 1, 'Contracanto', 'Libera a un aliado a 10 casillas o menos del control mental, el pánico y la desorientación, y lo hace inmune al Canto durante 2 turnos (enfriamiento 3).', { ability: 'counterSong' }),
  hymn: t('specialist', 3, 2, 'hymn', 1, 'Himno', 'Los aliados a 6 casillas o menos ganan +10 de puntería y +10 de defensa hasta vuestro próximo turno (enfriamiento 4).', { ability: 'hymn' }),
  lightHands: t('specialist', 3, 3, 'lightHands', 2, 'Manos de luz', 'Tus curas sanan +1 por nivel.', { mods: { heal: 1 } }),
  lastVerse: t('specialist', 3, 4, 'lastVerse', 1, 'Último verso', 'Una vez por misión: un soldado caído este turno vuelve en pie con 3 de salud. Si no hay ninguno, cura 4 a todos los aliados a 6 casillas.', { ability: 'lastVerse', charges: { lastVerse: 1 }, req: ['tem', 7] }),
};

/** The centre's talents. */
export const CENTER: TalentDef[] = Object.values(TALENTS).filter((d) => d.axis === null);

/** An axis's talents (null = the centre). */
export function talentsOf(axis: ClassId | null): TalentDef[] {
  return Object.values(TALENTS).filter((d) => d.axis === axis);
}

/** Points a talent row needs above it in its branch. */
export function rowGate(d: Pick<TalentDef, 'row' | 'song'>): number {
  return (d.song ? SONG_GATES : ROW_GATES)[d.row];
}

/** Canto rows the Cámara de Ecos opens at this level. */
export function songRowsOpen(echoesLevel: number): number {
  return SONG_ROWS_OPEN[Math.max(0, Math.min(3, echoesLevel))]!;
}

/** Level of the Cámara de Ecos a Canto row needs. */
export function songRowLevel(row: number): number {
  return SONG_ROWS_OPEN.findIndex((open) => open > row);
}

export function talentPointsFree(b: Pick<Build, 'rank' | 'talents'>): number {
  return TALENT_POINTS_PER_RANK * b.rank - total(b.talents);
}

/** Talent points spent in the rows of a branch above `row`. */
export function pointsAbove(talents: Talents, axis: ClassId, branch: number, row: number): number {
  return talentsOf(axis)
    .filter((d) => d.branch === branch && d.row < row)
    .reduce((sum, d) => sum + (talents[d.id] ?? 0), 0);
}

export function pointsInBranch(talents: Talents, axis: ClassId, branch: number): number {
  return pointsAbove(talents, axis, branch, ROW_GATES.length);
}

/** Points in an axis: all of them, or only its ordinary branches (what specialises a recruit). */
export function pointsInAxis(talents: Talents, axis: ClassId, ordinaryOnly = false): number {
  return talentsOf(axis)
    .filter((d) => !ordinaryOnly || !d.song)
    .reduce((sum, d) => sum + (talents[d.id] ?? 0), 0);
}

/** Main axis (the class, null for a recruit) and the axes opened besides it. */
export function axesOf(b: Pick<Build, 'cls' | 'talents'>): { main: ClassId | null; second: ClassId | null; open: ClassId[] } {
  const open = AXES.filter((a) => a !== b.cls && pointsInAxis(b.talents, a) > 0);
  return { main: b.cls, second: b.cls ? (open[0] ?? null) : null, open };
}

/** The class this talent would make a retrained recruit already past SPECIALIZE_RANK, or null. */
export function wouldSpecialize(b: Pick<Build, 'cls' | 'rank' | 'talents'>, id: PerkId): ClassId | null {
  const d = TALENTS[id];
  if (b.cls || b.rank < SPECIALIZE_RANK || !d?.axis || d.song) return null;
  return pointsInAxis(b.talents, d.axis, true) + 1 >= SPECIALIZE_POINTS ? d.axis : null;
}

/** Whether a learnt talent waits for equipment the soldier doesn't carry (it does nothing yet). */
export function dormant(b: Pick<Build, 'cls'>, id: PerkId): boolean {
  const kit = TALENTS[id]?.kit;
  return !!kit && KIT_CLASS[kit] !== b.cls;
}

/**
 * The class a recruit leans to: the axis with the most talent points, counting
 * the points raised in its two aptitudes too. Ties go to talent points, then
 * to the order of the axes.
 */
export function leaningClass(b: Pick<Build, 'attrs' | 'talents'>): ClassId {
  const score = (a: ClassId) => 2 * pointsInAxis(b.talents, a) + CLASS_TOP[a].reduce((sum, id) => sum + b.attrs[id] - RECRUIT_ATTRS[id], 0);
  return [...AXES].sort((x, y) => score(y) - score(x) || pointsInAxis(b.talents, y) - pointsInAxis(b.talents, x))[0]!;
}

/** Makes the recruit that class: +1 to its two aptitudes. */
export function specialize(b: Build, cls: ClassId): void {
  b.cls = cls;
  for (const attr of CLASS_TOP[cls]) b.attrs[attr]++;
}

/** Why the talent can't take one more level, or null if it can. `echoes` is the Cámara de Ecos's level. */
export function talentBlocker(b: Build, id: PerkId, echoes = 0): string | null {
  const d = TALENTS[id];
  if (!d) return 'Ese talento no existe.';
  if ((b.talents[id] ?? 0) >= d.max) return `${d.name} ya está al máximo.`;
  if (talentPointsFree(b) <= 0) return 'No quedan puntos de talento: cada ascenso da 2.';
  if (!d.axis) return null;
  const axis = d.axis;
  const becomes = wouldSpecialize(b, id);
  // A recruit may learn it ahead: it sleeps until the soldier carries the kit.
  if (d.kit && b.cls && b.cls !== KIT_CLASS[d.kit]) return `Necesita ${KIT_NAMES[d.kit]}.`;
  if (axis !== b.cls) {
    const { open } = axesOf(b);
    const others = b.cls ? open : open.filter((a) => a !== axis);
    if (!open.includes(axis) && others.length >= (b.cls ? 1 : 2)) {
      const names = [b.cls, ...open].filter((a): a is ClassId => !!a).map((a) => TEMPLATES[a].name);
      return `Ya tienes dos ejes: ${names.join(' y ')}.`;
    }
    if (d.row >= SECOND_AXIS_ROWS && becomes !== axis) return b.cls ? 'Las dos últimas filas son solo de tu eje principal.' : `Las dos últimas filas se abren al especializarse, en ${SPECIALIZE_RANK_NAME}.`;
  }
  if (d.song) {
    if (d.row >= songRowsOpen(echoes)) return echoes ? `Hace falta la Cámara de Ecos a nivel ${songRowLevel(d.row)}.` : 'Hace falta la Cámara de Ecos.';
  }
  const gate = rowGate(d);
  const above = pointsAbove(b.talents, axis, d.branch, d.row);
  if (above < gate) return `Esta fila pide ${gate} ${gate === 1 ? 'punto' : 'puntos'} en ${BRANCHES[axis][d.branch].name} (lleva ${above}).`;
  if (d.needs && !b.talents[d.needs]) return `Antes hace falta ${TALENTS[d.needs].name}.`;
  if (d.req && b.attrs[d.req[0]] < d.req[1]) return `Pide ${ATTRS[d.req[0]].name} ${d.req[1]} (tiene ${b.attrs[d.req[0]]}).`;
  return null;
}

/**
 * Takes one level of the talent (check `talentBlocker` first). A retrained
 * recruit past SPECIALIZE_RANK whose axis reaches SPECIALIZE_POINTS becomes
 * that class again; younger recruits wait for the rank (campaign promotions).
 */
export function learnTalent(b: Build, id: PerkId): { specialized: ClassId | null } {
  const becomes = wouldSpecialize(b, id);
  b.talents[id] = (b.talents[id] ?? 0) + 1;
  if (becomes) specialize(b, becomes);
  return { specialized: becomes };
}

/** Talents with at least one level: what combat sees as the unit's perks. */
export function talentIds(talents: Talents): PerkId[] {
  return (Object.keys(talents) as PerkId[]).filter((id) => (talents[id] ?? 0) > 0);
}

/** Drops unknown talents and levels past their max (old saves). */
export function cleanTalents(talents: Talents): Talents {
  const out: Talents = {};
  for (const id of talentIds(talents)) {
    const d = TALENTS[id];
    if (d) out[id] = Math.min(d.max, Math.floor(talents[id]!));
  }
  return out;
}

// ------------------------------------------------------------------- stats

/** Everything a soldier fights with, before campaign upgrades (armour tier, techs, traits). */
export interface SoldierStats {
  template: TemplateId;
  mods: UnitMods;
  maxHp: number;
  charges: Partial<Record<ChargeId, number>>;
  will: number;
  armor: number;
  perks: PerkId[];
}

/** Aim, health, defence and the rest from aptitudes, veterancy, talents and equipment. */
export function soldierStats(b: Build, gear: ItemId[] = []): SoldierStats {
  const template = soldierTemplate(b.cls);
  const tpl = TEMPLATES[template];
  const { pul, agu, ref, tem, ofi } = b.attrs;
  let mods = addMods(
    { ...ZERO_MODS },
    {
      aim: 50 + 3 * pul - tpl.aim + VETERAN_AIM * b.rank,
      defense: 2 * Math.max(0, ref - 3),
      hack: 5 * (ofi - 5),
    },
  );
  let maxHp = 3 + Math.floor(agu / 2);
  let armor = 0;
  const charges = { ...tpl.charges };
  const addCharges = (extra: Partial<Record<ChargeId, number>> | undefined, times = 1) => {
    for (const [c, n] of Object.entries(extra ?? {}) as [ChargeId, number][]) charges[c] = (charges[c] ?? 0) + n * times;
  };
  // Extras at 7 and 10.
  if (pul >= 7) mods = addMods(mods, { crit: 10 });
  if (pul >= 10) mods = addMods(mods, { critDamage: 1 });
  if (agu >= 10) armor += 1;
  if (ref >= 7) mods = addMods(mods, { mobility: 1 });
  if (ref >= 10) mods = addMods(mods, { reactionDefense: 20 });
  if (tem >= 7) mods = addMods(mods, { psiGuard: 1 });
  if (tem >= 10) mods = addMods(mods, { psiGuard: 1 });
  if (ofi >= 7) addCharges({ grenade: 1 });
  if (ofi >= 10) mods = addMods(mods, { heal: 1, grenadeDamage: 1 });

  // Talents asleep for want of their class's equipment do nothing yet.
  const perks = talentIds(b.talents).filter((id) => !dormant(b, id));
  for (const id of perks) {
    const d = TALENTS[id];
    const level = b.talents[id]!;
    if (d.mods) for (let i = 0; i < level; i++) mods = addMods(mods, d.mods);
    if (d.onceMods) mods = addMods(mods, d.onceMods);
    if (d.maxMods && level >= d.max) mods = addMods(mods, d.maxMods);
    maxHp += (d.hp ?? 0) * level;
    addCharges(d.charges, level);
  }
  // The price of the Canto: whoever sings is easier for a Cantor to sing to, unless deaf to the Coro.
  if (perks.some((id) => TALENTS[id].song)) mods = addMods(mods, { singer: 1, songOpen: tem >= 10 ? 0 : 10 });
  for (const item of gear.map((id) => ITEMS[id])) {
    if (item.mods) mods = addMods(mods, item.mods);
    maxHp += item.hp ?? 0;
    addCharges(item.charges);
  }
  return { template, mods, maxHp, charges, will: 16 + 6 * tem + VETERAN_WILL * b.rank + mods.will, armor, perks };
}

/** Days off for a wound, shortened by Aguante 7. */
export function woundDays(lost: number, attrs: Attributes): number {
  const days = 2 + lost * 2;
  return attrs.agu >= 7 ? Math.ceil(days * 0.7) : days;
}

// -------------------------------------------------------------- auto build

function weighted<T>(rng: Rng, items: [T, number][]): T | null {
  const sum = items.reduce((s, [, w]) => s + w, 0);
  if (sum <= 0) return null;
  let roll = rng.int(sum);
  for (const [item, w] of items) {
    roll -= w;
    if (roll < 0) return item;
  }
  return null;
}

/**
 * A plausible soldier of that class and rank (skirmish veterans): points spent
 * leaning on the class's aptitudes and one main branch, a few in the centre.
 */
export function autoBuild(cls: ClassId, rank: number, rng: Rng): Build {
  const b: Build = { cls, rank, attrs: baseAttrs(cls), talents: {} };
  while (attrPointsFree(b) > 0) {
    const pick = weighted(
      rng,
      ATTR_IDS.filter((id) => !attrBlocker(b, id)).map((id): [AttrId, number] => [id, CLASS_TOP[cls].includes(id) ? 3 : 1]),
    );
    if (!pick) break;
    b.attrs[pick]++;
  }
  const main = rng.int(3);
  while (talentPointsFree(b) > 0) {
    const open = [...talentsOf(cls), ...CENTER].filter((d) => !talentBlocker(b, d.id));
    const pick = weighted(rng, open.map((d): [PerkId, number] => [d.id, d.axis === null ? 1 : d.branch === main ? 6 : 2]));
    if (!pick) break;
    learnTalent(b, pick);
  }
  return b;
}
