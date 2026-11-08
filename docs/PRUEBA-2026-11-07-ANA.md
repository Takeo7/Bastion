# Notas de Ana del 7/11 (jugando la 0.12.4)

Informe de Ana, «Bastion-informe-desarrollo-consolidado-2026-11-07.pdf», 22 puntos con capturas. Arturo lo pasó el 7/11. Las páginas, en texto y en imagen, están en `.dev-snapshots/gameplay/notas-ana-07-11/` (p1…p10). Van en la 0.12.8; la 0.12.7 (los tirones del combate) sale antes.

## Interfaz de combate (UI)

| # | Pág. | Qué pide | Quién |
|---|---|---|---|
| 01 | p2 | Vida y visión del zombi en un solo indicador sobre `SPR_FantasyMenus_Menu_Button_08_Background`: la parte alargada, la vida; en el círculo, el ojo, abierto si ve al jugador y cerrado si no. | UI |
| 02 | p2 | Los escudos de cobertura con `Image_Apocalypse_Icon`. | UI |
| 03 | p2 | En la ruta, un solo icono de ruido en vez de «Ruido 3: corre». | UI |
| 04 | p3 | El ojo y la flecha con el marco `SPR_HUD_Apocalypse_Map_Circle_Large_02`; la flecha, sin el tinte naranja, con `SPR_HUD_DarkFantasy_Reticle_Arrow_Small_08_Variant_02`. | UI |
| 05 | p3 | Un solo botón de «Terminar turno»: quitar el duplicado. Arturo (cuadro, 7/11): sí salen dos a veces, el ✓ «Terminar el turno de Yuki» y el →| «Terminar turno», cuando a un soldado le queda movimiento y no acciones. Queda uno: mientras un soldado está en juego, dice «Terminar el turno de Yuki» y el mando pasa; sin nadie en juego, «Terminar turno» para todos. La flecha del objetivo de abajo a la derecha no es un botón de terminar: se queda, con la 04 y la ayuda «Hacia el objetivo». | UI |
| 06 | p3 | Lo naranja de la interfaz común pasa a blanco (textos, iconos, bordes, resaltados). El morado y el naranja quedan solo para los jugadores y lo que es de cada uno. | UI |
| 07 | p3 | Movimiento en una línea: «8» grande y azul, «/8» pequeño y gris, y la huella. | UI |
| 08 | p4 | «TU TURNO» y «TURNO ENEMIGO» como el cartel «LEVEL UP»: fondo negro con textura sutil, líneas blancas finas arriba y abajo, título blanco centrado en mayúsculas, sin la flecha amarilla. | UI |
| 09 | p4 | Una sola barra de tiempo abajo: cada jugador ve la suya. | UI |
| 10 | p4 | Fuera el cartel «Lo que ven» al mostrar la vista de los zombis. | UI |
| 11 | p4 | Fuera lo del «contacto», heredado de XCOM. Arturo (cuadro, 7/11): solo los avisos, es decir, la línea «Contacto: …» del registro, el golpe de música «¡Contacto!» y la cámara que va al grupo que despierta. Las reglas no cambian: los zombis siguen quietos hasta que ven u oyen, los vivos se siguen poniendo a cubierto y la escuadra sigue empezando oculta. | UI |

## Panel de soldados (UI)

| # | Pág. | Qué pide | Quién |
|---|---|---|---|
| 12 | p5 | En el panel de soldados, también una tarjeta con nombre y retrato por cada personaje del compañero: Ana ve solo los suyos y no los de Arturo. | UI |
| 13 | p5 | El nombre en su franja bajo el retrato; el retrato sobre un fondo de estudio del color del jugador. La ficha de arriba en una fila con separadores: nombre y rango, arma, munición, valor en rojo, estadísticas y la mochila a la derecha. Los bolsillos solo al pulsar la mochila, y más grandes. La tarjeta elegida, sin borde naranja: más grande. | UI |

## Movimiento y visibilidad

| # | Pág. | Qué pide | Quién |
|---|---|---|---|
| 14 | p6 | Un zombi junto a la puerta de un edificio sin descubrir se ve (modelo e indicadores) al detectar a un soldado de fuera. Mientras esté en una zona sin descubrir, no se ve. | Arte (UnitViews) y UI (indicadores); Gameplay si es del motor |
| 15 | p6 | Las líneas de movimiento del interior, solo si el edificio está descubierto y el elegido está dentro, en esa planta; sobre el suelo visible cuando se corta la pared. Ahora se dibujan sobre la fachada desde la calle. | UI (MoveOverlay); Arte da lo descubierto y el corte por plantas |
| 16 | p7 | La línea azul, a ras del suelo y tapada por los coches que la tapen desde la cámara. El contorno rojo de la vista de los zombis, solo el perímetro de fuera: sin bucles alrededor de objetos ni recuadros sueltos (los bolardos). Arturo (cuadro, 7/11): «Quiero que rodeen esos obstáculos, no que queden dentro del área pisable»: el contorno azul del movimiento rodea los coches y no los deja dentro. La línea sigue por encima de los objetos y solo la tapan las personas (Arturo, 31/10 #3). | UI (MoveOverlay, AimOverlay.ShowVision); Gameplay si las casillas del coche se pueden pisar en el motor |

## Escenario e inicio

| # | Pág. | Qué pide | Quién |
|---|---|---|---|
| 17 | p8 | Las ventanas que faltan en la primera planta; las del lateral del patio de la fuente, repartidas con separaciones regulares. Arturo (cuadro, 7/11): «En columnas». En cada lado, las mismas columnas en todas las plantas, cada 3 en una vivienda y cada 2 en lo demás, centradas y con márgenes iguales; en la planta baja también, salvo en la puerta y a su lado, o donde fuera hay pared. Los tabiques se apartan de las columnas. Cambian los mapas nuevos. Para la 0.12.9. | Gameplay (motor: `WindowColumns`, Mapgen) y Arte (`WallLayout.Storeys` con la misma función) |
| 18 | p8 | La esquina de ladrillo continua (sin la franja gris) y la puerta encajada en su hueco. | Arte |
| 19 | p9 | Nieve en tejados y azoteas, como la de las calles y los coches. | Arte |
| 20 | p9 | Al empezar una partida nueva, «Escuadra» es la pestaña de inicio, ya con el mensaje de introducción. | UI |
| 21 | p10 | Fuera el cartel «EN MARCHA» sobre La Lata al salir del campamento. | UI |
| 22 | p10 | El modelo de La Lata, solo para La Lata: fuera de los coches de las ciudades y de los demás mapas. | Arte |

Los textos nuevos, con Lore.
