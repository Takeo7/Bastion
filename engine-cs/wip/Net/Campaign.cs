using System.Collections.Generic;
using Bastion.Engine;

namespace Bastion.Net
{
    // Provisional, until campaign.ts is in C# (Types/, the new Gameplay's): the few parts of the campaign the room reads,
    // and what it asks of campaign.ts. Every «Wip» name goes when they come: WipCampaign → CampaignState, WipSoldier → its
    // soldier, WipProposal → Proposal, WipCampaignCommand → CampaignCommand, and the three commands to theirs.

    public sealed class WipCampaign
    {
        /// <summary>campaign.ts CAMPAIGN_VERSION when saved.</summary>
        public int Version { get; set; }
        public int Day { get; set; }
        public int? MissionsFlown { get; set; }
        public int Doom { get; set; }
        public string? Outcome { get; set; } // Outcomes
        /// <summary>The slots taking part.</summary>
        public List<int> Players { get; set; } = new List<int>();
        public List<WipSoldier> Soldiers { get; set; } = new List<WipSoldier>();
        /// <summary>A joint decision waiting for the partner's vote.</summary>
        public WipProposal? Proposal { get; set; }
        /// <summary>The operation being got ready (its squad picked in the hangar).</summary>
        public string? ActiveMission { get; set; }
        public List<string> Squad { get; set; } = new List<string>();
        public bool[] LaunchReady { get; set; } = new bool[2];
    }

    public sealed class WipSoldier
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Owner { get; set; }
        public bool Alive { get; set; }
        public List<string> Board { get; set; } = new List<string>();
    }

    public sealed class WipProposal
    {
        public int By { get; set; }
        public string Action { get; set; } = "";
        public string Text { get; set; } = "";
    }

    /// <summary>What a player asks of the host on the base (protocol.ts campaignCommandSchema, by `type`).</summary>
    public abstract class WipCampaignCommand
    {
        public abstract string Type { get; }
    }

    /// <summary>The answer to the partner's proposal.</summary>
    public sealed class WipVoteCommand : WipCampaignCommand
    {
        public override string Type => "vote";
        public bool Accept { get; set; }
    }

    public sealed class WipLaunchCommand : WipCampaignCommand
    {
        public override string Type => "launch";
    }

    public sealed class WipUnlockCommand : WipCampaignCommand
    {
        public override string Type => "unlock";
        public string Soldier { get; set; } = "";
        public string Cell { get; set; } = "";
    }

    public sealed class WipChooseMissionCommand : WipCampaignCommand
    {
        public override string Type => "chooseMission";
        public string Mission { get; set; } = "";
    }

    /// <summary>campaign.ts executeCampaignCommand's answer: done or why not, and whether the mission goes now.</summary>
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
    /// IRules, a skirmish's room has none. The engine's, once campaign.ts is in C#; the tests', a fake.
    /// </summary>
    public interface ICampaignRules
    {
        /// <summary>CAMPAIGN_VERSION: a campaign saved by an older one is migrated (MigrateCampaign), by a newer one not read.</summary>
        int CampaignVersion { get; }

        /// <summary>campaignOutdated: from before FRESH_CAMPAIGN_VERSION, it can't take the progression: kept aside.</summary>
        bool CampaignOutdated(int version);

        WipCampaign MigrateCampaign(WipCampaign c);

        WipCampaign CreateCampaign(IReadOnlyList<int> slots, Rng rng);

        /// <summary>A new campaign opens with its first operation chosen and its squad in the hangar (playtest 2026-10-13).</summary>
        void OpeningMission(WipCampaign c);

        /// <summary>A player who joins late gets soldiers of their own; false if they had them.</summary>
        bool AddPlayer(WipCampaign c, int slot, Rng rng);

        List<string> DefaultSquad(WipCampaign c, IReadOnlyList<int> connected);

        void SyncPads(WipCampaign c);

        CampaignResult ExecuteCampaignCommand(WipCampaign c, WipCampaignCommand cmd, int slot, IReadOnlyList<int> connected, Rng rng);

        /// <summary>
        /// room.ts launchCampaignMission: the mission of the operation chosen, from activeOffer, missionInfo, offerMap,
        /// siegePlan, vehicleOf, buildSquad, podsFor and the rest into createMission, and its name for the log; null
        /// without an operation chosen.
        /// </summary>
        (GameState state, string name)? LaunchMission(WipCampaign c, Rng rng);

        /// <summary>A mission of the campaign over, folded into the campaign (once: the room keeps resultApplied).</summary>
        void ApplyMissionResult(WipCampaign c, GameState state, Rng rng);

        int BastionLevel(WipCampaign c);

        /// <summary>skills.ts pointsLeft of a soldier, for red.log.</summary>
        int PointsLeft(WipSoldier s);
    }
}
