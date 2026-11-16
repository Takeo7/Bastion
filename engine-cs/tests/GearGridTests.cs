using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The runs' grids (H0, Campaign/Gears.cs): a gear's cells, unturned and turned, what fits where, the first place free,
    /// piles that fill up, split and pay; and the gear's tables (ContentGear.cs): sizes, piles, which kit place takes what.
    /// </summary>
    public class GearGridTests
    {
        private static Gear G(string kind, string? type = null, int count = 1, string id = "")
        {
            return new Gear { Id = id, Kind = kind, Type = type, Count = count };
        }

        private static Gear Rifle() => G(GearKinds.Weapon, FoundWeaponIds.AssaultRifle, id: "rifle");
        private static Gear Vest() => G(GearKinds.Vest, VestIds.Vest, id: "vest");
        private static Gear Med(int n = 1) => G(GearKinds.Medicine, null, n, "med");

        // ------------------------------------------------------------- the tables

        [Fact]
        public void SizesLieWide()
        {
            Assert.Equal((4, 1), Content.GearSize(GearKinds.Weapon, FoundWeaponIds.AssaultRifle));
            Assert.Equal((4, 1), Content.GearSize(GearKinds.Weapon, FoundWeaponIds.PumpShotgun));
            Assert.Equal((4, 1), Content.GearSize(GearKinds.Weapon, FoundWeaponIds.ScopedRifle));
            Assert.Equal((4, 2), Content.GearSize(GearKinds.Weapon, FoundWeaponIds.LightMachineGun));
            Assert.Equal((3, 1), Content.GearSize(GearKinds.Weapon, FoundWeaponIds.Katana));
            Assert.Equal((3, 1), Content.GearSize(GearKinds.Weapon, FoundWeaponIds.Chainsaw));
            Assert.Equal((2, 1), Content.GearSize(GearKinds.Weapon, FoundWeaponIds.Knife));
            Assert.Equal((2, 1), Content.GearSize(GearKinds.Weapon, FoundWeaponIds.SawnOff));
            Assert.Equal((2, 1), Content.GearSize(GearKinds.Weapon, FoundWeaponIds.Pistol));
            Assert.Equal((2, 2), Content.GearSize(GearKinds.Vest, VestIds.HeavyVest));
            Assert.Equal((1, 1), Content.GearSize(GearKinds.Item, ItemIds.Molotov));
            Assert.Equal((1, 1), Content.GearSize(GearKinds.Supplies, null));
            // Every weapon has its size.
            Assert.All(FoundWeaponIds.All, (id) => Assert.NotEqual((1, 1), Content.GearSize(GearKinds.Weapon, id)));
            // Turned, they swap.
            Assert.Equal((1, 4), Gears.Footprint(Rifle(), true));
            Assert.Equal((2, 4), Gears.Footprint(G(GearKinds.Weapon, FoundWeaponIds.MachineGun), true));
        }

        [Fact]
        public void PilesByKind()
        {
            Assert.Equal(100, Content.PileOf(GearKinds.Supplies, null));
            Assert.Equal(5, Content.PileOf(GearKinds.Medicine, null));
            Assert.Equal(5, Content.PileOf(GearKinds.Mags, null));
            Assert.Equal(5, Content.PileOf(GearKinds.Item, ItemIds.FragGrenade));
            Assert.Equal(3, Content.PileOf(GearKinds.Mod, WeaponModIds.Scope));
            Assert.Equal(1, Content.PileOf(GearKinds.Weapon, FoundWeaponIds.Pistol));
            Assert.Equal(1, Content.PileOf(GearKinds.Vest, VestIds.Vest));
        }

        [Fact]
        public void KitPlacesTakeTheirOwn()
        {
            var knife = G(GearKinds.Weapon, FoundWeaponIds.Knife);
            var pistol = G(GearKinds.Weapon, FoundWeaponIds.Pistol);
            var sawnOff = G(GearKinds.Weapon, FoundWeaponIds.SawnOff);
            var grenade = G(GearKinds.Item, ItemIds.FragGrenade);
            Assert.True(Content.SlotTakes(KitSlots.Primary, Rifle()));
            Assert.True(Content.SlotTakes(KitSlots.Primary, sawnOff));
            Assert.False(Content.SlotTakes(KitSlots.Primary, knife));
            Assert.False(Content.SlotTakes(KitSlots.Primary, pistol));
            Assert.True(Content.SlotTakes(KitSlots.Melee, knife));
            Assert.True(Content.SlotTakes(KitSlots.Melee, G(GearKinds.Weapon, FoundWeaponIds.Machete)));
            Assert.False(Content.SlotTakes(KitSlots.Melee, Rifle()));
            Assert.True(Content.SlotTakes(KitSlots.Pistol, pistol));
            Assert.False(Content.SlotTakes(KitSlots.Pistol, knife));
            Assert.True(Content.SlotTakes(KitSlots.Vest, Vest()));
            Assert.False(Content.SlotTakes(KitSlots.Vest, Med()));
            Assert.True(Content.SlotTakes(KitSlots.Belt0, grenade));
            Assert.True(Content.SlotTakes(KitSlots.Belt1, G(GearKinds.Item, ItemIds.FirstAid)));
            Assert.False(Content.SlotTakes(KitSlots.Belt0, Med()));
            Assert.True(Content.SlotTakes(KitSlots.Pack, Rifle()));
            Assert.True(Content.SlotTakes(KitSlots.Pack, Med()));
            Assert.False(Content.SlotTakes("hat", Med()));
            // The belt of a run: no vest, earplugs nor special rounds of before.
            Assert.False(Content.IsGear(GearKinds.Item, ItemIds.NanoVest));
            Assert.False(Content.IsGear(GearKinds.Item, ItemIds.MindShield));
            Assert.False(Content.SlotTakes(KitSlots.Belt0, G(GearKinds.Item, ItemIds.ApRounds)));
            Assert.False(Content.SlotTakes(KitSlots.Pack, G(GearKinds.Item, ItemIds.TracerRounds)));
            Assert.True(Content.IsGear(GearKinds.Supplies, null));
            Assert.False(Content.IsGear(GearKinds.Supplies, "gold"));
            // Mods go on the firearms for the hands only.
            Assert.True(Content.TakesMods(Rifle()));
            Assert.False(Content.TakesMods(pistol));
            Assert.False(Content.TakesMods(knife));
        }

        [Fact]
        public void TheTradeAndTheHaggle()
        {
            Assert.Equal(40, Content.TradeWares.First((w) => w.Type == FoundWeaponIds.Pistol).Price);
            Assert.Equal(120, Content.TradeWares.First((w) => w.Type == FoundWeaponIds.PrecisionRifle).Price);
            Assert.Equal(15, Content.TradeWares.First((w) => w.Kind == GearKinds.Medicine).Price);
            Assert.All(Content.TradeWares, (w) => Assert.True(Content.IsGear(w.Kind, w.Type), $"{w.Kind} {w.Type}"));
            Assert.All(Content.TradeWares.Where((w) => w.Kind == GearKinds.Weapon && Content.GearWeapon(G(w.Kind, w.Type))!.Slot == FoundWeaponSlots.Melee), (w) => Assert.InRange(w.Price, 20, 40));
            Assert.Equal(40, Content.Haggled(40, false));
            Assert.Equal(32, Content.Haggled(40, true));
            Assert.Equal(12, Content.Haggled(15, true));
            Assert.Equal(new[] { 150, 300 }, Content.UpgradePrices[UpgradeIds.Squad]);
            Assert.Equal(new[] { 80, 160, 240 }, Content.UpgradePrices[UpgradeIds.Stash]);
            Assert.All(UpgradeIds.All, (id) => Assert.True(Content.UpgradePrices.ContainsKey(id)));
        }

        // -------------------------------------------------------------- the grids

        [Fact]
        public void FitsInsideAndOnNothingElse()
        {
            var grid = new GearGrid { W = 10, H = 6 };
            var rifle = Rifle();
            Assert.True(Gears.Fits(grid, rifle, 6, 0, false));
            Assert.False(Gears.Fits(grid, rifle, 7, 0, false));
            Assert.True(Gears.Fits(grid, rifle, 0, 5, false));
            Assert.False(Gears.Fits(grid, rifle, 0, 6, false));
            Assert.False(Gears.Fits(grid, rifle, -1, 0, false));
            // Turned, it stands four high.
            Assert.True(Gears.Fits(grid, rifle, 9, 2, true));
            Assert.False(Gears.Fits(grid, rifle, 9, 3, true));
            var vest = Vest();
            Assert.NotNull(Gears.Place(grid, vest, 0, 0, false));
            Assert.False(Gears.Fits(grid, rifle, 1, 1, false));
            Assert.True(Gears.Fits(grid, rifle, 2, 0, false));
            Assert.False(Gears.Fits(grid, rifle, 1, 0, true));
            Assert.True(Gears.Fits(grid, rifle, 0, 2, true));
            // A gear doesn't block itself, nor one about to leave.
            Assert.True(Gears.Fits(grid, vest, 0, 0, false));
            Assert.True(Gears.Fits(grid, vest, 1, 1, false));
            Assert.True(Gears.Fits(grid, rifle, 1, 1, false, new[] { vest }));
            Assert.Null(Gears.Place(grid, rifle, 1, 1, false));
            Assert.Single(grid.Items);
        }

        [Fact]
        public void AtFindsTheGearOnACell()
        {
            var grid = new GearGrid { W = 4, H = 5 };
            var rifle = Rifle();
            Gears.Place(grid, rifle, 1, 0, true);
            Assert.Same(rifle, Gears.At(grid, 1, 3)!.Gear);
            Assert.Null(Gears.At(grid, 2, 0));
            Assert.Null(Gears.At(grid, 1, 4));
        }

        [Fact]
        public void FirstFitRowByRowThenTurned()
        {
            var pack = new GearGrid { W = 4, H = 3 };
            var at = Gears.FirstFit(pack, Rifle())!;
            Assert.Equal((0, 0, false), (at.X, at.Y, at.Turned));
            Gears.Place(pack, Med(), 0, 0, false);
            at = Gears.FirstFit(pack, Rifle())!;
            Assert.Equal((0, 1, false), (at.X, at.Y, at.Turned));
            at = Gears.FirstFit(pack, G(GearKinds.Weapon, FoundWeaponIds.Knife))!;
            Assert.Equal((1, 0, false), (at.X, at.Y, at.Turned));
            // Too wide lying down: stood up.
            var narrow = new GearGrid { W = 2, H = 4 };
            at = Gears.FirstFit(narrow, Rifle())!;
            Assert.Equal((0, 0, true), (at.X, at.Y, at.Turned));
            Assert.Empty(narrow.Items);
            // Nowhere.
            Assert.Null(Gears.FirstFit(new GearGrid { W = 3, H = 3 }, Rifle()));
            at = Gears.FirstFit(new GearGrid { W = 3, H = 4 }, G(GearKinds.Weapon, FoundWeaponIds.MachineGun))!;
            Assert.Equal((0, 0, true), (at.X, at.Y, at.Turned));
            var full = new GearGrid { W = 2, H = 1 };
            Gears.Place(full, Med(), 0, 0, false);
            Assert.Null(Gears.FirstFit(full, G(GearKinds.Weapon, FoundWeaponIds.Knife)));
            Assert.NotNull(Gears.FirstFit(full, G(GearKinds.Weapon, FoundWeaponIds.Knife), new[] { full.Items[0].Gear }));
        }

        [Fact]
        public void RemoveTakesItOut()
        {
            var grid = new GearGrid { W = 4, H = 3 };
            var med = Med();
            Gears.Place(grid, med, 2, 2, false);
            Assert.Equal(2, Gears.Remove(grid, med)!.X);
            Assert.Empty(grid.Items);
            Assert.Null(Gears.Remove(grid, med));
        }

        [Fact]
        public void PilesOfTheSameFillUp()
        {
            var a = Med(4);
            var b = G(GearKinds.Medicine, null, 3, "b");
            Assert.True(Gears.SamePile(a, b));
            Assert.False(Gears.SamePile(a, a));
            Assert.False(Gears.SamePile(a, G(GearKinds.Mags)));
            Assert.False(Gears.SamePile(Rifle(), G(GearKinds.Weapon, FoundWeaponIds.AssaultRifle, id: "other")));
            Assert.False(Gears.SamePile(G(GearKinds.Item, ItemIds.Molotov), G(GearKinds.Item, ItemIds.FragGrenade)));
            Assert.Equal(1, Gears.Merge(a, b));
            Assert.Equal((5, 2), (a.Count, b.Count));
            Assert.Equal(0, Gears.Merge(a, b));
            var c = G(GearKinds.Supplies, null, 10, "c");
            var d = G(GearKinds.Supplies, null, 50, "d");
            Assert.Equal(20, Gears.Merge(c, d, 20));
            Assert.Equal((30, 30), (c.Count, d.Count));
        }

        [Fact]
        public void StowFillsPilesThenFindsACell()
        {
            var grid = new GearGrid { W = 2, H = 1 };
            var pile = G(GearKinds.Supplies, null, 90, "s1");
            Gears.Place(grid, pile, 0, 0, false);
            var more = G(GearKinds.Supplies, null, 30, "s2");
            Assert.True(Gears.CanStow(grid, more));
            Assert.Equal(0, Gears.Stow(grid, more));
            Assert.Equal(100, pile.Count);
            Assert.Equal(20, more.Count);
            Assert.Equal((1, 0), (grid.Items[1].X, grid.Items[1].Y));
            // Full: what doesn't go stays in the gear.
            var med = Med(2);
            Assert.False(Gears.CanStow(grid, med));
            Assert.Equal(2, Gears.Stow(grid, med));
            Assert.Equal(2, grid.Items.Count);
            // Room in a pile is enough.
            var rest = G(GearKinds.Supplies, null, 5, "s3");
            Assert.True(Gears.CanStow(grid, rest));
            Assert.Equal(0, Gears.Stow(grid, rest));
            Assert.Equal(25, more.Count);
            Assert.True(Gears.CanStow(grid, med, new[] { pile }));
        }

        [Fact]
        public void SuppliesArePaidSmallestPileFirst()
        {
            var stash = new GearGrid { W = 4, H = 1 };
            var pack = new GearGrid { W = 4, H = 1 };
            var big = G(GearKinds.Supplies, null, 80, "big");
            var small = G(GearKinds.Supplies, null, 15, "small");
            var other = G(GearKinds.Supplies, null, 40, "other");
            Gears.Place(stash, big, 0, 0, false);
            Gears.Place(stash, small, 1, 0, false);
            Gears.Place(stash, Med(), 2, 0, false);
            Gears.Place(pack, other, 0, 0, false);
            Assert.Equal(95, Gears.Supplies(stash));
            var plan = Gears.SuppliesFor(new[] { stash, pack }, 100);
            Assert.Equal(new[] { ("small", 15), ("big", 80), ("other", 5) }, plan.Select((x) => (x.Pile.Gear.Id, x.Take)));
            Gears.PaySupplies(new[] { stash, pack }, 100);
            Assert.Equal(new[] { "med" }, stash.Items.Select((p) => p.Gear.Id));
            Assert.Equal(35, other.Count);
        }

        [Fact]
        public void SplitMakesANewGear()
        {
            var c = new CampaignState { NextId = 4 };
            var pile = Med(5);
            var part = Gears.Split(c, pile, 2);
            Assert.Equal(("g5", 2, 3), (part.Id, part.Count, pile.Count));
            Assert.Equal(GearKinds.Medicine, part.Kind);
        }
    }
}
