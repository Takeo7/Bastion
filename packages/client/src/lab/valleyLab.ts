import { createCampaign, REGIONS, Rng, underMarea, type RegionId } from '@bastion/engine';
import * as THREE from 'three';
import { GEOGRAPHY, GeoscapeSet } from '../strategy/geoscape';
import { skyTexture, Stage } from '../strategy/stage';
import { clearRound, lights, pools, sketchTown } from './valleyProposals';
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

/**
 * The sketch town (valleyProposals.ts) on the meadow at (x, z), `scale` times
 * true scale; `dusk` adds its lamps and lit windows. Returns the group, to take it away.
 */
/** The valley's ground (the biggest plain mesh, first in the diorama). */
function ground(): THREE.Mesh {
  let found: THREE.Mesh | null = null;
  valley.scene.traverse((o) => {
    if (o instanceof THREE.Mesh && !(o instanceof THREE.InstancedMesh) && !found && (o.geometry.getAttribute('position')?.count ?? 0) > 5000) found = o;
  });
  return found!;
}

/** The ground's height at (x, z). */
function height(x: number, z: number): number {
  const ray = new THREE.Raycaster(new THREE.Vector3(x, 20, z), new THREE.Vector3(0, -1, 0));
  return ray.intersectObject(ground(), false)[0]?.point.y ?? 0;
}

function sketch(x = -6.2, z = 0.4, axis = 0.35, scale = 1, dusk = false): THREE.Group {
  const town = sketchTown(ground(), x, z, axis, scale);
  if (dusk) town.group.add(lights(town.lamps, false, 0.028 * scale), lights(town.windows, true, 0.04 * scale), pools(town.lamps, 0.1 * scale));
  valley.scene.add(town.group);
  return town.group;
}

/** Takes the valley's models within `r` of (x, z) away, for a sketch in their place (the ground, roads and markers stay). */
function clear(x: number, z: number, r: number, more: [number, number, number][] = []): void {
  const statics = (valley as unknown as { statics: { group: THREE.Group } }).statics.group;
  const meshes: THREE.Mesh[] = [];
  statics.traverse((o) => {
    if (o instanceof THREE.Mesh) meshes.push(o);
  });
  clearRound(meshes, x, z, r, more);
}

/** The valley at dusk, for the sketch read by its lights: the sun low and red, the sky and the haze gone blue, more bloom. */
function dusk(): void {
  const sun = (valley as unknown as { sun: THREE.DirectionalLight }).sun;
  sun.color.set(0xff9c84);
  sun.intensity = 0.55;
  sun.position.set(-22, 4.5, 6);
  valley.scene.traverse((o) => {
    if (o instanceof THREE.HemisphereLight) {
      o.color.set(0x7484b8);
      o.groundColor.set(0x23262e);
      o.intensity = 0.95;
    }
  });
  valley.scene.background = skyTexture(['#141a33', '#2d3558', '#9a6a68']);
  (valley.scene.fog as THREE.Fog).color.set(0x2e3248);
  const mood = valley.mood;
  mood.exposure = 1.05;
  mood.bloom.strength = 1.1;
  mood.bloom.threshold = 0.55;
  mood.grade = { ...mood.grade, saturation: 0.9, shadowTint: [0.85, 0.9, 1.15], highlightTint: [0.98, 0.98, 1.02], lift: [0.03, 0.035, 0.06] };
  (stage as unknown as { applyMood(set: unknown): void }).applyMood(valley);
}

/**
 * The valley from straight above, sharp and saturated, for Arturo's reference
 * (Gameplay, 2026-10-14: «el mapa de la isla de Fortnite visto desde arriba»):
 * the relief shaded smooth, the water turquoise, no haze or cloud, the sun
 * high; one town with streets in each zone that has villages (the sketch
 * town, valleyProposals.ts). `satelliteSnap` draws the names and the
 * campaign's marks over it.
 */
async function satellite(o: { smooth?: boolean; look?: boolean; towns?: boolean } = {}): Promise<void> {
  const { smooth = true, look = true, towns = true } = o;
  const v = valley as unknown as { sun: THREE.DirectionalLight; clouds: THREE.Group; mist: THREE.Mesh; birds: THREE.Mesh };
  valley.scene.fog = new THREE.Fog(0xd8d2d4, 400, 800);
  if (smooth) smoothGround();
  if (look) {
    // Turquoise water.
    valley.scene.traverse((o) => {
      if (o instanceof THREE.Mesh && o.material instanceof THREE.MeshStandardMaterial && o.material.color.getHex() === 0x4f8aa0) {
        o.material.color.set(0x2fb7c2);
        o.material.opacity = 0.93;
      }
    });
    v.clouds.visible = false;
    v.mist.visible = false;
    v.birds.visible = false;
    v.sun.position.set(-7, 30, 9);
    v.sun.intensity = 3.3;
    const mood = valley.mood;
    mood.exposure = 1.1;
    mood.grade = { ...mood.grade, saturation: 1.35, contrast: 1.1, vignette: 0.1, lift: [0.02, 0.025, 0.035] };
    (stage as unknown as { applyMood(set: unknown): void }).applyMood(valley);
  }
  if (towns) {
    // One town with streets in each zone that has villages; their villages gone.
    clear(GEOGRAPHY.towns[0]!.x, GEOGRAPHY.towns[0]!.z, 1.7, GEOGRAPHY.towns.slice(1).map((t) => [t.x, t.z, 1.7] as [number, number, number]).concat([[6.9, -4.0, 2.5]]));
    sketch(-4.4, -4.3, 0.15);
    sketch(6.3, -3.2, 0);
    sketch(-8.8, 0.5, 1.25);
  }
  // Straight down, from far off with a narrow lens: as good as flat.
  const target = new THREE.Vector3(0, 0, 0.3);
  await stage.show('geo', { pos: target.clone().add(new THREE.Vector3(0, 160, 12)), target, fov: 9.6 }, true);
  await shadows();
}

/** The ground smooth: each corner's normal from the height, its colour the mean of its faces'. */
function smoothGround(): void {
  const g = ground();
  const geo = g.geometry;
  const pos = geo.getAttribute('position') as THREE.BufferAttribute;
  const col = geo.getAttribute('color') as THREE.BufferAttribute;
  const sums = new Map<string, [number, number, number, number]>();
  const key = (i: number) => `${pos.getX(i).toFixed(3)},${pos.getZ(i).toFixed(3)}`;
  for (let i = 0; i < pos.count; i++) {
    const k = key(i);
    const e = sums.get(k) ?? [0, 0, 0, 0];
    e[0] += col.getX(i);
    e[1] += col.getY(i);
    e[2] += col.getZ(i);
    e[3]++;
    sums.set(k, e);
  }
  const normal = geo.getAttribute('normal') as THREE.BufferAttribute;
  const n = new THREE.Vector3();
  for (let i = 0; i < pos.count; i++) {
    const e = sums.get(key(i))!;
    col.setXYZ(i, e[0] / e[3], e[1] / e[3], e[2] / e[3]);
    const x = pos.getX(i);
    const z = pos.getZ(i);
    n.set(GEOGRAPHY.heightAt(x - 0.1, z) - GEOGRAPHY.heightAt(x + 0.1, z), 0.2, GEOGRAPHY.heightAt(x, z - 0.1) - GEOGRAPHY.heightAt(x, z + 0.1)).normalize();
    normal.setXYZ(i, n.x, n.y, n.z);
  }
  col.needsUpdate = normal.needsUpdate = true;
  (g.material as THREE.MeshStandardMaterial).flatShading = false;
  (g.material as THREE.MeshStandardMaterial).needsUpdate = true;
}

/** The places' names and the campaign's marks (Atalaya, La Lata on its road, two Nidos, two operations) over the frame, saved as <name>.jpg. */
async function satelliteSnap(name = 'valle_cenital', width = 1600): Promise<string> {
  await new Promise((r) => requestAnimationFrame(() => r(null)));
  const src = stage.element.querySelector('canvas')!;
  const c = document.createElement('canvas');
  c.width = width;
  c.height = Math.round((width * src.height) / src.width);
  const ctx = c.getContext('2d')!;
  ctx.drawImage(src, 0, 0, c.width, c.height);
  const cam = stage.camera;
  const at = (x: number, z: number) => {
    const p = new THREE.Vector3(x, GEOGRAPHY.heightAt(x, z), z).project(cam);
    return [((p.x + 1) / 2) * c.width, ((1 - p.y) / 2) * c.height] as const;
  };
  const load = (id: string) =>
    new Promise<HTMLImageElement>((done) => {
      const img = new Image();
      img.onload = () => done(img);
      img.onerror = () => done(img);
      img.src = `/ui/synty/icons/${id}.webp`;
    });
  const [home, bus, nido, op] = await Promise.all(['home', 'r-mula', 'biohazard', 'target'].map(load));
  const mark = (img: HTMLImageElement, x: number, z: number, ring: string) => {
    const [px, py] = at(x, z);
    ctx.beginPath();
    ctx.arc(px, py, 21, 0, Math.PI * 2);
    ctx.fillStyle = 'rgba(27, 30, 32, 0.94)';
    ctx.fill();
    ctx.lineWidth = 3.5;
    ctx.strokeStyle = ring;
    ctx.stroke();
    if (img.naturalWidth) ctx.drawImage(img, px - 14, py - 14, 28, 28);
  };
  await document.fonts.load('600 20px Oswald');
  const plate = (text: string, x: number, z: number, lost: boolean) => {
    const [px, py] = at(x, z);
    ctx.font = '600 19px Oswald, Barlow, sans-serif';
    const w = ctx.measureText(text.toUpperCase()).width + 24;
    ctx.fillStyle = 'rgba(27, 30, 32, 0.9)';
    ctx.beginPath();
    ctx.roundRect(px - w / 2, py + 18, w, 28, 3);
    ctx.fill();
    ctx.fillStyle = lost ? '#f0503c' : '#f2c230';
    ctx.fillRect(px - w / 2, py + 18, 4, 28);
    ctx.fillStyle = lost ? '#f7b2a8' : '#eef0ea';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(text.toUpperCase(), px + 2, py + 33);
    ctx.beginPath();
    ctx.arc(px, py, 7, 0, Math.PI * 2);
    ctx.fillStyle = lost ? '#f0503c' : '#f2c230';
    ctx.fill();
    ctx.lineWidth = 2.5;
    ctx.strokeStyle = '#1b1e20';
    ctx.stroke();
  };
  for (const id of zones) plate(REGIONS[id].name, REGIONS[id].x, REGIONS[id].z, underMarea(campaign, id));
  mark(home, GEOGRAPHY.atalaya.x, GEOGRAPHY.atalaya.z, '#f2c230');
  const road = GEOGRAPHY.road('weu', 'oce');
  const [bx, bz] = road[Math.round(road.length * 0.6)]!;
  mark(bus, bx, bz, '#f2c230');
  for (const id of ['afr', 'na'] as RegionId[]) mark(nido, REGIONS[id].x + 0.8, REGIONS[id].z - 0.6, '#f0503c');
  for (const id of ['sa', 'eeu'] as RegionId[]) mark(op, REGIONS[id].x + 0.8, REGIONS[id].z - 0.6, '#f2c230');
  await fetch(`/__snap?name=${name}`, { method: 'POST', body: c.toDataURL('image/jpeg', 0.92) });
  return name;
}

(window as unknown as { __valle: unknown }).__valle = { stage, valley, campaign, look, shadows, snap, last, zones, sketch, clear, height, dusk, satellite, satelliteSnap };
