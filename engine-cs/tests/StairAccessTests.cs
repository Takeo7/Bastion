using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using Xunit.Sdk;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/stairAccess.test.ts, test by test (StairAccess.RepairStairAccess and Setup.NormalizeMission).
    /// Nothing left out.
    /// </summary>
    public class StairAccessTests
    {
        private static readonly string[] Rows = { "#######", "#.....#", "#.....#", "#.....#", "#.....#", "#.....#", "#######" };

        /// <summary>A saved two-storey house, with an old flight against one outer wall.</summary>
        private static (GameState s, Pos at, int i) House(int up)
        {
            var rows = Rows;
            var s = WithLevels(StateFrom(rows), new[] { rows, rows.Select((row) => row.Replace('.', 'R')).ToArray() });
            s.MapId = "gen-city-saved";
            s.Ground = s.Tiles.Select((_) => 2).ToList();
            s.Storeys = s.Tiles.Select((_) => 2).ToList();
            s.Decor = s.Tiles.Select((_) => 0).ToList();
            var d = Grid.Dirs4[up];
            var at = P(3 - d.X * 2, 3 - d.Y * 2, 0);
            var i = at.Y * s.Width + at.X;
            s.Tiles[i] = TileKinds.Ladder;
            s.Decor[i] = Decor.DecorCode(DecorKinds.Stairs, up);
            s.Levels![0].Tiles[i] = null;
            return (s, at, i);
        }

        /// <summary>toEqual on anything: the same JSON, null and missing alike.</summary>
        private static void Same(object? expected, object? actual)
        {
            var diff = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "");
            if (diff != null) throw new XunitException(diff);
        }

        private static bool IsStairs(int d)
        {
            return Decor.DecorOf(d)?.Kind == DecorKinds.Stairs;
        }

        // ------------------------------------------------------------ saved stairs

        [Fact(DisplayName = "saved stairs › moves wall-adjacent flights in all four directions and keeps both floors accessible")]
        public void MovesWallAdjacentFlightsInAllFourDirections()
        {
            for (var up = 0; up < 4; up++)
            {
                var (s, at, i) = House(up);
                var soldier = AddUnit(s, "s1", TemplateIds.Assault, at.X, at.Y);
                s.Dropped = new List<DroppedLoot> { new DroppedLoot { Id = "drop", Pos = P(3, 3, 1), Loot = new Loot { Medicines = 1 } } };
                var beforeUnits = Events.CloneState(s).Units;
                var beforeMission = Events.CloneState(s).Mission;
                var oldRoom = Looting.RoomAt(s, P(3, 3, 1))!;
                s.RoomsSearched = new List<int> { oldRoom.Id };
                Setup.NormalizeMission(s);
                Assert.Equal(TileKinds.Floor, s.Tiles[i]);
                Assert.True(Grid.SlabAt(s, at.X, at.Y, 1));
                var j = s.Decor!.FindIndex(IsStairs);
                Assert.NotEqual(i, j);
                var p = Grid.PosOf(s, j);
                var facing = Decor.DecorOf(s.Decor[j])!.Facing;
                var d = Grid.Dirs4[facing];
                var foot = P(p.X - d.X, p.Y - d.Y, 0);
                var landing = P(p.X + d.X, p.Y + d.Y, 1);
                foreach (var side in Grid.Dirs4) foreach (var z in new[] { 0, 1 }) Assert.Equal(TileKinds.Floor, Grid.KindAt(s, P(p.X + side.X, p.Y + side.Y, z)));
                Assert.Equal(TileKinds.Floor, Grid.KindAt(s, foot));
                Assert.False(Grid.SlabAt(s, p.X, p.Y, 1));
                Assert.Equal(Paths.CostOrtho, Paths.StepBetween(s, foot, p));
                Assert.Equal(Paths.CostOrtho + Paths.CostClimb, Paths.StepBetween(s, p, landing));
                Assert.Equal(Paths.CostOrtho + Paths.CostClimb, Paths.StepBetween(s, landing, p));
                Assert.Contains(Looting.RoomAt(s, landing)!.Id, s.RoomsSearched);
                Same(beforeUnits, s.Units);
                Same(beforeMission, s.Mission);
                SamePlace(at, soldier.Pos);
                Assert.Equal(TileKinds.Floor, Grid.KindAt(s, s.Dropped[0].Pos));
                var @fixed = Events.CloneState(s);
                Setup.NormalizeMission(s);
                Same(@fixed, s);
            }
        }

        [Fact(DisplayName = "saved stairs › chooses another stairwell when a unit or the objective occupies the nearest one")]
        public void ChoosesAnotherStairwell()
        {
            var (s, _, _) = House(1);
            var soldier = AddUnit(s, "s1", TemplateIds.Assault, 2, 3, 1);
            s.Mission.Item = new MissionItem { Pos = P(2, 2, 1), Carrier = null, Evacuated = false };
            StairAccess.RepairStairAccess(s);
            Assert.Equal(TileKinds.Floor, Grid.KindAt(s, soldier.Pos));
            Assert.Equal(TileKinds.Floor, Grid.KindAt(s, s.Mission.Item!.Pos!));
            Assert.True(s.Decor!.Where((d, i) => IsStairs(d) && i != 3 * s.Width + 1).Any());
        }

        [Fact(DisplayName = "saved stairs › repairs a flight starting on an upper floor as well as on the ground")]
        public void RepairsAFlightStartingOnAnUpperFloor()
        {
            var (s, _, i) = House(1);
            var rows = Rows;
            var above = WithLevels(StateFrom(rows), new[] { rows, rows, rows }).Levels!;
            s.Levels = above;
            s.Tiles[i] = TileKinds.Floor;
            s.Decor![i] = 0;
            s.Levels[0].Tiles[i] = TileKinds.Ladder;
            s.Levels[0].Decor = s.Tiles.Select((_) => 0).ToList();
            s.Levels[0].Decor![i] = Decor.DecorCode(DecorKinds.Stairs, 1);
            s.Levels[1].Tiles[i] = null;
            StairAccess.RepairStairAccess(s);
            Assert.Equal(TileKinds.Floor, s.Levels[0].Tiles[i]);
            Assert.Equal(TileKinds.Floor, s.Levels[1].Tiles[i]);
            var j = s.Levels[0].Decor!.FindIndex(IsStairs);
            var at = Grid.PosOf(s, j);
            var p = P(at.X, at.Y, 1);
            var d = Grid.Dirs4[Decor.DecorOf(s.Levels[0].Decor![j])!.Facing];
            var landing = P(p.X + d.X, p.Y + d.Y, 2);
            Assert.Null(s.Levels[1].Tiles[j]);
            Assert.Equal(Paths.CostOrtho + Paths.CostClimb, Paths.StepBetween(s, p, landing));
            Assert.Equal(Paths.CostOrtho + Paths.CostClimb, Paths.StepBetween(s, landing, p));
        }

        [Fact(DisplayName = "saved stairs › leaves a cramped saved room and handmade maps intact when no safe relocation fits")]
        public void LeavesACrampedSavedRoomAndHandmadeMapsIntact()
        {
            var s = WithLevels(StateFrom(new[] { "#####", "#S..#", "#####" }), new[] { new[] { "#####", "# ..#", "#####" } });
            s.MapId = "gen-city-cramped";
            s.Ground = s.Tiles.Select((_) => 2).ToList();
            s.Storeys = s.Tiles.Select((_) => 2).ToList();
            s.Decor = s.Tiles.Select((_) => 0).ToList();
            s.Decor[Grid.NodeOf(s, P(1, 1, 0))] = Decor.DecorCode(DecorKinds.Stairs, 1);
            var before = Events.CloneState(s);
            StairAccess.RepairStairAccess(s);
            Same(before, s);
            var handmade = House(1).s;
            handmade.MapId = "torre";
            var original = Events.CloneState(handmade);
            StairAccess.RepairStairAccess(handmade);
            Same(original, handmade);
        }
    }
}
