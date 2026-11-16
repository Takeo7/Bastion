using System.Collections.Generic;
using System.Linq;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The runs' JSON (H0): a campaign of version 28 with its stash, kits, run and stop reads back as it was written, by
    /// the network's JSON (Net/Json.cs); every new command and vote comes through as its own class by its `type`; and a
    /// campaign of before writes none of the new fields.
    /// </summary>
    public class RunsJsonTests
    {
        /// <summary>The runs' commands (Types/CampaignCommand.cs), by `type`.</summary>
        public static readonly IReadOnlyList<string> CommandTags = new[]
        {
            "moveGear", "splitGear", "dropGear", "mount", "unmount", "useGear", "upgrade", "startRun", "chooseNode", "endRun", "answerStop", "buyWare", "leaveStop",
        };

        /// <summary>The runs' votes (Types/Runs.cs RunAction), by `type`.</summary>
        public static readonly IReadOnlyList<string> ActionTags = new[] { "buyUpgrade", "startRun", "chooseNode", "endRun", "answerStop" };

        [Fact]
        public void EveryNewCommandComesThroughAsItsClass()
        {
            var commands = new CampaignCommand[]
            {
                new MoveGearCommand { Gear = "g3", To = "stash", Slot = "pack", X = 2, Y = 1, Turned = true },
                new SplitGearCommand { Gear = "g3", Count = 2, To = "c1", Slot = "belt1" },
                new DropGearCommand { Gear = "g4" },
                new MountCommand { Mod = "g5", Weapon = "g6" },
                new UnmountCommand { Weapon = "g6", Mod = "scope" },
                new UseGearCommand { Gear = "g7", Soldier = "c2" },
                new UpgradeCommand { Upgrade = "stash" },
                new StartRunCommand(),
                new ChooseNodeCommand { Node = "n1-2" },
                new EndRunCommand(),
                new AnswerStopCommand { Option = 1 },
                new BuyWareCommand { Ware = "g9", Soldier = "c3" },
                new LeaveStopCommand { Ready = false },
            };
            Assert.Equal(CommandTags, commands.Select((x) => x.Type));
            foreach (var cmd in commands)
            {
                var json = Json.Write(cmd);
                var back = Json.ReadFromPlayer<CampaignCommand>(json);
                Assert.Equal(cmd.GetType(), back.GetType());
                Assert.Equal(json, Json.Write(back));
            }
            Assert.Equal("{\"type\":\"moveGear\",\"gear\":\"g3\",\"to\":\"stash\",\"slot\":\"pack\",\"x\":2,\"y\":1,\"turned\":true}", Json.Write(commands[0]));
            // Missing, a leave is a yes.
            Assert.True(Assert.IsType<LeaveStopCommand>(Json.Read<CampaignCommand>("{\"type\":\"leaveStop\"}")).Ready);
            var actions = new ProposalAction[] { new BuyUpgradeAction { Upgrade = "pack" }, new StartRunAction(), new ChooseNodeAction { Node = "n2-0" }, new EndRunAction(), new AnswerStopAction { Node = "n3-1", Option = 1 } };
            Assert.Equal(ActionTags, actions.Select((x) => x.Type));
            foreach (var action in actions)
            {
                var json = Json.Write(new Proposal { By = 1, Action = action, Text = "algo" });
                var back = Json.Read<Proposal>(json);
                Assert.Equal(action.GetType(), back.Action.GetType());
                Assert.Equal(json, Json.Write(back));
            }
            // The base of before's «upgrade» is still its own.
            Assert.IsType<UpgradeAction>(Json.Read<Proposal>("{\"by\":0,\"action\":{\"type\":\"upgrade\",\"plot\":\"p2\"},\"text\":\"\"}").Action);
        }

        [Fact]
        public void ARunCampaignReadsBackAsItWas()
        {
            var c = Two();
            var s = Soldier(c);
            InStash(c, GearKinds.Supplies, null, 0, 0, 60);
            InStash(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 1, 0, turned: true);
            InSlot(c, s, KitSlots.Belt1, GearKinds.Item, ItemIds.Molotov, 2);
            InPack(c, s, GearKinds.Medicine, null, 3, 2, 4);
            InSlot(c, s, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.HuntingRifle).Mods = new List<string> { WeaponModIds.Scope };
            c.Upgrades![UpgradeIds.Sight] = 1;
            GoTo(c, NodeKinds.Trade);
            c.Run!.Hands = new Dictionary<string, int> { [s.Id] = 1 };
            var json = Json.Write(c);
            var back = Json.Read<CampaignState>(json);
            Assert.Equal(json, Json.Write(back));
            var o = JObject.Parse(json);
            Assert.Equal(28, (int)o["version"]!);
            Assert.Equal(10, (int)o["stash"]!["w"]!);
            Assert.Equal("knife", (string)o["soldiers"]![0]!["kit"]!["melee"]!["type"]!);
            Assert.Equal(JTokenType.Null, o["soldiers"]![0]!["kit"]!["belt"]![0]!.Type);
            Assert.Equal("trade", (string)o["run"]!["nodes"]!.First((n) => (string)n["id"]! == (string)o["run"]!["at"]!)["kind"]!);
            Assert.Equal(5, ((JArray)o["run"]!["stop"]!["wares"]!).Count);
            Assert.Equal(1, (int)o["nodesPlayed"]!);
            Assert.Equal(Campaign.RunsCampaignVersion, back.Version);
            Assert.True(Campaign.IsRuns(back));
        }

        [Fact]
        public void ACampaignOfBeforeWritesNoneOfIt()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            var o = JObject.Parse(Json.Write(c));
            foreach (var field in new[] { "stash", "upgrades", "chapter", "chaptersDone", "run", "nodesPlayed" }) Assert.Null(o[field]);
            Assert.All((JArray)o["soldiers"]!, (s) => Assert.Null(s["kit"]));
            Assert.False(Campaign.IsRuns(c));
        }
    }
}
