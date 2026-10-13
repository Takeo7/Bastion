# Fase 5 · El valle de Valdesombra (la lógica)

Quinta fase del cambio a zombis ([LORE.md](LORE.md) «El mundo», [EQUIPO.md](EQUIPO.md)). El arte ya está: el mapa del valle en Synty sustituye al globo (`strategy/geoscape.ts`). Falta que las reglas sean de un valle y no de un globo: carreteras, distancias y una Marea que sube zona a zona. Las reglas las fija Gameplay y las hace Gameplay · Reglas.

## Geografía en el motor

- **Las zonas tienen sitio y carreteras.** `REGIONS` pierde `lat` y `lon` y gana `x`, `z` (las posiciones del mapa de Arte · Base) y la lista de carreteras. El cliente las lee del motor en vez de tener su propia tabla.
- **Las carreteras** (`ROADS`, las mismas que dibuja el mapa):

  | Desde | Hasta |
  |---|---|
  | Altomonte (`weu`) | La Vega (`oce`), Carboneda (`sa`), Valdelobos (`eeu`) |
  | Valdelobos (`eeu`) | El Pantano Gris (`eas`) |
  | El Pantano Gris (`eas`) | La Vega (`oce`) |
  | La Vega (`oce`) | Ciudad Varela (`afr`), Puerto Galena (`me`) |
  | Ciudad Varela (`afr`) | Los Llanos de Ceniza (`na`), Carboneda (`sa`) |
  | Puerto Galena (`me`) | Los Llanos de Ceniza (`na`) |

- **Funciones:**
  - `roadHops(a, b)`: el número de tramos por el camino más corto;
  - `roadRoute(a, b)`: el camino, para La Mula.

## Contactar por carretera

- Solo se puede contactar una zona unida por carretera a Altomonte o a una zona ya contactada. La radio llega hasta donde llega la gente.
- La razón sale en el botón: «Sin carretera desde vuestras zonas: contactad antes con La Vega o Carboneda».

## El viaje de La Mula

- Los días de una misión dependen de lo lejos que esté su zona de Atalaya: `2 + tramos`, menos lo que quite el Taller de La Mula, con un mínimo de 1.
  - Altomonte, 2.
  - La Vega, Carboneda y Valdelobos, 3.
  - Ciudad Varela, Puerto Galena y El Pantano Gris, 4.
  - Los Llanos, 5.
- Esos días pasan como hoy: obras, heridas, fiebres, comida y el reloj zombi.
- Cada operación lo dice en su ficha («4 días de viaje»).

## La Marea sube zona a zona

- La Marea entra por Los Llanos de Ceniza y sube por las carreteras.
- Una zona queda **bajo la Marea** cuando la actividad zombi llega a su umbral, que depende de los tramos que la separan de Los Llanos:

  | Tramos desde Los Llanos | Zonas | Umbral |
  |---|---|---|
  | 0 | Los Llanos | 4 |
  | 1 | Ciudad Varela, Puerto Galena | 6 |
  | 2 | La Vega, Carboneda | 9 |
  | 3 | El Pantano Gris | 10 |
  | 4 | Valdelobos | 11 |

  Altomonte no cae por umbral: si la actividad llega a 12, la campaña se pierde, como hoy.
- **Una zona bajo la Marea:**
  - su enclave deja de mandar suministros, medicinas y comida;
  - sus misiones tienen un grupo más de muertos (la dificultad no cambia);
  - no se puede contactar de nuevo hasta que salga.
- **Si la actividad baja** (Nidos callados, acontecimientos), la Marea retrocede y las zonas salen.
- Un aviso en la base cuando una zona cae o se libra («La Marea llega a La Vega»).
- **Los Nidos** aparecen sobre todo en zonas bajo la Marea o junto a ella.

## Estado y migración

- Los umbrales y la geografía son datos; el estado no guarda nada nuevo, salvo lo que haga falta para los avisos.
- `CAMPAIGN_VERSION` 14 si cambia el estado. Las partidas antiguas siguen; los contactos que ya tengan se quedan aunque no estén unidos por carretera.

## Tests

- **Geografía:** las carreteras son simétricas y todo está conectado; los tramos y las rutas.
- **Contactar:** con carretera sí, sin carretera no, y la partida antigua.
- **Viaje:** los días por zona y el Taller de La Mula.
- **La Marea:** cada umbral; los enclaves bajo la Marea no mandan nada; un grupo más; retrocede; la derrota a 12.
- **Nidos:** dónde aparecen.

## Interfaz

- **Arte · Base** (`geoscape.ts`):
  - posiciones y carreteras leídas del motor;
  - el frente de la Marea alineado con los umbrales: cada zona bajo la Marea se ve dentro de la mancha;
  - La Mula sigue `roadRoute`.
- **Gameplay** (`screen.ts`):
  - los días de viaje en cada operación;
  - la razón al contactar;
  - las zonas bajo la Marea en la lista de zonas y en la agenda.
- **Lore:**
  - los avisos «La Marea llega a…» y «…se libra de la Marea»;
  - la razón de contacto;
  - una línea de radio la primera vez que cae una zona.

## Orden

1. Gameplay · Reglas: el motor y los tests.
2. Arte · Base y Gameplay, en paralelo.
3. Lore, cuando su sesión esté abierta (a [TEXTOS-PENDIENTES.md](TEXTOS-PENDIENTES.md)).
