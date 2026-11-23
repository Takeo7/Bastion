using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// N9 (Arturo, 22/11, cuadro 114): the game's level (CampaignState.GameLevel), chosen when it is made: the dead and the horde,
    /// what is found and paid; Supervivencia's wounds carried home. Missing, hard: today's, every game made before.
    /// </summary>
    public class GameLevelTests
    {
        [Fact]
        public void FourLevelsHardWhenMissingAndHardIsToday()
        {
            Assert.Equal(new[] { LevelIds.Easy, LevelIds.Medium, LevelIds.Hard, LevelIds.Survival }, LevelIds.All);
            var c = Solo();
            Assert.Null(c.GameLevel);
            Assert.Same(Levels.All[LevelIds.Hard], Levels.Of(c));
            var hard = Levels.All[LevelIds.Hard];
            Assert.Equal((1.0, 1.0, 1.0, false), (hard.Dead, hard.Horde, hard.Loot, hard.Wounds));
            Assert.True(Levels.All[LevelIds.Survival].Wounds);
            Assert.All(LevelIds.All.Where((l) => l != LevelIds.Survival), (l) => Assert.False(Levels.All[l].Wounds));
            // Easier, fewer dead, a slower horde, more found; harder, the other way.
            Assert.True(Levels.All[LevelIds.Easy].Dead < Levels.All[LevelIds.Medium].Dead && Levels.All[LevelIds.Medium].Dead < 1 && Levels.All[LevelIds.Survival].Dead > 1);
            Assert.True(Levels.All[LevelIds.Easy].Loot > Levels.All[LevelIds.Medium].Loot && Levels.All[LevelIds.Medium].Loot > 1 && Levels.All[LevelIds.Survival].Loot < 1);
        }

        [Fact]
        public void TheDeadsGroupsByTheLevelTheLivingsAsTheyAre()
        {
            var plan = new List<List<string>>
            {
                new List<string> { TemplateIds.Trooper, TemplateIds.Officer },
                new List<string> { TemplateIds.Zombie, TemplateIds.Zombie },
                new List<string> { TemplateIds.Zombie, TemplateIds.Xenoid },
                new List<string> { TemplateIds.Zombie },
                new List<string> { TemplateIds.Zombie, TemplateIds.Bloater },
                new List<string> { TemplateIds.Zombie, TemplateIds.Zombie, TemplateIds.Zombie },
            };
            Assert.Same(plan, Levels.Pods(plan, 1));
            int Dead(List<List<string>> p) => p.Count((g) => g.All((t) => Content.Templates[t].Undead));
            Assert.Equal(3, Dead(Levels.Pods(plan, 0.6)));
            Assert.Equal(6, Dead(Levels.Pods(plan, 1.25)));
            Assert.All(new[] { 0.6, 1.25 }, (k) => Assert.Single(Levels.Pods(plan, k), (g) => g.Contains(TemplateIds.Trooper)));
        }

        [Fact]
        public void AFightAtTheLevelFewerDeadASlowerHordeMoreFound()
        {
            int DeadOf(GameState s) => s.Units.Count((u) => u.Team == Teams.Alien && Content.Templates[u.Template].Undead);
            var (_, hard) = LaunchAt(NodeKinds.Normal, 3);
            var (_, easy) = LaunchAt(NodeKinds.Normal, 3, before: (c) => c.GameLevel = LevelIds.Easy);
            var (_, survival) = LaunchAt(NodeKinds.Normal, 3, before: (c) => c.GameLevel = LevelIds.Survival);
            Assert.True(DeadOf(easy) < DeadOf(hard), $"{DeadOf(easy)} {DeadOf(hard)}");
            Assert.True(DeadOf(survival) > DeadOf(hard), $"{DeadOf(survival)} {DeadOf(hard)}");
            Assert.Null(hard.Mission.HordeScale);
            Assert.Null(hard.Run!.Loot);
            Assert.Equal(Levels.All[LevelIds.Easy].Horde, easy.Mission.HordeScale);
            Assert.Equal(Levels.All[LevelIds.Easy].Loot, easy.Run!.Loot);
            Assert.True(RunLoot.SuppliesScale(easy.Run) > RunLoot.SuppliesScale(hard.Run));
            // The horde's meter fills sooner with a bigger scale.
            hard.Mission.Noise = 25;
            survival.Mission.Noise = 25;
            Assert.True(Rules.HordeFill(survival) > Rules.HordeFill(hard));
        }

        [Fact]
        public void SurvivalsWoundsComeHomeAndOutAgainTheRefugeHealsThem()
        {
            var c = Solo();
            c.GameLevel = LevelIds.Survival;
            var run = Start(c);
            var s = Squad(c)[0];
            run.Hp[s.Id] = Runs.MaxHp(s) - 3;
            Runs.EndRun(c, new Rng(1));
            Assert.Equal(3, s.Hurt);
            // Its line says so (Lore, 23/11): not «todos se curan».
            Assert.Equal(RunTexts.RunEndedHurt, c.Log[0].Text);
            // Out again, still hurt.
            Start(c);
            Assert.Equal(Runs.MaxHp(s) - 3, Runs.HpOf(c, s));
            // Healed full (a Refuge), home whole.
            c.Run!.Hp[s.Id] = Runs.MaxHp(s);
            Runs.EndRun(c, new Rng(1));
            Assert.Null(s.Hurt);
            // At any other level, home is home: nothing carried.
            var h = Solo();
            var hr = Start(h);
            var hs = Squad(h)[0];
            hr.Hp[hs.Id] = Runs.MaxHp(hs) - 3;
            Runs.EndRun(h, new Rng(1));
            Assert.Null(hs.Hurt);
            Assert.Equal(RunTexts.RunEnded, h.Log[0].Text);
            Start(h);
            Assert.Equal(Runs.MaxHp(hs), Runs.HpOf(h, hs));
        }
    }
}
