using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The stops of a run (H0, H0-DISENO §5; their real contents are H4's): the Refuge heals, the Trade sells for the
    /// supplies in the packs, an event and a survivor are answered (a vote with both here), and the squad goes on when
    /// everyone asks; a medicine between nodes.
    /// </summary>
    public class RunsStopTests
    {
        [Fact]
        public void TheRefugeHealsOnArrival()
        {
            var c = Solo();
            var run = Start(c);
            foreach (var s in Squad(c)) run.Hp[s.Id] = 1;
            Assert.False(GoTo(c, NodeKinds.Refuge).Launch);
            Assert.Equal(RunPhases.Stop, run.Phase);
            Assert.All(Squad(c), (s) => Assert.Equal(Runs.MaxHp(s), run.Hp[s.Id]));
            Assert.Equal("refuge.rest", run.Stop!.Outcome);
            Assert.Equal(1, c.NodesPlayed);
            Refused(RunTexts.NothingToAnswer, c, new AnswerStopCommand { Option = 0 });
            Refused(RunTexts.NotChoosing, c, new ChooseNodeCommand { Node = RunMap.Node(run, run.At)!.Next[0] });
            Ok(c, new LeaveStopCommand());
            Assert.Equal(RunPhases.Choose, run.Phase);
            Assert.Null(run.Stop);
            Refused(RunTexts.NotAtStop, c, new LeaveStopCommand());
        }

        [Fact]
        public void TheTradeSetsOutFiveWares()
        {
            var c = Solo();
            GoTo(c, NodeKinds.Trade);
            var wares = c.Run!.Stop!.Wares!;
            Assert.Equal(Content.TradeCount, wares.Count);
            Assert.Equal(wares.Count, wares.Select((w) => (w.Gear.Kind, w.Gear.Type)).Distinct().Count());
            Assert.All(wares, (w) => Assert.Equal(Content.TradeWares.First((x) => x.Kind == w.Gear.Kind && x.Type == w.Gear.Type).Price, w.Price));
            Assert.All(wares, (w) => Assert.Equal(1, w.Gear.Count));
            // With the haggle, a fifth off.
            var d = Solo();
            d.Upgrades![UpgradeIds.Haggle] = 1;
            GoTo(d, NodeKinds.Trade);
            Assert.All(d.Run!.Stop!.Wares!, (w) => Assert.Equal(Content.Haggled(Content.TradeWares.First((x) => x.Kind == w.Gear.Kind && x.Type == w.Gear.Type).Price, true), w.Price));
        }

        [Fact]
        public void BuyingPaysFromTheOwnPacks()
        {
            var c = Two();
            GoTo(c, NodeKinds.Trade);
            var stop = c.Run!.Stop!;
            var ware = stop.Wares![0];
            ware.Price = 50;
            var buyer = Squad(c).First((s) => s.Owner == 0);
            var other = Squad(c).Last((s) => s.Owner == 0);
            var partner = Squad(c).First((s) => s.Owner == 1);
            var cmd = new BuyWareCommand { Ware = ware.Gear.Id, Soldier = buyer.Id };
            Refused(RunTexts.Missing(50), c, cmd, 0, Both);
            // The partner's supplies are theirs.
            InPack(c, partner, GearKinds.Supplies, null, 0, 0, 100);
            Refused(RunTexts.Missing(50), c, cmd, 0, Both);
            Refused(RunTexts.NotYours, c, new BuyWareCommand { Ware = ware.Gear.Id, Soldier = partner.Id }, 0, Both);
            var own = InPack(c, buyer, GearKinds.Supplies, null, 0, 0, 30);
            var more = InPack(c, other, GearKinds.Supplies, null, 0, 0, 40);
            Ok(c, cmd, 0, Both);
            // The buyer's first: their 30, then 20 of the other's.
            Assert.DoesNotContain(buyer.Kit!.Pack.Items, (p) => p.Gear == own);
            Assert.Equal(20, more.Count);
            Assert.Contains(buyer.Kit.Pack.Items, (p) => p.Gear == ware.Gear);
            Assert.DoesNotContain(ware, stop.Wares);
            Assert.Equal(100, Gears.Supplies(partner.Kit!.Pack));
            Refused(RunTexts.NoWare, c, cmd, 0, Both);
        }

        [Fact]
        public void AWareThatDoesntFitIsNotBought()
        {
            var c = Solo();
            GoTo(c, NodeKinds.Trade);
            var stop = c.Run!.Stop!;
            var ware = stop.Wares![0];
            ware.Gear = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            ware.Price = 10;
            var buyer = Squad(c)[0];
            // A pack with something in each row: no rifle.
            InPack(c, buyer, GearKinds.Supplies, null, 0, 0, 20);
            InPack(c, buyer, GearKinds.Supplies, null, 1, 1, 20);
            InPack(c, buyer, GearKinds.Mags, null, 2, 2);
            var cmd = new BuyWareCommand { Ware = ware.Gear.Id, Soldier = buyer.Id };
            Refused(RunTexts.PackFull(buyer.Name), c, cmd);
            // Paying 20 empties the pile of the first row: room for it there.
            ware.Price = 20;
            Ok(c, cmd);
            Assert.Contains(buyer.Kit!.Pack.Items, (p) => p.Gear == ware.Gear && p.Y == 0);
            Refused(RunTexts.NotTrade, Solo2Refuge(), new BuyWareCommand { Ware = "g1", Soldier = "c1" });
        }

        private static CampaignState Solo2Refuge()
        {
            var c = Solo();
            GoTo(c, NodeKinds.Refuge);
            return c;
        }

        [Fact]
        public void AnEventIsAnsweredOnce()
        {
            var c = Solo();
            GoTo(c, NodeKinds.Event);
            var stop = c.Run!.Stop!;
            Assert.Contains(stop.Event, Content.RunEventIds);
            stop.Event = "radio";
            Refused(RunTexts.AnswerFirst, c, new LeaveStopCommand());
            Refused(RunTexts.NoOption, c, new AnswerStopCommand { Option = 2 });
            Ok(c, new AnswerStopCommand { Option = 1 });
            Assert.Equal("radio.1", stop.Outcome);
            Assert.Equal(20, Gears.Supplies(Squad(c)[0].Kit!.Pack));
            Refused(RunTexts.Answered, c, new AnswerStopCommand { Option = 0 });
            Ok(c, new LeaveStopCommand());
        }

        [Fact]
        public void ASurvivorStopOfThe0130IsARefugeOnceLoaded()
        {
            // N3 (17/11): the Superviviente stop is gone; a 0.13.0 save at one (version 28) rests there instead. Its seats go
            // to 2 (N4, cuadro 23), the run under way going on with the squad that went.
            var c = Solo();
            GoTo(c, NodeKinds.Refuge);
            // 4 seats as the 0.13.0 had them, with no upgrade.
            c.Upgrades!.Remove(UpgradeIds.Squad);
            var run = c.Run!;
            var at = RunMap.Node(run, run.At)!;
            at.Kind = Campaign.OldSurvivorStop;
            var ahead = run.Nodes.First((n) => n.Row == 3);
            ahead.Kind = Campaign.OldSurvivorStop;
            run.Stop = new StopState();
            var hurt = Squad(c)[0];
            run.Hp[hurt.Id] = 1;
            c.Version = Campaign.FirstRunsVersion;
            var back = Campaign.MigrateCampaign(Bastion.Net.Json.Read<CampaignState>(Bastion.Net.Json.Write(c))!);
            Assert.Equal(Campaign.RunsCampaignVersion, back.Version);
            Assert.Equal((0, 2), (Runs.Level(back, UpgradeIds.Squad), Runs.SeatsOf(back)));
            Assert.Equal(4, back.Run!.Squad.Count);
            Assert.Equal(Lore.SeatsReset(0), back.Log[0].Text);
            Assert.DoesNotContain(back.Run!.Nodes, (n) => n.Kind == Campaign.OldSurvivorStop);
            Assert.Equal(NodeKinds.Refuge, RunMap.Node(back.Run, ahead.Id)!.Kind);
            Assert.Equal("refuge.rest", back.Run.Stop!.Outcome);
            Assert.Equal(Runs.MaxHp(hurt), back.Run.Hp[hurt.Id]);
            Assert.Null(Runs.LeaveStopRefusal(back));
            // Once up to date, as it is.
            Assert.Same(back, Campaign.MigrateCampaign(back));
            Assert.Equal(0, Runs.Level(back, UpgradeIds.Squad));
        }

        [Fact]
        public void The0130sSeatsArePaidBack()
        {
            // 8 seats bought in the 0.13.0 (150 + 300 §): down to 2, the 450 § in the stash; the squad on the base, one each.
            var c = Two();
            c.Upgrades![UpgradeIds.Squad] = 2;
            c.Version = Campaign.FirstRunsVersion;
            var had = Runs.StashSupplies(c);
            var back = Campaign.MigrateCampaign(c);
            Assert.Equal((0, 2), (Runs.Level(back, UpgradeIds.Squad), Runs.SeatsOf(back)));
            Assert.Equal(had + 450, Runs.StashSupplies(back));
            Assert.Equal(Lore.SeatsReset(450), back.Log[0].Text);
            Assert.Equal(new[] { 0, 1 }, back.Squad.Select((id) => back.Soldiers.First((s) => s.Id == id).Owner).OrderBy((o) => o));
            // 6 seats: 150 §.
            var d = Solo();
            d.Version = Campaign.FirstRunsVersion;
            Assert.Equal(150, Runs.StashSupplies(Campaign.MigrateCampaign(d)));
        }

        [Fact]
        public void WithBothTheAnswerIsVotedAndBothLeave()
        {
            var c = Two();
            GoTo(c, NodeKinds.Event);
            var stop = c.Run!.Stop!;
            stop.Event = "radio";
            Ok(c, new AnswerStopCommand { Option = 1 }, 1, Both);
            Assert.Equal(RunTexts.AnswerText(stop, 1), c.Proposal!.Text);
            Assert.Null(stop.Outcome);
            Ok(c, new VoteCommand { Accept = true }, 0, Both);
            Assert.Equal("radio.1", stop.Outcome);
            // One asks: waits for the other; one takes it back.
            Ok(c, new LeaveStopCommand(), 0, Both);
            Assert.Equal(new[] { true, false }, stop.Leaving);
            Assert.Equal(RunPhases.Stop, c.Run.Phase);
            Ok(c, new LeaveStopCommand { Ready = false }, 0, Both);
            Assert.Equal(new[] { false, false }, stop.Leaving);
            Ok(c, new LeaveStopCommand(), 1, Both);
            Ok(c, new LeaveStopCommand(), 0, Both);
            Assert.Equal(RunPhases.Choose, c.Run.Phase);
            // The other away: at once.
            var d = Two();
            GoTo(d, NodeKinds.Refuge);
            Ok(d, new LeaveStopCommand(), 1, new[] { 1 });
            Assert.Equal(RunPhases.Choose, d.Run!.Phase);
        }

        [Fact]
        public void AnAnswerLeftOverFromAnotherStopIsRefused()
        {
            var c = Two();
            GoTo(c, NodeKinds.Event);
            var at = c.Run!.At!;
            Ok(c, new AnswerStopCommand { Option = 0 }, 0, Both);
            var action = Assert.IsType<AnswerStopAction>(c.Proposal!.Action);
            Assert.Equal(at, action.Node);
            Assert.Equal(RunTexts.StaleAnswer, Runs.AnswerStopRefusal(c, 0, "n9-9"));
        }

        [Fact]
        public void AMedicineBetweenNodes()
        {
            var c = Solo();
            var s = Soldier(c);
            var med = InPack(c, s, GearKinds.Medicine, null, 0, 0, 2);
            var cmd = new UseGearCommand { Gear = med.Id, Soldier = s.Id };
            Refused(RunTexts.BetweenNodes, c, cmd);
            var run = Start(c);
            Refused(RunTexts.FullHealth, c, cmd);
            var max = Runs.MaxHp(s);
            run.Hp[s.Id] = 1;
            Ok(c, cmd);
            Assert.Equal(System.Math.Min(max, 1 + Content.MedicineHeal), run.Hp[s.Id]);
            Assert.Equal(1, med.Count);
            run.Hp[s.Id] = max - 1;
            Ok(c, cmd);
            Assert.Equal(max, run.Hp[s.Id]);
            Assert.Empty(s.Kit!.Pack.Items);
            Refused(RunTexts.NoGear, c, cmd);
            var mags = InPack(c, s, GearKinds.Mags, null, 0, 0);
            run.Hp[s.Id] = 1;
            Refused(RunTexts.NotUsable, c, new UseGearCommand { Gear = mags.Id, Soldier = s.Id });
            // Not in a fight.
            var med2 = InPack(c, s, GearKinds.Medicine, null, 1, 0);
            GoTo(c, NodeKinds.Normal);
            Refused(RunTexts.InFight, c, new UseGearCommand { Gear = med2.Id, Soldier = s.Id });
        }
    }
}
