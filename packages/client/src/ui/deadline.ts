import { h } from './dom';

// The clock on a question left open (docs/PRUEBA-2026-10-14.md #28): a
// proposal in the base or a call to leave in La Mula is taken as a yes once
// it has waited long enough, so whoever stays can play on if the partner has
// gone. The server keeps the time and says how much is left; each client
// counts it down on its own clock (the two machines' clocks may differ).

/** Under this many seconds the countdown turns red. */
const HURRY_S = 10;

/** Ends known by what they count down to: `kind` → when it falls due, on this machine's clock (performance.now). */
const due = new Map<string, { at: number; total: number }>();

/** The server's word: `kind`'s question falls due in `ms` (null: it was answered, or there is none). */
export function setDeadline(kind: string, ms: number | null, total = ms ?? 0): void {
  if (ms === null) due.delete(kind);
  else due.set(kind, { at: performance.now() + ms, total: Math.max(total, ms) });
}

export function deadlineOf(kind: string): { at: number; total: number } | null {
  return due.get(kind) ?? null;
}

const clock = (s: number) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;

/** What is left of `kind` right now, «1:25», or null when nothing waits (for words drawn once, not ticking). */
export function deadlineLeft(kind: string): string | null {
  const d = due.get(kind);
  return d ? clock(Math.max(0, Math.ceil((d.at - performance.now()) / 1000))) : null;
}

/**
 * The countdown of `kind` as a line (`text` words it round the time left,
 * «Se acepta sola en 1:25») over a bar that drains; null when nothing waits.
 * It ticks by itself while it is on the page; screens that redraw just make
 * a new one.
 */
export function deadlineEl(kind: string, text: (left: string) => string): HTMLElement | null {
  const d = due.get(kind);
  if (!d) return null;
  const words = h('span.deadline-text');
  const bar = h('i');
  const el = h('span.deadline', { role: 'timer' }, words, h('span.deadline-bar', {}, bar));
  const tick = () => {
    const left = Math.max(0, Math.ceil((d.at - performance.now()) / 1000));
    words.textContent = text(clock(left));
    bar.style.width = `${d.total ? Math.max(0, Math.min(100, ((d.at - performance.now()) / d.total) * 100)) : 0}%`;
    el.classList.toggle('hurry', left <= HURRY_S);
    return left;
  };
  tick();
  // It stops once taken off the page (a redraw made a new one), or if it never got there.
  let shown = false;
  let misses = 0;
  const timer = window.setInterval(() => {
    if (!el.isConnected) {
      if (shown || ++misses > 8) window.clearInterval(timer);
      return;
    }
    shown = true;
    if (tick() <= 0) window.clearInterval(timer);
  }, 250);
  return el;
}
