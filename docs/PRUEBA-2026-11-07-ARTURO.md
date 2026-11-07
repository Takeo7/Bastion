# Notas de Arturo del 7/11 (0.12.1, Mac)

Arturo, jugando solo con la 0.12.1 en el Mac, en una campaña de dos sin Ana. Van en una 0.12.2 solo con esto, compilada en una copia limpia, aparte de lo de iOS. La captura, en `.dev-snapshots/gameplay/notas-arturo-07-11-bloqueo.png`.

| # | Nota | Quién | Cómo queda |
|---|---|---|---|
| 1 | «No se puede mover en diagonal»: no era un fallo, sino lo que quiere («lo que quiero es que no se puedan mover en diagonal») | Gameplay, UI, Lore | Abajo |
| 2 | «Se me ha bugueado y no aparecen los botones, no me deja moverlos… ni pasar turno ni nada» | Gameplay, Red | Abajo |

## 2 · Solo, sin Ana

Lo que pasó:
- **La espera:** al seguir una misión guardada sin el compañero, la sala lo esperaba 5 minutos (`Room.AwaitAbsent`, desde la 0.11.8), con sus soldados como suyos. Ana Rios y Jonas no se podían mover, y la pantalla no decía que esperaba ni cuánto.
- **Los de Arturo:** sus dos estaban en «Hoja en alto», así que no tenía nada que hacer.
- **El fondo:** pasados los 5 minutos, los soldados de Ana pasaban a Arturo, pero el fin de turno no los esperaba. `MaybeEndXcomTurn` soltaba a los de un dueño que no está, y el turno acababa con ellos sin mover. Era el fallo que la espera de la 0.11.8 tapaba.

Arturo, en el cuadro: «no esperar nunca».
- **`Room`:** fuera `AwaitAbsent`. Al seguir una misión sin el compañero, sus soldados son de quien está desde el principio. Si el compañero entra luego, cada uno manda en lo suyo, y el mando pasa a él si quien lo tiene no tiene nada suyo que mover (`SyncTurn`, `AutoPassCommand`).
- **`Rules.MaybeEndXcomTurn`:** los soldados de quien no está cuentan como los compartidos. El turno los espera hasta que quien está pulse «Terminar turno» o no les quede nada.
- **Lo que no cambia:** las esperas de quien se desconecta a media partida (30 s, o 5 min con el juego fuera de la vista).
- **Pruebas:** `RulesTests.AnAbsentPartnersSoldiersWaitForTheOneHere` y `NetEngineCampaignTests.AMissionOpenedAgainLeavesThePartnersSoldiersToTheOneHere`, que sustituye a la de la 0.11.8.

## 1 · Nadie se mueve en diagonal

Arturo, en el cuadro:
- a todos: soldados, zombis y vivos. Solo en cruz, así que 8 puntos son 8 casillas en cruz;
- el cuerpo a cuerpo y el mordisco siguen valiendo en la casilla de la esquina (`Aim.InMeleeReach`).

Hecho:
- **`Paths.ComputeReach`** anda solo las 4 direcciones de la cuadrícula. `StepCost` y `StepBetween` siguen dando su coste a la diagonal para las cuentas de los mapas (Mapgen, Furnishing, StairAccess), así que los mapas generados no cambian.
- **Las partidas grabadas**, de nuevo (23).
- **La IA, al pie de una escalera:** el Hinchado se quedaba al pie con el movimiento justo para no subir, gastaba la acción en ir a la casilla de al lado, «más cerca» en línea recta, y al turno siguiente volvía. Sin diagonales se repetía sin fin. Ahora, si ni con la acción le llega, espera al pie y sube al turno siguiente con las dos (`Ai.Walk`; `FlightsStep2Tests.TheHinchadoWaitsAtItsFoot`).
- **Las pruebas de la recuperación,** con los caminos en cruz.
- **Textos:** la ayuda del movimiento ya no dice «en diagonal, 1,5» (Lore, `Lore/Moving.cs`; UI, `HudText.Moves.Tip`).
- **De paso, un fallo del clic** que encontró UI al buscarlo: con la cámara lejos, la casilla de detrás de un soldado volvía a elegirlo en vez de mover (`CombatController.BodyUnder`, 28 px fijos). Ahora la figura se mide en el mundo.

## Para la 0.12.3: cambios de interfaz (informe del 7/11)

El informe de Arturo «Cambios de interfaz · Informe para desarrollo», del 7/11. Las dos páginas, en texto y en imagen, están en `.dev-snapshots/gameplay/notas-ui-2026-11-07/`. La maqueta final es la v39, a 2560 × 1440 px, y las medidas del informe son a esa resolución. Es de UI (con Arte en lo que se dibuja en la escena), y va en dos commits, en este orden:

**Commit 01 · La pantalla de preparación de la misión**
- **La barra de arriba:**
  - 83 px de alto, con los controles centrados en vertical; el fondo de toda la barra, #141618 al 75 %, y encima los textos, los iconos y los controles;
  - fuera la textura, el ribete naranja de abajo y el indicador de infestación entero (el icono, la barra segmentada y el valor). La barra es la misma en todas las pantallas de la base y era el único sitio con el valor de la Marea: Arturo, en el cuadro, «Llevarla al Mapa», en la columna de la izquierda, con su valor y su ayuda (UI);
  - el mismo orden: Bastión, aviso de asedio, día, recursos y Autocaravana;
  - la placa de «Menú», junto a los FPS, con el contorno desgastado.
- **«Tus soldados»:**
  - 350 × 320 px, debajo de la barra a la izquierda, con contorno rasgado y fondo negro al 75 % en vez del marco metálico;
  - el encabezado blanco, alineado con el borde izquierdo de las filas, sin la raya naranja ni los acentos morados;
  - cuatro filas de 313 × 57 px con 3 px entre ellas: el retrato, el hexágono gris y el nombre en blanco;
  - relleno irregular #535458 al 30 % en todas, a 4 px del marco;
  - en vez de los checks verdes, un marco blanco desgastado de 1 px solo en los elegidos. En la maqueta van marcados Liam, Freya y Elena, Hiro no, y el encabezado dice 4/4.
- **Los carteles bajo los personajes:**
  - fuera los círculos morados del suelo (Arte, `Settlement.Lineup`) y la línea morada de los carteles (UI, `LineupCards`);
  - de 150 × 68 a 200 × 91 px, todos a la misma altura, cada uno centrado bajo su personaje, con espacio entre las botas y el cartel;
  - fondo negro al 75 % con contorno rasgado: el nombre centrado en blanco y más grande, el rango debajo en gris claro y más pequeño, y la × pequeña arriba a la derecha.
- **El panel de la misión y el botón:**
  - el panel de abajo («El consultorio de Metsovo»), con fondo negro al 75 % y contorno rasgado; el título centrado sobre la columna de texto, los párrafos a la izquierda y el objetivo en amarillo;
  - «LANZAR MISIÓN» sigue a la derecha, en el rectángulo con muescas de 201 × 50 px de la maqueta en vez del contorno inclinado, con el mismo degradado amarillo y el texto oscuro centrado.

**Commit 02 · Las tarjetas del resto del juego**, en un commit aparte, cuando el 01 esté hecho y comprobado contra la maqueta:
- todas las tarjetas del juego, con el mismo contorno rasgado, eligiendo el sprite por las proporciones de cada una;
- el fondo, negro al 75 %; el contenido, los iconos y los controles encima, cada uno con su opacidad.

**En los dos:**
- los porcentajes van en la capa de fondo o en el relleno; los textos, los retratos, los iconos, los marcos y los botones, en capas aparte;
- los sprites: `SPR_HUD_Apocalypse_Box_Large_01` (el de la maqueta), `_Large_02`, `_Medium_01` a `_04`, `_Small_01` a `_03` y `SPR_HUD_Apocalypse_Frame_Large_03_Underlay`, de INTERFACE - Apocalypse HUD, que ya está en el proyecto (`Assets/Synty/InterfaceApocalypseHUD/Sprites/HUD/`).

## Para la 0.12.4 (notas de Arturo del 7/11, tras la 0.12.2)

| # | Nota | Quién | Cómo queda |
|---|---|---|---|
| 1 | «Que acabe el turno del soldado cuando acaba los PM y las acciones, o cuando pase el turno manualmente» | Gameplay, UI | Hoy, con un compañero, un soldado sin acciones deja de estar en juego aunque le quede movimiento, y el mando pasa al otro (`Rules.AfterSoldier`). Ahora sigue en juego mientras le quede algo (`Paths.HasTurnLeft`): el mando solo pasa cuando no le queda nada, o cuando el jugador lo cede o termina el turno. UI: el soldado sigue elegido mientras le quede movimiento |
| 2 | «Los animales al andar siguen dando tirones de casilla a casilla» | Arte | Tira de fotogramas desde la cámara del juego, como en la #7 del 5/11 |
| 3 | «Si ya tengo en la mochila, por ejemplo, piezas, y salen más al registrar, se acumulan, no ocupan otro hueco» | Gameplay, UI, Lore | Arturo, en el cuadro: «Lo que es igual, sin límite». Se juntan las piezas, los suministros, las medicinas y la comida, y los objetos y las mejoras iguales (con su cantidad); las armas no, que cada una tiene su estado. Aunque la mochila esté llena, lo que se junta cabe |
| 4 | «Lo de los turnos sigue bugueándose» (en el cuadro: «como que no deja usar puntos de movimiento, pasar turno, igual no queda claro cuando no es tu turno, no lo sé») | UI, Gameplay | La #1 arregla lo del movimiento. UI: reproducirlo jugando solo y con el compañero, y que la pantalla diga claro de quién es el turno y el mando y por qué no se puede hacer algo |
| 5 | «Los cuadrados de la cobertura, con el escudito correspondiente del color correspondiente, pero en el panel del soldado de abajo a la izquierda, no en la vida» | UI, Lore | Fuera el cuadrado de la bandera de los soldados (`uflag__cover`, provisional «hasta el sprite de un escudo»). En la ficha del soldado elegido, el escudo de su cobertura, con el mismo color y la misma regla: azul completa, amarillo media, rojo flanqueado |

Hecho en el motor (7/11):
- **#1:** un soldado sigue en juego mientras le queden acciones o movimiento (`Rules.AfterSoldier` e `InPlay` con `Paths.HasTurnLeft`). Se le deja a mano con `DoneCommand` («done»): `UnitDoneEvent` le quita lo que le queda, y el mando pasa si se alterna. `MissionBot` deja así al que solo tiene movimiento y nada que hacer.
- **#3:** `Loot.Count` para los objetos y las mejoras, y `Looting.PileFor`, `Fits`, `CountOf` y `Pile`. Al cinturón o al chaleco sale uno del montón (`Events.TakeOne`). `LootTotalsOf` cuenta cada uno.
- **Pruebas:** `ZombiesTests2.TheSamePilesInOneSlot`, y las de la mochila, el mando y el disparo, al día.
- **Terminar el turno sin el mando** (UI, en el cliente; el motor ya lo aceptaba): queda «A punto», y el turno acaba cuando acabe el compañero.
- **Pendiente de Red:** `NetCampaignRunTests`, porque la 364 ya no gana las dos con el bot.

## Para la 0.12.6 (notas de Arturo del 7/11, tras la 0.12.4)

| # | Nota | Quién | Cómo queda |
|---|---|---|---|
| 1 | «Que la opacidad de la UI sea más opaca, se ve poco el contenido» | UI | Arturo, en el cuadro: las placas rasgadas al 90 % (eran al 75 % desde la 0.12.3) |
| 2 | «No quiero que haya acciones que acaben el turno» | Gameplay, UI, Lore | Arturo, en el cuadro: Vigilancia, Hoja en alto, Zona letal (Nadie pasa), Cubrirse y Esconderse siguen acabando el turno; el resto de las de los soldados no (las granadas, el humo, la aturdidora, el molotov, el lanzagranadas, la granada del chaleco, Arreglar, Supresión, Cara a cara, Pasar visita, Hacerse sitio). Las de los muertos y los vivos, como estaban. La celda «Sin pausa» de Lanzar pasa a «Mechero»: un cóctel molotov más, si lleva (en el cuadro) |
| 3 | «Las cajas, puertas, etc., que no sean una casilla en el suelo: que se resalten con el símbolo de la acción, igual que los cuerpos y los coches» | UI, Arte | El contorno sobre el objeto (`SearchOutline`) y el icono de su acción (registrar, abrir, quitar las tablas…), sin resaltar la casilla, como desde la 0.12.0 (#12 del 5/11) con los cuerpos y los coches |
| 4 | «Quitamos el efecto del mordisco en general: solo daño; se transforman cuando se mueren» | Gameplay, UI, Lore | Arturo, en el cuadro: un mordisco o un reventón solo quitan salud; sin contagio, sin fiebre, sin perder salud cada turno, sin «con un mordisco no se sube a La Lata». Se levanta como zombi cualquier soldado que muera en la misión, lo mate quien lo mate. La Cuarentena sale de la base y se devuelve lo que costó; los soldados con fiebre de una partida guardada se curan al cargarla; fuera el tratamiento |
| 5 | «Molaría que cuando el coche salga, si hay algún zombi, lo atropelle y salga volando» | Arte | Al salir La Lata, los zombis en su camino salen volando. Es solo lo que se ve |
| 6 | «Quitamos el asedio de momento, ya lo retomaremos más adelante» | Gameplay, UI, Lore, Red | En la campaña no hay asedios hasta que Arturo lo diga: no se anuncian ni llegan, y uno anunciado en una partida guardada se quita. El código se queda, apagado |

Decidido después en el cuadro (7/11):
- **«Manos de enfermero»** (`lightHands`) paraba el contagio y se quedaba sin efecto: «Usar una medicina no gasta acción.» (`Rules.FreeMedicine`, `MedicineUsedEvent.Free`).

Hecho en el motor (7/11):
- **#2:** `Content` con `EndsTurn` solo en Vigilancia, Hoja en alto, Nadie pasa, Cubrirse y Esconderse (y en las de los enemigos: Red y porra del Cazador, Grito, Despertar a un muerto, Reventón). «Mechero» en la celda `salvo`.
- **#4:**
  - sin `Infect`: un mordisco o un reventón solo quitan salud;
  - `RiseFallen` levanta a cualquier soldado caído (no a una torreta, al VIP ni a un cautivo);
  - fuera la salud perdida cada turno y el «con un mordisco no se sube a La Lata»;
  - nadie vuelve con fiebre;
  - `Base.Retired` (la Cuarentena) y `PlaceBlocker`;
  - `CampaignVersion` 27 cura la fiebre, desmonta la Cuarentena y devuelve lo que costó.
  - Pruebas: las de la fiebre en la base, fuera; las del combate, la Cuarentena y las medicinas, reescritas.
  - La Enfermería de nivel 2 hacía que las medicinas pararan el mordisco: se queda sin ese efecto.
- **#6:**
  - `Campaign.SiegesOn` en false: el reloj no corre, no se anuncia ni empieza, y `NextSteps` calla;
  - la versión 27 quita la oferta de un asedio anunciado, no la del que se juega;
  - las pruebas de los asedios de campaña, saltadas hasta que vuelvan.
- **Pendiente de Red:** `NetCampaignRunTests` (esperaba «Asedio» de segunda misión).
