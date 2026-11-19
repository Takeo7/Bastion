using System;
using System.IO;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// A run's day sky (Arturo, 19/11, cuadro 83: «Varios, según la etapa»; «Sí, solo aspecto»; .dev-snapshots/gameplay/g23/
    /// CIELO.md): MissionState.Sky, RunMap.SkyOf its node's, by the map's seed; none at night, overcast in a storm, else
    /// clear, overcast or dusk, 50/30/20. Look only: no rule reads it.
    /// </summary>
    public class RunSkyTests
    {
        [Fact]
        public void TheSameNodeTheSameSkyInItsFight()
        {
            foreach (var seed in Enumerable.Range(1, 8))
            {
                RunNode? node = null;
                var (_, s) = LaunchAt(NodeKinds.Normal, seed, node: (n) =>
                {
                    n.Modifier = null;
                    n.Added = null;
                    node = n;
                });
                Assert.NotNull(s.Mission.Sky);
                Assert.Contains(s.Mission.Sky, Skies.All);
                Assert.Equal(RunMap.SkyOf(node!), s.Mission.Sky);
                Assert.Equal(s.Mission.Sky, RunMap.SkyOf(node!));
                // With the state, to the partner and in a save.
                Assert.Equal(s.Mission.Sky, Events.CloneState(s).Mission.Sky);
                Assert.Equal(s.Mission.Sky, Bastion.Net.Json.Read<GameState>(Bastion.Net.Json.Write(s)).Mission.Sky);
            }
        }

        [Fact]
        public void AtNightNoneInAStormOvercast()
        {
            var n = new RunNode { Id = "n2-0", Row = 2, Kind = NodeKinds.Normal, Seed = 12345 };
            Assert.NotNull(RunMap.SkyOf(n));
            n.Modifier = Modifiers.Night;
            Assert.Null(RunMap.SkyOf(n));
            n.Modifier = Modifiers.Storm;
            Assert.Equal(Skies.Overcast, RunMap.SkyOf(n));
            // An event's (RunNode.Added) as the node's own.
            n.Modifier = null;
            n.Added = Modifiers.Night;
            Assert.Null(RunMap.SkyOf(n));
            n.Added = Modifiers.Storm;
            Assert.Equal(Skies.Overcast, RunMap.SkyOf(n));
            // A night fight launched: none.
            var (_, s) = LaunchAt(NodeKinds.Normal, 3, node: (x) => x.Modifier = Modifiers.Night);
            Assert.True(s.Mission.Night);
            Assert.Null(s.Mission.Sky);
            // No stop has one.
            Assert.Null(RunMap.SkyOf(new RunNode { Id = "n6-0", Row = 6, Kind = NodeKinds.Refuge, Seed = 7 }));
        }

        [Fact]
        public void FiftyThirtyTwenty()
        {
            int clear = 0, overcast = 0, dusk = 0;
            foreach (var seed in Enumerable.Range(1, 2000))
                foreach (var n in RunMap.Generate(1, seed).Where((x) => RunMap.IsFight(x.Kind) && x.Modifier == null))
                {
                    var sky = RunMap.SkyOf(n);
                    if (sky == Skies.Clear) clear++;
                    else if (sky == Skies.Overcast) overcast++;
                    else if (sky == Skies.Dusk) dusk++;
                    else Assert.Fail($"{n.Id}: {sky}");
                }
            double all = clear + overcast + dusk;
            Assert.InRange(clear / all, 0.47, 0.53);
            Assert.InRange(overcast / all, 0.27, 0.33);
            Assert.InRange(dusk / all, 0.17, 0.23);
        }

        [Fact]
        public void AFightSavedBeforeHasNone()
        {
            // Saved by the 0.13.2 a turn in: today's sky (null).
            var s = Bastion.Net.Json.Read<GameState>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "g19", "comisaria-0132.json")));
            Assert.Null(s.Mission.Sky);
            // (Out of the runs none either: no recorded mission of step1–3 has it, RecorderTests.)
        }
    }
}
