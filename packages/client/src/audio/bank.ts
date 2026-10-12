import { audio, type PlayOptions } from './engine';
import { SOUNDS } from './sounds';
import { Synth } from './synth';

// Renders the recipes of sounds.ts into buffers, a few variants each, once
// the audio context exists (and in idle moments, a few at a time, so the
// first shot of a mission is already there). Loudness is evened out per
// variant before the recipe's own level, so a key click and a gunshot can be
// balanced by ear in one place.

const RATE = 44100;
const rendered = new Map<string, AudioBuffer[]>();
const pending = new Map<string, Promise<AudioBuffer[]>>();

/** Small deterministic random, so every variant renders the same each time. */
function rng(seed: number): () => number {
  let s = seed >>> 0 || 1;
  return () => {
    s = (s + 0x6d2b79f5) >>> 0;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function seedOf(name: string, variant: number): number {
  let h = 2166136261;
  for (let i = 0; i < name.length; i++) h = Math.imul(h ^ name.charCodeAt(i), 16777619);
  return h + variant * 7919;
}

/**
 * Evens out loudness: the loudest 30 ms of the sound is brought to the same
 * level for every sound, without letting the peak pass 0.98.
 */
function normalise(buf: AudioBuffer): void {
  const d = buf.getChannelData(0);
  const win = Math.floor(RATE * 0.03);
  let peak = 0;
  let loudest = 0;
  let acc = 0;
  for (let i = 0; i < d.length; i++) {
    const v = d[i]!;
    if (!Number.isFinite(v)) d[i] = 0;
    peak = Math.max(peak, Math.abs(d[i]!));
    acc += d[i]! * d[i]!;
    if (i >= win) acc -= d[i - win]! * d[i - win]!;
    if (i >= win) loudest = Math.max(loudest, acc / win);
  }
  const rms = Math.sqrt(Math.max(loudest, 1e-12));
  const gain = Math.min(0.25 / rms, 0.98 / Math.max(peak, 1e-9));
  for (let i = 0; i < d.length; i++) d[i] = d[i]! * gain;
}

async function renderOne(name: string, variant: number): Promise<AudioBuffer> {
  const def = SOUNDS[name]!;
  const ctx = new OfflineAudioContext(1, Math.ceil(def.dur * RATE), RATE);
  def.build(new Synth(ctx, rng(seedOf(name, variant))));
  const buf = await ctx.startRendering();
  normalise(buf);
  return buf;
}

/** The variants of a sound, rendering them the first time they are asked for. */
export function render(name: string): Promise<AudioBuffer[]> {
  const done = rendered.get(name);
  if (done) return Promise.resolve(done);
  let p = pending.get(name);
  if (!p) {
    const def = SOUNDS[name];
    if (!def) return Promise.reject(new Error(`No sound "${name}"`));
    p = Promise.all(Array.from({ length: def.variants ?? 3 }, (_, v) => renderOne(name, v))).then((bufs) => {
      rendered.set(name, bufs);
      pending.delete(name);
      return bufs;
    });
    pending.set(name, p);
  }
  return p;
}

/** Rendered buffers of a sound, if they are ready (for the lab's waveforms). */
export function buffers(name: string): AudioBuffer[] | undefined {
  return rendered.get(name);
}

const dB = (db: number) => Math.pow(10, db / 20);

/**
 * Plays a sound: a random variant, pitch nudged by up to ±`spread` (a
 * fraction). If it is still rendering it plays when ready, unless that takes
 * more than a fifth of a second (a late gunshot is worse than none).
 */
export function sound(name: string, o: PlayOptions & { spread?: number } = {}): void {
  const def = SOUNDS[name];
  if (!def || !audio.live()) return;
  const go = (bufs: AudioBuffer[]) => {
    const buf = bufs[Math.floor(Math.random() * bufs.length)]!;
    const spread = o.spread ?? 0.04;
    audio.play(buf, {
      bus: o.bus ?? def.bus,
      gain: (o.gain ?? 1) * dB(def.level ?? 0),
      rate: (o.rate ?? 1) * (1 + (Math.random() * 2 - 1) * spread),
      pan: o.pan,
      delay: o.delay,
      reverb: (o.reverb ?? 0) + (def.reverb ?? 0),
    });
  };
  const ready = rendered.get(name);
  if (ready) return go(ready);
  const asked = performance.now();
  void render(name).then((bufs) => {
    if (performance.now() - asked < 200) go(bufs);
  });
}

/** Renders every sound in idle moments, the most common first. */
export function prerender(first: string[] = []): void {
  const names = [...new Set([...first, ...Object.keys(SOUNDS)])];
  const next = () => {
    const name = names.shift();
    if (!name) return;
    void render(name).then(() => {
      if (typeof requestIdleCallback === 'function') requestIdleCallback(next, { timeout: 500 });
      else setTimeout(next, 16);
    });
  };
  next();
}

// Start rendering as soon as there is a context.
audio.onReady(() => prerender(['ui.key', 'ui.paper', 'ui.page', 'ui.stamp', 'ui.return', 'step', 'shot.rifle.1', 'impact.flesh', 'impact.miss']));
