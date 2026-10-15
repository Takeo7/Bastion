import * as THREE from 'three';
import { levelCount, type GameState } from '@bastion/engine';
import { piecesLoaded, type Focus } from '../../game/buildings';
import { setTerrain } from '../../game/coords';
import { MapView } from '../../game/mapView';
import { Renderer } from '../../game/renderer';
import { syntyPropsLoaded } from '../../game/syntyProps';
import { UnitViews } from '../../game/unitViews';

// What Arte's shots of the trailer share (docs/TRAILER.md): a cinema camera
// moved through keys, the game's own map drawn without its rules (the empty
// street, the horde), and the street a map is best seen down. The shots
// themselves are shots/<id>.ts.

export type V3 = readonly [number, number, number];

/** Where the camera stands at `t` seconds into the shot, what it looks at, and its lens (vertical field of view, degrees). */
export interface Key {
  t: number;
  pos: V3;
  at: V3;
  fov?: number;
}

const catmull = (p0: number, p1: number, p2: number, p3: number, u: number) =>
  0.5 * (2 * p1 + (p2 - p0) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (3 * p1 - p0 - 3 * p2 + p3) * u * u * u);

/**
 * A camera through `keys` (in time order): its position and aim each on a
 * Catmull-Rom curve through theirs, so it never stops at a key; `ease` slows
 * it in at the first and out at the last. Returns what places a camera at `t`.
 */
export function cameraPath(keys: Key[], ease: { in?: boolean; out?: boolean } = {}): (t: number, camera: THREE.PerspectiveCamera) => void {
  const first = keys[0]!.t;
  const last = keys[keys.length - 1]!.t;
  const span = last - first;
  // An eased clock over the whole path: from rest at the start, to rest at the end, as asked (the speed between stays continuous).
  const clock = (t: number) => {
    const u = THREE.MathUtils.clamp((t - first) / (span || 1), 0, 1);
    const v = ease.in && ease.out ? u * u * (3 - 2 * u) : ease.in ? u * u * (2 - u) : ease.out ? 1 - (1 - u) * (1 - u) * (1 + u) : u;
    return first + v * span;
  };
  const p = new THREE.Vector3();
  const q = new THREE.Vector3();
  return (t, camera) => {
    const time = clock(t);
    let i = 0;
    while (i < keys.length - 2 && keys[i + 1]!.t <= time) i++;
    const k0 = keys[Math.max(0, i - 1)]!;
    const k1 = keys[i]!;
    const k2 = keys[Math.min(keys.length - 1, i + 1)]!;
    const k3 = keys[Math.min(keys.length - 1, i + 2)]!;
    const u = k2.t > k1.t ? THREE.MathUtils.clamp((time - k1.t) / (k2.t - k1.t), 0, 1) : 0;
    const at = (f: (k: Key) => V3, axis: number) => catmull(f(k0)[axis]!, f(k1)[axis]!, f(k2)[axis]!, f(k3)[axis]!, u);
    p.set(at((k) => k.pos, 0), at((k) => k.pos, 1), at((k) => k.pos, 2));
    q.set(at((k) => k.at, 0), at((k) => k.at, 1), at((k) => k.at, 2));
    const fov = THREE.MathUtils.lerp(k1.fov ?? 40, k2.fov ?? k1.fov ?? 40, u * u * (3 - 2 * u));
    camera.position.copy(p);
    camera.lookAt(q);
    if (camera.fov !== fov) {
      camera.fov = fov;
      camera.updateProjectionMatrix();
    }
  };
}

/** The game's battlefield without its rules: the map as a mission draws it, its units standing, everything in sight. */
export interface MapScene {
  renderer: Renderer;
  mapView: MapView;
  units: UnitViews;
  state: GameState;
}

export async function mapScene(stage: HTMLElement, state: GameState, time: 'day' | 'night' | 'dawn' = 'day'): Promise<MapScene> {
  const renderer = new Renderer(stage);
  renderer.closeUp = true;
  setTerrain(state);
  const mapView = new MapView();
  const units = new UnitViews();
  renderer.scene.add(mapView.group, units.group);
  renderer.fitMap(state.width, state.height);
  renderer.setTime(time);
  const focus: Focus = { selected: null, hover: null, target: null, level: null };
  mapView.watch(renderer.camera, () => focus);
  mapView.setNight(time === 'night');
  mapView.build(state);
  units.build(state);
  mapView.setVisibility(new Array<boolean>(state.width * state.height * levelCount(state)).fill(true));
  renderer.onFrame((dt, t) => {
    mapView.tick(dt);
    units.tick(dt, t);
  });
  // The models in (a map built before them is built again when they arrive), then a few frames to settle.
  await Promise.all([syntyPropsLoaded(), piecesLoaded()]);
  await frames(10);
  return { renderer, mapView, units, state };
}

/** A fixed scatter (the same backdrop every take). */
const scatter = (n: number) => {
  const x = Math.sin(n * 78.233 + 12.9898) * 43758.5453;
  return x - Math.floor(x);
};

/**
 * What closes the horizon round a map (beyond it the game has only its ground,
 * which the haze turns into a flat grey band): the dead city's skyline, blocks
 * of one storey to five, some with a hut on the roof: low ones on the bare
 * ground round the map, higher ones far off, flat dark silhouettes the
 * scene's haze takes with distance; and
 * `smoke` columns of fires far off, in faceted puffs as the game's smoke.
 */
export function backdrop(scene: THREE.Scene, s: GameState, smoke: [number, number][] = [], smokeInHaze = false): THREE.Group {
  const group = new THREE.Group();
  const box = new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0);
  const cx = s.width / 2;
  const cz = s.height / 2;
  const half = Math.max(s.width, s.height) / 2;
  // Dark slate, a little darker or lighter block by block; the scene's haze takes them with distance as it takes the map.
  const fogColor = (scene.fog as THREE.FogExp2 | null)?.color ?? new THREE.Color(0xb88a80);
  const near = new THREE.Color(0x232733);
  const tint = (r: number) => near.clone().multiplyScalar(0.85 + scatter(r * 3.1) * 0.3);
  const blocks: { m: THREE.Matrix4; c: THREE.Color }[] = [];
  const m = new THREE.Matrix4();
  const ring = (from: number, to: number, count: number, low: number, high: number, seed: number) => {
    for (let k = 0, placed = 0; k < count * 6 && placed < count; k++) {
      const n = k * 13 + seed;
      const a = scatter(n + 1) * Math.PI * 2;
      const r = half + from + Math.pow(scatter(n + 2), 0.8) * (to - from);
      const x = cx + Math.cos(a) * r;
      const z = cz + Math.sin(a) * r;
      // Clear of the map and of the ground just round it.
      if (Math.abs(x - cx) < half + from - 4 && Math.abs(z - cz) < half + from - 4) continue;
      const w = 4 + scatter(n + 3) * 8;
      const d = 4 + scatter(n + 4) * 8;
      const h = low + Math.pow(scatter(n + 5), 2) * (high - low);
      const yaw = Math.round(scatter(n + 6) * 4) * (Math.PI / 2) + (scatter(n + 9) - 0.5) * 0.2;
      const q = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), yaw);
      const c = tint(r);
      blocks.push({ m: m.clone().compose(new THREE.Vector3(x, 0, z), q, new THREE.Vector3(w, h, d)), c });
      if (scatter(n + 7) < 0.35) blocks.push({ m: m.clone().compose(new THREE.Vector3(x, h, z), q, new THREE.Vector3(w * 0.3, 1.2, d * 0.3)), c });
      placed++;
    }
  };
  // Low near the map (on the band of bare ground round it), higher far off.
  ring(9, 45, 240, 2, 7, 1);
  ring(60, 130, 260, 3, 13, 7);
  const skyline = new THREE.InstancedMesh(box, new THREE.MeshBasicMaterial(), blocks.length);
  blocks.forEach((b, i) => {
    skyline.setMatrixAt(i, b.m);
    skyline.setColorAt(i, b.c);
  });
  skyline.frustumCulled = false;
  group.add(skyline);
  // Smoke: a leaning column of grey puffs, bigger and fainter as it rises, flat against the sky like the skyline.
  const puff = new THREE.IcosahedronGeometry(1, 0);
  smoke.forEach(([x, z], c) => {
    const puffs: THREE.Matrix4[] = [];
    for (let i = 0; i < 26; i++) {
      const t = i / 25;
      const size = 2.5 + t * 7 + scatter(c * 50 + i) * 1.5;
      const p = new THREE.Vector3(x + t * 16 + (scatter(c * 50 + i + 7) - 0.5) * 3, 3 + t * 28, z + t * 4 + (scatter(c * 50 + i + 13) - 0.5) * 3);
      const q = new THREE.Quaternion().setFromEuler(new THREE.Euler(scatter(i + 1) * 3, scatter(i + 2) * 3, 0));
      puffs.push(m.clone().compose(p, q, new THREE.Vector3(size, size * 0.8, size)));
    }
    const r = Math.hypot(x - cx, z - cz);
    // In a thin haze (dusk) the smoke keeps its own dark, or the haze would pale it into a cloud; in a thick one (dawn) it goes into it.
    const column = new THREE.InstancedMesh(puff, new THREE.MeshBasicMaterial({ fog: smokeInHaze }), puffs.length);
    // Dark against the sky (the grade and the exposure lift it): a fire's smoke, not a cloud.
    const base = new THREE.Color(0x141317).lerp(fogColor, THREE.MathUtils.clamp((r - half) / 1000, 0, 0.08));
    puffs.forEach((b, i) => {
      column.setMatrixAt(i, b);
      column.setColorAt(i, base.clone().lerp(fogColor, (i / puffs.length) * 0.12 + scatter(c * 70 + i) * 0.04));
    });
    column.frustumCulled = false;
    group.add(column);
  });
  scene.add(group);
  return group;
}

/** Waits `n` animation frames (the trailer's clock runs them in real time during a shot's setup). */
export function frames(n: number): Promise<void> {
  return new Promise((done) => {
    const step = () => (n-- <= 0 ? done() : requestAnimationFrame(step));
    requestAnimationFrame(step);
  });
}

/** A straight run of street: its first and last tile and which way it runs. */
export interface Street {
  from: { x: number; y: number };
  to: { x: number; y: number };
  /** Tiles across it that are street too, either side of the line from `from` to `to`. */
  width: number;
}

/**
 * The longest straight street on a generated map, with houses along it: a
 * row or column of street tiles (GroundKind 1) at least `width` across,
 * scored by its length and how much of it has a building within a few tiles
 * on both sides.
 */
export function longestStreet(s: GameState, minWidth = 2): Street | null {
  const g = s.ground;
  if (!g) return null;
  const w = s.width;
  const at = (x: number, y: number) => (x >= 0 && y >= 0 && x < w && y < s.height ? g[y * w + x] : -1);
  const built = (x: number, y: number, dx: number, dy: number) => {
    for (let k = 1; k <= 5; k++) if (at(x + dx * k, y + dy * k) === 2) return true;
    return false;
  };
  let best: { street: Street; score: number } | null = null;
  for (const horizontal of [true, false]) {
    const rows = horizontal ? s.height : w;
    const cols = horizontal ? w : s.height;
    for (let r = 0; r < rows; r++) {
      let start = -1;
      for (let c = 0; c <= cols; c++) {
        const [x, y] = horizontal ? [c, r] : [r, c];
        // Street here and `minWidth` across (on the side of growing r).
        let ok = c < cols;
        for (let k = 0; ok && k < minWidth; k++) ok = (horizontal ? at(x, y + k) : at(x + k, y)) === 1;
        if (ok && start < 0) start = c;
        if (ok || start < 0) continue;
        const length = c - start;
        let lined = 0;
        for (let k = start; k < c; k++) {
          const [px, py] = horizontal ? [k, r] : [r, k];
          const [ax, ay] = horizontal ? [0, -1] : [-1, 0];
          if (built(px, py, ax, ay) && built(px + (horizontal ? 0 : minWidth - 1), py + (horizontal ? minWidth - 1 : 0), -ax, -ay)) lined++;
        }
        const score = length * 0.4 + lined;
        if (!best || score > best.score) {
          const [fx, fy] = horizontal ? [start, r] : [r, start];
          const [tx, ty] = horizontal ? [c - 1, r] : [r, c - 1];
          best = { street: { from: { x: fx, y: fy }, to: { x: tx, y: ty }, width: minWidth }, score };
        }
        start = -1;
      }
    }
  }
  if (!best) return null;
  // As wide as it really is: the rows (or columns) beside it that are street over most of its length.
  const st = best.street;
  const horizontal = st.from.y === st.to.y;
  const length = horizontal ? st.to.x - st.from.x + 1 : st.to.y - st.from.y + 1;
  const street = (offset: number) => {
    let n = 0;
    for (let k = 0; k < length; k++) if ((horizontal ? at(st.from.x + k, st.from.y + offset) : at(st.from.x + offset, st.from.y + k)) === 1) n++;
    return n >= length * 0.7;
  };
  let lo = 0;
  let hi = st.width - 1;
  while (street(lo - 1)) lo--;
  while (street(hi + 1)) hi++;
  return {
    from: horizontal ? { x: st.from.x, y: st.from.y + lo } : { x: st.from.x + lo, y: st.from.y },
    to: horizontal ? { x: st.to.x, y: st.to.y + lo } : { x: st.to.x + lo, y: st.to.y },
    width: hi - lo + 1,
  };
}
