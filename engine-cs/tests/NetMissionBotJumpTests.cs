using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// N2 (Arturo, 22/11, cuadros 119 and 122), MissionBot: it never jumps off a storey to its death; it takes the jumps the way
    /// gives (by the stairs where they get it), and none carrying someone (Paths.Leaps).
    /// </summary>
    public class NetMissionBotJumpTests
    {
        /// <summary>
        /// JumpTests' block: a field 12 × 5 with three storeys over its left half (columns 0–5), open to the right, no stairs; a
        /// soldier on the first floor's edge with only the blade, and one of the dead awake in the street, in sight.
        /// </summary>
        private static (GameState s, Unit u) Ledge(int hp)
        {
            var s = StateFrom(Enumerable.Repeat(new string('.', 12), 5).ToArray());
            var floor = Enumerable.Repeat("......      ", 5).ToArray();
            WithLevels(s, new[] { floor, floor, floor });
            s.Mission.Kind = MissionKinds.Elimination;
            s.Concealed = false;
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 2, 1, (x) => x.Arms = new Arms { Pistol = false });
            u.Hp = hp;
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 8, 2);
            Pod(s, "p1", new[] { z });
            return (s, u);
        }

        /// <summary>The bot's orders for its turn, each carried out, till it ends it.</summary>
        private static List<Command> Turn(GameState s)
        {
            var connected = new List<int> { 0 };
            var rng = new Rng(5);
            var said = new List<Command>();
            for (var k = 0; k < 20; k++)
            {
                var cmd = MissionBot.Next(s, 0, connected);
                if (cmd == null || cmd is EndTurnCommand) break;
                said.Add(cmd);
                Assert.True(Rules.ExecuteCommand(s, cmd, 0, connected, rng).Ok, Json.Write(cmd));
            }
            return said;
        }

        [Fact]
        public void ItNeverJumpsToItsDeath()
        {
            var (s, u) = Ledge(1);
            // The only way down is the jump, a storey: all its health.
            var reach = Paths.ComputeReach(s, u, Paths.MoveBudget(s, u));
            Assert.Contains(Grid.NodeOf(s, P(7, 2)), reach.Leapt!);
            Assert.True(MissionBot.Deadly(s, u, reach, P(7, 2)));
            Turn(s);
            Assert.True(u.Alive);
            Assert.Equal(1, u.Pos.Z);
            Assert.Equal(1, u.Hp);
        }

        /// <summary>
        /// Producto's review of the 0.14.0 (point 6): a place the stairs get it to, the stairs on fire. The rules take the way
        /// round the fire (Paths.RoundFire), and round it there is only the jump: with 1 hp it doesn't go. Nor onto the
        /// burning ladder itself, where the rules let it end (the old bot did, and burned).
        /// </summary>
        [Fact]
        public void NorWhereTheWayRoundAFireJumps()
        {
            var (s, u) = Ledge(1);
            s.Tiles[Grid.TileIndex(s, 6, 0)] = TileKinds.Ladder;
            s.Fires = new List<Fire> { new Fire { Id = "f1", Source = Grid.TileIndex(s, 6, 0), Tiles = new List<int> { Grid.NodeOf(s, P(6, 0)) } } };
            var reach = Paths.ComputeReach(s, u, Paths.MoveBudget(s, u));
            // Beside the dead, by the stairs on the reach; as the order goes, round the fire, jumping.
            Assert.DoesNotContain(Grid.NodeOf(s, P(7, 2)), reach.Leapt ?? new HashSet<int>());
            Assert.True(MissionBot.Deadly(s, u, reach, P(7, 2)));
            Turn(s);
            Assert.True(u.Alive);
            Assert.Equal(1, u.Pos.Z);
            Assert.Equal(1, u.Hp);
        }

        [Fact]
        public void WithTheHealthForItItJumpsWhereTheWayDoes()
        {
            var (s, u) = Ledge(5);
            Assert.False(MissionBot.Deadly(s, u, Paths.ComputeReach(s, u, Paths.MoveBudget(s, u)), P(7, 2)));
            var said = Turn(s);
            Assert.Contains(said, (c) => c is MoveCommand);
            Assert.True(u.Alive);
            Assert.Equal(0, u.Pos.Z);
            Assert.True(u.Hp <= 5 - Content.FallDamage);
        }
    }
}
