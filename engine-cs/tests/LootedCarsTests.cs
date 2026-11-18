using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// G21 (Arturo, 18/11, cuadros 70, 72 y 74: «uno de cada tres, y los demás mejores»; «oxidado = no se registra»): one
    /// whole car in three was rifled before the squad came (MissionState.Rifled: no boot, drawn rusted); never a police car;
    /// the rest hold a little more (Spots.Trunk).
    /// </summary>
    public class LootedCarsTests
    {
        private static List<Pos> Boots(GameState s) => Enumerable.Range(0, s.Width * s.Height).Where((i) => Looting.BootOf(s, i) != null).Select((i) => new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 }).ToList();

        [Fact]
        public void OneCarInThreeWasRifledAlready()
        {
            int cars = 0, rifled = 0;
            foreach (var seed in Enumerable.Range(1, 30))
            {
                var (_, s) = RunInteriorTests.At(Places.City, NodeKinds.Normal, seed);
                // (Police cars aside: never rifled.)
                var noses = Boots(s).Select((p) => Looting.BootOf(s, Grid.TileIndex(s, p.X, p.Y))!.Value).Where((n) => !(s.Mission.PoliceCar ?? new List<int>()).Contains(n)).ToList();
                cars += noses.Count;
                foreach (var nose in s.Mission.Rifled ?? new List<int>())
                {
                    rifled++;
                    // A whole car, no boot to open any more; never a police car.
                    var car = Decor.ObjectAt(s.Decor!, s.Width, nose)!;
                    Assert.Equal(DecorKinds.CarFront, car.Kind);
                    Assert.All(car.Tiles, (j) => Assert.Null(Looting.BootOf(s, j)));
                    Assert.All(car.Tiles, (j) => Assert.Null(Looting.SearchSpot(s, new Pos { X = j % s.Width, Y = j / s.Width, Z = 0 })));
                    Assert.DoesNotContain(nose, s.Mission.PoliceCar ?? new List<int>());
                }
            }
            Assert.InRange(rifled / (double)(cars + rifled), 0.28, 0.38);
        }

        [Fact]
        public void APoliceStationsPatrolCarsAlwaysHoldSomething()
        {
            foreach (var seed in Enumerable.Range(1, 30))
            {
                var (_, s) = RunInteriorTests.At(Places.Police, NodeKinds.Normal, seed);
                Assert.All(Boots(s), (p) => Assert.True(Looting.IsSearchable(s, p) || s.Mission.OutOfReach?.Contains(Grid.NodeOf(s, p)) == true));
            }
        }

        [Fact]
        public void TheBootsLeftHoldALittleMore()
        {
            // Supplies 34, a belt's thing 18, medicines 8, rations 2, parts 8, a weapon 9, an improvement 5, magazines 16:
            // never nothing (it was 11 in 100), a weapon 9 times in 100 (it was 4).
            const int N = 20000;
            var rolls = Enumerable.Range(1, N).Select((seed) => Looting.RollBox(Looting.CarBoot, new Rng(seed), true, true)).ToList();
            Assert.All(rolls, (l) => Assert.NotNull(l));
            Assert.InRange(rolls.Count((l) => l!.Weapon != null) / (double)N, 0.08, 0.10);
            Assert.InRange(rolls.Count((l) => l!.Mags != null) / (double)N, 0.15, 0.17);
            Assert.InRange(rolls.Count((l) => l!.Supplies != null) / (double)N, 0.32, 0.36);
        }

        [Fact]
        public void AFightSavedBeforeHasNone()
        {
            // Without the list (a fight of the 0.13.3's), every whole car has its boot, as then.
            var (_, s) = RunInteriorTests.At(Places.City, NodeKinds.Normal, 7);
            Assert.NotNull(s.Mission.Rifled);
            var nose = s.Mission.Rifled![0];
            s.Mission.Rifled = null;
            Assert.Contains(Decor.ObjectAt(s.Decor!, s.Width, nose)!.Tiles, (j) => Looting.BootOf(s, j) == nose);
        }
    }
}
