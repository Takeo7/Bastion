import { createCampaign, REGIONS, Rng, type RegionId } from '@bastion/engine';
import * as THREE from 'three';
import { GeoscapeSet } from '../strategy/geoscape';
import { Stage } from '../strategy/stage';
import '../styles.css';
import '../strategy/strategy.css';

// Valley lab (/valle.html): the map of the valley on its own, as the campaign
// shows it, to look at it close and count what a frame costs. A corner shows
// the draw calls and triangles of the last frame (the shadow map is drawn
// only when something moves: `__valle.shadows()` forces one and reports it).
// Keys: 0 the whole valley; 1–8 a close look at each zone (the campaign's
// closest zoom); M the menu's backdrop (no markers, the Marea's strip only).
// `?doom=6` draws the Marea at that zombi activity; `?contact` contacts every
// zone. `__valle.look(x, z, distance)` frames any spot; `__valle.snap(name)`
// saves the frame to the dev server's /__snap. `?timer` drives frames with a
// timer, for a preview that isn't on screen.

const params = new URLSearchParams(location.search);
if (params.has('timer')) {
  window.requestAnimationFrame = (cb) => window.setTimeout(() => cb(performance.now()), 16);
  Object.defineProperty(document, 'hidden', { get: () => false });
}
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

const whole = valley.shot();
void stage.show('geo', whole, true);

/** The draw calls and triangles of the last frame (the stage's renderer adds them up over the frame and resets after it). */
const gl = (stage as unknown as { gl: THREE.WebGLRenderer }).gl;
const last = { calls: 0, triangles: 0 };
const reset = gl.info.reset.bind(gl.info);
gl.info.reset = () => {
  last.calls = gl.info.render.calls;
  last.triangles = gl.info.render.triangles;
  reset();
};

const readout = document.createElement('pre');
readout.style.cssText = 'position: fixed; left: 8px; top: 8px; margin: 0; padding: 6px 8px; font: 12px/1.4 monospace; color: #eee; background: rgba(0,0,0,0.55); pointer-events: none';
document.body.append(readout);
setInterval(() => {
  readout.textContent = `${last.calls} llamadas · ${Math.round(last.triangles / 1000)}k triángulos\n0 todo · 1–8 zonas · M menú`;
}, 250);

/** The camera over (x, z) of the map, from the campaign's angle, `distance` away. */
function look(x: number, z: number, distance = 10): void {
  const back = whole.pos.clone().sub(whole.target).normalize();
  const target = new THREE.Vector3(x, 0, z);
  void stage.show('geo', { ...whole, pos: target.clone().addScaledVector(back, distance), target });
}

const zones = Object.keys(REGIONS) as RegionId[];

/** Draws the shadow map on the next frame and reports that frame. */
async function shadows(): Promise<typeof last> {
  gl.shadowMap.needsUpdate = true;
  await new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
  return { ...last };
}

/** Saves the stage's last frame as .dev-snapshots/<name>.jpg. */
async function snap(name = 'valle', width = 1600): Promise<string> {
  await new Promise((r) => requestAnimationFrame(() => r(null)));
  const src = stage.element.querySelector('canvas')!;
  const c = document.createElement('canvas');
  c.width = width;
  c.height = Math.round((width * src.height) / src.width);
  c.getContext('2d')!.drawImage(src, 0, 0, c.width, c.height);
  await fetch(`/__snap?name=${name}`, { method: 'POST', body: c.toDataURL('image/jpeg', 0.92) });
  return name;
}

window.addEventListener('keydown', (e) => {
  if (e.key === '0') void stage.show('geo', whole);
  const n = Number(e.key);
  if (n >= 1 && n <= zones.length) {
    const z = REGIONS[zones[n - 1]!];
    // The campaign's closest zoom is 0.4 of the whole shot's distance.
    look(z.x, z.z, whole.pos.distanceTo(whole.target) * 0.4);
  }
  if (e.key === 'm' || e.key === 'M') {
    menu = !menu;
    if (menu) params.set('menu', '');
    else params.delete('menu');
    location.search = params.toString();
  }
});

(window as unknown as { __valle: unknown }).__valle = { stage, valley, campaign, look, shadows, snap, last, zones };
