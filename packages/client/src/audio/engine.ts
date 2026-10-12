import { storageKey } from '../net';

// The game's sound: one AudioContext for everything, with a bus per kind of
// sound so each has its own volume (Ajustes → Sonido), a compressor before the
// speakers, a shared reverb, and the music ducking under loud moments and the
// radio. Browsers only start audio after a gesture: the context opens on the
// first click or key, and whatever waits on it (music, ambience) starts then.

export type Bus = 'sfx' | 'ui' | 'radio' | 'music' | 'ambience';

/** Volumes from 0 to 1. The radio follows the interface volume. */
export interface Volumes {
  master: number;
  sfx: number;
  music: number;
  ambience: number;
  ui: number;
}

const DEFAULTS: Volumes = { master: 0.8, sfx: 0.8, music: 0.5, ambience: 0.6, ui: 0.7 };
const VOLUME_KEY = storageKey('volumes');
const MUTE_KEY = storageKey('muted');

/** How loud each bus sits in the mix before the player's volume. */
const MIX: Record<Bus, number> = { sfx: 1, ui: 0.55, radio: 0.7, music: 0.42, ambience: 0.5 };

function load(): Volumes {
  try {
    return { ...DEFAULTS, ...(JSON.parse(localStorage.getItem(VOLUME_KEY) ?? '{}') as Partial<Volumes>) };
  } catch {
    return { ...DEFAULTS };
  }
}

/** Options for one sound: which bus, how loud (linear), pitch, stereo position, delay and how much reverb. */
export interface PlayOptions {
  bus?: Bus;
  gain?: number;
  rate?: number;
  pan?: number;
  delay?: number;
  reverb?: number;
}

class AudioEngine {
  ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private buses = new Map<Bus, GainNode>();
  /** Between the music bus and the master: lowered while something loud plays. */
  private duckNode: GainNode | null = null;
  private reverbIn: GainNode | null = null;
  volumes = load();
  muted: boolean;
  private readyFns: (() => void)[] = [];
  private volumeFns = new Set<() => void>();

  constructor() {
    let muted = false;
    try {
      muted = localStorage.getItem(MUTE_KEY) === '1';
    } catch {
      /* storage unavailable */
    }
    this.muted = muted;
    if (typeof window === 'undefined') return;
    const unlock = () => this.unlock();
    window.addEventListener('pointerdown', unlock, { once: true, capture: true });
    window.addEventListener('keydown', unlock, { once: true, capture: true });
    // A hidden tab makes no sound and costs nothing.
    document.addEventListener('visibilitychange', () => {
      if (!this.ctx) return;
      if (document.hidden) void this.ctx.suspend();
      else void this.ctx.resume();
    });
  }

  /** Runs `fn` once the context exists (right away if it already does). */
  onReady(fn: () => void): void {
    if (this.ctx) fn();
    else this.readyFns.push(fn);
  }

  /** Called when volumes or mute change (the settings panel redraws). */
  onVolumes(fn: () => void): () => void {
    this.volumeFns.add(fn);
    return () => this.volumeFns.delete(fn);
  }

  private unlock(): void {
    if (this.ctx) return;
    const ctx = new AudioContext({ latencyHint: 'interactive' });
    this.ctx = ctx;
    // Compressor last: many shots and an explosion at once must not clip.
    const comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -14;
    comp.knee.value = 10;
    comp.ratio.value = 4;
    comp.attack.value = 0.004;
    comp.release.value = 0.25;
    comp.connect(ctx.destination);
    this.master = ctx.createGain();
    this.master.connect(comp);
    this.duckNode = ctx.createGain();
    this.duckNode.connect(this.master);
    for (const bus of Object.keys(MIX) as Bus[]) {
      const g = ctx.createGain();
      g.connect(bus === 'music' ? this.duckNode : this.master);
      this.buses.set(bus, g);
    }
    // A shared reverb for effects that ask for space (explosions, sniper tails, radio).
    const convolver = ctx.createConvolver();
    convolver.buffer = impulse(ctx, 2.4, 2.8);
    this.reverbIn = ctx.createGain();
    const wet = ctx.createGain();
    wet.gain.value = 0.55;
    this.reverbIn.connect(convolver).connect(wet).connect(this.master);
    this.applyVolumes();
    for (const fn of this.readyFns.splice(0)) fn();
  }

  private applyVolumes(): void {
    if (!this.ctx || !this.master) return;
    const now = this.ctx.currentTime;
    this.master.gain.setTargetAtTime(this.muted ? 0 : this.volumes.master, now, 0.05);
    const of: Record<Bus, number> = {
      sfx: this.volumes.sfx,
      ui: this.volumes.ui,
      radio: this.volumes.ui,
      music: this.volumes.music,
      ambience: this.volumes.ambience,
    };
    for (const [bus, g] of this.buses) g.gain.setTargetAtTime(MIX[bus] * of[bus], now, 0.05);
  }

  setVolumes(patch: Partial<Volumes>): void {
    this.volumes = { ...this.volumes, ...patch };
    try {
      localStorage.setItem(VOLUME_KEY, JSON.stringify(this.volumes));
    } catch {
      /* private mode */
    }
    this.applyVolumes();
    for (const fn of this.volumeFns) fn();
  }

  toggleMute(): boolean {
    this.muted = !this.muted;
    try {
      localStorage.setItem(MUTE_KEY, this.muted ? '1' : '0');
    } catch {
      /* storage unavailable */
    }
    this.applyVolumes();
    for (const fn of this.volumeFns) fn();
    return this.muted;
  }

  /** The context, if sound can play now; null before the first gesture, when muted or hidden. */
  live(): AudioContext | null {
    if (!this.ctx || this.muted || document.hidden) return null;
    if (this.ctx.state === 'suspended') void this.ctx.resume();
    return this.ctx;
  }

  bus(name: Bus): GainNode | null {
    return this.buses.get(name) ?? null;
  }

  /** The reverb's input, for sources that want to send to it. */
  reverb(): GainNode | null {
    return this.reverbIn;
  }

  /** Plays a buffer once; returns its source (to stop it early) or null if nothing can play. */
  play(buffer: AudioBuffer, o: PlayOptions = {}): AudioBufferSourceNode | null {
    const ctx = this.live();
    const bus = this.bus(o.bus ?? 'sfx');
    if (!ctx || !bus) return null;
    const src = ctx.createBufferSource();
    src.buffer = buffer;
    src.playbackRate.value = o.rate ?? 1;
    const gain = ctx.createGain();
    gain.gain.value = o.gain ?? 1;
    let node: AudioNode = src.connect(gain);
    if (o.pan) {
      const pan = ctx.createStereoPanner();
      pan.pan.value = Math.max(-1, Math.min(1, o.pan));
      node = node.connect(pan);
    }
    node.connect(bus);
    if (o.reverb && this.reverbIn) {
      const send = ctx.createGain();
      send.gain.value = o.reverb;
      node.connect(send).connect(this.reverbIn);
    }
    src.start(ctx.currentTime + (o.delay ?? 0));
    return src;
  }

  /** Lowers the music for a moment: `depth` 0–1, held for `hold` seconds, back over `release`. */
  duck(depth = 0.45, hold = 0.5, release = 1.4): void {
    if (!this.ctx || !this.duckNode) return;
    const g = this.duckNode.gain;
    const now = this.ctx.currentTime;
    g.cancelScheduledValues(now);
    g.setValueAtTime(g.value, now);
    g.linearRampToValueAtTime(1 - depth, now + 0.06);
    g.setValueAtTime(1 - depth, now + 0.06 + hold);
    g.linearRampToValueAtTime(1, now + 0.06 + hold + release);
  }

  /** Holds the music down (radio talking) until `release()`; returns the release. */
  holdDuck(depth = 0.35): () => void {
    if (!this.ctx || !this.duckNode) return () => undefined;
    const g = this.duckNode.gain;
    const now = this.ctx.currentTime;
    g.cancelScheduledValues(now);
    g.setValueAtTime(g.value, now);
    g.linearRampToValueAtTime(1 - depth, now + 0.3);
    return () => {
      if (!this.ctx) return;
      const t = this.ctx.currentTime;
      g.cancelScheduledValues(t);
      g.setValueAtTime(g.value, t);
      g.linearRampToValueAtTime(1, t + 1.2);
    };
  }
}

/** A stereo reverb tail: decaying noise, a little different in each ear. */
export function impulse(ctx: BaseAudioContext, seconds: number, decay: number): AudioBuffer {
  const length = Math.floor(ctx.sampleRate * seconds);
  const buffer = ctx.createBuffer(2, length, ctx.sampleRate);
  for (let ch = 0; ch < 2; ch++) {
    const data = buffer.getChannelData(ch);
    for (let i = 0; i < length; i++) {
      const t = i / length;
      // A short gap before the tail, then an exponential fade with a softer top end over time.
      data[i] = i < ctx.sampleRate * 0.012 ? 0 : (Math.random() * 2 - 1) * Math.pow(1 - t, decay);
    }
  }
  return buffer;
}

export const audio = new AudioEngine();
