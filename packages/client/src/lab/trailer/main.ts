// The clock goes first: everything after it reads the virtual time.
import { clock } from './clock';
import { shadowsChanged } from '../../game/renderer';
import { setAutoQuality, setGraphics } from '../../settings';
import { Clip, composeStage, FPS, HEIGHT, save, SoundTake, wav, WIDTH } from './capture';
import { renderScore } from './music';
import { placement, SCRIPT } from './script';
import type { MotionLayer, Shot } from './types';

// The trailer page (/trailer.html, docs/TRAILER.md). Arte's shots
// (shots/<id>.ts) draw in #escena; UI's motion graphics (motion.ts) copy that
// image and draw on top; this page runs them on the virtual clock (clock.ts)
// and, to record, sends each frame to the receiver (tools/trailer/receiver.ts).
//
//   ?toma=<id>&ver    the shot live, once, with the motion graphics (time runs in real time)
//   ?toma=<id>        records it: tomas/<id>.mp4 and its sounds, tomas/<id>.wav
//   ?todas            records every shot in script.ts that exists, one after another
//   &vacias           with ?todas: those not made yet too, black under the motion graphics (an animatic)
//   ?musica[=30]      renders the music (or its first 30 s) to musica/*.wav, and plays it here
//   &ss=2             drawn at 3840×2160 and brought down to 1920×1080
//   &semilla=N        Math.random's seed (the same take each time)
//   &alfa             a webm with transparency instead (UI's loose logo)
//   &sincapa          the shot alone, without the motion graphics
//   &receptor=URL     where the receiver is, if it does not serve this page

const params = new URLSearchParams(location.search);
const SHOTS = import.meta.glob<{ default: Shot }>('./shots/*.ts');
const MOTION = import.meta.glob<{ motionLayer(): MotionLayer }>('./motion.ts');

const frame = document.getElementById('marco')!;
const stage = document.getElementById('escena')!;
const status = document.getElementById('estado')!;

// The image of each frame before the motion graphics: the stage's canvases, composed.
const scene = Object.assign(document.createElement('canvas'), { width: WIDTH, height: HEIGHT });
const sceneCtx = scene.getContext('2d', { willReadFrequently: true })!;

/** The 1920×1080 frame fitted to the window (its own size never changes, so the renderers keep theirs). */
function fit(): void {
  const k = Math.min(innerWidth / WIDTH, innerHeight / HEIGHT);
  frame.style.transform = `scale(${k})`;
}
addEventListener('resize', fit);
fit();

function say(text: string): void {
  status.textContent = text;
}

// The game at «alto», whatever this browser had (the page keeps its own settings: clock.ts).
setGraphics({ effects: 'high', shadows: true, resolution: 1, fps: 0, showFps: false });
setAutoQuality(false);
// `&debug`: the clock and the graphics settings from the console.
if (params.has('debug')) Object.assign(window, { __trailer: { clock, setGraphics, shadowsChanged, stage } });

async function load(id: string): Promise<Shot> {
  const module = SHOTS[`./shots/${id}.ts`];
  // `&vacias`: a shot of the script not made yet is black for its time, with the motion graphics over it (the animatic).
  const place = placement(id);
  if (!module && place && params.has('vacias')) return { id, duration: place.to - place.from, setup: async () => {} };
  if (!module) throw new Error(`No hay toma «${id}» (src/lab/trailer/shots/${id}.ts). Hay: ${available().join(', ') || 'ninguna'}.`);
  return (await module()).default;
}

function available(): string[] {
  return Object.keys(SHOTS).map((k) => k.replace(/^\.\/shots\/|\.ts$/g, ''));
}

/** UI's layer, if it exists yet (and not `&sincapa`); its canvas is the one shown and captured. */
async function motion(): Promise<MotionLayer | null> {
  const module = MOTION['./motion.ts'];
  if (!module || params.has('sincapa')) return null;
  const layer = (await module()).motionLayer();
  await layer.setup?.();
  return layer;
}

/** Sets a shot up and wires each frame: its camera before the drawing, the composing after. */
async function prepare(id: string): Promise<{ shot: Shot; out: HTMLCanvasElement; at: { t: number } }> {
  say(`${id}: preparando…`);
  const shot = await load(id);
  await shot.setup(stage);
  const layer = await motion();
  const out = layer?.canvas ?? scene;
  out.id = 'imagen';
  frame.append(out);
  const at = { t: 0 };
  clock.onFrame(
    () => shot.frame?.(at.t),
    () => {
      const drawn = composeStage(stage, sceneCtx);
      layer?.draw(id, at.t, drawn ? scene : undefined);
    },
  );
  return { shot, out, at };
}

/** `?toma=<id>&ver`: plays the shot once in real time, then holds its last frame. */
async function view(id: string): Promise<void> {
  const { shot, at } = await prepare(id);
  const start = clock.now();
  const place = placement(id);
  const tick = () => {
    at.t = Math.min(shot.duration, (clock.now() - start) / 1000);
    say(`${id}: ${at.t.toFixed(2)} / ${shot.duration} s${place ? ` · en el tráiler, ${(place.from + at.t).toFixed(2)} s` : ''} · R para repetir`);
    if (at.t < shot.duration) requestAnimationFrame(tick);
  };
  requestAnimationFrame(tick);
  addEventListener('keydown', (e) => e.key.toLowerCase() === 'r' && location.reload());
}

/** `?toma=<id>`: records the shot, frame by frame, and its sounds. */
async function record(id: string): Promise<void> {
  say(`${id}: preparando los sonidos…`);
  await SoundTake.prepare();
  const { shot, out, at } = await prepare(id);
  const ctx = out.getContext('2d')!;
  await clock.stop();
  // Shaders compiling, textures and shadows settling: frames drawn with time standing still. The
  // Renderer looks for shadow casters every 30 frames (renderer.ts CASTERS_EVERY): past that.
  for (let i = 0; i < 40; i++) await clock.still();
  // An animatic's empty shot goes apart (vacias/), never to be taken for a real take.
  const folder = SHOTS[`./shots/${id}.ts`] ? 'tomas' : 'vacias';
  const clip = new Clip(`${folder}/${id}`, params.has('alfa'));
  await clip.open();
  const sounds = new SoundTake();
  sounds.begin();
  const total = Math.round(shot.duration * FPS);
  const began = clock.realNow();
  let drawing = 0;
  for (let n = 0; n < total; n++) {
    at.t = n / FPS;
    // The sun's shadows drawn again every frame: the game redraws those of moving poses ten times a second, to save.
    shadowsChanged();
    const t0 = clock.realNow();
    if (n === 0) await clock.still();
    else await clock.step();
    drawing += clock.realNow() - t0;
    await clip.frame(ctx);
    if (n % 15 === 0 || n === total - 1) {
      const rate = (n + 1) / ((clock.realNow() - began) / 1000);
      say(`${id}: fotograma ${n + 1} / ${total} · ${rate.toFixed(1)} por segundo · quedan ${Math.ceil((total - n - 1) / rate)} s`);
    }
  }
  sounds.end();
  say(`${id}: cerrando el vídeo…`);
  const done = await clip.close();
  await save(`${folder}/${id}.wav`, await sounds.mix(shot.duration));
  shot.teardown?.();
  const per = (ms: number) => `${(ms / total).toFixed(0)} ms`;
  const summary = `${id}: hecho en ${((clock.realNow() - began) / 1000).toFixed(0)} s · ${sounds.count} sonidos · ${done} · por fotograma: dibujar ${per(drawing)}, leer ${per(clip.spent.read)}, esperar al receptor ${per(clip.spent.wait)}`;
  say(summary);
  void fetch(`${params.get('receptor') ?? ''}/__trailer/log`, { method: 'POST', body: summary });
}

/** `?musica` (or `?musica=30`, the first 30 s): renders the music, saves it through the receiver and offers it to listen to. */
async function score(): Promise<void> {
  say('música: componiendo…');
  const seconds = Number(params.get('musica')) || undefined;
  const began = clock.realNow();
  const buf = await renderScore(seconds);
  const file = seconds ? `musica/maqueta-${seconds}s.wav` : 'musica/musica.wav';
  const data = wav(buf);
  const player = Object.assign(document.createElement('audio'), { controls: true, src: URL.createObjectURL(new Blob([data], { type: 'audio/wav' })) });
  Object.assign(player.style, { position: 'fixed', left: '8px', top: '8px', width: '480px' });
  document.body.append(player);
  try {
    await save(file, data);
    say(`música: ${buf.duration.toFixed(1)} s en ${((clock.realNow() - began) / 1000).toFixed(1)} s · ${file}`);
  } catch {
    say(`música: ${buf.duration.toFixed(1)} s · sin receptor, no se ha guardado (escúchala aquí)`);
  }
}

async function main(): Promise<void> {
  if (params.has('musica')) return score();
  const id = params.get('toma');
  if (params.has('todas')) {
    // One page per shot: each starts clean (WebGL contexts, the game's caches).
    const order = SCRIPT.map((p) => p.shot).filter((s) => params.has('vacias') || available().includes(s));
    const next = id ? order[order.indexOf(id) + 1] : order[0];
    if (id) await record(id);
    if (next) {
      params.set('toma', next);
      location.search = `?${params}`;
    } else say(`Todas grabadas: ${order.join(', ')}. Sin toma todavía: ${SCRIPT.map((p) => p.shot).filter((s) => !order.includes(s)).join(', ') || 'ninguna'}.`);
    return;
  }
  if (!id) {
    say(`Tomas: ${available().join(', ') || 'ninguna'}. Abre ?toma=<id>&ver para verla, o ?toma=<id> para grabarla con el receptor.`);
    return;
  }
  if (params.has('ver')) await view(id);
  else await record(id);
}

main().catch((e: Error) => {
  console.error(e);
  say(`Error: ${e.message}`);
  void fetch(`${params.get('receptor') ?? ''}/__trailer/log`, { method: 'POST', body: `${params.get('toma') ?? '-'}: ${e.stack ?? e.message}` }).catch(() => {});
});
