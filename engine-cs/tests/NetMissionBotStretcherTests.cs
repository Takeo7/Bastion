using System.Collections.Generic;
using System.Linq;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The bot with La Camilla's downed (C6 of the 0.14.11's review; Bots, 26/11): it never takes them for one who walks, so a
    /// fight with one always ends; with all the rest in La Lata, it calls it (the downed, left out, dies: Rules). It doesn't
    /// carry them: carrying them, as Gameplay's probe did (notas-26-11/camilla), saved 6 % of them and lost the whole squad
    /// more often. Without the Camilla nothing changes (the same orders: .dev-snapshots/bots/identidad-c6).
    /// </summary>
    public class NetMissionBotStretcherTests
    {
        private static void Up(CampaignState c) => (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Stretcher] = 1;

        /// <summary>A Saqueo of row 1 with the Camilla, alone, its first soldier felled at once: the fight and the downed.</summary>
        private static (GameState S, Unit Down) Felled(int seed)
        {
            var (_, s) = LaunchAt(NodeKinds.Normal, seed, Up);
            var first = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive);
            NewSim(s).Damage(first, 99, 0);
            Assert.True(first.Downed);
            return (s, first);
        }

        /// <summary>The bot alone (slot 0) to the fight's end, or 3000 orders: those refused.</summary>
        private static int Play(GameState s, int seed)
        {
            var rng = new Rng(seed * 7919 + 13);
            int orders = 0, refused = 0, idle = 0;
            while (s.Outcome == null && orders < 3000 && idle < 3)
            {
                var cmd = MissionBot.Next(s, 0, Zero);
                if (cmd == null)
                {
                    idle++;
                    continue;
                }
                orders++;
                if (Rules.ExecuteCommand(s, cmd, 0, Zero, rng).Ok) idle = 0;
                else
                {
                    refused++;
                    idle++;
                }
            }
            return refused;
        }

        [Fact(DisplayName = "camilla › a fight with the downed on the floor always ends, every order taken")]
        public void TheFightEnds()
        {
            foreach (var seed in Enumerable.Range(1, 8))
            {
                var (s, _) = Felled(seed);
                Assert.Equal(0, Play(s, seed));
                Assert.NotNull(s.Outcome);
            }
        }

        [Fact(DisplayName = "camilla › with all the rest in La Lata and the downed out, the bot calls it")]
        public void AllInItCallsLaLata()
        {
            // The rest of the squad in La Lata (as BoardedEvent leaves them), the downed out: La Lata is called (taken for one
            // who walks, it waited for them for ever), and goes; the downed, left out, dies (Rules: LeftBehind).
            var (s, down) = Felled(3);
            foreach (var u in s.Units.Where((x) => x.Team == Teams.Xcom && x.CampaignId != null && x.Id != down.Id && x.Alive).ToList())
            {
                u.Pos = Rules.OpenTiles(s).Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).First((p) => Grid.UnitAt(s, p) == null);
                u.Aboard = true;
                u.Alive = false;
                u.Evacuated = true;
            }
            s.Seq++;
            Assert.IsType<EvacCommand>(MissionBot.Next(s, 0, Zero));
            Assert.Equal(0, Play(s, 3));
            Assert.NotNull(s.Outcome);
            Assert.False(down.Alive && !down.Evacuated, "the downed still alive out of La Lata");
        }
    }
}
