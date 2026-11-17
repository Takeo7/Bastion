// The ambience bed of a map that is one building (R5; Arturo, 17/11: «Sí, ambiente de interior»): the hospital, the
// offices, the police station and the shed (MissionState.inside). ambience.ts's beds are all out of doors (wind, traffic,
// leaves) and packages/ stays frozen at the 0.10.1, so this one is made here, by the same means as ambience.ts's (its Kit:
// looping noise, oscillators, slow swells, events at random times; copied, as it isn't exported), and render.ts renders
// it as «ambience.inside» beside them. The tubes humming, the building's air plant, air in the vents; now and then a tube
// flickering, a door or a knock far off, water dripping.
import { audio } from '../../../packages/client/src/audio/engine';

export type InsideBed = 'inside';

interface Running {
  out: GainNode;
  stops: (() => void)[];
}

function noiseBuffer(ctx: BaseAudioContext, color: 'pink' | 'brown'): AudioBuffer {
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

/** ambience.ts's Kit, as much of it as this bed uses. */
class Kit {
  constructor(
    readonly ctx: BaseAudioContext,
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

  voice(pan: number): { node: GainNode; t: number } {
    const g = this.ctx.createGain();
    const p = this.ctx.createStereoPanner();
    p.pan.value = pan;
    g.connect(p).connect(this.out);
    return { node: g, t: this.ctx.currentTime };
  }

  /** ambience.ts's hum, giving back its level to swell. */
  hum(freqs: number[], level: number, cutoff = 260): GainNode {
    const sum = this.ctx.createGain();
    for (const f of freqs) this.osc('sawtooth', f).connect(sum);
    const g = this.gain(this.filter(sum, 'lowpass', cutoff, 0.7), level / freqs.length);
    g.connect(this.out);
    return g;
  }

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

function inside(k: Kit): void {
  // The tubes: the ballasts' hum at twice the mains and its harmonics, thin, breathing a little; the part over 200 Hz is
  // what a laptop's speakers give of it.
  const tubes = k.hum([100, 200.3, 299.6, 401.2], 0.16, 900);
  k.swell(tubes.gain, 0.04, 0.008, 0.11);
  // The building's air plant, somewhere under everything.
  const plant = k.gain(k.filter(k.loop('brown'), 'lowpass', 160), 0.3);
  k.swell(plant.gain, 0.3, 0.1, 0.025);
  plant.connect(k.out);
  // Air through the vents: a soft hiss.
  const air = k.gain(k.filter(k.loop('pink'), 'bandpass', 900, 0.7), 0.05);
  k.swell(air.gain, 0.05, 0.02, 0.04);
  air.connect(k.out);
  // A tube flickering: a few quick buzzes, on one side.
  k.every(9, 22, () => {
    const pan = side();
    const n = 2 + Math.floor(Math.random() * 4);
    for (let i = 0; i < n; i++) {
      window.setTimeout(() => {
        k.ping(100, 0.09, 0.06, pan, undefined, 'square');
        k.gust('highpass', 2500, 0.003, 0.05, 0.03, pan);
      }, i * (60 + Math.random() * 90));
    }
  });
  // Far in the building: a door swinging to, or the structure settling.
  k.every(12, 30, () => {
    const pan = side();
    if (Math.random() < 0.5) {
      k.gust('lowpass', 320, 0.004, 0.45, 0.12, pan);
      k.ping(85, 0.5, 0.06, pan, 60);
    } else k.ping(150 + Math.random() * 60, 0.9, 0.03, pan, 110, 'triangle');
  });
  // Water dripping somewhere.
  k.every(5, 14, () => k.ping(1500 + Math.random() * 500, 0.06, 0.015, side(), 1100));
}

/** As ambience.ts's Ambience, for this one bed: play('inside') or null, fading over `fade` seconds. */
class Interior {
  private running: Running | null = null;
  private wanted: InsideBed | null = null;
  private buffers: { pink: AudioBuffer; brown: AudioBuffer } | null = null;

  play(bed: InsideBed | null, fade = 2): void {
    this.wanted = bed;
    audio.onReady(() => this.apply(fade));
  }

  private apply(fade: number): void {
    const ctx = audio.ctx;
    const bus = audio.bus('ambience');
    if (!ctx || !bus) return;
    if (this.running && this.wanted) return;
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
    // Each piece from its own seed: the noises made again on the context there is now.
    this.buffers = { pink: noiseBuffer(ctx, 'pink'), brown: noiseBuffer(ctx, 'brown') };
    const out = ctx.createGain();
    out.gain.setValueAtTime(0, ctx.currentTime);
    out.gain.linearRampToValueAtTime(1, ctx.currentTime + fade);
    out.connect(bus);
    const run: Running = { out, stops: [] };
    this.running = run;
    inside(new Kit(ctx, out, run.stops, this.buffers));
  }
}

export const interior = new Interior();
