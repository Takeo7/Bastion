using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Bastion.UI.Gear
{
    /// <summary>
    /// One thing someone carries (the engine's Gear, .dev-snapshots/gameplay/runs/H0-DISENO.md §2), as the grid draws it:
    /// its picture, its size in cells lying across (a rifle 4×1), how many in its pile and its card on hover.
    /// </summary>
    [Serializable]
    public sealed class GearItemView
    {
        /// <summary>The Gear's id («g17»): what the orders name.</summary>
        public string Id;
        /// <summary>GearKinds: weapon, item, mod, vest, mags, medicine, supplies.</summary>
        public string Kind;
        /// <summary>Its def by kind (FoundWeaponIds, ItemIds…): with the kind, Arte's picture (GearPictures).</summary>
        public string Type;
        /// <summary>Its name, short: on the weapons' tile, and the card's title.</summary>
        public string Name;
        /// <summary>With its article, for sentences: «la mira» (RunTexts.The); null, the name in lower case.</summary>
        public string The;
        /// <summary>The HUD's icon (HudIcons, or one of our line drawings) while Arte's picture is missing; one character, that sign («§»).</summary>
        public string Icon;
        /// <summary>Its size in cells, not turned: lying across (Content.GearSize).</summary>
        public int W = 1, H = 1;
        public int Count = 1;
        /// <summary>A weapon's mods mounted (WeaponModIds), shown as marks on its tile (M13); null, none.</summary>
        public List<string> Mods;
        /// <summary>How many fit in one place (Content.PileOf): above 1, its count shows.</summary>
        public int Pile = 1;
        /// <summary>The words on hover when there is no card.</summary>
        public string Tip;
        /// <summary>
        /// In a line, what it changes against what is worn in its place (U4, a kit place's list): a weapon's damage and aim as
        /// that soldier would have them, a vest's health and armour; green better, red worse (rich text); null, nothing to say.
        /// </summary>
        public string Brief;
        /// <summary>Its card on hover (name, figures, mods); null, `Tip`.</summary>
        [NonSerialized] public Func<VisualElement> Card;
    }

    /// <summary>A thing in a grid: its top-left cell; turned, its width and height swap (the engine's Placed).</summary>
    [Serializable]
    public sealed class PlacedView
    {
        public GearItemView Gear;
        public int X, Y;
        public bool Turned;
    }

    /// <summary>A grid: the base's stash (10×6, two rows more with each upgrade) or a soldier's pack (4×3).</summary>
    [Serializable]
    public sealed class GearGridView
    {
        /// <summary>Whose: «stash», or a soldier's id (MoveGearCommand.To).</summary>
        public string Owner;
        /// <summary>Null for the stash; «pack» for a soldier's (MoveGearCommand.Slot).</summary>
        public string Slot;
        public int W, H;
        public List<PlacedView> Items = new List<PlacedView>();
        /// <summary>The player's own: things are taken from it and put in it. Not, it shows only (the partner's soldiers).</summary>
        public bool Mine = true;
        /// <summary>The rows seen before it scrolls (0: all of them).</summary>
        public int Rows;
    }

    /// <summary>One of a soldier's places (Kit): primary, melee, pistol, vest, belt0, belt1.</summary>
    [Serializable]
    public sealed class GearSlotView
    {
        /// <summary>The soldier's id.</summary>
        public string Owner;
        /// <summary>«primary», «melee», «pistol», «vest», «belt0», «belt1» (MoveGearCommand.Slot).</summary>
        public string Slot;
        /// <summary>Its name, shown while it is empty («PRINCIPAL»).</summary>
        public string Label;
        /// <summary>Its box, in cells: the biggest that goes in it lying across (the primary 4×2, for the machine gun).</summary>
        public int W = 1, H = 1;
        public GearItemView Gear;
        public bool Mine = true;
    }

    /// <summary>
    /// Where a thing would go: what the board asks the engine about while it is dragged (GearRefusal) and gives on letting
    /// it go (GearMoved). Count below the pile's, a split (SplitGearCommand); else a move (MoveGearCommand).
    /// </summary>
    [Serializable]
    public sealed class GearMove
    {
        public string Gear;
        /// <summary>How many: 0, the whole pile.</summary>
        public int Count;
        /// <summary>«stash» or a soldier's id.</summary>
        public string To;
        /// <summary>Null (the stash), «pack», or one of the kit's places.</summary>
        public string Slot;
        public int X, Y;
        public bool Turned;
        /// <summary>A weapon mod let go on a weapon: that weapon's id (MountCommand); then To, Slot and the cell say nothing.</summary>
        public string Onto;
        /// <summary>Let go on the bin: thrown away (DropGearCommand), once asked.</summary>
        public bool Throw;
        /// <summary>
        /// `Slot` for a thing let go on a soldier's bust (M12): wherever it goes on them, its own place if empty, else the
        /// first room of their pack; the handlers say where (the engine's helpers).
        /// </summary>
        public const string Anywhere = "*";

        public override string ToString() => Throw ? $"{Gear} a la basura" : Onto != null ? $"{Gear} sobre {Onto}" : $"{Gear}{(Count > 0 ? "×" + Count : "")} → {To}{(Slot != null ? "." + Slot : "")} {X},{Y}{(Turned ? " girado" : "")}";
    }

    /// <summary>An order for one thing besides moving it (Usar, Montar…): the handlers say which, and why not.</summary>
    [Serializable]
    public sealed class GearActionView
    {
        public string Id;
        public string Label;
        /// <summary>Why it can't now; null, it can.</summary>
        public string Why;
    }

    /// <summary>What the grids ask and tell (the base's presenter, the combat's controller, the lab).</summary>
    public interface IGearHandlers
    {
        /// <summary>Null: it can go there. Else why not, in the engine's words (Gears.MoveRefusal, SplitRefusal).</summary>
        string GearRefusal(GearMove m);
        /// <summary>Let go where it can: the order.</summary>
        void GearMoved(GearMove m);
        /// <summary>Tirar: it is lost.</summary>
        void GearDropped(string gear);
        /// <summary>The other orders for one thing, under it when it is picked (a click); none, an empty list.</summary>
        IReadOnlyList<GearActionView> GearActions(string gear);
        void GearAction(string gear, string action);
    }
}
