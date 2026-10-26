using System;
using System.Collections.Generic;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Soldados (screen.ts renderBarracks): one's own soldiers, the recruit's card and the partner's while away on the
    /// left; the picked soldier's sheet on the right.
    /// </summary>
    [Serializable]
    public sealed class SoldiersView
    {
        public string MineHead;
        public List<SoldierTileView> Mine = new List<SoldierTileView>();
        public RecruitView Recruit;
        /// <summary>«De Ana» and «no está: los preparas tú»; none while the partner is here.</summary>
        public string PartnerHead;
        public string PartnerNote;
        public List<SoldierTileView> Partner = new List<SoldierTileView>();
        public SoldierSheetView Sheet;
        /// <summary>Personalizar open: in place of the sheet.</summary>
        public CustomizerView Customizer;
    }

    /// <summary>A soldier's tile: the bust, the best line's hexagon and the name, the rank's chevrons, ▲ for a talent to choose.</summary>
    [Serializable]
    public sealed class SoldierTileView
    {
        public string Id;
        public string Name;
        /// <summary>The owner's colour: the tile's bottom edge.</summary>
        public string Accent;
        /// <summary>The six lines' experience (SkillIds.All order), for the hexagon.</summary>
        public float[] Skills = new float[6];
        public int Rank;
        /// <summary>«Fogueada · 6 puntos gastados en el tablero».</summary>
        public string RankTip;
        public bool Points;
        public string PointsTip;
        /// <summary>Under the name: «Fiebre · ahora», «2 misiones», «Caído»; null, none.</summary>
        public string Tag;
        /// <summary>bad, warn or null.</summary>
        public string TagTone;
        public string TagTip;
        public bool Dead;
        public bool Selected;
    }

    [Serializable]
    public sealed class RecruitView
    {
        public string Title;
        public string Blurb;
        public List<ChipView> Chips = new List<ChipView>();
        public string Button;
        public bool Enabled;
        /// <summary>Why not (the first reason that applies); null, it can be done.</summary>
        public string Why;
    }

    /// <summary>A small chip: a figure or a word, its tone, its tooltip.</summary>
    [Serializable]
    public sealed class ChipView
    {
        public string Text;
        /// <summary>good, bad, warn or null.</summary>
        public string Tone;
        public string Tip;
    }

    /// <summary>The picked soldier's sheet (the right column).</summary>
    [Serializable]
    public sealed class SoldierSheetView
    {
        /// <summary>«NOVATO · DE ANA», «FOGUEADA · TUYO».</summary>
        public string Kicker;
        public string Name;
        public float[] Skills = new float[6];
        public string NameTip;
        /// <summary>Under the name: the weapon in the hand.</summary>
        public string Subtitle;
        /// <summary>«Ana no está: lo preparas tú hasta que vuelva.»; null, none.</summary>
        public string Note;
        public string Customize;
        /// <summary>Health, firearm aim, melee aim, will: each an icon, a figure, a colour.</summary>
        public List<SheetStatView> Stats = new List<SheetStatView>();
        /// <summary>«Listo», «Fiebre · ahora»… at the end of the figures.</summary>
        public string Health;
        public string HealthTone;
        public string HealthTip;
        /// <summary>Mordido, Herido: what can be done for them; null, nothing.</summary>
        public CareView Care;
        public List<ChipView> Traits = new List<ChipView>();
        public string ArmsHead;
        public List<PickView> Arms = new List<PickView>();
        public ArmorView Armor;
        public string BeltHead;
        public List<PickView> Belt = new List<PickView>();
        public SkillsView Experience;
    }

    [Serializable]
    public sealed class SheetStatView
    {
        public string Icon;
        public string Value;
        public string Color;
        public string Tip;
    }

    /// <summary>A bitten or wounded soldier's care: the head, the buttons (the first the main one), who it waits for.</summary>
    [Serializable]
    public sealed class CareView
    {
        /// <summary>«Mordisco»; null, no head (a wound's cure).</summary>
        public string Title;
        /// <summary>What it is about: «El tratamiento le quita la fiebre. Tenéis 4 ✚.»; null, none.</summary>
        public string Line;
        public List<string> Buttons = new List<string>();
        public List<bool> Enabled = new List<bool>();
        public string Waiting;
        /// <summary>Why the main one is off, in red; null, it isn't.</summary>
        public string Why;
        /// <summary>The main one's help on hover.</summary>
        public string Tip;
    }

    /// <summary>
    /// A choice made on the sheet itself (screen.ts .x-pick, arms.ts weaponPick): «MANO Fusil de asalto ▾» with its
    /// lines under it; open, the options in a list under it.
    /// </summary>
    [Serializable]
    public sealed class PickView
    {
        /// <summary>«hand», «belt», «slot0»…: what the handler gets.</summary>
        public string Id;
        public string Label;
        public string Value;
        public List<string> Lines = new List<string>();
        /// <summary>The weapon's condition, 0–100; −1, none shown.</summary>
        public int Condition = -1;
        public string ConditionText;
        /// <summary>Its mods as tags («Mira»).</summary>
        public List<string> Tags = new List<string>();
        public bool Editable;
        public List<PickOptionView> Options = new List<PickOptionView>();
    }

    [Serializable]
    public sealed class PickOptionView
    {
        public string Id;
        public string Name;
        public string Note;
        public bool Enabled = true;
        public bool Current;
    }

    [Serializable]
    public sealed class ArmorView
    {
        public int Tier;
        public string Name;
        public string Figures;
        /// <summary>«→ Chaleco de kevlar» and why it isn't there yet; null, the top.</summary>
        public string Next;
        public string NextTip;
    }

    /// <summary>The experience (skillSheet.ts skillsBlock): six lines, the rank, the board's button.</summary>
    [Serializable]
    public sealed class SkillsView
    {
        public string Head;
        public List<SkillRowView> Rows = new List<SkillRowView>();
        public string Rank;
        public int RankFilled;
        public int RankTotal;
        public string Will;
        /// <summary>What the next rank gives in full; at the top, «No hay rango más alto.».</summary>
        public string WillTip;
        public string RankLine;
        /// <summary>What the rank gives now: «Voluntad 40 · 2 acciones por turno».</summary>
        public string Gives;
        public string Board;
        /// <summary>One's own soldier with points to spend: the button pulses.</summary>
        public bool BoardPulse;
    }

    [Serializable]
    public sealed class SkillRowView
    {
        /// <summary>The line's place in SkillIds.All (its colour).</summary>
        public int Line;
        public string Name;
        /// <summary>«+8 %» growth from the last missions; null, none.</summary>
        public string Growth;
        public int Filled;
        public int Total;
        public string Reward;
        public string RewardTip;
        /// <summary>How the bar fills (SKILL_DEFS line), on its name; what it raises (its stat), on the growth.</summary>
        public string Tip;
        public string GrowthTip;
    }
}

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Personalizar (screen.ts customizer): the draft of a soldier's name, nickname and looks, shown in place of the
    /// sheet. Each change goes to the handlers, which keep the draft and preview it on the figure; Guardar sends it.
    /// </summary>
    [System.Serializable]
    public sealed class CustomizerView
    {
        public string Kicker;
        public string Title;
        public string Name;
        public int NameMax = 24;
        public string Nickname;
        public int NicknameMax = 16;
        /// <summary>Ropa, Cabeza, Complexión: each a row of choices.</summary>
        public System.Collections.Generic.List<ChoiceGroupView> Groups = new System.Collections.Generic.List<ChoiceGroupView>();
        public string ColorsHead;
        /// <summary>«Para teñir la ropa, activa…» while the setting is off (the rows then dim); null, on.</summary>
        public string ColorsOff;
        public System.Collections.Generic.List<ColorRowView> Colors = new System.Collections.Generic.List<ColorRowView>();
        public bool CanSave;
        public string SaveWhy;
        /// <summary>«Copiar a todos mis soldados» pressed once: it asks again.</summary>
        public bool CopyArmed;
        public int CopyCount;
        /// <summary>It can be pressed: a name, other soldiers of one's own, and the clothes taking colours.</summary>
        public bool CanCopy = true;
    }

    [System.Serializable]
    public sealed class ChoiceGroupView
    {
        /// <summary>outfit, head, build.</summary>
        public string Id;
        public string Title;
        public System.Collections.Generic.List<ChoiceView> Choices = new System.Collections.Generic.List<ChoiceView>();
    }

    [System.Serializable]
    public sealed class ChoiceView
    {
        public string Id;
        public string Label;
        public bool On;
    }

    /// <summary>A part's colour row: its swatch, its name, a note («como la armadura»), the palette, «Otro color».</summary>
    [System.Serializable]
    public sealed class ColorRowView
    {
        /// <summary>all, helmet, torso, arms, legs, trim, mark.</summary>
        public string Id;
        public string Label;
        public string Note;
        /// <summary>«↺ como la armadura»: back to following; null, none.</summary>
        public string Reset;
        public string Color;
        public System.Collections.Generic.List<string> Swatches = new System.Collections.Generic.List<string>();
        /// <summary>Nothing to paint (the head without a helmet) or always the player's (the mark).</summary>
        public bool Locked;
    }
}

namespace Bastion.UI.Strategy
{
    /// <summary>The skill board's window (skillSheet.ts treePanel): whose, their rank, the cells they have, points to spend.</summary>
    [System.Serializable]
    public sealed class SkillBoardView
    {
        public string Kicker;
        public string SoldierId;
        public string Name;
        public string Rank;
        public int RankFilled;
        public int RankTotal;
        public string Will;
        public int Points;
        /// <summary>One's own living soldier: cells can be bought (held a second).</summary>
        public bool Editable;
        public System.Collections.Generic.List<string> Board = new System.Collections.Generic.List<string>();
    }
}
