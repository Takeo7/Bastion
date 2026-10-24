using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/levels.test.ts, the tests that use only step 1's modules (grid, decor, flights, los, path,
    /// cover), and setup.ts createMission for the missions they look at. «who sees whom» leaves out its two lines on
    /// previewShot (aim.ts).
    /// </summary>
    public class LevelsStep1Tests
    {
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

        private static bool Has(IEnumerable<Pos> places, Pos p)
        {
            return places.Any((q) => Grid.SamePos(q, p));
        }

        // ------------------------------------------------------------ places with a level

        [Fact(DisplayName = "places with a level › a node puts the ground first, so on the ground it is the tile index")]
        public void ANodePutsTheGroundFirst()
        {
            var s = Building();
            Assert.Equal(3, Grid.LevelCount(s));
            Assert.Equal(1, Grid.LevelCount(StateFrom(new[] { "..." })));
            var area = s.Width * s.Height;
            for (var n = 0; n < area * Grid.LevelCount(s); n++) Assert.Equal(n, Grid.NodeOf(s, Grid.PosOf(s, n)));
            Assert.Equal(Grid.TileIndex(s, 5, 3), Grid.NodeOf(s, At(5, 3)));
            SamePlace(At(5, 3, 1), Grid.PosOf(s, area + Grid.TileIndex(s, 5, 3)));
        }

        [Fact(DisplayName = "places with a level › what stands on each level, how high it is, its slab and where one can stand")]
        public void WhatStandsOnEachLevel()
        {
            var s = Building();
            Assert.Equal(TileKinds.Ladder, Grid.KindAt(s, At(2, 2)));
            Assert.Null(Grid.KindAt(s, At(2, 2, 1)));
            Assert.Equal(TileKinds.Floor, Grid.KindAt(s, At(3, 2, 1)));
            Assert.Equal(TileKinds.Window, Grid.KindAt(s, At(7, 3, 1)));
            Assert.Equal(TileKinds.Roof, Grid.KindAt(s, At(8, 1, 1)));
            Assert.Null(Grid.KindAt(s, At(0, 0, 3)));
            Assert.Equal(TileKinds.Wall, Grid.KindAt(s, At(-1, 0)));
            Assert.Equal(new[] { 0, 1, 2 }, new[] { Grid.HeightAt(s, At(3, 3)), Grid.HeightAt(s, At(3, 3, 1)), Grid.HeightAt(s, At(3, 3, 2)) });
            // The stairwell has no slab; the lean-to's roof is one nobody stands on.
            Assert.False(Grid.SlabAt(s, 2, 2, 1));
            Assert.True(Grid.SlabAt(s, 3, 2, 1));
            Assert.True(Grid.SlabAt(s, 8, 1, 1));
            Assert.False(Grid.SlabAt(s, 3, 2, 0));
            SamePlace(At(3, 2), Grid.SurfaceAt(s, 3, 2, 0));
            SamePlace(At(3, 2, 1), Grid.SurfaceAt(s, 3, 2, 1));
            SamePlace(At(3, 2, 2), Grid.SurfaceAt(s, 3, 2, 2));
            Assert.Null(Grid.SurfaceAt(s, 2, 2, 1));
            Assert.Null(Grid.SurfaceAt(s, 8, 1, 1));
            Assert.Null(Grid.SurfaceAt(s, 1, 2, 0));
            // Raised ground (a block of flats' roof) is the ground's surface at its height.
            var flats = StateFrom(new[] { ".1." }, new[] { ".1." });
            SamePlace(At(1, 0), Grid.SurfaceAt(flats, 1, 0, 1));
        }

        [Fact(DisplayName = "places with a level › one unit above another is not on the same place; a place without its level is the ground")]
        public void OneUnitAboveAnotherIsNotOnTheSamePlace()
        {
            var s = Building();
            var below = AddUnit(s, "s1", TemplateIds.Assault, 3, 3);
            var above = AddUnit(s, "a1", TemplateIds.Trooper, 3, 3, 1);
            SamePlace(At(3, 3), below.Pos);
            SamePlace(At(3, 3, 1), above.Pos);
            Assert.Same(above, Grid.UnitAt(s, At(3, 3, 1)));
            Assert.Same(below, Grid.UnitAt(s, At(3, 3)));
            Assert.Same(below, Grid.UnitAt(s, new Pos { X = 3, Y = 3 }));
            Assert.Null(Grid.UnitAt(s, At(3, 3, 2)));
            Assert.False(Grid.SamePos(below.Pos, above.Pos));
            SamePlace(At(1, 1), MakeUnit("x", TemplateIds.Zombie, "Zombi", null, new Pos { X = 1, Y = 1 }, null).Pos);
        }

        // ------------------------------------------------------------ moving up and down

        [Fact(DisplayName = "moving up and down › stairs climb a storey like a ladder, up to the first floor and on to the roof, and back down")]
        public void StairsClimbAStoreyLikeALadder()
        {
            var s = Building();
            var climb = Paths.CostOrtho + Paths.CostClimb;
            Assert.Equal(climb, Paths.StepBetween(s, At(2, 2), At(3, 2, 1)));
            Assert.Equal(climb, Paths.StepBetween(s, At(2, 2), At(2, 3, 1)));
            Assert.Equal(climb, Paths.StepBetween(s, At(3, 2, 1), At(2, 2)));
            Assert.Equal(climb, Paths.StepBetween(s, At(6, 4, 1), At(6, 3, 2)));
            // Only from the stairs, one storey at a time.
            Assert.Null(Paths.StepBetween(s, At(3, 2), At(4, 2, 1)));
            Assert.Null(Paths.StepBetween(s, At(3, 2, 1), At(3, 3, 2)));
            Assert.Null(Paths.StepBetween(s, At(2, 2), At(3, 2, 2)));
            // From a soldier on the ground floor to the roof and back: the path climbs both flights.
            var u = AddUnit(s, "s1", TemplateIds.Assault, 4, 3);
            var reach = Paths.ComputeReach(s, u, 1000);
            var roof = At(3, 3, 2);
            var path = Paths.BuildPath(s, u, reach, roof)!;
            SamePlace(roof, path[path.Count - 1]);
            var climbs = path.Skip(1).Where((p, i) => Grid.HeightAt(s, p) > Grid.HeightAt(s, path[i])).ToList();
            Assert.Equal(2, climbs.Count);
            Assert.True(Has(path, At(2, 2)));
            Assert.True(Has(path, At(6, 4, 1)));
            // Every place a unit can stand on, on every level, is in reach; the stairwell and the roofs nobody stands on are not.
            var ends = Paths.Destinations(s, u, reach).Select((d) => d.Pos).ToList();
            Assert.True(Has(ends, At(5, 2, 1)));
            Assert.False(Has(ends, At(2, 2, 1)));
            Assert.False(Has(ends, At(8, 1, 1)));
            Assert.False(Has(ends, At(7, 3, 1)));
        }

        [Fact(DisplayName = "moving up and down › a building's stairs are left and taken at the top only by their landing, the tile they face; a ladder by any side")]
        public void ABuildingsStairsAreLeftAndTakenAtTheTopOnlyByTheirLanding()
        {
            var s = Building();
            // The stairs at (2,2) climb east, onto (3,2) on the first floor.
            s.Decor = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            s.Decor[Grid.TileIndex(s, 2, 2)] = Decor.DecorCode(DecorKinds.Stairs, 1);
            var climb = Paths.CostOrtho + Paths.CostClimb;
            Assert.Equal(climb, Paths.StepBetween(s, At(2, 2), At(3, 2, 1)));
            Assert.Equal(climb, Paths.StepBetween(s, At(3, 2, 1), At(2, 2)));
            // Not off the side of the flight, up or down, nor dropping into the stairwell.
            Assert.Null(Paths.StepBetween(s, At(2, 2), At(2, 3, 1)));
            Assert.Null(Paths.StepBetween(s, At(2, 3, 1), At(2, 2)));
            Assert.Empty(Paths.StepsFrom(s, At(2, 3, 1), 0, -1));
            // On its own level the foot of the flight is a tile like any other.
            Assert.Equal(Paths.CostOrtho, Paths.StepBetween(s, At(2, 3), At(2, 2)));
            // The upstairs is still all in reach, by the landing.
            var u = AddUnit(s, "s1", TemplateIds.Assault, 4, 3);
            var reach = Paths.ComputeReach(s, u, 1000);
            var path = Paths.BuildPath(s, u, reach, At(2, 4, 1))!;
            Assert.True(Has(path, At(3, 2, 1)));
            // The ladder up the shed's side, with no stairs on it, still climbs as before.
            Assert.Equal(climb, Paths.StepBetween(s, At(9, 7), At(10, 7, 1)));
        }

        [Fact(DisplayName = "moving up and down › floors are solid: nobody walks or drops through them, and enemies on another floor block nothing")]
        public void FloorsAreSolid()
        {
            var s = Building();
            // Inside, the floor above is no way down or up.
            Assert.Null(Paths.StepBetween(s, At(4, 3, 1), At(5, 3)));
            Assert.Null(Paths.StepBetween(s, At(4, 3, 2), At(5, 3, 1)));
            var a = Paths.StepsFrom(s, At(4, 3, 1), 1, 0);
            Assert.Single(a);
            SamePlace(At(5, 3, 1), a[0].To);
            Assert.Equal(Paths.CostOrtho, a[0].Cost);
            var b = Paths.StepsFrom(s, At(4, 3), 1, 1);
            Assert.Single(b);
            SamePlace(At(5, 4), b[0].To);
            Assert.Equal(Paths.CostDiag, b[0].Cost);
            // A zombie upstairs doesn't stop a soldier crossing the ground floor under it, one on the ground does.
            var u = AddUnit(s, "s1", TemplateIds.Assault, 2, 4);
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 4, 4, 1);
            Reach Reach() => Paths.ComputeReach(s, u, 1000);
            Assert.Equal(4 * Paths.CostOrtho, Reach().Cost.Get(Grid.NodeOf(s, At(6, 4)), -1));
            Assert.False(Reach().Cost.Has(Grid.NodeOf(s, At(4, 4, 1))));
            zombie.Pos = At(4, 4);
            Assert.True(Reach().Cost.Get(Grid.NodeOf(s, At(6, 4)), -1) > 4 * Paths.CostOrtho);
            Assert.True(Reach().Cost.Has(Grid.NodeOf(s, At(4, 4, 1))));
            // Two units, one above the other: each stands on its own place.
            var ally = AddUnit(s, "s2", TemplateIds.Assault, 5, 3, 1);
            Assert.True(Paths.Destinations(s, u, Reach()).Any((d) => Grid.SamePos(d.Pos, At(5, 3))));
            Assert.False(Paths.Destinations(s, u, Reach()).Any((d) => Grid.SamePos(d.Pos, ally.Pos)));
        }

        [Fact(DisplayName = "moving up and down › off a roof or out of an upper window one drops to the street (two storeys at most), never through a roof below")]
        public void OffARoofOrOutOfAnUpperWindowOneDropsToTheStreet()
        {
            var s = Building();
            Assert.Equal(Paths.CostOrtho + 2 * Paths.CostDrop, Paths.StepBetween(s, At(7, 4, 2), At(8, 4)));
            Assert.Null(Paths.StepBetween(s, At(7, 1, 2), At(8, 1)));
            // Into the first floor's window from the room, and out of it to the street below (docs/FASE7.md).
            Assert.Equal(Paths.CostOrtho + Paths.CostVault, Paths.StepBetween(s, At(6, 3, 1), At(7, 3, 1)));
            Assert.Equal(Paths.CostOrtho + Paths.CostDrop, Paths.StepBetween(s, At(7, 3, 1), At(8, 3)));
            var jumper = AddUnit(s, "s0", TemplateIds.Assault, 6, 3, 1);
            var out_ = Paths.BuildPath(s, jumper, Paths.ComputeReach(s, jumper, 1000), At(9, 3))!;
            SamePlaces(new[] { At(7, 3, 1), At(8, 3) }, out_.Take(2));
            s.Units = s.Units.Where((o) => o != jumper).ToList();
            // A ladder up the side of a flat one-storey roof (decision 1), and a drop back off its edge.
            Assert.Equal(Paths.CostOrtho + Paths.CostClimb, Paths.StepBetween(s, At(9, 7), At(10, 7, 1)));
            Assert.Equal(Paths.CostOrtho + Paths.CostDrop, Paths.StepBetween(s, At(10, 6, 1), At(10, 5)));
            Assert.Null(Paths.StepBetween(s, At(11, 7, 1), At(11, 7)));
            var u = AddUnit(s, "s1", TemplateIds.Assault, 9, 6);
            Assert.True(Paths.ComputeReach(s, u, 1000).Cost.Has(Grid.NodeOf(s, At(12, 8, 1))));
        }

        [Fact(DisplayName = "moving up and down › the steps from a place are every level of the next tile the rule allows, no more")]
        public void TheStepsFromAPlaceAreEveryLevelTheRuleAllows()
        {
            foreach (var s in new[] { Building(), Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("torre"), Slots = new List<int> { 0 }, Rng = new Rng(1) }) })
            {
                for (var n = 0; n < s.Width * s.Height * Grid.LevelCount(s); n++)
                {
                    var p = Grid.PosOf(s, n);
                    foreach (var d in Grid.Dirs8)
                    {
                        var every = Enumerable.Range(0, Grid.LevelCount(s))
                            .Select((z) => At(p.X + d.X, p.Y + d.Y, z))
                            .Select((to) => (to, cost: Paths.StepBetween(s, p, to)))
                            .Where((x) => x.cost != null)
                            .Select((x) => (x.to.X, x.to.Y, x.to.Z, x.cost!.Value))
                            .ToList();
                        var steps = Paths.StepsFrom(s, p, d.X, d.Y).Select((x) => (x.To.X, x.To.Y, x.To.Z, x.Cost)).ToList();
                        foreach (var e in every) Assert.Contains(e, steps);
                        Assert.Equal(every.Count, steps.Count);
                    }
                }
            }
        }

        [Fact(DisplayName = "moving up and down › maps without storeys move exactly as before: one step per direction, the same costs")]
        public void MapsWithoutStoreysMoveExactlyAsBefore()
        {
            foreach (var seed in new[] { 3, 11 })
            {
                var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, seed), Slots = new List<int> { 0 }, Rng = new Rng(seed) });
                // As a mission saved before storeys had floors.
                s.Levels = null;
                var rng = new Rng(seed);
                for (var k = 0; k < 3000; k++)
                {
                    var p = At(rng.Int(s.Width), rng.Int(s.Height));
                    foreach (var d in Grid.Dirs8)
                    {
                        var old = Paths.StepCost(s, p.X, p.Y, d.X, d.Y);
                        var steps = Paths.StepsFrom(s, p, d.X, d.Y);
                        if (old == null) Assert.Empty(steps);
                        else
                        {
                            Assert.Single(steps);
                            SamePlace(At(p.X + d.X, p.Y + d.Y), steps[0].To);
                            Assert.Equal(old.Value, steps[0].Cost);
                        }
                        // The rule for every level gives the same on the ground.
                        if (s.Tiles[Grid.TileIndex(s, p.X, p.Y)] != TileKinds.Wall) Assert.Equal(old, Paths.StepBetween(s, p, At(p.X + d.X, p.Y + d.Y)));
                    }
                }
            }
        }

        // ------------------------------------------------------------ sight through storeys

        [Fact(DisplayName = "sight through storeys › floors and ceilings block: from the ground floor nobody sees the floor above, nor from the roof the rooms below")]
        public void FloorsAndCeilingsBlock()
        {
            var s = Building();
            Assert.False(Los.LineClear(s, At(2, 4), At(5, 4, 1)));
            Assert.False(Los.LineClear(s, At(5, 4, 1), At(2, 4)));
            Assert.True(Los.LineClear(s, At(3, 3, 1), At(6, 2, 1)));
            Assert.True(Los.LineClear(s, At(4, 3), At(6, 2)));
            Assert.False(Los.LineClear(s, At(4, 3, 2), At(5, 4)));
            Assert.False(Los.LineClear(s, At(4, 3, 2), At(5, 4, 1)));
            // From the roof's edge, the street below is in sight; the roof itself hides what's close under it.
            Assert.True(Los.LineClear(s, At(7, 3, 2), At(12, 3)));
            Assert.False(Los.LineClear(s, At(4, 3, 2), At(9, 3)));
        }

        [Fact(DisplayName = "sight through storeys › a target straight above or below is hidden by the floor between; down a stairwell it is not")]
        public void ATargetStraightAboveOrBelowIsHidden()
        {
            var s = Building();
            Assert.False(Los.LineClear(s, At(3, 3), At(3, 3, 1)));
            Assert.False(Los.LineClear(s, At(3, 3, 1), At(3, 3)));
            Assert.False(Los.LineClear(s, At(11, 7, 1), At(11, 7)));
            // Down the stairwell, from beside it; a step further back, the floor's edge is in the way.
            Assert.True(Los.LineClear(s, At(3, 2, 1), At(2, 2)));
            Assert.False(Los.LineClear(s, At(4, 2, 1), At(2, 2)));
        }

        [Fact(DisplayName = "sight through storeys › windows are holes on their own floor: from the street into the first floor and back out, not through a wall")]
        public void WindowsAreHolesOnTheirOwnFloor()
        {
            var s = Building();
            Assert.True(Los.LineClear(s, At(12, 3), At(6, 3, 1)));
            Assert.True(Los.LineClear(s, At(6, 3, 1), At(12, 3)));
            // Further into the room than beside it, from the street: the glass, not through it (Ana, 23/10 #21).
            Assert.False(Los.LineClear(s, At(12, 3), At(4, 3, 1)));
            Assert.False(Los.LineClear(s, At(4, 3, 1), At(12, 3)));
            var walled = Building();
            walled.Levels![0].Tiles[Grid.TileIndex(walled, 7, 3)] = TileKinds.Wall;
            Assert.False(Los.LineClear(walled, At(12, 3), At(6, 3, 1)));
            Assert.False(Los.LineClear(walled, At(6, 3, 1), At(12, 3)));
            // The ground floor's door, closed, shows nothing; open, its own floor (with a wall over it, nothing upstairs).
            Assert.False(Los.LineClear(walled, At(12, 3), At(5, 3)));
            walled.Tiles[Grid.TileIndex(walled, 7, 3)] = TileKinds.DoorOpen;
            Assert.True(Los.LineClear(walled, At(12, 3), At(5, 3)));
            Assert.False(Los.LineClear(walled, At(9, 3), At(5, 3, 1)));
        }

        [Fact(DisplayName = "sight through storeys › who sees whom: through an upper window both ways, never through a floor")]
        public void WhoSeesWhom()
        {
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
            // From up there the shot has the height: previewShot is aim.ts's, not ported yet.
        }

        [Fact(DisplayName = "sight through storeys › the fog has a layer per level; a floor of the cutaway shows the highest place at or below it")]
        public void TheFogHasALayerPerLevel()
        {
            var s = Building();
            AddUnit(s, "s1", TemplateIds.Assault, 6, 3, 2);
            AddUnit(s, "a1", TemplateIds.Zombie, 13, 0);
            var mask = Los.TeamVisibility(s, Teams.Xcom);
            var area = s.Width * s.Height;
            Assert.Equal(area * 3, mask.Length);
            bool Seen(Pos p) => mask[Grid.NodeOf(s, p)];
            Assert.True(Seen(At(2, 2, 2)));
            Assert.True(Seen(At(12, 3)));
            Assert.False(Seen(At(4, 4)));
            Assert.False(Seen(At(4, 4, 1)));
            // Only slabs are worked out above the ground: open air is never "seen".
            Assert.False(Seen(At(12, 3, 1)));
            // The roof's floor of the cutaway: the roof where there is one, the ground elsewhere.
            var top = Los.FloorView(s, mask, 2);
            Assert.True(top[Grid.TileIndex(s, 4, 4)]);
            Assert.True(top[Grid.TileIndex(s, 12, 3)]);
            // What each floor of the cutaway looks down on: the roof over the building, the ground elsewhere and in the stairwell.
            Assert.Equal(2, Los.FloorLevels(s, 2)[Grid.TileIndex(s, 4, 4)]);
            Assert.Equal(0, Los.FloorLevels(s, 1)[Grid.TileIndex(s, 2, 2)]);
            Assert.Equal(1, Los.FloorLevels(s, 1)[Grid.TileIndex(s, 3, 2)]);
            Assert.Equal(0, Los.FloorLevels(s, 9)[Grid.TileIndex(s, 12, 3)]);
            // The ground floor's: inside, the room nobody sees.
            var ground = Los.FloorView(s, mask, 0);
            Assert.False(ground[Grid.TileIndex(s, 4, 4)]);
            Assert.True(ground[Grid.TileIndex(s, 12, 3)]);
            // Without storeys, the mask is the map's tiles and floorView gives it back.
            var flat = StateFrom(new[] { "....", "...." });
            AddUnit(flat, "s1", TemplateIds.Assault, 0, 0);
            var m = Los.TeamVisibility(flat, Teams.Xcom);
            Assert.Equal(8, m.Length);
            Assert.Equal(m, Los.FloorView(flat, m, 0));
        }

        [Fact(DisplayName = "sight through storeys › a light lights its own floor and what it sees from there: not through the floor below it")]
        public void ALightLightsItsOwnFloor()
        {
            var s = Building();
            s.Mission.Night = true;
            var tile = At(4, 3, 1);
            s.Lights = new List<Light> { new Light { Id = "f1", Tile = tile, Radius = 3, Turns = 2, Tiles = Los.LightTiles(s, tile, 3, 0.5) } };
            Assert.True(Los.IsLit(s, At(5, 3, 1)));
            Assert.False(Los.IsLit(s, At(5, 3)));
            Assert.False(Los.IsLit(s, new Pos { X = 5, Y = 3 }));
        }

        // ------------------------------------------------------------ cover per level

        [Fact(DisplayName = "cover per level › upstairs: the storey's walls and windows are full cover, its furniture as on the ground; a roof's edge gives nothing")]
        public void UpstairsTheStoreysWallsAndWindowsAreFullCover()
        {
            var s = Building();
            s.Levels![0].Tiles[Grid.TileIndex(s, 4, 2)] = TileKinds.Low;
            // Against the east wall on the first floor; beside the window; beside the desk.
            Assert.Equal(new[] { 2, 2, 0, 0 }, Cover.CoverSides(s, At(6, 2, 1)));
            Assert.Equal(new[] { 0, 2, 0, 0 }, Cover.CoverSides(s, At(6, 3, 1)));
            Assert.Equal(1, Cover.CoverSides(s, At(4, 3, 1))[0]);
            // On the roof, the edge is open air; below it, on the ground floor, the same tile has the wall.
            Assert.Equal(new[] { 0, 0, 0, 0 }, Cover.CoverSides(s, At(7, 3, 2)));
            Assert.Equal(2, Cover.CoverSides(s, At(6, 3))[1]);
            // A parapet is half cover; the street beside a building's wall is full, as ever.
            var torre = Setup.CreateMission(new MissionOptions { Map = MapSource.Handmade("torre"), Slots = new List<int> { 0 }, Rng = new Rng(1) });
            Assert.Equal(1, Cover.CoverSides(torre, At(10, 5, 3))[3]);
            Assert.Equal(2, Cover.CoverSides(torre, At(8, 6))[1]);
        }

        [Fact(DisplayName = "cover per level › flanking upstairs is by direction, as on the ground; the step out of cover keeps to the floor")]
        public void FlankingUpstairsIsByDirection()
        {
            var s = Building();
            Assert.Equal(2, Cover.CoverAgainst(s, At(6, 2, 1), new Vec2 { X = 12, Y = 2 }));
            Assert.Equal(0, Cover.CoverAgainst(s, At(6, 2, 1), new Vec2 { X = 2, Y = 4 }));
            // Behind the east wall on the first floor, a soldier steps out along it, on that floor.
            var peeks = Los.PeekOrigins(s, At(6, 2, 1));
            HasPlace(peeks, At(6, 3, 1));
            Assert.True(peeks.All((p) => p.Z == 1));
        }
    }
}
