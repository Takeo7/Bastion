import * as THREE from 'three';
import type { Dirty } from './batch';
import { extendShader } from './fogOfWar';

// Contact occlusion baked into a texture over the map (reference block:
// Synty's captures sit every car, wall and pile of rubbish in its own soft
// dark; screen-space occlusion only runs on «alto»). Once per map, and again
// when it changes, the GPU draws the height of everything solid seen from
// above, then works out for each texel how much of the sky the things round
// it hide (horizon-based, as GTAO but on the height field). The ground and the
// floors read it with one texture lookup: it costs nothing a frame, on every
// preset. Poles, wires and canopies are left out of the height field: seen
// from above they'd stand in for the ground under them.

/** Texels per tile of the occlusion (and of the height field it comes from). */
const TEXELS = 6;
/** How far round a texel it looks for what hides the sky (tiles), and in how many directions and steps. */
const RADIUS = 2.0;
const DIRECTIONS = 12;
const STEPS = 8;

const HEIGHT_VERTEX = /* glsl */ `
  varying float vHeight;
  void main() {
    vec4 world = modelMatrix * vec4(position, 1.0);
    vHeight = world.y;
    gl_Position = projectionMatrix * viewMatrix * world;
  }`;

const HEIGHT_FRAGMENT = /* glsl */ `
  varying float vHeight;
  void main() {
    gl_FragColor = vec4(vHeight, 0.0, 0.0, 1.0);
  }`;

const AO_FRAGMENT = /* glsl */ `
  uniform sampler2D tHeight;
  uniform vec2 mapSize;
  varying vec2 vUv;
  float hash(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
  }
  void main() {
    float h0 = texture2D(tHeight, vUv).r;
    float spin = hash(gl_FragCoord.xy) * 6.2831853 / ${DIRECTIONS}.0;
    float hidden = 0.0;
    for (int d = 0; d < ${DIRECTIONS}; d++) {
      float a = spin + float(d) * 6.2831853 / ${DIRECTIONS}.0;
      vec2 dir = vec2(cos(a), sin(a));
      float horizon = 0.0;
      for (int s = 1; s <= ${STEPS}; s++) {
        float dist = float(s) * ${(RADIUS / STEPS).toFixed(4)};
        float h = texture2D(tHeight, vUv + dir * dist / mapSize).r;
        float dh = h - h0;
        // The sine of the angle up to it, fading with distance.
        float fall = dist / ${(RADIUS * 1.05).toFixed(3)};
        horizon = max(horizon, dh / sqrt(dh * dh + dist * dist) * (1.0 - fall * fall));
      }
      hidden += horizon;
    }
    gl_FragColor = vec4(pow(1.0 - hidden / ${DIRECTIONS}.0, 2.4), h0, 0.0, 1.0);
  }`;

/**
 * The bake's two passes, made once and kept (playtest 21/10 #5): made and thrown away at every bake, their programs
 * went with them and compiled again at each change of the map (a door, a car blown up), a stall on Windows.
 */
let passes: { height: THREE.ShaderMaterial; ao: THREE.ShaderMaterial; quad: THREE.Mesh; quadScene: THREE.Scene } | null = null;
function bakePasses(): NonNullable<typeof passes> {
  if (passes) return passes;
  const height = new THREE.ShaderMaterial({ vertexShader: HEIGHT_VERTEX, fragmentShader: HEIGHT_FRAGMENT, side: THREE.DoubleSide });
  const ao = new THREE.ShaderMaterial({
    uniforms: { tHeight: { value: null as THREE.Texture | null }, mapSize: { value: new THREE.Vector2(1, 1) } },
    vertexShader: 'varying vec2 vUv; void main() { vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }',
    fragmentShader: AO_FRAGMENT,
    depthTest: false,
    depthWrite: false,
  });
  const quad = new THREE.Mesh(new THREE.PlaneGeometry(2, 2), ao);
  return (passes = { height, ao, quad, quadScene: new THREE.Scene().add(quad) });
}

/** The baked contact occlusion of one map. */
export class GroundOcclusion {
  /** r: how much sky each point of the ground sees (1 open, 0 hidden); g: the height there. */
  readonly target: THREE.WebGLRenderTarget;
  readonly uniforms = {
    tGroundAO: { value: null as THREE.Texture | null },
    groundAOSize: { value: new THREE.Vector2(1, 1) },
  };
  private readonly heights: THREE.WebGLRenderTarget;

  constructor(
    private readonly width: number,
    private readonly height: number,
  ) {
    const w = width * TEXELS;
    const h = height * TEXELS;
    this.heights = new THREE.WebGLRenderTarget(w, h, { type: THREE.HalfFloatType, minFilter: THREE.NearestFilter, magFilter: THREE.NearestFilter, depthBuffer: true });
    this.target = new THREE.WebGLRenderTarget(w, h, { type: THREE.UnsignedByteType, minFilter: THREE.LinearFilter, magFilter: THREE.LinearFilter, depthBuffer: false });
    this.uniforms.tGroundAO.value = this.target.texture;
    this.uniforms.groundAOSize.value.set(width, height);
  }

  /**
   * Draws the height field of `occluders` (merged, placed geometry) and works
   * the occlusion out of it, in `region` only (tiles; what changed): the rest
   * of both textures stays as it was.
   */
  bake(gl: THREE.WebGLRenderer, occluders: THREE.BufferGeometry[], region: Dirty): void {
    const { width, height } = this;
    // In texels, the texture's rows counted up from z = height. Heights a tile beyond (what stands at the edge
    // overhangs it), occlusion as far again as it looks.
    const rect = (pad: number) => {
      const x0 = Math.max(0, region.x0 - pad);
      const x1 = Math.min(width, region.x1 + pad);
      const z0 = Math.max(0, region.y0 - pad);
      const z1 = Math.min(height, region.y1 + pad);
      return new THREE.Vector4(Math.floor(x0 * TEXELS), Math.floor((height - z1) * TEXELS), Math.ceil((x1 - x0) * TEXELS), Math.ceil((z1 - z0) * TEXELS));
    };
    this.heights.scissor.copy(rect(1));
    this.heights.scissorTest = true;
    this.target.scissor.copy(rect(1 + Math.ceil(RADIUS)));
    this.target.scissorTest = true;
    const pass = bakePasses();
    const scene = new THREE.Scene();
    for (const g of occluders) scene.add(new THREE.Mesh(g, pass.height));
    // From above, looking down: x across, z down the texture (v = 1 − z / height).
    const camera = new THREE.OrthographicCamera(-width / 2, width / 2, height / 2, -height / 2, 0.1, 200);
    camera.up.set(0, 0, -1);
    camera.position.set(width / 2, 100, height / 2);
    camera.lookAt(width / 2, 0, height / 2);
    camera.updateMatrixWorld();

    const before = gl.getRenderTarget();
    const clear = gl.getClearColor(new THREE.Color());
    const alpha = gl.getClearAlpha();
    const autoClear = gl.autoClear;
    const shadows = gl.shadowMap.autoUpdate;
    gl.shadowMap.autoUpdate = false;
    gl.autoClear = true;
    gl.setClearColor(0x000000, 1);
    gl.setRenderTarget(this.heights);
    gl.render(scene, camera);

    pass.ao.uniforms.tHeight!.value = this.heights.texture;
    (pass.ao.uniforms.mapSize!.value as THREE.Vector2).set(width, height);
    gl.setRenderTarget(this.target);
    gl.render(pass.quadScene, camera);

    gl.setRenderTarget(before);
    gl.setClearColor(clear, alpha);
    gl.autoClear = autoClear;
    gl.shadowMap.autoUpdate = shadows;
  }

  dispose(): void {
    this.heights.dispose();
    this.target.dispose();
  }
}

/**
 * Makes a material darken by the baked occlusion where its surfaces lie on
 * the ground or on a floor (`strength`: how dark the most hidden point gets).
 * Faces looking up only: walls get theirs from the screen-space pass.
 */
export function contactPatch(material: THREE.Material, ao: GroundOcclusion, strength = 0.95): void {
  extendShader(material, 'contact-ao', (shader) => {
    Object.assign(shader.uniforms, ao.uniforms);
    shader.vertexShader = shader.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 vCaoPos;\nvarying float vCaoUp;').replace(
      '#include <project_vertex>',
      `#include <project_vertex>
      {
        vec4 caoWorld = vec4(transformed, 1.0);
        vec3 caoNormal = objectNormal;
        #ifdef USE_INSTANCING
          caoWorld = instanceMatrix * caoWorld;
          caoNormal = mat3(instanceMatrix) * caoNormal;
        #endif
        vCaoPos = (modelMatrix * caoWorld).xyz;
        vCaoUp = normalize(mat3(modelMatrix) * caoNormal).y;
      }`,
    );
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', '#include <common>\nuniform sampler2D tGroundAO;\nuniform vec2 groundAOSize;\nvarying vec3 vCaoPos;\nvarying float vCaoUp;')
      .replace(
        '#include <aomap_fragment>',
        `#include <aomap_fragment>
        if (vCaoUp > 0.7) {
          vec4 cao = texture2D(tGroundAO, vec2(vCaoPos.x / groundAOSize.x, 1.0 - vCaoPos.z / groundAOSize.y));
          // Only where this surface is the one the occlusion was worked out for (not a roof over the ground).
          float here = 1.0 - smoothstep(0.15, 0.6, abs(vCaoPos.y - cao.g));
          float occlusion = mix(1.0, cao.r, ${strength.toFixed(2)} * here);
          reflectedLight.indirectDiffuse *= occlusion;
          reflectedLight.directDiffuse *= mix(1.0, occlusion, 0.85);
        }`,
      );
  });
}
