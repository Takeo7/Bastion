// The 0.10.1's sound, rendered offline to WAV for Unity (Red; docs/UNITY-SONIDO.md): the synthesised effects of
// sounds.ts as bank.ts renders them, and the score of music.ts and the beds of ambience.ts as they play live, on a
// clock of their own. Runs in a hidden Electron page with Node (main.js), which writes the files.
import { SOUNDS, LOOP_SEAM } from '../../../packages/client/src/audio/sounds';
import { Synth } from '../../../packages/client/src/audio/synth';
import { audio } from '../../../packages/client/src/audio/engine';
import { music } from '../../../packages/client/src/audio/music';
import { ambience } from '../../../packages/client/src/audio/ambience';

const RATE = 44100;
const fs = (window as any).require('fs');
const path = (window as any).require('path');

// ------------------------------------------------------------------ effects
// bank.ts as it is, but one variant at a time: rendered all at once (its Promise.all) the filters' tails came out a
// bit or two different from one run to the next. Even so, Chromium's offline render varies by one bit (−90 dBFS)
// between runs, as it did in every game of the 0.10.1: generar.mjs --comprobar allows for it.

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

/** bank.ts normalise: the loudest 30 ms to the same level, the peak under 0.98. */
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

/** bank.ts loopBuffer: the first variant cut to the period, what ran past it faded into the head (equal power). */
function loopOf(name: string, first: AudioBuffer): Float32Array[] {
  const period = SOUNDS[name]!.loop!;
  const a = first.getChannelData(0);
  const n = Math.floor(period * RATE);
  const seam = Math.min(a.length - n, Math.floor(LOOP_SEAM * RATE));
  const d = new Float32Array(n);
  d.set(a.subarray(0, n));
  for (let i = 0; i < seam; i++) {
    const w = (i / seam) * (Math.PI / 2);
    d[i] = a[i]! * Math.sin(w) + a[n + i]! * Math.cos(w);
  }
  return [d];
}

/** 16-bit PCM WAV of `channels` (one or two). */
function wav(channels: Float32Array[], rate = RATE): Uint8Array {
  const n = channels[0]!.length;
  const k = channels.length;
  const out = new DataView(new ArrayBuffer(44 + n * k * 2));
  const text = (o: number, s: string) => {
    for (let i = 0; i < s.length; i++) out.setUint8(o + i, s.charCodeAt(i));
  };
  text(0, 'RIFF');
  out.setUint32(4, 36 + n * k * 2, true);
  text(8, 'WAVE');
  text(12, 'fmt ');
  out.setUint32(16, 16, true);
  out.setUint16(20, 1, true);
  out.setUint16(22, k, true);
  out.setUint32(24, rate, true);
  out.setUint32(28, rate * k * 2, true);
  out.setUint16(32, k * 2, true);
  out.setUint16(34, 16, true);
  text(36, 'data');
  out.setUint32(40, n * k * 2, true);
  for (let i = 0; i < n; i++)
    for (let c = 0; c < k; c++) out.setInt16(44 + (i * k + c) * 2, Math.max(-32768, Math.min(32767, Math.round(channels[c]![i]! * 32767))), true);
  return new Uint8Array(out.buffer);
}

async function effects(dir: string): Promise<Record<string, unknown>> {
  fs.mkdirSync(dir, { recursive: true });
  const list: Record<string, unknown> = {};
  for (const [name, def] of Object.entries(SOUNDS)) {
    const files: string[] = [];
    let first: AudioBuffer | null = null;
    for (let v = 0; v < (def.variants ?? 3); v++) {
      const buf = await renderOne(name, v);
      first ??= buf;
      const file = `${name}.${v}.wav`;
      fs.writeFileSync(path.join(dir, file), wav([buf.getChannelData(0)]));
      files.push(file);
    }
    let loop: string | undefined;
    if (def.loop) {
      loop = `${name}.bucle.wav`;
      fs.writeFileSync(path.join(dir, loop), wav(loopOf(name, first!)));
    }
    list[name] = { bus: def.bus, level: def.level ?? 0, reverb: def.reverb ?? 0, files, loop: def.loop, loopFile: loop };
  }
  return list;
}

// ------------------------------------------------------------- live pieces
// music.ts and ambience.ts write themselves as they play, on the context's clock and window's timers, with
// Math.random. Here the context is an OfflineAudioContext stopped every STEP of its own time; at each stop the
// timers due run (the page's own, replaced), on a seeded Math.random: the same piece every time.

const STEP = 0.05;
/** Played before the part kept: the piece's own fade-in, and its layers all going. */
const WARM = 12;
/** Played past the loop's length and faded into its head (equal power): the end runs on into the start. */
const SEAM = 6;

interface Timer {
  at: number;
  fn: () => void;
  every: number;
}

function seeded(seed: number): void {
  const r = rng(seed);
  Math.random = r;
}

async function live(kind: 'music' | 'ambience', name: string, seconds: number, seed: number): Promise<Float32Array[]> {
  seeded(seed);
  const total = WARM + seconds + SEAM;
  const off = new OfflineAudioContext(2, Math.ceil(total * RATE), RATE);
  const bus = off.createGain();
  bus.connect(off.destination);
  const eng = audio as any;
  eng.ctx = off;
  eng.buses = new Map([[kind, bus]]);
  // No reverb send here: music.ts sends 0.35 of its output, which Unity's engine does with the shared reverb.
  eng.wets = new Map();
  const timers = new Map<number, Timer>();
  let id = 1;
  const w = window as any;
  w.setTimeout = (fn: () => void, ms = 0) => {
    timers.set(id, { at: off.currentTime + ms / 1000, fn, every: 0 });
    return id++;
  };
  w.setInterval = (fn: () => void, ms = 0) => {
    timers.set(id, { at: off.currentTime + ms / 1000, fn, every: Math.max(ms, 1) / 1000 });
    return id++;
  };
  w.clearTimeout = w.clearInterval = (n: number) => timers.delete(n);
  const due = () => {
    for (let guard = 0; guard < 10000; guard++) {
      let next: [number, Timer] | null = null;
      for (const e of timers) if (e[1].at <= off.currentTime + 1e-9 && (!next || e[1].at < next[1].at)) next = e;
      if (!next) return;
      const [n, t] = next;
      if (t.every) t.at += t.every;
      else timers.delete(n);
      t.fn();
    }
  };
  // music.ts keeps its interval while it exists: the last piece's, on the last clock. Each piece starts its own.
  (music as any).timer = 0;
  if (kind === 'music') music.play(name as any, 0.01);
  else ambience.play(name as any, 0.01);
  due();
  const stop = (t: number): void => {
    void off.suspend(t).then(() => {
      due();
      if (t + STEP < total) stop(t + STEP);
      void off.resume();
    });
  };
  stop(STEP);
  const buf = await off.startRendering();
  // Silence after: the next piece starts from nothing.
  if (kind === 'music') music.play(null, 0.01);
  else ambience.play(null, 0.01);
  const a = WARM * RATE;
  const n = Math.floor(seconds * RATE);
  const seam = Math.floor(SEAM * RATE);
  return [0, 1].map((c) => {
    const src = buf.getChannelData(c);
    const d = new Float32Array(n);
    d.set(src.subarray(a, a + n));
    for (let i = 0; i < seam; i++) {
      const x = (i / seam) * (Math.PI / 2);
      d[i] = src[a + i]! * Math.sin(x) + src[a + n + i]! * Math.cos(x);
    }
    return d;
  });
}

async function pieces(dir: string, list: { kind: 'music' | 'ambience'; name: string; seconds: number; seed: number }[]): Promise<Record<string, unknown>> {
  fs.mkdirSync(dir, { recursive: true });
  // Never hidden here: music.ts and ambience.ts write nothing while the page is.
  Object.defineProperty(Document.prototype, 'hidden', { get: () => false, configurable: true });
  const out: Record<string, unknown> = {};
  for (const p of list) {
    const file = `${p.kind === 'music' ? 'musica' : 'ambiente'}.${p.name}.wav`;
    fs.writeFileSync(path.join(dir, file), wav(await live(p.kind, p.name, p.seconds, p.seed)));
    out[`${p.kind}.${p.name}`] = { file, seconds: p.seconds, seed: p.seed };
  }
  return out;
}

(window as any).renderEffects = effects;
(window as any).renderPieces = pieces;
