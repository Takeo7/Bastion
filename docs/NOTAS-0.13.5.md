# Notas de la 0.13.5

Texto de Lore para la 0.13.5. Lleva lo decidido por Arturo el 19/11 tras la 0.13.4. Commits locales: d5a507a, 8fe9ed9 y b4544d1, más El herido en los mapas, que aún no tiene commit. Fuentes:
- lo decidido por Arturo: `.dev-snapshots/producto/RUNS.md`, cuadros 89 a 92;
- la revisión de Producto: `.dev-snapshots/producto/notas-2026-11-18/REVISION.md`, pasadas 62 a 71;
- los contratos de Gameplay: `.dev-snapshots/gameplay/g23/MORDIDO.md` y `g23/HERIDO.md`;
- los mensajes de los tres commits.

Lo que no va en las notas:
- **Lo de dentro:** las pruebas, los bots, los bancos y los laboratorios.
- **Lo no probado:** el rendimiento.

Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Va para Mac y Windows. Lo que va entre [corchetes] lo completa Producto al publicar: el commit. El aviso de Windows sin probar se quita si alguien la prueba en un PC. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.13.5: Mordido y El herido

Versión 0.13.5 (commit [commit]). Salen en los mapas dos misiones nuevas: Mordido, en el hospital, y El herido. En La llave, la armería se ve cerrada con una puerta roja blindada. La llave y el antiviral, si caen, se ven en el suelo.

## Partidas guardadas

- **La partida de la 0.13.4 sigue en la 0.13.5.** No se empieza de cero.
- **Una expedición ya empezada** sigue con su mapa, sin Mordido ni El herido. Salen desde la expedición siguiente.

## Novedades de la 0.13.5

### Mordido, misión nueva

- **Dónde:** siempre en el hospital, de la segunda a la quinta fila del mapa.
- **Empieza como un Rescate:** alguien espera en una zona marcada, se le encuentra con la vista y se une a la escuadra con «Hablar».
- **Está mordido.** Desde que se le encuentra, le quedan 15 turnos. La cuenta baja al acabar cada turno de la escuadra.
  - **Se ve** en su bandera y en el objetivo; con 2 turnos y con 1, en rojo, y el registro avisa.
- **Lo cura el antiviral.** Está en uno cualquiera de los armarios o estantes de medicinas del hospital, nunca en un carro de curas.
  - **No se distingue** de los demás muebles: hay que registrarlos. El mueble que lo tiene da, además, lo de siempre.
  - **Se usa con «Usar»,** desde una casilla de al lado, con una acción, antes o después de hablarle. La cuenta se para y el antiviral se gasta.
  - **Las medicinas** curan la salud, no la mordedura.
  - **Si cae quien lo lleva,** el antiviral queda en el suelo, y se recoge.
  - **No sale de la etapa.**
- **Se gana** con la persona mordida curada y a bordo de La Lata: el recluta y los 40–80 § de las misiones especiales.
- **Si se convierte,** muere y se levanta como un muerto más en su casilla. Se puede salir en La Lata, sin recluta ni recompensa.
- **Ocupa plaza en La Lata.** Con La Lata llena, la ficha de la etapa en el mapa avisa «Sin plaza libre», como en el Rescate: al salir, quien no quepa se queda fuera.
- **Es de momento:** se revisa al jugarlo. Apuntad si da tiempo a encontrar el antiviral.

### El herido, misión nueva

- **Dónde:** de la segunda a la quinta fila, en los mismos mapas que el Rescate.
- **Alguien con la pierna rota** pide ayuda en una zona marcada, y se le encuentra con la vista. No anda ni hace nada: hay que llevar a la persona herida a hombros hasta La Lata.
- **Tres acciones nuevas,** de una acción cada una y desde una casilla de al lado:
  - **«Cargar»:** a hombros. La primera vez, se une a la escuadra, sin «Hablar».
  - **«Dejar»:** en una casilla libre. En una casilla de La Lata sube en el acto, y el botón dice «Subir a … a La Lata».
  - **«Pasar»:** a alguien de la escuadra que esté al lado y no cargue a nadie. Recibir no cuesta nada.
- **Quien carga** anda como mucho 6 casillas por turno con ella a hombros. No dispara el arma larga, tampoco en vigilancia; la pistola y el cuerpo a cuerpo, sí.
- **A hombros,** nadie le apunta. Se ve atravesada sobre los hombros de quien carga, que esconde la mochila y el arma larga, y su bandera dice «CARGANDO».
- **En el suelo** se ve sentada, con la pierna entablillada y «PIERNA ROTA» en su bandera.
  - **Desde que se le encuentra,** los muertos van a por ella, como a por cualquiera.
  - **Si cae quien carga,** queda en el suelo en esa casilla, sin daño.
- **No ocupa plaza en La Lata.** Sube con quien la carga, o al dejarla en una casilla de La Lata.
- **La Lata no sale sin ella** mientras siga viva: el botón de salir lo dice, con su nombre.
- **Se gana** con ella a bordo: el recluta y los 40–80 § de las misiones especiales. **Si muere,** se sale sin recluta ni recompensa.
- **Es de momento:** se revisa al jugarlo. Apuntad si cuesta demasiado llegar con ella a La Lata.

### La llave: la puerta de la armería

- **La armería se cierra con una puerta roja blindada,** con candado. Se ve por las dos caras, y el corte de muros no la recorta. Abierta, ya no se ve roja.
- **Con la pared volada,** la puerta sigue cerrada junto al boquete.
- **En la comisaría de las demás misiones,** la puerta de la armería es la de siempre.

### Lo que cae, en el suelo

- **Antes:** la llave caída no se veía en el suelo, solo su marca, un rombo. Las notas de la 0.13.4 no lo decían.
- **Ahora:** la llave y el antiviral caídos se ven en el suelo, con un haz y un aro verdes, sin el rombo que los tapaba. El antiviral es un frasco naranja con una jeringa.
  - **Lo que se busca en una Recuperación** sigue con su rombo.
- **En la mochila,** la llave y el antiviral tienen su dibujo. El del antiviral es una jeringa con líquido verde.

## Lo que sigue sin arreglar

- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **La dificultad con 2 plazas** aún no se ha ajustado. Apuntad lo que notéis al jugar.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.13.5-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.13.5-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.13.5:** con la 0.13.4 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.13.4.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
