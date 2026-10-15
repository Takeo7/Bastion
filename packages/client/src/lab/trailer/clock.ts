// The trailer's virtual clock (docs/TRAILER.md). Imported first on
// trailer.html, before anything that reads the time: performance.now,
// Date.now, requestAnimationFrame and the timers all follow a clock that the
// page moves, so everything the game animates by time (the Renderer's loop,
// tween() and wait(), the Synty clips) comes out exactly as at 60 fps however
// long a frame takes to draw and send.
//
// Two ways of running:
//  - free: the clock follows real time, frames every 1/60 s of it (setup:
//    loading, compiling, a shot's own waits; and previews);
//  - stepped: it moves only by step(), one frame at a time (the capture).
//
// Besides: the page always counts as visible (tween() gives up in a hidden
// tab, and the browser pane hides it), Math.random has a seed (the same take
// twice) and the pixel ratio is fixed.

const FRAME_MS = 1000 / 60;

const real = {
  now: performance.now.bind(performance),
  setTimeout: window.setTimeout.bind(window),
  clearTimeout: window.clearTimeout.bind(window),
  dateNow: Date.now,
};

interface Timer {
  id: number;
  due: number;
  fn: () => void;
  every: number | null;
}

let now = real.now();
const epoch = real.dateNow() - now;
const timers = new Map<number, Timer>();
let frames: { id: number; cb: FrameRequestCallback }[] = [];
// Ids far above the browser's own, so a stray clearTimeout of a real one is told apart.
let nextId = 1_000_000;
let mode: 'free' | 'stepped' = 'free';
let lastReal = real.now();

/** Lets everything waiting on promises run: one real task, which the browser never throttles. */
export function settle(): Promise<void> {
  return new Promise((resolve) => {
    const channel = new MessageChannel();
    channel.port1.onmessage = () => resolve();
    channel.port2.postMessage(null);
  });
}

function call(fn: () => void): void {
  try {
    fn();
  } catch (e) {
    console.error(e);
  }
}

function nextTimer(until: number): Timer | null {
  let next: Timer | null = null;
  for (const t of timers.values()) if (t.due <= until && (!next || t.due < next.due || (t.due === next.due && t.id < next.id))) next = t;
  return next;
}

/** Moves the clock on by `ms`: the timers due on the way, each at its time, then one frame. */
async function advance(ms: number): Promise<void> {
  const until = now + ms;
  for (let t = nextTimer(until); t; t = nextTimer(until)) {
    now = Math.max(now, t.due);
    if (t.every === null) timers.delete(t.id);
    else t.due += t.every;
    call(t.fn);
    await settle();
  }
  now = until;
  const due = frames;
  frames = [];
  if (hooks.before) call(() => hooks.before!(now));
  for (const f of due) call(() => f.cb(now));
  // Straight after the drawing, in the same task: a WebGL canvas still holds its image.
  if (hooks.after) call(() => hooks.after!(now));
  await settle();
}

const hooks: { before: ((now: number) => void) | null; after: ((now: number) => void) | null } = { before: null, after: null };

let busy = false;

// The free clock's beat is a message to itself, not a timer: a hidden page (the browser pane when
// another panel is shown) has its timers slowed to once a second or worse; messages go on.
const beat = new MessageChannel();
beat.port1.onmessage = tick;

function tick(): void {
  if (mode !== 'free') {
    pumping = false;
    return;
  }
  const t = real.now();
  if (t - lastReal >= FRAME_MS && !busy) {
    // A long stall (a big model loading) is not replayed as a burst of frames.
    const ms = Math.min(100, t - lastReal);
    lastReal = t;
    busy = true;
    void advance(ms).finally(() => {
      busy = false;
    });
  }
  beat.port2.postMessage(null);
}
let pumping = false;

export const clock = {
  /** The page's time in milliseconds, as performance.now gives it. */
  now: (): number => now,
  get mode(): 'free' | 'stepped' {
    return mode;
  },
  /** Time follows real time again. */
  free(): void {
    mode = 'free';
    lastReal = real.now();
    if (!pumping) {
      pumping = true;
      beat.port2.postMessage(null);
    }
  },
  /** Time stops; only step() moves it. Resolves once a frame the free clock had started is over. */
  async stop(): Promise<void> {
    mode = 'stepped';
    while (busy || pumping) await settle();
  },
  /** One frame of `ms` (1/60 s by default), with everything it sets going. */
  async step(ms = FRAME_MS): Promise<void> {
    if (mode !== 'stepped') throw new Error('clock.step() needs clock.stop() first');
    await advance(ms);
  },
  /** Draws a frame without moving time (shaders compiling, shadows settling before a take). */
  async still(): Promise<void> {
    await advance(0);
  },
  /** Runs `before` ahead of each frame's drawing (the shot's camera) and `after` straight after it (composing the image). */
  onFrame(before: ((now: number) => void) | null, after: ((now: number) => void) | null): void {
    hooks.before = before;
    hooks.after = after;
  },
  /** Real time, for measuring how long the capture takes. */
  realNow: real.now,
  frameMs: FRAME_MS,
};

// ------------------------------------------------------------------ install

performance.now = () => now;
Date.now = () => Math.round(epoch + now);

window.requestAnimationFrame = (cb) => {
  const id = nextId++;
  frames.push({ id, cb });
  return id;
};
window.cancelAnimationFrame = (id) => {
  frames = frames.filter((f) => f.id !== id);
};

function timer(fn: TimerHandler, delay: number | undefined, args: unknown[], every: boolean): number {
  const id = nextId++;
  const run = typeof fn === 'function' ? () => (fn as (...a: unknown[]) => void)(...args) : () => (0, eval)(String(fn));
  const ms = Math.max(0, Number(delay) || 0);
  timers.set(id, { id, due: now + ms, fn: run, every: every ? Math.max(1, ms) : null });
  return id;
}
const clear = ((id?: number) => {
  if (id === undefined) return;
  if (!timers.delete(id)) real.clearTimeout(id);
}) as typeof window.clearTimeout;
window.setTimeout = ((fn: TimerHandler, delay?: number, ...args: unknown[]) => timer(fn, delay, args, false)) as typeof window.setTimeout;
window.setInterval = ((fn: TimerHandler, delay?: number, ...args: unknown[]) => timer(fn, delay, args, true)) as typeof window.setInterval;
window.clearTimeout = clear;
window.clearInterval = clear;

Object.defineProperty(document, 'hidden', { get: () => false, configurable: true });
Object.defineProperty(document, 'visibilityState', { get: () => 'visible', configurable: true });

const params = new URLSearchParams(location.search);
// Settings and volumes of their own (net.ts storageKey reads `perfil` when each module loads, after this one):
// forcing «alto» here must not change those of the game served on the same address.
if (!params.has('perfil')) {
  params.set('perfil', 'trailer');
  history.replaceState(null, '', `${location.pathname}?${params}${location.hash}`);
}
/** `&ss=2`: drawn at twice the size and brought down, for finer edges. */
const ratio = Math.max(1, Number(params.get('ss')) || 1);
Object.defineProperty(window, 'devicePixelRatio', { get: () => ratio, configurable: true });

// mulberry32: a small seeded generator, so a take comes out the same each time (`&semilla=N`).
let seed = (Number(params.get('semilla')) || 1) >>> 0;
Math.random = () => {
  seed = (seed + 0x6d2b79f5) >>> 0;
  let t = seed;
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
};

clock.free();
