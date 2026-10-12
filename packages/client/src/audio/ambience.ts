import { audio } from './engine';

// Background beds, synthesised live: wind, the hum of a town or a machine
// hall, leaves and birds, the base's generator and radar, the globe's drone.
// Each bed is a few looping noise sources and oscillators breathing on slow
// random swells, plus little events at random times (a car passing, a bird,
// a press stamping). Changing bed fades one into the other.

export type Bed = 'city' | 'wilds' | 'facility' | 'base' | 'geo';

interface Running {
  bed: Bed;
  out: GainNode;
  stops: (() => void)[];
}

function noiseBuffer(ctx: AudioContext, color: 'pink' | 'brown'): AudioBuffer {
  const buf = ctx.createBuffer(1, ctx.sampleRate * 4, ctx.sampleRate);
  const d = buf.getChannelData(0);
  let b0 = 0;
  let b1 = 0;
  let b2 = 0;
  let last = 0;
  for (let i = 0; i < d.length; i++) {
    const w = Math.random() * 2 - 1;
    if (color === 'pink') {
      b0 = 0.99765 * b0 + w * 0.099046;
      b1 = 0.963 * b1 + w * 0.2965164;
      b2 = 0.57 * b2 + w * 1.0526913;
      d[i] = (b0 + b1 + b2 + w * 0.1848) * 0.25;
    } else {
      last = (last + 0.02 * w) / 1.02;
      d[i] = last * 3.5;
    }
  }
  return buf;
}

class Ambience {
  private running: Running | null = null;
  private wanted: Bed | null = null;
  private buffers: { pink: AudioBuffer; brown: AudioBuffer } | null = null;

  /** Changes the bed (null: none), fading over `fade` seconds. */
  play(bed: Bed | null, fade = 2): void {
    this.wanted = bed;
    audio.onReady(() => this.apply(fade));
  }

  private apply(fade: number): void {
    const ctx = audio.ctx;
    const bus = audio.bus('ambience');
    if (!ctx || !bus) return;
    if (this.running?.bed === this.wanted) return;
    if (this.running) {
      const { out, stops } = this.running;
      out.gain.cancelScheduledValues(ctx.currentTime);
      out.gain.setValueAtTime(out.gain.value, ctx.currentTime);
      out.gain.linearRampToValueAtTime(0, ctx.currentTime + fade);
      window.setTimeout(() => {
        for (const s of stops) s();
        out.disconnect();
      }, (fade + 0.2) * 1000);
      this.running = null;
    }
    if (!this.wanted) return;
    this.buffers ??= { pink: noiseBuffer(ctx, 'pink'), brown: noiseBuffer(ctx, 'brown') };
    const out = ctx.createGain();
    out.gain.setValueAtTime(0, ctx.currentTime);
    out.gain.linearRampToValueAtTime(1, ctx.currentTime + fade);
    out.connect(bus);
    const run: Running = { bed: this.wanted, out, stops: [] };
    this.running = run;
    BEDS[this.wanted](new Kit(ctx, out, run.stops, this.buffers));
  }
}

/** What a bed is built with: looping noise, oscillators, slow swells, and events at random intervals. */
class Kit {
  constructor(
    readonly ctx: AudioContext,
    readonly out: AudioNode,
    private readonly stops: (() => void)[],
    private readonly buffers: { pink: AudioBuffer; brown: AudioBuffer },
  ) {}

  loop(color: 'pink' | 'brown'): AudioBufferSourceNode {
    const src = this.ctx.createBufferSource();
    src.buffer = this.buffers[color];
    src.loop = true;
    src.start(this.ctx.currentTime, Math.random() * 3);
    this.stops.push(() => src.stop());
    return src;
  }

  osc(type: OscillatorType, freq: number): OscillatorNode {
    const o = this.ctx.createOscillator();
    o.type = type;
    o.frequency.value = freq;
    o.start();
    this.stops.push(() => o.stop());
    return o;
  }

  filter(input: AudioNode, type: BiquadFilterType, freq: number, q = 1): BiquadFilterNode {
    const f = this.ctx.createBiquadFilter();
    f.type = type;
    f.frequency.value = freq;
    f.Q.value = q;
    input.connect(f);
    return f;
  }

  gain(input: AudioNode, level: number): GainNode {
    const g = this.ctx.createGain();
    g.gain.value = level;
    input.connect(g);
    return g;
  }

  /** Moves a parameter around `base` by ±`depth`, with two slow sines that never line up. */
  swell(param: AudioParam, base: number, depth: number, rate = 0.07): void {
    param.value = base;
    for (const [r, d] of [
      [rate, depth * 0.6],
      [rate * 2.37, depth * 0.4],
    ] as const) {
      const lfo = this.osc('sine', r);
      const g = this.ctx.createGain();
      g.gain.value = d;
      lfo.connect(g).connect(param);
    }
  }

  /** Calls `fn` again and again, every `min`–`max` seconds, while the bed runs. */
  every(min: number, max: number, fn: () => void): void {
    let timer = 0;
    const next = () => {
      timer = window.setTimeout(
        () => {
          if (!document.hidden) fn();
          next();
        },
        (min + Math.random() * (max - min)) * 1000,
      );
    };
    next();
    this.stops.push(() => window.clearTimeout(timer));
  }

  /** A one-off voice for an event: through a panner, gone when it ends. */
  voice(pan: number): { node: GainNode; t: number } {
    const g = this.ctx.createGain();
    const p = this.ctx.createStereoPanner();
    p.pan.value = pan;
    g.connect(p).connect(this.out);
    return { node: g, t: this.ctx.currentTime };
  }

  // ------------------------------------------------------------ layers

  wind(level: number, whistle = 0.25): void {
    const w = this.filter(this.loop('brown'), 'bandpass', 380, 0.6);
    const g = this.gain(w, level);
    this.swell(g.gain, level, level * 0.6, 0.06);
    g.connect(this.out);
    const hi = this.filter(this.loop('pink'), 'bandpass', 1100, 6);
    this.swell(hi.frequency, 1100, 300, 0.05);
    const hg = this.gain(hi, level * whistle);
    this.swell(hg.gain, level * whistle, level * whistle * 0.9, 0.09);
    hg.connect(this.out);
  }

  hum(freqs: number[], level: number, cutoff = 260): void {
    const sum = this.ctx.createGain();
    for (const f of freqs) this.osc('sawtooth', f).connect(sum);
    const g = this.gain(this.filter(sum, 'lowpass', cutoff, 0.7), level / freqs.length);
    g.connect(this.out);
  }

  /** A tone ringing out once: radar pings, clanks, birds (with a pitch sweep). */
  ping(freq: number, decay: number, level: number, pan = 0, sweepTo?: number, type: OscillatorType = 'sine'): void {
    const { node, t } = this.voice(pan);
    const o = this.ctx.createOscillator();
    o.type = type;
    o.frequency.setValueAtTime(freq, t);
    if (sweepTo) o.frequency.exponentialRampToValueAtTime(sweepTo, t + decay * 0.6);
    node.gain.setValueAtTime(0, t);
    node.gain.linearRampToValueAtTime(level, t + 0.005);
    node.gain.exponentialRampToValueAtTime(0.0005, t + decay);
    o.connect(node);
    o.start(t);
    o.stop(t + decay + 0.05);
  }

  /** A burst of filtered noise: a car passing, steam, a press. */
  gust(type: BiquadFilterType, freq: number, attack: number, decay: number, level: number, pan = 0, sweepTo?: number): void {
    const { node, t } = this.voice(pan);
    const src = this.ctx.createBufferSource();
    src.buffer = this.buffers.pink;
    const f = this.ctx.createBiquadFilter();
    f.type = type;
    f.frequency.setValueAtTime(freq, t);
    if (sweepTo) f.frequency.exponentialRampToValueAtTime(sweepTo, t + attack + decay);
    node.gain.setValueAtTime(0, t);
    node.gain.linearRampToValueAtTime(level, t + attack);
    node.gain.exponentialRampToValueAtTime(0.0005, t + attack + decay);
    src.connect(f).connect(node);
    src.start(t, Math.random() * 2);
    src.stop(t + attack + decay + 0.1);
  }
}

const side = () => Math.random() * 1.6 - 0.8;

const BEDS: Record<Bed, (k: Kit) => void> = {
  // A town: wind between buildings, the far rumble of traffic, a car going by now and then.
  city(k) {
    k.wind(0.35, 0.2);
    const rumble = k.gain(k.filter(k.loop('brown'), 'lowpass', 140), 0.45);
    k.swell(rumble.gain, 0.45, 0.2, 0.03);
    rumble.connect(k.out);
    k.every(7, 18, () => k.gust('bandpass', 380, 1.6, 2.4, 0.18, side(), 700));
  },
  // The outskirts: wind in the trees, leaves, birds.
  wilds(k) {
    k.wind(0.45, 0.3);
    const leaves = k.gain(k.filter(k.loop('pink'), 'highpass', 3500), 0.05);
    k.swell(leaves.gain, 0.05, 0.045, 0.21);
    leaves.connect(k.out);
    k.every(2.5, 8, () => {
      const pan = side();
      const base = 2600 + Math.random() * 2200;
      const notes = 2 + Math.floor(Math.random() * 4);
      for (let i = 0; i < notes; i++) window.setTimeout(() => k.ping(base * (1 + (Math.random() - 0.3) * 0.25), 0.12, 0.05, pan, base * 0.7), i * (70 + Math.random() * 60));
    });
  },
  // The printing plant: machines humming, a press stamping, steam.
  facility(k) {
    k.hum([50, 100, 150.5], 0.35, 300);
    k.wind(0.12, 0.1);
    k.every(2.1, 2.3, () => {
      k.gust('lowpass', 500, 0.005, 0.25, 0.35);
      k.ping(170, 0.3, 0.15, 0, 90);
    });
    k.every(6, 14, () => k.gust('highpass', 3000, 0.3, 1.2, 0.08, side()));
  },
  // The Bastión outside: snow wind, the generator, the radar's ping.
  base(k) {
    k.wind(0.5, 0.35);
    k.hum([60, 120.3], 0.12, 200);
    k.every(8, 11, () => k.ping(1250, 1.6, 0.04, 0.3));
    k.every(10, 25, () => k.ping(620, 0.5, 0.05, side(), 540, 'triangle'));
  },
  // The globe: a slow drone and data chattering.
  geo(k) {
    const drone = k.ctx.createGain();
    for (const f of [55, 82.6, 110.3]) k.osc('sine', f).connect(drone);
    const g = k.gain(drone, 0.12);
    k.swell(g.gain, 0.12, 0.05, 0.05);
    g.connect(k.out);
    k.gain(k.filter(k.loop('pink'), 'highpass', 6000), 0.02).connect(k.out);
    k.every(3, 8, () => {
      const pan = side();
      for (let i = 0; i < 3 + Math.floor(Math.random() * 4); i++) window.setTimeout(() => k.ping(900 + Math.random() * 1800, 0.05, 0.03, pan, undefined, 'square'), i * 60);
    });
  },
};

export const ambience = new Ambience();
