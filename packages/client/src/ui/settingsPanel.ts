import { graphics, setGraphics, type GraphicsSettings } from '../settings';
import { h } from './dom';

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
