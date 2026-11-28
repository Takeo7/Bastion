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
    /// The rule faults of the notes of 28/11 (0.14.15, .dev-snapshots/gameplay/MENSAJE-DE-PRODUCTO-0.14.15.md): the
    /// soldiers left at the base all of one player (note 9), a walk stopped for nothing (note 12), the window at the
    /// hospital (note 6) and «Terminar turno» only with the command in hand (note 10).
    /// </summary>
    public class NotesOf28NovTests
    {
        private static readonly Bastion.Net.EngineCampaignRules Rules_ = new Bastion.Net.EngineCampaignRules();

        /// <summary>The run's fight at the first node of row 1, chosen by both and launched as the room does.</summary>
        private static GameState FirstFight(CampaignState c)
        {
            var n = RunMap.Choices(c.Run!)[0];
            n.Kind = NodeKinds.Normal;
            n.Modifier = null;
            n.Site ??= Places.Country;
            n.Seed ??= 3;
            Assert.True(Agree(c, new ChooseNodeCommand { Node = n.Id }).Launch);
            return Rules_.LaunchMission(c, new Rng(55))!.Value.state;
        }

        /// <summary>Every soldier of the squad on the map dead, and the fight over as the room ends it.</summary>
        private static void Wiped(CampaignState c, GameState s)
        {
            foreach (var u in s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null))
            {
                u.Alive = false;
                u.Hp = 0;
            }
            EndFight(c, s, Outcomes.Defeat, EndReasons.Wiped);
        }

        private static (int, int) Owners(CampaignState c) => (c.Soldiers.Count((s) => s.Alive && s.Owner == 0), c.Soldiers.Count((s) => s.Alive && s.Owner == 1));

        // ------------------------------------------------------------ note 12

        private static List<Unit> Soldiers(GameState s) => s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null && u.Alive).ToList();

        /// <summary>A soldier of a run's fight on a walk of 4 steps or more on the ground, no door nor window on the way: the walk planned.</summary>
        private static (GameState S, Unit A, Unit B, List<Pos> Path) Planned()
        {
            var (_, s) = LaunchAt(NodeKinds.Normal, 3);
            var a = Soldiers(s)[0];
            var b = Soldiers(s)[1];
            var reach = Paths.ComputeReach(s, a, Paths.MoveBudget(s, a));
            foreach (var d in Paths.Destinations(s, a, reach).OrderBy((x) => x.Cost))
            {
                var path = Paths.BuildPath(s, a, reach, d.Pos);
                if (path == null || path.Count < 4 || path.Any((q) => q.Z != 0 || Grid.KindAt(s, q) != TileKinds.Floor)) continue;
                return (s, a, b, path);
            }
            throw new InvalidOperationException("no walk of 4 steps");
        }

        [Fact(DisplayName = "notes 28/11 › 12: one in the way since the walk was planned: it stops short, says who and where, and the way not walked is given back")]
        public void CutShort()
        {
            var (s, a, b, path) = Planned();
            var left = Paths.MovesLeft(s, a);
            var origin = Events.CopyPos(a.Pos);
            var whole = Math.Min(Paths.CostAlong(s, a, origin, path), Paths.MoveBudget(s, a));
            var two = Paths.CostAlong(s, a, origin, path.Take(2).ToList());
            // B steps into the way, on the walk's third tile, after it was planned.
            b.Pos = Events.CopyPos(path[2]);
            var sim = NewSim(s);
            sim.MoveAlong(a, path, 1);
            Assert.True(Grid.SamePos(a.Pos, path[1]));
            // The first segment took the whole way, as before; the cut gives back what it didn't walk.
            Assert.Equal(whole, Of<MovedEvent>(sim.Events).Single().Moves);
            var cut = Of<WalkBlockedEvent>(sim.Events).Single();
            Assert.Equal((a.Id, b.Id), (cut.Unit, cut.By));
            Assert.True(Grid.SamePos(path[2], cut.At));
            Assert.Equal(whole - two, cut.Moves);
            Assert.Equal(left - two, Paths.MovesLeft(s, a));
        }

        [Fact(DisplayName = "notes 28/11 › 12: in the way at the first step: nothing walked, nothing taken, and it says why")]
        public void CutAtOnce()
        {
            var (s, a, b, path) = Planned();
            b.Pos = Events.CopyPos(path[0]);
            var start = Events.CopyPos(a.Pos);
            var sim = NewSim(s);
            sim.MoveAlong(a, path, 1);
            Assert.True(Grid.SamePos(start, a.Pos));
            Assert.Null(a.Moves);
            Assert.Empty(Of<MovedEvent>(sim.Events));
            var cut = Of<WalkBlockedEvent>(sim.Events).Single();
            Assert.Equal((b.Id, (int?)null), (cut.By, cut.Moves));
        }

        // ------------------------------------------------------------ note 10

        /// <summary>A run's fight of two, one soldier each (the motorbike), the squad's first turn: slot 0 holds the command.</summary>
        private static GameState OfTwo()
        {
            var c = Campaign.CreateRunsCampaign(Both, new Rng(1));
            Start(c);
            var s = FirstFight(c);
            Assert.Equal((Teams.Xcom, (int?)0), (s.ActiveTeam, s.Command));
            return s;
        }

        private static Unit Of(GameState s, int slot) => Soldiers(s).Single((u) => u.Owner == slot);

        [Fact(DisplayName = "notes 28/11 › 10: «Terminar turno» with the command only; ended, it passes, and the other ends the turn")]
        public void EndedItPasses()
        {
            var s = OfTwo();
            var turn = s.Turn;
            Assert.True(Rules.MayEndTurn(s, 0, Both));
            Assert.False(Rules.MayEndTurn(s, 1, Both));
            Assert.True(Exec(s, new EndTurnCommand { Ready = true }, 0, 1, Both).Ok);
            Assert.Equal((turn, (int?)1), (s.Turn, s.Command));
            Assert.False(Rules.MayEndTurn(s, 0, Both));
            Assert.True(Rules.MayEndTurn(s, 1, Both));
            Assert.True(Exec(s, new EndTurnCommand { Ready = true }, 1, 1, Both).Ok);
            Assert.True(s.Outcome != null || s.Turn == turn + 1);
        }

        [Fact(DisplayName = "notes 28/11 › 10: who has nothing left to do hands the command on by itself; nothing left to anyone, the turn ends by itself")]
        public void NothingLeft()
        {
            var s = OfTwo();
            var turn = s.Turn;
            // A's soldier done (its last order): the command to B, who has his turn whole.
            var r = Exec(s, new DoneCommand { Unit = Of(s, 0).Id }, 0, 1, Both);
            Assert.True(r.Ok, r.Error);
            Assert.Equal((turn, (int?)1), (s.Turn, s.Command));
            Assert.True(Rules.MayEndTurn(s, 1, Both));
            // B's last: nobody has anything left, and nobody pressed «Terminar turno».
            r = Exec(s, new DoneCommand { Unit = Of(s, 1).Id }, 1, 1, Both);
            Assert.True(r.Ok, r.Error);
            Assert.True(s.Outcome != null || s.Turn == turn + 1);
            Assert.Equal(new[] { false, false }, s.Ready);
        }

        [Fact(DisplayName = "notes 28/11 › 10: a player with nobody alive needn't end it; one gone, the one here holds the command and ends it")]
        public void NobodyOrGone()
        {
            // B's soldier dead: A ends it alone.
            var s = OfTwo();
            var turn = s.Turn;
            Of(s, 1).Alive = false;
            Assert.True(Exec(s, new EndTurnCommand { Ready = true }, 0, 1, Both).Ok);
            Assert.True(s.Outcome != null || s.Turn == turn + 1);
            // B gone with the command: A holds it, and ends it with B's soldier to move.
            s = OfTwo();
            turn = s.Turn;
            s.Command = 1;
            Assert.True(Rules.MayEndTurn(s, 0, Zero));
            Assert.False(Rules.MayEndTurn(s, 0, Both));
            Assert.True(Exec(s, new EndTurnCommand { Ready = true }, 0, 1, Zero).Ok);
            Assert.True(s.Outcome != null || s.Turn == turn + 1);
        }

        [Fact(DisplayName = "notes 28/11 › 10: the holder's clock run out, ready, and the command to the partner, who may end it; not in the enemy's turn")]
        public void TheClock()
        {
            var s = OfTwo();
            var turn = s.Turn;
            Rules.RunOutOfTime(s, 0, Both, new Rng(1));
            Assert.Equal((turn, true, (int?)1), (s.Turn, s.Ready[0], s.Command));
            Assert.True(Rules.MayEndTurn(s, 1, Both));
            s.ActiveTeam = Teams.Alien;
            Assert.False(Rules.MayEndTurn(s, 1, Both));
        }

        // ------------------------------------------------------------- note 9

        [Fact(DisplayName = "notes 28/11 › 9: a new game of two, one each out, both fall: one each at the base")]
        public void BothFallOfTwo()
        {
            var c = Campaign.CreateRunsCampaign(Both, new Rng(1));
            Start(c);
            var s = FirstFight(c);
            Assert.Equal(new int?[] { 0, 1 }, s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).Select((u) => u.Owner).OrderBy((o) => o));
            Wiped(c, s);
            Assert.Equal((1, 1), Owners(c));
        }

        [Fact(DisplayName = "notes 28/11 › 9: the partner comes with the run under way, one each out, both fall: one each at the base")]
        public void BothFallThePartnerLate()
        {
            var c = Campaign.CreateRunsCampaign(Zero, new Rng(1));
            Start(c);
            Campaign.AddPlayer(c, 1, new Rng(2));
            // The one of the squad he plays is his own, not lent: nothing to hand over (RunState.hands).
            Assert.Equal(0, Runs.HandOver(c, 1, Both));
            Assert.Null(c.Run!.Hands);
            Assert.Equal((2, 2), Owners(c));
            var s = FirstFight(c);
            Assert.Equal(new int?[] { 0, 1 }, s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null).Select((u) => u.Owner).OrderBy((o) => o));
            Wiped(c, s);
            Assert.Equal((1, 1), Owners(c));
        }
    }
}
