#nullable enable
// campaign.ts CampaignResult: what executeCampaignCommand answers (the room's ICampaignRules passes it on).

namespace Bastion.Engine
{
    /// <summary>
    /// `{ ok: true; launch?: boolean } | { ok: false; error: string }`: done, and whether the mission goes now; or why not.
    /// One class for both, as CommandResult: `Launch` false where TypeScript leaves it out.
    /// </summary>
    public sealed class CampaignResult
    {
        public bool Ok { get; set; }
        /// <summary>When not `Ok`: why it was refused.</summary>
        public string? Error { get; set; }
        /// <summary>When `Ok`: every connected player is ready, and the mission goes now.</summary>
        public bool Launch { get; set; }

        public static CampaignResult Success(bool launch = false) => new CampaignResult { Ok = true, Launch = launch };

        public static CampaignResult Failure(string error) => new CampaignResult { Ok = false, Error = error };
    }
}
