import { sound } from './bank';
import { audio, type PlayOptions } from './engine';

// The interface's sounds (ui/theme.ts, «Mixta»): Kenney's «Interface Sounds»
// (CC0; public/audio/ui, made by tools/ui/kenney-sounds.sh). Clean ticks on
// buttons, glass between tabs, a mechanical switch to confirm, a rise when a
// panel opens, a chime when something is done. Until a recording has loaded,
// the same moment plays one of the game's own sounds (sounds.ts). One
// listener for the whole document, so new screens need no wiring.

/** What an interface sound means. The names come from the old case file: a key, the bell, a page, a stamp. */
export type UiKind = 'key' | 'space' | 'bell' | 'confirm' | 'page' | 'paper' | 'stamp' | 'error' | 'cellBuy' | 'cellHover' | 'cellHold';

/** While the recordings load: the game's own sounds closest to them. */
const SYNTH: Record<UiKind, string> = {
  key: 'ui.tick',
  space: 'ui.tick',
  bell: 'command',
  confirm: 'command',
  page: 'holo.page',
  paper: 'radio.close',
  stamp: 'geo.ping',
  error: 'ui.error',
  // The skill board's (strategy/hexBoard.ts): made in code only, no recording.
  cellBuy: 'cell.buy',
  cellHover: 'cell.hover',
  cellHold: 'cell.hold',
};

/** The recordings of each moment (one is picked at random); a moment without any plays its own sound (SYNTH). */
const RECORDED: Partial<Record<UiKind, string[]>> = {
  key: ['tick_004', 'click_001'],
  space: ['tick_004'],
  bell: ['select_002'],
  confirm: ['switch_004'],
  page: ['glass_006'],
  paper: ['maximize_007'],
  stamp: ['confirmation_001'],
  error: ['error_004'],
};

/** How loud each moment sits (the recordings are all normalised to the same peak). */
const GAIN: Partial<Record<UiKind, number>> = { key: 0.32, space: 0.3, bell: 0.4, confirm: 0.5, page: 0.4, paper: 0.42, stamp: 0.55, error: 0.45 };

const buffers = new Map<string, AudioBuffer>();
const loading = new Set<string>();

/** Fetches and decodes the recordings once (needs the audio context, so after the first gesture). */
function load(): void {
  const ctx = audio.ctx;
  if (!ctx) return;
  for (const name of new Set(Object.values(RECORDED).flatMap((names) => names ?? []))) {
    if (buffers.has(name) || loading.has(name)) continue;
    loading.add(name);
    fetch(`/audio/ui/${name}.wav`)
      .then((r) => r.arrayBuffer())
      .then((data) => ctx.decodeAudioData(data))
      .then((buf) => buffers.set(name, buf))
      .catch(() => {
        /* left to the fallback sound */
      })
      .finally(() => loading.delete(name));
  }
}

/** The skill board is swept fast across its cells: at most one hover tick this often. */
const HOVER_GAP_MS = 45;
let lastHover = -Infinity;

/** Plays an interface sound; returns what stops it, fading out (the board's hold, let go before it buys). */
export function uiSound(kind: UiKind, o: PlayOptions & { spread?: number } = {}): () => void {
  if (kind === 'cellHover') {
    const now = performance.now();
    if (now - lastHover < HOVER_GAP_MS) return () => {};
    lastHover = now;
  }
  const names = RECORDED[kind];
  const buf = names && buffers.get(names[Math.floor(Math.random() * names.length)]!);
  if (buf) {
    const spread = o.spread ?? 0.03;
    const src = audio.play(buf, { ...o, bus: 'ui', gain: (o.gain ?? 1) * (GAIN[kind] ?? 1), rate: (o.rate ?? 1) * (1 + (Math.random() * 2 - 1) * spread) });
    return () => {
      if (src) audio.stop(src);
    };
  }
  if (names) load();
  // The game's own sounds (a radio click, the search slide…) still follow the interface's volume.
  return sound(SYNTH[kind], { bus: 'ui', ...o });
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
  // The recordings, ready before the first click that needs them.
  audio.onReady(load);
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
