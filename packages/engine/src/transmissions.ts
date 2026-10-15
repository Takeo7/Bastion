/**
 * "Transmisiones": short radio conversations between the Bastion's people at
 * the story's turning points (docs/LORE.md). Text only. Each client plays a
 * scene once, when its condition first holds, and keeps it in the radio
 * archive; nothing here changes the campaign.
 *
 * `{saludo}` becomes "Comandantes Ana y Luis" (or "Comandante Ana" alone);
 * `{yo}`, "Comandante Ana" for the player reading it.
 */
import { DOOM_MAX, nextSteps, treatCost, type CampaignState } from './campaign';
import { FOUND_WEAPONS } from './content';
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
  morse: { name: 'Nadia Albescu, «Morse»', role: 'Radio' },
  teo: { name: 'Teodor Vidić, «Teo»', role: 'Taller' },
  nwosu: { name: 'Dra. Amara Nwosu', role: 'Enfermería' },
  simon: { name: 'Simão Ferreira', role: 'Desertor de los Cuervos' },
  voz: { name: 'Celia Marsh, «la Voz»', role: 'La Congregación' },
  halvorsen: { name: 'Ruth Halvorsen', role: 'Refugio de Brena' },
  coro: { name: 'Voz sin nombre', role: 'Frecuencia de la Congregación' },
};

export interface SceneLine {
  who: SpeakerId;
  text: string;
  /** The rule behind it, in small print under the line (the radio's warnings). */
  rule?: string;
  /** Said only when this holds (Simão speaks only if he was let in). */
  if?: (c: CampaignState) => boolean;
}

export type SceneId =
  | 'council'
  | 'ch1After'
  | 'ch2Before'
  | 'ch2After'
  | 'ch3Before'
  | 'ch3After'
  | 'firstFlood'
  | 'partitura'
  | 'simon'
  | 'ch5After'
  | 'velmar'
  | 'firstSong'
  // Phases 3 and 4 (docs/FASE3.md, FASE4.md): the bite, medicines, food and found weapons.
  | 'firstBite'
  | 'shortMedicines'
  | 'firstTurned'
  | 'hunger'
  | 'foundWeapon'
  | 'confession'
  | 'ch6After'
  | 'contranota'
  | 'voz'
  | 'eve'
  | 'endingBreak'
  | 'endingRecord'
  | 'granCoral'
  /** Built on the spot: the recap for whoever opens a campaign already under way. */
  | 'recap';

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
const bitten = (c: CampaignState) => c.soldiers.filter((s) => s.alive && s.infection);

/** In story order: when several unlock at once they play in this order. */
export const SCENES: Scene[] = [
  {
    id: 'council',
    title: 'Carta del Refugio',
    channel: 'Correo a pie · leída por Morse',
    when: (c) => c.day > 31,
    lines: [
      { who: 'halvorsen', text: 'A Atalaya, del Refugio de Brena. Con esta carta suben los suministros del mes.' },
      { who: 'halvorsen', text: 'Cada caja sale de una despensa que también pasa hambre. Esperamos resultados.' },
    ],
  },
  {
    id: 'ch1After',
    title: 'Campanas de fondo',
    channel: 'Sala de radio',
    when: (c) => c.story.done >= 1 && bastionLevel(c) >= 2,
    lines: [
      { who: 'morse', text: 'Anoche, en onda media, una mujer hablaba de los «salvados». Había campanas de fondo.' },
      { who: 'teo', text: 'Apúntalo.' },
    ],
  },
  {
    id: 'ch2Before',
    title: 'Una llamada de Corran',
    channel: 'Onda corta · 7.040 kHz',
    when: (c) => chapterOpen(c, 2),
    lines: [
      { who: 'morse', text: 'Llamada desde el hospital de Corran. Una doctora, encerrada en la tercera planta desde hace tres semanas.' },
      { who: 'morse', text: 'Trabajaba en el Instituto Galen. Se le acaba el agua.' },
      { who: 'teo', text: 'Si sabe algo del Galen, hay que sacarla.' },
    ],
  },
  {
    id: 'ch2After',
    title: 'La doctora',
    channel: 'Línea interna · Enfermería',
    when: (c) => c.story.done >= 2,
    lines: [
      { who: 'nwosu', text: 'Amara Nwosu. Virología, Instituto Galen.' },
      { who: 'nwosu', text: 'Cuando cerraron las puertas salí con mis notas y nada más. Creo que hay una vacuna.' },
      { who: 'nwosu', text: 'Me faltan muestras, tiempo y una nevera que no se apague.' },
      { who: 'teo', text: 'La nevera es cosa mía.' },
    ],
  },
  {
    id: 'ch3Before',
    title: 'La sirena',
    channel: 'Línea interna · Taller',
    when: (c) => chapterOpen(c, 3),
    lines: [
      { who: 'teo', text: 'Los Cuervos tienen un coche patrulla en los Llanos de Ceniza. Lo llevan junto a quien no les paga, encienden la sirena, y los muertos acuden.' },
      { who: 'teo', text: 'Así vaciaron dos aldeas el invierno pasado. Quiero que se calle.' },
    ],
  },
  {
    id: 'ch3After',
    title: 'Silencio en los Llanos',
    channel: 'Onda corta · 4.625 kHz',
    when: (c) => c.story.done >= 3,
    lines: [
      { who: 'teo', text: 'Tres días sin una horda en la carretera.' },
      { who: 'teo', text: 'Los muertos no piensan: van al ruido. Quien controla el ruido, controla la horda.' },
    ],
  },
  {
    id: 'firstFlood',
    title: 'Los Llanos no contestan',
    channel: 'Sala de radio',
    when: (c) => (c.flooded ?? []).length > 0,
    lines: [
      { who: 'morse', text: 'Los Llanos de Ceniza no contestan desde ayer.' },
      { who: 'morse', text: 'El último mensaje era un hombre contando muertos desde un tejado. Paró en el doscientos.' },
      { who: 'teo', text: 'De allí ya no va a llegar nada.' },
      { who: 'morse', text: 'Y lo que baja de los Llanos sube por las carreteras. Si la infestación sigue creciendo, las siguientes serán Corran y Galen.' },
    ],
  },
  {
    id: 'partitura',
    title: 'La Marea',
    channel: 'Sala de radio',
    when: (c) => bastionLevel(c) >= 4,
    lines: [
      { who: 'morse', text: 'Con la antena nueva oigo a los otros enclaves. Todos dicen lo mismo: con el deshielo sube algo desde los Llanos de Ceniza, miles de muertos que no se dispersan. La llaman la Marea.' },
      { who: 'teo', text: 'Y Atalaya está al final de la cuesta.' },
      { who: 'morse', text: 'Desde hoy, el reloj de la sala de mando cuenta kilómetros.' },
    ],
  },
  {
    id: 'simon',
    title: 'Alguien en la puerta',
    channel: 'Puerta norte · interfono',
    when: (c) => c.event === 'simon' || c.story.simon !== null,
    lines: [
      { who: 'simon', text: 'Me llamo Simão Ferreira. Hasta hace un mes iba con los Cuervos.' },
      { who: 'morse', text: 'Hace un mes nos habrías vendido.' },
      { who: 'simon', text: 'Sí. Ya no. Sé dónde tienen a una niña a la que muerden y no se levanta.' },
      { who: 'nwosu', text: 'Si es verdad, esa niña vale más que todo el valle.' },
      { who: 'morse', text: 'Decidid vosotros. Yo no le abriría.' },
    ],
  },
  {
    id: 'ch5After',
    title: 'La niña',
    channel: 'Línea interna · Enfermería',
    when: (c) => c.story.done >= 5,
    lines: [
      { who: 'nwosu', text: 'Está a salvo. Tres mordiscos viejos en el brazo y ni una décima de fiebre.' },
      { who: 'simon', text: 'Os dije dónde estaba.', if: trusted },
      { who: 'nwosu', text: 'El virus entra en su sangre y no encuentra dónde agarrarse. Es el principio de una vacuna.' },
      { who: 'teo', text: 'Pregunta cuándo puede subir a La Lata.' },
    ],
  },
  {
    id: 'velmar',
    title: 'Taller de oficios',
    channel: 'Taller de oficios',
    when: hasEchoes,
    lines: [
      { who: 'teo', text: 'El taller de oficios está abierto. Lo que se sabía antes de los motores y se nos olvidó.' },
      { who: 'teo', text: 'Machete, ballesta, fuego y vendas.' },
      { who: 'nwosu', text: 'Las vendas las enseño yo.' },
    ],
  },
  {
    id: 'firstSong',
    title: 'El primer oficio',
    channel: 'Taller de oficios',
    when: someoneTrained,
    lines: [
      { who: 'teo', text: 'Alguien ha terminado su primer oficio. Aprende rápido.' },
      { who: 'teo', text: 'En el campo, con cabeza: el machete y la ballesta casi no se oyen. El fuego se oye desde lejos.' },
    ],
  },
  {
    id: 'firstBite',
    title: 'Fiebre',
    channel: 'Línea interna · Enfermería',
    when: (c) => bitten(c).length > 0,
    lines: [
      { who: 'nwosu', text: 'Treinta y nueve y subiendo. Es el mordisco. Está en la Cuarentena, con el cerrojo echado.' },
      { who: 'nwosu', text: 'Tres días, alguno más con la Enfermería o con la Cuarentena mejor montada. Con el tratamiento, la fiebre baja.' },
      { who: 'nwosu', text: 'Cada tratamiento se lleva medicinas, y no nos sobran.' },
      { who: 'morse', text: 'Con fiebre no sale de misión. El tratamiento lo aprobáis los dos.' },
    ],
  },
  {
    id: 'shortMedicines',
    title: 'No hay para todos',
    channel: 'Línea interna · Enfermería',
    when: (c) => {
      const sick = bitten(c).length;
      return sick > 0 && (c.medicines ?? 0) < sick * treatCost(c);
    },
    lines: [
      { who: 'nwosu', text: 'No hay medicinas para todos. Lo he contado tres veces.' },
      { who: 'nwosu', text: 'A quién se le dan no lo decido yo.' },
    ],
  },
  {
    id: 'firstTurned',
    title: 'La fiebre no bajó',
    channel: 'Sala de radio',
    when: (c) => c.soldiers.some((s) => !s.alive && s.fellCause === 'turned'),
    lines: [
      { who: 'morse', text: 'Se ha ido esta mañana. La fiebre no bajó.' },
      { who: 'teo', text: 'Me he encargado yo.' },
      { who: 'morse', text: 'Apuntad su nombre.' },
    ],
  },
  {
    id: 'hunger',
    title: 'La despensa',
    channel: 'Línea interna · Taller',
    when: (c) => !!c.hungry,
    lines: [
      { who: 'teo', text: 'Se ha acabado la despensa.' },
      { who: 'morse', text: 'Con hambre, en las misiones se aguanta peor el miedo, y las heridas tardan el doble en cerrar.' },
      { who: 'teo', text: 'Hacen falta raciones: despensas, tiendas, almacenes. Y un huerto, si queda tierra.' },
    ],
  },
  {
    id: 'foundWeapon',
    title: 'En una mochila',
    channel: 'Línea interna · Armería',
    when: (c) => (c.foundWeapons ?? []).some((w) => FOUND_WEAPONS[w.kind].better),
    lines: [
      { who: 'teo', text: 'Ha llegado un arma buena en una mochila. Está limpia y engrasada.' },
      { who: 'teo', text: 'A quien la lleve, su arma le sube un nivel. Decidme a quién. Si cae, se pierde.' },
    ],
  },
  {
    id: 'confession',
    title: 'La noche de Galen',
    channel: 'Sala de radio · 03:10',
    when: (c) => bastionLevel(c) >= 6,
    lines: [
      { who: 'morse', text: 'Teo. Nwosu dice que en el Galen había un vigilante de noche. El último que vio el sótano.' },
      { who: 'teo', text: 'Era yo.' },
      { who: 'teo', text: 'Hacía las noches para estirar la pensión. A las tres y diez saltó una alarma en el sótano. Saltaba cada dos por tres. La apagué.' },
      { who: 'teo', text: 'Tres semanas después, los muertos andaban por Corran.' },
      { who: 'morse', text: 'Dos inviernos callado.' },
      { who: 'teo', text: 'Dos inviernos intentando arreglarlo.' },
    ],
  },
  {
    id: 'ch6After',
    title: 'Los cuadernos de Galen',
    channel: 'Sala de radio',
    when: (c) => c.story.done >= 6,
    lines: [
      { who: 'nwosu', text: 'Están todos: pruebas, muestras, fallos. Estábamos más cerca de lo que yo creía.' },
      { who: 'teo', text: 'Y el parte de aquella noche. «03:10, alarma en el sótano. Apagada por el vigilante.»' },
      { who: 'nwosu', text: 'Esa alarma saltaba tres veces por semana, Teo. También lo pone aquí.' },
      { who: 'morse', text: 'Alguien ha entrado en nuestra frecuencia.' },
      { who: 'coro', text: 'Atalaya. Quemad esos cuadernos. Nadie está enfermo. Solo os falta dar el último paso.' },
      { who: 'morse', text: 'La Congregación. Saben lo que hemos traído.' },
    ],
  },
  {
    id: 'contranota',
    title: 'La emisora',
    channel: 'Línea interna · Taller',
    when: (c) => bastionLevel(c) >= 7,
    lines: [
      { who: 'nwosu', text: 'La vacuna está a semanas. Si la Marea nos deja esas semanas.' },
      { who: 'morse', text: 'No nos las va a dejar. Cada noche la Voz habla por radio, y la Marea gira hacia donde ella dice.' },
      { who: 'teo', text: 'Campanas y altavoces. La lleva como a un rebaño.' },
      { who: 'morse', text: 'Es Celia Marsh. Hacía el programa de la tarde en Radio Corran.' },
      { who: 'teo', text: 'Hay que sacarla de su emisora. Viva: solo ella sabe cómo se deshace la Marea.' },
    ],
  },
  {
    id: 'voz',
    title: 'Fuera de antena',
    channel: 'Atalaya · celda del sótano',
    when: (c) => c.story.done >= 7,
    lines: [
      { who: 'voz', text: 'Buenas noches, Kessra. Están escuchando a la Voz.' },
      { who: 'morse', text: 'Ya no está en antena. Aquí solo la oímos nosotros.' },
      { who: 'voz', text: 'Qué silencio tienen aquí arriba.' },
      { who: 'voz', text: 'La Marea no me obedece a mí. Obedece al ruido más fuerte. Mis campanas solo le enseñaban el camino.' },
      { who: 'voz', text: 'Denle un ruido más fuerte que mi voz, lejos de los vivos, y no lo apaguen hasta que se gire la última cabeza.' },
    ],
  },
  {
    id: 'eve',
    title: 'La víspera',
    channel: 'Línea interna · todas las salas',
    when: (c) => c.offers.some((o) => o.final) || c.event === 'finale' || c.ending !== null,
    lines: [
      { who: 'morse', text: 'La Marea se ve desde la presa. Llena el valle.' },
      { who: 'teo', text: 'La Lata tiene gasolina para un viaje y la sirena de los Cuervos atornillada al techo.' },
      { who: 'nwosu', text: 'La vacuna está lista. Pocas dosis. Habrá que decidir para quién.' },
      { who: 'simon', text: 'Los Cuervos no se la merecen. Aunque mi hermano siga con ellos.', if: trusted },
      { who: 'morse', text: 'Lo decidiréis los dos.' },
    ],
  },
  {
    id: 'endingBreak',
    title: 'Para todos',
    channel: 'Onda corta · todas las frecuencias',
    when: (c) => c.ending === 'break',
    lines: [
      { who: 'morse', text: 'Aquí Atalaya, a cualquiera con una radio encendida. Coged papel: así se hace la vacuna.' },
      { who: 'nwosu', text: 'La receta entera, en abierto. Para los enclaves y para los Cuervos.' },
      { who: 'teo', text: 'Alguno la venderá. Otro la usará para mandar.' },
      { who: 'morse', text: 'Contestan sitios que no sabía que seguían vivos. Doce frecuencias. Trece.' },
    ],
  },
  {
    id: 'endingRecord',
    title: 'Para la Red',
    channel: 'Frecuencia de la Red',
    when: (c) => c.ending === 'record',
    lines: [
      { who: 'teo', text: 'Última caja de dosis. Sale mañana en La Lata, para las bodegas.' },
      { who: 'morse', text: '¿Y la receta?' },
      { who: 'nwosu', text: 'En el refugio, bajo llave.' },
      { who: 'morse', text: 'Anoche llamaron desde el otro lado de la sierra. Preguntaban por una vacuna.' },
      { who: 'teo', text: '¿Qué les dijiste?' },
      { who: 'morse', text: 'Que aquí no sabemos nada. Atalaya, cambio y corto.' },
    ],
  },
  {
    id: 'granCoral',
    title: 'Kilómetro cero',
    channel: 'Todas las frecuencias',
    when: (c) => c.outcome === 'defeat' && c.doom >= DOOM_MAX,
    lines: [
      { who: 'coro', text: 'Kilómetro cero. Las campanas suenan en la sierra.' },
      { who: 'coro', text: 'Abrid las puertas. Todos seréis salvados.' },
      { who: 'morse', text: 'Aquí Atalaya. Si alguien recibe esto: lo intentamos.' },
    ],
  },
];

/** Scenes whose moment has come, in story order. */
export function unlockedScenes(c: CampaignState): Scene[] {
  return SCENES.filter((s) => s.when(c));
}

/** Scenes tied to what is on screen (Simão at the door, the end): they play as soon as they unlock. */
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

// ---------------------------------------------------------------- recap

/** Played to whoever opens a campaign already under way (a partner arriving late, another browser). */
export const RECAP: Scene = { id: 'recap', title: 'Relevo', channel: 'Línea interna · Estación Atalaya', when: () => false, lines: [] };

/** Where the campaign stands, for `slot`: the day, the Bastion, the Marea, their soldiers and the first thing to do. */
export function recapLines(c: CampaignState, slot: Slot): SceneLine[] {
  const level = bastionLevel(c);
  const story = c.story.done ? `Llevamos ${c.story.done} de ${STORY.length} hojas del diario.` : 'El diario está en la primera hoja.';
  const mine = c.soldiers.filter((s) => s.owner === slot && s.alive).map((s) => s.name);
  const names = mine.length > 1 ? `${mine.slice(0, -1).join(', ')} y ${mine[mine.length - 1]}` : (mine[0] ?? 'nadie todavía');
  const first = nextSteps(c, slot)[0];
  const lines: SceneLine[] = [
    { who: 'morse', text: `{yo}: día ${c.day}. El Bastión está en el nivel ${level}.` },
    { who: 'morse', text: `${doomStatus(level, c.doom)}. ${story}` },
    { who: 'teo', text: `A su mando: ${names}.` },
  ];
  if (first) lines.push({ who: 'morse', text: `Lo más urgente: ${first.label[0]!.toLowerCase()}${first.label.slice(1)}.` });
  return lines;
}
