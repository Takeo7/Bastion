# Notas de la 0.14.0

Texto de Lore para la 0.14.0. Lo pidió Producto el 23/11 para cerrar la versión. Sin commit.

**Qué lleva:**
- las nueve notas de Arturo del 22/11 (RUNS.md, cuadros 113 a 127);
- el arco y la ballesta pulidos;
- lo de la revisión de la interfaz que entra.

**Fuentes:**
- el plan de Producto, `.dev-snapshots/producto/notas-2026-11-22/PLAN.md`;
- los contratos de Gameplay, en `.dev-snapshots/gameplay/notas-22-11/`;
- los cierres de Gameplay (`producto/MENSAJE-DE-GAMEPLAY-cierre-0140.md` y `-revision-0140.md`), de Arte (`producto/MENSAJE-DE-ARTE-cierre-0.14.0.md`) y de UI (`ui/unity/runs/CIERRE-0.14.0.md`);
- los textos de Lore, en `.dev-snapshots/lore/NOTAS-22-11-TEXTOS.md`.

**Lo que no va:**
- lo de dentro: pruebas, bots, bancos y laboratorios;
- Evacuación, Las cargas, el mapa de etapas nuevo y la sala nueva de dos jugadores;
- lo que solo cambia en el iPad y el iPhone.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.0: dificultad, saltos y partida perdida

Versión 0.14.0 (commit [commit]). Las nueve notas de Arturo del 22/11, el arco y la ballesta pulidos y retoques de la interfaz.

## Lo que cambia la partida

- **La dificultad se elige al crear la partida.**
  - Cuatro tarjetas: Fácil, Medio, Difícil y Supervivencia. Sale marcada Medio.
  - Después no se cambia. Se ve en el menú, bajo «Continuar partida» («Capítulo 1 · Medio»).
  - Las partidas de antes quedan en Difícil, que es como se jugaba hasta ahora.
- **Sin ningún soldado vivo, ni en la base, se pierde la partida entera.**
  - Se pierden el almacén, las mejoras y el capítulo, y hay que empezar otra.
  - Ya no llega un recluta de regalo.
- **Ya no se puede abandonar un combate de expedición.** Solo cuando no hay forma de llegar a La Lata.
- **La Lata sale también en la Limpieza.** Irse en ella antes de acabar es retirarse: la etapa queda sin la recompensa, pero vuelven todos los de a bordo y la expedición sigue.

## Partidas guardadas

- **La partida de la 0.13.8 sigue en la 0.14.0,** en Difícil. No se empieza de cero.
- **Una expedición a medias** sigue donde estaba.
- **Un combate guardado a medias con la 0.13.8** puede no tener La Lata (una Limpieza). En ese caso, el menú deja abandonarlo sin perder a nadie.

## Novedades de la 0.14.0

### La dificultad

- **Lo que cambia** es cuántos muertos hay, la horda y los suministros, tanto los que se encuentran como los de las recompensas. No cambian la salud, el daño ni el equipo del principio.

| | Fácil | Medio | Difícil | Supervivencia |
|---|---|---|---|---|
| Muertos en el mapa | −40 % | −20 % | como hasta ahora | +25 % |
| Horda (los que llegan y lo que tarda una oleada) | la mitad | −25 % | como hasta ahora | +30 % |
| Suministros | +50 % | +25 % | como hasta ahora | −25 % |

- **Supervivencia, además:** las heridas no se curan al volver a la base. Se curan en un Refugio, con medicinas o con «Curas en el camino». En la base se ve con cuánta salud sale cada herido.
- **A dos,** la elige quien aloja la partida.
- **Los números** se ajustarán jugando.
- **«Dificultad» es solo esto.** La de cada etapa se llama ahora **«Peligro»** («Peligro 2.» en la ficha de la etapa). Sube con la fila y con el capítulo.

### Sin soldados

- **La partida se pierde** cuando la escuadra vuelve a la base, o cae entera, y no queda ningún soldado vivo en ningún sitio. Sale la ventana «Partida perdida».
- **Para empezar otra,** «Nueva partida» enseña las tarjetas de dificultad con la última marcada.
- **Con soldados en la base,** se sigue con ellos.
- **A dos, si uno se queda sin soldados y el otro tiene,** no pierde nadie:
  - el que tiene puede llenar todas las plazas;
  - al partir, el que no tiene manda en la mitad de la escuadra, y la base se lo dice;
  - un superviviente rescatado se une al que tiene menos soldados.

### Sin abandonar

- **«Abandonar la misión»** ya no sale en el menú de un combate de expedición.
- **Solo sale cuando no hay forma de llegar a La Lata:** un combate sin ella, guardado con la 0.13.8, o un soldado sin camino hasta ella. Debajo dice por qué. Al abandonar no se pierde a nadie: todos siguen la expedición con lo que llevan, y la etapa queda sin la recompensa.
- **Desde cualquier sitio al que llegue un soldado,** hay camino a La Lata. Si un mapa sale sin camino, se hace otro.
- **En la Escaramuza** se sigue pudiendo abandonar.

### Saltar por una ventana

- **Bajar de una planta por una ventana,** sin escalera, es saltar. Se salta desde las plantas 1 y 2. Las azoteas tienen parapeto: de ellas se baja por la escalera.
  - Quita 1 de salud por planta, aunque lleve blindaje, y puede matar.
  - No hace ruido.
- **El soldado baja por la escalera si llega,** aunque saltar sea más corto.
  - Si solo llega saltando, se avisa antes («−2 de salud», en rojo si muere) y hay que hacer clic otra vez.
- **Cargando a alguien** no se salta.
- **Los muertos también saltan** y se hacen daño. Los vivos bajan por la escalera.
- **El salto se ve:** el soldado se encoge, cae y aterriza con polvo.

### Granadas y molotov, a cualquier planta

- **La molotov** se lanza a cualquier planta, como la granada, y arde solo en la planta donde cae: no baja.
- **Basta con ver la casilla:** por una ventana, una puerta, un hueco o por encima del borde de una azotea.
- **El estallido** no atraviesa suelos ni techos.
- **Para elegir la planta:**
  - un selector fijo abajo a la derecha («▲ Planta 1 ▼»);
  - al apuntar algo que se lanza, una línea con la planta y la tecla para cambiarla: Fn + ↑ y Fn + ↓ en el Mac, RePág y AvPág en Windows.
- **La botella y la granada** se ven volar, con su brillo.

### Muertos contra vivos

- **Los muertos dormidos que ven a un Cuervo** se despiertan y van a por él.
- **Los vivos que aún no saben de la escuadra** disparan al muerto más cercano que ven, a 8 casillas o menos, una vez por turno. Lo hacen desde donde están.
- **Un guardia del campamento atacado por los muertos** se defiende en su puesto. El campamento solo lo despierta la escuadra.
- **Los tiros de los vivos** atraen a la horda en todas las etapas, no solo en Saqueadores. Los oyen los muertos; los otros vivos no salen a mirar.
- **El turno enemigo** enseña también esos ataques.
- **Lo que no cambia:**
  - un guardia que dispara a un muerto no os descubre: después vuelve a mirar hacia donde miraba;
  - unos vivos despertados por los muertos no saben dónde está la escuadra oculta.

### El Rescate

- **El superviviente** ya no sale en una puerta, una ventana ni un paso, y se queda en su edificio.
- **Los muertos** quedan apartados de él y del camino de la escuadra hasta él y hasta La Lata.
- **También en Mordido y en El herido.**
- **Con los bots,** la escuadra cae entera en el Rescate menos de la mitad de las veces que antes.

### La cámara, antes que el soldado

- **Al tocarle a un soldado,** su área de andar y sus botones no se activan hasta que la cámara llega a él.
- **Un clic en su busto** lleva la cámara al soldado solo si no se ve. Si ya se ve, lo elige sin moverla.

### El tablero de habilidades

- **Al pasar por una habilidad,** al pie de su ficha sale lo que cambia de los totales del soldado, en verde («Esquiva 10 → 15»).
- **También salen sus totales:** salud, acciones, movimiento, puntería, esquiva, voluntad, y lo demás cuando tiene algo.
- **«Puntería al disparar» y «Crítico al disparar»** sustituyen a «con armas de fuego», en todo el juego, porque valen también para el arco y la ballesta.

### La imagen partida

- **La sincronía vertical** va encendida: la imagen ya no se parte en horizontal al mover la cámara.
- **Se apaga en Ajustes,** con «Sincronía vertical».

### El arco y la ballesta, pulidos

- **La flecha se ve volar,** también de noche, sin fogonazo, y el golpe espera a que llegue.
- **La ballesta** tiene su forma de apuntar y de llevarse a la espalda. La recarga se hace con el cuerpo, y el rótulo lo dice.
- **Quien lleva arco** lleva su carcaj.

### En pantalla

- **La barra de acciones** enseña la tecla de cada botón, en su esquina.
- **Los bustos del compañero** se ven encendidos, con el borde de su color. Apagado, solo quien ha caído, con «CAÍDO».
- **Las etiquetas de los enemigos** ya no pisan la barra ni los paneles.
- **El glosario** tiene «Dificultad», «Peligro» y «Saltar». La ayuda de «Moverse» explica el salto.
- **Si el invitado sale en mitad de una misión,** el aviso ya dice lo que pasa: el que se queda manda en sus soldados hasta que vuelva. Antes decía que se quedaban quietos.

## Lo que sigue sin arreglar

- **El fuego en una planta alta** no deja hollín: las llamas se ven, pero el suelo quemado no.
- **Un soldado o un cuerpo** puede quedar tapado por un edificio sin descubrir.
- **La sincronía vertical con la pantalla dormida,** o apagada al momento con una esquina activa, está sin probar.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **La dificultad con 2 plazas** se sigue midiendo en cada nivel. Apuntad lo que notéis al jugar.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.14.0-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.0-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.0:** con la 0.13.8 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.13.8.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
