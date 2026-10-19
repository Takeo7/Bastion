import * as THREE from 'three';
import { decorOf, DIRS4, DIRS8, type Biome, type DecorKind, type GameState, type TileKind } from '@bastion/engine';
import { extendShader } from './fogOfWar';
import { STOREY } from './coords';
import { snowTexture, snowyMission } from './snow';
import { terrainFile, type PropId, type TerrainId } from './syntyManifest';

// The ground as continuous terrain (second playtest: «los mapas siguen siendo
// demasiado cuadrículas»). Tiles still say what the floor is, but the look is
// worked out in world space with Synty's tiling textures: broad patches that
// run across tiles, borders between paving, lawns and streets that wander
// (kerbs and gutters in town, grass over the edge of a dirt track), cracks
// and road paint. On top, small Synty props (grass tufts, pebbles, leaves,
// papers…) are scattered off the grid, cosmetic only.

/** Floor kinds, as the ground shader reads them from the red channel. */
const OPEN = 0;
const STREET = 1;
const INDOOR = 2;
const ROOF = 3;
/** Top of a hill in the wilds: grass, like open ground. */
const HILL = 4;
/** A furrow of a ploughed field: one tile wide, along the field. */
const FURROW = 5;
/** Tarmac that is not a street (the engine's ground 3: a yard, a forecourt, a car park): no paint on it. */
const YARD = 6;

/** The floor kinds (GroundMap.kinds), for what dresses the ground (dressing.ts). */
export const FLOOR = { OPEN, STREET, INDOOR, ROOF, HILL, FURROW, YARD } as const;

/** Open ground marked as grass in a town (blue channel). */
const LAWN = 3;
/** A street tile of a zebra crossing (blue channel; +1 when the street runs along z). */
const CROSSWALK = 4;
/** Things that stand on paving: open ground round them stays paved. */
const PAVED = new Set<DecorKind>(['planter', 'fountain', 'fountainCorner', 'statue', 'kiosk', 'carFront', 'carRear', 'crates', 'barricade', 'bin', 'bench']);

/** What each tile's floor is, for the ground shader and the scattered details. */
export class GroundMap {
  readonly texture: THREE.DataTexture;
  /** Kind per tile (OPEN, STREET…). */
  readonly kinds: Uint8Array;

  constructor(readonly s: GameState) {
    const { width: w, height: h } = s;
    const generated = s.mapId.startsWith('gen-');
    // Procedural maps say which tiles are streets (or dirt tracks) and rooms; older saves had streets on a 13-tile grid.
    const isStreet = (x: number, y: number) =>
      x >= 0 && y >= 0 && x < w && y < h && (s.elev[y * w + x] ?? 0) === 0 && (s.ground ? s.ground[y * w + x] === 1 : generated && w === 36 && (x % 13 >= 10 || y % 13 >= 10));
    const wilds = s.biome === 'wilds';
    // The forest road and the stop's yards (site roadside, #25): tarmac, the road with a town street's centre line.
    const tarmac = wilds && s.site === 'roadside';
    const data = new Uint8Array(w * h * 4);
    this.kinds = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const i = y * w + x;
        const e = s.elev[i] ?? 0;
        // A siege's watchtowers stand on the ground (docs/FASE12.md §10): their decks are the guard tower's own.
        let kind = e > 0 && !s.mission.siege ? (wilds ? HILL : ROOF) : s.ground?.[i] === 2 ? INDOOR : s.ground?.[i] === 3 ? YARD : isStreet(x, y) ? STREET : OPEN;
        let axis = 0;
        let offset = 0;
        // A one-tile strip in the wilds is a furrow of a field, not the track.
        const furrowX = wilds && !isStreet(x, y - 1) && !isStreet(x, y + 1) && (isStreet(x - 1, y) || isStreet(x + 1, y));
        const furrowZ = wilds && !isStreet(x - 1, y) && !isStreet(x + 1, y) && (isStreet(x, y - 1) || isStreet(x, y + 1));
        if (kind === STREET && (furrowX || furrowZ)) {
          [kind, axis] = [FURROW, furrowX ? 1 : 2];
        } else if (kind === STREET && (!wilds || tarmac)) {
          // Centre line of a street two to four tiles wide, away from crossings.
          const run = (dx: number, dy: number) => {
            let n = 0;
            while (n < 8 && isStreet(x + dx * (n + 1), y + dy * (n + 1))) n++;
            return n;
          };
          const [up, down, left, right] = [run(0, -1), run(0, 1), run(-1, 0), run(1, 0)];
          const across = up + down + 1;
          const along = left + right + 1;
          if (across >= 2 && across <= 4 && along >= across + 3 && left && right) [axis, offset] = [1, y - up + across / 2 - y];
          else if (along >= 2 && along <= 4 && across >= along + 3 && up && down) [axis, offset] = [2, x - left + along / 2 - x];
          if (offset < 0 || offset > 1) axis = 0;
        }
        this.kinds[i] = kind;
        data.set([kind, e, axis, Math.round(offset * 255)], i * 4);
      }
    }
    // Hand-made maps say nothing of streets: they stay paved.
    if (s.biome === 'city' && s.ground) {
      this.lawns(data);
      this.crossings(data, isStreet);
    }
    this.texture = new THREE.DataTexture(data, w, h);
    this.texture.needsUpdate = true;
  }

  /**
   * Town ground away from the streets is grass (gardens, verges), unless
   * something paved stands near it: a square, a car park, a kiosk. Marked
   * LAWN in the blue channel of open ground, for the ground shader.
   */
  private lawns(data: Uint8Array): void {
    const s = this.s;
    const { width: w, height: h } = s;
    const far = new Array<number>(w * h).fill(99);
    const queue: number[] = [];
    for (let i = 0; i < w * h; i++) {
      if (this.kinds[i] === STREET || this.kinds[i] === YARD) {
        far[i] = 0;
        queue.push(i);
      }
    }
    for (let k = 0; k < queue.length; k++) {
      const i = queue[k]!;
      const x = i % w;
      const y = (i - x) / w;
      for (const d of DIRS4) {
        const nx = x + d.x;
        const ny = y + d.y;
        if (nx < 0 || ny < 0 || nx >= w || ny >= h || far[ny * w + nx]! <= far[i]! + 1) continue;
        far[ny * w + nx] = far[i]! + 1;
        queue.push(ny * w + nx);
      }
    }
    const paved = new Uint8Array(w * h);
    for (let i = 0; i < w * h; i++) {
      const kind = decorOf(s.decor?.[i])?.kind;
      if (!kind || !PAVED.has(kind)) continue;
      const x = i % w;
      const y = (i - x) / w;
      for (let dy = -3; dy <= 3; dy++) for (let dx = -3; dx <= 3; dx++) if (x + dx >= 0 && y + dy >= 0 && x + dx < w && y + dy < h) paved[(y + dy) * w + x + dx] = 1;
    }
    for (let i = 0; i < w * h; i++) if (this.kinds[i] === OPEN && far[i]! >= 2 && !paved[i]) data[i * 4 + 2] = LAWN;
  }

  /**
   * Zebra crossings: the row of street tiles just before a crossroads, across
   * the street (blue channel CROSSWALK + 0 for a street along x, + 1 along z).
   */
  private crossings(data: Uint8Array, isStreet: (x: number, y: number) => boolean): void {
    const { width: w, height: h } = this.s;
    const span = (x: number, y: number, dx: number, dy: number) => {
      let n = 1;
      for (let k = 1; k < 8 && isStreet(x + dx * k, y + dy * k); k++) n++;
      for (let k = 1; k < 8 && isStreet(x - dx * k, y - dy * k); k++) n++;
      return n;
    };
    // A crossroads: street far both ways.
    const cross = (x: number, y: number) => isStreet(x, y) && span(x, y, 1, 0) > 5 && span(x, y, 0, 1) > 5;
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        if (!isStreet(x, y) || cross(x, y)) continue;
        const alongX = span(x, y, 1, 0) > span(x, y, 0, 1);
        const next = alongX ? cross(x - 1, y) || cross(x + 1, y) : cross(x, y - 1) || cross(x, y + 1);
        if (next) data[(y * w + x) * 4 + 2] = CROSSWALK + (alongX ? 0 : 1);
      }
    }
  }

  /** Open ground in town marked as a lawn (garden, verge), not paving. */
  lawn(i: number): boolean {
    return this.kinds[i] === OPEN && (this.texture.image.data as Uint8Array)[i * 4 + 2] === LAWN;
  }

  /** Tile i is open ground (the outer half of a wall drawn thin along its middle: buildings.ts). */
  outdoors(i: number): void {
    if (this.kinds[i] !== INDOOR) return;
    this.kinds[i] = OPEN;
    (this.texture.image.data as Uint8Array)[i * 4] = OPEN;
    this.texture.needsUpdate = true;
  }

  /** The material of the floor slabs: their tops painted with the packs' tiling textures and flat colours. */
  material(): THREE.MeshStandardMaterial {
    // The slabs' sides take their tile's colour, per vertex (batch.ts); the tops are painted over it.
    const m = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.95, vertexColors: true });
    const biome = this.s.biome === 'wilds' ? 1 : this.s.biome === 'facility' ? 2 : 0;
    const c = (hex: number) => ({ value: new THREE.Color(hex) });
    const uniforms = {
      groundMap: { value: this.texture },
      groundSize: { value: new THREE.Vector2(this.s.width, this.s.height) },
      gBiome: { value: biome },
      gTarmac: { value: this.s.biome === 'wilds' && this.s.site === 'roadside' ? 1 : 0 },
      gSnow: { value: snowyMission(this.s) ? 1 : 0 },
      tSnow: { value: snowyMission(this.s) ? snowTexture() : null },
      tAsphalt: { value: terrainTexture('asphalt') },
      tGrass: { value: terrainTexture('grass') },
      tDirt: { value: terrainTexture('dirt') },
      ...Object.fromEntries(Object.entries(SYNTY_LOOK).map(([k, v]) => [k, c(v)])),
    };
    extendShader(m, 'synty-ground', (shader) => {
      Object.assign(shader.uniforms, uniforms);
      shader.vertexShader = shader.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 vGPos;\nvarying vec3 vGNormal;').replace(
        '#include <project_vertex>',
        `#include <project_vertex>
        {
          vec4 gw = vec4(transformed, 1.0);
          vec3 gn = objectNormal;
          #ifdef USE_INSTANCING
            gw = instanceMatrix * gw;
            gn = mat3(instanceMatrix) * gn;
          #endif
          vGPos = (modelMatrix * gw).xyz;
          vGNormal = normalize(mat3(modelMatrix) * gn);
        }`,
      );
      shader.fragmentShader = shader.fragmentShader
        .replace('#include <common>', `#include <common>\n${SYNTY_GROUND_GLSL}`)
        .replace('#include <color_fragment>', `#include <color_fragment>\n${SYNTY_GROUND_FRAGMENT}`);
    });
    return m;
  }

  dispose(): void {
    this.texture.dispose();
  }
}

function random(seed: number): () => number {
  let s = seed >>> 0 || 1;
  return () => {
    s = (s + 0x6d2b79f5) >>> 0;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** Stable pseudo-random value in [0, 1) for a lattice point. */
function hash2(x: number, y: number, salt: number): number {
  let h = Math.imul(x, 374761393) + Math.imul(y, 668265263) + Math.imul(salt, 2147483647);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

/** Smooth value noise, for where the details clump. */
function noise2(x: number, y: number, salt: number): number {
  const ix = Math.floor(x);
  const iy = Math.floor(y);
  const fx = x - ix;
  const fy = y - iy;
  const u = fx * fx * (3 - 2 * fx);
  const v = fy * fy * (3 - 2 * fy);
  const a = hash2(ix, iy, salt);
  const b = hash2(ix + 1, iy, salt);
  const c = hash2(ix, iy + 1, salt);
  const d = hash2(ix + 1, iy + 1, salt);
  return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}

// ------------------------------------------------------- textures and cover

const terrainTextures = new Map<TerrainId, THREE.Texture>();

/** A tiling ground texture (syntyManifest: TERRAIN), loaded once. */
function terrainTexture(id: TerrainId): THREE.Texture {
  let t = terrainTextures.get(id);
  if (!t) {
    t = new THREE.TextureLoader().load(`${import.meta.env.BASE_URL}${terrainFile(id)}`);
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    t.colorSpace = THREE.SRGBColorSpace;
    t.anisotropy = 4;
    terrainTextures.set(id, t);
  }
  return t;
}

/** How much the asphalt texture is lifted (its average is a 27 % grey): a whole apron of it read as black. */
const ASPHALT_GAIN = 2.6;

/** Colours of the Synty ground (sRGB): flat colours, and tints over the tiling textures. */
const SYNTY_LOOK = {
  // Pale and only a little warm, as the paving in Synty's Apocalypse captures (the warmer sun adds the rest).
  sPave: 0xbdb5ac,
  sKerb: 0xd3cbc1,
  sAsphalt: 0xc9b8b0,
  /** Moss in clumps on the paving and in the gutters, and grime. */
  sMoss: 0x6c6e3e,
  sGrime: 0x564840,
  /** Dust and dirt blown over the paving and into the road. */
  sDust: 0x9c8a72,
  sPaint: 0xe6e2d4,
  sYellow: 0xd8aa3a,
  sGrass: 0xd2dab8,
  sGrassDry: 0xf0dca2,
  sDirt: 0xffffff,
  sConcrete: 0x9a9a96,
  sDeck: 0x8a8c8e,
};

const SYNTY_GROUND_GLSL = /* glsl */ `
  uniform sampler2D groundMap;
  uniform vec2 groundSize;
  uniform float gBiome;
  uniform float gTarmac;
  uniform float gSnow;
  uniform sampler2D tSnow;
  uniform sampler2D tAsphalt;
  uniform sampler2D tGrass;
  uniform sampler2D tDirt;
  ${Object.keys(SYNTY_LOOK).map((k) => `uniform vec3 ${k};`).join('\n  ')}
  varying vec3 vGPos;
  varying vec3 vGNormal;
  float sHash(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
  }
  float sNoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(sHash(i), sHash(i + vec2(1.0, 0.0)), f.x), mix(sHash(i + vec2(0.0, 1.0)), sHash(i + vec2(1.0, 1.0)), f.x), f.y);
  }
  float sFbm(vec2 p) {
    return sNoise(p) * 0.55 + sNoise(p * 2.07 + 11.3) * 0.3 + sNoise(p * 4.13 + 5.7) * 0.15;
  }
  vec4 sTile(ivec2 t) {
    return texelFetch(groundMap, clamp(t, ivec2(0), ivec2(groundSize) - 1), 0);
  }
  /** 1 if tile t is of kind k on this level (k −1: open ground marked as a lawn). */
  float sIs(ivec2 t, float k, float level) {
    vec4 g = sTile(t);
    float kind = floor(g.r * 255.0 + 0.5);
    if (abs(g.g - level) > 0.002) return 0.0;
    if (k < -0.5) return kind < 0.5 && floor(g.b * 255.0 + 0.5) > 2.5 ? 1.0 : 0.0;
    return abs(kind - k) < 0.5 ? 1.0 : 0.0;
  }
  /** How much point p is in tiles of kind k: 1 in their middle, blended between tile middles. */
  float sMask(vec2 p, float k, float level) {
    vec2 q = p - 0.5;
    ivec2 i = ivec2(floor(q));
    vec2 f = fract(q);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(sIs(i, k, level), sIs(i + ivec2(1, 0), k, level), f.x), mix(sIs(i + ivec2(0, 1), k, level), sIs(i + ivec2(1, 1), k, level), f.x), f.y);
  }
  /** Distance from p to the nearest tile of kind k round tile t (2 if none). */
  float sNear(vec2 p, ivec2 t, float k, float level) {
    float d = 2.0;
    for (int y = -1; y <= 1; y++) {
      for (int x = -1; x <= 1; x++) {
        ivec2 n = t + ivec2(x, y);
        if (sIs(n, k, level) < 0.5) continue;
        d = min(d, length(max(abs(p - vec2(n) - 0.5) - 0.5, 0.0)));
      }
    }
    return d;
  }
  /** Distance to the nearest border of a jittered cell pattern: thin lines are cracks. */
  float sCrack(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    float d1 = 9.0;
    float d2 = 9.0;
    for (int y = -1; y <= 1; y++) {
      for (int x = -1; x <= 1; x++) {
        vec2 o = vec2(float(x), float(y));
        vec2 c = o + vec2(sHash(i + o), sHash(i + o + 17.31)) - f;
        float d = dot(c, c);
        if (d < d1) {
          d2 = d1;
          d1 = d;
        } else if (d < d2) {
          d2 = d;
        }
      }
    }
    return sqrt(d2) - sqrt(d1);
  }
  /** Road paint along a street's middle (the tile says its axis and where across it the line is). */
  float sLine(vec2 p, ivec2 t, vec4 own) {
    float axis = floor(own.b * 255.0 + 0.5);
    if (axis < 0.5 || axis > 2.5) return 0.0;
    vec2 local = p - vec2(t);
    float across = axis < 1.5 ? local.y : local.x;
    float along = axis < 1.5 ? p.x : p.y;
    return step(abs(across - own.a), 0.045) * step(fract(along / 1.75), 0.5) * smoothstep(0.2, 0.4, sNoise(p * 2.0 + 5.0));
  }`;

/**
 * The ground of each biome, smooth: in town, paving with its slabs and a kerb
 * along the streets (rounded at the corners), lawns away from them, asphalt
 * with a centre line, a gutter, patches and cracks; in the wilds, grass in
 * broad lush and dry patches, a dirt track whose edges wander, ploughed
 * fields; in facilities, concrete with expansion joints and stains, asphalt
 * yards with yellow lines. Tiles only say what the ground is: nothing lines
 * up with them.
 */
const SYNTY_GROUND_FRAGMENT = /* glsl */ `
  if (vGNormal.y > 0.5) {
    vec2 p = vGPos.xz;
    ivec2 t = ivec2(floor(p));
    vec4 own = sTile(t);
    float k = floor(own.r * 255.0 + 0.5);
    float level = own.g;
    float big = sFbm(p * 0.07);
    float mid = sFbm(p * 0.33 + 3.1);
    vec2 warp = (vec2(sNoise(p * 0.8), sNoise(p * 0.8 + 17.0)) - 0.5) * 0.7;
    // Synty's road texture is nearly black (Town lights it hard): lifted to a dark grey that overlays read on.
    vec3 asphalt = texture2D(tAsphalt, p / 2.2).rgb * sAsphalt * ${ASPHALT_GAIN.toFixed(2)} * (0.9 + 0.18 * big) * (0.97 + 0.06 * mid);
    vec3 grass = texture2D(tGrass, p / 3.2).rgb * mix(sGrass, sGrassDry, smoothstep(0.42, 0.72, big)) * (0.94 + 0.12 * mid);
    vec3 dirt = texture2D(tDirt, p / 2.6).rgb * sDirt * (0.92 + 0.14 * big);
    vec3 c;
    if (k > 2.5 && k < 3.5) {
      // A deck or platform up a storey.
      c = sDeck * (0.94 + 0.08 * mid);
    } else if (gBiome < 0.5) {
      if (k < 0.5) {
        // Paving (slabs that never line up with the tiles), lawns away from the streets, a kerb along them.
        // The slabs are oblong and laid in a running bond (each row half a slab along from the last), their
        // joints thin, smoothed and worn away in patches: a square's paving never reads as a grid.
        vec2 sp = p / vec2(0.86, 0.58) + vec2(0.31, 0.17);
        sp.x += 0.5 * mod(floor(sp.y), 2.0);
        vec2 sf = fract(sp);
        vec2 edge = min(sf, 1.0 - sf);
        vec2 seam = 1.0 - smoothstep(vec2(0.025), vec2(0.025) + 1.5 * fwidth(sp), edge);
        float joint = max(seam.x, seam.y) * (0.3 + 0.7 * smoothstep(0.3, 0.7, sNoise(p * 0.9 + 7.0)));
        vec3 pave = sPave * (0.965 + 0.05 * sHash(floor(sp))) * (1.0 - 0.1 * joint);
        pave *= 1.0 - 0.14 * smoothstep(0.55, 0.8, big);
        float lawn = smoothstep(0.42, 0.6, sMask(p + warp * 0.6, -1.0, level) + (mid - 0.5) * 0.25);
        c = mix(pave, grass, lawn);
        float ds = sNear(p, t, 1.0, level);
        // Grime in broad patches, and moss in clumps, more of it by the kerb.
        c = mix(c, c * sGrime * 2.2, smoothstep(0.55, 0.82, sFbm(p * 0.4 + 17.0)) * 0.35 * (1.0 - lawn));
        // Dust blown over it in drifts, and the slabs cracked where the paving is oldest.
        c = mix(c, sDust * (0.9 + 0.2 * sNoise(p * 3.0)), smoothstep(0.6, 0.78, sFbm(p * 0.3 + 71.0)) * 0.45 * (1.0 - lawn));
        float split = 1.0 - smoothstep(0.02, 0.04, sCrack(p * 1.3 + 9.0));
        c *= 1.0 - 0.22 * split * smoothstep(0.58, 0.72, sFbm(p * 0.25 + 13.0)) * (1.0 - lawn);
        float mossy = smoothstep(0.66, 0.69, sFbm(p * 0.6 + 29.0) + (1.0 - smoothstep(0.0, 0.7, ds)) * 0.08);
        c = mix(c, sMoss * (0.8 + 0.4 * sNoise(p * 6.0)), mossy * 0.85 * (1.0 - lawn));
        float kerb = 1.0 - smoothstep(0.105, 0.125, ds);
        c = mix(c, sKerb * (0.82 + 0.18 * smoothstep(0.0, 0.03, ds)) * (0.96 + 0.06 * mid), kerb);
      } else if (k < 1.5 || k > 5.5) {
        // A street, or a yard's tarmac (no paint on it).
        c = asphalt;
        // Repairs, a gutter by the kerb, the centre line, cracks.
        c *= 1.0 - 0.09 * smoothstep(0.6, 0.62, sNoise(floor(p * 1.3) * 0.41 + 9.0) * 0.7 + sNoise(p * 0.5) * 0.3);
        float dk = sNear(p, t, 0.0, level);
        c *= mix(0.8, 1.0, smoothstep(0.02, 0.22, dk));
        // Oil and dirt, and weeds in the gutter.
        c *= 1.0 - 0.16 * smoothstep(0.62, 0.8, sFbm(p * 0.7 + 41.0));
        c = mix(c, sMoss * (0.8 + 0.4 * sNoise(p * 6.0)), smoothstep(0.7, 0.74, sFbm(p * 0.9 + 5.0) + (1.0 - smoothstep(0.0, 0.25, dk)) * 0.2) * 0.8);
        c = mix(c, sPaint, sLine(p, t, own) * 0.9);
        // Zebra crossing: bars along the street, across its width, worn in places.
        float zebra = floor(own.b * 255.0 + 0.5);
        if (k < 1.5 && zebra > ${CROSSWALK - 0.5}) {
          vec2 zl = p - vec2(t);
          float across = zebra < ${CROSSWALK + 0.5} ? p.y : p.x;
          float inTile = zebra < ${CROSSWALK + 0.5} ? zl.x : zl.y;
          float bar = step(0.5, fract(across * 2.2)) * step(0.12, inTile) * step(inTile, 0.88);
          c = mix(c, sPaint, bar * 0.88 * smoothstep(0.18, 0.4, sNoise(p * 2.6 + 9.0)));
        }
        // Cracks in the worn stretches (a good part of a town nobody mends), and dust blown into the road.
        float crack = 1.0 - smoothstep(0.025, 0.045, sCrack(p * 0.9 + 3.0));
        c *= 1.0 - 0.34 * crack * smoothstep(0.56, 0.7, sFbm(p * 0.22 + 61.0));
        c = mix(c, sDust * 0.8 * (0.9 + 0.2 * sNoise(p * 3.0)), smoothstep(0.62, 0.8, sFbm(p * 0.35 + 83.0)) * 0.35);
      } else {
        c = sPave;
      }
    } else if (gBiome < 1.5) {
      // Grass, a dirt track with wandering edges, ploughed fields.
      c = grass;
      // Tarmac (#25): the yards (a forecourt, a car park, a sawmill's yard), and at the forest stop its road.
      float m = sMask(p + warp * 0.2, 6.0, level) + gTarmac * sMask(p + warp * 0.2, 1.0, level);
      float road = smoothstep(0.42, 0.52, m);
      if (gTarmac < 0.5) {
        float track = smoothstep(0.36, 0.64, sMask(p + warp, 1.0, level) + (mid - 0.5) * 0.35);
        c = mix(c, dirt, track);
      }
      if (m > 0.001) {
        // On a gravel verge, worn, cracked and dusty; a centre line down the road where it runs straight.
        c = mix(c, dirt * 0.88, smoothstep(0.2, 0.38, m));
        vec3 tar = asphalt * (1.0 - 0.16 * smoothstep(0.62, 0.8, sFbm(p * 0.7 + 41.0)));
        tar = mix(tar, sPaint, sLine(p, t, own) * 0.85);
        float crack = 1.0 - smoothstep(0.025, 0.045, sCrack(p * 0.9 + 3.0));
        tar *= 1.0 - 0.32 * crack * smoothstep(0.54, 0.7, sFbm(p * 0.22 + 61.0));
        tar = mix(tar, sDust * 0.8 * (0.9 + 0.2 * sNoise(p * 3.0)), smoothstep(0.6, 0.8, sFbm(p * 0.35 + 83.0)) * 0.4);
        c = mix(c, tar, road);
      }
      // Worn ground: bare patches here and there.
      c = mix(c, dirt * 0.95, smoothstep(0.7, 0.78, sFbm(p * 0.45 + 41.0)) * 0.8 * (1.0 - road));
      float rows = 0.0;
      float axis = 0.0;
      for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
          ivec2 n = t + ivec2(x, y);
          if (sIs(n, 5.0, level) < 0.5) continue;
          rows += 1.0;
          axis = floor(sTile(n).b * 255.0 + 0.5);
        }
      }
      if (abs(k - 5.0) < 0.5 || rows > 1.5) {
        float across = axis < 1.5 ? p.y : p.x;
        float ridge = smoothstep(0.2, 0.5, abs(fract(across * 2.5) - 0.5) * 2.0);
        c = dirt * (0.72 + 0.3 * ridge) * (0.95 + 0.1 * mid);
      }
    } else {
      if (k < 0.5) {
        // Concrete slabs with joints off the grid, oil stains.
        vec2 sp = p / 2.4 + vec2(0.37, 0.71);
        vec2 sf = fract(sp) * 2.4;
        float joint = max(step(sf.x, 0.035), step(sf.y, 0.035));
        c = sConcrete * (0.95 + 0.08 * sHash(floor(sp))) * (0.93 + 0.1 * big) * (1.0 - 0.2 * joint);
        c *= 1.0 - 0.3 * smoothstep(0.72, 0.8, sFbm(p * 0.6 + 23.0));
      } else if (k < 1.5 || k > 5.5) {
        c = asphalt * 1.08;
        float dk = sNear(p, t, 0.0, level);
        // A yellow line along the yard's edge, and down its middle.
        c = mix(c, sYellow, (1.0 - smoothstep(0.03, 0.045, abs(dk - 0.22))) * step(dk, 1.0) * smoothstep(0.2, 0.4, sNoise(p * 2.0 + 5.0)) * 0.85);
        c = mix(c, sYellow, sLine(p, t, own) * 0.85);
        float crack = 1.0 - smoothstep(0.025, 0.045, sCrack(p * 0.9 + 7.0));
        c *= 1.0 - 0.28 * crack * smoothstep(0.66, 0.76, sFbm(p * 0.22 + 31.0));
      } else {
        c = sConcrete;
      }
    }
    // Deep cover on open ground and roofs; slushy streets retain their darker routes and markings.
    if (gSnow > 0.5 && abs(k - 2.0) > 0.5) {
      vec3 snow = texture2D(tSnow, p / 3.0).rgb * vec3(0.89, 0.95, 1.0);
      float road = sMask(p, 1.0, level) + sMask(p, 6.0, level);
      float cover = mix(0.97, 0.5 + 0.26 * smoothstep(0.28, 0.72, mid), clamp(road, 0.0, 1.0));
      c = mix(c, snow * (0.94 + 0.08 * big), cover);
    }
    diffuseColor.rgb = c;
  }`;

/** Ground cover per tile, by what the floor is (before the clumping): Synty props, cosmetic. */
const COVER: Record<Biome, Partial<Record<number, [PropId, number][]>>> = {
  // Town (second look against the capture: «muy vestidas»): litter, rubble and trash spread over paving and road.
  city: {
    [OPEN]: [['papers', 0.06], ['pebbles', 0.09], ['trash', 0.025]],
    [LAWN + 10]: [['tuftLow', 0.6], ['tuft', 0.25], ['flowers', 0.06]],
    [STREET]: [['papers', 0.045], ['pebbles', 0.07], ['trash', 0.02]],
    [YARD]: [['papers', 0.04], ['pebbles', 0.08], ['trash', 0.02]],
    [ROOF]: [['pebbles', 0.08], ['papers', 0.02]],
  },
  wilds: {
    [OPEN]: [['tuft', 0.55], ['tuftLow', 0.75], ['tuftWide', 0.3], ['flowers', 0.12], ['pebbles', 0.06]],
    [STREET]: [['pebbles', 0.18], ['tuftLow', 0.12]],
    [YARD]: [['pebbles', 0.08], ['papers', 0.02], ['tuftLow', 0.04]],
    [HILL]: [['tuft', 0.5], ['tuftLow', 0.5], ['pebbles', 0.08]],
    [FURROW]: [['pebbles', 0.05]],
  },
  facility: {
    [OPEN]: [['papers', 0.03], ['pebbles', 0.07], ['grassBlob', 0.03]],
    [STREET]: [['papers', 0.02], ['pebbles', 0.05]],
    [YARD]: [['papers', 0.02], ['pebbles', 0.05]],
    [ROOF]: [['pebbles', 0.05]],
  },
};

/** A manhole every this many tiles down a street's middle (some 24 m), a drain in each gutter every this many. */
const MANHOLE_EVERY = 16;
const DRAIN_EVERY = 13;

/**
 * Small Synty props scattered off the grid: grass tufts and flowers in the
 * wilds and on lawns, papers and pebbles in town, leaves under trees, weeds
 * at the kerb, a drain now and then in the gutter and manholes down the
 * middle of streets. Where they fall clumps and thins out across tiles.
 * Cosmetic: no shadows, no picking. `density` scales how much (Ajustes →
 * Efectos: fewer on slower machines). With `area` (a rebuild of a few chunks:
 * mapView), only on its tiles: each tile's cover is its own, and stays on it.
 */
export function scatterCover(s: GameState, ground: GroundMap, density = 1, area: Uint8Array | null = null): { id: PropId; matrix: THREE.Matrix4 }[] {
  const out: { id: PropId; matrix: THREE.Matrix4 }[] = [];
  const { width: w, height: h } = s;
  // On the forest road's tarmac (#25), what a town's street has.
  const plan = s.biome === 'wilds' && s.site === 'roadside' ? { ...COVER.wilds, [STREET]: COVER.city[STREET]! } : (COVER[s.biome] ?? COVER.city);
  const decor = s.decor ?? [];
  const data = ground.texture.image.data as Uint8Array;
  const at = (x: number, y: number): TileKind | null => (x < 0 || y < 0 || x >= w || y >= h ? null : s.tiles[y * w + x]!);
  const elev = (x: number, y: number) => s.elev[y * w + x] ?? 0;
  const quaternion = new THREE.Quaternion();
  const up = new THREE.Vector3(0, 1, 0);
  const put = (id: PropId, x: number, y: number, z: number, turn: number, size: number) => {
    quaternion.setFromAxisAngle(up, turn);
    out.push({ id, matrix: new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), quaternion, new THREE.Vector3(size, size, size)) });
  };
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = y * w + x;
      if (s.tiles[i] !== 'floor' || (area && area[i] === 0)) continue;
      const e = elev(x, y);
      // A siege's watchtower deck (docs/FASE12.md §10) is planks: nothing grows on it.
      if (e && s.mission.siege) continue;
      const kind = ground.kinds[i]!;
      if (kind === INDOOR) continue;
      const lawn = s.biome === 'city' && kind === OPEN && data[i * 4 + 2] === LAWN;
      const list: [PropId, number][] = [...(plan[lawn ? LAWN + 10 : kind] ?? [])];
      const r = random(hash2(x, y, 23) * 4294967296);
      const near = (test: (k: TileKind | null, x: number, y: number) => boolean) => DIRS4.some((d) => test(at(x + d.x, y + d.y), x + d.x, y + d.y));
      // Leaves round trees, weeds where the paving meets the street.
      const tree = (k: TileKind | null, nx: number, ny: number) => k === 'high' && ['tree', 'streetTree'].includes(decorOf(decor[ny * w + nx])?.kind ?? '');
      if (near(tree)) list.push(['leaves', 0.5]);
      if (s.biome === 'city' && kind === OPEN && near((_k, nx, ny) => ground.kinds[ny * w + nx] === STREET)) list.push(['grassBlob', 0.16]);
      // The middle of a square or a car park (open paving all round: the squad's own square among them), so it
      // never reads as an empty slab: weeds through the cracks, leaves blown in, more litter. All of it flat.
      const paved = (nx: number, ny: number) => nx >= 0 && ny >= 0 && nx < w && ny < h && s.tiles[ny * w + nx] === 'floor' && ground.kinds[ny * w + nx] === OPEN && data[(ny * w + nx) * 4 + 2] !== LAWN && elev(nx, ny) === e;
      if (s.biome === 'city' && kind === OPEN && !lawn && DIRS8.every((d) => paved(x + d.x, y + d.y))) list.push(['grassBlob', 0.12], ['leaves', 0.06], ['papers', 0.05], ['pebbles', 0.06]);
      // Margins on the sides that walls, cover or a higher floor close.
      const margin = DIRS4.map((d) => {
        const k = at(x + d.x, y + d.y);
        return k === null || (k !== 'floor' && k !== 'doorOpen') || elev(x + d.x, y + d.y) > e ? 0.2 : 0;
      });
      const clump = 0.25 + 1.5 * noise2(x / 4.5, y / 4.5, 3);
      for (const [id, amount] of list) {
        // The winter has buried flowers, leaves and short grass; a few tall tufts still show.
        if (snowyMission(s) && ['flowers', 'leaves', 'tuftLow', 'grassBlob'].includes(id)) continue;
        let n = Math.floor(amount * density * (snowyMission(s) && (id === 'tuft' || id === 'tuftWide') ? 0.15 : 1) * (id === 'leaves' ? 1 : clump) + r());
        while (n-- > 0) {
          const px = THREE.MathUtils.clamp(x + r(), x + margin[3]!, x + 1 - margin[1]!);
          const pz = THREE.MathUtils.clamp(y + r(), y + margin[0]!, y + 1 - margin[2]!);
          put(id, px, e * STOREY, pz, r() * Math.PI * 2, 0.8 + r() * 0.45);
        }
      }
      // Street furniture in the ground, spaced as a street has it (playtest 18/10, #11: there were far too many): a
      // manhole down the middle every MANHOLE_EVERY tiles along each street, a drain in each gutter every
      // DRAIN_EVERY, each street's own offset so they never line up across the map.
      if (s.biome === 'city' && kind === STREET && e === 0) {
        const axis = data[i * 4 + 2]!;
        if (axis) {
          const [along, across] = axis === 1 ? [x, y] : [y, x];
          if ((along + Math.floor(hash2(across, axis, 34) * MANHOLE_EVERY)) % MANHOLE_EVERY === 0) put('manhole', x + 0.5, 0.002, y + 0.5, (hash2(x, y, 32) * 4) | 0, 1);
        }
        const kerb = DIRS4.findIndex((d) => ground.kinds[(y + d.y) * w + x + d.x] === OPEN && at(x + d.x, y + d.y) !== null);
        if (kerb >= 0) {
          const d = DIRS4[kerb]!;
          // Along the kerb: x for one to the north or south, y for one to the east or west.
          const [along, across] = d.y !== 0 ? [x, y * 4 + kerb] : [y, x * 4 + kerb];
          if ((along + Math.floor(hash2(across, kerb, 35) * DRAIN_EVERY)) % DRAIN_EVERY === 0) put('drain', x + 0.5 + d.x * 0.36, 0.002, y + 0.5 + d.y * 0.36, Math.atan2(d.x, d.y), 1);
        }
      }
    }
  }
  return out;
}
