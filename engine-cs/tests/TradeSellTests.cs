using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// M10 (Arturo, cuadro 141 of 23/11; gameplay/notas-23-11/TRUEQUE-VENDER.md): at a Trade, what a soldier carries (in its
    /// pack or worn) sells for half its list price (20 % more with «Regateo»), never the §, the knife nor what a mission
    /// gives; the merchant with no cap; what is sold gone; its § into the seller's pack.
    /// </summary>
    public class TradeSellTests
    {
        private static Gear? InPacks(CampaignSoldier s, string kind) => s.Kit!.Pack.Items.Select((p) => p.Gear).FirstOrDefault((g) => g.Kind == kind);

        [Fact(DisplayName = "vender › from the pack: half the list price, into the seller's pack; the log says so; the wares as they were")]
        public void FromThePackHalfTheListPrice()
        {
            var c = Solo();
            GoTo(c, NodeKinds.Trade);
            var s = Squad(c)[0];
            var rifle = InPack(c, s, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, 0);
            var wares = c.Run!.Stop!.Wares!.Select((w) => w.Gear.Id).ToList();
            Ok(c, new SellGearCommand { Soldier = s.Id, Gear = rifle.Id });
            Assert.DoesNotContain(s.Kit!.Pack.Items, (p) => p.Gear == rifle);
            Assert.Equal(45, Gears.Supplies(s.Kit.Pack));
            Assert.Contains(c.Log, (e) => e.Text == RunTexts.Sold(s.Name, Lore.GearA(GearKinds.Weapon, FoundWeaponIds.AssaultRifle), 45));
            // Gone: not on sale after.
            Assert.Equal(wares, c.Run.Stop.Wares!.Select((w) => w.Gear.Id).ToList());
            Assert.Null(Gears.Find(c, rifle.Id));
        }

        [Fact(DisplayName = "vender › what is worn sells too: a vest with the damage kept, a rifle leaving the knife")]
        public void WhatIsWornSellsToo()
        {
            var c = Solo();
            GoTo(c, NodeKinds.Trade);
            var s = Squad(c)[0];
            var vest = InSlot(c, s, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
            var max = Runs.MaxHp(s);
            c.Run!.Hp[s.Id] = max - 1;
            Ok(c, new SellGearCommand { Soldier = s.Id, Gear = vest.Id });
            Assert.Null(s.Kit!.Vest);
            Assert.Equal(30, Gears.Supplies(s.Kit.Pack));
            // One down still: the vest's health gone with it, the wound kept.
            Assert.Equal((max - Content.Vests[VestIds.Vest].Hp, Runs.MaxHp(s) - 1), (Runs.MaxHp(s), c.Run.Hp[s.Id]));
            var rifle = InSlot(c, s, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            Ok(c, new SellGearCommand { Soldier = s.Id, Gear = rifle.Id });
            Assert.Null(s.Kit.Primary);
            // A soldier with the knife alone, as a recruit: it goes out as one.
            var member = Runs.SquadMemberOf(c, s);
            Assert.Null(member.Arms!.Primary);
            Assert.False(member.Arms.Pistol);
        }

        [Fact(DisplayName = "vender › never the §, the knife nor what a mission gives; a pile worth nothing neither")]
        public void NeverTheseThings()
        {
            var c = Solo();
            GoTo(c, NodeKinds.Trade);
            var s = Squad(c)[0];
            var supplies = InPack(c, s, GearKinds.Supplies, null, 0, 0, 50);
            var key = InPack(c, s, GearKinds.Key, null, 1, 0);
            var arrow = InPack(c, s, GearKinds.Arrows, null, 2, 0, 1);
            foreach (var g in new[] { supplies, key, arrow, s.Kit!.Melee! })
                Refused(RunTexts.NotForSale, c, new SellGearCommand { Soldier = s.Id, Gear = g.Id });
            Assert.Equal(FoundWeaponIds.Knife, s.Kit.Melee!.Type);
        }

        [Fact(DisplayName = "vender › the table: a weapon with its accessories on top, the piles whole, down to a 5")]
        public void ThePrices()
        {
            Gear G(string kind, string? type, int count = 1, params string[] mods) => new Gear { Kind = kind, Type = type, Count = count, Mods = mods.Length > 0 ? mods.ToList() : null };
            Assert.Equal(45, Content.SellPrice(G(GearKinds.Weapon, FoundWeaponIds.AssaultRifle), false));
            Assert.Equal(60, Content.SellPrice(G(GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 1, WeaponModIds.Scope), false));
            // One that comes with its accessory: the same as the rifle with it.
            Assert.Equal(60, Content.SellPrice(G(GearKinds.Weapon, FoundWeaponIds.ScopedRifle, 1, WeaponModIds.Scope), false));
            Assert.Equal(60, Content.SellPrice(G(GearKinds.Weapon, FoundWeaponIds.PrecisionRifle), false));
            Assert.Equal(10, Content.SellPrice(G(GearKinds.Weapon, FoundWeaponIds.Bat), false));
            Assert.Equal(15, Content.SellPrice(G(GearKinds.Mod, WeaponModIds.Silencer), false));
            Assert.Equal(50, Content.SellPrice(G(GearKinds.Vest, VestIds.HeavyVest), false));
            Assert.Equal(5, Content.SellPrice(G(GearKinds.Mags, null), false));
            Assert.Equal(20, Content.SellPrice(G(GearKinds.Mags, null, 3), false));
            // Arrows as the Trade's bundle of Looting.BoxArrows (15 §): half, all the pile, down to a 5.
            Assert.Equal(5, Content.SellPrice(G(GearKinds.Arrows, null, Looting.BoxArrows), false));
            Assert.Equal(15, Content.SellPrice(G(GearKinds.Arrows, null, 2 * Looting.BoxArrows), false));
            Assert.Equal(0, Content.SellPrice(G(GearKinds.Arrows, null, 1), false));
            Assert.Equal(5, Content.SellPrice(G(GearKinds.Item, ItemIds.Molotov), false));
            Assert.Equal(0, Content.SellPrice(G(GearKinds.Supplies, null, 40), false));
            Assert.Equal(0, Content.SellPrice(G(GearKinds.Weapon, FoundWeaponIds.Knife), false));
            // Regateo: 20 % more, rounded.
            Assert.Equal(54, Content.SellPrice(G(GearKinds.Weapon, FoundWeaponIds.AssaultRifle), true));
            Assert.Equal(6, Content.SellPrice(G(GearKinds.Mags, null), true));
        }

        [Fact(DisplayName = "vender › with «Regateo» the Trade pays 20 % more")]
        public void WithTheHaggleUpgrade()
        {
            var c = Solo();
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Haggle] = 1;
            GoTo(c, NodeKinds.Trade);
            var s = Squad(c)[0];
            var rifle = InPack(c, s, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, 0);
            Ok(c, new SellGearCommand { Soldier = s.Id, Gear = rifle.Id });
            Assert.Equal(54, Gears.Supplies(s.Kit!.Pack));
        }

        [Fact(DisplayName = "vender › no cap: sell all one carries, and buy with it at the same Trade")]
        public void NoCapAndBuyAfter()
        {
            var c = Solo();
            GoTo(c, NodeKinds.Trade);
            var s = Squad(c)[0];
            var guns = new[] { 0, 1, 2 }.Select((y) => InPack(c, s, GearKinds.Weapon, FoundWeaponIds.PrecisionRifle, 0, y)).ToList();
            foreach (var g in guns) Ok(c, new SellGearCommand { Soldier = s.Id, Gear = g.Id });
            Assert.Equal(180, Gears.Supplies(s.Kit!.Pack));
            var ware = c.Run!.Stop!.Wares![0];
            ware.Price = 100;
            Ok(c, new BuyWareCommand { Ware = ware.Gear.Id, Soldier = s.Id });
            Assert.Equal(80, Gears.Supplies(s.Kit.Pack));
        }

        [Fact(DisplayName = "vender › at two, each sells their own soldiers'; only at a Trade")]
        public void EachTheirOwnOnlyAtATrade()
        {
            var c = Two();
            GoTo(c, NodeKinds.Trade);
            var mine = Squad(c).First((s) => s.Owner == 0);
            var partner = Squad(c).First((s) => s.Owner == 1);
            var theirs = InPack(c, partner, GearKinds.Weapon, FoundWeaponIds.Pistol, 0, 0);
            Refused(RunTexts.NotYours, c, new SellGearCommand { Soldier = partner.Id, Gear = theirs.Id }, 0, Both);
            var pistol = InPack(c, mine, GearKinds.Weapon, FoundWeaponIds.Pistol, 0, 0);
            Ok(c, new SellGearCommand { Soldier = mine.Id, Gear = pistol.Id }, 0, Both);
            Assert.Equal((20, 0), (Gears.Supplies(mine.Kit!.Pack), Gears.Supplies(partner.Kit!.Pack)));
            var d = Solo();
            GoTo(d, NodeKinds.Refuge);
            var s = Squad(d)[0];
            var gun = InPack(d, s, GearKinds.Weapon, FoundWeaponIds.Pistol, 0, 0);
            Refused(RunTexts.NotTrade, d, new SellGearCommand { Soldier = s.Id, Gear = gun.Id });
        }

        [Fact(DisplayName = "vender › worn, with no room in the pack: the § to a comrade's of the same player; none, refused")]
        public void TheSuppliesNeedRoom()
        {
            var c = Solo();
            GoTo(c, NodeKinds.Trade);
            var squad = Squad(c);
            var s = squad[0];
            for (var y = 0; y < s.Kit!.Pack.H; y++) InPack(c, s, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, y);
            var vest = InSlot(c, s, KitSlots.Vest, GearKinds.Vest, VestIds.Vest);
            Ok(c, new SellGearCommand { Soldier = s.Id, Gear = vest.Id });
            Assert.Equal((0, 30), (Gears.Supplies(s.Kit.Pack), squad.Skip(1).Sum((x) => Gears.Supplies(x.Kit!.Pack))));
            foreach (var o in squad.Skip(1))
            {
                o.Kit!.Pack.Items.Clear();
                for (var y = 0; y < o.Kit.Pack.H; y++) InPack(c, o, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, y);
            }
            var rifle = InSlot(c, s, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            Refused(RunTexts.SuppliesNoRoom(s.Name), c, new SellGearCommand { Soldier = s.Id, Gear = rifle.Id });
            // From the pack it always fits: its own cells.
            Ok(c, new SellGearCommand { Soldier = s.Id, Gear = s.Kit.Pack.Items[0].Gear.Id });
            Assert.Equal(45, Gears.Supplies(s.Kit.Pack));
        }

        [Fact(DisplayName = "vender › past 100 §, in piles of 100 as a reward's: they need the room of each")]
        public void PastAHundredInPiles()
        {
            var c = Solo();
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Haggle] = 1;
            GoTo(c, NodeKinds.Trade);
            var squad = Squad(c);
            var s = squad[0];
            // A precision rifle with two accessories, with «Buen trato»: (60 + 30) × 1.2 = 108 (Producto's review, 24/11).
            var rifle = InSlot(c, s, KitSlots.Primary, GearKinds.Weapon, FoundWeaponIds.PrecisionRifle);
            rifle.Mods = new List<string> { WeaponModIds.Scope, WeaponModIds.Silencer };
            Assert.Equal(108, Content.SellPrice(rifle, true));
            // One cell free in the seller's pack, the rest of the squad's full: the 8 past the 100 have nowhere.
            s.Kit!.Pack.Items.Clear();
            for (var y = 0; y < s.Kit.Pack.H - 1; y++) InPack(c, s, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, y);
            InPack(c, s, GearKinds.Weapon, FoundWeaponIds.Bat, 0, s.Kit.Pack.H - 1);
            foreach (var o in squad.Skip(1))
            {
                o.Kit!.Pack.Items.Clear();
                for (var y = 0; y < o.Kit.Pack.H; y++) InPack(c, o, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, y);
            }
            Refused(RunTexts.SuppliesNoRoom(s.Name), c, new SellGearCommand { Soldier = s.Id, Gear = rifle.Id });
            // Room in a comrade's: two piles there, 100 and 8.
            squad[1].Kit!.Pack.Items.Clear();
            Ok(c, new SellGearCommand { Soldier = s.Id, Gear = rifle.Id });
            var piles = squad[1].Kit!.Pack.Items.Where((p) => p.Gear.Kind == GearKinds.Supplies).Select((p) => p.Gear.Count).OrderBy((n) => n).ToList();
            Assert.Equal(new List<int> { 8, 100 }, piles);
            Assert.True(squad.All((o) => o.Kit!.Pack.Items.All((p) => p.Gear.Count <= Content.PileOf(p.Gear.Kind, p.Gear.Type))));
        }
    }
}
