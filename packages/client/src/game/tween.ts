/**
 * How fast the battle plays (docs/UX-RITMO.md): Ajustes' «Velocidad del
 * combate» (Lenta, Normal, Rápida: everything ×1.4, ×1 or ×0.6 as long), and
 * Rápida while the space bar is held in the enemy turn. There is no other
 * speed-up: a backlog of events no longer hurries them (it made the enemy
 * turn run at ×2.5).
 */
let rate = 1;
let hurry = false;
/** The rate of «Rápida»: everything takes 0.6 of the time. */
export const FAST_RATE = 1 / 0.6;

/** The chosen speed, as a rate (1 normal, 1/1.4 Lenta, 1/0.6 Rápida); 1 outside the battle. */
export function setCombatRate(k: number): void {
  rate = k;
}

/** The space bar held in the enemy turn: at least Rápida until it is let go. */
export function setHurry(on: boolean): void {
  hurry = on;
}

/** How many seconds of animation pass in a second right now. */
export function timeRate(): number {
  return hurry ? Math.max(rate, FAST_RATE) : rate;
}

export const easeInOut = (t: number) => (t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2);

/**
 * Runs `fn(t)` with t going 0→1 over `seconds` (at timeRate). Resolves immediately when the
 * tab is hidden, so a background window never builds up a queue of animations.
 */
export function tween(seconds: number, fn: (t: number) => void): Promise<void> {
  const duration = seconds * 1000;
  if (document.hidden || duration <= 0) {
    fn(1);
    return Promise.resolve();
  }
  return new Promise((resolve) => {
    // Time is added up frame by frame at the rate of the moment, so holding or letting go of the space bar takes effect mid-way.
    let last = performance.now();
    let elapsed = 0;
    const step = (now: number) => {
      elapsed += Math.max(0, now - last) * timeRate();
      last = now;
      const t = Math.min(1, elapsed / duration);
      fn(t);
      if (t < 1 && !document.hidden) requestAnimationFrame(step);
      else {
        if (t < 1) fn(1);
        resolve();
      }
    };
    requestAnimationFrame(step);
  });
}

export function wait(seconds: number): Promise<void> {
  return tween(seconds, () => {});
}
