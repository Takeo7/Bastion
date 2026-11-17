using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The campaign of before stays parked (Gameplay, 16/11: it goes in the next version): a campaign of runs (version 28)
    /// never reaches it. Played as the room plays it (EngineCampaignRules), its fields of before (the days, the Marea, the
    /// operations, the base, the siege…) are as they were, and every order of before is refused.
    /// </summary>
    public class RunsOldCampaignTests
    {
        /// <summary>What a campaign of runs has of its own, by its JSON name: the rest is the campaign of before.</summary>
        private static readonly HashSet<string> RunsFields = new HashSet<string>
        {
            "version", "players", "soldiers", "squad", "proposal", "log", "lastReport", "uid", "nextId",
            "stash", "upgrades", "chapter", "chaptersDone", "run", "nodesPlayed",
        };

        /// <summary>The orders a campaign of runs takes (RunCommands.cs); any other is of before.</summary>
        private static readonly HashSet<Type> RunsOrders = new HashSet<Type>
        {
            typeof(MoveGearCommand), typeof(SplitGearCommand), typeof(DropGearCommand), typeof(MountCommand), typeof(UnmountCommand),
            typeof(UseGearCommand), typeof(BuyWareCommand), typeof(SquadCommand), typeof(LeaveStopCommand), typeof(UpgradeCommand),
            typeof(StartRunCommand), typeof(ChooseNodeCommand), typeof(EndRunCommand), typeof(AnswerStopCommand), typeof(VoteCommand),
            typeof(UnlockCommand), typeof(WithdrawCommand), typeof(CustomizeCommand),
        };

        private static JObject Old(CampaignState c)
        {
            var all = JObject.Parse(Json.Write(c));
            foreach (var name in RunsFields) all.Remove(name);
            return all;
        }

        private static void Same(JObject before, CampaignState c, string after)
        {
            var now = Old(c);
            var changed = before.Properties().Select((p) => p.Name).Concat(now.Properties().Select((p) => p.Name)).Distinct().Where((n) => !JToken.DeepEquals(before[n], now[n])).ToList();
            Assert.True(changed.Count == 0, $"{after}: {string.Join(", ", changed)}");
        }

        [Fact]
        public void ARunNeverTouchesTheCampaignOfBefore()
        {
            var rules = new EngineCampaignRules();
            IReadOnlyList<int> one = new[] { 0 };
            var played = 0;
            for (var seed = 1; seed <= 4; seed++)
            {
                var c = rules.CreateCampaign(one, new Rng(seed));
                Assert.True(Campaign.IsRuns(c));
                Assert.Equal(Campaign.RunsCampaignVersion, c.Version);
                var before = Old(c);
                Assert.True(new[] { "day", "doom", "offers", "regions", "base", "siege", "story" }.All((name) => before.ContainsKey(name)), string.Join(", ", before.Properties().Select((p) => p.Name)));
                var (day, doom, offers) = (c.Day, c.Doom, Json.Write(c.Offers));
                // What the room asks of it besides the orders.
                rules.OpeningMission(c);
                rules.SyncPads(c);
                rules.DefaultSquad(c, one);
                rules.SquadOpen(c);
                rules.Regroup(c, one);
                rules.HandOver(c, 0, one);
                c = rules.MigrateCampaign(c);
                Same(before, c, "the room's calls");
                // A few nodes, as the room plays them.
                var k = 0;
                CampaignResult Order(CampaignCommand cmd) => rules.ExecuteCampaignCommand(c, cmd, 0, one, new Rng(seed * 100 + k++));
                Assert.True(Order(new StartRunCommand()).Ok);
                Same(before, c, "going out");
                for (var step = 0; step < 4 && c.Run != null; step++)
                {
                    var choices = RunMap.Choices(c.Run);
                    var n = choices[step % choices.Count];
                    Assert.True(Order(new ChooseNodeCommand { Node = n.Id }).Ok);
                    Same(before, c, $"{n.Kind} chosen");
                    if (c.Run.Phase == RunPhases.Stop)
                    {
                        if (c.Run.Stop!.Outcome == null && n.Kind == NodeKinds.Event)
                        {
                            // An event's second answer (the first may cost what the squad hasn't got).
                            var answer = Order(new AnswerStopCommand { Option = 1 });
                            Assert.True(answer.Ok, $"{n.Kind}: {answer.Error}");
                        }
                        var left = Order(new LeaveStopCommand { Ready = true });
                        Assert.True(left.Ok, $"{n.Kind}: {left.Error}");
                        Same(before, c, $"{n.Kind} done");
                        continue;
                    }
                    var launched = rules.LaunchMission(c, new Rng(seed * 31 + step));
                    Assert.NotNull(launched);
                    var s = launched!.Value.state;
                    // Won, or now and then lost.
                    s.Outcome = (seed + step) % 5 == 0 ? Outcomes.Defeat : Outcomes.Victory;
                    s.OutcomeReason = s.Outcome == Outcomes.Victory ? EndReasons.Objective : EndReasons.Wiped;
                    rules.ApplyMissionResult(c, s, new Rng(seed));
                    played++;
                    Same(before, c, $"{n.Kind} fought");
                }
                if (c.Run != null) Order(new EndRunCommand());
                Same(before, c, "back home");
                // Every order of before: refused, and nothing of before changes.
                var tried = 0;
                foreach (var type in typeof(CampaignCommand).Assembly.GetTypes().Where((t) => !t.IsAbstract && typeof(CampaignCommand).IsAssignableFrom(t) && !RunsOrders.Contains(t)))
                {
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;
                    var cmd = (CampaignCommand)Activator.CreateInstance(type)!;
                    Assert.False(Order(cmd).Ok, type.Name);
                    Same(before, c, type.Name);
                    tried++;
                }
                Assert.True(tried >= 20, $"{tried} orders of before");
                Assert.Equal((day, doom, offers), (c.Day, c.Doom, Json.Write(c.Offers)));
            }
            Assert.True(played >= 4, $"{played} fights");
        }
    }
}
