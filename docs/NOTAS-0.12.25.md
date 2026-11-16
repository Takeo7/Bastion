# Notas de la 0.12.25

Texto de Lore para la 0.12.25: lo de los dos informes de Arturo del 15/11, el de errores y el de propuestas. Fuentes:
- el plan y lo hecho: `.dev-snapshots/gameplay/notas-2026-11-15/PLAN.md`, con lo que eligió Arturo en el cuadro;
- UI: `.dev-snapshots/ui/unity/0.12.25/LEEME.md` y los textos revisados por Lore (`.dev-snapshots/lore/0.12.25/TEXTOS-UI.md`);
- Arte: `.dev-snapshots/arte/unity/arreglos-0.12.25/LEEME.md`;
- Red: `.dev-snapshots/red/0.12.25/red-01225.patch`.

Lo que no va en las notas:
- **Lo que aún no está:** el ruido de los grupos (se prueba y se le enseña a Arturo antes) y las tres propuestas grandes, que primero van en papel.
- **Lo que Arturo no marcó:** cambiar de arma con una acción, las habilidades en otro sitio.
- **Los laboratorios.**

Las reglas cambian en el mordisco al pasar, los maleteros de policía y el tiempo del turno. Va para Mac y Windows, sin nada de iOS. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.25: el mordisco al pasar y el ruido antes de actuar

Versión 0.12.25 (commit [commit]). Lo que pidió Arturo en sus notas del 15/11: tres cambios de reglas, varios arreglos y cuatro mejoras de la interfaz.

## Novedades de la 0.12.25

### Reglas (lo eligió Arturo)

- **Mordisco al pasar corriendo.**
  - **Correr** es gastar todo el movimiento del turno de una vez. Cada zombi que queda al lado de una casilla por la que pasas, sin nada en medio, intenta morder una vez, como un fuego de reacción. Puede fallar.
  - **No cuenta** la casilla en la que te paras, y andando no pasa nunca.
  - **Sobre el zombi** sale «MORDISCO AL PASAR». Se ve desde la cámara de siempre, sin plano al hombro: cada mordisco dura 1 s y la carrera sigue. Si el mordisco mata, la muerte se ve como cualquier otra.
- **Los maleteros de los coches de policía** tienen un arma una vez de cada cinco. Antes, una de cada tres.
- **El tiempo del turno:** 30 s por soldado. Antes, 45.

### Arreglos (del informe de errores de Arturo)

- **Órdenes entre tu turno y el de los zombis.**
  - **Antes:** mientras la pantalla aún enseñaba el turno de los zombis, la partida ya iba por tu turno siguiente, y aceptaba órdenes. Un soldado puesto en vigilancia en ese rato seguía así el turno siguiente.
  - **Ahora:** no se pueden dar órdenes hasta que la pantalla llega a tu turno.
- **La piedra que no llega.** Si apuntas más allá del alcance, la piedra o la granada caen donde acaba la línea, más cerca, y la nota de esa casilla lo dice: «No llega más lejos». Antes caía ahí sin avisar, y parecía que el ruido salía en otro sitio.
- **El zombi que oye un ruido** gira la cabeza y luego el cuerpo hacia él, en menos de un segundo. Antes seguía mirando a otro lado hasta echar a andar. Solo se ve en los zombis que tiene a la vista la escuadra.
- **Andar en diagonal:** soldados y zombis ya no van en zigzag. Cada tramo se anda en línea recta, sin atravesar muros, coches ni a nadie, y tarda lo mismo.
- **El tablero de habilidades:** algunas casillas salían en blanco tras volver de una misión. No faltaba ninguna habilidad: eran sus iconos.
- **El contorno azul del área de andar** ya no se dibuja por encima de los contenedores y los coches: lo tapan, como dentro de las casas (lo eligió Arturo).
- **Los retratos de Ana Ríos y Elena Petrova** que no eran su soldado: era el fallo de la 0.12.23 que ya arregló la 0.12.24. La prueba se hizo con la 0.12.23.

### Interfaz (lo eligió Arturo)

- **Terminar el turno de un soldado con algo por gastar.**
  - **Antes:** «Terminar turno» lo terminaba a la primera.
  - **Ahora:** la primera vez dice lo que le queda, por ejemplo «A Zainab le quedan 2 acciones y 5 casillas de movimiento. Pulsa otra vez para terminar su turno.», y parpadean sus acciones, su movimiento y su tarjeta. Pulsando otra vez en 4 s, termina.
  - Al terminar el turno de todos con soldados aún en juego, además de la pregunta de siempre, parpadean sus tarjetas.
- **El ruido, antes de actuar,** en dos datos separados:
  - **hasta dónde se oye,** en casillas, lo mismo que oyen los zombis (con lo que suma la escuadra y la lluvia, que antes no se contaban). El fusil de asalto, por ejemplo, pasa de 12 a 13;
  - **cuánto sube el indicador «RUIDO»,** en dB, en las misiones que lo tienen.
  - **Dónde sale:** en las fichas de las teclas («SE OYE: hasta 18 casillas», «RUIDO: +3 dB»), al apuntar y al lanzar («Se oye hasta 18 casillas · Ruido +3 dB»).
- **Cada acción, siempre en la misma tecla:** 1 Disparar (y Espacio), 2 Tajo, 3 Pistola, 4 Vigilancia, 5 Lanzar, 6 Recargar, 7 Cubrirse, 8 Esconderse, 9 Botiquín, 0 Investigar. Si un soldado no tiene una, su tecla queda libre. Las habilidades del tablero, las puertas y la palanca van detrás, sin número.
- **Cuando un zombi ve a un soldado:**
  - el zombi se gira hacia él, con «OS HA VISTO» encima;
  - sobre el soldado sale «¡A LA VISTA!», con una línea roja del uno al otro;
  - la cámara encuadra a los dos y parpadean las tarjetas de toda la escuadra.
  - El cartel y el registro acaban en «Toda la escuadra queda al descubierto.».

## Partidas guardadas

- **La campaña de la 0.12.24 sigue** en la 0.12.25, y también una misión guardada a medias. En ella, los maleteros que queden por abrir y el mordisco al pasar ya van con lo nuevo.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.25-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.25-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.12.25:** con la 0.12.24 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.24.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
