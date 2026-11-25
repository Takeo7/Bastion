#nullable enable
// The runs (.dev-snapshots/gameplay/runs/H0-DISENO.md §2; new in C#): what a soldier carries and the base keeps, as
// things laid in grids. Every gear is one thing with its own id, so it can go stash ↔ soldier ↔ pack ↔ floor. Sizes,
// piles and which kit slot takes what are Content's (ContentGear.cs); the grids' rules and the gear commands, Gears.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>Gear.kind: what sort of thing a gear is; its `type` says which, by kind.</summary>
    public static class GearKinds
    {
        /// <summary>Type: FoundWeaponIds (also "knife").</summary>
        public const string Weapon = "weapon";
        /// <summary>Type: ItemIds, the belt's (Content.BeltItems: grenades, molotov, smoke, flashbang, first aid).</summary>
        public const string Item = "item";
        /// <summary>Type: WeaponModIds.</summary>
        public const string Mod = "mod";
        /// <summary>Type: VestIds: "vest" (+2 HP), "heavyVest" (+4 HP, +1 armour).</summary>
        public const string Vest = "vest";
        /// <summary>Spare magazines; no type.</summary>
        public const string Mags = "mags";
        /// <summary>Heals 3 (+ Heal) in a mission or between nodes; no type.</summary>
        public const string Medicine = "medicine";
        /// <summary>The § (the suministros): in a run campaign the only ones there are; no type.</summary>
        public const string Supplies = "supplies";
        /// <summary>G23.1: a police station's armoury key (MissionState.Key); no type; never home from the fight.</summary>
        public const string Key = "key";
        /// <summary>G23.2: Mordido's antiviral (MissionState.Antiviral); no type; never home from the fight.</summary>
        public const string Antiviral = "antiviral";

        /// <summary>
        /// Arco y ballesta (cuadro 102): the arrows, one ammunition for both (WeaponDef.Arrows), 10 to a cell; no type. A
        /// run's soldier counts those in its pack (Unit.Arrows); one that hits stays in the body (Unit.ArrowsIn).
        /// </summary>
        public const string Arrows = "arrows";
        /// <summary>G23 Las cargas (cuadro 109): a charge to set at a point of the nest (AbilityIds.PlaceCharge); no type; never home from the fight.</summary>
        public const string Charge = "charge";

        public static readonly IReadOnlyList<string> All = new[] { Weapon, Item, Mod, Vest, Mags, Medicine, Supplies, Key, Antiviral, Arrows, Charge };
    }

    /// <summary>The vests (Content.Vests): armour is a thing now, not a level of the Taller.</summary>
    public static class VestIds
    {
        /// <summary>+2 HP.</summary>
        public const string Vest = "vest";
        /// <summary>+4 HP and +1 armour; rare, from an Élite or a police station.</summary>
        public const string HeavyVest = "heavyVest";

        public static readonly IReadOnlyList<string> All = new[] { Vest, HeavyVest };
    }

    /// <summary>Where on a soldier a gear goes (MoveGearCommand.slot): the pack's grid, or one of the kit's places.</summary>
    public static class KitSlots
    {
        public const string Pack = "pack";
        public const string Primary = "primary";
        public const string Melee = "melee";
        public const string Pistol = "pistol";
        public const string Vest = "vest";
        public const string Belt0 = "belt0";
        public const string Belt1 = "belt1";

        public static readonly IReadOnlyList<string> All = new[] { Pack, Primary, Melee, Pistol, Vest, Belt0, Belt1 };
    }

    /// <summary>MoveGearCommand.to: the shared stash, or a soldier's id.</summary>
    public static class GearPlaces
    {
        public const string Stash = "stash";
        /// <summary>La Lata's trunk (RunState.Trunk; cuadro 165).</summary>
        public const string Trunk = "trunk";
    }

    /// <summary>One thing someone can carry: unique in the campaign, so it can go stash ↔ soldier ↔ pack ↔ floor.</summary>
    public sealed class Gear
    {
        /// <summary>"g17": from the campaign's NextId, as the soldiers'.</summary>
        public string Id { get; set; } = "";
        public string Kind { get; set; } = ""; // GearKinds
        /// <summary>The def, by kind; null for mags, medicine and supplies.</summary>
        public string? Type { get; set; }
        /// <summary>A pile: Content.PileOf(kind, type) at most.</summary>
        public int Count { get; set; } = 1;
        /// <summary>A weapon's mounted mods, WeaponModIds values (not gear), Content.ModSlots at most.</summary>
        public List<string>? Mods { get; set; } // WeaponModIds
        /// <summary>
        /// Given by the base at setting out (Botiquín de salida, cuadro 165): it can't be offered at a Trade nor sold
        /// (Content.SellPrice 0), and makes no pile with one that wasn't (Gears.SamePile). Missing: no.
        /// </summary>
        public bool? Issued { get; set; }
    }

    /// <summary>Where a gear lies in a grid: its top-left cell; turned, its width and height swap.</summary>
    public sealed class Placed
    {
        public Gear Gear { get; set; } = new Gear();
        public int X { get; set; }
        public int Y { get; set; }
        public bool Turned { get; set; }
    }

    /// <summary>A grid of cells, W wide and H high: the stash, a pack.</summary>
    public sealed class GearGrid
    {
        public int W { get; set; }
        public int H { get; set; }
        public List<Placed> Items { get; set; } = new List<Placed>();
    }

    /// <summary>A kit's places, its pack apart: the campaign's Kit, and a soldier's in a fight (KitPlaces, its pack Unit.Pack).</summary>
    public interface IKitPlaces
    {
        Gear? Primary { get; set; }
        Gear? Melee { get; set; }
        Gear? Pistol { get; set; }
        Gear? Vest { get; set; }
        Gear?[] Belt { get; set; }
    }

    /// <summary>What a soldier takes out: three weapons, a vest, two belt places and the pack.</summary>
    public sealed class Kit : IKitPlaces
    {
        public Gear? Primary { get; set; }
        public Gear? Melee { get; set; }
        public Gear? Pistol { get; set; }
        public Gear? Vest { get; set; }
        /// <summary>Two places, by index (KitSlots.Belt0 and Belt1); an empty one is null.</summary>
        public Gear?[] Belt { get; set; } = new Gear?[2];
        public GearGrid Pack { get; set; } = new GearGrid { W = 4, H = 3 };
    }

    /// <summary>
    /// A run's soldier in a fight (H3, Unit.Kit): the kit's places as they are out there; its pack is Unit.Pack. What it
    /// fights with (Unit.Arms, its charges, its health) follows from them (RunPack).
    /// </summary>
    public sealed class KitPlaces : IKitPlaces
    {
        public Gear? Primary { get; set; }
        public Gear? Melee { get; set; }
        public Gear? Pistol { get; set; }
        public Gear? Vest { get; set; }
        /// <summary>Two places, by index (KitSlots.Belt0 and Belt1); an empty one is null.</summary>
        public Gear?[] Belt { get; set; } = new Gear?[2];
    }

    /// <summary>A gear on the floor of a run's fight (GameState.floorGear): dropped, found with no room, or a fallen soldier's (`from`, its unit).</summary>
    public sealed class DroppedGear
    {
        public Gear Gear { get; set; } = new Gear();
        public Pos Pos { get; set; } = new Pos();
        public string? From { get; set; }
    }
}
