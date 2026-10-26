using System;
using System.Collections.Generic;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The Armería (screen.ts renderEngineering, arms.ts): its two tabs, Accesorios (belt items to buy) and Armamento
    /// (the good weapons, loose mods, armour, what the Taller makes), as lists on the left; what is picked, on the right.
    /// </summary>
    [Serializable]
    public sealed class ArmoryView
    {
        public string Head;
        /// <summary>items or gear.</summary>
        public string Tab;
        public bool ItemsNew;
        public bool GearNew;
        /// <summary>«Taller: −35 %» under the items' list; null, none.</summary>
        public string Note;
        public List<ArmorySectionView> Sections = new List<ArmorySectionView>();
        public ArmoryDetailView Detail;
    }

    [Serializable]
    public sealed class ArmorySectionView
    {
        /// <summary>«En la Armería», «Mejoras sueltas»…; null for the items' list, which has none.</summary>
        public string Title;
        public List<ArmoryRowView> Rows = new List<ArmoryRowView>();
    }

    /// <summary>
    /// A row of a list: its icon (or the armour's diamonds), its name and tags, its price under it (items), and on the
    /// right where it goes and who has it, a count, a chip or ✓.
    /// </summary>
    [Serializable]
    public sealed class ArmoryRowView
    {
        /// <summary>What the handlers get: «i:grenade», «f:w100», «x:stock», «a2», «m:silencer».</summary>
        public string Id;
        public string Icon;
        /// <summary>The armour's tier (its diamonds instead of an icon); 0, none.</summary>
        public int Tier;
        public string Name;
        public List<ChipView> Tags = new List<ChipView>();
        /// <summary>The price chip under the name (items); null, none.</summary>
        public string Price;
        /// <summary>«MANO», «CINTO»: in small capitals on the right.</summary>
        public string Place;
        /// <summary>Under the place: who carries it, «En la Armería».</summary>
        public string Holder;
        /// <summary>«Tienes 2», «× 2», «✓».</summary>
        public string Right;
        /// <summary>«Taller · 2 misiones»; null, none.</summary>
        public ChipView Chip;
        public bool Selected;
        /// <summary>Not made yet (armour, the Taller's): dim.</summary>
        public bool Dim;
        public bool New;
        /// <summary>What its «Nuevo» marks (fresh.ts: «item:grenade», «craft:stock»): told back when the pointer goes over it.</summary>
        public string NewKey;
        public string Tip;
    }

    /// <summary>What is picked: a belt item, a good weapon, a loose mod, the Taller's work, an armour tier.</summary>
    [Serializable]
    public sealed class ArmoryDetailView
    {
        /// <summary>The kicker's help («Se equipa en la ficha del soldado; si cae, se pierde»); null, none.</summary>
        public string KickerTip;
        /// <summary>«ACCESORIO · CINTURÓN», «ARMA · MANO», «TALLER · MEJORA»…</summary>
        public string Kicker;
        public string Name;
        /// <summary>The silhouette in the corner (an icon id).</summary>
        public string Art;
        public List<string> Text = new List<string>();
        public List<ChipView> Chips = new List<ChipView>();
        /// <summary>The weapon's figures (Daño 3-5, Crítico ±0 %…).</summary>
        public List<RowView> Figures = new List<RowView>();
        public RangeView Range;
        public ConditionView Condition;
        public ModsView Mods;
        /// <summary>«La lleva»: who and «Quitársela», or «Nadie: está en la Armería.».</summary>
        public HolderView Holder;
        /// <summary>«Tus soldados»: each with what they carry now, the difference, «Dársela» or «La lleva».</summary>
        public string SoldiersHead;
        public List<CarrierView> Soldiers = new List<CarrierView>();
        /// <summary>«No tienes soldados que puedan llevarla.»</summary>
        public string SoldiersEmpty;
        /// <summary>The main button (Comprar, Hacer la culata, Ponérsela…) and its action; null, none.</summary>
        public string Button;
        public string Action;
        public bool Enabled = true;
        public string Why;
    }

    /// <summary>The weapon's reach (arms.ts rangeChart): a bar above or below the line at each distance, its name, a line.</summary>
    [Serializable]
    public sealed class RangeView
    {
        public string Name;
        public List<RangeStepView> Steps = new List<RangeStepView>();
        public string Line;
    }

    [Serializable]
    public sealed class RangeStepView
    {
        public string Distance;
        /// <summary>«+19», «±0», «−10».</summary>
        public string Value;
        /// <summary>−1 to 1: the bar's height, above the line (good) or below it.</summary>
        public float Height;
    }

    [Serializable]
    public sealed class ConditionView
    {
        public int Value;
        public string Text;
        public string Line;
        /// <summary>«Arreglar · 1 pieza» and its action; null, nothing to mend.</summary>
        public string Button;
        public bool Enabled = true;
        public string Why;
    }

    [Serializable]
    public sealed class ModsView
    {
        public string Head;
        /// <summary>What is mounted: its name, its line, «Quitar».</summary>
        public List<ModRowView> Mounted = new List<ModRowView>();
        /// <summary>«Hueco libre» and «Poner culata (2)»…; empty, no free slot.</summary>
        public string FreeHead;
        public List<ModRowView> Options = new List<ModRowView>();
        /// <summary>How many places are free: one «Hueco libre» each when there is nothing to put in them (arms.ts weaponCare).</summary>
        public int FreeSlots;
        /// <summary>No loose improvement to put in: «No hay mejoras sueltas…»; null, there are.</summary>
        public string Empty;
    }

    [Serializable]
    public sealed class ModRowView
    {
        public string Id;
        public string Name;
        public string Line;
        public string Button;
        public bool Enabled = true;
        /// <summary>Its key's help: why it is off, or what it does («Vuelve a la Armería, suelta»; the improvement's line).</summary>
        public string Tip;
    }

    [Serializable]
    public sealed class HolderView
    {
        public string Head;
        public string Name;
        /// <summary>«Quitársela»; null, nobody carries it.</summary>
        public string Button;
    }

    [Serializable]
    public sealed class CarrierView
    {
        public string Id;
        public string Name;
        /// <summary>«· en la mano: Escopeta».</summary>
        public string Note;
        /// <summary>The figures that change, each better (good) or worse.</summary>
        public List<ChipView> Diff = new List<ChipView>();
        /// <summary>«Dársela»; null with `Chip` («La lleva»).</summary>
        public string Button;
        public string Chip;
        public bool Enabled = true;
        /// <summary>Why its key is off (modBlocker); null, it isn't.</summary>
        public string Tip;
    }
}
