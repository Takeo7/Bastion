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
    /// G23.4 Separados (Arturo, cuadros 71 and 94; .dev-snapshots/gameplay/g23/SEPARADOS.md): a Saqueo whose squad starts in
    /// two groups on either side of La Lata, by owner with two players, halved alone; one soldier, none. The horde and La
    /// Lata as ever; the reward with one of each group aboard as it goes, else «ETAPA SUPERADA, SIN LA RECOMPENSA» (as
    /// Mordido's: a defeat with somebody out, the run going on). Only on the open maps; never with the horde awake from the
    /// map, though an event's for the next fight it takes.
    /// </summary>
    public class RunSplitTests
    {
        /// <summary>The «squad» upgrade's level for 2, 4, 6 and 8 seats.</summary>
        private static readonly int[] Levels = { 0, 1, 2, 3 };

        /// <summary>
        /// A run's Separados on `site`'s map of `seed`, the squad upgrade at `level` and as many soldiers as its seats (in turn
        /// to each player), alone or `two`; `squad` picks who goes, if given; `kind`, the same node as another stage. The campaign
        /// and the mission.
        /// </summary>
        private static (CampaignState C, GameState S) Split(int seed = 7, string site = Places.Country, int level = 1, bool two = false, Func<CampaignState, List<string>>? squad = null, string? added = null, string kind = NodeKinds.Split)
        {
            var c = two ? Two(seed) : Solo(seed);
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = level;
            var rng = new Rng(seed);
            while (c.Soldiers.Count((x) => x.Alive) < Runs.SeatsOf(c)) Runs.NewSoldier(c, c.Players[c.Soldiers.Count % c.Players.Count], rng);
            c.Squad = squad != null ? squad(c) : Runs.DefaultSquad(c, c.Players);
            var run = Start(c);
            var n = RunMap.Choices(run)[0];
            n.Kind = kind;
            n.Modifier = null;
            n.Site = site;
            n.Seed = seed;
            run.Pending = added;
            var cmd = new ChooseNodeCommand { Node = n.Id };
            Assert.True((two ? Agree(c, cmd) : Ok(c, cmd)).Launch);
            var launched = new Bastion.Net.EngineCampaignRules().LaunchMission(c, new Rng(seed + 100));
            Assert.NotNull(launched);
            return (c, launched!.Value.state);
        }

        private static List<Unit> Group(GameState s, int g) => s.Mission.Split![g].Select((id) => Grid.GetUnit(s, id)!).ToList();

        private static List<Unit> Squad(GameState s) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive).ToList();

        private static Pos Bus(GameState s, int i = 0) => s.Mission.EvacZone.Select((v) => new Pos { X = (int)v.X, Y = (int)v.Y, Z = 0 }).Where((p) => Abilities.InEvacZone(s, p)).ElementAt(i);

        /// <summary>Steps on foot (Paths' cost) from `from` to every node, as one of the squad walks, nobody in the way.</summary>
        private static Dictionary<int, double> Walk(GameState s, Unit u, Pos from)
        {
            var was = u.Pos;
            u.Pos = from;
            var cost = Paths.ComputeReach(s, u, 1_000_000, ignore: (_) => true).Cost;
            u.Pos = was;
            var all = cost.ToDictionary((kv) => kv.Key, (kv) => (double)kv.Value);
            all[Grid.NodeOf(s, from)] = 0;
            return all;
        }

        private static void Board(GameState s, Unit u) => Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });

        private static void Kill(GameState s, Unit u) => new Sim(s, new Rng(1), new[] { 0, 1 }).Damage(u, 999, 0);

        // ------------------------------------------------------------------ the stage

        [Fact]
        public void OnTheMapsRowsTwoToFiveOnlyOutOfDoorsAndNeverAwakeFromTheMap()
        {
            Assert.Equal(MissionKinds.Scavenge, Runs.MissionKindOf(NodeKinds.Split));
            // On the maps since Red's bot, UI and Lore are there (Producto, 20/11), as the other specials: rows 2–5.
            Assert.Contains(NodeKinds.Split, RunMap.Specials);
            Assert.True(RunMap.Rewarded(NodeKinds.Split));
            var nodes = Enumerable.Range(1, 500).SelectMany((seed) => RunMap.Generate(1, seed)).ToList();
            var split = nodes.Where((n) => n.Kind == NodeKinds.Split).ToList();
            Assert.All(split, (n) => Assert.InRange(n.Row, 2, RunMap.NodeRows));
            double share = split.Count / (double)nodes.Count((n) => n.Row >= 2 && n.Row <= RunMap.NodeRows);
            Assert.InRange(share, 0.012, 0.035);
            // Out of doors only, all six (a town's or a city's by its seed), never a special map nor the map's «Despiertos».
            Assert.All(split, (n) => Assert.Contains(n.Site, RunMap.SplitSites));
            Assert.Equal(RunMap.SplitSites.OrderBy((x) => x), split.Select((n) => n.Site).Distinct().OrderBy((x) => x));
            Assert.All(split, (n) => Assert.NotEqual(Modifiers.Awake, n.Modifier));
            Assert.Contains(split, (n) => n.Modifier != null);
            // As often as each of the others drawn only as specials (500 maps: 114 against 99–118; the Rescate has its own too).
            var others = new[] { NodeKinds.Bitten, NodeKinds.Wounded, NodeKinds.Hold, NodeKinds.Crossing, NodeKinds.Stealth, NodeKinds.Elite };
            Assert.InRange(split.Count / others.Average((k) => nodes.Count((n) => n.Kind == k)), 0.75, 1.3);
            // Never a special map: as drawn, and every SiteMix draw is out of doors.
            foreach (var seed in Enumerable.Range(1, 200))
                foreach (var site in RunMap.SplitSites)
                    Assert.Null(RunMap.SpecialFor(new RunNode { Kind = NodeKinds.Split, Seed = seed * 5, Site = site }));
            Assert.DoesNotContain(Places.Camping, RunMap.SplitSites);
            Assert.DoesNotContain(Places.Plaza, RunMap.SplitSites);
            Assert.DoesNotContain(RunMap.SplitSites, (p) => Sites.IndoorsOnly(p) || p == Places.Hospital);
            // The map's modifier: never «Despiertos» (Producto, 20/11); the rest as any.
            Assert.DoesNotContain(Modifiers.Awake, RunMap.ModifiersFor(NodeKinds.Split));
            Assert.Equal(Modifiers.All.Where((m) => m != Modifiers.Awake), RunMap.ModifiersFor(NodeKinds.Split));
            Assert.Equal(Modifiers.All, RunMap.ModifiersFor(NodeKinds.Normal));
        }

        [Fact]
        public void AnEventsAwakeHordeItTakes()
        {
            // The gasolinera's «el siguiente combate» (Producto, 20/11: the player chose it).
            var (_, s) = Split(added: Modifiers.Awake);
            Assert.True(s.Mission.Awake);
            Assert.NotNull(s.Mission.Split);
        }

        [Fact]
        public void ARunStartedBeforeHasNone()
        {
            var (_, s) = LaunchAt(NodeKinds.Normal, node: (n) => n.Site = Places.Country);
            Assert.Null(s.Mission.Split);
            Assert.True(Rules.FreeExit(s.Mission));
            Assert.Null(Rules.SplitShort(s));
            Assert.False(Rules.SplitLost(s));
            // A fight saved before: no field, nothing of it.
            var back = Events.CloneState(s);
            Assert.Null(back.Mission.Split);
            Assert.DoesNotContain("\"split\"", Bastion.Net.Json.Write(s.Mission));
        }

        [Fact]
        public void TheGroupsGoWithTheStateCopiedAndSent()
        {
            var (_, s) = Split(two: true);
            var copy = Events.CloneState(s);
            Assert.Equal(s.Mission.Split, copy.Mission.Split);
            Assert.NotSame(s.Mission.Split![0], copy.Mission.Split![0]);
            var sent = Bastion.Net.Json.Read<GameState>(Bastion.Net.Json.Write(s));
            Assert.Equal(s.Mission.Split, sent.Mission.Split);
        }

        // ------------------------------------------------------------------ the groups

        [Fact]
        public void ByOwnerWithTwoHalvedAloneNoneWithOne()
        {
            Assert.Equal(new[] { 0, 1, 0, 1 }, Setup.SplitGroups(new[] { 0, 1, 0, 1 }));
            // Uneven, by owner as it comes: 3 and 1 (Producto, 20/11: the R1 shows them apart).
            Assert.Equal(new[] { 1, 0, 0, 0 }, Setup.SplitGroups(new[] { 1, 0, 0, 0 }));
            // One player, or one of the two without soldiers: halved in order, the odd one in the first.
            Assert.Equal(new[] { 0, 0, 1, 1 }, Setup.SplitGroups(new[] { 0, 0, 0, 0 }));
            Assert.Equal(new[] { 0, 0, 1 }, Setup.SplitGroups(new[] { 1, 1, 1 }));
            Assert.Equal(new[] { 0, 1 }, Setup.SplitGroups(new[] { 0, 0 }));
            Assert.Null(Setup.SplitGroups(new[] { 0 }));
            Assert.Null(Setup.SplitGroups(new int[0]));
        }

        [Fact]
        public void TwoPlayersEachTheirOwnAloneHalvedOneSoldierAPlainSaqueo()
        {
            var (_, s2) = Split(two: true);
            Assert.Equal(2, s2.Mission.Split!.Count);
            Assert.All(Group(s2, 0), (u) => Assert.Equal(0, u.Owner));
            Assert.All(Group(s2, 1), (u) => Assert.Equal(1, u.Owner));
            Assert.Equal(Squad(s2).Count, Group(s2, 0).Count + Group(s2, 1).Count);
            var (_, s1) = Split(level: 2);
            Assert.Equal(new[] { 3, 3 }, s1.Mission.Split!.Select((g) => g.Count));
            // One soldier: no groups, a Saqueo of the stage, its reward by getting out.
            var (_, one) = Split(squad: (c) => c.Soldiers.Take(1).Select((x) => x.Id).ToList());
            Assert.Single(Squad(one));
            Assert.Null(one.Mission.Split);
            Assert.True(Rules.FreeExit(one.Mission));
        }

        // ------------------------------------------------------------------ the starts

        [Fact]
        public void EachGroupOnItsSideOfLaLataClearOfTheDead()
        {
            // The six open sites, 12 maps each, 4 seats: two groups always; each together, out of doors, as clear of the dead
            // as the usual start (12 tiles; 5 at the least where there is no room), as far from La Lata as the usual start
            // (SEPARADOS.md: 43–54 steps), and La Lata between them.
            var between = 0;
            var maps = 0;
            var clear = 0;
            foreach (var site in RunMap.SplitSites)
                foreach (var seed in Enumerable.Range(1, 12))
                {
                    var (_, s) = Split(seed, site);
                    var at = $"{site} {seed}";
                    Assert.True(s.Mission.Split != null, $"{at}: no groups");
                    var dead = s.Units.Where((u) => u.Alive && u.Team == Teams.Alien).ToList();
                    foreach (var u in Squad(s))
                    {
                        Assert.All(dead, (d) => Assert.True(Math.Max(Math.Abs(d.Pos.X - u.Pos.X), Math.Abs(d.Pos.Y - u.Pos.Y)) >= 5, $"{at}: {u.Id} beside {d.Id}"));
                        if (dead.All((d) => Math.Max(Math.Abs(d.Pos.X - u.Pos.X), Math.Abs(d.Pos.Y - u.Pos.Y)) >= 12)) clear++;
                        Assert.Equal(0, s.Storeys?[Grid.TileIndex(s, u.Pos.X, u.Pos.Y)] ?? 0);
                        Assert.NotEqual(2, s.Ground?[Grid.TileIndex(s, u.Pos.X, u.Pos.Y)] ?? 0);
                        Assert.False(Abilities.InEvacZone(s, u.Pos));
                        Assert.False(Burning.At(s, u.Pos), $"{at}: {u.Id} in the fire");
                    }
                    var a = Group(s, 0);
                    var b = Group(s, 1);
                    // Together: each group's soldiers within a few tiles of its first.
                    foreach (var g in new[] { a, b })
                        Assert.All(g, (u) => Assert.True(Grid.Distance(u.Pos, g[0].Pos) <= 4, $"{at}: {u.Id} away from its group"));
                    var fromBus = Walk(s, a[0], Bus(s));
                    var fromA = Walk(s, a[0], a[0].Pos);
                    double steps(Dictionary<int, double> from, Unit u) => from[Grid.NodeOf(s, u.Pos)] / Paths.CostOrtho;
                    var la = steps(fromBus, a[0]);
                    var lb = steps(fromBus, b[0]);
                    var ab = steps(fromA, b[0]);
                    Assert.InRange(la, 15, 100);
                    Assert.InRange(lb, 15, 100);
                    Assert.True(ab > la && ab > lb, $"{at}: the groups {ab:0} apart, La Lata {la:0} and {lb:0}");
                    maps++;
                    if (ab >= 0.8 * (la + lb)) between++;
                }
            // La Lata in the middle (the probe: 90–100 %); the dead kept off both since cuadro 95 (before it, 186 of 288
            // soldiers 12 tiles clear: a town's and a city's dead stand all round La Lata).
            Assert.True(between >= 0.85 * maps, $"La Lata between the groups in {between} of {maps}");
            Assert.True(clear >= 0.9 * 4 * maps, $"12 tiles from the dead: {clear} of {4 * maps}");
        }

        [Fact]
        public void TheTwoWalksToLaLataAlike()
        {
            // Producto, 20/11 (cuadro 95, «Igualar los lados»): each group's walk to the nearest of La Lata's zone, from its
            // first, the longer 1.2 times the shorter at the most (Setup.SplitEven) where the map has room (the probe of
            // 20/11, 600 maps: 99 %; before it 47 %, and up to 4.3 times); La Lata still between them.
            var maps = 0;
            var alike = 0;
            foreach (var site in RunMap.SplitSites)
                foreach (var seed in Enumerable.Range(1, 8))
                {
                    var (_, s) = Split(seed, site, 1, true);
                    var at = $"{site} {seed}";
                    var zone = s.Mission.EvacZone.Select((v) => new Pos { X = (int)v.X, Y = (int)v.Y, Z = 0 }).Where((p) => Abilities.InEvacZone(s, p)).ToList();
                    var walks = new[] { Group(s, 0)[0], Group(s, 1)[0] }.Select((u) =>
                    {
                        var from = Walk(s, u, u.Pos);
                        return zone.Where((p) => from.ContainsKey(Grid.NodeOf(s, p))).Min((p) => from[Grid.NodeOf(s, p)]) / Paths.CostOrtho;
                    }).ToList();
                    Assert.True(walks.Min() >= 15, $"{at}: {walks[0]:0} and {walks[1]:0} steps to La Lata");
                    maps++;
                    if (walks.Max() <= 1.2 * walks.Min()) alike++;
                }
            Assert.True(alike >= 0.9 * maps, $"Alike in {alike} of {maps}");
        }

        [Fact]
        public void TheDeadKeptOffBothGroupsAsOffTheUsualStart()
        {
            // Cuadro 95 («Igualar los lados»): the map's rules for the usual start, for each group. A group of the dead (with a
            // round) has its first tile a third of the map's side from each group's middle (16 at the least), and no stop of
            // its round within 12; a lone one is 14 from every soldier. As many of the dead as the Saqueo of the same map; the
            // nearest of them to each group as the usual start's (the probe of 20/11: medians 16–20 against 17–23, none within
            // 10; before it 10–14, and 0.2 to 1.6 within 10).
            foreach (var site in RunMap.SplitSites)
                foreach (var seed in Enumerable.Range(1, 8))
                    foreach (var (level, two) in new[] { (1, true), (3, false), (0, true) })
                    {
                        var (_, s) = Split(seed, site, level, two);
                        var (_, plain) = Split(seed, site, level, two, kind: NodeKinds.Normal);
                        var at = $"{site} {seed} {level} {(two ? "two" : "alone")}";
                        var dead = s.Units.Where((u) => u.Alive && u.Team == Teams.Alien).ToList();
                        Assert.Equal(plain.Units.Count((u) => u.Alive && u.Team == Teams.Alien), dead.Count);
                        var gap = Math.Max(16, (int)Math.Round(Math.Max(s.Width, s.Height) / 3.0));
                        foreach (var g in new[] { Group(s, 0), Group(s, 1) })
                        {
                            double mx = g.Average((u) => u.Pos.X), my = g.Average((u) => u.Pos.Y);
                            foreach (var pod in s.Pods.Where((p) => p.UnitIds.Any((id) => Grid.GetUnit(s, id)?.Alive == true && Grid.GetUnit(s, id)!.Pos.Z == 0)))
                            {
                                if (pod.Patrol.Count > 0)
                                {
                                    Assert.True(Js.Hypot(pod.Patrol[0].X - mx, pod.Patrol[0].Y - my) >= gap, $"{at}: {pod.Id} by a group");
                                    Assert.All(pod.Patrol.Skip(1), (p) => Assert.True(Js.Hypot(p.X - mx, p.Y - my) >= 12, $"{at}: {pod.Id}'s round by a group"));
                                }
                                else
                                    foreach (var d in pod.UnitIds.Select((id) => Grid.GetUnit(s, id)!))
                                        Assert.All(g, (u) => Assert.True(Js.Hypot(d.Pos.X - u.Pos.X, d.Pos.Y - u.Pos.Y) >= 14, $"{at}: {d.Id} by {u.Id}"));
                            }
                            Assert.True(dead.Min((d) => g.Min((u) => Math.Max(Math.Abs(d.Pos.X - u.Pos.X), Math.Abs(d.Pos.Y - u.Pos.Y)))) >= 8, $"{at}: the dead by a group");
                        }
                        // Where one of them can stand: on the ground, nobody else there, off fire and La Lata's tiles.
                        Assert.Equal(dead.Count, dead.Select((d) => Grid.TileIndex(s, d.Pos.X, d.Pos.Y) + 100000 * d.Pos.Z).Distinct().Count());
                        Assert.All(dead.Where((d) => d.Pos.Z == 0), (d) =>
                        {
                            Assert.True(Grid.CanStandOn(Grid.KindAt(s, d.Pos)), $"{at}: {d.Id} on {Grid.KindAt(s, d.Pos)}");
                            Assert.False(Abilities.InEvacZone(s, d.Pos) && !Grid.SamePos(d.Pos, Grid.GetUnit(plain, d.Id)!.Pos), $"{at}: {d.Id} put in La Lata");
                            Assert.False(Burning.At(s, d.Pos) && !Grid.SamePos(d.Pos, Grid.GetUnit(plain, d.Id)!.Pos), $"{at}: {d.Id} put in the fire");
                        });
                    }
        }

        [Fact]
        public void WhichGroupTakesWhichSideIsTheMapsSeeds()
        {
            // One side is clearer of the dead (R1, 20/11: the first group fell before getting there 149 times in 400, the
            // second 59): not always the first player's. The same map, swapped: each group where the other was.
            var (_, s) = LaunchAt(NodeKinds.Normal, node: (n) =>
            {
                n.Site = Places.Country;
                n.Seed = 7;
            });
            var t = Events.CloneState(s);
            Setup.Split(s);
            Setup.Split(t, swap: true);
            Assert.True(Grid.SamePos(Group(s, 0)[0].Pos, Group(t, 1)[0].Pos));
            Assert.True(Grid.SamePos(Group(s, 1)[0].Pos, Group(t, 0)[0].Pos));
            // Both ways over the maps.
            var sides = Enumerable.Range(1, 40).Select((seed) => Split(seed).S).Select((x) => Group(x, 0)[0].Pos.X < Group(x, 1)[0].Pos.X).ToList();
            Assert.Contains(true, sides);
            Assert.Contains(false, sides);
        }

        // ------------------------------------------------------------------ the reward

        [Fact]
        public void OneOfEachAboardWonWithItsReward()
        {
            var (c, s) = Split();
            var a = Group(s, 0);
            var b = Group(s, 1);
            Board(s, a[0]);
            Assert.Equal(b.Select((u) => u.Id), Rules.SplitShort(s)!.Select((u) => u.Id));
            Assert.False(Rules.EvacWins(s));
            Board(s, b[1]);
            Assert.Null(Rules.SplitShort(s));
            Assert.True(Rules.EvacWins(s));
            // La Lata goes now (called alone): the rest stay, it is won.
            Assert.True(Exec(s, new EvacCommand { Unit = a[0].Id }).Ok);
            Assert.Equal(Outcomes.Victory, s.Outcome);
            Assert.True(Rules.SplitSafe(s));
            var report = EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.True(report.Victory);
            Assert.InRange(report.Run!.Reward ?? 0, Runs.RewardMin, Runs.RewardMax);
            Assert.Null(report.Run.Recruits);
            Assert.NotNull(c.Run);
        }

        [Fact]
        public void OneOnLaLatasTilesWithASeatCountsAsGoing()
        {
            var (_, s) = Split();
            Board(s, Group(s, 0)[0]);
            var b = Group(s, 1)[0];
            b.Pos = Bus(s, 1);
            Assert.True(Abilities.InEvacZone(s, b.Pos));
            Assert.Null(Rules.SplitShort(s));
            Assert.True(Rules.EvacWins(s));
        }

        [Fact]
        public void WithoutOneOfAGroupNoRewardTheRunGoesOn()
        {
            // «ETAPA SUPERADA, SIN LA RECOMPENSA», as Mordido's: a defeat with somebody out.
            var (c, s) = Split();
            var a = Group(s, 0);
            foreach (var u in a) Board(s, u);
            Assert.True(Exec(s, new EvacCommand { Unit = a[0].Id }).Ok);
            Assert.Equal(Outcomes.Defeat, s.Outcome);
            Assert.Equal(EndReasons.Abandoned, s.OutcomeReason);
            Assert.False(Rules.SplitSafe(s));
            var lost = Group(s, 1).Select((u) => u.CampaignId).ToList();
            var report = EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.False(report.Victory);
            Assert.Null(report.Run!.Reward);
            Assert.Null(report.Run.Ended);
            Assert.NotNull(c.Run);
            Assert.All(lost, (id) => Assert.False(c.Soldiers.Single((x) => x.Id == id).Alive));
            Assert.All(a, (u) => Assert.True(c.Soldiers.Single((x) => x.Id == u.CampaignId).Alive));
        }

        [Fact]
        public void AGroupAllFallenNoRewardNoWarningLaLataGoesByItself()
        {
            var (c, s) = Split();
            foreach (var u in Group(s, 1)) Kill(s, u);
            Assert.True(Rules.SplitLost(s));
            Assert.Null(Rules.SplitShort(s));
            Assert.Null(s.Outcome);
            // The rest in: La Lata goes by itself, as ever, not won.
            var a = Group(s, 0);
            foreach (var u in a.Take(a.Count - 1)) Board(s, u);
            Assert.False(Rules.AllAboard(s));
            Board(s, a.Last());
            Assert.True(Rules.AllAboard(s));
            Assert.False(Rules.EvacWins(s));
            new Sim(s, new Rng(1), new[] { 0 }).DepartEvac();
            Assert.Equal(Outcomes.Defeat, s.Outcome);
            Assert.Equal(EndReasons.Abandoned, s.OutcomeReason);
            var report = EndFight(c, s, s.Outcome!, s.OutcomeReason!);
            Assert.Null(report.Run!.Reward);
            Assert.NotNull(c.Run);
        }

        [Fact]
        public void LaLataGoesAsEverNothingRefused()
        {
            // Arturo, cuadro 94: no «no sale sin ellos». Asked with a group out, it is asked; the partner's yes, it goes.
            var (_, s) = Split(two: true);
            var a = Group(s, 0);
            Board(s, a[0]);
            Assert.NotEmpty(Rules.SplitShort(s)!);
            var ask = Exec(s, new EvacCommand { Unit = a[0].Id }, slot: 0, connected: new[] { 0, 1 });
            Assert.True(ask.Ok, ask.Error);
            Assert.NotNull(s.EvacCall);
            var yes = Exec(s, new EvacAnswerCommand { Accept = true }, slot: 1, connected: new[] { 0, 1 });
            Assert.True(yes.Ok, yes.Error);
            Assert.Equal(Outcomes.Defeat, s.Outcome);
        }

        [Fact]
        public void AJoinedSurvivorCountsForNoGroup()
        {
            var (_, s) = Split();
            Board(s, Group(s, 0)[0]);
            // A survivor loose on the map, talked to, in La Lata: nobody of the second group yet.
            var x = Group(s, 1)[0];
            var vip = new Unit { Id = "vip", Team = Teams.Xcom, Alive = true, Name = "Superviviente", Owner = 0, Template = TemplateIds.Rookie, Hp = 3, MaxHp = 3, Pos = Events.CopyPos(x.Pos) };
            s.Units.Add(vip);
            Board(s, vip);
            Assert.False(Rules.EvacWins(s));
            Assert.Equal(Group(s, 1).Select((u) => u.Id), Rules.SplitShort(s)!.Select((u) => u.Id));
        }

        [Fact]
        public void AloneWithBothGroupsOneOfEachHalf()
        {
            var (_, s) = Split(level: 2);
            Assert.All(Squad(s), (u) => Assert.Equal(0, u.Owner));
            var a = Group(s, 0);
            foreach (var u in a) Board(s, u);
            Assert.False(Rules.EvacWins(s));
            Board(s, Group(s, 1)[2]);
            Assert.True(Rules.EvacWins(s));
        }

        [Fact]
        public void TheOtherPlayerGoneTheirSoldiersAreTheOnesHere()
        {
            // As in any fight (Content.CommandsFor; Arturo, 7/11: «no esperar nunca»): the one here brings both groups, the
            // reward as ever; and calls La Lata alone, no answer to wait for.
            var (_, s) = Split(two: true);
            var b = Group(s, 1)[0];
            Assert.Equal(1, b.Owner);
            var to = Grid.Dirs8.Select((d) => new Pos { X = b.Pos.X + d.X, Y = b.Pos.Y + d.Y, Z = 0 }).First((p) => Grid.InBounds(s, p.X, p.Y) && Grid.IsWalkable(Grid.KindAt(s, p)) && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null);
            Assert.False(Exec(s, new MoveCommand { Unit = b.Id, To = to }, slot: 0, connected: new[] { 0, 1 }).Ok);
            var r = Exec(s, new MoveCommand { Unit = b.Id, To = to }, slot: 0, connected: new[] { 0 });
            Assert.True(r.Ok, r.Error);
            Board(s, Group(s, 0)[0]);
            Board(s, b);
            Assert.True(Exec(s, new EvacCommand { Unit = b.Id }, slot: 0, connected: new[] { 0 }).Ok);
            Assert.Equal(Outcomes.Victory, s.Outcome);
        }

        // ------------------------------------------------------------------ never stuck

        [Fact]
        public void BothGroupsAlwaysReachLaLata()
        {
            // As El herido's (Producto, 19/11: never a stage that can't end): the six sites by 12 maps, with 8 seats (the most
            // to place) alone and two, and 2 seats two (one each): every soldier of both groups walks to La Lata, on foot.
            foreach (var (level, two) in new[] { (Levels[3], false), (Levels[3], true), (Levels[0], true) })
                foreach (var site in RunMap.SplitSites)
                    foreach (var seed in Enumerable.Range(1, 12))
                    {
                        var (_, s) = Split(seed, site, level, two);
                        var at = $"{site} {seed}, nivel {level}{(two ? ", a dos" : "")}";
                        Assert.True(s.Mission.Split != null, $"{at}: no groups");
                        Assert.Equal(Runs.Seats[level], Squad(s).Count);
                        foreach (var u in Squad(s))
                        {
                            var home = Walk(s, u, u.Pos);
                            Assert.True(s.Mission.EvacZone.Any((v) => home.ContainsKey(Grid.NodeOf(s, new Pos { X = (int)v.X, Y = (int)v.Y, Z = 0 }))), $"{at}: La Lata out of {u.Id}'s reach");
                        }
                    }
        }
    }
}
