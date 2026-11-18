using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The four new combat stages (H5; Lore's names Asedio, Travesía, Sigilo, Guarida): how each is set up, how each is won,
    /// and their own losses: no La Lata before its turn, the horde from behind, the wave when seen and the reward halved,
    /// leaving without the box.
    /// </summary>
    public class RunStageTests
    {
        private static Unit Squaddie(GameState s, int nth = 0) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).ElementAt(nth);

        /// <summary>A soldier on La Lata's zone, and the order to leave as the room gives it (alone: it goes at once).</summary>
        private static CommandResult Leave(GameState s)
        {
            var u = Squaddie(s);
            var z = s.Mission.EvacZone[0];
            u.Pos = new Pos { X = z.X, Y = z.Y, Z = 0 };
            u.Ap = 2;
            s.Command = 0;
            s.ActiveUnit = null;
            // In La Lata: a run's Evacuar only from inside (T24). Before an Asedio's comes, nobody is.
            if (s.Mission.HoldUntil == null || s.Turn >= s.Mission.HoldUntil) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            return Exec(s, new EvacCommand { Unit = u.Id });
        }

        private static double Gap(IEnumerable<Vec2> a, IEnumerable<Vec2> b) => a.Min((p) => b.Min((q) => Math.Max(Math.Abs(p.X - q.X), Math.Abs(p.Y - q.Y))));

        // ------------------------------------------------------------------ Asedio

        [Fact]
        public void AnAsedioWaitsForLaLataUnderWaves()
        {
            var (c, s) = LaunchAt(NodeKinds.Hold, 3);
            Assert.Equal((MissionKinds.Scavenge, Setup.HoldTurns), (s.Mission.Kind, s.Mission.HoldUntil!.Value));
            var waves = s.Mission.Reinforcements.Where((r) => r.Horde == true && r.Trickle != true).GroupBy((r) => r.Turn).ToDictionary((g) => g.Key, (g) => g.Sum((r) => r.Units.Count));
            Assert.Equal(new[] { 2, 4, 6, 8 }, waves.Keys.OrderBy((t) => t));
            Assert.True(waves[2] < waves[4] && waves[4] < waves[6] && waves[6] < waves[8], string.Join(", ", waves));
            Assert.All(s.Mission.Reinforcements, (r) => Assert.True(r.Units.Count <= 5));
            // No La Lata before its turn.
            var early = Leave(s);
            Assert.Equal((false, "La Lata llega en el turno 8."), (early.Ok, early.Error));
            s.Turn = Setup.HoldTurns;
            var r8 = Leave(s);
            Assert.True(r8.Ok, r8.Error);
            Assert.Equal(Outcomes.Victory, s.Outcome);
            Assert.Equal(Lore.StageLine(NodeKinds.Hold, holdTurns: 8), Runs.StageLine(RunMap.Node(c.Run!, c.Run!.At)!));
            // Won, a special's reward.
            var report = EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.InRange(report.Run!.Reward!.Value, Runs.RewardMin, Runs.RewardMax);
        }

        // ---------------------------------------------------------------- Travesía

        [Fact]
        public void ATravesiaStartsAtTheFarEnd()
        {
            var maps = new[] { (Places.Country, 101), (Places.Town, 202), (Places.Factory, 303), (Places.Estate, 404), (Places.Roadside, 505) };
            foreach (var (site, seed) in maps)
            {
                void Map(RunNode n)
                {
                    n.Site = site;
                    n.Seed = seed;
                }
                var (_, s) = LaunchAt(NodeKinds.Crossing, node: Map);
                var (_, plain) = LaunchAt(NodeKinds.Normal, node: Map);
                // The squad, not a survivor loose on the map (N2).
                var squad = s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).Select((u) => (Vec2)u.Pos).ToList();
                var plainSquad = plain.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).Select((u) => (Vec2)u.Pos).ToList();
                Assert.Equal(MissionKinds.Scavenge, s.Mission.Kind);
                Assert.True(Gap(squad, s.Mission.EvacZone) >= Gap(plainSquad, plain.Mission.EvacZone), $"seed {seed}");
                Assert.True(Gap(squad, s.Mission.EvacZone) >= Math.Max(s.Width, s.Height) / 2, $"seed {seed}: {Gap(squad, s.Mission.EvacZone)}");
                // Clear of the dead.
                Assert.True(Gap(squad, s.Units.Where((u) => u.Team == Teams.Alien).Select((u) => (Vec2)u.Pos)) >= 5, $"seed {seed}");
                // The horde behind them: by the edge they start at.
                var x = squad.Average((p) => p.X);
                var y = squad.Average((p) => p.Y);
                var gaps = new[] { y, s.Width - 1 - x, s.Height - 1 - y, x };
                Assert.Equal(Array.IndexOf(gaps, gaps.Min()), s.Mission.HordeSide);
                var sim = NewSim(s);
                sim.CallHorde(1);
                var incoming = Of<ReinforcementsIncomingEvent>(sim.Events);
                Assert.NotEmpty(incoming);
                Assert.All(incoming, (e) => Assert.Equal(s.Mission.HordeSide, e.R.Side));
                // Won by getting out.
                Assert.True(Leave(s).Ok);
                Assert.Equal(Outcomes.Victory, s.Outcome);
                Assert.Null(plain.Mission.HordeSide);
            }
        }

        // ------------------------------------------------------------------ Sigilo

        [Fact]
        public void ASigiloSeenBringsTheHorde()
        {
            var (_, s) = LaunchAt(NodeKinds.Stealth, 3);
            Assert.Equal((MissionKinds.Recovery, true), (s.Mission.Kind, s.Mission.Stealth!.Value));
            Assert.NotNull(s.Mission.Item);
            Assert.True(s.Concealed);
            var before = s.Units.Count((u) => u.Team == Teams.Alien && u.Alive);
            var sim = NewSim(s);
            sim.BreakConcealment(ConcealReasons.Spotted, Squaddie(s).Id);
            Assert.False(s.Concealed);
            Assert.NotEmpty(Of<SpawnedEvent>(sim.Events));
            Assert.True(s.Units.Count((u) => u.Team == Teams.Alien && u.Alive) > before);
            // Twice a horde's wave.
            Assert.Equal(2 * Setup.HordeWave(s.Mission.Difficulty ?? 2).Count, Of<ReinforcementsIncomingEvent>(sim.Events).Sum((e) => e.R.Units.Count));
            // Seen again: nothing more.
            var again = NewSim(s);
            again.BreakConcealment(ConcealReasons.Spotted, Squaddie(s).Id);
            Assert.Empty(again.Events);
            // A Recuperación of before, seen: no wave.
            var (_, plain) = LaunchAt(NodeKinds.Recovery, 3);
            var other = NewSim(plain);
            other.BreakConcealment(ConcealReasons.Spotted, Squaddie(plain).Id);
            Assert.Empty(Of<SpawnedEvent>(other.Events));
        }

        [Fact]
        public void ASigiloUnseenPaysDoubleSeenHalf()
        {
            var (c, s) = LaunchAt(NodeKinds.Stealth, 3);
            var unseen = EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            Assert.True(unseen.Run!.Unseen);
            Assert.InRange(unseen.Run.Reward!.Value, 2 * Runs.RewardMin, 2 * Runs.RewardMax);
            var (d, t) = LaunchAt(NodeKinds.Stealth, 3);
            t.Concealed = false;
            var seen = EndFight(d, t, Outcomes.Victory, EndReasons.Objective);
            Assert.False(seen.Run!.Unseen);
            Assert.InRange(seen.Run.Reward!.Value, Runs.RewardMin / 2, (Runs.RewardMax + 1) / 2);
        }

        // ---------------------------------------------------------------- Guarida

        [Fact]
        public void AGuaridaIsWonWithItsBoxOpened()
        {
            var (c, s) = LaunchAt(NodeKinds.Elite, 3);
            var node = RunMap.Node(c.Run!, c.Run!.At)!;
            var lair = s.Mission.Lair!;
            Assert.Equal(MissionKinds.Scavenge, s.Mission.Kind);
            Assert.Equal(TileKinds.Low, Grid.KindAt(s, lair));
            Assert.Equal(DecorKinds.LootWeapons, Decor.DecorOf(s.Decor![Grid.TileIndex(s, lair.X, lair.Y)])!.Kind);
            // Its guard and group round it.
            var pod = s.Pods.Single((p) => p.Id == "pg");
            var guard = Setup.GuardOf(node.Seed!.Value);
            Assert.Equal(new[] { guard }.Concat(Setup.GuardPod), pod.UnitIds.Select((id) => Grid.GetUnit(s, id)!.Template));
            Assert.All(pod.UnitIds, (id) => Assert.True(Grid.Distance(Grid.GetUnit(s, id)!.Pos, lair) <= 4));
            Assert.Equal(Lore.StageLine(NodeKinds.Elite, guard: guard), Runs.StageLine(node));
            // Out without opening it: lost.
            Assert.True(Leave(s).Ok);
            Assert.Equal((Outcomes.Defeat, EndReasons.Abandoned), (s.Outcome, s.OutcomeReason));

            // Opened, then out: won, the reward in the pack.
            var (d, t) = LaunchAt(NodeKinds.Elite, 3);
            var box = t.Mission.Lair!;
            var u = Squaddie(t);
            var side = Grid.Dirs4.Select((dd) => new Pos { X = box.X + dd.X, Y = box.Y + dd.Y, Z = 0 }).First((p) => Flights.CanStopAt(t, p));
            var there = Grid.UnitAt(t, side);
            if (there != null) there.Pos = new Pos { X = 1, Y = 1, Z = 0 };
            u.Pos = side;
            u.Ap = 2;
            t.Command = 0;
            var r = Exec(t, Ability(u.Id, AbilityIds.Search, tile: box));
            Assert.True(r.Ok, r.Error);
            var ev = Of<SearchedEvent>(r.Events!).Single();
            var found = (ev.Packed?.Select((p) => p.Gear) ?? new List<Gear>()).Concat(ev.Floor ?? new List<Gear>()).ToList();
            Assert.InRange(found.Where((g) => g.Kind == GearKinds.Supplies).Sum((g) => g.Count), RunLoot.LairMin, RunLoot.LairMax);
            Assert.Contains(found, (g) => g.Kind == GearKinds.Weapon && RunLoot.GoodWeapons.Contains(g.Type!));
            Assert.True(t.Mission.ObjectiveDone);
            Assert.True(Leave(t).Ok);
            Assert.Equal(Outcomes.Victory, t.Outcome);
            // The box was its reward: nothing on top.
            var report = EndFight(d, t, t.Outcome!, t.OutcomeReason!);
            Assert.Null(report.Run!.Reward);
        }

        [Fact]
        public void TheGuardIsAnAcorazadoOrAChillon()
        {
            Assert.Equal(TemplateIds.Mec, Setup.GuardOf(4));
            Assert.Equal(TemplateIds.Sectoid, Setup.GuardOf(7));
            Assert.Equal(TemplateIds.Sectoid, Setup.GuardOf(-7));
        }

        // ------------------------------------------------------------------- maps

        [Fact]
        public void TheFourAreOnTheMaps()
        {
            Assert.All(RunMap.NewSpecials, (k) => Assert.Contains(k, RunMap.Specials));
            var kinds = Enumerable.Range(0, 200).SelectMany((seed) => RunMap.Generate(1, seed)).Select((n) => n.Kind).Distinct().ToList();
            Assert.All(RunMap.NewSpecials, (k) => Assert.Contains(k, kinds));
        }
    }
}
