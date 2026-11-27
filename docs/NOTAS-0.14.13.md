# Notas de la 0.14.13

Texto de Lore para la 0.14.13. Lo pidió Producto el 27/11 (`lore/MENSAJE-DE-PRODUCTO-0.14.13.md`). Corto. Sin commit.

**Qué lleva:**
- el combate, repasado (cuadro 177): menos letra y las piezas más iguales entre sí;
- la ficha del soldado elegido, dentro de su tarjeta (cuadros 178 y 179);
- la hoja del soldado, nueva (cuadros 180 y 181);
- unirse en el iPhone y el iPad: el aviso ya dice la causa, como en el Mac desde la 0.14.12.

**Fuentes:**
- `producto/ENTREGA-0.14.13-UI.md`, `ENTREGA-0.14.13-ficha-UI.md` y `ENTREGA-0.14.13-hoja-UI.md`, con las fotos de `ui/unity/runs/01413-ficha/` y `01413-hoja/`;
- `red/parches/unirse-ios-errno.patch` y `Client/Net/NetLink.cs` (`NotReached`, con `Lore.JoinText`);
- `producto/RUNS.md`, cuadros 176 a 181;
- `lore/MENSAJE-DE-PRODUCTO-0.14.13-b.md`.

**Lo de Lore:**
- Nada nuevo en el motor.
- Los textos que dejan de verse en el combate siguen donde estaban: los rangos en `Bars.cs`, la frase del Saqueo en `Lore/Scavenge.cs` y «PUNT.», «CaC», «Con acciones» y «RUIDO» en `HudText.cs`. Ninguno tiene que quedarse.
- **Me faltan líneas de «qué hace»** para nueve ayudas de la hoja: salud, acciones, voluntad, blindaje, daño, crítico, daño crítico, acciones por disparo y quita blindaje. Van en la siguiente, como pidió Producto.

**Sin probar, para Producto:**
- **Nada de lo del combate se ha visto en la app:** solo en el editor, a 1280×800 (lo verá Pruebas).
- **La ficha y la hoja, sin ver:** con un soldado del compañero elegido, con arco o ballesta, con un soldado escondido o con un veterano de tablero avanzado.
- **La barra, sin ver:** en la partida con reloj (la barra del reloj queda justo debajo), en el turno enemigo de verdad, con una novena tecla.
- **El aviso en el iPhone:** las pruebas de Red leen los números de Darwin. En el simulador, «rechazada» daba 61; los demás no se han visto en un aparato.
- **Para Gameplay, visto por UI:** la ficha de la pistola en el motor da cargador 0, así que su ayuda en el almacén dirá «No gasta». No va en las notas.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows; lo del iPhone y el iPad, para la app que Arturo instala desde Xcode. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.13: el combate, con menos que leer

Versión 0.14.13 (commit [commit]). El combate, con menos que leer: la ficha del soldado elegido, rehecha, y una hoja nueva con el acierto y el daño de cada arma. Y en el iPhone y el iPad, el aviso de unirse ya dice qué falla.

## Partidas guardadas

- **La partida de la 0.14.12 sigue en la 0.14.13.**

## Novedades de la 0.14.13

### El combate

Todo esto, con ratón. En el iPad y el iPhone, el combate sigue como en la 0.14.12.

- **La barra de acciones va sobre una placa,** como la de la escuadra. Las teclas son del mismo tamaño que antes. Las que no se pueden usar se ven apagadas, y «Terminar turno» va algo aparte.
- **La ficha del soldado elegido ya no es una tira suelta** encima de la escuadra: es su tarjeta, que crece al lado de su cara. Lleva:
  - el arma, con su dibujo y su nombre;
  - la salud y las acciones, en barras;
  - las balas y los cargadores;
  - la cobertura, los estados («VIGILANCIA», «CARGA»…) y «MOCHILA».

  El movimiento va en la barra («8/8»). El rango y la puntería salen al pasar el ratón por la ficha.
- **La hoja del soldado se abre pulsando su ficha,** o con la H. La cara sigue igual: un clic elige al soldado y el doble clic lleva la cámara hasta él. En la hoja:
  - la salud, las acciones y el movimiento, en barras;
  - cada cifra con su icono, sin rótulo: el nombre sale al pasar el ratón;
  - cada arma que lleva, con su acierto en % y, aparte y en rojo, su daño. Debajo, en pequeño, el alcance, el cargador y el ruido.
- **Los dos jugadores, en una sola placa.** Ya no sale «Con acciones»; «A punto» sí, cuando el compañero termina.
- **La tarjeta del objetivo, más pequeña:**
  - la frase fija de la etapa («Registrad lo que podáis y evacuad») sale al pasar el ratón por su nombre («SAQUEO»). Lo que cuenta algo sigue a la vista: el botín, el turno en que llega La Lata, los pasos;
  - el ruido lleva el altavoz en vez de «RUIDO».
- **El porcentaje de un zombi que está fuera de la pantalla** ya no tapa el ojo, la flecha del objetivo, el selector de plantas, la barra ni las caras.

### Unirse en el iPhone y el iPad

- **El aviso dice qué falla,** como en el Mac desde la 0.14.12. Hasta ahora decía siempre «No responde nadie…». Ahora también:
  - en esa dirección no hay ninguna partida abierta;
  - el iPhone o el iPad no deja a Bastión usar la red local: dónde activarlo.

## Lo que sigue sin arreglar

- **El velo del menú de inicio,** en la app, sale más flojo que en el editor: la mitad de arriba se ve más clara.
- **El icono de la esquiva** en la hoja (las flechas «»»») no se entiende sin su ayuda.
- **Algunas ayudas de la hoja** solo dicen el nombre (la salud, la voluntad, el daño…) y no qué es.
- **Al abrir «Unirse por dirección» con el teclado,** hay que pinchar el campo para escribir.
- **«Alojar» con una partida guardada** enseña la hoja «Empezando una partida nueva», aunque abre la guardada.
- **Un soldado o un cuerpo** puede quedar tapado por un edificio sin descubrir.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **Quedan saltos de animación:**
  - un soldado se levanta de golpe;
  - algunos muertos suben el brazo o bajan el cuerpo de golpe;
  - el malherido de la Camilla, si muere, se pone de pie de golpe antes de caer.

  Si veis alguno, apuntad dónde y cuándo.
- **La dificultad** se ajusta jugando: apuntad lo que notéis en cada nivel.
- **La sincronía vertical con la pantalla dormida,** o apagada al momento con una esquina activa, está sin probar.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.14.13-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.13-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.13:** con la 0.14.12 no se puede jugar juntos.
- **En el Mac,** la app se llama «Bastión 0.14.13» y no sustituye a la 0.14.12. Borrad de Aplicaciones las versiones viejas: con varias, el Mac puede dejar a la nueva sin red local. Las partidas y los ajustes son los mismos. Si teníais la vieja en el Dock, poned la nueva.
- **En Windows,** la carpeta se llama «Bastion 0.14.13».
- **Descargas solo para Mac y Windows:** no hay versión para Linux ni para el navegador. En el iPhone y el iPad, la app se instala desde Xcode.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **En el Mac, para unirse:** en Ajustes del Sistema → Privacidad y seguridad → Red local, activad «Bastión 0.14.13».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
