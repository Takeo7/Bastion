# Notas de la 0.14.5

Texto de Lore para la 0.14.5. Lo pidió Producto el 24/11 por la noche (`lore/MENSAJE-DE-PRODUCTO-0.14.5.md`). Corto. Sin commit.

**Qué lleva:**
- el menú principal nuevo y sus páginas (cuadros 149 a 151);
- los arreglos de la pasada de la 0.14.4;
- el muerto que embiste sin parpadeo;
- las texturas a 2048 como máximo.

**Fuentes:**
- `producto/RUNS.md`, cuadros 149 a 151;
- `producto/menu-2026-11-24/ENCARGO.md` y `producto/ENTREGA-menu-151-UI.md`;
- las fotos de `ui/unity/runs/menu-151/fotos/`;
- `producto/ENTREGA-tras-0144-UI.md`;
- `producto/MENSAJE-DE-ARTE-tras-0.14.4.md` y `MENSAJE-DE-ARTE-menu-y-texturas.md`.

**Lo que no va:** las herramientas (`publicar.sh`, `AppBuild`), los bots, y lo que solo cambia en el iPad y el iPhone.

**Sin probar, para Producto:**
- el menú en la app, con teclado y dedo de verdad;
- el menú en «Bajo»;
- lo que cuestan las sombras largas del inicio;
- un guardado real de la 0.14.4 cargado en la 0.14.5. El motor solo añade `Runs.RewardRoom`, que no cambia el formato.

**Para completar al publicar:** [commit]. El tamaño de la descarga, en el texto de la release.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.5: el menú principal nuevo

Versión 0.14.5 (commit [commit]). El menú principal nuevo que eligió Arturo, arreglos de la pasada de la 0.14.4 y una descarga más pequeña.

## Lo que se arregla

- **Con «Interfaz» al 130 %,** la barra de acciones ya no queda debajo del ojo ni de la flecha del objetivo.
- **Cuando la recompensa no cabe en las mochilas:**
  - la tarjeta del final ya no dice «y la recompensa»;
  - el parte enseña solo lo que de verdad entró.
- **El botín del parte,** si es largo, va en dos filas que se desplazan. Ya no sale la franja gris de debajo.
- **En el Trueque, a 1920 × 1080,** la placa ya no tapa las etapas siguientes cuando le falta poco sitio: lo de dentro se desplaza, y «Seguir» queda siempre a la vista.
- **El muerto que embiste** ya no parpadea hacia atrás al dar el cabezazo.

## Partidas guardadas

- **La partida de la 0.14.4 sigue en la 0.14.5.**

## Novedades de la 0.14.5

### El menú principal nuevo

- **«BASTIÓN»,** centrado arriba, en letra de plantilla gastada.
- **Las cinco entradas de siempre,** a la izquierda y sin placas. La elegida lleva detrás un brochazo ámbar.
- **La escena,** más azul y con sombras largas, bajo un velo oscuro en diagonal.
- **Se elige** con el ratón, con el dedo o con las flechas. Intro pulsa la elegida.
- **Las páginas del inicio y la sala,** con el mismo aspecto:
  - «Nueva partida», «Unirse por dirección» y la sala multijugador, con sus botones como entradas;
  - «Partida multijugador» es ahora una columna, como el menú: «Alojar partida», «Unirse a la partida de Ana» (o «Unirse por dirección») y «Volver».
- **Siguen como antes:** Ajustes, las tarjetas de dificultad, los campos de texto y la pregunta «¿Empezar de cero?».
- **Al abrir el juego y al volver al inicio,** el título nuevo sale ya en su sitio.

### Una descarga más pequeña

- **Las texturas más grandes bajan a 2048 de lado.** La descarga es más pequeña y el juego usa menos memoria de vídeo.
- **A la vista, igual.**

## Lo que sigue sin arreglar

- **Un soldado o un cuerpo** puede quedar tapado por un edificio sin descubrir.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **Quedan saltos de animación:**
  - un soldado se levanta de golpe;
  - algunos muertos suben el brazo o bajan el cuerpo de golpe.

  Si veis alguno, apuntad dónde y cuándo.
- **La dificultad** se ajusta jugando: apuntad lo que notéis en cada nivel.
- **La sincronía vertical con la pantalla dormida,** o apagada al momento con una esquina activa, está sin probar.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.14.5-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.5-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.5:** con la 0.14.4 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.14.4.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
