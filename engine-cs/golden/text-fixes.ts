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
export const TEXT_FIXES: [string | RegExp, string, string][] = [
  // quests.ts
  ['Se une como sargento, con el rasgo «Pesadillas»', 'Se une con tres barras llenas y el rasgo «Pesadillas»', 'Lindqvist: there are no ranks since Fase 10; `rank: 3` is three bars more (seasoned)'],
  ['un refuerzo (Granadero) y dos granadas', 'un recluta y dos granadas', 'the mine: there are no classes since Fase 10; `recruit: {}` is a plain recruit'],
  ['Abatid a todos los hostiles. Primero el Chillón.', 'Abatid a todo lo que haya. Primero el Chillón.', '«hostiles» is English; as the final mission says it'],
  ['Abatid a todos los hostiles.', 'Abatid a toda la banda.', '«hostiles» is English: Lindqvist\'s band and the Ruta 50\'s'],
  ['Los Cuervos se llevan la carga del embarcadero.', 'Los Cuervos se llevan la carga.', 'the river truck: its briefing leaves the load by the road, not on a jetty'],
  // lore.ts
  [/Heridas: curan ×(\d+) de rápido/g, 'Heridas: curan $1 veces más rápido', 'the Enfermería\'s figure, said plainly (textos directos)'],
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
];

export function fixText(text: string): string {
  let out = text;
  for (const [from, to] of TEXT_FIXES) out = typeof from === 'string' ? out.split(from).join(to) : out.replace(from, to);
  return out;
}

export function fixDeep<T>(value: T): T {
  if (typeof value === 'string') return fixText(value) as T;
  if (Array.isArray(value)) return value.map(fixDeep) as T;
  if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, fixDeep(v)])) as T;
  return value;
}
