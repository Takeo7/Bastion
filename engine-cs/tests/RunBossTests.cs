using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// El Toro, chapter 1's boss (H6, .dev-snapshots/gameplay/runs/H6-TORO.md): his numbers, the Mazazo, the charge and its
    /// line, knocking down and getting up, the roar, what doesn't take him, how he chooses, and his stage: the factory, his
    /// group, won by killing him and getting out, its reward and the chapter done.
    /// </summary>
    public class RunBossTests
    {
        /// <summary>An open field (20×20), El Toro on (2, 5) with his group awake, a soldier on `at`.</summary>
        private static (GameState S, Unit Toro, Unit Soldier) Arena(int x = 6, int y = 5, bool roared = true)
        {
            var s = StateFrom(Enumerable.Range(0, 20).Select((_) => new string('.', 20)).ToArray());
            s.Mission.Kind = MissionKinds.Scavenge;
            var toro = AddUnit(s, "t", TemplateIds.Brute, 2, 5);
            if (roared) toro.Charges[ChargeIds.Roar] = 0;
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, x, y, extra: (u) => u.Owner = 0);
            Pod(s, "pt", new[] { toro }, true);
            return (s, toro, soldier);
        }

        [Fact]
        public void HisNumbers()
        {
            var t = Content.Templates[TemplateIds.Brute];
            Assert.Equal((30, 1, 6.0, "El Toro"), (t.Hp, t.Armor, t.Mobility, t.Name));
            Assert.True(t.Undead);
            Assert.Equal(new[] { AbilityIds.Pound, AbilityIds.Charge, AbilityIds.Roar, AbilityIds.Smash }, t.Abilities);
            var (_, toro, _) = Arena();
            Assert.Equal(2, Content.ApPerTurnOf(toro));
            Assert.Equal(6, Content.AttackWeapon(toro, AbilityIds.Pound).Damage);
            Assert.True(Content.IsAttack(AbilityIds.Pound));
            Assert.Equal((2, 3, true), (Content.Abilities[AbilityIds.Charge].Ap, Content.Abilities[AbilityIds.Charge].Cooldown!.Value, Content.Abilities[AbilityIds.Charge].EndsTurn));
            // The other dead keep their one action.
            Assert.Equal(1, Content.ApPerTurnOf(Setup.MakeUnit("z", TemplateIds.Zombie, "z", null, new Pos(), null)));
        }

        [Fact]
        public void TheChargesLine()
        {
            var (s, toro, soldier) = Arena();
            SamePlaces(new[] { new Pos { X = 3, Y = 5 }, new Pos { X = 4, Y = 5 }, new Pos { X = 5, Y = 5 } }, Toro.ChargePath(s, toro, soldier));
            // Diagonal.
            soldier.Pos = new Pos { X = 5, Y = 8 };
            SamePlaces(new[] { new Pos { X = 3, Y = 6 }, new Pos { X = 4, Y = 7 } }, Toro.ChargePath(s, toro, soldier));
            // Off the eight ways, alongside, or too far: none.
            soldier.Pos = new Pos { X = 6, Y = 6 };
            Assert.Null(Toro.ChargePath(s, toro, soldier));
            soldier.Pos = new Pos { X = 3, Y = 5 };
            Assert.Null(Toro.ChargePath(s, toro, soldier));
            soldier.Pos = new Pos { X = 10, Y = 5 };
            Assert.Null(Toro.ChargePath(s, toro, soldier));
            soldier.Pos = new Pos { X = 9, Y = 5 };
            Assert.Equal(6, Toro.ChargePath(s, toro, soldier)!.Count);
            // Somebody in the way, or a wall.
            soldier.Pos = new Pos { X = 6, Y = 5 };
            var other = AddUnit(s, "s2", TemplateIds.Rookie, 4, 5, extra: (u) => u.Owner = 0);
            Assert.Null(Toro.ChargePath(s, toro, soldier));
            other.Pos = new Pos { X = 15, Y = 15 };
            s.Tiles[Grid.TileIndex(s, 4, 5)] = TileKinds.Wall;
            Assert.Null(Toro.ChargePath(s, toro, soldier));
        }

        [Fact]
        public void TheChargeKnocksDown()
        {
            var (s, toro, soldier) = Arena();
            soldier.Overwatch = true;
            var hp = soldier.Hp;
            var sim = NewSim(s);
            Ai.TakeAlienTurn(sim, toro);
            var tags = Tags(sim.Events);
            Assert.True(tags.IndexOf("charged") < tags.IndexOf("knockedDown") && tags.IndexOf("knockedDown") < tags.IndexOf("damaged"), string.Join(", ", tags));
            var charged = Of<ChargedEvent>(sim.Events).Single();
            Assert.Equal(soldier.Id, charged.Target);
            SamePlace(new Pos { X = 5, Y = 5 }, toro.Pos);
            Assert.Equal(hp - Toro.ChargeDamage, soldier.Hp);
            Assert.True(soldier.Down);
            Assert.False(soldier.Overwatch);
            Assert.Equal(0, toro.Ap);
            Assert.Equal(3, toro.Cooldowns[AbilityIds.Charge]);
            // On the ground: easier to hit.
            var other = AddUnit(s, "z2", TemplateIds.Zombie, 6, 6);
            var down = Aim.PreviewShot(s, other, soldier, new ShotOptions { Ability = AbilityIds.Claw })!.Hit;
            soldier.Down = null;
            var up = Aim.PreviewShot(s, other, soldier, new ShotOptions { Ability = AbilityIds.Claw })!.Hit;
            Assert.Equal(System.Math.Min(100, up + Toro.DownDefense), down);
        }

        [Fact]
        public void TheMazazoWhenTheChargeIsntReady()
        {
            var (s, toro, soldier) = Arena(3, 5);
            var sim = NewSim(s);
            Ai.TakeAlienTurn(sim, toro);
            Assert.Empty(Of<ChargedEvent>(sim.Events));
            Assert.Contains(Of<AbilityUsedEvent>(sim.Events), (e) => e.Ability == AbilityIds.Pound);
            // Charge on its cooldown, a soldier on its line: he walks up instead.
            var (t, toro2, far) = Arena();
            toro2.Cooldowns[AbilityIds.Charge] = 2;
            var walk = NewSim(t);
            Ai.TakeAlienTurn(walk, toro2);
            Assert.Empty(Of<ChargedEvent>(walk.Events));
            Assert.True(Grid.Distance(toro2.Pos, far.Pos) < 4);
        }

        [Fact]
        public void TheRoarWakesTheMap()
        {
            var (s, toro, _) = Arena(roared: false);
            var sleeper = AddUnit(s, "z9", TemplateIds.Zombie, 18, 18);
            Pod(s, "p9", new[] { sleeper }, false);
            var sim = NewSim(s);
            Ai.TakeAlienTurn(sim, toro);
            var tags = Tags(sim.Events);
            Assert.Equal("roared", tags.First((t) => t == "roared" || t == "charged"));
            Assert.Equal(0, toro.Charges[ChargeIds.Roar]);
            Assert.True(s.Pods.Single((p) => p.Id == "p9").Active);
            Assert.NotNull(sleeper.Heard);
            // Once.
            toro.Ap = 2;
            toro.Cooldowns.Clear();
            var again = NewSim(s);
            Ai.TakeAlienTurn(again, toro);
            Assert.Empty(Of<RoaredEvent>(again.Events));
        }

        [Fact]
        public void UpAsTheTurnBegins()
        {
            var (s, toro, soldier) = Arena();
            AddUnit(s, "z9", TemplateIds.Zombie, 18, 18);
            soldier.Down = true;
            var sim = NewSim(s);
            sim.GetUp();
            Assert.Single(Of<GotUpEvent>(sim.Events));
            Assert.Null(soldier.Down);
            Assert.Equal(1, soldier.Ap);
            // In the turn's flow: the squad's turn begins with it.
            soldier.Down = true;
            soldier.Ap = 0;
            s.Command = 0;
            var r = Exec(s, new EndTurnCommand { Ready = true });
            Assert.True(r.Ok, r.Error);
            var tags = Tags(r.Events!);
            Assert.True(tags.IndexOf("gotUp") > tags.LastIndexOf("turnBegan"), string.Join(", ", tags));
            Assert.Equal(Content.ApPerTurn - 1, soldier.Ap);
            Assert.Null(soldier.Down);
        }

        [Fact]
        public void TheGolpeSilenciosoDoesntTakeHim()
        {
            var (s, toro, soldier) = Arena(3, 5);
            Assert.Equal("Solo contra Zombis y Corredores.", Abilities.TakedownRefusal(s, soldier, toro));
        }

        // ------------------------------------------------------------------ his stage

        [Fact]
        public void HisStage()
        {
            var (c, s) = LaunchAt(NodeKinds.Boss);
            Assert.Equal(Biomes.Facility, s.Biome);
            Assert.Equal("El Toro · Fábrica", s.MapName);
            Assert.Equal("El Toro", Lore.StageName(NodeKinds.Boss, 1));
            var toro = s.Units.Single((u) => u.Template == TemplateIds.Brute);
            Assert.Equal(toro.Id, s.Mission.Boss);
            var pod = s.Pods.Single((p) => p.UnitIds.Contains(toro.Id));
            var group = pod.UnitIds.Select((id) => Grid.GetUnit(s, id)!.Template).ToList();
            Assert.Equal(Runs.BossPod(1).OrderBy((x) => x), group.OrderBy((x) => x));
            Assert.Equal((4, 1), (group.Count((t) => t == TemplateIds.Zombie), group.Count((t) => t == TemplateIds.Xenoid)));
            Assert.Equal(Runs.BossPod(3).Count, Runs.BossPod(1).Count + 2);
            // Out without killing him: lost.
            var u = s.Units.First((x) => x.CampaignId != null);
            var z = s.Mission.EvacZone[0];
            u.Pos = new Pos { X = z.X, Y = z.Y, Z = 0 };
            s.Command = 0;
            Assert.True(Exec(s, new EvacCommand { Unit = u.Id }).Ok);
            Assert.Equal((Outcomes.Defeat, EndReasons.Abandoned), (s.Outcome, s.OutcomeReason));
            // Him dead, then out: won, his reward, the chapter done.
            var (d, t) = LaunchAt(NodeKinds.Boss);
            var him = t.Units.Single((x) => x.Template == TemplateIds.Brute);
            NewSim(t).Damage(him, 99, 0);
            Assert.True(t.Mission.ObjectiveDone);
            var w = t.Units.First((x) => x.CampaignId != null);
            var zone = t.Mission.EvacZone[0];
            w.Pos = new Pos { X = zone.X, Y = zone.Y, Z = 0 };
            t.Command = 0;
            Assert.True(Exec(t, new EvacCommand { Unit = w.Id }).Ok);
            Assert.Equal(Outcomes.Victory, t.Outcome);
            var report = EndFight(d, t, t.Outcome!, t.OutcomeReason!);
            Assert.Equal(Runs.BossReward, report.Run!.Reward);
            Assert.Contains(RunLoot.GoodWeapons, (k) => report.Run.Prize!.Single().Type == k);
            Assert.Equal(RunEnds.Boss, report.Run.Ended);
            Assert.Null(d.Run);
            Assert.Equal((2, 1), (d.Chapter, d.ChaptersDone));
            Assert.Contains(d.Soldiers.Where((x) => x.Alive).SelectMany((x) => x.Kit!.Pack.Items), (p) => p.Gear.Id == report.Run.Prize!.Single().Id);
        }

        [Fact]
        public void TheFightNameHasTheChapter()
        {
            var n = new RunNode { Kind = NodeKinds.Boss, Site = Places.Factory };
            Assert.Equal("El Toro · Fábrica", RunTexts.FightName(n, 1));
            Assert.Equal($"{Lore.StageName(NodeKinds.Normal, 2)} · Campo", RunTexts.FightName(new RunNode { Kind = NodeKinds.Normal, Site = Places.Country }, 2));
        }
    }
}
