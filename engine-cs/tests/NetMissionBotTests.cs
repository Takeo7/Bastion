using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The player that plays by itself in the tests between two apps (Net/MissionBot.cs): the opening operation of a new
    /// campaign of two (golden/campaign/states.json#duo-new), launched as the room does and played by it for both seats with
    /// the engine's rules, is done: the insulin carried into La Lata, and the bus gone.
    /// </summary>
    public class NetMissionBotTests
    {
        private static readonly string Golden = Path.Combine(AppContext.BaseDirectory, "golden");

        /// <summary>The opening operation of a new campaign of two, as the room launches it.</summary>
        private static GameState Launch(int seed)
        {
            var states = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(Golden, "campaign", "states.json")))["states"]!;
            var c = states.Single((s) => (string)s["name"]! == "duo-new")["campaign"]!.ToObject<CampaignState>(Json.Serializer)!;
            return new EngineCampaignRules().LaunchMission(c, new Rng(seed))!.Value.state;
        }

        /// <summary>The mission over, both seats played by MissionBot: its state, the orders given and those refused.</summary>
        private static (GameState state, int orders, int refused) Play(int seed)
        {
            var state = Launch(seed);
            var rng = new Rng(seed * 7919 + 13);
            var connected = new List<int> { 0, 1 };
            int orders = 0, refused = 0, idle = 0;
            while (state.Outcome == null && orders < 2000 && idle < 3)
            {
                var any = false;
                foreach (var slot in connected)
                {
                    var cmd = MissionBot.Next(state, slot, connected);
                    if (cmd == null) continue;
                    orders++;
                    if (Rules.ExecuteCommand(state, cmd, slot, connected, rng).Ok) any = true;
                    else refused++;
                }
                idle = any ? 0 : idle + 1;
            }
            return (state, orders, refused);
        }

        [Fact]
        public void TheOpeningOperationIsDone()
        {
            // 95 of the first 100 seeds when it was written; these eight, all.
            var played = Enumerable.Range(1, 8).Select(Play).ToList();
            Assert.All(played, (p) => Assert.Equal(0, p.refused));
            var won = played.Count((p) => p.state.Outcome == Outcomes.Victory);
            Assert.True(won >= 7, $"{won} of 8: {string.Join(", ", played.Select((p) => $"{p.state.Outcome}/{p.state.OutcomeReason}"))}");
            Assert.All(played.Where((p) => p.state.Outcome == Outcomes.Victory), (p) => Assert.True(p.state.Mission.Item!.Evacuated || p.state.Units.Any((u) => u.Evacuated && u.Id == p.state.Mission.Item.Carrier)));
        }

        [Fact]
        public void ItWaitsForTheCommandAndAnswersTheBus()
        {
            var state = Launch(3);
            var both = new List<int> { 0, 1 };
            // With the command held by one, the other has nothing to say.
            state.Command = 0;
            Assert.Null(MissionBot.Next(state, 1, both));
            Assert.NotNull(MissionBot.Next(state, 0, both));
            // The partner calls La Lata: yes, whoever holds the command; one's own call waits.
            state.EvacCall = new EvacCall { By = 0, Unit = state.Units.First((u) => u.Team == Teams.Xcom).Id };
            Assert.True(Assert.IsType<EvacAnswerCommand>(MissionBot.Next(state, 1, both)).Accept);
            Assert.Null(MissionBot.Next(state, 0, both));
            // Over, nothing.
            state.EvacCall = null;
            state.Outcome = Outcomes.Victory;
            Assert.Null(MissionBot.Next(state, 0, both));
        }
    }
}
