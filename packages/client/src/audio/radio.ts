import { audio } from './engine';

// Morse at the key: a few letters tapped under the radio operator's lines,
// quiet, on the radio bus. Live oscillator, so any word can be sent.

const CODES: Record<string, string> = {
  A: '.-',
  D: '-..',
  E: '.',
  G: '--.',
  I: '..',
  K: '-.-',
  M: '--',
  N: '-.',
  O: '---',
  R: '.-.',
  S: '...',
  T: '-',
  U: '..-',
  W: '.--',
};

/** Taps `word` in Morse (letters without a code are skipped); random letters when none is given. */
export function morse(word?: string, level = 0.05, unit = 0.055): void {
  const ctx = audio.live();
  const bus = audio.bus('radio');
  if (!ctx || !bus) return;
  const letters = Object.keys(CODES);
  const text = word ?? Array.from({ length: 2 + Math.floor(Math.random() * 3) }, () => letters[Math.floor(Math.random() * letters.length)]).join('');
  const o = ctx.createOscillator();
  o.frequency.value = 720;
  const g = ctx.createGain();
  g.gain.value = 0;
  o.connect(g).connect(bus);
  let t = ctx.currentTime + 0.08;
  const start = t;
  for (const ch of text.toUpperCase()) {
    const code = CODES[ch];
    if (!code) continue;
    for (const sym of code) {
      const dur = sym === '.' ? unit : unit * 3;
      g.gain.setValueAtTime(0, t);
      g.gain.linearRampToValueAtTime(level, t + 0.006);
      g.gain.setValueAtTime(level, t + dur - 0.006);
      g.gain.linearRampToValueAtTime(0, t + dur);
      t += dur + unit;
    }
    t += unit * 2;
  }
  o.start(start);
  o.stop(t + 0.05);
}
