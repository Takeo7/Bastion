/**
 * The story (docs/LORE.md): mission briefings, the Diario pages, the story
 * chapters and the epilogues. Apart from the chapters' mission setup (`STORY`),
 * which the campaign reads, it is text only.
 */
import type { MissionOffer, MissionReward, RegionId } from './campaign';
import type { Room } from './loot';
import { FOUND_WEAPONS } from './content';
import { DEED_XP, SKILL_TALENTS, SKILLS, type DeedId, type Deeds, type SkillId } from './skills';
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
  Pandemia: 'Empezó hace dos inviernos en el Instituto Galen. Quien se contagia muere en un día y se levanta.',
  Zombi: 'Un muerto que anda. Va a por el vivo que ve y, si no ve a nadie, hacia el último ruido que oyó.',
  Corredor: 'Un zombi reciente, todavía ágil. Rápido, y ve de lejos.',
  Acorazado: 'Fue policía o soldado: el chaleco y el casco le dan blindaje.',
  Chillón: 'Un zombi que no muerde: grita. Su grito asusta a los que están cerca, llama a todos los zombis que lo oyen y levanta a los muertos.',
  Hinchado: 'Un zombi lento y lleno de gases. Revienta al morir o al llegar a vosotros: hace daño a quien tenga al lado y puede contagiarlo.',
  Cuervos: 'Bandas de saqueadores. Viven de robar a otros vivos y atraen hordas con sirenas.',
  Congregación: 'Una secta que cree que los zombis están salvados. Los guía con campanas y por radio.',
  Marea: 'Una horda inmensa que sube desde los Llanos de Ceniza. Si llega a Atalaya, la campaña está perdida.',
  Nido: 'Un sitio donde los zombis se juntan y la Marea crece. Quemarlo la hace retroceder.',
  Sirena: 'La de un coche patrulla: los Cuervos la encienden cerca de quien no les paga, y los muertos acuden. En el valle también suenan ambulancias atascadas y alarmas de naves.',
  Ruido: 'Cada acción suena: un disparo se oye a una docena de casillas, un registro a dos. Lo que lo oye va a mirar. El ruido no os delata: os delata que os vean.',
  Saqueo: 'Misión de registrar un pueblo: llenad las mochilas y evacuad. No hay reloj, pero llegan zombis por los bordes, y antes cuanto más ruido.',
  Registrar: 'Una acción dentro de una habitación. Sale lo que haya según el sitio: comida y medicinas en las casas, piezas en las oficinas, comida en las tiendas, de todo en los almacenes. Cada habitación se registra una vez, para toda la escuadra. Hace un poco de ruido.',
  Cajas: 'Marcadas, como mucho tres por mapa, cada una de un tipo: armas, medicinas, munición o comida. Su marca se ve cuando están a la vista, y se queda en el mapa. Se abren con una acción, al lado, y dan siempre lo suyo.',
  Mochila: 'Tres cosas por soldado, cuatro con la Chapistería. Se suelta gratis, se pasa a un compañero de al lado con una acción, y las armas y las medicinas se usan ahí mismo. El botín cuenta si vuelve: lo que llevaba un caído se pierde.',
  Armería: 'Las armas de Atalaya: las que se encuentran, las que hace el Taller y las que trae el nivel del Bastión. Cada una es de quien la lleve, sea de la clase que sea.',
  Cargadores: 'Los de repuesto los hace el Taller: cada soldado sale con uno por tanda hecha, hasta tres. Valen para todas las armas; recargar llena primero la principal y luego la pistola. Registrando aparecen más.',
  Suministros: 'Lo que se saquea y se cambia: sirve para construir, reclutar y equipar.',
  Medicinas: 'Se cuentan en ✚. El tratamiento de un mordido cuesta 2 (1 con la Enfermería a nivel 2), y una dosis le quita 3 días de cama a un herido. En la misión, las de la mochila curan 3 con una acción. Nunca hay bastantes.',
  Mordisco: 'El de un zombi puede contagiar. En la misión, el mordido pierde 1 de salud cada turno, y sin una Cuarentena en el Bastión no sube a La Lata. Si vuelve, tiene fiebre y se convierte en 3 días (5 con la Enfermería, uno más por cada mejora de la Cuarentena) si no recibe el tratamiento. Si cae contagiado, se levanta.',
  Cuarentena: 'La nave donde se aísla a los mordidos, con cerrojo por fuera. Sin ella, un mordido no sube a La Lata y se queda en la misión.',
  Comida: 'Se cuenta en raciones. Cada día el Bastión gasta una por cada cuatro soldados. Sin raciones hay hambre: −10 de voluntad en las misiones y las heridas curan despacio.',
  Huerto: 'Bancales, un invernadero y bidones para el agua de lluvia: da raciones cada día, más cuanto más nivel.',
  Piezas: 'Muelles, motores, tornillería: con ellas el Taller hace armas y armaduras mejores. Salen de almacenes, maleteros y oficinas, y del equipo de los Acorazados.',
  Noche: 'De noche se ve poco y os ven poco, salvo bajo una farola, una bengala o un fuego. Hay más muertos en la calle, pero el botín vale la mitad más.',
  Enclave: 'Un grupo de supervivientes con el que tenéis contacto por radio. Si sigue vivo, manda suministros.',
  Rumor: 'Algo que se oye por la radio en una zona. Si lo seguís unos días a la escucha, sabréis qué era.',
  Encargo: 'Misión secundaria en cadena: varias operaciones y decisiones con un desenlace que se queda.',
  Oficio: 'Lo que Teo enseña en el Taller de oficios: machete, fuego, ballesta y enfermería de campo.',
  'Taller de oficios': 'Donde Teo enseña los oficios. Abre la cuarta rama de cada clase.',
  Maña: 'La aptitud de las manos: arreglar equipos, lanzar granadas y curar.',
  Grillo: 'El dron del Especialista: uno de juguete con una batería de coche, montado por Teo. Cura, cubre, da calambres y arregla a distancia.',
  'La Lata': 'El autobús de Atalaya: uno de línea, con la chapa que Teo le soldó por encima. Lleva a los equipos y los espera en la zona de evacuación.',
  Evacuar: `Se sale ${BUS.in}, desde la zona de evacuación: lo pide uno y lo acepta el otro. Quien esté fuera cuando sale, se pierde. En la Limpieza, salir es retirarse y la misión se pierde.`,
  Recluta: 'Superviviente todavía sin clase, con cuchillo y pistola. Al llegar a Capitán se especializa en el eje del árbol de habilidades en que más haya aprendido.',
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
    what: 'Un muerto que anda. Lo que queda de alguien, sin nada dentro.',
    does: 'Lento, sin miedo y sin cubrirse. Va a por el vivo más cercano que vea; si no ve a nadie, adonde oyó el último ruido. Muerde cuerpo a cuerpo, y un mordisco de cada cuatro contagia.',
    tip: 'Uno solo no es nada. Diez, sí. Sin ruido pasan de largo; con ruido, vienen todos.',
  },
  xenoid: {
    what: 'Un Corredor: un zombi reciente, que aún tiene piernas.',
    does: 'Corre mucho más que los otros y ve de lejos. Ataca con las uñas, cuerpo a cuerpo, y también contagia, aunque algo menos que un zombi.',
    tip: 'Llega antes de lo que creéis. Vigilancia en su camino, y que nadie se quede solo.',
  },
  mec: {
    what: 'Un Acorazado: fue policía o soldado, y conserva el chaleco y el casco.',
    does: 'Blindaje 1: resta daño a cada golpe que recibe. Aguanta mucho y embiste cuerpo a cuerpo. Es el que más contagia.',
    tip: 'Los explosivos le quitan el blindaje y la munición perforante lo atraviesa. Que no llegue a vuestro lado.',
  },
  sectoid: {
    what: 'Un Chillón: un zombi que grita.',
    does: 'No muerde. Su grito desorienta o asusta a los soldados cercanos, y se oye a dieciséis casillas: todo zombi que lo oiga viene. Además, levanta a los muertos que tenga cerca.',
    tip: 'Primero el Chillón. Cada grito trae más, y cada cadáver a su alcance puede volver a andar.',
  },
  bloater: {
    what: 'Un Hinchado: un cuerpo lleno de gases, a punto de reventar.',
    does: 'Lento. Al morir o al llegar a un soldado revienta: 3 de daño a quien tenga al lado, y a uno de cada dos lo contagia.',
    tip: 'Abatidlo de lejos, y nunca junto a los vuestros.',
  },
  trooper: {
    what: 'Un Saqueador de los Cuervos. Vive de robar a otros vivos.',
    does: 'Escopeta o fusil de caza, y alguna granada. Se cubre y dispara como una persona.',
    tip: 'Rompedle la cobertura o buscadle el flanco. Y recordad que sus disparos también hacen ruido.',
  },
  officer: {
    what: 'Un Cabecilla: manda un grupo de Cuervos.',
    does: 'Mejor arma y más aguante que un Saqueador. Aguanta en vigilancia.',
    tip: 'Sin él, los suyos dudan. Merece el mejor disparo del turno.',
  },
  lancer: {
    what: 'Un Cazador de los Cuervos: no mata, captura. Los vivos valen más.',
    does: 'Red y porra cuerpo a cuerpo: el soldado inmovilizado empieza su siguiente turno con una sola acción.',
    tip: 'Corre mucho y no necesita línea de tiro. Vigilancia en su camino, y nadie solo.',
  },
  relay: {
    what: 'Lo que suena: un coche patrulla, una ambulancia o la alarma de una nave.',
    does: 'No dispara. Tiene salud y blindaje 1, y mientras suene, la zona se llena de zombis.',
    tip: 'Es el objetivo: que deje de sonar antes de que termine la cuenta atrás. Los explosivos ayudan.',
  },
};

// ------------------------------------------------------------------ regions

export interface RegionLore {
  /** Who sends the reports from there, as it reads after "Aviso de". */
  ally: string;
  situation: string;
}

export const REGION_LORE: Record<RegionId, RegionLore> = {
  weu: { ally: 'el Refugio de Brena', situation: 'Los pueblos de la sierra, junto a Atalaya. La nieve y la altura frenan a los zombis; los Cuervos, no.' },
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
  fragGrenade: 'una granada',
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

/** How each deed reads in the debrief (docs/FASE9.md §1), one and many, and whether the noun is feminine. */
const DEED_WORDS: Record<DeedId, [string, string, boolean]> = {
  hit: ['acierto', 'aciertos', false],
  gunKill: ['baja', 'bajas', true],
  blow: ['golpe', 'golpes', false],
  bladeKill: ['baja', 'bajas', true],
  healOther: ['cura a un compañero', 'curas a compañeros', true],
  healSelf: ['cura propia', 'curas propias', true],
  search: ['registro', 'registros', false],
  fix: ['arreglo', 'arreglos', false],
  blast: ['explosivo con blanco', 'explosivos con blanco', false],
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
 * una baja». When the deeds were worth more than the mission's cap, it says so.
 */
export function learnedLine(skill: SkillId, gained: number, deeds: Deeds): string {
  const own = (Object.keys(DEED_XP) as DeedId[]).filter((d) => DEED_XP[d][0] === skill && (deeds[d] ?? 0) > 0);
  const raw = own.reduce((sum, d) => sum + DEED_XP[d][1] * deeds[d]!, 0);
  const why = own.map((d) => deedCount(d, deeds[d]!));
  const head = `+${gained} de ${SKILLS[skill].name}${raw > gained ? ' (lo más que da una misión)' : ''}`;
  return why.length ? `${head}: ${joinAnd(why)}` : head;
}

/** A skill going up: «Ana sube a Tiro 3.»; at a talent's level, it says one waits in the Cuartel. */
export function levelUpLine(who: string, skill: SkillId, level: number): string {
  const talent = Object.values(SKILL_TALENTS).some((t) => t?.skill === skill && t.level === level);
  return `${who} sube a ${SKILLS[skill].name} ${level}.${talent ? ' Tiene un talento por elegir en el Cuartel.' : ''}`;
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
  return `Hablar por radio con ${REGION_LORE[id].ally}: ${income} § al mes mientras su zona no caiga, y se pueden asaltar sus Nidos.`;
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

/** The log line for someone outside when the bus leaves: «Ana se queda fuera.» */
export function leftBehindLine(who: string): string {
  return `${who} se queda fuera.`;
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
  title: 'Atalaya',
  lines: [
    'Hace dos inviernos, la Pandemia vació el valle de Kessra. Los muertos se levantaron y siguen andando.',
    'Atalaya, una vieja estación militar en la sierra de Brena, aguanta con cuarenta supervivientes llegados de todas partes.',
    'Vosotros dos la dirigís. Lo importante lo decidís juntos.',
    'Atalaya vive de lo que sus equipos traen de fuera. El primero sale hoy.',
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
  atalaya: 'Atalaya',
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
  briefing: 'Iréis de noche: veréis poco y os verán poco, salvo bajo una farola o una bengala. Habrá más muertos en la calle, pero lo que traigáis vale la mitad más.',
  report: 'Volvéis con la primera luz. De noche, el botín y la recompensa valen la mitad más.',
};

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
    `Dentro hay medicinas, intactas desde el primer invierno. Recuperadlas y volved ${BUS.to}.`,
    'Uno de sus mensajeros murió allí, a la intemperie. Su mochila sigue junto al cuerpo. Recuperadla antes de que se acabe el tiempo.',
    'Los Cuervos escondieron allí su botín y no volvieron a por él: los muertos llegaron antes. Llevaos lo que podáis.',
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
const RECOVERY_ITEMS: { item: string; outdoors?: true }[] = [{ item: 'las medicinas' }, { item: 'la mochila', outdoors: true }, { item: 'el botín' }];

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
    summary: 'La Pandemia lo vació todo. Atalaya aguanta: muros, agua y una sola carretera.',
    paragraphs: [
      'Hace dos inviernos llegó la Pandemia. Quien se contagia muere en un día y se levanta. Cayeron las ciudades, se fue el ejército y la radio se quedó muda.',
      'Atalaya era una estación militar: muros de hormigón, un refugio bajo tierra, la presa al lado y una sola carretera. Por eso sigue en pie. Quienes llegaron después conservaron su orden: equipos, rangos y armería. Los equipos salen en La Lata, un autobús de línea al que Teo le soldó chapa por encima.',
      'La dirigen dos personas, y lo importante lo deciden juntas. El año pasado, en otro refugio, una líder sola lo vendió a los Cuervos a cambio de su vida.',
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
    title: 'Primer invierno',
    kind: 'recovery',
    biome: 'city',
    region: 'weu',
    difficulty: 1,
    reward: { credits: 90 },
    briefing:
      'El consultorio del pueblo sigue cerrado, con las medicinas del último invierno dentro. Caducan este mes, y el pueblo no está vacío. Recuperadlas y sacadlas por la zona de evacuación.',
    signature: '— N. A.',
    item: 'las medicinas',
    after: 'Las medicinas del consultorio ya están en la enfermería. Por primera vez desde el otoño, nadie tiene que elegir quién se cura.',
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
    after: 'La sirena se calló y los zombis del valle se dispersaron en un día. Teo lo apuntó todo.',
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
      return 'La Marea se deshizo en la sierra. La vacuna salió de Atalaya en La Lata, enclave por enclave, solo para quienes estuvieron. El valle sobrevive. Fuera de él, la radio sigue pidiendo ayuda. La fórmula se queda en una caja del refugio.';
    }
    return 'La Marea se ha deshecho en la sierra. Por primera vez en dos inviernos, el valle duerme sin oír pasos.';
  }
  return doomFull
    ? 'Kilómetro cero. La Marea llegó a Atalaya con la primera niebla. Morse siguió emitiendo hasta el final. La última hoja del diario se quedó a medio escribir.'
    : 'No queda nadie que conteste por radio. Atalaya vuelve a quedarse a oscuras.';
}
