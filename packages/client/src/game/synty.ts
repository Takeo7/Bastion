import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';
import { NIGHT } from './night';
import { ATLASES, atlasFile, fxTextureFile, modelFile, type AtlasId, type FxTextureId, type ModelKind, type PackId } from './syntyManifest';

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

const fxTextures = new Map<FxTextureId, THREE.Texture>();

/** One of Particle FX's sprites (white on transparent, tinted by the effect); usable at once, its image arrives when loaded. */
export function fxTexture(id: FxTextureId): THREE.Texture {
  let t = fxTextures.get(id);
  if (!t) {
    t = new THREE.TextureLoader().load(url(fxTextureFile(id)));
    t.colorSpace = THREE.SRGBColorSpace;
    fxTextures.set(id, t);
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
  /** A skin tone of its own (1 in dyeSkinOn), for the one swatch Synty paints every face and hand with. */
  dyeSkin: { value: THREE.Color };
  dyeSkinOn: { value: number };
  /**
   * How much the night's cool rim lights this unit's edges (night.ts NIGHT, so
   * it comes and goes with the night and no shader is rebuilt): 1 the squad,
   * a trace for the dead and the Cuervos, who should stay darker.
   */
  dyeRim: { value: number };
  /**
   * How much of the unit is drawn (1 all of it): coming into sight it fades in (playtest 20/10 #10: the dead
   * popped up on their tiles), dithered, the material still opaque: a fragment shows where a 4 × 4 pattern is under it.
   */
  dyeFade: { value: number };
}

export type DyedMaterial = THREE.MeshStandardMaterial & { userData: { dyes: Dyes } };

/** A unit's own material on its pack's atlas (one program for all of them, uniforms apart). */
export function dyedMaterial(atlas: AtlasId): DyedMaterial {
  const uniforms: Dyes = {
    dyeColors: { value: Array.from({ length: 6 }, () => new THREE.Color(1, 1, 1)) },
    dyeAmount: { value: new THREE.Vector4(0, 0, 0, 0) },
    dyeBand: { value: 0 },
    dyeFlash: { value: 0 },
    dyeSkin: { value: new THREE.Color(1, 1, 1) },
    dyeSkinOn: { value: 0 },
    dyeRim: { value: 0 },
    dyeFade: { value: 1 },
  };
  const m = new THREE.MeshStandardMaterial({ map: atlasTexture(atlas), roughness: 0.82, metalness: 0 }) as DyedMaterial;
  m.userData.dyes = uniforms;
  m.onBeforeCompile = (shader) => {
    Object.assign(shader.uniforms, uniforms, { unitNight: NIGHT });
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
        uniform float dyeFlash;
        uniform vec3 dyeSkin;
        uniform float dyeSkinOn;
        uniform float dyeRim;
        uniform float dyeFade;
        uniform float unitNight;`,
      )
      .replace(
        '#include <map_fragment>',
        `#include <map_fragment>
        // Fading in: an ordered 4 × 4 pattern decides which fragments show yet.
        if (dyeFade < 0.999) {
          ivec2 dyeCell = ivec2(mod(gl_FragCoord.xy, 4.0));
          int dyeI = dyeCell.x + dyeCell.y * 4;
          float dyeOrder = float(dyeI == 0 ? 0 : dyeI == 1 ? 8 : dyeI == 2 ? 2 : dyeI == 3 ? 10 : dyeI == 4 ? 12 : dyeI == 5 ? 4 : dyeI == 6 ? 14 : dyeI == 7 ? 6 : dyeI == 8 ? 3 : dyeI == 9 ? 11 : dyeI == 10 ? 1 : dyeI == 11 ? 9 : dyeI == 12 ? 15 : dyeI == 13 ? 7 : dyeI == 14 ? 13 : 5);
          if ((dyeOrder + 0.5) / 16.0 > dyeFade) discard;
        }
        // The part map: u is the piece (in eighths), v whether it is cloth (glTF flips v).
        int dyePart = int(vDyePart.x * 8.0);
        bool dyeCloth = vDyePart.y < 0.5;
        bool dyeBandHere = dyePart == 5 && dyeBand > 0.5;
        // Synty's skin swatch (255, 204, 174), and the texels a distant mip blends it into.
        if (dyeSkinOn > 0.5) diffuseColor.rgb = mix(diffuseColor.rgb, dyeSkin, 1.0 - smoothstep(0.05, 0.14, distance(diffuseColor.rgb, vec3(1.0, 0.604, 0.423))));
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
        if (dyeBandHere) totalEmissiveRadiance += dyeColors[5] * (0.9 + 1.1 * unitNight * dyeRim);
        // By night a cool moonlit rim on the edges turning away from the camera, and a breath of the
        // unit's own colours, so the squad reads in the dark without looking lit by day.
        float nightK = unitNight * dyeRim;
        if (nightK > 0.0) {
          float rimFacing = 1.0 - clamp(dot(normal, normalize(vViewPosition)), 0.0, 1.0);
          totalEmissiveRadiance += vec3(0.42, 0.62, 0.8) * pow(rimFacing, 3.0) * 0.5 * nightK;
          totalEmissiveRadiance += diffuseColor.rgb * 0.07 * nightK;
        }
        totalEmissiveRadiance += vec3(1.0, 0.23, 0.16) * dyeFlash;`,
      );
  };
  m.customProgramCacheKey = () => 'synty-dye';
  return m;
}

// -------------------------------------------------------------- animations

/** A converted clip: the animation pack's skeleton (no meshes) and the clip that moves it. */
export interface SyntyClip {
  scene: THREE.Group;
  clip: THREE.AnimationClip;
}

const clips = new Map<string, Promise<SyntyClip>>();

/** An animation clip (tools/synty, kind 'anim'), loaded once and shared. */
export function loadAnimation(pack: PackId, id: string): Promise<SyntyClip> {
  const path = modelFile(pack, 'anim', id);
  let p = clips.get(path);
  if (!p) {
    p = loader.loadAsync(url(path)).then((gltf) => ({ scene: gltf.scene, clip: gltf.animations[0]! }));
    p.catch((e: unknown) => console.warn(`Synty: ${path} did not load`, e));
    clips.set(path, p);
  }
  return p;
}
