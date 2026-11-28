# Notas de la 0.14.15

Texto de Lore para la 0.14.15. Lo pidió Producto el 28/11 (`lore/MENSAJE-DE-PRODUCTO-0.14.15.md`, con su añadido). Corto. Sin commit.

**Qué lleva:** las notas de Ana y Arturo del 28/11 que entran en esta versión:
- la portada y el icono nuevos (notas 1 y 2; cuadro 193);
- «Bastion» sin tilde en todo el juego (cuadros 188 y 192);
- «Terminar turno» solo con el mando (nota 10);
- las paredes que parpadeaban a dos (nota 11);
- los soldados de quien entra con la expedición en marcha (nota 9);
- la parada al andar con su motivo (nota 12).

**Lo que no lleva:** el salto en el hospital (nota 6) y las reglas de las notas del 28/11. Va en «Pendiente».

**Fuentes:**
- `producto/ENTREGA-0.14.15-gameplay.md`, `-ui.md`, `-arte.md`, `-red.md` y `-lore.md`;
- `producto/notas-2026-11-28/PLAN.md` y `producto/RUNS.md`, cuadros 188 a 193;
- la release de la 0.14.14, para la frase de la escaramuza de «Partidas guardadas»: la versión afinada quedó solo allí.

**Lo de Lore, en el árbol y sin commit:**
- «Bastion» sin tilde en 87 cadenas del juego, con sus pruebas y los JSON de `golden/` (parte de Lore);
- `Lore.WalkText`, para la nota 12.

**Sin probar, para Producto:**
- **Nada se ha visto en la app:** solo en el editor y en el laboratorio. Tampoco a dos por red, ni el ratón ni el dedo en la portada, ni el iPhone y el iPad, ni Windows.
- **El tono del velo** está medido en el laboratorio, no en la app (UI).
- **La parada al andar** solo se ha visto con el suceso puesto a mano en el laboratorio, no salido del motor (UI).
- **La carpeta de capturas nueva:** nadie ha guardado una captura en ella (UI).

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows; lo del iPhone y el iPad, para la app que Arturo instala desde Xcode. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastion 0.14.15: la portada nueva y arreglos a dos

Versión 0.14.15 (commit [commit]). Portada e icono nuevos, y el nombre, sin tilde: Bastion. A dos, «Terminar turno» solo con el mando, las paredes ya no parpadean y quien entra con la expedición en marcha tiene soldados suyos. Y si un soldado se para al andar, se dice por qué.

## Partidas guardadas

- **La partida de la 0.14.14 sigue en la 0.14.15.**
- **Lo ya escrito en una partida** (el registro, el diario) sigue diciendo «Bastión», con tilde. Lo nuevo sale sin ella.
- **Si apartó la expedición la 0.12.26 o la 0.12.27** y aún no habéis abierto la 0.14.14, la 0.14.15 también la recupera sola. Hay un caso que no recupera: después de alojar con la 0.12, una escaramuza dejada en pantalla, a medias o acabada sin pulsar «Volver al menú». Cómo arreglarlo a mano está en las notas de la 0.14.14.

## Novedades de la 0.14.15

### La portada y el nombre

- **La portada, nueva:**
  - el rótulo «BASTION», sin tilde y estarcido;
  - un velo con textura sobre la escena;
  - la opción elegida, con una pincelada roja oscura;
  - todas las letras, en blanco hueso y algo más grandes.

  Lo mismo en la ventana de carga, en «Multijugador», en la sala y en los formularios («Unirse», «Empezar de cero»…).
- **El icono, nuevo:** el número de la versión, «14.15», en rojo sobre negro. En el Mac, en Windows y en el iPhone y el iPad.
- **«Bastion», sin tilde, en todo el juego:** el rótulo, los textos y el nombre de la app. En el Mac se llama «Bastion 0.14.15»; en el iPhone y el iPad, «Bastion» bajo el icono.
- **Las capturas (F12)** van a «Imágenes/Bastion». Las de antes siguen en «Imágenes/Bastión».

### A dos

- **«Terminar turno», solo con el mando.** Sin él, la tecla está apagada y Retroceso no hace nada; al pasar el ratón, dice quién lo tiene. Si pulsasteis «Terminar turno» y quedasteis «A punto», ya no se puede quitar.
- **Las paredes ya no aparecen y desaparecen** con un soldado de cada uno en una planta distinta. Mientras el compañero tiene el mando, el edificio se queda en la planta de su soldado, y vuelve a la del vuestro cuando el mando vuelve a vosotros. En el turno de los zombis se queda en la planta del último que actuó, también jugando solo.
- **Quien entra con la expedición en marcha tiene soldados suyos.** Hasta ahora se quedaba con los dos de la base y le prestaban uno de la escuadra. Si caía la escuadra, en la base quedaban solo los suyos, y en la siguiente salida le prestaban uno al otro. Ahora la mitad de la escuadra pasa a ser suya y, si hace falta, recibe soldados de la base hasta tener dos.

### Al andar

- **Si un soldado se para a medio camino, se dice por qué.** Sobre él sale «CAMINO CORTADO», y en el registro, quién se le ha cruzado («alguien que no se ve», si no está a la vista). Pasa cuando alguien se mete en el camino mientras anda: un grupo de enemigos que corre a cubrirse o un civil que echa a andar.
- **Recupera el movimiento que no ha andado.** Hasta ahora se cobraba el camino entero.
- **Ya no se queda quieto cuando se activa un grupo que no se ve.** Hasta ahora, si un grupo de enemigos fuera de la vista le oía o le veía mientras andaba, el soldado se paraba casi medio segundo a mitad de camino, sin aviso.

## Pendiente

- **Salir saltando del hospital, las oficinas y la comisaría** sigue igual: la mayoría de las ventanas de arriba dan fuera del mapa y no dejan saltar. Llega en la siguiente, con un callejón alrededor del edificio.
- **El resto de las notas del 28/11 va en la siguiente:**
  - el daño del salto;
  - registrar las tiendas de camping;
  - las puertas;
  - los zombis en las plantas de arriba;
  - la escalera por casillas;
  - controlar al zombi en que se convierte vuestro último soldado.
- **Sin ver aún en la app:** todo lo de arriba, también a dos por red; el ratón y el dedo en la portada; el iPhone y el iPad; Windows.

## Lo que sigue sin arreglar

- **Entre vuestros dos ordenadores, la conexión aún no llega,** y no se sabe por qué. Si os pasa, apuntad qué dice el aviso y guardad el `red.log` de los dos, en la carpeta de datos.
- **Unirse no se ha probado entre dos ordenadores:** las pruebas se han hecho con las dos apps en el mismo ordenador.
- **Si quien empezó la partida tiene dos soldados y los dos están en la escuadra,** al entrar el otro con la expedición en marcha se le sigue prestando uno, como antes.
- **Puede quedar alguna parada sin motivo al andar:** se ha quitado la del grupo que no se ve, pero no se han medido las de las puertas, el fuego, las caídas, los objetos y los supervivientes a mitad de una marcha. Si lo veis, apuntad dónde.
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
| `Bastion-0.14.15-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.15-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.15:** con la 0.14.14 no se puede jugar juntos.
- **En el Mac,** la app se llama «Bastion 0.14.15», ya sin tilde, y no sustituye a la 0.14.14. Borrad las versiones viejas, como «Bastión 0.14.14»: con varias, el Mac puede dejar a la nueva sin red local. Las partidas y los ajustes son los mismos. Si teníais la vieja en el Dock, poned la nueva.
- **En Windows,** la carpeta se llama «Bastion 0.14.15».
- **Descargas solo para Mac y Windows:** no hay versión para Linux ni para el navegador. En el iPhone y el iPad, la app se instala desde Xcode.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **En el Mac, para unirse:** en Ajustes del Sistema → Privacidad y seguridad → Red local, activad «Bastion 0.14.15». Si salen otras, como «Bastión 0.14.14», borradlas.
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
