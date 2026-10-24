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

En la columna de la derecha, bajo el objetivo. Plegado enseña las 2 últimas líneas; abierto, las 30 últimas. Cada línea va en un color según el tono: daño propio, daño al enemigo, enemigos entre sí, cosas buenas.

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

## 7. Estado (24/10)

En `unity/Bastion/Assets/Bastion/UI/` (asmdef `Bastion.UI`). Compila en el proyecto con `unity/tools/unity.sh`. Detalle en su [README](../unity/Bastion/Assets/Bastion/UI/README.md). Capturas en `.dev-snapshots/ui/unity/`.

| Hecho | Pieza |
|---|---|
| sí | Vistas (`HudViews`), textos fijos (`HudText`), manejadores (`IHudHandlers` y su relevo), ayudas al pasar el ratón (`TooltipLayer`) |
| sí | Lista de la escuadra, panel del soldado (sin mochila), barra de acciones con menús y tarjeta, panel de disparo (ataque, apoyo, propia sobre su botón) |
| sí | Franja de turno con marco, jugadores y planta; aviso (sello, retenido); botones del turno con los relojes |
| sí | Tarjeta del objetivo, rosa del asedio, registro |
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
| sí | Menú del juego (Esc o «Menú»): Continuar, Ajustes (velocidad del turno enemigo, seguir las rutas planeadas), Controles con las reglas de la noche, y volver al menú, abandonar y salir, cada uno con su pregunta |
| sí | El camino (y los planes) sobre los peldaños de una escalera, a la altura de la 0.10.1 (`MapSpace.StairLift` de Arte, la misma con la que pisan las figuras); los iconos de puertas, dibujados con Painter2D (`LineIcon`); el «§» como la caja de suministros (`BastionSuministros.ttf`, fuente de reserva de Oswald y Barlow); las notas y las líneas de las banderas no se salen de la pantalla |
| sí | Rendimiento: telemetría `[rend …]` al red.log del anfitrión cada 10 s (`Perf`, de perf.ts; la cpu es el trabajo del hilo principal, con la espera de la pantalla aparte), y en Ajustes Gráficos (Altos, Medios, Bajos: los niveles de calidad de Arte), Límite de FPS y Mostrar FPS (`GameSettings`); fuera del combate, 60 fps como mucho; `BASTION_PERFILAR=<fotogramas>` saca al registro los marcadores más caros (`Perfil`) |
| sí | El suelo del mapa (en `Client/Map`, sobre MapSpace y el catálogo de Arte): `GroundMap` y `Bastion/Ground` (ground.ts: losas, bordillos, césped, asfalto con línea central, pasos de cebra, grietas, musgo; el campo y las instalaciones; el agua del canal), lo menudo (`GroundCover`: scatterCover con los mismos hashes, alcantarillas, sumideros, islas de surtidores) y la sombra de contacto horneada (`GroundOcclusion`, de groundAO.ts) |
| sí | El marco fuera de juego (`FrameArt`, `FrameView`: frameArt.ts con lo que usa de dressing.ts; el canal, la vía, la autovía, el agua y el barranco), las calles vestidas (`StreetDressing`, de dressStreets: farolas, postes y cables, señales, bocas de riego y buzones, bolsas; `GroundView.Lamps` para las luces de noche de Arte), y el foco del corte de edificios de Arte desde el controlador |
| no | En Ajustes: el sonido (cuando exista en Unity) y el tamaño de la interfaz; la hoja del soldado, la mochila y la ficha del enemigo |

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
