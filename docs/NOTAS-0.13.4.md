# Notas de la 0.13.4

Texto de Lore para la 0.13.4. Lleva lo decidido por Arturo del 18/11 al 19/11 tras la 0.13.3 (commits locales b65247d, e4c8bd8, 321e957, d038723, 5888703 y 6c5d95a). Fuentes:
- lo decidido por Arturo: `.dev-snapshots/producto/RUNS.md`, cuadros 70 a 88;
- la revisión de Producto: `.dev-snapshots/producto/notas-2026-11-18/REVISION.md`, pasadas 50 a 58;
- los contratos de Gameplay: `.dev-snapshots/gameplay/g21-g22/`, `g23/LLAVE.md`;
- los mensajes de los seis commits.

Lo que no va en las notas:
- **Mordido:** está en el código, pero la etapa aún no sale en los mapas.
- **Lo de dentro:** las pruebas, los bots, los laboratorios y las sondas.
- **Lo no probado:** el rendimiento.

Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Va para Mac y Windows. Lo que va entre [corchetes] lo completa Producto al publicar: el commit. El aviso de Windows sin probar se quita si alguien la prueba en un PC. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.13.4: La llave y los Refugios

Versión 0.13.4 (commit [commit]). A la base se vuelve solo desde un Refugio o al superar el capítulo, y hay más Refugios. Sale la primera misión nueva, La llave, en la comisaría. Los coches ya saqueados se ven oxidados, cada etapa tiene su cielo y entre etapas la escuadra para en una carretera.

## Partidas guardadas

- **La partida de la 0.13.3 sigue en la 0.13.4.** No se empieza de cero.
- **Una expedición ya empezada** sigue con su mapa: se puede volver a la base desde el mapa, como antes, y no tiene La llave. Lo nuevo empieza en la expedición siguiente.
- **Si se guardó a mitad de un combate,** ese combate acaba con el cielo y los coches de antes.

## Novedades de la 0.13.4

### Volver a la base, desde un Refugio

- **Antes:** se podía volver a la base desde el mapa de etapas tras cualquier etapa.
- **Ahora:** se vuelve solo desde un Refugio, o al superar el capítulo, como antes.
  - **En el mapa de etapas** ya no está el botón. En su sitio: «Se vuelve a la base desde un Refugio o al superar el capítulo.»
  - **En el Refugio,** la escuadra se cura del todo, como antes, y elige entre «Seguir la expedición» y «Volver a la base», que pregunta antes. Con dos jugadores, volver se vota.
- **Más Refugios:** unos 3 por mapa. Casi siempre se llega a uno como tarde en la cuarta fila. Como mucho hay dos en una fila.
- **El Trueque sale algo menos,** para dejar sitio a los Refugios.
- **Es de momento:** se revisa al jugarlo. Apuntad si las expediciones se pierden demasiado.

### La llave, misión nueva

- **Dónde:** siempre en la comisaría, desde la tercera fila del mapa.
- **La armería está cerrada, con todos sus muebles llenos:** los cuatro armeros con fusiles.
  - **La llave la lleva uno de los muertos de uniforme.** No lleva marca: hay que matarlos y registrar sus cuerpos hasta dar con ella.
  - **Con la llave en la mochila,** la puerta se abre con «Abrir con la llave». La llave se gasta. Sin ella: «Está cerrada. Hace falta la llave de la armería.»
  - **También se entra volando la pared con una granada,** con su ruido. La explosión vacía los muebles que alcanza.
  - **Si cae quien la lleva,** la llave queda en el suelo, y se recoge.
  - **La llave no sale de la etapa.**
- **Se gana como un Saqueo:** saliendo en La Lata. La armería es el premio: no da los 40–80 § de las otras misiones especiales.
- **El objetivo, en tres pasos:** buscar la llave, abrir la armería o volar su pared, y salir en La Lata.
- **En el mapa de etapas,** La llave lleva su icono.

### Los coches

- **Uno de cada tres coches ya está saqueado:** se ve oxidado y no se registra. Los que arden, tampoco.
- **Oxidado quiere decir que no se registra.** Un coche que se registra se ve entero, con su color.
- **Los que se registran se ven abandonados:** suciedad, hojas en el techo y maleza junto a las ruedas; algunos, caídos sobre una rueda deshinchada.
- **Sus maleteros dan más,** y nunca están vacíos.
- **El contorno de registrar** rodea solo la tapa del maletero, no el coche entero.

### Tres cielos

- **Cada etapa tiene su cielo:** despejado (la mitad de las veces), encapotado o atardecer, cada uno con su luz.
  - **De noche** manda la noche. **Con tormenta,** siempre encapotado.
  - **Es solo aspecto:** no cambia ninguna regla ni sale en la ficha de la etapa.
- **La base** conserva su cielo y su luz de siempre.
- **Fuera el fondo desenfocado:** el «Aspecto de película» ya no desenfoca el fondo.

### Entre etapas, en la carretera

- **Antes:** el parte entre etapas se veía en el claro de la base.
- **Ahora:** la escuadra para en un tramo de carretera entre pinos, junto a su vehículo (la moto, el coche, la furgoneta o el autobús), con el cielo y el tiempo de la etapa recién jugada. La base de verdad sigue en el claro.

### Otros cambios

- **La primera partida** entra directa a «Partir», sin pestañas. La base entera, con Almacén y Soldados, sale al volver de la primera expedición.
- **El mapa del combate (M)** se acerca y se aleja con la rueda.
- **Con Mayús,** también se marcan las habitaciones ya descubiertas que quedan por registrar, con el suelo teñido y un borde.
- **El registro junta lo igual:** «Anja encuentra 40 §, 2 pistolas y 3 cargadores.» Antes decía «una pistola y una pistola».
- **En el parte de la etapa,** el botón de puntos por gastar cabe con 8 soldados, y el «%» de las tarjetas ya no se queda solo en otra línea.

## Lo que sigue sin arreglar

- **La puerta cerrada de la armería** aún no lleva el candado dibujado.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **La dificultad con 2 plazas** aún no se ha ajustado. Apuntad lo que notéis al jugar.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.13.4-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.13.4-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.13.4:** con la 0.13.3 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.13.3.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
