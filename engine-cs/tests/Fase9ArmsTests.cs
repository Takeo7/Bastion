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
    /// packages/engine/test/fase9-arms.test.ts, test by test: the three of «weapons wear», the two of «improvements» and
    /// the two of «the Armería», which play a campaign. Its helpers `field`, `armed`, `shot` and the Armería's `run`, here
    /// as private ones.
    /// </summary>
    public class Fase9ArmsTests
    {
        // Fase 9 §5 (docs/FASE9.md): weapons wear, mend with parts, and take improvements.

        private static GameState Field()
        {
            return StateFrom(Enumerable.Range(0, 12).Select((_) => new string('.', 12)).ToArray());
        }

        private static Arms Armed(string? primary, double? condition = null, List<string>? mods = null)
        {
            return new Arms { Primary = primary, Melee = null, Condition = condition, Mods = mods };
        }

        private static GameEvent Shot(string unit, string target, string ability)
        {
            return new ShotEvent { Unit = unit, Target = target, Ability = ability, From = P(1, 1, 0), Result = ShotResults.Miss, Chance = 50, Roll = 90, Reaction = false };
        }

        private static bool HasMod(ShotPreview p, string label, int value)
        {
            return p.Mods.Any((m) => m.Label == label && m.Value == value);
        }

        // ------------------------------------------------------------ weapons wear

        [Fact(DisplayName = "weapons wear › every shot of the weapon in the hand wears it: two for the precision rifle; the pistol and the blade, nothing")]
        public void EveryShotOfTheWeaponInTheHandWearsIt()
        {
            var s = Field();
            var rifle = AddUnit(s, "s1", TemplateIds.Rookie, 1, 1, extra: (u) =>
            {
                u.Owner = 0;
                u.Arms = Armed(FoundWeaponIds.AssaultRifle);
                u.Ammo = 4;
                u.PistolAmmo = 6;
            });
            var sniper = AddUnit(s, "s2", TemplateIds.Rookie, 1, 2, extra: (u) =>
            {
                u.Owner = 0;
                u.Arms = Armed(FoundWeaponIds.PrecisionRifle, condition: 50);
                u.Ammo = 4;
            });
            AddUnit(s, "z1", TemplateIds.Zombie, 8, 8);
            foreach (var e in new[] { Shot("s1", "z1", AbilityIds.Shoot), Shot("s1", "z1", AbilityIds.Pistol), Shot("s1", "z1", AbilityIds.Slash), Shot("s2", "z1", AbilityIds.Shoot) }) Events.ApplyEvent(s, e);
            Assert.Equal(99, rifle.Arms!.Condition);
            Assert.Equal(48, sniper.Arms!.Condition);
        }

        [Fact(DisplayName = "weapons wear › below 30 it is «Gastada», −10 of aim; at 0, «Rota»: it neither fires nor keeps watch")]
        public void Below30ItIsGastada()
        {
            var s = Field();
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 1, 1, extra: (x) =>
            {
                x.Owner = 0;
                x.Arms = Armed(FoundWeaponIds.AssaultRifle, condition: 29);
                x.Ammo = 4;
            });
            var z = AddUnit(s, "z1", TemplateIds.Zombie, 5, 1);
            Assert.True(HasMod(Aim.PreviewShot(s, u, z)!, "Gastada", -10));
            u.Arms!.Condition = 0;
            Assert.Equal("Rota: no dispara. Se arregla en la Armería.", Abilities.AbilityBlocker(s, u, AbilityIds.Shoot));
            Assert.Equal("Rota: no dispara. Se arregla en la Armería.", Abilities.AbilityBlocker(s, u, AbilityIds.Overwatch));
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.Slash));
        }

        [Fact(DisplayName = "weapons wear › Apaños mends 40 of a partner's weapon alongside, once a mission")]
        public void ApanosMends40OfAPartnersWeaponAlongside()
        {
            var s = Field();
            AddUnit(s, "s1", TemplateIds.Rookie, 1, 1, extra: (u) =>
            {
                u.Owner = 0;
                u.Perks = new List<string> { PerkIds.PatchUp };
                u.Charges = new Dictionary<string, int> { [ChargeIds.PatchUp] = 1 };
            });
            var mate = AddUnit(s, "s2", TemplateIds.Rookie, 2, 1, extra: (u) =>
            {
                u.Owner = 0;
                u.Arms = Armed(FoundWeaponIds.AssaultRifle, condition: 20);
            });
            AddUnit(s, "z1", TemplateIds.Zombie, 11, 11);
            var ctx = new CommandContext { Slot = 0, Connected = new[] { 0 }, Rng = new Rng(1) };
            var r = Rules.ExecuteCommand(s, Ability("s1", AbilityIds.PatchUp, target: "s2"), ctx);
            if (!r.Ok) throw new Exception(r.Error);
            Assert.Equal(60, mate.Arms!.Condition);
            Assert.False(Rules.ExecuteCommand(s, Ability("s1", AbilityIds.PatchUp, target: "s2"), ctx).Ok);
        }

        // ------------------------------------------------------------ improvements

        [Fact(DisplayName = "improvements › a scope aims, a stock steadies on watch, a bigger magazine holds two more, a silencer halves the noise")]
        public void AScopeAimsAStockSteadiesOnWatch()
        {
            var s = Field();
            var plain = AddUnit(s, "s1", TemplateIds.Rookie, 1, 1, extra: (u) =>
            {
                u.Owner = 0;
                u.Arms = Armed(FoundWeaponIds.AssaultRifle);
                u.Ammo = 4;
            });
            var fitted = AddUnit(s, "s2", TemplateIds.Rookie, 1, 3, extra: (u) =>
            {
                u.Owner = 0;
                u.Arms = Armed(FoundWeaponIds.AssaultRifle, mods: new List<string> { WeaponModIds.Scope, WeaponModIds.Stock });
                u.Ammo = 4;
            });
            var quiet = AddUnit(s, "s3", TemplateIds.Rookie, 1, 5, extra: (u) =>
            {
                u.Owner = 0;
                u.Arms = Armed(FoundWeaponIds.AssaultRifle, mods: new List<string> { WeaponModIds.ExtMag, WeaponModIds.Silencer });
                u.Ammo = 4;
            });
            var z = AddUnit(s, "z1", TemplateIds.Zombie, 6, 3);
            Assert.True(HasMod(Aim.PreviewShot(s, fitted, z)!, "Mira", 10));
            Assert.False(HasMod(Aim.PreviewShot(s, fitted, z)!, "Culata", 10));
            Assert.True(HasMod(Aim.PreviewShot(s, fitted, z, new ShotOptions { Reaction = true })!, "Culata", 10));
            Assert.Equal(Content.ClipOf(plain, AbilityIds.Shoot) + 2, Content.ClipOf(quiet, AbilityIds.Shoot));
            Assert.Equal((int)Math.Ceiling(Abilities.NoiseOf(s, plain, AbilityIds.Shoot) / 2.0), Abilities.NoiseOf(s, quiet, AbilityIds.Shoot));
        }

        [Fact(DisplayName = "improvements › a good weapon found comes with its improvement, and any one somewhat worn")]
        public void AGoodWeaponFoundComesWithItsImprovement()
        {
            for (var seed = 1; seed <= 40; seed++)
            {
                var loot = Looting.RollBox(DecorKinds.LootWeapons, new Rng(seed), false)!;
                if (!string.IsNullOrEmpty(loot.Mod)) continue;
                Assert.True(loot.Condition >= 40, $"seed {seed}: condition {loot.Condition}");
                Assert.True(loot.Condition <= 90, $"seed {seed}: condition {loot.Condition}");
                var def = Content.FoundWeapons[loot.Weapon!];
                Assert.Equal(!string.IsNullOrEmpty(def.Mod) ? new[] { def.Mod! } : new string[0], loot.Mods ?? new List<string>());
            }
        }

        // ------------------------------------------------------------ the Armería

        /// <summary>A command alone at the base, with Rng(1); it must go through, or it throws with its reason.</summary>
        private static void Run(CampaignState c, CampaignCommand cmd, int slot = 0)
        {
            var r = Campaign.ExecuteCampaignCommand(c, cmd, new CampaignContext { Slot = slot, Connected = new[] { 0 }, Rng = new Rng(1) });
            if (!r.Ok) throw new InvalidOperationException(r.Error);
        }

        [Fact(DisplayName = "the Armería › what the shots took comes home with the weapon; parts mend it, 25 each, as far as they go")]
        public void WhatTheShotsTookComesHomeWithTheWeapon()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            var s = c.Soldiers[0];
            c.FoundWeapons = new List<FoundWeapon> { new FoundWeapon { Id = "w1", Kind = FoundWeaponIds.AssaultRifle, Holder = s.Id } };
            c.ActiveMission = c.Offers[0].Id;
            c.Squad = new List<string> { s.Id };
            var mission = Setup.CreateMission(new MissionOptions { Map = c.Offers[0].Map, Slots = new List<int> { 0 }, Rng = new Rng(2), Kind = c.Offers[0].Kind, Squad = Campaign.BuildSquad(c) });
            var unit = mission.Units.First((u) => u.CampaignId == s.Id);
            Assert.NotNull(unit.Arms);
            Assert.Equal((FoundWeaponIds.AssaultRifle, "w1"), (unit.Arms!.Primary, unit.Arms.PrimaryId));
            unit.Arms.Condition = 40;
            mission.Outcome = Outcomes.Victory;
            Campaign.ApplyMissionResult(c, mission, new Rng(3));
            var w = c.FoundWeapons.First((x) => x.Id == "w1");
            Assert.Equal(40, w.Condition);
            Assert.Equal(40, Campaign.SquadMember(c, s).Arms?.Condition);
            c.Parts = 1;
            Run(c, new RepairCommand { Weapon = "w1" });
            Assert.Equal(((double?)65, (int?)0), (w.Condition, c.Parts));
            Assert.Equal("No hay piezas.", Campaign.RepairBlocker(c, 0, "w1"));
            c.Parts = 5;
            Run(c, new RepairCommand { Weapon = "w1" });
            Assert.Equal(((double?)null, (int?)3), (w.Condition, c.Parts));
            Assert.Equal("Está como nueva.", Campaign.RepairBlocker(c, 0, "w1"));
        }

        [Fact(DisplayName = "the Armería › improvements found wait in the Armería, two at most on a weapon, and come off again")]
        public void ImprovementsFoundWaitInTheArmeria()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            c.FoundWeapons = new List<FoundWeapon> { new FoundWeapon { Id = "w1", Kind = FoundWeaponIds.AssaultRifle, Holder = null } };
            c.WeaponMods = new Dictionary<string, int> { [WeaponModIds.Scope] = 1, [WeaponModIds.Stock] = 1, [WeaponModIds.Silencer] = 1 };
            Run(c, new ModCommand { Weapon = "w1", Mod = WeaponModIds.Scope, On = true });
            Run(c, new ModCommand { Weapon = "w1", Mod = WeaponModIds.Stock, On = true });
            Assert.Matches("quita una antes", Assert.Throws<InvalidOperationException>(() => Run(c, new ModCommand { Weapon = "w1", Mod = WeaponModIds.Silencer, On = true })).Message);
            Run(c, new ModCommand { Weapon = "w1", Mod = WeaponModIds.Scope, On = false });
            Assert.Matches("No queda ningún cargador ampliado", Assert.Throws<InvalidOperationException>(() => Run(c, new ModCommand { Weapon = "w1", Mod = WeaponModIds.ExtMag, On = true })).Message);
            Assert.Equal(new[] { WeaponModIds.Stock }, c.FoundWeapons[0].Mods!);
            Assert.Equal(new[] { "scope=1", "silencer=1", "stock=0" }, c.WeaponMods.OrderBy((e) => e.Key, StringComparer.Ordinal).Select((e) => $"{e.Key}={e.Value}"));
        }
    }
}
