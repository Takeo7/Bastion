# Notas de la 0.14.2

Texto de Lore para la 0.14.2. Lo pidió Producto el 23/11, con el árbol congelado. Sin commit.

**Qué lleva:**
- M1, los edificios sin puertas (Gameplay; cuadros 133, 134 y 137);
- el mapa de etapas en papel (UI y Arte; cuadros 131, 132 y 135);
- las hojas de carga, la salida de La Lata a la vista y el combate sin «Entendido» (UI y Arte; cuadros 136, 138 y 139);
- las llamas de puertas y ventanas (Arte, `FireView.cs`).

**Fuentes:**
- `.dev-snapshots/lore/ARBOL-CONGELADO.md`;
- `producto/RUNS.md`, cuadros 131 a 139;
- `gameplay/notas-23-11/SIN-PUERTAS.md`;
- `ui/unity/runs/m2-cargas/CUADRO-138-139.md`;
- `producto/MENSAJE-DE-UI-tarjeta-entendido.md`, `MENSAJE-DE-ARTE-salida-de-la-lata.md` y `MENSAJE-DE-ARTE-fireview.md`.

**Lo que no va:**
- lo de dentro: pruebas, laboratorios y las líneas nuevas de `red.log`;
- lo que se deja para la 0.14.3: M3 (Mayús y clic), M4 (la ficha en «Partir»), M5 (casillas vacías), M6 (esquiva y defensa), M7 (el soldado que se une), la escena de arranque con «BASTIÓN», la bienvenida que tapa a los soldados y la copa del árbol a puntos;
- lo que solo cambia en el iPad y el iPhone.

Es para Arturo y Ana: cuenta qué cambia al jugar. Va para Mac y Windows. Producto completa al publicar lo que va entre [corchetes]: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.14.2: el mapa en papel y edificios sin puertas

Versión 0.14.2 (commit [commit]). Las notas de Arturo del 23/11: los edificios sin puertas se ven por dentro, el mapa de etapas dibujado en papel y las esperas en hojas de papel.

## Lo que cambia la partida

- **Un edificio con un hueco sin puerta se ve por dentro, como la calle,** desde el principio. Antes no se veía hasta entrar.
  - Un hueco es una abertura a la calle sin puerta: la boca de carga de una nave, un coche estrellado contra una fachada, un boquete.
  - Un edificio con puertas y, además, un hueco, también se ve.
  - Las ventanas, aunque estén rotas, y las puertas tapiadas no cuentan.
- **Una granada que abre un boquete a la calle deja a la vista la casa entera,** todas sus plantas.
  - Vale también para el boquete de la granada de un Saqueador.
  - Un boquete entre dos casas pegadas no abre ninguna.
  - En los mapas de un solo edificio (hospital, oficinas, comisaría, nave), abre solo la sala de detrás.
- **Ver a los de dentro no los despierta.** Despiertan como siempre: si ven u oyen a un soldado, o si les atacan.

## Partidas guardadas

- **La partida de la 0.14.1 sigue en la 0.14.2.**
- **Un combate guardado a medias** sigue donde estaba, y al cargarlo sus edificios con hueco quedan a la vista.

## Novedades de la 0.14.2

### El mapa de etapas, en papel

- **Es un mapa dibujado:** papel envejecido con el valle calcado debajo (el río, la carretera, las pistas), y las etapas y los caminos a mano:
  - en rojo, a dónde se puede ir;
  - en negro, el recorrido hecho;
  - más tenue, lo que queda más adelante.
- **Para ir a una etapa:** un clic la elige y abre su ficha a la derecha; se va con «Ir a esta etapa».
- **La ficha de la etapa, por partes:**
  - «Etapa 4 de 7»;
  - el peligro;
  - «Qué hay que hacer»;
  - «Dónde», con el sitio.
- **Junto a cada etapa,** sus notas: «de noche», «mal tiempo», «horda despierta», «Sin plaza libre» y, a dos, «Propuesta de Ana».
- **La etapa del jefe** dice «Fin del capítulo».
- **Debajo del mapa,** una leyenda con los iconos y las líneas.
- **La escuadra, plegada:** los bustos con su salud. «Equipo ▸» abre las mochilas y «Mapa ◂» vuelve al mapa.

### Las esperas, en papel

- **Al ir a una etapa,** en vez de la imagen parada sale una hoja de papel con:
  - «Siguiente etapa» y su nombre;
  - el sitio y el peligro;
  - sus notas y lo que hay que hacer;
  - quién va, cada nombre en el color de su jugador.
- **Al salir de la base** se ve a La Lata arrancar e irse, en unos 2 segundos; después, la hoja. Antes eran casi 6 segundos en negro. Entre etapas, la hoja sale directa.
- **Las demás esperas, también en papel:**
  - al volver del combate: «Sigue la expedición» o «Vuelta a la base», con el resultado de la etapa;
  - al continuar una partida («Continuando la partida», «Capítulo 1 · Medio»), con la etapa a medias si la hay;
  - al empezar una nueva («Empezando una partida nueva»);
  - al entrar en una Escaramuza.
- **Solo al abrir el juego** sale «BASTIÓN» sobre negro.

### Se entra al combate sin «Entendido»

- **Ya no sale la tarjeta** con la etapa, el objetivo y «Entendido»: la hoja ya lo dice.
- **Al quitarse la hoja,** sale «TU TURNO» y se queda hasta el primer clic o tecla, que ya cuentan como orden; mientras tanto, el reloj del turno no corre. Al otro jugador le sale «TURNO DE ANA».
- **La ficha del objetivo,** arriba a la izquierda, sigue abriendo la tarjeta para releerla.

### El fuego en puertas y ventanas

- **Una puerta o una ventana que arde** ya no saca las llamas por fuera del muro: van pegadas al marco, en el lado que arde. Desde la calle, el fuego de dentro se ve por el cristal.

## Lo que sigue sin arreglar

- **Un soldado o un cuerpo** puede quedar tapado por un edificio sin descubrir.
- **Los marcos de una casa sin descubrir** se ven claros, como si diera la luz de la calle.
- **Una farola** puede quedar en el bordillo, delante de las puertas de La Lata.
- **El aviso del compañero caído** («Pierre Lambert ha caído · Ver (F)») tapa «Turno enemigo».
- **Los guardias sentados de la hoguera,** con la cámara normal, parecen de pie.
- **La bienvenida de la base** tapa a los soldados.
- **La dificultad** se ajusta jugando: apuntad lo que notéis en cada nivel.
- **La sincronía vertical con la pantalla dormida,** o apagada al momento con una esquina activa, está sin probar.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.14.2-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.14.2-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.14.2:** con la 0.14.1 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.14.1.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma *ad hoc*. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
