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
- **Coma flotante:** JavaScript y .NET usan dobles IEEE, pero `Math.atan2`, `Math.hypot` y compañía pueden diferir en el último bit. Donde un evento lleve un ángulo o una distancia, la comparación admite 1e-9. Si una decisión del motor cambia por eso, se arregla en los dos motores igual.

## 4. Orden del trabajo

### Hito 1: una misión de combate

| Paso | Quién | Qué |
|---|---|---|
| 1 | Gameplay | `engine-cs`: types, content, rng, grid, decor, cover, flights, path, los. Pruebas y partidas grabadas de lo que toca. |
| 2 | Gameplay | aim, abilities, skills (barras), loot, events, rules, ai. |
| 2 | Lore | lore.ts a C# (`engine-cs/Runtime/Lore`), empezando por los textos del combate. |
| 3 | Gameplay | setup, maps y mapgen con todo lo suyo (townPlan, furnishing, stairAccess, homeDressing, medical*). Es lo más largo: 6.500 líneas solo mapgen. |
| 1–3 | Arte | Proyecto de Unity con URP. Importar los paquetes de Synty de la ciudad y Sidekick, y los clips de Mixamo con avatar humanoide. Una herramienta de capturas en batchmode desde el principio: sin ella, nadie puede revisar nada. |
| 2–3 | Arte | El mapa a partir del estado (casillas, decoración, edificios, plantas): lo que hoy hace mapView.ts. Los soldados y zombis Sidekick con sus animaciones. La niebla de guerra. |
| 2–3 | UI | El HUD del combate en UI Toolkit: lista de la escuadra, barra de acciones, panel de disparo, avisos de turno, tarjeta del objetivo, registro. Las mismas dos tipografías OFL. |
| 3 | Red | El anfitrión en C#: la app del anfitrión lleva la partida y el compañero se conecta. El mismo protocolo de órdenes y eventos que hoy (protocol.ts), pasado a C#. |
| 4 | Todos | La misión entera entre dos apps: elegir, mover, disparar, turno enemigo, ganar o perder. |

**El hito 1 está cerrado** cuando Arturo y Ana pueden jugar una misión generada de la ciudad entre dos Mac (o Mac y Windows), con tiras de movimiento revisadas, sin errores en el registro y con el rendimiento de las normas.

### Después

La campaña (campaign.ts, quests, transmissions, base, progression, board), el Asentamiento y el asedio, las pantallas de estrategia, el sonido y la app de Windows. El orden se decide con Arturo al cerrar el hito 1.

## 5. Para las sesiones de Claude

- **El motor** se prueba solo, con `dotnet test engine-cs/tests`.
- **Unity** se maneja desde la línea de órdenes en batchmode:
  - compilar: `-batchmode -quit -projectPath unity/Bastion -executeMethod …`;
  - pruebas del editor: `-runTests`;
  - capturas: la herramienta de Arte.
- **Las normas de siempre siguen:**
  - tiras a 10 fps de cada animación, revisadas por Gameplay;
  - leer los errores (aquí, el registro de Unity) antes de publicar;
  - commits y publicaciones solo con el «sí» de Arturo.
