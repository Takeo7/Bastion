# La biblioteca de recursos

Pedido de Arturo, 5/11: una herramienta con todos los paquetes, modelos, animaciones y sonidos, que diga qué se usa, en qué, y que deje elegir más cosas, quitarlas y asignarlas a un uso del juego.

## 1. Decisiones de Arturo (5/11, en el cuadro)

| Qué | Decisión |
|---|---|
| Dónde | **Una web local**: una página en el navegador, servida desde el Mac. No se publica nada. Primero se eligió una ventana del editor de Unity, y Arturo la cambió por la web (§2). |
| Cómo se cambia | **Arturo selecciona, Gameplay revisa y Arte lo mete.** La web no toca el juego. Solo guarda la selección de Arturo. Gameplay aprueba o rechaza cada punto, y Arte hace en Unity lo aprobado. |
| Qué se selecciona | Cambiar el modelo de algo que ya existe, añadir o quitar variantes de una lista que ya existe, y pedir un uso nuevo (para Gameplay, porque lleva reglas o código). |
| Paquetes sin importar en Unity | Se ven igual que los importados: la web lee los FBX del disco. |
| Tipos | **Todos:** modelos del mapa, animaciones, personajes, sonidos e interfaz. |

## 2. Por qué una web y no la ventana del editor

- **No bloquea Unity.** El proyecto solo lo puede abrir un editor a la vez. Con la ventana abierta, los roles no podrían usar `unity.sh` ni `captura.sh`.
- **Ve todos los paquetes sin importarlos.** Cada paquete del disco trae sus fuentes en un ZIP (`SourceFiles/*.zip`: los FBX y sus texturas). Three.js los abre con su `FBXLoader` (está en el repositorio, versión 0.186). No hay que meter 147 paquetes más en `Assets/Synty`.
- **Encaja con el reparto.** Las listas del juego son de Arte, y Gameplay revisa lo que entra. Si la web no escribe en el juego, nadie toca los archivos de otro.
- **Lo que se pierde:** la vista previa no es el aspecto exacto del juego, porque no tiene la luz ni los shaders de URP. El aspecto final se ve en las capturas que hace Arte en Unity desde la cámara del juego, antes de dar algo por hecho.

## 3. Lo que hay hoy (5/11)

**En el disco** (`/Volumes/4T_White/50_INBOX/Synty`, 60 GB): 183 paquetes. 106 POLYGON, 37 SIMPLE, 20 SIDEKICK, 10 INTERFACE, 6 ANIMATION y 4 POLYGON MINI. Ya hay inventario:
- `inventory.json`: la lista de paquetes;
- `<paquete>/manifest.json`: las versiones que ofrece Synty, sus tamaños y el sha256 de lo bajado;
- `<paquete>/Extras/`: el icono del paquete;
- `<paquete>/SourceFiles/*.zip`: los FBX, los OBJ y las texturas (el de Office: 775 FBX y 41 texturas). Los paquetes de SIDEKICK no traen fuentes: solo existen para Unity.

Además, `assets-src/synty` tiene 44 paquetes ya descomprimidos (los de la conversión de la 0.10.1) y `assets-src/mixamo` tiene los clips de Mixamo.

**En Unity:** unos 30 paquetes en `Assets/Synty` (6,6 GB, fuera de git), que importa `unity/tools/importar-synty.sh` con una lista fija.

**Lo que usa el juego**, por lista:

| Lista (`Client/Resources/`) | Qué guarda | Cuántos | De dónde sale |
|---|---|---|---|
| `MapArt.json` | Cada objeto del mapa (id → paquete, modelo y encaje), las listas de variantes (`sets`) y lo que ocupa varias casillas | 383 objetos de 11 paquetes, 77 listas | `map-art-json.mts`, desde `syntyManifest.ts` de la 0.10.1; desde la 0.11.7, también a mano (`doorPlank`) |
| `Anims.json` | Cada clip (id → paquete y archivo) y el papel de cada uno: andar, apuntar, morir, golpes, la vida del campamento, las armas | 186 clips | `anims-json.mts` |
| `Wardrobe.json` | Las piezas de Sidekick de los soldados | 55 | `wardrobe-json.py`, desde `sources.json` de la 0.10.1 |
| `Pieces.json` | Las piezas de los edificios, cortadas con Blender | 153 | `pieces-json.mts` y `pieces-cut.py` |
| `Undead.json` | Los vivos hechos muertos | 12 | `FigureCatalogMake` |
| `Audio/…/manifiesto.json` | Los sonidos | 451 archivos (110 sintetizados, 7 de los vivos, 24 grabados, 8 de interfaz) | `unity/tools/sonido/generar.mjs` |

Hay modelos que nombra el código y no una lista:
- los animales (`Fauna.Models`: `dog:`, `cats:`, `simple:`);
- los muertos especiales (`UnitViews.DeadPrefabs`: City Zombies y Boss Zombies);
- los efectos (`FxArt`, de Particle FX);
- los vehículos de La Lata;
- los nombres de Unity de `MapLab.UnityNames`.

Lo que lleva la app de verdad son los catálogos horneados (`MapCatalog.asset`, `FigureCatalog.asset`, `PieceCatalog.asset` y `FxArt.asset`), las escenas de la compilación y `Resources`.

## 4. Cómo funciona

```
disco (SourceFiles, inventario) ──┐
listas del juego (MapArt.json…) ──┼──> servidor local ──> página (Arturo)
usados.json (exportado de Unity) ─┘          │
                                             └──> seleccion.json ──> Gameplay revisa ──> Arte lo mete en Unity
```

### El servidor

- Es Node, en `unity/tools/biblioteca/`. Escucha solo en `127.0.0.1:3420`. Desde el 6/11 también se llega desde fuera, por el túnel de Cloudflare de Arturo, y solo con su usuario y contraseña (§7, «Desde fuera»). El puerto va a la tabla de EQUIPO.md. No usa el 3000, el 3001 ni el 5173.
- Lee el inventario del disco, las listas del juego y `usados.json`.
- Descomprime cada ZIP de fuentes la primera vez que se abre su paquete, en `assets-src/biblioteca/`, que no se sube. Sirve los FBX y las texturas a la página.
- Solo escribe `seleccion.json`.

### La página

Tres columnas:
- **Paquetes**, a la izquierda: los 183, agrupados por línea, con un filtro por estado y un buscador.
- **Recursos del paquete**, en el centro: miniaturas de sus modelos, clips, sonidos o imágenes, hechas en la propia página con Three.js y guardadas.
- **Ficha**, a la derecha: la vista en 3D (se gira con el ratón), dónde se usa y los botones para seleccionar.

| Estado | Qué quiere decir |
|---|---|
| **En uso** | Lo nombra una lista o el código, o lo lleva la compilación. La ficha dice dónde. |
| **Sin usar** | Está en el disco, pero el juego no lo usa. Dice si ya está importado en Unity. |
| **Seleccionado** | Arturo lo ha marcado. Dice en qué punto está: propuesto, aprobado, rechazado (con el motivo) o hecho (con la captura). |
| **Fuera de estilo** | En gris, no se puede seleccionar: SIMPLE (salvo Forest Animals y Cats) y POLYGON MINI (CLAUDE.md). |

### «Se usa en»

- **Objetos del mapa:** objeto → listas que lo llevan → dónde se usa cada lista (bioma, tipo de decorado). Con «Contar», cuánto sale de verdad: una herramienta de `dotnet` genera mapas con el motor de C# (semillas fijas, por bioma, sin Unity) y cuenta cada tipo de decorado. Las cuentas se guardan.
- **Animaciones:** clip → papeles de `Anims.json` → quién lo usa (soldado, zombi común, especial, campamento).
- **Personajes:** pieza de Sidekick → pieza del armario → soldados y presets. Prefab de POLYGON → tipo de muerto especial o animal.
- **Sonidos:** archivo → suceso del juego.
- **Interfaz:** imagen → pantallas que la usan.
- **Lo que lleva la app:** `unity/tools/biblioteca/usados.json`. Lo saca Unity con un paso de `unity.sh` (`BibliotecaExport`, de Arte, con su formato). Lleva las dependencias de las escenas de la compilación, de los catálogos y de `Resources`, cada una con su paquete del disco, su modelo y lo que la trae, y la parte de Sidekick: piezas, personajes y muertos hechos de vivos, con sus imágenes en `assets-src/biblioteca/unity/sidekick/`. Se rehace con cada versión.

### Lo que se ve en 3D y lo que no

- **Modelos POLYGON:** el FBX con el atlas de su paquete. Las trampas de los atlas salen igual de mal aquí que en la conversión de la 0.10.1: una segunda textura, un `SK_`, el atlas de otro paquete o el cristal (memoria «Synty atlas traps»). La ficha avisa cuando el modelo tiene una de ellas.
- **Animaciones:** el clip sobre el maniquí de su paquete (ANIMATION y Mixamo traen FBX con esqueleto).
- **Personajes de Sidekick:** no hay FBX. Unity saca una imagen de cada pieza y de cada preset con el mismo paso que `usados.json`.
- **Sonidos:** se escuchan en la página.
- **Interfaz:** las imágenes de los paquetes INTERFACE, tal cual.

### Seleccionar

Cada botón añade un punto a `seleccion.json`, con la fecha y una nota de Arturo:
- **Usar para…**: este modelo o clip en un id que ya existe (otro contenedor, otra muerte).
- **Añadir a una lista**: una variante más (más coches, más cuerpos de zombi).
- **Quitar**: de una lista o del juego. Si algo lo nombra, la ficha dice quién.
- **Uso nuevo**: algo que el juego aún no hace («que se pueda registrar»). Va a Gameplay como pedido de reglas.

Antes de guardar, la página avisa de lo que ya ha dado problemas:
- un modelo alto en una lista de cobertura baja (`cityLow`, `cityHigh`), por la caja del FBX;
- un atlas nuevo: un material más rompe lo juntado y cuesta en el fotograma (tope de 4,2 ms en «alto»);
- un paquete que no está importado en Unity: Arte tendrá que importarlo y añadirlo a la lista de paquetes.

### La revisión

1. **Gameplay** lee los puntos propuestos de `seleccion.json`. Pone cada uno en aprobado o rechazado, con el motivo, y reparte los aprobados: a Arte lo que es arte y a sí mismo lo que lleva reglas.
2. **Arte** lo mete en Unity. Importa el paquete si hace falta y escribe la lista. Rehace el catálogo (`MapCatalogMake.Run` o `FigureCatalogMake.Run`) y pasa la comprobación (`MapCatalogMake.Check`). Saca una captura desde la cámara del juego y pone el punto en «hecho», con la ruta de la captura.
3. **Arturo** ve en la página el estado de cada punto y la captura.

`seleccion.json` está en git (`unity/tools/biblioteca/seleccion.json`), con el historial de quién lo cambió. Cada punto lleva un id, para nombrarlo en los mensajes y en los commits.

## 5. Antes de que Arte meta cosas: la fuente de las listas

`MapArt.json`, `Anims.json`, `Wardrobe.json` y `Pieces.json` salen de scripts de `unity/tools` que leen la 0.10.1, que está congelada. `MapArt.json` ya se tocó a mano en la 0.11.7: si hoy se pasa `map-art-json.mts`, desaparece `doorPlank`. Con la biblioteca habrá más cambios a mano.

**Propuesta:** las listas pasan a ser la fuente, y los generadores se retiran: se quedan en el repositorio como historia, con una línea al principio que lo diga. Antes de retirarlos, se pasa cada uno y se comprueba que da la lista de hoy, salvo los cambios a mano que se sepan. Lo decide Arte, que es quien tiene esas listas, con Gameplay.

## 6. Fases

| Fase | Qué |
|---|---|
| 1 · Ver | El servidor y la página: los 183 paquetes con su ficha e icono, los modelos POLYGON en 3D desde los ZIP, «Se usa en» desde las listas y `usados.json` (el paso de Unity, de Arte), los estados y los sonidos. |
| 2 · Seleccionar y revisar | Los botones, los avisos, `seleccion.json` y el ciclo con Gameplay y Arte. Retirar los generadores (§5) antes del primer punto que Arte meta. |
| 3 · Animaciones y personajes | Los clips sobre su maniquí y las imágenes de Sidekick de Unity. |
| 4 · Contar e interfaz | La herramienta de `dotnet` que cuenta los mapas y las imágenes de los paquetes INTERFACE, que están en `Extras/*Source_Sprites*.zip` y no en SourceFiles (Arte, 5/11). |

Cada fase se prueba en la propia página, con capturas. La biblioteca no cambia nada del juego: no hace falta `CombatLab.Close` para ella. Sí hace falta para cada punto que Arte meta.

## 7. Cómo se abre y en qué está (5/11)

```bash
node unity/tools/biblioteca/server.mjs
```

Desde el 6/11 está siempre en marcha (abajo, «Arranque»); a mano, solo para probar. Luego, en el navegador, `http://127.0.0.1:3420`. La primera vez, en `unity/tools/biblioteca`, `npm install` (solo three.js 0.186.1). Se arranca desde una terminal: un proceso lanzado por la app de Claude no puede leer el disco externo sin el permiso de macOS para unidades extraíbles, y se queda colgado. Para el panel del navegador de las sesiones, `.claude/launch.json` tiene «biblioteca», que se engancha al servidor ya arrancado.

**Desde fuera (Arturo, 6/11): `https://biblioteca-bastion.yumewagener.com`.**
- **Cómo llega.** El túnel de Cloudflare de Arturo (`~/.cloudflared/config.yml`, el mismo de `bastion.yumewagener.com`) lleva ese nombre a `127.0.0.1:3420`. El servidor no escucha en ninguna otra dirección.
- **La contraseña.** Lo que llega por el túnel (con otro Host o con la cabecera `cf-connecting-ip` de Cloudflare) pide usuario y contraseña. Están en `~/Services/biblioteca/biblioteca.env` (600, nunca en git); sin ese archivo, desde fuera no entra nadie. Diez fallos desde una IP la bloquean 15 minutos; no hay tope para todos, para que nadie pueda dejar fuera a Arturo. Desde el Mac, en `127.0.0.1:3420`, no se pide: las capturas y las demás sesiones siguen igual.
- **Los cambios.** Desde fuera, los cambios (POST y DELETE: proponer, borrar, miniaturas, «Volver a leer») tienen que llevar la cabecera `X-Biblioteca: 1`, que pone la página (`web/util.js`, `OWN`), y no pueden venir de otro Origin. Así otra web abierta en el navegador de Arturo no puede usar la contraseña guardada.
- **Sin copias en Cloudflare.** Todo va con `Cache-Control: private` o `no-store`.
- **Arranque.** Lo arranca un LaunchAgent (`~/Library/LaunchAgents/com.yume.biblioteca.plist`, que ejecuta `~/Services/biblioteca/run.sh`), también al encender el Mac, y lo vuelve a arrancar si se cae. Así se lee el disco sin colgarse. Para reiniciarlo tras cambiar el servidor: `launchctl kickstart -k gui/$UID/com.yume.biblioteca`. Para probar con otra selección (`BIBLIOTECA_SELECCION`): `launchctl bootout gui/$UID/com.yume.biblioteca`, arrancarlo a mano y, al acabar, `launchctl bootstrap gui/$UID ~/Library/LaunchAgents/com.yume.biblioteca.plist`.
- **Si el disco no estaba montado** al arrancar, el servidor vuelve a leer todo en cuanto el disco aparece y la página pide el estado. Si no, «Volver a leer» o el `kickstart`.
- **Lo que queda abierto.** Con la contraseña se pueden ver y bajar los FBX y las texturas de Synty, como en el repositorio privado. Solo funciona con el Mac encendido y despierto.

**Hecho en la fase 1:**
- **Los 183 paquetes**, con su icono, versión, tamaños, si están importados en Unity (36) y si tienen algo en uso (29). Fuera de estilo, en gris y con el aviso.
- **Los modelos de cualquier paquete con fuentes**, en miniatura y en 3D, también los que no están importados en Unity. Se pinta cada malla con su textura de la MaterialList. Las MaterialList vienen escritas de tres formas, y los nombres de sus texturas no siempre son los de los archivos: se buscan por sus palabras.
- **Un prefab de Unity hecho con un FBX de otro nombre** (los zombis de City Zombies: `SM_Chr_Zombie_Jock_Male_01` es `SK_Zombie_Jock_Male_01.fbx`) se ve con ese FBX.
- **«Se usa en»**, sacado de MapArt.json (objetos, listas y si el código pide la lista por su nombre), Pieces.json, Anims.json (cada clip con sus papeles; las armas y quién las lleva), Wardrobe.json, Undead.json y los nombres que escribe el código (los animales, los muertos especiales, los efectos y lo que el código compone, como `City("Jock_Male_01")`: `composed` en `packs.json`). Lo que el juego pide solo por el nombre va al paquete que le daría Unity (`MapLab.Prefabs`).
- **«Lo lleva la app»**, del `usados.json` de Arte (el de la 0.11.9). Se dejan fuera las piezas recortadas por nosotros (`Pieces`, `Extra`), porque las listas ya dan su uso al FBX del que salen, y las mallas de colisión.
- **Sidekick**, con las imágenes de Arte: los personajes (los que dibuja el juego, en uso) y cada pieza por su hueco (pelo, torso, cascos…), con el armario, los muertos hechos de vivos y los personajes que la llevan.
- **Las animaciones de Synty y de Mixamo y los sonidos**, en listas con su uso. Los sonidos se escuchan.

**Lo que aún no se ve:**
- Los perros y los gatos salen en uso, pero sin 3D: van todos en un FBX. Se verán en la fase 3.
- Las partículas (Particle FX) salen en uso, pero no tienen 3D.

Capturas: `.dev-snapshots/biblioteca/fase1/`.

### Fase 2: seleccionar y revisar (5/11)

- **En la ficha, «Proponer»**, con cuatro botones:
  - «Usar para…»: un objeto del mapa, un arma, una animación o una prenda de los soldados que ya existen;
  - «Añadir a una lista»: una lista de objetos del mapa, de animaciones o una opción más en Personalizar;
  - «Quitar»: de dónde, y debajo, quién lo nombra, con lo de `usados.json`;
  - «Uso nuevo»: la nota dice cuál.

  Los destinos salen de las listas del juego (`lib/targets.mjs`).
- **Antes de guardar, los avisos.**
  - Un modelo más alto que lo que hay en una lista de cobertura baja, o más bajo en una de cobertura alta. Lo mide la página con la forma de su FBX y el `fit` de la lista (lo más largo de su planta a `fit` casillas, como `MapBuilder.Place`).
  - Un paquete sin importar en Unity, o importado a medias.
  - Una textura que el juego aún no carga, o un paquete del que no hay nada en el juego: un material más.
- **`unity/tools/biblioteca/seleccion.json`**, en git, con un punto por línea. Cada punto lleva:
  - `id` (`B-001`; no se repite aunque se borre uno) y `tipo` (usar, añadir, quitar, nuevo);
  - `recurso` (clase, paquete, nombre y FBX) y `destino` (clase e id, o nada);
  - `nota`, `fecha`, `estado`, `avisos`, `motivo` y `captura`.
- **Quién toca qué:**
  - la página solo crea puntos en «propuesto» y deja borrarlos mientras lo sigan siendo;
  - Gameplay pone «aprobado» o «rechazado», con `motivo`, editando el archivo a mano;
  - Arte, UI o Red ponen «hecho», con `captura` (la ruta en `.dev-snapshots`).

  La página lo vuelve a leer cada 8 s y al volver a la ventana, sin reiniciar.
- **«Tu selección»**, a la izquierda: todos los puntos por estado, con el motivo de Gameplay y la captura de «hecho». En los modelos, una etiqueta con su estado.
- **Para probar sin tocar la de Arturo:** `BIBLIOTECA_SELECCION=<otro archivo> node unity/tools/biblioteca/server.mjs`.

Capturas: `.dev-snapshots/biblioteca/fase2/`.

### Fase 3: clips, perros y gatos (5/11)

- **Cada clip se mueve en la ficha**, en bucle, con «Pausa».
  - Los de los paquetes ANIMATION van sobre el maniquí de Synty que trae su paquete: `PolygonSyntyCharacter` o `SidekickSyntyCharacter`, según el rig del clip. Si su paquete no lo trae, el de Base Locomotion.
  - Los de Mixamo vienen sin cuerpo: van sobre sus huesos, hechos una figura de palos.
  - Si el maniquí no tiene los huesos del clip (menos del 60 %), también sobre sus huesos.
  - Se ven en el sitio: el hueso raíz no avanza, así que andar o correr no se salen de la vista.
- **Los perros y los gatos**, que van todos en un FBX (`Unity_SK_Animals_Dog_01.fbx`, `SimpleCats.fbx`; `container` en `packs.json`). Cada prefab enseña solo sus mallas: las de la MaterialList, sin los accesorios, o la que da su nombre (`SimpleCats_Tabby_02` es `SM_Tabby_02`), cada una con su textura. El FBX se carga una vez y se copia para cada uno.
- **Las animaciones de los perros** (`FBX/Animations/`) van sobre el labrador.

Capturas: `.dev-snapshots/biblioteca/fase3/`.

### Fase 4: las imágenes de INTERFACE (5/11)

- **Las imágenes de los paquetes INTERFACE**, tal cual y sobre un fondo de cuadros que deja ver la transparencia, por carpeta. Salen de `Extras/*Source_Sprites*.zip` (Apocalypse HUD, Military Combat HUD, Sci-Fi Menus…) o de sus fuentes (Modern Menus).
- **En uso**: los iconos del HUD que lleva la app, de `usados.json` (Military Combat HUD 42, Apocalypse HUD 19, Modern Menus 7, Dark Fantasy HUD 1).
  - El paquete de Unity de Apocalypse HUD les cambia el nombre: `ICON_Apocalpyse_Inventory_Backpack_01` es `ICON_Inventory_Backpack`. Se enlazan por el nombre sin `Apocalpyse` ni `_01`, y la ficha dice cómo se llama en Unity.
  - Uno (`ICON_Apocalpyse_Status_Health_01`) no tiene imagen con su nombre en las fuentes.
- **«Usar para…»** una imagen que ya usa el juego, «Quitar» y «Uso nuevo». Lo aprobado lo mete UI.
- **«En los mapas»** (fase 4, con las cuentas de Gameplay: `unity/tools/biblioteca/cuentas.json`, `dotnet run --project engine-cs/tools/ContarMapas`, 100 mapas de cada uno de los 30 tipos). En la ficha de un modelo, por cada objeto del mapa que es: en cuántos tipos de mapa sale y, donde más, cuántos por mapa (media, de… a…) y en qué parte de los mapas. Lo que Unity pone con otra regla sale «Sin contar».
- **Lo que no se usa en el juego** (`sinNombrar` de las cuentas): 7 listas de MapArt que nada pide (`cityLow`, `cityHigh`, `wildsLow`, `wildsHigh`, `facilityLow`, `facilityHigh`, `basePines`: las de reserva de la 0.10.1) y 32 objetos. No salen en uso, aunque vayan dentro de la app: su ficha dice «En las listas, pero no sale». Tampoco se ofrecen como destino.
- **«Quitar» un objeto del mapa** (Gameplay, 5/11): lo pide un tipo de decorado del motor, y quitado sin más su casilla quedaría sin dibujar. Así que se lee «Quitar «x» del mapa. Gameplay decide qué sale en su lugar». Quitar una variante de una lista es directo, salvo si es la última: entonces avisa.
- **El aviso de altura** va por las `coberturas` de las cuentas: las 37 listas que el motor pone en casillas de cobertura, «baja» o «alta» (`crates` baja, `tree` alta…). `shelfLow` y `shelfHigh` las pone Furnished con su propia regla, y van por su nombre. Las listas que nada dibuja no avisan, porque no se ofrecen. Por ejemplo, la nevera en «bin» mediría 1,98 casillas, frente a 0,72 de lo más alto de la lista.

Capturas: `.dev-snapshots/biblioteca/fase4/`.

### Candidatos (9/11, encargo de Gameplay)

Gameplay pide a la biblioteca que busque en el disco lo que podría entrar en el juego. Arturo lo elige en el cuadro.
- **«Candidatos 9/11»**, en «Del juego»: la lista `unity/tools/biblioteca/candidatos/09-11.json`, por grupos (soldados, zombis, armas y objetos). Cada candidato lleva por qué encaja, si es pieza de Sidekick o personaje entero de POLYGON, y sus vértices, junto a los de un zombi de hoy. Su ficha lo enseña en 3D, o en foto si es de Sidekick, y se propone desde ella como cualquier otro recurso. Si una lista nueva sale otro día, irá en otro archivo (`candidatos/<día-mes>.json`).
- **Las fotos que no saca Unity** (presets de Sidekick sin importar, como Modern Thugs) están en `assets-src/biblioteca/candidatos/`.
- **`containers` en `packs.json`:** el FBX con todos los personajes de los paquetes que el juego no usa (Horror Asylum, Apocalypse Wasteland, Police Station…). En ellos, la MaterialList de Synty da a cada personaje todas las mallas del FBX, así que se dibuja la malla con su nombre. `big` es el FBX de los personajes grandes (el gigante de Wasteland, el bruto de Movie Set). Antes salían como «Prefab de Unity: sin FBX con su nombre».

Las hojas, el LEEME y los clips sin usar están en `.dev-snapshots/biblioteca/candidatos-09-11/`.

## 8. Decidido por Gameplay (5/11, encargo de Arturo)

- **La web la programa la sesión «Biblioteca de assets Unity»** (Arturo, 5/11; antes se había dado a Arte): el servidor y la página en `unity/tools/biblioteca/`, en las fases 1 a 4, con las imágenes de INTERFACE.
- **Arte se queda con lo de Unity:** `BibliotecaExport` (`usados.json` y las imágenes de Sidekick, con `unity.sh`), retirar los generadores (abajo) y meter en el juego los puntos aprobados de mapa, animaciones y personajes. La web lee lo que exporta Arte; mientras no esté, «Se usa en» sale solo de las listas.
- **La herramienta de `dotnet` que cuenta los mapas** (fase 4) es de Gameplay, porque corre el `Mapgen` del motor. Lore revisa los textos de la página al acabar la fase 1.
  - Hecha el 5/11: `engine-cs/tools/ContarMapas` (`dotnet run --project engine-cs/tools/ContarMapas [semillas]`, unos 2 min con 100) escribe `unity/tools/biblioteca/cuentas.json`. Genera 100 mapas de cada uno de los 30 tipos (Ciudad, Ciudad · Polígono, Afueras, Afueras · Área de servicio y Fábrica, por las 6 misiones) y los cuenta con la regla de `MapBuilder.Decor`: `MapArt.DecorSet`, `MapArt.Hash` y la elección por casilla, copiadas en la herramienta tal como están en la 0.11.9. Si Arte cambia esa regla, hay que cambiar la copia.
  - Lo que Unity pone con otra regla va a `sinContar`: los coches y bloques de varias casillas, las fuentes, el botín, las estanterías de tienda, los muebles de clínica y de casa, las tablas de las puertas y las barricadas de las ventanas.
  - **Hallazgo:** 7 listas de MapArt no se dibujan en Unity, y están en `sinNombrar`: `cityLow`, `cityHigh`, `wildsLow`, `wildsHigh`, `facilityLow`, `facilityHigh` y `basePines`. Las seis primeras eran la reserva de la 0.10.1 para la cobertura sin decorado. En Unity, toda la cobertura del suelo a ras lleva decorado: `sinDibujo` da 0 en los 3.000 mapas, y la que no lo lleva arriba o en alto son pretiles, que dibujan las paredes. `basePines` no se usa porque el pinar de la base tiene su propia lista en `BaseWoods`. Con ellas, 32 objetos que no salen en ninguna otra lista. La página no las ofrece como destino.
- **Quién mete cada punto aprobado:** modelos del mapa, animaciones y personajes, Arte; sonidos, Red (`unity/tools/sonido`, `generar.mjs` sigue siendo la fuente del manifiesto); interfaz, UI (los archivos de UI-COMBATE v1.0, solo con el sí de Arturo); usos nuevos, Gameplay.
- **Los generadores se retiran**, antes del primer punto que Arte meta (§5): `map-art-json.mts`, `anims-json.mts`, `wardrobe-json.py` y `pieces-json.mts`. Cada uno se pasa a una salida aparte (nunca encima de la lista), se compara con la lista de hoy y las diferencias que no sean cambios a mano conocidos (como `doorPlank`) se miran con Gameplay antes de nada. Luego, una línea al principio de cada script dice que está retirado y que la lista es la fuente. `pieces-cut.py` y `props-cut.py` siguen: cortan modelos, no escriben listas. `Undead.json` sigue saliendo de `FigureCatalogMake`.
- **El puerto 3420**, solo en 127.0.0.1, en la tabla de EQUIPO.md.
