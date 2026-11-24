# Notas de la 0.14.3

Texto de Lore para la 0.14.3. Lo pidió Producto el 24/11, con el árbol congelado. Sin commit.

**Qué lleva:**
- fallos de la 0.14.2 y de antes: la barra de acciones jugando solo, el tablero de habilidades casi vacío (M5, cuadro 144), el turno de los muertos que no se veía (z67), el salto al andar en diagonal (M11), el tirón de la granada;
- el mapa de etapas y las esperas en oscuro, y el papel fuera (cuadro 143);
- las notas de Arturo del 23/11: M3, M4, M6, M7 (cuadro 140), M8 (cuadro 142), M9, M10 (cuadro 141), M12 y las marcas de M13 en el icono.

**Fuentes:**
- `.dev-snapshots/lore/ARBOL-CONGELADO.md` y `producto/notas-2026-11-23/PLAN.md`;
- `producto/RUNS.md`, cuadros 140 a 144;
- `producto/ENTREGA-0.14.3-UI-1.md`;
- `gameplay/notas-23-11/0.14.3-M7-M8.md` y `0.14.3-M10.md`;
- `producto/MENSAJE-DE-ARTE-m11-saltos.md`, `MENSAJE-DE-ARTE-r22-granada.md` y `MENSAJE-DE-ARTE-m13-accesorios.md`;
- `lore/MENSAJE-DE-ARTE-saltos.md` (Arte, 24/11): de los saltos, solo da por arreglado el de andar en diagonal. El del golpe cuerpo a cuerpo está «arreglado sin comprobar», así que no va en las notas;
- `lore/ESQUIVA-DEFENSA.md`.

**Lo que no va:**
- lo de dentro: pruebas, bots, laboratorios, la venta en la sala y el bot que vende (Red);
- lo que se quita antes de publicar: la escena de arranque y la prioridad de la precarga;
- lo que no entra: los accesorios en el arma en 3D, los demás saltos de M11 y la copa del árbol;
- lo que solo cambia en el iPad y el iPhone.

**De la revisión independiente (24/11), ya en el árbol:** en «El soldado que se une», el que no cabe deja lo que cabe en las mochilas, y el que se queda fuera de La Lata sale como «se quedó fuera». Su línea del parte está en `ui/MENSAJE-DE-LORE-no-se-queda.md`. Lo puesto pregunta antes de cambiarse (Trueque). El tablero: según UI, el fallo solo tocaba el tablero (Soldados y la hoja H) y la ficha de una casilla; el parte, el Almacén y los iconos del combate usan otra función.

**Sin probar, para Producto:** «La partida de la 0.14.2 sigue en la 0.14.3» solo lo cubren las pruebas del motor (`Setup.UpToDate`: `SurvivorPack`, `NumberGear`), no un guardado real cargado en la app (cierre de Gameplay). Si la R23 de Red no lo prueba, mejor quitar esa línea o decirlo.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.3: ofrecer en el Trueque y el mapa en oscuro

Versión 0.14.3 (commit [commit]). Arregla fallos de la 0.14.2 y de antes, devuelve el mapa de etapas y las esperas al fondo oscuro, y trae las notas de Arturo del 23/11.

## Lo que se arregla

- **Jugando solo, la barra de acciones** salía descentrada y sin el botón de terminar el turno («→|»), que quedaba debajo. A dos jugadores pasaba ya en la 0.14.1. Ahora va centrada y con «→|».
- **El tablero de habilidades salía casi vacío** con varias casillas compradas: sin sus colores, sin el icono del centro y sin los totales.
  - Venía de antes: 13 habilidades no tenían icono, y al fallar una se dejaba de pintar el resto del tablero.
  - Ahora salen todas, en el tablero de Soldados, en el de la hoja del soldado en combate (H) y en la ficha de cada casilla.
  - Las casillas que aún no se pueden coger siguen oscuras y sin icono, como antes.
- **El turno de los muertos se ve entero,** también cuando uno levanta a otro y este se mueve en ese mismo turno. Antes, a veces no se veía.
- **Al andar en diagonal,** el soldado ya no salta un instante a cada casilla que pisa.
- **Al estallar una granada que abre una casa,** la imagen se para mucho menos: las paredes que caen se colocan todas de una vez.

## Partidas guardadas

- **La partida de la 0.14.2 sigue en la 0.14.3.**
- **Un combate guardado a medias con un superviviente esperando:** al cargarlo, el superviviente recibe su mochila.

## Novedades de la 0.14.3

### El mapa de etapas y las esperas, otra vez en oscuro

- **El papel se quita:** el mapa dibujado y las hojas de papel de la 0.14.2 desaparecen.
- **El mapa de etapas** tiene fondo oscuro y los iconos del juego:
  - en blanco, el recorrido hecho;
  - en rojo, a dónde se puede ir;
  - más fino, lo que queda más adelante.
- **Se queda lo nuevo del mapa:** la ficha de la etapa por partes, las notas junto a cada etapa, la leyenda, y un clic para elegir y «Ir a esta etapa».
- **Al ir a una etapa,** una pantalla negra con el sitio, el peligro, las notas y quién va.
- **Las demás esperas,** en negro, con las mismas palabras que en la 0.14.2.
- **También se queda:** se ve a La Lata salir de la base, y se entra al combate sin «Entendido».

### Ofrecer en el Trueque

- **El Trueque tiene dos lados:**
  - «Lo que te ofrece»: lo del mercader, como siempre;
  - «Lo que le ofreces»: lo que lleva puesto y en la mochila el soldado elegido a la derecha. Para ofrecer lo de otro soldado, se elige su busto.
- **Cada cosa, con lo que paga el mercader** en verde («+45») y su «Cambiar».
  - Paga la mitad de su precio, redondeada a 5 hacia abajo, y 15 más por cada accesorio montado en un arma. Con «Buen trato», un 20 % más.
  - Lo que lleva puesto pregunta antes de cambiarse, con «Cambiar» y «Cancelar»: «¿Cambiar el fusil de cerrojo que lleva puesto por 55 §?».
  - Los § van a su mochila; si no caben, a la de otro de tus soldados. Si no caben en ninguna, no se puede.
- **No se pueden cambiar** los §, el cuchillo ni lo de una misión, como la llave o el antiviral.
- **El mercader no tiene tope.** Lo cambiado desaparece, y con lo ganado se puede comprar en el mismo Trueque.
- **Sin su arma,** el soldado pelea con el cuchillo. Un chaleco se va con su salud; la herida se queda.
- **A dos,** cada uno ofrece lo de sus soldados.
- **La mejora «Buen trato»** lo dice: «En el Trueque, todo cuesta un 20 % menos, y lo que ofrecéis se paga un 20 % más.»

### El soldado que se une

- **Tiene mochila desde que se une:** la de un recluta, vacía, y el cuchillo. Antes no tenía.
- **Sale en el parte y en el alto de la carretera,** con lo que hizo desde que se unió: sus bajas y su experiencia. Lo que lleva vuelve con él.
- **Si cae después de unirse,** sale en el parte como caído.
- **Si en el Bastión ya no caben más soldados,** no se queda, y el registro lo dice: «Pablo Rey no se queda: en el Bastión ya no caben más soldados.».
  - Antes de irse, deja a la escuadra lo que cabe en las mochilas, menos su cuchillo. Lo demás se queda allí. El parte lo dice: «Pablo Rey no se queda: en el Bastión ya no caben más soldados. Deja a la escuadra lo que cabía en las mochilas; lo demás se queda allí: el fusil de asalto.».
- **Si se queda fuera de La Lata,** el parte dice que se quedó fuera, no que ha caído: «Elena Petrova se quedó fuera de La Lata. La perdéis con lo que llevaba: el cuchillo.».
- **En un Rescate con la escuadra llena,** como hasta ahora: ocupa plaza, y el mapa avisa con «Sin plaza libre».

### La ballesta acierta igual a cualquier distancia

- **Ya no pierde puntería de cerca.** De lejos, igual que antes. Su alcance, en la ficha, es «Parejo».
- **El Cazador lleva la misma ballesta:** de cerca acierta bastante más que antes. En las pruebas de Red, a una casilla o menos, pasa del 29 % al 58 %.
- **Solo el fusil de precisión** pierde puntería de cerca.

### El equipo

- **Mayús + clic** pasa una cosa entera al otro lado, sin arrastrarla:
  - en el Almacén, del almacén al soldado elegido (a su sitio si está libre; si no, a la mochila) y del soldado al almacén;
  - en «Equipo ▸», entre su sitio y la mochila;
  - en combate, con la mochila abierta, de la mochila al suelo y del suelo a la mochila;
  - si no hay sitio, no se mueve y sale el porqué;
  - una pila va entera. Arrastrar con Mayús sigue moviendo la mitad.
- **Pasar cosas entre tus soldados:** en el Almacén y en «Equipo ▸», lo que se arrastra se puede soltar sobre el busto de otro soldado. Va a su sitio o a su mochila, como con Mayús + clic.
  - Al arrastrar, los bustos de los que pueden llevarlo se marcan en verde. Encima de uno, su nombre en verde, o en rojo con el porqué.
  - Así se pasa equipo de un soldado a otro entre etapa y etapa.
- **Los accesorios:** al coger uno, las armas que lo admiten se marcan en verde. Soltado encima de una, se monta.
- **El icono de un arma** lleva una marca por cada accesorio montado.

### La ficha del soldado en «Partir»

- **Al pasar el puntero por la fila de un soldado, o por su tarjeta,** sale su ficha junto a la lista:
  - rango y de quién es;
  - salud, acciones, movimiento, voluntad, PUNT., CaC, esquiva y defensa;
  - armas, chaleco, cinturón y mochila;
  - experiencia, habilidades, puntos por gastar y heridas.
- **También la de los soldados del compañero,** solo para mirar.

### Qué hacen la esquiva y la defensa

- **Defensa:** quita puntos al acierto de quien ataca. Con defensa 10, un 60 % pasa a 50 %.
  - Se suma a la cobertura, que solo vale contra disparos (la baja quita 20 y la alta, 40).
  - Cubrirse da 30 más.
- **Esquiva:** de lo que le aciertan, ese tanto por ciento se queda en rozadura: la mitad de daño, como mínimo 1.
  - Un crítico nunca se queda en rozadura.
  - Cubrirse da 50 más.
- **Las dos valen** contra disparos, golpes y mordiscos. No valen contra explosiones, fuego ni caídas.
- **Dónde se ve:**
  - al pasar el puntero por la cifra, en los totales del tablero, la hoja del soldado en combate (H) y la ficha de un enemigo, sale qué hace con ese número;
  - la defensa sale siempre en los totales del tablero y en la hoja del combate; antes, en los totales solo si no era 0, y en la hoja no salía;
  - la ficha de un enemigo enseña también su esquiva;
  - el glosario tiene tres entradas nuevas: Defensa, Esquiva y Rozadura.

### Retoques

- **La salud de los totales del tablero** cuenta el chaleco de la expedición, como en «Partir». Al pasar el puntero: «Salud 7: 5 + 2 del chaleco antibalas».
- **A una soldado caída,** el sello de su tarjeta en el parte y el de su busto en combate dicen «CAÍDA».
- **Tras perder la partida, al empezar una nueva,** ya no se ve un instante la tarjeta de «PARTIDA PERDIDA».

## Lo que sigue sin arreglar

- **Un soldado o un cuerpo** puede quedar tapado por un edificio sin descubrir.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **La bienvenida de la base** tapa a los soldados.
- **Quedan saltos de animación,** sin causa conocida:
  - un soldado quieto tras un disparo aparece una casilla más allá;
  - un soldado cae al suelo de golpe al acabar de trepar, o se levanta de golpe;
  - algunos muertos suben el brazo o bajan el cuerpo de golpe.

  Si veis alguno, apuntad dónde y cuándo.
- **Los accesorios montados** se ven en el icono del arma, pero aún no en el arma que lleva el soldado.
- **La dificultad** se ajusta jugando: apuntad lo que notéis en cada nivel.
- **La sincronía vertical con la pantalla dormida,** o apagada al momento con una esquina activa, está sin probar.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.14.3-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.3-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.3:** con la 0.14.2 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.14.2.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
