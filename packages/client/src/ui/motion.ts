// Small motions that make the interface feel alive (ui/themes/mixta*.css),
// added from outside the screens so none of them needs wiring:
//   - a base screen that changes (the strategy layer's `data-view`) gets the
//     class `entering` for a moment, and its panels slide in;
//   - a resource in the top bar that changes counts up or down to its new
//     value, and flashes green or red.
// The screens rebuild their DOM on every update, so both watch for what is
// new rather than holding on to elements. Everything else that moves is CSS.

const ENTER_MS = 450;
const TICK_MS = 700;

/** The last value shown for each resource of the top bar, by its classes. */
const shown = new Map<string, number>();

function enter(el: HTMLElement): void {
  el.classList.remove('entering');
  // Restart the panels' animations even if the class was still on.
  void el.offsetWidth;
  el.classList.add('entering');
  window.clearTimeout(Number(el.dataset.enterTimer ?? 0));
  el.dataset.enterTimer = String(window.setTimeout(() => el.classList.remove('entering'), ENTER_MS));
}

/** Counts `b` from `from` to its own number, easing out; the plate flashes the way it went. */
function tick(b: HTMLElement, from: number, to: number): void {
  const plate = b.parentElement;
  plate?.classList.add(to > from ? 'tick-up' : 'tick-down');
  const start = performance.now();
  const step = (now: number) => {
    if (!b.isConnected) return;
    const t = Math.min(1, (now - start) / TICK_MS);
    const eased = 1 - Math.pow(1 - t, 3);
    b.textContent = String(Math.round(from + (to - from) * eased));
    if (t < 1) requestAnimationFrame(step);
  };
  b.textContent = String(from);
  requestAnimationFrame(step);
}

function resources(top: Element): void {
  for (const b of top.querySelectorAll<HTMLElement>('.strat-res .strat-credits > b')) {
    const value = Number(b.textContent);
    if (!Number.isFinite(value)) continue;
    const key = b.parentElement!.className.replace(/\s*(tick-up|tick-down|x-bad)\b/g, '');
    const before = shown.get(key);
    shown.set(key, value);
    if (before !== undefined && before !== value) tick(b, before, value);
  }
}

let installed = false;

export function installMotion(): void {
  if (installed || typeof MutationObserver === 'undefined') return;
  installed = true;
  new MutationObserver((records) => {
    for (const r of records) {
      const el = r.target as HTMLElement;
      if (r.type === 'attributes') {
        if (el.classList.contains('strat') && r.oldValue !== el.dataset.view) enter(el);
      } else if (el.classList?.contains('strat-top')) resources(el);
    }
  }).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-view'], attributeOldValue: true });
}
