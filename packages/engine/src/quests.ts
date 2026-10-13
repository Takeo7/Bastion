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
  grudge: { name: 'Rencor', description: '+10 de puntería, −15 de voluntad. No perdona a los Cuervos.', aim: 10, will: -15 },
  scarred: { name: 'Pesadillas', description: '−20 de voluntad. No ha vuelto a dormir una noche entera.', aim: 0, will: -20 },
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
    signature: '— N. A.',
    hook: '{soldier} ha reconocido un nombre en la radio de los Cuervos.',
    prize: '{relative} se une al Bastión',
    region: null,
    rumor: false,
    steps: [
      {
        type: 'event',
        title: 'Caras conocidas',
        text: 'Los Cuervos leen por radio su lista de prisioneros. Los cambian por comida y munición. {soldier} ha oído el nombre de {relation}, {relative}. Encontrar el campamento costará días y soldados.',
        options: [
          { label: 'Buscar el campamento', effect: 'Tres operaciones', then: 'next' },
          { label: 'No podemos permitírnoslo', effect: '{soldier} gana el rasgo «Rencor»', then: 'refused' },
        ],
      },
      {
        type: 'mission',
        name: 'La lista de prisioneros',
        kind: 'recovery',
        biome: 'city',
        days: 8,
        briefing: 'Los Cuervos llevan un registro de prisioneros en un control de la carretera. Ahí consta quién tiene a {relative}. Recuperadlo y sacadlo por la zona de evacuación.',
        item: 'el registro de prisioneros',
        reward: { credits: 40 },
      },
      {
        type: 'mission',
        name: 'La radio del campamento',
        kind: 'hack',
        biome: 'city',
        days: 8,
        briefing: 'El registro no dice dónde está el campamento. Su radio llega a un repetidor roto de la ciudad. Reparadlo y lo localizaremos. Si falla, el ruido atraerá a lo que ande cerca.',
        reward: { credits: 40 },
      },
      {
        type: 'mission',
        name: 'Sacar a {relative}',
        kind: 'rescue',
        biome: 'facility',
        difficulty: 1,
        days: 8,
        briefing: 'Los prisioneros están en un almacén cerrado con cadenas. {relative} está dentro. Hay que sacar a {relative} con vida.',
        vip: '{relative}',
      },
    ],
    outcomes: {
      success: { text: '{relative} llega a Atalaya en los huesos. Al día siguiente pide un fusil.', tone: 'good', recruit: { name: '{relative}' }, retry: 40 },
      refused: { text: '{soldier} no dice nada. Desde entonces dispara antes de preguntar.', tone: 'bad', trait: 'grudge', retry: 30 },
      failure: { text: 'El campamento estaba vacío cuando llegamos. {soldier} no habla de ello. Ahora dispara antes de preguntar.', tone: 'bad', trait: 'grudge', retry: 30 },
    },
  },

  ibarra: {
    id: 'ibarra',
    title: 'La comandante Lindqvist',
    signature: '— N. A.',
    hook: 'Morse ha oído el nombre de Vera Lindqvist en una frecuencia de los Cuervos.',
    prize: 'decidir qué hacer con Lindqvist',
    region: 'weu',
    rumor: false,
    steps: [
      {
        type: 'mission',
        name: 'El diario del Collado',
        kind: 'recovery',
        biome: 'city',
        days: 10,
        briefing: 'Vera Lindqvist mandaba el Refugio del Collado. El año pasado se lo entregó a los Cuervos a cambio de su vida. Por ella, aquí todo se decide con dos llaves. Su diario sigue en las ruinas y dice adónde fue. Recuperadlo y sacadlo por la zona de evacuación.',
        item: 'el diario',
        reward: { credits: 50 },
      },
      {
        type: 'mission',
        name: 'Vera Lindqvist',
        kind: 'elimination',
        biome: 'city',
        difficulty: 1,
        days: 8,
        briefing: 'Lindqvist manda ahora una banda de Cuervos. Abatid a todos los hostiles. A ella, si se puede, la quiero viva.',
        leader: { template: 'officer', name: 'Vera Lindqvist' },
      },
      {
        type: 'event',
        title: 'Vera Lindqvist',
        text: 'Lindqvist está herida entre los escombros. No pide perdón. Dice que lo volvería a hacer. Sabe mandar, y sabe dónde guarda su banda lo robado.',
        options: [
          { label: 'Llevarla a Atalaya', effect: 'Se une como sargento, con el rasgo «Pesadillas»', then: 'captured' },
          { label: 'Acabar con esto', effect: '+150 suministros de su alijo', then: 'killed' },
        ],
      },
    ],
    outcomes: {
      captured: { text: 'Lindqvist duerme en Atalaya, poco y con la luz encendida. Nadie le habla si no hace falta. Sabe mandar.', tone: 'good', recruit: { name: 'Vera Lindqvist', cls: 'sharpshooter', rank: 3, trait: 'scarred' } },
      killed: { text: 'Lindqvist está muerta. Su alijo pasa a Atalaya (+150 suministros).', tone: 'good', credits: 150 },
      failure: { text: 'Lindqvist ha escapado. Volverá a aparecer.', tone: 'bad', retry: 25 },
    },
  },

  amara: {
    id: 'amara',
    title: 'El ayudante de la doctora',
    signature: '— Dra. Nwosu',
    hook: 'Ha llegado una carta para la Dra. Nwosu.',
    prize: 'decidir qué hacer con Tomasz Rybak',
    region: 'afr',
    rumor: false,
    steps: [
      {
        type: 'event',
        title: 'Una carta para la doctora',
        text: 'Los del Hospital traen una carta para la Dra. Nwosu. La firma Tomasz Rybak, que fue su ayudante en el Instituto Galen. Ella lo daba por muerto. Trabaja para la Congregación en un laboratorio de Corran. Quiere hablar.',
        options: [
          { label: 'Contestar', effect: 'Una operación en el laboratorio', then: 'next' },
          { label: 'Quemar la carta', effect: 'Sin efecto', then: 'burned' },
        ],
      },
      {
        type: 'mission',
        name: 'El laboratorio de Corran',
        kind: 'hack',
        biome: 'facility',
        difficulty: 1,
        days: 8,
        briefing: 'Rybak ha dejado abierto el laboratorio. Su ordenador va con un generador. Arrancadlo y leed sus notas: sabremos si quiere salir o si es una trampa. Si falla, la Congregación tocará las campanas y vendrán todos.',
      },
      {
        type: 'event',
        title: 'Tomasz Rybak',
        text: 'Rybak quiere salir. La Congregación lo tiene estudiando cómo guiar a los zombis con sonido. A escondidas, ha avanzado con los antibióticos. Si lo sacamos, trae sus cuadernos. Si quemamos el laboratorio, la Voz pierde un año de trabajo. La Dra. Nwosu no opina.',
        options: [
          { label: 'Sacarlo de allí', effect: 'Antibióticos sin esperar al nivel 6', then: 'rescued' },
          { label: 'Quemar el laboratorio', effect: 'Los zombis retroceden (−1)', then: 'ended' },
        ],
      },
    ],
    outcomes: {
      rescued: { text: 'Rybak y la Dra. Nwosu comparten mesa y no se hablan. Los antibióticos llegan antes de tiempo.', tone: 'good', tech: 'nanoMedkit' },
      ended: { text: 'El laboratorio arde con un año de trabajo de la Congregación dentro. Los zombis retroceden. La Dra. Nwosu pasa la noche junto a la radio.', tone: 'good', doom: -1 },
      burned: { text: 'La carta arde. La Dra. Nwosu no pregunta.', tone: 'bad' },
      failure: { text: 'La Congregación sabe que la buscamos. Sus campanas atraen a Corran a los Acorazados del cuartel: uno más en cada operación de la zona.', tone: 'bad', enemy: 'mec' },
    },
  },

  bells: {
    id: 'bells',
    title: 'Las campanas de Telm',
    signature: '— Los de Telm',
    hook: 'Las campanas de Telm tocan a rebato.',
    prize: 'dos granadas aturdidoras y +10 suministros al mes',
    region: 'weu',
    rumor: true,
    steps: [
      {
        type: 'event',
        title: 'Las campanas de Telm',
        text: 'Telm es un pueblo de la sierra. Cada noche llega una horda y la gente se encierra en la iglesia. Una sirena de los Cuervos, en el valle, atrae a los zombis hacia el pueblo. Cuando no quede nadie, bajarán a saquearlo.',
        options: [
          { label: 'Callar la sirena', effect: 'Una operación de sabotaje', then: 'next' },
          { label: 'Ahora no podemos', effect: 'Telm se vacía: −10 suministros al mes', then: 'failure' },
        ],
      },
      {
        type: 'mission',
        name: 'Las campanas de Telm',
        kind: 'sabotage',
        biome: 'wilds',
        days: 8,
        briefing: 'La sirena está en una loma sobre el valle. Calladla antes de que la horda llegue al pueblo.',
      },
    ],
    outcomes: {
      success: { text: 'Telm resiste. El pueblo nos da las dos granadas aturdidoras del cuartelillo y parte de cada cosecha (+10 suministros al mes).', tone: 'good', items: { flashbang: 2 }, income: 10 },
      failure: { text: 'Las campanas de Telm ya no suenan. El pueblo está vacío (−10 suministros al mes).', tone: 'bad', income: -10 },
    },
  },

  numbers: {
    id: 'numbers',
    title: 'La estación de números',
    signature: '— N. A.',
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
        briefing: 'Una estación de números emite con el indicativo de Otto Brandt. Fue mi maestro. Lo creía muerto. En el tejado del ayuntamiento de Volkren hay un repetidor roto que recoge su señal. Reparadlo y sabremos desde dónde emite. Si falla, el ruido atraerá a lo que ande cerca.',
      },
      {
        type: 'mission',
        name: 'Otto Brandt',
        kind: 'rescue',
        biome: 'wilds',
        days: 8,
        briefing: 'Otto está vivo. Emite desde una cabaña de Volkren con una radio de válvulas. Los Cuervos también lo han oído. Llegad antes y sacadlo con vida.',
        vip: 'Otto Brandt',
      },
    ],
    outcomes: {
      success: { text: 'Otto Brandt monta su radio en Atalaya. Con él, la red llega a una zona más.', tone: 'good', contacts: 1 },
      failure: { text: 'No llegamos a tiempo. Morse firma el informe y no añade nada.', tone: 'bad' },
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
        briefing: 'Una partida de caza de los Cuervos ha asaltado el convoy en la Ruta 50. La conductora, Lola Haskins, se ha encerrado en una gasolinera. Sacadla con vida.',
        vip: 'Lola Haskins',
        reward: { credits: 40 },
      },
      {
        type: 'mission',
        name: 'Cacería en la Ruta 50',
        kind: 'elimination',
        biome: 'wilds',
        days: 6,
        briefing: 'La partida que asaltó el convoy sigue en la carretera. Mientras esté ahí no pasará ningún camión. Abatid a todos los hostiles.',
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
        briefing: 'La Red Callada son sordos y sus familias. Han sobrevivido hablando por señas, sin un ruido. Los Cuervos se han llevado a Mei Takahara, una de sus enlaces. No saben lo que vale. Sacadla con vida.',
        vip: 'Mei Takahara',
      },
      {
        type: 'mission',
        name: 'Las notas de Mei',
        kind: 'recovery',
        biome: 'city',
        days: 8,
        briefing: 'Mei anotaba por dónde pasan las hordas del Pantano Gris. Sus notas siguen en una casa que los Cuervos están saqueando. Recuperadlas y sacadlas por la zona de evacuación.',
        item: 'las notas',
      },
    ],
    outcomes: {
      success: { text: 'La Red Callada nos enseña sus señas. Las escuadras se comunican sin ruido y sabemos por dónde pasan las hordas del Pantano.', tone: 'good', xp: 15, credits: 50 },
      failure: { text: 'La Red Callada se esconde. No volveremos a saber de ella.', tone: 'bad' },
    },
  },

  mole: {
    id: 'mole',
    title: 'El topo',
    signature: '— N. A.',
    hook: 'Alguien ha saboteado el generador dos veces.',
    prize: 'encontrar al topo',
    region: 'weu',
    rumor: false,
    steps: [
      {
        type: 'event',
        title: 'El topo',
        text: 'Alguien de Atalaya pasa información a los Cuervos. Las noches de los sabotajes solo bajaron dos personas al generador: {a} y {b}. Podemos vigilar a una durante una semana.',
        options: [
          { label: 'Vigilar a {a}', effect: 'Una semana de vigilancia', then: 'next', clue: true },
          { label: 'Vigilar a {b}', effect: 'Una semana de vigilancia', then: 'next', clue: true },
        ],
      },
      {
        type: 'event',
        title: 'El topo',
        text: '{clue} Hay que decidir antes del próximo sabotaje. Si acusamos a quien no es, el topo sabrá que lo buscamos.',
        options: [
          { label: 'Acusar a {a}', effect: 'Si es el topo, los zombis retroceden (−1)', then: 'guess' },
          { label: 'Acusar a {b}', effect: 'Si es el topo, los zombis retroceden (−1)', then: 'guess' },
        ],
      },
    ],
    clues: {
      guilty: 'Vigilamos a {name}, que sale de noche hacia la antena vieja y vuelve antes del cambio de guardia.',
      innocent: 'Vigilamos a {name}, que no se mueve de su catre en toda la semana.',
    },
    outcomes: {
      success: { text: 'Confiesa {culprit}. Pasaba los turnos de guardia a los Cuervos con una radio escondida en el almacén. Sabían cuándo dormíamos y por dónde echarnos a los zombis. Sin esos avisos, los zombis retroceden.', tone: 'good', doom: -1 },
      failure: { text: 'Hemos acusado a quien no era. Esa noche desaparece {culprit}. Se lleva la caja (−60 suministros) y deja las obras saboteadas (4 días de retraso).', tone: 'bad', credits: -60, delay: 4 },
    },
  },

  mine: {
    id: 'mine',
    title: 'La mina',
    signature: '— El Sindicato de la Mina',
    hook: 'De una mina abandonada de Carvik salen gritos por la noche.',
    prize: 'un refuerzo (Granadero) y dos granadas',
    region: 'sa',
    rumor: true,
    steps: [
      {
        type: 'event',
        title: 'La mina',
        text: 'Las galerías de una mina abandonada de Carvik están llenas de zombis. Dentro hay un Chillón que los llama. Cada noche salen más hacia los pueblos.',
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
        briefing: 'El Chillón de la mina lleva semanas llamando a los zombis del valle. Abatid a todos los hostiles. Primero el Chillón.',
        leader: { template: 'sectoid', name: 'El Chillón de la mina' },
      },
    ],
    outcomes: {
      success: { text: 'La mina queda en silencio. El Sindicato nos cede a quien mejor maneja los explosivos y dos cargas de dinamita (+2 granadas).', tone: 'good', recruit: { cls: 'grenadier' }, items: { fragGrenade: 2 } },
      sealed: { text: 'El Sindicato sella la mina. Nadie sabe cuántos quedaron dentro.', tone: 'bad' },
      failure: { text: 'Los mineros abandonan Carvik (−10 suministros al mes).', tone: 'bad', income: -10 },
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
        text: 'Con el río bajo ha aparecido un camión de suministros hundido junto al puente de La Vega. Las cajas están precintadas y muchas siguen secas. Los Cuervos también lo han visto.',
        options: [
          { label: 'Llegar antes que ellos', effect: 'Una operación de recuperación', then: 'next' },
          { label: 'Dejarlo en el río', effect: 'Sin efecto', then: 'left' },
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
      success: { text: 'Las cajas traen conservas, vendas y munición (+150 suministros).', tone: 'good', credits: 150 },
      left: { text: 'El camión se queda en el fondo del río.', tone: 'bad' },
      failure: { text: 'Los Cuervos se llevan las cajas del embarcadero.', tone: 'bad' },
    },
  },
};

// ------------------------------------------------------------------- people

/** Relatives a soldier can hear named on the Cuervos' prisoner list ("Caras conocidas"). */
export const RELATIVES: { name: string; relation: string; sex: 'f' | 'm' }[] = [
  { name: 'Mariam', relation: 'su hermana', sex: 'f' },
  { name: 'Tomás', relation: 'su hermano', sex: 'm' },
  { name: 'Ewa', relation: 'su madre', sex: 'f' },
  { name: 'Andrés', relation: 'su padre', sex: 'm' },
  { name: 'Lucía', relation: 'su hermana pequeña', sex: 'f' },
  { name: 'Kenji', relation: 'su primo', sex: 'm' },
]

/** People with access to the generator ("El topo"); Simão joins them if he was trusted. */
export const SUSPECTS = ['Pavel Lanz, el electricista', 'Rosa Alves, la cocinera', 'Bruno Sauer, el de la radio'];

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
