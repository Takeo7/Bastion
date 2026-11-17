using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Red, R3 (17/11): in a town a police car's boot stood walled in (a lot the outline shut up, its cars left in the wall),
    /// searchable and out of everyone's reach. In a run's fight, what there is to search is within reach on foot.
    /// </summary>
    public class RunReachTests
    {
        /// <summary>Whether a soldier gets beside `p` on foot from the squad and reaches it with the hands (Looting.ReachesToSearch).</summary>
        private static bool Searchable(GameState s, HashSet<int> reach, Pos p)
        {
            return Grid.Dirs8.Any((d) =>
            {
                var from = new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z };
                return Grid.InBounds(s, from.X, from.Y) && reach.Contains(Grid.NodeOf(s, from)) && Looting.ReachesToSearch(s, from, p);
            });
        }

        [Fact]
        public void RedsWalledInBoot()
        {
            // The map of R2's seed 2, 2nd expedition, n2-0 (maletero-emparedado.json): a lot the outline shut up, four cars
            // left whole in its wall, their boots at x 7, y 3, 5, 7 and 9. None to search, none a police car.
            var (_, s) = RunInteriorTests.At(Places.Town, NodeKinds.Elimination, 1526191232);
            Assert.Equal("gen-city-1526191232", s.MapId);
            foreach (var y in new[] { 3, 5, 7, 9 })
            {
                var boot = new Pos { X = 7, Y = y, Z = 0 };
                Assert.Equal(TileKinds.Low, Grid.KindAt(s, boot));
                Assert.NotNull(Looting.BootOf(s, Grid.TileIndex(s, boot.X, boot.Y)));
                Assert.Contains(Grid.NodeOf(s, boot), s.Mission.OutOfReach!);
                Assert.Null(Looting.SearchSpot(s, boot));
                Assert.False(Looting.IsSearchable(s, boot));
                Assert.DoesNotContain(Grid.TileIndex(s, boot.X, boot.Y), s.Mission.PoliceCar ?? new List<int>());
            }
            // A copy of the state keeps it.
            Assert.Equal(s.Mission.OutOfReach, Events.CloneState(s).Mission.OutOfReach);
        }

        [Fact]
        public void EveryBootAndEverySpotToSearchWithinReach()
        {
            var boots = 0;
            var spots = 0;
            foreach (var place in new[] { Places.Town, Places.City })
            {
                foreach (var kind in new[] { NodeKinds.Normal, NodeKinds.Elimination })
                {
                    for (var seed = 1; seed <= 40; seed++)
                    {
                        var (_, s) = RunInteriorTests.At(place, kind, seed);
                        var soldier = s.Units.First((u) => u.Team == Teams.Xcom && u.Alive);
                        var reach = new HashSet<int>(Paths.ComputeReach(s, soldier, 1_000_000, ignore: (_) => true).Cost.Keys);
                        var pc = s.Mission.PoliceCar ?? new List<int>();
                        for (var k = 1; k < pc.Count; k += 2)
                        {
                            Assert.True(Searchable(s, reach, Grid.PosOf(s, pc[k])), $"{place} {kind} {seed}: the police boot at {Grid.PosOf(s, pc[k]).X},{Grid.PosOf(s, pc[k]).Y}");
                            boots++;
                        }
                        var area = s.Width * s.Height;
                        for (var z = 0; z <= (s.Levels?.Count ?? 0); z++)
                        {
                            for (var i = 0; i < area; i++)
                            {
                                var p = new Pos { X = i % s.Width, Y = i / s.Width, Z = z };
                                if (!Looting.IsSearchable(s, p)) continue;
                                Assert.True(Searchable(s, reach, p), $"{place} {kind} {seed}: {Looting.SearchSpot(s, p)} at {p.X},{p.Y},{p.Z}");
                                spots++;
                            }
                        }
                    }
                }
            }
            Assert.True(boots > 20 && spots > 500, $"{boots} boots, {spots} spots");
        }
    }
}
