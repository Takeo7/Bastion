# El tráiler y lo que acompaña al juego

Lo pidió Arturo el 15/10: un tráiler de motion graphics de 60–90 s, en español y con música nuestra. Además, la portada del juego en los tamaños de Steam, 8–10 capturas buenas y el logo animado por separado.

## Qué se entrega

| Qué | Formato | Quién |
|---|---|---|
| Tráiler | 1920×1080, 60 fps, H.264 (mp4), unos 75 s, con música y sonidos | Todos; monta Red |
| Logo animado | El mismo de la última toma, suelto: mp4 sobre negro y webm con transparencia | UI |
| Portada | Key art a 3840×2160, y los tamaños de Steam: 920×430, 462×174, 1232×706, 748×896, 600×900, 3840×1240 (fondo de biblioteca, sin logo) y 1280×720 (logo con transparencia) | Arte (la imagen), UI (el logo encima) |
| Capturas | 8–10 a 1920×1080, del juego de verdad, con la interfaz del juego y sin nada de prueba | Arte (combate), UI (pantallas del Bastión) |

Todo sale a `.dev-snapshots/trailer/`, fuera del repositorio. Las herramientas y las tomas, en cambio, sí van al repositorio, para poder rehacerlo.

## Normas

- **Arte:** solo Synty POLYGON, Kenney (CC0) o lo hecho por nosotros. La licencia de Synty deja enseñar sus modelos en el juego y en sus vídeos, no repartirlos sueltos.
- **Tipografías:** las dos del juego (OFL), ninguna más.
- **Música y sonidos:** originales, hechos por código como los del juego, o de Kenney.
- **Opciones rechazadas:** no vuelven (vóxel, Expediente, Holotáctico, Viñeta, CRT…).
- **Imagen:** todo lo que se ve es el juego de verdad (sus mapas, sus modelos, su luz), con una cámara de cine. No hay nada pintado aparte.
- **Ritmo:** cada acción se ve entera, como en el juego.

## Guion (unos 75 s)

Los textos de pantalla son de Lore (15/10): cortos, secos y que se lean en dos segundos. Cuentan la premisa con las frases de la tarjeta del juego y venden lo que lo distingue: el ruido, la escasez y volver vivos.

| # | Tiempo | Imagen | Texto en pantalla |
|---|---|---|---|
| 1 | 0:00–0:06 | Negro, viento. Se abre en una calle vacía al amanecer, con la cámara baja; papeles y un coche con la puerta abierta. | «Hace dos inviernos, la Pandemia vació el valle.» |
| 2 | 0:06–0:13 | Por el fondo de la calle entra la horda: 45 zombis andando hacia la cámara, sin prisa. | «Los muertos siguen andando.» |
| 3 | 0:13–0:20 | Atalaya en la sierra: el muro, el patio, La Lata saliendo de la cochera. | «Atalaya aguanta.» |
| 4 | 0:20–0:24 | Golpe de música. Título intermedio. | «Táctica por turnos. Para dos.» |
| 5 | 0:24–0:44 | Montaje de combate, una acción por plano y cada una entera. | Tres rótulos cortos sobre la imagen, uno por tercio del montaje: «Cada disparo se oye.» (sobre el disparo de reacción o la granada), «Registrad lo que podáis.» (la habitación y la caja) y «Volved vivos.» (el salto por la ventana o la puerta con el zombi detrás) |
| 6 | 0:44–0:56 | La campaña: el valle con la Marea que avanza, el árbol de habilidades, la Armería y la mochila (pantallas del juego, con movimiento). | «Construid. Decidid los dos.» |
| 7 | 0:56–1:06 | El clímax: la horda llega a la zona de evacuación; uno pide evacuar, el otro acepta, y La Lata arranca. Uno se queda fuera. | «Quien se queda fuera, se pierde.» |
| 8 | 1:06–1:15 | El logo de BASTIÓN entra con su animación; debajo, el lema y «Próximamente». | «BASTIÓN» · «Supervivencia táctica para dos» · «Próximamente» |

Los planos del montaje (5), unos dos segundos cada uno:
- la escuadra en vigilancia en un tejado y un disparo de reacción;
- el machete (Tajo) contra un zombi que se cae de verdad;
- una granada que rompe una pared;
- un salto por una ventana;
- una puerta que se abre y un zombi detrás;
- registrar una habitación; la caja de armas que se abre;
- el Chillón gritando y el Hinchado reventando;
- de noche, con la linterna y la luz de las farolas;
- el área de servicio del bosque y el polígono, en un plano largo cada uno.

### Por qué estos textos

- **1 y 2** son las dos primeras frases de la premisa del juego (`PREMISE` en `lore.ts`): el tráiler empieza como empieza la campaña.
- **3, «Atalaya aguanta»:** la premisa dice «aguanta», no «resiste». Es más seco y más verdad: Atalaya no gana, dura.
- **4** dice el género y que es para dos, sin adjetivos.
- **5** cuenta lo que distingue al juego de un XCOM: el ruido llama a los muertos, lo que se trae es lo que se registra, y una misión se gana volviendo. «Cada bala cuenta» era un tópico; «Cada disparo se oye» es una regla.
- **6, «Decidid los dos»:** sin metáforas sobre el mando compartido (prueba del 14/10); dice lo que pasa.
- **7** es la regla de la evacuación tal cual: sobre la imagen de alguien que se queda fuera, no hace falta más.
- **El lema, «Supervivencia táctica para dos»,** dice el género y el modo en cuatro palabras. «Táctica por turnos cooperativa» ya sale en la toma 4.

## Ficha

Para la página de la tienda (Steam u otra), cuando haga falta.

**Corta** (unas 300 letras):

> Hace dos inviernos, la Pandemia vació el valle de Kessra. Dirigís Atalaya, una vieja estación militar en la sierra, y mandáis a sus equipos a por comida, medicinas y armas. Táctica por turnos para dos jugadores: cada disparo se oye, las balas se cuentan y quien se queda fuera del autobús, se pierde.

**Larga:**

> Hace dos inviernos, la Pandemia vació el valle de Kessra. Los muertos se levantaron y siguen andando. En la sierra, Atalaya, una vieja estación militar, aguanta con cuarenta supervivientes llegados de todas partes. Vosotros dos la dirigís.
>
> Bastión es táctica por turnos cooperativa para dos jugadores en la misma partida. Cada uno manda a sus soldados, y el mando se cede cuando hace falta. Registráis casas, abrís cajas, contáis las balas y medís el ruido: cada disparo atrae a más. Una misión se gana volviendo a La Lata, el autobús de Atalaya, y quien se queda fuera cuando arranca, se pierde.
>
> Entre misión y misión, levantáis Atalaya: el Huerto, el Taller, la Enfermería. Fabricáis las armas que no encontráis, curáis a los mordidos si llegan las medicinas y decidís juntos qué se hace con lo poco que hay. Mientras, desde los Llanos de Ceniza, sube la Marea.

## Cómo se hace

1. **Las tomas** (Arte): una página de laboratorio, `trailer.html`. Cada toma es un mapa o un estado del juego, una cámara con sus puntos clave (posición, objetivo, lente) y, en el combate, una lista de órdenes del motor que se juegan con el juego de verdad (`game.ts`). Las tomas viven en `packages/client/src/lab/trailer/`. Gameplay prepara los estados de partida que hagan falta.
2. **El motion graphics** (UI): los rótulos, las transiciones, las pantallas del Bastión con movimiento y el logo. Todo se dibuja en un canvas 2D encima de la imagen 3D y en la misma página, nada en el DOM, para que la captura lo recoja.
3. **La captura** (Red):
   - un reloj virtual: `performance.now` y `requestAnimationFrame` avanzan 1/60 s por fotograma, lo que tarde en dibujarse; las animaciones del juego van por tiempo, así que el resultado sale perfecto aunque el Mac no llegue a 60;
   - cada fotograma se manda como imagen a un receptor local sin dependencias (`node:http`), que lo pasa a ffmpeg.
4. **La música** (Red): una pieza original de unos 75 s, compuesta por código y renderizada a WAV, con estos momentos:
   - tensión al principio;
   - el golpe en el título intermedio (0:20);
   - pulso en el montaje;
   - subida en el clímax;
   - el acento final en el logo.
   Los sonidos del juego (disparos, gritos, el motor de La Lata) se mezclan encima.
5. **El montaje** (Red): ffmpeg junta las tomas, la música y los sonidos, con los tiempos de este guion. Gameplay revisa y se lo enseña a Arturo.

## Orden

1. **Primero, en paralelo:**
   - Lore escribe los textos;
   - UI hace el logo animado y el estilo de los rótulos;
   - Red hace la captura y una primera música;
   - Arte termina el valle nuevo (0.2.7), que sale en la toma 6.
2. **Después:**
   - Arte hace las tomas, la portada y las capturas de combate;
   - UI, las pantallas del Bastión.
3. **Al final:** el montaje, la revisión con Arturo y los retoques.
