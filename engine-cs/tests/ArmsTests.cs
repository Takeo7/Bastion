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
    /// packages/engine/test/arms.test.ts, test by test. Not ported, they play a campaign and need campaign.ts
    /// (createCampaign, executeCampaignCommand, applyMissionResult; the helpers raise and levelTo): «the weapons from zero
    /// › a new campaign…», «… › anyone takes any weapon…», «… › the Taller makes weapons again and again…» and «a weapon
    /// found in the mission › back home, the find is the soldier's…».
    /// </summary>
    public class ArmsTests
    {
        // The weapons from zero (docs/FASE8.md §A): the class no longer brings a weapon.

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
    }
}
