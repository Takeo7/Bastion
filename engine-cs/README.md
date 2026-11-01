# engine-cs: el motor de Bastión en C#

Es el motor de `packages/engine` pasado a C#, regla por regla ([docs/UNITY.md](../docs/UNITY.md)). Se usa de dos maneras:
- Unity lo compila desde `package/` (paquete local `com.bastion.engine`);
- .NET lo compila desde `Bastion.Engine.csproj` para las pruebas.

```bash
dotnet test engine-cs/tests
```

| Proyecto | Qué |
|---|---|
| `Bastion.Engine.csproj` | el motor: `package/Runtime/**` menos `Net/` |
| `net/Bastion.Net.csproj` | la red: `package/Runtime/Net/**`, con Newtonsoft.Json (en Unity, `com.unity.nuget.newtonsoft-json`) |
| `tests/Bastion.Engine.Tests.csproj` | las pruebas de los dos |

Cada csproj va en su carpeta: dos en la misma comparten `obj/` y se pisan la restauración.

**Lo que está a medias va en `engine-cs/wip/`** (la red, en `wip/Net/`). Unity compila `package/` cada vez que se abre, y un solo error en el motor tumba el proyecto entero: nadie puede compilar, importar ni capturar. Los csproj de .NET compilan `wip/` junto con `package/Runtime/`. Un archivo pasa a `package/Runtime/` cuando `dotnet build` no da errores y `dotnet test` pasa.

**`wip/Relay/` no lo compila ninguno de esos csproj.** Es el servidor intermedio de «Unirse con código», aparcado el 31/10 con sus propios csproj y sus pruebas (`dotnet test engine-cs/wip/Relay/tests`). Qué es y cómo se retoma: [docs/UNIRSE-CON-CODIGO.md](../docs/UNIRSE-CON-CODIGO.md).

**En `wip/Net/`, un archivo sustituye al de `package/Runtime/Net/` que se llama igual**, solo para .NET (regla de `net/Bastion.Net.csproj`). Sirve para rehacer una clase que ya funciona, como la sala con la campaña, sin tocar la que usa Unity.
- **Cómo se nota:** si un nombre está en las dos carpetas, `dotnet build` y `dotnet test` usan la copia de `wip/Net/`, y Unity la de `package/Runtime/Net/`. Hoy no hay ninguno: la sala con la campaña pasó a `package/Runtime/Net/` el 24/10. Un arreglo en la de `package` no lo ven las pruebas, y uno en la de `wip` no lo ve Unity: mientras duren las dos, se arregla en las dos.
- **Al terminar:** se mueve la de `wip/Net/` encima de la de `package/Runtime/Net/` (sin dejarla en `wip`), se compila con `dotnet build`, se pasa `dotnet test` y se compila Unity.

## Normas

- **C# 9, netstandard2.1, sin UnityEngine** (asmdef con `noEngineReferences`). Nada de namespaces de archivo, `global using`, `record struct` ni otras cosas más nuevas.
- **Un módulo de TypeScript, un archivo** (o una carpeta de clases `partial` si es grande): `rng.ts` → `Rng.cs`, `lore.ts` → `Lore/*.cs`.
  - Las funciones sueltas de un módulo van en una clase estática con su nombre: `los.ts lineClear` → `Los.LineClear`.
  - Las interfaces de `types.ts` pasan a clases en `Types/`.
  - Si el nombre del módulo choca con un tipo, la clase lleva otro nombre: `skills.ts` → `Bars` (el tipo `Skills` ya existe). `realtime.ts` queda para después del hito 1.
- **Nombres:**
  - en PascalCase los tipos, los métodos, las propiedades y las constantes (`MAX_WINDOWS_SEEN_THROUGH` → `MaxWindowsSeenThrough`, `itemObjective` → `ItemObjective`);
  - las variables locales, como en TypeScript.
- **Los ids son `string`**, con los mismos valores que en TypeScript (`"fragGrenade"`, `"zombie"`, `"floor"`). Así los JSON del protocolo, las partidas guardadas y las grabadas no cambian.
  - Una tabla `Record<ItemId, X>` pasa a `IReadOnlyDictionary<string, X>`.
- **Números:** `int` si siempre son enteros (casillas, vida, acciones, turnos, cuentas) y `double` si no (alturas, ángulos, distancias, probabilidades).
- **Opcionales:** tanto `x?: T` como `x: T | null` pasan a `T?`.
  - El JSON se escribe sin los null.
  - Al leer, falta y null valen lo mismo.
  - Las partidas grabadas se comparan igual.
  - Así se leen también las partidas guardadas con la versión de TypeScript.
- **Listas:** `List<T>` en el estado, `IReadOnlyList<T>` en las tablas fijas.
- **Textos:**
  - `const string` cuando es literal; `static readonly` cuando se compone de otros valores;
  - plantillas de TypeScript → `$"…"`;
  - grupos de textos (`EVAC_TEXT`, `SIEGE_TEXT`) → clases estáticas anidadas: `Lore.EvacText.Retreat`.
- **Comentarios:** los de TypeScript, tal cual y en inglés; los de documentación, con `///`.
- **El JSON** se escribe en camelCase, así que `Unit.Hp` sale como `hp`, igual que en TypeScript. El motor no depende de ninguna biblioteca de JSON: eso es de la red (`Runtime/Net`) y de las pruebas.

## Código muerto de la 0.10.1

Se pasa tal cual, aunque nada lo alcance, para que el C# siga siendo el TypeScript línea a línea:
- **«X deja su hueco a Y»** (campaign.ts `joinSquad`, `Campaign.JoinSquad`): solo se escribe cuando `squadPlace` devuelve a quién echar (`evict`), y desde «cada uno su parte» (Arturo, 18/10 #43) `squadPlace` solo devuelve `{ error }`. Ninguna partida grabada ni prueba llega a esa línea.
- **`BUILDING_PIECES`** (campaign.ts): una tabla privada que nada lee; no se ha pasado.

## Pruebas

- **Las de TypeScript** se pasan a xUnit una a una, con el mismo nombre de prueba.
- **`golden/`** guarda resultados grabados con el motor de TypeScript hasta el 29/10, y desde el 31/10 con el de C# («Grabar con C#», más abajo). El motor tiene que repetirlos:
  - `rng.json`: los números del azar por semilla;
  - `step1/`: 9 misiones (ciudad, campo y fábrica, «la torre» con plantas, dos de noche) con sus líneas de visión, visión de equipo, alcances, caminos, escaleras y cobertura. Se graban con `npx tsx engine-cs/golden/record-step1.ts`, desde la raíz;
  - `step2/`: 17 partidas enteras (10 misiones de `createMission` y 7 escenarios hechos a mano) jugadas por un bot con las funciones del motor. Cada operación guarda su resultado, sus eventos, el estado del azar y lo que cambió del estado; `Step2GoldenTests.cs` las repite con `Rules` y compara todo. Se graban con `npx tsx engine-cs/golden/record-step2.ts`, desde la raíz.
  - `step3/`: 80 misiones de `createMission` (los tres biomas y cada tipo de misión; cada escenario: el pueblo, el polígono, el consultorio, la planta y el hospital, las afueras, el área de servicio y la fábrica; de día y de noche, dificultades 1 a 4, con veteranos o sin ellos, uno o dos asientos, las misiones de los vivos, los tamaños de La Lata, una escuadra de campaña, cada mapa hecho a mano y asedios de una base hecha con base.ts) y 24 mapas de `generateLayout`, `handmadeLayout` y `baseLayout`. `Step3GoldenTests.cs` los hace otra vez y compara el JSON entero; también rehace con `Setup.CreateMission` los estados de partida de `step1/` y `step2/`. Se graban con `npx tsx engine-cs/golden/record-step3.ts`, desde la raíz.
  - `campaign/`: la campaña (campaign.ts). Se graban con `npx tsx engine-cs/golden/record-campaign.ts`, desde la raíz (`saves/` y `muestra.json` se copiaron a mano); lo que dice C# pasa antes por `text-fixes.ts` (fixDeep):
    - `states.json`: 26 estados de campañas hechas con el motor de TypeScript; `tables.json` y `commands.json`, las tablas, constantes e ids, y una orden de cada clase; `saves/`, nueve campañas guardadas jugando (versiones 20 a 25); `muestra.json`, la partida de muestra de UI. `CampaignJsonTests.cs` las lee en `CampaignState` y las escribe igual;
    - `reads.json`: lo que solo lee (unas 70 funciones) sobre 29 estados; `CampaignReadsTests.cs` lo calcula en C# y lo compara entero;
    - `games/`: 14 campañas enteras jugadas por un bot, operación a operación (órdenes con su asiento y quién está conectado, misiones terminadas sin jugarlas, el segundo jugador, el tiempo, partidas migradas, trampas repuestas); dos llegan a la misión final y a la vacuna y ganan (`final-break`, `final-record`, con las misiones ganadas por ayuda); `CampaignGamesTests.cs` las repite y compara la campaña, el azar y el final (título y epílogo) tras cada una.
      - Una regla cambió después de grabarlas: «Cancelar misión» con el compañero conectado se vota (Arturo, 26/10; `CancelMissionAction`), y TypeScript la cancelaba al momento. Cuando C# hace la propuesta, la repetición contesta «sí» por el compañero y compara el paso como los demás, sin pasar nada por alto: son 14 cancelaciones en 7 partidas.
  - `net/`: lo de la red. `mision-ciudad.json`, una misión de ciudad para el JSON de la red (`NetJsonTests.cs`); `launch.json`, siete misiones de campaña lanzadas como en room.ts (launchCampaignMission), con su semilla y el azar después, que `NetEngineCampaignTests.cs` lanza igual con `EngineCampaignRules`. Se graba con `npx tsx engine-cs/golden/record-launch.ts`, desde la raíz.
- **Desde el 28/10 las reglas se apartan de la 0.10.1** (Arturo, tras la prueba de Ana, docs/PRUEBA-2026-10-28.md): los zombis más rápidos y el Chillón, la base con un solo camino y un claro más pequeño, los muertos del asedio por las veredas, la versión 26 de la campaña y los bolardos de las aceras. TypeScript sigue como estaba, y todas las grabaciones de `golden/` se hacen con el mismo cambio aplicado antes: `git apply engine-cs/golden/since-0101.patch`, grabar (`record-step1.ts`, `record-step2.ts`, `record-step3.ts`, `record-campaign.ts` y `record-launch.ts`) y `git checkout packages/engine`. Así C# sigue comparándose con otro motor. El 29/10 entraron también las notas antiguas de Ana (docs/PRUEBA-2026-10-29.md): cuatro armas cuerpo a cuerpo, la piedra del tablero, la horda en la Limpieza solo por ruido, Metsovo y la Autocaravana (estos dos, por `text-fixes.ts`). **Es la última tanda con TypeScript** (Arturo, 29/10): a partir de ahí, un cambio de reglas se escribe solo en C#, y las partidas de referencia se graban con el propio C# para comprobar que nada cambia sin querer. Los grabadores de C# están desde el 31/10: los `record-*.ts`, `make-lore-tests.ts`, `text-fixes.ts` y `since-0101.patch` quedan como historia.
- **Grabar con C#** (31/10): `BASTION_GRABAR=1 dotnet test engine-cs/tests --filter Recorder`, desde la raíz. Tarda un minuto. Sin la variable, los grabadores salen como omitidos.
  - **Qué hacen:** cada `tests/Record*.cs` rehace sus archivos de `golden/` con el motor de C# tal como está. Las entradas se quedan: las opciones y la semilla de cada misión, las operaciones de cada partida (con su asiento y quién estaba conectado), las campañas de partida. Las salidas son las de C#: resultados, eventos, azar, cambios del estado, mapas y lecturas. `step1/` saca sus muestras con el mismo azar que `record-step1.ts`, así que si cambia un mapa cambian también las preguntas.
  - **Qué cambia en el archivo:** solo lo que C# dice distinto. Lo que dice igual se queda con el texto del archivo, byte a byte; lo nuevo se escribe como `JSON.stringify` (los números como los imprime JavaScript, `RecordingNumbersTests`). Con las reglas sin tocar, `git status engine-cs/golden` queda limpio. Un cambio de estado grabado (`delta`) que ya no lleva al estado de C# se escribe entero otra vez, con el orden de claves de C# y sin los null.
  - **Una operación que una regla nueva rechaza** graba su nuevo error, y la partida sigue con las mismas órdenes.
  - **Cómo se usa:** se cambia la regla, se graba, se mira el `git diff` de `golden/` (que cambie solo lo que se quería), se pasa `dotnet test` y se sube todo junto.
  - **Lo que no se graba:** `campaign/states.json`, `saves/`, `muestra.json`, `commands.json` y `net/mision-ciudad.json` son puntos de partida, no lo que dice una regla. `LoreGoldenTests.cs` (los textos de lore.ts) es C# que escribió `make-lore-tests.ts`. `lore/scenes.json` sí se graba.
- **Reglas solo en C#.** La primera, el 31/10 (Ana, docs/PRUEBA-2026-10-31.md #1 y #5): `Buildings.cs`. Dentro de un edificio que la escuadra no ha encontrado, no ve a nadie, aunque a ella sí la vean. Lo encuentra al pisarlo, umbral incluido, o al pararse en la casilla de fuera justo delante de una de sus ventanas. Se guarda en `GameState.Discovered`, por casillas de la planta baja, y no en los.ts. Lo aplican `Los.CanSee` y `Los.TeamVisibility` de la escuadra; las de los enemigos no cambian. Al grabarla solo cambiaron la niebla de `step1/` y el `discovered` de cinco partidas de `step2/`: ninguna orden ni resultado. Las pruebas, en `BuildingsTests.cs`. El mismo día, con las notas de Arturo (docs/PRUEBA-2026-10-31-ARTURO.md): los enemigos no ven a través de ninguna ventana, ni de dentro afuera ni de fuera adentro (`Los.WindowReachOf`; la escuadra, como antes); cada lugar con su parte de los muertos de una ciudad, y las estrellas de la misión según sus muertos (`Places.cs`, `Setup.PodsFor`, `Campaign.OfferStars`; el pueblo, por la semilla del mapa); los soldados con 55 de puntería, en la campaña (`Bars.BaseAim`, antes 60) y en las plantillas (antes 65), y el zombi común sin su −10 de defensa. Las pruebas, en `PlacesTests.cs` y `BuildingsTests.cs`. El 1/11, el carácter de cada ciudad (Arturo): residencial, comercial o mixta por la semilla del mapa (`Places.TownKindSeed`), que pesa en el uso de cada manzana (`TownKinds.Weight`, `Mapgen/Lots.cs` CityProgram); la mixta, como antes. Y en el asedio, sin nadie a la vista, cada muerto va a por el soldado más cercano al que puede llegar (`Ai.cs` BesideByGround).
- **Coches siniestrados, fuego y tejados inclinados** (Arturo, 1/11; `Mapgen/Wrecks.cs`, `Burning.cs`, pruebas en `WrecksAndFiresTests.cs`). Lo que lee el cliente, todo en `GameState` y por índice de casilla (`y * width + x`, en el suelo):
  - **`wrecks`** (lista de `Wreck`, o nada): cada coche siniestrado es un coche más del `decor` (`CarFront` en `anchor`, su morro, con sus `Part`; cobertura baja), y esto dice cómo llegó ahí. `anchor`: su morro. `kind`: `pileup` (dos o tres chocados junto a un cruce), `abandoned` (en mitad de la calzada) o `wall` (empotrado en una fachada: el morro ocupa la casilla del muro, que pasa a ser `low` con el coche; detrás sigue el suelo de dentro). `tilt`: grados torcido respecto a su `facing`, de −35 a 35, ya con el espejo del mapa contado. Si una explosión quita el coche, el `decor` de `anchor` deja de ser `CarFront`: entonces no hay coche que dibujar.
  - **`fires`** (lista de `Fire`, o nada): lo que arde toda la misión. `id`: `f1`, `f2`…, que es también el id de su luz en `lights` (radio `Content.Flames.Light` = 3, altura 1; de noche se ve de lejos como con una farola). `source`: la casilla de lo que arde (el morro de un coche, unos cubos o bidones, o el propio montón). `tiles`: el suelo en llamas, casillas que se pisan; quien entra en una o acaba su turno en ella pierde 1 de vida aunque lleve blindaje (evento `burned` con `unit` y `pos`, seguido de `damaged`). Los enemigos la rodean si pueden (`Content.Flames.Avoid` en su camino). La escuadra también (Arturo, 1/11): `Paths.BuildPath` da el camino que no pisa fuego si cuesta las mismas acciones que el más corto, y si no, el más corto, que la cruza. El alcance no cambia, y la vista previa y la orden usan el mismo `BuildPath`, así que el camino dibujado es el que se anda. Si el camino aún pisa fuego, conviene avisarlo en pantalla.
  - **`pitched`** (lista de `PitchedRoof`, o nada en partidas viejas): los edificios con tejado inclinado que nadie pisa (`Mapgen/Upstairs.cs` `Pitched`: no planos, casa o granero, no en ruinas). `x`, `y`, `w`, `h`: su rectángulo con los muros, como `homes`. `kind`: `house` (a dos o cuatro aguas) o `barn` (a dos aguas, de granero).
  - **Dónde salen.** Coches: en el pueblo, casi siempre un choque junto a un cruce, uno o dos abandonados y a veces uno empotrado; menos en el polígono; en el campo, a veces uno en la carretera; ninguno en la fábrica. Nunca cerca de donde entra la escuadra, ni en la evacuación, el autobús y su salida, el objetivo o las rondas de los grupos, y nunca cortan un camino. Fuego: dos de cada cinco pueblos y polígonos, uno de cada cuatro en el campo y en la fábrica, de uno a tres fuegos separados. Cada cosa con su propia semilla, así que el resto del mapa no cambia.
- **El clima** (Arturo, 1/11: las cinco pruebas de Arte, en misiones distintas; pruebas en `WeatherTests.cs`). Lo que lee el cliente: **`GameState.weather`**, con `drizzle` (llovizna), `downpour` (aguacero), `storm` (tormenta), `snowfall` (nevada) o `blizzard` (ventisca) (`Weathers`); sin campo, despejado. En la base, antes de lanzar, la ficha lo saca de **`Campaign.OfferWeather(c, oferta)`**, la misma función con la que se lanza (`EngineCampaignRules`). Sale de la semilla del mapa de la operación (`Campaign.WeatherFor`, como `NightFor` pero con otros bits): dos de cada tres, despejado; en invierno (`Wintry`), nevada 22 de cada 100 y ventisca 12; si no, llovizna 14, aguacero 12 y tormenta 8. Despejados siempre: la historia, la misión final, el asedio y los mapas hechos a mano. En la Escaramuza, `SkirmishOptions.Weather`, el que se elija. Reglas (`Content.Weather`): con tormenta o ventisca, la vista de todos y los radios de detección se quedan en el 70 % (`Los.SightIn`; de noche, el menor de los dos); con aguacero, el ruido llega al 80 % de lejos, y con tormenta al 60 % (`Sim.MakeNoise`; el `noise` que se emite ya lleva el radio reducido). La llovizna y la nevada solo se ven.
- **Las matemáticas de JavaScript** van en `Js.cs` y dan el mismo resultado bit a bit:
  - `Js.Round`: las mitades hacia arriba;
  - `Js.Hypot`: la de V8;
  - `Js.Atan2`: fdlibm con multiplicación y suma fundidas, como node en Apple silicon. Por eso las grabaciones se hacen en el Mac;
  - ordenaciones estables;
  - `JsMap`, que guarda el orden de entrada.
  Nunca `Math.Round`, `List.Sort` ni `Math.Atan2` en el motor.
