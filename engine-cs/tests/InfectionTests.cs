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

        // ------------------------------------------------------------ infection at the base

        private static CampaignResult Run(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            return CampaignHelpers.Run(c, cmd, slot, connected ?? Both);
        }

        /// <summary>A campaign whose last mission brought `bitten` soldiers home infected.</summary>
        private static (CampaignState c, MissionReport report, List<CampaignSoldier> sick, CampaignSoldier fallen) Bitten(int count = 1, IReadOnlyList<int>? players = null)
        {
            players ??= Both;
            var c = Campaign.CreateCampaign(players, new Rng(2));
            // Nothing else interrupts the days.
            c.NextEventDay = 999;
            c.NextMissionDay = 999;
            c.NextRumorDay = 999;
            c.NextFacilityDay = 999;
            c.NextSupplyDay = 999;
            c.NextDoomDay = 999;
            // Nor the siege (it comes straight after the first mission: Ana, 22/10).
            c.Siege.In = 99;
            var offer = c.Offers[0];
            if (!Run(c, new ChooseMissionCommand { Id = offer.Id }, 0, new[] { players[0] }).Ok) throw new InvalidOperationException("mission");
            var mission = Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = players.ToList(), Rng = new Rng(3), Kind = offer.Kind, Squad = Campaign.BuildSquad(c), Pods = offer.Pods, Difficulty = offer.Difficulty });
            var squad = mission.Units.Where((u) => u.Team == Teams.Xcom).ToList();
            foreach (var u in squad.Take(count)) u.Infected = true;
            // A bitten soldier who fell stays fallen (in the field, the corpse rose).
            squad[count].Infected = true;
            squad[count].Alive = false;
            squad[count].Hp = 0;
            mission.Outcome = Outcomes.Victory;
            mission.OutcomeReason = EndReasons.Objective;
            var report = Campaign.ApplyMissionResult(c, mission, new Rng(4));
            var sick = squad.Take(count).Select((u) => c.Soldiers.First((s) => s.Id == u.CampaignId)).ToList();
            return (c, report, sick, c.Soldiers.First((s) => s.Id == squad[count].CampaignId));
        }

        /// <summary>`r toEqual { ok: false, error }`.</summary>
        private static void Refused(string error, CampaignResult r)
        {
            Assert.Equal((false, error, false), (r.Ok, r.Error, r.Launch));
        }

        [Fact(DisplayName = "infection at the base › a soldier back with a bite has a fever: a day (a mission), two with an Enfermería, and no deploying")]
        public void ASoldierBackWithABiteHasAFever()
        {
            var (c, report, sick, fallen) = Bitten();
            var s = sick[0];
            Assert.Equal(new[] { s.Id }, report.Infected!);
            Assert.NotNull(s.Infection);
            Assert.Equal(c.Day, s.Infection!.Since);
            Assert.False(fallen.Alive);
            Assert.Null(fallen.Infection);
            Assert.Equal(c.Day + Campaign.TurnDays, Campaign.TurnDay(c, s));
            Raise(c, PieceIds.Infirmary);
            Assert.Equal(c.Day + Campaign.TurnDays + Campaign.InfirmaryTurnDays, Campaign.TurnDay(c, s));
            Assert.False(Campaign.SoldierReady(s));
            Assert.DoesNotContain(s.Id, Campaign.DefaultSquad(c, Both));
        }

        [Fact(DisplayName = "infection at the base › untreated, it turns on its day: one more of the Caídos")]
        public void UntreatedItTurnsOnItsDay()
        {
            var (c, _, sick, _) = Bitten();
            var s = sick[0];
            var day = Campaign.TurnDay(c, s)!.Value;
            for (var i = 0; i < 10 && s.Alive; i++) Campaign.AdvanceTime(c, new Rng(7 + i));
            Assert.False(s.Alive);
            Assert.Equal(day, c.Day);
            Assert.Equal(((int?)day, FellCauses.Turned), (s.FellDay, s.FellCause));
            Assert.Null(s.Infection);
            Assert.Contains(c.Log, (l) => l.Alert != null && Regex.IsMatch(l.Alert.Title, "se ha convertido"));
        }

        [Fact(DisplayName = "infection at the base › treating takes both keys (alone, one) and 2 ✚, 1 with the Enfermería at level 2")]
        public void TreatingTakesBothKeys()
        {
            var (c, _, sick, _) = Bitten(2);
            var a = sick[0];
            var b = sick[1];
            c.Medicines = 5;
            Assert.True(Run(c, new TreatCommand { Soldier = a.Id, Ready = true }, 0).Ok);
            Assert.Equal(a.Id, c.Treatment?.Soldier);
            Assert.Equal(new[] { true, false }, c.Treatment!.Ready);
            Assert.NotNull(a.Infection);
            // Another proposal waits for this one.
            Assert.False(Run(c, new TreatCommand { Soldier = b.Id, Ready = true }, 1).Ok);
            Assert.True(Run(c, new TreatCommand { Soldier = a.Id, Ready = true }, 1).Ok);
            Assert.Null(a.Infection);
            Assert.Equal(3, c.Medicines);
            Assert.Null(c.Treatment);
            // The partner can turn it down.
            Run(c, new TreatCommand { Soldier = b.Id, Ready = true }, 0);
            Run(c, new TreatCommand { Soldier = b.Id, Ready = false }, 1);
            Assert.Null(c.Treatment);
            Assert.NotNull(b.Infection);
            // With the Enfermería at level 2, one ✚; and a player alone needs no second key.
            Raise(c, PieceIds.Infirmary, 2);
            Assert.Equal(1, Campaign.TreatCost(c));
            Assert.True(Run(c, new TreatCommand { Soldier = b.Id, Ready = true }, 0, new[] { 0 }).Ok);
            Assert.Null(b.Infection);
            Assert.Equal(2, c.Medicines);
            Assert.Equal(b.WoundedDays == 0, Campaign.SoldierReady(b));
        }

        [Fact(DisplayName = "infection at the base › no medicines, no treatment")]
        public void NoMedicinesNoTreatment()
        {
            var (c, _, sick, _) = Bitten(1, new[] { 0 });
            c.Medicines = 1;
            var r = Run(c, new TreatCommand { Soldier = sick[0].Id, Ready = true }, 0, new[] { 0 });
            Refused("No hay medicinas suficientes.", r);
            Assert.NotNull(sick[0].Infection);
        }

        [Fact(DisplayName = "infection at the base › a bitten soldier cannot join the squad")]
        public void ABittenSoldierCannotJoinTheSquad()
        {
            var (c, _, sick, _) = Bitten();
            var s = sick[0];
            var offer = c.Offers.FirstOrDefault((o) => (o.Story ?? 0) == 0) ?? c.Offers[0];
            Run(c, new ChooseMissionCommand { Id = offer.Id }, 0, new[] { 0 });
            c.Squad = new List<string>();
            var r = Run(c, new SquadCommand { Soldier = s.Id, On = true }, s.Owner, new[] { s.Owner });
            Assert.False(r.Ok);
            // «tiene fiebre: hay que tratarle antes» is «tiene fiebre: necesita el tratamiento antes de salir» (text-fixes.ts): «fiebre» either way.
            Assert.Matches("fiebre", r.Ok ? "" : r.Error);
        }

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
