// Graphics settings: per player, kept in this browser, applied live to every
// 3D view (battle and strategy stage). They trade looks for performance on
// modest machines: a frame-rate cap, lighter post-processing, no shadows and a
// lower rendering resolution.
import { storageKey } from './net';

export type FpsCap = 0 | 30 | 60 | 120;
/** high: ambient occlusion and bloom · medium: bloom only · low: no post-processing. */
export type EffectsLevel = 'high' | 'medium' | 'low';
/** The old «Velocidad del combate» (docs/UX-RITMO.md): every action as long ×1.4, ×1 or ×0.6; now enemySpeed's rate. */
export type CombatSpeed = 'slow' | 'normal' | 'fast';
export const COMBAT_RATES: Record<CombatSpeed, number> = { slow: 1 / 1.4, normal: 1, fast: 1 / 0.6 };

export interface GraphicsSettings {
  /** Frames per second at most; 0 for no cap. */
  fps: FpsCap;
  effects: EffectsLevel;
  shadows: boolean;
  /** Fraction of the screen's pixel density rendered (1, 0.75 or 0.5); 0 for automatic. */
  resolution: number;
  showFps: boolean;
  /** Interface size (0.9 to 1.3): scales the HUD and the base's sheets, not the 3D view. */
  uiScale: number;
  /** «Velocidad del turno enemigo» (playtest 18/10 #6): how fast it plays, as a rate (1 normal, ENEMY_SPEED's range). */
  enemySpeed: number;
  /** «Seguir las rutas planeadas» (playtest 18/10 #3): a route planned without the command is walked when it comes. */
  autoPlan: boolean;
}

/** The enemy turn's speed slider: from half as fast to two and a half times. */
export const ENEMY_SPEED = { min: 0.5, max: 2.5, step: 0.1 } as const;

const DEFAULTS: GraphicsSettings = { fps: 0, effects: 'high', shadows: true, resolution: 0, showFps: false, uiScale: 1, enemySpeed: 1, autoPlan: true };
const KEY = storageKey('graphics');
/** Version of the stored settings: 2 made the resolution automatic by default. */
const VERSION = 2;

function load(): GraphicsSettings {
  try {
    const stored = JSON.parse(localStorage.getItem(KEY) ?? '{}') as Partial<GraphicsSettings> & { v?: number };
    // Before version 2, "100 %" was simply the default: move it to automatic.
    if (stored.v !== VERSION && stored.resolution === 1) delete stored.resolution;
    delete stored.v;
    // The «Modelos» choice (Synty or voxel) went with the voxel models.
    delete (stored as { models?: unknown }).models;
    // The phone's effects («Móvil», 0.5.0) went with the phone: the nearest level is the medium one.
    if ((stored.effects as string | undefined) === 'mobile') stored.effects = 'medium';
    // And its touch controls (0.5.0), with their setting.
    delete (stored as { touch?: unknown }).touch;
    // «Velocidad del combate» (three steps, for everything) became the enemy turn's slider: its rate carries over.
    const old = (stored as { combatSpeed?: CombatSpeed }).combatSpeed;
    if (old && stored.enemySpeed === undefined) stored.enemySpeed = COMBAT_RATES[old] ?? 1;
    delete (stored as { combatSpeed?: unknown }).combatSpeed;
    return { ...DEFAULTS, ...stored };
  } catch {
    return { ...DEFAULTS };
  }
}

let current = load();
const listeners = new Set<(g: GraphicsSettings) => void>();

export function graphics(): GraphicsSettings {
  return current;
}

export function setGraphics(patch: Partial<GraphicsSettings>): void {
  current = { ...current, ...patch };
  try {
    localStorage.setItem(KEY, JSON.stringify({ ...current, v: VERSION }));
  } catch {
    /* private mode: the settings last until the page closes */
  }
  // What the player picks is theirs: the frame-rate guard steps back from it.
  if (patch.effects !== undefined) saveAuto({ ...auto, effectsChosen: true, ao: 0, bloom: 0 });
  if (patch.resolution !== undefined) saveAuto({ ...auto, resolution: 0 });
  notify();
}

function notify(): void {
  for (const fn of listeners) fn(current);
  fpsCounter.classList.toggle('show', current.showFps);
  applyUiScale();
}

/** The interface size goes to CSS as --ui-scale (styles.css and strategy.css apply it). */
function applyUiScale(): void {
  document.documentElement.style.setProperty('--ui-scale', String(current.uiScale));
}

/** Calls `fn` now and on every change; returns the unsubscribe. */
export function onGraphics(fn: (g: GraphicsSettings) => void): () => void {
  listeners.add(fn);
  fn(current);
  return () => listeners.delete(fn);
}

/**
 * Pixels a frame may have in automatic resolution: about a 1440p screen. The
 * post-processing reads and writes every pixel several times, so its cost
 * grows with the window: a large Retina window has twice as many and ran at
 * 44 FPS on an M4.
 */
const PIXEL_BUDGET = 2560 * 1600;

/**
 * Pixel ratio to render a `width` × `height` view at: the screen's (capped at
 * 2) scaled by the resolution setting. Automatic keeps the screen's density
 * unless that goes over the budget, and then lowers it, never below 1. Only the
 * 3D view changes: the interface is HTML and stays sharp. `adaptive`: the view
 * the frame-rate guard watches (the battle), where Auto also takes its steps.
 */
export function pixelRatio(width: number, height: number, adaptive = false): number {
  const screen = Math.min(window.devicePixelRatio, 2);
  if (current.resolution > 0) return screen * current.resolution;
  const fit = Math.sqrt(PIXEL_BUDGET / Math.max(1, width * height));
  const steps = adaptive && auto.on ? auto.resolution : 0;
  return Math.min(screen, Math.max(1, fit)) * RESOLUTION_STEPS[steps]!;
}

// ---------------------------------------------------------- adaptive quality

/**
 * What the frame-rate guard has taken off on this machine (playtest: «me va a
 * 25 fps», Ana on a Mac), only from what the player left as it came: steps of
 * the automatic resolution, of the ambient occlusion and of the bloom. Kept
 * between missions, so a slow machine starts the next one already light, and
 * never raised again by itself: the player can, in Ajustes.
 */
export interface AutoQuality {
  /** The guard may lower the quality (Ajustes → «Ajuste auto»). */
  on: boolean;
  /** Steps of the automatic resolution (RESOLUTION_STEPS). */
  resolution: number;
  /** Ambient occlusion: 0 as it comes, 1 lighter, 2 off. */
  ao: number;
  /** Bloom: 0 as it comes, 1 lighter. */
  bloom: number;
  /** The player picked the effects level in Ajustes: the guard leaves them alone. */
  effectsChosen: boolean;
}

/** The automatic resolution, as the guard lowers it. */
const RESOLUTION_STEPS = [1, 0.8, 0.65];
/** The guard's steps, lightest loss first: a little resolution, then the occlusion, then the bloom. */
const LADDER: [keyof Pick<AutoQuality, 'resolution' | 'ao' | 'bloom'>, number][] = [
  ['resolution', 1],
  ['ao', 1],
  ['resolution', 2],
  ['ao', 2],
  ['bloom', 1],
];
const AUTO_KEY = storageKey('quality');

function loadAuto(): AutoQuality {
  // Effects set before the guard existed, if not the default, were a choice.
  const fresh: AutoQuality = { on: true, resolution: 0, ao: 0, bloom: 0, effectsChosen: current.effects !== 'high' };
  try {
    return { ...fresh, ...(JSON.parse(localStorage.getItem(AUTO_KEY) ?? '{}') as Partial<AutoQuality>) };
  } catch {
    return fresh;
  }
}

let auto = loadAuto();

function saveAuto(next: AutoQuality): void {
  auto = next;
  try {
    localStorage.setItem(AUTO_KEY, JSON.stringify(auto));
  } catch {
    /* private mode */
  }
}

export function autoQuality(): AutoQuality {
  return auto;
}

/** Turns the guard on or off; off gives back everything it took. */
export function setAutoQuality(on: boolean): void {
  saveAuto(on ? { ...auto, on } : { ...auto, on, resolution: 0, ao: 0, bloom: 0 });
  notify();
}

/** Whether a step of the ladder would change anything with the current settings. */
function applies([key, level]: (typeof LADDER)[number]): boolean {
  if (auto[key] >= level) return false;
  if (key === 'resolution') return current.resolution === 0;
  if (auto.effectsChosen) return false;
  return key === 'ao' ? current.effects === 'high' : current.effects !== 'low';
}

/** Takes the next step down that changes something; false when there is none left. */
export function lowerQuality(): boolean {
  const step = LADDER.find(applies);
  if (!step || !auto.on) return false;
  saveAuto({ ...auto, [step[0]]: step[1] });
  notify();
  return true;
}

/** How the battle's post-processing runs: the effects setting, less what the guard took. */
export function postQuality(): { ao: 'full' | 'light' | 'off'; bloom: 'full' | 'light' | 'off' } {
  const taken = auto.on && !auto.effectsChosen ? auto : { ao: 0, bloom: 0 };
  return {
    ao: current.effects !== 'high' || taken.ao >= 2 ? 'off' : taken.ao === 1 ? 'light' : 'full',
    bloom: current.effects === 'low' ? 'off' : taken.bloom >= 1 ? 'light' : 'full',
  };
}

/** What the guard has lowered, in Ajustes' words, or null if nothing. */
export function autoQualityNote(): string | null {
  if (!auto.on) return null;
  const q = postQuality();
  const parts = [
    current.resolution === 0 && auto.resolution ? `resolución al ${Math.round(RESOLUTION_STEPS[auto.resolution]! * 100)} %` : '',
    !auto.effectsChosen && current.effects === 'high' ? (q.ao === 'off' ? 'sin oclusión ambiental' : q.ao === 'light' ? 'oclusión ambiental más ligera' : '') : '',
    !auto.effectsChosen && q.bloom === 'light' ? 'brillo más ligero' : '',
  ].filter(Boolean);
  return parts.length ? `Bajado para ir fluido: ${parts.join(', ')}.` : null;
}

/** Below this, a mission is not fluid: the guard steps in. */
const SLOW_FPS = 45;
/** Later on, it takes two windows in a row under this. */
const WATCH_FPS = 38;
/** Nothing counts in the first seconds (shaders compiling, the map being built) nor right after a step. */
const WARM_UP_MS = 2500;
const SETTLE_MS = 1500;
/** The first seconds of a mission are watched closely, in short windows; then in long ones. */
const CLOSE_WATCH_MS = 25_000;
const SHORT_WINDOW_MS = 3000;
const LONG_WINDOW_MS = 8000;
/** A frame this long is a hitch (or a throttled tab), not the frame rate. */
const HITCH_MS = 250;

/**
 * Watches a mission's frame rate. It times the frames for a few seconds and,
 * well under 45 FPS, takes one step off (`lowerQuality`), then measures
 * again; after the first seconds it keeps watching gently. A hidden or
 * unfocused page, a frame-rate cap under the bar and hitches don't count, and
 * the average drops the fastest and slowest tenth of the frames.
 */
export class QualityGuard {
  private readonly born = performance.now();
  private resumeAt = this.born + WARM_UP_MS;
  private last = 0;
  private times: number[] = [];
  private spent = 0;
  private slowWindows = 0;

  /** `onLower`: a step was taken (the view must apply it). */
  constructor(private readonly onLower: () => void) {}

  /** Called with every frame drawn. */
  frame(now: number): void {
    // Only while the player is at it: a hidden or unfocused page may be throttled.
    if (document.hidden || !document.hasFocus() || !auto.on || (current.fps > 0 && current.fps < SLOW_FPS)) {
      this.last = 0;
      return;
    }
    const dt = this.last ? now - this.last : 0;
    this.last = now;
    if (!dt || dt > HITCH_MS || now < this.resumeAt) return;
    this.times.push(dt);
    this.spent += dt;
    const close = now - this.born < CLOSE_WATCH_MS;
    if (this.spent < (close ? SHORT_WINDOW_MS : LONG_WINDOW_MS)) return;
    const sorted = this.times.sort((a, b) => a - b);
    const kept = sorted.slice(Math.floor(sorted.length * 0.1), Math.ceil(sorted.length * 0.9));
    const fps = (1000 * kept.length) / kept.reduce((sum, t) => sum + t, 0);
    this.times = [];
    this.spent = 0;
    this.slowWindows = fps < (close ? SLOW_FPS : WATCH_FPS) ? this.slowWindows + 1 : 0;
    if (this.slowWindows < (close ? 1 : 2)) return;
    this.slowWindows = 0;
    if (!lowerQuality()) return;
    this.resumeAt = now + SETTLE_MS;
    this.onLower();
  }
}

/**
 * Frame-rate cap for a render loop driven by requestAnimationFrame: `ready()`
 * says whether this tick should draw. Skipped ticks cost almost nothing.
 */
export class FrameLimiter {
  private last = 0;

  ready(now = performance.now()): boolean {
    const cap = current.fps;
    if (cap) {
      const step = 1000 / cap;
      // A small tolerance, so a 60 cap on a 60 Hz screen never drops frames.
      if (now - this.last < step - 2) return false;
      // Keep to the cap's own beat, not the tick's: a 60 cap on a 144 Hz screen
      // then draws every 2nd or 3rd tick and averages 60, not 48. After a pause
      // it starts afresh instead of catching up.
      this.last = Math.max(this.last + step, now - step);
    } else this.last = now;
    countFrame(now);
    return true;
  }
}

// --------------------------------------------------------------- FPS counter

const fpsCounter = document.createElement('div');
fpsCounter.className = 'fps-counter';
fpsCounter.classList.toggle('show', current.showFps);
document.body.append(fpsCounter);
applyUiScale();

let frames = 0;
let windowStart = 0;

function countFrame(now: number): void {
  frames++;
  if (now - windowStart < 1000) return;
  if (current.showFps) fpsCounter.textContent = `${Math.round((frames * 1000) / (now - windowStart))} FPS`;
  frames = 0;
  windowStart = now;
}
