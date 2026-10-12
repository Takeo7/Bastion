import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';
import { ATLASES, atlasFile, modelFile, type AtlasId, type ModelKind, type PackId } from './syntyManifest';

// Synty models at run time: the converted GLBs (tools/synty) carry geometry
// and UVs only, with each material named after its pack's atlas. Every model
// of a pack shares that one texture and, unless it needs its own (a unit's
// dyes), one material. Models load once and are cloned.

const loader = new GLTFLoader().setMeshoptDecoder(MeshoptDecoder);
const url = (path: string) => `${import.meta.env.BASE_URL}${path}`;

// ----------------------------------------------------------------- atlases

const textures = new Map<AtlasId, THREE.Texture>();

/** A pack's atlas; usable at once, its image arrives when loaded. */
export function atlasTexture(id: AtlasId): THREE.Texture {
  let t = textures.get(id);
  if (!t) {
    t = new THREE.TextureLoader().load(url(atlasFile(id)));
    // glTF UVs start at the top left.
    t.flipY = false;
    t.colorSpace = THREE.SRGBColorSpace;
    t.anisotropy = 4;
    textures.set(id, t);
  }
  return t;
}

const isAtlas = (name: string): name is AtlasId => name in ATLASES;

const shared = new Map<AtlasId, THREE.MeshStandardMaterial>();

/** The one plain material of an atlas, for everything that needs nothing of its own. */
export function atlasMaterial(id: AtlasId): THREE.MeshStandardMaterial {
  let m = shared.get(id);
  if (!m) {
    m = new THREE.MeshStandardMaterial({ map: atlasTexture(id), roughness: 0.82, metalness: 0 });
    m.name = id;
    shared.set(id, m);
  }
  return m;
}

/** The atlas a converted mesh was painted with (its material's name). */
export function atlasOf(mesh: THREE.Mesh): AtlasId {
  const name = (Array.isArray(mesh.material) ? mesh.material[0] : mesh.material)?.name ?? '';
  return isAtlas(name) ? name : 'apocalypse';
}

// ------------------------------------------------------------------ models

const loading = new Map<string, Promise<THREE.Group>>();
const loaded = new Map<string, THREE.Group>();

/**
 * A converted model, loaded once. Its meshes get their atlas's shared
 * material and cast shadows; callers clone it (SkeletonUtils.clone for the
 * skinned ones) and never change the original.
 */
export function loadModel(pack: PackId, kind: ModelKind, id: string): Promise<THREE.Group> {
  const path = modelFile(pack, kind, id);
  let p = loading.get(path);
  if (!p) {
    p = loader.loadAsync(url(path)).then((gltf) => {
      const scene = gltf.scene;
      scene.traverse((o) => {
        if (!(o instanceof THREE.Mesh)) return;
        o.material = atlasMaterial(atlasOf(o));
        o.castShadow = true;
        o.receiveShadow = true;
      });
      scene.updateMatrixWorld(true);
      loaded.set(path, scene);
      return scene;
    });
    p.catch((e: unknown) => console.warn(`Synty: ${path} did not load`, e));
    loading.set(path, p);
  }
  return p;
}

/** The model if it has loaded already, else undefined (and it starts loading). */
export function modelNow(pack: PackId, kind: ModelKind, id: string): THREE.Group | undefined {
  const m = loaded.get(modelFile(pack, kind, id));
  if (!m) void loadModel(pack, kind, id).catch(() => undefined);
  return m;
}

// -------------------------------------------------------------- unit dyes

/**
 * Per-unit colours on a character, read from the "part" UV map the converter
 * writes (u: the armour piece, v: whether the texel is cloth): the barracks'
 * helmet, torso, arms and legs colours dye their pieces, the owner's colour
 * paints a glowing band round both upper arms, and hits flash red. Skin,
 * hair, boots and belts keep their colours.
 */
export interface Dyes {
  /** 1 helmet, 2 torso, 3 arms, 4 legs, 5 the owner's band (0 unused). */
  dyeColors: { value: THREE.Color[] };
  /** How far each piece's cloth goes to its colour (helmet, torso, arms, legs; 0: as Synty painted it). */
  dyeAmount: { value: THREE.Vector4 };
  /** The owner's band: 0 none, 1 painted. */
  dyeBand: { value: number };
  dyeFlash: { value: number };
}

export type DyedMaterial = THREE.MeshStandardMaterial & { userData: { dyes: Dyes } };

/** A unit's own material on its pack's atlas (one program for all of them, uniforms apart). */
export function dyedMaterial(atlas: AtlasId): DyedMaterial {
  const uniforms: Dyes = {
    dyeColors: { value: Array.from({ length: 6 }, () => new THREE.Color(1, 1, 1)) },
    dyeAmount: { value: new THREE.Vector4(0, 0, 0, 0) },
    dyeBand: { value: 0 },
    dyeFlash: { value: 0 },
  };
  const m = new THREE.MeshStandardMaterial({ map: atlasTexture(atlas), roughness: 0.82, metalness: 0 }) as DyedMaterial;
  m.userData.dyes = uniforms;
  m.onBeforeCompile = (shader) => {
    Object.assign(shader.uniforms, uniforms);
    shader.vertexShader = shader.vertexShader
      .replace(
        '#include <common>',
        `#include <common>
        #ifndef USE_UV1
          attribute vec2 uv1;
        #endif
        varying vec2 vDyePart;`,
      )
      .replace('#include <uv_vertex>', '#include <uv_vertex>\nvDyePart = uv1;');
    shader.fragmentShader = shader.fragmentShader
      .replace(
        '#include <common>',
        `#include <common>
        varying vec2 vDyePart;
        uniform vec3 dyeColors[6];
        uniform vec4 dyeAmount;
        uniform float dyeBand;
        uniform float dyeFlash;`,
      )
      .replace(
        '#include <map_fragment>',
        `#include <map_fragment>
        // The part map: u is the piece (in eighths), v whether it is cloth (glTF flips v).
        int dyePart = int(vDyePart.x * 8.0);
        bool dyeCloth = vDyePart.y < 0.5;
        bool dyeBandHere = dyePart == 5 && dyeBand > 0.5;
        if (dyeBandHere) diffuseColor.rgb = dyeColors[5];
        else if (dyePart > 0 && dyePart < 5 && dyeCloth) {
          // Keep the cloth's shading, take the piece's colour.
          float dyeLum = dot(diffuseColor.rgb, vec3(0.2126, 0.7152, 0.0722));
          vec3 dyed = dyeColors[dyePart] * clamp(dyeLum / 0.14, 0.35, 1.6);
          // (No dynamic index into the vector: some drivers get it wrong.)
          float dyeK = dot(dyeAmount, vec4(equal(ivec4(dyePart - 1), ivec4(0, 1, 2, 3))));
          diffuseColor.rgb = mix(diffuseColor.rgb, dyed, dyeK);
        }`,
      )
      .replace(
        '#include <emissivemap_fragment>',
        `#include <emissivemap_fragment>
        if (dyeBandHere) totalEmissiveRadiance += dyeColors[5] * 0.9;
        totalEmissiveRadiance += vec3(1.0, 0.23, 0.16) * dyeFlash;`,
      );
  };
  m.customProgramCacheKey = () => 'synty-dye';
  return m;
}
