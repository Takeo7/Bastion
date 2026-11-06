using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Buildings (new in C#, not in los.ts): inside a building the squad has neither been into nor looked into from just
    /// before a window, nobody shows to it, though they see it (Ana, 31/10, docs/PRUEBA-2026-10-31.md #1 and #5).
    /// </summary>
    public class BuildingsTests
    {
        // A one-storey house (walls x 2–6, y 1–5), a window on its west wall at (2,3) and a door on its south one at (4,5).
        private static readonly string[] HouseRows = { "............", "..#####.....", "..#...#.....", "..=...#.....", "..#...#.....", "..##+##.....", "............" };

        private static GameState House()
        {
            var s = StateFrom(HouseRows);
            s.Storeys = Enumerable.Repeat(0, s.Width * s.Height).ToList();
            for (var y = 1; y <= 5; y++) for (var x = 2; x <= 6; x++) s.Storeys[y * s.Width + x] = 1;
            return s;
        }

        private static void Walk(GameState s, Unit u, params Pos[] path)
        {
            Events.ApplyEvent(s, new MovedEvent { Unit = u.Id, Path = path.ToList(), Ap = 0 });
        }

        private static List<string> Inside(bool[] seen)
        {
            var out_ = new List<string>();
            for (var y = 2; y <= 4; y++) for (var x = 3; x <= 5; x++) if (seen[y * 12 + x]) out_.Add($"{x},{y}");
            return out_;
        }

        [Fact(DisplayName = "buildings › a door opened from just before it looks in, as a window (Arturo, 5/11 #2); from further off, nothing more")]
        public void ADoorOpenedLooksIn()
        {
            var s = House();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 4, 6);
            AddUnit(s, "a1", TemplateIds.Zombie, 4, 3);
            Assert.True(Buildings.Hidden(s, P(4, 3), new[] { u.Pos }));
            Events.ApplyEvent(s, new TileChangedEvent { Pos = P(4, 5), Kind = TileKinds.DoorOpen });
            Assert.False(Buildings.Hidden(s, P(4, 3)));
            Assert.Contains(Grid.TileIndex(s, 4, 3), s.Discovered!);
            // Two tiles off the open door: still out of sight inside.
            var w = House();
            var far = AddUnit(w, "s1", TemplateIds.Rookie, 4, 7);
            Events.ApplyEvent(w, new TileChangedEvent { Pos = P(4, 5), Kind = TileKinds.DoorOpen });
            Assert.True(Buildings.Hidden(w, P(4, 3), new[] { far.Pos }));
        }

        [Fact(DisplayName = "buildings › from afar nobody inside shows, nor do they see the squad through the window; right before it, the room and whoever is in it")]
        public void RightBeforeTheWindow()
        {
            var s = House();
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 0, 3);
            // Right behind the glass: los.ts showed it from two steps back.
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 3, 3, extra: (u) => u.Facing = System.Math.PI);
            Assert.False(Los.CanSee(s, soldier, zombie));
            // Nor do they see out through the glass (Arturo, 31/10: the enemies see through no window).
            Assert.False(Los.CanSee(s, zombie, soldier));
            Assert.Empty(Inside(Los.TeamVisibility(s, Teams.Xcom)));
            Assert.Empty(Los.VisibleEnemies(s, Teams.Xcom));
            // Where it would stand, it would see in (the client's previews).
            Assert.True(Los.CanSee(s, soldier, zombie, P(1, 3)));
            Walk(s, soldier, P(1, 3));
            Assert.True(Los.CanSee(s, soldier, zombie));
            Assert.Equal(9, Inside(Los.TeamVisibility(s, Teams.Xcom)).Count);
            // Found for the mission: back off, and what is in line through the glass still shows.
            Walk(s, soldier, P(0, 3));
            Assert.True(Los.CanSee(s, soldier, zombie));
            Assert.NotNull(s.Discovered);
            Assert.Equal(s.Discovered, Events.CloneState(s).Discovered);
        }

        [Fact(DisplayName = "buildings › before the window only: not on its diagonal, not two steps back, not walking past it")]
        public void BeforeTheWindowOnly()
        {
            var s = House();
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 0, 3);
            AddUnit(s, "a1", TemplateIds.Zombie, 3, 3);
            Walk(s, soldier, P(1, 2));
            Walk(s, soldier, P(0, 3));
            Walk(s, soldier, P(1, 4), P(1, 3), P(1, 2), P(0, 2));
            Assert.Null(s.Discovered);
            Assert.Empty(Los.VisibleEnemies(s, Teams.Xcom));
        }

        [Fact(DisplayName = "buildings › walking in through the door finds it, threshold and all; whoever stands in a doorway shows")]
        public void InThroughTheDoor()
        {
            var s = House();
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 4, 6);
            var doorway = AddUnit(s, "a1", TemplateIds.Zombie, 4, 5);
            Assert.True(Los.CanSee(s, soldier, doorway));
            Assert.Null(s.Discovered);
            var s2 = House();
            var walker = AddUnit(s2, "s1", TemplateIds.Rookie, 4, 6);
            var zombie = AddUnit(s2, "a1", TemplateIds.Zombie, 5, 2);
            Assert.False(Los.CanSee(s2, walker, zombie));
            // The threshold counts as in (Arturo, 28/10), and walking on through, every step does.
            Walk(s2, walker, P(4, 5));
            Assert.True(Los.CanSee(s2, walker, zombie));
            var s3 = House();
            var runner = AddUnit(s3, "s1", TemplateIds.Rookie, 4, 6);
            Walk(s3, runner, P(4, 5), P(4, 4), P(4, 5), P(4, 6));
            Assert.Equal(9 + 1, s3.Discovered!.Count);
        }

        [Fact(DisplayName = "windows › the enemies see through none, in or out (Arturo, 31/10); through an open door they do; the squad still does")]
        public void TheEnemiesSeeThroughNoWindow()
        {
            var s = House();
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 1, 3);
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 3, 3, extra: (u) => u.Facing = System.Math.PI);
            // Right at the glass the squad finds the room and sees in; the dead inside see nobody out.
            Assert.True(Los.CanSee(s, soldier, zombie));
            Assert.False(Los.CanSee(s, zombie, soldier));
            Assert.False(Los.TeamVisibility(s, Teams.Alien)[3 * 12 + 1]);
            Assert.True(Los.TeamVisibility(s, Teams.Xcom)[3 * 12 + 3]);
            // Nor the living, who shoot.
            var raider = AddUnit(s, "a2", TemplateIds.Trooper, 0, 3);
            zombie.Pos = P(4, 3);
            Assert.False(Los.CanSee(s, AddUnit(s, "a3", TemplateIds.Trooper, 3, 2), soldier));
            Assert.Null(Aim.PreviewShot(s, Grid.GetUnit(s, "a3")!, soldier));
            Assert.True(Los.CanSee(s, raider, soldier));
            // Out of the house by its door, open: in sight, both ways.
            var open = House();
            open.Tiles[5 * 12 + 4] = TileKinds.DoorOpen;
            var walker = AddUnit(open, "s1", TemplateIds.Rookie, 4, 7);
            var inside = AddUnit(open, "a1", TemplateIds.Zombie, 4, 3, extra: (u) => u.Facing = System.Math.PI / 2);
            Assert.True(Los.CanSee(open, inside, walker));
        }

        [Fact(DisplayName = "buildings › a siege's map is the squad's own base: nothing hidden; a map without storeys, nothing either")]
        public void SiegeAndNoStoreys()
        {
            var s = House();
            s.Mission.Siege = new Siege { Hold = 10 };
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 0, 3);
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 3, 3);
            Assert.True(Los.CanSee(s, soldier, zombie));
            var flat = StateFrom(HouseRows);
            var watcher = AddUnit(flat, "s1", TemplateIds.Rookie, 0, 3);
            var inside = AddUnit(flat, "a1", TemplateIds.Zombie, 3, 3);
            Assert.True(Los.CanSee(flat, watcher, inside));
        }
    }
}
