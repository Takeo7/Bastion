# El paso a Unity

Plan del 23/10, después de la 0.10.1. La 0.10.1 (commit 9ac9753) es la última versión con el cliente en Three.js.

## 1. Decisiones de Arturo (23/10)

| Qué | Decisión |
|---|---|
| Reglas y servidor | **Todo a C#.** El motor (27.000 líneas de TypeScript) y el servidor de Node se reescriben en C#. |
| Primer hito | **Una misión de combate.** Un mapa generado, la escuadra y los zombis con sus animaciones, mover, disparar y el turno enemigo, entre dos jugadores. |
| La versión de Three.js | **Congelada.** Arturo y Ana jugaron con la 0.10.1 hasta la 0.11.0 (29/10), la primera de Unity; solo se arregla lo que impida jugar. |
| Instalación | Unity Hub con Homebrew; Arturo inicia sesión (licencia Personal). Después, Unity 6 LTS desde la línea de órdenes, con soporte de Mac y Windows. |
| Reglas después de la 0.10.1 (28/10) | **Cambian ya, antes de la primera versión de Unity.** Tras la prueba de Ana: zombis al 80 % de un soldado (6,4 casillas por acción), sin haber visto a nadie vagan con las dos acciones, y el Chillón no vuelve hacia los vivos después de retroceder; la base con un solo camino (al sur), un claro de radio 6 a 8 y los muertos del asedio entrando por las veredas de entre los pinos (las partidas guardadas, versión 26, llenan de pinos los caminos viejos); bolardos y papeleras en las aceras en vez de macetas, con las mismas reglas. Lo detalla [PRUEBA-2026-10-28.md](PRUEBA-2026-10-28.md). El motor de C# deja de ser idéntico a la 0.10.1 en eso; las grabaciones de TypeScript se hacen con `engine-cs/golden/since-0101.patch` (engine-cs/README.md). |
| Las pruebas del motor (29/10) | **TypeScript, solo hasta la tanda del 29/10.** Las reglas de las notas del 28/10 y del 29/10 se escribieron también en TypeScript (sin publicarlo, `engine-cs/golden/since-0101.patch`) para grabar las partidas de referencia. Después, las reglas solo en C#, y las partidas de referencia se graban con el propio C# (engine-cs/README.md). |
| El tiempo real (29/10) | **No se trae a Unity.** El modo en tiempo real de la 0.8.3 (realtime.ts, [TIEMPO-REAL.md](TIEMPO-REAL.md)) no se usaba; Arturo no lo quiere. |
| Por internet (31/10) | **Unirse con código.** Además de la red local y Tailscale, un servidor intermedio propio en internet: quien aloja recibe un código y el otro lo escribe, sin instalar nada ni abrir puertos. Elegido entre seguir solo con Tailscale (ya funcionaba en la 0.11.0), abrir el puerto con UPnP y Steam. Lo hace Red. **Aparcado (31/10):** irá en una máquina que monta Arturo para alojar cosas, no en un servidor de pago; se retoma cuando esté. El plan de Red, en [UNIRSE-CON-CODIGO.md](UNIRSE-CON-CODIGO.md). |
| Por internet, abriendo el puerto (1/11) | **UPnP y NAT-PMP, con una clave corta.** Arturo prefirió no hacer perforación de NAT ni cambiar a UDP. Al alojar, la app pide al router que abra el 41391 y enseña la dirección pública con una clave de 4 a 6 letras, sin la cual no se entra por internet. Con CG-NAT o sin UPnP, lo dice y remite a Tailscale. El servidor intermedio sigue aparcado. Lo hace Red (`Net/RouterPort.cs`), con UI (el menú de la sala) y Lore. |
| La prueba de la 0.11.0 (31/10) | **Entra lo de Ana, salvo las barras de vida más grandes.** Arturo decidió cada nota en el cuadro: todos los zombis andan a la vez con la cámara abierta y luego atacan de uno en uno; la cámara a 45°; asomarse a una ventana, desde la casilla de delante, descubre el edificio; dentro de uno sin descubrir, los zombis no se ven ni se les puede disparar (regla nueva, solo en C#). Lo detalla [PRUEBA-2026-10-31.md](PRUEBA-2026-10-31.md). |
| Las notas de Arturo sobre la 0.11.1 (31/10) | **Entran todas.** Arturo las decidió en el cuadro: menos zombis fuera de la ciudad, y las estrellas de cada misión según sus zombis, con una fila «Lugar»; el recluta con 55 de puntería y el zombi común sin −10 de defensa; los enemigos no ven a través de ventanas; en el asedio, el pinar en negro y solo los zombis a la vista; La Lata se va sin animación de subir, con un giro de cámara; la línea de movimiento la tapan solo las personas. Lo detalla [PRUEBA-2026-10-31-ARTURO.md](PRUEBA-2026-10-31-ARTURO.md). |
| La interfaz de combate nueva (1/11) | **En un commit propio, para poder deshacerla.** La especificación de Arturo (UI-COMBATE v1.0, UI-01 a UI-12; el PDF, en `.dev-snapshots/gameplay/notas-interfaz-01-11/`) está entera en **bc9810c**, sin nada más. Para volver a la interfaz de antes: `git revert 5787472 bc9810c` (5787472 es un arreglo de una línea en SquadList.cs; comprobado el 1/11 que bc9810c solo deja el árbol como en fd05e5c). Si algo posterior toca los mismos archivos del HUD, el revert puede pedir resolver choques. Arturo eligió también: en el panel, solo los soldados de cada jugador; el mapa, solo con la M; «Ceder el mando» y «Ver a…», al menú. Detalle en UNITY-UI.md §7. |

Por qué Unity: los paquetes de Synty y Sidekick están hechos para Unity, con sus shaders, su luz y su retargeting humanoide. Con eso desaparece la conversión con Blender y desaparecen las trampas de atlas, el retargeting de Mixamo a mano y la sombra de pliegues horneada. Además corre nativo, no en WebGL dentro de Electron.

## 2. Estructura en el repositorio

```
engine-cs/                 el motor en C#: paquete local de Unity y biblioteca de .NET a la vez
  package.json             paquete de Unity (com.bastion.engine)
  Runtime/                 el código (asmdef Bastion.Engine), sin nada de UnityEngine
  Bastion.Engine.csproj    el mismo código para .NET: netstandard2.1, C# 9
  tests/                   pruebas con xUnit (net10), con `dotnet test`
  golden/                  partidas grabadas con el motor de TypeScript
unity/Bastion/             el proyecto de Unity (URP)
  Packages/manifest.json   "com.bastion.engine": "file:../../../engine-cs"
  Assets/Bastion/          lo nuestro: escenas, scripts del cliente, prefabs, materiales
  Assets/Synty/            importado del disco externo, **no se sube** (.gitignore)
packages/                  la versión de Three.js, congelada en la 0.10.1
```

- **El motor no usa UnityEngine.** Así se prueba con `dotnet test` sin abrir el editor, que es lo que las sesiones de Claude pueden hacer solas.
- **C# 9 y netstandard2.1**, lo que acepta Unity 6. Sin construcciones más nuevas.
- **Los paquetes de Synty** se importan desde `/Volumes/4T_White/50_INBOX/Synty` (`.unitypackage`), solo los que se usan. Nunca a GitHub, igual que `assets-src/`.

## 3. Cómo se comprueba que el motor de C# es el mismo

- **Las pruebas de TypeScript** (558) se pasan a xUnit una a una, con el mismo nombre y el mismo comentario.
- **Partidas grabadas** (`engine-cs/golden/`): un script de TypeScript juega misiones enteras con el jugador automático, por semilla, tipo de misión y bioma. Guarda el estado inicial, cada orden, los eventos que salen y un resumen del estado tras cada orden. El motor de C# repite las mismas órdenes y tiene que dar los mismos eventos y el mismo estado.
- **El azar:** `Rng` es sfc32 con aritmética de 32 bits; se pasa exacto (uint en C#).
- **Coma flotante:** no hay tolerancia; todo coincide al bit. Las matemáticas de JavaScript están en `engine-cs/package/Runtime/Js.cs`:
  - `Js.Atan2`: fdlibm con la suma y la multiplicación fundidas, como node en Apple silicon;
  - `Js.Hypot`: la de V8;
  - `Js.Round`;
  - ordenaciones estables.
  Por eso las grabaciones se hacen en el Mac. Ver engine-cs/README.md.


## 4. Estado (26/10)

**El hito 1 está hecho:** una misión de ciudad entre dos apps de Unity. Arturo la ha probado y funciona en Mac y Linux.

**El hito 2 está hecho a falta de la prueba con Arturo** ([PRUEBA-CAMPAÑA-UNITY.md](PRUEBA-CAMPAÑA-UNITY.md)): la campaña en C#, la sala con campaña, las pantallas de estrategia, el Asentamiento entero (fases 1–6: el claro, las piezas, la vida, construir, andar por la base y los puestos del asedio) y lo que faltaba del combate para una misión normal (la mochila, los avisos, la cámara al soldado…). Lore ha repasado todos los textos visibles contra la 0.10.1.

| Parte | Estado |
|---|---|
| Motor en C# (`engine-cs`) | Pasos 1–3 hechos: tipos, azar, contenido, mapa, visión, caminos, reglas, IA, montaje de misiones, generador de mapas, base y asedio. Idéntico al bit con TypeScript: 119 mapas, 17 partidas grabadas, 623 pruebas. Campaña (24/10, tarde): los tipos (`Types/Campaign*.cs`, `QuestTypes.cs`) y todas las lecturas (`Runtime/Campaign/`), comparados con 29 estados grabados (`golden/campaign/`) y con las partidas guardadas reales. campaign.ts entero (21d61ab): 161 funciones, idéntico a TypeScript en 14 campañas grabadas hasta los dos finales; 886 pruebas (26/10). El tiempo real no se trae (Arturo, 29/10). |
| Red (`engine-cs/package/Runtime/Net`, Red) | Sala, TCP, reconexión, «Partidas en tu red», reloj de turno, guardado. EngineRules con Setup.CreateMission. La sala con campaña (EngineCampaignRules): órdenes de la base, propuestas y votos, la misión a medias, el asedio desde la campaña; el guardado lee las partidas de la 0.10.1 (v20–v25, con su migración y las de antes de la 0.7 apartadas). 162 pruebas. Apps de Mac (firmada ad hoc), Windows y Linux con `AppBuild`. La medida sola en la app (`BASTION_MEDIR`: ciudad, ciudad de noche, asedio y base) y `medir.bat` para el PC de la 3090 ([PRUEBA-PC-3090.md](PRUEBA-PC-3090.md)); falta que Arturo la pase. |
| Textos (`Runtime/Lore`, Lore) | lore.ts entero (24/10) y quests.ts (`Runtime/Quests.cs`); transmissions.ts (las transmisiones de radio) se quitó entero en la 0.11.7 (Ana, «Informe para desarrollo 3» #1; Arturo, 2/11). Los arreglos de texto sobre la 0.10.1, en una tabla viejo→nuevo (`golden/text-fixes.ts`) que usan también las grabaciones. |
| Mapa y unidades (Arte) | Mapa desde el estado, edificios con piezas de Synty (muros, suelos, tejados, escaleras, corte), niebla con edificios y plantas, luz y posproceso de la 0.10.1, figuras Sidekick con sus animaciones (fusil, granada, morir, comer, agacharse, esperas), efectos. Faltan la pistola y las armas blancas en la mano, recargar, y los muertos de City Zombies y Boss Zombies. El Asentamiento en 3D, entero (26/10). |
| HUD y control (UI) | HUD del combate entero, controlador, cámara, marcas, reproductor de eventos con su ritmo, plano al hombro, menú de Esc con Ajustes, pantalla de entrar y sala, suelo, lo menudo, marco fuera de juego, calles vestidas. El estilo del HUD queda en «Mixta» (Arturo aparcó elegir otro, §7.1 de UNITY-UI.md). |
| Apps | Mac (universal, firma ad hoc comprobada), Windows y Linux; `AppBuild.*` en Assets/Bastion/Client/Net/Editor. Windows sin probar en un PC. |

**Decisiones de Arturo del 26/10** que se apartan de la 0.10.1:
- Pulsar una zona del Mapa abre su hoja con todas sus misiones (UI).
- «Cancelar misión» se vota, como desplegar y contactar (motor). Las partidas grabadas de dos jugadores con una cancelación lo tienen en cuenta en la repetición.
- Los avisos del combate sin género: «No tienes heridas», «No sufre aturdimiento…».
- Los rótulos de estado de las soldados concuerdan: ATURDIDA, DESORIENTADA, EVACUADA (UI).
- «SER ANFITRIÓN» pasa a «ALOJAR LA PARTIDA», neutro, como «quien aloja la partida» (UI y Red). Con la pasada de Lore, el estado del otro jugador también va sin género («A punto», «Sin conexión», «En línea»), y fuera «hostiles» y «VIP» («la persona retenida»).

## 5. Lo que falta para terminar el port

Arturo, 24/10: «quiero que terminemos con el port a Unity completo». Orden propuesto; cada hito se prueba con Arturo antes de seguir.

### Hito 2: la campaña

| Quién | Qué |
|---|---|
| Gameplay | **Hecho** (21d61ab). `campaign.ts` (3983) a C# (`quests.ts` y `transmissions.ts` pasaron a Lore el 24/10), con partidas de campaña grabadas en TypeScript (`engine-cs/golden/`), como los pasos 1–3. Las pruebas de campaña que quedaron fuera en los pasos 2 y 3 (base, comida, infección en la base, apariencia, misiones). Lo que leen las pantallas de estrategia (unas 70 funciones) está en UNITY-ESTRATEGIA.md §4; lo que espera la sala de Red, en `ICampaignRules` (engine-cs/wip/Net/Campaign.cs). Una campaña de muestra avanzada (versión 25, día 20, nivel 3, dos jugadores) para grabar partidas: `.dev-snapshots/ui/estrategia-0101/partida-muestra.json` (fuera de git, en este Mac). |
| Red | La sala con campaña: CampaignCommand, propuestas y votos, partidas guardadas de la campaña (y leer las de la 0.10.1), el asedio desde la campaña. room.ts tiene todo eso. La campaña de la app de Electron no se trae (Arturo, 24/10): en Unity se empieza una nueva. |
| Lore | Lo que queda de lore.ts (STORY, missionBriefing, storyVip, recoveryItem, fellHint…). |
| UI | El plan está en UNITY-ESTRATEGIA.md. Las pantallas de estrategia: strategy/screen.ts (3551) y lo suyo: Mapa (geoscape, valley*), Soldados con la hoja (skillSheet, hexBoard), Armería (arms), Escuadra y fila de Lanzar misión (stage), diario y transmisiones, propuestas y votos, acontecimientos. |
| Arte | Los retratos (render a textura) y el Asentamiento en 3D: clearing.ts, clearingArt.ts, clearingLife.ts, baseKit/buildMode (construir piezas), la caravana y La Lata, el campamento con sus clips (MIXAMO campamento), andar por la base en primera persona (walkAbout). |

La prueba del hito 2 con Arturo: [PRUEBA-CAMPAÑA-UNITY.md](PRUEBA-CAMPAÑA-UNITY.md).

**Lo que falta del combate respecto a la 0.10.1** (repaso de UI, 26/10, de hud.ts y game.ts).
- **Antes de la prueba:**
  1. el clic derecho en el mapa;
  2. «Cámara al soldado»;
  3. el sello «TU TURNO» que suelta cualquier clic;
  4. «Turno enemigo» sobre la barra;
  5. la tarjeta de llegada que tapa las teclas;
  6. la pantalla de carga;
  7. «Volviendo al Bastión…»;
  8. la mochila, con su `ItemCommand`;
  9. los avisos de enemigo a la vista;
  10. la nota al lanzar;
  11. el desgaste del arma en el panel.
- **Después:** la hoja del soldado, la ficha del enemigo, el asedio (la rosa con datos, las brechas, la vida de puertas y barricadas), «Mochila de X» en el suelo, la luz de noche en la casilla del tirador, las filas de Ajustes, y los avisos del compañero que se va o vuelve.

### Hito 3: lo que queda

**Repaso del dibujo de la 0.10.1 contra Unity** (Arte, 26/10: mapView.ts, fx.ts, unitViews.ts, clearingArt.ts, night.ts). Faltan o están a medias:
1. Dos fallos de la base: la fila sale también paseando por el campamento, y no cambia el arma hasta que cambia el vehículo.
2. La terminal de hackeo, que no se dibuja.
3. Andar (esquinas redondeadas, apartarse, girar suave), apuntar (subir el arma) y la postura de vigilancia.
4. Lo tirado en el suelo al caer alguien, y el objetivo elegido que se enrojece.
5. El fundido al entrar en la vista y el paso del zombi al atacar.
6. Las ventanas tapiadas por dentro y los muebles de las clínicas.
7. La luz propia de los soldados en la base (Arturo, 23/10).
8. Escaleras y saltos animados, registrar, copas de árboles que se desvanecen, palés y generadores, el asedio que tiembla, cambiar de arma, levantarse un cadáver.
9. La multitud horneada y las sombras solo de los 24 más cercanos, según la medida del PC.
10. El cielo, la luz de relleno fija y detalles raros.

Hecho y revisado con tiras contra la 0.10.1 (27/10), salvo el 9, que espera la medida del PC de la 3090 (`medir.bat`). De paso: la oclusión de los suelos de casas, tiendas y clínicas, como la de la 0.10.1 (UI); la luz del cielo con los números de three (`Mood`, AmbientMode.Custom); las chapas de La Mula, de acero como en Synty (Arturo, 27/10). Detalles en UNITY-ARTE.md y UNITY-UI.md §7.

UI ya hizo la marca de las cajas de botín, el anillo de los enemigos, apartar figuras en el plano al hombro y los tiempos de las llegadas (UNITY-UI §7, 26/10).

- **Sonido** (audio/: sonidos grabados, ambiente, música, radio). Lo hace Red, que hizo el de la 0.10.x (pasó de Arte el 26/10, porque Arte va con el Asentamiento). Cómo se mide sin oír, en la memoria «Medir sonidos».
  - **Generado y fuera de git** (Gameplay, 26/10). En WAV son unos 575 MB. `unity/tools/sonido.sh` los saca del código y de los Opus de la 0.10.1 a `Assets/Bastion/Client/Audio/Resources/Generado/`, que no se sube, como Synty y Mixamo.
  - **Más peso en la app:** unos 60 MB, porque Unity lo comprime a Vorbis.
  - **AppBuild se para** si falta. Para compilar sin sonido hay que pedirlo de forma explícita.
  - **Depende de packages/:** de `packages/client/src/audio` y `public/audio`. Al retirar packages/, eso se queda o se mueve a `unity/tools/sonido/`.
  - **Música y camas:** la música va en bucles de 8 min y las camas de ambiente en bucles de 4 min. Si se nota la repetición, se pasan a C# en vivo, como music.ts y ambience.ts.
- **El resto de biomas y mapas:** campo y fábrica con Alpine, Farm y Woodland; la base y el asedio con sus luces. UI empezó el 26/10 con lo suyo: el suelo, los caminos, las calles y una partida de cada tipo junto a la 0.10.1. El suelo, los caminos y las calles ya casan con la 0.10.1 (capturas en `.dev-snapshots/ui/unity/mapas/`). Falta:
    - **Hecho (26/10):**
      - UI: el `drawBlock` de mapView.ts (silos, camiones, contenedores, depósito de agua) en `MapBuilder.Blocks.cs`, y los techos de los surtidores con su desvanecido;
      - Arte: Woodland Apocalypse Map importado y las cercas con sus raíles (`fenceAt`/`rails`).
      - `MapCatalogMake.Check` no echa nada en falta en 25 mapas de campo, 14 de fábrica y 67 de ciudad. Alpine no hace falta para estos mapas.
    - **Las sombras con la cámara lejos** (Arte, 26/10, `ShadowFit`): llegan a 95 m en combate y a 90 m en la base, en los tres niveles. En el Mac cuestan 0,4 ms de GPU en Altos con la cámara al máximo. Falta medirlo en el PC de la 3090.
- **La noche:** luces de farolas, coches y ventanas, y las bengalas.
- **Armas:** hecho el 26/10 (Arte). Pistola, machete y cuchillo en la mano, en la funda y en el cinto, y recargar, en combate y en la base. Los detalles, cerrados el 29/10: la pistola fuera del muslo (`PistolSpread`, el `spreadPistol` de la 0.10.1); la guardia con la hoja ya estaba (27/10); el fusil a la espalda al agacharse (`slingOnBack`) no se trae, porque solo salía en el tiempo real.
- **Muertos especiales** (Arte, hecho el 26/10).
  - Los cuerpos: City Zombies para el Corredor y el Acorazado, Boss Zombies para el Chillón y el Hinchado, con los tamaños y el pelo de la 0.10.1.
  - El grito del Chillón con su cuerpo y el reventón del Hinchado.
  - La inclinación de cada tipo al andar (STYLES), medida por el ángulo del tronco contra la 0.10.1.
  - Los 17 cuerpos del zombi común, con los 12 «vivos hechos muertos» horneados en `Assets/Synty/Extra/Undead`, fuera de git: los hace FigureCatalogMake.
  - Con ellos, para todas las unidades: el destello rojo al recibir un golpe y al morir, y la muerte y el golpe según de dónde viene (antes iban al azar).
  - Los detalles, cerrados el 29/10: la nube del Hinchado, con los verdes de la 0.10.1; la mano en la cadera en Soldados ya era como allí (lo distinto era el vaivén de la pantalla).
- **Rendimiento medido** en el PC de la 3090 («alto» ≤ 4,2 ms) y en el Mac («medio» ≥ 30 fps), con red.log. Arturo, 1/11: una pasada de rendimiento de Arte, después de los animales, el fuego, los choques y el clima, y antes de la fase 2 de los edificios. Se mide el asedio con muchos zombis, la ciudad grande con la cámara lejos y el fuego con tormenta, y se prueban el GPU occlusion culling de Unity 6, los LOD, el instancing, juntar las mallas del mapa y no animar lo que no se ve. El FOV se queda en 40°. Sigue faltando `medir.bat` en el PC de la 3090.
- **Perros zombi** (Arturo, 1/11, «sí, más adelante»): un enemigo nuevo con los dóberman y pastores alemanes zombi del Dog Pack de POLYGON (morder, ladrar, gruñir, morir). Rápido y débil; Gameplay propone sus reglas cuando acabe lo que hay en marcha.
- **Releases de la app de Unity.** La primera es la 0.11.0 (29/10), con las notas de Lore ([NOTAS-UNITY.md](NOTAS-UNITY.md)). La 0.11.1 (31/10, commit 8a3400e) lleva las notas de Ana del 31/10 ([NOTAS-0.11.1.md](NOTAS-0.11.1.md)). La 0.11.2 (1/11, commit eca8150), las de Arturo sobre la 0.11.1 ([NOTAS-0.11.2.md](NOTAS-0.11.2.md)). La 0.11.3 (1/11, commit c5070ba), las de Ana del 1/11, la interfaz de combate nueva, el mal tiempo, el fuego, los coches chocados, los animales, las casas, jugar por internet con UPnP y la pasada de rendimiento ([NOTAS-0.11.3.md](NOTAS-0.11.3.md)). Su prueba con la app encontró el tejado en punta sin normal (arreglado antes de publicar) y que, con el Mac bloqueado y la pantalla dormida, el combate sin tope de fps se queda esperando la sincronía vertical (también la 0.11.2; Arturo: publicar y mirarlo después). UI lo probó después: aunque la pantalla despierte, el combate no vuelve. Arturo, 1/11: en la 0.11.4, en el Mac el combate sin tope va sin vsync y limitado a la frecuencia de la pantalla (GameSettings, UI); en Windows no cambia. La 0.11.4 (1/11, commit 277e80a) lleva las fases 2 a 4 de los edificios, las tiendas, las oficinas y las naves ([EDIFICIOS-SYNTY.md](EDIFICIOS-SYNTY.md) §6), el arreglo de las fachadas en peine de los bloques del borde y ese arreglo del vsync ([NOTAS-0.11.4.md](NOTAS-0.11.4.md)); Arturo, 1/11: una sola versión con todo, en cuanto acabaron las naves. Su prueba con la app (UI, `.dev-snapshots/ui/unity/app-0114/`) pasó la pantalla dormida sin tope (136–144 fps, vuelve al despertar), pero en la niebla las paredes de chapa de las naves salen con rayas claras que siguen sus ondas (Arturo: publicar y arreglarlo en la 0.11.5, Arte). También para la 0.11.5, de UI: la última línea del menú en red.log lleva el tope del combate (Perf). La 0.11.5 (2/11, commit 9e65ead) lleva las notas de Arturo del 1/11 por la tarde ([PRUEBA-2026-11-01-ARTURO.md](PRUEBA-2026-11-01-ARTURO.md)): sin pistola, granadas ni botiquín al empezar una campaña nueva, la pistola de la Armería, el coche de policía de la primera misión, el cóctel molotov antes que las granadas, sin armas al subir de nivel, el asedio con un grupo cada turno y el tratamiento por la placa; los animales del bosque de SIMPLE (Arturo); y lo que dejó la prueba de la 0.11.4 ([NOTAS-0.11.5.md](NOTAS-0.11.5.md)). La 0.11.6 (2/11) lleva las notas de Ana del 1/11, «Informe para desarrollo 2» ([PRUEBA-2026-11-01-ANA.md](PRUEBA-2026-11-01-ANA.md)): «Esconderse», cada zombi por su cuenta, primero muerden y luego andan, agachados con la escuadra oculta (al paso de pie; para correr se levantan), el levantado con su cuerpo, zombis sin pieles moradas ni cuerpos repetidos, la vista de Soldados recta, un perro y gatos de SIMPLE - Cats ([NOTAS-0.11.6.md](NOTAS-0.11.6.md)). Su prueba con la app (UI, `.dev-snapshots/ui/unity/app-0116/`) encontró que en Metsovo la cámara se queda sobre la azotea de un bloque de tres plantas cuando los zombis que se ven están detrás de él, al anunciarlos, tras el tiro y en el contacto (ya en la 0.11.5; Arturo: publicar y arreglarlo lo primero en la 0.11.7, Arte y UI). Para Lore, en la 0.11.7: el registro dice «abre el coche de policía» también al abrir el maletero. La 0.11.7 (2/11) arregla esa azotea: el corte de edificios (Cutaway) solo recalculaba lo que ve la escuadra al cargar, porque el estado cambia en el sitio evento a evento; ahora lo hace con cada evento (`GameState.Seq`) y reutiliza la vista de las unidades. Además, la ropa clara de los zombis se apaga aunque tenga poco color. Y lleva las notas de Arturo del 2/11 y el «Informe para desarrollo 3» de Ana ([PRUEBA-2026-11-02-ARTURO.md](PRUEBA-2026-11-02-ARTURO.md)): 2 soldados por jugador con 2 pistolas, un machete y una palanca; la palanca quita las tablas de ventanas y puertas tapiadas; registrar cuerpos y maleteros, con coches de policía en cualquier misión; la piedra sin daño; la horda solo por vuestro ruido; comprar y fabricar por cantidades; katana, espada, rastrillo y palo; un recluta gratis a quien se queda sin soldados; en el asedio, los heridos si no hay sanos; fuera las transmisiones de radio; marcas en vez de letreros al ver zombis; cada uno los suyos a la izquierda; ajustes de pantalla; el perro, el fuego del molotov y las ventanas en columnas ([NOTAS-0.11.7.md](NOTAS-0.11.7.md)). Su prueba con la app (UI, `.dev-snapshots/ui/unity/app-0117/`) salió bien; la pantalla completa no se pudo probar con el Mac bloqueado. Para la 0.11.8: «Resolución» marca 1920 × 1080 cuando el tamaño de la ventana no está en la lista (UI), y al restaurar una misión en la que el anfitrión no tiene soldados en el campo, la sala acaba el turno de la escuadra antes de que entre el compañero (Red). La 0.11.8 (3/11) arregla las dos: la sala espera 5 minutos al compañero que aún no ha entrado (`Room.AwaitAbsent`, `PlayerInfo.GraceMs`), el mando pasa solo al que vuelve con soldados (`Rules.SyncTurn`) y «Resolución» enseña el tamaño de ahora ([NOTAS-0.11.8.md](NOTAS-0.11.8.md)). La 0.11.9 (5/11) lleva lo que dejó la prueba de la 0.11.8: al restaurar con el anfitrión sin nada que mover, el mando ya no pasa al compañero que aún no ha entrado (`Rules.SyncTurn` con `back`, `Room.ResyncTurn`); y la pasada de rendimiento medida en el Mac (Arte, `.dev-snapshots/arte/unity/rendimiento-0411/`): «alto» de 1,0 a 1,35 ms menos, gradación y niebla sin copia (`ScreenPass`), hordas lejanas a medias; el depth priming queda fuera. Su prueba con la app (UI, `.dev-snapshots/ui/unity/app-0119/`) encontró que «Bajos» no existía en las apps desde el hito 1 (el nivel «Bajo» excluía Standalone); Arturo: arreglarlo antes de publicar, y entra con la nieve con sombras en una cascada ([NOTAS-0.11.9.md](NOTAS-0.11.9.md)). La 0.12.0 (6/11) lleva las notas de Arturo del 5/11 ([PRUEBA-2026-11-05-ARTURO.md](PRUEBA-2026-11-05-ARTURO.md)): el movimiento aparte de las acciones (soldados y vivos, 2 acciones y 8 puntos; los muertos, 1 acción y su paso, 5 los comunes; por tramos; correr es pasar de 5 en el turno, y se oye; `Unit.Moves`, `Paths.MoveBudget`), la cobertura que oculta de los zombis (3/4 y la mitad, `Los.ConcealShare`), el ojo gris y la vista de todos (V), solo el Sabotaje con cuenta atrás, la puerta abierta que descubre la casa, los maleteros en su sitio y abiertos, el contorno y la mochila al registrar, los perros sin tirones y los reclutas con arma ([NOTAS-0.12.0.md](NOTAS-0.12.0.md)). Su prueba con la app (UI y Red, `.dev-snapshots/ui/unity/app-0120/`, `.dev-snapshots/red/app-0120/`) encontró, y se arreglaron antes de publicar: el mando que no se podía ceder a quien solo tenía movimiento (`AbilityTexts.BusySlots`), la copia del estado sin el movimiento (`Events.CloneUnit`), el maletero que no se abría en el primer cambio tras cargar (`CombatSession.Load`) y la puerta que pedía una acción (`Abilities.AbilityBlocker`). Los zombis quedan más flojos (un mordisco, 5 casillas): Arturo lo mirará jugando. La 0.12.1 (6/11), decidida por Arturo tras publicar la 0.12.0 ([PRUEBA-2026-11-05-ARTURO.md](PRUEBA-2026-11-05-ARTURO.md), sección final): correr es gastar todo el movimiento de golpe, un solo tramo desde los 8 puntos (`Paths.IsRun`; fuera `Content.QuietTiles`); Forma física sube la esquiva, de +3 a +5 por barra (`Bars.DodgeBase`), y la salud solo el tablero; recibir daño y las rozaduras la llenan (`DeedIds.Hurt`, `Dodge`); las barras llenas llevan su crecimiento de antes (`BarFilled.From`, `BarFilledEvent.From`) para que los textos digan la esquiva exacta; la esquiva en las hojas (UI) ([NOTAS-0.12.1.md](NOTAS-0.12.1.md)). Su prueba con la app (UI, `.dev-snapshots/ui/unity/app-0121/`) encontró la etiqueta del parte fuera de la tarjeta (`Squad.uss`), arreglada antes de publicar. La 0.12.2 (7/11), con las notas de Arturo del 7/11, jugando solo con la 0.12.1 ([PRUEBA-2026-11-07-ARTURO.md](PRUEBA-2026-11-07-ARTURO.md)): nadie se mueve en diagonal, ni soldados, ni zombis, ni vivos (`Paths.ComputeReach` en 4 direcciones; el cuerpo a cuerpo sigue llegando a la esquina; los mapas generados no cambian), con el Hinchado que esperaba sin fin al pie de una escalera arreglado (`Ai.Walk`); al seguir una misión sin el compañero ya no se le esperan 5 minutos (fuera `Room.AwaitAbsent`), y el turno espera a sus soldados, que manda quien está (`Rules.MaybeEndXcomTurn`); y el clic en la casilla de detrás de un soldado, que lo volvía a elegir con la cámara lejos (`CombatController.BodyUnder`, UI) ([NOTAS-0.12.2.md](NOTAS-0.12.2.md)). Se compiló en una copia limpia del repositorio, sin el trabajo de iOS a medias. La 0.12.3 (7/11) lleva el informe de interfaz de Arturo del 7/11 con la maqueta final v39 ([PRUEBA-2026-11-07-ARTURO.md](PRUEBA-2026-11-07-ARTURO.md), «Para la 0.12.3»), en dos commits como pide: la pantalla de preparación de la misión (la barra de arriba más alta y oscura, «Tus soldados» y los carteles en placas rasgadas, sin los aros del suelo, el panel de la misión con «LANZAR MISIÓN» con muescas, y la Marea en el Mapa: Arturo, «Llevarla al Mapa») y las tarjetas de todo el juego en placas rasgadas al 75 % en vez del acero (`TornPlates`, el sprite según las proporciones) ([NOTAS-0.12.3.md](NOTAS-0.12.3.md)). La pantalla completa sigue sin probar (Mac bloqueado). Falta retirar packages/ (Three.js, Electron). Lo que decidió Arturo el 29/10:
  - **El número** sigue a la 0.10.1 (`bundleVersion`; la app decía «0.1.0»).
  - **En el Mac sigue siendo `Bastion.app`:** al copiarla a Aplicaciones sustituye a la de Electron. Las partidas no se pisan, porque Unity guarda en `Bastion-Unity/`.
  - **Sin comprimir** y **solo Mac (universal) y Windows**, sin Linux.
  - **El tamaño** (Red, 26/10). La app del Mac ocupa 842 MB porque la compilación no comprime nada: el zip ya pesa 224 MB.
    - Con LZ4HC baja a 306 MB sin cambiar lo que se ve. Red lo midió en el Mac:
      - la carga sube de 0,2 a 0,6 s (combate 3,5→3,7 s; base 1,2→1,8 s);
      - los fotogramas no cambian;
      - compilar tarda 95 s más;
      - el zip apenas cambia (214→201 MB).
    - Por defecto se compila sin comprimir; LZ4HC es una opción de AppBuild. En las releases, se decide con Arturo.
    - Hecho (26/10): fuera de la app los 28 presets de Sidekick que no se dibujan y las 814 piezas que no usa el armario (`FigureCatalogMake.Trim`, a `Assets/Synty/Extra/SidekickSpare`). Con el sonido, la app queda en 686 MB sin comprimir y 189 MB en zip, y entra antes en combate (2,0 s). AppBuild comprueba los presets y las piezas que se usan.
- **El tiempo real** (realtime.ts): no se trae (Arturo, 29/10).
- **Por internet con código** (Red, 31/10; decisión en §1): aparcado hasta que Arturo monte su máquina. Está el plan de Red: el servidor intermedio, el código en el menú de quien aloja y el campo para escribirlo al unirse (UI), y sus textos (Lore). Primero en local, con dos apps de prueba; después, en esa máquina. Mientras, por internet se juega con Tailscale.
- **La prueba de la 0.11.0** (Ana, 31/10; decisión en §1, [PRUEBA-2026-10-31.md](PRUEBA-2026-10-31.md)): UI, nueve cosas del combate y la base (el turno enemigo, la cámara a 45°, las banderas…); Arte, la Escuadra de frente y asomarse a una ventana; Gameplay, la regla de los edificios sin descubrir: hecha en el motor (`Buildings.cs`), con los grabadores de C#, y la niebla de Arte la lee de ahí (31/10).

## 6. Cómo se trabaja (lo aprendido)

- **Motor:**
  - lo que está a medias va en `engine-cs/wip/`, porque Unity se para con cualquier error de `package/`;
  - las cachés estáticas, `[ThreadStatic]`, porque la sala ejecuta reglas en su hilo;
  - ningún `\uXXXX` crudo en el código (la herramienta Write lo convierte; comprobar con `grep -P`);
  - cada módulo grande se pasa con un agente y se comprueba con grabaciones de TypeScript.
- **Unity:**
  - `unity/tools/unity.sh` compila, y no puede haber dos Unity a la vez: avisar antes de lanzar;
  - `unity/tools/captura.sh` saca capturas en el editor;
  - las apps compiladas se comprueban con su propia captura (`-shotOut`), no solo con el registro. La pantalla negra del 24/10 salió así.
- **Lo que el editor no enseña:**
  - en la app compilada, `Shader.Find` devuelve null si el shader no va incluido: hay que referenciarlo desde un material o un asset, o meterlo en «Always Included Shaders» (26/10);
  - los UIDocument sin PanelSettings no pintan nada (24/10).
  Por eso las apps se prueban de verdad, con dos apps y `-shotOut`.
- **Escenas:** un solo dueño por archivo .unity. Partida es de Arte; Inicio, de UI.
- **En otra máquina, tras clonar:** importar Synty (`unity/tools/importar-synty.sh`), los clips de Mixamo y la herramienta Sidekicks (assets-src/sidekicks-tool), y ejecutar SourceProps.Run, SnowFeatureMake.Run, MapCatalogMake.Run, PieceMake.Run, GradeMake.Run y ProjectSetup.Run (UNITY-ARTE.md §6). El sonido, con `unity/tools/sonido.sh` ([UNITY-SONIDO.md](UNITY-SONIDO.md)): unos 580 MB de WAV generados de la 0.10.1, fuera de git.
- **Revisión:** cada animación, con tira a 10 fps de cerca y desde la cámara del juego; cada pantalla, junto a la 0.10.1 con el mismo encuadre. Gameplay aprueba.
- **Medir en el Mac:** con vsync, la GPU de un nivel ligero va medio parada y macOS le baja la frecuencia, así que sus ms de gpu suben sin perder fotogramas. Para comparar niveles, medir sin tope con la GPU llena (`MapLab.Variants`), no la gpu con vsync (Arte, 26/10).
- **Antes de cada commit y de cada compilación de la app:** preguntar a todos si tienen algo a medio probar en Assets o en engine-cs. A Assets solo se copia lo ya probado. Además de `dotnet test` y `unity.sh`, pasar `CombatLab.Close` y leer sus excepciones (3af21bd se llevó código de armas a medio probar).
- **Apps de prueba:** reutilizar una compilación y abrirla en modo de prueba (`BASTION_SOLO_LOCAL` o cualquier mando de prueba), que solo escucha en 127.0.0.1. Cada compilación nueva le saca a Arturo los cuadros de macOS del cortafuegos y la red local.

## 7. Para un Gameplay nuevo

1. Leer CLAUDE.md, este documento, engine-cs/README.md, UNITY-ARTE.md, UNITY-UI.md y PRUEBA-UNITY.md. La memoria (MEMORY.md) se carga sola en este Mac.
2. Las otras sesiones son Arte, UI (sale en ListAgents como «UI»), Networking (Red) y Lore. Se les habla con SendMessage, y ellas avisan con mensajes. Presentarse: «soy el Gameplay nuevo, sigo donde lo dejó el anterior».
3. Comprobar antes de nada:
   ```bash
   dotnet test engine-cs/tests
   ```
   ```bash
   unity/tools/unity.sh
   ```
4. Seguir el §5 por el hito 2. Lo primero es campaign.ts a C#, con un agente y grabaciones de partidas de campaña.
5. Decisiones de Arturo, siempre en el cuadro (AskUserQuestion). Commits y publicaciones, solo con su «sí».
