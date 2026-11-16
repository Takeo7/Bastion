#nullable enable
// The runs in Campaign (.dev-snapshots/gameplay/runs/H0-DISENO.md §3; new in C#): a new campaign of version 28, how to
// tell one, and what Runs takes from Campaign's own helpers (a soldier, the log, the player who needs soldiers most, a
// seed, the commands of before that stay). The campaign of before (CampaignVersion, CreateCampaign) goes on as it was
// until the runs replace it.
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        /// <summary>A campaign of runs: the stash, the kits, the upgrades and the node maps (H0). CampaignVersion stays for the one of before.</summary>
        public const int RunsCampaignVersion = 28;

        /// <summary>Whether a campaign is one of runs (version 28 on, with its stash).</summary>
        public static bool IsRuns(CampaignState c)
        {
            return c.Version >= RunsCampaignVersion && c.Stash != null;
        }

        /// <summary>
        /// A new campaign of runs: 4 soldiers, 2 a player (the 4 of the only one alone), each with a knife and the pack empty;
        /// the stash empty, 0 §, chapter 1, no run. The squad that would go out is everyone (Runs.DefaultSquad).
        /// </summary>
        public static CampaignState CreateRunsCampaign(IReadOnlyList<int> players, Rng rng)
        {
            var c = new CampaignState
            {
                Version = RunsCampaignVersion,
                Day = 1,
                Players = new List<int>(players),
                Credits = 0,
                Soldiers = new List<CampaignSoldier>(),
                Squad = new List<string>(),
                AdvanceReady = new bool[2],
                LaunchReady = new bool[2],
                Log = new List<LogEntry>(),
                NextId = 0,
                Stash = new GearGrid { W = Runs.StashW, H = Runs.StashH },
                Upgrades = new Dictionary<string, int>(),
                Chapter = 1,
                ChaptersDone = 0,
                Run = null,
                NodesPlayed = 0,
            };
            for (var i = 0; i < 2 * Runs.StartSoldiers; i++) Runs.NewSoldier(c, players.Count > 1 ? players[i % 2] : players[0], rng);
            c.Squad = Runs.DefaultSquad(c, c.Players);
            Log(c, RunTexts.Welcome, LogTones.Good);
            c.Uid = Base36(Seed31(rng));
            return c;
        }

        // ------------------------------------------------------- for Runs (internal)

        /// <summary>A recruit as AddSoldier makes them, without the arms of before; `join` false takes them out of the soldiers again.</summary>
        internal static CampaignSoldier RunsRecruit(CampaignState c, int owner, Rng rng, bool join)
        {
            var s = AddSoldier(c, owner, rng, armed: false);
            if (!join) c.Soldiers.Remove(s);
            return s;
        }

        internal static void RunsLog(CampaignState c, string text, string tone = LogTones.Info)
        {
            Log(c, text, tone);
        }

        internal static int RunsNeediest(CampaignState c)
        {
            return NeediestPlayer(c);
        }

        internal static int RunsSeed(Rng rng)
        {
            return Seed31(rng);
        }

        /// <summary>The commands of before a run campaign keeps (unlock, customize, vote, withdraw), as they are.</summary>
        internal static CampaignResult RunsBaseCommand(CampaignState c, CampaignCommand cmd, CampaignContext ctx)
        {
            return RunCampaignCommand(c, cmd, ctx);
        }
    }
}
