// 0.10.1's mix (audio/engine.ts, radio.ts) rendered offline from a script of events (guiones/), for comparing with
// Unity's Mixer (mezcla.mjs; docs/UNITY-SONIDO.md). Runs in the sound tools' Electron page (main.js).
import { audio } from '../../../packages/client/src/audio/engine';
import { morse } from '../../../packages/client/src/audio/radio';

const fs = (window as any).require('fs');
const path = (window as any).require('path');

function rng(seed: number): () => number {
  let s = seed >>> 0 || 1;
  return () => {
    s = (s + 0x6d2b79f5) >>> 0;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** A 16-bit WAV as Chromium's decoder reads it (s / 32768), into a buffer at the file's own rate: played, it is resampled as bank.ts's were. */
function wav(ctx: BaseAudioContext, file: string): AudioBuffer {
  const b: Uint8Array = fs.readFileSync(file);
  const v = new DataView(b.buffer, b.byteOffset, b.byteLength);
  let at = 12;
  let channels = 1;
  let rate = 44100;
  while (at < b.length) {
    const id = String.fromCharCode(b[at]!, b[at + 1]!, b[at + 2]!, b[at + 3]!);
    const size = v.getUint32(at + 4, true);
    if (id === 'fmt ') {
      channels = v.getUint16(at + 10, true);
      rate = v.getUint32(at + 12, true);
    } else if (id === 'data') {
      const n = size / 2 / channels;
      const buf = ctx.createBuffer(channels, n, rate);
      for (let c = 0; c < channels; c++) {
        const d = buf.getChannelData(c);
        for (let i = 0; i < n; i++) d[i] = v.getInt16(at + 8 + (i * channels + c) * 2, true) / 32768;
      }
      return buf;
    }
    at += 8 + size + (size & 1);
  }
  throw new Error(`${file}: sin datos`);
}

interface Ev {
  at: number;
  do: string;
  [k: string]: any;
}

async function renderMix(scenario: { rate: number; seconds: number; seed: number; events: Ev[] }, dir: string, out: string): Promise<number> {
  Object.defineProperty(Document.prototype, 'hidden', { get: () => false, configurable: true });
  const rate = scenario.rate;
  const off = new OfflineAudioContext(2, Math.ceil(scenario.seconds * rate), rate);
  (window as any).AudioContext = function () {
    return off;
  };
  Math.random = rng(scenario.seed);
  const eng = audio as any;
  eng.muted = false;
  eng.volumes = { master: 0.8, sfx: 0.8, music: 0.5, ambience: 0.6, ui: 0.7 };
  eng.unlock();
  // live() resumes a suspended context before the nodes are made: offline, they would join at a quantum that varies.
  eng.live = () => eng.ctx;
  const buffers = new Map<string, AudioBuffer>();
  for (const e of scenario.events) if (e.file && !buffers.has(e.file)) buffers.set(e.file, wav(off, path.join(dir, e.file)));
  const ids = new Map<string, any>();
  const apply = (e: Ev) => {
    const now = off.currentTime;
    switch (e.do) {
      case 'play':
        ids.set(e.id, audio.play(buffers.get(e.file)!, { bus: e.bus, gain: e.gain, rate: e.rate, pan: e.pan, delay: e.delay, reverb: e.reverb }));
        break;
      case 'stop':
        audio.stop(ids.get(e.id), e.fade);
        break;
      case 'loop':
        ids.set(e.id, audio.loop(buffers.get(e.file)!, { bus: e.bus, gain: e.gain, reverb: e.reverb }));
        break;
      case 'setLoop':
        ids.get(e.id).set(e.level, e.place, e.muffle);
        break;
      case 'endLoop':
        ids.get(e.id).stop(e.windDown);
        break;
      case 'duck':
        audio.duck(e.depth, e.hold, e.release);
        break;
      case 'holdDuck':
        ids.set(e.id, audio.holdDuck(e.depth));
        break;
      case 'release':
        ids.get(e.id)();
        break;
      case 'volumes':
        audio.setVolumes(e.patch);
        break;
      case 'mute':
        audio.toggleMute();
        break;
      case 'morse':
        morse(e.word, e.level, e.unit);
        break;
      case 'music': {
        // music.ts apply: the piece's output faded in, into the music bus and 0.35 of it to the reverb.
        const g = off.createGain();
        g.gain.setValueAtTime(0, now);
        g.gain.linearRampToValueAtTime(1, now + e.fade);
        g.connect(audio.bus('music')!);
        const send = off.createGain();
        send.gain.value = 0.35;
        g.connect(send).connect(audio.reverb('music')!);
        const src = off.createBufferSource();
        src.buffer = buffers.get(e.file)!;
        src.loop = true;
        src.connect(g);
        src.start(now);
        ids.set(e.id, g);
        break;
      }
      case 'fade': {
        const g: GainNode = ids.get(e.id);
        g.gain.cancelScheduledValues(now);
        g.gain.setValueAtTime(g.gain.value, now);
        g.gain.linearRampToValueAtTime(e.level, now + e.seconds);
        break;
      }
      default:
        throw new Error(`no sé hacer «${e.do}»`);
    }
  };
  const quantum = 128 / rate;
  const byTime = new Map<number, Ev[]>();
  for (const e of scenario.events) {
    const q = Math.round(e.at / quantum);
    byTime.set(q, [...(byTime.get(q) ?? []), e]);
  }
  for (const [q, evs] of byTime) {
    if (q === 0) {
      for (const e of evs) apply(e);
      continue;
    }
    void off.suspend(q * quantum).then(() => {
      for (const e of evs) apply(e);
      off.resume().catch(() => undefined);
    });
  }
  const buf = await off.startRendering();
  const l = buf.getChannelData(0);
  const r = buf.getChannelData(1);
  const data = new Float32Array(l.length * 2);
  for (let i = 0; i < l.length; i++) {
    data[i * 2] = l[i]!;
    data[i * 2 + 1] = r[i]!;
  }
  fs.writeFileSync(out, new Uint8Array(data.buffer));
  return l.length;
}

(window as any).renderMix = renderMix;
