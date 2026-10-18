/**
 * The story (docs/LORE.md): mission briefings, the Diario pages, the story
 * chapters and the epilogues. Apart from the chapters' mission setup (`STORY`),
 * which the campaign reads, it is text only.
 */
import type { MissionOffer, MissionReward, RegionId } from './campaign';
import type { Room } from './loot';
import { DISMANTLE_BACK, FELL_COST, type PieceId } from './base';
import { FOUND_WEAPONS, NIGHT, THIRD_ACTION_RANK, type RangeTable } from './content';
import { BAR_GROWTH, DEED_XP, RANK_STEP, SKILLS, VETERANCY, WILL_PER_RANK, type DeedId, type Deeds, type SkillId } from './skills';
import type { Alarm, Biome, DecorKind, FoundWeaponId, ItemId, MissionKind, Site, TemplateId } from './types';

/** Bastion level at which the doom track gets its name: until the radio is fixed nobody knows the Marea is coming. */
export const PARTITURA_LEVEL = 4;

/**
 * The squad's bus (docs/LORE.md «El Bastión»), named once for every text that
 * names it: `to` and `of` contract like a zone's, should it ever start with «El».
 */
const BUS_NAME = 'La Lata';
export const BUS = { name: BUS_NAME, to: toZone(BUS_NAME), of: ofZone(BUS_NAME), in: `en ${BUS_NAME}` };

/** The doom track as a label: "La Marea" once known. */
export function doomName(level: number): string {
  return level >= PARTITURA_LEVEL ? 'La Marea' : 'Infestación';
}

/** The doom track inside a sentence: "la Marea". */
export function doomSubject(level: number): string {
  return level >= PARTITURA_LEVEL ? 'la Marea' : 'la infestación';
}

/** The doom track at a glance: "La Marea, a 9 km" once known, "Infestación 3/12" before. */
export function doomStatus(level: number, doom: number): string {
  return level >= PARTITURA_LEVEL ? `La Marea, a ${Math.max(0, DOOM_TRACK - doom)} km` : `Infestación ${doom}/${DOOM_TRACK}`;
}

/** Length of the doom track (campaign.ts DOOM_MAX): the Marea starts this many km away. */
const DOOM_TRACK = 12;

/** "La Marea se acerca 1 km." / "La infestación baja (−2)." */
export function doomChangeText(level: number, delta: number): string {
  if (level >= PARTITURA_LEVEL) return delta > 0 ? `La Marea se acerca ${delta} km.` : `La Marea retrocede ${-delta} km.`;
  return delta > 0 ? `La infestación crece (+${delta}).` : `La infestación baja (−${-delta}).`;
}

// ----------------------------------------------------------------- glossary

/** One line for each word of the world, for tooltips. */
export const GLOSSARY: Record<string, string> = {
  Pandemia: 'Empezó hace dos inviernos en el Instituto Galen. Quien se contagia muere en un día y se levanta como zombi.',
  Zombi: 'Va a por el vivo que ve. Si no ve a nadie, va hacia el último ruido que oyó.',
  Corredor: 'Zombi rápido que ve de lejos.',
  Acorazado: 'Zombi con chaleco y casco: tiene blindaje.',
  Chillón: 'Zombi que no muerde: grita. El grito asusta a los soldados cercanos, atrae a los zombis que lo oyen y levanta a los muertos.',
  Hinchado: 'Zombi lento. Revienta al morir o al llegar a un soldado: hace daño a quien tenga al lado y puede contagiarlo.',
  Cuervos: 'Bandas de saqueadores armados. Atacan a otros vivos y atraen hordas con sirenas. Los zombis también van a por ellos.',
  Congregación: 'Secta que cree que los zombis están salvados. Los guía con campanas y por radio.',
  Marea: 'Horda enorme que sube desde los Llanos de Ceniza. Si llega al Bastión, se pierde la campaña.',
  Nido: 'Sitio donde se juntan los zombis. Hace crecer la Marea; quemarlo la hace retroceder.',
  Sirena: 'Mientras suena, atrae a todos los zombis de la zona. Puede ser un coche patrulla, una ambulancia o la alarma de una nave.',
  Ruido: 'Cada acción hace ruido. Un disparo se oye a entre 12 y 22 casillas, según el arma; una granada, a 26; un registro, a 2. Los enemigos van a ver qué ha sido. El ruido no te descubre: te descubre que te vean.',
  Saqueo: 'Misión de registrar y volver con el botín. No hay límite de turnos, pero entran zombis por los bordes, y antes cuanto más ruido hagas.',
  Registrar: 'Una acción dentro de una habitación. Da lo que haya según el sitio: comida y medicinas en las casas, piezas en las oficinas, comida en las tiendas, de todo en los almacenes. Cada habitación se registra una sola vez. Hace un poco de ruido.',
  Cajas: 'Hasta tres por mapa, cada una de un tipo: armas, medicinas, munición o comida. Su marca queda en el mapa en cuanto se ve. Se abren con una acción, desde al lado, y siempre dan lo suyo.',
  Mochila: 'Tres huecos por soldado, cuatro con la Chapistería. Soltar es gratis; pasar algo a un soldado de al lado cuesta una acción. Las armas y las medicinas se pueden usar ahí mismo. El botín solo cuenta si vuelve. Lo que llevaba un caído se queda en el suelo, y otro puede recogerlo.',
  Armería: 'Las armas del Bastión: las encontradas, las que hace el Taller y las que llegan al subir de nivel. Cada una es de quien la lleva.',
  Cargadores: 'Los de repuesto los hace el Taller: cada soldado sale con uno por tanda hecha, hasta tres. Valen para todas las armas. Recargar llena primero el arma principal y luego la pistola. Registrando se encuentran más.',
  Suministros: 'Sirven para construir, reparar, subir de nivel, reclutar, equipar y contactar zonas. Se consiguen en las misiones y de los enclaves.',
  Medicinas: 'Se cuentan en ✚. Tratar a un mordido cuesta 2 (1 con la Enfermería a nivel 2). Una dosis acorta la baja de un herido. En la misión, las de la mochila curan 3 con una acción.',
  Mordisco: 'El de un zombi puede contagiar. En la misión, el contagiado pierde 1 de salud cada turno y, sin Cuarentena, no puede subir a La Lata. Al volver tiene fiebre: si no se le trata, se convierte. La Enfermería y la Cuarentena dan más tiempo. Si muere contagiado, se levanta.',
  Cuarentena: 'Sin ella, un mordido no puede subir a La Lata y se queda en la misión.',
  Comida: 'Se cuenta en raciones. Cada misión son cinco días, y cada día se gasta una ración por cada cuatro soldados. Sin raciones hay hambre: −10 de voluntad en las misiones y las heridas curan a la mitad de velocidad.',
  Huerto: 'Da raciones en cada misión; más cuanto más nivel.',
  Piezas: 'Con ellas el Taller hace armas y armaduras mejores, y se reponen las trampas. Salen de almacenes, maleteros y oficinas, y del equipo de los Acorazados.',
  Noche: 'De noche, tus soldados solo ven de cerca. Bajo una farola, una bengala o un fuego se ve como de día. Hasta que te descubren, los enemigos solo te notan de cerca si estás a oscuras. Hay más zombis, y el botín vale la mitad más.',
  Enclave: 'Grupo de supervivientes con el que hay contacto por radio. Mientras su zona no caiga, manda suministros en cada convoy.',
  Rumor: 'Algo que se oye por radio en una zona. Si se escucha durante unas misiones, se sabe qué es.',
  Encargo: 'Cadena de misiones y decisiones con un desenlace que se queda.',
  'Taller de oficios': 'Cada misión llena más las barras.',
  'La Lata': `El vehículo que lleva a la escuadra y la espera en la zona de evacuación. Empieza siendo un todoterreno; luego, furgoneta, camión y autobús. Cuanto más grande, más soldados caben.`,
  Evacuar: `Se sale ${BUS.in} desde la zona de evacuación: lo pide un jugador y lo acepta el otro. Quien esté fuera cuando sale, se pierde. En una Limpieza, salir es retirarse, y la misión se pierde.`,
  Recluta: 'Superviviente recién llegado, con cuchillo y pistola.',
  Barras: 'Seis: Disparo, Cuerpo a cuerpo, Lanzar, Curar, Investigar y Forma física. Se llenan haciendo eso mismo en las misiones. Cada barra llena sube un poco su habilidad y da un punto para el tablero.',
  Tablero: 'Las habilidades de cada soldado, alrededor de su Instinto. Se compran con puntos de habilidad, siempre al lado de una que ya tenga. Lo comprado no se pierde.',
  Veteranía: 'Cuenta los puntos gastados en el tablero: un rango cada cinco (Novato, Fogueado, Curtido, Veterano y Superviviente). Cada rango da +10 de voluntad, y desde Veterano hay una tercera acción por turno.',
};

// ---------------------------------------------------------- enemy files

export interface EnemyFile {
  /** What it is, in the world's words. */
  what: string;
  /** What it does on the field. */
  does: string;
  /** How to deal with it. */
  tip: string;
}

/** The Bastion's file on each enemy, for the enemy sheet in combat. */
export const ENEMY_FILES: Partial<Record<TemplateId, EnemyFile>> = {
  zombie: {
    what: 'El muerto corriente.',
    does: 'Lento. No se cubre ni tiene miedo. Va a por el vivo más cercano que vea; si no ve a nadie, hacia el último ruido. Muerde cuerpo a cuerpo: uno de cada cuatro mordiscos contagia.',
    tip: 'Peligroso en grupo. El ruido los atrae.',
  },
  xenoid: {
    what: 'Zombi reciente y rápido.',
    does: 'Corre más que los demás y ve de lejos. Ataca cuerpo a cuerpo y también contagia, algo menos que un zombi.',
    tip: 'Llega antes de lo que parece: pon vigilancia en su camino.',
  },
  mec: {
    what: 'Zombi con chaleco y casco.',
    does: 'Blindaje 1: resta 1 de daño a cada golpe. Aguanta mucho. Es el que más contagia.',
    tip: 'Los explosivos le quitan el blindaje, y la munición perforante lo atraviesa.',
  },
  sectoid: {
    what: 'Zombi que grita.',
    does: 'No muerde. Su grito desorienta o asusta a los soldados cercanos y se oye a 16 casillas: atrae a todos los zombis que lo oyen. Levanta a los muertos que tiene cerca.',
    tip: 'Mátalo primero.',
  },
  bloater: {
    what: 'Zombi lleno de gases.',
    does: 'Lento. Revienta al morir o al llegar a un soldado: 3 de daño a quien tenga al lado, y contagia a uno de cada dos.',
    tip: 'Mátalo de lejos, y lejos de tus soldados.',
  },
  trooper: {
    what: 'Saqueador de los Cuervos.',
    does: 'Escopeta o fusil de caza, y alguna granada. Se cubre y dispara.',
    tip: 'Rómpele la cobertura o flanquéalo. Sus disparos también atraen zombis.',
  },
  officer: {
    what: 'Jefe de un grupo de Cuervos.',
    does: 'Mejor arma y más aguante que un Saqueador. Usa la vigilancia.',
    tip: 'Es el más peligroso del grupo: dispárale primero.',
  },
  lancer: {
    what: 'Cuervo que captura en vez de matar.',
    does: 'Red y porra cuerpo a cuerpo: el soldado inmovilizado empieza su siguiente turno con una sola acción.',
    tip: 'Corre mucho y no necesita línea de tiro. Pon vigilancia en su camino y no dejes a nadie solo.',
  },
  relay: {
    what: 'La alarma: un coche patrulla, una ambulancia o la de una nave.',
    does: 'No dispara. Tiene salud y blindaje 1. Mientras suena, la zona se llena de zombis.',
    tip: 'Es el objetivo: hay que callarla antes de que acabe la cuenta atrás. Los explosivos ayudan.',
  },
};

// ------------------------------------------------------------------ regions

export interface RegionLore {
  /** Who sends the reports from there, as it reads after "Aviso de". */
  ally: string;
  situation: string;
}

export const REGION_LORE: Record<RegionId, RegionLore> = {
  weu: { ally: 'el Refugio de Brena', situation: 'Los pueblos de la sierra, bajo el Bastión. La nieve y la altura frenan a los zombis; los Cuervos, no.' },
  afr: { ally: 'los del Hospital', situation: 'La primera ciudad caída. Calles llenas de zombis y farmacias que nadie ha terminado de vaciar.' },
  eas: { ally: 'la Red Callada', situation: 'Un embalse y una central muerta. La Red Callada sobrevivió hablando en lengua de signos: sin un ruido.' },
  eeu: { ally: 'los Pastores', situation: 'Valles cerrados y aldeas que aún resisten. Los Cuervos cobran aquí su peaje.' },
  me: { ally: 'los del Instituto', situation: 'La ciudad muerta donde empezó todo. El Instituto Galen sigue en pie, y nadie quiere entrar.' },
  na: { ally: 'los Camioneros', situation: 'La llanura por donde sube la Marea. Los camioneros la vigilan desde la carretera.' },
  sa: { ally: 'el Sindicato de la Mina', situation: 'Minas y carreteras cortadas. Los mineros resisten con túneles y dinamita.' },
  oce: { ally: 'las Bodegas', situation: 'Granjas, bodegas y un río. Es donde hay comida, y por eso donde más se pelea.' },
};

// ------------------------------------------------------------- searching

/** A piece of furniture that can be searched, as the log and the button read it. */
export interface SearchSpot {
  /** With its article, as it reads after «Abrir»: «la caja de armas». */
  name: string;
  /** What the supplies found in it were; one is picked by seed. */
  supplies: string[];
}

/**
 * What can be opened (docs/FASE8.md §B): the marked boxes, one kind each. They
 * always give what they say, so they need no supplies of their own.
 */
export const SEARCH_SPOTS: Partial<Record<DecorKind, SearchSpot>> = {
  lootWeapons: { name: 'la caja de armas', supplies: [] },
  lootMedical: { name: 'la caja de medicinas', supplies: [] },
  lootAmmo: { name: 'la caja de munición', supplies: [] },
  lootFood: { name: 'la caja de comida', supplies: [] },
};

/** A room as its furniture makes it (loot.ts Room.spot): what it is called, and what the supplies found in it were. */
const ROOMS: Partial<Record<Room['spot'], SearchSpot>> = {
  home: { name: 'la habitación', supplies: ['ropa de abrigo', 'mantas y un saco de dormir', 'una caja de zapatos con pilas y velas', 'cubiertos, cerillas y un mechero', 'una linterna y pilas en la mesilla'] },
  office: { name: 'el despacho', supplies: ['pilas, cinta americana y un cúter', 'una radio de manivela', 'una linterna y un juego de llaves', 'pilas detrás de los expedientes'] },
  shop: { name: 'la tienda', supplies: ['tabaco y mecheros', 'pilas y bombillas', 'jabón y lejía', 'velas y botellas de agua'] },
  store: { name: 'el almacén', supplies: ['herramientas', 'bidones de agua y lonas', 'cuerda, alambre y clavos', 'repuestos y cable'] },
};

/** When a search turns up nothing. */
export const EMPTY_FINDS = ['Nada. Alguien pasó antes.', 'Vacío. Solo polvo.', 'Nada que sirva.', 'Nada, salvo una foto de familia.', 'Ya lo registraron. Hace tiempo.'];

/** What the medicines found were. */
export const MEDICINE_FINDS = ['un blíster de antibióticos', 'una caja de ibuprofeno a medias', 'vendas sin abrir', 'un frasco de yodo', 'jarabe para la tos', 'un tubo de pomada antibiótica'];

/** Accessories as they read when found: with their article. */
export const ITEM_FINDS: Record<ItemId, string> = {
  fragGrenade: 'dos granadas',
  smokeGrenade: 'una granada de humo',
  flashbang: 'una granada aturdidora',
  firstAid: 'un botiquín',
  nanoVest: 'un chaleco antibalas',
  mindShield: 'unos tapones',
  apRounds: 'munición perforante',
  tracerRounds: 'munición trazadora',
};

/** What a search turned up, as the log needs it: Gameplay's loot, reduced to its parts. */
export interface SearchFind {
  supplies?: number;
  item?: ItemId;
  medicines?: number;
  /** Raciones (docs/FASE4.md). */
  food?: number;
  /** Piezas, for the Taller. */
  parts?: number;
  /** A found weapon (content.ts FOUND_WEAPONS). */
  weapon?: FoundWeaponId;
  /** Magazines (docs/FASE7.md), straight to the searcher's spares. */
  mags?: number;
}

/** What the rations found were. */
export const FOOD_FINDS = ['latas de lentejas', 'un saco de arroz', 'conservas de atún', 'paquetes de galletas', 'un tarro de miel', 'pasta y tomate frito'];

/** What the parts found were. */
export const PARTS_FINDS = ['muelles y tornillería', 'un motor pequeño', 'cable de cobre', 'rodamientos', 'una batería de coche', 'piezas de una bicicleta'];

/** «1 ración», «3 raciones»: the Bastion's food, counted. */
export function rations(n: number): string {
  return n === 1 ? '1 ración' : `${n} raciones`;
}

/** «1 pieza», «4 piezas»: what the Taller makes things with. */
export function partsCount(n: number): string {
  return n === 1 ? '1 pieza' : `${n} piezas`;
}

/** What a magazine found was (docs/FASE7.md): it fits the searcher's weapon. */
export const MAG_FINDS = ['un cargador lleno', 'un cargador que le vale', 'una caja de balas de su calibre'];

/** «1 cargador», «3 cargadores»: a soldier's spares. */
export function magsCount(n: number): string {
  return n === 1 ? '1 cargador' : `${n} cargadores`;
}

function joinAnd(parts: string[]): string {
  return parts.length > 1 ? `${parts.slice(0, -1).join(', ')} y ${parts[parts.length - 1]}` : (parts[0] ?? '');
}

/** What a search turned up, as a list for the log: the supplies' flavour comes from where they were found. */
function findParts(find: SearchFind | null, supplies: string[], seed: number): string[] {
  const parts: string[] = [];
  if (find?.supplies) parts.push(`${supplies.length ? supplies[seed % supplies.length] : 'suministros'} (+${find.supplies} §)`);
  if (find?.item) parts.push(ITEM_FINDS[find.item]);
  if (find?.medicines) parts.push(find.medicines === 1 ? MEDICINE_FINDS[seed % MEDICINE_FINDS.length]! : `${MEDICINE_FINDS[seed % MEDICINE_FINDS.length]!} (${find.medicines} ✚)`);
  if (find?.food) parts.push(`${FOOD_FINDS[seed % FOOD_FINDS.length]!} (${rations(find.food)})`);
  if (find?.parts) parts.push(`${PARTS_FINDS[seed % PARTS_FINDS.length]!} (${partsCount(find.parts)})`);
  if (find?.weapon) parts.push(FOUND_WEAPONS[find.weapon].find);
  if (find?.mags) parts.push(find.mags === 1 ? MAG_FINDS[seed % MAG_FINDS.length]! : magsCount(find.mags));
  return parts;
}

/** The log line for opening a marked box: «Ana abre la caja de armas: un fusil de asalto.» */
export function searchLine(who: string, decor: DecorKind, find: SearchFind | null, seed: number): string {
  const where = SEARCH_SPOTS[decor]?.name ?? 'la caja';
  const parts = findParts(find, [], seed);
  return parts.length ? `${who} abre ${where}: ${joinAnd(parts)}.` : `${who} abre ${where}. Está vacía.`;
}

/** The log line for searching a room (docs/FASE8.md §B): «Ana registra el despacho: una radio de manivela (+12 §) y 1 pieza.» */
export function roomSearchLine(who: string, spot: Room['spot'], find: SearchFind | null, seed: number): string {
  const room = ROOMS[spot] ?? ROOMS.home!;
  const parts = findParts(find, room.supplies, seed);
  return parts.length ? `${who} registra ${room.name}: ${joinAnd(parts)}.` : `${who} registra ${room.name}. ${EMPTY_FINDS[seed % EMPTY_FINDS.length]!}`;
}

/** Opening a marked box, for the action's title: «Abrir la caja de armas». */
export function boxTitle(decor: DecorKind): string {
  return `Abrir ${SEARCH_SPOTS[decor]?.name ?? 'la caja'}`;
}

/** What a search turned up, short, to float over the furniture: «+12 §», «Botiquín», «Medicinas», «+2 raciones», «+1 pieza», a weapon's name, «+1 cargador» or «Nada». */
export function findLabel(find: SearchFind | null): string {
  const parts: string[] = [];
  if (find?.supplies) parts.push(`+${find.supplies} §`);
  if (find?.item) parts.push(ITEM_FINDS[find.item].replace(/^(una?|unos|unas) /, '').replace(/^./, (ch) => ch.toUpperCase()));
  if (find?.medicines) parts.push('Medicinas');
  if (find?.food) parts.push(`+${rations(find.food)}`);
  if (find?.parts) parts.push(`+${partsCount(find.parts)}`);
  if (find?.weapon) parts.push(FOUND_WEAPONS[find.weapon].name);
  if (find?.mags) parts.push(`+${magsCount(find.mags)}`);
  return parts.join(' · ') || 'Nada';
}

// -------------------------------------------------------------- learning

/** How each deed reads in the debrief (docs/FASE10.md §3), one and many, and whether the noun is feminine. */
const DEED_WORDS: Record<DeedId, [string, string, boolean]> = {
  shot: ['disparo', 'disparos', false],
  hit: ['acierto', 'aciertos', false],
  gunKill: ['baja', 'bajas', true],
  swing: ['tajo', 'tajos', false],
  blow: ['golpe', 'golpes', false],
  bladeKill: ['baja', 'bajas', true],
  throw: ['lanzamiento', 'lanzamientos', false],
  throwHit: ['lanzamiento con blanco', 'lanzamientos con blanco', false],
  healOther: ['cura a un compañero', 'curas a compañeros', true],
  healSelf: ['cura propia', 'curas propias', true],
  search: ['registro', 'registros', false],
  fix: ['arreglo', 'arreglos', false],
  hunker: ['vez a cubierto', 'veces a cubierto', true],
  run: ['carrera', 'carreras', true],
};

const NUMBERS = ['', 'un', 'dos', 'tres', 'cuatro', 'cinco', 'seis', 'siete', 'ocho', 'nueve', 'diez'];

/** «un acierto», «una baja», «tres golpes», «12 registros». */
function deedCount(deed: DeedId, n: number): string {
  const [one, many, feminine] = DEED_WORDS[deed];
  if (n === 1) return `${feminine ? 'una' : 'un'} ${one}`;
  return `${NUMBERS[n] ?? n} ${many}`;
}

/**
 * The debrief's line for a skill a mission taught: «+3 de Tiro: dos aciertos y
 * una baja». What the base adds on top (the Taller de oficios, the Camposanto)
 * goes at the end: «… (+1 en la base)».
 */
export function learnedLine(skill: SkillId, gained: number, deeds: Deeds): string {
  const own = (Object.keys(DEED_XP) as DeedId[]).filter((d) => DEED_XP[d][0] === skill && (deeds[d] ?? 0) > 0);
  const raw = own.reduce((sum, d) => sum + DEED_XP[d][1] * deeds[d]!, 0);
  const why = own.map((d) => deedCount(d, deeds[d]!));
  const head = `+${gained} de ${SKILLS[skill].name}`;
  const base = gained > raw && raw > 0 ? ` (+${gained - raw} en la base)` : '';
  return why.length ? `${head}: ${joinAnd(why)}${base}` : head;
}

/**
 * Bars filled on coming back (docs/FASE10.md §2), one line per line: «Ana
 * llena una barra de Disparo: +7 % de puntería con armas de fuego. Tiene un
 * punto para el tablero.». `growth`, what each bar added.
 */
export function barLine(who: string, skill: SkillId, growth: number[]): string {
  const n = growth.length;
  const bars = n === 1 ? 'una barra' : `${NUMBERS[n] ?? n} barras`;
  const percent = growth.reduce((a, b) => a + b, 0);
  const stat = SKILLS[skill].stat.charAt(0).toLowerCase() + SKILLS[skill].stat.slice(1);
  return `${who} llena ${bars} de ${SKILLS[skill].name}: +${percent} % de ${stat}. ${n === 1 ? 'Tiene un punto de habilidad' : `Tiene ${NUMBERS[n] ?? n} puntos de habilidad`} para el tablero.`;
}

/**
 * A weapon that comes back worn or broken (docs/FASE9.md §5): «La escopeta de Ana
 * vuelve gastada.», «El fusil de asalto de Kofi vuelve roto.». Its gender from
 * the article of FOUND_WEAPONS' find.
 */
export function wornLine(who: string, kind: FoundWeaponId, broken = false): string {
  const w = FOUND_WEAPONS[kind];
  const female = w.find.startsWith('una ');
  return `${female ? 'La' : 'El'} ${w.name.toLowerCase()} de ${who} vuelve ${broken ? (female ? 'rota' : 'roto') : female ? 'gastada' : 'gastado'}.`;
}

/**
 * Each cell of the hex board (board.ts), in a line of the world, in the
 * Bastión's voice: never the effect, which the sheet shows apart
 * (docs/FASE10.md §6). By cell id, the core's too.
 */
export const BOARD_ABOUT: Record<string, string> = {
  core: 'Lo que le ha mantenido vivo hasta hoy. Lo demás se aprende encima.',
  steadyHand: 'Respirar, soltar el aire a medias, y entonces.',
  grip: 'Cinta en el mango y la muñeca firme.',
  looseArm: 'Se pasó la infancia tirando piedras al río.',
  bandage: 'Apretar donde sangra, y apretar bien.',
  tinker: 'Desmonta cualquier cosa para ver cómo va.',
  stamina: 'Subía a la sierra corriendo antes de que hubiera de qué huir.',
  lightTrigger: 'El gatillo no se aprieta: se acaricia.',
  sidearm: 'La pistola es lo último que queda. Más vale que sirva.',
  edge: 'Una hoja afilada cada noche corta a la primera.',
  sidestep: 'Un muerto embiste en línea recta. Basta con no estar ahí.',
  sharpStone: 'Mejor una piedra con aristas que una redonda.',
  curtain: 'Humo de sobra para cruzar una calle.',
  calmVoice: 'Hablar bajo cuando todos gritan.',
  revival: 'Un cachete, su nombre en voz alta, y vuelve.',
  lookout: 'Mira antes de entrar. Siempre.',
  quiet: 'Abre los cajones como si hubiera alguien durmiendo.',
  crouch: 'Lo primero que se aprende fuera.',
  quickReflex: 'Salta antes de saber por qué.',
  aimedShot: 'Es lo único que los para del todo.',
  closeIn: 'A esa distancia no hay excusas.',
  fineTrigger: 'Una pistola bien cuidada no tiembla.',
  elbowRoom: 'Cuando le rodean, hay que abrir hueco a golpes.',
  bladeCrit: 'A la sien, a la nuca. Donde acaba.',
  closeRanks: 'Juntos, no se cuela nadie.',
  salvo: 'La anilla, el brazo, y a lo siguiente.',
  bigCharge: 'Teo pone algo más de lo que dice el manual.',
  decoy: 'Un golpe contra una chapa lejos de aquí, y todos miran allí.',
  bridge: 'Los del otro también son de los nuestros.',
  bigKit: 'Una bolsa más grande y mejor ordenada.',
  firstAid: 'Primero se aparta al herido de la línea de tiro; luego se le cura.',
  patchUp: 'Cinta, alambre y un poco de paciencia.',
  deft: 'Las cerraduras le duran poco.',
  crossfire: 'Dos ángulos y ningún sitio donde esconderse.',
  sprinter: 'Cuando hay que correr, llega antes que nadie.',
  lungs: 'Aguanta sin resollar donde otros se paran.',
  dodgeFire: 'De esquina en esquina, nunca en línea recta.',
  rapidFire: 'Cuando no hay tiempo para apuntar dos veces.',
  flanker: 'Por donde no miran, duele más.',
  rooftop: 'Arriba se respira, y se ve todo.',
  coldBlood: 'Esperar a que se acerquen sin que tiemble la mano.',
  implacable: 'Sin pararse a mirar al que acaba de caer.',
  heavyBlow: 'Todo el peso del cuerpo en el golpe.',
  parry: 'Por debajo de la línea de tiro, siempre.',
  grit: 'Ha visto de cerca lo que otros solo ven de lejos.',
  goodArm: 'Llega más lejos, y siempre lleva una de más.',
  shrapnel: 'Tornillos y clavos dentro de la carga.',
  dazzle: 'Un destello de más en el bolsillo.',
  overhand: 'Por encima del hombro, llega más lejos.',
  lightHands: 'Nwosu le enseñó qué dar, cuánto y cuándo.',
  suture: 'Aguja, hilo y pulso.',
  bedside: 'Alguien que se sienta al lado y no se va.',
  toughSkin: 'Las heridas pequeñas ya ni las nota.',
  care: 'Limpia el arma cada noche, aunque no la haya usado.',
  sideBySide: 'Con el otro al lado, se apunta mejor.',
  parapet: 'El compañero cubre lo que uno no ve.',
  picklock: 'Un alambre doblado abre más puertas que una llave.',
  runAndGun: 'Cuando hace falta, saca fuerzas de donde no hay.',
  sturdy: 'Nació para trabajar, no para quejarse.',
  temper: 'Ya no le tiembla nada.',
  fixedPost: 'Elige su sitio y no se mueve de él.',
  chainShot: 'Si el primero entra, el segundo ya va de camino.',
  hollowPoint: 'Munición de caza, limada en el taller de Teo.',
  bigBore: 'Una pistola vieja, de las que ya no se fabrican.',
  sentryCrit: 'Quien espera, elige dónde dar.',
  deathFromAbove: 'Desde arriba, uno cae y el siguiente ya está en la mira.',
  bladeMaster: 'Años de monte antes de que todo empezara.',
  earBlow: 'Un buen golpe en la cabeza aturde hasta a los muertos.',
  butcher: 'Antes de la Pandemia, trabajaba en un matadero.',
  bladeWall: 'Quien llega a su lado no da el segundo paso.',
  backstab: 'Si no le ve llegar, no se gira a tiempo.',
  safeBlast: 'Sabe dónde están los suyos antes de soltar la anilla.',
  blastWave: 'La metralla vuela más lejos de lo que parece.',
  bandolier: 'Una cinta cruzada al pecho, y una granada más.',
  boulder: 'No hace falta que sea bonita, solo que pese.',
  demolition: 'Abría galerías en la mina de Carvik con dinamita.',
  restoration: 'Una vuelta por todos, sin dejarse a nadie.',
  surgeon: 'Ha cosido en sitios peores que este.',
  lullaby: 'Sabe qué decir para que nadie salga corriendo.',
  spares: 'Vendas y gasas de sobra en cada bolsillo.',
  triage: 'Mientras venda, se pone en medio.',
  morePatch: 'Siempre le queda un trozo de cinta.',
  binoculars: 'Unos prismáticos de cazador, sin una raya.',
  coolHead: 'Primero piensa, luego se mueve.',
  shoulder: 'Dos que se conocen pelean como uno.',
  pointOut: 'Un gesto, y el otro ya sabe dónde disparar.',
  shadowstep: 'Cuando quieren apuntarle, ya no está.',
  leathery: 'Ni los muertos pueden con su pellejo.',
  eel: 'Se escurre entre las manos de cualquiera.',
  vest: 'Ha salido de peores.',
  dugIn: 'Sacos terreros, y a esperar.',
  lightningHands: 'La pistola sale antes de que nadie se dé cuenta.',
  apRounds: 'Todo chaleco tiene un hueco. Hay que saber dónde.',
  shredder: 'Lo que no atraviesa, rompe.',
  serenity: 'Si da, da de verdad.',
  coveringFire: 'Mientras vigila, nadie toca a los suyos.',
  noEscape: 'De cerca, ya no hay adónde huir.',
  steelNerves: 'Ha oído gritar a un Chillón muchas veces.',
  cleanCut: 'Un tajo, y no hace falta otro.',
  waist: 'Un giro de cadera vale más que un escudo.',
  onGuard: 'La hoja en alto y los pies quietos.',
  hardHead: 'Se ha llevado más golpes de los que cuenta.',
  sweep: 'Un paso de más es un mordisco.',
  thrower: 'Una piedra bien tirada llega a la otra acera.',
  smokeEyes: 'Sabe dónde estaban antes de que subiera el humo.',
  doubleCharge: 'El doble de lo que hace falta, por si acaso.',
  crater: 'Donde estalla, no queda nada en pie.',
  fog: 'Para salir de un sitio sin que lo vean.',
  stoning: 'Una buena pedrada tumba a cualquiera.',
  rounds: 'Otra vuelta antes de que acabe el día.',
  fieldMedic: 'Ha curado tirado en el barro, bajo la lluvia.',
  ironBlood: 'Come poco, duerme menos y no enferma nunca.',
  cheer: 'Con alguien así al lado, se aguanta un poco más.',
  courage: 'Tiene miedo, como todos. No se le nota.',
  kitBag: 'El maletín de un médico de pueblo, encontrado en una casa vacía.',
  engineer: 'Trabajaba en la central del Pantano Gris.',
  magnifier: 'Se fija en lo que nadie mira.',
  weakSpot: 'Todo tiene un punto por donde cede.',
  temperance: 'Ha oído gritos peores que los de un Chillón.',
  lynx: 'Ve moverse algo en la niebla antes que nadie.',
  bodge: 'No queda bonito, pero aguanta hasta casa.',
  marathon: 'Va y vuelve de Brena sin parar.',
  ironBody: 'Las caídas, los golpes, el frío: todo le resbala.',
  poise: 'Nada le hace perder la calma.',
  feline: 'Cae de pie y sigue corriendo.',
  ironWill: 'Sabe por qué sigue, y eso le basta.',
  agile: 'Salta vallas como si no estuvieran.',
  killZone: 'Una calle, un arma y toda la noche.',
  faceoff: 'Cuando le rodean, se dispara a todos a la vez.',
  rupture: 'Una herida abierta por la que entra todo lo demás.',
  suppression: 'Que no asome la cabeza. Ni lo intente.',
  marker: 'Donde da uno, apuntan todos.',
  heavyHand: 'El arma de siempre, y la costumbre de usarla.',
  eagleEye: 'Lo que otros ven como una mancha, aquí tiene cara.',
  secondWind: 'Cada uno que cae le da fuerzas para el siguiente.',
  cleaver: 'Lo que corta leña corta hueso.',
  slippery: 'Nunca está donde le esperan.',
  duelist: 'De uno en uno, no hay muerto que aguante.',
  rampart: 'Cuero, cinta y lo que haga falta bajo la ropa.',
  bladeDance: 'La hoja, siempre en medio de lo que viene.',
  killerInstinct: 'Lo que antes era alguien ya no lo es. No lo piensa.',
  blaster: 'Las cargas las prepara a mano, y nunca le han fallado.',
  shockwave: 'Lo que no alcanza la metralla, lo alcanza el golpe.',
  cartridge: 'Cada hueco del cinturón, aprovechado.',
  flares: 'Para cuando hay que cegar a muchos a la vez.',
  lure: 'Los muertos van al ruido. Basta con decidir dónde suena.',
  ironArm: 'Años cargando sacos en el puerto de Galen.',
  sling: 'Una tira de cuero y un buen giro.',
  miracles: 'Lo que otros darían por perdido, se cose.',
  presence: 'Basta con que esté para que los demás se calmen.',
  vigor: 'Se recupera antes que nadie.',
  serenityMind: 'Ha visto morir a mucha gente. Ya no se asusta.',
  warKit: 'Material militar que se sacó de Atalaya.',
  fieldHospital: 'Donde se para, se cura a todos.',
  goodHand: 'Los heridos se calman en cuanto le ven las manos.',
  mastermind: 'Teo dice que tiene manos de taller.',
  inseparable: 'Saben dónde está el otro sin mirar.',
  watchman: 'Mientras uno avanza, el otro mira por los dos.',
  anatomy: 'Sabe dónde dar para que no se levanten.',
  watchtower: 'Desde lo alto de cualquier cosa, todo el pueblo.',
  shadow: 'Se mueve junto a las paredes, sin ruido y sin prisa.',
  ingenuity: 'Con lo que hay a mano, arregla lo que haga falta.',
  titan: 'Grande como un armario, y igual de difícil de mover.',
  longRunner: 'Lleva dos inviernos sin dejar de correr.',
  rock: 'Le pueden dar. No le mueven.',
  untouchable: 'Dicen que nadie le ha puesto la mano encima.',
  bulwark: 'Donde se planta, los demás se ponen detrás.',
  lastStand: 'No piensa caer antes que nadie.',
  tireless: 'Nunca se sienta antes que los demás.',
};

/** Floating over a soldier whose bar fills in the field (docs/PRUEBA-2026-10-16.md #10): «DISPARO +7 %». */
export function barFillFloat(skill: SkillId, percent: number): string {
  return `${SKILLS[skill].name.toUpperCase()} +${percent} %`;
}

/**
 * At the end of a bar (#16): what filling it gives. `short` beside the bar,
 * «+5–10 % · +1 punto»; `long` for its tooltip. «Punto de habilidad» in full:
 * no «PH», which reads as the pH and is the only acronym the game would have.
 */
export function barReward(skill: SkillId): { short: string; long: string } {
  const [low, high] = BAR_GROWTH;
  const stat = SKILLS[skill].stat.charAt(0).toLowerCase() + SKILLS[skill].stat.slice(1);
  return {
    short: `+${low}–${high} % · +1 punto`,
    long: `Al llenarse: entre un ${low} y un ${high} % más de ${stat}, y un punto de habilidad para el tablero.`,
  };
}

/** At the end of the veterancy bar: what the next rank gives (its name by the soldier's sex), or null at the top. */
export function rankReward(rank: number, sex?: 'f' | 'm'): { short: string; long: string } | null {
  const next = VETERANCY[rank + 1];
  if (!next) return null;
  const name = sex === 'f' ? next.female : next.name;
  const third = rank + 1 === THIRD_ACTION_RANK;
  return {
    short: third ? `+${WILL_PER_RANK} de voluntad · +1 acción` : `+${WILL_PER_RANK} de voluntad`,
    long: `${name}, con ${RANK_STEP} puntos de habilidad más gastados en el tablero: +${WILL_PER_RANK} de voluntad${third ? ', y una tercera acción por turno' : ''}.`,
  };
}

/**
 * A weapon's card in the Armería (aim.ts weaponProfile; Arturo, 17/10: «para
 * saber qué elegir»): the label of each figure, short.
 */
export const WEAPON_LABELS = {
  damage: 'Daño',
  critDamage: 'Daño crítico',
  crit: 'Crítico',
  aim: 'Puntería',
  clip: 'Cargador',
  actions: 'Acciones por disparo',
  actionsMelee: 'Acciones por golpe',
  noise: 'Ruido',
  shred: 'Quita blindaje',
  reach: 'Alcance',
  range: 'Puntería según la distancia',
};

/**
 * Each reach, its name and what the weapon is good at, as its range table
 * says (content.ts RANGE_TABLES): the shotgun adds most close and takes away
 * from twelve tiles on, the rifles add close and never take away, the
 * precision rifle takes away close and nothing from eleven on.
 */
export const REACH: Record<RangeTable, { name: string; line: string }> = {
  short: { name: 'Corto', line: 'De cerca. Cuanto más cerca, más acierta; desde doce casillas, resta puntería.' },
  medium: { name: 'Medio', line: 'Vale para todo: suma de cerca y no resta de lejos.' },
  long: { name: 'Largo', line: 'De lejos. De cerca resta mucha puntería; desde once casillas, ya no.' },
  none: { name: 'Cuerpo a cuerpo', line: 'Contra quien está al lado. Casi no se oye.' },
};

/** A figure of the card, in words: «4 balas», «2 acciones», «12 casillas»; the blade «No gasta». */
export function weaponFigure(what: 'clip' | 'actions' | 'noise', n: number, melee = false): string {
  if (what === 'clip') return melee || !n ? 'No gasta' : `${n} ${n === 1 ? 'bala' : 'balas'}`;
  if (what === 'actions') return `${n} ${n === 1 ? 'acción' : 'acciones'}`;
  return `${n} ${n === 1 ? 'casilla' : 'casillas'}`;
}

// -------------------------------------------------------------- backpack

/**
 * What a backpack's thing is, in a sentence (docs/FASE8.md §C): «un
 * botiquín», «2 raciones», «12 suministros», «un fusil de asalto», «un
 * cargador».
 */
export function lootName(loot: SearchFind): string {
  const parts: string[] = [];
  if (loot.supplies) parts.push(loot.supplies === 1 ? '1 suministro' : `${loot.supplies} suministros`);
  if (loot.item) parts.push(ITEM_FINDS[loot.item]);
  if (loot.medicines) parts.push(loot.medicines === 1 ? 'una medicina' : `${loot.medicines} medicinas`);
  if (loot.food) parts.push(rations(loot.food));
  if (loot.parts) parts.push(partsCount(loot.parts));
  if (loot.weapon) parts.push(FOUND_WEAPONS[loot.weapon].find);
  if (loot.mags) parts.push(loot.mags === 1 ? 'un cargador' : magsCount(loot.mags));
  return joinAnd(parts) || 'nada';
}

/** The log lines of the backpack: «Ana deja en el suelo un botiquín.», «Ana recoge 2 raciones.», «Ana le da a Kofi un cargador.» */
export function lootDroppedLine(who: string, loot: SearchFind): string {
  return `${who} deja en el suelo ${lootName(loot)}.`;
}

export function lootTakenLine(who: string, loot: SearchFind): string {
  return `${who} recoge ${lootName(loot)}.`;
}

export function lootGivenLine(who: string, to: string, loot: SearchFind): string {
  return `${who} le da a ${to} ${lootName(loot)}.`;
}

/** Finding something with the backpack full, without a verb to agree: «Mochila llena: 2 medicinas, al suelo.» */
export function packFullLine(loot: SearchFind): string {
  return `Mochila llena: ${lootName(loot)}, al suelo.`;
}

/**
 * A medicine used in the field: «Ana se cura con una medicina: +3 de salud.»,
 * «Ana cura a Kofi con una medicina: +3 de salud, y el contagio se corta.».
 * `to` is null on oneself.
 */
export function medicineLine(who: string, to: string | null, healed: number, cured: boolean): string {
  const what = to ? `${who} cura a ${to} con una medicina` : `${who} se cura con una medicina`;
  const effects = [healed ? `+${healed} de salud` : '', cured ? 'el contagio se corta' : ''].filter(Boolean);
  return effects.length ? `${what}: ${effects.join(', y ')}.` : `${what}.`;
}

/** Floating over whoever is cured of the bite. */
export const CURED_FLOAT = 'SIN CONTAGIO';

/** Taking up a found weapon in the field: «Ana empuña un fusil de asalto.» */
export function weaponEquippedLine(who: string, kind: FoundWeaponId): string {
  return `${who} empuña ${FOUND_WEAPONS[kind].find}.`;
}

/** Putting on the belt what was found (playtest 18/10 #5): the backpack's button, its tooltip and the floating word. */
export const BELT_TEXT = {
  button: 'Al cinturón',
  tip: 'Lo lleva en el cinturón y puede usarlo ya.',
  float: 'AL CINTURÓN',
};

/** The log line: «Ana se cuelga dos granadas al cinturón.»; the earplugs go in the ears: «Ana se pone unos tapones.» */
export function beltLine(who: string, item: ItemId): string {
  return item === 'mindShield' ? `${who} se pone ${ITEM_FINDS[item]}.` : `${who} se cuelga ${ITEM_FINDS[item]} al cinturón.`;
}

// --------------------------------------------------------------- objective

/** What a mission's objective is called when nothing names it (a skirmish). */
export const DEFAULT_ITEM = 'la carga';

/** The item down to its article and noun: «el registro de prisioneros» → «el registro». */
function shortItem(item: string): string {
  const words = item.split(' ');
  return /^(el|la|los|las)$/.test(words[0]!) ? words.slice(0, 2).join(' ') : words[0]!;
}

/** The carrier's tag, short and in capitals, over their head and on their sheet: «MEDICINAS», «REGISTRO». */
export function itemTag(item: string): string {
  return item.replace(/^(el|la|los|las) /, '').split(' ')[0]!.toUpperCase();
}

/** Floating over whoever picks it up: «RECOGE LAS MEDICINAS». */
export function itemTakenFloat(item: string): string {
  return `RECOGE ${shortItem(item).toUpperCase()}`;
}

/** The log line when it is picked up: «Ana recoge las medicinas.» */
export function itemTakenLine(who: string, item: string): string {
  return `${who} recoge ${item}.`;
}

/** Floating where it falls when its carrier goes down: «MEDICINAS AL SUELO». */
export function itemDroppedFloat(item: string): string {
  return `${itemTag(item)} AL SUELO`;
}

/** The log line when it falls: «Las medicinas han caído al suelo.», «El diario ha caído al suelo.» */
export function itemDroppedLine(item: string): string {
  return `${item.charAt(0).toUpperCase()}${item.slice(1)} ${/^(los|las) /.test(item) ? 'han' : 'ha'} caído al suelo.`;
}

/** Where the objective is, for the strip over the battlefield (docs/PRUEBA-2026-10-14.md #23). */
export type ItemPhase = 'loose' | 'carried' | 'aboard' | 'out';

/**
 * The objective strip for an item: «Recuperad las medicinas y volved a La
 * Mula», «Ana lleva las medicinas: a La Lata», «Las medicinas, en La Lata: que
 * suba el resto», «Las medicinas, a salvo». `carrier` names who has it.
 */
export function itemObjective(item: string, phase: ItemPhase, carrier?: string): string {
  const Item = `${item.charAt(0).toUpperCase()}${item.slice(1)}`;
  switch (phase) {
    case 'loose':
      return `Recuperad ${item} y volved ${BUS.to}`;
    case 'carried':
      return carrier ? `${carrier} lleva ${item}: ${BUS.to}` : `Llevad ${item} ${BUS.to}`;
    case 'aboard':
      return `${Item}, ${BUS.in}: que suba el resto`;
    case 'out':
      return `${Item}, a salvo`;
  }
}

/**
 * A name with a title takes its article: «la Dra. Irina Sokolova», «el Ing.
 * Kenji Mori», «la alcaldesa Mara Velasco»; a bare name doesn't.
 */
function titled(name: string): string {
  const m = /^(Dra\.|Dr\.|Ing\.|Alcaldesa|Agente) /.exec(name);
  if (!m) return name;
  const title = m[1]!;
  const female = title === 'Dra.' || title === 'Alcaldesa' || NAMED_SEX[name] === 'f';
  return `${female ? 'la' : 'el'} ${title.endsWith('.') ? title : title.toLowerCase()} ${name.slice(m[0].length)}`;
}

/**
 * The same for a rescue: «Llegad hasta la Dra. Irina Sokolova» while still
 * held, «Llevad a la Dra. Irina Sokolova a La Lata», «La Dra. Irina Sokolova,
 * en La Lata: que suba el resto», «…, a salvo».
 */
export function vipObjective(vip: string, phase: 'captive' | 'loose' | 'aboard' | 'out'): string {
  const who = titled(vip);
  if (phase === 'captive') return `Llegad hasta ${who}`;
  if (phase === 'loose') return `Llevad ${who.startsWith('el ') ? `al ${who.slice(3)}` : `a ${who}`} ${BUS.to}`;
  const Who = `${who.charAt(0).toUpperCase()}${who.slice(1)}`;
  return phase === 'aboard' ? `${Who}, ${BUS.in}: que suba el resto` : `${Who}, a salvo`;
}

/** A sabotage's objective strip, by what sounds: «Apagad la sirena del coche patrulla». */
export function alarmObjective(alarm?: Alarm): string {
  return `Apagad ${alarm ? ALARMS[alarm].name : 'la alarma'}`;
}

/** The log line when it stops: «Ana apaga la sirena de la ambulancia.» */
export function alarmDoneLine(who: string, alarm?: Alarm): string {
  return `${who} apaga ${alarm ? ALARMS[alarm].name : 'la alarma'}.`;
}

/** The strip once the objective of a sabotage or a repair is done: back to the bus. */
export const OBJECTIVE_DONE: Partial<Record<MissionKind, string>> = {
  sabotage: `Ya no suena: volved ${BUS.to}`,
  hack: `Equipo en marcha: volved ${BUS.to}`,
};

/**
 * What sounds in a sabotage (docs/PRUEBA-2026-10-15.md #14): a police car the
 * Cuervos took the siren from, an ambulance whose siren stuck, or a factory's
 * alarm running on its battery. `name` for a sentence, `find` to introduce it.
 */
export const ALARMS: Record<Alarm, { name: string; find: string }> = {
  police: { name: 'la sirena del coche patrulla', find: 'un coche patrulla con la sirena puesta' },
  ambulance: { name: 'la sirena de la ambulancia', find: 'una ambulancia con la sirena atascada' },
  factory: { name: 'la alarma de la nave', find: 'la alarma de una nave, que salta sola con la batería' },
};

/**
 * What an operation is, in one line, for «Qué hace» (docs/PRUEBA-2026-10-15.md
 * #4): «Recuperar las medicinas y volver a La Lata», «Sacar a la alcaldesa Mara
 * Velasco con vida…». The item, the hostage and the alarm when known (campaign
 * missionInfo); without them, words that still hold. The same line for story
 * chapters and quest steps: none of them says more than the briefing.
 */
export function operationAim(kind: MissionKind, o: { item?: string; vip?: string; alarm?: Alarm; facility?: boolean; final?: boolean } = {}): string {
  if (o.final) return 'Asaltar el campamento de la Congregación y abatir a todo lo que haya.';
  const alarm = o.alarm ? ALARMS[o.alarm].name : 'la alarma';
  if (o.facility) return `Apagar ${alarm} que alimenta el Nido antes de que termine la cuenta atrás, y volver ${BUS.to}.`;
  switch (kind) {
    case 'elimination':
      return `Abatir a todo lo que haya. Se gana sin volver ${BUS.to}.`;
    case 'recovery':
      return `Recuperar ${o.item ?? DEFAULT_ITEM} y volver ${BUS.to}.`;
    case 'sabotage':
      return `Apagar ${alarm} antes de que termine la cuenta atrás, y volver ${BUS.to}.`;
    case 'hack':
      return `Poner en marcha el equipo y volver ${BUS.to}. Si falla, salta la alarma.`;
    case 'rescue': {
      const who = o.vip ? titled(o.vip) : 'alguien';
      return `Sacar ${who.startsWith('el ') ? `al ${who.slice(3)}` : `a ${who}`} con vida y volver ${BUS.to}.`;
    }
    case 'scavenge':
      return `Registrar lo que se pueda y volver ${BUS.to}. Llegan muertos por los bordes.`;
  }
}

/** «Qué hace» for clearing a plot (campaign.ts TERRAINS): «Despejar el bosque de C4 y dejar la parcela libre para construir.» */
export function clearAim(terrain: 'rocks' | 'forest' | 'ruins', plot: string): string {
  const what = { rocks: 'la roca', forest: 'el bosque', ruins: 'las ruinas' }[terrain];
  return `Despejar ${what} de ${plot} y dejar la parcela libre para construir.`;
}

/** «Qué hace» for a contact: who answers, what they send and what it opens. */
export function contactAim(id: RegionId, income: number): string {
  return `Hablar por radio con ${REGION_LORE[id].ally}: ${income} § en cada convoy mientras su zona no caiga, y se pueden asaltar sus Nidos.`;
}

/** «Qué hace» for listening to a rumour: what it may turn out to be. */
export function rumorAim(zone: string): string {
  return `Escuchar el rumor ${ofZone(zone)} unos días: puede ser un encargo, una misión mejor pagada, un alijo o nada.`;
}

/** «Qué hace» for an order to the Taller (progression.ts CRAFTS): its `kind` and `the`. */
export function craftAim(kind: string, the: string): string {
  if (kind === 'arms') return `Hacer ${the}: va a la Armería y se reparte desde allí.`;
  if (kind === 'ammo') return `Hacer ${the}: un cargador de repuesto más para cada soldado que salga.`;
  return `Hacer ${the}: para todos, hasta lo que permita su rango.`;
}

/** The partner's notice: «Ana lleva las medicinas». */
export function itemPartnerLine(who: string, item: string): string {
  return `${who} lleva ${item}`;
}

// ---------------------------------------------------------------- evacuate

/**
 * Leaving in La Lata (docs/PRUEBA-2026-10-14.md P1): one player calls it from
 * the bus, the other answers, and whoever is outside when it leaves is lost.
 * `clearing`: a Limpieza, where leaving is a retreat that loses the mission;
 * `ready`: the objective is done (always so in a Saqueo); `left`: who would
 * stay outside.
 */
export interface EvacSituation {
  clearing: boolean;
  ready: boolean;
  left: string[];
}

/** What leaving now costs, in a sentence or two: the mission, and who stays outside. */
function evacStakes(o: EvacSituation): string {
  const lose = o.clearing
    ? 'Si os vais ahora, la Limpieza queda a medias y la misión se pierde.'
    : !o.ready
      ? 'El objetivo no está cumplido: si salís ahora, la misión se pierde.'
      : '';
  const out = !o.left.length
    ? 'Estáis todos dentro.'
    : o.left.length === 1
      ? `${o.left[0]} se quedaría fuera, y se perdería.`
      : `${joinAnd(o.left)} se quedarían fuera, y se perderían.`;
  return lose ? `${lose} ${out}` : out;
}

/** The prompt on reaching the bus: leave or wait. `partner`: who has to accept, or null when playing alone. */
export function evacPrompt(o: EvacSituation, partner: string | null): { title: string; text: string; go: string; wait: string } {
  const title = o.clearing ? 'Retirada' : o.ready ? `Salir ${BUS.in}` : 'Salir sin el objetivo';
  const text = partner ? `${evacStakes(o)} ${partner} tiene que aceptar.` : evacStakes(o);
  return { title, text, go: o.clearing ? 'Retirarse' : partner ? 'Pedir salir' : 'Salir', wait: 'Esperar' };
}

/** The partner's notice: `who` asks to leave; accept or wait. */
export function evacAsk(who: string, o: EvacSituation): { title: string; text: string; accept: string; refuse: string } {
  return {
    title: o.clearing ? `${who} pide retirarse` : `${who} pide salir`,
    text: evacStakes(o),
    accept: o.clearing ? 'Retirarse' : 'Salir',
    refuse: 'Esperar',
  };
}

/** While the call waits for the partner's answer: «Esperando a Ana…». */
export function evacWaiting(partner: string): string {
  return `Esperando a ${partner}…`;
}

/** The log line when the partner says no: «Ana quiere esperar.» */
export function evacRefusedLine(who: string): string {
  return `${who} quiere esperar.`;
}

/** The bus leaves: the log line, with who is in it. */
export function evacDepartLine(out: string[]): string {
  return out.length ? `${BUS.name} sale con ${joinAnd(out)}.` : `${BUS.name} sale sin nadie.`;
}

/** The words for evacuating that don't depend on who. */
export const EVAC_TEXT = {
  /** Log, when a call nobody answered lapses with the squad's turn. */
  lapsed: 'Nadie ha contestado: la petición de salir caduca con el turno.',
  /** The evacuation notice's kicker, and the EVACUAR button's tooltip. */
  kicker: BUS.name,
  buttonTip: `Salir ${BUS.in}: quien esté fuera cuando salga se pierde`,
  /** The debrief's status of someone left outside (`lost`). */
  lost: 'Se quedó fuera',
  /** The mission's end when the bus left early: a Limpieza abandoned, or any other without its objective. */
  retreat: 'Retirada',
  withoutObjective: 'Salisteis sin el objetivo',
};

/**
 * A mission against the clock (playtest 18/10 #8–9): at the end of its last
 * turn the bus leaves by itself with whoever is in the zone, and those
 * outside are lost. The outcomes are headlines without a full stop.
 */
export const TIME_TEXT = {
  /** At the start of the last turn: the strip's alert and the log. */
  lastTurn: `Último turno: ${BUS.name} sale al acabarlo, con quien esté en la zona de evacuación.`,
  lastTurnFloat: 'ÚLTIMO TURNO',
  /** Log, as it leaves on time. */
  departs: `${BUS.name} no espera más.`,
  /** Outcomes: on time with the objective aboard (a win), without it (`timer`), with nobody aboard, and a mission with no bus. */
  wonOnTime: `Se acabó el tiempo, pero ${BUS.name} salió con el objetivo a bordo`,
  lostOnTime: `Se acabó el tiempo: ${BUS.name} se fue sin cumplir el objetivo`,
  leftEmpty: `Se acabó el tiempo: ${BUS.name} se fue sin nadie`,
  timeUp: 'Se acabó el tiempo',
};

/** With `n` turns left (5, playtest 18/10 #9): «Quedan 5 turnos. Al acabar el último, La Lata se va.» */
export function turnsLeftWarning(n: number): string {
  return `Quedan ${n} turnos. Al acabar el último, ${BUS.name} se va.`;
}

/**
 * The countdown on a proposal nobody answers (docs/PRUEBA-2026-10-14.md #28):
 * when it runs out, it is accepted. `left` comes formatted («1:25»); `mine`:
 * shown to whoever proposed it.
 */
export function proposalClock(left: string, mine: boolean): string {
  return mine ? `Si no contesta, se acepta sola en ${left}` : `Sin respuesta, se acepta sola en ${left}`;
}

/** The same for a call to leave: when it runs out, the bus goes. */
export function evacClock(left: string, mine: boolean): string {
  return mine ? `Si no contesta, ${BUS.name} sale en ${left}` : `Sin respuesta, ${BUS.name} sale en ${left}`;
}

/**
 * Leaving from the action bar, without a window (docs/PRUEBA-2026-10-15.md
 * #10): the partner's button, the card of «Esperar», and the notice.
 */
export const EVAC_BAR = {
  accept: 'Aceptar evacuación',
  wait: 'Esperar',
  /** The card when the partner chooses to wait. */
  stay: `${BUS.name} no sale todavía: seguís en la misión.`,
};

/** The notice to the partner: «Ana pide salir: «Aceptar evacuación» o «Esperar», en tu barra.» */
export function evacBarNotice(who: string): string {
  return `${who} pide salir: «${EVAC_BAR.accept}» o «${EVAC_BAR.wait}», en tu barra.`;
}

/**
 * The bite in the field (docs/PRUEBA-2026-10-15.md #13): the tag, its note,
 * the floating word and the log line. A noun, not «mordido», so it agrees with
 * everyone.
 */
export const BITE_TAG = 'MORDISCO';
export const BITE_FLOAT = 'MORDISCO −1';

/** Why someone bitten can't board without a Cuarentena: the button's reason. The same words are written in rules.ts (the evac refusal) and test/prueba15.test.ts: change the three together. */
export const BITE_NO_BOARD = `Con un mordisco no se sube a ${BUS.name} sin una Cuarentena en el Bastión.`;

/** The tag's note, on the sheet and the list: what it does now and what it will need. */
export function biteNote(quarantine: boolean): string {
  return `Mordisco: pierde 1 de salud al empezar cada turno y, si cae, se levanta. ${quarantine ? 'En la base necesitará el tratamiento.' : `Sin Cuarentena en el Bastión no sube a ${BUS.name}.`}`;
}

/** The log line each turn: «Ana pierde 1 de salud por el mordisco.» */
export function biteLossLine(who: string): string {
  return `${who} pierde 1 de salud por el mordisco.`;
}

/**
 * Leaving a mission early, asked of the partner like the bus (playtest 16/10):
 * whoever is still standing comes home, and the mission is lost.
 */
export const ABANDON_TEXT = {
  /** The button, and what it costs, for whoever asks. */
  button: 'Abandonar la misión',
  stakes: 'Los que sigan en pie vuelven a la base, pero la misión se da por perdida.',
  accept: 'Abandonar',
  refuse: 'Seguir',
};

/** The partner's notice: «Arturo quiere abandonar la misión». */
export function abandonAsk(who: string): string {
  return `${who} quiere abandonar la misión`;
}

/** The notice to the partner, as the bus's: «Arturo quiere abandonar la misión: «Abandonar» o «Seguir», en tu barra.» */
export function abandonBarNotice(who: string): string {
  return `${abandonAsk(who)}: «${ABANDON_TEXT.accept}» o «${ABANDON_TEXT.refuse}», en tu barra.`;
}

/** The log line when the partner says no: «Arturo quiere seguir.» */
export function abandonRefusedLine(who: string): string {
  return `${who} quiere seguir.`;
}

/** The log line for someone outside when the bus leaves: «Ana se queda fuera.» */
export function leftBehindLine(who: string): string {
  return `${who} se queda fuera.`;
}

// ------------------------------------------------------------------ quit

/**
 * Leaving the game from the desktop app (the Esc menu in a mission, the
 * base's menu, the lobby), named like the app's own menu. The room saves
 * after every action, so nothing is lost; what changes is the partner's side.
 */
export const QUIT_TEXT = {
  button: 'Salir de Bastión',
  title: '¿Salir de Bastión?',
  saved: 'La partida queda guardada tal como está.',
  savedMission: 'La partida queda guardada tal como está, con la misión a medias.',
  accept: 'Salir',
  refuse: 'Seguir',
};

/** For the host, with the partner connected: the server stops with the app. «La partida está en tu ordenador: si sales, Ana se desconecta hasta que vuelvas a abrirla.» */
export function quitHostWarning(partner: string): string {
  return `La partida está en tu ordenador: si sales, ${partner} se desconecta hasta que vuelvas a abrirla.`;
}

/** For the guest in a mission, with the host connected: the turns go on, and an absent player's soldiers never act. */
export function quitGuestWarning(partner: string): string {
  return `${partner} sigue la misión sin ti: tus soldados se quedan quietos donde están hasta que vuelvas.`;
}

/** For the guest when the host's game closes (client net.ts): the notice, its second line and, in a browser, the button. */
export const HOST_CLOSED = {
  back: 'Cuando la vuelva a abrir, podrás entrar de nuevo.',
  retry: 'Volver a entrar',
};

/** «Arturo ha cerrado la partida.»; without the host's name, a neutral one. */
export function hostClosedLine(host?: string): string {
  return host ? `${host} ha cerrado la partida.` : 'Quien aloja la partida la ha cerrado.';
}

/** Back to the window (minimised, or the laptop asleep) while the dead socket comes back (client net.ts). */
export const REJOIN_LINE = 'Volviendo a la partida…';

// --------------------------------------------------------------- settings

/** Ajustes and Controles (playtest 18/10 #3, #6, #7): labels, their help, and the plan's log lines. */
export const SETTINGS_TEXT = {
  /** #3: a soldier's route planned without the command, walked when it comes. */
  plans: 'Seguir las rutas planeadas',
  plansAbout: 'Sin el mando, puedes marcar el camino de un soldado. Cuando te llega el mando, lo recorre solo.',
  /** #6: the slider. */
  enemySpeed: 'Velocidad del turno enemigo',
  enemySpeedAbout: 'A qué velocidad se ve el turno enemigo: movimientos, apariciones y detecciones.',
  /** #7: the ping: its line in Controles (an infinitive, like the rest), its button and the button's tooltip, with the keys. */
  pingControls: 'Señalar un sitio unos segundos, para que lo veáis los dos',
  ping: 'Señalar',
  pingTip: 'Marca un sitio unos segundos, para que lo veáis los dos.',
  pingKeys: 'También con Alt+clic, clic con la rueda o G.',
};

/** #3: the soldier walks the planned route: «Mateo sigue la ruta planeada.» */
export function planFollowedLine(who: string): string {
  return `${who} sigue la ruta planeada.`;
}

/** #3: the route can no longer be walked; it stays marked: «Mateo ya no llega: la ruta sigue marcada.» */
export function planBlockedLine(who: string): string {
  return `${who} ya no llega: la ruta sigue marcada.`;
}

// ------------------------------------------------------------------ rubble

/** Walls and floors a grenade breaks (docs/FASE7.md §4–5): the log lines and the floating word. */
export const RUBBLE_TEXT = {
  /** Log, when the floor gives way under the blast. */
  floor: 'El suelo cede.',
  /** Floating over whoever falls through it. */
  fallFloat: '¡CAE!',
  /** Log, when the objective lying there goes down with the floor. */
  item: 'Lo que veníais a buscar cae con el suelo.',
};

/** The log line for a blast that breaks walls: one or several. */
export function breachLine(walls: number): string {
  return walls === 1 ? 'La explosión abre un boquete en la pared.' : 'La explosión abre boquetes en las paredes.';
}

/**
 * The log line for a fall through a broken floor: «Ana cae con el suelo a la
 * planta de abajo.», «… dos plantas.». The damage (FALL_DAMAGE a storey) has
 * its own line after, from `damaged`.
 */
export function fallLine(who: string, storeys: number): string {
  const n = Math.max(1, storeys);
  return `${who} cae con el suelo ${n === 1 ? 'a la planta de abajo' : `${['dos', 'tres'][n - 2] ?? n} plantas`}.`;
}

/**
 * The debrief's loot line: «El botín: +34 §, un botiquín, 2 medicinas, 3 raciones
 * y una escopeta recortada.», or null with nothing brought back.
 */
export function lootSummary(
  supplies: number,
  items: readonly ItemId[],
  medicines: number,
  food = 0,
  parts = 0,
  weapons: readonly FoundWeaponId[] = [],
): string | null {
  const list: string[] = [];
  if (supplies) list.push(`+${supplies} §`);
  for (const id of items) list.push(ITEM_FINDS[id]);
  if (medicines) list.push(medicines === 1 ? '1 medicina' : `${medicines} medicinas`);
  if (food) list.push(rations(food));
  if (parts) list.push(partsCount(parts));
  for (const w of weapons) list.push(FOUND_WEAPONS[w].find);
  return list.length ? `El botín: ${joinAnd(list)}.` : null;
}

// --------------------------------------------------------------- scavenge

/** Reports for the Saqueo mission (`scavenge`), in the style of BRIEFINGS. */
export const SCAVENGE_BRIEFINGS = [
  `Se vació a toda prisa, con las puertas cerradas y las despensas llenas. Registrad lo que podáis y volved ${BUS.to}. Cada tiro llama a más.`,
  `Nadie lo ha tocado desde el primer invierno. Llenad las mochilas y volved ${BUS.to}. Vendrán, y antes cuanto más ruido hagáis.`,
  `Cuando empezó todo, había gente dentro, y lo que dejaron sigue allí. Registrad, cargad y volved ${BUS.to} antes de que la horda os encuentre.`,
];

/** The Saqueo mission's lines for the combat screen and the debrief. */
export const SCAVENGE = {
  /** The mission's name (MISSION_NAMES). */
  name: 'Saqueo',
  /** Objective before anything is carried. */
  objective: 'Registrad lo que podáis y evacuad',
  /** Objective while the squad carries loot, short for the objective strip; `{n}` is how many things. */
  carrying: 'Botín: {n} · registrad o evacuad',
  /** The same, in full, for its tooltip. */
  carryingTip: `Lo que llevan las mochilas ({n}) cuenta si sale ${BUS.in}. Lo que llevaba un caído, o alguien que se queda fuera, se pierde.`,
  /** Debrief headline when loot came home. */
  success: 'Botín a salvo',
  /** Debrief headline when the squad came home with nothing. */
  emptyHanded: 'Volvéis con las mochilas vacías',
  /** The horde arrives at the edges. */
  horde: 'Llegan más zombis por los bordes',
};

// -------------------------------------------------------------------- base

/**
 * The base in the clearing (docs/FASE12.md §11; the draft in docs/LORE-BASE.md):
 * each piece's name, what it is for (the figures come from base.ts), and a
 * building's line of the world at each of its levels.
 */
export interface PieceText {
  name: string;
  /** With its article, for a sentence: «Repara la empalizada», «mejorar la Sala de radio»; buildings keep their capital, as names. */
  the: string;
  /** «construir una empalizada», «un Barracón». */
  a: string;
  line: string;
  levels?: [string, string, string];
}

export const PIECE_TEXT: Record<PieceId, PieceText> = {
  caravan: { name: 'Caravana', the: 'la caravana', a: 'una caravana', line: 'Camas para los primeros soldados y el almacén. No se desmonta.' },
  radio: {
    name: 'Sala de radio', the: 'la Sala de radio', a: 'una Sala de radio',
    line: 'Avisa de los asedios, por dónde y cuántos, y trae misiones y zonas que contactar.',
    levels: ['Una radio de coche, una batería y Morse con los cascos.', 'Una antena en el pino más alto.', 'La antena llega hasta el mar: Morse oye antes lo que viene.'],
  },
  quarters: { name: 'Barracón', the: 'el Barracón', a: 'un Barracón', line: 'Camas para soldados.', levels: ['Literas de madera y una estufa que solo se enciende de noche.', 'Un segundo piso de literas.', 'Mantas de verdad.'] },
  infirmary: {
    name: 'Enfermería', the: 'la Enfermería', a: 'una Enfermería',
    line: 'Las heridas curan antes.',
    levels: ['Una camilla y un hornillo para hervir el agua.', 'Agua caliente en la sala de curas.', 'Un quirófano de verdad, con lámpara y generador propio.'],
  },
  workshop: { name: 'Taller', the: 'el Taller', a: 'un Taller', line: 'Accesorios y armas más baratos.', levels: ['Un banco de trabajo y las herramientas de Teo.', 'Un torno de verdad.', 'Teo deja de quejarse.'] },
  garden: { name: 'Huerto', the: 'el Huerto', a: 'un Huerto', line: 'Da raciones en cada misión.', levels: ['Bancales bajo plástico y bidones para el agua de lluvia.', 'Un invernadero de verdad, con estufa.', 'Riego por goteo y semillas de La Vega.'] },
  quarantine: {
    name: 'Cuarentena', the: 'la Cuarentena', a: 'una Cuarentena',
    line: `Sin ella, un mordido no sube ${BUS.to}.`,
    levels: ['Una nave aparte, con cerrojo por fuera.', 'Catres separados y una ventana con rejas.', 'Una sala de guardia, y alguien despierto toda la noche.'],
  },
  memorial: {
    name: 'Camposanto', the: 'el Camposanto', a: 'un Camposanto',
    line: 'Los caídos descansan aquí y salen de la lista de Soldados; sus nombres se guardan.',
    levels: ['Cruces de pino bajo la nieve, una por cada uno.', 'Una cerca y un banco.', 'Los nombres, grabados en una tabla.'],
  },
  range: { name: 'Campo de tiro', the: 'el Campo de tiro', a: 'un Campo de tiro', line: 'Puntería para todos.', levels: ['Dianas de cartón en una trinchera del pinar.', 'Siluetas de zombi a cincuenta metros.', 'Blancos móviles, con una polea de Teo.'] },
  training: {
    name: 'Centro de entrenamiento', the: 'el Centro de entrenamiento', a: 'un Centro de entrenamiento',
    line: 'Los reclutas llegan sabiendo más; junto a la caravana, reclutar cuesta menos.',
    levels: ['Un patio despejado y un horario.', 'Una semana de prácticas antes de salir.', 'Alguien que ya ha vuelto de fuera les enseña.'],
  },
  simulation: {
    name: 'Sala de mapas', the: 'la Sala de mapas', a: 'una Sala de mapas',
    line: 'Las misiones dan más suministros.',
    levels: ['Un tablero con los caminos del valle.', 'Cada casa registrada, marcada con una chincheta.', 'Las rutas de los Camioneros, y dónde paraban.'],
  },
  garage: {
    name: 'Chapistería', the: 'la Chapistería', a: 'una Chapistería',
    line: 'Las heridas curan antes.',
    levels: [`${BUS.name} lleva camilla.`, 'Mantas y un botiquín fijo atrás.', `${BUS.name} ya no para a repostar con heridos dentro.`],
  },
  echoes: {
    name: 'Taller de oficios', the: 'el Taller de oficios', a: 'un Taller de oficios',
    line: 'Las barras se llenan más en cada misión.',
    levels: ['Lo que se aprende fuera, aquí se repasa.', 'Más herramientas, y Nwosu repasa las curas.', 'Gente de otros enclaves enseña lo suyo.'],
  },
  palisade: { name: 'Empalizada', the: 'la empalizada', a: 'una empalizada', line: 'Barata y débil: cede pronto.' },
  wall: { name: 'Muro', the: 'el muro', a: 'un muro', line: 'Caro, pero aguanta mucho.' },
  gate: { name: 'Puerta', the: 'la puerta', a: 'una puerta', line: 'Se abre y se cierra con una acción. Cerrada, los muertos tienen que tirarla.' },
  sandbags: { name: 'Barricada de sacos', the: 'la barricada de sacos', a: 'una barricada de sacos', line: 'Cobertura para disparar detrás.' },
  logs: { name: 'Pila de troncos', the: 'la pila de troncos', a: 'una pila de troncos', line: 'Cobertura baja. Lo más barato.' },
  tower: { name: 'Torre de vigía', the: 'la torre de vigía', a: 'una torre de vigía', line: 'Una planta de altura: más vista, y ventaja de altura al disparar.' },
  floodlight: { name: 'Foco', the: 'el foco', a: 'un foco', line: 'De noche, alumbra a su alrededor: ahí se ve como de día.' },
  wire: { name: 'Alambre de espino', the: 'el alambre de espino', a: 'un tramo de alambre de espino', line: 'Quien lo cruza va más despacio, vivo o muerto.' },
  mine: { name: 'Mina', the: 'la mina', a: 'una mina', line: 'Estalla una vez, cuando algo la pisa. Se repone con piezas.' },
  spikes: { name: 'Foso con pinchos', the: 'el foso con pinchos', a: 'un foso con pinchos', line: 'Hiere a quien cae dentro. Aguanta unas cuantas veces; luego se repone con piezas.' },
  snare: { name: 'Cepo', the: 'el cepo', a: 'un cepo', line: 'Atrapa al primero que lo pisa y lo retiene. Se repone con piezas.' },
  turret: { name: 'Torreta', the: 'la torreta', a: 'una torreta', line: 'Dispara sola, en el turno enemigo, a lo que se acerque. Sin munición se calla; se recarga con piezas.' },
};

/** A piece's state, a tag beside its name («Foso con pinchos · por reponer») and its line in the card; never gendered, the names are of both. */
export const PIECE_STATE = {
  spent: 'por reponer',
  spentLine: 'No le quedan usos: se repone con piezas.',
  silent: 'sin munición',
  silentLine: 'No dispara hasta que se recargue con piezas.',
  broken: 'por reparar',
  brokenLine: 'No sirve hasta que se repare.',
};

/** The base screen (UI: strategy/buildMode.ts, clearing.ts, screen.ts): its labels and its help. */
export const BUILD_TEXT = {
  build: 'Construir',
  fell: 'Talar',
  repair: 'Reparar',
  dismantle: 'Desmontar',
  rotate: 'Girar',
  upgrade: 'Mejorar',
  rearm: 'Reponer',
  fellTip: `Talar: cuesta ${FELL_COST} suministros, y el sitio queda libre.`,
  dismantleTip: `Desmontar: se recupera ${DISMANTLE_BACK === 0.5 ? 'la mitad' : `el ${Math.round(DISMANTLE_BACK * 100)} %`} de lo que costó, menos si está dañado.`,
  /** The palette's tabs, by PieceKind. */
  tabs: { building: 'Edificios', defence: 'Defensas', trap: 'Automáticas' },
  /** The card's kicker, by PieceKind (a building adds its level: buildingKicker). */
  kinds: { building: 'EDIFICIO', defence: 'DEFENSA', trap: 'AUTOMÁTICA' },
  paletteHelp: 'Elige una pieza y ponla en el claro: R la gira; Esc o el botón derecho la sueltan.',
  /** The bottom line, by what is in hand: felling, or nothing (a piece in hand: holdingLine). */
  felling: 'Arrastra sobre los árboles que quieras talar. Esc para dejarlo.',
  idle: 'Pulsa una pieza del claro para repararla, mejorarla o desmontarla.',
  noTrees: 'Aquí no hay árboles: arrastra sobre el pinar.',
  /** A dear piece with the partner connected: its tag (voteTip has the why). */
  vote: 'se propone',
  /** Dismantling's confirmation, and its two buttons. */
  dismantleAsk: '¿Desmontar?',
  dismantleYes: 'Desmontar',
  dismantleNo: 'Dejarlo',
};

/** The Bastion's level, bought now (playtest 18/10 #40: suministros and missions done, no experience). */
export const LEVEL_TEXT = {
  button: 'Subir de nivel',
  about: 'Cada nivel del Bastión abre piezas nuevas para construir y el siguiente capítulo de la historia.',
  top: 'El Bastión ya está en su nivel más alto.',
};

/** The button's line: «Subir el Bastión al nivel 3: 160 §.» (what blocks it is levelUpBlocker's, in campaign.ts). */
export function levelUpLine(level: number, cost: number): string {
  return `Subir el Bastión al nivel ${level}: ${cost} §.`;
}

/** The mark at the top, where «DÍA n» was (§11.6): «7 SALIDAS», «1 SALIDA», «AÚN SIN SALIR». */
export function outingsMark(n: number): string {
  if (n <= 0) return 'AÚN SIN SALIR';
  return n === 1 ? '1 SALIDA' : `${n} SALIDAS`;
}

/** Its tooltip: time is missions now, and building is free of it (`level`: the Bastion's, for the doom's name). */
export function timeHelp(level: number): string {
  return `El tiempo pasa con cada misión: mientras la escuadra está fuera, en el Bastión se curan las heridas, se come y ${doomSubject(level)} avanza. Construir, comprar o reclutar no gasta tiempo.`;
}

/** What locks a piece, or an upgrade: «Hace falta el nivel 2 del Bastión.»; the palette's tag is «Nivel 2». */
export function needsLevelLine(level: number): string {
  return `Hace falta el nivel ${level} del Bastión.`;
}

/** Why a dear piece goes to a vote (`over`: the price above which it does): «Cuesta más de 30 §: se propone, y se paga cuando los dos estáis de acuerdo.» */
export function voteTip(over: number): string {
  return `Cuesta más de ${over} §: se propone, y se paga cuando los dos estáis de acuerdo.`;
}

/** The bottom line with a piece in hand: «Empalizada: clic para poner, R para girar, Esc para soltar.» */
export function holdingLine(piece: PieceId): string {
  return `${PIECE_TEXT[piece].name}: clic para poner, R para girar, Esc para soltar.`;
}

/** By the pointer, where it fits: «Empalizada · Cuesta 6 §», « · se propone» when dear; where it doesn't: «Sala de radio · Hay árboles: tala antes.» */
export function placeHint(piece: PieceId, cost: { supplies: number; parts?: number }, vote = false, blocked?: string): string {
  const name = PIECE_TEXT[piece].name;
  if (blocked) return `${name} · ${blocked}`;
  return `${name} · ${buildCostLine(cost)}${vote ? ` · ${BUILD_TEXT.vote}` : ''}`;
}

/** By the pointer, felling: «Talar un árbol · +2 §», «Talar 12 árboles · +24 §». */
export function fellHint(trees: number): string {
  return `Talar ${trees === 1 ? 'un árbol' : `${trees} árboles`} · ${trees * FELL_COST} §`;
}

/** A piece under the pointer: «Barricada de sacos · 5/15», «Foso con pinchos · por reponer», «Taller · nivel 2». */
export function pieceHint(piece: PieceId, what: { hp?: [number, number]; state?: keyof typeof PIECE_STATE; level?: number }): string {
  const name = PIECE_TEXT[piece].name;
  if (what.state) return `${name} · ${PIECE_STATE[what.state]}`;
  if (what.level) return `${name} · nivel ${what.level}`;
  return what.hp ? `${name} · ${what.hp[0]}/${what.hp[1]}` : name;
}

/** The card's kicker for a building: «EDIFICIO · NIVEL 2». */
export function buildingKicker(level: number): string {
  return `${BUILD_TEXT.kinds.building} · NIVEL ${level}`;
}

/** The card's figures: «Aguante 12/30», «Usos 0/3», «Junto a: Enfermería, Huerto». */
export function holdsLine(hp: number, full: number): string {
  return `Aguante ${hp}/${full}`;
}
export function usesLine(left: number, full: number): string {
  return `Usos ${left}/${full}`;
}
export function besideLine(names: readonly string[]): string {
  return `Junto a: ${names.join(', ')}`;
}

/** The card's buttons with their price: «Reparar · 4 §», «Mejorar · 60 §», «Reponer · 1 pieza», «Desmontar · +12 §». */
export function actionPrice(action: 'repair' | 'upgrade' | 'rearm' | 'dismantle', cost: { supplies?: number; parts?: number }): string {
  const what = [cost.supplies ? `${action === 'dismantle' ? '+' : ''}${cost.supplies} §` : '', cost.parts ? partsCount(cost.parts) : ''].filter(Boolean).join(' y ');
  return what ? `${BUILD_TEXT[action]} · ${what}` : BUILD_TEXT[action];
}

/** Under «Mejorar»: what the next level brings, as the world sees it: «Nivel 2: Un torno de verdad.» */
export function upgradeLine(piece: PieceId, to: number): string {
  const line = PIECE_TEXT[piece].levels?.[to - 1];
  return line ? `Nivel ${to}: ${line}` : `Nivel ${to}`;
}

/** Dismantling's confirmation line: «Se recuperan 12 §.», «Se recupera 1 §.», «No se recupera nada.» */
export function dismantleBack(supplies: number): string {
  if (supplies <= 0) return 'No se recupera nada.';
  return supplies === 1 ? 'Se recupera 1 §.' : `Se recuperan ${supplies} §.`;
}

/** «Cuesta 15 § y 1 pieza», «Cuesta 6 §». */
export function buildCostLine(cost: { supplies: number; parts?: number }): string {
  return cost.parts ? `Cuesta ${cost.supplies} § y ${partsCount(cost.parts)}` : `Cuesta ${cost.supplies} §`;
}

/** What is short: «Faltan 5 §», «Falta 1 pieza», «Faltan 5 § y 2 piezas». */
export function buildShortLine(supplies: number, parts = 0): string {
  const what = [supplies > 0 ? `${supplies} §` : '', parts > 0 ? partsCount(parts) : ''].filter(Boolean);
  const one = what.length === 1 && (supplies === 1 || (!supplies && parts === 1));
  return `${one ? 'Falta' : 'Faltan'} ${what.join(' y ')}`;
}

/** A dear piece put to the partner (§11.7): «Propuesta de Arturo: Muro, 15 § y 1 pieza». */
export function buildProposalLine(who: string, piece: PieceId, cost: { supplies: number; parts?: number }): string {
  return `Propuesta de ${who}: ${PIECE_TEXT[piece].name}, ${buildCostLine(cost).replace(/^Cuesta /, '')}`;
}

/**
 * What the rations are for (playtest 18/10 #37), for the top mark's tooltip:
 * five days a mission, a ration a day for every four soldiers (`perMission`:
 * dailyRations × DAYS_PER_MISSION).
 */
export function rationsHelp(perMission: number): string {
  return `Cada día se gasta una ración por cada cuatro soldados, y una misión son cinco días: ahora, ${perMission} raciones por misión. Sin raciones hay hambre: −10 de voluntad en las misiones y las heridas curan a la mitad de velocidad. Se consiguen en las misiones, en el Huerto y de los enclaves.`;
}

/** The agenda's line when there is food for less than two missions (`missions`: how many it lasts, 0 or 1). */
export function rationsLowLine(missions: number): string {
  return missions >= 1 ? 'Hay raciones para una misión más: traed comida o levantad un Huerto.' : 'No llegan las raciones para la próxima misión: traed comida o levantad un Huerto.';
}

/** When the next siege comes, in missions (§11.6): «tras la próxima misión», «dentro de 3 misiones». */
export function inMissions(n: number): string {
  return n <= 1 ? 'tras la próxima misión' : `dentro de ${n} misiones`;
}

/** How many, as the radio hears it: never the exact count. */
function roughly(n: number): string {
  if (n < 8) return 'unos pocos';
  if (n >= 50) return 'más de cincuenta';
  return `unos ${['diez', 'veinte', 'treinta', 'cuarenta'][Math.min(3, Math.round(n / 10) - 1)]}`;
}

/** Map edges as the radio says them: «por el norte y el este»; all four, «por todos lados». */
function bySides(sides: readonly number[]): string {
  const names = [...new Set(sides)].sort().map((i) => `el ${SIDES[i]}`);
  if (names.length >= 4) return 'por todos lados';
  return `por ${names.length > 1 ? `${names.slice(0, -1).join(', ')} y ${names[names.length - 1]}` : names[0]}`;
}

/**
 * The siege's warning (§11.5): without a Sala de radio, only when; with one,
 * how many and by where. «Viene un asedio: llegará dentro de 3 misiones.»,
 * «Por la radio: unos veinte, por el norte, dentro de 3 misiones.»
 */
export function siegeWarning(missions: number, heard?: { count: number; sides: readonly number[] }): string {
  if (!heard) return `Viene un asedio: llegará ${inMissions(missions)}.`;
  return `Por la radio: ${roughly(heard.count)}, ${bySides(heard.sides)}, ${inMissions(missions)}.`;
}

/** The same, short for the strip: «ASEDIO EN 3 MISIONES», «ASEDIO TRAS LA PRÓXIMA». */
export function siegeWarningShort(missions: number): string {
  return missions <= 1 ? 'ASEDIO TRAS LA PRÓXIMA' : `ASEDIO EN ${missions} MISIONES`;
}

/** When it comes, and when the base falls: with someone left (to another clearing) or with nobody (the campaign is over). */
/**
 * La Lata as it grows (Arturo, 18/10 #45; Lore's story: the bus stayed at
 * Atalaya, and they fled in Teo's 4×4): the vehicle by `c.vehicle`. Lore
 * writes the lines; these are the names.
 */
export const VEHICLES = [
  { name: 'Todoterreno', the: 'el todoterreno', a: 'un todoterreno', female: false, line: 'El de Teo, el que tiró de la caravana. Cuatro plazas y el maletero.' },
  { name: 'Furgoneta', the: 'la furgoneta', a: 'una furgoneta', female: true, line: 'De reparto, con las ventanillas tapadas con chapa.' },
  { name: 'Camión', the: 'el camión', a: 'un camión', female: false, line: 'De mudanzas: la caja atrás, con bancos y una trampilla.' },
  { name: 'Autobús', the: 'el autobús', a: 'un autobús', female: false, line: 'El de línea de Atalaya, con la chapa que Teo le soldó. La Lata de verdad.' },
] as const;

/** A zone's name after «en»: «en el Pantano Gris», «en La Vega». */
export function inZone(name: string): string {
  return name.startsWith('El ') ? `en el ${name.slice(3)}` : `en ${name}`;
}

/** The operation's name: «La furgoneta del Pantano Gris», «El camión de La Vega»; the bus's is always «El autobús de Atalaya». */
export function vehicleMissionName(tier: number, zone: string): string {
  if (tier >= 3) return 'El autobús de Atalaya';
  const the = (VEHICLES[tier] ?? VEHICLES[1]).the;
  return `${the[0]!.toUpperCase()}${the.slice(1)} ${ofZone(zone)}`;
}

/** An operation to bring back a better vehicle (#45), a Limpieza: who saw it, where, and that it is ours once the zone is clear. */
export function vehicleBriefing(tier: number, zone: string): string {
  if (tier >= 3) return 'El autobús sigue en Atalaya, en la cochera, donde lo dejamos al huir. Desde la cuesta se ve entero. Despejad la zona y La Lata vuelve a ser La Lata.';
  if (tier === 2) return `Teo ha visto un camión de mudanzas ${inZone(zone)}, con medio depósito. Arranca, dice, si alguien empuja. Despejad la zona: el camión es nuestro.`;
  return `Teo ha visto una furgoneta de reparto entera ${inZone(zone)}. Las ruedas aguantan. Despejad la zona: la furgoneta es nuestra.`;
}

const SEATS = ['cero', 'una', 'dos', 'tres', 'cuatro', 'cinco', 'seis', 'siete', 'ocho', 'nueve', 'diez'];

/** What La Lata is now, said when it is won (`seats`: soldiers it takes): «La Lata ya es una furgoneta: caben seis.» */
export function vehicleWonLine(tier: number, seats: number): string {
  const n = SEATS[seats] ?? String(seats);
  return tier >= 3 ? `La Lata vuelve a ser el autobús: caben ${n}.` : `La Lata ya es ${(VEHICLES[tier] ?? VEHICLES[1]).a}: caben ${n}.`;
}

/**
 * What a fallen soldier carried (#49): it stays on the ground, marked with
 * whose it was, and anyone can search it. Never «caja»: those are the loot's.
 */
export function fallenPackLine(who: string): string {
  return `${who} cae. Su mochila se queda en el suelo, con sus armas.`;
}
export function fallenPackMark(who: string): string {
  return `MOCHILA DE ${who.split(' ')[0]!.toUpperCase()}`;
}
export function fallenPackAction(who: string): string {
  return `Registrar la mochila de ${who.split(' ')[0]}`;
}
/** «Mateo recoge lo que llevaba Chiara: un fusil de asalto y dos granadas.» */
export function fallenPackTakenLine(taker: string, fallen: string, what: string): string {
  return `${taker} recoge lo que llevaba ${fallen.split(' ')[0]}: ${what}.`;
}

/** Missions with the living (#47): the dead go for anyone alive, them too. */
export const LIVING_TIP = 'Los muertos van a por cualquier vivo, también a por ellos, y el tiroteo los atrae.';

export const BASE_SIEGE_TEXT = {
  here: 'Ya están aquí.',
  /** A siege by night (one in three from the second): the squad starts in sight, so the dark only hides the dead from it. */
  night: 'Asedio de noche: tus soldados solo ven de cerca, salvo junto a un foco. Los zombis ven como de día.',
  defenders: 'Defienden los sanos. Los heridos y los mordidos se quedan dentro.',
  fallen: 'El Bastión ha caído',
  fallenLine: 'Otra vez. Se pierde lo levantado y lo del almacén. Los que quedan huyen con la caravana y lo puesto, y vuelven a empezar en otro claro.',
  nobody: 'No queda nadie',
  nobodyLine: 'El Bastión ha caído con todos dentro. La campaña termina aquí.',
};

// ------------------------------------------------------------------- siege

/** The map edges, by DIRS4 index (Reinforcement.side: 0 north, 1 east, 2 south, 3 west). */
export const SIDES = ['norte', 'este', 'sur', 'oeste'] as const;

/**
 * The siege (docs/FASE12.md §10; playtest 18/10 #17, #20): the dead come at
 * the base in waves and go for the living, from no side anyone can tell; the
 * base falls with the last of its defenders. Headlines without a full stop,
 * like the other missions' outcomes.
 */
export const SIEGE_TEXT = {
  /** In place of MISSION_NAMES: the card and the strip. */
  name: 'Asedio',
  /** The skirmish menu's line under «El Bastión», like the other maps'. */
  menu: 'La muralla, cuatro torres y cinco oleadas',
  briefing: 'Vienen en oleadas, cada una más grande, y van a por los vivos. Tiran puertas y barricadas para llegar a vosotros, y no se sabe por dónde entrarán.',
  /** Once the last wave is in. */
  noMoreWaves: 'No vienen más',
  /** The outcomes: held to the last turn, every wave down, nobody left. */
  held: 'El Bastión aguanta: los que quedan se retiran',
  cleared: 'El Bastión aguanta: no queda ninguno en pie',
  wiped: 'El Bastión ha caído: no queda nadie para defenderlo',
};

/** The strip's objective (`hold`: SIEGE.hold, the last turn): «Aguantad hasta el turno 11». */
export function siegeObjective(hold: number): string {
  return `Aguantad hasta el turno ${hold}`;
}

/** The objective in full, for the card and the strip's tooltip. */
export function siegeObjectiveTip(hold: number): string {
  return `Aguantad hasta el final del turno ${hold} o acabad con todas las oleadas. Si caen todos, el Bastión cae.`;
}

/** Which turn a wave lands at the end of (`turn`: Reinforcement.turn), seen on squad turn `now`: «este turno», «próximo turno», «turno 5». */
function waveTurn(turn: number, now: number): string {
  if (turn <= now) return 'este turno';
  return turn === now + 1 ? 'próximo turno' : `turno ${turn}`;
}

/** When the next wave lands, in full: «Próxima oleada: al final del turno 5.» */
export function waveDueLine(turn: number, now: number): string {
  const when = waveTurn(turn, now);
  return `Próxima oleada: al final ${when === 'este turno' ? 'de' : 'del'} ${when}.`;
}

/** The same, short for the strip: «Turno 5», «Próximo turno», «Este turno». */
export function waveDueShort(turn: number, now: number): string {
  const when = waveTurn(turn, now);
  return `${when[0]!.toUpperCase()}${when.slice(1)}`;
}

/** The log line as a wave comes in, from wherever it does: «Entra la oleada 2 de 5.»; the last one says so. */
export function waveEntersLine(n: number, total: number): string {
  return n >= total ? 'Entra la última oleada.' : `Entra la oleada ${n} de ${total}.`;
}

/** A door or a barricade under the cursor: «Puerta: aguanta 12 de 18». */
export function breakableTip(kind: 'door' | 'barricade', left: number, full: number): string {
  return `${kind === 'door' ? 'Puerta' : 'Barricada'}: aguanta ${left} de ${full}`;
}

/** The floating word when one gives way in sight. */
export const GIVE_WAY_FLOAT = { door: 'CEDE LA PUERTA', barricade: 'CEDE LA BARRICADA' };

/**
 * The log's line when one gives way, seen or not (`side`: a SIDES index):
 * «Han tirado una puerta, al norte.», «Han abierto un hueco en la barricada
 * del este.». A gate's leaves fall one by one, and a barricade's three tiles
 * too: each says so.
 */
export function giveWayLine(kind: 'door' | 'barricade', side?: number): string {
  const where = side === undefined ? undefined : SIDES[side];
  if (kind === 'door') return where ? `Han tirado una puerta, al ${where}.` : 'Han tirado una puerta.';
  return where ? `Han abierto un hueco en la barricada del ${where}.` : 'Han abierto un hueco en una barricada.';
}

/** The mark on the screen's edge and on the map where they broke in, like «Oleada · sur»: «Brecha · este». */
export function breachMark(side: number): string {
  return `Brecha · ${SIDES[side]}`;
}

// ------------------------------------------------------------------ people

/** The sex of every named person who can be on the battlefield or join the squad, so their model matches their name. */
export const NAMED_SEX: Record<string, 'f' | 'm'> = {
  'Dra. Amara Nwosu': 'f',
  'Simão Ferreira': 'm',
  'Noor Haddad': 'f',
  'Celia Marsh': 'f',
  'Vera Lindqvist': 'f',
  'Lola Haskins': 'f',
  'Mei Takahara': 'f',
  'Otto Brandt': 'm',
  'Tomasz Rybak': 'm',
  'Dra. Irina Sokolova': 'f',
  'Ing. Kenji Mori': 'm',
  'Alcaldesa Mara Velasco': 'f',
  'Dr. Femi Adebayo': 'm',
  'Agente Claire Dubois': 'f',
};

// ----------------------------------------------------------------- premise

/** The card before the first mission: who the players are and why they fight. One paragraph per line. */
export const PREMISE = {
  title: 'El claro',
  lines: [
    'La infestación parece haber aflojado este invierno. Las nevadas han dejado a muchos caminantes sepultados y, aunque salir sigue siendo peligroso, quizá sea el momento de volver a intentarlo.',
    'Atalaya, la vieja estación militar, cayó en otoño. Los que salimos con vida hemos parado en un claro del pinar, sobre Brena, con una caravana y lo puesto. Necesitamos comida, medicinas y suministros si queremos llegar todos vivos al final de la temporada.',
    'Toca volver a sacar La Lata, ese trasto forrado con chapas que nos sacó de Atalaya, y salir a buscar lo que nos hace falta.',
  ],
};

/** Where an ordinary operation goes, by kind of map: it gives the mission its name («La gasolinera de Velsa»). */
const MISSION_PLACES: Record<Biome, string[]> = {
  city: ['El mercado', 'La comisaría', 'El colegio', 'El hospital', 'La estación de autobuses', 'El centro comercial', 'Los juzgados', 'La biblioteca'],
  wilds: ['La granja', 'El camping', 'El pinar', 'La ermita', 'La casa de campo'],
  facility: ['La conservera', 'La nave de transportes', 'La planta química', 'El almacén frigorífico', 'La fábrica de piensos'],
};

/** What a sabotage's objective is called on the field, by its alarm (playtest 2026-10-15, #14; Lore's to change). */
export const ALARM_NAMES: Record<Alarm, string> = { police: 'Coche patrulla', ambulance: 'Ambulancia', factory: 'Alarma de la nave' };

/** The scenarios' places (playtest 2026-10-14, #25): a forest's service area, a town's industrial estate. */
const SITE_PLACES: Record<Site, string[]> = {
  roadside: ['El área de servicio', 'El motel', 'El aserradero', 'La gasolinera'],
  industrial: ['El polígono', 'Los muelles de carga', 'Las naves', 'El depósito de contenedores'],
};

/** An ordinary operation's name: a place in the zone, «El colegio de Corran», «La granja del Pantano Gris». */
export function missionName(biome: Biome, zone: string, seed: number, site?: Site): string {
  const places = site ? SITE_PLACES[site] : MISSION_PLACES[biome];
  return `${places[Math.abs(seed) % places.length]!} ${ofZone(zone)}`;
}

// ------------------------------------------------------------------ valley

/**
 * The places one knows the valley by, named small on its map under the zones'
 * names (Arte places them; docs/LORE.md «El mundo»). Each says something of its
 * zone: the Peaje is where the Cuervos charge Velsa its toll, the Emisora is
 * the Voz's, the Pico Gris holds the repeater of chapter 4.
 */
export const LANDMARKS = {
  /** The home: a clearing in the pines above Brena (the key is the old one, kept for the map). */
  atalaya: 'El Bastión',
  /** Brena: the old station, burnt in the autumn (Arturo, 18/10: «Atalaya cayó»); just its name, the drawing says the rest. */
  ruins: 'Atalaya',
  /** Brena: the village of the bells (quest «Las campanas de Telm»). */
  telm: 'Telm',
  /** Carvik: the Sindicato's mine. */
  mine: 'Mina',
  /** El Pantano Gris: the reservoir's dam, its dead power plant and the peak with the repeater. */
  dam: 'Presa',
  plant: 'Central',
  peak: 'Pico Gris',
  /** La Vega: the Bodegas' cellars. */
  wineries: 'Bodegas',
  /** Velsa: the Cuervos' toll on the road. */
  toll: 'Peaje',
  /** Corran: chapter 2. */
  hospital: 'Hospital',
  /** Galen: where it began, and its port. */
  institute: 'Instituto Galen',
  port: 'Puerto',
  /** Los Llanos de Ceniza: the Voz's station, and the Camioneros' stop. */
  station: 'Emisora',
  truckStop: 'Parada de camiones',
} as const;

/** A zone's name after «a»: «al Pantano Gris», «a La Vega». */
export function toZone(name: string): string {
  return name.startsWith('El ') ? `al ${name.slice(3)}` : `a ${name}`;
}

/** A zone's name after «de»: «del Pantano Gris», «de La Vega». */
export function ofZone(name: string): string {
  return name.startsWith('El ') ? `del ${name.slice(3)}` : `de ${name}`;
}

function joinWith(names: string[], last: string): string {
  return names.length > 1 ? `${names.slice(0, -1).join(', ')} ${last} ${names[names.length - 1]}` : (names[0] ?? '');
}

/**
 * The alert when the Marea reaches a zone or leaves it (docs/FASE5.md). Before
 * the Bastion knows the Marea's name (PARTITURA_LEVEL) it is just «los muertos».
 * Written without verbs that agree with the zone, which may be plural.
 */
export function mareaAlert(level: number, zone: string, reached: boolean): { kicker: string; title: string; text: string } {
  const kicker = doomName(level);
  if (level >= PARTITURA_LEVEL) {
    return reached
      ? { kicker, title: `La Marea llega ${toZone(zone)}`, text: `La Marea llega ${toZone(zone)}. Su enclave calla, y por sus caminos solo andan muertos: de allí ya no llegará nada.` }
      : { kicker, title: `La Marea se retira ${ofZone(zone)}`, text: `La Marea se retira ${ofZone(zone)}. Vuelve a haber quien conteste por radio.` };
  }
  return reached
    ? { kicker, title: `Los muertos toman ${zone}`, text: `Los muertos han tomado ${zone}. Su enclave calla: de allí ya no llegará nada.` }
    : { kicker, title: `Los muertos se retiran ${ofZone(zone)}`, text: `Los muertos se retiran ${ofZone(zone)}. Vuelve a haber quien conteste por radio.` };
}

/** Why a zone under the Marea can't be contacted. */
export function floodedContactReason(level: number, zone: string): string {
  return level >= PARTITURA_LEVEL ? `La Marea cubre ${zone}: nadie contesta hasta que se retire.` : `Los muertos han tomado ${zone}: nadie contesta hasta que se retiren.`;
}

/** Why a zone with no road from ours can't be contacted; `via` are the zones to contact first. */
export function noRoadReason(via: string[]): string {
  return `No hay carretera desde vuestras zonas: contactad antes con ${via.length ? joinWith(via, 'o') : 'otra zona'}.`;
}

/** The to-do line listing the zones under the Marea. */
export function floodedListLabel(level: number, zones: string[]): string {
  return `${level >= PARTITURA_LEVEL ? 'Bajo la Marea' : 'En manos de los muertos'}: ${joinWith(zones, 'y')}`;
}

/** A zone's card when it is under the Marea. */
export function floodedZoneNote(level: number): string {
  return level >= PARTITURA_LEVEL
    ? 'Bajo la Marea: de aquí no llega nada, y sus misiones traen un grupo más de muertos. Saldrá si la Marea retrocede: quemad Nidos.'
    : 'En manos de los muertos: de aquí no llega nada, y sus misiones traen un grupo más. Saldrá si baja la infestación: quemad Nidos.';
}

// ------------------------------------------------------------------- night

/** Night missions (docs/ALTURA-Y-NOCHE.md): the tag, the briefing's last line and the debrief's. */
export const NIGHT_TEXT = {
  label: 'De noche',
  briefing: 'De noche: tus soldados solo ven de cerca, salvo bajo una farola, una bengala o un fuego. Hay más zombis, y el botín vale la mitad más.',
  report: 'Misión de noche: el botín y la recompensa valen la mitad más.',
  /**
   * How the light works (los.ts sightTo, detectionTo), a line each, for the
   * help and the first night: the living see less in the dark; the dead see
   * as ever, but notice the hidden squad from closer unless it stands in light.
   */
  rules: [
    'De noche, tus soldados solo ven de cerca.',
    'Bajo una farola, una bengala o un fuego se ve como de día.',
    'Hasta que te descubren, los enemigos solo te notan de cerca si estás a oscuras. Después, los zombis ven como de día.',
    `Las farolas no se apagan. La Bengala ilumina al enemigo que alcanza y lo que tiene alrededor durante ${NIGHT.flare.turns} turnos. El Cóctel molotov y El bidón dejan fuego durante ${NIGHT.fire.turns}.`,
  ],
};

/** A lit tile under the cursor, at night: «A la luz: quien esté aquí, vivo o muerto, se ve desde tan lejos como de día. Se apaga en 2 turnos.» (`turns`: Light.turns; none for a street lamp). */
export function litNote(turns?: number): string {
  const lit = 'Iluminado: quien esté aquí se ve desde lejos, como de día.';
  return turns ? `${lit} Se apaga en ${turns} ${turns === 1 ? 'turno' : 'turnos'}.` : lit;
}

// ---------------------------------------------------------------- briefings

/** An ordinary sabotage's report, by what sounds (MissionOffer.alarm). */
const SABOTAGE_BRIEFINGS: Record<Alarm, string> = {
  police: 'Los Cuervos dejaron allí un coche patrulla con la sirena puesta y se fueron. Mientras suene, los muertos de toda la comarca vendrán hacia aquí. Apagadla antes de que termine la cuenta atrás.',
  ambulance: 'Allí hay una ambulancia con la sirena atascada, y la batería aguanta. Mientras suene, los muertos de toda la comarca vendrán hacia aquí. Apagadla antes de que termine la cuenta atrás.',
  factory: 'La alarma de una nave salta allí cada noche, sola, con la batería. Mientras suene, los muertos de toda la comarca vendrán hacia aquí. Apagadla antes de que termine la cuenta atrás.',
};

/** Several reports per mission type; each offer always gets the same one. */
const BRIEFINGS: Record<MissionKind, string[]> = {
  elimination: [
    'Hace semanas que nadie puede acercarse: está lleno de muertos, y es paso obligado hacia la sierra. Que no quede ninguno.',
    'Una manada de zombis ha cortado la única carretera con agua potable. Limpiad la zona.',
    'Los muertos se han quedado allí, y por ahí pasa la ruta de la sierra. Abatidlos a todos.',
  ],
  recovery: [
    `Allí quedó el mapa de los pozos de la comarca. Sin él, el agua del verano es una apuesta. Recuperadlo y volved ${BUS.to}.`,
    'Uno de sus mensajeros murió allí, a la intemperie. La saca del correo sigue junto al cuerpo. Recuperadla antes de que se acabe el tiempo.',
    'Los Cuervos escondieron allí su libro de cuentas y no volvieron a por él: los muertos llegaron antes. Dice quién les paga y por dónde pasan. Traedlo.',
  ],
  sabotage: [
    'Algo suena allí cada noche, y los muertos de toda la comarca acuden. Que deje de sonar antes de que termine la cuenta atrás.',
    'Cada noche suena una alarma junto a nuestras rutas. Apagadla a tiempo.',
  ],
  hack: [
    'Hay una radio de emergencia en la zona, muerta desde el invierno. Arregladla y oiremos a quien queda. Si falla, el ruido atraerá a todo lo que ande cerca.',
    'Los Cuervos montaron allí un repetidor y hablan por él. Ponedlo a nuestro favor y sabremos qué traman. Si falla, saltará la alarma.',
    'Allí queda un equipo de radio entero. Ponedlo en marcha. Si falla, el ruido traerá compañía.',
  ],
  rescue: [
    'Los Cuervos se han llevado {a}. Si llegan a su campamento, no habrá otra ocasión. Que salga con vida.',
    'Los Cuervos retienen {a} y piden gasolina a cambio. No vamos a pagar. Que vuelva con vida.',
    'Los Cuervos han parado allí con sus prisioneros. Entre ellos va {who}, que sabe cosas que nos hacen falta. Que salga con vida.',
  ],
  scavenge: SCAVENGE_BRIEFINGS,
};

function hash(text: string): number {
  let h = 0;
  for (let i = 0; i < text.length; i++) h = (h * 31 + text.charCodeAt(i)) >>> 0;
  return h;
}

/** What picks an ordinary operation's report: the same offer always gets the same one. */
function briefingSeed(o: MissionOffer): number {
  return hash(o.id) + (o.map.kind === 'generated' ? o.map.seed : 0);
}

/**
 * What each ordinary recovery carries out, in the order of its reports
 * (BRIEFINGS.recovery), and whether its report puts it out in the open
 * rather than in a building (docs/PRUEBA-2026-10-14.md #27): the pharmacy's
 * medicines and the Cuervos' warehouse are indoors, the courier's backpack
 * lies in the street.
 */
/** Never anything a search turns up (playtest 18/10 #13): the objective must not look like the loot. */
const RECOVERY_ITEMS: { item: string; outdoors?: true }[] = [{ item: 'el mapa de los pozos' }, { item: 'la saca del correo', outdoors: true }, { item: 'el libro de cuentas' }];

function ordinaryRecovery(o: MissionOffer): { item: string; outdoors?: true } | undefined {
  if (o.kind !== 'recovery' || o.story || o.quest || o.final || o.facility) return undefined;
  return RECOVERY_ITEMS[briefingSeed(o) % RECOVERY_ITEMS.length];
}

/** The objective an ordinary recovery names, as its report does; undefined for any other operation. */
export function recoveryItem(o: MissionOffer): string | undefined {
  return ordinaryRecovery(o)?.item;
}

/** Whether an ordinary recovery's objective lies out in the open (its report says so); false indoors or for any other operation. */
export function recoveryOutdoors(o: MissionOffer): boolean {
  return !!ordinaryRecovery(o)?.outdoors;
}

/** Who signs an operation's report, in pen under it. */
export function briefingSignature(o: MissionOffer): string {
  if (o.story === 1) return '';
  return o.story ? STORY[o.story - 1]!.signature : '— N. A.';
}

/** Morse's report for an operation, signed by the region's ally. */
export function missionBriefing(o: MissionOffer, level: number, simon: SimonChoice = null): string {
  const text = briefingText(o, level, simon);
  return o.night ? `${text} ${NIGHT_TEXT.briefing}` : text;
}

function briefingText(o: MissionOffer, level: number, simon: SimonChoice): string {
  if (o.story) return storyBriefing(o.story, simon);
  if (o.final) {
    return 'La Marea está en la sierra y la Congregación la empuja desde su campamento. La Lata se la llevará con la sirena; vosotros, al campamento. Abatid a todo lo que haya. No habrá otra oportunidad.';
  }
  if (o.facility) {
    const effect = level >= PARTITURA_LEVEL ? 'acerca la Marea un kilómetro cada catorce días' : 'hace crecer la infestación cada catorce días';
    return `Este Nido ${effect}. Lo mantiene vivo la alarma de una nave, que no deja de sonar: apagadla antes de que termine la cuenta atrás.`;
  }
  const options = BRIEFINGS[o.kind];
  const seed = briefingSeed(o);
  const ally = REGION_LORE[o.region].ally;
  // What sounds, and who is held, once the offer says (docs/PRUEBA-2026-10-15.md #14, #20).
  const who = o.vip ? titled(o.vip) : 'alguien';
  const text = (o.kind === 'sabotage' && o.alarm ? SABOTAGE_BRIEFINGS[o.alarm] : options[seed % options.length]!)
    .replace('{a}', who.startsWith('el ') ? `al ${who.slice(3)}` : `a ${who}`)
    .replace('{who}', who);
  return `Aviso ${ally.startsWith('el ') ? `del ${ally.slice(3)}` : `de ${ally}`}. ${text}`;
}

// ------------------------------------------------------------ Expediente

export interface ExpedientePage {
  title: string;
  /** The page in one line, for the level-up pop-up (the full page opens on request). */
  summary: string;
  paragraphs: string[];
  /** Handwritten in the margin, signed. */
  note: string;
}

/** One page per Bastion level: what the Bastion has found out by then. */
export const EXPEDIENTE: ExpedientePage[] = [
  {
    title: 'Atalaya',
    summary: 'La Pandemia lo vació todo. Atalaya cayó en otoño; los que quedan empiezan de cero en un claro del pinar.',
    paragraphs: [
      'Hace dos inviernos llegó la Pandemia. Quien se contagia muere en un día y se levanta. Cayeron las ciudades, se fue el ejército y la radio se quedó muda.',
      'Atalaya era una estación militar: muros de hormigón, un refugio bajo tierra y una sola carretera. Aguantó dos años. En otoño llegó una horda y fue derecha a ella, como si alguien le hubiera enseñado el camino. Salimos en el todoterreno de Teo, con la caravana enganchada detrás. El autobús se quedó en la cochera de Atalaya, y el nombre de La Lata ha pasado al todoterreno.',
      'Hemos parado en un claro del pinar, sobre Brena. Lo que se levante aquí será el Bastión. Lo dirigen dos personas, y lo importante lo deciden juntas. El año pasado, en otro refugio, una líder sola lo vendió a los Cuervos a cambio de su vida.',
    ],
    note: 'Lo que entra por la puerta se apunta aquí. — N. A.',
  },
  {
    title: 'Notas de la doctora',
    summary: 'De noche ven igual que de día. Una puerta no los para. No todos los mordiscos contagian.',
    paragraphs: [
      'Notas de la Dra. Nwosu. La oscuridad no les quita nada: de noche ven igual que de día. Los que perdemos vista somos nosotros.',
      'Una puerta cerrada no los para: la empujan y pasan. Solo les tapa la vista. Para frenarlos sirve un paso estrecho, no una puerta.',
      'No todos los mordiscos contagian. El de un muerto corriente, uno de cada cuatro; el de un Acorazado, uno de cada tres. Los gases de un Hinchado, uno de cada dos.',
    ],
    note: 'Si veis una cara conocida entre ellos, no es quien creéis. — T. V.',
  },
  {
    title: 'Los Cuervos',
    summary: 'Los Cuervos atraen hordas con sirenas contra quien no les paga. Sin sirena, la zona respira.',
    paragraphs: [
      'Los Cuervos no son una banda: son muchas, con un mismo negocio. Cobran peaje en comida y en gente.',
      'Su arma más sucia es la sirena de un coche patrulla. Lo llevan cerca de quien no paga, la encienden y dejan que la horda haga el resto.',
      'Cuando se calló la sirena de los Llanos, los zombis se dispersaron en un día. Sin ruido, se pierden.',
    ],
    note: 'Las sirenas primero. Siempre. — N. A.',
  },
  {
    title: 'La Marea',
    summary: 'Una horda inmensa sube desde los Llanos con el deshielo. Desde hoy, la contamos en kilómetros.',
    paragraphs: [
      'La radio vuelve a funcionar, y lo primero que hemos oído son los demás enclaves contando lo mismo: algo sube desde los Llanos de Ceniza.',
      'No es una horda: son todas a la vez. Miles de zombis caminando juntos hacia la sierra con el deshielo. La llaman la Marea.',
      'Los Nidos la alimentan. Cada Nido que quememos la frena. Desde hoy, el reloj de la sala de mando cuenta kilómetros.',
    ],
    note: 'Doce kilómetros. Contadlos. — N. A.',
  },
  {
    title: 'Los inmunes',
    summary: 'Hay gente a la que la Pandemia no mata. Su sangre podría ser una vacuna.',
    paragraphs: [
      'Hay gente a la que la Pandemia no mata. Se contagian, tienen fiebre y al día siguiente siguen vivos.',
      'Los Cuervos lo saben y los cazan: un inmune vale más que un camión de comida.',
      'La Dra. Nwosu dice que con la sangre de un inmune y los cuadernos del Instituto se podría hacer una vacuna.',
    ],
    note: 'No es un experimento. Es una niña. — A. N.',
  },
  {
    title: 'Galen',
    summary: 'La Pandemia salió del Instituto Galen. Teo estaba allí aquella noche.',
    paragraphs: [
      'Los cuadernos del Instituto Galen cuentan lo que pasó: buscaban una vacuna y crearon otra cosa. La primera muestra se perdió una noche de octubre.',
      'Aquella noche había un vigilante de guardia. Firmó el parte de entrada a las once y el de salida a las seis. Entre medias, una puerta quedó abierta.',
      'Ese vigilante era Teodor Vidić.',
    ],
    note: 'Lo sé desde el primer día. No sabía cómo decíroslo. — T. V.',
  },
  {
    title: 'La Congregación',
    summary: 'La Congregación empuja la Marea con campanas y radio. Solo la Voz sabe cómo pararla.',
    paragraphs: [
      'La Marea no sube sola. La Congregación cree que los zombis están salvados, y los guía con campanas hacia los vivos.',
      'Los dirige una voz por radio: Celia Marsh, la que presentaba el programa de la tarde. Cada noche les dice hacia dónde caminar.',
      'Si alguien sabe cómo parar la Marea, es ella. Hay que sacarla de su emisora.',
    ],
    note: 'Esta vez la necesitamos viva. — N. A.',
  },
  {
    title: 'La vacuna',
    summary: 'La vacuna está lista y la Marea, en la sierra. Allí decidiréis los dos qué hacer con ella.',
    paragraphs: [
      'La Dra. Nwosu ha terminado la vacuna: la sangre de Noor, los cuadernos de Galen y muchas noches sin dormir.',
      'La Marea ya está en la sierra. Si la paramos, habrá que decidir qué hacemos con la vacuna, y eso lo decidiréis vosotros dos.',
    ],
    note: 'Volved. — N. A. y T. V.',
  },
];

// ------------------------------------------------------------------ story

/** What the commanders did when Simão knocked on the door. */
export type SimonChoice = 'trusted' | 'refused' | null;

export interface StoryChapter {
  /** Also the mission's name. */
  title: string;
  kind: MissionKind;
  biome: Biome;
  region: RegionId;
  difficulty: number;
  reward: MissionReward;
  /** What the chapter gives besides the reward, for the briefing. */
  prize?: string;
  briefing: string;
  signature: string;
  /** The living hold it (docs/ZOMBIS-PRIMERO.md): bands of the Cuervos or the Congregación among the dead. */
  living?: boolean;
  /** Recovery: what the squad carries out. */
  item?: string;
  /** Rescue: who waits at the objective. */
  vip?: string;
  /** Read in the debrief after winning it. */
  after: string;
}

/** The chapter whose mission needs Simão's visit answered first. */
export const SIMON_CHAPTER = 5;
/** From this chapter on Dr Nwosu is in the Bastion: the loot is worth more. */
export const NWOSU_CHAPTER = 2;

/** One story mission per Bastion level, in order; the final mission needs all of them. */
export const STORY: StoryChapter[] = [
  {
    title: 'El consultorio de Brena',
    kind: 'recovery',
    biome: 'city',
    region: 'weu',
    difficulty: 1,
    reward: { credits: 90 },
    briefing:
      'El consultorio de Brena, el pueblo de abajo, sigue cerrado. En su nevera quedaba insulina, y con este frío puede que siga buena. En el campamento hay quien no pasará el invierno sin ella.\n\nLa nieve ha sepultado a muchos caminantes, pero no sabemos cómo estará el pueblo. Buscad una entrada y traed la insulina.',
    signature: '',
    item: 'la insulina',
    after: 'La insulina ya está en la caravana, en una caja con nieve. Hay para todo el invierno.',
  },
  {
    title: 'La doctora',
    kind: 'rescue',
    biome: 'city',
    region: 'afr',
    difficulty: 1,
    reward: { credits: 60 },
    prize: 'la Dra. Nwosu se une al Bastión (el botín vale un 25 % más)',
    briefing:
      'Una doctora lleva una semana encerrada en el hospital de Corran, con el edificio lleno de zombis. Se llama Amara Nwosu y trabajaba en el Instituto Galen. Llegad hasta ella y sacadla con vida.',
    signature: '— N. A.',
    vip: 'Dra. Amara Nwosu',
    after: 'La Dra. Nwosu se queda en el Bastión. Sabe qué aprovechar de cada saqueo: el botín valdrá un 25 % más.',
  },
  {
    title: 'La sirena',
    living: true,
    kind: 'sabotage',
    biome: 'wilds',
    region: 'na',
    difficulty: 2,
    reward: { credits: 80, doom: 1 },
    briefing:
      'Los Cuervos tienen en los Llanos un coche patrulla con la sirena puesta, y la encienden cada noche: nos trae a media comarca. Quiero que se calle. Destruidlo antes de que termine la cuenta atrás.',
    signature: '— T. V.',
    after: 'La sirena se calló y los zombis del valle se dispersaron en un día. Teo lo apuntó todo. Esa misma semana empezó el deshielo.',
  },
  {
    title: 'La torre del Pico Gris',
    kind: 'hack',
    biome: 'wilds',
    region: 'eas',
    difficulty: 2,
    reward: { credits: 120, doom: 1 },
    briefing:
      'El repetidor del Pico Gris lleva muerto desde el invierno. Si lo arreglamos, oiremos a los demás enclaves del valle. Si falla, el ruido atraerá a todo lo que ande por el pantano.',
    signature: '— N. A.',
    after: 'El repetidor vuelve a funcionar. Lo primero que hemos oído es a los demás enclaves avisando de lo mismo: la Marea.',
  },
  {
    title: 'Los inmunes',
    living: true,
    kind: 'rescue',
    biome: 'wilds',
    region: 'eeu',
    difficulty: 3,
    reward: { credits: 80, recruit: 'assault' },
    prize: 'una niña inmune a salvo en el Bastión',
    briefing:
      'Simão entró en el campamento de los Cuervos a por la niña inmune, pero lo han descubierto. Sabe dónde la esconden. Llegad hasta él y sacadlo con vida.',
    signature: '— N. A.',
    vip: 'Simão Ferreira',
    after: 'Noor está en la enfermería. Se contagió hace un mes y sigue viva. Nwosu no ha dormido.',
  },
  {
    title: 'Los cuadernos de Galen',
    kind: 'recovery',
    biome: 'facility',
    region: 'me',
    difficulty: 3,
    reward: { credits: 150, doom: 1 },
    briefing:
      'Los cuadernos del Instituto Galen siguen en el laboratorio donde empezó todo. Sin ellos no habrá vacuna. Recuperad los cuadernos y sacadlos por la zona de evacuación.',
    signature: '— T. V.',
    item: 'los cuadernos',
    after: 'Los cuadernos ya están en el Bastión. Teo los ha leído enteros y lleva dos días sin hablar.',
  },
  {
    title: 'La Voz',
    living: true,
    kind: 'rescue',
    biome: 'city',
    region: 'na',
    difficulty: 4,
    reward: { credits: 100 },
    prize: 'cómo parar la Marea (con el nivel 8, la misión final)',
    briefing:
      'Celia Marsh, la Voz de la Congregación, emite cada noche desde una vieja emisora de los Llanos. Es la única que sabe cómo parar la Marea. Asaltad la emisora, llegad hasta ella y sacadla con vida.',
    signature: '— N. A.',
    vip: 'Celia Marsh',
    after: 'La Voz está en el Bastión. Ha pasado una hora callada. Luego ha dicho cómo se para la Marea.',
  },
];

/** Without Simão inside, «Los inmunes» is a break-in, straight for the girl. */
const SIMON_REFUSED = {
  briefing:
    'Sin nadie dentro, habrá que entrar a la fuerza en el campamento de los Cuervos. La niña inmune se llama Noor Haddad. Llegad hasta ella y sacadla con vida.',
  vip: 'Noor Haddad',
};

export function storyBriefing(chapter: number, simon: SimonChoice): string {
  return chapter === SIMON_CHAPTER && simon === 'refused' ? SIMON_REFUSED.briefing : STORY[chapter - 1]!.briefing;
}

/** Who waits at the objective of a story rescue. */
export function storyVip(chapter: number, simon: SimonChoice): string | undefined {
  return chapter === SIMON_CHAPTER && simon === 'refused' ? SIMON_REFUSED.vip : STORY[chapter - 1]!.vip;
}

// ---------------------------------------------------------------- endings

/** How the two commanders ended it, with both keys: the vaccine for everyone, or for the Red. */
export type Ending = 'break' | 'record';

export const ENDING_TITLES: Record<Ending, string> = {
  break: 'La vacuna, para todos',
  record: 'La vacuna, para la Red',
};

/** The last page of the campaign. */
export function epilogue(outcome: 'victory' | 'defeat', ending: Ending | null, doomFull: boolean): string {
  if (outcome === 'victory') {
    if (ending === 'break') {
      return 'La Marea se deshizo en la sierra. Esa noche, Morse leyó la fórmula de la vacuna por todas las frecuencias, despacio, dos veces. La oyeron los enclaves, la oyeron los Cuervos y la oyó quien quisiera oírla. Nadie sabe qué harán con ella. Por primera vez en dos inviernos, nadie se queda fuera.';
    }
    if (ending === 'record') {
      return 'La Marea se deshizo en la sierra. La vacuna salió del Bastión en La Lata, enclave por enclave, solo para quienes estuvieron. El valle sobrevive. Fuera de él, la radio sigue pidiendo ayuda. La fórmula se queda en una caja del refugio.';
    }
    return 'La Marea se ha deshecho en la sierra. Por primera vez en dos inviernos, el valle duerme sin oír pasos.';
  }
  return doomFull
    ? 'Kilómetro cero. La Marea llegó al Bastión con la primera niebla. Morse siguió emitiendo hasta el final. La última hoja del diario se quedó a medio escribir.'
    : 'No queda nadie que conteste por radio. El Bastión se queda a oscuras.';
}
