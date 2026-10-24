using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Newtonsoft.Json.Linq;
using Xunit;
using static Bastion.Engine.Tests.CampaignHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/food.test.ts, test by test: the seven of «food», the three of «the Taller», the three of «found
    /// weapons» and «the campaign › some operations pay rations or parts…». `toEqual` on the campaign's objects is the
    /// same JSON, nulls apart (CampaignJsonTests.FirstDifference). Its helpers `run`, `quiet`, `days`, `share`,
    /// `workshop` and `found`, here as private ones.
    /// </summary>
    public class FoodTests
    {
        // Phase 4 of the zombies: food, the Taller's work and found weapons (docs/FASE4.md).

        private static CampaignResult Run(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            return CampaignHelpers.Run(c, cmd, slot, connected ?? new[] { 0 });
        }

        /// <summary>A campaign where nothing interrupts the days but what is being tested.</summary>
        private static CampaignState Quiet(IReadOnlyList<int>? players = null)
        {
            var c = Campaign.CreateCampaign(players ?? new[] { 0 }, new Rng(1));
            c.NextEventDay = 999;
            c.NextMissionDay = 999;
            c.NextRumorDay = 999;
            c.NextFacilityDay = 999;
            c.NextSupplyDay = 999;
            c.NextDoomDay = 999;
            c.NextMedicineDay = 999;
            c.NextFoodDay = 999;
            c.Offers = c.Offers.Where((o) => Story(o)).ToList();
            foreach (var o in c.Offers) o.ExpiresDay = null;
            return c;
        }

        /// <summary>`o.story`, as JavaScript reads it: a chapter (1 to 7), not null nor 0.</summary>
        private static bool Story(MissionOffer o) => (o.Story ?? 0) != 0;

        /// <summary>Days pass one at a time (the clock of days goes on inside, with the missions: docs/FASE12.md §11.6).</summary>
        private static void Days(CampaignState c, int n)
        {
            var until = c.Day + n;
            for (var i = 0; i < 4 * n && c.Day < until; i++)
            {
                var at = c.Day;
                // One day: everything else scheduled far away, so only the day stops the clock.
                c.NextEventDay = Math.Min(c.NextEventDay, at + 1);
                Campaign.AdvanceTime(c, new Rng(7));
                c.Event = null;
                c.NextEventDay = 999;
            }
        }

        /// <summary>Out of `n` searches of `decor`, the share that found `what`.</summary>
        private static double Share(string decor, Func<Loot, object?> what, int n = 4000)
        {
            var hits = 0;
            for (var seed = 1; seed <= n; seed++)
            {
                var loot = Looting.RollLoot(decor, 2, new Rng(seed));
                if (loot != null && what(loot) != null) hits++;
            }
            return (double)hits / n;
        }

        private static object? Food(Loot l) => l.Food;

        private static object? Weapon(Loot l) => l.Weapon;

        /// <summary>toBeCloseTo(expected, digits): within half a unit of the last digit.</summary>
        private static void CloseTo(double expected, double actual, int digits)
        {
            Assert.True(Math.Abs(expected - actual) < Math.Pow(10, -digits) / 2, $"expected {actual} to be close to {expected}");
        }

        private static JToken J(object? value) => value == null ? JValue.CreateNull() : JToken.Parse(CampaignJsonTests.Write(value));

        /// <summary>toEqual: the same JSON, nulls apart.</summary>
        private static void Same(object? expected, object? actual)
        {
            var d = CampaignJsonTests.FirstDifference(CampaignJsonTests.Clean(J(expected)), CampaignJsonTests.Clean(J(actual)));
            Assert.True(d == null, d);
        }

        /// <summary>`JSON.parse(JSON.stringify(c))`.</summary>
        private static CampaignState Copy(CampaignState c)
        {
            return J(c).ToObject<CampaignState>(CampaignJsonTests.Serializer)!;
        }

        // ------------------------------------------------------------ food

        [Fact(DisplayName = "food › the Bastion eats a ration a day for every soldier, and a mission is a day (Ana and Arturo, 21/10 #34)")]
        public void TheBastionEatsARationADayForEverySoldier()
        {
            var c = Quiet();
            Assert.Equal(1, Campaign.DaysPerMission);
            Assert.Equal(Campaign.StartFood, c.Food);
            var alive = c.Soldiers.Count((s) => s.Alive);
            Assert.Equal(alive, Campaign.DailyRations(c));
            Assert.Equal(alive, Campaign.RationsPerMission(c));
            Days(c, 1);
            Assert.Equal(Campaign.StartFood - alive, c.Food);
            c.Soldiers[0].Alive = false;
            Assert.Equal(alive - 1, Campaign.DailyRations(c));
        }

        [Fact(DisplayName = "food › with no rations, hunger: less will on missions and wounds heal every other day, until food comes")]
        public void WithNoRationsHunger()
        {
            var c = Quiet();
            c.Food = 1;
            var wounded = c.Soldiers[0];
            Days(c, 1);
            Assert.True(c.Hungry);
            Assert.Equal(0, c.Food);
            Assert.Contains(c.Log, (l) => l.Alert?.Title == "Hambre en el Bastión");
            var fit = c.Soldiers[1];
            var will = Campaign.SquadMember(c, fit).Will;
            c.Hungry = false;
            Assert.Equal(will + Campaign.HungryWill, Campaign.SquadMember(c, fit).Will);
            c.Hungry = true;
            c.Food = 0;
            // Two days of healing take four.
            wounded.WoundedDays = 2;
            Days(c, 2);
            Assert.Equal(1, wounded.WoundedDays);
            Days(c, 2);
            Assert.Equal(0, wounded.WoundedDays);
            // Huertos bring rations: with enough for a day, the hunger ends.
            Raise(c, PieceIds.Garden, 3);
            Raise(c, PieceIds.Garden, 3);
            Days(c, 1);
            Assert.False(c.Hungry);
            Assert.Equal(2 * 3 * Campaign.GardenRations - Campaign.DailyRations(c), c.Food);
        }

        [Fact(DisplayName = "food › a campaign of before (a mission, five days) comes over with what was to come in missions (21/10 #34)")]
        public void ACampaignOfBeforeComesOverWithWhatWasToComeInMissions()
        {
            var c = Quiet();
            var old = Copy(c);
            old.Version = 24;
            old.Day = 31;
            old.NextDoomDay = 43;
            old.NextSupplyDay = 61;
            old.Soldiers[0].WoundedDays = 8;
            old.Soldiers[1].Infection = new SoldierInfection { Since = 29 };
            var m = Campaign.MigrateCampaign(old);
            Assert.Equal(Campaign.CampaignVersion, m.Version);
            Assert.Equal(31, m.Day);
            Assert.Equal(31 + 3, m.NextDoomDay);
            Assert.Equal(31 + 6, m.NextSupplyDay);
            Assert.Equal(6, Campaign.ConvoyIn(m));
            Assert.Equal(2, m.Soldiers[0].WoundedDays);
            Same(new SoldierInfection { Since = 30 }, m.Soldiers[1].Infection);
            Assert.True(Campaign.ConvoyIncome(m) > 0);
        }

        [Fact(DisplayName = "food › the Huerto grows five rations a day (a mission) for each level")]
        public void TheHuertoGrowsFiveRationsADayForEachLevel()
        {
            var c = Quiet();
            Raise(c, PieceIds.Garden, 2);
            Assert.Equal(10, Campaign.GardenPerMission(c));
            c.Food = 100;
            Days(c, 4);
            Assert.Equal(100 + 4 * (10 - Campaign.DailyRations(c)), c.Food);
        }

        [Fact(DisplayName = "food › several Huertos, each its own rations (Arturo, 20/10)")]
        public void SeveralHuertosEachItsOwnRations()
        {
            var c = Quiet();
            Raise(c, PieceIds.Garden, 2);
            Raise(c, PieceIds.Garden, 1);
            Assert.Equal(2, c.Bastion.Pieces.Count((p) => p.Piece == PieceIds.Garden));
            c.Food = 100;
            Days(c, 4);
            Assert.Equal(100 + 4 * (Campaign.GardenRations * (2 + 1) - Campaign.DailyRations(c)), c.Food);
        }

        [Fact(DisplayName = "food › La Vega, an ally standing, sends 2 rations every 4 days")]
        public void LaVegaAnAllyStandingSendsRations()
        {
            var c = Quiet();
            c.Food = 100;
            var vega = c.Regions.First((r) => r.Id == RegionIds.Oce);
            c.NextFoodDay = c.Day + 1;
            vega.Contacted = false;
            var food = c.Food!.Value;
            Days(c, Campaign.LaVegaFoodDays);
            Assert.Equal(food - Campaign.LaVegaFoodDays * Campaign.DailyRations(c), c.Food);
            vega.Contacted = true;
            c.NextFoodDay = c.Day + 1;
            food = c.Food!.Value;
            Days(c, Campaign.LaVegaFoodDays);
            Assert.Equal(food - Campaign.LaVegaFoodDays * Campaign.DailyRations(c) + Campaign.LaVegaFood, c.Food);
            vega.Facility = new RegionFacility { NextDoomDay = 999 };
            food = c.Food!.Value;
            Days(c, Campaign.LaVegaFoodDays);
            Assert.Equal(food - Campaign.LaVegaFoodDays * Campaign.DailyRations(c), c.Food);
        }

        [Fact(DisplayName = "food › searching finds rations: pantries, shops and stores most, car boots little")]
        public void SearchingFindsRations()
        {
            foreach (var decor in new[] { DecorKinds.Wardrobe, DecorKinds.Shelf, DecorKinds.Crates }) Assert.True(Share(decor, Food) > Share(DecorKinds.CarRear, Food) * 2, decor);
            for (var seed = 1; seed < 400; seed++)
            {
                var food = Looting.RollLoot(DecorKinds.Shelf, 2, new Rng(seed))?.Food;
                if (food != null)
                {
                    Assert.True(food >= 2);
                    Assert.True(food <= 4);
                }
            }
        }

        // ------------------------------------------------------------ the Taller

        /// <summary>A campaign with a Taller at `level` and plenty to pay with.</summary>
        private static (CampaignState c, BasePiece plot) Workshop(int level, int bastion = 6)
        {
            var c = Quiet();
            var plot = Raise(c, PieceIds.Workshop, level);
            c.Level = bastion;
            c.Credits = 1000;
            c.Parts = 30;
            return (c, plot);
        }

        [Fact(DisplayName = "the Taller › making plate armour: Taller, Bastion level 4, 60 §, 4 parts and a day (a mission; 3 days of before) of a crew; then everyone wears it")]
        public void MakingPlateArmour()
        {
            var none = Quiet();
            Assert.Equal("Hace falta el Taller.", Campaign.CraftBlocker(none, CraftIds.Armor2));
            var (c, plot) = Workshop(1, 2);
            Assert.Matches("casa móvil con porche", Campaign.CraftBlocker(c, CraftIds.Armor2));
            c.Level = 4;
            c.Parts = 3;
            Assert.Equal("No hay piezas suficientes.", Campaign.CraftBlocker(c, CraftIds.Armor2));
            c.Parts = 10;
            Assert.True(Run(c, new CraftCommand { Project = CraftIds.Armor2 }).Ok);
            Assert.Equal(1000 - Progression.Crafts[CraftIds.Armor2].Cost, c.Credits);
            Assert.Equal(10 - Progression.Crafts[CraftIds.Armor2].Parts, c.Parts);
            Same(new List<Construction> { new Construction { Plot = plot.Id, Kind = ConstructionKinds.Craft, Facility = FacilityIds.Workshop, DaysLeft = 1, Total = 1, Project = CraftIds.Armor2 } }, c.Base.Works);
            Assert.Equal("Ya se está haciendo.", Campaign.CraftBlocker(c, CraftIds.Armor2));
            var anyone = c.Soldiers[0];
            Assert.Equal(1, Progression.ArmorTier(c, anyone));
            Days(c, 1);
            Assert.Equal(new[] { CraftIds.Armor2 }, c.Crafted!);
            Assert.Equal(2, Progression.ArmorTier(c, anyone));
        }

        [Fact(DisplayName = "the Taller › improvements for the weapons: culatas and silenciadores from «Culatas y silenciadores», miras and cargadores from «Miras y cargadores ampliados», again and again")]
        public void ImprovementsForTheWeapons()
        {
            var (c, _) = Workshop(1, 2);
            Assert.Matches("la casa móvil\\.", Campaign.CraftBlocker(c, CraftIds.MakeStock));
            c.Level = 3;
            Assert.Null(Campaign.CraftBlocker(c, CraftIds.MakeStock));
            Assert.Matches("albergue ampliado", Campaign.CraftBlocker(c, CraftIds.MakeScope));
            Assert.True(Run(c, new CraftCommand { Project = CraftIds.MakeStock }).Ok);
            Days(c, Progression.Crafts[CraftIds.MakeStock].Days);
            Same(new Dictionary<string, int> { [WeaponModIds.Stock] = 1 }, c.WeaponMods);
            Assert.Null(Campaign.CraftBlocker(c, CraftIds.MakeStock));
        }

        [Fact(DisplayName = "the Taller › armour 3 needs the Taller at level 2, its level of the Bastion and armour 2 made first; two players decide together")]
        public void Armour3NeedsTheTallerAtLevel2()
        {
            var (c, plot) = Workshop(1, 7);
            Assert.Equal("Hace falta el Taller a nivel 2.", Campaign.CraftBlocker(c, CraftIds.Armor3));
            plot.Level = 2;
            Assert.Matches("Antes hay que hacer", Campaign.CraftBlocker(c, CraftIds.Armor3));
            c.Crafted = new List<string> { CraftIds.Armor2 };
            Assert.Null(Campaign.CraftBlocker(c, CraftIds.Armor3));
            var two = Workshop(2, 7);
            two.c.Players = new List<int> { 0, 1 };
            two.c.Crafted = new List<string> { CraftIds.Armor2 };
            Assert.True(Run(two.c, new CraftCommand { Project = CraftIds.Armor3 }, 0, Both).Ok);
            Same(new CraftAction { Project = CraftIds.Armor3 }, two.c.Proposal?.Action);
            Assert.Empty(two.c.Base.Works);
            Assert.True(Run(two.c, new VoteCommand { Accept = true }, 1, Both).Ok);
            var work = two.c.Base.Works[0];
            Assert.Equal((ConstructionKinds.Craft, CraftIds.Armor3, 1), (work.Kind, work.Project, work.Total));
        }

        // ------------------------------------------------------------ found weapons

        /// <summary>A campaign whose last mission brought home `kind`, carried by its first soldier.</summary>
        private static (CampaignState c, MissionReport report) Found(string kind = FoundWeaponIds.HuntingRifle)
        {
            var c = Quiet(Both);
            var offer = Campaign.CreateCampaign(Both, new Rng(2)).Offers.First((o) => !Story(o));
            c.Offers.Add(offer);
            if (!Run(c, new ChooseMissionCommand { Id = offer.Id }, 0, new[] { 0 }).Ok) throw new InvalidOperationException("mission");
            var mission = Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = new List<int> { 0, 1 }, Rng = new Rng(3), Kind = offer.Kind, Squad = Campaign.BuildSquad(c), Pods = offer.Pods, Difficulty = offer.Difficulty });
            var squad = mission.Units.Where((u) => u.Team == Teams.Xcom).ToList();
            squad[0].Alive = false;
            squad[0].Evacuated = true;
            squad[0].Backpack = new List<Loot> { new Loot { Weapon = kind } };
            mission.Outcome = Outcomes.Victory;
            mission.OutcomeReason = EndReasons.Objective;
            var report = Campaign.ApplyMissionResult(c, mission, new Rng(4));
            return (c, report);
        }

        [Fact(DisplayName = "found weapons › rare: boots, stores and home wardrobes; never an office")]
        public void RareBootsStoresAndHomeWardrobes()
        {
            CloseTo(0.04, Share(DecorKinds.CarRear, Weapon), 1);
            CloseTo(0.03, Share(DecorKinds.Crates, Weapon), 1);
            Assert.True(Share(DecorKinds.Wardrobe, Weapon) > 0);
            Assert.Equal(0, Share(DecorKinds.Desk, Weapon));
        }

        [Fact(DisplayName = "found weapons › brought home, it goes to the Armería; any of your soldiers takes it")]
        public void BroughtHomeItGoesToTheArmeria()
        {
            var (c, report) = Found();
            Assert.Equal(new[] { FoundWeaponIds.HuntingRifle }, report.Loot?.Weapons);
            Assert.Matches("fusil de cerrojo", report.Loot?.Line);
            var w = c.FoundWeapons![0];
            Assert.Equal((FoundWeaponIds.HuntingRifle, (string?)null), (w.Kind, w.Holder));
            var rookie = c.Soldiers.First((s) => s.Owner == 0 && s.Alive);
            // Not for a partner's soldier while the partner is here; any of yours, whatever its class (docs/FASE8.md §A).
            var theirs = c.Soldiers.First((s) => s.Owner == 1);
            Assert.False(Run(c, new AssignWeaponCommand { Weapon = w.Id, Soldier = theirs.Id }, 0, Both).Ok);
            var assault = c.Soldiers.First((s) => s.Owner == 0 && s.Alive && s != rookie);
            Assert.True(Run(c, new AssignWeaponCommand { Weapon = w.Id, Soldier = assault.Id }, 0).Ok);
            Assert.Equal(assault.Id, w.Holder);
            Assert.True(Run(c, new AssignWeaponCommand { Weapon = w.Id, Soldier = rookie.Id }, 0).Ok);
            var member = Campaign.SquadMember(c, rookie);
            Assert.Equal(((int?)1, "Fusil de cerrojo"), (member.WeaponTier, member.WeaponName));
            // The partner can't take it from them.
            Assert.False(Run(c, new AssignWeaponCommand { Weapon = w.Id, Soldier = null }, 1).Ok);
            Assert.True(Run(c, new AssignWeaponCommand { Weapon = w.Id, Soldier = null }, 0).Ok);
            Assert.Null(w.Holder);
        }

        [Fact(DisplayName = "found weapons › it is lost if its soldier falls")]
        public void ItIsLostIfItsSoldierFalls()
        {
            var (c, _) = Found();
            var w = c.FoundWeapons![0];
            var rookie = c.Soldiers.First((s) => s.Owner == 0 && s.Alive);
            Run(c, new AssignWeaponCommand { Weapon = w.Id, Soldier = rookie.Id }, 0);
            var offer = Campaign.CreateCampaign(Both, new Rng(5)).Offers.First((o) => !Story(o));
            c.Offers.Add(offer);
            Run(c, new ChooseMissionCommand { Id = offer.Id }, 0, new[] { 0 });
            c.Squad = new List<string> { rookie.Id };
            var mission = Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = new List<int> { 0, 1 }, Rng = new Rng(3), Kind = offer.Kind, Squad = Campaign.BuildSquad(c), Pods = offer.Pods, Difficulty = offer.Difficulty });
            var unit = mission.Units.First((u) => u.CampaignId == rookie.Id);
            Assert.Equal("Fusil de cerrojo", unit.WeaponName);
            unit.Alive = false;
            unit.Hp = 0;
            mission.Outcome = Outcomes.Defeat;
            mission.OutcomeReason = EndReasons.Wiped;
            Campaign.ApplyMissionResult(c, mission, new Rng(4));
            Assert.False(rookie.Alive);
            Assert.NotNull(c.FoundWeapons);
            Assert.Empty(c.FoundWeapons!);
        }

        // ------------------------------------------------------------ the campaign

        [Fact(DisplayName = "the campaign › some operations pay rations or parts; Acorazados brought down give a part each")]
        public void SomeOperationsPayRationsOrParts()
        {
            var offers = Enumerable.Range(0, 20).SelectMany((i) => Campaign.CreateCampaign(new[] { 0 }, new Rng(i + 1)).Offers).Where((o) => !Story(o)).ToList();
            Assert.Contains(offers, (o) => (o.Reward.Food ?? 0) != 0);
            Assert.Contains(offers, (o) => (o.Reward.Parts ?? 0) != 0);
            var c = Quiet();
            // `{ ...offer, id: 'mfood' }`: the one found is changed where it is instead, as nothing reads it again.
            var offer = offers.First((o) => o.Kind != MissionKinds.Scavenge);
            offer.Id = "mfood";
            offer.Reward = new MissionReward { Credits = 10, Food = 4, Parts = 2 };
            c.Offers.Add(offer);
            Run(c, new ChooseMissionCommand { Id = offer.Id });
            var pods = new List<List<string>> { new List<string> { TemplateIds.Mec, TemplateIds.Mec }, new List<string> { TemplateIds.Zombie } };
            var mission = Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = new List<int> { 0 }, Rng = new Rng(3), Kind = offer.Kind, Squad = Campaign.BuildSquad(c), Pods = pods, Difficulty = offer.Difficulty });
            foreach (var u in mission.Units)
            {
                if (u.Template == TemplateIds.Mec)
                {
                    u.Alive = false;
                    u.Hp = 0;
                }
            }
            mission.Outcome = Outcomes.Victory;
            mission.OutcomeReason = EndReasons.Objective;
            var food = c.Food!.Value;
            var report = Campaign.ApplyMissionResult(c, mission, new Rng(4));
            Assert.Equal(2, report.Scrap);
            Assert.Equal(2 + 2, c.Parts);
            // The ration reward, less what the days out ate (docs/FASE12.md §11.6).
            Assert.Equal(c.Day - Campaign.DaysPerMission, report.Day - Campaign.DaysPerMission);
            Assert.Equal(food + 4 - Campaign.DaysPerMission * Campaign.DailyRations(c), c.Food);
        }
    }
}
