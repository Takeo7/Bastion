// How the 3D view runs in each player's browser, told to the host: every ten
// seconds of play, one line with the frame rate and the screen's refresh, the
// time the frame takes on the processor and on the graphics card, the draw
// calls and triangles, the graphics card, the graphics settings and the size,
// so a partner's machine can be measured without them doing anything. The
// server prints it in its terminal and in data/red.log (report.ts, __report).
//
// Each render loop calls perfBegin() once the frame is going to be drawn and
// perfFrame() right after drawing it. A frame costs a few reads and sums, a
// slot of a ring buffer and, where the browser has them, a graphics-card
// timer query from a small pool: no allocations, no timers; a hidden tab
// draws nothing and counts nothing.
import type { WebGLRenderer } from 'three';
import { storageKey } from './net';
import { post } from './report';
import { autoQuality, graphics, postQuality } from './settings';

/** Milliseconds of frames per sample. */
const WINDOW_MS = 10_000;
/** After a change of view or a return to the tab, shaders compile and scenes build: those frames wait. */
const WARM_UP_MS = 2000;
/** On a change of view, what was measured of the last one goes out if it was at least this long. */
const FLUSH_MS = 3000;
/** A frame this long is a hitch the player sees. */
const HITCH_MS = 50;
/** A gap this long is a pause (the loop stopped, the page hung): it is no frame. */
const PAUSE_MS = 1000;
/** Times kept per sample: ten seconds at 240 Hz. Beyond that the oldest are overwritten. */
const RING = 2400;
/** Graphics-card timer queries per renderer: their results come a few frames late. */
const POOL = 8;

const NAME_KEY = storageKey('name');
/** The stage's sets, in the players' words; any other view goes as it is named. */
const VIEWS: Record<string, string> = { bastion: 'base', geo: 'globo' };
const EFFECTS = { high: 'altos', medium: 'medios', low: 'bajos' } as const;

/** Time between frames drawn, as performance.now() sees it at their end. */
const intervals = new Float32Array(RING);
/** Time between the animation-frame timestamps of frames drawn: steps of the screen's refresh. */
const ticks = new Float32Array(RING);
/** Our loop's own time per frame: update and drawing (perfBegin to perfFrame). */
const cpuTimes = new Float32Array(RING);
/** The graphics card's time per frame (timer queries). */
const gpuTimes = new Float32Array(RING);

/** The view drawing now, its renderer and its timer queries. */
let view = '';
let renderer: WebGLRenderer | null = null;
let timer: GpuTimer | null = null;
let last = 0;
let lastTick = 0;
let begun = 0;
let warmUntil = 0;
/** The line saying the view changed has gone out. */
let announced = true;
let gpu = '';
/** Counts the samples, so a timer query's late result lands in the one it was taken for. */
let generation = 0;

// The sample being gathered: frames counted and their sums.
let frames = 0;
let spent = 0;
let hitches = 0;
let tickCount = 0;
let cpuCount = 0;
let cpuSum = 0;
let gpuCount = 0;
let gpuSum = 0;
let calls = 0;
let triangles = 0;
let shadowPasses = 0;
let shadowCalls = 0;
let shadowTriangles = 0;

// What the shadow map drew since the last frame (watchShadows).
let framePasses = 0;
let frameShadowCalls = 0;
let frameShadowTriangles = 0;

const watched = new WeakSet<WebGLRenderer>();

/**
 * Counts what each renderer's shadow map draws. It is drawn inside every
 * `render()` of a scene with a light casting shadows: also inside a
 * post-processing pass that renders the scene again (the occlusion's normals).
 */
function watchShadows(gl: WebGLRenderer): void {
  if (watched.has(gl)) return;
  watched.add(gl);
  const map = gl.shadowMap;
  const render = map.render;
  map.render = (lights, scene, camera) => {
    const info = gl.info.render;
    const c = info.calls;
    const t = info.triangles;
    render.call(map, lights, scene, camera);
    if (info.calls === c) return;
    framePasses++;
    frameShadowCalls += info.calls - c;
    frameShadowTriangles += info.triangles - t;
  };
}

// -------------------------------------------------------- graphics-card time

interface TimerExt {
  TIME_ELAPSED_EXT: GLenum;
  GPU_DISJOINT_EXT: GLenum;
}

/**
 * A renderer's pool of timer queries (EXT_disjoint_timer_query_webgl2: Chrome
 * on Windows and others; never Safari). Each frame drawn is wrapped in one;
 * the results are read when ready, a few frames later, oldest first.
 */
interface GpuTimer {
  ctx: WebGL2RenderingContext;
  ext: TimerExt;
  queries: WebGLQuery[];
  /** The sample each pending query belongs to, or -1 when its frame doesn't count. */
  owner: Int32Array;
  /** Next slot to start, oldest slot pending, and how many are pending. */
  head: number;
  tail: number;
  pending: number;
  /** A query is running: the frame being drawn. */
  running: boolean;
}

const timers = new WeakMap<WebGLRenderer, GpuTimer | null>();

function gpuTimer(gl: WebGLRenderer): GpuTimer | null {
  let t = timers.get(gl);
  if (t !== undefined) return t;
  t = null;
  try {
    const ctx = gl.getContext();
    const ext = ctx instanceof WebGL2RenderingContext ? (ctx.getExtension('EXT_disjoint_timer_query_webgl2') as TimerExt | null) : null;
    if (ctx instanceof WebGL2RenderingContext && ext) {
      const queries: WebGLQuery[] = [];
      for (let i = 0; i < POOL; i++) queries.push(ctx.createQuery()!);
      t = { ctx, ext, queries, owner: new Int32Array(POOL).fill(-1), head: 0, tail: 0, pending: 0, running: false };
    }
  } catch {
    t = null;
  }
  timers.set(gl, t);
  return t;
}

/** Stops the running query; the slot it used, or -1. */
function stopQuery(t: GpuTimer): number {
  if (!t.running) return -1;
  t.ctx.endQuery(t.ext.TIME_ELAPSED_EXT);
  t.running = false;
  const slot = t.head;
  t.owner[slot] = -1;
  t.head = (t.head + 1) % POOL;
  t.pending++;
  return slot;
}

/** Takes the results that are ready. A disjoint event (the card changed clocks, or was reset) spoils all pending ones. */
function readQueries(t: GpuTimer): void {
  while (t.pending > 0 && !t.running) {
    const query = t.queries[t.tail]!;
    if (!t.ctx.getQueryParameter(query, t.ctx.QUERY_RESULT_AVAILABLE)) return;
    if (t.ctx.getParameter(t.ext.GPU_DISJOINT_EXT)) {
      t.tail = t.head;
      t.pending = 0;
      return;
    }
    const ns = t.ctx.getQueryParameter(query, t.ctx.QUERY_RESULT) as number;
    if (t.owner[t.tail] === generation && gpuCount < RING) {
      gpuTimes[gpuCount++] = ns / 1e6;
      gpuSum += ns / 1e6;
    }
    t.tail = (t.tail + 1) % POOL;
    t.pending--;
  }
}

// ------------------------------------------------------------------ frames

function restart(): void {
  frames = spent = hitches = tickCount = cpuCount = cpuSum = gpuCount = gpuSum = 0;
  calls = triangles = shadowPasses = shadowCalls = shadowTriangles = 0;
  generation++;
}

/** Call when the loop is about to update and draw a frame (after any frame-rate cap has let it through). */
export function perfBegin(gl: WebGLRenderer): void {
  begun = performance.now();
  if (gl !== renderer || !timer || document.hidden) return;
  // A frame that threw before perfFrame left its query running.
  stopQuery(timer);
  if (timer.pending >= POOL) return;
  timer.ctx.beginQuery(timer.ext.TIME_ELAPSED_EXT, timer.queries[timer.head]!);
  timer.running = true;
}

/**
 * Call once per frame drawn, right after drawing, with the view's renderer and
 * its name ('combate'; the stage passes its set, or 'menú').
 */
export function perfFrame(gl: WebGLRenderer, where: string): void {
  const now = performance.now();
  const work = begun ? now - begun : -1;
  begun = 0;
  const slot = gl === renderer && timer ? stopQuery(timer) : -1;
  const info = gl.info;
  // By default the counts restart at every render() call, so after a post-processing chain they
  // would hold only its last pass: here they add up over the whole frame, and restart each frame.
  if (info.autoReset) {
    info.autoReset = false;
    watchShadows(gl);
  }
  const frameCalls = info.render.calls;
  const frameTriangles = info.render.triangles;
  info.reset();
  const passes = framePasses;
  const sCalls = frameShadowCalls;
  const sTriangles = frameShadowTriangles;
  framePasses = frameShadowCalls = frameShadowTriangles = 0;
  if (document.hidden) {
    last = lastTick = 0;
    return;
  }
  if (gl !== renderer || where !== view) {
    changeView(gl, where, now);
    return;
  }
  const tick = document.timeline?.currentTime;
  const tickDt = typeof tick === 'number' && lastTick ? tick - lastTick : 0;
  lastTick = typeof tick === 'number' ? tick : 0;
  const dt = last ? now - last : 0;
  last = now;
  const counted = dt > 0 && dt <= PAUSE_MS && now >= warmUntil;
  if (timer) {
    if (counted && slot >= 0) timer.owner[slot] = generation;
    readQueries(timer);
  }
  if (!counted) return;
  if (!announced) announce(gl);
  intervals[frames % RING] = dt;
  frames++;
  spent += dt;
  if (dt >= HITCH_MS) hitches++;
  if (tickDt > 0 && tickDt < PAUSE_MS) ticks[tickCount++ % RING] = tickDt;
  if (work >= 0 && work < PAUSE_MS) {
    cpuTimes[cpuCount++ % RING] = work;
    cpuSum += work;
  }
  calls += frameCalls;
  triangles += frameTriangles;
  shadowPasses += passes;
  shadowCalls += sCalls;
  shadowTriangles += sTriangles;
  if (spent >= WINDOW_MS) {
    sample();
    restart();
  }
}

// A tab back from hidden: the time it spent away is no frame.
document.addEventListener('visibilitychange', () => {
  last = lastTick = 0;
  if (!document.hidden) warmUntil = performance.now() + WARM_UP_MS / 2;
});

/** What went before goes out if it is worth it, and the new view starts warming up. */
function changeView(gl: WebGLRenderer, where: string, now: number): void {
  if (renderer && spent >= FLUSH_MS) sample();
  restart();
  renderer = gl;
  view = where;
  timer = gpuTimer(gl);
  last = lastTick = 0;
  warmUntil = now + WARM_UP_MS;
  announced = false;
}

/**
 * One line says where the player went, once the view has settled (its size
 * is set by then). Two views drawing by turns would never settle: no lines.
 */
function announce(gl: WebGLRenderer): void {
  announced = true;
  const cores = navigator.hardwareConcurrency ? ` · ${navigator.hardwareConcurrency} núcleos` : '';
  send(`→ ${label()} · ${gpuName(gl)} · ${settings()} · ${size(gl)}${cores}`);
}

/**
 * One line: frames per second and the refresh that caps them, the slowest
 * frames, our loop's time and the graphics card's (mean / 95th percentile),
 * what was drawn per frame, and the machine and settings.
 */
function sample(): void {
  if (!renderer || !frames) return;
  const fps = Math.round((1000 * frames) / spent);
  const n = Math.min(frames, RING);
  const p95 = percentile(intervals, n, 0.95);
  // With vsync every gap is one or more refreshes: the short ones are the screen's, even when the game is slow.
  const cap = tickCount ? ` (tope ${Math.round(1000 / percentile(ticks, Math.min(tickCount, RING), 0.1))} Hz)` : '';
  const slow = hitches ? ` · ${hitches} ${hitches === 1 ? 'tirón' : 'tirones'}` : '';
  const cpu = cpuCount ? `${ms(cpuSum / cpuCount)}/${ms(percentile(cpuTimes, Math.min(cpuCount, RING), 0.95))} ms` : 'n/d';
  const gpu = gpuCount ? `${ms(gpuSum / gpuCount)}/${ms(percentile(gpuTimes, gpuCount, 0.95))} ms` : 'n/d';
  const shadows = shadowPasses
    ? ` · sombras ${round(shadowPasses / frames)}× (${amount(shadowCalls / frames)} llamadas, ${amount(shadowTriangles / frames)} tri)`
    : '';
  send(
    `${label()} ${fps} fps${cap} · p95 ${ms(p95)} ms${slow} · cpu ${cpu} · gpu ${gpu}` +
      ` · ${amount(calls / frames)} llamadas · ${amount(triangles / frames)} tri${shadows}` +
      ` · ${gpuName(renderer)} · ${settings()} · ${size(renderer)}`,
  );
}

/** The `q` quantile of the first `n` values (sorts them in place: the sample is over). */
function percentile(values: Float32Array, n: number, q: number): number {
  const sorted = values.subarray(0, n).sort();
  return sorted[Math.min(n - 1, Math.floor(n * q))]!;
}

function label(): string {
  return VIEWS[view] ?? view;
}

/** One line to the host's terminal, tagged with this player's name (the server puts the address if there is none). */
function send(text: string): void {
  let name = '';
  try {
    name = (localStorage.getItem(NAME_KEY) ?? '').trim().slice(0, 24);
  } catch {
    /* private mode */
  }
  post(`[rend${name ? ` ${name}` : ''}] ${text}`);
}

/** The graphics settings in short: the effects level, what differs from the defaults, and what the guard took in battle. */
function settings(): string {
  const g = graphics();
  const parts = [`efectos ${EFFECTS[g.effects]}`];
  if (!g.shadows) parts.push('sin sombras');
  if (g.resolution) parts.push(`res ${Math.round(g.resolution * 100)} %`);
  if (g.fps) parts.push(`límite ${g.fps} fps`);
  // The frame-rate guard only works on the battle (settings.ts).
  if (view !== 'combate') return parts.join(', ');
  const a = autoQuality();
  if (!a.on) return [...parts, 'sin ajuste auto'].join(', ');
  const q = postQuality();
  const taken = [
    a.resolution && !g.resolution ? `res −${a.resolution}` : '',
    q.ao === 'light' ? 'AO ligera' : q.ao === 'off' && g.effects === 'high' ? 'sin AO' : '',
    q.bloom === 'light' ? 'brillo ligero' : '',
  ].filter(Boolean);
  return parts.join(', ') + (taken.length ? ` (auto: ${taken.join(', ')})` : '');
}

/** The view's size in CSS pixels, the density it is drawn at and, if different, the screen's. */
function size(gl: WebGLRenderer): string {
  const ratio = gl.getPixelRatio();
  const canvas = gl.domElement;
  const screen = window.devicePixelRatio;
  const own = Math.abs(ratio - screen) < 0.01 ? '' : ` (pantalla ×${round(screen)})`;
  return `${Math.round(canvas.width / ratio)}×${Math.round(canvas.height / ratio)} ×${round(ratio)}${own}`;
}

/**
 * The graphics card, short: «ANGLE (Apple, ANGLE Metal Renderer: Apple M1,
 * Unspecified Version)» is «Apple M1». Safari only ever says «Apple GPU».
 */
function gpuName(gl: WebGLRenderer): string {
  if (gpu) return gpu;
  let raw = '';
  try {
    const ctx = gl.getContext();
    const ext = ctx.getExtension('WEBGL_debug_renderer_info');
    raw = String(ctx.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : ctx.RENDERER) ?? '');
  } catch {
    /* the name helps, nothing more */
  }
  const angle = /^ANGLE \([^,]*, (.*?)(?:, [^,]*)?\)$/.exec(raw);
  const name = (angle ? angle[1]! : raw)
    .replace(/^ANGLE Metal Renderer: /, '')
    .replace(/ \(0x[0-9a-f]+\)/i, '')
    .replace(/ Direct3D.*$/, '')
    .replace(/ OpenGL Engine$/, '')
    .trim();
  gpu = (name || raw || 'tarjeta desconocida').slice(0, 48);
  return gpu;
}

/** Milliseconds: a decimal under ten. */
function ms(v: number): string {
  return v < 10 ? v.toFixed(1) : String(Math.round(v));
}

/** A count per frame: 412, 38 k, 1.1 M. */
function amount(v: number): string {
  if (v >= 1e6) return `${(v / 1e6).toFixed(1)} M`;
  if (v >= 1e4) return `${Math.round(v / 1e3)} k`;
  return String(Math.round(v));
}

function round(v: number): string {
  return String(Math.round(v * 100) / 100);
}
