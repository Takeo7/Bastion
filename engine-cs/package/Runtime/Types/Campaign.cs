#nullable enable
// campaign.ts: the campaign's state, CampaignState and everything that hangs from it, as the host keeps it, saves it
// and sends it whole to both players after every command (the campaign is small and changes rarely, so the server
// broadcasts it whole instead of streaming events like the tactical layer); and the ids it holds. The commands are in
// CampaignCommand.cs; the definitions of its tables and what some of its functions give (FacilityDef, RegionDef,
// EventDef, MissionInfo, NextStep, CampaignContext) in CampaignDefs.cs; what it takes from quests.ts in QuestTypes.cs;
// its tables, constants and rules in Campaign/ (the class Campaign).
//
// As the engine-cs README says, `x?: T` and `x: T | null` are both `T?`, and leave the JSON when null. A field typed
// `true` in TypeScript (`tide?: true`) is a `bool?` that is true or null, never false. A `[boolean, boolean]` is a
// `bool[]` of two, by Slot.
//
// A campaign begun with the 0.7.0 or the 0.7.1 (versions 20 to 22; 20 is FRESH_CAMPAIGN_VERSION, the oldest
// migrateCampaign takes) may still hold, whatever version it was saved with since, things nothing makes since the
// 0.7.2: `base.plots` (always empty), works of kind `clear`, `build` or `upgrade` and proposals of those three (the base
// of before), and rewards whose `recruit` names a class. They are here so those saves read and write as they are.
using System.Collections.Generic;

namespace Bastion.Engine
{
    // ------------------------------------------------------------------ the ids

    /// <summary>RegionId: the zones of the valley of Ossau (docs/FASE5.md), in the order of REGIONS (Campaign.Regions).</summary>
    public static class RegionIds
    {
        /// <summary>Los Llanos de Ceniza, where the Marea comes up from (MAREA_SOURCE).</summary>
        public const string Na = "na";
        /// <summary>Rudnik.</summary>
        public const string Sa = "sa";
        /// <summary>Bielsa, the Bastion's own (HOME_REGION).</summary>
        public const string Weu = "weu";
        /// <summary>Sappada.</summary>
        public const string Eeu = "eeu";
        /// <summary>Tarbes.</summary>
        public const string Afr = "afr";
        /// <summary>Koper.</summary>
        public const string Me = "me";
        /// <summary>El Pantano Gris.</summary>
        public const string Eas = "eas";
        /// <summary>La Vega.</summary>
        public const string Oce = "oce";

        /// <summary>Also `Object.keys(REGIONS)`'s order.</summary>
        public static readonly IReadOnlyList<string> All = new[] { Na, Sa, Weu, Eeu, Afr, Me, Eas, Oce };
    }

    /// <summary>FacilityId: what a building of the base does (FACILITIES, Campaign.Facilities).</summary>
    public static class FacilityIds
    {
        public const string Simulation = "simulation";
        public const string Workshop = "workshop";
        public const string Infirmary = "infirmary";
        public const string Training = "training";
        public const string Relay = "relay";
        public const string Quarters = "quarters";
        public const string Range = "range";
        public const string Garage = "garage";
        public const string Listening = "listening";
        public const string Crew = "crew";
        public const string Echoes = "echoes";
        public const string Memorial = "memorial";
        public const string Garden = "garden";
        public const string Quarantine = "quarantine";

        /// <summary>In the type's order; FACILITIES' keys (Object.keys) go in Campaign.Facilities' own.</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            Simulation, Workshop, Infirmary, Training, Relay, Quarters, Range, Garage, Listening, Crew, Echoes, Memorial,
            Garden, Quarantine,
        };
    }

    /// <summary>
    /// FixedBuildingId: the buildings of before that pieces of the base stand for (BUILDING_PIECES). A BuildingId is one
    /// of these or a FacilityIds value.
    /// </summary>
    public static class FixedBuildingIds
    {
        public const string Command = "command";
        public const string Barracks = "barracks";
        public const string Research = "research";
        public const string Engineering = "engineering";

        public static readonly IReadOnlyList<string> All = new[] { Command, Barracks, Research, Engineering };
    }

    /// <summary>CampaignEventId. `quest`: the current decision of a side quest (`CampaignState.questEvent`), built by `eventDef`.</summary>
    public static class CampaignEventIds
    {
        public const string Smugglers = "smugglers";
        public const string Refugees = "refugees";
        public const string Signal = "signal";
        public const string Convoy = "convoy";
        public const string Defector = "defector";
        public const string BlackMarket = "blackMarket";
        public const string Sabotage = "sabotage";
        public const string Scientist = "scientist";
        public const string Simon = "simon";
        public const string Finale = "finale";
        public const string Quest = "quest";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Smugglers, Refugees, Signal, Convoy, Defector, BlackMarket, Sabotage, Scientist, Simon, Finale, Quest,
        };
    }

    /// <summary>Construction.kind: `'craft'`; saves from 0.7.1 and before (the base of before) may hold the other three.</summary>
    public static class ConstructionKinds
    {
        public const string Craft = "craft";
        /// <summary>The base of before: clearing a plot.</summary>
        public const string Clear = "clear";
        /// <summary>The base of before: a building on a plot.</summary>
        public const string Build = "build";
        /// <summary>The base of before: a building one level up.</summary>
        public const string Upgrade = "upgrade";

        /// <summary>What the type allows now.</summary>
        public static readonly IReadOnlyList<string> All = new[] { Craft };
    }

    /// <summary>ReportSoldier.status. `lost`: left behind when the bus went (docs/PRUEBA-2026-10-14.md P1).</summary>
    public static class ReportStatuses
    {
        public const string Ok = "ok";
        public const string Wounded = "wounded";
        public const string Dead = "dead";
        public const string Lost = "lost";

        public static readonly IReadOnlyList<string> All = new[] { Ok, Wounded, Dead, Lost };
    }

    /// <summary>LogEntry.tone: `'info' | 'good' | 'bad'` (and QuestOutcome.tone, `'good' | 'bad'`).</summary>
    public static class LogTones
    {
        public const string Info = "info";
        public const string Good = "good";
        public const string Bad = "bad";

        public static readonly IReadOnlyList<string> All = new[] { Info, Good, Bad };
    }

    /// <summary>CampaignSoldier.fellCause: why the fallen fell, when it wasn't in combat.</summary>
    public static class FellCauses
    {
        /// <summary>The infection won (TURNED_CAUSE).</summary>
        public const string Turned = "turned";

        public static readonly IReadOnlyList<string> All = new[] { Turned };
    }

    /// <summary>lore.ts Ending: how the final mission was resolved, with both keys (CampaignState.ending; Lore.EndingTitles).</summary>
    public static class Endings
    {
        /// <summary>«Para todos»: the formula read on every frequency.</summary>
        public const string Break = "break";
        /// <summary>«Para quienes cumplieron»: the doses only for those who kept their deals.</summary>
        public const string Record = "record";

        public static readonly IReadOnlyList<string> All = new[] { Break, Record };
    }

    // ----------------------------------------------------------- the compound

    public sealed class Construction
    {
        /// <summary>The Taller's piece on the base, where the work is done.</summary>
        public string Plot { get; set; } = "";
        /// <summary>The Taller makes a tier of armour, a weapon, munition or an improvement (docs/FASE4.md).</summary>
        public string Kind { get; set; } = ConstructionKinds.Craft; // ConstructionKinds
        public string? Facility { get; set; } // FacilityIds
        public int DaysLeft { get; set; }
        public int Total { get; set; }
        /// <summary>What it makes.</summary>
        public string? Project { get; set; } // CraftIds
    }

    /// <summary>
    /// CampaignState.base: `{ works: Construction[] }`. The Taller's projects going on (saves from 0.7.1 and before also
    /// hold `plots`, always empty: the base of before).
    /// </summary>
    public sealed class BaseWorks
    {
        public List<Construction> Works { get; set; } = new List<Construction>();
        /// <summary>The base of before, in saves from 0.7.1 and before: always empty.</summary>
        public List<BasePlot>? Plots { get; set; }
    }

    /// <summary>A plot of the base of before (campaign.ts of the 0.7.1 and before): only in `base.plots` of old saves, which is always empty.</summary>
    public sealed class BasePlot
    {
        public string Id { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public string Terrain { get; set; } = ""; // 'clear' | 'rocks' | 'forest' | 'ruins'
        public string? Building { get; set; } // BuildingId: FixedBuildingIds or FacilityIds
        public int? Level { get; set; }
    }

    // ------------------------------------------------------------------ regions

    public sealed class RegionState
    {
        public string Id { get; set; } = ""; // RegionIds
        public bool Contacted { get; set; }
        /// <summary>Alien facility: pushes the project forward until destroyed.</summary>
        public RegionFacility? Facility { get; set; }
    }

    /// <summary>RegionState.facility: `{ nextDoomDay: number }`, a Nido.</summary>
    public sealed class RegionFacility
    {
        public int NextDoomDay { get; set; }
    }

    // ------------------------------------------------------------------ soldiers

    public sealed class Loadout
    {
        public string? Utility { get; set; } // ItemIds
        public string? Ammo { get; set; } // ItemIds
    }

    public sealed class CampaignSoldier
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Owner { get; set; } // Slot
        public int Kills { get; set; }
        public int Missions { get; set; }
        /// <summary>Fase 10 (skills.ts): experience in each line, from what it does in the missions; the bars follow from it.</summary>
        public Skills Skills { get; set; } = new Skills();
        /// <summary>What each line's filled bars added to its stat, in percent.</summary>
        public Growth Growth { get; set; } = new Growth();
        /// <summary>The cells unlocked on the hex board (board.ts), in the order they were bought; the core isn't listed.</summary>
        public List<string> Board { get; set; } = new List<string>(); // CellId
        public int WoundedDays { get; set; }
        public bool Alive { get; set; }
        /// <summary>Campaign day of the mission the soldier fell in (the Camposanto lists it).</summary>
        public int? FellDay { get; set; }
        public Loadout Loadout { get; set; } = new Loadout();
        /// <summary>Shown in quotes next to the name; empty for none.</summary>
        public string Nickname { get; set; } = "";
        public Appearance Appearance { get; set; } = new Appearance();
        /// <summary>Left by side quests, for good (quests.ts TRAITS).</summary>
        public List<string> Traits { get; set; } = new List<string>(); // TraitIds
        /// <summary>Came back from a mission bitten (docs/FASE3.md): turns on `turnDay` unless treated. Can't deploy meanwhile.</summary>
        public SoldierInfection? Infection { get; set; }
        /// <summary>Why the fallen fell, when it wasn't in combat: 'turned', the infection won (TURNED_CAUSE).</summary>
        public string? FellCause { get; set; } // FellCauses
    }

    /// <summary>CampaignSoldier.infection: `{ since: number }`, the day it came back bitten (not Infection: that is content.ts INFECTION, Content.Infection).</summary>
    public sealed class SoldierInfection
    {
        public int Since { get; set; }
    }

    // ----------------------------------------------------------------- missions

    public sealed class MissionReward
    {
        public int? Credits { get; set; }
        /// <summary>
        /// A recruit joins the Bastion, with no class (saves from before name the class it once came with: just as true).
        /// So it is `true` or, in those saves, a string (a ClassIds value), and comes back as it was: test it with `!= null`.
        /// </summary>
        public object? Recruit { get; set; } // true | ClassIds (campaigns begun before the 0.7.2)
        /// <summary>Slows the enemy project.</summary>
        public int? Doom { get; set; }
        /// <summary>Medicines (✚, docs/FASE3.md).</summary>
        public int? Medicines { get; set; }
        /// <summary>Rations and parts (docs/FASE4.md).</summary>
        public int? Food { get; set; }
        public int? Parts { get; set; }
        /// <summary>A better vehicle for La Lata (Arturo, 18/10 #45): its tier (1 furgoneta … 3 autobús).</summary>
        public int? Vehicle { get; set; }
    }

    public sealed class MissionOffer
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = ""; // MissionKinds
        public int Difficulty { get; set; }
        /// <summary>Last day it can be launched; null for the final mission and facility assaults.</summary>
        public int? ExpiresDay { get; set; }
        public MissionReward Reward { get; set; } = new MissionReward();
        /// <summary>Procedural battlefield (biome + seed), so both players fight on the same map.</summary>
        public MapSource Map { get; set; } = new MapSource();
        public List<List<string>> Pods { get; set; } = new List<List<string>>(); // TemplateIds
        public bool Final { get; set; }
        public string Region { get; set; } = ""; // RegionIds
        /// <summary>Assault on the alien facility of this region.</summary>
        public string? Facility { get; set; } // RegionIds
        /// <summary>Story chapter (1–7, see lore.ts STORY); stays until won.</summary>
        public int? Story { get; set; }
        /// <summary>Side quest whose current step this is; losing it or letting it expire fails the quest.</summary>
        public string? Quest { get; set; } // QuestIds
        /// <summary>Flown at night (byNight): its reward already counts the night's half as much again. Missing: by day.</summary>
        public bool? Night { get; set; }
        /// <summary>Its zone is under the Marea (docs/FASE5.md): one more group of the dead, the last of `pods`; gone when the zone is freed.</summary>
        public bool? Tide { get; set; } // true
        /// <summary>The living hold it (docs/ZOMBIS-PRIMERO.md): their bands among the dead, and their garrison. Missing: the dead's alone.</summary>
        public bool? Living { get; set; } // true
        /// <summary>An ordinary rescue: who is held (playtest 2026-10-15, #20), so the report and the card can name them.</summary>
        public string? Vip { get; set; }
        /// <summary>A sabotage: what makes the noise (#14).</summary>
        public string? Alarm { get; set; } // Alarms
        /// <summary>A siege of the base (docs/FASE12.md §11.5): fought on the base as it stands when it starts (siegeMap), by every soldier fit to.</summary>
        public bool? Siege { get; set; } // true
    }

    // ---------------------------------------------------------------- proposals

    /// <summary>
    /// ProposalAction: what a joint decision would do (Proposal.action), one class per variant, told by `type` as the
    /// commands are. Not the commands themselves: `event` answers the pending event (the command is `answerEvent`), and
    /// `recruit` names whose the recruit is.
    /// </summary>
    public abstract class ProposalAction
    {
        /// <summary>The action's tag (TypeScript `type`).</summary>
        public abstract string Type { get; }
    }

    /// <summary>The base (docs/FASE12.md §11): what is dear goes to a vote.</summary>
    public sealed class PlaceAction : ProposalAction
    {
        public override string Type => "place";
        public string Piece { get; set; } = ""; // PieceIds
        public int X { get; set; }
        public int Y { get; set; }
        public int Rot { get; set; }
    }

    public sealed class RaiseAction : ProposalAction
    {
        public override string Type => "raise";
        /// <summary>The piece on the base (BasePiece.id).</summary>
        public string Piece { get; set; } = "";
    }

    public sealed class PlaceLineAction : ProposalAction
    {
        public override string Type => "placeLine";
        public string Piece { get; set; } = ""; // PieceIds
        public List<Vec2> Cells { get; set; } = new List<Vec2>();
        public int Rot { get; set; }
    }

    public sealed class FellAction : ProposalAction
    {
        public override string Type => "fell";
        public List<Vec2> Tiles { get; set; } = new List<Vec2>();
    }

    public sealed class MendAction : ProposalAction
    {
        public override string Type => "mend";
        /// <summary>The piece on the base (BasePiece.id).</summary>
        public string Piece { get; set; } = "";
    }

    public sealed class RearmAction : ProposalAction
    {
        public override string Type => "rearm";
        /// <summary>The piece on the base (BasePiece.id).</summary>
        public string Piece { get; set; } = "";
    }

    public sealed class DismantleAction : ProposalAction
    {
        public override string Type => "dismantle";
        /// <summary>The piece on the base (BasePiece.id).</summary>
        public string Piece { get; set; } = "";
    }

    public sealed class LevelUpAction : ProposalAction
    {
        public override string Type => "levelUp";
    }

    public sealed class ChooseMissionAction : ProposalAction
    {
        public override string Type => "chooseMission";
        /// <summary>The operation (MissionOffer.id).</summary>
        public string Id { get; set; } = "";
    }

    public sealed class ContactAction : ProposalAction
    {
        public override string Type => "contact";
        public string Region { get; set; } = ""; // RegionIds
    }

    /// <summary>The answer to the pending event (CampaignState.event).</summary>
    public sealed class EventAction : ProposalAction
    {
        public override string Type => "event";
        public int Option { get; set; } // 0 | 1
    }

    public sealed class InvestigateAction : ProposalAction
    {
        public override string Type => "investigate";
        /// <summary>The rumour (Rumor.id).</summary>
        public string Id { get; set; } = "";
    }

    public sealed class CraftAction : ProposalAction
    {
        public override string Type => "craft";
        public string Project { get; set; } = ""; // CraftIds
    }

    /// <summary>The shared supplies go to a vote too (Ana, 21/10 #31): a recruit for `for`, straight into the squad with `squad`; an accessory for the Armería.</summary>
    public sealed class RecruitAction : ProposalAction
    {
        public override string Type => "recruit";
        public int For { get; set; } // Slot
        public bool? Squad { get; set; }
    }

    public sealed class BuyAction : ProposalAction
    {
        public override string Type => "buy";
        public string Item { get; set; } = ""; // ItemIds
    }

    /// <summary>The base of before (campaigns begun before the 0.7.2): clearing a plot.</summary>
    public sealed class ClearAction : ProposalAction
    {
        public override string Type => "clear";
        public string Plot { get; set; } = "";
    }

    /// <summary>The base of before (campaigns begun before the 0.7.2): a facility on a plot.</summary>
    public sealed class BuildAction : ProposalAction
    {
        public override string Type => "build";
        public string Plot { get; set; } = "";
        public string Facility { get; set; } = ""; // FacilityIds
    }

    /// <summary>The base of before (campaigns begun before the 0.7.2): a building one level up.</summary>
    public sealed class UpgradeAction : ProposalAction
    {
        public override string Type => "upgrade";
        public string Plot { get; set; } = "";
    }

    public sealed class Proposal
    {
        public int By { get; set; } // Slot
        /// <summary>Always there (`null!` only until it is set).</summary>
        public ProposalAction Action { get; set; } = null!;
        public string Text { get; set; } = "";
    }

    // ---------------------------------------------------------------- the debrief

    /// <summary>One line of the debrief: how a soldier of the squad came out of the mission.</summary>
    public sealed class ReportSoldier
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Owner { get; set; } // Slot
        /// <summary>`lost`: left behind when the bus went (docs/PRUEBA-2026-10-14.md P1).</summary>
        public string Status { get; set; } = ""; // ReportStatuses
        public int WoundedDays { get; set; }
        public int Kills { get; set; }
        /// <summary>Fase 10 (skills.ts): what the mission taught, line by line, what it did, and the bars that filled (a point each).</summary>
        public Skills? Learned { get; set; }
        public Deeds? Deeds { get; set; }
        public List<BarFilled>? Bars { get; set; }
    }

    public sealed class MissionReport
    {
        /// <summary>Campaign day the mission was flown.</summary>
        public int Day { get; set; }
        public string MissionName { get; set; } = "";
        public bool Victory { get; set; }
        public string Reason { get; set; } = ""; // EndReasons
        public List<ReportSoldier> Squad { get; set; } = new List<ReportSoldier>();
        public MissionReward? Reward { get; set; }
        public int DoomChange { get; set; }
        /// <summary>Bastion experience earned, and the levels before and after.</summary>
        public int Xp { get; set; }
        public int LevelFrom { get; set; }
        public int LevelTo { get; set; }
        /// <summary>Alien bodies sold and what they fetched.</summary>
        public int Bodies { get; set; }
        public int Salvage { get; set; }
        /// <summary>Story chapter the mission belonged to.</summary>
        public int? Story { get; set; }
        /// <summary>What the squad brought back from searching (docs/FASE2.md), already added; missing in older reports.</summary>
        public MissionLoot? Loot { get; set; }
        /// <summary>It was a night mission (docs/ALTURA-Y-NOCHE.md).</summary>
        public bool? Night { get; set; }
        /// <summary>Soldiers (campaign ids) who came back bitten (docs/FASE3.md); missing in older reports.</summary>
        public List<string>? Infected { get; set; }
        /// <summary>Parts stripped from the Acorazados brought down (docs/FASE4.md), already added.</summary>
        public int? Scrap { get; set; }
        /// <summary>A siege of the base (docs/FASE12.md §11.5); `fallen`, the base fell (and the rest fled to another clearing).</summary>
        public bool? Siege { get; set; } // true
        public bool? Fallen { get; set; } // true
        /// <summary>What a siege held left of the base: pieces broken down, pieces beaten, traps and turrets with uses spent (to mend and rearm).</summary>
        public SiegeDamage? Damage { get; set; }
    }

    /// <summary>MissionReport.damage: `{ broken: number; hurt: number; spent: number }`.</summary>
    public sealed class SiegeDamage
    {
        public int Broken { get; set; }
        public int Hurt { get; set; }
        public int Spent { get; set; }
    }

    /// <summary>The haul of a mission: suministros (to the credits), accessories (to the inventory), medicines, rations, parts and found weapons.</summary>
    public sealed class MissionLoot
    {
        public int Supplies { get; set; }
        public List<string> Items { get; set; } = new List<string>(); // ItemIds
        public int Medicines { get; set; }
        /// <summary>Missing in reports from before phase 4.</summary>
        public int? Food { get; set; }
        public int? Parts { get; set; }
        public List<string>? Weapons { get; set; } // FoundWeaponIds
        /// <summary>The debrief's line (lootLine), or null when nothing came back.</summary>
        public string? Line { get; set; }
        // Not in the TypeScript type, but in its JSON: applyMissionResult makes `report.loot` as `{ ...haul, line }`, so
        // LootTotals' own fields come along too.
        /// <summary>LootTotals.arms: the weapons found, as found: their condition and mods (docs/FASE9.md §5).</summary>
        public List<CarriedWeapon>? Arms { get; set; }
        /// <summary>LootTotals.returned: the Armería's own weapons coming back in a backpack (Loot.weaponId), swapped for a find (docs/FASE8.md §C).</summary>
        public List<CarriedWeapon>? Returned { get; set; }
        /// <summary>LootTotals.mods: loose improvements found, for the Armería (docs/FASE9.md §5).</summary>
        public List<string>? Mods { get; set; } // WeaponModIds
    }

    // ---------------------------------------------------------------- the log

    public sealed class LogEntry
    {
        public int Day { get; set; }
        public string Text { get; set; } = "";
        public string Tone { get; set; } = LogTones.Info; // LogTones
        /// <summary>Also shown to the players as a pop-up with this kicker and title; `rumor` lets it offer to listen.</summary>
        public LogAlert? Alert { get; set; }
    }

    /// <summary>LogEntry.alert: `{ kicker: string; title: string; rumor?: string }`.</summary>
    public sealed class LogAlert
    {
        public string Kicker { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>The rumour it offers to listen to (Rumor.id).</summary>
        public string? Rumor { get; set; }
    }

    // ------------------------------------------------------- side quests, rumours

    /// <summary>A side quest under way.</summary>
    public sealed class QuestState
    {
        public string Id { get; set; } = ""; // QuestIds
        /// <summary>Index of the current step in `QUESTS[id].steps`.</summary>
        public int Step { get; set; }
        public string Region { get; set; } = ""; // RegionIds
        /// <summary>Personal quests belong to one commander.</summary>
        public int? Owner { get; set; } // Slot
        /// <summary>Campaign soldier the quest is about.</summary>
        public string? Soldier { get; set; }
        /// <summary>Values for the placeholders of its texts.</summary>
        public Dictionary<string, string> Names { get; set; } = new Dictionary<string, string>();
        /// <summary>Hidden option of its 'guess' decisions (0 or 1).</summary>
        public int Secret { get; set; }
    }

    /// <summary>A value of CampaignState.questLog: `{ outcome: string; day: number }`, how a finished side quest ended, and when.</summary>
    public sealed class QuestLogEntry
    {
        /// <summary>The key of its QuestDef.outcomes (`failure`, `success` or a choice's own).</summary>
        public string Outcome { get; set; } = "";
        public int Day { get; set; }
    }

    /// <summary>Something heard in a region; listening to it for a few days tells what it was.</summary>
    public sealed class Rumor
    {
        public string Id { get; set; } = "";
        public string Region { get; set; } = ""; // RegionIds
        public string Text { get; set; } = "";
        /// <summary>Gone after this day unless the Bastion is listening to it.</summary>
        public int ExpiresDay { get; set; }
        /// <summary>Days of listening still needed.</summary>
        public int DaysLeft { get; set; }
    }

    // ---------------------------------------------------------------- the state

    public sealed class CampaignState
    {
        public int Version { get; set; } // typeof CAMPAIGN_VERSION
        public int Day { get; set; }
        /// <summary>Seats that started the campaign.</summary>
        public List<int> Players { get; set; } = new List<int>(); // Slot
        /// <summary>The only currency: builds, buys accessories, recruits and contacts regions.</summary>
        public int Credits { get; set; }
        /// <summary>Bastion experience from combat, before version 21: its level then came from it (progression.ts).</summary>
        public int Xp { get; set; }
        /// <summary>The Bastion's level, bought (Arturo, 18/10 #40: suministros and missions flown, `levelUp`); since version 21.</summary>
        public int? Level { get; set; }
        /// <summary>La Lata (Arturo, 18/10 #45): 0 todoterreno, 1 furgoneta, 2 camión, 3 autobús (VEHICLE_SEATS); since version 22.</summary>
        public int? Vehicle { get; set; }
        /// <summary>Where each defender stands when the siege begins (Arturo, 18/10 #58), by soldier; the rest, by the Bastion.</summary>
        public Dictionary<string, Vec2>? SiegePosts { get; set; }
        public List<CampaignSoldier> Soldiers { get; set; } = new List<CampaignSoldier>();
        /// <summary>Accessories bought: copies owned (equipped ones included).</summary>
        public Dictionary<string, int> Inventory { get; set; } = new Dictionary<string, int>(); // ItemIds → copies (Partial<Record>)
        /// <summary>Medicines (✚): from searching, some rewards and the enclaves; they treat the infected and heal wounds (docs/FASE3.md).</summary>
        public int? Medicines { get; set; }
        /// <summary>Next day the enclaves send medicines (ENCLAVES).</summary>
        public int? NextMedicineDay { get; set; }
        /// <summary>A treatment one commander has proposed, waiting for both keys (like launching); null for none.</summary>
        public Treatment? Treatment { get; set; }
        // Food and the Taller (docs/FASE4.md); missing before version 13.
        /// <summary>Rations: the Bastion eats every day (dailyRations).</summary>
        public int? Food { get; set; }
        /// <summary>No rations for the day: −10 will on missions and wounds heal half as fast, until there are again.</summary>
        public bool? Hungry { get; set; }
        /// <summary>Next day La Vega sends rations.</summary>
        public int? NextFoodDay { get; set; }
        /// <summary>Parts, for the Taller's work.</summary>
        public int? Parts { get; set; }
        /// <summary>What the Taller has made for everyone (CRAFTS).</summary>
        public List<string>? Crafted { get; set; } // CraftIds
        /// <summary>Good weapons found, kept in the Armería: each for one soldier at most.</summary>
        public List<FoundWeapon>? FoundWeapons { get; set; }
        /// <summary>Loose improvements for the weapons, waiting in the Armería to be mounted (docs/FASE9.md §5).</summary>
        public Dictionary<string, int>? WeaponMods { get; set; } // WeaponModIds → copies (Partial<Record>)
        /// <summary>Zones under the Marea when it was last looked at (syncMarea), for the alerts; missing before version 14.</summary>
        public List<string>? Flooded { get; set; } // RegionIds
        /// <summary>The Taller's projects going on (saves from 0.7.1 and before also hold `plots`, always empty: the base of before).</summary>
        public BaseWorks Base { get; set; } = new BaseWorks();
        /// <summary>The base (docs/FASE12.md §11): the clearing in the pines and what stands on it (base.ts).</summary>
        public BaseState Bastion { get; set; } = new BaseState();
        /// <summary>When the next siege comes, and what the Sala de radio hears of it (§11.5).</summary>
        public SiegeClock Siege { get; set; } = new SiegeClock();
        public List<RegionState> Regions { get; set; } = new List<RegionState>();
        public List<MissionOffer> Offers { get; set; } = new List<MissionOffer>();
        public string? ActiveMission { get; set; }
        public List<string> Squad { get; set; } = new List<string>();
        /// <summary>Missions flown so far: the operations get a little harder with them.</summary>
        public int? MissionsFlown { get; set; }
        /// <summary>Hangar pad of each squad member (squadPads): nobody moves when others come and go.</summary>
        public Dictionary<string, int>? Pads { get; set; }
        public int Doom { get; set; }
        public int NextMissionDay { get; set; }
        public int NextDoomDay { get; set; }
        public int NextFacilityDay { get; set; }
        public int NextSupplyDay { get; set; }
        public int NextEventDay { get; set; }
        /// <summary>Event waiting for an answer; time stands still until then.</summary>
        public string? Event { get; set; } // CampaignEventIds
        public bool[] AdvanceReady { get; set; } = new bool[2]; // [boolean, boolean]: by Slot.
        public bool[] LaunchReady { get; set; } = new bool[2]; // [boolean, boolean]: by Slot.
        public Proposal? Proposal { get; set; }
        public List<LogEntry> Log { get; set; } = new List<LogEntry>();
        public MissionReport? LastReport { get; set; }
        public string? Outcome { get; set; } // Outcomes
        /// <summary>How the final mission was resolved, with both keys.</summary>
        public string? Ending { get; set; } // Endings
        public StoryState Story { get; set; } = new StoryState();
        public List<QuestState> Quests { get; set; } = new List<QuestState>();
        /// <summary>How each finished side quest ended, and when.</summary>
        /// <remarks>A JsMap: a quest that starts again leaves it (`delete`) and comes back last, and questOutcomes walks it in that order.</remarks>
        public JsMap<string, QuestLogEntry> QuestLog { get; set; } = new JsMap<string, QuestLogEntry>(); // QuestIds → (Partial<Record>)
        /// <summary>Side quest whose decision is the pending event (`event: 'quest'`).</summary>
        public string? QuestEvent { get; set; } // QuestIds
        public List<Rumor> Rumors { get; set; } = new List<Rumor>();
        /// <summary>Rumour the Bastion is listening to while time passes.</summary>
        public string? Listening { get; set; }
        public int NextRumorDay { get; set; }
        /// <summary>Technology granted by side quests before its level.</summary>
        public List<string> Techs { get; set; } = new List<string>(); // TechIds
        /// <summary>How many times each event has come up.</summary>
        public Dictionary<string, int> Seen { get; set; } = new Dictionary<string, int>(); // CampaignEventIds → times (Partial<Record>)
        /// <summary>Tells campaigns apart in each browser (scenes already played); missing in campaigns from before it.</summary>
        public string? Uid { get; set; }
        public int NextId { get; set; }
    }

    /// <summary>CampaignState.treatment: `{ soldier: string; ready: [boolean, boolean] }`.</summary>
    public sealed class Treatment
    {
        public string Soldier { get; set; } = "";
        public bool[] Ready { get; set; } = new bool[2]; // [boolean, boolean]: by Slot.
    }

    /// <summary>The next siege of the base (docs/FASE12.md §11.5).</summary>
    public sealed class SiegeClock
    {
        /// <summary>Missions to go before it; at 0, it is the next thing the base does.</summary>
        public int In { get; set; }
        /// <summary>How many of the dead come, and by which sides (DIRS4 indices): what the Sala de radio hears.</summary>
        public int Count { get; set; }
        public List<int> Sides { get; set; } = new List<int>();
        /// <summary>Sieges held so far: each one comes bigger.</summary>
        public int Held { get; set; }
    }

    /// <summary>A weapon found searching (docs/FASE4.md), in the Armería.</summary>
    public sealed class FoundWeapon
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = ""; // FoundWeaponIds
        /// <summary>The soldier who carries it; null, on the rack. Lost with them if they fall.</summary>
        public string? Holder { get; set; }
        /// <summary>0 to 100 (docs/FASE9.md §5); absent, 100. Below 30 it is «Gastada»; at 0, «Rota».</summary>
        public double? Condition { get; set; }
        /// <summary>What is mounted on it, MOD_SLOTS at most.</summary>
        public List<string>? Mods { get; set; } // WeaponModIds
    }

    public sealed class StoryState
    {
        /// <summary>Story chapters won, in order.</summary>
        public int Done { get; set; }
        /// <summary>Answer to Simão's visit, which opens chapter 5.</summary>
        public string? Simon { get; set; } // lore.ts SimonChoice: Lore.SimonChoices; null, not answered yet
        /// <summary>The next chapter can't open before this day: chapters don't chain back to back.</summary>
        public int? NextDay { get; set; }
    }
}
