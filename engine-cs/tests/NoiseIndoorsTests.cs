using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Arturo's notes of 14/11 (.dev-snapshots/gameplay/notas-2026-11-14/partida, p1): a shot inside a house, and the dead
    /// outside that heard it never came in. Two causes: a pod whose first member was boxed in by the rest forgot the noise
    /// at once (only the leader tried the walk), and a pod on patrol keeps out of windows, also going to look at a noise
    /// from inside a house with no door its way.
    /// </summary>
    public class NoiseIndoorsTests
    {
        /// <summary>A house of x 5–11, y 2–6, with a window on its west wall (5, 4) and no door.</summary>
        private static GameState House()
        {
            var s = StateFrom(new[]
            {
                "................",
                "................",
                ".....#######....",
                ".....#.....#....",
                ".....=.....#....",
                ".....#.....#....",
                ".....#######....",
                "................",
                "................",
            });
            // The squad, far off and out of sight: somebody to fight, so the mission goes on.
            AddUnit(s, "s1", TemplateIds.Assault, 15, 8);
            s.ActiveTeam = Teams.Alien;
            return s;
        }

        private static bool Inside(Pos p) => p.X >= 6 && p.X <= 10 && p.Y >= 3 && p.Y <= 5;

        [Fact(DisplayName = "noise indoors › a pod that heard something goes to look though its first member is boxed in by the rest")]
        public void APodGoesToLookThoughItsLeaderIsBoxedIn()
        {
            var s = House();
            // The first one in the corner by the map's edge, the other three round it.
            var leader = AddUnit(s, "a1", TemplateIds.Zombie, 0, 4);
            var others = new[] { AddUnit(s, "a2", TemplateIds.Zombie, 1, 4), AddUnit(s, "a3", TemplateIds.Zombie, 0, 3), AddUnit(s, "a4", TemplateIds.Zombie, 0, 5) };
            Pod(s, "p1", new[] { leader }.Concat(others), active: false);
            foreach (var z in new[] { leader }.Concat(others)) z.Heard = P(9, 4);
            var sim = NewSim(s, 1);
            sim.RunAlienTurn();
            Assert.Empty(Of<ForgotEvent>(sim.Events));
            Assert.Contains(Of<MovedEvent>(sim.Events), (m) => m.Unit != leader.Id);
        }

        [Fact(DisplayName = "noise indoors › the dead that heard something inside a house with no door their way climb in through a window")]
        public void TheDeadClimbInThroughAWindow()
        {
            var s = House();
            var zs = new[] { AddUnit(s, "a1", TemplateIds.Zombie, 2, 4), AddUnit(s, "a2", TemplateIds.Zombie, 2, 5) };
            Pod(s, "p1", zs, active: false);
            foreach (var z in zs) z.Heard = P(9, 4);
            var walked = new List<Pos>();
            for (var turn = 0; turn < 3 && !zs.Any((z) => Inside(z.Pos)); turn++)
            {
                s.ActiveTeam = Teams.Alien;
                var sim = NewSim(s, 1 + turn);
                sim.RunAlienTurn();
                foreach (var m in Of<MovedEvent>(sim.Events)) walked.AddRange(m.Path);
            }
            Assert.Contains(zs, (z) => Inside(z.Pos));
            // In by the window, which breaks.
            Assert.Contains(walked, (p) => p.X == 5 && p.Y == 4);
            Assert.Equal(TileKinds.WindowBroken, Grid.KindAt(s, P(5, 4)));
        }
    }
}
