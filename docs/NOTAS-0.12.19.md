# Notas de la 0.12.19

Texto de Lore para la 0.12.19: la primera de las tres versiones con los arreglos de la revisión de lo 3D que pidió Arturo el 10/11 (tandas 1, 2 y 3, y el humo). Fuentes:
- lo elegido y el plan: `.dev-snapshots/gameplay/REVISION-3D-ARREGLOS-GAMEPLAY.md`;
- Arte: `.dev-snapshots/arte/unity/arreglos-0.12.19/LEEME.md`, con `luz-y-humo/LEEME.md` y `armas-en-mano/LEEME.md`;
- UI: `.dev-snapshots/ui/unity/0.12.19/LEEME.md`.

Lo que no va en las notas:
- **Lo que no se nota al jugar:** los laboratorios y sus medidas; los escalones de la niebla oscura (L-16: solo se rompen); el cabezazo que no era fallo (A-05, salvo el del ataque); la limpieza de documentos y el README.
- **Lo que no se ha cerrado** (sigue en las fichas):
  - el pie que resbala al frenar (A-06);
  - a quemarropa contra un Cuervo (A-15);
  - la mano al registrar en el suelo (A-18);
  - las sombras de «Bajo» y la lluvia de la tormenta, mejor pero sin llegar a la meta (L-10, L-13).

Las reglas no cambian. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.19: la cámara, las armas en la mano y la noche

Versión 0.12.19 (commit [commit]). La primera de tres versiones con los arreglos de la revisión de lo 3D que pidió Arturo el 10/11. Las reglas no cambian.

## Novedades de la 0.12.19

- **La cámara, al hombro antes del golpe o el disparo.**
  - **Antes:** el golpe o el disparo salía con la cámara aún llegando al hombro, y al volver daba un salto.
  - **Ahora:** la cámara llega, se queda quieta y entonces se golpea o se dispara. Al volver arranca y frena suave.
  - **Lo que tarda:** cada ataque visto por encima del hombro (el que mata, o un mordisco a uno de los nuestros) dura unos 0,8 s más.
- **El que muere cae con el golpe.**
  - **Antes:** se ponía rojo, volvía a quedarse quieto, otra vez rojo, y después caía. Los brazos le caían de golpe.
  - **Ahora:** un solo destello y cae, con los brazos bajando poco a poco.
  - **Un golpe con filo que no mata:** la reacción sigue a la hoja, sin pausa.
- **Se apunta al pecho.**
  - **Antes:** el disparo iba a los pies del blanco.
  - **Ahora:** al pecho, el mismo punto que al elegirlo.
- **Girar hacia un blanco de lado.**
  - **Antes:** el soldado pasaba de lado a de cara de golpe, con el arma ya arriba.
  - **Ahora:** gira en unas décimas con el arma abajo, y la sube al quedar de cara. Apuntar a un lado tarda entre 0,1 y 0,4 s más.
- **Lanzar, con un gesto nuevo.**
  - **Antes:** el brazo no se leía, y el fusil giraba siguiendo a la mano que lanzaba.
  - **Ahora:** se lanza por encima del hombro (un movimiento nuevo de Mixamo), con el fusil abajo, en la mano derecha. Las granadas duran entre 0,4 y 0,7 s menos.
  - **La piedra** se ve en la mano antes de lanzarla.
- **Curar a otro.**
  - **Antes:** el soldado se deslizaba un metro con el fusil cruzado al pecho.
  - **Ahora:** anda hasta el herido, le cura y vuelve andando hacia atrás, con el fusil abajo. Tarda 1,8 s (antes, 0,85).
- **Recargar las armas largas.**
  - **Antes:** el arma quedaba de pie delante de la cara, y al empezar se colaba un fotograma suelto con el fusil arriba.
  - **Ahora:** el cañón sube menos y la recarga empieza sin ese fotograma.
- **Más arreglos de las manos y el cuerpo:**
  - **Tras un golpe con filo,** el soldado vuelve a su postura en unas décimas, no de golpe, y el fusil ya no se le mete en el muslo.
  - **A quemarropa contra un zombi,** el soldado se echa atrás desde el primer disparo: la boca del arma ya no se mete en el blanco.
  - **El fogonazo de los fusiles con mira telescópica** sale de la boca, no de la mitad del arma.
  - **La carrera ya no acaba en seco:** el soldado frena en el último tramo. Llega en el mismo tiempo.
  - **Al registrar,** la mano va hacia lo que se registra. Tarda un poco más en levantarse.
  - **Una figura que vuelve a la vista** aparece en 0,35 s, con su sombra a la vez. Antes, un fotograma con otra postura y la sombra de golpe.
  - **Un zombi que ataca** ya no levanta la cabeza de golpe.
- **El humo, semitransparente** (lo eligió Arturo).
  - **Antes:** el humo del disparo, el de las explosiones, el polvo de la piedra y el gas del Hinchado eran bultos grises opacos.
  - **Ahora:** son nubes que dejan ver lo que hay detrás. De noche, más oscuras. La granada de humo sigue tapando.
- **La noche y la luz:**
  - **Con lluvia o nieve de noche,** las farolas se encienden y la escuadra lleva su borde de luz. Antes, el clima apagaba todo lo de noche.
  - **La noche nevada,** igual de oscura que una noche de invierno sin clima. Antes salía más clara.
  - **El borde de la escuadra de noche,** más blando y con menos motas blancas.
  - **El Mapa (la vista desde arriba),** menos lavado. De noche se leen las calles, los tejados, los coches y las farolas.
  - **En «Bajo»,** las sombras llegan más lejos.
  - **En el campo,** las farolas alumbran hacia abajo y ya no pintan de amarillo las copas de los pinos. En la ciudad alumbran como antes.
  - **Con niebla de noche,** el charco de una farola deja ver el color de las cosas. Antes lo volvía todo dorado, y alumbraba la pared en una columna.
  - **Charcos de la tormenta y el aguacero,** solo en el asfalto y lo pisado. Antes también en la hierba.
  - **La lluvia junto a la cámara:** ya no salen rayas enormes delante de la escuadra.
  - **Ventanas de noche:** en las naves, los graneros y los almacenes ya no hay ventanas encendidas, y el cristal apagado se ve más oscuro.
- **Plantas y chispas:**
  - **Cuando alguien actúa en otra planta,** se ve la planta de quien actúa. Antes se veía la del soldado elegido. Al acabar, vuelve.
  - **Un disparo fallado con una pared detrás** acaba dentro de la habitación. Antes, las chispas salían fuera del edificio.
  - **Las chispas del golpe,** solo con la red y porra del Cazador. Antes salían con cualquier filo.
- **La app del Mac vuelve a la firma de antes** (*ad hoc*). Con la firma de desarrollo, el Mac siguió preguntando por el cortafuegos y la red local en cada versión, así que se quita.

## Partidas guardadas

- **La campaña de la 0.12.18 sigue** en la 0.12.19, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.19-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.19-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.19:** con la 0.12.18 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.18.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
