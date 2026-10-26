# El sonido en Unity

Lo lleva Red (hito 3, docs/UNITY.md §5). El objetivo es que suene igual que la 0.10.1.

## 1. De dónde sale

Todo se genera a partir del sonido de la 0.10.1, que está en git:
- `packages/client/src/audio/`: las recetas sintetizadas (`sounds.ts`, `synth.ts`, `bank.ts`), la partitura (`music.ts`) y las camas de ambiente (`ambience.ts`);
- `packages/client/public/audio/`: los grabados (Opus, con su `index.json`) y los WAV de la interfaz.

Lo generado va a `unity/Bastion/Assets/Bastion/Client/Audio/Generado/`, que **no está en git**: son unos 580 MB de WAV. Unity los comprime al compilar la app.

| Carpeta | Qué | Cómo |
|---|---|---|
| `sintetizados/` | las 110 recetas de `sounds.ts`, con todas sus variantes, y los bucles de las que lo son (la sirena) | renderizadas como `bank.ts`: misma semilla por variante, misma normalización; mono, 44,1 kHz |
| `vivos/` | la partitura de la base y los menús (`musica.base`) y del Mapa (`musica.geo`), 8 min cada una; las cinco camas de ambiente (ciudad, campo, nave, base, globo), 4 min cada una | `music.ts` y `ambience.ts` tal cual, en un OfflineAudioContext con su reloj y sus temporizadores simulados y `Math.random` con semilla. Se quita el fundido de entrada y el final se funde en el principio (6 s, igual potencia): bucles sin costura. Estéreo, 44,1 kHz |
| `grabados/` | los 12 grupos grabados, con su nivel por toma | de Opus a WAV con ffmpeg, sin tocar nada más; 48 kHz, las dos camas de combate en estéreo |
| `interfaz/` | los 8 sonidos de interfaz | copiados |
| `manifiesto.json` | qué es cada cosa (bus, nivel, reverb, bucle, variantes) y el sha256 de cada archivo | |

En combate la partitura no suena: la sustituyen las camas grabadas, como en la 0.10.1. Por eso solo se renderizan `base` y `geo`.

## 2. Generarlo

```bash
unity/tools/sonido.sh
```

- Necesita Node, el `node_modules` de la raíz (esbuild y el Electron de `packages/desktop`) y el ffmpeg de Homebrew. No descarga nada.
- Tarda unos 40 s en el M2 Pro.
- `--salida <carpeta>` lo deja en otra carpeta, y `--comprobar` lo genera otra vez en una carpeta temporal y lo compara con la de ahora.
- La primera importación en Unity es larga (cientos de MB): se avisa a los demás antes, y se hace con el cerrojo de `unity/tools/unity.sh`.

## 3. Reproducible, con un bit de margen

- Los grabados y los de interfaz salen idénticos cada vez: `--comprobar` los compara byte a byte.
- Lo que renderiza Chromium (las recetas, la partitura y las camas) varía como mucho **1 LSB** (−90 dBFS) de una pasada a otra, en un tercio de los archivos. Es el redondeo de su render offline, no azar: no se puede evitar desde fuera. La 0.10.1 ya era así: cada partida renderizaba su banco con estas mismas diferencias. `--comprobar` compara esos archivos muestra a muestra y falla si alguno se aparta más de 1 LSB.
- El manifiesto lleva el hash de lo que hay en la carpeta. AppBuild lo usa para saber que `Generado/` está entero y es el que se generó.

## 4. Cuando se retire packages/

`sonido.sh` depende de `packages/client/src/audio` y `packages/client/public/audio`. Al retirar packages/ tras las releases de Unity, esos dos se quedan, o se mueven a `unity/tools/sonido/` con las rutas de `generar.mjs` cambiadas.
