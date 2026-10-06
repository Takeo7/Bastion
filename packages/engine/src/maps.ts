import type { Biome, TemplateId, TileKind, Vec2 } from './types';

/**
 * Hand-made maps in ASCII.
 *
 *   .  floor          #  wall (indestructible, full cover)
 *   H  high cover     h  low cover
 *   +  door           =  window          L  ladder
 *   x  XCOM spawn     1-9  alien pod spawn (members listed in `pods`)
 *   D  objective spot (data, terminal, relay or VIP depending on the mission)
 *   E  evacuation zone
 *
 * `heights`, when present, gives each tile's elevation as a digit (storeys).
 */
export interface MapDef {
  id: string;
  name: string;
  biome: Biome;
  rows: string[];
  heights?: string[];
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
    pods: {
      '1': ['trooper', 'lancer'],
      '2': ['trooper', 'officer'],
      '3': ['trooper', 'mec', 'xenoid'],
      '4': ['lancer', 'xenoid'],
    },
  },
};

export interface ParsedMap {
  width: number;
  height: number;
  tiles: TileKind[];
  elev: number[];
  spawns: Vec2[];
  podSpawns: Record<string, Vec2[]>;
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
};

export function parseMap(def: MapDef): ParsedMap {
  const height = def.rows.length;
  const width = def.rows[0]!.length;
  const tiles: TileKind[] = [];
  const elev: number[] = [];
  const spawns: Vec2[] = [];
  const podSpawns: Record<string, Vec2[]> = {};
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

  return { width, height, tiles, elev, spawns, podSpawns, item, evacZone };
}
