import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { GTAOPass } from 'three/addons/postprocessing/GTAOPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { FXAAShader } from 'three/addons/shaders/FXAAShader.js';
import { AO_PASS } from './fogOfWar';

// Post-processing, shared by the battle renderer and the strategy stage:
// ambient occlusion in corners and contacts, a soft bloom on lights and
// glowing things, tone mapping and a gentle colour grade.

/**
 * The render layer of characters (soldiers, zombies, survivors): the battle
 * camera sees it and the sun's shadows follow that camera, but the occlusion's
 * pre-pass leaves it out, so characters add no draw calls to that pass.
 */
export const UNIT_LAYER = 1;

/**
 * Ambient occlusion that leaves overlays and effects (transparent, additive or
 * line geometry: movement range, paths, tracers, glows) out of its depth and
 * normal pre-pass, so they neither cast nor receive occlusion, and characters
 * too (UNIT_LAYER). The map's own materials draw that pass themselves
 * (AO_PASS, fogOfWar.ts), leaving out what is unexplored and the roofs and
 * walls the cutaway has taken away. The pre-pass doesn't redraw the shadow
 * maps: the scene pass before it has just drawn them.
 */
export class SolidGTAO extends GTAOPass {
  /** At a third of the size instead of half (lightenPost). */
  light?: boolean;
  /**
   * Characters in the pre-pass too. Left out, the occlusion behind a
   * character is multiplied over it and it looks see-through; the battle
   * leaves them out all the same, for the pass's cost with a crowd, and the
   * base's stage (a handful, close up) puts them in.
   */
  withUnits = false;

  /**
   * Occlusion is soft by nature: at half resolution it costs a quarter and
   * looks the same once denoised and blended over the full-size image.
   */
  override setSize(width: number, height: number): void {
    const k = this.light ? 3 : 2;
    super.setSize(Math.max(1, Math.round(width / k)), Math.max(1, Math.round(height / k)));
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
    const shadows = args[0].shadowMap;
    const [auto, needs] = [shadows.autoUpdate, shadows.needsUpdate];
    const units = this.camera.layers.isEnabled(UNIT_LAYER);
    shadows.autoUpdate = shadows.needsUpdate = false;
    if (!this.withUnits) this.camera.layers.disable(UNIT_LAYER);
    AO_PASS.value = 1;
    try {
      super.render(...args);
    } finally {
      AO_PASS.value = 0;
      [shadows.autoUpdate, shadows.needsUpdate] = [auto, needs];
      if (units) this.camera.layers.enable(UNIT_LAYER);
    }
    for (const o of hidden) o.visible = true;
  }
}

/**
 * Bloom is a wide blur: at a quarter of the pixels it looks the same and, on a
 * high-density screen, saves a couple of milliseconds a frame.
 */
class SoftBloomPass extends UnrealBloomPass {
  /** At a quarter of the size instead of half (lightenPost). */
  light?: boolean;

  override setSize(width: number, height: number): void {
    const k = this.light ? 4 : 2;
    super.setSize(Math.max(1, Math.round(width / k)), Math.max(1, Math.round(height / k)));
  }
}

/** The grade's controls, as uniforms. */
export interface Grade {
  saturation: { value: number };
  contrast: { value: number };
  vignette: { value: number };
  /** Split toning: the shade multiplied by one colour, the light by another (Synty's captures: lavender shade, warm light). */
  shadowTint: { value: THREE.Color };
  highlightTint: { value: THREE.Color };
  /** Blacks lifted to this colour: the captures' soft, pastel shade. */
  lift: { value: THREE.Color };
}

/** Colour grade after tone mapping: saturation, contrast, split toning, lifted blacks and a soft vignette. */
const GRADE_GLSL = /* glsl */ `
  float luma = dot(gl_FragColor.rgb, vec3(0.299, 0.587, 0.114));
  gl_FragColor.rgb = mix(vec3(luma), gl_FragColor.rgb, saturation);
  gl_FragColor.rgb = (gl_FragColor.rgb - 0.5) * contrast + 0.5;
  gl_FragColor.rgb *= mix(shadowTint, highlightTint, smoothstep(0.12, 0.7, luma));
  gl_FragColor.rgb = lift + gl_FragColor.rgb * (1.0 - lift);
  vec2 d = vUv - 0.5;
  gl_FragColor.rgb *= 1.0 - vignette * dot(d, d) * 2.0;`;

/**
 * Tone mapping and colour space (three's OutputPass) followed by the grade, in
 * one full-screen pass instead of two: each pass reads and writes every pixel.
 */
class GradedOutputPass extends OutputPass {
  constructor() {
    super();
    Object.assign(this.uniforms, {
      saturation: { value: 1.08 },
      contrast: { value: 1.04 },
      vignette: { value: 0.22 },
      shadowTint: { value: new THREE.Color(1, 1, 1) },
      highlightTint: { value: new THREE.Color(1, 1, 1) },
      lift: { value: new THREE.Color(0, 0, 0) },
    });
    this.material.fragmentShader = this.material.fragmentShader
      .replace(
        'uniform sampler2D tDiffuse;',
        'uniform sampler2D tDiffuse;\nuniform float saturation;\nuniform float contrast;\nuniform float vignette;\nuniform vec3 shadowTint;\nuniform vec3 highlightTint;\nuniform vec3 lift;',
      )
      .replace(/#ifdef SRGB_TRANSFER[\s\S]*?#endif/, (block) => block + GRADE_GLSL);
  }
}

export interface PostChain {
  composer: EffectComposer;
  /** Scene pass: point it at another scene to render a different set. */
  scene: RenderPass;
  ao: SolidGTAO;
  bloom: UnrealBloomPass;
  /** The colour grade after tone mapping (each view sets its own: renderer.ts). */
  grade: Grade;
  /** Edge smoothing after the grade, on the phone's effects instead of the HDR target's MSAA (fitPostToPixelRatio). */
  fxaa: ShaderPass;
}

/** The occlusion's samples, as it comes and lightened. */
const AO_SAMPLES = 8;
const AO_SAMPLES_LIGHT = 4;

/**
 * Lighter post-processing for a machine that can't keep up (the frame-rate
 * guard, settings.ts): the occlusion at a third of the size with half the
 * samples, the bloom at a quarter. Resize the composer after, so the passes
 * take their new size.
 */
export function lightenPost(post: PostChain, ao: boolean, bloom: boolean): void {
  post.ao.light = ao;
  const samples = ao ? AO_SAMPLES_LIGHT : AO_SAMPLES;
  post.ao.updateGtaoMaterial({ samples });
  post.ao.updatePdMaterial({ samples });
  (post.bloom as SoftBloomPass).light = bloom;
}

/**
 * MSAA on the post chain pays off on standard screens, but on high-density
 * ones (Retina) the pixels are already tiny and it was the costliest step:
 * at 2x it took most of a 29 ms frame. On the phone's effects (`fxaa`, the
 * drawing buffer's size: docs/FASE11.md §5) FXAA after the grade smooths
 * the edges instead: the HDR target's MSAA was 40 % of the frame there, FXAA
 * costs next to nothing. Call after every pixel-ratio change.
 */
export function fitPostToPixelRatio(post: PostChain, pixelRatio: number, fxaa?: { width: number; height: number }): void {
  post.fxaa.enabled = !!fxaa;
  if (fxaa) (post.fxaa.material.uniforms['resolution']!.value as THREE.Vector2).set(1 / Math.max(1, fxaa.width), 1 / Math.max(1, fxaa.height));
  const samples = fxaa || pixelRatio >= 1.5 ? 0 : 4;
  for (const target of [post.composer.renderTarget1, post.composer.renderTarget2]) {
    if (target.samples === samples) continue;
    target.samples = samples;
    target.dispose();
  }
}

/** Builds the post chain on `gl`, rendering `scene` through `camera`. */
export function postChain(gl: THREE.WebGLRenderer, scene: THREE.Scene, camera: THREE.Camera): PostChain {
  // HDR target with MSAA, so edges stay clean through the passes.
  const composer = new EffectComposer(gl, new THREE.WebGLRenderTarget(4, 4, { type: THREE.HalfFloatType, samples: 4 }));
  const pass = new RenderPass(scene, camera);
  composer.addPass(pass);
  const ao = new SolidGTAO(scene, camera, 4, 4);
  // Wider than before: Synty's captures sit everything in a soft dark (the ground's own is baked: groundAO.ts).
  ao.updateGtaoMaterial({ radius: 0.6, distanceExponent: 1.5, thickness: 2, scale: 1.1, samples: AO_SAMPLES });
  // Half the denoise taps: the occlusion is already soft at half resolution.
  ao.updatePdMaterial({ samples: AO_SAMPLES });
  ao.blendIntensity = 1;
  composer.addPass(ao);
  const bloom = new SoftBloomPass(new THREE.Vector2(256, 256), 0.45, 0.4, 0.9);
  composer.addPass(bloom);
  const output = new GradedOutputPass();
  composer.addPass(output);
  const fxaa = new ShaderPass(FXAAShader);
  fxaa.enabled = false;
  composer.addPass(fxaa);
  const u = output.uniforms as unknown as Grade;
  return {
    composer,
    scene: pass,
    ao,
    bloom,
    fxaa,
    grade: { saturation: u.saturation, contrast: u.contrast, vignette: u.vignette, shadowTint: u.shadowTint, highlightTint: u.highlightTint, lift: u.lift },
  };
}
