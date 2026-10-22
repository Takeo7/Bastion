import { SETTINGS_TEXT } from '@bastion/engine';
import { audio, type Volumes } from '../audio/engine';
import { autoQuality, autoQualityNote, ENEMY_SPEED, graphics, setAutoQuality, setGraphics, WALK_SPEED, type GraphicsSettings } from '../settings';
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
    // 120 for a screen of 120 Hz or more (playtest 20/10 #3).
    row('fps', 'Límite de FPS', [[0, 'Sin límite'], [120, '120'], [60, '60'], [30, '30']], 'Menos imágenes por segundo: menos trabajo para la tarjeta gráfica y la batería.', redraw),
    row(
      'effects',
      'Efectos',
      [
        ['high', 'Altos', 'Oclusión ambiental y brillo'],
        ['medium', 'Medios', 'Solo brillo'],
        ['low', 'Bajos', 'Sin posprocesado'],
      ],
      // All live but the mission's dressing and night lights, which are placed when the map is built (mapView.ts).
      'El posprocesado es lo que más cuesta dibujar. Todo cambia al momento, menos los adornos del mapa y las luces de noche, que cambian en la siguiente misión.',
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
    // The enemy turn's speed, a slider (playtest 18/10 #6); the squad's own actions play at their own pace.
    speedRow('enemySpeed', SETTINGS_TEXT.enemySpeed, ENEMY_SPEED, `${SETTINGS_TEXT.enemySpeedAbout} En el turno enemigo, mantén la barra espaciadora para verlo más rápido.`),
    // How fast the soldiers walk and run on screen, to try (Ana, 22/10 #32): the rules don't change.
    speedRow('walkSpeed', 'Velocidad de los soldados', WALK_SPEED, 'Lo rápido que se mueven en pantalla. No cambia las reglas.'),
    row('autoPlan', SETTINGS_TEXT.plans, [[true, 'Sí'], [false, 'No']], SETTINGS_TEXT.plansAbout, redraw),
    // Off by default (Arturo, 21/10): each outfit with its own colours, and only the ring underneath says whose.
    row('squadTint', 'Ropa con el color del jugador', [[false, 'No'], [true, 'Sí']], 'Tiñe la ropa de los soldados con los colores de Personalizar y les pone el brazalete del color de su jugador.', redraw),
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

/** A speed as a slider (the enemy turn's, the soldiers' walk): from `range.min` to `.max`, its value beside it («×1,4»), applied as it moves. */
function speedRow(key: 'enemySpeed' | 'walkSpeed', label: string, range: { min: number; max: number; step: number }, hint: string): HTMLElement {
  const value = h('b.setting-value', {}, speedText(graphics()[key]));
  const input = h('input.setting-slider', {
    type: 'range',
    min: String(range.min),
    max: String(range.max),
    step: String(range.step),
    value: String(graphics()[key]),
    'aria-label': label,
  }) as HTMLInputElement;
  input.addEventListener('input', () => {
    const k = Number(input.value);
    value.textContent = speedText(k);
    setGraphics({ [key]: k } as Partial<GraphicsSettings>);
  });
  return h('div.setting', { title: hint }, h('span.setting-name', {}, label), h('div.setting-choices.slider', {}, h('small', {}, 'Lenta'), input, h('small', {}, 'Rápida'), value));
}

/** «×1,4»: a speed's rate, the Spanish way. */
function speedText(k: number): string {
  return `×${k.toFixed(1).replace('.', ',')}`;
}

/**
 * One volume, a slider from 0 to 100 % in steps of one (Ana, 22/10 #25: «si quiero ponerlo al 1 %»), its value
 * beside it, heard as it moves.
 */
function volumeRow(key: keyof Volumes, label: string, hint: string): HTMLElement {
  const percent = () => `${Math.round(audio.volumes[key] * 100)} %`;
  const value = h('b.setting-value', {}, percent());
  const input = h('input.setting-slider', { type: 'range', min: '0', max: '100', step: '1', value: String(Math.round(audio.volumes[key] * 100)), 'aria-label': label }) as HTMLInputElement;
  input.addEventListener('input', () => {
    audio.setVolumes({ [key]: Number(input.value) / 100 });
    value.textContent = percent();
  });
  return h('div.setting', { title: hint }, h('span.setting-name', {}, label), h('div.setting-choices.slider', {}, input, value));
}

/** Sound volumes, by kind, for the in-mission menu and the main menu. The M key still mutes everything. */
export function soundSettings(redraw: () => void): HTMLElement {
  return h(
    'div.settings',
    {},
    volumeRow('master', 'Volumen', 'Todo el sonido del juego. La tecla M lo silencia en combate.'),
    volumeRow('sfx', 'Efectos', 'Disparos, explosiones, pasos y acciones.'),
    volumeRow('music', 'Música', 'La música de la base, del mapa y del combate.'),
    volumeRow('ambience', 'Ambiente', 'Viento, ciudad, bosque, máquinas.'),
    volumeRow('ui', 'Interfaz y radio', 'Botones, avisos y las transmisiones de radio.'),
  );
}
