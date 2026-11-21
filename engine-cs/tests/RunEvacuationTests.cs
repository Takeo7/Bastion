using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G23.6 Evacuación (Arturo, cuadros 71 and 100; .dev-snapshots/gameplay/g23/EVACUACION.md): a Saqueo with four civilians
    /// hidden in buildings of their own, each in its circle; found, they walk to La Lata by themselves as the squad's turn
    /// ends, five tiles a step, panicking at the dead they see (one of three at random, by their seed), and get in without a
    /// seat. Won with two aboard, 20 § more for each one past two. Out of the maps till Red's bot, UI and Lore are there.
    /// </summary>
    public class RunEvacuationTests
    {
        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8 };

        private static (CampaignState C, GameState S) Evac(int seed = 3, string site = Places.Town, string kind = NodeKinds.Evacuation)
        {
            return LaunchAt(kind, seed, node: (n) =>
            {
                n.Site = site;
                n.Seed = seed;
                n.Modifier = null;
            });
        }

        private static List<Unit> Civilians(GameState s) => s.Mission.Civilians!.Select((c) => Grid.GetUnit(s, c.Unit)!).ToList();

        private static CivilianSpot SpotOf(GameState s, Unit u) => s.Mission.Civilians!.Single((c) => c.Unit == u.Id);

        private static List<Unit> Squad(GameState s) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive).ToList();

        private static void Found(GameState s, Unit u) => Events.ApplyEvent(s, new CivilianFoundEvent { Unit = u.Id, By = Squad(s)[0].Id });

        /// <summary>Every one of the dead off the map, so no panic.</summary>
        private static void NoDead(GameState s)
        {
            foreach (var d in s.Units.Where((u) => u.Alive && u.Team == Teams.Alien).ToList()) d.Alive = false;
        }

        private static int ToBus(GameState s, Unit u)
        {
            var reach = Paths.ComputeReach(s, u, 1_000_000, ignore: (_) => true).Cost;
            return s.Mission.EvacZone.Select((z) => Grid.TileIndex(s, (int)z.X, (int)z.Y)).Where(reach.ContainsKey).Select((i) => reach[i]).DefaultIfEmpty(int.MaxValue).Min();
        }

        /// <summary>A free outdoor floor tile, the first from the squad's start outwards at `from` tiles or more.</summary>
        private static Pos OpenTile(GameState s, Pos near, int from = 0)
        {
            for (var r = from; r < 30; r++)
                for (var dy = -r; dy <= r; dy++)
                    for (var dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        var p = new Pos { X = near.X + dx, Y = near.Y + dy, Z = 0 };
                        if (!Grid.InBounds(s, p.X, p.Y) || Grid.KindAt(s, p) != TileKinds.Floor || Grid.UnitAt(s, p) != null || Abilities.InEvacZone(s, p)) continue;
                        if (s.Ground != null && s.Ground[Grid.TileIndex(s, p.X, p.Y)] == 2) continue;
                        return p;
                    }
            throw new InvalidOperationException("no open tile");
        }

        // ------------------------------------------------------------------ the stage

        [Fact]
        public void ASaqueoWithCiviliansOnlyWhereThereAreBuildings()
        {
            Assert.Equal(MissionKinds.Scavenge, Runs.MissionKindOf(NodeKinds.Evacuation));
            Assert.Contains(NodeKinds.Evacuation, NodeKinds.All);
            Assert.DoesNotContain(NodeKinds.Evacuation, RunMap.Specials);
            Assert.Equal(new[] { Places.City, Places.Town, Places.Roadside, Places.Plaza }, RunMap.EvacuationSites);
            Assert.Equal("Evacuación", Lore.StageName(NodeKinds.Evacuation));
            Assert.False(RunMap.LooseSurvivor(new RunNode { Kind = NodeKinds.Evacuation, Row = 2, Seed = Enumerable.Range(1, 400).First((x) => RunMap.LooseSurvivor(new RunNode { Kind = NodeKinds.Normal, Row = 2, Seed = x })) }));
        }

        [Fact]
        public void FourCiviliansHiddenInBuildingsOfTheirOwnEachInItsCircle()
        {
            var fewer = 0;
            foreach (var site in new[] { Places.Town, Places.Roadside, Places.Plaza })
            {
                foreach (var seed in Seeds)
                {
                    var (_, s) = Evac(seed, site);
                    Assert.True(s.Mission.Civilians != null, $"{site} {seed}");
                    // Four, or three where the map has no more buildings in reach (roadside 5).
                    Assert.InRange(s.Mission.Civilians!.Count, Setup.CivilianFewest, Setup.Civilians);
                    if (s.Mission.Civilians.Count < Setup.Civilians) fewer++;
                    var plan = Buildings.GroundTiles(s);
                    var houses = new HashSet<int>();
                    foreach (var u in Civilians(s))
                    {
                        var spot = SpotOf(s, u);
                        // Hidden, captive, nobody's, going by themselves, not shown; in its circle.
                        Assert.Equal(TemplateIds.Civilian, u.Template);
                        Assert.True(u.Captive);
                        Assert.Null(u.Owner);
                        Assert.True(Rules.IsCivilian(u));
                        Assert.False(Rules.SurvivorShown(s, u));
                        Assert.False(spot.Found);
                        Assert.True(Js.Hypot(spot.Area!.Center.X - u.Pos.X, spot.Area.Center.Y - u.Pos.Y) <= spot.Area.Radius);
                        // Indoors, each in a building of its own, its walk to La Lata in its band.
                        var house = plan.FindIndex((t) => t.Contains(Grid.TileIndex(s, u.Pos.X, u.Pos.Y)));
                        Assert.True(house >= 0, $"{site} {seed}: {u.Id} outdoors");
                        Assert.True(houses.Add(house), $"{site} {seed}: two in one building");
                        var walk = ToBus(s, u) / Paths.CostOrtho;
                        Assert.InRange(walk, Setup.CivilianNear, Setup.CivilianFar);
                    }
                }
            }
            Assert.True(fewer <= 2, $"{fewer} of 24 with three");
        }

        [Fact]
        public void NoCiviliansOnAnotherStage()
        {
            var (_, s) = Evac(3, Places.Town, NodeKinds.Normal);
            Assert.Null(s.Mission.Civilians);
            Assert.DoesNotContain(s.Units, (u) => u.Template == TemplateIds.Civilian);
        }

        // ------------------------------------------------------------------ found

        [Fact]
        public void SeenByTheSquadACivilianIsFoundShownAndNoCaptive()
        {
            var (_, s) = Evac(3);
            var c = Civilians(s)[0];
            var u = Squad(s)[0];
            var beside = Grid.Dirs8.Select((d) => new Pos { X = c.Pos.X + d.X, Y = c.Pos.Y + d.Y, Z = 0 }).First((q) => Grid.InBounds(s, q.X, q.Y) && Grid.KindAt(s, q) == TileKinds.Floor && Grid.UnitAt(s, q) == null);
            u.Pos = beside;
            Assert.Contains(c, Rules.CiviliansSeen(s, u, u.Pos));
            // A civilian sees nobody for the squad.
            Assert.Empty(Rules.CiviliansSeen(s, Civilians(s)[1], c.Pos));
            Found(s, c);
            Assert.True(SpotOf(s, c).Found);
            Assert.Null(SpotOf(s, c).Area);
            Assert.False(c.Captive);
            Assert.True(Rules.SurvivorShown(s, c));
            Assert.DoesNotContain(c, Rules.CiviliansSeen(s, u, u.Pos));
        }

        [Fact]
        public void NobodyOrdersACivilian()
        {
            var (_, s) = Evac(3);
            var c = Civilians(s)[0];
            Assert.Equal((false, "No puedes dar órdenes a esa unidad."), (Exec(s, Move(c.Id, c.Pos.X, c.Pos.Y + 1)).Ok, Exec(s, Move(c.Id, c.Pos.X, c.Pos.Y + 1)).Error));
            Found(s, c);
            var r = Exec(s, Move(c.Id, c.Pos.X, c.Pos.Y + 1));
            Assert.Equal((false, "Va sola: nadie le da órdenes."), (r.Ok, r.Error));
        }

        // ------------------------------------------------------------------ their step

        [Fact]
        public void FoundACivilianWalksItsStepToLaLataAsTheSquadsTurnEndsAndGetsInWithoutASeat()
        {
            var (_, s) = Evac(3);
            NoDead(s);
            var c = Civilians(s)[0];
            Found(s, c);
            var before = ToBus(s, c);
            var from = c.Pos;
            var sim = NewSim(s);
            sim.CivilianStep();
            Assert.True(ToBus(s, c) < before);
            Assert.True(before - ToBus(s, c) <= Paths.FullMoves(c));
            Assert.InRange(Paths.FullMoves(c), 0, (int)(Content.CivilianMobility * Paths.CostOrtho));
            Assert.False(Grid.SamePos(from, c.Pos));
            // The others, not found, stay.
            Assert.All(Civilians(s).Skip(1), (o) => Assert.True(o.Captive));
            // In La Lata's zone, it gets in, and takes no seat.
            var seats = Rules.SeatsFree(s);
            var zone = s.Mission.EvacZone.Select((z) => new Pos { X = z.X, Y = z.Y, Z = 0 }).First((p) => Grid.UnitAt(s, p) == null);
            c.Pos = zone;
            sim.CivilianStep();
            Assert.True(c.Aboard);
            Assert.Equal(seats, Rules.SeatsFree(s));
            Assert.Equal(1, Rules.CiviliansAboard(s));
        }

        [Fact]
        public void TheirStepComesBeforeTheDeadsTurn()
        {
            var (_, s) = Evac(3);
            NoDead(s);
            var c = Civilians(s)[0];
            Found(s, c);
            var r = Exec(s, new EndTurnCommand { Ready = true });
            Assert.True(r.Ok, r.Error);
            var moved = r.Events!.FindIndex((e) => e is MovedEvent m && m.Unit == c.Id);
            var alien = r.Events!.FindIndex((e) => e is TurnBeganEvent t && t.Team == Teams.Alien);
            Assert.True(moved >= 0 && alien > moved);
        }

        [Fact]
        public void PanicOneOfTheThreeByItsSeedTheSameEveryTime()
        {
            var seen = new HashSet<string>();
            for (var seed = 1; seed <= 30; seed++)
            {
                var spot = new CivilianSpot { Seed = seed * 31 };
                for (var k = 0; k < 3; k++)
                {
                    spot.Panics = k;
                    var a = Rules.PanicOf(spot);
                    Assert.Equal(a, Rules.PanicOf(new CivilianSpot { Seed = spot.Seed, Panics = k }));
                    seen.Add(a);
                }
            }
            Assert.Equal(PanicKinds.All.OrderBy((x) => x), seen.OrderBy((x) => x));
        }

        /// <summary>Civilian c1 found, out of doors near the squad, one of the dead `gap` tiles off it in sight; its panics set so the next is `kind`.</summary>
        private static (GameState S, Unit C, Unit Dead) Panicking(string kind, int gap = 3)
        {
            var (_, s) = Evac(3);
            NoDead(s);
            var c = Civilians(s)[0];
            Found(s, c);
            c.Pos = OpenTile(s, Squad(s)[0].Pos, 4);
            var spot = SpotOf(s, c);
            while (Rules.PanicOf(spot) != kind) spot.Panics++;
            var z = s.Units.First((u) => u.Template == TemplateIds.Zombie);
            z.Alive = true;
            var at = OpenTile(s, c.Pos, gap);
            z.Pos = at;
            Assert.True(Los.CanSee(s, c, z));
            return (s, c, z);
        }

        [Fact]
        public void PanickedItCowersAndDoesntWalk()
        {
            var (s, c, z) = Panicking(PanicKinds.Cower);
            var from = c.Pos;
            var sim = NewSim(s);
            sim.CivilianStep();
            var e = Of<CivilianPanickedEvent>(sim.Events).Single();
            Assert.Equal((c.Id, PanicKinds.Cower, z.Id), (e.Unit, e.Kind, e.Dead));
            Assert.True(Grid.SamePos(from, c.Pos));
        }

        [Fact]
        public void PanickedItWalksAwayFromTheDead()
        {
            var (s, c, z) = Panicking(PanicKinds.Away);
            var before = Grid.Distance(c.Pos, z.Pos);
            var sim = NewSim(s);
            sim.CivilianStep();
            Assert.Equal(PanicKinds.Away, Of<CivilianPanickedEvent>(sim.Events).Single().Kind);
            Assert.True(Grid.Distance(c.Pos, z.Pos) > before);
        }

        [Fact]
        public void PanickedItHidesInABuildingAndIsFoundAgainInItsNewCircle()
        {
            var (s, c, z) = Panicking(PanicKinds.Hide);
            var sim = NewSim(s);
            var hide = Rules.HideTile(s, c, z);
            sim.CivilianStep();
            var e = Of<CivilianPanickedEvent>(sim.Events).Single();
            if (hide == null)
            {
                // Nowhere to hide in its step: away.
                Assert.Equal(PanicKinds.Away, e.Kind);
                return;
            }
            Assert.Equal(PanicKinds.Hide, e.Kind);
            var spot = SpotOf(s, c);
            Assert.False(spot.Found);
            Assert.True(c.Captive);
            Assert.False(Rules.SurvivorShown(s, c));
            Assert.True(Js.Hypot(spot.Area!.Center.X - c.Pos.X, spot.Area.Center.Y - c.Pos.Y) <= spot.Area.Radius);
            Assert.Equal(2, s.Ground![Grid.TileIndex(s, c.Pos.X, c.Pos.Y)]);
            // Hidden, it doesn't walk; found again, it does.
            var at = c.Pos;
            z.Alive = false;
            sim.CivilianStep();
            Assert.True(Grid.SamePos(at, c.Pos));
            Found(s, c);
            sim.CivilianStep();
            Assert.False(Grid.SamePos(at, c.Pos));
        }

        // ------------------------------------------------------------------ the squad

        [Fact]
        public void ACivilianSeenGivesNobodyAway()
        {
            var (s, c, z) = Panicking(PanicKinds.Cower);
            s.Concealed = true;
            var sim = NewSim(s);
            sim.BreakConcealment(ConcealReasons.Spotted, c.Id, z.Id);
            Assert.True(s.Concealed);
            sim.BreakConcealment(ConcealReasons.Spotted, Squad(s)[0].Id, z.Id);
            Assert.False(s.Concealed);
        }

        [Fact]
        public void WithTheSquadAllFallenTheCiviliansDontKeepItGoing()
        {
            var (_, s) = Evac(3);
            Found(s, Civilians(s)[0]);
            var sim = NewSim(s);
            foreach (var u in Squad(s)) sim.Damage(u, 999, 0);
            sim.CheckOutcome();
            Assert.True((Outcomes.Defeat, EndReasons.Wiped) == (s.Outcome, s.OutcomeReason), $"{s.Outcome} {s.OutcomeReason}; alive {string.Join(",", s.Units.Where((u) => u.Alive && u.Team == Teams.Xcom).Select((u) => u.Id + ":" + u.Template))}");
        }

        [Fact]
        public void LaLataWaitsForAFoundCivilianWalkingAndACallTakesIt()
        {
            var (_, s) = Evac(3);
            NoDead(s);
            var c = Civilians(s)[0];
            Found(s, c);
            foreach (var u in Squad(s)) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            Assert.False(Rules.AllAboard(s));
            // Hidden again, it doesn't hold La Lata.
            Events.ApplyEvent(s, new CivilianHidEvent { Unit = c.Id, Area = new SearchArea { Center = new Vec2 { X = c.Pos.X, Y = c.Pos.Y }, Radius = 5 } });
            Assert.True(Rules.AllAboard(s));
        }

        [Fact]
        public void ACivilianNeverShootsNotEvenOnWatch()
        {
            // Producto's review of 21/11, 1: a civilian goes by itself as the base's turrets, but never shoots (its pistol's
            // only there for the template): no watch armed at the enemy's turn, found or not.
            var (_, s) = Evac(3);
            NoDead(s);
            var c = Civilians(s)[0];
            Found(s, c);
            var r = Exec(s, new EndTurnCommand { Ready = true });
            Assert.True(r.Ok, r.Error);
            Assert.DoesNotContain(r.Events!, (e) => e is OverwatchEvent o && Civilians(s).Any((x) => x.Id == o.Unit));
            Assert.All(Civilians(s), (x) => Assert.False(x.Overwatch));
        }

        [Fact]
        public void AllTheSquadAboardAndACivilianStillWalkingTheStageGoesOn()
        {
            // Producto's review of 21/11, 2: not wiped; La Lata waits for them, and one aboard may call it to leave.
            var (_, s) = Evac(3);
            NoDead(s);
            var c = Civilians(s)[0];
            Found(s, c);
            foreach (var u in Squad(s)) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            NewSim(s).CheckOutcome();
            Assert.Null(s.Outcome);
            var aboard = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Aboard == true);
            var r = Exec(s, new EvacCommand { Unit = aboard.Id });
            Assert.True(r.Ok, r.Error);
            Assert.NotNull(s.Outcome);
        }

        // ------------------------------------------------------------------ the reward

        [Fact]
        public void WonWithTwoAboardAndTwentyMoreForEachPastTwo()
        {
            var (c, s) = Evac(3);
            NoDead(s);
            var civilians = Civilians(s);
            foreach (var x in civilians.Take(1)) { Found(s, x); Events.ApplyEvent(s, new BoardedEvent { Unit = x.Id }); }
            Events.ApplyEvent(s, new BoardedEvent { Unit = Squad(s)[0].Id });
            Assert.False(Rules.EvacWins(s));
            Assert.Equal(1, Rules.CiviliansShort(s));
            foreach (var x in civilians.Skip(1)) { Found(s, x); Events.ApplyEvent(s, new BoardedEvent { Unit = x.Id }); }
            Assert.True(Rules.EvacWins(s));
            Assert.Null(Rules.CiviliansShort(s));
            foreach (var u in Squad(s)) Events.ApplyEvent(s, new BoardedEvent { Unit = u.Id });
            NewSim(s).DepartEvac();
            Assert.Equal(Outcomes.Victory, s.Outcome);
            new EngineCampaignRules().ApplyMissionResult(c, s, new Rng(1));
            // Not on the maps yet (not Rewarded): the bonus is checked as RunFights adds it.
            Assert.Equal(4, Rules.CiviliansAboard(s));
            Assert.Equal(Rules.CivilianBonus * 2, Rules.CivilianBonus * Math.Max(0, Rules.CiviliansAboard(s) - Rules.CiviliansNeeded));
        }

        [Fact]
        public void TooFewLeftAliveNoRewardToLose()
        {
            var (_, s) = Evac(3);
            var sim = NewSim(s);
            foreach (var x in Civilians(s).Skip(1)) sim.Damage(x, 999, 0);
            Assert.Null(Rules.CiviliansShort(s));
            Assert.False(Rules.EvacWins(s));
        }

        // ------------------------------------------------------------------ saved

        [Fact]
        public void TheCiviliansKeptInTheState()
        {
            var (_, s) = Evac(3);
            SpotOf(s, Civilians(s)[0]).Panics = 2;
            var back = Json.Read<GameState>(Json.Write(s));
            Assert.Equal(s.Mission.Civilians!.Select((c) => (c.Unit, c.Found, c.Panics, c.Seed)), back.Mission.Civilians!.Select((c) => (c.Unit, c.Found, c.Panics, c.Seed)));
            Assert.All(back.Units.Where((u) => u.Template == TemplateIds.Civilian), (u) => Assert.True(Rules.IsCivilian(u)));
            var copy = Events.CloneState(s);
            Assert.Equal(s.Mission.Civilians.Select((c) => c.Seed), copy.Mission.Civilians!.Select((c) => c.Seed));
        }
    }
}
