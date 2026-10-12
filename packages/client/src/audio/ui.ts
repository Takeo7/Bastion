import { sound } from './bank';
import type { PlayOptions } from './engine';

// The interface sounds like the case file it looks like: every button is a
// typewriter key, confirming is the carriage returning with its bell, the
// tabs turn a page, a disabled button knocks dully, and typing into a field
// types. With the Holotáctico interface the same moments are electronic
// blips. One listener for the whole document, so new screens need no wiring.

/** What an interface sound means; each interface theme has its own sound for it. */
export type UiKind = 'key' | 'space' | 'bell' | 'confirm' | 'page' | 'paper' | 'stamp' | 'error';

const PAPER: Record<UiKind, string> = {
  key: 'ui.key',
  space: 'ui.space',
  bell: 'ui.bell',
  confirm: 'ui.return',
  page: 'ui.page',
  paper: 'ui.paper',
  stamp: 'ui.stamp',
  error: 'ui.error',
};

const HOLO: Record<UiKind, string> = {
  key: 'holo.click',
  space: 'holo.click',
  bell: 'holo.confirm',
  confirm: 'holo.confirm',
  page: 'holo.page',
  paper: 'holo.open',
  stamp: 'holo.alert',
  error: 'holo.error',
};

/** Plays an interface sound in the voice of the active theme: the case file (typewriter, paper, stamps) or Holotáctico (blips). */
export function uiSound(kind: UiKind, o: PlayOptions & { spread?: number } = {}): void {
  sound((document.documentElement.dataset.ui === 'holo' ? HOLO : PAPER)[kind], o);
}

const CLICKABLE = 'button, [role="button"], .x-row, .lineup-card, .edge-mark, .setting-choices button';

function soundFor(el: Element): UiKind {
  if (el instanceof HTMLButtonElement && el.disabled) return 'error';
  if (el.closest('.strat-nav, .nav-tabs')) return 'page';
  if (el.matches('.x-btn.primary, .x-btn.big, .hud button.fire, .menu-foot .x-btn.primary')) return 'confirm';
  return 'key';
}

let installed = false;

export function installUiSounds(): void {
  if (installed) return;
  installed = true;
  document.addEventListener(
    'pointerdown',
    (e) => {
      if (e.button !== 0) return;
      const target = e.target as Element | null;
      // Disabled buttons swallow pointer events in some browsers: look from the target up.
      const el = target?.closest(CLICKABLE);
      if (!el) return;
      const kind = soundFor(el);
      uiSound(kind, { spread: kind === 'key' ? 0.08 : 0.03 });
    },
    { capture: true },
  );
  document.addEventListener(
    'keydown',
    (e) => {
      const t = e.target as HTMLElement | null;
      if (!t || !(t instanceof HTMLInputElement || t instanceof HTMLTextAreaElement) || e.repeat) return;
      if (e.key === 'Enter') uiSound('bell');
      else if (e.key === ' ') uiSound('space');
      else if (e.key.length === 1 || e.key === 'Backspace') uiSound('key', { spread: 0.1 });
    },
    { capture: true },
  );
}
