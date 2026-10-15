import * as THREE from 'three';
import { canStandOn, createMission, DIRS4, levelCount, Rng, teamVisibility, type Biome, type GameState, type MissionKind, type Site, type Vec2 } from '@bastion/engine';
import type { Focus } from '../game/buildings';
import { setGraphics, type EffectsLevel } from '../settings';
import { CameraRig } from '../game/camera';
import { CROWD } from '../game/crowd';
import { setTerrain } from '../game/coords';
import { MapView } from '../game/mapView';
import { Renderer } from '../game/renderer';
import { UnitViews } from '../game/unitViews';
import '../styles.css';

// Reference block (/referencia.html): a generated mission drawn with the
// game's own code (Renderer, MapView, UnitViews) from the game's camera, to
// set the battlefield's look against Synty's promo captures and to measure
// what a frame costs. Measures, per frame drawn:
//  - CPU: our loop in JS (frame functions, then the render calls), in ms;
//  - GPU: the frame's GPU time, by timer query where the browser has one;
//  - draw calls and triangles of each pass (shadow maps, scene, AO, bloom, output);
//  - objects in the scene (what three walks every frame).
// `__ref.bench(300)` draws 300 frames back to back, uncapped, and reports the
// JS cost and the throughput (frames finished per second). Keys: 1/2/3 alto,
// medio, bajo; N day/night; U units; C capture camera / play camera; P snapshot.
// URL: ?biome=city&seed=7&kind=recovery&night&fog&site=roadside (kind: the mission, a Limpieza by default; site: a
// scenario of the biome, roadside in the wilds, industrial in town; fog: the squad's own sight instead of
// everything; `__ref.peek()` then stands a soldier outside a window);
// ?map=torre draws a hand-made map instead. `__ref.focus` is what the
// cutaway follows (the selected soldier, the cursor, the active storey).

const params = new URLSearchParams(location.search);
const biome = (params.get('biome') ?? 'city') as Biome;
const seed = Number(params.get('seed') ?? 7);
const site = (params.get('site') ?? undefined) as Site | undefined;

const container = document.getElementById('ref')!;
const report = document.getElementById('ref-report')!;
const renderer = new Renderer(container);
const gl = renderer.gl;
gl.info.autoReset = false;

const handmade = params.get('map');
const state: GameState = handmade
  ? createMission({ map: { kind: 'handmade', id: handmade }, slots: [0], rng: new Rng(seed), difficulty: 3 })
  : createMission({ map: { kind: 'generated', biome, seed, site }, slots: [0], rng: new Rng(seed), kind: (params.get('kind') as MissionKind) ?? 'elimination', difficulty: 3 });
setTerrain(state);
const mapView = new MapView();
const units = new UnitViews();
const rig = new CameraRig(renderer.camera);
renderer.scene.add(mapView.group, units.group);
renderer.fitMap(state.width, state.height);
rig.setBounds(state.width, state.height);
const focus: Focus = { selected: null, hover: null, target: null, level: null };
mapView.watch(renderer.camera, () => focus);
mapView.build(state);
units.build(state);
// Everything in sight: the block is seen as a whole, as in the captures (no roofs off for what is seen inside, then).
// With ?fog, what the squad sees.
const fog = params.has('fog');
mapView.seeInside = fog;
const everything = () => new Array<boolean>(state.width * state.height * levelCount(state)).fill(true);
mapView.setVisibility(fog ? teamVisibility(state, 'xcom') : everything());

/**
 * Stands the first soldier out in the street two tiles from a ground-floor
 * window facing the camera (the `n`th nearest the middle of the map that has room), looking
 * in, and the camera on it; the squad's sight and the fog follow, over a map
 * already explored (as midway through a mission). Returns the window.
 */
function peek(n = 0): Vec2 | null {
  const w = state.width;
  const free = (x: number, y: number) => x >= 0 && y >= 0 && x < w && y < state.height && canStandOn(state.tiles[y * w + x]!) && !state.elev[y * w + x] && state.ground?.[y * w + x] !== 2;
  const windows = state.tiles
    .map((t, i) => (t === 'window' && !state.elev[i] ? i : -1))
    .filter((i) => i >= 0)
    .sort((a, b) => Math.hypot((a % w) - w / 2, Math.floor(a / w) - w / 2) - Math.hypot((b % w) - w / 2, Math.floor(b / w) - w / 2));
  let k = 0;
  for (const i of windows) {
    const x = i % w;
    const y = (i - x) / w;
    // A window on the side the camera looks at (the house isn't between them, so it keeps its roof for that).
    for (const d of DIRS4.filter((q) => q.x > 0 || q.y > 0)) {
      const out = { x: x + 2 * d.x, y: y + 2 * d.y };
      if (!free(x + d.x, y + d.y) || !free(out.x, out.y) || state.ground?.[(y - d.y) * w + x - d.x] !== 2) continue;
      if (k++ < n) continue;
      const soldier = state.units.find((u) => u.team === 'xcom')!;
      soldier.pos = { ...soldier.pos, x: out.x, y: out.y };
      units.build(state);
      mapView.seeInside = true;
      mapView.setVisibility(everything());
      mapView.setVisibility(teamVisibility(state, 'xcom'));
      rig.focus(new THREE.Vector3(x + 0.5, 0, y + 0.5), true);
      return { x, y };
    }
  }
  return null;
}

/** Camera views: the capture's (wide, over the middle of the map) and the game's default. */
const VIEWS = {
  capture: { x: state.width * 0.5, z: state.height * 0.5, distance: 40 },
  play: { x: state.width * 0.35, z: state.height * 0.62, distance: 24 },
};
let viewName: keyof typeof VIEWS = (params.get('view') as keyof typeof VIEWS) ?? 'capture';
function setView(name: keyof typeof VIEWS): void {
  viewName = name;
  const v = VIEWS[name];
  rig.focus(new THREE.Vector3(v.x, 0, v.z), true);
  const r = rig as unknown as { distance: number; desiredDistance: number };
  r.distance = r.desiredDistance = v.distance;
}
setView(viewName);
container.addEventListener('wheel', (e) => rig.zoom(e.deltaY), { passive: true });
let dragging = false;
container.addEventListener('pointerdown', () => (dragging = true));
window.addEventListener('pointerup', () => (dragging = false));
window.addEventListener('pointermove', (e) => dragging && rig.panPixels(e.movementX, e.movementY));

// ----------------------------------------------------------------- measuring

type Pass = 'shadow' | 'scene' | 'ao' | 'bloom' | 'output' | 'direct';
const passCalls: Record<Pass, number> = { shadow: 0, scene: 0, ao: 0, bloom: 0, output: 0, direct: 0 };
const passTris: Record<Pass, number> = { shadow: 0, scene: 0, ao: 0, bloom: 0, output: 0, direct: 0 };
let shadowPasses = 0;

/** Draws made by passes running inside the current one (the shadow maps inside the scene pass): not its own. */
let nested = { calls: 0, tris: 0 };
function counted<T extends (...args: never[]) => unknown>(pass: Pass, fn: T, self: unknown): T {
  return ((...args: Parameters<T>) => {
    const outer = nested;
    nested = { calls: 0, tris: 0 };
    const [c, t] = [gl.info.render.calls, gl.info.render.triangles];
    const out = fn.apply(self, args);
    const [dc, dt] = [gl.info.render.calls - c, gl.info.render.triangles - t];
    passCalls[pass] += dc - nested.calls;
    passTris[pass] += dt - nested.tris;
    nested = { calls: outer.calls + dc, tris: outer.tris + dt };
    return out;
  }) as T;
}

const shadowMap = gl.shadowMap as unknown as { render: (...a: never[]) => void };
const shadowRender = shadowMap.render;
shadowMap.render = (...args: never[]) => {
  const c = gl.info.render.calls;
  shadowRender.apply(gl.shadowMap, args);
  if (gl.info.render.calls > c) shadowPasses++;
};
shadowMap.render = counted('shadow', shadowMap.render, gl.shadowMap);
const post = (renderer as unknown as { post: { composer: { passes: { render: (...a: never[]) => void; constructor: { name: string } }[] } } }).post;
for (const pass of post.composer.passes) {
  const name = pass.constructor.name;
  const kind: Pass = name.includes('Render') ? 'scene' : name.includes('GTAO') ? 'ao' : name.includes('Bloom') ? 'bloom' : 'output';
  pass.render = counted(kind, pass.render, pass);
}

/** Frame start (our first frame function runs before the others). */
let frameStart = 0;
const cpuTimes: number[] = [];
const gpuTimes: number[] = [];
const ctx = gl.getContext() as WebGL2RenderingContext;
const timer = ctx.getExtension('EXT_disjoint_timer_query_webgl2') as { TIME_ELAPSED_EXT: number; GPU_DISJOINT_EXT: number } | null;
const queries: WebGLQuery[] = [];
let query: WebGLQuery | null = null;
let benchRunning = false;

renderer.onFrame(() => {
  frameStart = performance.now();
  gl.info.reset();
  for (const k of Object.keys(passCalls) as Pass[]) passCalls[k] = passTris[k] = 0;
  shadowPasses = 0;
  if (timer && !query && !benchRunning) {
    query = ctx.createQuery()!;
    ctx.beginQuery(timer.TIME_ELAPSED_EXT, query);
  }
});
renderer.onFrame((dt, time) => {
  rig.update(dt);
  mapView.tick(dt);
  units.tick(dt, time);
});
const render = renderer.render.bind(renderer);
renderer.render = (dt = 0) => {
  render(dt);
  passCalls.direct = gl.info.render.calls - (passCalls.shadow + passCalls.scene + passCalls.ao + passCalls.bloom + passCalls.output);
  if (benchRunning) return;
  cpuTimes.push(performance.now() - frameStart);
  if (cpuTimes.length > 240) cpuTimes.shift();
  if (query && timer) {
    ctx.endQuery(timer.TIME_ELAPSED_EXT);
    queries.push(query);
    query = null;
  }
  while (queries.length && timer) {
    const q = queries[0]!;
    if (!ctx.getQueryParameter(q, ctx.QUERY_RESULT_AVAILABLE)) break;
    queries.shift();
    if (!ctx.getParameter(timer.GPU_DISJOINT_EXT)) gpuTimes.push(Number(ctx.getQueryParameter(q, ctx.QUERY_RESULT)) / 1e6);
    ctx.deleteQuery(q);
    if (gpuTimes.length > 240) gpuTimes.shift();
  }
};

const pct = (xs: number[], p: number) => (xs.length ? [...xs].sort((a, b) => a - b)[Math.min(xs.length - 1, Math.floor(xs.length * p))]! : 0);
const avg = (xs: number[]) => (xs.length ? xs.reduce((a, b) => a + b, 0) / xs.length : 0);

/** What the scene holds: objects three walks every frame, meshes it may draw. */
function sceneCounts(): { objects: number; meshes: number; instances: number; auto: number } {
  let objects = 0;
  let meshes = 0;
  let instances = 0;
  let auto = 0;
  renderer.scene.traverse((o) => {
    objects++;
    if (o.matrixAutoUpdate) auto++;
    if ((o as THREE.Mesh).isMesh && o.visible) {
      meshes++;
      instances += (o as THREE.InstancedMesh).isInstancedMesh ? (o as THREE.InstancedMesh).count : 1;
    }
  });
  return { objects, meshes, instances, auto };
}

let lastBench = '';
let preset: EffectsLevel = 'high';
let night = params.has('night');

function show(): void {
  const c = sceneCounts();
  const k = (n: number) => (n > 9999 ? `${(n / 1000).toFixed(0)}k` : `${n}`);
  report.textContent = [
    `${biome} seed ${seed} · ${state.width}×${state.height} · ${viewName} · preset ${preset} · ${night ? 'noche' : 'día'}`,
    `CPU frame  ${avg(cpuTimes).toFixed(2)} ms (p95 ${pct(cpuTimes, 0.95).toFixed(2)})`,
    `GPU frame  ${timer ? `${avg(gpuTimes).toFixed(2)} ms (p95 ${pct(gpuTimes, 0.95).toFixed(2)})` : 'n/a (no timer query)'}`,
    `draws      shadow ${passCalls.shadow} (${shadowPasses} pass) · scene ${passCalls.scene} · ao ${passCalls.ao} · bloom ${passCalls.bloom} · out ${passCalls.output}${passCalls.direct ? ` · direct ${passCalls.direct}` : ''} = ${gl.info.render.calls}`,
    `triangles  shadow ${k(passTris.shadow)} · scene ${k(passTris.scene)} · ao ${k(passTris.ao)} = ${k(gl.info.render.triangles)}`,
    `scene      ${c.objects} objects (${c.auto} auto-matrix) · ${c.meshes} meshes · ${k(c.instances)} instances`,
    `programs   ${gl.info.programs?.length ?? '?'} · geometries ${gl.info.memory.geometries} · textures ${gl.info.memory.textures}`,
    lastBench,
  ].join('\n');
}
setInterval(show, 500);

/** Draws n frames back to back (no vsync): JS cost per frame, and frames finished per second. */
function bench(n = 300): { js: number; total: number; fps: number; gpu: number | null } {
  benchRunning = true;
  const fns = (renderer as unknown as { frameFns: Set<(dt: number, t: number) => void> }).frameFns;
  let js = 0;
  let gpuQuery: WebGLQuery | null = null;
  if (timer) {
    gpuQuery = ctx.createQuery()!;
    ctx.beginQuery(timer.TIME_ELAPSED_EXT, gpuQuery);
  }
  ctx.finish();
  const t0 = performance.now();
  for (let i = 0; i < n; i++) {
    const s = performance.now();
    for (const fn of fns) fn(1 / 240, i / 240);
    renderer.render(1 / 240);
    js += performance.now() - s;
  }
  if (timer && gpuQuery) ctx.endQuery(timer.TIME_ELAPSED_EXT);
  ctx.finish();
  const total = (performance.now() - t0) / n;
  benchRunning = false;
  let gpu: number | null = null;
  if (timer && gpuQuery) {
    // The result arrives a little later; poll for it.
    const poll = () => {
      if (!ctx.getQueryParameter(gpuQuery!, ctx.QUERY_RESULT_AVAILABLE)) return void setTimeout(poll, 50);
      gpu = Number(ctx.getQueryParameter(gpuQuery!, ctx.QUERY_RESULT)) / 1e6 / n;
      lastBench = lastBench.replace(/GPU [^·]*$/, `GPU ${gpu.toFixed(2)} ms`);
    };
    poll();
  }
  lastBench = `bench ${n}: JS ${(js / n).toFixed(2)} ms · finished ${total.toFixed(2)} ms/frame (${(1000 / total).toFixed(0)} fps) · GPU …`;
  return { js: js / n, total, fps: 1000 / total, gpu };
}

/** One frame, counted: draws and triangles of each pass, shadow passes, objects in the scene. */
function counts(): Record<string, number | string> {
  const fns = (renderer as unknown as { frameFns: Set<(dt: number, t: number) => void> }).frameFns;
  for (const fn of fns) fn(1 / 60, performance.now() / 1000);
  renderer.render(1 / 60);
  const c = sceneCounts();
  return {
    preset,
    night: night ? 'noche' : 'día',
    total: gl.info.render.calls,
    shadow: passCalls.shadow,
    shadowPasses,
    scene: passCalls.scene,
    ao: passCalls.ao,
    bloom: passCalls.bloom,
    output: passCalls.output,
    trisK: Math.round(gl.info.render.triangles / 1000),
    sceneTrisK: Math.round(passTris.scene / 1000),
    shadowTrisK: Math.round(passTris.shadow / 1000),
    objects: c.objects,
    autoMatrix: c.auto,
    meshes: c.meshes,
  };
}

function setPreset(p: EffectsLevel): void {
  preset = p;
  setGraphics({ effects: p });
}
setPreset((params.get('preset') as EffectsLevel) ?? 'high');

function setNight(on: boolean): void {
  night = on;
  (renderer as unknown as { setNight?: (on: boolean) => void }).setNight?.(on);
  (mapView as unknown as { setNight?: (on: boolean) => void }).setNight?.(on);
}
setNight(night);

/** Saves the canvas as .dev-snapshots/<name>.jpg (dev server). */
async function snap(name = 'ref', width = 1600): Promise<string> {
  renderer.render(0);
  const src = gl.domElement;
  const c = document.createElement('canvas');
  c.width = width;
  c.height = Math.round((width * src.height) / src.width);
  c.getContext('2d')!.drawImage(src, 0, 0, c.width, c.height);
  await fetch(`/__snap?name=${name}`, { method: 'POST', body: c.toDataURL('image/jpeg', 0.9) });
  return name;
}

window.addEventListener('keydown', (e) => {
  if (e.code === 'Digit1') setPreset('high');
  if (e.code === 'Digit2') setPreset('medium');
  if (e.code === 'Digit3') setPreset('low');
  if (e.code === 'KeyN') setNight(!night);
  if (e.code === 'KeyU') units.group.visible = !units.group.visible;
  if (e.code === 'KeyC') setView(viewName === 'capture' ? 'play' : 'capture');
  if (e.code === 'KeyB') bench();
  if (e.code === 'KeyP') void snap(`ref_${biome}_${night ? 'night' : 'day'}_${preset}`);
});

Object.assign(window, { __ref: { renderer, mapView, units, rig, state, bench, counts, snap, setPreset, setNight, setView, sceneCounts, show, peek, focus, crowd: CROWD } });
