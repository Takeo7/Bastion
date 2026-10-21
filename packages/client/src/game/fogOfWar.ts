import * as THREE from 'three';
import { STOREY } from './coords';

// Fog of war on the map's materials, in three zones (Arturo, playtest 21/10 #8):
//  - in sight (where enemies show: teamVisibility, 18 tiles by day, 10 at
//    night unless lit): as it is;
//  - beyond, as far as the squad's line of sight goes (teamView, 40 tiles, day
//    or night): seen well, under a very light haze, a touch less colour, so it
//    reads as "no enemies show here";
//  - out of anyone's line of sight, seen before or not: a dark mist drifting
//    slowly, where only the tallest shapes show as the faintest silhouettes; so
//    are the map's outline (the frame round what is played) and what lies
//    beyond its edge.
// Each tile keeps both in a small texture, and every material of the map
// samples it at the world position of each fragment, blended between tiles.
// So the zones fade as soldiers move, with no per-instance work, and cover
// whatever stands on a tile.
//
// The screen-space ambient occlusion (post.ts) draws the scene once more for
// its normals and depth. Map materials draw that pass themselves (AO_PASS),
// so what lies under the mist or cut away (buildings.ts) is left out of it too:
// no occlusion shading in the mist, no outline of a roof that has faded.

/** Seconds a tile takes to come into sight or view or leave them; and to light up or go dark at night. */
const SIGHT_FADE = 0.35;
const LIT_FADE = 0.6;
/** The fog texture's alpha holds each tile's floor, this much per storey. */
const FLOOR_STEP = 32;
/**
 * Night: what lit tiles get, a lamp's warm light over their own colours, and the mist's darker tint. The
 * pools read by their colour against the teal night more than by their brightness: a deep orange, not too
 * much of it, and less where a light already makes the tile bright, so pale pavement doesn't burn white
 * (Arturo 17/10).
 */
const LAMP = new THREE.Color(1.0, 0.55, 0.22).multiplyScalar(0.6);
const UNKNOWN_NIGHT = new THREE.Color(0x070d11);

/** The mist over what is out of view, the map's outline and beyond its edge. */
const UNKNOWN = new THREE.Color(0x161e28);
/** The light haze over what is in view but out of sight, by day and by night. */
const HAZE = new THREE.Color(0xa9b8c8);
const HAZE_NIGHT = new THREE.Color(0x10202a);

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

/** The fog of war of one map: a texture of tiles in sight and on the map, faded over time. */
export class FogOfWar {
  readonly uniforms = {
    fowMap: { value: new THREE.DataTexture(new Uint8Array([255, 255, 0, 255]), 1, 1) as THREE.DataTexture },
    fowSize: { value: new THREE.Vector2(1, 1) },
    fowTime: { value: 0 },
    fowUnknown: { value: UNKNOWN.clone() },
    fowHaze: { value: HAZE.clone() },
    /** 1 by night: lit tiles (blue channel) glow warm. */
    fowNight: { value: 0 },
    fowLamp: { value: LAMP.clone() },
    /** How far beyond the map's edge (tiles) the dark begins and ends going into the haze (MapView.fadeBeyond). */
    fowFar: { value: new THREE.Vector2(24, 70) },
    /** 1: past the edge is as the edge is, lit (the base's clearing, MapView.litBeyond); 0, in the dark. */
    fowEdge: { value: 0 },
  };

  /** Day or night: the haze's and the mist's tints (beyond the map's edge too), and whether lit tiles glow. (Snow changes none.) */
  setNight(on: boolean, _snow = false): void {
    this.uniforms.fowUnknown.value.copy(on ? UNKNOWN_NIGHT : UNKNOWN);
    this.uniforms.fowHaze.value.copy(on ? HAZE_NIGHT : HAZE);
    this.uniforms.fowNight.value = on ? 1 : 0;
  }

  /** Each tile's floor (storeys up): light from the ground stays on what stands at that floor, not on a roof above it. */
  setFloors(elev: readonly number[]): void {
    const data = this.uniforms.fowMap.value.image.data as Uint8Array;
    for (let i = 0; i < this.size; i++) data[i * 4 + 3] = Math.min(255, (elev[i] ?? 0) * FLOOR_STEP);
    this.uniforms.fowMap.value.needsUpdate = true;
  }

  /**
   * Tiles lit at night (the engine's lit mask: street lights, a Bengala, a
   * fire, as far as their light reaches past walls); they light up and go dark
   * with a short fade.
   */
  setLit(mask: readonly boolean[]): void {
    for (let i = 0; i < this.size; i++) this.litGoal[i] = mask[i] ? 1 : 0;
    this.settled = false;
  }
  private size = 0;
  /** Shown now (0–1) and where each tile is heading (0 or 1). */
  private sight = new Float32Array(0);
  private known = new Float32Array(0);
  private sightGoal = new Uint8Array(0);
  private knownGoal = new Uint8Array(0);
  private lit = new Float32Array(0);
  private litGoal = new Uint8Array(0);
  private settled = true;
  /** Tiles never in sight, whatever the squad sees: the outline round what is played, dark (playtest 20/10 #23); null, none. */
  private shut: Uint8Array | null = null;

  constructor() {
    this.uniforms.fowMap.value.needsUpdate = true;
  }

  /** A new map: everything starts under the mist until `set` says otherwise. */
  resize(width: number, height: number): void {
    const size = width * height;
    if (size === this.size && this.uniforms.fowSize.value.x === width) return;
    this.size = size;
    this.sight = new Float32Array(size);
    this.known = new Float32Array(size);
    this.sightGoal = new Uint8Array(size);
    this.knownGoal = new Uint8Array(size);
    this.lit = new Float32Array(size);
    this.litGoal = new Uint8Array(size);
    this.shut = null;
    this.uniforms.fowMap.value.dispose();
    const texture = new THREE.DataTexture(new Uint8Array(size * 4), width, height);
    texture.magFilter = THREE.LinearFilter;
    texture.minFilter = THREE.LinearFilter;
    texture.needsUpdate = true;
    this.uniforms.fowMap.value = texture;
    this.uniforms.fowSize.value.set(width, height);
  }

  /** The tiles under the fog for good from now on (the map's outline), as a mask of the map's size; null, none. */
  setShut(mask: Uint8Array | null): void {
    this.shut = mask && mask.length === this.size ? mask : null;
  }

  /**
   * Tiles in sight (`visible`, where enemies show) and in view (`view`, the line of sight; at least those in sight),
   * never the outline (setShut); `instant` skips the fade (loading a mission).
   */
  set(visible: readonly boolean[], view: readonly boolean[], instant = false): void {
    for (let i = 0; i < this.size; i++) {
      const shut = !!this.shut?.[i];
      this.sightGoal[i] = visible[i] && !shut ? 1 : 0;
      this.knownGoal[i] = (view[i] || visible[i]) && !shut ? 1 : 0;
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
    const litStep = dt / LIT_FADE;
    const data = this.uniforms.fowMap.value.image.data as Uint8Array;
    let moving = false;
    for (let i = 0; i < this.size; i++) {
      const s = approach(this.sight[i]!, this.sightGoal[i]!, sightStep);
      const k = approach(this.known[i]!, this.knownGoal[i]!, sightStep);
      const l = approach(this.lit[i]!, this.litGoal[i]!, litStep);
      this.sight[i] = s;
      this.known[i] = k;
      this.lit[i] = l;
      if (s !== this.sightGoal[i] || k !== this.knownGoal[i] || l !== this.litGoal[i]) moving = true;
      data[i * 4] = Math.round(s * 255);
      data[i * 4 + 1] = Math.round(k * 255);
      data[i * 4 + 2] = Math.round(l * 255);
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
          uniform vec3 fowUnknown;
          uniform vec3 fowHaze;
          uniform vec2 fowFar;
          uniform float fowEdge;
          uniform float fowNight;
          uniform vec3 fowLamp;
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
          float fowInside = max(fowEdge, 1.0 - smoothstep(0.0, 6.0, length(fowOut)));
          // A mist drifting slowly; it also frays the edge of what is in sight.
          float fowMist = fowNoise(vFowPos.xz * 0.44 + vec2(fowTime * 0.05, fowTime * 0.03));
          // In sight; in view (the line of sight: the rest goes under the mist, seen before or not).
          float fowSeen = smoothstep(0.3, 0.7, fowTile.r + (fowMist - 0.5) * 0.12) * fowInside;
          float fowKnown = smoothstep(0.22, 0.78, fowTile.g + (fowMist - 0.5) * 0.4) * fowInside;
          // In view, out of sight: a very light haze, a touch less colour (no enemies show there).
          vec3 fowGrey = vec3(dot(diffuseColor.rgb, vec3(0.2126, 0.7152, 0.0722)));
          diffuseColor.rgb = mix(mix(diffuseColor.rgb, fowGrey, 0.3) * 0.84 + fowHaze * 0.07, diffuseColor.rgb, fowSeen);
          ${lit ? '' : 'if (fowAoPass > 0.5) discard;'}
          #include <alphamap_fragment>`,
        )
        .replace(
          '#include <normal_fragment_maps>',
          `#include <normal_fragment_maps>
          // The occlusion's pass: the view-space normal, packed as MeshNormalMaterial does; nothing under the mist.
          if (fowAoPass > 0.5) {
            if (fowKnown < 0.5) discard;
            gl_FragColor = vec4(normalize(normal) * 0.5 + 0.5, 1.0);
            return;
          }`,
        )
        .replace(
          '#include <fog_fragment>',
          `#include <fog_fragment>
          ${
            lit
              ? `// Night: a lit tile glows warm, so players read where they'd be seen.
          float fowFloor = fowTile.a * ${(255 / FLOOR_STEP).toFixed(3)} * ${STOREY.toFixed(3)};
          float fowHot = dot(gl_FragColor.rgb, vec3(0.2126, 0.7152, 0.0722));
          gl_FragColor.rgb += diffuseColor.rgb * fowLamp * fowTile.b * fowNight * fowInside * (1.0 - smoothstep(${(STOREY * 0.6).toFixed(2)}, ${(STOREY * 1.2).toFixed(2)}, vFowPos.y - fowFloor)) / (1.0 + 2.5 * fowHot);`
              : ''
          }
          {
            // Out of sight: the mist, over the scene's haze (which would lift it to a grey where
            // everything reads), a little lighter higher up so tall shapes barely show; beyond the edge a little
            // more, the outskirts' outlines (outskirts.ts: the town going on, playtest 21/10 #17).
            vec3 fowDark = fowUnknown * (0.55 + 1.1 * fowMist) * (1.0 + (0.15 + 0.35 * (1.0 - fowInside)) * clamp(vFowPos.y / 5.0, 0.0, 1.0));
            // Beyond the map's edge the same (playtest 20/10 #23, Arturo: what is not played is dark, always); far out
            // (past what the tactical view sees), into the haze, where the sky's horizon begins (sky.ts, 0.9.1).
            gl_FragColor.rgb = mix(fowDark, gl_FragColor.rgb, fowKnown);
            #ifdef USE_FOG
              gl_FragColor.rgb = mix(gl_FragColor.rgb, fogColor, smoothstep(fowFar.x, fowFar.y, length(fowOut)));
            #endif
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
