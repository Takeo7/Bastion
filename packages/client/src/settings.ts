// Graphics settings: per player, kept in this browser, applied live to every
// 3D view (battle and strategy stage). They trade looks for performance on
// modest machines: a frame-rate cap, lighter post-processing, no shadows and a
// lower rendering resolution.
import { storageKey } from './net';

export type FpsCap = 0 | 30 | 60;
/** high: ambient occlusion and bloom · medium: bloom only · low: no post-processing. */
export type EffectsLevel = 'high' | 'medium' | 'low';

export interface GraphicsSettings {
  /** Frames per second at most; 0 for no cap. */
  fps: FpsCap;
  effects: EffectsLevel;
  shadows: boolean;
  /** Fraction of the screen's pixel density rendered (1, 0.75 or 0.5); 0 for automatic. */
  resolution: number;
  showFps: boolean;
}

const DEFAULTS: GraphicsSettings = { fps: 0, effects: 'high', shadows: true, resolution: 0, showFps: false };
const KEY = storageKey('graphics');
/** Version of the stored settings: 2 made the resolution automatic by default. */
const VERSION = 2;

function load(): GraphicsSettings {
  try {
    const stored = JSON.parse(localStorage.getItem(KEY) ?? '{}') as Partial<GraphicsSettings> & { v?: number };
    // Before version 2, "100 %" was simply the default: move it to automatic.
    if (stored.v !== VERSION && stored.resolution === 1) delete stored.resolution;
    delete stored.v;
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
  for (const fn of listeners) fn(current);
  fpsCounter.classList.toggle('show', current.showFps);
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
 * 3D view changes: the interface is HTML and stays sharp.
 */
export function pixelRatio(width: number, height: number): number {
  const screen = Math.min(window.devicePixelRatio, 2);
  if (current.resolution > 0) return screen * current.resolution;
  const fit = Math.sqrt(PIXEL_BUDGET / Math.max(1, width * height));
  return Math.min(screen, Math.max(1, fit));
}

/**
 * Frame-rate cap for a render loop driven by requestAnimationFrame: `ready()`
 * says whether this tick should draw. Skipped ticks cost almost nothing.
 */
export class FrameLimiter {
  private last = 0;

  ready(now = performance.now()): boolean {
    const cap = current.fps;
    // A small tolerance, so a 60 cap on a 60 Hz screen never drops frames.
    if (cap && now - this.last < 1000 / cap - 2) return false;
    this.last = now;
    countFrame(now);
    return true;
  }
}

// --------------------------------------------------------------- FPS counter

const fpsCounter = document.createElement('div');
fpsCounter.className = 'fps-counter';
fpsCounter.classList.toggle('show', current.showFps);
document.body.append(fpsCounter);

let frames = 0;
let windowStart = 0;

function countFrame(now: number): void {
  frames++;
  if (now - windowStart < 1000) return;
  if (current.showFps) fpsCounter.textContent = `${Math.round((frames * 1000) / (now - windowStart))} FPS`;
  frames = 0;
  windowStart = now;
}
