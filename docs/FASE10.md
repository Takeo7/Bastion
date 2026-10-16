# Fase 10 · Barras de experiencia

Lo pidió Arturo el 16/10, después de jugar la 0.3.1. Sustituye las habilidades por niveles y los talentos por parejas de la Fase 9 ([FASE9.md](FASE9.md)). Se queda lo demás de la Fase 9: el cinturón, las armas con estado y mejoras, la Armería y la escaramuza.

Campaña nueva (Arturo): una campaña de antes se guarda aparte, como en la 0.3.

Las cifras son el punto de partida y se ajustan jugando.

## 1. Tres cosas distintas

- **Estadísticas:** puntería, puntería cuerpo a cuerpo, alcance de lanzamiento, curación, arreglar, salud, movimiento, acciones, defensa y armadura.
- **Experiencia:** seis barras: Disparo, Cuerpo a cuerpo, Lanzar, Curar, Investigar y Forma física.
- **Acciones:** lo que se hace en combate. Cada una llena la barra de su línea.

## 2. Las barras

- No hay niveles por barra: solo una barra que se llena.
- Al llenarse vuelve a cero y:
  - da **un punto de habilidad**, para el tablero hexagonal;
  - sube entre un **5 y un 10 %**, al azar, la estadística de esa barra.
- Cada vez cuesta más llenarla: 10, 15, 20, 25… (5 más cada vez).

| Barra | Sube su estadística |
|---|---|
| Disparo | puntería con armas de fuego |
| Cuerpo a cuerpo | puntería cuerpo a cuerpo |
| Lanzar | alcance de lanzamiento (piedras y granadas) |
| Curar | lo que curan el botiquín y las medicinas |
| Investigar | arreglar (equipos, radios, alarmas) |
| Forma física | salud |

El porcentaje se acumula sobre la estadística de base y se redondea al usarla. Una salud de 5 con un +23 % es 6.

## 3. Lo que llena cada barra

| Acción | Barra | Experiencia |
|---|---|---|
| Disparar (también en vigilancia) | Disparo | 1 por disparo, +1 si da, +1 si abate |
| Atacar cuerpo a cuerpo (también en vigilancia) | Cuerpo a cuerpo | 1 por golpe, +1 si da, +1 si abate |
| Lanzar (una piedra, una granada, humo…) | Lanzar | 1 por lanzamiento, +1 si alcanza a alguien |
| Curar con el botiquín o con medicinas | Curar | 2 a otro, 1 a uno mismo |
| Investigar (una caja o una habitación) | Investigar | 2 |
| Arreglar un equipo o callar una alarma | Investigar | 3 |
| Agazaparse | Forma física | 1 |
| Correr (moverse con las dos acciones) | Forma física | 1 |

Se cuenta en la misión y se suma al volver vivo. No hay tope por misión.

## 4. Las acciones nuevas

- **Lanzar** es una acción con menú: al pulsarla, encima se elige qué lanzar.
  - **Piedra:** siempre hay. Suena donde cae y los zombis van a mirar. Si le da a alguien, 1 de daño. No acaba el turno.
  - **Granada, humo, aturdidora:** si se llevan en el cinturón.
- **Vigilancia** se elige al pulsarla:
  - **a distancia**, como hasta ahora;
  - **cuerpo a cuerpo:** golpea con la hoja al primer enemigo que se le ponga al lado.
- **Investigar:** por defecto, la habitación. Si hay una caja al lado, se elige entre la caja y la habitación.

## 5. Veteranía

La veteranía sube con los **puntos gastados en el tablero**: un rango cada 5 (Arturo, 16/10). Los puntos sin gastar no cuentan.

| Puntos gastados | Rango | Voluntad |
|---|---|---|
| 0–4 | Novato/a | 40 |
| 5–9 | Fogueado/a | 50 |
| 10–14 | Curtido/a | 60 |
| 15–19 | Veterano/a | 70 |
| 20 o más | Superviviente | 80 |

- Cada rango da **+10 de voluntad**.
- Desde Veterano, una **tercera acción** por turno.
- El rango no da nada más (antes sumaba al arreglar; ya no).

## 6. El tablero hexagonal

Lo pasó Arturo el 16/10. Cada soldado tiene el suyo; los puntos se gastan en él.

### Forma

- **169 celdas hexagonales:** el núcleo en el centro y 168 habilidades en siete anillos (6, 12, 18… 42 celdas).
- **Seis sectores de 28**, uno por barra, en este orden alrededor del núcleo: Disparo, Cuerpo a cuerpo, Lanzar, Curar, Investigar y Forma física. Cada familia, su color; el núcleo, su identidad propia.
- Celdas juntas, con una separación estrecha y uniforme.
- **Marco circular de líneas:** círculos concéntricos, líneas radiales y marcas de orientación alrededor del árbol, con el acabado del juego.

### Reglas (motor)

- El núcleo empieza desbloqueado.
- Una habilidad se compra con **un punto** y solo si está **al lado** de otra desbloqueada. No pide nada más (Arturo, 16/10): un punto de cualquier barra sirve para cualquier sector.
- Los puntos: uno por barra llenada, menos las celdas compradas.
- Lo comprado es para siempre.
- Tres clases de habilidad:
  - **mejora:** una cifra (+3 de puntería, +1 de salud…);
  - **pasiva:** una regla (Uno tras otro, Fuego de cobertura…);
  - **activa:** una acción nueva (A la cabeza, Ráfaga…).
- Dentro de cada sector, las activas y las pasivas fuertes van por el radio del sector y hacia fuera.

### Estados en la vista

- **Desbloqueada:** color de su familia e icono nítido.
- **Disponible** (al lado de una desbloqueada, aunque aún no haya punto para pagarla): apagada, casi sin color, icono gris.
- **Bloqueada:** mucho más atenuada; la celda se ve, el icono no.

### Cómo se maneja

- **Consultar:**
  - al pasar el cursor, la ficha con icono, nombre, categoría, descripción, efecto y requisitos, con la interfaz del juego;
  - consultar no compra ni cambia cómo se ve la celda;
  - al retirar el cursor se queda la última ficha.
- **Desbloquear:**
  - manteniendo pulsada la celda un segundo; un indicador recorre su contorno;
  - un clic corto solo consulta.
- **Cancelar:** soltar antes, salir de la celda, empezar a arrastrar, Escape o perder el foco. Nunca se desbloquea sin querer.
- **Al completarse:** un rebote pequeño y un pulso breve por el contorno; luego queda como adquirida y se actualizan las vecinas disponibles.
- **Ola:** las celdas cercanas al puntero se elevan según la distancia; cara e icono suben juntos. Al salir del árbol, vuelven a reposo despacio.
- **Navegar:**
  - arrastrar para desplazarse;
  - la rueda acerca o aleja hacia el cursor;
  - se abre con el árbol entero encuadrado, con límites de desplazamiento y de zoom.
- **Accesibilidad:**
  - animaciones suaves que respetan el movimiento reducido;
  - las flechas consultan; mantener Enter o Espacio desbloquea;
  - en pantalla táctil, tocar consulta y mantener pulsado desbloquea.

### Para el código

- `board.ts`:
  - `BOARD` y `BOARD_CELLS`: las celdas, con `q`, `r` (axiales), `ring`, `sector`, `line`, `kind`, `name`, `effect` y lo que dan;
  - `boardNeighbors`, `withinReach`, `CELL_KINDS`.
- `skills.ts`:
  - `cellState(board, id)`: `unlocked`, `open` o `locked`;
  - `unlockBlocker(skills, board, id)`: por qué no se puede comprar, o `null`;
  - `pointsLeft(skills, board)`.
- Orden de campaña: `{ type: 'unlock', soldier, cell }`. El soldado guarda `board` (lo comprado), `skills` (experiencia) y `growth` (el porcentaje ganado en cada línea).

## 7. Quién hace qué

- **Gameplay:**
  - el motor: barras, experiencia, estadísticas, la piedra, la vigilancia cuerpo a cuerpo y la campaña nueva;
  - la escaramuza: veteranos con barras llenadas.
- **UI:**
  - la ficha (seis barras, el porcentaje ganado en cada estadística y los puntos);
  - el tablero hexagonal entero (§6), con un icono propio para cada habilidad;
  - las acciones con menú (Lanzar, Vigilancia, Investigar);
  - el parte («+5 de Disparo · barra llena: +7 % de puntería»).
- **Lore:** nombres y líneas de las barras, de la piedra, de la vigilancia cuerpo a cuerpo y del parte; los nombres de las 168 habilidades y su descripción.
- **Arte:** lanzar una piedra (el gesto y la piedra) y el golpe de vigilancia cuerpo a cuerpo.
