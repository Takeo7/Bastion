/**
 * "Transmisiones": short radio conversations between the Bastion's people at
 * the story's turning points (docs/LORE.md). Text only. Each client plays a
 * scene once, when its condition first holds, and keeps it in the radio
 * archive; nothing here changes the campaign.
 *
 * `{saludo}` becomes "Comandantes Ana y Luis" (or "Comandante Ana" alone);
 * `{yo}`, "Comandante Ana" for the player reading it.
 */
import { DOOM_MAX, nextSteps, type CampaignState } from './campaign';
import { doomStatus, STORY } from './lore';
import { bastionLevel } from './progression';
import type { Slot } from './types';

/** `coro` is the nameless voice on the Congregación's frequency (the id predates the zombie lore). */
export type SpeakerId = 'morse' | 'teo' | 'nwosu' | 'simon' | 'voz' | 'halvorsen' | 'coro';

export interface Speaker {
  name: string;
  role: string;
}

export const SPEAKERS: Record<SpeakerId, Speaker> = {
  morse: { name: 'Inés Albarrán, «Morse»', role: 'Radio' },
  teo: { name: 'Teodoro Vidal, «el Relojero»', role: 'Taller' },
  nwosu: { name: 'Dra. Amara Nwosu', role: 'Enfermería' },
  simon: { name: 'Simón Ferreira', role: 'Desertor de los Cuervos' },
  voz: { name: 'Celia Arranz, «la Voz»', role: 'La Congregación' },
  halvorsen: { name: 'Ruth Halvorsen', role: 'Refugio de Altomonte' },
  coro: { name: 'Voz sin nombre', role: 'Frecuencia de la Congregación' },
};

export interface SceneLine {
  who: SpeakerId;
  text: string;
  /** The rule behind it, in small print under the line (the radio's warnings). */
  rule?: string;
  /** Said only when this holds (Simón speaks only if he was let in). */
  if?: (c: CampaignState) => boolean;
}

export type SceneId =
  | 'welcome'
  | 'council'
  | 'ch1After'
  | 'ch2Before'
  | 'ch2After'
  | 'ch3Before'
  | 'ch3After'
  | 'partitura'
  | 'simon'
  | 'ch5After'
  | 'velmar'
  | 'firstSong'
  | 'confession'
  | 'ch6After'
  | 'contranota'
  | 'voz'
  | 'eve'
  | 'endingBreak'
  | 'endingRecord'
  | 'granCoral'
  /** Built on the spot: the recap for whoever opens a campaign already under way, and the warnings in combat. */
  | 'recap'
  | 'tip';

export interface Scene {
  id: SceneId;
  title: string;
  /** Where it is heard: frequency, line or room. */
  channel: string;
  /** Unlocked once this holds; it never locks again. */
  when: (c: CampaignState) => boolean;
  lines: SceneLine[];
}

const trusted = (c: CampaignState) => c.story.simon === 'trusted';

/**
 * The Oficio branch's talents (classes.ts, «el oficio de superviviente» in
 * docs/LORE.md) and the building that teaches them, the Taller de oficios
 * (`echoes` in the code). Written as plain strings so these scenes don't
 * depend on the ids existing yet.
 */
const SONG_TALENTS = ['songBlade', 'aegis', 'echoStep', 'layOnHands', 'velmarOath', 'bolt', 'silence', 'fireball', 'highFlame', 'velmarStorm', 'huntersMark', 'spectralArrow', 'shadowStep', 'windEyes', 'lightRain', 'calmSong', 'counterSong', 'hymn', 'lightHands', 'lastVerse'];
const hasEchoes = (c: CampaignState) => c.base.plots.some((p) => (p.building as string | null) === 'echoes');
const someoneTrained = (c: CampaignState) => c.soldiers.some((s) => SONG_TALENTS.some((id) => (s.talents as Record<string, number | undefined>)[id]));
const chapterOpen = (c: CampaignState, n: number) => c.story.done >= n || c.offers.some((o) => o.story === n);

/** In story order: when several unlock at once they play in this order. */
export const SCENES: Scene[] = [
  {
    id: 'welcome',
    title: 'Primera llave',
    channel: 'Línea interna · Estación Atalaya',
    when: () => true,
    lines: [
      { who: 'morse', text: '{saludo}: bienvenidos a Atalaya. Soy Inés Albarrán. Aquí me llaman Morse, y no por mi carácter.' },
      { who: 'teo', text: 'Teodoro Vidal. Taller, generadores y La Mula, el autobús de ahí fuera. Llamadme Teo, o el Relojero, como prefiráis.' },
      { who: 'morse', text: 'Tres normas. Primera: el ruido mata. Los zombis van a lo que oyen, y un tiro se oye a dos pueblos de distancia.' },
      { who: 'teo', text: 'Segunda: aquí no se tira nada. Ni una bala, ni una venda, ni una lata abollada.' },
      { who: 'morse', text: 'Y tercera: lo importante lo decidís los dos. El año pasado, una líder sola vendió su refugio a los Cuervos por salvar el pellejo.' },
      { who: 'morse', text: 'Primer trabajo: las medicinas del consultorio del pueblo, antes de que caduquen. Empezad por ahí.' },
    ],
  },
  {
    id: 'council',
    title: 'Carta del Refugio',
    channel: 'Correo a pie · leída por Morse',
    when: (c) => c.day > 31,
    lines: [
      { who: 'halvorsen', text: 'A la Estación Atalaya: el Refugio de Altomonte ha recibido vuestros partes. Con esta carta suben los suministros.' },
      { who: 'halvorsen', text: 'Cada caja sale de la despensa de gente que también pasa hambre. Queremos resultados, no caprichos.' },
      { who: 'morse', text: 'Firma Ruth Halvorsen, portavoz del Refugio. Escribe así hasta para felicitar la Navidad.' },
      { who: 'teo', text: '¿Caprichos? Llevo dos inviernos soldando a la luz de una vela.' },
    ],
  },
  {
    id: 'ch1After',
    title: 'Campanas de fondo',
    channel: 'Sala de radio',
    when: (c) => c.story.done >= 1,
    lines: [
      { who: 'morse', text: 'Las medicinas han llegado enteras: antibióticos, vendas, insulina. Y dos cajas que caducaron el mes pasado.' },
      { who: 'teo', text: 'Caducado no es estropeado. Guárdalas, que aquí no se tira nada.' },
      { who: 'morse', text: 'Otra cosa. Anoche, en la onda media, una mujer hablaba de los «salvados». Con campanas de fondo.' },
      { who: 'teo', text: 'Apúntalo. Lo que no se entiende hoy, mañana nos mata.' },
    ],
  },
  {
    id: 'ch2Before',
    title: 'Una llamada de Ciudad Varela',
    channel: 'Onda corta · 7.040 kHz',
    when: (c) => chapterOpen(c, 2),
    lines: [
      { who: 'morse', text: 'Llamada desde el hospital de Ciudad Varela. Una doctora lleva tres semanas encerrada en la tercera planta.' },
      { who: 'teo', text: '¿En un hospital? Si en algún sitio había muertos, era ahí.' },
      { who: 'morse', text: 'Dice que trabajaba en el Instituto Galena. Y que se le acaba el agua.' },
      { who: 'teo', text: 'Del Galena, nada menos. Habrá que ir.' },
    ],
  },
  {
    id: 'ch2After',
    title: 'La doctora',
    channel: 'Línea interna · Enfermería',
    when: (c) => c.story.done >= 2,
    lines: [
      { who: 'nwosu', text: 'Amara Nwosu. Gracias por venir a buscarme. No pensé que nadie lo haría.' },
      { who: 'morse', text: '¿Es verdad que viene del Instituto Galena?' },
      { who: 'nwosu', text: 'Del laboratorio de vacunas. Cuando cerraron las puertas, salí corriendo con mis notas y nada más.' },
      { who: 'nwosu', text: 'Creo que hay una vacuna. Me faltan muestras, tiempo y una nevera que no se apague.' },
      { who: 'teo', text: 'La nevera es cosa mía. Bienvenida, doctora. Aquí todos tenemos algo que pagar.' },
    ],
  },
  {
    id: 'ch3Before',
    title: 'Un favor personal',
    channel: 'Línea interna · Taller',
    when: (c) => chapterOpen(c, 3),
    lines: [
      { who: 'teo', text: 'Comandantes, os pido un favor personal. Los Cuervos tienen una sirena en los Llanos de Ceniza.' },
      { who: 'teo', text: 'La hacen sonar junto a quien no les paga, y los zombis acuden en manada. Así vaciaron dos aldeas el invierno pasado.' },
      { who: 'teo', text: 'Quiero que esa sirena se calle. Para siempre.' },
      { who: 'morse', text: 'Y si sale mal, la tendremos sonando encima de vuestras cabezas, con medio valle acudiendo a la fiesta.' },
    ],
  },
  {
    id: 'ch3After',
    title: 'Silencio en los Llanos',
    channel: 'Onda corta · 4.625 kHz',
    when: (c) => c.story.done >= 3,
    lines: [
      { who: 'teo', text: '¿Lo oís? Nada. Ni sirena ni gemidos. Tres días sin una horda en la carretera.' },
      { who: 'morse', text: 'Teo lleva media hora riéndose solo en el taller.' },
      { who: 'teo', text: 'Porque ahora lo sé: los zombis no piensan, van al ruido. Quien manda en el ruido, manda en la horda.' },
    ],
  },
  {
    id: 'partitura',
    title: 'La Marea',
    channel: 'Sala de radio',
    when: (c) => bastionLevel(c) >= 4,
    lines: [
      { who: 'morse', text: 'Con la antena nueva ya oigo a otros enclaves. Los Camioneros, los de la Mina, unas bodegas de La Vega.' },
      { who: 'morse', text: 'Todos dicen lo mismo: algo sube desde los Llanos de Ceniza con el deshielo.' },
      { who: 'teo', text: '¿Algo?' },
      { who: 'morse', text: 'Zombis. Miles. Una horda que no se dispersa y va adonde va el ruido. La llaman la Marea.' },
      { who: 'teo', text: 'Y Atalaya está en lo alto de la sierra. Donde se acaba la cuesta.' },
      { who: 'morse', text: 'Desde hoy, el reloj de la sala de mando cuenta kilómetros: los que le faltan a la Marea para llegar.' },
    ],
  },
  {
    id: 'simon',
    title: 'Alguien en la puerta',
    channel: 'Puerta norte · interfono',
    when: (c) => c.event === 'simon' || c.story.simon !== null,
    lines: [
      { who: 'simon', text: '¿Hola? ¿Funciona esto? Me llamo Simón Ferreira. Hasta hace un mes iba con los Cuervos.' },
      { who: 'morse', text: 'Hasta hace un mes nos habrías vendido por un plato caliente.' },
      { who: 'simon', text: 'Por menos. Pero me he ido. Y sé dónde tienen a una niña a la que muerden y no se levanta.' },
      { who: 'nwosu', text: 'Si dice la verdad, esa niña vale más que todo el valle.' },
      { who: 'morse', text: 'Decidid vosotros, comandantes. Yo no le abriría ni la ventana.' },
    ],
  },
  {
    id: 'ch5After',
    title: 'La niña',
    channel: 'Línea interna · Enfermería',
    when: (c) => c.story.done >= 5,
    lines: [
      { who: 'nwosu', text: 'Está a salvo. Tiene tres mordiscos viejos en el brazo y ni una décima de fiebre.' },
      { who: 'simon', text: 'Os dije que sabía dónde estaba. Ahora, a lo mejor, me miráis de otra manera.', if: trusted },
      { who: 'nwosu', text: 'Su sangre hace algo que no había visto nunca: el virus entra y no encuentra dónde agarrarse.' },
      { who: 'morse', text: '¿Eso es una vacuna?' },
      { who: 'nwosu', text: 'Es el principio de una. Con tiempo, y con una nevera que no se apague.' },
      { who: 'teo', text: 'La cría solo pregunta cuándo puede subir a La Mula.' },
    ],
  },
  {
    id: 'velmar',
    title: 'Taller de oficios',
    channel: 'Taller de oficios',
    when: hasEchoes,
    lines: [
      { who: 'teo', text: 'Bienvenidos al Taller de oficios. Aquí no se enseña nada nuevo: lo que sabían nuestros abuelos y se nos ha olvidado.' },
      { who: 'teo', text: 'Machete, para el cuerpo a cuerpo. Ballesta, para de lejos. Fuego, para cuando ya da igual. Y enfermería de campo, para lo demás.' },
      { who: 'morse', text: '¿Y quién enseña todo eso? ¿Tú?' },
      { who: 'teo', text: 'Me crié en una aldea, Inés. Antes de los motores, cazaba conejos con una ballesta que hizo mi padre.' },
      { who: 'nwosu', text: 'Las vendas, mejor yo. Que nadie se me cosa una herida con hilo de pescar.' },
      { who: 'teo', text: 'Con lo que hay ahí fuera, no podemos permitirnos no aprender.' },
    ],
  },
  {
    id: 'firstSong',
    title: 'El primer oficio',
    channel: 'Taller de oficios',
    when: someoneTrained,
    lines: [
      { who: 'morse', text: 'Alguien se ha quedado en el Taller hasta las tantas. Teo dice que ya tiene oficio.' },
      { who: 'teo', text: 'Aprende rápido. Pero en el campo, comandantes, con cabeza: cualquier oficio hace ruido, y el ruido os delata.' },
      { who: 'morse', text: 'Teo está orgulloso, aunque no lo diga.' },
      { who: 'teo', text: 'Ni lo digas tú.' },
    ],
  },
  {
    id: 'confession',
    title: 'La noche de Galena',
    channel: 'Sala de radio · 03:10',
    when: (c) => bastionLevel(c) >= 6,
    lines: [
      { who: 'morse', text: 'Teo, la doctora dice que en el Instituto Galena había un vigilante de noche. Que fue el último en ver el sótano entero.' },
      { who: 'teo', text: 'Era yo.' },
      { who: 'morse', text: '¿Cómo que eras tú?' },
      { who: 'teo', text: 'Hacía las noches allí para estirar la pensión. A las tres y diez saltó una alarma en el sótano. Saltaba cada dos por tres, y la apagué.' },
      { who: 'teo', text: 'A las tres semanas, los muertos se levantaban en Varela. Desde entonces me despierto todas las noches a esta hora.' },
      { who: 'morse', text: 'Dos inviernos callado.' },
      { who: 'teo', text: 'Dos inviernos intentando arreglarlo, Inés.' },
    ],
  },
  {
    id: 'ch6After',
    title: 'Los cuadernos de Galena',
    channel: 'Sala de radio · cuadernos de Galena',
    when: (c) => c.story.done >= 6,
    lines: [
      { who: 'nwosu', text: 'Están todos: pruebas, muestras, fallos. Estábamos más cerca de la vacuna de lo que yo creía.' },
      { who: 'teo', text: 'Y el parte de aquella noche. «03:10, alarma en el sótano. Apagada por el vigilante.»' },
      { who: 'nwosu', text: 'Esa alarma saltaba tres veces por semana, Teo. También lo pone aquí.' },
      { who: 'morse', text: 'Silencio. Alguien ha entrado en nuestra frecuencia.' },
      { who: 'coro', text: 'Atalaya. Quemad esos cuadernos. Nadie está enfermo: solo os falta dar el último paso.' },
      { who: 'morse', text: 'La Congregación. Saben lo que hemos traído.' },
      { who: 'teo', text: 'Apágalo, Inés.' },
    ],
  },
  {
    id: 'contranota',
    title: 'La emisora',
    channel: 'Línea interna · Taller',
    when: (c) => bastionLevel(c) >= 7,
    lines: [
      { who: 'nwosu', text: 'La vacuna está cerca. Semanas, no meses. Si la Marea nos deja esas semanas.' },
      { who: 'morse', text: 'No nos las va a dejar. Cada noche la Voz habla por la radio, y la Marea gira hacia donde ella dice.' },
      { who: 'teo', text: 'Campanas y altavoces: la lleva como a un rebaño. Mientras ella hable, no se para.' },
      { who: 'morse', text: 'Es Celia Arranz. Hacía el programa de la tarde en Radio Varela. Mi madre no se lo perdía.' },
      { who: 'teo', text: 'Pues habrá que ir a su emisora y sacarla de ahí. Viva: solo ella sabe cómo se deshace la Marea.' },
    ],
  },
  {
    id: 'voz',
    title: 'Fuera de antena',
    channel: 'Atalaya · celda del sótano',
    when: (c) => c.story.done >= 7,
    lines: [
      { who: 'voz', text: 'Buenas noches, Valdesombra. Están escuchando a la Voz. No tengan miedo de los que suben: vienen a buscarlos.' },
      { who: 'morse', text: 'Ya no está en antena, señora Arranz. Aquí solo la oímos nosotros.' },
      { who: 'voz', text: 'Lo sé. Qué silencio tienen aquí arriba. ¿Ustedes viven siempre así, susurrando?' },
      { who: 'voz', text: 'La Marea no me obedece a mí. Obedece al ruido más fuerte. Mis campanas solo le enseñaban el camino.' },
      { who: 'voz', text: 'Denle un ruido más fuerte que mi voz, lejos de los vivos, y no lo apaguen hasta que se gire la última cabeza. Y luego déjenme dormir.' },
    ],
  },
  {
    id: 'eve',
    title: 'La víspera',
    channel: 'Línea interna · todas las salas',
    when: (c) => c.offers.some((o) => o.final) || c.event === 'finale' || c.ending !== null,
    lines: [
      { who: 'morse', text: 'Comandantes, la Marea ya se ve desde la presa. Llena el valle hasta donde alcanza la vista.' },
      { who: 'teo', text: 'La Mula tiene gasolina para un viaje y la sirena de los Cuervos atornillada al techo. Cuando suene, la oirán hasta los de atrás.' },
      { who: 'nwosu', text: 'Y la vacuna está lista. Pocas dosis, pero ya sé hacer más. Habrá que decidir para quién.' },
      { who: 'simon', text: 'Si me lo preguntáis a mí, los Cuervos no se la merecen. Aunque mi hermano siga con ellos.', if: trusted },
      { who: 'morse', text: 'Lo decidiréis los dos. Para eso estáis aquí.' },
    ],
  },
  {
    id: 'endingBreak',
    title: 'Para todos',
    channel: 'Onda corta · todas las frecuencias',
    when: (c) => c.ending === 'break',
    lines: [
      { who: 'morse', text: 'Aquí Atalaya, a todo el que tenga una radio encendida. Coged papel: así se hace la vacuna.' },
      { who: 'nwosu', text: 'Ya está. La receta entera, en abierto. Para los Pastores, la Mina, las Bodegas. Y para los Cuervos.' },
      { who: 'teo', text: 'Alguno la venderá a precio de oro. Otro la usará para mandar sobre los demás.' },
      { who: 'morse', text: 'Y me contestan desde sitios que ni sabía que seguían vivos. Doce frecuencias. Trece.' },
      { who: 'nwosu', text: 'Nadie se queda fuera. Con eso me basta.' },
    ],
  },
  {
    id: 'endingRecord',
    title: 'Para la Red',
    channel: 'Frecuencia de la Red',
    when: (c) => c.ending === 'record',
    lines: [
      { who: 'teo', text: 'Última caja de dosis. Sale mañana en La Mula, para las Bodegas.' },
      { who: 'morse', text: '¿Y la receta?' },
      { who: 'nwosu', text: 'En el refugio, en una caja con dos cerraduras. Hacen falta dos llaves.' },
      { who: 'morse', text: 'Esta noche han llamado desde el otro lado de la sierra. Preguntaban por una vacuna.' },
      { who: 'teo', text: '¿Y qué les has dicho?' },
      { who: 'morse', text: 'Que aquí no sabemos nada. Atalaya, cambio y corto.' },
    ],
  },
  {
    id: 'granCoral',
    title: 'Kilómetro cero',
    channel: 'Todas las frecuencias',
    when: (c) => c.outcome === 'defeat' && c.doom >= DOOM_MAX,
    lines: [
      { who: 'coro', text: 'Kilómetro cero. Las campanas ya suenan en la sierra.' },
      { who: 'coro', text: 'No tengáis miedo. Abrid las puertas. Todos seréis salvados.' },
      { who: 'morse', text: 'Aquí Atalaya. Si alguien recibe esto: lo intentamos.' },
    ],
  },
];

/** Scenes whose moment has come, in story order. */
export function unlockedScenes(c: CampaignState): Scene[] {
  return SCENES.filter((s) => s.when(c));
}

/** Scenes tied to what is on screen (Simón at the door, the end): they play as soon as they unlock. */
const URGENT: readonly SceneId[] = ['simon', 'endingBreak', 'endingRecord', 'granCoral'];

/**
 * What to play now among the unlocked scenes not seen yet: an urgent one at
 * any time; otherwise the first in story order, and only at a `moment` (back
 * at the base, or after a scan), so they never pile up.
 */
export function nextScene(c: CampaignState, seen: ReadonlySet<SceneId>, moment: boolean): Scene | null {
  const fresh = unlockedScenes(c).filter((s) => !seen.has(s.id));
  return fresh.find((s) => URGENT.includes(s.id)) ?? (moment ? (fresh[0] ?? null) : null);
}

/** The lines of a scene that apply to this campaign. */
export function sceneLines(scene: Scene, c: CampaignState): SceneLine[] {
  return scene.lines.filter((l) => !l.if || l.if(c));
}

/** Morse on the radio during a story chapter: when the squad lands and when the objective is done. */
export const STORY_RADIO: { start: string; objective: string }[] = [
  { start: 'Aquí Morse. Os oigo. El consultorio está en la plaza; las medicinas, en el armario de la enfermera.', objective: 'Medicinas en la mochila. Ahora, a casa, y sin hacer ruido.' },
  { start: 'La doctora está en la tercera planta. Los pasillos están llenos: id despacio.', objective: 'La tenéis. Sacadla antes de que el hospital entero sepa que estáis dentro.' },
  { start: 'Teo está pegado a la radio. Callad esa sirena.', objective: 'Silencio en los Llanos. Teo dice que os quiere.' },
  { start: 'Arreglad el repetidor del Pico Gris. Mientras tanto, calladitos.', objective: 'Os oigo como si estuvierais aquí. Y oigo a todo el valle.' },
  { start: 'Los Cuervos tienen a la niña bien guardada. Que nadie le toque un pelo.', objective: 'La tenéis. Fuera de ahí, y abrigadla, que hace frío.' },
  { start: 'El Instituto Galena. Los cuadernos antes que nada. Teo no se separa de la radio.', objective: 'Los cuadernos están a salvo. Teo no respira.' },
  { start: 'Está en antena ahora mismo. La oigo. Id a por ella.', objective: 'La tenéis. Viva, por favor.' },
];

// ---------------------------------------------------------------- recap

/** Played to whoever opens a campaign already under way (a partner arriving late, another browser). */
export const RECAP: Scene = { id: 'recap', title: 'Relevo', channel: 'Línea interna · Estación Atalaya', when: () => false, lines: [] };

/** Where the campaign stands, for `slot`: the day, the Bastion, the Marea, their soldiers and the first thing to do. */
export function recapLines(c: CampaignState, slot: Slot): SceneLine[] {
  const level = bastionLevel(c);
  const story = c.story.done ? `Llevamos ${c.story.done} de ${STORY.length} hojas del Expediente.` : 'El Expediente está en la primera hoja.';
  const mine = c.soldiers.filter((s) => s.owner === slot && s.alive).map((s) => s.name);
  const names = mine.length > 1 ? `${mine.slice(0, -1).join(', ')} y ${mine[mine.length - 1]}` : (mine[0] ?? 'nadie todavía');
  const first = nextSteps(c, slot)[0];
  const lines: SceneLine[] = [
    { who: 'morse', text: `{yo}: Atalaya le esperaba. Llega el día ${c.day}, con el Bastión en el nivel ${level}.` },
    { who: 'morse', text: `${doomStatus(level, c.doom)}. ${story}` },
    { who: 'teo', text: `A su mando: ${names}. Los tiene en el Cuartel.` },
  ];
  if (first) lines.push({ who: 'morse', text: `Lo más urgente: ${first.label[0]!.toLowerCase()}${first.label.slice(1)}.` });
  return lines;
}

// ------------------------------------------------------------------ tips

/** Morse's first-time warnings in combat: each plays once per browser, the first time it matters. */
export type TipId = 'concealed' | 'redTile' | 'contact' | 'shot' | 'flanked' | 'wounded' | 'command' | 'flare' | 'psionic' | 'song';

export interface RadioTip {
  who: SpeakerId;
  /** What Morse says. */
  line: string;
  /** The rule, in small print. */
  rule: string;
}

export const RADIO_TIPS: Record<TipId, RadioTip> = {
  concealed: {
    who: 'morse',
    line: 'Aún no os han oído. Que siga así.',
    rule: 'Ocultos: los zombis no os han visto ni oído. Las casillas con borde rojo os delatan, y un disparo también.',
  },
  redTile: {
    who: 'morse',
    line: 'Ahí os ven. Pensadlo dos veces.',
    rule: 'Acabar el movimiento en una casilla roja rompe la ocultación. El primer clic avisa y el segundo mueve.',
  },
  contact: {
    who: 'morse',
    line: 'Contacto. Ahora se mueven ellos también.',
    rule: 'Los vivos corren a cubrirse; los zombis vienen derechos a por vosotros. Buscad cobertura (el escudo) antes de terminar el turno.',
  },
  shot: {
    who: 'morse',
    line: 'Mirad el número antes de apretar. Y pensad en quién más lo oye.',
    rule: 'El porcentaje ya cuenta cobertura, distancia y altura; por el flanco, más acierto y más crítico. Un disparo se oye lejos: rompe la ocultación.',
  },
  flanked: {
    who: 'morse',
    line: 'Ese está al descubierto. Movedlo.',
    rule: 'Escudo rojo: sin cobertura frente a un enemigo, cualquier ataque puede ser crítico.',
  },
  wounded: {
    who: 'morse',
    line: 'Herido no es caído. Que no se nos muera.',
    rule: 'En misión, la salud solo vuelve con botiquín; en la base, las heridas tardan días en curar. Los caídos no vuelven.',
  },
  command: {
    who: 'morse',
    line: 'Dos llaves, pero un solo mando a la vez.',
    rule: 'Quien tiene el mando da las órdenes. La tecla C se lo cede al compañero.',
  },
  flare: {
    who: 'morse',
    line: 'Movimiento en esa calle. Llegan más.',
    rule: 'Refuerzos: llegan ahí al final del próximo turno enemigo. Apartaos o dejad a alguien en vigilancia.',
  },
  song: {
    who: 'morse',
    line: 'Eso se ha oído. Y no solo nosotros.',
    rule: 'Los oficios hacen ruido: usar uno rompe la ocultación de la escuadra, salvo con el Taller de oficios junto a la Estación de escucha.',
  },
  psionic: {
    who: 'morse',
    line: 'Un Chillón. Primero el Chillón.',
    rule: 'Su grito mete el pánico en el cuerpo y despierta a los muertos de alrededor. La voluntad resiste, y sube con el rango. Si cae el Chillón, quien haya perdido los nervios vuelve en sí.',
  },
};

/** The scene a warning plays as. */
export const TIP_SCENE: Scene = { id: 'tip', title: 'Aviso', channel: 'Radio de campaña', when: () => false, lines: [] };
