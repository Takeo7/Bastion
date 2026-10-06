// Typewriter text: every letter struck slightly off, as on a real machine.
// Each letter gets its own baseline shift, tilt and ink, derived from the
// text itself, so the same label always looks the same and does not jump
// when the HUD redraws it. Styled by `.typed` in styles.css.
//
// Only for short static labels (titles, names, the objective): it creates one
// element per letter.

/** Small deterministic hash → [0, 1). */
function rand(seed: number): number {
  let h = Math.imul(seed ^ 0x9e3779b9, 0x85ebca6b);
  h = Math.imul(h ^ (h >>> 13), 0xc2b2ae35);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

function hashText(text: string): number {
  let h = 2166136261;
  for (let i = 0; i < text.length; i++) h = Math.imul(h ^ text.charCodeAt(i), 16777619);
  return h;
}

/** A typed label, ready to append: `<span class="typed" aria-label=…>` with one span per letter. */
export function typed(text: string): DocumentFragment {
  const fragment = document.createDocumentFragment();
  const wrap = document.createElement('span');
  wrap.className = 'typed';
  wrap.setAttribute('aria-label', text);
  const seed = hashText(text);
  let run = '';
  const flush = () => {
    if (run) wrap.append(run);
    run = '';
  };
  [...text].forEach((ch, i) => {
    if (ch === ' ') {
      run += ch;
      return;
    }
    flush();
    const r = (k: number) => rand(seed + i * 7 + k);
    const letter = document.createElement('span');
    letter.textContent = ch;
    letter.setAttribute('aria-hidden', 'true');
    letter.style.setProperty('--dy', `${((r(1) - 0.5) * 0.09).toFixed(3)}em`);
    letter.style.setProperty('--rot', `${((r(2) - 0.5) * 4).toFixed(2)}deg`);
    // Most keys strike evenly; a few hit light, a few hit hard and leave a ghost.
    const strike = r(3);
    letter.style.setProperty('--ink', strike < 0.12 ? '0.62' : strike > 0.9 ? '1' : (0.82 + r(4) * 0.14).toFixed(2));
    if (strike > 0.93) letter.classList.add('struck');
    wrap.append(letter);
  });
  flush();
  fragment.append(wrap);
  return fragment;
}
