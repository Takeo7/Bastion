# Notas de la 0.13.1

Texto de Lore para la 0.13.1, que lleva las notas de Ana tras la 0.13.0 y las 23 de Arturo del 18/11. Fuentes:
- lo decidido por Arturo, cuadro a cuadro: `.dev-snapshots/producto/RUNS.md` (desde «Notas de Ana tras la 0.13.0») y `TABLERO.md`;
- las notas de Arturo: `.dev-snapshots/producto/notas-2026-11-18/NOTAS.md` y la revisión de Producto, `REVISION.md`;
- el motor: `.dev-snapshots/gameplay/n2-n4/CONTRATO.md` y `.dev-snapshots/gameplay/t2/T2-GAMEPLAY.md`;
- UI: `.dev-snapshots/ui/unity/runs/n7/LEEME.md` y `notas-18/LEEME.md`;
- los textos de Lore: `.dev-snapshots/lore/runs/N8-RESCATE.md` y `.dev-snapshots/lore/notas-18-11/`.

Lo que no va en las notas:
- **Lo de dentro:** los bancos de bots y sus cifras, los laboratorios y las pruebas.
- **Lo que ya no se ve:** los retoques de las armas dentro del cuerpo que no se ven desde las cámaras del juego (regla del 22/10).

Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Va para Mac y Windows. Lo que va entre [corchetes] lo completa Producto al publicar: el commit. El aviso de Windows sin probar se quita si alguien la prueba en un PC. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md). La partida guardada va primero, como en la 0.13.0, porque es lo primero que se encuentra.

---

**Título:** Bastión 0.13.1: la moto y el Rescate

Versión 0.13.1 (commit [commit]). Toda expedición sale ahora con 2 plazas, en moto. El superviviente del Rescate se busca en el mapa y se le habla, y a veces hay alguien con vida en cualquier mapa. Salen también los cambios de las notas de Arturo: la ficha de los objetos, la barra Registrar, «Partir» y «Evacuar», y un ajuste para el aspecto de película.

## Partidas guardadas: siguen, con 2 plazas

- **La partida de la 0.13.0 sigue en la 0.13.1.** No se empieza de cero.
- **Las plazas vuelven a 2,** como en toda partida nueva. La mejora «Más plazas» vuelve a empezar, y lo que costó vuelve al almacén: 150 § si teníais 6 plazas, y 450 § si teníais 8. Con 4 plazas no se había pagado nada.
- **La primera vez que se carga, un aviso lo dice:** «Ahora toda expedición sale con 2 plazas, en moto. La mejora «Más plazas» vuelve a empezar, y los 150 § que costó están en el almacén.»
- **Una expedición a medias** sigue con la escuadra que salió. La siguiente ya sale con 2.
- **En el mapa de una expedición a medias:**
  - una parada «Superviviente» pasa a ser un Refugio;
  - una etapa «Arreglar la radio» pasa a ser un Saqueo. Si se estaba jugando justo esa, sigue como estaba.

## Novedades de la 0.13.1

### Se sale en moto, con 2 plazas

- **Toda expedición sale con 2 plazas,** en una moto, que también se llama La Lata. Con dos jugadores, un soldado cada uno.
- **Los soldados que no salen** esperan en la base.
- **Más plazas sube a 4, 6 y 8,** y cambia el vehículo:

| Plazas | La Lata | Lo que cuesta la mejora |
|---|---|---|
| 2 | Moto | — |
| 4 | Todoterreno | 40 § |
| 6 | Furgoneta | 150 § |
| 8 | Autobús | 300 § |

- **Quien conduce la moto** lleva el fusil a la espalda, con las manos en el manillar.
- **En Escuadra** sale el vehículo que toca. Antes salía siempre el autobús. Debajo dice cuál es, por ejemplo: «La Lata es una moto: 2 plazas.»

### Subir a La Lata

- **Antes:** había que esperar en la zona de La Lata hasta que salía, y allí te podían ver y atacar.
- **Ahora:** al acabar su movimiento en una de sus casillas, el soldado sube y está a salvo: no le ven ni le atacan. Pasar por encima sin pararse no cuenta.
  - **Antes de mover,** el camino avisa: «Subirá a La Lata».
  - **Su busto** dice «A BORDO».
  - **En su turno puede bajar** («Bajar»), con una acción, a una casilla libre junto a las puertas. Le queda la otra acción y todo su movimiento, y deja libre su plaza para otro.
  - **Dentro solo se puede bajar o pedir «Evacuar».**
  - **A dos, con todos tus soldados a bordo,** el mando no pasa solo: decides tú si bajas a alguien o pulsas «Ceder el mando».
- **Las casillas de La Lata son sus plazas libres,** junto a sus puertas: al subir uno, desaparece una. Llena, no queda ninguna, y nadie más puede subir: «No queda plaza en La Lata.» La plaza es de quien sube antes.
- **La Lata sale:**
  - cuando lo pedís con «Evacuar», desde dentro; a dos, lo acepta el compañero, salvo que todos los suyos ya estén dentro;
  - o sola, cuando ya están dentro todos los vivos.
- **Quien siga fuera cuando sale, se pierde.** El aviso al pedir la salida dice quién se quedaría fuera.
- **En una Recuperación,** el objeto cuenta en cuanto sube quien lo lleva, y se queda en La Lata aunque baje.

### El Rescate

- **Antes:** había que llegar a la persona atrapada, y al acabar se unía a la escuadra.
- **Ahora:** se la busca en el mapa, se le habla y se une en ese momento.
  - **Está en una zona marcada,** un círculo blanco de unas 10 casillas de ancho. No se ve hasta que alguien de la escuadra la ve. Al verla, el registro dice «… ve a alguien con vida.»
  - **Unas veces está sola y escondida,** agachada, y su bandera dice «ESCONDIDO/A». **Otras, presa de los Cuervos,** de rodillas y con grupos de vivos alrededor, y su bandera dice «CAUTIVO/A». Con Cuervos solo los hay desde la fila 3: de ahí en adelante, más o menos uno de cada tres Rescates. La ficha de la etapa lo dice antes de elegirla.
  - **Hablar:** una acción nueva, desde una casilla de al lado. Tu soldado le pregunta «¿Te vienes?», y le contesta «Sí. Vámonos de aquí.»
  - **Siempre acepta.** Pasa a ser un soldado del jugador que le habló, con un cuchillo, y actúa ese mismo turno, con sus dos acciones.
  - **Si está dentro de La Lata cuando sale,** se queda en la escuadra para el resto de la expedición, y después en la base.
  - **Si muere,** la etapa sigue: se gana igual volviendo a La Lata, con su recompensa, pero sin recluta.
- **Sin plaza libre,** el mapa lo avisa antes de entrar: «Sin plaza libre», en rojo, bajo la etapa. Para que suba quien rescatáis, alguien de la escuadra tendrá que bajar y quedarse fuera.
- **Siempre hay un Rescate a mano** en las filas 2 a 4, desde cualquier salida.

### Alguien con vida en cualquier mapa

- **En uno de cada seis combates, más o menos,** alguien con vida se esconde dentro de un edificio. No hay aviso ni marca.
- **Se le encuentra y se le habla** como en el Rescate, y se une igual.
- **No sale** en los Rescates ni en el combate del Toro.

### Lo que se ha quitado

- **La parada «Superviviente».** Los reclutas llegan ahora por el Rescate y por los supervivientes de cualquier mapa. Quedan tres paradas: Refugio, Trueque y Suceso.
- **Dos Sucesos ya no dan un recluta:** «Alguien herido» («Que os espere más adelante») y «La radio» («Contestar»). Ahora convierten una de las etapas siguientes en un Rescate, siempre sin Cuervos.
- **La etapa «Arreglar la radio»,** y con ella todo lo de arreglar en las expediciones. Su parte va a las otras ocho especiales.

### La barra Registrar

- **La barra Investigar pasa a llamarse Registrar.** Se llena igual: registrando habitaciones y cuerpos, abriendo cajas y maleteros, y apagando alarmas.
- **Su cifra es ahora «Suerte al registrar»:** la probabilidad de sacar algo más al registrar o al abrir. Empieza en 10 % y crece con la barra. Antes era «Destreza al arreglar».
- **Sus cinco casillas de arreglar cambian:**

| Antes | Ahora | Qué hace |
|---|---|---|
| Manitas | De un vistazo | Una vez por combate, registrar una habitación no gasta acción |
| Dedos hábiles | Gancho | Abre cajas y maleteros, y registra cuerpos, desde 2 casillas |
| Ganzúa | Ganzúa | +25 % de suerte al abrir cajas y maleteros |
| Ingeniero | Armero | Al registrar, los accesorios de arma salen el doble de veces |
| Mecánico | Buscavidas | +25 % de suerte al registrar habitaciones |

- **Quien ya tenía esas casillas** tiene ahora las nuevas, en el mismo sitio del tablero.
- **El botón de combate «Investigar» pasa a «Registrar».** Su icono dice qué hay al lado: un coche, un cuerpo o una caja.
- **Las casillas que piden un soldado del otro jugador** (Fuego cruzado, Codo con codo, Parapeto, Hombro con hombro, Señalar, Uña y carne, Te cubro y Entre los dos) valen jugando solo con cualquier compañero. A dos, como antes.

### El ataque de reacción, también andando

- **Antes:** un zombi mordía al pasar a su lado solo si el soldado corría.
- **Ahora:** también andando. Cada zombi que esté al lado de una casilla del camino y te vea intenta morder una vez. Es un ataque de reacción y puede fallar. En la casilla donde te paras no muerde.
- **Antes de mover,** el camino marca con un aro rojo cada casilla donde te morderían, con una línea hasta el zombi.

### Las ventanas

- **Pegado a la pared, justo al lado de una ventana de la planta baja,** se ven una o dos filas de casillas detrás del cristal, unas 8.
- **Los enemigos que estén ahí se ven,** pero el edificio no se descubre. Al irse, se deja de ver.

### La ficha de los objetos

- **Al pasar el ratón por un objeto** sale lo que hace, en el Almacén, en el Trueque, en la mochila y al registrar.
- **En las armas,** una línea con para qué sirve, y sus cifras: Daño, Puntería, Crítico, Alcance, Cargador, Ruido, y las acciones y lo que quita de blindaje cuando importan.
  - **El alcance va en casillas:** «hasta 18 casillas; mejor de 1 a 11». En cuerpo a cuerpo, «1 casilla», y en el rastrillo, «2 casillas».
  - **El cargador cuenta disparos:** «4 disparos». Antes decía balas.
  - **Se compara con el arma que lleva en ese hueco el soldado elegido:** cada cifra en verde si es mejor y en rojo si es peor, con la diferencia al lado, por ejemplo «(+1)».
- **En lo del cinturón,** cuántos usos da cada pieza: dos la granada, el cóctel molotov y el botiquín; uno la granada de humo y la aturdidora. Se usa desde el cinturón: en la mochila no se puede usar.
- **En lo demás,** lo que hace: los accesorios de arma, los chalecos, los cargadores, las medicinas y los suministros.

### «Partir» y «Evacuar»

- **En la base, la pestaña y el botón «Salir» pasan a «Partir».** Parecía que se salía del juego.
- **En combate, el cuadro de salir en La Lata dice «Evacuar»,** como el botón que lo abre: «Evacuar en La Lata», «Pedir evacuar» y «Ana pide evacuar».

### El aspecto de película

- **La imagen se ve más de película:** colores con más contraste y algo más oscuros, más bruma a lo lejos de día y el fondo desenfocado.
- **No cambia las reglas** ni lo que se ve del combate.
- **Con gráficos «Bajos»,** sin desenfoque.
- **Para apagarlo:** Ajustes → «Aspecto de película» → «No». Viene encendido.

### Otros cambios

- **Personalizar:** la figura grande en el centro, con carruseles para la ropa y la cabeza que la cambian al momento, y la complexión y los colores al lado.
- **Soldados:** alrededor de la figura, el equipo que lleva cada uno, con su nombre: arma principal, cuerpo a cuerpo, pistola, chaleco y cinturón.
- **Almacén:** el busto de cada soldado, en vez de su nombre.
- **El mapa de etapas** se construye fila a fila la primera vez que se ve en una expedición.
- **En el mapa, la escuadra** va en cuadrados del mismo ancho. Antes, un nombre largo parecía más vida.
- **En el Trueque y en el mapa** ya no salen arriba los suministros de la base, que en la expedición no se pueden usar.
- **En combate:**
  - la mochila es un botón claro, con su dibujo y «MOCHILA»;
  - las teclas del 1 al 0 son la posición de cada botón en la barra;
  - al acabar un soldado, la cámara va al siguiente después de ver entera su acción;
  - al terminar el turno, el parpadeo ya no mueve los demás botones;
  - la niebla y lo que se ve cambian casilla a casilla mientras anda un soldado, no al final;
  - la rueda del ratón acerca y aleja más deprisa: unas cuatro vueltas de lejos a cerca, donde antes eran diez;
  - con seis soldados o más, el estado de cada uno sale en su busto como un icono, porque el nombre no cabe; el nombre entero sigue en la bandera sobre su cabeza.
- **El fusil de cerrojo** dispara una bala cada vez, no una ráfaga, y tiene su propio icono. Sus cifras son las del fusil de asalto.
- **Las armas, en las manos:** se agarran mejor, y las ametralladoras se meten menos en el pecho.
- **En la base,** cada soldado lleva en la mano su arma de verdad: con solo un cuchillo, ya no sale con una pistola.
- **La escopeta de corredera** tiene un modelo nuevo.
- **Quien vuelve a entrar en la partida al acabar una etapa** ve la línea de los reclutas que han llegado.

## Lo que sigue sin arreglar

- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle. Queda para otra versión.
- **La dificultad con 2 plazas** aún no se ha ajustado. Apuntad lo que notéis al jugar.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.13.1-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.13.1-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.13.1:** con la 0.13.0 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.13.0.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
