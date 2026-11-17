using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Arturo's notes of 15/11 (.dev-snapshots/gameplay/notas-2026-11-15/propuestas, p1): «Correr junto a un zombi. Darle
    /// una posibilidad de intentar morder al soldado que pasa corriendo. El intento podría acertar o fallar.» Chosen in the
    /// box the same day. Since T20 (Arturo, 18/11) walking past too, if it sees the soldier: Rules.PassBite, Sim.PassBiters,
    /// and the marks before moving, Rules.ReactionsAlong.
    /// </summary>
    public class RunPastBiteTests
    {
        /// <summary>An open street, a soldier at its west end (1, 2) and one of the dead by the way, at (5, 1).</summary>
        private static GameState Street(out Unit soldier, out Unit zombie)
        {
            var s = StateFrom(new[]
            {
                "............",
                "............",
                "............",
                "............",
            });
            soldier = AddUnit(s, "s1", TemplateIds.Assault, 1, 2);
            zombie = AddUnit(s, "a1", TemplateIds.Zombie, 5, 1);
            Pod(s, "p1", new[] { zombie });
            return s;
        }

        private static List<ShotEvent> Bites(CommandResult r, Unit zombie) =>
            Of<ShotEvent>(r.Events).Where((e) => e.Unit == zombie.Id && e.Ability == AbilityIds.Claw).ToList();

        [Fact(DisplayName = "running past › one of the dead alongside the way tries a bite, once, as a reaction")]
        public void OneOfTheDeadTriesABite()
        {
            var s = Street(out var soldier, out var zombie);
            // The whole movement at once (8 tiles): a run, past (4, 2), (5, 2) and (6, 2), all within its reach.
            var r = Exec(s, new MoveCommand { Unit = soldier.Id, To = P(9, 2) });
            Assert.True(r.Ok, r.Error);
            var bites = Bites(r, zombie);
            Assert.Single(bites);
            Assert.Equal(soldier.Id, bites[0].Target);
            Assert.True(bites[0].Reaction);
            // It may miss: less than the dead's whole aim.
            Assert.True(bites[0].Chance < Content.Templates[TemplateIds.Zombie].Aim);
        }

        [Fact(DisplayName = "walking past › a bite too, if it sees the soldier (T20); stopping alongside, none then")]
        public void WalkingPastBitesTooButNotWhereItStops()
        {
            // T20 (Arturo, 18/11): «siempre que te vea, no solo cuando corras». Five tiles of eight, a walk: past (4, 2) and
            // (5, 2), where it bites; it stops at (6, 2), alongside, and that tile draws nothing more.
            var s = Street(out var soldier, out var zombie);
            var path = new List<Pos> { P(2, 2), P(3, 2), P(4, 2), P(5, 2), P(6, 2) };
            var marks = Rules.ReactionsAlong(s, soldier, path);
            var mark = Assert.Single(marks);
            Assert.Equal((2, zombie.Id, PathReactions.Bite), (mark.Index, mark.Unit, mark.Kind));
            var r = Exec(s, new MoveCommand { Unit = soldier.Id, To = P(6, 2) });
            Assert.True(r.Ok, r.Error);
            var bites = Bites(r, zombie);
            Assert.Single(bites);
            Assert.True(bites[0].Reaction);
            // Stopping right beside it, from one tile off: nothing on the way.
            var t = Street(out var other, out var dead);
            other.Pos = P(3, 2);
            Assert.Empty(Rules.ReactionsAlong(t, other, new List<Pos> { P(4, 2) }));
            var stop = Exec(t, new MoveCommand { Unit = other.Id, To = P(4, 2) });
            Assert.True(stop.Ok, stop.Error);
            Assert.Empty(Bites(stop, dead));
        }

        [Fact(DisplayName = "walking past › nothing from beyond a wall")]
        public void NothingBeyondAWall()
        {
            // One of the dead behind the wall that runs along the way: no bite, nor a mark before moving.
            var s = StateFrom(new[]
            {
                "............",
                "....###.....",
                "............",
                "............",
            });
            var soldier = AddUnit(s, "s1", TemplateIds.Assault, 1, 2);
            var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 5, 0);
            Pod(s, "p1", new[] { zombie });
            Assert.False(Rules.PassBite(s, zombie, soldier, P(5, 2)));
            Assert.Empty(Rules.ReactionsAlong(s, soldier, new List<Pos> { P(2, 2), P(3, 2), P(4, 2), P(5, 2), P(6, 2) }));
            var r = Exec(s, new MoveCommand { Unit = soldier.Id, To = P(6, 2) });
            Assert.True(r.Ok, r.Error);
            Assert.Empty(Bites(r, zombie));
        }
    }
}

