using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/terrain.test.ts, the tests that use only step 1's modules (grid, los, path, cover): the rest
    /// need aim.ts or rules.ts.
    /// </summary>
    public class TerrainStep1Tests
    {
        // A one-storey block in the middle with a ladder on its west side.
        private static readonly string[] Rows = new[]
        {
            "..........",
            "..........",
            "...L111...",
            "....111...",
            "....111...",
            "..........",
        }.Select((r) => r.Replace('1', '.')).ToArray();

        private static readonly string[] Heights =
        {
            "0000000000",
            "0000000000",
            "0000111000",
            "0000111000",
            "0000111000",
            "0000000000",
        };

        [Fact(DisplayName = "heights › a raised block hides what is behind it from the ground but not from its roof")]
        public void ARaisedBlockHidesWhatIsBehindIt()
        {
            var s = StateFrom(Rows, Heights);
            Assert.False(Los.LineClear(s, P(2, 3), P(8, 3)));
            Assert.True(Los.LineClear(s, P(5, 3), P(9, 3)));
        }

        [Fact(DisplayName = "heights › only ladders lead up; anyone can drop down")]
        public void OnlyLaddersLeadUpAnyoneCanDropDown()
        {
            var s = StateFrom(Rows, Heights);
            var u = AddUnit(s, "s1", TemplateIds.Assault, 1, 3);
            var reach = Paths.ComputeReach(s, u, 40);
            // Climbing straight up the wall is impossible, the ladder at (3,2) leads to the roof.
            var path = Paths.BuildPath(s, u, reach, P(5, 3));
            Assert.NotNull(path);
            Assert.Contains(path!, (p) => p.X == 3 && p.Y == 2);
            var s2 = StateFrom(Rows, Heights);
            var top = AddUnit(s2, "s2", TemplateIds.Assault, 6, 3);
            var down = Paths.ComputeReach(s2, top, 6);
            Assert.True(down.Cost.Has(3 * 10 + 7));
        }

        [Fact(DisplayName = "heights › the foot of a raised block is full cover; its edge gives nothing to whoever stands on top")]
        public void TheFootOfARaisedBlockIsFullCover()
        {
            var s = StateFrom(Rows, Heights);
            Assert.Equal(2, Cover.CoverSides(s, P(7, 3))[3]);
            Assert.Equal(0, Cover.CoverSides(s, P(6, 3))[1]);
        }
    }
}
