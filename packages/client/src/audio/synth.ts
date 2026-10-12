// Building blocks for sounds made in code: each recipe in sounds.ts draws a
// small graph on an OfflineAudioContext (noise, oscillators, filters,
// envelopes, distortion, echoes, metal resonances) and renders it once to a
// buffer. Times are seconds from the start of the sound.

/** Points of an envelope or a sweep: [time, value]. Rises are linear; falls between positive values are exponential. */
export type Env = [number, number][];

const noiseCache = new WeakMap<BaseAudioContext, Map<string, AudioBuffer>>();

function noiseBuffer(ctx: BaseAudioContext, color: 'white' | 'pink' | 'brown', rnd: () => number): AudioBuffer {
  let byColor = noiseCache.get(ctx);
  if (!byColor) noiseCache.set(ctx, (byColor = new Map()));
  let buf = byColor.get(color);
  if (buf) return buf;
  const length = ctx.sampleRate * 2;
  buf = ctx.createBuffer(1, length, ctx.sampleRate);
  const d = buf.getChannelData(0);
  let b0 = 0;
  let b1 = 0;
  let b2 = 0;
  let last = 0;
  for (let i = 0; i < length; i++) {
    const w = rnd() * 2 - 1;
    if (color === 'white') d[i] = w;
    else if (color === 'pink') {
      // Paul Kellet's economy pink filter.
      b0 = 0.99765 * b0 + w * 0.099046;
      b1 = 0.963 * b1 + w * 0.2965164;
      b2 = 0.57 * b2 + w * 1.0526913;
      d[i] = (b0 + b1 + b2 + w * 0.1848) * 0.25;
    } else {
      last = (last + 0.02 * w) / 1.02;
      d[i] = last * 3.5;
    }
  }
  byColor.set(color, buf);
  return buf;
}

export function applyEnv(param: AudioParam, env: Env | number): void {
  if (typeof env === 'number') {
    param.value = env;
    return;
  }
  param.setValueAtTime(env[0]![1], env[0]![0]);
  for (let k = 1; k < env.length; k++) {
    const [t, v] = env[k]!;
    const prev = env[k - 1]![1];
    if (v > 0 && prev > 0 && v !== prev) param.exponentialRampToValueAtTime(v, t);
    else param.linearRampToValueAtTime(v, t);
  }
}

/** A hit: rises in `attack`, falls to silence by `decay`, starting at `at`. */
export const hit = (at: number, attack: number, decay: number, peak = 1): Env => [
  [at, 0],
  [at + attack, peak],
  [at + attack + decay, 0.0005 * peak],
];

export class Synth {
  readonly out: GainNode;

  constructor(
    readonly ctx: OfflineAudioContext,
    readonly rnd: () => number,
  ) {
    this.out = ctx.createGain();
    this.out.connect(ctx.destination);
  }

  get dur(): number {
    return this.ctx.length / this.ctx.sampleRate;
  }

  /** A random number in [a, b). */
  r(a: number, b: number): number {
    return a + (b - a) * this.rnd();
  }

  /** `x` nudged by up to ±`spread` (a fraction): variants never sound the same. */
  vary(x: number, spread = 0.08): number {
    return x * (1 + (this.rnd() * 2 - 1) * spread);
  }

  noise(color: 'white' | 'pink' | 'brown' = 'white', at = 0, dur = this.dur - at): AudioBufferSourceNode {
    const src = this.ctx.createBufferSource();
    src.buffer = noiseBuffer(this.ctx, color, this.rnd);
    src.loop = true;
    src.start(at, this.rnd() * 1.5);
    src.stop(Math.min(this.dur, at + dur + 0.01));
    return src;
  }

  osc(type: OscillatorType, freq: Env | number, at = 0, dur = this.dur - at, detune = 0): OscillatorNode {
    const o = this.ctx.createOscillator();
    o.type = type;
    o.detune.value = detune;
    applyEnv(o.frequency, freq);
    o.start(at);
    o.stop(Math.min(this.dur, at + dur + 0.01));
    return o;
  }

  filter(input: AudioNode, type: BiquadFilterType, freq: Env | number, q = 1, gain = 0): BiquadFilterNode {
    const f = this.ctx.createBiquadFilter();
    f.type = type;
    applyEnv(f.frequency, freq);
    f.Q.value = q;
    f.gain.value = gain;
    input.connect(f);
    return f;
  }

  amp(input: AudioNode, env: Env | number): GainNode {
    const g = this.ctx.createGain();
    g.gain.value = 0;
    applyEnv(g.gain, env);
    input.connect(g);
    return g;
  }

  /** Soft clipping: `drive` 1 is gentle, 4 is crunchy. */
  shape(input: AudioNode, drive: number): WaveShaperNode {
    const n = 1024;
    const curve = new Float32Array(n);
    for (let i = 0; i < n; i++) {
      const x = (i / (n - 1)) * 2 - 1;
      curve[i] = Math.tanh(x * drive) / Math.tanh(drive);
    }
    const w = this.ctx.createWaveShaper();
    w.curve = curve;
    w.oversample = '2x';
    input.connect(w);
    return w;
  }

  /** Echoes: a delay line fed back into itself, mixed in at `mix`. */
  echo(input: AudioNode, time: number, feedback: number, mix: number, tone = 2500): GainNode {
    const sum = this.ctx.createGain();
    input.connect(sum);
    const d = this.ctx.createDelay(2);
    d.delayTime.value = time;
    const fb = this.ctx.createGain();
    fb.gain.value = feedback;
    const lp = this.ctx.createBiquadFilter();
    lp.type = 'lowpass';
    lp.frequency.value = tone;
    const wet = this.ctx.createGain();
    wet.gain.value = mix;
    input.connect(d);
    d.connect(lp).connect(fb).connect(d);
    lp.connect(wet).connect(sum);
    return sum;
  }

  /**
   * Metal or glass: sine partials at inharmonic ratios, each ringing out
   * (higher ones die sooner), as a struck plate, a casing or a bell does.
   */
  metal(at: number, freqs: number[], decay: number, level = 1): GainNode {
    const sum = this.ctx.createGain();
    freqs.forEach((f, i) => {
      const o = this.osc('sine', f, at, decay * 1.2 + 0.05);
      const g = this.amp(o, hit(at, 0.0015, decay / (1 + i * 0.45), level / (1 + i * 0.5)));
      g.connect(sum);
    });
    return sum;
  }

  /** A sound written sample by sample (grains, crackle, rattles). */
  samples(dur: number, fn: (t: number, i: number) => number, at = 0): AudioBufferSourceNode {
    const length = Math.max(1, Math.floor(dur * this.ctx.sampleRate));
    const buf = this.ctx.createBuffer(1, length, this.ctx.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < length; i++) d[i] = fn(i / this.ctx.sampleRate, i);
    const src = this.ctx.createBufferSource();
    src.buffer = buf;
    src.start(at);
    return src;
  }

  /** Short random bursts: `count` grains of noise scattered over [from, to), each `len` long, fading out across the span. */
  grains(from: number, to: number, count: number, len: number, fade = true): AudioBufferSourceNode {
    const starts = Array.from({ length: count }, () => this.r(from, to)).sort((a, b) => a - b);
    const amps = starts.map((s) => (fade ? Math.pow(1 - (s - from) / (to - from), 1.5) : 1) * this.r(0.4, 1));
    const rnd = this.rnd;
    return this.samples(to + len, (t) => {
      let v = 0;
      for (let k = 0; k < starts.length; k++) {
        const dt = t - starts[k]!;
        if (dt >= 0 && dt < len) v += (rnd() * 2 - 1) * amps[k]! * Math.exp((-dt / len) * 5);
      }
      return v;
    });
  }

  /** Sends a node to the output at `level`. */
  to(node: AudioNode, level = 1): void {
    const g = this.ctx.createGain();
    g.gain.value = level;
    node.connect(g).connect(this.out);
  }
}
