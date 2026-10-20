# Modo en tiempo real (propuesta)

**Estado:** hablado con Arturo el 20/10. No hay código. Gameplay lo reparte cuando Arturo quiera empezar.

La misión se puede jugar en tiempo real en lugar de por turnos. Es un shooter cenital cooperativo, como Alien Swarm, Helldivers (el primero) o Dead Nation: la misma cámara desde arriba, tú llevas a un soldado con el teclado y apuntas con el ratón, y el resto de la escuadra te sigue con su IA.

## 1. Lo que decidió Arturo (20/10)

- **Un interruptor al preparar la misión,** para los dos jugadores y a voto, como todo lo de la base. No cambia a mitad de misión.
- **La misma cámara desde arriba.** Sigue al soldado que llevas.
- **WASD para moverse y el ratón para apuntar.** Se dispara hacia el cursor: acierta quien apunta bien.
- **La puntería del soldado sigue contando,** como dispersión y retroceso del arma. Con más puntería, las balas se abren menos y el arma se recupera antes.
- **Tab cambia de soldado.** El que dejas pasa a la IA.
- **Los compañeros con IA** te siguen, se cubren y disparan solos. Con teclas se les dan órdenes sencillas: «aguantad aquí», «seguidme» y «centrad el fuego en ese» (la marca de Señalar).
- **Sin pausa.**
- **Cuenta igual en la campaña:** experiencia, heridas, botín y muerte permanente.
- **Si cierra el anfitrión a mitad,** la misión se repite desde el principio, sin consecuencias en la campaña.

## 2. Qué se aprovecha

- Los mapas, con sus proporciones nuevas ([MAPAS.md](MAPAS.md)), el arte, las luces, la noche y la cámara.
- La niebla y la vista en cono de los enemigos: se recalculan sin parar en vez de por turno.
- El ruido: los disparos atraen a los muertos igual.
- Las armas, con su daño, cargador, ruido y alcance, y los zombis, con su vida, velocidad y mordisco.
- El botín, la mochila y el cinturón.
- Los objetivos y La Lata.
- La campaña entera.
- Las colisiones de hoy: nadie atraviesa a nadie y los muebles ocupan su casilla.

## 3. Qué cambia

- **Movimiento libre.** La cuadrícula pasa a ser el mapa de colisiones:
  - paredes, cobertura y muebles son bloques;
  - los personajes son círculos de unas 0,3 casillas que chocan entre sí;
  - las puertas se abren al llegar;
  - los tramos de escalera son rampas;
  - las ventanas se saltan con una tecla.
- **Disparar.** La bala sale hacia el cursor, con la dispersión del arma y del soldado.
  - La cobertura es física: una caja baja entre tú y el zombi para balas a ras de suelo.
  - Desde una planta alta se dispara por encima.
- **Habilidades.** La barra 1–8 se queda, con tiempos de recarga en vez de acciones: granada, Tajo, curar, Al cinturón. La vigilancia como orden desaparece: en tiempo real ya disparas tú.
- **El tiempo.** Las misiones se miden en minutos, no en turnos. Las hordas llegan por tiempo y con el ruido.
- **La experiencia.** Las barras se llenan con lo mismo que hoy (disparar, acertar, curar, registrar), contado en tiempo real.

## 4. Qué es nuevo

1. **La simulación en tiempo real,** en el anfitrión, a unas 20–30 veces por segundo. Va aparte del motor por turnos y usa los mismos datos de contenido (armas, zombis, objetos).
2. **El control y la animación:**
   - las piernas van en la dirección de WASD y el tronco apunta al ratón;
   - el pack BaseLocomotion de Synty trae los pasos en 8 direcciones (andar, correr y agachado);
   - hacen falta el retroceso, la recarga y los impactos.
3. **La IA de los compañeros:**
   - seguir sin estorbar;
   - cubrirse mirando hacia la amenaza;
   - disparar a quien tienen a tiro;
   - no herir a los propios;
   - obedecer las tres órdenes.

   De ella depende que el modo sea divertido.
4. **La IA de los zombis en tiempo real:** perseguir, rodear, morder a su ritmo y apretarse en las puertas sin atascarse.
5. **La red:**
   - el movimiento propio se ve al momento en tu pantalla y el anfitrión lo corrige;
   - entre amigos, cada uno decide sus propios disparos, lo que simplifica mucho;
   - si se cae el compañero, su soldado pasa a la IA hasta que vuelve.
6. **La pantalla.** Hoy reproduce los eventos de uno en uno. En este modo muestra a todos moviéndose y disparando a la vez, interpolando lo que llega del anfitrión.
7. **El HUD:** munición y recarga, la salud de la escuadra, la orden activa y una retícula.
8. **El ritmo y la dificultad:** el tamaño de las hordas, la munición y el tiempo de cada misión.

## 5. Las normas del proyecto en este modo

- **«Cada acción se ve entera»:** no vale en tiempo real. Sigue valiendo por turnos.
- **«La cámara nunca pelea con el jugador»:** sí vale. La cámara sigue al soldado con un poco de adelanto hacia el cursor, y nunca gira sola.
- **Rendimiento:** la misma cámara y los mismos mapas. La simulación va en el anfitrión y el cliente interpola. El presupuesto de 4,2 ms por fotograma no cambia.

## 6. Pasos

Cada paso se juega y se prueba solo.

1. **Prototipo:** escaramuza en solitario, un soldado con WASD y ratón contra zombis. Sirve para juzgar la sensación: respuesta del control, impactos, sonido y temblor de cámara. Si no convence, se para aquí.
2. **Tab y los compañeros** con IA y las tres órdenes.
3. **Cooperativo a dos.**
4. **El resto de la misión:** habilidades, objetos, registrar, puertas, ventanas, escaleras, objetivos y evacuar en La Lata.
5. **La campaña:** el interruptor a voto al preparar la misión, la experiencia, las heridas, el botín y el equilibrio.

## 7. Riesgos

- **Dos juegos en uno.** Cada cosa nueva tendrá que funcionar en los dos modos, o declararse solo de uno. Si el tiempo real gusta más, el táctico se puede quedar atrás.
- **La IA de los compañeros:** sin pausa, si no es buena, el modo frustra.
- **La sensación:** un shooter vive de la respuesta del control y de cómo se notan los impactos. Lo dice el paso 1.
- **El equilibrio con muerte permanente:** apuntar bien o mal decide quién vive.

## 8. Por decidir

- El tiempo de cada tipo de misión.
- El fuego amigo.
- Si la munición se cuenta igual que por turnos.
- Qué habilidades del tablero cambian en tiempo real (las de vigilancia, por ejemplo).
- Si se puede agachar, y si agachado se dispara mejor.
