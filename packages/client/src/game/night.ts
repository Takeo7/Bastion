import * as THREE from 'three';
import type { FogOfWar } from './fogOfWar';

// The map by night (reference block's night test, after Synty's Woodland
// motel capture): a dense teal haze and a cold moon (renderer.ts), and the
// warm light of what still works. Few real lights (their cost is per pixel
// of everything lit): only the street lights nearest the view light the
// scene; every lamp, every headlight left on, paints its pool on the ground
// as an additive decal and glows (bloom), and some windows are lit. Built
// with the map, hidden by day: a day frame draws none of it.

/** 1 by night, 0 by day: what glows only at night reads it (lit windows: buildings.ts). */
export const NIGHT = { value: 0 };

/** Real lights, at the street lights nearest what the camera looks at. */
const REAL_LIGHTS = 6;
const LAMP = new THREE.Color(1.0, 0.62, 0.3);
const HEADLIGHT = new THREE.Color(0.95, 0.9, 0.75);
/** How bright the painted pools are (additive over the ground). */
const POOL = 0.55;

/** Where a lamp shines from (a street light's head) or a car's front, and which way it faces. */
export interface Glow {
  x: number;
  y: number;
  z: number;
  /** The way it faces (a headlight's beam), on the ground. */
  dx: number;
  dz: number;
}

let pool: THREE.Texture | null = null;

/** A soft round pool of light, bright in the middle (shared). */
function poolTexture(): THREE.Texture {
  if (pool) return pool;
  const c = document.createElement('canvas');
  c.width = c.height = 128;
  const g = c.getContext('2d')!;
  const r = g.createRadialGradient(64, 64, 0, 64, 64, 64);
  r.addColorStop(0, 'rgba(255,255,255,1)');
  r.addColorStop(0.35, 'rgba(255,255,255,0.55)');
  r.addColorStop(0.7, 'rgba(255,255,255,0.15)');
  r.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = r;
  g.fillRect(0, 0, 128, 128);
  pool = new THREE.CanvasTexture(c);
  pool.colorSpace = THREE.SRGBColorSpace;
  return pool;
}

/** Quads merged into one geometry: position, uv, colour. */
class Quads {
  readonly position: number[] = [];
  readonly uv: number[] = [];
  readonly color: number[] = [];

  /** A flat quad on the ground at (x, y, z): `length` along (dx, dz), `width` across, starting `back` behind the point. */
  ground(x: number, y: number, z: number, dx: number, dz: number, length: number, width: number, back: number, c: THREE.Color): void {
    const ax = -dz * width * 0.5;
    const az = dx * width * 0.5;
    const x0 = x - dx * back;
    const z0 = z - dz * back;
    const x1 = x0 + dx * length;
    const z1 = z0 + dz * length;
    const corners = [
      [x0 - ax, z0 - az, 0, 0],
      [x0 + ax, z0 + az, 1, 0],
      [x1 + ax, z1 + az, 1, 1],
      [x1 - ax, z1 - az, 0, 1],
    ];
    // Facing up.
    for (const k of [0, 1, 2, 0, 2, 3]) {
      const [px, pz, u, v] = corners[k]!;
      this.position.push(px!, y, pz!);
      this.uv.push(u!, v!);
      this.color.push(c.r, c.g, c.b);
    }
  }

  /** A small glowing box (a lamp's head, a headlight), all faces. */
  box(x: number, y: number, z: number, sx: number, sy: number, sz: number, c: THREE.Color): void {
    const b = new THREE.BoxGeometry(sx, sy, sz).toNonIndexed().translate(x, y, z);
    const p = b.attributes.position!;
    for (let i = 0; i < p.count; i++) {
      this.position.push(p.getX(i), p.getY(i), p.getZ(i));
      this.uv.push(0.5, 0.5);
      this.color.push(c.r, c.g, c.b);
    }
    b.dispose();
  }

  geometry(): THREE.BufferGeometry {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(this.position, 3));
    g.setAttribute('uv', new THREE.Float32BufferAttribute(this.uv, 2));
    g.setAttribute('color', new THREE.Float32BufferAttribute(this.color, 3));
    // The fog of war reads the normal (a face belongs to the tile behind it): without one it was NaN, and bloom spread it everywhere.
    g.computeVertexNormals();
    g.computeBoundingSphere();
    return g;
  }
}

/** The night's lights of one map. */
export class NightLights {
  readonly group = new THREE.Group();
  private readonly lamps: Glow[] = [];
  private readonly lights: THREE.PointLight[] = [];
  private readonly materials: THREE.Material[] = [];
  private readonly geometries: THREE.BufferGeometry[] = [];
  private readonly focus = new THREE.Vector3();
  private readonly ray = new THREE.Vector3();
  private readonly chosen = new Int32Array(REAL_LIGHTS);

  constructor(lamps: Glow[], headlights: Glow[], fog: FogOfWar) {
    this.group.visible = NIGHT.value > 0.5;
    this.lamps = lamps;
    const pools = new Quads();
    const glows = new Quads();
    for (const l of lamps) {
      pools.ground(l.x, 0.02, l.z, 1, 0, 3.6, 3.6, 1.8, LAMP.clone().multiplyScalar(POOL));
      glows.box(l.x, l.y, l.z, 0.32, 0.08, 0.2, new THREE.Color(3.2, 2.2, 1.3));
    }
    for (const h of headlights) {
      // A beam on the road ahead, and the two lamps.
      pools.ground(h.x, 0.025, h.z, h.dx, h.dz, 3.2, 1.9, -0.1, HEADLIGHT.clone().multiplyScalar(POOL));
      const sx = -h.dz * 0.24;
      const sz = h.dx * 0.24;
      for (const s of [-1, 1]) glows.box(h.x + sx * s, h.y, h.z + sz * s, 0.1, 0.07, 0.1, new THREE.Color(3, 2.9, 2.4));
    }
    const poolMaterial = fog.patch(
      new THREE.MeshBasicMaterial({ map: poolTexture(), vertexColors: true, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 }),
    );
    const glowMaterial = fog.patch(new THREE.MeshBasicMaterial({ vertexColors: true, toneMapped: true }));
    this.materials.push(poolMaterial, glowMaterial);
    for (const [quads, material] of [
      [pools, poolMaterial],
      [glows, glowMaterial],
    ] as const) {
      if (!quads.position.length) continue;
      const g = quads.geometry();
      this.geometries.push(g);
      const mesh = new THREE.Mesh(g, material);
      mesh.matrixAutoUpdate = false;
      mesh.renderOrder = 1;
      this.group.add(mesh);
    }
    for (let k = 0; k < Math.min(REAL_LIGHTS, lamps.length); k++) {
      const light = new THREE.PointLight(LAMP, 22, 7, 2);
      this.lights.push(light);
      this.group.add(light);
    }
  }

  /** Night or day. */
  setVisible(on: boolean): void {
    this.group.visible = on;
  }

  /** Puts the real lights at the street lights nearest the point the camera looks at (no allocation). */
  update(camera: THREE.Camera | null): void {
    if (!this.group.visible || !camera || !this.lights.length) return;
    camera.getWorldDirection(this.ray);
    const t = this.ray.y < -0.01 ? -camera.position.y / this.ray.y : 0;
    this.focus.copy(camera.position).addScaledVector(this.ray, t);
    // The few nearest, by a partial selection (few lamps, few lights).
    this.chosen.fill(-1);
    for (let n = 0; n < this.lights.length; n++) {
      const light = this.lights[n]!;
      let best = -1;
      let bestD = Infinity;
      for (let i = 0; i < this.lamps.length; i++) {
        if (this.chosen.includes(i)) continue;
        const l = this.lamps[i]!;
        const d = (l.x - this.focus.x) ** 2 + (l.z - this.focus.z) ** 2;
        if (d < bestD) [best, bestD] = [i, d];
      }
      if (best < 0) break;
      this.chosen[n] = best;
      const l = this.lamps[best]!;
      // Well under the lamp's own head: a point light right at a surface overflows the half-float target (and bloom spreads it).
      light.position.set(l.x, l.y - 0.6, l.z);
    }
  }

  dispose(): void {
    for (const m of this.materials) m.dispose();
    for (const g of this.geometries) g.dispose();
  }
}
