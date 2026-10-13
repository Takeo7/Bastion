# Equipo y reparto

Quién hace qué en Bastión, qué archivos toca cada uno y en qué está ahora. Lo mantiene **Gameplay**, que coordina. Antes de tocar un archivo de otro rol, se le pregunta por mensaje.

## Roles

| Rol | Quién | Hace | Sus archivos |
|---|---|---|---|
| **Gameplay** (coordina) | Sesión Gameplay | Reglas, campaña, servidor, la lógica de las pantallas y el reparto del trabajo | `engine/src/` (reglas: `rules.ts`, `campaign.ts`, `abilities.ts`, `aim.ts`, `ai.ts`, `setup.ts`, `mapgen.ts`…), `server/src/room.ts`, `client/src/strategy/screen.ts`, `client/src/main.ts` |
| **Lore** | Sesión Lore | El mundo y todos los textos: nombres, capítulos, encargos, radio, glosario, fichas | `engine/src/lore.ts`, `quests.ts`, `transmissions.ts`, `docs/LORE.md` (el de zombis; el del Coro está en `docs/archive/LORE-coro.md`); los textos (no la lógica) de `content.ts`, `campaign.ts`, `progression.ts`, `classes.ts`, `setup.ts`, `mapgen.ts` |
| **Arte · Personajes** | Agente Arte-Personajes | Supervivientes, zombis y armas de Synty: escala, animaciones, personaje de cada soldado | `game/syntyFigures.ts`, `models.ts`, `figures.ts`, `unitViews.ts`, `fx.ts`; en `syntyManifest.ts`, las secciones de personajes y armas |
| **Arte · Mapas** | Agente Arte-Mapas | Mapas de combate en Synty: casas en las que se entra (tejados que se ocultan), suelo, calles, fábricas, campo, iluminación | `game/mapView.ts`, `ground.ts`, `props.ts`, `syntyProps.ts`, `fogOfWar.ts`, `explored.ts`, `post.ts`; en `syntyManifest.ts`, las secciones de objetos, edificios y terreno; la disposición de los edificios en `mapgen.ts`, de acuerdo con Gameplay |
| **Arte · Base** | Agente Arte-Base | La base y el hangar en Synty (el globo pasa a mapa del valle en la fase 5) | `strategy/bastion.ts`, `stage.ts`; en `syntyManifest.ts`, la sección de la base |
| **Gameplay · Reglas** | Agente Gameplay-Reglas | Implementa las reglas que fija Gameplay (ahora, la fase 2); Gameplay revisa y sube | `engine/src/rules.ts`, `ai.ts`, `content.ts`, `setup.ts`, `campaign.ts`, `types.ts`, `events.ts`, `abilities.ts` y sus tests, mientras dure el encargo |
| **Combate (interfaz)** | Agente de combate | El HUD y el control en combate: avisos, fichas, teclas, cámara | `client/src/game/game.ts`, `client/src/ui/hud.ts`, `client/src/styles.css` |
| **UX/UI** (lidera la interfaz) | Sesión UX/UI | La interfaz nueva y sus sonidos: laboratorio con las propuestas A «Apocalipsis», B «Moderna» y C (mezcla) y, tras el voto de Arturo, el tema; la Hoja de servicio de `screen.ts`, de acuerdo con Gameplay | `src/lab/ui/`, `interfaz.html`, `ui/theme.ts` y `ui/holo.css` (se borran tras el voto), `game/combatTips.ts`, `docs/UX-REVISION*`; tras el voto, los estilos del tema (`styles.css`, `strategy.css`, `transmissions.css`) de acuerdo con Combate y Gameplay, y `audio/ui.ts` de acuerdo con Red |
| **Clases** | Sesión Clases | Reglas, números y estructura de las clases: aptitudes, talentos, la Rosa; el equilibrio, de acuerdo con la simulación de Gameplay. Avisa a Gameplay antes de tocar `classes.ts` | `engine/src/classes.ts` (los textos son de Lore), `engine/test/classes.test.ts`, la sección de clases de `docs/DESIGN.md` |
| **Red** | Sesión Networking (o un agente Red si no está) | Conexión, https, informes del navegador del compañero, el servidor de juego de los jugadores, la telemetría de rendimiento y la aplicación de escritorio (Electron) | `client/src/net.ts`, `report.ts`, `ui/lobby.ts`, `audio/`, `server/src/index.ts`, `tls.ts` |

La sesión **Arte** sigue abierta sin encargo: escribió el motor vóxel y se le puede consultar.

## Normas

- Nada de Prettier ni formateadores: el código está formateado a mano.
- Ediciones exactas y pequeñas en archivos compartidos, nunca reescribir un archivo entero de otro.
- Nadie hace commit sin avisar a Gameplay, que agrupa y sube.
- Conversión de Synty: `tools/synty/convert.ts` siempre con ids explícitos, nunca una pasada completa (borra lo que no está en el manifiesto y podría llevarse lo nuevo de otro agente).
- Cada uno prueba con su propio servidor:

  | Quién | Servidor | Cliente |
  |---|---|---|
  | Gameplay | `bastion-test` (3310) | `bastion-gp-client` (5193) |
  | Arte · Mapas | `bastion-ux-server` (3330) | `bastion-ux-client` (5197) |
  | Arte · Personajes | `bastion-clases-server` (3340) | `bastion-clases-client` (5199) |
  | Arte · Base | `bastion-base-server` (3350) | `bastion-base-client` (5201) |
  | UX/UI | `bastion-uiux-server` (3360) | `bastion-uilab` (5203; también sirve `interfaz.html`) |
  | Combate | `bastion-test` (3310), con permiso de Gameplay | `bastion-gp-client` (5193) |
  | Red | 3390 (https en 3391), arrancado a mano | — |

  Nunca se usan ni se paran el 3000/3001 (la partida de los jugadores) ni el 5173.
- El panel admite 5 servidores de prueba a la vez: como mucho, dos o tres agentes probando en el navegador al mismo tiempo.
- Los packs de Synty originales viven en `assets-src/synty/`, que no se sube: la licencia no deja redistribuirlos. Al repositorio solo van los modelos convertidos que use el juego.

## En marcha (2026-10-13)

Decisiones de Arturo del 13/10 (las trajo General):
- el aspecto objetivo es el de las capturas promocionales de Synty (`assets-src/referencias/aspecto-synty/`, no se suben);
- fuera los dos temas de interfaz, Expediente y Holotáctico (`ui/theme.ts`, `ui/holo.css`, el selector «Interfaz» del menú y de Ajustes, los ganchos `data-ui`), y una interfaz nueva que pegue con Synty y los zombis;
- fuera todo lo vóxel;
- seguimos en Three.js, en la web, y con una aplicación de escritorio en Electron (aprobada; de Red, después de la telemetría);
- equipo de referencia: un PC con Windows y una RTX 3090, en Chrome o Electron, a 1440p o 4K; objetivo «alto» a **≥240 fps, es decir, ≤4,2 ms por fotograma entre CPU y GPU** (a ese ritmo manda la CPU: el JS de cada fotograma, el recorrido de la escena, las hordas y las llamadas de dibujo). El Mac es el mínimo: «medio» a ≥30 fps. El aspecto no se recorta por el Mac: para eso están los ajustes. Se mide el tiempo de fotograma, no los fps (requestAnimationFrame va al ritmo del monitor).
- El servidor también tiene que funcionar en Windows (sin openssl, así que sin https; el Firewall pedirá permiso a Node la primera vez).

Orden:
1. Telemetría de rendimiento.
2. Manzana de referencia y coste de las hordas.
3. Quitar lo vóxel.
4. Laboratorio de la interfaz, para que Arturo elija.
5. La luz nueva en la base y el globo en Synty.

La fase 3 de zombis empieza cuando se suba lo que hay y pase la ronda 2.

| Quién | Qué |
|---|---|
| Gameplay | Coordinar, revisar y subir. Al final: borrar el motor vóxel (`voxel.ts`, `vox.ts`, `voxFigures.ts`, `voxBodies.ts`, `assets/voxel/`), los laboratorios vóxel y la opción «Modelos» de Ajustes. |
| Red (agente, mientras la sesión Networking no esté) | Hecho: telemetría (`client/src/perf.ts`, líneas `[rend …]` en la consola del servidor y en `red.log`), sonidos del Oficio, chillido del Chillón y muertes de zombi, servidor en Windows sin openssl (solo http, mensajes claros), y la aplicación de escritorio en Electron (`packages/desktop`; `npm run desktop`, `npm run desktop:dist`): pantalla «Ser anfitrión» / «Unirse», servidor dentro, partida y `red.log` en `%APPDATA%\Bastion\` o `~/Library/Application Support/Bastion/`, `BASTION_SIN_TOPE=1` para medir sin tope. Funciona en Mac; el instalador de Windows se construye en el PC de Arturo. |
| Arte · Mapas | Hecho: suelo negro, lo vóxel fuera, la manzana de referencia (`referencia.html`; estático fundido en `batch.ts`, sombras en caché, oclusión de contacto horneada, calle vestida), reconstrucción por trozos (de 160–270 ms a 4–13 ms), ciudad variada en `mapgen.ts` (hileras de tiendas, gasolinera, motel, casas con jardín, obras, control militar), bordes que siguen la calle con vehículos abandonados, packs Office, Construction y Military. Pendiente: los pisos de arriba solo se dibujan (las reglas tienen una planta por casilla), la densidad de la ciudad apenas cambió, unir los atlas del mapa en una textura (de 83 a ~74 llamadas). La noche, solo en el laboratorio. |
| Arte · Personajes | Hecho: pelo unido al cuerpo, sombras solo de los 24 enemigos más cercanos, capa propia para las unidades (`UNIT_LAYER`), `models.ts` sin vóxel, laboratorios viejos borrados, retratos de radio Synty (`portraits.ts`), POLYGON Particle FX en `fx.ts` (laboratorio `efectos.html`), los efectos del Oficio (`songFx.ts`) y el grito y el levantarse sin rayos. También: detalle de pose por distancia y estado (en reposo o lejos a 30 Hz, fuera de pantalla a 8 Hz, en la niebla o muertos nada), sin subir huesos si no cambian, material compartido. El horneado de hordas (InstancedMesh con animación en textura) queda a la espera de medir en el PC con la 3090: solo si las unidades pasan de ~1 ms por fotograma. Ahora: dos renders para iconos de UX/UI (el molotov y La Mula). |
| Arte · Base | Hecho: la base en Synty y `powerPreference: 'high-performance'` en `stage.ts`. Ronda 5: la luz de la manzana validada (≤4,2 ms en la 3090), el globo (`geoscape.ts`, también fondo del menú) en Synty, el pedestal del Cuartel, Construction para las obras y la Cámara de Ecos (malla de cobre, verde del Coro, y su fallback) convertida en Taller de oficios. |
| UX/UI | Hecho: el Árbol de habilidades (`skillTree.ts`, `skillTree.css`; `promotionPanel` vuelve a Gameplay), los sonidos de interfaz Kenney (CC0) por propuesta en `audio/ui.ts`, iconos MilitaryHUD. Las 3 propuestas se pueden votar en el servidor de los jugadores. Lidera la interfaz nueva; parte de lo que recopiló el agente de interfaz (`.dev-snapshots/uilab/HANDOVER.md`, ya entregado). El tema se comparte entre los dos jugadores (mensaje `uiTheme`; lo último que elige uno vale para los dos). Un laboratorio con las propuestas A «Apocalipsis», B «Moderna» y C (mezcla), jugables en el juego real desde el selector de interfaz (Arturo decide jugando; el tema por defecto no cambia hasta el voto); tipografías libres (OFL) Oswald, Barlow, Barlow Condensed y Black Ops One, que solo carga el tema que las use (packs INTERFACE Apocalypse HUD en `Source_Sprites` y Modern Menus), sonidos de interfaz incluidos, para que Arturo elija; no se aplica nada sin su voto. También una lista de lo que falte en los packs (los nombres, de Lore y Clases) para que Arturo lo descargue; candidato: INTERFACE Military Combat HUD. |
| Gameplay · Reglas | En espera: fase 3 (infección y medicinas). |
| Lore | Nombres pendientes: el doble sentido de «Oficio» (aptitud y rama; propuesta «Maña» para la aptitud, a falta de Arturo), nombres repetidos, si el Grillo (el dron) se queda, «4 a robots» en `classes.ts`; con ellos, los textos de `abilities.ts` y `game.ts` que los usan. Y los textos de ciencia ficción que quedan (p. ej. «Agente» en el Rescate de la plaza). |
| Clases | Dueña de `classes.ts`; equilibrio de aptitudes y talentos con la simulación de Gameplay. |

«La Rosa» pasa a llamarse **«Árbol de habilidades»** para el jugador (los ids internos no cambian; las reglas tampoco). Lore cambia los textos, Clases `classes.ts` y `DESIGN.md`, y UX/UI rehace la Hoja de servicio (`promotionPanel` de `screen.ts`, cedido a UX/UI mientras dure) como un árbol de caminos y nodos conectados.
| Combate | En espera. |

## Plan de zombis (fases)

1. Reskin: textos y nombres (Lore). Gameplay aplica los de las pantallas.
2. Saqueo, ruido e IA de zombis (Gameplay; Arte, los muebles registrables y los zombis).
3. Infección y medicinas (Gameplay; Lore, los textos).
4. Comida y armas que se encuentran o se fabrican (Gameplay).
5. El mapa del valle de Valdesombra en lugar del globo (Arte; Gameplay, la lógica).
