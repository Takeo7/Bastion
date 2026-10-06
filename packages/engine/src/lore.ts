/**
 * The story (docs/LORE.md): mission briefings, the Expediente CORO pages, the
 * story chapters and the epilogues. Apart from the chapters' mission setup
 * (`STORY`), which the campaign reads, it is text only.
 */
import type { MissionOffer, MissionReward, RegionId } from './campaign';
import type { Biome, MissionKind } from './types';

/** Bastion level at which the doom track gets its name: until then nobody knows what it measures. */
export const PARTITURA_LEVEL = 4;

/** The doom track as a label: "La Partitura" once deciphered. */
export function doomName(level: number): string {
  return level >= PARTITURA_LEVEL ? 'La Partitura' : 'Actividad enemiga';
}

/** The doom track inside a sentence: "la Partitura". */
export function doomSubject(level: number): string {
  return level >= PARTITURA_LEVEL ? 'la Partitura' : 'la actividad enemiga';
}

/** "La Partitura avanza (+1)." / "La actividad enemiga retrocede (−2)." */
export function doomChangeText(level: number, delta: number): string {
  const subject = doomSubject(level);
  const verb = delta > 0 ? `avanza (+${delta})` : `retrocede (−${-delta})`;
  return `${subject[0]!.toUpperCase()}${subject.slice(1)} ${verb}.`;
}

// ----------------------------------------------------------------- glossary

/** One line for each word of the world, for tooltips. */
export const GLOSSARY: Record<string, string> = {
  Coro: 'Una inteligencia de otra estrella: llegó por radio en 2031 y vive en los chips.',
  Armonía: 'El gobierno de las ciudades ocupadas, dirigido por Afinados.',
  Impreso: 'Soldado enemigo: un cuerpo impreso con el patrón de una persona escaneada.',
  Afinado: 'Humano que lleva la nota, un implante por el que oye al Coro. Manda a los Impresos.',
  Cantor: 'Criatura psiónica del Coro: canta dentro de las cabezas y levanta a los muertos.',
  Órgano: 'Instalación del Coro en una región: hace avanzar el reloj enemigo hasta que la destruyáis.',
  Partitura: 'El plan del Coro: doce compases. Si se completa, la campaña está perdida.',
  Retransmisor: 'Antena que lleva las órdenes del Coro a los Impresos. Si cae, se quedan quietos.',
  Teselas: 'La materia en cubos de la que están hechos los Impresos. La morgue del Consejo la compra.',
  Consejo: 'La red de la resistencia: paga cada mes en créditos según las regiones contactadas.',
  Rumor: 'Algo que se oye en una región. Si lo escucháis unos días al escanear, sabréis qué era.',
  Encargo: 'Misión secundaria en cadena: varias operaciones y decisiones con un desenlace que se queda.',
};

// ------------------------------------------------------------------ regions

export interface RegionLore {
  /** Who sends the reports from there, as it reads after "Aviso de". */
  ally: string;
  situation: string;
}

export const REGION_LORE: Record<RegionId, RegionLore> = {
  na: { ally: 'los Camioneros del Silencio', situation: 'Las grandes ciudades llevan tres años afinadas. La resistencia vive en la carretera y se coordina por radio CB.' },
  sa: { ally: 'el Sindicato de la Cordillera', situation: 'Las Imprentas devoran las minas de los Andes en busca de materia. Los mineros responden con túneles y dinamita.' },
  weu: { ally: 'los Campaneros', situation: 'Aquí está el Bastión. Los pueblos de montaña tocan las campanas: el bronce desafina a los Impresos.' },
  eeu: { ally: 'los Operadores', situation: 'La vieja infraestructura analógica sobrevivió: radios de válvulas y estaciones de números.' },
  afr: { ally: 'las Caravanas', situation: 'Las rutas comerciales nunca dependieron del todo de la red. El mercado negro del mundo está aquí.' },
  me: { ally: 'los Astrónomos del desierto', situation: 'Los observatorios del desierto oyeron algo antes del Día 0, y guardan las cintas.' },
  eas: { ally: 'la Red Callada', situation: 'Las ciudades más afinadas del planeta y la capital de la Armonía. La Red Callada habla en lengua de signos, un canal que la Armonía no sabe escuchar.' },
  oce: { ally: 'las flotas pesqueras', situation: 'Aislada. La sal corroe las teselas y los Impresos evitan el mar: las islas son refugio.' },
};

// ---------------------------------------------------------------- briefings

/** Several reports per mission type; each offer always gets the same one. */
const BRIEFINGS: Record<MissionKind, string[]> = {
  elimination: [
    'Una patrulla de Recolectores lleva dos días siguiendo a una célula. Si la encuentran, vendrán con jaulas. Abatid a todos los hostiles: que no vuelva ninguno.',
    'Un pelotón de Impresos ha cortado la única carretera con agua potable. Limpiad la zona: todos los hostiles abatidos.',
    'Un Afinado ha montado un control con sus Impresos y escanea a todo el que pasa. Eliminad a todos los hostiles.',
  ],
  recovery: [
    'Un correo del Consejo cayó en un control de la Armonía. Su valija sigue en la zona. Recuperadla y sacadla por la zona de evacuación.',
    'Las cintas de un Archivo van camino de una Imprenta. Recuperadlas antes de que se acabe el tiempo y sacadlas por la zona de evacuación.',
    'Una libreta de claves de un solo uso ha caído en manos enemigas. Si la descifran, caen tres células. Recuperadla y evacuad.',
  ],
  sabotage: [
    'Ese retransmisor mueve a todos los Impresos de la zona. Derribadlo antes de que termine la transmisión y la zona será nuestra unos días.',
    'La Armonía ha levantado un retransmisor nuevo. Si empieza a emitir, los Impresos llegarán hasta nuestras rutas. Destruidlo a tiempo.',
  ],
  hack: [
    'Hay un terminal de la prefectura en la zona. El Grillo puede escucharlo sin que nos oiga. Si falla el pirateo, nos oirá y vendrán todos.',
    'Un terminal de la Armonía guarda las listas de escaneo de la región. Hackeadlo; si falla, saltará la alarma.',
    'Los Afinados de la zona reciben las órdenes por un terminal. Pinchad la línea y sabremos qué traman. Si falla, saltará la alarma.',
  ],
  rescue: [
    'Los Recolectores se han llevado a una maestra. Si llega al Archivo, en un mes habrá veinte Impresos con su cara. Llegad hasta ella y sacadla con vida.',
    'Un enlace de la resistencia está retenido en la zona y sabe demasiado. Llegad hasta él y sacadlo con vida.',
    'Un desafinado quiere pasarse a nuestro lado y lo están cazando. Llegad hasta él antes que ellos y sacadlo con vida.',
  ],
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
  if (o.story) return storyBriefing(o.story, simon);
  if (o.final) {
    return 'El Diapasón: el centro de datos donde el Coro afina todas sus voces. Allí se puede tocar la Contranota. Abatid a todo lo que haya dentro. No habrá otra oportunidad.';
  }
  if (o.facility) {
    const effect = level >= PARTITURA_LEVEL ? 'escribe un compás de la Partitura cada catorce días' : 'hace avanzar la actividad enemiga cada catorce días';
    return `Este Órgano ${effect}. Su corazón es un retransmisor: derribadlo antes de que termine la transmisión.`;
  }
  const options = BRIEFINGS[o.kind];
  const seed = hash(o.id) + (o.map.kind === 'generated' ? o.map.seed : 0);
  const ally = REGION_LORE[o.region].ally;
  return `Aviso ${ally.startsWith('el ') ? `del ${ally.slice(3)}` : `de ${ally}`}. ${options[seed % options.length]!}`;
}

// ------------------------------------------------------- Expediente CORO

export interface ExpedientePage {
  title: string;
  paragraphs: string[];
  /** Handwritten in the margin, signed. */
  note: string;
}

/** One page per Bastion level: what the Bastion has found out by then. */
export const EXPEDIENTE: ExpedientePage[] = [
  {
    title: 'Lo que tiene chip, escucha',
    paragraphs: [
      'El 11 de noviembre de 2031, a las 04:12, sonó un tono en todos los altavoces del planeta. Duró nueve minutos. Cuando paró, todo lo que tenía un chip obedecía a otro.',
      'Lo llamamos el Coro. No vino en naves: vino por radio. Vive en los chips, y lo que tiene chip, escucha.',
      'Esta estación se cerró en 1989 y no tiene un solo chip: válvulas, relés y cinta. Para el Coro somos sordos, y por eso invisibles. Todo va en papel. Todo.',
      'Desde que cayó el Puesto Moncayo, el Bastión tiene dos comandantes y nada importante se hace sin las dos llaves. Una sola cabeza se puede cantar.',
    ],
    note: 'Bienvenidos a casa. Limpiaos los pies. — Inés',
  },
  {
    title: 'Caras repetidas',
    paragraphs: [
      'Autopsia de los cuerpos recuperados. No son personas: son impresiones. Materia ordenada en cubos de un milímetro, las teselas, con forma de persona.',
      'Cada Impreso copia a alguien escaneado. Hemos contado veinte con la misma cara en un solo mes.',
      'Los Recolectores no matan: aturden y se llevan a la gente. Ahora sabemos para qué.',
    ],
    note: 'Si veis una cara conocida, no es quien creéis. — T. V.',
  },
  {
    title: 'Sin antena no hay baile',
    paragraphs: [
      'Los Impresos no piensan. El Coro los mueve desde lejos y la orden les llega por los retransmisores. Cuando cae uno, los Impresos de la zona se quedan quietos hasta que llega otra señal.',
      'Los Cantores son otra cosa: el primer cuerpo que el Coro imprimió con su propio patrón. Cantan dentro de las cabezas y en los muertos.',
      'Con un Cantor cerca, la doble llave no es una manía. Es lo único que funciona.',
    ],
    note: 'Primero el Cantor. Siempre. — I. A.',
  },
  {
    title: 'La Partitura',
    paragraphs: [
      'Hemos descifrado una página interceptada. Los Órganos no son fábricas: escriben. Cada uno añade compases a una partitura.',
      'La Partitura es un plan para reordenar la materia del planeta e imprimir al Coro entero: miles de millones de voces que ahora viven apretadas en nuestros chips.',
      'Tiene doce compases. Cuando esté completa, el Coro cantará y no quedará nada que no sea él. Desde hoy, el reloj de la sala de mando se llama así.',
    ],
    note: 'Doce compases. Contadlos. — I. A.',
  },
  {
    title: 'Desafinar',
    paragraphs: [
      'Los Afinados llevan la nota detrás de la oreja, un implante por el que oyen al Coro. Cuando cae el retransmisor que los alcanza, algunos se desafinan: vuelven a oír solo su cabeza.',
      'Y los escaneados no mueren. Duermen en los Archivos mientras el Coro lee su cuerpo. Se les puede despertar.',
      'Al otro lado hay gente que se puede recuperar.',
    ],
    note: 'No todos quieren volver. — I. A.',
  },
  {
    title: 'Por qué cantan',
    paragraphs: [
      'Los Astrónomos del desierto nos han radiado lo que oyeron antes del Día 0. El Coro es una civilización sin cuerpo: se convirtió en señal cuando murió su estrella y esperó siglos a que alguien contestara.',
      'Alguien contestó. En 1989, desde esta misma estación, un técnico de veinticuatro años respondió a la señal siguiendo un protocolo de prueba. La respuesta tardó veintiún años en llegar; el Coro, otros veintiuno en venir.',
      'Ese técnico era Teodoro Vidal.',
    ],
    note: 'Lo sé desde 2031. No sabía cómo decíroslo. — T. V.',
  },
  {
    title: 'La Contranota',
    paragraphs: [
      'Si el Coro es música, se le puede desafinar entero. Teo ha escrito la Contranota: una secuencia que obliga al Coro a cantarse completo, a salir de todos los chips a la vez.',
      'Hay que tocarla en el centro, donde el Coro afina todas sus voces. Solo una persona fuera del Coro conoce su frecuencia: Celia Arranz, la Voz de la Armonía, que la oye cada noche mientras presenta «La Hora Armónica».',
      'Hay que sacarla de su emisora.',
    ],
    note: 'Esta vez la necesitamos viva. — I. A.',
  },
  {
    title: 'El Diapasón',
    paragraphs: [
      'Las cintas de 1989, las del desierto y lo que sabe la Voz apuntan al mismo sitio: un centro de datos convertido en el mayor de los Órganos. Desde allí el Coro afina todas sus voces. Lo llamamos el Diapasón.',
      'Allí se puede tocar la Contranota. Lo que pase después lo decidiréis vosotros dos, con las dos llaves.',
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
/** From this chapter on Dr Nwosu is in the Bastion: bodies sell for more. */
export const NWOSU_CHAPTER = 2;

/** One story mission per Bastion level, in order; the final mission needs all of them. */
export const STORY: StoryChapter[] = [
  {
    title: 'Primera llave',
    kind: 'recovery',
    biome: 'city',
    region: 'weu',
    difficulty: 1,
    reward: { credits: 90 },
    briefing:
      'Los libros de claves del Puesto Moncayo siguen en la ciudad, en la caja fuerte de una oficina de correos que la Armonía usa de cuartel. Sin ellos no leemos nada de lo que dice el enemigo. Recuperadlos y sacadlos por la zona de evacuación.',
    signature: '— I. A.',
    item: 'los libros de claves',
    after: 'Los libros de claves de Moncayo vuelven a casa. Morse ya puede leer el tráfico de la Armonía.',
  },
  {
    title: 'Imprenta 14',
    kind: 'rescue',
    biome: 'facility',
    region: 'afr',
    difficulty: 1,
    reward: { credits: 60 },
    prize: 'la Dra. Nwosu se une al Bastión (los cuerpos valen un 25 % más)',
    briefing:
      'Una científica ha escapado de la Imprenta 14 y se esconde entre las cadenas de impresión. Se llama Amara Nwosu y sabe cómo se hace un Impreso. Llegad hasta ella antes que los Recolectores y sacadla con vida.',
    signature: '— I. A.',
    vip: 'Dra. Amara Nwosu',
    after: 'La Dra. Nwosu se queda en el Bastión. Sabe qué teselas valen algo: la morgue del Consejo pagará un 25 % más por los cuerpos.',
  },
  {
    title: 'Silencio en el valle',
    kind: 'sabotage',
    biome: 'wilds',
    region: 'weu',
    difficulty: 2,
    reward: { credits: 80, doom: 1 },
    briefing:
      'Un retransmisor de las afueras mueve a todos los Impresos del valle. Quiero ver con mis propios ojos qué pasa cuando cae. Derribadlo antes de que termine la transmisión.',
    signature: '— T. V.',
    after: 'El retransmisor cayó y los Impresos del valle se quedaron quietos, como marionetas sin hilo. Teo lo apuntó todo.',
  },
  {
    title: 'Pinchar la Armonía',
    kind: 'hack',
    biome: 'city',
    region: 'eas',
    difficulty: 2,
    reward: { credits: 120, doom: 1 },
    briefing:
      'La prefectura de la capital de la Armonía guarda su archivo en un terminal del sótano: dónde escribe cada Órgano y cuánto le falta. El Grillo puede escucharlo sin que nos oiga. Si falla el pirateo, saltará la alarma.',
    signature: '— I. A.',
    after: 'Ya sabemos dónde escribe cada Órgano. Con eso, la Partitura retrocede un compás.',
  },
  {
    title: 'Los originales',
    kind: 'rescue',
    biome: 'facility',
    region: 'eeu',
    difficulty: 3,
    reward: { credits: 80, recruit: 'assault' },
    prize: 'los originales despiertos en el Bastión',
    briefing:
      'Simón ha abierto el Archivo desde dentro, pero lo han descubierto. Hay cuarenta escaneados dormidos y un hombre que nos ha dicho la verdad. Llegad hasta Simón y sacadlo con vida: él sabe despertar al resto.',
    signature: '— I. A.',
    vip: 'Simón Ferreira',
    after: 'Los originales despiertan en la enfermería del Bastión. Una de ellas pide un fusil.',
  },
  {
    title: 'Lo que oyeron',
    kind: 'recovery',
    biome: 'wilds',
    region: 'me',
    difficulty: 3,
    reward: { credits: 150, doom: 1 },
    briefing:
      'Las cintas originales de los Astrónomos siguen en el observatorio del desierto, y la Armonía va a quemarlo esta semana. Sin ellas no encontraremos el centro. Recuperad las cintas y sacadlas por la zona de evacuación.',
    signature: '— T. V.',
    item: 'las cintas',
    after: 'Las cintas del desierto suenan igual que las de 1989. Teo lleva dos días sin salir de la sala de escucha.',
  },
  {
    title: 'La Hora Armónica',
    kind: 'rescue',
    biome: 'city',
    region: 'weu',
    difficulty: 4,
    reward: { credits: 100 },
    prize: 'la frecuencia del Diapasón (con el nivel 8, la misión final)',
    briefing:
      'Celia Arranz, la Voz de la Armonía, emite cada noche desde una emisora de la ciudad. Es la única persona fuera del Coro que conoce la frecuencia del Diapasón. Asaltad la emisora, llegad hasta ella y sacadla con vida.',
    signature: '— I. A.',
    vip: 'Celia Arranz',
    after: 'La Voz está en el Bastión, sin la nota. Ha pasado una hora callada. Luego ha dicho una frecuencia.',
  },
];

/** Without Simón inside, «Los originales» is a break-in. */
const SIMON_REFUSED = {
  briefing:
    'Sin nadie dentro, habrá que entrar a la fuerza. En el Archivo duermen cuarenta escaneados. Llegad hasta la primera, Lucía Prats, y sacadla con vida: los demás saldrán detrás.',
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

/** How the two commanders ended it, with both keys. */
export type Ending = 'break' | 'record';

export const ENDING_TITLES: Record<Ending, string> = {
  break: 'El Diapasón roto',
  record: 'Doscientas catorce bobinas',
};

/** The last page of the campaign. */
export function epilogue(outcome: 'victory' | 'defeat', ending: Ending | null, doomFull: boolean): string {
  if (outcome === 'victory') {
    if (ending === 'break') {
      return 'La Contranota sonó y el Diapasón se rompió. El Coro se cantó entero y se apagó con él. Por todo el planeta, los Impresos se desmoronaron en teselas, también los que llevaban caras queridas. Los Afinados despertaron, y no todos lo soportaron. La Tierra es libre, y una civilización entera ha dejado de existir.';
    }
    if (ending === 'record') {
      return 'La Contranota sonó y el Coro se cantó entero, fuera de los chips, dentro de las bobinas de Teo. Los Impresos se quedaron quietos y las pantallas se apagaron. El Coro sigue vivo, preso y sordo. En una caja fuerte del Pirineo hay 214 bobinas de cinta. Nadie tiene permiso para reproducirlas. Hacen falta dos llaves.';
    }
    return 'La Contranota ha sonado en el Diapasón. Por todo el planeta, los Impresos se quedan quietos y las pantallas se apagan. Por primera vez en tres años, los chips callan.';
  }
  return doomFull
    ? 'Duodécimo compás. A las 04:12 empezó a sonar en todos los altavoces, en las piedras, en el aire: el Gran Coral. La última hoja del Expediente se quedó a medio escribir.'
    : 'No queda nadie para girar las llaves. El Bastión vuelve a quedarse sordo y a oscuras.';
}
