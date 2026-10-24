using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/flights.test.ts, the tests that use only step 1's modules (flights, grid, los, path): the
    /// rest need rules.ts, events.ts or setup.ts. One line of «is no place to stop» is left out: riseSpot is abilities.ts's.
    /// </summary>
    public class FlightsStep1Tests
    {
        /// <summary>
        /// A two-storey hall (x 1–12, y 1–5) with a flight of three up its middle row, climbing east: its tiles (4,3), (5,3)
        /// and (6,3), its foot (3,3), its landing (7,3) on the first floor, the stairwell over the flight. A door at (12,3)
        /// to the street.
        /// </summary>
        private static GameState Hall(int length = 3)
        {
            var s = StateFrom(new[]
            {
                "..............",
                ".############.",
                ".#..........#.",
                ".#..........+.",
                ".#..........#.",
                ".############.",
                "..............",
            });
            WithLevels(s, new[]
            {
                new[]
                {
                    "              ",
                    " ############ ",
                    " #..........# ",
                    " #..........# ",
                    " #..........# ",
                    " ############ ",
                    "              ",
                },
            });
            s.Decor = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            for (var n = 0; n < length; n++)
            {
                var i = Grid.TileIndex(s, 6 - n, 3);
                s.Tiles[i] = TileKinds.Ladder;
                s.Decor[i] = Decor.DecorCode(n == 0 ? DecorKinds.Stairs : DecorKinds.Part, 1);
                s.Levels![0].Tiles[i] = null;
            }
            return s;
        }

        [Fact(DisplayName = "a flight of stairs of several tiles › is its tiles in a line, its foot behind the lowest and its landing in front of the top, a storey up")]
        public void IsItsTilesInALine()
        {
            var s = Hall();
            foreach (var (x, step) in new[] { (4, 0), (5, 1), (6, 2) })
            {
                var on = Flights.FlightAt(s, P(x, 3))!;
                Assert.Equal(step, on.Step);
                Assert.Equal(0, on.Flight.Z);
                Assert.Equal(1, on.Flight.Up);
                Assert.Equal(new[] { (4, 3), (5, 3), (6, 3) }, on.Flight.Tiles.Select((t) => (t.X, t.Y)));
            }
            SamePlace(P(3, 3), Flights.FlightFoot(Flights.FlightAt(s, P(5, 3))!.Flight));
            SamePlace(P(7, 3, 1), Flights.FlightLanding(Flights.FlightAt(s, P(5, 3))!.Flight));
            Assert.Null(Flights.FlightAt(s, P(3, 3)));
            Assert.True(Flights.HasLongFlights(s));
            // The stairwell over all of it.
            foreach (var x in new[] { 4, 5, 6 }) Assert.False(Grid.SlabAt(s, x, 3, 1));
        }

        private static void Steps(IEnumerable<(Pos to, int cost)> expected, List<PathStep> actual)
        {
            Assert.Equal(expected.Select((e) => (e.to.X, e.to.Y, e.to.Z, e.cost)), actual.Select((a) => (a.To.X, a.To.Y, a.To.Z, a.Cost)));
        }

        [Fact(DisplayName = "a flight of stairs of several tiles › is entered only from its foot, the way it climbs, and left only by its landing or its foot")]
        public void IsEnteredOnlyFromItsFoot()
        {
            var s = Hall();
            // Foot to landing: a tile's walk each step, the one up onto the landing too (no climb on top).
            Assert.Equal(Paths.CostOrtho, Paths.StepBetween(s, P(3, 3), P(4, 3)));
            Assert.Equal(Paths.CostOrtho, Paths.StepBetween(s, P(4, 3), P(5, 3)));
            Assert.Equal(Paths.CostOrtho, Paths.StepBetween(s, P(5, 3), P(6, 3)));
            Assert.Equal(Paths.CostOrtho, Paths.StepBetween(s, P(6, 3), P(7, 3, 1)));
            // And back down.
            Assert.Equal(Paths.CostOrtho, Paths.StepBetween(s, P(7, 3, 1), P(6, 3)));
            Assert.Equal(Paths.CostOrtho, Paths.StepBetween(s, P(6, 3), P(5, 3)));
            Assert.Equal(Paths.CostOrtho, Paths.StepBetween(s, P(4, 3), P(3, 3)));
            // Never from a side, nor diagonally, nor across a banister's corner.
            foreach (var x in new[] { 4, 5, 6 })
            {
                Assert.Null(Paths.StepBetween(s, P(x, 2), P(x, 3)));
                Assert.Null(Paths.StepBetween(s, P(x, 3), P(x, 4)));
            }
            Assert.Null(Paths.StepBetween(s, P(3, 2), P(4, 3)));
            Assert.Null(Paths.StepBetween(s, P(4, 3), P(5, 4)));
            Assert.Null(Paths.StepBetween(s, P(4, 2), P(3, 3)));
            Assert.Equal(Paths.CostDiag, Paths.StepBetween(s, P(2, 2), P(3, 3)));
            // Not onto the top from in front of it on its own floor, nor off it there; not off the bottom onto the landing's floor.
            Assert.Null(Paths.StepBetween(s, P(7, 3), P(6, 3)));
            Assert.Null(Paths.StepBetween(s, P(6, 3), P(7, 3)));
            Assert.Null(Paths.StepBetween(s, P(4, 3), P(3, 3, 1)));
            // Upstairs, nobody drops into the stairwell from beside it; only the landing leads down.
            foreach (var x in new[] { 4, 5, 6 }) Assert.Empty(Paths.StepsFrom(s, P(x, 2, 1), 0, 1));
            Assert.Empty(Paths.StepsFrom(s, P(3, 3, 1), 1, 0));
            Steps(new[] { (P(6, 3), Paths.CostOrtho) }, Paths.StepsFrom(s, P(7, 3, 1), -1, 0));
        }

        [Fact(DisplayName = "a flight of stairs of several tiles › costs as four tiles from its foot to its landing (three for a flight of two): one more than a flight of one")]
        public void CostsAsFourTilesFromItsFootToItsLanding()
        {
            foreach (var (length, tiles) in new[] { (3, 4), (2, 3) })
            {
                var s = Hall(length);
                var foot = Flights.FlightFoot(Flights.FlightAt(s, P(6, 3))!.Flight);
                var u = AddUnit(s, "s1", TemplateIds.Assault, foot.X, foot.Y);
                Assert.Equal(tiles * Paths.CostOrtho, Paths.ComputeReach(s, u, 100).Cost.Get(Grid.NodeOf(s, P(7, 3, 1)), -1));
            }
            // A flight of one tile (of before), from the floor in front of its foot: a tile and a climb.
            var old = Hall(1);
            var v = AddUnit(old, "s1", TemplateIds.Assault, 5, 3);
            Assert.Equal(2 * Paths.CostOrtho + Paths.CostClimb, Paths.ComputeReach(old, v, 100).Cost.Get(Grid.NodeOf(old, P(7, 3, 1)), -1));
        }

        [Fact(DisplayName = "a flight of stairs of several tiles › is no place to stop: never a destination, nor a path end, a step-out or a place to rise or drop things")]
        public void IsNoPlaceToStop()
        {
            var s = Hall();
            var u = AddUnit(s, "s1", TemplateIds.Assault, 2, 3);
            var reach = Paths.ComputeReach(s, u, 100);
            // Walked across (the way up passes it), never stopped on.
            foreach (var x in new[] { 4, 5, 6 })
            {
                Assert.True(reach.Cost.Has(Grid.NodeOf(s, P(x, 3))));
                Assert.False(Flights.CanStopAt(s, P(x, 3)));
                Assert.Null(Paths.BuildPath(s, u, reach, P(x, 3)));
            }
            var ends = Paths.Destinations(s, u, reach).Select((d) => d.Pos).ToList();
            Assert.DoesNotContain(ends, (p) => Flights.MidFlight(s, p));
            HasPlace(ends, P(3, 3));
            HasPlace(ends, P(7, 3, 1));
            SamePlaces(new[] { P(3, 3), P(4, 3), P(5, 3), P(6, 3), P(7, 3, 1), P(8, 3, 1) }, Paths.BuildPath(s, u, reach, P(8, 3, 1)));
            // A move that can't reach the landing ends short of the flight: its foot is as far as it goes.
            var @short = Paths.ComputeReach(s, u, 3 * Paths.CostOrtho);
            Assert.Empty(Paths.Destinations(s, u, @short).Select((d) => d.Pos).Where((p) => p.X > 3 && p.Y == 3));
            // What a soldier falling on it carried lands at its nearer end.
            SamePlace(P(3, 3), Flights.RestingPlace(s, P(4, 3)));
            SamePlace(P(7, 3, 1), Flights.RestingPlace(s, P(6, 3)));
            // A body on it rises at its nearer free end: abilities.ts riseSpot, not ported yet.
        }

        [Fact(DisplayName = "a flight of stairs of several tiles › upstairs one looks down the stairwell, the whole of its length; the floor round it hides the hall below")]
        public void UpstairsOneLooksDownTheStairwell()
        {
            var s = Hall();
            var up = AddUnit(s, "s1", TemplateIds.Assault, 5, 2, 1);
            Assert.True(Los.CanSee(s, up, AddUnit(s, "a1", TemplateIds.Zombie, 3, 3, 0, (u) => u.Facing = 1)));
            Assert.False(Los.CanSee(s, up, AddUnit(s, "a2", TemplateIds.Zombie, 10, 2, 0, (u) => u.Facing = 1)));
        }
    }
}
