import * as THREE from 'three';

// Fog of war on the map's materials (second playtest: «que las zonas que aún
// no hemos descubierto no se pudieran ver»). Each tile keeps two values in a
// small texture, how much it is in sight now and how much it has been
// explored, and every material of the map samples it at the world position
// of each fragment, blended between tiles. So the fog fades as soldiers
// move, with no per-instance work, and covers whatever stands on a tile:
//  - never seen: a dark mist drifting slowly, where only the tallest shapes
//    show as the faintest silhouettes;
//  - seen before: the remembered look, darker and cooler;
//  - in sight: as it is.
// Beyond the map's edge the ground fades into the same mist.
//
// The screen-space ambient occlusion (post.ts) draws the scene once more for
// its normals and depth. Map materials draw that pass themselves (AO_PASS),
// so what lies unexplored or cut away (buildings.ts) is left out of it too:
// no occlusion shading in the mist, no outline of a roof that has faded.

/** Seconds a tile takes to come into sight or leave it, and to come out of the unexplored dark. */
const SIGHT_FADE = 0.35;
const EXPLORE_FADE = 0.9;

/** Seen before, not in sight now: darker and towards a cool slate, so in daylight it reads as "not seen", not as mud. */
const REMEMBERED_TINT = new THREE.Color(0x26303c);
/** Never seen. */
const UNKNOWN = new THREE.Color(0x161e28);
/** Beyond the map's edge: the haze of the day (renderer.ts), so the map fades into distance, not into a void. */
const BEYOND_DAY = new THREE.Color(0x7a7380);
const BEYOND_NIGHT = new THREE.Color(0x0c1a1e);

/** 1 while the ambient occlusion draws its normals and depth (post.ts), else 0; shared by every map material. */
export const AO_PASS = { value: 0 };

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
 * (the ground, the buildings' cutaway), keeping their program cache keys apart.
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
    fowBeyond: { value: BEYOND_DAY.clone() },
  };

  /** Day or night beyond the map's edge. */
  setNight(on: boolean): void {
    this.uniforms.fowBeyond.value.copy(on ? BEYOND_NIGHT : BEYOND_DAY);
  }
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
    // It draws the occlusion's normals itself (see AO_PASS); unlit materials (glows) stay out of that pass.
    material.allowOverride = false;
    const lit = 'isMeshStandardMaterial' in material || 'isMeshLambertMaterial' in material || 'isMeshPhongMaterial' in material;
    extendShader(material, 'fog-of-war', (shader) => {
      Object.assign(shader.uniforms, this.uniforms, { fowAoPass: AO_PASS });
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
          uniform vec3 fowBeyond;
          uniform float fowAoPass;
          ${NOISE_GLSL}`,
        )
        .replace(
          '#include <alphamap_fragment>',
          `// Fog of war, blended between tile middles (the texture filters linearly): soft edges, no steps.
          vec2 fowCell = vFowPos.xz;
          vec4 fowTile = texture2D(fowMap, fowCell / fowSize);
          vec2 fowOut = max(max(-fowCell, fowCell - fowSize), 0.0);
          // Beyond the edge the town goes on for a few tiles (dressing.ts: dressEdges) and fades into the haze.
          float fowInside = 1.0 - smoothstep(0.0, 6.0, length(fowOut));
          // A mist drifting slowly; it also frays the edge of the unexplored.
          float fowMist = fowNoise(vFowPos.xz * 0.44 + vec2(fowTime * 0.05, fowTime * 0.03));
          float fowSeen = smoothstep(0.3, 0.7, fowTile.r + (fowMist - 0.5) * 0.12) * fowInside;
          float fowKnown = smoothstep(0.22, 0.78, fowTile.g + (fowMist - 0.5) * 0.4) * fowInside;
          // Remembered: half as bright and towards the slate, as the per-instance tint it replaces.
          diffuseColor.rgb = mix(diffuseColor.rgb * 0.35 + fowRemembered * 0.3, diffuseColor.rgb, fowSeen);
          ${lit ? '' : 'if (fowAoPass > 0.5) discard;'}
          #include <alphamap_fragment>`,
        )
        .replace(
          '#include <normal_fragment_maps>',
          `#include <normal_fragment_maps>
          // The occlusion's pass: the view-space normal, packed as MeshNormalMaterial does; nothing where unexplored.
          if (fowAoPass > 0.5) {
            if (fowKnown < 0.5) discard;
            gl_FragColor = vec4(normalize(normal) * 0.5 + 0.5, 1.0);
            return;
          }`,
        )
        .replace(
          '#include <fog_fragment>',
          `#include <fog_fragment>
          {
            // Unexplored: the mist, over the scene's haze (which would lift it to a grey where
            // everything reads), a little lighter higher up so tall shapes barely show.
            vec3 fowDark = fowUnknown * (0.55 + 1.1 * fowMist) * (1.0 + 0.15 * clamp(vFowPos.y / 5.0, 0.0, 1.0));
            // Beyond the map's edge, the haze instead.
            fowDark = mix(fowBeyond * (0.92 + 0.16 * fowMist), fowDark, fowInside);
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
