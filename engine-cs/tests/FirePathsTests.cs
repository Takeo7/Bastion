using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// A soldier's walk round a fire (Arturo, 1/11: «que lo rodee»; new in C#): the shortest way unless a way round takes
    /// no more actions; the reach as before, and the walk the order makes the one the preview draws (Paths.BuildPath).
    /// </summary>
    public class FirePathsTests
    {
        private static GameState Street(int fireX)
        {
            var row = new string('.', 22);
            var s = StateFrom(new[] { row, row, row, row, row });
            s.Fires = new List<Fire> { new Fire { Id = "f1", Source = Grid.TileIndex(s, fireX, 0), Tiles = new List<int> { Grid.TileIndex(s, fireX, 2) } } };
            // One of the dead far off, so the mission goes on.
            AddUnit(s, "a1", TemplateIds.Zombie, 21, 4);
            return s;
        }

        private static bool OnFire(List<Pos> path, int x) => path.Any((p) => p.X == x && p.Y == 2);

        [Fact(DisplayName = "fire paths › a soldier goes round a fire when its movement covers it, and the order walks the way the preview drew")]
        public void RoundWhenItCostsNoMoreActions()
        {
            var s = Street(3);
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 1, 2);
            var reach = Paths.ComputeReach(s, soldier, Paths.MaxMoveCost(soldier));
            var preview = Paths.BuildPath(s, soldier, reach, P(5, 2))!;
            Assert.False(OnFire(preview, 3));
            Assert.True(Grid.SamePos(P(5, 2), preview[preview.Count - 1]));
            var hp = soldier.Hp;
            var result = Exec(s, Move("s1", 5, 2));
            Assert.True(result.Ok, result.Error);
            var walked = result.Events!.OfType<MovedEvent>().Where((e) => e.Unit == "s1").SelectMany((e) => e.Path).ToList();
            Assert.Equal(preview.Select((p) => (p.X, p.Y, p.Z)), walked.Select((p) => (p.X, p.Y, p.Z)));
            Assert.Equal(hp, soldier.Hp);
            // Moving takes no action (Arturo, 5/11 #14).
            Assert.Equal(2, soldier.Ap);
        }

        [Fact(DisplayName = "fire paths › through it when going round would take more movement than it has")]
        public void ThroughWhenRoundTakesMoreThanItHas()
        {
            var s = Street(3);
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 1, 2);
            // As far as its movement goes, in a straight line: any way round costs more.
            var far = 1 + Paths.MoveBudget(soldier) / Paths.CostOrtho;
            var reach = Paths.ComputeReach(s, soldier, Paths.MaxMoveCost(soldier));
            var path = Paths.BuildPath(s, soldier, reach, P(far, 2))!;
            Assert.True(OnFire(path, 3));
            var hp = soldier.Hp;
            Assert.True(Exec(s, Move("s1", far, 2)).Ok);
            Assert.Equal(hp - 1, soldier.Hp);
            Assert.Equal((2, 0), (soldier.Ap, Paths.MoveBudget(soldier)));
        }

        [Fact(DisplayName = "fire paths › the soldier's reach is the same with and without fires; one asked onto a fire goes")]
        public void TheReachIsTheSame()
        {
            var s = Street(3);
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 1, 2);
            var row = new string('.', 22);
            var fresh = StateFrom(new[] { row, row, row, row, row });
            AddUnit(fresh, "a1", TemplateIds.Zombie, 21, 4);
            var other = AddUnit(fresh, "s1", TemplateIds.Rookie, 1, 2);
            var burning = Paths.ComputeReach(s, soldier, Paths.MaxMoveCost(soldier));
            var plain = Paths.ComputeReach(fresh, other, Paths.MaxMoveCost(other));
            Assert.Equal(plain.Cost.Select((e) => (e.Key, e.Value)).OrderBy((e) => e.Key), burning.Cost.Select((e) => (e.Key, e.Value)).OrderBy((e) => e.Key));
            // Onto the fire itself: the shortest way, ending on it.
            var onto = Paths.BuildPath(s, soldier, burning, P(3, 2))!;
            Assert.True(Grid.SamePos(P(3, 2), onto[onto.Count - 1]));
            Assert.Equal(Paths.BuildPath(fresh, other, plain, P(3, 2))!.Select((p) => (p.X, p.Y)), onto.Select((p) => (p.X, p.Y)));
        }
    }
}
