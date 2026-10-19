import * as THREE from 'three';
import { loadModel } from '../../game/synty';
import { UI_MODELS, type BaseModelId, type PackId, type WeaponId } from '../../game/syntyManifest';
import { KitBatch, kitMesh, kitModel, loadBaseKit, loadKitModels } from '../../strategy/baseKit';
import { buildLata, lataModels } from '../../strategy/mula';

// Icon renders for the interface (/renders.html): things of the game as
// Synty models, in a 3/4 view, lit like the radio portraits, on a transparent
// background, rendered at 1024 and halved down to 512 and 128. Each also
// comes as a white silhouette (its alpha only), for CSS mask-image tinting.
// `__renders.save()` sends them all, as WebP, to the dev server's /__snap
// (.dev-snapshots/icon-<name>[-mask][@128].jpg, WebP inside): they go to
// public/ui/synty/renders/<name>[-mask][@128].webp.

const SIZE = 1024;
const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
renderer.setPixelRatio(1);
renderer.setSize(SIZE, SIZE, false);
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.1;
renderer.setClearColor(0x000000, 0);

/** Studio light: a warm key from the front left, a cool rim from behind, a soft sky. */
function studio(subject: THREE.Object3D): THREE.Scene {
  const scene = new THREE.Scene();
  scene.add(subject);
  scene.add(new THREE.HemisphereLight(0xd8e4ff, 0x4a3a30, 1.4));
  const key = new THREE.DirectionalLight(0xffe6c8, 2.6);
  key.position.set(-1, 1.4, 1.5);
  const rim = new THREE.DirectionalLight(0xa8c8ff, 1.6);
  rim.position.set(1.2, 0.8, -1.2);
  scene.add(key, rim);
  return scene;
}

/** Renders `subject` seen from `view` (a direction), framed tight with a small margin, as a PNG data URL. */
function shoot(subject: THREE.Object3D, view: THREE.Vector3, margin = 0.06): string {
  const scene = studio(subject);
  subject.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(subject);
  const fov = 18;
  const camera = new THREE.PerspectiveCamera(fov, 1, 0.01, 200);
  // Frame the box in the camera's own axes: its extent across and up, and its middle.
  const back = view.clone().normalize();
  const right = new THREE.Vector3(0, 1, 0).cross(back).normalize();
  const up = back.clone().cross(right);
  const corners = [0, 1, 2, 3, 4, 5, 6, 7].map((i) => new THREE.Vector3(i & 1 ? box.max.x : box.min.x, i & 2 ? box.max.y : box.min.y, i & 4 ? box.max.z : box.min.z));
  const centre = box.getCenter(new THREE.Vector3());
  let [x0, x1, y0, y1, depth] = [Infinity, -Infinity, Infinity, -Infinity, 0];
  for (const c of corners) {
    const d = c.clone().sub(centre);
    x0 = Math.min(x0, d.dot(right));
    x1 = Math.max(x1, d.dot(right));
    y0 = Math.min(y0, d.dot(up));
    y1 = Math.max(y1, d.dot(up));
    depth = Math.max(depth, Math.abs(d.dot(back)));
  }
  const target = centre.clone().addScaledVector(right, (x0 + x1) / 2).addScaledVector(up, (y0 + y1) / 2);
  const half = Math.max(x1 - x0, y1 - y0) / 2 / (1 - margin * 2);
  const distance = half / Math.tan(THREE.MathUtils.degToRad(fov / 2)) + depth;
  camera.position.copy(target).addScaledVector(back, distance);
  // Characters draw on their own layer (post.ts).
  camera.layers.enableAll();
  camera.up.copy(up);
  camera.lookAt(target);
  renderer.render(scene, camera);
  return renderer.domElement.toDataURL('image/png');
}

/** Resolves once every texture the subject wears has its image (or after ten seconds). */
async function painted(subject: THREE.Object3D): Promise<void> {
  for (let i = 0; i < 200; i++) {
    let ok = true;
    subject.traverse((o) => {
      const map = o instanceof THREE.Mesh ? (o.material as THREE.MeshStandardMaterial).map : null;
      if (map && map.image == null) ok = false;
    });
    if (ok) return;
    await new Promise((r) => setTimeout(r, 50));
  }
}

/**
 * La Mula, as the old hangar built it: POLYGON Town's bus with steel plates
 * over its windows, slits in the windscreen and plates on the roof.
 */
async function mulaIcon(): Promise<THREE.Object3D> {
  await loadBaseKit();
  const model = kitModel('mulaBus');
  if (!model) throw new Error('La Mula did not load');
  const body = new THREE.Group();
  const doorCentres: THREE.Vector3[] = [];
  for (const part of kitMesh('mulaBus', 'plain').children.slice()) {
    const mesh = part as THREE.Mesh;
    if (/door/i.test(mesh.name) && !/glass/i.test(mesh.name)) {
      mesh.geometry.computeBoundingBox();
      doorCentres.push(mesh.geometry.boundingBox!.getCenter(new THREE.Vector3()));
    }
    body.add(mesh);
  }
  const glass = model.parts.find((p) => /^SM_Veh_Bus_Glass/i.test(p.name));
  const band = new THREE.Box3();
  glass?.geometry.computeBoundingBox();
  if (glass?.geometry.boundingBox) band.copy(glass.geometry.boundingBox);
  else band.set(new THREE.Vector3(-1.1, 1.0, -3.6), new THREE.Vector3(1.1, 1.8, 3.4));
  const plates = new KitBatch();
  const windowY = (band.min.y + band.max.y) / 2 + 0.05;
  const plateH = (kitModel('plateSlit')?.size.y ?? 0.45) * 1.15;
  const sy = Math.max(1, (band.max.y - band.min.y) / plateH);
  const halfW = model.size.x / 2 + 0.02;
  const inDoorway = (z: number) => doorCentres.some((c) => Math.abs(c.z - z) < 0.75);
  for (let z = band.min.z + 0.35; z < band.max.z - 0.2; z += 0.68) {
    for (const side of [-1, 1]) {
      if (side < 0 && inDoorway(z)) continue;
      plates.put(Math.round(z * 3) % 3 ? 'plate' : 'plateSlit', { x: side * halfW, y: windowY - 0.25 * sy, z, s: 1.15, sy, finish: 'plain' });
    }
  }
  const nose = model.size.z / 2 + 0.03;
  for (const x of [-0.55, 0.55]) plates.put('plateSlit', { x, y: windowY - 0.35, z: nose - 0.15, yaw: Math.PI / 2, s: 1.3, finish: 'plain' });
  for (let z = -2.6; z <= 2.6; z += 1.3) plates.put('plateRoof', { x: (Math.round(z) % 2) * 0.2, y: model.size.y + 0.01, z, yaw: 0.05 * z, s: 1.1, finish: 'plain' });
  body.add(plates.build(false));
  const lights = new THREE.MeshStandardMaterial({ color: 0x2a2a20, emissive: 0xfff2c0, emissiveIntensity: 0.6 });
  for (const x of [-0.75, 0.75]) {
    const lamp = new THREE.Mesh(new THREE.BoxGeometry(0.28, 0.14, 0.05), lights);
    lamp.position.set(x, 0.62, nose + 0.01);
    body.add(lamp);
  }
  return body;
}

/** La Lata before it is the bus (playtest 18/10, #45): the 4×4, the van or the lorry with its armour, its lights on. */
const lataIcon = (vehicle: number) => async (): Promise<THREE.Object3D> => {
  await loadKitModels(lataModels(vehicle));
  const lata = buildLata(vehicle);
  if (!lata) throw new Error(`La Lata ${vehicle} did not load`);
  lata.lights.emissiveIntensity = 0.6;
  return lata.body;
};

/** One of the base's models as it stands in the Bastion (strategy/baseKit.ts), in its atlas colours. */
const kit = (id: BaseModelId) => async (): Promise<THREE.Object3D> => {
  await loadBaseKit();
  if (!kitModel(id)) throw new Error(`${id} did not load`);
  return kitMesh(id, 'plain');
};

/**
 * One of UI_MODELS, `size` across its longest side, standing on y = 0 at
 * (x, z), turned `yaw` about the vertical and tipped `tilt` about x.
 */
async function piece(id: keyof typeof UI_MODELS, size: number, x = 0, z = 0, yaw = 0, tilt = 0): Promise<THREE.Object3D> {
  const model = (await loadModel(UI_MODELS[id].pack, 'ui', id)).clone();
  const holder = new THREE.Group();
  model.rotation.set(tilt, yaw, 0);
  holder.add(model);
  holder.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(model);
  const dim = box.getSize(new THREE.Vector3());
  const k = size / Math.max(dim.x, dim.y, dim.z);
  model.scale.setScalar(k);
  holder.updateMatrixWorld(true);
  box.setFromObject(model);
  const centre = box.getCenter(new THREE.Vector3());
  model.position.set(x - centre.x, -box.min.y, z - centre.z);
  return holder;
}

/** A ration: a tall food tin and two flat ones stacked beside it. */
async function rationIcon(): Promise<THREE.Object3D> {
  const g = new THREE.Group();
  const tall = await piece('tinTall', 0.34, 0.1, -0.02);
  const flat = await piece('tinFlat', 0.3, -0.13, 0.08, 0.3);
  const flat2 = await piece('tinFlat2', 0.27, -0.12, 0.07, -0.25);
  // The second flat tin sits on the first.
  const under = new THREE.Box3().setFromObject(flat);
  flat2.position.y = under.max.y;
  g.add(tall, flat, flat2);
  return g;
}

/** «Piezas»: a scrap of sheet metal, two gears leaning on it, a bolt and a battery. */
async function partsIcon(): Promise<THREE.Object3D> {
  const g = new THREE.Group();
  // A heap on the sheet: the battery standing at the back, the gears tipped back against it, the bolt in front.
  g.add(await piece('scrapSheet', 0.55, 0, 0, 0.3));
  g.add(await piece('battery', 0.2, -0.14, -0.12, 0.4));
  g.add(await piece('gears', 0.34, 0.04, 0.02, -0.25, -0.9));
  g.add(await piece('boltIcon', 0.22, 0.17, 0.16, -0.6, Math.PI / 2));
  return g;
}

/** One of the units' weapons (syntyManifest.ts, WEAPONS), its barrel along +z, turned `tilt` about its side (up from the barrel). */
const weapon = (id: WeaponId, tilt = 0) => async (): Promise<THREE.Object3D> => {
  const g = new THREE.Group();
  const m = (await loadModel('apocalypse', 'wep', id)).clone();
  m.rotation.x = -tilt;
  g.add(m);
  return g;
};

/** One of the battle maps' props (syntyManifest.ts, PROPS). */
const prop = (pack: PackId, id: string) => async (): Promise<THREE.Object3D> => (await loadModel(pack, 'prop', id)).clone();

/**
 * What to render: the subject and the direction it is seen from (its nose or
 * face towards +z); `masks`: only its white silhouettes are wanted; `trim`: as
 * the packs' weapon icons (public/ui/synty/icons), cut to the silhouette and
 * 128 wide, as tall as it comes; `light`: brighter than the rest by so much.
 */
const ICONS: Record<string, { make: () => Promise<THREE.Object3D>; view: THREE.Vector3; masks?: boolean; trim?: boolean; light?: number }> = {
  // More nose than side: chunkier, so it still reads at 128 px.
  mula: { make: mulaIcon, view: new THREE.Vector3(0.95, 0.6, 1.45) },
  // A pure profile, both wheels showing, like the pack's weapon icons: what reads at 26 px.
  'mula-side': { make: mulaIcon, view: new THREE.Vector3(1, 0, 0) },
  // Map symbols, white masks only: shapes that read at 24–32 px.
  // Camposanto: the wooden grave cross, arms to the camera.
  camposanto: { make: kit('cross'), view: new THREE.Vector3(1, 0.08, 0.15), masks: true },
  // Roca: an Alpine crag, peaked, its foot flat on the ground.
  roca: { make: kit('crag2'), view: new THREE.Vector3(0.5, 0.12, 1), masks: true },
  // Ruinas: the collapsed brick wall, its broken edge stepping down.
  ruinas: { make: kit('brokenWall'), view: new THREE.Vector3(1, 0.15, 0.3), masks: true },
  // La Mula nose on, for the Mixta panels.
  'mula-front': { make: mulaIcon, view: new THREE.Vector3(0.05, 0.12, 1) },
  // La Lata before it is the bus, the same two ways (the garage's header goes by the campaign's vehicle).
  // The van's and the lorry's noses are narrow and dark at 88 px: a little of their side shows, and more light.
  todoterreno: { make: lataIcon(0), view: new THREE.Vector3(0.95, 0.6, 1.45) },
  'todoterreno-front': { make: lataIcon(0), view: new THREE.Vector3(0.05, 0.12, 1) },
  furgoneta: { make: lataIcon(1), view: new THREE.Vector3(0.95, 0.6, 1.45) },
  'furgoneta-front': { make: lataIcon(1), view: new THREE.Vector3(0.32, 0.14, 1), light: 1.35 },
  camion: { make: lataIcon(2), view: new THREE.Vector3(0.95, 0.6, 1.45) },
  'camion-front': { make: lataIcon(2), view: new THREE.Vector3(0.32, 0.14, 1), light: 1.35 },
  // Their pure profiles cut to the silhouette, as La Mula's (r-mula), for La Lata on El Valle's map.
  'todoterreno-side': { make: lataIcon(0), view: new THREE.Vector3(1, 0, 0), masks: true, trim: true },
  'furgoneta-side': { make: lataIcon(1), view: new THREE.Vector3(1, 0, 0), masks: true, trim: true },
  'camion-side': { make: lataIcon(2), view: new THREE.Vector3(1, 0, 0), masks: true, trim: true },
  // Items.
  botiquin: { make: kit('medKit'), view: new THREE.Vector3(0.55, 0.85, 1) },
  racion: { make: rationIcon, view: new THREE.Vector3(0.6, 0.5, 1) },
  piezas: { make: partsIcon, view: new THREE.Vector3(0.6, 0.55, 1) },
  // Weapons for the Armería, beside the packs' own (rifle, pistol, mh-machete): a pure profile, the muzzle to the
  // right; a blade slanting up to the right, as the machete.
  escopeta: { make: weapon('shotgun'), view: new THREE.Vector3(-1, 0, 0), masks: true, trim: true },
  cuchillo: { make: weapon('knife', Math.PI / 4), view: new THREE.Vector3(-1, 0, 0), masks: true, trim: true },
};

const shots = new Map<string, string>();
const shelf = document.getElementById('renders')!;

// `?only=botiquin,racion` renders just those.
const only = new URLSearchParams(location.search).get('only')?.split(',');
for (const [name, icon] of Object.entries(ICONS).filter(([n]) => !only || only.includes(n))) {
  void icon.make().then(async (subject) => {
    await painted(subject);
    renderer.toneMappingExposure = 1.1 * (icon.light ?? 1);
    const url = shoot(subject, icon.view);
    renderer.toneMappingExposure = 1.1;
    shots.set(name, url);
    const cell = document.createElement('figure');
    cell.style.margin = '0';
    const img = new Image();
    img.src = url;
    img.style.cssText = 'width: 256px; height: 256px; display: block';
    const caption = document.createElement('figcaption');
    caption.textContent = name;
    cell.append(img, caption);
    shelf.append(cell);
  });
}

/** An image halved step by step down to `size` (smoother than one big step); `mask` paints it all white. */
function scaled(img: HTMLImageElement, size: number, mask: boolean): HTMLCanvasElement {
  let source: CanvasImageSource = img;
  let from = img.naturalWidth;
  let canvas = document.createElement('canvas');
  do {
    const to = Math.max(size, Math.round(from / 2));
    canvas = document.createElement('canvas');
    canvas.width = canvas.height = to;
    const g = canvas.getContext('2d')!;
    g.imageSmoothingQuality = 'high';
    g.drawImage(source, 0, 0, to, to);
    source = canvas;
    from = to;
  } while (from > size);
  if (mask) {
    const g = canvas.getContext('2d')!;
    g.globalCompositeOperation = 'source-in';
    g.fillStyle = '#fff';
    g.fillRect(0, 0, size, size);
  }
  return canvas;
}

/** A mask cut to its silhouette's box (and a pixel round it), `width` wide. */
function trimmed(mask: HTMLCanvasElement, width: number): HTMLCanvasElement {
  const { width: w, height: h } = mask;
  const data = mask.getContext('2d')!.getImageData(0, 0, w, h).data;
  let [x0, y0, x1, y1] = [w, h, -1, -1];
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (data[(y * w + x) * 4 + 3]! < 8) continue;
      x0 = Math.min(x0, x);
      x1 = Math.max(x1, x);
      y0 = Math.min(y0, y);
      y1 = Math.max(y1, y);
    }
  }
  const pad = 2;
  const sw = x1 - x0 + 1 + pad * 2;
  const sh = y1 - y0 + 1 + pad * 2;
  const out = document.createElement('canvas');
  out.width = width;
  out.height = Math.max(1, Math.round((sh * width) / sw));
  const g = out.getContext('2d')!;
  g.imageSmoothingQuality = 'high';
  g.drawImage(mask, x0 - pad, y0 - pad, sw, sh, 0, 0, out.width, out.height);
  return out;
}

/** Sends every render, at 512 and 128 and as masks, to the dev server's /__snap as WebP. */
async function save(): Promise<string[]> {
  const sent: string[] = [];
  for (const [name, url] of shots) {
    const img = new Image();
    // (onload, not decode(): a hidden page may put decoding off indefinitely.)
    await new Promise((resolve) => {
      img.onload = resolve;
      img.src = url;
    });
    if (ICONS[name]!.trim) {
      const file = `icon-${name}-mask-trim`;
      await fetch(`/__snap?name=${encodeURIComponent(file)}`, { method: 'POST', body: trimmed(scaled(img, 512, true), 128).toDataURL('image/webp', 0.92) });
      sent.push(file);
      continue;
    }
    for (const [size, suffix] of [[512, ''], [128, '@128']] as const) {
      for (const mask of ICONS[name]!.masks ? [true] : [false, true]) {
        const file = `icon-${name}${mask ? '-mask' : ''}${suffix}`;
        await fetch(`/__snap?name=${encodeURIComponent(file)}`, { method: 'POST', body: scaled(img, size, mask).toDataURL('image/webp', 0.92) });
        sent.push(file);
      }
    }
  }
  return sent;
}

(window as unknown as { __renders: unknown }).__renders = { save, shots };
