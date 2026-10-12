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
import { doomName, PARTITURA_LEVEL, STORY } from './lore';
import { bastionLevel } from './progression';
import type { Slot } from './types';

export type SpeakerId = 'morse' | 'teo' | 'nwosu' | 'simon' | 'voz' | 'halvorsen' | 'coro';

export interface Speaker {
  name: string;
  role: string;
}

export const SPEAKERS: Record<SpeakerId, Speaker> = {
  morse: { name: 'Inés Albarrán, «Morse»', role: 'Comunicaciones' },
  teo: { name: 'Teodoro Vidal, «el Relojero»', role: 'Armería' },
  nwosu: { name: 'Dra. Amara Nwosu', role: 'Ciencia' },
  simon: { name: 'Simón Ferreira', role: 'Desafinado' },
  voz: { name: 'Celia Arranz, «la Voz»', role: 'La Armonía' },
  halvorsen: { name: 'Ruth Halvorsen', role: 'Consejera' },
  coro: { name: 'El Coro', role: 'Señal sin origen' },
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
 * The Canto branch's talents (classes.ts, "El Canto de Velmar" in docs/LORE.md)
 * and the building that teaches them. Written as plain strings so these scenes
 * don't depend on the ids existing yet.
 */
const SONG_TALENTS = ['songBlade', 'aegis', 'echoStep', 'layOnHands', 'velmarOath', 'bolt', 'silence', 'fireball', 'highFlame', 'velmarStorm', 'huntersMark', 'spectralArrow', 'shadowStep', 'windEyes', 'lightRain', 'calmSong', 'counterSong', 'hymn', 'lightHands', 'lastVerse'];
const hasEchoes = (c: CampaignState) => c.base.plots.some((p) => (p.building as string | null) === 'echoes');
const someoneSings = (c: CampaignState) => c.soldiers.some((s) => SONG_TALENTS.some((id) => (s.talents as Record<string, number | undefined>)[id]));
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
      { who: 'teo', text: 'Teodoro Vidal. Armería, generadores y todo lo que haga ruido. Llamadme Teo, o el Relojero, como prefiráis.' },
      { who: 'morse', text: 'Tres normas. Primera: lo que tiene chip, escucha. Aquí no entra ni un reloj digital.' },
      { who: 'teo', text: 'Segunda: todo va en papel. Si lo escribís en otro sitio, lo lee el Coro antes que vosotros.' },
      { who: 'morse', text: 'Y tercera: lo importante lo decidís los dos. Una cabeza sola se puede cantar. Lo aprendimos en Moncayo.' },
      { who: 'morse', text: 'El Consejo quiere los libros de claves del Puesto Moncayo. Siguen en la ciudad. Empezad por ahí.' },
    ],
  },
  {
    id: 'council',
    title: 'Carta del Consejo',
    channel: 'Correo a pie · leída por Morse',
    when: (c) => c.day > 31,
    lines: [
      { who: 'halvorsen', text: 'Al Bastión de Atalaya: el Consejo ha recibido vuestros informes. Los créditos van con esta carta.' },
      { who: 'halvorsen', text: 'Recordad que cada vale sale de una región que se juega el cuello por vosotros. No los gastéis en caprichos.' },
      { who: 'morse', text: 'Firma la consejera Halvorsen. Escribe así hasta para felicitar la Navidad.' },
      { who: 'teo', text: '¿Caprichos? Llevo dos años soldando a la luz de una vela.' },
    ],
  },
  {
    id: 'ch1After',
    title: 'Lo que dice la Armonía',
    channel: 'Onda corta · 4.625 kHz',
    when: (c) => c.story.done >= 1,
    lines: [
      { who: 'morse', text: 'Los libros de claves están enteros. Con ellos ya leo lo que la Armonía se dice por radio.' },
      { who: 'teo', text: '¿Y qué se dice?' },
      { who: 'morse', text: 'Turnos de guardia, partes de abastecimiento y una palabra que repiten mucho y no entiendo: «compás».' },
      { who: 'teo', text: 'Apúntala. Lo que no se entiende hoy, mañana nos mata.' },
    ],
  },
  {
    id: 'ch2Before',
    title: 'Una llamada de las Caravanas',
    channel: 'Onda corta · 7.040 kHz',
    when: (c) => chapterOpen(c, 2),
    lines: [
      { who: 'morse', text: 'Llamada de las Caravanas. Una científica se ha escapado de la Imprenta 14 y se esconde dentro, entre las máquinas.' },
      { who: 'teo', text: '¿Dentro? Pues sí que tiene valor.' },
      { who: 'morse', text: 'O miedo. Dice que sabe cómo se hace un Impreso, y que la buscan por eso.' },
    ],
  },
  {
    id: 'ch2After',
    title: 'La doctora',
    channel: 'Línea interna · Enfermería',
    when: (c) => c.story.done >= 2,
    lines: [
      { who: 'nwosu', text: 'Amara Nwosu. Gracias por venir a buscarme. No pensé que nadie lo haría.' },
      { who: 'morse', text: 'Dicen que sabe cómo se hace un Impreso.' },
      { who: 'nwosu', text: 'Lo sé porque los hacía. Escaneaba a la gente que traían los Recolectores. Me decía a mí misma que era para estudiarlos.' },
      { who: 'nwosu', text: 'Traedme los cuerpos que podáis. Sé qué teselas valen algo. Es lo mínimo que puedo hacer.' },
      { who: 'teo', text: 'Bienvenida, doctora. Aquí todos tenemos algo que pagar.' },
    ],
  },
  {
    id: 'ch3Before',
    title: 'Un favor personal',
    channel: 'Línea interna · Armería',
    when: (c) => chapterOpen(c, 3),
    lines: [
      { who: 'teo', text: 'Comandantes, os pido un favor personal. Hay un retransmisor en el valle.' },
      { who: 'teo', text: 'Quiero ver qué pasa cuando cae. Si tengo razón, los Impresos no son soldados: son marionetas.' },
      { who: 'morse', text: 'Y si no tiene razón, nos los encontraremos a todos despiertos y de mal humor.' },
    ],
  },
  {
    id: 'ch3After',
    title: 'Marionetas',
    channel: 'Onda corta · 4.625 kHz',
    when: (c) => c.story.done >= 3,
    lines: [
      { who: 'teo', text: '¿Lo habéis visto? Se han quedado quietos, con el fusil a medio levantar. ¡Quietos!' },
      { who: 'morse', text: 'Teo lleva media hora riéndose solo en la Armería.' },
      { who: 'teo', text: 'Porque ahora sé dónde cortar los hilos.' },
    ],
  },
  {
    id: 'partitura',
    title: 'La Partitura',
    channel: 'Sala de escucha',
    when: (c) => bastionLevel(c) >= 4,
    lines: [
      { who: 'morse', text: 'Ya sé qué es «compás». Hemos descifrado una página entera.' },
      { who: 'teo', text: 'Los Órganos no fabrican nada. Escriben. Cada uno añade compases a una partitura.' },
      { who: 'morse', text: 'Doce compases. Cuando estén todos, el Coro se imprime entero con la materia del planeta.' },
      { who: 'teo', text: 'Con la de los Pirineos también. Con la nuestra.' },
      { who: 'morse', text: 'Desde hoy, el reloj de la sala de mando se llama así: la Partitura.' },
    ],
  },
  {
    id: 'simon',
    title: 'Alguien en la puerta',
    channel: 'Puerta norte · interfono',
    when: (c) => c.event === 'simon' || c.story.simon !== null,
    lines: [
      { who: 'simon', text: '¿Hola? ¿Funciona esto? Me llamo Simón Ferreira. Hasta hace un mes llevaba la nota.' },
      { who: 'morse', text: 'Hasta hace un mes nos habrías vendido por un plato caliente.' },
      { who: 'simon', text: 'Por menos. Pero ya no la oigo. Y sé cómo se abre un Archivo desde dentro.' },
      { who: 'nwosu', text: 'Si dice la verdad, en ese Archivo hay gente dormida. Gente que se puede despertar.' },
      { who: 'morse', text: 'Decidid vosotros, comandantes. Yo no le abriría ni la ventana.' },
    ],
  },
  {
    id: 'ch5After',
    title: 'Los originales',
    channel: 'Línea interna · Enfermería',
    when: (c) => c.story.done >= 5,
    lines: [
      { who: 'nwosu', text: 'Están despertando. Despacio, como quien sale de una fiebre.' },
      { who: 'simon', text: 'Os dije que lo abriría. No esperaba que me sacarais a mí también.', if: trusted },
      { who: 'nwosu', text: 'Una de ellas me ha reconocido. Fui yo quien la escaneó.' },
      { who: 'morse', text: '¿Y qué le ha dicho?' },
      { who: 'nwosu', text: 'Que le diera un fusil.' },
    ],
  },
  {
    id: 'velmar',
    title: 'Velmar',
    channel: 'Cámara de Ecos',
    when: hasEchoes,
    lines: [
      { who: 'nwosu', text: 'Las cintas del Archivo no son solo de gente de aquí. Hay voces de otro sitio. De Velmar.' },
      { who: 'teo', text: 'Un mundo que el Coro se cantó hace mil años. Su gente aprendió a usar el canto antes de desaparecer. Lo llamaban la Lengua.' },
      { who: 'nwosu', text: 'Sus magos, sus caballeros, sus cazadores. Está todo en la cinta: cómo respiraban, cómo cantaban.' },
      { who: 'morse', text: '¿Y queréis que nuestros soldados canten como ellos? ¿En el idioma del Coro?' },
      { who: 'teo', text: 'Con válvulas no nos oye. Y con lo que hay ahí fuera, no podemos permitirnos no aprender.' },
    ],
  },
  {
    id: 'firstSong',
    title: 'La primera canción',
    channel: 'Sala de escucha',
    when: someoneSings,
    lines: [
      { who: 'morse', text: 'Esta noche alguien ha cantado en la Cámara de Ecos. La aguja de la sala de escucha ni se ha movido.' },
      { who: 'teo', text: 'Lo que se canta entre válvulas se queda en el Bastión. En el campo, comandantes, eso se oye. Cantad con cabeza.' },
      { who: 'coro', text: 'Conocemos esa canción.' },
    ],
  },
  {
    id: 'confession',
    title: '1989',
    channel: 'Sala de escucha · 03:10',
    when: (c) => bastionLevel(c) >= 6,
    lines: [
      { who: 'morse', text: 'Teo, los Astrónomos nos han radiado lo que oyeron antes del Día 0. Dicen que el Coro llevaba siglos esperando una respuesta.' },
      { who: 'teo', text: 'Ya lo sé.' },
      { who: 'morse', text: '¿Cómo que ya lo sabes?' },
      { who: 'teo', text: 'En 1989 yo trabajaba aquí. Una noche entró una señal de veintiún años luz. Había un protocolo de prueba y lo seguí: contesté.' },
      { who: 'teo', text: 'No lo supe hasta el Tono. Reconocí la cadencia. Era la misma.' },
      { who: 'morse', text: 'Tres años callado.' },
      { who: 'teo', text: 'Tres años intentando arreglarlo, Inés.' },
    ],
  },
  {
    id: 'ch6After',
    title: 'Lo que oyeron',
    channel: 'Sala de escucha · cintas del desierto',
    when: (c) => c.story.done >= 6,
    lines: [
      { who: 'teo', text: 'Las cintas del desierto. Escuchad esto.' },
      { who: 'coro', text: '¿Hay alguien?' },
      { who: 'morse', text: 'Lo dice en castellano.' },
      { who: 'teo', text: 'Lo dice en el idioma de quien lo escucha. En 1989 también lo oí en el mío.' },
      { who: 'coro', text: 'Os oímos. No hace falta tener miedo.' },
      { who: 'morse', text: 'Apágalo, Teo.' },
    ],
  },
  {
    id: 'contranota',
    title: 'La Contranota',
    channel: 'Línea interna · Armería',
    when: (c) => bastionLevel(c) >= 7,
    lines: [
      { who: 'teo', text: 'La Contranota está escrita. Si el Coro es música, se le puede obligar a cantarse entero, fuera de los chips, de una vez.' },
      { who: 'morse', text: 'Pero hay que tocarla en el centro, y no sabemos dónde está.' },
      { who: 'teo', text: 'Lo sabe una persona: Celia Arranz.' },
      { who: 'morse', text: '¿La de «La Hora Armónica»? Mi madre la veía todas las tardes.' },
      { who: 'teo', text: 'Pues habrá que ir a por ella. Viva.' },
    ],
  },
  {
    id: 'voz',
    title: 'Fuera de antena',
    channel: 'Sala de interrogatorios',
    when: (c) => c.story.done >= 7,
    lines: [
      { who: 'voz', text: 'Buenas noches. Están escuchando «La Hora Armónica». Recuerden: el silencio es convivencia.' },
      { who: 'morse', text: 'Ya no está en antena, señora Arranz. Le hemos quitado la nota.' },
      { who: 'voz', text: 'Lo sé. Hay mucho ruido aquí dentro. ¿Ustedes viven siempre así?' },
      { who: 'voz', text: 'La frecuencia que buscan es la que sonaba debajo de mi voz cada noche. Apunten. Y luego déjenme dormir.' },
    ],
  },
  {
    id: 'eve',
    title: 'La víspera',
    channel: 'Línea interna · todas las salas',
    when: (c) => c.offers.some((o) => o.final) || c.event === 'finale' || c.ending !== null,
    lines: [
      { who: 'morse', text: 'Comandantes, el Diapasón está localizado. La Mula tiene combustible para un viaje.' },
      { who: 'teo', text: 'La Contranota va en una cinta, en una maleta, en la Mula. Todo de válvulas. No oirá nada hasta que suene.' },
      { who: 'nwosu', text: 'Cuando suene, el Coro saldrá de los chips. Habrá que decidir qué hacemos con él.' },
      { who: 'simon', text: 'Si me lo preguntáis a mí: no le pidáis perdón.', if: trusted },
      { who: 'morse', text: 'Lo decidiréis los dos. Para eso estáis aquí.' },
    ],
  },
  {
    id: 'endingBreak',
    title: 'Silencio',
    channel: 'Onda corta · todas las frecuencias',
    when: (c) => c.ending === 'break',
    lines: [
      { who: 'morse', text: 'Aquí Atalaya. ¿Me oye alguien?' },
      { who: 'morse', text: 'Silencio en todas las frecuencias. Silencio de verdad, del que no escucha.' },
      { who: 'teo', text: 'Se ha ido. Todo. No queda ni el eco.' },
      { who: 'nwosu', text: 'Los Impresos se han deshecho. Todos. También los que llevaban caras que conocíamos.' },
    ],
  },
  {
    id: 'endingRecord',
    title: 'Bobina 214',
    channel: 'Cámara acorazada · Atalaya',
    when: (c) => c.ending === 'record',
    lines: [
      { who: 'teo', text: 'Bobina doscientos catorce. Es la última.' },
      { who: 'morse', text: '¿Está todo dentro?' },
      { who: 'teo', text: 'Todo lo que era el Coro. Sin chips donde vivir y sin nadie que lo escuche.' },
      { who: 'coro', text: 'Os oímos.' },
      { who: 'teo', text: 'Ya no. Cerrad la puerta. Hacen falta dos llaves.' },
    ],
  },
  {
    id: 'granCoral',
    title: 'El Gran Coral',
    channel: 'Todas las frecuencias',
    when: (c) => c.outcome === 'defeat' && c.doom >= DOOM_MAX,
    lines: [
      { who: 'coro', text: 'Duodécimo compás.' },
      { who: 'coro', text: 'Os oímos. Os hemos oído siempre. Ya no hace falta tener miedo.' },
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
  { start: 'Aquí Morse. Os oigo. La caja fuerte está en la oficina de correos.', objective: 'Ya tenéis los libros. Ahora, a casa.' },
  { start: 'La doctora se esconde entre las máquinas. No dejéis que los Recolectores lleguen antes.', objective: 'La tenéis. Sacadla de ahí.' },
  { start: 'Teo está pegado a la radio. Tiradle su retransmisor.', objective: 'Se han quedado quietos. Teo dice que os quiere.' },
  { start: 'El Grillo escucha; vosotros, calladitos.', objective: 'Ya está. La Partitura entera, en papel.' },
  { start: 'El Archivo está abierto. Daos prisa: no sabemos cuánto aguanta.', objective: 'Tenéis a quien buscabais. Fuera.' },
  { start: 'El observatorio arde por el ala norte. Las cintas antes que nada.', objective: 'Las cintas están a salvo. Teo no respira.' },
  { start: 'Está en antena ahora mismo. La oigo. Id a por ella.', objective: 'La tenéis. Viva, por favor.' },
];

// ---------------------------------------------------------------- recap

/** Played to whoever opens a campaign already under way (a partner arriving late, another browser). */
export const RECAP: Scene = { id: 'recap', title: 'Relevo', channel: 'Línea interna · Estación Atalaya', when: () => false, lines: [] };

/** Where the campaign stands, for `slot`: the day, the Bastion, the enemy, their soldiers and the first thing to do. */
export function recapLines(c: CampaignState, slot: Slot): SceneLine[] {
  const level = bastionLevel(c);
  const enemy = `${doomName(level)}: ${c.doom} de ${DOOM_MAX}${level >= PARTITURA_LEVEL ? ' compases' : ''}`;
  const story = c.story.done ? `Llevamos ${c.story.done} de ${STORY.length} hojas del Expediente.` : 'El Expediente está en la primera hoja.';
  const mine = c.soldiers.filter((s) => s.owner === slot && s.alive).map((s) => s.name);
  const names = mine.length > 1 ? `${mine.slice(0, -1).join(', ')} y ${mine[mine.length - 1]}` : (mine[0] ?? 'nadie todavía');
  const first = nextSteps(c, slot)[0];
  const lines: SceneLine[] = [
    { who: 'morse', text: `{yo}: Atalaya le esperaba. Llega el día ${c.day}, con el Bastión en el nivel ${level}.` },
    { who: 'morse', text: `${enemy}. ${story}` },
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
    rule: 'Ocultos: el enemigo no os ha visto. Las casillas con borde rojo os delatan, y disparar también.',
  },
  redTile: {
    who: 'morse',
    line: 'Ahí os ven. Pensadlo dos veces.',
    rule: 'Acabar el movimiento en una casilla roja rompe la ocultación. El primer clic avisa y el segundo mueve.',
  },
  contact: {
    who: 'morse',
    line: 'Contacto. Ahora juegan ellos también.',
    rule: 'Al veros corren a cubrirse. Buscad cobertura (el escudo) antes de terminar el turno.',
  },
  shot: {
    who: 'morse',
    line: 'Mirad el número antes de apretar.',
    rule: 'El porcentaje ya cuenta cobertura, distancia y altura. Por el flanco, más acierto y más crítico.',
  },
  flanked: {
    who: 'morse',
    line: 'Ese está al descubierto. Movedlo.',
    rule: 'Escudo rojo: sin cobertura frente a un enemigo, cualquier disparo puede ser crítico.',
  },
  wounded: {
    who: 'morse',
    line: 'Herido no es caído. Que no se convierta.',
    rule: 'En misión, la salud solo vuelve con botiquín o protocolo médico; en la base cuesta días. Los caídos no vuelven.',
  },
  command: {
    who: 'morse',
    line: 'Una voz manda; la otra escucha. Luego se cambian.',
    rule: 'Quien tiene el mando da las órdenes. La tecla C se lo cede al compañero.',
  },
  flare: {
    who: 'morse',
    line: 'Bengala roja. Llegan más.',
    rule: 'Caen ahí al final del próximo turno enemigo. Apartaos o dejad a alguien en vigilancia.',
  },
  song: {
    who: 'morse',
    line: 'Eso se oye. Que valga la pena.',
    rule: 'Cantar rompe la ocultación, y quien canta es más fácil de cantar para un Cantor.',
  },
  psionic: {
    who: 'morse',
    line: 'Un Cantor os está cantando en la cabeza. Primero el Cantor.',
    rule: 'La voluntad resiste, y sube con el rango. Si cae el Cantor, quien esté bajo su control vuelve con vosotros.',
  },
};

/** The scene a warning plays as. */
export const TIP_SCENE: Scene = { id: 'tip', title: 'Aviso', channel: 'Radio de campaña', when: () => false, lines: [] };
