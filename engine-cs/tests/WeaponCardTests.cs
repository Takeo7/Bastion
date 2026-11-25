using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Bastion.Net;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// O7 (Arturo, 25/11: «me gustaría ver el daño que voy a hacer, no el daño del arma en sí»): a weapon's card in a soldier's
    /// hands (Aim.CardFor in a fight, Runs.CardFor at the base), the numbers the rules' own, the bonuses by their names.
    /// </summary>
    public class WeaponCardTests
    {
        /// <summary>The board's cells by their ids: Mano dura (+1 main weapon), Golpe pesado (+1 blade), Punta hueca (+1 on a crit), Calibre gordo (+1 pistol).</summary>
        private static readonly string[] Cells = { "heavyHand", "heavyBlow", "hollowPoint", "bigBore" };

        private static string Name(string cell) => Board.BoardCells[cell].Name;

        /// <summary>A run's fight launched with the first soldier holding an assault rifle and the board's cells `cells`.</summary>
        private static (CampaignState C, GameState S, CampaignSoldier Soldier, Unit Unit) Launched(params string[] cells)
        {
            CampaignSoldier? soldier = null;
            var (c, s) = LaunchAt(NodeKinds.Normal, 1, (c) =>
            {
                soldier = c.Soldiers.First((x) => x.Alive);
                soldier.Board = new List<string>(cells);
                InSlot(c, soldier, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            });
            var unit = s.Units.First((u) => u.CampaignId == soldier!.Id);
            return (c, s, soldier!, unit);
        }

        [Fact(DisplayName = "ficha › the damage of a soldier carrying the weapon is ShotDamage's, in the fight and at the base")]
        public void TheRulesOwnDamage()
        {
            var (c, _, soldier, u) = Launched(Cells);
            var sources = Runs.SourcesOf(soldier);
            foreach (var (gear, ability) in new[] { (u.Kit!.Primary!, AbilityIds.Shoot), (u.Kit.Melee!, AbilityIds.Slash) })
            {
                var shot = Aim.ShotDamage(u, ability);
                var card = Aim.CardFor(u, gear, sources)!;
                Assert.Equal(ability, card.Ability);
                Assert.Equal((shot.Hit.Min, shot.Hit.Max, shot.Crit!.Min, shot.Crit.Max), (card.Damage.Min, card.Damage.Max, card.CritDamage.Min, card.CritDamage.Max));
                // At the base, as it set out with it: the same.
                var base_ = Runs.CardFor(c, soldier, gear)!;
                Assert.Equal((card.Damage.Min, card.Damage.Max, card.CritDamage.Min, card.CritDamage.Max, card.Aim, card.Crit), (base_.Damage.Min, base_.Damage.Max, base_.CritDamage.Min, base_.CritDamage.Max, base_.Aim, base_.Crit));
            }
        }

        [Fact(DisplayName = "ficha › the sources add up to the soldier's mods as it sets out")]
        public void TheSourcesAddUp()
        {
            var (c, _, soldier, u) = Launched(Cells);
            soldier.Traits = new List<string> { "grudge" };
            var sum = Runs.SourcesOf(soldier).Aggregate(Content.ZeroMods, (m, x) => Content.AddMods(m, x.Mods));
            Assert.Equal(Json.Write(Runs.SquadMemberOf(c, soldier).Mods), Json.Write(sum));
            Assert.Contains(Runs.SourcesOf(soldier), (x) => x.Label == Quests.Traits["grudge"].Name && x.Mods.Aim == 10);
        }

        [Fact(DisplayName = "ficha › each bonus by its name, only those of that weapon; the damage the weapon's and theirs")]
        public void BonusesByName()
        {
            var (c, _, soldier, u) = Launched(Cells);
            var rifle = Aim.CardFor(u, u.Kit!.Primary!, Runs.SourcesOf(soldier))!;
            var knife = Aim.CardFor(u, u.Kit.Melee!, Runs.SourcesOf(soldier))!;
            Assert.Contains(rifle.Bonuses, (b) => b.Label == Name("heavyHand") && b.Damage == 1);
            Assert.DoesNotContain(rifle.Bonuses, (b) => b.Label == Name("heavyBlow") || b.Label == Name("bigBore"));
            Assert.Contains(knife.Bonuses, (b) => b.Label == Name("heavyBlow") && b.Damage == 1);
            Assert.DoesNotContain(knife.Bonuses, (b) => b.Label == Name("heavyHand"));
            foreach (var card in new[] { rifle, knife })
            {
                Assert.Contains(card.Bonuses, (b) => b.Label == Name("hollowPoint") && b.CritDamage == 1);
                Assert.Equal(card.Weapon.Damage.Min + card.Bonuses.Sum((b) => b.Damage), card.Damage.Min);
                Assert.Equal(card.Weapon.CritDamage.Max + card.Bonuses.Sum((b) => b.Damage + b.CritDamage), card.CritDamage.Max);
                // Nothing left without a name.
                Assert.DoesNotContain(card.Bonuses, (b) => b.Label == "");
            }
            // The lines too, by their names: shooting's aim on the rifle.
            Assert.All(rifle.Bonuses, (b) => Assert.False(string.IsNullOrEmpty(b.Label)));
        }

        [Fact(DisplayName = "ficha › the pistol: beside a weapon in the hand its own shot (Calibre gordo); alone, the main one's (Mano dura)")]
        public void ThePistol()
        {
            var (c, _, soldier, _) = Launched(Cells);
            var pistol = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Pistol);
            var beside = Runs.CardFor(c, soldier, pistol)!;
            Assert.Equal(AbilityIds.Pistol, beside.Ability);
            Assert.Contains(beside.Bonuses, (b) => b.Label == Name("bigBore") && b.Damage == 1);
            Assert.DoesNotContain(beside.Bonuses, (b) => b.Label == Name("heavyHand"));
            soldier.Kit!.Primary = null;
            var alone = Runs.CardFor(c, soldier, pistol)!;
            Assert.Equal(AbilityIds.Shoot, alone.Ability);
            Assert.Contains(alone.Bonuses, (b) => b.Label == Name("heavyHand") && b.Damage == 1);
            Assert.DoesNotContain(alone.Bonuses, (b) => b.Label == Name("bigBore"));
        }

        [Fact(DisplayName = "ficha › Mano de machete: at the base with a machete, its +2 and +15; picked up in a fight, not (the rules')")]
        public void BladeMaster()
        {
            var (c, _, soldier, u) = Launched("bladeMaster", "heavyBlow");
            var machete = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Machete);
            var atBase = Runs.CardFor(c, soldier, machete)!;
            Assert.Contains(atBase.Bonuses, (b) => b.Label == Name("bladeMaster") && b.Damage == 2 && b.Aim == 15);
            Assert.DoesNotContain(Runs.CardFor(c, soldier, soldier.Kit!.Melee!)!.Bonuses, (b) => b.Label == Name("bladeMaster"));
            // Out with the knife, the machete taken up there: no Mano de machete (Bars.FighterStatsOf, as it set out).
            var inFight = Aim.CardFor(u, machete, Runs.SourcesOf(soldier))!;
            Assert.DoesNotContain(inFight.Bonuses, (b) => b.Label == Name("bladeMaster"));
            Assert.Equal(atBase.Damage.Min - 2, inFight.Damage.Min);
            Assert.DoesNotContain(inFight.Bonuses, (b) => b.Label == "");
        }

        [Fact(DisplayName = "ficha › the aim and the crit are PreviewShot's own terms, the scope's aim with them")]
        public void AimAndCritArePreviewShots()
        {
            var s = Step1Helpers.StateFrom(new[] { "..........", "..........", ".........." });
            var rifle = new Gear { Id = "g1", Kind = GearKinds.Weapon, Type = FoundWeaponIds.AssaultRifle, Count = 1, Mods = new List<string> { WeaponModIds.Scope } };
            var u = Step1Helpers.AddUnit(s, "s1", TemplateIds.Rookie, 1, 1, extra: (x) =>
            {
                x.Arms = new Arms { Primary = FoundWeaponIds.AssaultRifle, PrimaryId = "g1", Mods = new List<string> { WeaponModIds.Scope }, Pistol = false };
                x.Kit = new KitPlaces { Primary = rifle };
                x.Mods = Content.AddMods(x.Mods, new UnitMods { Aim = 3, GunAim = 5, MeleeAim = 7, Crit = 4, GunCrit = 2, MeleeCrit = 9 });
            });
            var z = Step1Helpers.AddUnit(s, "a1", TemplateIds.Zombie, 6, 1);
            var shot = Aim.PreviewShot(s, u, z)!;
            var card = Aim.CardFor(u, rifle)!;
            var own = new[] { "Puntería", "Arma", "Tiro", "Mira", "Gastada", "Pistolero" };
            Assert.Equal(shot.Mods.Where((m) => own.Contains(m.Label)).Sum((m) => m.Value), card.Aim);
            Assert.Equal(shot.CritMods.Where((m) => m.Label != "Flanqueo" && m.Label != "Quemarropa" && m.Label != "Visión de escuadra").Sum((m) => m.Value), card.Crit);
            var bare = Aim.CardFor(u, new Gear { Id = "g2", Kind = GearKinds.Weapon, Type = FoundWeaponIds.AssaultRifle, Count = 1 })!;
            Assert.Equal(Content.ModAim, card.Aim - bare.Aim);
        }

        [Fact(DisplayName = "ficha › the knife, no weapon of the Armería's: its card as everyone's, with the soldier's blade bonuses")]
        public void TheKnife()
        {
            var (c, _, soldier, u) = Launched("heavyBlow");
            var knife = u.Kit!.Melee!;
            Assert.Equal(FoundWeaponIds.Knife, knife.Type);
            var card = Aim.CardFor(u, knife, Runs.SourcesOf(soldier))!;
            var alone = Aim.BaseWeaponProfile(WeaponIds.Knife);
            Assert.Equal((FoundWeaponIds.Knife, alone.Damage.Min, alone.Damage.Max), (card.Weapon.Kind, card.Weapon.Damage.Min, card.Weapon.Damage.Max));
            Assert.Equal((alone.Damage.Min + 1, alone.Damage.Max + 1), (card.Damage.Min, card.Damage.Max));
            Assert.NotNull(Runs.CardFor(c, soldier, soldier.Kit!.Melee!));
        }

        [Fact(DisplayName = "ficha › the soldier's base, the weapon and the bonuses add up to the aim and the crit (cuadro 156)")]
        public void BaseWeaponAndBonusesAddUp()
        {
            var (c, _, soldier, u) = Launched(Cells);
            soldier.Growth[SkillIds.Shooting] = 40;
            soldier.Growth[SkillIds.Melee] = 30;
            var scoped = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            scoped.Mods = new List<string> { WeaponModIds.Scope };
            var cards = new[]
            {
                Runs.CardFor(c, soldier, scoped)!,
                Runs.CardFor(c, soldier, soldier.Kit!.Melee!)!,
                Runs.CardFor(c, soldier, Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Pistol))!,
                Aim.CardFor(u, u.Kit!.Primary!, Runs.SourcesOf(soldier))!,
                Aim.CardFor(u, u.Kit.Melee!)!,
            };
            foreach (var card in cards)
            {
                Assert.Equal(card.Aim, card.BaseAim + card.Weapon.Aim + card.Bonuses.Sum((b) => b.Aim));
                Assert.Equal(card.Crit, System.Math.Clamp(card.BaseCrit + card.Weapon.Crit + card.Bonuses.Sum((b) => b.Crit), 0, 100));
            }
            // With the names: the soldier's base, 55 on a run (Bars.BaseAim), and its template's crit; the scope in the weapon's.
            var tpl = Content.Templates[TemplateIds.Rookie];
            foreach (var card in cards.Take(4)) Assert.Equal((Bars.BaseAim, tpl.Crit), (card.BaseAim, card.BaseCrit));
            Assert.Equal(Content.Weapons[Content.FoundWeapons[FoundWeaponIds.AssaultRifle].Weapon].Aim + Content.ModAim, cards[0].Weapon.Aim);
            Assert.Contains(cards[0].Bonuses, (b) => b.Label == Bars.SkillDefs[SkillIds.Shooting].Name && b.Aim > 0);
            // Without the names, the soldier's all in its base.
            Assert.Empty(cards[4].Bonuses);
            Assert.Equal(cards[4].Aim - cards[4].Weapon.Aim, cards[4].BaseAim);
        }

        [Fact(DisplayName = "ficha › not a weapon: none")]
        public void NotAWeapon()
        {
            var (c, _, soldier, u) = Launched();
            var vest = Gears.NewGear(c, GearKinds.Vest, VestIds.Vest);
            Assert.Null(Aim.CardFor(u, vest));
            Assert.Null(Runs.CardFor(c, soldier, vest));
        }
    }
}
