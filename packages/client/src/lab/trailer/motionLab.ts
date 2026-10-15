import { motionLayer } from './motion';
import { H, image, W } from './paint';
import { LENGTH, SCRIPT } from './script';

// rotulos.html: the trailer's motion graphics on their own, to look at any
// moment of them (docs/TRAILER.md). The real page is trailer.html (Red's);
// this one only scrubs the layer over a stand-in image.
//   ?bg=<url>   the image under Arte's shots (a still of the game); black without it
//   ?t=<s>      the moment to open at

const params = new URLSearchParams(location.search);
/** The shots with no scene: the layer draws them whole. */
const OWN = new Set(['titulo', 'pantallas', 'logo']);

const layer = motionLayer();
const scene = Object.assign(document.createElement('canvas'), { width: W, height: H });
const sceneCtx = scene.getContext('2d')!;

const view = layer.canvas;
view.style.cssText = 'display: block; width: 100vw; max-height: calc(100vh - 44px); object-fit: contain; background: #111';
const bar = document.createElement('div');
bar.style.cssText = 'display: flex; gap: 10px; align-items: center; height: 44px; padding: 0 12px';
const play = Object.assign(document.createElement('button'), { textContent: '▶' });
const slider = Object.assign(document.createElement('input'), { type: 'range', min: '0', max: String(LENGTH), step: String(1 / 60) });
slider.style.flex = '1';
const label = document.createElement('span');
bar.append(play, slider, label);
document.body.append(view, bar);

let time = Number(params.get('t') ?? 0);

function shotAt(at: number): { shot: string; t: number } {
  const p = SCRIPT.find((s) => at >= s.from && at < s.to) ?? SCRIPT[SCRIPT.length - 1];
  return { shot: p.shot, t: Math.min(at, p.to) - p.from };
}

function show(at: number): { shot: string; t: number } {
  time = Math.max(0, Math.min(LENGTH, at));
  const here = shotAt(time);
  layer.draw(here.shot, here.t, background && !OWN.has(here.shot) ? scene : undefined);
  slider.value = String(time);
  label.textContent = `${time.toFixed(2)} s · ${here.shot} ${here.t.toFixed(2)} s`;
  return here;
}

let background: HTMLImageElement | null = null;
async function setup(): Promise<void> {
  await layer.setup?.();
  const bg = params.get('bg');
  if (bg) {
    background = await image(bg);
    // Cover the frame, like a still of the game would.
    const k = Math.max(W / background.width, H / background.height);
    sceneCtx.drawImage(background, (W - background.width * k) / 2, (H - background.height * k) / 2, background.width * k, background.height * k);
  }
  show(time);
}

let playing = false;
let last = 0;
function tick(now: number): void {
  if (!playing) return;
  show(time + (now - last) / 1000);
  last = now;
  if (time >= LENGTH) playing = false;
  else requestAnimationFrame(tick);
}
play.onclick = () => {
  playing = !playing;
  play.textContent = playing ? '❚❚' : '▶';
  last = performance.now();
  if (playing) requestAnimationFrame(tick);
};
slider.oninput = () => show(Number(slider.value));
addEventListener('keydown', (e) => {
  if (e.key === 'ArrowRight') show(time + (e.shiftKey ? 1 : 1 / 60));
  if (e.key === 'ArrowLeft') show(time - (e.shiftKey ? 1 : 1 / 60));
});

/**
 * Saves the frame shown as .dev-snapshots/trailer/<name> (the UX/UI vite's /__uiux-snap): a .jpg, or else a .png.
 * A name starting with / goes under .dev-snapshots/ itself: many frames written into trailer/ make vite reload
 * this page (screens.ts and cover.ts look into it).
 */
async function snap(name: string): Promise<string> {
  const jpg = name.endsWith('.jpg');
  const file = jpg || name.endsWith('.png') ? name : `${name}.png`;
  const blob = await new Promise<Blob>((done) => layer.canvas.toBlob((b) => done(b!), jpg ? 'image/jpeg' : 'image/png', 0.92));
  return (await fetch(`/__uiux-snap?name=${file.startsWith('/') ? file.slice(1) : `trailer/${file}`}`, { method: 'POST', body: blob })).text();
}

Object.assign(window, { __motion: { show, snap, ready: setup() } });
