import { decorCode } from './decor';
import { DIRS4, isWalkable } from './grid';
import type { Biome, Decor, Level, Place, TemplateId, TileKind, Vec2 } from './types';

/**
 * Hand-made maps in ASCII.
 *
 *   .  floor          #  wall (indestructible, full cover)
 *   H  high cover     h  low cover
 *   +  door           =  window          L  ladder
 *   S  a building's stairs (a ladder to the storey above, drawn as stairs)
 *   x  XCOM spawn     1-9  alien pod spawn (members listed in `pods`)
 *   D  objective spot (data, terminal, relay or VIP depending on the mission)
 *   E  evacuation zone
 *
 * `heights`, when present, gives each tile's elevation as a digit (storeys).
 *
 * `levels`, when present, are the storeys above the ground, bottom up
 * (docs/FASE6-PLAN.md), drawn like `rows`: a space where the storey has no
 * slab (open air, the stairwell over a flight of stairs), `R` a roof nobody
 * stands on, and pod spawns (digits) for enemies posted up there.
 */
export interface MapDef {
  id: string;
  name: string;
  biome: Biome;
  rows: string[];
  heights?: string[];
  levels?: string[][];
  pods: Record<string, TemplateId[]>;
}

export const MAPS: Record<string, MapDef> = {
  plaza: {
    id: 'plaza',
    name: 'Distrito Comercial',
    biome: 'city',
    rows: [
      '..........h.........##########',
      '..H.......h.........#..3.....#',
      '.......H.....hh.....=...3..D.#',
      '....................#.....3..#',
      '.###=####....hhh....###+######',
      '.#......#.....................',
      '.#..1...=...H.......h....H....',
      '.#....1.+.....h.....h.........',
      '.########.....##......hh......',
      '..............##..............',
      '....hh..........2.....#####...',
      '...........h...2......#...#...',
      '..H........h..........=.4.#...',
      '......................#..4#...',
      '...hhhh....H..........##+##...',
      '..............................',
      '..........####.......hh....H..',
      '...H......#..=................',
      '..........#..#.....H..........',
      '......hh..#..#...........hhh..',
      '..........##+#................',
      '....H..........hh.....H.......',
      '..............................',
      '..hh.....H.........hh......H..',
      '..............................',
      '......h.......H...............',
      '.xxx..h...................EEE.',
      '.xxx......hh..............EEE.',
      '.xx......................hEEE.',
      '..............................',
    ],
    // The dead's, like every ordinary mission (docs/ZOMBIS-PRIMERO.md).
    pods: {
      '1': ['zombie', 'zombie'],
      '2': ['zombie', 'zombie', 'bloater'],
      '3': ['zombie', 'mec', 'xenoid'],
      '4': ['sectoid', 'xenoid'],
    },
  },
  // Phase 6's first building of several storeys (docs/FASE6-PLAN.md), for the
  // client to draw and move on before the generator makes them: a tower of
  // three storeys with stairs up to its roof and two of the Cuervos on the
  // first floor; a shed with a flat roof and a ladder up it; a house with a
  // pitched roof.
  torre: {
    id: 'torre',
    name: 'La torre',
    biome: 'city',
    rows: [
      '.....................EEE',
      '.....................EEE',
      '....................22..',
      '....................2...',
      '.........##=##=##.......',
      '.........#S....H#.......',
      '...hh....#......#.......',
      '.........=......=..hh...',
      '.........#....h.#.......',
      '......H..#......#.......',
      '.........###+####.......',
      '........................',
      '........................',
      '..####...........##+##..',
      '..#..#L.....hh...#...#..',
      '..#h.#...........=...=..',
      '..##+#...........#..h#..',
      '........H......h.#####..',
      '.xxx....................',
      '.xxx....................',
    ],
    levels: [
      [
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '         ###=####       ',
        '         # ..#11#       ',
        '         #.H.#..#       ',
        '         =...+..=       ',
        '         #...#..#       ',
        '         #h..#.S#       ',
        '         ##=##=##       ',
        '                        ',
        '                        ',
        '  ....           RRRRR  ',
        '  ....           RRRRR  ',
        '  ....           RRRRR  ',
        '  ....           RRRRR  ',
        '                 RRRRR  ',
        '                        ',
        '                        ',
      ],
      [
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '         ####=###       ',
        '         #......#       ',
        '         =....H.#       ',
        '         #......#       ',
        '         #..h...=       ',
        '         #S.... #       ',
        '         ###=####       ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
      ],
      [
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '         hhhhhhhh       ',
        '         h......h       ',
        '         h......h       ',
        '         h......h       ',
        '         h......h       ',
        '         h .....h       ',
        '         hhhhhhhh       ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
        '                        ',
      ],
    ],
    pods: {
      '1': ['trooper', 'officer'],
      '2': ['zombie', 'zombie', 'zombie'],
    },
  },
};

export interface ParsedMap {
  width: number;
  height: number;
  tiles: TileKind[];
  elev: number[];
  /** Only on maps with stairs: what they look like (decor.ts). */
  decor?: Decor[];
  /** The storeys above the ground, and how tall each tile's building stands (GameState.storeys), on maps with them. */
  levels?: Level[];
  storeys?: number[];
  spawns: Vec2[];
  /** On the ground unless they say their level. */
  podSpawns: Record<string, Place[]>;
  item: Vec2 | null;
  evacZone: Vec2[];
}

const LEGEND: Record<string, TileKind> = {
  '.': 'floor',
  '#': 'wall',
  H: 'high',
  h: 'low',
  '+': 'door',
  '=': 'window',
  L: 'ladder',
  S: 'ladder',
};

export function parseMap(def: MapDef): ParsedMap {
  const height = def.rows.length;
  const width = def.rows[0]!.length;
  const tiles: TileKind[] = [];
  const elev: number[] = [];
  const spawns: Vec2[] = [];
  const podSpawns: Record<string, Place[]> = {};
  const evacZone: Vec2[] = [];
  let item: Vec2 | null = null;

  def.rows.forEach((row, y) => {
    if (row.length !== width) {
      throw new Error(`Map ${def.id}: row ${y} has length ${row.length}, expected ${width}`);
    }
    [...row].forEach((ch, x) => {
      elev.push(Number(def.heights?.[y]?.[x] ?? 0) || 0);
      const kind = LEGEND[ch];
      if (kind) {
        tiles.push(kind);
        return;
      }
      tiles.push('floor');
      if (ch === 'x') spawns.push({ x, y });
      else if (ch === 'D') item = { x, y };
      else if (ch === 'E') evacZone.push({ x, y });
      else if (ch >= '1' && ch <= '9') (podSpawns[ch] ??= []).push({ x, y });
      else throw new Error(`Map ${def.id}: unknown tile '${ch}' at ${x},${y}`);
    });
  });

  if (!def.levels?.length) return { width, height, tiles, elev, spawns, podSpawns, item, evacZone };

  // The storeys above (docs/FASE6-PLAN.md).
  const levels: Level[] = def.levels.map((rows, k) => {
    if (rows.length !== height || rows.some((r) => r.length !== width)) throw new Error(`Map ${def.id}: storey ${k + 1} isn't ${width} × ${height}`);
    const out: (TileKind | null)[] = [];
    rows.forEach((row, y) =>
      [...row].forEach((ch, x) => {
        if (ch === ' ') out.push(null);
        else if (ch === 'R') out.push('roof');
        else if (LEGEND[ch]) out.push(LEGEND[ch]);
        else if (ch >= '1' && ch <= '9') {
          out.push('floor');
          (podSpawns[ch] ??= []).push({ x, y, z: k + 1 });
        } else throw new Error(`Map ${def.id}: unknown tile '${ch}' at ${x},${y} on storey ${k + 1}`);
      }),
    );
    return { tiles: out };
  });
  // Stairs look up to the first side with a floor one storey up (they climb like a ladder: GameState.levels).
  const stairs = (rows: string[], level: (TileKind | null)[] | undefined): Decor[] | undefined => {
    if (!rows.some((r) => r.includes('S'))) return undefined;
    const out = new Array<Decor>(width * height).fill(0);
    rows.forEach((row, y) =>
      [...row].forEach((ch, x) => {
        if (ch !== 'S') return;
        const up = DIRS4.findIndex((d) => {
          const q = { x: x + d.x, y: y + d.y };
          return q.x >= 0 && q.y >= 0 && q.x < width && q.y < height && isWalkable(level?.[q.y * width + q.x] ?? null);
        });
        out[y * width + x] = decorCode('stairs', Math.max(0, up));
      }),
    );
    return out;
  };
  const decor = stairs(def.rows, levels[0]?.tiles);
  def.levels.forEach((rows, k) => {
    const look = stairs(rows, levels[k + 1]?.tiles);
    if (look) levels[k]!.decor = look;
  });
  // How tall each building stands: up to its highest slab.
  const storeys = tiles.map((_, i) => levels.reduce((top, l, k) => (l.tiles[i] !== null ? k + 1 : top), 0));
  return { width, height, tiles, elev, ...(decor ? { decor } : {}), levels, storeys, spawns, podSpawns, item, evacZone };
}
