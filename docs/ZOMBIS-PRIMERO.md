# Los zombis primero

Decisión de Arturo del 14/10: el juego se centra en los zombis. Los humanos (Saqueadores, Cabecillas y Cazadores: los Cuervos y la Congregación) solo aparecen en las misiones que son suyas.

## Antes

- **Saqueo:** solo zombis.
- **El resto de misiones** eran sobre todo humanos: en Limpieza de dificultad 1, todos; en dificultad 3, diez humanos y cuatro zombis.
- **Botín, Sabotaje, Radio y Rescate:** además, una guarnición humana desde el principio.

## Ahora

- **Las misiones normales son de los muertos** (`podPlan`):
  - grupos de 2 a 4 zombis, con Corredores, Hinchados, Chillones y Acorazados según la dificultad;
  - los mismos cuerpos que tenían las bandas, algo más desde la dificultad 3, porque solo muerden.
- **La guarnición** de las misiones con objetivo (y de Limpieza desde la dificultad 3) es de los muertos, alrededor del objetivo (`DEAD_GARRISON`).
- **Las misiones de los vivos** (`living`) llevan dos bandas de vivos y dos grupos de muertos (`missionPods`), y su guarnición es de vivos, apostada arriba desde la dificultad 2:
  - **Rescate normal:** los Cuervos retienen a alguien;
  - **la misión final,** «El campamento de la sierra», que es de la Congregación;
  - **historia:** «La sirena», «Los inmunes» y «La Voz». «La doctora» no, porque está encerrada en un hospital lleno de zombis;
  - **encargos:** «La lista de prisioneros», «Sacar a {relative}», «Vera Lindqvist» (por su jefa, que es de los vivos), «Otto Brandt», «Ruta 50», «Cacería en la Ruta 50», «Manos» y «Las notas de Mei».
- **Los refuerzos de veteranos** que crecen con las misiones son de quien tenga la misión: zombis o Corredores; Saqueadores o Cazadores en las de los vivos.
- **Sin cambios:**
  - el Saqueo, las hordas por los bordes, la Marea y los Nidos, que ya eran de los muertos;
  - «La torre», el mapa hecho a mano de la escaramuza con plantas, con su banda arriba y sus zombis abajo.
- **«Distrito Comercial»,** el otro mapa hecho a mano, pasa a ser de los muertos (decisión de Arturo).
- **Las ofertas ya generadas** en una partida guardada conservan sus grupos. Las nuevas salen con la regla nueva.

## Qué cambia en el combate

Los zombis solo atacan cuerpo a cuerpo, y la mayoría de las misiones pasan a ser de vigilancia, puertas, pasos estrechos, ruido y munición contada ([FASE7.md](FASE7.md)), más que de cobertura. La cobertura sigue importando contra los vivos, donde los haya.

Conviene probarlo en una partida. Si se queda corto o se pasa, los números están en `podPlan` y `DEAD_GARRISON` (`setup.ts`).
