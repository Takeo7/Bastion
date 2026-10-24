using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.CampaignHelpers;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/arms.test.ts, test by test: the five of «the weapons from zero», the one of «the Armería shows
    /// what each weapon does» and the six of «a weapon found in the mission». Its helpers `run`, `ctx` and `field`, here
    /// as private ones (`of` is Step2Helpers.Of).
    /// </summary>
    public class ArmsTests
    {
        // The weapons from zero (docs/FASE8.md §A): the class no longer brings a weapon.

        private static CampaignResult Run(CampaignState c, CampaignCommand cmd, int slot = 0)
        {
            return CampaignHelpers.Run(c, cmd, slot, new[] { 0 });
        }

        private static CommandContext Ctx()
        {
            return new CommandContext { Slot = 0, Connected = new[] { 0 }, Rng = new Rng(1) };
        }

        private static Arms Armed(string? primary, string? primaryId = null)
        {
            return new Arms { Primary = primary, PrimaryId = primaryId, Melee = null };
        }

        /// <summary>toEqual on anything: the same JSON, null and missing alike.</summary>
        private static void Same(object? expected, object? actual)
        {
            var diff = Step2Json.Diff(Step2Json.Element(expected), Step2Json.Element(actual), "");
            if (diff != null) throw new Xunit.Sdk.XunitException(diff);
        }

        private static void SameState(GameState expected, GameState actual)
        {
            var diff = Step2Json.StateDiff(expected, actual);
            if (diff != null) throw new Xunit.Sdk.XunitException(diff);
        }

        private static void Refused(string error, CommandResult r)
        {
            Assert.Equal((false, error), (r.Ok, r.Error));
        }

        /// <summary>A field with a zombie asleep far away, so the mission goes on, and an armed soldier.</summary>
        private static (GameState s, Unit u) Field(Arms arms, Action<Unit>? extra = null)
        {
            var s = StateFrom(Enumerable.Range(0, 20).Select((_) => new string('.', 20)).ToArray());
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 19, 19);
            var pod = new Pod { Id = "p1", Active = false, UnitIds = new List<string> { z.Id }, Patrol = new List<Pos>(), PatrolIndex = 0 };
            z.PodId = pod.Id;
            s.Pods.Add(pod);
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 5, 5, extra: (x) =>
            {
                x.Arms = arms;
                x.Mags = 0;
                extra?.Invoke(x);
            });
            u.Ammo = Content.ClipOf(u, AbilityIds.Shoot);
            if (!string.IsNullOrEmpty(arms.Primary)) u.PistolAmmo = Content.PistolClip;
            return (s, u);
        }

        private static CommandResult Equip(GameState s, int index)
        {
            return Rules.ExecuteCommand(s, new ItemCommand { Unit = "s1", Action = ItemActions.Equip, Index = index }, Ctx());
        }

        // ------------------------------------------------------------ the weapons from zero

        [Fact(DisplayName = "the weapons from zero › a new campaign: knife and pistol, nothing else, and no spare magazines")]
        public void ANewCampaignKnifeAndPistolNothingElse()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            c.Squad = c.Soldiers.Take(4).Select((x) => x.Id).ToList();
            Assert.Empty(c.FoundWeapons ?? new List<FoundWeapon>());
            var m = Campaign.BuildSquad(c)[0];
            Assert.NotNull(m.Arms);
            Assert.Equal(((string?)null, (string?)null, (int?)0, "Pistola"), (m.Arms!.Primary, m.Arms.Melee, m.Mags, m.WeaponName));
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 3), Slots = new List<int> { 0 }, Rng = new Rng(1), Squad = Campaign.BuildSquad(c) });
            var u = s.Units.First((x) => x.Team == Teams.Xcom);
            Assert.Equal("pistol", Content.AttackWeapon(u, AbilityIds.Shoot).Id);
            Assert.Equal("knife", Content.AttackWeapon(u, AbilityIds.Slash).Id);
            Assert.Equal((Content.PistolClip, 0), (u.Ammo, u.Mags));
            Assert.Null(u.PistolAmmo);
            Assert.Contains(AbilityIds.Shoot, Abilities.AbilitiesOf(u));
            Assert.Contains(AbilityIds.Slash, Abilities.AbilitiesOf(u));
            Assert.DoesNotContain(AbilityIds.Pistol, Abilities.AbilitiesOf(u));
        }

        [Fact(DisplayName = "the weapons from zero › the pistol counts its rounds; beside a primary it has its own")]
        public void ThePistolCountsItsRounds()
        {
            var bare = Field(Armed(null));
            AddUnit(bare.s, "a2", TemplateIds.Zombie, 8, 5);
            Assert.True(Rules.ExecuteCommand(bare.s, Ability("s1", AbilityIds.Shoot, target: "a2"), Ctx()).Ok);
            Assert.Equal(Content.PistolClip - 1, bare.u.Ammo);
            bare.u.Ammo = 0;
            bare.u.Ap = 2;
            Assert.Equal("Sin munición y sin cargadores.", Abilities.AbilityBlocker(bare.s, bare.u, AbilityIds.Shoot));

            var (s, u) = Field(Armed(FoundWeaponIds.AssaultRifle, "w1"), (x) => x.Mags = 1);
            AddUnit(s, "a2", TemplateIds.Zombie, 8, 5);
            Assert.Contains(AbilityIds.Pistol, Abilities.AbilitiesOf(u));
            Assert.True(Rules.ExecuteCommand(s, Ability("s1", AbilityIds.Pistol, target: "a2"), Ctx()).Ok);
            Assert.Equal(Content.PistolClip - 1, u.PistolAmmo);
            Assert.Equal(Content.ClipOf(u, AbilityIds.Shoot), u.Ammo);
            // The rifle is full: reloading fills the pistol.
            u.Ap = 2;
            Assert.True(Rules.ExecuteCommand(s, Ability("s1", AbilityIds.Reload), Ctx()).Ok);
            Assert.Equal(((int?)Content.PistolClip, (int?)0), (u.PistolAmmo, u.Mags));
            Assert.Equal("Los cargadores están llenos.", Abilities.AbilityBlocker(s, u, AbilityIds.Reload));
        }

        [Fact(DisplayName = "the weapons from zero › the far shot is the Francotirador with a fusil de precisión")]
        public void TheFarShotIsTheFrancotiradorWithAFusilDePrecision()
        {
            var (s, _) = Field(Armed(null));
            var sharp = AddUnit(s, "s2", TemplateIds.Sharpshooter, 2, 2, extra: (u) => u.Arms = Armed(FoundWeaponIds.AssaultRifle));
            Assert.False(Content.HasSquadsight(sharp));
            sharp.Arms = Armed(FoundWeaponIds.PrecisionRifle);
            Assert.True(Content.HasSquadsight(sharp));
            Assert.False(Content.HasSquadsight(AddUnit(s, "s3", TemplateIds.Rookie, 3, 3, extra: (u) => u.Arms = Armed(FoundWeaponIds.ScopedRifle))));
            // Without arms of its own (a skirmish), its class's.
            Assert.True(Content.HasSquadsight(AddUnit(s, "s4", TemplateIds.Sharpshooter, 4, 4)));
        }

        [Fact(DisplayName = "the weapons from zero › anyone takes any weapon: one in the hand and one at the belt; Mano de machete wants the machete")]
        public void AnyoneTakesAnyWeapon()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            var s = c.Soldiers[0];
            s.Board = new List<string> { "bladeMaster" };
            c.FoundWeapons = new List<FoundWeapon>
            {
                new FoundWeapon { Id = "w1", Kind = FoundWeaponIds.PumpShotgun, Holder = null },
                new FoundWeapon { Id = "w2", Kind = FoundWeaponIds.Machete, Holder = null },
                new FoundWeapon { Id = "w3", Kind = FoundWeaponIds.AssaultRifle, Holder = null },
            };
            var knife = Campaign.SquadMember(c, s).Mods;
            foreach (var w in new[] { "w1", "w2" }) Assert.True(Run(c, new AssignWeaponCommand { Weapon = w, Soldier = s.Id }).Ok);
            var member = Campaign.SquadMember(c, s);
            Assert.NotNull(member.Arms);
            Assert.Equal((FoundWeaponIds.PumpShotgun, "w1", FoundWeaponIds.Machete, "w2"), (member.Arms!.Primary, member.Arms.PrimaryId, member.Arms.Melee, member.Arms.MeleeId));
            Assert.Equal("Escopeta", member.WeaponName);
            Assert.Equal(2, Campaign.SquadMember(c, s).Mods.MeleeDamage - knife.MeleeDamage);
            Assert.Equal(15, Campaign.SquadMember(c, s).Mods.MeleeAim - knife.MeleeAim);
            // A second primary takes the first one's place; the machete stays.
            Assert.True(Run(c, new AssignWeaponCommand { Weapon = "w3", Soldier = s.Id }).Ok);
            Assert.Equal(new[] { null, s.Id, s.Id }, c.FoundWeapons.Select((w) => w.Holder));
        }

        [Fact(DisplayName = "the weapons from zero › the Taller makes weapons again and again, and munition three times; the Bastion's level brings some")]
        public void TheTallerMakesWeaponsAgainAndAgain()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            Raise(c, PieceIds.Workshop);
            c.Credits = 999;
            c.Parts = 99;
            Assert.Null(Campaign.CraftBlocker(c, CraftIds.MakeRifle));
            Assert.Equal("Se desbloquea con la casa móvil.", Campaign.CraftBlocker(c, CraftIds.MakeSniper));
            Assert.Equal("Se desbloquea con la casa móvil.", Campaign.CraftBlocker(c, CraftIds.Ammo2));
            c.Crafted = new List<string> { CraftIds.Ammo1 };
            Assert.Equal(1, Progression.SpareMags(c));
            Assert.Equal(FoundWeaponIds.AssaultRifle, Progression.Crafts[CraftIds.MakeRifle].Makes);
            // Made once, it can be made again.
            c.Crafted.Add(CraftIds.MakeRifle);
            Assert.Null(Campaign.CraftBlocker(c, CraftIds.MakeRifle));
            Assert.Equal("Ya está hecho.", Campaign.CraftBlocker(c, CraftIds.Ammo1));
            LevelTo(c, 3, new Rng(1));
            Assert.Equal(new[] { FoundWeaponIds.AssaultRifle, FoundWeaponIds.Machete, FoundWeaponIds.PrecisionRifle, FoundWeaponIds.PumpShotgun }, (c.FoundWeapons ?? new List<FoundWeapon>()).Select((w) => w.Kind).OrderBy((k) => k, StringComparer.Ordinal));
        }

        // ------------------------------------------------------------ the Armería shows what each weapon does (Arturo, 17/10)

        [Fact(DisplayName = "the Armería shows what each weapon does (Arturo, 17/10) › damage, crit, aim, magazine, actions, noise and how it shoots by distance")]
        public void DamageCritAimMagazineActionsNoiseAndHowItShootsByDistance()
        {
            var shotgun = Aim.WeaponProfileOf(FoundWeaponIds.PumpShotgun);
            Assert.Equal(("Escopeta", false, RangeTableIds.Short, 1), (shotgun.Name, shotgun.Melee, shotgun.Reach, shotgun.Actions));
            Assert.True(shotgun.Damage.Max >= shotgun.Damage.Min);
            Assert.True(shotgun.CritDamage.Min > shotgun.Damage.Min);
            Assert.Equal(new[] { 2, 5, 8, 12, 16 }, shotgun.Range.Select((r) => r.Tiles));
            // A shotgun is good close and bad far; a precision rifle, the other way round.
            Assert.True(shotgun.Range[0].Aim > shotgun.Range[4].Aim);
            var rifle = Aim.WeaponProfileOf(FoundWeaponIds.PrecisionRifle);
            Assert.True(rifle.Range[0].Aim < rifle.Range[4].Aim);
            Assert.Equal(2, rifle.Actions);
            // What is mounted and how worn it is count.
            var plain = Aim.WeaponProfileOf(FoundWeaponIds.AssaultRifle, 100, new string[0]);
            var fitted = Aim.WeaponProfileOf(FoundWeaponIds.AssaultRifle, 100, new[] { WeaponModIds.Scope, WeaponModIds.ExtMag, WeaponModIds.Silencer });
            Assert.Equal((plain.Aim + 10, plain.Clip + 2, (int)Math.Ceiling(plain.Noise / 2.0)), (fitted.Aim, fitted.Clip, fitted.Noise));
            Assert.Equal(plain.Aim - 10, Aim.WeaponProfileOf(FoundWeaponIds.AssaultRifle, 10, new string[0]).Aim);
            // A good find comes with its mod: the scoped rifle aims better than the plain one.
            Assert.Equal(Aim.WeaponProfileOf(FoundWeaponIds.PrecisionRifle).Aim + 10, Aim.WeaponProfileOf(FoundWeaponIds.ScopedRifle).Aim);
            var machete = Aim.WeaponProfileOf(FoundWeaponIds.Machete);
            Assert.Equal((true, RangeTableIds.None, 0), (machete.Melee, machete.Reach, machete.Clip));
            Assert.Empty(machete.Range);
        }

        // ------------------------------------------------------------ a weapon found in the mission (docs/FASE8.md §C)

        [Fact(DisplayName = "a weapon found in the mission (docs/FASE8.md §C) › is equipped for an action; the one in hand goes into the backpack, and a client replays it")]
        public void IsEquippedForAnAction()
        {
            var (s, u) = Field(Armed(FoundWeaponIds.AssaultRifle, "w1"), (x) => x.Backpack = new List<Loot> { new Loot { Weapon = FoundWeaponIds.SawnOff } });
            u.Ammo = 1;
            var client = Events.CloneState(s);
            var r = Equip(s, 0);
            Assert.True(r.Ok, r.Error);
            Assert.Single(Of<WeaponEquippedEvent>(r.Events!));
            Same(new Arms { Primary = FoundWeaponIds.SawnOff, Melee = null }, u.Arms);
            Assert.Equal(Content.ClipOf(u, AbilityIds.Shoot), u.Ammo);
            Same(new List<Loot> { new Loot { Weapon = FoundWeaponIds.AssaultRifle, WeaponId = "w1" } }, u.Backpack);
            Assert.Equal(1, u.Ap);
            foreach (var e in r.Events!) Events.ApplyEvent(client, e);
            SameState(s, client);
            // Only weapons, and only a campaign soldier.
            u.Backpack!.Add(new Loot { Food = 2 });
            Refused("Solo se equipan las armas, los chalecos y lo del cinturón.", Equip(s, 1));
        }

        [Fact(DisplayName = "a weapon found in the mission (docs/FASE8.md §C) › a grenade found goes on the belt for an action, and is thrown at once (playtest 18/10 #5)")]
        public void AGrenadeFoundGoesOnTheBeltForAnAction()
        {
            var (s, u) = Field(Armed(FoundWeaponIds.AssaultRifle), (x) =>
            {
                x.Backpack = new List<Loot> { new Loot { Item = ItemIds.FragGrenade }, new Loot { Item = ItemIds.SmokeGrenade } };
                x.Charges = new Dictionary<string, int>();
                x.Gear = new List<string>();
            });
            Assert.DoesNotContain(AbilityIds.Grenade, Abilities.AbilitiesOf(u));
            var client = Events.CloneState(s);
            var r = Equip(s, 0);
            Assert.True(r.Ok, r.Error);
            Assert.Single(Of<BeltEquippedEvent>(r.Events!));
            Assert.Equal(1, u.Ap);
            Assert.Equal(2, u.Charges.TryGetValue(ChargeIds.Grenade, out var grenades) ? grenades : (int?)null);
            Assert.Equal(new[] { ItemIds.FragGrenade }, u.Belted!);
            Same(new List<Loot> { new Loot { Item = ItemIds.SmokeGrenade } }, u.Backpack);
            Assert.Contains(AbilityIds.Grenade, Abilities.AbilitiesOf(u));
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.Grenade));
            foreach (var e in r.Events!) Events.ApplyEvent(client, e);
            SameState(s, client);
            // The smoke brings its own ability.
            u.Ap = 2;
            Assert.True(Equip(s, 0).Ok);
            Assert.Contains(AbilityIds.Smoke, Abilities.AbilitiesOf(u));
            Assert.Equal(1, u.Charges[ChargeIds.Smoke]);
        }

        [Fact(DisplayName = "a weapon found in the mission (docs/FASE8.md §C) › ear plugs go on once; ammunition is mounted at home")]
        public void EarPlugsGoOnOnce()
        {
            var (s, u) = Field(Armed(FoundWeaponIds.AssaultRifle), (x) => x.Backpack = new List<Loot>
            {
                new Loot { Item = ItemIds.MindShield },
                new Loot { Item = ItemIds.MindShield },
                new Loot { Item = ItemIds.ApRounds },
            });
            var will = u.Mods.Will;
            Assert.True(Equip(s, 0).Ok);
            Assert.Equal(will + 100, u.Mods.Will);
            Refused("Ya lleva eso puesto.", Equip(s, 0));
            Refused("La munición se monta en el arma, en la Armería.", Equip(s, 1));
        }

        [Fact(DisplayName = "a weapon found in the mission (docs/FASE8.md §C) › a belt thing found comes home if its uses are still there, the soldier's own counted as spent first")]
        public void ABeltThingFoundComesHomeIfItsUsesAreStillThere()
        {
            var (_, u) = Field(Armed(FoundWeaponIds.AssaultRifle), (x) =>
            {
                x.Gear = new List<string> { ItemIds.FragGrenade };
                x.Charges = new Dictionary<string, int> { [ChargeIds.Grenade] = 2 };
                x.Backpack = new List<Loot>();
            });
            // Own two, and two found on the belt: one thrown still leaves the find whole.
            u.Charges = new Dictionary<string, int> { [ChargeIds.Grenade] = 3 };
            u.Belted = new List<string> { ItemIds.FragGrenade };
            Same(new List<Loot> { new Loot { Item = ItemIds.FragGrenade } }, Looting.BeltedBack(u));
            // Three thrown: the find was dipped into.
            u.Charges[ChargeIds.Grenade] = 1;
            Assert.Empty(Looting.BeltedBack(u));
            // Ear plugs have no uses: always home.
            u.Belted = new List<string> { ItemIds.MindShield };
            Same(new List<Loot> { new Loot { Item = ItemIds.MindShield } }, Looting.BeltedBack(u));
        }

        [Fact(DisplayName = "a weapon found in the mission (docs/FASE8.md §C) › without a primary, the pistol keeps its rounds beside the new gun")]
        public void WithoutAPrimaryThePistolKeepsItsRoundsBesideTheNewGun()
        {
            var (s, u) = Field(Armed(null), (x) => x.Backpack = new List<Loot> { new Loot { Weapon = FoundWeaponIds.MachineGun } });
            u.Ammo = 2;
            Assert.True(Equip(s, 0).Ok);
            Assert.Equal(((int?)2, Content.ClipOf(u, AbilityIds.Shoot)), (u.PistolAmmo, u.Ammo));
            Assert.NotNull(u.Backpack);
            Assert.Empty(u.Backpack!);
            Assert.Contains(AbilityIds.Pistol, Abilities.AbilitiesOf(u));
        }

        [Fact(DisplayName = "a weapon found in the mission (docs/FASE8.md §C) › back home, the find is the soldier's; its own comes back to the rack in a backpack, or is lost on the floor")]
        public void BackHomeTheFindIsTheSoldiers()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            var a = c.Soldiers[0];
            var b = c.Soldiers[1];
            c.FoundWeapons = new List<FoundWeapon>
            {
                new FoundWeapon { Id = "w1", Kind = FoundWeaponIds.AssaultRifle, Holder = a.Id },
                new FoundWeapon { Id = "w2", Kind = FoundWeaponIds.PumpShotgun, Holder = b.Id },
            };
            c.Squad = new List<string> { a.Id, b.Id };
            var offer = c.Offers.FirstOrDefault((o) => (o.Story ?? 0) == 0) ?? c.Offers[0];
            c.ActiveMission = offer.Id;
            var mission = Setup.CreateMission(new MissionOptions { Map = offer.Map, Slots = new List<int> { 0 }, Rng = new Rng(3), Kind = offer.Kind, Squad = Campaign.BuildSquad(c), Pods = offer.Pods, Difficulty = offer.Difficulty });
            var ua = mission.Units.First((u) => u.CampaignId == a.Id);
            var ub = mission.Units.First((u) => u.CampaignId == b.Id);
            // A swapped its rifle for a hunting rifle and kept it in the backpack; B left its shotgun on the floor for a machine gun.
            ua.Arms = Armed(FoundWeaponIds.HuntingRifle);
            ua.Backpack = new List<Loot> { new Loot { Weapon = FoundWeaponIds.AssaultRifle, WeaponId = "w1" } };
            ub.Arms = Armed(FoundWeaponIds.MachineGun);
            ub.Backpack = new List<Loot>();
            foreach (var u in mission.Units.Where((x) => x.Team == Teams.Xcom))
            {
                u.Alive = false;
                u.Evacuated = true;
            }
            mission.Outcome = Outcomes.Victory;
            mission.OutcomeReason = EndReasons.Objective;
            var report = Campaign.ApplyMissionResult(c, mission, new Rng(4));
            List<string> Held(string id) => c.FoundWeapons!.Where((w) => w.Holder == id).Select((w) => w.Kind).ToList();
            Assert.Equal(new[] { FoundWeaponIds.HuntingRifle }, Held(a.Id));
            Assert.Equal(new[] { FoundWeaponIds.MachineGun }, Held(b.Id));
            var w1 = c.FoundWeapons!.FirstOrDefault((w) => w.Id == "w1");
            Assert.NotNull(w1);
            Assert.Null(w1!.Holder);
            Assert.DoesNotContain(c.FoundWeapons!, (w) => w.Id == "w2");
            // The rifle coming back is not a find.
            Assert.Empty(report.Loot?.Weapons ?? new List<string>());
            Assert.Contains(c.Log, (l) => l.Text.Contains("vuelve sin su escopeta"));
        }
    }
}
