# Notas de la 0.14.6

Texto de Lore para la 0.14.6. Lo pidió Producto el 25/11 (`lore/MENSAJE-DE-PRODUCTO-0.14.6.md`). Corto. Sin commit.

**Qué lleva:** las cinco notas de Arturo jugando la 0.14.5 (O1 a O5; cuadros 152 y 153), la rejilla del suelo (O6, cuadro 154), la ficha del arma con lo que hace el soldado (O7) y «QUEDAN N» en la Limpieza (O8).

**La rejilla, escrita con el encargo** (`producto/notas-2026-11-25/REJILLA.md`), mientras Arte y UI la hacen. Si cambia algo, lo ajusta Producto. Sobre todo: qué hace donde no se ve, y el nombre de la opción en Ajustes.

**Fuentes:**
- `producto/notas-2026-11-25/PLAN.md` y `producto/RUNS.md`, cuadros 152 y 153;
- `producto/ENTREGA-notas-25-11-UI.md`;
- `producto/ENTREGA-notas-25-11-gameplay.md`;
- `producto/notas-2026-11-25/REJILLA.md`;
- para O7 y O8, el final de `producto/ENTREGA-notas-25-11-UI.md` y sus fotos `ui/unity/runs/notas-25-11/fotos/o7-*` y `o8-*`. Las palabras de esas fotos están bien. La ficha en el Trueque y en el mapa de etapas está sin probar: es el mismo código que el Almacén.

**Lo que no va:** el paso de prueba `tecla=`, y lo que solo cambia en el iPad y el iPhone.

**El texto del molotov:** el de Gameplay vale tal cual. No hace falta tocar `Content.cs`.

**«Lo que sigue sin arreglar»:** el de la 0.14.5 publicado, con la línea del teclado del menú, que Producto añadió. Si la pasada lo prueba, la quita Producto.

**Sin probar, para Producto:**
- nada en la app: ni el fuego ni la puerta (Gameplay); ni el mapa, la hoja o los cuerpos (UI);
- la hoja de 5 s con dos apps;
- los cuerpos con el dedo;
- un guardado real de la 0.14.5. El motor cambia dos reglas (`Rules.BurnStanding`, `Los.PeekOrigins`) y un texto, no el formato.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.6: el equipo a la vista y el fuego al empezar el turno

Versión 0.14.6 (commit [commit]). Las notas de Arturo jugando la 0.14.5: una rejilla para ver las casillas, la ficha del arma con lo que hace tu soldado, y más.

## Lo que se arregla

- **Desde dentro de una casa, junto a una puerta cerrada y a un mueble alto,** ya no se ve la calle ni se dispara a través de la puerta. Tampoco al revés.
  - Pasaba al asomarse desde la cobertura: el soldado miraba desde la casilla de la puerta cerrada.
  - Por las ventanas, como antes: solo pegado al cristal.
- **La ficha del cuchillo** decía «3–4» de daño. Hace 2–4, como siempre.

## Partidas guardadas

- **La partida de la 0.14.5 sigue en la 0.14.6.**

## Novedades de la 0.14.6

### El equipo, a la vista en el mapa de etapas

- **Ya no hay botón «Equipo ▸»:** el equipo del soldado elegido sale siempre:
  - lo que lleva puesto, el cinturón y la mochila;
  - «Tablero de habilidades», con sus puntos por gastar debajo.
- **Así se pueden gastar los puntos** desde la primera etapa, sin buscar nada.
- **La ficha de la etapa,** junto al mapa y a la vez: «Elige una etapa», o la etapa bajo el puntero con «Ir a esta etapa». El mapa queda algo más estrecho.
- **En el Trueque y en el Refugio,** la ficha se va, como antes, y el equipo se queda.

### La rejilla del suelo

- **En combate, la G pone y quita una rejilla** que marca las casillas en el suelo.
- **Al empezar, está apagada.** Lo que elijas se recuerda para las partidas siguientes, en tu ordenador.
- **Con el dedo,** o si se prefiere sin tecla: «Rejilla del suelo», en Ajustes.
- **Señalar un sitio para el compañero** pasa de la G a la P.

### La ficha de un arma, con lo que hace tu soldado

- **El daño es el que haría el soldado elegido con ella:** «Daño: 3–5», y debajo, en pequeño, de dónde sale: «arma 2–4 · Golpe pesado +1».
- **La puntería y el crítico son el total del soldado con esa arma:** «Puntería: 70», y debajo «base 55 · arma +15». En las armas de fuego, sin contar distancia ni cobertura.
- **La comparación,** en verde y rojo, es entre lo que haría ese soldado con un arma y con la otra.
- **Dónde:**
  - en el Almacén, el equipo del mapa de etapas, el Trueque y la mochila del combate;
  - lo que lleva puesto, con sus cifras y sin comparar, también en la hoja de Soldados.

### Cuántos quedan en la Limpieza

- **La tarjeta del objetivo dice cuántos quedan:** «QUEDAN 7».
- **Cuenta todos,** se vean o no.
- **Vale** en la Escaramuza y en las etapas de Limpieza de las expediciones.

### La hoja de la etapa dura 5 segundos

- **La hoja que sale al ir a una etapa,** la que dice qué hay que hacer, se queda al menos 5 segundos. Antes, unos 3,5.
- **Si la carga tarda más,** se queda hasta que esté.
- **Mientras está,** el reloj del turno no corre.

### Los cuerpos, solo con «Registrar»

- **Pinchar la casilla de un cuerpo mueve al soldado,** como cualquier otra. Al pasar el puntero se ve el camino, no la mochila del cuerpo.
- **Un cuerpo se registra con «Registrar»** o su tecla.
  - Con un cuerpo al lado, lo registra al momento.
  - Con dos o más, los marca: se elige uno con un clic, o con Tab y Mayús + Tab.
- **Lo de un compañero caído** se coge desde la mochila.
- **Cajas, muebles, maleteros, puertas y tablas,** como antes: un clic a su lado los abre.

### El fuego quema al empezar el turno

- **Quema al pisarlo y al empezar el turno encima,** a todos: soldados, muertos y vivos. Al acabar el turno, ya no.
- **Quien empieza el turno en el fuego y cae,** cae antes de actuar.
- **En su último turno,** el fuego se apaga antes de que empiece el de la escuadra: quema a los muertos que empiezan su turno en él, pero ya no a los soldados.
- **El molotov lo dice:** «Quita 1 de salud a quien esté dentro al caer, a quien entre y a quien empiece el turno dentro, también a los tuyos.».

## Lo que sigue sin arreglar

- **El teclado en el menú nuevo** (flechas, Intro y Esc) está sin probar en la app. Si Intro abre algo que no está marcado, o envía un formulario nada más abrirlo, apuntadlo.
- **Un soldado o un cuerpo** puede quedar tapado por un edificio sin descubrir.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **Quedan saltos de animación:**
  - un soldado se levanta de golpe;
  - algunos muertos suben el brazo o bajan el cuerpo de golpe.

  Si veis alguno, apuntad dónde y cuándo.
- **La dificultad** se ajusta jugando: apuntad lo que notéis en cada nivel.
- **La sincronía vertical con la pantalla dormida,** o apagada al momento con una esquina activa, está sin probar.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.14.6-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.6-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.6:** con la 0.14.5 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.14.5.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
