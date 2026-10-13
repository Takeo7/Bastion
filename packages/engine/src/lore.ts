/**
 * The story (docs/LORE.md): mission briefings, the Expediente pages, the story
 * chapters and the epilogues. Apart from the chapters' mission setup (`STORY`),
 * which the campaign reads, it is text only.
 */
import type { MissionOffer, MissionReward, RegionId } from './campaign';
import { FOUND_WEAPONS } from './content';
import type { Biome, DecorKind, FoundWeaponId, ItemId, MissionKind, TemplateId } from './types';

/** Bastion level at which the doom track gets its name: until the radio is fixed nobody knows the Marea is coming. */
export const PARTITURA_LEVEL = 4;

/** The doom track as a label: "La Marea" once known. */
export function doomName(level: number): string {
  return level >= PARTITURA_LEVEL ? 'La Marea' : 'Actividad zombi';
}

/** The doom track inside a sentence: "la Marea". */
export function doomSubject(level: number): string {
  return level >= PARTITURA_LEVEL ? 'la Marea' : 'la actividad zombi';
}

/** The doom track at a glance: "La Marea, a 9 km" once known, "Actividad zombi 3/12" before. */
export function doomStatus(level: number, doom: number): string {
  return level >= PARTITURA_LEVEL ? `La Marea, a ${Math.max(0, DOOM_TRACK - doom)} km` : `Actividad zombi ${doom}/${DOOM_TRACK}`;
}

/** Length of the doom track (campaign.ts DOOM_MAX): the Marea starts this many km away. */
const DOOM_TRACK = 12;

/** "La Marea se acerca 1 km." / "La actividad zombi baja (−2)." */
export function doomChangeText(level: number, delta: number): string {
  if (level >= PARTITURA_LEVEL) return delta > 0 ? `La Marea se acerca ${delta} km.` : `La Marea retrocede ${-delta} km.`;
  return delta > 0 ? `La actividad zombi crece (+${delta}).` : `La actividad zombi baja (−${-delta}).`;
}

// ----------------------------------------------------------------- glossary

/** One line for each word of the world, for tooltips. */
export const GLOSSARY: Record<string, string> = {
  Pandemia: 'Empezó hace dos inviernos en el Instituto Galena. Quien se contagia muere en un día y se levanta.',
  Zombi: 'Un muerto que anda. Va a por el vivo que ve y, si no ve a nadie, hacia el último ruido que oyó.',
  Corredor: 'Un zombi reciente, todavía ágil. Rápido, y ve de lejos.',
  Acorazado: 'Fue policía o soldado: el chaleco y el casco le dan blindaje.',
  Chillón: 'Un zombi que no muerde: grita. Su grito asusta a los que están cerca, llama a todos los zombis que lo oyen y levanta a los muertos. Primero el Chillón.',
  Hinchado: 'Un zombi lento y lleno de gases. Revienta al morir o al llegar a vosotros: hace daño a quien tenga al lado y puede contagiarlo. Mejor de lejos.',
  Cuervos: 'Bandas de saqueadores. Viven de robar a otros vivos y atraen hordas con sirenas.',
  Congregación: 'Una secta que cree que los zombis están salvados. Los guía con campanas y por radio.',
  Marea: 'Una horda inmensa que sube desde los Llanos de Ceniza. Si llega a Atalaya, la campaña está perdida.',
  Nido: 'Un sitio donde los zombis se juntan y la Marea crece. Quemarlo la hace retroceder.',
  Sirena: 'El altavoz con el que los Cuervos atraen hordas. Calladla y la zona respira.',
  Ruido: 'Cada acción suena: un disparo se oye a una docena de casillas, un registro a dos. Lo que lo oye va a mirar. El ruido no os delata: os delata que os vean.',
  Saqueo: 'Misión de registrar un pueblo: llenad las mochilas y evacuad. No hay reloj, pero llegan zombis por los bordes, y antes cuanto más ruido.',
  Registrar: 'Una acción junto a un mueble, un coche o unas cajas: suministros, algún accesorio y, a veces, medicinas. Hace un poco de ruido.',
  Mochila: 'Cada soldado lleva tres cosas. El botín cuenta si vuelve: lo que llevaba un caído se pierde.',
  Suministros: 'Lo que se saquea y se cambia: sirve para construir, reclutar y equipar.',
  Medicinas: 'Se cuentan en ✚. El tratamiento de un mordido cuesta 2 (1 con la Enfermería a nivel 2), y una dosis le quita 3 días de cama a un herido. Nunca hay bastantes.',
  Mordisco: 'El de un zombi puede contagiar. El mordido vuelve con fiebre y se convierte en 3 días (5 con Enfermería) si no recibe el tratamiento. Si cae en combate contagiado, se levanta.',
  Comida: 'Se cuenta en raciones. Cada día el Bastión gasta una por cada cuatro soldados. Sin raciones hay hambre: −10 de voluntad en las misiones y las heridas curan despacio.',
  Huerto: 'Bancales, un invernadero y bidones para el agua de lluvia: da raciones cada día, más cuanto más nivel.',
  Piezas: 'Muelles, motores, tornillería: con ellas el Taller hace armas y armaduras mejores. Salen de almacenes, maleteros y oficinas, y del equipo de los Acorazados.',
  Noche: 'De noche se ve poco y os ven poco, salvo bajo una farola, una bengala o un fuego. Hay más muertos en la calle, pero el botín vale la mitad más.',
  Enclave: 'Un grupo de supervivientes con el que tenéis contacto por radio. Si sigue vivo, manda suministros.',
  Rumor: 'Algo que se oye por la radio en una zona. Si lo escucháis unos días al escanear, sabréis qué era.',
  Encargo: 'Misión secundaria en cadena: varias operaciones y decisiones con un desenlace que se queda.',
  Oficio: 'Lo que Teo enseña en el Taller de oficios: machete, fuego, ballesta y enfermería de campo.',
  'Taller de oficios': 'Donde Teo enseña a sobrevivir. Abre la rama de Oficio de cada clase.',
  Maña: 'La aptitud de las manos: arreglar equipos, lanzar granadas y curar.',
  Grillo: 'El dron del Especialista: uno de juguete con una batería de coche, montado por Teo. Cura, cubre, da calambres y arregla a distancia.',
  Recluta: 'Superviviente todavía sin clase, con fusil. Al llegar a Capitán se especializa en el eje del árbol de habilidades en que más haya aprendido.',
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
    what: 'Una Sirena: el altavoz con el que los Cuervos atraen hordas.',
    does: 'No dispara. Tiene salud y blindaje 1, y mientras suene, la zona se llena de zombis.',
    tip: 'Es el objetivo: calladla antes de que termine la cuenta atrás. Los explosivos ayudan.',
  },
};

// ------------------------------------------------------------------ regions

export interface RegionLore {
  /** Who sends the reports from there, as it reads after "Aviso de". */
  ally: string;
  situation: string;
}

export const REGION_LORE: Record<RegionId, RegionLore> = {
  weu: { ally: 'el Refugio de Altomonte', situation: 'Los pueblos de la sierra, junto a Atalaya. La nieve y la altura frenan a los zombis; los Cuervos, no.' },
  afr: { ally: 'los del Hospital', situation: 'La primera ciudad caída. Calles llenas de zombis y farmacias que nadie ha terminado de vaciar.' },
  eas: { ally: 'la Red Callada', situation: 'Un embalse y una central muerta. La Red Callada sobrevivió hablando en lengua de signos: sin un ruido.' },
  eeu: { ally: 'los Pastores', situation: 'Valles cerrados y aldeas que aún resisten. Los Cuervos cobran aquí su peaje.' },
  me: { ally: 'los del Instituto', situation: 'La ciudad muerta donde empezó todo. El Instituto Galena sigue en pie, y nadie quiere entrar.' },
  na: { ally: 'los Camioneros', situation: 'La llanura por donde sube la Marea. Los camioneros la vigilan desde la carretera.' },
  sa: { ally: 'el Sindicato de la Mina', situation: 'Minas y carreteras cortadas. Los mineros resisten con túneles y dinamita.' },
  oce: { ally: 'las Bodegas', situation: 'Granjas, bodegas y un río. Es donde hay comida, y por eso donde más se pelea.' },
};

// ------------------------------------------------------------- searching

/** A piece of furniture that can be searched, as the log and the button read it. */
export interface SearchSpot {
  /** With its article, as it reads after «Registrar»: «el armario». */
  name: string;
  /** What the supplies found in it were; one is picked by seed. */
  supplies: string[];
}

/** Searchable furniture (docs/FASE2.md): what each decor is called and what tends to turn up in it. */
export const SEARCH_SPOTS: Partial<Record<DecorKind, SearchSpot>> = {
  wardrobe: { name: 'el armario', supplies: ['ropa de abrigo', 'mantas y un saco de dormir', 'una caja de zapatos con pilas y velas'] },
  table: { name: 'la mesa', supplies: ['pilas en una bolsa de la compra', 'cubiertos, cerillas y un mechero', 'un mapa de carreteras y una linterna'] },
  desk: { name: 'el escritorio', supplies: ['pilas, cinta americana y un cúter', 'un cargador de manivela', 'un cajón lleno de herramientas pequeñas'] },
  shelf: { name: 'la estantería', supplies: ['velas y cerillas', 'botellas de agua', 'un tarro de clavos y tornillos'] },
  counter: { name: 'el mostrador', supplies: ['tabaco y mecheros', 'pilas y bombillas', 'jabón y lejía'] },
  cabinet: { name: 'el archivador', supplies: ['una linterna y un juego de llaves', 'una petaca y dos mecheros', 'pilas detrás de los expedientes'] },
  bed: { name: 'la cama', supplies: ['mantas limpias', 'una linterna y pilas en la mesilla', 'una bolsa de deporte con ropa seca'] },
  crates: { name: 'las cajas', supplies: ['herramientas', 'bidones de agua y lonas', 'cuerda, alambre y clavos'] },
  carRear: { name: 'el maletero', supplies: ['una garrafa de gasolina', 'herramientas y un gato', 'mantas y cadenas para la nieve'] },
  carFront: { name: 'la guantera', supplies: ['un mapa y un mechero', 'pilas y una linterna', 'un paquete de tabaco a medias'] },
  rack: { name: 'el estante', supplies: ['repuestos y cable', 'bidones de aceite', 'cajas de tornillería'] },
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

function joinAnd(parts: string[]): string {
  return parts.length > 1 ? `${parts.slice(0, -1).join(', ')} y ${parts[parts.length - 1]}` : (parts[0] ?? '');
}

/** The log line for a search: «Ana registra el maletero: una garrafa de gasolina (+12 §) y un botiquín.» */
export function searchLine(who: string, decor: DecorKind, find: SearchFind | null, seed: number): string {
  const spot = SEARCH_SPOTS[decor];
  const where = spot?.name ?? 'el mueble';
  const parts: string[] = [];
  if (find?.supplies) parts.push(`${spot ? spot.supplies[seed % spot.supplies.length] : 'suministros'} (+${find.supplies} §)`);
  if (find?.item) parts.push(ITEM_FINDS[find.item]);
  if (find?.medicines) parts.push(MEDICINE_FINDS[seed % MEDICINE_FINDS.length]!);
  if (find?.food) parts.push(`${FOOD_FINDS[seed % FOOD_FINDS.length]!} (${rations(find.food)})`);
  if (find?.parts) parts.push(`${PARTS_FINDS[seed % PARTS_FINDS.length]!} (${partsCount(find.parts)})`);
  if (find?.weapon) parts.push(FOUND_WEAPONS[find.weapon].find);
  if (!parts.length) return `${who} registra ${where}. ${EMPTY_FINDS[seed % EMPTY_FINDS.length]!}`;
  return `${who} registra ${where}: ${joinAnd(parts)}.`;
}

/** What a search turned up, short, to float over the furniture: «+12 §», «Botiquín», «Medicinas», «+2 raciones», «+1 pieza», a weapon's name or «Nada». */
export function findLabel(find: SearchFind | null): string {
  const parts: string[] = [];
  if (find?.supplies) parts.push(`+${find.supplies} §`);
  if (find?.item) parts.push(ITEM_FINDS[find.item].replace(/^(una?|unos|unas) /, '').replace(/^./, (ch) => ch.toUpperCase()));
  if (find?.medicines) parts.push('Medicinas');
  if (find?.food) parts.push(`+${rations(find.food)}`);
  if (find?.parts) parts.push(`+${partsCount(find.parts)}`);
  if (find?.weapon) parts.push(FOUND_WEAPONS[find.weapon].name);
  return parts.join(' · ') || 'Nada';
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
  'Un pueblo que se vació a toda prisa, con las casas cerradas y las despensas llenas. Registrad lo que podáis y evacuad. Sin ruido: cada tiro llama a más.',
  'Un polígono que nadie ha tocado desde el primer invierno: naves, oficinas y coches en el aparcamiento. Llenad las mochilas y salid por la zona de evacuación. Vendrán zombis, y antes cuanto más ruido hagáis.',
  'Una urbanización a medio construir. Cuando empezó todo, la mitad de las casas tenía gente, y lo que dejaron sigue dentro. Registrad, cargad y evacuad antes de que la horda os encuentre.',
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
  carryingTip: 'Lo que llevan las mochilas ({n}) cuenta si sale por la zona de evacuación o si seguís en pie al ganar. Lo que llevaba un caído se pierde.',
  /** Debrief headline when loot came home. */
  success: 'Botín a salvo',
  /** Debrief headline when the squad came home with nothing. */
  emptyHanded: 'Volvéis con las mochilas vacías',
  /** The horde arrives at the edges. */
  horde: 'Llegan más zombis por los bordes',
};

// ------------------------------------------------------------------ valley

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
    : 'En manos de los muertos: de aquí no llega nada, y sus misiones traen un grupo más. Saldrá si baja la actividad zombi: quemad Nidos.';
}

// ------------------------------------------------------------------- night

/** Night missions (docs/ALTURA-Y-NOCHE.md): the tag, the briefing's last line and the debrief's. */
export const NIGHT_TEXT = {
  label: 'De noche',
  briefing: 'Iréis de noche: veréis poco y os verán poco, salvo bajo una farola o una bengala. Habrá más muertos en la calle, pero lo que traigáis vale la mitad más.',
  report: 'Volvéis con la primera luz. De noche, el botín y la recompensa valen la mitad más.',
};

// ---------------------------------------------------------------- briefings

/** Several reports per mission type; each offer always gets the same one. */
const BRIEFINGS: Record<MissionKind, string[]> = {
  elimination: [
    'Una banda de Cuervos lleva dos días siguiendo a una familia. Si la encuentran, se la llevarán. Abatid a todos los hostiles: que no vuelva ninguno.',
    'Una manada de zombis ha cortado la única carretera con agua potable. Limpiad la zona: todos los hostiles abatidos.',
    'Los Cuervos han montado un control en el cruce y cobran en comida a todo el que pasa. Eliminad a todos los hostiles.',
  ],
  recovery: [
    'La farmacia del pueblo sigue cerrada, con la persiana echada. Dentro hay medicinas. Recuperadlas y sacadlas por la zona de evacuación.',
    'Un correo del Refugio cayó con su mochila en un pueblo infestado. La mochila sigue allí. Recuperadla antes de que se acabe el tiempo y evacuad.',
    'Los Cuervos guardan su botín en un almacén mal vigilado. Recuperad lo que podáis y sacadlo por la zona de evacuación.',
  ],
  sabotage: [
    'Los Cuervos han puesto una sirena en el valle. Mientras suene, cada zombi de la comarca vendrá hacia aquí. Calladla antes de que termine la cuenta atrás.',
    'Una sirena nueva suena cada noche junto a nuestras rutas. Destruidla a tiempo o tendremos la horda en la puerta.',
  ],
  hack: [
    'Hay una radio de emergencia en la zona, muerta desde el invierno. Si la arreglamos, oiremos a quien queda. Si falla, el ruido atraerá a todo lo que ande cerca.',
    'Los Cuervos se comunican por un repetidor de la zona. Pinchadlo y sabremos qué traman. Si falla, saltará la alarma.',
    'En el cuartelillo de la Guardia queda un equipo de radio entero. Ponedlo en marcha; si falla, el ruido traerá compañía.',
  ],
  rescue: [
    'Los Cuervos se han llevado a una maestra del pueblo. Si llega a su campamento, no la volveremos a ver. Llegad hasta ella y sacadla con vida.',
    'Un superviviente está atrapado en una azotea, rodeado. Lleva tres días sin agua. Llegad hasta él y sacadlo con vida.',
    'Un desertor de los Cuervos quiere pasarse a nuestro lado y lo están cazando. Llegad hasta él antes que ellos y sacadlo con vida.',
  ],
  scavenge: SCAVENGE_BRIEFINGS,
};

function hash(text: string): number {
  let h = 0;
  for (let i = 0; i < text.length; i++) h = (h * 31 + text.charCodeAt(i)) >>> 0;
  return h;
}

/** Who signs an operation's report, in pen under it. */
export function briefingSignature(o: MissionOffer): string {
  return o.story ? STORY[o.story - 1]!.signature : '— I. A.';
}

/** Morse's report for an operation, signed by the region's ally. */
export function missionBriefing(o: MissionOffer, level: number, simon: SimonChoice = null): string {
  const text = briefingText(o, level, simon);
  return o.night ? `${text} ${NIGHT_TEXT.briefing}` : text;
}

function briefingText(o: MissionOffer, level: number, simon: SimonChoice): string {
  if (o.story) return storyBriefing(o.story, simon);
  if (o.final) {
    return 'La Marea está en la sierra y la Congregación la empuja desde su campamento. La Mula se la llevará con la sirena; vosotros, al campamento. Abatid a todo lo que haya. No habrá otra oportunidad.';
  }
  if (o.facility) {
    const effect = level >= PARTITURA_LEVEL ? 'acerca la Marea un kilómetro cada catorce días' : 'hace crecer la actividad zombi cada catorce días';
    return `Este Nido ${effect}. Lo mantiene vivo una sirena: calladla antes de que termine la cuenta atrás.`;
  }
  const options = BRIEFINGS[o.kind];
  const seed = hash(o.id) + (o.map.kind === 'generated' ? o.map.seed : 0);
  const ally = REGION_LORE[o.region].ally;
  return `Aviso ${ally.startsWith('el ') ? `del ${ally.slice(3)}` : `de ${ally}`}. ${options[seed % options.length]!}`;
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
    title: 'Primera llave',
    summary: 'La Pandemia lo vació todo. En Atalaya hay muros, agua y dos grupos que se mandan a medias.',
    paragraphs: [
      'Hace dos inviernos llegó la Pandemia. Quien se contagia muere en un día y se levanta. Cayeron las ciudades, se fue el ejército y la radio se quedó muda.',
      'La Estación Atalaya era un puesto de defensa civil: muros de hormigón, un refugio bajo tierra, la presa al lado y una sola carretera. Por eso sigue en pie.',
      'Aquí mandan dos grupos a la vez, y nada importante se hace sin las dos llaves. El año pasado, una líder sola vendió su refugio a los Cuervos a cambio de su vida.',
    ],
    note: 'Bienvenidos a casa. Limpiaos las botas. — Inés',
  },
  {
    title: 'Lo que sabemos de ellos',
    summary: 'Los zombis van hacia lo que ven y lo que oyen. El mordisco contagia. El ruido mata.',
    paragraphs: [
      'Notas de la Dra. Nwosu. No piensan, no se cansan y no tienen miedo. Van hacia lo que ven y, sobre todo, hacia lo que oyen.',
      'Un disparo se oye a kilómetros. Un machete, no. Cada vez que apretamos el gatillo, la comarca viene a ver qué pasa.',
      'El mordisco contagia. Con medicinas a tiempo, a veces se para. Sin ellas, en unos días tendremos que hacer lo que nadie quiere hacer.',
    ],
    note: 'Si veis una cara conocida entre ellos, no es quien creéis. — T. V.',
  },
  {
    title: 'Los Cuervos',
    summary: 'Los Cuervos atraen hordas con sirenas contra quien no les paga. Sin sirena, la zona respira.',
    paragraphs: [
      'Los Cuervos no son una banda: son muchas, con un mismo negocio. Cobran peaje en comida y en gente.',
      'Su arma más sucia es la sirena. La encienden cerca de quien no paga y dejan que la horda haga el resto.',
      'Cuando cayó la sirena del valle, los zombis se dispersaron en un día. Sin ruido, se pierden.',
    ],
    note: 'Las sirenas primero. Siempre. — I. A.',
  },
  {
    title: 'La Marea',
    summary: 'Una horda inmensa sube desde los Llanos con el deshielo. Desde hoy, la contamos en kilómetros.',
    paragraphs: [
      'La radio vuelve a funcionar, y lo primero que hemos oído son los demás enclaves contando lo mismo: algo sube desde los Llanos de Ceniza.',
      'No es una horda: son todas a la vez. Miles de zombis caminando juntos hacia la sierra con el deshielo. La llaman la Marea.',
      'Los Nidos la alimentan. Cada Nido que quememos la frena. Desde hoy, el reloj de la sala de mando cuenta kilómetros.',
    ],
    note: 'Doce kilómetros. Contadlos. — I. A.',
  },
  {
    title: 'Los inmunes',
    summary: 'Hay gente a la que la Pandemia no mata. Su sangre podría ser una vacuna.',
    paragraphs: [
      'Hay gente a la que la Pandemia no mata. Se contagian, tienen fiebre y se levantan al día siguiente… vivos.',
      'Los Cuervos lo saben y los cazan: un inmune vale más que un camión de comida.',
      'La Dra. Nwosu dice que con la sangre de un inmune y los cuadernos del Instituto se podría hacer una vacuna.',
    ],
    note: 'No es un experimento. Es una niña. — A. N.',
  },
  {
    title: 'Galena',
    summary: 'La Pandemia salió del Instituto Galena. Teo estaba allí aquella noche.',
    paragraphs: [
      'Los cuadernos del Instituto Galena cuentan lo que pasó: buscaban una vacuna y crearon otra cosa. La primera muestra se perdió una noche de octubre.',
      'Aquella noche había un vigilante de guardia. Firmó el parte de entrada a las once y el de salida a las seis. Entre medias, una puerta quedó abierta.',
      'Ese vigilante era Teodoro Vidal.',
    ],
    note: 'Lo sé desde el primer día. No sabía cómo decíroslo. — T. V.',
  },
  {
    title: 'La Congregación',
    summary: 'La Congregación empuja la Marea con campanas y radio. Solo la Voz sabe cómo pararla.',
    paragraphs: [
      'La Marea no sube sola. La Congregación cree que los zombis están salvados, y los guía con campanas hacia los vivos.',
      'Los dirige una voz por radio: Celia Arranz, la que presentaba el programa de la tarde. Cada noche les dice hacia dónde caminar.',
      'Si alguien sabe cómo parar la Marea, es ella. Hay que sacarla de su emisora.',
    ],
    note: 'Esta vez la necesitamos viva. — I. A.',
  },
  {
    title: 'La vacuna',
    summary: 'La vacuna está lista y la Marea, en la sierra. Allí decidiréis los dos qué hacer con ella.',
    paragraphs: [
      'La Dra. Nwosu ha terminado la vacuna: la sangre de Lucía, los cuadernos de Galena y muchas noches sin dormir.',
      'La Marea ya está en la sierra. Si la paramos, habrá que decidir qué hacemos con la vacuna, y eso lo decidiréis vosotros dos, con las dos llaves.',
    ],
    note: 'Volved. — I. A. y T. V.',
  },
];

// ------------------------------------------------------------------ story

/** What the commanders did when Simón knocked on the door. */
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
  /** Recovery: what the squad carries out. */
  item?: string;
  /** Rescue: who waits at the objective. */
  vip?: string;
  /** Read in the debrief after winning it. */
  after: string;
}

/** The chapter whose mission needs Simón's visit answered first. */
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
    signature: '— I. A.',
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
      'Una doctora lleva una semana encerrada en el hospital de Ciudad Varela, con el edificio lleno de zombis. Se llama Amara Nwosu y trabajaba en el Instituto Galena. Llegad hasta ella y sacadla con vida.',
    signature: '— I. A.',
    vip: 'Dra. Amara Nwosu',
    after: 'La Dra. Nwosu se queda en el Bastión. Sabe qué aprovechar de cada saqueo: el botín valdrá un 25 % más.',
  },
  {
    title: 'La sirena',
    kind: 'sabotage',
    biome: 'wilds',
    region: 'na',
    difficulty: 2,
    reward: { credits: 80, doom: 1 },
    briefing:
      'Los Cuervos han montado una sirena en los Llanos que suena cada noche y nos trae a media comarca. Quiero verla callada. Destruidla antes de que termine la cuenta atrás.',
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
    signature: '— I. A.',
    after: 'El repetidor vuelve a funcionar. Lo primero que hemos oído es a los demás enclaves avisando de lo mismo: la Marea.',
  },
  {
    title: 'Los inmunes',
    kind: 'rescue',
    biome: 'wilds',
    region: 'eeu',
    difficulty: 3,
    reward: { credits: 80, recruit: 'assault' },
    prize: 'una niña inmune a salvo en el Bastión',
    briefing:
      'Simón entró en el campamento de los Cuervos a por la niña inmune, pero lo han descubierto. Sabe dónde la esconden. Llegad hasta él y sacadlo con vida.',
    signature: '— I. A.',
    vip: 'Simón Ferreira',
    after: 'Lucía está en la enfermería. Se contagió hace un mes y sigue viva. Nwosu no ha dormido.',
  },
  {
    title: 'Los cuadernos de Galena',
    kind: 'recovery',
    biome: 'facility',
    region: 'me',
    difficulty: 3,
    reward: { credits: 150, doom: 1 },
    briefing:
      'Los cuadernos del Instituto Galena siguen en el laboratorio donde empezó todo. Sin ellos no habrá vacuna. Recuperad los cuadernos y sacadlos por la zona de evacuación.',
    signature: '— T. V.',
    item: 'los cuadernos',
    after: 'Los cuadernos ya están en el Bastión. Teo los ha leído enteros y lleva dos días sin hablar.',
  },
  {
    title: 'La Voz',
    kind: 'rescue',
    biome: 'city',
    region: 'na',
    difficulty: 4,
    reward: { credits: 100 },
    prize: 'cómo parar la Marea (con el nivel 8, la misión final)',
    briefing:
      'Celia Arranz, la Voz de la Congregación, emite cada noche desde una vieja emisora de los Llanos. Es la única que sabe cómo parar la Marea. Asaltad la emisora, llegad hasta ella y sacadla con vida.',
    signature: '— I. A.',
    vip: 'Celia Arranz',
    after: 'La Voz está en el Bastión. Ha pasado una hora callada. Luego ha dicho cómo se para la Marea.',
  },
];

/** Without Simón inside, «Los inmunes» is a break-in, straight for the girl. */
const SIMON_REFUSED = {
  briefing:
    'Sin nadie dentro, habrá que entrar a la fuerza en el campamento de los Cuervos. La niña inmune se llama Lucía Prats. Llegad hasta ella y sacadla con vida.',
  vip: 'Lucía Prats',
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
      return 'La Marea se deshizo en la sierra. La vacuna salió de Atalaya en La Mula, enclave por enclave, solo para quienes estuvieron. El valle sobrevive. Fuera de él, la radio sigue pidiendo ayuda. En el refugio hay una caja con la fórmula, y para abrirla hacen falta dos llaves.';
    }
    return 'La Marea se ha deshecho en la sierra. Por primera vez en dos inviernos, el valle duerme sin oír pasos.';
  }
  return doomFull
    ? 'Kilómetro cero. La Marea llegó a Atalaya con la primera niebla. Morse siguió emitiendo hasta el final. La última hoja del Expediente se quedó a medio escribir.'
    : 'No queda nadie para girar las llaves. Atalaya vuelve a quedarse a oscuras.';
}
