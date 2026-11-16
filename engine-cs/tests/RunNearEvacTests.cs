using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using Xunit.Abstractions;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// La Lata near the squad on an expedition's normal node (Gameplay, 16/11; GenOptions.EvacNear, Mapgen.NearWalk): on the
    /// squad's half at a moderate walk, where it was at the far end of the map. Measured on every place, against the map as it
    /// was (an Asedio's: the same map, La Lata where the map puts it).
    /// </summary>
    public class RunNearEvacTests
    {
        private readonly ITestOutputHelper output;

        public RunNearEvacTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>Steps on foot on the ground (every way, as the generator counts them) from the squad to La Lata's nearest tile; -1 if none.</summary>
        public static int Walk(GameState s)
        {
            var steps = Enumerable.Repeat(-1, s.Width * s.Height).ToArray();
            var queue = new Queue<int>();
            foreach (var u in s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null))
            {
                var i = u.Pos.Y * s.Width + u.Pos.X;
                if (steps[i] >= 0) continue;
                steps[i] = 0;
                queue.Enqueue(i);
            }
            var zone = new HashSet<int>(s.Mission.EvacZone.Select((z) => (int)z.Y * s.Width + (int)z.X));
            while (queue.Count > 0)
            {
                var i = queue.Dequeue();
                if (zone.Contains(i)) return steps[i];
                int x = i % s.Width, y = i / s.Width;
                foreach (var d in Grid.Dirs8)
                {
                    if (Paths.StepCost(s, x, y, d.X, d.Y) == null) continue;
                    var j = (y + d.Y) * s.Width + x + d.X;
                    if (steps[j] >= 0) continue;
                    steps[j] = steps[i] + 1;
                    queue.Enqueue(j);
                }
            }
            return -1;
        }

        /// <summary>The places a normal node is drawn in, with map seeds that give each (a town or a city by its seed: Places.OldTownSeed).</summary>
        public static readonly IReadOnlyList<(string Site, int[] Seeds)> Sites = new[]
        {
            (Places.Town, new[] { 0, 1, 2, 6, 7, 8, 12, 13 }),
            (Places.City, new[] { 3, 4, 5, 9, 10, 11, 15, 16 }),
            (Places.Estate, new[] { 101, 102, 103, 104, 105, 106, 107, 108 }),
            (Places.Factory, new[] { 201, 202, 203, 204, 205, 206, 207, 208 }),
            (Places.Roadside, new[] { 301, 302, 303, 304, 305, 306, 307, 308 }),
            (Places.Country, new[] { 401, 402, 403, 404, 405, 406, 407, 408 }),
        };

        [Fact]
        public void LaLataNearOnANormalNode()
        {
            var all = new List<int>();
            foreach (var (site, seeds) in Sites)
            {
                var near = new List<int>();
                var far = new List<int>();
                foreach (var seed in seeds)
                {
                    void Map(RunNode n)
                    {
                        n.Site = site;
                        n.Seed = seed;
                    }
                    var (_, s) = LaunchAt(NodeKinds.Normal, node: Map);
                    var (_, before) = LaunchAt(NodeKinds.Hold, node: Map);
                    Assert.Equal(site, Places.Of(RunMap.MapOf(new RunNode { Kind = NodeKinds.Normal, Site = site, Seed = seed })!));
                    Assert.Equal(MissionKinds.Scavenge, s.Mission.Kind);
                    Assert.NotEmpty(s.Mission.EvacZone);
                    // Its bus parked by it.
                    Assert.True(s.Mission.EvacBus != null && s.Mission.EvacBus.Tiles.Count > 0, $"{site} {seed}: no bus");
                    var w = Walk(s);
                    var was = Walk(before);
                    Assert.True(w > 0, $"{site} {seed}: La Lata out of reach");
                    // Never further than it was, but where it was near already.
                    Assert.True(w <= Math.Max(was, Mapgen.NearWalk[1] + 5), $"{site} {seed}: {w} > {was}");
                    near.Add(w);
                    far.Add(was);
                }
                all.AddRange(near);
                output.WriteLine($"{site}: ahora {string.Join(", ", near)} (media {near.Average():0.#}); antes {string.Join(", ", far)} (media {far.Average():0.#})");
            }
            // Moderate: most inside NearWalk give or take a few, none far.
            Assert.True(all.Count((w) => w >= Mapgen.NearWalk[0] - 5 && w <= Mapgen.NearWalk[1] + 5) >= all.Count * 3 / 4, string.Join(", ", all));
            Assert.All(all, (w) => Assert.True(w <= 40, string.Join(", ", all)));
        }

        [Fact]
        public void OnlyANormalNodesNear()
        {
            // An Asedio, a Travesía and the rest: where the map puts it, as before.
            void Map(RunNode n)
            {
                n.Site = Places.Roadside;
                n.Seed = 301;
            }
            var (_, crossing) = LaunchAt(NodeKinds.Crossing, node: Map);
            var (_, hold) = LaunchAt(NodeKinds.Hold, node: Map);
            var (_, normal) = LaunchAt(NodeKinds.Normal, node: Map);
            Assert.Equal(hold.Mission.EvacZone.Select((z) => (z.X, z.Y)), crossing.Mission.EvacZone.Select((z) => (z.X, z.Y)));
            Assert.NotEqual(hold.Mission.EvacZone.Select((z) => (z.X, z.Y)), normal.Mission.EvacZone.Select((z) => (z.X, z.Y)));
        }
    }
}
