# Fase 2 · Saqueo, ruido y zombis

Segunda fase del cambio a zombis ([LORE.md](LORE.md), [EQUIPO.md](EQUIPO.md)). Esta hoja es el acuerdo entre roles: las reglas las fija Gameplay, y Combate, Arte y Lore construyen encima.

## Reglas (Gameplay · `engine/`)

### Ruido

- Cada acción hace un ruido con un radio en casillas (`NOISE` en `content.ts`):

  | Acción | Radio |
  |---|---|
  | Pistola | 8 |
  | Escopeta | 10 |
  | Fusil | 12 |
  | Ametralladora o cañón | 14 |
  | Francotirador | 16 |
  | Granada o explosión | 18 |
  | Romper una puerta o ventana | 6 |
  | Correr (gastar las dos acciones en mover) | 3 |
  | Registrar un mueble | 2 |
  | Machete, ballesta y el Oficio silencioso | 0 a 2 |

- **Evento:** `noise { tile, radius, by? }`, emitido tras la acción.
- **Quién lo oye:** todo enemigo a menos del radio. Si no ve a nadie, va hacia ese punto (`heard`); un grupo inactivo que lo oye se pone en marcha hacia el ruido, pero no se activa hasta ver a alguien.
- **Ocultación:** el ruido por sí solo no rompe la ocultación; la rompe que el que acude os vea.
- **El Chillón:** su grito hace un ruido de 16 en su casilla y atrae a todos los zombis.

### Zombis

- **Plantillas:**
  - `zombie`: zombi.
  - `xenoid`: Corredor, rápido.
  - `mec`: Acorazado, con blindaje.
  - `sectoid`: Chillón.
  - `bloater`: Hinchado, nuevo.
- Todos los zombis atacan cuerpo a cuerpo (garras). El Corredor y el Acorazado dejan de disparar.
- **IA de horda:** sin cobertura; van a por el soldado visible más cercano, y si no ven a nadie, al último ruido oído. Si no han oído nada, deambulan.
- **El Chillón:** grita (desorienta o asusta a los soldados cercanos y atrae a los zombis) y no muerde.
- **El Hinchado:** lento. Al morir o al llegar a un soldado revienta: 3 de daño en radio 1,5. La infección llega en la fase 3.
- **Los humanos** (Saqueador, Cabecilla, Cazador) siguen como ahora: se cubren y disparan. También oyen el ruido.

### Saqueo

- **Muebles registrables:** las casillas cuyo `decor` es un mueble (armario, mesa, escritorio, estantería, mostrador, archivador, cama), cajas o un coche (el maletero).
- **Acción `search`:** 1 acción, junto a un mueble aún sin registrar, con un ruido de 2. Sale de una tabla según el tipo de mueble y la dificultad:
  - suministros (5–20 §);
  - un accesorio (granada, botiquín, munición…);
  - a veces medicinas (se guardan; se usan en la fase 3);
  - a veces nada.
  - **Evento:** `searched { unit, tile, loot }`.
- **Mochila:** cada soldado lleva 3 cosas. Con la mochila llena no puede registrar.
- **El botín cuenta** si el soldado evacúa o sigue en pie al ganar. Lo que lleva un caído se pierde.
- **Misión nueva `scavenge`, «Saqueo»:** un pueblo con mucho interior. El objetivo es registrar lo que podáis y evacuar; no hay temporizador, pero cada pocos turnos llegan más zombis por los bordes, y antes cuanto más ruido.
- En las demás misiones también se puede registrar, a cambio de turnos y ruido.
- **Campaña:**
  - el informe suma el botín (`report.loot`);
  - los suministros van a `c.credits`;
  - los accesorios, al inventario;
  - las medicinas, a `c.medicines`, solo contador por ahora.

## Interfaz de combate (Combate · `game.ts`, `hud.ts`)

- **Registrar:**
  - botón «Registrar» (con su ficha: «1 acción · ruido 2»);
  - los muebles registrables se marcan al elegir la acción o al pasar el ratón;
  - lo encontrado sale flotando sobre el mueble.
- **Mochila:** en la tarjeta del soldado, sus 3 huecos con iconos.
- **Ruido:**
  - al actuar, un anillo del radio de ruido;
  - aviso «Os han oído: N zombis vienen hacia aquí»;
  - en la previsión de cada acción, su ruido («Ruido 12»).
- **Horda:** aviso cuando llegan zombis por los bordes.
- **Informe:** el botín traído.
- **Textos de Lore** (`lore.ts`):
  - `SEARCH_SPOTS[decor].name` para el botón y la ficha («Registrar el armario»);
  - `searchLine(...)` para el registro;
  - `SCAVENGE.*` para el objetivo y el parte;
  - `lootSummary(...)` (ya en el informe).
- **Consejos de radio** (`transmissions.ts`):
  - `heard`, la primera vez que salta «Os han oído»;
  - `search`, el primer `searched` de uno de los míos;
  - `horde`, la primera llegada por los bordes en un Saqueo;
  - `bloater`, el primer Hinchado a la vista.

## Decisiones (Gameplay)

- **El Chillón:** sigue levantando cadáveres como zombis, pero ya no controla: su grito desorienta o asusta.
- **El Oficio:** ya no rompe la ocultación por sí mismo. Cada habilidad tiene su ruido (ballesta 1, vendaje o escudo 0–2, molotov 10, petardos 12 como cebo), y lo que os delata es que os vean.

## Arte (`game/`, `public/models/`)

- **Muebles:** un brillo o contorno en los registrables, y el aspecto de ya registrado (cajones abiertos, maletero abierto).
- **Modelos:** el Hinchado (de Boss Zombies o City Zombies); el Corredor y el Acorazado, sin arma.
- **Efectos:** el grito del Chillón, el reventón del Hinchado y el anillo del ruido.
- **Animaciones** de Base Locomotion e Idles. Para disparar, apuntar, recargar, morir y los zombis, por código, sobre la locomoción.

## Lore

- Consejos de radio para el ruido (disparar, el grito, ir ocultos) y para saquear.
- Fichas de enemigo: zombi, Corredor, Acorazado, Chillón e Hinchado.
- Nombres y frases del botín y de los muebles.
- La misión de Saqueo: informe, objetivo y líneas del parte.

## Orden

1. Gameplay: el ruido, la IA de los zombis y el Hinchado en el motor, con sus tests.
2. Gameplay: la acción `search`, la mochila, la misión `scavenge` y el botín en la campaña.
3. En paralelo, en cuanto las reglas estén escritas: Combate, Arte y Lore.
- Los zombis no buscan cobertura, pero si acaban detrás de una, les protege como a cualquiera (decidido tras la prueba de Combate).
