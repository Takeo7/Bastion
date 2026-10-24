#nullable enable
// furnishing.ts: what stands on the floor and takes its tile.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>A map being furnished, as the layout has it (its final frame): the ground and the storeys above it.</summary>
    public sealed class FurnishMap
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<string> Tiles { get; set; } = new List<string>(); // TileKinds
        public List<int> Elev { get; set; } = new List<int>();
        public List<int> Decor { get; set; } = new List<int>();
        public List<Level> Levels { get; set; } = new List<Level>();
        public List<int>? Ground { get; set; }
    }

    /// <summary>A building with furniture of its trade (the layout's, in its final frame): its bounds, walls included.</summary>
    public sealed class FurnishSite
    {
        public string Kind { get; set; } = ""; // 'shop' | 'hall' | 'barn' (FurnishSiteKinds)
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
    }

    /// <summary>FurnishSite.kind: `'shop' | 'hall' | 'barn'`.</summary>
    public static class FurnishSiteKinds
    {
        public const string Shop = "shop";
        public const string Hall = "hall";
        public const string Barn = "barn";

        public static readonly IReadOnlyList<string> All = new[] { Shop, Hall, Barn };
    }

    /// <summary>A shop's trade (furnishing.ts tradeOf): `'books' | 'cafe' | 'grocer'`.</summary>
    public static class ShopTrades
    {
        public const string Books = "books";
        public const string Cafe = "cafe";
        public const string Grocer = "grocer";

        public static readonly IReadOnlyList<string> All = new[] { Books, Cafe, Grocer };
    }

    // What stands on the floor takes its tile (Arturo, 19/10: «que ocupen»):
    // anything drawn with a body to it (above the knee, or that one would walk
    // round) stands where nobody walks or stops. The generator decides every such
    // thing here, last, over the finished map: the armchairs, coffee tables,
    // lamps, plants, bedside tables, towel rails and coat stands of the houses;
    // a café's tables, a bookshop's or a library's reading tables, a grocer's
    // drinks fridge; a barn's tools and wheelbarrow; the hydrants and mailboxes
    // by the kerbs. Each is decor (decor.ts) on a `low` tile (the fridge `high`,
    // as a kitchen's is), and the client only draws what is here
    // (homeFurnishing.ts, dressing.ts). Flat things stay the client's and stay
    // walkable: rugs, papers, stains, blood, small litter, the dead lying.
    //
    // Nothing goes where it would close a way: never by a door, a ladder or
    // stairs, a window one may vault or a stair's landing, nor where anybody is
    // placed, and only where its neighbours stay joined round it (keepsWays): so
    // every room stays one, and everything reached before is reached still. A
    // room keeps most of its floor (MAX_SHARE). On a seed of its own: the rest of
    // the layout doesn't move.
    public static class Furnishing
    {
        /// <summary>The furniture furnishing.ts puts on tiles of its own (the client draws each, as its own: homeFurnishing.ts, dressing.ts).</summary>
        public static readonly IReadOnlyCollection<string> FurnitureOfItsOwn = new HashSet<string>
        {
            DecorKinds.Armchair,
            DecorKinds.CoffeeTable,
            DecorKinds.FloorLamp,
            DecorKinds.PottedPlant,
            DecorKinds.Nightstand,
            DecorKinds.DiningChair,
            DecorKinds.TowelRack,
            DecorKinds.CoatRack,
            DecorKinds.CafeTable,
            DecorKinds.ReadingTable,
            DecorKinds.DrinksFridge,
            DecorKinds.Wheelbarrow,
            DecorKinds.Tools,
            DecorKinds.Hydrant,
            DecorKinds.Mailbox,
            DecorKinds.Strewn,
        };

        /// <summary>The most of a room's floor its furniture takes (playtest 12/10: rooms are 3 × 3 or more, never mazes).</summary>
        private const double MaxShare = 0.4;
        /// <summary>How far round a tile keepsWays looks for the way round it.</summary>
        private const int Radius = 4;
        /// <summary>A kerb's hydrant or mailbox: the chance on each kerb tile, and how far from another (or a street light), by steps.</summary>
        private const double KerbChance = 0.035;
        private const int KerbGap = 2;

        /// <summary>What a door, a ladder, the stairs or a window is, and the tiles round them stay clear of.</summary>
        private static readonly HashSet<string> Ways = new HashSet<string> { TileKinds.Door, TileKinds.DoorOpen, TileKinds.Ladder, TileKinds.Window, TileKinds.WindowBroken };
        /// <summary>What one walks across inside a room (loot.ts roomFloor): its floor and its stairs, not a door nor a window.</summary>
        private static readonly HashSet<string> Pass = new HashSet<string> { TileKinds.Floor, TileKinds.Ladder };

        /// <summary>`WAYS.has(kind)`, null (open air) never one.</summary>
        internal static bool IsWay(string? kind)
        {
            return kind != null && Ways.Contains(kind);
        }

        /// <summary>
        /// Whether a blocking object on `p` of `layer` (on the ground, `elev`) keeps
        /// every way across it: its open neighbours stay joined round it, within
        /// RADIUS tiles on its floor, by floor (not through a door nor a window). Then
        /// whatever was reached before is reached still, by the movement rules (a
        /// diagonal past it needs both sides open: they are two of its neighbours) and
        /// the rooms stay one (loot.ts roomAt). `p` may be the tiles of an object over
        /// several (decor.ts objectTiles): the open neighbours of all of them stay
        /// joined round the whole of it. A flight of stairs of several tiles (with the
        /// layer's `decor`) is no way round: nobody steps off its sides.
        /// </summary>
        /// <remarks>TS `layer: { tiles; decor? }` is `tiles` and `decor` here; `p: Vec2 | readonly Vec2[]` the two overloads.</remarks>
        public static bool KeepsWays(IReadOnlyList<string?> tiles, IReadOnlyList<int>? decor, int width, int height, Vec2 p, IReadOnlyList<int>? elev = null)
        {
            return KeepsWays(tiles, decor, width, height, new[] { p }, elev);
        }

        /// <summary>keepsWays for the tiles of an object over several (decor.ts objectTiles).</summary>
        public static bool KeepsWays(IReadOnlyList<string?> tiles, IReadOnlyList<int>? decor, int width, int height, IReadOnlyList<Vec2> p, IReadOnlyList<int>? elev = null)
        {
            int At(int x, int y) => y * width + x;
            var own = p;
            var first = own[0];
            var level = Js.At(elev, At(first.X, first.Y), 0);
            var taken = new HashSet<int>(own.Select((q) => At(q.X, q.Y)));
            var x0 = own.Min((q) => q.X) - Radius;
            var x1 = own.Max((q) => q.X) + Radius;
            var y0 = own.Min((q) => q.Y) - Radius;
            var y1 = own.Max((q) => q.Y) + Radius;
            bool Passes(int x, int y) =>
                x >= 0 && y >= 0 && x < width && y < height && x >= x0 && x <= x1 && y >= y0 && y <= y1 && !taken.Contains(At(x, y)) && IsPass(Js.At(tiles, At(x, y), null)) && Js.At(elev, At(x, y), 0) == level &&
                !Flights.LongFlightTile(tiles, decor, width, At(x, y));
            var near = new List<Vec2>();
            foreach (var q in own)
            {
                foreach (var d in Grid.Dirs4)
                {
                    var n = new Vec2 { X = q.X + d.X, Y = q.Y + d.Y };
                    if (Passes(n.X, n.Y) && !near.Any((o) => o.X == n.X && o.Y == n.Y)) near.Add(n);
                }
            }
            if (near.Count <= 1) return true;
            var seen = new HashSet<int> { At(near[0].X, near[0].Y) };
            var queue = new List<Vec2> { near[0] };
            while (queue.Count > 0)
            {
                var q = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                foreach (var d in Grid.Dirs4)
                {
                    var x = q.X + d.X;
                    var y = q.Y + d.Y;
                    if (!Passes(x, y) || seen.Contains(At(x, y))) continue;
                    seen.Add(At(x, y));
                    queue.Add(new Vec2 { X = x, Y = y });
                }
            }
            return near.All((q) => seen.Contains(At(q.X, q.Y)));
        }

        /// <summary>`PASS.has(kind)`, null (open air) never one.</summary>
        private static bool IsPass(string? kind)
        {
            return kind != null && Pass.Contains(kind);
        }

        /// <summary>
        /// The flood fill of each storey (the ground first): every walkable tile
        /// (doors, stairs, ladders and windows too) by the piece of that storey it is
        /// joined to, side by side and on the ground at its height; -1 for the rest,
        /// and for a flight of stairs of several tiles (nobody steps off its sides).
        /// The ways between storeys (stairs, ladders, drops) start and end on tiles
        /// nothing is ever put on or beside (Furnisher.free), so pieces joined as
        /// before mean every place reached as before.
        /// </summary>
        public static List<int[]> StoreyPieces(FurnishMap map)
        {
            var w = map.Width;
            var h = map.Height;
            var layers = new List<IReadOnlyList<string?>> { map.Tiles };
            layers.AddRange(map.Levels.Select((l) => (IReadOnlyList<string?>)l.Tiles));
            return layers.Select((tiles, z) =>
            {
                var piece = new int[w * h];
                for (var k = 0; k < piece.Length; k++) piece[k] = -1;
                int Level(int i) => z != 0 ? 0 : Js.At(map.Elev, i, 0);
                IReadOnlyList<int>? decor = z != 0 ? map.Levels[z - 1].Decor : map.Decor;
                bool Open(int i) => Grid.IsWalkable(Js.At(tiles, i, null)) && !Flights.LongFlightTile(tiles, decor, w, i);
                var n = 0;
                for (var start = 0; start < w * h; start++)
                {
                    if (piece[start] != -1 || !Open(start)) continue;
                    piece[start] = n;
                    var queue = new List<int> { start };
                    while (queue.Count > 0)
                    {
                        var i = queue[queue.Count - 1];
                        queue.RemoveAt(queue.Count - 1);
                        var x = i % w;
                        foreach (var j in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, i - w, i + w })
                        {
                            if (j < 0 || j >= w * h || piece[j] != -1 || !Open(j) || Level(j) != Level(i)) continue;
                            piece[j] = n;
                            queue.Add(j);
                        }
                    }
                    n++;
                }
                return piece;
            }).ToList();
        }

        /// <summary>Whether what was joined (`before`, storeyPieces) is joined still (`after`), wherever it can still be walked.</summary>
        public static bool JoinedAsBefore(List<int[]> before, List<int[]> after)
        {
            return before.Select((was, z) => (was, z)).All((e) =>
            {
                var was = e.was;
                var now = after[e.z];
                var into = new Dictionary<int, int>();
                for (var i = 0; i < was.Length; i++)
                {
                    if (was[i] < 0 || now[i] < 0) continue;
                    if (!into.TryGetValue(was[i], out var seen)) into[was[i]] = now[i];
                    else if (seen != now[i]) return false;
                }
                return true;
            });
        }

        /// <summary>Every place the movement rules reach from `from` (path.ts stepsFrom), by node (grid.ts nodeOf), nobody in the way.</summary>
        public static JsSet<int> ReachableFrom(FurnishMap map, Pos from)
        {
            var s = new GameState { Width = map.Width, Height = map.Height, Tiles = map.Tiles, Elev = map.Elev, Decor = map.Decor, Levels = map.Levels.Count > 0 ? map.Levels : null };
            int Node(Pos p) => (p.Z * map.Height + p.Y) * map.Width + p.X;
            var seen = new JsSet<int>(new[] { Node(from) });
            var queue = new List<Pos> { from };
            while (queue.Count > 0)
            {
                var p = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                foreach (var d in Grid.Dirs8)
                {
                    foreach (var step in Paths.StepsFrom(s, p, d.X, d.Y))
                    {
                        var to = step.To;
                        var n = Node(to);
                        if (seen.Has(n)) continue;
                        seen.Add(n);
                        queue.Add(to);
                    }
                }
            }
            return seen;
        }

        /// <summary>
        /// The look of a building as the client works it out (buildings.ts `look`,
        /// from its first corner): the same number, so a shop is the trade its
        /// shelves show (a bookshop's are books).
        /// </summary>
        public static double BuildingLook(int x, int y)
        {
            unchecked
            {
                // Math.imul is the low 32 bits of the product; the sum of three of them, taken by `^` and `>>>` as 32 bits,
                // is the same as wrapping each addition.
                var h = (x + 1) * 374761393 + (y + 7) * 668265263 + (5 + 3) * 2147483647;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>A shop's trade by its look (the client's homeFurnishing.ts shopTrade, the same split).</summary>
        /// <returns>`'books' | 'cafe' | 'grocer'` (ShopTrades).</returns>
        public static string TradeOf(double look)
        {
            return look < 0.3 ? ShopTrades.Books : look < 0.55 ? ShopTrades.Cafe : ShopTrades.Grocer;
        }

        private static Vec2 Step(Vec2 p, int f)
        {
            return new Vec2 { X = p.X + Grid.Dirs4[f].X, Y = p.Y + Grid.Dirs4[f].Y };
        }

        private static List<T> Shuffled<T>(List<T> xs, Rng rng)
        {
            for (var i = xs.Count - 1; i > 0; i--)
            {
                var j = rng.Int(i + 1);
                var t = xs[i];
                xs[i] = xs[j];
                xs[j] = t;
            }
            return xs;
        }

        /// <summary>furnishHome's `things`: `{ p, ...look }`.</summary>
        private sealed class Thing
        {
            public Vec2 P = new Vec2();
            public string Kind = "";
            public int Facing;
        }

        /// <summary>
        /// A house's rooms, by what they are (playtest 18/10, #36, now on tiles of
        /// their own): in the living room an armchair across the table from the sofa
        /// (or a coffee table before it) and another at its side, a standard lamp and
        /// a plant in the corners; a plant in the kitchen's; a bedside table by each
        /// bed and a chair or a plant in a corner of the bedroom; a towel rail on the
        /// bathroom's wall; a plant in the study; a coat stand in the hall, and a
        /// plant. Each only where it fits (Furnisher.put) and while the room keeps
        /// most of its floor.
        /// </summary>
        private static void FurnishHome(Furnisher f, HomeSite home, Rng rng)
        {
            foreach (var room in home.Rooms)
            {
                var z = room.Z;
                if (f.Layer(z) == null) continue;
                bool InRoom(Vec2 p) => p.X >= room.X && p.Y >= room.Y && p.X < room.X + room.W && p.Y < room.Y + room.H;
                var cells = new List<Vec2>();
                for (var y = room.Y; y < room.Y + room.H; y++) for (var x = room.X; x < room.X + room.W; x++) cells.Add(new Vec2 { X = x, Y = y });
                var taken = cells.Count((p) =>
                {
                    var kind = f.TileAt(z, p.X, p.Y);
                    return kind == TileKinds.Low || kind == TileKinds.High;
                });
                var budget = (int)Math.Floor(room.W * room.H * MaxShare) - taken;
                bool Put(Vec2 p, string look, int facing = 0, string kind = TileKinds.Low)
                {
                    if (budget <= 0 || !InRoom(p) || !f.Put(z, p, kind, look, facing)) return false;
                    budget--;
                    return true;
                }
                var things = cells.SelectMany((p) =>
                {
                    var look = f.LookAt(z, p.X, p.Y);
                    return look != null ? new[] { new Thing { P = p, Kind = look.Kind, Facing = look.Facing } } : new Thing[0];
                }).ToList();
                List<Thing> Of(string kind) => things.Where((t) => t.Kind == kind).ToList();
                // The way into the room's middle from p (furnishRoom's inward).
                int Inward(Vec2 p)
                {
                    var cx = room.X + (room.W - 1) / 2.0;
                    var cy = room.Y + (room.H - 1) / 2.0;
                    return Math.Abs(cx - p.X) > Math.Abs(cy - p.Y) ? (cx > p.X ? 1 : 3) : cy > p.Y ? 2 : 0;
                }
                var corners = Shuffled(
                    new List<Vec2>
                    {
                        new Vec2 { X = room.X, Y = room.Y },
                        new Vec2 { X = room.X + room.W - 1, Y = room.Y },
                        new Vec2 { X = room.X, Y = room.Y + room.H - 1 },
                        new Vec2 { X = room.X + room.W - 1, Y = room.Y + room.H - 1 },
                    },
                    rng);
                bool Corner(string look) => corners.Any((c) => Put(c, look, Inward(c)));
                // The way to the wall beside p, if one is; -1 if none.
                int WallBy(Vec2 p) => FindIndex(Grid.Dirs4, (d) => f.TileAt(z, p.X + d.X, p.Y + d.Y) == TileKinds.Wall);
                switch (room.Use)
                {
                    case HomeUses.Living:
                    {
                        foreach (var sofa in Of(DecorKinds.Sofa))
                        {
                            var ahead = Step(sofa.P, sofa.Facing);
                            var table = f.ObjectAt(z, ahead.X, ahead.Y)?.Kind == DecorKinds.Table;
                            // Across the table, facing the sofa; or a coffee table before it.
                            if (table) Put(Step(ahead, sofa.Facing), DecorKinds.Armchair, (sofa.Facing + 2) % 4);
                            else Put(ahead, DecorKinds.CoffeeTable, sofa.Facing);
                            // At the side of the table (or of the sofa), turned to it.
                            foreach (var side in new[] { (sofa.Facing + 1) % 4, (sofa.Facing + 3) % 4 }) if (Put(Step(table ? ahead : sofa.P, side), DecorKinds.Armchair, (side + 2) % 4)) break;
                        }
                        Corner(DecorKinds.FloorLamp);
                        Corner(DecorKinds.PottedPlant);
                        break;
                    }
                    case HomeUses.Kitchen:
                        Corner(DecorKinds.PottedPlant);
                        break;
                    case HomeUses.Bedroom:
                        // By its head: beside the anchor, or beside the other tile of the head of a double bed (docs/MAPAS.md, phase 1).
                        foreach (var bed in Of(DecorKinds.Bed))
                        {
                            var d = Grid.Dirs4[bed.Facing];
                            var head = f.TilesOf(z, bed.P).Where((q) => (q.X - bed.P.X) * d.X + (q.Y - bed.P.Y) * d.Y == 0).ToList();
                            head.Any((q) => new[] { (bed.Facing + 1) % 4, (bed.Facing + 3) % 4 }.Any((side) => Put(Step(q, side), DecorKinds.Nightstand, bed.Facing)));
                        }
                        Corner(rng.Next() < 0.5 ? DecorKinds.DiningChair : DecorKinds.PottedPlant);
                        break;
                    case HomeUses.Bath:
                    {
                        var spots = corners.Concat(room.W > 2 ? new[] { new Vec2 { X = room.X + 1, Y = room.Y } } : new Vec2[0]).Where((p) => WallBy(p) >= 0).ToList();
                        spots.Any((p) => Put(p, DecorKinds.TowelRack, (WallBy(p) + 2) % 4));
                        break;
                    }
                    case HomeUses.Study:
                        Corner(DecorKinds.PottedPlant);
                        break;
                    case HomeUses.Hall:
                        corners.Where((p) => WallBy(p) >= 0).ToList().Any((p) => Put(p, DecorKinds.CoatRack, (WallBy(p) + 2) % 4));
                        Corner(DecorKinds.PottedPlant);
                        break;
                }
            }
        }

        /// <summary>`DIRS4.findIndex(test)`: -1 if none.</summary>
        private static int FindIndex(IReadOnlyList<Vec2> dirs, Func<Vec2, bool> test)
        {
            for (var k = 0; k < dirs.Count; k++) if (test(dirs[k])) return k;
            return -1;
        }

        /// <summary>
        /// A shop's floor by its trade (tradeOf, as the client shows it): a
        /// bookshop's reading table, a café's tables (their chairs on their tiles), a
        /// grocer's drinks fridge against a wall; a library's reading tables. Tables
        /// stand out on the open floor, with free floor all round them.
        /// </summary>
        private static void FurnishShop(Furnisher f, FurnishSite site, Rng rng)
        {
            var look = BuildingLook(site.X, site.Y);
            if (site.Kind == FurnishSiteKinds.Hall && look >= 0.3) return;
            var trade = site.Kind == FurnishSiteKinds.Hall ? ShopTrades.Books : TradeOf(look);
            var inner = new List<Vec2>();
            for (var y = site.Y + 1; y < site.Y + site.H - 1; y++) for (var x = site.X + 1; x < site.X + site.W - 1; x++) inner.Add(new Vec2 { X = x, Y = y });
            var area = inner.Count;
            var order = Shuffled(new List<Vec2>(inner), rng);
            // Open floor all round (a table and its chairs stand clear of walls and furniture).
            bool Open(Vec2 p)
            {
                foreach (var d in new[] { new Vec2 { X = 0, Y = 0 } }.Concat(Grid.Dirs8)) if (f.TileAt(0, p.X + d.X, p.Y + d.Y) != TileKinds.Floor || f.LookAt(0, p.X + d.X, p.Y + d.Y) != null) return false;
                return true;
            }
            void Tables(int max, string kind)
            {
                var n = 0;
                foreach (var p in order)
                {
                    if (n >= max) break;
                    if (Open(p) && f.Put(0, p, TileKinds.Low, kind, rng.Int(4))) n++;
                }
            }
            if (site.Kind == FurnishSiteKinds.Hall) Tables(Math.Max(2, (int)Math.Floor(area / 18.0)), DecorKinds.ReadingTable);
            else if (trade == ShopTrades.Books) Tables(1, DecorKinds.ReadingTable);
            else if (trade == ShopTrades.Cafe) Tables(Math.Max(1, (int)Math.Floor(area / 12.0)), DecorKinds.CafeTable);
            else
            {
                // The drinks fridge against a wall, its door to the room.
                foreach (var p in order)
                {
                    var wall = FindIndex(Grid.Dirs4, (d) => f.TileAt(0, p.X + d.X, p.Y + d.Y) == TileKinds.Wall);
                    if (wall >= 0 && f.Put(0, p, TileKinds.High, DecorKinds.DrinksFridge, (wall + 2) % 4)) break;
                }
            }
        }

        /// <summary>A barn's floor (playtest 18/10, #53): its tools leant against the walls, three at most, and a wheelbarrow out on the floor.</summary>
        private static void FurnishBarn(Furnisher f, FurnishSite site, Rng rng)
        {
            var inner = new List<Vec2>();
            for (var y = site.Y + 1; y < site.Y + site.H - 1; y++) for (var x = site.X + 1; x < site.X + site.W - 1; x++) inner.Add(new Vec2 { X = x, Y = y });
            var tools = 0;
            var barrow = rng.Next() < 0.8;
            foreach (var p in Shuffled(inner, rng))
            {
                var wall = FindIndex(Grid.Dirs4, (d) => f.TileAt(0, p.X + d.X, p.Y + d.Y) == TileKinds.Wall);
                if (wall >= 0 && tools < 3)
                {
                    if (f.Put(0, p, TileKinds.Low, DecorKinds.Tools, wall)) tools++;
                }
                else if (wall < 0 && barrow && f.Put(0, p, TileKinds.Low, DecorKinds.Wheelbarrow, rng.Int(4))) barrow = false;
            }
        }

        /// <summary>
        /// A town's kerbs (the streets' dressing, dressing.ts): now and then a fire
        /// hydrant or a mailbox on the pavement by the street, apart from each other
        /// and from the street lights (`lamps`), facing the street.
        /// </summary>
        private static void FurnishKerbs(Furnisher f, IReadOnlyList<Vec2> lamps, Rng rng)
        {
            var w = f.Map.Width;
            var h = f.Map.Height;
            var ground = f.Map.Ground;
            if (ground == null) return;
            var placed = new List<Vec2>(lamps);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    // `ground[i] !== 0`: past its end, undefined, which is not 0.
                    if (Js.At(ground, i, -1) != 0 || Js.At(f.Map.Elev, i, 0) != 0) continue;
                    var street = FindIndex(Grid.Dirs4, (d) =>
                    {
                        var nx = x + d.X;
                        var ny = y + d.Y;
                        return nx >= 0 && ny >= 0 && nx < w && ny < h && Js.At(ground, ny * w + nx, -1) == 1;
                    });
                    if (street < 0 || rng.Next() >= KerbChance) continue;
                    if (placed.Any((q) => Math.Abs(q.X - x) + Math.Abs(q.Y - y) <= KerbGap)) continue;
                    if (f.Put(0, new Vec2 { X = x, Y = y }, TileKinds.Low, rng.Next() < 0.6 ? DecorKinds.Hydrant : DecorKinds.Mailbox, street)) placed.Add(new Vec2 { X = x, Y = y });
                }
            }
        }

        /// <summary>
        /// Furnishes a finished map (its final frame): the houses' rooms, the shops,
        /// libraries and barns, and in town the kerbs. Returns the Furnisher, whose
        /// `strew` the dressing that stands up uses after (homeDressing,
        /// medicalAbandonment), and which undoes it all if the map lost a way.
        /// </summary>
        /// <remarks>TS `kerbs: { lamps } | null` is `lamps` here, null for none.</remarks>
        public static Furnisher FurnishMap(FurnishMap map, IReadOnlyList<HomeSite> homes, IReadOnlyList<FurnishSite> sites, int seed, IReadOnlyList<Pos> reserved, IReadOnlyList<Vec2>? lamps)
        {
            var f = new Furnisher(map, reserved);
            var rng = new Rng(unchecked((uint)(seed ^ 0x6675726e)));
            foreach (var home in homes) FurnishHome(f, home, rng);
            foreach (var site in sites)
            {
                if (site.Kind == FurnishSiteKinds.Barn) FurnishBarn(f, site, rng);
                else FurnishShop(f, site, rng);
            }
            if (lamps != null) FurnishKerbs(f, lamps, rng);
            return f;
        }
    }

    /// <summary>
    /// Puts the things that take a tile on a map, keeping its ways (keepsWays),
    /// clear of the places in `reserved` (where somebody starts, the objective,
    /// what lies on the floor) and of every way through a door, up a ladder or
    /// stairs, out of a window and off a stair's landing. Every change is kept, to
    /// be undone (undo) if the map lost a way after all.
    /// </summary>
    public sealed class Furnisher
    {
        /// <summary>One level of the map: what stands on each tile, and what it is meant to be (furnishing.ts `Layer`).</summary>
        public sealed class LayerView
        {
            public List<string?> Tiles { get; }
            public List<int> Decor { get; }

            public LayerView(List<string?> tiles, List<int> decor)
            {
                Tiles = tiles;
                Decor = decor;
            }
        }

        /// <summary>A change to undo: what stood on a place before an object was put there.</summary>
        private sealed class Change
        {
            public int Z;
            public int I;
            public string? Kind;
            public int Decor;
        }

        private readonly List<Change> changes = new List<Change>();
        private readonly HashSet<(int, int, int)> reserved;
        private readonly HashSet<(int, int, int)> landings = new HashSet<(int, int, int)>();

        public FurnishMap Map { get; }

        public Furnisher(FurnishMap map, IReadOnlyList<Pos> reserved)
        {
            Map = map;
            this.reserved = new HashSet<(int, int, int)>(reserved.Select((p) => (p.X, p.Y, p.Z)));
            // A building's stairs land on the tile they face, a storey up.
            for (var z = 0; z < map.Levels.Count; z++)
            {
                var layer = Layer(z);
                if (layer == null) continue;
                for (var i = 0; i < layer.Tiles.Count; i++)
                {
                    var look = layer.Tiles[i] == TileKinds.Ladder ? Engine.Decor.DecorOf(Js.At(layer.Decor, i, 0)) : null;
                    if (look?.Kind != DecorKinds.Stairs) continue;
                    var d = Grid.Dirs4[look.Facing];
                    landings.Add(((i % map.Width) + d.X, (int)Math.Floor((double)i / map.Width) + d.Y, z + 1));
                }
            }
        }

        /// <summary>Level `z` (0 the ground), its decor made if it had none.</summary>
        public LayerView? Layer(int z)
        {
            // The ground's tiles are never null: List<string> and List<string?> are the same list.
            if (z == 0) return new LayerView(Map.Tiles!, Map.Decor);
            var level = z - 1 >= 0 && z - 1 < Map.Levels.Count ? Map.Levels[z - 1] : null;
            if (level == null) return null;
            level.Decor ??= Enumerable.Repeat(0, Map.Width * Map.Height).ToList();
            return new LayerView(level.Tiles, level.Decor);
        }

        public string? TileAt(int z, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) return null;
            var layer = Layer(z);
            return layer != null ? Js.At(layer.Tiles, y * Map.Width + x, null) : null;
        }

        public DecorLook? LookAt(int z, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) return null;
            var layer = Layer(z);
            return Engine.Decor.DecorOf(layer != null ? Js.At(layer.Decor, y * Map.Width + x, 0) : 0);
        }

        /// <summary>What the object standing on (x, y) of level z is: for another tile of one over several (a part), its anchor's look.</summary>
        public DecorLook? ObjectAt(int z, int x, int y)
        {
            var o = Object(z, x, y);
            return o != null ? new DecorLook { Kind = o.Kind, Facing = o.Facing } : null;
        }

        /// <summary>The tiles of the object standing on p of level z (decor.ts objectAt: all of one over several), p alone for none.</summary>
        public List<Vec2> TilesOf(int z, Vec2 p)
        {
            var tiles = Object(z, p.X, p.Y)?.Tiles ?? new List<int> { p.Y * Map.Width + p.X };
            return tiles.Select((i) => new Vec2 { X = i % Map.Width, Y = (int)Math.Floor((double)i / Map.Width) }).ToList();
        }

        private DecorObject? Object(int z, int x, int y)
        {
            var layer = Layer(z);
            if (layer == null || x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) return null;
            return Engine.Decor.ObjectAt(layer.Decor, Map.Width, y * Map.Width + x);
        }

        /// <summary>Whether an object may go on (x, y) of level z: plain floor, nothing on it nor anybody's, off every way through.</summary>
        public bool Free(int z, Vec2 p)
        {
            if (TileAt(z, p.X, p.Y) != TileKinds.Floor || LookAt(z, p.X, p.Y) != null) return false;
            var key = (p.X, p.Y, z);
            if (reserved.Contains(key) || landings.Contains(key)) return false;
            // Beside a door, a ladder or the stairs, a window, or a stairwell's open air upstairs.
            return !Grid.Dirs4.Any((d) =>
            {
                var x = p.X + d.X;
                var y = p.Y + d.Y;
                if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) return false;
                var kind = TileAt(z, x, y);
                return Furnishing.IsWay(kind) || (z > 0 && kind == null);
            });
        }

        /// <summary>Puts `look` on (x, y) of level z, as `kind` cover, if it may go there and keeps every way; false if not.</summary>
        /// <param name="kind">`'low' | 'high'`.</param>
        public bool Put(int z, Vec2 p, string kind, string look, int facing = 0)
        {
            var layer = Layer(z);
            if (layer == null || !Free(z, p) || !Furnishing.KeepsWays(layer.Tiles, layer.Decor, Map.Width, Map.Height, p, z != 0 ? null : Map.Elev)) return false;
            Block(z, p, kind, look, facing);
            return true;
        }

        /// <summary>Blocks a floor tile for what lies there and stands up (homeDressing, medicalAbandonment), on the same terms as put.</summary>
        public bool Strew(Pos p)
        {
            return Put(p.Z, p, TileKinds.Low, DecorKinds.Strewn);
        }

        private void Block(int z, Vec2 p, string kind, string look, int facing)
        {
            var layer = Layer(z)!;
            var i = p.Y * Map.Width + p.X;
            changes.Add(new Change { Z = z, I = i, Kind = Js.At(layer.Tiles, i, null), Decor = Js.At(layer.Decor, i, 0) });
            layer.Tiles[i] = kind;
            layer.Decor[i] = Engine.Decor.DecorCode(look, facing);
        }

        /// <summary>Takes everything put back off, last first.</summary>
        public void Undo()
        {
            changes.Reverse();
            foreach (var c in changes)
            {
                var layer = Layer(c.Z)!;
                layer.Tiles[c.I] = c.Kind;
                layer.Decor[c.I] = c.Decor;
            }
            changes.Clear();
        }
    }
}
