using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// A map's levels against what the engine walks and the room takes (Networking, R4, 17/11: the room takes a tile's z up to
    /// 3, and the city, the town, the factory, the hospital and the offices step on 3). Grid.TopLevel, the roof of a
    /// three-storey building, is the highest the engine stands anyone on (SurfaceAt) and the room takes (Room.Spot): a
    /// taller building wants the three raised together.
    /// </summary>
    public sealed class MapLevelsTests
    {
        private readonly ITestOutputHelper output;

        public MapLevelsTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void NoMapHasALevelAboveTheTop()
        {
            var sites = new[] { Places.City, Places.Town, Places.Estate, Places.Factory, Places.Roadside, Places.Country, Places.Hospital, Places.Offices, Places.Shed, Places.Police, Places.Plaza, Places.Camping };
            var tallest = 0;
            foreach (var site in sites)
            {
                foreach (var seed in Enumerable.Range(1, 6))
                {
                    var (_, s) = RunInteriorTests.At(site, NodeKinds.Normal, seed);
                    var top = Grid.LevelCount(s) - 1;
                    Assert.True(top <= Grid.TopLevel, $"{site} {seed}: level {top}");
                    Assert.All(s.Units, (u) => Assert.InRange(u.Pos.Z, 0, Grid.TopLevel));
                    tallest = Math.Max(tallest, top);
                }
            }
            output.WriteLine($"the highest level of {sites.Length * 6} maps: {tallest}");
        }
    }
}
