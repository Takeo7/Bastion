using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>Arturo's notes of 5/11 (docs/PRUEBA-2026-11-05-ARTURO.md), the engine's part.</summary>
    public class NotesOfFiveNovTests
    {
        /// <summary>A soldier at (5, 15) on an open field, the squad seen, and one of `template` east of it, `share` of its sight away, facing it.</summary>
        static (GameState s, Unit soldier, Unit enemy) Facing(double share, string template = TemplateIds.Zombie)
        {
            var s = ZombiesTests.Field(30, 30);
            s.Concealed = false;
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 5, 15);
            AddUnit(s, "s2", TemplateIds.Rookie, 1, 1);
            var enemy = AddUnit(s, "a1", template, 6, 15);
            var range = Los.SightTo(s, enemy, soldier.Pos);
            enemy.Pos = P(5 + (int)Math.Floor(range * share), 15);
            enemy.Facing = Los.FacingTo(enemy.Pos, soldier.Pos)!.Value;
            return (s, soldier, enemy);
        }

        [Fact(DisplayName = "#4 cover hides from the dead › behind half cover they see a soldier at 3/4 of their range, behind full cover at half")]
        public void CoverHidesFromTheDead()
        {
            var (s, soldier, z) = Facing(0.85);
            Assert.True(Los.CanSee(s, z, soldier));
            s.Tiles[15 * s.Width + 6] = TileKinds.Low;
            Assert.Equal(Content.CoverSight.Half, Los.CoverShare(s, z, soldier.Pos, soldier.Team));
            Assert.False(Los.CanSee(s, z, soldier));
            Assert.Empty(Los.Watchers(s, soldier, soldier.Pos));

            (s, soldier, z) = Facing(0.6);
            s.Tiles[15 * s.Width + 6] = TileKinds.Low;
            Assert.True(Los.CanSee(s, z, soldier));
            Assert.Equal(new[] { z.Id }, Los.Watchers(s, soldier, soldier.Pos).Select((u) => u.Id));
            s.Tiles[15 * s.Width + 6] = TileKinds.High;
            Assert.Equal(Content.CoverSight.Full, Los.CoverShare(s, z, soldier.Pos, soldier.Team));
            Assert.False(Los.CanSee(s, z, soldier));
        }

        [Fact(DisplayName = "#4 cover hides from the dead › only the cover between them counts, and the living see as before")]
        public void OnlyTheCoverBetweenThemCounts()
        {
            var (s, soldier, z) = Facing(0.85);
            // On the far side of the soldier: no cover against one coming from the east.
            s.Tiles[15 * s.Width + 4] = TileKinds.High;
            Assert.Equal(1, Los.CoverShare(s, z, soldier.Pos, soldier.Team));
            Assert.True(Los.CanSee(s, z, soldier));
            // One of the living sees past it: cover is for its shots.
            var (s2, soldier2, living) = Facing(0.85, TemplateIds.Trooper);
            s2.Tiles[15 * s2.Width + 6] = TileKinds.High;
            Assert.Equal(1, Los.CoverShare(s2, living, soldier2.Pos, soldier2.Team));
            Assert.True(Los.CanSee(s2, living, soldier2));
        }

        [Fact(DisplayName = "#4 cover hides from the dead › hiding behind cover counts the one that hides more, not both")]
        public void HidingAndCoverDontAddUp()
        {
            var (s, soldier, z) = Facing(0.45);
            s.Tiles[15 * s.Width + 6] = TileKinds.Low;
            soldier.Hidden = true;
            Assert.Equal(Content.HideSight, Los.ConcealShare(s, z, soldier));
            Assert.True(Los.CanSee(s, z, soldier));
            s.Tiles[15 * s.Width + 6] = TileKinds.High;
            Assert.Equal(Math.Min(Content.HideSight, Content.CoverSight.Full), Los.ConcealShare(s, z, soldier));
        }

        // Red, in the app of 0.12.0: the screen draws from copies, and they had every soldier with the whole movement.
        [Fact(DisplayName = "#14 moving apart from the actions › a copy of the state keeps the movement each one has left, as the state itself")]
        public void ACopyKeepsTheMovementLeft()
        {
            var (s, soldier, _) = Facing(0.85);
            soldier.Moves = 3;
            soldier.Ap = 0;
            var copy = Events.CloneState(s);
            Assert.Equal(3, copy.Units.Single((u) => u.Id == soldier.Id).Moves);
            Assert.Equal(Bastion.Net.Json.Write(s), Bastion.Net.Json.Write(copy));
        }

        [Fact(DisplayName = "#4 cover hides from the dead › a hidden squad slips past a group behind cover it would be noticed without")]
        public void AHiddenSquadSlipsPastBehindCover()
        {
            var (s, soldier, z) = Facing(0.5);
            s.Concealed = true;
            var detection = Los.DetectionTo(s, z, soldier.Pos);
            z.Pos = P(5 + (int)Math.Floor(detection * 0.85), 15);
            z.Facing = Los.FacingTo(z.Pos, soldier.Pos)!.Value;
            ZombiesTests.PodOf(s, "p1", false, z);
            Assert.Equal(new[] { "p1" }, NewSim(s).PodsNoticing(soldier.Pos));
            s.Tiles[15 * s.Width + 6] = TileKinds.Low;
            Assert.Empty(NewSim(s).PodsNoticing(soldier.Pos));
            Assert.Empty(Los.Watchers(s, soldier, soldier.Pos));
        }
    }
}
