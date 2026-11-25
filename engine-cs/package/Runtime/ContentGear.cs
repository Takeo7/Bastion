#nullable enable
// The runs' tables (.dev-snapshots/gameplay/runs/H0-DISENO.md §2–3, PLAN-RUNS.md §3.3; new in C#): the gear's sizes and
// piles, which kit place takes what, the vests and the knife, the upgrades' prices, the Trade's price list and the
// events' rules. First numbers, to be tuned with the bots (H8). Names here are placeholders for Lore (RunTexts has the
// rest). The partial files' statics start in no set order: nothing here reads another part's in its initializer.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>A vest (Content.Vests): what it adds to the most health and to the armour.</summary>
    public sealed class VestDef
    {
        public string Id { get; set; } = ""; // VestIds
        public string Name { get; set; } = "";
        public int Hp { get; set; }
        public int Armor { get; set; }
    }

    /// <summary>A line of the Trade's price list (Content.TradeWares): a gear, its price in §, and how often it is on sale.</summary>
    public sealed class TradeWareDef
    {
        public string Kind { get; set; } = ""; // GearKinds
        public string? Type { get; set; }
        public int Price { get; set; }
        public int Weight { get; set; }
        /// <summary>How many of it make one ware (the arrows' bundle); absent, one.</summary>
        public int Count { get; set; } = 1;
    }

    /// <summary>
    /// An event of the runs (Content.RunEvents): its rules, the two options' consequences. Its texts are Lore's by the same
    /// id (Lore.RunEvents: title, text, each option's button and line).
    /// </summary>
    public sealed class RunEventDef
    {
        public string Id { get; set; } = "";
        public IReadOnlyList<RunEventOption> Options { get; set; } = new RunEventOption[0];
    }

    /// <summary>
    /// What an event's option does (Lore's rules, accepted 16/11): always the same, no dice in it, so that both vote knowing
    /// it. The figures are only here: Lore.RunEventEffects says them by the button.
    /// </summary>
    public sealed class RunEventOption
    {
        /// <summary>Supplies won (into the squad's packs) or, negative, paid (from them; refused if they don't hold it).</summary>
        public int Supplies { get; set; }
        /// <summary>Health lost by one of the squad, at random among those who have more than this.</summary>
        public int HurtOne { get; set; }
        /// <summary>Health lost by everyone of the squad, never below 1; and won, up to the most.</summary>
        public int HurtAll { get; set; }
        public int HealAll { get; set; }
        /// <summary>A gear won, into the pack of the one it fell to or of whoever has room.</summary>
        public string? GearKind { get; set; } // GearKinds
        public string? GearType { get; set; }
        public int GearCount { get; set; }
        /// <summary>What the next fight chosen takes, besides its own (Modifiers).</summary>
        public string? Modifier { get; set; } // Modifiers
        /// <summary>
        /// Someone joined the squad (a new soldier with a knife); with SurvivorHalf, at half health. None does since N2 (Arturo,
        /// 17/11: recruits only by a Rescate): kept false till UI's stop plate stops reading them.
        /// </summary>
        public bool Survivor { get; set; }
        public bool SurvivorHalf { get; set; }
        /// <summary>A fight of the next row becomes a Rescate, its survivor hidden alone (N2: «herido» and «radio»; Lore.RunEventEffects's rescue).</summary>
        public bool Rescue { get; set; }

        /// <summary>H0's, derived, while UI reads it (StrategyPresenter.Expedition): the health one loses, negative.</summary>
        public int Hp => -HurtOne;
        /// <summary>H0's, derived, while UI reads it: the medicines won.</summary>
        public int Medicine => GearKind == GearKinds.Medicine ? GearCount : 0;
    }

    public static partial class Content
    {
        // ------------------------------------------------------------- the runs' gear

        /// <summary>The knife every soldier of a run starts with (FoundWeaponIds.Knife): it fights as the default blade.</summary>
        public static readonly FoundWeaponDef Knife = new FoundWeaponDef { Id = FoundWeaponIds.Knife, Name = "Cuchillo", Find = "un cuchillo", Weapon = WeaponIds.Knife, Slot = FoundWeaponSlots.Melee, Better = false, Weight = 0 };

        /// <summary>The vests: armour is a thing to carry now (PLAN-RUNS §3.5).</summary>
        public static readonly IReadOnlyDictionary<string, VestDef> Vests = new Dictionary<string, VestDef>
        {
            [VestIds.Vest] = new VestDef { Id = VestIds.Vest, Name = "Chaleco antibalas", Hp = 2, Armor = 0 },
            [VestIds.HeavyVest] = new VestDef { Id = VestIds.HeavyVest, Name = "Chaleco con placas", Hp = 4, Armor = 1 },
        };

        /// <summary>What goes on the belt in a run (Gameplay, 16/11): the vest, the earplugs and the special rounds of before don't.</summary>
        public static readonly IReadOnlyList<string> BeltItems = new[] { ItemIds.FragGrenade, ItemIds.Molotov, ItemIds.SmokeGrenade, ItemIds.Flashbang, ItemIds.FirstAid };

        /// <summary>A weapon's size, width × height lying unturned: wide (Gameplay and UI, 16/11).</summary>
        private static readonly IReadOnlyDictionary<string, (int W, int H)> WeaponSizes = new Dictionary<string, (int W, int H)>
        {
            [FoundWeaponIds.AssaultRifle] = (4, 1),
            [FoundWeaponIds.PumpShotgun] = (4, 1),
            [FoundWeaponIds.PrecisionRifle] = (4, 1),
            [FoundWeaponIds.HuntingRifle] = (4, 1),
            [FoundWeaponIds.ScopedRifle] = (4, 1),
            [FoundWeaponIds.MachineGun] = (4, 2),
            [FoundWeaponIds.LightMachineGun] = (4, 2),
            [FoundWeaponIds.Pistol] = (2, 1),
            [FoundWeaponIds.SawnOff] = (2, 1),
            [FoundWeaponIds.Knife] = (2, 1),
            [FoundWeaponIds.Pan] = (2, 1),
            [FoundWeaponIds.Bat] = (3, 1),
            [FoundWeaponIds.Crowbar] = (3, 1),
            [FoundWeaponIds.Machete] = (3, 1),
            [FoundWeaponIds.Katana] = (3, 1),
            [FoundWeaponIds.Longsword] = (3, 1),
            [FoundWeaponIds.Rake] = (3, 1),
            [FoundWeaponIds.Stick] = (3, 1),
            [FoundWeaponIds.Chainsaw] = (3, 1),
            // Arco y ballesta (cuadro 101): the recurve as long as a rifle, the crossbow shorter.
            [FoundWeaponIds.Bow] = (4, 1),
            [FoundWeaponIds.Crossbow] = (3, 1),
        };

        /// <summary>Cells a gear takes lying unturned, width × height: weapons by WeaponSizes, vests 2×2, anything else 1×1.</summary>
        public static (int W, int H) GearSize(string kind, string? type)
        {
            if (kind == GearKinds.Weapon) return type != null && WeaponSizes.TryGetValue(type, out var size) ? size : (1, 1);
            if (kind == GearKinds.Vest) return (2, 2);
            return (1, 1);
        }

        /// <summary>How many of a gear one cell holds as a pile: supplies 100; medicines, mags and the belt's 5; mods 3; weapons and vests 1.</summary>
        public static int PileOf(string kind, string? type)
        {
            switch (kind)
            {
                case GearKinds.Supplies:
                    return 100;
                case GearKinds.Medicine:
                case GearKinds.Mags:
                case GearKinds.Item:
                    return 5;
                // Arco y ballesta (cuadro 102): 10 arrows a cell.
                case GearKinds.Arrows:
                    return ArrowsPile;
                case GearKinds.Mod:
                    return 3;
                default:
                    return 1;
            }
        }

        /// <summary>A gear the runs know: its kind, and a type of that kind (none for mags, medicine and supplies).</summary>
        public static bool IsGear(string? kind, string? type)
        {
            switch (kind)
            {
                case GearKinds.Weapon:
                    return type != null && (type == FoundWeaponIds.Knife || FoundWeapons.ContainsKey(type));
                case GearKinds.Item:
                    return type != null && BeltItems.Contains(type);
                case GearKinds.Mod:
                    return type != null && WeaponMods.ContainsKey(type);
                case GearKinds.Vest:
                    return type != null && Vests.ContainsKey(type);
                case GearKinds.Mags:
                case GearKinds.Medicine:
                case GearKinds.Supplies:
                case GearKinds.Key:
                case GearKinds.Antiviral:
                case GearKinds.Arrows:
                case GearKinds.Charge:
                    return type == null;
                default:
                    return false;
            }
        }

        /// <summary>A weapon gear's def: the Armería's, or the knife; null if it isn't a weapon.</summary>
        public static FoundWeaponDef? GearWeapon(Gear g)
        {
            if (g.Kind != GearKinds.Weapon || g.Type == null) return null;
            if (g.Type == FoundWeaponIds.Knife) return Knife;
            return FoundWeapons.TryGetValue(g.Type, out var def) ? def : null;
        }

        /// <summary>
        /// Which kit place takes a gear (KitSlots): primary, a firearm for the hands; melee, a blade (the knife too); pistol,
        /// the pistol; vest, a vest; belt0 and belt1, the belt's things (BeltItems); the pack, anything.
        /// </summary>
        public static bool SlotTakes(string slot, Gear g)
        {
            if (!IsGear(g.Kind, g.Type)) return false;
            var weapon = GearWeapon(g);
            switch (slot)
            {
                case KitSlots.Pack:
                    return true;
                case KitSlots.Primary:
                    return weapon != null && weapon.Slot == FoundWeaponSlots.Primary;
                case KitSlots.Melee:
                    return weapon != null && weapon.Slot == FoundWeaponSlots.Melee;
                case KitSlots.Pistol:
                    return weapon != null && weapon.Slot == FoundWeaponSlots.Sidearm;
                case KitSlots.Vest:
                    return g.Kind == GearKinds.Vest;
                case KitSlots.Belt0:
                case KitSlots.Belt1:
                    return g.Kind == GearKinds.Item;
                default:
                    return false;
            }
        }

        /// <summary>Whether mods go on a weapon gear: only the firearms for the hands, as in the Armería (Supplies.cs ModBlocker).</summary>
        public static bool TakesMods(Gear g)
        {
            // Arco y ballesta: nothing on them (ARCO-Y-BALLESTA.md: «sin mejoras»; not firearms, RunTexts.ModsOnGuns).
            return GearWeapon(g) is FoundWeaponDef d && d.Slot == FoundWeaponSlots.Primary && !Weapons[d.Weapon].Arrows;
        }

        // ------------------------------------------------------------ the upgrades

        /// <summary>Each upgrade's price in §, level by level (H0-DISENO §3): its count is how many levels it has.</summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<int>> UpgradePrices = new Dictionary<string, IReadOnlyList<int>>
        {
            // 2→4 cheap (N4, 17/11: without it no survivor of a Rescate fits with two players), then as before.
            [UpgradeIds.Squad] = new[] { 40, 150, 300 },
            [UpgradeIds.Stash] = new[] { 80, 160, 240 },
            [UpgradeIds.Pack] = new[] { 100, 200 },
            [UpgradeIds.Mend] = new[] { 120 },
            [UpgradeIds.Sight] = new[] { 80, 160 },
            [UpgradeIds.Haggle] = new[] { 100 },
            // The base's ten more (Arturo, cuadros 164 and 165): the first six (0.14.9).
            [UpgradeIds.Training] = new[] { 60, 120, 200 },
            [UpgradeIds.Route] = new[] { 100 },
            [UpgradeIds.Engine] = new[] { 80, 160 },
            [UpgradeIds.Garden] = new[] { 150, 250 },
            [UpgradeIds.Radio] = new[] { 120 },
            [UpgradeIds.Kit] = new[] { 100, 150 },
            // The next three (0.14.10).
            [UpgradeIds.Trunk] = new[] { 120, 200 },
            [UpgradeIds.Workshop] = new[] { 100, 200 },
            [UpgradeIds.Scrap] = new[] { 120 },
        };

        /// <summary>Baúl de La Lata: its rows by level, 4 wide (cuadro 165: 4×2, 4×4).</summary>
        public static readonly IReadOnlyList<int> TrunkRows = new[] { 2, 4 };

        /// <summary>
        /// Taller (cuadro 165): what it makes, at the Trade's price (Content.TradeWares), and from which level: magazines,
        /// arrows (a bundle, Looting.BoxArrows) and molotovs; at level 2, the four accessories at 45.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, WorkshopWareDef> WorkshopWares = new Dictionary<string, WorkshopWareDef>
        {
            [WorkshopIds.Mags] = new WorkshopWareDef { Kind = GearKinds.Mags, Type = null, Count = 1, Price = 15, Level = 1 },
            [WorkshopIds.Arrows] = new WorkshopWareDef { Kind = GearKinds.Arrows, Type = null, Count = Looting.BoxArrows, Price = 15, Level = 1 },
            [WorkshopIds.Molotov] = new WorkshopWareDef { Kind = GearKinds.Item, Type = ItemIds.Molotov, Count = 1, Price = 10, Level = 1 },
            [WorkshopIds.Scope] = new WorkshopWareDef { Kind = GearKinds.Mod, Type = WeaponModIds.Scope, Count = 1, Price = 45, Level = 2 },
            [WorkshopIds.Silencer] = new WorkshopWareDef { Kind = GearKinds.Mod, Type = WeaponModIds.Silencer, Count = 1, Price = 45, Level = 2 },
            [WorkshopIds.Stock] = new WorkshopWareDef { Kind = GearKinds.Mod, Type = WeaponModIds.Stock, Count = 1, Price = 45, Level = 2 },
            [WorkshopIds.ExtMag] = new WorkshopWareDef { Kind = GearKinds.Mod, Type = WeaponModIds.ExtMag, Count = 1, Price = 45, Level = 2 },
        };

        /// <summary>
        /// Chatarrero (cuadro 165): what it pays for a gear, half what a Trade does without «Buen trato» (SellPrice), down to
        /// a 5. 0: it doesn't take it (§, the knife, what a mission gives, what the base gave, a pile worth too little).
        /// </summary>
        public static int ScrapPrice(Gear g)
        {
            return (int)Math.Floor(SellPrice(g, false) / 2.0 / 5) * 5;
        }

        // ---------------------------------------------------------------- the Trade

        /// <summary>
        /// The Trade's price list (PLAN-RUNS §3.3, without the repair kit): what may be on sale, and how often. A melee
        /// weapon 20–40 by how good it is.
        /// </summary>
        public static readonly IReadOnlyList<TradeWareDef> TradeWares = new[]
        {
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Pistol, Price = 40, Weight = 3 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.AssaultRifle, Price = 90, Weight = 2 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.PumpShotgun, Price = 80, Weight = 2 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.PrecisionRifle, Price = 120, Weight = 1 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Stick, Price = 20, Weight = 1 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Pan, Price = 20, Weight = 1 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Bat, Price = 25, Weight = 1 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Rake, Price = 25, Weight = 1 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Crowbar, Price = 30, Weight = 1 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Machete, Price = 30, Weight = 1 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Longsword, Price = 35, Weight = 1 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Katana, Price = 40, Weight = 1 },
            new TradeWareDef { Kind = GearKinds.Item, Type = ItemIds.FirstAid, Price = 25, Weight = 3 },
            new TradeWareDef { Kind = GearKinds.Item, Type = ItemIds.FragGrenade, Price = 20, Weight = 3 },
            new TradeWareDef { Kind = GearKinds.Item, Type = ItemIds.Molotov, Price = 10, Weight = 3 },
            new TradeWareDef { Kind = GearKinds.Vest, Type = VestIds.Vest, Price = 60, Weight = 2 },
            new TradeWareDef { Kind = GearKinds.Mags, Type = null, Price = 15, Weight = 3 },
            new TradeWareDef { Kind = GearKinds.Medicine, Type = null, Price = 15, Weight = 3 },
            // Arco y ballesta (cuadro 105): the arrows as the magazines, a bundle as a box gives them (Looting.BoxArrows); the
            // two weapons as the good ones.
            new TradeWareDef { Kind = GearKinds.Arrows, Type = null, Price = 15, Weight = 3, Count = Looting.BoxArrows },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Crossbow, Price = 100, Weight = 1 },
            new TradeWareDef { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Bow, Price = 60, Weight = 1 },
        };

        /// <summary>Wares a Trade has.</summary>
        public const int TradeCount = 5;
        /// <summary>What the «haggle» upgrade takes off the Trade's prices, in percent.</summary>
        public const int HaggleDiscount = 20;

        /// <summary>
        /// M10 (cuadro 141): a list price for what the Trade never sells, as the G19 probe priced it (RunBench.Worth). A weapon
        /// that comes with an accessory mounted (FoundWeaponDef.Mod), without it: each accessory mounted sells on its own.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> ListPrices = new Dictionary<string, int>
        {
            [FoundWeaponIds.HuntingRifle] = 50,
            [FoundWeaponIds.SawnOff] = 30,
            [FoundWeaponIds.ScopedRifle] = 90,
            [FoundWeaponIds.MachineGun] = 120,
            [FoundWeaponIds.LightMachineGun] = 80,
            [FoundWeaponIds.Chainsaw] = 45,
            [ItemIds.SmokeGrenade] = 15,
            [ItemIds.Flashbang] = 20,
            [VestIds.HeavyVest] = 100,
            [GearKinds.Mod] = 30,
        };

        /// <summary>
        /// One of a gear's list price: the Trade's (an arrow, the bundle's share), else ListPrices'. 0 for what has none: §, the
        /// knife, what a mission gives.
        /// </summary>
        public static double ListPrice(string kind, string? type)
        {
            if (kind == GearKinds.Weapon && type == FoundWeaponIds.Knife) return 0;
            var ware = TradeWares.FirstOrDefault((w) => w.Kind == kind && w.Type == type);
            if (ware != null) return ware.Price / (double)Math.Max(1, ware.Count);
            if (kind == GearKinds.Mod) return ListPrices[GearKinds.Mod];
            return type != null && ListPrices.TryGetValue(type, out var p) ? p : 0;
        }

        /// <summary>What the «haggle» upgrade adds to what the Trade pays (cuadro 141), in percent.</summary>
        public const int HaggleSale = 20;

        /// <summary>
        /// What the Trade pays for a gear (M10, Arturo, cuadro 141: «a la mitad, y un 20 % más con Regateo»): half its list
        /// price, all of the pile, down to a 5; a weapon's accessories mounted on top, each as one; with the upgrade, 20 %
        /// more, rounded. 0: it isn't sold (§, the knife, what a mission gives, a pile worth less than 5).
        /// </summary>
        public static int SellPrice(Gear g, bool haggle)
        {
            // What the base gave (Botiquín de salida, cuadro 165): never.
            if (g.Issued == true) return 0;
            var half = ListPrice(g.Kind, g.Type) * g.Count / 2;
            var price = (int)Math.Floor(half / 5) * 5;
            if (g.Kind == GearKinds.Weapon && price > 0) price += (g.Mods?.Count ?? 0) * (int)Math.Floor(ListPrices[GearKinds.Mod] / 2.0 / 5) * 5;
            return haggle ? (int)Js.Round(price * (100 + HaggleSale) / 100.0) : price;
        }

        /// <summary>A price of the list as the Trade asks it: HaggleDiscount off, rounded, with the upgrade.</summary>
        public static int Haggled(int price, bool haggle)
        {
            return haggle ? (int)Js.Round(price * (100 - HaggleDiscount) / 100.0) : price;
        }

        // --------------------------------------------------------------- the events

        /// <summary>
        /// The events of the runs, by Lore's ids (.dev-snapshots/lore/runs/SUCESOS.md; its texts in Lore.RunEvents): option 0
        /// and option 1. First figures, at the level of PLAN-RUNS §3.3.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, RunEventDef> RunEvents = new Dictionary<string, RunEventDef>
        {
            ["farmacia"] = Event("farmacia", new RunEventOption { GearKind = GearKinds.Item, GearType = ItemIds.FirstAid, GearCount = 1, HurtOne = 2 }, new RunEventOption()),
            ["gasolinera"] = Event("gasolinera", new RunEventOption { Supplies = 25, Modifier = Modifiers.Awake }, new RunEventOption()),
            ["herido"] = Event("herido", new RunEventOption { Rescue = true }, new RunEventOption { Supplies = -15, GearKind = GearKinds.Weapon, GearType = FoundWeaponIds.Pistol, GearCount = 1 }),
            ["campamento"] = Event("campamento", new RunEventOption { HealAll = 2, Modifier = Modifiers.Night }, new RunEventOption { Supplies = 15 }),
            ["puente"] = Event("puente", new RunEventOption { HurtOne = 3 }, new RunEventOption { Modifier = Modifiers.Storm }),
            ["caja-atada"] = Event("caja-atada", new RunEventOption { GearKind = GearKinds.Item, GearType = ItemIds.FragGrenade, GearCount = 2, HurtOne = 2 }, new RunEventOption()),
            ["perro"] = Event("perro", new RunEventOption { Supplies = -5, GearKind = GearKinds.Item, GearType = ItemIds.FirstAid, GearCount = 1 }, new RunEventOption()),
            ["control"] = Event("control", new RunEventOption { Supplies = -30 }, new RunEventOption { HurtAll = 2 }),
            ["radio"] = Event("radio", new RunEventOption { Rescue = true, Modifier = Modifiers.Night }, new RunEventOption { Supplies = 20 }),
            ["supermercado"] = Event("supermercado", new RunEventOption { Supplies = 30, HurtAll = 1 }, new RunEventOption { Supplies = 10 }),
        };

        private static RunEventDef Event(string id, RunEventOption first, RunEventOption second)
        {
            return new RunEventDef { Id = id, Options = new[] { first, second } };
        }

        /// <summary>RunEvents' ids, in the order they are drawn from.</summary>
        public static readonly IReadOnlyList<string> RunEventIds = new[] { "farmacia", "gasolinera", "herido", "campamento", "puente", "caja-atada", "perro", "control", "radio", "supermercado" };
    }
}
