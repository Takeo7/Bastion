using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Arturo, 16/11, chosen in the box: «Cuando estas oculto que tengas más movimiento y que cuando dejes de estarlo tengas
    /// lo normal», 3 tiles more (Content.ConcealedMobility, Paths.ConcealedBonus). A soldier walks 11 tiles while the squad
    /// is hidden, 8 once it is found; found mid-turn, each soldier loses the 3 from what it has left (Events, concealmentBroken).
    /// </summary>
    public class ConcealedMovesTests
    {
        /// <summary>An open field 30 × 7, a soldier at its west end (1, 2) and one of the dead far east (28, 2), unaware, facing away.</summary>
        private static GameState Field(out Unit soldier, bool concealed = true)
        {
            var s = StateFrom(Enumerable.Repeat(new string('.', 30), 7).ToArray());
            s.Concealed = concealed;
            soldier = AddUnit(s, "s1", TemplateIds.Rookie, 1, 2);
            // Somebody to fight, so the mission goes on; looking east, it never notices the squad.
            var far = AddUnit(s, "a9", TemplateIds.Zombie, 28, 2, extra: (u) => u.Facing = 0);
            Pod(s, "p9", new[] { far }, active: false);
            return s;
        }

        private static bool Reaches(GameState s, Unit u, int x, int y) => Paths.ComputeReach(s, u, Paths.MoveBudget(s, u)).Cost.Has(Grid.NodeOf(s, P(x, y)));

        [Fact(DisplayName = "concealed movement › hidden, a soldier reaches 11 tiles; found, 8")]
        public void HiddenElevenFoundEight()
        {
            var s = Field(out var u);
            Assert.Equal(3 * Paths.CostOrtho, Paths.ConcealedBonus(s, u));
            Assert.Equal((22, 22), (Paths.FullMoves(s, u), Paths.MoveBudget(s, u)));
            // Its own, without the state, stays 8: the squad's reach and moves are counted with the state.
            Assert.Equal(16, Paths.FullMoves(u));
            Assert.True(Reaches(s, u, 12, 2));
            Assert.False(Reaches(s, u, 13, 2));
            s.Concealed = false;
            Assert.Equal((0, 16, 16), (Paths.ConcealedBonus(s, u), Paths.FullMoves(s, u), Paths.MoveBudget(s, u)));
            Assert.True(Reaches(s, u, 9, 2));
            Assert.False(Reaches(s, u, 10, 2));
        }

        [Fact(DisplayName = "concealed movement › the order goes as far: 11 tiles hidden, and 12 is too far")]
        public void TheOrderGoesElevenTiles()
        {
            var s = Field(out var u);
            Assert.False(Exec(s, Move("s1", 13, 2)).Ok);
            var r = Exec(s, Move("s1", 12, 2));
            Assert.True(r.Ok, r.Error);
            SamePlace(P(12, 2), u.Pos);
            Assert.Equal(0, u.Moves);
            Assert.True(s.Concealed);
        }

        [Fact(DisplayName = "concealed movement › hidden, a run is all 11 tiles at once; 8 is a walk")]
        public void AHiddenRunIsElevenTiles()
        {
            var s = Field(out var u);
            Assert.True(Paths.IsRun(s, u, 22));
            Assert.False(Paths.IsRun(s, u, 16));
            var r = Exec(s, Move("s1", 9, 2));
            Assert.True(r.Ok, r.Error);
            Assert.Null(Of<MovedEvent>(r.Events)[0].Run);
            Assert.Equal(6, u.Moves);

            var t = Field(out var v);
            r = Exec(t, Move("s1", 12, 2));
            Assert.True(r.Ok, r.Error);
            Assert.True(Of<MovedEvent>(r.Events)[0].Run);
            // Heard, as any run.
            Assert.Contains(Of<NoiseEvent>(r.Events), (n) => n.By == v.Id);
        }

        [Fact(DisplayName = "concealed movement › found mid-turn, each soldier loses the 3 tiles from what it has left, never below nothing")]
        public void FoundMidTurnLosesThree()
        {
            var s = Field(out var a);
            var b = AddUnit(s, "s2", TemplateIds.Rookie, 1, 5);
            var c = AddUnit(s, "s3", TemplateIds.Rookie, 1, 6);
            // One of the dead looking west along the bottom row: it notices s2 six tiles off.
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 16, 5, extra: (u) => u.Facing = System.Math.PI);
            Pod(s, "p1", new[] { z }, active: false);
            // s1 walks 4 tiles of its 11: 7 left.
            Assert.True(Exec(s, Move("s1", 5, 2)).Ok);
            Assert.Equal(14, a.Moves);
            Assert.True(s.Concealed);
            // s2 walks 10 tiles and is seen on the way, at (10, 5): the squad is found.
            var before = Events.CloneState(s);
            var r = Exec(s, Move("s2", 11, 5));
            Assert.True(r.Ok, r.Error);
            Assert.Single(Of<ConcealmentBrokenEvent>(r.Events));
            Assert.False(s.Concealed);
            // s1: 7 tiles left, 4 now; s2: 1 left, none now; s3 hasn't moved, and has its 8.
            Assert.Equal((8, 0), (a.Moves!.Value, b.Moves!.Value));
            Assert.Null(c.Moves);
            Assert.Equal((16, 16), (Paths.FullMoves(s, c), Paths.MoveBudget(s, c)));
            // A copy that only replays the events (a client's) comes to the same.
            foreach (var e in r.Events) Events.ApplyEvent(before, e);
            Assert.Equal((a.Moves, b.Moves, c.Moves, false), (Grid.GetUnit(before, "s1")!.Moves, Grid.GetUnit(before, "s2")!.Moves, Grid.GetUnit(before, "s3")!.Moves, before.Concealed));
        }

        [Fact(DisplayName = "concealed movement › found with 2 tiles left, it has none; the next turn, its own 8")]
        public void NoneLeftThenTheNextTurnItsOwn()
        {
            var s = Field(out var u);
            Assert.True(Exec(s, Move("s1", 10, 2)).Ok);
            Assert.Equal(4, u.Moves);
            NewSim(s).BreakConcealment(ConcealReasons.Attack, u.Id);
            Assert.Equal((0, 0), (u.Moves!.Value, Paths.MoveBudget(s, u)));
            Events.ApplyEvent(s, new TurnBeganEvent { Team = Teams.Xcom, Turn = 2 });
            Assert.Null(u.Moves);
            Assert.Equal((16, 16), (Paths.FullMoves(s, u), Paths.MoveBudget(s, u)));
        }

        [Fact(DisplayName = "concealed movement › still hidden the next turn, the 11 tiles again")]
        public void StillHiddenElevenAgain()
        {
            var s = Field(out var u);
            Assert.True(Exec(s, Move("s1", 12, 2)).Ok);
            Events.ApplyEvent(s, new TurnBeganEvent { Team = Teams.Xcom, Turn = 2 });
            Assert.Equal(22, Paths.MoveBudget(s, u));
        }

        [Fact(DisplayName = "concealed movement › the dead, a captive and a turret walk as ever")]
        public void TheDeadWalkAsEver()
        {
            var s = Field(out _);
            var z = Grid.GetUnit(s, "a9")!;
            var vip = AddUnit(s, "v1", TemplateIds.Vip, 3, 3, extra: (u) => u.Captive = true);
            var turret = AddUnit(s, "t1", TemplateIds.Turret, 3, 4);
            foreach (var hidden in new[] { true, false })
            {
                s.Concealed = hidden;
                Assert.Equal((0, 10, 10), (Paths.ConcealedBonus(s, z), Paths.FullMoves(s, z), Paths.MoveBudget(s, z)));
                Assert.Equal(0, Paths.ConcealedBonus(s, vip));
                Assert.Equal((0, 0), (Paths.ConcealedBonus(s, turret), Paths.FullMoves(s, turret)));
            }
        }
    }
}
