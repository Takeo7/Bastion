# Notas de la 0.14.4

Texto de Lore para la 0.14.4. Lo pidió Producto el 24/11, con el árbol congelado. Sin commit.

**Qué lleva:**
- el arranque sin la presentación de Unity y la interfaz que crece en pantallas grandes (cuadro 146);
- el silenciador y la mira en el arma (cuadro 145);
- dos saltos de M11: la casilla entera tras disparar asomado, y el golpe;
- los retoques de UI tras las pasadas de la 0.14.3, y «Sin cuchillo» (`Content.BareHanded`).

**Fuentes:**
- `.dev-snapshots/lore/ARBOL-CONGELADO.md` y `producto/RUNS.md`, cuadros 145 y 146;
- `producto/ENTREGA-tras-0143-UI.md` y `CIERRE-0.14.4-UI.md`;
- `producto/CIERRE-0.14.4-arte.md` y `MENSAJE-DE-ARTE-m11-saltos.md` (el bloque del 24/11 por la mañana);
- `gameplay/notas-24-11/A-MANO.md`.

**Lo que no va:**
- lo de dentro: los bots (el bot sin trampa y la mejora 1 van apagados; juega como en la 0.14.3), las opciones de medida de `AppBuild`, el paso «escribe» y `publicar.sh`;
- lo que solo cambia en el iPad y el iPhone.

**La palabra:** Producto la llama «a mano», pero en el juego dice «Sin cuchillo» (`HudText.Bare`; Lore, 24/11). Las notas dicen lo que se lee.

**Sin probar, para Producto:**
- **Los accesorios en el arma** no se han visto en la app ni en una partida entera (cierre de Arte). La mira de tubo lleva un cristal con un material transparente nuevo.
- **La interfaz que crece:** probada en el editor como una pantalla de densidad 1. La Retina y el 4K, solo por la cuenta.
- **«La partida de la 0.14.3 sigue en la 0.14.4»:** el motor solo añade `Content.BareHanded` y cambia el bot, así que el formato de la partida no cambia. Sin probar con un guardado real.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.4: abre antes y los accesorios en el arma

Versión 0.14.4 (commit [commit]). El juego abre antes, la interfaz crece en pantallas grandes, el silenciador y la mira se ven en el arma, dos saltos de animación menos y retoques de las partidas de la 0.14.3.

## Lo que se arregla

- **Al disparar asomado desde una cobertura,** el soldado ya no aparece de golpe en su casilla: vuelve andando.
- **Al dar un golpe cuerpo a cuerpo,** el soldado ya no salta un instante al centro de su casilla.
- **Las ayudas que se quedaban en pantalla** al cerrar o esconder su panel ya se van.
- **En el turno de los muertos,** ninguno atraviesa a otro que espera en su camino.
- **La hoja del soldado en combate (H)** decía «Mochila: Vacía» en las expediciones aunque la mochila estuviera llena. Ahora dice lo que lleva.

## Partidas guardadas

- **La partida de la 0.14.3 sigue en la 0.14.4.**

## Novedades de la 0.14.4

### El juego abre antes

- **Ya no sale la presentación de Unity:** el menú llega unos 2 segundos antes.
- **Al abrir sale «BASTIÓN» sobre negro,** en el mismo sitio y del mismo tamaño que en el menú, y el menú aparece debajo: se ve un solo título. Lo mismo al volver al menú desde una partida.

### La interfaz crece en pantallas grandes

- **Con la ventana de más de 900 de alto,** la interfaz crece con ella, a la mitad de lo que crece la ventana:
  - en un monitor de 1920 × 1080, un 10 % más grande que antes;
  - en uno de 2560 × 1440, un 30 % más.
- **Hasta 900 de alto,** como antes.
- **En una pantalla Retina,** cuenta el alto de la ventana en puntos, no en píxeles: crece solo si la ventana es alta, no por tener más píxeles.
- **El ajuste «Interfaz»** de Ajustes sigue encima: la agranda o la achica igual que antes.

### Los accesorios, en el arma

- **El silenciador** se ve en la boca del cañón de las armas de fuego. En la pistola, solo si es el arma principal. La ballesta y el arco no lo llevan.
- **La mira:**
  - de tubo, en los fusiles de asalto y la ametralladora ligera;
  - de punto rojo, en el subfusil y las escopetas;
  - las armas que ya traen mira (los fusiles de caza, los de precisión y la ametralladora grande) se quedan con la suya. La pistola, sin mira.
- **La culata y el cargador ampliado,** solo con su marca en el icono, como en la 0.14.3.
- **Se ve en el combate y en la hoja del soldado.** Desde la cámara del combate, el silenciador se distingue; la mira, apenas.

### Retoques

- **En el Trueque,** al cambiar algo que lleva puesto, «Cambiar» ya no se mueve. La pregunta y «Cancelar» salen en una línea debajo.
- **La bienvenida de la base** sale solo al empezar: no si ya se ha jugado algo de las expediciones, ni si La Lata ya no es la moto. Sale arriba y deja ver a los soldados.
- **El que se queda fuera de La Lata:**
  - en el parte, con su retrato, el mismo aspecto que tenía en el combate;
  - en la carretera, su sitio queda vacío, sin la cruz de los caídos.
- **La tarjeta del final, «Botín a salvo»,** cuenta también lo que deja el superviviente que no se queda, y acaba con «y la recompensa».
- **Sin soldados que mandar,** por ejemplo cuando el tuyo cae en el turno enemigo:
  - «→|» se queda, con su estado («A punto», «Turno enemigo»), hasta que sale la tarjeta del final;
  - el soldado que aún se ve en pie conserva su ficha y sus teclas, apagadas, hasta que se ve su caída.
- **«Sin cuchillo»:** si el soldado no lleva arma cuerpo a cuerpo puesta, porque pasó el cuchillo a la mochila, lo dejó en el suelo o salió sin él:
  - la ficha del combate dice «Sin cuchillo · CaC 55», y también la hoja H;
  - el cuchillo de al lado sale atenuado;
  - la ayuda dice «No lleva arma cuerpo a cuerpo puesta. Golpea igual que con el cuchillo.»;
  - el golpe no cambia.
- **Mayús + clic en combate,** con la mochila abierta, lo explica una línea bajo la mochila: «Arrastra para mover. Mayús + clic: lo del suelo y lo puesto, a la mochila; lo de la mochila, al suelo.». La lista de controles dice lo mismo.

## Lo que sigue sin arreglar

- **Un soldado o un cuerpo** puede quedar tapado por un edificio sin descubrir.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **Quedan saltos de animación:**
  - un muerto que embiste salta un instante al centro de su casilla y vuelve;
  - un soldado se levanta de golpe, o cae al suelo de golpe al acabar de trepar;
  - algunos muertos suben el brazo o bajan el cuerpo de golpe.

  Si veis alguno, apuntad dónde y cuándo.
- **La dificultad** se ajusta jugando: apuntad lo que notéis en cada nivel.
- **La sincronía vertical con la pantalla dormida,** o apagada al momento con una esquina activa, está sin probar.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.14.4-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.4-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.4:** con la 0.14.3 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.14.3.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
