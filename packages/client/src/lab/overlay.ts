import * as THREE from 'three';
import { ACTORS, blockedTiles, DEPTH, WIDTH } from './layout';

// Tactical interface drawn over every style the same way: movement range
// (blue: one action, yellow: dash), a planned path and the selection ring, so
// each look can be judged on how well the game stays readable.

const SOURCE = ACTORS.find((a) => a.id === 'assault')!;
const DEST = { x: 8, z: 9 };
const BLUE = 5;
const YELLOW = 10;

interface Reach {
  cost: Map<string, number>;
  parent: Map<string, string>;
}

function reach(): Reach {
  const blocked = blockedTiles();
  blocked.delete(`${SOURCE.x},${SOURCE.z}`);
  const cost = new Map<string, number>([[`${SOURCE.x},${SOURCE.z}`, 0]]);
  const parent = new Map<string, string>();
  const open = [{ x: SOURCE.x, z: SOURCE.z, c: 0 }];
  while (open.length) {
    open.sort((a, b) => a.c - b.c);
    const cur = open.shift()!;
    for (let dx = -1; dx <= 1; dx++) {
      for (let dz = -1; dz <= 1; dz++) {
        if (!dx && !dz) continue;
        const x = cur.x + dx;
        const z = cur.z + dz;
        if (x < 0 || z < 1 || x >= WIDTH || z >= DEPTH || blocked.has(`${x},${z}`)) continue;
        if (dx && dz && (blocked.has(`${cur.x + dx},${cur.z}`) || blocked.has(`${cur.x},${cur.z + dz}`))) continue;
        const c = cur.c + (dx && dz ? 1.5 : 1);
        if (c > YELLOW) continue;
        const key = `${x},${z}`;
        if (c < (cost.get(key) ?? Infinity)) {
          cost.set(key, c);
          parent.set(key, `${cur.x},${cur.z}`);
          open.push({ x, z, c });
        }
      }
    }
  }
  return { cost, parent };
}

function quads(tiles: [number, number][], inset: number, y: number): THREE.BufferGeometry {
  const pos: number[] = [];
  for (const [x, z] of tiles) {
    const a = [x + inset, z + inset];
    const b = [x + 1 - inset, z + 1 - inset];
    pos.push(a[0]!, y, a[1]!, a[0]!, y, b[1]!, b[0]!, y, b[1]!, a[0]!, y, a[1]!, b[0]!, y, b[1]!, b[0]!, y, a[1]!);
  }
  return withNormals(pos);
}

function withNormals(pos: number[]): THREE.BufferGeometry {
  const g = new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.computeVertexNormals();
  return g;
}

/** Thin quads along the outer edges of a tile set. */
function border(set: Set<string>, width: number, y: number): THREE.BufferGeometry {
  const pos: number[] = [];
  const strip = (x0: number, z0: number, x1: number, z1: number) => {
    const horizontal = z0 === z1;
    const hx = horizontal ? 0 : width / 2;
    const hz = horizontal ? width / 2 : 0;
    pos.push(x0 - hx, y, z0 - hz, x0 + hx, y, z0 + hz, x1 + hx, y, z1 + hz, x0 - hx, y, z0 - hz, x1 + hx, y, z1 + hz, x1 - hx, y, z1 - hz);
  };
  for (const key of set) {
    const [x, z] = key.split(',').map(Number) as [number, number];
    if (!set.has(`${x},${z - 1}`)) strip(x, z, x + 1, z);
    if (!set.has(`${x},${z + 1}`)) strip(x, z + 1, x + 1, z + 1);
    if (!set.has(`${x - 1},${z}`)) strip(x, z, x, z + 1);
    if (!set.has(`${x + 1},${z}`)) strip(x + 1, z, x + 1, z + 1);
  }
  return withNormals(pos);
}

const flat = (color: number, opacity: number) =>
  new THREE.MeshBasicMaterial({ color, transparent: true, opacity, depthWrite: false, toneMapped: false, side: THREE.DoubleSide });

export function buildOverlay(): THREE.Group {
  const g = new THREE.Group();
  const { cost, parent } = reach();
  const blue = new Set<string>();
  const yellow = new Set<string>();
  for (const [key, c] of cost) (c <= BLUE ? blue : yellow).add(key);
  const all = new Set([...blue, ...yellow]);
  const tiles = (set: Set<string>) => [...set].map((k) => k.split(',').map(Number) as [number, number]);

  g.add(new THREE.Mesh(quads(tiles(blue), 0.04, 0.025), flat(0x3fa9ff, 0.2)));
  g.add(new THREE.Mesh(quads(tiles(yellow), 0.04, 0.025), flat(0xffc94a, 0.08)));
  g.add(new THREE.Mesh(border(blue, 0.06, 0.03), flat(0x7fd8ff, 0.95)));
  g.add(new THREE.Mesh(border(all, 0.06, 0.03), flat(0xffc94a, 0.9)));

  // Planned path to DEST.
  const path: THREE.Vector3[] = [];
  let key: string | undefined = `${DEST.x},${DEST.z}`;
  while (key) {
    const [x, z] = key.split(',').map(Number) as [number, number];
    path.unshift(new THREE.Vector3(x + 0.5, 0.045, z + 0.5));
    key = parent.get(key);
  }
  for (let i = 0; i < path.length - 1; i++) {
    const a = path[i]!;
    const b = path[i + 1]!;
    const seg = new THREE.Mesh(new THREE.PlaneGeometry(0.1, a.distanceTo(b)).rotateX(-Math.PI / 2), flat(0xbff0ff, 0.95));
    seg.position.copy(a).lerp(b, 0.5);
    seg.rotation.y = Math.atan2(b.x - a.x, b.z - a.z);
    g.add(seg);
  }
  const dest = new THREE.Mesh(new THREE.RingGeometry(0.28, 0.38, 32).rotateX(-Math.PI / 2), flat(0xbff0ff, 0.95));
  dest.position.set(DEST.x + 0.5, 0.05, DEST.z + 0.5);
  g.add(dest);

  // Half-cover shield on the edge facing the barrier.
  const shield = new THREE.Mesh(new THREE.CircleGeometry(0.16, 3, Math.PI / 2), flat(0x7fd8ff, 0.95));
  shield.position.set(DEST.x + 0.93, 0.5, DEST.z + 0.5);
  shield.rotation.y = Math.PI / 2;
  g.add(shield);

  const sel = new THREE.Mesh(new THREE.RingGeometry(0.46, 0.56, 40).rotateX(-Math.PI / 2), flat(new THREE.Color(SOURCE.color).getHex(), 0.95));
  sel.position.set(SOURCE.x + 0.5, 0.05, SOURCE.z + 0.5);
  g.add(sel);
  g.renderOrder = 10;
  return g;
}
