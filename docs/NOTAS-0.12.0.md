# Notas de la 0.12.0

Texto de Lore para la 0.12.0, con las notas de Arturo del 5/11, decididas por él en el cuadro ([PRUEBA-2026-11-05-ARTURO.md](PRUEBA-2026-11-05-ARTURO.md): las 16 notas y la §14, el movimiento aparte de las acciones, con la regla de correr: pasar de 5 en el turno). La biblioteca de recursos no va en esta versión. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.0: moverse sin gastar acciones, la cobertura frente a los zombis y sin cuenta atrás

Versión 0.12.0 (commit [commit]). Las notas de Arturo del 5/11. El combate cambia: el movimiento va aparte de las acciones.

## Novedades de la 0.12.0

### Moverse sin gastar acciones
- **Cada turno, un soldado tiene 2 acciones y 8 puntos de movimiento.** Desde Veterano, 3 acciones, como antes. Cada casilla cuesta un punto; en diagonal, 1,5.
- **Moverse no gasta acciones.** El movimiento se reparte en tramos, antes y después de actuar: mover 2, disparar y mover 3 más.
- **Ya no se cambia una acción por un segundo movimiento.** El alcance sale en una sola banda azul cielo; el amarillo desaparece.
- **Correr es pasar de 5 puntos en el turno, en un tramo o en varios.** El tramo que pasa de 5 se hace corriendo y se oye. Mover 3, disparar y mover 3 más es correr en el segundo tramo.
- **Al elegir casilla, si para llegar ya se corre, el aviso de ruido lo dice:** «Ruido 3: corre».
- **Forma física se llena con el tramo que gasta todo el movimiento,** aunque antes haya habido otros. Antes se llenaba al moverse con las dos acciones.
- **Lo que acaba el turno gasta también el movimiento que quede:** Vigilancia, Cubrirse, Esconderse o lanzar una granada.
- **Un soldado sin acciones pasa el mando aunque le quede movimiento,** y lo puede gastar más tarde.
- **El turno acaba solo cuando nadie tiene acciones ni movimiento.** Si no, «Terminar turno», que avisa si a alguien le queda algo.
- **El movimiento que queda se ve** en la tarjeta (una pieza azul por casilla), delante de las teclas («5 de 8»), en la ficha y en la hoja (H).
- **El camino que planeas sin el mando sale en gris,** para no confundirlo con el alcance.
- **Velocista, Maratón e Incansable, en el tablero, dan +1 punto de movimiento por turno** cada una. Antes, +1 casilla por acción.
- **«Correr y disparar» se llama ahora «Esfuerzo extra».** Hace lo mismo: sin gastar acción, una acción más ese turno, cada 4 turnos.
- **Las reglas de moverse, en «Controles»,** debajo de las de la noche.

### Los zombis y los vivos
- **Un zombi común tiene 1 acción y 5 puntos de movimiento:** muerde una vez por turno, no dos, y anda 5 casillas, no hasta casi 13.
- **En su turno, los zombis andan lo que pueden y muerden.**
- **Los zombis especiales, 1 acción y su paso de siempre:** el Corredor 8 casillas, el Acorazado 5, el Chillón 5 y el Hinchado 3.
- **Los vivos, como los Cuervos, tienen 2 acciones y 8 puntos, como los soldados.** Se mueven una vez y actúan. Antes se movían dos veces y disparaban de lejos.

### La cobertura también oculta de los zombis
- **Detrás de cobertura baja, un zombi ve y nota al soldado a tres cuartos de la distancia de siempre; detrás de cobertura alta, a la mitad.**
- **Cuenta la cobertura que haya entre el zombi y el soldado,** como en los disparos.
- **Esconderse no se suma a la cobertura:** escondido, a la mitad, con cobertura o sin ella.
- **Solo frente a los muertos.** Frente a los vivos, la cobertura sigue siendo para los disparos.
- **Los escudos de cobertura salen siempre,** también frente a los zombis.
- **Las reglas, en «Controles».** Esconderse y Cubrirse dicen en su ayuda en qué se diferencian: Cubrirse da defensa y esquiva, pero no oculta.

### Lo que ven los zombis
- **Al elegir dónde moverte, un ojo gris sobre cada zombi que vería al soldado allí.** Sale también sin arma de fuego.
- **«A la vista» desaparece.** El porcentaje de tiro sigue, en la misma placa que el ojo.
- **Un botón con un ojo, junto a la flecha del objetivo, o la tecla V.** Se queda puesto y enseña a la vez la vista de todos los zombis que se ven, con un ojo rojo sobre los que ya ven al soldado elegido. Con el ratón sobre un zombi, solo la suya.
- **Mientras se ve la vista de un zombi, una leyenda con sus colores:** «Lo que ve» y, con la escuadra oculta, «Aquí notaría a un soldado».

### Solo el Sabotaje tiene cuenta atrás
- **Botín, Arreglar la radio, Rescate y Saqueo ya no tienen cuenta atrás:** La Lata espera. La Limpieza ya no la tenía.
- **El Sabotaje sigue con la suya:** hay que apagar la alarma antes de que acabe.
- **Los zombis siguen entrando por los bordes,** con el ruido y unos pocos cada turno, salvo en la Limpieza.
- **Los encargos y las ayudas ya no hablan de tiempo** donde no lo hay.

### Registrar cuerpos y coches
- **El maletero está donde lo tiene el coche.** Los coches enteros de la ciudad y del pueblo, y el de policía, iban del revés: el maletero caía en el morro y los faros de noche salían por detrás.
- **Registrado, el maletero se queda abierto:** con la tapa o el portón levantados. La furgoneta abre sus puertas traseras y la grúa baja su trampilla.
- **Los coches destrozados con el maletero abierto ya no salen en la calle:** abierto quiere decir registrado.
- **Una mochila sobre el cuerpo o el coche, y su contorno,** al pasar el ratón o con «Abrir o registrar» elegido: claros si un clic lo registra, grises si no llega. Ya no se resalta la casilla ni sale texto.

### Más cosas del combate
- **Al abrir la puerta de una casa desde fuera, se ve el interior,** sin tejado. Por la puerta abierta se mira dentro como por una ventana.
- **Al hombro, se cambia de objetivo,** a distancia y cuerpo a cuerpo: Tab, Mayús+Tab, las flechas o un clic en la placa del otro objetivo, que lleva su probabilidad. El panel dice «1/3 · Tab».
- **Fuera de tu turno o sin el mando, las teclas no preparan nada:** ni el 3 de vigilar, ni Espacio, ni la barra, ni el clic en un zombi. Lo que tuvieras preparado se quita al perder el mando. Mientras, puedes planear los movimientos.
- **El borde del alcance, sin la línea negra.** Por dentro, un solo resplandor que sigue el borde por las curvas y las esquinas, sin cortes entre casillas. La vista de los zombis lleva el mismo resplandor.
- **Los perros ya no se paran en seco:** frenan al llegar y, al girarse en el sitio, mueven las patas.

### En la base
- **Los reclutas nuevos llegan con un arma al azar, como los del principio:** la mitad de las veces una pistola y, si no, un machete o una palanca.
- **Vale para todos los reclutas:** los que se reclutan en la base, los de recompensa y el gratis de quien se queda sin soldados.
- **Solo en las campañas empezadas desde la 0.11.5,** las que empiezan sin pistolas.

## Partidas guardadas

- **La campaña de la 0.11.9 sigue** en la 0.12.0, y también una misión guardada a medias. Lo que cambia al cargarla, en Avisos.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.0-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.0-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los zombis comunes son ahora más flojos:** muerden una vez por turno y andan 5 casillas. Arturo lo mirará jugando.
- **Una misión guardada a medias pierde su cuenta atrás** si no es un Sabotaje.
- **Al cargar una misión guardada a medias, cada soldado tiene el movimiento entero.**
- **Los dos jugadores, con la 0.12.0:** con la 0.11.9 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.11.9.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
