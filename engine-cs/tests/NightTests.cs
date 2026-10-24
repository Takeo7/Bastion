using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/night.test.ts: building height and night missions (docs/ALTURA-Y-NOCHE.md), test by test.
    /// `client toEqual s` is Step2Json.StateDiff; an operation's `toEqual`, its JSON (CampaignJsonTests.Write).
    /// </summary>
    public class NightTests
    {
        private static Sim RunOf(GameState s, int seed = 1)
        {
            return NewSim(s, seed);
        }

        /// <summary>Storeys for every tile of a rectangle (a building's walls and inside).</summary>
        private static void Raise(GameState s, int x0, int y0, int x1, int y1, int n)
        {
            s.Storeys ??= Enumerable.Repeat(0, s.Width * s.Height).ToList();
            for (var y = y0; y <= y1; y++) for (var x = x0; x <= x1; x++) s.Storeys[y * s.Width + x] = n;
        }

        /// <summary>lineClear as it was before storeys existed (commit 3655d07), to hold today's results against; with today's window rules (MAX_WINDOWS_SEEN_THROUGH, WINDOW_SIGHT_REACH).</summary>
        private static bool LineClearBefore(GameState s, Vec2 a, Vec2 b, double? haGiven = null, double? hbGiven = null)
        {
            var ha = haGiven ?? Grid.ElevAt(s, a.X, a.Y) + Grid.EyeHeight;
            var hb = hbGiven ?? Grid.ElevAt(s, b.X, b.Y) + Grid.BodyHeight;
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var nx = Math.Abs(dx);
            var ny = Math.Abs(dy);
            var sx = Math.Sign(dx);
            var sy = Math.Sign(dy);
            var len2 = dx * dx + dy * dy;
            if (len2 == 0) len2 = 1;
            double LineAt(int px, int py) => ha + (hb - ha) * Math.Min(1, Math.Max(0, (double)((px - a.X) * dx + (py - a.Y) * dy) / len2));
            bool Blocks(int px, int py) => Grid.SightTop(s, px, py) > LineAt(px, py) + 1e-6;
            var x = a.X;
            var y = a.Y;
            var ix = 0;
            var iy = 0;
            var windows = 0;
            while (ix < nx || iy < ny)
            {
                var decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
                if (decision == 0)
                {
                    if (Blocks(x + sx, y) && Blocks(x, y + sy)) return false;
                    x += sx;
                    y += sy;
                    ix++;
                    iy++;
                }
                else if (decision < 0)
                {
                    x += sx;
                    ix++;
                }
                else
                {
                    y += sy;
                    iy++;
                }
                if (x == b.X && y == b.Y) return true;
                if (Blocks(x, y)) return false;
                var i = y * s.Width + x;
                if ((s.Tiles[i] == TileKinds.Window || s.Tiles[i] == TileKinds.WindowBroken) && LineAt(x, y) < s.Elev[i] + 1 && ((Math.Max(Math.Abs(x - a.X), Math.Abs(y - a.Y)) > Los.WindowSightReach && Math.Max(Math.Abs(x - b.X), Math.Abs(y - b.Y)) > Los.WindowSightReach) || ++windows > Los.MaxWindowsSeenThrough)) return false;
            }
            return true;
        }

        // ------------------------------------------------------------ tall buildings

        /// <summary>A shop (walls x 6–10, y 1–5) between a one-storey rooftop at (2,3) and the street beyond.</summary>
        private static GameState Street()
        {
            return StateFrom(
                new[] { "....................", "......#####.........", "......#...#.........", "......#...#.........", "......#...#.........", "......#####.........", "...................." },
                new[] { "", "", "", "..1", "", "", "" });
        }

        private static readonly string[] WindowRows = { "............", "..#####.....", "..#...#.....", "..=...#.....", "..#...#.....", "..#####.....", "............" };

        [Fact(DisplayName = "tall buildings › from a house’s rooftop nobody sees over a three-storey shop")]
        public void FromAHousesRooftopNobodySeesOverAThreeStoreyShop()
        {
            var s = Street();
            var watcher = AddUnit(s, "s1", TemplateIds.Rookie, 2, 3);
            var target = AddUnit(s, "a1", TemplateIds.Trooper, 16, 3);
            // One storey (or no storeys at all): over the roof, as ever.
            Assert.True(Los.CanSee(s, watcher, target));
            Raise(s, 6, 1, 10, 5, 1);
            Assert.True(Los.CanSee(s, watcher, target));
            Raise(s, 6, 1, 10, 5, 3);
            Assert.False(Los.CanSee(s, watcher, target));
        }

        [Fact(DisplayName = "tall buildings › through a ground-floor window, someone on the ground beside it sees inside; from a rooftop, nobody does")]
        public void ThroughAGroundFloorWindow()
        {
            var rows = WindowRows;
            var ground = StateFrom(rows);
            Raise(ground, 2, 1, 6, 5, 3);
            var inside = P(4, 3, 0);
            Assert.True(Los.CanSeeTile(ground, P(1, 3, 0), inside, 18));
            // A step further back, the glass (Ana, 23/10 #21).
            Assert.False(Los.CanSeeTile(ground, P(0, 3, 0), inside, 18));
            // The same wall without the window: nothing.
            var blind = StateFrom(rows.Select((r) => r.Replace('=', '#')).ToArray());
            Raise(blind, 2, 1, 6, 5, 3);
            Assert.False(Los.CanSeeTile(blind, P(0, 3, 0), inside, 18));
            // From a one-storey rooftop beside it, the window shows only the wall above it.
            var roof = StateFrom(rows, new[] { "", "", "", "1" });
            Assert.True(Los.LineClear(roof, P(0, 3), inside));
            Raise(roof, 2, 1, 6, 5, 3);
            Assert.False(Los.LineClear(roof, P(0, 3), inside));
            // And no line crosses its upper storeys, even an open one over the ground floor's furniture.
            Assert.False(Los.LineClear(roof, P(0, 3), P(10, 3), 1.7, 1.5));
        }

        [Fact(DisplayName = "tall buildings › playtest 2026-10-13: from beside it, a window, whole or broken, shows the room and lifts its fog; from further off, nothing (23/10 #21)")]
        public void FromBesideItAWindowShowsTheRoom()
        {
            var rows = WindowRows;
            List<string> Room(bool[] seen)
            {
                var out_ = new List<string>();
                for (var y = 2; y <= 4; y++) for (var x = 3; x <= 5; x++) if (seen[y * 12 + x]) out_.Add($"{x},{y}");
                return out_;
            }
            foreach (var glass in new[] { TileKinds.Window, TileKinds.WindowBroken })
            {
                foreach (var storeys in new[] { 1, 2, 3 })
                {
                    var s = StateFrom(rows);
                    s.Tiles[3 * 12 + 2] = glass;
                    Raise(s, 2, 1, 6, 5, storeys);
                    // Two steps back: the glass, and only what is right behind it.
                    var watcher = AddUnit(s, "s1", TemplateIds.Rookie, 0, 3);
                    var label = $"{glass}, {storeys} storeys";
                    Assert.True(Room(Los.TeamVisibility(s, Teams.Xcom)).SequenceEqual(new[] { "3,3" }), label);
                    var inside = AddUnit(s, "a1", TemplateIds.Zombie, 5, 4);
                    Assert.False(Los.CanSee(s, watcher, inside), label);
                    // Right at the glass: the whole room, and whoever is in there.
                    watcher.Pos = P(1, 3, 0);
                    Assert.True(Room(Los.TeamVisibility(s, Teams.Xcom)).Count == 9, label);
                    Assert.True(Los.CanSee(s, watcher, inside), label);
                }
            }
            // The same wall without the window: the room stays in the fog.
            var blind = StateFrom(rows.Select((r) => r.Replace('=', '#')).ToArray());
            Raise(blind, 2, 1, 6, 5, 1);
            AddUnit(blind, "s1", TemplateIds.Rookie, 1, 3);
            Assert.Empty(Room(Los.TeamVisibility(blind, Teams.Xcom)));
        }

        [Fact(DisplayName = "tall buildings › maps without storeys see exactly as before; from the ground, so do one-storey buildings")]
        public void MapsWithoutStoreysSeeExactlyAsBefore()
        {
            var layout = Mapgen.GenerateLayout(new GenOptions { Biome = Biomes.City, Kind = MissionKinds.Elimination, PodSizes = new List<int> { 2, 2, 3 }, Seed = 7 });
            var old = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 7), Slots = new List<int> { 0 }, Rng = new Rng(1) });
            Assert.Equal(layout.Storeys, old.Storeys);
            var tall = old.Storeys!.Any((n) => n >= 2);
            Assert.True(tall);
            // Without their storeys above the ground (docs/FASE6-PLAN.md): the model of tall buildings this tests.
            old.Levels = null;
            var flat = Events.CloneState(old);
            flat.Storeys = null;
            var low = Events.CloneState(old);
            low.Storeys = low.Storeys!.Select((n) => Math.Min(n, 1)).ToList();
            // Rooftops and the street, from every height a unit has.
            var rng = new Rng(3);
            var differs = 0;
            for (var k = 0; k < 4000; k++)
            {
                var a = P(rng.Int(48), rng.Int(48));
                var b = P(rng.Int(48), rng.Int(48));
                var ha = Grid.ElevAt(old, a.X, a.Y) + rng.Pick(new[] { Grid.EyeHeight, 1.7, 2.7 });
                var before = LineClearBefore(old, a, b, ha);
                Assert.True(Los.LineClear(flat, a, b, ha) == before, $"flat, ({a.X},{a.Y}) to ({b.X},{b.Y}) from {ha}");
                // From the street to the street, nothing reaches a one-storey roof: the same as before.
                if (Grid.ElevAt(old, a.X, a.Y) == 0 && Grid.ElevAt(old, b.X, b.Y) == 0) Assert.True(Los.LineClear(low, a, b) == LineClearBefore(old, a, b), $"low, ({a.X},{a.Y}) to ({b.X},{b.Y})");
                if (Los.LineClear(old, a, b, ha) != before) differs++;
            }
            // With the real storeys, some lines over the town now stop at the tall buildings.
            Assert.True(differs > 0, $"differs {differs}");
        }

        [Fact(DisplayName = "tall buildings › every building has a roof: from a raised platform nobody sees into a house, but over it they do")]
        public void EveryBuildingHasARoof()
        {
            // A one-storey house (walls x 4–8, y 1–5) with a window on its west wall, a platform west of it.
            var rows = new[] { "...............", "....#####......", "....#...#......", "....=...#......", "....#...#......", "....#####......", "..............." };
            var s = StateFrom(rows, new[] { "", "", "11", "11" });
            var room = P(7, 2);
            // Without storeys, as before: down over the wall into the room.
            Assert.True(Los.LineClear(s, P(0, 2), room));
            Assert.True(Los.LineClear(s, P(1, 2), room));
            Raise(s, 4, 1, 8, 5, 1);
            Assert.False(Los.LineClear(s, P(0, 2), room));
            Assert.False(Los.LineClear(s, P(1, 2), room));
            // Looking out of the room at the platform is the same line backwards.
            Assert.False(Los.LineClear(s, room, P(1, 2), 0.7, 1.55));
            // Over the roof to the street beyond, as before.
            Assert.Equal(LineClearBefore(s, P(1, 2), P(13, 2), 1.7, 0.55), Los.LineClear(s, P(1, 2), P(13, 2), 1.7, 0.55));
            // A window is still a window from beside it, and only from there (Ana, 23/10 #21).
            Assert.False(Los.LineClear(s, P(0, 3), P(6, 3)));
            var street = StateFrom(rows);
            Raise(street, 4, 1, 8, 5, 1);
            Assert.True(Los.LineClear(street, P(3, 3), P(6, 3)));
            Assert.False(Los.LineClear(street, P(1, 3), P(6, 3)));
        }

        [Fact(DisplayName = "tall buildings › the generator sets the storeys of each building, by its use and by its seed")]
        public void TheGeneratorSetsTheStoreysOfEachBuilding()
        {
            GameState Town(int seed) => Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, seed), Slots = new List<int> { 0 }, Rng = new Rng(1) });
            // TypeScript counts in `[0, 0, 0, 0]`, where a building of four storeys counts NaN: here, any number of them.
            var counts = new Dictionary<int, int>();
            var desks = new HashSet<int>();
            for (var seed = 1; seed <= 6; seed++)
            {
                var s = Town(seed);
                Assert.Equal(s.Storeys, Town(seed).Storeys);
                for (var i = 0; i < s.Tiles.Count; i++)
                {
                    var n = s.Storeys![i];
                    counts[n] = counts.GetValueOrDefault(n) + 1;
                    // Inside a building, or its walls; nothing out in the street.
                    if (n != 0) Assert.True(s.Ground![i] == 2 || new[] { TileKinds.Wall, TileKinds.Door, TileKinds.Window, TileKinds.WindowBroken, TileKinds.DoorOpen, TileKinds.Floor }.Contains(s.Tiles[i]));
                    else Assert.NotEqual(2, s.Ground![i]);
                    // Offices stand two or three storeys (a hall's office, one); buildings stand on the ground.
                    if (Decor.DecorOf(s.Decor![i])?.Kind == DecorKinds.Desk) desks.Add(n);
                    if (n != 0) Assert.Equal(0, s.Elev[i]);
                }
            }
            Assert.True(counts.GetValueOrDefault(1) > 0);
            Assert.True(counts.GetValueOrDefault(2) > 0);
            Assert.True(counts.GetValueOrDefault(3) > 0);
            Assert.Contains(desks, (n) => n >= 2);
            // Out of town, everything is one storey; at a factory, but its offices, which may have two (playtest 15/10, #17).
            foreach (var biome in new[] { Biomes.Wilds, Biomes.Facility })
            {
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(biome, 4), Slots = new List<int> { 0 }, Rng = new Rng(1) });
                Assert.True(s.Storeys!.Max() <= (biome == Biomes.Wilds ? 1 : 2), biome);
            }
            // Hand-made maps have none: they see as before.
            Assert.Null(Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("plaza"), Slots = new List<int> { 0 }, Rng = new Rng(1) }).Storeys);
        }

        // ------------------------------------------------------------ night

        /// <summary>An open field at night, with one street light at (2,15).</summary>
        private static GameState Dark()
        {
            var s = StateFrom(Enumerable.Range(0, 20).Select((_) => new string('.', 30)).ToArray());
            s.Mission.Night = true;
            s.Lights = new List<Light> { new Light { Id = "l1", Tile = P(2, 15, 0), Radius = Content.Night.StreetLight } };
            return s;
        }

        [Fact(DisplayName = "night › the living see 8 tiles less (6 at least), unless what they look at is lit; the dead see as ever")]
        public void TheLivingSeeEightTilesLess()
        {
            var s = Dark();
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 2, 2);
            var near = AddUnit(s, "a1", TemplateIds.Trooper, 2, 11);
            var far = AddUnit(s, "a2", TemplateIds.Trooper, 2, 14);
            Assert.Equal(18, Los.SightOf(soldier));
            Assert.Equal(10, Los.NightSight(soldier));
            Assert.True(Los.CanSee(s, soldier, near));
            // Twelve tiles off in the dark: unseen; in the street light's circle: seen.
            far.Pos = P(12, 14, 0);
            Assert.False(Los.CanSee(s, soldier, far));
            far.Pos = P(2, 14, 0);
            Assert.True(Los.IsLit(s, far.Pos));
            Assert.True(Los.CanSee(s, soldier, far));
            // Short-sighted to begin with: never under 6.
            var myopic = Events.CloneUnit(soldier);
            myopic.Mods = Content.AddMods(soldier.Mods, new UnitMods { Sight = -8 }); // `{ ...soldier.mods, sight: -8 }`: its sight is 0.
            Assert.Equal(Content.Night.MinSight, Los.NightSight(myopic));
            // The dead hunt by ear, and see as far as by day.
            var zombie = AddUnit(s, "a3", TemplateIds.Zombie, 20, 2);
            Assert.Equal(Content.Templates[TemplateIds.Zombie].Sight, Los.SightTo(s, zombie, P(20, 15)));
            // The squad's view of the map is the same: dark tiles only from close by.
            var seen = Los.TeamVisibility(s, Teams.Xcom);
            Assert.True(seen[11 * 30 + 2]);
            Assert.False(seen[13 * 30 + 12]);
            Assert.True(seen[15 * 30 + 2]);
        }

        [Fact(DisplayName = "night › a light reaches only what it sees: walls and roofs keep it out")]
        public void ALightReachesOnlyWhatItSees()
        {
            // A garden wall (x 6), an alley (x 7) and a one-storey house (x 8–12) with a window at (8,3).
            var s = StateFrom(new[] { "..............", "......#.#####.", "......#.#...#.", "......#.=...#.", "......#.#...#.", "......#.#####.", ".............." });
            s.Mission.Night = true;
            Raise(s, 8, 1, 12, 5, 1);
            int At(int x, int y) => y * 14 + x;
            // A street lamp at (5,2): the pavement round it is lit, and from up high it shines over the garden wall.
            var lamp = Los.LightTiles(s, P(5, 2, 0), Content.Night.StreetLight, Content.Night.LampHeight);
            s.Lights = new List<Light> { new Light { Id = "l1", Tile = P(5, 2, 0), Radius = Content.Night.StreetLight, Tiles = lamp } };
            Assert.True(Los.IsLit(s, P(4, 2)));
            Assert.True(Los.IsLit(s, P(5, 4)));
            Assert.True(Los.IsLit(s, P(7, 2)));
            // A light burning on the ground doesn't: the wall keeps its light in.
            var (radius, height) = (3, 0.5);
            var low = Los.LightTiles(s, P(5, 2, 0), radius, height);
            Assert.Contains(At(3, 2), low);
            Assert.DoesNotContain(At(7, 2), low);
            // In the alley: a lamp's light stops at the house's roof, but a light on the ground lights the room through the window.
            Assert.DoesNotContain(At(9, 3), Los.LightTiles(s, P(7, 3, 0), Content.Night.StreetLight, Content.Night.LampHeight));
            var flare = Los.LightTiles(s, P(7, 3, 0), radius, height);
            Assert.Contains(At(9, 3), flare);
            Assert.Contains(At(7, 5), flare);
            Assert.DoesNotContain(At(9, 2), flare);
            // A map's lamps are worked out when the mission is made, and the mask matches isLit.
            var night = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 21), Slots = new List<int> { 0 }, Rng = new Rng(1), Night = true });
            var mask = Los.LitMask(night);
            foreach (var l in night.Lights!)
            {
                Assert.NotEmpty(l.Tiles!);
                foreach (var i in l.Tiles!) Assert.True(Los.IsLit(night, Grid.PosOf(night, i)) == mask[i], $"{l.Id}, node {i}");
            }
        }

        [Fact(DisplayName = "night › by day nothing changes, street lights or not")]
        public void ByDayNothingChanges()
        {
            var s = Dark();
            s.Mission.Night = false;
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 2, 2);
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 2, 8);
            Assert.Equal(18, Los.SightTo(s, soldier, P(20, 18)));
            Assert.Equal(Content.Templates[TemplateIds.Zombie].Detection, Los.DetectionTo(s, zombie, soldier.Pos));
            s.Mission.Night = null;
            Assert.Equal(18, Los.SightTo(s, soldier, P(20, 18)));
        }

        [Fact(DisplayName = "night › a hidden squad is noticed from half as far, unless it stands in the light")]
        public void AHiddenSquadIsNoticedFromHalfAsFar()
        {
            bool Run(bool night, Pos at)
            {
                var s = Dark();
                s.Mission.Night = night;
                s.Concealed = true;
                AddUnit(s, "s1", TemplateIds.Rookie, 25, 2);
                var scout = AddUnit(s, "s2", TemplateIds.Rookie, 25, 3);
                var z = AddUnit(s, "a1", TemplateIds.Zombie, at.X, at.Y + 4);
                Pod(s, "p1", new[] { z }, active: false);
                Assert.Equal(night && !Los.IsLit(s, at) ? 3 : 6, Los.DetectionTo(s, z, at));
                var r = RunOf(s);
                r.MoveAlong(scout, new List<Pos> { at }, 0);
                return s.Pods[0].Active;
            }
            // Four tiles from a zombie (detection 6): noticed by day, not at night...
            Assert.True(Run(false, P(10, 5, 0)));
            Assert.False(Run(true, P(10, 5, 0)));
            // ...unless the soldier walks under a street light.
            Assert.True(Run(true, P(2, 13, 0)));
        }

        [Fact(DisplayName = "night › a night mission: one more group of the dead, its street lights, and the same map")]
        public void ANightMissionOneMoreGroupOfTheDead()
        {
            var map = MapSource.Generated(Biomes.City, 21);
            var day = Setup.CreateMission(new MissionOptions { Map = map, Slots = new List<int> { 0 }, Rng = new Rng(1), Kind = MissionKinds.Elimination, Difficulty = 2 });
            var night = Setup.CreateMission(new MissionOptions { Map = map, Slots = new List<int> { 0 }, Rng = new Rng(1), Kind = MissionKinds.Elimination, Difficulty = 2, Night = true });
            Assert.Equal(false, day.Mission.Night);
            Assert.Equal(true, night.Mission.Night);
            // The lone dead (setup.ts LURKERS) are the same by night and by day, after the groups.
            List<Pod> Groups(GameState s) => s.Pods.Where((p) => !p.Id.StartsWith("l", StringComparison.Ordinal)).ToList();
            Assert.Equal(Groups(day).Count + 1, Groups(night).Count);
            Assert.Equal(day.Pods.Count - Groups(day).Count, night.Pods.Count - Groups(night).Count);
            var extra = Groups(night)[Groups(night).Count - 1].UnitIds.Select((id) => night.Units.Find((u) => u.Id == id)!.Template);
            Assert.Equal(Setup.NightPod(2), extra);
            Assert.Equal(day.Tiles, night.Tiles);
            Assert.True(night.Lights!.Count > 4);
            foreach (var l in night.Lights!)
            {
                // A town's street lights reach three (docs/MAPAS.md §6), further than a 48 map's.
                Assert.Equal(Content.Night.StreetLight + 1, l.Radius);
                Assert.Null(l.Turns);
                Assert.Equal(TileKinds.Floor, night.Tiles[l.Tile.Y * night.Width + l.Tile.X]);
            }
        }

        [Fact(DisplayName = "night › more suministros at night: in what turns up and in what the operation pays")]
        public void MoreSuministrosAtNight()
        {
            for (var seed = 1; seed < 200; seed++)
            {
                var by = Looting.RollLoot("shelf", 2, new Rng(seed));
                var dusk = Looting.RollLoot("shelf", 2, new Rng(seed), true);
                if (by?.Supplies is int supplies && supplies != 0) Assert.Equal(Step2Json.Element(new Loot { Supplies = (int)Js.Round(supplies * Content.Night.Supplies) }).GetRawText(), Step2Json.Element(dusk).GetRawText());
                else Assert.Equal(Step2Json.Element(by).GetRawText(), Step2Json.Element(dusk).GetRawText());
            }
            var seeds = Enumerable.Range(0, 300).Select((i) => i * 7919 + 13).ToList();
            var nights = seeds.Where(Campaign.NightFor).ToList();
            Assert.True((double)nights.Count / seeds.Count > 0.25, $"{nights.Count} nights");
            Assert.True((double)nights.Count / seeds.Count < 0.42, $"{nights.Count} nights");
            MissionOffer Offer(int seed, Action<MissionOffer>? extra = null)
            {
                var o = new MissionOffer
                {
                    Id = "m1",
                    Name = "Operación",
                    Kind = MissionKinds.Elimination,
                    Difficulty = 2,
                    ExpiresDay = 9,
                    Reward = new MissionReward { Credits = 100, Recruit = true },
                    Map = MapSource.Generated(Biomes.City, seed),
                    Pods = new List<List<string>>(),
                    Final = false,
                    Region = RegionIds.Na,
                    Facility = null,
                    Story = null,
                    Quest = null,
                };
                extra?.Invoke(o);
                return o;
            }
            var dark = Campaign.ByNight(Offer(nights[0]));
            Assert.Equal((true, 150, (object?)true), (dark.Night, dark.Reward.Credits, dark.Reward.Recruit));
            var day = seeds.First((x) => !Campaign.NightFor(x));
            Assert.Equal(CampaignJsonTests.Write(Offer(day)), CampaignJsonTests.Write(Campaign.ByNight(Offer(day))));
            // Story chapters and the final mission are flown by day.
            Assert.Null(Campaign.ByNight(Offer(nights[0], (o) => o.Story = 2)).Night);
            Assert.Null(Campaign.ByNight(Offer(nights[0], (o) => o.Final = true)).Night);
            // In a campaign, every operation's night is its seed's.
            for (var seed = 1; seed < 12; seed++)
            {
                // `(o.map as { seed: number }).seed`: a map without one is `nightFor(undefined)`, which is nightFor(0).
                foreach (var o in Campaign.CreateCampaign(new[] { 0 }, new Rng(seed)).Offers) Assert.Equal(o.Story == null && !o.Final && Campaign.NightFor(o.Map.Seed ?? 0), o.Night == true);
            }
        }

        [Fact(DisplayName = "night › the Saqueo’s horde comes a turn sooner at night")]
        public void TheSaqueosHordeComesATurnSoonerAtNight()
        {
            int? First(bool night)
            {
                var s = StateFrom(Enumerable.Range(0, 40).Select((_) => new string('.', 40)).ToArray());
                s.Mission.Kind = MissionKinds.Scavenge;
                s.Mission.EvacZone = new List<Vec2> { new Vec2 { X = 39, Y = 39 } };
                s.Mission.Horde = new List<string> { TemplateIds.Zombie, TemplateIds.Zombie };
                s.Mission.Night = night;
                AddUnit(s, "s1", TemplateIds.Rookie, 20, 20);
                for (var turn = 1; turn < 10; turn++)
                {
                    var r = RunOf(s, turn);
                    r.RunAlienTurn();
                    var wave = Of<ReinforcementsIncomingEvent>(r.Events).FirstOrDefault((e) => e.R.Trickle != true);
                    if (wave != null) return turn;
                }
                return null;
            }
            Assert.Equal(First(false) - Content.Night.HordeHead, First(true));
        }

        [Fact(DisplayName = "night › clients replaying a night end with the host state")]
        public void ClientsReplayingANightEndWithTheHostState()
        {
            var host = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 33), Slots = new List<int> { 0 }, Rng = new Rng(1), Night = true });
            var client = Events.CloneState(host);
            var run = RunOf(host, 4);
            run.RunAlienTurn();
            run.RunAlienTurn();
            foreach (var e in run.Events) Events.ApplyEvent(client, e);
            Assert.Null(Step2Json.StateDiff(host, client));
        }
    }
}
