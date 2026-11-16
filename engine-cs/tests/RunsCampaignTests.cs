using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// A campaign of runs (H0, .dev-snapshots/gameplay/runs/H0-DISENO.md §3 and §5): how it starts, the free recruit, a
    /// partner who joins, the squad that goes out, the upgrades, going out, choosing nodes, going back, the votes and the
    /// soldiers handed over; and that the campaign of before goes on as it was.
    /// </summary>
    public class RunsCampaignTests
    {
        // ------------------------------------------------------------------ create

        [Fact]
        public void ANewCampaignAlone()
        {
            var c = Solo();
            Assert.Equal(Campaign.RunsCampaignVersion, c.Version);
            Assert.True(Campaign.IsRuns(c));
            Assert.Equal(4, c.Soldiers.Count);
            Assert.All(c.Soldiers, (s) =>
            {
                Assert.Equal(0, s.Owner);
                Assert.True(s.Alive);
                Assert.Equal((GearKinds.Weapon, FoundWeaponIds.Knife), (s.Kit!.Melee!.Kind, s.Kit.Melee.Type));
                Assert.Null(s.Kit.Primary);
                Assert.Null(s.Kit.Pistol);
                Assert.Null(s.Kit.Vest);
                Assert.Equal(new Gear?[] { null, null }, s.Kit.Belt);
                Assert.Equal((4, 3), (s.Kit.Pack.W, s.Kit.Pack.H));
                Assert.Empty(s.Kit.Pack.Items);
            });
            Assert.Equal(4, c.Soldiers.Select((s) => s.Kit!.Melee!.Id).Distinct().Count());
            Assert.Equal((10, 6), (c.Stash!.W, c.Stash.H));
            Assert.Empty(c.Stash.Items);
            Assert.Equal(0, c.Credits);
            Assert.Equal(0, Runs.StashSupplies(c));
            Assert.Equal(1, c.Chapter);
            Assert.Equal(0, c.ChaptersDone);
            Assert.Equal(0, c.NodesPlayed);
            Assert.Null(c.Run);
            Assert.Empty(c.Upgrades!);
            Assert.Equal(c.Soldiers.Select((s) => s.Id), c.Squad);
            Assert.NotNull(c.Uid);
            Assert.Equal(RunTexts.Welcome, c.Log[0].Text);
            // Nothing of the campaign of before.
            Assert.Empty(c.Offers);
            Assert.Null(c.FoundWeapons);
        }

        [Fact]
        public void ANewCampaignOfTwo()
        {
            var c = Two();
            Assert.Equal(2, c.Soldiers.Count((s) => s.Owner == 0));
            Assert.Equal(2, c.Soldiers.Count((s) => s.Owner == 1));
            Assert.Equal(4, c.Squad.Count);
            // The same seed, the same campaign.
            Assert.Equal(Bastion.Net.Json.Write(Two()), Bastion.Net.Json.Write(c));
        }

        [Fact]
        public void TheCampaignOfBeforeGoesOnAsItWas()
        {
            var old = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            Assert.Equal(Campaign.CampaignVersion, old.Version);
            Assert.Null(old.Stash);
            Assert.All(old.Soldiers, (s) => Assert.Null(s.Kit));
            // The runs' orders don't go in it…
            var r = CampaignHelpers.Run(old, new StartRunCommand());
            Assert.Equal((false, "Esa orden no existe."), (r.Ok, r.Error));
            // …nor its orders of before in a campaign of runs.
            var c = Solo();
            Refused("Esa orden no existe.", c, new ChooseMissionCommand { Id = "m1" });
            Refused("Esa orden no existe.", c, new LaunchCommand { Ready = true });
            Refused("Esa orden no existe.", c, new BuyCommand { Item = ItemIds.Molotov });
            // A run campaign is not migrated.
            Assert.Same(c, Campaign.MigrateCampaign(c));
            Assert.Equal(Campaign.RunsCampaignVersion, c.Version);
            // Customize and unlock stay.
            var s = Soldier(c);
            Ok(c, new CustomizeCommand { Soldier = s.Id, Name = "Nuevo", Nickname = "", Appearance = s.Appearance });
            Assert.Equal("Nuevo", s.Name);
        }

        // ------------------------------------------------------- nobody left, joins

        [Fact]
        public void NobodyLeftBringsOneWithAKnife()
        {
            var c = Two();
            foreach (var s in c.Soldiers.Where((x) => x.Owner == 1)) s.Alive = false;
            // Not while a run is going.
            c.Run = new RunState();
            Assert.False(Runs.NobodyLeft(c, new Rng(3)));
            c.Run = null;
            Assert.True(Runs.NobodyLeft(c, new Rng(3)));
            var fresh = c.Soldiers.Where((s) => s.Owner == 1 && s.Alive).ToList();
            Assert.Single(fresh);
            Assert.Equal(FoundWeaponIds.Knife, fresh[0].Kit!.Melee!.Type);
            Assert.Equal(Runs.PackRows(c), fresh[0].Kit!.Pack.H);
            Assert.Equal(RunTexts.Recruit(fresh[0].Name), c.Log[0].Text);
            // One, and nothing more.
            Assert.False(Runs.NobodyLeft(c, new Rng(3)));
            Assert.Equal(5, c.Soldiers.Count);
        }

        [Fact]
        public void APartnerWhoJoinsGetsTwoRookies()
        {
            var c = Solo();
            var last = c.Soldiers.Skip(2).Select((s) => s.Id).ToList();
            Assert.Equal(2, Campaign.AddPlayer(c, 1, new Rng(5)));
            Assert.Equal(new[] { 0, 1 }, c.Players);
            Assert.Equal(last, c.Soldiers.Where((s) => s.Owner == 1).Select((s) => s.Id).OrderBy((x) => x));
            Assert.Equal(2, c.Soldiers.Count((s) => s.Owner == 0));
            Assert.All(c.Soldiers, (s) => Assert.NotNull(s.Kit));
            Assert.Equal(0, Campaign.AddPlayer(c, 1, new Rng(5)));
            // With the rookies gone out once, recruits with a knife.
            var d = Solo();
            foreach (var s in d.Soldiers) s.Missions = 1;
            Campaign.AddPlayer(d, 1, new Rng(5));
            Assert.Equal(6, d.Soldiers.Count);
            Assert.All(d.Soldiers.Where((s) => s.Owner == 1), (s) => Assert.Equal(FoundWeaponIds.Knife, s.Kit!.Melee!.Type));
        }

        // ---------------------------------------------------------------- the squad

        [Fact]
        public void EachPicksTheirOwnSquad()
        {
            var c = Two();
            var mine = Soldier(c, 0);
            var theirs = Soldier(c, 1);
            Refused(RunTexts.PickOwn, c, new SquadCommand { Soldier = theirs.Id, On = false }, 0, Both);
            Ok(c, new SquadCommand { Soldier = mine.Id, On = false }, 0, Both);
            Assert.DoesNotContain(mine.Id, c.Squad);
            Ok(c, new SquadCommand { Soldier = mine.Id, On = true }, 0, Both);
            // Half the seats each with both here.
            var extra = Runs.NewSoldier(c, 0, new Rng(2));
            Refused(RunTexts.PerPlayer(2), c, new SquadCommand { Soldier = extra.Id, On = true }, 0, Both);
            // Alone, all four seats, but they're taken.
            Refused(RunTexts.Seats(4), c, new SquadCommand { Soldier = extra.Id, On = true }, 0, Zero);
            Ok(c, new SquadCommand { Soldier = theirs.Id, On = false }, 1, Both);
            Ok(c, new SquadCommand { Soldier = extra.Id, On = true }, 0, Zero);
            Assert.Equal(3, c.Squad.Count((id) => c.Soldiers.First((s) => s.Id == id).Owner == 0));
            // Six seats with the upgrade.
            c.Upgrades![UpgradeIds.Squad] = 1;
            Assert.Equal(6, Runs.SeatsOf(c));
            Assert.Equal(3, Runs.SquadLimit(c, Both));
            // Not once out.
            Start(c);
            Refused(RunTexts.RunOn, c, new SquadCommand { Soldier = mine.Id, On = false });
        }

        // -------------------------------------------------------------- upgrades

        [Fact]
        public void UpgradesArePaidFromTheStash()
        {
            var c = Solo();
            Refused(RunTexts.Missing(80), c, new UpgradeCommand { Upgrade = UpgradeIds.Stash });
            Refused(RunTexts.NoUpgrade, c, new UpgradeCommand { Upgrade = "wings" });
            var big = InStash(c, GearKinds.Supplies, null, 0, 0, 70);
            var small = InStash(c, GearKinds.Supplies, null, 1, 0, 20);
            Assert.Equal(RunTexts.Missing(10), Runs.UpgradeRefusal(c, UpgradeIds.Pack));
            Ok(c, new UpgradeCommand { Upgrade = UpgradeIds.Stash });
            Assert.Equal(1, Runs.Level(c, UpgradeIds.Stash));
            Assert.Equal(8, c.Stash!.H);
            // The smallest pile first.
            Assert.DoesNotContain(c.Stash.Items, (p) => p.Gear == small);
            Assert.Equal(10, big.Count);
            Assert.Equal(RunTexts.Upgraded(UpgradeIds.Stash, 1), c.Log[0].Text);
            Assert.Equal(0, c.Credits);
            // A row more in every pack.
            big.Count = 100;
            InStash(c, GearKinds.Supplies, null, 2, 0, 100);
            Ok(c, new UpgradeCommand { Upgrade = UpgradeIds.Pack });
            Assert.All(c.Soldiers, (s) => Assert.Equal(4, s.Kit!.Pack.H));
            Assert.Equal(4, Runs.PackRows(c));
            Assert.Equal(4, Runs.NewSoldier(c, 0, new Rng(1)).Kit!.Pack.H);
            // At the top.
            c.Upgrades![UpgradeIds.Mend] = 1;
            Refused(RunTexts.UpgradeTop, c, new UpgradeCommand { Upgrade = UpgradeIds.Mend });
            // Not in a run.
            Start(c);
            Refused(RunTexts.UpgradeAtBase, c, new UpgradeCommand { Upgrade = UpgradeIds.Sight });
        }

        [Fact]
        public void WithTheOtherHereAnUpgradeIsVoted()
        {
            var c = Two();
            InStash(c, GearKinds.Supplies, null, 0, 0, 100);
            Ok(c, new UpgradeCommand { Upgrade = UpgradeIds.Sight }, 0, Both);
            Assert.Equal(0, Runs.Level(c, UpgradeIds.Sight));
            Assert.IsType<BuyUpgradeAction>(c.Proposal!.Action);
            Assert.Equal(RunTexts.UpgradeText(UpgradeIds.Sight, 80), c.Proposal.Text);
            Assert.Equal(80, Runs.ProposalCost(c, (RunAction)c.Proposal.Action));
            Refused(RunTexts.Pending, c, new UpgradeCommand { Upgrade = UpgradeIds.Stash }, 1, Both);
            Refused("No puedes votar tu propia propuesta.", c, new VoteCommand { Accept = true }, 0, Both);
            Ok(c, new VoteCommand { Accept = false }, 1, Both);
            Assert.Null(c.Proposal);
            Assert.Equal(0, Runs.Level(c, UpgradeIds.Sight));
            Assert.StartsWith("Propuesta rechazada", c.Log[0].Text);
            // Accepted.
            Agree(c, new UpgradeCommand { Upgrade = UpgradeIds.Sight }, 1);
            Assert.Equal(1, Runs.Level(c, UpgradeIds.Sight));
            Assert.Equal(20, Runs.StashSupplies(c));
            // The supplies gone meanwhile: dropped, and said so.
            var pile = InStash(c, GearKinds.Supplies, null, 1, 0, 80);
            Ok(c, new UpgradeCommand { Upgrade = UpgradeIds.Haggle }, 0, Both);
            Assert.NotNull(c.Proposal);
            Ok(c, new MoveGearCommand { Gear = pile.Id, To = Soldier(c, 0).Id, Slot = KitSlots.Pack, X = 0, Y = 0 }, 0, Both);
            Ok(c, new VoteCommand { Accept = true }, 1, Both);
            Assert.Equal(0, Runs.Level(c, UpgradeIds.Haggle));
            Assert.Equal($"Propuesta aceptada, pero ya no se puede: {RunTexts.UpgradeText(UpgradeIds.Haggle, 100)}. {RunTexts.Missing(80)}", c.Log[0].Text);
            // Withdrawn.
            InStash(c, GearKinds.Supplies, null, 2, 0, 100);
            Ok(c, new UpgradeCommand { Upgrade = UpgradeIds.Stash }, 1, Both);
            Ok(c, new WithdrawCommand(), 1, Both);
            Assert.Null(c.Proposal);
        }

        // -------------------------------------------------------------- going out

        [Fact]
        public void TheSquadGoesOut()
        {
            var c = Solo();
            var squad = c.Squad.ToList();
            Ok(c, new StartRunCommand());
            var run = c.Run!;
            Assert.Equal((1, RunPhases.Choose), (run.Chapter, run.Phase));
            Assert.Null(run.At);
            Assert.Empty(run.Path);
            Assert.Equal(squad, run.Squad);
            Assert.All(Squad(c), (s) => Assert.Equal(Runs.MaxHp(s), run.Hp[s.Id]));
            Assert.Equal(RunMap.Rows, run.Nodes.Max((n) => n.Row));
            Assert.Equal(RunTexts.RunStarted(1), c.Log[0].Text);
            Refused(RunTexts.RunOn, c, new StartRunCommand());
            // Its map is its seed's.
            Assert.Equal(Bastion.Net.Json.Write(RunMap.Generate(1, run.Seed)), Bastion.Net.Json.Write(run.Nodes));
        }

        [Fact]
        public void NobodyGoesOutEmptyOrTooMany()
        {
            var c = Solo();
            c.Squad = new List<string>();
            Refused(RunTexts.EmptySquad, c, new StartRunCommand());
            // The dead don't go.
            c.Squad = new List<string> { Soldier(c).Id };
            Soldier(c).Alive = false;
            Refused(RunTexts.EmptySquad, c, new StartRunCommand());
            // Three of one with both here: two each.
            var d = Two();
            Runs.NewSoldier(d, 0, new Rng(2));
            Ok(d, new SquadCommand { Soldier = Soldier(d, 1).Id, On = false }, 1, Both);
            Ok(d, new SquadCommand { Soldier = Soldier(d, 0, 2).Id, On = true }, 0, Zero);
            Refused(RunTexts.PerPlayer(2), d, new StartRunCommand(), 0, Both);
            Ok(d, new StartRunCommand(), 0, Zero);
            Assert.Equal(4, d.Run!.Squad.Count);
        }

        [Fact]
        public void WithBothHereGoingOutIsVoted()
        {
            var c = Two();
            Ok(c, new StartRunCommand(), 1, Both);
            Assert.Null(c.Run);
            Assert.IsType<StartRunAction>(c.Proposal!.Action);
            Assert.Equal(RunTexts.StartRunText(1), c.Proposal.Text);
            Ok(c, new VoteCommand { Accept = true }, 0, Both);
            Assert.NotNull(c.Run);
        }

        // ------------------------------------------------------------- on the map

        [Fact]
        public void AFightLaunchesAndWaitsForItsEnd()
        {
            var c = Solo();
            var run = Start(c);
            var second = run.Nodes.First((n) => n.Row == 2);
            Refused(RunTexts.NotNext, c, new ChooseNodeCommand { Node = second.Id });
            Refused(RunTexts.NoNode, c, new ChooseNodeCommand { Node = "n9-9" });
            var r = GoTo(c, NodeKinds.Normal);
            Assert.True(r.Launch);
            Assert.Equal(RunPhases.Fight, run.Phase);
            Assert.Equal(new[] { run.At }, run.Path);
            Assert.Equal(1, c.NodesPlayed);
            Refused(RunTexts.NotChoosing, c, new ChooseNodeCommand { Node = RunMap.Node(run, run.At)!.Next[0] });
            Refused(RunTexts.EndFromMap, c, new EndRunCommand());
            // Won (H3 calls it; here by hand): back to the map, the next row to choose from.
            Runs.NodeCleared(c, new Rng(1));
            Assert.Equal(RunPhases.Choose, run.Phase);
            Assert.All(RunMap.Choices(run), (n) => Assert.Equal(2, n.Row));
            Assert.Null(Runs.ChooseNodeRefusal(c, RunMap.Choices(run)[0].Id));
        }

        [Fact]
        public void WhatIsInSight()
        {
            var c = Solo();
            Assert.Equal(0, RunMap.VisibleRows(c));
            var run = Start(c);
            Assert.Equal(2, RunMap.VisibleRows(c));
            Assert.True(RunMap.Visible(c, run.Nodes.First((n) => n.Row == 2)));
            Assert.False(RunMap.Visible(c, run.Nodes.First((n) => n.Row == 3)));
            // The boss, always, from the start (as in Slay the Spire); the rows before it, not.
            var boss = run.Nodes.Single((n) => n.Kind == NodeKinds.Boss);
            Assert.Equal(RunMap.BossRow, boss.Row);
            Assert.True(RunMap.Visible(c, boss));
            Assert.All(run.Nodes.Where((n) => n.Row > 2 && n.Row < RunMap.BossRow), (n) => Assert.False(RunMap.Visible(c, n)));
            GoTo(c, NodeKinds.Normal);
            Assert.Equal(3, RunMap.VisibleRows(c));
            c.Upgrades![UpgradeIds.Sight] = 2;
            Assert.Equal(5, RunMap.VisibleRows(c));
            Assert.False(RunMap.Visible(c, run.Nodes.First((n) => n.Row == 6)));
            Assert.True(RunMap.Visible(c, boss));
            // A node out of sight can't be chosen, should the map lead there.
            Runs.NodeCleared(c, new Rng(1));
            c.Upgrades[UpgradeIds.Sight] = 0;
            var far = run.Nodes.First((n) => n.Row == 5);
            RunMap.Node(run, run.At)!.Next.Add(far.Id);
            Assert.Equal(RunTexts.NotSeen, Runs.ChooseNodeRefusal(c, far.Id));
        }

        [Fact]
        public void TheMendUpgradeHealsAfterANode()
        {
            var c = Solo();
            c.Upgrades![UpgradeIds.Mend] = 1;
            var run = Start(c);
            var s = Squad(c)[0];
            GoTo(c, NodeKinds.Normal);
            run.Hp[s.Id] = 1;
            Runs.NodeCleared(c, new Rng(1));
            Assert.Equal(1 + Runs.MendHeal, run.Hp[s.Id]);
            Assert.Equal(Runs.MaxHp(Squad(c)[1]), run.Hp[Squad(c)[1].Id]);
        }

        [Fact]
        public void TheBossDeadTheChapterIsDone()
        {
            var c = Solo();
            var run = Start(c);
            var refuge = run.Nodes.First((n) => n.Row == RunMap.RefugeRow);
            Assert.Equal(NodeKinds.Refuge, refuge.Kind);
            run.At = refuge.Id;
            var boss = RunMap.Choices(run).Single();
            Assert.Equal(NodeKinds.Boss, boss.Kind);
            Assert.True(Ok(c, new ChooseNodeCommand { Node = boss.Id }).Launch);
            Runs.NodeCleared(c, new Rng(1));
            Assert.Null(c.Run);
            Assert.Equal((2, 1), (c.Chapter, c.ChaptersDone));
            Assert.Equal(RunTexts.ChapterDone(1), c.Log[0].Text);
        }

        [Fact]
        public void GoingBackEndsTheRun()
        {
            var c = Two();
            var run = Start(c);
            var s = Squad(c)[0];
            var med = InPack(c, s, GearKinds.Medicine, null, 0, 0, 2);
            run.Hp[s.Id] = 1;
            run.Hands = new Dictionary<string, int> { [s.Id] = 1 };
            Ok(c, new EndRunCommand(), 0, Both);
            Assert.IsType<EndRunAction>(c.Proposal!.Action);
            Ok(c, new VoteCommand { Accept = true }, 1, Both);
            Assert.Null(c.Run);
            // Healed, and each pack with what it held.
            Assert.Equal(Runs.MaxHp(s), Runs.HpOf(c, s));
            Assert.Contains(s.Kit!.Pack.Items, (p) => p.Gear == med);
            Assert.Equal(RunTexts.RunEnded, c.Log[0].Text);
            Refused(RunTexts.NoRun, c, new EndRunCommand());
            // Not from a stop.
            GoTo(c, NodeKinds.Trade);
            Refused(RunTexts.EndFromMap, c, new EndRunCommand(), 0, Both);
        }

        [Fact]
        public void FallenSquadsLeaveARecruitWhenTheRunEnds()
        {
            var c = Solo();
            Start(c);
            foreach (var s in c.Soldiers) s.Alive = false;
            Ok(c, new EndRunCommand());
            Assert.Single(c.Soldiers, (s) => s.Alive);
            Assert.Empty(c.Squad);
        }

        // ------------------------------------------------------------- hands, control

        [Fact]
        public void APartnerWhoJoinsARunIsHandedHalf()
        {
            var c = Solo();
            Campaign.AddPlayer(c, 1, new Rng(1));
            // The partner's rookies were not in the squad picked alone: give it all four of player 0's soldiers.
            c.Squad = c.Soldiers.Where((s) => s.Owner == 0).Select((s) => s.Id).Concat(c.Soldiers.Where((s) => s.Owner == 1).Select((s) => s.Id)).Take(4).ToList();
            var squad = c.Squad.ToList();
            foreach (var id in squad) c.Soldiers.First((s) => s.Id == id).Owner = 0;
            Ok(c, new StartRunCommand(), 0, Zero);
            Assert.Equal(0, Runs.HandOver(c, 1, Zero));
            Assert.Equal(2, Runs.HandOver(c, 1, Both));
            Assert.Equal(squad.Skip(2), c.Run!.Hands!.Keys);
            Assert.All(c.Run.Hands.Values, (v) => Assert.Equal(1, v));
            Assert.Equal(0, Runs.HandOver(c, 1, Both));
            var handed = c.Soldiers.First((s) => s.Id == squad[3]);
            var kept = c.Soldiers.First((s) => s.Id == squad[0]);
            Assert.True(Runs.Controls(c, 1, Both, handed));
            Assert.False(Runs.Controls(c, 0, Both, handed));
            Assert.True(Runs.Controls(c, 0, Both, kept));
            Assert.False(Runs.Controls(c, 1, Both, kept));
            // Whoever is away is commanded by the one here.
            Assert.True(Runs.Controls(c, 0, Zero, handed));
            // Gone with the run.
            Ok(c, new EndRunCommand());
            Assert.True(Runs.Controls(c, 0, Both, handed));
            Assert.False(Runs.Controls(c, 1, Both, handed));
        }
    }
}
