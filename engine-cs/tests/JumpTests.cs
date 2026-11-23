using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// N2 (Arturo, 22/11, cuadro 119): jumping down off a storey hurts, a point a storey (Content.FallDamage), through armour, and
    /// may kill; no cap on the storeys; only movement, no noise. The dead jump and get hurt; the living never jump (they take the
    /// stairs). Off a hill (the ground's own heights) it doesn't hurt, and its cap stays.
    /// </summary>
    public class JumpTests
    {
        /// <summary>
        /// A field 12 × 5 with a block of three storeys over its left half (columns 0–5): its storeys' floors and its roof, open
        /// to the right, so that one on any of them can step off the edge onto the ground.
        /// </summary>
        private static GameState Block()
        {
            var s = StateFrom(Enumerable.Repeat(new string('.', 12), 5).ToArray());
            var floor = Enumerable.Repeat("......      ", 5).ToArray();
            WithLevels(s, new[] { floor, floor, floor });
            return s;
        }

        private static int Fallen(GameState s, Unit u, Pos to)
        {
            var reach = Paths.ComputeReach(s, u, Paths.FullMoves(u));
            var path = Paths.BuildPath(s, u, reach, to);
            Assert.NotNull(path);
            return Paths.FallAlong(s, u.Pos, path!);
        }

        [Fact]
        public void OffAStoreyAPointAStoreyThroughArmourNoCapNoNoise()
        {
            foreach (var z in new[] { 1, 2, 3 })
            {
                var s = Block();
                var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 2, z);
                u.Hp = 9;
                u.Armor = 1;
                AddUnit(s, "a1", TemplateIds.Zombie, 11, 0);
                var to = P(7, 2);
                Assert.Equal(z, Fallen(s, u, to));
                var r = Exec(s, new MoveCommand { Unit = "s1", To = to });
                Assert.True(r.Ok, r.Error);
                Assert.True(Grid.SamePos(to, u.Pos));
                Assert.Equal(9 - z * Content.FallDamage, u.Hp);
                // Only movement: no noise of its own.
                Assert.Empty(Of<NoiseEvent>(r.Events!));
            }
        }

        [Fact]
        public void ItMayKill()
        {
            var s = Block();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 2, 2);
            u.Hp = 2;
            AddUnit(s, "s2", TemplateIds.Rookie, 0, 4);
            AddUnit(s, "a1", TemplateIds.Zombie, 11, 0);
            Assert.True(Exec(s, new MoveCommand { Unit = "s1", To = P(7, 2) }).Ok);
            Assert.False(u.Alive);
        }

        [Fact]
        public void TheDeadJumpAndGetHurtTheLivingNever()
        {
            var s = Block();
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 5, 2, 2);
            var raider = AddUnit(s, "a2", TemplateIds.Trooper, 5, 0, 2);
            Assert.True(Paths.Leaps(zombie));
            Assert.False(Paths.Leaps(raider));
            var below = P(7, 2);
            Assert.True(Paths.ComputeReach(s, zombie, Paths.FullMoves(zombie)).Cost.ContainsKey(Grid.NodeOf(s, below)));
            Assert.False(Paths.ComputeReach(s, raider, Paths.FullMoves(raider)).Cost.ContainsKey(Grid.NodeOf(s, P(7, 0))));
            var reach = Paths.ComputeReach(s, zombie, Paths.FullMoves(zombie));
            NewSim(s).MoveAlong(zombie, Paths.BuildPath(s, zombie, reach, below)!, 1);
            Assert.Equal(Content.Templates[TemplateIds.Zombie].Hp - 2 * Content.FallDamage, zombie.Hp);
        }

        /// <summary>The block with a ladder down from its first floor's corner (5, 0) to the ground's (6, 0).</summary>
        private static GameState WithLadder()
        {
            var s = Block();
            s.Tiles[Grid.TileIndex(s, 6, 0)] = TileKinds.Ladder;
            return s;
        }

        [Fact]
        public void ASoldierGoesByTheStairsWhereTheyGetThere()
        {
            // N2, cuadro 122: by the stairs where they reach with the movement given, though jumping is shorter; jumping
            // only where nothing else gets there this turn.
            var s = WithLadder();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 2, 1);
            AddUnit(s, "a1", TemplateIds.Zombie, 11, 4);
            var to = P(7, 2);
            var far = Paths.ComputeReach(s, u, 24);
            var path = Paths.BuildPath(s, u, far, to)!;
            Assert.Equal(0, Paths.FallAlong(s, u.Pos, path));
            Assert.Contains(path, (p) => Grid.SamePos(p, P(6, 0)));
            Assert.Equal(Paths.CostAlong(s, u, u.Pos, path), far.Cost.Get(Grid.NodeOf(s, to), -1));
            Assert.False(far.Leapt?.Contains(Grid.NodeOf(s, to)) ?? false);
            // With the movement for the jump only: the jump.
            var near = Paths.ComputeReach(s, u, 8);
            var leap = Paths.BuildPath(s, u, near, to)!;
            Assert.Equal(1, Paths.FallAlong(s, u.Pos, leap));
            Assert.Contains(Grid.NodeOf(s, to), near.Leapt!);
            Assert.Equal(Paths.CostAlong(s, u, u.Pos, leap), near.Cost.Get(Grid.NodeOf(s, to), -1));
            // The order: by the stairs, unhurt.
            u.Hp = 3;
            Assert.True(Exec(s, new MoveCommand { Unit = "s1", To = to }).Ok);
            Assert.True(Grid.SamePos(to, u.Pos));
            Assert.Equal(3, u.Hp);
        }

        [Fact]
        public void TheDeadFallTheShortestWay()
        {
            var s = WithLadder();
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 5, 2, 1);
            var reach = Paths.ComputeReach(s, zombie, 24);
            Assert.Null(reach.Leapt);
            Assert.Equal(1, Paths.FallAlong(s, zombie.Pos, Paths.BuildPath(s, zombie, reach, P(7, 2))!));
        }

        [Fact]
        public void CarryingSomeoneTheyDontJump()
        {
            var s = Block();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 2, 1);
            AddUnit(s, "a1", TemplateIds.Zombie, 11, 4);
            Assert.True(Paths.Leaps(u));
            u.Carrying = "w1";
            Assert.False(Paths.Leaps(u));
            Assert.DoesNotContain(Paths.ComputeReach(s, u, 24).Cost.Keys, (n) => Grid.PosOf(s, n).Z == 0);
        }

        [Fact]
        public void DownTheStairsOrOffAHillItDoesntHurt()
        {
            var s = Block();
            Assert.Equal(0, Paths.FallOf(s, P(5, 2), P(6, 2)));
            // A ladder's step down: none.
            var i = Grid.TileIndex(s, 6, 2);
            s.Tiles[i] = TileKinds.Ladder;
            Assert.Equal(0, Paths.FallOf(s, P(5, 2, 1), P(6, 2)));
        }
    }
}
