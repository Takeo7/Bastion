# La interfaz (UI Toolkit) y las manos del combate

El HUD del combate de la 0.10.1 pasado a UI Toolkit, la pantalla de inicio, la sala, el paso entre escenas, y el controlador, la cámara y las marcas del combate. Va en `unity/Bastion/Assets/Bastion/UI/`. El plan y el inventario están en [docs/UNITY-UI.md](../../../../../docs/UNITY-UI.md).

## Qué hay

```
Resources/Hud/     Hud.uxml (un contenedor por pieza), las plantillas (SquadCard, UnitPanel, AbilityButton,
                   ShotPanel, TurnActions) y un USS por pieza; Mixta.uss, el tema
Resources/Start/   Start.uxml y Room.uxml, con sus USS
Resources/Combat/  Marks.shader, el sombreador de las marcas del mapa
Resources/HudIcons.asset   la tabla de iconos (id → sprite de Synty)
Scripts/           Bastion.UI.asmdef, Ui.cs (ayudantes)
Scripts/Hud/       HudRoot (el componente del UIDocument), HudViews (las vistas), HudText (los textos fijos,
                   que revisa Lore), IHudHandlers (lo que el HUD pide al juego), TooltipLayer (las ayudas)
  Pieces/          una clase por pieza: SquadList, UnitPanel, ActionBar, ShotPanel, TurnStrip, TurnActions,
                   GoalCard (con la rosa del asedio), Log, UnitFlags (marcas y textos flotantes), Banner,
                   Cards (llamada, llegada, resultado), Notes, EdgeMarks (flechas en el borde), GameMenu (el menú
                   de Esc y sus preguntas)
  Draw/            lo que USS no dibuja: el hexágono de habilidad, el dial de impacto, la aguja, el triángulo
  Lab/             HudLab y RoomLab: vistas inventadas para recorrer los estados
Scripts/Start/     StartScreen (nombre, crear, unirse, partidas en la red local), RoomMenu (la sala) y sus textos
Scripts/App/       AppFlow: de la pantalla de inicio a la sala y a la partida, el aviso de conexión
Scripts/Combat/    CombatController (en seis archivos: elegir, puntero y teclas; Orders, los modos y las órdenes;
                   Bar, la barra; Hud, el presentador; Plans, los planes y señalar; Menu, el menú del juego),
                   CombatSession (la red en Partida), EventPlayer (los eventos uno a uno, con las animaciones de
                   Arte) y WalkPlan (los paseos repartidos), CameraRig y ShotCam (la cámara y el plano al hombro),
                   MoveOverlay, AimOverlay y MissionMarks (las marcas), AbilityTexts (los textos de las
                   habilidades), MeshBuilder, Board, CombatLab
Editor/            HudShots (escenas para las capturas), HudFonts y HudIcons (generan los assets),
                   AppScenes (la escena Inicio), UiSheets (reimporta los USS)
Fonts/             Oswald y Barlow (OFL) y sus Font Assets estáticos
Settings/          HudPanel (PanelSettings: 1440×900, mitad ancho y mitad alto) y HudTheme
```

## Cómo funciona

- **Las vistas** (`HudViews.cs`) son clases `[Serializable]` con campos públicos. Las construye el presentador a partir del estado del motor. Cada pieza compara la vista nueva con la anterior por su JSON (`Ui.Key`) y solo vuelve a pintar si ha cambiado, como hacía hud.ts.
- **El HUD no depende de `Bastion.Engine`.** El presentador traduce de los tipos del motor a estas vistas.
- **Las órdenes** salen por `IHudHandlers`, que implementa el controlador del combate. `HudRoot.Bind` lo engancha.
- **Las plantillas** se cargan de `Resources/Hud` con `Resources.Load`. Los USS van todos en `Hud.uxml`, en la raíz, y llegan a las plantillas clonadas debajo.
- **Cada fotograma** (`HudRoot.LateUpdate`) las marcas siguen a las unidades (`HeadOf`), corren los relojes de turno (`ClockOf`) y la rosa gira con la cámara. Solo cambian `translate`, `width` y `rotate`; no se rehace la maquetación.
- **Las marcas del mapa** son mallas con el sombreador `Bastion/Marks`: colores en sRGB, líneas con su grosor en píxeles del HUD (igual de gruesas cerca y lejos) y, como en la web, las líneas y los escudos encima de los muros. Se rehacen solo al cambiar el elegido, el estado o la casilla del puntero. El aspecto es el que eligió Arturo el 18/10, «Hueso y óxido».

## Comprobar sin el editor

El C# se compila contra las DLL de la instalación de Unity con un csproj aparte, fuera del repositorio:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup><Compile Include="…/UI/**/*.cs;…/Client/Map/*.cs" /></ItemGroup>
  <!-- Referencias: todos los UnityEngine.*Module.dll y UnityEngine.dll de
       /Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine/,
       UnityEditor.dll (una carpeta más arriba), Newtonsoft.Json de Library/PackageCache, y de
       Library/ScriptAssemblies: Bastion.Engine, Bastion.Net, Bastion.Client.Net y Unity.InputSystem -->
</Project>
```

Con todos los módulos: si falta uno (GridModule, por ejemplo), un nombre como `Grid` deja de ser ambiguo fuera y lo es dentro de Unity. Los UXML, los USS y el sombreador solo los valida Unity al importarlos: se miran los errores en el registro.

## Pendiente

- El camino sobre los peldaños de una escalera.
- El «§» de la fuente, y los iconos de puertas (abrir y cerrar).
- En Ajustes: el sonido y los gráficos, cuando existan en Unity, y el tamaño de la interfaz.
- La mochila, la hoja del soldado y la ficha del enemigo.
