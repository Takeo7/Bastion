using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G22 (Arturo, 18/11, cuadros 72 y 76: «por lo menos un refugio cada 2 líneas, de momento»; «algo intermedio»): a run
    /// goes home only from a Refuge, at its stop, or with the boss dead; one started before keeps going home from the map.
    /// The map has a Refuge in row 2, one in rows 3–4 past every node of row 2, and row 6's; never two stops in a row.
    /// </summary>
    public class RunHomeTests
    {
        private const string NotHere = RunTexts.EndFromRefuge;

        [Fact]
        public void HomeOnlyFromARefuge()
        {
            var c = Solo();
            var run = Start(c);
            Assert.True(run.HomeFromRefuge);
            // From the map, between stages: no.
            Refused(NotHere, c, new EndRunCommand());
            // At a Trade: no.
            GoTo(c, NodeKinds.Trade);
            Refused(NotHere, c, new EndRunCommand());
            Ok(c, new LeaveStopCommand());
            // At a Refuge: healed, and home or on.
            var s = Squad(c)[0];
            run.Hp[s.Id] = 1;
            GoTo(c, NodeKinds.Refuge);
            Assert.Equal(Runs.MaxHp(s), Runs.HpOf(c, s));
            Assert.Null(Runs.EndRunRefusal(c));
            Ok(c, new EndRunCommand());
            Assert.Null(c.Run);
            Assert.Equal(RunTexts.RunEnded, c.Log[0].Text);
        }

        [Fact]
        public void AtARefugeTwoPlayersVote()
        {
            var c = Two();
            Start(c);
            GoTo(c, NodeKinds.Refuge);
            Ok(c, new EndRunCommand(), 0, Both);
            Assert.IsType<EndRunAction>(c.Proposal!.Action);
            // No: on they go from it.
            Ok(c, new VoteCommand { Accept = false }, 1, Both);
            Assert.NotNull(c.Run);
            Ok(c, new EndRunCommand(), 1, Both);
            Ok(c, new VoteCommand { Accept = true }, 0, Both);
            Assert.Null(c.Run);
        }

        [Fact]
        public void ARunStartedBeforeGoesHomeAsThen()
        {
            // Saved by the 0.13.3 between stages (no RunState.HomeFromRefuge): from the map, as then.
            var c = Solo();
            var run = Start(c);
            run.HomeFromRefuge = null;
            Assert.Null(Runs.EndRunRefusal(c));
            GoTo(c, NodeKinds.Refuge);
            Refused(RunTexts.EndFromMap, c, new EndRunCommand());
            Ok(c, new LeaveStopCommand());
            Ok(c, new EndRunCommand());
            Assert.Null(c.Run);
        }

        [Fact]
        public void ARefugeEveryTwoRows()
        {
            // Cuadro 76 («algo intermedio»): one in row 2; one in rows 3–4 past every node of row 2 (all but where nothing
            // there can be one: no two stops in a row), so in reach by then whatever way the run went; row 6's.
            int second = 0, covered = 0, reach = 0, starts = 0;
            for (var seed = 1; seed <= 1000; seed++)
            {
                var all = RunMap.Generate(1, seed);
                Assert.Contains(all, (n) => n.Row == RunMap.RefugeFirst && n.Kind == NodeKinds.Refuge);
                Assert.Single(all, (n) => n.Row == RunMap.RefugeRow && n.Kind == NodeKinds.Refuge);
                foreach (var n in all.Where((n) => n.Row == RunMap.RefugeFirst))
                {
                    second++;
                    if (Ahead(n, all).Any((x) => x.Kind == NodeKinds.Refuge && x.Row > RunMap.RefugeFirst && x.Row <= RunMap.RefugeSecond)) covered++;
                }
                foreach (var s in all.Where((n) => n.Row == 1))
                {
                    starts++;
                    if (Ahead(s, all).Any((x) => x.Kind == NodeKinds.Refuge && x.Row <= RunMap.RefugeSecond)) reach++;
                }
                // Never two stops in a row (rows 1–5).
                foreach (var n in all.Where((n) => NodeKinds.IsStop(n.Kind) && n.Row < RunMap.RefugeRow))
                    Assert.DoesNotContain(n.Next, (id) => NodeKinds.IsStop(all.First((x) => x.Id == id).Kind) && all.First((x) => x.Id == id).Row < RunMap.RefugeRow);
                // A Rescate in reach from every start still (N3).
                Assert.All(all.Where((n) => n.Row == 1), (s) => Assert.Contains(Ahead(s, all), (x) => x.Kind == NodeKinds.Rescue && x.Row <= RunMap.RescueLastRow));
            }
            Assert.InRange(covered / (double)second, 0.95, 1);
            Assert.InRange(reach / (double)starts, 0.96, 1);
        }

        private static List<RunNode> Ahead(RunNode n, List<RunNode> all)
        {
            var seen = new HashSet<string>();
            var next = new List<RunNode> { n };
            var ahead = new List<RunNode>();
            while (next.Count > 0)
            {
                next = next.SelectMany((x) => x.Next).Distinct().Select((id) => all.First((x) => x.Id == id)).Where((x) => seen.Add(x.Id)).ToList();
                ahead.AddRange(next);
            }
            return ahead;
        }
    }
}
