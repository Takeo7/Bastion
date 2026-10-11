// MagicaVoxel .vox files: read and write. Hand-made models (soldiers,
// aliens) live in .vox files so they can be opened and touched up in
// MagicaVoxel. One file holds a kit: several named models placed in one
// scene, each the voxels of one bone (or one variant of it, "head:helmet").
//
// Axes: MagicaVoxel is z-up and looks at the model from −y; the game is y-up
// with +z forward. A model cell (x, y, z) in game axes is MagicaVoxel
// (x, −z − 1, y). Colours are palette indices 1–255; the palette is sRGB.
// Indices with an emissive material (MATL `_type: _emit`) glow.
//
// No three.js here: the tools that write the kits run in Node.

/**
 * Colour indices the game repaints on each soldier: the customizer's armour
 * (primary), plates (secondary) and visor, the owner's colour as a team mark,
 * and the gun by weapon tier. Kits draw them in placeholder colours.
 */
export const SLOT = {
  primary: 1,
  primaryLight: 2,
  primaryDark: 3,
  secondary: 4,
  secondaryLight: 5,
  secondaryDark: 6,
  visor: 7,
  team: 8,
  gun: 9,
  gunDark: 10,
  /** Coils or chamber: dark metal on conventional guns, glowing on magnetic and plasma. */
  gunGlow: 11,
  /** Hood and cape: the plates' colour, dyed dark like cloth. */
  cloth: 12,
  clothDark: 13,
} as const;

/** One named model: its cells in game axes, scene position included. */
export interface VoxPart {
  name: string;
  /** Cells as x, y, z, colour index, flattened. */
  cells: Int16Array;
  /** Shown hidden when the file opens in MagicaVoxel (variants that overlap the default look). */
  hidden?: boolean;
}

export interface VoxKit {
  parts: VoxPart[];
  /** 256 sRGB colours as 0xrrggbb; entry i is colour index i (0 is unused). */
  palette: number[];
  /** Colour indices that glow. */
  emit: Set<number>;
}

// ------------------------------------------------------------------ reading

type Dict = Record<string, string>;

interface SceneNode {
  type: 'trn' | 'grp' | 'shp';
  attrs: Dict;
  children: number[];
  /** nTRN: translation and rotation of its child. */
  t?: [number, number, number];
  r?: number[];
  /** nSHP: the model it shows. */
  model?: number;
}

/** The rotation byte of a transform frame as a row-major 3 × 3 matrix. */
function rotation(byte: number): number[] {
  const first = byte & 3;
  const second = (byte >> 2) & 3;
  const third = 3 - first - second;
  const m = [0, 0, 0, 0, 0, 0, 0, 0, 0];
  m[first] = byte & 16 ? -1 : 1;
  m[3 + second] = byte & 32 ? -1 : 1;
  m[6 + third] = byte & 64 ? -1 : 1;
  return m;
}

const IDENTITY = [1, 0, 0, 0, 1, 0, 0, 0, 1];

function mul(a: number[], b: number[]): number[] {
  const out = new Array<number>(9);
  for (let i = 0; i < 3; i++) for (let j = 0; j < 3; j++) out[i * 3 + j] = a[i * 3]! * b[j]! + a[i * 3 + 1]! * b[3 + j]! + a[i * 3 + 2]! * b[6 + j]!;
  return out;
}

function apply(m: number[], v: [number, number, number]): [number, number, number] {
  return [m[0]! * v[0] + m[1]! * v[1] + m[2]! * v[2], m[3]! * v[0] + m[4]! * v[1] + m[5]! * v[2], m[6]! * v[0] + m[7]! * v[1] + m[8]! * v[2]];
}

/** MagicaVoxel's default palette is not needed: every kit we write carries its own. */
export function readVox(data: Uint8Array): VoxKit {
  const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
  const text = (at: number, n: number) => String.fromCharCode(...data.subarray(at, at + n));
  if (text(0, 4) !== 'VOX ') throw new Error('Not a .vox file');
  let p = 8;
  const int = () => {
    const v = view.getInt32(p, true);
    p += 4;
    return v;
  };
  const str = () => {
    const n = int();
    const s = new TextDecoder().decode(data.subarray(p, p + n));
    p += n;
    return s;
  };
  const dict = (): Dict => {
    const out: Dict = {};
    for (let n = int(); n > 0; n--) {
      const k = str();
      out[k] = str();
    }
    return out;
  };

  const sizes: [number, number, number][] = [];
  const models: Uint8Array[] = [];
  const nodes = new Map<number, SceneNode>();
  const palette = new Array<number>(256).fill(0);
  const emit = new Set<number>();

  // MAIN's children follow its header.
  p = 8 + 12;
  while (p < data.length) {
    const id = text(p, 4);
    const size = view.getInt32(p + 4, true);
    const start = p + 12;
    p = start;
    switch (id) {
      case 'SIZE':
        sizes.push([int(), int(), int()]);
        break;
      case 'XYZI': {
        const n = int();
        models.push(data.slice(p, p + n * 4));
        break;
      }
      case 'RGBA':
        // Entry i is colour index i + 1.
        for (let i = 0; i < 255; i++) palette[i + 1] = (data[p + i * 4]! << 16) | (data[p + i * 4 + 1]! << 8) | data[p + i * 4 + 2]!;
        break;
      case 'MATL': {
        const index = int();
        if (dict()._type === '_emit') emit.add(index);
        break;
      }
      case 'nTRN': {
        const nodeId = int();
        const attrs = dict();
        const child = int();
        int(); // reserved
        int(); // layer
        const frames = int();
        const frame = frames > 0 ? dict() : {};
        const t = (frame._t ?? '0 0 0').split(' ').map(Number) as [number, number, number];
        nodes.set(nodeId, { type: 'trn', attrs, children: [child], t, r: frame._r ? rotation(Number(frame._r)) : IDENTITY });
        break;
      }
      case 'nGRP': {
        const nodeId = int();
        const attrs = dict();
        const children: number[] = [];
        for (let n = int(); n > 0; n--) children.push(int());
        nodes.set(nodeId, { type: 'grp', attrs, children });
        break;
      }
      case 'nSHP': {
        const nodeId = int();
        const attrs = dict();
        int(); // number of models: always 1
        const model = int();
        nodes.set(nodeId, { type: 'shp', attrs, children: [], model });
        break;
      }
    }
    p = start + size;
  }

  const parts: VoxPart[] = [];
  const emitModel = (model: number, name: string, t: [number, number, number], r: number[]) => {
    const [sx, sy, sz] = sizes[model]!;
    const voxels = models[model]!;
    const cells = new Int16Array((voxels.length / 4) * 4);
    for (let i = 0; i < voxels.length / 4; i++) {
      // Cell centre relative to the model's centre, rotated, then placed.
      const c = apply(r, [voxels[i * 4]! + 0.5 - sx / 2, voxels[i * 4 + 1]! + 0.5 - sy / 2, voxels[i * 4 + 2]! + 0.5 - sz / 2]);
      const mx = Math.floor(t[0] + c[0]);
      const my = Math.floor(t[1] + c[1]);
      const mz = Math.floor(t[2] + c[2]);
      cells.set([mx, mz, -my - 1, voxels[i * 4 + 3]!], i * 4);
    }
    parts.push({ name, cells });
  };

  if (nodes.size) {
    const walk = (id: number, t: [number, number, number], r: number[], name: string) => {
      const node = nodes.get(id);
      if (!node) return;
      if (node.type === 'trn') {
        const nt = apply(r, node.t!);
        walk(node.children[0]!, [t[0] + nt[0], t[1] + nt[1], t[2] + nt[2]], mul(r, node.r!), node.attrs._name ?? name);
      } else if (node.type === 'grp') {
        for (const c of node.children) walk(c, t, r, name);
      } else emitModel(node.model!, name, t, r);
    };
    walk(0, [0, 0, 0], IDENTITY, '');
  } else {
    // Old files without a scene: models at the origin, unnamed.
    models.forEach((_, i) => emitModel(i, `model${i}`, [Math.floor(sizes[i]![0] / 2), Math.floor(sizes[i]![1] / 2), Math.floor(sizes[i]![2] / 2)], IDENTITY));
  }
  return { parts, palette, emit };
}

// ------------------------------------------------------------------ writing

/** Writes a kit: one model per part, each named and placed where its cells are. */
export function writeVox(kit: VoxKit): Uint8Array {
  const chunks: Uint8Array[] = [];
  const enc = new TextEncoder();
  const chunk = (id: string, content: Uint8Array) => {
    const head = new Uint8Array(12);
    head.set(enc.encode(id), 0);
    new DataView(head.buffer).setInt32(4, content.length, true);
    chunks.push(head, content);
  };
  const buf = () => {
    const parts: number[] = [];
    const out = {
      int(v: number) {
        const b = new Uint8Array(4);
        new DataView(b.buffer).setInt32(0, v, true);
        parts.push(...b);
        return out;
      },
      str(s: string) {
        const b = enc.encode(s);
        out.int(b.length);
        parts.push(...b);
        return out;
      },
      dict(d: Dict) {
        const entries = Object.entries(d);
        out.int(entries.length);
        for (const [k, v] of entries) out.str(k).str(v);
        return out;
      },
      bytes: () => new Uint8Array(parts),
    };
    return out;
  };

  // Models, each in MagicaVoxel axes with its own bounds.
  const placed = kit.parts.map((part) => {
    const n = part.cells.length / 4;
    const mv = new Int32Array(n * 4);
    const min = [Infinity, Infinity, Infinity];
    const max = [-Infinity, -Infinity, -Infinity];
    for (let i = 0; i < n; i++) {
      const v = [part.cells[i * 4]!, -part.cells[i * 4 + 2]! - 1, part.cells[i * 4 + 1]!];
      for (let k = 0; k < 3; k++) {
        min[k] = Math.min(min[k]!, v[k]!);
        max[k] = Math.max(max[k]!, v[k]!);
      }
      mv.set([...v, part.cells[i * 4 + 3]!], i * 4);
    }
    const size = [0, 1, 2].map((k) => max[k]! - min[k]! + 1) as [number, number, number];
    const voxels = new Uint8Array(n * 4);
    for (let i = 0; i < n; i++) voxels.set([mv[i * 4]! - min[0]!, mv[i * 4 + 1]! - min[1]!, mv[i * 4 + 2]! - min[2]!, mv[i * 4 + 3]!], i * 4);
    // The scene places a model by its centre cell.
    const t = [0, 1, 2].map((k) => min[k]! + Math.floor(size[k]! / 2));
    return { name: part.name, hidden: part.hidden, size, voxels, t };
  });

  for (const m of placed) {
    chunk('SIZE', buf().int(m.size[0]).int(m.size[1]).int(m.size[2]).bytes());
    const xyzi = new Uint8Array(4 + m.voxels.length);
    new DataView(xyzi.buffer).setInt32(0, m.voxels.length / 4, true);
    xyzi.set(m.voxels, 4);
    chunk('XYZI', xyzi);
  }

  // Scene: root transform → group → one transform + shape per model.
  chunk('nTRN', buf().int(0).dict({}).int(1).int(-1).int(-1).int(1).dict({}).bytes());
  const group = buf().int(1).dict({}).int(placed.length);
  placed.forEach((_, i) => group.int(2 + i * 2));
  chunk('nGRP', group.bytes());
  placed.forEach((m, i) => {
    const trn = 2 + i * 2;
    const attrs: Dict = m.hidden ? { _name: m.name, _hidden: '1' } : { _name: m.name };
    chunk('nTRN', buf().int(trn).dict(attrs).int(trn + 1).int(-1).int(0).int(1).dict({ _t: m.t.join(' ') }).bytes());
    chunk('nSHP', buf().int(trn + 1).dict({}).int(1).int(i).dict({}).bytes());
  });

  const rgba = new Uint8Array(256 * 4);
  for (let i = 0; i < 255; i++) {
    const c = kit.palette[i + 1] ?? 0;
    rgba.set([(c >> 16) & 255, (c >> 8) & 255, c & 255, 255], i * 4);
  }
  chunk('RGBA', rgba);
  for (const index of [...kit.emit].sort((a, b) => a - b)) chunk('MATL', buf().int(index).dict({ _type: '_emit', _emit: '0.6', _flux: '1' }).bytes());

  const body = chunks.reduce((n, c) => n + c.length, 0);
  const out = new Uint8Array(20 + body);
  out.set(enc.encode('VOX '), 0);
  const dv = new DataView(out.buffer);
  dv.setInt32(4, 200, true);
  out.set(enc.encode('MAIN'), 8);
  dv.setInt32(12, 0, true);
  dv.setInt32(16, body, true);
  let at = 20;
  for (const c of chunks) {
    out.set(c, at);
    at += c.length;
  }
  return out;
}
