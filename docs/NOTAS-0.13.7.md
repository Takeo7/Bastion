# Notas de la 0.13.7

Texto de Lore para la 0.13.7. Lleva lo decidido por Arturo el 20 y el 21/11 tras la 0.13.6. Commit local: 42423ad, más Saqueadores en los mapas, que aún no tiene commit. Fuentes:
- lo decidido por Arturo: `.dev-snapshots/producto/RUNS.md`, cuadros 98, 99 y 104 (el 106, no: el Cazador sigue con lanza en esta versión);
- la revisión de Producto: `.dev-snapshots/producto/notas-2026-11-18/REVISION.md`, pasadas 87 a 89;
- el contrato de Gameplay: `.dev-snapshots/gameplay/g23/SAQUEADORES.md`, sobre todo «En los mapas»;
- los textos de Lore: `.dev-snapshots/lore/SAQUEADORES-TEXTOS.md`;
- lo de UI: `.dev-snapshots/ui/unity/runs/u28/LEEME.md`;
- el mensaje del commit 42423ad.

Lo que no va en las notas:
- **Lo de dentro:** las pruebas, los bots, los bancos y los laboratorios.
- **Lo que no entra en esta versión:** Evacuación, el arco, la ballesta y las misiones nuevas.
- **Lo no probado:** el rendimiento.

Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Va para Mac y Windows. Lo que va entre [corchetes] lo completa Producto al publicar: el commit. El aviso de Windows sin probar se quita si alguien la prueba en un PC. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.13.7: Saqueadores

Versión 0.13.7 (commit [commit]). Sale en los mapas una misión nueva, Saqueadores: un campamento de los Cuervos con su alijo, para robarlo sin que os vean o a tiros. En todo el juego, con la escuadra oculta, un vivo desprevenido ya no nota a quien le llega por detrás, y el golpe silencioso vale contra el Saqueador y el Cazador.

## Partidas guardadas

- **La partida de la 0.13.6 sigue en la 0.13.7.** No se empieza de cero.
- **Una expedición ya empezada** sigue con su mapa, sin Saqueadores. Sale desde la expedición siguiente.

## Novedades de la 0.13.7

### Saqueadores, misión nueva

- **Dónde:** desde la tercera fila, en el campo, el área de servicio y el camping.
  - **Su icono** en el mapa de etapas es una calavera con huesos cruzados.
- **El campamento de los Cuervos** está lejos de donde entra la escuadra, y no se ve al empezar. La Lata espera cerca de la entrada: el campamento es un desvío que se elige.
  - **En el campo y el área de servicio,** en un claro:
    - un cerco bajo de sacos, chapas, palés y tablas, con dos o tres huecos, uno del lado por el que entra la escuadra;
    - una hoguera y tiendas;
    - el alijo: tres cajas naranjas sobre palés.
  - **En el camping,** en la zona militar, con su valla y dos huecos.
  - **Los muertos** empiezan lejos del campamento y del camino hasta él.
- **Los guardias:** cuatro, o cinco en la quinta fila y desde el capítulo 2. Son el Cabecilla, Saqueadores y un Cazador.
  - **Uno hace la ronda** por fuera del cerco.
  - **Otro vigila el alijo,** de pie junto a la caja más lejana de la entrada, mirando hacia fuera.
  - **Los demás, sentados en la hoguera,** miran al fuego y solo ven a 4 casillas por delante.
  - **Un ruido** manda a mirar a quien lo oye. El de la hoguera que lo oye se levanta y ve como los demás.
  - **Si uno ve a un soldado, despierta todo el campamento:** se ponen a cubierto y disparan, como los Cuervos de un Rescate. Los muertos también van a por ellos.
- **El alijo es la única recompensa:** no hay los 40–80 § de las misiones especiales.
  - **Cada caja** se abre con una acción, desde una casilla de al lado, y hace un poco de ruido.
  - **Da** la mitad de las veces un arma larga (fusil de asalto, de cerrojo o escopeta) con un cargador; si no, el doble de los suministros de una caja y una medicina.
  - **Sin robarlo, la etapa es un Saqueo:** se gana saliendo en La Lata.
- **Los tiros de los Cuervos** también llenan el medidor de la horda, como los vuestros. En las demás etapas, solo cuentan los vuestros.
- **Puede salir** de noche, con mal tiempo o con «Horda despierta», como un Saqueo. De noche alumbra la hoguera.
- **No sale ningún superviviente suelto.**
- **Si el mapa no tiene sitio para el campamento,** cosa que pasa muy pocas veces, la etapa es un Saqueo, con su objetivo. La tarjeta sigue diciendo «SAQUEADORES», y al empezar el registro avisa: «Los Cuervos se han ido: no hay campamento. La etapa es un Saqueo.»
- **Es de momento:** se revisa al jugarlo. Apuntad si el alijo vale el riesgo.

### Saqueadores en pantalla

- **El objetivo va por pasos:** «Robad el alijo: quedan 3 de 3 cajas», que baja al abrirlas, y «Salid en La Lata». Robar es opcional: se puede salir sin el primer paso. Su nota cuenta cómo vigila el campamento.
- **Las cajas del alijo** llevan encima la calavera con un aro naranja, desde el principio y a través de la niebla.
  - **La flecha del objetivo y la tecla O** van a la caja cerrada más cercana y, después, a La Lata.
- **Sobre la bandera de cada guardia:**
  - **«DESPREVENIDO»** (o «DESPREVENIDA»), en arena, mientras el campamento duerme;
  - **«ALERTA»,** en rojo, desde que despierta.
- **Hacia dónde mira cada guardia:** un abanico corto en el suelo, en rojo, mientras el campamento duerme. Al despertar, desaparece.
  - **Hasta dónde os notaría:** con el ratón encima del guardia, o dejando el dedo sobre él, salen en el suelo sus casillas. El ojo de «lo que ven» enseña las de todos.
  - **Los ojos rojos** del alcance de andar cuentan igual: los de la hoguera, a 4 casillas como mucho.

### Sigilo, en todo el juego

- **Un vivo que aún no está alerta,** con la escuadra oculta, solo nota a quien tiene delante: por detrás o de lado, no os nota aunque estéis pegados. Antes notaba también a quien tenía pegado. Vale también para los Cuervos de un Rescate.
- **El golpe silencioso** vale ahora contra el Saqueador y el Cazador que aún no estén alerta, también en el Rescate. Antes, solo contra el Zombi y el Corredor.
  - **Sobre ellos** sale «GOLPE SILENCIOSO», como sobre un zombi.
  - **Contra el Cabecilla, no:** «Contra el Cabecilla no hay golpe silencioso.»
- **«Por el camino os ven»:** antes solo se avisaba si os veían desde la casilla de destino. Ahora también si el camino pasa por una casilla donde un enemigo os notaría.
  - **En esa casilla,** un anillo rojo y una raya hasta el enemigo. El anillo del destino, rojo, y debajo, «Por el camino os ve un Saqueador».
  - **El primer clic pregunta:** «Por el camino os ven: se rompe la ocultación. Haz clic otra vez para mover.». Con el dedo, «Toca otra vez para mover.».
  - **Solo cuentan los enemigos que veis,** como los ojos rojos de siempre.
- **El glosario** («Ocultación» y «Golpe silencioso») y las fichas del Saqueador, el Cazador y el Cabecilla ya lo dicen.

## Lo que sigue sin arreglar

- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **La dificultad con 2 plazas** aún no se ha ajustado. Apuntad lo que notéis al jugar.
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.13.7-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.13.7-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.13.7:** con la 0.13.6 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.13.6.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
