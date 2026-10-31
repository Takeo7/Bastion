# Notas de la 0.11.2

Texto de Lore para la 0.11.2, con las notas de Arturo del 31/10 sobre la 0.11.1 ([PRUEBA-2026-10-31-ARTURO.md](PRUEBA-2026-10-31-ARTURO.md), las 14, y el asedio que no acababa). Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.11.2: zombis según el lugar y enemigos que no ven por las ventanas

Versión 0.11.2 (commit [commit]). Las notas de Arturo del 31/10, con la 0.11.1.

## Novedades de la 0.11.2

### Cada lugar con sus zombis
- **La ciudad tiene los mismos zombis que antes.** Los demás lugares tienen menos: el polígono y la fábrica, el 80 % de los de la ciudad; el pueblo, el 70 %; el área de servicio, el 60 %; el campo, el 50 %.
- **Ya no depende del tamaño del mapa:** antes, algunos mapas grandes llevaban más zombis que la ciudad.

### La ficha de la misión
- **Dice el lugar,** con su dibujo: Ciudad, Pueblo, Polígono, Fábrica, Área de servicio o Campo.
- **Las estrellas salen de cuántos zombis habrá:** el mismo día, una misión en el campo puede salir más fácil que una en la ciudad.
- **Al pulsar una misión,** su ficha se abre sin plegarse antes.

### Se acierta menos
- **Los soldados tienen 5 menos de puntería,** también los de una campaña empezada: un recluta, 55 (antes, 60). Los veteranos pierden algo más: con un 25 % más de Tiro, de 75 a 69.
- **El zombi común es más difícil de acertar:** ya no tiene 10 menos de defensa.
- **Contra un zombi a 6 casillas,** un recluta con el fusil acierta el 66 % (antes, el 81 %).

### Las ventanas
- **Los enemigos no ven por las ventanas,** ni de dentro afuera ni de fuera adentro. Vale para los zombis y para los vivos.
- **Por una puerta abierta o por una pared rota, sí.**
- **Oyen como siempre.**
- **Los soldados ven por las ventanas como antes.**
- **Lo que ve un zombi al pasar el ratón por él,** y el aviso del camino de que un enemigo te verá, ya no pasan por las ventanas.

### El asedio
- **De día, el pinar sale en negro** hasta que lo ve un soldado. La base y el claro se ven siempre.
- **Solo se ven los zombis que ve la escuadra:** antes se veían todos.
- **De noche, como antes.**
- **Un asedio podía no acabar nunca:** un zombi se quedaba en un rincón del pinar sin llegar a nadie. Ahora, sin nadie a la vista, cada zombi va a por el soldado más cercano al que puede llegar, andando o rompiendo una puerta o una barricada.

### La Lata
- **Los soldados ya no suben con animación:** se desvanecen junto a la puerta.
- **Al acabar la misión, La Lata arranca y se va,** y la cámara gira un poco para verla irse.
- **En la base, igual:** al salir de misión, corren a la puerta, se desvanecen y La Lata se va. Es más corto: con el 4×4, de unos 10 s a 6.

### El mapa del combate
- **Con la tecla M o con el botón de la chincheta,** junto a «Menú», la cámara sube y mira desde arriba, con el norte arriba, sobre lo explorado.
- **Se ve el combate mismo:** la niebla, las banderas de los soldados y de los zombis, la zona de evacuación y La Lata.
- **Al pulsar en el suelo, la cámara baja allí,** con el ángulo y la distancia que tenía. Con M o con Esc, vuelve a donde estaba.
- **Mientras mira desde arriba, el clic no da órdenes.**
- **La M ya no silencia el sonido:** el silencio está solo en Ajustes › Sonido.

### En combate
- **Al pasar el ratón por el nombre de un soldado en la lista,** su aro se ilumina y una flecha lo señala. Si está fuera de la pantalla, la flecha sale en el borde.
- **Contra los muertos ya no sale «Flanco».** El crítico de más sigue igual, y en el panel del disparo se llama «Al descubierto». Contra los vivos, como antes.
- **Las casillas del ojo,** donde un enemigo te verá, ya no llevan el recuadro rojo, y el ojo es de un rojo más suave.
- **Con una sola acción ya no sale «correr»,** ni en la zona ni en el camino.
- **La línea del camino solo la tapan los soldados y los zombis:** los coches, las cajas y los muros ya no la cortan.
- **Al disparar, el soldado mira al blanco:** antes miraba de lado. Tampoco da un salto al disparar.

## Partidas guardadas

- **La campaña de la 0.11.1 sigue** en la 0.11.2.
- **Las misiones que ya estaban en el Mapa** salen con su lugar, sus estrellas y sus zombis nuevos.
- **En una misión guardada a medias,** los zombis son los que ya había. La puntería y las ventanas cambian desde que se retoma.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.11.2-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.11.2-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.11.2:** con la 0.11.1 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.11.1.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
