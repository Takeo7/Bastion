let timeScale = 1;

/** Speeds every animation up (e.g. when a backlog of events is waiting). */
export function setTimeScale(k: number): void {
  timeScale = k;
}

export const easeInOut = (t: number) => (t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2);

/**
 * Runs `fn(t)` with t going 0→1 over `seconds`. Resolves immediately when the
 * tab is hidden, so a background window never builds up a queue of animations.
 */
export function tween(seconds: number, fn: (t: number) => void): Promise<void> {
  const duration = (seconds * 1000) / timeScale;
  if (document.hidden || duration <= 0) {
    fn(1);
    return Promise.resolve();
  }
  return new Promise((resolve) => {
    const start = performance.now();
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / duration);
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
