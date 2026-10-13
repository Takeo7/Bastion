import * as THREE from 'three';

// Static geometry merged into few big meshes (the 240 fps budget: at those
// rates the CPU decides, and its cost is draw calls and objects to walk).
// Everything that stands still on the map (props, ground cover, the pieces of
// walls, floor slabs) is copied, already placed, into one mesh per material
// and kind of shadow, in chunks of the map: a frame draws a handful of meshes
// instead of one per part of each prop, in the scene, the shadow map and the
// occlusion's pre-pass alike, and a change merges again only its chunk. What
// an instance carried (a colour, the cutaway's building and facing) becomes
// the same value on each of its vertices.

/** Values every vertex of one instance takes (an attribute's name → its components). */
export type Consts = Record<string, readonly number[]>;

interface Source {
  geometry: THREE.BufferGeometry;
  matrix: THREE.Matrix4;
  consts?: Consts;
}

/** A source geometry decoded once to plain floats (Synty's come quantized and interleaved): merging then only does arithmetic. */
interface Decoded {
  count: number;
  position: Float32Array;
  normal: Float32Array | null;
  uv: Float32Array | null;
  index: Uint32Array;
}

const decoded = new WeakMap<THREE.BufferGeometry, Decoded>();

function decode(g: THREE.BufferGeometry): Decoded {
  let d = decoded.get(g);
  if (d) return d;
  const read = (a: THREE.BufferAttribute | THREE.InterleavedBufferAttribute | undefined, size: number) => {
    if (!a) return null;
    const out = new Float32Array(a.count * size);
    for (let i = 0; i < a.count; i++) for (let k = 0; k < size; k++) out[i * size + k] = a.getComponent(i, k);
    return out;
  };
  const p = g.attributes.position!;
  const index = new Uint32Array(g.index ? g.index.count : p.count);
  for (let i = 0; i < index.length; i++) index[i] = g.index ? g.index.getX(i) : i;
  d = { count: p.count, position: read(p, 3)!, normal: read(g.attributes.normal, 3), uv: read(g.attributes.uv, 2), index };
  decoded.set(g, d);
  return d;
}

/** Tiles where something changed at the last commit (the contact occlusion is baked again there: groundAO.ts). */
export interface Dirty {
  x0: number;
  y0: number;
  x1: number;
  y1: number;
}

/** One chunk of one group: where its merged geometry lives in the group's BatchedMesh, and what it was made of. */
interface Chunk {
  geometryId: number;
  instanceId: number;
  /** Of its sources (geometry, place, consts); EMPTY once they're all gone. */
  signature: number;
  /** Room kept for it in the mesh's buffers (it may grow a little and stay put). */
  vertices: number;
  indices: number;
}

/** Everything of one material and kind of shadow: one BatchedMesh, one draw (a multi-draw over its chunks). */
interface Group {
  mesh: THREE.BatchedMesh;
  /** The consts every vertex carries (all the group's geometry has the same attributes). */
  names: Map<string, number>;
  chunks: Map<string, Chunk>;
  maxVertices: number;
  maxIndices: number;
}

const EMPTY = -1;

/** A stable number per source geometry, for the signatures. */
const ids = new WeakMap<THREE.BufferGeometry, number>();
let nextId = 1;
function idOf(g: THREE.BufferGeometry): number {
  let id = ids.get(g);
  if (id === undefined) ids.set(g, (id = nextId++));
  return id;
}

/** A hash of what a chunk is made of: which geometries, where, with which consts. */
function signature(sources: Source[]): number {
  let h = Math.imul(2166136261 ^ sources.length, 16777619);
  for (const s of sources) {
    h = Math.imul(h ^ idOf(s.geometry), 16777619);
    const e = s.matrix.elements;
    for (let k = 0; k < 16; k++) h = Math.imul(h ^ ((e[k]! * 4096) | 0), 16777619);
    if (s.consts) for (const name in s.consts) for (const v of s.consts[name]!) h = Math.imul(h ^ ((v * 4096) | 0), 16777619);
  }
  return h >>> 0;
}

/**
 * The map's static geometry, kept merged between rebuilds. Each rebuild adds
 * everything again (placing is cheap); `commit` then compares each chunk of
 * each material with what it was and merges again only the chunks that
 * changed, in place in their BatchedMesh (the rest of its buffers isn't
 * touched or uploaded). A door kicked in or a wall blown up re-merges one
 * chunk or two, not the map (reference block: the full merge was a hitch).
 * Each material still draws once: a multi-draw over its chunks, which also
 * leaves out the chunks off screen.
 */
export class StaticBatch {
  /** The merged meshes, one per material and kind of shadow. */
  readonly group = new THREE.Group();
  private readonly groups = new Map<string, Group>();
  private pending = new Map<string, { group: string; material: THREE.Material; cast: boolean; chunk: string; sources: Source[] }>();
  private occluding = new Map<string, Source[]>();
  /** What darkens the ground round it, merged per chunk (positions are what count). */
  private readonly occluders = new Map<string, { signature: number; geometry: THREE.BufferGeometry }>();
  /** The chunks this rebuild redoes (null: all); what's added elsewhere is ignored and stays as it was. */
  private only: Set<string> | null = null;

  /** `chunk`: tiles a side of each chunk (what one change merges again). */
  constructor(readonly chunk = 8) {}

  /** The chunk of world point (x, z). */
  chunkAt(x: number, z: number): string {
    return `${Math.floor(x / this.chunk)},${Math.floor(z / this.chunk)}`;
  }

  private chunkOf(matrix: THREE.Matrix4): string {
    const e = matrix.elements;
    return this.chunkAt(e[12]!, e[14]!);
  }

  /**
   * Starts a rebuild of `chunks` only (null: the whole map): the caller places
   * at least everything that lands in them (anything else it adds is ignored),
   * and commit leaves the other chunks as they are.
   */
  begin(chunks: Set<string> | null): void {
    this.only = chunks;
    this.pending.clear();
    this.occluding.clear();
  }

  /**
   * One placed instance of `geometry`; `cast` whether it casts a shadow
   * (everything receives them), `occlude` whether it darkens the ground round
   * it (by default what casts a shadow).
   */
  add(geometry: THREE.BufferGeometry, material: THREE.Material, matrix: THREE.Matrix4, cast: boolean, consts?: Consts, occlude = cast): void {
    const chunk = this.chunkOf(matrix);
    if (this.only && !this.only.has(chunk)) return;
    if (occlude) {
      let list = this.occluding.get(chunk);
      if (!list) this.occluding.set(chunk, (list = []));
      list.push({ geometry, matrix });
    }
    const group = `${material.uuid}|${cast ? 1 : 0}`;
    const key = `${group}#${chunk}`;
    let bucket = this.pending.get(key);
    if (!bucket) this.pending.set(key, (bucket = { group, material, cast, chunk, sources: [] }));
    bucket.sources.push({ geometry, matrix, consts });
  }

  /** Brings the merged meshes up to date with what was added since the last commit; the tiles where something changed, or null. */
  commit(): Dirty | null {
    const dirty: Dirty = { x0: Infinity, y0: Infinity, x1: -Infinity, y1: -Infinity };
    const mark = (chunk: string) => {
      const [cx, cz] = chunk.split(',').map(Number) as [number, number];
      dirty.x0 = Math.min(dirty.x0, cx * this.chunk);
      dirty.y0 = Math.min(dirty.y0, cz * this.chunk);
      dirty.x1 = Math.max(dirty.x1, (cx + 1) * this.chunk);
      dirty.y1 = Math.max(dirty.y1, (cz + 1) * this.chunk);
    };
    const seen = new Set<string>();
    for (const b of this.pending.values()) {
      seen.add(`${b.group}#${b.chunk}`);
      const sig = signature(b.sources);
      let group = this.groups.get(b.group);
      const was = group?.chunks.get(b.chunk);
      if (was && was.signature === sig) continue;
      mark(b.chunk);
      if (!group) this.groups.set(b.group, (group = this.newGroup(b.group, b.material, b.cast)));
      this.place(group, b.chunk, merge(b.sources, group.names), sig, was);
    }
    // Chunks left with nothing of a material: hidden (their room is kept for when something comes back).
    for (const [key, group] of this.groups) {
      for (const [chunk, c] of group.chunks) {
        if (c.signature === EMPTY || seen.has(`${key}#${chunk}`) || (this.only && !this.only.has(chunk))) continue;
        group.mesh.setVisibleAt(c.instanceId, false);
        c.signature = EMPTY;
        mark(chunk);
      }
    }
    // The occluders, per chunk.
    for (const [chunk, sources] of this.occluding) {
      const sig = signature(sources);
      const was = this.occluders.get(chunk);
      if (was?.signature === sig) continue;
      was?.geometry.dispose();
      this.occluders.set(chunk, { signature: sig, geometry: merge(sources) });
      mark(chunk);
    }
    for (const [chunk, o] of this.occluders) {
      if (this.occluding.has(chunk) || (this.only && !this.only.has(chunk))) continue;
      o.geometry.dispose();
      this.occluders.delete(chunk);
      mark(chunk);
    }
    this.pending.clear();
    this.occluding.clear();
    this.only = null;
    return dirty.x1 > dirty.x0 ? dirty : null;
  }

  /** The merged occluders, one per chunk (owned here). */
  occluderGeometries(): THREE.BufferGeometry[] {
    return [...this.occluders.values()].map((o) => o.geometry);
  }

  private newGroup(key: string, material: THREE.Material, cast: boolean): Group {
    // Room for everything this group has now, and some to grow.
    let vertices = 0;
    let indices = 0;
    let chunks = 0;
    const names = new Map<string, number>();
    for (const b of this.pending.values()) {
      if (b.group !== key) continue;
      chunks++;
      for (const s of b.sources) {
        const d = decode(s.geometry);
        vertices += d.count;
        indices += d.index.length;
        for (const [name, values] of Object.entries(s.consts ?? {})) names.set(name, values.length);
      }
    }
    const maxVertices = Math.ceil(vertices * 1.3) + 1024;
    const maxIndices = Math.ceil(indices * 1.3) + 3072;
    const mesh = new THREE.BatchedMesh(Math.max(4, chunks * 2), maxVertices, maxIndices, material);
    mesh.castShadow = cast;
    mesh.receiveShadow = true;
    mesh.matrixAutoUpdate = false;
    // Its chunks are culled one by one; the map as a whole is nearly always in view.
    mesh.frustumCulled = false;
    this.group.add(mesh);
    return { mesh, names, chunks: new Map(), maxVertices, maxIndices };
  }

  /** Puts a chunk's merged geometry in its group's mesh: in its old room if it fits, else at the end (growing the buffers if need be). */
  private place(group: Group, chunk: string, geometry: THREE.BufferGeometry, sig: number, was: Chunk | undefined): void {
    const mesh = group.mesh;
    const v = geometry.attributes.position!.count;
    const i = geometry.index!.count;
    if (was && v <= was.vertices && i <= was.indices) {
      mesh.setGeometryAt(was.geometryId, geometry);
      mesh.setVisibleAt(was.instanceId, true);
      was.signature = sig;
      return;
    }
    if (was) mesh.deleteGeometry(was.geometryId);
    const rv = Math.ceil(v * 1.25) + 64;
    const ri = Math.ceil(i * 1.25) + 192;
    if (mesh.unusedVertexCount < rv || mesh.unusedIndexCount < ri) {
      mesh.optimize();
      if (mesh.unusedVertexCount < rv || mesh.unusedIndexCount < ri) {
        group.maxVertices = Math.ceil((group.maxVertices + rv) * 1.3);
        group.maxIndices = Math.ceil((group.maxIndices + ri) * 1.3);
        mesh.setGeometrySize(group.maxVertices, group.maxIndices);
      }
    }
    const geometryId = mesh.addGeometry(geometry, rv, ri);
    if (mesh.instanceCount >= mesh.maxInstanceCount) mesh.setInstanceCount(mesh.maxInstanceCount * 2);
    const instanceId = mesh.addInstance(geometryId);
    group.chunks.set(chunk, { geometryId, instanceId, signature: sig, vertices: rv, indices: ri });
  }

  dispose(): void {
    for (const g of this.groups.values()) g.mesh.dispose();
    for (const o of this.occluders.values()) o.geometry.dispose();
    this.groups.clear();
    this.occluders.clear();
    this.group.clear();
  }
}

/** One indexed geometry of every source, placed by its matrix: position, normal and uv, and the consts. */
function merge(sources: Source[], names?: Map<string, number>): THREE.BufferGeometry {
  let vertices = 0;
  let indices = 0;
  const parts = sources.map((s) => decode(s.geometry));
  for (const d of parts) {
    vertices += d.count;
    indices += d.index.length;
  }
  // The group's consts (every chunk of a BatchedMesh carries the same attributes), or these sources' own.
  const constNames = names ?? new Map<string, number>();
  if (!names) for (const s of sources) for (const [name, values] of Object.entries(s.consts ?? {})) constNames.set(name, values.length);
  // Always a uv, if only zeros: all the chunks of a BatchedMesh need the same attributes.
  const hasUv = true;

  const position = new Float32Array(vertices * 3);
  const normal = new Float32Array(vertices * 3);
  const uv = hasUv ? new Float32Array(vertices * 2) : null;
  const consts = [...constNames].map(([name, size]) => ({ name, size, data: new Float32Array(vertices * size) }));
  const index = vertices > 65535 ? new Uint32Array(indices) : new Uint16Array(indices);

  let base = 0;
  let at = 0;
  for (let j = 0; j < sources.length; j++) {
    const s = sources[j]!;
    const d = parts[j]!;
    const e = s.matrix.elements;
    // The normal matrix: the inverse transpose of the matrix's 3×3 (here as its cofactors; the scale is normalised away).
    const [a00, a01, a02, a10, a11, a12, a20, a21, a22] = [e[0]!, e[1]!, e[2]!, e[4]!, e[5]!, e[6]!, e[8]!, e[9]!, e[10]!];
    const n00 = a11 * a22 - a12 * a21;
    const n01 = a12 * a20 - a10 * a22;
    const n02 = a10 * a21 - a11 * a20;
    const n10 = a21 * a02 - a22 * a01;
    const n11 = a22 * a00 - a20 * a02;
    const n12 = a20 * a01 - a21 * a00;
    const n20 = a01 * a12 - a02 * a11;
    const n21 = a02 * a10 - a00 * a12;
    const n22 = a00 * a11 - a01 * a10;
    const det = a00 * n00 + a01 * n01 + a02 * n02;
    const sign = det < 0 ? -1 : 1;
    const [tx, ty, tz] = [e[12]!, e[13]!, e[14]!];
    const P = d.position;
    const N = d.normal;
    for (let i = 0, o = base * 3; i < d.count * 3; i += 3, o += 3) {
      const x = P[i]!;
      const y = P[i + 1]!;
      const z = P[i + 2]!;
      position[o] = a00 * x + a10 * y + a20 * z + tx;
      position[o + 1] = a01 * x + a11 * y + a21 * z + ty;
      position[o + 2] = a02 * x + a12 * y + a22 * z + tz;
      if (N) {
        const nx = N[i]!;
        const ny = N[i + 1]!;
        const nz = N[i + 2]!;
        const wx = n00 * nx + n10 * ny + n20 * nz;
        const wy = n01 * nx + n11 * ny + n21 * nz;
        const wz = n02 * nx + n12 * ny + n22 * nz;
        const l = sign / (Math.sqrt(wx * wx + wy * wy + wz * wz) || 1);
        normal[o] = wx * l;
        normal[o + 1] = wy * l;
        normal[o + 2] = wz * l;
      }
    }
    if (uv && d.uv) uv.set(d.uv, base * 2);
    for (const c of consts) {
      const values = s.consts?.[c.name];
      if (!values) continue;
      for (let k = 0; k < c.size; k++) {
        const value = values[k]!;
        for (let i = (base * c.size) + k, end = (base + d.count) * c.size; i < end; i += c.size) c.data[i] = value;
      }
    }
    // A mirroring matrix turns the faces inside out: their corners go the other way round.
    const flip = det < 0;
    const I = d.index;
    for (let i = 0; i < I.length; i += 3) {
      index[at++] = base + I[i]!;
      index[at++] = base + I[flip ? i + 2 : i + 1]!;
      index[at++] = base + I[flip ? i + 1 : i + 2]!;
    }
    base += d.count;
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(position, 3));
  geometry.setAttribute('normal', new THREE.BufferAttribute(normal, 3));
  if (uv) geometry.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
  for (const c of consts) geometry.setAttribute(c.name, new THREE.BufferAttribute(c.data, c.size));
  geometry.setIndex(new THREE.BufferAttribute(index, 1));
  geometry.computeBoundingBox();
  geometry.computeBoundingSphere();
  return geometry;
}
