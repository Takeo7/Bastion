# Notas de la 0.13.2

Texto de Lore para la 0.13.2, que lleva las notas de Arturo del 18/11 tras la 0.13.1 (commits locales dd023be, f4deafd y 0d4a09d). Fuentes:
- lo decidido por Arturo: `.dev-snapshots/producto/RUNS.md`, desde el cuadro 51 hasta el 63, y lo que pidió por mensaje entre medias;
- la revisión de Producto: `.dev-snapshots/producto/notas-2026-11-18/REVISION.md`, pasadas 20 a 36;
- los mensajes de los dos commits.

Lo que no va en las notas:
- **Lo de dentro:** las pruebas, los laboratorios y las medidas de fotograma.
- **Lo no probado:** el rendimiento y el iPhone y el iPad, salvo el botón de la mochila.

Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Va para Mac y Windows. Lo que va entre [corchetes] lo completa Producto al publicar: el commit. El aviso de Windows sin probar se quita si alguien la prueba en un PC. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.13.2: el tablero entre etapas

Versión 0.13.2 (commit [commit]). El tablero de habilidades se abre ya entre etapas, sin volver a la base. Con Mayús se ve todo lo que queda por registrar, y lo que llevaba un compañero caído se recoge desde su cuerpo. Las armas de cuerpo a cuerpo se llevan en el puño, y las casas en ruinas y las colinas del bosque cambian.

## Partidas guardadas

- **La partida de la 0.13.1 sigue en la 0.13.2.** No se empieza de cero.
- **Si se guardó a mitad de un combate,** sus casas en ruinas cuentan como casas hasta que acaba ese combate. Desde el siguiente, ya no.

## Novedades de la 0.13.2

### El tablero de habilidades, entre etapas

- **Antes:** el tablero de habilidades solo se abría en la base.
- **Ahora:** también entre etapas, en el mapa de la expedición y en las paradas.
  - **Bajo el nombre del soldado elegido,** en la columna de la escuadra, el botón «TABLERO DE HABILIDADES» lo abre. Debajo dice cuántos puntos quedan, por ejemplo «2 puntos por gastar», y el botón late si puede aprender algo.
  - **En la escuadra,** una «▲» verde en el busto marca a cada soldado tuyo que puede aprender algo.
  - **En el parte de la etapa,** el botón «▲ 2 PUNTOS POR GASTAR» abre el tablero de ese soldado.
  - **Los soldados del compañero** se pueden mirar, pero no se aprende por ellos.
- **En combate no se aprende:** «Se aprende entre etapas, no en combate.»

### Mayús: todo lo que queda por registrar

- **Mientras mantienes Mayús en combate,** se marca todo lo que queda por registrar y ya ha visto la escuadra: cuerpos, maleteros, cajas y muebles.
  - **En claro,** lo que el soldado elegido puede registrar desde donde está. **En gris,** el resto.
  - **No se marca** lo que nadie ha visto, ni lo que está dentro de una casa sin descubrir. Las puertas y las tablas tampoco.
  - **En el mapa (M),** las marcas tienen un tamaño mínimo, para que se vean desde arriba.
- **Mayús sigue haciendo lo de antes:** Mayús + clic señala, y Mayús + rueda cambia de planta.
- **En Controles,** en el menú de pausa, la fila nueva «Mantener Mayús».
- **Con el dedo,** un botón con una mochila, junto a los de planta, hace lo mismo: un toque lo enciende y otro lo apaga.

### Los cuerpos, solo de cerca

- **Antes:** un cuerpo se marcaba para registrarlo aunque estuviera lejos, y al pasar el ratón por su casilla no salía la línea de andar.
- **Ahora:** un cuerpo se marca, con su contorno y la mochila encima, solo si el soldado elegido ya está a su lado y puede registrarlo.
  - **En cualquier otro caso,** su casilla es como las demás: sale la línea de andar y el clic mueve. Esto vale también al lado de un cuerpo si al soldado no le quedan acciones.
  - **Mientras el soldado anda hacia el cuerpo,** no hay marca. Sale cuando llega.
- **Los maleteros y las cajas,** como antes.

### El compañero caído

- **Antes:** lo que llevaba un soldado caído se quedaba en su casilla, sin marca, y solo se cogía pisándola.
- **Ahora:** se recoge desde su cuerpo.
  - **Al caer,** el registro lo dice: «… cae. Lo que llevaba se queda en su cuerpo y se puede registrar.»
  - **Con un soldado a su lado,** el cuerpo se marca con su contorno y la mochila encima. Con Gancho, desde 2 casillas.
  - **Un clic abre la Mochila,** con lo que llevaba en «EN EL SUELO»: armas, chaleco, cinturón y mochila.
  - **Cogerlo no gasta acción.**
  - **Cuando ya no queda nada,** el cuerpo deja de marcarse.

### Las armas de cuerpo a cuerpo, en el puño

- **Antes:** colgaban de la mano abierta y apuntaban al suelo. El cuchillo salía por debajo del puño, como si fuera parte del brazo.
- **Ahora:** el mango cruza la palma, el puño está cerrado y el arma apunta hacia delante y hacia abajo. Así en la base y en combate.
  - **El cuchillo,** con la hoja hacia delante.
  - **Agachado,** el arma va por fuera del muslo.
  - **La katana y la espada larga,** en reposo, con la mano junto a la guarda.

### Las casas en ruinas

- **Antes:** una casa en ruinas contaba como una casa sin descubrir. No se veía a quien estaba dentro hasta descubrirla, y el área de andar no entraba.
- **Ahora:** dentro de una ruina no hay nada que descubrir. Se ve y se pisa como el campo.
  - **Se ve a quien está dentro** si hay línea de visión, desde el primer turno.
  - **El área de andar entra** en ella.
  - **Los muros que siguen en pie** tapan la vista y dan cobertura, como antes.
- **Se dibuja caída:**
  - cada tramo de muro está cortado a una altura distinta, con la madera a la vista en el corte;
  - dentro hay vigas, tablones y un trozo del tejado;
  - el color es más sucio.
- **Un muro que tapa se dibuja alto,** más que un soldado de pie.

### Las colinas del bosque

- **Antes:** la cima no se dibujaba, y la colina se veía como un cercado de roca hueco. Quien subía parecía flotar sobre un hoyo.
- **Ahora:** es un peñasco redondo de roca, macizo, con la cima de hierba.
  - **La escalera de mano** va apoyada contra la roca.
  - **Las rocas y los pinos de la cima** están arriba. Antes salían al pie.
- **No cambian las reglas:** las casillas, la altura y la cobertura son las mismas.

## Lo que sigue sin arreglar

- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle. Queda para otra versión.
- **La dificultad con 2 plazas** aún no se ha ajustado. Apuntad lo que notéis al jugar.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.13.2-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.13.2-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.13.2:** con la 0.13.1 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.13.1.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
