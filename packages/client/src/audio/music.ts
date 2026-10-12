import { audio } from './engine';

// The score, generated while it plays: a few synthesised instruments (pads,
// soft keys, bass, drums, bells, drones) and one piece per mood, written bar
// by bar a little ahead of time. All in D minor, slow and sparse, so it sits
// under the game rather than over it.
//
//   base   — the Bastión and the menus: warm chords, a few notes on the keys
//   geo    — the globe: a drone and bells far apart
//   calm   — combat while concealed: a low drone and a heartbeat
//   tense  — combat after contact: drums, an ostinato bass, chord stabs
//   alien  — the enemy turn: a tritone drone and metal

export type Piece = 'base' | 'geo' | 'calm' | 'tense' | 'alien';

const midi = (m: number) => 440 * Math.pow(2, (m - 69) / 12);

interface Voice {
  ctx: AudioContext;
  out: AudioNode;
  noise: AudioBuffer;
}

function noiseBuffer(ctx: AudioContext): AudioBuffer {
  const buf = ctx.createBuffer(1, ctx.sampleRate, ctx.sampleRate);
  const d = buf.getChannelData(0);
  for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
  return buf;
}

function env(g: AudioParam, t: number, attack: number, hold: number, release: number, level: number): void {
  g.setValueAtTime(0, t);
  g.linearRampToValueAtTime(level, t + attack);
  g.setValueAtTime(level, t + attack + hold);
  g.linearRampToValueAtTime(0, t + attack + hold + release);
}

// ---------------------------------------------------------------- instruments

/** A slow pad: two detuned saws and a triangle an octave down, behind a filter that opens slowly. */
function pad(v: Voice, notes: number[], t: number, dur: number, level: number, cutoff = 900): void {
  const { ctx } = v;
  const f = ctx.createBiquadFilter();
  f.type = 'lowpass';
  f.Q.value = 0.6;
  f.frequency.setValueAtTime(cutoff * 0.4, t);
  f.frequency.linearRampToValueAtTime(cutoff, t + dur * 0.5);
  f.frequency.linearRampToValueAtTime(cutoff * 0.5, t + dur + 1.5);
  const g = ctx.createGain();
  env(g.gain, t, Math.min(1.5, dur * 0.4), dur - Math.min(1.5, dur * 0.4), 1.8, level / notes.length);
  f.connect(g).connect(v.out);
  for (const n of notes) {
    for (const [type, detune, oct] of [
      ['sawtooth', -6, 0],
      ['sawtooth', 6, 0],
      ['triangle', 0, -12],
    ] as const) {
      const o = ctx.createOscillator();
      o.type = type;
      o.frequency.value = midi(n + oct);
      o.detune.value = detune;
      o.connect(f);
      o.start(t);
      o.stop(t + dur + 2);
    }
  }
}

/** Soft keys: a triangle with a faint octave, the brightness falling off as it rings. */
function keys(v: Voice, note: number, t: number, level: number): void {
  const { ctx } = v;
  const f = ctx.createBiquadFilter();
  f.type = 'lowpass';
  f.frequency.setValueAtTime(2600, t);
  f.frequency.exponentialRampToValueAtTime(700, t + 0.5);
  const g = ctx.createGain();
  g.gain.setValueAtTime(0, t);
  g.gain.linearRampToValueAtTime(level, t + 0.008);
  g.gain.exponentialRampToValueAtTime(0.0005, t + 2.2);
  f.connect(g).connect(v.out);
  for (const [type, mult, l] of [
    ['triangle', 1, 1],
    ['sine', 2, 0.25],
  ] as const) {
    const o = ctx.createOscillator();
    o.type = type;
    o.frequency.value = midi(note) * mult;
    const og = ctx.createGain();
    og.gain.value = l;
    o.connect(og).connect(f);
    o.start(t);
    o.stop(t + 2.3);
  }
}

function bass(v: Voice, note: number, t: number, dur: number, level: number): void {
  const { ctx } = v;
  const f = ctx.createBiquadFilter();
  f.type = 'lowpass';
  f.frequency.value = 320;
  const g = ctx.createGain();
  env(g.gain, t, 0.02, Math.max(0, dur - 0.1), 0.15, level);
  f.connect(g).connect(v.out);
  for (const [type, l] of [
    ['sine', 1],
    ['sawtooth', 0.25],
  ] as const) {
    const o = ctx.createOscillator();
    o.type = type;
    o.frequency.value = midi(note);
    const og = ctx.createGain();
    og.gain.value = l;
    o.connect(og).connect(f);
    o.start(t);
    o.stop(t + dur + 0.2);
  }
}

function kick(v: Voice, t: number, level: number): void {
  const { ctx } = v;
  const o = ctx.createOscillator();
  o.frequency.setValueAtTime(110, t);
  o.frequency.exponentialRampToValueAtTime(42, t + 0.14);
  const g = ctx.createGain();
  g.gain.setValueAtTime(level, t);
  g.gain.exponentialRampToValueAtTime(0.0005, t + 0.32);
  o.connect(g).connect(v.out);
  o.start(t);
  o.stop(t + 0.35);
}

function noiseHit(v: Voice, t: number, type: BiquadFilterType, freq: number, decay: number, level: number, q = 1): void {
  const { ctx } = v;
  const src = ctx.createBufferSource();
  src.buffer = v.noise;
  const f = ctx.createBiquadFilter();
  f.type = type;
  f.frequency.value = freq;
  f.Q.value = q;
  const g = ctx.createGain();
  g.gain.setValueAtTime(level, t);
  g.gain.exponentialRampToValueAtTime(0.0005, t + decay);
  src.connect(f).connect(g).connect(v.out);
  src.start(t, Math.random() * 0.5);
  src.stop(t + decay + 0.02);
}

const snare = (v: Voice, t: number, level: number) => {
  noiseHit(v, t, 'bandpass', 1900, 0.16, level, 0.8);
  noiseHit(v, t, 'lowpass', 400, 0.08, level * 0.6);
};
const hat = (v: Voice, t: number, level: number) => noiseHit(v, t, 'highpass', 8000, 0.04, level);

/** A bell: inharmonic partials ringing out. */
function bell(v: Voice, note: number, t: number, level: number): void {
  const { ctx } = v;
  [1, 2.76, 5.4].forEach((mult, i) => {
    const o = ctx.createOscillator();
    o.frequency.value = midi(note) * mult;
    const g = ctx.createGain();
    g.gain.setValueAtTime(level / (1 + i * 1.5), t);
    g.gain.exponentialRampToValueAtTime(0.0005, t + 3.2 / (1 + i));
    o.connect(g).connect(v.out);
    o.start(t);
    o.stop(t + 3.3);
  });
}

/** A short bowed stab: a filtered saw, staccato (the tense ostinato's chords). */
function stab(v: Voice, notes: number[], t: number, level: number): void {
  const { ctx } = v;
  const f = ctx.createBiquadFilter();
  f.type = 'lowpass';
  f.frequency.setValueAtTime(2200, t);
  f.frequency.exponentialRampToValueAtTime(600, t + 0.35);
  const g = ctx.createGain();
  g.gain.setValueAtTime(0, t);
  g.gain.linearRampToValueAtTime(level / notes.length, t + 0.015);
  g.gain.exponentialRampToValueAtTime(0.0005, t + 0.45);
  f.connect(g).connect(v.out);
  for (const n of notes) {
    const o = ctx.createOscillator();
    o.type = 'sawtooth';
    o.frequency.value = midi(n);
    o.detune.value = (Math.random() - 0.5) * 10;
    o.connect(f);
    o.start(t);
    o.stop(t + 0.5);
  }
}

// ---------------------------------------------------------------- pieces

interface Score {
  bpm: number;
  /** Writes bar `n`, starting at time `t`. */
  bar(v: Voice, n: number, t: number, beat: number): void;
}

const pick = <T>(xs: readonly T[]): T => xs[Math.floor(Math.random() * xs.length)]!;

// D minor: Dm(add9) · B♭maj7 · F(add9) · C(sus2), two bars each.
const BASE_CHORDS = [
  { root: 38, pad: [50, 53, 57, 64], tones: [62, 65, 69, 74, 76] },
  { root: 46, pad: [46, 50, 53, 57], tones: [62, 65, 70, 74, 77] },
  { root: 41, pad: [53, 57, 60, 67], tones: [65, 69, 72, 77, 79] },
  { root: 48, pad: [48, 55, 60, 62], tones: [62, 67, 72, 74, 79] },
];

const SCORES: Record<Piece, Score> = {
  base: {
    bpm: 68,
    bar(v, n, t, beat) {
      const chord = BASE_CHORDS[Math.floor(n / 2) % BASE_CHORDS.length]!;
      if (n % 2 === 0) {
        pad(v, chord.pad, t, beat * 8, 0.32, 1100);
        bass(v, chord.root - 12, t, beat * 7.5, 0.22);
      }
      // A few notes on the keys, mostly chord tones, never on every beat.
      let last = pick(chord.tones);
      for (let b = 0; b < 4; b++) {
        if (Math.random() < (b === 0 ? 0.55 : 0.32)) {
          const i = chord.tones.indexOf(last);
          last = chord.tones[Math.max(0, Math.min(chord.tones.length - 1, i + pick([-1, 0, 1, 1])))]!;
          keys(v, last, t + b * beat + (Math.random() < 0.3 ? beat / 2 : 0), 0.13);
        }
      }
    },
  },
  geo: {
    bpm: 60,
    bar(v, n, t, beat) {
      if (n % 4 === 0) pad(v, [38, 45, 50], t, beat * 16, 0.3, 500);
      if (n % 2 === 0) kick(v, t, 0.12);
      for (let b = 0; b < 4; b++) if (Math.random() < 0.28) bell(v, pick([74, 77, 79, 81, 84, 86]), t + b * beat, 0.07);
    },
  },
  calm: {
    bpm: 76,
    bar(v, n, t, beat) {
      if (n % 4 === 0) pad(v, [26, 38, 45], t, beat * 16, 0.38, 380);
      // A heartbeat: lub-dub on the first beat.
      kick(v, t, 0.3);
      kick(v, t + beat * 0.28, 0.18);
      for (let b = 0; b < 8; b++) hat(v, t + (b * beat) / 2, b % 2 ? 0.015 : 0.03);
      if (Math.random() < 0.35) keys(v, pick([62, 65, 69, 63, 74]), t + Math.floor(Math.random() * 4) * beat, 0.08);
    },
  },
  tense: {
    bpm: 112,
    bar(v, n, t, beat) {
      // Dm · B♭ · Gm · A: the A major pulls back to D, and never settles.
      const chords = [
        [50, 53, 57],
        [46, 50, 53],
        [43, 46, 50],
        [45, 49, 52],
      ];
      const roots = [38, 34, 31, 33];
      const k = Math.floor(n / 2) % 4;
      if (n % 2 === 0) pad(v, chords[k]!, t, beat * 8, 0.18, 1400);
      // Drums.
      kick(v, t, 0.45);
      kick(v, t + beat * 1.5, 0.35);
      kick(v, t + beat * 2, 0.4);
      snare(v, t + beat, 0.12);
      snare(v, t + beat * 3, 0.14);
      for (let b = 0; b < 8; b++) hat(v, t + (b * beat) / 2, b % 2 ? 0.02 : 0.04);
      // Ostinato bass in eighths, and chord stabs on the offbeats.
      const r = roots[k]!;
      const line = [r, r, r + 3, r, r - 2, r, r + 3, r + 5];
      line.forEach((m, i) => bass(v, m, t + (i * beat) / 2, beat * 0.42, 0.2));
      for (const b of [0.5, 2.5]) stab(v, chords[k]!.map((m) => m + 12), t + b * beat, 0.16);
    },
  },
  alien: {
    bpm: 84,
    bar(v, n, t, beat) {
      // D against A♭: the tritone, low, with metal ringing on the downbeat.
      if (n % 2 === 0) pad(v, [26, 32, 38, 44], t, beat * 8, 0.42, 420);
      kick(v, t, 0.35);
      bell(v, pick([37, 38, 44]), t, 0.12);
      if (n % 2 === 1) noiseHit(v, t + beat * 2, 'bandpass', 600, 1.2, 0.05, 3);
      if (Math.random() < 0.5) keys(v, pick([62, 68, 63]), t + beat * 3, 0.06);
    },
  },
};

// ---------------------------------------------------------------- player

interface Playing {
  piece: Piece;
  out: GainNode;
  bar: number;
  next: number;
}

class Music {
  private playing: Playing | null = null;
  private wanted: Piece | null = null;
  private noise: AudioBuffer | null = null;
  private timer = 0;

  /** Changes the music to `piece` (null: silence), fading over `fade` seconds. */
  play(piece: Piece | null, fade = 2.5): void {
    this.wanted = piece;
    audio.onReady(() => this.apply(fade));
  }

  get current(): Piece | null {
    return this.wanted;
  }

  private apply(fade: number): void {
    const ctx = audio.ctx;
    const bus = audio.bus('music');
    if (!ctx || !bus) return;
    if (this.playing?.piece === this.wanted) return;
    if (this.playing) {
      const old = this.playing.out;
      old.gain.cancelScheduledValues(ctx.currentTime);
      old.gain.setValueAtTime(old.gain.value, ctx.currentTime);
      old.gain.linearRampToValueAtTime(0, ctx.currentTime + fade);
      window.setTimeout(() => old.disconnect(), (fade + 4) * 1000);
      this.playing = null;
    }
    if (!this.wanted) return;
    this.noise ??= noiseBuffer(ctx);
    const out = ctx.createGain();
    out.gain.setValueAtTime(0, ctx.currentTime);
    out.gain.linearRampToValueAtTime(1, ctx.currentTime + fade);
    out.connect(bus);
    const verb = audio.reverb();
    if (verb) {
      const send = ctx.createGain();
      send.gain.value = 0.35;
      out.connect(send).connect(verb);
    }
    this.playing = { piece: this.wanted, out, bar: 0, next: ctx.currentTime + 0.1 };
    if (!this.timer) this.timer = window.setInterval(() => this.schedule(), 120);
    this.schedule();
  }

  /** Writes the bars that start within the next second. */
  private schedule(): void {
    const ctx = audio.ctx;
    const p = this.playing;
    if (!ctx || !p) return;
    const score = SCORES[p.piece];
    const beat = 60 / score.bpm;
    const v: Voice = { ctx, out: p.out, noise: this.noise! };
    while (p.next < ctx.currentTime + 1) {
      if (!document.hidden) score.bar(v, p.bar, p.next, beat);
      p.bar++;
      p.next += beat * 4;
    }
  }
}

export const music = new Music();
