import { audio, type Volumes } from '../audio/engine';
import { autoQuality, autoQualityNote, graphics, setAutoQuality, setGraphics, type GraphicsSettings } from '../settings';
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
      'combatSpeed',
      'Velocidad del combate',
      [
        ['slow', 'Lenta', 'Cada acción tarda más en verse'],
        ['normal', 'Normal'],
        ['fast', 'Rápida', 'Cada acción tarda menos en verse'],
      ],
      'Lo que tarda en verse cada acción, la vuestra y la del enemigo. En el turno enemigo, mantén la barra espaciadora para verlo en Rápida.',
      redraw,
    ),
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
    autoQualityRow(redraw),
  );
}

/** The frame-rate guard (settings.ts): on or off, and what it has lowered on this machine. Shared by every Ajustes. */
function autoQualityRow(redraw: () => void): HTMLElement {
  const on = autoQuality().on;
  const note = autoQualityNote();
  const choice = (value: boolean, text: string, tip: string) => h(`button${value === on ? '.on' : ''}`, { title: tip, onclick: () => (setAutoQuality(value), redraw()) }, text);
  return h(
    'div.setting',
    { title: 'Si el combate va a menos de 45 imágenes por segundo, baja poco a poco la resolución automática, la oclusión ambiental y el brillo. Lo que elijas tú en Efectos o Resolución no lo toca.' },
    h('span.setting-name', {}, 'Ajuste auto'),
    h('div.setting-choices', {}, choice(true, 'Sí', 'Baja la calidad si el combate no va fluido'), choice(false, 'No', 'Siempre la calidad elegida')),
    note ? h('small.setting-note', {}, note) : null,
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
    volumeRow('music', 'Música', 'La música de la base, del valle y del combate.', redraw),
    volumeRow('ambience', 'Ambiente', 'Viento, ciudad, bosque, máquinas.', redraw),
    volumeRow('ui', 'Interfaz y radio', 'Botones, avisos y las transmisiones de radio.', redraw),
  );
}
