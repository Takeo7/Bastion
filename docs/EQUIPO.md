# Equipo y reparto

Quién hace qué en Bastión, qué archivos toca cada uno y en qué está ahora. Lo mantiene **Gameplay**, que coordina. Antes de tocar un archivo de otro rol, se le pregunta por mensaje.

## Roles

| Rol | Quién | Hace | Sus archivos |
|---|---|---|---|
| **Gameplay** (coordina) | Sesión Gameplay | Reglas, campaña, servidor, la lógica de las pantallas y el reparto del trabajo | `engine/src/` (reglas: `rules.ts`, `campaign.ts`, `abilities.ts`, `aim.ts`, `ai.ts`, `setup.ts`, `mapgen.ts`…), `server/src/room.ts`, `client/src/strategy/screen.ts`, `client/src/main.ts` |
| **Lore** | Sesión Lore | El mundo y todos los textos: nombres, capítulos, encargos, radio, glosario, fichas | `engine/src/lore.ts`, `quests.ts`, `transmissions.ts`, `docs/LORE.md` (el de zombis; el del Coro está en `docs/archive/LORE-coro.md`); los textos (no la lógica) de `content.ts`, `campaign.ts`, `progression.ts`, `classes.ts`, `setup.ts`, `mapgen.ts` |
| **Arte** | Sesión Arte | Todo el arte de Synty: supervivientes, zombis y armas (escala, animaciones, personaje de cada soldado); mapas de combate (casas en las que se entra, suelo, calles, iluminación); la base y el mapa del valle | `game/syntyFigures.ts`, `models.ts`, `figures.ts`, `unitViews.ts`, `fx.ts`, `mapView.ts`, `ground.ts`, `props.ts`, `syntyProps.ts`, `fogOfWar.ts`, `explored.ts`, `post.ts`, `renderer.ts`, `strategy/bastion.ts`, `stage.ts`, `docs/ARTE.md`; `syntyManifest.ts` entero; la disposición de los edificios en `mapgen.ts`, de acuerdo con Gameplay |
| **Gameplay · Reglas** | Agente Gameplay-Reglas | Implementa las reglas que fija Gameplay (ahora, la fase 2); Gameplay revisa y sube | `engine/src/rules.ts`, `ai.ts`, `content.ts`, `setup.ts`, `campaign.ts`, `types.ts`, `events.ts`, `abilities.ts` y sus tests, mientras dure el encargo |
| **Combate (interfaz)** | Sesión UI, mientras no haya sesión de Combate | El HUD y el control en combate: avisos, fichas, teclas, cámara | `client/src/game/game.ts`, `game/overlays.ts`, `client/src/ui/hud.ts`, `client/src/styles.css` |
| **UX/UI** (lidera la interfaz) | Sesión UI | La interfaz nueva y sus sonidos: laboratorio con las propuestas A «Apocalipsis», B «Moderna» y C (mezcla) y, tras el voto de Arturo, el tema; la Hoja de servicio de `screen.ts`, de acuerdo con Gameplay | `src/lab/ui/`, `interfaz.html`, `ui/theme.ts` y `ui/holo.css` (se borran tras el voto), `game/combatTips.ts`, `docs/UX-REVISION*`; tras el voto, los estilos del tema (`styles.css`, `strategy.css`, `transmissions.css`) de acuerdo con Combate y Gameplay, y `audio/ui.ts` de acuerdo con Red |
| **Clases** | Sesión Clases | Reglas, números y estructura de las clases: aptitudes, talentos, la Rosa; el equilibrio, de acuerdo con la simulación de Gameplay. Avisa a Gameplay antes de tocar `classes.ts` | `engine/src/classes.ts` (los textos son de Lore), `engine/test/classes.test.ts`, la sección de clases de `docs/DESIGN.md` |
| **Red** | Sesión Networking | Conexión, https, informes del navegador del compañero, el servidor de juego de los jugadores, la telemetría de rendimiento y la aplicación de escritorio (Electron) | `client/src/net.ts`, `report.ts`, `ui/lobby.ts`, `audio/`, `server/src/index.ts`, `tls.ts`, `lan.ts`, `packages/desktop/` |

Sesiones abiertas (2026-10-14), todas con Remote Control para seguirlas desde claude.ai/code: Gameplay, Arte, Lore, UI y Networking. Gameplay · Reglas y Clases no tienen sesión: lo suyo lo lleva Gameplay.

## Normas

- Nada de Prettier ni formateadores: el código está formateado a mano.
- Ediciones exactas y pequeñas en archivos compartidos, nunca reescribir un archivo entero de otro.
- Nadie hace commit sin avisar a Gameplay, que agrupa y sube.
- Conversión de Synty: `tools/synty/convert.ts` siempre con ids explícitos, nunca una pasada completa (borra lo que no está en el manifiesto y podría llevarse lo nuevo de otro agente).
- Cada uno prueba con su propio servidor:

  | Quién | Servidor | Cliente |
  |---|---|---|
  | Gameplay | `bastion-test` (3310) | `bastion-gp-client` (5193) |
  | Arte | `bastion-ux-server` (3330) | `bastion-ux-client` (5197) |
  | UX/UI | `bastion-uiux-server` (3360) | `bastion-uilab` (5203; también sirve `interfaz.html`) |
  | Red | 3390 (https en 3391), arrancado a mano; 3392, el receptor de la captura del tráiler | 5205 (`trailer.html`, config local en `.claude/`) |

  Nunca se usan ni se paran el 3000/3001 (la partida de los jugadores) ni el 5173.
- El panel admite 5 servidores de prueba a la vez: como mucho, dos o tres agentes probando en el navegador al mismo tiempo.
- Los packs de Synty originales viven en `assets-src/synty/`, que no se sube: la licencia no deja redistribuirlos. Al repositorio solo van los modelos convertidos que use el juego.
- El repositorio `Takeo7/Bastion` es **siempre privado**: lleva los modelos de Synty convertidos y la licencia no deja publicarlos. Nadie cambia su visibilidad. Si algún día hace falta una descarga pública, será un repositorio o sitio aparte solo con los binarios, y siempre preguntando antes a Arturo. Las releases (`v0.1.0`) solo las ve quien tiene acceso al repositorio; para pasar el juego a alguien, Arturo le manda el zip.

## En marcha (2026-10-14, histórico)

Esta sección se quedó en el 14/10. Lo de ahora está en [TRASPASO.md](TRASPASO.md) §3 y §5; la última revisión del código, en [REVISION-2026-10-19.md](REVISION-2026-10-19.md).

Subida la 0.2.2 (bdf4953 y 0d4db5b) y, después, la 0.2.3: la fase 7 ([FASE7.md](FASE7.md)) y los zombis primero ([ZOMBIS-PRIMERO.md](ZOMBIS-PRIMERO.md)), con el cliente de Arte y UI y los textos de Lore; y la 0.2.4: pasos sin patinar por plantilla, muertes de verdad, posturas, andar de zombi y el ritmo del turno enemigo ([UX-RITMO.md](UX-RITMO.md)). Las carpetas de `assets-src/synty/` tienen ya el nombre de su pack (tabla en `packages/client/tools/synty/README.md`). Lo que hizo cada sesión antes de eso:

| Quién | Hecho el 14/10 | Ahora |
|---|---|---|
| Gameplay | Visibilidad el doble de rápida en mapas con plantas (`los.ts`); «Tejado» en lugar de «Nido» en la puntería; el contador de raciones en rojo si no llegan para mañana; «Caras conocidas» sin familiares con el nombre del soldado; los documentos al día | Las mecánicas pendientes ([FASE7.md](FASE7.md)) |
| Arte | ARTE.md reescrito; el valle con relieve, campos, pueblos y carreteras; luz y suciedad del combate; plazas con árboles y escombros (`mapgen.ts`, con Gameplay); fuera las figuras de XCOM; etiquetas del seleccionado al frente; `renderer.ts` pasa a Arte | Esperando encargo |
| Lore | Los textos del globo y de XCOM que quedaban; el consejo de radio de las plantas; nombres que chocaban (Chioma Okafor, Samira Benali, Pierre Lambert, Mariana Duarte, Tariq Rahimi, Anja Kowalska; Clara y Yusuf) | Esperando encargo |
| UI | Revisión de UX 3: velo de carga, ventanas estrechas, pulido del combate, estilos del panel de direcciones | Sin subir: la franja de combate en dos filas hasta 1366 px (`styles.css`), para después de la prueba |
| Networking | Deflate en el WebSocket; el panel «Comandantes» sin interfaces virtuales; la app no se queda con un puerto ocupado en 127.0.0.1; la guía [PRUEBA-PC-3090.md](PRUEBA-PC-3090.md) | Leer el `red.log` de la prueba en el PC |

## Lo que se hizo hasta el 13/10

Subido b56a5b4 (noche, altura, fases 3 y 4, el valle, fuera lo vóxel). Fase 5 hecha y subida (8bc98a8). Las 5 fases del plan de zombis están terminadas.

**Prueba del 13/10** ([PRUEBA-2026-10-13.md](PRUEBA-2026-10-13.md)), en marcha:
- Gameplay · Reglas: sin refuerzos humanos, zombis solo por los bordes, abrir puertas gratis, ver por las ventanas, la campaña empieza con una misión, el sexo del soldado y el aviso de ascenso.
- Combate: el botón Registrar, los textos que no caben, la cuadrícula del suelo como en XCOM, el objetivo en la presentación de la misión.
- Arte · Personajes: modelo según el sexo, cómo cogen las armas, animaciones, auriculares, transparencia en el Cuartel.
- Arte · Interfaz (agente, mientras UX/UI no esté): Mixta, elegida por Arturo, como única interfaz; más profundidad y vida; el árbol de habilidades con referencias; la marca «A»; los estilos de la fase 4.
- Lore: tono adulto, nombres internacionales, la premisa, la apertura y los nombres nuevos ([LORE-PROPUESTA-2026-10-13.md](LORE-PROPUESTA-2026-10-13.md), pendiente de Arturo).
- Arte · Mapas: hecho. Dos estilos de ciudad (cuadrícula y pueblo viejo, mitad y mitad), aceras más estrechas, fachadas escalonadas, plazuelas; el tejado se desvanece cuando se ve el interior por una ventana o puerta; pavimento sin aspecto de cuadrícula.
- Gameplay · Reglas: fase 6 hecha, edificios de varias plantas ([FASE6.md](FASE6.md), [FASE6-PLAN.md](FASE6-PLAN.md)): plantas reales en el generador, escaleras, visión, niebla y cobertura por planta, ruido y explosiones, IA que sube; 345 tests. Pendiente para más adelante: saltar por ventanas altas, suelos destructibles, comprimir el estado (deflate en el WebSocket, de Red).
- Arte · Mapas y Combate: plantas dibujadas, corte por plantas, niebla por planta, planta activa (RePág/AvPág, Mayús+rueda), marca «▲ P1».
- Después: Arte · Base (más detalle en el valle).

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
| Gameplay | Hecho: la base de la fase 5 (días de viaje por operación, razón al contactar, zonas bajo la Marea), la base de la fase 4 (raciones y piezas arriba, el Huerto, fabricar en la Armería, las armas encontradas), el motor vóxel borrado (archivos, modelos `.vox` y la opción «Modelos» de Ajustes); la noche en el servidor y en el menú de escaramuza; la base de la fase 3 en `screen.ts` (fiebre con cuenta atrás, «Tratar» con dos llaves, «Curar», el informe y los Caídos). Coordinar, revisar y subir. Al final: borrar el motor vóxel (`voxel.ts`, `vox.ts`, `voxFigures.ts`, `voxBodies.ts`, `assets/voxel/`), los laboratorios vóxel y la opción «Modelos» de Ajustes. |
| Red (agente, mientras la sesión Networking no esté) | Hecho: telemetría (`client/src/perf.ts`, líneas `[rend …]` en la consola del servidor y en `red.log`), sonidos del Oficio, chillido del Chillón y muertes de zombi, servidor en Windows sin openssl (solo http, mensajes claros), y la aplicación de escritorio en Electron (`packages/desktop`; `npm run desktop`, `npm run desktop:dist`): pantalla «Ser anfitrión» / «Unirse», servidor dentro, partida y `red.log` en `%APPDATA%\Bastion\` o `~/Library/Application Support/Bastion/`, `BASTION_SIN_TOPE=1` para medir sin tope. Funciona en Mac; el instalador de Windows se construye en el PC de Arturo. También: icono (La Mula), partidas en tu red por UDP 41390 (`server/src/lan.ts`) y regla del cortafuegos en el instalador «para todos los usuarios». |
| Arte · Mapas | Hecho: suelo negro, lo vóxel fuera, la manzana de referencia (`referencia.html`; estático fundido en `batch.ts`, sombras en caché, oclusión de contacto horneada, calle vestida), reconstrucción por trozos (de 160–270 ms a 4–13 ms), ciudad variada en `mapgen.ts` (hileras de tiendas, gasolinera, motel, casas con jardín, obras, control militar), bordes que siguen la calle con vehículos abandonados, packs Office, Construction y Military.. También: las plantas y la noche desde el motor, los ocho atlas del mapa en una textura (de 83 a 63 llamadas) y una ciudad más densa (del 31 % al 42 % construido, callejones, hileras espalda con espalda). |
| Arte · Personajes | Hecho: pelo unido al cuerpo, sombras solo de los 24 enemigos más cercanos, capa propia para las unidades (`UNIT_LAYER`), `models.ts` sin vóxel, laboratorios viejos borrados, retratos de radio Synty (`portraits.ts`), POLYGON Particle FX en `fx.ts` (laboratorio `efectos.html`), los efectos del Oficio (`songFx.ts`) y el grito y el levantarse sin rayos. También: detalle de pose por distancia y estado (en reposo o lejos a 30 Hz, fuera de pantalla a 8 Hz, en la niebla o muertos nada), sin subir huesos si no cambian, material compartido. El horneado de hordas (InstancedMesh con animación en textura) queda a la espera de medir en el PC con la 3090: solo si las unidades pasan de ~1 ms por fotograma. Ahora: dos renders para iconos de UX/UI (el molotov y La Mula). |
| Arte · Base | Hecho: la base en Synty con la luz de la manzana (de 524 a 132 llamadas), palés en el Cuartel, Taller de oficios, obras con Construction, y el mapa del valle de Kessra en Synty en lugar del globo (8 zonas con sus pueblos, el embalse, las minas, la Vega y la Marea que sube desde el sur; también de fondo del menú; 49 llamadas). y el Huerto por niveles (bancales con plástico, invernadero con estufa, riego por goteo y espantapájaros).. También: el mapa del valle lee la geografía del motor, La Mula sigue las carreteras y la Marea cubre exactamente las zonas caídas (zombis, luces apagadas, frente animado). |
| UX/UI | Hecho: el Árbol de habilidades (`skillTree.ts`, `skillTree.css`; `promotionPanel` vuelve a Gameplay), los sonidos de interfaz Kenney (CC0) por propuesta en `audio/ui.ts`, iconos MilitaryHUD. Las 3 propuestas se pueden votar en el servidor de los jugadores. Lidera la interfaz nueva; parte de lo que recopiló el agente de interfaz (`.dev-snapshots/uilab/HANDOVER.md`, ya entregado). El tema se comparte entre los dos jugadores (mensaje `uiTheme`; lo último que elige uno vale para los dos). Un laboratorio con las propuestas A «Apocalipsis», B «Moderna» y C (mezcla), jugables en el juego real desde el selector de interfaz (Arturo decide jugando; el tema por defecto no cambia hasta el voto); tipografías libres (OFL) Oswald, Barlow, Barlow Condensed y Black Ops One, que solo carga el tema que las use (packs INTERFACE Apocalypse HUD en `INTERFACE_ApocalypseHUD_SourceFiles` y Modern Menus), sonidos de interfaz incluidos, para que Arturo elija; no se aplica nada sin su voto. También una lista de lo que falte en los packs (los nombres, de Lore y Clases) para que Arturo lo descargue; candidato: INTERFACE Military Combat HUD. Ahora (2026-10-14): revisión de UX 3 sobre la 0.2.1 (menú, misión, base y cooperativo); lo que era pendiente de estilos e iconos está hecho. |
| Gameplay · Reglas | Hecho: la altura y la noche, la fase 3 (contagio y medicinas), el Oficio en los veteranos de escaramuza y la fase 4 (comida, Huerto, piezas, fabricar en el Taller, armas encontradas; 282 tests).. También la fase 5, la lógica del valle (carreteras, contactar por carretera, días de viaje, la Marea por zonas con umbrales 4/6/9/10/11; [FASE5.md](FASE5.md); 291 tests). |
| Lore | Resuelto: «Maña» para la aptitud, el Grillo, «4 a Acorazados y Sirenas» en `classes.ts` y «Agente Claire Dubois». Ahora (2026-10-14): los textos viejos de globo, regiones y escanear que quedan, como «zona», «el valle», «esperar noticias» y «Nido», en `campaign.ts`, `screen.ts`, `game.ts`, `bastion.ts` y `settingsPanel.ts` (solo cadenas). |
| Clases | Dueña de `classes.ts`; equilibrio de aptitudes y talentos con la simulación de Gameplay. |

«La Rosa» pasa a llamarse **«Árbol de habilidades»** para el jugador (los ids internos no cambian; las reglas tampoco). Lore cambia los textos, Clases `classes.ts` y `DESIGN.md`, y UX/UI rehace la Hoja de servicio (`promotionPanel` de `screen.ts`, cedido a UX/UI mientras dure) como un árbol de caminos y nodos conectados.
| Combate | Hecho: la noche (alcances, «DE NOCHE», casillas a la luz, luces), el mordisco («MORDIDO», el que se levanta) y la fase 4 (raciones, piezas y armas encontradas al registrar, en la mochila y en el HUD). |

## Plan de zombis (fases)

1. Reskin: textos y nombres (Lore). Gameplay aplica los de las pantallas.
2. Saqueo, ruido e IA de zombis (Gameplay; Arte, los muebles registrables y los zombis).
3. Infección y medicinas (Gameplay; Lore, los textos).
4. Comida y armas que se encuentran o se fabrican (Gameplay).
5. El mapa del valle de Kessra en lugar del globo (hecho).
