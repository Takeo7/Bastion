using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>The runs' tests (H0): campaigns of version 28, gear laid by hand, a run at the node wanted.</summary>
    public static class RunsHelpers
    {
        public static readonly int[] Both = { 0, 1 };
        public static readonly int[] Zero = { 0 };

        public static CampaignState Solo(int seed = 1) => Campaign.CreateRunsCampaign(new[] { 0 }, new Rng(seed));

        public static CampaignState Two(int seed = 1) => Campaign.CreateRunsCampaign(Both, new Rng(seed));

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

        /// <summary>A joint decision with both here: `by` proposes, the other accepts. What the vote gave.</summary>
        public static CampaignResult Agree(CampaignState c, CampaignCommand cmd, int by = 0)
        {
            Ok(c, cmd, by, Both);
            Assert.NotNull(c.Proposal);
            return Ok(c, new VoteCommand { Accept = true }, 1 - by, Both);
        }

        /// <summary>A run going out with the squad there is (alone, or both agreeing).</summary>
        public static RunState Start(CampaignState c)
        {
            if (c.Players.Count > 1) Agree(c, new StartRunCommand());
            else Ok(c, new StartRunCommand());
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

        /// <summary>The soldiers of the run's squad.</summary>
        public static List<CampaignSoldier> Squad(CampaignState c) => c.Run!.Squad.Select((id) => c.Soldiers.First((s) => s.Id == id)).ToList();
    }
}
