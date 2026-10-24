#nullable enable
// campaign.ts: what can be read of the operations (night, winter, the enemy's veterans, what the battlefield names, the
// briefing and who signs it) and of the soldiers' veterancy and learning. Making the offers (makeOffer and the rest)
// comes with the rules.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        // ----------------------------------------------------------------- missions

        /// <summary>Every 4 missions flown the enemy sends one more to an operation, up to 2.</summary>
        public static int VeteranEnemies(CampaignState c)
        {
            return Math.Min(2, (c.MissionsFlown ?? 0) / 4);
        }

        /// <summary>About one operation in three is flown at night (docs/ALTURA-Y-NOCHE.md), as its battlefield's seed says.</summary>
        public static bool NightFor(int seed)
        {
            // Math.imul(seed ^ 0x9e3779b9, 0x85ebca6b) >>> 0: the low 32 bits of the product, unsigned.
            return unchecked(((uint)seed ^ 0x9e3779b9u) * 0x85ebca6bu) % 3 == 0;
        }

        /// <summary>
        /// An offer whose seed says night becomes a night mission, paying half as much
        /// again in suministros (NIGHT.supplies). Story chapters and the final mission
        /// are flown by day.
        /// </summary>
        /// <remarks>A new offer, as `{ ...o, night: true, reward: { ...o.reward, ...credits } }`: what it doesn't change is shared with `o`.</remarks>
        public static MissionOffer ByNight(MissionOffer o)
        {
            if (o.Final || (o.Story ?? 0) != 0 || o.Map.Kind != MapSourceKinds.Generated || !NightFor(o.Map.Seed ?? 0)) return o;
            var reward = CopyReward(o.Reward);
            if ((o.Reward.Credits ?? 0) != 0) reward.Credits = (int)Js.Round(o.Reward.Credits!.Value * Content.Night.Supplies);
            var night = CopyOffer(o);
            night.Night = true;
            night.Reward = reward;
            return night;
        }

        /// <summary>`{ ...o }`: a new offer with the same fields (its lists, map and reward shared, as a spread shares them).</summary>
        private static MissionOffer CopyOffer(MissionOffer o)
        {
            return new MissionOffer
            {
                Id = o.Id,
                Name = o.Name,
                Kind = o.Kind,
                Difficulty = o.Difficulty,
                ExpiresDay = o.ExpiresDay,
                Reward = o.Reward,
                Map = o.Map,
                Pods = o.Pods,
                Final = o.Final,
                Region = o.Region,
                Facility = o.Facility,
                Story = o.Story,
                Quest = o.Quest,
                Night = o.Night,
                Tide = o.Tide,
                Living = o.Living,
                Vip = o.Vip,
                Alarm = o.Alarm,
                Siege = o.Siege,
            };
        }

        /// <summary>`{ ...r }`.</summary>
        private static MissionReward CopyReward(MissionReward r)
        {
            return new MissionReward { Credits = r.Credits, Recruit = r.Recruit, Doom = r.Doom, Medicines = r.Medicines, Food = r.Food, Parts = r.Parts, Vehicle = r.Vehicle };
        }

        /// <summary>Whether it is still winter in the campaign: until the story says it is over.</summary>
        public static bool Wintry(CampaignState c)
        {
            return c.Story.Done < ThawAfterStory;
        }

        /// <summary>What the battlefield of an operation names: its kicker, the object to recover, the VIP and an enemy leader.</summary>
        public static MissionInfo MissionInfo(CampaignState c, MissionOffer offer)
        {
            var info = OwnInfo(c, offer);
            // An ordinary rescue's hostage and a sabotage's alarm, chosen with the offer (playtest 2026-10-15, #14, #20).
            if (string.IsNullOrEmpty(info.VipName) && !string.IsNullOrEmpty(offer.Vip)) info.VipName = offer.Vip;
            if (!string.IsNullOrEmpty(offer.Alarm)) info.Alarm = offer.Alarm;
            if (Wintry(c)) info.Snow = true;
            return info;
        }

        /// <summary>What the story, a side quest or an ordinary Botín say of an operation.</summary>
        private static MissionInfo OwnInfo(CampaignState c, MissionOffer offer)
        {
            if ((offer.Story ?? 0) != 0)
            {
                var chapter = offer.Story!.Value;
                return new MissionInfo { Kicker = chapter == 1 ? null : $"DIARIO · HOJA {chapter}", Item = Lore.Story[chapter - 1].Item, VipName = Lore.StoryVip(chapter, c.Story.Simon) };
            }
            var q = QuestOf(c, offer);
            // An ordinary Botín carries out what its report speaks of (docs/PRUEBA-2026-10-14.md #2).
            if (q == null || !(Js.At<QuestStep?>(Quests.QuestDefs[q.Id].Steps, q.Step, null) is QuestMission step))
            {
                var item = Lore.RecoveryItem(offer);
                return !string.IsNullOrEmpty(item) ? new MissionInfo { Item = item, ItemOutdoors = Lore.RecoveryOutdoors(offer) ? true : (bool?)null } : new MissionInfo();
            }
            return new MissionInfo
            {
                Kicker = $"ENCARGO · {Quests.QuestDefs[q.Id].Title.ToUpperInvariant()}",
                Item = string.IsNullOrEmpty(step.Item) ? step.Item : Quests.Fill(step.Item!, q.Names),
                ItemOutdoors = step.Outdoors == true ? true : (bool?)null,
                VipName = string.IsNullOrEmpty(step.Vip) ? step.Vip : Quests.Fill(step.Vip!, q.Names),
                Leader = step.Leader != null ? new MissionLeader { Template = step.Leader.Template, Name = step.Leader.Name } : null,
            };
        }

        /// <summary>Who waits at the objective of a rescue: story chapters and side quests have their own.</summary>
        public static string? OfferVip(CampaignState c, MissionOffer offer)
        {
            return MissionInfo(c, offer).VipName;
        }

        /// <summary>Morse's report for an operation; side quests write their own.</summary>
        public static string OfferBriefing(CampaignState c, MissionOffer offer)
        {
            if ((offer.Reward.Vehicle ?? 0) != 0) return Lore.VehicleBriefing(offer.Reward.Vehicle!.Value, Regions[offer.Region].Name);
            // The dead at the base: nobody writes to warn of it (docs/FASE12.md §11.5).
            if (offer.Siege == true) return $"{Lore.BaseSiegeText.Arrival}{(offer.Night == true ? $" {Lore.BaseSiegeText.Night}" : "")}";
            var q = QuestOf(c, offer);
            if (q != null && Js.At<QuestStep?>(Quests.QuestDefs[q.Id].Steps, q.Step, null) is QuestMission step) return Quests.Fill(step.Briefing, q.Names);
            return Lore.MissionBriefing(offer, Progression.BastionLevel(c.Level), c.Story.Simon);
        }

        /// <summary>Who signs an operation's report.</summary>
        public static string OfferSignature(CampaignState c, MissionOffer offer)
        {
            if (offer.Siege == true) return "";
            var q = QuestOf(c, offer);
            return q != null ? Quests.QuestDefs[q.Id].Signature : Lore.BriefingSignature(offer);
        }

        // ------------------------------------------------------------- the soldiers

        /// <summary>A soldier's veterancy, by name (Novato, Fogueado…).</summary>
        /// <remarks>`s: Pick&lt;CampaignSoldier, 'board'&gt; &amp; { appearance?: Pick&lt;Appearance, 'sex'&gt; }`.</remarks>
        public static string VeterancyName(CampaignSoldier s)
        {
            var def = Bars.Veterancy[Bars.VeterancyRank(s.Board.Count)];
            return s.Appearance?.Sex == Sexes.F ? def.Female : def.Name;
        }

        /// <summary>Of what a mission teaches, this much more for each level of the Taller de oficios, and with the Camposanto at 3 after losses.</summary>
        private const double LearningShare = 0.2; // LEARNING_BONUS

        /// <summary>
        /// How much more a mission teaches (skills.ts learn): 20 % for every level of
        /// the Taller de oficios (docs/FASE9.md), Teo going over what was done out there.
        /// </summary>
        public static double LearningBonus(CampaignState c)
        {
            return LearningShare * FacilityLevel(c, FacilityIds.Echoes);
        }

        /// <summary>
        /// Whether a soldier has a point it can spend now on the board (docs/FASE10.md
        /// §6): a cell beside its own whose line's bars allow it.
        /// </summary>
        public static bool CanSpendPoints(CampaignState c, CampaignSoldier s)
        {
            return Board.Cells.Any((cell) => Bars.UnlockBlocker(s.Skills, s.Board, cell.Id) == null);
        }
    }
}
