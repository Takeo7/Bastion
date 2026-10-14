# Dirección de arte

Estado al 2026-10-14, con la versión 0.2.1. Lo lleva la sesión **Arte**, con los archivos de [EQUIPO.md](EQUIPO.md). La etapa anterior (laboratorio de estilos, vóxel, Expediente, Holotáctico, el Canto de Velmar) está en [archive/ARTE-historial.md](archive/ARTE-historial.md).

![El juego hoy: la ciudad de día y de noche, el campo, una instalación, la base al empezar y el valle](arte/juego-hoja.jpg)

## Normas

- **De dónde sale el arte:** solo Synty POLYGON (con la suscripción de Arturo), Kenney (CC0) o lo hecho por nosotros. Tipografías OFL, como mucho dos familias. Nada de otros packs, aunque sean gratuitos.
- **El aspecto objetivo** son las capturas promocionales de Synty, en `assets-src/referencias/aspecto-synty/`.
- **Licencia:**
  - los originales viven en `assets-src/synty/` y no se suben;
  - al repositorio solo van los modelos convertidos que usa el juego, y por eso `Takeo7/Bastion` es siempre privado.
- **Rendimiento:**
  - ≤4,2 ms por fotograma en «alto» en el PC de referencia (Windows, RTX 3090), y «medio» a ≥30 fps en el Mac;
  - el aspecto no se recorta por el Mac: para eso están los ajustes;
  - se mide el tiempo de fotograma, no los fps.
- **Ritmo:** cada acción se ve entera antes de pasar a la siguiente, y la cámara nunca pelea con el jugador.
- **Lo rechazado no se vuelve a proponer:** vóxel, Expediente, Holotáctico, Viñeta, CRT y el resto del laboratorio de estilos.

## Los packs

Todos en `assets-src/synty/`. Las rutas de cada uno están en `PACKS` de `syntyManifest.ts`.

| Pack | Para qué |
|---|---|
| POLYGON Apocalypse | Supervivientes, saqueadores, rescatados, dos de los muertos, las armas y casi todo el decorado de la calle |
| POLYGON City Zombies | Los muertos comunes, los corredores y los acorazados. Solo viene como `.unitypackage`: el conversor lo desempaqueta una vez |
| POLYGON Boss Zombies | El Chillón y el Hinchado |
| POLYGON Town, City, Farm, Nature, Construction y Military | Los mapas de combate: edificios modulares, calles, campo, obras y controles militares |
| POLYGON Office, y dos de City | Las voces de la radio (`strategy/portraits.ts`) |
| Nature Biomes – Alpine Mountain y POLYGON Snow | La base en la sierra |
| ANIMATION – Base Locomotion e Idles | Las animaciones de todos los personajes |
| POLYGON Particle FX | Los efectos del combate y del Oficio |
| POLYGON Icons | Piezas para los renders de la interfaz |
| INTERFACE Apocalypse HUD, Modern Menus y Military Combat HUD | La interfaz (los lleva UX/UI) |

## La tubería

La explica [tools/synty/README.md](../packages/client/tools/synty/README.md).

- Una sola lista dice qué se usa y para qué: `src/game/syntyManifest.ts`. El conversor (`tools/synty/convert.ts`, con Blender) convierte exactamente eso a GLB en `public/models/synty/<pack>/`. Hoy son unos 12 MB.
- **Siempre con ids explícitos:**

  ```bash
  npx tsx packages/client/tools/synty/convert.ts hunter sofa
  ```

  Sin ids, o con `--force`, borra lo que no está en el manifiesto y podría llevarse lo nuevo de otra sesión.
- **Escala:** una casilla son 1,5 m (`SYNTY_SCALE`). Los kits modulares van en módulos de 2,5 m y se dibujan 1,2 veces más grandes (`KIT_SCALE`).
- **Materiales:** un atlas por pack, cargado una vez y compartido (`ATLASES`, `game/synty.ts`). Los ocho atlas del mapa van además en una sola textura (`game/mapAtlas.ts`).
- **Personajes:**
  - 22 huesos con los nombres de los packs de animación;
  - pelo, barba y sombrero rígidos, cada uno en su hueso;
  - un segundo mapa UV (`part`) dice qué se puede teñir.
- **Ajustes sin reconvertir:** el giro, el tamaño y el sitio de los objetos (`fit`, `yaw`, `shift` en `PROPS`) se aplican en el juego.

## Qué hay y dónde

### Personajes (`game/syntyFigures.ts`, `syntyAnims.ts`, `unitViews.ts`)

- **Supervivientes:** 16 personajes para elegir en el Cuartel (`SURVIVOR_CHOICES`). Sin elección, el soldado sale de su complexión y de su sexo (`SQUAD_BODIES`), y es el mismo en todas partes.
- **Los muertos:** comunes (10 modelos), corredores (4), acorazados (4, un poco más grandes), el Chillón y el Hinchado (`UNIT_BODIES`).
- **Animación,** por capas:
  - los clips de Base Locomotion e Idles se miden contra la pose en T del pack y se pasan a los huesos de cada personaje, así que el mismo clip sirve para los dos rigs de Synty;
  - encima, el torso (encogerse, impactos, retroceso, lanzar, gritar) y los brazos: el arma en la mano derecha y la izquierda en el guardamanos, las dos con cinemática inversa;
  - la muerte: se doblan las rodillas y el cuerpo cae de espaldas o de lado.
- **Coste:** la pose se actualiza a 30 Hz en reposo o lejos, a 8 Hz fuera de pantalla y nada en la niebla o muertos. Solo los 24 enemigos más cercanos dan sombra. Las unidades van en su propia capa (`UNIT_LAYER`), fuera de la oclusión ambiental.
- **Retratos de la radio:** personajes de Synty en su panel (`strategy/portraits.ts`).
- **Sin reserva de primitivas:** `models.ts` y `figures.ts` solo guardan el contrato que mueven las vistas (`UnitRig`, `FigureRig`, `FigurePose`, `Gear`). Una plantilla sin personaje en el manifiesto sale como recluta y avisa en la consola.

### Mapas de combate (`game/mapView.ts`, `buildings.ts`, `ground.ts`, `dressing.ts`, `batch.ts`)

- **Edificios en los que se entra,** de los kits modulares:
  - el tejado se desvanece cuando se ve el interior por una ventana o una puerta;
  - varias plantas, con corte por planta y la planta activa (RePág/AvPág, Mayús+rueda).
- **Dos estilos de ciudad,** mitad y mitad: cuadrícula y pueblo viejo. Hileras de tiendas, gasolinera, motel, casas con jardín, obras y control militar. Los bordes siguen la calle con vehículos abandonados.
- **Luz de día** (`renderer.ts`), medida contra la captura de la ciudad de Apocalypse por bandas de tono: una neblina fina y cálida, el sol bajo a la derecha, la sombra levantada hacia el malva y una saturación media como la suya (0,29).
- **Suelo:** terreno continuo, sin aspecto de cuadrícula, y oclusión de contacto horneada (`groundAO.ts`). En la ciudad:
  - pavimento claro y poco cálido;
  - polvo a manchas, losas y asfalto agrietados en las zonas viejas, musgo en el bordillo;
  - papeles, escombros y basura sueltos, todo plano y cosmético (`ground.ts`, `dressing.ts`);
  - los tejados con trastos (`buildings.ts`); cajas solo en los que no se pisan.
  Lo que pueda leerse como cobertura lo pone el motor, nunca el decorado. El generador (mapgen.ts, acordado con Gameplay) pone árboles en las esquinas de las plazas, escombros en plazas y aparcamientos y más coches abandonados, siempre fuera del despliegue, del objetivo, de la evacuación y de los bordes, y a 2 casillas o más de paredes, puertas y escaleras. La cobertura cercana pasa del 25,7 % al 26,9 % del suelo abierto.
- **Noche** (`night.ts`), contra la captura del motel de Woodland de Synty: neblina verdosa y luna fría, y la luz cálida de lo que aún funciona (farolas, algún faro, bengalas y fuegos). Qué casillas están a la luz lo dice el motor.
- **Niebla de guerra** (`fogOfWar.ts`, `explored.ts`) sobre los materiales, por planta.
- **Coste:**
  - lo estático, fundido en pocas mallas (`batch.ts`), con sombras en caché;
  - la reconstrucción por trozos baja de 160–270 ms a 4–13 ms;
  - los ocho atlas del mapa en una textura: de 83 a 63 llamadas en la manzana de referencia.

### Base y valle (`strategy/bastion.ts`, `baseKit.ts`, `stage.ts`, `geoscape.ts`)

- **El Bastión:** Atalaya en la sierra, con la luz de la manzana de referencia (de 524 a 132 llamadas). Tiene el Cuartel, el Taller de oficios, obras con Construction y el Huerto por niveles: bancales con plástico, invernadero con estufa, riego por goteo y espantapájaros.
- **El valle de Kessra** en Synty, en lugar del globo:
  - las 8 zonas con sus pueblos, el embalse, las minas y la Vega;
  - lee la geografía del motor (REGIONS, ROADS), y La Mula sigue las carreteras;
  - la Marea cubre exactamente las zonas caídas: zombis, luces apagadas y el frente animado. También oscurece campos y carreteras;
  - lomas suaves en el fondo del valle; prados de varios verdes, el suelo oscuro bajo los bosques y nieve a parches en Aldren;
  - campos de cultivo en una malla de hileras (granjas, el mosaico de La Vega, huertos tras las casas);
  - pueblos con tejados de Town y un hito cada uno: iglesia o depósito de agua;
  - ciudades con el borde irregular, un parque y patios;
  - carreteras con arcén, asfalto y línea discontinua, postes con cable, coches abandonados y dos choques en cadena;
  - bosques en manchas;
  - 57 llamadas en el menú y 77 en la campaña con las 8 zonas contactadas (los marcadores suman 20). También sirve de fondo del menú.

### Efectos y renders (`game/fx.ts`, `songFx.ts`, `lab/renders/`)

- **Efectos,** con el aspecto de Particle FX: humo, polvo y gas en bocanadas facetadas, sangre en gotas planas, fogonazos en estrellas y fuego en lenguas de llama. La luz (fogonazos, trazadoras, chispas) queda como brillo suave. Aparte, los del Oficio (`songFx.ts`).
- **Renders para la interfaz:** cosas del juego como modelos de Synty, en vista de 3/4 y sobre fondo transparente, cada una con su silueta para teñir con CSS. Salen a `public/ui/synty/renders/`. Hay clases, recluta, molotov, La Mula (tres vistas), botiquín, ración, piezas, camposanto, roca y ruinas.

## Laboratorios

Se abren con el cliente de pruebas (el mío, en el 5197). Todos usan el código del juego y su luz.

| Página | Para qué |
|---|---|
| `/referencia.html` | Una misión generada con la cámara del juego, para comparar con las capturas de Synty y medir un fotograma: CPU, GPU, llamadas, triángulos y objetos. `__ref.bench(300)` dibuja 300 fotogramas sin tope. Con `?biome=city&seed=7&night&fog` y `?map=torre` se elige el mapa |
| `/hordas.html` | Lo que cuestan N zombis por fotograma, por partes (clips, transferencia, pose, escena, render). `__horde.run([10, 20, 40, 60, 80])`, y `__horde.floor(40)` estima lo que ahorraría hornearlos |
| `/modelos.html` | Todos los personajes en una pose (`#idle`, `#aim`, `#run`, `#crouch`, `#search`, `#hit`, `#dead`) y los retratos de la radio |
| `/armas.html` | Cada arma en las manos de cada cuerpo, pose a pose. `__weapons.sheet(...)` guarda una hoja |
| `/efectos.html` | Cada efecto del combate y del Oficio sobre un superviviente y tres zombis (`#molotov`, `__fx.play('bengala')`) |
| `/cuartel.html` | La escena del Cuartel con la cámara sobre el soldado |
| `/valle.html` | El mapa del valle solo, con las llamadas y los triángulos de cada fotograma. Teclas 0–8 para las zonas, con el zoom más cercano del jugador; `?menu` para el fondo del menú, `?doom=6` para la Marea, `?contact` para contactarlo todo. `__valle.look(x, z, distancia)` y `__valle.snap(nombre)` |
| `/renders.html` | Los renders de la interfaz. `__renders.save()` los manda a `.dev-snapshots/` |
| `/sonido.html` | Todos los sonidos (son de Red y de UX/UI) |

Las capturas de los laboratorios van a `.dev-snapshots/`, que no se sube. Con el panel del navegador oculto, `?timer` hace que los fotogramas avancen igual.

## Pendiente

- **Medir en el PC con la RTX 3090:** la telemetría de una misión (`data/red.log`) dice si hace falta lo siguiente.
- **Hornear las hordas** (InstancedMesh con la animación en una textura), solo si las unidades pasan de ~1 ms por fotograma.
- **Lo que pedirán las reglas más adelante:** saltar desde ventanas altas, suelos y paredes destructibles, escaleras que solo se dejan por un lado.
- **Para UX/UI, si lo pide:** la venda en render ([UX-CARENCIAS.md](UX-CARENCIAS.md)).
- **Si las etiquetas de los soldados aún molestan** con la escuadra junta: apartar en vertical las que se solapen. Por ahora, la del seleccionado y la del señalado van por encima de las demás (`renderOrder` en `unitViews.ts`).
