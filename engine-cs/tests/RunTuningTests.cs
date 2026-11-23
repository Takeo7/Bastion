using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.RunsHelpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The benches' knobs (Runs.Tuning; Producto, 17/11): today's when none is set; what each changes when a bench sets it;
    /// each flow of work its own (the benches play four campaigns at once).
    /// </summary>
    public class RunTuningTests
    {
        private static Runs.Tuning.Settings Variant() => new Runs.Tuning.Settings
        {
            Wipe = Runs.Tuning.Wipes.FillPistol,
            SalvageSupplies = 0.5,
            SalvageFirearms = true,
            SuppliesScale = 3,
            UpgradePrices = new Dictionary<string, int> { [UpgradeIds.Squad] = 120, [UpgradeIds.Mend] = 80 },
        };

        [Fact]
        public void TodaysWithNoneSet()
        {
            Assert.False(Runs.Tuning.IsSet);
            var c = Campaign.CreateRunsCampaign(Zero, new Rng(1));
            Assert.Equal(Content.UpgradePrices[UpgradeIds.Squad][0], Runs.NextPrice(c, UpgradeIds.Squad));
            var run = new RunFight { Row = 1 };
            Assert.Equal(1, RunLoot.SuppliesScale(run));
            Assert.Equal(Runs.Tuning.Wipes.Lose, Runs.Tuning.Current.Wipe);
        }

        [Fact]
        public void PricesAndSuppliesWhenSet()
        {
            Runs.Tuning.Set(Variant());
            try
            {
                var c = Campaign.CreateRunsCampaign(Zero, new Rng(1));
                Assert.Equal(120, Runs.NextPrice(c, UpgradeIds.Squad));
                Assert.Equal(80, Runs.NextPrice(c, UpgradeIds.Mend));
                // Only the first level, only those given.
                Assert.Equal(Content.UpgradePrices[UpgradeIds.Stash][0], Runs.NextPrice(c, UpgradeIds.Stash));
                c.Upgrades![UpgradeIds.Squad] = 1;
                Assert.Equal(Content.UpgradePrices[UpgradeIds.Squad][1], Runs.NextPrice(c, UpgradeIds.Squad));
                Assert.Equal(3, RunLoot.SuppliesScale(new RunFight { Row = 1 }));
            }
            finally
            {
                Runs.Tuning.Reset();
            }
            Assert.False(Runs.Tuning.IsSet);
        }

        /// <summary>A launched fight whose squad all fall, each with § and a rifle on them: the campaign after it.</summary>
        private static CampaignState Wiped(int supplies)
        {
            var (c, s) = LaunchAt(NodeKinds.Normal);
            var next = s.Run!.NextGear;
            foreach (var u in s.Units.Where((x) => x.CampaignId != null).ToList())
            {
                u.Pack!.Items.Add(new Placed { Gear = new Gear { Id = $"g{++next}", Kind = GearKinds.Supplies, Count = supplies }, X = 0, Y = 0 });
                Gears.SetSlot(u.Kit!, KitSlots.Primary, new Gear { Id = $"g{++next}", Kind = GearKinds.Weapon, Type = FoundWeaponIds.AssaultRifle, Count = 1 });
            }
            s.Run.NextGear = next;
            foreach (var u in s.Units.Where((x) => x.CampaignId != null).ToList()) Events.ApplyEvent(s, new DiedEvent { Unit = u.Id });
            var report = EndFight(c, s, Outcomes.Defeat, EndReasons.Wiped);
            Assert.Equal(RunEnds.Wiped, report.Run!.Ended);
            return c;
        }

        [Fact]
        public void AWipeAsToday()
        {
            var c = Wiped(40);
            // Nobody at the base: the game lost (N8), nothing back.
            Assert.Empty(c.Soldiers.Where((x) => x.Alive));
            Assert.Equal(Outcomes.Defeat, c.Outcome);
            Assert.Equal(0, Runs.StashSupplies(c));
            Assert.DoesNotContain(c.Stash!.Items, (p) => p.Gear.Type == FoundWeaponIds.AssaultRifle);
        }

        [Fact]
        public void AWipeWithTheVariant()
        {
            Runs.Tuning.Set(Variant());
            try
            {
                var c = Wiped(40);
                // The seats full, each with a knife and a pistol.
                var alive = c.Soldiers.Where((x) => x.Alive).ToList();
                Assert.Equal(Runs.SeatsOf(c), alive.Count);
                Assert.All(alive, (x) => Assert.Equal(FoundWeaponIds.Pistol, x.Kit!.Pistol?.Type));
                // Half the § they carried, and their rifles.
                var carried = 40 * 4;
                Assert.Equal(carried / 2, Runs.StashSupplies(c));
                Assert.Equal(4, c.Stash!.Items.Count((p) => p.Gear.Type == FoundWeaponIds.AssaultRifle));
            }
            finally
            {
                Runs.Tuning.Reset();
            }
        }

        [Fact]
        public void EachFlowOfWorkItsOwn()
        {
            // As a bench plays them: four at once, each with its own, none leaking to the next.
            var seen = new int?[8];
            Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (i) =>
            {
                if (i % 2 == 0) Runs.Tuning.Set(Variant());
                try
                {
                    seen[i] = Task.Run(() => Runs.NextPrice(Campaign.CreateRunsCampaign(Zero, new Rng(1)), UpgradeIds.Squad)).Result;
                }
                finally
                {
                    Runs.Tuning.Reset();
                }
            });
            for (var i = 0; i < 8; i++) Assert.Equal(i % 2 == 0 ? 120 : Content.UpgradePrices[UpgradeIds.Squad][0], seen[i]);
            Assert.False(Runs.Tuning.IsSet);
        }
    }
}
