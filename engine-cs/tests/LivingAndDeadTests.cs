using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// N3 (Arturo, 22/11: «Los zombies atacan a los saqueadores y el resto de humanos y el resto de humanos les atacan a
    /// ellos»): the dead asleep that see one of the living wake for them (Rules.WakeForTheLiving); the living still unaware of
    /// the squad shoot the dead they see close (Ai.FendOff); the squad stays hidden through it; the living's noise winds the
    /// horde everywhere (Events).
    /// </summary>
    public class LivingAndDeadTests
    {
        private const double East = 0;
        private static readonly double West = System.Math.PI;

        /// <summary>
        /// An open field 20 × 9, the squad hidden far off at (1, 1) looking away; a Saqueador asleep at (8, 4) looking east;
        /// one of the dead asleep at (13, 4) looking west, at it.
        /// </summary>
        private static GameState Field(out Unit soldier, out Unit raider, out Unit dead)
        {
            var s = StateFrom(Enumerable.Repeat(new string('.', 20), 9).ToArray());
            s.Concealed = true;
            soldier = AddUnit(s, "s1", TemplateIds.Rookie, 1, 1, extra: (u) => u.Facing = West);
            raider = AddUnit(s, "a1", TemplateIds.Trooper, 8, 4, extra: (u) => u.Facing = East);
            dead = AddUnit(s, "a2", TemplateIds.Zombie, 13, 4, extra: (u) => u.Facing = West);
            Pod(s, "p1", new[] { raider }, active: false);
            Pod(s, "p2", new[] { dead }, active: false);
            s.ActiveTeam = Teams.Alien;
            return s;
        }

        [Fact]
        public void TheDeadAsleepWakeForTheLivingAndTheLivingShootThemTheSquadStillHidden()
        {
            var s = Field(out _, out var raider, out var dead);
            foreach (var u in s.Units) u.Ap = 2;
            var sim = NewSim(s, 3);
            sim.RunAlienTurn();
            // The dead one woke for the Saqueador, and went for it.
            Assert.True(s.Pods.Single((p) => p.UnitIds.Contains(dead.Id)).Active || !dead.Alive);
            Assert.Contains(sim.Events, (e) => e is HuntingEvent h && h.Unit == dead.Id && h.Target == raider.Id);
            // The Saqueador shot at it, still unaware of the squad: its group asleep.
            Assert.Contains(sim.Events, (e) => e is ShotEvent sh && sh.Unit == raider.Id && sh.Target == dead.Id);
            Assert.False(s.Pods.Single((p) => p.Id == "p1").Active);
            // Nobody of the squad given away.
            Assert.True(s.Concealed);
            Assert.Empty(Of<ConcealmentBrokenEvent>(sim.Events));
        }

        [Fact]
        public void HavingShotItLooksWhereItLookedBefore()
        {
            // Red's R18 (22/11): a fire guard turned to shoot one of the dead saw the hidden squad on its turn. Back to its look.
            var s = Field(out _, out var raider, out _);
            raider.Facing = -0.4;
            foreach (var u in s.Units) u.Ap = 2;
            var sim = NewSim(s, 3);
            sim.RunAlienTurn();
            Assert.Contains(sim.Events, (e) => e is ShotEvent sh && sh.Unit == raider.Id);
            if (raider.Alive) Assert.Equal(-0.4, raider.Facing);
        }

        [Fact]
        public void FarOffOrUnseenNeitherWakesNorShoots()
        {
            var s = Field(out _, out var raider, out var dead);
            dead.Pos = new Pos { X = 19, Y = 8, Z = 0 };
            dead.Facing = East;
            raider.Facing = West;
            foreach (var u in s.Units) u.Ap = 2;
            var sim = NewSim(s, 3);
            sim.RunAlienTurn();
            Assert.DoesNotContain(sim.Events, (e) => e is ShotEvent sh && sh.Unit == raider.Id);
            Assert.DoesNotContain(sim.Events, (e) => e is HuntingEvent h && h.Target == raider.Id);
        }

        [Fact]
        public void TheLivingsNoiseWindsTheHordeAnywhere()
        {
            var s = Field(out _, out var raider, out var dead);
            var before = s.Mission.Noise ?? 0;
            Events.ApplyEvent(s, new NoiseEvent { Tile = raider.Pos, Radius = 12, By = raider.Id, Heard = new System.Collections.Generic.List<string>() });
            Assert.Equal(before + Content.HordePoints(12), s.Mission.Noise ?? 0);
            Events.ApplyEvent(s, new NoiseEvent { Tile = dead.Pos, Radius = 12, By = dead.Id, Heard = new System.Collections.Generic.List<string>() });
            Assert.Equal(before + Content.HordePoints(12), s.Mission.Noise ?? 0);
        }

        [Fact]
        public void OneOfTheLivingsNoiseOnlyTheDeadGoToLookAt()
        {
            // Red's R18 (22/11): a Cuervo's shot at the dead doesn't send the other Cuervos looking (the camp's stealth as before).
            var s = Field(out _, out var raider, out var dead);
            var other = AddUnit(s, "a3", TemplateIds.Trooper, 9, 6, extra: (u) => u.Facing = East);
            Pod(s, "p3", new[] { other }, active: false);
            var sim = NewSim(s, 3);
            sim.MakeNoise(raider.Pos, 12, raider);
            var heard = Of<NoiseEvent>(sim.Events).Single().Heard;
            Assert.Contains(dead.Id, heard);
            Assert.DoesNotContain(other.Id, heard);
        }

        [Fact]
        public void ACampsGuardTheDeadGoForStaysAtItsPostTheCampAsleep()
        {
            var s = Field(out _, out var raider, out var dead);
            raider.Guard = GuardPosts.Fire;
            var sim = NewSim(s, 3);
            sim.ActivatePodOf(raider, dead);
            Assert.False(s.Pods.Single((p) => p.Id == "p1").Active);
            // One not of a camp, yes.
            raider.Guard = null;
            NewSim(s, 3).ActivatePodOf(raider, dead);
            Assert.True(s.Pods.Single((p) => p.UnitIds.Contains(raider.Id)).Active);
        }
    }
}
