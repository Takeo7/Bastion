import * as THREE from 'three';
import type { GameState } from '@bastion/engine';
import { baseTexture, kitModel, loadKitModels } from '../strategy/baseKit';
import type { BuildingPlan } from './buildings';
import { STOREY } from './coords';
import { extendShader } from './fogOfWar';

/** The winter described by the opening chapter, independent of map seed or biome. */
export function snowyMission(s: Pick<GameState, 'campaignMission' | 'snow'>): boolean {
  // Still winter in the campaign (the engine says so, playtest 16/10); before it did, only the opening chapter.
  return s.snow ?? s.campaignMission?.story === 1;
}

let texture: THREE.Texture | null = null;

/** The same converted Synty snow as Atalaya; shared, never repainted into an atlas. */
export function snowTexture(): THREE.Texture {
  return (texture ??= baseTexture('snowGround', 1));
}

/** Snow on this map's exposed surfaces, before the fog of war darkens them. */
export class SnowCover {
  readonly ready = loadKitModels(['drift']);
  private readonly shelter: THREE.DataTexture;

  constructor(private readonly s: GameState) {
    this.shelter = new THREE.DataTexture(new Uint8Array(s.width * s.height * 4), s.width, s.height);
    this.shelter.needsUpdate = true;
  }

  /** A cutaway roof still shelters the rooms and their furniture from snow. */
  shelterBuildings(plan: BuildingPlan): void {
    const data = this.shelter.image.data as Uint8Array;
    for (let i = 0; i < plan.owner.length; i++) {
      const b = plan.buildings[plan.owner[i]!];
      if (!b || b.kind === 'ruin') continue;
      data[i * 4] = Math.min(255, Math.round((b.base + b.storeys * STOREY - 0.12) * 16));
    }
    this.shelter.needsUpdate = true;
  }

  patch(material: THREE.Material): void {
    extendShader(material, 'combat-snow', (shader) => {
      Object.assign(shader.uniforms, {
        snowGround: { value: snowTexture() },
        snowShelter: { value: this.shelter },
        snowSize: { value: new THREE.Vector2(this.s.width, this.s.height) },
      });
      shader.vertexShader = shader.vertexShader
        .replace('#include <common>', '#include <common>\nvarying vec3 vSnowPos;\nvarying vec3 vSnowNormal;')
        .replace('#include <project_vertex>', `#include <project_vertex>
        {
          vec4 w = vec4(transformed, 1.0);
          vec3 n = objectNormal;
          #ifdef USE_BATCHING
            w = batchingMatrix * w;
            n = mat3(batchingMatrix) * n;
          #endif
          #ifdef USE_INSTANCING
            w = instanceMatrix * w;
            n = mat3(instanceMatrix) * n;
          #endif
          vSnowPos = (modelMatrix * w).xyz;
          vSnowNormal = normalize(mat3(modelMatrix) * n);
        }`);
      shader.fragmentShader = shader.fragmentShader
        .replace('#include <common>', `#include <common>
        varying vec3 vSnowPos;
        varying vec3 vSnowNormal;
        uniform sampler2D snowGround;
        uniform sampler2D snowShelter;
        uniform vec2 snowSize;`)
        .replace('#include <alphamap_fragment>', `
        {
          vec2 cell = floor(vSnowPos.xz) + 0.5;
          float roof = texture2D(snowShelter, cell / snowSize).r * (255.0 / 16.0);
          float outside = step(roof, vSnowPos.y + 0.03);
          float wave = sin(vSnowPos.x * 1.7 + sin(vSnowPos.z * 1.3)) * sin(vSnowPos.z * 1.9 + vSnowPos.y * 2.1);
          float cap = smoothstep(0.38, 0.8, normalize(vSnowNormal).y + wave * 0.06) * outside;
          vec3 snow = texture2D(snowGround, vSnowPos.xz / 3.0).rgb * vec3(0.89, 0.95, 1.0);
          diffuseColor.rgb = mix(diffuseColor.rgb, snow, cap * 0.96);
        }
        #include <alphamap_fragment>`);
    });
  }

  /** Small accumulated banks within outdoor cover tiles, clear of walkable routes. */
  drifts(plan: BuildingPlan): THREE.Matrix4[] {
    const out: THREE.Matrix4[] = [];
    const { width, tiles, elev } = this.s;
    const size = kitModel('drift')?.size;
    if (!size) return out;
    for (let i = 0; i < tiles.length; i++) {
      if (tiles[i] !== 'high' || plan.owner[i]! >= 0 || (elev[i] ?? 0) !== 0 || i % 3 !== 0) continue;
      const x = i % width;
      const z = Math.floor(i / width);
      out.push(new THREE.Matrix4().compose(
        new THREE.Vector3(x + 0.5, 0.006, z + 0.5),
        new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), i * 2.39996),
        new THREE.Vector3(0.92 / Math.max(size.x, size.z), 0.23 / size.y, 0.92 / Math.max(size.x, size.z)),
      ));
    }
    return out;
  }

  dispose(): void {
    this.shelter.dispose();
  }
}
