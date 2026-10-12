/**
 * Side quests, the "encargos" of docs/LORE.md: chains of steps (operations or
 * decisions taken with both keys) that end with a unique reward and leave a
 * consequence behind. Data only: campaign.ts starts and runs them.
 *
 * Texts may hold placeholders in braces ({soldier}, {relative}, {a}…), filled
 * from the quest's `names` when it starts.
 */
import type { MissionReward, RegionId } from './campaign';
import type { TechId } from './progression';
import type { Biome, ClassId, ItemId, MissionKind, TemplateId } from './types';

export type QuestId = 'faces' | 'ibarra' | 'amara' | 'bells' | 'numbers' | 'route50' | 'hands' | 'mole' | 'mine' | 'sunken';

// ------------------------------------------------------------------- traits

/** What a quest can leave on a soldier, for good. */
export type TraitId = 'grudge' | 'scarred';

export interface TraitDef {
  name: string;
  description: string;
  aim: number;
  will: number;
}

export const TRAITS: Record<TraitId, TraitDef> = {
  grudge: { name: 'Rencor', description: '+10 de puntería y −15 de voluntad: no perdona a los Cuervos.', aim: 10, will: -15 },
  scarred: { name: 'Pesadillas', description: '−20 de voluntad: desde entonces no duerme una noche entera.', aim: 0, will: -20 },
};

// -------------------------------------------------------------------- steps

/** An operation the quest puts on the geoscape. */
export interface QuestMission {
  type: 'mission';
  name: string;
  kind: MissionKind;
  biome: Biome;
  /** Added to the difficulty of the day. */
  difficulty?: number;
  /** Days to launch it before it expires (and the quest fails). */
  days: number;
  briefing: string;
  /** Recovery: what the squad carries out. */
  item?: string;
  /** Rescue: who waits at the objective. */
  vip?: string;
  /** Named enemy leading the first group. */
  leader?: { template: TemplateId; name: string };
  reward?: MissionReward;
}

export interface QuestChoice {
  label: string;
  effect: string;
  /**
   * 'next' moves on to the following step; 'guess' ends the quest well if this
   * option is the hidden one (`secret`) and badly otherwise; any other value
   * ends it with that outcome.
   */
  then: 'next' | 'guess' | string;
  /** Watching someone tells whether they are the hidden one. */
  clue?: boolean;
}

/** A decision taken with both keys, shown like any event. */
export interface QuestDecision {
  type: 'event';
  title: string;
  text: string;
  options: [QuestChoice, QuestChoice];
}

export type QuestStep = QuestMission | QuestDecision;

// ----------------------------------------------------------------- outcomes

export interface QuestOutcome {
  /** Logged and shown when the quest ends this way. */
  text: string;
  tone: 'good' | 'bad';
  credits?: number;
  /** Bastion experience. */
  xp?: number;
  /** Change of the doom track: negative sets it back. */
  doom?: number;
  items?: Partial<Record<ItemId, number>>;
  /** Monthly credits from the quest's region, for good. */
  income?: number;
  /** Extra regions that can be contacted. */
  contacts?: number;
  /** Technology granted before its level. */
  tech?: TechId;
  /** Someone joins the Bastion (owned by the quest's commander, if it has one). */
  recruit?: { name?: string; cls?: ClassId; rank?: number; trait?: TraitId };
  /** Left on the quest's soldier. */
  trait?: TraitId;
  /** One more of these in the first group of every operation in the region. */
  enemy?: TemplateId;
  /** Days the ongoing construction is delayed. */
  delay?: number;
  /** The quest can start again after this many days. */
  retry?: number;
}

export interface QuestDef {
  id: QuestId;
  title: string;
  /** Who asks: signs its reports. */
  signature: string;
  /** Logged when it starts. */
  hook: string;
  /** What finishing it gives, for the briefings. */
  prize: string;
  /** Where its operations happen and its regional effects apply; null: a contacted region chosen at the start. */
  region: RegionId | null;
  /** Regional quests come out of a rumour in their region once it is contacted; the rest have their own trigger. */
  rumor: boolean;
  steps: QuestStep[];
  /** `success` closes a quest whose last step is an operation; `failure`, a lost or expired one. */
  outcomes: { failure: QuestOutcome } & Record<string, QuestOutcome>;
  /** For quests with a hidden one (`clue` choices): what watching them shows. */
  clues?: { guilty: string; innocent: string };
}

export const QUESTS: Record<QuestId, QuestDef> = {
  faces: {
    id: 'faces',
    title: 'Caras conocidas',
    signature: '— I. A.',
    hook: '{soldier} ha oído un nombre conocido en la radio de los Cuervos.',
    prize: '{relative} se une al Bastión',
    region: null,
    rumor: false,
    steps: [
      {
        type: 'event',
        title: 'Caras conocidas',
        text: 'Los Cuervos leen por radio la lista de sus prisioneros: los cambian por comida y munición. {soldier} ha oído un nombre, el de {relation}, {relative}. Encontrar su campamento costará días y soldados.',
        options: [
          { label: 'Buscar el campamento', effect: 'Tres operaciones para encontrar a {relative}', then: 'next' },
          { label: 'No podemos permitírnoslo', effect: '{soldier} gana el rasgo «Rencor»', then: 'refused' },
        ],
      },
      {
        type: 'mission',
        name: 'La lista de prisioneros',
        kind: 'recovery',
        biome: 'city',
        days: 8,
        briefing: 'Los Cuervos apuntan a sus prisioneros en un registro que guardan en un control de la carretera. Dice quién tiene a {relative}. Recuperadlo y sacadlo por la zona de evacuación.',
        item: 'el registro de prisioneros',
        reward: { credits: 40 },
      },
      {
        type: 'mission',
        name: 'La radio del campamento',
        kind: 'hack',
        biome: 'city',
        days: 8,
        briefing: 'El registro no dice dónde está el campamento. Su radio se oye desde un repetidor roto de la ciudad. Arregladlo y escuchad: sabremos dónde tienen a {relative}. Si el arreglo falla, el ruido atraerá a todo lo que haya cerca.',
        reward: { credits: 40 },
      },
      {
        type: 'mission',
        name: 'Sacar a {relative}',
        kind: 'rescue',
        biome: 'facility',
        difficulty: 1,
        days: 8,
        briefing: 'Los Cuervos guardan a sus prisioneros en un almacén cerrado con cadenas. {relative} está dentro. Llegad hasta allí y sacad a {relative} con vida.',
        vip: '{relative}',
      },
    ],
    outcomes: {
      success: { text: '{relative} llega a Atalaya en los huesos. Al día siguiente pide un fusil, como {soldier}.', tone: 'good', recruit: { name: '{relative}' }, retry: 40 },
      refused: { text: '{soldier} no ha dicho nada. Desde entonces dispara antes de preguntar.', tone: 'bad', trait: 'grudge', retry: 30 },
      failure: { text: 'Cuando llegamos, el campamento estaba vacío. {soldier} no habla de ello, pero ahora dispara antes de preguntar.', tone: 'bad', trait: 'grudge', retry: 30 },
    },
  },

  ibarra: {
    id: 'ibarra',
    title: 'La comandante Ibarra',
    signature: '— I. A.',
    hook: 'Morse ha oído el nombre de Elena Ibarra en una frecuencia de los Cuervos.',
    prize: 'decidir qué hacer con Ibarra',
    region: 'weu',
    rumor: false,
    steps: [
      {
        type: 'mission',
        name: 'El diario del Collado',
        kind: 'recovery',
        biome: 'city',
        days: 10,
        briefing: 'Elena Ibarra mandaba el Refugio del Collado. El año pasado se lo vendió a los Cuervos a cambio de su vida: por eso aquí todo se decide con dos llaves. Su diario sigue en las ruinas del Collado y dice adónde fue después. Recuperadlo y sacadlo por la zona de evacuación.',
        item: 'el diario',
        reward: { credits: 50 },
      },
      {
        type: 'mission',
        name: 'Elena Ibarra',
        kind: 'elimination',
        biome: 'city',
        difficulty: 1,
        days: 8,
        briefing: 'Ibarra manda ahora una banda de Cuervos. Abatid a todos los hostiles. A ella, si se puede, la quiero viva.',
        leader: { template: 'officer', name: 'Elena Ibarra' },
      },
      {
        type: 'event',
        title: 'Elena Ibarra',
        text: 'La escuadra encuentra a Ibarra herida entre los escombros. No pide perdón: dice que lo volvería a hacer. Sabe mandar, y sabe dónde guarda su banda lo que roba.',
        options: [
          { label: 'Llevarla a Atalaya', effect: 'Se une como sargento, con el rasgo «Pesadillas»', then: 'captured' },
          { label: 'Acabar con esto', effect: '+150 suministros de su alijo', then: 'killed' },
        ],
      },
    ],
    outcomes: {
      captured: { text: 'Ibarra ha vuelto. Duerme poco y con la luz encendida. Cuesta mirarla, pero sabe mandar.', tone: 'good', recruit: { name: 'Elena Ibarra', cls: 'sharpshooter', rank: 3, trait: 'scarred' } },
      killed: { text: 'Ibarra ya no venderá a nadie más. Su alijo nos da munición para un mes.', tone: 'good', credits: 150 },
      failure: { text: 'Ibarra se nos ha escapado. Volverá a aparecer.', tone: 'bad', retry: 25 },
    },
  },

  amara: {
    id: 'amara',
    title: 'El ayudante de la doctora',
    signature: '— Dra. Nwosu',
    hook: 'Ha llegado una carta para la Dra. Nwosu.',
    prize: 'decidir qué hacer con Tomás Rubio',
    region: 'afr',
    rumor: false,
    steps: [
      {
        type: 'event',
        title: 'Una carta para la doctora',
        text: 'Los del Hospital traen una carta para la Dra. Nwosu. La firma el doctor Tomás Rubio, su ayudante en el Instituto Galena. Ella lo creía muerto. Está vivo, trabaja para la Congregación en un laboratorio de Ciudad Varela y quiere hablar.',
        options: [
          { label: 'Contestar', effect: 'Una operación en el laboratorio', then: 'next' },
          { label: 'Quemar la carta', effect: 'Sin efecto', then: 'burned' },
        ],
      },
      {
        type: 'mission',
        name: 'El laboratorio de Varela',
        kind: 'hack',
        biome: 'facility',
        difficulty: 1,
        days: 8,
        briefing: 'Rubio nos ha dejado abierto su laboratorio. Tiene un ordenador que funciona con un generador. Arrancadlo y leed sus notas: sabremos si quiere salir o tendernos una trampa. Si algo falla, la Congregación tocará sus campanas y vendrán todos.',
      },
      {
        type: 'event',
        title: 'Tomás Rubio',
        text: 'Quiere salir. La Congregación le hace estudiar cómo guiar a los zombis con sonido, y a escondidas ha avanzado con los antibióticos. Si lo sacamos, viene con sus cuadernos. Si quemamos el laboratorio, la Voz pierde un año de trabajo. La Dra. Nwosu no sabe si perdonarlo.',
        options: [
          { label: 'Sacarlo de allí', effect: 'Antibióticos sin esperar al nivel 6', then: 'rescued' },
          { label: 'Quemar el laboratorio', effect: 'Los zombis retroceden (−1)', then: 'ended' },
        ],
      },
    ],
    outcomes: {
      rescued: { text: 'Rubio y la Dra. Nwosu trabajan en la misma mesa y no se dirigen la palabra. Los antibióticos llegan antes de tiempo.', tone: 'good', tech: 'nanoMedkit' },
      ended: { text: 'El laboratorio arde con un año de trabajo de la Congregación dentro, y los zombis retroceden. La Dra. Nwosu pasa la noche junto a la radio.', tone: 'good', doom: -1 },
      burned: { text: 'La carta arde. La Dra. Nwosu no pregunta.', tone: 'bad' },
      failure: { text: 'La Congregación sabe que la buscamos. Con sus campanas trae a Ciudad Varela a los Acorazados del cuartel: uno más en cada operación de la zona.', tone: 'bad', enemy: 'mec' },
    },
  },

  bells: {
    id: 'bells',
    title: 'Las campanas de San Telmo',
    signature: '— Los de San Telmo',
    hook: 'Las campanas de San Telmo tocan a rebato.',
    prize: 'dos granadas aturdidoras y +10 suministros al mes',
    region: 'weu',
    rumor: true,
    steps: [
      {
        type: 'event',
        title: 'Las campanas de San Telmo',
        text: 'San Telmo, un pueblo de la sierra, toca a rebato cada noche: viene una horda y la gente corre a encerrarse en la iglesia. Los Cuervos han puesto una sirena en el valle que llama a los zombis hacia el pueblo. Cuando no quede nadie, bajarán a por lo que haya.',
        options: [
          { label: 'Callar la sirena', effect: 'Una operación de sabotaje', then: 'next' },
          { label: 'Ahora no podemos', effect: 'San Telmo se vacía: −10 suministros al mes', then: 'failure' },
        ],
      },
      {
        type: 'mission',
        name: 'Las campanas de San Telmo',
        kind: 'sabotage',
        biome: 'wilds',
        days: 8,
        briefing: 'La sirena de los Cuervos está en una loma sobre el valle. Destrozadla antes de que la horda llegue al pueblo. Las campanas avisarán al resto.',
      },
    ],
    outcomes: {
      success: { text: 'San Telmo ha resistido. El pueblo nos da las dos granadas aturdidoras del cuartelillo y parte de cada cosecha (+10 suministros al mes).', tone: 'good', items: { flashbang: 2 }, income: 10 },
      failure: { text: 'Las campanas de San Telmo se han callado. El pueblo está vacío (−10 suministros al mes).', tone: 'bad', income: -10 },
    },
  },

  numbers: {
    id: 'numbers',
    title: 'La estación de números',
    signature: '— I. A.',
    hook: 'Morse ha oído un indicativo que no oía desde antes de la Pandemia.',
    prize: 'una zona contactable más',
    region: 'eeu',
    rumor: false,
    steps: [
      {
        type: 'mission',
        name: 'La estación de números',
        kind: 'hack',
        biome: 'city',
        days: 10,
        briefing: 'Una estación de números emite con el indicativo de mi maestro, Ramiro Ostalé. Lo creía muerto. En lo alto del ayuntamiento de Valdelobos hay un repetidor roto que recoge su señal: si lo arregláis, sabremos desde dónde emite. Si el arreglo falla, el ruido atraerá a todo lo que haya cerca.',
      },
      {
        type: 'mission',
        name: 'Ramiro Ostalé',
        kind: 'rescue',
        biome: 'wilds',
        days: 8,
        briefing: 'Ramiro está vivo, en una cabaña de Valdelobos, con una radio de válvulas. Los Cuervos también lo han oído. Llegad antes que ellos y sacadlo con vida.',
        vip: 'Ramiro Ostalé',
      },
    ],
    outcomes: {
      success: { text: 'Ramiro Ostalé monta su radio en Atalaya. Con él, la red llega a una zona más.', tone: 'good', contacts: 1 },
      failure: { text: 'No llegamos a tiempo. Morse ha escrito algo en el margen del informe y lo ha tachado todo menos una palabra: «gracias».', tone: 'bad' },
    },
  },

  route50: {
    id: 'route50',
    title: 'Ruta 50',
    signature: '— Los Camioneros',
    hook: 'Los Camioneros han perdido un convoy en los Llanos de Ceniza.',
    prize: '+15 suministros al mes',
    region: 'na',
    rumor: true,
    steps: [
      {
        type: 'mission',
        name: 'Ruta 50',
        kind: 'rescue',
        biome: 'wilds',
        days: 8,
        briefing: 'Una partida de caza de los Cuervos ha asaltado el convoy en la Ruta 50. La conductora, Lola Haskins, se ha encerrado en una gasolinera. Llegad hasta ella y sacadla con vida.',
        vip: 'Lola Haskins',
        reward: { credits: 40 },
      },
      {
        type: 'mission',
        name: 'Cacería en la Ruta 50',
        kind: 'elimination',
        biome: 'wilds',
        days: 6,
        briefing: 'La partida de caza que asaltó el convoy sigue en la carretera. Mientras siga ahí, no pasará ningún camión. Abatid a todos los hostiles.',
      },
    ],
    outcomes: {
      success: { text: 'Los Camioneros vuelven a la Ruta 50 y paran en Atalaya en cada viaje (+15 suministros al mes).', tone: 'good', income: 15 },
      failure: { text: 'Los Camioneros dejan de pasar por los Llanos (−10 suministros al mes).', tone: 'bad', income: -10 },
    },
  },

  hands: {
    id: 'hands',
    title: 'Manos',
    signature: '— La Red Callada',
    hook: 'La Red Callada ha perdido a una de sus enlaces.',
    prize: '+15 de experiencia y 50 suministros',
    region: 'eas',
    rumor: true,
    steps: [
      {
        type: 'mission',
        name: 'Manos',
        kind: 'rescue',
        biome: 'city',
        days: 8,
        briefing: 'En la Red Callada hay sordos y sus familias: han sobrevivido porque hablan con las manos, sin hacer ruido. Los Cuervos se han llevado a Mei Takahara, una de sus enlaces, y no saben lo que vale. Llegad hasta ella y sacadla con vida.',
        vip: 'Mei Takahara',
      },
      {
        type: 'mission',
        name: 'Las notas de Mei',
        kind: 'recovery',
        biome: 'city',
        days: 8,
        briefing: 'Mei apuntaba en papel por dónde pasan las hordas del Pantano Gris. Sus notas siguen en una casa que los Cuervos están saqueando. Recuperadlas y sacadlas por la zona de evacuación.',
        item: 'las notas',
      },
    ],
    outcomes: {
      success: { text: 'La Red Callada nos enseña sus señas. Ahora las escuadras hablan sin hacer ruido, y sabemos por dónde pasan las hordas del Pantano.', tone: 'good', xp: 15, credits: 50 },
      failure: { text: 'La Red Callada se esconde. No volveremos a saber de ella.', tone: 'bad' },
    },
  },

  mole: {
    id: 'mole',
    title: 'El topo',
    signature: '— I. A.',
    hook: 'Dos sabotajes en el generador no son casualidad.',
    prize: 'encontrar al topo',
    region: 'weu',
    rumor: false,
    steps: [
      {
        type: 'event',
        title: 'El topo',
        text: 'Alguien del Bastión pasa información a los Cuervos. Solo dos personas bajaron al generador las noches de los sabotajes: {a} y {b}. Podemos vigilar a una de ellas una semana.',
        options: [
          { label: 'Vigilar a {a}', effect: 'Una semana de vigilancia', then: 'next', clue: true },
          { label: 'Vigilar a {b}', effect: 'Una semana de vigilancia', then: 'next', clue: true },
        ],
      },
      {
        type: 'event',
        title: 'El topo',
        text: '{clue} Hay que decidir antes de que vuelva a pasar. Si acusamos a quien no es, el topo sabrá que lo buscamos.',
        options: [
          { label: 'Acusar a {a}', effect: 'Si es el topo, los zombis retroceden (−1)', then: 'guess' },
          { label: 'Acusar a {b}', effect: 'Si es el topo, los zombis retroceden (−1)', then: 'guess' },
        ],
      },
    ],
    clues: {
      guilty: '{name} sale de noche hacia la antena vieja y vuelve sin hacer ruido.',
      innocent: '{name} no se mueve de su catre en toda la semana.',
    },
    outcomes: {
      success: { text: '{culprit} confiesa: pasaba los turnos de guardia a los Cuervos por una radio escondida en el almacén. Sabían cuándo dormíamos y por dónde mandarnos a los zombis. Sin sus avisos, los zombis retroceden.', tone: 'good', doom: -1 },
      failure: { text: 'Hemos acusado a quien no era. Esa noche, {culprit} desaparece con la caja de suministros (−60) y deja las obras saboteadas.', tone: 'bad', credits: -60, delay: 4 },
    },
  },

  mine: {
    id: 'mine',
    title: 'La mina',
    signature: '— El Sindicato de la Mina',
    hook: 'En una mina abandonada de Carboneda se oyen gritos por las noches.',
    prize: 'una granadera y dos granadas',
    region: 'sa',
    rumor: true,
    steps: [
      {
        type: 'event',
        title: 'La mina',
        text: 'Los mineros del Sindicato oyen gritos en una mina abandonada de Carboneda. Las galerías están llenas de zombis, y dentro hay un Chillón que los llama. Cada noche salen más hacia los pueblos.',
        options: [
          { label: 'Bajar a la mina', effect: 'Una operación contra el Chillón', then: 'next' },
          { label: 'Sellarla con dinamita', effect: 'Sin efecto', then: 'sealed' },
        ],
      },
      {
        type: 'mission',
        name: 'La mina',
        kind: 'elimination',
        biome: 'wilds',
        difficulty: 1,
        days: 8,
        briefing: 'El Chillón de la mina lleva semanas llamando a los zombis del valle. Bajad y abatid a todos los hostiles. Primero el Chillón: si grita, vendrán todos.',
        leader: { template: 'sectoid', name: 'El Chillón de la mina' },
      },
    ],
    outcomes: {
      success: { text: 'La mina calla. El Sindicato nos manda a su mejor barrenera y una caja de dinamita.', tone: 'good', recruit: { cls: 'grenadier' }, items: { fragGrenade: 2 } },
      sealed: { text: 'El Sindicato sella la mina. Nadie sabe cuántos quedaron dentro.', tone: 'bad' },
      failure: { text: 'Los mineros abandonan Carboneda (−10 suministros al mes).', tone: 'bad', income: -10 },
    },
  },

  sunken: {
    id: 'sunken',
    title: 'El camión del río',
    signature: '— Las Bodegas',
    hook: 'Los de las Bodegas han encontrado algo en el fondo del río.',
    prize: '150 suministros',
    region: 'oce',
    rumor: true,
    steps: [
      {
        type: 'event',
        title: 'El camión del río',
        text: 'Con el río bajo, los de las Bodegas han visto un camión de suministros hundido junto al puente de La Vega. Las cajas están precintadas y muchas siguen secas. Los Cuervos también lo han visto.',
        options: [
          { label: 'Llegar antes que ellos', effect: 'Una operación de recuperación', then: 'next' },
          { label: 'Dejárselo al río', effect: 'Sin efecto', then: 'left' },
        ],
      },
      {
        type: 'mission',
        name: 'El camión del río',
        kind: 'recovery',
        biome: 'wilds',
        days: 6,
        briefing: 'Los de las Bodegas han subido las cajas del camión a un embarcadero. Recuperadlas y sacadlas por la zona de evacuación antes de que lleguen los Cuervos.',
        item: 'las cajas',
      },
    ],
    outcomes: {
      success: { text: 'Las cajas del camión traen conservas, vendas y munición para un mes entero (+150 suministros).', tone: 'good', credits: 150 },
      left: { text: 'El camión se queda en el fondo del río.', tone: 'bad' },
      failure: { text: 'Los Cuervos se llevan las cajas del embarcadero.', tone: 'bad' },
    },
  },
};

// ------------------------------------------------------------------- people

/** Relatives a soldier can hear named on the Cuervos' prisoner list ("Caras conocidas"). */
export const RELATIVES: { name: string; relation: string }[] = [
  { name: 'Marta', relation: 'su hermana' },
  { name: 'Tomás', relation: 'su hermano' },
  { name: 'Carmen', relation: 'su madre' },
  { name: 'Andrés', relation: 'su padre' },
  { name: 'Lucía', relation: 'su hermana pequeña' },
  { name: 'Joaquín', relation: 'su primo' },
];

/** People with access to the generator ("El topo"); Simón joins them if he was trusted. */
export const SUSPECTS = ['Paco Lanuza, el electricista', 'Rosa Aldea, la cocinera', 'Bruno Sanz, el de la radio'];

/** What rumours are about ("Se habla de …"). */
export const RUMOR_LINES = [
  'humo en una chimenea de un pueblo vacío',
  'una radio que repite un nombre',
  'una farmacia que nadie ha saqueado',
  'una horda que baja hacia el río',
  'un convoy que no llegó',
  'luces en una mina cerrada',
  'campanas que suenan a deshora',
  'un perro que ladra en una granja abandonada',
];

/** "{relative} llega a Atalaya…" with the quest's names. */
export function fill(text: string, names: Record<string, string>): string {
  return text.replace(/\{(\w+)\}/g, (match, key: string) => names[key] ?? match);
}
