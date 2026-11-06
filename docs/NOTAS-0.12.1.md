# Notas de la 0.12.1

Texto de Lore para la 0.12.1, con lo que Arturo cambió el 6/11, después de publicar la 0.12.0 ([PRUEBA-2026-11-05-ARTURO.md](PRUEBA-2026-11-05-ARTURO.md), la sección final): la regla de correr (en la 0.12.0, pasar de 5 puntos en el turno; ahora, gastarlo todo de una vez, `Paths.IsRun`) y Forma física, que sube la esquiva y no la salud, y se llena también al recibir daño y con las rozaduras ([FASE10.md](FASE10.md) §2 y §3). Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.12.1: correr de una vez, y Forma física sube la esquiva

Versión 0.12.1 (commit [commit]). Dos cambios en las reglas, decididos por Arturo el 6/11: correr y Forma física.

## Novedades de la 0.12.1

### Correr, de una vez
- **Correr es gastar todo el movimiento de una vez, en un solo tramo:** los 8 puntos, o los que dé el tablero. Solo ese tramo se hace corriendo y se oye.
- **Repartido en tramos, ya no se corre:** mover 5, disparar y mover 3 no se oye. En la 0.12.0, pasar de 5 puntos en el turno era correr.
- **Si queda al menos una casilla, tampoco se corre:** mover 7 de una vez no se oye.
- **El aviso «Ruido 3: corre» sale solo** al elegir una casilla a la que se llega gastándolo todo de una vez.

### Forma física sube la esquiva, no la salud
- **Cada barra de Forma física da de +3 a +5 de esquiva.** Por el redondeo, alguna da 2 o 6.
- **La esquiva dice, de cada 100 impactos que recibe un soldado, cuántos se quedan en rozadura.**
- **La salud sube solo con el tablero:** la rama de Forma física (Fondo, Pulmones, Robusto, Correoso, Fondista y Hasta el final, +1 cada una; De hierro, +2; Corpulento, +3). Esa rama no cambia.
- **La esquiva se ve en la hoja del soldado,** con su icono, y en la hoja del combate (H). En la fila de Forma física, lo que han dado sus barras sale en puntos de esquiva («+8»), no en %.
- **Al llenar la barra, el registro y el parte dicen la esquiva que da:** «Ana llena una barra de Forma física: +4 de esquiva.»

### Qué llena Forma física
- **Recibir daño:** 1 por cada golpe que hace daño.
- **Una rozadura:** 1 por cada impacto que la esquiva deja en rozadura. Como la rozadura también hace daño, cuenta 2.
- **Como antes, cubrirse y gastar todo el movimiento del turno,** de una vez o tras otros tramos: mover 8 de una vez, o mover 5, disparar y mover 3.
- **En el parte, cada cosa por su nombre:** «+3 de Forma física: una herida y una rozadura». Gastar todo el movimiento se llama ahora «movimiento completo», no «carrera», porque no siempre es correr.

### En el tablero
- **«Sin roces» se llama ahora «Sin rozaduras»,** como en el combate. Hace lo mismo: con armas de fuego, un impacto nunca se queda en rozadura.

## Partidas guardadas

- **La campaña de la 0.12.0 sigue** en la 0.12.1, y también una misión guardada a medias. Lo que cambia en los soldados, en Avisos.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.12.1-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.12.1-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los soldados que tenían salud de las barras de Forma física la pierden,** y a cambio ganan su esquiva. La salud del tablero se queda.
- **Los dos jugadores, con la 0.12.1:** con la 0.12.0 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.12.0.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Por internet:** con algunas compañías, aunque salga la dirección de internet, desde fuera no se llega, y quien se une ve «No responde nadie…». Entonces, Tailscale.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
