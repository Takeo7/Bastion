import * as THREE from 'three';
import type { TileKind } from '@bastion/engine';
import { STOREY } from './coords';

// Fog of war on the map's materials, in three zones (Arturo, playtests 21/10 #8
// and 22/10 #13):
//  - in sight (where enemies show: teamVisibility, 18 tiles by day, 10 at
//    night unless lit): as it is;
//  - the fog of war: as far as the squad's line of sight goes (teamView, 40
//    tiles, day or night), and wherever it has seen this mission (explored.ts):
//    seen, under a haze, greyer and darker, so it reads as "no enemies show
//    here";
//  - never seen: a dark mist drifting slowly, where only the tallest shapes
//    show as the faintest silhouettes; so are the map's outline (the frame
//    round what is played) and what lies beyond its edge.
// A building's outer wall shows as the tile it looks onto does: a face is seen
// where the street before it is (soldiers see the faces of the buildings they
// stand by, 22/10 #13). Its roof and what stands on it, as the street along
// the building at its best. Its inside, room by room (Ana, 22/10 #36): a room
// any of which has been seen is under the fog of war whole, with the walls
// round it; what nobody has seen stays dark, even with its roof open. What is
// in sight (where enemies show) is still tile by tile.
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
/** The fog texture's alpha holds each tile's floor, this much per storey; the buildings' texture, their storeys. */
const FLOOR_STEP = 32;
/** What parts the rooms of a building (setRooms): its walls, doors and windows. */
const BETWEEN_ROOMS: ReadonlySet<TileKind> = new Set<TileKind>(['wall', 'door', 'doorOpen', 'window', 'windowBroken', 'roof']);

/** How far out from a wall's face (tiles) the tile it looks onto is: past the far half of its own (walls stand in their tiles' middles). */
const FACE_OUT = 0.6;
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
/**
 * The fog of war over what is seen but out of sight (Arturo, 22/10: «más niebla», the haze before was hardly there):
 * how much of the colour goes grey, how dark it gets, and the haze's tint over it, part of it drifting with the mist.
 */
const FOG_GREY = 0.55;
const FOG_DARK = 0.62;
const FOG_HAZE = 0.1;
const FOG_DRIFT = 0.12;
/** The haze's tint over what is seen but out of sight, by day and by night. */
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
    /** By tile, the storeys of its building (red, FLOOR_STEP a storey; 0 outside buildings), and the street along it at its best: in sight (green), seen (blue). */
    fowShell: { value: new THREE.DataTexture(new Uint8Array(4), 1, 1) as THREE.DataTexture },
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
  /** The buildings (setShells): each one's tiles, and the tiles outside it along its walls. */
  private shells: { tiles: number[]; along: number[] }[] = [];
  /** By tile, its building's sight and fog of war shown now (0–1), and where they are heading (for its roof). */
  private shellSight = new Float32Array(0);
  private shellGoal = new Uint8Array(0);
  private shellKnown = new Float32Array(0);
  private shellKnownGoal = new Uint8Array(0);
  /** The rooms of the floor in view (setRooms): each one's tiles, and the walls, doors and windows round it. */
  private rooms: { tiles: number[]; round: number[] }[] = [];

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
    this.shells = [];
    this.rooms = [];
    this.shellSight = new Float32Array(size);
    this.shellGoal = new Uint8Array(size);
    this.shellKnown = new Float32Array(size);
    this.shellKnownGoal = new Uint8Array(size);
    this.uniforms.fowMap.value.dispose();
    const texture = new THREE.DataTexture(new Uint8Array(size * 4), width, height);
    texture.magFilter = THREE.LinearFilter;
    texture.minFilter = THREE.LinearFilter;
    texture.needsUpdate = true;
    this.uniforms.fowMap.value = texture;
    // Tile by tile, no blend: which tile a wall's face stands in and which it looks onto.
    this.uniforms.fowShell.value.dispose();
    const shell = new THREE.DataTexture(new Uint8Array(size * 4), width, height);
    shell.needsUpdate = true;
    this.uniforms.fowShell.value = shell;
    this.uniforms.fowSize.value.set(width, height);
  }

  /**
   * The map's buildings, from the storeys of the building each tile belongs to (GameState.storeys, 0 outside
   * buildings; null, none): their outer walls and roofs show as the street along them. Call after setShut, before set.
   */
  setShells(storeys: readonly number[] | null | undefined, width: number): void {
    const data = this.uniforms.fowShell.value.image.data as Uint8Array;
    data.fill(0);
    this.shells = [];
    const size = this.size;
    if (storeys?.length === size) {
      const height = size / width;
      const home = new Int32Array(size).fill(-1);
      for (let start = 0; start < size; start++) {
        if (!(storeys[start]! > 0) || home[start]! >= 0) continue;
        // One building: its tiles joined side by side.
        const shell = { tiles: [start], along: [] as number[] };
        const outside = new Set<number>();
        home[start] = this.shells.length;
        for (let k = 0; k < shell.tiles.length; k++) {
          const i = shell.tiles[k]!;
          const x = i % width;
          const y = (i - x) / width;
          for (const [nx, ny] of [[x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]] as const) {
            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
            const n = ny * width + nx;
            if (!(storeys[n]! > 0)) {
              if (!this.shut?.[n]) outside.add(n);
            } else if (home[n]! < 0) {
              home[n] = home[start]!;
              shell.tiles.push(n);
            }
          }
        }
        shell.along = [...outside];
        this.shells.push(shell);
      }
      for (let i = 0; i < size; i++) data[i * 4] = Math.min(255, Math.max(0, storeys[i]!) * FLOOR_STEP);
    }
    this.uniforms.fowShell.value.needsUpdate = true;
  }

  /** The tiles under the fog for good from now on (the map's outline), as a mask of the map's size; null, none. */
  setShut(mask: Uint8Array | null): void {
    this.shut = mask && mask.length === this.size ? mask : null;
  }

  /**
   * The rooms of the floor in view, from what stands on each tile there (`kinds`, null where it has no floor) and the
   * storeys of the building each belongs to (GameState.storeys): the inside of a building parted by its walls, doors and
   * windows. Call before set, whenever the floor in view or a wall changes.
   */
  setRooms(kinds: readonly (TileKind | null)[], storeys: readonly number[] | undefined, width: number): void {
    this.rooms = [];
    const size = this.size;
    if (!storeys || storeys.length !== size || kinds.length !== size) return;
    const height = size / width;
    const inside = (i: number) => storeys[i]! > 0 && kinds[i] !== null && !BETWEEN_ROOMS.has(kinds[i]!);
    const done = new Uint8Array(size);
    for (let start = 0; start < size; start++) {
      if (done[start] || !inside(start)) continue;
      const tiles = [start];
      const round = new Set<number>();
      done[start] = 1;
      for (let k = 0; k < tiles.length; k++) {
        const i = tiles[k]!;
        const x = i % width;
        const y = (i - x) / width;
        for (const [nx, ny] of [[x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]] as const) {
          if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
          const n = ny * width + nx;
          if (inside(n)) {
            if (!done[n]) {
              done[n] = 1;
              tiles.push(n);
            }
          } else if (storeys[n]! > 0) round.add(n);
        }
      }
      this.rooms.push({ tiles, round: [...round] });
    }
  }

  /**
   * Tiles in sight (`visible`, where enemies show) and under the fog of war (`view`: the line of sight and what was
   * seen before; at least those in sight), never the outline (setShut); `instant` skips the fade (loading a mission).
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
    // A room any of which has been seen, whole, with the walls round it (Ana, 22/10 #36).
    for (const room of this.rooms) {
      if (!room.tiles.some((i) => this.knownGoal[i])) continue;
      for (const i of [...room.tiles, ...room.round]) {
        if (this.shut?.[i]) continue;
        this.knownGoal[i] = 1;
        if (instant) this.known[i] = 1;
      }
    }
    // A building's roof: in sight when any of the street along it is, seen when any of it was.
    for (const shell of this.shells) {
      const goal = shell.along.some((i) => this.sightGoal[i]) ? 1 : 0;
      const known = shell.along.some((i) => this.knownGoal[i]) ? 1 : 0;
      for (const i of shell.tiles) {
        this.shellGoal[i] = goal;
        this.shellKnownGoal[i] = known;
        if (instant) {
          this.shellSight[i] = goal;
          this.shellKnown[i] = known;
        }
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
    if (this.shells.length) {
      const shell = this.uniforms.fowShell.value.image.data as Uint8Array;
      for (let i = 0; i < this.size; i++) {
        const b = approach(this.shellSight[i]!, this.shellGoal[i]!, sightStep);
        const k = approach(this.shellKnown[i]!, this.shellKnownGoal[i]!, sightStep);
        this.shellSight[i] = b;
        this.shellKnown[i] = k;
        if (b !== this.shellGoal[i] || k !== this.shellKnownGoal[i]) moving = true;
        shell[i * 4 + 1] = Math.round(b * 255);
        shell[i * 4 + 2] = Math.round(k * 255);
      }
      this.uniforms.fowShell.value.needsUpdate = true;
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
      shader.vertexShader = shader.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 vFowPos;\nvarying vec2 vFowFront;').replace(
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
          // The tile it looks onto: outside its building, it is the building's outside.
          vFowFront = fowWorld.xz + fowNormal.xz * ${FACE_OUT.toFixed(2)};
        }`,
      );
      shader.fragmentShader = shader.fragmentShader
        .replace(
          '#include <common>',
          `#include <common>
          varying vec3 vFowPos;
          varying vec2 vFowFront;
          uniform sampler2D fowMap;
          uniform sampler2D fowShell;
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
          // A building's outside (playtest 22/10 #13). Anything on or above its roof: as the street along the
          // building at its best. A face that looks out of it onto a tile outside: as that tile (seen from there).
          vec4 fowHome = texture2D(fowShell, fowCell / fowSize);
          float fowStoreys = fowHome.r * ${(255 / FLOOR_STEP).toFixed(4)};
          float fowBuilt = step(0.5, fowStoreys) * (1.0 - step(0.001, length(fowOut)));
          float fowRoof = fowBuilt * step(fowStoreys * ${STOREY.toFixed(3)} - 0.15, vFowPos.y);
          float fowFace = (fowBuilt - fowRoof) * step(texture2D(fowShell, vFowFront / fowSize).r, 0.01);
          vec4 fowAhead = texture2D(fowMap, vFowFront / fowSize);
          fowSeen = max(fowSeen, max(smoothstep(0.3, 0.7, fowAhead.r + (fowMist - 0.5) * 0.12) * fowFace, smoothstep(0.3, 0.7, fowHome.g) * fowRoof));
          fowKnown = max(fowKnown, max(smoothstep(0.22, 0.78, fowAhead.g + (fowMist - 0.5) * 0.4) * fowFace, smoothstep(0.22, 0.78, fowHome.b) * fowRoof));
          // The fog of war, out of sight (no enemies show there): greyer and darker, under a haze that drifts.
          vec3 fowGrey = vec3(dot(diffuseColor.rgb, vec3(0.2126, 0.7152, 0.0722)));
          vec3 fowFogged = mix(diffuseColor.rgb, fowGrey, ${FOG_GREY.toFixed(2)}) * ${FOG_DARK.toFixed(2)} + fowHaze * (${FOG_HAZE.toFixed(2)} + ${FOG_DRIFT.toFixed(2)} * fowMist);
          diffuseColor.rgb = mix(fowFogged, diffuseColor.rgb, fowSeen);
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
              // The base's (fowEdge): only what lies past the map as the camera looks, where its wood ends; what is
              // between the camera and the map, or beside it, stays clear (playtest 22/10 #30: the pines in front came
              // out white). What the view reaches that far out it reaches looking out across the edge (fowPast > 0.5).
              vec2 fowAway = fowCell - clamp(fowCell, vec2(0.0), fowSize);
              float fowPast = dot(fowAway, vFowPos.xz - cameraPosition.xz) / max(length(fowAway) * length(vFowPos.xz - cameraPosition.xz), 1e-4);
              float fowHaze = smoothstep(fowFar.x, fowFar.y, length(fowOut)) * mix(1.0, smoothstep(0.1, 0.5, fowPast), fowEdge);
              gl_FragColor.rgb = mix(gl_FragColor.rgb, fogColor, fowHaze);
            #endif
          }`,
        );
    });
    return material;
  }

  dispose(): void {
    this.uniforms.fowMap.value.dispose();
    this.uniforms.fowShell.value.dispose();
  }
}

function approach(value: number, goal: number, step: number): number {
  return value < goal ? Math.min(goal, value + step) : Math.max(goal, value - step);
}
