using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G15 (Arturo, 18/11: «tengo que poder mejorar las habilidades de los soldados entre etapas/nodos»): a cell of the board
    /// is learnt between the stages of a run, at a stop too, and counts in the next fight; not with a fight going on.
    /// </summary>
    public class RunLearnTests
    {
        /// <summary>The first ring's «Pulso firme»: +3 aim with firearms (UnitMods.GunAim).</summary>
        private const string Cell = "steadyHand";

        /// <summary>A point for the board: a bar of Disparo filled.</summary>
        private static void APoint(CampaignSoldier s)
        {
            var skills = Bars.NoSkills();
            skills[SkillIds.Shooting] = Bars.BarsCost(1);
            s.Skills = skills;
            s.Board.Clear();
            Assert.Null(Bars.UnlockBlocker(s.Skills, s.Board, Cell));
        }

        /// <summary>The next stage a fight of `kind`, chosen and launched as the room does it: the mission.</summary>
        private static GameState Next(CampaignState c, string kind = NodeKinds.Normal)
        {
            Assert.True(GoTo(c, kind).Launch);
            return new Bastion.Net.EngineCampaignRules().LaunchMission(c, new Rng(77))!.Value.state;
        }

        private static int GunAim(GameState s, CampaignSoldier of) => s.Units.Single((u) => u.CampaignId == of.Id).Mods.GunAim;

        [Fact]
        public void LearntBetweenStagesItCountsInTheNext()
        {
            var (c, s) = LaunchAt(NodeKinds.Normal);
            var sol = Squad(c)[0];
            APoint(sol);
            var unlock = new UnlockCommand { Soldier = sol.Id, Cell = Cell };
            // With the fight going on: no, and nothing learnt.
            Assert.Equal(RunPhases.Fight, c.Run!.Phase);
            Refused(RunTexts.LearnInFight, c, unlock);
            Assert.Empty(sol.Board);
            var before = GunAim(s, sol);
            EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            // On the map, between stages: learnt.
            Assert.Equal(RunPhases.Choose, c.Run!.Phase);
            Assert.True(Campaign.CanSpendPoints(c, sol));
            Ok(c, unlock);
            Assert.Equal(new[] { Cell }, sol.Board);
            Assert.Equal($"{sol.Name} aprende Pulso firme.", c.Log[0].Text);
            // The next stage's fighter with it.
            Assert.Equal(before + 3, GunAim(Next(c), sol));
        }

        [Fact]
        public void AtAStopToo()
        {
            var (c, s) = LaunchAt(NodeKinds.Normal);
            var sol = Squad(c)[0];
            APoint(sol);
            var before = GunAim(s, sol);
            EndFight(c, s, Outcomes.Victory, EndReasons.Objective);
            GoTo(c, NodeKinds.Refuge);
            Assert.Equal(RunPhases.Stop, c.Run!.Phase);
            Ok(c, new UnlockCommand { Soldier = sol.Id, Cell = Cell });
            Ok(c, new LeaveStopCommand());
            Assert.Equal(before + 3, GunAim(Next(c), sol));
        }

        [Fact]
        public void TwoPlayersEachTheirOwn()
        {
            // As before the runs (Arturo, 18/10 #42): only its own commander, the other here or not.
            var c = Two();
            Start(c);
            var theirs = Squad(c).First((s) => s.Owner == 1);
            APoint(theirs);
            var unlock = new UnlockCommand { Soldier = theirs.Id, Cell = Cell };
            Refused("Solo puedes elegir las habilidades de tus soldados.", c, unlock, 0, Both);
            Ok(c, unlock, 1, Both);
            Assert.Equal(new[] { Cell }, theirs.Board);
        }
    }
}
