using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/infection.test.ts: phase 3 of the zombies, infection and medicines (docs/FASE3.md). The
    /// tests of «infection in combat». `client toEqual s` is Step2Json.StateDiff. Not ported, the seven of «infection at
    /// the base» (a fever…, untreated it turns…, treating takes both keys…, no medicines…, a bitten soldier cannot join…,
    /// medicines heal wounds…, La Vega and Tarbes send medicines…): they play a campaign and need campaign.ts
    /// (createCampaign, executeCampaignCommand, applyMissionResult, buildSquad; the helper raise).
    /// </summary>
    public class InfectionTests
    {
        private static GameState Field()
        {
            return StateFrom(Enumerable.Range(0, 12).Select((_) => new string('.', 12)).ToArray());
        }

        /// <summary>Out of `n` bites that drew blood, how many infected.</summary>
        private static double BiteRate(string template, int n, Action<Unit>? extra = null)
        {
            var hurt = 0;
            var infected = 0;
            for (var seed = 1; hurt < n; seed++)
            {
                var s = Field();
                s.ActiveTeam = Teams.Alien;
                var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) =>
                {
                    u.Hp = 99;
                    u.MaxHp = 99;
                    extra?.Invoke(u);
                });
                var zombie = AddUnit(s, "a1", template, 6, 5);
                var run = NewSim(s, seed);
                run.Attack(zombie, soldier, AbilityIds.Claw);
                if (soldier.Hp == 99) continue;
                hurt++;
                if (Of<InfectedEvent>(run.Events).Count > 0) infected++;
            }
            return (double)infected / n;
        }

        /// <summary>toBeCloseTo(expected, 1): within 0.05.</summary>
        private static void CloseTo(double expected, double actual)
        {
            Assert.True(Math.Abs(expected - actual) < 0.05, $"expected {expected} (± 0.05), got {actual}");
        }

        // ------------------------------------------------------------ infection in combat

        [Fact(DisplayName = "infection in combat › a bite that draws blood infects by who bites; a Chaleco antibalas takes 10 off")]
        public void ABiteThatDrawsBloodInfects()
        {
            Assert.Equal(new[] { "mec=35", "xenoid=20", "zombie=25" }, Content.Infection.Bite.OrderBy((e) => e.Key, StringComparer.Ordinal).Select((e) => $"{e.Key}={e.Value}"));
            CloseTo(0.25, BiteRate(TemplateIds.Zombie, 1500));
            CloseTo(0.2, BiteRate(TemplateIds.Xenoid, 1500));
            CloseTo(0.35, BiteRate(TemplateIds.Mec, 1500));
            CloseTo(0.15, BiteRate(TemplateIds.Zombie, 1500, (u) => u.Gear = new List<string> { ItemIds.NanoVest }));
        }

        [Fact(DisplayName = "infection in combat › no blood, no infection: a miss, or a bite the armour stops; and only the living of the squad")]
        public void NoBloodNoInfection()
        {
            for (var seed = 1; seed < 80; seed++)
            {
                var s = Field();
                var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => u.Armor = 20);
                var zombie = AddUnit(s, "a1", TemplateIds.Zombie, 6, 5);
                var run = NewSim(s, seed);
                run.Attack(zombie, soldier, AbilityIds.Claw);
                Assert.Empty(Of<InfectedEvent>(run.Events));
            }
        }

        [Fact(DisplayName = "infection in combat › the event marks the soldier for the rest of the mission; the botiquín heals wounds, not bites")]
        public void TheEventMarksTheSoldier()
        {
            var s = Field();
            var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) =>
            {
                u.Hp = 2;
                u.MaxHp = 6;
                u.Charges = new Dictionary<string, int> { [ChargeIds.FirstAid] = 1 };
                u.Gear = new List<string> { ItemIds.FirstAid };
            });
            Events.ApplyEvent(s, new InfectedEvent { Unit = "s1", By = "a1" });
            Assert.True(soldier.Infected);
            NewSim(s).Perform(soldier, AbilityIds.FirstAid, target: soldier);
            Assert.Equal(6, soldier.Hp);
            Assert.True(soldier.Infected);
        }

        [Fact(DisplayName = "infection in combat › a Hinchado’s burst infects half of those it hurts")]
        public void AHinchadosBurstInfectsHalf()
        {
            var hurt = 0;
            var infected = 0;
            for (var seed = 1; seed < 800; seed++)
            {
                var s = Field();
                var soldier = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) =>
                {
                    u.Hp = 99;
                    u.MaxHp = 99;
                });
                var bloater = AddUnit(s, "a1", TemplateIds.Bloater, 6, 5, extra: (u) => u.Hp = 1);
                var run = NewSim(s, seed);
                run.Damage(bloater, 5, 0);
                if (soldier.Hp < 99) hurt++;
                if (Of<InfectedEvent>(run.Events).Any((e) => e.Unit == "s1" && e.By == "a1")) infected++;
            }
            Assert.Equal(799, hurt);
            CloseTo(Content.Infection.Burst / 100.0, (double)infected / hurt);
        }

        [Fact(DisplayName = "infection in combat › an infected soldier who falls rises as the zombies’ turn begins, and acts from the next")]
        public void AnInfectedSoldierWhoFallsRises()
        {
            var s = Field();
            var look = new Appearance { Primary = "#123456", Secondary = "#654321", Visor = "#9fe3ff", Head = HeadIds.Helmet, Build = BuildIds.Normal, Sex = Sexes.F };
            var fallen = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) =>
            {
                u.Infected = true;
                u.Appearance = look;
            });
            var clean = AddUnit(s, "s2", TemplateIds.Rookie, 7, 5);
            var out_ = AddUnit(s, "s3", TemplateIds.Rookie, 0, 0, extra: (u) =>
            {
                u.Infected = true;
                u.Alive = false;
                u.Evacuated = true;
            });
            AddUnit(s, "s4", TemplateIds.Rookie, 0, 11, extra: (u) =>
            {
                u.Hp = 99;
                u.MaxHp = 99;
            });
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 11, 11);
            Pod(s, "p1", new[] { z }, active: false);
            var run = NewSim(s, 1);
            run.Damage(fallen, 99, 0, z);
            run.Damage(clean, 99, 0, z);
            var client = Events.CloneState(s);
            var turn = NewSim(s, 2);
            turn.RunAlienTurn();
            var risen = Of<ReanimatedEvent>(turn.Events);
            Assert.Single(risen);
            Assert.Equal("s1", risen[0].Corpse);
            Assert.True(risen[0].Pod?.Active);
            Assert.Null(risen[0].Unit);
            var zombie = s.Units.First((u) => u.Id == risen[0].Zombie.Id);
            // The soldier keeps its name and its look (playtest 2026-10-16); anyone else rises as a Zombi.
            Assert.Equal((TemplateIds.Zombie, "Zombi · s1", true, 5, 5, "s1"), (zombie.Template, zombie.Name, zombie.Alive, zombie.Pos.X, zombie.Pos.Y, zombie.RisenFrom));
            Assert.Equal(Step2Json.Element(look).GetRawText(), Step2Json.Element(zombie.Appearance).GetRawText());
            Assert.Equal(Content.Templates[TemplateIds.Zombie].Name, Rules.RisenName(AddUnit(Events.CloneState(s), "a9", TemplateIds.Trooper, 0, 0)));
            Assert.Equal(new[] { zombie.Id }, s.Pods.FirstOrDefault((p) => p.Id == zombie.PodId)?.UnitIds);
            Assert.True(fallen.Reanimated);
            Assert.DoesNotContain(Of<MovedEvent>(turn.Events), (e) => e.Unit == zombie.Id);
            Assert.False(out_.Reanimated);
            // Once only, and the clients see the same.
            var next = NewSim(s, 3);
            next.RunAlienTurn();
            Assert.Empty(Of<ReanimatedEvent>(next.Events));
            foreach (var e in turn.Events) Events.ApplyEvent(client, e);
            foreach (var e in next.Events) Events.ApplyEvent(client, e);
            Assert.Null(Step2Json.StateDiff(s, client));
        }
    }
}
