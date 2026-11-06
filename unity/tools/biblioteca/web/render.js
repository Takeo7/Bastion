// Synty's FBX painted as Unity paints them, near enough (docs/BIBLIOTECA.md §4): each material slot with its texture from
// the pack's MaterialList (the server's `slots`), else the pack's atlas; glass see-through. Not the game's light nor
// URP's shaders: the game's look is in Arte's captures. Thumbnails one at a time, kept by the server.
import * as THREE from 'three';
import { FBXLoader } from 'three/addons/loaders/FBXLoader.js';
import { TGALoader } from 'three/addons/loaders/TGALoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';
import { OWN } from './util.js';

const BLANK = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=';
const fileUrl = (pack, path) => `/api/file?id=${encodeURIComponent(pack)}&path=${encodeURIComponent(path)}`;

// (The FBX name textures by their author's paths: never fetched, the slots get ours.)
const manager = new THREE.LoadingManager();
manager.setURLModifier((url) => (/\.(png|jpe?g|tga|psd|tif+|bmp)(\?|$)/i.test(url) ? BLANK : url));
const fbxLoader = new FBXLoader(manager);
const textureLoader = new THREE.TextureLoader();
const tgaLoader = new TGALoader();

/** The textures of the pack open, and the FBX that hold all its characters: dropped with it. */
let textures = new Map();
let shared = new Map();
export function forgetTextures() {
  for (const t of textures.values()) t.then((x) => x?.dispose()).catch(() => {});
  textures = new Map();
  shared = new Map();
}

/** An FBX loaded; one that holds many characters (the dogs' 14 MB), loaded once and copied for each. */
async function fbxFor(url, many) {
  if (!many) return fbxLoader.loadAsync(url);
  if (!shared.has(url)) shared.set(url, fbxLoader.loadAsync(url));
  return SkeletonUtils.clone(await shared.get(url));
}
function texture(pack, path) {
  if (!path) return Promise.resolve(null);
  const key = `${pack}|${path}`;
  if (!textures.has(key)) {
    const loader = /\.tga$/i.test(path) ? tgaLoader : textureLoader;
    textures.set(key, loader.loadAsync(fileUrl(pack, path)).then((t) => {
      t.colorSpace = THREE.SRGBColorSpace;
      t.anisotropy = 4;
      return t;
    }).catch(() => null));
  }
  return textures.get(key);
}

/** A slot's or a texture's name as the server's sources.mjs compares them (textureKey). */
const textureKey = (name) => name.toLowerCase().replace(/\.[a-z]+$/, '').replace(/textures?|tex|materials?|mat/g, '').replace(/[^a-z0-9]/g, '');

/**
 * A model, loaded and painted. `look` (the server's /api/model): `meshes`, each mesh's slots' textures in order;
 * `byKey`, a slot's texture by textureKey of its name; `atlas`, the pack's.
 */
export async function loadModel(pack, path, look) {
  const object = await fbxFor(fileUrl(pack, path), !!look.only);
  // (A character drawn from the FBX that holds them all: only its own meshes stay.)
  if (look.only) {
    const keep = new Set(look.only.map((n) => n.toLowerCase()));
    const drop = [];
    object.traverse((o) => { if (o.isMesh && !keep.has(o.name.toLowerCase())) drop.push(o); });
    for (const o of drop) o.parent.remove(o);
  }
  const atlas = await texture(pack, look.atlas);
  const painted = [];
  object.traverse((o) => {
    if (!o.isMesh) return;
    const list = Array.isArray(o.material) ? o.material : [o.material];
    const listed = look.meshes?.[o.name.toLowerCase().replace(/^sk_/, 'sm_')];
    const mats = list.map((m, i) => {
      const name = (m?.name ?? '').toLowerCase();
      const mat = new THREE.MeshStandardMaterial({ roughness: 0.85, metalness: 0, color: 0xffffff });
      mat.name = m?.name ?? '';
      if (/glass/.test(name)) Object.assign(mat, { transparent: true, opacity: 0.35, color: new THREE.Color(0xa9c4d6), roughness: 0.2 });
      else {
        const own = listed?.[i] ?? look.byKey?.[textureKey(name)];
        painted.push((own ? texture(pack, own) : Promise.resolve(null)).then((t) => {
          mat.map = t ?? atlas;
          if (!mat.map) mat.color = new THREE.Color(0xb8b8b8);
          mat.needsUpdate = true;
        }));
      }
      m?.dispose?.();
      return mat;
    });
    o.material = Array.isArray(o.material) ? mats : mats[0];
    o.castShadow = o.receiveShadow = false;
  });
  await Promise.all(painted);
  return object;
}

/** A model's size as its FBX has it (x, y up, z), for comparing shapes; kept by model. */
const sizes = new Map();
export function measure(pack, path) {
  const key = `${pack}|${path}`;
  if (!sizes.has(key)) {
    sizes.set(key, fbxLoader.loadAsync(fileUrl(pack, path)).then((object) => {
      const size = new THREE.Box3().setFromObject(object).getSize(new THREE.Vector3());
      dispose(object);
      return { x: size.x, y: size.y, z: size.z };
    }).catch(() => null));
  }
  return sizes.get(key);
}

export function dispose(object) {
  object?.traverse((o) => {
    if (!o.isMesh) return;
    o.geometry?.dispose();
    for (const m of Array.isArray(o.material) ? o.material : [o.material]) m?.dispose();
  });
}

function lights(scene) {
  scene.add(new THREE.HemisphereLight(0xe6eeff, 0x3d342b, 1.7));
  const sun = new THREE.DirectionalLight(0xffffff, 2.1);
  sun.position.set(3, 5, 4);
  scene.add(sun);
  const back = new THREE.DirectionalLight(0x9fb4d8, 0.6);
  back.position.set(-4, 2, -3);
  scene.add(back);
}

/** Puts `camera` to see all of `object` from three quarters in front, a little above. */
function frame(camera, object, controls) {
  const box = new THREE.Box3().setFromObject(object);
  if (box.isEmpty()) return;
  const sphere = box.getBoundingSphere(new THREE.Sphere());
  const r = Math.max(sphere.radius, 1e-4);
  const dist = r / Math.sin(THREE.MathUtils.degToRad(camera.fov / 2)) * 1.05;
  const dir = new THREE.Vector3(0.75, 0.55, 1).normalize();
  camera.position.copy(sphere.center).addScaledVector(dir, dist);
  camera.near = dist / 100;
  camera.far = dist * 10;
  camera.lookAt(sphere.center);
  camera.updateProjectionMatrix();
  if (controls) { controls.target.copy(sphere.center); controls.update(); }
}

/** The thumbnails: one renderer, one model at a time. */
const THUMB = 256;
let thumbRenderer = null;
const thumbScene = new THREE.Scene();
lights(thumbScene);
const thumbCamera = new THREE.PerspectiveCamera(30, 1, 0.01, 1000);
let queue = [], busy = false;

export function clearThumbs() { queue = []; }

/** Asks for a thumbnail; `done(url)` with an object URL, or `done(null)` if the model would not open. */
export function thumb(pack, path, look, done, key = path, name = null) {
  queue.push({ pack, path, look, done, key, name });
  if (!busy) drain();
}

async function drain() {
  busy = true;
  while (queue.length) {
    const job = queue.shift();
    if (!job.done.wanted?.()) { job.done(undefined); continue; }
    try {
      if (!thumbRenderer) {
        thumbRenderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
        thumbRenderer.setSize(THUMB, THUMB, false);
        thumbRenderer.setClearColor(0x000000, 0);
        thumbRenderer.outputColorSpace = THREE.SRGBColorSpace;
      }
      const info = await fetch(`/api/model?id=${encodeURIComponent(job.pack)}&path=${encodeURIComponent(job.path)}${job.name ? `&name=${encodeURIComponent(job.name)}` : ''}`).then((r) => r.json());
      const object = await loadModel(job.pack, job.path, info.look ?? job.look);
      thumbScene.add(object);
      frame(thumbCamera, object);
      thumbRenderer.render(thumbScene, thumbCamera);
      thumbScene.remove(object);
      dispose(object);
      const blob = await new Promise((ok) => thumbRenderer.domElement.toBlob(ok, 'image/png'));
      fetch(`/api/thumb?id=${encodeURIComponent(job.pack)}&path=${encodeURIComponent(job.key)}`, { method: 'POST', headers: OWN, body: blob }).catch(() => {});
      job.done(URL.createObjectURL(blob));
    } catch (e) {
      console.warn('Sin miniatura', job.path, e);
      job.done(null);
    }
  }
  busy = false;
}

/** The 3D view of the card: turns with the mouse. */
export class Viewer {
  constructor(canvas) {
    this.canvas = canvas;
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    this.scene = new THREE.Scene();
    lights(this.scene);
    this.camera = new THREE.PerspectiveCamera(30, 1, 0.01, 1000);
    this.controls = new OrbitControls(this.camera, canvas);
    this.controls.enableDamping = true;
    this.object = null;
    this.token = 0;
    const loop = () => {
      if (!canvas.isConnected) return;
      this.resize();
      this.controls.update();
      this.renderer.render(this.scene, this.camera);
      requestAnimationFrame(loop);
    };
    requestAnimationFrame(loop);
  }

  resize() {
    const w = this.canvas.clientWidth, h = this.canvas.clientHeight;
    if (!w || !h) return;
    if (this.canvas.width !== Math.round(w * this.renderer.getPixelRatio())) {
      this.renderer.setSize(w, h, false);
      this.camera.aspect = w / h;
      this.camera.updateProjectionMatrix();
    }
  }

  /** Shows a model; the last one asked for wins. Resolves false if it would not open. */
  async show(pack, path, look) {
    const token = ++this.token;
    this.clear();
    try {
      const object = await loadModel(pack, path, look);
      if (token !== this.token) { dispose(object); return true; }
      this.object = object;
      this.scene.add(object);
      this.resize();
      frame(this.camera, object, this.controls);
      return true;
    } catch (e) {
      console.warn('No se abre', path, e);
      return false;
    }
  }

  clear() {
    if (!this.object) return;
    this.scene.remove(this.object);
    dispose(this.object);
    this.object = null;
  }

  destroy() {
    this.token++;
    this.clear();
    this.controls.dispose();
    this.renderer.dispose();
  }
}

// ---- Clips (phase 3): a clip on its pack's mannequin, or on its own bones made into a stick figure. ----

const FIGURE = new THREE.MeshStandardMaterial({ color: 0x9aa7b8, roughness: 0.7, metalness: 0 });

/** A stick figure on a skeleton: a stick from each bone to each of its children, and a ball for the head. */
function boneFigure(root) {
  root.updateMatrixWorld(true);
  const bones = [];
  root.traverse((o) => { if (o.isBone) bones.push(o); });
  const box = new THREE.Box3();
  for (const b of bones) box.expandByPoint(b.getWorldPosition(new THREE.Vector3()));
  const tall = box.getSize(new THREE.Vector3()).y || 1;
  const up = new THREE.Vector3(0, 1, 0);
  for (const b of bones) {
    const scale = b.getWorldScale(new THREE.Vector3()).x || 1;
    const r = (tall * 0.022) / scale;
    for (const c of b.children) {
      if (!c.isBone) continue;
      const len = c.position.length();
      if (len < 1e-6) continue;
      const geo = new THREE.CylinderGeometry(r, r, len, 6);
      geo.translate(0, len / 2, 0);
      const stick = new THREE.Mesh(geo, FIGURE);
      stick.quaternion.setFromUnitVectors(up, c.position.clone().normalize());
      b.add(stick);
    }
    if (/head$/i.test(b.name) && !b.children.some((c) => c.isBone && /head/i.test(c.name))) {
      const ball = new THREE.Mesh(new THREE.SphereGeometry((tall * 0.065) / scale, 12, 8), FIGURE);
      ball.position.y = (tall * 0.05) / scale;
      b.add(ball);
    }
  }
}

/** The clip in place: its root bone keeps its first spot across, so a walk or a run does not leave the view. */
function inPlace(clip) {
  const root = clip.tracks.find((t) => t.name.endsWith('.position') && /(hips|pelvis|root)$/i.test(t.name.split('.')[0]));
  if (!root) return;
  const v = root.values;
  for (let i = 0; i < v.length; i += 3) { v[i] = v[0]; v[i + 2] = v[2]; }
}

/** How many of the clip's bones the model has, by name. */
function fits(clip, object) {
  const names = new Set();
  object.traverse((o) => names.add(o.name));
  const bones = [...new Set(clip.tracks.map((t) => t.name.split('.')[0]))];
  return bones.length ? bones.filter((b) => names.has(b)).length / bones.length : 0;
}

/** The card's moving figure for a clip. */
export class ClipViewer extends Viewer {
  constructor(canvas) {
    super(canvas);
    this.clock = new THREE.Clock();
    this.mixer = null;
    this.action = null;
    const tick = () => {
      if (!canvas.isConnected) return;
      this.mixer?.update(this.clock.getDelta());
      requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  }

  /**
   * `clipUrl`: the clip's FBX. `mannequin`: { pack, path } of the character it was made for, or null. Resolves
   * { ok, on: 'maniquí' | 'huesos', seconds } (ok false if the FBX has no clip).
   */
  async play(clipUrl, mannequin) {
    const token = ++this.token;
    this.clear();
    const source = await fbxLoader.loadAsync(clipUrl);
    const clip = source.animations[0];
    if (!clip) return { ok: false };
    inPlace(clip);
    let target = null, on = 'huesos';
    if (mannequin) {
      const body = await fbxFor(fileUrl(mannequin.pack, mannequin.path), !!mannequin.only).catch(() => null);
      if (body && mannequin.only) {
        const keep = new Set(mannequin.only.map((n) => n.toLowerCase()));
        const drop = [];
        body.traverse((o) => { if (o.isMesh && !keep.has(o.name.toLowerCase())) drop.push(o); });
        for (const o of drop) o.parent.remove(o);
      }
      if (body && fits(clip, body) >= 0.6) {
        body.traverse((o) => { if (o.isMesh) o.material = FIGURE; });
        target = body;
        on = 'maniquí';
        dispose(source);
      } else if (body) dispose(body);
    }
    if (!target) {
      boneFigure(source);
      target = source;
    }
    if (token !== this.token) { dispose(target); return { ok: true, on, seconds: clip.duration }; }
    this.object = target;
    this.scene.add(target);
    this.mixer = new THREE.AnimationMixer(target);
    this.action = this.mixer.clipAction(clip);
    this.action.play();
    this.mixer.update(0);
    this.resize();
    target.updateMatrixWorld(true);
    // (Framed on the figure's bones as it starts: a skinned mesh's box is its bind pose.)
    const box = new THREE.Box3();
    target.traverse((o) => { if (o.isBone) box.expandByPoint(o.getWorldPosition(new THREE.Vector3())); });
    if (box.isEmpty()) box.setFromObject(target);
    const sphere = box.getBoundingSphere(new THREE.Sphere());
    const dist = (sphere.radius * 1.5) / Math.sin(THREE.MathUtils.degToRad(this.camera.fov / 2));
    this.camera.position.copy(sphere.center).add(new THREE.Vector3(0.6, 0.35, 1).normalize().multiplyScalar(dist));
    this.camera.near = dist / 100;
    this.camera.far = dist * 20;
    this.camera.updateProjectionMatrix();
    this.controls.target.copy(sphere.center);
    this.controls.update();
    return { ok: true, on, seconds: clip.duration };
  }

  get paused() { return !!this.action?.paused; }
  toggle() { if (this.action) this.action.paused = !this.action.paused; }

  clear() {
    this.mixer?.stopAllAction();
    this.mixer = null;
    this.action = null;
    super.clear();
  }
}
