import * as THREE from 'three';

// Turns ordinary meshes into voxel models (MagicaVoxel look): the surface of
// every triangle is sampled into a grid, the inside is filled by flooding the
// outside, and only faces that touch the outside are emitted, with per-vertex
// ambient occlusion and a little per-voxel colour noise. Voxels can be
// non-cubic (toy bricks), snapped to a palette and topped with studs.

export interface Colour {
  r: number;
  g: number;
  b: number;
  glow: boolean;
}

export interface VoxelSource {
  mesh: THREE.Mesh;
  /** Mesh space → voxel space. */
  matrix: THREE.Matrix4;
}

export interface VoxelOptions {
  /** Voxel size, or [x, y, z] sizes for non-cubic voxels. */
  size: number | [number, number, number];
  /** Per-voxel brightness noise. */
  jitter?: number;
  /** Per-vertex ambient occlusion. */
  ao?: boolean;
  /** Snaps colours (e.g. to a fixed palette). */
  quantize?: (c: Colour) => Colour;
  /** Toy-brick studs on every voxel whose top is exposed. */
  studs?: boolean;
  /** Material for lit or glowing voxels; `vertexColors` is false for the instanced studs. */
  material?: (glow: boolean, vertexColors: boolean) => THREE.Material;
}

const EMPTY = 0;
const OUTSIDE = 1;
const SOLID = 2;

const DIRS: [number, number, number][] = [
  [1, 0, 0],
  [-1, 0, 0],
  [0, 1, 0],
  [0, -1, 0],
  [0, 0, 1],
  [0, 0, -1],
];

const AO_LEVELS = [0.5, 0.68, 0.84, 1];

function colourOf(material: THREE.Material | THREE.Material[]): Colour {
  const m = Array.isArray(material) ? material[0]! : material;
  if (m instanceof THREE.MeshStandardMaterial) {
    if (m.emissiveIntensity > 0 && m.emissive.getHex() !== 0) {
      const k = Math.min(m.emissiveIntensity, 3);
      return {
        r: m.emissive.r * k,
        g: m.emissive.g * k,
        b: m.emissive.b * k,
        glow: true,
      };
    }
    return { r: m.color.r, g: m.color.g, b: m.color.b, glow: false };
  }
  const c = (m as THREE.MeshBasicMaterial).color ?? new THREE.Color(1, 1, 1);
  return { r: c.r, g: c.g, b: c.b, glow: false };
}

function hash(x: number, y: number, z: number): number {
  let h = (x * 374761393 + y * 668265263 + z * 2147483647) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

function defaultMaterial(glow: boolean, vertexColors: boolean): THREE.Material {
  return glow
    ? new THREE.MeshBasicMaterial({ vertexColors })
    : new THREE.MeshStandardMaterial({
        vertexColors,
        roughness: 0.82,
        metalness: 0.05,
      });
}

function normalize(o: number | VoxelOptions): VoxelOptions {
  return typeof o === 'number' ? { size: o } : o;
}

/**
 * Voxelizes `sources` into one solid mesh plus, if any part glows, one
 * unlit mesh for the emissive voxels. Both live in voxel space.
 */
export function voxelize(sources: VoxelSource[], options: number | VoxelOptions): THREE.Group {
  const opts = normalize(options);
  const [sx, sy, sz] = typeof opts.size === 'number' ? [opts.size, opts.size, opts.size] : opts.size;
  if (!sources.length) return new THREE.Group();

  // Larger parts first so small details (visors, eyes, stripes) win overlaps.
  const items = sources.map((s) => {
    s.mesh.geometry.computeBoundingBox();
    const box = s.mesh.geometry.boundingBox!.clone().applyMatrix4(s.matrix);
    const v = box.getSize(new THREE.Vector3());
    const raw = colourOf(s.mesh.material);
    return {
      ...s,
      box,
      volume: v.x * v.y * v.z,
      colour: opts.quantize ? opts.quantize(raw) : raw,
    };
  });
  items.sort((a, b) => b.volume - a.volume || Number(a.colour.glow) - Number(b.colour.glow));

  const bounds = new THREE.Box3();
  for (const it of items) bounds.union(it.box);
  const min = [Math.floor(bounds.min.x / sx) - 1, Math.floor(bounds.min.y / sy) - 1, Math.floor(bounds.min.z / sz) - 1];
  const max = [Math.ceil(bounds.max.x / sx) + 1, Math.ceil(bounds.max.y / sy) + 1, Math.ceil(bounds.max.z / sz) + 1];
  const nx = max[0]! - min[0]! + 1;
  const ny = max[1]! - min[1]! + 1;
  const nz = max[2]! - min[2]! + 1;
  const state = new Uint8Array(nx * ny * nz);
  const colour = new Int32Array(nx * ny * nz);
  const palette: Colour[] = [];
  const at = (x: number, y: number, z: number) => (z * ny + y) * nx + x;

  // 1. Surface sampling.
  const a = new THREE.Vector3();
  const b = new THREE.Vector3();
  const c = new THREE.Vector3();
  const ab = new THREE.Vector3();
  const ac = new THREE.Vector3();
  const p = new THREE.Vector3();
  const step = Math.min(sx, sy, sz) * 0.45;
  for (const it of items) {
    const ci = palette.push(it.colour) - 1;
    const geo = it.mesh.geometry;
    const pos = geo.getAttribute('position');
    const index = geo.getIndex();
    const triCount = index ? index.count / 3 : pos.count / 3;
    for (let t = 0; t < triCount; t++) {
      a.fromBufferAttribute(pos, index ? index.getX(t * 3) : t * 3).applyMatrix4(it.matrix);
      b.fromBufferAttribute(pos, index ? index.getX(t * 3 + 1) : t * 3 + 1).applyMatrix4(it.matrix);
      c.fromBufferAttribute(pos, index ? index.getX(t * 3 + 2) : t * 3 + 2).applyMatrix4(it.matrix);
      ab.subVectors(b, a);
      ac.subVectors(c, a);
      const longest = Math.max(a.distanceTo(b), b.distanceTo(c), c.distanceTo(a));
      const n = Math.max(1, Math.ceil(longest / step));
      for (let i = 0; i <= n; i++) {
        for (let j = 0; j <= n - i; j++) {
          p.copy(a)
            .addScaledVector(ab, i / n)
            .addScaledVector(ac, j / n);
          const cell = at(Math.floor(p.x / sx) - min[0]!, Math.floor(p.y / sy) - min[1]!, Math.floor(p.z / sz) - min[2]!);
          state[cell] = SOLID;
          colour[cell] = ci;
        }
      }
    }
  }

  // 2. Flood the outside from the padded border; whatever it cannot reach is inside.
  const queue: number[] = [];
  const push = (x: number, y: number, z: number) => {
    if (x < 0 || y < 0 || z < 0 || x >= nx || y >= ny || z >= nz) return;
    const i = at(x, y, z);
    if (state[i] !== EMPTY) return;
    state[i] = OUTSIDE;
    queue.push(i);
  };
  push(0, 0, 0);
  while (queue.length) {
    const i = queue.pop()!;
    const x = i % nx;
    const y = Math.floor(i / nx) % ny;
    const z = Math.floor(i / (nx * ny));
    for (const [dx, dy, dz] of DIRS) push(x + dx, y + dy, z + dz);
  }
  return meshGrid({ nx, ny, nz, min, state, colour, palette }, opts);
}

/** A filled voxel grid: cells are OUTSIDE or solid, with an index into `palette`. */
interface Grid {
  nx: number;
  ny: number;
  nz: number;
  /** Grid cell (0, 0, 0) in voxel coordinates. */
  min: number[];
  state: Uint8Array;
  colour: Int32Array;
  palette: Colour[];
}

/** 3. Faces towards the outside (and studs on exposed tops), split into lit and glowing meshes. */
function meshGrid(grid: Grid, opts: VoxelOptions): THREE.Group {
  const { nx, ny, nz, min, state, colour, palette } = grid;
  const [sx, sy, sz] = typeof opts.size === 'number' ? [opts.size, opts.size, opts.size] : opts.size;
  const jitter = opts.jitter ?? 0.1;
  const useAo = opts.ao ?? true;
  const makeMaterial = opts.material ?? defaultMaterial;
  const out = new THREE.Group();
  const at = (x: number, y: number, z: number) => (z * ny + y) * nx + x;
  const solid = (x: number, y: number, z: number) => x >= 0 && y >= 0 && z >= 0 && x < nx && y < ny && z < nz && state[at(x, y, z)] !== OUTSIDE;

  const size = [sx, sy, sz];
  const buffers = {
    lit: {
      pos: [] as number[],
      nrm: [] as number[],
      col: [] as number[],
      idx: [] as number[],
      studs: [] as number[],
    },
    glow: {
      pos: [] as number[],
      nrm: [] as number[],
      col: [] as number[],
      idx: [] as number[],
      studs: [] as number[],
    },
  };
  for (let z = 0; z < nz; z++) {
    for (let y = 0; y < ny; y++) {
      for (let x = 0; x < nx; x++) {
        const i = at(x, y, z);
        if (state[i] === OUTSIDE) continue;
        const col = palette[colour[i]!]!;
        const buf = col.glow ? buffers.glow : buffers.lit;
        const shade = col.glow ? 1 : 1 + (hash(x + min[0]!, y + min[1]!, z + min[2]!) - 0.5) * jitter;
        if (opts.studs && !solid(x, y + 1, z)) {
          buf.studs.push((x + min[0]! + 0.5) * sx, (y + min[1]! + 1) * sy, (z + min[2]! + 0.5) * sz, col.r * shade, col.g * shade, col.b * shade);
        }
        for (const [dx, dy, dz] of DIRS) {
          if (solid(x + dx, y + dy, z + dz)) continue;
          const axis = dx ? 0 : dy ? 1 : 2;
          const sign = dx + dy + dz;
          const u: [number, number, number] = axis === 0 ? [0, 1, 0] : axis === 1 ? [0, 0, 1] : [1, 0, 0];
          const v: [number, number, number] = axis === 0 ? [0, 0, 1] : axis === 1 ? [1, 0, 0] : [0, 1, 0];
          const base = buf.pos.length / 3;
          const ao: number[] = [];
          for (const [cu, cv] of [
            [0, 0],
            [1, 0],
            [1, 1],
            [0, 1],
          ] as const) {
            const su = cu ? 1 : -1;
            const sv = cv ? 1 : -1;
            const ox = x + dx;
            const oy = y + dy;
            const oz = z + dz;
            let level = 3;
            if (useAo) {
              const s1 = solid(ox + u[0] * su, oy + u[1] * su, oz + u[2] * su);
              const s2 = solid(ox + v[0] * sv, oy + v[1] * sv, oz + v[2] * sv);
              const cr = solid(ox + u[0] * su + v[0] * sv, oy + u[1] * su + v[1] * sv, oz + u[2] * su + v[2] * sv);
              level = s1 && s2 ? 0 : 3 - (Number(s1) + Number(s2) + Number(cr));
            }
            ao.push(level);
            const corner = [
              x + min[0]! + (dx > 0 ? 1 : 0) + u[0] * cu + v[0] * cv,
              y + min[1]! + (dy > 0 ? 1 : 0) + u[1] * cu + v[1] * cv,
              z + min[2]! + (dz > 0 ? 1 : 0) + u[2] * cu + v[2] * cv,
            ];
            buf.pos.push(corner[0]! * size[0]!, corner[1]! * size[1]!, corner[2]! * size[2]!);
            buf.nrm.push(dx, dy, dz);
            const k = col.glow ? 1 : shade * AO_LEVELS[level]!;
            buf.col.push(col.r * k, col.g * k, col.b * k);
          }
          // Winding depends on the face direction; flip the diagonal to keep AO smooth.
          const flip = ao[0]! + ao[2]! < ao[1]! + ao[3]!;
          const quad = flip ? [1, 2, 3, 1, 3, 0] : [0, 1, 2, 0, 2, 3];
          const ordered = sign > 0 ? quad : [quad[0]!, quad[2]!, quad[1]!, quad[3]!, quad[5]!, quad[4]!];
          for (const q of ordered) buf.idx.push(base + q);
        }
      }
    }
  }

  const studGeometry = new THREE.CylinderGeometry(0.3 * sx, 0.3 * sx, 0.18 * sy, 10);
  for (const [kind, buf] of Object.entries(buffers)) {
    const glow = kind === 'glow';
    if (buf.idx.length) {
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.Float32BufferAttribute(buf.pos, 3));
      g.setAttribute('normal', new THREE.Float32BufferAttribute(buf.nrm, 3));
      g.setAttribute('color', new THREE.Float32BufferAttribute(buf.col, 3));
      g.setIndex(buf.idx);
      const mesh = new THREE.Mesh(g, makeMaterial(glow, true));
      mesh.castShadow = !glow;
      mesh.receiveShadow = true;
      out.add(mesh);
    }
    const count = buf.studs.length / 6;
    if (count) {
      const studs = new THREE.InstancedMesh(studGeometry, makeMaterial(glow, false), count);
      const m = new THREE.Matrix4();
      const colourValue = new THREE.Color();
      for (let s = 0; s < count; s++) {
        const o = s * 6;
        m.makeTranslation(buf.studs[o]!, buf.studs[o + 1]! + 0.09 * sy, buf.studs[o + 2]!);
        studs.setMatrixAt(s, m);
        studs.setColorAt(s, colourValue.setRGB(buf.studs[o + 3]!, buf.studs[o + 4]!, buf.studs[o + 5]!));
      }
      studs.castShadow = !glow;
      studs.receiveShadow = true;
      out.add(studs);
    }
  }
  return out;
}

/**
 * Voxelizes a volume given cell by cell: `cell(x, y, z)` gets integer cell
 * coordinates in [min, max] (cell centre = (x + 0.5) * size) and returns the
 * cell's colour, or null for empty. Colours are pooled by identity, so return
 * shared constants. Used for shapes no mesh describes well (the globe).
 */
export function voxelField(
  min: [number, number, number],
  max: [number, number, number],
  cell: (x: number, y: number, z: number) => Colour | null,
  options: number | VoxelOptions,
): THREE.Group {
  // One empty cell of padding on every side, as voxelize() leaves.
  const lo = [min[0] - 1, min[1] - 1, min[2] - 1];
  const nx = max[0] - min[0] + 3;
  const ny = max[1] - min[1] + 3;
  const nz = max[2] - min[2] + 3;
  const state = new Uint8Array(nx * ny * nz).fill(OUTSIDE);
  const colour = new Int32Array(nx * ny * nz);
  const palette: Colour[] = [];
  const pooled = new Map<Colour, number>();
  for (let z = 1; z < nz - 1; z++) {
    for (let y = 1; y < ny - 1; y++) {
      for (let x = 1; x < nx - 1; x++) {
        const c = cell(x + lo[0]!, y + lo[1]!, z + lo[2]!);
        if (!c) continue;
        let ci = pooled.get(c);
        if (ci === undefined) pooled.set(c, (ci = palette.push(c) - 1));
        const i = (z * ny + y) * nx + x;
        state[i] = SOLID;
        colour[i] = ci;
      }
    }
  }
  return meshGrid({ nx, ny, nz, min: lo, state, colour, palette }, normalize(options));
}

/**
 * Replaces the direct mesh children of `group` with one voxel model in the
 * group's own space (keeps rig groups such as the gun pivot animatable).
 */
export function voxelizeChildren(group: THREE.Object3D, options: number | VoxelOptions, skip: Set<THREE.Object3D> = new Set()): void {
  group.updateMatrix();
  const meshes = group.children.filter((c): c is THREE.Mesh => c instanceof THREE.Mesh && !skip.has(c));
  if (!meshes.length) return;
  const sources = meshes.map((mesh) => {
    mesh.updateMatrix();
    return { mesh, matrix: mesh.matrix.clone() };
  });
  const model = voxelize(sources, options);
  for (const m of meshes) group.remove(m);
  group.add(model);
}

/** Voxelizes a whole prop (every descendant mesh) in the prop's local space. */
export function voxelizeProp(prop: THREE.Object3D, options: number | VoxelOptions): THREE.Group {
  prop.updateMatrixWorld(true);
  const inverse = prop.matrixWorld.clone().invert();
  const sources: VoxelSource[] = [];
  prop.traverse((o) => {
    if (o instanceof THREE.Mesh)
      sources.push({
        mesh: o,
        matrix: inverse.clone().multiply(o.matrixWorld),
      });
  });
  const model = voxelize(sources, options);
  model.position.copy(prop.position);
  model.rotation.copy(prop.rotation);
  // Keep light anchors.
  prop.traverse((o) => {
    if (o.userData.light) {
      const a = new THREE.Object3D();
      a.position.copy(o.getWorldPosition(new THREE.Vector3())).applyMatrix4(inverse);
      a.userData.light = o.userData.light;
      model.add(a);
    }
  });
  model.userData.kind = prop.userData.kind;
  return model;
}

/**
 * Voxelizes a whole articulated rig: every group's own mesh children become
 * one voxel model in that group's space, so each bone keeps animating.
 */
export function voxelizeRig(root: THREE.Object3D, options: number | VoxelOptions, skip: Set<THREE.Object3D> = new Set()): void {
  const groups: THREE.Object3D[] = [];
  root.traverse((o) => {
    if (o.children.some((c) => c instanceof THREE.Mesh && !skip.has(c))) groups.push(o);
  });
  for (const g of groups) voxelizeChildren(g, options, skip);
}

// ------------------------------------------------------------- game art

/** Voxel size of the game's art: a standing soldier is about 29 voxels tall. */
export const GAME_VOXEL = 1 / 16;

/**
 * Turns a unit model into voxels in place. Every bone keeps animating, groups
 * such as the drone keep their identity, and the hit flash stays smooth.
 */
export function voxelizeUnit(rig: { root: THREE.Object3D; flash: THREE.Object3D }): void {
  voxelizeRig(rig.root, GAME_VOXEL, new Set([rig.flash]));
}

/**
 * A single shape (tree, rock…) as stepped voxel geometry, white and shaded
 * with ambient occlusion, ready to be instanced and tinted per instance.
 */
export function voxelShape(geometry: THREE.BufferGeometry, size: number): THREE.BufferGeometry {
  const source = new THREE.Mesh(geometry, new THREE.MeshStandardMaterial({ color: 0xffffff }));
  const model = voxelize([{ mesh: source, matrix: new THREE.Matrix4() }], {
    size,
    jitter: 0.12,
  });
  const lit = model.children.find((c): c is THREE.Mesh => c instanceof THREE.Mesh);
  return lit ? lit.geometry : geometry;
}

export interface VoxelSurfaceOptions {
  /** Cell size in world units. */
  size?: number;
  /** Brightness noise between cells. */
  jitter?: number;
  /** Height of one storey: walls darken in steps where they meet each floor. */
  storey?: number;
  /** Darkening of the lowest cell row against the floor. */
  ao?: number;
  /** Paints a window per tile and storey on vertical faces (building facades). */
  windows?: boolean;
}

/**
 * Paints a material's surfaces as world-space voxel cells: every cell gets
 * its own shade, and walls darken in steps where they meet the floor, like
 * the ambient occlusion of a voxel model. Works on instanced and scaled
 * boxes without subdividing them, so the map keeps its instancing.
 */
export function voxelSurface<T extends THREE.Material>(material: T, options: VoxelSurfaceOptions = {}): T {
  const size = options.size ?? GAME_VOXEL;
  const jitter = options.jitter ?? 0.16;
  const storey = options.storey ?? 0;
  const ao = options.ao ?? 0.3;
  const windows = !!options.windows && storey > 0;
  material.onBeforeCompile = (shader) => {
    shader.vertexShader = shader.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 vVxPos;\nvarying vec3 vVxNormal;').replace(
      '#include <project_vertex>',
      `#include <project_vertex>
        vec4 vxWorld = vec4(transformed, 1.0);
        vec3 vxNormal = objectNormal;
        #ifdef USE_INSTANCING
          vxWorld = instanceMatrix * vxWorld;
          vxNormal = mat3(instanceMatrix) * vxNormal;
        #endif
        vVxPos = (modelMatrix * vxWorld).xyz;
        vVxNormal = normalize(mat3(modelMatrix) * vxNormal);`,
    );
    shader.fragmentShader = shader.fragmentShader.replace('#include <common>', '#include <common>\nvarying vec3 vVxPos;\nvarying vec3 vVxNormal;').replace(
      '#include <color_fragment>',
      `#include <color_fragment>
        {
          // The cell just behind the surface.
          vec3 cell = floor(vVxPos / ${size.toFixed(5)} - vVxNormal * 0.5);
          // Hash without sin(): stays precise for large world coordinates on the GPU.
          vec3 hp = fract(cell * vec3(0.1031, 0.1030, 0.0973));
          hp += dot(hp, hp.yxz + 33.33);
          float noise = fract((hp.x + hp.y) * hp.z);
          float shade = 1.0 - ${jitter.toFixed(3)} * noise;
          ${
            storey > 0
              ? `float above = mod((cell.y + 0.5) * ${size.toFixed(5)}, ${storey.toFixed(4)});
          float side = 1.0 - abs(vVxNormal.y);
          float contact = 1.0 - ${ao.toFixed(3)} * (1.0 - smoothstep(0.0, ${(size * 4).toFixed(4)}, above));
          shade *= mix(1.0, contact, side);`
              : ''
          }
          diffuseColor.rgb *= shade;
          ${
            windows
              ? `// One window per tile and storey, snapped to whole cells, with a lit sill.
          if (abs(vVxNormal.y) < 0.5) {
            vec3 centre = (cell + 0.5) * ${size.toFixed(5)};
            float along = fract(abs(vVxNormal.x) > 0.5 ? centre.z : centre.x);
            float up = mod(centre.y, ${storey.toFixed(4)});
            float pane = step(0.22, along) * step(along, 0.78) * step(0.75, up) * step(up, 1.75);
            float sill = step(0.18, along) * step(along, 0.82) * step(0.66, up) * step(up, 0.75);
            diffuseColor.rgb = mix(diffuseColor.rgb, vec3(0.12, 0.17, 0.22) * (0.8 + 0.4 * noise), pane);
            diffuseColor.rgb = mix(diffuseColor.rgb, diffuseColor.rgb * 1.25, sill);
          }`
              : ''
          }
        }`,
    );
  };
  material.customProgramCacheKey = () => `voxel-surface-${size}-${jitter}-${storey}-${ao}-${windows}`;
  return material;
}

export interface StaticVoxelOptions {
  size?: number;
  /** Surface shader for the parts left as they are because they are too big to voxelize. */
  surface?: VoxelSurfaceOptions;
  /** Subtrees left untouched (already voxel, or rebuilt elsewhere). */
  skip?: THREE.Object3D[];
  /** userData keys of parts that animate their transform or visibility: each is voxelized alone, in a group that keeps the key. */
  animated?: string[];
  /** userData keys of parts that must stay exactly as they are (e.g. their material is animated). */
  keep?: string[];
}

/**
 * Turns a built 3D set into voxel art in place. Small opaque parts are merged
 * into one voxel model per parent group (so groups keep moving); parts that
 * animate on their own are voxelized one by one inside a group that keeps
 * their userData; big structural slabs (walls, floors, rock) keep their shape
 * and get the voxel surface shader, as voxelizing them would cost millions of
 * cells. Transparent parts (glass tubes, holograms) are voxelized alone and
 * keep their opacity. Invisible parts (hotspots) and kept parts are not touched.
 */
export function voxelizeStatic(root: THREE.Object3D, options: StaticVoxelOptions = {}): void {
  const size = options.size ?? GAME_VOXEL;
  const skip = new Set(options.skip ?? []);
  const animated = options.animated ?? [];
  const keep = options.keep ?? [];
  root.updateMatrixWorld(true);
  const byParent = new Map<THREE.Object3D, THREE.Mesh[]>();
  const loners: THREE.Mesh[] = [];
  const box = new THREE.Box3();
  const extent = new THREE.Vector3();

  const visit = (o: THREE.Object3D) => {
    if (skip.has(o)) return;
    for (const child of [...o.children]) visit(child);
    if (!(o instanceof THREE.Mesh) || o instanceof THREE.InstancedMesh) return;
    const material = Array.isArray(o.material) ? o.material[0] : o.material;
    if (!material || material.visible === false || !o.parent) return;
    if (keep.some((k) => o.userData[k] !== undefined)) return;
    box.setFromObject(o).getSize(extent);
    const longest = Math.max(extent.x, extent.y, extent.z);
    const thinnest = Math.min(extent.x, extent.y, extent.z);
    if (longest > 5.5 || (longest > 3 && thinnest < 0.3)) {
      // Shared materials get the shader once (re-applying would recompile it).
      if (!material.userData.voxelSurface) {
        material.userData.voxelSurface = true;
        voxelSurface(material, options.surface);
        material.needsUpdate = true;
      }
      return;
    }
    if (material.transparent || animated.some((k) => o.userData[k] !== undefined)) loners.push(o);
    else {
      const list = byParent.get(o.parent) ?? [];
      list.push(o);
      byParent.set(o.parent, list);
    }
  };
  visit(root);

  for (const [parent, meshes] of byParent) {
    const keepHere = new Set(parent.children.filter((c) => !meshes.includes(c as THREE.Mesh)));
    voxelizeChildren(parent, size, keepHere);
  }
  for (const mesh of loners) {
    const group = new THREE.Group();
    group.position.copy(mesh.position);
    group.quaternion.copy(mesh.quaternion);
    group.scale.copy(mesh.scale);
    group.userData = { ...mesh.userData };
    const model = voxelize([{ mesh, matrix: new THREE.Matrix4() }], size);
    const source = Array.isArray(mesh.material) ? mesh.material[0]! : mesh.material;
    if (source.transparent) {
      model.traverse((m) => {
        if (!(m instanceof THREE.Mesh)) return;
        const voxels = m.material as THREE.Material;
        voxels.transparent = true;
        voxels.opacity = source.opacity;
        voxels.depthWrite = false;
        m.castShadow = false;
      });
    }
    group.add(model);
    const parent = mesh.parent!;
    parent.children[parent.children.indexOf(mesh)] = group;
    group.parent = parent;
    mesh.parent = null;
  }
}
