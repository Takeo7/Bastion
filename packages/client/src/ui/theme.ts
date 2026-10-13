// The interface's look: «Mixta», the one Arturo and Ana chose (playtest of
// 13/10) out of the three Synty proposals. Dark steel panels with a bevel and
// a little grime, hazard yellow for the main action, Oswald titles over Barlow
// text, and the INTERFACE packs' icons (Apocalypse HUD, Modern Menus, Military
// Combat HUD) on the buttons. Each player sees it the same; there is no choice
// any more, so nothing is shared through the room.
//
// Every rule of the theme hangs from :root[data-ui='mixta'] (set here, and in
// index.html so the first paint has it): the attribute gives the theme's rules
// the weight to win over the layout rules of styles.css, strategy.css and
// skillTree.css, whatever order the bundler loads them in.
//   - themes/synty-base.css: undoes the old case file's paper habits and paints
//     every component from the --t-* values;
//   - themes/mixta.css: the --t-* values, the materials, type and motion;
//   - themes/mixta-base.css: the base screens, the main menu and Ajustes;
//   - themes/mixta-hud.css: the combat HUD;
//   - themes/synty-icons.css: the packs' silhouettes on abilities and plots.
// ui/motion.ts adds the few motions CSS can't start on its own.
// Faces (OFL, latin subset; licences in docs/ux/FUENTES.md): Barlow for text,
// the HUD and small figures; Oswald for titles and big numbers only.
import '@fontsource/barlow/latin-500.css';
import '@fontsource/barlow/latin-600.css';
import '@fontsource/barlow/latin-700.css';
import '@fontsource/oswald/latin-500.css';
import '@fontsource/oswald/latin-600.css';
import { storageKey } from '../net';
import { installMotion } from './motion';
import './themes/synty-base.css';
import './themes/mixta.css';
import './themes/mixta-base.css';
import './themes/mixta-hud.css';
import './themes/synty-icons.css';

/** Puts the interface on the page; call once at start-up. */
export function applyUiTheme(): void {
  document.documentElement.dataset.ui = 'mixta';
  installMotion();
  // Older builds remembered a theme per browser ('expediente', 'holo', the proposals): it is dropped.
  try {
    localStorage.removeItem(storageKey('ui'));
  } catch {
    /* storage unavailable: nothing to forget */
  }
}
