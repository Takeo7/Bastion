# Fuentes, sonidos y gráficos de la interfaz: qué se usa y con qué licencia

Regla (Arturo, por General, 2026-10-13): todo lo de la interfaz sale de Synty o de Kenney, o lo hacemos nosotros. Las fuentes libres son la única excepción, con estas condiciones:

- solo con licencia OFL o Apache;
- dos familias como mucho;
- texto, HUD y cifras en una fuente muy legible;
- la fuente de carácter, solo en títulos y cifras grandes;
- solo el subconjunto latino, en woff2, con 2 o 3 grosores.

La interfaz es **«Mixta»**, la que eligieron Arturo y Ana tras la prueba del 13/10. Las otras propuestas (Apocalipsis y Moderna), Expediente y Holotáctico están borradas, con sus fuentes y sonidos.

## Fuentes

Vienen de npm, `@fontsource/*` 5.3.0, en latín y woff2. Las importa `packages/client/src/ui/theme.ts`, y no hay más paquetes de fuentes en `package.json`.

| Uso | Familia y grosores |
|---|---|
| Texto, HUD, botones | Barlow 500/600/700 |
| Títulos y cifras grandes (recursos, probabilidad de impacto, nombres) | Oswald 500/600 |

| Familia | Licencia | Autoría |
|---|---|---|
| Barlow | SIL OFL 1.1 | The Barlow Project Authors (github.com/jpt/barlow) |
| Oswald | SIL OFL 1.1 | The Oswald Project Authors (github.com/googlefonts/OswaldFont) |
| Oxanium (Synty, `INTERFACE_MilitaryCombatHUD_SourceFiles/Fonts`; sin usar) | SIL OFL 1.1 | Severin Meyer |

Comprobado: las dos tienen en su subconjunto latino todos los caracteres del español (ñ, á é í ó ú, ü, ¿ ¡ « » §, la raya, los puntos suspensivos y ×).

## Sonidos de la interfaz

Salen de **Kenney, «Interface Sounds»**, con licencia CC0 (dominio público, sin créditos obligatorios). El original está en `assets-src/kenney/interface-sounds/` y no se sube.

- Al repositorio van solo los 8 que se usan, en `packages/client/public/audio/ui/`: sin silencios, con el pico a −3 dB, mono y en WAV a 32 kHz (unos 72 KB en total). WAV se decodifica en todos los navegadores, también Safari, y no retrasa el clic como un MP3.
- Los genera `tools/ui/kenney-sounds.sh`.
- Los reparte `audio/ui.ts`:

| Momento | Sonido |
|---|---|
| Botón | tick_004, click_001 |
| Espacio (al escribir) | tick_004 |
| Intro (al escribir) | select_002 |
| Confirmar (botón principal) | switch_004 |
| Pestaña | glass_006 |
| Se abre un panel | maximize_007 |
| Guardado, aviso | confirmation_001 |
| No se puede | error_004 |

Mientras se cargan, suena el sonido sintetizado más parecido de `audio/sounds.ts`.

## Gráficos

- **Synty, INTERFACE** (Apocalypse HUD, Modern Menus, Military Combat HUD): iconos, galones de rango, el dial de la probabilidad y la cinta de peligro. Están en `packages/client/public/ui/synty/`, reducidos y en WebP. Solo van los que se usan; los copia `tools/ui/synty-sprites.py` desde `assets-src/synty/`, que no se sube.
- **Renders de Arte-Personajes** de modelos Synty (`public/ui/synty/renders/`):
  - los retratos de clase de las tarjetas de la cochera y del informe;
  - La Mula de frente en la cochera;
  - el botiquín de la Armería;
  - las siluetas de la ración, la Mula, el Camposanto, la roca y las ruinas.
- **Nuestros:**
  - el acero cepillado y la suciedad de los paneles, de `tools/ui/mixta-textures.py`;
  - las dos puertas (abrir y cerrar), en SVG, en `public/ui/mixta/`.
