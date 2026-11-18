using System.Collections.Generic;
using System.Linq;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// What the test apps' bots do out of a fight in the runs (HeardGame.BaseOrder, H0-DISENO §7; Gameplay, 16/11): go out,
    /// take the first node in sight, back before the boss only with the squad broken, answer a stop yes and buy something at a Trade; and see to
    /// their soldiers' kit, so they don't go out with the knife for ever. Each order one the engine takes.
    /// </summary>
    public class NetRunBotTests
    {
        /// <summary>What an app has heard: `slots` connected, and the campaign as it is now.</summary>
        private static HeardGame Heard(CampaignState c, params int[] slots) => Heard(c, new FakeClock(), slots);

        private static HeardGame Heard(CampaignState c, FakeClock clock, params int[] slots)
        {
            var heard = new HeardGame(clock);
            heard.Observe(new RoomMessage { Phase = RoomPhases.Base, Players = slots.Select((s) => new PlayerInfo { Slot = s, Name = $"P{s}", Connected = true }).ToList() });
            heard.Observe(new CampaignMessage { State = c });
            return heard;
        }

        /// <summary>The order `slot`'s bot gives, done as the room would (the engine refusing nothing).</summary>
        private static CampaignCommand Done(HeardGame heard, CampaignState c, int slot, IReadOnlyList<int> connected)
        {
            var order = heard.BaseOrder(slot, slot == 0);
            Assert.NotNull(order);
            Ok(c, order!, slot, connected);
            return order!;
        }

        [Fact]
        public void OnTheBaseTheHostProposesGoingOutAndThePartnerSaysYes()
        {
            var c = Two();
            var (host, guest) = (Heard(c, 0, 1), Heard(c, 0, 1));
            Assert.Null(guest.BaseOrder(1, false));
            Assert.IsType<StartRunCommand>(Done(host, c, 0, Both));
            Assert.Null(host.BaseOrder(0, true));
            Assert.Equal(true, Assert.IsType<VoteCommand>(Done(guest, c, 1, Both)).Accept);
            Assert.NotNull(c.Run);
        }

        [Fact]
        public void ASoldierTakesAFirearmFromTheStashAndThePackGoesBackToIt()
        {
            var c = Solo();
            var rifle = InStash(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, 0);
            var s = Soldier(c);
            var bat = InPack(c, s, GearKinds.Weapon, FoundWeaponIds.Bat, 0, 0);
            var heard = Heard(c, 0);
            var take = Assert.IsType<MoveGearCommand>(Done(heard, c, 0, Zero));
            Assert.Equal((rifle.Id, s.Id, KitSlots.Primary), (take.Gear, take.To, take.Slot));
            Assert.Same(rifle, s.Kit!.Primary);
            // What the pack carries for no use, to the stash; then out.
            var back = Assert.IsType<MoveGearCommand>(Done(heard, c, 0, Zero));
            Assert.Equal((bat.Id, GearPlaces.Stash), (back.Gear, back.To));
            Assert.Contains(c.Stash!.Items, (p) => p.Gear == bat);
            Assert.IsType<StartRunCommand>(heard.BaseOrder(0, true));
        }

        [Fact]
        public void OnTheBaseTheSuppliesGoToTheStashAndPayTheUpgradesInTurn()
        {
            // As a game starts it: 2 seats (N4).
            var c = Campaign.CreateRunsCampaign(Zero, new Rng(1));
            var s = Soldier(c);
            var kept = InStash(c, GearKinds.Supplies, null, 0, 0, 90);
            InPack(c, s, GearKinds.Supplies, null, 0, 0, 70);
            var heard = Heard(c, 0);
            // Onto the stash's pile first, then the rest where it fits.
            var onto = Assert.IsType<MoveGearCommand>(Done(heard, c, 0, Zero));
            Assert.Equal((GearPlaces.Stash, 0, 0), (onto.To, onto.X, onto.Y));
            var next = heard.BaseOrder(0, true);
            while (next is MoveGearCommand)
            {
                Ok(c, next, 0, Zero);
                next = heard.BaseOrder(0, true);
            }
            Assert.Equal(Content.PileOf(GearKinds.Supplies, null), kept.Count);
            Assert.Equal((160, 0), (Runs.StashSupplies(c), Gears.Supplies(s.Kit!.Pack)));
            // More soldiers out (40) first; then the mending (120), and then saving, out meanwhile.
            Assert.Equal(UpgradeIds.Squad, Assert.IsType<UpgradeCommand>(next).Upgrade);
            Ok(c, next!, 0, Zero);
            Assert.Equal((1, 120), (Runs.Level(c, UpgradeIds.Squad), Runs.StashSupplies(c)));
            // Its soldiers into the seats it bought.
            next = heard.BaseOrder(0, true);
            while (next is SquadCommand)
            {
                Ok(c, next, 0, Zero);
                next = heard.BaseOrder(0, true);
            }
            Assert.Equal(4, c.Squad.Count);
            Assert.Equal(UpgradeIds.Mend, Assert.IsType<UpgradeCommand>(next).Upgrade);
            Ok(c, next!, 0, Zero);
            Assert.IsType<StartRunCommand>(heard.BaseOrder(0, true));
        }

        [Fact]
        public void OnTheMapTheHostChoosesTheFirstNodeInSight()
        {
            var c = Solo();
            var run = Start(c);
            var heard = Heard(c, 0);
            var choose = Assert.IsType<ChooseNodeCommand>(Done(heard, c, 0, Zero));
            Assert.Equal(RunMap.Choices(new RunState { Nodes = run.Nodes })[0].Id, choose.Node);
            Assert.Equal(1, c.NodesPlayed);
        }

        [Fact]
        public void BackToTheBaseOnceTheSquadIsBroken()
        {
            var c = Solo();
            var run = Start(c);
            // A run of before G22's: home from the map (GoesHomeFromARefuge: the new ones).
            run.HomeFromRefuge = null;
            var heard = Heard(c, 0);
            Assert.IsType<ChooseNodeCommand>(heard.BaseOrder(0, true));
            // After a fight, one of four fallen and the rest hurt, but over half their health: on.
            var first = RunMap.Choices(run)[0];
            run.Path.Add(first.Id);
            run.At = first.Id;
            var squad = Squad(c);
            Assert.Equal(4, squad.Count);
            squad[0].Alive = false;
            run.Squad.Remove(squad[0].Id);
            foreach (var s in squad.Skip(1)) run.Hp[s.Id] = Runs.MaxHp(s) / 2 + 1;
            Assert.IsType<ChooseNodeCommand>(heard.BaseOrder(0, true));
            // At half their health, back; as with two of four fallen.
            foreach (var s in squad.Skip(1)) run.Hp[s.Id] = Runs.MaxHp(s) / 2;
            Assert.IsType<EndRunCommand>(heard.BaseOrder(0, true));
            foreach (var s in squad.Skip(1)) run.Hp[s.Id] = Runs.MaxHp(s);
            squad[1].Alive = false;
            run.Squad.Remove(squad[1].Id);
            Assert.IsType<EndRunCommand>(Done(heard, c, 0, Zero));
            Assert.Null(c.Run);
        }

        [Fact]
        public void GoesHomeFromARefuge()
        {
            // G22: a broken squad heads for a Refuge and goes home from it; not from the map.
            var c = Solo();
            var run = Start(c);
            var heard = Heard(c, 0);
            var first = RunMap.Choices(run)[0];
            run.Path.Add(first.Id);
            run.At = first.Id;
            var squad = Squad(c);
            foreach (var s in squad.Take(2)) { s.Alive = false; run.Squad.Remove(s.Id); }
            // On the map: a Refuge among the choices is chosen.
            var refuge = RunMap.Choices(run)[RunMap.Choices(run).Count - 1];
            refuge.Kind = NodeKinds.Refuge;
            refuge.Site = null;
            refuge.Seed = null;
            var go = Assert.IsType<ChooseNodeCommand>(Done(heard, c, 0, Zero));
            Assert.Equal(refuge.Id, go.Node);
            // At it: home.
            Assert.IsType<EndRunCommand>(Done(heard, c, 0, Zero));
            Assert.Null(c.Run);
        }

        [Fact]
        public void AWoundedSoldierGetsAMedicineBeforeTheWayOn()
        {
            var c = Solo();
            var run = Start(c);
            var s = Soldier(c);
            run.Hp[s.Id] = Runs.MaxHp(s) - Content.MedicineHeal - 1;
            var medicine = InPack(c, Soldier(c, 0, 1), GearKinds.Medicine, null, 0, 0, 2);
            var heard = Heard(c, 0);
            var use = Assert.IsType<UseGearCommand>(Done(heard, c, 0, Zero));
            Assert.Equal((medicine.Id, s.Id), (use.Gear, use.Soldier));
            Assert.Equal(Runs.MaxHp(s) - 1, run.Hp[s.Id]);
            // One short of full: not worth a medicine.
            Assert.IsType<ChooseNodeCommand>(heard.BaseOrder(0, true));
        }

        [Fact]
        public void AtAnEventTheHostAnswersYesAndBothAskToGoOn()
        {
            var c = Two();
            GoTo(c, NodeKinds.Event);
            // H4's real events: one whose yes gives nothing to wear (the farmacia's botiquín would go to the belt first).
            c.Run!.Stop!.Event = "gasolinera";
            var (host, guest) = (Heard(c, 0, 1), Heard(c, 0, 1));
            Assert.Null(guest.BaseOrder(1, false));
            Assert.Equal(0, Assert.IsType<AnswerStopCommand>(Done(host, c, 0, Both)).Option);
            Done(guest, c, 1, Both);
            Assert.NotNull(c.Run!.Stop!.Outcome);
            Assert.IsType<LeaveStopCommand>(Done(host, c, 0, Both));
            // Asked: the host waits for the guest.
            Assert.Null(host.BaseOrder(0, true));
            Assert.IsType<LeaveStopCommand>(Done(guest, c, 1, Both));
            Assert.Equal(RunPhases.Choose, c.Run.Phase);
        }

        [Fact]
        public void AskedToGoOnAndThePartnerGoneItAsksAgain()
        {
            var c = Two();
            GoTo(c, NodeKinds.Refuge);
            var clock = new FakeClock();
            var host = Heard(c, clock, 0, 1);
            Assert.IsType<LeaveStopCommand>(Done(host, c, 0, Both));
            Assert.Null(host.BaseOrder(0, true));
            // Dropped: it still counts as here for the host's grace (HeardGame.Active), then not.
            host.Observe(new RoomMessage { Phase = RoomPhases.Base, Players = new List<PlayerInfo> { new PlayerInfo { Slot = 0, Connected = true }, new PlayerInfo { Slot = 1, Connected = false } } });
            Assert.Null(host.BaseOrder(0, true));
            clock.Advance(Room.ReconnectGraceMs);
            Assert.IsType<LeaveStopCommand>(Done(host, c, 0, Zero));
            Assert.Equal(RunPhases.Choose, c.Run!.Phase);
        }

        [Fact]
        public void AtATradeItBuysOneWareAndGoesOn()
        {
            var c = Solo();
            var s = Soldier(c);
            InPack(c, s, GearKinds.Supplies, null, 0, 0, 100);
            GoTo(c, NodeKinds.Trade);
            var wares = c.Run!.Stop!.Wares!.Count;
            var heard = Heard(c, 0);
            var buy = Assert.IsType<BuyWareCommand>(Done(heard, c, 0, Zero));
            Assert.Equal(wares - 1, c.Run.Stop!.Wares!.Count);
            Assert.Equal(buy.Soldier, Gears.Find(c, buy.Ware)!.Soldier!.Id);
            // One only, then its kit (a firearm bought goes to its place), then on.
            var next = heard.BaseOrder(0, true);
            while (next is MoveGearCommand)
            {
                Ok(c, next, 0, Zero);
                next = heard.BaseOrder(0, true);
            }
            Assert.IsType<LeaveStopCommand>(next);
        }

        [Fact]
        public void InAFightItWaits()
        {
            var c = Solo();
            GoTo(c, NodeKinds.Normal);
            Assert.Equal(RunPhases.Fight, c.Run!.Phase);
            Assert.Null(Heard(c, 0).BaseOrder(0, true));
        }
    }
}
