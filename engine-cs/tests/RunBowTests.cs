using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;
using static Bastion.Engine.Tests.Step2Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Arco y ballesta (Arturo, cuadros 101–106; .dev-snapshots/gameplay/g23/ARCO-Y-BALLESTA.md): two quiet long arms that fire
    /// arrows, one ammunition for both in the pack, 10 to a cell. The crossbow strong, a bolt loaded and an action and an arrow to
    /// reload; the recurve quick, never reloaded, an arrow a shot. A hit stays in the body and comes back searching it; a miss
    /// is lost. A kill with them nobody else sees keeps the squad hidden. The Cazador carries the crossbow (its arrows
    /// uncounted) and gives it, with 5 arrows, off its body; one Cazador fewer in a guarded Rescate. Rare in a run's boxes,
    /// in the Trueque; never in the campaign of before.
    /// </summary>
    public class RunBowTests
    {
        private const double East = 0;

        /// <summary>
        /// An open field 16 × 7, the squad hidden: a soldier at (3, 3) with `weapon` loaded, `arrows` in its pack, never
        /// missing; one of the dead at (9, 3), unaware and looking away; another far off, looking away too.
        /// </summary>
        private static GameState Field(out Unit u, out Unit z, string weapon, int? arrows, int hp = 1)
        {
            var s = StateFrom(Enumerable.Repeat(new string('.', 16), 7).ToArray());
            s.Concealed = true;
            u = AddUnit(s, "s1", TemplateIds.Rookie, 3, 3, extra: (x) =>
            {
                x.Arms = new Arms { Primary = weapon };
                x.Mods = new UnitMods { Aim = 300 };
                x.Arrows = arrows;
                x.Mags = 3;
            });
            u.Ammo = Content.ClipOf(u, AbilityIds.Shoot);
            z = AddUnit(s, "a1", TemplateIds.Zombie, 9, 3, extra: (x) => x.Facing = East);
            z.Hp = hp;
            Pod(s, "p1", new[] { z }, active: false);
            var far = AddUnit(s, "a9", TemplateIds.Zombie, 15, 6, extra: (x) => x.Facing = East);
            Pod(s, "p9", new[] { far }, active: false);
            return s;
        }

        private static CommandResult Shoot(GameState s, string ability = AbilityIds.Shoot) => Exec(s, Ability("s1", ability, target: "a1"));

        [Fact]
        public void TheTwoWeaponsTheirFindsAndTheirArrows()
        {
            var crossbow = Content.Weapons[WeaponIds.Crossbow];
            Assert.Equal((5, 2, 1, RangeTableIds.Long, 2, true), (crossbow.Damage, crossbow.CritDamage, crossbow.Clip, crossbow.Range, crossbow.Noise, crossbow.Arrows));
            var bow = Content.Weapons[WeaponIds.Bow];
            Assert.Equal((3, 1, 0, RangeTableIds.Medium, 1, true), (bow.Damage, bow.CritDamage, bow.Clip, bow.Range, bow.Noise, bow.Arrows));
            Assert.False(Content.Weapons[WeaponIds.Rifle].Arrows);
            foreach (var (id, weapon) in new[] { (FoundWeaponIds.Crossbow, WeaponIds.Crossbow), (FoundWeaponIds.Bow, WeaponIds.Bow) })
            {
                var def = Content.FoundWeapons[id];
                Assert.Equal((weapon, FoundWeaponSlots.Primary, 0), (def.Weapon, def.Slot, def.Weight));
                Assert.Contains(id, FoundWeaponIds.All);
                Assert.True(Content.IsGear(GearKinds.Weapon, id));
                // Nothing mounts on them: not firearms.
                Assert.False(Content.TakesMods(new Gear { Kind = GearKinds.Weapon, Type = id }));
            }
            Assert.Equal((4, 1), Content.GearSize(GearKinds.Weapon, FoundWeaponIds.Bow));
            Assert.Equal((3, 1), Content.GearSize(GearKinds.Weapon, FoundWeaponIds.Crossbow));
            Assert.Contains(GearKinds.Arrows, GearKinds.All);
            Assert.True(Content.IsGear(GearKinds.Arrows, null));
            Assert.Equal(10, Content.PileOf(GearKinds.Arrows, null));
        }

        [Fact]
        public void ABowShotTakesAnArrowAndAKillNobodySeesKeepsTheSquadHidden()
        {
            var s = Field(out var u, out var z, FoundWeaponIds.Bow, 5);
            Assert.True(Rules.SilentKill(s, u, z));
            var r = Shoot(s);
            Assert.True(r.Ok, r.Error);
            Assert.Equal(ShotResults.Hit, Of<ShotEvent>(r.Events).Single().Result);
            Assert.False(z.Alive);
            Assert.Equal(4, u.Arrows);
            Assert.Equal(1, z.ArrowsIn);
            Assert.True(s.Concealed);
            Assert.Empty(Of<ConcealmentBrokenEvent>(r.Events));
            Assert.Empty(Of<PodActivatedEvent>(r.Events));
            Assert.False(s.Pods.Single((p) => p.Id == "p9").Active);
        }

        [Fact]
        public void AHitThatDoesntKillGivesTheSquadAwayAndTheArrowStaysIn()
        {
            var s = Field(out var u, out var z, FoundWeaponIds.Bow, 5, hp: 20);
            var r = Shoot(s);
            Assert.True(r.Ok, r.Error);
            Assert.True(z.Alive);
            Assert.Equal((4, 1), (u.Arrows, z.ArrowsIn));
            Assert.False(s.Concealed);
            // A kill with a rifle gives it away too.
            var rifle = Field(out _, out var other, FoundWeaponIds.AssaultRifle, null);
            var shot = Shoot(rifle);
            Assert.True(shot.Ok, shot.Error);
            Assert.False(other.Alive);
            Assert.False(rifle.Concealed);
            Assert.Null(other.ArrowsIn);
        }

        [Fact]
        public void WithoutArrowsTheBowNeitherShootsNorWatches()
        {
            var s = Field(out var u, out var z, FoundWeaponIds.Bow, 0);
            Assert.Equal(Abilities.NoArrows, Abilities.AbilityBlocker(s, u, AbilityIds.Shoot));
            Assert.Equal(Abilities.NoArrows, Abilities.AbilityBlocker(s, u, AbilityIds.Overwatch));
            Assert.False(Content.LoadedFor(u, AbilityIds.Shoot));
            Assert.Equal((false, Abilities.NoArrows), (Shoot(s).Ok, Shoot(s).Error));
            Assert.True(z.Alive);
            // The pistol beside it still fires.
            u.Arms!.Pistol = true;
            u.PistolAmmo = Content.PistolClip;
            Assert.Null(Abilities.AbilityBlocker(s, u, AbilityIds.Pistol));
        }

        [Fact]
        public void TheCrossbowShootsItsBoltAndReloadsWithAnArrowNotAMagazine()
        {
            var s = Field(out var u, out _, FoundWeaponIds.Crossbow, 2, hp: 20);
            Assert.Equal(1, u.Ammo);
            Assert.True(Shoot(s).Ok);
            Assert.Equal((0, 2, 3), (u.Ammo, u.Arrows!.Value, u.Mags!.Value));
            Assert.Equal("Sin munición: recarga.", Abilities.AbilityBlocker(s, u, AbilityIds.Shoot));
            u.Ap = 2;
            var r = Exec(s, Ability("s1", AbilityIds.Reload));
            Assert.True(r.Ok, r.Error);
            Assert.Equal((1, 1, 3), (u.Ammo, u.Arrows!.Value, u.Mags!.Value));
            // No arrows: no bolt, whatever magazines there are.
            u.Ammo = 0;
            u.Arrows = 0;
            Assert.Equal(Abilities.NoArrows, Abilities.AbilityBlocker(s, u, AbilityIds.Reload));
            Assert.Equal(Abilities.NoArrows, Abilities.AbilityBlocker(s, u, AbilityIds.Shoot));
            // Nothing mounted lengthens its clip.
            u.Arms!.Mods = new List<string> { WeaponModIds.ExtMag };
            Assert.Equal(1, Content.ClipOf(u, AbilityIds.Shoot));
        }

        [Fact]
        public void TheCazadorShootsACrossbowWhoseArrowsNeverRunOut()
        {
            var t = Content.Templates[TemplateIds.Lancer];
            Assert.Equal(WeaponIds.Crossbow, t.Weapon);
            Assert.Null(t.MeleeWeapon);
            Assert.Equal(new[] { AbilityIds.Shoot, AbilityIds.Overwatch, AbilityIds.Reload }, t.Abilities);
            var s = StateFrom(Enumerable.Repeat(new string('.', 8), 5).ToArray());
            var lancer = AddUnit(s, "a1", TemplateIds.Lancer, 2, 2);
            Assert.Null(lancer.Arrows);
            Assert.True(Content.FiresArrows(lancer));
            Assert.False(Content.OutOfArrows(lancer));
            lancer.Ammo = 0;
            Assert.Null(Abilities.AbilityBlocker(s, lancer, AbilityIds.Reload));
        }

        [Fact]
        public void ArrowsInABodyComeBackSearchingItAndTheCazadorsGivesItsCrossbowAndFive()
        {
            var z = new Unit { Id = "a1", Team = Teams.Alien, Template = TemplateIds.Zombie, ArrowsIn = 2 };
            var loot = Looting.RollBody(z, new Rng(3), true);
            Assert.Equal(2, loot!.Arrows);
            Assert.Null(Looting.RollBody(new Unit { Id = "a2", Team = Teams.Alien, Template = TemplateIds.Zombie }, new Rng(1), true)?.Arrows);
            var lancer = Looting.RollBody(new Unit { Id = "a3", Team = Teams.Alien, Template = TemplateIds.Lancer, ArrowsIn = 1 }, new Rng(1), true)!;
            Assert.Equal((FoundWeaponIds.Crossbow, Content.CazadorArrows + 1), (lancer.Weapon, lancer.Arrows));
            // As a run's gear: arrows, ten to a cell.
            var maker = new RunLoot.Maker(1);
            var gear = RunLoot.GearOf(new Loot { Arrows = 13 }, new RunFight(), new Rng(1), maker);
            Assert.Equal(new[] { 10, 3 }, gear.Where((g) => g.Kind == GearKinds.Arrows).Select((g) => g.Count));
        }

        [Fact]
        public void ARunSoldierCountsTheArrowsInItsPackAndSpendsThemOffTheSmallestPile()
        {
            var (_, s) = RunsHelpers.LaunchAt(NodeKinds.Normal);
            var u = s.Units.First((x) => x.Team == Teams.Xcom && x.CampaignId != null);
            Assert.Equal(0, u.Arrows);
            Assert.NotNull(Gears.Place(u.Pack!, new Gear { Id = "ga", Kind = GearKinds.Arrows, Count = 10 }, 0, 0, false));
            Assert.NotNull(Gears.Place(u.Pack!, new Gear { Id = "gb", Kind = GearKinds.Arrows, Count = 2 }, 1, 0, false));
            RunPack.SyncMags(u);
            Assert.Equal(12, u.Arrows);
            RunPack.SpendArrow(u);
            RunPack.SpendArrow(u);
            Assert.Equal(10, u.Arrows);
            Assert.DoesNotContain(u.Pack!.Items, (p) => p.Gear.Id == "gb");
        }

        [Fact]
        public void RareInARunsBoxesNeverInTheCampaignOfBefore()
        {
            const int N = 20000;
            var run = Enumerable.Range(1, N).Select((seed) => Looting.RollBox(DecorKinds.LootWeapons, new Rng(seed), true, true)).ToList();
            var bows = run.Count((l) => l?.Weapon == FoundWeaponIds.Bow || l?.Weapon == FoundWeaponIds.Crossbow);
            var scoped = run.Count((l) => l?.Weapon == FoundWeaponIds.ScopedRifle);
            Assert.InRange(bows, scoped, scoped * 3);
            Assert.InRange(bows / (double)N, 0.01, 0.04);
            var before = Enumerable.Range(1, N).Select((seed) => Looting.RollBox(DecorKinds.LootWeapons, new Rng(seed), true, false)).ToList();
            Assert.DoesNotContain(before, (l) => l?.Weapon == FoundWeaponIds.Bow || l?.Weapon == FoundWeaponIds.Crossbow);
            // An ammunition box: a bundle of arrows one time in four, in a run only.
            var ammo = Enumerable.Range(1, N).Select((seed) => Looting.RollBox(DecorKinds.LootAmmo, new Rng(seed), true, true)).ToList();
            Assert.InRange(ammo.Count((l) => l!.Arrows == Looting.BoxArrows) / (double)N, 0.22, 0.28);
            Assert.All(ammo, (l) => Assert.True(l!.Arrows != null ^ l.Mags != null));
            Assert.DoesNotContain(Enumerable.Range(1, 200).Select((seed) => Looting.RollBox(DecorKinds.LootAmmo, new Rng(seed), true, false)), (l) => l!.Arrows != null);
        }

        [Fact]
        public void TheTruequeSellsArrowsByTheBundleAndTheTwoWeapons()
        {
            var arrows = Content.TradeWares.Single((w) => w.Kind == GearKinds.Arrows);
            Assert.Equal((Looting.BoxArrows, 15), (arrows.Count, arrows.Price));
            Assert.Contains(Content.TradeWares, (w) => w.Type == FoundWeaponIds.Crossbow);
            Assert.Contains(Content.TradeWares, (w) => w.Type == FoundWeaponIds.Bow);
            Assert.All(Content.TradeWares.Where((w) => w.Kind != GearKinds.Arrows), (w) => Assert.Equal(1, w.Count));
        }

        [Fact]
        public void OneCazadorFewerInAGuardedRescate()
        {
            var pods = new List<List<string>> { new List<string> { TemplateIds.Trooper, TemplateIds.Officer }, new List<string> { TemplateIds.Trooper, TemplateIds.Lancer }, new List<string> { TemplateIds.Lancer, TemplateIds.Trooper } };
            Runs.OneCazadorFewer(pods);
            Assert.Equal(new[] { 2, 1, 2 }, pods.Select((p) => p.Count));
            Assert.Equal(1, pods.Sum((p) => p.Count((t) => t == TemplateIds.Lancer)));
            Runs.OneCazadorFewer(new List<List<string>> { new List<string> { TemplateIds.Zombie } });
            // On a guarded Rescate at difficulty 2: its groups' Cazadores but one, and the garrison's.
            RunNode? node = null;
            var (_, s) = RunsHelpers.LaunchAt(NodeKinds.Rescue, 2, node: (n) =>
            {
                n.Guarded = true;
                n.Difficulty = 2;
                node = n;
            });
            var plan = Setup.PodsFor(RunMap.MapOf(node!)!, Setup.MissionPods(2, MissionKinds.Rescue, true));
            var planned = plan.Sum((p) => p.Count((t) => t == TemplateIds.Lancer));
            var garrison = Setup.Garrison(MissionKinds.Rescue, 2, true)!.Count((t) => t == TemplateIds.Lancer);
            Assert.True(planned >= 1);
            Assert.Equal(planned - 1 + garrison, s.Units.Count((u) => u.Template == TemplateIds.Lancer));
        }
    }
}
