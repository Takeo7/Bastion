# Revisión de UX 2: combate, cooperativo y el bucle entero

**Estado (2026-10-06):** aplicados 22–29, 31 y 34 (sesión UX/UI, con el visto bueno de Gameplay), probados en el juego con dos jugadores. El resto sigue pendiente. Sigue a [UX-REVISION.md](UX-REVISION.md), que revisó los menús y la base y ya está aplicada. La numeración sigue desde el 22.

Cómo quedó lo aplicado:

- **22:** los botones «listo» siguen verdes con el ratón encima.
- **23:** el botón dice solo la habilidad («Disparar», «Tajo»), y el panel ya no se sale a 1000 px.
- **24:** en solitario no hay estado junto al nombre; a dos, «Con acciones» en vez de «Espera».
- **25:** el menú dice «Día 5 · Nivel 1 · Actividad enemiga 4/12». El servidor ya envía el nivel en `savedCampaign`.
- **26:** las etiquetas van dentro de su sala.
- **27:** el punto del compañero va en la esquina de la pestaña.
- **28:** con soldados con acciones, Terminar turno pasa a «¿TERMINAR? · 3 soldados con acciones». El aviso dice qué pasa después («el mando pasa a Ana» o «juega el enemigo»). Hay que pulsar otra vez antes de 4 s; Esc o cualquier otra orden lo cancelan.
- **29:** el relleno dice el coste y un borde rojo marca dónde os descubren. El primer clic en esas casillas avisa y el segundo mueve.
- **31:** el turno, el objetivo, los jugadores, la lista y la ficha del soldado se pintan con lo ya animado. Medido: las bajas salen en la lista al verse caer, 3,5 y 13 s después de llegar del servidor.
- **34:** sin enemigos que lo vean, el escudo solo aparece si el soldado está a cubierto.
- **Además:**
  - Gameplay impide ceder el mando a un compañero sin acciones; antes se quedaba atascado con él.
  - «Refuerzos en camino» ya no se corta en la barra superior. Si no cabe, baja a una segunda línea bajo el objetivo; antes, a 1280 px, el objetivo desaparecía y el aviso quedaba en «REFUERZOS EN C».

Revisión hecha jugando como un jugador nuevo, primero en solitario y luego con un compañero en otro navegador, a 1280 × 760 y a 1000 × 663 (la ventana del usuario). Se usó un servidor de pruebas aparte, sin tocar `data/save.json`. Recorrido:

- **En solitario:** menú → campaña nueva → radio → geoesfera → escuadra → misión (turnos 1 y 2) → abandonar → informe → base y cuartel.
- **En cooperativo:** entra el compañero a mitad de campaña → propuesta de obra → escanear a dos → misión cooperativa con el mando → armería y progreso → escaramuza.

Capturas: [combate](ux/revision2-combate.jpg) y [cooperativo y base](ux/revision2-coop-base.jpg).

## Veredicto

La revisión anterior se nota. Siempre se sabe qué toca: el bloque «Ahora» y las marcas de la barra lo dicen. Las propuestas y el mando se entienden sin explicación. De «Nueva campaña» a la primera orden en combate hay **5 clics**.

Lo que falla ahora está en tres sitios:

1. **El combate no enseña ni protege.** Las reglas que se ven en pantalla solo están en el README: colores de casilla, ocultación, escudos y puntos de acción. Las dos decisiones más caras se toman sin aviso, con un clic o una tecla: romper la ocultación y terminar el turno con soldados sin mover. En la primera misión de la prueba pasaron las dos cosas. Un clic en una casilla roja hizo una carrera y rompió la ocultación. Luego **Retroceso** terminó el turno con cuatro soldados sin mover. Resultado: una baja permanente en el turno 2.
2. **La información llega antes de tiempo o fuera de cuadro.**
   - La lista de soldados y el contador de turno muestran el resultado antes de la animación: la baja sale tachada mientras aún está el cartel «Turno enemigo».
   - La cámara empieza en la esquina del mapa, con los enemigos ya visibles fuera de cuadro.
   - Ninguna flecha dice dónde está el objetivo.
3. **En cooperativo, el que espera ni juega ni ve.**
   - Sin el mando no puede ni planear con sus soldados.
   - El contacto que provoca su compañero pasa fuera de su cámara.
   - Quien entra a mitad de campaña aterriza en el informe de una misión ajena.
   - Además, le quita al otro sus únicos soldados sanos sin decir cuáles.

## Lo que ya funciona (no tocar)

- **«Ahora» y la agenda**, con las marcas en la barra: siempre hay un siguiente paso.
- **Propuestas:**
  - la franja amarilla con Aceptar y Rechazar;
  - «Retirar» para quien propone;
  - «Ahora» en rojo para el compañero.
- **Presencia del compañero:** dónde está, arriba a la derecha, y su punto en la barra.
- **El mando:** «MANDO: ANA · Espera a que te lo ceda» no deja dudas, y «Ceder el mando (C)» está donde se busca.
- **El ritmo de cada acción:** cámara de disparo, «FALLO» flotante y registro con el porcentaje.
- **El panel de disparo** desglosa los modificadores (cuando cabe, ver #23).
- **La vista previa del destino** muestra el camino y el escudo de cobertura.
- **El informe en el hangar** pone el memorial en el sitio del caído.
- **Las confirmaciones** de salir al menú y de abandonar la misión.
- **La radio** no bloquea, y se avanza o se cierra con un clic.

## El bucle, paso a paso

| Paso | Coste | Fricciones |
|---|---|---|
| Menú → campaña nueva | 2 clics | 43 (escaramuza) |
| Base: «Ahora» → geoesfera | 1 clic | 40 |
| Geoesfera → informe de operación | 1 clic | 42 |
| Escuadra → lanzar | 1 clic (2 jugadores: los dos) | 22, 39, 47 |
| Despegue y presentación | ≈ 10 s | — |
| Combate | — | 23, 24, 28–36, 50, 51 |
| Resultado → informe del hangar | 2 clics | 41 |
| Vuelta a la base | 1 clic | 37, 40 |

## Problemas, por prioridad

### P0 · Fallos (arreglos pequeños, verificados en el código)

| # | Dónde | Qué pasa | Propuesta | Quién |
|---|---|---|---|---|
| 22 | Botones «listo» (Escanear, Lanzar misión) | Al pulsarlos, el ratón sigue encima. `.x-btn:hover:not(:disabled)` ([strategy.css:230](../packages/client/src/strategy/strategy.css)) pone el fondo blanco y gana a `.x-btn.ready` (texto blanco). El botón se queda **en blanco**: no se lee «Escaneando… Esperando a Beto». | Que el hover no cambie el fondo de `.ready` (o un hover verde más claro). | Arte |
| 23 | Panel de disparo | `.shot-panel` mide 214 px ([styles.css:1056](../packages/client/src/styles.css)) y su contenido, 231 px. El botón repite el título («DISPARAR · FUSIL DE PRECISIÓN», [hud.ts:334](../packages/client/src/ui/hud.ts)) y empuja «Cancelar» fuera de la pantalla. También se cortan los **valores de los modificadores** y la flecha del siguiente objetivo, a 1280 y a 1000 px. | Botón «DISPARAR» a secas (el arma ya está en el título); `minmax(0, 1fr)` en la rejilla del panel. | Gameplay (texto) + Arte |
| 24 | Barra superior del combate, en solitario | «Ana (tú) **Espera**» mientras eres tú quien juega ([hud.ts:208](../packages/client/src/ui/hud.ts): con acciones = «Espera»). | En solitario no mostrar estado. En cooperativo, «Con acciones» en vez de «Espera». | Gameplay |
| 25 | Menú principal · Continuar | «Día 1 · **Proyecto** 2/12» ([lobby.ts:170](../packages/client/src/ui/lobby.ts)): en el resto del juego es «Actividad enemiga» y luego «la Partitura». | Usar `doomName()` de lore.ts. | Gameplay |
| 26 | Instalaciones, vista 3D | Las etiquetas van ancladas al techo de cada sala y, con este encuadre, se leen como de la fila de arriba. «Sala vacía» cae sobre Mando y Progreso; «Roca», sobre las salas vacías. | Anclar la etiqueta al suelo, dentro de la sala. | Arte |
| 27 | Barra de navegación | El punto de color que marca dónde está el compañero tapa el número de pendientes (Geoesfera «4», Instalaciones «1»). | El punto a la izquierda del rótulo y el número a la derecha. | Arte |

### P1 · Combate: errores caros sin aviso, información a destiempo

| # | Dónde | Qué pasa | Propuesta | Quién |
|---|---|---|---|---|
| 28 | Terminar turno | **Retroceso** o el botón terminan el turno al instante aunque queden soldados con acciones. Retroceso se pulsa sin querer (es «borrar» y «atrás»). En la prueba: cuatro soldados sin mover, una granada y una baja permanente. | Si quedan acciones, la primera pulsación cambia el botón a «¿Terminar? 4 soldados con acciones · Retroceso otra vez». Es una segunda pulsación, sin ventana. Sin acciones pendientes, como ahora. | Gameplay + Arte |
| 29 | Mover estando oculto | El rojo («te descubrirían») **sustituye** al azul o al amarillo: no se sabe si es una acción o una carrera. Un clic basta: carrera, ocultación rota y contacto. | (a) El color de la casilla sigue diciendo el coste, y la detección va aparte: borde rojo o un ojo en la casilla. (b) Si el destino rompe la ocultación, el primer clic enseña el camino con «Romperás la ocultación» y el segundo mueve. Solo en ese caso. | Gameplay + Arte |
| 30 | Todo el combate | Ninguna pantalla explica los colores de casilla, los escudos (alta, baja, flanqueado), «Ocultos/Detectados», los puntos de acción ●○ ni el mando. Solo el README. | (a) Una leyenda al pasar el ratón por el área de movimiento: «Azul: 1 acción · Amarillo: carrera · Rojo: te ven». (b) **Avisos de primera vez por radio**: Morse explica cada regla la primera vez que importa. Casos: misión oculta, primera casilla roja, primer contacto, primer flanqueo, primer herido, primera vez con el mando. Se guardan por navegador, como las transmisiones vistas. Es diegético y encaja con el Expediente. | Lore (textos) + Gameplay (disparadores) |
| 31 | Turno enemigo (y cualquier acción) | El HUD lee el estado del servidor, no lo ya animado (`refreshHud` usa `this.state`, [game.ts:1316](../packages/client/src/game/game.ts); se llama al llegar los eventos, [game.ts:292](../packages/client/src/game/game.ts)). Con el cartel «Turno enemigo» aún en pantalla, la lista ya tacha a Elena, baja la salud de los demás y arriba pone «Turno 2 · 11 turnos». La granada aún no ha salido. **Destripa el resultado** y va contra el ritmo «ver cada acción antes de seguir». | El HUD (turno, lista, salud, acciones) se pinta con `this.view`. La barra de habilidades puede seguir con `state` para no bloquear órdenes. | Gameplay |
| 32 | Inicio de misión | La escuadra aparece en una esquina del mapa (0,0 o 35,35) y la cámara se centra en ella: ¾ de la pantalla vacía. En la primera misión ya había dos enemigos a la vista, fuera de cuadro. | Plano de apertura con la escuadra, los enemigos visibles y la dirección del objetivo. O, como XCOM 2, una pasada corta escuadra → objetivo → escuadra durante la presentación. | Gameplay |
| 33 | Objetivo | El haz del objetivo existe, pero a zoom normal queda fuera de pantalla y nada indica hacia dónde ir. El objetivo en la barra se corta a 1280 px («Recuperad los libros de…»). | Flecha al borde de la pantalla con la distancia en casillas (objetivo y evacuación), y una tecla (**O**) para centrar en el objetivo. El texto del objetivo, entero (en dos líneas como a 1000 px) o al pasar el ratón. | Gameplay + Arte |
| 34 | Escudo sobre cada soldado | Sin enemigos mirando, cada soldado al descubierto lleva el escudo **rojo «Flanqueado»** ([game.ts:969](../packages/client/src/game/game.ts)). Al empezar la misión, oculta, la escuadra entera está en rojo. Es falso (nadie flanquea) y gasta el rojo. | Sin enemigos que lo vean: sin escudo, o gris. Rojo solo si un enemigo visible lo flanquea. | Gameplay + Arte |

### P1 · Cooperativo: el que espera ni juega ni ve

| # | Dónde | Qué pasa | Propuesta | Quién |
|---|---|---|---|---|
| 35 | Sin el mando | Barra apagada, sin área de movimiento ni porcentajes: el que espera solo mira. | **Planear sin el mando**: elegir tus soldados, ver su alcance, el porcentaje contra cada enemigo y dejar un **camino fantasma** que el compañero ve (la presencia ya se envía). Las órdenes siguen siendo solo de quien tiene el mando. La espera pasa a ser planificación conjunta, que es el corazón del cooperativo. | Gameplay |
| 36 | Contacto provocado por el compañero | Beto mueve a Julia y activa un grupo. Ana solo ve «Contacto: Impreso, Impreso, Recolector» en el registro y su cámara sigue en la salida. | Mantener la regla (la cámara no sigue al compañero), pero en los momentos clave (contacto, baja, ocultación rota, objetivo cogido) dar un aviso con «**Ver (F)**» y una flecha al borde hacia el sitio. | Gameplay |
| 37 | Entrar a mitad de campaña | Beto entra y aterriza en «MISIÓN FRACASADA», el informe de una misión que no jugó. Julia y Adrián, las dos únicas reservas sanas de Ana, pasan a ser de Beto. A Ana solo le llega una línea de registro: «4 soldados pasan a su mando». | Quien entra va a la base, con una ficha de bienvenida: día, nivel, actividad enemiga y lo pendiente. A quien ya estaba, un aviso con los nombres. Mejor aún: reclutas nuevos para el que entra, o que elija el que ya estaba. | Gameplay + Lore (bienvenida) |
| 38 | Propuestas | «Ana propone construir Enfermería en la sala 1-1 · Aceptar · Rechazar», sin coste, plazo ni efecto. «Ana propone ir a la misión X»: el globo no se mueve y el informe no se abre. Quien decide sabe menos que quien propone. | La franja con una línea de datos («60 ₡ · 6 días · las heridas se curan el doble») y «Ver». Si es una misión, abrir su informe al compañero. | Gameplay + Arte |
| 39 | Escuadra con huecos | Ana solo tenía un soldado sano: escuadra 4/6, con dos huecos «Libre». Beto tenía a Marta Soler sin usar y no podía ponerla. | Si un jugador no llena su parte, el compañero puede ocupar los huecos. En cada hueco libre de tu parte, «Reclutar (30 ₡)». | Gameplay |

### P2 · Orientarse y entender las consecuencias

| # | Dónde | Qué pasa | Propuesta |
|---|---|---|---|
| 40 | Base, tras un desastre | Con 5 heridos y 1 caído, «Ahora» sigue diciendo «Elige una operación», y la agenda no menciona las heridas. | `nextSteps` mira la disponibilidad. Por ejemplo: «Solo 1 soldado listo: recluta (30 ₡) o escanea para que se curen (8 d)». Y en la agenda, «Heridos: 5 · vuelven el día 10». |
| 41 | Informe del hangar | «HERIDO · 8 días» no dice qué implica, y el sello «CAÍDO» tapa «Caído en combate». | Una línea de consecuencia («5 heridos: no podrán desplegar hasta el día 10»). El sello, desplazado sobre la figura o el memorial. |
| 42 | Aviso de rumor | Sale «Asia Oriental», pero el globo no gira hasta allí. «Proponer escuchar · 5 d» en el aviso y «10 d» en la lista: dos plazos con la misma unidad. | Llevar el globo a la región al saltar el aviso, como XCOM 2. Escribir «Escuchar: 5 días · caduca en 10». |
| 43 | Escaramuza | «Empezar escaramuza» queda bajo el pliegue: y = 755 con 760 de alto, y más abajo a 663. | Botón fijo al pie de la hoja, o mapa y escuadra en una fila. |
| 44 | Instalaciones · construir | Se cortan las descripciones de lo que **sí** puedes construir («Los accesorios…», «Las heridas se curan el…»), pero las bloqueadas se leen enteras. | Botón más estrecho («Proponer · 60 ₡») y la descripción entera. Las bloqueadas, en una línea. |
| 45 | Armería | «2/2», «1/1», «0/0» sin rótulo junto al precio: «Granada de humo 0/0 · 25 créditos» parece agotada. | «Tienes 2 (2 libres)», o nada si no tienes ninguna. |
| 46 | Clases | ASL, GRN, FRT y ESP en todas partes, también en los botones de reclutar; FRT no es obvio. | Icono de clase y nombre completo al pasar el ratón; en Reclutar, el nombre entero. |
| 47 | Selección de escuadra | Las fichas solo dicen clase, rango y dueño: ni accesorios ni munición. Para ponerle una granada a alguien hay que volver al Cuartel. | Utilidad y munición en cada ficha, cambiables desde ahí (como XCOM 2). |
| 48 | Colores | En combate, el rojo es a la vez enemigo, casilla de detección, soldado propio flanqueado (#34), el botón de terminar turno y las alertas. En los menús es la acción principal (Desplegar) y también la peligrosa (el borde de «Cancelar misión»): nada destaca. | Sin tocar la paleta del Expediente: en combate, rojo solo para «peligro para ti». Las acciones destructivas, con el estilo de texto, no con el del sello. |
| 49 | Legibilidad y accesibilidad | El texto va casi todo a 11–13 px y las etiquetas a 10, en letra de máquina sobre papel. Los colores de casilla solo se distinguen por el color. | «Tamaño de la interfaz» en Ajustes (90–130 %, una variable CSS en `:root`). Mínimo 12 px. Un patrón además del color: rayado en la carrera, ojo en la detección. |
| 50 | Enemigo al pasar el ratón | Solo nombre y salud. El porcentaje no se ve hasta entrar a apuntar, y nada dice qué hace cada enemigo. | Con un soldado elegido, al pasar por un enemigo: «72 % · crít. 18 %». Clic derecho: ficha del enemigo con texto de Lore. |
| 51 | Destino al pasar el ratón | Se ve el escudo del destino, pero no **a quién verías ni a quién flanquearías** desde allí. Es la decisión más repetida del juego («¿adónde muevo?») y le falta su dato clave. | Al pasar por una casilla, marcar los enemigos visibles desde ella, con una señal amarilla si quedan flanqueados (como XCOM 2). |

Quién: Arte para 41–46, 48 y 49; Gameplay para 40, 47, 50 y 51, con Arte en lo visual; Gameplay y Lore para 42 (el giro del globo es de Arte).

### P3 · Pulido

- La confirmación de abandonar dice «termina para los dos jugadores» también en solitario.
- «Volver a la base» de uno saca a los dos de la pantalla de resultado, aunque el otro aún la esté leyendo.
- Las ayudas son `title` del navegador: tardan un segundo y no siguen el estilo. En combate deberían salir al momento, con el estilo del juego.
- «Evacuar» ocupa un hueco en la barra aunque el mapa aún no tenga zona de evacuación.
- En la selección de escuadra, «Tuyo» en cada ficha sobra en solitario.
- La marca «4» de Geoesfera suma operaciones y regiones por contactar; se entiende mejor contando solo operaciones.

## La regla, en combate

La revisión anterior pedía que cada pantalla respondiera a tres preguntas. En combate, cada momento tiene que responder a otras tres:

1. **¿Qué ha pasado?** En orden, y nunca antes de que se vea (#31, #36).
2. **¿A quién muevo y qué le queda?** Soldado, acciones y lo que no ha hecho aún (#28, #35).
3. **¿Qué gano y qué arriesgo si hago esto?** Antes de confirmar: coste, cobertura, quién te ve y a quién ves (#29, #34, #50, #51).

| Momento | Qué tiene que verse | Qué sobra |
|---|---|---|
| Inicio | Escuadra, enemigos ya visibles y flecha al objetivo | Escudos rojos en todos |
| Elegir destino | Coste (color), detección (marca aparte), cobertura del destino, enemigos visibles desde allí | — |
| Apuntar | Porcentaje, desglose entero, Disparar y Cancelar | El arma repetida en el botón |
| Turno enemigo | Cada acción, y el HUD que avanza con ella | Resultados por adelantado |
| Sin el mando | Tus soldados, su alcance y el camino que planeas | La barra entera apagada |
| Terminar turno | Cuántos soldados tienen acciones | — |

## Orden recomendado

1. **Fallos** (22–27): media hora, y quitan rarezas visibles.
2. **No perder soldados por la interfaz** (28, 29, 31, 34): es lo que más castiga a un jugador nuevo en una campaña con muerte permanente.
3. **Orientarse** (30, 32, 33, 40): los avisos de Morse convierten las reglas del README en parte de la historia.
4. **Cooperativo** (35–39): el planear sin el mando (35) es el cambio que más valor añade al juego a dos.
5. **El resto** (41–51 y P3).

## Reparto

- **Gameplay** (lógica y estructura del HUD):
  - fallos: 24, 25 y el texto de 23;
  - combate: 28, 29, 31–34 y los disparadores de 30;
  - cooperativo: 35–39;
  - orientarse: 40, 47, 50, 51.
- **Arte** (aspecto y pantallas): 22, la rejilla de 23, 26, 27, la parte visual de 28, 29, 33, 34 y 38, 41–46, 48, 49 y el giro del globo de 42.
- **Lore** (textos): los avisos de Morse de 30 y la bienvenida de 37.

## Cómo repetir la prueba

- `.claude/launch.json` (local, no versionado) tiene `bastion-ux-server` (puerto 3330, guardado en el scratchpad) y `bastion-ux-client` (puerto 5197, Vite apuntando a ese servidor).
- Dos pestañas, `?perfil=ux1&debug` y `?perfil=ux2&debug`, hacen de dos jugadores.
- Con el panel del navegador oculto, las capturas salen de un Chrome sin interfaz manejado por el protocolo de DevTools (script en el scratchpad de esta sesión).
