#nullable enable
// The runs (.dev-snapshots/gameplay/runs/H0-DISENO.md §4; new in C#): a chapter's node map, the run going on in it and
// the stops on the way, the base's upgrades, and the joint decisions of the runs (RunAction). The map is RunMap's; the
// rules and the commands, Runs'.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>RunNode.kind: what a node of the map is.</summary>
    public static class NodeKinds
    {
        /// <summary>No objective: search and leave on La Lata.</summary>
        public const string Normal = "normal";
        // Specials, today's objectives (MissionKinds).
        public const string Elimination = "elimination";
        public const string Recovery = "recovery";
        public const string Sabotage = "sabotage";
        public const string Rescue = "rescue";
        // New ones (H5): not on the maps until their rules are there (RunMap.Specials).
        public const string Hold = "hold";
        public const string Crossing = "crossing";
        public const string Stealth = "stealth";
        public const string Elite = "elite";
        /// <summary>
        /// G23.1 La llave (Arturo, 18/11, cuadros 71, 73 y 79): a Saqueo at a police station whose armoury is locked; its key on
        /// one of the dead in uniform; its furniture all full (MissionOptions.KeyArmory).
        /// </summary>
        public const string Key = "key";
        /// <summary>G23.2 Mordido (Arturo, 19/11, cuadros 73 y 87): a Rescate whose survivor is bitten; always at the hospital.</summary>
        public const string Bitten = "bitten";
        /// <summary>
        /// G23.3 El herido (Arturo, cuadros 71, 73 y 92): a Rescate whose survivor can't walk; carried to La Lata, turn about,
        /// and La Lata doesn't go without them alive (MissionState.Wounded).
        /// </summary>
        public const string Wounded = "wounded";
        /// <summary>
        /// G23.4 Separados (Arturo, cuadros 71 y 94): a Saqueo whose squad starts in two groups on either side of La Lata; its
        /// reward with one of each aboard (MissionState.Split). Only on open maps (RunMap.SplitSites).
        /// </summary>
        public const string Split = "split";
        /// <summary>
        /// G23.5 Saqueadores (Arturo, cuadro 71; .dev-snapshots/gameplay/g23/SAQUEADORES.md): a Saqueo with a camp of the living
        /// on the map, their stash in three crates to rob, unseen or by fighting (MissionState.Raiders). Only out of doors with
        /// room for it (RunMap.RaidersSites).
        /// </summary>
        public const string Raiders = "raiders";
        /// <summary>
        /// G23.6 Evacuación (Arturo, cuadros 71 and 100; .dev-snapshots/gameplay/g23/EVACUACION.md): a Saqueo with four civilians
        /// hidden in buildings, each in a circle to look in; found, they walk to La Lata by themselves, slowly and in panic now
        /// and then; won with two aboard, 20 § more for each one past two (MissionState.Civilians). Only where there are
        /// buildings enough (RunMap.EvacuationSites).
        /// </summary>
        public const string Evacuation = "evacuation";
        /// <summary>
        /// G23 Las cargas (Arturo, cuadros 105 and 109; .dev-snapshots/gameplay/g23/CARGAS.md): a Saqueo with a nest of the dead to
        /// blow up, three charges carried in the packs to set at its three points; the last one set, a count of
        /// Setup.ChargesTurns, and La Lata goes as they blow (MissionState.Charges).
        /// </summary>
        public const string Charges = "charges";
        // Stops, no fight (H4).
        public const string Refuge = "refuge";
        public const string Trade = "trade";
        public const string Event = "event";
        public const string Boss = "boss";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Normal, Elimination, Recovery, Sabotage, Rescue, Hold, Crossing, Stealth, Elite, Key, Bitten, Wounded, Split, Raiders,
            Evacuation, Charges, Refuge, Trade, Event, Boss,
        };

        /// <summary>The stops: no fight, the squad answers them on the map.</summary>
        public static readonly IReadOnlyList<string> Stops = new[] { Refuge, Trade, Event };

        public static bool IsStop(string kind) => kind == Refuge || kind == Trade || kind == Event;
    }

    /// <summary>RunNode.modifier: what makes a fight harder (and richer), seen before choosing it.</summary>
    public static class Modifiers
    {
        public const string Night = "night";
        public const string Storm = "storm";
        /// <summary>The horde awake: every group awake and a wave at the start (new in H3).</summary>
        public const string Awake = "awake";

        public static readonly IReadOnlyList<string> All = new[] { Night, Storm, Awake };
    }

    public sealed class RunNode
    {
        /// <summary>"n3-1": row 3, column 1.</summary>
        public string Id { get; set; } = "";
        /// <summary>1–7: 1–5 nodes, 6 the Refuge, 7 the boss.</summary>
        public int Row { get; set; }
        /// <summary>0–3, its place in its row, for drawing.</summary>
        public int Col { get; set; }
        public string Kind { get; set; } = ""; // NodeKinds
        /// <summary>Ids in the next row, in column order (no two links cross).</summary>
        public List<string> Next { get; set; } = new List<string>();
        /// <summary>Seen before choosing; only on fights.</summary>
        public string? Modifier { get; set; } // Modifiers
        /// <summary>What kind of place a fight is (Places ids: town, city, estate, factory, roadside, country); decides map and loot.</summary>
        public string? Site { get; set; } // Places
        /// <summary>The map's seed of a fight (RunMap.MapOf: with the site, the map).</summary>
        public int? Seed { get; set; }
        /// <summary>A modifier an event left for it (RunState.pending, H4), besides its own: set when it is chosen.</summary>
        public string? Added { get; set; } // Modifiers
        /// <summary>Rows 1–2: 1, 3–4: 2, 5 and the boss: 3; +1 per chapter after the first.</summary>
        public int Difficulty { get; set; }
        /// <summary>
        /// A Rescate with Cuervos (N2, Arturo 17/11: «a veces»): the survivor their prisoner, groups of the living round
        /// them (RunMap.GuardedRescue); null, hidden alone among the dead. Lore.StageLine's `crows`.
        /// </summary>
        public bool? Guarded { get; set; }
    }

    /// <summary>RunState.phase.</summary>
    public static class RunPhases
    {
        /// <summary>On the node map, picking the next node (or going back).</summary>
        public const string Choose = "choose";
        /// <summary>A combat node is being played.</summary>
        public const string Fight = "fight";
        /// <summary>At a stop, answering it.</summary>
        public const string Stop = "stop";

        public static readonly IReadOnlyList<string> All = new[] { Choose, Fight, Stop };
    }

    /// <summary>The run going on (CampaignState.run): its map, where the squad is and how it is.</summary>
    public sealed class RunState
    {
        public int Chapter { get; set; }
        public int Seed { get; set; }
        public List<RunNode> Nodes { get; set; } = new List<RunNode>();
        /// <summary>The node last reached; null before the first.</summary>
        public string? At { get; set; }
        public List<string> Path { get; set; } = new List<string>();
        public string Phase { get; set; } = RunPhases.Choose; // RunPhases
        public List<string> Squad { get; set; } = new List<string>();
        /// <summary>Health each soldier of the squad has left; full at the start, carried node to node.</summary>
        public Dictionary<string, int> Hp { get; set; } = new Dictionary<string, int>();
        public StopState? Stop { get; set; }
        /// <summary>
        /// Soldiers handed to a player who joined the run with none of theirs in it (Runs.HandOver): soldier id → slot.
        /// They command them till the run ends.
        /// </summary>
        public Dictionary<string, int>? Hands { get; set; } // soldier id → Slot
        /// <summary>The events met in this run (H4), so that none comes twice.</summary>
        public List<string>? Events { get; set; }
        /// <summary>A modifier an event left for the next fight chosen (H4: «la siguiente etapa, de noche»).</summary>
        public string? Pending { get; set; } // Modifiers
        /// <summary>
        /// G22 (Arturo, 18/11, cuadro 72): a run started since goes home only from a Refuge (or the boss dead: the chapter
        /// done); missing, one started before, from the map after any stage as it did (Runs.EndRunRefusal).
        /// </summary>
        public bool? HomeFromRefuge { get; set; }
    }

    /// <summary>The stop the squad is at (RunState.stop).</summary>
    public sealed class StopState
    {
        /// <summary>Trade: what is on sale; a ware bought leaves it.</summary>
        public List<Ware>? Wares { get; set; }
        /// <summary>Event: its id (Content.RunEvents; Lore's texts by the same id).</summary>
        public string? Event { get; set; }
        /// <summary>What happened once answered (an event's), as an outcome id ("refuge.rest", "herido.0"…); null till then.</summary>
        public string? Outcome { get; set; }
        /// <summary>Who has asked to go on (LeaveStopCommand), by slot: with two, it counts when both have.</summary>
        public bool[] Leaving { get; set; } = new bool[2]; // [boolean, boolean]: by Slot.
        /// <summary>An event answered (H4): what Lore's line needs (who it fell to, who joined) and whether something was lost.</summary>
        public EventResult? Result { get; set; }
    }

    /// <summary>
    /// StopState.result (H4): an event's option as it went. Lore.RunEvents[event].Options[option].Result(soldierName,
    /// survivorName) is its line, and Lore.RunEventLost goes after it when `lost`.
    /// </summary>
    public sealed class EventResult
    {
        public string Event { get; set; } = "";
        public int Option { get; set; }
        /// <summary>The soldier «a uno» fell to ({Nombre}), id and name.</summary>
        public string? Soldier { get; set; }
        public string? SoldierName { get; set; }
        /// <summary>Who joined ({Superviviente}), id and name, and whether into the squad (false: at the base, no seat).</summary>
        public string? Survivor { get; set; }
        public string? SurvivorName { get; set; }
        public bool? InSquad { get; set; }
        /// <summary>The node an event made a Rescate (N2, RunEventOption.Rescue), its id.</summary>
        public string? Rescue { get; set; }
        /// <summary>What was won fitted in no pack: lost there.</summary>
        public bool? Lost { get; set; }
    }

    /// <summary>A thing on sale at a Trade: its gear, and its price in § with the haggle upgrade already off.</summary>
    public sealed class Ware
    {
        public Gear Gear { get; set; } = new Gear();
        public int Price { get; set; }
    }

    /// <summary>The base's upgrades (CampaignState.upgrades, Content.UpgradePrices), paid with the stash's supplies.</summary>
    public static class UpgradeIds
    {
        /// <summary>Soldiers who go out: 4 → 6 → 8.</summary>
        public const string Squad = "squad";
        /// <summary>The stash: +2 rows each.</summary>
        public const string Stash = "stash";
        /// <summary>Every pack: +1 row each (4×3 → 4×4 → 4×5).</summary>
        public const string Pack = "pack";
        /// <summary>Heals 2 to everyone after each node.</summary>
        public const string Mend = "mend";
        /// <summary>One more row of the map in sight each.</summary>
        public const string Sight = "sight";
        /// <summary>The Trade 20 % cheaper.</summary>
        public const string Haggle = "haggle";

        public static readonly IReadOnlyList<string> All = new[] { Squad, Stash, Pack, Mend, Sight, Haggle };
    }

    // ------------------------------------------------------- the runs' votes

    /// <summary>A joint decision of the runs (H0-DISENO §5): proposed to the partner, or applied at once alone (Runs.Propose).</summary>
    public abstract class RunAction : ProposalAction
    {
    }

    /// <summary>
    /// UpgradeCommand's. Its tag is not "upgrade": that one is the base of before's (UpgradeAction), still read in old saves.
    /// </summary>
    public sealed class BuyUpgradeAction : RunAction
    {
        public override string Type => "buyUpgrade";
        public string Upgrade { get; set; } = ""; // UpgradeIds
    }

    public sealed class StartRunAction : RunAction
    {
        public override string Type => "startRun";
    }

    public sealed class ChooseNodeAction : RunAction
    {
        public override string Type => "chooseNode";
        /// <summary>RunNode.id.</summary>
        public string Node { get; set; } = "";
    }

    public sealed class EndRunAction : RunAction
    {
        public override string Type => "endRun";
    }

    public sealed class AnswerStopAction : RunAction
    {
        public override string Type => "answerStop";
        /// <summary>The stop it answers (RunState.at when proposed): an answer left over from another stop is refused.</summary>
        public string Node { get; set; } = "";
        /// <summary>0 or 1: an event's option; a survivor's 0 yes, 1 no.</summary>
        public int Option { get; set; }
    }

    // ----------------------------------------------------------- a node's fight

    /// <summary>
    /// GameState.run (H3): the fight is a run's node. Gear instead of loot, packs as grids, nothing wears. Its gear ids go
    /// on from the campaign's counter: the next found is `g{NextGear + 1}`, and ApplyMissionResult hands NextGear back.
    /// </summary>
    public sealed class RunFight
    {
        /// <summary>RunNode.id.</summary>
        public string Node { get; set; } = "";
        public string NodeKind { get; set; } = ""; // NodeKinds
        public int Row { get; set; }
        public string? Modifier { get; set; } // Modifiers
        /// <summary>The counter when the fight began: gear with a higher number was found in it.</summary>
        public int FirstGear { get; set; }
        /// <summary>The counter now.</summary>
        public int NextGear { get; set; }
        /// <summary>A modifier an event left for this fight (RunNode.added, H4), besides the node's own.</summary>
        public string? Added { get; set; } // Modifiers
        /// <summary>N9: the game's level's share of what is found and paid (Levels.Def.Loot); missing, 1.</summary>
        public double? Loot { get; set; }
    }

    /// <summary>RunReport.ended: how the run ended, when the fight ended it.</summary>
    public static class RunEnds
    {
        /// <summary>Back to the base without the chapter: the boss left alive.</summary>
        public const string Returned = "returned";
        /// <summary>The boss dead: the chapter done.</summary>
        public const string Boss = "boss";
        /// <summary>The whole squad fell: what they carried is lost.</summary>
        public const string Wiped = "wiped";

        public static readonly IReadOnlyList<string> All = new[] { Returned, Boss, Wiped };
    }

    /// <summary>MissionReport.run (H3): a node's fight as it came back.</summary>
    public sealed class RunReport
    {
        public string Node { get; set; } = "";
        public string NodeKind { get; set; } = ""; // NodeKinds
        /// <summary>Gear found in the fight that came home in a kit or a pack.</summary>
        public List<Gear> Found { get; set; } = new List<Gear>();
        /// <summary>Those who fell, and what they carried that nobody picked up.</summary>
        public List<RunFallen> Fallen { get; set; } = new List<RunFallen>();
        /// <summary>Health left, by soldier, after the fight (and «mend»).</summary>
        public Dictionary<string, int> Hp { get; set; } = new Dictionary<string, int>();
        /// <summary>A special won: its reward in §, and what of it found no room and was lost.</summary>
        public int? Reward { get; set; }
        public int? RewardLost { get; set; }
        /// <summary>The run ended with the fight (RunEnds); null, it goes on.</summary>
        public string? Ended { get; set; } // RunEnds
        /// <summary>A survivor brought out on La Lata (H4, N2: a Rescate's or one loose): the soldier they are now (CampaignSoldier.id).</summary>
        public string? Joined { get; set; }
        /// <summary>
        /// A survivor brought out with no bed for them (every player at the soldiers' cap; cuadro 140, Producto's review of
        /// 24/11): they don't stay, and what they carried goes into the squad's packs. Null: none.
        /// </summary>
        public RunNoBed? NoBed { get; set; }
        /// <summary>
        /// The run ended with the fight and gave recruits for nothing (Runs.NobodyLeft, a player left with nobody; since N8,
        /// only on a bench): their ids. For the line of whoever comes into the room after it (Red, N9). Null: none.
        /// </summary>
        public List<string>? Recruits { get; set; }
        /// <summary>Whether they came into the squad; false, waiting at the base (no seat).</summary>
        public bool? InSquad { get; set; }
        /// <summary>A Sigilo (H5): unseen (its reward doubled) or seen (halved).</summary>
        public bool? Unseen { get; set; }
        /// <summary>The boss (H6): the weapon it gave besides the §, and whether it found no room and was lost.</summary>
        public List<Gear>? Prize { get; set; }
        public bool? PrizeLost { get; set; }
        /// <summary>
        /// The fight's number among the campaign's nodes (CampaignState.NodesPlayed when it was fought), which a stop after it
        /// doesn't change: what tells apart two reports of the same node and outcome (UI's BaseController.ReportKey, U8, 17/11).
        /// Null in the reports from before it.
        /// </summary>
        public int? Stage { get; set; }
    }

    /// <summary>
    /// RunReport.noBed: a survivor who doesn't stay, with no bed for them. What they left in the squad's packs (all but their
    /// knife; what the fight found of it is in Found too), and what found no room there, lost: both empty, they carried
    /// nothing (Lore's line, StrategyText.Runs.NoBedLine).
    /// </summary>
    public sealed class RunNoBed
    {
        public string Name { get; set; } = "";
        public List<Gear> Gave { get; set; } = new List<Gear>();
        public List<Gear> Lost { get; set; } = new List<Gear>();
    }

    /// <summary>RunReport.fallen: a soldier who fell, and the gear lost with them.</summary>
    public sealed class RunFallen
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<Gear> Lost { get; set; } = new List<Gear>();
    }
}
