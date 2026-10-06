import { storageKey } from '../net';

const MUTE_KEY = storageKey('muted');

/**
 * Placeholder sound effects synthesised with WebAudio (no audio files), so the
 * game has feedback until real sounds are added. Browsers only allow audio
 * after a user gesture, hence `unlock`.
 */
export class Sfx {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private noiseBuffer: AudioBuffer | null = null;
  muted: boolean;

  constructor() {
    let stored = false;
    try {
      stored = localStorage.getItem(MUTE_KEY) === '1';
    } catch {
      /* storage unavailable */
    }
    this.muted = stored;
    const unlock = () => this.unlock();
    window.addEventListener('pointerdown', unlock, { once: true });
    window.addEventListener('keydown', unlock, { once: true });
  }

  toggleMute(): boolean {
    this.muted = !this.muted;
    try {
      localStorage.setItem(MUTE_KEY, this.muted ? '1' : '0');
    } catch {
      /* storage unavailable */
    }
    return this.muted;
  }

  private unlock(): void {
    if (this.ctx) return;
    this.ctx = new AudioContext();
    this.master = this.ctx.createGain();
    this.master.gain.value = 0.5;
    this.master.connect(this.ctx.destination);
    const len = this.ctx.sampleRate;
    this.noiseBuffer = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
    const data = this.noiseBuffer.getChannelData(0);
    for (let i = 0; i < len; i++) data[i] = Math.random() * 2 - 1;
  }

  private ready(): AudioContext | null {
    if (this.muted || !this.ctx || !this.master || document.hidden) return null;
    if (this.ctx.state === 'suspended') void this.ctx.resume();
    return this.ctx;
  }

  private noise(at: number, duration: number, filter: BiquadFilterType, freq: number, gain: number, q = 1): void {
    const ctx = this.ctx!;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuffer;
    const f = ctx.createBiquadFilter();
    f.type = filter;
    f.frequency.value = freq;
    f.Q.value = q;
    const g = ctx.createGain();
    g.gain.setValueAtTime(gain, at);
    g.gain.exponentialRampToValueAtTime(0.001, at + duration);
    src.connect(f).connect(g).connect(this.master!);
    src.start(at, Math.random() * 0.5);
    src.stop(at + duration + 0.02);
  }

  private tone(at: number, duration: number, from: number, to: number, gain: number, type: OscillatorType = 'sine'): void {
    const ctx = this.ctx!;
    const osc = ctx.createOscillator();
    osc.type = type;
    osc.frequency.setValueAtTime(from, at);
    osc.frequency.exponentialRampToValueAtTime(Math.max(20, to), at + duration);
    const g = ctx.createGain();
    g.gain.setValueAtTime(gain, at);
    g.gain.exponentialRampToValueAtTime(0.001, at + duration);
    osc.connect(g).connect(this.master!);
    osc.start(at);
    osc.stop(at + duration + 0.02);
  }

  shot(alien: boolean): void {
    const ctx = this.ready();
    if (!ctx) return;
    const t = ctx.currentTime;
    this.noise(t, 0.13, 'bandpass', alien ? 950 : 1700, 0.9, 0.8);
    this.tone(t, 0.09, alien ? 520 : 160, 60, 0.5, alien ? 'sawtooth' : 'sine');
  }

  impact(): void {
    const ctx = this.ready();
    if (!ctx) return;
    const t = ctx.currentTime;
    this.tone(t, 0.14, 170, 55, 0.6);
    this.noise(t, 0.06, 'lowpass', 900, 0.4);
  }

  explosion(): void {
    const ctx = this.ready();
    if (!ctx) return;
    const t = ctx.currentTime;
    this.noise(t, 1.3, 'lowpass', 420, 1.4);
    this.tone(t, 0.7, 95, 32, 1.0);
  }

  throwWhoosh(): void {
    const ctx = this.ready();
    if (!ctx) return;
    this.noise(ctx.currentTime, 0.25, 'bandpass', 600, 0.25, 0.6);
  }

  step(): void {
    const ctx = this.ready();
    if (!ctx) return;
    this.noise(ctx.currentTime, 0.035, 'highpass', 2500, 0.06);
  }

  reload(): void {
    const ctx = this.ready();
    if (!ctx) return;
    const t = ctx.currentTime;
    this.noise(t, 0.04, 'highpass', 3000, 0.35);
    this.noise(t + 0.28, 0.05, 'highpass', 2200, 0.45);
  }

  alert(): void {
    const ctx = this.ready();
    if (!ctx) return;
    const t = ctx.currentTime;
    this.tone(t, 0.12, 660, 660, 0.25, 'square');
    this.tone(t + 0.13, 0.18, 990, 990, 0.25, 'square');
  }

  ui(): void {
    const ctx = this.ready();
    if (!ctx) return;
    this.tone(ctx.currentTime, 0.06, 1200, 900, 0.12, 'triangle');
  }

  turn(alien: boolean): void {
    const ctx = this.ready();
    if (!ctx) return;
    const t = ctx.currentTime;
    if (alien) {
      this.tone(t, 0.5, 220, 110, 0.3, 'sawtooth');
    } else {
      this.tone(t, 0.12, 520, 520, 0.2, 'triangle');
      this.tone(t + 0.12, 0.2, 780, 780, 0.2, 'triangle');
    }
  }
}
