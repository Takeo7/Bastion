using System.Collections.Generic;
using Bastion.Engine;

namespace Bastion.Net
{
    // What the room asks of campaign.ts, which comes to C# in parts (Campaign/, docs/UNITY.md hito 2): until all of it is
    // there, the room is tested with rules of its own (tests/NetCampaignRoomTests.cs). The campaign's types are the
    // engine's (Types/Campaign.cs, CampaignCommand.cs); what the room reads that is already ported it takes from the engine
    // itself: Campaign.CampaignVersion and FreshCampaignVersion (the save's checks), Progression.BastionLevel and
    // Bars.PointsLeft.

    /// <summary>
    /// campaign.ts executeCampaignCommand's answer (CampaignResult): done, and whether the mission goes now; or why not.
    /// Here until executeCampaignCommand is ported, which brings its own.
    /// </summary>
    public sealed class CampaignResult
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public bool Launch { get; set; }

        public static CampaignResult Success(bool launch = false) => new CampaignResult { Ok = true, Launch = launch };

        public static CampaignResult Failure(string error) => new CampaignResult { Ok = false, Error = error };
    }

    /// <summary>
    /// What the room asks of the campaign (room.ts imports them from campaign.ts), with TypeScript's signatures: apart from
    /// IRules, a skirmish's room has none. The engine's, once campaign.ts is in C#; the tests', a fake. `connected`: the
    /// seats connected, by slot (room.ts connectedSlots).
    /// </summary>
    public interface ICampaignRules
    {
        /// <summary>migrateCampaign: one saved by an older version (from FreshCampaignVersion on) brought up to CampaignVersion.</summary>
        CampaignState MigrateCampaign(CampaignState c);

        CampaignState CreateCampaign(IReadOnlyList<int> slots, Rng rng);

        /// <summary>A new campaign opens with its first operation chosen and its squad in the hangar (playtest 2026-10-13).</summary>
        void OpeningMission(CampaignState c);

        /// <summary>A player who joins late gets soldiers of their own; false if they had them (addPlayer gave 0).</summary>
        bool AddPlayer(CampaignState c, int slot, Rng rng);

        List<string> DefaultSquad(CampaignState c, IReadOnlyList<int> connected);

        void SyncPads(CampaignState c);

        /// <summary>executeCampaignCommand, with its CampaignContext (`slot`, `connected`, `rng`) given apart, as IRules does.</summary>
        CampaignResult ExecuteCampaignCommand(CampaignState c, CampaignCommand cmd, int slot, IReadOnlyList<int> connected, Rng rng);

        /// <summary>
        /// room.ts launchCampaignMission: the mission of the operation chosen, from activeOffer, missionInfo, offerMap,
        /// siegePlan, vehicleOf, buildSquad, podsFor and the rest into createMission, and its name for the log; null
        /// without an operation chosen.
        /// </summary>
        (GameState state, string name)? LaunchMission(CampaignState c, Rng rng);

        /// <summary>A mission of the campaign over, folded into the campaign (once: the room keeps resultApplied).</summary>
        void ApplyMissionResult(CampaignState c, GameState state, Rng rng);
    }
}
