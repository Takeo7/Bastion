import * as THREE from 'three';
import { atlasOf, atlasTexture, loadModel } from '../game/synty';
import { BASE_MODELS, baseTextureFile, type AtlasId, type BaseModelId, type BaseTextureId } from '../game/syntyManifest';

// The Synty models of the Bastion (syntyManifest: BASE_MODELS), ready to
// place: each is loaded once, baked to one geometry per mesh, centred on x/z
// and standing on y = 0 at its true size (game units). The base places them
// by the hundred, so placements are gathered in a batch and drawn as one
// InstancedMesh per mesh and finish. Materials are shared: one per atlas and
// finish, the snowy ones whitening every face that looks up (Synty's flat
// shading gives crisp low-poly snow caps).

/** One mesh of a model, in the model's normalised space. */
export interface KitPart {
  name: string;
  geometry: THREE.BufferGeometry;
  atlas: AtlasId;
}

export interface KitModel {
  parts: KitPart[];
  /** Size of the whole model (x, y, z), in game units. */
  size: THREE.Vector3;
}

/**
 * How a model is painted: `snow` its atlas with snow on what looks up,
 * `plain` its atlas as it comes (indoors, vehicles), `stone` and `mountain`
 * bare rock under snow (Alpine's rocks come for a triplanar shader, so their
 * atlas colours mean little), `snowbank` all snow.
 */
export type Finish = 'snow' | 'plain' | 'stone' | 'mountain' | 'snowbank';

const models = new Map<BaseModelId, KitModel>();
let loading: Promise<void> | null = null;

/** An attribute as plain floats: meshopt stores positions and normals quantized (the node's matrix scales them back). */
function floats(attr: THREE.BufferAttribute | THREE.InterleavedBufferAttribute): THREE.BufferAttribute {
  const n = attr.itemSize;
  const out = new Float32Array(attr.count * n);
  for (let i = 0; i < attr.count; i++) {
    out[i * n] = attr.getX(i);
    if (n > 1) out[i * n + 1] = attr.getY(i);
    if (n > 2) out[i * n + 2] = attr.getZ(i);
    if (n > 3) out[i * n + 3] = attr.getW(i);
  }
  return new THREE.BufferAttribute(out, n);
}

/** A mesh's geometry in the model's space, as floats. */
function baked(mesh: THREE.Mesh): THREE.BufferGeometry {
  const src = mesh.geometry;
  const g = new THREE.BufferGeometry();
  for (const name of ['position', 'normal', 'uv'] as const) {
    const attr = src.getAttribute(name);
    if (attr) g.setAttribute(name, floats(attr));
  }
  if (src.index) g.setIndex(src.index.clone());
  g.applyMatrix4(mesh.matrixWorld);
  return g;
}

function prepare(scene: THREE.Group): KitModel {
  scene.updateMatrixWorld(true);
  const parts: KitPart[] = [];
  scene.traverse((o) => {
    if (o instanceof THREE.Mesh) parts.push({ name: o.name, geometry: baked(o), atlas: atlasOf(o) });
  });
  const box = new THREE.Box3();
  for (const p of parts) {
    p.geometry.computeBoundingBox();
    box.union(p.geometry.boundingBox!);
  }
  const centre = box.getCenter(new THREE.Vector3());
  for (const p of parts) {
    p.geometry.translate(-centre.x, -box.min.y, -centre.z);
    p.geometry.computeBoundingSphere();
  }
  return { parts, size: box.getSize(new THREE.Vector3()) };
}

/** Loads every model of the base (once); resolves when all have loaded or failed. */
export function loadBaseKit(): Promise<void> {
  loading ??= Promise.all(
    (Object.keys(BASE_MODELS) as BaseModelId[]).map((id) =>
      loadModel(BASE_MODELS[id].pack, 'base', id)
        .then((scene) => void models.set(id, prepare(scene)))
        .catch(() => undefined),
    ),
  ).then(() => undefined);
  return loading;
}

export function kitModel(id: BaseModelId): KitModel | undefined {
  return models.get(id);
}

// ------------------------------------------------------------- materials

const SNOW = new THREE.Color(0xeef3f8);

/**
 * Snow on faces that look up: by the face's world normal, with a gentle
 * wave in world space so the edge of the cover is not a straight line.
 */
function snowPatch(material: THREE.MeshStandardMaterial, amount: number, low: number, high: number, key: string): void {
  material.onBeforeCompile = (shader) => {
    Object.assign(shader.uniforms, { snowColor: { value: SNOW }, snowAmount: { value: amount }, snowRange: { value: new THREE.Vector2(low, high) } });
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', '#include <common>\nvarying vec3 vSnowNormal;\nvarying vec3 vSnowPos;')
      .replace(
        '#include <project_vertex>',
        `#include <project_vertex>
        vec4 snowWorld = vec4(transformed, 1.0);
        vec3 snowNormal = objectNormal;
        #ifdef USE_INSTANCING
          snowWorld = instanceMatrix * snowWorld;
          snowNormal = mat3(instanceMatrix) * snowNormal;
        #endif
        vSnowPos = (modelMatrix * snowWorld).xyz;
        vSnowNormal = normalize(mat3(modelMatrix) * snowNormal);`,
      );
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', '#include <common>\nvarying vec3 vSnowNormal;\nvarying vec3 vSnowPos;\nuniform vec3 snowColor;\nuniform float snowAmount;\nuniform vec2 snowRange;')
      .replace(
        '#include <map_fragment>',
        `#include <map_fragment>
        float snowNoise = sin(vSnowPos.x * 1.7 + sin(vSnowPos.z * 1.3)) * sin(vSnowPos.z * 1.9 + vSnowPos.y * 2.1);
        float snowCover = smoothstep(snowRange.x, snowRange.y, normalize(vSnowNormal).y + snowNoise * 0.06);
        diffuseColor.rgb = mix(diffuseColor.rgb, snowColor, snowCover * snowAmount);`,
      );
  };
  material.customProgramCacheKey = () => key;
}

const materials = new Map<string, THREE.Material>();

/** The shared material of an atlas in a finish. */
export function kitMaterial(atlas: AtlasId, finish: Finish): THREE.Material {
  const key = `${atlas}:${finish}`;
  let m = materials.get(key);
  if (m) return m;
  if (finish === 'stone' || finish === 'mountain') {
    const stone = new THREE.MeshStandardMaterial({ color: finish === 'stone' ? 0x5b5955 : 0x6c6e72, roughness: 0.95, flatShading: true });
    snowPatch(stone, finish === 'stone' ? 0.85 : 1, finish === 'stone' ? 0.7 : 0.6, finish === 'stone' ? 0.9 : 0.76, `base-${finish}`);
    m = stone;
  } else if (finish === 'snowbank') {
    m = new THREE.MeshStandardMaterial({ color: 0xdfe6ee, roughness: 1, flatShading: true });
  } else {
    const painted = new THREE.MeshStandardMaterial({ map: atlasTexture(atlas), roughness: 0.82, metalness: 0 });
    if (finish === 'snow') snowPatch(painted, 0.8, 0.8, 0.96, 'base-snow');
    m = painted;
  }
  m.name = key;
  materials.set(key, m);
  return m;
}

/** Glass of vehicles and huts: dark and a little shiny. */
export const KIT_GLASS = new THREE.MeshStandardMaterial({ color: 0x1c2a33, roughness: 0.15, metalness: 0.4 });

const isGlass = (name: string) => /glass/i.test(name);

/** A tiling ground texture of the base (its image arrives when loaded). */
export function baseTexture(id: BaseTextureId, repeat: number): THREE.Texture {
  const t = new THREE.TextureLoader().load(`${import.meta.env.BASE_URL}${baseTextureFile(id)}`);
  t.wrapS = t.wrapT = THREE.RepeatWrapping;
  t.repeat.set(repeat, repeat);
  t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = 4;
  return t;
}

// ------------------------------------------------------------- placing

/** Where and how big a model stands: a turn about y, its size and a shift. */
export interface Put {
  x?: number;
  y?: number;
  z?: number;
  /** Turn about y, in radians (counter-clockwise from above). */
  yaw?: number;
  /** Scale on its true size. */
  s?: number;
  /** Instead of `s`: the scale that makes its longer side (x or z) this long. */
  fit?: number;
  /** Extra scale on each axis, after `s` or `fit` (the model's own axes). */
  sx?: number;
  sy?: number;
  sz?: number;
  finish?: Finish;
}

const tmp = { m: new THREE.Matrix4(), q: new THREE.Quaternion(), p: new THREE.Vector3(), s: new THREE.Vector3(), up: new THREE.Vector3(0, 1, 0) };

/** The matrix of a placement, under `parent` (a plot's origin). */
export function putMatrix(id: BaseModelId, o: Put, parent?: THREE.Matrix4): THREE.Matrix4 {
  const size = models.get(id)?.size;
  const base = o.fit && size ? o.fit / Math.max(size.x, size.z, 1e-3) : (o.s ?? 1);
  tmp.q.setFromAxisAngle(tmp.up, o.yaw ?? 0);
  tmp.p.set(o.x ?? 0, o.y ?? 0, o.z ?? 0);
  tmp.s.set(base * (o.sx ?? 1), base * (o.sy ?? 1), base * (o.sz ?? 1));
  const m = new THREE.Matrix4().compose(tmp.p, tmp.q, tmp.s);
  return parent ? m.premultiply(parent) : m;
}

/**
 * Placements gathered for one build, drawn as instanced meshes: one per mesh
 * of each model and finish, and one per primitive kind (lamps, slabs).
 */
export class KitBatch {
  private readonly placed = new Map<string, { id: BaseModelId; finish: Finish; matrices: THREE.Matrix4[] }>();
  private readonly prims = new Map<string, { geometry: THREE.BufferGeometry; material: THREE.Material; shadow: boolean; matrices: THREE.Matrix4[] }>();

  /** Places model `id` (nothing if it failed to load). Returns the matrix used. */
  put(id: BaseModelId, o: Put = {}, parent?: THREE.Matrix4): THREE.Matrix4 {
    const m = putMatrix(id, o, parent);
    if (!models.has(id)) return m;
    const finish = o.finish ?? 'snow';
    const key = `${id}:${finish}`;
    let entry = this.placed.get(key);
    if (!entry) this.placed.set(key, (entry = { id, finish, matrices: [] }));
    entry.matrices.push(m);
    return m;
  }

  /** A primitive (a box for a lamp, a slab, a strip), by kind; all of a kind share geometry and material. */
  prim(kind: string, geometry: THREE.BufferGeometry, material: THREE.Material, matrix: THREE.Matrix4, shadow = true): void {
    let entry = this.prims.get(kind);
    if (!entry) this.prims.set(kind, (entry = { geometry, material, shadow, matrices: [] }));
    entry.matrices.push(matrix);
  }

  /** The instanced meshes; with `shadows` false none casts one (what stands beyond the sun's shadow box). */
  build(shadows = true): THREE.Group {
    const g = new THREE.Group();
    const instanced = (geometry: THREE.BufferGeometry, material: THREE.Material, matrices: THREE.Matrix4[], shadow: boolean) => {
      const mesh = new THREE.InstancedMesh(geometry, material, matrices.length);
      matrices.forEach((m, i) => mesh.setMatrixAt(i, m));
      mesh.instanceMatrix.needsUpdate = true;
      mesh.computeBoundingSphere();
      mesh.castShadow = shadow && shadows;
      mesh.receiveShadow = true;
      g.add(mesh);
    };
    for (const { id, finish, matrices } of this.placed.values()) {
      for (const part of models.get(id)!.parts) instanced(part.geometry, isGlass(part.name) ? KIT_GLASS : kitMaterial(part.atlas, finish), matrices, true);
    }
    for (const p of this.prims.values()) instanced(p.geometry, p.material, p.matrices, p.shadow);
    return g;
  }
}

/** One model as plain meshes (it moves on its own: a door, a wheel, a radar), sharing the kit's geometry and materials. */
export function kitMesh(id: BaseModelId, finish: Finish = 'snow', only?: (part: string) => boolean): THREE.Group {
  const g = new THREE.Group();
  for (const part of models.get(id)?.parts ?? []) {
    if (only && !only(part.name)) continue;
    const mesh = new THREE.Mesh(part.geometry, isGlass(part.name) ? KIT_GLASS : kitMaterial(part.atlas, finish));
    mesh.name = part.name;
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    g.add(mesh);
  }
  return g;
}
