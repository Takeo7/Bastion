#nullable enable
// The runs in Campaign (.dev-snapshots/gameplay/runs/H0-DISENO.md §3; new in C#): a new campaign of version 28, how to
// tell one, and what Runs takes from Campaign's own helpers (a soldier, the log, the player who needs soldiers most, a
// seed, the commands of before that stay). The campaign of before (CampaignVersion, CreateCampaign) goes on as it was
// until the runs replace it.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Campaign
    {
        /// <summary>
        /// A campaign of runs: the stash, the kits, the upgrades and the node maps (H0). CampaignVersion stays for the one of
        /// before. 29 since N3 and N4 (17/11): no Superviviente stop, and the squad's seats from 2; 30 since T2 (18/11): no
        /// «Arreglar la radio» (MigrateRuns).
        /// </summary>
        public const int RunsCampaignVersion = 30;

        /// <summary>The first campaign of runs, the 0.13.0's (SaveStore.FreshVersion): from it, MigrateRuns brings it up to date.</summary>
        public const int FirstRunsVersion = 28;

        /// <summary>Whether a campaign is one of runs (version 28 on, with its stash).</summary>
        public static bool IsRuns(CampaignState c)
        {
            return c.Version >= FirstRunsVersion && c.Stash != null;
        }

        /// <summary>«Arreglar la radio» (NodeKinds), gone since T2 (18/11): only in saves, a normal fight once loaded.</summary>
        public const string OldRadioStage = "hack";

        /// <summary>The 0.13.0's Superviviente stop (NodeKinds), gone since N3: only in its saves, a Refugio once loaded.</summary>
        public const string OldSurvivorStop = "survivor";

        /// <summary>What the 0.13.0's «squad» levels had cost (4 seats free, then 150 and 300 §): paid back by MigrateRuns.</summary>
        public static readonly IReadOnlyList<int> SquadPaid0130 = new[] { 0, 150, 450 };

        /// <summary>
        /// A campaign of runs saved by an earlier version, brought up to RunsCampaignVersion. From 28 (the 0.13.0): every
        /// campaign goes out with 2 seats now (N4, Arturo 17/11, cuadro 23: down to 2, and what was paid back), so its «squad»
        /// upgrade is 0 again and the § its levels cost go into the stash (Lore.SeatsReset in the log); the squad that would go
        /// out, as many as fit; a run under way ends with the squad that went (RunFights.Launch's seats). A Superviviente
        /// stop on its map is a Refugio (N3), and one the squad is at, a rest.
        /// </summary>
        public static CampaignState MigrateRuns(CampaignState c)
        {
            if (c.Version >= RunsCampaignVersion) return c;
            if (c.Version < 29 && c.Stash != null)
            {
                var upgrades = c.Upgrades ??= new Dictionary<string, int>();
                var level = Math.Max(0, Math.Min(SquadPaid0130.Count - 1, Runs.Level(c, UpgradeIds.Squad)));
                upgrades.Remove(UpgradeIds.Squad);
                var refund = SquadPaid0130[level];
                if (refund > 0)
                {
                    // Into the stash, piles of 100; what it has no room for, into the packs at the base.
                    var packs = c.Soldiers.Where((s) => s.Alive && s.Kit != null).Select((s) => s.Kit!.Pack);
                    for (var left = refund; left > 0; left -= Content.PileOf(GearKinds.Supplies, null))
                    {
                        var pile = Gears.NewGear(c, GearKinds.Supplies, null, Math.Min(Content.PileOf(GearKinds.Supplies, null), left));
                        foreach (var grid in new[] { c.Stash }.Concat(packs))
                        {
                            if (Gears.Stow(grid, pile) == 0) break;
                        }
                    }
                }
                Log(c, Lore.SeatsReset(refund), LogTones.Good);
                // The squad that would go out, as a new one is picked (each player's, as many as they may take).
                if (c.Run == null) c.Squad = Runs.DefaultSquad(c, c.Players);
                var run = c.Run;
                if (run != null)
                {
                    var at = run.Nodes.FirstOrDefault((n) => n.Id == run.At);
                    if (run.Phase == RunPhases.Stop && run.Stop != null && at?.Kind == OldSurvivorStop && run.Stop.Outcome == null)
                    {
                        foreach (var id in run.Squad)
                        {
                            var s = c.Soldiers.FirstOrDefault((x) => x.Id == id);
                            if (s != null && s.Alive) run.Hp[id] = Runs.MaxHp(s);
                        }
                        run.Stop = new StopState { Outcome = "refuge.rest", Leaving = run.Stop.Leaving };
                    }
                    foreach (var n in run.Nodes.Where((n) => n.Kind == OldSurvivorStop)) n.Kind = NodeKinds.Refuge;
                }
            }
            // T2 (Arturo, 18/11): «Arreglar la radio» gone from the maps; one still on a map is a normal fight there (one being
            // fought goes on as it was launched: its mission is the save's).
            if (c.Version < 30 && c.Stash != null && c.Run != null)
            {
                foreach (var n in c.Run.Nodes.Where((n) => n.Kind == OldRadioStage)) n.Kind = NodeKinds.Normal;
            }
            c.Version = RunsCampaignVersion;
            return c;
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

        /// <summary>
        /// A Rescate's survivor (N2) as a recruit would be, but not one: a free soldier's name from `rng`, the bars given,
        /// a knife, the look of RunsSurvivorLook's seed in the first player's colours; no id, and `c` as it was.
        /// </summary>
        internal static CampaignSoldier RunsSurvivor(CampaignState c, Rng rng, SkillLines lines)
        {
            var (name, sex) = SoldierName(c, rng);
            return new CampaignSoldier
            {
                Id = "",
                Name = name,
                Owner = c.Players.Count > 0 ? c.Players[0] : 0,
                Skills = lines.Skills,
                Growth = lines.Growth,
                Board = new List<string>(),
                Alive = true,
                Loadout = new Loadout(),
                Nickname = "",
                Appearance = Appearances.DefaultAppearance(c.Players.Count > 0 ? c.Players[0] : 0, null, sex, RunsSurvivorLook(name), Appearances.Wardrobe),
                Traits = new List<string>(),
                Kit = new Kit { Melee = new Gear { Kind = GearKinds.Weapon, Type = FoundWeaponIds.Knife }, Pack = new GearGrid { W = Runs.PackW, H = Runs.PackH } },
            };
        }

        /// <summary>The seed of a survivor's look (head, build): by their name, so it stays once they join.</summary>
        internal static string RunsSurvivorLook(string name) => $"vip:{name}";

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
