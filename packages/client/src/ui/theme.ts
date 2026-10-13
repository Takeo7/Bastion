// The interface's look. "Expediente" (paper, typewriter and rubber stamps) is
// the default until Arturo votes; the three proposals for the new, Synty-style
// interface are playable next to it:
//   - "Apocalipsis": the INTERFACE Apocalypse HUD pack, rusty low-poly metal
//     frames, hazard stripes, stencil headings and the pack's icons;
//   - "Moderna": INTERFACE Modern Menus, clean dark panels with bevelled
//     silver frames and a cut corner, a steel-blue accent;
//   - "Mixta": Moderna's skeleton with Apocalipsis's icons, hazard yellow and
//     torn edges.
// The choice is applied as `data-ui` on <html> (plus `data-uf="synty"` for the
// three proposals, which share ui/themes/synty-base.css, and `data-ui-icons`
// where the pack's icons replace the line ones). Each browser remembers it, the
// room shares it with the partner (main.ts), and `?ui=<id>` forces one.
// "Holotáctico" (ui/holo.css) is out: it still loads if a room asks for it,
// but it is no longer offered.
import '@fontsource/rajdhani/latin-500.css';
import '@fontsource/rajdhani/latin-600.css';
import '@fontsource/rajdhani/latin-700.css';
// The proposals' faces (OFL, latin subset, woff2; licences in docs/ux/FUENTES.md): at most two
// families each, the legible one for text and numbers and a character one for big titles only.
import '@fontsource/oswald/latin-600.css';
import '@fontsource/barlow/latin-500.css';
import '@fontsource/barlow/latin-600.css';
import '@fontsource/barlow/latin-700.css';
import '@fontsource/barlow-condensed/latin-500.css';
import '@fontsource/barlow-condensed/latin-600.css';
import '@fontsource/barlow-condensed/latin-700.css';
import '@fontsource/black-ops-one/latin-400.css';
import { storageKey } from '../net';
import './holo.css';
import './themes/synty-base.css';
import './themes/apocalipsis.css';
import './themes/moderna.css';
import './themes/mixta.css';
import './themes/synty-icons.css';

export type UiTheme = 'expediente' | 'apocalipsis' | 'moderna' | 'mixta' | 'holo';

interface ThemeDef {
  name: string;
  hint: string;
  /** One of the Synty proposals (shared base layer). */
  synty?: boolean;
  /** The pack's icons instead of the line ones. */
  icons?: boolean;
  /** Not offered any more (kept so an old choice still loads). */
  retired?: boolean;
}

export const UI_THEMES: Record<UiTheme, ThemeDef> = {
  expediente: { name: 'Expediente', hint: 'La de ahora: papel, máquina de escribir y sellos.' },
  apocalipsis: { name: 'Apocalipsis', hint: 'Propuesta A: chapa oxidada low-poly, rayas de peligro y los iconos del pack Apocalypse HUD.', synty: true, icons: true },
  moderna: { name: 'Moderna', hint: 'Propuesta B: paneles oscuros y limpios con marcos plateados (Modern Menus).', synty: true },
  mixta: { name: 'Mixta', hint: 'Propuesta C: la estructura de Moderna con los iconos, el amarillo de peligro y los bordes rotos de Apocalipsis.', synty: true, icons: true },
  holo: { name: 'Holotáctico', hint: 'Descartada.', retired: true },
};

/** The ones the selectors offer, in order. */
export const OFFERED_THEMES = (Object.keys(UI_THEMES) as UiTheme[]).filter((id) => !UI_THEMES[id].retired);

const KEY = storageKey('ui');

const isTheme = (id: string | null | undefined): id is UiTheme => !!id && id in UI_THEMES;

function saved(): UiTheme {
  try {
    const id = localStorage.getItem(KEY);
    return isTheme(id) ? id : 'expediente';
  } catch {
    return 'expediente';
  }
}

function put(theme: UiTheme): void {
  const root = document.documentElement;
  const def = UI_THEMES[theme];
  root.dataset.ui = theme;
  if (def.synty) root.dataset.uf = 'synty';
  else delete root.dataset.uf;
  if (def.icons) root.dataset.uiIcons = 'synty';
  else delete root.dataset.uiIcons;
}

/** The theme on the page now. */
export function uiTheme(): UiTheme {
  const id = document.documentElement.dataset.ui;
  return isTheme(id) ? id : 'expediente';
}

/** Puts the saved theme (or `?ui=<id>`) on the page; call once at start-up. */
export function applyUiTheme(): void {
  const forced = new URLSearchParams(location.search).get('ui');
  put(isTheme(forced) ? forced : saved());
}

/** Switches the look at once, everywhere; screens that care listen to 'bastion:theme'. */
export function setUiTheme(theme: UiTheme): void {
  try {
    localStorage.setItem(KEY, theme);
  } catch {
    /* private mode: the choice lasts until the page is closed */
  }
  put(theme);
  window.dispatchEvent(new CustomEvent('bastion:theme', { detail: theme }));
}

/** The next offered theme, for the main menu's one-click switch. */
export function otherUiTheme(theme = uiTheme()): UiTheme {
  const i = OFFERED_THEMES.indexOf(theme);
  return OFFERED_THEMES[(i + 1) % OFFERED_THEMES.length]!;
}
