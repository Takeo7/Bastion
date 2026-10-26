# El paso a Unity

Plan del 23/10, después de la 0.10.1. La 0.10.1 (commit 9ac9753) es la última versión con el cliente en Three.js.

## 1. Decisiones de Arturo (23/10)

| Qué | Decisión |
|---|---|
| Reglas y servidor | **Todo a C#.** El motor (27.000 líneas de TypeScript) y el servidor de Node se reescriben en C#. |
| Primer hito | **Una misión de combate.** Un mapa generado, la escuadra y los zombis con sus animaciones, mover, disparar y el turno enemigo, entre dos jugadores. |
| La versión de Three.js | **Congelada.** Arturo y Ana juegan con la 0.10.1; solo se arregla lo que impida jugar. |
| Instalación | Unity Hub con Homebrew; Arturo inicia sesión (licencia Personal). Después, Unity 6 LTS desde la línea de órdenes, con soporte de Mac y Windows. |

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
| Motor en C# (`engine-cs`) | Pasos 1–3 hechos: tipos, azar, contenido, mapa, visión, caminos, reglas, IA, montaje de misiones, generador de mapas, base y asedio. Idéntico al bit con TypeScript: 119 mapas, 17 partidas grabadas, 623 pruebas. Campaña (24/10, tarde): los tipos (`Types/Campaign*.cs`, `QuestTypes.cs`) y todas las lecturas (`Runtime/Campaign/`), comparados con 29 estados grabados (`golden/campaign/`) y con las partidas guardadas reales. campaign.ts entero (21d61ab): 161 funciones, idéntico a TypeScript en 14 campañas grabadas hasta los dos finales; 886 pruebas (26/10). Falta realtime (solo si Arturo lo quiere). |
| Red (`engine-cs/package/Runtime/Net`, Red) | Sala, TCP, reconexión, «Partidas en tu red», reloj de turno, guardado. EngineRules con Setup.CreateMission. La sala con campaña (EngineCampaignRules): órdenes de la base, propuestas y votos, la misión a medias, el asedio desde la campaña; el guardado lee las partidas de la 0.10.1 (v20–v25, con su migración y las de antes de la 0.7 apartadas). 162 pruebas. Apps de Mac (firmada ad hoc), Windows y Linux con `AppBuild`. La medida sola en la app (`BASTION_MEDIR`: ciudad, ciudad de noche, asedio y base) y `medir.bat` para el PC de la 3090 ([PRUEBA-PC-3090.md](PRUEBA-PC-3090.md)); falta que Arturo la pase. |
| Textos (`Runtime/Lore`, Lore) | lore.ts entero (24/10), quests.ts (`Runtime/Quests.cs`) y transmissions.ts (`Runtime/Transmissions.cs`; lo que pide `NextSteps` espera en `wip/Transmissions.Campaign.cs`). Los arreglos de texto sobre la 0.10.1, en una tabla viejo→nuevo (`golden/text-fixes.ts`) que usan también las grabaciones. |
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

- **Sonido** (audio/: sonidos grabados, ambiente, música, radio). Lo hace Red, que hizo el de la 0.10.x (pasó de Arte el 26/10, porque Arte va con el Asentamiento). Cómo se mide sin oír, en la memoria «Medir sonidos».
  - **Generado y fuera de git** (Gameplay, 26/10). En WAV son unos 575 MB. `unity/tools/sonido.sh` los saca del código y de los Opus de la 0.10.1 a `Assets/Bastion/Client/Audio/Generado/`, que no se sube, como Synty y Mixamo.
  - **Más peso en la app:** unos 60 MB, porque Unity lo comprime a Vorbis.
  - **AppBuild se para** si falta. Para compilar sin sonido hay que pedirlo de forma explícita.
  - **Depende de packages/:** de `packages/client/src/audio` y `public/audio`. Al retirar packages/, eso se queda o se mueve a `unity/tools/sonido/`.
  - **Música y camas:** la música va en bucles de 8 min y las camas de ambiente en bucles de 4 min. Si se nota la repetición, se pasan a C# en vivo, como music.ts y ambience.ts.
- **El resto de biomas y mapas:** campo y fábrica con Alpine, Farm y Woodland; la base y el asedio con sus luces. UI empezó el 26/10 con lo suyo: el suelo, los caminos, las calles y una partida de cada tipo junto a la 0.10.1. El suelo, los caminos y las calles ya casan con la 0.10.1 (capturas en `.dev-snapshots/ui/unity/mapas/`). Falta:
    - **Hecho (26/10):**
      - UI: el `drawBlock` de mapView.ts (silos, camiones, contenedores, depósito de agua) en `MapBuilder.Blocks.cs`, y los techos de los surtidores con su desvanecido;
      - Arte: Woodland Apocalypse Map importado y las cercas con sus raíles (`fenceAt`/`rails`).
      - `MapCatalogMake.Check` no echa nada en falta en 25 mapas de campo, 14 de fábrica y 67 de ciudad. Alpine no hace falta para estos mapas.
    - **Falta:** las sombras cuando la cámara se aleja (acaban a 50 m y la cámara llega a 63 m). Lo hace Arte, con la medida de lo que cuesta.
- **La noche:** luces de farolas, coches y ventanas, y las bengalas.
- **Armas:** pistola, machete y cuchillo en la mano; Cuervos y muertos especiales. Recargar, que en Unity aún es una espera de 1,4 s (`UnitViews.Reload`): en la 0.10.1 es `rifleReload` («Reloading» de Mixamo). Es de Arte.
- **Rendimiento medido** en el PC de la 3090 («alto» ≤ 4,2 ms) y en el Mac («medio» ≥ 30 fps), con red.log.
- **Releases de la app de Unity** (versión que decide Arturo) y, entonces, retirar packages/ (Three.js, Electron).
- **El tiempo real** (realtime.ts): solo si Arturo lo quiere; no estaba en uso.

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
