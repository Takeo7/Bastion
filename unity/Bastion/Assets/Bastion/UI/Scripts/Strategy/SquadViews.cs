using System;
using System.Collections.Generic;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Escuadra (screen.ts renderSquad): the squad lined up by La Lata, one's own soldiers to pick on the left, the
    /// partner's on the right, who is ready, the mission and LANZAR at the bottom, a card under each figure.
    /// </summary>
    [Serializable]
    public sealed class SquadView
    {
        /// <summary>«No hay ninguna misión elegida.»: then only this and «Ir al mapa».</summary>
        public string Empty;
        public string MineHead;
        public string MineTip;
        public List<PickRowView> Mine = new List<PickRowView>();
        public string PartnerHead;
        public List<PickRowView> Partner = new List<PickRowView>();
        /// <summary>Each player and whether they are ready (only with both connected); empty, none.</summary>
        public List<ReadyView> Ready = new List<ReadyView>();
        public string Title;
        /// <summary>«DE NOCHE»; null, by day.</summary>
        public string Night;
        /// <summary>The briefing, with **bold** and ==the goal== (offerBriefing).</summary>
        public string Briefing;
        /// <summary>The living's warning (LIVING_TIP), in amber; null, none.</summary>
        public string Warning;
        /// <summary>«LANZAR MISIÓN», «DEFENDER EL BASTIÓN», «A PUNTO».</summary>
        public string Launch;
        public bool LaunchReady;
        /// <summary>The partner is ready and waits for this player: the button glows.</summary>
        public bool LaunchWaiting;
        public bool LaunchEnabled = true;
        public string LaunchWhy;
        /// <summary>«Volver al Bastión»; null for the first story mission.</summary>
        public string Back;
        /// <summary>One card per pad, in order.</summary>
        public List<LineupCardView> Cards = new List<LineupCardView>();
    }

    /// <summary>A soldier's row in a squad list: hexagon, name, rank, a chip (✓, Fiebre, 2 misiones).</summary>
    [Serializable]
    public sealed class PickRowView
    {
        public string Id;
        public string Name;
        public string Accent;
        public float[] Skills = new float[6];
        public int Rank;
        public string Chip;
        /// <summary>good, bad, warn or null.</summary>
        public string ChipTone;
        public string ChipTip;
        public bool In;
        public bool Enabled = true;
        public string Tip;
    }

    [Serializable]
    public sealed class ReadyView
    {
        public string Name;
        public string Color;
        public bool Ready;
        /// <summary>«A punto para salir», «A punto para defender», «Aún elige la escuadra».</summary>
        public string Tip;
    }

    /// <summary>
    /// The card under a figure of the line-up (Escuadra and the debrief): the bust, the name, a line or two under it,
    /// «×» to take them out; an empty pad says «Libre» (and maybe «Reclutar»).
    /// </summary>
    [Serializable]
    public sealed class LineupCardView
    {
        /// <summary>null: an empty pad.</summary>
        public string Id;
        public string Name;
        public string Accent;
        /// <summary>Under the name: «Fogueada»; in the debrief «ILESO», «HERIDAS · 2 MISIONES», «CAÍDO EN COMBATE».</summary>
        public List<string> Lines = new List<string>();
        /// <summary>«CAÍDO»: the card in red.</summary>
        public bool Fallen;
        public bool Removable;
        public bool Ready;
        /// <summary>What the mission taught (learnedTags): «+4 Disparo», «▲ DISPARO +8 %», «+1 Habilidad».</summary>
        public List<LearnedView> Learned = new List<LearnedView>();
        /// <summary>An empty pad: «Libre», a hint under it, or a recruit's button.</summary>
        public string EmptyLabel;
        public string EmptyHint;
        public string Recruit;
        public bool RecruitEnabled = true;
        public string Tip;
    }

    [Serializable]
    public sealed class LearnedView
    {
        /// <summary>skill (a line's tag in its colour), bar (a bar filled: «▲»), point («→ +2 Habilidades»).</summary>
        public string Kind;
        /// <summary>A point's: how many the mission gave.</summary>
        public int Count = 1;
        /// <summary>The line's place in SkillIds.All (its colour); −1, none.</summary>
        public int Line = -1;
        public string Text;
        public string Tip;
    }

    /// <summary>The debrief (screen.ts renderDebrief): the result, the haul, what changed, the lines, the story's page.</summary>
    [Serializable]
    public sealed class DebriefView
    {
        public bool Victory;
        public string Result;
        public string Mission;
        public List<HaulView> Haul = new List<HaulView>();
        public List<ChipView> Events = new List<ChipView>();
        /// <summary>Fallen, the base's damage («En la base: 2 rotas…» with «Construir»), the night.</summary>
        public List<DebriefLineView> Lines = new List<DebriefLineView>();
        /// <summary>«Leer el parte»: the story's page after the mission; null, none.</summary>
        public string Story;
        public string Continue;
        public List<LineupCardView> Cards = new List<LineupCardView>();
    }

    /// <summary>A tile of the haul: its icon and figure; the story's goal first and larger.</summary>
    [Serializable]
    public sealed class HaulView
    {
        public string Icon;
        public string Figure;
        public bool Big;
        /// <summary>A better weapon than the usual (purple).</summary>
        public bool Better;
        public string Tip;
    }

    [Serializable]
    public sealed class DebriefLineView
    {
        public string Text;
        /// <summary>A button at the end of the line (Construir); null, none.</summary>
        public string Button;
        public bool Bad;
    }
}
