// The interface's look: "Expediente" (paper, typewriter and rubber stamps, the
// default) or "Holotáctico" (dark glass panels, cut corners and cyan light,
// closer to the voxel and low-poly art). Each browser keeps its own choice; it
// is applied as `data-ui` on <html>, and ui/holo.css restyles everything under
// it by redefining the shared tokens of styles.css and strategy.css.
import '@fontsource/rajdhani/latin-500.css';
import '@fontsource/rajdhani/latin-600.css';
import '@fontsource/rajdhani/latin-700.css';
import { storageKey } from '../net';
import './holo.css';

export type UiTheme = 'expediente' | 'holo';

export const UI_THEMES: Record<UiTheme, { name: string; hint: string }> = {
  expediente: { name: 'Expediente', hint: 'Papel, máquina de escribir y sellos: el archivo de la resistencia.' },
  holo: { name: 'Holotáctico', hint: 'Paneles de vidrio oscuro, esquinas cortadas y luz cian, como el arte vóxel y low-poly.' },
};

const KEY = storageKey('ui');

function saved(): UiTheme {
  try {
    return localStorage.getItem(KEY) === 'holo' ? 'holo' : 'expediente';
  } catch {
    return 'expediente';
  }
}

/** The theme on the page now. */
export function uiTheme(): UiTheme {
  return document.documentElement.dataset.ui === 'holo' ? 'holo' : 'expediente';
}

/** Puts the saved theme on the page; call once at start-up. */
export function applyUiTheme(): void {
  document.documentElement.dataset.ui = saved();
}

/** Switches the look at once, everywhere; screens that care listen to 'bastion:theme'. */
export function setUiTheme(theme: UiTheme): void {
  try {
    localStorage.setItem(KEY, theme);
  } catch {
    /* private mode: the choice lasts until the page is closed */
  }
  document.documentElement.dataset.ui = theme;
  window.dispatchEvent(new CustomEvent('bastion:theme', { detail: theme }));
}

export function otherUiTheme(theme = uiTheme()): UiTheme {
  return theme === 'holo' ? 'expediente' : 'holo';
}
