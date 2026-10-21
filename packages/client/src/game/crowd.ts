import * as THREE from 'three';
import { extendShader } from './fogOfWar';
import { UNIT_LAYER } from './post';

// The dead in a crowd (Gameplay, 2026-10-14: «muchos más zombis», 45 on the
// hardest maps): a dead unit at ease (shambling along, or standing swaying)
// is drawn by one instanced mesh for all the units with its body, its
// animation baked into a texture, instead of by its own skinned figure. A
// body's loops are baked once, from a figure of its own that no one sees
// (its walk and its stance, with everything the figure adds on top: the
// hunch, the arms out, the roll), a few frames a tick. The moment a unit does
// anything else (it is hit, it lunges, it falls, it climbs) it goes back to
// its own figure (syntyFigures.ts), and back to the crowd when it is calm,
// each time at the point of its loop it was at (playtest 15/10 #19: a body
// changing over at another point of its step jumped).

/** A loop the crowd plays: where its frames start in the body's texture, how many, and how long it lasts at its own speed. */
export interface CrowdLoop {
  row: number;
  frames: number;
  seconds: number;
}

/** What a figure gives the crowd to bake its body from: its mesh's frames of each loop, in its yaw's frame. */
export interface Baker {
  /** The skinned mesh's vertex count and index, and the material to draw it with (made for the crowd). */
  readonly vertices: number;
  readonly geometry: THREE.BufferGeometry;
  material(): THREE.Material;
  /** The loops it bakes and their frames; `frame` poses loop `k` at frame `f` (f / frames through it) and writes positions and normals (xyz each). */
  readonly loops: { frames: number; seconds: number }[];
  frame(k: number, f: number, positions: Float32Array, normals: Float32Array): void;
  dispose(): void;
}

/** A unit in the crowd: its body's instance, set every frame it stays. */
export interface Member {
  readonly key: string;
  index: number;
}

const CAPACITY = 32;
/**
 * The bake textures' width at most: a frame's vertices go in rows of this many (a Sidekick body has up to 21 000 and
 * no texture may be wider than 16 384: 21/10), one strip of rows a frame.
 */
const STRIP_WIDTH = 4096;

interface Body {
  key: string;
  baker: Baker | null;
  /** Baking: the next loop and frame to bake; null once done. */
  next: { k: number; f: number } | null;
  pos: Uint16Array;
  nor: Uint8Array;
  /** The textures' width, and how many of their rows a frame takes (STRIP_WIDTH). */
  width: number;
  strip: number;
  posTexture: THREE.DataTexture | null;
  norTexture: THREE.DataTexture | null;
  loops: CrowdLoop[];
  mesh: THREE.InstancedMesh | null;
  /** Each instance's loop row, frames, phase and rate (aCrowd). */
  params: THREE.InstancedBufferAttribute | null;
  free: number[];
  /** Instances handed out so far (the mesh draws that many: the rest are never drawn) and room for them. */
  used: number;
  capacity: number;
}

const HIDDEN = new THREE.Matrix4().makeScale(0, 0, 0);
/** Bake frames a tick at most, and the milliseconds it may take. */
const BAKE_BUDGET_MS = 3;

/** Draws a body's loops from its textures (texelFetch by gl_VertexID): its position and normal for the instance's time. */
function crowdPatch(material: THREE.Material, body: Body, time: { value: number }, depth = false): void {
  extendShader(material, `crowd:${body.key}:${depth ? 'depth' : 'colour'}`, (shader) => {
    shader.uniforms.crowdPos = { value: body.posTexture };
    shader.uniforms.crowdNor = { value: body.norTexture };
    shader.uniforms.crowdTime = time;
    const fetch = /* glsl */ `
      attribute vec4 aCrowd;
      uniform highp sampler2D crowdPos;
      uniform highp sampler2D crowdNor;
      uniform float crowdTime;
      // A frame's strip of rows (STRIP_WIDTH vertices a row): this vertex's place in it.
      const int crowdWidth = ${body.width};
      const int crowdStrip = ${body.strip};
      vec3 crowdNormal;
      vec3 crowdAt() {
        // The loop's frame now, and the next one: blended.
        float f = fract(crowdTime * aCrowd.w + aCrowd.z) * aCrowd.y;
        float f0 = floor(f);
        float k = f - f0;
        int r0 = int(aCrowd.x + f0);
        int r1 = int(aCrowd.x + mod(f0 + 1.0, aCrowd.y));
        ivec2 at = ivec2(gl_VertexID % crowdWidth, gl_VertexID / crowdWidth);
        ivec2 a = ivec2(at.x, r0 * crowdStrip + at.y);
        ivec2 b = ivec2(at.x, r1 * crowdStrip + at.y);
        crowdNormal = normalize(mix(texelFetch(crowdNor, a, 0).xyz, texelFetch(crowdNor, b, 0).xyz, k) * 2.0 - 1.0);
        return mix(texelFetch(crowdPos, a, 0).xyz, texelFetch(crowdPos, b, 0).xyz, k);
      }`;
    shader.vertexShader = shader.vertexShader.replace('#include <common>', `#include <common>\n${fetch}`);
    // The depth pass (the shadows) has no normals outside its displacement map's block: only the position there.
    if (!depth)
      shader.vertexShader = shader.vertexShader
        .replace('#include <beginnormal_vertex>', 'vec3 crowdPosition = crowdAt();\nvec3 objectNormal = crowdNormal;\n#ifdef USE_TANGENT\nvec3 objectTangent = vec3(tangent.xyz);\n#endif')
        .replace('#include <begin_vertex>', 'vec3 transformed = crowdPosition;');
    else shader.vertexShader = shader.vertexShader.replace('#include <begin_vertex>', 'vec3 transformed = crowdAt();');
  });
}

export class Crowd {
  readonly group = new THREE.Group();
  /** The crowd's clock: what the bodies' loops play by (the units' clock, at the battle's speed). */
  private readonly time = { value: 0 };
  private readonly bodies = new Map<string, Body>();
  /** The crowd on or off (the horde lab compares both). */
  enabled = true;
  /** A body's mesh made (baked after the loading veil): the renderer compiles its materials then (Renderer.warm). */
  onMesh: ((mesh: THREE.Object3D) => void) | null = null;

  constructor() {
    this.group.name = 'crowd';
  }

  /** Starts baking body `key` from `baker` (made once: later calls for the same key do nothing). */
  bake(key: string, baker: () => Baker | null): void {
    if (this.bodies.has(key)) return;
    const b = baker();
    if (!b) return;
    const rows = b.loops.reduce((n, l) => n + l.frames, 0);
    const width = Math.min(b.vertices, STRIP_WIDTH);
    const strip = Math.ceil(b.vertices / width);
    const body: Body = {
      key,
      baker: b,
      next: { k: 0, f: 0 },
      pos: new Uint16Array(width * strip * rows * 4),
      nor: new Uint8Array(width * strip * rows * 4),
      width,
      strip,
      posTexture: null,
      norTexture: null,
      loops: [],
      mesh: null,
      params: null,
      free: [],
      used: 0,
      capacity: CAPACITY,
    };
    let row = 0;
    for (const l of b.loops) {
      body.loops.push({ row, frames: l.frames, seconds: l.seconds });
      row += l.frames;
    }
    this.bodies.set(key, body);
  }

  /** The crowd's clock now: a loop played at `rate` from `phase` is fract(clock · rate / its seconds + phase) through. */
  get clock(): number {
    return this.time.value;
  }

  /** Whether body `key` has been baked and can be drawn. */
  ready(key: string): boolean {
    return !!this.bodies.get(key)?.mesh;
  }

  /** A place in the crowd for a unit of body `key` (null if it isn't baked yet). */
  join(key: string): Member | null {
    const body = this.bodies.get(key);
    if (!body?.mesh) return null;
    let index = body.free.pop();
    if (index === undefined) {
      if (body.used >= body.capacity) this.grow(body);
      index = body.used++;
      // Only as many drawn as handed out: a body's 32 places, drawn hidden, cost a Sidekick body 32 times its
      // vertices a pass (21/10: 17 kinds of the dead).
      body.mesh!.count = body.used;
    }
    return { key, index };
  }

  /** The unit's instance this frame: at `matrix`, playing loop `loop` from `phase` (0 to 1) at `rate` times its speed; hidden if not `shown`. */
  set(m: Member, matrix: THREE.Matrix4, loop: number, phase: number, rate: number, shown: boolean): void {
    const body = this.bodies.get(m.key);
    if (!body?.mesh || !body.params) return;
    body.mesh.setMatrixAt(m.index, shown ? matrix : HIDDEN);
    body.mesh.instanceMatrix.needsUpdate = true;
    const l = body.loops[loop]!;
    const p = body.params;
    if (p.getX(m.index) !== l.row || p.getW(m.index) !== rate / l.seconds || p.getZ(m.index) !== phase) {
      p.setXYZW(m.index, l.row, l.frames, phase, rate / l.seconds);
      p.needsUpdate = true;
    }
  }

  /** The unit leaves the crowd (it does something, or is gone). */
  leave(m: Member): void {
    const body = this.bodies.get(m.key);
    if (!body?.mesh) return;
    body.mesh.setMatrixAt(m.index, HIDDEN);
    body.mesh.instanceMatrix.needsUpdate = true;
    body.free.push(m.index);
  }

  /** Advances the clock (`dt` at the battle's speed) and bakes a little more of what is waiting. */
  tick(dt: number): void {
    this.time.value += dt;
    const start = performance.now();
    for (const body of this.bodies.values()) {
      const b = body.baker;
      if (!body.next || !b) continue;
      const scratchP = new Float32Array(b.vertices * 3);
      const scratchN = new Float32Array(b.vertices * 3);
      while (body.next && performance.now() - start < BAKE_BUDGET_MS) {
        const { k, f }: { k: number; f: number } = body.next;
        b.frame(k, f, scratchP, scratchN);
        const row = body.loops[k]!.row + f;
        for (let i = 0; i < b.vertices; i++) {
          const o = (((row * body.strip + Math.floor(i / body.width)) * body.width) + (i % body.width)) * 4;
          body.pos[o] = THREE.DataUtils.toHalfFloat(scratchP[i * 3]!);
          body.pos[o + 1] = THREE.DataUtils.toHalfFloat(scratchP[i * 3 + 1]!);
          body.pos[o + 2] = THREE.DataUtils.toHalfFloat(scratchP[i * 3 + 2]!);
          body.nor[o] = Math.round((scratchN[i * 3]! * 0.5 + 0.5) * 255);
          body.nor[o + 1] = Math.round((scratchN[i * 3 + 1]! * 0.5 + 0.5) * 255);
          body.nor[o + 2] = Math.round((scratchN[i * 3 + 2]! * 0.5 + 0.5) * 255);
        }
        body.next = f + 1 < body.loops[k]!.frames ? { k, f: f + 1 } : k + 1 < body.loops.length ? { k: k + 1, f: 0 } : null;
      }
      if (!body.next) this.finish(body);
      if (performance.now() - start >= BAKE_BUDGET_MS) break;
    }
  }

  /** A body baked: its textures and its instanced mesh. */
  private finish(body: Body): void {
    const b = body.baker!;
    const rows = body.loops.reduce((n, l) => n + l.frames, 0);
    body.posTexture = new THREE.DataTexture(body.pos, body.width, rows * body.strip, THREE.RGBAFormat, THREE.HalfFloatType);
    body.norTexture = new THREE.DataTexture(body.nor, body.width, rows * body.strip, THREE.RGBAFormat, THREE.UnsignedByteType);
    for (const t of [body.posTexture, body.norTexture]) {
      t.minFilter = t.magFilter = THREE.NearestFilter;
      t.needsUpdate = true;
    }
    const geometry = b.geometry;
    const material = b.material();
    crowdPatch(material, body, this.time);
    const depth = new THREE.MeshDepthMaterial({ depthPacking: THREE.RGBADepthPacking });
    crowdPatch(depth, body, this.time, true);
    const mesh = new THREE.InstancedMesh(geometry, material, CAPACITY);
    mesh.customDepthMaterial = depth;
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    mesh.frustumCulled = false;
    mesh.layers.set(UNIT_LAYER);
    for (let i = 0; i < CAPACITY; i++) mesh.setMatrixAt(i, HIDDEN);
    mesh.count = body.used;
    body.params = new THREE.InstancedBufferAttribute(new Float32Array(CAPACITY * 4), 4);
    body.params.setUsage(THREE.DynamicDrawUsage);
    geometry.setAttribute('aCrowd', body.params);
    body.mesh = mesh;
    this.group.add(mesh);
    this.onMesh?.(mesh);
    b.dispose();
    body.baker = null;
  }

  /** Room for more of a body (twice as many). */
  private grow(body: Body): void {
    const old = body.mesh!;
    const count = body.capacity * 2;
    body.capacity = count;
    const mesh = new THREE.InstancedMesh(old.geometry, old.material, count);
    mesh.customDepthMaterial = old.customDepthMaterial;
    mesh.castShadow = mesh.receiveShadow = true;
    mesh.frustumCulled = false;
    mesh.layers.set(UNIT_LAYER);
    const m = new THREE.Matrix4();
    for (let i = 0; i < count; i++) mesh.setMatrixAt(i, i < body.used ? (old.getMatrixAt(i, m), m) : HIDDEN);
    mesh.count = body.used;
    const params = new THREE.InstancedBufferAttribute(new Float32Array(count * 4), 4);
    params.setUsage(THREE.DynamicDrawUsage);
    (params.array as Float32Array).set(body.params!.array as Float32Array);
    old.geometry.setAttribute('aCrowd', params);
    body.params = params;
    this.group.remove(old);
    old.dispose();
    body.mesh = mesh;
    this.group.add(mesh);
  }

  /** Everything gone (a battle ends). */
  clear(): void {
    for (const body of this.bodies.values()) {
      body.baker?.dispose();
      body.mesh?.dispose();
      body.posTexture?.dispose();
      body.norTexture?.dispose();
    }
    this.bodies.clear();
    this.group.clear();
  }
}

/** The battle's crowd (one battle at a time): the unit views add its group and tick it. */
export const CROWD = new Crowd();
