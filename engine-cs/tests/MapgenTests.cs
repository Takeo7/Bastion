using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bastion.Engine;
using Xunit;
using Xunit.Sdk;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/mapgen.test.ts, test by test: the generated maps (mapgen.ts generateLayout) and the missions
    /// made on them (setup.ts createMission), their timers (missionTurns, routeTurns), their doors and the squad at the
    /// start. Nothing is left out. «generate quickly» measures time: it is in MapgenTests.Timed, alone in the time budgets'
    /// collection (beside the other tests it misses its bound, as in TypeScript), so the rest, some 30 s of maps, runs
    /// beside the other classes.
    /// </summary>
    public class MapgenTests
    {
        private static readonly string[] BiomeList = { Biomes.City, Biomes.Wilds, Biomes.Facility };
        private static readonly string[] Kinds = { MissionKinds.Elimination, MissionKinds.Recovery, MissionKinds.Sabotage, MissionKinds.Hack, MissionKinds.Rescue };
        /// <summary>`ground` codes (GroundKind).</summary>
        private const int Road = 1;
        private const int Indoor = 2;

        private static MissionLayout Layout(string biome, string kind, int seed)
        {
            return Mapgen.GenerateLayout(new GenOptions { Biome = biome, Kind = kind, PodSizes = new List<int> { 2, 3, 3, 2 }, Seed = seed * 104729 });
        }

        /// <summary>Walkable tiles reachable from `start` with the movement rules.</summary>
        private static HashSet<int> Reachable(MissionLayout l, Vec2 start)
        {
            var s = new GameState { Width = l.Width, Height = l.Height, Tiles = l.Tiles, Elev = l.Elev };
            var seen = new HashSet<int> { start.Y * l.Width + start.X };
            var queue = new List<Vec2> { start };
            while (queue.Count > 0)
            {
                var p = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                foreach (var d in Grid.Dirs8)
                {
                    if (Paths.StepCost(s, p.X, p.Y, d.X, d.Y) == null) continue;
                    var q = new Vec2 { X = p.X + d.X, Y = p.Y + d.Y };
                    var i = q.Y * l.Width + q.X;
                    if (seen.Contains(i)) continue;
                    seen.Add(i);
                    queue.Add(q);
                }
            }
            return seen;
        }

        /// <summary>Bounding boxes of the 4-connected areas of tiles that pass `inside`, with their tile count.</summary>
        private static List<(int W, int H, int N)> Areas(MissionLayout l, Func<int, bool> inside)
        {
            var seen = new HashSet<int>();
            var out_ = new List<(int W, int H, int N)>();
            for (var i = 0; i < l.Tiles.Count; i++)
            {
                if (seen.Contains(i) || !inside(i)) continue;
                var queue = new List<int> { i };
                seen.Add(i);
                var (x0, y0, x1, y1, n) = (int.MaxValue, int.MaxValue, -1, -1, 0);
                while (queue.Count > 0)
                {
                    var j = queue[queue.Count - 1];
                    queue.RemoveAt(queue.Count - 1);
                    var x = j % l.Width;
                    var y = (j - x) / l.Width;
                    (x0, y0, x1, y1, n) = (Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y), n + 1);
                    foreach (var d in Grid.Dirs4)
                    {
                        var k = (y + d.Y) * l.Width + x + d.X;
                        if (x + d.X < 0 || y + d.Y < 0 || x + d.X >= l.Width || y + d.Y >= l.Height || seen.Contains(k) || !inside(k)) continue;
                        seen.Add(k);
                        queue.Add(k);
                    }
                }
                out_.Add((x1 - x0 + 1, y1 - y0 + 1, n));
            }
            return out_;
        }

        private static (double X, double Y) Centroid(IReadOnlyCollection<Vec2> ps)
        {
            return (ps.Sum((p) => (double)p.X) / ps.Count, ps.Sum((p) => (double)p.Y) / ps.Count);
        }

        private static (double X, double Y) V(Vec2 p)
        {
            return (p.X, p.Y);
        }

        private static double Dist((double X, double Y) a, (double X, double Y) b)
        {
            return Js.Hypot(a.X - b.X, a.Y - b.Y);
        }

        // ------------------------------------------------------------ what TypeScript does in passing

        /// <summary>`{ ...s, units }`: the same state (the same lists), with other units.</summary>
        private static GameState WithUnits(GameState s, params Unit[] units)
        {
            var copy = new GameState();
            foreach (var p in typeof(GameState).GetProperties()) if (p.CanWrite) p.SetValue(copy, p.GetValue(s));
            copy.Units = units.ToList();
            return copy;
        }

        /// <summary>`decor[i]`, undefined (null) past either end.</summary>
        private static int? CodeAt(IReadOnlyList<int>? decor, int i)
        {
            return decor != null && i >= 0 && i < decor.Count ? decor[i] : (int?)null;
        }

        /// <summary>toBe, with the test's label.</summary>
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

        private static MissionOptions Generated(string biome, int seed, List<int> slots, int rngSeed, string? kind, string? site = null)
        {
            return new MissionOptions { Map = MapSource.Generated(biome, seed, site), Slots = slots, Rng = new Rng(rngSeed), Kind = kind };
        }

        // ------------------------------------------------------------ procedural maps

        [Fact(DisplayName = "procedural maps › puts the opening medicines on reachable clinic room floor, beyond its doorways")]
        public void PutsTheOpeningMedicinesOnReachableClinicRoomFloor()
        {
            for (var seed = 0; seed < 80; seed++)
            {
                var s = Setup.CreateMission(new MissionOptions
                {
                    Map = MapSource.Generated(Biomes.City, seed * 104729),
                    Slots = seed % 2 != 0 ? new List<int> { 0 } : new List<int> { 0, 1 },
                    Rng = new Rng(seed),
                    Kind = MissionKinds.Recovery,
                    Difficulty = 1,
                    CampaignMission = new CampaignMission { Id = "opening", Name = "El consultorio de Bielsa", Story = 1 },
                });
                var label = $"opening/{seed}";
                Is(MissionKinds.Recovery, s.Mission.Kind, label);
                var p = s.Mission.Item!.Pos!;
                var i = p.Y * s.Width + p.X;
                Is(TileKinds.Floor, s.Tiles[i], label);
                Is(Indoor, s.Ground![i], label);
                // A doorstep is tagged INDOOR too: all eight surrounding tiles must
                // still belong to the building, and none may be an outside entrance.
                foreach (var d in Grid.Dirs8) Is(Indoor, s.Ground![(p.Y + d.Y) * s.Width + p.X + d.X], label);
                var room = Looting.RoomAt(s, p);
                Assert.True(room != null, label);
                var furniture = new HashSet<string?>(room!.Tiles.SelectMany((q) => Grid.Dirs4.Select((d) => Decor.DecorOf(CodeAt(s.Decor, (q.Y + d.Y) * s.Width + q.X + d.X))?.Kind)));
                Assert.True(furniture.Contains(DecorKinds.ExamTable), label);
                Assert.True(furniture.Contains(DecorKinds.MedicalShelf), label);
                var scout = s.Units.Find((u) => u.Team == Teams.Xcom)!;
                var reach = Paths.ComputeReach(WithUnits(s, scout), scout, 100_000);
                Assert.True(reach.Cost.Has(i), label);
            }
        }

        [Fact(DisplayName = "procedural maps › keeps clinic furniture out of other missions")]
        public void KeepsClinicFurnitureOutOfOtherMissions()
        {
            var s = Setup.CreateMission(Generated(Biomes.City, 3, new List<int> { 0 }, 3, MissionKinds.Recovery));
            Assert.DoesNotContain(s.Decor!, (d) => new[] { DecorKinds.HospitalBed, DecorKinds.MedicalShelf }.Contains(Decor.DecorOf(d)?.Kind ?? ""));
        }

        [Fact(DisplayName = "procedural maps › are deterministic for a seed")]
        public void AreDeterministicForASeed()
        {
            var a = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 2, 2, 3 }, Seed = 42 });
            var b = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 2, 2, 3 }, Seed = 42 });
            Same(a, b);
        }

        [Fact(DisplayName = "procedural maps › are of their size (a town 64, a hospital's 72 × 64 either way round, the outskirts and the factory 72), with streets or tracks on the ground layer")]
        public void AreOfTheirSize()
        {
            foreach (var biome in BiomeList)
            {
                var l = Layout(biome, MissionKinds.Elimination, 3);
                // (The outskirts and the factory 72: docs/MAPAS.md phase 5.)
                var side = biome == Biomes.City ? Mapgen.TownSize : 72;
                Assert.Equal(new[] { side, side }, new[] { l.Width, l.Height });
                foreach (var count in new[] { l.Tiles.Count, l.Elev.Count, l.Ground!.Count }) Assert.Equal(side * side, count);
                var roads = l.Ground!.Count((g) => g == Road);
                Assert.True(roads > 40, $"{biome}: {roads}");
            }
            var hospital = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Rescue, PodSizes = new List<int> { 3 }, Seed = 7, Medical = MedicalSiteKinds.Hospital });
            Assert.Equal(new[] { 64, 72 }, new[] { hospital.Width, hospital.Height }.OrderBy((x) => x));
        }

        [Fact(DisplayName = "procedural maps › every biome and mission kind places the squad, pods and objective where the squad can reach them")]
        public void EveryBiomeAndMissionKindPlacesTheSquadPodsAndObjectiveWhereTheSquadCanReachThem()
        {
            foreach (var biome in BiomeList)
            {
                foreach (var kind in Kinds)
                {
                    for (var seed = 1; seed <= 8; seed++)
                    {
                        var plan = Setup.PodPlan(1 + (seed % 4));
                        var opts = Generated(biome, seed * 7919, new List<int> { 0, 1 }, seed, kind);
                        opts.Pods = plan;
                        var s = Setup.CreateMission(opts);
                        var label = $"{biome}/{kind}/{seed}";
                        Is(kind, s.Mission.Kind, label);
                        var soldiers = s.Units.Where((u) => u.Team == Teams.Xcom && !u.Captive).ToList();
                        Is(6, soldiers.Count, label);
                        // A scout alone on the map (no enemies blocking) reaches everything that matters.
                        var scout = Events.CloneUnit(soldiers[0]);
                        var alone = WithUnits(s, scout);
                        var reach = Paths.ComputeReach(alone, scout, 100_000);
                        bool Near(Vec2 p) => new[] { p }.Concat(Grid.Dirs8.Select((d) => new Vec2 { X = p.X + d.X, Y = p.Y + d.Y })).Any((q) => reach.Cost.Has(q.Y * s.Width + q.X) || (q.X == scout.Pos.X && q.Y == scout.Pos.Y));
                        foreach (var u in s.Units) Assert.True(Near(u.Pos), $"{label}: {u.Id} at {u.Pos.X},{u.Pos.Y}");
                        foreach (var z in s.Mission.EvacZone)
                        {
                            Assert.True(Grid.CanStandOn(s.Tiles[z.Y * s.Width + z.X]), label);
                            Assert.True(Near(z), $"{label}: evac");
                        }
                        // A town's or the country's along the bus's doors (docs/MAPAS.md §4.5), 3 × 7; elsewhere, or where there is no room, 3 × 3.
                        if (kind != MissionKinds.Elimination) Assert.True(new[] { 9, 21 }.Contains(s.Mission.EvacZone.Count), $"{label}: {s.Mission.EvacZone.Count}");
                        if (kind == MissionKinds.Hack) Assert.True(Near(s.Mission.Terminal!), $"{label}: terminal");
                        // The lone dead of 22/10 (setup.ts LURKERS) come on top: their own pods, l1, l2…
                        var aliens = s.Units.Where((u) => u.Team == Teams.Alien && !u.Objective).ToList();
                        var lone = aliens.Where((u) => u.PodId?.StartsWith("l", StringComparison.Ordinal) == true).ToList();
                        // The plan, and the living enemy's whole strength from the start (playtest 2026-10-13).
                        Is(plan.Sum((p) => p.Count) + (Setup.Garrison(kind, 2)?.Count ?? 0), aliens.Count - lone.Count, label);
                        Assert.True(lone.Count <= Setup.Lurkers[2], $"{label}: {lone.Count}");
                        foreach (var a in aliens)
                        {
                            var d = soldiers.Min((x) => Js.Hypot(x.Pos.X - a.Pos.X, x.Pos.Y - a.Pos.Y));
                            Assert.True(d >= 12, $"{label}: {a.Id} too close ({d})");
                        }
                    }
                }
            }
        }

        [Fact(DisplayName = "procedural maps › put the squad down with room to move: no soldier hemmed in by the others at the start (20/10)")]
        public void PutTheSquadDownWithRoomToMove()
        {
            foreach (var biome in BiomeList)
            {
                for (var seed = 1; seed <= 12; seed++)
                {
                    var s = Setup.CreateMission(Generated(biome, seed * 7919, new List<int> { 0 }, seed, MissionKinds.Recovery));
                    foreach (var u in s.Units.Where((x) => x.Team == Teams.Xcom))
                    {
                        var reach = Paths.ComputeReach(s, u, Paths.MaxMoveCost(u));
                        Assert.True(Paths.Destinations(s, u, reach).Count > 0, $"{biome}/{seed}: {u.Id} at {u.Pos.X},{u.Pos.Y}");
                    }
                }
            }
        }

        [Fact(DisplayName = "procedural maps › leave no walkable pocket out of reach (rooftops, rooms, the heart of a grove)")]
        public void LeaveNoWalkablePocketOutOfReach()
        {
            foreach (var biome in BiomeList)
            {
                for (var seed = 1; seed <= 20; seed++)
                {
                    var l = Layout(biome, seed % 2 != 0 ? MissionKinds.Recovery : MissionKinds.Hack, seed);
                    var reach = Reachable(l, l.Spawns[0]);
                    var lost = l.Tiles.Select((t, i) => Grid.IsWalkable(t) && !reach.Contains(i) ? i : -1).Where((i) => i >= 0).ToList();
                    Assert.True(lost.Count == 0, $"{biome}/{seed}: [{string.Join(", ", lost.Take(40))}]");
                }
            }
        }

        [Fact(DisplayName = "procedural maps › build walled buildings of believable sizes, with rooms at least 3×3 off corridors 2 wide")]
        public void BuildWalledBuildingsOfBelievableSizes()
        {
            var openings = new HashSet<string> { TileKinds.Wall, TileKinds.Door, TileKinds.DoorOpen, TileKinds.Window, TileKinds.WindowBroken };
            foreach (var biome in BiomeList)
            {
                var rooms = 0;
                for (var seed = 1; seed <= 12; seed++)
                {
                    var l = Layout(biome, MissionKinds.Recovery, seed);
                    // A building is a full rectangle from 7 tiles (cabins, houses) to 16 (halls), walls included.
                    foreach (var b in Areas(l, (i) => l.Ground![i] == Indoor))
                    {
                        Is(b.W * b.H, b.N, $"{biome}/{seed}: ragged building");
                        Assert.True(Math.Min(b.W, b.H) >= 7, $"{biome}/{seed}: {b.W} × {b.H}");
                        // (Up to a factory's hall of 30, docs/MAPAS.md §4.2: halls 18–24, the factory's 24–30, offices up to 18, flats 18.)
                        Assert.True(Math.Max(b.W, b.H) <= 30, $"{biome}/{seed}: {b.W} × {b.H}");
                    }
                    foreach (var r in Areas(l, (i) => l.Ground![i] == Indoor && !openings.Contains(l.Tiles[i])))
                    {
                        // A corridor two wide, or a bathroom two by three (Arturo, 19/10), a lavatory, a pantry.
                        var corridor = Math.Min(r.W, r.H) == 2 && Math.Max(r.W, r.H) >= 6;
                        var small = Math.Min(r.W, r.H) == 2 && Math.Max(r.W, r.H) <= 5;
                        if (!corridor && !small) Assert.True(Math.Min(r.W, r.H) >= 3, $"{biome}/{seed}: room {r.W}×{r.H}");
                        rooms++;
                    }
                }
                Assert.True(rooms > 12, $"{biome}: {rooms}");
            }
        }

        [Fact(DisplayName = "procedural maps › start the squad on the ground (in a corner, or a town's way in), with the objective far and the evac away from it")]
        public void StartTheSquadOnTheGround()
        {
            foreach (var biome in BiomeList)
            {
                foreach (var kind in new[] { MissionKinds.Recovery, MissionKinds.Sabotage, MissionKinds.Hack, MissionKinds.Rescue })
                {
                    for (var seed = 1; seed <= 6; seed++)
                    {
                        var l = Layout(biome, kind, seed);
                        var label = $"{biome}/{kind}/{seed}";
                        Is(8, l.Spawns.Count, label);
                        var W = l.Width;
                        int Edge(Vec2 p) => Math.Min(Math.Min(p.X, p.Y), Math.Min(W - 1 - p.X, l.Height - 1 - p.Y));
                        foreach (var p in l.Spawns)
                        {
                            // A town's squad comes in by a street from the edge, the country's by its track, a factory's by its road (docs/MAPAS.md §5.10).
                            Assert.True(Edge(p) < 10, $"{label}: {p.X},{p.Y}");
                            Is(0, l.Elev[p.Y * W + p.X], label);
                        }
                        var squad = Centroid(l.Spawns);
                        var objective = l.Item ?? l.ObjectiveSpot ?? l.Terminal;
                        Assert.True(objective != null, label);
                        var far = Dist(V(objective!), squad);
                        Assert.True(far > 26, $"{label}: {far}");
                        var evac = Centroid(l.EvacZone);
                        var away = Dist(evac, squad);
                        Assert.True(away > 20, $"{label}: {away}");
                        Assert.True(l.EvacZone.All((z) => l.Ground![z.Y * W + z.X] != Indoor && l.Elev[z.Y * W + z.X] == 0), label);
                    }
                }
            }
        }

        [Fact(DisplayName = "procedural maps › put the data and the VIP indoors, and a hack console with room around it")]
        public void PutTheDataAndTheVipIndoors()
        {
            foreach (var biome in BiomeList)
            {
                for (var seed = 1; seed <= 8; seed++)
                {
                    foreach (var kind in new[] { MissionKinds.Recovery, MissionKinds.Rescue })
                    {
                        var m = Layout(biome, kind, seed);
                        var spot = (kind == MissionKinds.Recovery ? m.Item : m.ObjectiveSpot)!;
                        Is(Indoor, m.Ground![spot.Y * m.Width + spot.X], $"{biome}/{kind}/{seed}");
                    }
                    var l = Layout(biome, MissionKinds.Hack, seed);
                    var t = l.Terminal!;
                    Assert.Equal(TileKinds.Console, l.Tiles[t.Y * l.Width + t.X]);
                    var free = Grid.Dirs8.Count((d) => Grid.CanStandOn(Js.At(l.Tiles, (t.Y + d.Y) * l.Width + t.X + d.X, TileKinds.Wall)));
                    Assert.True(free >= 3, $"{biome}/{seed}: {free}");
                }
            }
        }

        [Fact(DisplayName = "procedural maps › spread the pods away from the squad, each with a patrol route")]
        public void SpreadThePodsAwayFromTheSquad()
        {
            foreach (var biome in BiomeList)
            {
                for (var seed = 1; seed <= 8; seed++)
                {
                    var l = Layout(biome, MissionKinds.Elimination, seed);
                    var squad = Centroid(l.Spawns);
                    Assert.Equal(4, l.Pods.Count);
                    foreach (var pod in l.Pods)
                    {
                        Assert.True(pod.Tiles.Count > 0);
                        Assert.True(pod.Patrol.Count > 1);
                        foreach (var p in pod.Tiles) Assert.True(Dist(V(p), squad) >= 13, $"{biome}/{seed}: {p.X},{p.Y}");
                    }
                    var anchors = l.Pods.Select((p) => p.Patrol[0]).ToList();
                    var closest = anchors.SelectMany((a, i) => anchors.Skip(i + 1).Select((b) => Dist(V(a), V(b)))).DefaultIfEmpty(double.PositiveInfinity).Min();
                    Assert.True(closest >= 6, $"{biome}/{seed}: {closest}");
                }
            }
        }

        [Fact(DisplayName = "procedural maps › city maps have rooftops and openings; wilds have hills")]
        public void CityMapsHaveRooftopsAndOpenings()
        {
            var roofs = 0;
            var openings = 0;
            var hills = 0;
            for (var seed = 1; seed <= 10; seed++)
            {
                var s = Setup.CreateMission(Generated(Biomes.City, seed, new List<int> { 0 }, seed, MissionKinds.Elimination));
                // Raised ground, or the storeys above the ground with their roofs (docs/FASE6-PLAN.md).
                roofs += s.Elev.Count((e) => e > 0) + (s.Levels ?? new List<Level>()).Sum((l) => l.Tiles.Count((t) => t != null));
                openings += s.Tiles.Count((t) => t == TileKinds.Door || t == TileKinds.Window);
                // Every pod walks a round, but the living posted upstairs (docs/FASE6-PLAN.md) and the lone dead (22/10), who stay put.
                foreach (var pod in s.Pods)
                {
                    if (!pod.Id.StartsWith("l", StringComparison.Ordinal) && !s.Units.Any((u) => u.PodId == pod.Id && u.Pos.Z > 0)) Assert.True(pod.Patrol.Count > 0, $"{seed}: {pod.Id}");
                }
                hills += Layout(Biomes.Wilds, MissionKinds.Elimination, seed).Elev.Count((e) => e > 0);
            }
            Assert.True(roofs > 0);
            Assert.True(openings > 0);
            Assert.True(hills > 0);
        }

        [Fact(DisplayName = "procedural maps › say what their cover is meant to be, only on cover, with cars whole")]
        public void SayWhatTheirCoverIsMeantToBe()
        {
            foreach (var biome in BiomeList)
            {
                var kinds = new HashSet<string>();
                for (var seed = 1; seed <= 6; seed++)
                {
                    var l = Layout(biome, seed % 2 != 0 ? MissionKinds.Recovery : MissionKinds.Elimination, seed);
                    var label = $"{biome}/{seed}";
                    Assert.Equal(l.Width * l.Height, l.Decor!.Count);
                    var again = Mapgen.GenerateLayout(new GenOptions { Biome = biome, Kind = seed % 2 != 0 ? MissionKinds.Recovery : MissionKinds.Elimination, PodSizes = new List<int> { 2, 3, 3, 2 }, Seed = seed * 104729 });
                    Assert.True(again.Decor!.SequenceEqual(l.Decor), label);
                    for (var i = 0; i < l.Decor.Count; i++)
                    {
                        var look = Decor.DecorOf(l.Decor[i]);
                        if (look == null) continue;
                        kinds.Add(look.Kind);
                        // A building's stairs are ladder tiles (docs/FASE6-PLAN.md), a flight's lower ones its parts (docs/MAPAS.md,
                        // phase 2); everything else dressed is cover.
                        var flight = look.Kind == DecorKinds.Stairs || (look.Kind == DecorKinds.Part && Decor.DecorOf(CodeAt(l.Decor, Decor.AnchorOf(l.Decor, l.Width, i)))?.Kind == DecorKinds.Stairs);
                        var allowed = flight ? new[] { TileKinds.Ladder } : new[] { TileKinds.Low, TileKinds.High };
                        Assert.True(allowed.Contains(l.Tiles[i]), $"{label}: {look.Kind} on {l.Tiles[i]}");
                        // A car whole, over its three tiles (docs/MAPAS.md, phase 1); every other tile of an object leads to its anchor.
                        if (look.Kind == DecorKinds.CarFront) Is(Decor.Footprint.Car[1], Decor.ObjectTiles(l.Decor, l.Width, i).Count, $"{label}: a car in pieces");
                        if (look.Kind == DecorKinds.Part) Assert.True(Decor.AnchorOf(l.Decor, l.Width, i) >= 0, $"{label}: a part of nothing");
                    }
                    // Everything on the ground is dressed; only rooftop parapets are left to the client.
                    var plain = l.Tiles.Where((t, i) => (t == TileKinds.Low || t == TileKinds.High) && l.Decor[i] == 0 && l.Elev[i] == 0).Count();
                    Is(0, plain, label);
                }
                var expected = new Dictionary<string, string[]>
                {
                    [Biomes.City] = new[] { DecorKinds.CarFront, DecorKinds.Bollard, DecorKinds.Shelf, DecorKinds.Desk, DecorKinds.Fountain },
                    [Biomes.Wilds] = new[] { DecorKinds.Fence, DecorKinds.GatePost, DecorKinds.Hay, DecorKinds.Tree, DecorKinds.Bush, DecorKinds.Rock },
                    [Biomes.Facility] = new[] { DecorKinds.Shelf, DecorKinds.Machine, DecorKinds.Container, DecorKinds.Pallets, DecorKinds.Drums, DecorKinds.Truck, DecorKinds.Silo, DecorKinds.CarFront },
                }[biome];
                foreach (var k in expected) Assert.True(kinds.Contains(k), $"{biome}: {k}");
            }
            // The mission keeps the hints; hand-made maps have none.
            var s = Setup.CreateMission(Generated(Biomes.City, 3, new List<int> { 0 }, 3, MissionKinds.Elimination));
            Assert.Contains(s.Decor!, (code) => Decor.DecorOf(code)?.Kind == DecorKinds.CarFront);
            Assert.Null(Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0 }, Rng = new Rng(3) }).Decor);
        }

        [Fact(DisplayName = "procedural maps › a factory: its fenced compound with gates onto the apron, the road in from the squad's side, out by the apron to the edge (docs/MAPAS.md phase 5)")]
        public void AFactoryItsFencedCompound()
        {
            for (var seed = 1; seed <= 8; seed++)
            {
                var m = Layout(Biomes.Facility, MissionKinds.Sabotage, seed);
                var label = $"{seed}";
                // The squad on the road in, a few tiles from the edge; the bus by the apron's way out, far from it.
                var squad = Centroid(m.Spawns);
                var edge = Math.Min(Math.Min(squad.X, squad.Y), Math.Min(m.Width - 1 - squad.X, m.Height - 1 - squad.Y));
                Assert.True(edge < 8, $"{label}: {edge}");
                Assert.True(m.EvacBus != null, label);
                var bus = Dist(Centroid(m.EvacBus!.Tiles), squad);
                Assert.True(bus > 20, $"{label}: {bus}");
                // A fence: long runs of wall, with gates in them; and the outline round it all.
                var walls = m.Tiles.Count((t) => t == TileKinds.Wall);
                Assert.True(walls > 150, $"{label}: {walls}");
                Assert.True(m.Frame!.Count > 0, label);
                Assert.True(m.Frame!.Select((f) => f.Kind).Distinct().Count() >= 2, label);
            }
        }

        /// <summary>The test of procedural maps that measures time, measured alone.</summary>
        [Collection(TimeBudgets.Name)]
        public class Timed
        {
            [Fact(DisplayName = "procedural maps › generate quickly")]
            public void GenerateQuickly()
            {
                var start = Stopwatch.StartNew();
                foreach (var biome in BiomeList) for (var seed = 1; seed <= 10; seed++) Layout(biome, MissionKinds.Hack, seed);
                var each = start.Elapsed.TotalMilliseconds / 30;
                Assert.True(each < 60, $"{each:F1} ms a map");
            }
        }

        // ------------------------------------------------------------ the scenarios (playtest 2026-10-14, #25)

        private static string? KindOf(MissionLayout l, int i)
        {
            return Decor.DecorOf(l.Decor![i])?.Kind;
        }

        [Fact(DisplayName = "the scenarios (playtest 2026-10-14, #25) › the service area and the industrial estate: everything reachable, every objective placed")]
        public void TheServiceAreaAndTheIndustrialEstate()
        {
            foreach (var (biome, site) in new[] { (Biomes.Wilds, Sites.Roadside), (Biomes.City, Sites.Industrial) })
            {
                foreach (var kind in Kinds)
                {
                    for (var seed = 1; seed <= 5; seed++)
                    {
                        var label = $"{site}/{kind}/{seed}";
                        var opts = Generated(biome, seed * 7919, new List<int> { 0, 1 }, seed, kind, site);
                        opts.Pods = Setup.PodPlan(2);
                        var s = Setup.CreateMission(opts);
                        Is(site, s.Site, label);
                        var scout = s.Units.Find((u) => u.Team == Teams.Xcom)!;
                        var reach = Paths.ComputeReach(WithUnits(s, scout), scout, 100_000);
                        bool Near(Vec2 p) => new[] { p }.Concat(Grid.Dirs8.Select((d) => new Vec2 { X = p.X + d.X, Y = p.Y + d.Y })).Any((q) => reach.Cost.Has(q.Y * s.Width + q.X) || (q.X == scout.Pos.X && q.Y == scout.Pos.Y));
                        foreach (var u in s.Units) Assert.True(Near(u.Pos), $"{label}: {u.Id}");
                        foreach (var z in s.Mission.EvacZone) Assert.True(Near(z), $"{label}: evac");
                        if (kind == MissionKinds.Recovery) Assert.True(s.Mission.Item != null, label);
                    }
                }
            }
        }

        [Fact(DisplayName = "the scenarios (playtest 2026-10-14, #25) › they are made of what such places have, each object a whole block of its own")]
        public void TheyAreMadeOfWhatSuchPlacesHave()
        {
            Dictionary<string, int> Counts(MissionLayout l)
            {
                var n = new Dictionary<string, int>();
                for (var i = 0; i < l.Tiles.Count; i++)
                {
                    var k = KindOf(l, i);
                    if (k != null) n[k] = (n.TryGetValue(k, out var c) ? c : 0) + 1;
                }
                return n;
            }
            int Of(Dictionary<string, int> n, string k) => n.TryGetValue(k, out var c) ? c : 0;
            for (var seed = 1; seed <= 6; seed++)
            {
                var road = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.Wilds, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3, 3 }, Seed = seed, Site = Sites.Roadside });
                var n = Counts(road);
                Assert.Equal(Sites.Roadside, road.Site);
                foreach (var k in new[] { DecorKinds.Truck, DecorKinds.WaterTower, DecorKinds.Sign, DecorKinds.Tank, DecorKinds.Counter }) Assert.True(Of(n, k) > 0, $"roadside {seed}: {k}");
                // A lorry is 2 × 6 where it fits, else 2 × 4 (docs/MAPAS.md, phase 1), a water tower 2 × 2: blocks that never touch another of their kind.
                foreach (var a in Areas(road, (i) => KindOf(road, i) == DecorKinds.Truck)) Assert.True(new[] { 8, 12 }.Contains(a.N), $"roadside {seed}: truck {a.N}");
                foreach (var a in Areas(road, (i) => KindOf(road, i) == DecorKinds.WaterTower)) Is(4, a.N, $"roadside {seed}: waterTower");
                var estate = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3, 3 }, Seed = seed, Site = Sites.Industrial });
                var m = Counts(estate);
                Assert.True(Of(m, DecorKinds.Container) >= 10, $"industrial {seed}: {Of(m, DecorKinds.Container)}");
                foreach (var a in Areas(estate, (i) => KindOf(estate, i) == DecorKinds.Container)) Assert.True(new[] { a.W, a.H }.OrderBy((x) => x).SequenceEqual(new[] { 2, 5 }), $"industrial {seed}: {a.W} × {a.H}");
                // No homes on an industrial estate.
                Assert.Equal(0, Of(m, DecorKinds.Bed));
            }
            // A site only where it belongs, and a map without one is the biome's usual.
            Assert.Null(Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.Facility, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3 }, Seed = 1, Site = Sites.Roadside }).Site);
            Same(
                Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3 }, Seed = 1 }),
                Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3 }, Seed = 1, Site = Sites.Roadside }));
        }

        // ------------------------------------------------------------ mission timers

        [Fact(DisplayName = "mission timers › a town has a supermarket now and then: one storey, its front glazed but its double door, checkouts, aisles and fridges inside (playtest 20/10, #18)")]
        public void ATownHasASupermarketNowAndThen()
        {
            var towns = 0;
            for (var seed = 1; seed <= 12; seed++)
            {
                var l = Layout(Biomes.City, MissionKinds.Recovery, seed);
                var markets = l.Markets ?? new List<MarketSite>();
                Assert.True(markets.Count <= 1, $"city/{seed}: {markets.Count}");
                if (markets.Count == 0) continue;
                towns++;
                var m = markets[0];
                var label = $"city/{seed}: market {m.X},{m.Y}";
                var W = l.Width;
                string At(int x, int y) => l.Tiles[y * W + x];
                // Its front: every tile glass but the corners and the two doors side by side.
                var d = Grid.Dirs4[m.Front];
                var along = d.X == 0 ? new Vec2 { X = 1, Y = 0 } : new Vec2 { X = 0, Y = 1 };
                var start = new Vec2 { X = d.X > 0 ? m.X + m.W - 1 : m.X, Y = d.Y > 0 ? m.Y + m.H - 1 : m.Y };
                var n = d.X == 0 ? m.W : m.H;
                var front = Enumerable.Range(0, n).Select((k) => At(start.X + along.X * k, start.Y + along.Y * k)).ToList();
                Is(TileKinds.Wall, front[0], label);
                Is(TileKinds.Wall, front[n - 1], label);
                var doors = front.Select((t, k) => t == TileKinds.Door ? k : -1).Where((k) => k >= 0).ToList();
                Is(2, doors.Count, label);
                Is(1, doors[1] - doors[0], label);
                Assert.True(front.Skip(1).Take(n - 2).All((t) => t == TileKinds.Window || t == TileKinds.Door), label);
                // One storey, and what a supermarket has inside.
                var kinds = new Dictionary<string, int>();
                var floor = new List<int>();
                for (var y = m.Y; y < m.Y + m.H; y++)
                {
                    for (var x = m.X; x < m.X + m.W; x++)
                    {
                        Is(1, l.Storeys![y * W + x], label);
                        var k = Decor.DecorOf(l.Decor![y * W + x])?.Kind;
                        if (k != null) kinds[k] = (kinds.TryGetValue(k, out var c) ? c : 0) + 1;
                        if (Grid.CanStandOn(At(x, y)) && At(x, y) != TileKinds.Door) floor.Add(y * W + x);
                    }
                }
                int Of(string k) => kinds.TryGetValue(k, out var c) ? c : 0;
                Assert.True(Of(DecorKinds.Counter) >= 2, $"{label}: {Of(DecorKinds.Counter)} counters");
                Assert.True(Of(DecorKinds.Shelf) >= 8, $"{label}: {Of(DecorKinds.Shelf)} shelves");
                Assert.True(Of(DecorKinds.DrinksFridge) >= 6, $"{label}: {Of(DecorKinds.DrinksFridge)} fridges");
                // All its floor reached from its doors.
                var seen = new HashSet<int>();
                var queue = doors.Select((k) => (start.Y + along.Y * k) * W + start.X + along.X * k).ToList();
                foreach (var i in queue) seen.Add(i);
                for (var q = 0; q < queue.Count; q++)
                {
                    var i = queue[q];
                    foreach (var e in Grid.Dirs4)
                    {
                        var j = i + e.Y * W + e.X;
                        var x = j % W;
                        var y = (int)Math.Floor((double)j / W);
                        if (seen.Contains(j) || x < m.X || y < m.Y || x >= m.X + m.W || y >= m.Y + m.H || !Grid.CanStandOn(At(x, y))) continue;
                        seen.Add(j);
                        queue.Add(j);
                    }
                }
                foreach (var i in floor) Assert.True(seen.Contains(i), $"{label}: {i % W},{i / W} out of reach");
            }
            Assert.True(towns >= 4, $"{towns} towns with a supermarket");
        }

        [Fact(DisplayName = "mission timers › nothing stands inside a window by chance; a barricaded room has every window barricaded (playtest 21/10, #20)")]
        public void NothingStandsInsideAWindowByChance()
        {
            var barricades = 0;
            for (var seed = 1; seed <= 8; seed++)
            {
                var l = Layout(Biomes.City, MissionKinds.Recovery, seed);
                var W = l.Width;
                string? At(int x, int y) => Js.At<string?>(l.Tiles, y * W + x, null);
                string? Look(int i) => Decor.DecorOf(CodeAt(l.Decor, i))?.Kind;
                for (var i = 0; i < l.Tiles.Count; i++)
                {
                    if (l.Tiles[i] != TileKinds.Window) continue;
                    var x = i % W;
                    var y = i / W;
                    foreach (var d in Grid.Dirs4)
                    {
                        var j = (y + d.Y) * W + x + d.X;
                        var tile = Js.At<string?>(l.Tiles, j, null);
                        if (CodeAt(l.Ground, j) != 2 || (tile != TileKinds.High && tile != TileKinds.Low)) continue;
                        Is(DecorKinds.WindowBarricade, Look(j), $"city/{seed}: {Look(j)} inside the window at {x},{y}");
                        barricades++;
                    }
                }
                // Each house room on the ground: all its windows barricaded, or none.
                foreach (var room in (l.Homes ?? new List<HomeSite>()).SelectMany((h) => h.Rooms).Where((r) => r.Z == 0))
                {
                    var states = new HashSet<bool>();
                    for (var y = room.Y; y < room.Y + room.H; y++)
                    {
                        for (var x = room.X; x < room.X + room.W; x++)
                        {
                            foreach (var d in Grid.Dirs4)
                            {
                                if (At(x + d.X, y + d.Y) != TileKinds.Window) continue;
                                states.Add(Look(y * W + x) == DecorKinds.WindowBarricade);
                            }
                        }
                    }
                    Assert.True(states.Count <= 1, $"city/{seed}: room {room.X},{room.Y} barricaded only in part");
                }
            }
            Assert.True(barricades > 0);
        }

        [Fact(DisplayName = "mission timers › keep the values tuned on 36-tile maps and add turns on bigger ones, more when the objective comes back")]
        public void KeepTheValuesTunedOn36TileMaps()
        {
            var turns = Setup.MissionTurnsByKind;
            Assert.Equal(turns[MissionKinds.Hack], Setup.MissionTurns(MissionKinds.Hack, 36));
            Assert.Equal(turns[MissionKinds.Rescue], Setup.MissionTurns(MissionKinds.Rescue, 30));
            Assert.Null(Setup.MissionTurns(MissionKinds.Elimination, 48));
            Assert.Equal(2, Setup.MissionTurns(MissionKinds.Hack, 48)!.Value - turns[MissionKinds.Hack]);
            Assert.Equal(2, Setup.MissionTurns(MissionKinds.Sabotage, 48)!.Value - turns[MissionKinds.Sabotage]);
            Assert.Equal(3, Setup.MissionTurns(MissionKinds.Recovery, 48)!.Value - turns[MissionKinds.Recovery]);
            Assert.Equal(3, Setup.MissionTurns(MissionKinds.Rescue, 48)!.Value - turns[MissionKinds.Rescue]);
            // A generated map's, by the walk its mission takes (docs/MAPAS.md §6): a 48 map's today at its median walk, no fewer
            // than on a 36 map.
            Assert.Equal(Setup.MissionTurns(MissionKinds.Recovery, Mapgen.GenSize), Setup.RouteTurns(MissionKinds.Recovery, 83));
            Assert.Equal(turns[MissionKinds.Hack], Setup.RouteTurns(MissionKinds.Hack, 40));
            Assert.True(Setup.RouteTurns(MissionKinds.Sabotage, 120)!.Value > Setup.MissionTurns(MissionKinds.Sabotage, Mapgen.GenSize)!.Value);
            var s = Setup.CreateMission(Generated(Biomes.City, 5, new List<int> { 0 }, 5, MissionKinds.Recovery));
            var route = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3 }, Seed = 5 }).Route!.Value;
            Assert.Equal(Setup.RouteTurns(MissionKinds.Recovery, route), s.Mission.TurnsLeft);
            var plaza = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0 }, Rng = new Rng(5), Kind = MissionKinds.Recovery });
            Assert.Equal(turns[MissionKinds.Recovery], plaza.Mission.TurnsLeft);
        }

        // ------------------------------------------------------------ doors (playtest 23/10 #11)

        [Fact(DisplayName = "doors (playtest 23/10 #11) › no leaf of a double door on a building corner, where the wall that turns meets it")]
        public void NoLeafOfADoubleDoorOnABuildingCorner()
        {
            foreach (var biome in BiomeList)
            {
                for (var seed = 1; seed <= 12; seed++)
                {
                    var m = Setup.CreateMission(Generated(biome, seed, new List<int> { 0 }, seed, MissionKinds.Elimination));
                    string K(int x, int y) => m.Tiles[y * m.Width + x];
                    bool Door(int x, int y) => K(x, y) == TileKinds.Door;
                    bool Solid(int x, int y) => K(x, y) == TileKinds.Wall || K(x, y) == TileKinds.Window || K(x, y) == TileKinds.WindowBroken;
                    for (var y = 1; y < m.Height - 1; y++)
                    {
                        for (var x = 1; x < m.Width - 1; x++)
                        {
                            if (!Door(x, y)) continue;
                            Assert.False((Door(x - 1, y) || Door(x + 1, y)) && (Solid(x, y - 1) || Solid(x, y + 1)), $"{biome}/{seed} {x},{y}");
                            Assert.False((Door(x, y - 1) || Door(x, y + 1)) && (Solid(x - 1, y) || Solid(x + 1, y)), $"{biome}/{seed} {x},{y}");
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------ the squad at the start (Arturo, 23/10)

        [Fact(DisplayName = "the squad at the start (Arturo, 23/10) › looks into the map, towards what the mission is about or its middle, never at the edge behind it")]
        public void LooksIntoTheMap()
        {
            foreach (var kind in new[] { MissionKinds.Elimination, MissionKinds.Recovery, MissionKinds.Hack })
            {
                for (var seed = 1; seed <= 6; seed++)
                {
                    var s = Setup.CreateMission(Generated(Biomes.City, seed, new List<int> { 0 }, seed, kind));
                    foreach (var u in s.Units.Where((x) => x.Team == Teams.Xcom))
                    {
                        Assert.True(u.Facing != null, $"{kind}/{seed} {u.Id}");
                        // A step that way gets nearer the middle of the map.
                        var mid = (X: (s.Width - 1) / 2.0, Y: (s.Height - 1) / 2.0);
                        var step = (X: u.Pos.X + Math.Cos(u.Facing!.Value) * 3, Y: u.Pos.Y + Math.Sin(u.Facing!.Value) * 3);
                        var after = Js.Hypot(step.X - mid.X, step.Y - mid.Y);
                        var before = Js.Hypot(u.Pos.X - mid.X, u.Pos.Y - mid.Y);
                        Assert.True(after < before + 1, $"{kind}/{seed} {u.Id}: {after} from the middle, {before} before");
                    }
                }
            }
        }
    }
}
