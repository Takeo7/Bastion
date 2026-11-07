using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Arturo's rules of 1/11 (docs/PRUEBA-2026-11-01-ARTURO.md #10): a new campaign without pistols, grenades or medkits,
    /// the pistol a weapon of the Armería, and the cóctel molotov before the grenades. The police car of the first mission
    /// gave way on 2/11 to the police cars of any mission (NotesOfTwoNovTests).
    /// New in C#, not in the TypeScript engine.
    /// </summary>
    public class PistolsAndMolotovTests
    {
        private static CommandContext Ctx()
        {
            return new CommandContext { Slot = 0, Connected = new[] { 0 }, Rng = new Rng(1) };
        }

        /// <summary>An open field with a zombie asleep far off, so the mission goes on, and a soldier of the campaign with nothing.</summary>
        private static (GameState s, Unit u) Field(Arms arms, int x = 5, int y = 5)
        {
            var s = StateFrom(Enumerable.Range(0, 20).Select((_) => new string('.', 20)).ToArray());
            var z = AddUnit(s, "a1", TemplateIds.Zombie, 19, 19);
            var pod = new Pod { Id = "p1", Active = false, UnitIds = new List<string> { z.Id }, Patrol = new List<Pos>(), PatrolIndex = 0 };
            z.PodId = pod.Id;
            s.Pods.Add(pod);
            var u = AddUnit(s, "s1", TemplateIds.Rookie, x, y, extra: (v) =>
            {
                v.Arms = arms;
                v.Mags = 0;
            });
            u.Ammo = Content.HasGun(u) ? Content.ClipOf(u, AbilityIds.Shoot) : 0;
            return (s, u);
        }

        // ------------------------------------------------------------ the pistol

        [Fact(DisplayName = "pistols › a pistol picked up is the gun of one without any, and a second one beside a weapon in the hand")]
        public void APistolPickedUp()
        {
            var (s, u) = Field(new Arms { Pistol = false });
            Assert.DoesNotContain(AbilityIds.Shoot, Abilities.AbilitiesOf(u));
            u.Backpack = new List<Loot> { new Loot { Weapon = FoundWeaponIds.Pistol, WeaponId = "w3" } };
            Assert.True(Rules.ExecuteCommand(s, new ItemCommand { Unit = "s1", Action = ItemActions.Equip, Index = 0 }, Ctx()).Ok);
            Assert.Equal((true, "w3"), (u.Arms!.Pistol, u.Arms.PistolId));
            Assert.Equal(Content.PistolClip, u.Ammo);
            Assert.Equal("Pistola", u.WeaponName);
            Assert.Equal("pistol", Content.AttackWeapon(u, AbilityIds.Shoot).Id);
            Assert.Contains(AbilityIds.Shoot, Abilities.AbilitiesOf(u));
            Assert.DoesNotContain(AbilityIds.Pistol, Abilities.AbilitiesOf(u));
            Assert.Empty(u.Backpack);

            // Beside a rifle, the pistol is the second gun, with rounds of its own.
            var (t, v) = Field(new Arms { Primary = FoundWeaponIds.AssaultRifle, Pistol = false });
            Assert.Contains(AbilityIds.Shoot, Abilities.AbilitiesOf(v));
            Assert.DoesNotContain(AbilityIds.Pistol, Abilities.AbilitiesOf(v));
            v.Backpack = new List<Loot> { new Loot { Weapon = FoundWeaponIds.Pistol } };
            Assert.True(Rules.ExecuteCommand(t, new ItemCommand { Unit = "s1", Action = ItemActions.Equip, Index = 0 }, Ctx()).Ok);
            Assert.Equal(FoundWeaponIds.AssaultRifle, v.Arms!.Primary);
            Assert.Equal(Content.PistolClip, v.PistolAmmo);
            Assert.Contains(AbilityIds.Pistol, Abilities.AbilitiesOf(v));
        }

        [Fact(DisplayName = "pistols › searches turn them up only where they are counted")]
        public void SearchesTurnThemUpOnlyWhereCounted()
        {
            var counted = 0;
            for (var seed = 1; seed <= 300; seed++)
            {
                Assert.NotEqual(FoundWeaponIds.Pistol, Looting.RollBox(DecorKinds.LootWeapons, new Rng(seed), false)?.Weapon);
                if (Looting.RollBox(DecorKinds.LootWeapons, new Rng(seed), false, true)?.Weapon == FoundWeaponIds.Pistol) counted++;
            }
            // The commonest weapon found: 12 in about 50 of the weights, in seven boxes of ten.
            Assert.InRange(counted, 25, 80);
        }

        [Fact(DisplayName = "pistols › one carried comes home to the Armería, the soldier's; the Taller makes them only where they are counted")]
        public void OneCarriedComesHome()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            // Without the opening arms (Arturo, 2/11), to see the one picked up come home.
            c.FoundWeapons = null;
            c.Squad = c.Soldiers.Take(4).Select((x) => x.Id).ToList();
            var s = Setup.CreateMission(new MissionOptions { Map = MapSource.Generated(Biomes.City, 3), Slots = new List<int> { 0 }, Rng = new Rng(1), Squad = Campaign.BuildSquad(c), Pistols = true });
            var u = s.Units.First((x) => x.CampaignId == c.Squad[0]);
            u.Arms!.Pistol = true;
            s.Outcome = Outcomes.Victory;
            // (Who carried it, kept: the squad is cleared once the mission is folded in; it used to be filled again by a
            // siege starting, before sieges were off, Arturo 7/11.)
            var bearer = c.Squad[0];
            Campaign.ApplyMissionResult(c, s, new Rng(1));
            var pistol = Assert.Single(c.FoundWeapons!);
            Assert.Equal((FoundWeaponIds.Pistol, bearer), (pistol.Kind, pistol.Holder));
            Assert.Equal((true, pistol.Id), (Campaign.SoldierArms(c, c.Soldiers.First((x) => x.Id == bearer)).Pistol, Campaign.SoldierArms(c, c.Soldiers.First((x) => x.Id == bearer)).PistolId));
            Assert.Equal("En esta campaña, todos los soldados ya llevan pistola.", Campaign.CraftBlocker(new CampaignState { Pistols = null, Base = c.Base, Crafted = new List<string>() }, CraftIds.MakePistol));
        }

        [Fact(DisplayName = "pistols › where they are counted, a level reached brings no weapons: they are only found or made")]
        public void ALevelBringsNoWeapons()
        {
            foreach (var counted in new bool?[] { true, null })
            {
                var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
                c.Pistols = counted;
                c.FoundWeapons = null;
                CampaignHelpers.LevelTo(c, 2);
                Assert.Equal(counted == true ? 0 : 2, (c.FoundWeapons ?? new List<FoundWeapon>()).Count);
            }
        }

        // ------------------------------------------------------------ the molotov

        [Fact(DisplayName = "molotov › the ground round where it lands burns, 3×3, for two of the squad's turns, and whoever is in it burns at once")]
        public void TheGroundBurns()
        {
            var (s, u) = Field(new Arms { Pistol = false });
            u.Gear = new List<string> { ItemIds.Molotov };
            u.Charges[ChargeIds.Molotov] = 2;
            Assert.Contains(AbilityIds.Molotov, Abilities.AbilitiesOf(u));
            var z = AddUnit(s, "a2", TemplateIds.Zombie, 9, 5);
            // Another soldier with actions left, so that the squad's turn doesn't end by itself with the throw.
            AddUnit(s, "s2", TemplateIds.Rookie, 1, 1, extra: (v) => v.Arms = new Arms { Pistol = false });
            var hp = z.Hp;
            Assert.True(Rules.ExecuteCommand(s, Ability("s1", AbilityIds.Molotov, tile: P(9, 5)), Ctx()).Ok);
            Assert.Equal(hp - 1, z.Hp);
            Assert.Equal(1, u.Charges[ChargeIds.Molotov]);
            var fire = Assert.Single(s.Fires!);
            Assert.Equal(("m1", 9, Content.MolotovTurns), (fire.Id, fire.Tiles.Count, fire.Turns));
            Assert.Equal(Grid.TileIndex(s, 9, 5), fire.Source);
            Assert.True(Burning.At(s, P(8, 4)));
            Assert.True(Burning.At(s, P(10, 6)));
            Assert.False(Burning.At(s, P(11, 5)));
            Assert.Equal(Content.MolotovTurns, s.Lights!.Single((l) => l.Id == "m1").Turns);
            // Through the enemy's turn and the squad's next: burning; out as the second enemy turn ends.
            z.Hp = 0;
            z.Alive = false;
            Assert.True(Rules.ExecuteCommand(s, new EndTurnCommand { Ready = true }, Ctx()).Ok);
            Assert.Equal(2, s.Turn);
            Assert.Equal(1, s.Fires!.Single().Turns);
            Assert.True(Rules.ExecuteCommand(s, new EndTurnCommand { Ready = true }, Ctx()).Ok);
            Assert.Equal(3, s.Turn);
            Assert.Empty(s.Fires!);
            Assert.DoesNotContain(s.Lights!, (l) => l.Id == "m1");
        }

        [Fact(DisplayName = "molotov › on the ground only, and it burns no wall")]
        public void OnTheGroundOnly()
        {
            var s = StateFrom(new[] { "..........", "....#.....", "..........", "..........", ".........." });
            var u = AddUnit(s, "s1", TemplateIds.Rookie, 1, 1, extra: (v) => v.Arms = new Arms { Pistol = false });
            AddUnit(s, "a1", TemplateIds.Zombie, 9, 4);
            u.Gear = new List<string> { ItemIds.Molotov };
            u.Charges[ChargeIds.Molotov] = 2;
            Assert.False(Abilities.CanTargetTile(s, u, AbilityIds.Molotov, P(4, 2, 1)));
            Assert.True(Rules.ExecuteCommand(s, Ability("s1", AbilityIds.Molotov, tile: P(4, 2)), Ctx()).Ok);
            Assert.DoesNotContain(Grid.TileIndex(s, 4, 1), s.Fires!.Single().Tiles);
            Assert.Equal(8, s.Fires!.Single().Tiles.Count);
        }

        [Fact(DisplayName = "molotov › before the grenades: from the Bastión's first level, the grenades from its third")]
        public void BeforeTheGrenades()
        {
            Assert.True(Progression.ItemUnlocked(1, ItemIds.Molotov));
            Assert.False(Progression.ItemUnlocked(1, ItemIds.FragGrenade));
            Assert.Equal(3, Content.Items[ItemIds.FragGrenade].Level);
            Assert.Equal(new Dictionary<string, int> { [ChargeIds.Molotov] = 2 }, Content.Items[ItemIds.Molotov].Charges);
        }
    }
}
