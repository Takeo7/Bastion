using System;
using System.Collections.Generic;
using Bastion.UI.Gear;

namespace Bastion.UI.Strategy
{
    // The runs' base and map (.dev-snapshots/gameplay/runs/PLAN-RUNS.md, H0-DISENO.md), as plain views: the presenter
    // fills them from the campaign's state, the lab with made-up ones.

    /// <summary>
    /// Almacén (H2): the shared stash on the left, the chosen soldier's kit and pack on the right, the clearing with that
    /// soldier between them (Arte's Soldados framing).
    /// </summary>
    [Serializable]
    public sealed class StashScreenView
    {
        public string Head;
        /// <summary>The supplies in the stash, «230 §» (Gears.Supplies): not money, so no coins (Arturo, 16/11).</summary>
        public string Supplies;
        public string SuppliesTip;
        public GearGridView Stash;
        /// <summary>How things are handled, under the stash.</summary>
        public string Hint;
        /// <summary>The soldiers to choose from: the player's own first, then the partner's (shown only).</summary>
        public List<KitTabView> Soldiers = new List<KitTabView>();
        /// <summary>The chosen one's; null, none chosen.</summary>
        public KitView Kit;
    }

    /// <summary>A soldier to choose in Almacén: name, health, whose.</summary>
    [Serializable]
    public sealed class KitTabView
    {
        public string Id;
        public string Name;
        public bool On;
        public bool Mine;
        /// <summary>The partner's colour on theirs («#rrggbb»); null on one's own.</summary>
        public string Color;
        public string Tip;
        /// <summary>In a run, the health left and the most (Run.Hp, Runs.MaxHp); 0, none shown.</summary>
        public int Hp, HpMax;
        /// <summary>
        /// Between stages (U10, Arturo 18/11): one of this player's with a cell it can learn now (Missions.CanSpendPoints),
        /// Soldados' «▲» on its bust.
        /// </summary>
        public bool Points;
        public string PointsTip;
    }

    /// <summary>A soldier's kit (Kit): its six places and its pack.</summary>
    [Serializable]
    public sealed class KitView
    {
        public string Soldier;
        public string Name;
        /// <summary>Under the name: «9 de 12 de salud».</summary>
        public string Line;
        public bool Mine;
        /// <summary>primary, melee, pistol, vest, belt0, belt1, each with its box.</summary>
        public List<GearSlotView> Slots = new List<GearSlotView>();
        public GearGridView Pack;
        /// <summary>On the partner's: whose it is and that it can't be touched.</summary>
        public string Note;
        /// <summary>
        /// Between stages (U10): Soldados' «Tablero de habilidades» under the name, its points to spend, pressing for
        /// them if one's own; null, none (Almacén).
        /// </summary>
        public string Board;
        public string BoardPoints;
        public bool BoardPulse;
    }

    /// <summary>
    /// The map of an expedition (H1): its rows from the start at the bottom to the boss at the top, the ways between the
    /// stages, where the squad is, what can be chosen now; the squad's kits on the right (between stages); «Volver a la base».
    /// </summary>
    [Serializable]
    public sealed class RunMapView
    {
        /// <summary>Which expedition (its chapter and seed), and whether it hasn't gone anywhere yet: the map builds itself once (T9).</summary>
        public string Run;
        public bool Fresh;
        /// <summary>«Expedición · Capítulo 1».</summary>
        public string Head;
        /// <summary>Under it: what to do now («Elige la siguiente etapa.»).</summary>
        public string Line;
        public List<RunNodeView> Nodes = new List<RunNodeView>();
        public List<RunLinkView> Links = new List<RunLinkView>();
        /// <summary>The start's name under it («La base»).</summary>
        public string Start;
        /// <summary>No stage reached yet: the squad at the start.</summary>
        public bool AtStart;
        /// <summary>«Volver a la base», its help and why not (null, it can): an expedition started before G22's only.</summary>
        public string Back;
        public string BackTip;
        public string BackWhy;
        /// <summary>Where the button went (G22, U21): where one goes back from (Lore.ExpeditionText.ReturnWhere); null, none.</summary>
        public string HomeNote;
        public string SquadHead;
        public List<KitTabView> Squad = new List<KitTabView>();
        public KitView Kit;
        /// <summary>The stop the squad is at (H4), over the map; null, none.</summary>
        public StopView Stop;
    }

    /// <summary>A stage on the map: where it sits, what it is (if it is seen), its weather or wake, how far the squad got.</summary>
    [Serializable]
    public sealed class RunNodeView
    {
        public string Id;
        /// <summary>1–7, from the bottom; its place in its row of `Cols`.</summary>
        public int Row, Col, Cols;
        /// <summary>Not seen yet: dark, without kind nor modifier (RunMap.Visible).</summary>
        public bool Hidden;
        public string Name;
        /// <summary>The HUD's icon of its kind.</summary>
        public string Icon;
        /// <summary>A stop (no fight): drawn apart from the fights.</summary>
        public bool Stop;
        public bool Boss;
        /// <summary>Passed by the squad; where it is now; one it can go to now.</summary>
        public bool Done, At, Open;
        /// <summary>Night, storm, the horde awake: its name and icon; null, none.</summary>
        public string Modifier;
        public string ModifierIcon;
        /// <summary>Modifiers: night, storm, awake (its badge's colour).</summary>
        public string ModifierId;
        /// <summary>One an event left (H4: RunNode.Added, or RunState.Pending on the fights to choose): a second badge.</summary>
        public string AddedId;
        public string AddedIcon;
        /// <summary>A special map (the hospital, the offices…: Arturo, 17/11): its place's drawing as a third badge; null, none.</summary>
        public string SiteIcon;
        /// <summary>A line under its name, in red: a Rescate with no seat left in La Lata (N2, 17/11: Lore.RescueNoSeatTag).</summary>
        public string Warn;
        /// <summary>One it can go to, but can't now (ChooseNodeRefusal); null, it can.</summary>
        public string Why;
        /// <summary>Its card on hover after the name: what it is, where, how hard, its modifier.</summary>
        public List<string> Card = new List<string>();
    }

    /// <summary>A way between two stages (From null: from the start).</summary>
    [Serializable]
    public sealed class RunLinkView
    {
        public string From, To;
        /// <summary>The way the squad took; the ways it can take now; to a stage not seen yet.</summary>
        public bool Taken, Open, Dim;
    }

    /// <summary>
    /// A stop on the way (H4), over the map: the Refugio's rest, the Trueque's wares, a Suceso's two options, a
    /// Superviviente asking to come; what happened once answered; «Seguir» (with two, once both have asked).
    /// </summary>
    [Serializable]
    public sealed class StopView
    {
        /// <summary>refuge, trade, event, survivor.</summary>
        public string Kind;
        /// <summary>The stop's kind over the title («TRUEQUE»).</summary>
        public string Kicker;
        public string Title;
        public string Text;
        /// <summary>An event's two, a survivor's yes and no; none once answered.</summary>
        public List<StopOptionView> Options = new List<StopOptionView>();
        /// <summary>What happened; null, not answered yet.</summary>
        public string Outcome;
        /// <summary>The Trueque: what is on offer, and the supplies the player's soldiers carry.</summary>
        public List<WareView> Wares = new List<WareView>();
        public string Purse;
        /// <summary>Whose pack a ware goes into («Va a la mochila de Rafael»: the soldier chosen on the right).</summary>
        public string For;
        /// <summary>That only what the soldiers carry counts on an expedition, not the stash (B18).</summary>
        public string PurseNote;
        /// <summary>«Seguir»; on, this player has asked; why not yet (answer first); the partner awaited.</summary>
        public string Leave;
        public bool LeaveOn;
        public string LeaveWhy;
        /// <summary>A Refugio's «Volver a la base» (G22, U21), after «Seguir»: asked first (EndAsk); why not; null, none.</summary>
        public string Home;
        public string HomeTip;
        public string HomeWhy;
        public string Waiting;
        /// <summary>With two: the answer goes to a vote (the plate shows it).</summary>
        public string Vote;
    }

    [Serializable]
    public sealed class StopOptionView
    {
        public int Index;
        public string Label;
        /// <summary>What it does, short, beside the button («+25 § · horda despierta en la siguiente etapa»).</summary>
        public string Short;
        public string Why;
    }

    /// <summary>A ware of the Trueque: the thing, what it costs, why it can't be had now.</summary>
    [Serializable]
    public sealed class WareView
    {
        public GearItemView Gear;
        public string Price;
        public string Why;
    }

    /// <summary>Mejoras (H2): the base's six, each with its level, what the next costs and «Mejorar» (a vote with two).</summary>
    [Serializable]
    public sealed class UpgradesView
    {
        public string Head;
        public string Supplies;
        public string SuppliesTip;
        public List<UpgradeRowView> Rows = new List<UpgradeRowView>();
    }

    [Serializable]
    public sealed class UpgradeRowView
    {
        public string Id;
        public string Name;
        /// <summary>What the next level does (or, at the top, what it does now).</summary>
        public string Line;
        public int Level, Levels;
        /// <summary>The level in words beside its marks (B12): «Sin mejorar», «Nivel 1 de 3», «Nivel 3 de 3: al máximo».</summary>
        public string LevelText;
        /// <summary>«120 §»; null at the top.</summary>
        public string Price;
        /// <summary>«Mejorar», «Al máximo».</summary>
        public string Button;
        /// <summary>Why not now (Runs.UpgradeRefusal); null, it can.</summary>
        public string Why;
        public string Tip;
    }
}
