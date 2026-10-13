# Fuentes y sonidos de la interfaz: qué se usa y con qué licencia

Regla (Arturo, por General, 2026-10-13): todo lo de la interfaz sale de Synty o de Kenney, o lo hacemos nosotros. Las fuentes libres son la única excepción, con estas condiciones:

- solo con licencia OFL o Apache;
- dos familias como mucho por propuesta;
- texto, HUD y cifras en una fuente muy legible;
- las fuentes de carácter, solo en títulos de 16 px o más;
- solo el subconjunto latino, en woff2, con 2 o 3 grosores.

## Fuentes de las propuestas

Todas vienen de npm, `@fontsource/*` 5.3.0, en latín y woff2. Las importa `packages/client/src/ui/theme.ts`.

| Propuesta | Texto, HUD y cifras | Títulos (16 px o más) |
|---|---|---|
| A · Apocalipsis | Barlow Condensed 500/600/700 | Black Ops One 400 |
| B · Moderna | Barlow 500/600/700 | Barlow en negrita (no usa una segunda familia) |
| C · Mixta | Barlow 500/600/700 | Oswald 600 |

| Familia | Licencia | Autoría |
|---|---|---|
| Barlow, Barlow Condensed | SIL OFL 1.1 | The Barlow Project Authors (github.com/jpt/barlow) |
| Oswald | SIL OFL 1.1 | The Oswald Project Authors (github.com/googlefonts/OswaldFont) |
| Black Ops One | SIL OFL 1.1 | James Grieshaber y Eben Sorkin, Sorkin Type (github.com/SorkinType/Black-Ops) |
| Oxanium (Synty, `MilitaryHUD/Fonts`; sin usar por ahora) | SIL OFL 1.1 | Severin Meyer |

Comprobado: las cuatro que se usan tienen en su subconjunto latino todos los caracteres del español (ñ, á é í ó ú, ü, ¿ ¡ « » §, la raya, los puntos suspensivos y ×).

**Tras el voto** se borran del `package.json` y de `theme.ts`:

- las fuentes que no use la propuesta ganadora;
- las de Expediente: Caveat, Courier Prime y Special Elite;
- la de Holotáctico: Rajdhani.

## Sonidos de la interfaz

Salen de **Kenney, «Interface Sounds»**, con licencia CC0 (dominio público, sin créditos obligatorios). El original está en `assets-src/kenney/interface-sounds/` y no se sube.

- Al repositorio van solo los 26 que se usan, en `packages/client/public/audio/ui/`: sin silencios, con el pico a −3 dB, mono y en WAV a 32 kHz (unos 230 KB en total). WAV se decodifica en todos los navegadores, también Safari, y no retrasa el clic como un MP3.
- Los genera `tools/ui/kenney-sounds.sh`.
- Los reparte `audio/ui.ts`, un juego distinto por propuesta:

| Momento | A · Apocalipsis | B · Moderna | C · Mixta |
|---|---|---|---|
| Botón | back_002, back_004 | click_002, click_003 | tick_004, click_001 |
| Espacio (al escribir) | back_004 | click_005 | tick_004 |
| Intro (al escribir) | drop_001 | select_001 | select_002 |
| Confirmar (botón principal) | switch_003 | select_003 | switch_004 |
| Pestaña | scratch_004 | glass_002 | glass_006 |
| Se abre un panel | drop_004 | maximize_002 | maximize_007 |
| Guardado, aviso | bong_001 | confirmation_003 | confirmation_001 |
| No se puede | error_006 | error_008 | error_004 |

Expediente sigue con sus sonidos sintetizados (`audio/sounds.ts`). Tras el voto, se borran los WAV que no use la propuesta ganadora.
