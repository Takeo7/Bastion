import * as THREE from 'three';
import type { Light } from '@bastion/engine';
import type { FogOfWar } from './fogOfWar';

// The map by night (docs/ALTURA-Y-NOCHE.md; set against Synty's Woodland
// motel capture in the reference block): a dense teal haze and a cold moon
// (renderer.ts), and the warm light of what still works. Which tiles are lit
// is the engine's (its lit mask: street lights, a Bengala, a fire); the fog of
// war paints them (fogOfWar.ts: setLit). Here, what shines: every street
// light's head glows, a few headlights left on, and the Bengalas and fires
// while they last. Few real lights (their cost is per pixel of everything
// lit): a fixed pool, the Bengalas and fires first, then the street lights
// nearest the view; the pool never changes size, so no shader is rebuilt.

/** 1 by night, 0 by day: what glows only at night reads it (lit windows: buildings.ts). */
export const NIGHT = { value: 0 };

/** Real lights, wherever they're most needed. */
const REAL_LIGHTS = 6;
const LAMP = new THREE.Color(1.0, 0.62, 0.3);
/**
 * How strong each light is (Arturo 17/10: «la luz deslumbra mucho de noche»): the pools still read from afar,
 * where the squad is seen, without burning the ground and what stands on it white, or the bloom veiling the rest.
 */
const LAMP_LIGHT = 13;
const BURNING_LIGHT = 16;
/** The lamps' heads and the headlights left on (HDR, for the bloom). */
const LAMP_HEAD = new THREE.Color(2.2, 1.5, 0.9);
const HEADLIGHT = new THREE.Color(2.0, 1.9, 1.6);
/** A Bengala burns red; a fire, orange. */
const FLARE = new THREE.Color(1.0, 0.1, 0.08);
const FIRE = new THREE.Color(1.0, 0.5, 0.15);
/** Seconds a Bengala's or a fire's light takes to come up or die down. */
const FADE = 0.5;

/** Where a lamp shines from (a street light's head) or a car's front, and which way it faces. */
export interface Glow {
  x: number;
  y: number;
  z: number;
  /** The way it faces (a headlight's beam), on the ground. */
  dx: number;
  dz: number;
}

/** A Bengala or a fire as drawn: its glow, how far it has come up, whether it's going out. */
interface Burning {
  id: string;
  at: THREE.Vector3;
  color: THREE.Color;
  /** Its light's reach (tiles). */
  radius: number;
  glow: THREE.Mesh;
  level: number;
  out: boolean;
}

/** Small glowing boxes merged into one geometry: position, colour (HDR, for the bloom). */
class Glows {
  readonly position: number[] = [];
  readonly color: number[] = [];

  box(x: number, y: number, z: number, sx: number, sy: number, sz: number, c: THREE.Color): void {
    const b = new THREE.BoxGeometry(sx, sy, sz).toNonIndexed().translate(x, y, z);
    const p = b.attributes.position!;
    for (let i = 0; i < p.count; i++) {
      this.position.push(p.getX(i), p.getY(i), p.getZ(i));
      this.color.push(c.r, c.g, c.b);
    }
    b.dispose();
  }

  geometry(): THREE.BufferGeometry {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(this.position, 3));
    g.setAttribute('color', new THREE.Float32BufferAttribute(this.color, 3));
    // The fog of war reads the normal (a face belongs to the tile behind it): without one it was NaN, and bloom spread it everywhere.
    g.computeVertexNormals();
    g.computeBoundingSphere();
    return g;
  }
}

const flareGeometry = new THREE.SphereGeometry(0.16, 10, 8);

/** The night's lights of one map. */
export class NightLights {
  readonly group = new THREE.Group();
  private readonly lamps: Glow[];
  private readonly lights: THREE.PointLight[] = [];
  private readonly materials: THREE.Material[] = [];
  private readonly geometries: THREE.BufferGeometry[] = [];
  private readonly burning = new Map<string, Burning>();
  private readonly focus = new THREE.Vector3();
  private readonly ray = new THREE.Vector3();
  private readonly chosen = new Int32Array(REAL_LIGHTS);
  private time = 0;

  constructor(
    lamps: Glow[],
    headlights: Glow[],
    private readonly fog: FogOfWar,
  ) {
    this.group.visible = NIGHT.value > 0.5;
    this.lamps = lamps;
    const glows = new Glows();
    for (const l of lamps) glows.box(l.x, l.y, l.z, 0.32, 0.08, 0.2, LAMP_HEAD);
    for (const h of headlights) {
      const sx = -h.dz * 0.24;
      const sz = h.dx * 0.24;
      for (const s of [-1, 1]) glows.box(h.x + sx * s, h.y, h.z + sz * s, 0.1, 0.07, 0.1, HEADLIGHT);
    }
    const glowMaterial = fog.patch(new THREE.MeshBasicMaterial({ vertexColors: true }));
    this.materials.push(glowMaterial);
    if (glows.position.length) {
      const g = glows.geometry();
      this.geometries.push(g);
      const mesh = new THREE.Mesh(g, glowMaterial);
      mesh.matrixAutoUpdate = false;
      this.group.add(mesh);
    }
    for (let k = 0; k < REAL_LIGHTS; k++) {
      const light = new THREE.PointLight(LAMP, 0, 7, 2);
      this.lights.push(light);
      this.group.add(light);
    }
  }

  /** Night or day. */
  setVisible(on: boolean): void {
    this.group.visible = on;
  }

  /**
   * The Bengalas and fires burning now (the engine's lights that last a few
   * turns); `height` the floor under a tile. New ones come up, gone ones die
   * down.
   */
  setBurning(lights: readonly Light[], height: (x: number, y: number) => number): void {
    const now = new Set<string>();
    for (const l of lights) {
      if (l.turns === undefined) continue;
      now.add(l.id);
      if (this.burning.has(l.id)) continue;
      const flare = l.radius >= 3;
      const color = (flare ? FLARE : FIRE).clone();
      const material = this.fog.patch(new THREE.MeshBasicMaterial({ color: color.clone().multiplyScalar(flare ? 2.2 : 2.5), transparent: true, opacity: 0 }));
      this.materials.push(material);
      const glow = new THREE.Mesh(flareGeometry, material);
      const at = new THREE.Vector3(l.tile.x + 0.5, height(l.tile.x, l.tile.y) + (flare ? 1.4 : 0.35), l.tile.y + 0.5);
      glow.position.copy(at);
      glow.scale.setScalar(flare ? 1 : 1.6);
      this.group.add(glow);
      this.burning.set(l.id, { id: l.id, at, color, radius: l.radius, glow, level: 0, out: false });
    }
    for (const b of this.burning.values()) if (!now.has(b.id)) b.out = true;
  }

  /** Fades, flicker, and the real lights put where they count (no allocation). */
  update(dt: number, camera: THREE.Camera | null): void {
    if (!this.group.visible) return;
    this.time += dt;
    for (const b of this.burning.values()) {
      b.level = Math.max(0, Math.min(1, b.level + (b.out ? -dt : dt) / FADE));
      (b.glow.material as THREE.MeshBasicMaterial).opacity = b.level;
      if (b.out && b.level === 0) {
        this.group.remove(b.glow);
        (b.glow.material as THREE.Material).dispose();
        this.burning.delete(b.id);
      }
    }
    // The Bengalas and fires first.
    let n = 0;
    for (const b of this.burning.values()) {
      if (n >= this.lights.length) break;
      const light = this.lights[n++]!;
      const flicker = b.radius >= 3 ? 0.9 + 0.1 * Math.sin(this.time * 23) : 0.75 + 0.25 * Math.sin(this.time * 11) * Math.sin(this.time * 7.3);
      light.color.copy(b.color);
      light.intensity = BURNING_LIGHT * b.level * flicker;
      light.distance = b.radius * 2.6;
      // A fire's light over its flames, not in them: from that close it burnt the ground and whatever stood by white.
      light.position.copy(b.at);
      if (b.radius < 3) light.position.y += 0.55;
    }
    // Then the street lights nearest the point the camera looks at.
    if (camera) {
      camera.getWorldDirection(this.ray);
      const t = this.ray.y < -0.01 ? -camera.position.y / this.ray.y : 0;
      this.focus.copy(camera.position).addScaledVector(this.ray, t);
    }
    this.chosen.fill(-1);
    for (let k = 0; n < this.lights.length; k++) {
      let best = -1;
      let bestD = Infinity;
      for (let i = 0; i < this.lamps.length; i++) {
        if (this.chosen.includes(i)) continue;
        const l = this.lamps[i]!;
        const d = (l.x - this.focus.x) ** 2 + (l.z - this.focus.z) ** 2;
        if (d < bestD) [best, bestD] = [i, d];
      }
      const light = this.lights[n++]!;
      if (best < 0) {
        light.intensity = 0;
        continue;
      }
      this.chosen[k] = best;
      const l = this.lamps[best]!;
      light.color.copy(LAMP);
      light.intensity = LAMP_LIGHT;
      light.distance = 7;
      // Well under the lamp's own head: a point light right at a surface overflows the half-float target (and bloom spreads it).
      light.position.set(l.x, l.y - 0.6, l.z);
    }
  }

  dispose(): void {
    for (const b of this.burning.values()) (b.glow.material as THREE.Material).dispose();
    for (const m of this.materials) m.dispose();
    for (const g of this.geometries) g.dispose();
  }
}
