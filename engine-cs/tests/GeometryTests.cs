using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>packages/engine/test/geometry.test.ts, test by test.</summary>
    public class GeometryTests
    {
        [Fact(DisplayName = "line of sight › walls block, low cover does not")]
        public void WallsBlockLowCoverDoesNot()
        {
            var s = StateFrom(new[] { ".....", "..#..", ".....", "..h..", "....." });
            Assert.False(Los.LineClear(s, P(2, 0), P(2, 2)));
            Assert.True(Los.LineClear(s, P(2, 2), P(2, 4)));
        }

        [Fact(DisplayName = "line of sight › a diagonal through a corner is blocked only when both sides block")]
        public void ADiagonalThroughACornerIsBlockedOnlyWhenBothSidesBlock()
        {
            var one = StateFrom(new[] { ".#", ".." });
            Assert.True(Los.LineClear(one, P(0, 1), P(1, 0)));
            var both = StateFrom(new[] { ".#", "#." });
            Assert.False(Los.LineClear(both, P(0, 0), P(1, 1)));
        }

        [Fact(DisplayName = "line of sight › a soldier behind a wall can step out to see around it")]
        public void ASoldierBehindAWallCanStepOutToSeeAroundIt()
        {
            var s = StateFrom(new[]
            {
                ".....",
                ".###.",
                ".....",
            });
            // Standing under the wall end at (3,2), the step-out tile (4,2) sees (4,0).
            HasPlace(Los.PeekOrigins(s, P(3, 2)), P(4, 2));
            Assert.NotNull(Los.SightOrigin(s, P(3, 2), P(4, 0), 10));
            // The middle of the wall has no corner to peek around.
            Assert.Null(Los.SightOrigin(s, P(2, 2), P(2, 0), 10));
        }

        private static GameState CoverState()
        {
            return StateFrom(new[]
            {
                ".......",
                ".......",
                "...h...",
                "...u...",
                ".......",
            });
        }

        private static readonly Pos Target = P(3, 3);

        [Fact(DisplayName = "cover › low cover protects against attacks from in front")]
        public void LowCoverProtectsAgainstAttacksFromInFront()
        {
            var s = CoverState();
            Assert.Equal(1, Cover.CoverAgainst(s, Target, new Vec2 { X = 3, Y = 0 }));
            Assert.Equal(1, Cover.CoverAgainst(s, Target, new Vec2 { X = 4, Y = 0 }));
        }

        [Fact(DisplayName = "cover › attacks from the side or behind flank the target")]
        public void AttacksFromTheSideOrBehindFlankTheTarget()
        {
            var s = CoverState();
            Assert.Equal(0, Cover.CoverAgainst(s, Target, new Vec2 { X = 6, Y = 3 }));
            Assert.Equal(0, Cover.CoverAgainst(s, Target, new Vec2 { X = 3, Y = 4 }));
        }

        [Fact(DisplayName = "cover › full cover beats half cover")]
        public void FullCoverBeatsHalfCover()
        {
            var t = StateFrom(new[] { ".#.", ".u.", "..." });
            t.Tiles[3] = TileKinds.Low;
            Assert.Equal(2, Cover.CoverAgainst(t, P(1, 1), new Vec2 { X = 1, Y = -5 }));
        }

        [Fact(DisplayName = "pathfinding › uses 1.5-tile diagonals and never cuts wall corners")]
        public void UsesDiagonalsAndNeverCutsWallCorners()
        {
            var s = StateFrom(new[] { "...", ".#.", "..." });
            var u = AddUnit(s, "s1", TemplateIds.Specialist, 0, 0);
            var reach = Paths.ComputeReach(s, u, 100);
            Assert.Equal(Paths.CostOrtho * 4, reach.Cost.Get(2 * 3 + 2, -1)); // around the wall, no corner cutting
            var path = Paths.BuildPath(s, u, reach, P(2, 2))!;
            SamePlace(P(2, 2), path[path.Count - 1]);
        }

        [Fact(DisplayName = "pathfinding › allies block as enemies do (Arturo, 19/10: «los compañeros se bloquean»): nobody walks through anyone")]
        public void AlliesBlockAsEnemiesDo()
        {
            var s = StateFrom(new[] { "....." });
            var u = AddUnit(s, "s1", TemplateIds.Specialist, 0, 0);
            AddUnit(s, "s2", TemplateIds.Specialist, 1, 0);
            var reach = Paths.ComputeReach(s, u, 100);
            Assert.Null(Paths.BuildPath(s, u, reach, P(1, 0)));
            Assert.Null(Paths.BuildPath(s, u, reach, P(2, 0)));
            Assert.False(reach.Cost.Has(2));

            // In the open it goes round, and never cuts the corner of the one it passes.
            var open = StateFrom(new[] { ".....", ".....", "....." });
            var v = AddUnit(open, "s1", TemplateIds.Specialist, 0, 1);
            AddUnit(open, "s2", TemplateIds.Specialist, 1, 1);
            AddUnit(open, "a1", TemplateIds.Trooper, 3, 0);
            var round = Paths.ComputeReach(open, v, 100);
            Assert.Equal(4, Paths.BuildPath(open, v, round, P(2, 1))!.Count);
            Assert.Equal(Paths.CostOrtho * 4, round.Cost.Get(1 * 5 + 2, -1));
            Assert.False(round.Cost.Has(3));
        }
    }
}
