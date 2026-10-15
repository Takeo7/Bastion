import { createCampaign, REGIONS, Rng, type RegionId } from '@bastion/engine';
import * as THREE from 'three';
import { GeoscapeSet } from '../strategy/geoscape';
import { Stage } from '../strategy/stage';
import '../styles.css';
import '../strategy/strategy.css';
import '../ui/theme';

// Valley lab (/valle.html): the map of the valley on its own, as the campaign
// shows it (strategy/geoscape.ts: the painted tiles, the Marea, the markers),
// to look at it close and count what a frame costs. A corner shows the draw
// calls, the triangles and the time between frames. Keys: 0 the whole valley;
// 1–8 a close look at each zone; M the menu's backdrop (no markers, the
// Marea's strip only). `?doom=6` draws the Marea at that zombi activity;
// `?contact` contacts every zone. `__valle.look(x, z, scale)` frames any spot;
// `__valle.snap(name)` saves the frame (the map, not its HTML names) to the
// dev server's /__snap. `?timer` drives frames with a timer, for a preview
// that isn't on screen.

const params = new URLSearchParams(location.search);
if (params.has('timer')) {
  window.requestAnimationFrame = (cb) => window.setTimeout(() => cb(performance.now()), 16);
  Object.defineProperty(document, 'hidden', { get: () => false });
}
document.documentElement.dataset.ui = 'mixta';
const stage = new Stage();
stage.element.style.cssText = 'position: fixed; inset: 0';
document.body.append(stage.element);
const valley = new GeoscapeSet(() => undefined);
stage.add('geo', valley);

const campaign = createCampaign([0], new Rng(3));
if (params.has('doom')) campaign.doom = Number(params.get('doom'));
if (params.has('contact')) for (const r of campaign.regions) r.contacted = true;
let menu = params.has('menu');
if (!menu) valley.update(campaign, null);
void stage.show('geo', valley.shot(), true);

/** The draw calls and triangles of the last frame (the stage's renderer adds them up over the frame and resets after it), and the time between frames. */
const gl = (stage as unknown as { gl: THREE.WebGLRenderer }).gl;
const last = { calls: 0, triangles: 0, ms: 0 };
const reset = gl.info.reset.bind(gl.info);
let then = performance.now();
gl.info.reset = () => {
  last.calls = gl.info.render.calls;
  last.triangles = gl.info.render.triangles;
  const now = performance.now();
  last.ms += (now - then - last.ms) * 0.05;
  then = now;
  reset();
};

const readout = document.createElement('pre');
readout.style.cssText = 'position: fixed; left: 8px; top: 8px; margin: 0; padding: 6px 8px; font: 12px/1.4 monospace; color: #eee; background: rgba(0,0,0,0.55); pointer-events: none';
document.body.append(readout);
setInterval(() => {
  readout.textContent = `${last.calls} llamadas · ${last.triangles} triángulos · ${last.ms.toFixed(1)} ms\n0 todo · 1–8 zonas · M menú`;
}, 250);

const zones = Object.keys(REGIONS) as RegionId[];

/** Frames (x, z) of the map at `scale` times the whole valley. */
function look(x: number, z: number, scale = 3): void {
  valley.look(x, z, scale);
}

/** Saves a frame of the stage as .dev-snapshots/<name>.jpg. */
async function snap(name = 'valle', width = 1600): Promise<string> {
  const src = stage.frameNow();
  const c = document.createElement('canvas');
  c.width = width;
  c.height = Math.round((width * src.height) / src.width);
  c.getContext('2d')!.drawImage(src, 0, 0, c.width, c.height);
  await fetch(`/__snap?name=${name}`, { method: 'POST', body: c.toDataURL('image/jpeg', 0.92) });
  return name;
}

window.addEventListener('keydown', (e) => {
  if (e.key === '0') look(0, 0, 1);
  const n = Number(e.key);
  if (n >= 1 && n <= zones.length) {
    const z = REGIONS[zones[n - 1]!];
    look(z.x, z.z, 3.5);
  }
  if (e.key === 'm' || e.key === 'M') {
    menu = !menu;
    if (menu) params.set('menu', '');
    else params.delete('menu');
    location.search = params.toString();
  }
});

(window as unknown as { __valle: unknown }).__valle = { stage, valley, campaign, look, snap, last, zones };
