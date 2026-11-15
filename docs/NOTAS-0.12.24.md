# Notas de la 0.12.24

Texto de Lore para la 0.12.24: los menús y la vista de Soldados de las notas de Arturo del 14/11, y sus notas sueltas del 15/11 sobre el combate. Fuentes:
- el plan y lo elegido: `.dev-snapshots/gameplay/notas-2026-11-14/PLAN.md`;
- UI: `.dev-snapshots/ui/unity/0.12.24/LEEME.md` y `DUDAS.md`;
- Arte: `.dev-snapshots/arte/unity/arreglos-0.12.24/LEEME.md`;
- Red: `.dev-snapshots/red/menu-0124/LEEME.md`.

Los textos nuevos los revisó Lore (`.dev-snapshots/lore/0.12.24/TEXTOS-UI.md` y `TEXTOS-RED.md`) y ya van en los parches de UI y de Red. Lo que no va en las notas: los laboratorios, el coste de los fondos y lo de iOS.

Las reglas no cambian. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.24: menús nuevos y la vista de Soldados

Versión 0.12.24 (commit [commit]). El menú principal, «Partida multijugador» y la vista de Soldados, nuevos, como los dibujó Arturo, y tres arreglos del combate. Las reglas no cambian.

## Novedades de la 0.12.24

- **El menú principal, nuevo.** Al fondo, La Lata en una carretera nevada, con los faros encendidos. En columna:
  - **«Continuar partida»** (solo si hay una guardada): va directa a la base o a la misión que dejaste a medias.
  - **«Nueva partida»:** pide tu nombre y empieza una campaña nueva. Si ya hay una guardada, antes pregunta si quieres sustituirla. La partida empieza contigo; el otro jugador puede entrar después y toma el mando de 4 soldados, como hasta ahora.
  - **«Partida multijugador».**
  - **«Ajustes»** y **«Salir de Bastión».**
  - Con «Continuar partida» y «Nueva partida» la partida también se abre en la red, como al alojar. Si «Abrir el puerto del router» está en «Sí» en Ajustes, también por internet.
- **«Partida multijugador», nueva.** Al fondo, un cruce nevado. Dos tarjetas:
  - **«Alojar partida»:** abre la partida y te lleva a la pantalla de siempre, con «Continuar campaña», «Nueva campaña» y «Escaramuza», y con la dirección y la clave para quien juegue contigo.
  - **«Unirte a la partida de …»,** si hay una partida de esta misma versión en tu red. Si no hay ninguna, **«Unirse por dirección»**, que pide la dirección y la clave por separado. La clave, solo si la partida la pide. Pegar en la dirección lo que da «Copiar» («dirección · clave») sigue valiendo.
  - Ya no hay lista de partidas ni textos de ayuda.
- **La dirección y la clave, también en el «Menú».** Mientras alojas y el otro jugador no ha entrado, el «Menú» (Esc) de la base y de la misión las enseña, con «Copiar». Antes, con «Continuar partida» o «Nueva partida» no salían en ningún sitio.
- **En toda la interfaz:**
  - los botones tienen los extremos rectos, también «Seguir» en «¿Salir de Bastión?»;
  - al pulsar «Alojar partida» ya no sale el naranja;
  - fuera la raya de color del registro («Llegan más…») y de la ficha del enemigo;
  - el «Menú» y las ventanas de la base ya no dejan ver lo de detrás;
  - «Controles» solo sale en combate. En la base y en el inicio salía vacía.
- **La vista de Soldados, nueva.**
  - **Los retratos,** de frente y llenando su casilla, con el nombre debajo. Detrás, luz morada para los tuyos y turquesa para los del otro jugador (lo eligió Arturo). El elegido se ve por la banda del nombre en blanco.
  - **«Reclutar»** es una casilla más, con un «+» y el precio. Lo que contaba su tarjeta sale al pasar el ratón.
  - **El equipo, alrededor de la figura:** a la izquierda, Mano, Costado y Cinto; a la derecha, la ropa (con su salud y su blindaje) y los dos huecos del cinturón. Al pulsar una casilla, sus opciones se abren al lado.
  - **Personalizar se abre pulsando la figura** (lo eligió Arturo). Al pasar el ratón, la figura se resalta y sale «PERSONALIZAR».
- **La ficha del soldado, nueva:**
  - debajo del nombre, las tres líneas del combate: salud, acciones por turno y movimiento;
  - la puntería, la voluntad y la esquiva, al pasar el ratón por el nombre;
  - la experiencia, en barras continuas, con el bono en verde;
  - el rango, con sus cinco partes;
  - el botón «Tablero de habilidades», con los puntos por gastar al lado.
- **En combate:**
  - **El soldado elegido** lleva a los pies un aro ancho del color de su jugador (lo eligió Arturo). Antes era un aro blanco que en la nieve casi no se veía.
  - **«Cámara al soldado»:**
    - **Antes:** si habías tocado la cámara en los 4 s anteriores, no te llevaba al soldado al que le tocaba.
    - **Ahora:** te lleva, salvo si la estás moviendo en ese momento.
  - **Turnos alternos con el otro jugador:** al acabar tu soldado, la cámara va al suyo, que es al que le toca. Antes iba a otro de los tuyos.
  - **Los retratos de la lista de la escuadra** enseñaban a otra persona que la figura del mapa. Ahora es la misma.

## Partidas guardadas

- **La campaña de la 0.12.23 sigue** en la 0.12.24, con «Continuar partida». Una misión guardada a medias también.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.24-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.24-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.24:** con la 0.12.23 no se puede jugar juntos, y en «Partida multijugador» no salen las partidas de otra versión.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.23.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
