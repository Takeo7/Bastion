import * as THREE from 'three';
import { KitBatch, kitModel } from '../strategy/baseKit';
import type { BaseModelId } from '../game/syntyManifest';

// Sketches for Gameplay (docs/PRUEBA-2026-10-14.md, P12: the valley's towns
// «parecen un boceto»): a town laid out as a real one, dropped on an empty
// meadow of the valley so the lab can shoot it beside what is there now,
// from the campaign's camera or another, by day or at dusk. Lab only.

const TRUE = 0.035;
const STREET = new THREE.MeshStandardMaterial({ color: 0x5b5c5e, roughness: 0.95 });
const WALK = new THREE.MeshStandardMaterial({ color: 0xa29d93, roughness: 0.95 });
const SQUARE = new THREE.MeshStandardMaterial({ color: 0x8c877d, roughness: 0.95 });
const YARD = new THREE.MeshStandardMaterial({ color: 0x5f7a3c, roughness: 1 });
const LAMP = new THREE.MeshBasicMaterial({ color: 0xffc777, toneMapped: false });
const WINDOW = new THREE.MeshBasicMaterial({ color: 0xffb35c, toneMapped: false });
const BOX = new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0);
const UP = new THREE.Vector3(0, 1, 0);

function hash(a: number, b: number): number {
  const s = Math.sin(a * 127.1 + b * 311.7) * 43758.5453;
  return s - Math.floor(s);
}

export interface Town {
  group: THREE.Group;
  /** Where its lamps and lit windows stand (for the dusk sketch). */
  lamps: THREE.Vector3[];
  windows: THREE.Vector3[];
}

/**
 * A town at (cx, cz) along `axis`: its main street with pavements, terraced
 * houses and shops either side facing it, a square halfway with the church,
 * two cross streets, yards and plots behind; everything at true scale.
 */
export function sketchTown(ground: THREE.Object3D, cx: number, cz: number, axis: number, scale = 1): Town {
  const kit = new KitBatch();
  const ray = new THREE.Raycaster();
  const heightAt = (x: number, z: number) => {
    ray.set(new THREE.Vector3(x, 20, z), new THREE.Vector3(0, -1, 0));
    return ray.intersectObject(ground, false)[0]?.point.y ?? 0;
  };
  const k = TRUE * scale;
  const dir = new THREE.Vector2(Math.cos(axis), Math.sin(axis));
  const side = new THREE.Vector2(-dir.y, dir.x);
  const at = (t: number, s: number) => ({ x: cx + dir.x * t + side.x * s, z: cz + dir.y * t + side.y * s });
  /** A flat strip from a to b (a street, a pavement, a yard), in short pieces that follow the ground. */
  const strip = (m: THREE.Material, a: { x: number; z: number }, b: { x: number; z: number }, width: number, lift = 0) => {
    const length = Math.hypot(b.x - a.x, b.z - a.z);
    const n = Math.max(1, Math.ceil(length / 0.2));
    const yaw = Math.atan2(-(b.z - a.z), b.x - a.x);
    const q = new THREE.Quaternion().setFromAxisAngle(UP, yaw);
    for (let i = 0; i < n; i++) {
      const u = (i + 0.5) / n;
      const x = a.x + (b.x - a.x) * u;
      const z = a.z + (b.z - a.z) * u;
      // A little longer than its share, and tilted with the slope, so the pieces join.
      const h0 = heightAt(a.x + (b.x - a.x) * (i / n), a.z + (b.z - a.z) * (i / n));
      const h1 = heightAt(a.x + (b.x - a.x) * ((i + 1) / n), a.z + (b.z - a.z) * ((i + 1) / n));
      const tilt = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 0, 1), Math.atan2(h1 - h0, length / n));
      kit.prim(BOX, m, new THREE.Matrix4().compose(new THREE.Vector3(x, (h0 + h1) / 2 - 0.012 + lift, z), q.clone().multiply(tilt), new THREE.Vector3(length / n + 0.01, 0.02, width)), false);
    }
  };
  const put = (id: BaseModelId, x: number, z: number, yaw: number, s = k) => kit.put(id, { x, z, y: heightAt(x, z) - 0.01, yaw, s, finish: 'map' });
  const widthOf = (id: BaseModelId) => {
    const size = kitModel(id)?.size;
    return size ? Math.min(size.x, size.z) * k : 0.25 * scale;
  };
  const lamps: THREE.Vector3[] = [];
  const windows: THREE.Vector3[] = [];

  const half = 1.7 * scale;
  const road = 0.16 * scale;
  const walk = 0.05 * scale;
  // The main street and its pavements.
  strip(STREET, at(-half - 0.6 * scale, 0), at(half + 0.15 * scale, 0), road);
  for (const s of [-1, 1]) strip(WALK, at(-half, s * (road / 2 + walk / 2)), at(half, s * (road / 2 + walk / 2)), walk, 0.004);
  // Two cross streets.
  const cross = [-0.95 * scale, 0.95 * scale];
  for (const t of cross) {
    strip(STREET, at(t, -1.0 * scale), at(t, 1.0 * scale), road * 0.8);
    for (const s of [-1, 1]) strip(WALK, at(t + s * (road * 0.4 + walk / 2), -1.0 * scale), at(t + s * (road * 0.4 + walk / 2), 1.0 * scale), walk, 0.004);
  }
  // The square, halfway, on the north side: paved, the church across it, trees at its corners.
  const sq = { t: 0, s: 0.55 * scale, w: 0.62 * scale, d: 0.6 * scale };
  strip(SQUARE, at(sq.t - sq.w / 2, sq.s), at(sq.t + sq.w / 2, sq.s), sq.d, 0.003);
  const church = at(sq.t, sq.s + sq.d / 2 + 0.22 * scale);
  put('mapChurch', church.x, church.z, axis + Math.PI, k * 1.05);
  for (const [dt, ds] of [
    [-1, -1],
    [1, -1],
    [-1, 1],
    [1, 1],
  ] as const) {
    const p = at(sq.t + dt * sq.w * 0.42, sq.s + ds * sq.d * 0.38);
    put('mapTree', p.x, p.z, hash(dt, ds) * 6, k * 0.8);
  }

  // The rows: shops near the square, houses further out, gable to gable, facing the street; a yard behind.
  const SHOPS: BaseModelId[] = ['mapShopS', 'mapShop', 'mapDiner', 'mapShopM', 'mapOffice'];
  const HOUSES: BaseModelId[] = ['mapHouse', 'mapHouseSquare', 'mapHouseL', 'mapHouseA', 'mapHouseC'];
  /** A row along a street (`along` its way, `across` the way out of it): its fronts `out` from the street's middle. */
  const row = (from: number, to: number, s: 1 | -1, set: number, street: (t: number) => { x: number; z: number }, along: THREE.Vector2, out: number) => {
    const across = new THREE.Vector2(-along.y, along.x);
    // Facing the street: from the house back across to it.
    const facing = Math.atan2(-across.x * s, -across.y * s);
    let t = from;
    let n = 0;
    while (t < to) {
      const near = Math.abs(t) < 0.75 * scale && set === 0;
      const list = near ? SHOPS : HOUSES;
      const id = list[Math.floor(hash(t * 13 + set, s) * list.length)]!;
      const w = widthOf(id);
      const mid = t + w / 2;
      if (mid + w / 2 > to) break;
      const front = street(mid);
      const back = 0.04 * scale + w / 2;
      const p = { x: front.x + across.x * s * (out + back), z: front.z + across.y * s * (out + back) };
      if (set === 0 && s > 0 && Math.abs(mid - sq.t) < sq.w / 2 + 0.05 * scale) {
        t += w;
        continue;
      }
      put(id, p.x, p.z, facing, k);
      windows.push(new THREE.Vector3(p.x - across.x * s * w * 0.5, heightAt(p.x, p.z) + 0.06 * scale, p.z - across.y * s * w * 0.5));
      // The yard behind, a tree in one now and then.
      const yard = { x: p.x + across.x * s * (w / 2 + 0.12 * scale), z: p.z + across.y * s * (w / 2 + 0.12 * scale) };
      strip(YARD, { x: yard.x - along.x * w * 0.45, z: yard.z - along.y * w * 0.45 }, { x: yard.x + along.x * w * 0.45, z: yard.z + along.y * w * 0.45 }, 0.2 * scale, 0.002);
      if (hash(n, set + s) < 0.35) put('mapTree', yard.x, yard.z, hash(n, 3) * 6, k * 0.7);
      t += w + 0.01 * scale;
      n++;
    }
  };
  const mainAt = (t: number) => at(t, 0);
  const gaps = [-half, cross[0]! - road * 0.4 - walk, cross[0]! + road * 0.4 + walk, cross[1]! - road * 0.4 - walk, cross[1]! + road * 0.4 + walk, half];
  for (const s of [-1, 1] as const) {
    for (let g = 0; g < gaps.length; g += 2) row(gaps[g]!, gaps[g + 1]!, s, 0, mainAt, dir, road / 2 + walk);
    // Along the cross streets, out from the main street's rows.
    const crossDir = new THREE.Vector2(side.x, side.y);
    for (const [c, t] of cross.entries()) {
      const crossAt = (u: number) => at(t, u);
      for (const [from, to] of [
        [0.5 * scale, 1.0 * scale],
        [-1.0 * scale, -0.5 * scale],
      ] as const)
        row(from, to, s, 1 + c, crossAt, crossDir, road * 0.4 + walk);
    }
  }
  // Lamps along the main street and the cross streets, both sides in turn.
  for (let t = -half; t <= half; t += 0.32 * scale) {
    const s = Math.round(t / (0.32 * scale)) % 2 ? 1 : -1;
    const p = at(t, s * (road / 2 + walk * 0.6));
    lamps.push(new THREE.Vector3(p.x, heightAt(p.x, p.z) + 0.07 * scale, p.z));
  }
  for (const t of cross)
    for (let u = -0.9 * scale; u <= 0.9 * scale; u += 0.32 * scale) {
      const p = at(t + road * 0.5, u);
      lamps.push(new THREE.Vector3(p.x, heightAt(p.x, p.z) + 0.07 * scale, p.z));
    }
  const group = kit.build(true);
  return { group, lamps, windows };
}

const POOL = new THREE.MeshBasicMaterial({ color: 0xffb768, transparent: true, opacity: 0.32, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false });

/** The light a street lamp throws on the ground under it, for the dusk sketch. */
export function pools(points: THREE.Vector3[], radius = 0.09): THREE.InstancedMesh {
  const mesh = new THREE.InstancedMesh(new THREE.CircleGeometry(radius, 16).rotateX(-Math.PI / 2), POOL, points.length);
  points.forEach((p, i) => mesh.setMatrixAt(i, new THREE.Matrix4().makeTranslation(p.x, p.y - 0.062, p.z)));
  mesh.instanceMatrix.needsUpdate = true;
  return mesh;
}

/** Small glowing points for the dusk sketch: a lamp's light, a window lit. */
export function lights(points: THREE.Vector3[], window = false, size = 0.025): THREE.InstancedMesh {
  const mesh = new THREE.InstancedMesh(new THREE.BoxGeometry(size, size * (window ? 0.8 : 1), size), window ? WINDOW : LAMP, points.length);
  points.forEach((p, i) => mesh.setMatrixAt(i, new THREE.Matrix4().makeTranslation(p.x, p.y, p.z)));
  mesh.instanceMatrix.needsUpdate = true;
  return mesh;
}

/** Rounds a sketch can clear at once. */
const ROUNDS = 8;

/**
 * Takes away what `meshes` draw within `r` of (x, z), their shadows too (a
 * sketch's room on the map: the town that stood there goes); `more` rounds
 * besides, up to ROUNDS in all. Lab only: it wraps each material's shader
 * with a discard.
 */
export function clearRound(meshes: THREE.Mesh[], x: number, z: number, r: number, more: [number, number, number][] = []): void {
  const rounds = [[x, z, r] as [number, number, number], ...more].slice(0, ROUNDS);
  const clear = { value: Array.from({ length: ROUNDS }, (_, k) => new THREE.Vector3(...(rounds[k] ?? [0, 0, 0]))) };
  const wrap = (m: THREE.Material) => {
    if (m.userData.cleared) {
      (m.userData.cleared as { value: THREE.Vector3[] }).value.forEach((v, k) => v.set(...(rounds[k] ?? [0, 0, 0])));
      return;
    }
    m.userData.cleared = clear;
    const before = m.onBeforeCompile.bind(m);
    const key = m.customProgramCacheKey.bind(m);
    m.onBeforeCompile = (shader, renderer) => {
      before(shader, renderer);
      shader.uniforms.uClear = clear;
      shader.vertexShader = shader.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 vClearAt;\nvarying float vClearKeep;').replace(
        '#include <project_vertex>',
        `#include <project_vertex>
        {
          vec4 at = vec4(transformed, 1.0);
          #ifdef USE_BATCHING
            at = batchingMatrix * at;
          #endif
          vClearKeep = 0.0;
          #ifdef USE_INSTANCING
            at = instanceMatrix * at;
            vClearKeep = 1.0;
          #endif
          vClearAt = (modelMatrix * at).xyz;
        }`,
      );
      shader.fragmentShader = shader.fragmentShader
        .replace('#include <common>', `#include <common>\nvarying vec3 vClearAt;\nvarying float vClearKeep;\nuniform vec3 uClear[${ROUNDS}];`)
        // The sketch's own models are instanced (KitBatch.build) and share these materials: they stay.
        .replace(
          '#include <clipping_planes_fragment>',
          `#include <clipping_planes_fragment>
          if (vClearKeep < 0.5) for (int k = 0; k < ${ROUNDS}; k++) if (distance(vClearAt.xz, uClear[k].xy) < uClear[k].z) discard;`,
        );
    };
    m.customProgramCacheKey = () => `${key()}|clear`;
    m.needsUpdate = true;
  };
  const depth = new THREE.MeshDepthMaterial({ depthPacking: THREE.RGBADepthPacking });
  wrap(depth);
  for (const mesh of meshes) {
    for (const m of Array.isArray(mesh.material) ? mesh.material : [mesh.material]) wrap(m);
    mesh.customDepthMaterial = depth;
  }
}
