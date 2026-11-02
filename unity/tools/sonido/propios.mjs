// Sounds of our own, made rather than recorded (Gameplay 1/11, Arturo's #10: the molotov), for what neither the 0.10.1's
// bank nor a pack we have brings: the flame catching as the bottle breaks (a soft thump and a rising whoosh of noise),
// and a fire crackling, a loop that runs on without a seam (a low roar under sparse pops). Mono, 48 kHz, the same each
// time (their own random), as WAV originals in assets-src/sonido/ (out of the repository, as the downloaded ones), which
// grabados.mjs cuts and converts with the rest (its «propio-» sources).
//
//   node unity/tools/sonido/propios.mjs
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const SRC = join(resolve(HERE, '../../..'), 'assets-src', 'sonido');
const RATE = 48000;

/** mulberry32: a seed's numbers in [0, 1). */
function random(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** A one-pole low-pass at `hz` (its state kept between samples). */
function lowpass(hz) {
  const k = 1 - Math.exp((-2 * Math.PI * hz) / RATE);
  let y = 0;
  return (x) => (y += k * (x - y));
}

/** RBJ's band-pass (constant peak), its centre settable each sample. */
function bandpass(q) {
  let x1 = 0, x2 = 0, y1 = 0, y2 = 0;
  return (x, hz) => {
    const w = (2 * Math.PI * hz) / RATE, alpha = Math.sin(w) / (2 * q), a0 = 1 + alpha;
    const y = ((alpha * x) - (alpha * x2) - (-2 * Math.cos(w)) * y1 - (1 - alpha) * y2) / a0;
    x2 = x1; x1 = x;
    y2 = y1; y1 = y;
    return y;
  };
}

/** Brought to `peak` at its loudest. */
function normalise(samples, peak) {
  let most = 0;
  for (const s of samples) most = Math.max(most, Math.abs(s));
  return samples.map((s) => (s * peak) / (most || 1));
}

/** A mono float WAV. */
function wav(file, samples) {
  const data = Buffer.alloc(samples.length * 4);
  samples.forEach((s, i) => data.writeFloatLE(s, i * 4));
  const head = Buffer.alloc(44);
  head.write('RIFF', 0);
  head.writeUInt32LE(36 + data.length, 4);
  head.write('WAVEfmt ', 8);
  head.writeUInt32LE(16, 16);
  head.writeUInt16LE(3, 20); // IEEE float
  head.writeUInt16LE(1, 22);
  head.writeUInt32LE(RATE, 24);
  head.writeUInt32LE(RATE * 4, 28);
  head.writeUInt16LE(4, 32);
  head.writeUInt16LE(32, 34);
  head.write('data', 36);
  head.writeUInt32LE(data.length, 40);
  writeFileSync(file, Buffer.concat([head, data]));
  console.log(`${file} (${(samples.length / RATE).toFixed(2)} s)`);
}

/** Pops over `seconds`: about `perSecond` of them, each a short burst of bright noise dying away; one in ten deeper and longer. */
function pops(seconds, perSecond, rnd) {
  const out = new Float64Array(Math.round(seconds * RATE));
  for (let t = -Math.log(1 - rnd()) / perSecond; t < seconds; t += -Math.log(1 - rnd()) / perSecond) {
    const big = rnd() < 0.1;
    const hz = big ? 500 + 500 * rnd() : 1800 + 2500 * rnd();
    const decay = (big ? 0.012 : 0.002 + 0.004 * rnd()) * RATE;
    const amp = (big ? 0.9 : 0.25) * -Math.log(1 - rnd() * 0.95);
    const filter = bandpass(1.4);
    const start = Math.round(t * RATE);
    for (let i = 0; i < decay * 6 && start + i < out.length; i++) out[start + i] += filter((rnd() * 2 - 1) * amp * Math.exp(-i / decay), hz);
  }
  return out;
}

/** The flame catching: a thump at 60 Hz, a whoosh of noise rising from 250 Hz to 900 and falling, a rumble under, a few pops. */
function ignite() {
  const rnd = random(0x6d6f6c6f);
  const seconds = 1.4, n = Math.round(seconds * RATE);
  const whoosh = bandpass(0.8), rumble = lowpass(180);
  const crackle = pops(seconds, 12, rnd);
  const out = new Array(n);
  for (let i = 0; i < n; i++) {
    const t = i / RATE;
    const env = t < 0.06 ? (t / 0.06) ** 2 : Math.exp(-(t - 0.06) / 0.35);
    const hz = t < 0.12 ? 250 + (650 * t) / 0.12 : 900 - 500 * Math.min(1, (t - 0.12) / 1.1);
    const noise = rnd() * 2 - 1;
    const thump = Math.sin(2 * Math.PI * 60 * t) * Math.exp(-t / 0.09);
    out[i] = 0.9 * whoosh(noise, hz) * env + 2.2 * rumble(noise) * env + 0.5 * thump + (t > 0.25 ? 0.6 * crackle[i] * Math.exp(-(t - 0.25) / 0.5) : 0);
  }
  return normalise(out, 0.8);
}

/**
 * A fire burning: sparse pops over a soft low roar, slowly swelling (the pops what is heard: a roar alone reads as wind);
 * its last 0.6 s faded into its first, so it loops without a seam.
 */
function crackle() {
  const rnd = random(0x66756567);
  const seconds = 8, overlap = 0.6, n = Math.round((seconds + overlap) * RATE), m = Math.round(overlap * RATE);
  const roar = lowpass(380), floor = lowpass(70);
  const pop = pops(seconds + overlap, 22, rnd);
  let brown = 0;
  const raw = new Array(n);
  for (let i = 0; i < n; i++) {
    const t = i / RATE;
    brown = 0.995 * brown + (rnd() * 2 - 1) * 0.1;
    const swell = 1 + 0.25 * Math.sin(2 * Math.PI * 0.27 * t) + 0.12 * Math.sin(2 * Math.PI * 0.71 * t + 1.3);
    // (The roar from 70 Hz up: under it, rumble; the pops louder.)
    const r = roar(brown);
    raw[i] = 0.25 * (r - floor(r)) * swell + 6 * pop[i];
  }
  const out = raw.slice(0, Math.round(seconds * RATE));
  for (let i = 0; i < m; i++) {
    const k = i / m;
    out[i] = out[i] * Math.sin((k * Math.PI) / 2) + raw[out.length + i] * Math.cos((k * Math.PI) / 2);
  }
  return normalise(out, 0.7);
}

mkdirSync(SRC, { recursive: true });
wav(join(SRC, 'propio-fuego-prende.wav'), ignite());
wav(join(SRC, 'propio-fuego-crepita.wav'), crackle());
