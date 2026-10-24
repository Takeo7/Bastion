using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;

namespace Bastion.Net
{
    /// <summary>
    /// The engine's campaign (Campaign/, campaign.ts in C#) as the room asks for it (ICampaignRules), with what room.ts
    /// does itself around it: launching the operation chosen. The game's host makes its room with these (NetLink).
    /// </summary>
    public sealed class EngineCampaignRules : ICampaignRules
    {
        /// <summary>
        /// room.ts launchCampaignMission: the mission of the operation chosen, made with createMission from what the
        /// campaign says of it, and its name for the log; null without an operation chosen. It only reads the campaign.
        /// </summary>
        public (GameState state, string name)? LaunchMission(CampaignState c, Rng rng)
        {
            var offer = Campaign.ActiveOffer(c);
            if (offer == null) return null;
            var info = Campaign.MissionInfo(c, offer);
            var siege = Campaign.SiegePlan(c, offer);
            var state = Setup.CreateMission(new MissionOptions
            {
                // A siege is fought on the base as it stands now (docs/FASE12.md §11.5).
                Map = Campaign.OfferMap(c, offer),
                SiegePlan = siege,
                Vehicle = Campaign.VehicleOf(c),
                Posts = siege != null ? c.SiegePosts : null,
                Slots = c.Players.ToList(),
                Rng = rng,
                Kind = offer.Kind,
                Night = offer.Night,
                Living = offer.Living,
                Squad = Campaign.BuildSquad(c),
                Pods = Setup.PodsFor(offer.Map, offer.Pods),
                Difficulty = offer.Difficulty,
                HackBonus = Campaign.CampaignHackBonus(c),
                // `...(offer.story ? { story } : {})`, and the same for the kicker and the item: 0 and "" are left out too.
                CampaignMission = new CampaignMission
                {
                    Id = offer.Id,
                    Name = offer.Name,
                    Story = offer.Story != 0 ? offer.Story : null,
                    Kicker = string.IsNullOrEmpty(info.Kicker) ? null : info.Kicker,
                    Item = string.IsNullOrEmpty(info.Item) ? null : info.Item,
                    Briefing = Campaign.OfferBriefing(c, offer),
                },
                VipName = info.VipName,
                Alarm = string.IsNullOrEmpty(info.Alarm) ? null : info.Alarm,
                ObjectiveName = string.IsNullOrEmpty(info.Alarm) ? null : Lore.AlarmNames[info.Alarm!],
                Leader = info.Leader,
                ItemOutdoors = info.ItemOutdoors,
                MedicineCures = Campaign.MedicineCures(c),
                Quarantine = Campaign.HasQuarantine(c),
                Snow = info.Snow,
            });
            return (state, offer.Name);
        }

        public List<string> DefaultSquad(CampaignState c, IReadOnlyList<int> connected) => Campaign.DefaultSquad(c, connected);

        /// <summary>
        /// Changes the campaign where it is, unlike TypeScript's copy (JSON.parse/stringify): the room keeps only what it
        /// gives back, from a save read just for this.
        /// </summary>
        public CampaignState MigrateCampaign(CampaignState c) => Campaign.MigrateCampaign(c);

        public CampaignState CreateCampaign(IReadOnlyList<int> slots, Rng rng) => Campaign.CreateCampaign(slots, rng);

        public void OpeningMission(CampaignState c) => Campaign.OpeningMission(c);

        /// <summary>addPlayer gives how many soldiers they got: none, they were in.</summary>
        public bool AddPlayer(CampaignState c, int slot, Rng rng) => Campaign.AddPlayer(c, slot, rng) > 0;

        public void SyncPads(CampaignState c) => Campaign.SyncPads(c);

        public CampaignResult ExecuteCampaignCommand(CampaignState c, CampaignCommand cmd, int slot, IReadOnlyList<int> connected, Rng rng)
        {
            return Campaign.ExecuteCampaignCommand(c, cmd, new CampaignContext { Slot = slot, Connected = connected, Rng = rng });
        }

        /// <summary>Its report stays in the campaign (lastReport), which goes to both whole: the room needs nothing else.</summary>
        public void ApplyMissionResult(CampaignState c, GameState state, Rng rng) => Campaign.ApplyMissionResult(c, state, rng);
    }
}
