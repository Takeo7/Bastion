# Notas de la primera versión de Unity (borrador)

Borrador de Lore (26/10) para cuando Arturo saque la primera versión hecha en Unity (hito 3, «Releases de la app de Unity», en [UNITY.md](UNITY.md) §5). Es para Arturo y Ana, que vienen de la 0.10.1: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] se ajusta al final, con lo que haya. Desde «Título» hasta el final es el texto de la release, con el formato de la 0.10.1.

---

**Título:** Bastión [versión]: el juego en Unity

Versión [versión] (commit [commit]). Es la primera versión hecha en Unity. La campaña y las misiones son las de la 0.10.1. Cambian la app y algunas reglas, tras la prueba de Ana del 28/10.

## Una app nueva

- **Es otra app,** no una actualización de la 0.10.1. [Comprobar al cerrar: en el Mac las dos se llaman `Bastion.app`. Si es así, al copiar esta a Aplicaciones sustituye a la 0.10.1, y hay que decirlo.]
- **Los dos, con esta versión:** con la 0.10.1 no se puede jugar juntos.

## Partidas guardadas

- **La campaña de la 0.10.1 no pasa a esta versión.** Aquí se empieza una campaña nueva.
- **La de la 0.10.1 sigue en su carpeta:** si se abre la 0.10.1, está como se dejó.
- **La campaña empezada con las apps de prueba de Unity sigue aquí:** se continúa donde se dejó. [Comprobar al cerrar: la carpeta de las partidas sale del nombre de la app (Bastion-Unity/Bastion). Si cambia al decidir el nombre de la app del Mac, se quita o se cambia esta línea, y también la de los caminos del asedio.]
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
- **Cuatro armas cuerpo a cuerpo nuevas,** que solo se encuentran registrando: la sartén, el bate, la palanca y la motosierra. La motosierra tiene tres usos por misión.
- **En la Limpieza también llega la horda,** pero solo la atrae el ruido.
- **El medidor de ruido,** en la tarjeta del objetivo, cuenta en dB lo que falta para la próxima oleada de la horda: a 90 se oye llegar, y entra por los bordes un turno después.

### En combate
- **Esc para la partida a los dos** y tapa la pantalla de quien abre el menú. En la otra pantalla sale «Partida en pausa: Ana está en el menú.».
- **A 3 turnos de perder la misión,** un aviso: «QUEDAN 3 TURNOS».
- **F12 guarda una captura** en Imágenes/Bastión. Los atajos de captura del Mac también funcionan, a pantalla completa incluida.
- **Lanzar enseña siempre la granada,** apagada cuando no quedan.
- **El escudo de cobertura propio** sale cuando en la misión se ha visto a un enemigo que dispara.
- **Sin compañía,** no salen el marco del turno ni la lista de jugadores.

### La base
- **Pulsar una zona del Mapa** abre su hoja, con todas sus misiones.
- **«Cancelar misión» se vota,** como desplegar y contactar.

### Los textos
- **Bielsa se llama ahora Metsovo,** también el Refugio y el consultorio del primer capítulo.
- **La caravana es una autocaravana:** el Bastión empieza siendo una «Autocaravana», luego una «Autocaravana con toldo».
- **Nada da por hecho que un soldado es un hombre:**
  - los avisos dicen «No tienes heridas» o «No sufre aturdimiento, desorientación ni pánico»;
  - los rótulos de estado concuerdan con quien los lleva: ATURDIDA, DESORIENTADA, EVACUADA.
- **El otro jugador sale «A punto», «En línea» o «Sin conexión».**
- **El tiempo, siempre en misiones:** «Misiones de baja», «Se sabrá tras la próxima misión», «Fiebre: +1 misión».
- **«Volver al menú principal»,** donde antes ponía «menú de inicio».
- **Quien se rescata es «la persona retenida»,** donde antes ponía «el VIP».

### Ajustes
- **«Gráficos» (Altos, Medios, Bajos)** junta lo que antes eran Efectos, Sombras y Resolución. Cambia al momento.

### Se ve distinto
- **Viento suave** en los árboles y las enredaderas, en la base y en el combate.
- **La pistola va en su funda y el fusil, a la espalda,** cuando no se usan.
- **Las barricadas de sacos se ven gastadas** según el daño que llevan.
- **Las sombras llegan a todo lo que se ve,** también con la cámara alejada del todo, en el combate y en la base.
- **Una casa sale en negro hasta que entra un soldado.** Un enemigo que se ve por una ventana sale con su casilla iluminada.
- **Coches enteros y destrozados,** con la pintura apagada.
- **En las aceras, bolardos y papeleras;** las macetas con árbol, solo en plazas y parques.
- **Los muertos no llevan ropa azul ni morada.**
- **En el asedio,** cada punto cardinal sale también en el borde de la pantalla.

## Lo que aún no está

[Se ajusta al final. El sonido y los muertos especiales con su cuerpo ya llegaron (9ff0b7e). Si al cerrar no falta nada, esta sección se quita.]

## Descargas

[Los nombres, como salgan de AppBuild.]

| Archivo | Para |
|---|---|
| `Bastion-[versión]-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-[versión]-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |
| `Bastion-[versión]-linux-x64.tar.gz` | Linux de 64 bits: descomprimir y abrir `Bastion/Bastion.x86_64` desde una terminal |

## Avisos

- **Los dos jugadores, con la misma versión.**
- **Solo escritorio:** no hay versión para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos:**
  - macOS: la primera vez, el Mac pregunta si Bastion puede aceptar conexiones entrantes: «Permitir».
  - Linux con el cortafuegos activado (ufw): para alojar, hay que abrir el TCP 41391 (o el siguiente libre, hasta el 41400) y el UDP 41390.
- [**Windows y Linux:** construidas desde el Mac y sin probar en un PC ni en un Linux de verdad.]
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
