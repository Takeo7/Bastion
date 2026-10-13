# Bastión — documento de diseño

## Objetivo

Un juego táctico por turnos con la jugabilidad y las mecánicas de XCOM 2, pensado **desde el primer día para dos jugadores cooperando**, cada uno en su PC. Ninguno de los dos se queda mirando: ni en combate ni en la base.

## Principios

1. **Fidelidad mecánica a XCOM 2.** Los valores iniciales salen de la configuración del juego original (convertidos a casillas de 1,5 m). Así el juego "se siente" bien antes de ajustar valores propios.
2. **Cooperación real.** Cada jugador tiene sus soldados y las decisiones de ambos importan.
3. **Un único estado verdadero.** El host decide. Los clientes nunca calculan dados, así que no puede haber desincronización.
4. **Historia propia.** El mundo, el enemigo y los personajes están en [LORE.md](LORE.md); los textos del juego salen de `engine/src/lore.ts` y de los nombres de `content.ts`, `progression.ts` y `campaign.ts`. Los ids del código conservan los nombres de XCOM 2 (`sectoid`, `doom`…).
5. **Arte sustituible.** Todo lo visual está aislado en `client/src/game/`: `figures.ts` (figuras articuladas con esqueleto y `applyPose`, de la sesión de arte), `models.ts` (`buildUnit` usa una figura para cada plantilla de `FIGURE_TEMPLATES` y el modelo provisional como respaldo) y `mapView.ts`. Las animaciones de `unitViews.ts` se expresan como una pose (agacharse, apuntar, correr, retroceso, impacto, caída…) que cada modelo interpreta.

## Arquitectura

```
Cliente A ──comando──▶ Host (Node) ──eventos──▶ Cliente A y Cliente B
                       ├─ valida el comando
                       ├─ tira los dados (RNG con semilla)
                       └─ produce eventos (moved, shot, damaged…)
```

- **`engine`** es TypeScript puro, sin DOM ni Node. Lo usan el servidor (para jugar) y el cliente (para previsualizar rutas, porcentajes y coberturas).
- **Event sourcing.** Todo cambio de estado es un evento. El host y los clientes aplican los mismos eventos con el mismo reductor (`applyEvent`). Un test reproduce partidas enteras con un bot y comprueba que el cliente termina con un estado idéntico al del host.
- **Dos copias del estado en el cliente:**
  - `state`: lo último recibido. Se usa para la entrada y las previsualizaciones, así que puedes planear mientras se animan las acciones de tu compañero.
  - `view`: lo que se está mostrando. Avanza al ritmo de las animaciones.
- **El mando.** En el turno de la escuadra solo actúa quien tiene el mando (`GameState.command`). Se cede con la orden `pass` (evento `commandPassed`), pasa solo al terminar el turno o al quedarse sin acciones (`autoPassCommand`) y cada turno lo abre un jugador distinto (`openingCommander`). Si quien lo tiene se desconecta, el primero que actúa lo toma (`syncTurn` también lo reasigna).
- **Reconexión.** Cada asiento tiene un token guardado en el navegador. Al reconectar recibes el estado completo. Si se detecta un hueco en la secuencia de eventos, el cliente pide un estado nuevo. Un jugador que se cae sigue bloqueando el fin de turno durante 30 s, así que una recarga o un corte breve no le hacen perder el turno.

### Presentación (animación y cámara)

- **Animación procedural** sobre un esqueleto mínimo (cadera, hombro, boca del arma). Las poses persistentes (agazapado, vigilancia, muerto) se combinan cada fotograma con las transitorias (correr, apuntar, retroceso, impacto, lanzamiento).
- **Disparo:** el tirador se gira, se asoma si dispara desde una esquina, levanta el arma y lanza una ráfaga de tres proyectiles con fogonazo. Si falla, las balas pasan de largo. Después llega el impacto con número de daño y retroceso del objetivo.
- **Cámara cinemática** solo para tus acciones y para el turno alien. Recuerda el "turno de cámara" en que empezó y se detiene si mueves la cámara o eliges otro soldado.
- **Selección diferida:** el soldado que actúa sigue seleccionado hasta que termina su animación y una breve pausa para ver el resultado. Después pasa al siguiente con acciones.
- La entrada se bloquea hasta que el turno alien termina de verse, no solo cuando el servidor lo resuelve.
- **Sonido provisional** sintetizado con WebAudio, sin ficheros de audio.
- **Persistencia.** El host guarda la sala tras cada acción con escritura atómica: el asiento de cada jugador, la fase (menú, base o misión), la campaña, el estado de la misión y el RNG. El formato lleva versión: un guardado de una versión anterior se descarta con un aviso.

### Tecnologías

| Pieza | Elección | Motivo |
|---|---|---|
| Lenguaje | TypeScript en todo | Un solo motor de reglas compartido entre cliente y servidor |
| 3D | Three.js | Estándar en web, control total y ligero |
| Cliente | Vite | Recarga en caliente instantánea y build simple |
| Red | WebSocket (`ws`) con protocolo propio validado con `zod` | Turnos con pocos mensajes; el protocolo de eventos es lo que mejor encaja |
| Servidor | Node + `tsx` | Ejecuta el TypeScript directamente, sin paso de compilación |
| Tests | Vitest | Rápido y nativo con TypeScript |
| Por internet | Tailscale | Red privada entre los dos PCs sin abrir puertos |

## Mecánicas implementadas (M1)

| Mecánica | Valor | Origen en XCOM 2 |
|---|---|---|
| Puntos de acción | 2 por turno. Disparar gasta una acción (el fusil de precisión, las dos: `apCost` 2); vigilancia, agazaparse, granada y cuerpo a cuerpo terminan el turno | `bConsumeAllPoints`. **Cambio sobre XCOM 2** (prueba del 2026-10-12): allí disparar termina el turno. La IA sigue disparando una vez por turno |
| Movimiento | 8 casillas por acción, 16 en carrera; las diagonales cuestan 1,5 | Movilidad 12 m |
| Visión | 18 casillas | `SightRadius` 27 m |
| Cobertura | Baja −20, alta −40; direccional; sin cobertura = flanqueado | `LOW/HIGH_COVER_BONUS` |
| Flanqueo | +50 % de crítico para los soldados, +33 % para los aliens | `FlankingCritChance` |
| Alcance | Tablas corto/medio/largo por casilla | `*_CONVENTIONAL_RANGE` |
| Fuego de reacción | −30 % de impacto, sin crítico | `REACTION_FINALMOD` |
| Agazaparse | +30 de defensa, +50 de esquiva (rozaduras al 50 % de daño) | `HUNKERDOWN_*`, `GRAZE_DMG_MULT` |
| Asomarse | Desde cobertura alta puedes salir a la casilla lateral para ver y disparar | *Step-out* |
| Ocultación | La misión empieza oculta. Los aliens solo detectan a 8 casillas; atacar rompe la ocultación | `DetectionRadius` 12 m |
| Grupos enemigos | Inactivos hasta el contacto. Al activarse corren a cubrirse (*scamper*) y pueden disparar a quien esté en vigilancia | Pods |
| Granada | 3 de daño (20 % de +1), radio 2,5, alcance 10 a cualquier casilla que vea la escuadra; daña también a los tuyos (se marcan en rojo al apuntar), destruye cobertura y alerta a grupos cercanos | Frag grenade |
| Soldado | 5 de salud, 65 de puntería, fusil 3-5 (crítico +2), cargador de 4 | Rookie y fusil convencional |

## Mecánicas añadidas en M2b y M4

| Mecánica | Valor | Origen en XCOM 2 |
|---|---|---|
| Alturas | Cada casilla tiene elevación en pisos (`GameState.elev`); la línea de visión se traza en 3D desde los ojos (0,7 pisos) hasta el cuerpo (0,55) | Niveles de suelo |
| Ventaja de altura | +20 de puntería disparando hacia abajo | `HEIGHT_ADVANTAGE_BONUS` |
| Cobertura con alturas | Relativa a la elevación de quien se cubre: el borde de una azotea no cubre a quien está arriba, la pared de un bloque sí cubre a quien está al pie | Cobertura por altura del objeto |
| Escaleras y caídas | Subir solo por escalera (+1 casilla); bajar desde cualquier borde (+1 casilla por piso, hasta 2) | Escalar y saltar |
| Plantas | Edificios de 2 y 3 plantas con habitaciones, ventanas y escalera interior (sube como una escalera de mano); azoteas planas con antepecho (cobertura baja). Los suelos tapan la vista y paran las explosiones; el ruido cuenta +1 casilla por planta. Los vivos apostados arriba valoran la altura (+6 por planta) y los zombis suben (3 casillas de rodeo por planta) | Edificios de varias plantas |
| Puertas | Tapan la vista; se abren al cruzarlas y quedan abiertas; las explosiones las destrozan | Puertas |
| Ventanas | Transparentes, cobertura alta al lado, saltarlas cuesta +1 casilla y rompe el cristal | Ventanas |
| Patrullas | Los grupos inactivos caminan entre puntos de ruta; si ven a la escuadra durante su turno, se activan y corren a cubrirse | Pod patrols |
| Refuerzos | Bengala visible un turno antes; caen al final del turno alienígena ya activos | Reinforcement flare |
| Enfriamientos | Turnos del propio equipo; acciones gratuitas (coste 0) que exigen tener acciones | Ability cooldowns |
| Presupuesto alienígena | Un explosivo y un Canto por turno para todo el equipo | Cooldowns globales |
| Canto | Éxito = 70 − (voluntad − 40); efecto 50 % desorientado, 30 % pánico, 20 % control mental (2 turnos o hasta que muera el Cantor) | Sectoid Mindspin |

## Modelo cooperativo

### Combate (implementado)

- Cada jugador manda en la mitad de la escuadra. El color del jugador se ve en la armadura, el anillo y la lista.
- **Los dos actuáis a la vez** durante el turno de la escuadra. El host procesa las órdenes en orden de llegada.
- El turno alien empieza cuando todo jugador con soldados con acciones está **listo** (Retroceso). Un jugador desconectado nunca bloquea el turno.
- **Presencia:** ves qué soldado tiene seleccionado tu compañero (anillo de su color) y dónde tiene el cursor.

### Base (implementado, M3a y M3b)

- Base y recursos compartidos; cada jugador tiene sus soldados, los asciende, reparte sus aptitudes y talentos y los equipa.
- Los dos usáis la base a la vez, cada uno en su pantalla. Se ve en cuál está el compañero.
- **Propuestas:** despejar terreno, construir, contactar regiones, elegir misión y responder a acontecimientos las propone uno y las acepta o rechaza el otro. Solo hay una propuesta abierta a la vez. Si solo hay un jugador conectado, se aplican directamente. Lo personal (reclutar, ascender, comprar y equipar accesorios, personalizar) no se vota.
- **Listo:** el tiempo avanza y la misión despega solo cuando todos los jugadores conectados están listos. Un acontecimiento sin responder congela el tiempo y no deja despegar.
- **Tiempo:** cada misión lleva 3–4 días (ida, vuelta y recuperación) en los que pasa lo de cada día: heridas, obras, misiones nuevas y el reloj enemigo (`passDay`). Entre un capítulo de la historia ganado y el siguiente pasan al menos 8 días (`STORY_GAP`). La simulación de equilibrio del 2026-10-11 mostró que, si las misiones no gastaban tiempo, encadenarlas ganaba la campaña en menos de un mes con la base sin construir.
- **Personalización:** cada soldado de campaña tiene `nickname` y `appearance` (`engine/src/appearance.ts`: colores, cabeza, complexión). Es una orden personal (`customize`), sin propuesta. Viaja con el soldado a la misión (`Unit.appearance`, opcional: alienígenas y escaramuza no la tienen) y `buildFigure` la interpreta. `defaultAppearance` reproduce el aspecto de antes de que existiera (mezcla en espacio lineal, como THREE.Color), y `migrateCampaign` la rellena (campaña versión 3).
- **El recinto (campaña versión 7):** la base está en la superficie (`BASE_SIZE` 7 × 7 parcelas, `c.base.plots`). El búnker de mando (`BUNKER`, en el centro) y sus tres edificios fijos (`FIXED_BUILDINGS`: cuartel, Expediente y Armería) vienen hechos; el lado sur queda libre hacia el hangar, que está fuera de la cuadrícula. Se construye en una parcela despejada pegada (sin diagonales) a otro edificio, y se despeja (`clear`) una parcela junto a otra ya despejada; el coste sube con el anillo (`plotRing`) y el terreno (`TERRAINS`: roca +1 día, ruinas +15 ₡ de chatarra) sale de un hash de la parcela, igual para todos. Las pasarelas (`walkways`) unen los edificios contiguos y las **adyacencias** (`ADJACENCY`, `adjacencyBonuses`) mejoran cinco instalaciones pared con pared con su pareja. `migrateCampaign` sube la base subterránea de 3 × 3: cada instalación a la parcela libre más cercana y la obra en curso sigue con sus días.
- **Edificios y niveles (2026-10-11):** diez instalaciones (`FACILITIES`), cada una con `description` (nivel 1) y `upgrades` (niveles 2 y 3), y `BasePlot.level`. Mejorar (`upgrade`, propuesta como construir) cuesta lo de construir y ×1,5 para el nivel 3, y pide el nivel del Bastión de la instalación +2 / +4 (`upgradeLevel`). Los efectos salen de funciones del motor: `shopPrice`, `healRate`, `xpMultiplier`, `beds`/`bedsFree` (8 del búnker + barracones), `rangeAim` (en `squadMember`), `garageCut` (días de misión y de herida), `listeningDelay` (reloj enemigo), `workDays` (brigada de obras) y `contactCapacity`. Las obras duran 2–6 días y hay dos cuadrillas (`c.base.works`, tres con la brigada: `workCrews`; campaña versión 8): con una sola obra a la vez, la simulación dejaba la base a medias y casi sin mejoras, con los créditos amontonándose. Los cuerpos pagan la mitad que antes (2–6 ₡). Con todo ello, la simulación da ~50 % de campañas ganadas a un jugador normal, ~9 mejoras por campaña y un saldo que ya no pasa de unos cientos de créditos.
- **Escuadra:** cada jugador aporta hasta 3 soldados propios (4 con el centro de entrenamiento). Los soldados de la campaña llegan al combate con su rango, habilidades, equipo y mejoras ya calculados (`UnitMods`, `charges`, `gear`), así que las reglas de combate no saben nada de la campaña.

### Progresión (campaña versión 4)

Sustituye a la investigación y a los tres recursos (suministros, inteligencia, aleaciones).

- **Créditos**, una sola moneda (`CampaignState.credits`). Entran por recompensas de misión, el pago mensual del Consejo (60 + ingresos de las regiones contactadas) y la venta de cuerpos alienígenas tras una victoria (`BODY_VALUE`).
- **Nivel del Bastión** (`engine/src/progression.ts`): `c.xp` sube con cada misión (`missionXp`: +2, +3 y la dificultad si se gana, +1 por baja; ×1,25 con la sala de simulación) y con algunos acontecimientos (`gainXp`). `LEVEL_XP` da 8 niveles. Tecnologías (`TECHS`), accesorios (`ItemDef.level`) e instalaciones (`FacilityDef.level`) se desbloquean por nivel; el nivel 8 revela la misión final.
- **Armas y armadura por soldado**, sin compra: `weaponTier(c, s)` y `armorTier(c, s)` son el mínimo entre lo que el Bastión ha desbloqueado y lo que permite el rango (`TIER_RANK`: nivel 2 desde Cabo, nivel 3 desde Teniente). `squadMember` aplica el daño (`WEAPON_TIER_DAMAGE`) y la salud y blindaje (`ARMOR_TIERS`), y los pasa a la unidad (`Unit.weaponTier`, `Unit.armorTier`) para que `buildFigure` dibuje el arma y la armadura de su nivel.
- `migrateCampaign` convierte campañas antiguas: créditos = suministros + 2 × inteligencia + 3 × aleaciones; experiencia = la suma de la de sus soldados, y como mínimo la del nivel de lo que ya se había investigado o fabricado; los laboratorios pasan a salas de simulación.

### Clases: aptitudes y el árbol de habilidades (campaña versión 10)

Elegida el 2026-10-12 entre tres propuestas (ramas, tablero y especialidades; maqueta en https://claude.ai/artifact/3PS1PmQ2mDWC86XN7khTpp).
- Primero se hicieron ramas por clase (versión 9). El mismo día, a petición del usuario, pasaron a ser un árbol común. Se llamó «la Rosa» y el 2026-10-13 pasó a ser el **Árbol de habilidades**.
- La cuarta rama de cada eje nació como el Canto de Velmar. Con el paso a los zombis es el **Oficio**, que enseña el **Taller de oficios** (`docs/LORE.md`).
- Todo vive en `engine/src/classes.ts`. Los ids internos del Canto se conservan para no romper partidas guardadas: `song`, `singer`, `SONG_GATES`, la instalación `echoes` y los ids de talentos y habilidades (`velmarOath`, `calmSong`…). El jugador no los ve.

- **Aptitudes** (`ATTRS`), del 1 al 10:
  - Pulso: +3 de puntería por punto.
  - Aguante: +1 de salud cada 2.
  - Reflejos: +2 de defensa por encima de 3.
  - Temple: +6 de voluntad.
  - Maña: +5 a arreglar por encima de 5.
  - Un recluta empieza con 5/5/5/4/3 (`RECRUIT_ATTRS`) y todo tope en 8.
  - Al especializarse gana +1 en las dos aptitudes de su clase (`CLASS_TOP`: Asalto Reflejos y Aguante, Granadero Aguante y Maña, Francotirador Pulso y Temple, Especialista Maña y Reflejos), que además llegan a 10 (`baseAttrs`, `attrCap`).
  - Extras a 7 y a 10:
    - Pulso: +10 de crítico y +1 de daño crítico.
    - Aguante: heridas un 30 % más cortas (`woundDays`) y +1 de blindaje.
    - Reflejos: +1 de movimiento y −20 a los disparos de reacción contra él.
    - Temple: sin pánico y «Nervios de acero», con el que el grito del Chillón no puede con él (`UnitMods.psiGuard`).
    - Maña: +1 granada, y +1 de curación y de daño con granadas.
- **El Árbol de habilidades:** un centro («Recluta», `CENTER`: 4 talentos para todos) y cuatro ejes (`AXES`), uno por clase.
  - Cada eje tiene tres ramas normales y una cuarta de Oficio (`BRANCHES`, `song: true`):
    - Asalto: Hoja, Escopeta, Paso y **Machete**.
    - Granadero: Demolición, Artillería, Control y **Fuego**.
    - Francotirador: Tirador, Pistolero, Vigía y **Ballesta**.
    - Especialista: Médico, Grillo (el dron casero de Teo), Enlace y **Enfermería**.
  - 116 talentos en total (`TALENTS`), con niveles (`max`), aptitud mínima (`req`) y talento previo (`needs`). 28 dan una habilidad activa, 15 de ellas del Oficio.
  - Las filas se abren con 0, 2, 4, 7 y 10 puntos en las filas de arriba de la rama (`ROW_GATES`). El Oficio tiene un talento por fila y se abre con 0, 1, 2, 3 y 4 (`SONG_GATES`).
  - Las 40 habilidades de antes siguen con su id.
- **La clase sale de las elecciones, en Capitán** (prueba del 2026-10-12). Todo soldado empieza como recluta (`CampaignSoldier.cls` null, plantilla de combate `rookie`: fusil y granada): los iniciales, los del relevo y los de encargos y acontecimientos, salvo quien llegue con rango de Capitán o más.
  - Hasta Capitán aprende de dos ejes como mucho, hasta su tercera fila, también en su Oficio si el Taller lo permite.
  - Al ascender a Capitán (`SPECIALIZE_RANK`, en `applyMissionResult`) toma la clase del eje en que más ha aprendido (`leaningClass`).
    - Cuenta 2 por cada punto de talento del eje, Oficio incluido, más los puntos subidos en sus dos aptitudes; desempatan los talentos.
    - Sale en el informe (`ReportSoldier.specializedAs`) y en el registro (`SPECIALIZE_TEXT`).
  - La clase queda fija hasta reentrenar. Un Capitán reentrenado se vuelve a especializar con 2 puntos en las ramas normales de un eje (`SPECIALIZE_POINTS`, `wouldSpecialize`).
- **Ejes y equipo:**
  - Se puede abrir un segundo eje hasta su tercera fila (`SECOND_AXIS_ROWS`), también en su Oficio. Nunca un tercero (`axesOf`).
  - Los talentos que usan el equipo propio de una clase (`kit`: espada, escopeta, cañón, fusil de precisión, pistola, Grillo) los puede aprender un recluta. «Duermen» (`dormant`: sin efecto ni habilidad) hasta que el soldado lleve ese equipo. Un soldado ya especializado solo coge los de su clase.
  - Cada eje tiene un talento de **llave doble** (`coop`) que solo funciona junto a soldados del compañero (`partnerNear` en `aim.ts`, `bridged` en `rules.ts`): Codo con codo, Parapeto, Fuego cruzado y Puente.
- **El Oficio:** su rama se abre con el Taller de oficios (`echoesLevel`, `songRowsOpen`). El nivel 1 abre las filas 1–2, el 2 las 3–4 y el 3 la 5. Los cuatro oficios mayores (la última fila de cada rama de Oficio) piden Temple 7.
  - Quien sabe algún oficio tiene `mods.singer`. Con él funciona «A dos voces»: con un soldado del compañero que también sepa uno a 2 casillas o menos, el oficio gana +1 de daño, radio o duración (`duet` en `rules.ts`).
  - Cada oficio hace su propio ruido (`AbilityDef.noise`, que atrae zombis). Con el Taller junto a la estación de escucha, hace menos (`quietSongs`, `OFICIO_QUIET`).
  - Las mecánicas de cada oficio son de Gameplay (`rules.ts`, `test/songs.test.ts`).
- **Ascensos:** cada uno da 2 puntos de aptitud y 2 de talento. Los puntos libres no se guardan: son lo que da el rango menos lo gastado (`pointsToSpend`). La veteranía suma sola +2 de puntería, +2 de voluntad y +3 a arreglar por ascenso.
- **Órdenes personales**, sin propuesta:
  - `train`: un punto de aptitud.
  - `talent`: un nivel; en un Capitán reentrenado, puede especializar.
  - La Hoja de servicio prepara el ascenso como borrador (`plan` en `screen.ts`) y manda las órdenes al pulsar «Guardar», aptitudes primero. Un sello «GUARDADO» lo confirma.
  - `retrain`: devuelve todo por `RETRAIN_COST` ₡ (30; la mitad con centro de entrenamiento) y deja al soldado como recluta.
  - `recruit`: va sin clase.
  - Los motivos de bloqueo salen de `attrBlocker` y `talentBlocker(build, id, echoes)`.
- **Cálculo:** `soldierStats(build, gear)` da:
  - la plantilla (`soldierTemplate`), los `mods`, la salud, las cargas, la voluntad y el blindaje;
  - la lista de talentos despiertos, que el combate ve como `Unit.perks`.
  - Puntería = 50 + 3 × Pulso + veteranía. Salud = 3 + Aguante ÷ 2.
  - `squadMember` añade el nivel de arma y armadura, las tecnologías, los rasgos y el campo de tiro.
  - Los soldados de escaramuza salen de la misma hoja (`autoBuild`).
- **Interfaz:** «Hoja de servicio» en el Cuartel (`promotionPanel` en `strategy/screen.ts`). La rehacen UX/UI y Gameplay en el estilo nuevo:
  - El árbol se dibuja con caminos y nodos conectados: el centro Recluta en medio y los cuatro ejes en cruz.
  - Los umbrales de fila y el nivel del Taller se ven como puertas y candados; los talentos dormidos, apagados; la llave doble, como un nodo especial.
  - Forma real de las ramas:
    - Las cuatro de Oficio son un camino recto de 5 nodos, uno por fila.
    - Las doce normales tienen 2 nodos en paralelo en las filas 1–3 (Hoja, Médico, Grillo y Enlace solo en las filas 1–2) y un nodo en las filas 4 y 5. Son caminos que se abren en dos y se vuelven a juntar.
    - Las reglas no dependen del dibujo: una fila se abre con los puntos de las filas de arriba de su rama, no por estar conectada a un nodo concreto.
- `migrateCampaign`: los soldados de antes de la versión 9 recuperan todos sus puntos, porque las habilidades viejas no encajan.
  - Los de la 9 conservan sus talentos.
  - Todos rehacen las aptitudes desde las bases nuevas y conservan su clase.
  - Lo anuncia el aviso «Nueva instrucción».
  - `persistence.ts` rellena con ceros los `mods` de una misión guardada antes.
- **Nombres:** los decide Lore y los aplica Clases.
  - El 2026-10-13 se quitaron los nombres repetidos o que chocaban con los zombis: Machetazo, Mano en el hombro, Rematador, Zancada, Tejado, Aplomo, Tiro tranquilo y Pistola certera.
  - Decidido por Arturo el mismo día:
    - La aptitud que era «Oficio» se llama **Maña** (MAÑ), para no chocar con la rama. Su id sigue siendo `ofi`.
    - Hackear pasa a ser «arreglar» (una radio, un repetidor).
    - El Grillo se queda como el dron casero de Teo. Sus protocolos son Botiquín volante, Cobertura del Grillo, Calambrazo, Espabilar y Descarga de batería; el talento que mejora arreglar es Manitas.
- **Pendiente:** el equilibrio de números, con la simulación de Gameplay.

### Ayudas al jugador

- `nextSteps(c, slot)`: lo pendiente para cada jugador, lo más urgente primero. El primero es el bloque «Ahora» de la base, y cada pestaña muestra la suma de sus pendientes.
- `defaultSquad`: al elegir misión, la escuadra viene montada con los mejores soldados listos de cada jugador conectado, una de cada clase primero.
- `addPlayer`: un compañero que entra en una campaña empezada sin él toma el mando de 4 soldados (los novatos sin estrenar del otro, o reclutas). El servidor lo llama al entrar en la base y rehace la escuadra si había una misión elegida.
- `soldierCap`: 6 soldados por jugador en cooperativo, 12 en solitario.
- El servidor manda sus direcciones de red en `welcome` (`hosts`) y el menú construye los enlaces para el compañero (`inviteLinks`, nunca localhost). `backToMenu` lleva a los dos al menú desde la base o tras una escaramuza.

### Arquitectura de la campaña

- `engine/src/campaign.ts` es puro como el resto del motor: `executeCampaignCommand` valida y aplica órdenes de la base, `advanceTime` corre los días hasta el siguiente acontecimiento (obras, misiones, instalaciones alienígenas, pago del mes, acontecimientos) y `applyMissionResult` devuelve a la campaña bajas, heridas, experiencia (de soldados y del Bastión), cuerpos vendidos, accesorios perdidos, recompensas y el reloj alienígena.
- La campaña es pequeña, así que el host la **envía entera** tras cada cambio en vez de usar eventos. El combate sigue con event sourcing.
- Cada oferta de misión lleva su mapa (`MapSource`: bioma + semilla), su región y su plan de grupos, así que los dos clientes juegan exactamente el mismo mapa.
- La sala tiene tres fases: `lobby`, `base` y `mission`. El resultado de una misión se aplica una sola vez (`resultApplied`), aunque el host se reinicie entre medias.
- **Historia (campaña versión 5).** `lore.ts` define siete capítulos (`STORY`): tipo, bioma, región, dificultad, recompensa, informe firmado, objeto (recuperación) y VIP (rescate). `refreshStory` abre lo que toque tras cada nivel, misión, acontecimiento o escaneo:
  - El capítulo N, cuando el Bastión llega al nivel N y el N − 1 está ganado.
  - El acontecimiento de Simón, antes del capítulo 5.
  - La misión final, con el nivel 8 y los siete capítulos.

  Las ofertas de historia (`MissionOffer.story`) no caducan y no cuentan para el límite de operaciones. El servidor pasa a la misión el nombre del VIP (`offerVip` → `MissionOptions.vipName`) y el capítulo (`campaignMission.story`). Ganar la final no termina la campaña: abre el acontecimiento `finale`, y su respuesta, votada como cualquier otra, fija `ending` y `outcome`. Los acontecimientos con `story: true` nunca salen al azar.
- **Encargos y rumores (campaña versión 6).** `quests.ts` define diez encargos como datos:
  - **Pasos:** misiones con tipo, bioma, días, informe, objeto, VIP y jefe con nombre, o decisiones con dos opciones.
  - **Desenlaces:** créditos, experiencia, compases, objetos, ingresos o enemigos extra en su región, contactos, una tecnología adelantada (`CampaignState.techs`, que `hasTech` respeta), un recluta o un rasgo (`CampaignSoldier.traits`, que `squadMember` aplica).

  `campaign.ts` los ejecuta:
  - `startQuest` y `enterStep` crean la oferta (`MissionOffer.quest`) o la decisión, que se muestra como el acontecimiento `quest` mediante `eventDef`.
  - Ganar el paso lo hace avanzar; perderlo o dejarlo caducar llama a `finishQuest` con `failure`.
  - `questLog` guarda cómo acabó cada uno. De ahí salen `regionIncome`, los contactos extra y los enemigos de cada región.
  - Los disparadores están en `refreshQuests` (junto a `refreshStory`, en `refreshNarrative`); «Caras conocidas» se dispara al aplicar el resultado de una misión.

  Los rumores (`rumors`, `listening`) aparecen al escanear. La orden compartida `investigate` elige cuál escuchar, y `resolveRumor` decide en qué acaba. Los avisos emergentes de rumores y encargos van en el propio registro (`LogEntry.alert`), y `collectAlerts` los muestra.
- **Transmisiones.** `engine/src/transmissions.ts` define las escenas de radio (`SCENES`, con su condición `when` y las líneas). `nextScene(c, seen, moment)` decide cuál toca: una urgente en cualquier momento, o la primera pendiente en orden de historia solo en un momento. `client/src/strategy/transmissions.ts` tiene el panel (`RadioPanel`) y el controlador (`Transmissions`):
  - Recuerda en `localStorage` las escenas ya oídas por campaña (`CampaignState.uid`).
  - Trata como momento cada día nuevo, y espera mientras se ve el informe de misión.
  - `StrategyScreen.render()` lo llama en cada actualización.
- **Avisos por momento.** `collectAlerts` reúne los avisos nuevos con un rango y `flushAlerts` saca solo el más importante como ventana. El resto va a `news`, la bandeja «Novedades». Los avisos que el registro marca (`LogEntry.alert`) entran por `logAlert`, que añade «Escuchar» a los rumores.
- `migrateCampaign` actualiza campañas de versiones anteriores (rellena base, regiones y equipo, y recalcula los niveles de habilidad pendientes). Una campaña anterior a la historia la retoma en el capítulo de su nivel. Una anterior a los encargos empieza sin ninguno. Las misiones tácticas a medias de una versión anterior se descartan.

### Capa estratégica (cliente)

El flujo imita a XCOM 2: Bastión → geoesfera (escanear, avisos) → informe de misión → selección de escuadra en el hangar → despegue → misión → informe con la escuadra formada.

- `client/src/strategy/stage.ts`: `Stage`, un único lienzo 3D a pantalla completa con varios *decorados* (`StageSet`). Entre planos del mismo decorado la cámara se desplaza; al cambiar de decorado hay un corte a negro. Cada decorado tiene su capa de etiquetas CSS2D, aislada (`isolation: isolate`) para que quede por debajo de los paneles. Las zonas pulsables se marcan con `userData.hotspot`.
- `strategy/bastion.ts`: el Bastión en superficie, visto desde arriba en ángulo (búnker, edificios fijos, parcelas con su terreno o su edificio, pasarelas, y el hangar con la nave al sur) y sus planos: `overview`, `room`, `facilities`, `hangar`, `armory`, `takeoff`. Pone en escena la formación del hangar (`setLineup`), el soldado del pedestal (`setShowcase`), la etiqueta del compañero (`setPartner`) y el despegue (`launch`: embarque, techo que se abre y nave que sale).
- `strategy/geoscape.ts`: la Tierra con regiones, misiones e instalaciones; se gira arrastrando y vuela a una región (`flyTo`).
- `strategy/screen.ts`: `StrategyScreen`, los paneles DOM encima del decorado. Cada vista (`BaseView`: hub, geoscape, research, engineering, barracks, facilities, squad, debrief) elige un plano y sus paneles. Los avisos salen de comparar la campaña anterior con la nueva (`collectAlerts`). Prioridad de ventanas: fin de campaña > acontecimiento > avisos > cuadrícula de ascensos.
- La vista de cada jugador viaja al servidor (`baseView`), que la recuerda por asiento y la reenvía al compañero, también al reconectar.
- Despegue: cuando la sala pasa a `mission`, `main.ts` reproduce el despegue y **retiene** el tráfico de la misión (instantánea y eventos) hasta que termina. El combate se construye con la pantalla en negro, así la animación no da tirones, y abre con la presentación de la misión.
- Estilos: `strategy/strategy.css` centraliza colores y fuentes en variables `--x-*` (paneles con esquinas recortadas, botones, filas), para que la dirección de arte de M5 pueda cambiarlos en un solo sitio. El menú principal (`ui/lobby.ts`) usa los mismos estilos, con la geoesfera de fondo.

### Mapas procedurales (M4)

- `engine/src/mapgen.ts`: mapa de `GEN_SIZE` (48×48). Ciudad e imprenta parten el terreno en manzanas con una división binaria con ruido (calles de 2 a 4 que pueden torcerse o acabar) y cada manzana o parcela lleva un programa (casas, tiendas, oficinas, pisos, naves, plaza, aparcamiento; laboratorios, hangares, plataformas, almacén, reactor). Las afueras salen de ruido: bosquecillos, colinas con forma de mancha, campos vallados y un camino con la granja o las ruinas. Los edificios tienen habitaciones de 3×3 como mínimo y todos los de un mapa comparten escala (`Style`). `ground` dice qué suelo es calle o interior. Los temporizadores suben con el tamaño del mapa (`missionTurns`). En la imprenta la escuadra empieza al azar fuera del muro, en la explanada, o dentro, en un patio de llegada con el muro en los lados lejanos. `decor` dice qué es cada cobertura (coche, valla, estantería, mueble…) para que el cliente la dibuje (`decor.ts`).
- La escuadra empieza en una esquina y el objetivo está en la opuesta; la evacuación, en una de las otras dos.
- Una pasada final garantiza que todo lo transitable es alcanzable (añade escaleras, puertas o quita objetos) y solo se colocan unidades y objetivos en casillas alcanzables. Un test recorre biomas × tipos de misión × semillas comprobándolo.
- El mapa hecho a mano (`maps.ts`) sigue disponible y admite todos los tipos de misión en su punto `D`.

## Hoja de ruta

| Hito | Contenido |
|---|---|
| **M1 · Combate cooperativo** | **Hecho.** Motor de reglas, red, mapa, IA básica, HUD y modelos provisionales |
| **M2a · Clases y misiones** | **Hecho.** 4 clases con su kit (tajo, lanzagranadas, visión de escuadra, pistola, dron médico y de ayuda), lancero (aturde) y MEC (blindaje, micromisiles), blindaje y desgaste, IA con granadas y cuerpo a cuerpo, misión de recuperación con temporizador y evacuación, marcas entre jugadores, abandonar misión |
| **M2b · Profundidad táctica** | **Hecho.** Árbol de 5 niveles × 2 opciones por clase (40 habilidades, 13 activas), 8 rangos, enfriamientos y acciones gratuitas, estados (supresión, holo-objetivo, ruptura, desorientación, pánico, control mental), sectoide y zombi psiónico, alturas, escaleras, puertas y ventanas, escaramuza veterana |
| **M3a · Campaña cooperativa** | **Hecho.** Base compartida en 2D (mando, cuartel, investigación, ingeniería), recursos, propuestas entre jugadores, avance del tiempo, misiones con caducidad y dificultad creciente, escuadra mixta, experiencia, rangos y habilidades a elegir, heridas y muerte permanente, reclutamiento, reloj del Proyecto Ascensión, misión final, informe de misión, guardado de campaña |
| **M3b · Base viva** | **Hecho.** Bastión en 3D con salas que se excavan y 5 instalaciones, globo con 8 regiones, contactos e ingresos mensuales, instalaciones alienígenas y su asalto, 8 acontecimientos con decisión conjunta, autopsias que requieren cuerpos, 11 investigaciones, 5 mejoras y 8 objetos de equipo por soldado |
| **M4 · Variedad táctica** | **Hecho.** Mapas procedurales en 3 biomas, 5 tipos de misión (eliminación, recuperación, sabotaje, pirateo, rescate), patrullas, refuerzos con bengala |
| **Clases** | **Hecho (2026-10-12).** Cinco aptitudes que se reparten al ascender. El Árbol de habilidades: un árbol común de 116 talentos (centro, cuatro ejes con tres ramas y el Oficio del Taller de oficios), reclutas sin clase que se especializan en Capitán, segundo eje, kits, llave doble, reentrenamiento y la Hoja de servicio en el Cuartel |
| **Prueba 2026-10-12** | **Hecho.** Las notas de la primera partida cooperativa de Arturo y Ana: disparar gasta una acción, la clase en Capitán, mapas de combate de 48 × 48, el ascenso como borrador, puestos fijos en el hangar y más ([PRUEBA-2026-10-12.md](PRUEBA-2026-10-12.md)) |
| **Zombis (fases 1–5)** | **Hecho (2026-10-13).** El cambio a supervivencia zombi ([LORE.md](LORE.md)): textos; Saqueo, ruido y zombis ([FASE2.md](FASE2.md)); contagio y medicinas ([FASE3.md](FASE3.md)); comida, Huerto, piezas y armas ([FASE4.md](FASE4.md)); el valle de Valdesombra con carreteras y la Marea ([FASE5.md](FASE5.md)). Además, noche y altura de los edificios ([ALTURA-Y-NOCHE.md](ALTURA-Y-NOCHE.md)) |
| **Fase 6 · Plantas** | **Hecho en las reglas (2026-10-13).** Edificios de varias plantas que se suben ([FASE6.md](FASE6.md), [FASE6-PLAN.md](FASE6-PLAN.md)): posiciones con nivel, escaleras, visión y cobertura en 3D, ruido y explosiones por planta, plantas y azoteas en el generador, IA que sube y se aposta arriba, y «La torre» para escaramuza. El cliente (dibujo por plantas, corte y elección de planta) lo hacen Arte · Mapas y Combate |
| **M5 · Arte y pulido** | **Casi hecho.** Todo en Synty: personajes y animaciones, mapas en los que se entra, la base, el mapa del valle, efectos y sonido. Telemetría de rendimiento. Aplicación de escritorio en Electron (release `v0.1.0`). Falta: la interfaz nueva (3 propuestas a votar por Arturo), probar en el PC de referencia (RTX 3090, objetivo ≤4,2 ms por fotograma) y firmar la aplicación |

## Habilidades: una sola fuente de verdad

`engine/src/abilities.ts` decide si una unidad puede usar una habilidad y sobre qué objetivos o casillas (`abilityBlocker`, `abilityTargets`, `canTargetTile`). El servidor lo usa para validar órdenes, el cliente para activar o desactivar botones y explicar por qué, y la IA para elegir objetivos. Las órdenes de red son genéricas: `{ type: 'ability', unit, ability, target?, tile? }`.

Todas las habilidades, de jugadores y de alienígenas, se ejecutan por el mismo camino: `Sim.perform` emite primero `abilityUsed` (acciones, enfriamiento y carga) y después los eventos del efecto. Los perks que dan habilidades activas y los objetos de equipo solo añaden entradas a `abilitiesOf`.

## Limitaciones conocidas del prototipo

- La IA conoce la posición de los soldados una vez activada. Es aceptable para empezar, pero en XCOM es más sutil.
- El servidor envía el estado completo a ambos clientes, posiciones alienígenas incluidas. Entre amigos no importa.
- La cobertura ocupa casillas enteras: no hay muros finos entre casillas.
- Los edificios tienen plantas de verdad (fase 6, [FASE6-PLAN.md](FASE6-PLAN.md)): se entra, se sube por la escalera a cada planta y a las azoteas planas, y se pisan. Lo que falta:
  - Saltar por una ventana de arriba a la calle (decisión 4: más adelante).
  - Suelos destructibles: los forjados paran las explosiones y no se rompen (decisión 5).
  - Muros destructibles: los explosivos solo rompen cobertura, puertas y ventanas.
  - Una escalera sube como una escalera de mano: arriba se puede salir por cualquier lado libre, no solo por el que se dibuja.
  - Solo hay plantas sobre suelo a altura 0. Los bloques macizos (pisos con terraza, plataformas, colinas) siguen siendo un mapa de elevaciones.
  - De los mapas hechos a mano, solo «La torre» tiene plantas.
  - Una misión guardada antes de la fase 6 se juega sin plantas.
  - El estado completo de una misión pesa unos 107 KB, por unos 58 sin plantas. Se manda al entrar, al reconectar y al empezar una misión. Si hiciera falta, se puede comprimir (deflate en el WebSocket, o las capas en cadenas): queda para más adelante.
