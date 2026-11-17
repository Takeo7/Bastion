using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>The runs' tests (H0): campaigns of runs, gear laid by hand, a run at the node wanted.</summary>
    public static class RunsHelpers
    {
        public static readonly int[] Both = { 0, 1 };
        public static readonly int[] Zero = { 0 };

        /// <summary>
        /// A new campaign alone with 4 seats, as they were before N4 (17/11): the «squad» upgrade's first level, and everyone
        /// in the squad. Campaign.CreateRunsCampaign is the one a game starts with (2 seats).
        /// </summary>
        public static CampaignState Solo(int seed = 1) => Seats4(Campaign.CreateRunsCampaign(new[] { 0 }, new Rng(seed)));

        /// <summary>The same with two players: 2 soldiers each, all going out.</summary>
        public static CampaignState Two(int seed = 1) => Seats4(Campaign.CreateRunsCampaign(Both, new Rng(seed)));

        /// <summary>The «squad» upgrade at its first level (4 seats) and the squad that would go out with them.</summary>
        public static CampaignState Seats4(CampaignState c)
        {
            (c.Upgrades ??= new Dictionary<string, int>())[UpgradeIds.Squad] = 1;
            c.Squad = Runs.DefaultSquad(c, c.Players);
            return c;
        }

        /// <summary>The `nth` living soldier of `owner`.</summary>
        public static CampaignSoldier Soldier(CampaignState c, int owner = 0, int nth = 0) => c.Soldiers.Where((s) => s.Owner == owner && s.Alive).ElementAt(nth);

        /// <summary>A new gear laid in a grid (it must fit).</summary>
        public static Gear Lay(CampaignState c, GearGrid grid, string kind, string? type, int x, int y, int count = 1, bool turned = false)
        {
            var g = Gears.NewGear(c, kind, type, count);
            Assert.NotNull(Gears.Place(grid, g, x, y, turned));
            return g;
        }

        public static Gear InStash(CampaignState c, string kind, string? type, int x, int y, int count = 1, bool turned = false) => Lay(c, c.Stash!, kind, type, x, y, count, turned);

        public static Gear InPack(CampaignState c, CampaignSoldier s, string kind, string? type, int x, int y, int count = 1, bool turned = false) => Lay(c, s.Kit!.Pack, kind, type, x, y, count, turned);

        /// <summary>A new gear put in a kit's place.</summary>
        public static Gear InSlot(CampaignState c, CampaignSoldier s, string slot, string kind, string? type, int count = 1)
        {
            var g = Gears.NewGear(c, kind, type, count);
            Gears.SetSlot(s.Kit!, slot, g);
            return g;
        }

        public static CampaignResult Run(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null, int seed = 7) => CampaignHelpers.Run(c, cmd, slot, connected ?? Zero, seed);

        public static CampaignResult Ok(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null, int seed = 7) => CampaignHelpers.Ok(c, cmd, slot, connected ?? Zero, seed);

        public static void Refused(string error, CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            var r = Run(c, cmd, slot, connected);
            Assert.Equal((false, error), (r.Ok, r.Error));
        }

        /// <summary>A joint decision with both here: `by` proposes, the other accepts, the dice rolled from `seed`. What the vote gave.</summary>
        public static CampaignResult Agree(CampaignState c, CampaignCommand cmd, int by = 0, int seed = 7)
        {
            Ok(c, cmd, by, Both, seed);
            Assert.NotNull(c.Proposal);
            return Ok(c, new VoteCommand { Accept = true }, 1 - by, Both, seed);
        }

        /// <summary>
        /// A run going out with the squad there is (alone, or both agreeing). The run's dice from `seed`: its map and all it
        /// draws (7, as every command here, unless told).
        /// </summary>
        public static RunState Start(CampaignState c, int seed = 7)
        {
            if (c.Players.Count > 1) Agree(c, new StartRunCommand(), 0, seed);
            else Ok(c, new StartRunCommand(), 0, Zero, seed);
            return c.Run!;
        }

        /// <summary>
        /// The squad at a node of `kind`: a run started if there's none, a node of the row next made that kind (a stop's
        /// neighbours are fights as they come), and chosen. What choosing it gave.
        /// </summary>
        public static CampaignResult GoTo(CampaignState c, string kind, int col = 0)
        {
            var run = c.Run ?? Start(c);
            var n = RunMap.Choices(run)[col];
            n.Kind = kind;
            if (NodeKinds.IsStop(kind))
            {
                n.Modifier = null;
                n.Site = null;
                n.Seed = null;
            }
            else if (n.Seed == null)
            {
                // A stop made a fight: a map for it.
                n.Seed = 4242 + n.Row;
                n.Site = Places.Country;
            }
            var cmd = new ChooseNodeCommand { Node = n.Id };
            return c.Players.Count > 1 ? Agree(c, cmd) : Ok(c, cmd);
        }

        /// <summary>
        /// A campaign alone at a node of row 1 made `kind` (a sabotage and the boss in a factory), its fight launched as the
        /// room does it (H3): the campaign and the mission.
        /// </summary>
        public static (CampaignState C, GameState S) LaunchAt(string kind, int seed = 1, Action<CampaignState>? before = null, Action<RunNode>? node = null)
        {
            var c = Solo(seed);
            before?.Invoke(c);
            var run = Start(c);
            var n = RunMap.Choices(run)[0];
            n.Kind = kind;
            n.Modifier = null;
            if (kind == NodeKinds.Sabotage || kind == NodeKinds.Boss) n.Site = Places.Factory;
            node?.Invoke(n);
            Assert.True(Ok(c, new ChooseNodeCommand { Node = n.Id }).Launch);
            var launched = new Bastion.Net.EngineCampaignRules().LaunchMission(c, new Rng(seed + 100));
            Assert.NotNull(launched);
            return (c, launched!.Value.state);
        }

        /// <summary>A launched fight over as it stands, folded into the run as the room does it: the report.</summary>
        public static MissionReport EndFight(CampaignState c, GameState s, string outcome, string reason)
        {
            s.Outcome = outcome;
            s.OutcomeReason = reason;
            new Bastion.Net.EngineCampaignRules().ApplyMissionResult(c, s, new Rng(9));
            return c.LastReport!;
        }

        /// <summary>The soldiers of the run's squad.</summary>
        public static List<CampaignSoldier> Squad(CampaignState c) => c.Run!.Squad.Select((id) => c.Soldiers.First((s) => s.Id == id)).ToList();
    }
}
