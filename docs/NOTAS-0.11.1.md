# Notas de la 0.11.1

Texto de Lore para la 0.11.1, con las notas de Ana del 31/10 ([PRUEBA-2026-10-31.md](PRUEBA-2026-10-31.md); la #8 no entra). Es para Arturo y Ana: cuenta qué cambia al jugar, no cómo está hecho. Lo que va entre [corchetes] lo completa Gameplay al publicar: el commit. Desde «Título» hasta el final es el texto de la release, con el formato de [NOTAS-UNITY.md](NOTAS-UNITY.md).

---

**Título:** Bastión 0.11.1: las casas sin descubrir y el turno enemigo más corto

Versión 0.11.1 (commit [commit]). Las notas de Ana del 31/10, con la 0.11.0.

## Novedades de la 0.11.1

### Las casas sin descubrir
- **Dentro de una casa sin descubrir no se ve a los enemigos** ni se les puede disparar, aunque ellos sí vean a la escuadra.
- **Una casa se descubre al entrar.** Pisar el umbral ya cuenta; subir a su tejado, no.
- **También al asomarse a una ventana:** el soldado tiene que pararse en la casilla de fuera, justo delante de la ventana. Entonces se ve la habitación de detrás, con lo que haya dentro. Pasar por delante sin pararse no cuenta, y desde más lejos sigue en negro.
- **Quien está en una puerta sí se ve.**
- **En el asedio no se esconde nada.**

### El turno enemigo
- **Los zombis andan todos a la vez,** y la cámara se aleja para que se vean todos.
- **Después atacan de uno en uno,** y cada ataque se ve entero: mordiscos, golpes y el grito del Chillón.
- **Es más corto:** un turno de 47 zombis pasa de 40 s a 11 s.

### La cámara
- **Menos inclinada:** a 45° en lugar de 52°, también en la base.
- **Se aleja un poco menos,** pero se ve igual de lejos.
- **Con la cámara al hombro** no salen el alcance, el camino ni las rutas planeadas, ni los propios ni los del compañero.

### En pantalla
- **La Escuadra se ve de frente,** con el vehículo entero detrás. También en el parte.
- **«Tus soldados» lleva el retrato de cada soldado,** también en la lista del compañero.
- **El nombre sobre los personajes** sale solo con el ratón encima.
- **Las banderas de los enemigos** llevan su vida y sus estados, sin el ojo ni la cobertura.
- **Las líneas del alcance y del camino** pasan por debajo del soldado.
- **Con el menú de armas abierto,** el aviso de «¡contacto!» sube y ya no tapa los botones.
- **Pulsar la tarjeta del objetivo** vuelve a abrir la ventana de la misión, también en una misión retomada.

## Partidas guardadas

- **La campaña de la 0.11.0 sigue** en la 0.11.1.
- **En una misión guardada a medias con la 0.11.0,** las casas en las que ya se había entrado vuelven a estar sin descubrir, hasta que un soldado entra o se asoma.

## Descargas

| Archivo | Para |
|---|---|
| `Bastion-0.11.1-mac.zip` | Mac con Apple Silicon o con Intel: es la misma app para los dos |
| `Bastion-0.11.1-win-x64.zip` | Windows: descomprimir y abrir `Bastion.exe` |

## Avisos

- **Los dos jugadores, con la 0.11.1:** con la 0.11.0 no se puede jugar juntos.
- **En el Mac,** al copiarla a Aplicaciones sustituye a la 0.11.0.
- **Solo Mac y Windows:** no hay versión para Linux, ni para el navegador ni para el móvil.
- **Firma:**
  - Mac: firma ad hoc. La primera vez que se abre cada versión, Ajustes del Sistema → Privacidad y seguridad → «Abrir igualmente».
  - Windows: sin firmar. «Más información» → «Ejecutar de todas formas».
- **Cortafuegos del Mac:** con cada versión, el Mac vuelve a preguntar si la app puede aceptar conexiones entrantes y si puede buscar en la red local: «Permitir» en los dos.
- **Windows:** construida desde el Mac; aún no se ha probado en un PC.
- **Para jugar,** mejor enchufado y sin modo de bajo consumo.
