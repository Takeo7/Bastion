using System;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>packages/engine/test/aim.test.ts, test by test.</summary>
    public class AimTests
    {
        private static GameState Open()
        {
            return StateFrom(Enumerable.Range(0, 12).Select((_) => new string('.', 12)).ToArray());
        }

        /// <summary>A shot's numbers as the tests write them by hand (`{ origin, hit, crit, graze, … }`).</summary>
        private static ShotPreview Shot(int hit, int crit, int graze, bool flanked)
        {
            return new ShotPreview { Origin = P(0, 0), Hit = hit, Crit = crit, Graze = graze, Flanked = flanked, Cover = 0, Distance = 5, Squadsight = false };
        }

        // ------------------------------------------------------------ hit chance (XCOM 2 formula)

        [Fact(DisplayName = "hit chance (XCOM 2 formula) › aim + range modifier against a flanked target")]
        public void AimPlusRangeModifierAgainstAFlankedTarget()
        {
            var s = Open();
            var shooter = AddUnit(s, "s1", TemplateIds.Specialist, 0, 0);
            var target = AddUnit(s, "a1", TemplateIds.Trooper, 0, 10);
            var p = Aim.PreviewShot(s, shooter, target)!;
            // 65 aim + medium range at 10 tiles (+4); no cover -> flanked -> +50 crit.
            Assert.Equal(69, p.Hit);
            Assert.True(p.Flanked);
            Assert.Equal(50, p.Crit);
        }

        [Fact(DisplayName = "hit chance (XCOM 2 formula) › high cover subtracts 40 and removes the flank crit")]
        public void HighCoverSubtracts40AndRemovesTheFlankCrit()
        {
            var s = Open();
            s.Tiles[9 * 12 + 0] = TileKinds.High;
            var shooter = AddUnit(s, "s1", TemplateIds.Specialist, 0, 0);
            var target = AddUnit(s, "a1", TemplateIds.Trooper, 0, 10);
            // The high cover blocks the straight line; the shooter sees the target by its step-out.
            var p = Aim.PreviewShot(s, shooter, target)!;
            Assert.Equal(2, p.Cover);
            Assert.False(p.Flanked);
            Assert.Equal(0, p.Crit);
            Assert.True(p.Hit < 40);
        }

        [Fact(DisplayName = "hit chance (XCOM 2 formula) › reaction shots lose 30% and cannot crit")]
        public void ReactionShotsLose30PercentAndCannotCrit()
        {
            var s = Open();
            var shooter = AddUnit(s, "s1", TemplateIds.Specialist, 0, 0);
            var target = AddUnit(s, "a1", TemplateIds.Trooper, 0, 10);
            var p = Aim.PreviewShot(s, shooter, target, new ShotOptions { Reaction = true })!;
            Assert.Equal(69 - (int)Math.Floor(69 * 0.3), p.Hit);
            Assert.Equal(0, p.Crit);
        }

        [Fact(DisplayName = "hit chance (XCOM 2 formula) › hunkering adds defense and dodge (grazes)")]
        public void HunkeringAddsDefenseAndDodge()
        {
            var s = Open();
            s.Tiles[9 * 12 + 1] = TileKinds.Low;
            var shooter = AddUnit(s, "a1", TemplateIds.Trooper, 1, 0);
            var target = AddUnit(s, "s1", TemplateIds.Specialist, 1, 10, extra: (u) => u.Hunkered = true);
            var p = Aim.PreviewShot(s, shooter, target)!;
            Assert.Equal(-30, p.Mods.Find((m) => m.Label == "Cubriéndose")?.Value);
            Assert.True(p.Graze > 0);
        }

        // ------------------------------------------------------------ rolls

        [Fact(DisplayName = "rolls › rolls are deterministic per seed and damage stays in range")]
        public void RollsAreDeterministicPerSeedAndDamageStaysInRange()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (var i = 0; i < 200; i++)
            {
                var p = Shot(60, 20, 10, true);
                var r = Aim.RollShot(p, a);
                Assert.Equal(Aim.RollShot(p, b), r);
                var d = Aim.RollDamage(Content.Weapons["rifle"], r, a);
                Aim.RollDamage(Content.Weapons["rifle"], r, b);
                if (r == ShotResults.Miss) Assert.Equal(0, d);
                if (r == ShotResults.Hit) Assert.True(d >= 3);
                if (r == ShotResults.Hit) Assert.True(d <= 5);
                if (r == ShotResults.Crit) Assert.True(d >= 5);
            }
        }

        // ------------------------------------------------------------ the die on screen

        [Fact(DisplayName = "the die on screen › the roll runs 1–100 and hits exactly when it is at most the chance")]
        public void TheRollRuns1To100AndHitsExactlyWhenItIsAtMostTheChance()
        {
            var rng = new Rng(7);
            var p = Shot(93, 10, 0, false);
            for (var i = 0; i < 300; i++)
            {
                var r = Aim.ResolveShot(p, rng);
                Assert.True(r.Roll >= 1);
                Assert.True(r.Roll <= 100);
                Assert.Equal(r.Roll > 93, r.Result == ShotResults.Miss);
                if (r.Result == ShotResults.Crit) Assert.True(r.Roll <= 10);
            }
        }

        [Fact(DisplayName = "the die on screen › gives the same result as rollShot for the same seed")]
        public void GivesTheSameResultAsRollShotForTheSameSeed()
        {
            var p = Shot(55, 15, 10, false);
            var a = new Rng(3);
            var b = new Rng(3);
            for (var i = 0; i < 100; i++) Assert.Equal(Aim.RollShot(p, b), Aim.ResolveShot(p, a).Result);
        }

        // ------------------------------------------------------------ bad luck (aim assist)

        private static void Shoot(GameState s, Unit unit, Unit target, string result)
        {
            var e = new ShotEvent { Unit = unit.Id, Target = target.Id, Ability = AbilityIds.Shoot, From = unit.Pos, Result = result, Chance = 70, Roll = result == ShotResults.Miss ? 90 : 10, Reaction = false };
            Events.ApplyEvent(s, e);
        }

        private static int Luck(GameState s, Unit shooter, Unit target)
        {
            return Aim.PreviewShot(s, shooter, target)!.Mods.Find((m) => m.Label == "Mala racha")?.Value ?? 0;
        }

        [Fact(DisplayName = "bad luck (aim assist) › each miss in a row gives that player +10, up to +20, and a hit clears it")]
        public void EachMissInARowGivesThatPlayerTenUpToTwenty()
        {
            var s = Open();
            var mine = AddUnit(s, "s1", TemplateIds.Specialist, 0, 0, extra: (u) => u.Owner = 0);
            var partner = AddUnit(s, "s2", TemplateIds.Specialist, 2, 0, extra: (u) => u.Owner = 1);
            var alien = AddUnit(s, "a1", TemplateIds.Trooper, 0, 10);
            var @base = Aim.PreviewShot(s, mine, alien)!.Hit;
            Shoot(s, mine, alien, ShotResults.Miss);
            Assert.Equal(10, Luck(s, mine, alien));
            Assert.Equal(Math.Min(100, @base + 10), Aim.PreviewShot(s, mine, alien)!.Hit);
            // Per player: the partner's odds don't move.
            Assert.Equal(0, Luck(s, partner, alien));
            Shoot(s, mine, alien, ShotResults.Miss);
            Shoot(s, mine, alien, ShotResults.Miss);
            Assert.Equal(20, Luck(s, mine, alien));
            Shoot(s, mine, alien, ShotResults.Graze);
            Assert.Equal(0, Luck(s, mine, alien));
        }

        [Fact(DisplayName = "bad luck (aim assist) › counts any of that player's soldiers, never the aliens")]
        public void CountsAnyOfThatPlayersSoldiersNeverTheAliens()
        {
            var s = Open();
            var a = AddUnit(s, "s1", TemplateIds.Specialist, 0, 0, extra: (u) => u.Owner = 0);
            var b = AddUnit(s, "s2", TemplateIds.Sharpshooter, 2, 0, extra: (u) => u.Owner = 0);
            var alien = AddUnit(s, "a1", TemplateIds.Trooper, 0, 10);
            Shoot(s, a, alien, ShotResults.Miss);
            Assert.Equal(10, Luck(s, b, alien));
            Shoot(s, alien, a, ShotResults.Miss);
            Shoot(s, alien, a, ShotResults.Miss);
            Assert.Equal(0, Luck(s, alien, a));
            Assert.Equal(10, Luck(s, b, alien));
        }

        // ------------------------------------------------------------ damage on screen

        [Fact(DisplayName = "damage on screen › the range shown is exactly what the rolls give")]
        public void TheRangeShownIsExactlyWhatTheRollsGive()
        {
            foreach (var weapon in new[] { Content.Weapons["rifle"], Content.Weapons["pistol"], Content.Weapons["fragGrenade"], Content.Weapons["sniper"] })
            {
                foreach (var result in new[] { ShotResults.Hit, ShotResults.Crit, ShotResults.Graze })
                {
                    foreach (var (bonus, mult, critBonus) in new[] { (0, 1.0, 0), (2, 1.5, 1) })
                    {
                        var range = Aim.DamageRangeOf(weapon, result, bonus, mult, critBonus);
                        var rng = new Rng(7);
                        var rolls = Enumerable.Range(0, 400).Select((_) => Aim.RollDamage(weapon, result, rng, bonus, mult, critBonus)).ToList();
                        Assert.Equal((range.Min, range.Max), (rolls.Min(), rolls.Max()));
                    }
                }
            }
        }

        [Fact(DisplayName = "damage on screen › against a target: its armour soaks, Ruptura adds, the shred is named")]
        public void AgainstATargetItsArmourSoaksRupturaAdds()
        {
            var s = Open();
            var shooter = AddUnit(s, "s1", TemplateIds.Specialist, 0, 0);
            var target = AddUnit(s, "a1", TemplateIds.Mec, 0, 10, extra: (u) => u.Ruptured = 3);
            var d = Aim.ShotDamage(shooter, AbilityIds.Shoot, target, Aim.PreviewShot(s, shooter, target)!);
            // Rifle 3–5 (+2 on a crit), +3 from Ruptura, −1 for the Gólem's armour.
            Assert.Equal((5, 7), (d.Hit.Min, d.Hit.Max));
            Assert.Equal((7, 9), (d.Crit!.Min, d.Crit.Max));
            Assert.Equal(1, d.Armor);
            // Without a target, the weapon's own numbers.
            var own = Aim.ShotDamage(shooter, AbilityIds.Shoot).Hit;
            Assert.Equal((3, 5), (own.Min, own.Max));
        }

        [Fact(DisplayName = "damage on screen › grenades use the same numbers as the rules")]
        public void GrenadesUseTheSameNumbersAsTheRules()
        {
            var s = Open();
            var grenadier = AddUnit(s, "s1", TemplateIds.Grenadier, 0, 0);
            // `{ ...grenadier.mods, grenadeDamage: 2 }`: a unit's mods are its own (content.ts ZERO_MODS is a new one each time).
            grenadier.Mods.GrenadeDamage = 2;
            var mec = AddUnit(s, "a1", TemplateIds.Mec, 0, 10);
            // Frag 3 (+1 one time in five) +2, shreds 1; the Gólem's armour takes 1.
            var plain = Rules.AbilityDamage(s, grenadier, AbilityIds.Grenade)!;
            Assert.Equal((5, 6), (plain.Hit.Min, plain.Hit.Max));
            Assert.Equal(1, plain.Shred);
            Assert.Null(plain.Crit);
            var onMec = Rules.AbilityDamage(s, grenadier, AbilityIds.Grenade, mec)!.Hit;
            Assert.Equal((4, 5), (onMec.Min, onMec.Max));
            Assert.Null(Rules.AbilityDamage(s, grenadier, AbilityIds.Overwatch));
        }
    }
}
