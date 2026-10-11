import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { GTAOPass } from 'three/addons/postprocessing/GTAOPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';

// Post-processing of the voxel art (M5), shared by the battle renderer and the
// strategy stage: ambient occlusion in corners and contacts, a soft bloom on
// lights and glowing voxels, tone mapping and a gentle colour grade.

/**
 * Ambient occlusion that leaves overlays and effects (transparent, additive or
 * line geometry: movement range, paths, tracers, glows) out of its depth and
 * normal pre-pass, so they neither cast nor receive occlusion.
 */
export class SolidGTAO extends GTAOPass {
  /**
   * Occlusion is soft by nature: at half resolution it costs a quarter and
   * looks the same once denoised and blended over the full-size image.
   */
  override setSize(width: number, height: number): void {
    super.setSize(Math.max(1, Math.round(width / 2)), Math.max(1, Math.round(height / 2)));
  }

  override render(...args: Parameters<GTAOPass['render']>): void {
    const hidden: THREE.Object3D[] = [];
    this.scene.traverseVisible((o) => {
      if (o instanceof THREE.Line || o instanceof THREE.Points || o instanceof THREE.Sprite) hidden.push(o);
      else if (o instanceof THREE.Mesh) {
        const m = Array.isArray(o.material) ? o.material[0] : o.material;
        if (m && (m.transparent || m.blending === THREE.AdditiveBlending)) hidden.push(o);
      }
    });
    for (const o of hidden) o.visible = false;
    super.render(...args);
    for (const o of hidden) o.visible = true;
  }
}

/**
 * Bloom is a wide blur: at a quarter of the pixels it looks the same and, on a
 * high-density screen, saves a couple of milliseconds a frame.
 */
class SoftBloomPass extends UnrealBloomPass {
  override setSize(width: number, height: number): void {
    super.setSize(Math.max(1, Math.round(width / 2)), Math.max(1, Math.round(height / 2)));
  }
}

/** Gentle colour grade after tone mapping: a little saturation and contrast, a soft vignette. */
const GRADE_GLSL = /* glsl */ `
  float luma = dot(gl_FragColor.rgb, vec3(0.299, 0.587, 0.114));
  gl_FragColor.rgb = mix(vec3(luma), gl_FragColor.rgb, saturation);
  gl_FragColor.rgb = (gl_FragColor.rgb - 0.5) * contrast + 0.5;
  vec2 d = vUv - 0.5;
  gl_FragColor.rgb *= 1.0 - vignette * dot(d, d) * 2.0;`;

/**
 * Tone mapping and colour space (three's OutputPass) followed by the grade, in
 * one full-screen pass instead of two: each pass reads and writes every pixel.
 */
class GradedOutputPass extends OutputPass {
  constructor() {
    super();
    Object.assign(this.uniforms, { saturation: { value: 1.08 }, contrast: { value: 1.04 }, vignette: { value: 0.22 } });
    this.material.fragmentShader = this.material.fragmentShader
      .replace('uniform sampler2D tDiffuse;', 'uniform sampler2D tDiffuse;\nuniform float saturation;\nuniform float contrast;\nuniform float vignette;')
      .replace(/#ifdef SRGB_TRANSFER[\s\S]*?#endif/, (block) => block + GRADE_GLSL);
  }
}

export interface VoxelPost {
  composer: EffectComposer;
  /** Scene pass: point it at another scene to render a different set. */
  scene: RenderPass;
  ao: SolidGTAO;
}

/**
 * MSAA on the post chain pays off on standard screens, but on high-density
 * ones (Retina) the pixels are already tiny and it was the costliest step:
 * at 2x it took most of a 29 ms frame. Call after every pixel-ratio change.
 */
export function fitPostToPixelRatio(post: VoxelPost, pixelRatio: number): void {
  const samples = pixelRatio >= 1.5 ? 0 : 4;
  for (const target of [post.composer.renderTarget1, post.composer.renderTarget2]) {
    if (target.samples === samples) continue;
    target.samples = samples;
    target.dispose();
  }
}

/** Builds the voxel post chain on `gl`, rendering `scene` through `camera`. */
export function voxelPost(gl: THREE.WebGLRenderer, scene: THREE.Scene, camera: THREE.Camera): VoxelPost {
  // HDR target with MSAA, so voxel edges stay clean through the passes.
  const composer = new EffectComposer(gl, new THREE.WebGLRenderTarget(4, 4, { type: THREE.HalfFloatType, samples: 4 }));
  const pass = new RenderPass(scene, camera);
  composer.addPass(pass);
  const ao = new SolidGTAO(scene, camera, 4, 4);
  ao.updateGtaoMaterial({ radius: 0.35, distanceExponent: 1.5, thickness: 2, scale: 1, samples: 8 });
  // Half the denoise taps: the occlusion is already soft at half resolution.
  ao.updatePdMaterial({ samples: 8 });
  ao.blendIntensity = 1;
  composer.addPass(ao);
  composer.addPass(new SoftBloomPass(new THREE.Vector2(256, 256), 0.45, 0.4, 0.9));
  composer.addPass(new GradedOutputPass());
  return { composer, scene: pass, ao };
}
