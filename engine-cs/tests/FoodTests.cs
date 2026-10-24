using System;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/food.test.ts, the tests on searching (loot.ts rollLoot). Not ported, the rest, which play a
    /// campaign and need campaign.ts (createCampaign, executeCampaignCommand, applyMissionResult, buildSquad; the helper
    /// raise): the six of «food» but «searching finds rations…», the three of «the Taller», «found weapons › brought home,
    /// it goes to the Armería…» and «… › it is lost if its soldier falls», and «the campaign › some operations pay rations
    /// or parts…».
    /// </summary>
    public class FoodTests
    {
        // Phase 4 of the zombies: food, the Taller's work and found weapons (docs/FASE4.md).

        /// <summary>Out of `n` searches of `decor`, the share that found `what`.</summary>
        private static double Share(string decor, Func<Loot, object?> what, int n = 4000)
        {
            var hits = 0;
            for (var seed = 1; seed <= n; seed++)
            {
                var loot = Looting.RollLoot(decor, 2, new Rng(seed));
                if (loot != null && what(loot) != null) hits++;
            }
            return (double)hits / n;
        }

        private static object? Food(Loot l) => l.Food;

        private static object? Weapon(Loot l) => l.Weapon;

        /// <summary>toBeCloseTo(expected, digits): within half a unit of the last digit.</summary>
        private static void CloseTo(double expected, double actual, int digits)
        {
            Assert.True(Math.Abs(expected - actual) < Math.Pow(10, -digits) / 2, $"expected {actual} to be close to {expected}");
        }

        [Fact(DisplayName = "food › searching finds rations: pantries, shops and stores most, car boots little")]
        public void SearchingFindsRations()
        {
            foreach (var decor in new[] { DecorKinds.Wardrobe, DecorKinds.Shelf, DecorKinds.Crates }) Assert.True(Share(decor, Food) > Share(DecorKinds.CarRear, Food) * 2, decor);
            for (var seed = 1; seed < 400; seed++)
            {
                var food = Looting.RollLoot(DecorKinds.Shelf, 2, new Rng(seed))?.Food;
                if (food != null)
                {
                    Assert.True(food >= 2);
                    Assert.True(food <= 4);
                }
            }
        }

        [Fact(DisplayName = "found weapons › rare: boots, stores and home wardrobes; never an office")]
        public void RareBootsStoresAndHomeWardrobes()
        {
            CloseTo(0.04, Share(DecorKinds.CarRear, Weapon), 1);
            CloseTo(0.03, Share(DecorKinds.Crates, Weapon), 1);
            Assert.True(Share(DecorKinds.Wardrobe, Weapon) > 0);
            Assert.Equal(0, Share(DecorKinds.Desk, Weapon));
        }
    }
}
