# Notas de la 0.12.16

Texto de Lore para la 0.12.16: la primera tanda de la revisión del proyecto que pidió Arturo el 10/11, con lo que podía parar una partida. Fuentes:
- UI: el combate que se quedaba parado y la carga a media animación (`.dev-snapshots/ui/unity/0.12.16/LEEME.md`, filas 1 y 2);
- Arte: las esperas que se colgaban, en el golpe cuerpo a cuerpo, los efectos y La Lata (`.dev-snapshots/arte/unity/0.12.16/LEEME.md`, fila 3);
- Red: el anfitrión que dejaba de aceptar conexiones y la lista de «Partidas en tu red» (`TcpHost.cs`, `LanBeacon.cs`);
- Gameplay: el rescate y «Convoy emboscado» (`.dev-snapshots/gameplay/0.12.16/motor.patch`, filas 12 y 13) y la firma del Mac ([FIRMA.md](FIRMA.md)).

Lo que no va en las notas:
- **El alambre de espino** (fila 14): solo sale en los asedios, que están apagados desde el 7/11, así que hoy no le pasa a nadie.
- **Una conexión con asiento que vuelve a presentarse:** solo pasa con un cliente hecho a mano. Su texto, «Ya tienes un puesto en esta partida.», está bien.
- **Lo que solo es de iOS o del laboratorio:** UPnP en iOS (`RouterPort`), `BASTION_FALLO_SUCESOS`, `CombatLab.GolpeFallido` y `UnitViews.Step` con una figura destruida a mano.
- **Lo que no se arregla en esta versión:** hacia dónde miran dos zombis tras `PlayGroup` sin `Choreograph`, que irá en la 0.12.17 si Gameplay quiere.

No hay arte nuevo ni textos nuevos del juego. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.16: el combate ya no se queda parado

Versión 0.12.16 (commit [commit]). La primera parte de la revisión que pidió Arturo el 10/11: lo que podía parar una partida. Arreglos en el combate, en la red y en dos reglas, y la app del Mac con otra firma, a prueba. No hay arte nuevo ni textos nuevos.

## Novedades de la 0.12.16

- **El combate ya no se queda parado.**
  - **Antes:** si fallaba algo al enseñar una acción en pantalla (un golpe cuerpo a cuerpo, un disparo, una granada, un humo, la salida de La Lata…), la pantalla se quedaba en el turno de los zombis. Nadie podía dar órdenes hasta salir de la partida y volver a entrar.
  - **Ahora:** el juego se salta lo que ha fallado, pone la pantalla al día y sigue con lo siguiente. Si falla dos veces seguidas, pide la partida entera al anfitrión, como al reconectar. Puede verse un salto: las figuras aparecen de golpe en su sitio. Pero el turno vuelve.
  - **Si veis un salto así, mandadnos red.log:** el fallo queda apuntado ahí.
- **Reconectar a mitad de una animación.**
  - **Antes:** al cargar la partida de nuevo, lo que estaba a medias se aplicaba otra vez encima. Lo que se veía podía no ser lo que había pasado.
  - **Ahora:** lo que estaba a medias se corta. Una animación ya empezada acaba sola, pero no cambia nada, y cada figura queda en su sitio.
- **El anfitrión sigue aceptando conexiones.**
  - **Antes:** si una conexión se cortaba justo al llegar, el anfitrión podía dejar de aceptar a nadie más, ni siquiera a quien volvía a conectarse, hasta alojar la partida otra vez.
  - **Ahora:** descarta esa conexión y sigue aceptando las demás.
- **«Partidas en tu red» ya no se queda parada.**
  - **Antes:** un mensaje mal formado de otro aparato de la red local podía parar la búsqueda hasta volver a abrir la app. La lista ya no encontraba partidas nuevas.
  - **Ahora:** ese mensaje se ignora y la búsqueda sigue.
- **Rescates: nadie da órdenes a quien hay que rescatar.**
  - **Antes:** hasta que un soldado llegaba hasta esa persona, no era de ningún jugador, así que los dos podían darle órdenes. En el primer turno, incluso moverla.
  - **Ahora:** nadie le da órdenes mientras espera («No puedes dar órdenes a esa unidad.»), y empieza sin movimiento. Cuando un soldado llega hasta ella, pasa a ser del jugador de ese soldado, como antes.
- **El acontecimiento «Convoy emboscado».**
  - **Antes:** con «Enviar a alguien a socorrerlo», el soldado que volvía herido seguía en la escuadra elegida. La misión salía con un soldado menos y, si era el único, sin nadie.
  - **Ahora:** sale de la escuadra al quedar de baja. Si en la escuadra no queda nadie que pueda ir, «Lanzar misión» dice «La escuadra está vacía.».
- **La app del Mac, con otra firma (a prueba, la eligió Arturo).**
  - **Antes:** firma *ad hoc*.
  - **Ahora:** firmada con el certificado de desarrollo de Arturo. No está notarizada.
  - **Lo que no cambia:** la primera vez que se abre cada versión descargada hay que darle a «Abrir igualmente».
  - **Lo que puede cambiar:** que el cortafuegos y la red local dejen de preguntar en cada versión. Con la 0.12.16 preguntarán como siempre, porque es la primera con esta firma. Se sabrá con la 0.12.17; si siguen preguntando, se vuelve a la firma de antes.

## Partidas guardadas

- **La campaña de la 0.12.15 sigue** en la 0.12.16.
- **Una misión guardada a medias también sigue.** En un rescate a medias, si aún no habéis llegado hasta la persona que hay que rescatar, ya no acepta órdenes.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.16-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.16-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.16:** con la 0.12.15 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.15.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firmada con el certificado de desarrollo de Arturo, sin notarizar. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con la 0.12.16, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos. Con la firma nueva, puede que desde la 0.12.17 ya no pregunte.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
