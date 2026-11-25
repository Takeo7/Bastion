using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// O3 (Arturo, 25/11: «no deberías ser capaz de ver a través de puertas cerradas… desde dentro vi la calle»): one inside
    /// by a closed door, beside full cover, stepped out to look from the doorway (Los.PeekOrigins). Never from a closed door.
    /// </summary>
    public class ClosedDoorSightTests
    {
        // A house with its door on the west side; inside, by the door, a cupboard (H) north of whoever stands at 3,3.
        private static readonly string[] House =
        {
            "..........",
            "..#####...",
            "..#H..#...",
            "..+...#...",
            "..#...#...",
            "..#####...",
            "..........",
        };

        [Fact(DisplayName = "puerta cerrada › inside by it, beside a cupboard: no peeking from the doorway, the street not seen")]
        public void NoPeekingFromAClosedDoor()
        {
            var s = StateFrom(House);
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 3, 3);
            var street = P(0, 3);
            Assert.DoesNotContain(Los.PeekOrigins(s, u.Pos), (o) => o.X == 2 && o.Y == 3);
            Assert.Contains(Los.PeekOrigins(s, u.Pos), (o) => o.X == 4 && o.Y == 3);
            Assert.Null(Los.SightOrigin(s, u.Pos, street, 20));
            Assert.False(Los.TeamVisibility(s, Teams.Xcom)[Grid.TileIndex(s, 0, 3)]);
            // One outside by the door sees nobody inside either.
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 1, 3);
            Assert.Null(Los.SightOrigin(s, u.Pos, z.Pos, 20));
        }

        [Fact(DisplayName = "puerta cerrada › open, the doorway is looked through as ever")]
        public void OpenItIsSeenThrough()
        {
            var s = StateFrom(House);
            s.Tiles[Grid.TileIndex(s, 2, 3)] = TileKinds.DoorOpen;
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 3, 3);
            Assert.NotNull(Los.SightOrigin(s, u.Pos, P(0, 3), 20));
            Assert.True(Los.TeamVisibility(s, Teams.Xcom)[Grid.TileIndex(s, 0, 3)]);
        }
    }
}
