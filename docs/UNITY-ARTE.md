# El arte en Unity: lo que hay que importar y lo que Unity da hecho

Del 23/10, para el hito 1 de [UNITY.md](UNITY.md): una misión generada de la ciudad. Sacado de lo que usa hoy la 0.10.1 (`syntyManifest.ts` y `public/models/synty/sources.json`) y de lo que hay en el disco externo (`/Volumes/4T_White/50_INBOX/Synty`, cada paquete en `<carpeta>/Unity/`). Escrito sin editor: lo que solo se sabe al abrir Unity está en el §5.

## 1. Paquetes de Synty

### Para el hito 1 (misión de ciudad)

Unos 2,8 GB comprimidos en total.

| Carpeta en el disco | .unitypackage | Tamaño | Qué se usa en el combate |
|---|---|---|---|
| POLYGON - Apocalypse Pack | `POLYGON_Apocalypse_Unity_2022_3_v1_19_4` | 240 MB | La mayor parte del mapa: unos 150 objetos (coches, barricadas, muebles, cajas, cuerpos), 15 piezas de edificio, las 15 armas y sus piezas. Trae dentro PolygonGeneric (su atlas y los shaders de URP). Su monja es un retrato de la radio: va después. |
| POLYGON - Town Pack | `POLYGON_Town_Unity_2022_3_v1_9_2` | 133 MB | 55 objetos (entre ellos las góndolas y estanterías del súper), 11 piezas de edificio, el asfalto (`Road_01`). |
| POLYGON - City Pack | `POLYGON_City_Unity_2022_3_v1_12_5` | 122 MB | 7 objetos y 4 piezas. |
| POLYGON - Office Pack | `POLYGON_Office_Unity_2022_3_v1_9_4` | 142 MB | La puerta y el escaparate de las tiendas, 5 objetos. |
| POLYGON - Military Pack | `POLYGON_Military_Unity_2022_3_v1_10_4` | 233 MB | 26 objetos: barreras, cajas y botín de armas y munición, alambre de espino, minas, cráteres, escombros y las granadas que se lanzan. |
| POLYGON - Construction Pack | `POLYGON_Construction_Unity_2022_3_v1_5_2` | 146 MB | 13 objetos de las obras. |
| POLYGON - Nature Pack | `POLYGON_Nature_Unity_2022_3_v1_2_1` | 162 MB | 10 objetos: árboles, pinos, arbustos, rocas, troncos y peñascos. El cielo: su skybox y, de PolygonGeneric, el anillo de nubes y el perfil de la ciudad. |
| POLYGON - Military Warehouse Map | `POLYGON_Military_Warehouse_Map_Unity_2022_3_v1_0_2` | 119 MB | La claraboya y la persiana metálica. Es el paquete que en el conversor se llamaba «SinIdentificar_NaveIndustrialYMilitar». |
| POLYGON - City Zombies Pack | `POLYGON_City_Zombies_Unity_2022_3_v1_7_2` | 114 MB | Los muertos que aún no son Sidekick: `xenoid` (Jock, Cheerleader, Hoodie, SchoolGirl) y `mec` (RiotCop, Military, Police, Firefighter). |
| POLYGON - Boss Zombies | `POLYGON_BossZombies_Unity_2022_3_v1_4_1` | 113 MB | Los jefes: `sectoid` (Wretch) y `bloater` (Blobber). |
| POLYGON - Particle FX Pack | `POLYGON_Particle_FX_Unity_2022_3_v1_4_1` | 113 MB | Humo, polvo, gas, sangre, fogonazos, fuego. |
| POLYGON - Alpine Mountain - Nature Biome | `POLYGON_NatureBiomes_AlpineMountain_Unity_2022_3_v1_5_5` | 300 MB | Solo la hierba y la tierra del suelo (`Grass_Texture_01`, `Dirt_Texture_01`) y el anillo de nubes fino. Es mucho paquete para tan poco: basta copiar esos tres archivos. |
| SIDEKICK - Starter | `SIDEKICK_Starter_Unity_2021_3_v1_0_4` | 43 MB | La base de Sidekick (esqueleto y cuerpo). |
| SIDEKICK - Apocalypse Survivors | `SIDEKICK_Apocalypse_Survivors_Unity_2021_3_v1_0_4` | 65 MB | La ropa de los soldados (la mayor parte del armario) y dos muertos. |
| SIDEKICK - Apocalypse Zombies | `SIDEKICK_Apocalypse_Zombies_Unity_2021_3_v1_0_3` | 83 MB | Los cinco zombis de Sidekick. Trae `Sidekick_ShaderGraph`, el shader con la sombra de los pliegues. |
| SIDEKICK - Apocalypse Outlaws | `SIDEKICK_Apocalypse_Outlaws_Unity_2021_3_v1_0_6` | 63 MB | Los vivos enemigos (`trooper`, `officer`, `lancer`) y algo del armario. |
| SIDEKICK - Modern Civilians | `SIDEKICK_Modern_Civilians_Unity_2021_3_v1_0_2` | 76 MB | Seis muertos civiles y ropa del armario. |
| SIDEKICK - Modern Police | `SIDEKICK_Modern_Police_Unity_2021_3_v1_0_0` | 52 MB | Cuatro policías muertos y ropa del armario. |
| ANIMATION - Base Locomotion | `ANIMATION_Base_Locomotion_Unity_2021_1_v1_1_3` | 99 MB | Quieto, agachado, andar, correr, esprintar y andar agachado, de hombre y de mujer. |
| ANIMATION - Idles | `ANIMATION_Idles_Unity_2021_3_v1_1_2` | 69 MB | Las esperas: mirar a los lados y atrás, cambiar el peso; las posturas (borracho para los muertos, agresiva para los vivos enemigos, encorvada para el herido, y las tres esperas con pistola: `restBase`, `handsOnHips`, `thoughtfulL`). |
| ANIMATION - Bow Combat | `ANIMATION_Bow_Combat_Unity_2022_3_v1_0_0` | 79 MB | Cinco muertes y siete golpes recibidos, de hombre y de mujer. |
| ANIMATION - Sword Combat | `ANIMATION_Sword_Combat_Unity_2021_1_v1_2_0` | 23 MB | Una muerte hacia delante y cuatro golpes fuertes. |
| ANIMATION - Goblin Locomotion | `ANIMATION_Goblin_Locomotion_Unity_2022_3_v1_0_1` | 71 MB | Cómo andan, corren y esprintan los muertos. |
| POLYGON - Farm Pack | `POLYGON_Farm_Unity_2022_3_v1_7_3` | 132 MB | Los postes de valla y de verja de los patios de la ciudad, el cobertizo y el bancal del huerto. Lo piden mapas de ciudad (city-3, city-4), así que entra ya (Gameplay, 24/10); en el campo y la base, además, las granjas. |

Para los iconos del HUD (UI), de tres paquetes INTERFACE **solo sus sprites y texturas**: ni prefabs ni scripts, porque el HUD es UI Toolkit y no su uGUI.

| Carpeta en el disco | .unitypackage | Qué se usa |
|---|---|---|
| INTERFACE - Apocalypse HUD | `INTERFACE_Apocalypse_HUD_Unity_2022_3_v1_1_1` | Iconos de armas, mochila, botiquín, munición, recargar, salud. |
| INTERFACE - Military Combat HUD | `INTERFACE_Military_Combat_HUD_Unity_2022_3_v1_1_1` | Los `mh-*` (rangos, caja de suministros, acciones, objetivo) y las bandas de peligro. |
| INTERFACE - Modern Menus | `INTERFACE_Modern_Menus_Unity_2022_3_v1_0_2` | Flechas, la marca de hecho y botones. |

Además, **la herramienta Sidekicks**, que no viene en los paquetes. Cada paquete de Sidekick trae solo `ToolDownloader.cs`, que baja dos archivos de GitHub: `Sidekicks.unitypackage` y `SidekicksDatabase.unitypackage`, de `github.com/SyntyStudios/SidekicksToolRelease/releases/latest`. Es una descarga: Arturo tiene que dar el «sí», o bajarla él.

Probablemente sobra **POLYGON - Shopping Plaza Map** (`POLYGON_Shopping_Plaza_Map_Unity_2022_3_v1_0_4`, 126 MB). Hoy solo se usa su atlas Generic para pintar la persiana metálica. En Unity, la persiana trae su material en el prefab del Military Warehouse Map; se comprueba al importar.

### Después del hito 1

| Paquete | Para qué |
|---|---|
| POLYGON - Woodland Apocalypse Map (`…_Woodland_Apocalypse_Map_Unity_2022_3_v1_0_1`, 140 MB) | El área de servicio del bosque (`roadside`). |
| POLYGON - Alpine Mountain, entero | Las montañas, rocas y pinos de la base. |
| POLYGON - Snow Kit (`…_Snow_Kit_Unity_2022_3_v1_6_1`, 108 MB) | La base en la nieve. La nieve de las misiones es un shader nuestro (`snow.ts`), no este paquete. |
| POLYGON - Icons Pack (`…_Icons_Unity_2022_3_v1_2_2`, 122 MB) | Los renders para la interfaz. |
| ANIMATION - Emotes and Taunts (`…_Emotes_And_Taunts_Unity_2022_3_v1_0_0`, 79 MB) | Los gestos del Asentamiento. |
| Office y City, sus personajes | Las voces de la radio (pantallas de estrategia). Los paquetes ya entran en el hito 1 por sus piezas. |

### Cómo importarlos

- En batchmode, con `-importPackage <ruta>` uno a uno, así se puede hacer sin abrir el editor.
- Sin escenas de demostración ni muestras. Apocalypse trae escenas de demostración, en «Standard» y en «Universal». Las muestras de Base Locomotion traen scripts que piden el paquete Input System. Se borran tras importar, o se dejan fuera.
- Todo va a `Assets/Synty/`, que no se sube (`.gitignore`). Los paquetes de Synty ya meten ahí sus recursos.

## 2. Clips de Mixamo

28 clips en el combate. Viven en el repositorio, en `assets-src/` (no en el disco externo): X Bot, sin piel, a 30 fps. Los cuatro de los muertos son de la cuarta tanda, en `assets-src/mixamo-30fps/`.

| Id del juego | Archivo | Para qué |
|---|---|---|
| `rifleIdle`, `rifleLowered`, `rifleIdleLook` | `mixamo/Rifle Idle.fbx`, `Rifle Idle 4.fbx`, `Rifle Idle 3.fbx` | Esperar con el fusil, uno distinto por soldado. |
| `rifleAim`, `rifleFire`, `rifleReload` | `Rifle Aiming Idle.fbx`, `Firing Rifle.fbx`, `Reloading.fbx` | Apuntar, disparar y recargar. |
| `rifleKneel`, `rifleKneelAim` | `Rifle Kneel Idle.fbx`, `Idle Crouching Aiming.fbx` | Agachado con el fusil, y apuntando. |
| `rifleWalk`, `rifleRun`, `rifleSprint` | `Rifle Walk.fbx`, `Rifle Run.fbx`, `Sprint Forward.fbx` | Moverse con el fusil. |
| `rifleTurnL45`, `rifleTurnR45`, `rifleTurnL90`, `rifleTurnR90`, `rifleTurnL180`, `rifleTurnR180` | `Rifle Turn.fbx`, `Rifle Turn 2.fbx`, `Rifle Turn 6.fbx`, `Rifle Turn 8.fbx`, `Rifle Turn 5.fbx`, `Rifle Turn 3.fbx` | Girarse en el sitio con el fusil. |
| `pistolRest`, `pistolAim`, `pistolFire` | `Pistol Aim.fbx`, `Pistol Idle 2.fbx`, `Shooting.fbx` | La pistola: en reposo, apuntando, disparando. |
| `tossGrenade`, `throwStone` | `Toss Grenade.fbx`, `Throw Object.fbx` | Lanzar. Van en espejo (se lanza con la otra mano). |
| `knifeStab`, `macheteSwing` | `Stabbing 3.fbx`, `Standing Melee Attack Horizontal.fbx` | Cuerpo a cuerpo. |
| `zombieNeckBite`, `zombieHeadbutt`, `zombieFeed`, `zombieFeedFromStanding` | `mixamo-30fps/Zombie Neck Bite.fbx`, `Zombie Headbutt.fbx`, `Zombie Biting.fbx`, `Zombie Biting 2.fbx` | El muerto que agarra, muerde y se come al caído. |

Con ellos va `mixamo/T-Pose.fbx`, la postura de referencia. En Unity sirve para crear el avatar humanoide de X Bot una vez, y los demás clips lo copian (*Copy From Other Avatar*).

Los otros 17 clips de Mixamo que se convierten hoy son del Asentamiento (sentarse, arrodillarse ante el fuego, subir al coche y al 4×4): van después.

## 3. Lo que hoy hacemos a mano y Unity da hecho

| Hoy, en Three.js | En Unity | Lo que sigue siendo nuestro |
|---|---|---|
| **La conversión con Blender** (`convert.ts` y `convert.py`): de FBX a GLB y de atlas a webp, siempre por ids. | Se usan los prefabs de Synty tal como vienen. | Nada. |
| **Las trampas de los atlas**: una segunda textura, los `SK_`, el atlas de otro paquete, el vidrio, leer `MaterialList`, el arreglo `plain` de las casillas raras. | Cada prefab trae sus materiales, y el vidrio el suyo. | Nada. |
| **Los ocho atlas del mapa en una textura** (`mapAtlas`), para bajar de 83 a 63 llamadas de dibujo. | El SRP Batcher y el GPU instancing de URP, y el *static batching*. | Medir. |
| **El retargeting de Mixamo a Synty a mano**: postura de referencia, huesos uno a uno, espejo de los lanzamientos. | El avatar humanoide: cualquier clip humanoide sirve para cualquier esqueleto humanoide. El espejo es una casilla (*Mirror*). Los clips de Synty ya vienen como humanoides (`animationType: 3`, con su avatar). | Nada. |
| **Bucles y cortes de clips**: quitar el avance de los que no son *In Place* (sentarse, 0,32), partir «Kneel», pasar a 30 fps. | El importador: *Loop Time*, *Root Transform → Bake Into Pose*, cortes por fotograma. | Decidir los cortes, que ya están medidos. |
| **Capas de animación** (`AnimDriver`: el cuerpo entero, la mitad de arriba, solo el brazo del arma). | Capas del Animator con *Avatar Mask*. | Montarlas. |
| **Los agarres**: la mano de apoyo en el punto `fore` medido por arma, el agarre rígido, los tiempos en que se suelta lo lanzado. | *Animation Rigging*: *Two Bone IK* para la mano de apoyo y *Multi-Aim* para apuntar. *Animation Events* para el momento de soltar. | Los puntos de agarre de cada arma (un hijo vacío en su prefab) y los tiempos. No vienen hechos: Unity da las herramientas. |
| **La sombra de los pliegues**, horneada en Blender (`occlusion()` en `convert.py`, el color «ao» en los vértices). | La máscara de oscuro del shader de Sidekick (`_DarkAmount` 0,75), que viene con Sidekick para Unity. | Nada. |
| **Montar soldados con piezas** (`wardrobe.ts`): unir mallas, componer la paleta por casillas, las formas del cuerpo. | La herramienta Sidekicks: piezas, presets, paletas y formas del cuerpo. | Elegir las piezas según el aspecto de cada soldado (`Appearance`). Está por ver si la herramienta monta personajes mientras se juega o solo en el editor (§5). |
| **La luz y el acabado** (`UNIT_FACE`, `UNIT_SKY`; `post.ts`: oclusión de ambiente, resplandor, saturación). | Luces de URP, sombras en cascada, sondas de luz y un *Volume* de posproceso (SSAO, Bloom, Color Adjustments). Synty trae sus volúmenes de ejemplo. | Ajustarlo a lo que eligió Arturo: luz principal 2,4 con medio cielo en el Asentamiento, saturación en el nivel 1. |
| **Los efectos** (`fx.ts` imita a mano el aspecto de Particle FX). | Los prefabs de Particle FX, con sus sistemas de partículas. | Cuándo y dónde sale cada uno. |
| **El cielo**: tiras del skybox recortadas a mano y anillos de nubes convertidos. | El material de skybox de Synty y los anillos como prefabs. | Nada. |
| **El bosque lejano** (a partir de cierta distancia, el pino más simple, y un tercio menos de pinos). | *LOD Group* y GPU instancing. | Medir. Los prefabs de POLYGON no suelen traer LOD. |
| **Las tiras y capturas** (Electron fuera de pantalla, CDP, ffmpeg). | Una cámara que pinta en una *RenderTexture* en batchmode, y Unity Recorder para vídeo. | La herramienta de capturas. Es lo primero que hago. |
| **La telemetría** (`red.log`, las líneas `[rend`). | El Profiler y `FrameTimingManager` (CPU y GPU por fotograma). | Escribir la misma línea en `data/red.log`. |

## 4. Lo que Unity no da

Esto se rehace en C# y en shaders de URP:

- **El mapa a partir del estado** (lo de `mapView.ts`): casillas, decoración, edificios, plantas, y qué prefab va en cada sitio.
- **El corte de los edificios** y las plantas (`buildings.ts`).
- **La niebla de guerra** (`fogOfWar.ts`): la textura por casilla y su copia suavizada, las fachadas, los interiores por habitaciones, la bruma. En URP, un Renderer Feature o una función de Shader Graph común a los materiales del mapa.
- **Los tintes de las unidades**: el destello, la marca del blanco, la sombra en la niebla, el fundido.
- **Las copas que se traman** sobre lo que se mira (`treeFade.ts`), como Arturo eligió dejarlo.
- **La multitud de los muertos** (`crowd.ts`). De entrada, sin ella: un SkinnedMeshRenderer por figura, con *culling*. Si el asedio no llega al rendimiento, se hornea la animación para la GPU.
- **El ritmo**: cada acción se ve entera antes de la siguiente y la cámara no pelea con el jugador.

## 5. Por comprobar al abrir el editor

- **Paquetes de Unity 2021.3 y 2022.3 en Unity 6.** Traen sus shaders de URP (`PolygonShader.shadergraph` en PolygonGeneric) y escenas para «Standard» y «Universal». Hay que ver que los materiales salen bien en URP sin convertirlos.
- **`SyntyPackageHelper`**, un script del editor que viene en cada paquete: qué hace al importar en batchmode (si pregunta o instala algo).
- **La herramienta Sidekicks**: la descarga de GitHub con el «sí» de Arturo; si monta personajes mientras se juega; qué pide para su base de datos.
- **Alpine Mountain**: copiar solo las dos texturas del suelo y el anillo de nubes, en vez de importar los 300 MB.
- **Shopping Plaza**: confirmar que no hace falta.

## 6. El proyecto y las capturas

`unity/Bastion`, con Unity 6.3 LTS (6000.3.27f1) y URP 17.3. Creado desde la plantilla «3D URP» del editor, sin el tutorial, el control de versiones de Unity, el asistente de multijugador ni la navegación. En `Packages/manifest.json`:
- el motor (`com.bastion.engine`, `file:../../../engine-cs/package`) y `com.unity.nuget.newtonsoft-json`;
- `com.unity.shadergraph` a la vista: lo trae URP, pero el `SyntyPackageHelper` de cada paquete lo busca escrito y, si no lo ve, saca un diálogo para instalarlo;
- `com.unity.visualscripting`: la herramienta Sidekicks usa su YamlDotNet.

| Ensamblado | Dónde | Qué |
|---|---|---|
| `Bastion.Client` | `Assets/Bastion/Client` | El cliente del juego: el mapa, las unidades, la niebla. Ve al motor y a URP. |
| `Bastion.Editor` | `Assets/Bastion/Editor` | Solo en el editor: las capturas y las herramientas de importar. |

La configuración del proyecto que es de Arte la pone `Bastion.EditorTools.ProjectSetup.Run` (se puede volver a lanzar):
- empresa «Bastion-Unity», producto «Bastion» (sin tilde: da nombre al ejecutable del Mac, y con tilde la firma ad hoc no vale; el nombre con tilde que ve el jugador lo pone `AppBuild` en el Info.plist) e identificador `com.takeo7.bastion.unity` (el de Electron es `com.takeo7.bastion`). Los datos de la app van a `~/Library/Application Support/Bastion-Unity/Bastion` en el Mac y a `AppData/LocalLow/Bastion-Unity/Bastion` en Windows, no a la carpeta de la app de Electron (`…/Application Support/Bastion`);
- `enableFrameTimingStats`, para que `FrameTimingManager` dé los tiempos de la CPU y de la tarjeta;
- tres niveles de calidad, que los Ajustes de UI buscan por su nombre. El editor y las capturas usan «Alto».

| Nivel | Recurso de URP | Renderizador | Sombras | SSAO | Posproceso | Escala |
|---|---|---|---|---|---|---|
| Bajo | `Mobile_RPAsset` | `Bajo_Renderer` | 1 cascada, 1024, duras | no | no | 0,8 |
| Medio | `Medio_RPAsset` | `Mobile_Renderer` | 2 cascadas, 1024, duras | no | sí | 1 |
| Alto | `PC_RPAsset` | `PC_Renderer` | 4 cascadas, 2048, suaves | sí | sí | 1 |

La regla: «alto» en ≤4,2 ms por fotograma en la 3090, «medio» a ≥30 fps en el Mac. Los tres renderizadores llevan el pase de la niebla (`FogFeatureMake`).

Unity se maneja desde la línea de órdenes, con dos guiones de `unity/tools/`. Los dos esperan a que termine otro Unity lanzado con ellos, y Unity no abre el proyecto si el editor ya lo tiene abierto. El registro va a `unity/Bastion/Logs/batch.log`, y al final se imprimen sus errores.

```bash
unity/tools/unity.sh
```

Abre el proyecto, importa, compila y sale: la comprobación de que todo compila.

```bash
unity/tools/captura.sh -shotOut captura.png -shotSetup Bastion.EditorTools.ShotSetups.Primitives
```

Una captura (`Bastion.EditorTools.Shots`):
- `-shotOut`: el PNG, o una carpeta si es una tira;
- `-shotScene`: la escena que se abre; sin ella, una vacía con un sol;
- `-shotSetup`: un método estático sin argumentos que monta lo que se captura;
- `-shotSize 1600x900`, `-shotCamera <nombre>`, `-shotFrom x,y,z`, `-shotAt x,y,z`, `-shotFov 40`;
- `-shotFrames 20 -shotFps 10`: una tira (`f000.png`, `f001.png`…), y `-shotWarm 1`, los segundos que se avanzan antes del primer fotograma.
- `-shotPlay`: en modo juego, para lo que solo se pinta con el juego en marcha. Los paneles del HUD (UI Toolkit) se pintan en una textura aparte, sobre una copia de su PanelSettings, y se componen encima de la imagen de la cámara. El tiempo avanza `1/-shotFps` por fotograma (`Time.captureDeltaTime`, que se vuelve a fijar ya dentro del modo juego), con una foto por fotograma del juego; el registro dice el tiempo de juego de la primera foto y de la última. Por ejemplo, el HUD apuntando: `-shotPlay -shotSetup Bastion.UI.EditorTools.HudShots.Aim -shotSize 1440x900 -shotWarm 1`.

En una tira la escena se avanza a mano, el mismo tiempo cada fotograma, así que sale igual cada vez. La herramienta avanza los Animator y los sistemas de partículas. Lo demás que se mueva implementa `Bastion.Client.IShotStep`. Cada pasada tarda unos 10 s.

### En una máquina nueva

`Assets/Synty` y `Assets/Mixamo` no se suben (licencias). Después de clonar, y con el disco externo montado:

```bash
unity/tools/importar-synty.sh
```

Importa los paquetes de Synty del §1 y la herramienta Sidekicks (`assets-src/sidekicks-tool/1.2.8/`), cada uno con su registro en `Logs/import-<paquete>.log`. Copia sueltas las dos texturas del suelo de Alpine (`unity/tools/upkg-extract.py`) y quita las escenas de demostración y las muestras. Tarda de 20 a 30 minutos. Con nombres de paquete (sin `.unitypackage`), importa solo esos.

```bash
unity/tools/unity.sh -executeMethod Bastion.EditorTools.MixamoImport.Run
```

Copia los 28 clips de Mixamo y la T-Pose de `assets-src/mixamo` y `assets-src/mixamo-30fps` a `Assets/Mixamo`, cada uno con el id del juego (`rifleIdle.fbx`…). Los configura humanoides sobre el avatar de la T-Pose, en el sitio, y en espejo los dos lanzamientos.

`assets-src/` tampoco se sube: en otra máquina hay que copiarlo antes.

```bash
unity/tools/unity.sh -executeMethod Bastion.EditorTools.FxArtMake.Run -quit
```

```bash
unity/tools/unity.sh -executeMethod Bastion.EditorTools.SourceProps.Run -quit
```

```bash
unity/tools/unity.sh -executeMethod Bastion.EditorTools.MapCatalogMake.Run -quit
```

`SourceProps.Run`, antes que el catálogo, importa a `Assets/Synty/Extra` los modelos que solo están en los archivos fuente de su paquete y no en el de Unity (la basura suelta, `SM_Prop_Trash_02`), con el material del paquete.

Rehacen los dos recursos del juego que apuntan a Synty: `Resources/FxArt.asset` (las formas y sprites de Particle FX para los efectos) y `Resources/MapCatalog.asset` (los modelos y materiales del mapa por nombre, porque en la app no hay AssetDatabase). Se rehacen tras importar otra vez los paquetes, y MapCatalog también cuando cambian las piezas de `MapArt.json`. Hoy el catálogo tiene 307 modelos. Los mapas de ciudad no piden nada que falte: lo comprueba `MapCatalogMake.Check`, que construye con el catálogo cada estado de golden de un bioma (`BASTION_BIOME`, ciudad si no se dice). Faltan los de Alpine y Woodland, que van después del hito 1 (Gameplay, 24/10), y unas pocas piezas que el Apocalypse de Unity no trae (la camilla, la ambulancia). Donde Unity arma una pieza de otra forma que los FBX de origen de la 0.10.1, `MapLab.UnityNames` da el nombre de Unity: la maceta con planta es el prefab `SM_Prop_Pot_Street_01`.

```bash
unity/tools/unity.sh -executeMethod Bastion.EditorTools.GameScenes.Make
```

Pone en la lista de la compilación Inicio y Partida, y en Partida lo que cuelga de ella si falta: «Mapa» (MapView y Battlefield), «Unidades» (UnitViews) y «Efectos» (Fx).

### La escena Partida y el reproductor de UI

- `Battlefield.Current.Show(estado)`: lo llama UI con cada estado entero (al empezar, al volver, tras un resync). Construye el mapa desde el catálogo, solo si cambió su firma (MapId, tamaño, casillas, decorado, plantas y elevaciones); si es el mismo mapa con casillas distintas, rehace solo esas.
- `MapView.Current.Rebuild(visto)`: lo llama UI al asentarse un tileChanged o un floorBroken (una puerta que se abre es un tileChanged). Rehace las casillas cambiadas y lo que estaba encima de ellas (un coche entero), y la losa de su planta. MapBuilder apunta qué objeto hay en cada casilla para eso.

Los muros de los edificios son las piezas de Synty, como en la 0.10.1:
- `BuildingPlan` es el de `buildings.ts`, igual que el TS en los 106 estados de `engine-cs/golden`. Se hace una vez por mapa y sigue al estado (`Follow`).
- `WallLayout.Of(estado, plan)` es `buildWalls`. Da cada pieza, cada cristal y cada cosa colgada del muro (la persiana de una nave, el cartel de una tienda, el del motel) con su matriz en el mundo de la 0.10.1, en casillas. Todavía no lleva las fachadas del terreno alto (`layRaised`), los pretiles, lo que hay en los tejados ni las tapas de los muros.
- `WallsView` junta las piezas de cada trozo de 8×8 casillas y cada planta en una malla por material. Las hojas de las puertas van aparte, abiertas un cuarto de vuelta hacia dentro o cerradas. Al rehacer el mapa se calcula entero (unos ms) y solo se rehacen los trozos que cambian.
- Las piezas salen del FBX original de Synty, cortadas con Blender (`unity/tools/pieces-cut.py`), y su sitio en la casilla viene de `Resources/Pieces.json` (`pieces-json.mts`). `PieceMake.Run` hace `PieceCatalog.asset`. Por cada material de Synty que usan las piezas crea uno de `Bastion/Building` con su textura, en `Client/Map/Materials`. También crea el cristal (`Shaders/Glass.mat`) y los dos materiales de parches, «Suelos» y «Tejados».
- `BuildingSurfaces` (el `buildStatic` y el `redoUpper` de `buildings.ts`) da los suelos, tejados, escaleras y la tapa del terreno alto; `roofProps` da lo que va en los tejados. `BuildingLooks` da los colores de cada edificio (`looksOf`). Los dos salen idénticos al bit con el TS en los 108 estados de golden. MapBuilder los dibuja: cada planta en su `Storeys[k]`, los tejados en `Roofs` y la azotea pisable en su planta. También pone los pretiles.
- El shader `Shaders/Building.shader`, con URP iluminado y sus cuatro pasadas, pinta dos cosas. Las piezas usan la textura de su paquete, y en la franja de arriba se tiñe el yeso con el color de dentro y las tablas con el de fuera, como en `wallCutPatch`; la cara de atrás sale oscura. Los suelos y tejados (`_SWATCH`) son un parche de la textura de Town repetido sobre el plano del mundo, como en `swatchPatch`. Los colores van por vértice en TEXCOORD2 y 3, porque son lineales y pueden pasar de 1.
- El suelo de la calle es de UI (`GroundView`, a partir de `ground.ts`).
- La luz (`Client/Light/Mood.cs`, los MOODS de `renderer.ts`): el sol con sombras, el relleno que sigue a la cámara, la luz de cielo como ambiente de tres colores y la bruma exponencial al cuadrado, de día o de noche (`Mood.For(estado)`). Las intensidades de three van divididas por π (su BRDF es albedo/π y la de URP no). Después del posproceso de URP, `Shaders/Grade.shader` hace la exposición, el tonemapping Khronos Neutral de three y la gradación de `post.ts` en valores sRGB, con el nivel «vivo» 1. `GradeMake.Run` pone ese pase en los tres renderizadores, deja la gradación de URP en HDR y, en el perfil de volumen que usan los tres recursos de URP (`SampleSceneProfile`), quita el tonemapping y la viñeta de URP y pone el brillo de la 0.10.1.
- `HomeFurnishing` es `homeFurnishing.ts` (alfombras, lámparas, sillas alrededor de las mesas, toallas…): igual que el TS en los 107 estados de golden. MapBuilder lo pone al construir y lo rehace con cada cambio.
- `MapArt.UnityTurn`: los modelos que el paquete de Unity trae girados respecto a su FBX de origen, con el que se ajustó la 0.10.1 (los cuatro coches destrozados de Apocalypse, un cuarto de vuelta). Lo usan `MapBuilder.Place` y el `PropParts` de UI.
- El corte (`Cutaway`, el de `buildings.ts`) va en «Mapa». Cada fotograma decide qué edificios se abren, a partir de la cámara, el foco de UI (`Cutaway.Focus`: el elegido, el blanco, el cursor, la planta, los tejados y el plano al hombro) y lo que ve la escuadra. Deja un texel por edificio en `_CutMap`. `Bastion/Building` lo lee en `BuildingCut.hlsl`, también en las pasadas de profundidad para que la niebla y la SSAO lo vean. Los muebles de las plantas, lo que hay en las azoteas y los carteles se apuntan con `Track` y se apagan con su planta; las figuras se apagan con `Hides`. `MapView.ShowLevel` ya no apaga plantas: pasa la planta al corte.
- `UnitViews.Current`: las figuras. `Sync(visto)` las crea, quita y coloca. Lo que se espera (MoveAlong, RaiseWeapon, HitReact, Die…) devuelve `Awaitable` con CancellationToken, y los momentos dentro de una animación llegan como `Action onHit`: el golpe, la granada que sale de la mano, cada mordisco. Los nombres son los de unitViews.ts de la 0.10.1.
- `Fx.Current`: los efectos, con los tamaños, velocidades y tiempos de fx.ts (en casillas). Bullet, GrenadeArc, Stone, PsiBeam, Explosion y Missiles se esperan; los demás, no.
- Los ritmos de paso son los de gaits.ts (vivos 0,375 s por casilla en un movimiento y 0,24 en una carrera; zombi 0,82/0,62…; la diagonal, √2), con un solo reloj para todo el paseo.
- Las figuras son de Sidekick. Los soldados se montan con su `Dress` (el dressOf de wardrobe.ts, con el mismo pick), con la API de Sidekick y su base de datos, que también llega a la app (`Resources/Database/Side_Kick_Data.bytes`); se guarda uno por traje. Los muertos son por ahora los cinco zombis de Sidekick, y los Cuervos los forajidos. Lo demás (los vivos hechos muertos, City Zombies, los jefes) sigue en cápsula.
- Los clips son humanoides: Mecanim los pasa a Sidekick sin el proxy de huesos de la 0.10.1. `FigureAnimator` (PlayableGraph a mano) mezcla los seis del MOTION_ORDER, uno sobre la parte de arriba (un golpe, una acción; con AvatarMask) y uno sobre el cuerpo entero (una muerte, que se queda en su último fotograma). La velocidad de cada clip, la de gaits.ts (`Gaits.ClipSpeed`); las muertes acaban cuando el cuerpo se queda quieto (`FigureCatalog.Settle`, medido como el settleOf de la 0.10.1: 1,0–1,6 s).
- El arma va en la palma derecha y apunta a la izquierda mientras las dos manos están en ella; esa postura se aprende en el marco de la mano derecha y se mantiene rígida cuando la izquierda suelta (el clipGrip de la 0.10.1). En reposo, uno de RIFLE_RESTS por soldado; al moverse, los clips de Mixamo con armedPace; apuntando, `rifleAim` con el tronco enderezado 18° (los brazos compensan para que el fusil siga a nivel); el disparo, `rifleFire` encima. Agachado, de rodillas (`rifleKneel`, `rifleKneelAim`). Los lanzamientos van sobre la parte de arriba menos el brazo del arma (la máscara «free»).
- Las esperas: borrachos los muertos, agresivos los Cuervos, encorvado el soldado bajo la mitad de su vida. Comer, como unitViews.ts feed y unfeed: muerde en el Lunge, se arrodilla junto al pecho del caído, come en bucle y vuelve a su sitio al levantarse.
- La niebla de guerra (`Fog`, en «Niebla»): una textura de casillas (en vista, visto, el suelo de cada una) que lee una pasada a pantalla completa (`Bastion/Fog`, un FullScreenPassRendererFeature en los dos renderers, `FogFeatureMake.Run`) en la posición de cada píxel sacada de la profundidad. Así cubre también los modelos de Synty, cuyos shaders no se tocan. UI la llama con el estado visto (`Fog.Current.Show`) cada vez que hace Sync; los enemigos fuera de vista se ocultan en `UnitViews.Sync`. Como en la 0.10.1, un muro se ve como la casilla a la que da (la normal de cada píxel, de la textura de normales de URP), un tejado como la mejor calle a lo largo de su edificio (una segunda textura, la de los edificios), y una habitación vista en parte se ve entera con sus muros. Todo en la planta a la vista (`MapView.ShowLevel`, Los.FloorLevels); lo explorado se guarda por nodo.
- Qué clip hace cada papel sale de `Resources/Anims.json` (`unity/tools/anims-json.mts`, de syntyManifest.ts). Los clips, los prefabs de los presets y sus mapas de color, de `Resources/FigureCatalog.asset` (`FigureCatalogMake.Run`).
- Tira de ejemplo: `CombatLab.Shoot` (andar, dos disparos, morir, granada) con `-shotPlay -shotFrames 90 -shotFps 10`; los efectos de cerca, `CombatLab.Effects`; Partida sin UI, `CombatLab.Partida`.

## 7. Coordenadas

Unity es zurdo. Para que el mapa no salga en espejo respecto a la 0.10.1:
- este = +x, norte = +z, arriba = +y;
- la y del mapa, que crece hacia el sur, pasa a −z;
- 1 unidad = 1 metro, la escala propia de Synty;
- una casilla mide 1,5 m, y el centro de la casilla (x, y) está en (1,5·x + 0,75, altura, −(1,5·y + 0,75));
- como en la 0.10.1, las piezas de edificio van a 1,2 veces su tamaño (`KIT_SCALE`) y los personajes a 1,125 (`CHARACTER_SCALE`).

La cámara del juego: 52° hacia abajo, 40° de campo de visión, 24 casillas (36 m) del punto que mira.

## 8. Trampas encontradas

- **Un script que no compila aborta Unity en batchmode**, antes de importar o capturar nada: «Scripts have compiler errors». Lo que está a medias va fuera del proyecto (`engine-cs/wip/`, una copia en el scratchpad), y se compila por fuera antes de copiarlo. Por fuera se compila con dotnet, referenciando las DLL de `Unity.app/Contents/Resources/Scripting/Managed` (`UnityEngine.dll`, `UnityEditor.dll` y las de `UnityEngine/`) y las de `unity/Bastion/Library/ScriptAssemblies`.
- **Los paquetes de Synty se sacan sin Unity** (`upkg-extract.py`): con `-importPackage`, un script roto del proyecto deja el paquete fuera.
- **SRP Batcher y Sidekick:** con el SRP Batcher encendido, dos personajes Sidekick dibujados seguidos salían los dos con el ColorMap de uno, aunque cada material tenía el suyo (Unity 6.3, URP 17.3). Se arregla poniendo a cada personaje su `_ColorMap` en un MaterialPropertyBlock (`SidekickLab.OwnColours`), hasta que el shader de unidades sea nuestro.
- **El shader de Sidekick lee `_ColorMap`**, la referencia de su propiedad «ColorTexture».
- **`Bastion.Engine` ya tiene `Grid` y `Light`:** en el cliente, `MapSpace` y `UnityEngine.Light` escrito entero.
- **Un `sleep` en segundo plano retiene la salida de un guion**: quien lo lea por tubería espera hasta que acabe. `unity.sh` lo mata al terminar.
- **Al entrar en modo juego, Unity olvida el paso fijo** (`captureFramerate` y `captureDeltaTime`) que se puso antes. Además, en batchmode `EditorApplication.update` llega más veces que los fotogramas del juego. Sin fijarlo dentro del modo juego y sin contar por `Time.frameCount`, una tira de 90 fotos a 10 fps cubría 2 s de juego en vez de 9.
- **Un archivo nuevo en Assets lo compila el Unity de cualquiera**, también el de otra sesión que capture en ese momento. Se escribe y compila en el scratchpad y se copia ya compilado.
- **`Stage` choca con `UnityEditor.SceneManagement.Stage`** en el código de editor: por eso el arranque de Partida se llama Battlefield.
- **Los argumentos se evalúan antes que el cuerpo**: un efecto que pedía su textura como argumento la pedía antes de cargar el arte, y la primera vez salía un cuadrado. Lo que elige arte (`Shape`, `TextureOf`) carga primero.
- **Las texturas de Particle FX llevan la forma en el alfa**: el color es blanco casi entero.
- **Al entrar en modo juego, Unity invalida los PlayableGraph creados antes**: las figuras de una tira en modo juego se hacen ya en él (`CombatLab` llama a `Sync` al empezar).
- **Un color global de shader no se pasa a lineal** (`Shader.SetGlobalColor`): la niebla salía azul clara en vez de oscura. Se le pasa `color.linear`.
- **Las capturas en modo juego iban a 4:3 hasta disparar** (la pantalla de batchmode): lo que el HUD colocaba en el calentamiento quedaba una quinta parte más lejos del centro. Shots pone el aspecto y el panel de la foto desde el primer fotograma.
- **`Grid` choca con `UnityEngine.Grid`** también en el cliente: `Bastion.Engine.Grid` escrito entero.
- **El primer fotograma de cada pasada sale mal** en batchmode: sin la luz ambiente ni los reflejos hechos, el cielo alumbra menos y los materiales de Synty salen teñidos de lo que tuviera la GPU (rojo, rosa, azul, distinto cada vez; el árbol negro o rojo de antes era esto). Shots dibuja uno y lo tira antes de la primera foto. En modo juego no pasa: espera diez fotogramas.
- **Coches cruzados en sus casillas**: los prefabs de coches de Apocalypse en Unity van a lo largo de z; los FBX de origen, a lo largo de x. Se ve mal en las filas de aparcamiento a distancia, pero `BASTION_CARS=1` en MapLab lo dice coche por coche.
- **Sidekick en tiempo de ejecución:** su `DatabaseManager` hay que abrirlo (`GetDbConnection`) antes de buscar piezas, y quien lo use necesita las referencias a `SidekickCharacters` y `Gilzoide.SqliteNet`.
