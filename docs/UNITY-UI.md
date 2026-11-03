# El HUD del combate en Unity (UI Toolkit)

Inventario del HUD del combate de la 0.10.1 (Three.js), para pasarlo a Unity en el hito 1 de [UNITY.md](UNITY.md). Lo ha escrito la sesión UI el 23/10, sin editor de Unity todavía.

Para cada pieza:
- qué es y dónde está;
- qué datos lee (del estado, de los eventos o de los mensajes del servidor);
- qué órdenes manda;
- cómo la haría en UI Toolkit.

Al final están lo que el motor de C# tiene que darme, las diferencias entre CSS y USS que obligan a hacer las cosas de otra manera, y las preguntas abiertas.

Los nombres de campos y funciones son los de TypeScript de hoy (`packages/engine`). En C# se llamarán como decida Gameplay; lo que importa es que estén.

## 1. Cómo está montado hoy

- **hud.ts** (1.880 líneas) pinta el HUD en el DOM. No calcula reglas: recibe «vistas» ya hechas (`AbilityView`, `TargetView`, `MissionView`…) por sus `set…` y, si la vista no ha cambiado (compara una clave), no vuelve a pintar.
- **game.ts** construye esas vistas. Hay dos estados:
  - `view`, lo que ya se ha animado: con él se pinta lo que se lee (turno, escuadra, soldado elegido, objetivo). Así una muerte no sale en la lista antes de verse.
  - `state`, lo último que llegó: con él se deciden las órdenes y sus comprobaciones (barra de acciones, panel de disparo, Terminar turno, el mando).
- **Las órdenes** salen por `HudHandlers` (botones y teclas) hacia game.ts, que manda al servidor una orden del motor (`Command`) o un mensaje de red (`ClientMessage`).
- **Los eventos** del motor llegan en tandas (`events`, con `seq` y `by`) y se reproducen uno a uno. El HUD reacciona a algunos: avisos, registro, textos flotantes.
- **Tema «Mixta»** (`ui/themes/mixta*.css`): placas de acero con textura y bisel, amarillo de peligro para lo que hay que pulsar, verde bueno y rojo peligro. Oswald para títulos y números grandes; Barlow para lo que se lee. Una tercera «fuente» de un solo carácter, el «§» de suministros, hecha por nosotros (`tools/ui/suministros-font.py`).

**En Unity propongo el mismo reparto:**
- Un *presentador* en C# del cliente construye las vistas a partir del estado del motor; es lo que hoy hacen `refreshHud`, `refreshStatus`, `abilities()`, `targetPanel()` y `missionView()` de game.ts.
- Cada pieza del HUD es una clase que recibe su vista y solo pinta.
- El HUD no lee nada de la escena de Unity salvo la cámara, para colocar las marcas sobre las unidades.
- Las vistas son objetos inmutables que se comparan por valor: pintar solo si cambian.

## 2. Inventario

### 2.1 Lista de la escuadra (`setSquad`, `.squad`)

Arriba a la izquierda. Una ficha por soldado: primero los propios, luego los del compañero que mandas mientras no está y luego el resto.

**Cada ficha:**
- el hexágono de su mejor habilidad (`skillHex`, #30);
- el nombre (el apodo entre comillas angulares si tiene);
- los puntos de salud;
- las acciones que le quedan (en una torreta del asedio, la munición);
- las etiquetas de estado: EN JUEGO, CONTROLADO, CAUTIVO, DESORIENTADO, PÁNICO, MORDIDO, EVACUADO, la carga de la misión, VIGILANCIA / ZONA LETAL / HOJA EN ALTO, CUBRIÉNDOSE, ATURDIDO;
- el borde en el color de su jugador.

**Estados de la ficha:** elegida, muerta, evacuada, mía y en juego.

**Lee** (de `view`), por unidad:
- `id`, `name`, `nickname`, `team`, `owner`, `template`, `alive`, `evacuated`;
- `hp`, `maxHp`, `ap`, `apPerTurn(u)`;
- `lines` y `deeds` (`liveSkills`), para el hexágono;
- `automaton` y `ammo`, para la torreta;
- los estados: `controlledBy`, `captive`, `disoriented`, `panicked`, `infected`, `overwatch`, `killZone`, `watchMelee`, `hunkered`, `stunned`.

**Y además:**
- `mission.item.carrier`, quién lleva la carga;
- `activeUnit`, el soldado en juego cuando se juega de uno en uno con el compañero;
- los colores de los jugadores.
- Para saber si una ficha es mía (se puede elegir) hace falta saber qué soldados mando: los míos, más los del compañero si no está (`commands(u)` en game.ts).

**Manda:** nada al servidor. El clic elige el soldado (`onSelectUnit`), que es estado del cliente. Elegir lo ve el compañero por `presence`.

**En UI Toolkit:**
- La ficha es una plantilla UXML (`SquadCard.uxml`) instanciada en una columna, sin ListView: son 4–12 fichas y cambian poco.
- Puntos de salud y acciones: filas de `VisualElement` pequeños con clase `on`.
- Etiquetas: `Label` con clase por tipo.
- Hexágono: un elemento propio con `generateVisualContent` y `Painter2D` (ver §4).

### 2.2 Panel del soldado elegido (`setSelected`, `.unit-panel`)

Abajo a la izquierda, sobre la barra.

**Lleva:**
- **Cabeza:** galones de veteranía, el hexágono, el nombre, el rango, «DE ANA» si es del compañero y la etiqueta MORDIDO. Un clic abre la hoja del soldado (H).
- **Cifras con icono:**
  - salud;
  - acciones;
  - munición del arma, con los cargadores de repuesto como «+3»;
  - munición de la pistola si lleva arma principal;
  - puntería con armas de fuego y cuerpo a cuerpo;
  - granadas;
  - blindaje;
  - chaleco puesto.
- **El arma**, con su estado y sus mejoras (`wearMarks`).
- **La mochila**, si el mapa tiene algo que registrar o lleva algo encima: casillas y menú de cada casilla (Soltar, Usar, Curar a, Dar a, Equipar, Ponerse, Al cinturón), lo que hay en el suelo y lo que lleva toda la escuadra.

**Lee** (de `view`), por unidad:
- `rank` (`VETERANCY`), `appearance.sex` (el rango en femenino);
- `arms` (`primary`, `melee`, `mods`), `weaponName`, `weaponTier`;
- `ammo`, `clipOf(u, 'shoot')`, `mags` (`magsCount`), `pistolAmmo`;
- `TEMPLATES[t].aim` + `mods.aim` + `mods.gunAim` / `mods.meleeAim`;
- `charges.grenade`, `armor`, `worn`, `gear`, `conditionOf(u)`.

**Para la mochila** (`packView` en game.ts):
- `backpack`, `packSlots`, `dropped` (lo que hay en el suelo de su casilla y de quién era);
- `lootTotals` de la escuadra;
- quién está al lado (`inMeleeReach`);
- textos de Lore: `findLabel` y `lootName` (`haulText` es de game.ts);
- reglas: `wearable`, `beltable`.

**Manda:**
- `item` (`drop`, `take`, `give`, `use`, `equip`, con `index`, `id` o `to`);
- coger todo (varios `take` seguidos);
- el cambio cuando la mochila está llena (`drop` y luego `take`).

**En UI Toolkit:**
- Panel con dos zonas (cabeza, cifras) y la mochila como plantilla aparte.
- El menú de una casilla es un panel flotante sobre la mochila; se cierra al pulsar fuera, con un `PointerDownEvent` en la raíz.
- **Para el hito 1** basta con la cabeza, las cifras y el arma. La mochila entra cuando haya misiones con registro.

### 2.3 Barra de acciones (`setAbilities`, `.abilities`)

Abajo en el centro. Un botón por habilidad del soldado elegido, solo con el icono (Ana, 22/10 #35). Lleva:
- el icono del arma en Disparar (#7 del 23/10);
- `×N` si tiene usos contados;
- el número de turnos de espera;
- un sello si está agotado;
- una nota sobre el botón cuando está bloqueado por algo que se arregla («Sin balas: recarga»).

**Tarjeta al pasar el ratón:**
- nombre y tecla;
- las cifras en dos columnas (acciones, alcance, daño, ruido…) o, si no tiene cifras, coste · límites · ruido;
- qué hace;
- por qué no se puede ahora.

**Menús:** Lanzar, Vigilancia e Investigar agrupan opciones. Con una sola opción, el botón es esa opción. Con dos o más, abre las opciones encima, numeradas desde 1, y las teclas numéricas las eligen.

**Fuera de mi turno,** la barra descansa con un aviso encima: «Turno de Ana: puedes apuntar y planear» o «Turno enemigo».

**Teclas:** 1–9 usan la habilidad del número. Con el menú abierto, eligen opción.

**Lee** (de `state`, porque son órdenes), con la vista `AbilityView`:
- `id`, `label`, `hotkey`, `enabled`, `reason`, `note`;
- `name`, `cost`, `limits`, `damage`, `stats`, `noise`, `description`;
- `active`, `count`, `cooldown`, `free`, `depleted`, `icon`, `weapon`, `options`.

Lo construye game.ts `abilities()`, hacia la línea 4393.

Del motor:
- `abilityBlocker(s, u, a)`, que da el porqué en texto;
- `attackWeapon`, `hackChance`, `clipOf`, `apPerTurn`;
- `cooldowns` de la unidad;
- `ABILITIES`;
- `searchSpot` y `reachesToSearch`, para Investigar.

De game.ts, que pasan al presentador (míos):
- `soldierAbilities` y `abilityList`;
- `abilityStats`, `damageText`, `limitsText`, `noiseText`;
- `crateBeside`;
- `evacActions` y `abandonActions`, que añaden los botones de Evacuar y de responder a la llamada del compañero.

**Manda:**
- **Habilidad sin objetivo** (Cubrirse, Recargar, Vigilancia…): se arma con la primera pulsación y se confirma con la segunda o con Intro. Manda `ability` (`unit`, `ability`).
- **Habilidad con objetivo:** abre el panel de disparo (2.4).
- **Habilidad de casilla** (granadas): se apunta en el mapa y manda `ability` con `tile`.
- **Evacuar** manda `evac` (`unit`); responder a la llamada, `evacAnswer`.
- **Abandonar** manda los mensajes `abandon` y `abandonAnswer` (no son órdenes del motor).

**En UI Toolkit:**
- Fila de `Button` con el icono como imagen de fondo: los iconos de los paquetes INTERFACE de Synty, que en Unity vienen como sprites.
- La tarjeta de ayuda es un elemento propio (ver «Tooltips» en §4).
- El menú es un contenedor absoluto encima del botón.
- El sello de agotado y los contadores son `Label` superpuestos.
- Las teclas van por el Input System y llaman a la misma función que el clic.

### 2.4 Panel de disparo (`setTarget`, `.shot-panel`)

A la derecha, bajo la columna del objetivo, sin taparla. Tres formas:

**Ataque:**
- título (habilidad y arma);
- el blanco con flechas para cambiarlo y «2/5»;
- el dial con el porcentaje de impacto (un arco alrededor) y el de crítico;
- el daño («DAÑO 4–6 · crít. 6–8», notas del blindaje, «Lo mata»);
- los modificadores de impacto y de crítico, en verde y en rojo;
- la rozadura posible y la nota de la habilidad;
- el ruido;
- DISPARAR y Cancelar.
- **Sin el mando** el botón dice «MANDO: ANA» y está apagado: se puede apuntar para planear.

**Apoyo** (curar, etc.): título, blanco, efecto y, si lo tiene, el daño.

**Propia armada:** se coloca sobre el botón que se pulsó y dice:
- coste, límites y ruido;
- qué hace;
- «Otra vez, Espacio o Intro: usar · Esc: cancelar».

**Lee** (de `state`), con la vista `TargetView`, que construye game.ts `targetPanel()` hacia la línea 5174.

Del motor:
- `rangedTargets`, `meleeTargets`, `abilityTargets`;
- **`previewShot`** (aim.ts): impacto, crítico, rozadura, `mods` y `critMods` con su texto;
- `abilityDamage`, `noiseOf`.

De game.ts, al presentador: `targetOptions`, `lethality` («Lo mata») y `armorNotes`.

Mientras se apunta, la cámara pasa al hombro (`overShoulder`, `aimShot`) y el blanco se marca en su cuerpo (`highlight`, `setTargetFlag`). Eso es de la cámara y de Arte; el panel solo lo dispara.

**Manda:**
- `ability` (`unit`, `ability`, `target`);
- cambiar de blanco (Tab y Mayús+Tab) y cancelar (Esc), que son del cliente.

**En UI Toolkit:**
- Panel en UXML.
- El dial es un elemento propio con `Painter2D`: un arco de 0–100 % y el número dentro con un `Label`.
- Los modificadores van en dos listas de filas.
- Pegado al botón en la forma «propia»: se coloca con `worldBound` del botón y se recorta a la pantalla, como hoy `placeTarget`.

### 2.5 Avisos de turno y TU TURNO (`setTurn`, `banner`, marco)

**La franja de arriba en el centro:**
- «TU TURNO», «TURNO DE ANA» o «TURNO ENEMIGO», en una pestaña del color de quien juega (el rojo de sello para el enemigo). El número de turno va en la ayuda.
- «EN JUEGO · MATEO» cuando se juega de uno en uno.
- OCULTOS o DETECTADOS.
- La etiqueta de noche, con lo que cambia de noche en la ayuda.

**El marco:** un borde alrededor de toda la pantalla, en el color de quien juega; a trazos si es el compañero. Con la franja, cambian de color la pestaña y la tira sobre las fichas.

**Los avisos** (`banner`, un sello en el centro con sonido de sello):
- TU TURNO;
- TURNO DE ANA;
- TURNO ENEMIGO;
- ¡CONTACTO!;
- OCULTACIÓN ROTA;
- OS HAN OÍDO;
- ¡LLEGAN MÁS!;
- MÁS ZOMBIS EN CAMINO;
- ¡OLEADA!;
- ¡LA HORDA!;
- ¡SE LEVANTA!;
- ¡FUERA DE SÍ!;
- EQUIPO ARREGLADO;
- ¡ALARMA!

Cada uno lleva una línea opcional debajo. «TU TURNO» se queda puesto hasta que se pulsa (`held`, Ana 22/10 #50), o hasta que el jugador hace cualquier otra cosa (`releaseBanner(quiet)`, Arturo 23/10 #8). Mientras está puesto, el reloj de turno no corre (`covering`).

**Lee:**
- de `view`: `turn`, `activeTeam`, `command` (quién tiene el mando), `concealed`, `mission.night`, `activeUnit`, `outcome`;
- los jugadores conectados.

**Eventos** que disparan avisos:
- `turnBegan` (`team`, `command`);
- `commandPassed` (`to`);
- `podActivated` (cómo fue);
- `concealmentBroken` (por qué);
- `noise` (alarma);
- `reinforcementsIncoming`, `spawned`, `hacked`, `psi` (`effect`), `reanimated`.

Los textos de abajo salen de Lore.

**Manda:** nada. El clic en el aviso lo quita, y entonces el reloj de turno vuelve a correr (mensaje `idle`).

**En UI Toolkit:**
- La franja es una fila de `Label`.
- El marco son cuatro elementos absolutos en los bordes con `border-color` variable. No hay `border-style: dashed` en USS (ver §4): el marco a trazos del compañero se hace con una textura repetida.
- El aviso es un elemento centrado que entra con una transición de `scale` y `opacity`. El «golpe de sello» se consigue encadenando dos transiciones (ver §4).

### 2.6 Botones del turno (`.turn-actions`)

Abajo a la derecha.

**TERMINAR TURNO** (Retroceso) tiene seis estados:

| Estado | Texto |
|---|---|
| actuar | TERMINAR TURNO · Retroceso |
| confirmar | ¿TERMINAR? · «2 soldados con acciones» |
| listo | LISTO ✓ · «Esperando a Ana» o «Pulsa para cancelar» |
| esperando | MANDO: ANA · «Puedes planear: clic en una casilla» |
| enemigo | TURNO ENEMIGO · «Espacio: más rápido» |
| apagado | — |

**El reloj de turno** va encima: una barra gruesa para mí y una fina para el compañero. Se mueve mientras corre, se pone ámbar con poco tiempo y rojo al final.

**Los demás:**
- CEDER EL MANDO (C), solo si tengo el mando y el compañero sigue jugando;
- VER A ANA (F), si lo tiene ella;
- IR A MATEO (F), el soldado al que le toca;
- el interruptor «Cámara al soldado», que se guarda en el equipo.

**Lee:**
- de `state`: `ready[slot]`, `command`, quién tiene acciones (`busySlots`, de game.ts), si se puede actuar ahora y cuántos soldados tienen acciones;
- el mensaje `turnClock`: `left`, `total` y `running` de cada jugador.

**Manda:**
- `endTurn` (`ready: true` o `false`);
- `pass`.

«Ver a», «Ir a» y el interruptor son del cliente (cámara y selección).

**En UI Toolkit:**
- `Button` con dos `Label`s.
- El reloj son dos barras con el ancho en `style.width` por fotograma. Es barato: solo cambia un estilo.
- Las rayas en movimiento, con una textura que se desplaza (`background-position` desde C#).

### 2.7 Tarjeta del objetivo (`setMission`, `.goal-dock`)

Arriba a la derecha, bajo Menú. Lleva:
- el tipo de misión (o «Asedio»);
- el objetivo tal como va («Eliminad a todos los hostiles», «Llevad las medicinas a La Lata»…), con la versión larga en la ayuda;
- los turnos que quedan, en rojo si son 3 o menos;
- en el asedio, la próxima oleada;
- en el Saqueo, la barra de la horda;
- VIENEN MÁS si hay refuerzos anunciados.

Un clic vuelve a abrir la tarjeta de llegada.

Debajo, solo en el asedio, va **la rosa de los vientos** (#34). Gira con la cámara y pone en rojo los lados de la próxima oleada, si hay Sala de radio.

En los mapas con plantas va también **el indicador de planta** («Planta 1 de 3», con flechas; RePág y AvPág). Está en la franja de arriba.

**Lee:**
- `mission.kind`, `turnsLeft`, `item`, `objectiveDone`, `alarm`, `objectiveUnit`, `night`, `siege`;
- `reinforcements` (`turn`, `landed`, `pos`, `horde`, `side`);
- `mapName`, `base.pieces` (la radio, `buildingWorks`).

Funciones y textos:
- `itemObjective`, `alarmObjective`, `vipObjective`, `OBJECTIVE_DONE`, `SCAVENGE`, `MISSION_NAMES`;
- `siegeObjective`, `siegeObjectiveTip`, `waveDueShort`, `waveDueLine`, `SIEGE_TEXT`.

game.ts `missionView()` está hacia la línea 4934.

**Manda:** nada (cambiar de planta es del cliente).

**En UI Toolkit:**
- Panel con una fila que se parte en varias líneas (`flex-wrap`).
- La rosa con `Painter2D`, o con un sprite que gira (`style.rotate`), y las letras contragiradas.

### 2.8 Registro (`log`, `.log`)

En la columna de la derecha, bajo el objetivo. Plegado enseña las 2 últimas líneas; abierto, las 30 últimas. Cada línea lleva a la izquierda una raya del color de su tono (daño propio, daño al enemigo, enemigos entre sí, cosas buenas); el texto, en blanco.

**Lee:** solo eventos. Hoy hay 71 llamadas en game.ts. Por evento:
- **Combate:** `damaged`, `died`, `stunned`, `suppressed`, `disoriented`, `psi`, `released`, `reanimated`, `infected`, `cleansed`, `aided`.
- **Turno:**
  - `turnBegan`, `ready`, `commandPassed`;
  - `podActivated`, `concealmentBroken`, `noise`, `spawned`, `reinforcementsIncoming`.
- **Objetivo:** `hacked`, `vipRescued`, `evacCalled`, `evacRefused`, `evacDeparted`, `leftBehind`, `abandonCalled`, `abandonRefused`.
- **Registro y mochila:** `searched`, `roomSearched`, `itemPicked`, `itemDropped`, `lootDropped`, `lootTaken`, `lootGiven`, `medicineUsed`, `weaponEquipped`, `vestWorn`, `beltEquipped`, `cured`, `refund`.
- **Otros:** `barFilled`, `floorBroken`, `fell`, `abilityUsed`.

Los textos vienen de Lore (`barLine`, `biteLossLine` y compañía).

**Manda:** nada.

**En UI Toolkit:**
- Un `ScrollView` con `Label`s; se limita a 30.
- Plegar y desplegar es una clase que cambia la altura máxima.
- Sin ListView: son pocas líneas.

### 2.9 Marcas sobre las unidades (unitViews.ts `renderFlag`, `setTags`; CSS mío)

**Encima de cada unidad que se ve:**
- el nombre (el apodo en los soldados);
- la fila de salud;
- el blindaje;
- el icono de cobertura (alta, baja o flanqueado), si hay un tirador enemigo a la vista o, en el enemigo, frente al soldado elegido;
- las etiquetas de estado: CONTROLADO, VIGILANCIA / ZONA LETAL / HOJA EN ALTO, CUBRIÉNDOSE, PROTEGIDO, ATURDIDO, DESORIENTADO, PÁNICO, MORDIDO, SUPRIMIDO, MARCADO, VULNERABLE, la carga, CAUTIVO, OBJETIVO.

**Al pasar el ratón por una casilla,** sobre cada enemigo, una etiqueta con la probabilidad de impacto desde esa casilla, si lo vería o si quedaría flanqueado (`EnemyTag`: `aim`, `sight`, `flank`).

**En el plano al hombro** se esconden todas menos la del blanco (`.close-shot`).

**Otras marcas:**
- «MOCHILA DE CHIARA» sobre una mochila en el suelo;
- el nombre de quien señala, en un ping;
- los textos flotantes: «−3», «CRÍTICO −5», «ROZADURA», «BLINDAJE −1», «ATURDIDO», la barra que se llena, «¡APARECE UN ZOMBI!»;
- en el asedio, las barras de vida de puertas y barricadas.

Los textos flotantes los hace hoy fx.ts (Arte) y las demás marcas, overlays.ts (mío).

**Lee:**
- por unidad, lo mismo que la lista de la escuadra, más `armor`, `aided`, `suppressedBy`, `marked`, `ruptured`, `objective`;
- la cobertura: `coverAgainst`, `coverSides`, `hasAnyCover`, `sightOrigin` y `sightTo` del motor; `shootsFromRange` es de game.ts;
- las etiquetas al pasar el ratón: `previewShot` desde la casilla y `canSee`.

**Eventos:** `damaged` (cantidad, mitigado, `shred`), `died`, `stunned`, `barFilled` (habilidad, crecimiento), `spawned`.

**Manda:** nada. El clic derecho sobre un enemigo abre su ficha (2.13).

**En UI Toolkit:**
- Una capa de pantalla completa con un elemento por unidad.
- Cada fotograma, la posición sale de `RuntimePanelUtils.CameraTransformWorldToPanel(panel, cabeza, cámara)` y se aplica con `style.translate`, que no rehace la maquetación.
- Hay que esconder la marca si la unidad queda detrás de la cámara o fuera de pantalla.
- Si la versión de Unity que se instale trae paneles de UI Toolkit en el mundo, podrían ir ahí. Está por comprobar con el editor.
- Los textos flotantes, igual: un `Label` que sube y se desvanece con una transición.
- **Coste:** con 20–40 unidades se mueve un estilo por marca y fotograma. Hay que medirlo en el Profiler contra los 4,2 ms.

### 2.10 Llamadas del compañero (`showCall`, tarjeta central)

«Ana quiere evacuar» o «Ana quiere abandonar la misión». Lleva:
- el texto;
- el tiempo que queda y qué pasa cuando se acaba;
- Aceptar y Rechazar.

Mientras está, el reloj de turno no corre.

**Lee:** el estado de la evacuación y del abandono (`evacWaiting`, la llamada de abandono con quién la hizo) y el mensaje `deadline` (`evac` o `abandon`, con los ms que quedan).

**Eventos:** `evacCalled`, `evacRefused`, `evacDeparted`, `abandonCalled`, `abandonRefused`.

**Manda:** `evacAnswer` (`accept`) o el mensaje `abandonAnswer` (`accept`).

**En UI Toolkit:** una tarjeta modal en el centro con una cuenta atrás (`Label` actualizado una vez por segundo).

### 2.11 Tarjeta de llegada y resultado (`intro`, `showOutcome`)

**Tarjeta de llegada:** al empezar, la operación, el nombre, «Objetivo: …», los turnos y la línea del informe. Se cierra con «Entendido», Intro o Espacio, y al cerrarse se encoge hacia la tarjeta del objetivo.

**Resultado:** «MISIÓN CUMPLIDA» o «MISIÓN FRACASADA», con una línea. En campaña, VOLVER A LA BASE. En escaramuza:
- OTRA IGUAL (MAPA NUEVO);
- MISIÓN AL AZAR;
- VOLVER AL MENÚ.

**Lee:**
- el nombre de la misión y del mapa, el objetivo, los turnos y el informe (textos de Lore y de la oferta);
- `outcome` y el evento `missionEnded`.

**Manda:** los mensajes `newMission`, `returnToBase` y `backToMenu`.

**En UI Toolkit:** dos tarjetas modales. La que se encoge hacia la esquina, con transiciones de `translate` y `scale`.

### 2.12 Notas pequeñas

- **`toast`:** mensajes breves abajo en el centro (45 llamadas). Lo que el servidor rechaza (`rejected`, con su texto) y avisos del cliente: recargar, esperar, ruido, casilla no válida.
- **`notice`:** algo que hizo el compañero, con «Ver (F)», que lleva la cámara allí.
- **`packOffer`:** «No cabe: lo dejas en el suelo» con «Cambiar», cuando la mochila está llena.
- **Conexión:** el estado de la red (conectado, reconectando, perdido).

**Lee:** los mensajes `rejected` y `error` del servidor, los eventos del compañero (`by`) y el estado del socket.

**En UI Toolkit:** una pila de notas abajo en el centro y un indicador de red en una esquina.

### 2.13 Para después del hito 1

- **Menú (Esc):** las teclas (`CONTROLS` en hud.ts), los ajustes de gráficos y de sonido, abandonar, volver al menú y salir.
- **Hoja del soldado (H):** las cifras, lo que lleva y el tablero de habilidades (hexBoard).
- **Ficha del enemigo** (clic derecho): qué es, qué hace y un consejo (textos de Lore).
- **Tiempo real** (`setRealtime`, PAUSA, CÁMARA LENTA): era un prototipo de escaramuza. No se pasa.

Para el hito 1, del menú basta lo mínimo: las teclas, abandonar y salir.

## 3. Lo que el motor de C# tiene que darme

### 3.1 Estado

**`GameState`:**
- `turn`, `activeTeam`, `command`, `ready[2]`, `activeUnit`, `concealed`, `outcome`;
- `units`, `dropped`, `width`, `height` y las plantas;
- `base.pieces` (asedio), `mapName`.

**`mission`:**
- `kind`, `turnsLeft`, `night`, `siege`;
- `item` (`pos`, `carrier`), `objectiveDone`, `objectiveUnit`, `alarm`, `terminal`, `evacZone`;
- `reinforcements` (`turn`, `landed`, `pos`, `horde`, `side`).

**`Unit`:**
- **Identidad y dueño:**
  - `id`, `name`, `nickname`, `team`, `owner`, `template`;
  - `alive`, `evacuated`;
  - `pos`, `rank`, `appearance.sex`.
- **Cifras:**
  - `hp`, `maxHp`, `ap`, `armor`;
  - `ammo`, `mags`, `pistolAmmo`, `charges`;
  - `mods` (`aim`, `gunAim`, `meleeAim`), `cooldowns`.
- **Armas y equipo:**
  - `arms` (`primary`, `melee`, `mods`), `weaponName`, `weaponTier`;
  - `worn`, `gear`, `backpack`, `packSlots`.
- **Experiencia:** `lines`, `deeds`.
- **Estados:**
  - `overwatch`, `killZone`, `watchMelee`, `hunkered`, `aided`, `stunned`;
  - `disoriented`, `panicked`, `infected`, `suppressedBy`, `marked`, `ruptured`;
  - `controlledBy`, `captive`, `objective`, `automaton`.

### 3.2 Consultas (funciones puras sobre el estado)

Solo las del motor; las he comprobado una a una en `packages/engine/src`.

| Para | Funciones del motor |
|---|---|
| Barra de acciones | `abilityBlocker` (con su texto), `attackWeapon`, `apPerTurn`, `clipOf`, `hackChance`, `isAttack`, `searchSpot`, `reachesToSearch` |
| Panel de disparo | `rangedTargets`, `meleeTargets`, `abilityTargets`, **`previewShot`** (impacto, crítico, rozadura y modificadores con su texto), `abilityDamage`, `noiseOf` |
| Marcas | `coverAgainst`, `coverSides`, `hasAnyCover`, `canSee`, `sightOrigin`, `sightTo`, `visibleEnemies(s, 'xcom')` |
| Mochila | `lootTotals`, `packSlots`, `wearable`, `beltable`, `inMeleeReach`, `conditionOf` |
| Objetivo | `buildingWorks`, `liveSkills` |
| Textos (Lore) | `magsCount`, `evacWaiting`, `itemObjective`, `alarmObjective`, `vipObjective`, `siegeObjective`, `waveDueShort`, `waveDueLine`, `findLabel`, `lootName` |
| Tablas | `TEMPLATES`, `WEAPONS`, `FOUND_WEAPONS`, `ITEMS`, `ABILITIES` (nombre, coste, descripción, tipo de blanco, acciones, ráfaga), `VETERANCY`, `MISSION_NAMES`, `SKILLS` |

**No son del motor, son ayudantes de game.ts:** `soldierAbilities`, `abilityList`, `abilityStats`, `damageText`, `limitsText`, `noiseText`, `crateBeside`, `evacActions`, `abandonActions`, `targetOptions`, `lethality`, `armorNotes`, `shootsFromRange`, `busySlots` y `haulText`. Pasan al presentador del HUD en C#: los escribo yo sobre las funciones de arriba.

Si alguna función devuelve texto en español, ese texto es de Lore, como hoy.

### 3.3 Eventos

Los de §2.5, §2.8, §2.9 y §2.10, con los mismos campos que hoy (`events.ts`, 75 tipos). El HUD no necesita eventos nuevos.

### 3.4 Órdenes y mensajes de red (protocol.ts)

| Sale del HUD | Qué es |
|---|---|
| `move` (`unit`, `to`) | mover (desde el mapa, no desde el HUD, pero es el mismo flujo) |
| `ability` (`unit`, `ability`, `target` o `tile`) | habilidades, disparo, granadas |
| `endTurn` (`ready`) | Terminar turno, y cancelarlo |
| `pass` | ceder el mando |
| `evac` (`unit`), `evacAnswer` (`accept`) | evacuar y responder |
| `item` (`unit`, `action`, `index`, `id`, `to`) | mochila |
| mensaje `abandon`, `abandonAnswer` | abandonar y responder |
| mensaje `ping`, `plan`, `presence` | señalar, planear sin el mando, qué mira cada uno |
| mensaje `idle`, `away` | que el reloj de turno no corra mientras se mira un aviso o el juego no está a la vista |
| mensaje `newMission`, `returnToBase`, `backToMenu` | tras el resultado |

| Llega al HUD | Para |
|---|---|
| `snapshot`, `events` (`seq`, `by`) | todo |
| `room` (`players`: `name`, `color`, `connected`, `away`) | jugadores, colores, quién está |
| `turnClock` | el reloj |
| `deadline` | la cuenta atrás de las llamadas |
| `rejected`, `error`, `closing` | notas |
| `presence`, `ping`, `plan` | lo que hace el compañero |

### 3.5 Textos

Todo lo que se lee sale de Lore (`engine-cs/Runtime/Lore`). Además, hud.ts tiene hoy sus propios textos fijos: TERMINAR TURNO, ¿TERMINAR?, LISTO ✓, CEDER EL MANDO, los estados de las fichas, la lista de teclas… Propongo pasarlos a Lore en C# con el resto, en una clase de textos del HUD, para que Lore los revise en un solo sitio (pregunta 4).

## 4. CSS frente a USS: lo que hay que hacer de otra manera

Por lo que conozco, UI Toolkit tiene maquetación flexbox (Yoga), USS con variables (`var(--x)`), pseudoclases (`:hover`, `:active`, `:disabled`), transiciones y `rotate`, `scale` y `translate`.

**Lo que no tiene, y cómo lo resuelvo:**

| Hoy (CSS) | En UI Toolkit |
|---|---|
| `display: grid` (tarjeta de habilidad, cifras) | filas y columnas con flex |
| `box-shadow`, bisel y placas de acero | sprites de 9 partes (`-unity-slice-*`) hechos con nuestras texturas de `public/ui/mixta` |
| `clip-path` (hexágono), `conic-gradient` (tarta del hexágono, arco del dial) | un elemento propio que dibuja con `Painter2D` en `generateVisualContent` |
| `@keyframes` (sello que cae, latido de lo urgente, rayas del reloj) | transiciones encadenadas con clases (`TransitionEndEvent`) o animación desde C# (`schedule.Execute`, `experimental.animation`) |
| `border-style: dashed` (marco del compañero) | textura repetida |
| `zoom: var(--ui-scale)` (el tamaño de la interfaz de Ajustes) | `PanelSettings.scale` |
| `title` (ayudas al pasar el ratón) | una capa de ayuda propia: en tiempo de juego UI Toolkit no enseña `tooltip` (solo en el editor). Un `TooltipLayer` que escucha `PointerEnterEvent` y `PointerLeaveEvent` y coloca una tarjeta |
| iconos con `mask` y color (`::before`) | `background-image` con el sprite y `-unity-background-image-tint-color` |
| sombra de texto | `text-shadow` de USS, o `-unity-text-outline` |
| etiquetas CSS2D de Three.js | §2.9: mundo a panel en cada fotograma |
| fuentes con `@font-face` y `unicode-range` | Font Asset de TextCore para Oswald y Barlow (OFL, desde sus TTF), y el «§» como fuente de reserva en la tabla de fallback. El script del «§» tiene que sacar un TTF, no solo woff2 |

**Más cosas que hay que cuidar:**
- **Clics al mapa:** los contenedores que no son botones van con `pickingMode = Ignore`, para que el clic llegue al mundo. El juego pregunta `panel.Pick(posición)` antes de tratar un clic como clic en el mapa.
- **Rendimiento:** UI Toolkit rehace la maquetación y las mallas solo de lo que cambia. Hay que seguir con lo de hoy: comparar la vista y no tocar nada si es igual. Lo que se mueve cada fotograma (marcas, reloj, rosa) solo cambia `translate`, `width` o `rotate`.
- **Escala:** `PanelSettings` con «Scale With Screen Size» y una resolución de referencia; encima, el tamaño de Ajustes.
- **Iconos:** los sprites de los paquetes INTERFACE de Synty, que hoy convertimos a webp, en Unity se usan tal cual. La tabla habilidad → icono (`synty-icons.css`, `icons.ts`) pasa a un ScriptableObject o a una tabla en C#.
- **Sonidos de la interfaz** (sello, confirmar, papel): hoy `uiSound` (Red). En Unity hace falta una llamada de C# equivalente.

**Estructura propuesta en el proyecto:**

```
unity/Bastion/Assets/Bastion/UI/
  Fonts/            Oswald, Barlow y el «§» (Font Assets)
  Theme/            Mixta.uss (variables --t-*), sprites de 9 partes
  Hud/              un UXML y un USS por pieza: SquadList, SquadCard, UnitPanel, ActionBar,
                    AbilityButton, AbilityCard, ShotPanel, TurnStrip, Banner, TurnActions,
                    GoalCard, Compass, Log, UnitFlags, CallCard, Intro, Outcome, Notes
  Scripts/Hud/      una clase por pieza (Set(vista)), HudRoot (UIDocument), TooltipLayer,
                    los dibujos con Painter2D (SkillHex, HitDial, CompassRose)
  Scripts/Present/  HudPresenter: las vistas a partir del estado (lo de game.ts)
  Lab/              una escena con vistas inventadas para ver y capturar cada pieza sin partida
```

**Cómo lo probaría sin abrir el editor:**
- Una escena de laboratorio (como hoy `src/lab`) que monta el HUD con vistas inventadas: cada estado de cada pieza.
- Capturas en batchmode con la herramienta de Arte.
- El presentador se prueba con estados de las partidas grabadas (`engine-cs/golden`): para un estado dado, la vista esperada.

## 5. Orden de trabajo propuesto

1. **Sin editor** (ya): este inventario. Después, los UXML, los USS y las clases de C# con vistas inventadas, que se pueden escribir sin compilar.
2. **Con editor y Font Assets:** el laboratorio con las piezas del hito 1:
   - lista de la escuadra, panel del soldado (sin mochila), barra de acciones con su tarjeta, panel de disparo;
   - franja de turno, avisos y marco, botones del turno con el reloj;
   - tarjeta del objetivo, registro, marcas sobre las unidades;
   - tarjeta de llegada, resultado y notas.
   Capturas de cada una para Gameplay.
3. **Con el motor de C# (pasos 1–2 de Gameplay):** el presentador sobre el estado de verdad, primero con estados grabados.
4. **Con la red de C# (paso 3 de Red):** las órdenes y los mensajes. Prueba entre dos apps.
5. **Después del hito 1:** la mochila, las llamadas de evacuar y abandonar si no han hecho falta antes, el menú completo, la hoja del soldado, la ficha del enemigo y la rosa del asedio.

## 6. Decidido con Gameplay (23/10)

| Pregunta | Decisión |
|---|---|
| Marcas en el mapa (overlays.ts) | **UI**, con mallas en URP. Arte revisa el aspecto. |
| Textos flotantes | **UI**, como etiquetas en la capa de marcas. Arte se queda las partículas y los efectos de fx.ts. |
| Controlador del combate (elegir, mover, apuntar, teclas) y la cámara | **UI**, con el Input System. «La cámara nunca pelea con el jugador» sigue valiendo. |
| Textos fijos del HUD | Donde están hoy: los de hud.ts y game.ts, en una clase de UI (`HudText`) que Lore revisa; los de lore.ts los pasa Lore. |
| Paneles de UI Toolkit en el mundo | Se mira con la versión instalada. |
| El «§» como TTF | UI. |

**Mientras no haya editor:**
- Los UXML y los USS de cada pieza, y las clases de C#, con vistas inventadas, en `unity/Bastion/Assets/Bastion/UI/`. Sin compilar: lo que no compile se rehace con el editor.
- El proyecto de Unity (`ProjectSettings`, `Packages`) lo crea Arte.
- Las vistas pasan a los tipos de verdad del motor (`engine-cs/package/Runtime/Types/`) cuando estén.

## 7. Estado (26/10)

En `unity/Bastion/Assets/Bastion/UI/` (asmdef `Bastion.UI`). Compila en el proyecto con `unity/tools/unity.sh`. Detalle en su [README](../unity/Bastion/Assets/Bastion/UI/README.md). Capturas en `.dev-snapshots/ui/unity/`.

| Hecho | Pieza |
|---|---|
| sí | Vistas (`HudViews`), textos fijos (`HudText`), manejadores (`IHudHandlers` y su relevo), ayudas al pasar el ratón (`TooltipLayer`) |
| sí | Lista de la escuadra, panel del soldado (con la mochila y las marcas del arma), barra de acciones con menús y tarjeta, panel de disparo (ataque, apoyo, propia sobre su botón) |
| sí | Franja de turno con marco, jugadores y planta; aviso (sello, retenido); botones del turno con los relojes |
| sí | Tarjeta del objetivo y registro; la rosa del asedio con los lados de la próxima oleada en rojo si hay Sala de radio (`CombatController.Siege`) |
| sí | Marcas sobre las unidades y textos flotantes (del mundo al panel en cada fotograma) |
| sí | Tarjetas de llamada, llegada y resultado; notas |
| sí | Dibujos con Painter2D: hexágono de habilidad partido, dial de impacto, aguja, triángulo |
| sí | Fuentes (Oswald y Barlow, Font Assets estáticos), iconos de Synty (57), placa de acero, PanelSettings con su tema |
| sí | Pantalla de inicio, sala, `AppFlow` (escenas Inicio y Partida, aviso de conexión, anfitrión que cierra) |
| sí | Cámara (`CameraRig`: Q/E, WASD y flechas, rueda, arrastre con el botón derecho, muelle, `Epoch`) |
| sí | Controlador (`CombatController`: Tab y Mayús+Tab, Esc, clic para elegir o mover, puntero por `MapSpace.Pick`) |
| sí | Marcas del movimiento con el aspecto «Hueso y óxido»: alcance de una y dos acciones, camino, anillo del puntero, escudos, marcos de «te verían», anillos del elegido y del compañero |
| sí | Presentador (`CombatController.Hud`): turno, jugadores, escuadra, panel del soldado, objetivo, asedio, panel de disparo, llamadas, banderas con cobertura y líneas al pasar el puntero; con las consultas de verdad del motor |
| sí | Barra (`CombatController.Bar`) con sus menús, y modos y órdenes (`CombatController.Orders`): blanco, lanzar, a mano, propia, Terminar turno, mando, evacuar, abandonar |
| sí | Marcas del disparo y de lanzar (`AimOverlay`): anillo del blanco, línea a un aliado, área de la granada con su centro y las paredes que rompe, alcance de lanzar, lo que se hace a mano |
| sí | Red en Partida (`CombatSession`): sala, snapshot (con `Battlefield.Show` de Arte), eventos, presencia del compañero, plazos, relojes, `idle` |
| sí | Reproductor de eventos (`EventPlayer`): `View` por detrás de `State`, las animaciones y efectos de Arte (`UnitViews`, `Fx`), registro, textos flotantes, sellos, momentos del compañero, resultado; tarjeta de llegada y plano de apertura |
| sí | Planta que se ve (`MapView.ShowLevel`) y cabezas y pies de las figuras (`Figures`) |
| sí | En el reproductor: el ritmo del turno enemigo (pausa entre actores, cámara a cada uno, resultado leído), los zombis que andan juntos, llegadas y contactos a la vez, una explosión con sus golpes a la vez, la velocidad del turno enemigo con la barra espaciadora (`Time.timeScale`), el plano al hombro (`ShotCam`), el acercamiento de cada acción, los temblores, el anillo del ruido |
| sí | Los paseos de los zombis repartidos para que nadie ande a través de nadie (`WalkPlan`, de walkPlan.ts), con las esperas de `MoveAlong` |
| sí | Cono de visión de un enemigo y el ojo en su bandera; marcas de la misión (evacuación, balizas, objeto, bengalas) con flechas en el borde de la pantalla; notas de la casilla bajo el puntero |
| sí | Planes sin el mando (se ven los del compañero y se recorren al llegar el mando) y señalar un sitio (G, Mayús+clic, clic con la rueda); Mayús+rueda cambia de planta |
| sí | Menú del juego (Esc o «Menú»): Continuar, Ajustes (velocidad del turno enemigo y de los soldados, seguir las rutas planeadas, tamaño de la interfaz: `GameSettings.ApplyInterface`, la resolución de referencia del panel entre el tamaño), Controles con las reglas de la noche, y volver al menú, abandonar y salir, cada uno con su pregunta |
| sí | El camino (y los planes) sobre los peldaños de una escalera, a la altura de la 0.10.1 (`MapSpace.StairLift` de Arte, la misma con la que pisan las figuras); los iconos de puertas, dibujados con Painter2D (`LineIcon`); el «§» como la caja de suministros (`BastionSuministros.ttf`, fuente de reserva de Oswald y Barlow); las notas y las líneas de las banderas no se salen de la pantalla |
| sí | Rendimiento: telemetría `[rend …]` al red.log del anfitrión cada 10 s (`Perf`, de perf.ts; la cpu es el trabajo del hilo principal, con la espera de la pantalla aparte), y en Ajustes Gráficos (Altos, Medios, Bajos: los niveles de calidad de Arte), Límite de FPS y Mostrar FPS (`GameSettings`); fuera del combate, 60 fps como mucho; en el Mac, el combate sin límite va sin vsync, limitado a la frecuencia de la pantalla (60 si no la da), porque con vsync se colgaba si la pantalla se dormía (0.11.4, `GameSettings.Pacing`; la telemetría dice «tope 144 Hz sin vsync»; comprobación: `FramePacing.Check`); `BASTION_PERFILAR=<fotogramas>` saca al registro los marcadores más caros (`Perfil`) |
| sí | El suelo del mapa (en `Client/Map`, sobre MapSpace y el catálogo de Arte): `GroundMap` y `Bastion/Ground` (ground.ts: losas, bordillos, césped, asfalto con línea central, pasos de cebra, grietas, musgo; el campo y las instalaciones; el agua del canal), lo menudo (`GroundCover`: scatterCover con los mismos hashes, alcantarillas, sumideros, islas de surtidores) y la sombra de contacto horneada (`GroundOcclusion`, de groundAO.ts) |
| sí | El marco fuera de juego (`FrameArt`, `FrameView`: frameArt.ts con lo que usa de dressing.ts; el canal, la vía, la autovía, el agua y el barranco), las calles vestidas (`StreetDressing`, de dressStreets: farolas, postes y cables, señales, bocas de riego y buzones, bolsas; `GroundView.Lamps` para las luces de noche de Arte), y el foco del corte de edificios de Arte desde el controlador |
| sí | Ajustes en dos columnas (el sonido; los gráficos y lo del juego), bajo la franja de arriba y con desplazamiento si no cabe: «Sonido» y los cinco volúmenes sobre `Bastion.Client.Audio.Volumes` de Red, y la tecla M en combate (se oirán cuando llegue su motor de sonido) |
| sí | El compañero que se va o vuelve (game.ts setPlayers, commandChanged): cuenta como presente durante la gracia del servidor (`Room.ReconnectGraceMs`, `AwayGraceMs`); pasada, «Ana no está: mandas también sus soldados.», y al volver «Ana ha vuelto: cada uno manda en lo suyo.». Mayús o Alt y la rueda cambian de planta, también la rueda de lado que manda el Mac con Mayús, una planta por gesto. Laboratorio: `join`, `drop`, `gone`, `wheel=±1`, `wheelx=±1` |
| sí | La base contra la 0.10.1 (26/10, con Lore): «ESPERAR A QUE SE CUREN» en Soldados cuando nadie puede salir (`Campaign.MustWait`); el estado de la hoja que baja de renglón; Encargos abre la misión de su paso; Esc cierra el desplegable de la hoja y Personalizar antes que el menú, y no hace nada con un aviso o un acontecimiento abierto; la tecla B (Soldados ↔ Asentamiento); los parpadeos (`StrategyRoot.Blink`: la placa de propuesta cuando el compañero propone o se pone a punto, la fila y el título a los que lleva «Ver» o una fila del Mapa) y el latido de «Confirmar el tratamiento» (`Pulse`, el borde inclinado en rojo). Laboratorio: `muestra-esperar`, `-esperar-pedido`, `-mapa-encargo`, `BASTION_ESTRATEGIA_PULSA`, `_PARPADEO`, `_LATIDO`; BaseRun `esc`, `b`, `choice:` |
| sí | El selector de arma de la hoja (arms.ts weaponPick): cada opción con su icono, sus marcas («Gastada», «+2»), la nota y la diferencia en verde y rojo con lo que lleva; la ayuda de las del compañero; «No hay armas en la Armería.» sin ninguna. Laboratorio: `BASTION_ESTRATEGIA_DESPLEGABLE=hand|belt` |
| sí | «Ver» de una propuesta de colocar, poner una fila, talar, reparar, reponer, desmontar o mejorar: el Asentamiento y la cámara del claro allí, con su resorte (`FocusClearing` sobre `Settlement.TileTarget`/`PieceTarget` de Arte; si la cámara de Arte aún va hacia el plano, al tomarla el rig). Sellos de las tarjetas: «A PUNTO» en las del jugador que lo está y «CAÍDO» en las de los caídos. Esc en el tablero con una casilla pulsada suelta la compra, no el tablero. BaseRun: `see-tile`, `see-piece`; laboratorio: `muestra-escuadra-listo`, `-parte-caido` |
| sí | Ayudas de la base (cruce del 26/10): en la Armería, por qué está apagado «Quitar», «Poner…» o «Ponérsela» (o qué hace la mejora), «No hay mejoras sueltas…» y un «Hueco libre» por cada hueco vacío; en el Diario, cada cosa entera al pasar el ratón y los iconos propios de los Avances; el antetítulo de un encargo; el resalte de la zona bajo el puntero en el Mapa; en el menú de la base, la ayuda de «Volver al menú principal» y la nota de Ajustes («Solo para ti: Ana sigue a lo suyo.» o «Se guardan en este ordenador.»). BaseRun: `menu[:settings]` |
| sí | Ayudas de Soldados y Personalizar (cruce del 26/10): el nombre entero con apodo sobre el retrato, el código de cada muestra, «Que vuelva a ir como…», «Una al azar, y no cambia.», «Vuelve al aspecto de serie…», «Guarda a este soldado y pinta…» (o cómo activar los colores) y la de la marca de jugador; las de botones apagados, sobre un envoltorio |
| sí | Ayudas vistas en captura (26/10): `BASTION_ESTRATEGIA_AYUDA=clase[:n|:off]` abre la del elemento (el n-ésimo, o el primero con la tecla apagada) con `TooltipLayer.Pin`, que la deja abierta pase lo que pase con el puntero del editor; `muestra-armeria-arma-ana`, con Ana conectada (las teclas del arma de Rafael, apagadas). Arreglo de paso en `TooltipLayer`: la ayuda la guarda el propio elemento (`ConditionalWeakTable`), así que no se pierde si el elemento sale del panel y vuelve (las capturas cambian el panel del documento, y la franja y la Armería se quedaban sin ayudas); se coloca otra vez cuando cambia su tamaño, y si no hay nada que enseñar se oculta. Capturas en `.dev-snapshots/ui/unity/estrategia/ayuda-*.png` |
| — | El Mapa estrecho de la 0.10.1 (plegar la columna) no hace falta: a 1280 con Interfaz al 130 % la columna cabe y se desplaza, y el valle se encaja en lo que deja (`ValleyMap.Fit`, con la zona elegida a la izquierda de la hoja) |
| sí | «Ajuste auto» (settings.ts QualityGuard y autoQualityRow de la 0.10.1), sobre los niveles de Gráficos: `App.QualityGuard` mira el combate con las mismas ventanas (2,5 s de arranque; ventanas de 3 s por debajo de 45 fps los primeros 25 s, luego de 8 s, dos seguidas por debajo de 38; fuera los tirones de más de 250 ms y el 10 % de cada punta) y baja un nivel cada vez (`GameSettings.LowerQuality`, `ShownLevel`), guardado entre combates. No toca el nivel si el jugador lo eligió en Ajustes, con la app sin foco ni con un límite por debajo de 45. Avisa una vez por combate («Calidad ajustada para ir más fluido.» y la línea del registro). La fila va al final de Ajustes, con Sí/No, la ayuda de cada uno y la nota «Bajado para ir fluido: gráficos bajos.», que se va al cambiar la fila o los Gráficos. En la telemetría: «auto: bajos» o «sin ajuste auto». Prueba en el editor: `BASTION_LENTO=ms` (fotogramas lentos en `CombatLab`, sin foco y sin guardar nada) y los toques `ajustes` y `autono`. Capturas en `.dev-snapshots/ui/unity/ajustes/ajuste-auto-*` |
| sí | El registrado en la misión (26/10, con Arte): `EventPlayer` rehace el mapa también con `SearchedEvent` y `RoomSearchedEvent` (MapBuilder lo pinta como mapView.ts searchedSeen). Arreglo de paso: a `MapView.Rebuild` va una copia de la vista (`Events.CloneState`), porque los eventos cambian la vista en su sitio y, desde el segundo cambio, el mapa comparaba la vista consigo misma y no rehacía nada (puertas, suelos, registros). Laboratorio: toques `caja` y `sala`. Capturas en `.dev-snapshots/ui/unity/registrado/` |
| sí | Del cruce de Arte con mapView.ts y unitViews.ts (26/10): las marcas sobre las cajas de botín (`CombatController.Loot`, lootMarks.ts y markLoot: icono en placa oscura con el aro de su color, a la escala del mapa; solo si ya se vio la caja, con un soldado a 4 casillas o menos y no en una planta cortada; `Fog.Explored`, de Arte) y el anillo rojo bajo cada enemigo a la vista (`MoveOverlay.ShowEnemies`: 0,33–0,46 de casilla, #ff4d4d, sigue los pies). Toque de laboratorio `botin`. Capturas en `.dev-snapshots/ui/unity/botin-y-anillos/` |
| sí | Con Arte (26/10): en el plano al hombro, quien tapa sale del plano (`CombatController.ClearView`, unitViews.ts clearView con sus medidas ×1,5; Arte `UnitViews.Veil`, por ahora al momento, el tramado de 0,2 s después). Las llegadas (`Arrivals`, arrivals.ts): el camino de cada uno desde el borde (`Paths.ComputeReach`, 7 casillas), los más hondos primero, `WalkPlan.PlanHasted` con sus ritmos (0,42 s por casilla los muertos, 0,16 los vivos, 2,2 s como mucho), el polvo en el borde, los faros del primer vivo, y quien no tiene camino se levanta del polvo; Arte `UnitViews.Arrive`, `Rise` y `Fx.Headlights` (por ahora mínimos). Laboratorio: `BASTION_LAB_ESTORBO=1` (alguien en la línea de tiro) y los toques `llegada` y `llegada-dentro` (los que se levantan). Capturas en `.dev-snapshots/ui/unity/hombro-y-llegadas/` |
| sí | Repaso de Arte (27/10): las copas de los árboles se apartan sobre lo que se mira (`TreeFade`, treeFade.ts: por árbol, la línea de cada punto hacia la cámara, hasta 0,88; mientras, sus materiales en `Bastion/Crown`, tramado por encima del tronco y con su sombra entera) y un poste por tronco en la oclusión del suelo (`GroundOcclusion`, TRUNK_OCCLUDER). El cielo (`Sky`, sky.ts): la cúpula, las montañas y los tejados de ciudad o fábrica (`Sky.Land` desde `GroundView`), los anillos de nubes (MapCatalog de Arte) y sus colores por humor, leídos de `Mood.Current.Now`; ni en cámaras ortográficas (el valle) ni en el estudio de los bustos. La niebla de Arte deja el cielo fuera (una línea en Fog.shader). Revisión de la interfaz al 90 y al 130 % en 1280×720: la alarma de la franja cede sitio a «MENÚ», y Ajustes del combate reparte sus filas para caber sin desplazar. Capturas en `.dev-snapshots/ui/unity/copas-y-cielo/` |
| sí | B5 y B6, la oclusión de los suelos de las casas (27/10). Regla de la 0.10.1: en sus alturas no entran las tapas de los muros (buildings.ts: fuera la clave `cap`), y como sus muros son planos de pie, de un muro solo quedan los dinteles de puertas y ventanas. Los props tapan solo si dan sombra (mapView.ts: `occlude = shadows`), y el contorno del mapa no tapa. En Unity: `GroundBake` descarta las tapas (`TEXCOORD5.y` = 1, el `AddCap` de WallsView); `GroundOcclusion.Occluders` salta lo que no da sombra y el «Marco», y deja de saltar los muros de la clínica (la línea «clínica» compensaba las tapas). Casilla media del mapa entero, 0.10.1 / Unity antes / Unity después: casas de city-7 0,61 / 0,34 / 0,61; tiendas 0,60 / 0,52 / 0,59; clínica 0,69 / 0,78 / 0,69; calle 0,88 / 0,74 / 0,88. Fila 48 de la sala de la tele, igual al centésimo. Volcado: `MapLab` con `BASTION_AO_VOLCADO=archivo`. Capturas en `.dev-snapshots/ui/unity/oclusion/` |
| sí | Notas de Ana (28/10), la parte de UI:
- **Teclas.** Los números iban por `Key.Digit0 + n`, y en el Input System los dígitos van del 1 al 9, luego el 0, y detrás Mayús, Alt, Ctrl y Cmd. Por eso Mayús hacía de «1» y los números no hacían nada. Con Cmd o Ctrl nada del juego responde: combate, cámara, base y claro de construir (`App/Shortcuts`). Al soltar Cmd se sueltan las teclas que macOS deja pegadas. Mayús sola no suelta «TU TURNO». Los menús de la barra salían sin iconos porque `JsonUtility` deja los textos nulos en "".
- **Barras de vida.** Cuelgan de los pies a la mayor altura de la cabeza (0.10.1: de `rig.root`); del hueso de la cabeza giraban con el cuerpo.
- **Pausa con Esc.** El menú del juego, abierto en una misión en curso, la para para los dos (`CombatController.PauseTick`, `NetLink.Pause`, `PlayerInfo.Paused`). El tiempo se para (`EventPlayer.SetHalted`, `timeScale` 0) y no sale ninguna orden menos la respuesta a evacuar. Quien lo abre ve la pantalla tapada; el otro, un cartel con su nombre.
- **Árbol de habilidades.** Como `hexBoard.ts`/`.css`: sin aro naranja (solo el blanco del teclado), la luz blanca que recorre el borde al mantener, la ola de 7 unidades con su sombra (las celdas de fuera primero), los colores por estado y el rebote al comprar.
- **Tamaño.** La escala del HUD y de las líneas del mapa no pasa de la densidad de la pantalla (`GameSettings.PixelScale`: 2, 1,5 o 1 según `Screen.dpi`), como los píxeles CSS de la 0.10.1. En un monitor grande todo salía un 69 % mayor.
- **«TU TURNO».** Placa de acero de Mixta entre franjas de peligro de Synty (`UI/Textures/hazard-*`), recta, y no el sello de papel.
- **Barra.** Las teclas apagadas, opacas y oscuras (UI Toolkit las ponía a media opacidad). Lanzar con la roca de la 0.10.1 (`UI/Textures/Icons/r-roca`) y la granada siempre en su menú, apagada si no hay («No le quedan granadas.»). Las teclas como las de Mixta (mixta-hud.css `.ability`, sí de Gameplay): 60 px y 5 de separación; el icono, de 26 × 28 (las armas de lado, 40 de ancho), en un pozo redondo con su aro, ámbar al pasar y al elegir; un ▲ en las que abren menú (`Triangle`); y en las apagadas, sin pozo y con rayas finas (`Draw/Stripes`). Como USS mezcla en espacio lineal, los alfas se calcularon para que los píxeles salgan como en la 0.10.1 (el negro del pozo, 0,63; las rayas, opacas).
- **Jugando sola.** Sin marco del turno ni lista de jugadores.
- **Brújula.** Del asedio, con sus letras también en los bordes de la pantalla.
- **«Quedan 3 turnos».** Textos de Lore, con el aviso fijo sobre el soldado.
- **Escudo de cobertura.** La propia solo sale si se ha visto a alguien que dispara o la llegada anuncia vivos (`CoverMatters`).
- **F12.** Guarda una captura en Imágenes/Bastión (`App/Screenshots`; para pruebas, `BASTION_CAPTURAS`).
- **Laboratorio.** CombatLab tiene un reloj propio para los toques (sigue en pausa) y toques nuevos: `key=`, `keep=`, `giro`, `pausa-otro`(`-fin`), `turnoenemigo`, `quedan=`, `sin-granadas=`. En estrategia, `BASTION_TABLERO_RATON` (`celda[:mantener]` o `cruce`); en general, `BASTION_DENSIDAD`.
- **Capturas.** Junto a la 0.10.1, en `.dev-snapshots/ui/unity/notas-ana/`. |
| sí | Las copas también se apartan sobre los enemigos a la vista (Gameplay, 28/10: en el asedio los muertos entran por las veredas entre los pinos y no se veían hasta llegar al claro; la 0.10.1 tampoco lo hacía). En todos los mapas. `TreeFade.Looked` añade cada enemigo vivo cuya figura está en el mapa (`UnitViews.Shown`, una línea en el archivo de Arte). Para que salga barato: los árboles van por cuadros de 4 casillas y cada línea mira solo los cercanos; y el mapa se vuelve a recorrer solo cuando cambia su número de objetos (`hierarchyCount`, como mucho cada 0,5 s), no cada 2 s. Medido en el editor, en el pinar del asedio (1.220 árboles): antes, 0,34 ms por fotograma con 2 puntos y un pico de 6–9 ms cada 2 s; ahora, 0,01 ms sin enemigos y 0,14 ms con 20 muertos a la vista (el peor fotograma, 0,2 ms, también con la cámara en marcha), sin picos. Medida: `BASTION_COPAS_MEDIR=1`. Estado de prueba (el de Arte, con la escuadra en la vereda del noroeste y 20 muertos) y capturas antes y después en `.dev-snapshots/ui/unity/copas-enemigos/` |
| sí | `HudShots.PartidaTurn` buscaba la primera orden `endTurn` con 4 muertos andando o más. Las partidas de golden/step2 grabadas otra vez el 28/10 ya no tienen órdenes `endTurn`: los turnos de los muertos van dentro de `ability`, `move` o `pass`. No encontraba ninguna, aplicaba la grabación entera y quedaba el final de la partida (con `wilds-9-rescue` no había funcionado nunca). Ahora `CombatLab` busca cualquier orden con 4 muertos andando (sí de Gameplay): con `city-11-scavenge`, la orden 8, con 47. No era del motor: en el registro no había excepciones suyas. Captura en `.dev-snapshots/ui/unity/copas-enemigos/partida-turno-city11.png` |
| sí | Lo de UI de las notas de Ana, comprobado en la app (29/10, antes de pasársela a Arturo): en `ebd6223` (teclas, Esc, F12, aviso de 3 turnos, sin compañero, escala en 2560 × 1440 y árbol) y en la de Arte con SnowBuild (escudo, barra sobre la nieve, barras de vida al andar, rosa del asedio, líneas y «TU TURNO» sin nieve). Las teclas, con las simuladas de `BASTION_PASOS_COMBATE`; las de verdad (Mayús, Cmd+Mayús+4, F12), a mano, porque mandarlas pide el permiso de Accesibilidad. De esa prueba salieron dos arreglos. Jugando sola salía el reloj del compañero, una raya fina bajo el propio. La 0.10.1 solo lo pone con compañero en la partida (hud.ts `clockSlots.theirs`); ahora `CombatSession.ClockOf` hace lo mismo, con el puesto del otro en `Players`. El otro, de Arte: la copa apagada del árbol de calle (`Synty/Generic_Basic`) toma su textura de `_Albedo_Map` (`TreeFade.CrownOf`); antes se quedaba sin ella, con un aviso de `_MainTex`. Capturas en `.dev-snapshots/ui/unity/app-ebd6223/` |
| sí | Notas antiguas de Ana (docs/PRUEBA-2026-10-29.md, Arturo 29/10), la parte de UI:
- **#2 Anillos.** Bajo el elegido, el compañero, los enemigos y el blanco, el `SPR_HUD_Apocalypse_Ring_Large_02_Clean` de Synty (copiado a `Resources/Combat/Aro.png`), mucho más fino que las bandas de antes. Va teñido del color de cada uno, con dos hilos oscuros por fuera y dos por dentro para que se lea sobre la nieve (`MoveOverlay.Ring`).
- **#4 Lista con retratos.** Cada tarjeta de la lista de combate lleva el busto del soldado, de `Portraits` de Arte (`Know` al montar la lista, una vez por soldado; `Changed` la redibuja).
- **#5 Girar al soldado.** En Soldados y Personalizar, arrastrar sobre el claro, por debajo de los paneles, gira al soldado (`SoldiersScreen` «soldiers-turn», 0,4° por punto; `Settlement.TurnShowcase` de Arte). Laboratorio: `BASTION_ESTRATEGIA_ARRASTRE=puntos[:segundos]`.
- **#8 Barras de vida.** Como el `Slider_DarkFantasy_Horizontal_01` de Synty cortado: su pergamino (`Textures/hp-pergamino.png`) oscurecido hace de ranura, y cada punto de vida es un trozo, verde o rojo. Van en las banderas, la lista y el panel.
- **#9 Cámara al morder.** Además del golpe que mata, cuando un muerto da un golpe cuerpo a cuerpo a uno de la escuadra, la cámara va sobre su hombro hasta la acción siguiente (`EventPlayer.PlayAttack`). Al acercarse, no.
- **#14 Lo que ve un muerto.** Al pasar el ratón por encima, su campo de visión lleva el borde del alcance: la franja hacia dentro y la línea con las esquinas redondeadas, en rojo suave (`MoveOverlay.Border`). El relleno rojo de antes no cambia.
- **#15 Partes.** La página del capítulo sale abierta junto a CONTINUAR, sin «Leer el parte» (Lore: de 17 a 24 palabras).
- **#18 Ojo.** En la bandera de quien ve a la escuadra, un ojo de contorno, pequeño y gris, en vez del rojo relleno.
- **#21 Hexágono.** El color de la rama del tablero con más casillas compradas; si empatan, partido (`SkillHex.Branches`). En combate sale de la campaña tal como estaba al salir; en las escaramuzas, de las habilidades de la unidad. Ayuda de Lore: «Más puntos en el tablero: X».
- **De paso.** Metsovo y la autocaravana en lo de UI (Lore). Laboratorio: `BASTION_ESTRATEGIA_NIVEL`, `muestra-parte-capitulo`.
- **Capturas.** En `.dev-snapshots/ui/unity/notas-ana-29/`. |
| sí | Notas antiguas de Ana, segunda parte (reglas de Gameplay, 29/10):
- **#19 Medidor de ruido.** En la tarjeta del objetivo, «RUIDO» con una barra de 80 px y marcas cada 10 dB, y la cifra: de 40 dB en silencio a 90 al llegar la oleada (40 + 50 × `Rules.HordeFill`, redondeado). Gris; ámbar desde 60; rojo desde 80. Sale en todas las misiones con horda, también en la Limpieza; en el asedio, no. Ayudas de Lore (de tú), una para las misiones con autobús y otra para la Limpieza («Solo el ruido que haces…»). A 90 no se añade nada: ya están el cartel y el registro.
- **Armas nuevas.** Iconos de la sartén y la palanca de Military Combat, y del bate y la motosierra de Apocalypse (`HudIcons`, `weapon:pan|bat|crowbar|chainsaw`; `Ui.WeaponIcon` para todos). Los planos del bate y la palanca se leían como una raya, así que en `Textures/Icons` van sus dibujos en diagonal, en blanco y recortados (`w-bate`, `w-palanca`). Salen en la Armería, en el selector del cinto, en la mochila, en la tecla del Tajo y en el rótulo al encontrarlas o equiparlas, ahora con su icono. La motosierra enseña sus usos de gasolina en la tecla, como las granadas (×3).
- **La piedra.** Solo con «Piedras». Sin ella, la tecla de Lanzar lleva la granada y el menú, lo que haya (granada, humo).
- **Laboratorio.** CombatLab `ayuda=<clase>` (deja la ayuda abierta); StrategyLab `muestra-armeria-arma-nuevas` y `muestra-soldados-nuevas`. Sonido: `SoundCheck.Combate` de Red da cada arma con su golpe.
- **Capturas.** En `.dev-snapshots/ui/unity/ruido-armas/`. |
| sí | Notas de Ana con la 0.11.0 (docs/PRUEBA-2026-10-31.md, decididas por Arturo el 31/10), las de UI:
- **#2 El cartel del compañero.** «Arturo: ¡contacto! Ver (F)» sube por encima del menú abierto en la barra (las armas, Lanzar, Vigilancia): `ActionBar.MenuShown` y `.notes--over-menu` (152 px).
- **#3 Al hombro, sin zona.** Mientras la cámara está fuera de su vista (`CameraRig.Away`: al hombro, entrando o volviendo), se apagan el alcance, el camino, los punteros, la cobertura y las rutas planeadas, las propias y las del compañero (`MoveOverlay.Cinematic`, `AimOverlay.Cinematic`). Los aros bajo los pies se quedan.
- **#4 El turno enemigo.** Al empezar, `EventPlayer.Choreograph` reordena lo que se va a ver (solo en pantalla; los resultados son los del motor). Primero, a la vez, todos los paseos de los muertos (también las patrullas y los que luego atacan), cada uno con lo que le pasa por el camino: el disparo de vigilancia, la caída, la trampa, la puerta que abre. La cámara se aleja hasta encuadrar lo que la escuadra ve de ellos (`CameraRig.FrameZoom`). Después, uno a uno y enteros, los ataques (mordiscos, golpes, el grito del Chillón, la puerta a golpes) y lo demás, en su orden y con la cámara en cada uno. Van en su orden, sin juntarse: quien pertenece a un grupo descubierto o llega en el turno, lo que hace uno después de atacar y el paseo que cruzaría por donde aún está uno de los nuestros que cae después. «Cambia de presa», «se gira» y «olvida» ya no se llevan pausa ni cámara (eran 2,3 s cada uno sin verse nada), y las esperas, un poco más cortas: 0,25 / 0,75 / 0,55 / 0,35 s (antes 0,35 / 0,9 / 0,7 / 0,5). Medido con `PartidaTurn` a 10 fps, el mismo turno grabado con el EventPlayer de la 0.11.0 y con el nuevo: city-11 orden 12 (47 patrullando), de 40,3 a 10,6 s; facility-2-hack orden 47 (7 ataques), de 154,0 a 57,9 s; city-6-hard-veterans orden 34, de más de 185 s (no acabó en la captura) a 44,5 s. `BASTION_RITMO=1` dice cada suceso que tarda.
- **#6 La línea, por debajo.** Las líneas de la zona y del camino pasan la prueba de profundidad, acercadas 0,4 m a la cámara por su propio rayo (`Bastion/Marks` `_Pull`): el soldado y lo que haya delante las tapan, y siguen viéndose sobre el suelo y por la pared que bordean.
- **#7 Banderas de enemigos.** Sin el ojo y sin la cobertura; la vida, el blindaje y los estados se quedan. La de los soldados guarda su cobertura. Fuera `Eye.cs`.
- **#9 El nombre**, solo con el ratón encima.
- **#11 «Tus soldados»** en la Escuadra: cada fila con su retrato (`SquadScreen.PortraitOf`), también los no elegidos.
- **#12 La cámara a 45°** (antes 52°). Para no ver más allá que antes, el alejamiento máximo baja de 42 a 33,5 casillas: a 45° y 42, el borde del mapa entraba aunque se mirara al centro. Con el mismo rig, también el claro de la base. Arte pasó a 45° la cámara de sus laboratorios y la toma del Asentamiento.
- **#13 La tarjeta del objetivo.** Al pulsarla se abre la ventana de la misión tal como está (`HudRoot.IntroNow`), y la tarjeta se queda en su sitio. Antes se ocultaba y, en una misión retomada (sin ventana de llegada), no abría nada.
- **Laboratorio.** `HudShots.ContactMenu` y `GoalAgain`; HudLab `contacto`, `menu` y `objetivo`.
- **Capturas.** En `.dev-snapshots/ui/unity/notas-31-10/`. |
| sí | Notas de Arturo con la 0.11.1 (docs/PRUEBA-2026-10-31-ARTURO.md, decididas por él el 31/10), las de UI:
- **#1 La Lata se va.** Sin subir: todos los que embarcan a la vez se desvanecen junto a la puerta (`UnitViews.Board` y `BoardSeconds` de Arte, 0,5 s; `EventPlayer.SameMoment` los junta), sin el polvo de antes. Luego, mientras arranca y se va (`MapView.LataDeparts`, de Arte), la cámara la sigue 2,5 s girando unos 20° hacia su espalda (`CameraRig.Turn`). No pasa de 6 casillas de su sitio, para que La Lata salga del cuadro y la cámara no se vaya con ella por los tejados hasta la niebla. Si el jugador mueve la cámara, la suelta. Desde que la cámara va a La Lata, como un plano de cine, sin ninguna zona: ni la de evacuación, ni las balizas y bengalas, ni el alcance o el camino (`CombatController.Film`, `MissionMarks.Cinematic`; Gameplay). Ya no vuelven, porque la misión se ha acabado. En la base, la salida es de Arte (`Settlement.Leave`).
- **#2 Sin «correr» con una acción.** Con la última acción, la zona, el camino y el aro son los de andar, y la nota no dice «corre»: el motor solo oye correr cuando el movimiento gasta dos (`Rules`: `ap >= 2`). Cambia el #10 del 21/10.
- **#3 La línea, solo tapada por personas.** Las líneas de la zona y del camino, con sus sombras, van por encima de todo menos de las figuras: soldados, zombis y muertos, con lo que llevan. `Bastion/Marks` lleva dos pasadas. Donde no está el bit 64 del stencil (el que deja el `FigureMask` de Arte), dibuja sin prueba de profundidad. Donde está, con la prueba y el `_Pull` del #6 de Ana. `MeshBuilder.Material(…, figures: true)`. Mientras una figura se desvanece, la tapa su silueta entera.
- **Ventanas (regla nueva de Arturo, 31/10, en el motor).** Los enemigos no ven a través de ventanas, en ninguno de los dos sentidos. Lo que se mira por sus ojos usa su alcance: el aviso de que te verán (`Tiles`, Detected) y lo que ve un muerto con el ratón encima llevan `Los.WindowReachOf(a.Team)` en `Los.SightOrigin`.
- **#5 Asedio de día.** Solo se ven los zombis que ve la escuadra (fuera el `all` de `See()`), y las cajas especiales, cuando su casilla está explorada. Arte da por explorados la base y el claro, y deja el pinar en negro (`Fog`). Cambia el #18 del 18/10.
- **#8 La ficha del Mapa ya no se pliega.** El parpadeo del título metía su marco como hijo del `Label`, y un `Label` con hijos se mide como caja y no por su texto. El título se quedaba sin alto 1,35 s y la ficha se plegaba. Ahora, sobre un texto, el marco va al lado, encima de su sitio (`StrategyRoot.BlinkOn`, `Over`).
- **#9 Lugar y dificultad.** En la ficha de la misión, una fila «Lugar» entre «Tipo» y «Dificultad», con el nombre (`Places.Of(o.Map, o.Story)`, `Places.Names`) y un dibujo nuestro, porque Synty no tiene ciudad ni fábrica (`LineIcon` `place:city|town|estate|factory|roadside|country`: bloques, casa con campanario, nave de dientes de sierra, chimenea, surtidor, granero). El subtítulo usa el nombre del lugar en vez del bioma. Las estrellas salen de los zombis que habrá (`Campaign.OfferStars(o)`, de 1 a 4). Sin lugar (mapas hechos a mano, el asedio), no hay fila.
- **#11 Señalar desde la lista.** Con el ratón sobre la tarjeta de un soldado de la lista (vivo, propio o del compañero):
  - su aro se ilumina, doble y en luz cálida, y late (`MoveOverlay.ShowPointed`);
  - sobre su bandera sale una flecha que baja y sube, con su nombre (`FlagView.Pointed`, `UnitFlags.PointerArrow`);
  - fuera de la pantalla, la flecha del borde con su nombre y las casillas, como las marcas de la misión (`EdgeMarks`, `edge-mark--soldier`).

  Se apaga al salir de la tarjeta o al mover el ratón por el mapa (`IHudHandlers.OnHoverSquad`).
- **#12 El mapa, con la cámara desde arriba.** La primera versión era un mapa plano en un panel; Arturo, el 01/11: «el minimapa es muy minimapa», que la cámara se ponga cenital y más grande. Con la M (y hasta la interfaz nueva del 01/11, con la chincheta junto a «Menú»), la cámara sube recta sobre el mapa, con el norte arriba, y se aleja hasta encuadrar lo explorado más dos casillas, como mínimo 20 de lado; sin niebla, el mapa entero (`CameraRig.EnterOverhead`, `CombatController.MiniMap`).
  - Se ve el juego mismo, con la niebla de Arte: lo explorado, las banderas de los soldados y de los zombis a la vista, la evacuación y La Lata.
  - Debajo de la tira del turno sale «MAPA · Clic: bajar la cámara allí · M o Esc: volver» (`MiniMap`).
  - Al pulsar en el suelo, la cámara baja allí, con su ángulo y su distancia de antes. La M otra vez o Esc la devuelven a donde estaba.
  - Mientras está arriba no salen las marcas del movimiento, un clic no da órdenes y nada más mueve la cámara: ni los pasos del turno enemigo, ni los zooms, ni la cámara al hombro (`Overhead`).
  - La M ya no silencia el sonido, que queda solo en Ajustes › Sonido (Arturo, 01/11).
- **#13 Sin «Flanco» contra los muertos.** No sale ni en la bandera al pasar el ratón ni en el camino (`Flanks`). El +50 % de crítico sigue y en el panel del disparo se llama «Al descubierto» (`CritLabel`). Contra los vivos, como antes.
- **#14 El ojo, sin recuadro.** En las casillas donde te verán, solo el ojo, en un rojo más suave (`SoftSight`, 0xe58a80 al 85 %).
- **Laboratorio.** `HudShots.BaseRed` con pasos `offer:n` y fotogramas a 30 fps para la ficha; `PartidaEvac` a 10 fps para La Lata; StrategyLab `BASTION_ESTRATEGIA_LUGARES=1` (con `muestra-mapa-mision`) pone los seis dibujos en una tira. En CombatLab (PackSteps), `list=s3` y `list=` para la lista, `mapa` para subir la cámara o bajarla, y `mapa=40/12` para pulsar en esa casilla desde arriba.
- **Capturas.** En `.dev-snapshots/ui/unity/notas-arturo-31/`. |
| sí | Notas de Ana con la 0.11.1 (docs/PRUEBA-2026-11-01.md, decididas por Arturo el 01/11), las de UI:
- **#1 El aro del puntero**, solo con el soldado elegido, con acciones y sobre una casilla a la que llega, con el color de su franja. Sobre otra casilla o sobre un enemigo, ninguno. Fuera `PlainRing` y `EnemyRing`. El aro bajo el soldado no cambia.
- **#2 Sin «Flanco» sobre nadie**, tampoco contra los vivos: ni el rótulo ni su fondo rojo. Sobre el enemigo, la probabilidad en su color normal; desde una casilla, la probabilidad. El dato sigue en el panel del disparo («Flanqueo» contra los vivos, «Al descubierto» contra los muertos).
- **#4 La pausa en las cinemáticas.** Con la pausa (`Time.timeScale` 0) ya se paraban el reproductor, las figuras y los efectos; ahora también la cámara (`CameraRig.Update`), y al reanudar sigue desde donde estaba: al hombro, siguiendo a La Lata o encuadrando el turno enemigo. La pausa vale hasta que se ha visto el final, no solo hasta que el motor lo decide (`Shown.Outcome`), así que La Lata también se para. Mientras dura, el menú tapa la batalla (Ana, 28/10).
- **#5 Varios muertos contra el mismo soldado:** una sola entrada al hombro en el primer golpe, de un hombro al siguiente sin salir, y una sola vuelta a 45° tras el último (`BlowOnKillCam`, que mira también el golpe que sigue a su `abilityUsed`).
- **#6 Comer juntos** (con Arte; solo pantalla). Cuando uno se pone a comer a uno de los nuestros, los muertos a la vista a 2 casillas o menos del cuerpo, del más cercano al más lejano: el que cabe junto al cuerpo se arrodilla y come (`CanJoinFeed`, `JoinFeed`, de Arte, hasta 3 además del que mata) y el que no, alarga los brazos desde su casilla (`ReachFor`). Se espera a todos (`EventPlayer.Crowd`).
- **De paso:** «1 casilla», en singular, en las flechas del borde.
- **Capturas.** En `.dev-snapshots/ui/unity/notas-ana-01-11/`. |
| sí | La interfaz de combate nueva (`.dev-snapshots/gameplay/notas-interfaz-01-11/UI-COMBATE-v1.0.pdf`, UI-01 a UI-12; decisiones de Arturo del 01/11):
- **UI-01, UI-02: la escuadra, un solo panel abajo a la izquierda.** Sustituye a la lista de arriba y al panel del soldado. Arriba, la ficha del elegido en una línea: nombre, rango, arma con sus marcas, cargador y cargadores de repuesto, pistola, puntería, cuerpo a cuerpo, granadas, blindaje y la mochila al final. Si no cabe, las cifras pasan a una segunda línea. Debajo, un retrato por soldado: el busto de Arte, el nombre, la salud en verde y las acciones en amarillo. El elegido sale más grande y todos acaban en la misma línea (`SquadList`, `SquadCard.uxml`, `UnitPanel`).
  - Solo salen los soldados de este jugador, también los caídos y los evacuados, y los del compañero mientras los manda él (Arturo). Los del compañero y las torretas del asedio no tienen retrato: si se eligen, su ficha lleva la salud y las acciones (`OnCard`).
  - Con cuatro, el panel ocupa una cuarta parte de la pantalla. Con más (La Lata lleva ocho), crece un poco y los retratos se estrechan, con el nombre de pila. La fila de teclas se aparta si el panel llegaría a ella (`HudRoot.PlaceActions`).
  - Los estados del soldado (VIGILANCIA, MORDIDO…) salen sobre el retrato, los dos primeros; todos, con lo que significa cada uno, en la ayuda de la tarjeta.
  - La mochila, con sus reglas de siempre: los huecos encima del final de la ficha, lo que hay en el suelo encima de ellos y su menú hacia arriba. Lo que lleva la escuadra va en la ayuda del icono de la mochila de la ficha (`Backpack.Haul`).
- **UI-08: clic y doble clic.** Un clic en un retrato elige al soldado sin mover la cámara; antes la movía. El doble clic lleva la cámara a ese soldado, en su planta, y desde el mapa cenital la baja allí (`OnLookUnit`). El doble clic se cuenta en `SquadList`, porque las tarjetas se pintan de nuevo al elegir.
- **UI-03, UI-10: fuera de arriba «TU TURNO», «OCULTOS» y el selector de plantas.** El sello del centro sigue (Arturo), y también el marco del color del turno en cooperativo, la noche, «EN JUEGO» y los jugadores. Las plantas siguen con RePág/AvPág y Mayús+rueda (Arturo).
- **UI-04, UI-05: «Terminar turno», la séptima tecla de la barra.** Es una tecla cuadrada como las demás, con →| dibujado (`LineIcon` `endTurn`) y sin texto; lo que dice va en su ayuda. Armada para la segunda pulsación, lleva el borde de acento, como cualquier tecla armada; a punto, la marca de visto en verde; apagada mientras el turno no es de este jugador. Debajo de toda la fila, el reloj de este jugador en su color, del mismo ancho que la fila, y en cooperativo el del compañero, fino y en su color, justo debajo (Arturo). Fuera del turno las barras desaparecen pero dejan su hueco, para que la fila no se mueva (`TurnActions`).
- **UI-06: el objetivo, una flecha en un aro.** Abajo a la derecha, un aro de acero oscuro con una flecha amarilla de una punta. En cada fotograma apunta a donde está el objetivo en pantalla, visto desde el centro, así que gira con la cámara. Sin texto ni distancia, y no hace nada al pulsarla. El objetivo es el de la tecla O: el de la misión mientras falta, si no la evacuación; sin ninguno, no hay aro (`GoalArrow`, `HudRoot.SetGoal`). Las flechas del borde siguen para las brechas del asedio, el momento del compañero y el soldado señalado en la lista.
- **UI-07, UI-09: la cámara y el mando, al menú.** Fuera el botón «Menú» (el menú se abre con Esc) y la chincheta del mapa (el mapa, con la M; Arturo). En el menú, debajo de «Continuar», cada entrada con su tecla a la derecha: «Ir a Mateo» (F) mientras este jugador da las órdenes y alguien tiene acciones; en cooperativo, «Ceder el mando» (C) o «Ver a Ana» (F); y «Cámara al soldado» con su interruptor (`GameMenuView.GoTo`, `Watch`, `Pass`, `Follow`). Al elegir una, la pausa se quita antes de la orden (`PauseTick`), para que no se quede retenida. En Controles se añade «Doble clic en un retrato».
- **UI-11, UI-12.** La escuadra, la fila con su reloj y el aro acaban a la misma altura, a 8 px del borde. La misión, el ruido, el contador, las demás teclas, la escena y las banderas no cambian.
- **Laboratorio.** En CombatLab (PackSteps): `retrato=s2` pulsa la tarjeta (dos seguidos, el doble clic); `reloj=0.8` enseña los relojes, que en el laboratorio no hay; `fin`, una sola pulsación de «Terminar turno».
- **Capturas.** En `.dev-snapshots/ui/unity/interfaz-01-11/`. |
| sí | El fuego (regla del motor, Arturo 01/11: `GameState.Fires`, el evento `burned` seguido de `damaged`): lo que dice UI.
- **La quemadura.** El `damaged` que sigue a un `burned` sale como cualquier daño (la cifra «−1», el golpe de Arte), y en el registro «Zainab Bello pierde 1 de salud por el fuego.»; la de un muerto, solo si la escuadra lo ve («Un zombi pierde…»). En el turno enemigo la quemadura va con el paseo de quien la sufre (`OnTheWay`).
- **El camino.** Las casillas que arden dentro de la zona de movimiento llevan una llama naranja, como el ojo de las que te verán, y al lado si hay las dos (`MoveTile.Fire`, `MoveOverlay.FlameTexture`). La nota del destino dice «Fuego en el camino: −2 de salud», y si acaba en el fuego, «…, y −1 más si acaba el turno ahí» (`FireNote`).
- **La casilla bajo el puntero.** Sobre una que arde, fuera de un movimiento: «Fuego: quita 1 de salud al pisarlo, y 1 más al acabar el turno en él».
- Lo que se ve del fuego (llamas, humo, luz) es de Arte. Textos de Lore («salud», como en todo el juego). Capturas en `.dev-snapshots/ui/unity/fuego/`. |
| sí | El clima (regla del motor, Arturo 01/11: llovizna, aguacero, tormenta, nevada y ventisca; `Campaign.OfferWeather`, `GameState.Weather`, `Content.Weather`): lo que dice UI.
- **La ficha de la misión.** Junto a «De noche», en el subtítulo y en la lista de misiones, el nombre del tiempo; en la ficha, una fila «Tiempo» con su dibujo (`LineIcon` `weather:drizzle|downpour|storm|snowfall|blizzard`: la nube y lo que cae de ella) y en su ayuda lo que cambia. Sale de la misma función que el lanzamiento, así que lo que dice la ficha es lo que habrá.
- **En combate.** En la tarjeta del objetivo, junto a los turnos, el dibujo y el nombre del tiempo («TORMENTA»), con lo que cambia en su ayuda; y en la tarjeta de llegada, tras «DE NOCHE». Lo que cambia sale de las cifras del motor (`HudText.Weather.About`, sobre `Content.Weather.SightOf` y `NoiseOf`): «Tormenta: se ve menos lejos y los ruidos se oyen menos.».
- **La vista y el ruido que se enseñan.** El cono de un muerto y el aviso de que te verán ya salían de `Los.SightTo` y `DetectionTo`, que aplican el clima; el recuadro del cono y el «Alcance» de la tarjeta de un arma usan ahora `Los.SightIn` (la vista con el clima), no la de día. El anillo del ruido sigue al radio del evento, que ya trae el clima, y las cifras de «Ruido» de las vistas previas (la nota del camino, la tarjeta de una tecla, el panel del disparo, el lanzamiento) dicen hasta dónde llegará con este tiempo, redondeadas como `Rules.MakeNoise` (`AbilityTexts.Heard`: en tormenta, «Ruido 3» pasa a «Ruido 1,8»; Gameplay, 01/11).
- Laboratorio: StrategyLab `BASTION_ESTRATEGIA_TIEMPOS=1` (con `muestra-mapa-mision`) pone los cinco dibujos en una tira y abre la primera misión con tiempo. Textos pendientes de Lore. Capturas en `.dev-snapshots/ui/unity/clima/`. |
| sí | Jugar por internet abriendo el puerto del router (encargo de Arturo a Red, 01/11: `NetLink.Router`, `Internet`; UPnP y NAT-PMP en `RouterPort`): lo que dice la sala.
- Con el puerto abierto, debajo de la primera dirección, la fila «Por internet, con la clave» con la dirección para el otro y la clave de la sala (Arturo, 01/11: el protocolo está a la vista; `NetLink.Invite`, «83.45.12.7:41391 · KXRT5»), y «Copiar», que copia las dos para pegarlas tal cual en «Unirse por dirección». En la red y con Tailscale no hay clave. Mientras se abre, «Abriendo el puerto del router…»; si no se puede (sin router que conteste, la dirección compartida de la compañía, o el router que no lo abre), una línea en ámbar que lo dice y manda a Tailscale (`RoomText.RouterLine`). La de Tailscale se llama ahora «Con Tailscale», y la pista de «Unirse por dirección» del inicio no habla de Tailscale y pide la clave si la lleva. Textos de Lore.
- Se dibuja otra vez cuando cambia el estado (la clave de `RoomMenu.Update`). Laboratorio: `BASTION_SALA_ROUTER=trying|open|norouter|shared|refused` y `BASTION_SALA_ABIERTO=1` (las otras direcciones desplegadas). Capturas en `.dev-snapshots/ui/unity/sala-router/`. |
| sí | Notas de Arturo del 01/11 por la tarde (docs/PRUEBA-2026-11-01-ARTURO.md), las de UI:
- **#1 Los nombres bajo la escuadra.** En la Escuadra, al meter o sacar soldados, las tarjetas bajo las figuras se quedaban con los nombres de antes: `LineupCards` comparaba la lista con `Ui.Key`, y JsonUtility escribe «{}» para una lista, así que solo se dibujaban otra vez si cambiaba un retrato. Ahora `Ui.Key` compara una lista elemento por elemento.
- **#2 La ficha en corto.** En la Escuadra, con el puntero sobre un soldado de cualquiera de las listas (la tuya, la del compañero, los defensores del asedio), su ficha en corto al lado de la lista, a la altura de su fila, sin taparla y por encima de la misión: rango, salud, puntería, movimiento, arma, cinturón, habilidades del tablero, y heridas o mordisco (`SoldierPeekView`, `StrategyPresenter.Peek`, `SquadScreen.Peek`). El cinturón, que salía en la ayuda de la fila, va ahora en la ficha.
- **#4 La línea del alcance, seguida.** Se cortaba en cada coche, caja, consola o persona del borde (desde la #18 de Ana del 21/10, el lado que da a una cosa en medio no se dibujaba): de 5 a 80 lados por alcance en las partidas grabadas. Ahora la línea rodea por fuera lo que toca el alcance y los huecos de dentro que son solo cosas, así que sigue sin cortes y no hay recuadros dentro (`MoveOverlay.Outline`). Las paredes, y los huecos con alguna pared, los rodea como antes. No había cortes por la altura ni por las plantas.
- **#5 Por qué no andan todos a la vez.** La partida de Arturo no se jugó en este Mac. En 15 turnos enemigos grabados, solo se separan un zombi que anda después de atacar y los vivos que disparan. La línea «[ritmo] turno enemigo» del registro dice ahora por qué anda aparte cada uno: recién llegados o descubiertos, su camino pasa por donde aún hay alguien, disparan, andan después de atacar.
- **#8 El tratamiento en la placa.** Con el motor nuevo el tratamiento es una propuesta de la sala (`TreatAction`): sale en la placa como las demás, con el reloj, el soldado y de quién es, la medicina y lo que hace (`ProposalDetail`); la ficha del soldado vota y retira con ella (`Treating`, `CareOrder`). Uno de antes guardado a medias (`c.Treatment`) sale en la placa sin reloj y va con `TreatCommand` (`ProposalView.Treat`, `StrategyPresenter.Treatment`).
- **#10 Sin pistolas, y el molotov** (reglas de Gameplay, 01/11).
  - La ficha: «Costado», con las pistolas de la Armería, cuando la campaña las cuenta (`Pistols`); sin nada en la mano ni al costado, «Nada» y «sin arma de fuego: solo el cuchillo» (`WeaponPick` por sitio: `Hand`, `Side`, `Belt`). La Armería explica la pistola y quién la lleva.
  - En combate, sin arma de fuego no hay munición ni puntería con armas, y el arma es el cuchillo o el machete (`ArmsOf`, `UnitPanel`); los cócteles que quedan, con nuestro dibujo (`LineIcon` `molotov`).
  - El molotov en «Lanzar», antes que las granadas, con su daño en la ficha de la tecla. Al apuntar, el 3×3 bordeado en rojo anaranjado con una llama por casilla (`AimOverlay.ShowBlast` con `fire`); en un piso alto, en gris y «Solo a la calle o a una planta baja». Al caer, un fogonazo naranja con chispas y el sonido del cristal y la llama de Arte (`Sfx.Molotov`) en vez de la explosión, y «Arden 9 casillas durante 2 turnos.» y «Se apaga el fuego del cóctel molotov.» en el registro; si delata a la escuadra, «El cóctel molotov de X se ha oído en toda la zona.». La botella en la mano y en el arco y el fuego son de Arte (`ThrowMotion` con `held`, `Fx.MolotovArc`, `FireView.Light` y `Out`, que EventPlayer llama al caer y con `FireOutEvent`). El fuego en el alcance y en la nota del camino sale de lo que ya se ha visto (`Shown`), no del motor: antes salía con la botella aún en el aire.
  - A quien no tiene arma de fuego, una pistola o un fusil le dicen «Su primera arma de fuego» en vez de compararse con el cuchillo (la ficha y la Armería).
  - La tienda enseña todo lo del cinturón: lo que aún no se puede comprar, al final y con su «Nivel N».
- **La cámara al hombro y La Lata.** `ShotCam` no deja la cámara dentro de ninguna pieza de La Lata ni mira a través de ella (sus cajas giradas con ella: `Things`, `InThing`); en arena-evac se quedaba 3 s dentro del autobús. Las marcas de la misión se quitan sobre el hombro hasta que la cámara vuelve (`Rig.Away`), porque el objeto soltado tapaba el plano cercano; desde arriba se quedan.
- **#6 Comer, en la mitad** (con Arte). De unos 4,2 s a unos 2,4 s desde que cae el soldado hasta la siguiente acción. Arte bajó `FeedWait` a 0,2, `FeedSeen` a 0,4 y arrodillarse va 1,8 veces más deprisa; UI bajó `FeedHold` de 0,5 a 0,2.
- **Arreglos de la prueba con la app de la 0.11.5** (`.dev-snapshots/ui/unity/app-0115/LEEME.md`):
  - con la pistola puesta, la hoja y el panel decían «Cuchillo»: `ArmsOf` usa el nombre de la unidad (`WeaponName`) solo con un arma en la mano;
  - el coche de policía, «ya está abierto» (concuerda con `SearchSpot.Masculine` de Lore), y en la ayuda de «Registrar», «clic en el coche» y «Tab: la otra mitad»;
  - en el Mapa y la Escuadra, la radio tapaba la cabeza de la placa: con una propuesta, baja bajo ella (`StrategyRoot.Unstack`, por el borde de la placa);
  - reclutar en una campaña que cuenta las pistolas: «sale con un cuchillo, sin pistola» (Lore);
  - en una campaña de antes, «la pistola al costado» (la ficha y la Armería).
- **Laboratorio.** StrategyLab `mapa-propuesta-radio`: la placa y la radio a la vez. StrategyLab `escuadra`: pulsar un soldado lo saca o lo mete y dibuja otra vez la escuadra; `BASTION_ESTRATEGIA_ENCIMA=s3` pone el puntero en su fila; `muestra-propuesta-tratamiento` (y `-mio`, `-viejo`), el tratamiento de Hiro en la placa; `-pistolas` y `-nivel1` en cualquier caso de la muestra, con `BASTION_ESTRATEGIA_SOLDADO` y `BASTION_ESTRATEGIA_ARMA`; en CombatLab, `hover=x/y/planta`. CombatLab (`PartidaMochila`) con `BASTION_LAB_ARCHIVO` y `pick=`, `hover=`, `look=` para el alcance; `PartidaTurn` con `BASTION_LAB_OP` para un turno concreto.
- **Capturas.** En `.dev-snapshots/ui/unity/notas-arturo-01-11-tarde/`. |
| sí | Notas de Ana del 01/11, «Informe para desarrollo 2» (docs/PRUEBA-2026-11-01-ANA.md, decididas por Arturo), las de UI para la 0.11.6:
- **#10 El turno de los zombis** (y la #9). Solo en pantalla: `EventPlayer.Choreograph` reordena lo que se ve, los resultados son los del motor. Primero muerden, de uno en uno y enteros, los que empiezan su turno con un golpe a mano sobre uno de los nuestros (`FrontBites`: sus sucesos desde el primer golpe hasta lo siguiente que hagan, andar u otro que actúa). Luego andan todos a la vez, como desde la #4 de Ana del 31/10, también el que anda después de morder. Al final muerden, en su orden, los que han llegado. Uno de los que ya estaban al lado muerde en su orden si adelantarlo cambiaría lo que se ve: si tumba a quien otro muerde antes, o si su presa anda, dispara o cae antes. También los descubiertos o llegados en el turno. Quien olvida o se gira por el camino ya no se queda fuera de los que andan a la vez (`OnTheWay`; el registro decía «andan después de atacar»). La línea «[ritmo] turno enemigo» lleva ahora cuántos muerden primero y por qué muerden en su orden los demás, además de por qué anda aparte cada uno.
- **#7 Los aros de los zombis**, solo el trazo rojo: sin los hilos oscuros que `MoveOverlay.Ring` pone a los demás aros (`threads`), que al lado del rojo parecían un borde gris.
- **#2 La Escuadra**, la tarjeta bajo cada figura solo con el nombre y el rango, sin retrato (`LineupCards.Busts`). En el parte de la misión sigue el retrato, porque los caídos no tienen figura.
- **(e) de la prueba con la app de la 0.11.5, la cámara del contacto.** Al responder un grupo, la cámara iba al centro de todos sus zombis, también de los que la escuadra no ve. Si estaban detrás o dentro de un edificio alto, nada abría su tejado y se veía de cerca. Ahora va solo a los que se ven; si no se ve ninguno, se queda donde está (`PlayContact`, `PlayTogether`). El sello y la línea del registro no cambian. Con `BASTION_RITMO=1`, «[ritmo] contacto: N del grupo, M a la vista».
- **#1 Esconderse** (el motor, de Gameplay: `AbilityIds.Hide`, `HiddenEvent`, `Unit.Hidden`, `Los.HiddenShare`). Su tecla sale junto a Cubrirse, con el número que le toca por su sitio en la barra. El icono es la capucha de Dark Fantasy HUD (`ICON_DarkFantasy_Status_Stealthy_01_Clean`, en `HudIcons` y su tabla); la silueta de puntos («Invisible») era fina en una tecla y decía «no se ve», no «se ve desde la mitad». La ficha es la de Content, como las demás. Al esconderse, «ESCONDIDO» o «ESCONDIDA» sobre el soldado, con el sonido de agacharse, y el gesto de Arte entero antes de lo siguiente (`UnitViews.Hide`: se gira hacia la cobertura y baja a una rodilla; en wilds-9-rescue, 1,1 s, y la Vigilancia de otro pedida 0,2 s después empieza al acabar); y mientras `Hidden`, la etiqueta en su rótulo y en su tarjeta, en gris azulado (`.tag--hide`), aparte del verde de «CUBRIÉNDOSE». Lo que ve un zombi al pasar el puntero (`ShowVision`) lleva `HiddenShare` en la casilla de un escondido, como `Los.CanSee`; la nota sobre un zombi bajo el puntero («Ve a…», `IntentNote`) ya usaba `CanSee`. Agacharse escondido es de Arte.
- **#4 Cada zombi por su cuenta** (el motor: `PodSplitEvent`, «p3» deja «p3.1» con los que despiertan). El cliente toma el grupo de la activación de la vista, que ya tiene la división cuando se toca: el «!», la cámara, el sello y el registro son solo de los que despiertan, y los demás siguen sin la alerta («A LA VISTA» al aparecer). Al ordenar el turno enemigo, `Choreograph` y `FrontBites` toman los del grupo nuevo de la propia división, que aún no está en la vista (`Woken`), y la división corta como una activación. La frase del contacto concuerda si despierta uno solo («Priya lo ha avistado», «Responde al ataque de…»); si la escuadra no ve a ninguno de los que despiertan, fueron ellos quienes vieron al soldado y dice «Os ha visto: ha descubierto a…» (antes, «Tariq los ha avistado» sin verlos). La línea del registro dice cuántos de su grupo a la vista no se han enterado (solo los que se ven, para no delatar a los demás). Con `BASTION_RITMO=1`, «[ritmo] contacto» dice de qué grupo se separa y cuántos siguen sin enterarse.
- **Laboratorio.** CombatLab `PartidaTurn` con `BASTION_LAB_OP` y `BASTION_LAB_MIO=1`: los sucesos de una orden de la escuadra como propios, con su cámara. Turnos grabados con mordiscos de los que ya estaban al lado: arena-hack 16, arena-night 30 y 50, bastion-siege 40, arena-storeys 6. Contactos de uno de un grupo de tres: arena-night 11 (lo ve uno), wilds-9-rescue 3 (despierta el golpeado). `PackSteps` «ficha=hide»: la ficha de esa tecla, fija (`KeyIndex`).
- **Capturas.** En `.dev-snapshots/ui/unity/notas-ana-01-11-b/`: las de la #10, la #7 y la #2; `1-esconder-*` (la ficha, el momento, el rótulo y la tarjeta, en city-3-elimination con `PartidaMochila` y «ficha=hide», «ability=hide»; el gesto y la Vigilancia que espera, en wilds-9-rescue tras el contacto, con su `1-esconder-ritmo.txt`); `4-contacto-*` (la tira a 10 fps de wilds-9-rescue 3, el «!» solo sobre el golpeado, el sello y el registro, y el contacto de noche sin verlo) y `4-contactos-ritmo.txt`. |
| sí | 0.11.7, la cámara sobre una azotea en Metsovo (la (e) de la prueba de la 0.11.5, que siguió en la de la 0.11.6; Arturo: arreglarlo ya). Los zombis que la escuadra ve en la calle y el callejón de 31–32/20–22, tras los bloques de tres plantas de la fila 22, y el bloque de en medio entero: la cámara, al ir a ellos, se quedaba sobre su azotea. Pasaba en tres momentos: el encuadre de los que andan en el turno enemigo (con «A la vista»), tras el tiro de Priya y en el contacto. **Causa:** `Cutaway.Step` (Arte) solo recalculaba la vista de la escuadra cuando cambiaba el objeto del estado (`s != seenFor`), y `UnitViews.Sync` le pasa `control.View`, que los eventos cambian en el sitio y solo se sustituye al cargar. Durante la misión el corte se quedaba con la vista del principio: un enemigo visto después no abría ni su edificio ni lo que hay entre la cámara y él (`Behind`). **Arreglo** (dos líneas en `Cutaway.cs`, con aviso a Arte): también se recalcula cuando cambia `GameState.Seq`, que sube con cada evento. La cámara no cambia. **Laboratorio:** `BASTION_LAB_GUARDADO` (el `save.json` de una app: su estado y sus dados) con `PartidaMochila` y `BASTION_TOQUES`; en `PackSteps`, «banner» y «confirm», como en la app. En el editor, los tres momentos ya sin azotea, con el horario de la app y fotos desde el principio a 5 fps (`captura-editor.sh` en las capturas: `W=0 FPS=5`). Si se calienta antes de grabar (`-shotWarm`), el juego corre unas 14 veces más deprisa que el reloj, `FrameAll` cree que la cámara aún es del jugador (`PlayerCamera`, 2,5 s de tiempo real) y el encuadre no se mueve; con `BASTION_RITMO=1` lo dice «[ritmo] encuadre de los que andan». En la app candidata (2/11, 17:58), los tres momentos con fotos cada 0,1 s contra los de la 0.11.6: sin azotea; entrar en un edificio andando y corriendo, el cursor dentro y el turno enemigo, sin parpadeos (lo negro hasta que entra un soldado es la niebla de los edificios sin explorar, como antes). Capturas en `.dev-snapshots/ui/unity/tejado-0117/` (`app-0116-*` antes, `app-0117-*` después) |
| a medias | Notas de Arturo y de Ana del 2/11 (docs/PRUEBA-2026-11-02-ARTURO.md, decididas en el cuadro), las de UI para la 0.11.7:
- **A3 Marcar desde el mapa (M).** Desde arriba, Mayús y clic, el clic de la rueda o G ponen la marca en la casilla del suelo bajo el puntero, la misma que en la vista (`Ping` con la casilla, `GroundUnderPointer`); un clic solo sigue bajando la cámara allí. Las marcas ya se veían desde arriba. Laboratorio: «marca-mapa=x/y» en `PackSteps`, y CombatLab devuelve las marcas como el anfitrión.
- **A5 Pantalla y resolución en Ajustes**, antes de «Gráficos». «Pantalla»: en el Mac, «Completa» (su pantalla completa, en un escritorio propio) y «Ventana»; en Windows, también «Sin bordes» (`FullScreenMode`). «Resolución»: la de la pantalla y las alturas habituales por debajo (1440, 1080, 900, 720) con su misma forma, cuatro como mucho; en ventana, solo las más pequeñas que la pantalla. Lo guarda el propio Unity (`Screen.SetResolution`); en el editor no cambia nada. Falta verlo en la app.
- **B1 Sin transmisiones.** Fuera la radio de la base: `Radio`, `RadioViews`, `RadioPanel` y `Radio.uss`, su tarjeta en `StrategyRoot`, `StrategyText.Radio`, Intro y Espacio de la base y los casos del laboratorio. El volumen «Interfaz y radio» pasa a «Botones y avisos». Lore quita las escenas del motor y Arte los retratos de las voces, en ese orden.
- **B2 Los avisos de zombis** (Arturo, en el cuadro, la A: marcas, no letreros). Fuera «A LA VISTA» y «¡APARECE UN …!» flotando sobre cada uno. El recién visto lleva 2 s, al principio de su rótulo, un ojo gris (su grupo no os ha visto; dibujado como el de la zona de movimiento) o un «!» rojo (os ha visto), y se apaga en el último medio segundo (`UnitFlags.Spot`, `HudRoot.Spot`). Cada zombi, una vez por misión: si sale de la vista y vuelve, ya no (`EventPlayer.announced`). Una línea por suceso, «A la vista: 3 zombis (aún no os han visto)», con «(os han visto)» o «(2 os han visto)» si alguno va a por vosotros, la raya gris o roja (`HeardCount`). Los que llegan por el borde, con el «!». En un contacto, el «!» en el rótulo de los que se ven (y flotando sobre los demás), y el sello «¡CONTACTO!» solo el primero de cada turno (`stamped`); los demás, su «!» y su línea. Lo del compañero (lo que ve al moverse y sus contactos) va al registro con «Ver» al final, que lleva la cámara allí (`Log.Add` con `see`, `HudText.Log.See`); ya no sale el aviso de abajo ni su chasquido. Laboratorio: `HudShots.Seen` (HudLab «vistos»), y en la partida `captura-b2.sh` (mision1-turno2: los tres de la esquina con el ojo al mover a Anja, el turno enemigo sin repetirlos, el contacto con el «!» y el sello).
- **A4 El ruido.** El medidor ya era `Rules.HordeFill`; su ayuda dice ahora en todas las misiones «Solo el ruido que haces atrae a la horda…» (`HudText.Mission.NoiseTip`; fuera `NoiseTipQuiet`).
- **A7 Por cantidades.** En la Tienda y, para armas y mejoras, en el Taller: «−», el número y «+» junto al botón («Comprar ×4», «Hacer 3»), de 1 a `Campaign.MaxBatch`; el coste, las piezas y las misiones multiplicados, con «cada uno» en la ayuda (`ArmoryPick.Count`, `ArmoryDetailView.Count`, `BuyCommand.Count`, `CraftCommand.Count`). Vuelve a 1 al elegir otra fila. Laboratorio: `muestra-armeria-cantidad`, `muestra-armeria-taller`.
- **A9 y A13 Cuerpos y maleteros.** Por la misma acción que las cajas (`AbilityIds.Search`): la tecla, su opción en «Investigar» («Cuerpo», «Maletero»), el panel («Registrar el cuerpo», «Abrir el maletero del coche de policía»: `Lore.BoxTitle`), la nota bajo el puntero («El cuerpo · clic: registrar», «ya registrado») y Tab por su nombre; el cuerpo a los pies también (`Looting.ReachesToOpen`). Fuera el coche de policía de la primera misión (`Looting.PoliceCar`, `PoliceCarParts`, el `part` del registro).
- **A11 La palanca.** «Quitar las tablas» (`AbilityIds.Pry`) a mano, como Registrar: un clic en la ventana tapiada de al lado o la tecla (con una sola, a la vez), Tab entre varias, su nota bajo el puntero; el icono, la palanca (`HudIcons`). Al quitarlas, la línea «X quita las tablas de la ventana.» (o «de la puerta», mirado antes de aplicar el suceso, porque después la casilla ya es puerta) y el mapa se vuelve a dibujar (`BoardsRemovedEvent` en `Settle`); el gesto, el golpe de palanca de Arte (`Units.Pry`). También las puertas tapiadas (`DecorKinds.DoorBoards`): «Puerta tapiada · clic: quitar las tablas», «clic en la puerta» en la ayuda, «Tab: la otra tapiada» y «No hay ninguna ventana ni puerta tapiada a tu lado.» (textos de Lore).
- **B5 Armas de mano.** En la Armería, «Golpea a la vez a todos los enemigos que tenga al lado» (katana, espada, motosierra: `Content.Sweeps`) o «Llega hasta 2 casillas, en línea o en diagonal» (rastrillo: `Content.ReachOf`); el Tajo, «Golpea: a todos los que tenga al lado» y su alcance en la ficha, y la nota en el panel de apuntar. La lista de quién la lleva dice el arma de cada uno (decía «Machete» para cualquiera). Los golpes seguidos se ven uno a uno, cada uno entero.
- **Laboratorio.** `mano/prueba-mano.json` (de mision1-inicio: un cuerpo junto a Leila, Priya junto al maletero del coche de policía, Mateo con katana y dos zombis al lado, Anja con palanca junto a una ventana tapiada) con su `captura.sh`.
- **A6 Izquierda y derecha** (Arturo, en el cuadro: cada uno los suyos a la izquierda). En el Parte, la fila y sus tarjetas van como en la Escuadra: los del primer jugador y luego los del segundo (`StrategyPresenter.ReportOrder`), y la app del segundo da la vuelta a la fila (`LineupSides(report)`); el hueco del medio solo si los dos traen los mismos, porque la fila se parte por la mitad. Laboratorio: `muestra-parte-mezcla` y `muestra-parte-mezcla-ana`.
- **El recluta gratis** (`Campaign.NobodyLeft`, de Gameplay). La base no enseña el registro de la campaña: su línea («X se une al Bastión como recluta…», de Lore) sale en el Parte, o como nota si llega al pasar el día (`StrategyPresenter.FreeRecruit`, `Joined`). Nadie queda bloqueado: lanzar solo pide que la escuadra no esté vacía, y cada uno dice «a punto» aunque no lleve a nadie.
- **La placa repetida** (captura de Red, «Un desertor»): con la votación dentro de la ventana del acontecimiento, la placa de arriba ya no sale detrás (`BaseController`).
- **Capturas** en `.dev-snapshots/ui/unity/notas-02-11/`.
- **Prueba con la app de la 0.11.7** (3/11, `Builds/Mac` de Gameplay, una sola copia, apps de prueba en 127.0.0.1:41398). Todo bien, sin excepciones en los registros de las apps ni errores en red.log:
  - una puerta tapiada de verdad (ciudad 22, la puerta de 37/12): la nota al pasar el ratón, el gesto y «Yuki Tanaka quita las tablas de la puerta.»;
  - B2 al andar: una sola línea, «A la vista: 4 zombis (aún no os han visto)», que no se repite en el turno enemigo;
  - «Ver» con dos apps: la línea de lo que ve Arturo sale en la app de Ana con «Ver», y la cámara va allí;
  - A6 en el Parte de las dos apps, también con caídos;
  - el recluta gratis: «Freya Lund se une al Bastión como recluta: a un jugador no le quedaban soldados vivos.» en el Parte de los dos, y en el asedio entra en la escuadra;
  - la placa con «Un desertor»: la propuesta y su voto, dentro de la ventana y sin placa detrás;
  - un asedio sin sanos: defienden los heridos, y la mordida no sale;
  - A5, en ventana: sí. La pantalla completa no se pudo probar, porque la sesión del Mac estaba bloqueada; falta probarla a mano. Capturas en `.dev-snapshots/ui/unity/app-0117/` |
| sí | 0.11.8, «Resolución» con un tamaño que no está en la lista (de la prueba de la 0.11.7; Gameplay, 3/11). Una ventana abierta o arrastrada a otro tamaño, como 1440 × 900, hacía que se marcara la primera opción, 1920 × 1080. Ahora el tamaño de ahora sale como una opción más, en su sitio por altura, marcada y con la ayuda «La de ahora»; al elegir otra, desaparece (`GameSettings.ShownSizes`, también al cambiarla). En el editor por lotes no hay pantalla (la lista es solo 640 × 480). En la app de la 0.11.8, con una ventana de 1440 × 900, se marca «1440 × 900» y desaparece al elegir otro tamaño. Con cuatro tamaños en una ventana de 1440 de ancho o menos, el último se cortaba en el borde del panel: ahora las opciones siguen en una segunda línea (`flex-wrap: wrap` en `.setting__choices`, GameMenu.uss). Con 1280 × 800, como la ventana de Arturo, ya no se corta, y las demás filas de Ajustes, en la base y en combate, no cambian. Capturas en `.dev-snapshots/ui/unity/app-0118/` |
| sí | 0.11.8, el compañero que aún no ha entrado en una misión abierta otra vez (de Red, por lo que salió en la prueba de la 0.11.7). La sala le espera 5 minutos y dice cuánto le queda (`PlayerInfo.GraceMs`). `CombatController.SetPlayers` lo toma como el plazo de quien se ha caído, aunque no se le haya visto entrar, así que mientras tanto no sale «X no está: mandas también sus soldados.», que la sala rechazaría. El bucle es el que propuso Red. En el laboratorio, el paso «waiting=N» de `PackSteps`: mientras dura el plazo no sale el aviso, y al acabar sale y se mandan también sus soldados (el plazo es de tiempo real; en las capturas, 1 s son unos 8 s de juego). Capturas en `.dev-snapshots/ui/unity/notas-03-11/`. En la app de la 0.11.8, con dos apps y la anfitriona sin soldados en el campo:
  - restaurando la partida y con «Continuar campaña», el turno espera y no sale ningún aviso hasta que entra el otro, que manda lo suyo;
  - pasado el plazo, sigue como tras una caída, y al volver sale «X ha vuelto…»;
  - al restaurar, el mando pasa enseguida al que falta; con «Continuar campaña», solo cuando entra (para Red, en la 0.11.9). Capturas en `.dev-snapshots/ui/unity/app-0118/` |
| sí | Entre el «confirm» y la acción (Gameplay, 29/10; visto en la app de las 10:27 al revisar el Tajo): al confirmar, el controlador vuelve a mover antes de que lleguen los eventos, y durante esa vuelta por la sala (menos de 0,1 s en la app) salían el alcance, el camino hasta el puntero y «Flanco» sobre los enemigos. En la 0.10.1 pasaba igual (game.ts confirmTarget → cancelMode). Ahora el soldado de la orden (`AbilityCommand`) no enseña nada de eso hasta que llega su respuesta. Lo suelta `Played` con los eventos de este jugador (también si vienen sin eventos; los del compañero no, como dijo Red), `TurnedDown` si la sala la rechaza (`RejectedMessage`, también en la pausa) o, a los 2 s, si no contesta. El aro blanco del puntero sigue. Laboratorio: `BASTION_LAB_TARDA=0.3`, la respuesta del anfitrión con ese retraso, como por la red. Capturas (antes en la app, el hueco en el editor y cómo vuelve al acabar) en `.dev-snapshots/ui/unity/confirmar/` |
| sí | La cámara al hombro al apuntar (game.ts updateAimCam y aimShot, Ana 22/10 #19 y 23/10 #22): con un enemigo a la vista elegido, la cámara va sobre el hombro del tirador (`CombatController.AimCam`, `ShotCam.OverShoulder`), más alta si desde el hombro no se ve o alguien tapa; al soltar, vuelve, salvo si ya está el plano del tiro que mata. Con la cámara al hombro (apuntando o en el tiro que mata), los rótulos se ocultan salvo el del blanco (`CloseFlags`, game.ts `close-shot`) y los edificios quedan enteros alrededor del plano (`CutFocus.close`, como `close` de buildings.ts). Laboratorio: `BASTION_LAB_SIN_DISPARO=1` (apunta y no dispara) y `BASTION_LAB_NOCHE=1` (la partida grabada, de noche). Parejas con la 0.10.1 en `copas-y-cielo/cielo-*-0101-y-unity.jpg` |
| sí | Tanda del repaso del 26/10 (UNITY-UI §5.5 y lo que faltaba de hud.ts y game.ts): clic derecho (retirar el plan, cancelar lo armado), «Cámara al soldado» (`turnCamera`), el sello que suelta cualquier clic o la rueda, «Turno enemigo» sobre la barra, la tarjeta de llegada que tapa y se cierra con Intro o Espacio, la pantalla de carga (`AppFlow.RaiseVeil`, `VeilWords`, `LiftVeil`), «Volviendo al Bastión…» y «Misión al azar». Tiras en `.dev-snapshots/ui/unity/combate-tanda1/` (laboratorio: `BASTION_TOQUES`) |
| sí | Lo que faltaba del combate (26/10): la mochila con sus órdenes por la sala (`Backpack`, `CombatController.Pack`; «Mochila llena… Cambiar»), las marcas del arma, los avisos de enemigo a la vista (`EventPlayer.AnnounceSighted`), la nota de lanzar, la hoja del soldado (H, con el tablero en campaña) y la ficha del enemigo (clic derecho) en `BattleWindow`, y el asedio: la rosa con datos, los haces y las flechas de las brechas, la línea de cada brecha (`GiveWayLine`), las barras de puertas y barricadas y su nota, y «MOCHILA DE X» sobre la de un caído (`UnitFlags.SetTags`); de noche, «A la luz: os ven de lejos» bajo el tirador al apuntar desde una casilla iluminada. Cotejado con la 0.10.1 en el asedio (`.dev-snapshots/ui/unity/asedio-combate/comparacion-0101.png`): de ahí, la distancia en todas las flechas del borde, la flecha dibujada como la de la web y el latido de las rojas, el anillo blanco bajo el puntero en cualquier casilla y la raya de color del registro. Laboratorio: `PackSteps` (pick, slot, act, floor, fallen, offer, ability, why, hover, click, look, end, sheet, file, close), `BASTION_LAB_ARCHIVO`, `BASTION_LAB_CAMPANA`, `BASTION_LAB_OP`; dos apps: `BASTION_PASOS_COMBATE` (`CombatRun`) y `BASTION_RECORRIDO_*` (`TestSteps`). Capturas en `.dev-snapshots/ui/unity/` (mochila, avisos, hoja-y-ficha, asedio-combate, prueba-campana) |

### 7.1 El estilo del HUD, aparcado (Arturo, 24/10)

El HUD de verdad se queda con la Mixta de hoy. Arturo: «vamos a dejarlo como está y ya volveremos».

- **Las cinco primeras** (Chatarra, Pavonado, Biselado, Mixta oxidada, Campaña) eran el HUD de hoy con piezas de Synty encima. Arturo dijo «ninguna»: quitadas, y no se vuelven a proponer.
- **Las tres guardadas** siguen cómo enseña Synty sus paquetes en sus escenas de muestra:
  - **Taller**, de Apocalypse HUD: la barra como un aparato con cilindros y contador, y el texto suelto sobre la escena.
  - **Mando**, de Military Combat HUD: placas azul acero, escuadra numerada, hexágonos y el arma encima de Terminar turno.
  - **Losetas**, de Modern Menus: losetas inclinadas y números en hexágonos.
- **Dónde están:**
  - las hojas de estilo, en `Assets/Bastion/UI/Propuestas/{Taller,Mando,Losetas}.uss`, generadas desde plantillas con `sprite("…")`;
  - las piezas que se añaden y sus datos, en `Scripts/Hud/Lab/Temas.cs`;
  - solo entran en las capturas del editor: `HudShots.PartidaTema` con `BASTION_TEMA=<nombre>` y la escena Partida (`BASTION_ROTULO=1` para el rótulo);
  - las capturas, en `.dev-snapshots/ui/unity/tema-{Taller,Mando,Losetas}.png` y `temas3-hoja.png`.
- **Las referencias:**
  - las escenas de muestra de los tres paquetes, capturadas en un proyecto desechable, en `.dev-snapshots/ui/unity/referencias/` (una hoja por paquete y las pantallas que importan);
  - lo que aportan las galerías de la web y el vídeo de Apocalypse, en `referencias/hoja-web.png`.
- **Lo que es 3D:** solo los iconos de Modern Menus (139 mallas, con un shader graph de URP que el paquete no trae). Apocalypse y Military son sprites.
