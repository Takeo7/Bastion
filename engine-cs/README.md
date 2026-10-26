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
- **`golden/`** guarda resultados grabados con el motor de TypeScript, que el de C# tiene que repetir:
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
- **Las matemáticas de JavaScript** van en `Js.cs` y dan el mismo resultado bit a bit:
  - `Js.Round`: las mitades hacia arriba;
  - `Js.Hypot`: la de V8;
  - `Js.Atan2`: fdlibm con multiplicación y suma fundidas, como node en Apple silicon. Por eso las grabaciones se hacen en el Mac;
  - ordenaciones estables;
  - `JsMap`, que guarda el orden de entrada.
  Nunca `Math.Round`, `List.Sort` ni `Math.Atan2` en el motor.
