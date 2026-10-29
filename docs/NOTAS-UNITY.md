# Notas de la primera versión de Unity

Texto de Lore para la primera versión hecha en Unity (hito 3, «Releases de la app de Unity», en [UNITY.md](UNITY.md) §5). Empezó como borrador el 26/10 y se cerró el 29/10 con las notas de Ana del 28/10 y del 29/10. Es para Arturo y Ana, que vienen de la 0.10.1: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de la 0.10.1.

---

**Título:** Bastión 0.11.0: el juego en Unity

Versión 0.11.0 (commit [commit]). Es la primera versión hecha en Unity. La campaña y las misiones son las de la 0.10.1. Cambian la app y algunas reglas, con las últimas notas de Ana.

## Una app nueva

- **Es otra app,** no una actualización de la 0.10.1.
- **En el Mac se llama igual, `Bastion.app`:** al copiarla a Aplicaciones, sustituye a la 0.10.1. Las partidas de cada una siguen en su carpeta.
- **Los dos, con esta versión:** con la 0.10.1 no se puede jugar juntos.

## Partidas guardadas

- **La campaña de la 0.10.1 no pasa a esta versión.** Aquí se empieza una campaña nueva.
- **La de la 0.10.1 sigue en su carpeta:** si se vuelve a abrir la 0.10.1, está como se dejó.
- **La campaña empezada con las apps de prueba de Unity sigue aquí:** se continúa donde se dejó.
- **En esa campaña, lo que ya estaba escrito sigue diciendo «Bielsa» y «caravana»:** el registro, el Diario y las misiones que ya estaban en el Mapa.

## Lo que cambia al jugar

### Entrar en la partida
- **«Ser anfitrión» se llama ahora «Alojar la partida».**
- **El nombre se escribe en la primera pantalla,** en «Tu nombre».
- **La dirección para «Unirse por dirección»** lleva ahora el puerto 41391 (por ejemplo, 192.168.1.20:41391). Sale en el menú principal de quien aloja.
- **Escaramuza ya no tiene el ritmo en tiempo real,** que era una prueba.

### Las reglas
- **El zombi común y el Acorazado andan más:** el 80 % de lo que anda un soldado (antes, el 60 %).
- **Un zombi que no ha visto a nadie** gasta sus dos acciones en vagar.
- **El Chillón ya no se aleja y vuelve en el mismo turno.** Si no os ve, busca desde dónde veros a 5 casillas o más; si estáis más lejos que su grito, se acerca.
- **En el asedio:**
  - hay un solo camino, al sur, por el que llega La Lata;
  - los muertos salen de entre los pinos, por las veredas;
  - en una campaña nueva, el claro es más pequeño: de 6 a 8 casillas de radio (antes, de 8 a 11);
  - en una campaña empezada con las apps de prueba, los dos caminos de los lados y el del norte se llenan de pinos; queda el del sur.
- **La piedra ya no es de todos:** se aprende con «Piedras», la primera casilla de Lanzar en el tablero. En una campaña empezada con las apps de prueba, quien tenía «Brazo suelto» tiene ahora «Piedras».
- **Cuatro armas cuerpo a cuerpo nuevas,** que solo se encuentran registrando. Van en el cinto, como el machete:
  - la sartén, 3 de daño;
  - el bate, 4 de daño;
  - la palanca, 4 de daño, y atraviesa 1 de blindaje;
  - la motosierra, 6 de daño y tanto ruido como la ametralladora. Tiene gasolina para tres usos por misión, y su tecla enseña los que le quedan.
- **El medidor de ruido,** en la tarjeta del objetivo, sube con el ruido que hacéis y con los turnos que pasan, de 40 dB a 90. A 90 se oye llegar una oleada de la horda, que entra por los bordes un turno después. En el asedio no sale.
- **En la Limpieza también llega la horda,** pero solo el ruido sube el medidor.

### En combate
- **Esc para la partida a los dos** y tapa la pantalla de quien abre el menú. En la otra pantalla sale «Partida en pausa: Ana está en el menú.».
- **A 3 turnos de perder la misión,** un aviso: «QUEDAN 3 TURNOS».
- **Cuando un zombi ataca a un soldado,** la cámara se pone sobre el hombro del soldado hasta la acción siguiente.
- **Al pasar el ratón por un zombi,** lo que ve lleva un borde rojo suave, con las esquinas redondeadas.
- **La lista de soldados** lleva el retrato de cada uno junto a su nombre.
- **F12 guarda una captura** en Imágenes/Bastión. Los atajos de captura del Mac también funcionan, a pantalla completa incluida.
- **Lanzar enseña siempre la granada,** apagada cuando no quedan.
- **El escudo de cobertura propio** sale cuando en la misión se ha visto a un enemigo que dispara.
- **Sin compañía,** no salen el marco del turno ni la lista de jugadores.

### La base
- **La caravana es ahora una autocaravana,** en el claro y en los textos: el Bastión empieza siendo una «Autocaravana», luego una «Autocaravana con toldo».
- **Pulsar una zona del Mapa** abre su hoja, con todas sus misiones.
- **«Cancelar misión» se vota,** como desplegar y contactar.
- **En Soldados y en Personalizar, arrastrar con el ratón** sobre el claro gira al soldado.
- **Al volver de una misión, la página de la historia sale abierta** junto a CONTINUAR, sin «Leer el parte».

### Los textos
- **Bielsa se llama ahora Metsovo,** también el Refugio y el consultorio del primer capítulo.
- **Nada da por hecho que un soldado es un hombre:**
  - los avisos dicen «No tienes heridas» o «No sufre aturdimiento, desorientación ni pánico»;
  - los rótulos de estado concuerdan con quien los lleva: ATURDIDA, DESORIENTADA, EVACUADA.
- **El otro jugador sale «A punto», «En línea» o «Sin conexión».**
- **El tiempo, siempre en misiones:** «Misiones de baja», «Se sabrá tras la próxima misión», «Fiebre: +1 misión».
- **«Volver al menú principal»,** donde antes ponía «menú de inicio».
- **Quien se rescata es «la persona retenida»,** donde antes ponía «el VIP».

### Ajustes
- **«Gráficos» (Altos, Medios, Bajos)** junta lo que antes eran Efectos, Sombras y Resolución. Cambia al momento.

### Se ve y se oye distinto
- **Los aros bajo las figuras, mucho más finos,** del color de cada uno.
- **Las barras de vida van por trozos:** uno por punto de vida, verde en los soldados y rojo en los enemigos.
- **El blanco elegido lleva una línea roja fina alrededor,** en lugar de teñirse entero de rojo.
- **Sobre el enemigo que os ve, un ojo pequeño y gris,** en lugar del rojo.
- **El hexágono de cada soldado** sale del color de la rama del tablero en la que más casillas ha comprado; si empatan, partido entre las dos.
- **Viento suave** en los árboles y las enredaderas, en la base y en el combate.
- **La pistola va en su funda y el fusil, a la espalda,** cuando no se usan.
- **Las barricadas de sacos se ven gastadas** según el daño que llevan.
- **Las sombras llegan a todo lo que se ve,** también con la cámara alejada del todo, en el combate y en la base.
- **Una casa sale en negro hasta que entra un soldado.** Un enemigo que se ve por una ventana sale con su casilla iluminada.
- **Coches enteros y destrozados,** con la pintura apagada.
- **En las aceras, bolardos y papeleras;** las macetas con árbol, solo en plazas y parques.
- **Los muertos tienen apagados los verdes, los azules y los morados,** en la piel, el pelo y la ropa.
- **Cinco Hinchados distintos.** El Hinchado tiene ahora su voz, sus pasos y su muerte, y su estallido suena más fuerte.
- **En el asedio,** cada punto cardinal sale también en el borde de la pantalla.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.11.0-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.11.0-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la misma versión.**
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** la primera vez, el Mac pregunta si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
