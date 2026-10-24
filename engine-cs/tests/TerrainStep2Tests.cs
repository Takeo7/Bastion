using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/terrain.test.ts, the tests TerrainStep1Tests leaves out: they need aim.ts, abilities.ts or
    /// rules.ts. The TypeScript file runs every command with one `ctx`, a single Rng(1) shared by all its tests in turn;
    /// here each test has its own Rng(1), shared by its commands (nothing in them rolls a die).
    /// </summary>
    public class TerrainStep2Tests
    {
        // A one-storey block in the middle with a ladder on its west side.
        private static readonly string[] HeightRows = new[]
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

        [Fact(DisplayName = "heights › shooting down from a roof adds the height advantage")]
        public void ShootingDownFromARoofAddsTheHeightAdvantage()
        {
            var s = StateFrom(HeightRows, Heights);
            var sniper = AddUnit(s, "s1", TemplateIds.Specialist, 6, 3);
            var alien = AddUnit(s, "a1", TemplateIds.Trooper, 9, 3);
            var shot = Aim.PreviewShot(s, sniper, alien)!;
            Assert.Equal(20, shot.Mods.Find((m) => m.Label == "Ventaja de altura")?.Value);
            var back = Aim.PreviewShot(s, alien, sniper)!;
            Assert.DoesNotContain(back.Mods, (m) => m.Label == "Ventaja de altura");
        }

        // ------------------------------------------------------------ doors and windows

        private static readonly string[] Rows =
        {
            ".......",
            ".##+##.",
            ".#...#.",
            ".=...#.",
            ".#####.",
            ".......",
        };

        private const string NoClosedDoor = "No hay ninguna puerta cerrada a tu lado.";
        private const string NoOpenDoor = "No hay ninguna puerta abierta a tu lado.";

        /// <summary>toEqual({ ok: false, error }).</summary>
        private static void Refused(string error, CommandResult r)
        {
            Assert.False(r.Ok);
            Assert.Equal(error, r.Error);
            Assert.Null(r.Events);
        }

        [Fact(DisplayName = "doors and windows › a closed door blocks sight until someone walks through it")]
        public void AClosedDoorBlocksSightUntilSomeoneWalksThroughIt()
        {
            var rng = new Rng(1);
            var s = StateFrom(Rows);
            AddUnit(s, "s1", TemplateIds.Assault, 3, 0);
            AddUnit(s, "a1", TemplateIds.Trooper, 3, 3);
            Assert.Null(Los.SightOrigin(s, P(3, 0), P(3, 3), 18));
            var r = Exec(s, Move("s1", 3, 2), rng);
            Assert.True(r.Ok);
            Assert.Equal(TileKinds.DoorOpen, s.Tiles[1 * 7 + 3]);
        }

        [Fact(DisplayName = "doors and windows › windows are transparent and give full cover; vaulting costs extra and breaks the glass")]
        public void WindowsAreTransparentAndGiveFullCover()
        {
            var rng = new Rng(1);
            var s = StateFrom(Rows);
            Assert.True(Los.LineClear(s, P(0, 3), P(3, 3)));
            Assert.Equal(2, Cover.CoverAgainst(s, P(2, 3), new Vec2 { X = 0, Y = 3 }));
            var u = AddUnit(s, "s1", TemplateIds.Assault, 0, 3);
            AddUnit(s, "a1", TemplateIds.Trooper, 6, 5);
            var reach = Paths.ComputeReach(s, u, 40);
            Assert.DoesNotContain(Paths.Destinations(s, u, reach), (d) => d.Pos.X == 1 && d.Pos.Y == 3);
            Assert.Equal(2 + 2 + 2, reach.Cost.Get(3 * 7 + 2, -1));
            Exec(s, Move("s1", 2, 3), rng);
            Assert.Equal(TileKinds.WindowBroken, s.Tiles[3 * 7 + 1]);
        }

        // Playtest 2026-10-13: opening a door beside you costs nothing.
        [Fact(DisplayName = "doors and windows › a soldier beside a door opens and closes it without moving, for free, quietly")]
        public void ASoldierBesideADoorOpensAndClosesIt()
        {
            var rng = new Rng(1);
            var s = StateFrom(Rows);
            var u = AddUnit(s, "s1", TemplateIds.Assault, 3, 0);
            AddUnit(s, "a1", TemplateIds.Trooper, 6, 5);
            var door = P(3, 1);
            Assert.Contains(AbilityIds.OpenDoor, Abilities.AbilitiesOf(u));
            Assert.Contains(AbilityIds.CloseDoor, Abilities.AbilitiesOf(u));
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.OpenDoor));
            Assert.Equal(NoOpenDoor, Abilities.AbilityBlocker(s, u, AbilityIds.CloseDoor));
            Assert.Null(Los.SightOrigin(s, u.Pos, P(3, 3), 18));
            var host = Events.CloneState(s);
            var r = Exec(s, Ability("s1", AbilityIds.OpenDoor, tile: door), rng);
            Assert.True(r.Ok);
            Assert.Equal(TileKinds.DoorOpen, s.Tiles[1 * 7 + 3]);
            Assert.Equal(2, u.Ap);
            SamePlace(P(3, 0), u.Pos);
            Assert.NotNull(Los.SightOrigin(s, u.Pos, P(3, 3), 18));
            // As quiet as walking through it, heard from the door.
            var events = r.Ok ? r.Events! : new System.Collections.Generic.List<GameEvent>();
            var noise = Assert.Single(Of<NoiseEvent>(events));
            SamePlace(door, noise.Tile);
            Assert.Equal(Js.Round(Content.Noise.Door * Rules.SquadLouder * 10) / 10, noise.Radius);
            Assert.Equal("s1", noise.By);
            Assert.Empty(noise.Heard);
            // Clients get there by the events alone.
            foreach (var e in events) Events.ApplyEvent(host, e);
            Assert.Equal(s.Tiles, host.Tiles);

            Assert.Equal(NoClosedDoor, Abilities.AbilityBlocker(s, u, AbilityIds.OpenDoor));
            var shut = Exec(s, Ability("s1", AbilityIds.CloseDoor, tile: door), rng);
            Assert.True(shut.Ok);
            Assert.Equal(TileKinds.Door, s.Tiles[1 * 7 + 3]);
            Assert.Equal(2, u.Ap);
            Assert.Null(Los.SightOrigin(s, u.Pos, P(3, 3), 18));
            // Even disoriented: it's done with the hands.
            u.Disoriented = 1;
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.OpenDoor));
        }

        [Fact(DisplayName = "doors and windows › only a door beside the soldier, on its level and not round a corner; nobody can be in the doorway to close it")]
        public void OnlyADoorBesideTheSoldier()
        {
            var rng = new Rng(1);
            var s = StateFrom(Rows);
            AddUnit(s, "a1", TemplateIds.Trooper, 6, 5);
            var far = AddUnit(s, "s1", TemplateIds.Assault, 6, 0);
            var door = P(3, 1);
            Assert.Equal(NoClosedDoor, Abilities.AbilityBlocker(s, far, AbilityIds.OpenDoor));
            Refused(NoClosedDoor, Exec(s, Ability("s1", AbilityIds.OpenDoor, tile: door), rng));
            // Diagonally beside it, from outside or from inside, is fine; a wall or a window is no door.
            var near = AddUnit(s, "s2", TemplateIds.Assault, 2, 0);
            Refused("Esa casilla no es una puerta cerrada contigua.", Exec(s, Ability("s2", AbilityIds.OpenDoor, tile: P(2, 1)), rng));
            var inside = AddUnit(s, "s3", TemplateIds.Assault, 4, 2);
            Assert.True(Abilities.CanTargetTile(s, near, AbilityIds.OpenDoor, door));
            Assert.True(Abilities.CanTargetTile(s, inside, AbilityIds.OpenDoor, door));
            Assert.False(Abilities.CanTargetTile(s, near, AbilityIds.OpenDoor, P(2, 1)));
            Assert.False(Abilities.CanTargetTile(s, AddUnit(s, "s4", TemplateIds.Assault, 0, 3), AbilityIds.OpenDoor, P(1, 3)));
            // Somebody in the doorway: it can't be shut on them.
            Exec(s, Ability("s2", AbilityIds.OpenDoor, tile: door), rng);
            var blocking = AddUnit(s, "s5", TemplateIds.Assault, 3, 1);
            Assert.Equal(NoOpenDoor, Abilities.AbilityBlocker(s, near, AbilityIds.CloseDoor));
            Refused(NoOpenDoor, Exec(s, Ability("s2", AbilityIds.CloseDoor, tile: door), rng));
            blocking.Alive = false;
            s.Units = s.Units.Where((x) => x != blocking).ToList();
            Assert.True(Abilities.CanTargetTile(s, near, AbilityIds.CloseDoor, door));

            // Round a wall's corner (both sides of the diagonal are wall): out of reach.
            var corner = StateFrom(new[] { "..#..", ".#+..", "....." });
            var stuck = AddUnit(corner, "s1", TemplateIds.Assault, 1, 0);
            Assert.Equal(NoClosedDoor, Abilities.AbilityBlocker(corner, stuck, AbilityIds.OpenDoor));
            // On a roof above the doorway, neither.
            var roof = StateFrom(new[] { ".....", "..+..", "....." }, new[] { ".1..." });
            Assert.Equal(NoClosedDoor, Abilities.AbilityBlocker(roof, AddUnit(roof, "s1", TemplateIds.Assault, 1, 0), AbilityIds.OpenDoor));
        }
    }
}
