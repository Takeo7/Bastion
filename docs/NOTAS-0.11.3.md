# Notas de la 0.11.3

Texto de Lore para la 0.11.3. **Borrador** desde el 1/11, con lo que ya está hecho: las notas de Ana del 1/11 ([PRUEBA-2026-11-01.md](PRUEBA-2026-11-01.md), las 7), la interfaz de combate nueva (UI-COMBATE v1.0, [UNITY.md](UNITY.md) §1 y UNITY-UI.md §7), el carácter de cada ciudad, las casas, los coches chocados, el fuego, el icono con la versión, el clima, los animales y jugar por internet sin Tailscale. Falta, cuando se cierre: lo de rendimiento. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.11.3: interfaz de combate nueva, mal tiempo, fuego y juego por internet

Versión 0.11.3 (commit [commit]). Las notas de Ana del 1/11, con la 0.11.1, y la interfaz de combate de Arturo.

## Novedades de la 0.11.3

### La interfaz del combate
- **La escuadra va en un solo panel, abajo a la izquierda.** Arriba, la ficha del soldado elegido: arma, cargadores, pistola, puntería, cuerpo a cuerpo, granadas, blindaje y la mochila. Debajo, un retrato por soldado, con su salud en verde y sus acciones en amarillo. El elegido sale más grande.
- **Solo salen tus soldados,** y los del compañero mientras te cede el mando.
- **Los estados de cada soldado** (VIGILANCIA, MORDIDO…) salen sobre su retrato. Con el ratón encima, todos, con lo que significa cada uno.
- **Un clic en un retrato elige al soldado** sin mover la cámara. Con doble clic, la cámara va a él.
- **«Terminar turno» es la última tecla de la barra,** con →| dibujado y sin texto. Debajo de toda la barra va tu reloj; en cooperativo, el del compañero, más fino, justo debajo.
- **El objetivo es una flecha en un aro,** abajo a la derecha. Señala dónde está el objetivo de la misión y, cuando ya está hecho, la evacuación.
- **Arriba ya no salen «TU TURNO», «OCULTOS» ni el selector de plantas.** Las plantas se cambian con RePág y AvPág, o con Mayús y la rueda.
- **Ya no están los botones «Menú» ni el del mapa:** el menú se abre con Esc y el mapa con la M.
- **En el menú, con su tecla al lado:** «Ir a» un soldado que aún tiene acciones (F) y, en cooperativo, «Ceder el mando» (C) o «Ver a» tu compañero (F). También está ahí «Cámara al soldado», que se enciende y se apaga.

### El fuego
- **En algunos mapas hay fuego desde el principio:** coches, cubos de basura y bidones, y escombros delante de alguna casa. Sale en 2 de cada 5 mapas de ciudad, pueblo y polígono, y en 1 de cada 4 de fábrica, área de servicio y campo.
- **Quema a cualquiera,** soldado, zombi o vivo: pisar una casilla que arde quita 1 de salud, y acabar el turno en ella, 1 más. El blindaje no protege.
- **El camino de un soldado rodea el fuego** si hay otro que gaste las mismas acciones. Si no, pasa por él, y la nota del destino dice cuánto quita: «Fuego en el camino: −1 de salud».
- **En la zona de movimiento,** las casillas que arden llevan una llama naranja.
- **Los zombis lo rodean** cuando pueden.
- **Lo que arde echa llamas grandes, brasas y humo negro;** en las casillas de alrededor, llamas pequeñas y hollín.
- **De noche, el fuego alumbra** como una farola, en naranja y parpadeando.

### La lluvia y la nieve
- **Una de cada tres misiones trae mal tiempo.** En invierno, nevada o ventisca; cuando acaba el invierno, llovizna, aguacero o tormenta.
- **Sale en la ficha de la misión,** en la fila «Tiempo», y es el que habrá al lanzarla. En combate, en la tarjeta del objetivo; con el ratón encima, dice qué cambia.
- **El asedio, las misiones de la historia y los mapas hechos a mano** van siempre despejados.
- **Con tormenta o ventisca, todos ven un 30 % menos lejos:** los soldados, los zombis y los vivos. De noche no se suma: cuenta lo que deje ver menos, la noche o el tiempo.
- **Con aguacero, el ruido llega un 20 % menos lejos,** y con tormenta, un 40 % menos.
- **El medidor de ruido también sube más despacio con el ruido:** un 20 % menos con aguacero y un 40 % menos con tormenta. Lo que sube con los turnos no cambia.
- **La llovizna y la nevada no cambian nada:** solo se ven.
- **Cómo se ve cada uno:**
  - la llovizna, fina y con neblina;
  - el aguacero, con charcos que reflejan el cielo;
  - la tormenta, con rachas de viento y relámpagos;
  - la nevada cubre el suelo, los tejados, los coches y los árboles;
  - la ventisca va de lado, con una bruma casi blanca.
- **Bajo techo no llueve ni nieva.** De noche, con mal tiempo, todo se ve más oscuro.

### Los coches chocados
- **En las calles hay coches chocados:** varios juntos en un cruce, alguno cruzado en medio de la calzada y, a veces, uno empotrado en una casa o una tienda. En el polígono, menos; en el campo y en el área de servicio, a veces uno abandonado en la carretera; en la fábrica, ninguno.
- **Dan cobertura baja,** como los coches aparcados, y nunca cierran el paso a ningún sitio.
- **Están destrozados y oxidados,** cada uno torcido a su manera, con restos alrededor: una rueda suelta, piedras.
- **El que se empotra en una casa rompe la fachada:** el morro queda dentro y levantado, con grietas y cascotes dentro y fuera.

### Los animales
- **En pueblos y ciudades hay perros** por las calles y los patios, cuervos en los tejados y palomas en algún sitio abierto. En el campo, un perro, un zorro o un lobo, y cuervos.
- **Huyen de quien se acerca,** soldado o zombi, y los pájaros echan a volar.
- **Solo se ven donde mira la escuadra,** y no cuentan en las reglas.

### Las ciudades y las casas
- **Cada ciudad y cada pueblo tiene su carácter, al azar:** residencial, con más casas y pisos; comercial, con más tiendas y oficinas; o mixto, como hasta ahora. No sale en la ficha de la misión: se descubre al jugar.
- **Las casas tienen tejado inclinado,** a dos o a cuatro aguas, con chimenea. Su tejado no se pisa, como antes.
- **Muchas llevan porche** sobre la puerta, y algunas, contraventanas rojas y puerta de garaje. El garaje no se abre: es pared.
- **Las ventanas y las puertas** cambian de una casa a otra.

### En combate
- **«Flanco» ya no sale sobre nadie,** tampoco contra los vivos. El dato sigue en el panel del disparo: «Flanqueo» contra los vivos y «Al descubierto» contra los muertos.
- **El aro bajo el puntero** sale solo con un soldado elegido que tiene acciones, y sobre una casilla a la que puede llegar.
- **Con Esc, la partida se para también en mitad de una escena:** la cámara al hombro, la llegada, La Lata que se va y el turno enemigo. Al cerrar el menú, sigue desde el mismo punto.
- **Si varios zombis atacan seguidos al mismo soldado,** la cámara entra al hombro una sola vez y vuelve al acabar el último ataque.
- **Cuando un zombi se pone a comer a un soldado caído,** hasta tres zombis más que estén al lado se arrodillan a comer con él, y los que están a dos casillas alargan los brazos desde su sitio. Solo se ve: no gastan acciones ni se mueven.
- **Los zombis comen pegados al cuerpo,** no a un metro de él.

### Jugar por internet
- **Ya no hace falta Tailscale:** al alojar, la app pide al router que abra el puerto. Si puede, la sala enseña la dirección de internet con una clave de 5 letras, por ejemplo «83.45.12.7:41391 · KXRT5».
- **Quien se une pega la línea entera** en «Unirse por dirección». «Copiar» copia la dirección y la clave juntas.
- **La clave es nueva cada vez que se aloja.** Quien ya ha entrado y se cae vuelve sin ella.
- **Diez claves equivocadas en 10 minutos** dejan fuera esa conexión durante media hora.
- **Si el router no deja abrir el puerto,** la sala lo dice. Entonces, Tailscale, como hasta ahora.
- **En la misma red y con Tailscale, como siempre,** sin clave. La dirección de Tailscale se llama ahora «Con Tailscale».

### La app
- **El icono vuelve a ser el autobús sobre la sierra,** en el Mac y en Windows. Las versiones de Unity salían con el icono de Unity.
- **Lleva el número de la versión** en una insignia amarilla, arriba a la derecha.

## Partidas guardadas

- **La campaña de la 0.11.2 sigue** en la 0.11.3.
- **Las misiones que ya estaban en el Mapa** salen con todo esto, porque el mapa se hace al lanzarlas.
- **Una misión guardada a medias** sigue con su mapa: sin fuego ni coches chocados, y despejada.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.11.3-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.11.3-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.11.3:** con la 0.11.2 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.11.2.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
