import { render } from '../../audio/bank';
import { audio, type PlayOptions } from '../../audio/engine';
import { SOUNDS } from '../../audio/sounds';
import { clock } from './clock';

// The capture (docs/TRAILER.md): each frame composed into one 1920×1080
// canvas and sent, raw, to the receiver (tools/trailer/receiver.ts), which
// feeds ffmpeg; and the sounds the game plays during a take, noted with their
// time and mixed into the take's own track.

export const WIDTH = 1920;
export const HEIGHT = 1080;
export const FPS = 60;

/** Draws every canvas in `stage`, in order and where it sits, onto `ctx` (black first). False if there was none. */
export function composeStage(stage: HTMLElement, ctx: CanvasRenderingContext2D): boolean {
  ctx.globalCompositeOperation = 'source-over';
  ctx.globalAlpha = 1;
  ctx.fillStyle = '#000';
  ctx.fillRect(0, 0, WIDTH, HEIGHT);
  const box = stage.getBoundingClientRect();
  // The stage is shown scaled to the window; its own size is always 1920×1080.
  const scale = box.width / stage.offsetWidth || 1;
  let drawn = false;
  for (const c of stage.querySelectorAll('canvas')) {
    const r = c.getBoundingClientRect();
    if (!c.width || !c.height || !r.width || !r.height) continue;
    ctx.drawImage(c, (r.left - box.left) / scale, (r.top - box.top) / scale, r.width / scale, r.height / scale);
    drawn = true;
  }
  return drawn;
}

// ------------------------------------------------------------------ frames

/** Where the receiver is: the page's own address when it serves the page, else `&receptor=http://…:3392`. */
const RECEIVER = new URLSearchParams(location.search).get('receptor') ?? '';

async function post(path: string, body?: BodyInit): Promise<Response> {
  const res = await fetch(`${RECEIVER}/__trailer/${path}`, { method: 'POST', body });
  if (!res.ok) throw new Error(`receptor: ${path} → ${res.status} ${await res.text()}`);
  return res;
}

/**
 * One clip on its way to ffmpeg: open, frames in order, close. The frames go
 * over a WebSocket (receiver.ts says why not by POST), each answered once
 * ffmpeg has it; one travels while the next is drawn.
 */
export class Clip {
  private ws: WebSocket | null = null;
  private sending: Promise<void> = Promise.resolve();
  private answer: ((reply: string) => void) | null = null;

  /** `name`: where it goes under .dev-snapshots/trailer/, without extension. `alpha`: a webm with transparency (UI's loose logo). */
  constructor(
    private readonly name: string,
    private readonly alpha = false,
  ) {}

  /** Milliseconds spent reading the pixels, and waiting for the previous frame to arrive. */
  readonly spent = { read: 0, wait: 0 };

  async open(): Promise<void> {
    const name = encodeURIComponent(this.name);
    await post(`start?name=${name}&w=${WIDTH}&h=${HEIGHT}&fps=${FPS}${this.alpha ? '&alfa' : ''}`);
    const base = new URL(RECEIVER || location.origin);
    const ws = new WebSocket(`${base.protocol === 'https:' ? 'wss' : 'ws'}://${base.host}/__trailer/frames?name=${name}`);
    ws.binaryType = 'arraybuffer';
    ws.onmessage = (e) => this.answer?.(String(e.data));
    await new Promise<void>((resolve, reject) => {
      ws.onopen = () => resolve();
      ws.onerror = () => reject(new Error('receptor: el WebSocket de los fotogramas no abre'));
    });
    ws.onclose = () => this.answer?.('error: el receptor ha cerrado la conexión');
    this.ws = ws;
  }

  /** Sends what `ctx` holds now. Resolves once the previous frame has arrived. */
  async frame(ctx: CanvasRenderingContext2D): Promise<void> {
    const t0 = clock.realNow();
    const pixels = ctx.getImageData(0, 0, WIDTH, HEIGHT).data;
    const t1 = clock.realNow();
    await this.sending;
    this.spent.read += t1 - t0;
    this.spent.wait += clock.realNow() - t1;
    this.sending = new Promise<void>((resolve, reject) => {
      this.answer = (reply) => (reply.startsWith('error') ? reject(new Error(`receptor: ${reply}`)) : resolve());
    });
    this.ws!.send(pixels);
  }

  /** Waits for the last frame and for ffmpeg to finish the file; returns what the receiver says. */
  async close(): Promise<string> {
    await this.sending;
    this.ws?.close();
    return (await post(`end?name=${encodeURIComponent(this.name)}`)).text();
  }
}

/** Saves a file through the receiver, under .dev-snapshots/trailer/. */
export async function save(name: string, body: Blob | ArrayBuffer): Promise<void> {
  await post(`file?name=${encodeURIComponent(name)}`, body);
}

// ------------------------------------------------------------------ sounds

const RATE = 48000;

interface Cue {
  at: number;
  buffer: AudioBuffer;
  o: PlayOptions;
}

/**
 * The game's sounds during a take. While on, audio.play notes each sound with
 * the clock's time instead of playing it; at the end they are mixed, offline,
 * into the take's track, in step with its frames.
 */
export class SoundTake {
  private cues: Cue[] = [];
  private start = 0;
  private readonly play = audio.play.bind(audio);

  /** The sound context exists (without a click: nothing is heard), silent, and every sound rendered ahead. */
  static async prepare(): Promise<void> {
    (audio as unknown as { unlock(): void }).unlock();
    audio.setVolumes({ master: 0 });
    await Promise.all(Object.keys(SOUNDS).map((name) => render(name)));
  }

  begin(): void {
    this.cues = [];
    this.start = clock.now();
    audio.play = (buffer, o = {}) => {
      // The interface's clicks are not the trailer's.
      if (o.bus !== 'ui') this.cues.push({ at: (clock.now() - this.start) / 1000 + (o.delay ?? 0), buffer, o });
      return null;
    };
  }

  end(): void {
    audio.play = this.play;
  }

  get count(): number {
    return this.cues.length;
  }

  /** The take's track: `seconds` long, stereo, at 48 kHz, as a WAV file. */
  async mix(seconds: number): Promise<ArrayBuffer> {
    const ctx = new OfflineAudioContext(2, Math.ceil(seconds * RATE), RATE);
    // Like the game's chain (engine.ts): a compressor last and a shared reverb.
    const comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -14;
    comp.knee.value = 10;
    comp.ratio.value = 4;
    comp.attack.value = 0.004;
    comp.release.value = 0.25;
    comp.connect(ctx.destination);
    const reverb = ctx.createConvolver();
    reverb.buffer = impulse(ctx, 2.4, 2.8);
    const wet = ctx.createGain();
    wet.gain.value = 0.55;
    reverb.connect(wet).connect(comp);
    for (const { at, buffer, o } of this.cues) {
      if (at >= seconds) continue;
      const src = ctx.createBufferSource();
      src.buffer = buffer;
      src.playbackRate.value = o.rate ?? 1;
      const gain = ctx.createGain();
      gain.gain.value = o.gain ?? 1;
      const pan = ctx.createStereoPanner();
      pan.pan.value = Math.max(-1, Math.min(1, o.pan ?? 0));
      src.connect(gain).connect(pan).connect(comp);
      if (o.reverb) {
        const send = ctx.createGain();
        send.gain.value = o.reverb;
        pan.connect(send).connect(reverb);
      }
      src.start(Math.max(0, at));
    }
    return wav(await ctx.startRendering());
  }
}

/** A room's echo: noise falling away over `seconds` (as engine.ts makes it). */
function impulse(ctx: BaseAudioContext, seconds: number, decay: number): AudioBuffer {
  const length = Math.floor(ctx.sampleRate * seconds);
  const buf = ctx.createBuffer(2, length, ctx.sampleRate);
  for (let ch = 0; ch < 2; ch++) {
    const d = buf.getChannelData(ch);
    for (let i = 0; i < length; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / length, decay);
  }
  return buf;
}

/** An AudioBuffer as a 16-bit PCM WAV file. */
export function wav(buf: AudioBuffer): ArrayBuffer {
  const channels = buf.numberOfChannels;
  const frames = buf.length;
  const out = new DataView(new ArrayBuffer(44 + frames * channels * 2));
  const text = (at: number, s: string) => [...s].forEach((c, i) => out.setUint8(at + i, c.charCodeAt(0)));
  text(0, 'RIFF');
  out.setUint32(4, 36 + frames * channels * 2, true);
  text(8, 'WAVE');
  text(12, 'fmt ');
  out.setUint32(16, 16, true);
  out.setUint16(20, 1, true);
  out.setUint16(22, channels, true);
  out.setUint32(24, buf.sampleRate, true);
  out.setUint32(28, buf.sampleRate * channels * 2, true);
  out.setUint16(32, channels * 2, true);
  out.setUint16(34, 16, true);
  text(36, 'data');
  out.setUint32(40, frames * channels * 2, true);
  const data = Array.from({ length: channels }, (_, c) => buf.getChannelData(c));
  let at = 44;
  for (let i = 0; i < frames; i++) {
    for (let c = 0; c < channels; c++) {
      const s = Math.max(-1, Math.min(1, data[c]![i]!));
      out.setInt16(at, s < 0 ? s * 0x8000 : s * 0x7fff, true);
      at += 2;
    }
  }
  return out.buffer;
}
