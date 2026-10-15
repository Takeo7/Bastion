import * as THREE from 'three';
import type { TemplateId } from '@bastion/engine';
import { CROWD } from '../game/crowd';
import type { FigurePose, FigureRig } from '../game/figures';
import { IDLE_POSE } from '../game/models';
import { UNIT_LAYER } from '../game/post';
import { Renderer } from '../game/renderer';
import { AnimDriver } from '../game/syntyAnims';
import { SYNTY_LOD, SyntyFigure, syntyFigure } from '../game/syntyFigures';

// Horde lab (/hordas.html): what N Synty zombies cost a frame. Builds N
// figures with the game's code (syntyFigures, syntyAnims), half shambling and
// half waiting, in the game's renderer (light, shadows, post-processing), and
// times, per frame and in CPU milliseconds:
//  - clips: every unit's mixer advancing (AnimDriver.update);
//  - retarget: reading the clip pose onto the unit's own bones (AnimDriver.read);
//  - pose: the rest of applyPose (procedural layers, IK, writing the bones);
//  - scene: the matrix update of the whole scene (bones included);
//  - render: the renderer's CPU time, and the frame with the GPU (synced).
// `__horde.run([10, 20, 40, 60, 80])` measures each N; `#40` shows 40;
// `__horde.compare(80)` measures with the figures' level of detail on and off;
// `__horde.floor(40)` what baking the crowd into instanced meshes could save;
// `__horde.crowd(45)` measures with the game's crowd (crowd.ts) on and off.

const container = document.getElementById('horde')!;
const report = document.getElementById('horde-report')!;
const renderer = new Renderer(container);
const scene = renderer.scene;

const ground = new THREE.Mesh(new THREE.PlaneGeometry(80, 80).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x77705f, roughness: 0.95 }));
ground.position.set(20, 0, 20);
ground.receiveShadow = true;
scene.add(ground, CROWD.group);
renderer.fitMap(40, 40);

const TEMPLATES: TemplateId[] = ['zombie', 'zombie', 'zombie', 'xenoid', 'mec', 'bloater'];

// ------------------------------------------------------------------ timing

const spent = { clips: 0, retarget: 0 };
const update = AnimDriver.prototype.update;
const read = AnimDriver.prototype.read;
AnimDriver.prototype.update = function (this: AnimDriver, dt: number, calm: boolean) {
  const t = performance.now();
  update.call(this, dt, calm);
  spent.clips += performance.now() - t;
};
AnimDriver.prototype.read = function (this: AnimDriver, out: THREE.Quaternion[], hips: THREE.Vector3) {
  const t = performance.now();
  read.call(this, out, hips);
  spent.retarget += performance.now() - t;
};

// ------------------------------------------------------------------- horde

interface Member {
  rig: FigureRig;
  pose: FigurePose;
  walking: boolean;
}

let horde: Member[] = [];

function clear(): void {
  for (const m of horde) scene.remove(m.rig.root);
  horde = [];
  CROWD.clear();
}

/** N zombies in a loose crowd, every other one shambling forward. */
function build(n: number): void {
  clear();
  const cols = Math.ceil(Math.sqrt(n * 1.6));
  for (let i = 0; i < n; i++) {
    const rig = syntyFigure(TEMPLATES[i % TEMPLATES.length]!, '#ff4d4d', undefined, undefined, `z${i}`)!;
    rig.root.position.set(20 + (i % cols) * 1.3 - cols * 0.65 + (Math.random() - 0.5) * 0.4, 0, 18 + Math.floor(i / cols) * 1.3 + (Math.random() - 0.5) * 0.4);
    const pose: FigurePose = { ...IDLE_POSE, yaw: Math.random() * Math.PI * 2, phase: Math.random() * Math.PI * 2, spin: 0, search: 0 };
    scene.add(rig.root);
    horde.push({ rig, pose, walking: i % 2 === 0 });
  }
}

/** Resolves once every figure is dressed and has its clips. */
async function ready(): Promise<void> {
  for (let k = 0; k < 200; k++) {
    if (horde.every((m) => m.rig instanceof SyntyFigure && m.rig.ready)) return;
    await new Promise((r) => setTimeout(r, 50));
  }
}

let clock = 0;

/** One frame of the horde: poses (timed), scene and render. */
function frame(dt: number, sync: boolean): { pose: number; scene: number; render: number; total: number } {
  clock += dt;
  const t0 = performance.now();
  CROWD.tick(dt);
  for (const m of horde) {
    m.pose.run = m.walking ? 1 : 0;
    m.rig.applyPose(m.pose, clock);
  }
  const t1 = performance.now();
  scene.updateMatrixWorld();
  const t2 = performance.now();
  renderer.render(dt);
  const t3 = performance.now();
  if (sync) {
    const ctx = renderer.gl.getContext();
    ctx.readPixels(0, 0, 1, 1, ctx.RGBA, ctx.UNSIGNED_BYTE, pixel);
  }
  return { pose: t1 - t0, scene: t2 - t1, render: t3 - t2, total: performance.now() - t0 };
}

const pixel = new Uint8Array(4);

interface Result {
  n: number;
  clips: number;
  retarget: number;
  layers: number;
  scene: number;
  render: number;
  frame: number;
  calls: number;
}

async function measure(n: number, frames = 120, keep = false): Promise<Result> {
  if (!keep) build(n);
  await ready();
  renderer.camera.position.set(20, 18, 38);
  renderer.camera.lookAt(20, 0, 22);
  for (let i = 0; i < 30; i++) frame(1 / 60, false);
  spent.clips = spent.retarget = 0;
  let pose = 0;
  let sceneMs = 0;
  let render = 0;
  let total = 0;
  for (let i = 0; i < frames; i++) {
    const f = frame(1 / 60, true);
    pose += f.pose;
    sceneMs += f.scene;
    render += f.render;
    total += f.total;
  }
  renderer.gl.info.autoReset = false;
  renderer.gl.info.reset();
  renderer.render(0);
  const calls = renderer.gl.info.render.calls;
  renderer.gl.info.autoReset = true;
  const per = (x: number) => +(x / frames).toFixed(3);
  return {
    n,
    clips: per(spent.clips),
    retarget: per(spent.retarget),
    layers: per(pose - spent.clips - spent.retarget),
    scene: per(sceneMs),
    render: per(render),
    frame: per(total),
    calls,
  };
}

function table(results: Result[]): string {
  const head = '   N   clips  retarget  layers   poses   scene  render | frame(gpu) calls';
  const rows = results.map(
    (r) =>
      `${String(r.n).padStart(4)} ${r.clips.toFixed(2).padStart(7)} ${r.retarget.toFixed(2).padStart(9)} ${r.layers.toFixed(2).padStart(7)} ${(r.clips + r.retarget + r.layers).toFixed(2).padStart(7)} ${r.scene
        .toFixed(2)
        .padStart(7)} ${r.render.toFixed(2).padStart(7)} | ${r.frame.toFixed(2).padStart(10)} ${String(r.calls).padStart(5)}`,
  );
  return [`CPU ms per frame, ${renderer.gl.domElement.width}×${renderer.gl.domElement.height}`, head, ...rows].join('\n');
}

async function run(ns = [10, 20, 40, 60, 80]): Promise<Result[]> {
  const results: Result[] = [];
  for (const n of ns) {
    results.push(await measure(n));
    report.textContent = table(results);
  }
  return results;
}

/** Keeps a horde on screen, animated by the page's own loop. */
async function show(n: number): Promise<void> {
  build(n);
  await ready();
  renderer.camera.position.set(20, 14, 34);
  renderer.camera.lookAt(20, 0, 22);
  report.textContent = `${n} zombis`;
}

renderer.onFrame((dt) => {
  clock += dt;
  for (const m of horde) {
    m.pose.run = m.walking ? 1 : 0;
    m.rig.applyPose(m.pose, clock);
  }
});

/**
 * The floor GPU baking could reach: the same N bodies as one instanced mesh
 * per character (unskinned, standing as they rest), so nothing is posed or
 * skinned and each character is one draw. Measures scene and render as
 * `measure` does.
 */
async function floor(n: number, frames = 120): Promise<Result> {
  build(n);
  await ready();
  const groups = new Map<THREE.BufferGeometry, { material: THREE.Material; at: THREE.Matrix4[] }>();
  scene.updateMatrixWorld();
  for (const m of horde) {
    m.rig.root.traverse((o) => {
      if (!(o instanceof THREE.SkinnedMesh)) return;
      let g = groups.get(o.geometry);
      if (!g) groups.set(o.geometry, (g = { material: o.material as THREE.Material, at: [] }));
      g.at.push(o.matrixWorld.clone());
    });
    scene.remove(m.rig.root);
  }
  const baked: THREE.InstancedMesh[] = [];
  for (const [geometry, g] of groups) {
    const mesh = new THREE.InstancedMesh(geometry, new THREE.MeshStandardMaterial({ map: (g.material as THREE.MeshStandardMaterial).map, roughness: 0.82 }), g.at.length);
    g.at.forEach((m, i) => mesh.setMatrixAt(i, m));
    mesh.castShadow = true;
    mesh.layers.set(UNIT_LAYER);
    scene.add(mesh);
    baked.push(mesh);
  }
  renderer.camera.position.set(20, 18, 38);
  renderer.camera.lookAt(20, 0, 22);
  for (let i = 0; i < 30; i++) renderer.render(1 / 60);
  let [sceneMs, render, total] = [0, 0, 0];
  for (let i = 0; i < frames; i++) {
    const t0 = performance.now();
    scene.updateMatrixWorld();
    const t1 = performance.now();
    renderer.render(1 / 60);
    const t2 = performance.now();
    const ctx = renderer.gl.getContext();
    ctx.readPixels(0, 0, 1, 1, ctx.RGBA, ctx.UNSIGNED_BYTE, pixel);
    sceneMs += t1 - t0;
    render += t2 - t1;
    total += performance.now() - t0;
  }
  renderer.gl.info.autoReset = false;
  renderer.gl.info.reset();
  renderer.render(0);
  const calls = renderer.gl.info.render.calls;
  renderer.gl.info.autoReset = true;
  for (const b of baked) scene.remove(b);
  horde = [];
  const per = (x: number) => +(x / frames).toFixed(3);
  return { n, clips: 0, retarget: 0, layers: 0, scene: per(sceneMs), render: per(render), frame: per(total), calls };
}

/** Level of detail on and off, `rounds` times each, at N figures: the CPU per frame of both. */
async function compare(n = 80, rounds = 3): Promise<{ lod: Result[]; full: Result[] }> {
  const out = { lod: [] as Result[], full: [] as Result[] };
  for (let i = 0; i < rounds; i++) {
    for (const on of [true, false]) {
      SYNTY_LOD.on = on;
      (on ? out.lod : out.full).push(await measure(n));
    }
  }
  SYNTY_LOD.on = true;
  return out;
}

/** N zombies with the crowd on and off: its bodies baked first (frames run until every one of them is in it). */
async function crowd(n = 45): Promise<{ on: Result; off: Result }> {
  CROWD.enabled = true;
  build(n);
  await ready();
  for (let k = 0; k < 2000 && !horde.every((m) => (m.rig as unknown as { member: unknown }).member); k++) frame(1 / 60, false);
  const on = await measure(n, 120, true);
  CROWD.enabled = false;
  const off = await measure(n, 120, true);
  CROWD.enabled = true;
  return { on, off };
}

(window as unknown as { __horde: unknown }).__horde = { run, show, measure, compare, floor, crowd, renderer };
void show(Number(location.hash.slice(1)) || 40);
