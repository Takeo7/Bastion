using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using Xunit.Sdk;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/townPlan.test.ts, test by test (TownPlans.TownPlan, Mapgen.GenerateLayout and Setup.CreateMission).
    /// Nothing left out.
    /// </summary>
    public class TownPlanTests
    {
        // docs/MAPAS.md, phase 3 and §5.10 (Arturo, 19/10: «que los mapas no sean cuadrados, que sean más orgánicos, con
        // bordes con sentido»): a town of 64 on a plan of streets, what is played closed round by what makes sense, its ways in.

        private const int Out = 4;
        private const int Road = 1;

        private static MissionLayout Town(int seed, string kind = MissionKinds.Recovery)
        {
            return Mapgen.GenerateLayout(new GenOptions
            {
                Biome = Biomes.City,
                Kind = kind,
                PodSizes = new List<int> { 3, 3, 3, 3 },
                Seed = seed * 7919,
                Alarm = kind == MissionKinds.Sabotage ? Alarms.Police : null,
            });
        }

        /// <summary>Each tile of `l` in how many pieces of its frame.</summary>
        private static int[] Covered(MissionLayout l)
        {
            var n = new int[l.Width * l.Height];
            foreach (var f in l.Frame ?? new List<FramePiece>()) for (var y = f.Y; y < f.Y + f.H; y++) for (var x = f.X; x < f.X + f.W; x++) n[y * l.Width + x]++;
            return n;
        }

        /// <summary>`expect(actual, label).toBe(expected)`.</summary>
        private static void Is<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new XunitException($"{label}: expected {expected}, got {actual}");
        }

        /// <summary>toEqual on anything: the same JSON, null and missing alike.</summary>
        private static void Same(object? expected, object? actual, string label = "")
        {
            var diff = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "");
            if (diff != null) throw new XunitException($"{label}{diff}");
        }

        private static MissionOptions Opts(MapSource map, int seed, string? kind = null, int? difficulty = null, int? vehicle = null)
        {
            return new MissionOptions { Map = map, Slots = new List<int> { 0 }, Rng = new Rng(seed), Kind = kind, Difficulty = difficulty, Vehicle = vehicle };
        }

        // ------------------------------------------------------------ the plan of a town (townPlan.ts)

        [Fact(DisplayName = "the plan of a town (townPlan.ts) › a grid has an avenue and streets of five; an old town, its square and a high street; most of the map is played")]
        public void AGridHasAnAvenueAndStreetsOfFive()
        {
            for (var seed = 1; seed <= 20; seed++)
            {
                foreach (var old in new[] { false, true })
                {
                    var p = TownPlans.TownPlan(Mapgen.TownSize, Mapgen.TownSize, new Rng(seed), old);
                    var label = $"{(old ? "old" : "grid")}/{seed}";
                    var avenue = p.Roads.Where((r) => r.Avenue == true).ToList();
                    Assert.True(avenue.Count > 0, label);
                    foreach (var r in avenue) Is(old ? TownPlans.Street : TownPlans.Avenue, r.Vertical ? r.R.W : r.R.H, label);
                    if (!old) foreach (var r in p.Roads.Where((x) => x.Avenue != true)) Is(TownPlans.Street, r.Vertical ? r.R.W : r.R.H, label);
                    Is(old, p.Square != null, label);
                    var played = (double)p.Out.Count((o) => !o) / p.Out.Length;
                    Assert.True(played > 0.6, $"{label}: {played}");
                    Assert.True(played < 0.92, $"{label}: {played}");
                    Assert.True(p.Entries.Count >= 2, label);
                    Assert.True(p.Entries.Count <= 4, label);
                    Assert.Same(p.Entries[0], p.SquadEntry);
                    // A seed is one plan.
                    Same(p, TownPlans.TownPlan(Mapgen.TownSize, Mapgen.TownSize, new Rng(seed), old), $"{label}: ");
                }
            }
        }

        // ------------------------------------------------------------ the ways in stay open

        [Fact(DisplayName = "the ways in stay open › where the squad comes in and every way in are never shut, whatever is closed beside them")]
        public void WhereTheSquadComesInAndEveryWayInAreNeverShut()
        {
            for (var seed = 0; seed < 200; seed++)
            {
                foreach (var (old, wide) in new[] { (false, false), (true, false), (false, true) })
                {
                    var p = TownPlans.TownPlan(wide ? 72 : Mapgen.TownSize, Mapgen.TownSize, new Rng(seed), old, wide);
                    var label = $"{seed}/{(old ? "old" : wide ? "wide" : "grid")}";
                    Is(false, p.Out[p.Squad.Y * p.Width + p.Squad.X], label);
                    foreach (var e in p.Entries)
                    {
                        var r = e.Road.R;
                        for (var y = r.Y; y < r.Y + r.H; y++) for (var x = r.X; x < r.X + r.W; x++) if (p.Out[y * p.Width + x]) throw new XunitException($"{label}: {x},{y} shut");
                    }
                }
            }
        }

        // ------------------------------------------------------------ a town on its plan (docs/MAPAS.md phase 3)

        [Fact(DisplayName = "a town on its plan (docs/MAPAS.md phase 3) › its outline: every tile out of play a wall in exactly one rectangle of the frame, and none in play")]
        public void ItsOutline()
        {
            for (var seed = 1; seed <= 12; seed++)
            {
                var l = Town(seed);
                var label = $"{seed}";
                Assert.True((l.Frame?.Count ?? 0) > 0, label);
                var n = Covered(l);
                for (var i = 0; i < l.Ground!.Count; i++)
                {
                    var g = l.Ground[i];
                    var at = $"{label}: {i % l.Width},{i / l.Width}";
                    Is(g == Out ? 1 : 0, n[i], at);
                    if (g == Out) Is(TileKinds.Wall, l.Tiles[i], at);
                }
                foreach (var f in l.Frame!)
                {
                    // A river, the railway, the motorway or a barrier runs along a side; a block shut up, a ruin or works stands so many storeys.
                    Is(new[] { FrameKinds.River, FrameKinds.Rail, FrameKinds.Highway, FrameKinds.Barrier }.Contains(f.Kind), f.Side != null, $"{label}: {f.Kind}");
                    if (new[] { FrameKinds.Block, FrameKinds.Ruin, FrameKinds.Works }.Contains(f.Kind)) Assert.True(f.Storeys > 0, $"{label}: {f.Kind}");
                    if (f.Storeys != null && f.Storeys != 0) for (var y = f.Y; y < f.Y + f.H; y++) for (var x = f.X; x < f.X + f.W; x++) Is(f.Storeys, l.Storeys![y * l.Width + x], label);
                }
            }
        }

        [Fact(DisplayName = "a town on its plan (docs/MAPAS.md phase 3) › nothing played by the edge but on its ways in, two to four streets the dead come in by")]
        public void NothingPlayedByTheEdgeButOnItsWaysIn()
        {
            for (var seed = 1; seed <= 12; seed++)
            {
                var l = Town(seed);
                var W = l.Width;
                var H = l.Height;
                // The ways in: streets that reach the edge, each a run of road along it.
                var runs = new HashSet<string>();
                for (var i = 0; i < W * H; i++)
                {
                    var x = i % W;
                    var y = i / W;
                    var edge = Math.Min(Math.Min(x, y), Math.Min(W - 1 - x, H - 1 - y));
                    if (l.Ground![i] == Out) continue;
                    Assert.True(edge >= (l.Ground[i] == Road ? 0 : 2), $"{seed}: {x},{y} played by the edge");
                    if (edge == 0) runs.Add(x == 0 || x == W - 1 ? $"{x}/{y / 8}" : $"{x / 8}/{y}");
                }
                Assert.True(runs.Count >= 2, $"{seed}");
            }
        }

        [Fact(DisplayName = "a town on its plan (docs/MAPAS.md phase 3) › the bus waits by the kerb, two by nine, facing its way out, the evacuation along its doors three by seven")]
        public void TheBusWaitsByTheKerb()
        {
            var along = 0;
            for (var seed = 1; seed <= 12; seed++)
            {
                foreach (var kind in new[] { MissionKinds.Recovery, MissionKinds.Sabotage, MissionKinds.Rescue })
                {
                    var l = Town(seed, kind);
                    var label = $"{kind}/{seed}";
                    if (l.EvacZone.Count != 21) continue;
                    along++;
                    var bus = l.EvacBus!;
                    Is(18, bus.Tiles.Count, label);
                    foreach (var p in bus.Tiles)
                    {
                        Is(TileKinds.High, l.Tiles[p.Y * l.Width + p.X], label);
                        Is(DecorKinds.Bus, Decor.DecorOf(l.Decor![p.Y * l.Width + p.X])?.Kind, label);
                    }
                    // Long the way it faces.
                    var xs = bus.Tiles.Select((p) => p.X).ToList();
                    var ys = bus.Tiles.Select((p) => p.Y).ToList();
                    var @long = bus.Heading % 2 == 0 ? ys.Max() - ys.Min() + 1 : xs.Max() - xs.Min() + 1;
                    Is(9, @long, label);
                    // Every tile of the evacuation beside the bus or beside one that is, floor all of it.
                    bool IsBus(Vec2 p) => bus.Tiles.Any((q) => q.X == p.X && q.Y == p.Y);
                    bool Near(Vec2 p, int d) => bus.Tiles.Any((q) => Math.Abs(q.X - p.X) + Math.Abs(q.Y - p.Y) <= d);
                    foreach (var z in l.EvacZone)
                    {
                        Is(false, IsBus(z), label);
                        Is(true, Near(z, 3), label);
                        Is(TileKinds.Floor, l.Tiles[z.Y * l.Width + z.X], label);
                    }
                    // Its way out, straight on to the edge, is road.
                    var d = Grid.Dirs4[bus.Heading];
                    var nose = bus.Tiles.Aggregate((a, p) => p.X * d.X + p.Y * d.Y > a.X * d.X + a.Y * d.Y ? p : a);
                    for (var p = new Vec2 { X = nose.X + d.X, Y = nose.Y + d.Y }; p.X >= 0 && p.Y >= 0 && p.X < l.Width && p.Y < l.Height; p = new Vec2 { X = p.X + d.X, Y = p.Y + d.Y })
                    {
                        Is(Road, l.Ground![p.Y * l.Width + p.X], $"{label}: its way out at {p.X},{p.Y}");
                    }
                }
            }
            // Nearly always by the bus; a town with no room on its streets has the evacuation of before.
            Assert.True(along > 30, $"{along}");
        }

        [Fact(DisplayName = "a town on its plan (docs/MAPAS.md phase 3) › the mission takes as many turns as its walk asks, and the groups of the dead grow with the map")]
        public void TheMissionTakesAsManyTurnsAsItsWalkAsks()
        {
            for (var seed = 1; seed <= 4; seed++)
            {
                var map = MapSource.Generated(Biomes.City, seed * 7919);
                var s = Setup.CreateMission(Opts(map, seed, MissionKinds.Recovery, difficulty: 2));
                var l = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3 }, Seed = seed * 7919 });
                Assert.Equal(Setup.RouteTurns(MissionKinds.Recovery, l.Route!.Value), s.Mission.TurnsLeft);
                // Eight groups at 48, eleven at 64 (docs/MAPAS.md §6) and a quarter more (20/10): thirteen; and the garrison, if the mission has one.
                // The lone dead of 22/10 (setup.ts LURKERS) come on top, in pods of their own (l1, l2…).
                Assert.Equal(13 + (Setup.Garrison(MissionKinds.Recovery, 2) != null ? 1 : 0), s.Pods.Count((p) => !p.Id.StartsWith("l")));
                Same(l.Frame, s.Frame);
            }
        }

        [Fact(DisplayName = "a town on its plan (docs/MAPAS.md phase 3) › La Lata takes the place it needs: the 4×4 three tiles, its nose where the bus's was; what is behind it, ground again")]
        public void LaLataTakesThePlaceItNeeds()
        {
            for (var seed = 1; seed <= 6; seed++)
            {
                var map = MapSource.Generated(Biomes.City, seed * 7919);
                var bus = Setup.CreateMission(Opts(map, seed, MissionKinds.Recovery, vehicle: 3));
                var car = Setup.CreateMission(Opts(map, seed, MissionKinds.Recovery, vehicle: 0));
                var full = bus.Mission.EvacBus!.Tiles;
                var @short = car.Mission.EvacBus!.Tiles;
                Assert.Equal(Math.Min(6, full.Count), @short.Count);
                // The nose stays: every tile kept was the bus's front.
                var d = Grid.Dirs4[car.Mission.EvacBus!.Heading];
                int Along(Vec2 p) => p.X * d.X + p.Y * d.Y;
                Assert.Equal(full.Max(Along), @short.Max(Along));
                foreach (var p in full)
                {
                    var kept = @short.Any((q) => q.X == p.X && q.Y == p.Y);
                    Assert.Equal(kept ? TileKinds.High : TileKinds.Floor, car.Tiles[p.Y * car.Width + p.X]);
                }
            }
        }

        [Fact(DisplayName = "a town on its plan (docs/MAPAS.md phase 3) › turned with the map: the frame and the bus follow the tiles")]
        public void TurnedWithTheMap()
        {
            var seen = new HashSet<string>();
            for (var seed = 1; seed <= 16; seed++)
            {
                var l = Town(seed);
                var n = Covered(l);
                // The outline is where the frame says, whichever way the map was turned.
                for (var i = 0; i < l.Ground!.Count; i++) Is(l.Ground[i] == Out, n[i] == 1, $"{seed}");
                if (l.EvacBus != null)
                {
                    var d = Grid.Dirs4[l.EvacBus.Heading];
                    seen.Add($"{d.X},{d.Y}");
                }
            }
            Assert.True(seen.Count > 1);
        }

        // ------------------------------------------------------------ the ways in through the outline (docs/MAPAS.md §5.10)

        [Fact(DisplayName = "the ways in through the outline (docs/MAPAS.md §5.10) › the country's bus faces its way out: the track or the road on to the edge, open")]
        public void TheCountrysBusFacesItsWayOut()
        {
            var along = 0;
            var buses = 0;
            foreach (var site in new string?[] { null, Sites.Roadside })
            {
                for (var seed = 1; seed <= 20; seed++)
                {
                    var l = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.Wilds, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3, 3, 3 }, Seed = seed * 7919, Site = site });
                    var label = $"{site ?? "wilds"}/{seed}";
                    if (l.EvacBus == null) continue;
                    buses++;
                    // By its far end, along its doors; where there is no room there, the evacuation of before.
                    if (l.EvacZone.Count != 21) continue;
                    along++;
                    var d = Grid.Dirs4[l.EvacBus.Heading];
                    var nose = l.EvacBus.Tiles.Aggregate((a, p) => p.X * d.X + p.Y * d.Y > a.X * d.X + a.Y * d.Y ? p : a);
                    for (var p = new Vec2 { X = nose.X + d.X, Y = nose.Y + d.Y }; p.X >= 0 && p.Y >= 0 && p.X < l.Width && p.Y < l.Height; p = new Vec2 { X = p.X + d.X, Y = p.Y + d.Y })
                    {
                        Is(Road, l.Ground![p.Y * l.Width + p.X], $"{label}: its way out at {p.X},{p.Y}");
                        Is(TileKinds.Floor, l.Tiles[p.Y * l.Width + p.X], $"{label}: its way out at {p.X},{p.Y}");
                    }
                }
            }
            Assert.True(along > buses * 0.8, $"{along} of {buses}");
        }

        [Fact(DisplayName = "the ways in through the outline (docs/MAPAS.md §5.10) › every map shut round has room by an edge for the biggest wave of the dead")]
        public void EveryMapShutRoundHasRoomByAnEdge()
        {
            var maps = new List<(string, MapSource)>();
            for (var seed = 1; seed <= 5; seed++)
            {
                maps.Add(("city", MapSource.Generated(Biomes.City, seed * 7919)));
                maps.Add(("industrial", MapSource.Generated(Biomes.City, seed * 7919, Sites.Industrial)));
                maps.Add(("wilds", MapSource.Generated(Biomes.Wilds, seed * 7919)));
                maps.Add(("roadside", MapSource.Generated(Biomes.Wilds, seed * 7919, Sites.Roadside)));
                maps.Add(("facility", MapSource.Generated(Biomes.Facility, seed * 7919)));
            }
            foreach (var (name, map) in maps)
            {
                var s = Setup.CreateMission(Opts(map, 1, MissionKinds.Sabotage, difficulty: 4));
                var label = $"{name}/{map.Seed}";
                s.Mission.Reinforcements = new List<Reinforcement> { new Reinforcement { Id = "r1", Turn = 1, Pos = null, Units = Setup.HordeWave(4), Landed = false, Horde = true } };
                NewSim(s, 2, 0).LandReinforcements(1);
                Assert.True(s.Mission.Reinforcements[0].Landed, label);
                // All of it by the edge, in by a way in.
                foreach (var u in s.Units.Where((x) => x.Id.StartsWith("r1"))) Assert.True(Math.Min(Math.Min(u.Pos.X, u.Pos.Y), Math.Min(s.Width - 1 - u.Pos.X, s.Height - 1 - u.Pos.Y)) < 4, label);
            }
        }
    }
}
