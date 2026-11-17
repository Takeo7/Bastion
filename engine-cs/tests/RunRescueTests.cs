using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using Xunit.Abstractions;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// A run's Rescate (N2, Arturo 17/11; .dev-snapshots/gameplay/n2-n4): the survivor waits on the map among the dead, a
    /// recruit with a knife, nobody's and not shown till found; talked to from beside them (Hablar), they join, the talker's
    /// to command with two actions; La Lata takes its seats and who doesn't fit is lost; the survivor fallen, the fight goes
    /// on as a Saqueo; brought out, the talker's soldier.
    /// </summary>
    public class RunRescueTests
    {
        private readonly ITestOutputHelper output;

        public RunRescueTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static Unit Survivor(GameState s) => s.Units.Single((u) => u.Id == s.Mission.ObjectiveUnit);

        private static List<Unit> Squaddies(GameState s) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).ToList();

        /// <summary>A tile beside the survivor, on their floor, a soldier can stand on and talk from, free.</summary>
        private static Pos Beside(GameState s, Unit vip)
        {
            foreach (var d in Grid.Dirs8)
            {
                var p = new Pos { X = vip.Pos.X + d.X, Y = vip.Pos.Y + d.Y, Z = vip.Pos.Z };
                if (Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null && Looting.ReachesToSearch(s, p, vip.Pos)) return p;
            }
            throw new InvalidOperationException("nowhere beside the survivor");
        }

        /// <summary>Everyone given standing in La Lata's zone, on its free tiles.</summary>
        private static void Aboard(GameState s, IEnumerable<Unit> units)
        {
            var free = s.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).Where((p) => Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null).ToList();
            var i = 0;
            foreach (var u in units) u.Pos = free[i++];
        }

        /// <summary>A Rescate launched with the «squad» upgrade at `level` (null: as the helpers have it, 4 seats).</summary>
        private static (CampaignState C, GameState S) Rescue(int seed = 3, int? level = null, bool two = false)
        {
            if (!two) return LaunchAt(NodeKinds.Rescue, seed, before: (c) => Seats(c, level));
            var d = Two(seed);
            Seats(d, level);
            GoTo(d, NodeKinds.Rescue);
            return (d, new EngineCampaignRules().LaunchMission(d, new Rng(seed + 100))!.Value.state);
        }

        private static void Seats(CampaignState c, int? level)
        {
            if (level == null) return;
            c.Upgrades![UpgradeIds.Squad] = level.Value;
            c.Squad = Runs.DefaultSquad(c, c.Players);
        }

        [Fact]
        public void TheSurvivorWaitsWithAKnifeAmongTheDead()
        {
            var (c, s) = Rescue();
            var vip = Survivor(s);
            Assert.Equal(MissionKinds.Rescue, s.Mission.Kind);
            Assert.True(vip.Captive);
            Assert.Null(vip.Owner);
            Assert.Null(vip.CampaignId);
            Assert.NotEqual(TemplateIds.Vip, vip.Template);
            // A recruit's name (Lore, N8), the knife alone, two actions once they join.
            Assert.Contains(Content.SoldierNames, (n) => n.Name == vip.Name);
            Assert.Null(vip.Arms!.Melee);
            Assert.False(Content.HasGun(vip));
            Assert.Null(vip.Pack);
            Assert.Equal(0, vip.Ap);
            // Among the dead, as on a Saqueo: no Cuervos.
            Assert.All(s.Units.Where((u) => u.Team == Teams.Alien), (u) => Assert.True(Content.Templates[u.Template].Undead, u.Template));
            // The circle to look in holds them; nobody sees them yet.
            Assert.NotNull(s.Mission.SearchArea);
            Assert.True(Js.Hypot(vip.Pos.X - s.Mission.SearchArea!.Center.X, vip.Pos.Y - s.Mission.SearchArea.Center.Y) <= Setup.SearchRadius);
            Assert.Null(s.Mission.SurvivorFound);
            Assert.False(Rules.SurvivorShown(s, vip));
            Assert.True(Rules.SurvivorShown(s, Squaddies(s)[0]));
            Assert.Equal(Runs.SeatsOf(c), s.Mission.Seats);
            // The same node, the same survivor.
            var (_, again) = Rescue();
            Assert.Equal((vip.Name, vip.MaxHp, vip.Appearance!.Head), (Survivor(again).Name, Survivor(again).MaxHp, Survivor(again).Appearance!.Head));
        }

        [Fact]
        public void TheyAreTalkedToFromBesideThemAndJoin()
        {
            var (_, s) = Rescue();
            var vip = Survivor(s);
            var u = Squaddies(s)[0];
            Assert.Contains(AbilityIds.Talk, Abilities.AbilitiesOf(u));
            Assert.Equal("Ponte al lado de quien pide ayuda.", Abilities.AbilityBlocker(s, u, AbilityIds.Talk));
            u.Pos = Beside(s, vip);
            // Standing beside them doesn't free them, as walking up to a VIP did.
            Assert.True(vip.Captive);
            Assert.Same(vip, Rules.SurvivorSeen(s, u, u.Pos));
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.Talk));
            var ap = u.Ap;
            var r = Exec(s, Ability(u.Id, AbilityIds.Talk));
            Assert.True(r.Ok, r.Error);
            var joined = Assert.Single(Of<VipRescuedEvent>(r.Events));
            Assert.Equal((vip.Id, u.Id, u.Owner), (joined.Unit, joined.By, joined.Owner));
            Assert.False(vip.Captive);
            Assert.Equal(u.Owner, vip.Owner);
            Assert.Equal(ap - 1, u.Ap);
            // Theirs to command at once: their two actions and their walk.
            Assert.Equal(Content.ApPerTurnOf(vip), vip.Ap);
            Assert.Null(vip.Moves);
            Assert.True(s.Mission.SurvivorFound);
            Assert.True(Rules.SurvivorShown(s, vip));
            Assert.Equal("No hay nadie con quien hablar.", Abilities.AbilityBlocker(s, Squaddies(s)[1], AbilityIds.Talk));
            // Outside a run's Rescate, nobody to talk to.
            var (_, other) = LaunchAt(NodeKinds.Normal, node: (n) => { while (RunMap.LooseSurvivor(n)) n.Seed++; });
            Assert.Equal("No hay nadie con quien hablar.", Abilities.AbilityBlocker(other, Squaddies(other)[0], AbilityIds.Talk));
        }

        [Fact]
        public void FoundOnceSeen()
        {
            var (_, s) = Rescue();
            var vip = Survivor(s);
            var u = Squaddies(s)[0];
            var to = Beside(s, vip);
            // A step up to them: seen on the way, shown from then on, once.
            var from = Grid.Dirs8.Select((d) => new Pos { X = to.X + d.X, Y = to.Y + d.Y, Z = to.Z })
                .First((p) => Grid.InBounds(s, p.X, p.Y) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null && !Grid.SamePos(p, vip.Pos) && Grid.Distance(p, vip.Pos) > 1.5);
            u.Pos = from;
            var r = Exec(s, Move(u.Id, to.X, to.Y));
            Assert.True(r.Ok, r.Error);
            var found = Assert.Single(Of<SurvivorFoundEvent>(r.Events));
            Assert.Equal((vip.Id, u.Id), (found.Unit, found.By));
            Assert.True(s.Mission.SurvivorFound);
            Assert.True(Rules.SurvivorShown(s, vip));
            Assert.Null(Rules.SurvivorSeen(s, u, u.Pos));
            // Still waiting to be talked to.
            Assert.True(vip.Captive);
        }

        [Fact]
        public void TheSurvivorFallenTheFightGoesOnAsASaqueo()
        {
            var (c, s) = Rescue();
            var vip = Survivor(s);
            var u = Squaddies(s)[0];
            u.Pos = Beside(s, vip);
            Assert.True(Exec(s, Ability(u.Id, AbilityIds.Talk)).Ok);
            vip.Hp = 0;
            vip.Alive = false;
            Assert.Null(s.Outcome);
            Aboard(s, Squaddies(s));
            Assert.True(Rules.EvacWins(s));
            var r = Exec(s, new EvacCommand { Unit = u.Id });
            Assert.True(r.Ok, r.Error);
            Assert.Equal((Outcomes.Victory, EndReasons.Objective), (s.Outcome, s.OutcomeReason));
            var report = EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.Null(report.Run!.Joined);
        }

        [Fact]
        public void WhoDoesntFitIsLeftBehindAndLost()
        {
            // Two seats alone (a new game's), both taken: the survivor in the zone too stays behind.
            var (c, s) = Rescue(level: 0);
            Assert.Equal(2, s.Mission.Seats);
            Assert.False(Runs.SeatFree(c));
            var vip = Survivor(s);
            var squad = Squaddies(s);
            Assert.Equal(2, squad.Count);
            squad[0].Pos = Beside(s, vip);
            Assert.True(Exec(s, Ability(squad[0].Id, AbilityIds.Talk)).Ok);
            Aboard(s, new[] { vip }.Concat(squad));
            // The squad's own first, whatever stands first.
            Assert.Equal(new[] { vip.Id }, Rules.NoSeat(s).Select((x) => x.Id));
            var r = Exec(s, new EvacCommand { Unit = squad[0].Id });
            Assert.True(r.Ok, r.Error);
            var left = Assert.Single(Of<LeftBehindEvent>(r.Events));
            Assert.Equal((vip.Id, (bool?)true), (left.Unit, left.NoSeat));
            Assert.True(vip.Lost);
            Assert.All(squad, (x) => Assert.True(x.Evacuated));
            Assert.Equal(Outcomes.Victory, s.Outcome);
            var report = EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.Null(report.Run!.Joined);
            Assert.DoesNotContain(c.Soldiers, (x) => x.Name == vip.Name);
            // With a seat free, nobody is left.
            var (d, t) = Rescue(level: 2);
            Assert.True(Runs.SeatFree(d));
            Aboard(t, Squaddies(t));
            Assert.Empty(Rules.NoSeat(t));
        }

        [Fact]
        public void BroughtOutTheyAreTheTalkersSoldier()
        {
            // Two players, six seats: Pepe's soldier talks, so the survivor is Pepe's, by their name and look.
            var (c, s) = Rescue(level: 2, two: true);
            var vip = Survivor(s);
            var pepes = Squaddies(s).First((x) => x.Owner == 1);
            pepes.Pos = Beside(s, vip);
            // Ana hands Pepe the command.
            Assert.True(Exec(s, new PassCommand(), slot: 0, connected: new[] { 0, 1 }).Ok);
            var talk = Exec(s, Ability(pepes.Id, AbilityIds.Talk), slot: 1, connected: new[] { 0, 1 });
            Assert.True(talk.Ok, talk.Error);
            Assert.Equal(1, vip.Owner);
            vip.Hp = Math.Max(1, vip.MaxHp - 2);
            Aboard(s, Squaddies(s).Concat(new[] { vip }));
            Assert.Empty(Rules.NoSeat(s));
            foreach (var x in Squaddies(s).Concat(new[] { vip })) x.Evacuated = true;
            var report = EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            var joined = c.Soldiers.Single((x) => x.Id == report.Run!.Joined);
            Assert.Equal((vip.Name, 1, FoundWeaponIds.Knife), (joined.Name, joined.Owner, joined.Kit!.Melee!.Type));
            Assert.Equal(vip.Appearance!.Head, joined.Appearance.Head);
            Assert.Equal(vip.MaxHp, Runs.MaxHp(joined));
            Assert.Contains(joined.Id, c.Run!.Squad);
            Assert.Equal(vip.Hp, c.Run.Hp[joined.Id]);
            Assert.True(report.Run.InSquad);
            // Pepe with no bed left: Ana's.
            var (d, t) = Rescue(level: 2, two: true);
            while (d.Soldiers.Count((x) => x.Owner == 1 && x.Alive) < Campaign.SoldierCap(d)) Runs.NewSoldier(d, 1, new Rng(d.Soldiers.Count));
            var other = Survivor(t);
            var by = Squaddies(t).First((x) => x.Owner == 1);
            by.Pos = Beside(t, other);
            Assert.True(Exec(t, new PassCommand(), slot: 0, connected: new[] { 0, 1 }).Ok);
            Assert.True(Exec(t, Ability(by.Id, AbilityIds.Talk), slot: 1, connected: new[] { 0, 1 }).Ok);
            other.Evacuated = true;
            var theirs = EndFight(d, t, Outcomes.Victory, EndReasons.Objective);
            Assert.Equal(0, d.Soldiers.Single((x) => x.Id == theirs.Run!.Joined).Owner);
        }

        /// <summary>A map seed past `from` whose normal fight has a survivor loose on it (RunMap.LooseSurvivor).</summary>
        private static int LooseSeed(int from = 1)
        {
            var seed = from;
            while (!RunMap.LooseSurvivor(new RunNode { Kind = NodeKinds.Normal, Row = 1, Seed = seed })) seed++;
            return seed;
        }

        [Fact]
        public void OneFightInSixHasOneLoose()
        {
            // Arturo, 17/11: «reclutas por cualquier mapa, con un ratio relativamente escaso»; never the Rescate's, nor the boss's.
            var fights = Enumerable.Range(1, 3000).Select((seed) => new RunNode { Kind = NodeKinds.Normal, Row = 2, Seed = seed }).ToList();
            var share = fights.Count(RunMap.LooseSurvivor) / (double)fights.Count;
            Assert.InRange(share, 0.13, 0.20);
            Assert.False(RunMap.LooseSurvivor(new RunNode { Kind = NodeKinds.Rescue, Seed = LooseSeed() }));
            Assert.False(RunMap.LooseSurvivor(new RunNode { Kind = NodeKinds.Boss, Seed = LooseSeed() }));
            Assert.False(RunMap.LooseSurvivor(new RunNode { Kind = NodeKinds.Trade, Seed = LooseSeed() }));
            // On the map: inside a building, far from the start, no mark nor circle; talked to and brought out, a soldier.
            var seed = LooseSeed(40);
            var (c, s) = LaunchAt(NodeKinds.Normal, node: (n) => { n.Seed = seed; n.Site = Places.Town; });
            var loose = s.Units.Single((u) => u.Id == s.Mission.Survivor);
            Assert.Equal("survivor", loose.Id);
            Assert.True(loose.Captive);
            Assert.Null(s.Mission.ObjectiveUnit);
            Assert.Null(s.Mission.SearchArea);
            Assert.True(s.Storeys![Grid.TileIndex(s, loose.Pos.X, loose.Pos.Y)] > 0);
            Assert.False(Rules.SurvivorShown(s, loose));
            var u = Squaddies(s)[0];
            var walk = Paths.ComputeReach(s, u, 1_000_000, ignore: (_) => true).Cost[Grid.NodeOf(s, loose.Pos)];
            Assert.True(walk >= Runs.LooseFrom, $"{walk}");
            u.Pos = Beside(s, loose);
            Assert.True(Exec(s, Ability(u.Id, AbilityIds.Talk)).Ok);
            Assert.False(loose.Captive);
            foreach (var x in Squaddies(s).Concat(new[] { loose })) x.Evacuated = true;
            var report = EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            Assert.Equal(loose.Name, c.Soldiers.Single((x) => x.Id == report.Run!.Joined).Name);
            // A map without: nobody.
            var (_, none) = LaunchAt(NodeKinds.Normal, node: (n) => { n.Seed = seed + 1; while (RunMap.LooseSurvivor(n)) n.Seed++; });
            Assert.Null(none.Mission.Survivor);
        }

        [Fact]
        public void SomeRescatesHaveCuervosAndNotAtTheStart()
        {
            // RunMap.GuardedRescue: from row GuardedFrom, one in GuardedOdds; the prisoner among two bands of the living.
            var maps = Enumerable.Range(1, 400).SelectMany((seed) => RunMap.Generate(1, seed)).Where((n) => n.Kind == NodeKinds.Rescue).ToList();
            Assert.DoesNotContain(maps, (n) => n.Guarded == true && n.Row < RunMap.GuardedFrom);
            var late = maps.Where((n) => n.Row >= RunMap.GuardedFrom).ToList();
            Assert.InRange(late.Count((n) => n.Guarded == true) / (double)late.Count, 0.22, 0.45);
            var (_, s) = LaunchAt(NodeKinds.Rescue, seed: 3, node: (n) => n.Guarded = true);
            Assert.True(s.Mission.Guarded);
            Assert.True(Survivor(s).Captive);
            Assert.Contains(s.Units, (u) => u.Team == Teams.Alien && !Content.Templates[u.Template].Undead);
            Assert.NotNull(s.Mission.SearchArea);
            Assert.Contains("Cuervos", Runs.StageLine(new RunNode { Kind = NodeKinds.Rescue, Guarded = true }));
        }

        [Fact]
        public void ARunWipedSaysItsRecruits()
        {
            // Red, N9: the recruits a run's end gives, by id, for whoever comes into the room after it.
            var (c, s) = LaunchAt(NodeKinds.Normal);
            foreach (var x in Squaddies(s))
            {
                x.Alive = false;
                x.Hp = 0;
            }
            var had = c.Soldiers.Select((x) => x.Id).ToList();
            var report = EndFight(c, s, Outcomes.Defeat, EndReasons.Wiped);
            Assert.Equal(RunEnds.Wiped, report.Run!.Ended);
            Assert.NotEmpty(report.Run.Recruits!);
            Assert.All(report.Run.Recruits!, (id) => Assert.DoesNotContain(id, had));
            Assert.Equal(c.Soldiers.Where((x) => !had.Contains(x.Id)).Select((x) => x.Id), report.Run.Recruits);
        }

        [Fact]
        public void TheBotsTalkToThemAndBringThemOut()
        {
            // MissionBot (Red's) plays the Rescate as the test apps do: talked to, and brought out in some.
            var talked = 0;
            var out_ = 0;
            for (var seed = 1; seed <= 6; seed++)
            {
                var c = Two(seed);
                Seats(c, 2);
                foreach (var x in c.Soldiers)
                {
                    InSlot(c, x, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
                    InSlot(c, x, KitSlots.Pistol, GearKinds.Weapon, FoundWeaponIds.Pistol);
                    InPack(c, x, GearKinds.Mags, null, 0, 0, 3);
                }
                GoTo(c, NodeKinds.Rescue);
                var s = new EngineCampaignRules().LaunchMission(c, new Rng(seed * 31))!.Value.state;
                var rng = new Rng(seed * 7919 + 13);
                int orders = 0, refused = 0, idle = 0;
                while (s.Outcome == null && orders < 3000 && idle < 3)
                {
                    var any = false;
                    foreach (var slot in Both)
                    {
                        var cmd = MissionBot.Next(s, slot, Both);
                        if (cmd == null) continue;
                        orders++;
                        if (Rules.ExecuteCommand(s, cmd, slot, Both, rng).Ok) any = true;
                        else refused++;
                    }
                    idle = any ? 0 : idle + 1;
                }
                var vip = Survivor(s);
                if (!vip.Captive) talked++;
                if (vip.Evacuated) out_++;
                Assert.Equal(0, refused);
                output.WriteLine($"Rescate {seed} ({s.MapName}): {s.Outcome}/{s.OutcomeReason}, turno {s.Turn}, {(vip.Captive ? "sin hablarle" : vip.Evacuated ? "sacado" : vip.Alive ? "fuera" : "muerto")}, {orders} órdenes");
            }
            // 3 of 6 talked to and brought out (17/11); 2 since the bites going past (T20, 18/11). The least asked is under it:
            // the maps move on under it, and a bot that never talks talks to none.
            output.WriteLine($"hablados {talked} de 6, sacados {out_}");
            Assert.True(talked >= 1 && out_ >= 1, $"hablados {talked} de 6, sacados {out_}");
        }
    }
}
