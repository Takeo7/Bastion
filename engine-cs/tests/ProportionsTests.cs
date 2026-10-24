using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using Xunit.Sdk;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/proportions.test.ts, test by test: the proportions of the generated maps (docs/MAPAS.md),
    /// on the layouts setup.ts missionLayout makes. Nothing is left out.
    /// </summary>
    public class ProportionsTests
    {
        // docs/MAPAS.md, phase 0: the proportions of the generated maps as a test, the net the later phases rebuild them
        // under. Seeded maps of each type, through the entry the server uses (setup.ts missionLayout → mapgen.ts
        // generateLayout). Phase 1: what stands over several tiles (a car 1 × 3, an ambulance 2 × 4, a bed 1 × 2…) is
        // whole, of its footprint, and closes no way; every place one can stand on is reached still; a seed is one map.
        // Phase 2: the stairs are flights of 3 tiles (2 in small houses), with their stairwell, and nobody starts on one.

        private sealed class MapType
        {
            public string Name { get; set; } = "";
            public Func<int, MapSource> Map { get; set; } = (seed) => new MapSource();
            public string Kind { get; set; } = ""; // MissionKinds
            public bool Hospital { get; set; }
            public string? Alarm { get; set; } // Alarms
            /// <summary>The share of the ground's tiles that is cover, today (low, high).</summary>
            public double[] Cover { get; set; } = new double[2];
        }

        private static readonly MapType[] Types =
        {
            new MapType { Name = "city sabotage", Map = (seed) => MapSource.Generated(Biomes.City, seed), Kind = MissionKinds.Sabotage, Alarm = Alarms.Police, Cover = new[] { 0.05, 0.18 } },
            new MapType { Name = "city recovery", Map = (seed) => MapSource.Generated(Biomes.City, seed), Kind = MissionKinds.Recovery, Cover = new[] { 0.05, 0.18 } },
            new MapType { Name = "industrial", Map = (seed) => MapSource.Generated(Biomes.City, seed, Sites.Industrial), Kind = MissionKinds.Sabotage, Alarm = Alarms.Ambulance, Cover = new[] { 0.06, 0.2 } },
            new MapType { Name = "clinic", Map = (seed) => MapSource.Generated(Biomes.City, seed, null, MedicalSiteKinds.Clinic), Kind = MissionKinds.Recovery, Cover = new[] { 0.05, 0.18 } },
            new MapType { Name = "ward", Map = (seed) => MapSource.Generated(Biomes.City, seed, null, MedicalSiteKinds.Ward), Kind = MissionKinds.Recovery, Cover = new[] { 0.05, 0.18 } },
            new MapType { Name = "hospital", Map = (seed) => MapSource.Generated(Biomes.City, seed, null, MedicalSiteKinds.Hospital), Kind = MissionKinds.Rescue, Hospital = true, Cover = new[] { 0.05, 0.18 } },
            new MapType { Name = "wilds", Map = (seed) => MapSource.Generated(Biomes.Wilds, seed), Kind = MissionKinds.Hack, Cover = new[] { 0.15, 0.4 } },
            new MapType { Name = "roadside", Map = (seed) => MapSource.Generated(Biomes.Wilds, seed, Sites.Roadside), Kind = MissionKinds.Recovery, Cover = new[] { 0.12, 0.35 } },
            new MapType { Name = "facility", Map = (seed) => MapSource.Generated(Biomes.Facility, seed), Kind = MissionKinds.Sabotage, Alarm = Alarms.Ambulance, Cover = new[] { 0.05, 0.2 } },
        };
        private static readonly int[] Seeds = { 3, 1201, 20011, 77017 };
        private static readonly List<int> Pods = Setup.PodPlan(2).Select((p) => p.Count).ToList();

        private static MissionLayout LayoutOf(MapType t, int seed)
        {
            return Setup.MissionLayout(t.Map(seed), t.Kind, Pods, false, false, t.Hospital, t.Alarm);
        }

        private sealed class MapCase
        {
            public MapType T { get; set; } = new MapType();
            public int Seed { get; set; }
            public string Label { get; set; } = "";
            public MissionLayout L { get; set; } = new MissionLayout();
        }

        /// <summary>MAPS: every type on every seed, made once.</summary>
        private static readonly Lazy<List<MapCase>> AllMaps = new Lazy<List<MapCase>>(() => Types.SelectMany((t) => Seeds.Select((seed) => new MapCase { T = t, Seed = seed, Label = $"{t.Name}/{seed}", L = LayoutOf(t, seed) })).ToList());

        private static List<MapCase> Maps => AllMaps.Value;

        private sealed class Layer
        {
            public int Z { get; set; }
            public IReadOnlyList<string?> Tiles { get; set; } = new List<string?>();
            public IReadOnlyList<int> Decor { get; set; } = new List<int>();
        }

        /// <summary>Each level of a layout: its tiles and decor, and z.</summary>
        private static List<Layer> LayersOf(MissionLayout l)
        {
            var out_ = new List<Layer> { new Layer { Z = 0, Tiles = l.Tiles, Decor = l.Decor ?? new List<int>() } };
            out_.AddRange((l.Levels ?? new List<Level>()).Select((v, k) => new Layer { Z = k + 1, Tiles = v.Tiles, Decor = v.Decor ?? new List<int>() }));
            return out_;
        }

        /// <summary>
        /// What each object over several tiles may take (decor.ts FOOTPRINT), as [wide, deep]: across the way it faces and
        /// along it. A patrol car's or an ambulance's counts the siren's tile; a lorry's block is 2 × 6 where it fits, 2 × 4
        /// where not, and a barn's tractor 2 × 3; a siren's car squeezed in where no other fits keeps the two tiles of before.
        /// </summary>
        private static readonly Dictionary<string, IReadOnlyList<int>[]> Shapes = new Dictionary<string, IReadOnlyList<int>[]>
        {
            [DecorKinds.CarFront] = new[] { Decor.Footprint.Car },
            [DecorKinds.SirenCar] = new[] { Decor.Footprint.PatrolCar, Decor.Footprint.Ambulance, new[] { 1, 2 } },
            [DecorKinds.Truck] = new[] { Decor.Footprint.Truck, Decor.Footprint.TruckShort, Decor.Footprint.Tractor },
            [DecorKinds.Container] = new[] { Decor.Footprint.Container },
            [DecorKinds.Bed] = new[] { Decor.Footprint.Bed, Decor.Footprint.DoubleBed },
            [DecorKinds.Sofa] = new[] { Decor.Footprint.Sofa },
            // A garden's (docs/MAPAS.md phase 4, Arte): the shed, the washing line, the vegetable patch.
            [DecorKinds.Shed] = new[] { Decor.Footprint.Shed },
            [DecorKinds.WashingLine] = new[] { Decor.Footprint.WashingLine },
            [DecorKinds.VegPatch] = new[] { Decor.Footprint.VegPatch },
            [DecorKinds.Bathtub] = new[] { Decor.Footprint.Bathtub },
            [DecorKinds.Table] = new[] { Decor.Footprint.Table },
            [DecorKinds.HospitalBed] = new[] { Decor.Footprint.HospitalBed },
            [DecorKinds.Gurney] = new[] { Decor.Footprint.Gurney },
        };

        /// <summary>Furniture over several tiles, indoors: what never stands by a way through a wall or up a storey.</summary>
        private static readonly HashSet<string> Furniture = new HashSet<string> { DecorKinds.Bed, DecorKinds.Sofa, DecorKinds.Bathtub, DecorKinds.Table, DecorKinds.HospitalBed, DecorKinds.Gurney };
        private static readonly HashSet<string?> Ways = new HashSet<string?> { TileKinds.Door, TileKinds.DoorOpen, TileKinds.Window, TileKinds.WindowBroken, TileKinds.Ladder };

        /// <summary>
        /// Where a building's flight of stairs with a tile on j of `layer` is walked onto from: the tile behind its lowest (it
        /// climbs the way it faces; docs/MAPAS.md, phase 2: a flight of several tiles, its top dressed 'stairs', the rest parts).
        /// </summary>
        private static int? FootOf(GameState s, Layer layer, int j)
        {
            var on = Flights.FlightAt(s, new Pos { X = j % s.Width, Y = (int)Math.Floor((double)j / s.Width), Z = layer.Z });
            if (on == null) return null;
            var foot = Flights.FlightFoot(on.Flight);
            return foot.Y * s.Width + foot.X;
        }

        // ------------------------------------------------------------ what TypeScript does in passing

        /// <summary>`l as unknown as GameState`: the layout read as a state (its map; no units, no mission).</summary>
        private static GameState StateOf(MissionLayout l)
        {
            return new GameState
            {
                MapId = l.Id,
                MapName = l.Name,
                Biome = l.Biome,
                Site = l.Site,
                Width = l.Width,
                Height = l.Height,
                Tiles = l.Tiles,
                Elev = l.Elev,
                Ground = l.Ground,
                Medical = l.Medical,
                Homes = l.Homes,
                Markets = l.Markets,
                Decor = l.Decor,
                Storeys = l.Storeys,
                Levels = l.Levels,
                Frame = l.Frame,
                Breakables = l.Breakables,
            };
        }

        /// <summary>A house's or a medical building's room, whichever (HomeRoom, MedicalRoom).</summary>
        private static (int X, int Y, int W, int H, int Z, string Use) RoomOf(HomeRoom r)
        {
            return (r.X, r.Y, r.W, r.H, r.Z, r.Use);
        }

        private static (int X, int Y, int W, int H, int Z, string Use) RoomOf(MedicalRoom r)
        {
            return (r.X, r.Y, r.W, r.H, r.Z, r.Use);
        }

        private static string? TileAt(IReadOnlyList<string?> tiles, int i)
        {
            return Js.At(tiles, i, null);
        }

        /// <summary>`l.ground?.[i]`: undefined (null) without a ground layer or past either end.</summary>
        private static int? GroundAt(MissionLayout l, int i)
        {
            return l.Ground != null && i >= 0 && i < l.Ground.Count ? l.Ground[i] : (int?)null;
        }

        /// <summary>toBe, with the test's label.</summary>
        private static void Is<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new XunitException($"{label}: expected {expected}, got {actual}");
        }

        // ------------------------------------------------------------ the proportions of the generated maps (docs/MAPAS.md)

        [Fact(DisplayName = "the proportions of the generated maps (docs/MAPAS.md) › phase 1: every object over several tiles is whole, of its footprint and on cover of one kind")]
        public void Phase1EveryObjectOverSeveralTilesIsWhole()
        {
            var counts = new Dictionary<string, int>();
            foreach (var c in Maps)
            {
                var l = c.L;
                foreach (var layer in LayersOf(l))
                {
                    for (var i = 0; i < layer.Decor.Count; i++)
                    {
                        var look = Decor.DecorOf(layer.Decor[i]);
                        if (look == null) continue;
                        var at = $"{c.Label}: {look.Kind} at {i % l.Width},{i / l.Width},{layer.Z}";
                        // A part leads to its anchor, on cover of the anchor's kind.
                        if (look.Kind == DecorKinds.Part)
                        {
                            var a = Decor.AnchorOf(layer.Decor, l.Width, i);
                            Assert.True(a >= 0, $"{at}: a part of nothing");
                            Is(TileAt(layer.Tiles, a), TileAt(layer.Tiles, i), at);
                            continue;
                        }
                        if (!Shapes.TryGetValue(look.Kind, out var shapes)) continue;
                        var o = Decor.ObjectAt(layer.Decor, l.Width, i)!;
                        if (o.Anchor != i) continue;
                        var tiles = new List<int>(o.Tiles);
                        // The siren's own tile is the rest of its car (rules.ts damage makes it a part when it falls silent).
                        if (look.Kind == DecorKinds.SirenCar && l.ObjectiveSpot != null) tiles.Add(l.ObjectiveSpot.Y * l.Width + l.ObjectiveSpot.X);
                        var xs = tiles.Select((t) => t % l.Width).ToList();
                        var ys = tiles.Select((t) => t / l.Width).ToList();
                        var w = xs.Max() - xs.Min() + 1;
                        var h = ys.Max() - ys.Min() + 1;
                        Is(w * h, tiles.Count, $"{at}: not a rectangle ({w} × {h})");
                        // [wide, deep] by the way it faces: deep along it.
                        var along = look.Facing % 2 == 0 ? h : w;
                        var across = look.Facing % 2 == 0 ? w : h;
                        Assert.True(shapes.Any((s) => s[0] == across && s[1] == along), $"{at}: {across} × {along}");
                        foreach (var t in o.Tiles) Assert.True(layer.Tiles[t] == TileKinds.Low || layer.Tiles[t] == TileKinds.High, $"{at}: {layer.Tiles[t]}");
                        Is(1, o.Tiles.Select((t) => layer.Tiles[t]).Distinct().Count(), $"{at}: of two kinds of cover");
                        var key = $"{look.Kind} {across}×{along}";
                        counts[key] = (counts.TryGetValue(key, out var n) ? n : 0) + 1;
                    }
                }
            }
            // What phase 1 made bigger is there, at its new size.
            foreach (var key in new[] { "carFront 1×3", "sirenCar 1×3", "sirenCar 2×4", "truck 2×6", "container 2×5", "bed 1×2", "bed 2×2", "sofa 2×1", "bathtub 2×1", "table 2×1", "hospitalBed 1×2", "gurney 1×2" })
            {
                Assert.True((counts.TryGetValue(key, out var n) ? n : 0) > 0, key);
            }
        }

        [Fact(DisplayName = "the proportions of the generated maps (docs/MAPAS.md) › phase 1: furniture over several tiles stands clear of doors, windows, ladders and stairs, and every room stays one")]
        public void Phase1FurnitureOverSeveralTilesStandsClearOfWays()
        {
            foreach (var c in Maps)
            {
                var l = c.L;
                var label = c.Label;
                var s = StateOf(l);
                foreach (var layer in LayersOf(l))
                {
                    for (var i = 0; i < layer.Decor.Count; i++)
                    {
                        var o = Decor.DecorOf(layer.Decor[i])?.Kind == DecorKinds.Part ? null : Decor.ObjectAt(layer.Decor, l.Width, i);
                        if (o == null || o.Anchor != i || o.Tiles.Count < 2 || !Furniture.Contains(o.Kind)) continue;
                        foreach (var t in o.Tiles)
                        {
                            var p = new Vec2 { X = t % l.Width, Y = t / l.Width };
                            foreach (var d in Grid.Dirs4)
                            {
                                var j = (p.Y + d.Y) * l.Width + p.X + d.X;
                                // A flight of stairs put in later may stand beside it (mapgen.ts placeStairs), never on the tile one climbs it from.
                                var foot = FootOf(s, layer, j);
                                if (foot != null) Assert.True(foot != t, $"{label}: {o.Kind} at the foot of the stairs at {p.X},{p.Y},{layer.Z}");
                                else Assert.False(Ways.Contains(TileAt(layer.Tiles, j)), $"{label}: {o.Kind} by a way at {p.X},{p.Y},{layer.Z}");
                            }
                        }
                    }
                }
                // A house's or a clinic's room: all its floor one room (loot.ts roomAt), round its furniture.
                var rooms = (l.Homes ?? new List<HomeSite>()).SelectMany((h) => h.Rooms).Select(RoomOf)
                    .Concat((l.Medical ?? new List<MedicalSite>()).SelectMany((m) => (m.Rooms ?? new List<MedicalRoom>()).Where((r) => r.Use != "stairs")).Select(RoomOf));
                foreach (var room in rooms)
                {
                    var tiles = room.Z != 0 ? l.Levels![room.Z - 1].Tiles : (IReadOnlyList<string?>)l.Tiles;
                    var ids = new HashSet<int?>();
                    for (var y = room.Y; y < room.Y + room.H; y++)
                    {
                        for (var x = room.X; x < room.X + room.W; x++)
                        {
                            var kind = TileAt(tiles, y * l.Width + x);
                            if (Grid.IsWalkable(kind) && kind != TileKinds.Door && kind != TileKinds.DoorOpen && kind != TileKinds.Window && kind != TileKinds.WindowBroken) ids.Add(Looting.RoomAt(s, new Pos { X = x, Y = y, Z = room.Z })?.Id);
                        }
                    }
                    Assert.True(ids.Count <= 1, $"{label}: room {room.X},{room.Y},{room.Z} cut in two");
                }
            }
        }

        [Fact(DisplayName = "the proportions of the generated maps (docs/MAPAS.md) › every place one can stand on, on every storey, is reached from where the squad starts")]
        public void EveryPlaceOneCanStandOnIsReached()
        {
            foreach (var c in Maps)
            {
                var l = c.L;
                var reach = Furnishing.ReachableFrom(new FurnishMap { Width = l.Width, Height = l.Height, Tiles = l.Tiles, Elev = l.Elev, Decor = l.Decor ?? new List<int>(), Levels = l.Levels ?? new List<Level>() }, new Pos { X = l.Spawns[0].X, Y = l.Spawns[0].Y, Z = 0 });
                var area = l.Width * l.Height;
                foreach (var layer in LayersOf(l))
                {
                    for (var i = 0; i < layer.Tiles.Count; i++)
                    {
                        if (Grid.CanStandOn(layer.Tiles[i]) && !reach.Has(layer.Z * area + i)) throw new XunitException($"{c.Label}: {i % l.Width},{i / l.Width},{layer.Z} out of reach");
                    }
                }
            }
        }

        [Fact(DisplayName = "the proportions of the generated maps (docs/MAPAS.md) › phase 2: the stairs are flights of 3 tiles, 2 in a small house, under their stairwell; nobody starts on one")]
        public void Phase2TheStairsAreFlightsOf3Tiles()
        {
            var lengths = new Dictionary<string, int>();
            foreach (var c in Maps)
            {
                var l = c.L;
                var s = StateOf(l);
                foreach (var layer in LayersOf(l))
                {
                    for (var i = 0; i < layer.Decor.Count; i++)
                    {
                        if (Decor.DecorOf(layer.Decor[i])?.Kind != DecorKinds.Stairs) continue;
                        var top = new Pos { X = i % l.Width, Y = i / l.Width, Z = layer.Z };
                        var at = $"{c.Label}: {top.X},{top.Y},{top.Z}";
                        var flight = Flights.FlightAt(s, top)!.Flight;
                        var n = flight.Tiles.Count;
                        // A house (its rooms small) or a small shop where three has no room, or a short hospital's stairwell: two.
                        var home = (l.Homes ?? new List<HomeSite>()).Any((h) => top.X >= h.X && top.Y >= h.Y && top.X < h.X + h.W && top.Y < h.Y + h.H);
                        var where = home ? "house" : c.T.Hospital ? "hospital" : "other";
                        var key = $"{where} {n}";
                        lengths[key] = (lengths.TryGetValue(key, out var k) ? k : 0) + 1;
                        Assert.True(n == 2 || n == 3, $"{at}: {n}");
                        foreach (var p in flight.Tiles) Assert.False(Grid.SlabAt(s, p.X, p.Y, layer.Z + 1), $"{at}: no stairwell over {p.X},{p.Y}");
                        Is(TileKinds.Floor, Grid.KindAt(s, Flights.FlightFoot(flight)), $"{at}: its foot");
                        Assert.True(Grid.CanStandOn(Grid.KindAt(s, Flights.FlightLanding(flight))), $"{at}: its landing");
                    }
                }
                // Where the groups start, and the living posted upstairs.
                foreach (var p in l.Pods.SelectMany((pod) => pod.Tiles).Concat(l.Perch ?? new List<Pos>())) Assert.False(Flights.MidFlight(s, p), $"{c.Label}: a group on a flight at {p.X},{p.Y}");
            }
            int Count(string key) => lengths.TryGetValue(key, out var n) ? n : 0;
            // Three wherever there is room: in shops and offices nearly always, in most houses' and hospitals'.
            Assert.True(Count("other 3") > 100, $"other 3: {Count("other 3")}");
            Assert.True(Count("other 2") <= Count("other 3") / 20.0, $"other 2: {Count("other 2")}, other 3: {Count("other 3")}");
            Assert.True(Count("house 3") + Count("hospital 3") > Count("house 2") + Count("hospital 2"), $"house 3: {Count("house 3")}, hospital 3: {Count("hospital 3")}, house 2: {Count("house 2")}, hospital 2: {Count("hospital 2")}");
        }

        [Fact(DisplayName = "the proportions of the generated maps (docs/MAPAS.md) › a seed is one map")]
        public void ASeedIsOneMap()
        {
            foreach (var t in Types)
            {
                var diff = Step2Json.Diff(Step2Json.Element(Maps.Find((m) => m.T == t && m.Seed == Seeds[1])!.L), Step2Json.Element(LayoutOf(t, Seeds[1])), "");
                if (diff != null) throw new XunitException($"{t.Name}: {diff}");
            }
        }

        [Fact(DisplayName = "the proportions of the generated maps (docs/MAPAS.md) › today's figures: rooms of 3 × 3 or more, the ground's share of cover, the walk to the objective")]
        public void TodaysFigures()
        {
            foreach (var c in Maps)
            {
                var (t, l, label) = (c.T, c.L, c.Label);
                // (A small clinic's waiting room in front is a lobby two tiles deep.)
                foreach (var room in (l.Homes ?? new List<HomeSite>()).SelectMany((h) => h.Rooms).Select(RoomOf).Concat((l.Medical ?? new List<MedicalSite>()).SelectMany((m) => m.Rooms ?? new List<MedicalRoom>()).Select(RoomOf)))
                {
                    // (A hall with the stairs is two across or more, docs/MAPAS.md §4.3: «recibidor con escalera 2 × 5 – 3 × 6»; a
                    // bathroom two by three, decided by Arturo 19/10.)
                    var least = new[] { "reception", "hall", "bath", "toilet", "pantry" }.Contains(room.Use) ? 2 : 3;
                    Assert.True(Math.Min(room.W, room.H) >= least, $"{label}: {room.Use} {room.W} × {room.H}");
                }
                var cover = (double)l.Tiles.Count((k) => k == TileKinds.Low || k == TileKinds.High) / l.Tiles.Count;
                Assert.True(cover >= t.Cover[0], $"{label}: cover {cover}");
                Assert.True(cover <= t.Cover[1], $"{label}: cover {cover}");
                // On the ground, step by step (the movement rules), from the squad's first tile to the objective.
                var goal = l.Item ?? l.Terminal ?? l.ObjectiveSpot;
                if (goal == null) continue;
                var s = new GameState { Width = l.Width, Height = l.Height, Tiles = l.Tiles, Elev = l.Elev };
                var steps = new Dictionary<int, int> { [l.Spawns[0].Y * l.Width + l.Spawns[0].X] = 0 };
                var queue = new List<Vec2> { l.Spawns[0] };
                for (var k = 0; k < queue.Count; k++)
                {
                    var p = queue[k];
                    var n = steps[p.Y * l.Width + p.X];
                    foreach (var d in Grid.Dirs8)
                    {
                        var q = new Vec2 { X = p.X + d.X, Y = p.Y + d.Y };
                        var near = Math.Max(Math.Abs(q.X - goal.X), Math.Abs(q.Y - goal.Y)) == 0;
                        if ((!near && Paths.StepCost(s, p.X, p.Y, d.X, d.Y) == null) || steps.ContainsKey(q.Y * l.Width + q.X)) continue;
                        if (q.X < 0 || q.Y < 0 || q.X >= l.Width || q.Y >= l.Height) continue;
                        steps[q.Y * l.Width + q.X] = n + 1;
                        queue.Add(q);
                    }
                }
                Assert.True(steps.TryGetValue(goal.Y * l.Width + goal.X, out var walk), $"{label}: the objective out of reach");
                Assert.True(walk >= 20, $"{label}: walk {walk}");
                Assert.True(walk <= 90, $"{label}: walk {walk}");
            }
        }

        [Fact(DisplayName = "the proportions of the generated maps (docs/MAPAS.md) › phase 4: every courtyard has two ways out or more; every room a way in that is no window")]
        public void Phase4EveryCourtyardHasTwoWaysOut()
        {
            var courtyards = 0;
            foreach (var c in Maps)
            {
                var (l, label) = (c.L, c.Label);
                var W = l.Width;
                bool Walkable(int x, int y) => x >= 0 && y >= 0 && x < W && y < l.Height && Grid.IsWalkable(l.Tiles[y * W + x]) && GroundAt(l, y * W + x) != 4;
                foreach (var r in l.Courtyards ?? new List<PlanRect>())
                {
                    courtyards++;
                    // The walkable tiles just outside it, beside walkable ground inside it: in runs, each a way out.
                    bool Inside(int x, int y) => x >= r.X && y >= r.Y && x < r.X + r.W && y < r.Y + r.H;
                    var gates = new JsSet<int>();
                    for (var y = r.Y - 1; y <= r.Y + r.H; y++)
                    {
                        for (var x = r.X - 1; x <= r.X + r.W; x++)
                        {
                            if (Inside(x, y) || !Walkable(x, y) || l.Tiles[y * W + x] == TileKinds.Window || l.Tiles[y * W + x] == TileKinds.WindowBroken) continue;
                            if (Grid.Dirs4.Any((d) => Inside(x + d.X, y + d.Y) && Walkable(x + d.X, y + d.Y))) gates.Add(y * W + x);
                        }
                    }
                    var runs = 0;
                    var seen = new HashSet<int>();
                    foreach (var g in gates)
                    {
                        if (seen.Contains(g)) continue;
                        runs++;
                        var queue = new List<int> { g };
                        seen.Add(g);
                        for (var k = 0; k < queue.Count; k++)
                        {
                            var i = queue[k];
                            foreach (var d in Grid.Dirs8)
                            {
                                var j = i + d.Y * W + d.X;
                                if (!gates.Has(j) || seen.Contains(j) || Math.Abs((j % W) - (i % W)) > 1) continue;
                                seen.Add(j);
                                queue.Add(j);
                            }
                        }
                    }
                    Assert.True(runs >= 2, $"{label}: courtyard {r.X},{r.Y} {r.W} × {r.H}: {runs}");
                }
                // A house's room: on its ring, a door or an open way (a hall, an open plan), not only windows.
                foreach (var room in (l.Homes ?? new List<HomeSite>()).SelectMany((h) => h.Rooms).Select(RoomOf).Concat((l.Medical ?? new List<MedicalSite>()).SelectMany((m) => m.Rooms ?? new List<MedicalRoom>()).Select(RoomOf)))
                {
                    var layer = room.Z != 0 ? l.Levels![room.Z - 1].Tiles : (IReadOnlyList<string?>)l.Tiles;
                    string? At(int x, int y) => x >= 0 && y >= 0 && x < W && y < l.Height ? TileAt(layer, y * W + x) : null;
                    var way = false;
                    for (var y = room.Y - 1; y <= room.Y + room.H && !way; y++)
                    {
                        for (var x = room.X - 1; x <= room.X + room.W && !way; x++)
                        {
                            if (x >= room.X && y >= room.Y && x < room.X + room.W && y < room.Y + room.H) continue;
                            var k = At(x, y);
                            way = k == TileKinds.Door || k == TileKinds.DoorOpen || k == TileKinds.Ladder || (k != null && Grid.IsWalkable(k) && k != TileKinds.Window && k != TileKinds.WindowBroken);
                        }
                    }
                    Assert.True(way, $"{label}: {room.Use} {room.X},{room.Y},{room.Z} only by a window");
                }
            }
            Assert.True(courtyards >= 0);
        }

        [Fact(DisplayName = "the proportions of the generated maps (docs/MAPAS.md) › phase 4: a window looks out, never onto another room nor a neighbour's wall; a house's and a clinic's rooms lie in it")]
        public void Phase4AWindowLooksOut()
        {
            foreach (var c in Maps)
            {
                var (l, label) = (c.L, c.Label);
                var W = l.Width;
                var layers = new List<(int Z, IReadOnlyList<string?> Tiles)> { (0, l.Tiles) };
                layers.AddRange((l.Levels ?? new List<Level>()).Select((v, k) => (k + 1, (IReadOnlyList<string?>)v.Tiles)));
                foreach (var (z, tiles) in layers)
                {
                    for (var i = 0; i < tiles.Count; i++)
                    {
                        var k = tiles[i];
                        if (k != TileKinds.Window && k != TileKinds.WindowBroken) continue;
                        var x = i % W;
                        var y = i / W;
                        // Out of doors on one side at least: open ground, a street or the outline below; no slab up a storey.
                        var out_ = Grid.Dirs4.Any((d) =>
                        {
                            var j = (y + d.Y) * W + x + d.X;
                            if (x + d.X < 0 || y + d.Y < 0 || x + d.X >= W || y + d.Y >= l.Height) return true;
                            return z == 0 ? GroundAt(l, j) != 2 : TileAt(tiles, j) == null;
                        });
                        Assert.True(out_, $"{label}: a window at {x},{y},{z} between two insides");
                    }
                }
                var sites = (l.Homes ?? new List<HomeSite>()).Select((h) => (h.X, h.Y, h.W, h.H, Rooms: h.Rooms.Select(RoomOf).ToList()))
                    .Concat((l.Medical ?? new List<MedicalSite>()).Select((m) => (m.X, m.Y, m.W, m.H, Rooms: (m.Rooms ?? new List<MedicalRoom>()).Select(RoomOf).ToList())));
                foreach (var site in sites)
                {
                    foreach (var room in site.Rooms)
                    {
                        var inSite = room.X >= site.X && room.Y >= site.Y && room.X + room.W <= site.X + site.W && room.Y + room.H <= site.Y + site.H;
                        Assert.True(inSite, $"{label}: room {room.X},{room.Y},{room.Z} outside its building {site.X},{site.Y}");
                        Assert.True(room.Z == 0 || (l.Levels != null && room.Z - 1 >= 0 && room.Z - 1 < l.Levels.Count), $"{label}: room {room.X},{room.Y} on storey {room.Z} with no storey");
                    }
                }
            }
        }
    }
}
