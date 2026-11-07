using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.CampaignHelpers;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/infection.test.ts: phase 3 of the zombies, infection and medicines (docs/FASE3.md), test by
    /// test: the five of «infection in combat» and the seven of «infection at the base», which play a campaign. `client
    /// toEqual s` is Step2Json.StateDiff. Its helpers `field`, `pod`, `biteRate`, and the base's `run` and `bitten`, here
    /// as private ones (`pod` is Step2Helpers.Pod).
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

        [Fact(DisplayName = "infection in combat › a bite or a Hinchado's burst that draws blood only hurts: nobody is infected (Arturo, 7/11)")]
        public void ABiteOnlyHurts()
        {
            foreach (var template in new[] { TemplateIds.Zombie, TemplateIds.Xenoid, TemplateIds.Mec })
                Assert.Equal(0, BiteRate(template, 200));
            for (var seed = 1; seed < 100; seed++)
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
                Assert.True(soldier.Hp < 99);
                Assert.Empty(Of<InfectedEvent>(run.Events));
            }
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

        [Fact(DisplayName = "infection in combat › every soldier who falls rises as the zombies’ turn begins, infected or not, and acts from the next (Arturo, 7/11)")]
        public void EverySoldierWhoFallsRises()
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
            // Both who fell, the infected and the one never bitten (Arturo, 7/11: «cualquier soldado que muera»); not one evacuated.
            var risen = Of<ReanimatedEvent>(turn.Events);
            Assert.Equal(new[] { "s1", "s2" }, risen.Select((e) => e.Corpse).OrderBy((x) => x, StringComparer.Ordinal));
            Assert.All(risen, (e) => Assert.True(e.Pod?.Active));
            Assert.All(risen, (e) => Assert.Null(e.Unit));
            var zombie = s.Units.First((u) => u.Id == risen.First((e) => e.Corpse == "s1").Zombie.Id);
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

        // ------------------------------------------------------------ infection at the base

        private static CampaignResult Run(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            return CampaignHelpers.Run(c, cmd, slot, connected ?? Both);
        }

        /// <summary>`r toEqual { ok: false, error }`.</summary>
        private static void Refused(string error, CampaignResult r)
        {
            Assert.Equal((false, error, false), (r.Ok, r.Error, r.Launch));
        }

        // (The fever at the base, its treatment and its turning, gone with the bites that infected: Arturo, 7/11. A save's
        // fever is cured as it loads, CampaignTests' version 27.)

        [Fact(DisplayName = "infection at the base › medicines heal wounds: 1 ✚ for a day (a mission) fewer")]
        public void MedicinesHealWounds()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            Assert.Equal(Campaign.StartMedicines, c.Medicines);
            var s = c.Soldiers[0];
            Assert.False(Run(c, new MedicateCommand { Soldier = s.Id }, 0, new[] { 0 }).Ok);
            s.WoundedDays = 3;
            Assert.True(Run(c, new MedicateCommand { Soldier = s.Id }, 0, new[] { 0 }).Ok);
            Assert.Equal(3 - Campaign.MedicateDays, s.WoundedDays);
            Assert.Equal(Campaign.StartMedicines - 1, c.Medicines);
            c.Medicines = 0;
            Refused("No hay medicinas.", Run(c, new MedicateCommand { Soldier = s.Id }, 0, new[] { 0 }));
        }

        [Fact(DisplayName = "infection at the base › La Vega and Tarbes send medicines every few days while they are allies and stand; some operations pay them too")]
        public void LaVegaAndTarbesSendMedicines()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            // No Marea to get in the way (docs/FASE5.md): the activity stays put.
            c.NextEventDay = 999;
            c.NextMissionDay = 999;
            c.NextRumorDay = 999;
            c.NextFacilityDay = 999;
            c.NextSupplyDay = 999;
            c.NextDoomDay = 999;
            // Advances to day `until` at least; what the enclaves sent meanwhile, convoy by convoy.
            List<int> Wait(int until)
            {
                // The log is newest first (and short): what's new is what comes before the entry on top now.
                var mark = c.Log.Count > 0 ? c.Log[0] : null;
                var start = c.Medicines!.Value;
                // A day at a time: an event set for the next day stops the clock there (and is dropped).
                for (var i = 0; i < 60 && c.Day < until; i++)
                {
                    c.NextEventDay = c.Day + 1;
                    Campaign.AdvanceTime(c, new Rng(7 + i));
                    c.Event = null;
                    c.NextEventDay = 999;
                }
                var fresh = mark != null && c.Log.Contains(mark) ? c.Log.Take(c.Log.IndexOf(mark)).ToList() : c.Log;
                var convoys = fresh.Where((l) => Regex.IsMatch(l.Text, "medicinas: \\+")).Select((l) => int.Parse(Regex.Match(l.Text, "\\+(\\d+) ✚").Groups[1].Value)).ToList();
                Assert.Equal(convoys.Sum(), c.Medicines!.Value - start);
                return convoys;
            }
            // Not contacted yet: no allies, no medicines.
            foreach (var r in c.Regions) if (r.Id == RegionIds.Oce || r.Id == RegionIds.Afr) r.Contacted = false;
            Assert.Empty(Wait(1 + 2 * Campaign.EnclaveDays));
            // Contacted: each sends 1 ✚ every few days.
            foreach (var r in c.Regions) if (r.Id == RegionIds.Oce || r.Id == RegionIds.Afr) r.Contacted = true;
            var both = Wait(c.Day + 2 * Campaign.EnclaveDays);
            Assert.True(both.Count >= 1);
            Assert.All(both, (n) => Assert.Equal(2, n));
            // A Nido in La Vega: only Tarbes still sends.
            c.Regions.First((r) => r.Id == RegionIds.Oce).Facility = new RegionFacility { NextDoomDay = 999 };
            var one = Wait(c.Day + 2 * Campaign.EnclaveDays);
            Assert.True(one.Count >= 1);
            Assert.All(one, (n) => Assert.Equal(1, n));
            // Recoveries always pay a ✚; some others do too.
            var offers = Enumerable.Range(0, 12).SelectMany((i) => Campaign.CreateCampaign(new[] { 0 }, new Rng(i + 1)).Offers).Where((o) => (o.Story ?? 0) == 0).ToList();
            foreach (var o in offers) if (o.Kind == MissionKinds.Recovery) Assert.Equal(1, o.Reward.Medicines);
            Assert.Contains(offers, (o) => o.Kind != MissionKinds.Recovery && (o.Reward.Medicines ?? 0) != 0);
        }
    }
}
