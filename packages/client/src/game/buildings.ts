import * as THREE from 'three';
import type { SnowCover } from './snow';
import { decorOf, DIRS4, isWalkable, type DecorKind, type GameState, type Place, type TileKind, type Vec2 } from '@bastion/engine';
import { StaticBatch, type Dirty } from './batch';
import { groundAt, STOREY } from './coords';
import { DoorLeaves } from './doors';
import { extendShader, type FogOfWar } from './fogOfWar';
import { contactPatch, type GroundOcclusion } from './groundAO';
import { ATLAS_RECTS, atlasIndex, atlasRect, mapAtlasTexture, PACKED } from './mapAtlas';
import { NIGHT } from './night';
import { MedicalFinish } from './medicalFinish';
import { atlasOf, atlasTexture, loadModel } from './synty';
import { KIT_SCALE, PIECES, SYNTY_SCALE, type AtlasId, type PieceAsset, type PieceId, type PropId } from './syntyManifest';
import { syntyProp } from './syntyProps';
import { unitPosNow } from './unitViews';

// Buildings you can walk into, from Synty's modular kits (second playtest of
// the Synty look: «poder entrar en las casas»).
//
// The rules see a building as wall tiles round floor tiles. Here each wall
// tile is a thin Synty wall along its middle line: the middle half of a kit
// module, drawn 1.2 times bigger (syntyManifest: KIT_SCALE), so that a door
// or window module puts its opening right in the tile the rules say. Corners
// get a post, doors swing open, windows lose their glass. Inside there is a
// floor, on top a roof: gabled on houses and barns, flat with a parapet on
// shops, offices and warehouses. Floors and roofs are built here and painted
// with patches of the pack's atlas repeated in world space, so they run
// across tiles without a seam or a grid.
//
// Cutaway, as in XCOM: a building's roof fades out (dithered) while a soldier
// or a visible enemy is inside it (where they are on screen, so it opens as
// they walk in), and the walls facing the camera drop to waist height; the
// same for the cursor, the selected soldier and the target. A building whose
// inside the squad sees from out in the street (through a window or a door)
// loses its roof too, so what they see in there is on screen. A building
// between the camera and a unit only loses its roof; between the camera and
// the selected soldier, the cursor or the target, all its walls drop too.
// Close up, the roofs round the view fade. Each building fades on its own
// timer with a short hold, so passing the cursor over the streets never
// makes roofs flicker. Every player decides for their own screen; the
// soldiers of both count.

const WALLISH = new Set<TileKind>(['wall', 'door', 'doorOpen', 'window', 'windowBroken']);
/** BuildingPlan.before: what a tile was when the map was built. */
const WAS_WALL = 1;
const WAS_SLAB = 2;
export const isWallLike = (k: TileKind | undefined): boolean => k !== undefined && WALLISH.has(k);
const at = (s: GameState, x: number, y: number): TileKind | undefined => (x < 0 || y < 0 || x >= s.width || y >= s.height ? undefined : s.tiles[y * s.width + x]);

export type BuildingKind = 'house' | 'shop' | 'office' | 'hall' | 'barn' | 'ruin' | 'motel' | 'medical';

/**
 * The wall kit a building is built from: Town's houses (siding outside,
 * plaster inside), Apocalypse's concrete bunker walls (labs), the brick of
 * its warehouses (warehouses, hangars), the quarantine wall (a facility's
 * perimeter); sheet metal in three colours (sheds: #25).
 */
export type KitId = 'house' | 'bunker' | 'brick' | 'fence' | 'shedGrey' | 'shedRed' | 'shedBlue' | 'medical';

/** The sheds' kits, picked per building (twice as often grey). */
const SHEDS: KitId[] = ['shedGrey', 'shedGrey', 'shedRed', 'shedBlue'];
const isShed = (kit: KitId) => kit.startsWith('shed');

/** How a raised tile is drawn: a block of flats, a loading platform, a hill. */
export const RAISED_FLATS = 1;
export const RAISED_PLATFORM = 2;
export const RAISED_HILL = 3;
/** A siege's watchtower (docs/FASE12.md §10): a deck on legs, the guard tower's own (mapView); the ground shows under it. */
export const RAISED_TOWER = 4;

export interface Building {
  /** Index in the plan (the cutaway's texel). */
  index: number;
  /** Tile bounds, walls included. */
  x0: number;
  y0: number;
  x1: number;
  y1: number;
  kind: BuildingKind;
  kit: KitId;
  medicalWear?: number;
  /** Stable number in [0, 1) for its looks. */
  look: number;
  /** World height of its floor. */
  base: number;
  /** World height of the top of its roof. */
  top: number;
  /**
   * Storeys it stands (Synty's captures: shops and offices of two to four).
   * Without levels only the ground floor is played: the ones above are a
   * façade, and go with the roof in the cutaway.
   */
  storeys: number;
  /** The side its doors open onto (a motel's rooms), as a DIRS4 index; -1 none in particular. */
  front: number;
  /**
   * Its storeys are real (GameState.levels, docs/FASE6-PLAN.md): each is drawn
   * from its own tiles (floor, walls, furniture, stairs) instead of as a façade,
   * and the floors above the one in view fade in the cutaway.
   */
  leveled: boolean;
  /** Its top storey is a roof one can walk on (a flat roof with stairs or a ladder up): drawn as a deck, open where the stairs come up. */
  walkableRoof: boolean;
  /** A siege's Sala de radio, the heart's building: its mast on the roof (roofProps). */
  radio?: boolean;
}

/** Wall pieces stand on tile middles, half a tile each way: these are the tiles a building of bounds b spans. */
interface Span {
  x0: number;
  y0: number;
  x1: number;
  y1: number;
}

/** Stable pseudo-random value in [0, 1). */
function hash(x: number, y: number, salt = 0): number {
  let h = Math.imul(x + 1, 374761393) + Math.imul(y + 7, 668265263) + Math.imul(salt + 3, 2147483647);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

// ------------------------------------------------------------------ layout

/** Which tiles make which building: walls round a floor, at ground level. */
export class BuildingPlan {
  readonly buildings: Building[] = [];
  /** The building each tile belongs to (its walls and the floor inside), -1 none. */
  readonly owner: Int16Array;
  /** Floor inside a building (not its walls). */
  readonly inside: Uint8Array;
  /** How each raised tile is drawn (RAISED_FLATS, RAISED_PLATFORM, RAISED_HILL), 0 on the ground. */
  readonly raised: Uint8Array;
  /** Floor tiles the buildings draw their own floor on (not ruins: the ground shows there). */
  readonly floor: Uint8Array;
  /** Wall tiles in a solid mass two tiles thick or more (a reactor's core): drawn as a block, not as thin walls. */
  readonly massive: Uint8Array;
  /** The kit of walls that belong to no building (a perimeter, a lone stretch). */
  readonly looseKit: KitId;
  /** A base to defend (docs/FASE12.md §10): its wall is Quarantine's, its gates sheet metal, its corners watchtowers. */
  readonly siege: boolean;
  /**
   * What each tile was when the map was built, per storey (0 the ground):
   * WAS_WALL for a wall, a door or a window, WAS_SLAB for a storey's floor.
   * The rules only ever take these away (docs/FASE7.md §4, §5): a wall
   * that is low cover now was blown down, a slab that is gone was blown through.
   */
  private readonly before: Uint8Array[] = [];

  constructor(readonly s: GameState) {
    const { width: w, height: h } = s;
    const n = w * h;
    this.owner = new Int16Array(n).fill(-1);
    this.inside = new Uint8Array(n);
    this.raised = new Uint8Array(n);
    this.floor = new Uint8Array(n);
    this.massive = new Uint8Array(n);
    this.siege = !!s.mission.siege;
    this.looseKit = s.biome === 'facility' || this.siege ? 'fence' : 'brick';
    this.findRaised();
    const wallAt = (i: number) => this.wallIn(i);
    for (let y = 0; y + 1 < h; y++) {
      for (let x = 0; x + 1 < w; x++) {
        const square = [y * w + x, y * w + x + 1, (y + 1) * w + x, (y + 1) * w + x + 1];
        if (square.every(wallAt)) for (const i of square) this.massive[i] = 1;
      }
    }
    const area = new Uint8Array(n);
    if (s.ground) {
      // Procedural maps say what is indoors (ruins are open ground again).
      for (let i = 0; i < n; i++) if (s.ground[i] === 2 && (s.elev[i] ?? 0) === 0) area[i] = 1;
    } else {
      // Hand-made maps: what can't be reached from the edge without going through a wall is inside.
      const out = new Uint8Array(n);
      const queue: number[] = [];
      for (let i = 0; i < n; i++) {
        const x = i % w;
        const y = (i - x) / w;
        if ((x === 0 || y === 0 || x === w - 1 || y === h - 1) && !wallAt(i)) {
          out[i] = 1;
          queue.push(i);
        }
      }
      for (let k = 0; k < queue.length; k++) {
        const i = queue[k]!;
        const x = i % w;
        const y = (i - x) / w;
        for (const d of DIRS4) {
          const nx = x + d.x;
          const ny = y + d.y;
          if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
          const j = ny * w + nx;
          if (out[j] || wallAt(j)) continue;
          out[j] = 1;
          queue.push(j);
        }
      }
      for (let i = 0; i < n; i++) {
        if (out[i] || wallAt(i) || (s.elev[i] ?? 0) !== 0) continue;
        const x = i % w;
        const y = (i - x) / w;
        area[i] = 1;
        for (let dy = -1; dy <= 1; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            const nx = x + dx;
            const ny = y + dy;
            if (nx >= 0 && ny >= 0 && nx < w && ny < h && wallAt(ny * w + nx) && !this.massive[ny * w + nx]) area[ny * w + nx] = 1;
          }
        }
      }
    }
    // Each building is a connected piece of that area.
    for (let start = 0; start < n; start++) {
      if (!area[start] || this.owner[start] !== -1) continue;
      const index = this.buildings.length;
      const tiles = [start];
      this.owner[start] = index;
      let [x0, y0, x1, y1] = [w, h, -1, -1];
      let walls = 0;
      let gaps = 0;
      const kinds = new Set<DecorKind>();
      for (let k = 0; k < tiles.length; k++) {
        const i = tiles[k]!;
        const x = i % w;
        const y = (i - x) / w;
        [x0, y0, x1, y1] = [Math.min(x0, x), Math.min(y0, y), Math.max(x1, x), Math.max(y1, y)];
        if (wallAt(i)) walls++;
        const look = decorOf(s.decor?.[i]);
        if (look) kinds.add(look.kind);
        for (const d of DIRS4) {
          const nx = x + d.x;
          const ny = y + d.y;
          if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
          const j = ny * w + nx;
          if (!area[j] || this.owner[j] !== -1) continue;
          this.owner[j] = index;
          tiles.push(j);
        }
      }
      for (const i of tiles) {
        const x = i % w;
        const y = (i - x) / w;
        if (!wallAt(i)) this.inside[i] = 1;
        // A gap in the outline: a loading bay, a doorway with no door.
        if (!wallAt(i) && (x === x0 || x === x1 || y === y0 || y === y1)) gaps++;
      }
      if (!walls || tiles.length < 9) {
        for (const i of tiles) {
          this.owner[i] = -1;
          this.inside[i] = 0;
        }
        continue;
      }
      const base = (s.elev[start] ?? 0) * STOREY;
      // A row of shops wall to wall (a high street): one building each, split at the walls they share.
      const parts = this.partyWalls({ x0, y0, x1, y1 });
      parts.forEach((r, k) => {
        const own = k === 0 ? index : this.buildings.length;
        if (k > 0) {
          // The shared wall stays with the part before; the rest of this part is its own.
          for (let y = r.y0; y <= r.y1; y++) for (let x = r.x0; x <= r.x1; x++) if ((k === 0 || x > parts[k - 1]!.x1 || y > parts[k - 1]!.y1) && this.owner[y * w + x] === index) this.owner[y * w + x] = own;
        }
        const decor = new Map<DecorKind, number>();
        let doorsBySide = [0, 0, 0, 0];
        for (let y = r.y0; y <= r.y1; y++) {
          for (let x = r.x0; x <= r.x1; x++) {
            const i = y * w + x;
            const look = decorOf(s.decor?.[i]);
            if (look) decor.set(look.kind, (decor.get(look.kind) ?? 0) + 1);
            const t = s.tiles[i];
            if (t === 'door' || t === 'doorOpen') {
              if (y === r.y0) doorsBySide[0]!++;
              if (x === r.x1) doorsBySide[1]!++;
              if (y === r.y1) doorsBySide[2]!++;
              if (x === r.x0) doorsBySide[3]!++;
            }
          }
        }
        const span = Math.min(r.x1 - r.x0 + 1, r.y1 - r.y0 + 1);
        const most = doorsBySide.indexOf(Math.max(...doorsBySide));
        const n = (k: DecorKind) => decor.get(k) ?? 0;
        let kind: BuildingKind = 'house';
        const medical = s.medical?.find((b) => r.x0 >= b.x && r.y0 >= b.y && r.x1 < b.x + b.w && r.y1 < b.y + b.h);
        // Older saved clinics have no bounds yet. Their existing clinical
        // furniture identifies them without repainting ordinary houses.
        const medicalWear = medical?.wear ?? (n('hospitalBed') || n('medicalShelf') ? (s.campaignMission?.story === 1 ? 0.08 : 0.65) : undefined);
        if (medicalWear !== undefined) kind = 'medical';
        else if (n('bed') && doorsBySide[most]! >= 3) kind = 'motel';
        else if (n('counter')) kind = 'shop';
        // A warehouse is rows of shelves, whatever desk its office corner has; a small one full of them too (a sawmill).
        else if (
          (span >= 11 && ((n('shelf') && n('shelf') >= 2 * (n('desk') + n('rack'))) || n('hay') || gaps >= 2)) ||
          (span >= 9 && n('shelf') >= 6 && n('shelf') >= 2 * (n('desk') + n('rack')))
        )
          kind = s.biome === 'wilds' ? 'barn' : 'hall';
        else if (n('desk') || n('rack')) kind = 'office';
        else if (span >= 10) kind = 'office';
        const look = hash(r.x0, r.y0, 5);
        // Sheet metal (#25): the sawmill by the forest road, three warehouses in four on an industrial estate.
        const shed = (kind === 'barn' && s.site === 'roadside') || (kind === 'hall' && s.site === 'industrial' && look < 0.75);
        let kit: KitId = kind === 'medical' ? 'house' : shed ? SHEDS[Math.floor(hash(r.x0, r.y0, 21) * SHEDS.length)]! : kind === 'hall' ? 'brick' : s.biome === 'facility' ? 'bunker' : 'house';
        // A siege's base (docs/FASE12.md §10): the old military station's own, a storey high and flat-roofed: the
        // Sala de radio (round the heart) of concrete under its mast, the barracks of block, the infirmary of concrete,
        // the workshop and the store of sheet metal.
        let radio = false;
        if (this.siege) {
          const heart = s.mission.siege!.heart;
          radio = heart.x > r.x0 && heart.x < r.x1 && heart.y > r.y0 && heart.y < r.y1;
          const west = r.x0 + r.x1 < w;
          const north = r.y0 + r.y1 < h;
          [kind, kit] = radio ? ['office', 'bunker'] : west ? ['office', north ? 'brick' : 'bunker'] : ['hall', north ? 'shedGrey' : 'shedRed'];
        }
        // As tall as the engine says (it stops sight up to its roof: GameState.storeys), read on a tile of its floor
        // (a wall two share takes the taller one's); older saves and hand-made maps: town's shops and offices two or three, some houses two.
        const floorTile = (() => {
          for (let y = r.y0 + 1; y < r.y1; y++) for (let x = r.x0 + 1; x < r.x1; x++) if (!wallAt(y * w + x)) return y * w + x;
          return r.y0 * w + r.x0;
        })();
        const told = s.storeys?.[floorTile];
        const tall = this.siege || s.biome !== 'city' || base > 0 ? 0 : kind === 'shop' ? (look < 0.55 ? 1 : 2) : kind === 'office' ? (look < 0.6 ? 1 : 2) : kind === 'house' ? (look < 0.3 ? 1 : 0) : 0;
        const storeys = told && told > 0 ? told : 1 + tall;
        const leveled = this.slabsOver(r) > 0;
        const walkableRoof = leveled && this.walkableAt(r, storeys);
        this.buildings.push({ index: own, ...r, kind, kit, medicalWear, look, base, top: base + STOREY * storeys, storeys, front: kind === 'motel' ? most : -1, leveled, walkableRoof, ...(radio ? { radio } : {}) });
      });
      for (const i of tiles) if (this.inside[i]) this.floor[i] = 1;
    }
    if (s.biome === 'wilds') this.findRuins();
    for (let k = 0; k <= (s.levels?.length ?? 0); k++) {
      const tiles = k ? s.levels![k - 1]!.tiles : s.tiles;
      this.before.push(Uint8Array.from(tiles, (t) => (isWallLike(t ?? undefined) ? WAS_WALL : 0) | (k && t !== null ? WAS_SLAB : 0)));
    }
  }

  /** Storey k's tiles (0 the ground) as they are now. */
  private tilesAt(k: number): readonly (TileKind | null)[] | undefined {
    return k ? this.s.levels?.[k - 1]?.tiles : this.s.tiles;
  }

  /**
   * Tile i of storey k was a wall and is low cover now: blown down to rubble
   * (docs/FASE7.md §4). The engine marks it ('breach'), so it is known after a
   * reload too; before that, against the map as it was built.
   */
  breached(i: number, k: number): boolean {
    if (this.tilesAt(k)?.[i] !== 'low') return false;
    if ((this.before[k]?.[i] ?? 0) & WAS_WALL) return true;
    const decor = k ? this.s.levels?.[k - 1]?.decor : this.s.decor;
    return decorOf(decor?.[i])?.kind === 'breach';
  }

  /** The ground's tile i is part of a building's walls: a wall, a door, a window, or one blown down (breached). */
  private wallIn(i: number): boolean {
    return isWallLike(this.s.tiles[i]) || (this.s.tiles[i] === 'low' && decorOf(this.s.decor?.[i])?.kind === 'breach');
  }

  /** Storey k's (≥ 1) slab at tile i was there and is gone: blown through (docs/FASE7.md §5). */
  holed(i: number, k: number): boolean {
    return k > 0 && !!((this.before[k]?.[i] ?? 0) & WAS_SLAB) && this.tilesAt(k)?.[i] === null;
  }

  /** What stands on storey k (≥ 1) of tile (x, y): null where there's no slab (open air, a stairwell), or off the map. */
  kindAt(x: number, y: number, k: number): TileKind | null {
    const s = this.s;
    if (k < 1 || x < 0 || y < 0 || x >= s.width || y >= s.height) return null;
    return s.levels?.[k - 1]?.tiles[y * s.width + x] ?? null;
  }

  /** How many slabs (of any storey) stand over the span. */
  private slabsOver(r: Span): number {
    let n = 0;
    for (let k = 1; k <= (this.s.levels?.length ?? 0); k++) for (let y = r.y0; y <= r.y1; y++) for (let x = r.x0; x <= r.x1; x++) if (this.kindAt(x, y, k) !== null) n++;
    return n;
  }

  /** Whether storey k has somewhere to stand on within the span. */
  private walkableAt(r: Span, k: number): boolean {
    for (let y = r.y0; y <= r.y1; y++) for (let x = r.x0; x <= r.x1; x++) if (isWalkable(this.kindAt(x, y, k))) return true;
    return false;
  }

  /**
   * Raised areas: in town, a block of flats wherever one reaches two
   * storeys; other raised areas in town and facilities are loading
   * platforms; in the wilds, hills.
   */
  private findRaised(): void {
    const s = this.s;
    const { width: w, height: h } = s;
    const seen = new Uint8Array(w * h);
    for (let start = 0; start < w * h; start++) {
      if (seen[start] || !(s.elev[start] ?? 0)) continue;
      const tiles = [start];
      seen[start] = 1;
      let tall = false;
      for (let k = 0; k < tiles.length; k++) {
        const i = tiles[k]!;
        if ((s.elev[i] ?? 0) >= 2) tall = true;
        const x = i % w;
        const y = (i - x) / w;
        for (const d of DIRS4) {
          const nx = x + d.x;
          const ny = y + d.y;
          if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
          const j = ny * w + nx;
          if (seen[j] || !(s.elev[j] ?? 0)) continue;
          seen[j] = 1;
          tiles.push(j);
        }
      }
      const style = s.mission.siege ? RAISED_TOWER : s.biome === 'wilds' ? RAISED_HILL : tall && s.biome === 'city' ? RAISED_FLATS : RAISED_PLATFORM;
      for (const i of tiles) this.raised[i] = style;
    }
  }

  /**
   * Ruins in the wilds: the walls left of a farmhouse (their floor is open
   * ground again). Walls that belong to no building, a few tiles apart at
   * most, round a space at least 5 tiles a side, make a roofless building:
   * the walls get their inside and outside faces, and its floor is the ground.
   */
  private findRuins(): void {
    const s = this.s;
    const { width: w, height: h } = s;
    const loose = (i: number) => this.wallIn(i) && this.owner[i] === -1 && !this.massive[i];
    const seen = new Uint8Array(w * h);
    for (let start = 0; start < w * h; start++) {
      if (seen[start] || !loose(start)) continue;
      const tiles = [start];
      seen[start] = 1;
      let [x0, y0, x1, y1] = [w, h, -1, -1];
      for (let k = 0; k < tiles.length; k++) {
        const i = tiles[k]!;
        const x = i % w;
        const y = (i - x) / w;
        [x0, y0, x1, y1] = [Math.min(x0, x), Math.min(y0, y), Math.max(x1, x), Math.max(y1, y)];
        // Gaps where the walls were knocked down are up to three tiles long.
        for (let dy = -4; dy <= 4; dy++) {
          for (let dx = -4; dx <= 4; dx++) {
            const nx = x + dx;
            const ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= w || ny >= h || (dx && dy)) continue;
            const j = ny * w + nx;
            if (seen[j] || !loose(j)) continue;
            seen[j] = 1;
            tiles.push(j);
          }
        }
      }
      if (x1 - x0 < 4 || y1 - y0 < 4) continue;
      const index = this.buildings.length;
      for (let y = y0; y <= y1; y++) {
        for (let x = x0; x <= x1; x++) {
          const i = y * w + x;
          if (this.owner[i] !== -1) continue;
          const edge = x === x0 || y === y0 || x === x1 || y === y1;
          if (this.wallIn(i)) this.owner[i] = index;
          else if (!edge) {
            this.owner[i] = index;
            this.inside[i] = 1;
          }
        }
      }
      this.buildings.push({ index, x0, y0, x1, y1, kind: 'ruin', kit: 'house', look: hash(x0, y0, 5), base: 0, top: STOREY, storeys: 1, front: -1, leveled: false, walkableRoof: false });
    }
  }

  /** Floor of building `b` at tile (x, y). */
  /**
   * Splits a building's bounds at the walls it shares with a neighbour: a
   * straight line of plain wall right across it, with shops on both sides
   * (shelves, counters). A motel's rooms are walled off from each other too,
   * but they're one building: they aren't shops.
   */
  private partyWalls(r: Span): Span[] {
    const s = this.s;
    const w = s.width;
    const shopAt = (x0: number, y0: number, x1: number, y1: number) => {
      for (let y = y0; y <= y1; y++) {
        for (let x = x0; x <= x1; x++) {
          const k = decorOf(s.decor?.[y * w + x])?.kind;
          if (k === 'counter' || k === 'shelf') return true;
        }
      }
      return false;
    };
    for (let x = r.x0 + 4; x <= r.x1 - 4; x++) {
      let solid = true;
      for (let y = r.y0; y <= r.y1 && solid; y++) solid = s.tiles[y * w + x] === 'wall';
      if (solid && shopAt(r.x0, r.y0, x - 1, r.y1) && shopAt(x + 1, r.y0, r.x1, r.y1)) return [{ ...r, x1: x }, ...this.partyWalls({ ...r, x0: x })];
    }
    for (let y = r.y0 + 4; y <= r.y1 - 4; y++) {
      let solid = true;
      for (let x = r.x0; x <= r.x1 && solid; x++) solid = s.tiles[y * w + x] === 'wall';
      if (solid && shopAt(r.x0, r.y0, r.x1, y - 1) && shopAt(r.x0, y + 1, r.x1, r.y1)) return [{ ...r, y1: y }, ...this.partyWalls({ ...r, y0: y })];
    }
    return [r];
  }

  interior(b: number, x: number, y: number): boolean {
    const { width: w, height: h } = this.s;
    if (x < 0 || y < 0 || x >= w || y >= h) return false;
    const i = y * w + x;
    return this.owner[i] === b && this.inside[i] === 1;
  }

  /** The building whose floor tile `p` is, or -1. */
  at(p: Vec2): number {
    const { width: w, height: h } = this.s;
    if (p.x < 0 || p.y < 0 || p.x >= w || p.y >= h) return -1;
    const i = p.y * w + p.x;
    return this.inside[i] ? this.owner[i]! : -1;
  }
}

// ------------------------------------------------------------------ pieces

/** One mesh of a kit piece, standing on y = 0, running along +X, its outer face towards +Z. */
export interface PiecePart {
  name: string;
  geometry: THREE.BufferGeometry;
  atlas: AtlasId;
  matrix: THREE.Matrix4;
  /** The part that swings open (a door's leaf); its hinge, in the piece's frame. */
  leaf: boolean;
  hinge: THREE.Vector3;
  /** Its hinge is on its far end (it swings the other way to open inwards, as a gate's second leaf). */
  far?: boolean;
}

const pieces = new Map<PieceId, PiecePart[]>();
const pending = new Map<PieceId, Promise<void>>();

/** World units per metre of a kit piece. */
const KIT = SYNTY_SCALE * KIT_SCALE;
/**
 * Walls are drawn this much thicker than Synty's 0.2 m, so a soldier in cover
 * against one reads as touching it (the tile's middle is a whole tile from
 * the next wall's). Doors, windows and posts thicken with them.
 */
const THICK = 1.75;
/** Half a wall's thickness, in tiles. */
const WALL_HALF = 0.1 * THICK * KIT;

function prepare(id: PieceId, model: THREE.Group): void {
  const a = PIECES[id] as PieceAsset;
  model.updateMatrixWorld(true);
  // Blender (x, y, z) arrives as glTF (x, z, −y).
  const [along, across] = a.centre;
  const centre = a.run === 'y' ? new THREE.Vector3(across * SYNTY_SCALE, 0, -along * SYNTY_SCALE) : new THREE.Vector3(along * SYNTY_SCALE, 0, -across * SYNTY_SCALE);
  const out = a.out ?? -1;
  const turn = a.run === 'y' ? (out > 0 ? -Math.PI / 2 : Math.PI / 2) : out > 0 ? Math.PI : 0;
  // KIT_SCALE bigger (in metres, whatever the FBX's unit), and a storey of the kit stretched to STOREY.
  const k = KIT_SCALE * (a.unit ?? 1);
  const fit = new THREE.Matrix4()
    .makeScale(k * (a.squeeze ?? 1), k * (STOREY / ((a.storey ?? 2.9) * KIT)), k)
    .multiply(new THREE.Matrix4().makeRotationY(turn))
    .multiply(new THREE.Matrix4().makeTranslation(-centre.x, -(a.base ?? 0) * SYNTY_SCALE, -centre.z));
  const list: PiecePart[] = [];
  model.traverse((o) => {
    if (!(o instanceof THREE.Mesh)) return;
    const matrix = fit.clone().multiply(o.matrixWorld);
    const leaf = !!a.leaf && o.name === a.leaf;
    const box = new THREE.Box3().setFromBufferAttribute(o.geometry.getAttribute('position') as THREE.BufferAttribute).applyMatrix4(matrix);
    list.push({ name: o.name, geometry: o.geometry, atlas: atlasOf(o), matrix, leaf, hinge: new THREE.Vector3(a.hingeFar ? box.max.x : box.min.x, 0, (box.min.z + box.max.z) / 2), far: !!a.hingeFar });
  });
  pieces.set(id, list);
}

/** The parts of a piece, or null while it loads (asking for it starts the load). */
function pieceNow(id: PieceId): PiecePart[] | null {
  const ready = pieces.get(id);
  if (ready) return ready;
  if (!pending.has(id)) {
    const a = PIECES[id];
    pending.set(
      id,
      loadModel(a.pack, 'piece', id)
        .then((m) => prepare(id, m))
        // One that won't load (not converted yet) is drawn as nothing: waited for, the walls rebuilt for it without end.
        .catch((e: unknown) => {
          console.warn(`[synty] piece ${id} did not load`, e);
          pieces.set(id, []);
        }),
    );
  }
  return null;
}

/** The parts of a piece for other views (a searched wardrobe: mapView.ts), or null while it loads. */
export const syntyPiece = (id: PieceId): PiecePart[] | null => pieceNow(id);

/** Resolves when every piece asked for so far has loaded (or failed). */
export function piecesLoaded(): Promise<void> {
  return Promise.all(pending.values()).then(() => undefined);
}

/** Starts loading every building piece. */
export function preloadPieces(): void {
  for (const id of Object.keys(PIECES) as PieceId[]) pieceNow(id);
}

// ---------------------------------------------------------------- swatches

/**
 * A patch of an atlas (glTF uv: u right, v down) that Synty paints a whole
 * surface with, repeated every `period` tiles in world space. A patch of no
 * size is one of the palette's flat colours.
 */
interface Swatch {
  atlas: AtlasId;
  rect: [number, number, number, number];
  period: number;
}

/** Inset so mipmaps don't bleed the neighbouring patches in. */
const swatch = (atlas: AtlasId, u0: number, v0: number, u1: number, v1: number, period: number, inset = 0.004): Swatch => ({
  atlas,
  rect: [u0 + inset, v0 + inset, Math.max(0, u1 - u0 - 2 * inset), Math.max(0, v1 - v0 - 2 * inset)],
  period,
});

/** A grey of the ramp along the bottom of Town's atlas, from black (0) to white (1): a flat colour. */
const grey = (k: number): Swatch => ({ atlas: 'town', rect: [0.0078 + Math.round(k * 15) * 0.03125, 0.985, 0, 0], period: 1 });

// Town's atlas (PolygonTown_01_A): measured from the kit's own floor, roof and wall pieces.
const SWATCH = {
  /** Floorboards (Interior_Floor). */
  boards: swatch('town', 0.2534, 0.2518, 0.4959, 0.4943, 2),
  /** Shingles (Roof_Angled): stripes down the slope. */
  shingles: swatch('town', 0.507, 0.2579, 0.6689, 0.4346, 2),
  /** Weatherboard siding (the walls' outer face). */
  siding: swatch('town', 0.5, 0.007, 0.669, 0.25, 2),
  /** Ceiling panels (Interior_Ceiling): tiled floors. */
  panels: swatch('town', 0.0103, 0.2591, 0.2407, 0.4895, 1.6),
  /** White: parapets, tar, concrete, in the colour they are given. */
  flat: grey(1),
};

/** The average colour Synty painted each patch (sRGB), to tint it towards another. */
const MEAN = { boards: 0x763d35, shingles: 0x2f363e, siding: 0xbdc2bf, plaster: 0x8fa4aa, panels: 0xb6b5b5, flat: 0xffffff };

/** The (linear) multiplier that turns a patch whose average is `mean` into `target` (both sRGB). */
function tintTo(target: number, mean: number): THREE.Color {
  const t = new THREE.Color(target);
  const m = new THREE.Color(mean);
  return new THREE.Color().setRGB(t.r / m.r, t.g / m.g, t.b / m.b);
}

/**
 * How each kind of building looks, as sRGB colours picked per building: its
 * siding, the plaster inside, its floor (on which patch) and its roof
 * (shingles on gables, tar on flat roofs).
 */
interface Style {
  outer: number[];
  inner: number[];
  floor: [keyof typeof MEAN & keyof typeof SWATCH, number[]];
  /** Shingles of a gable roof. */
  roof: number[];
  /** A flat roof's membrane: pale concrete and render, as in Synty's captures (weathered in the shader). */
  flat?: number[];
}

/** Flat roofs that say nothing of their own. */
const FLAT_ROOFS = [0xcdbfb3, 0xc2b8ae, 0xd5c8b8, 0xbab2aa, 0xc9bba4];

const STYLES: Record<BuildingKind, Style> = {
  medical: {
    outer: [0xe0ddd0], inner: [0xe0ddd0],
    floor: ['flat', [0xe0ddd0]], roof: [0x65645e],
    flat: [0xb7b5ac],
  },
  house: {
    outer: [0xeee4cc, 0xe8d49c, 0xc0d4b6, 0xb8cce0, 0xe8c2ae, 0xf2f2ec, 0xccccc6, 0xc0ded0],
    inner: [0xd6ccb8, 0xc0ccb8, 0xdac0ac, 0xbac4ce, 0xccccc8],
    floor: ['boards', [0x966c46, 0x765236, 0xac845c, 0x88543e]],
    roof: [0x484442, 0x763e32, 0x425262, 0x58624a, 0x665446],
  },
  shop: {
    outer: [0xe8d49c, 0xe8c2ae, 0xb8cce0, 0xc0ded0, 0xdcaa78, 0xaac896],
    inner: [0xe6e0d4, 0xd6dce2, 0xe2d8c8],
    floor: ['panels', [0xd6d2c8, 0xc4ccd2, 0xdcd4be]],
    roof: [0x5a5a58, 0x4e5256, 0x625a52],
    flat: [0xd2c4b6, 0xc8bcae, 0xdacbb8, 0xc6b2a2],
  },
  office: {
    outer: [0xdcd6c8, 0xbebfbe, 0xd0c4b0, 0xc4ccd0],
    inner: [0xe2e2de, 0xd8d4cc, 0xd2d8dc],
    floor: ['panels', [0xaaaeb2, 0xbab4a8, 0xa4acb4]],
    roof: [0x58585a, 0x4c4e52],
  },
  hall: {
    outer: [0xaaa08c, 0x96a0a5, 0xb49678, 0x9c9c94],
    inner: [0xb4b4ae, 0xaeb2b4],
    floor: ['flat', [0x8c8a86, 0x9a948a, 0x868c90]],
    roof: [0x4e4e4c, 0x5c544a, 0x484e54],
    // Warehouses: sheet metal, darker.
    flat: [0x7c8086, 0x8a8478, 0x737a80],
  },
  barn: {
    outer: [0x9c3a2c, 0x8a3428, 0xa8a49a, 0x7c5a3c],
    inner: [0xa88c6c, 0x9c8468],
    floor: ['flat', [0x8c7a58, 0x9a8660]],
    roof: [0x4a4a48, 0x6a4a3a, 0x5a5e62],
  },
  // A roadside motel: pastel render and a flat roof.
  motel: {
    outer: [0xe8d2b0, 0xd6e2d2, 0xf0c8b4, 0xcfdce6],
    inner: [0xe6dccb, 0xd8d2c4],
    floor: ['boards', [0x8a6e52, 0x7a5e44]],
    roof: [0x5a5a58],
    flat: [0xc9bcab, 0xd2c6b2],
  },
  ruin: {
    outer: [0xb8b0a0, 0xa8a49a, 0x9a9488],
    inner: [0xa8a49a, 0x9c9890],
    floor: ['flat', [0x8c8a86]],
    roof: [0x4a4a48],
  },
};

const pick = (list: number[], k: number) => list[Math.floor(k * list.length) % list.length]!;

/** A shed's gables and trim in its sheet metal's colour. */
const SHED_TINT: Partial<Record<KitId, number>> = { shedGrey: 0xa4a8aa, shedRed: 0x9a4436, shedBlue: 0x4a6a8a };

/** The colours of building b, as tints of the patches they go on. */
function looksOf(b: Building) {
  const st = STYLES[b.kind];
  const outer = SHED_TINT[b.kit] ?? pick(st.outer, b.look);
  const [floor, floors] = st.floor;
  return {
    /** On the siding of walls and gables, and the plaster inside. */
    outer: tintTo(outer, MEAN.siding),
    inner: tintTo(pick(st.inner, hash(b.x0, b.y0, 13)), MEAN.plaster),
    /** A flat colour: parapets. */
    trim: new THREE.Color(outer),
    floor: tintTo(pick(floors, hash(b.x0, b.y0, 9)), MEAN[floor]),
    floorSwatch: SWATCH[floor],
    shingles: tintTo(pick(st.roof, hash(b.x0, b.y0, 11)), MEAN.shingles),
    tar: new THREE.Color(pick(st.flat ?? FLAT_ROOFS, hash(b.x0, b.y0, 11))),
  };
}

/**
 * Paints a material from swatches: each vertex says which patch (aSwatch) and
 * where on its surface it is (aPlane, in periods); the patch repeats. The
 * derivatives come from the continuous coordinates, so there is no seam where
 * it wraps.
 */
/** Town's place in the packed texture: the swatches are Town's. */
const TOWN = atlasRect('town')!;

function swatchPatch(material: THREE.MeshStandardMaterial): void {
  extendShader(material, 'swatch', (shader) => {
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', '#include <common>\nattribute vec2 aPlane;\nattribute vec4 aSwatch;\nvarying vec2 vPlane;\nvarying vec4 vSwatch;')
      .replace('#include <uv_vertex>', '#include <uv_vertex>\nvPlane = aPlane;\nvSwatch = aSwatch;');
    shader.fragmentShader = shader.fragmentShader.replace('#include <common>', '#include <common>\nvarying vec2 vPlane;\nvarying vec4 vSwatch;').replace(
      '#include <map_fragment>',
      `{
        // Town's swatches, in Town's place in the packed texture (mapAtlas.ts).
        vec2 swScale = vSwatch.zw * vec2(${TOWN[2].toFixed(4)}, ${TOWN[3].toFixed(4)});
        vec2 swUv = vec2(${TOWN[0].toFixed(4)}, ${TOWN[1].toFixed(4)}) + vSwatch.xy * vec2(${TOWN[2].toFixed(4)}, ${TOWN[3].toFixed(4)}) + fract(vPlane) * swScale;
        vec4 swTexel = textureGrad(map, swUv, dFdx(vPlane) * swScale, dFdy(vPlane) * swScale);
        diffuseColor *= swTexel;
      }`,
    );
  });
}

// ---------------------------------------------------------------- cutaway

/**
 * What the player is looking at, from the game: the selected soldier, the
 * cursor's place (with its storey, `z`), the target, and the active storey
 * (`level`: the selected soldier's, or the one chosen with PageUp/PageDown,
 * the wheel or the indicator: docs/FASE6.md). Buildings show that storey and
 * those below it.
 */
export interface Focus {
  selected: string | null;
  hover: Place | null;
  target: string | null;
  level?: number | null;
}

/** The cut map's alpha holds the storey in view, this much a storey. */
const FLOOR_CODE = 64;
/** Seconds a building keeps its cutaway after the reason for it goes, and a fade takes. */
const HOLD = 0.6;
const FADE = 0.3;
/** Height the cut walls drop to (about the low cover's). */
const CUT_LOW = 0.95;
/** Closer than this (camera to the point it looks at), roofs near that point fade. */
const ZOOM_NEAR = 15;
const ZOOM_RADIUS = 8;

/**
 * The cutaway of every building: a texel each (r roof gone, g walls facing
 * the camera cut, b every wall cut, a the storey in view: what stands above
 * it fades with r, its walls drop with g and b), read by the materials of
 * walls, roofs, floors and furniture.
 */
export class Cutaway {
  readonly uniforms = {
    cutMap: { value: new THREE.DataTexture(new Uint8Array(4), 1, 1) as THREE.DataTexture },
    cutCount: { value: 1 },
    /** The camera's view direction on the ground (x, z). */
    cutView: { value: new THREE.Vector2(-Math.SQRT1_2, -Math.SQRT1_2) },
  };
  private readonly n: number;
  private readonly now: Float32Array;
  private readonly hold: Float32Array;
  private readonly marks: Uint8Array;
  /** This frame, the storey each building is opened down to (-1 none said), and whether the focus said it (it wins). */
  private readonly want: Int8Array;
  private readonly locked: Uint8Array;
  /** The storey each building shows now. */
  private readonly shown: Uint8Array;
  private readonly data: Uint8Array;
  private boxes: THREE.Box3[] = [];
  private readonly ray = new THREE.Ray();
  private readonly hit = new THREE.Vector3();
  private readonly point = new THREE.Vector3();
  private readonly tile: Vec2 = { x: 0, y: 0 };
  private readonly look = new THREE.Vector3();
  /** The buildings whose inside is in the squad's sight, and the sight they were worked out for. */
  private seenInside: number[] = [];
  private seenFor: readonly boolean[] | null = null;

  constructor(private readonly plan: BuildingPlan) {
    this.n = plan.buildings.length;
    const n = Math.max(1, this.n);
    this.now = new Float32Array(n * 3);
    this.hold = new Float32Array(n * 3);
    this.marks = new Uint8Array(n * 3);
    this.want = new Int8Array(n);
    this.locked = new Uint8Array(n);
    this.shown = new Uint8Array(n);
    this.data = new Uint8Array(n * 4);
    const texture = new THREE.DataTexture(this.data, n, 1);
    texture.needsUpdate = true;
    this.uniforms.cutMap.value = texture;
    this.uniforms.cutCount.value = n;
    this.measure();
  }

  /** The space each building hides things in: to the walls' middle lines and up to its roof (call once the roofs are built). */
  measure(): void {
    this.boxes = this.plan.buildings.map((b) => new THREE.Box3(new THREE.Vector3(b.x0 + 0.4, b.base, b.y0 + 0.4), new THREE.Vector3(b.x1 + 0.6, b.top, b.y1 + 0.6)));
  }

  /**
   * Works out which buildings open up this frame and moves every fade towards
   * it. With `inside`, a building whose inside tiles are in `visible` (seen
   * through a window or a door) loses its roof.
   */
  update(dt: number, s: GameState, camera: THREE.Camera | null, focus: Focus | null, visible: readonly boolean[] | null, inside = true): void {
    if (!this.n) return;
    this.marks.fill(0);
    this.want.fill(-1);
    this.locked.fill(0);
    const plan = this.plan;
    const mark = (b: number, what: 0 | 1 | 2) => {
      if (b >= 0) this.marks[b * 3 + what] = 1;
    };
    /** Building b is opened down to storey `level` (the highest anyone in it stands on; `keen`, the focus's, wins). */
    const aim = (b: number, level: number, keen: boolean) => {
      if (b < 0 || this.locked[b]) return;
      if (keen) {
        this.want[b] = level;
        this.locked[b] = 1;
      } else this.want[b] = Math.max(this.want[b]!, level);
    };
    const selectedUnit = focus?.selected ? s.units.find((u) => u.id === focus.selected) : undefined;
    /** The storey the player looks at: the active one, else the selected soldier's. */
    const view = focus?.level ?? selectedUnit?.pos.z ?? 0;
    /** Chosen by hand (not the selected soldier's own): it wins over where the soldier stands. */
    const chosen = focus?.level != null && focus.level !== (selectedUnit?.pos.z ?? 0);
    if (visible !== this.seenFor) {
      this.seenFor = visible;
      const seen = new Set<number>();
      // A mask by place: the ground's tiles, then each storey's.
      const area = s.width * s.height;
      if (visible) for (let n = 0; n < visible.length; n++) if (visible[n]) seen.add(plan.at({ x: (n % area) % s.width, y: Math.floor((n % area) / s.width) }));
      seen.delete(-1);
      this.seenInside = [...seen];
    }
    if (inside) for (const b of this.seenInside) mark(b, 0);
    /** Opens building b for something inside it on storey `level`: what stands above off, the walls facing the camera down. */
    const within = (p: Vec2, level: number, keen = false) => {
      const b = plan.at(p);
      mark(b, 0);
      mark(b, 1);
      aim(b, level, keen);
    };
    if (camera) {
      camera.getWorldDirection(this.look);
      const flat = Math.hypot(this.look.x, this.look.z) || 1;
      this.uniforms.cutView.value.set(this.look.x / flat, this.look.z / flat);
    }
    /**
     * Every building between the camera and the world point `at` (not the one
     * `own` it is in) loses its roof; with `all`, all its walls drop too.
     */
    const behind = (at: THREE.Vector3, own: number, all: boolean) => {
      if (!camera) return;
      this.ray.origin.copy(camera.position);
      this.ray.direction.copy(at).sub(camera.position);
      const far = this.ray.direction.length();
      this.ray.direction.divideScalar(far);
      for (let b = 0; b < this.n; b++) {
        if (b === own) continue;
        const hit = this.ray.intersectBox(this.boxes[b]!, this.hit);
        if (hit && hit.distanceTo(camera.position) < far - 0.3) {
          mark(b, 0);
          if (all) mark(b, 2);
        }
      }
    };
    /** Where a unit is now (mid-walk too), its tile in `this.tile` and its chest in `this.point`. */
    const locate = (u: { id: string; pos: Place }) => {
      if (unitPosNow(u.id, this.point)) {
        this.tile.x = Math.floor(this.point.x);
        this.tile.y = Math.floor(this.point.z);
      } else {
        this.tile.x = u.pos.x;
        this.tile.y = u.pos.y;
        this.point.set(u.pos.x + 0.5, groundAt(u.pos), u.pos.y + 0.5);
      }
      this.point.y += 0.9;
    };
    // The cursor first: the storey under it is the one the player looks at in that building.
    if (focus?.hover) {
      const p = focus.hover;
      within(p, p.z ?? view, true);
      this.point.set(p.x + 0.5, groundAt(p) + 0.2, p.y + 0.5);
      behind(this.point, plan.at(p), true);
    }
    for (const u of s.units) {
      if (!u.alive) continue;
      const ours = u.team === 'xcom';
      if (!ours && !(visible?.[((u.pos.z ?? 0) * s.height + u.pos.y) * s.width + u.pos.x] ?? false)) continue;
      locate(u);
      within(this.tile, u.pos.z ?? 0, u.id === focus?.selected && !chosen);
      const keen = u.id === focus?.selected || u.id === focus?.target;
      behind(this.point, plan.at(this.tile), keen);
      if (u.id !== focus?.selected) continue;
      // The selected soldier by a building's wall (about to go in, or in cover against it).
      for (const b of plan.buildings) {
        const dx = Math.max(b.x0 - this.tile.x, 0, this.tile.x - b.x1);
        const dy = Math.max(b.y0 - this.tile.y, 0, this.tile.y - b.y1);
        if (dx + dy <= 1) {
          mark(b.index, 0);
          mark(b.index, 1);
          aim(b.index, u.pos.z ?? 0, false);
        }
      }
    }
    if (camera) {
      // Zoomed in: the roofs round where the camera looks.
      camera.getWorldDirection(this.look);
      if (this.look.y < -0.05) {
        const t = -camera.position.y / this.look.y;
        if (t < ZOOM_NEAR) {
          const gx = camera.position.x + this.look.x * t;
          const gz = camera.position.z + this.look.z * t;
          for (const b of plan.buildings) {
            const dx = Math.max(b.x0 - gx, 0, gx - b.x1 - 1);
            const dz = Math.max(b.y0 - gz, 0, gz - b.y1 - 1);
            if (dx * dx + dz * dz < ZOOM_RADIUS * ZOOM_RADIUS) mark(b.index, 0);
          }
        }
      }
    }
    // A storey chosen by hand: every building with storeys above it opens down to it.
    if (chosen) for (const b of plan.buildings) if (b.leveled && b.storeys > view) mark(b.index, 0);
    // The storey each opened building shows: the cursor's or the selected soldier's in theirs; else the one chosen by
    // hand; else the highest anyone in it stands on; else the one in view. A building whose storeys aren't real shows
    // its ground floor. Kept while it fades back.
    let changed = false;
    for (const b of plan.buildings) {
      const k = b.index;
      if (!this.marks[k * 3] && !this.marks[k * 3 + 1] && !this.marks[k * 3 + 2]) continue;
      const asked = this.locked[k] || (!chosen && this.want[k]! >= 0) ? this.want[k]! : view;
      const floor = b.leveled ? Math.max(0, Math.min(b.storeys, asked)) : 0;
      if (floor !== this.shown[k]) {
        this.shown[k] = floor;
        changed = true;
      }
    }
    // Walls opened right through also lose their roof; held a moment, then faded.
    const step = dt / FADE;
    for (let k = 0; k < this.n * 3; k++) {
      if (this.marks[k]) this.hold[k] = HOLD;
      else this.hold[k] = Math.max(0, this.hold[k]! - dt);
      const goal = this.hold[k]! > 0 ? 1 : 0;
      const v = this.now[k]!;
      const next = v < goal ? Math.min(goal, v + step) : Math.max(goal, v - step);
      if (next !== v) {
        this.now[k] = next;
        changed = true;
      }
    }
    if (!changed) return;
    for (let b = 0; b < this.n; b++) {
      // Smoothstep: the fades ease in and out.
      for (let c = 0; c < 3; c++) {
        const v = this.now[b * 3 + c]!;
        this.data[b * 4 + c] = Math.round(v * v * (3 - 2 * v) * 255);
      }
      this.data[b * 4 + 3] = this.shown[b]! * FLOOR_CODE;
    }
    this.uniforms.cutMap.value.needsUpdate = true;
  }

  /**
   * Whether place `p` is hidden by the cutaway: on a storey above the one its
   * building shows, while that building is cut (what stands there, a unit
   * included, isn't to be drawn).
   */
  hides(p: Place): boolean {
    const b = this.plan.at(p);
    return b >= 0 && (p.z ?? 0) > this.shown[b]! && this.now[b * 3]! > 0.5;
  }

  dispose(): void {
    this.uniforms.cutMap.value.dispose();
  }
}

const CUT_GLSL = /* glsl */ `
  uniform sampler2D cutMap;
  uniform float cutCount;
  uniform vec2 cutView;
  vec4 cutState(float building) {
    return texture2D(cutMap, vec2((building - 0.5) / cutCount, 0.5));
  }
  // The storey in view in a building that is cut (its alpha, ${FLOOR_CODE} a storey): what stands higher fades.
  float cutFloor(vec4 state) {
    return floor(state.a * ${(255 / FLOOR_CODE).toFixed(4)} + 0.5);
  }
  // How far what stands on storey 'level' of 'building' (+1; 0 none) has faded.
  float cutAbove(float building, float level) {
    if (building < 0.5) return 0.0;
    vec4 state = cutState(building);
    return level > cutFloor(state) + 0.5 ? state.r : 0.0;
  }`;

/** A pixel's threshold in a 4×4 ordered dither: fades with no sorting, steady on screen. */
const DITHER_GLSL = /* glsl */ `
  const float CUT_BAYER[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
  float cutDither() {
    ivec2 p = ivec2(mod(gl_FragCoord.xy, 4.0));
    return (CUT_BAYER[p.x + p.y * 4] + 0.5) / 16.0;
  }`;

/** aCut's way out for pieces on a roof (a chimney, an air conditioner): they fade with the roof. */
const ON_ROOF = 9;

/**
 * Walls that drop for the cutaway. Each instance carries aCut: its building
 * (+1; 0 none), the way its outer face looks (x, z; none between rooms, ON_ROOF
 * for what stands on the roof and fades with it) and the height of its floor.
 * Seen through the cut, the wall's inside is a flat dark colour, so it reads
 * as a solid section.
 */
function wallCutPatch(material: THREE.Material, cut: Cutaway): void {
  extendShader(material, 'wall-cut', (shader) => {
    Object.assign(shader.uniforms, cut.uniforms, { uAtlasRects: ATLAS_RECTS });
    shader.vertexShader = shader.vertexShader
      .replace(
        '#include <common>',
        `#include <common>
        ${CUT_GLSL}
        attribute vec4 aCut;
        attribute float aLevel;
        attribute vec3 aOuter;
        attribute vec3 aInner;
        attribute float aAtlas;
        varying float vAtlas;
        varying float vCutY;
        varying float vCutTop;
        varying float vCutFade;
        varying vec3 vOuter;
        varying vec3 vInner;`,
      )
      .replace(
        '#include <project_vertex>',
        `#include <project_vertex>
        {
          vec4 cutWorld = vec4(transformed, 1.0);
          #ifdef USE_INSTANCING
            cutWorld = instanceMatrix * cutWorld;
          #endif
          cutWorld = modelMatrix * cutWorld;
          vCutY = cutWorld.y;
          vCutTop = 1e4;
          vCutFade = 0.0;
          vOuter = aOuter;
          vInner = aInner;
          vAtlas = aAtlas;
          if (aCut.x > 0.5) {
            vec4 state = cutState(aCut.x);
            float shown = cutFloor(state);
            if (aLevel > shown + 0.5) {
              // Above the storey in view (the roof, a façade's upper storeys, a building's real storeys): it fades.
              vCutFade = state.r;
            } else if (aLevel > shown - 0.5 && aCut.y < ${ON_ROOF - 1}.0) {
              // On the storey in view: between rooms, or facing the camera, the walls drop.
              float facing = dot(aCut.yz, aCut.yz) < 0.01 ? 1.0 : smoothstep(-0.15, 0.25, -dot(aCut.yz, cutView));
              float k = max(state.g * facing, state.b);
              if (k > 0.0) vCutTop = aCut.w + mix(${(STOREY + 0.05).toFixed(2)}, ${CUT_LOW.toFixed(2)}, k);
            }
          }
        }`,
      );
    shader.fragmentShader = shader.fragmentShader
      .replace(
        '#include <common>',
        `#include <common>\n${DITHER_GLSL}\nvarying float vCutY;\nvarying float vCutTop;\nvarying float vCutFade;\nvarying vec3 vOuter;\nvarying vec3 vInner;\nvarying float vAtlas;\nuniform vec4 uAtlasRects[${PACKED.length}];`,
      )
      .replace('#include <clipping_planes_fragment>', '#include <clipping_planes_fragment>\nif (vCutY > vCutTop || cutDither() < vCutFade) discard;')
      .replace(
        '#include <map_fragment>',
        `#include <map_fragment>
        #ifdef USE_MAP
          // Town's atlas: plaster (left) and siding (right) along its top; the trims between keep their white.
          // The dye reads the UV in the piece's own atlas (its place in the packed texture: mapAtlas.ts).
          vec2 dyeUv = vMapUv;
          if (vAtlas > -0.5) {
            vec4 dyeRect = uAtlasRects[int(vAtlas + 0.5)];
            dyeUv = (vMapUv - dyeRect.xy) / dyeRect.zw;
          }
          if (dyeUv.y < 0.25) diffuseColor.rgb *= dyeUv.x < 0.4167 ? vInner : (dyeUv.x > 0.506 ? vOuter : vec3(1.0));
        #endif
        if (!gl_FrontFacing) diffuseColor.rgb = vec3(0.16, 0.15, 0.14);`,
      );
  });
}

/**
 * Roofs and a building's upper floors that fade (dithered) for the cutaway;
 * per vertex aRoof, the building (+1), and aLevel, the storey it stands on:
 * it fades while its building is cut below that storey.
 */
function roofFadePatch(material: THREE.Material, cut: Cutaway): void {
  extendShader(material, 'roof-fade', (shader) => {
    Object.assign(shader.uniforms, cut.uniforms);
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', `#include <common>\n${CUT_GLSL}\nattribute float aRoof;\nattribute float aLevel;\nvarying float vRoofFade;`)
      .replace(
        '#include <project_vertex>',
        `#include <project_vertex>
        vRoofFade = cutAbove(aRoof, aLevel);
        // Gone: out of the view altogether, so it costs nothing.
        if (vRoofFade > 0.995) gl_Position = vec4(2.0, 2.0, 2.0, 1.0);`,
      );
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', `#include <common>\n${DITHER_GLSL}\nvarying float vRoofFade;`)
      .replace('#include <clipping_planes_fragment>', '#include <clipping_planes_fragment>\nif (cutDither() < vRoofFade) discard;');
  });
}

/**
 * Furniture and props that stand on a building's storeys (mapView.ts): per
 * vertex aFloor, the building (+1; 0 for what stands out of doors) and the
 * storey. They fade with the floors above the one in view.
 */
export function floorFadePatch(material: THREE.Material, cut: Cutaway): void {
  extendShader(material, 'floor-fade', (shader) => {
    Object.assign(shader.uniforms, cut.uniforms);
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', `#include <common>\n${CUT_GLSL}\nattribute vec2 aFloor;\nvarying float vFloorFade;`)
      .replace(
        '#include <project_vertex>',
        `#include <project_vertex>
        vFloorFade = cutAbove(aFloor.x, aFloor.y);
        if (vFloorFade > 0.995) gl_Position = vec4(2.0, 2.0, 2.0, 1.0);`,
      );
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', `#include <common>\n${DITHER_GLSL}\nvarying float vFloorFade;`)
      .replace('#include <clipping_planes_fragment>', '#include <clipping_planes_fragment>\nif (cutDither() < vFloorFade) discard;');
  });
}

/**
 * Weather on what lies open to the sky (flat roofs, the tops of flats): grime
 * in broad patches, darker towards the edges' drains, and moss in clumps, as
 * on the roofs in Synty's captures. Worked out in world space: no two roofs
 * alike, nothing lines up with the tiles. Only up-facing faces.
 */
const WEATHER_GLSL = /* glsl */ `
  varying vec3 vWxPos;
  varying vec3 vWxNormal;
  float wxHash(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
  }
  float wxNoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(wxHash(i), wxHash(i + vec2(1.0, 0.0)), f.x), mix(wxHash(i + vec2(0.0, 1.0)), wxHash(i + vec2(1.0, 1.0)), f.x), f.y);
  }`;

function weatherPatch(material: THREE.Material): void {
  extendShader(material, 'weather', (shader) => {
    shader.vertexShader = shader.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 vWxPos;\nvarying vec3 vWxNormal;').replace(
      '#include <project_vertex>',
      `#include <project_vertex>
      vWxPos = (modelMatrix * vec4(transformed, 1.0)).xyz;
      vWxNormal = normalize(mat3(modelMatrix) * objectNormal);`,
    );
    shader.fragmentShader = shader.fragmentShader.replace('#include <common>', `#include <common>\n${WEATHER_GLSL}`).replace(
      '#include <color_fragment>',
      `#include <color_fragment>
      if (vWxNormal.y > 0.9) {
        vec2 wp = vWxPos.xz;
        float broad = wxNoise(wp * 0.3) * 0.6 + wxNoise(wp * 0.9 + 7.0) * 0.3 + wxNoise(wp * 3.1) * 0.1;
        // Grime in broad patches and streaks, a little moss where it's worst.
        diffuseColor.rgb *= 1.0 - 0.24 * smoothstep(0.48, 0.8, broad);
        diffuseColor.rgb *= 0.94 + 0.1 * wxNoise(wp * vec2(1.2, 6.0) + 3.0);
        float moss = smoothstep(0.74, 0.78, broad + wxNoise(wp * 2.4) * 0.12);
        diffuseColor.rgb = mix(diffuseColor.rgb, vec3(0.2, 0.22, 0.1) * (0.8 + 0.4 * wxNoise(wp * 7.0)), moss * 0.7);
      }`,
    );
  });
}

// --------------------------------------------------------------- geometry

/** Builds a merged mesh of quads and triangles painted from swatches. */
class Surfaces {
  private position: number[] = [];
  private normal: number[] = [];
  private plane: number[] = [];
  private patch: number[] = [];
  private color: number[] = [];
  private roof: number[] = [];
  private levels: number[] = [];
  /** The storey what is added now stands on: above the one in view, it fades in the cutaway (roofs: their building's top). */
  level = 0;
  private readonly n = new THREE.Vector3();
  private readonly e1 = new THREE.Vector3();
  private readonly e2 = new THREE.Vector3();

  /**
   * A flat polygon (3 or 4 corners in order round it), with its swatch
   * coordinates per corner (in tiles; divided by the period here). Its front
   * is the side `facing` points to (default: counter-clockwise corners).
   */
  poly(corners: THREE.Vector3[], planes: [number, number][], sw: Swatch, tint: THREE.Color, building: number, facing?: THREE.Vector3): void {
    this.e1.subVectors(corners[1]!, corners[0]!);
    this.e2.subVectors(corners[2]!, corners[0]!);
    this.n.crossVectors(this.e1, this.e2).normalize();
    let tri = corners.length === 3 ? [0, 1, 2] : [0, 1, 2, 0, 2, 3];
    if (facing && this.n.dot(facing) < 0) {
      tri = tri.reverse();
      this.n.negate();
    }
    for (const k of tri) {
      const c = corners[k]!;
      this.position.push(c.x, c.y, c.z);
      this.normal.push(this.n.x, this.n.y, this.n.z);
      this.plane.push(planes[k]![0] / sw.period, planes[k]![1] / sw.period);
      this.patch.push(...sw.rect);
      this.color.push(tint.r, tint.g, tint.b);
      this.roof.push(building + 1);
      this.levels.push(this.level);
    }
  }

  /** A box with the swatch on every face (a parapet, a ridge). */
  box(min: THREE.Vector3, max: THREE.Vector3, sw: Swatch, tint: THREE.Color, building: number, bottom = false): void {
    const V = (x: number, y: number, z: number) => new THREE.Vector3(x, y, z);
    const [a, b] = [min, max];
    // Top, then the four sides (planes in world tiles along each face).
    this.poly([V(a.x, b.y, a.z), V(a.x, b.y, b.z), V(b.x, b.y, b.z), V(b.x, b.y, a.z)], [[a.x, a.z], [a.x, b.z], [b.x, b.z], [b.x, a.z]], sw, tint, building);
    this.poly([V(a.x, a.y, b.z), V(b.x, a.y, b.z), V(b.x, b.y, b.z), V(a.x, b.y, b.z)], [[a.x, a.y], [b.x, a.y], [b.x, b.y], [a.x, b.y]], sw, tint, building);
    this.poly([V(b.x, a.y, a.z), V(a.x, a.y, a.z), V(a.x, b.y, a.z), V(b.x, b.y, a.z)], [[b.x, a.y], [a.x, a.y], [a.x, b.y], [b.x, b.y]], sw, tint, building);
    this.poly([V(b.x, a.y, b.z), V(b.x, a.y, a.z), V(b.x, b.y, a.z), V(b.x, b.y, b.z)], [[b.z, a.y], [a.z, a.y], [a.z, b.y], [b.z, b.y]], sw, tint, building);
    this.poly([V(a.x, a.y, a.z), V(a.x, a.y, b.z), V(a.x, b.y, b.z), V(a.x, b.y, a.z)], [[a.z, a.y], [b.z, a.y], [b.z, b.y], [a.z, b.y]], sw, tint, building);
    if (bottom) this.poly([V(a.x, a.y, a.z), V(b.x, a.y, a.z), V(b.x, a.y, b.z), V(a.x, a.y, b.z)], [[a.x, a.z], [b.x, a.z], [b.x, b.z], [a.x, b.z]], sw, tint, building);
  }

  get empty(): boolean {
    return !this.position.length;
  }

  geometry(): THREE.BufferGeometry {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(this.position, 3));
    g.setAttribute('normal', new THREE.Float32BufferAttribute(this.normal, 3));
    g.setAttribute('aPlane', new THREE.Float32BufferAttribute(this.plane, 2));
    g.setAttribute('aSwatch', new THREE.Float32BufferAttribute(this.patch, 4));
    g.setAttribute('color', new THREE.Float32BufferAttribute(this.color, 3));
    g.setAttribute('aRoof', new THREE.Float32BufferAttribute(this.roof, 1));
    g.setAttribute('aLevel', new THREE.Float32BufferAttribute(this.levels, 1));
    // The standard material wants a uv for its map; the swatch patch reads its own.
    g.setAttribute('uv', new THREE.Float32BufferAttribute(new Float32Array((this.position.length / 3) * 2), 2));
    g.computeBoundingSphere();
    return g;
  }
}

const V = (x: number, y: number, z: number) => new THREE.Vector3(x, y, z);

/** Raised blocks: their brick (the apartments' own colour), the stone of copings and the tar on their roofs. */
const BRICK = new THREE.Color(0x985c4e);
const STONE = new THREE.Color(0x968770);
/** The walkable top of a block of flats: pale roofing, weathered (WEATHER_GLSL). */
const TAR = new THREE.Color(0xbdb1a6);
/** A building's stairs: dark wood. */
const STAIR_WOOD = new THREE.Color(0x6e4c36);
/** Platforms' steel decks and posts, and the tops of solid walls. */
const DECK = new THREE.Color(0x5c6064);
const STEEL = new THREE.Color(0x3c4044);
const CONCRETE = new THREE.Color(0x8a8a86);
/** What's left of a wall blown down (docs/FASE7.md §4), as a share of its storey: about low cover's height. */
const BREACH_HEIGHT = 0.36;
/** A parapet's height (a little under the low cover's, which counts from the soldier's feet) and thickness. */
const PARAPET = 0.82;
const PARAPET_THICK = 0.2;

/** Pitch of house roofs and of the low roofs of halls. */
const PITCH = Math.tan(THREE.MathUtils.degToRad(30));
const HALL_PITCH = Math.tan(THREE.MathUtils.degToRad(14));

/**
 * A gabled roof over the walls' middle lines of b: two slopes of shingles
 * with eaves, a thickness, and siding gables at the ends.
 */
function gableRoof(out: Surfaces, b: Building, pitch: number, roofTint: THREE.Color, wallTint: THREE.Color): number {
  const along = b.x1 - b.x0 >= b.y1 - b.y0;
  // In the roof's frame: u along the ridge, v across; c the middle.
  const cx = (b.x0 + b.x1) / 2 + 0.5;
  const cz = (b.y0 + b.y1) / 2 + 0.5;
  const L = (along ? b.x1 - b.x0 : b.y1 - b.y0) / 2;
  const W = (along ? b.y1 - b.y0 : b.x1 - b.x0) / 2;
  const eave = 0.36;
  const gableOut = 0.22;
  const thick = 0.1;
  const h0 = b.base + STOREY * b.storeys;
  const H = (v: number) => h0 + thick + (W - Math.abs(v)) * pitch;
  const P = (u: number, y: number, v: number) => (along ? V(cx + u, y, cz + v) : V(cx + v, y, cz + u));
  const U = L + gableOut;
  const E = W + eave;
  /** World direction of local (u, v): what each face turns its front to. */
  const D = (u: number, y: number, v: number) => (along ? V(u, y, v) : V(v, y, u));
  const dark = roofTint.clone().multiplyScalar(0.7);
  for (const side of [1, -1]) {
    // The slope: its swatch runs along the ridge (u) and down the slope (v).
    const run = E * Math.hypot(1, pitch);
    const top = [P(-U, H(0), 0), P(U, H(0), 0), P(U, H(E), side * E), P(-U, H(E), side * E)];
    out.poly(top, [[-U, 0], [U, 0], [U, run], [-U, run]], SWATCH.shingles, roofTint, b.index, D(0, 1, side * pitch));
    // The eave's edge and the ends of the slope (rakes).
    const fascia = [P(-U, H(E) - thick, side * E), P(U, H(E) - thick, side * E), top[2]!, top[3]!];
    out.poly(fascia, [[-U, 0], [U, 0], [U, thick], [-U, thick]], SWATCH.shingles, dark, b.index, D(0, 0, side));
    for (const end of [-1, 1]) {
      const rake = [P(end * U, H(0) - thick, 0), P(end * U, H(E) - thick, side * E), P(end * U, H(E), side * E), P(end * U, H(0), 0)];
      out.poly(rake, [[0, 0], [run, 0], [run, thick], [0, thick]], SWATCH.shingles, dark, b.index, D(end, 0, 0));
      // Gables: siding from the top of the wall up under the ridge, in the end walls' plane.
      const gable = [P(end * (L + WALL_HALF), h0, side * W), P(end * (L + WALL_HALF), h0, 0), P(end * (L + WALL_HALF), H(0) - thick, 0)];
      out.poly(gable, [[side * W, h0], [0, h0], [0, H(0) - thick]], SWATCH.siding, wallTint, b.index, D(end, 0, 0));
    }
  }
  // A ridge cap.
  const ridge = H(0);
  const half = 0.07;
  const lo = along ? V(cx - U, ridge - 0.02, cz - half) : V(cx - half, ridge - 0.02, cz - U);
  const hi = along ? V(cx + U, ridge + 0.06, cz + half) : V(cx + half, ridge + 0.06, cz + U);
  out.box(lo, hi, SWATCH.shingles, roofTint.clone().multiplyScalar(0.6), b.index);
  return ridge + 0.06;
}

/** A flat roof over b: a slab of tar on the walls and a parapet round it (both flat colours). */
function flatRoof(out: Surfaces, b: Building, tar: THREE.Color, trim: THREE.Color): number {
  const h0 = b.base + STOREY * b.storeys;
  const x0 = b.x0 + 0.5 - WALL_HALF - 0.02;
  const x1 = b.x1 + 0.5 + WALL_HALF + 0.02;
  const z0 = b.y0 + 0.5 - WALL_HALF - 0.02;
  const z1 = b.y1 + 0.5 + WALL_HALF + 0.02;
  const slab = h0 + 0.04;
  out.poly([V(x0, slab, z0), V(x0, slab, z1), V(x1, slab, z1), V(x1, slab, z0)], [[x0, z0], [x0, z1], [x1, z1], [x1, z0]], SWATCH.flat, tar, b.index);
  const t = 0.16;
  const h = slab + 0.32;
  const lip = trim.clone().multiplyScalar(0.9);
  out.box(V(x0 - 0.02, h0 - 0.12, z0 - 0.02), V(x1 + 0.02, h, z0 + t), SWATCH.flat, lip, b.index);
  out.box(V(x0 - 0.02, h0 - 0.12, z1 - t), V(x1 + 0.02, h, z1 + 0.02), SWATCH.flat, lip, b.index);
  out.box(V(x0 - 0.02, h0 - 0.12, z0 + t), V(x0 + t, h, z1 - t), SWATCH.flat, lip, b.index);
  out.box(V(x1 - t, h0 - 0.12, z0 + t), V(x1 + 0.02, h, z1 - t), SWATCH.flat, lip, b.index);
  return h;
}

/** The storey a piece of building `b` at height `base` stands on (0: the ground floor, or no building). */
function storeyOf(b: Building | undefined, base: number): number {
  return b ? Math.max(0, Math.round((base - b.base) / STOREY)) : 0;
}

// -------------------------------------------------------------- the view

/** Instances of one part of one piece, collected and then turned into an InstancedMesh. */
interface Batch {
  geometry: THREE.BufferGeometry;
  atlas: AtlasId;
  matrices: THREE.Matrix4[];
  cut: number[];
  outer: number[];
  inner: number[];
  /** The storey each instance stands on (its building's, for the cutaway); the ground when missing. */
  level?: number[];
}

const WHITE = new THREE.Color(1, 1, 1);

/** Something standing on a roof: where (its foot), turned how. */
interface RoofProp {
  id: PropId;
  x: number;
  y: number;
  z: number;
  yaw: number;
}

/** The height of the Sala de radio's lattice mast (Apocalypse's SM_Bld_RadioTower_01, 6.8 m), where its head goes. */
const RADIO_MAST = 6.8 * SYNTY_SCALE;

/**
 * What stands on the roof of b, always the same for a building: a chimney on
 * some houses; air conditioners, vents and a dish on flat roofs, the stair
 * housing on offices, skylights in a row on warehouses.
 */
function roofProps(b: Building, open?: (x: number, y: number) => boolean): RoofProp[] {
  const out: RoofProp[] = [];
  const r = (salt: number) => hash(b.x0, b.y0, 40 + salt);
  const along = b.x1 - b.x0 >= b.y1 - b.y0;
  const cx = (b.x0 + b.x1) / 2 + 0.5;
  const cz = (b.y0 + b.y1) / 2 + 0.5;
  if (b.kind === 'house' && !b.walkableRoof) {
    if (r(0) < 0.65) {
      // On the ridge near one end, half sunk into the roof.
      const L = (along ? b.x1 - b.x0 : b.y1 - b.y0) / 2 - 1.1;
      const u = (r(1) < 0.5 ? -1 : 1) * L;
      const v = 0.35;
      out.push({ id: 'chimney', x: along ? cx + u : cx + v, y: b.top - 1.0, z: along ? cz + v : cz + u, yaw: along ? 0 : Math.PI / 2 });
    }
    return out;
  }
  if (b.kind === 'barn' || b.kind === 'ruin') return out;
  const y = b.base + STOREY * b.storeys + 0.04;
  if (b.radio) {
    // A siege's Sala de radio (docs/FASE12.md §10): Apocalypse's lattice mast in the middle of the roof, its head of
    // aerials on top, a dish by the parapet.
    out.push({ id: 'radioMast', x: cx, y, z: cz, yaw: 0 }, { id: 'radioMastTop', x: cx, y: y + RADIO_MAST, z: cz, yaw: 0 });
    out.push({ id: 'roofDish', x: b.x0 + 1.5, y, z: b.y1 - 1, yaw: Math.PI * 0.75 });
    return out;
  }
  // A roof one walks on keeps clear: only what's been left up there, never over the stairwell.
  const walk = b.walkableRoof;
  // Free spots on the roof, a tile in from the parapet, taken as things are put down.
  const taken = new Set<string>();
  const place = (id: PropId, x: number, z: number, w: number, d: number, yaw: number): boolean => {
    const cells: string[] = [];
    for (let i = Math.floor(x - w / 2); i <= Math.floor(x + w / 2 - 0.01); i++) for (let j = Math.floor(z - d / 2); j <= Math.floor(z + d / 2 - 0.01); j++) cells.push(`${i},${j}`);
    if (x - w / 2 < b.x0 + 1.2 || x + w / 2 > b.x1 - 0.2 || z - d / 2 < b.y0 + 1.2 || z + d / 2 > b.y1 - 0.2 || cells.some((c) => taken.has(c))) return false;
    for (const c of cells) taken.add(c);
    out.push({ id, x, y, z, yaw });
    return true;
  };
  const spot = (salt: number): [number, number] => [b.x0 + 1.7 + r(salt) * (b.x1 - b.x0 - 2.4), b.y0 + 1.7 + r(salt + 1) * (b.y1 - b.y0 - 2.4)];
  if (b.kind === 'hall' && !walk && isShed(b.kit)) {
    // A shed's roof light: flat skylights end to end down the middle (the model's corner is its origin).
    const size = 2.5 * SYNTY_SCALE;
    const L = along ? b.x1 - b.x0 : b.y1 - b.y0;
    const n = Math.max(1, Math.floor((L - 3) / size));
    for (let k = 0; k < n; k++) {
      const u = (k - n / 2) * size;
      const x = along ? cx + u : cx - size / 2;
      const z = along ? cz - size / 2 : cz + u;
      out.push({ id: 'skylight', x, y, z, yaw: 0 });
      for (let i = Math.floor(x); i <= Math.floor(x + size); i++) for (let j = Math.floor(z); j <= Math.floor(z + size); j++) taken.add(`${i},${j}`);
    }
  } else if (b.kind === 'hall' && !walk) {
    // Skylights down the middle.
    const n = Math.floor((along ? b.x1 - b.x0 : b.y1 - b.y0) / 3);
    for (let k = 0; k < n; k++) {
      const u = (k - (n - 1) / 2) * 3;
      place('roofSkylight', along ? cx + u : cx, along ? cz : cz + u, along ? 1.4 : 0.9, along ? 0.9 : 1.4, along ? 0 : Math.PI / 2);
    }
  }
  if (b.kind === 'office' && !walk) {
    const [x, z] = spot(2);
    place('roofAccess', x, z, 2.4, 2.4, Math.floor(r(4) * 4) * (Math.PI / 2));
  }
  const area = (b.x1 - b.x0) * (b.y1 - b.y0);
  // A billboard on some shops and offices, at the roof's edge facing the default view (south or east).
  if ((b.kind === 'shop' || b.kind === 'office') && !walk && area >= 30 && r(60) < 0.45) {
    const south = r(61) < 0.5;
    const [bx, bz] = south ? [cx + (r(62) - 0.5) * 2, b.y1 - 0.4] : [b.x1 - 0.4, cz + (r(62) - 0.5) * 2];
    // The frame is long along z and faces x: turned a quarter for the south edge.
    const yaw = south ? -Math.PI / 2 : 0;
    out.push({ id: 'billboard', x: bx, y, z: bz, yaw });
    const off = 0.32;
    out.push({ id: r(63) < 0.5 ? 'billboardSign' : 'billboardSign2', x: bx + (south ? 0 : off), y: y + 0.7, z: bz + (south ? off : 0), yaw });
  }
  // What's been left up there: weeds and moss, rubble, bags, a pallet, a tyre (they take no room); on a
  // roof nobody walks, a crate too (second look against the capture: its roofs are cluttered).
  const clutter: PropId[] = ['tuftLow', 'tuft', 'grassBlob', 'trashBag', 'pallet', 'pebbles', 'tire', 'trashBag2', 'cardboard', 'papers', 'grassBlob', 'pebbles'];
  if (!walk) clutter.push('crate', 'crateOpen');
  const litter = Math.min(16, Math.round(area / 8));
  for (let k = 0; k < litter; k++) {
    const [x, z] = spot(80 + k * 2);
    if (walk && !open?.(Math.floor(x), Math.floor(z))) continue;
    out.push({ id: clutter[Math.floor(r(120 + k) * clutter.length)]!, x, y: y + 0.003, z, yaw: r(140 + k) * Math.PI * 2 });
  }
  if (walk) return out;
  const count = Math.min(6, Math.max(2, Math.round(area / 22)));
  const kinds: PropId[] = ['roofAircon', 'roofAirconLong', 'roofVent', 'roofAircon', 'roofVent', 'roofDish'];
  for (let k = 0, tries = 0; k < count && tries < 24; tries++) {
    const id = kinds[(k + Math.floor(r(5) * 3)) % kinds.length]!;
    const [x, z] = spot(10 + tries * 2);
    const turned = r(30 + tries) < 0.5;
    const big = id === 'roofAirconLong' ? 1.6 : 1;
    if (place(id, Math.round(x) + 0.5, Math.round(z) + 0.5, turned ? 1 : big, turned ? big : 1, (turned ? Math.PI / 2 : 0) + (r(50 + tries) < 0.5 ? Math.PI : 0))) k++;
  }
  return out;
}

/**
 * The pieces a kit draws on a wall tile, by what the tile is. Only the first
 * piece of a list takes the building's colours (siding); a door's `leaf`
 * swings open between two jambs; windows get a pane (`glass`: its width,
 * height and middle's height, in the kit's metres, and how far in front of
 * the wall's middle plane it is); `top` stands on every stretch of wall (wire).
 */
interface Kit {
  wall: PieceId[];
  window: PieceId[];
  door: PieceId[];
  leaf?: PieceId;
  lintel: PieceId[];
  post: PieceId;
  /** The post's length along the wall (a strip of wall) and whether it's square (a corner post, thickened both ways). */
  postAlong: number;
  postSquare: boolean;
  thick: number;
  storey: number;
  glass: [number, number, number, number];
  top?: PieceId;
}

const KITS = {
  house: {
    wall: ['houseWall'],
    window: ['houseWindowWall', 'houseWindow'],
    door: ['houseDoorWall', 'houseDoor'],
    lintel: ['houseLintel'],
    post: 'houseCorner',
    postAlong: 1,
    postSquare: true,
    thick: THICK,
    storey: 2.9,
    glass: [0.89, 1.48, 1.45, -0.03],
  },
  bunker: {
    wall: ['bunkerWall'],
    window: ['bunkerWindow'],
    door: ['bunkerLintel'],
    leaf: 'metalDoor',
    lintel: ['bunkerLintel'],
    post: 'bunkerWall',
    postAlong: 0.3,
    postSquare: false,
    thick: THICK,
    storey: 3,
    glass: [0.93, 1.85, 1.355, 0],
  },
  brick: {
    wall: ['bunkerWall', 'brickFace'],
    window: ['bunkerWindow'],
    door: ['bunkerLintel', 'brickLintel'],
    leaf: 'metalDoor',
    lintel: ['bunkerLintel', 'brickLintel'],
    post: 'bunkerWall',
    postAlong: 0.3,
    postSquare: false,
    thick: THICK,
    storey: 3,
    glass: [0.93, 1.85, 1.355, 0],
  },
  fence: {
    wall: ['fenceWall'],
    window: ['bunkerWindow'],
    door: ['bunkerLintel'],
    leaf: 'metalDoor',
    lintel: ['bunkerLintel'],
    post: 'fenceColumn',
    postAlong: 1,
    postSquare: false,
    thick: 1,
    storey: 3,
    glass: [0.93, 1.85, 1.355, 0],
    top: 'barbedWire',
  },
} as Record<KitId, Kit>;

/** A shed: sheet metal outside a bunker wall (as the warehouses' brick), steel-framed windows, a metal door. */
const shedKit = (sheet: PieceId, lintel: PieceId): Kit => ({ ...KITS.brick, wall: ['bunkerWall', sheet], door: ['bunkerLintel', lintel], lintel: ['bunkerLintel', lintel] });
KITS.shedGrey = shedKit('sheetGrey', 'sheetGreyLintel');
KITS.shedRed = shedKit('sheetRed', 'sheetRedLintel');
KITS.shedBlue = shedKit('sheetBlue', 'sheetBlueLintel');
KITS.medical = { ...KITS.bunker };

/** Walls between rooms: Town's interior walls, whatever the building. */
const BETWEEN: Kit = { ...KITS.house, wall: ['roomWall'], door: ['roomDoorWall'], lintel: ['roomLintel'] };

/** Shop signs and how long each is (tiles): the long ones are scaled down to fit over a door. */
const SIGNS: [PropId, number][] = [
  ['signShop', 1.7],
  ['signFood', 2.2],
  ['signMarket', 2.64],
  ['signDiner', 1.97],
  ['signPets', 3.41],
];

/** The barbed wire's length in tiles (one piece is stretched to a tile). */
const WIRE_LENGTH = 3.94 * KIT;

/**
 * The map's buildings in Synty pieces: walls (rebuilt whenever a tile
 * changes: a door opens, a window breaks), floors and roofs (once per map),
 * and their cutaway.
 */
export class BuildingsView {
  private readonly medical: MedicalFinish;
  readonly plan: BuildingPlan;
  readonly cutaway: Cutaway;
  /** Floors and roofs: they don't change during a mission, but for the slabs a blast goes through (redoUpper). */
  readonly group = new THREE.Group();
  /** Walls, doors and windows as the tiles are now. */
  readonly walls = new THREE.Group();
  private readonly materials: THREE.Material[] = [];
  private readonly geometries: THREE.BufferGeometry[] = [];
  /** The walls' merged meshes (batch.ts) and the geometries they own. */
  private wallParts: { geometries: THREE.BufferGeometry[] } = { geometries: [] };
  private readonly pieceMaterial: THREE.MeshStandardMaterial;
  /** The same for what stands on roofs from other packs' atlases. */
  private readonly atlasMaterials = new Map<AtlasId, THREE.MeshStandardMaterial>();
  private readonly glassMaterial: THREE.MeshStandardMaterial;
  /** Parapets on raised blocks: rebuilt with the walls (they can be blown away). */
  private readonly blockMaterial: THREE.MeshStandardMaterial;
  /** Each kit's window pane. */
  private readonly glassGeometry = new Map<KitId, THREE.BufferGeometry>();
  /** What darkens the ground round it, merged (walls, raised blocks, parapets, decks): for the bake (groundAO.ts). */
  private staticOccluders: THREE.BufferGeometry[] = [];
  private wallOccluders: THREE.BufferGeometry[] = [];
  /** The walls' pieces, merged per chunk and kept between rebuilds: a change merges its chunk again (batch.ts). */
  private readonly merged = new StaticBatch(8);
  /** The doors' leaves, out of the merge so they swing (doors.ts). */
  private readonly doors = new DoorLeaves();
  /** Where the walls changed at the last buildWalls (null: nowhere), for the contact occlusion. */
  dirty: Dirty | null = null;
  /** What the parapets were at the last buildWalls (they're few: drawn whole each time). */
  private parapetSignature = 0;
  /** The storeys' floors and the roof decks one walks on, made again when a slab is blown through (redoUpper). */
  private upper: { storeys: THREE.Mesh | null; decks: THREE.Mesh | null } = { storeys: null, decks: null };
  /** The holes in the slabs when they were last made. */
  private holesMade = 0;

  constructor(
    s: GameState,
    private readonly fog: FogOfWar,
    private readonly ao: GroundOcclusion,
    private readonly snow: SnowCover | null = null,
  ) {
    this.plan = new BuildingPlan(s);
    this.cutaway = new Cutaway(this.plan);
    this.medical = new MedicalFinish(s, this.plan);
    // Every packed pack's pieces in one material (mapAtlas.ts): a draw for all of them.
    this.pieceMaterial = this.own(new THREE.MeshStandardMaterial({ map: mapAtlasTexture(), roughness: 0.85, side: THREE.DoubleSide }));
    wallCutPatch(this.pieceMaterial, this.cutaway);
    this.medical.patch(this.pieceMaterial, 'wall');
    this.blockMaterial = new THREE.MeshStandardMaterial({ map: mapAtlasTexture(), vertexColors: true, roughness: 0.9 });
    swatchPatch(this.blockMaterial);
    this.own(this.blockMaterial);
    this.glassMaterial = this.own(
      new THREE.MeshStandardMaterial({ color: 0x9fc4d8, transparent: true, opacity: 0.38, roughness: 0.08, metalness: 0.3, depthWrite: false, side: THREE.DoubleSide, emissive: new THREE.Color(0x203038) }),
    );
    wallCutPatch(this.glassMaterial, this.cutaway);
    // By night some windows are lit (aGlow per pane), warm and nearly opaque.
    extendShader(this.glassMaterial, 'night-windows', (shader) => {
      shader.uniforms.uNight = NIGHT;
      shader.vertexShader = shader.vertexShader
        .replace('#include <common>', '#include <common>\nattribute float aGlow;\nvarying float vGlow;')
        .replace('#include <uv_vertex>', '#include <uv_vertex>\nvGlow = aGlow;');
      shader.fragmentShader = shader.fragmentShader
        .replace('#include <common>', '#include <common>\nuniform float uNight;\nvarying float vGlow;')
        .replace(
          '#include <emissivemap_fragment>',
          '#include <emissivemap_fragment>\ntotalEmissiveRadiance += vec3(1.0, 0.6, 0.26) * 2.4 * vGlow * uNight;\ndiffuseColor.a = mix(diffuseColor.a, 0.92, vGlow * uNight);',
        );
    });
    // Each kit's pane: as wide and tall as its window's opening, at the same stretch as its walls.
    for (const [id, kit] of Object.entries(KITS) as [KitId, Kit][]) {
      const [gw, gh, gy, gz] = kit.glass;
      const fit = STOREY / (kit.storey * KIT);
      const g = new THREE.PlaneGeometry(gw * KIT, gh * KIT * fit).translate(0, gy * KIT * fit, gz * KIT * kit.thick);
      this.glassGeometry.set(id, g);
      this.geometries.push(g);
    }
    this.buildStatic(s);
    this.snow?.shelterBuildings(this.plan);
    this.cutaway.measure();
  }

  private own<T extends THREE.Material>(m: T): T {
    if (!(m instanceof THREE.MeshStandardMaterial) || !m.transparent) this.snow?.patch(m);
    this.fog.patch(m);
    this.materials.push(m);
    return m;
  }

  /** Floors and roofs. */
  private buildStatic(s: GameState): void {
    const floors = new Surfaces();
    const roofs = new Surfaces();
    // Real storeys (GameState.levels): their floors and the stairs, which fade with the floors above the one in view;
    // the decks of roofs one can walk on, weathered like any roof.
    const storeys = new Surfaces();
    const decks = new Surfaces();
    for (const b of this.plan.buildings) {
      if (b.kind === 'ruin') continue;
      const looks = looksOf(b);
      // The floor runs to the walls' middle lines (the outer half of a wall tile is the street's).
      const [x0, x1, z0, z1, y] = [b.x0 + 0.5, b.x1 + 0.5, b.y0 + 0.5, b.y1 + 0.5, b.base + 0.004];
      floors.poly([V(x0, y, z0), V(x0, y, z1), V(x1, y, z1), V(x1, y, z0)], [[x0, z0], [x0, z1], [x1, z1], [x1, z0]], looks.floorSwatch, looks.floor, -1);
      if (b.leveled) this.storeyFloors(b, storeys, looks);
      roofs.level = b.storeys;
      if (b.walkableRoof) b.top = this.roofDeck(b, decks, looks);
      else if (b.kind === 'house') b.top = gableRoof(roofs, b, PITCH, looks.shingles, looks.outer);
      else if (b.kind === 'barn') b.top = gableRoof(roofs, b, HALL_PITCH * 1.8, looks.shingles, looks.outer);
      else b.top = flatRoof(roofs, b, looks.tar, looks.trim);
    }
    this.stairs(s, storeys);
    const solid = new Surfaces();
    const raised = new Surfaces();
    this.raisedSurfaces(s, raised, solid);
    const make = (surfaces: Surfaces, roof: boolean, shadows = roof, weathered = roof, occludes = false, fades = roof): THREE.Mesh | null => {
      if (surfaces.empty) return null;
      const material = new THREE.MeshStandardMaterial({ map: mapAtlasTexture(), vertexColors: true, roughness: roof ? 0.8 : 0.9 });
      swatchPatch(material);
      this.medical.patch(material, 'floor');
      if (weathered) weatherPatch(material);
      if (fades) roofFadePatch(material, this.cutaway);
      // Floors and decks sit in the occlusion of what stands round them.
      else contactPatch(material, this.ao);
      this.own(material);
      const geometry = surfaces.geometry();
      this.geometries.push(geometry);
      if (occludes) this.staticOccluders.push(geometry);
      const mesh = new THREE.Mesh(geometry, material);
      mesh.receiveShadow = true;
      mesh.castShadow = shadows;
      mesh.matrixAutoUpdate = false;
      this.group.add(mesh);
      return mesh;
    };
    make(floors, false);
    make(roofs, true);
    make(raised, false, false, true, true);
    make(solid, false, true, false, true);
    this.upper = { storeys: make(storeys, false, true, false, false, true), decks: make(decks, true) };
  }

  /**
   * The storeys' floors (with their stairs) and the decks of roofs one walks
   * on, made again as the slabs are now: a slab blown through leaves its
   * hole (docs/FASE7.md §5). Their meshes and materials stay; only the
   * geometry is new.
   */
  private redoUpper(s: GameState): void {
    const storeys = new Surfaces();
    const decks = new Surfaces();
    for (const b of this.plan.buildings) {
      if (b.kind === 'ruin') continue;
      const looks = looksOf(b);
      if (b.leveled) this.storeyFloors(b, storeys, looks);
      if (b.walkableRoof) this.roofDeck(b, decks, looks);
    }
    this.stairs(s, storeys);
    for (const [mesh, surfaces] of [[this.upper.storeys, storeys], [this.upper.decks, decks]] as const) {
      if (!mesh) continue;
      const old = mesh.geometry;
      mesh.geometry = surfaces.geometry();
      this.geometries[this.geometries.indexOf(old)] = mesh.geometry;
      old.dispose();
    }
  }

  /** How many slabs have been blown through, at the last look (BuildingPlan.holed). */
  private holes(s: GameState): number {
    let n = 0;
    for (let k = 1; k <= (s.levels?.length ?? 0); k++) for (let i = 0; i < s.width * s.height; i++) if (this.plan.holed(i, k)) n++;
    return n;
  }

  /**
   * The floors of a building's storeys below its top, over every tile that
   * has a slab (not over a stairwell), to the walls' middle lines like the
   * ground floor's.
   */
  private storeyFloors(b: Building, out: Surfaces, looks: ReturnType<typeof looksOf>): void {
    const [x0, x1, z0, z1] = [b.x0 + 0.5, b.x1 + 0.5, b.y0 + 0.5, b.y1 + 0.5];
    for (let k = 1; k < b.storeys; k++) {
      out.level = k;
      const y = b.base + k * STOREY + 0.004;
      for (let ty = b.y0; ty <= b.y1; ty++) {
        for (let tx = b.x0; tx <= b.x1; tx++) {
          const kind = this.plan.kindAt(tx, ty, k);
          if (kind === null || kind === 'roof') continue;
          const [ax, bx, az, bz] = [Math.max(tx, x0), Math.min(tx + 1, x1), Math.max(ty, z0), Math.min(ty + 1, z1)];
          if (bx <= ax || bz <= az) continue;
          out.poly([V(ax, y, az), V(ax, y, bz), V(bx, y, bz), V(bx, y, az)], [[ax, az], [ax, bz], [bx, bz], [bx, az]], looks.floorSwatch, looks.floor, b.index);
        }
      }
    }
  }

  /**
   * A roof one can walk on (its top storey has floor): a deck of tar out to
   * the walls' outer faces, open where stairs come up through it, with a
   * parapet along the edge wherever the rules put low cover there. Returns
   * the height of its top.
   */
  private roofDeck(b: Building, out: Surfaces, looks: ReturnType<typeof looksOf>): number {
    const k = b.storeys;
    out.level = k;
    const y = b.base + k * STOREY + 0.04;
    const [x0, x1, z0, z1] = [b.x0 + 0.5 - WALL_HALF - 0.02, b.x1 + 0.5 + WALL_HALF + 0.02, b.y0 + 0.5 - WALL_HALF - 0.02, b.y1 + 0.5 + WALL_HALF + 0.02];
    const lip = looks.trim.clone().multiplyScalar(0.9);
    for (let ty = b.y0; ty <= b.y1; ty++) {
      for (let tx = b.x0; tx <= b.x1; tx++) {
        const kind = this.plan.kindAt(tx, ty, k);
        if (kind === null) continue;
        const [ax, bx, az, bz] = [Math.max(tx, x0), Math.min(tx + 1, x1), Math.max(ty, z0), Math.min(ty + 1, z1)];
        out.poly([V(ax, y, az), V(ax, y, bz), V(bx, y, bz), V(bx, y, az)], [[ax, az], [ax, bz], [bx, bz], [bx, az]], SWATCH.flat, looks.tar, b.index);
        // The parapet: on the deck's edge, along each side of this tile that is the roof's edge.
        if (kind !== 'low') continue;
        const t = PARAPET_THICK;
        const top = y + PARAPET;
        if (ty === b.y0) out.box(V(ax, y - 0.12, z0), V(bx, top, z0 + t), SWATCH.flat, lip, b.index);
        if (ty === b.y1) out.box(V(ax, y - 0.12, z1 - t), V(bx, top, z1), SWATCH.flat, lip, b.index);
        if (tx === b.x0) out.box(V(x0, y - 0.12, az), V(x0 + t, top, bz), SWATCH.flat, lip, b.index);
        if (tx === b.x1) out.box(V(x1 - t, y - 0.12, az), V(x1, top, bz), SWATCH.flat, lip, b.index);
      }
    }
    // The deck's edge, seen from the street: a band over the walls' tops.
    out.box(V(x0, y - 0.14, z0), V(x1, y + 0.02, z0 + 0.04), SWATCH.flat, lip, b.index);
    out.box(V(x0, y - 0.14, z1 - 0.04), V(x1, y + 0.02, z1), SWATCH.flat, lip, b.index);
    out.box(V(x0, y - 0.14, z0), V(x0 + 0.04, y + 0.02, z1), SWATCH.flat, lip, b.index);
    out.box(V(x1 - 0.04, y - 0.14, z0), V(x1, y + 0.02, z1), SWATCH.flat, lip, b.index);
    return y + PARAPET;
  }

  /**
   * A building's stairs (a ladder tile whose decor is 'stairs', facing the
   * way it climbs): a flight of steps across the tile, from its back
   * edge up to the landing a storey higher on the tile it faces. The rules
   * let one on and off them only by that landing (docs/FASE7.md), so they
   * have a banister on each side of the flight and a railing round the
   * stairwell above, open only to the landing; none against a wall.
   */
  private stairs(s: GameState, out: Surfaces): void {
    const { width: w, height: h } = s;
    const STEPS = 9;
    const half = 0.4;
    const tilesOf = (k: number) => (k === 0 ? s.tiles : s.levels?.[k - 1]?.tiles);
    /** A wall (or a door, a window) on storey k at (x, y): no railing needed against it. */
    const walled = (x: number, y: number, k: number) => {
      const t = x < 0 || y < 0 || x >= w || y >= h ? 'wall' : tilesOf(k)?.[y * w + x];
      return t === 'wall' || t === 'window' || t === 'windowBroken' || t === 'door' || t === 'doorOpen';
    };
    for (let k = 0; k <= (s.levels?.length ?? 0); k++) {
      const tiles = k ? s.levels![k - 1]!.tiles : s.tiles;
      const decor = k ? s.levels![k - 1]!.decor : s.decor;
      if (!decor) continue;
      for (let i = 0; i < w * h; i++) {
        const look = tiles[i] === 'ladder' ? decorOf(decor[i]) : null;
        if (look?.kind !== 'stairs') continue;
        const x = i % w;
        const y = (i - x) / w;
        const b = this.plan.owner[i]!;
        const bd = b >= 0 ? this.plan.buildings[b] : undefined;
        const base = (bd?.base ?? 0) + k * STOREY;
        const d = DIRS4[look.facing]!;
        const wood = bd?.kind === 'medical' ? new THREE.Color(0x79888b) : bd ? STAIR_WOOD : STEEL;
        out.level = k;
        for (let n = 0; n < STEPS; n++) {
          // Along the climb, from the tile's back edge (-0.5) to its front (+0.5).
          const u0 = -0.5 + n / STEPS;
          const u1 = -0.5 + (n + 1) / STEPS;
          const top = base + ((n + 1) / STEPS) * STOREY;
          const [ax, bx] = d.x !== 0 ? [x + 0.5 + Math.min(u0 * d.x, u1 * d.x), x + 0.5 + Math.max(u0 * d.x, u1 * d.x)] : [x + 0.5 - half, x + 0.5 + half];
          const [az, bz] = d.y !== 0 ? [y + 0.5 + Math.min(u0 * d.y, u1 * d.y), y + 0.5 + Math.max(u0 * d.y, u1 * d.y)] : [y + 0.5 - half, y + 0.5 + half];
          // Wooden treads are horizontal boards. Filling every step down
          // to the ground made the back of the flight look like a vertical floor.
          out.box(V(ax, bd ? top - 0.055 : base, az), V(bx, top, bz), SWATCH.flat, n % 2 ? wood : wood.clone().multiplyScalar(0.92), b, !!bd);
        }
        if (bd) this.stairStringers(out, x, y, d, base, wood.clone().multiplyScalar(0.72), b);
        this.banisters(out, x, y, k, d, base, STEPS, wood.clone().multiplyScalar(1.25), b, walled);
      }
    }
  }

  /** Two sloping wooden beams carry the horizontal treads, leaving the risers open. */
  private stairStringers(out: Surfaces, x: number, y: number, d: Vec2, base: number, tint: THREE.Color, b: number): void {
    const at = (u: number, v: number, h: number) => V(x + 0.5 + d.x * u - d.y * v, h, y + 0.5 + d.y * u + d.x * v);
    const up = V(0, 1, 0);
    const across = V(-d.y, 0, d.x);
    const along = V(d.x, 0, d.y);
    const h0 = base - 0.055;
    const h1 = base + STOREY - 0.055;
    for (const v of [-0.32, 0.32]) {
      const a = at(-0.5, v - 0.04, h0);
      const b0 = at(-0.5, v + 0.04, h0);
      const c = at(0.5, v + 0.04, h1);
      const d0 = at(0.5, v - 0.04, h1);
      const lower = (p: THREE.Vector3) => p.clone().addScaledVector(up, -0.16);
      const faces: [THREE.Vector3[], THREE.Vector3][] = [
        [[a, b0, c, d0], up],
        [[lower(a), lower(d0), lower(c), lower(b0)], up.clone().negate()],
        [[b0, lower(b0), lower(c), c], across],
        [[a, d0, lower(d0), lower(a)], across.clone().negate()],
        [[a, lower(a), lower(b0), b0], along.clone().negate()],
        [[d0, c, lower(c), lower(d0)], along],
      ];
      for (const [corners, facing] of faces) out.poly(corners, corners.map((p) => [p.x + p.z, p.y] as [number, number]), SWATCH.flat, tint, b, facing);
    }
  }

  /**
   * The railings of the stairs on tile (x, y) of storey k, climbing towards
   * `d` from `base`: along each side of the flight a handrail stepping up
   * with the treads, on balusters; round the stairwell a storey up, on each
   * of its edges but the landing's, a rail on posts at the floor's height.
   * Boxes in the stairs' own surfaces: no draws of their own.
   */
  private banisters(out: Surfaces, x: number, y: number, k: number, d: { x: number; y: number }, base: number, steps: number, tint: THREE.Color, b: number, walled: (x: number, y: number, k: number) => boolean): void {
    const RAIL = 0.9;
    const T = 0.035;
    const cx = x + 0.5;
    const cz = y + 0.5;
    /** A box from (along, across, height) to (along, across, height), in the stairs' own axes: `along` up the climb, `across` to its left. */
    const box = (u0: number, v0: number, h0: number, u1: number, v1: number, h1: number) => {
      const ax = cx + d.x * u0 - d.y * v0;
      const az = cz + d.y * u0 + d.x * v0;
      const bx = cx + d.x * u1 - d.y * v1;
      const bz = cz + d.y * u1 + d.x * v1;
      out.box(V(Math.min(ax, bx), h0, Math.min(az, bz)), V(Math.max(ax, bx), h1, Math.max(az, bz)), SWATCH.flat, tint, b);
    };
    /** A point in the stairs' axes. */
    const at = (u: number, v: number, h: number) => V(cx + d.x * u - d.y * v, h, cz + d.y * u + d.x * v);
    const across = V(-d.y, 0, d.x);
    const up = V(0, 1, 0);
    /** The handrail's height over the flight at `u`, parallel to its slope. */
    const rail = (u: number) => base + (u + 0.5) * STOREY + RAIL;
    out.level = k;
    for (const side of [1, -1]) {
      // Against a wall on that side, none.
      if (walled(x - d.y * side, y + d.x * side, k)) continue;
      const v = side * 0.44;
      // The handrail: one bar along the slope, from the foot to the landing.
      const [a, z] = [-0.5, 0.5];
      const [ha, hz] = [rail(a), rail(z)];
      const faces: [THREE.Vector3[], THREE.Vector3][] = [
        [[at(a, v - T, ha), at(a, v + T, ha), at(z, v + T, hz), at(z, v - T, hz)], up],
        [[at(a, v - T, ha - 0.05), at(z, v - T, hz - 0.05), at(z, v + T, hz - 0.05), at(a, v + T, ha - 0.05)], up.clone().negate()],
        [[at(a, v + T, ha - 0.05), at(z, v + T, hz - 0.05), at(z, v + T, hz), at(a, v + T, ha)], across],
        [[at(a, v - T, ha - 0.05), at(a, v - T, ha), at(z, v - T, hz), at(z, v - T, hz - 0.05)], across.clone().negate()],
      ];
      for (const [corners, facing] of faces) out.poly(corners, corners.map((c) => [c.x + c.z, c.y] as [number, number]), SWATCH.flat, tint, b, facing);
      // Balusters on every third tread, and a newel post at the foot.
      for (let n = 1; n < steps; n += 3) {
        const u = -0.5 + (n + 0.5) / steps;
        box(u - T, v - T, base + ((n + 1) / steps) * STOREY, u + T, v + T, rail(u) - 0.03);
      }
      box(-0.5, v - T, base, -0.5 + 2 * T, v + T, ha + 0.04);
    }
    // The stairwell above, open to the landing (the tile the stairs face).
    out.level = k + 1;
    const floor = base + STOREY;
    for (const [du, dv] of [[-1, 0], [0, 1], [0, -1]] as const) {
      if (walled(x + d.x * du - d.y * dv, y + d.y * du + d.x * dv, k + 1)) continue;
      // The edge's two ends, in (along, across), and a post at each.
      const [ua, va, ub, vb] = du ? [-0.5, -0.5, -0.5 + 2 * T, 0.5] : [-0.5, dv * 0.5 - T, 0.5, dv * 0.5 + T];
      box(ua, va, floor + RAIL - 0.05, ub, vb, floor + RAIL);
      box(ua, va, floor + 0.42, ub, vb, floor + 0.46);
      const posts: [number, number][] = du ? [[-0.5, -0.5], [-0.5, 0.5]] : [[-0.5, dv * 0.5], [0.5, dv * 0.5]];
      for (const [u, vv] of posts) box(u - T, vv - T, floor, u + T, vv + T, floor + RAIL);
    }
  }

  /**
   * The walkable tops of flats (tar) and platforms (a steel deck), posts at
   * their outer corners (brick, steel), and the top of solid wall masses.
   */
  private raisedSurfaces(s: GameState, tops: Surfaces, solid: Surfaces): void {
    const { width: w, height: h } = s;
    const plan = this.plan;
    const elev = (x: number, y: number) => (x < 0 || y < 0 || x >= w || y >= h ? 0 : (s.elev[y * w + x] ?? 0));
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const i = y * w + x;
        if (plan.massive[i]) {
          const top = (elev(x, y) + 1) * STOREY;
          tops.poly([V(x, top, y), V(x, top, y + 1), V(x + 1, top, y + 1), V(x + 1, top, y)], [[x, y], [x, y + 1], [x + 1, y + 1], [x + 1, y]], SWATCH.flat, CONCRETE, -1);
        }
        const style = plan.raised[i];
        if (style !== RAISED_FLATS && style !== RAISED_PLATFORM) continue;
        const e = elev(x, y);
        const top = e * STOREY + 0.003;
        const deck = style === RAISED_FLATS ? TAR : DECK;
        tops.poly([V(x, top, y), V(x, top, y + 1), V(x + 1, top, y + 1), V(x + 1, top, y)], [[x, y], [x, y + 1], [x + 1, y + 1], [x + 1, y]], SWATCH.flat, deck, -1);
        // Outer corners: two lower sides that meet.
        for (let k = 0; k < 4; k++) {
          const a = DIRS4[k]!;
          const b = DIRS4[(k + 1) % 4]!;
          if (elev(x + a.x, y + a.y) >= e || elev(x + b.x, y + b.y) >= e) continue;
          const low = Math.max(elev(x + a.x, y + a.y), elev(x + b.x, y + b.y));
          const cx = x + 0.5 + (a.x + b.x) * 0.5;
          const cz = y + 0.5 + (a.y + b.y) * 0.5;
          const r = style === RAISED_FLATS ? 0.09 : 0.06;
          solid.box(V(cx - r, low * STOREY, cz - r), V(cx + r, e * STOREY + 0.08, cz + r), SWATCH.flat, style === RAISED_FLATS ? BRICK : STEEL, -1);
        }
      }
    }
  }

  /** Walls, doors and windows for the tiles as they are; false if some pieces are still loading. */
  /**
   * Walls, doors and windows for the tiles as they are, merged per chunk
   * (`redo`: only those chunks, from the tiles marked in `area`; null: all of
   * them); false if some pieces are still loading.
   */
  buildWalls(s: GameState, redo: Set<string> | null = null, area: Uint8Array | null = null): boolean {
    // A slab blown through since: the storeys' floors again, with its hole (rebuilt with the walls, as a blast is).
    const holes = this.holes(s);
    if (holes !== this.holesMade) {
      this.holesMade = holes;
      this.redoUpper(s);
    }
    this.merged.begin(redo);
    this.doors.begin(area);
    for (const g of this.wallParts.geometries) g.dispose();
    this.wallParts = { geometries: [] };
    this.wallOccluders = [];
    this.walls.clear();
    this.dirty = null;
    const { width: w, height: h } = s;
    const plan = this.plan;
    const batches = new Map<string, Batch>();
    let ready = true;
    const glass = new Map<KitId, { matrices: THREE.Matrix4[]; cut: number[]; level: number[] }>();
    const palettes = new Map<number, ReturnType<typeof looksOf>>();
    /** The storey whose walls are being laid (0 the ground), and its tiles. */
    let level = 0;
    /** The tile being laid, and how many door leaves it has put so far (doors.ts keys them so). */
    let tile = -1;
    let leaves = 0;
    let layer: readonly (TileKind | null)[] = s.tiles;
    const at = (x: number, y: number): TileKind | undefined => (x < 0 || y < 0 || x >= w || y >= h ? undefined : (layer[y * w + x] ?? undefined));
    /** Wall-like, or a wall blown down (it still joins the walls beside it). */
    // A siege's watchtower carries the wall on round its base (below): the wall runs into it, without a column at its end.
    const wallish = (x: number, y: number) =>
      isWallLike(at(x, y)) || (x >= 0 && y >= 0 && x < w && y < h && (plan.breached(y * w + x, level) || (!level && plan.raised[y * w + x] === RAISED_TOWER)));
    const m = new THREE.Matrix4();
    const q = new THREE.Quaternion();
    const up = new THREE.Vector3(0, 1, 0);
    const pos = new THREE.Vector3();
    const scale = new THREE.Vector3();
    const thicken = new THREE.Matrix4();

    /**
     * One piece at (x, z), its outer face towards `out`, `along` its length in
     * tiles, `thick` its thickness against the kit's, `square` thickened both
     * ways (a corner post); `tint` with the building's colours, `open` a door
     * leaf swung open. Building b + 1 and the outer face go to the cutaway
     * (`between`: a wall between rooms, both of its faces inside).
     */
    const put = (
      id: PieceId,
      x: number,
      z: number,
      base: number,
      out: Vec2,
      between: boolean,
      b: number,
      o: { along?: number; thick?: number; square?: boolean; tint?: boolean; open?: boolean; roof?: boolean; tall?: number } = {},
    ) => {
      const parts = pieceNow(id);
      if (!parts) {
        ready = false;
        return;
      }
      const building = b >= 0 ? plan.buildings[b] : undefined;
      let looks = o.tint && building ? palettes.get(b) : null;
      if (o.tint && building && !looks) palettes.set(b, (looks = looksOf(building)));
      const outer = looks?.outer ?? WHITE;
      const inner = looks?.inner ?? WHITE;
      const thick = o.thick ?? THICK;
      q.setFromAxisAngle(up, Math.atan2(out.x, out.y));
      pos.set(x, base, z);
      scale.set(o.along ?? 1, o.tall ?? 1, 1);
      m.compose(pos, q, scale);
      thicken.makeScale(o.square ? thick : 1, 1, thick);
      for (const part of parts) {
        const key = `${id}:${part.name}`;
        const local = thicken.clone().multiply(part.matrix);
        if (part.leaf) {
          // A door's leaf swings (doors.ts): an instance of its own, shut, or open a quarter turn into the building.
          this.doors.put(`${level}:${tile}:${leaves++}`, tile, {
            kind: key,
            geometry: part.geometry,
            material: this.materialFor(part.atlas),
            uv: atlasRect(part.atlas) ?? undefined,
            piece: m.clone(),
            local,
            hinge: new THREE.Vector3(part.hinge.x, 0, part.hinge.z * thick),
            far: !!part.far,
            consts: {
              aCut: o.roof ? [b + 1, ON_ROOF, ON_ROOF, base] : [b + 1, between ? 0 : out.x, between ? 0 : out.y, base],
              aLevel: [storeyOf(building, base)],
              aOuter: [outer.r, outer.g, outer.b],
              aInner: [inner.r, inner.g, inner.b],
              aAtlas: [atlasIndex(part.atlas)],
            },
            open: !!o.open,
          });
          continue;
        }
        let batch = batches.get(key);
        if (!batch) batches.set(key, (batch = { geometry: part.geometry, atlas: part.atlas, matrices: [], cut: [], outer: [], inner: [] }));
        batch.matrices.push(m.clone().multiply(local));
        // A façade's upper storey goes with the roof in the cutaway; the walls of the storey in view drop as walls.
        if (o.roof) batch.cut.push(b + 1, ON_ROOF, ON_ROOF, base);
        else batch.cut.push(b + 1, between ? 0 : out.x, between ? 0 : out.y, base);
        batch.outer.push(outer.r, outer.g, outer.b);
        batch.inner.push(inner.r, inner.g, inner.b);
        (batch.level ??= []).push(storeyOf(building, base));
      }
    };
    /** A list of pieces: the first one in the building's colours. */
    const putAll = (ids: PieceId[], x: number, z: number, base: number, out: Vec2, between: boolean, b: number, kit: Kit, open = false, roof = false) =>
      ids.forEach((id, k) => put(id, x, z, base, out, between, b, { thick: kit.thick, tint: k === 0, open, roof }));
    const post = (kit: Kit, x: number, z: number, base: number, out: Vec2, between: boolean, b: number, roof = false) =>
      put(kit.post, x, z, base, out, between, b, { along: kit.postAlong, thick: kit.thick, square: kit.postSquare, roof });
    /** A window pane at (x, z), `base` its storey's floor (upper storeys' go with the roof). */
    const pane = (kitId: KitId, x: number, z: number, base: number, out: Vec2, between: boolean, b: number, roof: boolean) => {
      let p = glass.get(kitId);
      if (!p) glass.set(kitId, (p = { matrices: [], cut: [], level: [] }));
      p.level.push(storeyOf(plan.buildings[b], base));
      q.setFromAxisAngle(up, Math.atan2(out.x, out.y));
      p.matrices.push(new THREE.Matrix4().compose(new THREE.Vector3(x, base, z), q.clone(), new THREE.Vector3(1, 1, 1)));
      if (roof) p.cut.push(b + 1, ON_ROOF, ON_ROOF, base);
      else p.cut.push(b + 1, between ? 0 : out.x, between ? 0 : out.y, base);
    };
    /**
     * The storeys above the ground floor of a stretch of wall (`along` its
     * length, centred on (x, z)): the building's own outer walls up to its
     * roof, windows over doors and windows (and every other tile on shops and
     * offices); over a wall shared with a lower neighbour, what stands above
     * the neighbour's roof, facing it.
     */
    const storeys = (ground: TileKind, x: number, z: number, t: number, out: Vec2, between: boolean, b: number, other: number, along = 1) => {
      const bd = plan.buildings[b];
      // Real storeys are laid from their own tiles.
      if (!bd || bd.leveled || (other >= 0 && plan.buildings[other]!.leveled)) return;
      const kit = KITS[bd.kit];
      let from = 1;
      let to = between ? 1 : bd.storeys;
      let owner = b;
      let face = out;
      if (other >= 0) {
        // A party wall: only what one of the two stands above the other.
        const them = plan.buildings[other]!.storeys;
        from = Math.min(bd.storeys, them);
        to = Math.max(bd.storeys, them);
        owner = bd.storeys >= them ? b : other;
        face = bd.storeys >= them ? out : { x: -out.x, y: -out.y };
      }
      const ob = plan.buildings[owner]!;
      for (let k = from; k < to; k++) {
        const y0 = ob.base + k * STOREY;
        const glazed = other < 0 && (ground !== 'wall' || (ob.kind !== 'house' && ob.kind !== 'motel' && (t + k) % 2 === 0));
        if (along < 1) {
          kit.wall.forEach((id, n) => put(id, x, z, y0, face, false, owner, { along, thick: kit.thick, tint: n === 0, roof: true }));
          continue;
        }
        putAll(glazed ? kit.window : kit.wall, x, z, y0, face, false, owner, kit, false, true);
        if (glazed && !kit.top) pane(ob.kit, x, z, y0, face, false, owner, true);
      }
    };
    /** Each building's first door on its outline (in tile order): where its sign goes. */
    const signDoor = new Map<number, number>();
    for (let i = 0; i < w * h; i++) {
      const b = plan.owner[i]!;
      const t = s.tiles[i];
      if (b < 0 || signDoor.has(b) || (t !== 'door' && t !== 'doorOpen')) continue;
      const bd = plan.buildings[b]!;
      const x = i % w;
      const y = (i - x) / w;
      if (x === bd.x0 || x === bd.x1 || y === bd.y0 || y === bd.y1) signDoor.set(b, i);
    }
    /**
     * Where a shed's roller shutters go (#25): the middle of the longest
     * stretch of plain wall, four tiles or more, on each of two of its sides;
     * how wide each is.
     */
    const shutters = new Map<number, number>();
    for (const bd of plan.buildings) {
      if (!isShed(bd.kit) || bd.base > 0) continue;
      const runs: { tile: number; length: number; side: number }[] = [];
      const sides: [number, number, number, number, number][] = [
        [bd.x0 + 1, bd.y0, 1, 0, bd.x1 - bd.x0 - 1],
        [bd.x0 + 1, bd.y1, 1, 0, bd.x1 - bd.x0 - 1],
        [bd.x0, bd.y0 + 1, 0, 1, bd.y1 - bd.y0 - 1],
        [bd.x1, bd.y0 + 1, 0, 1, bd.y1 - bd.y0 - 1],
      ];
      sides.forEach(([x0, y0, dx, dy, n], side) => {
        let start = -1;
        for (let k = 0; k <= n; k++) {
          const i = (y0 + dy * k) * w + x0 + dx * k;
          const plain = k < n && s.tiles[i] === 'wall' && plan.owner[i] === bd.index;
          if (plain && start < 0) start = k;
          if (!plain && start >= 0) {
            const mid = Math.floor((start + k - 1) / 2);
            runs.push({ tile: (y0 + dy * mid) * w + x0 + dx * mid, length: k - start, side });
            start = -1;
          }
        }
      });
      runs.sort((a, b) => b.length - a.length);
      const sided = new Set<number>();
      for (const r of runs) {
        if (r.length < 4 || sided.has(r.side) || sided.size >= 2) continue;
        sided.add(r.side);
        shutters.set(r.tile, Math.min(3, r.length - 1.4));
      }
    }
    /**
     * A roller shutter `width` tiles wide on a shed's wall, shut, three
     * quarters of a storey tall, on its outer face; it drops with the wall in
     * the cutaway. (The model is 6.84 m wide from x = 0.35, 4.19 m tall, its
     * face to +z; syntyProps stands it on the floor.)
     */
    const shutter = (bd: Building, x: number, z: number, base: number, out: Vec2, width: number) => {
      const parts = syntyProp('rollerShutter');
      if (!parts) {
        ready = false;
        return;
      }
      const sx = width / (6.84 * SYNTY_SCALE);
      const sy = (0.75 * STOREY) / (4.19 * SYNTY_SCALE);
      const off = WALL_HALF + 0.02;
      // Its width along the wall, its face out: the model's x along, its +z the way out.
      const yaw = Math.atan2(out.x, out.y);
      q.setFromAxisAngle(up, yaw);
      const alongX = Math.cos(yaw);
      const alongZ = -Math.sin(yaw);
      const left = (3.77 * SYNTY_SCALE) * sx;
      m.compose(pos.set(x + out.x * off - alongX * left, base, z + out.y * off - alongZ * left), q, scale.set(sx, sy, 0.6));
      parts.forEach((part, k) => {
        const key = `shutter:${k}`;
        let batch = batches.get(key);
        if (!batch) batches.set(key, (batch = { geometry: part.geometry, atlas: part.atlas, matrices: [], cut: [], outer: [], inner: [] }));
        batch.matrices.push(m.clone().multiply(part.matrix));
        batch.cut.push(bd.index + 1, out.x, out.y, base);
        batch.outer.push(1, 1, 1);
        batch.inner.push(1, 1, 1);
      });
    };
    /** A sign over a door, on the wall's outer face, its face out; it drops with the wall in the cutaway. */
    const sign = (bd: Building, x: number, z: number, base: number, out: Vec2) => {
      const [id, length] = SIGNS[Math.floor(hash(bd.x0, bd.y0, 70) * SIGNS.length)]!;
      const parts = syntyProp(id);
      if (!parts) {
        ready = false;
        return;
      }
      const size = Math.min(1, 2.2 / length);
      const off = WALL_HALF + 0.05;
      m.compose(pos.set(x + out.x * off, base + 1.5, z + out.y * off), q.setFromAxisAngle(up, Math.atan2(-out.y, out.x)), scale.set(size, size, size));
      parts.forEach((part, k) => {
        const key = `sign:${id}:${k}`;
        let batch = batches.get(key);
        if (!batch) batches.set(key, (batch = { geometry: part.geometry, atlas: part.atlas, matrices: [], cut: [], outer: [], inner: [] }));
        batch.matrices.push(m.clone().multiply(part.matrix));
        batch.cut.push(bd.index + 1, out.x, out.y, base);
        batch.outer.push(1, 1, 1);
        batch.inner.push(1, 1, 1);
      });
    };

    // The ground's walls, then each storey's (GameState.levels).
    for (level = 0; level <= (s.levels?.length ?? 0); level++) {
    layer = level ? s.levels![level - 1]!.tiles : s.tiles;
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const i = y * w + x;
        if (area && !area[i]) continue;
        const kind = layer[i];
        if (kind === null || kind === undefined || kind === 'roof') continue;
        tile = i;
        leaves = 0;
        const b = plan.owner[i]!;
        const loose = b < 0;
        /** A wall blown down to low cover (docs/FASE7.md §4): laid as a wall, but its stump and the wall above the gap. */
        const broken = kind === 'low' && plan.breached(i, level);
        if (loose && (level > 0 || (!isWallLike(kind) && !broken) || plan.massive[i])) continue;
        if (!level && plan.massive[i]) continue;
        const building = loose ? undefined : plan.buildings[b]!;
        const base = building ? building.base + level * STOREY : (s.elev[i] ?? 0) * STOREY;
        const outerKit = KITS[building ? building.kit : plan.looseKit];
        const cx = x + 0.5;
        const cz = y + 0.5;
        if (!isWallLike(kind) && !broken) {
          // A gap in the outline (a loading bay): a lintel over it. Ruins are just broken.
          const bd = building!;
          if (bd.kind === 'ruin') continue;
          const edgeX = y === bd.y0 || y === bd.y1;
          const edgeZ = x === bd.x0 || x === bd.x1;
          if (!plan.inside[i] || !(edgeX || edgeZ) || (kind !== 'floor' && kind !== 'low')) continue;
          const dir = edgeX ? { x: 0, y: y === bd.y0 ? -1 : 1 } : { x: x === bd.x0 ? -1 : 1, y: 0 };
          const step = edgeX ? { x: 1, y: 0 } : { x: 0, y: 1 };
          const bounded = (sign: number) => {
            for (let k = 1; k <= 4; k++) {
              const t = at(x + step.x * k * sign, y + step.y * k * sign);
              if (isWallLike(t)) return true;
              if (t === undefined || plan.owner[(y + step.y * k * sign) * w + x + step.x * k * sign] !== b) return false;
            }
            return false;
          };
          if (bounded(1) && bounded(-1)) putAll(outerKit.lintel, cx, cz, base, dir, false, b, outerKit);
          continue;
        }
        const conn = DIRS4.map((d) => wallish(x + d.x, y + d.y));
        const nsConn = conn[0] || conn[2];
        const ewConn = conn[1] || conn[3];
        /** Which way the outer face of a wall along `run` (0: x, 1: z) looks, judged on the tiles beside (x + ox, y + oy); null between rooms. */
        /**
         * Which way the outer face of a wall along `run` looks (judged on the
         * tiles beside (x + ox, y + oy), for building `bb`), whether it's
         * between rooms, and the neighbour it's shared with (a party wall:
         * between, facing that neighbour), or -1.
         */
        const facing = (run: 0 | 1, ox: number, oy: number, bb = b): { out: Vec2; between: boolean; other: number } => {
          const a = run === 0 ? { x: 0, y: -1 } : { x: -1, y: 0 };
          const pa = { x: x + ox + a.x, y: y + oy + a.y };
          const pb = { x: x + ox - a.x, y: y + oy - a.y };
          const inA = bb >= 0 && plan.interior(bb, pa.x, pa.y);
          const inB = bb >= 0 && plan.interior(bb, pb.x, pb.y);
          const neighbour = (p: Vec2) => {
            if (bb < 0 || p.x < 0 || p.y < 0 || p.x >= w || p.y >= h) return -1;
            const o = plan.owner[p.y * w + p.x]!;
            return o >= 0 && o !== bb && plan.inside[p.y * w + p.x] ? o : -1;
          };
          if (inA && inB) return { out: { x: -a.x, y: -a.y }, between: true, other: -1 };
          if (inA) {
            const other = neighbour(pb);
            return { out: { x: -a.x, y: -a.y }, between: other >= 0, other };
          }
          const other = inB ? neighbour(pa) : -1;
          return { out: a, between: other >= 0, other };
        };
        const straight = (ewConn && !nsConn) || (nsConn && !ewConn) || (!ewConn && !nsConn);
        if (straight) {
          // Along x unless the wall runs north–south (a lone tile follows the floor beside it).
          const run: 0 | 1 = nsConn || (!ewConn && plan.interior(b, x - 1, y) && plan.interior(b, x + 1, y)) ? 1 : 0;
          const { out, between, other } = facing(run, 0, 0);
          const kit = between && building?.kind !== 'medical' ? BETWEEN : outerKit;
          const along = run === 0 ? { x: 1, y: 0 } : { x: 0, y: 1 };
          if (!loose) storeys(broken ? 'wall' : kind, cx, cz, run === 0 ? x : y, out, between, b, other);
          if ((kind === 'door' || kind === 'doorOpen') && loose && plan.siege) {
            // A siege's gate (docs/FASE12.md §10): a sheet-metal leaf to a tile under the open sky, each hinged at its
            // end of the gate (the piece's near end is the other way from its +x; the partner turns on its far end),
            // and a column of the wall where the gate meets it.
            const gateAt = (k: number) => {
              const t = at(x + along.x * k, y + along.y * k);
              return t === 'door' || t === 'doorOpen';
            };
            const partner = gateAt(1) ? 1 : gateAt(-1) ? -1 : isWallLike(at(x + along.x, y + along.y)) ? -1 : 1;
            const turn = Math.atan2(out.x, out.y);
            const near = (Math.cos(turn) * along.x - Math.sin(turn) * along.y) * partner > 0;
            put(near ? 'gateLeaf' : 'gateLeafFar', cx, cz, base, out, false, b, { thick: kit.thick, open: kind === 'doorOpen' });
            for (const side of [-1, 1]) if (!gateAt(side) && isWallLike(at(x + along.x * side, y + along.y * side))) post(kit, cx + along.x * side * 0.5, cz + along.y * side * 0.5, base, out, false, b);
          } else if (kind === 'door' || kind === 'doorOpen') {
            putAll(kit.door, cx, cz, base, out, between, b, kit, kind === 'doorOpen');
            // A shop's name over its front door: the first door of its outline (whatever part of the map is being redone).
            if (!level && !between && !plan.siege && building && signDoor.get(b) === i && (building.kind === 'shop' || (building.kind === 'office' && building.look < 0.35))) sign(building, cx, cz, base, out);
            if (kit.leaf) {
              put(kit.leaf, cx, cz, base, out, between, b, { thick: kit.thick, open: kind === 'doorOpen' });
              // Jambs either side of the leaf.
              for (const side of [-1, 1]) put(kit.post, cx + along.x * side * 0.46, cz + along.y * side * 0.46, base, out, between, b, { along: 0.08, thick: kit.thick });
            }
          } else if (kind === 'window' || kind === 'windowBroken') {
            putAll(kit.window, cx, cz, base, out, between, b, kit);
            if (kind === 'window') {
              const kitId = between ? 'house' : building ? building.kit : plan.looseKit;
              let pane = glass.get(kitId);
              if (!pane) glass.set(kitId, (pane = { matrices: [], cut: [], level: [] }));
              pane.level.push(level);
              q.setFromAxisAngle(up, Math.atan2(out.x, out.y));
              pane.matrices.push(new THREE.Matrix4().compose(new THREE.Vector3(cx, base, cz), q.clone(), new THREE.Vector3(1, 1, 1)));
              pane.cut.push(b + 1, between ? 0 : out.x, between ? 0 : out.y, base);
            }
          } else if (broken) {
            // Blown down (docs/FASE7.md §4): a stump of the wall at low cover's height and the wall over the gap
            // left standing, its lintel (the storey above stands); the rubble on it is the map's (mapView).
            kit.wall.forEach((id, n) => put(id, cx, cz, base, out, between, b, { thick: kit.thick, tint: n === 0, tall: BREACH_HEIGHT }));
            putAll(kit.lintel, cx, cz, base, out, between, b, kit);
          } else {
            putAll(kit.wall, cx, cz, base, out, between, b, kit);
            if (!level && !between && building && shutters.has(i)) shutter(building, cx, cz, base, out, shutters.get(i)!);
          }
          if (kit.top && kind === 'wall') put(kit.top, cx, cz, base + STOREY, out, between, b, { along: 1 / WIRE_LENGTH, thick: 1 });
          // The open end of a wall gets a post, so its hollow doesn't show (on a perimeter, a gate's column).
          const ends = run === 0 ? [1, 3] : [0, 2];
          for (const k of ends) {
            if (conn[k]) continue;
            const d = DIRS4[k]!;
            post(kit, cx + d.x * 0.5, cz + d.y * 0.5, base, out, between, b);
            if (building && !building.leveled && !between) for (let n = 1; n < building.storeys; n++) post(kit, cx + d.x * 0.5, cz + d.y * 0.5, base + n * STOREY, out, false, b, true);
          }
          continue;
        }
        // A corner or a junction: half a wall from the middle towards each neighbour.
        let arms = 0;
        DIRS4.forEach((d, k) => {
          if (!conn[k]) return;
          arms++;
          const run: 0 | 1 = d.x !== 0 ? 0 : 1;
          // An arm running on into a neighbour's wall (a shop front past a party wall) is the neighbour's.
          const next = plan.owner[(y + d.y) * w + x + d.x] ?? -1;
          const bb = !loose && next >= 0 && next !== b && isWallLike(at(x + d.x, y + d.y)) ? next : b;
          const { out, between, other } = facing(run, d.x, d.y, bb);
          const kit = between && building?.kind !== 'medical' ? BETWEEN : outerKit;
          kit.wall.forEach((id, n) => put(id, cx + d.x * 0.25, cz + d.y * 0.25, base, out, between, bb, { along: 0.5, thick: kit.thick, tint: n === 0, tall: broken ? BREACH_HEIGHT : undefined }));
          if (broken) kit.lintel.forEach((id, n) => put(id, cx + d.x * 0.25, cz + d.y * 0.25, base, out, between, bb, { along: 0.5, thick: kit.thick, tint: n === 0 }));
          if (!loose) storeys('wall', cx + d.x * 0.25, cz + d.y * 0.25, 0, out, between, bb, other, 0.5);
          if (kit.top) put(kit.top, cx + d.x * 0.25, cz + d.y * 0.25, base + STOREY, out, between, b, { along: 0.5 / WIRE_LENGTH, thick: 1 });
        });
        if (arms === 2) {
          const { out, between } = facing(0, conn[1] ? 1 : -1, 0);
          post(between && building?.kind !== 'medical' ? BETWEEN : outerKit, cx, cz, base, out, between, b);
          if (building && !building.leveled && !between) for (let n = 1; n < building.storeys; n++) post(outerKit, cx, cz, base + n * STOREY, out, false, b, true);
        }
      }
    }

    }
    level = 0;
    layer = s.tiles;

    this.layRaised(s, batches, () => (ready = false), area);

    // A siege's watchtowers (docs/FASE12.md §10) stand on a block the engine won't let anyone under or see through: on the
    // sides out of the base, Quarantine's wall goes on under the deck as the wall does; on the sides in, a shed of sheet
    // metal closes it, its door shut in the middle of the side without the ladder. Each stretch runs on to the wall or the
    // next side, a column where it turns.
    if (plan.siege) {
      const tower = (x: number, y: number) => x >= 0 && y >= 0 && x < w && y < h && plan.raised[y * w + x] === RAISED_TOWER;
      const solid = (x: number, y: number) => tower(x, y) || (x >= 0 && y >= 0 && x < w && y < h && isWallLike(s.tiles[y * w + x]));
      for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
          if (!tower(x, y) || (area && !area[y * w + x])) continue;
          const cx = x + 0.5;
          const cz = y + 0.5;
          for (const d of DIRS4) {
            if (tower(x + d.x, y + d.y)) continue;
            const r = { x: Math.abs(d.y), y: Math.abs(d.x) };
            const a = solid(x - r.x, y - r.y) ? -0.5 : 0;
            const b = solid(x + r.x, y + r.y) ? 0.5 : 0;
            if (b - a <= 0) continue;
            const outward = d.x * (cx - w / 2) + d.y * (cz - h / 2) > 0;
            const kit = outward ? KITS.fence : KITS.shedGrey;
            // The ladder leans on this side: on the tile out from this one or from either beside it.
            const ladder = [-1, 0, 1].some((n) => s.tiles[(y + d.y + r.y * n) * w + x + d.x + r.x * n] === 'ladder');
            const door = !outward && !ladder && tower(x - r.x, y - r.y) && tower(x + r.x, y + r.y);
            const ids = door ? KITS.bunker.window : kit.wall;
            const c = (a + b) / 2;
            ids.forEach((id, n) => put(id, cx + r.x * c, cz + r.y * c, 0, d, false, -1, { along: b - a, thick: kit.thick, tint: n === 0 }));
            // The column where it turns, kept under the deck (Quarantine's is a little taller than a storey).
            if (a === 0 || b === 0) put(kit.post, cx, cz, 0, d, false, -1, { along: kit.postAlong, thick: kit.thick, square: kit.postSquare, tall: outward ? 0.86 : 1 });
          }
        }
      }
    }

    // A motel's sign on its pole, by the road at one end of its car park (no cutaway: it stands outside).
    for (const b of plan.buildings) {
      if (b.kind !== 'motel' || b.front < 0) continue;
      const parts = syntyProp('motelSign');
      if (!parts) {
        ready = false;
        continue;
      }
      const d = DIRS4[b.front]!;
      const along = { x: -d.y, y: d.x };
      const end = hash(b.x0, b.y0, 71) < 0.5 ? -1 : 1;
      const mid = { x: (b.x0 + b.x1) / 2 + 0.5, z: (b.y0 + b.y1) / 2 + 0.5 };
      const half = { x: (b.x1 - b.x0) / 2, z: (b.y1 - b.y0) / 2 };
      const reach = Math.abs(d.x) * half.x + Math.abs(d.y) * half.z + 4.5;
      const side = Math.abs(along.x) * half.x + Math.abs(along.y) * half.z - 1;
      m.compose(pos.set(mid.x + d.x * reach + along.x * side * end, b.base, mid.z + d.y * reach + along.y * side * end), q.setFromAxisAngle(up, Math.atan2(d.x, d.y) + Math.PI / 2), scale.set(1, 1, 1));
      parts.forEach((part, k) => {
        const key = `sign:motel:${k}`;
        let batch = batches.get(key);
        if (!batch) batches.set(key, (batch = { geometry: part.geometry, atlas: part.atlas, matrices: [], cut: [], outer: [], inner: [] }));
        batch.matrices.push(m.clone().multiply(part.matrix));
        batch.cut.push(0, 0, 0, 0);
        batch.outer.push(1, 1, 1);
        batch.inner.push(1, 1, 1);
      });
    }

    // On the roofs: they fade with them.
    for (const b of plan.buildings) {
      for (const r of roofProps(b, (x, y) => plan.kindAt(x, y, b.storeys) === 'floor')) {
        const parts = syntyProp(r.id);
        if (!parts) {
          ready = false;
          continue;
        }
        m.compose(pos.set(r.x, r.y, r.z), q.setFromAxisAngle(up, r.yaw), scale.set(1, 1, 1));
        parts.forEach((part, k) => {
          const key = `roof:${r.id}:${k}`;
          let batch = batches.get(key);
          if (!batch) batches.set(key, (batch = { geometry: part.geometry, atlas: part.atlas, matrices: [], cut: [], outer: [], inner: [] }));
          batch.matrices.push(m.clone().multiply(part.matrix));
          batch.cut.push(b.index + 1, ON_ROOF, ON_ROOF, b.base);
          batch.outer.push(1, 1, 1);
          batch.inner.push(1, 1, 1);
          (batch.level ??= []).push(b.storeys);
        });
      }
    }

    // Every piece merged, placed, into one mesh per material (batch.ts); what each carried for the cutaway, per vertex.
    const merged = this.merged;
    const WHITE3 = [1, 1, 1] as const;
    for (const [key, batch] of batches) {
      const material = this.materialFor(batch.atlas);
      // What stands on roofs and hangs on walls is no ground's occluder (the roofs aren't in the height field).
      const occludes = !key.startsWith('roof:') && !key.startsWith('sign:');
      // Its UVs moved into its pack's place in the packed texture, and which pack it is (for the dye).
      const uv = atlasRect(batch.atlas) ?? undefined;
      const aAtlas = [atlasIndex(batch.atlas)];
      batch.matrices.forEach((mm, k) =>
        merged.add(
          batch.geometry,
          material,
          mm,
          true,
          {
            aCut: batch.cut.slice(k * 4, k * 4 + 4),
            aLevel: [batch.level?.[k] ?? 0],
            aOuter: batch.outer.slice(k * 3, k * 3 + 3),
            aInner: batch.inner.slice(k * 3, k * 3 + 3),
            aAtlas,
          },
          occludes,
          uv,
        ),
      );
    }
    for (const [kit, pane] of glass) {
      const geometry = this.glassGeometry.get(kit)!;
      pane.matrices.forEach((mm, k) => {
        const e = mm.elements;
        const glow = hash(Math.round(e[12]! * 2), Math.round(e[14]! * 2), 90) < 0.35 ? 1 : 0;
        merged.add(geometry, this.glassMaterial, mm, false, { aCut: pane.cut.slice(k * 4, k * 4 + 4), aLevel: [pane.level[k] ?? 0], aOuter: WHITE3, aInner: WHITE3, aGlow: [glow], aAtlas: [-1] });
      });
    }
    const changed = merged.commit();
    if (changed) {
      // layRaised may have marked the whole map (a parapet gone).
      const d = this.dirty as Dirty | null;
      this.dirty = d ? { x0: Math.min(d.x0, changed.x0), y0: Math.min(d.y0, changed.y0), x1: Math.max(d.x1, changed.x1), y1: Math.max(d.y1, changed.y1) } : changed;
    }
    this.walls.add(merged.group);
    this.doors.commit();
    this.walls.add(this.doors.group);
    return ready;
  }

  /**
   * What stands on raised ground, as it is now: flats get a brick façade on
   * every side above lower ground, a storey at a time (one window a tile,
   * now and then plain brick, arched windows under a cornice at the top) and
   * parapets on their roof edges; platforms, container sides; hills, rock
   * faces; solid wall masses, concrete. And ladders on every climb.
   */
  private layRaised(s: GameState, batches: Map<string, Batch>, missing: () => void, area: Uint8Array | null): void {
    /** Whether this tile's pieces are placed (a partial rebuild skips the rest; the parapets are drawn whole every time). */
    let placing = true;
    const { width: w, height: h } = s;
    const plan = this.plan;
    const elev = (x: number, y: number) => (x < 0 || y < 0 || x >= w || y >= h ? 0 : (s.elev[y * w + x] ?? 0));
    const m = new THREE.Matrix4();
    const q = new THREE.Quaternion();
    const up = new THREE.Vector3(0, 1, 0);
    const pos = new THREE.Vector3();
    const size = new THREE.Vector3(1, 1, 1);
    const batchOf = (key: string, geometry: THREE.BufferGeometry, atlas: AtlasId) => {
      let batch = batches.get(key);
      if (!batch) batches.set(key, (batch = { geometry, atlas, matrices: [], cut: [], outer: [], inner: [] }));
      return batch;
    };
    const add = (batch: Batch, matrix: THREE.Matrix4) => {
      batch.matrices.push(matrix);
      batch.cut.push(0, 0, 0, 0);
      batch.outer.push(1, 1, 1);
      batch.inner.push(1, 1, 1);
    };
    /** A piece standing at (x, base, z), its outer face (+Z) turned towards `out`. */
    const place = (id: PieceId, x: number, base: number, z: number, out: Vec2) => {
      if (!placing) return;
      const parts = pieceNow(id);
      if (!parts) return missing();
      m.compose(pos.set(x, base, z), q.setFromAxisAngle(up, Math.atan2(out.x, out.y)), size.set(1, 1, 1));
      for (const part of parts) add(batchOf(`${id}:${part.name}`, part.geometry, part.atlas), m.clone().multiply(part.matrix));
    };
    /** A rock of a hill's side, its face out towards `out`, stretched `tall` times, turned `turn` and `flip`ped. */
    const rock = (id: PropId, x: number, base: number, z: number, out: Vec2, tall: number, along: number, turn: number) => {
      if (!placing) return;
      const parts = syntyProp(id);
      if (!parts) return missing();
      // The rock lies along x in its own frame: turned to run along the edge.
      m.compose(pos.set(x, base, z), q.setFromAxisAngle(up, Math.atan2(out.x, out.y) + Math.PI / 2 + turn), size.set(along, tall, 1.7));
      parts.forEach((part, k) => add(batchOf(`rock:${id}:${k}`, part.geometry, part.atlas), m.clone().multiply(part.matrix)));
    };
    const parapets = new Surfaces();
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const i = y * w + x;
        const e = elev(x, y);
        placing = !area || !!area[i];
        // Ladders: against the side of the tile one storey up, whatever it is (a building's roof one walks on, too);
        // a building's stairs are drawn as stairs (buildStatic).
        if (s.tiles[i] === 'ladder' && decorOf(s.decor?.[i])?.kind !== 'stairs') {
          for (const d of DIRS4) {
            const up = elev(x + d.x, y + d.y) === e + 1 || (e === 0 && isWalkable(plan.kindAt(x + d.x, y + d.y, 1)));
            if (up) place('ladder', x + 0.5 + d.x * 0.44, e * STOREY, y + 0.5 + d.y * 0.44, { x: -d.x, y: -d.y });
          }
        }
        // On a storey: a ladder up to the next one.
        for (let k = 1; k <= (s.levels?.length ?? 0); k++) {
          if (plan.kindAt(x, y, k) !== 'ladder' || decorOf(s.levels![k - 1]!.decor?.[i])?.kind === 'stairs') continue;
          for (const d of DIRS4) if (isWalkable(plan.kindAt(x + d.x, y + d.y, k + 1))) place('ladder', x + 0.5 + d.x * 0.44, k * STOREY, y + 0.5 + d.y * 0.44, { x: -d.x, y: -d.y });
        }
        // A solid mass of wall: concrete on its sides.
        if (plan.massive[i]) {
          for (const d of DIRS4) {
            const n = at(s, x + d.x, y + d.y);
            if (n !== undefined && isWallLike(n) && plan.massive[(y + d.y) * w + x + d.x]) continue;
            place('bunkerWall', x + 0.5 + d.x * 0.42, e * STOREY, y + 0.5 + d.y * 0.42, d);
          }
        }
        const style = plan.raised[i];
        // A watchtower's sides are its legs (the guard tower, mapView).
        if (!style || style === RAISED_TOWER) continue;
        for (const d of DIRS4) {
          const low = elev(x + d.x, y + d.y);
          if (low >= e) continue;
          const t = d.x !== 0 ? y : x;
          const [ex, ez] = [x + 0.5 + d.x * 0.5, y + 0.5 + d.y * 0.5];
          if (style === RAISED_HILL) {
            // Two rocks stacked up the side, each its own way, bulging out a little.
            const r = (salt: number) => hash(x * 4 + d.x + 2, y * 4 + d.y + 2, salt);
            for (let k = low; k < e; k++) {
              const b0 = k * STOREY;
              rock(r(1) < 0.5 ? 'cliff' : 'cliff2', ex - d.x * 0.02, b0 - 0.25, ez - d.y * 0.02, d, 1.9 + r(2) * 0.3, 1.0 + r(3) * 0.25, r(4) < 0.5 ? 0 : Math.PI);
              rock(r(5) < 0.5 ? 'cliff' : 'cliff2', ex - d.x * 0.08, b0 + 1.0, ez - d.y * 0.08, d, 1.6 + r(6) * 0.3, 0.9 + r(7) * 0.25, r(8) < 0.5 ? 0 : Math.PI);
            }
            continue;
          }
          if (style === RAISED_PLATFORM) {
            for (let k = low; k < e; k++) place('containerWall', ex - d.x * 0.1, k * STOREY, ez - d.y * 0.1, d);
            continue;
          }
          // Flats: along the face every third tile plain brick, and where a ladder leans on it.
          const ladder = s.tiles[(y + d.y) * w + x + d.x] === 'ladder' && x + d.x >= 0 && y + d.y >= 0 && x + d.x < w && y + d.y < h;
          for (let k = low; k < e; k++) {
            const plain = ladder || (t + k) % 3 === 1;
            const id: PieceId = k === e - 1 ? (plain ? 'blockTopPlain' : 'blockTopWindow') : plain ? 'blockPlain' : 'blockWindow';
            place(id, ex, k * STOREY, ez, d);
          }
          // A parapet along this edge of a roof tile that holds one (low cover with nothing else on it).
          if (s.tiles[i] === 'low' && !decorOf(s.decor?.[i])) {
            const top = e * STOREY;
            const half = 0.5;
            const along = d.x !== 0 ? V(0, 0, half) : V(half, 0, 0);
            const inward = V(-d.x * PARAPET_THICK, 0, -d.y * PARAPET_THICK);
            const lo = V(Math.min(ex - along.x, ex + inward.x), top, Math.min(ez - along.z, ez + inward.z));
            const hi = V(Math.max(ex + along.x, ex + inward.x), top + PARAPET, Math.max(ez + along.z, ez + inward.z));
            parapets.box(lo, hi, SWATCH.flat, BRICK, -1);
            parapets.box(V(lo.x - 0.03, hi.y, lo.z - 0.03), V(hi.x + 0.03, hi.y + 0.07, hi.z + 0.03), SWATCH.flat, STONE, -1);
          }
        }
      }
    }
    if (!parapets.empty) {
      const geometry = parapets.geometry();
      this.wallParts.geometries.push(geometry);
      const mesh = new THREE.Mesh(geometry, this.blockMaterial);
      mesh.castShadow = true;
      mesh.receiveShadow = true;
      mesh.matrixAutoUpdate = false;
      this.walls.add(mesh);
      this.wallOccluders.push(geometry);
      // A parapet blown away: the contact occlusion of the whole map is baked again (rare, and they're few).
      const p = geometry.attributes.position!.array;
      let sig = p.length;
      for (let k = 0; k < p.length; k += 7) sig = (sig * 31 + Math.round(p[k]! * 64)) | 0;
      if (sig !== this.parapetSignature) this.dirty = { x0: 0, y0: 0, x1: w, y1: h };
      this.parapetSignature = sig;
    }
  }

  /** The pieces' material on an atlas (walls are Town's; what stands on roofs comes from other packs too). */
  private materialFor(atlas: AtlasId): THREE.MeshStandardMaterial {
    if (atlasRect(atlas)) return this.pieceMaterial;
    let m = this.atlasMaterials.get(atlas);
    if (!m) {
      m = this.own(new THREE.MeshStandardMaterial({ map: atlasTexture(atlas), roughness: 0.85, side: THREE.DoubleSide }));
      wallCutPatch(m, this.cutaway);
      this.medical.patch(m, 'wall');
      this.atlasMaterials.set(atlas, m);
    }
    return m;
  }

  /** What darkens the ground round it, as built now (owned here: don't dispose). */
  occluders(): THREE.BufferGeometry[] {
    return [...this.staticOccluders, ...this.merged.occluderGeometries(), ...this.wallOccluders];
  }

  /** Moves the cutaway on (`inside`: buildings whose inside is in sight lose their roof). */
  /** A siege's doors as worn as they are now (siegeDressing.ts), by ground tile. */
  wearDoors(wear: ReadonlyMap<number, number>): void {
    const tiles = this.plan.s.tiles;
    for (const [tile, stage] of wear) if (tiles[tile] === 'door' || tiles[tile] === 'doorOpen') this.doors.setWear(tile, stage);
  }

  /** A blow on a siege's door at ground tile `tile`: it shakes. */
  knock(tile: number): void {
    this.doors.knock(tile);
  }

  update(dt: number, s: GameState, camera: THREE.Camera | null, focus: Focus | null, visible: readonly boolean[] | null, inside = true): void {
    this.cutaway.update(dt, s, camera, focus, visible, inside);
    this.doors.update(dt);
  }

  dispose(): void {
    this.merged.dispose();
    this.doors.dispose();
    for (const g of this.wallParts.geometries) g.dispose();
    for (const m of this.materials) m.dispose();
    for (const g of this.geometries) g.dispose();
    this.cutaway.dispose();
    this.medical.dispose();
  }
}
