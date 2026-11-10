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
    /// What is left of packages/engine/test/infection.test.ts (phase 3 of the zombies, docs/FASE3.md) with no bite that
    /// infects (Arturo, 7/11; its code out in the 0.12.18): the fallen who rise, and the medicines at the base. `client
    /// toEqual s` is Step2Json.StateDiff. Its helpers `field`, `pod` and the base's `run`, here as private ones (`pod` is
    /// Step2Helpers.Pod).
    /// </summary>
    public class InfectionTests
    {
        private static GameState Field()
        {
            return StateFrom(Enumerable.Range(0, 12).Select((_) => new string('.', 12)).ToArray());
        }

        // ------------------------------------------------------------ infection in combat

        // (A bite only hurts: the tests that counted who it infected went with InfectedEvent, 0.12.18.)

        [Fact(DisplayName = "infection in combat › every soldier who falls rises as the zombies’ turn begins and acts from the next (Arturo, 7/11)")]
        public void EverySoldierWhoFallsRises()
        {
            var s = Field();
            var look = new Appearance { Primary = "#123456", Secondary = "#654321", Visor = "#9fe3ff", Head = HeadIds.Helmet, Build = BuildIds.Normal, Sex = Sexes.F };
            var fallen = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (u) => u.Appearance = look);
            var clean = AddUnit(s, "s2", TemplateIds.Rookie, 7, 5);
            var out_ = AddUnit(s, "s3", TemplateIds.Rookie, 0, 0, extra: (u) =>
            {
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
            // Both who fell (Arturo, 7/11: «cualquier soldado que muera»); not one evacuated.
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
        // fever is not read any more: ContagionGoneTests.)

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
