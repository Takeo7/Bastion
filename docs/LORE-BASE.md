# La base nueva: textos (borrador de Lore)

Para la Fase 12 §11 ([FASE12.md](FASE12.md)). Ya está en `lore.ts` (sección «base»: `PIECE_TEXT`, `PIECE_STATE`, `BUILD_TEXT`, `siegeWarning`, `BASE_SIEGE_TEXT`), con los ids de `base.ts`. La historia, «Atalaya cayó», la aprobó Arturo el 18/10 ([LORE.md](LORE.md), «El Bastión»). Faltan los efectos con cifras de cada edificio, cuando el motor los cuente por misión.

Las normas de siempre:
- cada texto dice primero lo que hace y luego, si cabe, una línea del mundo;
- seco, sin chistes;
- nada que se llame como algo del botín (prueba del 18/10, #13).

## La caravana

- **La caravana**: «Literas para los primeros y el almacén debajo de los asientos. Todo lo demás está por levantar.»

## Defensas a mano

| Pieza | Texto |
|---|---|
| **Empalizada** | Troncos del pinar, afilados y clavados. Barata, y cede pronto. |
| **Muro** | Bloques y hormigón. Caro, pero aguanta. |
| **Puerta** | Se abre y se cierra con una acción. Cerrada, los muertos tienen que tirarla. |
| **Barricada de sacos** | Sacos de tierra apilados: cobertura para disparar detrás. |
| **Pila de troncos** | Lo talado, apilado: cobertura baja, y barata. |
| **Torre de vigía** | Una plataforma de troncos a una planta de altura: se ve más y se dispara desde arriba. |
| **Foco** | Alumbra a su alrededor: de noche, lo que tiene cerca se ve como de día. |
| **Alambre de espino** | Quien lo cruza va más despacio, vivo o muerto. |

**Pila de troncos** en vez de «Cajas»: las Cajas ya son las marcadas del botín (el glosario, «Cajas»), y una defensa con ese nombre volvería a confundir lo que Arturo pidió separar en el #13. Si se prefiere otra, «Palés».

## Defensas automáticas

| Pieza | Texto |
|---|---|
| **Mina** | Estalla una vez, cuando algo la pisa. Se repone con piezas. |
| **Foso con pinchos** | Hiere a quien cae dentro. Aguanta unas cuantas veces; luego se repone con piezas. |
| **Cepo** | Atrapa al primero que lo pisa y lo retiene. Se repone con piezas. |
| **Torreta** | Dispara sola, en el turno enemigo, a lo que se acerque. Sin munición se calla; se recarga con piezas. |

Líneas de estado:
- **sin munición:** «Sin munición: se ha callado.»;
- **gastada:** «Gastada: reponla con piezas.»;
- **rota:** «Rota: hay que repararla.»

## Edificios

Cada uno tiene tres niveles. Lo de «junto a…» pasa a ser estar al lado en el mapa. Las líneas del mundo de los niveles 2 y 3 son las de ahora, donde siguen valiendo.

| Edificio | Qué hace | Nivel 1 | Nivel 2 | Nivel 3 |
|---|---|---|---|---|
| **Sala de radio** | Avisa de los asedios (por dónde y cuántos) y trae misiones; absorbe el Centro de comunicaciones y la Estación de escucha. | Una radio de coche, una batería y Morse con los cascos. | Una antena en el pino más alto. | La antena llega hasta el mar: Morse oye antes lo que viene. |
| **Barracón** | Camas para soldados. | Literas de madera y una estufa que solo se enciende de noche. | Un segundo piso de literas. | Mantas de verdad. |
| **Enfermería** | Las heridas curan antes. | Una camilla y un hornillo para hervir el agua. | Agua caliente en la sala de curas. | Un quirófano de verdad, con lámpara y generador propio. |
| **Taller** | Accesorios y armas más baratos. | Un banco de trabajo y las herramientas de Teo. | Un torno de verdad. | Teo deja de quejarse. |
| **Huerto** | Raciones. | Bancales bajo plástico y bidones para el agua de lluvia. | Un invernadero de verdad, con estufa. | Riego por goteo y semillas de La Vega. |
| **Cuarentena** | Sin ella, un mordido no sube a La Lata. | Una nave aparte, con cerrojo por fuera. | [efecto] | [efecto] |
| **Camposanto** | Los caídos descansan aquí y salen de la lista de Soldados. | Cruces de pino bajo la nieve, una por cada uno. | (las de ahora) | (las de ahora) |
| **Campo de tiro** | Puntería para todos. | Dianas de cartón en una trinchera del pinar. | Siluetas de zombi a cincuenta metros. | Blancos móviles, con una polea de Teo. |
| **Centro de entrenamiento** | Más soldados por misión; reclutas mejores. | Un patio despejado y un horario. | Una semana de prácticas antes de salir. | Alguien que ya ha vuelto de fuera les enseña. |
| **Taller de oficios** | Las barras se llenan más en cada misión. | Lo que se aprende fuera, aquí se repasa. | Más herramientas, y Nwosu repasa las curas. | Gente de otros enclaves enseña lo suyo. |
| **Chapistería** | Las heridas curan antes; La Lata lleva camilla. | La Lata lleva camilla. | Mantas y un botiquín fijo atrás. | La Lata ya no para a repostar con heridos dentro. |
| **Sala de mapas** | Las misiones rinden más suministros: se sabe dónde buscar (ya no hay experiencia del Bastión). | Un tablero con los caminos del valle. | Cada casa registrada, marcada con una chincheta. | Las rutas de los Camioneros, y dónde paraban. |

Lo que se va:
- la **Brigada de obras**, porque construir es al momento;
- el **Centro de comunicaciones** y la **Estación de escucha**, que pasan a la Sala de radio.

## Construir (para UI)

- **Botones:** Construir · Talar · Reparar · Desmontar · Girar · Mejorar.
- **Talar** (cuesta, no da; Arturo, 18/10): «Talar: cuesta 3 suministros, y el sitio queda libre.»; junto al puntero, «Talar 12 árboles · 36 §».
- **Coste:** «Cuesta N § y N piezas». Sin bastante: «Faltan N §» / «Faltan N piezas».
- **Desmontar:** «Desmontar: se recupera la mitad de lo que costó, menos si está dañado.»
- **Sitio:** ocupado, «Ahí ya hay algo»; árbol, «Primero hay que talar».
- **Propuesta** (lo caro se vota): «Propuesta de Arturo: Muro, 15 § y 1 pieza».

## Avisos de asedio

Sin Sala de radio, solo cuándo:
- «Viene un asedio: llegará tras la próxima misión.»
- «Viene un asedio: llegará dentro de 3 misiones.»
- En la franja: «ASEDIO EN 3 MISIONES» / «ASEDIO TRAS LA PRÓXIMA».

Con Sala de radio, también por dónde y cuántos:
- «Por la radio: unos veinte, por el norte, dentro de 3 misiones.»
- Por varios lados: «…por el norte y el este…».
- Cuántos, redondeado («unos diez», «unos veinte», «más de cincuenta»): es lo que se oye por radio, no la cuenta exacta.

Cuando llega:
- **tarjeta:** «Asedio», con «Ya están aquí.»;
- **línea:** «Defienden los sanos. Los heridos y los mordidos se quedan dentro.»

La caída:
- **con supervivientes:** «El Bastión ha caído» + «Se pierde lo levantado y lo del almacén. Los que quedan huyen con la caravana y lo puesto, y vuelven a empezar en otro claro.» Delante va «Otra vez.», por Atalaya.
- **sin nadie:** «No queda nadie» + «El Bastión ha caído con todos dentro. La campaña termina aquí.»

Si se aguanta, se quedan los finales del asedio de prueba: «El Bastión aguanta: los que quedan se retiran» y «…no queda ninguno en pie».

## Tiempo en misiones

Lo que hoy dice días pasa a decir misiones:
- «tras la próxima misión» o «dentro de N misiones»;
- **heridas:** «Vuelve en 2 misiones».
- **capítulos:** «Nueva misión de historia tras la próxima misión».
Nunca «días».
