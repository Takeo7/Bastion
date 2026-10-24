using System;
using System.Collections.Generic;

namespace Bastion.UI.Strategy
{
    // What the base's screens show (strategy/screen.ts of the 0.10.1, docs/UNITY-ESTRATEGIA.md), as plain views: the
    // presenter fills them from the campaign's state (once campaign.ts is in C#), the lab with made-up ones. Each piece
    // draws again only when its view changes (Ui.Key).

    /// <summary>The top strip (renderTop): the brand and outings, the siege, the day, the stores, the level, the Marea.</summary>
    [Serializable]
    public sealed class TopView
    {
        /// <summary>Missions flown («8 SALIDAS»); 0 shows none.</summary>
        public int Outings;
        public string OutingsTip;
        /// <summary>The siege's badge («ASEDIO EN 3 MISIONES», «ASEDIO · NOCHE»); null, none.</summary>
        public string Siege;
        /// <summary>The siege is on (it pulses); near, a mission or less away.</summary>
        public bool SiegeNow;
        public bool SiegeNear;
        public string SiegeTip;
        public int Day;
        public string DayTip;
        /// <summary>Suministros, medicinas, raciones, piezas: each in its well.</summary>
        public List<StoreView> Stores = new List<StoreView>();
        /// <summary>The Bastion's level («CASA MÓVIL») and the missions towards the next.</summary>
        public string Level;
        public int LevelFilled;
        public int LevelTotal;
        public string LevelTip;
        /// <summary>The Marea («INFESTACIÓN»): its marks filled up to `DoomFilled` of `DoomMax`, the figure («5/12», «7 km»).</summary>
        public string Doom;
        public int DoomFilled;
        public int DoomMax;
        public string DoomFigure;
        public string DoomTip;
    }

    /// <summary>A store in the top strip: its icon, its figure, what a pending proposal would spend, hungry or not.</summary>
    [Serializable]
    public sealed class StoreView
    {
        /// <summary>credits, medicines, food, parts: its icon and colour.</summary>
        public string Id;
        public string Value;
        /// <summary>«−30» while a proposal would spend it; null, nothing pending.</summary>
        public string Owed;
        public string OwedTip;
        public bool Bad;
        /// <summary>The tooltip's name and its sentences (cifras resaltadas).</summary>
        public string TipName;
        public List<string> Tip = new List<string>();
    }

    /// <summary>The tabs (renderNav).</summary>
    [Serializable]
    public sealed class NavView
    {
        /// <summary>Hidden on the squad and the debrief.</summary>
        public bool Hidden;
        public List<TabView> Tabs = new List<TabView>();
    }

    [Serializable]
    public sealed class TabView
    {
        /// <summary>geoscape, engineering, barracks, facilities, squad.</summary>
        public string Id;
        public string Label;
        public bool On;
        /// <summary>The squad's tab, red until visited.</summary>
        public bool Urgent;
        /// <summary>News not looked at yet («2», «▲1» for talents to choose); null, none.</summary>
        public string Count;
        public bool CountUrgent;
        /// <summary>Something a level brought, not looked at yet («Nuevo»).</summary>
        public bool New;
        /// <summary>The partner is on this tab: their initial in their colour.</summary>
        public string Partner;
        public string PartnerColor;
        public string Tip;
    }

    /// <summary>A proposal waiting for both keys (renderProposal): whose, what, what it costs, its detail.</summary>
    [Serializable]
    public sealed class ProposalView
    {
        public bool Mine;
        /// <summary>The partner's name and colour (who proposed, or who must answer).</summary>
        public string Partner;
        public string PartnerColor;
        /// <summary>The engine's sentence: «reclutar un soldado».</summary>
        public string Text;
        /// <summary>«30 §»: paid on the yes; null, free.</summary>
        public string Cost;
        public List<RowView> Rows = new List<RowView>();
        /// <summary>«Ver» goes where it is (a piece on the clearing, a zone on the map…).</summary>
        public bool CanSee;
    }

    /// <summary>A key and its value in a detail list (Suministros: 30 §).</summary>
    [Serializable]
    public sealed class RowView
    {
        public string Key;
        public string Value;
    }

    /// <summary>«Te esperan»: the partner is ready to go and this player isn't (renderWaiting); null text, none.</summary>
    [Serializable]
    public sealed class WaitingView
    {
        public string Text;
        public string Button;
    }

    /// <summary>
    /// The window over the base, one at a time (renderModal): «event» (two options, maybe proposed), «alert» (the
    /// diary and the siege's warnings, with what a level brought), «end» (the campaign is over), «leave» and «quit»
    /// (questions). The menu and Ajustes are the combat's own (GameMenu).
    /// </summary>
    [Serializable]
    public sealed class StrategyModalView
    {
        public string Kind;
        public string Kicker;
        /// <summary>good, bad or null.</summary>
        public string Tone;
        public string Title;
        /// <summary>Paragraphs, with **bold** and ==the goal== marked (marked.ts).</summary>
        public List<string> Body = new List<string>();
        public string Text;
        /// <summary>The handwritten note under it («Las sirenas primero. Siempre. — N. A.»).</summary>
        public string Note;
        /// <summary>What a level brought, in groups (broughtList).</summary>
        public List<BroughtGroupView> Brought = new List<BroughtGroupView>();
        /// <summary>An event's two options.</summary>
        public List<OptionView> Options = new List<OptionView>();
        /// <summary>Under an event's options: «El tiempo no avanza hasta que decidáis.», or the vote.</summary>
        public string Foot;
        public ProposalView Vote;
        /// <summary>The buttons, the first the main one (Entendido, Volver al menú…).</summary>
        public List<string> Buttons = new List<string>();
    }

    [Serializable]
    public sealed class OptionView
    {
        public string Label;
        public string Effect;
        /// <summary>«Ahora no está a vuestro alcance»; null, it can be chosen.</summary>
        public string Blocked;
        /// <summary>The option on the table (proposed).</summary>
        public bool Proposed;
    }

    [Serializable]
    public sealed class BroughtGroupView
    {
        /// <summary>«Para construir», «Suben de nivel», «En la Armería», «Avances».</summary>
        public string Title;
        public List<BroughtView> Items = new List<BroughtView>();
    }

    [Serializable]
    public sealed class BroughtView
    {
        /// <summary>The icon's id in the table (Resources/HudIcons).</summary>
        public string Icon;
        public string Name;
        public string Brief;
    }
}
