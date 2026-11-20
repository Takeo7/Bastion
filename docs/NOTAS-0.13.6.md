# Notas de la 0.13.6

Texto de Lore para la 0.13.6. Lleva lo decidido por Arturo el 20/11 tras la 0.13.5. Commits locales: de89a8b y 2158189. Fuentes:
- lo decidido por Arturo: `.dev-snapshots/producto/RUNS.md`, cuadros 94 a 97;
- la revisión de Producto: `.dev-snapshots/producto/notas-2026-11-18/REVISION.md`, pasadas 80 a 83;
- el contrato de Gameplay: `.dev-snapshots/gameplay/g23/SEPARADOS.md`;
- lo de UI: `.dev-snapshots/ui/unity/runs/u27/LEEME.md`;
- los mensajes de los dos commits y `.dev-snapshots/lore/NOTAS-SIGUIENTE.md`.

Lo que no va en las notas:
- **Lo de dentro:** las pruebas, los bots, los bancos y los laboratorios.
- **Lo no probado:** el rendimiento.

Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Va para Mac y Windows. Lo que va entre [corchetes] lo completa Producto al publicar: el commit. El aviso de Windows sin probar se quita si alguien la prueba en un PC. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.13.6: Separados

Versión 0.13.6 (commit [commit]). Sale en los mapas una misión nueva, Separados: la escuadra empieza partida en dos grupos. El aviso al evacuar dice lo que pasa de verdad en cada etapa de una expedición. La molotov ya no estropea los coches.

## Partidas guardadas

- **La partida de la 0.13.5 sigue en la 0.13.6.** No se empieza de cero.
- **Una expedición ya empezada** sigue con su mapa, sin Separados. Sale desde la expedición siguiente.

## Novedades de la 0.13.6

### Separados, misión nueva

- **Dónde:** de la segunda a la quinta fila, solo en los mapas abiertos: ciudad, pueblo, polígono, fábrica, área de servicio y campo. Nunca en los mapas de un edificio, ni en el centro comercial ni en el camping.
  - **Su icono** en el mapa de etapas son dos bustos.
- **La escuadra empieza partida en dos grupos,** a los dos lados de La Lata.
  - **A dos:** cada jugador, con sus soldados. Los grupos pueden salir desiguales, por ejemplo 3 y 1.
  - **Solo,** o si uno de los dos no tiene soldados: la escuadra, por la mitad, en su orden. Si son impares, va uno más en el primero.
  - **Con 2 plazas:** un soldado por grupo.
  - **Con un solo soldado** no hay grupos: se juega como un Saqueo, con la recompensa de la etapa.
- **Los dos lados:** al empezar, el mapa aparta a los muertos de los dos grupos, como de la salida de siempre. Los dos caminos hasta La Lata son de largo parecido. Qué grupo va a cada lado cambia de un mapa a otro.
  - **Un lado puede salir más difícil que el otro,** según el mapa. Apuntad si se nota.
- **Se juega como un Saqueo:** se registra, y la horda llega con el ruido, por cualquier borde.
  - **El mapa nunca le pone «Horda despierta».** Si un suceso deja la horda despierta para la siguiente etapa, sí cuenta.
  - **No sale ningún superviviente suelto.**
- **La ocultación es de toda la escuadra:** si ven a un grupo, se pierde para los dos.
- **La Lata sale como siempre:** quien no haya subido se queda fuera, y se pierde.
- **La recompensa,** los 40–80 § de las misiones especiales, sin recluta: solo si La Lata sale con alguien de cada grupo a bordo.
  - **Si no,** la etapa se supera sin la recompensa, y la expedición sigue con los que han salido. Al terminar: «SIN LA RECOMPENSA» y «Salisteis sin nadie del grupo de …».
  - **Si cae un grupo entero,** ya no hay recompensa. El registro lo dice, y el objetivo pasa a «Sin recompensa: salid en La Lata», en rojo.
- **El objetivo va por pasos:** «Llegad a La Lata con alguien de cada grupo» y «Salid en La Lata». Cuando solo falta uno de los dos grupos, debajo, en ámbar: «Falta el grupo de Jonas y Mariana».
- **En pantalla:**
  - **Al empezar,** cada jugador ve su grupo. Jugando solo, el primero.
  - **Una flecha en el borde** señala al otro grupo mientras no se vea: «Marcus y Mariana · 75 casillas», en el color de su jugador. No sale con el otro grupo todo en La Lata, o caído entero.
  - **En la fila de bustos,** cada grupo va junto, separado del otro por un hueco con una raya clara.
- **Si un jugador se desconecta,** el otro lleva sus soldados, como siempre, y puede traer a los dos grupos.
- **Es de momento:** se revisa al jugarlo. Apuntad si con 2 plazas se puede.

### El aviso al evacuar, en todas las etapas

- **Antes,** en una expedición, evacuar sin el objetivo avisaba «la misión se pierde», y no era así.
- **Ahora dice lo que pasa en cada tipo de etapa:**
  - **Recuperación, Sabotaje, Sigilo y Guarida,** con el objetivo sin cumplir: «la etapa se supera sin la recompensa».
  - **Limpieza:** «la Limpieza queda a medias: la etapa se supera sin la recompensa».
  - **Mordido y El herido:** «la etapa se supera sin recluta ni recompensa».
  - **Separados:** «Sin nadie del grupo de … a bordo, la etapa se supera sin la recompensa». Con un grupo de un soldado, «Sin Erik a bordo…». La ficha se llama «Evacuar sin la recompensa».
  - **Con el jefe en pie:** «El Toro sigue en pie: si salís ahora, la expedición acaba y volvéis a la base.»
  - **En las demás,** solo dice quién se queda fuera.
- **Con la recompensa ya perdida** (un grupo caído en Separados, el mordido convertido, el herido muerto): «Evacuar en La Lata. Ya no hay recompensa.». En Mordido y El herido, «Ya no hay recluta ni recompensa.».
- **Con nadie fuera:** «Salís todos.». Antes decía «Estáis todos dentro.», aunque hubiera alguien en las casillas de La Lata sin subir.
- **El mordido sin curar** sale entre los que se quedan fuera, aunque esté en las casillas de La Lata: La Lata no lo sube.
- **La ficha de la tecla** dice «Termina la etapa». Antes, «Termina la misión».
- **Al pulsar «Evacuar» la primera vez,** el aviso va encima de la barra, sobre su placa y partido en líneas, sin pisar los bustos. Lo mismo los demás avisos de ese sitio, como el de la mochila llena.

### Flechas y fichas, en todas las etapas

- **Las flechas del borde** se apartan de la tarjeta del objetivo, la brújula, el registro, la ficha del soldado, los bustos y la barra. Antes, una flecha arriba a la derecha se metía bajo el objetivo, y abajo a la izquierda pisaba los bustos.
- **Las fichas de las teclas** ya no tapan la placa de la barra («Turno de Ana: puedes planear los movimientos») ni los avisos de encima.

### La molotov y los coches

- **Antes,** una molotov en el morro de un coche le quitaba el maletero mientras duraba el fuego. Al volver a cargar el combate con el fuego encendido, el coche salía oxidado y en llamas.
- **Ahora,** el coche queda como estaba, con su maletero. Solo arden los coches que ya ardían en el mapa.

## Lo que sigue sin arreglar

- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **La dificultad con 2 plazas** aún no se ha ajustado. Apuntad lo que notéis al jugar.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.13.6-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.13.6-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.13.6:** con la 0.13.5 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.13.5.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
