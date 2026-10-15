import { hit, Synth, type Env } from '../../audio/synth';
import { LENGTH } from './script';

// The trailer's music (docs/TRAILER.md): original, made in code with the
// game's own building blocks (audio/synth.ts) and rendered offline. D minor,
// like the game's score; 120 BPM in 4/4, so a bar is 2 s and every moment of
// the script falls on a bar line:
//
//   0:00–0:19.5   cold: wind, a low drone, metal scraped far off; the horde at
//                 0:06 brings a high cluster that will not settle; Atalaya at
//                 0:13 a heartbeat; a riser from 0:15.5
//   0:19.5–0:20   everything cut, and a breath drawn in
//   0:20          the hit under the title, ringing until 0:24
//   0:24–0:44     the pulse: ostinato bass, drums, the stronger accent every
//                 4 s for the montage's cuts; i–VI–iv–i–V
//   0:44–0:56     the campaign: room to breathe; long warm chords, keys
//                 climbing through them, a bell at each change, no drums
//   0:56–1:06     the climax: drums, bass in sixteenths, brass opening bar
//                 by bar, cut dry a quarter of a second before the logo
//   1:06          the final accent under the logo, D minor, ringing out to 1:15

const RATE = 48000;
const BEAT = 0.5;
const BAR = 2;
const CUT = 19.5;
const TITLE = 20;
const PULSE = 24;
const CAMPAIGN = 44;

const freq = (midi: number) => 440 * Math.pow(2, (midi - 69) / 12);

/** mulberry32: the same piece every time it is rendered. */
function seeded(seed: number): () => number {
  return () => {
    seed = (seed + 0x6d2b79f5) >>> 0;
    let t = seed;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** Rises in `attack`, holds, falls over `release` from `until` (a hard cut with a tiny release). */
const swell = (at: number, attack: number, until: number, release: number, level: number): Env => [
  [at, 0],
  [at + attack, level],
  [until, level],
  [until + release, 0],
];

class Score {
  constructor(
    readonly s: Synth,
    private readonly mix: AudioNode,
    private readonly room: AudioNode,
  ) {}

  /** Into the mix at `level`, placed at `pan` (−1 left, 1 right), with `verb` of it sent to the room. */
  out(node: AudioNode, level: number, pan = 0, verb = 0): void {
    const ctx = this.s.ctx;
    const g = ctx.createGain();
    g.gain.value = level;
    const p = ctx.createStereoPanner();
    p.pan.value = pan;
    node.connect(g).connect(p).connect(this.mix);
    if (verb > 0) {
      const send = ctx.createGain();
      send.gain.value = verb;
      p.connect(send).connect(this.room);
    }
  }

  // ------------------------------------------------------------ instruments

  /** A low drone: two saws a hair apart and a sine an octave down, behind a lowpass that breathes. */
  drone(at: number, until: number, notes: number[], level: number, attack = 3, release = 0.01): void {
    const s = this.s;
    for (const m of notes) {
      const sum = s.ctx.createGain();
      for (const cents of [-7, 7]) s.osc('sawtooth', freq(m), at, until - at + release, cents).connect(sum);
      // The octave below only where it is still heard (above A1): lower it is weight, not sound.
      if (m - 12 >= 33) s.osc('sine', freq(m - 12), at, until - at + release).connect(sum);
      const f = s.filter(sum, 'lowpass', [[at, 120], [at + (until - at) / 2, 260], [until, 160]], 1.2);
      // The filter breathes, slowly.
      const lfo = s.osc('sine', 0.13, at, until - at);
      const depth = s.ctx.createGain();
      depth.gain.value = 60;
      lfo.connect(depth).connect(f.frequency);
      this.out(s.amp(f, swell(at, attack, until, release, 1)), level / notes.length, m % 2 ? -0.25 : 0.25, 0.25);
    }
  }

  /** Wind: pink noise through a band that wanders. */
  wind(at: number, until: number, sweep: Env, level: number, pan: number): void {
    const s = this.s;
    const band = s.filter(s.noise('pink', at, until - at + 0.02), 'bandpass', sweep, 0.9);
    this.out(s.amp(band, swell(at, 3, until, 0.01, 1)), level, pan, 0.15);
  }

  /** Strings that will not settle: saws a semitone apart, trembling, behind a lowpass. */
  cluster(at: number, until: number, notes: number[], level: number): void {
    const s = this.s;
    notes.forEach((m, i) => {
      const o = s.osc('sawtooth', freq(m), at, until - at + 0.02, (i - 1) * 6);
      // Vibrato, a little different on each.
      const vib = s.osc('sine', 4.7 + i * 0.6, at, until - at);
      const depth = s.ctx.createGain();
      depth.gain.value = 9;
      vib.connect(depth).connect(o.detune);
      const f = s.filter(o, 'lowpass', 2600, 0.7);
      const trem = s.amp(f, swell(at, (until - at) * 0.7, until, 0.01, 1));
      this.out(trem, level / notes.length, (i - 1) * 0.6, 0.45);
    });
  }

  /** Metal scraped far off: inharmonic partials, high and thin, mostly room. */
  scrape(at: number, level: number, pan: number): void {
    const s = this.s;
    const base = s.r(480, 720);
    const ring = s.metal(at, [1, 2.31, 3.54, 4.95, 6.83].map((k) => base * k), 2.8, 1);
    this.out(s.filter(ring, 'highpass', 900, 0.7), level, pan, 0.9);
  }

  /** A sub thump: a sine falling from `f0` to `f1`. */
  thump(at: number, f0: number, f1: number, decay: number, level: number, verb = 0.1): void {
    const s = this.s;
    const o = s.osc('sine', [[at, f0], [at + 0.12, f1]], at, decay + 0.1);
    this.out(s.amp(o, hit(at, 0.004, decay, 1)), level, 0, verb);
  }

  /** The heartbeat: lub-dub. */
  heart(at: number, level: number): void {
    this.thump(at, 70, 38, 0.32, level);
    this.thump(at + 0.19, 62, 35, 0.4, level * 0.65);
  }

  /** Noise rising through a band, and a saw sliding up under it, cut dead at `to`. */
  riser(from: number, to: number, level: number): void {
    const s = this.s;
    const band = s.filter(s.noise('white', from, to - from + 0.02), 'bandpass', [[from, 300], [to, 9000]], 1.4);
    this.out(s.amp(band, [[from, 0.001], [to - 0.01, 1], [to, 0]]), level, 0, 0.3);
    const saw = s.osc('sawtooth', [[from, freq(50)], [to, freq(86)]], from, to - from + 0.02);
    this.out(s.amp(s.filter(saw, 'lowpass', [[from, 400], [to, 5000]], 2), [[from, 0.001], [to - 0.01, 1], [to, 0]]), level * 0.35, 0, 0.2);
  }

  /** A breath drawn in: noise swelling the wrong way, as a reversed echo does, stopping just short of `to`. */
  inhale(from: number, to: number, level: number): void {
    const s = this.s;
    const rnd = s.rnd;
    const len = to - from - 0.02;
    const src = s.samples(len, (t) => (rnd() * 2 - 1) * Math.pow(t / len, 3), from);
    this.out(s.filter(src, 'lowpass', [[from, 600], [from + len, 5000]], 0.8), level, 0, 0);
  }

  /** The big hit: a falling sub, a distorted low chord opening and closing (the «braam»), a burst and a gong. */
  impact(at: number, notes: number[], level: number): void {
    const s = this.s;
    this.thump(at, 95, 30, 4.2, level, 0.2);
    const sum = s.ctx.createGain();
    for (const m of notes) for (const cents of [-9, 0, 9]) s.osc('sawtooth', freq(m), at, 4.6, cents).connect(sum);
    const opened = s.filter(sum, 'lowpass', [[at, 140], [at + 0.07, 1900], [at + 3.8, 220]], 1.6);
    this.out(s.amp(s.shape(opened, 2.6), [[at, 0], [at + 0.02, 1], [at + 4.4, 0.0005]]), level * 0.32, 0, 0.35);
    const burst = s.filter(s.noise('white', at, 0.6), 'lowpass', 2800, 0.7);
    this.out(s.amp(burst, hit(at, 0.002, 0.4, 1)), level * 0.4, 0, 0.5);
    const gong = s.metal(at, [73.4, 197.2, 331.6, 454.8, 615.3, 822.9], 4.8, 1);
    this.out(gong, level * 0.16, 0.1, 0.8);
  }

  /** A clock's tick: a short click of metal. */
  tick(at: number, level: number, pan: number): void {
    const s = this.s;
    this.out(s.filter(s.metal(at, [2400, 3710, 5230], 0.06, 1), 'highpass', 1500, 0.7), level, pan, 0.2);
  }

  kick(at: number, level: number): void {
    const s = this.s;
    this.thump(at, 140, 46, 0.36, level, 0.05);
    const click = s.filter(s.noise('white', at, 0.03), 'highpass', 3500, 0.7);
    this.out(s.amp(click, hit(at, 0.001, 0.012, 1)), level * 0.25, 0, 0);
  }

  /** A big drum, far back in the room: the accent every 4 s. */
  taiko(at: number, level: number): void {
    const s = this.s;
    this.thump(at, 105, 52, 0.9, level, 0.45);
    const skin = s.filter(s.noise('brown', at, 0.5), 'lowpass', 420, 0.8);
    this.out(s.amp(skin, hit(at, 0.002, 0.35, 1)), level * 0.6, 0, 0.5);
  }

  snare(at: number, level: number): void {
    const s = this.s;
    const rattle = s.filter(s.noise('white', at, 0.3), 'bandpass', 1900, 0.8);
    this.out(s.amp(rattle, hit(at, 0.001, 0.17, 1)), level, 0.05, 0.3);
    const body = s.osc('triangle', [[at, 210], [at + 0.05, 165]], at, 0.15);
    this.out(s.amp(body, hit(at, 0.001, 0.09, 1)), level * 0.5, 0, 0.1);
  }

  hat(at: number, level: number, pan: number): void {
    const s = this.s;
    const n = s.filter(s.noise('white', at, 0.08), 'highpass', 7200, 0.7);
    this.out(s.amp(n, hit(at, 0.001, 0.035, 1)), level, pan, 0.05);
  }

  tom(at: number, midi: number, level: number, pan: number): void {
    const s = this.s;
    const o = s.osc('sine', [[at, freq(midi) * 1.7], [at + 0.06, freq(midi)]], at, 0.5);
    this.out(s.amp(o, hit(at, 0.002, 0.38, 1)), level, pan, 0.3);
  }

  /** The ostinato's bass: a plucked saw behind a closing lowpass. */
  bass(at: number, midi: number, dur: number, level: number): void {
    const s = this.s;
    const sum = s.ctx.createGain();
    s.osc('sawtooth', freq(midi), at, dur + 0.05).connect(sum);
    s.osc('square', freq(midi - 12), at, dur + 0.05).connect(sum);
    const f = s.filter(sum, 'lowpass', [[at, 1700], [at + 0.12, 280]], 3.5);
    this.out(s.amp(f, [[at, 0], [at + 0.004, 1], [at + dur, 0.001]]), level, 0, 0.05);
  }

  /** Soft keys: a sine and, quieter, a triangle an octave up, struck and dying away. */
  keys(at: number, midi: number, level: number, pan: number): void {
    const s = this.s;
    const sum = s.ctx.createGain();
    s.osc('sine', freq(midi), at, 1.8).connect(sum);
    const octave = s.ctx.createGain();
    octave.gain.value = 0.22;
    s.osc('triangle', freq(midi + 12), at, 1.8).connect(octave).connect(sum);
    this.out(s.amp(sum, hit(at, 0.004, 1.5, 1)), level, pan, 0.55);
  }

  /** A bell: partials at a bell's ratios, ringing long. */
  bell(at: number, midi: number, level: number, pan: number): void {
    const f = freq(midi);
    this.out(this.s.metal(at, [f, f * 2.76, f * 5.4, f * 8.93], 4.5, 1), level, pan, 0.7);
  }

  /** A plucked saw, high and short: the climax's upper ostinato. */
  pluck(at: number, midi: number, level: number, pan: number): void {
    const s = this.s;
    const f = s.filter(s.osc('sawtooth', freq(midi), at, 0.3), 'lowpass', [[at, 3600], [at + 0.1, 700]], 2);
    this.out(s.amp(f, hit(at, 0.002, 0.2, 1)), level, pan, 0.25);
  }

  /** Brass, more or less: saws opening up across `dur` and stopping on the bar line. */
  brass(at: number, dur: number, notes: number[], level: number): void {
    const s = this.s;
    notes.forEach((m, i) => {
      const sum = s.ctx.createGain();
      for (const cents of [-5, 5]) s.osc('sawtooth', freq(m), at, dur + 0.05, cents).connect(sum);
      const f = s.filter(sum, 'lowpass', [[at, 280], [at + dur * 0.92, 2800]], 1.1);
      const pan = notes.length > 1 ? (i / (notes.length - 1)) * 1.0 - 0.5 : 0;
      this.out(s.amp(s.shape(f, 1.4), [[at, 0], [at + dur * 0.9, 1], [at + dur, 0]]), level / notes.length, pan, 0.3);
    });
  }

  /** A cymbal: noise above 5 kHz, struck and hissing away. */
  crash(at: number, level: number, decay = 2.2): void {
    const s = this.s;
    const n = s.filter(s.noise('white', at, decay + 0.1), 'highpass', 5000, 0.7);
    this.out(s.amp(n, hit(at, 0.002, decay, 1)), level, 0, 0.4);
  }

  /** A held low note: a sine and, quieter, a triangle an octave up. */
  sub(at: number, dur: number, midi: number, level: number): void {
    const s = this.s;
    const sum = s.ctx.createGain();
    s.osc('sine', freq(midi), at, dur + 0.6).connect(sum);
    const up = s.ctx.createGain();
    up.gain.value = 0.3;
    s.osc('triangle', freq(midi + 12), at, dur + 0.6).connect(up).connect(sum);
    this.out(s.amp(sum, swell(at, 0.4, at + dur, 0.5, 1)), level, 0, 0.15);
  }

  /** A pad: two saws a hair apart and a triangle an octave down per note, behind a lowpass that opens slowly. */
  pad(at: number, dur: number, notes: number[], level: number, cutoff = 1100, release = 1.2): void {
    const s = this.s;
    notes.forEach((m, i) => {
      const sum = s.ctx.createGain();
      for (const cents of [-6, 6]) s.osc('sawtooth', freq(m), at, dur + release, cents).connect(sum);
      s.osc('triangle', freq(m - 12), at, dur + release).connect(sum);
      const f = s.filter(sum, 'lowpass', [[at, cutoff * 0.4], [at + dur * 0.6, cutoff], [at + dur + release, cutoff * 0.5]], 0.6);
      const pan = notes.length > 1 ? (i / (notes.length - 1)) * 1.2 - 0.6 : 0;
      this.out(s.amp(f, swell(at, Math.min(1.2, dur * 0.4), at + dur, release, 1)), level / notes.length, pan, 0.4);
    });
  }
}

// --------------------------------------------------------------- sections

/** 0:00–0:19.5: the cold open. */
function tension(sc: Score): void {
  sc.wind(0, CUT, [[0, 520], [6, 900], [12, 600], [CUT, 1500]], 0.2, -0.6);
  sc.wind(0, CUT, [[0, 760], [7, 460], [14, 1050], [CUT, 1900]], 0.16, 0.6);
  sc.drone(1.5, CUT, [26, 38], 0.13);
  for (const [at, pan] of [[3.2, -0.7], [8.6, 0.6], [11.3, -0.3], [15.4, 0.8]] as const) sc.scrape(at, 0.18, pan);
  // The horde comes into the street.
  sc.thump(6, 60, 32, 2.2, 0.4, 0.6);
  sc.cluster(6, CUT, [81, 82, 88], 0.06);
  // Atalaya: a heart that quickens a little.
  for (let at = 13, k = 0; at < 19.2; at += 1, k++) sc.heart(at, 0.22 + k * 0.05);
  sc.riser(15.5, CUT, 0.2);
  // Fifths in the drone come in under the riser.
  sc.drone(16, CUT, [33], 0.07, 2);
}

/** 0:19.5–0:24: the cut, the breath and the hit under the title. */
function title(sc: Score): void {
  sc.inhale(CUT + 0.12, TITLE, 0.15);
  sc.impact(TITLE, [26, 38, 45], 1);
  // What is left after the hit: the drone, low, and a clock that starts counting into the pulse.
  sc.drone(TITLE + 0.3, PULSE, [38], 0.08, 1.5, 0.3);
  for (let at = 22; at < PULSE - 0.01; at += BEAT) sc.tick(at, 0.05 + (at - 22) * 0.02, at % 1 ? 0.4 : -0.4);
}

/** The pulse's chords, four seconds each: i–VI–iv–i–V. Root (for the bass) and the pad's notes. */
const PULSE_CHORDS: { at: number; root: number; notes: number[] }[] = [
  { at: 24, root: 38, notes: [50, 53, 57] },
  { at: 28, root: 34, notes: [50, 53, 58] },
  { at: 32, root: 31, notes: [50, 55, 58] },
  { at: 36, root: 38, notes: [50, 53, 57] },
  { at: 40, root: 33, notes: [49, 52, 57] },
];

/** 0:24–0:44: the montage. Thin at first, fuller every 8 s, building into the campaign. */
function pulse(sc: Score): void {
  const pattern = [0, 0, 12, 0, 0, 12, 0, 7];
  for (const { at, root, notes } of PULSE_CHORDS) {
    sc.pad(at, 4, notes, at < 32 ? 0.14 : 0.2);
    for (let k = 0; k < 16; k++) {
      const t = at + k * (BEAT / 2);
      sc.bass(t, root + pattern[k % 8]!, 0.22, k % 8 === 0 ? 0.62 : 0.45);
    }
  }
  for (let at = PULSE; at < CAMPAIGN - 0.01; at += BEAT) {
    const beat = Math.round((at - PULSE) / BEAT) % 4;
    // The accent for the cuts, every 4 s.
    if ((at - PULSE) % 4 === 0) sc.taiko(at, 1);
    if (beat === 0 || beat === 2 || at >= 32) sc.kick(at, beat === 0 ? 0.9 : 0.65);
    if (at >= 30 && (beat === 1 || beat === 3)) sc.snare(at, 0.45);
    if (at >= 28) for (const half of [0, BEAT / 2]) sc.hat(at + half, half ? 0.08 : 0.12, 0.3);
  }
  // A fill of toms into each 4 s accent from the second phrase on.
  for (let phrase = 28; phrase <= 40; phrase += 4) {
    const notes = [50, 47, 45, 43];
    notes.forEach((m, i) => sc.tom(phrase - 0.5 + i * 0.125, m, 0.28, i % 2 ? 0.3 : -0.3));
  }
  // Building into 0:44: toms in sixteenths, louder, and the riser.
  for (let at = 42; at < CAMPAIGN - 0.01; at += BEAT / 4) sc.tom(at, 45 - Math.floor((at - 42) * 2), 0.14 + (at - 42) * 0.12, 0);
  sc.riser(41, CAMPAIGN, 0.22);
}

/** The campaign's chords, two seconds each, warmer: i9–VImaj7–IIImaj7–VII(add9)–iv9–V(sus4→3). */
const CAMPAIGN_CHORDS: { at: number; root: number; notes: number[] }[] = [
  { at: 44, root: 38, notes: [50, 53, 57, 60, 64] },
  { at: 46, root: 34, notes: [46, 50, 53, 57] },
  { at: 48, root: 41, notes: [53, 57, 60, 64] },
  { at: 50, root: 36, notes: [48, 52, 55, 62] },
  { at: 52, root: 31, notes: [43, 46, 50, 53, 57] },
  { at: 54, root: 33, notes: [45, 50, 52, 55] },
];

/** 0:44–0:56: the campaign. Room to breathe: long chords, keys climbing through them, a bell at each change. */
function campaign(sc: Score): void {
  // The landing out of the montage, under UI's fade through black.
  sc.thump(CAMPAIGN, 70, 34, 1.6, 0.3, 0.6);
  sc.crash(CAMPAIGN, 0.12, 3);
  for (const { at, root, notes } of CAMPAIGN_CHORDS) {
    sc.pad(at, BAR, notes, 0.11, 900, 1.6);
    sc.sub(at, BAR - 0.2, root, 0.12);
    sc.bell(at + 0.02, notes.at(-1)! + 12, 0.05, at % 4 ? 0.5 : -0.5);
    // The keys climb the chord in eighths from the second beat, two octaves of it.
    const up = [...notes, ...notes.map((m) => m + 12)].slice(0, 7);
    up.forEach((m, k) => sc.keys(at + BEAT + k * (BEAT / 2), m + 12, 0.065 + k * 0.005, k % 2 ? 0.45 : -0.45));
  }
  // The fifth bar's chord turns sus4 → major third: into the climax.
  sc.pad(55, 1, [45, 49, 52, 57], 0.12, 1400, 0.4);
  // A soft pulse to keep time, far back.
  for (let at = CAMPAIGN + BAR; at < 56 - 0.01; at += BAR) sc.thump(at, 62, 36, 0.6, 0.12, 0.4);
  // Into the climax: the riser and a snare roll that grows, under UI's yellow wipe.
  sc.riser(54, 56, 0.18);
  for (let at = 55; at < 56 - 0.01; at += BEAT / 4) sc.snare(at, 0.08 + (at - 55) * 0.3);
}

const CLIMAX = 56;
const LOGO = 66;

/** The climax's chords, a bar each: i–VI–iv–V–V, the last held as it builds. */
const CLIMAX_CHORDS: { at: number; root: number; notes: number[]; top: number }[] = [
  { at: 56, root: 38, notes: [50, 53, 57], top: 62 },
  { at: 58, root: 34, notes: [50, 53, 58], top: 65 },
  { at: 60, root: 31, notes: [50, 55, 58], top: 67 },
  { at: 62, root: 33, notes: [49, 52, 57], top: 69 },
  { at: 64, root: 33, notes: [49, 52, 57], top: 73 },
];

/** 0:56–1:06: the climax. Everything back and climbing bar by bar, cut dry a quarter of a second before the logo. */
function climax(sc: Score): void {
  const end = LOGO - 0.25;
  const pattern = [0, 0, 12, 0, 7, 0, 12, 0];
  CLIMAX_CHORDS.forEach(({ at, root, notes, top }, bar) => {
    const lift = 1 + bar * 0.16;
    const until = Math.min(at + BAR, end);
    sc.taiko(at, 1);
    sc.crash(at, 0.1 * lift);
    sc.pad(at, until - at, notes, 0.16 * lift, 1400, 0.01);
    sc.brass(at, until - at, [...notes, top], 0.42 * lift);
    // The bass in sixteenths; the upper ostinato climbs the chord.
    const arp = [...notes, top, ...notes.map((m) => m + 12)];
    for (let k = 0; at + k * (BEAT / 4) < until - 0.01; k++) {
      const t = at + k * (BEAT / 4);
      sc.bass(t, root + pattern[k % 8]!, 0.11, (k % 4 === 0 ? 0.6 : 0.42) * lift);
      sc.pluck(t, arp[k % arp.length]! + 12, 0.07 * lift, k % 2 ? 0.5 : -0.5);
    }
    for (let b = 0; at + b * BEAT < until - 0.01; b++) {
      const t = at + b * BEAT;
      sc.kick(t, 1 * lift);
      if (b % 2) sc.snare(t, 0.5 * lift);
      for (const q of [0, 1, 2, 3]) if (t + q * (BEAT / 4) < until) sc.hat(t + q * (BEAT / 4), (q ? 0.06 : 0.1) * lift, 0.3);
    }
    // Toms into the next bar.
    if (bar < 4) [50, 47, 45, 43].forEach((m, i) => sc.tom(at + 1.5 + i * 0.125, m, 0.3 * lift, i % 2 ? 0.3 : -0.3));
  });
  // The last bar builds: a snare roll and the riser, cut dead with everything else.
  for (let at = 64; at < end - 0.01; at += BEAT / 4) sc.snare(at, 0.2 + (at - 64) * 0.22);
  sc.riser(62, end, 0.28);
}

/** 1:06–1:15: the final accent under the logo, D minor, ringing out to silence. */
function finale(sc: Score): void {
  sc.impact(LOGO, [26, 38, 45, 53], 1);
  sc.crash(LOGO, 0.2, 4);
  sc.pad(LOGO + 0.2, 6.5, [50, 53, 57, 62], 0.09, 700, 2);
  sc.sub(LOGO + 0.4, 7, 38, 0.1);
  sc.bell(LOGO + 0.01, 86, 0.06, 0.4);
  sc.bell(LOGO + 0.01, 81, 0.05, -0.4);
  // One last heartbeat, far away.
  sc.heart(71, 0.12);
}

// ---------------------------------------------------------------- render

/**
 * The music, `seconds` long (all of it by default). Cut short (the mock-up),
 * it fades out over its last second.
 */
export async function renderScore(seconds = LENGTH): Promise<AudioBuffer> {
  const ctx = new OfflineAudioContext(2, Math.ceil(seconds * RATE), RATE);
  const s = new Synth(ctx, seeded(1975));
  s.out.disconnect();
  // The end of the chain: glue, then a limiter, then the fade if the piece is cut short.
  const glue = ctx.createDynamicsCompressor();
  glue.threshold.value = -16;
  glue.knee.value = 8;
  glue.ratio.value = 2;
  glue.attack.value = 0.02;
  glue.release.value = 0.25;
  const limit = ctx.createDynamicsCompressor();
  limit.threshold.value = -2;
  limit.knee.value = 0;
  limit.ratio.value = 20;
  limit.attack.value = 0.001;
  limit.release.value = 0.08;
  const fade = ctx.createGain();
  if (seconds < LENGTH) {
    fade.gain.setValueAtTime(1, seconds - 1);
    fade.gain.linearRampToValueAtTime(0, seconds);
  }
  glue.connect(limit).connect(fade).connect(ctx.destination);
  const mix = ctx.createGain();
  mix.connect(glue);
  // The room: a long stereo echo, a little behind the sound.
  const room = ctx.createConvolver();
  room.buffer = hall(ctx, 3.6, 2.6);
  const pre = ctx.createDelay(0.1);
  pre.delayTime.value = 0.025;
  const wet = ctx.createGain();
  wet.gain.value = 0.42;
  // The cuts before the title and before the logo are dry: the room's tail goes with everything else, and is back for the hit.
  for (const [cut, back] of [[CUT, TITLE], [LOGO - 0.25, LOGO]] as const) {
    wet.gain.setValueAtTime(0.42, cut);
    wet.gain.linearRampToValueAtTime(0, cut + 0.02);
    wet.gain.setValueAtTime(0, back - 0.01);
    wet.gain.linearRampToValueAtTime(0.42, back);
  }
  pre.connect(room).connect(wet).connect(mix);
  const sc = new Score(s, mix, pre);
  tension(sc);
  title(sc);
  pulse(sc);
  campaign(sc);
  climax(sc);
  finale(sc);
  const buf = await ctx.startRendering();
  normalise(buf, Math.pow(10, -1 / 20));
  return buf;
}

/** A hall's echo: noise falling away, different on each side so it opens out. */
function hall(ctx: BaseAudioContext, seconds: number, decay: number): AudioBuffer {
  const rnd = seeded(7);
  const length = Math.floor(ctx.sampleRate * seconds);
  const buf = ctx.createBuffer(2, length, ctx.sampleRate);
  for (let ch = 0; ch < 2; ch++) {
    const d = buf.getChannelData(ch);
    for (let i = 0; i < length; i++) d[i] = (rnd() * 2 - 1) * Math.pow(1 - i / length, decay);
  }
  return buf;
}

/** Brings the loudest sample to `peak`. */
function normalise(buf: AudioBuffer, peak: number): void {
  let max = 0;
  for (let c = 0; c < buf.numberOfChannels; c++) for (const v of buf.getChannelData(c)) max = Math.max(max, Math.abs(v));
  if (!max) return;
  const k = peak / max;
  for (let c = 0; c < buf.numberOfChannels; c++) {
    const d = buf.getChannelData(c);
    for (let i = 0; i < d.length; i++) d[i]! *= k;
  }
}

export const MUSIC = { BAR, BEAT, CUT, TITLE, PULSE, CAMPAIGN, CLIMAX, LOGO };
