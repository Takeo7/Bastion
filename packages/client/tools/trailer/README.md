# Tráiler: captura, música y montaje

Las herramientas de Red para el tráiler ([docs/TRAILER.md](../../../../docs/TRAILER.md)). Todo lo que sale va a `.dev-snapshots/trailer/`, fuera del repositorio; las herramientas y las tomas sí se suben, para poder rehacerlo.

## Las piezas

- `trailer.html` → `src/lab/trailer/main.ts`: la página. Las tomas de Arte (`shots/<id>.ts`) dibujan en `#escena`; la capa de UI (`motion.ts`) pinta encima; los tiempos están en `script.ts`.
- `src/lab/trailer/clock.ts`: el reloj virtual. `performance.now`, `requestAnimationFrame`, los temporizadores y `document.hidden` siguen un reloj que en la captura avanza exactamente 1/60 s por fotograma. Por eso sale perfecto aunque el Mac no llegue a 60.
- `src/lab/trailer/capture.ts`: compone cada fotograma, lo manda al receptor y apunta los sonidos del juego con su tiempo para la pista de cada toma.
- `src/lab/trailer/music.ts`: la música, hecha por código con los bloques de los sonidos del juego (`audio/synth.ts`).
- `receiver.ts`: sirve una copia fija de la página y pasa los fotogramas a ffmpeg (`node:http`, sin dependencias).
- `montage.ts`: junta las tomas, la música y los sonidos.
- `escaparate.ts` (de Gameplay): las partidas de muestra para las capturas; `npx tsx packages/client/tools/trailer/escaparate.ts <carpeta> [tokenAna] [tokenArturo]` escribe `base.json` y `mision.json`, para arrancar un servidor con `--save`.

## Cómo se hace

1. Arrancar el receptor, que compila la página y la sirve en el 3392:

   ```bash
   npx tsx packages/client/tools/trailer/receiver.ts --build
   ```

2. Grabar, abriendo en el navegador:
   - `http://localhost:3392/trailer.html?toma=<id>`: una toma, que sale en `tomas/<id>.mp4` y `tomas/<id>.wav`;
   - `?todas`: todas las del guion que existan, una tras otra;
   - `?todas&vacias`: también las que aún no existen, en negro bajo la capa de UI (la animática). Van a `vacias/<id>.mp4`, nunca a `tomas/`;
   - `?musica`: la música entera, que sale en `musica/musica.wav`; con `?musica=30`, solo los primeros 30 s (la maqueta).

3. Montar:

   ```bash
   npx tsx packages/client/tools/trailer/montage.ts
   ```

   Sale `trailer.mp4`: 1920×1080, 60 fps, H.264 y AAC a −14 LUFS. Con `--borrador` codifica rápido, para mirar los cortes. Para cada hueco del guion usa `tomas/<id>.mp4` si existe, si no `vacias/<id>.mp4`, y si no, negro.

### Otros parámetros de la página

| Parámetro | Para qué |
|---|---|
| `&ver` | Ver la toma en vivo, una vez (R la repite). Vale también en el vite de cada uno, sin receptor |
| `&sincapa` | La toma sola, sin la capa de UI |
| `&ss=2` | Dibuja a 3840×2160 y reduce: bordes más finos, cuatro veces más trabajo |
| `&semilla=N` | La semilla de `Math.random`: la misma toma sale igual |
| `&alfa` | Un webm con transparencia en lugar del mp4 (el logo suelto de UI) |
| `&debug` | `window.__trailer` con el reloj y los ajustes gráficos, para medir |

## Lo que hay que saber

- **Los fotogramas van por WebSocket**, sin comprimir (8 MB cada uno). Por POST, Chromium los sube a unos 23 MB/s, unos 360 ms por fotograma. Como Blob van rápidos, pero llenan el almacén de Blobs del navegador (unos 2 GB) y, a partir de ahí, cualquier petición falla hasta que se vacía. Medido el 15/10: unos 17 fotogramas por segundo con la toma de prueba, unos 5 minutos el tráiler entero.
- **Se graba desde una copia fija** (`--build`): si otra sesión guarda un archivo, el vite de desarrollo recarga la página, y en mitad de una toma la cortaría.
- **El panel del navegador puede estar oculto**: el reloj libre late con mensajes (`MessageChannel`), no con temporizadores, que Chromium frena en una página oculta.
- **Sombras**: el Renderer busca los objetos con sombra cada 30 fotogramas, así que antes de grabar se dibujan 40 fotogramas con el tiempo parado. En cada fotograma se llama a `shadowsChanged()`: en el juego las sombras de las poses se rehacen a 10 Hz.
- **Los ajustes de la página son suyos**: `clock.ts` le pone `perfil=trailer`. Forzar «alto» no cambia los del juego servido en la misma dirección.
- `shots/prueba.ts` es la prueba de Red (12 zombis y sonidos en tiempos conocidos), no una toma del tráiler.
