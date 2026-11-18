# Notas de la 0.13.3

Texto de Lore para la 0.13.3. Lleva lo decidido por Arturo el 18/11 tras la 0.13.2 (commits locales ae0103c, b40b6e0 y d95d03c). Fuentes:
- lo decidido por Arturo: `.dev-snapshots/producto/RUNS.md`, cuadros 64 a 69;
- la revisión de Producto: `.dev-snapshots/producto/notas-2026-11-18/REVISION.md`, pasadas 40 a 44;
- el contrato de los muebles: `.dev-snapshots/gameplay/muebles/CONTRATO.md`, y `Looting.RollPiece`;
- los mensajes de los tres commits.

Lo que no va en las notas:
- **Lo de dentro:** las pruebas, los laboratorios y las sondas.
- **Lo no probado:** el rendimiento.

Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Va para Mac y Windows. Lo que va entre [corchetes] lo completa Producto al publicar: el commit. El aviso de Windows sin probar se quita si alguien la prueba en un PC. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.13.3: los muebles se registran

Versión 0.13.3 (commit [commit]). En la comisaría y en el hospital se registra mueble a mueble: armeros, taquillas, armarios de medicinas… Lo que el soldado puede registrar desde donde está se marca sin pasar el ratón. Y tres arreglos de la 0.13.2: el Gancho a 2 casillas, el coche patrulla que no se veía y La Lata de 8 plazas, que atravesaba muros.

## Partidas guardadas

- **La partida de la 0.13.2 sigue en la 0.13.3.** No se empieza de cero.
- **Si se guardó a mitad de un combate,** ese combate acaba con las reglas de antes: las salas se registran enteras y los muebles no. Desde el siguiente, ya no.

## Novedades de la 0.13.3

### Los muebles se registran uno a uno

- **Antes:** en la comisaría solo se registraban la caja de la armería y las salas enteras. En el hospital, las salas se registraban como las de una casa: salían suministros y casi nunca medicinas.
- **Ahora:** los muebles propios de la comisaría y los de medicinas se registran uno a uno, como una caja: con «Abrir o registrar», al lado del mueble (con Gancho, desde 2 casillas), y una vez para toda la escuadra.
  - **Se abre** lo que tiene puerta o tapa, y **se registra** lo que está abierto: «Abrir las taquillas», «Registrar el armero».
  - **Solo se marca el mueble que tiene algo.** El que no tiene nada se ve vacío desde el principio.
  - **Ya registrado,** el mueble se ve abierto o vacío.
  - **En una sala con estos muebles** no se registra la habitación entera: «Registrar la habitación» y «Rebuscar» salen apagadas, con «En esta habitación se registra mueble a mueble.»

**En la comisaría:**
- **El armero,** en la armería: un fusil de asalto o una escopeta. De los cuatro armeros, solo uno tiene fusiles; los otros se ven vacíos.
- **El armario de armas,** en la armería: una pistola.
- **Los maletines de armas,** en la armería: una pistola o un accesorio de arma.
- **Las cajas de munición,** en la armería: dos cargadores.
- **Las taquillas,** en el vestuario: un chaleco (a veces, de placas), un cargador o nada.
- **El estante de pruebas,** en la sala de pruebas: suministros, algo para el cinturón o nada.
- **Si hay un agente muerto,** su cuerpo se registra como el de un enemigo. A veces lleva una pistola o un chaleco.
- **Lo de la armería da siempre algo.** Lo demás, a veces nada.
- **Ya no hay caja de armas en la armería.** Y registrar una sala de la comisaría ya no da de más un arma, un cargador o un chaleco.
- **Los dos coches patrulla de delante** son coches enteros, con maletero: «Abrir el maletero del coche de policía». Una vez de cada cinco hay un arma; si no, lo que haya en cualquier maletero. A veces, también un chaleco.

**Las medicinas,** en el hospital y en la farmacia de la plaza:
- **El armario de medicinas:** una medicina o un botiquín. Solo uno de cada cuatro tiene algo; los demás se ven vacíos.
- **El estante de medicinas,** nuevo, con frascos y cajas a la vista: una medicina, un botiquín o nada.
- **El carro de curas:** una medicina o nada.
- **El hospital, registrado entero,** da unas 10 medicinas y 3 botiquines, donde antes daba 1 y 1, y menos suministros.

### Lo que se puede registrar, marcado sin ratón

- **Antes:** lo que el soldado tenía al lado solo se marcaba al pasar el ratón por encima o con Mayús. Las notas de la 0.13.2 no lo decían.
- **Ahora:** con un soldado elegido y quieto, en tu turno, se marca en claro todo lo que puede registrar desde donde está: cuerpos, maleteros, cajas, muebles y el cuerpo de un compañero caído que lleva cosas.
  - **Las marcas salen a la vez** que el área de andar.
  - **Sin acciones,** no se marca nada, salvo el compañero caído: coger lo suyo no gasta acción.
  - **De lejos,** nada, como antes.
  - **En el mapa (M),** no se marcan, salvo con Mayús.
- **Con Mayús,** lo de la planta de abajo solo se marca si no hay nada encima que lo tape.

### Arreglos de la 0.13.2

- **Con Gancho, lo de a 2 casillas** se marcaba, pero el clic no lo registraba. Pasaba también en la 0.13.1. Ahora se registra, con el clic y con la tecla.
- **El coche patrulla de la comisaría** no se dibujaba: junto a la entrada había una cobertura alta que no se veía. Ahora hay dos coches, y se ven.
- **El botón del panel** cortaba los nombres largos, como «ABRIR EL MALETERO DEL COCHE DE POLICÍA». Ahora caben enteros.
- **La Lata con 8 plazas,** el autobús, atravesaba el muro en las comisarías, los hospitales, las oficinas y las naves, y en el camping pisaba las tiendas. Ahora cabe en la calle.

## Lo que sigue sin arreglar

- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle. Queda para otra versión.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **La dificultad con 2 plazas** aún no se ha ajustado. Apuntad lo que notéis al jugar.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.13.3-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.13.3-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.13.3:** con la 0.13.2 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.13.2.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
