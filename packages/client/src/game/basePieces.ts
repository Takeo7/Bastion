import * as THREE from 'three';
import { bastionFootprint, bastionLot, bastionStage, BASTION_STAGES, bastionYard, cellsOf, DIRS4, nodeOf, PIECES, type BasePiece, type BaseState, type GameState, type PieceCell, type PieceDef, type PieceId, type TileKind, type Vec2 } from '@bastion/engine';
import { STOREY } from './coords';
import { inwards, wearStage } from './siegeDressing';
import type { PropId } from './syntyManifest';

// The new base (docs/FASE12.md §11) on the map: its pieces, for its siege
// (mapView) and for the base's own screen (clearingArt, with the same MapView).
// buildings.ts and ground.ts see open, flat ground where a piece stands
// (terrainOf); this draws the piece, as worn as it is, or what is left of it.

/** A piece on the map: its tiles, its middle and the tile that draws it (the one under its middle). */
export interface PieceSpot {
  piece: BasePiece;
  def: PieceDef;
  cells: (PieceCell & Vec2)[];
  /** Its middle, in tiles (z is the engine's y). */
  x: number;
  z: number;
  anchor: number;
  /** Its turn about y, three's way (counter-clockwise from above), for its `rot` quarter turns clockwise. */
  yaw: number;
}

/** The map's pieces, and the one on each tile (an index into `spots`; -1, none). */
export interface PieceMap {
  spots: PieceSpot[];
  at: Int16Array;
  /** The Bastion's yard, by tile (engine base.ts bastionYard): low cover in a siege, drawn with the Bastion (drawBastion), nothing else there. */
  yard: Set<number>;
}

export function pieceMap(s: GameState): PieceMap | null {
  if (!s.base) return null;
  const w = s.width;
  const at = new Int16Array(w * s.height).fill(-1);
  const base = s.base as unknown as BaseState;
  const spots = s.base.pieces.map((piece, k): PieceSpot => {
    // The caravan stands for the Bastion: the building of its stage, on its footprint.
    const cells = piece.piece === 'caravan' && bastionStage(base) > 0 ? bastionFootprint(base).map((c) => ({ ...c, tile: 'wall' as TileKind }) as PieceCell & Vec2) : cellsOf(piece);
    for (const c of cells) at[c.y * w + c.x] = k;
    const xs = cells.map((c) => c.x);
    const ys = cells.map((c) => c.y);
    const x = (Math.min(...xs) + Math.max(...xs) + 1) / 2;
    const z = (Math.min(...ys) + Math.max(...ys) + 1) / 2;
    // A piece of a base saved before 20/10 keeps its old size (BasePiece.size) until it is levelled up or rebuilt.
    return { piece, def: piece.size ? { ...PIECES[piece.piece], size: piece.size } : PIECES[piece.piece], cells, x, z, anchor: Math.floor(z) * w + Math.floor(x), yaw: (-piece.rot * Math.PI) / 2 };
  });
  return { spots, at, yard: new Set(bastionYard(base).map((p) => p.y * w + p.x)) };
}

/** The pieces drawn here: all of them. */
export const DRAWN = new Set<PieceId>(Object.keys(PIECES) as PieceId[]);

/**
 * The map as buildings.ts and ground.ts are to see it: what basePieces draws
 * taken off (open, flat ground), and the base's tracks (YARD) as the wilds'
 * paths of packed earth, not a yard's tarmac. What it doesn't say is the
 * state's own (its prototype).
 */
export function terrainOf(s: GameState, map: PieceMap): GameState {
  const tiles = s.tiles.slice();
  const elev = s.elev.slice();
  for (const spot of map.spots) {
    if (!DRAWN.has(spot.piece.piece)) continue;
    for (const c of spot.cells) {
      const i = c.y * s.width + c.x;
      tiles[i] = 'floor';
      elev[i] = 0;
    }
  }
  for (const i of map.yard) tiles[i] = 'floor';
  const ground = s.ground?.map((g) => (g === 3 ? 1 : g));
  return Object.create(s, { tiles: { value: tiles }, elev: { value: elev }, ground: { value: ground } }) as GameState;
}

/** How worn a piece is: 0 whole, 1 knocked about, 2 about to go (siegeDressing's stages), 3 given way (its ruin). */
export type PieceWear = 0 | 1 | 2 | 3;

const HARD = new Set<TileKind>(['wall', 'door', 'doorOpen', 'low', 'high']);

/**
 * In a siege, by what its tiles still hold (GameState.breakables, against its
 * health: the dead strike a tile), given way once none stands; on the base's
 * screen, or for a piece the dead can't strike (a deck, a trap), by its health.
 */
export function pieceWear(s: GameState, spot: PieceSpot): PieceWear {
  if (spot.piece.hp <= 0) return 3;
  const hard = spot.cells.filter((c) => !c.elev && HARD.has(c.tile));
  if (!s.breakables || !hard.length) return wearStage(spot.piece.hp, spot.def.hp);
  let least = Infinity;
  let standing = 0;
  for (const c of hard) {
    if (HARD.has(s.tiles[c.y * s.width + c.x]!)) standing++;
    const left = s.breakables[nodeOf(s, { x: c.x, y: c.y, z: 0 })];
    if (left !== undefined) least = Math.min(least, left);
  }
  if (!standing) return 3;
  return wearStage(Math.min(least, spot.piece.hp), spot.def.hp);
}

/** What a piece looks like as drawn: a change in it (a level up, a stage of wear, a trap spent) redraws it. */
export function pieceKey(s: GameState, spot: PieceSpot): string {
  const p = spot.piece;
  const charges = s.traps ? (s.traps.find((t) => t.piece === p.id)?.charges ?? 0) : (p.charges ?? 0);
  // A gate hangs by the gates beside it (drawPiece): one put or taken away next to it redraws it too.
  const beside = p.piece === 'gate' ? DIRS4.map((d) => (s.base?.pieces.some((o) => o.piece === 'gate' && o.x === p.x + d.x && o.y === p.y + d.y) ? 1 : 0)).join('') : '';
  return `${p.piece}|${p.x},${p.y},${p.rot}|${p.level ?? 0}|${pieceWear(s, spot)}|${charges}${p.piece === 'caravan' ? `|${s.base?.level ?? 1}` : ''}${beside}`;
}

/** What draws a piece: a prop at a matrix, how tall a prop stands (its model's top), and the map's tiles. */
export interface PieceArt {
  put(id: PropId, m: THREE.Matrix4, shadows?: boolean): void;
  top(id: PropId): number;
  size(id: PropId): THREE.Vector3;
}

function hash(x: number, y: number, salt = 0): number {
  return ((((x + salt * 31) * 73856093) ^ (y * 19349663) ^ (salt * 83492791)) >>> 0) % 1000 / 1000;
}

/** A matrix at (x, y, z): turned `yaw` about y, then leant `roll` about its own z and `pitch` about its own x; scaled. */
function at(x: number, y: number, z: number, yaw: number, s: [number, number, number] = [1, 1, 1], roll = 0, pitch = 0): THREE.Matrix4 {
  return new THREE.Matrix4().compose(
    new THREE.Vector3(x, y, z),
    new THREE.Quaternion().setFromEuler(new THREE.Euler(pitch, yaw, roll, 'YXZ')),
    new THREE.Vector3(...s),
  );
}

/** Quarter turns (counter-clockwise from above) that make a prop modelled facing north face DIRS4[f]. */
const turnTo = (f: number) => (4 - f) % 4;

/**
 * The tower's ladder: its foot this far from its tile's middle towards the deck, its top over the deck's edge,
 * leant `lean` tiles towards the deck for each tile up (its tilt's tangent), `top` tall. The units climb it by
 * the same numbers (unitViews.ts rungsOf).
 */
export const TOWER_LADDER = { foot: 0.1, lean: Math.tan(0.16), top: STOREY + 0.35 };

/** How tall a palisade, a wall and a gate stand (a little lower than a storey's walls). */
const FENCE_HEIGHT = 1.9;
/** What a palisade, a wall or a gate joins up with: one another, the buildings, the tower. */
const JOINS = new Set<PieceId>(['palisade', 'wall', 'gate', 'tower']);

/**
 * Draws one piece (but its sandbags: mapView's, as a siege's barricade's) on
 * the ground: the caravan, what is built by hand, the tower and the lights.
 */
export function drawPiece(s: GameState, map: PieceMap, spot: PieceSpot, wear: PieceWear, art: PieceArt): void {
  const { piece } = spot;
  const w = s.width;
  const x = piece.x;
  const y = piece.y;
  const cx = spot.x;
  const cz = spot.z;
  const r = (salt: number) => hash(x, y, 200 + salt);
  /** The pieces this one joins with, round its tile (DIRS4 order). */
  const joined = DIRS4.map((d) => {
    const nx = x + d.x;
    const ny = y + d.y;
    if (nx < 0 || ny < 0 || nx >= w || ny >= s.height) return false;
    const k = map.at[ny * w + nx]!;
    if (k < 0) return false;
    const other = map.spots[k]!;
    return other.piece.hp > 0 && (JOINS.has(other.piece.piece) || other.def.kind === 'building');
  });
  const along = joined[1] || joined[3] ? 'x' : joined[0] || joined[2] ? 'z' : piece.rot % 2 ? 'x' : 'z';
  // Pushed in by the dead: the lean goes towards the middle of the map.
  const into = inwards(s, x, y);
  switch (piece.piece) {
    case 'caravan':
      drawBastion(s, spot, wear, art, r);
      break;
    case 'palisade': {
      // A half to each neighbour it joins (a lone one, along its line), leant in as it wears; fallen flat when it gives way.
      const sy = FENCE_HEIGHT / art.top('palisade');
      const ways = joined.some(Boolean) ? DIRS4.map((_, k) => k).filter((k) => joined[k]) : along === 'x' ? [1, 3] : [0, 2];
      for (const k of ways) {
        const yaw = (turnTo(k) * Math.PI) / 2;
        // Its own x is across the line: lean towards the middle of the map.
        const side = Math.sign(Math.cos(yaw) * into.x - Math.sin(yaw) * into.z) || 1;
        if (wear === 3) {
          if (r(k) < 0.6) art.put('palisade', at(cx + into.x * 0.25, 0.22, cz + into.z * 0.25, yaw, [1, 0.5, 1], -side * (Math.PI / 2 - 0.12)));
          continue;
        }
        const lean = [0, 0.07, 0.17][wear]! * (0.7 + r(k + 4) * 0.6);
        art.put('palisade', at(cx, 0, cz, yaw, [1, sy * [1, 0.94, 0.85][wear]!, 1], -side * lean));
      }
      if (wear) for (let k = 0; k < wear * 2 + (wear === 3 ? 2 : 0); k++) art.put('pebbles', at(cx + (r(10 + k) - 0.5) * 0.8, 0.01, cz + (r(20 + k) - 0.5) * 0.8, r(30 + k) * 6.3), false);
      break;
    }
    case 'wall': {
      // Military's T-walls, a full one along its line (two at a corner); knocked askew, then toppled.
      const sy = FENCE_HEIGHT / art.top('milBarrierTall');
      const lines = (joined[1] || joined[3]) && (joined[0] || joined[2]) ? ['x', 'z'] : [along];
      for (const line of lines) {
        const yaw = line === 'x' ? Math.PI / 2 : 0;
        const side = Math.sign(Math.cos(yaw) * into.x - Math.sin(yaw) * into.z) || 1;
        if (wear === 3) {
          art.put('milBarrierTall', at(cx + into.x * 0.3, 0.2, cz + into.z * 0.3, yaw, [1, sy, 1], -side * (Math.PI / 2 - 0.05)));
          continue;
        }
        art.put('milBarrierTall', at(cx, 0, cz, yaw + (r(1) - 0.5) * 0.05 * wear, [1, sy, 1], -side * [0, 0.035, 0.09][wear]!));
      }
      if (wear) art.put(r(2) < 0.5 ? 'rubble1' : 'rubble2', at(cx - into.x * 0.4, 0, cz - into.z * 0.4, r(3) * 6.3, [0.35 + wear * 0.1, 0.3, 0.35 + wear * 0.1]), wear === 3);
      break;
    }
    case 'gate': {
      // Planks by a palisade, the quarantine's sheet metal by a wall; swung open on its hinge, ajar as it wears, down once it gives way.
      const metal = DIRS4.some((d) => {
        const k = map.at[(y + d.y) * w + x + d.x] ?? -1;
        return k >= 0 && map.spots[k]!.piece.piece === 'wall';
      });
      const id: PropId = metal ? 'gateMetal' : 'gateWood';
      const sy = FENCE_HEIGHT / art.top(id);
      const run = along === 'x' ? { x: 1, z: 0 } : { x: 0, z: 1 };
      // Across its line, into the base.
      const n = along === 'x' ? { x: 0, z: Math.sign(into.z) || 1 } : { x: Math.sign(into.x) || 1, z: 0 };
      if (wear === 3) {
        art.put(id, at(cx + n.x * 0.55, 0.05, cz + n.z * 0.55, Math.atan2(run.x, run.z) + (r(0) - 0.5) * 0.4, [1, sy, 1], Math.PI / 2));
        break;
      }
      // Hung on the end of its tile against what isn't another gate (19/10: in a row, never on the post between two
      // gates): a lone one or the row's first on its −run end, the row's last on its +run end; one between two slides,
      // shut in its place, open run along outside the next (which swings in).
      const gateAt = (k: number) => {
        const nx = x + run.x * k;
        const ny = y + run.z * k;
        const j = nx < 0 || ny < 0 || nx >= w || ny >= s.height ? -1 : map.at[ny * w + nx]!;
        return j >= 0 && map.spots[j]!.piece.piece === 'gate';
      };
      const open = s.tiles[y * w + x] === 'doorOpen';
      if (gateAt(-1) && gateAt(1)) {
        const slide = open ? 0.9 : 0;
        art.put(id, at(cx + run.x * slide - n.x * slide * 0.16, 0, cz + run.z * slide - n.z * slide * 0.16, Math.atan2(run.x, run.z), [1, sy, 1], [0, 0.04, 0.09][wear]!));
        break;
      }
      const end = gateAt(-1) ? 1 : -1;
      // From the hinge at that end of the tile, the leaf swung in: open, or ajar as it wears.
      const swing = open ? Math.PI / 2 - 0.15 : [0, 0.1, 0.28][wear]!;
      const v = { x: -end * run.x * Math.cos(swing) + n.x * Math.sin(swing), z: -end * run.z * Math.cos(swing) + n.z * Math.sin(swing) };
      const hinge = { x: cx + end * run.x * 0.5, z: cz + end * run.z * 0.5 };
      art.put(id, at(hinge.x + v.x * 0.5, 0, hinge.z + v.z * 0.5, Math.atan2(v.x, v.z), [1, sy, 1], [0, 0.04, 0.09][wear]!));
      break;
    }
    case 'logs': {
      const sy = [1, 0.85, 0.65][Math.min(wear, 2)]!;
      if (wear === 3) {
        for (let k = 0; k < 3; k++) art.put('log', at(cx + (r(k) - 0.5) * 0.7, 0.02, cz + (r(k + 3) - 0.5) * 0.7, r(k + 6) * 6.3, [0.5, 0.5, 0.5]));
        break;
      }
      art.put('woodStack', at(cx, 0, cz, spot.yaw + (r(0) - 0.5) * 0.1, [1, sy, 1]));
      break;
    }
    case 'tower': {
      // Military's guard tower without its hut and stairs (the siege's own), its deck the 2 × 2 a storey up; its open side
      // to the ladder in front, leaning on the deck's edge.
      const deck = spot.cells.filter((c) => c.elev);
      const ladder = spot.cells.find((c) => c.tile === 'ladder');
      const dx = deck.reduce((a, c) => a + c.x + 0.5, 0) / deck.length;
      const dz = deck.reduce((a, c) => a + c.y + 0.5, 0) / deck.length;
      const front = ladder ? { x: Math.sign(ladder.x + 0.5 - dx) * (Math.abs(ladder.x + 0.5 - dx) > 0.75 ? 1 : 0), z: Math.sign(ladder.y + 0.5 - dz) * (Math.abs(ladder.y + 0.5 - dz) > 0.75 ? 1 : 0) } : { x: 0, z: 1 };
      const yaw = Math.atan2(-front.x, -front.z);
      if (wear === 3) {
        art.put('rubble2', at(dx, 0, dz, r(0) * 6.3, [0.8, 0.5, 0.8]));
        break;
      }
      art.put('guardTower', at(dx, 0, dz, yaw, [2 / 3, 1, 2 / 3], [0, 0.01, 0.025][wear]!));
      if (ladder) {
        // Its rungs across the way up (the model's x is the ladder's face), its top leant onto the deck's edge.
        const face = Math.atan2(front.z, -front.x);
        const { foot, lean, top } = TOWER_LADDER;
        art.put('towerLadder', at(ladder.x + 0.5 - front.x * foot, 0, ladder.y + 0.5 - front.z * foot, face, [1, top / art.top('towerLadder'), 1], -Math.atan(lean)));
      }
      break;
    }
    case 'floodlight': {
      const tilt = [0, 0.05, 0.12][Math.min(wear, 2)]!;
      if (wear === 3) {
        art.put('floodlight', at(cx, 0.12, cz, spot.yaw, [0.7, 0.7, 0.7], Math.PI / 2 - 0.06));
        break;
      }
      art.put('floodlight', at(cx, 0, cz, spot.yaw, [0.7, 0.7, 0.7], tilt * (r(0) < 0.5 ? -1 : 1)));
      break;
    }
    case 'wire': {
      art.put('wireCoil', at(cx, 0, cz, spot.yaw + (r(0) - 0.5) * 0.4, [1, [1, 0.85, 0.65, 0.3][wear]!, 1]));
      break;
    }
    case 'turret': {
      // In a siege it is a unit (unitViews' TurretRig); on the base's screen, the same gun standing, turned as built.
      if (s.units.some((u) => u.automaton?.piece === piece.id)) break;
      if (wear === 3) {
        art.put('turretTripod', at(cx, 0.08, cz, spot.yaw, [0.78, 0.78, 0.78], 1.25));
        break;
      }
      // Its parts on one another as TurretRig puts them (syntyFigures.ts), its barrels (the model's +x) the way it was built facing.
      const k = 0.78;
      const yaw = spot.yaw;
      art.put('turretTripod', at(cx, 0, cz, yaw, [k, k, k]));
      art.put('turretSwivel', at(cx, (1.0 / 1.5) * k, cz, yaw, [k, k, k]));
      art.put('turretGun', at(cx, (1.32 / 1.5) * k, cz, yaw, [k, k, k]));
      art.put('turretDrum', at(cx + Math.cos(yaw) * (0.1 / 1.5) * k, (1.62 / 1.5) * k, cz - Math.sin(yaw) * (0.1 / 1.5) * k, yaw, [k, k, k]));
      break;
    }
    case 'mine':
    case 'spikes':
    case 'snare': {
      // Set, or spent (its uses gone, in the siege's GameState.traps or on the base's own piece).
      const charges = s.traps ? (s.traps.find((t) => t.piece === piece.id)?.charges ?? 0) : (piece.charges ?? 0);
      const spent = charges <= 0 || wear === 3;
      if (piece.piece === 'mine') {
        if (spent) art.put('crater', at(cx, -0.02, cz, r(0) * 6.3, [0.22, 0.5, 0.22]), false);
        else art.put('mine', at(cx, -0.03, cz, r(0) * 6.3, [1.6, 1.6, 1.6]), false);
      } else if (piece.piece === 'spikes') {
        // The pit, its stakes round it pointing up and out; broken and splashed once someone has fallen in.
        art.put('crater', at(cx, -0.06, cz, r(0) * 6.3, [0.2, 0.3, 0.2]), false);
        for (let k = 0; k < 6; k++) {
          const a = (k / 6) * Math.PI * 2 + r(1) * 1.2;
          const d = 0.12 + r(10 + k) * 0.16;
          const h = spent ? 0.18 + r(20 + k) * 0.12 : 0.32 + r(20 + k) * 0.12;
          // Turned so its own z points out of the pit: tipped about its x, its top goes out.
          art.put('spikeStake', at(cx + Math.cos(a) * d, -0.05, cz + Math.sin(a) * d, Math.PI / 2 - a, [1.4, h, 1.4], 0, (spent ? 0.6 : 0.3) + r(30 + k) * 0.25));
        }
      } else if (spent) {
        // Sprung: the two jaws shut on their hinge.
        for (const [id, side] of [['bearJaw', 1], ['bearJaw2', -1]] as const) art.put(id, at(cx, 0.02, cz, spot.yaw, [1, 1, 1], side * 1.35));
      } else art.put('bearTrap', at(cx, 0, cz, spot.yaw + (r(0) - 0.5) * 0.6), false);
      break;
    }
    default: {
      const build = BUILDINGS[piece.piece];
      if (!build) break;
      const [w, h] = spot.def.size;
      const lot = new Lot(art, spot, w, h);
      if (wear === 3) ruin(lot, r);
      else {
        build(lot, piece.level ?? 1);
        if (wear) debris(lot, wear, r);
      }
      break;
    }
  }
}

/**
 * The Bastion on its lot (playtest 18/10, #40; base.ts bastionLot): the
 * building of its stage (two levels each) on its footprint, its front to the
 * lot's yard (south), the second level of a stage with more on it; worn as the
 * siege leaves it. The caravan; the mobile home on its skirt (its porch, its
 * dish); Military's plywood barracón on stilts (solar panels, sandbags, a
 * floodlight); the house with its porch and chimney (sandbags on the porch, a
 * mast). Round it, its yard: a fire with chairs by it, wood.
 */
function drawBastion(s: GameState, spot: PieceSpot, wear: PieceWear, art: PieceArt, r: (salt: number) => number): void {
  const base = s.base as unknown as BaseState;
  const level = s.base?.level ?? 1;
  const stage = bastionStage({ level });
  const more = level % 2 === 0;
  const plot = bastionLot(base);
  const [w, h] = BASTION_STAGES[stage]!;
  // The footprint's frame (u across, v deep, its front +v) and the lot's.
  const here = new Lot(art, { ...spot, yaw: stage ? 0 : spot.yaw }, w, h);
  const yard = new Lot(art, { ...spot, x: plot ? plot.x + plot.w / 2 : spot.x, z: plot ? plot.y + plot.h / 2 : spot.z, yaw: 0 }, plot?.w ?? w, plot?.h ?? h);
  const lean = (k: number) => (r(k) < 0.5 ? -1 : 1) * [0, 0.012, 0.03, 0.06][wear]!;
  const sunk = wear === 3 ? -0.15 : 0;
  /** The middle of tile (x, y) of the lot (x across, y deep, its front the last row), in the yard's frame. */
  const T = (x: number, y: number) => ({ u: x + 0.5 - (plot?.w ?? w) / 2, v: y + 0.5 - (plot?.h ?? h) / 2 });
  switch (stage) {
    case 0: {
      const tilt = [0, 0.012, 0.03, 0][wear]!;
      art.put('caravan', at(spot.x, wear === 3 ? -0.18 : 0, spot.z, spot.yaw, [1, 1, 1], tilt * (r(0) < 0.5 ? -1 : 1), wear === 3 ? 0.06 : 0));
      if (more && wear < 3) {
        // Settled in: its steps, an aerial on the roof and the generator; by its end, its awning (playtest 20/10 #13:
        // «Caravana con toldo»), a tarp on poles over tiles 0–2 × 5–7 of the lot, a table and two chairs under it on
        // 0–1 × 6–7 (base.ts BASTION_YARD), the generator on 7, 3.
        here.put('trailerSteps', 0.6, h / 2 + 0.25, 0, 0.8);
        here.put('bAerial', -1.1, 0, 0, 0.45, here.top('caravan') - 0.05);
        const awning = T(1, 6);
        for (const id of ['bAwning', 'bAwningTarp'] as const) yard.put(id, awning.u, awning.v, 1, AWNING);
        const table = T(0.5, 6);
        yard.fill('bLongTable', table.u, table.v, 1.8, 0.8);
        for (const x of [0, 1]) {
          const chair = T(x, 7);
          yard.put('bCampChair', chair.u, chair.v - 0.1, 2 + (r(50 + x) - 0.5) * 0.3, 0.95);
        }
        const gen = T(7, 3);
        yard.put('bPowerGenerator', gen.u, gen.v, 1, 0.75);
      }
      break;
    }
    case 1: {
      // On its skirt, the wheels hidden: it isn't going anywhere. Worn, its broken twin.
      const k = 0.94;
      const body = wear >= 2 ? 'mobileHomeWreck' : 'mobileHome';
      for (const id of [body, wear >= 2 ? 'mobileHomeWreckGlass' : 'mobileHomeGlass', 'mobileHomeSkirt'] as const) {
        if (id === 'mobileHomeSkirt' && wear === 3) continue;
        const m = here.matrix(-0.03 * k, -0.12 * k, 0, k, sunk);
        if (wear) m.multiply(new THREE.Matrix4().makeRotationZ(lean(1)));
        art.put(id, m);
      }
      if (more && wear < 3) {
        here.put('trailerDeck', 0.9, 1.36 * k + 0.56 * k, 0, k);
        here.put('bDish', 2.2, -0.5, 1, 0.7, 2.61 * k);
        here.put('trailerSteps', -1.9, 1.4 * k + 0.2, 0, 0.8);
      }
      break;
    }
    case 2: {
      // Plywood on stilts, its steps to the front; a ruin once it gives way.
      const k = 0.93;
      const m = here.matrix(0, 1.475 * k, 0, k, sunk);
      if (wear && wear < 3) m.multiply(new THREE.Matrix4().makeRotationZ(lean(2)));
      art.put(wear === 3 ? 'plywoodShedRuin' : 'plywoodShed', m);
      if (more && wear < 3) {
        // Grown (playtest 20/10 #13: «Albergue ampliado»): sandbags either side of its steps (3 and 5, 7); up the
        // lot's free column its water tower (8, 0–1), taller than it, a floodlight on top, a solar panel and the
        // generator (8, 2–3).
        for (const [x, y, id, k] of [[8, 2, 'bSolarPanel', 0.6], [8, 3, 'bPowerGenerator', 0.7]] as const) {
          const t = T(x, y);
          yard.put(id, t.u, t.v, 1, k);
        }
        for (const x of [3, 5]) {
          const t = T(x, 7);
          yard.put('sandbags', t.u, t.v, 0, [0.95, 1, 1]);
        }
        const tower = T(8, 0.5);
        yard.put('bWaterTower', tower.u, tower.v, 1, WATER_TOWER);
        yard.put('bFloodlights', tower.u, tower.v + 0.3, 2.5, 0.4, yard.top('bWaterTower') * WATER_TOWER);
      }
      break;
    }
    default: {
      // The house, its porch to the yard; given way, a heap of what it was.
      const k = 0.977;
      if (wear === 3) {
        here.put('ruinPile', 0, 0, 0, 0.82);
        break;
      }
      const house = yard.matrix(0.088 * k, -3 + 2.12 * k, -1, k, sunk);
      if (wear) house.multiply(new THREE.Matrix4().makeRotationX(lean(3) * 0.5));
      for (const id of ['bastionHouse', 'bastionHouseGlass'] as const) art.put(id, house);
      if (more) {
        // Fortified (playtest 20/10 #13: «Casa fortificada»): every window shut, iron sheets and planks over it; a
        // wall of sandbags along the porch, its steps left open, and another across the lot's front row either side
        // of the way in (1–2 and 6–7, 8); tank traps in the back corners (0 and 8, 0); the mast; a floodlight in the
        // front corner (8, 8).
        for (const [i, w] of HOUSE_WINDOWS.entries()) armour(art, house, w, i, r);
        for (const u of [-2.4, -1.35, 1.35, 2.4]) yard.put('sandbags', u, 2.75, 0, [1.05, 0.9, 1]);
        for (const x of [1, 2, 6, 7]) {
          const bags = T(x, 8);
          yard.put('sandbags', bags.u, bags.v, 0, [1.02, 0.95, 1]);
        }
        here.put('bMast', 1.6, -0.8, 0, 0.6, 3.6);
        for (const x of [0, 8]) {
          const trap = T(x, 0);
          yard.put('bTankTrap', trap.u, trap.v, r(70 + x) * 4, 1.1);
        }
        const light = T(8, 8);
        yard.put('bFloodlights', light.u, light.v, 0.5, 0.6);
      }
    }
  }
  if (wear === 1 || wear === 2) debris(here, wear, r);
  if (wear === 3 && stage) ruin(here, r);
  // The yard, by tile of the lot (base.ts BASTION_YARD): a fire with a chair either side of it, turned to it, where
  // the building leaves room (before the caravan, along the mobile home's front, in the barracón's front corner);
  // the wood in the front corner opposite.
  const camp = [[5, 7, 4, 6], [5, 8, 4, 6], [7, 8, 6, 8]][stage];
  if (camp) {
    const [fx, fy, a, b] = camp as [number, number, number, number];
    const fire = T(fx, fy);
    yard.put('bFirePit', fire.u, fire.v, 0, 0.75);
    for (const x of [a, b]) {
      const chair = T(x, fy);
      // (Its front is its own +z: a quarter turn per π/2 from it.)
      yard.put('bCampChair', chair.u, chair.v, Math.atan2(fire.u - chair.u, fire.v - chair.v) / (Math.PI / 2) + (r(40 + x) - 0.5) * 0.3, 0.95);
    }
  }
  const wood = T(0, 8);
  yard.put('bWoodPile', wood.u, wood.v, 1, 0.8);
}

/** The caravan's awning's scale and the barracón's water tower's (drawBastion). */
const AWNING = 1.25;
const WATER_TOWER = 0.66;

/**
 * The house's windows (SM_Bld_House_03, its own frame: bastionHouseGlass's panes, and those broken out of it): the
 * middle of each, which way it looks (x or z, ±1), as wide and as tall (m).
 */
const HOUSE_WINDOWS: { at: [number, number, number]; axis: 'x' | 'z'; out: 1 | -1 }[] = [
  { at: [-1.71, 1.45, 1.66], axis: 'x', out: -1 },
  { at: [-1.71, 1.45, -1.67], axis: 'x', out: -1 },
  { at: [-1.71, 1.45, 0], axis: 'x', out: -1 },
  { at: [1.72, 1.45, -1.67], axis: 'x', out: 1 },
  { at: [0.83, 1.45, 2.55], axis: 'z', out: 1 },
  { at: [-0.83, 1.45, 2.55], axis: 'z', out: 1 },
  { at: [-0.83, 1.45, -2.55], axis: 'z', out: -1 },
  { at: [0.84, 1.45, -2.55], axis: 'z', out: -1 },
];
const WINDOW_WIDE = 0.62;
const WINDOW_TALL = 1;

/**
 * A window of the house shut against the dead, on `house` (its matrix): every other one an iron sheet over it, the
 * rest three planks across it, each a little askew.
 */
function armour(art: PieceArt, house: THREE.Matrix4, w: (typeof HOUSE_WINDOWS)[number], i: number, r: (salt: number) => number): void {
  const out = w.axis === 'x' ? new THREE.Vector3(w.out, 0, 0) : new THREE.Vector3(0, 0, w.out);
  const up = new THREE.Vector3(0, 1, 0);
  const along = new THREE.Vector3().crossVectors(up, out);
  const at = new THREE.Vector3(...w.at).addScaledVector(out, 0.12);
  if (i % 2 === 0) {
    // The sheet lies flat as it comes, its length across x: stood up, its face out.
    const size = art.size('bIronSheet');
    const m = new THREE.Matrix4().makeBasis(up, out, new THREE.Vector3().crossVectors(up, out)).scale(new THREE.Vector3((WINDOW_TALL + 0.15) / size.x, 1, (WINDOW_WIDE + 0.12) / size.z));
    art.put('bIronSheet', house.clone().multiply(new THREE.Matrix4().makeTranslation(at.x, at.y, at.z)).multiply(m));
    return;
  }
  const size = art.size('bPlank');
  for (const [n, dy] of [-0.32, 0, 0.32].entries()) {
    const tilt = (r(60 + i * 3 + n) - 0.5) * 0.5;
    const axis = along.clone().applyAxisAngle(out, tilt);
    const m = new THREE.Matrix4().makeBasis(axis, out, new THREE.Vector3().crossVectors(axis, out)).scale(new THREE.Vector3((WINDOW_WIDE + 0.25) / size.x, 1, 1));
    const p = at.clone().addScaledVector(up, dy * WINDOW_TALL).addScaledVector(out, n * 0.012);
    art.put('bPlank', house.clone().multiply(new THREE.Matrix4().makeTranslation(p.x, p.y, p.z)).multiply(m));
  }
}

/**
 * A building's ground in its own frame, as it was built (before its turn):
 * u across its width and v into its depth, in tiles from its middle; its
 * front (+v) is south when it isn't turned.
 */
class Lot {
  /** The design's scale (scaled): a layout drawn for one size laid on a lot of another (a piece's size of before). */
  private su = 1;
  private sv = 1;

  constructor(
    private readonly art: PieceArt,
    private readonly spot: PieceSpot,
    readonly w: number,
    readonly h: number,
  ) {}

  /**
   * Laid out for a lot `w` × `h` (a piece's footprint, docs/MAPAS.md §4.9): on another (a piece kept at its size of
   * before, BasePiece.size), where things go and how big what fills a space is, in proportion; and the models no
   * bigger than they are.
   */
  scaled(w: number, h: number): this {
    this.su = this.w / w;
    this.sv = this.h / h;
    return this;
  }

  /** How much smaller than designed the models go (scaled). */
  private get shrink(): number {
    return Math.min(1, this.su, this.sv);
  }

  private world(u: number, v: number): { x: number; z: number } {
    const c = Math.cos(this.spot.yaw);
    const s = Math.sin(this.spot.yaw);
    [u, v] = [u * this.su, v * this.sv];
    return { x: this.spot.x + u * c + v * s, z: this.spot.z - u * s + v * c };
  }

  /** `id` at (u, v), turned `turn` quarter turns (counter-clockwise from above), scaled `k` (or per axis), `y` up. */
  put(id: PropId, u: number, v: number, turn = 0, k: number | [number, number, number] = 1, y = 0, shadows = true): void {
    this.art.put(id, this.matrix(u, v, turn, k, y), shadows);
  }

  /** The matrix `put` puts a model at. */
  matrix(u: number, v: number, turn = 0, k: number | [number, number, number] = 1, y = 0): THREE.Matrix4 {
    const p = this.world(u, v);
    // (Its height up the same: what stands on another model goes as high as that one's top is.)
    const s = this.shrink;
    return at(p.x, y * s, p.z, this.spot.yaw + (turn * Math.PI) / 2, typeof k === 'number' ? [k * s, k * s, k * s] : [k[0] * s, k[1] * s, k[2] * s]);
  }

  /**
   * `id` as big as it fits in a `w` × `d` space round (u, v), turned `turn`; but never lower than `tall` tiles (a
   * shelter one walks into stands over a soldier's head, 1.34: overhanging its lot, until its piece grows, MAPAS.md
   * §4.9); its scale.
   */
  fill(id: PropId, u: number, v: number, w: number, d: number, turn = 0, y = 0, tall = 0): number {
    const size = this.art.size(id);
    // (The space in the lot's own tiles; put shrinks the model again by as much as the lot is smaller. The scale it
    // returns is the design's, as put takes it.)
    const [fw, fd] = [(w * this.su) / this.shrink, (d * this.sv) / this.shrink];
    const k = Math.max(Math.min(fw / (turn % 2 ? size.z : size.x), fd / (turn % 2 ? size.x : size.z)), tall / size.y / this.shrink);
    this.put(id, u, v, turn, k, y);
    return k;
  }

  top(id: PropId): number {
    return this.art.top(id);
  }

  size(id: PropId): THREE.Vector3 {
    return this.art.size(id);
  }
}

/**
 * The buildings (docs/FASE12.md §11.3), the base's installations of before in
 * the space they take on the map: what they are at level 1, with more round
 * them, or a bigger one, at levels 2 and 3. The same models as the old base's
 * screen (strategy/bastion.ts FACILITY_ART). In a fight they are solid blocks:
 * what is drawn fills their ground.
 */
/** How tall a shelter one walks into stands at least (tiles: 2.6 m drawn, over a soldier's 2.01), and a lamp post's scale (4.4 m). */
const SHELTER = 1.75;
const LAMP = 0.9;

const BUILDINGS: Partial<Record<PieceId, (lot: Lot, level: number) => void>> = {
  // Sala de radio (4 × 4): a shelter of scrap under a tarp with the set on a table, an aerial and a generator; a mast,
  // then a dish.
  radio: (lot, level) => {
    lot.scaled(4, 4);
    const k = lot.fill('bShackFrame', -0.45, -0.45, 2.9, 2.9, 0, 0, SHELTER);
    lot.put('bShackTarp', -0.45, -0.45, 0, k, 0.8 * k);
    const t = lot.fill('bLongTable', -0.45, -0.4, 1.6, 0.8);
    lot.put('bRadioSet', -0.45, -0.45, 1, 1, lot.top('bLongTable') * t);
    lot.put('bPowerGenerator', 1.35, 1.35, 1, 0.85);
    lot.put(level >= 2 ? 'bMast' : 'bAerial', 1.45, -1.45, 0, level >= 2 ? 0.7 : 0.75);
    if (level >= 2) lot.put('bSolarPanel', -0.9, 1.5, 1, 0.75);
    if (level >= 3) {
      lot.put('bDish', 1.5, 0.2, 1, 0.75);
      lot.put('bPowerBoxes', -1.65, 1.5, 0, 0.7);
    }
  },
  // Barracón (6 × 5): a field tent, its fire and a chair by it, the wood; an army tent and the field tent beside it;
  // washing out and a second chair.
  quarters: (lot, level) => {
    lot.scaled(6, 5);
    if (level < 2) {
      lot.fill('bFieldTent', -1.0, -0.6, 3.6, 2.5);
      lot.put('bFirePit', 1.5, 1.3, 0, 0.8);
      lot.put('bCampChair', 0.7, 1.75, 1.3, 1);
      lot.put('bWoodPile', 2.2, -1.5, 1, 0.9);
      return;
    }
    lot.fill('bArmyTent', -1.0, -0.4, 3.8, 3.6);
    lot.fill('bFieldTent', 2.0, -0.6, 1.6, 3.0, 1);
    lot.put('bFirePit', 1.6, 1.75, 0, 0.6);
    lot.put('bWoodPile', -2.4, 1.85, 1, 0.8);
    if (level >= 3) {
      lot.put('bWashingLine', -0.9, 1.9, 0, 0.7);
      lot.put('bCampChair', 0.85, 1.9, 2.4, 1);
    }
  },
  // Enfermería (6 × 5): a field tent with a first-aid box and a drip; the quarantine tent; a decontamination shower
  // and a gurney.
  infirmary: (lot, level) => {
    lot.scaled(6, 5);
    if (level < 2) {
      lot.fill('bFieldTent', -0.9, -0.6, 3.6, 2.5);
      lot.put('bMedKit', 1.6, 1.3, 0, 0.8);
      lot.put('ivStand', 2.1, 0.3, 0, 0.85);
      lot.put('bJerrycan', 2.3, -1.5, 0.3, 1);
      lot.put('gurney', -0.9, 1.6, 1, 0.75);
      return;
    }
    lot.fill('bQuarantineTent', 0, -0.4, 5.2, 3.4);
    lot.put('bMedKit', -2.2, 1.8, 0, 0.8);
    lot.put('ivStand', 2.4, 1.6, 0, 0.85);
    if (level >= 3) {
      lot.put('bDeconShower', 1.4, 1.8, 0, 0.55);
      lot.put('gurney', -0.8, 1.9, 1, 0.75);
    }
  },
  // Taller (5 × 4): a lean-to over the bench, the tools and the tyres; a generator and the battery bench; floodlights.
  workshop: (lot, level) => {
    lot.scaled(5, 4);
    lot.fill('bLeanTo', 0, -0.4, 4.6, 3.0);
    lot.put('bWorkbench', -1.2, -0.6, 0, 0.85);
    lot.put('bToolbox', -0.3, 0.1, 0.3, 1);
    lot.put('bTyres', 1.9, 1.4, 0, 0.9);
    if (level >= 2) {
      lot.put('bPowerGenerator', 1.6, -0.6, 1, 0.8);
      lot.put('bBatteryTable', 0.3, 1.5, 0, 0.8);
    }
    if (level >= 3) {
      lot.put('bFloodlights', -2.0, 1.5, 0.4, 0.45);
      lot.put('bJerrycan', 1.1, 1.6, 1.2, 1);
    }
  },
  // Huerto (7 × 6, low): four raised beds and a rain barrel; greens in them; a scarecrow and more barrels.
  garden: (lot, level) => {
    lot.scaled(7, 6);
    for (const u of [-2.55, -0.85, 0.85, 2.55]) {
      const k = lot.fill('bGardenBed', u, -0.5, 1.3, 4.2);
      if (level >= 2) for (let i = 0; i < 5; i++) lot.put(i % 2 ? 'bGardenPlant2' : 'bGardenPlant', u + (i % 2 ? 0.18 : -0.18), -2.1 + i * 0.8, i, 0.55, lot.top('bGardenBed') * k);
    }
    lot.put('bGardenBarrel', 3.0, 2.4, 0, 1.1);
    if (level >= 3) {
      lot.put('bScarecrow', -3.0, 2.3, 0.5, 0.6);
      lot.put('bGardenBarrel', 2.35, 2.5, 1, 1.1);
    }
  },
  // Cuarentena (4 × 4): a cage of mesh panels (two a side), its door on the front; a guard's chair and cans; then a lamp.
  quarantine: (lot, level) => {
    lot.scaled(4, 4);
    const sx = 1.85 / lot.size('cagePanel').x;
    for (const u of [-0.92, 0.92]) {
      lot.put('cagePanel', u, -1.8, 0, [sx, 1, 1]);
      lot.put('cagePanel', u, 1.8, 0, [sx, 1, 1]);
      lot.put('cagePanel', -1.8, u, 1, [sx, 1, 1]);
      lot.put('cagePanel', 1.8, u, 1, [sx, 1, 1]);
    }
    lot.put('bCampChair', 0.4, 0.3, 0.6, 1);
    if (level >= 2) {
      lot.put('bJerrycan', -1.2, 1.2, 0.4, 1);
      lot.put('bJerrycan', -0.9, 1.3, 1.4, 1);
    }
    if (level >= 3) lot.put('bLampPost', 1.6, 1.55, 2, LAMP);
  },
  // Camposanto (4 × 4, low): rows of crosses and grave sticks; a bench and a lamp; stone crosses at the head.
  memorial: (lot, level) => {
    lot.scaled(4, 4);
    const rows = level >= 2 ? 3 : 2;
    for (let r = 0; r < rows; r++) {
      for (let i = 0; i < 4; i++) {
        const stone = level >= 3 && r === 0;
        lot.put(stone ? 'bStoneCross' : (i + r) % 3 ? 'bCross' : 'bGraveStick', -1.35 + i * 0.9, -1.4 + r * 1.0, 0, stone ? 1.8 : (i + r) % 3 ? 2.1 : 1.1);
      }
    }
    if (level >= 2) {
      lot.put('bBench', 0.8, 1.65, 2, 0.8);
      lot.put('bLampPost', -1.6, 1.6, 0, LAMP);
    }
  },
  // Campo de tiro (10 × 3, low): targets along the back, a firing line of sandbags; bottles on drums; floodlights.
  range: (lot, level) => {
    lot.scaled(10, 3);
    const n = level + 2;
    for (let i = 0; i < n; i++) lot.put(i % 2 ? 'bTargetBoard' : 'bTarget', -3.8 + (7.6 * i) / (n - 1), -0.9, 0, 0.9);
    for (const u of [-3.6, -1.2, 1.2, 3.6]) lot.put('bSandbagWall', u, 0.85, 1, 0.75);
    if (level >= 2) lot.put('bBottleRange', 4.5, -0.3, 1, 0.65);
    if (level >= 3) lot.put('bFloodlights', -4.6, 0.3, 0.5, 0.45);
  },
  // Centro de entrenamiento (5 × 5): a tyre wall, a beam and sandbags; a climbing frame; dummies (targets) to strike.
  training: (lot, level) => {
    lot.scaled(5, 5);
    lot.fill('bTyreWall', 0, -1.9, 4.6, 0.7);
    lot.put('bBalanceBeam', -1.0, 0.1, 0, 1.0);
    for (const v of [-0.6, 0.8]) lot.put('bSandbagWall', 1.8, v, 0, 0.7);
    if (level >= 2) lot.put('bClimbFrame', -1.5, 1.5, 0, 0.75);
    if (level >= 3) lot.put('bTarget', 0.7, 1.6, 2, 0.8);
  },
  // Sala de mapas (6 × 6): an open army tent with a long table and the blackboard; another table; the radio and a
  // generator.
  simulation: (lot, level) => {
    lot.scaled(6, 6);
    lot.fill('bOpenTent', 0, -0.3, 5.0, 5.0, 0, 0, SHELTER);
    lot.put('bLongTable', -0.8, -0.7, 1, 0.8);
    lot.put('bBlackboard', 1.3, -1.7, 0.3, 0.85);
    if (level >= 2) lot.put('bLongTable', 0.9, 0.9, 1, 0.8);
    if (level >= 3) {
      lot.put('bRadioSet', -0.8, -0.7, 0, 1, 0.47 * 0.8);
      lot.put('bPowerGenerator', 2.4, 2.4, 0.3, 0.65);
    }
  },
  // Chapistería (7 × 5): the garage shed turned along its length, tyres and drums; cans and the battery bench; a fuel pump.
  garage: (lot, level) => {
    lot.scaled(7, 5);
    lot.fill('bGarageShed', -1.0, -0.2, 4.6, 4.3, 1);
    lot.put('bTyres', 2.5, 1.6, 1, 0.9);
    lot.put('bOilDrum', 2.7, -1.6, 0, 1);
    lot.put('bOilDrum', 2.35, -1.8, 0, 1);
    if (level >= 2) {
      lot.put('bJerrycan', 2.6, -0.7, 0.6, 1);
      lot.put('bBatteryTable', 2.4, 0.4, 1, 0.8);
    }
    if (level >= 3) lot.put('bFuelPump', 1.6, 1.9, 0, 0.8);
  },
  // Taller de oficios (4 × 4): a craft shed, the bench, a plank on sawhorses and wood stacked; a trestle; the timber
  // rack and an anvil.
  echoes: (lot, level) => {
    lot.scaled(4, 4);
    lot.fill('bGardenShed', -0.7, -0.7, 2.4, 2.4);
    lot.put('bWorkbench', 1.15, -1.4, 0, 0.85);
    for (const u of [-0.7, 0.5]) lot.put('bSawhorse', u, 1.25, 1, 1);
    lot.put('bWoodPile', 1.6, 0.7, 1, 0.9);
    if (level >= 2) lot.put('bTrestle', -1.55, 1.3, 0, 0.9);
    if (level >= 3) {
      lot.put('bPlankRack', 0.3, 1.7, 0, 0.6);
      lot.put('bAnvil', 1.2, 0.3, 0, 1);
    }
  },
};

/** A building knocked about: grit and bits at its foot, more and a heap of junk as it is about to go. */
function debris(lot: Lot, wear: 1 | 2, r: (salt: number) => number): void {
  const n = wear * 3;
  for (let k = 0; k < n; k++) {
    const side = Math.floor(r(50 + k) * 4);
    const t = r(60 + k) - 0.5;
    const u = side % 2 ? (side === 1 ? 1 : -1) * (lot.w / 2 + 0.05) : t * lot.w;
    const v = side % 2 ? t * lot.h : (side === 2 ? 1 : -1) * (lot.h / 2 + 0.05);
    lot.put('pebbles', u, v, r(70 + k) * 4, 1, 0.01, false);
  }
  if (wear === 2) lot.put('bJunkHeap', lot.w / 2 - 0.4, lot.h / 2 - 0.3, r(80) * 4, 0.45);
}

/** What is left of a building: heaps of burnt wreckage and junk over its ground, and grit round them. */
function ruin(lot: Lot, r: (salt: number) => number): void {
  lot.fill('bBurntHeap', -lot.w / 4, 0, lot.w / 2, lot.h * 0.8, Math.floor(r(90) * 4));
  lot.fill('bJunkHeap', lot.w / 4, (r(91) - 0.5) * 0.4, lot.w / 2, lot.h * 0.7, Math.floor(r(92) * 4));
  for (let k = 0; k < 4; k++) lot.put('pebbles', (r(93 + k) - 0.5) * lot.w, (r(97 + k) - 0.5) * lot.h, r(101 + k) * 4, 1, 0.01, false);
}
