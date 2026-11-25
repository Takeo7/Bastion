using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Producto's independent review of the 0.14.0's engine (23/11, .dev-snapshots/gameplay/MENSAJE-DE-PRODUCTO-revision-motor-0140.md):
    /// each point that was a fault, as it would fail before its fix. Only the rules there were before (they run on a tree of
    /// before too); those that need what is new are in Review0140NewTests.
    /// </summary>
    public class Review0140Tests
    {
        // ------------------------------------------------------------------ 1. a way out of a fight of before

        [Fact]
        public void AFightWithNoLaLataMayBeAbandoned()
        {
            // A Limpieza saved half played before the 0.14.0 had no La Lata: it may be abandoned, or it ends only killing them all.
            var (_, s) = LaunchAt(NodeKinds.Elimination);
            s.Mission.EvacZone.Clear();
            s.Mission.EvacBus = null;
            Assert.NotEmpty(Rules.CallAbandon(s, 0, Zero));
            Assert.Equal((Outcomes.Defeat, EndReasons.Abandoned), (s.Outcome, s.OutcomeReason));
        }

        [Fact]
        public void ASoldierWithNoWayToLaLataMayAbandonTheRestNot()
        {
            var (_, s) = LaunchAt(NodeKinds.Normal);
            // With a way out for everyone: no.
            Assert.Empty(Rules.CallAbandon(s, 0, Zero));
            Assert.Null(s.Outcome);
            // One walled in where they stand (a hollow of a map of before, say): yes.
            var u = s.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null);
            // Somewhere of its own, nobody beside it.
            u.Pos = Enumerable.Range(0, s.Width * s.Height).Select((i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 })
                .First((p) => p.X > 2 && p.Y > 2 && p.X < s.Width - 3 && p.Y < s.Height - 3 && Flights.CanStopAt(s, p) && Grid.UnitAt(s, p) == null
                    && Grid.Dirs8.All((d) => Grid.UnitAt(s, new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = 0 }) == null));
            foreach (var d in Grid.Dirs8)
            {
                var p = new Pos { X = u.Pos.X + d.X, Y = u.Pos.Y + d.Y, Z = 0 };
                if (Grid.InBounds(s, p.X, p.Y) && Grid.UnitAt(s, p) == null) s.Tiles[Grid.TileIndex(s, p.X, p.Y)] = TileKinds.Wall;
            }
            Assert.NotEmpty(Rules.CallAbandon(s, 0, Zero));
            Assert.Equal(Outcomes.Defeat, s.Outcome);
        }

        [Fact]
        public void AFightOfBeforeGetsItsPitsWhenLoaded()
        {
            // A country Saqueo with a hollow (WayOutTests' sweep): its pits, then as a save of before without them.
            var (_, s) = LaunchAt(NodeKinds.Normal, node: (n) => { n.Site = Places.Country; n.Seed = 845934591; n.Modifier = null; });
            var pits = s.Mission.Pits;
            Assert.NotNull(pits);
            s.Mission.Pits = null;
            Setup.UpToDate(s);
            Assert.Equal(pits, s.Mission.Pits);
        }

        // ---------------------------------------------------------- 4. La Lata out of reach, no map of pits

        [Fact]
        public void WithLaLataOutOfReachNothingIsAPit()
        {
            // La Lata's place walled in: before, every place the squad got to was a pit and it couldn't take a step.
            var s = StateFrom(new[] { "..........", "..........", "......#...", ".....#.#..", "......#..." });
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 1, extra: (u) => u.CampaignId = "c1");
            s.Mission.EvacZone = new List<Vec2> { new Vec2 { X = 6, Y = 3 } };
            Assert.Empty(Paths.NoWayOut(s));
            Setup.NoPits(s);
            Assert.Null(s.Mission.Pits);
        }

        // ------------------------------------------------------------------------- 5. a ladder's step

        [Fact]
        public void TwoStoreysOntoTheFootOfALadderIsAFall()
        {
            var s = StateFrom(Enumerable.Repeat(new string('.', 12), 5).ToArray());
            var floor = Enumerable.Repeat("......      ", 5).ToArray();
            WithLevels(s, new[] { floor, floor, floor });
            s.Tiles[Grid.TileIndex(s, 6, 0)] = TileKinds.Ladder;
            // Its own storey's step, none; from two storeys up onto it, two.
            Assert.Equal(0, Paths.FallOf(s, P(5, 0, 1), P(6, 0)));
            Assert.Equal(2, Paths.FallOf(s, P(5, 0, 2), P(6, 0)));
            Assert.Equal(3, Paths.FallOf(s, P(5, 0, 3), P(6, 0)));
        }

        // ------------------------------------------------------------------ 6. round a fire, never a jump

        [Fact]
        public void RoundAFireTheWayDoesntJumpWhereTheWayShownDidnt()
        {
            // By the stairs from the first floor to the street, the ladder's foot on fire: the way round it is only jumping.
            var s = StateFrom(Enumerable.Repeat(new string('.', 12), 5).ToArray());
            var floor = Enumerable.Repeat("......      ", 5).ToArray();
            WithLevels(s, new[] { floor, floor, floor });
            s.Tiles[Grid.TileIndex(s, 6, 0)] = TileKinds.Ladder;
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 2, 1);
            AddUnit(s, "a1", TemplateIds.Zombie, 11, 4);
            var foot = Grid.NodeOf(s, P(6, 0));
            s.Fires = new List<Fire> { new Fire { Id = "f1", Source = foot, Tiles = new List<int> { foot }, Turns = 2 } };
            var reach = Paths.ComputeReach(s, u, Paths.MoveBudget(s, u));
            var path = Paths.BuildPath(s, u, reach, P(7, 0))!;
            Assert.Equal(0, Paths.FallAlong(s, u.Pos, path));
        }

        // ------------------------------------------------------------- 8. the living awake, the squad hidden

        [Fact]
        public void AwakeFromTheStartTheLivingGoForAHiddenSquad()
        {
            // A Rescate with Cuervos «Despiertos»: the group awake, the squad hidden and out of its sight. Since cuadro 157 (P4,
            // «solo lo que ve cada uno») it knows of the squad only where it comes in, as a noise heard there (Setup, Despiertos;
            // Producto's review of 0.14.7): it goes there.
            var s = StateFrom(Enumerable.Repeat(new string('.', 40), 5).ToArray());
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 2);
            var raider = AddUnit(s, "a1", TemplateIds.Trooper, 38, 2, extra: (u) => { u.PodId = "p1"; u.Heard = P(1, 2); });
            s.Pods = new List<Pod> { new Pod { Id = "p1", Active = true, UnitIds = new List<string> { "a1" } } };
            s.Concealed = true;
            Assert.False(Los.CanSee(s, raider, s.Units[0]));
            Assert.True(Exec(s, new EndTurnCommand { Ready = true }).Ok);
            Assert.True(raider.Pos.X < 38, $"{raider.Pos.X}");
        }

        [Fact]
        public void AwakeKnowingNothingTheLivingKeepWatch()
        {
            // Awake with nothing heard and the squad out of its sight (cuadro 157, P4): it stays. The squad hidden, not on watch
            // either: it knows of no one to watch for (Producto's review of 0.14.7).
            var s = StateFrom(Enumerable.Repeat(new string('.', 40), 5).ToArray());
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 2);
            var raider = AddUnit(s, "a1", TemplateIds.Trooper, 38, 2, extra: (u) => u.PodId = "p1");
            s.Pods = new List<Pod> { new Pod { Id = "p1", Active = true, UnitIds = new List<string> { "a1" } } };
            s.Concealed = true;
            Assert.True(Exec(s, new EndTurnCommand { Ready = true }).Ok);
            Assert.Equal(38, raider.Pos.X);
            Assert.False(raider.Overwatch);
        }

        // ---------------------------------------------------------------------- 7. the trickle by level

        [Fact]
        public void TheTrickleScalesWithTheLevel()
        {
            // A trickle of 1 a turn (difficulty 1): a half of it, one turn in two; before, Round(0.5) gave it all.
            var factor = 1.0;
            int Came(double? scale)
            {
                var (_, s) = LaunchAt(NodeKinds.Normal);
                factor = System.Math.Max(1, System.Math.Max(s.Width, s.Height) / 48.0);
                s.Mission.Difficulty = 1;
                s.Mission.HordeScale = scale;
                var sim = NewSim(s);
                var count = 0;
                for (var turn = 1; turn <= 8; turn++)
                {
                    var before = sim.Events.Count;
                    sim.CallHorde(turn);
                    count += Of<ReinforcementsIncomingEvent>(sim.Events.Skip(before)).Where((e) => e.R.Trickle == true).Sum((e) => e.R.Units.Count);
                }
                return count;
            }
            var hard = Came(null);
            Assert.Equal(8 * (int)Js.Round(factor), hard);
            // A share of it for real: 8 turns of `rate` each, the part a turn leaves over carried to the next.
            Assert.Equal((int)System.Math.Floor(8 * factor * 0.5 + 1e-9), Came(0.5));
            Assert.Equal((int)System.Math.Floor(8 * factor * 0.75 + 1e-9), Came(0.75));
        }

        // ------------------------------------------------------------------- 10. the groups in their order

        [Fact]
        public void ALevelKeepsTheGroupsInTheirOrder()
        {
            var living = new List<string> { TemplateIds.Trooper };
            var dead = new List<string> { TemplateIds.Zombie };
            var pods = new List<List<string>> { living, dead, new List<string>(dead), new List<string>(living), new List<string>(dead) };
            var fewer = Levels.Pods(pods, 0.6);
            Assert.Equal(new[] { living, pods[1], pods[2], pods[3] }, fewer);
            var more = Levels.Pods(pods, 1.25);
            Assert.Equal(pods.Concat(new[] { pods[1] }).Select((g) => string.Join(",", g)), more.Select((g) => string.Join(",", g)));
        }
    }
}
