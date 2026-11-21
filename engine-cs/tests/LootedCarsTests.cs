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
            // Magazines, or in a run one time in four a bundle of arrows instead (Looting.ArrowsOdds, cuadro 102).
            Assert.InRange(rolls.Count((l) => l!.Mags != null || l!.Arrows != null) / (double)N, 0.15, 0.17);
            Assert.InRange(rolls.Count((l) => l!.Arrows != null) / (double)rolls.Count((l) => l!.Mags != null || l!.Arrows != null), 0.22, 0.28);
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

        [Fact]
        public void AMolotovOnACarsNoseLeavesItsBoot()
        {
            // Gameplay, 19/11: a molotov landing on a whole car's nose made its fire's Source the nose, and the car a burning
            // one (no boot while it burnt; drawn a burning wreck when the map was built again). It leaves the scenery alone
            // (Arturo, 1/11): only the map's own fires burn a car (Burning.Car).
            var (_, s) = RunInteriorTests.At(Places.City, NodeKinds.Normal, 7);
            s.Concealed = false;
            var u = s.Units.First((x) => x.Team == Teams.Xcom && x.Alive && x.CampaignId != null);
            u.Gear = new List<string> { ItemIds.Molotov };
            u.Charges[ChargeIds.Molotov] = 2;
            u.Ap = 2;
            var throws = Boots(s).Select((p) => Looting.BootOf(s, Grid.TileIndex(s, p.X, p.Y))!.Value).Distinct().SelectMany((nose) =>
            {
                var np = new Pos { X = nose % s.Width, Y = nose / s.Width, Z = 0 };
                return Enumerable.Range(0, 49).Select((k) => new Pos { X = np.X + k % 7 - 3, Y = np.Y + k / 7 - 3, Z = 0 })
                    .Where((p) => Grid.InBounds(s, p.X, p.Y) && Grid.Distance(p, np) >= 2 && Grid.IsWalkable(Grid.KindAt(s, p)) && Grid.UnitAt(s, p) == null && Decor.ObjectAt(s.Decor!, s.Width, Grid.TileIndex(s, p.X, p.Y)) == null)
                    .Select((p) => (nose, np, from: p));
            });
            var (carNose, at, stand) = throws.First((t) =>
            {
                var was = u.Pos;
                u.Pos = t.from;
                var can = Abilities.CanTargetTile(s, u, AbilityIds.Molotov, t.np);
                u.Pos = was;
                return can;
            });
            u.Pos = stand;
            var ctx = new CommandContext { Slot = u.Owner ?? 0, Connected = new[] { 0, 1 }, Rng = new Rng(1) };
            Assert.True(Rules.ExecuteCommand(s, new AbilityCommand { Unit = u.Id, Ability = AbilityIds.Molotov, Tile = at }, ctx).Ok);
            Assert.Contains(s.Fires!, (f) => f.Source == carNose && f.Turns != null);
            Assert.False(Burning.Car(s, carNose));
            Assert.Contains(Decor.ObjectAt(s.Decor!, s.Width, carNose)!.Tiles, (j) => Looting.BootOf(s, j) == carNose);
            // One of the map's own (no turns, for ever) from that nose: a burning car, no boot.
            s.Fires!.Add(new Fire { Id = "f9", Source = carNose, Tiles = new List<int>() });
            Assert.True(Burning.Car(s, carNose));
            Assert.All(Decor.ObjectAt(s.Decor!, s.Width, carNose)!.Tiles, (j) => Assert.Null(Looting.BootOf(s, j)));
        }
    }
}
