#nullable enable
// Procedural battlefields.
//
// Every map is GEN_SIZE×GEN_SIZE and is laid out in a canonical frame: the squad
// starts in the top-left corner, the objective lies in the far (bottom-right)
// area and the evacuation zone near the top-right corner. The finished map is
// then mirrored and/or transposed at random, so the squad may start anywhere.
//
// City and facility maps are districts: a jittered binary split of the ground
// into blocks of varied sizes, with streets (or yards) 2–4 tiles wide between
// them that may jog sideways or end against a block. Each block holds one
// program: a row of houses or shops, an office, a block of flats, a warehouse,
// a plaza, a car park; labs, hangars, loading platforms, a reactor or storage
// rows inside a facility's perimeter. Wilds are open country shaped by noise:
// groves with clearings between them, rock outcrops, blob-shaped hills, fenced
// fields and a dirt track with a farmstead or ruins along it.
//
// Buildings have real rooms (at least 3×3, usually 4–6 tiles a side), a door
// on the street side and doors between rooms, and windows at a regular spacing.
// All the houses (offices, halls…) of one map share a scale. A final pass makes
// sure every walkable area can be reached (adding ladders or doors if needed),
// and only reachable tiles are used to place the squad, the pods and objectives.
//
// mapgen.ts in C#: a partial class over Mapgen/*.cs, in the module's order. This file is its first part (the sizes,
// the rects, the canvas, objects and cover groups, the style and the buildings' type) and the types the other parts
// share (TypeScript's interfaces and string unions, as classes).
using System;
using System.Collections.Generic;
using System.Linq;
using Rect = Bastion.Engine.PlanRect;

namespace Bastion.Engine
{
    public static partial class Mapgen
    {
        /// <summary>Side of the procedural maps not yet laid out to scale (docs/MAPAS.md §4.8: the outskirts, the factory, the estate).</summary>
        public const int GenSize = 48;
        /// <summary>A town's side (docs/MAPAS.md §4.8, decided by Arturo 19/10): 64, both a grid and an old town; a hospital's town, 72 × 64.</summary>
        public const int TownSize = 64;
        private static readonly IReadOnlyList<int> HospitalSize = new[] { 72, 64 };
        /// <summary>The outskirts' side, and the service area's (docs/MAPAS.md §4.8).</summary>
        private const int CountrySize = 72;
        private static readonly IReadOnlyList<int> RoadsideSize = new[] { 64, 72 };

        /// <summary>Whether a generated map is a town laid out on a plan (townPlan.ts): a city but the industrial estate.</summary>
        private static bool IsTown(string biome, string? site = null) => biome == Biomes.City && site != Sites.Industrial;
        /// <summary>Whether a generated map is the industrial estate, on a plan too (estate).</summary>
        private static bool IsEstate(string biome, string? site = null) => biome == Biomes.City && site == Sites.Industrial;

        /// <summary>A generated map's size before it is turned (generateLayout may transpose it).</summary>
        public static (int Width, int Height) GeneratedSize(string biome, string? site = null, bool hospital = false)
        {
            // The country (docs/MAPAS.md §4.8, phase 5): the outskirts 72, the service area 64 × 72.
            if (biome == Biomes.Wilds) return site == Sites.Roadside ? (RoadsideSize[0], RoadsideSize[1]) : (CountrySize, CountrySize);
            if (IsEstate(biome, site)) return (EstateSize, EstateSize);
            if (biome == Biomes.Facility) return (FactorySize, FactorySize);
            if (!IsTown(biome, site)) return (GenSize, GenSize);
            return hospital ? (HospitalSize[0], HospitalSize[1]) : (TownSize, TownSize);
        }

        /// <summary>Tiles reserved for the squad at the start.</summary>
        public const int MaxSquad = 8;
        /// <summary>No pod starts closer than this to the middle of the squad.</summary>
        private const int PodDistance = 16;

        // GroundKind (Map.cs).
        private const int Open = 0;
        private const int Road = 1;
        private const int Indoor = 2;
        /// <summary>Asphalt that isn't a street (a forecourt, a yard, a car park off the road): no road markings.</summary>
        private const int Yard = 3;
        /// <summary>Out of play: the outline round a town (docs/MAPAS.md §5.10), 'wall' drawn as its piece of the frame.</summary>
        private const int Out = 4;

        // ------------------------------------------------------------------- rects

        // interface Rect { x; y; w; h } is PlanRect (townPlan.ts PlanRect, the same shape): `using Rect = PlanRect`.

        /// <summary>
        /// A point that may lie between tiles, where TypeScript's Vec2 holds a fraction: a rect's middle (centre), and
        /// what is worked out from it. Every Vec2 is one.
        /// </summary>
        private readonly struct Pt
        {
            public readonly double X;
            public readonly double Y;

            public Pt(double x, double y)
            {
                X = x;
                Y = y;
            }

            public static implicit operator Pt(Vec2 v) => new Pt(v.X, v.Y);
        }

        private static Vec2 V(int x, int y) => new Vec2 { X = x, Y = y };

        private static Rect Rect(int x, int y, int w, int h) => new Rect { X = x, Y = y, W = w, H = h };
        private static Rect Inset(Rect r, int n) => Rect(r.X + n, r.Y + n, r.W - 2 * n, r.H - 2 * n);
        private static bool InRect(Rect r, Vec2 p) => p.X >= r.X && p.Y >= r.Y && p.X < r.X + r.W && p.Y < r.Y + r.H;
        private static bool InRect(Rect r, Pt p) => p.X >= r.X && p.Y >= r.Y && p.X < r.X + r.W && p.Y < r.Y + r.H;
        private static Pt Centre(Rect r) => new Pt(r.X + (r.W - 1) / 2.0, r.Y + (r.H - 1) / 2.0);
        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));
        private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
        private static double Dist(Pt a, Pt b) => Js.Hypot(a.X - b.X, a.Y - b.Y);
        private static Vec2 Add(Vec2 a, Vec2 b) => V(a.X + b.X, a.Y + b.Y);
        private static Pt Add(Pt a, Pt b) => new Pt(a.X + b.X, a.Y + b.Y);

        // Sides are numbered like DIRS4 (N, E, S, W): DIRS4[s] points out of side s.
        private static int Opposite(int s) => (s + 2) % 4;
        /// <summary>Length of side `s` of `r`.</summary>
        private static int SideLen(Rect r, int s) => s % 2 == 0 ? r.W : r.H;
        /// <summary>Depth of `r` seen from side `s`.</summary>
        private static int DepthOf(Rect r, int s) => s % 2 == 0 ? r.H : r.W;

        /// <summary>
        /// Rectangle inside `r` in the frame of side `s`: `u` runs along that side (the
        /// same way for opposite sides) and `d` is the depth from it.
        /// </summary>
        private static Rect Local(Rect r, int s, int u, int d, int lu, int ld)
        {
            switch (s)
            {
                case 0:
                    return Rect(r.X + u, r.Y + d, lu, ld);
                case 2:
                    return Rect(r.X + u, r.Y + r.H - d - ld, lu, ld);
                case 3:
                    return Rect(r.X + d, r.Y + u, ld, lu);
                default:
                    return Rect(r.X + r.W - d - ld, r.Y + u, ld, lu);
            }
        }

        private static Vec2 LocalPt(Rect r, int s, int u, int d)
        {
            var q = Local(r, s, u, d, 1, 1);
            return V(q.X, q.Y);
        }

        /// <summary>`r` without its first `n` tiles from side `s`.</summary>
        private static Rect Cut(Rect r, int s, int n) => Local(r, s, 0, n, SideLen(r, s), DepthOf(r, s) - n);

        private static List<Vec2> TilesIn(Rect r)
        {
            var out_ = new List<Vec2>();
            for (var y = r.Y; y < r.Y + r.H; y++) for (var x = r.X; x < r.X + r.W; x++) out_.Add(V(x, y));
            return out_;
        }

        private static List<T> Shuffle<T>(List<T> items, Rng rng)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = rng.Int(i + 1);
                var t = items[i];
                items[i] = items[j];
                items[j] = t;
            }
            return items;
        }

        /// <summary>The option with the lowest key; keys are computed once, so the result only depends on the rng.</summary>
        private static T? Best<T>(IEnumerable<T> items, Func<T, double> key) where T : class
        {
            T? pick = null;
            var low = double.PositiveInfinity;
            foreach (var item in items)
            {
                var k = key(item);
                if (k < low)
                {
                    low = k;
                    pick = item;
                }
            }
            return pick;
        }

        /// <summary>best() over numbers or tuples: null where TypeScript's gives undefined.</summary>
        private static T? BestOf<T>(IEnumerable<T> items, Func<T, double> key) where T : struct
        {
            T? pick = null;
            var low = double.PositiveInfinity;
            foreach (var item in items)
            {
                var k = key(item);
                if (k < low)
                {
                    low = k;
                    pick = item;
                }
            }
            return pick;
        }

        /// <summary>Array.prototype.findIndex over a table (DIRS4): -1 for none.</summary>
        private static int FindIndex<T>(IReadOnlyList<T> items, Func<T, bool> test)
        {
            for (var i = 0; i < items.Count; i++) if (test(items[i])) return i;
            return -1;
        }

        // ------------------------------------------------------------------ canvas

        /// <summary>A tile of an object over several and the decor it was given (Canvas.objects: `{ i: number; code: Decor }`).</summary>
        private sealed class ObjTile
        {
            public readonly int I;
            public readonly int Code;

            public ObjTile(int i, int code)
            {
                I = i;
                Code = code;
            }
        }

        /// <summary>Mutable terrain under construction. `keep` marks tiles that must stay free.</summary>
        private sealed class Canvas
        {
            public readonly int Width;
            public readonly int Height;
            public readonly List<string> Tiles;
            public readonly List<int> Elev;
            public readonly bool[] Keep;
            public readonly List<int> Ground;
            public readonly List<int> Decor;
            /// <summary>Every walled building raised so far, for their storeys (storeysOf).</summary>
            public readonly List<Building> Buildings = new List<Building>();
            /// <summary>The objects over several tiles put so far (putObject): each tile and the decor it was given, for tidyObjects.</summary>
            public readonly List<List<ObjTile>> Objects = new List<List<ObjTile>>();
            /// <summary>Where the squad comes in (the north-west corner, or a town's way in: docs/MAPAS.md §5.10).</summary>
            public Vec2 Squad = V(0, 0);
            /// <summary>The ground kept for the evacuation (the north-east corner), or null where it is laid later (a town's avenue).</summary>
            public Rect? EvacArea;
            /// <summary>The side of the map the squad comes in by, where it comes in by a way through the outline (docs/MAPAS.md §5.10).</summary>
            public int? SquadSide = null;
            /// <summary>What closes the map round what is played (docs/MAPAS.md §5.10): a town's outline, the country's.</summary>
            public readonly List<FramePiece> Frame = new List<FramePiece>();
            /// <summary>A town's block courtyards (docs/MAPAS.md §5.4), for MissionLayout.courtyards.</summary>
            public readonly List<Rect> Courtyards = new List<Rect>();
            /// <summary>Straight ways out to the edge the bus may leave by (a street, a track's last stretch), and the roads in line with them.</summary>
            public List<Entry> Exits = new List<Entry>(); // { road: PlanRoad; side: number }[]: townPlan.ts Entry.
            public List<PlanRoad> Lines = new List<PlanRoad>();
            /// <summary>The country's ways in and out through its trees (countryWays), wider than the road: kept open by shutCountry.</summary>
            public List<Rect> Mouths = new List<Rect>();

            public Canvas(int width, int height)
            {
                Width = width;
                Height = height;
                var n = width * height;
                Tiles = Enumerable.Repeat(TileKinds.Floor, n).ToList();
                Elev = Enumerable.Repeat(0, n).ToList();
                Keep = new bool[n];
                Ground = Enumerable.Repeat(Open, n).ToList();
                Decor = Enumerable.Repeat(0, n).ToList();
                EvacArea = Rect(width - Mapgen.EvacArea, 0, Mapgen.EvacArea, Mapgen.EvacArea);
            }

            public bool In(int x, int y)
            {
                return x >= 0 && y >= 0 && x < Width && y < Height;
            }

            public int I(int x, int y)
            {
                return y * Width + x;
            }

            public string Get(int x, int y)
            {
                return In(x, y) ? Tiles[I(x, y)] : TileKinds.Wall;
            }

            public int ElevAt(int x, int y)
            {
                return In(x, y) ? Elev[I(x, y)] : 0;
            }

            public int GroundAt(int x, int y)
            {
                return In(x, y) ? Ground[I(x, y)] : Open;
            }

            public void Force(int x, int y, string kind, int? elev = null)
            {
                if (!In(x, y)) return;
                Tiles[I(x, y)] = kind;
                if (elev != null) Elev[I(x, y)] = elev.Value;
            }

            /// <summary>Notes what the object on `p` is meant to be, and which way it faces.</summary>
            public void Dress(Vec2 p, string kind, int facing = 0)
            {
                if (In(p.X, p.Y)) Decor[I(p.X, p.Y)] = global::Bastion.Engine.Decor.DecorCode(kind, facing);
            }

            public void Paint(Rect r, int ground)
            {
                foreach (var p in TilesIn(r)) if (In(p.X, p.Y)) Ground[I(p.X, p.Y)] = ground;
            }

            public void Reserve(int x, int y)
            {
                if (In(x, y)) Keep[I(x, y)] = true;
            }

            /// <summary>Free floor that may receive an object.</summary>
            public bool Free(int x, int y, int elev = 0)
            {
                if (!In(x, y)) return false;
                var i = I(x, y);
                return Tiles[i] == TileKinds.Floor && !Keep[i] && Elev[i] == elev;
            }

            /// <summary>Untouched open ground (no road, no building, nothing reserved) over all of `r`.</summary>
            public bool Vacant(Rect r)
            {
                if (r.X < 0 || r.Y < 0 || r.X + r.W > Width || r.Y + r.H > Height) return false;
                return TilesIn(r).All((p) => Free(p.X, p.Y) && GroundAt(p.X, p.Y) == Open);
            }

            /// <summary>A view usable by the engine's movement rules (its tiles and elevations are the canvas's own lists).</summary>
            public GameState AsState()
            {
                return new GameState { Width = Width, Height = Height, Tiles = Tiles, Elev = Elev };
            }
        }

        /// <summary>What an object is meant to be (decor) and which way it faces (a DIRS4 index, north by default): `[DecorKind, number?]`.</summary>
        private sealed class Look
        {
            public readonly string Kind; // DecorKinds
            public readonly int? Facing;

            public Look(string kind, int? facing = null)
            {
                Kind = kind;
                Facing = facing;
            }
        }

        /// <summary>Puts an object on free floor (furniture, fences, rows: no spacing rules).</summary>
        private static bool Put(Canvas c, Vec2 p, string kind, Look? look = null, int elev = 0)
        {
            if (!c.Free(p.X, p.Y, elev)) return false;
            c.Force(p.X, p.Y, kind);
            if (look != null) c.Dress(p, look.Kind, look.Facing ?? 0);
            return true;
        }

        /// <summary>A tile of a group, from where it is put: `[number, number, TileKind, Look?]`.</summary>
        private sealed class Part
        {
            public readonly int X;
            public readonly int Y;
            public readonly string Kind; // TileKinds
            public readonly Look? Look;

            public Part(int x, int y, string kind, Look? look = null)
            {
                X = x;
                Y = y;
                Kind = kind;
                Look = look;
            }
        }

        /// <summary>
        /// The tiles of an object `wide` × `deep` from `at` (decor.ts FOOTPRINT): `deep` of them along DIRS4[along], `wide`
        /// along DIRS4[across]; `at` first.
        /// </summary>
        private static List<Vec2> Footprint(Vec2 at, int along, int across, int wide, int deep)
        {
            var a = Grid.Dirs4[along];
            var b = Grid.Dirs4[across];
            var out_ = new List<Vec2>();
            for (var i = 0; i < deep; i++) for (var j = 0; j < wide; j++) out_.Add(V(at.X + a.X * i + b.X * j, at.Y + a.Y * i + b.Y * j));
            return out_;
        }

        /// <summary>
        /// Dresses `cells` as one object (docs/MAPAS.md, phase 1): the first, its anchor, as `look` facing `facing`; every
        /// other one a part facing the step towards it, nearest first (decor.ts objectTiles). Kept in c.objects, so that
        /// whatever later takes a tile of it takes all of it (tidyObjects).
        /// </summary>
        private static void DressObject(Canvas c, IReadOnlyList<Vec2> cells, string look, int facing)
        {
            c.Dress(cells[0], look, facing);
            var done = new List<Vec2> { cells[0] };
            var left = cells.Skip(1).ToList();
            while (left.Count > 0)
            {
                var k = left.FindIndex((p) => done.Any((q) => Math.Abs(p.X - q.X) + Math.Abs(p.Y - q.Y) == 1));
                if (k < 0) break;
                var p = left[k];
                left.RemoveAt(k);
                var q = done.First((o) => Math.Abs(p.X - o.X) + Math.Abs(p.Y - o.Y) == 1);
                c.Dress(p, DecorKinds.Part, FindIndex(Grid.Dirs4, (d) => d.X == q.X - p.X && d.Y == q.Y - p.Y));
                done.Add(p);
            }
            c.Objects.Add(done.Select((p) => new ObjTile(c.I(p.X, p.Y), c.Decor[c.I(p.X, p.Y)])).ToList());
        }

        /// <summary>An object over several tiles on free floor (all of `cells`, as `kind` cover), its anchor the first; false if any is taken.</summary>
        private static bool PutObject(Canvas c, IReadOnlyList<Vec2> cells, string kind, string look, int facing, int elev = 0)
        {
            if (!cells.All((p) => c.Free(p.X, p.Y, elev) && c.Decor[c.I(p.X, p.Y)] == 0)) return false;
            foreach (var p in cells) c.Force(p.X, p.Y, kind);
            DressObject(c, cells, look, facing);
            return true;
        }

        /// <summary>The ways through a wall or up a storey: no furniture over several tiles stands beside one (doors, windows one vaults, ladders and stairs).</summary>
        private static readonly HashSet<string> WaysThrough = new HashSet<string> { TileKinds.Door, TileKinds.DoorOpen, TileKinds.Window, TileKinds.WindowBroken, TileKinds.Ladder };

        /// <summary>
        /// Furniture over several tiles (a bed, a sofa, a bath, a table, a hospital bed: decor.ts FOOTPRINT) on `cells` of
        /// `room`, if it closes no way: all of it on free floor of the room, none beside a door, a window, a ladder or the
        /// stairs, and the floor round it still joined round the whole of it (furnishing.ts keepsWays). False, and nothing
        /// put, if not: the room goes without it rather than lose a way (rooms of 3 × 3 or more, playtest 12/10).
        /// </summary>
        private static bool PutFurniture(Canvas c, Rect room, IReadOnlyList<Vec2> cells, string kind, string look, int facing)
        {
            if (!cells.All((p) => InRect(room, p) && c.Free(p.X, p.Y) && c.Decor[c.I(p.X, p.Y)] == 0)) return false;
            if (cells.Any((p) => Grid.Dirs4.Any((d) => WaysThrough.Contains(c.Get(p.X + d.X, p.Y + d.Y))))) return false;
            if (!Furnishing.KeepsWays(c.Tiles, c.Decor, c.Width, c.Height, cells, c.Elev)) return false;
            return PutObject(c, cells, kind, look, facing);
        }

        /// <summary>
        /// Furniture `wide` × `deep` in a corner of `room` (the first of `corners` it fits), its back to a wall: facing away
        /// from it as inward() has it (a bed's head to the long wall), else from the other wall of the corner; its width
        /// along the wall at its back, into the room.
        /// </summary>
        private static Vec2? CornerFurniture(Canvas c, Rect room, IReadOnlyList<Vec2> corners, string kind, string look, int wide, int deep)
        {
            foreach (var p in corners)
            {
                var first = Inward(room, p);
                var walls = new[] { 0, 1, 2, 3 }.Where((s) => !InRect(room, Add(p, Grid.Dirs4[s]))).ToList();
                var facings = new List<int> { first };
                facings.AddRange(walls.Select(Opposite).Where((f) => f != first && InRect(room, Add(p, Grid.Dirs4[f]))));
                foreach (var f in facings)
                {
                    int? across = null;
                    foreach (var a in new[] { (f + 1) % 4, (f + 3) % 4 })
                    {
                        if (!InRect(room, Add(p, Grid.Dirs4[a]))) continue;
                        across = a;
                        break;
                    }
                    if (across == null) continue;
                    if (PutFurniture(c, room, Footprint(p, f, across.Value, wide, deep), kind, look, f)) return p;
                }
            }
            return null;
        }

        /// <summary>
        /// Takes whatever clearing a tile left of an object over several (a car the evacuation, a lane or connect() cut
        /// through, a bed the stairs' storey cleared): the rest of it goes too, to bare floor. True if any did.
        /// </summary>
        private static bool TidyObjects(Canvas c)
        {
            var changed = false;
            for (var k = c.Objects.Count - 1; k >= 0; k--)
            {
                var tiles = c.Objects[k];
                if (tiles.All((t) => c.Decor[t.I] == t.Code && (c.Tiles[t.I] == TileKinds.Low || c.Tiles[t.I] == TileKinds.High))) continue;
                foreach (var t in tiles)
                {
                    if (c.Decor[t.I] != t.Code) continue;
                    c.Tiles[t.I] = TileKinds.Floor;
                    c.Decor[t.I] = 0;
                }
                c.Objects.RemoveAt(k);
                changed = true;
            }
            return changed;
        }

        /// <summary>Clears the object standing on `p` whole (all its tiles, if it stands over several), to bare floor.</summary>
        private static void ClearObject(Canvas c, Vec2 p)
        {
            var i = c.I(p.X, p.Y);
            foreach (var j in Decor.ObjectAt(c.Decor, c.Width, i)?.Tiles ?? new List<int> { i })
            {
                c.Tiles[j] = TileKinds.Floor;
                c.Decor[j] = 0;
            }
        }

        /// <summary>`parts` turned by `q` quarter turns (north turns to east first, like DIRS4); their looks turn with them.</summary>
        private static List<Part> Turn(IReadOnlyList<Part> parts, int q)
        {
            return parts.Select((p) =>
            {
                int tx, ty;
                if (q % 4 == 0)
                {
                    tx = p.X;
                    ty = p.Y;
                }
                else if (q % 4 == 1)
                {
                    tx = -p.Y;
                    ty = p.X;
                }
                else if (q % 4 == 2)
                {
                    tx = -p.X;
                    ty = -p.Y;
                }
                else
                {
                    tx = p.Y;
                    ty = -p.X;
                }
                return new Part(tx, ty, p.Kind, p.Look != null ? new Look(p.Look.Kind, ((p.Look.Facing ?? 0) + q) % 4) : null);
            }).ToList();
        }

        /// <summary>`parts` dressed as what they are meant to be, by kind of cover (`high` defaults to `low`).</summary>
        private static List<Part> Dressed(IReadOnlyList<Part> parts, string low, string? high = null)
        {
            return parts.Select((p) => new Part(p.X, p.Y, p.Kind, new Look(p.Kind == TileKinds.High ? high ?? low : low))).ToList();
        }

        /// <summary>
        /// A stable number for a position, for cosmetic choices (which street furniture,
        /// which room is a dining room) that must not draw from the generator's rng: the
        /// layout of a seed stays the same whatever the map is dressed as.
        /// </summary>
        private static int Mix(int x, int y)
        {
            // JavaScript multiplies exactly (the products fit in a double) and `^` takes them modulo 2³²: so does int.
            return (int)(unchecked((uint)((x * 73856093) ^ (y * 19349663))) % 997);
        }

        /// <summary>
        /// Places a group of cover objects that works as one firing position, if every
        /// tile is free and nothing else stands right around it, so groups never wall
        /// off a passage. `parts` are offsets from `at`.
        /// </summary>
        private static bool Group(Canvas c, Vec2 at, IReadOnlyList<Part> parts, int elev = 0)
        {
            var tiles = parts.Select((p) => new Part(at.X + p.X, at.Y + p.Y, p.Kind, p.Look)).ToList();
            bool Own(int x, int y) => tiles.Any((t) => t.X == x && t.Y == y);
            foreach (var t in tiles)
            {
                if (!c.Free(t.X, t.Y, elev)) return false;
                foreach (var d in Grid.Dirs8)
                {
                    var x = t.X + d.X;
                    var y = t.Y + d.Y;
                    if (!c.In(x, y) || Own(x, y)) continue;
                    var k = c.Get(x, y);
                    if (c.ElevAt(x, y) != elev || (k != TileKinds.Floor && k != TileKinds.Ladder)) return false;
                }
            }
            foreach (var t in tiles)
            {
                c.Force(t.X, t.Y, t.Kind);
                if (t.Look != null) c.Dress(V(t.X, t.Y), t.Look.Kind, t.Look.Facing ?? 0);
            }
            // A lorry, a container, a silo… is a block of its kind (decor.ts BLOCK_KINDS): what takes a tile of it takes it all (tidyObjects).
            var kind = tiles.Count > 0 ? tiles[0].Look?.Kind : null;
            if (tiles.Count > 1 && !string.IsNullOrEmpty(kind) && Decor.BlockKinds.Contains(kind!) && tiles.All((t) => t.Look?.Kind == kind)) c.Objects.Add(tiles.Select((t) => new ObjTile(c.I(t.X, t.Y), c.Decor[c.I(t.X, t.Y)])).ToList());
            return true;
        }

        private static Part P(int x, int y, string kind, Look? look = null) => new Part(x, y, kind, look);

        // Cover groups, drawn facing north (turned at random when placed).
        private static readonly IReadOnlyList<Part> Barricade = new[] { P(0, 0, TileKinds.Low), P(1, 0, TileKinds.Low), P(2, 0, TileKinds.Low) };
        private static readonly IReadOnlyList<Part> Corner = new[] { P(0, 0, TileKinds.High), P(1, 0, TileKinds.Low), P(0, 1, TileKinds.Low) };
        private static readonly IReadOnlyList<Part> Pair = new[] { P(0, 0, TileKinds.High), P(1, 0, TileKinds.Low) };
        private static readonly IReadOnlyList<Part> Crates = new[] { P(0, 0, TileKinds.High), P(1, 0, TileKinds.Low), P(0, 1, TileKinds.Low), P(1, 1, TileKinds.Low) };
        private static readonly IReadOnlyList<IReadOnlyList<Part>> Rocks = new IReadOnlyList<Part>[]
        {
            new[] { P(0, 0, TileKinds.High), P(1, 0, TileKinds.Low), P(-1, 1, TileKinds.Low), P(0, 1, TileKinds.Low) },
            new[] { P(0, 0, TileKinds.Low), P(1, 0, TileKinds.Low), P(1, 1, TileKinds.High), P(2, 1, TileKinds.Low) },
            new[] { P(0, 0, TileKinds.Low), P(1, 0, TileKinds.High), P(2, 0, TileKinds.Low), P(1, 1, TileKinds.Low), P(1, -1, TileKinds.Low) },
            new[] { P(0, 0, TileKinds.Low), P(1, 0, TileKinds.Low), P(0, 1, TileKinds.Low) },
        };
        private static readonly IReadOnlyList<IReadOnlyList<Part>> Outcrops = Rocks.Select((parts) => (IReadOnlyList<Part>)Dressed(parts, DecorKinds.Rock)).ToList();
        private static readonly IReadOnlyList<IReadOnlyList<Part>> Rubble = Rocks.Select((parts) => (IReadOnlyList<Part>)Dressed(parts, DecorKinds.Rubble)).ToList();

        /// <summary>Tries to drop one of `shapes` (any rotation) around `at`, within `spread` tiles.</summary>
        private static bool GroupNear(Canvas c, Pt at, Rng rng, IReadOnlyList<IReadOnlyList<Part>> shapes, int spread = 2, int elev = 0, int tries = 12)
        {
            for (var k = 0; k < tries; k++)
            {
                var x = (int)Js.Round(at.X) + rng.Range(-spread, spread);
                var y = (int)Js.Round(at.Y) + rng.Range(-spread, spread);
                if (Group(c, V(x, y), Turn(rng.Pick(shapes), rng.Int(4)), elev)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------- style

        /// <summary>One scale per map: all the houses (offices, halls…) of a map are alike.</summary>
        private sealed class Style
        {
            /// <summary>Houses, shops and cabins: outer side along the street, and depth (7–9).</summary>
            public int House;
            public int HouseDepth;
            /// <summary>Alley between neighbouring houses.</summary>
            public int Gap;
            /// <summary>Offices and labs (10–14).</summary>
            public int Office;
            public int OfficeDepth;
            /// <summary>Warehouses, barns and hangars (12–16).</summary>
            public int Hall;
            /// <summary>From one window to the next along a wall.</summary>
            public int WindowEvery;
            /// <summary>From one planter (bin, bollard) to the next along a sidewalk.</summary>
            public int Furniture;
            /// <summary>A town laid out as an old town (bent streets, uneven plots, small squares) rather than a grid: city() sets it.</summary>
            public bool Organic;
            /// <summary>An industrial estate (playtest 2026-10-14, #25): big blocks of warehouses and container yards, a gate with wire.</summary>
            public bool Industrial;
        }

        private static Style PickStyle(Rng rng)
        {
            return new Style
            {
                House = rng.Range(7, 9),
                HouseDepth = rng.Range(7, 9),
                Gap = rng.Range(1, 2),
                Office = rng.Range(11, 14),
                OfficeDepth = rng.Range(10, 13),
                Hall = rng.Range(13, 16),
                WindowEvery = rng.Range(2, 3),
                Furniture = rng.Range(6, 8),
            };
        }

        // --------------------------------------------------------------- buildings

        /// <summary>How the interior is divided: `'rooms' | 'open' | 'corridor' | 'shop' | 'hall' | 'market'`.</summary>
        private static class Plans
        {
            public const string Rooms = "rooms";
            public const string Open = "open";
            public const string Corridor = "corridor";
            public const string Shop = "shop";
            public const string Hall = "hall";
            public const string Market = "market";
        }

        /// <summary>What stands inside: `'home' | 'shop' | 'office' | 'lab' | 'store' | 'barn' | 'bare' | 'clinic'`.</summary>
        private static class Furnishes
        {
            public const string Home = "home";
            public const string Shop = "shop";
            public const string Office = "office";
            public const string Lab = "lab";
            public const string Store = "store";
            public const string Barn = "barn";
            public const string Bare = "bare";
            public const string Clinic = "clinic";
        }

        private sealed class Building
        {
            public Rect Outer = Rect(0, 0, 0, 0);
            public List<Rect> Rooms = new List<Rect>();
            /// <summary>What it was furnished as: it sets how tall it stands (storeysOf).</summary>
            public string Furnish = Furnishes.Bare;
            /// <summary>Storeys when they aren't the furnishing's to say (a filling station's shop, a motel).</summary>
            public int? Storeys;
            /// <summary>Left in ruins (ruin): no roof, no storeys above.</summary>
            public bool Ruined;
            /// <summary>A flat roof though it is furnished as a home (a motel).</summary>
            public bool Flat;
            /// <summary>A factory's main hall (#17): its roof has a strip of skylights and nobody stands on it.</summary>
            public bool Skylit;
            /// <summary>Clinical finish and damage, independent of furniture surviving a blast.</summary>
            public double? MedicalWear;
            public string? MedicalKind; // 'clinic' | 'ward' | 'hospital'
            public List<MedicalRoom>? MedicalRooms;
            public int? Front;
            /// <summary>A house's rooms by use on every storey (homeUses), and how worn it is (homeWear).</summary>
            public List<HomeRoom>? HomeRooms;
            public double? HomeWear;
            /// <summary>One open hall (plan 'hall'): a warehouse, a factory's hall, a sawmill, a barn.</summary>
            public bool Hall;
            /// <summary>A supermarket (plan 'market'; playtest 20/10, #18): the client draws its glass front and its sign (GameState.markets).</summary>
            public bool Market;
            /// <summary>A block of flats one goes into (flatsBlock): its stair core, the hall each storey's flats open onto.</summary>
            public Rect? FlatsCore;
            /// <summary>
            /// A house of two storeys (docs/MAPAS.md §4.3): its hall down one side, the rooms off it, and the column of the hall
            /// away from its wall, where the stairs go (the other one stays clear by the rooms' doors, up there too).
            /// </summary>
            public Rect? HouseHall;
            public Rect? HouseStairs;
        }

        // ------------------------------------------------------------ shared types
        // TypeScript's interfaces and unions that more than one part of the module uses (declared where their part is).

        /// <summary>district()'s rules (Blocks.cs).</summary>
        private sealed class SplitRules
        {
            public int MinBlock;
            public int MaxBlock;
            /// <summary>Street width range by depth of the split (the first split is the avenue): `[number, number][]`.</summary>
            public IReadOnlyList<int[]> Widths = new int[0][];
            /// <summary>Chances that a street jogs sideways halfway, or ends against a block.</summary>
            public double Jog;
            public double DeadEnd;
            /// <summary>Chance of keeping a block whole, by depth.</summary>
            public IReadOnlyList<double> Stop = new double[0];
            /// <summary>Streets that bend (an old town): the chance, and the most stretches a street has, by depth.</summary>
            public IReadOnlyList<double>? Bend;
            public IReadOnlyList<int>? Bends;
        }

        /// <summary>A lot (Lots.cs): `{ r: Rect; roads: boolean[] }`, the sides that look onto a road.</summary>
        private class Lot
        {
            public Rect R = Rect(0, 0, 0, 0);
            /// <summary>Sides that look onto a road.</summary>
            public bool[] Roads = new bool[4];
        }

        /// <summary>`'indoor' | 'open'` (Lots.cs).</summary>
        private static class Roles
        {
            public const string Indoor = "indoor";
            public const string Open = "open";
        }

        /// <summary>`'houses' | 'shops' | 'highStreet' | 'office' | 'flats' | 'warehouse' | 'plaza' | 'parking' | 'gasStation' | 'motel' | 'site' | 'yard'` (Lots.cs).</summary>
        private static class CityPrograms
        {
            public const string Houses = "houses";
            public const string Shops = "shops";
            public const string HighStreet = "highStreet";
            public const string Office = "office";
            public const string Flats = "flats";
            public const string Warehouse = "warehouse";
            public const string Plaza = "plaza";
            public const string Parking = "parking";
            public const string GasStation = "gasStation";
            public const string Motel = "motel";
            public const string Site = "site";
            public const string Yard = "yard";
        }

        /// <summary>A storey above the ground while it is drawn: what stands on it, and where it has a slab at all (Upstairs.cs).</summary>
        private sealed class Storey
        {
            public Canvas C;
            public bool[] Slab;

            public Storey(Canvas c, bool[] slab)
            {
                C = c;
                Slab = slab;
            }
        }

        /// <summary>Where parkBus left the bus (Evac.cs): `{ tiles: Vec2[]; heading: number; lane: Vec2[] }`.</summary>
        private sealed class BusPlace
        {
            public List<Vec2> Tiles = new List<Vec2>();
            public int Heading;
            public List<Vec2> Lane = new List<Vec2>();
        }
    }
}
