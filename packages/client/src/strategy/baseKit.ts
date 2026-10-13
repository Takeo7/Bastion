import * as THREE from 'three';
import type { StaticBatch, Dirty } from '../game/batch';
import { extendShader } from '../game/fogOfWar';
import { GroundOcclusion } from '../game/groundAO';
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
 * atlas colours mean little), `snowbank` all snow. The `map…` finishes are
 * the same for the valley's map (geoscape.ts), without the base's baked
 * contact occlusion (it belongs to the base's ground, not the map's).
 */
export type Finish = 'snow' | 'plain' | 'stone' | 'mountain' | 'snowbank' | 'map' | 'mapSnow' | 'mapMountain';

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

/** Where a vertex is in the world and which way it faces, whatever draws it (plain, instanced or merged). */
const WORLD_VERTEX = (pos: string, normal: string) => /* glsl */ `
  {
    vec4 w = vec4(transformed, 1.0);
    vec3 n = objectNormal;
    #ifdef USE_BATCHING
      w = batchingMatrix * w;
      n = mat3(batchingMatrix) * n;
    #endif
    #ifdef USE_INSTANCING
      w = instanceMatrix * w;
      n = mat3(instanceMatrix) * n;
    #endif
    ${pos} = (modelMatrix * w).xyz;
    ${normal} = normalize(mat3(modelMatrix) * n);
  }`;

/**
 * Snow on faces that look up: by the face's world normal, with a gentle
 * wave in world space so the edge of the cover is not a straight line.
 */
function snowPatch(material: THREE.Material, amount: number, low: number, high: number): void {
  extendShader(material, `base-snow:${amount}:${low}:${high}`, (shader) => {
    Object.assign(shader.uniforms, { snowColor: { value: SNOW }, snowAmount: { value: amount }, snowRange: { value: new THREE.Vector2(low, high) } });
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', '#include <common>\nvarying vec3 vSnowNormal;\nvarying vec3 vSnowPos;')
      .replace('#include <project_vertex>', `#include <project_vertex>${WORLD_VERTEX('vSnowPos', 'vSnowNormal')}`);
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', '#include <common>\nvarying vec3 vSnowNormal;\nvarying vec3 vSnowPos;\nuniform vec3 snowColor;\nuniform float snowAmount;\nuniform vec2 snowRange;')
      .replace(
        '#include <map_fragment>',
        `#include <map_fragment>
        float snowNoise = sin(vSnowPos.x * 1.7 + sin(vSnowPos.z * 1.3)) * sin(vSnowPos.z * 1.9 + vSnowPos.y * 2.1);
        float snowCover = smoothstep(snowRange.x, snowRange.y, normalize(vSnowNormal).y + snowNoise * 0.06);
        diffuseColor.rgb = mix(diffuseColor.rgb, snowColor, snowCover * snowAmount);`,
      );
  });
}

/**
 * The base's contact occlusion, baked once into a texture over the compound
 * and the hangar (groundAO.ts, as on the combat maps: everything sits in its
 * own soft dark, at no cost a frame). The bake works in a map whose corner is
 * at the origin, so the base's area (centred on it) is shifted there for the
 * bake and the lookup.
 */
export class BaseOcclusion {
  readonly ao: GroundOcclusion;
  /** The world point at the texture's corner (x, z). */
  readonly origin: THREE.Vector2;
  readonly uniforms: GroundOcclusion['uniforms'] & { groundAOOrigin: { value: THREE.Vector2 } };

  constructor(x0: number, z0: number, x1: number, z1: number) {
    this.ao = new GroundOcclusion(x1 - x0, z1 - z0);
    this.origin = new THREE.Vector2(x0, z0);
    this.uniforms = { ...this.ao.uniforms, groundAOOrigin: { value: this.origin } };
  }

  /** Bakes `region` (world units) from `occluders` (merged, placed in the world). */
  bake(gl: THREE.WebGLRenderer, occluders: THREE.BufferGeometry[], region: Dirty): void {
    const { x: ox, y: oz } = this.origin;
    const shifted = occluders.map((g) => {
      const c = new THREE.BufferGeometry();
      c.setAttribute('position', g.attributes.position!.clone());
      if (g.index) c.setIndex(g.index);
      return c.translate(-ox, 0, -oz);
    });
    this.ao.bake(gl, shifted, { x0: region.x0 - ox, y0: region.y0 - oz, x1: region.x1 - ox, y1: region.y1 - oz });
    for (const c of shifted) c.dispose();
  }
}

/**
 * Darkens surfaces lying on the ground or on a floor by the baked occlusion
 * (as groundAO.ts's contactPatch, with the base's area shifted).
 */
function contactPatch(material: THREE.Material, occlusion: BaseOcclusion, strength = 0.9): void {
  extendShader(material, 'base-contact', (shader) => {
    Object.assign(shader.uniforms, occlusion.uniforms);
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', '#include <common>\nvarying vec3 vCaoPos;\nvarying vec3 vCaoNormal;')
      .replace('#include <project_vertex>', `#include <project_vertex>${WORLD_VERTEX('vCaoPos', 'vCaoNormal')}`);
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', '#include <common>\nuniform sampler2D tGroundAO;\nuniform vec2 groundAOSize;\nuniform vec2 groundAOOrigin;\nvarying vec3 vCaoPos;\nvarying vec3 vCaoNormal;')
      .replace(
        '#include <aomap_fragment>',
        `#include <aomap_fragment>
        {
          vec2 caoUv = vec2((vCaoPos.x - groundAOOrigin.x) / groundAOSize.x, 1.0 - (vCaoPos.z - groundAOOrigin.y) / groundAOSize.y);
          if (vCaoNormal.y > 0.7 && caoUv.x > 0.0 && caoUv.x < 1.0 && caoUv.y > 0.0 && caoUv.y < 1.0) {
            vec4 cao = texture2D(tGroundAO, caoUv);
            // Only where this surface is the one the occlusion was worked out for (not a roof over the ground).
            float here = 1.0 - smoothstep(0.15, 0.6, abs(vCaoPos.y - cao.g));
            float occlusion = mix(1.0, cao.r, ${strength.toFixed(2)} * here);
            reflectedLight.indirectDiffuse *= occlusion;
            reflectedLight.directDiffuse *= mix(1.0, occlusion, 0.8);
          }
        }`,
      );
  });
}

/**
 * The base's area for the baked occlusion: the compound, its barriers, the
 * hangar and its apron (world units, x and z).
 */
const AO_AREA = { x0: -52, z0: -36, x1: 52, z1: 54 };

/** The occlusion every material of the base reads (one for all: the base is one place). */
export const BASE_OCCLUSION = new BaseOcclusion(AO_AREA.x0, AO_AREA.z0, AO_AREA.x1, AO_AREA.z1);

const materials = new Map<string, THREE.Material>();

/** A material of the base's own (a pad, the ground): snow on it if asked, and it sits in the baked occlusion. */
export function baseMaterial<T extends THREE.Material>(m: T, snow?: { amount: number; low: number; high: number }): T {
  if (snow) snowPatch(m, snow.amount, snow.low, snow.high);
  contactPatch(m, BASE_OCCLUSION);
  return m;
}

/** The shared material of an atlas in a finish. */
export function kitMaterial(atlas: AtlasId, finish: Finish): THREE.Material {
  const key = `${atlas}:${finish}`;
  let m = materials.get(key);
  if (m) return m;
  if (finish === 'stone' || finish === 'mountain' || finish === 'mapMountain') {
    m = new THREE.MeshStandardMaterial({ color: finish === 'stone' ? 0x5b5955 : 0x6c6e72, roughness: 0.95, flatShading: true });
    snowPatch(m, finish === 'stone' ? 0.85 : 1, finish === 'stone' ? 0.7 : 0.6, finish === 'stone' ? 0.9 : 0.76);
  } else if (finish === 'snowbank') {
    m = new THREE.MeshStandardMaterial({ color: 0xdfe6ee, roughness: 1, flatShading: true });
  } else {
    m = new THREE.MeshStandardMaterial({ map: atlasTexture(atlas), roughness: 0.82, metalness: 0 });
    if (finish === 'snow' || finish === 'mapSnow') snowPatch(m, 0.8, 0.8, 0.96);
  }
  m.name = key;
  materials.set(key, m);
  if (!finish.startsWith('map')) contactPatch(m, BASE_OCCLUSION);
  return m;
}

/** Glass of vehicles and huts: dark and a little shiny. */
export const KIT_GLASS = new THREE.MeshStandardMaterial({ color: 0x1c2a33, roughness: 0.15, metalness: 0.4 });

/**
 * The Huerto's greenhouse panes: pale green and see-through, the plants
 * inside showing; no snow on them, and no shadow (the sun comes in).
 */
const GREENHOUSE_GLASS = new THREE.MeshStandardMaterial({ color: 0xa6dcae, roughness: 0.18, transparent: true, opacity: 0.4, depthWrite: false });

const isGlass = (name: string) => /glass/i.test(name);

/** What a model's mesh is made of: its glass (the greenhouse's of its own), or its atlas in a finish. */
const partMaterial = (id: BaseModelId, part: { name: string; atlas: AtlasId }, finish: Finish) =>
  isGlass(part.name) ? (id === 'greenhouse' ? GREENHOUSE_GLASS : KIT_GLASS) : kitMaterial(part.atlas, finish);

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
 * Placements gathered for one build: models (each mesh of each model, in a
 * finish) and primitives (lamps, pads), merged then into a StaticBatch
 * (game/batch.ts: one draw per material, chunks culled one by one).
 */
export class KitBatch {
  private readonly placed: { id: BaseModelId; finish: Finish; matrix: THREE.Matrix4 }[] = [];
  private readonly prims: { geometry: THREE.BufferGeometry; material: THREE.Material; shadow: boolean; matrix: THREE.Matrix4 }[] = [];

  /** Places model `id` (nothing if it failed to load). Returns the matrix used. */
  put(id: BaseModelId, o: Put = {}, parent?: THREE.Matrix4): THREE.Matrix4 {
    const m = putMatrix(id, o, parent);
    if (models.has(id)) this.placed.push({ id, finish: o.finish ?? 'snow', matrix: m });
    return m;
  }

  /** A primitive (a box for a lamp, a slab, a strip); `shadow`: whether it casts one. */
  prim(geometry: THREE.BufferGeometry, material: THREE.Material, matrix: THREE.Matrix4, shadow = true): void {
    this.prims.push({ geometry, material, shadow, matrix });
  }

  /**
   * Adds everything to `batch` (between its begin and commit). With `casts`
   * false nothing casts a shadow or darkens the ground (what stands beyond the
   * sun's shadow box and the baked occlusion).
   */
  addTo(batch: StaticBatch, casts = true): void {
    for (const { id, finish, matrix } of this.placed) {
      for (const part of models.get(id)!.parts) {
        const material = partMaterial(id, part, finish);
        batch.add(part.geometry, material, matrix, casts && material !== GREENHOUSE_GLASS);
      }
    }
    for (const p of this.prims) batch.add(p.geometry, p.material, p.matrix, casts && p.shadow);
  }

  /** As instanced meshes instead, one per mesh of each model and finish (small sets, e.g. a render of La Mula). */
  build(shadows = true): THREE.Group {
    const g = new THREE.Group();
    const groups = new Map<string, { geometry: THREE.BufferGeometry; material: THREE.Material; shadow: boolean; matrices: THREE.Matrix4[] }>();
    const add = (geometry: THREE.BufferGeometry, material: THREE.Material, shadow: boolean, matrix: THREE.Matrix4) => {
      const key = `${geometry.uuid}|${material.uuid}|${shadow}`;
      let e = groups.get(key);
      if (!e) groups.set(key, (e = { geometry, material, shadow, matrices: [] }));
      e.matrices.push(matrix);
    };
    for (const { id, finish, matrix } of this.placed) for (const part of models.get(id)!.parts) add(part.geometry, partMaterial(id, part, finish), true, matrix);
    for (const p of this.prims) add(p.geometry, p.material, p.shadow, p.matrix);
    for (const { geometry, material, shadow, matrices } of groups.values()) {
      const mesh = new THREE.InstancedMesh(geometry, material, matrices.length);
      matrices.forEach((m, i) => mesh.setMatrixAt(i, m));
      mesh.instanceMatrix.needsUpdate = true;
      mesh.computeBoundingSphere();
      mesh.castShadow = shadow && shadows;
      mesh.receiveShadow = true;
      g.add(mesh);
    }
    return g;
  }
}

/** One model as plain meshes (it moves on its own: a door, a wheel, a radar), sharing the kit's geometry and materials. */
export function kitMesh(id: BaseModelId, finish: Finish = 'snow', only?: (part: string) => boolean): THREE.Group {
  const g = new THREE.Group();
  for (const part of models.get(id)?.parts ?? []) {
    if (only && !only(part.name)) continue;
    const mesh = new THREE.Mesh(part.geometry, partMaterial(id, part, finish));
    mesh.name = part.name;
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    g.add(mesh);
  }
  return g;
}
