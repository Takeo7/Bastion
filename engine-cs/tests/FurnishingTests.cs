using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using Xunit.Sdk;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/furnishing.test.ts, test by test (Furnishing, Mapgen.GenerateLayout, Setup.CreateMission and
    /// base.ts's FreshBase, BaseLayout, BastionLot, BastionYard and PlacePiece in `Base`). Nothing left out.
    /// </summary>
    public class FurnishingTests
    {
        // Arturo, 19/10: «que ocupen». What is drawn standing (an armchair, a lamp,
        // a plant, a café's table, a hydrant, a chair over) stands on a tile nobody
        // walks through or stops on; what lies flat stays walkable. And never where
        // it would close a way.

        private static readonly (string Name, Func<int, GenOptions> Opts)[] Maps =
        {
            ("city", (seed) => new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3, 3, 3 }, Seed = seed }),
            ("city hack", (seed) => new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Hack, PodSizes = new List<int> { 3, 3 }, Seed = seed }),
            ("clinic", (seed) => new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3, 3 }, Seed = seed, Medical = MedicalSiteKinds.Clinic }),
            ("hospital", (seed) => new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Rescue, PodSizes = new List<int> { 3, 3 }, Seed = seed, Medical = MedicalSiteKinds.Hospital }),
            ("wilds", (seed) => new GenOptions { Biome = Biomes.Wilds, Kind = MissionKinds.Scavenge, PodSizes = new List<int> { 3, 3 }, Seed = seed }),
            ("roadside", (seed) => new GenOptions { Biome = Biomes.Wilds, Kind = MissionKinds.Scavenge, PodSizes = new List<int> { 3, 3 }, Seed = seed, Site = Sites.Roadside }),
            ("facility", (seed) => new GenOptions { Biome = Biomes.Facility, Kind = MissionKinds.Sabotage, PodSizes = new List<int> { 3, 3 }, Seed = seed }),
        };
        private static readonly int[] Seeds = { 11, 2027, 40961, 77003, 123457, 999331 };
        private static readonly HashSet<string?> Ways = new HashSet<string?> { TileKinds.Door, TileKinds.DoorOpen, TileKinds.Ladder, TileKinds.Window, TileKinds.WindowBroken };

        /// <summary>One level of a layout: its tiles and decor, z.</summary>
        private sealed class Layer
        {
            public int Z;
            public IReadOnlyList<string?> Tiles = new List<string?>();
            public IReadOnlyList<int>? Decor;
        }

        /// <summary>Each level of a layout: its tiles and decor, z.</summary>
        private static List<Layer> LayersOf(MissionLayout l)
        {
            var layers = new List<Layer> { new Layer { Z = 0, Tiles = l.Tiles, Decor = l.Decor ?? new List<int>() } };
            layers.AddRange((l.Levels ?? new List<Level>()).Select((v, k) => new Layer { Z = k + 1, Tiles = v.Tiles, Decor = v.Decor ?? new List<int>() }));
            return layers;
        }

        /// <summary>`list[i]`: undefined (null) out of it.</summary>
        private static string? At(IReadOnlyList<string?>? list, int i)
        {
            return list != null && i >= 0 && i < list.Count ? list[i] : null;
        }

        /// <summary>`decor?.[i]`: undefined (null) out of it.</summary>
        private static int? DecorAt(IReadOnlyList<int>? decor, int i)
        {
            return decor != null && i >= 0 && i < decor.Count ? decor[i] : (int?)null;
        }

        private static string? KindOf(IReadOnlyList<int>? decor, int i)
        {
            return Decor.DecorOf(DecorAt(decor, i))?.Kind;
        }

        private static bool OwnTile(string? kind)
        {
            return Furnishing.FurnitureOfItsOwn.Contains(kind ?? DecorKinds.Bus);
        }

        private static MissionOptions Opts(MapSource map, int seed, string? kind = null)
        {
            return new MissionOptions { Map = map, Slots = new List<int> { 0 }, Rng = new Rng(seed), Kind = kind };
        }

        /// <summary>toEqual on anything: the same JSON, null and missing alike.</summary>
        private static void Same(object? expected, object? actual)
        {
            var diff = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "");
            if (diff != null) throw new XunitException(diff);
        }

        // ------------------------------------------------------------ what stands up takes its tile (19/10)

        [Fact(DisplayName = "what stands up takes its tile (19/10) › stands on cover of its own, never by a door, a ladder, the stairs or a window, nor where anybody starts")]
        public void StandsOnCoverOfItsOwn()
        {
            var seen = 0;
            foreach (var (name, opts) in Maps)
            {
                foreach (var seed in Seeds)
                {
                    var l = Mapgen.GenerateLayout(opts(seed));
                    var label = $"{name}/{seed}";
                    var taken = new HashSet<string>(l.Spawns.Concat(l.Pods.SelectMany((p) => p.Tiles)).Concat(new[] { l.Item, l.ObjectiveSpot }).Where((p) => p != null).Select((p) => $"{p!.X},{p.Y}"));
                    foreach (var layer in LayersOf(l))
                    {
                        for (var i = 0; i < layer.Tiles.Count; i++)
                        {
                            var kind = layer.Tiles[i];
                            var look = Decor.DecorOf(DecorAt(layer.Decor, i));
                            if (look == null || !Furnishing.FurnitureOfItsOwn.Contains(look.Kind)) continue;
                            seen++;
                            var p = new Vec2 { X = i % l.Width, Y = i / l.Width };
                            var want = look.Kind == DecorKinds.DrinksFridge ? TileKinds.High : TileKinds.Low;
                            if (kind != want) throw new XunitException($"{label}: {look.Kind} at {p.X},{p.Y},{layer.Z}: expected {want}, got {kind}");
                            foreach (var d in Grid.Dirs4) Assert.False(Ways.Contains(At(layer.Tiles, (p.Y + d.Y) * l.Width + p.X + d.X)), $"{label}: {look.Kind} by a way at {p.X},{p.Y},{layer.Z}");
                            if (layer.Z == 0) Assert.False(taken.Contains($"{p.X},{p.Y}"), $"{label}: on somebody at {p.X},{p.Y}");
                        }
                    }
                    // And every place one reached without it (its tiles floor again) is reached still, from where the squad starts.
                    FurnishMap Map(bool strip)
                    {
                        bool Own(int? d) => strip && OwnTile(Decor.DecorOf(d)?.Kind);
                        return new FurnishMap
                        {
                            Width = l.Width,
                            Height = l.Height,
                            Tiles = l.Tiles.Select((k, i) => Own(DecorAt(l.Decor, i)) ? TileKinds.Floor : k).ToList(),
                            Elev = l.Elev,
                            Decor = l.Decor ?? new List<int>(),
                            Levels = (l.Levels ?? new List<Level>()).Select((v) => new Level { Tiles = v.Tiles.Select((k, i) => Own(DecorAt(v.Decor, i)) ? TileKinds.Floor : k).ToList(), Decor = v.Decor }).ToList(),
                        };
                    }
                    var before = Furnishing.ReachableFrom(Map(true), new Pos { X = l.Spawns[0].X, Y = l.Spawns[0].Y, Z = 0 });
                    var after = Furnishing.ReachableFrom(Map(false), new Pos { X = l.Spawns[0].X, Y = l.Spawns[0].Y, Z = 0 });
                    var area = l.Width * l.Height;
                    var layers = LayersOf(l);
                    foreach (var n in before)
                    {
                        var z = n / area;
                        var i = n - z * area;
                        var layer = layers[z];
                        if (OwnTile(KindOf(layer.Decor, i))) continue;
                        Assert.True(after.Has(n), $"{label}: {i % l.Width},{i / l.Width},{z} cut off");
                    }
                }
            }
            Assert.True(seen > 200, $"{seen}");
        }

        [Fact(DisplayName = "what stands up takes its tile (19/10) › keepsWays: an object goes only where what is round it stays joined round it")]
        public void KeepsWaysAnObjectGoesOnlyWhereWhatIsRoundItStaysJoined()
        {
            // A corridor a tile wide: never in it; a room: anywhere but where it would cut it in two.
            List<string?> Tiles(string[] rows) => string.Concat(rows).Select((c) => (string?)(c == '#' ? TileKinds.Wall : c == 'h' ? TileKinds.Low : TileKinds.Floor)).ToList();
            var corridor = Tiles(new[] { "#####", "#...#", "#####" });
            Assert.False(Furnishing.KeepsWays(corridor, null, 5, 3, new Vec2 { X = 2, Y = 1 }));
            Assert.True(Furnishing.KeepsWays(corridor, null, 5, 3, new Vec2 { X = 1, Y = 1 }));
            var room = Tiles(new[] { "#####", "#...#", "#...#", "#...#", "#####" });
            Assert.True(Furnishing.KeepsWays(room, null, 5, 5, new Vec2 { X = 2, Y = 2 }));
            var half = Tiles(new[] { "#####", "#.h.#", "#...#", "#.h.#", "#####" });
            Assert.False(Furnishing.KeepsWays(half, null, 5, 5, new Vec2 { X = 2, Y = 2 }));
            Assert.True(Furnishing.KeepsWays(half, null, 5, 5, new Vec2 { X = 1, Y = 1 }));
        }

        [Fact(DisplayName = "what stands up takes its tile (19/10) › a house keeps its rooms whole and most of their floor, and its furniture is there")]
        public void AHouseKeepsItsRoomsWhole()
        {
            var kinds = new HashSet<string>();
            foreach (var seed in Seeds)
            {
                var s = Setup.CreateMission(Opts(MapSource.Generated(Biomes.City, seed), seed, MissionKinds.Recovery));
                foreach (var home in s.Homes ?? new List<HomeSite>())
                {
                    foreach (var room in home.Rooms)
                    {
                        IReadOnlyList<string?> tiles = room.Z != 0 ? s.Levels![room.Z - 1].Tiles : (IReadOnlyList<string?>)s.Tiles;
                        IReadOnlyList<int>? decor = room.Z != 0 ? s.Levels![room.Z - 1].Decor : s.Decor ?? new List<int>();
                        var cells = new List<Vec2>();
                        for (var y = room.Y; y < room.Y + room.H; y++) for (var x = room.X; x < room.X + room.W; x++) cells.Add(new Vec2 { X = x, Y = y });
                        int Index(Vec2 p) => p.Y * s.Width + p.X;
                        var own = cells.Count((p) =>
                        {
                            var kind = KindOf(decor, Index(p));
                            if (kind != null) kinds.Add(kind);
                            return kind != null && Furnishing.FurnitureOfItsOwn.Contains(kind) && kind != DecorKinds.Strewn;
                        });
                        var blocked = cells.Count((p) => new[] { TileKinds.Low, TileKinds.High }.Contains(At(tiles, Index(p)) ?? ""));
                        var strewn = cells.Count((p) => KindOf(decor, Index(p)) == DecorKinds.Strewn);
                        if (own != 0) Assert.True(blocked - strewn <= (int)Math.Floor(room.W * room.H * 0.4), $"{seed}: room {room.X},{room.Y},{room.Z}");
                        // One room still: all its floor joined, round the furniture.
                        var ids = new HashSet<int?>(cells.Where((p) => Grid.IsWalkable(At(tiles, Index(p))) && At(tiles, Index(p)) != TileKinds.Door && At(tiles, Index(p)) != TileKinds.DoorOpen).Select((p) => Looting.RoomAt(s, new Pos { X = p.X, Y = p.Y, Z = room.Z })?.Id));
                        Assert.True(ids.Count <= 1, $"{seed}: room {room.X},{room.Y},{room.Z} split");
                    }
                }
            }
            foreach (var k in new[] { DecorKinds.Armchair, DecorKinds.FloorLamp, DecorKinds.PottedPlant, DecorKinds.Nightstand, DecorKinds.DiningChair, DecorKinds.TowelRack }) Assert.True(kinds.Contains(k), k);
        }

        [Fact(DisplayName = "what stands up takes its tile (19/10) › a chair over, a fallen drip or bags left packed stand on a tile of their own; the dead lie on the floor")]
        public void AChairOverAFallenDripOrBagsLeftPacked()
        {
            var upright = new HashSet<string> { MedicalDetailKinds.OverturnedChair, MedicalDetailKinds.FallenIV, MedicalDetailKinds.Luggage };
            var count = 0;
            foreach (var medical in new string?[] { MedicalSiteKinds.Clinic, MedicalSiteKinds.Ward, MedicalSiteKinds.Hospital, null })
            {
                foreach (var seed in Seeds)
                {
                    var s = Setup.CreateMission(Opts(MapSource.Generated(Biomes.City, seed, medical: medical), seed, medical == MedicalSiteKinds.Hospital ? MissionKinds.Rescue : MissionKinds.Recovery));
                    var sites = (s.Medical ?? new List<MedicalSite>()).Select((m) => m.Dressing).Concat((s.Homes ?? new List<HomeSite>()).Select((h) => h.Dressing));
                    foreach (var dressing in sites)
                    {
                        foreach (var d in dressing ?? new List<MedicalDetail>())
                        {
                            IReadOnlyList<string?> tiles = d.Z != 0 ? s.Levels![d.Z - 1].Tiles : (IReadOnlyList<string?>)s.Tiles;
                            IReadOnlyList<int>? decor = d.Z != 0 ? s.Levels![d.Z - 1].Decor : s.Decor ?? new List<int>();
                            var i = d.Y * s.Width + d.X;
                            var look = KindOf(decor, i);
                            if (upright.Contains(d.Kind))
                            {
                                count++;
                                Assert.True(tiles[i] == TileKinds.Low, $"{seed}: {d.Kind}: expected low, got {tiles[i]}");
                                // On a bedside table (the clinics' bags), or on its own tile.
                                Assert.True(look == DecorKinds.Strewn || look == DecorKinds.BedsideTable, $"{seed}: {d.Kind} on {look}");
                            }
                            if (d.Kind == MedicalDetailKinds.CorpseCivilian || d.Kind == MedicalDetailKinds.CorpseStaff || d.Kind == MedicalDetailKinds.CoveredCorpse) Assert.Equal(TileKinds.Floor, tiles[i]);
                        }
                    }
                }
            }
            Assert.True(count > 10, $"{count}");
        }

        [Fact(DisplayName = "what stands up takes its tile (19/10) › a hydrant or a mailbox stands by the kerb, on the pavement, facing the street")]
        public void AHydrantOrAMailboxStandsByTheKerb()
        {
            var count = 0;
            foreach (var seed in Seeds)
            {
                var l = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Elimination, PodSizes = new List<int> { 3, 3 }, Seed = seed });
                for (var i = 0; i < l.Tiles.Count; i++)
                {
                    var kind = l.Tiles[i];
                    var look = Decor.DecorOf(DecorAt(l.Decor, i));
                    if (look?.Kind != DecorKinds.Hydrant && look?.Kind != DecorKinds.Mailbox) continue;
                    count++;
                    var d = Grid.Dirs4[look!.Facing];
                    var x = i % l.Width;
                    var y = i / l.Width;
                    Assert.Equal(TileKinds.Low, kind);
                    Assert.Equal(0, l.Ground![i]);
                    Assert.Equal(1, l.Ground[(y + d.Y) * l.Width + x + d.X]);
                }
            }
            Assert.True(count > 5, $"{count}");
        }

        [Fact(DisplayName = "what stands up takes its tile (19/10) › the same seed furnishes the same; a mission saved before keeps its tiles")]
        public void TheSameSeedFurnishesTheSame()
        {
            var a = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3, 3 }, Seed = 2027 });
            var b = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Recovery, PodSizes = new List<int> { 3, 3 }, Seed = 2027 });
            Same(a.Tiles, b.Tiles);
            Same(a.Decor, b.Decor);
            Same(a.Levels, b.Levels);
        }

        // ------------------------------------------------------------ La Lata leaves by an open way (19/10)

        [Fact(DisplayName = "La Lata leaves by an open way (19/10) › nothing stands between the bus and the edge it drives off by")]
        public void NothingStandsBetweenTheBusAndTheEdge()
        {
            var buses = 0;
            foreach (var (name, opts) in Maps)
            {
                foreach (var seed in Seeds)
                {
                    var o = opts(seed);
                    o.Kind = MissionKinds.Recovery;
                    var l = Mapgen.GenerateLayout(o);
                    var bus = l.EvacBus;
                    if (bus == null) continue;
                    buses++;
                    var d = Grid.Dirs4[bus.Heading];
                    foreach (var t in bus.Tiles)
                    {
                        for (var p = new Vec2 { X = t.X + d.X, Y = t.Y + d.Y }; p.X >= 0 && p.Y >= 0 && p.X < l.Width && p.Y < l.Height; p = new Vec2 { X = p.X + d.X, Y = p.Y + d.Y })
                        {
                            var i = p.Y * l.Width + p.X;
                            if (KindOf(l.Decor, i) == DecorKinds.Bus) continue;
                            Assert.False(new[] { TileKinds.Low, TileKinds.High }.Contains(l.Tiles[i]), $"{name}/{seed}: {l.Tiles[i]} at {p.X},{p.Y}");
                        }
                    }
                }
            }
            Assert.True(buses > 20, $"{buses}");
        }

        // ------------------------------------------------------------ the Bastion's yard (19/10)

        [Fact(DisplayName = "the Bastion's yard (19/10) › what is drawn in its yard is low cover nobody starts on, and no free tile is shut in among it")]
        public void WhatIsDrawnInItsYardIsLowCover()
        {
            foreach (var level in new[] { 1, 2, 3, 4, 5, 6, 7, 8 })
            {
                var @base = Base.FreshBase(new Rng(level));
                @base.Level = level;
                var layout = Base.BaseLayout(@base);
                var yard = Base.BastionYard(@base);
                Assert.True(yard.Count > 0, $"level {level}");
                foreach (var p in yard)
                {
                    Assert.True(layout.Tiles[p.Y * @base.Width + p.X] == TileKinds.Low, $"level {level}: {p.X},{p.Y}: {layout.Tiles[p.Y * @base.Width + p.X]}");
                    Assert.DoesNotContain(layout.Spawns, (q) => q.X == p.X && q.Y == p.Y);
                }
                Assert.Equal(24, layout.Spawns.Count);
                // Every free tile of the lot joins the clearing.
                var lot = Base.BastionLot(@base)!;
                int Index(int x, int y) => y * @base.Width + x;
                var open = new HashSet<int> { Index(lot.X - 1, lot.Y - 1) };
                var queue = new List<Vec2> { new Vec2 { X = lot.X - 1, Y = lot.Y - 1 } };
                while (queue.Count > 0)
                {
                    var p = queue[queue.Count - 1];
                    queue.RemoveAt(queue.Count - 1);
                    foreach (var d in Grid.Dirs4)
                    {
                        var q = new Vec2 { X = p.X + d.X, Y = p.Y + d.Y };
                        if (q.X < lot.X - 2 || q.Y < lot.Y - 2 || q.X > lot.X + lot.W + 1 || q.Y > lot.Y + lot.H + 1 || open.Contains(Index(q.X, q.Y)) || layout.Tiles[Index(q.X, q.Y)] != TileKinds.Floor) continue;
                        open.Add(Index(q.X, q.Y));
                        queue.Add(q);
                    }
                }
                for (var y = lot.Y; y < lot.Y + lot.H; y++) for (var x = lot.X; x < lot.X + lot.W; x++) if (layout.Tiles[Index(x, y)] == TileKinds.Floor) Assert.True(open.Contains(Index(x, y)), $"level {level}: {x},{y} shut in");
            }
        }

        [Fact(DisplayName = "the Bastion's yard (19/10) › nobody is posted on it, and a siege puts nobody there")]
        public void NobodyIsPostedOnIt()
        {
            var @base = Base.FreshBase(new Rng(3));
            @base.Level = 2;
            Base.PlacePiece(@base, PieceIds.Palisade, 3, 3, 0);
            var s = Setup.CreateMission(Opts(MapSource.OfBase(@base), 3));
            var yard = new HashSet<int>(Base.BastionYard(@base).Select((p) => p.Y * @base.Width + p.X));
            foreach (var u in s.Units) Assert.DoesNotContain(u.Pos.Y * s.Width + u.Pos.X, yard);
        }
    }
}
