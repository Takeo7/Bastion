# Notas de la 0.12.8

Texto de Lore para la 0.12.8, con las notas de Ana del 7/11 y lo que decidió Arturo en el cuadro ([PRUEBA-2026-11-07-ANA.md](PRUEBA-2026-11-07-ANA.md)), todas menos la 17, que va en la 0.12.9:
- la interfaz del combate: la placa del zombi, una sola tecla para terminar, lo naranja en blanco, los carteles de turno y sin los avisos del contacto (UI);
- el panel de soldados, con los del compañero (UI);
- lo que se ve dentro de los edificios y los contornos (UI y Arte);
- el escenario: la esquina de ladrillo, la puerta, la nieve en los tejados y el 4×4 de La Lata (Arte);
- la base al empezar (UI).

Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.8: la interfaz que pidió Ana, una sola tecla para terminar y nieve en los tejados

Versión 0.12.8 (commit [commit]). Las notas de Ana del 7/11, con lo que decidió Arturo. Las reglas no cambian.

## Novedades de la 0.12.8

### La placa del zombi
- **La vida y la vista, en un solo indicador:**
  - en la parte alargada, una pieza roja por cada punto de vida;
  - en el círculo, el ojo: abierto y rojo, ve al soldado elegido (o lo vería en la casilla a la que vas, al planear un movimiento); cerrado y gris, no lo ve.
- **El porcentaje de acierto sigue encima,** y las etiquetas (VIGILANCIA…), detrás. Los soldados conservan su placa.

### Una sola tecla para terminar
- **Fuera el ✓ «Terminar el turno de Yuki»:** a veces salían dos teclas de terminar.
- **Con un soldado en juego,** «Terminar turno» (o Retroceso) termina el turno de ese soldado: pierde lo que le quede y, con el compañero, el mando pasa al otro.
- **Pulsada otra vez,** termina tu turno sin volver a preguntar. Con el compañero, quedas «A punto».
- **Sin nadie en juego,** «Terminar turno» va como antes: si a alguien le queda algo, pregunta.
- **La flecha de abajo a la derecha no es una tecla:** señala hacia el objetivo, y la O lleva la cámara allí. Con el puntero encima sale su ayuda.

### Lo que se ve en el combate
- **Lo naranja, en blanco:** textos, bordes, resaltados, el botón principal y el dial de acierto, en el combate y en la base.
  - El morado y el naranja quedan para los jugadores y lo de cada uno. El morado que no era de nadie (la marca de nivel 3, la voluntad, la etiqueta psíquica) también pasa a blanco.
  - Se quedan los colores que dicen algo: la cobertura (azul, amarillo y rojo), la salud en verde, el peligro en rojo, los de los suministros y la comida, y las acciones en amarillo.
- **Los escudos de cobertura,** con otro dibujo y una sombra detrás, para que se vean sobre la nieve. Los colores, los mismos.
- **El ruido de la ruta** es un icono, un altavoz con ondas, en vez de «Ruido 3: corre».
- **El botón del ojo y la flecha del objetivo,** en un círculo negro. La flecha, en blanco.
- **El movimiento, en una línea:** «8» grande y azul, «/8» pequeño y gris, y una huella.
- **Los carteles de turno** («TU TURNO», «TURNO ENEMIGO»), como el «LEVEL UP» que propuso Ana: fondo negro, una línea blanca fina arriba y otra abajo, y el título blanco en mayúsculas. Los de peligro («¡SE LEVANTA!», «¡ALARMA!»…), igual, con el título en rojo.
- **Una sola barra de tiempo:** cada uno ve la suya, bajo las teclas. La fina del compañero ya no sale.
- **Al encender la vista de los zombis** ya no sale el cartel «Lo que ven». Lo que significa el rojo está en la ayuda del botón del ojo.

### Sin avisos de contacto
- **Fuera los avisos de contacto, heredados de XCOM:**
  - la línea «Contacto: …» del registro;
  - el cartel «¡CONTACTO!» con su golpe de música;
  - la cámara que iba al grupo que despierta.
- **Las reglas no cambian:** los zombis siguen quietos hasta que ven u oyen, los vivos se siguen poniendo a cubierto y la escuadra sigue empezando oculta.
- **Se quedan** la «!» sobre los que se dan cuenta, que se giren hacia la escuadra y la música tensa.

### El panel de soldados
- **Las tarjetas del compañero,** con su nombre y su retrato, después de las tuyas. Un clic lleva la cámara a ese soldado; solo se elige si lo mandas tú.
- **El retrato,** sobre un fondo del color de su jugador (morado o naranja), con el nombre en una franja debajo.
- **La ficha del elegido,** en una fila encima de las tarjetas, con la mochila a la derecha.
- **Los huecos de la mochila** solo se ven al pulsarla, y son más grandes.
- **La tarjeta elegida,** más grande y sin borde.

### Dentro de los edificios
- **Un zombi dentro de un edificio sin descubrir ya no se ve:** ni la figura, ni su placa, ni la «!».
  - **Antes:** si veía a un soldado de fuera, se enseñaba y se quedaba dibujado.
  - **Ahora:** se ve cuando sale o se asoma al umbral de la puerta, o cuando el edificio está descubierto.
- **El alcance del movimiento dentro de un edificio** solo se dibuja con el elegido dentro, en esa planta, y con el edificio descubierto. Va sobre el suelo, con las paredes cortadas. Antes salía sobre la fachada, visto desde la calle.

### Los contornos
- **El azul del movimiento rodea los coches, las cajas y las consolas,** y los deja fuera de lo que se pisa. Antes los metía dentro. Las personas siguen dentro.
- **El rojo de la vista de los zombis va solo por fuera:** sin aros alrededor de lo que hay dentro ni recuadros sueltos, como el de los bolardos. El sombreado rojo sigue marcando todo lo que ven.

### El escenario
- **La esquina de ladrillo, seguida:** sin la franja gris de hormigón en las esquinas de los supermercados y de los muros de ladrillo de la ciudad.
- **La puerta, encajada en su hueco,** a ras del ladrillo.
- **Nieve en los tejados y las azoteas,** como en las calles y los coches: en los tejados planos y los inclinados, las chimeneas y lo alto de los pretiles.
- **El 4×4 de La Lata ya no sale aparcado en las ciudades:** en sus dos sitios hay otros coches.

### La base
- **Al empezar una campaña nueva,** la base abre en «Escuadra», con la introducción encima.
- **Al salir del campamento** ya no sale el cartel «EN MARCHA» sobre La Lata.

### Lo que viene
- **Las ventanas, en columnas y en todas las plantas,** también en la baja (la nota 17 de Ana), van en la 0.12.9. Cambiarán los mapas de las misiones nuevas.

## Partidas guardadas

- **La campaña de la 0.12.7 sigue** en la 0.12.8, y también una misión guardada a medias.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.8-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.8-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.8:** con la 0.12.7 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.7.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
