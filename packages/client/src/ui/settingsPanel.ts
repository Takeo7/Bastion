import { audio, type Volumes } from '../audio/engine';
import { graphics, setGraphics, type GraphicsSettings } from '../settings';
import { h } from './dom';
import { setUiTheme, UI_THEMES, uiTheme, type UiTheme } from './theme';

type Choice<K extends keyof GraphicsSettings> = [GraphicsSettings[K], string, string?];

/** One setting: its name and a row of choices, the current one marked. */
function row<K extends keyof GraphicsSettings>(key: K, label: string, choices: Choice<K>[], hint: string, redraw: () => void): HTMLElement {
  const value = graphics()[key];
  return h(
    'div.setting',
    { title: hint },
    h('span.setting-name', {}, label),
    h(
      'div.setting-choices',
      {},
      ...choices.map(([option, text, tip]) =>
        h(
          `button${option === value ? '.on' : ''}`,
          {
            title: tip ?? '',
            onclick: () => {
              setGraphics({ [key]: option } as Partial<GraphicsSettings>);
              redraw();
            },
          },
          text,
        ),
      ),
    ),
  );
}

/**
 * Graphics settings (frame-rate cap, effects, shadows, resolution, FPS counter),
 * shared by the in-mission menu and the main menu. `redraw` re-renders the
 * page so the chosen option shows as selected.
 */
export function graphicsSettings(redraw: () => void): HTMLElement {
  return h(
    'div.settings',
    {},
    row('fps', 'Límite de FPS', [[0, 'Sin límite'], [60, '60'], [30, '30']], 'Menos imágenes por segundo: menos trabajo para la tarjeta gráfica y la batería.', redraw),
    row(
      'effects',
      'Efectos',
      [
        ['high', 'Altos', 'Oclusión ambiental y brillo'],
        ['medium', 'Medios', 'Solo brillo'],
        ['low', 'Bajos', 'Sin posprocesado'],
      ],
      'El posprocesado es lo que más cuesta dibujar.',
      redraw,
    ),
    row('shadows', 'Sombras', [[true, 'Sí'], [false, 'No']], 'Las sombras del sol.', redraw),
    row(
      'resolution',
      'Resolución',
      [
        [0, 'Auto', 'Nítida, salvo en ventanas muy grandes, donde baja un poco para ir fluida'],
        [1, '100 %', 'Siempre la de la pantalla: la más nítida y la que más cuesta'],
        [0.75, '75 %'],
        [0.5, '50 %'],
      ],
      'Dibuja la escena a menos resolución y la escala a la pantalla. La interfaz siempre se ve nítida.',
      redraw,
    ),
    row('showFps', 'Mostrar FPS', [[true, 'Sí'], [false, 'No']], 'Un contador de imágenes por segundo en una esquina.', redraw),
    row(
      'uiScale',
      'Interfaz',
      [
        [0.9, '90 %'],
        [1, '100 %'],
        [1.15, '115 %'],
        [1.3, '130 %'],
      ],
      'El tamaño de los textos y paneles del juego. La vista 3D no cambia.',
      redraw,
    ),
  );
}

const LEVELS: [number, string][] = [
  [0, 'No'],
  [0.25, '25 %'],
  [0.5, '50 %'],
  [0.75, '75 %'],
  [1, '100 %'],
];

/** One volume: a row of levels, the nearest to the current one marked. */
function volumeRow(key: keyof Volumes, label: string, hint: string, redraw: () => void): HTMLElement {
  const value = audio.volumes[key];
  const nearest = LEVELS.reduce((a, b) => (Math.abs(b[0] - value) < Math.abs(a[0] - value) ? b : a))[0];
  return h(
    'div.setting',
    { title: hint },
    h('span.setting-name', {}, label),
    h(
      'div.setting-choices',
      {},
      ...LEVELS.map(([level, text]) =>
        h(
          `button${level === nearest ? '.on' : ''}`,
          {
            onclick: () => {
              audio.setVolumes({ [key]: level });
              redraw();
            },
          },
          text,
        ),
      ),
    ),
  );
}

/** Sound volumes, by kind, for the in-mission menu and the main menu. The M key still mutes everything. */
export function soundSettings(redraw: () => void): HTMLElement {
  return h(
    'div.settings',
    {},
    volumeRow('master', 'Volumen', 'Todo el sonido del juego. La tecla M lo silencia en combate.', redraw),
    volumeRow('sfx', 'Efectos', 'Disparos, explosiones, pasos y acciones.', redraw),
    volumeRow('music', 'Música', 'La música de la base, del globo y del combate.', redraw),
    volumeRow('ambience', 'Ambiente', 'Viento, ciudad, bosque, máquinas.', redraw),
    volumeRow('ui', 'Interfaz y radio', 'Teclas, sellos, papeles y las transmisiones de radio.', redraw),
  );
}

/** The interface's look, Expediente or Holotáctico: shared by both menus, applied at once. */
export function themeSettings(redraw: () => void): HTMLElement {
  const current = uiTheme();
  return h(
    'div.settings',
    {},
    h(
      'div.setting',
      { title: 'Cómo se ven los menús, los paneles y el HUD. Cada jugador elige el suyo.' },
      h('span.setting-name', {}, 'Estilo'),
      h(
        'div.setting-choices',
        {},
        ...(Object.keys(UI_THEMES) as UiTheme[]).map((id) =>
          h(
            `button${id === current ? '.on' : ''}`,
            {
              title: UI_THEMES[id].hint,
              onclick: () => {
                setUiTheme(id);
                redraw();
              },
            },
            UI_THEMES[id].name,
          ),
        ),
      ),
    ),
  );
}
