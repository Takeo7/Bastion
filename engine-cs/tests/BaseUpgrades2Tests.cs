using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The base's ten more upgrades (Arturo, cuadros 164 and 165), the next three (0.14.10): the Baúl de La Lata, the Taller
    /// and the Chatarrero.
    /// </summary>
    public class BaseUpgrades2Tests
    {
        private static void Up(CampaignState c, string id, int level) => (c.Upgrades ??= new Dictionary<string, int>())[id] = level;

        private static void Fill(CampaignState c, int supplies)
        {
            c.Stash!.Items.RemoveAll((p) => p.Gear.Kind == GearKinds.Supplies);
            for (var left = supplies; left > 0; left -= 100) Gears.Stow(c.Stash, Gears.NewGear(c, GearKinds.Supplies, null, System.Math.Min(100, left)));
        }

        [Fact(DisplayName = "mejoras › the three, their levels and prices (cuadro 165)")]
        public void ThePrices()
        {
            Assert.Equal(new[] { 120, 200 }, Content.UpgradePrices[UpgradeIds.Trunk]);
            Assert.Equal(new[] { 100, 200 }, Content.UpgradePrices[UpgradeIds.Workshop]);
            Assert.Equal(new[] { 120 }, Content.UpgradePrices[UpgradeIds.Scrap]);
            foreach (var id in new[] { UpgradeIds.Trunk, UpgradeIds.Workshop, UpgradeIds.Scrap })
            {
                Assert.False(string.IsNullOrEmpty(Lore.UpgradeNames[id]));
                for (var level = 1; level <= Content.UpgradePrices[id].Count; level++) Assert.False(string.IsNullOrEmpty(Lore.UpgradeLine(id, 2, 4, Runs.MendHeal, level)));
            }
        }

        [Fact(DisplayName = "mejoras › Baúl: 4×2 and 4×4, used between stages and at the stops, never in a fight")]
        public void TheTrunk()
        {
            var c = Solo();
            Start(c);
            Assert.Null(c.Run!.Trunk);
            Assert.False(Runs.TrunkOpen(c));
            foreach (var (level, rows) in new[] { (1, 2), (2, 4) })
            {
                var d = Solo();
                Up(d, UpgradeIds.Trunk, level);
                Start(d);
                Assert.Equal((4, rows), (d.Run!.Trunk!.W, d.Run.Trunk.H));
                Assert.True(Runs.TrunkOpen(d));
            }
            // Into it from a pack on the map, out of it to another's pack at a stop.
            var e = Solo();
            Up(e, UpgradeIds.Trunk, 1);
            Start(e);
            var squad = Squad(e);
            var rifle = InPack(e, squad[0], GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, 0);
            Ok(e, new MoveGearCommand { Gear = rifle.Id, To = GearPlaces.Trunk, X = 0, Y = 0 });
            Assert.Contains(e.Run!.Trunk!.Items, (p) => p.Gear == rifle);
            Assert.True(Gears.Find(e, rifle.Id)!.Trunk);
            GoTo(e, NodeKinds.Trade);
            squad[1].Kit!.Pack.Items.Clear();
            Ok(e, new MoveGearCommand { Gear = rifle.Id, To = squad[1].Id, X = 0, Y = 1 });
            Assert.Contains(squad[1].Kit!.Pack.Items, (p) => p.Gear == rifle);
            Assert.Empty(e.Run.Trunk.Items);
            // In a fight, no.
            Ok(e, new MoveGearCommand { Gear = rifle.Id, To = GearPlaces.Trunk, X = 0, Y = 0 });
            e.Run.Phase = RunPhases.Fight;
            Refused(RunTexts.InFight, e, new MoveGearCommand { Gear = rifle.Id, To = squad[0].Id, X = 0, Y = 0 });
            Assert.False(Runs.TrunkOpen(e));
            // Without one: no.
            var f = Solo();
            Start(f);
            var gun = InPack(f, Squad(f)[0], GearKinds.Weapon, FoundWeaponIds.Pistol, 0, 0);
            Refused(RunTexts.NoTrunk, f, new MoveGearCommand { Gear = gun.Id, To = GearPlaces.Trunk, X = 0, Y = 0 });
        }

        [Fact(DisplayName = "mejoras › Baúl: home, what it holds into the stash; the squad fallen, too (cuadro 165)")]
        public void TheTrunkComesHome()
        {
            var c = Solo();
            Up(c, UpgradeIds.Trunk, 1);
            Up(c, UpgradeIds.Route, 1);
            Start(c);
            var gun = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Pistol);
            c.Run!.Trunk!.Items.Add(new Placed { Gear = gun, X = 0, Y = 0 });
            GoTo(c, NodeKinds.Trade);
            Ok(c, new EndRunCommand());
            Assert.Contains(c.Stash!.Items, (p) => p.Gear.Id == gun.Id);
            Assert.Contains(c.Log, (e) => e.Text == RunTexts.TrunkHome(0));
            // The squad fallen: La Lata comes back with it all the same.
            var (d, s) = LaunchAt(NodeKinds.Normal, 1, (x) => Up(x, UpgradeIds.Trunk, 2));
            var rifle = Gears.NewGear(d, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            d.Run!.Trunk!.Items.Add(new Placed { Gear = rifle, X = 0, Y = 0 });
            foreach (var u in s.Units.Where((u) => u.Team == Teams.Xcom && u.CampaignId != null)) { u.Alive = false; u.Hp = 0; }
            var report = EndFight(d, s, Outcomes.Defeat, EndReasons.Wiped);
            Assert.Equal(RunEnds.Wiped, report.Run!.Ended);
            Assert.Contains(d.Stash!.Items, (p) => p.Gear.Id == rifle.Id);
        }

        /// <summary>A Trade reached with a Baúl (4×2), the first soldier of the squad and the rest.</summary>
        private static (CampaignState C, List<CampaignSoldier> Squad) AtATrade()
        {
            var c = Solo();
            Up(c, UpgradeIds.Trunk, 1);
            GoTo(c, NodeKinds.Trade);
            return (c, Squad(c));
        }

        private static void FullPack(CampaignState c, CampaignSoldier s)
        {
            s.Kit!.Pack.Items.Clear();
            for (var y = 0; y < s.Kit.Pack.H; y++) InPack(c, s, GearKinds.Weapon, FoundWeaponIds.AssaultRifle, 0, y);
        }

        [Fact(DisplayName = "mejoras › Trueque y baúl (cuadro 167): what is in the trunk is offered too, its § to the packs, else the trunk")]
        public void OfferFromTheTrunk()
        {
            var (c, squad) = AtATrade();
            var s = squad[0];
            s.Kit!.Pack.Items.Clear();
            var rifle = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            c.Run!.Trunk!.Items.Add(new Placed { Gear = rifle, X = 0, Y = 0 });
            Ok(c, new SellGearCommand { Soldier = s.Id, Gear = rifle.Id });
            Assert.Empty(c.Run.Trunk.Items);
            Assert.Equal(45, Gears.Supplies(s.Kit.Pack));
            // What the base gave, never, wherever it goes.
            var given = Gears.NewGear(c, GearKinds.Item, ItemIds.FirstAid);
            given.Issued = true;
            c.Run.Trunk.Items.Add(new Placed { Gear = given, X = 0, Y = 0 });
            Refused(RunTexts.NotForSale, c, new SellGearCommand { Soldier = s.Id, Gear = given.Id });
            c.Run.Trunk.Items.Clear();
            // Every pack full: the § into the trunk, where it was.
            foreach (var o in squad) FullPack(c, o);
            var gun = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Pistol);
            c.Run.Trunk.Items.Add(new Placed { Gear = gun, X = 0, Y = 0 });
            Ok(c, new SellGearCommand { Soldier = s.Id, Gear = gun.Id });
            Assert.Equal(20, Gears.Supplies(c.Run.Trunk));
            // The trunk full too: refused, said of the trunk as well (Lore's line).
            c.Run.Trunk.Items.Clear();
            for (var y = 0; y < c.Run.Trunk.H; y++) c.Run.Trunk.Items.Add(new Placed { Gear = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle), X = 0, Y = y });
            var nade = Gears.NewGear(c, GearKinds.Item, ItemIds.FragGrenade);
            s.Kit.Belt[0] = nade;
            Refused(RunTexts.SuppliesNoRoom(s.Name, trunk: true), c, new SellGearCommand { Soldier = s.Id, Gear = nade.Id });
        }

        [Fact(DisplayName = "mejoras › Trueque y baúl (cuadro 167): what is bought, into the buyer's pack, else the trunk, never a partner's")]
        public void BuyIntoTheTrunk()
        {
            var (c, squad) = AtATrade();
            var buyer = squad[0];
            var other = squad[1];
            FullPack(c, buyer);
            other.Kit!.Pack.Items.Clear();
            InPack(c, other, GearKinds.Supplies, null, 3, 0, 100);
            var ware = c.Run!.Stop!.Wares![0];
            ware.Price = 10;
            var cmd = new BuyWareCommand { Ware = ware.Gear.Id, Soldier = buyer.Id };
            Assert.Equal(GearPlaces.Trunk, Gears.BuyTo(c, 0, new[] { 0 }, cmd));
            Ok(c, cmd);
            Assert.Contains(c.Run.Trunk!.Items, (p) => p.Gear == ware.Gear);
            // The trunk full too: not into the partner's pack, with room as it has; refused, the trunk named.
            c.Run.Trunk.Items.Clear();
            for (var y = 0; y < c.Run.Trunk.H; y++) c.Run.Trunk.Items.Add(new Placed { Gear = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle), X = 0, Y = y });
            var next = c.Run.Stop.Wares![0];
            next.Price = 10;
            var second = new BuyWareCommand { Ware = next.Gear.Id, Soldier = buyer.Id };
            Assert.Null(Gears.BuyTo(c, 0, new[] { 0 }, second));
            Refused(RunTexts.NoRoomToBuy(buyer.Name, true), c, second);
            // Room in the buyer's pack: there, whatever the trunk.
            buyer.Kit!.Pack.Items.Clear();
            Assert.Equal(buyer.Id, Gears.BuyTo(c, 0, new[] { 0 }, second));
            Ok(c, second);
            Assert.Contains(buyer.Kit.Pack.Items, (p) => p.Gear == next.Gear);
        }

        [Fact(DisplayName = "mejoras › Trueque y baúl (cuadro 167): the trunk's § pay too, after the packs'")]
        public void TheTrunkPays()
        {
            var (c, squad) = AtATrade();
            foreach (var o in squad) o.Kit!.Pack.Items.Clear();
            var buyer = squad[0];
            InPack(c, buyer, GearKinds.Supplies, null, 3, 0, 5);
            c.Run!.Trunk!.Items.Add(new Placed { Gear = new Gear { Id = "t1", Kind = GearKinds.Supplies, Count = 30 }, X = 3, Y = 0 });
            var ware = c.Run.Stop!.Wares![0];
            // Short of all of it: what is missing counts the trunk.
            ware.Price = 50;
            var cmd = new BuyWareCommand { Ware = ware.Gear.Id, Soldier = buyer.Id };
            Refused(RunTexts.Missing(15), c, cmd);
            // The packs' first, then the trunk's.
            ware.Price = 10;
            Ok(c, cmd);
            Assert.Equal(0, Gears.Supplies(buyer.Kit!.Pack));
            Assert.Equal(25, Gears.Supplies(c.Run.Trunk));
            // Only the trunk's: paid all from there.
            var next = c.Run.Stop.Wares![0];
            next.Price = 20;
            Ok(c, new BuyWareCommand { Ware = next.Gear.Id, Soldier = buyer.Id });
            Assert.Equal(5, Gears.Supplies(c.Run.Trunk));
        }

        [Fact(DisplayName = "mejoras › Taller: at the base, at the Trade's price, into the stash; accessories at level 2")]
        public void TheWorkshop()
        {
            var c = Solo();
            Fill(c, 100);
            Refused(RunTexts.NoWorkshop, c, new WorkshopCommand { Ware = WorkshopIds.Mags });
            Up(c, UpgradeIds.Workshop, 1);
            Ok(c, new WorkshopCommand { Ware = WorkshopIds.Arrows });
            Assert.Equal(85, Gears.Supplies(c.Stash!));
            Assert.Contains(c.Stash!.Items, (p) => p.Gear.Kind == GearKinds.Arrows && p.Gear.Count == Looting.BoxArrows);
            Ok(c, new WorkshopCommand { Ware = WorkshopIds.Molotov });
            Assert.Equal(75, Gears.Supplies(c.Stash));
            Refused(RunTexts.WorkshopLevel, c, new WorkshopCommand { Ware = WorkshopIds.Scope });
            Up(c, UpgradeIds.Workshop, 2);
            Ok(c, new WorkshopCommand { Ware = WorkshopIds.Scope });
            Assert.Equal(30, Gears.Supplies(c.Stash));
            Assert.Contains(c.Stash.Items, (p) => p.Gear.Kind == GearKinds.Mod && p.Gear.Type == WeaponModIds.Scope);
            Refused(RunTexts.Missing(15), c, new WorkshopCommand { Ware = WorkshopIds.Silencer });
            Refused(RunTexts.NoWorkshopWare, c, new WorkshopCommand { Ware = "rocket" });
            // In a run, no.
            Start(c);
            Refused(RunTexts.WorkshopAtBase, c, new WorkshopCommand { Ware = WorkshopIds.Mags });
        }

        [Fact(DisplayName = "mejoras › Chatarrero: at the base, what is in the stash, at half what the Trade pays; never what isn't offered")]
        public void TheScrapDealer()
        {
            var c = Solo();
            Fill(c, 0);
            var rifle = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.AssaultRifle);
            Gears.Stow(c.Stash!, rifle);
            Refused(RunTexts.NoScrap, c, new ScrapGearCommand { Gear = rifle.Id });
            Up(c, UpgradeIds.Scrap, 1);
            Assert.Equal(45, Content.SellPrice(rifle, false));
            Assert.Equal(20, Content.ScrapPrice(rifle));
            Ok(c, new ScrapGearCommand { Gear = rifle.Id });
            Assert.Equal(20, Gears.Supplies(c.Stash!));
            Assert.Null(Gears.Find(c, rifle.Id));
            Assert.Contains(c.Log, (e) => e.Text == RunTexts.Scrapped(Lore.GearA(GearKinds.Weapon, FoundWeaponIds.AssaultRifle), 20));
            // Not what the base gave, nor a knife; only the stash's.
            var given = Gears.NewGear(c, GearKinds.Item, ItemIds.FirstAid);
            given.Issued = true;
            Gears.Stow(c.Stash!, given);
            Refused(RunTexts.NotForSale, c, new ScrapGearCommand { Gear = given.Id });
            var worn = c.Soldiers.First((x) => x.Alive);
            var pistol = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Pistol);
            worn.Kit!.Pack.Items.Clear();
            Assert.True(RunPack.Put(worn.Kit.Pack, pistol, 0, 0, false));
            Refused(RunTexts.ScrapStashOnly, c, new ScrapGearCommand { Gear = pistol.Id });
            // In a run, no.
            var other = Gears.NewGear(c, GearKinds.Weapon, FoundWeaponIds.Pistol);
            Gears.Stow(c.Stash!, other);
            Start(c);
            Refused(RunTexts.ScrapAtBase, c, new ScrapGearCommand { Gear = other.Id });
        }
    }
}
