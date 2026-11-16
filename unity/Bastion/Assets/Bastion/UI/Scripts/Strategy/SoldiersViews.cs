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
        /// <summary>
        /// One grid, no heads (Arturo, 14/11): one's own soldiers, the partner's while the partner is away (to prepare them;
        /// their backdrop another colour), and the recruit's tile last.
        /// </summary>
        public List<SoldierTileView> Mine = new List<SoldierTileView>();
        public RecruitView Recruit;
        public List<SoldierTileView> Partner = new List<SoldierTileView>();
        public SoldierSheetView Sheet;
        /// <summary>Personalizar open: in place of the sheet.</summary>
        public CustomizerView Customizer;
        /// <summary>Nobody can go out (Campaign.MustWait): «ESPERAR A QUE SE CUREN» under the clearing, and its line; null, they can.</summary>
        public WaitDayView MustWait;
    }

    /// <summary>
    /// A soldier's tile (Arturo, 14/11): the bust filling its square on a studio light (purple for one's own, another
    /// colour for the partner's), the name in a band under it (the picked one's lit), the rank's chevron, ▲ for points to
    /// spend, and how they are (hurt, fallen) over the bust's foot.
    /// </summary>
    [Serializable]
    public sealed class SoldierTileView
    {
        public string Id;
        public string Name;
        /// <summary>The partner's: its studio light the partner's colour.</summary>
        public bool Theirs;
        public int Rank;
        /// <summary>«Fogueada · 6 puntos gastados en el tablero».</summary>
        public string RankTip;
        public bool Points;
        public string PointsTip;
        /// <summary>Under the name: «2 misiones», «Caído»; null, none.</summary>
        public string Tag;
        /// <summary>bad, warn or null.</summary>
        public string TagTone;
        public string TagTip;
        public bool Dead;
        public bool Selected;
        /// <summary>On hover, the whole name with the nickname (screen.ts: displayName).</summary>
        public string FullName;
    }

    /// <summary>Recruiting, a tile of the grid (Arturo, 14/11): a «+», «Reclutar» and the price; what it is, on hover.</summary>
    [Serializable]
    public sealed class RecruitView
    {
        public string Label;
        /// <summary>«30 §».</summary>
        public string Cost;
        /// <summary>What a recruit is, the soldiers and the beds (its old card's words), on hover.</summary>
        public string Tip;
        public bool Enabled;
        /// <summary>Why not (the first reason that applies), on hover in red; null, it can be done.</summary>
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

    /// <summary>
    /// The picked soldier (Arturo, 14/11, vista-soldados): on the right the sheet (the name, the battle HUD's three lines,
    /// the experience, the rank and the board's button); round the figure its gear, the hand, the side and the belt on the
    /// left, the clothes and the belt's two places on the right.
    /// </summary>
    [Serializable]
    public sealed class SoldierSheetView
    {
        public string Name;
        /// <summary>On the name: missions and kills, and the figures the sheet's strip had (aims, will, dodge).</summary>
        public string NameTip;
        /// <summary>The battle HUD's three lines (its squad cards'): health, actions a turn, movement in tiles; their help.</summary>
        public int Hp;
        public int Ap;
        public int Moves;
        public string HpTip;
        public string ApTip;
        public string MovesTip;
        /// <summary>«Ana no está: lo preparas tú hasta que vuelva.»; null, none.</summary>
        public string Note;
        /// <summary>«Heridas · 2 misiones», «Caído»; null when ready (no «Listo» any more: Arturo, 14/11).</summary>
        public string Health;
        public string HealthTone;
        public string HealthTip;
        /// <summary>Mordido, Herido: what can be done for them; null, nothing.</summary>
        public CareView Care;
        public List<ChipView> Traits = new List<ChipView>();
        /// <summary>Round the figure, on its left: the hand, the side (where the Armería counts the pistols) and the belt.</summary>
        public List<PickView> Arms = new List<PickView>();
        /// <summary>On its right, at the top: the clothes; under them the belt's two places.</summary>
        public ArmorView Armor;
        public List<PickView> Belt = new List<PickView>();
        public SkillsView Experience;
        /// <summary>Personalizar, by a click on the figure (Arturo, 14/11): one's own (or the absent partner's), alive.</summary>
        public bool Customizable;
    }

    /// <summary>A wounded soldier's care: the buttons (the first the main one), who it waits for.</summary>
    [Serializable]
    public sealed class CareView
    {
        public List<string> Buttons = new List<string>();
        public List<bool> Enabled = new List<bool>();
        public string Waiting;
        /// <summary>Why the main one is off, in red; null, it isn't.</summary>
        public string Why;
        /// <summary>The main one's help on hover.</summary>
        public string Tip;
        /// <summary>The main one waits for this player's yes (the partner's treatment: strategy.css .x-btn.waiting): it pulses.</summary>
        public bool Pulse;
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
        /// <summary>What is there, on its tile (a weapon's, an accessory's); null, an empty place («+»).</summary>
        public string Icon;
        public List<string> Lines = new List<string>();
        /// <summary>The weapon's condition, 0–100; −1, none shown.</summary>
        public int Condition = -1;
        public string ConditionText;
        /// <summary>Its mods as tags («Mira»).</summary>
        public List<string> Tags = new List<string>();
        public bool Editable;
        public List<PickOptionView> Options = new List<PickOptionView>();
        /// <summary>Under the options when there is nothing else to pick (wpick-empty: «No hay armas en la Armería.»); null, none.</summary>
        public string Empty;
    }

    [Serializable]
    public sealed class PickOptionView
    {
        public string Id;
        public string Name;
        public string Note;
        /// <summary>A weapon's picture before its name (weaponIcon); null, none.</summary>
        public string Icon;
        /// <summary>A firearm's wear and improvements by its name (weaponMarks: «Gastada», «+2»).</summary>
        public List<ChipView> Tags = new List<ChipView>();
        /// <summary>Against what it carries now (weaponDiff): better in green, worse in red, «Igual».</summary>
        public List<ChipView> Diff = new List<ChipView>();
        /// <summary>On hover: why a partner's is off.</summary>
        public string Tip;
        public bool Enabled = true;
        public bool Current;
    }

    [Serializable]
    public sealed class ArmorView
    {
        public int Tier;
        public string Name;
        /// <summary>«5 de salud», «6 de salud · 1 de blindaje».</summary>
        public string Figures;
        /// <summary>«→ Chaleco de kevlar» and why it isn't there yet; null, the top.</summary>
        public string Next;
        public string NextTip;
    }

    /// <summary>
    /// The experience (skillSheet.ts skillsBlock; Arturo, 14/11): six rows, each its name and its green growth on the
    /// left, the experience into its bar on the right, the bar under them; «Rango» and the rank's name over its five
    /// pieces; the board's button with the points to spend beside it.
    /// </summary>
    [Serializable]
    public sealed class SkillsView
    {
        public string Head;
        public List<SkillRowView> Rows = new List<SkillRowView>();
        public string RankLabel;
        public string Rank;
        public int RankFilled;
        public int RankTotal;
        /// <summary>On the rank's row: what it gives now and what the next one gives (its old lines).</summary>
        public string RankTip;
        public string Board;
        /// <summary>«3 puntos por gastar» beside the button; null, none.</summary>
        public string BoardPoints;
        /// <summary>One's own soldier with points to spend: the button pulses.</summary>
        public bool BoardPulse;
    }

    [Serializable]
    public sealed class SkillRowView
    {
        public string Name;
        /// <summary>«+8 %» growth from the last missions; null, none.</summary>
        public string Growth;
        /// <summary>The experience into the bar being filled, and how far that is of it (0–1).</summary>
        public int Value;
        public float Share;
        /// <summary>How the bar fills and what a full one gives, on the row; what it raises (its stat), on the growth.</summary>
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
        /// <summary>«Copiar a todos»'s help: what it does, or how to turn the colours on.</summary>
        public string CopyTip;
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
        /// <summary>Its help («Una al azar, y no cambia.»); null, none.</summary>
        public string Tip;
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
        /// <summary>Its help («Que vuelva a ir como la armadura»).</summary>
        public string ResetTip;
        /// <summary>A locked row's help (the mark: why it is always the player's); null, none.</summary>
        public string Tip;
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

    /// <summary>«Esperar un día»: a key of both players (advance), what is missing, why not (Soldados' «esperar», the campaign of before the runs).</summary>
    [Serializable]
    public sealed class WaitDayView
    {
        public string Label;
        public bool Ready;
        /// <summary>The partner has asked: the button in the accent.</summary>
        public bool Asked;
        public bool Enabled = true;
        public string Why;
        public string Note;
        public string Tip;
    }
}
