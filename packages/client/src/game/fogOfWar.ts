import * as THREE from 'three';

// Fog of war on the map's materials (second playtest: «que las zonas que aún
// no hemos descubierto no se pudieran ver»). Each tile keeps two values in a
// small texture, how much it is in sight now and how much it has been
// explored, and every material of the map samples it at the world position
// of each fragment, stepped like the voxels. So the fog fades as soldiers
// move, with no per-instance work, and covers whatever stands on a tile:
//  - never seen: a dark mist drifting slowly, where only the tallest shapes
//    show as the faintest silhouettes;
//  - seen before: the remembered look, darker and cooler;
//  - in sight: as it is.
// Beyond the map's edge the ground fades into the same mist.

/** Seconds a tile takes to come into sight or leave it, and to come out of the unexplored dark. */
const SIGHT_FADE = 0.35;
const EXPLORE_FADE = 0.9;

/** Seen before, not in sight now: darker and towards a cool slate, so in daylight it reads as "not seen", not as mud. */
const REMEMBERED_TINT = new THREE.Color(0x26303c);
/** Never seen. */
const UNKNOWN = new THREE.Color(0x161e28);

/** Shared GLSL: a value noise for the mist. */
const NOISE_GLSL = /* glsl */ `
  float fowHash(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
  }
  float fowNoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(fowHash(i), fowHash(i + vec2(1.0, 0.0)), f.x), mix(fowHash(i + vec2(0.0, 1.0)), fowHash(i + vec2(1.0, 1.0)), f.x), f.y);
  }`;

/**
 * Adds a shader patch to a material, after whatever patch it already has
 * (the voxel surface, the ground), keeping their program cache keys apart.
 */
export function extendShader(material: THREE.Material, key: string, patch: (shader: THREE.WebGLProgramParametersWithUniforms) => void): void {
  const before = material.onBeforeCompile;
  const beforeKey = material.customProgramCacheKey;
  material.onBeforeCompile = (shader, renderer) => {
    before.call(material, shader, renderer);
    patch(shader);
  };
  material.customProgramCacheKey = () => `${beforeKey.call(material)}|${key}`;
}

/** The fog of war of one map: a texture of tiles in sight and explored, faded over time. */
export class FogOfWar {
  readonly uniforms = {
    fowMap: { value: new THREE.DataTexture(new Uint8Array([255, 255, 0, 255]), 1, 1) as THREE.DataTexture },
    fowSize: { value: new THREE.Vector2(1, 1) },
    fowTime: { value: 0 },
    fowRemembered: { value: REMEMBERED_TINT.clone() },
    fowUnknown: { value: UNKNOWN.clone() },
  };
  private size = 0;
  /** Shown now (0–1) and where each tile is heading (0 or 1). */
  private sight = new Float32Array(0);
  private known = new Float32Array(0);
  private sightGoal = new Uint8Array(0);
  private knownGoal = new Uint8Array(0);
  private settled = true;

  constructor() {
    this.uniforms.fowMap.value.needsUpdate = true;
  }

  /** A new map: everything starts unexplored until `set` says otherwise. */
  resize(width: number, height: number): void {
    const size = width * height;
    if (size === this.size && this.uniforms.fowSize.value.x === width) return;
    this.size = size;
    this.sight = new Float32Array(size);
    this.known = new Float32Array(size);
    this.sightGoal = new Uint8Array(size);
    this.knownGoal = new Uint8Array(size);
    this.uniforms.fowMap.value.dispose();
    const texture = new THREE.DataTexture(new Uint8Array(size * 4), width, height);
    texture.magFilter = THREE.LinearFilter;
    texture.minFilter = THREE.LinearFilter;
    texture.needsUpdate = true;
    this.uniforms.fowMap.value = texture;
    this.uniforms.fowSize.value.set(width, height);
  }

  /** Tiles in sight and tiles explored; `instant` skips the fade (loading a mission). */
  set(visible: readonly boolean[], explored: Uint8Array, instant = false): void {
    for (let i = 0; i < this.size; i++) {
      this.sightGoal[i] = visible[i] ? 1 : 0;
      this.knownGoal[i] = explored[i] || visible[i] ? 1 : 0;
      if (instant) {
        this.sight[i] = this.sightGoal[i]!;
        this.known[i] = this.knownGoal[i]!;
      }
    }
    this.settled = false;
    if (instant) this.update(0);
  }

  /** Moves every tile towards its goal and uploads the texture while anything is still fading. */
  update(dt: number): void {
    this.uniforms.fowTime.value += dt;
    if (this.settled) return;
    const sightStep = dt / SIGHT_FADE;
    const knownStep = dt / EXPLORE_FADE;
    const data = this.uniforms.fowMap.value.image.data as Uint8Array;
    let moving = false;
    for (let i = 0; i < this.size; i++) {
      const s = approach(this.sight[i]!, this.sightGoal[i]!, sightStep);
      const k = approach(this.known[i]!, this.knownGoal[i]!, knownStep);
      this.sight[i] = s;
      this.known[i] = k;
      if (s !== this.sightGoal[i] || k !== this.knownGoal[i]) moving = true;
      data[i * 4] = Math.round(s * 255);
      data[i * 4 + 1] = Math.round(k * 255);
    }
    this.uniforms.fowMap.value.needsUpdate = true;
    this.settled = !moving;
  }

  /** Makes a material of the map obey the fog; returns it. */
  patch<T extends THREE.Material>(material: T): T {
    extendShader(material, 'fog-of-war', (shader) => {
      Object.assign(shader.uniforms, this.uniforms);
      shader.vertexShader = shader.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 vFowPos;').replace(
        '#include <project_vertex>',
        `#include <project_vertex>
        {
          vec4 fowWorld = vec4(transformed, 1.0);
          vec3 fowNormal = normal;
          #ifdef USE_INSTANCING
            fowWorld = instanceMatrix * fowWorld;
            fowNormal = mat3(instanceMatrix) * fowNormal;
          #endif
          fowWorld = modelMatrix * fowWorld;
          fowNormal = normalize(mat3(modelMatrix) * fowNormal);
          // A wall's face belongs to the tile behind it, not to the street it looks onto.
          vFowPos = fowWorld.xyz - vec3(fowNormal.x, 0.0, fowNormal.z) * 0.3;
        }`,
      );
      shader.fragmentShader = shader.fragmentShader
        .replace(
          '#include <common>',
          `#include <common>
          varying vec3 vFowPos;
          uniform sampler2D fowMap;
          uniform vec2 fowSize;
          uniform float fowTime;
          uniform vec3 fowRemembered;
          uniform vec3 fowUnknown;
          ${NOISE_GLSL}`,
        )
        .replace(
          '#include <alphamap_fragment>',
          `// Fog of war, sampled per eighth of a tile so its edges step like the voxels.
          vec2 fowCell = (floor(vFowPos.xz * 8.0) + 0.5) / 8.0;
          vec4 fowTile = texture2D(fowMap, fowCell / fowSize);
          vec2 fowOut = max(max(-fowCell, fowCell - fowSize), 0.0);
          float fowInside = 1.0 - smoothstep(0.0, 1.6, length(fowOut));
          // A chunky mist (half-tile cells) drifting slowly; it also frays the edge of the unexplored.
          float fowMist = fowNoise((floor(vFowPos.xz * 2.0) + 0.5) * 0.22 + vec2(fowTime * 0.05, fowTime * 0.03));
          float fowSeen = smoothstep(0.3, 0.7, fowTile.r + (fowMist - 0.5) * 0.12) * fowInside;
          float fowKnown = smoothstep(0.22, 0.78, fowTile.g + (fowMist - 0.5) * 0.4) * fowInside;
          // Remembered: half as bright and towards the slate, as the per-instance tint it replaces.
          diffuseColor.rgb = mix(diffuseColor.rgb * 0.35 + fowRemembered * 0.3, diffuseColor.rgb, fowSeen);
          #include <alphamap_fragment>`,
        )
        .replace(
          '#include <fog_fragment>',
          `#include <fog_fragment>
          {
            // Unexplored: the mist, over the scene's haze (which would lift it to a grey where
            // everything reads), a little lighter higher up so tall shapes barely show.
            vec3 fowDark = fowUnknown * (0.55 + 1.1 * fowMist) * (1.0 + 0.15 * clamp(vFowPos.y / 5.0, 0.0, 1.0));
            gl_FragColor.rgb = mix(fowDark, gl_FragColor.rgb, fowKnown);
          }`,
        );
    });
    return material;
  }

  dispose(): void {
    this.uniforms.fowMap.value.dispose();
  }
}

function approach(value: number, goal: number, step: number): number {
  return value < goal ? Math.min(goal, value + step) : Math.max(goal, value - step);
}
