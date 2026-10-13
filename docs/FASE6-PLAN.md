# Fase 6 · Plan de Reglas (edificios de varias plantas)

Plan de Gameplay · Reglas para [FASE6.md](FASE6.md). Es solo el plan: no hay código. Gameplay lo revisa y lo aprueba antes de empezar.

## 0. De dónde partimos

- **Una superficie por casilla.** `tiles` dice qué hay y `elev` a qué altura, en plantas. Hoy se pisan los bloques macizos (pisos con terraza, plataformas, colinas), que son columnas sólidas. Las azoteas de las casas, tiendas y oficinas no se pisan: dentro solo existe la planta baja.
- **Las plantas altas son un apaño de la visión.** `storeys` dice cuántas plantas tiene cada edificio. `blocksSightAt` y `roofSide` (`grid.ts`) hacen macizo lo que hay por encima de la planta baja, y el tejado corta las líneas. No hay suelos ni techos de verdad.
- **Escalera de mano** (`path.ts` `stepCost`): desde una casilla `ladder` se sube a la casilla de al lado si está una planta más arriba.
- **Lo que cuesta hoy** (ciudad 48 × 48, semilla 7, medido en esta máquina):

  | Medida | Valor |
  |---|---|
  | Estado de la misión en JSON | 58 KB (de ellos, `tiles` 18 KB) |
  | `teamVisibility` de la escuadra | 1,6 ms |
  | `teamVisibility` de los enemigos | 3,1 ms |
  | `computeReach` de una carrera | 0,2 ms |
  | `lineClear` | ~1 µs |
  | Un turno enemigo con todos los pods activos | 6 ms |
  | `createMission` | 11 ms |

- **Cuánto hay que construir:** de media, 730 de las 2304 casillas de una ciudad están en edificios de 2 o 3 plantas, y 890 en algún edificio.

## 1. Modelo de datos

### Capas por planta; la planta baja no cambia

- **`GameState.levels?: Level[]`.** `levels[k - 1]` es la planta `k` (`k` = 1, 2, 3), a `k` plantas de altura. Con la baja son 4 niveles como mucho: baja, primera, segunda y la azotea de un edificio de tres.
- **`Level`:**

  ```ts
  interface Level {
    tiles: (TileKind | null)[];
    decor?: Decor[];
  }
  ```

  - `tiles` mide W × H y se indexa igual que `tiles` de la baja.
  - `null` es aire: fuera de los edificios, o el hueco de una escalera.
  - Una casilla que no es `null` es un forjado a la altura `k` con algo encima, con los mismos `TileKind` de hoy: suelo, pared, puerta, ventana, muebles (`low`/`high`) y escalera.
- **Tejados.** Cada edificio de `n` plantas tiene forjado a las alturas 1…n. De 1 a n-1 son suelos de verdad. La `n` es el tejado:
  - plano y con acceso (tiendas, oficinas): `floor`, con antepecho `low`;
  - si no, un `TileKind` nuevo, `'roof'`: un forjado que tapa pero no se pisa (tejados a dos aguas, o sin acceso). Así también las casas de una planta tienen techo de verdad.
- **Restricción:** las capas solo van sobre suelo a altura 0. Los edificios ya se generan siempre así.
  - La altura de una posición es `heightAt(s, x, y, z)`: `elev` en la baja y `z` en las plantas.
- **`elev` se queda** para los bloques macizos: son la baja a altura 1–2.
  - Por eso el nivel no es la altura sobre un bloque macizo.
  - La «planta» que enseña el cliente es `Math.floor(altura)`.
- **`storeys` se queda.** El cliente lo usa para dibujar las alturas.
  - Si hay `levels`, la visión usa la geometría de las capas e ignora el apaño de `storeys`.
  - Si no hay `levels` (partidas guardadas, mapas a mano), todo sigue como hoy. El cambio de un modelo a otro está en un solo sitio.

**Por qué no las otras opciones:**

- **Capas solo en el rectángulo de cada edificio:** el estado pesaría menos, pero cada consulta tendría que buscar el rectángulo, o reconstruir un índice en cada cliente. Todo para ahorrar unos 40 KB.
- **Columnas con una lista de superficies por casilla:** es lo más general, pero cada función tiene que buscar en una lista. Las capas dan lo mismo con un índice de array.
- **Pasar también los bloques macizos a capas, para que `z` sea siempre la altura:** sería más limpio, pero obliga a rehacer en el generador los pisos, las plataformas y las colinas, y rompe las partidas guardadas. No hace falta para esta fase.

### Posiciones con nivel

- **Tipo nuevo `Pos` (`Vec2` con `z: number` obligatorio).** `z` es el índice de nivel, de 0 a 3. Lo llevan:
  - `Unit.pos`, `heard` y `patrol`;
  - el objeto de la misión, el terminal, y el centro del humo, del Silencio y de las luces;
  - `Reinforcement.pos`;
  - las posiciones de los eventos: `moved.path`, `tileChanged.pos`, `noise.tile`, `searched.tile`, `explosive.target`, `teleported`, `itemDropped`.
- **Por qué no `Vec2.z?`.** `Vec2` sirve también para direcciones, rectángulos y toda la geometría del generador.
  - Un `z` opcional se pierde sin avisar en cada `{ x: p.x + d.x, y: p.y + d.y }`, y el motor tiene decenas.
  - La unidad acabaría en la planta baja sin ningún error.
  - Con `z` obligatorio en un tipo aparte, el compilador señala cada sitio que construye una posición.
  - Las salidas, la zona de evacuación y los mapas a mano siguen en `Vec2`: siempre están en la baja.
- **En la red y en las partidas guardadas, `z` es opcional y vale 0.**
  - Los comandos llevan `z: z.number().int().min(0).max(3).optional()`, y `executeCommand` lo completa. El cliente compila sin tocarlo y juega en la planta baja hasta que Combate añada las plantas.
  - Una misión guardada antes pasa por `normalizeMission(state)` (nueva, en `setup.ts`) al cargarla en `persistence.ts`. Pone `z: 0` donde falta, y se puede llamar dos veces sin cambiar nada.
- **Índice de nodo:** `nodo = z · W · H + y · W + x`.
  - En `z = 0` es el índice de casilla de hoy, así que no cambian de significado en la baja: `Reach.cost`, `searched`, `Light.tiles`, las máscaras de visión y lo que el cliente lee de ellas.
  - Las plantas van detrás.
- **Funciones nuevas en `grid.ts`:**
  - `nodeOf(s, p)` y `posOf(s, nodo)`;
  - `kindAt(s, p)`: la capa que toque, y `null` si es aire;
  - `heightAt(s, p)`;
  - `slabAt(s, x, y, k)`: si hay forjado;
  - `surfaceAt(s, x, y, h)`: qué nivel tiene superficie pisable a la altura `h` en esa columna;
  - `samePos(a, b)`: con `z`.
- **Cambian de firma:** `unitAt(s, p: Pos)` y todas las que hablan de la posición de una unidad.

## 2. Sistemas que cambian

### Rejilla (`grid.ts`)

- `tileAt` y `elevAt` se quedan para la planta baja. Llegan las funciones de arriba.
- `coverFrom` mira el nivel de la unidad.
- **Visión con capas:** `opaqueAt(s, x, y, h)` dice si hay algo que tape a la altura `h`:
  - la columna maciza bajo `elev`;
  - el objeto del forjado de esa franja de altura, con `OBSTACLE_HEIGHT` sobre su forjado: la pared tapa toda la planta y la ventana es un hueco;
  - los forjados, como capas finas.
- `blocksSightAt` y `roofSide` solo se usan sin capas.
- **Coste:** S. **Riesgo:** bajo.

### Recorridos y movimiento (`path.ts`, `rules.ts` `moveAlong`)

- **`stepCost(s, desde: Pos, dx, dy)`** devuelve `{ nodo, coste }` o `null`. En la columna vecina busca, por este orden:
  1. una superficie a la misma altura: andar;
  2. una a +1, si se sale de una escalera: subir, con `COST_CLIMB`;
  3. la superficie más alta por debajo, a `MAX_DROP` como mucho y sin nada en medio: saltar, por ejemplo desde la azotea de un edificio de dos plantas.
- **Diagonales:** solo a la misma altura, y con las dos columnas de los lados pisables a esa altura, como hoy.
- **`computeReach`:** Dijkstra sobre nodos (≤ 4 · W · H = 9216; hoy 2304).
  - Los enemigos bloquean su nodo, no la columna.
  - Hay que mantener el orden de exploración de la baja, para que los mapas sin plantas den los mismos caminos.
- `destinations` y `buildPath` devuelven `Pos`.
- **`moveAlong`:** las puertas y ventanas del camino cambian en su nivel. La vigilancia en cada paso sigue igual, con `canSee` en 3D.
- **Comandos:** `move.to` es `Pos`.
- **Coste:** M. **Riesgo:** medio (determinismo del orden del Dijkstra).

### Escaleras

- **La regla de la escalera de mano de hoy:**
  - una casilla `ladder` en el nivel `z`, y el rellano en la casilla de al lado, en `z + 1`;
  - cuesta `COST_ORTHO + COST_CLIMB`, lo que pide FASE6.md;
  - sobre la escalera, el nivel `z + 1` es `null`: el hueco, por donde también se ve.
- **`DecorKind` gana `'stairs'`,** para que el cliente dibuje una escalera y no una de mano.
  - No hace falta un `TileKind` nuevo, así que no cambian los costes ni las tablas de altura y cobertura.
  - Si Arte prefiere un `TileKind` propio, la regla es la misma.
- **Opcional:** saltar por una ventana de arriba a la calle, como bajada de hasta `MAX_DROP` plantas.

### Visión en 3D (`los.ts`)

- **`lineClear` con capas.** Mismo recorrido de casillas y misma regla de esquinas que hoy. En cada casilla:
  - la línea, a la altura que tiene ahí (la misma proyección de hoy), choca si `opaqueAt` dice que algo tapa;
  - entre dos casillas seguidas del recorrido, incluidas la primera y la última, choca si un forjado de cualquiera de las dos columnas queda entre las dos alturas: la línea ha cruzado un suelo o un techo. Esto generaliza `roofSide`;
  - si el blanco está justo encima o debajo, en la misma columna: choca si hay un forjado entre `ha` y `hb`. Hoy ese caso devuelve «libre» porque el recorrido no tiene pasos.
- **Ventanas de las plantas:** son huecos en su planta. Se ve hacia fuera y hacia dentro, como en la baja (el punto 3 de la prueba, en cada planta).
- **Sin capas, el código de hoy tal cual.** Un test lo comprueba con miles de líneas al azar, como el de `night.test.ts`.
- `peekOrigins`: el paso a un lado se hace en el mismo nivel.
- `sightOrigin`, `canSee` y `canSeeTile` trabajan con `Pos`.
- **`teamVisibility`:** una máscara por nodo, de largo W · H · niveles, con la baja delante: la niebla del cliente sigue valiendo en la planta baja. Solo se miran nodos con superficie.
- `lightTiles` y `litMask` usan nodos. Las farolas están en la baja; las Bengalas, donde caigan.
- **Ventaja de altura** (`aim.ts`): por `heightAt`.
- **Coste:** L, la parte más delicada. **Riesgo:** alto.
  - Corrección: que se cuele una línea por el borde entre dos casillas.
  - Rendimiento: los nodos casi se duplican (unos 1700 más por ciudad), así que `teamVisibility` cuesta 1,7–2 veces más; calculo 3–6 ms.

### Cobertura, flanqueo y cuerpo a cuerpo (`cover.ts`, `grid.ts`, `aim.ts`)

- **`coverFrom(s, de: Pos, lado)`** mira lo que hay en la columna del lado, en el nivel de la unidad:
  - pared, puerta, ventana o `high`: cobertura alta;
  - `low`: baja;
  - un bloque macizo más alto que la unidad: alta;
  - aire, como el borde de una azotea sin antepecho: nada.
- `coverSides`, `hasAnyCover` y `coverAgainst` reciben `Pos`. El flanqueo, por dirección en el plano, no cambia.
- **`inMeleeReach`:** a la misma altura, como hoy, con `heightAt`.
- **Coste:** S. **Riesgo:** bajo.

### Ruido, detección, explosiones y zonas (`rules.ts`, `abilities.ts`, `los.ts`)

- **Ruido:** se oye si la distancia en el plano, más 1 por cada planta de diferencia, cabe en el radio. Los forjados amortiguan un poco.
  - Los zombis van al nodo donde lo oyeron, escaleras incluidas.
- **Detección:** el mismo alcance; decide la visión en 3D.
- **Explosiones** (`blastTiles`, `explode`):
  - el mismo radio en el plano, pero solo en los nodos del mismo espacio: como mucho una planta de diferencia y ningún forjado en medio, así que los suelos paran la onda;
  - el daño al decorado (`BLAST_RESULT`) se hace en el nivel del nodo;
  - los forjados no se rompen en esta fase.
- **Humo, Silencio, auras y ayudas:** alcance en el plano y misma franja de planta, con una función `sameSpace(s, a, b)`.
- **Lo que pide estar al lado** (cada sitio, con una función `beside` en Chebyshev 1 a la misma altura):
  - registrar y abrir puertas;
  - las ayudas;
  - el mordisco y el zarpazo;
  - las casillas donde se levanta un zombi;
  - rescatar al VIP y coger el objeto.
- **Coste:** M (muchos sitios pequeños). **Riesgo:** medio: olvidar un sitio, y que un zombi muerda a través de un suelo.
  - Para evitarlo: la lista de sitios sale de buscar `distance(`, `adjacent(` y `sameTile(` (hoy unos 60 en el motor), más un test por familia de habilidades.

### IA (`ai.ts`)

- Trabaja con `Reach`, `destinations` y `canSee`, así que se mueve en 3D sin más.
- **Puntuación:**
  - los humanos valoran la altura sobre el blanco y no bajan sin motivo si están apostados arriba;
  - los zombis van a lo que oyen o a lo que ven.
- **Patrullas:** los puntos de paso llevan `z`. Un pod apostado arriba patrulla dentro de su planta o se queda quieto.
- **Coste:** S–M. **Riesgo:** medio: que una unidad se atasque en una escalera, y el tiempo del test de la horda de veinte.

### Generador (`mapgen.ts`)

- **Una pasada nueva después de `storeysOf`,** `upperFloors`, con su propia semilla como `storeysOf`. La planta baja no se mueve, salvo la casilla de la escalera.
- **Cada edificio de n ≥ 2 plantas, plantas 1 a n-1:**
  - las paredes exteriores con la misma planta que abajo;
  - ventanas en cada fachada, con la pauta de `windows()`;
  - habitaciones (`splitRooms` o pasillo) con sus puertas;
  - muebles registrables (`furnishRoom`, con `decor` por planta);
  - escaleras de la baja a la 1, de la 1 a la 2 y de la última a la azotea.
- **La azotea:**
  - tiendas y oficinas: plana, con antepecho (`parapet()`);
  - casas de dos plantas: a dos aguas (`'roof'`, sin acceso).
- **Edificios de una planta:** forjado `'roof'` sin acceso. La visión queda como hoy, porque el tejado corta las líneas.
- **El lienzo por planta:** `Canvas` gana un nivel, o hay un `LevelCanvas` que comparte las funciones. Así `building`, `splitRooms`, `windows` y `furnishRoom` dibujan en una planta alta sin duplicarse.
- La transformación final (simetrías y giro) se aplica a cada capa, y el `decor` de cada capa gira con `Dc`.
- **Alcance:** `connect` y `flood` en 3D, por las escaleras: toda planta tiene que poder alcanzarse.
- **Quién va arriba:** algunos pods de humanos empiezan arriba. Las salidas, la evacuación y las llegadas de hordas siguen en la baja.
- **Botín:** muebles registrables arriba; `searched` por nodo.
- `MissionLayout` gana `levels`, y `createMission` los copia.
- **Coste:** L, la parte más grande. **Riesgo:** medio: que se rompan los tests del generador (aberturas, alcance), y el tiempo de generación.

### Eventos y red (`events.ts`, `protocol.ts`)

- Las posiciones de los eventos pasan a ser `Pos`. `applyEvent` cambia `tileChanged` en su capa, y `searched` guarda nodos.
- **No hacen falta eventos nuevos:** subir es un paso de `moved` en el que cambia la altura.
- **Tamaño:**
  - las capas añaden unos 35–45 KB a los 58 KB de hoy (W · H · 3 casillas, casi todas `null`);
  - el estado completo solo se manda al entrar, al reconectar y al empezar una misión; los eventos solo ganan `"z":n`;
  - si hiciera falta, cada capa se puede codificar como una cadena de un carácter por casilla. No hace falta ahora.
- **Coste:** S. **Riesgo:** bajo. Lo cubre un test de réplica en un mapa con plantas.

### Partidas guardadas

- **Campaña:** nada. No guarda posiciones de combate, y `CAMPAIGN_VERSION` no cambia.
- **Misión en curso:** pasa por `normalizeMission`. Sin `levels` usa la visión de hoy (`storeys`), así que se juega exactamente igual. `SAVE_VERSION` no hace falta subirlo.
- **Mapas a mano:** sin capas, como hoy.
- **Coste:** S. **Riesgo:** bajo.

## 3. Coste y riesgo

| Parte | Coste (sesiones de Reglas) | Riesgo | Qué puede romperse |
|---|---|---|---|
| Datos, rejilla y normalización | 1 | bajo | nada visible: sin capas todo es igual |
| Movimiento y escaleras | 1–1,5 | medio | determinismo del Dijkstra; enemigos que bloquean otra planta |
| Visión en 3D | 1,5–2 | alto | líneas que se cuelan por un forjado; rendimiento de la niebla |
| Cobertura, cuerpo a cuerpo, ruido, explosiones, zonas | 1 | medio | sitios olvidados (mordiscos y ayudas a través del suelo) |
| Generador | 2–3 | medio | tests del generador; alcance; tiempo de generación |
| IA | 1 | medio | atascos en escaleras; tiempo del turno enemigo |
| Red, guardado y presupuestos | 0,5 | bajo | tamaño del estado |
| **Total** | **8–10** | | |

**Lo que hay que vigilar:**

- **Réplica.** Todo está en el motor y llega por eventos. Hay que añadir el test de réplica de `rules.test.ts` en un mapa con plantas.
- **Determinismo:**
  - el orden de los nodos de la baja no cambia;
  - el generador gasta su propio `Rng`, así que la planta baja de una semilla sigue igual salvo las escaleras.
- **Rendimiento a 48 × 48 × 4.** Presupuestos con test, medidos antes y después:
  - `teamVisibility` ≤ 6 ms;
  - `computeReach` ≤ 1 ms;
  - el turno de veinte zombis, dentro del límite del test actual;
  - `createMission` ≤ 40 ms.

  También hay que probar con Safari, en el equipo de Ana: la niebla se calcula en cada cliente.
- **Un `z` que se pierde:** lo evita el `Pos` obligatorio. Las funciones que reciben `Vec2` e ignorarían el `z` cambian de firma.

## 4. Pasos (cada uno con sus tests)

1. **Datos y rejilla.**
   - `Pos`, `Level`, `levels`, las funciones nuevas, `normalizeMission`, y `z` opcional en el protocolo.
   - Aún no hay capas en los mapas.
   - Tests: las funciones; la normalización; los 307 de hoy sin cambios. El cliente compila sin tocarlo.
2. **Movimiento y escaleras.**
   - `stepCost` en 3D, el alcance por nodos, las escaleras, los saltos, y `z` en los comandos y los eventos.
   - `stateFrom` gana filas ASCII por planta para escribir edificios en los tests.
   - Un mapa a mano pequeño con un edificio de dos plantas («la torre») para escaramuza, para que Arte y Combate empiecen antes de que haya generador.
   - Tests:
     - subir y bajar: costes y acciones;
     - no se atraviesan los suelos;
     - un enemigo arriba no tapa el paso abajo;
     - las puertas y ventanas de arriba se abren y se rompen en su planta;
     - réplica.
3. **Visión y cobertura.**
   - `lineClear` con forjados, `teamVisibility` por nodo, cobertura por nivel, cuerpo a cuerpo y ventaja de altura.
   - Tests:
     - una ventana de arriba hacia la calle y desde ella;
     - el suelo tapa la planta de abajo;
     - el hueco de la escalera;
     - el borde de la azotea, con y sin antepecho;
     - los mapas sin capas, idénticos;
     - presupuesto de tiempo.
4. **Ruido, explosiones y zonas.**
   - `sameSpace` y `beside` en cada sitio de la lista.
   - Tests por familia: mordisco, ayudas, registrar y puertas, granadas a través del suelo, humo.
5. **Generador.**
   - Plantas de verdad, escaleras y azoteas; pods arriba; botín arriba.
   - Tests:
     - todo nodo pisable se alcanza desde la salida;
     - las escaleras con su rellano y su hueco;
     - ventanas en cada fachada de cada planta;
     - muebles registrables por planta;
     - determinismo por semilla;
     - la planta baja igual salvo las escaleras;
     - presupuestos de tamaño y de tiempo.
6. **IA.** Tests:
   - los zombis suben a lo que oyen;
   - un pod apostado arriba conserva la altura;
   - el tiempo de la horda.
7. **Cierre.**
   - Repaso de equilibrio (que los humanos apostados arriba no sean demasiado fuertes) y docs.
   - Textos para Lore, si los hay, en `TEXTOS-PENDIENTES.md`. Los del indicador de planta son del cliente.

Arte y Combate pueden empezar al acabar el paso 2, con «la torre», y tener mapas generados al acabar el paso 5.

## 5. Lo que necesita el cliente

- **Arte · Mapas** (`buildings.ts`, `mapView.ts`, `fogOfWar.ts`, `coords.ts`):
  - Dibujar las capas: suelos, paredes, puertas y ventanas por planta, desde `levels`. Hoy las plantas altas salen de `storeys` y son solo fachada; con capas, son las de verdad.
  - Los muebles de `levels[k].decor`, la escalera (`'stairs'`) y su hueco, y los dos tipos de tejado.
  - **El corte por plantas, como en XCOM:** se ve la planta activa y las de abajo; las de arriba se ocultan. El fundido de tejados por edificio de hoy pasa a ser por planta.
  - La niebla por nodo (la baja, como hoy) y la noche por planta.
  - `tileChanged` con `z`.
  - `coords.groundAt(p)` usa la altura de `p`, con su `z`.
- **Combate** (`game.ts`, `overlays.ts`):
  - **Elegir planta:** el cursor elige con rayos contra los suelos visibles; la rueda o unas teclas cambian de planta; por defecto, la de la unidad elegida.
  - El alcance por nodo (`reach.cost`, con la baja igual que hoy) y el camino con `z`.
  - El indicador de planta.
  - `previewShot`, granadas y casillas con `Pos`.
  - Botones de puerta por planta.
  - La cámara sube con la planta.
- **Arte · Personajes:** la animación de subir cuando un paso de `moved` cambia de altura por una escalera; saltar desde una azotea.
- **Interfaz:** el indicador de planta en el HUD.
- **Red:** nada más que un estado más grande. Probarlo en el equipo de Ana.

## 6. Decisiones de Gameplay (13/10)

El plan está aprobado: el modelo de datos, `Pos` con `z` obligatorio, el índice de nodo y los siete pasos. Las respuestas a las preguntas:

1. **Azoteas de una planta:** la mitad de las planas, más o menos, según la semilla, llevan escalera de mano por fuera, como el acceso a las azoteas de hoy. Las demás, sin acceso (`'roof'`).
2. **Casas de dos plantas:** tejado a dos aguas, sin acceso.
3. **Ruido a través de los suelos:** +1 de distancia por cada planta que cruza.
4. **Saltar por una ventana de arriba a la calle:** más adelante, no en el paso 2. «Saltar por la ventana», del Oficio, sigue siendo de la planta baja por ahora.
5. **Suelos:** paran las explosiones y no se rompen en esta fase.
6. **Escaleras:** `ladder` con `decor` `'stairs'`.
7. **Humanos arriba:** como mucho un pod por mapa, desde la dificultad 2. Los zombis pueden subir si su camino los lleva.

## 7. Estado

**Pasos 1 y 2 hechos (13/10).**

- **Datos:**
  - `Pos` y `Level`, y `GameState.levels`;
  - `'roof'` en `TileKind` y `'stairs'` en `DecorKind`;
  - en `grid.ts`: `nodeOf`, `posOf`, `kindAt`, `heightAt`, `slabAt`, `surfaceAt`, `samePos`, `toPos` y `ground`;
  - `normalizeMission`, que el servidor llama al cargar una partida;
  - `z` opcional en los comandos, en los avisos y en las marcas.
- **Un cambio sobre el plan:** las consultas que llama el cliente con una casilla (`unitAt`, `buildPath`, `canTargetTile`, `blastTiles`, `searchSpot`, `wasSearched`, `reachesToSearch`, y `from` en `previewShot`) aceptan un `Place`, es decir, un `Pos` con `z` opcional, que sin `z` es la planta baja.
  - Así el cliente compila sin tocarlo.
  - El estado y los eventos siguen guardando siempre un `Pos`.
- **Movimiento** (`path.ts`):
  - `stepBetween` es la regla en cualquier nivel;
  - `stepsFrom` da los pasos posibles hacia una casilla: el suelo a la misma altura, el de arriba desde una escalera, y el más alto de abajo;
  - `computeReach` recorre nodos.
  - Sin capas, todo usa `stepCost` tal cual: un test lo compara paso a paso en mapas generados.
- **«La torre»** (`maps.ts`, `torre`): un mapa a mano para escaramuza.
  - Una torre de tres plantas con escaleras hasta la azotea, y dos Cuervos en la primera planta.
  - Un cobertizo con azotea y escalera de mano, y una casa con tejado a dos aguas.
  - `storeys` sale de las capas.
- **Mientras no esté el paso 3,** la visión de quien está arriba es todavía la de la planta baja de su casilla.

**Paso 3 hecho (13/10): visión y cobertura en 3D.**

- **Visión** (`los.ts`):
  - Con capas, `lineClear` usa `lineThroughStoreys`. Lo que hay en cada planta tapa a su altura (`opaqueAt`: paredes y puertas cerradas toda la planta; las ventanas, nada).
  - Los forjados cortan la línea entre dos casillas, en la casilla donde la cruza; también si el blanco está justo encima o debajo.
  - Sin capas, el código de antes tal cual. Un test lo compara con una copia del de la fase 5, en miles de líneas y en la niebla de dos ciudades.
- **Niebla:**
  - `teamVisibility` da una máscara por nodo (la planta baja primero); arriba solo se calculan las casillas con forjado.
  - `floorLevels` y `floorView` dan, para una planta del corte, el nivel que se ve en cada casilla y su valor de la máscara.
  - Las luces van por nodo.
- **Cobertura** (`coverAt`): cuenta lo que hay en la planta de la unidad. El borde de una azotea, sin nada. Los pasos para asomarse se quedan en la misma planta.
- **Coste** (ciudad con plantas simuladas, de 1500 a 2500 casillas de más): la niebla, 1,3–2,9 ms la escuadra y 3,0–4,8 ms los enemigos. Hay un test de presupuesto.

**Paso 4 hecho (13/10): ruido, explosiones y zonas.**

- **Ruido:** +1 de distancia por planta de diferencia (`storeysApart`).
- **`sameSpace`:** como mucho una planta de diferencia y ningún forjado en medio en ninguna de las dos columnas. Lo usan:
  - las explosiones (`blastTiles`: el decorado se rompe en su planta y los forjados no se rompen);
  - el humo y el Silencio;
  - las auras (cantos, Juramento, Restauración, «A dos voces», calma, «Llave doble»);
  - las ayudas a mano (alcance 1,5).
- **Lo que pide estar al lado,** en la misma planta: el terminal, registrar, las puertas, el VIP y el objeto.
- El Grillo sigue llegando a 15 casillas, con alguna planta de por medio o sin ella.
- Sin capas, nada cambia.

**Paso 5 hecho (13/10): el generador** (`mapgen.ts`, `upperFloors`, con su propia semilla, después de todo lo demás).

- **Plantas de verdad:** cada edificio de 2 o 3 plantas tiene:
  - en cada planta, paredes, habitaciones con puertas y ventanas;
  - las ventanas nunca dan a otra planta vecina;
  - muebles registrables (las de una tienda son su almacén);
  - una escalera desde la planta de abajo, contra una pared y con su hueco, que nunca parte la planta en dos.
- **Azoteas:** planas, con antepecho y la escalera, en tiendas y oficinas de 2 o 3 plantas.
  - Las casas y los pajares tienen tejado a dos aguas (`'roof'`), sin acceso.
  - Los edificios de una planta y azotea plana llevan una escalera de mano por fuera más o menos la mitad de las veces; los demás, `'roof'`.
  - Las ruinas, sin techo.
  - Las plantas a las que no llega ninguna escalera se quedan en tejado.
- **Los dos estilos de ciudad** (en cuadrícula y casco viejo) funcionan, porque se trabaja sobre la lista de edificios.
- **La planta baja** solo cambia donde van las escaleras y las escaleras de mano, que esquivan las salidas, los pods, el objetivo, la evacuación y las farolas.
- **Humanos arriba:** el generador ofrece un sitio junto a una ventana, lejos de la escuadra (`perch`). Desde la dificultad 2, `createMission` aposta allí el primer grupo que sea todo de vivos.
- **Medido en 8 ciudades reales:**
  - unos 1900 sitios de más por mapa;
  - generar, 8 ms;
  - el estado, 107 KB (58 antes);
  - la niebla, 1,3 ms la escuadra y 3,9 ms los enemigos (como mucho 5,0);
  - el alcance de una carrera, 0,27 ms;
  - el turno enemigo, 5,9 ms (como mucho 12,8).
  - El recorrido de la línea por las plantas se reescribió para que fuera más rápido.

**Paso 6 hecho (13/10): la IA** (`ai.ts`).

- **Al andar, cada planta de por medio cuenta 3 casillas más** (`farness`): es la vuelta hasta la escalera.
  - Así un zombi que oyó algo arriba sube por la escalera, en vez de quedarse debajo.
  - «Haber llegado» pide estar en la misma planta.
  - Lo usan el ruido, las patrullas, el seguir al jefe, el elegir a quién ir a por él y el bocado.
- **Los vivos valoran la altura:** +6 por planta sobre el soldado más cercano.
  - Los apostados arriba no bajan al despertar.
  - Los zombis suben si su camino los lleva.
- **Sin capas no cambia nada.**
- **Coste:** con plantas, un turno enemigo cuesta unas 2 veces más que en el mismo mapa sin ellas (5,0 frente a 2,2 ms), y una horda de 20, 68 frente a 36 ms por turno, con la máquina cargada. Se calcula solo en el anfitrión.

## 8. Cierre (13/10)

**Fase 6 cerrada en las reglas.** Los siete pasos están hechos. Lo que queda es del cliente: dibujar las plantas, el corte por planta y elegir la planta.

### Equilibrio

**Las pruebas:**

- **Saqueo, de antes y de ahora:** el bot de siempre (`scavenge-sim`: seis reclutas que registran la planta baja y se van) jugó 16 ciudades por dificultad.
- **Misma ciudad, con plantas y sin ellas:** un bot nuevo que sube, registra arriba y caza, jugó cada ciudad dos veces. En la versión sin plantas, los apostados se bajaron a la calle.
- **Campaña:** 12 campañas de 56 días con combates aproximados.

**El Saqueo, contra las versiones anteriores** (en tácticas, b56a5b4 y 8bc98a8 dan lo mismo):

| | b56a5b4 / 8bc98a8 | Ahora (con plantas) |
|---|---|---|
| Dificultad 2 | 16/16 victorias, 1,0 bajas | 16/16, 1,0 |
| Dificultad 3 | 16/16, 1,1 bajas | 15/16, 1,8, 1 escuadra perdida |

La dificultad 3 se endurece un poco, pero no por las plantas: en la misma ciudad sin plantas, el bot nuevo pierde lo mismo (1,8 bajas). Viene de los demás cambios desde 8bc98a8.

**Misma ciudad, sin plantas → con plantas:**

| Misión | Reclutas | Veteranos |
|---|---|---|
| Saqueo, dificultad 2 | 16 → 14 victorias; 1,0 → 1,1 bajas | 15 → 13; 0,6 → 0,4 |
| Saqueo, dificultad 3 | 14 → 13; 1,8 → 1,2 | 11 → 12; 1,1 → 0,9 |
| Eliminación, dificultad 2 | 13 → 12; 1,9 → 1,8 | 16 → 15; 0,3 → 0,7 |
| Eliminación, dificultad 3 | 3 → 4; 3,6 → 3,6 | 13 → 12; 2,3 → 1,7 |

**Lo que dicen los números:**

- Las diferencias son de 1 o 2 partidas sobre 16, dentro del ruido.
- Las partidas sin acabar en 20 turnos suben un poco con plantas. Las que se miraron una a una eran límites del bot:
  - un grupo apostado y dormido en una habitación que el bot no buscó;
  - el último soldado sin ir a la evacuación.
- **Los vivos apostados arriba** disparan de 0,7 a 1,8 veces por misión desde arriba, aciertan 3 de cada 4 y hacen de 1,5 a 5 de daño. Pesan, pero no deciden.
- **Los zombis** suben poco (de 0,1 a 0,3 por misión) porque el bot casi no sube. Cuando un soldado está arriba, lo siguen por la escalera (hay tests).
- **Campaña** (12 de 56 días, combates aproximados):
  - ahora, 3 derrotas, de media el día 49;
  - 8bc98a8, 3, el día 45;
  - b56a5b4, 4, el día 45.

  Igual: las plantas no cambian el modelo de combate de la campaña.

**Propuesta:** dejar +6 por planta de altura y 3 casillas de rodeo por planta. Si las partidas reales lo piden:

- altura de 4 (menos apostados) a 8 (más);
- rodeo de 2 (los zombis suben antes) a 4.

**Arreglado en el cierre:** la IA no planeaba bien un camino a otra planta cuando la escalera quedaba lejos, y un vivo podía quedarse justo encima del soldado. Ahora un camino a otra planta se planea hasta 40 acciones por delante (`LOOKAHEAD_STOREYS`), con su test.

### Queda para más adelante

- Saltar por una ventana de arriba (decisión 4).
- Suelos y muros destructibles (decisión 5).
- Escaleras con un solo lado de salida.
- Plantas sobre bloques macizos.
- **Que el estado pese menos** (107 KB frente a 58), si alguna vez molesta:
  - deflate en el WebSocket, una línea en el servidor, que Red tendría que probar en Safari;
  - o las capas como cadenas de un carácter por casilla, con codificar y descodificar en `net.ts` y en el guardado.
  
  No se ha hecho: no es barato sin tocar el cliente.
