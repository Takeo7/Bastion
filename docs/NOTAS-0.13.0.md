# Notas de la 0.13.0

Texto de Lore para la 0.13.0: las expediciones, que eligió Arturo del 16 al 17/11. Fuentes:
- lo decidido por Arturo, cuadro a cuadro: `.dev-snapshots/producto/RUNS.md` y `TABLERO.md`;
- el motor: `.dev-snapshots/gameplay/runs/LEEME-H0.md` a `LEEME-H9.md`, `H9-INTERIORES.md` y `G3-NUMEROS.md`, y las tablas de `ContentGear.cs` (mejoras, Trueque, sucesos);
- UI: `.dev-snapshots/ui/unity/runs/LEEME.md`, `pasada/` y `u1/`;
- los nombres y textos de Lore: `.dev-snapshots/lore/runs/NOMBRES.md`, `SUCESOS.md` y `TEXTOS-GUARDADO.md`;
- Red: el ambiente de interior (R5, `docs/UNITY-SONIDO.md`) y la partida apartada (`SaveStore.OutdatedLabel`);
- Arte: `.dev-snapshots/arte/unity/runs/`.

Lo que no va en las notas:
- **Lo de dentro:** los bots y sus medidas (R1–R3, G3), `Runs.Tuning`, las muestras y los laboratorios.
- **Lo que viene después:** los capítulos 2 y 3 de verdad, el manicomio, la historia, quitar el código de la campaña de antes e iOS.
- **Lo de iOS que ya va en el código:** no hace nada en Mac ni en Windows (G4).

Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Va para Mac y Windows. Lo que va entre [corchetes] lo completa Producto al publicar: el commit. El aviso de Windows sin probar se quita si R4 la prueba en un PC. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md). Como en la 0.11.0, la partida guardada va primero, porque es lo primero que se encuentra.

---

**Título:** Bastión 0.13.0: las expediciones

Versión 0.13.0 (commit [commit]). Es el cambio más grande hasta ahora: ya no hay campaña ni misiones. Se sale de la base en expediciones, etapa a etapa, hasta El Toro, el jefe del capítulo 1. Hay seis mapas nuevos: un edificio por dentro, o un sitio cerrado al aire libre. La partida de antes se aparta y se empieza de cero.

## Partidas guardadas: se empieza de cero

- **La campaña de la 0.12.26 no sigue en la 0.13.0.** No se borra: queda guardada aparte, en `save.antes-de-las-runs.json`, junto a la partida guardada, en el ordenador que la aloja. Una misión a medias va en el mismo archivo.
- **En el menú principal,** donde estaba «Continuar partida», sale un aviso que lo dice.
- **«Nueva partida» empieza la de las expediciones:** 4 soldados (2 por jugador, o los 4 si juega uno solo), cada uno con un cuchillo, el almacén vacío y 0 §.
- **Los soldados son nuevos:** los de antes, con sus barras y su tablero, se quedan en la campaña apartada.

## Novedades de la 0.13.0

### Las expediciones

- **Antes:** la campaña, con el Mapa del valle, los días, las misiones y la base que se construía.
- **Ahora:** expediciones. Sale la escuadra y avanza por las etapas del capítulo hasta su jefe.
  - **Tras cada etapa se puede volver a la base.** La expedición acaba y todos se curan. Lo que llevan los soldados se queda con ellos.
  - **La siguiente expedición** empieza el capítulo otra vez, con otro camino.
  - **Quien cae en un combate muere.** Lo que llevaba queda en el suelo, y otro soldado puede recogerlo.
  - **Si cae toda la escuadra,** la expedición acaba y se pierde todo lo que llevaban. Lo del almacén no se toca.
  - **Quien se quede sin soldados** recibe un recluta con un cuchillo.
- **Los soldados conservan** sus barras y su tablero de una expedición a otra, como hasta ahora.
- **La salud** solo baja durante la expedición. Se recupera con medicinas, en el Refugio y al volver a la base.
- **No hay derrota final:** la base sigue siempre.
- **A dos jugadores, todo es común:** una base, un almacén y los mismos suministros.
  - **Se votan:** salir, el camino, volver a la base, las mejoras y las respuestas en los Sucesos y al Superviviente. Los propone uno y los acepta el otro.
  - **No se votan:** equipar, mover y tirar cosas.

### La base

- **Antes:** construir y andar por el Asentamiento, la Armería, el Mapa del valle y los contadores de suministros, raciones, medicinas y piezas.
- **Ahora:** el claro en 3D de fondo, y cuatro pestañas: Almacén, Soldados, Mejoras y Salir.
  - **Almacén:** una cuadrícula de 10×6, común a los dos jugadores, con lo que se ha traído. Desde aquí se equipa a cada soldado.
    - Arrastrar mueve; mientras se arrastra, R o el clic derecho gira; con Mayús se mueve la mitad de la pila.
    - Lo que no se quiere, se tira con «Tirar».
  - **Soldados:** las barras, el tablero y Personalizar, como hasta ahora. Ya no se recluta aquí: se encuentra gente en las etapas.
  - **Mejoras:** se mejora la base con los § del almacén.
  - **Salir:** quién va en la expedición. Al principio, 4 plazas; con dos jugadores, la mitad para cada uno.
- **Los suministros (§) son el único recurso.** Se encuentran registrando y se guardan en el almacén. Con ellos se mejora la base y se cambia en el Trueque. Ya no hay convoy.
- **Al volver, los § siguen en las mochilas:** para usarlos en las mejoras, hay que pasarlos al almacén. Lo que se deja en una mochila sale con el soldado en la siguiente expedición, y se pierde si cae.

| Mejora | Qué hace | Niveles y lo que cuestan |
|---|---|---|
| Más plazas | Salen 6 soldados en vez de 4, y luego 8 | 150 §, 300 § |
| Estanterías | El almacén tiene 2 filas más | 80 §, 160 §, 240 § |
| Mochilas grandes | Cada mochila tiene una fila más | 100 §, 200 § |
| Curas en el camino | Tras cada etapa, todos recuperan 2 de salud | 120 § |
| Mapas de carreteras | Se ve una etapa más del camino | 80 §, 160 § |
| Buen trato | En el Trueque, todo cuesta un 20 % menos | 100 § |

### El mapa de etapas

- **Cada capítulo tiene 7 filas:**
  - de la 1 a la 5, etapas a elegir;
  - en la 6, un Refugio, para todos los caminos;
  - en la 7, el jefe.
- **Cada etapa lleva a una o dos de la fila siguiente.** El camino hecho va en blanco, y las que se pueden elegir, en verde.
- **Se ven las dos filas siguientes,** y una más con cada nivel de Mapas de carreteras. Lo de más allá sale a oscuras, también el jefe, que aparece como «?» hasta que la escuadra se acerca.
- **La ficha de cada etapa** dice qué se hace, el sitio, la dificultad y su modificador si lleva.
- **Cuanto más avanzada la fila,** más difíciles los combates y más suministros al registrar.
- **La primera fila es siempre un Saqueo.** Desde cualquiera de ellas se puede llegar a un Trueque.
- **Los modificadores** salen en uno de cada tres combates, más o menos, y se ven antes de elegir. Cada uno da un 50 % más de botín:
  - **De noche:** se ve menos y hay más zombis;
  - **Tormenta o Ventisca:** se ve menos y el ruido llega menos lejos;
  - **Horda despierta:** todos los zombis están alerta desde el principio, y entra una oleada al empezar.

### Las etapas de combate

- **El Saqueo** es la etapa normal: no tiene objetivo. Se registra lo que se quiera y se sale en La Lata. Entran zombis por los bordes, y antes cuanto más ruido se hace.
  - **La Lata espera hacia el centro del mapa,** a unas 30 casillas de donde empieza la escuadra. Antes estaba más lejos.
- **Las especiales tienen objetivo,** y al cumplirlo dan una recompensa, de 40 a 80 §, a las mochilas. Si se sale sin cumplirlo, la etapa cuenta, pero sin recompensa.
  - **Las de antes:** Limpieza, Recuperación (antes, Botín), Sabotaje, Arreglar la radio y Rescate. Quien se rescata se une a vosotros, con un cuchillo.
  - **Asedio:** aguantad hasta que llegue La Lata, en el turno 8. Antes no se puede salir. Llegan oleadas en los turnos 2, 4, 6 y 8, cada vez mayores y con especiales.
  - **Travesía:** la escuadra empieza en un extremo del mapa y La Lata espera en el otro. La horda entra por detrás.
  - **Sigilo:** recuperad la carga y volved a La Lata sin que os vean. Sin ser vistos, la recompensa es doble. Si os ven, llega enseguida una oleada grande y la recompensa baja a la mitad.
  - **Guarida:** un Acorazado o un Chillón, con dos zombis y un Corredor, guarda una caja dentro de un edificio. Esa caja es la recompensa: de 80 a 120 § y un arma buena.

### Las paradas

Son etapas sin combate.
- **Refugio:** todos se curan del todo. Hay uno fijo en la fila 6, antes del jefe.
- **Trueque:** alguien cambia 5 cosas por suministros, de 10 § (un cóctel molotov) a 120 § (un fusil de precisión). Los § salen de las mochilas de tus soldados, y lo cambiado va a la mochila de uno de ellos.
- **Suceso:** algo pasa por el camino, con dos opciones. Hay diez, y no se repiten hasta que han salido todos. Lo que hace cada opción se sabe antes de elegir: no hay azar. Algunos añaden un modificador al siguiente combate.
- **Superviviente:** alguien pide unirse, con un cuchillo. Si se une y no hay plaza en la escuadra, espera en la base.
- **Para seguir tras una parada,** con dos jugadores, lo piden los dos.

### El Toro, el jefe del capítulo 1

- **Un muerto enorme,** en una fábrica: 30 de salud y blindaje 1. Tiene dos acciones y anda algo menos que un zombi.
- **Mazazo:** 6 de daño, a quien tenga al lado.
- **Embestida,** cada 3 turnos: en línea recta, hasta 6 casillas, hace 4 de daño y tumba.
  - Quien cae se queda en el suelo hasta vuestro turno, con −20 de defensa y sin vigilancia. Levantarse le cuesta una acción.
- **Rugido:** al ver a la escuadra, ruge, y todos los muertos del mapa van hacia él.
- **No le vale el golpe silencioso.**
- **En pantalla:**
  - arriba, su barra con la salud, el blindaje y cuándo puede embestir;
  - con el puntero encima, una línea roja hasta cada soldado al que alcanzaría;
  - al mover a un soldado a una casilla de esa línea, lo avisa: «En la línea de la embestida»;
  - su ficha se abre con el clic derecho sobre él o con un clic en su barra.
- **Para ganar,** hay que matarlo y salir en La Lata. Da 150 § y un arma buena, el capítulo queda superado y la expedición vuelve sola a la base. Si se sale sin matarlo, la expedición acaba.
- **Tras El Toro se abre el capítulo 2,** que en esta versión es provisional: etapas como las del 1, algo más difíciles, y El Toro otra vez al final. El jefe del capítulo 2 serán los Cuervos, y el del 3, la horda.

### Los mapas nuevos

- **Uno de cada cinco combates, más o menos,** es uno de estos seis mapas.
  - Hospital, oficinas, comisaría y centro comercial salen en lugar de un pueblo o una ciudad; la nave, de un polígono o una fábrica; el camping, del campo.
  - Nunca son el mapa del Toro.
- **En el mapa de etapas,** cada uno lleva una marca. Su ficha, debajo de «Sitio», dice cómo es y qué sale más al registrar.
- **Los cuatro que son un edificio por dentro** (hospital, oficinas, nave y comisaría):
  - el mapa es el edificio, con la acera de la entrada;
  - se descubre sala a sala: al entrar en ella o al pararse justo delante de una de sus puertas o ventanas abiertas. A quien está en una sala sin descubrir no se le ve;
  - La Lata espera en la acera, junto a la entrada;
  - las oleadas entran por la acera o por las salas que dan a las paredes de fuera;
  - en ellos no hay Sabotaje ni Travesía;
  - suena un fondo propio: fluorescentes y edificio vacío.
- **Hospital:** tres plantas, con sus alas y su pasillo, y la fachada de hormigón. Sale más: medicinas y botiquines.
- **Oficinas:** dos o tres plantas, con la oficina abierta, despachos, salas de reuniones, cocina y aseos. Sale más: suministros y accesorios de arma.
- **Nave:** una sola planta grande, que se ve entera nada más entrar, con estanterías altas que tapan. Al lado, la oficina del capataz, la zona de carga y las taquillas. Sale más: cargadores, accesorios de arma y suministros.
- **Comisaría:** una o dos plantas, con la fachada de ladrillo rojo. Tiene vestíbulo, celdas, despachos, pruebas, interrogatorio, taquillas y una armería, con la mejor caja. Sale más: armas, cargadores y chalecos.
  - **Las celdas** dan a un pasillo. Las rejas dejan ver y disparar, pero no pasar.
  - **En la mitad de las celdas, más o menos, hay un muerto encerrado.** Está despierto y va a las rejas.
  - **A través de las rejas se pega cuerpo a cuerpo en los dos sentidos:** él muerde a quien está al otro lado, y desde el pasillo se le puede pegar.
  - **En un Sigilo,** un encerrado que os vea os descubre.
  - **Con la cámara lejos,** las rejas tapan bastante a quien está dentro.
- **Centro comercial,** al aire libre: nueve tiendas de una planta alrededor de una plaza con fuente. Una es un cine, del que solo está el vestíbulo. Sale más: suministros y cosas para el cinturón.
  - **Por los escaparates se ve dentro.** Se rompen con ruido al cruzarlos, como las ventanas.
  - **Siempre hay una farmacia,** que da medicinas.
  - **Los letreros y rótulos van en inglés,** como los de las tiendas de los pueblos.
  - **Sabotaje:** hay que apagar la sirena de un coche patrulla.
- **Camping,** al aire libre: un claro en el pinar. Tiene parcelas con tiendas de campaña, que dan cobertura, de 3 a 5 caravanas en las que se entra, aseos, la hoguera y una zona militar vallada. Sale de todo un poco.
  - **De noche, la hoguera alumbra como una farola:** a quien está junto a ella se le ve de lejos, sea soldado o muerto.
  - **Sabotaje:** hay que apagar la sirena de una ambulancia, en la zona de cuarentena.

### La mochila y el equipo

- **Antes:** tres huecos, cuatro con la Chapistería, y lo igual en el mismo hueco, sin límite.
- **Ahora:** una cuadrícula de 4×3, en la base y en combate, con una fila más con cada nivel de Mochilas grandes o con Todo cabe.
  - **Cada cosa ocupa su tamaño** y se puede girar: un fusil, 4 casillas en fila; una ametralladora, 4×2; un chaleco, 2×2; una pistola o un cuchillo, 2 en fila.
  - **Lo igual se apila:** los suministros, de 100 en 100; las medicinas, los cargadores y lo del cinturón, de 5 en 5; los accesorios de arma, de 3 en 3.
- **El equipo va aparte y no ocupa sitio en la mochila:** arma principal, arma cuerpo a cuerpo, pistola, chaleco y dos huecos de cinturón.
  - **Los chalecos son cosas que se encuentran:** el chaleco antibalas da +2 de salud, y el chaleco con placas, +4 y blindaje 1.
  - **En el cinturón** van las granadas (también las de humo y las aturdidoras), los cócteles molotov y los botiquines.
- **Accesorios de arma:** mira, culata, cargador ampliado y silenciador.
  - Cada arma de fuego principal lleva hasta 2.
  - Se ponen fuera del combate, en la base o entre etapas, y solo se quitan en la base.
- **Los cargadores** son cosas de la mochila: recargar gasta uno.
- **Las medicinas** curan 3 de salud: en combate, con una acción; entre etapas, cuando se quiera.
- **En combate,** la mochila se abre con su marca en la ficha del soldado:
  - **sin gastar acción:** soltar al suelo, coger lo del suelo de su casilla y ordenarla;
  - **con una acción:** dar algo a un compañero de al lado, usar una medicina (gratis con Manos de enfermero) y equipar, que cambia lo nuevo por lo que llevaba;
  - **lo del cinturón** no se quita en combate.
- **Al registrar,** lo encontrado va a la mochila de quien registra, y lo que no cabe queda en el suelo, a sus pies.
- **Las armas ya no se gastan.** Fuera el estado de las armas, los kits de reparación y reparar en la base.
- **La rama de Apaños del tablero pasa a Rebuscar:**
  - **Rebuscar** (antes, Apaños): registra otra vez la habitación en la que estás, aunque ya esté registrada. Una vez por combate;
  - **Hurgar** y **Olfato** (antes, Más apaños y Chapuzas): una vez más cada una;
  - **Ojo de urraca** (antes, Inventiva): dos veces más;
  - **Todo cabe** (antes, Mantenimiento): la mochila de ese soldado tiene una fila más.

### Lo que se ha quitado

- Construir, talar, las defensas y andar por la base, y los asedios a la base.
- Las raciones y el hambre, el Huerto, y las medicinas y las piezas como contadores.
- La Armería y el Taller: las armas solo se encuentran, se cambian en el Trueque o salen de las cajas y del Toro.
- La Marea y el Mapa del valle, con sus zonas, días, convoyes, enclaves, rumores y encargos.
- El desgaste de las armas.
- La historia, por ahora: volverá más adelante.

## El capítulo 1 sale duro, a propósito

- **Se ha dejado así para ajustarlo con lo que notéis al jugar.** No se ha tocado ningún número para hacerlo más fácil.
- **Lo que más cuesta es rehacerse tras perder a toda la escuadra:** solo llega un recluta con un cuchillo por jugador, y con cuchillos cuesta salir adelante.
- **Los suministros llegan despacio** para lo que cuestan las mejoras, y las armas de fuego hay que encontrarlas o conseguirlas en el Trueque.
- **Si algo os parece demasiado,** apuntadlo: es lo que se ajusta en la versión siguiente.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.13.0-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.13.0-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.13.0:** con la 0.12.26 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.26.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
