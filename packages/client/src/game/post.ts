import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { GTAOPass } from 'three/addons/postprocessing/GTAOPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
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

/** Gentle colour grade after tone mapping: a little saturation and contrast, a soft vignette. */
export const GRADE = {
  uniforms: { tDiffuse: { value: null }, saturation: { value: 1.08 }, contrast: { value: 1.04 }, vignette: { value: 0.22 } },
  vertexShader: /* glsl */ `
    varying vec2 vUv;
    void main() {
      vUv = uv;
      gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    }`,
  fragmentShader: /* glsl */ `
    uniform sampler2D tDiffuse;
    uniform float saturation;
    uniform float contrast;
    uniform float vignette;
    varying vec2 vUv;
    void main() {
      vec4 c = texture2D(tDiffuse, vUv);
      float luma = dot(c.rgb, vec3(0.299, 0.587, 0.114));
      c.rgb = mix(vec3(luma), c.rgb, saturation);
      c.rgb = (c.rgb - 0.5) * contrast + 0.5;
      vec2 d = vUv - 0.5;
      c.rgb *= 1.0 - vignette * dot(d, d) * 2.0;
      gl_FragColor = c;
    }`,
};

export interface VoxelPost {
  composer: EffectComposer;
  /** Scene pass: point it at another scene to render a different set. */
  scene: RenderPass;
  ao: SolidGTAO;
}

/** Builds the voxel post chain on `gl`, rendering `scene` through `camera`. */
export function voxelPost(gl: THREE.WebGLRenderer, scene: THREE.Scene, camera: THREE.Camera): VoxelPost {
  // HDR target with MSAA, so voxel edges stay clean through the passes.
  const composer = new EffectComposer(gl, new THREE.WebGLRenderTarget(4, 4, { type: THREE.HalfFloatType, samples: 4 }));
  const pass = new RenderPass(scene, camera);
  composer.addPass(pass);
  const ao = new SolidGTAO(scene, camera, 4, 4);
  ao.updateGtaoMaterial({ radius: 0.35, distanceExponent: 1.5, thickness: 2, scale: 1, samples: 16 });
  ao.blendIntensity = 1;
  composer.addPass(ao);
  composer.addPass(new UnrealBloomPass(new THREE.Vector2(256, 256), 0.45, 0.4, 0.9));
  composer.addPass(new OutputPass());
  composer.addPass(new ShaderPass(GRADE));
  return { composer, scene: pass, ao };
}
