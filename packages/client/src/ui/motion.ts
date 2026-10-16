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

/**
 * The last value shown for each resource, by its classes, kept per top bar
 * (playtest 14/10, P9). A module-wide memory outlived the screen: back from
 * a mission, the new base's counter started from what the old one had shown
 * and counted up again later, as if the money came back.
 */
const shownBy = new WeakMap<Element, Map<string, number>>();

function enter(el: HTMLElement): void {
  el.classList.remove('entering');
  // Restart the panels' animations even if the class was still on.
  void el.offsetWidth;
  el.classList.add('entering');
  window.clearTimeout(Number(el.dataset.enterTimer ?? 0));
  el.dataset.enterTimer = String(window.setTimeout(() => el.classList.remove('entering'), ENTER_MS));
}

/**
 * Counts `b` from `from` to its own number, easing out; the plate flashes the
 * way it went. The number it goes to stays in `data-to`: the text shows the
 * count on its way, and is never taken for the value (playtest 16/10 #5: two
 * renders in a row read the count's first number as the new value, counted
 * back to it, and the supplies seemed not to go down).
 */
function tick(b: HTMLElement, from: number, to: number): void {
  b.dataset.to = String(to);
  // Nobody sees it: straight to the number.
  if (document.hidden) {
    b.textContent = String(to);
    return;
  }
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
  let shown = shownBy.get(top);
  if (!shown) shownBy.set(top, (shown = new Map()));
  // A bar out of sight (a screen taking off, a hidden view) only remembers: nothing counts where nobody sees.
  const visible = (top as HTMLElement).offsetParent !== null;
  for (const b of top.querySelectorAll<HTMLElement>('.strat-res .strat-credits > b')) {
    const value = Number(b.dataset.to ?? b.textContent);
    if (!Number.isFinite(value)) continue;
    const key = b.parentElement!.className.replace(/\s*(tick-up|tick-down|x-bad)\b/g, '');
    const before = shown.get(key);
    shown.set(key, value);
    if (visible && before !== undefined && before !== value) tick(b, before, value);
  }
}

let installed = false;

export function installMotion(): void {
  if (installed || typeof MutationObserver === 'undefined') return;
  installed = true;
  new MutationObserver((records) => {
    // Each top bar once, however many changes it had: a second look would see the first's count.
    const tops = new Set<HTMLElement>();
    for (const r of records) {
      const el = r.target as HTMLElement;
      if (r.type === 'attributes') {
        if (el.classList.contains('strat') && r.oldValue !== el.dataset.view) enter(el);
      } else if (el.classList?.contains('strat-top')) tops.add(el);
    }
    for (const top of tops) resources(top);
  }).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-view'], attributeOldValue: true });
}
