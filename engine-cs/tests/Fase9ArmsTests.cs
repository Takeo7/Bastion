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
    /// packages/engine/test/fase9-arms.test.ts, test by test. Not ported, they play a campaign and need campaign.ts
    /// (createCampaign, buildSquad, applyMissionResult, executeCampaignCommand, repairBlocker): the two of «the Armería»
    /// (what the shots took comes home…; improvements found wait in the Armería…).
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
    }
}
