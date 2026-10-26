/**
 * Texts fixed in C# only, old → new (Lore's, approved by Gameplay on 24/10). packages/ is frozen, even for a stale
 * text, so what TypeScript says has to be fixed the same way before C# is compared with it. This is for every script
 * here that writes what C# must say: make-lore-tests.ts, and the campaign's recordings, where these texts end up too
 * (a quest's step or outcome in the log, an event's option).
 *
 *   fixText(text): one text with every fix applied, in order;
 *   fixDeep(value): the same for every string inside a value (a recorded state, a list of events).
 *
 * Each fix: what to look for (a string, or a RegExp for a family of texts), what goes instead, and why.
 */
import { inMissions } from '../../packages/engine/src/lore';

/** What goes instead: a text, or (for a RegExp) a function of what it matched, as String.prototype.replace takes it. */
type Fix = string | ((match: string, ...groups: string[]) => string);

export const TEXT_FIXES: [string | RegExp, Fix, string][] = [
  // quests.ts
  ['Se une como sargento, con el rasgo «Pesadillas»', 'Se une con tres barras llenas y el rasgo «Pesadillas»', 'Lindqvist: there are no ranks since Fase 10; `rank: 3` is three bars more (seasoned)'],
  ['un refuerzo (Granadero) y dos granadas', 'un recluta y dos granadas', 'the mine: there are no classes since Fase 10; `recruit: {}` is a plain recruit'],
  ['Abatid a todos los hostiles. Primero el Chillón.', 'Abatid a todo lo que haya. Primero el Chillón.', '«hostiles» is English; as the final mission says it'],
  ['Abatid a todos los hostiles.', 'Abatid a toda la banda.', '«hostiles» is English: Lindqvist\'s band and the Ruta 50\'s'],
  ['Los Cuervos se llevan la carga del embarcadero.', 'Los Cuervos se llevan la carga.', 'the river truck: its briefing leaves the load by the road, not on a jetty'],
  // lore.ts
  [/Heridas: curan ×(\d+) de rápido/g, 'Heridas: curan $1 veces más rápido', 'the Enfermería\'s figure, said plainly (textos directos)'],
  [/^Heridas: −(\d+) días?$/, (_, n) => `Heridas: −${n} ${n === '1' ? 'misión' : 'misiones'}`, 'the Garaje\'s figure: time is counted in missions, as the wounded soldier\'s card says it (26/10)'],
  [/^Fiebre: \+(\d+) días?$/, (_, n) => `Fiebre: +${n} ${n === '1' ? 'misión' : 'misiones'}`, 'the Cuarentena\'s figure: the same, as nextSteps says «se convierte dentro de N misiones» (26/10)'],
  // transmissions.ts (and lore.ts EXPEDIENTE, page 4, which says the same about the clock)
  ['el reloj de la sala de mando cuenta kilómetros.', 'el reloj cuenta kilómetros.', 'the Bastion has no «sala de mando»: Morse speaks from the Sala de radio (the scene «La Marea» and the diary\'s page 4)'],
  ['A quien la lleve, su arma le sube un nivel. Decidme a quién. Si cae, se pierde.', 'Está en la Armería. Decidme quién la lleva. Si cae, se pierde.', 'the scene «En una mochila»: weapons have no levels since Fase 9; a found one is a weapon of its own'],
  ['El taller de oficios está abierto.', 'El Taller de oficios está abierto.', 'the scene «Taller de oficios»: a piece\'s name, as everywhere'],
  ['Y un huerto, si queda tierra.', 'Y un Huerto, si queda tierra.', 'the scene «La despensa»: a piece\'s name, as nextSteps says it'],
  ['Sale mañana en La Lata, para las bodegas.', 'Sale mañana en La Lata, para las Bodegas.', 'the scene «Para quienes cumplieron»: las Bodegas are La Vega\'s enclave'],
  ['Una doctora, encerrada en la tercera planta desde hace tres semanas.', 'Una doctora, encerrada en la tercera planta desde hace una semana.', 'the scene «Una llamada de Tarbes»: chapter 2\'s briefing says a week (Gameplay, 24/10)'],
  ['Puerta norte · interfono', 'Entrada del claro', 'the scene «Alguien en la puerta»: a north gate with an intercom was Atalaya; the Bastion is a clearing (Gameplay, 24/10)'],
  ['A su mando: ', 'A tu mando: ', 'the recap: it is for one commander, and the radio says tú and vosotros, never usted'],
  // campaign.ts (its port is Gameplay's: EVENTS.smugglers, nextSteps)
  ['dos granadas extra para la armería', 'dos granadas extra para la Armería', 'the Armería is a place with a name'],
  ['ya quiere despegar: ', 'ya quiere salir: ', 'nextSteps: La Lata doesn\'t take off (XCOM\'s Skyranger)'],
  ['Revisa la escuadra y despega: ', 'Revisa la escuadra y sal: ', 'nextSteps: La Lata doesn\'t take off'],
  ['y sin camas: construye un barracón', 'y sin camas: construye un Barracón', 'nextSteps: a piece\'s name'],
  // campaign.ts: its events, its log, its reasons why not and nextSteps (Lore, 24/10), for the campaign's port
  ['una emboscada de los Cazadores cerca del Bastión', 'una emboscada de los Cuervos cerca del Bastión', 'EVENTS.convoy: a Cazador is one of the Cuervos, not a band'],
  ['−40 suministros, dos misiones nuevas', '−40 suministros; dos misiones nuevas', 'EVENTS.market: an effect\'s parts, as the other events write them'],
  ['−20 suministros, una misión nueva', '−20 suministros; una misión nueva', 'EVENTS.scientist: the same'],
  ['El Taller ya está con otro encargo.', 'El Taller ya está con otro trabajo.', 'craftBlocker: «encargo» is the side quests\' name'],
  ['Llega el segundo comandante', 'Se une el otro jugador', 'addPlayer\'s log and alert: no gender for the partner, who may be Ana'],
  ['Si lo escuchamos unos días, sabremos qué hay de cierto.', 'Si lo escuchamos un tiempo, sabremos qué hay de cierto.', 'a rumour\'s log: time is counted in missions, never days'],
  [/^Escuchamos el rumor (.+): faltan (\d+) días de escucha\.$/, (_, zone, n) => `Escuchamos el rumor ${zone}: se sabrá ${inMissions(Number(n))}.`, 'investigate\'s log: in missions, as the Mapa says it («Se sabrá tras la próxima misión»); it said «faltan 1 días»'],
  [/^El Taller se pone con (.+): (\d+) días\.$/, (_, what, n) => `El Taller se pone con ${what}: sale ${inMissions(Number(n))}.`, 'craft\'s log: in missions, as the Armería says it («Sale tras la próxima misión»)'],
  [/ recibe medicinas: (\d+) días menos de recuperación\.$/, (_, n) => (n === '1' ? ' recibe medicinas: vuelve una misión antes.' : ` recibe medicinas: vuelve ${n} misiones antes.`), 'medicate\'s log: it said «1 días menos de recuperación»; in missions, as the Curar button'],
  [/ caduca en (\d+) días?$/, (_, n) => ` caduca ${inMissions(Number(n))}`, 'nextSteps: in missions, as the Mapa says «Caduca tras la próxima misión»'],
  [/ se convierte mañana$/, ' se convierte tras la próxima misión', 'nextSteps: in missions, as the soldier\'s sheet says it'],
  [/ se convierte en (\d+) días$/, (_, n) => ` se convierte ${inMissions(Number(n))}`, 'nextSteps: the same'],
  [' tiene fiebre: hay que tratarle antes.', ' tiene fiebre: necesita el tratamiento antes de salir.', 'squad\'s reason: «tratarle» is leísmo, and the Escuadra\'s tooltip says it so'],
  [/^No está contagiado\.$/, 'No tiene ningún mordisco.', 'treat\'s reason: «contagiado» doesn\'t agree with a soldier who is a woman'],
  [/^No está herido\.$/, 'No tiene heridas.', 'medicate\'s reason, and rules.ts First aid and a medicine on a partner: «herido» doesn\'t agree with a soldier who is a woman'],
  // rules.ts (its port is Gameplay's: Rules.cs, changed on 26/10)
  [/^No estás herido\.$/, 'No tienes heridas.', 'a medicine on oneself: the same'],
  ['No está aturdido, desorientado ni en pánico.', 'No sufre aturdimiento, desorientación ni pánico.', 'Reanimar\'s reason: the same'],
  [/^Está lista\.$/, 'No hay nada que reponer.', 'rearm\'s reason: said of «el cepo» or «el foso» too'],
  ['La Marea ha llegado al Bastión.', 'Los muertos han llegado al Bastión.', 'the defeat\'s log: before level 4 nobody knows of the Marea'],
  ['El Bastión entra en servicio. Elegid la primera misión.', 'Lo que se levante en este claro será el Bastión. Elegid la primera misión.', 'WELCOME: the Bastion starts as a caravan in a clearing, not a station going into service; as the diary\'s first page (Gameplay, 24/10)'],
  [/^Llega el convoy de Bielsa: Ruth Halvorsen manda (\d+) suministros\.$/, 'Llega el convoy de Ruth Halvorsen: $1 suministros de lo pactado con los enclaves.', 'the convoy\'s log: it brings what every contacted zone pays, not Bielsa\'s alone (Gameplay, 24/10)'],
];

/**
 * transmissions.ts recapLines' last line, fixed in C# only (Gameplay, 24/10), which no fix above can say, for it
 * depends on the campaign: `step` is nextSteps' first label, as C# has it (fixText), `names` the soldiers'. The 0.10.1
 * lowered the step's first letter, a soldier's name too («hiro Sato se convierte…»), and a step with its own «:» came
 * out with two («Lo más urgente: revisa la escuadra y sal: El colegio…»), and one ending in a full stop got a second
 * («…levantad un Huerto..»). C#: Transmissions.UrgentLine.
 */
export function recapUrgent(step: string, names: readonly string[]): string {
  const end = step.endsWith('.') ? '' : '.';
  if (step.includes(': ')) return `Lo más urgente. ${step}${end}`;
  if (names.some((n) => n && step.startsWith(n))) return `Lo más urgente: ${step}${end}`;
  return `Lo más urgente: ${step[0]!.toLowerCase()}${step.slice(1)}${end}`;
}

export function fixText(text: string): string {
  let out = text;
  for (const [from, to] of TEXT_FIXES) out = typeof from === 'string' ? out.split(from).join(to as string) : out.replace(from, to as never);
  return out;
}

export function fixDeep<T>(value: T): T {
  if (typeof value === 'string') return fixText(value) as T;
  if (Array.isArray(value)) return value.map(fixDeep) as T;
  if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, fixDeep(v)])) as T;
  return value;
}
