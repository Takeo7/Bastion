// Sculpting voxel models by hand, in code: one Part per bone, built from
// boxes, ellipsoids and single cells on the integer grid, then painted. This
// is how the hand-made kits are drawn (there is no MagicaVoxel here); the
// result is written as .vox and can be touched up in MagicaVoxel after.
//
// Cells are in figure space, in voxels (1/16 of a tile): y up from the
// ground, +z forward, +x the figure's left. x and z are centred on the cell
// (cell 0 spans −0.5…0.5), so odd widths are symmetric about the figure's
// axis; y counts from the ground (cell 0 spans 0…1).

import type { VoxKit, VoxPart } from '../../src/game/vox.ts';

const key = (x: number, y: number, z: number) => `${x},${y},${z}`;

export class Part {
  readonly cells = new Map<string, [number, number, number, number]>();
  /** Hidden when the kit opens in MagicaVoxel: variants that would overlap the default look. */
  hidden = false;

  constructor(readonly name: string) {}

  hide(hidden = true): this {
    this.hidden = hidden;
    return this;
  }

  /** Draws a line of cells from one cell to another (both included). */
  line(x0: number, y0: number, z0: number, x1: number, y1: number, z1: number, c: number): this {
    const n = Math.max(Math.abs(x1 - x0), Math.abs(y1 - y0), Math.abs(z1 - z0));
    for (let i = 0; i <= n; i++) {
      const t = n ? i / n : 0;
      this.set(Math.round(x0 + (x1 - x0) * t), Math.round(y0 + (y1 - y0) * t), Math.round(z0 + (z1 - z0) * t), c);
    }
    return this;
  }

  set(x: number, y: number, z: number, c: number): this {
    this.cells.set(key(x, y, z), [x, y, z, c]);
    return this;
  }

  get(x: number, y: number, z: number): number {
    return this.cells.get(key(x, y, z))?.[3] ?? 0;
  }

  has(x: number, y: number, z: number): boolean {
    return this.cells.has(key(x, y, z));
  }

  del(x: number, y: number, z: number): this {
    this.cells.delete(key(x, y, z));
    return this;
  }

  /** Fills the box between two corner cells, both included. */
  box(x0: number, y0: number, z0: number, x1: number, y1: number, z1: number, c: number): this {
    for (let x = Math.min(x0, x1); x <= Math.max(x0, x1); x++)
      for (let y = Math.min(y0, y1); y <= Math.max(y0, y1); y++) for (let z = Math.min(z0, z1); z <= Math.max(z0, z1); z++) this.set(x, y, z, c);
    return this;
  }

  /** Removes the box between two corner cells. */
  carve(x0: number, y0: number, z0: number, x1: number, y1: number, z1: number): this {
    for (let x = Math.min(x0, x1); x <= Math.max(x0, x1); x++)
      for (let y = Math.min(y0, y1); y <= Math.max(y0, y1); y++) for (let z = Math.min(z0, z1); z <= Math.max(z0, z1); z++) this.del(x, y, z);
    return this;
  }

  /** Repaints the filled cells inside the box (empty ones stay empty). */
  paint(x0: number, y0: number, z0: number, x1: number, y1: number, z1: number, c: number): this {
    for (const cell of this.cells.values()) {
      const [x, y, z] = cell;
      if (x >= Math.min(x0, x1) && x <= Math.max(x0, x1) && y >= Math.min(y0, y1) && y <= Math.max(y0, y1) && z >= Math.min(z0, z1) && z <= Math.max(z0, z1)) cell[3] = c;
    }
    return this;
  }

  /** Repaints the cells for which `test` holds. */
  paintWhere(test: (x: number, y: number, z: number, c: number) => boolean, c: number): this {
    for (const cell of this.cells.values()) if (test(...cell)) cell[3] = c;
    return this;
  }

  /** Fills an ellipsoid centred at (cx, cy, cz) with radii in cells. */
  ellipsoid(cx: number, cy: number, cz: number, rx: number, ry: number, rz: number, c: number): this {
    for (let x = Math.floor(cx - rx); x <= Math.ceil(cx + rx); x++)
      for (let y = Math.floor(cy - ry); y <= Math.ceil(cy + ry); y++)
        for (let z = Math.floor(cz - rz); z <= Math.ceil(cz + rz); z++) {
          // y cells span y…y+1: test their centre.
          const d = ((x - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 + ((z - cz) / rz) ** 2;
          if (d <= 1) this.set(x, y, z, c);
        }
    return this;
  }

  /** Copies every cell to its mirror across the figure's axis (x → −x). */
  mirrorX(): this {
    for (const [x, y, z, c] of [...this.cells.values()]) this.set(-x, y, z, c);
    return this;
  }

  /** The part mirrored to the other side, under a new name. */
  mirrored(name: string): Part {
    const p = new Part(name).hide(this.hidden);
    for (const [x, y, z, c] of this.cells.values()) p.set(-x, y, z, c);
    return p;
  }

  /** Removes the cells that stick out alone (four or more open faces): stray steps on round shapes. */
  smooth(): this {
    const open = (x: number, y: number, z: number) =>
      [
        [1, 0, 0],
        [-1, 0, 0],
        [0, 1, 0],
        [0, -1, 0],
        [0, 0, 1],
        [0, 0, -1],
      ].filter(([dx, dy, dz]) => !this.has(x + dx!, y + dy!, z + dz!)).length;
    for (const [x, y, z] of [...this.cells.values()]) if (open(x, y, z) >= 4) this.del(x, y, z);
    return this;
  }

  /** Cells that show: at least one neighbour is empty. */
  surface(): [number, number, number, number][] {
    return [...this.cells.values()].filter(([x, y, z]) => [-1, 1].some((d) => !this.has(x + d, y, z) || !this.has(x, y + d, z) || !this.has(x, y, z + d)));
  }

  toVox(): VoxPart {
    const cells = new Int16Array(this.cells.size * 4);
    let i = 0;
    for (const v of this.cells.values()) cells.set(v, (i++) * 4);
    return { name: this.name, cells, hidden: this.hidden };
  }
}

export class Kit {
  readonly parts: Part[] = [];
  readonly palette = new Array<number>(256).fill(0);
  readonly emit = new Set<number>();

  part(name: string): Part {
    const p = new Part(name);
    this.parts.push(p);
    return p;
  }

  add(...parts: Part[]): this {
    this.parts.push(...parts);
    return this;
  }

  /** Names the colours: index → 0xrrggbb, glowing or not. */
  colours(entries: Record<number, number | [number, 'glow']>): this {
    for (const [i, v] of Object.entries(entries)) {
      const index = Number(i);
      if (Array.isArray(v)) {
        this.palette[index] = v[0];
        this.emit.add(index);
      } else this.palette[index] = v;
    }
    return this;
  }

  toVox(): VoxKit {
    return { parts: this.parts.filter((p) => p.cells.size).map((p) => p.toVox()), palette: this.palette, emit: this.emit };
  }
}
