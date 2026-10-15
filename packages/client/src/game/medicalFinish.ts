import * as THREE from 'three';
import type { GameState } from '@bastion/engine';
import type { BuildingPlan } from './buildings';
import { STOREY } from './coords';
import { extendShader } from './fogOfWar';

// Own clinical surfaces, inspired by the Apocalypse hospital reference.
// A tiny building mask scopes them to medical interiors, on every storey.
// Wear changes the paint and tiles, never the movement or visibility rules.
const COMMON = /* glsl */ `
  uniform sampler2D uMedical;
  uniform vec2 uMedicalSize;
  varying vec3 vMedicalWorld;
  varying vec3 vMedicalNormal;
  float medHash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
  }
  float medNoise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(medHash(i), medHash(i + vec2(1, 0)), f.x), mix(medHash(i + vec2(0, 1)), medHash(i + 1.0), f.x), f.y);
  }
`;

const FLOOR = /* glsl */ `
  // About 65 cm square, independent of the tactical grid.
  vec2 uv = vMedicalWorld.xz / 0.44;
  vec2 cell = floor(uv), f = fract(uv);
  float r = medHash(cell);
  vec2 edge = min(f, 1.0 - f);
  float aa = max(fwidth(uv.x), fwidth(uv.y));
  float grout = 1.0 - smoothstep(0.012, 0.023 + aa * 0.6, min(edge.x, edge.y));
  vec3 tile = mix(vec3(0.64, 0.65, 0.60), vec3(0.79, 0.78, 0.70), r);
  float dust = medNoise(vMedicalWorld.xz * 0.75 + 21.0) * medNoise(uv * 1.8);
  tile *= 1.0 - dust * (0.10 + wear * 0.65);
  float damaged = step(r, wear * wear * 0.38);
  float crack = abs(f.y - (0.18 + f.x * 0.48 + medNoise(vec2(f.x * 12.0, r * 71.0)) * 0.13));
  tile = mix(tile, vec3(0.17, 0.15, 0.13), damaged * (1.0 - smoothstep(0.005, 0.013 + aa * 0.5, crack)));
  float chip = step(min(edge.x, edge.y) + medNoise(f * 16.0 + cell) * 0.15, wear * 0.13) * damaged;
  tile = mix(tile, vec3(0.27, 0.24, 0.19), chip);
  diffuseColor.rgb = mix(tile, vec3(0.23, 0.23, 0.21), grout * 0.78);
`;

const WALL = /* glsl */ `
  float h = mod(vMedicalWorld.y, ${STOREY.toFixed(2)});
  vec2 along = abs(vMedicalNormal.x) > 0.5 ? vec2(vMedicalWorld.z, vMedicalWorld.y) : vMedicalWorld.xy;
  vec3 paint = h < 0.80 ? vec3(0.36, 0.34, 0.31) : vec3(0.76, 0.75, 0.67);
  if (h > 0.77 && h < 0.97) paint = vec3(0.25, 0.065, 0.055);
  if (h < 0.095) paint = vec3(0.16, 0.17, 0.16);
  float dirt = medNoise(along * vec2(1.9, 3.2) + 8.0);
  paint *= 0.93 + dirt * 0.07 - wear * medNoise(along * 7.0) * 0.17;
  // Ragged islands of exposed masonry, with staggered brick joints.
  float wornPatch = medNoise(along * 1.4 + 31.0) + medNoise(along * 9.0) * 0.18;
  float missing = step(0.99 - wear * 0.35, wornPatch) * step(0.2, wear);
  vec2 brickUv = along / vec2(0.29, 0.14);
  brickUv.x += mod(floor(brickUv.y), 2.0) * 0.5;
  vec2 joint = min(fract(brickUv), 1.0 - fract(brickUv));
  vec3 brick = mix(vec3(0.28, 0.13, 0.08), vec3(0.43, 0.23, 0.13), medHash(floor(brickUv)));
  brick = mix(brick, vec3(0.39, 0.36, 0.30), 1.0 - smoothstep(0.025, 0.055, min(joint.x, joint.y)));
  float rim = step(0.965 - wear * 0.35, wornPatch) * step(0.2, wear);
  paint = mix(paint, vec3(0.48, 0.44, 0.35), rim);
  diffuseColor.rgb = mix(paint, brick, missing);
`;

export class MedicalFinish {
  private readonly texture: THREE.DataTexture;
  private readonly size: THREE.Vector2;
  private readonly active: boolean;

  constructor(s: GameState, plan: BuildingPlan) {
    this.active = plan.buildings.some((b) => b.medicalWear !== undefined);
    const data = new Uint8Array(s.width * s.height * 4);
    for (let i = 0; i < plan.owner.length; i++) {
      const b = plan.buildings[plan.owner[i]!];
      if (b?.medicalWear === undefined) continue;
      data.set([Math.round(30 + b.medicalWear * 210), b.storeys, 0, 255], i * 4);
    }
    this.texture = new THREE.DataTexture(data, s.width, s.height);
    this.texture.needsUpdate = true;
    this.size = new THREE.Vector2(s.width, s.height);
  }

  patch(material: THREE.Material, surface: 'floor' | 'wall'): void {
    if (!this.active) return;
    extendShader(material, `medical-${surface}`, (shader) => {
      shader.uniforms.uMedical = { value: this.texture };
      shader.uniforms.uMedicalSize = { value: this.size };
      shader.vertexShader = shader.vertexShader
        .replace('#include <common>', `#include <common>\nvarying vec3 vMedicalWorld;\nvarying vec3 vMedicalNormal;${surface === 'wall' ? '\nvarying vec2 vMedicalOut;' : ''}`)
        .replace('#include <project_vertex>', `#include <project_vertex>
          ${surface === 'wall' ? 'vMedicalOut = aCut.yz;' : ''}
          vec4 medWorld = vec4(transformed, 1.0);
          vec3 medNormal = objectNormal;
          #ifdef USE_BATCHING
            medWorld = batchingMatrix * medWorld;
            medNormal = mat3(batchingMatrix) * medNormal;
          #endif
          #ifdef USE_INSTANCING
            medWorld = instanceMatrix * medWorld;
            medNormal = mat3(instanceMatrix) * medNormal;
          #endif
          vMedicalWorld = (modelMatrix * medWorld).xyz;
          vMedicalNormal = normalize(mat3(modelMatrix) * medNormal);
        `);
      shader.fragmentShader = shader.fragmentShader
        .replace('#include <common>', `#include <common>\n${COMMON}${surface === 'wall' ? '\nvarying vec2 vMedicalOut;' : ''}`)
        .replace('#include <roughnessmap_fragment>', `
          vec2 medUv = vMedicalWorld.xz / uMedicalSize;
          vec4 medical = texture2D(uMedical, medUv);
          if (medical.r > 0.01 && all(greaterThanEqual(medUv, vec2(0))) && all(lessThan(medUv, vec2(1)))
              && vMedicalWorld.y < medical.g * 255.0 * ${STOREY.toFixed(2)} - 0.08
              && ${surface === 'floor' ? `vMedicalNormal.y > 0.8 && mod(vMedicalWorld.y + 0.01, ${STOREY.toFixed(2)}) < 0.06` : 'abs(vMedicalNormal.y) < 0.5 && (dot(vMedicalOut, vMedicalOut) < 0.01 || dot(vMedicalNormal.xz, vMedicalOut) < -0.15)'}) {
            float wear = clamp((medical.r * 255.0 - 30.0) / 210.0, 0.0, 1.0);
            ${surface === 'floor' ? FLOOR : WALL}
          }
          #include <roughnessmap_fragment>
        `);
    });
  }

  dispose(): void {
    this.texture.dispose();
  }
}
