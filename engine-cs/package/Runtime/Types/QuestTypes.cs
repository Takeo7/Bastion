#nullable enable
// quests.ts: its types and ids, which the campaign's state holds (QuestState.id, CampaignSoldier.traits, questLog) and
// its rules read. Declared here with campaign.ts's types (Types/Campaign.cs) so the campaign could be ported before
// quests.ts; the data and `fill` are Lore's, against these types (Quests.QuestDefs, Quests.Traits, Quests.Relatives,
// Quests.Suspects, Quests.RumorLines, Quests.Fill).
// quests.ts's own header: side quests, the "encargos" of docs/LORE.md: chains of steps (operations or decisions taken
// with both keys) that end with a unique reward and leave a consequence behind. Data only: campaign.ts starts and runs
// them. Texts may hold placeholders in braces ({soldier}, {relative}, {a}…), filled from the quest's `names` when it
// starts.
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>QuestId: the side quests, in the order of QUESTS.</summary>
    public static class QuestIds
    {
        public const string Faces = "faces";
        public const string Ibarra = "ibarra";
        public const string Amara = "amara";
        public const string Bells = "bells";
        public const string Numbers = "numbers";
        public const string Route50 = "route50";
        public const string Hands = "hands";
        public const string Mole = "mole";
        public const string Mine = "mine";
        public const string Sunken = "sunken";

        public static readonly IReadOnlyList<string> All = new[] { Faces, Ibarra, Amara, Bells, Numbers, Route50, Hands, Mole, Mine, Sunken };
    }

    // ------------------------------------------------------------------- traits

    /// <summary>TraitId: what a quest can leave on a soldier, for good.</summary>
    public static class TraitIds
    {
        public const string Grudge = "grudge";
        public const string Scarred = "scarred";

        public static readonly IReadOnlyList<string> All = new[] { Grudge, Scarred };
    }

    public sealed class TraitDef
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int Aim { get; set; }
        public int Will { get; set; }
    }

    // -------------------------------------------------------------------- steps

    /// <summary>QuestStep: an operation (QuestMission) or a decision (QuestDecision), told by `type`.</summary>
    public abstract class QuestStep
    {
        /// <summary>The step's tag (TypeScript `type`): QuestStepTypes.</summary>
        public abstract string Type { get; }
    }

    /// <summary>QuestStep.type: `'mission' | 'event'`.</summary>
    public static class QuestStepTypes
    {
        public const string Mission = "mission";
        public const string Event = "event";

        public static readonly IReadOnlyList<string> All = new[] { Mission, Event };
    }

    /// <summary>An operation the quest puts on the geoscape.</summary>
    public sealed class QuestMission : QuestStep
    {
        public override string Type => QuestStepTypes.Mission;
        public string Name { get; set; } = "";
        public string Kind { get; set; } = ""; // MissionKinds
        public string Biome { get; set; } = ""; // Biomes
        /// <summary>Added to the difficulty of the day.</summary>
        public int? Difficulty { get; set; }
        /// <summary>Days to launch it before it expires (and the quest fails).</summary>
        public int Days { get; set; }
        public string Briefing { get; set; } = "";
        /// <summary>Recovery: what the squad carries out.</summary>
        public string? Item { get; set; }
        /// <summary>Recovery: the item lies out in the open, not in a building (docs/PRUEBA-2026-10-14.md #27).</summary>
        public bool? Outdoors { get; set; } // true
        /// <summary>Rescue: who waits at the objective.</summary>
        public string? Vip { get; set; }
        /// <summary>Named enemy leading the first group.</summary>
        public QuestLeader? Leader { get; set; }
        /// <summary>The living hold it (docs/ZOMBIS-PRIMERO.md): bands of the Cuervos among the dead. A living leader means it too.</summary>
        public bool? Living { get; set; }
        public MissionReward? Reward { get; set; }
    }

    /// <summary>QuestChoice.then besides an outcome's key.</summary>
    public static class QuestThens
    {
        public const string Next = "next";
        public const string Guess = "guess";
    }

    public sealed class QuestChoice
    {
        public string Label { get; set; } = "";
        public string Effect { get; set; } = "";
        /// <summary>
        /// 'next' moves on to the following step; 'guess' ends the quest well if this
        /// option is the hidden one (`secret`) and badly otherwise; any other value
        /// ends it with that outcome.
        /// </summary>
        public string Then { get; set; } = ""; // QuestThens or a key of QuestDef.outcomes
        /// <summary>Watching someone tells whether they are the hidden one.</summary>
        public bool? Clue { get; set; }
    }

    /// <summary>A decision taken with both keys, shown like any event.</summary>
    public sealed class QuestDecision : QuestStep
    {
        public override string Type => QuestStepTypes.Event;
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public IReadOnlyList<QuestChoice> Options { get; set; } = new QuestChoice[0]; // [QuestChoice, QuestChoice]
    }

    // ----------------------------------------------------------------- outcomes

    public sealed class QuestOutcome
    {
        /// <summary>Logged and shown when the quest ends this way.</summary>
        public string Text { get; set; } = "";
        public string Tone { get; set; } = ""; // LogTones: 'good' | 'bad'
        public int? Credits { get; set; }
        /// <summary>Change of the doom track: negative sets it back.</summary>
        public int? Doom { get; set; }
        public IReadOnlyDictionary<string, int>? Items { get; set; } // ItemIds → copies (Partial<Record>)
        /// <summary>Monthly credits from the quest's region, for good.</summary>
        public int? Income { get; set; }
        /// <summary>Extra regions that can be contacted.</summary>
        public int? Contacts { get; set; }
        /// <summary>Technology granted before its level.</summary>
        public string? Tech { get; set; } // TechIds
        /// <summary>Someone joins the Bastion (owned by the quest's commander, if it has one).</summary>
        public QuestRecruit? Recruit { get; set; }
        /// <summary>Left on the quest's soldier.</summary>
        public string? Trait { get; set; } // TraitIds
        /// <summary>One more of these in the first group of every operation in the region.</summary>
        public string? Enemy { get; set; } // TemplateIds
        /// <summary>Days the ongoing construction is delayed.</summary>
        public int? Delay { get; set; }
        /// <summary>The quest can start again after this many days.</summary>
        public int? Retry { get; set; }
    }

    /// <summary>QuestOutcome.recruit: `{ name?: string; rank?: number; trait?: TraitId }`.</summary>
    public sealed class QuestRecruit
    {
        public string? Name { get; set; }
        public int? Rank { get; set; }
        public string? Trait { get; set; } // TraitIds
    }

    public sealed class QuestDef
    {
        public string Id { get; set; } = ""; // QuestIds
        public string Title { get; set; } = "";
        /// <summary>Who asks: signs its reports.</summary>
        public string Signature { get; set; } = "";
        /// <summary>Logged when it starts.</summary>
        public string Hook { get; set; } = "";
        /// <summary>What finishing it gives, for the briefings.</summary>
        public string Prize { get; set; } = "";
        /// <summary>Where its operations happen and its regional effects apply; null: a contacted region chosen at the start.</summary>
        public string? Region { get; set; } // RegionIds
        /// <summary>Regional quests come out of a rumour in their region once it is contacted; the rest have their own trigger.</summary>
        public bool Rumor { get; set; }
        public IReadOnlyList<QuestStep> Steps { get; set; } = new QuestStep[0];
        /// <summary>`success` closes a quest whose last step is an operation; `failure`, a lost or expired one.</summary>
        public IReadOnlyDictionary<string, QuestOutcome> Outcomes { get; set; } = new Dictionary<string, QuestOutcome>(); // `{ failure: QuestOutcome } & Record<string, QuestOutcome>`
        /// <summary>For quests with a hidden one (`clue` choices): what watching them shows.</summary>
        public QuestClues? Clues { get; set; }
    }

    /// <summary>QuestMission.leader: `{ template: TemplateId; name: string }` (setup.ts MissionOptions.leader, MissionLeader, takes it as it is).</summary>
    public sealed class QuestLeader
    {
        public string Template { get; set; } = ""; // TemplateIds
        public string Name { get; set; } = "";
    }

    /// <summary>QuestDef.clues: `{ guilty: string; innocent: string }`.</summary>
    public sealed class QuestClues
    {
        public string Guilty { get; set; } = "";
        public string Innocent { get; set; } = "";
    }

    // ------------------------------------------------------------------- people

    /// <summary>An element of RELATIVES: `{ name: string; relation: string; sex: 'f' | 'm' }`.</summary>
    public sealed class Relative
    {
        public string Name { get; set; } = "";
        public string Relation { get; set; } = "";
        public string Sex { get; set; } = ""; // Sexes
    }
}
