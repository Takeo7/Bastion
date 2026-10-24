using System;
using System.Collections.Generic;

namespace Bastion.UI.Strategy
{
    // The Asentamiento's panels (screen.ts renderFacilities, buildMode.ts palette and pieceCard): on the left Talar and
    // the palette by kind, on the right the Bastion in figures, «Andar por la base» and the card of the piece picked on
    // the clearing. The clearing itself, the piece by the pointer and placing it are Arte's.

    [Serializable]
    public sealed class SettlementView
    {
        public FellView Fell;
        /// <summary>Edificios, then Defensas (with the traps): only what the Bastion's level allows.</summary>
        public List<PaletteGroupView> Groups = new List<PaletteGroupView>();
        /// <summary>A proposal waiting with the partner here: nothing else goes down, said in red over each grid; null, none.</summary>
        public string Pending;
        public string StatsHead;
        public List<BastionStatView> Stats = new List<BastionStatView>();
        /// <summary>«Andar por la base»; null, no button.</summary>
        public string Walk;
        /// <summary>The piece picked on the clearing; null, none.</summary>
        public PieceCardView Card;
    }

    /// <summary>Talar: one tool, its axe, its name and what a tree costs.</summary>
    [Serializable]
    public sealed class FellView
    {
        public string Label;
        public int Supplies;
        /// <summary>«por árbol».</summary>
        public string Per;
        /// <summary>Felling now: the key lit and `Keys` under it.</summary>
        public bool On;
        public string Keys;
        public string Tip;
    }

    [Serializable]
    public sealed class PaletteGroupView
    {
        public string Title;
        public List<PieceTileView> Tiles = new List<PieceTileView>();
        /// <summary>The piece of this group in hand: how to put it down («Barracón: clic para poner…»); null, none.</summary>
        public string Keys;
    }

    /// <summary>A piece of the palette: its picture, its name and its cost under it; what it does on hover.</summary>
    [Serializable]
    public sealed class PieceTileView
    {
        public string Id;
        public string Name;
        public int Supplies;
        public int Parts;
        /// <summary>A unique one already up: dimmed, ticked, not pressed.</summary>
        public bool Built;
        /// <summary>The stores can't pay it: its cost in red.</summary>
        public bool Short;
        /// <summary>In hand now.</summary>
        public bool On;
        /// <summary>A level brought it and this player hasn't looked at it («Nuevo»).</summary>
        public bool New;
        /// <summary>What it is for, and why not (PIECE_TEXT.line, pieceBlocker).</summary>
        public string Tip;
    }

    /// <summary>A figure of the Bastion: its icon, the number and what it is («8/8 Camas»).</summary>
    [Serializable]
    public sealed class BastionStatView
    {
        /// <summary>The icon's id in the table (Resources/HudIcons).</summary>
        public string Icon;
        /// <summary>The icon's own colour («#eab96c» for the rations); null, muted.</summary>
        public string IconColor;
        public string Value;
        public string Label;
        /// <summary>good, warn, bad or null.</summary>
        public string Tone;
    }

    /// <summary>The card of a piece picked on the clearing (pieceCard): what it gives, how it stands, what can be done to it.</summary>
    [Serializable]
    public sealed class PieceCardView
    {
        public string Title;
        public string CloseTip;
        /// <summary>A building's figure at its level, big («+2 camas»); null, `Line` instead.</summary>
        public string Figure;
        public string Line;
        /// <summary>A building's levels in a row, the one it is at lit; empty, none.</summary>
        public List<LadderStepView> Ladder = new List<LadderStepView>();
        /// <summary>Its level's line of the world, in italics.</summary>
        public string LevelLine;
        /// <summary>«Aguante 22/30».</summary>
        public string Holds;
        public string HoldsTip;
        /// <summary>«Usos 0/3»; null, it has none.</summary>
        public string Uses;
        /// <summary>Broken, spent or silent, in red; null, it works.</summary>
        public string State;
        /// <summary>«El Bastión ya está en su nivel más alto.»; null, none.</summary>
        public string Note;
        public List<PieceActionView> Actions = new List<PieceActionView>();
        /// <summary>«Desmontar» pressed once: the question and its two buttons in its place; null, not asked.</summary>
        public PieceConfirmView Confirm;
    }

    [Serializable]
    public sealed class LadderStepView
    {
        /// <summary>«Nivel 1».</summary>
        public string Label;
        /// <summary>«+2»; «—» when the level gives nothing to count.</summary>
        public string Figure;
        public bool Now;
        public bool Past;
    }

    /// <summary>A button of the card: off with its reason under it, or with `Tip` under it.</summary>
    [Serializable]
    public sealed class PieceActionView
    {
        /// <summary>mend, raise, levelUp, rearm, dismantle (asks first).</summary>
        public string Action;
        public string Label;
        public string Why;
        public string Tip;
        /// <summary>On hover; null, `Tip`.</summary>
        public string Hover;
        public bool New;
    }

    [Serializable]
    public sealed class PieceConfirmView
    {
        /// <summary>«¿Desmontar? Se recuperan 4 §.»</summary>
        public string Text;
        public string Yes;
        public string No;
    }
}
