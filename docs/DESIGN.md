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
| Puntos de acción | 2 por turno; disparo, vigilancia, agazaparse y granada terminan el turno | `bConsumeAllPoints` de esas habilidades |
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

- Base y recursos compartidos; cada jugador tiene sus soldados, los asciende, elige sus habilidades y los equipa.
- Los dos usáis la base a la vez, cada uno en su pantalla. Se ve en cuál está el compañero.
- **Propuestas:** excavar, construir, contactar regiones, elegir misión y responder a acontecimientos las propone uno y las acepta o rechaza el otro. Solo hay una propuesta abierta a la vez. Si solo hay un jugador conectado, se aplican directamente. Lo personal (reclutar, ascender, comprar y equipar accesorios, personalizar) no se vota.
- **Listo:** el tiempo avanza y la misión despega solo cuando todos los jugadores conectados están listos. Un acontecimiento sin responder congela el tiempo y no deja despegar.
- **Personalización:** cada soldado de campaña tiene `nickname` y `appearance` (`engine/src/appearance.ts`: colores, cabeza, complexión). Es una orden personal (`customize`), sin propuesta. Viaja con el soldado a la misión (`Unit.appearance`, opcional: alienígenas y escaramuza no la tienen) y `buildFigure` la interpreta. `defaultAppearance` reproduce el aspecto de antes de que existiera (mezcla en espacio lineal, como THREE.Color), y `migrateCampaign` la rellena (campaña versión 3).
- **Escuadra:** cada jugador aporta hasta 3 soldados propios (4 con el centro de entrenamiento). Los soldados de la campaña llegan al combate con su rango, habilidades, equipo y mejoras ya calculados (`UnitMods`, `charges`, `gear`), así que las reglas de combate no saben nada de la campaña.

### Progresión (campaña versión 4)

Sustituye a la investigación y a los tres recursos (suministros, inteligencia, aleaciones).

- **Créditos**, una sola moneda (`CampaignState.credits`). Entran por recompensas de misión, el pago mensual del Consejo (60 + ingresos de las regiones contactadas) y la venta de cuerpos alienígenas tras una victoria (`BODY_VALUE`).
- **Nivel del Bastión** (`engine/src/progression.ts`): `c.xp` sube con cada misión (`missionXp`: +2, +3 y la dificultad si se gana, +1 por baja; ×1,25 con la sala de simulación) y con algunos acontecimientos (`gainXp`). `LEVEL_XP` da 8 niveles. Tecnologías (`TECHS`), accesorios (`ItemDef.level`) e instalaciones (`FacilityDef.level`) se desbloquean por nivel; el nivel 8 revela la misión final.
- **Armas y armadura por soldado**, sin compra: `weaponTier(c, s)` y `armorTier(c, s)` son el mínimo entre lo que el Bastión ha desbloqueado y lo que permite el rango (`TIER_RANK`: nivel 2 desde Cabo, nivel 3 desde Teniente). `squadMember` aplica el daño (`WEAPON_TIER_DAMAGE`) y la salud y blindaje (`ARMOR_TIERS`), y los pasa a la unidad (`Unit.weaponTier`, `Unit.armorTier`) para que `buildFigure` dibuje el arma y la armadura de su nivel.
- `migrateCampaign` convierte campañas antiguas: créditos = suministros + 2 × inteligencia + 3 × aleaciones; experiencia = la suma de la de sus soldados, y como mínimo la del nivel de lo que ya se había investigado o fabricado; los laboratorios pasan a salas de simulación.

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
- `strategy/bastion.ts`: el Bastión en corte (hangar con la nave, cuatro salas de mando fijas, cuadrícula de 3×3) y sus planos: `overview`, `room`, `facilities`, `hangar`, `armory`, `takeoff`. Pone en escena la formación del hangar (`setLineup`), el soldado del pedestal (`setShowcase`), la etiqueta del compañero (`setPartner`) y el despegue (`launch`: embarque, techo que se abre y nave que sale).
- `strategy/geoscape.ts`: la Tierra con regiones, misiones e instalaciones; se gira arrastrando y vuela a una región (`flyTo`).
- `strategy/screen.ts`: `StrategyScreen`, los paneles DOM encima del decorado. Cada vista (`BaseView`: hub, geoscape, research, engineering, barracks, facilities, squad, debrief) elige un plano y sus paneles. Los avisos salen de comparar la campaña anterior con la nueva (`collectAlerts`). Prioridad de ventanas: fin de campaña > acontecimiento > avisos > cuadrícula de ascensos.
- La vista de cada jugador viaja al servidor (`baseView`), que la recuerda por asiento y la reenvía al compañero, también al reconectar.
- Despegue: cuando la sala pasa a `mission`, `main.ts` reproduce el despegue y **retiene** el tráfico de la misión (instantánea y eventos) hasta que termina. El combate se construye con la pantalla en negro, así la animación no da tirones, y abre con la presentación de la misión.
- Estilos: `strategy/strategy.css` centraliza colores y fuentes en variables `--x-*` (paneles con esquinas recortadas, botones, filas), para que la dirección de arte de M5 pueda cambiarlos en un solo sitio. El menú principal (`ui/lobby.ts`) usa los mismos estilos, con la geoesfera de fondo.

### Mapas procedurales (M4)

- `engine/src/mapgen.ts`: mapa de 36×36 = 3×3 parcelas de 10×10 separadas por calles de 3. Cada parcela la rellena un generador del bioma (`city`, `wilds`, `facility`).
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
| M5 · Arte y pulido | **En curso:** laboratorio de estilos para elegir la dirección de arte ([ARTE.md](ARTE.md)). Después: modelos y animaciones reales, sonido, menús; ejecutable opcional (Electron o Steam) |

## Habilidades: una sola fuente de verdad

`engine/src/abilities.ts` decide si una unidad puede usar una habilidad y sobre qué objetivos o casillas (`abilityBlocker`, `abilityTargets`, `canTargetTile`). El servidor lo usa para validar órdenes, el cliente para activar o desactivar botones y explicar por qué, y la IA para elegir objetivos. Las órdenes de red son genéricas: `{ type: 'ability', unit, ability, target?, tile? }`.

Todas las habilidades, de jugadores y de alienígenas, se ejecutan por el mismo camino: `Sim.perform` emite primero `abilityUsed` (acciones, enfriamiento y carga) y después los eventos del efecto. Los perks que dan habilidades activas y los objetos de equipo solo añaden entradas a `abilitiesOf`.

## Limitaciones conocidas del prototipo

- La IA conoce la posición de los soldados una vez activada. Es aceptable para empezar, pero en XCOM es más sutil.
- El servidor envía el estado completo a ambos clientes, posiciones alienígenas incluidas. Entre amigos no importa.
- La cobertura ocupa casillas enteras: no hay muros finos entre casillas.
- Las alturas son un mapa de elevaciones: no hay interiores bajo una azotea (un edificio con tejado transitable es macizo). Los edificios con interior no tienen tejado.
- Los muros de los edificios son indestructibles; los explosivos solo rompen cobertura, puertas y ventanas.
- El globo y el Bastión son vistas provisionales hechas con primitivas (continentes a grandes rasgos, salas con cajas).
