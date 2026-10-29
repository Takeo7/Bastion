using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/levels.test.ts, the tests LevelsStep1Tests leaves out: those that need rules.ts, ai.ts,
    /// aim.ts, abilities.ts, events.ts or loot.ts, and those on «la torre» and the generator's towns (setup.ts
    /// createMission, maps.ts). «who sees whom» is here whole, with its lines on previewShot. Not ported: «places with a
    /// level › commands may say the level or not (then the ground); four levels at most», which needs protocol.ts's
    /// schemas (commandSchema, campaignCommandSchema).
    /// </summary>
    // The time budgets are measured alone: beside 400 other tests running at once they miss their bounds, as in TypeScript.
    [Collection(TimeBudgets.Name)]
    public class LevelsStep2Tests
    {
        // Phase 6, steps 1 and 2 (docs/FASE6-PLAN.md): places with a level, and moving up and down.

        /// <summary>
        /// A two-storey building with a roof you can walk on (x 1–7, y 1–5): stairs
        /// from the ground at (2,2) to the first floor, and from (6,4) up to the roof.
        /// The first floor has a window over the ground floor's door. A lean-to with a
        /// pitched roof beside it (x 8–9, y 1–2), and a shed with a flat roof and a
        /// ladder up its side (x 10–12, y 6–8).
        /// </summary>
        private static GameState Building()
        {
            var s = StateFrom(new[]
            {
                "..............",
                ".#######......",
                ".#S....#......",
                ".#.....+......",
                ".#.....#......",
                ".#######......",
                "..........###.",
                ".........L#.#.",
                "..........###.",
            });
            return WithLevels(s, new[]
            {
                new[]
                {
                    "              ",
                    " #######RR    ",
                    " # ....#RR    ",
                    " #.....=      ",
                    " #....S#      ",
                    " #######      ",
                    "          ... ",
                    "          ... ",
                    "          ... ",
                },
                new[]
                {
                    "              ",
                    " .......      ",
                    " .......      ",
                    " .......      ",
                    " ..... .      ",
                    " .......      ",
                    "              ",
                    "              ",
                    "              ",
                },
            });
        }

        private static Pos At(int x, int y, int z = 0)
        {
            return P(x, y, z);
        }

        /// <summary>toEqual on JSON of two things of the state (levels, tiles).</summary>
        private static void SameJson<T>(T expected, T actual)
        {
            var a = JsonSerializer.SerializeToNode(expected, Options);
            var b = JsonSerializer.SerializeToNode(actual, Options);
            Assert.True(JsonNode.DeepEquals(a, b), $"expected {a?.ToJsonString()}, got {b?.ToJsonString()}");
        }

        // ------------------------------------------------------------ places with a level

        [Fact(DisplayName = "places with a level › a mission saved before storeys gets every place on the ground, and plays on")]
        public void AMissionSavedBeforeStoreysGetsEveryPlaceOnTheGround()
        {
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0 }, Rng = new Rng(2), Kind = MissionKinds.Recovery });
            s.Lights = new List<Light> { new Light { Id = "l1", Tile = At(3, 3), Radius = 3 } };
            s.Smoke = new List<SmokeCloud> { new SmokeCloud { Pos = At(4, 4), Radius = 2, Turns = 2 } };
            s.Units[s.Units.Count - 1].Heard = At(5, 5);
            // As it was saved: no level anywhere.
            var saved = JsonSerializer.SerializeToNode(s, Options);
            void NoZ(JsonNode? n)
            {
                if (n is JsonObject o)
                {
                    o.Remove("z");
                    foreach (var kv in o) NoZ(kv.Value);
                }
                else if (n is JsonArray a) foreach (var x in a) NoZ(x);
            }
            NoZ(saved);
            var json = saved!.ToJsonString();
            Assert.DoesNotContain("\"z\"", json);
            var old = JsonSerializer.Deserialize<GameState>(json, Options)!;
            Setup.NormalizeMission(old);
            Assert.Null(Step2Json.StateDiff(s, old));
            Assert.Null(Step2Json.StateDiff(old, Setup.NormalizeMission(Events.CloneState(old))));
            var soldier = old.Units.Find((u) => u.Team == Teams.Xcom)!;
            var r = Exec(old, new MoveCommand { Unit = soldier.Id, To = new Pos { X = soldier.Pos.X, Y = soldier.Pos.Y - 2 } });
            Assert.True(r.Ok);
            var moved = r.Ok ? r.Events!.OfType<MovedEvent>().FirstOrDefault() : null;
            Assert.True(moved?.Path.All((p) => p.Z == 0));
        }

        [Fact(DisplayName = "places with a level › a tile that changes on a storey changes there, on the host and on the clients; searching marks the place")]
        public void ATileThatChangesOnAStoreyChangesThere()
        {
            var s = Building();
            var client = Events.CloneState(s);
            var events = new List<GameEvent>
            {
                new TileChangedEvent { Pos = At(7, 3, 1), Kind = TileKinds.WindowBroken },
                new SearchedEvent { Unit = "s1", Tile = At(4, 4, 1), Loot = null },
            };
            AddUnit(s, "s1", TemplateIds.Assault, 4, 3, 1);
            AddUnit(client, "s1", TemplateIds.Assault, 4, 3, 1);
            foreach (var e in events) Events.ApplyEvent(client, e);
            Assert.Equal(TileKinds.WindowBroken, Grid.KindAt(client, At(7, 3, 1)));
            Assert.Equal(TileKinds.Door, Grid.KindAt(client, At(7, 3)));
            Assert.Equal(new[] { Grid.NodeOf(s, At(4, 4, 1)) }, client.Searched!);
        }

        // ------------------------------------------------------------ moving up and down

        [Fact(DisplayName = "moving up and down › a move upstairs: the command, the events and the doors on the way say the level, and clients replay it")]
        public void AMoveUpstairs()
        {
            var s = Building();
            s.Levels![0].Tiles[Grid.TileIndex(s, 4, 3)] = TileKinds.Door;
            s.Levels![0].Tiles[Grid.TileIndex(s, 4, 2)] = TileKinds.Wall;
            s.Levels![0].Tiles[Grid.TileIndex(s, 4, 4)] = TileKinds.Wall;
            var u = AddUnit(s, "s1", TemplateIds.Assault, 3, 3);
            u.Mods = Content.ZeroMods;
            u.Mods.Mobility = 20;
            AddUnit(s, "a1", TemplateIds.Zombie, 12, 0);
            var client = Events.CloneState(s);
            var to = At(6, 3, 1);
            var r = Exec(s, new MoveCommand { Unit = "s1", To = to });
            Assert.True(r.Ok);
            var events = r.Ok ? r.Events! : new List<GameEvent>();
            foreach (var e in events) Events.ApplyEvent(client, e);
            SamePlace(to, u.Pos);
            SamePlace(to, client.Units.Find((x) => x.Id == "s1")!.Pos);
            var opened = events.OfType<TileChangedEvent>().FirstOrDefault();
            Assert.NotNull(opened);
            SamePlace(At(4, 3, 1), opened!.Pos);
            Assert.Equal(TileKinds.DoorOpen, opened.Kind);
            Assert.Null(opened.Decor);
            Assert.Null(opened.Facing);
            var noise = events.OfType<NoiseEvent>().FirstOrDefault();
            Assert.NotNull(noise);
            SamePlace(At(4, 3, 1), noise!.Tile);
            SameJson(s.Levels, client.Levels);
            Assert.Equal(s.Tiles, client.Tiles);
            Assert.Equal(TileKinds.Floor, Grid.KindAt(s, At(4, 3)));
            // A command without a level goes to the ground floor.
            var down = Exec(s, new MoveCommand { Unit = "s1", To = new Pos { X = 5, Y = 2 } });
            Assert.True(down.Ok);
            SamePlace(At(5, 2), u.Pos);
        }

        // ------------------------------------------------------------ la torre

        [Fact(DisplayName = "la torre › a skirmish map with storeys: the Cuervos start on the first floor, everything is in reach, the stairs face up")]
        public void ASkirmishMapWithStoreys()
        {
            var map = Maps.ParseMap(Maps.All["torre"]);
            Assert.Equal(3, map.Levels!.Count);
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("torre"), Slots = new List<int> { 0, 1 }, Rng = new Rng(1) });
            Assert.Equal(3, s.Levels!.Count);
            // How tall each building stands, for the client: the tower three storeys, the shed and the house one.
            Assert.Equal(new[] { 3, 1, 1, 0 }, new[] { s.Storeys![Grid.TileIndex(s, 12, 7)], s.Storeys![Grid.TileIndex(s, 3, 14)], s.Storeys![Grid.TileIndex(s, 19, 15)], s.Storeys![0] });
            var cuervos = s.Units.Where((u) => u.Team == Teams.Alien && u.Template != TemplateIds.Zombie).ToList();
            Assert.Equal(new[] { 1, 1 }, cuervos.Select((u) => u.Pos.Z));
            Assert.True(s.Units.Where((u) => u.Template == TemplateIds.Zombie).All((u) => u.Pos.Z == 0));
            // Stairs: a ladder with its decor, looking toward the floor it climbs to.
            Assert.Equal(TileKinds.Ladder, Grid.KindAt(s, At(10, 5)));
            void Stairs(int facing, int? code)
            {
                var look = Decor.DecorOf(code);
                Assert.NotNull(look);
                Assert.Equal((DecorKinds.Stairs, facing), (look!.Kind, look.Facing));
            }
            Stairs(1, s.Decor![Grid.TileIndex(s, 10, 5)]);
            Stairs(0, s.Levels![0].Decor![Grid.TileIndex(s, 15, 9)]);
            Stairs(0, s.Levels![1].Decor![Grid.TileIndex(s, 10, 9)]);
            // From the squad's spawn: the roof of the tower, the shed's roof by its ladder.
            var scout = Events.CloneUnit(s.Units.Find((u) => u.Team == Teams.Xcom)!);
            scout.Id = "scout";
            s.Units = new List<Unit> { scout };
            var reach = Paths.ComputeReach(s, scout, 10_000);
            foreach (var p in new[] { At(12, 7, 3), At(15, 6, 1), At(3, 14, 1) }) Assert.True(reach.Cost.Has(Grid.NodeOf(s, p)), $"{p.X},{p.Y},{p.Z}");
            // The pitched roof is no place to stand.
            Assert.False(reach.Cost.Has(Grid.NodeOf(s, At(19, 15, 1))));
        }

        // ------------------------------------------------------------ step 3

        /// <summary>
        /// lineClear as it was before storeys had floors (phase 5, with `storeys`), to
        /// hold maps without them against; with the one rule added since, on purpose:
        /// through two windows at most, counted where the line goes through them
        /// (MAX_WINDOWS_SEEN_THROUGH, playtest 2026-10-14), and only those beside
        /// one end of the line (WINDOW_SIGHT_REACH, Ana 23/10 #21).
        /// </summary>
        private static bool LineClearPhase5(GameState s, Vec2 a, Vec2 b, double? haOr = null, double? hbOr = null)
        {
            var ha = haOr ?? Grid.ElevAt(s, a.X, a.Y) + Grid.EyeHeight;
            var hb = hbOr ?? Grid.ElevAt(s, b.X, b.Y) + Grid.BodyHeight;
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var nx = Math.Abs(dx);
            var ny = Math.Abs(dy);
            var sx = Math.Sign(dx);
            var sy = Math.Sign(dy);
            var len2 = dx * dx + dy * dy;
            if (len2 == 0) len2 = 1;
            double LineAt(int x, int y) => ha + (hb - ha) * Math.Min(1, Math.Max(0, (double)((x - a.X) * dx + (y - a.Y) * dy) / len2));
            bool Blocks(int x, int y) => Grid.BlocksSightAt(s, x, y, LineAt(x, y));
            var side = Grid.RoofSide(s, a.X, a.Y, ha);
            bool ThroughRoof(int x, int y, double h)
            {
                var now = Grid.RoofSide(s, x, y, h);
                var pierced = now != null && side != null && now != side;
                side = now;
                return pierced;
            }
            var cx = a.X;
            var cy = a.Y;
            var ix = 0;
            var iy = 0;
            var windows = 0;
            while (ix < nx || iy < ny)
            {
                var decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
                if (decision == 0)
                {
                    if (Blocks(cx + sx, cy) && Blocks(cx, cy + sy)) return false;
                    cx += sx;
                    cy += sy;
                    ix++;
                    iy++;
                }
                else if (decision < 0)
                {
                    cx += sx;
                    ix++;
                }
                else
                {
                    cy += sy;
                    iy++;
                }
                if (cx == b.X && cy == b.Y) return !ThroughRoof(cx, cy, hb);
                if (Blocks(cx, cy) || ThroughRoof(cx, cy, LineAt(cx, cy))) return false;
                if (Grid.IsWindow(Grid.TileAt(s, cx, cy)) && LineAt(cx, cy) < Grid.ElevAt(s, cx, cy) + 1 && ((Math.Max(Math.Abs(cx - a.X), Math.Abs(cy - a.Y)) > Los.WindowSightReach && Math.Max(Math.Abs(cx - b.X), Math.Abs(cy - b.Y)) > Los.WindowSightReach) || ++windows > Los.MaxWindowsSeenThrough)) return false;
            }
            return true;
        }

        /// <summary>teamVisibility as it was (phase 5), on lineClearPhase5 and the step-outs of then.</summary>
        private static bool[] VisibilityPhase5(GameState s, string team)
        {
            var mask = new bool[s.Width * s.Height];
            List<Vec2> Peeks(Vec2 p)
            {
                var @out = new List<Vec2> { p };
                var elev = Grid.ElevAt(s, p.X, p.Y);
                foreach (var d in Grid.Dirs4)
                {
                    if (Grid.CoverFrom(s, elev, new Vec2 { X = p.X + d.X, Y = p.Y + d.Y }) != 2) continue;
                    foreach (var side in new[] { new Vec2 { X = d.Y, Y = -d.X }, new Vec2 { X = -d.Y, Y = d.X } })
                    {
                        var q = new Vec2 { X = p.X + side.X, Y = p.Y + side.Y };
                        if (Grid.CanStandOn(Grid.TileAt(s, q.X, q.Y)) && Grid.ElevAt(s, q.X, q.Y) == elev && !@out.Any((o) => Grid.SameTile(o, q))) @out.Add(q);
                    }
                }
                return @out;
            }
            foreach (var u in s.Units.Where((v) => v.Alive && v.Team == team && !v.Objective && !v.Captive))
            {
                var range = Los.SightOf(u);
                var origins = Peeks(u.Pos);
                for (var y = 0; y < s.Height; y++)
                {
                    for (var x = 0; x < s.Width; x++)
                    {
                        var i = y * s.Width + x;
                        if (mask[i] || Js.Hypot(u.Pos.X - x, u.Pos.Y - y) > range) continue;
                        var to = new Vec2 { X = x, Y = y };
                        if (origins.Any((o) => LineClearPhase5(s, o, to))) mask[i] = true;
                    }
                }
            }
            return mask;
        }

        // ------------------------------------------------------------ sight through storeys

        [Fact(DisplayName = "sight through storeys › maps without storeys see exactly as before: every line, every fog")]
        public void MapsWithoutStoreysSeeExactlyAsBefore()
        {
            foreach (var seed in new[] { 7, 21 })
            {
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, seed), Slots = new List<int> { 0 }, Rng = new Rng(seed) });
                // As a mission saved before storeys had floors: everyone on the ground.
                s.Levels = null;
                foreach (var u in s.Units) u.Pos = At(u.Pos.X, u.Pos.Y);
                Assert.Contains(s.Storeys!, (n) => n >= 2);
                var rng = new Rng(seed);
                for (var k = 0; k < 4000; k++)
                {
                    var a = At(rng.Int(s.Width), rng.Int(s.Height));
                    var b = At(rng.Int(s.Width), rng.Int(s.Height));
                    var ha = Grid.ElevAt(s, a.X, a.Y) + rng.Pick(new[] { Grid.EyeHeight, 1.7, 2.7 });
                    Assert.Equal(LineClearPhase5(s, a, b, ha), Los.LineClear(s, a, b, ha));
                    Assert.Equal(LineClearPhase5(s, a, b), Los.LineClear(s, a, b));
                }
                Assert.Equal(VisibilityPhase5(s, Teams.Xcom), Los.TeamVisibility(s, Teams.Xcom));
                Assert.Equal(VisibilityPhase5(s, Teams.Alien), Los.TeamVisibility(s, Teams.Alien));
            }
        }

        [Fact(DisplayName = "sight through storeys › with storeys but nothing on them, the ground sees as a map without storeys")]
        public void WithStoreysButNothingOnThem()
        {
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 7), Slots = new List<int> { 0 }, Rng = new Rng(7) });
            s.Storeys = null;
            s.Levels = null;
            var empty = Events.CloneState(s);
            empty.Levels = new List<Level> { new Level { Tiles = Enumerable.Repeat<string?>(null, s.Width * s.Height).ToList() } };
            var rng = new Rng(3);
            for (var k = 0; k < 4000; k++)
            {
                var a = At(rng.Int(s.Width), rng.Int(s.Height));
                var b = At(rng.Int(s.Width), rng.Int(s.Height));
                var ha = Grid.ElevAt(s, a.X, a.Y) + rng.Pick(new[] { Grid.EyeHeight, 1.7, 2.7 });
                Assert.Equal(Los.LineClear(s, a, b, ha), Los.LineClear(empty, a, b, ha));
            }
        }

        [Fact(DisplayName = "sight through storeys › who sees whom: through an upper window both ways, never through a floor")]
        public void WhoSeesWhom()
        {
            // LevelsStep1Tests has this test without its lines on previewShot (aim.ts): here it is whole.
            var s = Building();
            var sniper = AddUnit(s, "a1", TemplateIds.Trooper, 6, 3, 1);
            var street = AddUnit(s, "s1", TemplateIds.Sharpshooter, 12, 3);
            var below = AddUnit(s, "s2", TemplateIds.Assault, 5, 3);
            Assert.True(Los.CanSee(s, sniper, street));
            Assert.True(Los.CanSee(s, street, sniper));
            // A step back from the glass, neither (Ana, 23/10 #21).
            sniper.Pos = At(5, 3, 1);
            Assert.False(Los.CanSee(s, sniper, street));
            Assert.False(Los.CanSee(s, street, sniper));
            sniper.Pos = At(6, 3, 1);
            Assert.False(Los.CanSee(s, sniper, below));
            Assert.False(Los.CanSee(s, below, sniper));
            // From up there the shot has the height.
            var down = Aim.PreviewShot(s, sniper, street);
            Assert.NotNull(down);
            Assert.Contains(down!.Mods, (m) => m.Label == "Ventaja de altura");
            var up = Aim.PreviewShot(s, street, sniper);
            Assert.NotNull(up);
            Assert.DoesNotContain(up!.Mods, (m) => m.Label == "Ventaja de altura");
        }

        // ------------------------------------------------------------ time budgets

        // docs/FASE6-PLAN.md: the fog ≤ 6 ms on a town with its storeys. The best of a few runs, with room for a loaded machine.
        [Fact(DisplayName = "time budgets › the fog of a town with storeys, each team")]
        public void TheFogOfATownWithStoreys()
        {
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 11), Slots = new List<int> { 0 }, Rng = new Rng(1) });
            Assert.True(s.Levels!.SelectMany((l) => l.Tiles).Count((k) => k != null) > 1000);
            foreach (var team in new[] { Teams.Xcom, Teams.Alien })
            {
                var best = double.PositiveInfinity;
                for (var k = 0; k < 6; k++)
                {
                    var t0 = Stopwatch.StartNew();
                    Los.TeamVisibility(s, team);
                    best = Math.Min(best, t0.Elapsed.TotalMilliseconds);
                }
                Assert.True(best < 12, $"{team}: {best} ms");
            }
        }

        // ---------------------------------------------------------------- step 4

        [Fact(DisplayName = "noise and blasts by floor › a storey between is one more tile for a noise to carry (decision 3); maps without storeys hear as before")]
        public void AStoreyBetweenIsOneMoreTileForANoise()
        {
            var s = Building();
            AddUnit(s, "s1", TemplateIds.Assault, 4, 3, 1);
            var near = AddUnit(s, "a1", TemplateIds.Zombie, 8, 3);
            var far = AddUnit(s, "a2", TemplateIds.Zombie, 9, 3);
            var upstairs = AddUnit(s, "a3", TemplateIds.Zombie, 9, 3, 1);
            foreach (var (z, id) in new[] { (near, "pn"), (far, "pf"), (upstairs, "pu") }) Pod(s, id, new[] { z }, false);
            var sim = NewSim(s, 1);
            sim.MakeNoise(At(4, 3, 1), 5);
            var noise = Of<NoiseEvent>(sim.Events).First();
            Assert.Equal(new[] { "a1", "a3" }, noise.Heard.OrderBy((x) => x, StringComparer.Ordinal));
            // The same on a map without storeys: only the distance.
            var flat = StateFrom(new[] { ".........." });
            AddUnit(flat, "s1", TemplateIds.Assault, 4, 0);
            var zombie = AddUnit(flat, "a1", TemplateIds.Zombie, 9, 0);
            Pod(flat, "p", new[] { zombie }, false);
            var flatSim = NewSim(flat, 1);
            flatSim.MakeNoise(At(4, 0), 5);
            Assert.Equal(new[] { "a1" }, Of<NoiseEvent>(flatSim.Events).First().Heard);
        }

        [Fact(DisplayName = "noise and blasts by floor › a floor stops a blast (decision 5): a grenade upstairs hurts that floor only, and breaks what stands there")]
        public void AFloorStopsABlast()
        {
            var s = Building();
            s.Levels![0].Tiles[Grid.TileIndex(s, 5, 2)] = TileKinds.Low;
            s.Tiles[Grid.TileIndex(s, 5, 2)] = TileKinds.Low;
            var thrower = AddUnit(s, "s1", TemplateIds.Grenadier, 12, 3);
            var up = AddUnit(s, "a1", TemplateIds.Trooper, 4, 3, 1);
            var under = AddUnit(s, "a2", TemplateIds.Trooper, 4, 3);
            var roof = AddUnit(s, "a3", TemplateIds.Trooper, 4, 3, 2);
            var center = At(4, 2, 1);
            var area = Abilities.BlastTiles(s, AbilityIds.Grenade, center, thrower);
            Assert.True(area.All((p) => p.Z == 1));
            HasPlace(area, At(4, 3, 1));
            var sim = NewSim(s, 1);
            sim.Explode(thrower, center, AbilityIds.Grenade);
            var hurt = Of<DamagedEvent>(sim.Events).Select((e) => e.Unit).ToList();
            Assert.Contains(up.Id, hurt);
            Assert.DoesNotContain(under.Id, hurt);
            Assert.DoesNotContain(roof.Id, hurt);
            var broken = Of<TileChangedEvent>(sim.Events);
            Assert.Contains(broken, (e) => Grid.SamePos(e.Pos, At(5, 2, 1)) && e.Kind == TileKinds.Floor && e.Decor == null && e.Facing == null);
            Assert.Equal(TileKinds.Low, Grid.KindAt(s, At(5, 2)));
            // On the ground outside, a grenade reaches the street, not the window above it nor the roof.
            var street = Abilities.BlastTiles(s, AbilityIds.Grenade, At(9, 3), thrower);
            Assert.DoesNotContain(street, (p) => p.Z > 0);
            HasPlace(street, At(8, 3));
        }

        [Fact(DisplayName = "noise and blasts by floor › without storeys a blast reaches every height within its radius, as before")]
        public void WithoutStoreysABlastReachesEveryHeight()
        {
            var s = StateFrom(new[] { "......", "......" }, new[] { "..22..", "......" });
            var tiles = Abilities.BlastTiles(s, AbilityIds.Grenade, At(1, 1), null);
            HasPlace(tiles, At(2, 0));
            Assert.True(tiles.All((p) => p.Z == 0));
        }

        [Fact(DisplayName = "noise and blasts by floor › by hand only beside you on your floor: first aid, the terminal")]
        public void ByHandOnlyBesideYouOnYourFloor()
        {
            var s = Building();
            var medic = AddUnit(s, "s1", TemplateIds.Assault, 4, 3, 1, (u) =>
            {
                u.Gear = new List<string> { ItemIds.FirstAid };
                u.Charges = new Dictionary<string, int> { [ChargeIds.FirstAid] = 1 };
            });
            var hurtBelow = AddUnit(s, "s2", TemplateIds.Assault, 5, 3, 0, (u) => u.Hp = 2);
            var hurtBeside = AddUnit(s, "s3", TemplateIds.Assault, 5, 4, 1, (u) => u.Hp = 2);
            var targets = Abilities.AllyTargets(s, medic, AbilityIds.FirstAid);
            Assert.Single(targets);
            Assert.Same(hurtBeside, targets[0]);
            SamePlace(At(5, 3), hurtBelow.Pos);
            // The terminal: from beside it, on its floor.
            s.Mission.Terminal = At(3, 4);
            s.Tiles[Grid.TileIndex(s, 3, 4)] = TileKinds.Console;
            Assert.False(Abilities.CanHackFrom(s, medic, At(3, 3, 1)));
            Assert.True(Abilities.CanHackFrom(s, medic, At(3, 3)));
        }

        // ---------------------------------------------------------------- step 5

        /// <summary>Every place on every level of `s`.</summary>
        private static List<Pos> Everywhere(GameState s)
        {
            return Enumerable.Range(0, s.Width * s.Height * Grid.LevelCount(s)).Select((n) => Grid.PosOf(s, n)).ToList();
        }

        /// <summary>A building's stairs: a ladder dressed as stairs, on any level.</summary>
        private static List<(Pos At, int Up)> StairsOf(GameState s)
        {
            return Everywhere(s).SelectMany((p) =>
            {
                var decor = p.Z == 0 ? s.Decor : s.Levels != null && p.Z - 1 < s.Levels.Count ? s.Levels[p.Z - 1].Decor : null;
                var look = Decor.DecorOf(decor?[Grid.TileIndex(s, p.X, p.Y)]);
                return Grid.KindAt(s, p) == TileKinds.Ladder && look?.Kind == DecorKinds.Stairs ? new[] { (p, look.Facing) } : new (Pos, int)[0];
            }).ToList();
        }

        /// <summary>The describe's sixteen towns, made once (TypeScript makes them as it collects the describe).</summary>
        private static readonly Lazy<List<GameState>> Towns = new Lazy<List<GameState>>(() => Enumerable.Range(0, 16).Select((k) => Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 101 * (k + 1)), Slots = new List<int> { 0 }, Rng = new Rng(k), Kind = MissionKinds.Hack })).ToList());

        [Fact(DisplayName = "the generator's storeys › towns of both styles stand their buildings for real: storeys, stairs up each one, windows, rooms to search")]
        public void TownsOfBothStylesStandTheirBuildingsForReal()
        {
            var three = 0;
            var windows = 0;
            var rooms = new HashSet<string>();
            foreach (var s in Towns.Value)
            {
                Assert.True(s.Levels?.Count >= 2, s.MapId);
                if (s.Levels!.Count >= 3) three++;
                foreach (var (p, up) in StairsOf(s))
                {
                    // A stairwell over the flight, a floor to step off onto on the side it climbs to.
                    Assert.False(Grid.SlabAt(s, p.X, p.Y, p.Z + 1), $"{p.X},{p.Y},{p.Z}");
                    Assert.True(Grid.CanStandOn(Grid.KindAt(s, At(p.X + Grid.Dirs4[up].X, p.Y + Grid.Dirs4[up].Y, p.Z + 1))), $"{p.X},{p.Y},{p.Z}");
                }
                foreach (var p in Everywhere(s).Where((q) => q.Z > 0))
                {
                    if (Grid.KindAt(s, p) == TileKinds.Window)
                    {
                        windows++;
                        // A window upstairs looks out onto open air, never into the next building's storey.
                        Assert.True(Grid.Dirs4.Any((d) => Grid.KindAt(s, At(p.X + d.X, p.Y + d.Y, p.Z)) == null), $"{p.X},{p.Y},{p.Z}");
                    }
                    // Upstairs rooms to search too (docs/FASE8.md §B), under the storey above.
                    var room = Looting.RoomAt(s, p);
                    if (room != null) rooms.Add($"{s.MapId}:{room.Id}");
                }
            }
            Assert.True(three > 0);
            Assert.True(windows > 100, $"{windows} windows");
            Assert.True(rooms.Count > 50, $"{rooms.Count} rooms");
        }

        [Fact(DisplayName = "the generator's storeys › every place one can stand on, on every level, is in the squad's reach")]
        public void EveryPlaceOneCanStandOnIsInTheSquadsReach()
        {
            var missions = Towns.Value.Take(8).Concat(new[] { Biomes.Wilds, Biomes.Facility }.SelectMany((biome) => new[] { 1, 2, 3 }.Select((k) => Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(biome, 57 * k), Slots = new List<int> { 0 }, Rng = new Rng(k) }))));
            foreach (var s in missions)
            {
                var scout = Events.CloneUnit(s.Units.Find((u) => u.Team == Teams.Xcom)!);
                scout.Id = "scout";
                var alone = Events.CloneState(s);
                alone.Units = new List<Unit> { scout };
                var reach = Paths.ComputeReach(alone, scout, 100_000);
                var lost = Everywhere(s).Where((p) => p.Z > 0 && Grid.CanStandOn(Grid.KindAt(s, p)) && !reach.Cost.Has(Grid.NodeOf(s, p))).Select((p) => $"{p.X},{p.Y},{p.Z}").ToList();
                Assert.True(lost.Count == 0, $"{s.MapId}: {string.Join(" ", lost.Take(20))}");
            }
        }

        [Fact(DisplayName = "the generator's storeys › lays each flight in its box (docs/MAPAS.md §4.4): a corridor beside it or the stairwell it turns back over, an open foot, a landing both ways")]
        public void LaysEachFlightInItsBox()
        {
            var count = 0;
            var cramped = 0;
            bool Wallish(string? k) => k == null || k == TileKinds.Wall || k == TileKinds.Window || k == TileKinds.WindowBroken || k == TileKinds.Roof;
            foreach (var s in Towns.Value)
            {
                foreach (var (p, up) in StairsOf(s))
                {
                    var label = $"{s.MapId}: {p.X},{p.Y},{p.Z}";
                    var flight = Flights.FlightAt(s, p)!.Flight;
                    Assert.True(up == flight.Up, label);
                    Assert.True(flight.Tiles.Count == 2 || flight.Tiles.Count == 3, label);
                    var tiles = flight.Tiles.Select((t) => At(t.X, t.Y, p.Z)).ToList();
                    var foot = Flights.FlightFoot(flight);
                    var landing = Flights.FlightLanding(flight);
                    Assert.True(Grid.KindAt(s, foot) == TileKinds.Floor, label);
                    // Up it and back down, a tile's walk each step; the stairwell over all of it.
                    var pairs = new List<(Pos, Pos)> { (foot, tiles[0]) };
                    for (var k = 1; k < tiles.Count; k++) pairs.Add((tiles[k - 1], tiles[k]));
                    pairs.Add((tiles[tiles.Count - 1], landing));
                    foreach (var (a, b) in pairs)
                    {
                        Assert.True(Paths.StepBetween(s, a, b) == Paths.CostOrtho, label);
                        Assert.True(Paths.StepBetween(s, b, a) == Paths.CostOrtho, label);
                    }
                    foreach (var t in tiles) Assert.False(Grid.SlabAt(s, t.X, t.Y, p.Z + 1), label);
                    // Its box: beside it, from its foot to its landing, a corridor with no wall on either floor; or, going back over
                    // the flight below, that one's stairwell. A few squeezed in where nothing else fits.
                    var d = Grid.Dirs4[up];
                    var rows = new List<Pos> { foot };
                    rows.AddRange(tiles);
                    rows.Add(At(landing.X, landing.Y, p.Z));
                    var sides = new[] { new Vec2 { X = -d.Y, Y = d.X }, new Vec2 { X = d.Y, Y = -d.X } };
                    bool Corridor(Vec2 v) => rows.All((q) => new[] { p.Z, p.Z + 1 }.All((z) => !Wallish(Grid.KindAt(s, At(q.X + v.X, q.Y + v.Y, z)))));
                    bool Well(Vec2 v) => p.Z > 0 && tiles.All((q) => Grid.KindAt(s, At(q.X + v.X, q.Y + v.Y, p.Z)) == null);
                    if (!sides.Any((v) => Corridor(v) || Well(v))) cramped++;
                    count++;
                }
            }
            Assert.True(count > 20, $"{count} flights");
            Assert.True(cramped <= count / 50.0, $"{cramped} of {count} cramped");
        }

        [Fact(DisplayName = "the generator's storeys › flat roofs: over two or three storeys reached by the stairs, with a parapet; over one, a ladder up the side about half the time; houses pitched")]
        public void FlatRoofs()
        {
            var decks = 0;
            var parapets = 0;
            var ladders = 0;
            var pitched = 0;
            // A town's one-storey halls are mostly on the industrial estate (docs/MAPAS.md §4.2: in a town of 64 a plot is a house, a shop, flats or an office).
            var estates = Enumerable.Range(0, 4).Select((k) => Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 101 * (k + 1), Sites.Industrial), Slots = new List<int> { 0 }, Rng = new Rng(k), Kind = MissionKinds.Hack })).ToList();
            foreach (var s in Towns.Value.Concat(estates))
            {
                var top = Everywhere(s).Where((p) => p.Z > 0).ToList();
                decks += top.Count((p) => Grid.KindAt(s, p) == TileKinds.Floor && !Grid.SlabAt(s, p.X, p.Y, p.Z + 1) && Grid.StoreysAt(s, p.X, p.Y) == p.Z);
                parapets += top.Count((p) => Grid.KindAt(s, p) == TileKinds.Low && Looting.SearchSpot(s, p) == null);
                pitched += top.Count((p) => Grid.KindAt(s, p) == TileKinds.Roof);
                // A ladder up a wall onto a one-storey roof: a plain ladder on the ground, a roof one can stand on above the wall beside it.
                ladders += Everywhere(s).Count((p) =>
                    p.Z == 0 &&
                    Grid.KindAt(s, p) == TileKinds.Ladder &&
                    Decor.DecorOf(s.Decor?[Grid.TileIndex(s, p.X, p.Y)]) == null &&
                    Grid.Dirs4.Any((d) => Grid.KindAt(s, At(p.X + d.X, p.Y + d.Y, 0)) == TileKinds.Wall && Grid.IsWalkable(Grid.KindAt(s, At(p.X + d.X, p.Y + d.Y, 1)))));
            }
            Assert.True(decks > 0);
            Assert.True(parapets > 0);
            Assert.True(ladders > 0);
            Assert.True(pitched > 0);
        }

        [Fact(DisplayName = "the generator's storeys › in a mission of the living one of their groups starts posted upstairs from difficulty 2, never the dead; none at difficulty 1")]
        public void InAMissionOfTheLivingOneOfTheirGroupsStartsPostedUpstairs()
        {
            // docs/ZOMBIS-PRIMERO.md: the living only hold some missions; in the dead's, everyone starts on the ground.
            foreach (var s in Towns.Value) Assert.False(s.Units.Any((u) => u.Team == Teams.Alien && u.Pos.Z > 0), s.MapId);
            var held = Enumerable.Range(0, 16).Select((k) => Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 101 * (k + 1)), Slots = new List<int> { 0 }, Rng = new Rng(k), Kind = MissionKinds.Hack, Living = true })).ToList();
            var posted = 0;
            foreach (var s in held)
            {
                var up = s.Units.Where((u) => u.Pos.Z > 0).ToList();
                foreach (var u in up) Assert.False(Content.Templates[u.Template].Undead, $"{s.MapId}: {u.Id}");
                if (up.Count > 0)
                {
                    posted++;
                    Assert.Single(up.Select((u) => u.PodId).Distinct());
                    Assert.True(Grid.CanStandOn(Grid.KindAt(s, up[0].Pos)));
                }
            }
            Assert.True(posted > held.Count / 2.0, $"{posted} posted");
            for (var k = 1; k <= 6; k++)
            {
                var easy = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 101 * k), Slots = new List<int> { 0 }, Rng = new Rng(k), Kind = MissionKinds.Hack, Difficulty = 1, Living = true });
                Assert.DoesNotContain(easy.Units, (u) => u.Pos.Z > 0);
            }
        }

        [Fact(DisplayName = "the generator's storeys › the same seed gives the same storeys")]
        public void TheSameSeedGivesTheSameStoreys()
        {
            var again = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 101), Slots = new List<int> { 0 }, Rng = new Rng(0), Kind = MissionKinds.Hack });
            SameJson(Towns.Value[0].Levels, again.Levels);
            Assert.Equal(Towns.Value[0].Tiles, again.Tiles);
        }

        // ---------------------------------------------------------------- step 6

        /// <summary>How far for a walker (ai.ts farness): the tiles, and 3 for each storey between.</summary>
        private static double Farness(GameState s, Pos a, Pos b)
        {
            return Js.Hypot(a.X - b.X, a.Y - b.Y) + 3 * Math.Abs(Grid.HeightAt(s, a) - Grid.HeightAt(s, b));
        }

        /// <summary>An active pod of `units`.</summary>
        private static void Active(GameState s, string id, params Unit[] units)
        {
            Pod(s, id, units);
        }

        [Fact(DisplayName = "the enemy upstairs › a zombie that heard something upstairs goes up the stairs to it, not to the floor under it")]
        public void AZombieThatHeardSomethingUpstairsGoesUp()
        {
            var s = Building();
            s.ActiveTeam = Teams.Alien;
            // A soldier out of its sight, on the roof.
            AddUnit(s, "s1", TemplateIds.Assault, 2, 1, 2);
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 12, 3, 0, (u) => u.Heard = At(5, 3, 1));
            s.Tiles[Grid.TileIndex(s, 7, 3)] = TileKinds.DoorOpen;
            Active(s, "p1", z);
            var walked = new List<Pos>();
            for (var turn = 0; turn < 6 && z.Heard != null; turn++)
            {
                var sim = NewSim(s, turn);
                sim.RunAlienTurn();
                foreach (var e in sim.Events) if (e is MovedEvent m && m.Unit == z.Id) walked.AddRange(m.Path);
            }
            Assert.True(z.Pos.Z >= 1);
            // It got there, upstairs (and having found nobody, may wander off: with both actions since 28/10, even up to the roof).
            Assert.Contains(walked, (p) => p.Z == 1 && Js.Hypot(p.X - 5, p.Y - 3) <= 1.5);
        }

        [Fact(DisplayName = "the enemy upstairs › a zombie that heard a soldier go upstairs climbs after him and bites, never through the floor")]
        public void AZombieThatHeardASoldierGoUpstairsClimbsAfterHim()
        {
            var s = Building();
            s.ActiveTeam = Teams.Alien;
            var soldier = AddUnit(s, "s1", TemplateIds.Assault, 4, 3, 1);
            // Right under him, out of sight and out of reach: the floor is between them.
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 3, 4);
            Active(s, "p1", z);
            z.Heard = Events.CopyPos(soldier.Pos);
            // Up the stairs, then the bite: this turn or the next (two actions, playtest 16/10 #21), always from upstairs.
            ShotEvent? bite = null;
            for (var turn = 0; turn < 2 && bite == null; turn++)
            {
                var sim = NewSim(s, 1);
                sim.RunAlienTurn();
                bite = Of<ShotEvent>(sim.Events).FirstOrDefault();
            }
            Assert.Equal(1, z.Pos.Z);
            Assert.NotNull(bite);
            Assert.Equal((z.Id, soldier.Id, AbilityIds.Claw), (bite!.Unit, bite.Target, bite.Ability));
            Assert.Equal(1, bite.From.Z);
        }

        [Fact(DisplayName = "the enemy upstairs › a living enemy hunting a soldier on another floor goes the long way round by the stairs, not to the spot over his head")]
        public void ALivingEnemyGoesTheLongWayRoundByTheStairs()
        {
            // A long two-storey hall, the stairs at its west end.
            var s = WithLevels(StateFrom(new[] { "........................", ".######################.", ".#S...................#.", ".#....................#.", ".######################.", "........................" }), new[]
            {
                new[] { "                        ", " ###################### ", " # " + new string('.', 19) + "# ", " #....................# ", " ###################### ", "                        " },
            });
            s.ActiveTeam = Teams.Alien;
            var soldier = AddUnit(s, "s1", TemplateIds.Assault, 20, 3);
            var lancer = AddUnit(s, "a1", TemplateIds.Lancer, 20, 2, 1);
            Active(s, "p1", lancer);
            lancer.Heard = Events.CopyPos(soldier.Pos);
            lancer.Ap = 2;
            var start = Farness(s, lancer.Pos, soldier.Pos);
            NewSim(s, 1).RunAlienTurn();
            // Off toward the stairs, though that is away from him for now.
            Assert.True(lancer.Pos.X < 18, $"x {lancer.Pos.X}");
            Assert.True(start > 0);
        }

        [Fact(DisplayName = "the enemy upstairs › the living posted upstairs keep the high ground when they wake")]
        public void TheLivingPostedUpstairsKeepTheHighGround()
        {
            var kept = 0;
            var woke = 0;
            for (var seed = 1; seed <= 6; seed++)
            {
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("torre"), Slots = new List<int> { 0 }, Rng = new Rng(seed), Difficulty = 2 });
                var cuervos = s.Units.Where((u) => u.Team == Teams.Alien && u.Pos.Z == 1).ToList();
                Assert.Equal(2, cuervos.Count);
                // The squad in the street in front of their window.
                var squad = s.Units.Where((u) => u.Team == Teams.Xcom).ToList();
                for (var k = 0; k < squad.Count; k++) squad[k].Pos = At(18 + (k % 3), 6 + (k / 3) * 2);
                s.Concealed = false;
                var sim = NewSim(s, seed);
                sim.ActivatePods(new[] { cuervos[0].PodId! }, new Activation { Cause = PodCauses.Sighted });
                foreach (var u in s.Units) if (u.Team == Teams.Alien) u.Ap = 2;
                sim.RunAlienTurn();
                woke++;
                if (cuervos.All((u) => !u.Alive || u.Pos.Z >= 1)) kept++;
            }
            Assert.Equal(woke, kept);
        }

        // ------------------------------------------------------------ the view (Arturo, 21/10 and 22/10)

        [Fact(DisplayName = "the view (Arturo, 21/10 and 22/10) › reaches farther than the sight, to the end of the street, day or night, and holds all of it")]
        public void ReachesFartherThanTheSight()
        {
            foreach (var night in new[] { false, true })
            {
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 3), Slots = new List<int> { 0 }, Rng = new Rng(3), Kind = MissionKinds.Elimination });
                s.Mission.Night = night;
                var sight = Los.TeamVisibility(s, Teams.Xcom);
                var view = Los.TeamView(s, Teams.Xcom);
                Assert.True(sight.Select((v, i) => !v || view[i]).All((x) => x));
                Assert.True(view.Count((v) => v) > sight.Count((v) => v));
                // As far as a soldier's line of sight goes, however far: straight down the street it stands in, to its end.
                var u = s.Units.Find((v) => v.Team == Teams.Xcom && v.Alive)!;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var x = u.Pos.X;
                    var y = u.Pos.Y;
                    while (x + dx >= 0 && y + dy >= 0 && x + dx < s.Width && y + dy < s.Height && s.Tiles[(y + dy) * s.Width + x + dx] == TileKinds.Floor)
                    {
                        x += dx;
                        y += dy;
                        var to = At(x, y);
                        if (Los.PeekOrigins(s, u.Pos).Any((o) => Los.LineClear(s, o, to))) Assert.True(view[y * s.Width + x], $"{x},{y}");
                    }
                }
            }
        }

        // ------------------------------------------------------------ sight over what is lower than the eyes (Arturo, 22/10)

        [Fact(DisplayName = "sight over what is lower than the eyes (Arturo, 22/10) › a stack of crates is full cover but is seen over; a container is not")]
        public void AStackOfCratesIsFullCoverButIsSeenOver()
        {
            var s = StateFrom(new[] { "..........", "....H.....", ".........." });
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 1, 1);
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 8, 1);
            var at = 1 * s.Width + 4;
            s.Decor = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            s.Decor[at] = Decor.DecorCode(DecorKinds.Crates);
            Assert.True(Los.CanSee(s, soldier, zombie));
            // Still full cover for whoever crouches behind it.
            Assert.Equal(2, Grid.CoverFrom(s, 0, new Vec2 { X = 4, Y = 1 }));
            s.Decor[at] = Decor.DecorCode(DecorKinds.Container);
            Assert.False(Los.CanSee(s, soldier, zombie));
        }
    }
}
