#nullable enable
// maps.ts: the hand-made maps, and how one is read into tiles.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>
    /// Hand-made maps in ASCII (maps.ts has the legend). `heights`, when present, gives each tile's elevation as a digit
    /// (storeys); `levels`, the storeys above the ground, bottom up; `siege`: a base to defend (docs/FASE12.md).
    /// </summary>
    public sealed class MapDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Biome { get; set; } = ""; // Biomes
        public IReadOnlyList<string> Rows { get; set; } = new string[0];
        public IReadOnlyList<string>? Heights { get; set; }
        public IReadOnlyList<IReadOnlyList<string>>? Levels { get; set; }
        /// <summary>`Record<string, TemplateId[]>`, in the order written.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Pods { get; set; } = new Dictionary<string, IReadOnlyList<string>>();
        public bool? Siege { get; set; }
    }

    public sealed class ParsedMap
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<string> Tiles { get; set; } = new List<string>(); // TileKinds
        public List<int> Elev { get; set; } = new List<int>();
        /// <summary>Only on maps with stairs: what they look like (decor.ts).</summary>
        public List<int>? Decor { get; set; }
        /// <summary>The storeys above the ground, and how tall each tile's building stands (GameState.storeys), on maps with them.</summary>
        public List<Level>? Levels { get; set; }
        public List<int>? Storeys { get; set; }
        public List<Vec2> Spawns { get; set; } = new List<Vec2>();
        /// <summary>On the ground unless they say their level (`Record<string, Place[]>`: keys in Object.keys order, '1'–'9' ascending).</summary>
        public JsMap<string, List<Pos>> PodSpawns { get; set; } = new JsMap<string, List<Pos>>();
        public Vec2? Item { get; set; }
        public List<Vec2> EvacZone { get; set; } = new List<Vec2>();
        /// <summary>The barricades' tiles (`b`): low cover that looks like a barricade.</summary>
        public List<Vec2> Barricades { get; set; } = new List<Vec2>();
    }

    /// <summary>
    /// Hand-made maps in ASCII.
    ///
    ///   .  floor          #  wall (indestructible, full cover)
    ///   H  high cover     h  low cover
    ///   +  door           =  window          L  ladder
    ///   S  a building's stairs (a ladder to the storey above, drawn as stairs)
    ///   b  barricade: low cover the dead break down in a siege (docs/FASE12.md)
    ///   x  XCOM spawn     1-9  alien pod spawn (members listed in `pods`)
    ///   D  objective spot (data, terminal, relay or VIP depending on the mission)
    ///   E  evacuation zone
    ///
    /// `heights`, when present, gives each tile's elevation as a digit (storeys).
    ///
    /// `levels`, when present, are the storeys above the ground, bottom up
    /// (docs/FASE6-PLAN.md), drawn like `rows`: a space where the storey has no
    /// slab (open air, the stairwell over a flight of stairs), `R` a roof nobody
    /// stands on, and pod spawns (digits) for enemies posted up there.
    ///
    /// `siege`: a base to defend (docs/FASE12.md). Every mission on it is a siege:
    /// no enemies at the start, the dead come in waves for the soldiers.
    /// </summary>
    public static class Maps
    {
        /// <summary>MAPS, by id, in the order written (a Dictionary only ever added to walks in that order).</summary>
        public static readonly IReadOnlyDictionary<string, MapDef> All = new[]
        {
            new MapDef
            {
                Id = "plaza",
                Name = "Distrito Comercial",
                Biome = Biomes.City,
                Rows = new[]
                {
                    "..........h.........##########",
                    "..H.......h.........#..3.....#",
                    ".......H.....hh.....=...3..D.#",
                    "....................#.....3..#",
                    ".###=####....hhh....###+######",
                    ".#......#.....................",
                    ".#..1...=...H.......h....H....",
                    ".#....1.+.....h.....h.........",
                    ".########.....##......hh......",
                    "..............##..............",
                    "....hh..........2.....#####...",
                    "...........h...2......#...#...",
                    "..H........h..........=.4.#...",
                    "......................#..4#...",
                    "...hhhh....H..........##+##...",
                    "..............................",
                    "..........####.......hh....H..",
                    "...H......#..=................",
                    "..........#..#.....H..........",
                    "......hh..#..#...........hhh..",
                    "..........##+#................",
                    "....H..........hh.....H.......",
                    "..............................",
                    "..hh.....H.........hh......H..",
                    "..............................",
                    "......h.......H...............",
                    ".xxx..h...................EEE.",
                    ".xxx......hh..............EEE.",
                    ".xx......................hEEE.",
                    "..............................",
                },
                // The dead's, like every ordinary mission (docs/ZOMBIS-PRIMERO.md).
                Pods = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["1"] = new[] { TemplateIds.Zombie, TemplateIds.Zombie },
                    ["2"] = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Bloater },
                    ["3"] = new[] { TemplateIds.Zombie, TemplateIds.Mec, TemplateIds.Xenoid },
                    ["4"] = new[] { TemplateIds.Sectoid, TemplateIds.Xenoid },
                },
            },
            // Phase 6's first building of several storeys (docs/FASE6-PLAN.md), for the
            // client to draw and move on before the generator makes them: a tower of
            // three storeys with stairs up to its roof and two of the Cuervos on the
            // first floor; a shed with a flat roof and a ladder up it; a house with a
            // pitched roof.
            new MapDef
            {
                Id = "torre",
                Name = "La torre",
                Biome = Biomes.City,
                Rows = new[]
                {
                    ".....................EEE",
                    ".....................EEE",
                    "....................22..",
                    "....................2...",
                    ".........##=##=##.......",
                    ".........#S....H#.......",
                    "...hh....#......#.......",
                    ".........=......=..hh...",
                    ".........#....h.#.......",
                    "......H..#......#.......",
                    ".........###+####.......",
                    "........................",
                    "........................",
                    "..####...........##+##..",
                    "..#..#L.....hh...#...#..",
                    "..#h.#...........=...=..",
                    "..##+#...........#..h#..",
                    "........H......h.#####..",
                    ".xxx....................",
                    ".xxx....................",
                },
                Levels = new IReadOnlyList<string>[]
                {
                    new[]
                    {
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "         ###=####       ",
                        "         # ..#11#       ",
                        "         #.H.#..#       ",
                        "         =...+..=       ",
                        "         #...#..#       ",
                        "         #h..#.S#       ",
                        "         ##=##=##       ",
                        "                        ",
                        "                        ",
                        "  ....           RRRRR  ",
                        "  ....           RRRRR  ",
                        "  ....           RRRRR  ",
                        "  ....           RRRRR  ",
                        "                 RRRRR  ",
                        "                        ",
                        "                        ",
                    },
                    new[]
                    {
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "         ####=###       ",
                        "         #......#       ",
                        "         =....H.#       ",
                        "         #......#       ",
                        "         #..h...=       ",
                        "         #S.... #       ",
                        "         ###=####       ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                    },
                    new[]
                    {
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "         hhhhhhhh       ",
                        "         h......h       ",
                        "         h......h       ",
                        "         h......h       ",
                        "         h......h       ",
                        "         h .....h       ",
                        "         hhhhhhhh       ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                        "                        ",
                    },
                },
                Pods = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["1"] = new[] { TemplateIds.Trooper, TemplateIds.Officer },
                    ["2"] = new[] { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie },
                },
            },
            // The base as a battlefield (docs/FASE12.md, hito 1): the old military
            // station over Bielsa, its wall with a gate and a barricade on each side,
            // a raised watchtower on each corner (a ladder up from inside), and the
            // Sala de radio in the middle.
            new MapDef
            {
                Id = "bastion",
                Name = "El Bastión",
                Biome = Biomes.City,
                Rows = new[]
                {
                    "....................................",
                    "....................H...............",
                    "....H...................hh.......H..",
                    "........hh..H.......................",
                    "...............h...........H...h....",
                    "....................................",
                    ".........########++####bbb#.........",
                    ".........L................L.........",
                    "......................h.............",
                    "......#.........h..h.######..#...H..",
                    "..H...#..##=###......#....=..b......",
                    "......#..#....#......#....#..b.h....",
                    "....h.#..#....+......##+###..b......",
                    "......#..######............h.#......",
                    "......#........######........#......",
                    "......#........#....#........#.H....",
                    "....H.#..h..H..#....#..H..h..#......",
                    "......+........#....#........+......",
                    "......+........#....#........+......",
                    "......#..h..H..###+##..H..h..#..h...",
                    "...h..#......................#......",
                    "......#..##+##..xxxx.........#....H.",
                    ".H....#.h#...#..xxxx..#####..#......",
                    "......b..#...=........+...#..#......",
                    "....h.b..#...#........#...#..#.h....",
                    "......b..#####........##=##..#......",
                    "......#.........h..h.........#......",
                    ".............h..................H...",
                    "...H.....L................L.........",
                    ".........#bbb####++########.........",
                    "....................................",
                    ".........h..............H...........",
                    ".....H...............hh.....h.......",
                    "............H.................H.....",
                    "..............h.....................",
                    "....................................",
                },
                Heights = new[]
                {
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000111000000000000000000111000000",
                    "000000111000000000000000000111000000",
                    "000000111000000000000000000111000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000111000000000000000000111000000",
                    "000000111000000000000000000111000000",
                    "000000111000000000000000000111000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                    "000000000000000000000000000000000000",
                },
                Pods = new Dictionary<string, IReadOnlyList<string>>(),
                Siege = true,
            },
        }.ToDictionary((d) => d.Id);

        private static Vec2 V(int x, int y)
        {
            return new Vec2 { X = x, Y = y };
        }

        private static readonly IReadOnlyDictionary<char, string> Legend = new Dictionary<char, string>
        {
            ['.'] = TileKinds.Floor,
            ['#'] = TileKinds.Wall,
            ['H'] = TileKinds.High,
            ['h'] = TileKinds.Low,
            ['+'] = TileKinds.Door,
            ['='] = TileKinds.Window,
            ['L'] = TileKinds.Ladder,
            ['S'] = TileKinds.Ladder,
        };

        public static ParsedMap ParseMap(MapDef def)
        {
            var height = def.Rows.Count;
            var width = def.Rows[0].Length;
            var tiles = new List<string>(); // TileKinds
            var elev = new List<int>();
            var spawns = new List<Vec2>();
            var podSpawns = new JsMap<string, List<Pos>>();
            var evacZone = new List<Vec2>();
            var barricades = new List<Vec2>();
            Vec2? item = null;
            // `(podSpawns[ch] ??= [])`.
            List<Pos> PodsOf(char ch)
            {
                var key = ch.ToString();
                if (!podSpawns.TryGetValue(key, out var list))
                {
                    list = new List<Pos>();
                    podSpawns.Set(key, list);
                }
                return list;
            }

            for (var y = 0; y < def.Rows.Count; y++)
            {
                var row = def.Rows[y];
                if (row.Length != width)
                {
                    throw new InvalidOperationException($"Map {def.Id}: row {y} has length {row.Length}, expected {width}");
                }
                for (var x = 0; x < row.Length; x++)
                {
                    var ch = row[x];
                    // `Number(def.heights?.[y]?.[x] ?? 0) || 0`: a digit, else 0 (a space is 0, anything else NaN).
                    var digit = def.Heights != null && y < def.Heights.Count && x < def.Heights[y].Length ? def.Heights[y][x] : '0';
                    elev.Add(digit >= '0' && digit <= '9' ? digit - '0' : 0);
                    if (Legend.TryGetValue(ch, out var kind))
                    {
                        tiles.Add(kind);
                        continue;
                    }
                    if (ch == 'b')
                    {
                        tiles.Add(TileKinds.Low);
                        barricades.Add(V(x, y));
                        continue;
                    }
                    tiles.Add(TileKinds.Floor);
                    if (ch == 'x') spawns.Add(V(x, y));
                    else if (ch == 'D') item = V(x, y);
                    else if (ch == 'E') evacZone.Add(V(x, y));
                    else if (ch >= '1' && ch <= '9') PodsOf(ch).Add(new Pos { X = x, Y = y });
                    else throw new InvalidOperationException($"Map {def.Id}: unknown tile '{ch}' at {x},{y}");
                }
            }

            // Barricades look like barricades, facing across their line (decor.ts).
            List<int>? Barricaded(List<int>? @base)
            {
                if (barricades.Count == 0) return @base;
                var @out = @base != null ? new List<int>(@base) : Enumerable.Repeat(0, width * height).ToList();
                foreach (var b in barricades)
                {
                    var row = def.Rows[b.Y];
                    var along = new[] { -1, 1 }.Any((dx) =>
                    {
                        var c = b.X + dx >= 0 && b.X + dx < row.Length ? row[b.X + dx] : ' ';
                        return c == '#' || c == 'b';
                    });
                    @out[b.Y * width + b.X] = Decor.DecorCode(DecorKinds.Barricade, along ? 0 : 1);
                }
                return @out;
            }
            // Record<string, Place[]> walks its keys as Object.keys does: integer-like ones ascending ('1'–'9': ordinal).
            JsMap<string, List<Pos>> Ordered()
            {
                var keys = podSpawns.Keys.ToList();
                Js.Sort(keys, (a, b) => string.CompareOrdinal(a, b));
                var ordered = new JsMap<string, List<Pos>>();
                foreach (var key in keys) ordered.Set(key, podSpawns.Get(key, new List<Pos>()));
                return ordered;
            }
            if (def.Levels == null || def.Levels.Count == 0)
            {
                var decor = Barricaded(null);
                return new ParsedMap { Width = width, Height = height, Tiles = tiles, Elev = elev, Decor = decor, Spawns = spawns, PodSpawns = Ordered(), Item = item, EvacZone = evacZone, Barricades = barricades };
            }

            // The storeys above (docs/FASE6-PLAN.md).
            var levels = new List<Level>();
            for (var k = 0; k < def.Levels.Count; k++)
            {
                var rows = def.Levels[k];
                if (rows.Count != height || rows.Any((r) => r.Length != width)) throw new InvalidOperationException($"Map {def.Id}: storey {k + 1} isn't {width} × {height}");
                var @out = new List<string?>(); // TileKinds
                for (var y = 0; y < rows.Count; y++)
                {
                    var row = rows[y];
                    for (var x = 0; x < row.Length; x++)
                    {
                        var ch = row[x];
                        if (ch == ' ') @out.Add(null);
                        else if (ch == 'R') @out.Add(TileKinds.Roof);
                        else if (Legend.TryGetValue(ch, out var kind)) @out.Add(kind);
                        else if (ch >= '1' && ch <= '9')
                        {
                            @out.Add(TileKinds.Floor);
                            PodsOf(ch).Add(new Pos { X = x, Y = y, Z = k + 1 });
                        }
                        else throw new InvalidOperationException($"Map {def.Id}: unknown tile '{ch}' at {x},{y} on storey {k + 1}");
                    }
                }
                levels.Add(new Level { Tiles = @out });
            }
            // Stairs look up to the first side with a floor one storey up (they climb like a ladder: GameState.levels).
            List<int>? Stairs(IReadOnlyList<string> rows, List<string?>? level)
            {
                if (!rows.Any((r) => r.IndexOf('S') >= 0)) return null;
                var @out = Enumerable.Repeat(0, width * height).ToList();
                for (var y = 0; y < rows.Count; y++)
                {
                    var row = rows[y];
                    for (var x = 0; x < row.Length; x++)
                    {
                        if (row[x] != 'S') continue;
                        var up = -1;
                        for (var n = 0; n < Grid.Dirs4.Count; n++)
                        {
                            var d = Grid.Dirs4[n];
                            var q = V(x + d.X, y + d.Y);
                            if (q.X >= 0 && q.Y >= 0 && q.X < width && q.Y < height && Grid.IsWalkable(Js.At(level, q.Y * width + q.X, null)))
                            {
                                up = n;
                                break;
                            }
                        }
                        @out[y * width + x] = Decor.DecorCode(DecorKinds.Stairs, Math.Max(0, up));
                    }
                }
                return @out;
            }
            var decorUp = Barricaded(Stairs(def.Rows, Js.At(levels, 0, null!)?.Tiles));
            for (var k = 0; k < def.Levels.Count; k++)
            {
                var look = Stairs(def.Levels[k], Js.At(levels, k + 1, null!)?.Tiles);
                if (look != null) levels[k].Decor = look;
            }
            // How tall each building stands: up to its highest slab.
            var storeys = tiles.Select((_, i) =>
            {
                var top = 0;
                for (var k = 0; k < levels.Count; k++) top = levels[k].Tiles[i] != null ? k + 1 : top;
                return top;
            }).ToList();
            return new ParsedMap { Width = width, Height = height, Tiles = tiles, Elev = elev, Decor = decorUp, Levels = levels, Storeys = storeys, Spawns = spawns, PodSpawns = Ordered(), Item = item, EvacZone = evacZone, Barricades = barricades };
        }
    }
}
