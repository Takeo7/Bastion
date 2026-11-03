# Notas de la 0.11.7

Texto de Lore para la 0.11.7, con las notas de Arturo y de Ana del 2/11, decididas por Arturo ([PRUEBA-2026-11-02-ARTURO.md](PRUEBA-2026-11-02-ARTURO.md): de Arturo, de la 1 a la 13; de Ana, de la 1 a la 7, con las puertas tapiadas; la 6 y la 7, de Red), y lo que dejó la prueba de la 0.11.6 con la app (`.dev-snapshots/ui/unity/app-0116/`): la cámara sobre una azotea en Metsovo y la ropa de los zombis. Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.11.7: registrar cuerpos y maleteros, la palanca y armas nuevas

Versión 0.11.7 (commit [commit]). Las notas de Arturo y de Ana del 2/11, con la 0.11.6, y los arreglos de su prueba.

## Novedades de la 0.11.7

### Al empezar una campaña
- **2 soldados por jugador,** no 4. En solitario, 4.
- **Entre los 4, dos pistolas, un machete y una palanca,** cada arma a un soldado al azar. Todos llevan su cuchillo. Sigue sin haber granadas ni botiquín.
- **Si alguien se une a una campaña empezada en solitario,** recibe 2 soldados.

### Un recluta si te quedas sin soldados
- **Si un jugador se queda sin soldados vivos, le llega uno a la base, gratis,** al acabar la misión o al pasar el día.
- **El registro lo dice:** «Samira Benali se une al Bastión como recluta: a un jugador no le quedaban soldados vivos.»

### Registrar cuerpos y maleteros
- **«Abrir la caja» se llama ahora «Abrir o registrar»:** abre cajas y maleteros, y registra cuerpos. Como antes, cuesta 1 acción y hace un poco de ruido. El botón dice qué se abre o se registra.
- **Cuerpos:** se registra el de cualquier enemigo caído, desde al lado o de pie sobre él. Los vivos llevan siempre su arma. Un zombi casi nunca lleva una (1 de cada 100), salvo los policías y los militares: uno de cada cuatro.
- **Los zombis policías y militares se distinguen por la ropa:** los policías, de uniforme de policía, y los militares, de soldado. Ningún otro zombi va de policía.
- **Maleteros:** el maletero cerrado de cualquier coche entero se abre una vez. Los coches chocados, no. Sale lo que haya: suministros, piezas, medicinas, algún cargador o accesorio y, a veces, un arma. Lo que había dentro queda en el suelo, detrás del coche.
- **Coches de policía en cualquier misión:** uno de cada 6 coches enteros. Su maletero lleva un arma una vez de cada tres.
- **Ya no hay un coche de policía seguro en la primera misión,** ni se abre un coche por delante.

### La palanca y las puertas tapiadas
- **«Quitar las tablas», solo con la palanca:** 1 acción y poco ruido.
- **En una ventana tapiada,** quita las tablas y los muebles. Después se puede pasar y disparar por ella.
- **Puertas tapiadas:** una de cada 4 puertas exteriores de casas, tiendas y oficinas. Nunca la última del edificio, así que siempre hay por dónde entrar. Quitadas las tablas, queda la puerta cerrada, que se abre como las demás.

### Armas de mano nuevas
- **Katana, espada, rastrillo y palo.** Salen al registrar, como las demás armas.
- **La katana y la espada golpean con el Tajo a todos los enemigos que tengas al lado,** no a uno. La motosierra, también. La katana pega más que el machete; la espada, como él.
- **El rastrillo llega a dos casillas,** en línea recta o en diagonal, si en medio no hay nadie. Pega poco.
- **El palo es lo que menos pega, pero casi siempre acierta.**

### El ruido
- **El medidor de ruido solo sube con el ruido que hacéis.** Antes subía también con cada turno. Sin ruido, no llega ninguna oleada. Vale en todas las misiones menos el asedio, también de noche.
- **Los pocos zombis que entran cada turno, pase lo que pase, siguen igual.**

### La piedra
- **Solo hace ruido,** para atraer a los zombis, hasta que aprendes Canto afilado, Pedrusco, Pedrada u Honda, que le dan daño. Antes hacía daño desde el principio.

### Comprar y fabricar por cantidades
- **En la Armería, «−» y «+» junto al botón:** de 1 a 10 de una vez, por ejemplo 4 cócteles molotov. Se paga por todos.
- **En el Taller, igual con las armas y sus mejoras.** Fabricar 3 tarda tres veces lo que una. Lo que se hace una sola vez sigue igual.

### Los avisos de zombis
- **Marcas en vez de letreros:** el zombi que acabáis de ver lleva 2 s una marca en su rótulo, un ojo gris si no os ha visto o un «!» rojo si os ha visto. Solo la primera vez que se ve en la misión.
- **Una línea en el registro por cada vez,** con todos los que se ven: «A la vista: 4 zombis».
- **«¡CONTACTO!» sale solo la primera vez de cada turno.**
- **El contacto de los soldados de tu compañero va al registro,** con «Ver» para llevar la cámara allí.

### Sin transmisiones de radio
- **Ya no salen las transmisiones de radio,** las escenas con caras que salían en la base. Se han quitado del todo. Los acontecimientos que llegan por radio, como «Transmisión interceptada», siguen.
- **Lo único que solo decían ellas sale ahora en el registro:** al nivel 4 del Bastión, «La infestación tiene nombre: la Marea. Desde ahora, el contador dice a cuántos kilómetros está del Bastión.»
- **En Ajustes, el volumen «Interfaz y radio» se llama ahora «Botones y avisos».**

### En el mapa
- **Desde el mapa (M) también se puede marcar,** como en la vista normal: Mayús y clic, o clic con la rueda.

### En la base
- **Cada uno ve sus soldados a la izquierda** al elegirlos, como ya era, y también a la vuelta, en el Parte, que antes salía mezclado.

### El asedio
- **Quien llega a la base con el asedio ya empezado también defiende:** el recluta de cuando te quedas sin soldados, o los refugiados que se acogen. Antes se quedaba fuera y, si no había nadie más, la partida se atascaba.
- **Si no queda ningún sano, defienden los heridos,** con la vida entera. Antes, un asedio con todos heridos no tenía salida.
- **Los mordidos se quedan siempre dentro.**

### Ajustes de vídeo
- **Pantalla:** «Completa» o «Ventana». En Windows, también «Sin bordes», que ocupa toda la pantalla como una ventana y deja cambiar antes a otro programa.
- **Resolución:** con menos va más fluido y se ve menos nítido. La interfaz no cambia de tamaño.

### Los edificios
- **En las plantas de arriba, una ventana de cada dos casillas, en columnas.** Antes iban alternas de planta en planta, como un tablero, y con las ventanas grandes de las tiendas parecían filas a medias.

### La cámara y los edificios
- **La cámara ya no se queda sobre la azotea de un bloque alto** cuando los zombis que se ven están detrás de él, como pasaba en Metsovo. Pasaba en tres momentos: al enseñar a los que andan en el turno de los zombis, tras un tiro y en el contacto.
- **El edificio que tapa a un enemigo se abre en cuanto lo veis.** Antes, los edificios se abrían según lo que la escuadra veía al empezar la misión o al cargarla, no según lo que ve en cada momento.
- **Se notará en toda la misión:** también se abren los edificios en los que la escuadra ve algo por dentro. Lo que aún no ha visto de dentro sale oscuro, con la niebla.

### La ropa de los zombis
- **También se apaga la ropa clara con algo de color,** no solo la más chillona. Por ejemplo, la blusa rosa y la falda amarilla de una de las zombis salen ahora rosa apagado y beis. Vale para los comunes y para los especiales.
- **La ropa oscura no cambia.**

### Los animales
- **El perro ya no da tirones:** acelera y frena, y si estaba tumbado se levanta antes para huir.
- **Rodea paredes, coches y muebles,** como los soldados. Antes pasaba a través de ellos.

### El cóctel molotov
- **El fuego cubre la casilla entera:** cuatro llamas por casilla y el suelo ardiendo debajo.

### «Un desertor»
- **La propuesta que no se aceptó sola al llegar el reloj a cero:** no lo hemos podido repetir. Puede que el Mac que alojaba la partida se durmiera. El reloj es de 30 s, o de 5 minutos si el otro jugador no tiene la ventana del juego a la vista.
- **Si vuelve a pasar, mandadnos red.log:** ahora apunta cuándo espera la sala y cuándo sigue adelante.

## Partidas guardadas

- **La campaña de la 0.11.6 sigue** en la 0.11.7, y también una misión guardada a medias.
- **Lo del principio de la campaña solo vale para las nuevas:** 2 soldados por jugador y las dos pistolas, el machete y la palanca. Lo demás, también para las de antes.
- **Las puertas tapiadas, los coches de policía y los maleteros** salen en las misiones nuevas. Una misión guardada a medias sigue con su mapa.
- **En la primera misión guardada a medias,** el coche de policía ya no se abre por delante: solo el maletero.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.11.7-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.11.7-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.11.7:** con la 0.11.6 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.11.6.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
