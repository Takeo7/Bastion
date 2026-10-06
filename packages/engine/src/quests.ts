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
  grudge: { name: 'Rencor', description: '+10 de puntería y −15 de voluntad: no perdona a los Impresos.', aim: 10, will: -15 },
  scarred: { name: 'Cicatriz de la nota', description: '−20 de voluntad: un Cantor sabe por dónde entrar.', aim: 0, will: -20 },
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
    hook: '{soldier} ha reconocido una cara entre los Impresos.',
    prize: '{relative} se une al Bastión',
    region: null,
    rumor: false,
    steps: [
      {
        type: 'event',
        title: 'Caras conocidas',
        text: '{soldier} ha visto una cara entre los Impresos: la de {relation}, {relative}. Si hay un Impreso con su cara, el original duerme en algún Archivo. Buscarlo costará días y soldados.',
        options: [
          { label: 'Buscar el Archivo', effect: 'Tres operaciones para encontrar a {relative}', then: 'next' },
          { label: 'No podemos permitírnoslo', effect: '{soldier} gana el rasgo «Rencor»', then: 'refused' },
        ],
      },
      {
        type: 'mission',
        name: 'Registro de patrón',
        kind: 'recovery',
        biome: 'city',
        days: 8,
        briefing: 'El registro de patrón de {relative} está en un control de la Armonía. Con él sabremos en qué Archivo duerme. Recuperadlo y sacadlo por la zona de evacuación.',
        item: 'el registro de patrón',
        reward: { credits: 40 },
      },
      {
        type: 'mission',
        name: 'Índice de Archivos',
        kind: 'hack',
        biome: 'city',
        days: 8,
        briefing: 'El índice de Archivos de la región está en un terminal de la prefectura. Si el Grillo lo escucha, sabremos dónde está {relative}. Si falla el pirateo, saltará la alarma.',
        reward: { credits: 40 },
      },
      {
        type: 'mission',
        name: 'Despertar a {relative}',
        kind: 'rescue',
        biome: 'facility',
        difficulty: 1,
        days: 8,
        briefing: '{relative} duerme en un Archivo de la región. Llegad hasta su cápsula y sacad a {relative} con vida.',
        vip: '{relative}',
      },
    ],
    outcomes: {
      success: { text: '{relative} despierta en la enfermería del Bastión y pide un fusil, como {soldier}.', tone: 'good', recruit: { name: '{relative}' }, retry: 40 },
      refused: { text: '{soldier} no ha dicho nada. Desde entonces dispara antes de preguntar.', tone: 'bad', trait: 'grudge', retry: 30 },
      failure: { text: 'No hemos llegado a tiempo. {soldier} no habla de ello, pero ahora dispara antes de preguntar.', tone: 'bad', trait: 'grudge', retry: 30 },
    },
  },

  ibarra: {
    id: 'ibarra',
    title: 'La comandante Ibarra',
    signature: '— I. A.',
    hook: 'Morse ha oído el nombre de Elena Ibarra en una frecuencia de la Armonía.',
    prize: 'decidir qué hacer con Ibarra',
    region: 'weu',
    rumor: false,
    steps: [
      {
        type: 'mission',
        name: 'El diario de Moncayo',
        kind: 'recovery',
        biome: 'city',
        days: 10,
        briefing: 'El diario de la comandante Ibarra sigue en las ruinas del Puesto Moncayo. Dice adónde se la llevaron después. Recuperadlo y sacadlo por la zona de evacuación.',
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
        briefing: 'Ibarra manda ahora un puesto de la Armonía, con la nota detrás de la oreja. Abatid a todos los hostiles. A ella, si se puede, la quiero viva.',
        leader: { template: 'officer', name: 'Elena Ibarra' },
      },
      {
        type: 'event',
        title: 'Elena Ibarra',
        text: 'La escuadra encuentra a Ibarra herida entre los escombros. Todavía lleva la nota. Nos mira como si no nos conociera.',
        options: [
          { label: 'Llevarla al Bastión y desafinarla', effect: 'Se une como sargento, con el rasgo «Cicatriz de la nota»', then: 'captured' },
          { label: 'Acabar con esto', effect: '+150 créditos del alijo de Moncayo', then: 'killed' },
        ],
      },
    ],
    outcomes: {
      captured: { text: 'Ibarra ha vuelto. Cuesta mirarla, pero sabe mandar.', tone: 'good', recruit: { name: 'Elena Ibarra', cls: 'sharpshooter', rank: 3, trait: 'scarred' } },
      killed: { text: 'Ibarra ya no entregará a nadie más. El alijo de Moncayo paga la munición de un mes.', tone: 'good', credits: 150 },
      failure: { text: 'Ibarra se nos ha escapado. Volverá a aparecer.', tone: 'bad', retry: 25 },
    },
  },

  amara: {
    id: 'amara',
    title: 'La otra Amara',
    signature: '— Dra. Nwosu',
    hook: 'Ha llegado una carta para la Dra. Nwosu.',
    prize: 'decidir qué hacer con la otra Amara',
    region: 'afr',
    rumor: false,
    steps: [
      {
        type: 'event',
        title: 'Una carta de Amara',
        text: 'Las Caravanas traen una carta firmada «Amara». La letra es la de la Dra. Nwosu, pero ella no la ha escrito. La otra Amara dirige ahora la Imprenta 14 y quiere hablar.',
        options: [
          { label: 'Contestar', effect: 'Una operación en la Imprenta 14', then: 'next' },
          { label: 'Quemar la carta', effect: 'Sin efecto', then: 'burned' },
        ],
      },
      {
        type: 'mission',
        name: 'La otra Amara',
        kind: 'hack',
        biome: 'facility',
        difficulty: 1,
        days: 8,
        briefing: 'La otra Amara ha dejado un terminal abierto en la Imprenta 14. Si el Grillo entra, sabremos si quiere desertar o tendernos una trampa. Si falla el pirateo, saltará la alarma.',
      },
      {
        type: 'event',
        title: 'La otra Amara',
        text: 'Quiere salir. Dice que recuerda todo lo que recuerda la Dra. Nwosu, y que no sabe cuál de las dos es la original. La Dra. Nwosu tampoco.',
        options: [
          { label: 'Sacarla de la Imprenta', effect: 'Teselas médicas sin esperar al nivel 6', then: 'rescued' },
          { label: 'Acabar con la copia', effect: 'El enemigo retrocede (−1)', then: 'ended' },
        ],
      },
    ],
    outcomes: {
      rescued: { text: 'Las dos Amaras trabajan en la misma mesa y no se dirigen la palabra. Las teselas médicas llegan antes de tiempo.', tone: 'good', tech: 'nanoMedkit' },
      ended: { text: 'La Imprenta 14 se queda sin directora y el enemigo retrocede. La Dra. Nwosu pasa la noche en la sala de escucha.', tone: 'good', doom: -1 },
      burned: { text: 'La carta arde. La Dra. Nwosu no pregunta.', tone: 'bad' },
      failure: { text: 'La otra Amara sabe que la buscamos. La Imprenta 14 se llena de Gólems: uno más en cada operación de África.', tone: 'bad', enemy: 'mec' },
    },
  },

  bells: {
    id: 'bells',
    title: 'Las campanas de Albarracín',
    signature: '— Los Campaneros',
    hook: 'Las campanas de Albarracín tocan a rebato.',
    prize: 'dos granadas aturdidoras y +10 créditos al mes',
    region: 'weu',
    rumor: true,
    steps: [
      {
        type: 'event',
        title: 'Las campanas de Albarracín',
        text: 'Los Campaneros de Albarracín tocan a rebato cada noche: el bronce desafina a los Impresos. La Armonía ha levantado un retransmisor en el valle para ahogar las campanas.',
        options: [
          { label: 'Derribar el retransmisor', effect: 'Una operación de sabotaje', then: 'next' },
          { label: 'Ahora no podemos', effect: 'El pueblo se afina: −10 créditos al mes', then: 'failure' },
        ],
      },
      {
        type: 'mission',
        name: 'Las campanas de Albarracín',
        kind: 'sabotage',
        biome: 'wilds',
        days: 8,
        briefing: 'Derribad el retransmisor del valle antes de que termine la transmisión. Las campanas harán el resto.',
      },
    ],
    outcomes: {
      success: { text: 'Albarracín toca las campanas por vosotros. Los Campaneros mandan dos granadas aturdidoras y un donativo cada mes (+10 créditos).', tone: 'good', items: { flashbang: 2 }, income: 10 },
      failure: { text: 'Las campanas de Albarracín se han callado. El pueblo está afinado (−10 créditos al mes).', tone: 'bad', income: -10 },
    },
  },

  numbers: {
    id: 'numbers',
    title: 'La estación de números',
    signature: '— I. A.',
    hook: 'Morse ha oído un indicativo que no oía desde 2031.',
    prize: 'una región contactable más',
    region: 'eeu',
    rumor: false,
    steps: [
      {
        type: 'mission',
        name: 'La estación de números',
        kind: 'hack',
        biome: 'city',
        days: 10,
        briefing: 'Una estación de números emite con el indicativo de mi maestro, Ramiro Ostalé. Lo creía muerto. El terminal de la prefectura sabe desde dónde emite. Si falla el pirateo, saltará la alarma.',
      },
      {
        type: 'mission',
        name: 'Ramiro Ostalé',
        kind: 'rescue',
        biome: 'wilds',
        days: 8,
        briefing: 'Ramiro está vivo, en una cabaña de los Cárpatos, con una radio de válvulas. La Armonía también lo ha oído. Llegad antes que ellos y sacadlo con vida.',
        vip: 'Ramiro Ostalé',
      },
    ],
    outcomes: {
      success: { text: 'Ramiro Ostalé monta su radio en el Bastión. Con él, la red llega a una región más.', tone: 'good', contacts: 1 },
      failure: { text: 'No llegamos a tiempo. Morse ha escrito algo en el margen del informe y lo ha tachado todo menos una palabra: «gracias».', tone: 'bad' },
    },
  },

  route50: {
    id: 'route50',
    title: 'Ruta 50',
    signature: '— Los Camioneros del Silencio',
    hook: 'Los Camioneros del Silencio han perdido un convoy.',
    prize: '+15 créditos al mes',
    region: 'na',
    rumor: true,
    steps: [
      {
        type: 'mission',
        name: 'Ruta 50',
        kind: 'rescue',
        biome: 'wilds',
        days: 8,
        briefing: 'Hemos perdido un convoy en la Ruta 50. La conductora, Lola Haskins, se esconde en una gasolinera. Llegad hasta ella y sacadla con vida.',
        vip: 'Lola Haskins',
        reward: { credits: 40 },
      },
      {
        type: 'mission',
        name: 'Cacería en la Ruta 50',
        kind: 'elimination',
        biome: 'wilds',
        days: 6,
        briefing: 'La Cacería que siguió a Lola sigue en la carretera. Mientras exista, ningún convoy pasará. Abatid a todos los hostiles.',
      },
    ],
    outcomes: {
      success: { text: 'Los Camioneros vuelven a la Ruta 50 y pagan peaje al Bastión (+15 créditos al mes).', tone: 'good', income: 15 },
      failure: { text: 'Los Camioneros dejan de pasar por la región (−10 créditos al mes).', tone: 'bad', income: -10 },
    },
  },

  hands: {
    id: 'hands',
    title: 'Manos',
    signature: '— La Red Callada',
    hook: 'La Red Callada ha perdido a una de sus enlaces.',
    prize: '+15 de experiencia y 50 créditos',
    region: 'eas',
    rumor: true,
    steps: [
      {
        type: 'mission',
        name: 'Manos',
        kind: 'rescue',
        biome: 'city',
        days: 8,
        briefing: 'La Armonía retiene a Mei Takahara, enlace de la Red Callada, sin saber qué es: no ha dicho una palabra, porque no las necesita. Llegad hasta ella y sacadla con vida.',
        vip: 'Mei Takahara',
      },
      {
        type: 'mission',
        name: 'Las notas de Mei',
        kind: 'recovery',
        biome: 'city',
        days: 8,
        briefing: 'Mei apuntaba en papel los turnos de los Afinados de la capital. Sus notas siguen en un piso franco que la Armonía está registrando. Recuperadlas y sacadlas por la zona de evacuación.',
        item: 'las notas',
      },
    ],
    outcomes: {
      success: { text: 'La Red Callada nos enseña sus señas. Por primera vez sabemos cómo se mueven los Afinados de la capital.', tone: 'good', xp: 15, credits: 50 },
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
        text: 'Alguien del Bastión pasa información a un Órgano. Solo dos personas bajaron al generador las noches de los sabotajes: {a} y {b}. Podemos vigilar a una de ellas una semana.',
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
          { label: 'Acusar a {a}', effect: 'Si es el topo, el enemigo retrocede (−1)', then: 'guess' },
          { label: 'Acusar a {b}', effect: 'Si es el topo, el enemigo retrocede (−1)', then: 'guess' },
        ],
      },
    ],
    clues: {
      guilty: '{name} sale de noche hacia la antena vieja y vuelve sin hacer ruido.',
      innocent: '{name} no se mueve de su catre en toda la semana.',
    },
    outcomes: {
      success: { text: '{culprit} confiesa: pasaba los turnos de guardia a un Órgano por una radio escondida en el almacén. Sin sus informes, el enemigo retrocede.', tone: 'good', doom: -1 },
      failure: { text: 'Hemos acusado a quien no era. Esa noche, {culprit} desaparece con la caja de créditos (−60) y deja las obras saboteadas.', tone: 'bad', credits: -60, delay: 4 },
    },
  },

  mine: {
    id: 'mine',
    title: 'La mina que canta',
    signature: '— El Sindicato de la Cordillera',
    hook: 'Una mina abandonada de los Andes canta por las noches.',
    prize: 'una granadera y dos granadas',
    region: 'sa',
    rumor: true,
    steps: [
      {
        type: 'event',
        title: 'La mina que canta',
        text: 'Los mineros del Sindicato de la Cordillera oyen cantar una mina abandonada por las noches. Dicen que dentro hay un Cantor y que los muertos se levantan.',
        options: [
          { label: 'Bajar a la mina', effect: 'Una operación contra el Cantor', then: 'next' },
          { label: 'Sellarla con dinamita', effect: 'Sin efecto', then: 'sealed' },
        ],
      },
      {
        type: 'mission',
        name: 'La mina que canta',
        kind: 'elimination',
        biome: 'wilds',
        difficulty: 1,
        days: 8,
        briefing: 'El Cantor de la mina lleva semanas levantando a los muertos del valle. Bajad y abatid a todos los hostiles. Primero el Cantor.',
        leader: { template: 'sectoid', name: 'El Cantor de la mina' },
      },
    ],
    outcomes: {
      success: { text: 'La mina calla. El Sindicato nos manda a su mejor barrenera y una caja de dinamita.', tone: 'good', recruit: { cls: 'grenadier' }, items: { fragGrenade: 2 } },
      sealed: { text: 'El Sindicato sella la mina. Nadie sabe qué quedó dentro.', tone: 'bad' },
      failure: { text: 'Los mineros abandonan el valle (−10 créditos al mes).', tone: 'bad', income: -10 },
    },
  },

  sunken: {
    id: 'sunken',
    title: 'La Imprenta hundida',
    signature: '— Las flotas pesqueras',
    hook: 'Las flotas pesqueras han encontrado algo en el fondo del mar.',
    prize: '150 créditos',
    region: 'oce',
    rumor: true,
    steps: [
      {
        type: 'event',
        title: 'La Imprenta hundida',
        text: 'Las flotas pesqueras han encontrado una Imprenta hundida cerca de la costa. La sal la ha corroído: sus teselas están sueltas y valen una fortuna. La Armonía también va a por ellas.',
        options: [
          { label: 'Llegar antes que ellos', effect: 'Una operación de recuperación', then: 'next' },
          { label: 'Dejársela al mar', effect: 'Sin efecto', then: 'left' },
        ],
      },
      {
        type: 'mission',
        name: 'La Imprenta hundida',
        kind: 'recovery',
        biome: 'wilds',
        days: 6,
        briefing: 'Las flotas han subido las teselas a un muelle. Recuperadlas y sacadlas por la zona de evacuación antes de que llegue la Armonía.',
        item: 'las teselas',
      },
    ],
    outcomes: {
      success: { text: 'Las teselas de la Imprenta hundida pagan un mes entero de munición (+150 créditos).', tone: 'good', credits: 150 },
      left: { text: 'La Imprenta hundida se queda en el fondo.', tone: 'bad' },
      failure: { text: 'La Armonía se lleva las teselas del muelle.', tone: 'bad' },
    },
  },
};

// ------------------------------------------------------------------- people

/** Relatives a soldier can recognise among the Impresos ("Caras conocidas"). */
export const RELATIVES: { name: string; relation: string }[] = [
  { name: 'Marta', relation: 'su hermana' },
  { name: 'Tomás', relation: 'su hermano' },
  { name: 'Carmen', relation: 'su madre' },
  { name: 'Andrés', relation: 'su padre' },
  { name: 'Lucía', relation: 'su hermana pequeña' },
  { name: 'Joaquín', relation: 'su primo' },
];

/** People with access to the generator ("El topo"); Simón joins them if he was trusted. */
export const SUSPECTS = ['Paco Lanuza, el mecánico', 'Rosa Aldea, la cocinera', 'Bruno Sanz, el de la radio'];

/** What rumours are about. */
export const RUMOR_LINES = [
  'luces en una mina cerrada',
  'un convoy que no llegó',
  'una emisora que repite un nombre',
  'Impresos que caminan hacia el mar',
  'un Afinado que pregunta por el Bastión',
  'un almacén de la Armonía sin vigilancia',
  'campanas que suenan a deshora',
  'una cabaña con la luz encendida en plena noche',
];

/** "{relative} despierta…" with the quest's names. */
export function fill(text: string, names: Record<string, string>): string {
  return text.replace(/\{(\w+)\}/g, (match, key: string) => names[key] ?? match);
}
