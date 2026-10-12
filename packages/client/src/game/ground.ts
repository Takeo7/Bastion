import * as THREE from 'three';
import { decorOf, DIRS4, type Biome, type DecorKind, type GameState, type TileKind } from '@bastion/engine';
import { extendShader } from './fogOfWar';
import { STOREY } from './coords';
import { GAME_VOXEL, voxelField, type Colour } from './voxel';

// The ground as continuous terrain (second playtest: «los mapas siguen siendo
// demasiado cuadrículas»). Tiles still say what the floor is, but the colour
// is worked out in world space, per voxel cell: broad patches that run across
// tiles, borders between open ground and streets that wander and fray (grass
// over the edge of a dirt track, kerbs and gutters in town), worn patches,
// cracks, puddles and road paint. On top, small voxel details (grass tufts,
// pebbles, leaves, litter, rubble…) are scattered off the grid, cosmetic only.

/** Floor kinds, as the ground shader reads them from the red channel. */
const OPEN = 0;
const STREET = 1;
const INDOOR = 2;
const ROOF = 3;
/** Top of a hill in the wilds: grass, like open ground. */
const HILL = 4;
/** A furrow of a ploughed field: one tile wide, along the field. */
const FURROW = 5;

interface Look {
  open: number;
  /** Open ground in its broad patches (drier grass, dirtier paving). */
  openAlt: number;
  worn: number;
  /** Open ground along a street: the kerb, a trampled verge. */
  kerb: number;
  street: number;
  streetAlt: number;
  /** Street along open ground: the gutter, or the grass over a track's edge. */
  gutter: number;
  floor: number;
  roof: number;
  paint: number;
  puddle: number;
  /** How far borders wander (tiles), how much the paving cracks, how many puddles, how worn. */
  amounts: [number, number, number, number];
  /** The street's edge is only grass here and there (wilds). */
  frayed: boolean;
  /** Indoor floors: wooden boards, or steel plates (facilities). */
  plates: boolean;
}

// The base colours are mapView's palettes; the rest are their variations.
const LOOKS: Record<Biome, Look> = {
  city: {
    open: 0x9a9384,
    openAlt: 0x8d887b,
    worn: 0x7c776b,
    kerb: 0xb8b1a1,
    street: 0x585d63,
    streetAlt: 0x4f5359,
    gutter: 0x45484d,
    floor: 0x8a705a,
    roof: 0x7d7a74,
    paint: 0xd9d5c6,
    puddle: 0x34404c,
    amounts: [0.22, 1, 1, 0.55],
    frayed: false,
    plates: false,
  },
  wilds: {
    open: 0x6a8a45,
    openAlt: 0x83904a,
    worn: 0x7d6c4b,
    kerb: 0x748442,
    street: 0x9a8460,
    streetAlt: 0x8b7654,
    gutter: 0x66843f,
    floor: 0x86705a,
    roof: 0x7a6a50,
    paint: 0x9a8460,
    puddle: 0x4d4a3e,
    amounts: [0.62, 0, 0.6, 0.85],
    frayed: true,
    plates: false,
  },
  facility: {
    open: 0x6a7078,
    openAlt: 0x737880,
    worn: 0x575b60,
    kerb: 0x80868d,
    street: 0x4a4f56,
    streetAlt: 0x44494f,
    gutter: 0x3d4147,
    floor: 0x7f8892,
    roof: 0x5a6068,
    paint: 0xc9a23a,
    puddle: 0x2c323a,
    amounts: [0.14, 0.8, 0.45, 0.7],
    frayed: false,
    plates: true,
  },
};

const GROUND_GLSL = /* glsl */ `
  uniform sampler2D groundMap;
  uniform vec2 groundSize;
  uniform vec3 gOpen;
  uniform vec3 gOpenAlt;
  uniform vec3 gWorn;
  uniform vec3 gKerb;
  uniform vec3 gStreet;
  uniform vec3 gStreetAlt;
  uniform vec3 gGutter;
  uniform vec3 gFloor;
  uniform vec3 gRoof;
  uniform vec3 gPaint;
  uniform vec3 gPuddle;
  uniform vec4 gAmounts;
  float gHash(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
  }
  float gNoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(gHash(i), gHash(i + vec2(1.0, 0.0)), f.x), mix(gHash(i + vec2(0.0, 1.0)), gHash(i + vec2(1.0, 1.0)), f.x), f.y);
  }
  /** The kind at world point p if it is open ground or street on the same level; -1 otherwise. */
  float gOutdoorAt(vec2 p, float level) {
    ivec2 t = ivec2(floor(p));
    if (t.x < 0 || t.y < 0 || float(t.x) >= groundSize.x || float(t.y) >= groundSize.y) return -1.0;
    vec4 g = texelFetch(groundMap, t, 0);
    float k = floor(g.r * 255.0 + 0.5);
    return k < 1.5 && abs(g.g - level) < 0.002 ? k : -1.0;
  }
  /** Distance to the nearest border of a jittered cell pattern: thin lines are cracks. */
  float gCrack(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    float d1 = 9.0;
    float d2 = 9.0;
    for (int y = -1; y <= 1; y++) {
      for (int x = -1; x <= 1; x++) {
        vec2 o = vec2(float(x), float(y));
        vec2 c = o + vec2(gHash(i + o), gHash(i + o + 17.31)) - f;
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
  }`;

function groundFragment(look: Look): string {
  return /* glsl */ `
  float gWetness = 0.0;
  if (vVxNormal.y > 0.5) {
    // Everything is worked out per voxel cell, so borders and patterns step like the art.
    vec2 gp = (floor(vVxPos.xz * 16.0) + 0.5) / 16.0;
    ivec2 gTile = clamp(ivec2(floor(gp)), ivec2(0), ivec2(groundSize) - 1);
    vec4 gOwn = texelFetch(groundMap, gTile, 0);
    float gBase = floor(gOwn.r * 255.0 + 0.5);
    float gK = gBase;
    float gEdge = 0.0;
    float gFine = gNoise(gp * 3.1 + 57.0);
    if (gBase < 1.5) {
      // Open ground and streets on the same level: the border between them wanders and frays.
      vec2 gw = vec2(gNoise(gp * 1.3), gNoise(gp * 1.3 + 17.0)) - 0.5;
      gw += (vec2(gNoise(gp * 4.3 + 3.0), gNoise(gp * 4.3 + 9.0)) - 0.5) * 0.6;
      vec2 gq = gp + gw * gAmounts.x;
      float gWarped = gOutdoorAt(gq, gOwn.g);
      if (gWarped >= 0.0) gK = gWarped;
      float gOther = 1.0 - gK;
      gEdge = max(
        max(float(gOutdoorAt(gq + vec2(0.13, 0.0), gOwn.g) == gOther), float(gOutdoorAt(gq - vec2(0.13, 0.0), gOwn.g) == gOther)),
        max(float(gOutdoorAt(gq + vec2(0.0, 0.13), gOwn.g) == gOther), float(gOutdoorAt(gq - vec2(0.0, 0.13), gOwn.g) == gOther))
      );
    }
    float gBroad = gNoise(gp * 0.16) * 0.65 + gNoise(gp * 0.43 + 7.0) * 0.35;
    float gPatch = gNoise(gp * 0.9 + 31.0);
    float gCracks = 0.0;
    vec3 gc;
    if (gK < 0.5 || gK == ${HILL}.0) {
      gc = mix(gOpen, gOpenAlt, smoothstep(0.35, 0.75, gBroad)) * (0.93 + 0.14 * gPatch);
      float worn = smoothstep(0.64, 0.7, gNoise(gp * 0.6 + 41.0) * 0.75 + gFine * 0.25);
      gc = mix(gc, gWorn, worn * gAmounts.w);
      gc = mix(gc, gKerb, gEdge);
      gCracks = 0.6;
    } else if (gK < 1.5) {
      gc = mix(gStreet, gStreetAlt, smoothstep(0.3, 0.8, gBroad)) * (0.93 + 0.14 * gPatch);
      gc = mix(gc, gGutter, gEdge * ${look.frayed ? 'step(0.45, gFine)' : '1.0'});
      // Road paint: a dashed centre line, worn away in places.
      float gAxis = floor(gOwn.b * 255.0 + 0.5);
      if (gAxis > 0.5 && gBase > 0.5) {
        vec2 gLocal = gp - vec2(gTile);
        float gAcross = gAxis < 1.5 ? gLocal.y : gLocal.x;
        float gAlong = gAxis < 1.5 ? gp.x : gp.y;
        float gLine = step(abs(gAcross - gOwn.a), 0.07) * step(fract(gAlong / 1.75), 0.5);
        gc = mix(gc, gPaint, gLine * smoothstep(0.25, 0.4, gNoise(gp * 2.0 + 5.0)) * 0.85);
      }
      gWetness = smoothstep(0.72, 0.74, gNoise(gp * 0.55 + 90.0) * 0.75 + gFine * 0.25) * gAmounts.z * (1.0 - gEdge);
      gCracks = 1.0;
    } else if (gK < 2.5) {
      ${
        look.plates
          ? `// Steel plates, staggered, that never line up with the tiles.
      vec2 gPlate = gp / vec2(1.5, 0.75);
      gPlate.x += floor(gPlate.y) * 0.5;
      vec2 gIn = fract(gPlate) * vec2(1.5, 0.75);
      float gSeam = max(step(gIn.x, 0.07), step(gIn.y, 0.07));
      gc = gFloor * (0.94 + 0.1 * gHash(floor(gPlate))) * (1.0 - 0.18 * gSeam);`
          : `// Boards of uneven lengths, their joints staggered.
      float gRow = floor(gp.y * 4.0);
      float gRun = gp.x + gHash(vec2(gRow, 1.7)) * 3.0;
      gc = gFloor * (0.84 + 0.26 * gHash(vec2(floor(gRun / 1.4), gRow)));
      gc *= 1.0 - 0.12 * step(fract(gp.y * 4.0), 0.25) - 0.2 * step(fract(gRun / 1.4) * 1.4, 0.0625);`
      }
      gc *= 0.93 + 0.1 * gBroad;
    } else if (gK < 3.5) {
      gc = gRoof * (0.9 + 0.12 * gPatch) * (0.96 + 0.08 * gFine);
      gc *= 1.0 - 0.12 * smoothstep(0.6, 0.7, gBroad + gFine * 0.1);
      gCracks = 0.8;
    } else {
      // Ridges along the furrow.
      float gAcross = floor(gOwn.b * 255.0 + 0.5) < 1.5 ? gp.y : gp.x;
      gc = gStreet * (0.8 + 0.28 * step(0.5, fract(gAcross * 3.0))) * (0.95 + 0.1 * gFine);
    }
    float gCrackLine = 1.0 - step(0.075, gCrack(gp * 1.5 + 3.0));
    gc *= 1.0 - 0.42 * gCrackLine * smoothstep(0.55, 0.68, gNoise(gp * 0.33 + 61.0)) * gCracks * gAmounts.y;
    gc = mix(gc, gc * 0.45 + gPuddle * 0.5, gWetness);
    diffuseColor.rgb = gc;
  }`;
}

/** The colours of a look as uniforms (linear, as three works). */
function lookUniforms(look: Look): Record<string, THREE.IUniform> {
  const c = (hex: number) => ({ value: new THREE.Color(hex) });
  return {
    gOpen: c(look.open),
    gOpenAlt: c(look.openAlt),
    gWorn: c(look.worn),
    gKerb: c(look.kerb),
    gStreet: c(look.street),
    gStreetAlt: c(look.streetAlt),
    gGutter: c(look.gutter),
    gFloor: c(look.floor),
    gRoof: c(look.roof),
    gPaint: c(look.paint),
    gPuddle: c(look.puddle),
    gAmounts: { value: new THREE.Vector4(...look.amounts) },
  };
}

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
    const data = new Uint8Array(w * h * 4);
    this.kinds = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const i = y * w + x;
        const e = s.elev[i] ?? 0;
        let kind = e > 0 ? (wilds ? HILL : ROOF) : s.ground?.[i] === 2 ? INDOOR : isStreet(x, y) ? STREET : OPEN;
        let axis = 0;
        let offset = 0;
        if (kind === STREET && wilds) {
          // A one-tile strip in the wilds is a furrow of a field, not the track.
          if (!isStreet(x, y - 1) && !isStreet(x, y + 1) && (isStreet(x - 1, y) || isStreet(x + 1, y))) [kind, axis] = [FURROW, 1];
          else if (!isStreet(x - 1, y) && !isStreet(x + 1, y) && (isStreet(x, y - 1) || isStreet(x, y + 1))) [kind, axis] = [FURROW, 2];
        } else if (kind === STREET) {
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
    this.texture = new THREE.DataTexture(data, w, h);
    this.texture.needsUpdate = true;
  }

  /** The material of the floor slabs: the voxel surface, painted as continuous terrain on top. */
  material(base: THREE.MeshStandardMaterial): THREE.MeshStandardMaterial {
    const look = LOOKS[this.s.biome] ?? LOOKS.city;
    const uniforms = { groundMap: { value: this.texture }, groundSize: { value: new THREE.Vector2(this.s.width, this.s.height) }, ...lookUniforms(look) };
    extendShader(base, `ground-${this.s.biome}`, (shader) => {
      Object.assign(shader.uniforms, uniforms);
      shader.fragmentShader = shader.fragmentShader
        .replace('#include <common>', `#include <common>\n${GROUND_GLSL}`)
        .replace('#include <color_fragment>', `#include <color_fragment>\n${groundFragment(look)}`)
        // Puddles shine a little (a sharp highlight bloomed into a white blot).
        .replace('#include <roughnessmap_fragment>', '#include <roughnessmap_fragment>\nroughnessFactor = mix(roughnessFactor, 0.5, gWetness);');
    });
    return base;
  }

  dispose(): void {
    this.texture.dispose();
  }
}

// ------------------------------------------------------------- details

type Detail = 'tuft' | 'flower' | 'weed' | 'pebbles' | 'leaves' | 'litter' | 'rubble' | 'twig' | 'scrap';

const VARIANTS: Record<Detail, number> = { tuft: 3, flower: 2, weed: 2, pebbles: 3, leaves: 2, litter: 2, rubble: 3, twig: 2, scrap: 2 };
/** Lying flat: squashed, so a sheet of paper or a leaf isn't a whole voxel thick. */
const FLAT: Partial<Record<Detail, number>> = { leaves: 0.5, litter: 0.4 };

/** Details per tile on each floor (before the clumping), by biome. */
const SCATTER: Record<Biome, Partial<Record<number, [Detail, number][]>>> = {
  city: {
    [OPEN]: [['litter', 0.08], ['pebbles', 0.12], ['weed', 0.07]],
    [STREET]: [['litter', 0.05], ['pebbles', 0.14]],
    [INDOOR]: [['litter', 0.1]],
    [ROOF]: [['pebbles', 0.18]],
  },
  wilds: {
    [OPEN]: [['tuft', 1.25], ['flower', 0.12], ['pebbles', 0.12], ['twig', 0.05]],
    [STREET]: [['pebbles', 0.45], ['tuft', 0.1]],
    [HILL]: [['tuft', 0.9], ['pebbles', 0.15]],
    [FURROW]: [['pebbles', 0.08]],
    [INDOOR]: [['twig', 0.06]],
  },
  facility: {
    [OPEN]: [['scrap', 0.1], ['pebbles', 0.12], ['weed', 0.04]],
    [STREET]: [['scrap', 0.05], ['pebbles', 0.1]],
    [INDOOR]: [['scrap', 0.07], ['litter', 0.04]],
    [ROOF]: [['pebbles', 0.1]],
  },
};

const TREES = new Set<DecorKind>(['streetTree', 'tree', 'bush']);

const colours = new Map<number, Colour>();
function col(hex: number): Colour {
  let c = colours.get(hex);
  if (!c) {
    const v = new THREE.Color(hex);
    colours.set(hex, (c = { r: v.r, g: v.g, b: v.b, glow: false }));
  }
  return c;
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

/** A detail as voxels: cell → colour. */
type Cells = Map<string, Colour>;

const GRASS = [col(0x4c7a3a), col(0x5c8c3e), col(0x76a14a)];
const STONES = [col(0x8a8780), col(0x6e6c66), col(0x9d998e), col(0x7a7468)];
const AUTUMN = [col(0xb5652a), col(0xc98a2e), col(0x8a5a2a), col(0x9aa03a)];

function makeCells(detail: Detail, r: () => number): Cells {
  const cells: Cells = new Map();
  const put = (x: number, y: number, z: number, c: Colour) => cells.set(`${x},${y},${z}`, c);
  const spot = (n: number) => Math.floor(r() * (2 * n + 1)) - n;
  const pick = <T>(list: T[]) => list[Math.floor(r() * list.length)]!;
  switch (detail) {
    case 'tuft':
    case 'weed':
    case 'flower': {
      const blades = detail === 'tuft' ? 4 + Math.floor(r() * 3) : 3;
      const tall = detail === 'weed' ? 2 : 3;
      let top: [number, number, number] = [0, 0, 0];
      for (let k = 0; k < blades; k++) {
        const x = spot(2);
        const z = spot(2);
        const h = 1 + Math.floor(r() * tall);
        for (let y = 0; y < h; y++) put(x, y, z, detail === 'weed' ? (y ? GRASS[0]! : col(0x40602f)) : y === h - 1 ? GRASS[2]! : y ? GRASS[1]! : GRASS[0]!);
        // Some blades bend over at the tip.
        if (h >= 3 && r() < 0.5) put(x + (r() < 0.5 ? -1 : 1), h - 1, z, GRASS[2]!);
        if (h > top[1]) top = [x, h, z];
      }
      if (detail === 'flower') {
        const petal = pick([col(0xe8c84a), col(0xeeeee6), col(0xa47ad6), col(0xd8584a)]);
        put(top[0], top[1], top[2], petal);
        put(top[0] + 1, top[1] - 1, top[2], petal);
      }
      break;
    }
    case 'pebbles': {
      const n = 2 + Math.floor(r() * 2);
      for (let k = 0; k < n; k++) {
        const x = spot(3);
        const z = spot(3);
        const c = pick(STONES);
        put(x, 0, z, c);
        if (r() < 0.6) put(x + 1, 0, z, c);
        if (r() < 0.4) put(x, 0, z + 1, c);
        if (k === 0 && r() < 0.5) put(x, 1, z, c);
      }
      break;
    }
    case 'leaves':
      for (let k = 0; k < 5 + Math.floor(r() * 3); k++) put(spot(3), 0, spot(3), pick(AUTUMN));
      break;
    case 'litter':
      if (r() < 0.5) {
        // A sheet of newspaper.
        for (let x = -1; x <= 1; x++) for (let z = -2; z <= 1; z++) put(x, 0, z, (x + z) % 3 ? col(0xe2dccc) : col(0xb8b3a6));
      } else {
        // A crumpled sheet and a can.
        for (const [x, z] of [[0, 0], [1, 0], [0, 1]] as const) put(x, 0, z, col(0xe8e2d2));
        put(-2, 0, 1, col(0xb03a2a));
        put(-2, 0, 2, col(0xb03a2a));
        put(-2, 0, 3, col(0x9aa4ad));
      }
      break;
    case 'rubble': {
      const n = 4 + Math.floor(r() * 3);
      for (let k = 0; k < n; k++) {
        const x = spot(3);
        const z = spot(3);
        const c = pick([col(0x9a968c), col(0x8a5a48), col(0x6c6a64), col(0xa8a294)]);
        put(x, 0, z, c);
        if (r() < 0.5) put(x + 1, 0, z, c);
        if (r() < 0.3) put(x, 1, z, c);
      }
      break;
    }
    case 'twig': {
      const len = 5 + Math.floor(r() * 3);
      for (let x = 0; x < len; x++) put(x - 3, 0, x > 3 && r() < 0.5 ? 1 : 0, col(0x5a4130));
      put(0, 0, -1, col(0x6a4e38));
      put(1, 0, -2, col(0x6a4e38));
      break;
    }
    case 'scrap': {
      for (let x = -1; x <= 1; x++) for (let z = -1; z <= 0; z++) put(x, 0, z, col(0x30353b));
      put(2, 0, 2, col(0x7d8790));
      for (let k = 0; k < 4; k++) put(-2 - (k > 2 ? 1 : 0), 0, 1 + k, col(0xc0652c));
      break;
    }
  }
  return cells;
}

const models = new Map<string, THREE.BufferGeometry | null>();

/** The voxel model of a detail variant, built on first use and shared. */
function detailModel(detail: Detail, variant: number): THREE.BufferGeometry | null {
  const key = `${detail}:${variant}`;
  if (models.has(key)) return models.get(key) ?? null;
  const cells = makeCells(detail, random(hash2(variant + 1, detail.length, 7) * 4294967296));
  const keys = [...cells.keys()].map((k) => k.split(',').map(Number) as [number, number, number]);
  const min = [0, 1, 2].map((a) => Math.min(...keys.map((k) => k[a]!))) as [number, number, number];
  const max = [0, 1, 2].map((a) => Math.max(...keys.map((k) => k[a]!))) as [number, number, number];
  const model = voxelField(min, max, (x, y, z) => cells.get(`${x},${y},${z}`) ?? null, { size: GAME_VOXEL, jitter: 0.12 });
  const mesh = model.children.find((c): c is THREE.Mesh => c instanceof THREE.Mesh);
  // Centred on its middle column, standing on y = 0.
  const geometry = mesh ? mesh.geometry.translate(-GAME_VOXEL / 2, 0, -GAME_VOXEL / 2) : null;
  models.set(key, geometry);
  return geometry;
}

const blocksDetail = (k: TileKind) => k !== 'floor' && k !== 'doorOpen';

/**
 * Small voxel details scattered over the ground, off the grid: where they
 * fall is noise (they clump and thin out across tiles), they turn any way and
 * keep clear of walls and cover. Purely cosmetic: no shadows, no picking.
 */
export function scatterDetails(s: GameState, ground: GroundMap, material: THREE.Material): THREE.Group {
  const group = new THREE.Group();
  const { width: w, height: h } = s;
  const plan = SCATTER[s.biome] ?? SCATTER.city;
  const decor = s.decor ?? [];
  const at = (x: number, y: number): TileKind | null => (x < 0 || y < 0 || x >= w || y >= h ? null : s.tiles[y * w + x]!);
  const elev = (x: number, y: number) => s.elev[y * w + x] ?? 0;
  const lists = new Map<string, { detail: Detail; variant: number; matrices: THREE.Matrix4[]; tints: THREE.Color[] }>();
  const add = (detail: Detail, variant: number, matrix: THREE.Matrix4, tint: THREE.Color) => {
    const key = `${detail}:${variant}`;
    let list = lists.get(key);
    if (!list) lists.set(key, (list = { detail, variant, matrices: [], tints: [] }));
    list.matrices.push(matrix);
    list.tints.push(tint);
  };
  // Trees drop leaves round them; blown-up cover leaves rubble.
  const leafy = new Uint8Array(w * h);
  const rubbly = new Uint8Array(w * h);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = y * w + x;
      const look = decorOf(decor[i]);
      const tree = (look && TREES.has(look.kind) && s.tiles[i] !== 'floor') || (s.biome === 'wilds' && !look && s.tiles[i] === 'high');
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const j = (y + dy) * w + x + dx;
          if (x + dx < 0 || y + dy < 0 || x + dx >= w || y + dy >= h) continue;
          if (tree) leafy[j] = 1;
          if (look?.kind === 'rubble') rubbly[j] = 1;
        }
      }
      if (look && s.tiles[i] === 'floor') rubbly[i] = 2;
    }
  }
  const quaternion = new THREE.Quaternion();
  const up = new THREE.Vector3(0, 1, 0);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = y * w + x;
      if (s.tiles[i] !== 'floor') continue;
      const e = elev(x, y);
      const kind = ground.kinds[i]!;
      const list: [Detail, number][] = [...(plan[kind] ?? [])];
      if (leafy[i]) list.push(['leaves', 1.1]);
      if (rubbly[i]) list.push(['rubble', rubbly[i] === 2 ? 2.2 : 0.5]);
      if (!list.length) continue;
      const r = random(hash2(x, y, 11) * 4294967296);
      // Keep a margin on the sides that walls, cover or a higher floor close.
      const margin = DIRS4.map((d) => {
        const k = at(x + d.x, y + d.y);
        return k === null || blocksDetail(k) || elev(x + d.x, y + d.y) > e ? 0.16 : 0;
      });
      const clump = 0.2 + 1.6 * noise2(x / 4.5, y / 4.5, 3);
      for (const [detail, density] of list) {
        let n = Math.floor(density * (detail === 'rubble' || detail === 'leaves' ? 1 : clump) + r());
        while (n-- > 0) {
          const px = THREE.MathUtils.clamp(x + r(), x + margin[3]!, x + 1 - margin[1]!);
          const pz = THREE.MathUtils.clamp(y + r(), y + margin[0]!, y + 1 - margin[2]!);
          const size = 0.8 + r() * 0.4;
          quaternion.setFromAxisAngle(up, r() * Math.PI * 2);
          const matrix = new THREE.Matrix4().compose(new THREE.Vector3(px, e * STOREY, pz), quaternion, new THREE.Vector3(size, size * (FLAT[detail] ?? 1), size));
          // Grass takes the dry patches' yellow, like the ground under it.
          const dry = detail === 'tuft' || detail === 'flower' ? THREE.MathUtils.smoothstep(noise2(px / 6, pz / 6, 5), 0.35, 0.75) : 0;
          const tint = new THREE.Color(1 + dry * 0.15, 1 + dry * 0.06, 1 - dry * 0.3).multiplyScalar(0.92 + r() * 0.16);
          add(detail, Math.floor(r() * VARIANTS[detail]), matrix, tint);
        }
      }
    }
  }
  for (const list of lists.values()) {
    const geometry = detailModel(list.detail, list.variant);
    if (!geometry) continue;
    const mesh = new THREE.InstancedMesh(geometry, material, list.matrices.length);
    list.matrices.forEach((m, k) => {
      mesh.setMatrixAt(k, m);
      mesh.setColorAt(k, list.tints[k]!);
    });
    mesh.receiveShadow = true;
    group.add(mesh);
  }
  return group;
}
