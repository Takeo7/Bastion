# Modo en tiempo real (propuesta)

**Estado:** hablado con Arturo el 20/10. No hay código. Gameplay lo reparte cuando Arturo quiera empezar.

La misión se puede jugar en tiempo real en lugar de por turnos, con los mismos controles de hoy: se elige un soldado, se pincha una casilla y va; se pincha un zombi y dispara. Todo pasa a la vez y nadie espera su turno. Es táctica en tiempo real, como Commandos o Door Kickers, con una pausa para pensar.

(Antes se habló de un modo con WASD y apuntar con el ratón; Arturo prefirió este, el 20/10.)

## 1. Lo que decidió Arturo (20/10)

- **Un interruptor al preparar la misión,** para los dos jugadores y a voto, como todo lo de la base. No cambia a mitad de misión.
- **La misma cámara y los mismos controles:** clic en una casilla para ir, clic en un enemigo para atacar, la barra de habilidades y Señalar.
- **Cada uno manda a todos sus soldados,** como en un juego de estrategia. Los que no reciben órdenes siguen la última y se defienden solos.
- **Los disparos aciertan con los números de hoy:** puntería, distancia, cobertura, altura y noche. El tablero y el equilibrio siguen valiendo.
- **Pausa con la barra espaciadora.** Se para, se dan órdenes y se sigue. A dos, la pide uno y se para para los dos.
- **Cuenta igual en la campaña:** experiencia, heridas, botín y muerte permanente.
- **Si cierra el anfitrión a mitad,** la misión se repite desde el principio, sin consecuencias en la campaña.

## 2. Cómo funciona

- **Las acciones cuestan tiempo, no puntos de acción:**
  - andar una casilla dura lo de hoy (al trote 0,5 s; corriendo 0,24 s; el zombi arrastrándose 1,02 s);
  - disparar es apuntar un momento, según el arma, y tirar; cada disparo tira su porcentaje;
  - recargar, registrar, curar y las habilidades tienen su duración y su tiempo de recarga.
- **Un soldado sin órdenes:** termina lo que hacía y se defiende. Dispara a lo que tenga a tiro y se queda donde está.
- **La vigilancia pasa a ser una postura:** «disparad a lo que se acerque», que se pone y se quita. Ya no es una orden de fin de turno.
- **Los zombis deciden sin parar,** no por turno: persiguen, rodean, muerden a su ritmo y se aprietan en las puertas sin atravesarse.
- **El tiempo de la misión se mide en minutos.** Las hordas llegan por tiempo y con el ruido.
- **La experiencia** se cuenta igual que hoy: disparar, acertar, curar, registrar.

## 3. Qué se aprovecha

Casi todo:
- **Los mapas,** con sus proporciones nuevas ([MAPAS.md](MAPAS.md)), y las rutas por casillas.
- **El movimiento:** el bloqueo entre personajes y los tramos de escalera.
- **El combate:** la cobertura por casilla, la puntería con todos sus factores, las armas y los zombis.
- **Lo que se ve y se oye:** la niebla, la vista en cono, la noche y el ruido.
- **El resto:** el botín, la mochila, los objetivos, La Lata y la campaña.
- **La red:** el cliente sigue mandando órdenes y el anfitrión las ejecuta. No hace falta predecir ni corregir retardos.
- **El motor puede seguir siendo determinista,** avanzando a pasos fijos de tiempo. Hoy no hace falta para guardar a medias, porque la misión se repite desde el principio.

## 4. Qué es nuevo

1. **El reloj en el anfitrión.** Avanza a pasos fijos, unas 10 veces por segundo, y ejecuta a la vez las órdenes de todos los soldados y de los zombis. Cada orden se valida en cualquier momento, no por turno.
2. **La pantalla.** Hoy reproduce los eventos de uno en uno («cada acción se ve entera»). En este modo muestra a todos moviéndose y disparando a la vez. El plan de pasos de los grupos de zombis (`walkPlan.ts`) ya hace parte de eso.
3. **El mando de varios soldados:**
   - seleccionar con clic o con las teclas de número;
   - el rango de movimiento se queda sin puntos de acción: muestra la ruta, no lo que alcanza un turno;
   - quizá encadenar órdenes con Mayúsculas (por decidir).
4. **La pausa compartida** y lo que se ve mientras está parado: las órdenes que esperan y las rutas.
5. **El HUD:** la barra de turnos sobra. Hacen falta los tiempos de recarga, quién está haciendo qué y el reloj de la misión.
6. **El ritmo y la dificultad:** las velocidades, los tiempos de cada acción, el tamaño de las hordas y el tiempo de cada misión.

## 5. Las normas del proyecto en este modo

- **«Cada acción se ve entera»:** no vale en tiempo real; la pausa es la manera de pensar. Sigue valiendo por turnos.
- **«La cámara nunca pelea con el jugador»:** sí vale. La cámara no se mueve sola a cada acción como en el turno enemigo; se puede seguir a un soldado si se pide.
- **Rendimiento:** la misma cámara y los mismos mapas. El reloj va en el anfitrión. El presupuesto de 4,2 ms por fotograma no cambia.

## 6. Pasos

Cada paso se juega y se prueba solo.

1. **Prototipo:** escaramuza en solitario. El reloj, clic para mover y disparar, los zombis persiguiendo y la pausa. Sirve para juzgar el ritmo y si se entiende lo que pasa. Si no convence, se para aquí.
2. **Todas las acciones con su tiempo:** recargar, habilidades con recarga, la postura de vigilancia y la defensa sola de los soldados sin órdenes.
3. **Cooperativo a dos,** con la pausa compartida.
4. **El resto de la misión:** registrar, puertas, ventanas, escaleras, objetivos y evacuar en La Lata.
5. **La campaña:** el interruptor a voto al preparar la misión, la experiencia, las heridas, el botín y el equilibrio.

## 7. Riesgos

- **Dos juegos en uno.** Cada cosa nueva tendrá que funcionar en los dos modos, o declararse solo de uno.
- **Demasiado a la vez:** seis u ocho soldados en tiempo real pueden abrumar. La pausa lo alivia, y la defensa sola de los que no tienen órdenes tiene que ser buena.
- **Que se entienda lo que pasa:** con todo a la vez, la pantalla tiene que dejar claro quién dispara a quién y qué ha pasado (avisos, sonido, marcas).
- **El equilibrio con muerte permanente:** los tiempos y las velocidades deciden si es justo.

## 8. Por decidir

- El tiempo de cada tipo de misión.
- Encadenar órdenes con Mayúsculas.
- Si los soldados sin órdenes también se cubren solos o solo disparan.
- Qué habilidades del tablero cambian en tiempo real (las de vigilancia, por ejemplo).
- Si la velocidad del juego se puede ajustar (más lento o más rápido), como hoy la del turno enemigo.
