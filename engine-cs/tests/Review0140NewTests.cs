using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Producto's review of the 0.14.0's engine (23/11): the points whose fix is something new (Setup.OutOfPits, Paths.Cut,
    /// Buildings.Of), and the fights as they are set out now.
    /// </summary>
    public class Review0140NewTests
    {
        /// <summary>The country Saqueos with a hollow of WayOutTests' sweep.</summary>
        private static readonly int[] Hollows = { 845934591, 1616620778, 1772576899 };

        private static GameState Country(int seed) => LaunchAt(NodeKinds.Normal, node: (n) => { n.Site = Places.Country; n.Seed = seed; n.Modifier = null; }).S;

        // ------------------------------------------------------------------- 3. nobody in a pit

        [Fact]
        public void NoneOfTheDeadIsLeftInAPit()
        {
            foreach (var seed in Hollows)
            {
                var s = Country(seed);
                var pits = new HashSet<int>(s.Mission.Pits!);
                Assert.DoesNotContain(s.Units, (u) => u.Alive && pits.Contains(Grid.NodeOf(s, u.Pos)));
                // One put in one: out, somewhere the squad walks to.
                var dead = s.Units.First((u) => u.Team == Teams.Alien && u.Alive);
                dead.Pos = Grid.PosOf(s, s.Mission.Pits![0]);
                Setup.OutOfPits(s);
                Assert.DoesNotContain(Grid.NodeOf(s, dead.Pos), pits);
                var soldier = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null);
                Assert.True(Paths.ComputeReach(s, soldier, 1_000_000, ignore: (_) => true).Cost.Has(Grid.NodeOf(s, dead.Pos)));
            }
        }

        [Fact]
        public void NobodyWalksIntoAPit()
        {
            // The dead too (a pit is shut to everyone): none of its places in any reach.
            var s = Country(Hollows[0]);
            var pits = new HashSet<int>(s.Mission.Pits!);
            foreach (var u in s.Units.Where((x) => x.Alive))
                Assert.DoesNotContain(Paths.ComputeReach(s, u, 1_000_000, ignore: (_) => true).Cost.Keys, (n) => pits.Contains(n));
        }

        // ------------------------------------------------------------------- 2. La Lata, by construction

        [Fact]
        public void EveryFightIsLaunchedWithAWayToLaLata()
        {
            // RunFights.Launch makes a map again (the next seed) when the squad can't walk to La Lata from where it comes in.
            foreach (var n in WayOutTests.Fights(1, 1))
            {
                var s = WayOutTests.Launch(n, 1);
                Assert.NotEmpty(s.Mission.EvacZone);
                Assert.Empty(Paths.Cut(s));
            }
        }

        [Fact]
        public void LaLataByItsDoorsIsWhereTheSquadGets()
        {
            // Red's R2 of 23/11: a Saqueadores' camping (gen-camping-420098296) whose van stands among the trees. The zone drawn
            // first had a way to it; moved to the van's doors (SeatZone) it was walled in by the trees, and the fight was
            // launched with no way out. Looked at once by the doors, the map is made again.
            var (_, s) = LaunchAt(NodeKinds.Raiders, node: (n) => { n.Site = Places.Camping; n.Seed = 420098296; n.Difficulty = 3; });
            Assert.NotEmpty(s.Mission.EvacZone);
            Assert.Empty(Paths.Cut(s));
            Assert.True(WayOutTests.WaysOut(s).Ok);
        }

        // ------------------------------------------------------------------ 9. the survivor's new place

        [Fact]
        public void TheSurvivorStaysInItsBuildingWhereTheSquadGets()
        {
            foreach (var site in new[] { Places.Town, Places.City, Places.Hospital, Places.Offices, Places.Police, Places.Country, Places.Roadside })
                for (var seed = 1; seed <= 6; seed++)
                {
                    var (_, s) = LaunchAt(NodeKinds.Rescue, seed, node: (n) => { n.Site = site; n.Seed = 700 + seed; n.Modifier = null; });
                    var vip = Grid.GetUnit(s, s.Mission.Survivor!)!;
                    var soldier = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null);
                    var walk = Paths.ComputeReach(s, soldier, 1_000_000, ignore: (_) => true);
                    Assert.True(walk.Cost.Has(Grid.NodeOf(s, vip.Pos)), $"{site} {seed}: {vip.Pos.X},{vip.Pos.Y},{vip.Pos.Z}");
                    Assert.False(Rules.DoorSpot(s, vip.Pos), $"{site} {seed}");
                    Assert.True(s.Mission.Pits == null || !s.Mission.Pits.Contains(Grid.NodeOf(s, vip.Pos)));
                }
        }
    }
}
