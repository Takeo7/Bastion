#nullable enable
// campaign.ts: the side quests (quests.ts, Quests.QuestDefs), what can be read of them: the quest of an operation, whether
// one can start, and what the finished ones left (contacts, income, an enemy). Starting, moving on and ending them come
// with the rules.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ----------------------------------------------------------- side quests

        /// <summary>Side quests under way at once.</summary>
        private const int MaxQuests = 3;
        private const int MaxRumors = 2;
        /// <summary>Days a rumour waits for someone to listen.</summary>
        private const int RumorLife = 2;

        private static QuestState? QuestOf(CampaignState c, MissionOffer offer)
        {
            return offer.Quest != null ? c.Quests.FirstOrDefault((q) => q.Id == offer.Quest) : null;
        }

        /// <summary>Not under way, and never finished or finished long enough ago to come back.</summary>
        public static bool QuestAvailable(CampaignState c, string id)
        {
            if (c.Quests.Any((q) => q.Id == id)) return false;
            if (!c.QuestLog.TryGetValue(id, out var done)) return true;
            var retry = Quests.QuestDefs[id].Outcomes.TryGetValue(done.Outcome, out var outcome) ? outcome.Retry : null;
            return retry != null && c.Day >= done.Day + OldDaysToNow(retry.Value);
        }

        /// <summary>How the finished side quests ended.</summary>
        private static List<(string Id, QuestOutcome Outcome)> QuestOutcomes(CampaignState c)
        {
            return c.QuestLog.Keys
                .Select((id) =>
                {
                    var outcomes = Quests.QuestDefs[id].Outcomes;
                    return (id, outcomes.TryGetValue(c.QuestLog[id].Outcome, out var o) ? o : outcomes["failure"]);
                })
                .ToList();
        }

        /// <summary>A region's monthly credits once contacted, with what side quests added or took away.</summary>
        public static int RegionIncome(CampaignState c, string id)
        {
            var extra = QuestOutcomes(c).Where((q) => Quests.QuestDefs[q.Id].Region == id).Sum((q) => q.Outcome.Income ?? 0);
            return Math.Max(0, Regions[id].Income + extra);
        }

        /// <summary>Enemy that side quests left in every operation of a region.</summary>
        private static string? RegionEnemy(CampaignState c, string id)
        {
            foreach (var q in QuestOutcomes(c)) if (Quests.QuestDefs[q.Id].Region == id && !string.IsNullOrEmpty(q.Outcome.Enemy)) return q.Outcome.Enemy;
            return null;
        }
    }
}
