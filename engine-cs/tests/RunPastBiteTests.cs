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
    /// box the same day. Rules.RunPastBiters.
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

        [Fact(DisplayName = "running past › walking past, or stopping alongside, draws no bite then")]
        public void WalkingPastDrawsNoBite()
        {
            var s = Street(out var soldier, out var zombie);
            // Five tiles of eight: a walk, and it stops alongside, at (6, 2). The dead bite on their own turn.
            var r = Exec(s, new MoveCommand { Unit = soldier.Id, To = P(6, 2) });
            Assert.True(r.Ok, r.Error);
            Assert.Empty(Bites(r, zombie));
        }
    }
}
