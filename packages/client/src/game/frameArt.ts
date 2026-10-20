import * as THREE from 'three';
import { DIRS4, type FramePiece, type GameState, type Vec2 } from '@bastion/engine';
import { STOREY } from './coords';
import { fenceOff, placed, shut, type Dressing } from './dressing';
import { bandOf, BEYOND, FAR_BANK, frameOf, SUNKEN, type Band } from './outline';
import type { PropId } from './syntyManifest';

// The outline round what is played (docs/MAPAS.md §5.10): the engine shuts a
// map's edge off with the pieces of GameState.frame (every tile of them a wall
// out of play, ground 4), and says what each piece is. Where a piece meets the
// ground that is played, its line: a lot's hoarding or its wall, a ruin's
// heaps, the hoarding round works, the closure across a street that goes on,
// a military cordon's T-walls. Inside it, and on past the map's edge, what
// fills it. The blocks shut up are buildings.ts' (BuildingPlan.frameBlocks):
// the town's own walls and roofs.

/** Stable pseudo-random value in [0, 1). */
function hash(x: number, y: number, salt: number): number {
  let h = Math.imul(x + 11, 374761393) + Math.imul(y + 5, 668265263) + Math.imul(salt + 17, 2147483647);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

/**
 * A piece of the outline's own geometry, in a flat colour (Synty's look): a
 * unit box or wedge (its base on y = 0, its slope rising to +z) placed by
 * `matrix`, or a unit square of a canal's water.
 */
export interface Shape {
  form: 'box' | 'wedge' | 'water';
  matrix: THREE.Matrix4;
  color: number;
}

/** The shapes' geometry, shared by every map: a box and a wedge with their bases on y = 0, a square of water. */
export const SHAPE_GEOMETRY: Record<Shape['form'], THREE.BufferGeometry> = {
  box: new THREE.BoxGeometry(1, 1, 1).translate(0, 0.5, 0),
  wedge: wedgeGeometry(),
  water: new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2),
};

/** A unit wedge: its base 1 × 1 on y = 0, rising from z = −½ to 1 at z = +½ (an embankment's slope). */
function wedgeGeometry(): THREE.BufferGeometry {
  const P = (x: number, y: number, z: number) => [x, y, z];
  const [a, b, c, d, e, f] = [P(-0.5, 0, -0.5), P(-0.5, 0, 0.5), P(-0.5, 1, 0.5), P(0.5, 0, -0.5), P(0.5, 0, 0.5), P(0.5, 1, 0.5)];
  // Each face its own vertices (flat): the slope, the back, the base and the two ends, wound outwards.
  const faces = [a, c, f, a, f, d, b, e, f, b, f, c, a, d, e, a, e, b, a, b, c, d, f, e];
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(faces.flat(), 3));
  geometry.computeVertexNormals();
  return geometry;
}

/** What the outline is drawn with: Synty's props and shapes of its own. */
export interface FrameArt {
  props: Dressing[];
  shapes: Shape[];
}

/** How far the band's geometry runs on along its side past the map's corners (tiles). */
const ALONG = 24;
/** How long each stretch of it is (tiles): what changes is merged again by chunks (batch.ts). */
const STRETCH = 4;

/** A block too thin to have an inside: walled off like a lot (buildings.ts lays the others). */
function thin(p: FramePiece): boolean {
  return p.kind === 'block' && (p.w < 3 || p.h < 3);
}

/**
 * What closes the map (docs/MAPAS.md §5.10): each piece's line where it meets
 * what is played, and what fills it, to the map's edge and past it. Always the
 * same for a map.
 */
export function dressFrame(s: GameState): FrameArt {
  const frame = s.frame ?? [];
  const shapes: Shape[] = [];
  if (!frame.length) return { props: [], shapes };
  const { width: w, height: h } = s;
  const at = frameOf(s);
  const played = (x: number, y: number) => x >= 0 && y >= 0 && x < w && y < h && at[y * w + x]! < 0;
  const out: Dressing[] = [];
  // Each run of a line with its length: the longest first (apart keeps them whole where two meet).
  const runs: { n: number; props: Dressing[] }[] = [];
  const band = bandOf(s);
  for (const p of frame) {
    const r = (salt: number) => hash(p.x * 7 + p.w, p.y * 7 + p.h, salt);
    // Its line: on each of its sides, the runs of its edge tiles with ground that is played across; `a` and `b` are
    // the played tiles at the run's ends, `inward` the way into the piece from them.
    for (let side = 0; side < 4; side++) {
      const d = DIRS4[side]!;
      const along = d.x ? { x: 0, y: 1 } : { x: 1, y: 0 };
      const n = d.x ? p.h : p.w;
      const first = { x: d.x > 0 ? p.x + p.w - 1 : p.x, y: d.y > 0 ? p.y + p.h - 1 : p.y };
      let start = -1;
      for (let k = 0; k <= n; k++) {
        const t = { x: first.x + along.x * k, y: first.y + along.y * k };
        if (k < n && played(t.x + d.x, t.y + d.y)) {
          if (start < 0) start = k;
          continue;
        }
        if (start >= 0) {
          const a = { x: first.x + along.x * start + d.x, y: first.y + along.y * start + d.y };
          const b = { x: first.x + along.x * (k - 1) + d.x, y: first.y + along.y * (k - 1) + d.y };
          // On round a corner of the outline (the next tile along is out of play), so that its line meets the next one.
          const ends: [number, number] = [played(a.x - along.x, a.y - along.y) ? 0 : 1, played(b.x + along.x, b.y + along.y) ? 0 : 1];
          runs.push({ n: k - start, props: line(s, p, a, b, { x: -d.x, y: -d.y }, (salt) => hash(a.x * 3 + side, a.y * 3 + side, salt), shapes, ends) });
        }
        start = -1;
      }
    }
    out.push(...fill(s, p, r, played, band));
  }
  out.unshift(...runs.sort((a, b) => b.n - a.n).flatMap((run) => run.props));
  if (band && (band.kind === 'water' || band.kind === 'ravine')) drawSunken(s, band, out, shapes);
  else if (band) drawBand(s, band, out, shapes);
  return { props: apart(out), shapes };
}

/** A piece's line from played tile `a` to `b` (along its side), `inward` into the piece: what keeps anybody out. */
function line(s: GameState, p: FramePiece, a: Vec2, b: Vec2, inward: Vec2, r: (salt: number) => number, shapes: Shape[], ends: [number, number] = [0, 0]): Dressing[] {
  const wired = { wired: s.biome === 'facility', pines: false };
  const wood = { wired: false, pines: pinewood(s) };
  switch (p.kind) {
    case 'forest':
      // The wood closing in, now and then trees felled across the way.
      return fenceOff(a, b, inward, r(4) < 0.18 ? 7 : 5, ends, r, wood);
    case 'rocks':
      // A rock face, boulders before it.
      return fenceOff(a, b, inward, 6, ends, r, wood);
    case 'containers':
      // (Not on round a corner: the next side's containers stand there, across these.)
      return fenceOff(a, b, inward, 8, [0, 0], r, wired);
    case 'ravine':
    case 'water':
      return brink(a, b, inward, p.kind === 'water', r);
    case 'river':
      // A canal's parapet, a railing on it: its water is seen over it.
      parapet(a, b, inward, shapes);
      return [];
    case 'highway':
      // Under the motorway, concrete T-walls.
      return fenceOff(a, b, inward, 3, ends, r, wired);
    case 'street':
      // The way shut where play ends: T-walls, a bus across it, or burnt-out cars and planks (dressing.ts shut).
      return shut(a, b, inward, Math.floor(r(1) * 3), r);
    case 'lot':
      // A hoarding of sheet metal, or a precast wall.
      return fenceOff(a, b, inward, r(2) < 0.55 ? 1 : 2, ends, r, wired);
    case 'works':
      return fenceOff(a, b, inward, 1, ends, r, wired);
    case 'ruin':
      // A block that came down: its heaps along the way.
      return fenceOff(a, b, inward, 0, ends, r, wired);
    case 'barrier':
      return fenceOff(a, b, inward, 3, ends, r, wired);
    case 'block':
      return thin(p) ? fenceOff(a, b, inward, 2, ends, r, wired) : [];
    default:
      // The railway: a precast wall at the foot of its embankment.
      return fenceOff(a, b, inward, 2, ends, r, wired);
  }
}

/** Whether the map's woods are pines (a facility's, the forest road's) or mixed. */
function pinewood(s: GameState): boolean {
  return s.biome === 'facility' || s.site === 'roadside';
}

/** The trees of a wood, pines or mixed. */
const PINES: PropId[] = ['pineLarge', 'pineLarge2', 'treePine'];
const MIXED: PropId[] = ['tree', 'pineLarge', 'treePine', 'pineLarge2', 'tree'];

/**
 * Along a ravine's rim or a river's bank, from played tile `a` to `b`, `inward` the drop's (or the water's) way:
 * low, so one sees over it to what is past: boulders and brush on a rim, rushes, brush and a stone on a bank.
 */
function brink(a: Vec2, b: Vec2, inward: Vec2, water: boolean, r: (salt: number) => number): Dressing[] {
  const list: Dressing[] = [];
  const along = { x: Math.sign(b.x - a.x) || Math.abs(inward.y), y: Math.sign(b.y - a.y) || Math.abs(inward.x) };
  const length = Math.abs(b.x - a.x) + Math.abs(b.y - a.y) + 1;
  for (let t = 0, i = 0; t < length; t += 0.9 + r(600 + i) * 0.6, i++) {
    const beyond = 0.75 + r(610 + i) * 0.5;
    const x = a.x + 0.5 + along.x * (t - 0.5) + inward.x * beyond;
    const z = a.y + 0.5 + along.y * (t - 0.5) + inward.y * beyond;
    const turn = r(620 + i) * Math.PI * 2;
    const k = r(630 + i);
    const id: PropId = water ? (k < 0.55 ? 'tuftWide' : k < 0.85 ? 'bush' : 'rock') : k < 0.45 ? 'boulder' : k < 0.75 ? 'rock' : 'bush';
    const size = id === 'tuftWide' ? 1.4 + r(640 + i) * 0.8 : id === 'boulder' ? 0.7 + r(640 + i) * 0.5 : 0.8 + r(640 + i) * 0.4;
    list.push(placed(id, x, 0, z, Math.cos(turn), Math.sin(turn), id !== 'tuftWide', 1, size));
  }
  return list;
}

const up = new THREE.Vector3(0, 1, 0);
const quat = new THREE.Quaternion();

/** A shape at (x, y, z), its own x along `yaw` (about y), sized (along, tall, across). */
function shape(form: Shape['form'], x: number, y: number, z: number, yaw: number, along: number, tall: number, across: number, color: number): Shape {
  quat.setFromAxisAngle(up, yaw);
  return { form, color, matrix: new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), quat, new THREE.Vector3(along, tall, across)) };
}

/** A canal's parapet from played tile `a` to `b`, `inward` the water's way: a low concrete wall and its railing. */
function parapet(a: Vec2, b: Vec2, inward: Vec2, shapes: Shape[]): void {
  const along = { x: Math.sign(b.x - a.x) || Math.abs(inward.y), y: Math.sign(b.y - a.y) || Math.abs(inward.x) };
  const length = Math.abs(b.x - a.x) + Math.abs(b.y - a.y) + 1;
  const yaw = Math.atan2(-along.y, along.x);
  const mid = { x: (a.x + b.x) / 2 + 0.5 + inward.x * 0.62, z: (a.y + b.y) / 2 + 0.5 + inward.y * 0.62 };
  shapes.push(shape('box', mid.x, 0, mid.z, yaw, length, 0.42, 0.24, 0xb6b0a4));
  shapes.push(shape('box', mid.x, 0.42, mid.z, yaw, length, 0.06, 0.3, 0xc8c2b6));
  shapes.push(shape('box', mid.x, 0.86, mid.z, yaw, length, 0.05, 0.06, 0x3d4246));
  for (let t = 0; t <= length; t++) {
    const x = a.x + 0.5 + inward.x * 0.62 + along.x * (t - 0.5);
    const z = a.y + 0.5 + inward.y * 0.62 + along.y * (t - 0.5);
    shapes.push(shape('box', x, 0.48, z, yaw, 0.05, 0.4, 0.05, 0x3d4246));
  }
}

/**
 * The river, the railway or the motorway along its side, as one straight band
 * `band.depth` deep (outline.ts), from far past one corner of the map to far
 * past the other: the canal's walls and water (its hole in the ground is
 * ground.ts'); the embankment with its tracks, a train of containers stopped
 * on one and the posts of the wires; the motorway's deck on its piers, its
 * barriers and lanes and what was left on it.
 */
function drawBand(s: GameState, band: Band, props: Dressing[], shapes: Shape[]): void {
  const { width: w, height: h } = s;
  const { side, depth: D } = band;
  const n = side % 2 ? h : w;
  // Its frame: u along the side, v in from the edge (past it, below 0); local x along, local z inwards.
  const yaw = [0, -Math.PI / 2, Math.PI, Math.PI / 2][side]!;
  const world = (u: number, v: number): [number, number] => (side === 0 ? [u, v] : side === 1 ? [w - v, u] : side === 2 ? [u, h - v] : [v, u]);
  const zero = world(0, 0);
  const [inX, inZ] = [world(0, 1)[0] - zero[0], world(0, 1)[1] - zero[1]];
  const [alX, alZ] = [world(1, 0)[0] - zero[0], world(1, 0)[1] - zero[1]];
  const put = (form: Shape['form'], u: number, v: number, y: number, along: number, tall: number, across: number, color: number, flip = false) => {
    const [x, z] = world(u, v);
    shapes.push(shape(form, x, y, z, yaw + (flip ? Math.PI : 0), along, tall, across, color));
  };
  const r = (k: number, salt: number) => hash(k, side * 31 + D, salt);
  for (let u0 = -ALONG; u0 < n + ALONG; u0 += STRETCH) {
    const u = u0 + STRETCH / 2;
    if (band.kind === 'river') {
      // The water down between the walls, from the near wall to the far bank's.
      put('water', u, (D - FAR_BANK) / 2, -1.15, STRETCH, 1, D + FAR_BANK, 0x3e524c);
      for (const [v, k] of [[D + 0.12, 1], [-FAR_BANK - 0.12, -1]] as const) {
        put('box', u, v, -1.4, STRETCH, 1.42, 0.24, 0x9f998d);
        put('box', u, v + k * 0.04, 0, STRETCH, 0.08, 0.36, 0xbcb6aa);
      }
    } else if (band.kind === 'rail') {
      // The embankment: slopes either side of a crest of ballast, the edge of the map on its far slope.
      const H = 0.6 * (s.frame?.find((p) => p.kind === 'rail')?.storeys ?? 1) + 0.25;
      const crest0 = -1;
      const crest1 = D - 1.8;
      put('wedge', u, D - 0.9, 0, STRETCH, H, 1.8, 0x6e7444, true);
      put('box', u, (crest0 + crest1) / 2, 0, STRETCH, H, crest1 - crest0, 0x77714f);
      put('wedge', u, crest0 - 0.9, 0, STRETCH, H, 1.8, 0x6e7444);
      put('box', u, (crest0 + crest1) / 2, H, STRETCH, 0.1, crest1 - crest0 - 0.3, 0x8b857a);
      const tracks = crest1 - crest0 >= 3.4 ? [(crest0 + crest1) / 2 - 0.95, (crest0 + crest1) / 2 + 0.95] : [(crest0 + crest1) / 2];
      for (const c of tracks) {
        for (let t = 0; t < STRETCH; t += 0.42) put('box', u0 + t + 0.21, c, H + 0.1, 0.17, 0.06, 1.5, 0x4c3c2e);
        for (const g of [-0.48, 0.48]) put('box', u, c + g, H + 0.16, STRETCH, 0.07, 0.06, 0x707070);
      }
      // The posts of the overhead wires, every eight tiles, an arm over the tracks.
      if (u0 % 8 === 0) {
        const pv = crest1 - 0.25;
        put('box', u0, pv, H + 0.1, 0.14, 2.7, 0.14, 0x6a6e70);
        put('box', u0, (tracks[0]! + pv) / 2, H + 2.6, 0.1, 0.1, pv - tracks[0]! + 0.6, 0x6a6e70);
      }
    } else {
      // The motorway's deck, two storeys up (or its own), its barriers and lanes.
      const Hd = (s.frame?.find((p) => p.kind === 'highway')?.storeys ?? 2) * STOREY;
      const v0 = -3;
      const v1 = D - 0.2;
      put('box', u, (v0 + v1) / 2, Hd - 0.5, STRETCH, 0.5, v1 - v0, 0x8c8a84);
      put('box', u, (v0 + v1) / 2, Hd, STRETCH, 0.02, v1 - v0 - 0.4, 0x4b4b49);
      for (const v of [v1 - 0.18, v0 + 0.18]) put('box', u, v, Hd, STRETCH, 0.48, 0.3, 0xaaa69c);
      for (let k = 1; k < Math.floor((v1 - v0 - 0.8) / 2.4); k++) put('box', u, v0 + 0.4 + k * 2.4, Hd + 0.021, 1.4, 0.005, 0.09, 0xd8d4c8);
      // Its piers every eight tiles: two columns and a beam across.
      if (u0 % 8 === 0) {
        for (const v of [v1 - 1.4, v0 + 1.6]) put('box', u0, v, 0, 0.75, Hd - 0.9, 0.75, 0x9a978f);
        put('box', u0, (v0 + v1) / 2, Hd - 1.0, 0.95, 0.5, v1 - v0 - 1.2, 0x9a978f);
      }
      // A wreck now and then, along its lanes.
      if (r(u0, 1) < 0.35) {
        const id: PropId = r(u0, 2) < 0.5 ? 'carRusted' : r(u0, 2) < 0.8 ? 'carWreck' : 'van';
        const [x, z] = world(u + (r(u0, 3) - 0.5) * 2, v0 + 1.2 + r(u0, 4) * (v1 - v0 - 2.4));
        const flip = r(u0, 5) < 0.5 ? 1 : -1;
        const skew = (r(u0, 6) - 0.5) * 0.5;
        props.push(placed(id, x, Hd, z, alX * flip + inX * skew, alZ * flip + inZ * skew, true));
      }
    }
  }
  if (band.kind === 'rail') {
    // A train of containers stopped on the far track, some of its flatcars empty.
    const H = 0.6 * (s.frame?.find((p) => p.kind === 'rail')?.storeys ?? 1) + 0.25;
    const crest0 = -1;
    const crest1 = D - 1.8;
    const c = crest1 - crest0 >= 3.4 ? (crest0 + crest1) / 2 - 0.95 : (crest0 + crest1) / 2;
    const cars = 6 + Math.floor(r(0, 7) * 6);
    const start = -ALONG / 2 + r(0, 8) * (n - cars * 5.4 + ALONG);
    const colours: PropId[] = ['container', 'containerBlue', 'containerOrange'];
    for (let k = 0; k < cars; k++) {
      const u = start + k * 5.4 + 2.6;
      put('box', u, c, H + 0.36, 5.1, 0.2, 1.55, 0x3b3d3f);
      for (const g of [-1.75, 1.75]) put('box', u + g, c, H + 0.16, 1.1, 0.24, 1.2, 0x26282a);
      if (r(k, 9) < 0.2) continue;
      const [x, z] = world(u, c);
      props.push(placed(colours[Math.floor(r(k, 10) * 3)]!, x, H + 0.56, z, inX, inZ, true));
    }
  }
}

/**
 * The country's ravine or water along its side (docs/MAPAS.md §5.10, phase 5), over the stretch it runs: the ground
 * open down to it (ground.ts) from `band.depth` in from the edge to FAR_BANK past it. A ravine's sheer walls of rock,
 * crags along their tops, its floor of stone and a stream down it; a river's earth banks sloping into its water. Its
 * ends walled the same, and past the far bank, the wood.
 */
function drawSunken(s: GameState, band: Band, props: Dressing[], shapes: Shape[]): void {
  const { width: w, height: h } = s;
  const { side, depth: D } = band;
  const ravine = band.kind === 'ravine';
  const yaw = [0, -Math.PI / 2, Math.PI, Math.PI / 2][side]!;
  const world = (u: number, v: number): [number, number] => (side === 0 ? [u, v] : side === 1 ? [w - v, u] : side === 2 ? [u, h - v] : [v, u]);
  const put = (form: Shape['form'], u: number, v: number, y: number, along: number, tall: number, across: number, color: number, flip = false, turn = 0) => {
    const [x, z] = world(u, v);
    shapes.push(shape(form, x, y, z, yaw + turn + (flip ? Math.PI : 0), along, tall, across, color));
  };
  const r = (k: number, salt: number) => hash(k, side * 31 + D, salt);
  const [near, far] = [D, -FAR_BANK];
  // Along the stretch it runs; on past a corner of the map where it reaches one (the ground stays open past it).
  const n = side % 2 ? h : w;
  const [ua, ub] = [band.from === 0 ? -ALONG : band.from, band.to === n - 1 ? n + ALONG : band.to + 1];
  const stream = (near + far) / 2 + (r(0, 1) - 0.5) * 1.4;
  const deep = ravine ? 5.2 : 0.45;
  const rock = 0x6f675c;
  const earth = 0x6b604c;
  for (let u0 = ua; u0 < ub; u0 += STRETCH) {
    const len = Math.min(STRETCH, ub - u0);
    const u = u0 + len / 2;
    if (ravine) {
      put('box', u, (near + far) / 2, -deep - 0.3, len, 0.3, near - far, 0x4f4a42);
      put('water', u, stream, -deep + 0.03, len, 1, 1.2, 0x34463f);
      for (const v of [near + 0.15, far - 0.15]) put('box', u, v, -deep, len, deep, 0.3, rock);
    } else {
      put('water', u, (near + far) / 2, -0.4, len, 1, near - far, 0x46605a);
      put('wedge', u, near - 0.45, -deep, len, deep, 0.9, earth);
      put('wedge', u, far + 0.45, -deep, len, deep, 0.9, earth, true);
    }
  }
  // Its ends, across it: walls of rock, or banks.
  for (const [u, k] of [[ua, 1], [ub, -1]] as const) {
    if (u < 0 || u > n) continue;
    if (ravine) put('box', u - k * 0.15, (near + far) / 2, -deep, 0.3, deep, near - far + 0.6, rock);
    // (The wedge turned a quarter rises along u: flipped at the start, where the land is before it.)
    else put('wedge', u + k * 0.45, (near + far) / 2, -deep, near - far, deep, 0.9, earth, k > 0, Math.PI / 2);
  }
  // A ravine's crags along the tops of its walls, sunk into them (their tops a little over the ground).
  if (ravine) {
    for (const [v, out, salt] of [[near + 0.2, -1, 0], [far - 0.2, 1, 7]] as const) {
      for (let u = ua + 0.6, i = 0; u < ub - 0.6; u += 2 + r(i, 40 + salt) * 0.8, i++) {
        const [x, z] = world(u, v);
        const [ox, oz] = [world(u, v + out)[0] - x, world(u, v + out)[1] - z];
        const a = (r(i, 41 + salt) - 0.5) * 0.6;
        const id: PropId = r(i, 42 + salt) < 0.5 ? 'cliff' : 'cliff2';
        props.push(placed(id, x, -1.5 - r(i, 43 + salt) * 0.4, z, ox * Math.cos(a) - oz * Math.sin(a), ox * Math.sin(a) + oz * Math.cos(a), true, 1, 2 + r(i, 44 + salt) * 0.6));
      }
    }
  }
  // The wood on the far bank (past the map's edge: no shadow on what is played).
  const trees = pinewood(s) ? PINES : MIXED;
  for (const [v, k] of [[far - 1.1, 0], [far - 2.5, 1], [far - 3.9, 2]] as const) {
    for (let u = ua + 0.4 + k * 0.5, i = 0; u < ub; u += 1.3 + r(i, 50 + k) * 0.5, i++) {
      const [x, z] = world(u, v + (r(i, 51 + k) - 0.5) * 0.6);
      const a = r(i, 52 + k) * Math.PI * 2;
      props.push(placed(trees[Math.floor(r(i, 53 + k) * trees.length)]!, x, 0, z, Math.cos(a), Math.sin(a), false, 1, 0.95 + r(i, 54 + k) * 0.35));
    }
  }
}

/**
 * What fills piece `p` away from its line (two tiles in from the played ground
 * or more), and on past the map's edge where it reaches it (BEYOND): a lot's
 * weeds, wrecks and rubbish; a ruin's heaps; a building site's stacks; what was
 * left on a street; a cordon's towers.
 */
function fill(s: GameState, p: FramePiece, r: (salt: number) => number, played: (x: number, y: number) => boolean, band: Band | null): Dressing[] {
  const out: Dressing[] = [];
  const { width: w, height: h } = s;
  // Its tiles, and the map's edge it touches carried on past it.
  const x0 = p.x === 0 ? -BEYOND : p.x;
  const y0 = p.y === 0 ? -BEYOND : p.y;
  const x1 = p.x + p.w === w ? w - 1 + BEYOND : p.x + p.w - 1;
  const y1 = p.y + p.h === h ? h - 1 + BEYOND : p.y + p.h - 1;
  /** How near played ground is (tiles, up to 3). */
  const near = (x: number, y: number) => {
    for (let k = 1; k <= 3; k++) for (let dy = -k; dy <= k; dy++) for (let dx = -k; dx <= k; dx++) if (Math.max(Math.abs(dx), Math.abs(dy)) === k && played(x + dx, y + dy)) return k;
    return 4;
  };
  const turn = (x: number, y: number, salt: number) => {
    const a = hash(x, y, salt) * Math.PI * 2;
    return [Math.cos(a), Math.sin(a)] as const;
  };
  const put = (id: PropId, x: number, y: number, salt: number, size = 1, cast = true) => {
    const [dx, dz] = turn(x, y, salt);
    out.push(placed(id, x + 0.5 + (hash(x, y, salt + 1) - 0.5) * 0.5, 0, y + 0.5 + (hash(x, y, salt + 2) - 0.5) * 0.5, dx, dz, cast, 1, size));
  };
  const along = p.w >= p.h;
  const trees = pinewood(s) ? PINES : MIXED;
  /** A tree of the wood on (x, y), a shadow from it only in its first row. */
  const tree = (x: number, y: number, gap: number, salt: number) => {
    const [dx, dz] = turn(x, y, salt);
    out.push(placed(trees[Math.floor(hash(x, y, salt + 1) * trees.length)]!, x + 0.5 + (hash(x, y, salt + 2) - 0.5) * 0.7, 0, y + 0.5 + (hash(x, y, salt + 3) - 0.5) * 0.7, dx, dz, gap <= 3, 1, 0.9 + hash(x, y, salt + 4) * 0.4));
  };
  /** Where (x, y) is from the band's side (`t` along it, `v` in from the edge), for its ravine's or water's pieces. */
  const bandAt = (x: number, y: number) => {
    const side = band!.side;
    return { t: side % 2 ? y : x, v: side === 0 ? y : side === 1 ? w - 1 - x : side === 2 ? h - 1 - y : x };
  };
  if (p.kind === 'containers') containerStacks(p, x0, y0, x1, y1, near, out);
  for (let y = y0; y <= y1; y++) {
    for (let x = x0; x <= x1; x++) {
      const gap = near(x, y);
      if (gap < 2) continue;
      const v = hash(x, y, 1);
      switch (p.kind) {
        case 'forest': {
          // Trees close together past the line's two rows, brush, a fallen trunk, a stone between them.
          if (gap < 3) break;
          if (v < 0.44) tree(x, y, gap, 60);
          else if (v < 0.52) put('bush', x, y, 61, 0.85 + hash(x, y, 62) * 0.4, false);
          else if (v < 0.535) put('log', x, y, 63, 1, false);
          else if (v < 0.55) put('rock', x, y, 64, 1, false);
          break;
        }
        case 'rocks': {
          // Boulders and crags, now and then a tree and brush among them (fewer than in a wood: they'd hide the rock).
          if (v < 0.15) put('boulder', x, y, 70, 0.9 + hash(x, y, 71) * 0.8, gap <= 3);
          else if (v < 0.25) put(hash(x, y, 72) < 0.5 ? 'cliff' : 'cliff2', x, y, 73, 1.1 + hash(x, y, 74) * 0.6, gap <= 3);
          else if (v < 0.35) put('rock', x, y, 75, 0.9 + hash(x, y, 76) * 0.5, false);
          else if (v < 0.39 && gap >= 3) tree(x, y, gap, 77);
          else if (v < 0.45) put('bush', x, y, 78, 0.8, false);
          break;
        }
        case 'ravine':
        case 'water': {
          const at = band && band.kind === p.kind ? bandAt(x, y) : null;
          const inStretch = !!at && at.t >= band!.from && at.t <= band!.to;
          // Down in it: nothing (drawSunken's); past its far bank, the wood; on its rim or its bank, what grows there.
          if (inStretch && at!.v < band!.depth && at!.v >= -FAR_BANK) break;
          if (inStretch && at!.v < -FAR_BANK) {
            if (v < 0.3) tree(x, y, 4, 80);
            break;
          }
          if (p.kind === 'ravine') {
            if (v < 0.1) put('boulder', x, y, 81, 0.7 + hash(x, y, 82) * 0.5);
            else if (v < 0.18) put('rock', x, y, 83, 0.9);
            else if (v < 0.28) put('bush', x, y, 84, 0.85, false);
            else if (v < 0.4) put('tuftLow', x, y, 85, 0.9, false);
          } else {
            if (v < 0.22) put('tuftWide', x, y, 86, 1.4 + hash(x, y, 87) * 0.6, false);
            else if (v < 0.32) put('bush', x, y, 88, 0.85, false);
            else if (v < 0.36) put('rock', x, y, 89, 0.9);
          }
          break;
        }
        case 'lot':
        case 'block': {
          if (p.kind === 'block' && !thin(p)) break;
          // Weeds over all of it; now and then rubbish, a heap of rubble, a dead tree, a wreck where there's room.
          if (v < 0.3) put(hash(x, y, 2) < 0.7 ? 'tuftLow' : 'tuft', x, y, 3, 0.8 + hash(x, y, 4) * 0.6, false);
          else if (v < 0.36) put(hash(x, y, 5) < 0.5 ? 'trashPile' : 'tirePile', x, y, 6);
          else if (v < 0.4) put(hash(x, y, 7) < 0.5 ? 'rubble1' : 'rubble2', x, y, 8);
          else if (v < 0.415 && gap >= 3) put('treeDead', x, y, 9, 0.8 + hash(x, y, 10) * 0.3);
          else if (v < 0.44 && gap >= 3) put(hash(x, y, 11) < 0.5 ? 'carWreck' : 'carRusted', x, y, 12);
          break;
        }
        case 'ruin': {
          // What a block that came down leaves: heaps of it, rubble between, weeds coming through.
          if ((x - x0) % 5 === 2 && (y - y0) % 5 === 2 && gap >= 3) put('ruinPile', x, y, 20, 0.9 + hash(x, y, 21) * 0.3);
          else if (v < 0.08) put(hash(x, y, 22) < 0.5 ? 'debrisPile' : 'debrisPile2', x, y, 23, 0.7);
          else if (v < 0.22) put(hash(x, y, 24) < 0.5 ? 'rubble1' : 'rubble2', x, y, 25);
          else if (v < 0.4) put('tuftLow', x, y, 26, 0.9, false);
          break;
        }
        case 'works': {
          // A building site: stacks of pallets and bricks, cement, pipes, a portaloo.
          if (v < 0.07) put('palletStack', x, y, 30);
          else if (v < 0.12) put('cementBags', x, y, 31);
          else if (v < 0.15) put('brickStack', x, y, 32);
          else if (v < 0.17) put('pipes', x, y, 33);
          else if (v < 0.18 && gap >= 3) put('portaloo', x, y, 34);
          else if (v < 0.3) put('tuftLow', x, y, 35, 0.8, false);
          break;
        }
        case 'street': {
          // What was left on it, along the way out: a wreck now and then, bags and tyres.
          const inside = x >= 0 && y >= 0 && x < w && y < h;
          if (v < (inside ? 0.05 : 0.04)) {
            const id: PropId = hash(x, y, 40) < 0.5 ? 'carRusted' : hash(x, y, 40) < 0.8 ? 'carWreck' : 'van';
            const flip = hash(x, y, 41) < 0.5 ? 1 : -1;
            const skew = (hash(x, y, 42) - 0.5) * 0.4;
            out.push(placed(id, x + 0.5, 0, y + 0.5, along ? flip : skew, along ? skew : flip, true));
          } else if (v < 0.1) put(hash(x, y, 43) < 0.5 ? 'trashPile' : 'tire', x, y, 44);
          break;
        }
        case 'barrier': {
          // A cordon in rows behind its T-walls: coils of wire along them, a line of sandbags, a tower every fourteen
          // tiles; past them, now and then a stack of crates or a floodlight.
          const k = along ? x : y;
          const [ax, az] = along ? [1, 0] : [0, 1];
          if (gap === 2) out.push(placed('razorWire', x + 0.5, 0, y + 0.5, ax, az, false));
          else if (gap === 3 && k % 14 === 7) put('guardTower', x, y, 50);
          else if (gap === 3 && k % 3 !== 0) out.push(placed('sandbags', x + 0.5, 0, y + 0.5, ax, az, true));
          else if (gap >= 4 && v < 0.025) put(hash(x, y, 53) < 0.6 ? 'milCrate' : 'floodlight', x, y, 54);
          break;
        }
        default:
          break;
      }
    }
  }
  return out;
}

/** A container's size (tiles, as syntyProps fits it: 4.85 long), and the height of one on another. */
const CONTAINER = { long: 4.85, wide: 1.98, high: 2.07 };
const CONTAINERS = new Set<PropId>(['container', 'containerBlue', 'containerOrange']);

/**
 * The containers apart: one that would stand through another put before it at its height (two sides' rows meeting
 * round a corner, a short side's hanging over its ends), or through the bus a street is shut with, is left out.
 */
function apart(props: Dressing[]): Dressing[] {
  type Box = { x0: number; x1: number; z0: number; z1: number; y: number };
  const e = new THREE.Vector3();
  /** Its bounds on the ground, `hx` and `hz` half its size along its own x and z, a little in from its sides. */
  const boxOf = (d: Dressing, hx: number, hz: number): Box => {
    const m = d.matrix.elements;
    const ex = Math.abs(m[0]!) * hx + Math.abs(m[8]!) * hz;
    const ez = Math.abs(m[2]!) * hx + Math.abs(m[10]!) * hz;
    e.setFromMatrixPosition(d.matrix);
    return { x0: e.x - ex + 0.15, x1: e.x + ex - 0.15, z0: e.z - ez + 0.15, z1: e.z + ez - 0.15, y: e.y };
  };
  const hits = (a: Box, b: Box) => Math.abs(a.y - b.y) < 1 && a.x0 < b.x1 && b.x0 < a.x1 && a.z0 < b.z1 && b.z0 < a.z1;
  // A wrecked bus is about 8 tiles long along its own x, 1.7 wide.
  const boxes = props.filter((d) => d.id === 'bus').map((d) => boxOf(d, 3.9, 0.85));
  return props.filter((d) => {
    if (!CONTAINERS.has(d.id)) return true;
    // Its own x across it, its z along it (syntyProps' fit).
    const box = boxOf(d, CONTAINER.wide / 2, CONTAINER.long / 2);
    if (boxes.some((b) => hits(b, box))) return false;
    boxes.push(box);
    return true;
  });
}

/**
 * A container yard's stacks (an estate's or a facility's outline), behind the line's containers end to end: in rows
 * along the piece's length, a lane of a little over half a tile between them, one to three high, a gap now and then.
 */
function containerStacks(p: FramePiece, x0: number, y0: number, x1: number, y1: number, near: (x: number, y: number) => number, out: Dressing[]): void {
  const colours: PropId[] = ['container', 'containerBlue', 'containerOrange'];
  const alongX = x1 - x0 >= y1 - y0;
  const [l0, l1, c0, c1] = alongX ? [x0, x1 + 1, y0, y1 + 1] : [y0, y1 + 1, x0, x1 + 1];
  const [stepL, stepC] = [CONTAINER.long + 0.35, CONTAINER.wide + 0.6];
  for (let c = c0 + stepC / 2; c + CONTAINER.wide / 2 <= c1; c += stepC) {
    for (let l = l0 + stepL / 2; l + CONTAINER.long / 2 <= l1; l += stepL) {
      const [x, z] = alongX ? [l, c] : [c, l];
      // Clear of what is played by three tiles all over it (the line's containers stand in the first two).
      const [hl, hw] = [CONTAINER.long / 2 - 0.1, CONTAINER.wide / 2 - 0.1];
      const corners = alongX ? [[x - hl, z - hw], [x + hl, z - hw], [x - hl, z + hw], [x + hl, z + hw]] : [[x - hw, z - hl], [x + hw, z - hl], [x - hw, z + hl], [x + hw, z + hl]];
      if (corners.some(([ex, ez]) => near(Math.floor(ex!), Math.floor(ez!)) < 3)) continue;
      const k = hash(Math.floor(x * 3), Math.floor(z * 3), 90 + p.w);
      if (k < 0.12) continue;
      const high = k < 0.5 ? 1 : k < 0.85 ? 2 : 3;
      for (let n = 0; n < high; n++) {
        const id = colours[Math.floor(hash(Math.floor(x * 3), Math.floor(z * 3) + n, 91) * 3)]!;
        const skew = (hash(Math.floor(x * 3) + n, Math.floor(z * 3), 92) - 0.5) * 0.08;
        // The model's length (its z) along the row: its +x across it.
        const [dx, dz] = alongX ? [skew, 1] : [1, skew];
        out.push(placed(id, x, n * CONTAINER.high, z, dx, dz, n === high - 1 && near(Math.floor(x), Math.floor(z)) <= 3));
      }
    }
  }
}
