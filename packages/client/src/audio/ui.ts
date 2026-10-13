import { sound } from './bank';
import { audio, type PlayOptions } from './engine';

// The interface sounds like the case file it looks like: every button is a
// typewriter key, confirming is the carriage returning with its bell, the
// tabs turn a page, a disabled button knocks dully, and typing into a field
// types. Each interface theme (data-ui, ui/theme.ts) has its own voice for the
// same moments. The three Synty proposals play recordings from Kenney's
// «Interface Sounds» (CC0; public/audio/ui, made by tools/ui/kenney-sounds.sh),
// a different set each; until a recording has loaded, the same moment plays
// one of the game's own sounds (sounds.ts). One listener for the whole
// document, so new screens need no wiring.

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

/** While a proposal's recordings load: the game's own sounds closest to them. */
const SYNTH: Record<UiKind, string> = {
  key: 'ui.tick',
  space: 'ui.tick',
  bell: 'command',
  confirm: 'command',
  page: 'holo.page',
  paper: 'radio.close',
  stamp: 'geo.ping',
  error: 'ui.error',
};

type Recorded = Record<UiKind, string[]>;

/** Proposal A, rusty metal: low dry clicks, a heavy switch to confirm, a scrape between tabs, a metal bong when saved. */
const APOCALIPSIS: Recorded = {
  key: ['back_002', 'back_004'],
  space: ['back_004'],
  bell: ['drop_001'],
  confirm: ['switch_003'],
  page: ['scratch_004'],
  paper: ['drop_004'],
  stamp: ['bong_001'],
  error: ['error_006'],
};

/** Proposal B, a clean console: tiny clicks, glass between tabs, a soft rise when a panel opens, a chime when saved. */
const MODERNA: Recorded = {
  key: ['click_002', 'click_003'],
  space: ['click_005'],
  bell: ['select_001'],
  confirm: ['select_003'],
  page: ['glass_002'],
  paper: ['maximize_002'],
  stamp: ['confirmation_003'],
  error: ['error_008'],
};

/** Proposal C, Moderna's clean ticks with Apocalipsis's mechanical switch. */
const MIXTA: Recorded = {
  key: ['tick_004', 'click_001'],
  space: ['tick_004'],
  bell: ['select_002'],
  confirm: ['switch_004'],
  page: ['glass_006'],
  paper: ['maximize_007'],
  stamp: ['confirmation_001'],
  error: ['error_004'],
};

const RECORDED: Record<string, Recorded> = { apocalipsis: APOCALIPSIS, moderna: MODERNA, mixta: MIXTA };

/** How loud each moment sits (the recordings are all normalised to the same peak). */
const GAIN: Record<UiKind, number> = { key: 0.32, space: 0.3, bell: 0.4, confirm: 0.5, page: 0.4, paper: 0.42, stamp: 0.55, error: 0.45 };

const buffers = new Map<string, AudioBuffer>();
const loading = new Set<string>();

/** Fetches and decodes a theme's recordings once (needs the audio context, so after the first gesture). */
function load(theme: string): void {
  const set = RECORDED[theme];
  const ctx = audio.ctx;
  if (!set || !ctx) return;
  for (const name of new Set(Object.values(set).flat())) {
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

/** Plays an interface sound in the voice of the active theme (the case file's typewriter, paper and stamps by default). */
export function uiSound(kind: UiKind, o: PlayOptions & { spread?: number } = {}): void {
  const theme = document.documentElement.dataset.ui ?? '';
  const set = RECORDED[theme];
  if (set) {
    const names = set[kind];
    const buf = buffers.get(names[Math.floor(Math.random() * names.length)]!);
    if (buf) {
      const spread = o.spread ?? 0.03;
      audio.play(buf, { ...o, bus: 'ui', gain: (o.gain ?? 1) * GAIN[kind], rate: (o.rate ?? 1) * (1 + (Math.random() * 2 - 1) * spread) });
      return;
    }
    load(theme);
  }
  // The game's own sounds (a radio click, the search slide…) still follow the interface's volume.
  sound((set ? SYNTH : theme === 'holo' ? HOLO : PAPER)[kind], { bus: 'ui', ...o });
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
  // The recordings of the theme on screen, ready before the first click that needs them.
  audio.onReady(() => load(document.documentElement.dataset.ui ?? ''));
  window.addEventListener('bastion:theme', () => load(document.documentElement.dataset.ui ?? ''));
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
