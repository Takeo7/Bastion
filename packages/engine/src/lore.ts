/**
 * The story (docs/LORE.md): mission briefings, the Expediente pages, the story
 * chapters and the epilogues. Apart from the chapters' mission setup (`STORY`),
 * which the campaign reads, it is text only.
 */
import type { MissionOffer, MissionReward, RegionId } from './campaign';
import type { Biome, MissionKind, TemplateId } from './types';

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
  Zombi: 'Un muerto que anda. Va hacia lo que ve y lo que oye. Su mordisco contagia.',
  Corredor: 'Un zombi reciente, todavía ágil. Rápido, y ve de lejos.',
  Acorazado: 'Fue policía o soldado: el chaleco y el casco le dan blindaje.',
  Chillón: 'Un zombi que grita. Llama a todos los de alrededor y mete el miedo en el cuerpo. Primero el Chillón.',
  Hinchado: 'Revienta al morir o al llegar a ti y deja una nube que contagia. Mejor de lejos.',
  Cuervos: 'Bandas de saqueadores. Viven de robar a otros vivos y atraen hordas con sirenas.',
  Congregación: 'Una secta que cree que los zombis están salvados. Los guía con campanas y por radio.',
  Marea: 'Una horda inmensa que sube desde los Llanos de Ceniza. Si llega a Atalaya, la campaña está perdida.',
  Nido: 'Un sitio donde los zombis se juntan y la Marea crece. Quemarlo la hace retroceder.',
  Sirena: 'El altavoz con el que los Cuervos atraen hordas. Calladla y la zona respira.',
  Suministros: 'Lo que se saquea y se cambia: sirve para construir, reclutar y equipar.',
  Medicinas: 'Curan heridas y contagios. Nunca hay bastantes.',
  Comida: 'Cada soldado come cada día. Sin comida no hay voluntad ni salud.',
  Enclave: 'Un grupo de supervivientes con el que tenéis contacto por radio. Si sigue vivo, manda suministros.',
  Rumor: 'Algo que se oye por la radio en una zona. Si lo escucháis unos días al escanear, sabréis qué era.',
  Encargo: 'Misión secundaria en cadena: varias operaciones y decisiones con un desenlace que se queda.',
  Oficio: 'Lo que Teo enseña en el Taller de oficios: machete, fuego, ballesta y enfermería de campo.',
  'Taller de oficios': 'Donde Teo enseña a sobrevivir. Abre la rama de Oficio de cada clase.',
  Recluta: 'Superviviente todavía sin clase, con fusil. Al llegar a Capitán se especializa en el eje de la Rosa en que más haya aprendido.',
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
    does: 'Lento y torpe, pero no se cansa ni tiene miedo. Muerde cuerpo a cuerpo.',
    tip: 'Uno solo no es nada. Diez, sí. No hagáis ruido y no dejéis que os rodeen.',
  },
  xenoid: {
    what: 'Un Corredor: un zombi reciente, que aún tiene piernas.',
    does: 'Rápido y ve de lejos: os encuentra antes de que lleguéis a sus casillas rojas.',
    tip: 'No os confiéis estando ocultos cerca de él. Vigilancia en su camino.',
  },
  mec: {
    what: 'Un Acorazado: fue policía o soldado, y conserva el chaleco y el casco.',
    does: 'Blindaje 1: resta daño a cada impacto. Aguanta mucho más de lo que parece.',
    tip: 'Los explosivos le quitan el blindaje. Si no, los disparos que no fallan.',
  },
  sectoid: {
    what: 'Un Chillón: un zombi que grita.',
    does: 'Su grito mete el miedo en el cuerpo (pánico, desorientación o peor) y despierta a los muertos de alrededor.',
    tip: 'Primero el Chillón. Mientras grite, los muertos se levantan.',
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
