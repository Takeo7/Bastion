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
  /** Fraction of the screen's pixel density rendered (1, 0.75 or 0.5). */
  resolution: number;
  showFps: boolean;
}

const DEFAULTS: GraphicsSettings = { fps: 0, effects: 'high', shadows: true, resolution: 1, showFps: false };
const KEY = storageKey('graphics');

function load(): GraphicsSettings {
  try {
    const stored = JSON.parse(localStorage.getItem(KEY) ?? '{}') as Partial<GraphicsSettings>;
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
    localStorage.setItem(KEY, JSON.stringify(current));
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

/** Pixel ratio to render at: the screen's (capped at 2) scaled by the resolution setting. */
export function pixelRatio(): number {
  return Math.min(window.devicePixelRatio, 2) * current.resolution;
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
