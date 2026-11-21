using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G23 Las cargas (Arturo, cuadros 105 and 109; .dev-snapshots/gameplay/g23/CARGAS.md), off the maps till its R1: a nest
    /// of the dead (a building, its dead asleep inside) with three points by its walls outside; three charges carried in
    /// the packs, set one by one beside a point (an action, heard 2 tiles off); the third set, a count of 4 enemy turns; at
    /// 0 the three go off (10 to whoever is within 4 of one, the squad too) and La Lata goes, whoever is out left behind. Won
    /// with the three set and somebody aboard; leaving before, «superada, sin la recompensa». The charges never go home.
    /// </summary>
    public class RunChargesTests
    {
        private static (CampaignState C, GameState S) Nest(int seed = 2, string site = Places.Town, bool two = false)
        {
            var c = two ? Two(seed) : Solo(seed);
            var run = Start(c);
            var n = RunMap.Choices(run)[0];
            n.Kind = NodeKinds.Charges;
            n.Modifier = null;
            n.Seed = 100 + seed;
            n.Site = site;
            n.Difficulty = 2;
            if (site == Places.Town) n.Site = Places.Of(RunMap.MapOf(n)!);
            var cmd = new ChooseNodeCommand { Node = n.Id };
            if (two) Agree(c, cmd);
            else Assert.True(Ok(c, cmd).Launch);
            var s = new Bastion.Net.EngineCampaignRules().LaunchMission(c, new Rng(seed + 100))!.Value.state;
            Assert.NotNull(s.Mission.Charges);
            return (c, s);
        }

        private static List<Unit> Squad(GameState s) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive).ToList();

        private static Pos Bus(GameState s) => s.Mission.EvacZone.Select((v) => new Pos { X = (int)v.X, Y = (int)v.Y, Z = 0 }).First((p) => Abilities.InEvacZone(s, p));

        /// <summary>Everyone of the dead out of the way, so that nothing but the rules acts.</summary>
        private static void NoDead(GameState s)
        {
            foreach (var u in s.Units.Where((u) => u.Team == Teams.Alien)) u.Alive = false;
        }

        /// <summary>`u` with a charge beside the first point still to set, its actions back.</summary>
        private static void Beside(GameState s, Unit u, int nth = 0)
        {
            var p = Rules.ChargeAt(s, s.Mission.Charges!.Points.Where((x) => !x.Placed).ElementAt(nth));
            u.Pos = p;
            u.Ap = 2;
        }

        private static Gear Charge(GameState s, Unit u)
        {
            var g = new Gear { Id = $"g{++s.Run!.NextGear}", Kind = GearKinds.Charge };
            Assert.True(Gears.CanStow(u.Pack!, g));
            Gears.Stow(u.Pack!, g);
            return g;
        }

        [Fact]
        public void AStageOffTheMapsAtItsSitesNeverASpecialMapNorALooseSurvivorNorAwake()
        {
            Assert.Contains(NodeKinds.Charges, NodeKinds.All);
            Assert.DoesNotContain(NodeKinds.Charges, RunMap.Specials);
            Assert.Equal("Las cargas", Lore.StageName(NodeKinds.Charges));
            Assert.Equal(MissionKinds.Scavenge, Runs.MissionKindOf(NodeKinds.Charges));
            foreach (var seed in Enumerable.Range(1, 100)) Assert.Contains(RunMap.SiteOf(NodeKinds.Charges, 3, new Rng(seed)), RunMap.ChargesSites);
            for (var seed = 0; seed < 50; seed++) Assert.Null(RunMap.SpecialFor(new RunNode { Kind = NodeKinds.Charges, Row = 3, Seed = seed * RunMap.HospitalOdds, Site = Places.Town }));
            var loose = Enumerable.Range(1, 400).First((x) => RunMap.LooseSurvivor(new RunNode { Kind = NodeKinds.Normal, Row = 3, Seed = x }));
            Assert.False(RunMap.LooseSurvivor(new RunNode { Kind = NodeKinds.Charges, Row = 3, Seed = loose }));
            Assert.DoesNotContain(Modifiers.Awake, RunMap.ModifiersFor(NodeKinds.Charges));
            Assert.Contains(GearKinds.Charge, GearKinds.All);
            Assert.True(Content.IsGear(GearKinds.Charge, null));
        }

        [Fact]
        public void TheNestItsThreePointsItsDeadAndTheChargesInThePacks()
        {
            foreach (var site in RunMap.ChargesSites)
                foreach (var seed in new[] { 2, 3, 4 })
                {
                    var (_, s) = Nest(seed, site);
                    var c = s.Mission.Charges!;
                    Assert.Equal(3, c.Points.Count);
                    Assert.Null(c.Countdown);
                    var points = c.Points.Select((p) => Rules.ChargeAt(s, p)).ToList();
                    // Outside the nest, on the ground, each walked to from the squad, apart.
                    var reach = Paths.ComputeReach(s, Squad(s)[0], 1_000_000, ignore: (_) => true).Cost;
                    Assert.All(points, (p) => Assert.True(reach.ContainsKey(Grid.NodeOf(s, p)), $"{site} {seed}: {p.X},{p.Y}"));
                    Assert.All(c.Points, (p) => Assert.DoesNotContain(p.Tile, c.Nest));
                    for (var i = 0; i < 3; i++)
                        for (var j = i + 1; j < 3; j++) Assert.True(System.Math.Max(System.Math.Abs(points[i].X - points[j].X), System.Math.Abs(points[i].Y - points[j].Y)) >= Setup.ChargesApart);
                    // Its dead asleep inside it.
                    var nest = s.Pods.Single((p) => p.Id == "pn");
                    Assert.False(nest.Active);
                    Assert.All(nest.UnitIds.Select((id) => Grid.GetUnit(s, id)!), (u) => Assert.Contains(u.Pos.Y * s.Width + u.Pos.X, c.Nest));
                    Assert.Equal(Setup.NestDead[1], nest.UnitIds.Count);
                    // The three charges in the packs, one each.
                    var carried = Squad(s).Select((u) => u.Pack!.Items.Count((p) => p.Gear.Kind == GearKinds.Charge)).ToList();
                    Assert.Equal(3, carried.Sum());
                    Assert.True(carried.Max() == 1 || Squad(s).Count < 3);
                }
        }

        [Fact]
        public void WithTwoSeatsTwoToTheFirstAndTwoPlayersInTurn()
        {
            var (_, s) = Nest(2, two: true);
            var squad = Squad(s);
            var carriers = squad.Where((u) => Rules.ChargeOf(u) != null).ToList();
            // Alternating players: both have one.
            Assert.Contains(carriers, (u) => u.Owner == 0);
            Assert.Contains(carriers, (u) => u.Owner == 1);
        }

        [Fact]
        public void SettingOneQuietlyBesideItsPoint()
        {
            var (_, s) = Nest();
            NoDead(s);
            s.Concealed = true;
            var u = Squad(s).First((x) => Rules.ChargeOf(x) != null);
            var gear = Rules.ChargeOf(u)!;
            Assert.Contains(AbilityIds.PlaceCharge, Abilities.ChargeAbilities(s, u));
            Assert.DoesNotContain(AbilityIds.PlaceCharge, Abilities.AbilitiesOf(u));
            // Not beside a point.
            u.Pos = Bus(s);
            u.Ap = 2;
            Assert.Equal("No hay ningún punto de carga al lado.", Abilities.AbilityBlocker(s, u, AbilityIds.PlaceCharge));
            Beside(s, u);
            var r = Exec(s, Ability(u.Id, AbilityIds.PlaceCharge));
            Assert.True(r.Ok, r.Error);
            var e = Of<ChargePlacedEvent>(r.Events!).Single();
            Assert.Equal((u.Id, gear.Id, 2), (e.Unit, e.Gear, e.Left));
            Assert.Single(s.Mission.Charges!.Points, (p) => p.Placed);
            Assert.DoesNotContain(u.Pack!.Items, (p) => p.Gear.Id == gear.Id);
            Assert.Null(s.Mission.Charges.Countdown);
            Assert.True(s.Concealed);
            // Heard 2 tiles off (the squad's a little more: Rules.MakeNoise).
            Assert.InRange(Of<NoiseEvent>(r.Events!).Single().Radius, 2, 3);
            // Without one, nothing to set.
            foreach (var p in u.Pack!.Items.Where((x) => x.Gear.Kind == GearKinds.Charge).ToList()) u.Pack.Items.Remove(p);
            Assert.Empty(Abilities.ChargeAbilities(s, u));
            Assert.Equal("No tiene esa habilidad.", Abilities.AbilityBlocker(s, u, AbilityIds.PlaceCharge));
        }

        [Fact]
        public void TheThirdStartsTheCountAtZeroTheyBlowAndLaLataGoes()
        {
            var (c, s) = Nest();
            NoDead(s);
            var squad = Squad(s);
            var setter = squad[0];
            foreach (var p in setter.Pack!.Items.Where((x) => x.Gear.Kind == GearKinds.Charge).ToList()) setter.Pack.Items.Remove(p);
            for (var k = 0; k < 3; k++) Charge(s, setter);
            for (var k = 0; k < 3; k++)
            {
                Beside(s, setter);
                Assert.True(Exec(s, Ability(setter.Id, AbilityIds.PlaceCharge)).Ok);
            }
            Assert.True(Rules.ChargesSet(s));
            Assert.Equal(Setup.ChargesTurns, s.Mission.Charges!.Countdown);
            // One of the squad left by a point; the rest aboard.
            var by = Rules.ChargeAt(s, s.Mission.Charges.Points[0]);
            setter.Pos = by;
            var left = squad.Count > 1 ? squad[1] : null;
            foreach (var u in squad.Where((x) => x != setter)) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            var ticks = new List<int>();
            var last = new List<GameEvent>();
            for (var turn = 0; turn < Setup.ChargesTurns && s.Outcome == null; turn++)
            {
                var sim = NewSim(s);
                sim.RunAlienTurn();
                ticks.AddRange(Of<ChargesTickEvent>(sim.Events).Select((e) => e.Left));
                last = sim.Events.ToList();
                if (s.Outcome == null) s.ActiveTeam = Teams.Xcom;
            }
            Assert.Equal(new[] { 3, 2, 1, 0 }, ticks);
            Assert.True(s.Mission.Charges.Blown);
            Assert.Single(Of<ChargesBlownEvent>(last));
            // Everyone within 4 of a point, 10, through armour: the one of the squad out there, and the dead (the horde come in).
            Assert.Contains(Of<DamagedEvent>(last), (d) => d.Unit == setter.Id && d.Amount == Setup.ChargesDamage);
            var points = s.Mission.Charges.Points.Select((p) => Rules.ChargeAt(s, p)).ToList();
            Assert.DoesNotContain(s.Units, (u) => u.Alive && u.Team == Teams.Alien && points.Any((p) => Js.Hypot(u.Pos.X - p.X, u.Pos.Y - p.Y) <= Setup.ChargesRadius));
            // La Lata gone: the one out, dead or left behind.
            Assert.Contains(last, (e) => e is EvacDepartedEvent);
            Assert.True(!setter.Alive || setter.Lost == true);
            Assert.NotNull(s.Outcome);
            if (left != null)
            {
                Assert.Equal((Outcomes.Victory, EndReasons.Objective), (s.Outcome, s.OutcomeReason));
                var report = EndFight(c, s, s.Outcome!, s.OutcomeReason!);
                Assert.True(report.Victory);
            }
        }

        [Fact]
        public void LeavingBeforeTheThreeNoRewardTheRunGoesOnAndTheyNeverGoHome()
        {
            var (c, s) = Nest();
            NoDead(s);
            var squad = Squad(s);
            Assert.False(Rules.EvacWins(s));
            foreach (var u in squad) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            Assert.False(Rules.EvacWins(s));
            Assert.True(Exec(s, new EvacCommand { Unit = squad[0].Id }).Ok);
            Assert.Equal((Outcomes.Defeat, EndReasons.Abandoned), (s.Outcome, s.OutcomeReason));
            var report = EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.False(report.Victory);
            Assert.Null(report.Run!.Reward);
            Assert.NotNull(c.Run);
            Assert.All(c.Soldiers.Where((x) => x.Alive && x.Kit != null), (x) => Assert.DoesNotContain(x.Kit!.Pack.Items, (p) => p.Gear.Kind == GearKinds.Charge));
        }

        [Fact]
        public void AStateWithoutItIsAsBefore()
        {
            var (_, s) = LaunchAt(NodeKinds.Normal);
            Assert.Null(s.Mission.Charges);
            Assert.False(Rules.ChargesSet(s));
            Assert.Empty(Abilities.ChargeAbilities(s, Squad(s)[0]));
            var copy = Events.CloneState(s);
            Assert.Null(copy.Mission.Charges);
        }
    }
}
