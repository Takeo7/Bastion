using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Places (new in C#): where a mission goes, how many of the dead it holds next to a city and how hard its card says it
    /// is (Arturo, 31/10, docs/PRUEBA-2026-10-31-ARTURO.md #6 and #9).
    /// </summary>
    public class PlacesTests
    {
        [Fact(DisplayName = "places › every generated map is a place, an old town by its seed, a clinic's and a hospital's on a grid")]
        public void EveryGeneratedMapIsAPlace()
        {
            Assert.True(Places.OldTownSeed(1));
            Assert.False(Places.OldTownSeed(3));
            Assert.Equal(Places.Town, Places.Of(MapSource.Generated(Biomes.City, 1)));
            Assert.Equal(Places.City, Places.Of(MapSource.Generated(Biomes.City, 3)));
            Assert.Equal(Places.City, Places.Of(MapSource.Generated(Biomes.City, 1), story: 1));
            Assert.Equal(Places.City, Places.Of(MapSource.Generated(Biomes.City, 1, medical: MedicalSiteKinds.Ward)));
            Assert.Equal(Places.Estate, Places.Of(MapSource.Generated(Biomes.City, 1, Sites.Industrial)));
            Assert.Equal(Places.Country, Places.Of(MapSource.Generated(Biomes.Wilds, 1)));
            Assert.Equal(Places.Roadside, Places.Of(MapSource.Generated(Biomes.Wilds, 1, Sites.Roadside)));
            Assert.Equal(Places.Factory, Places.Of(MapSource.Generated(Biomes.Facility, 1)));
            Assert.Null(Places.Of(MapSource.Handmade("plaza")));
            Assert.All(Places.Names.Keys, (k) => Assert.True(Places.DeadShare.ContainsKey(k)));
        }

        [Fact(DisplayName = "places › the city keeps its dead, the rest their share of them: at difficulty 2, 13, 11, 9, 8 and 7 groups")]
        public void TheCityKeepsItsDead()
        {
            var plan = Setup.MissionPods(2, MissionKinds.Elimination);
            int Groups(MapSource map) => Setup.PodsFor(map, plan).Count;
            Assert.Equal(13, Groups(MapSource.Generated(Biomes.City, 3)));
            Assert.Equal(11, Groups(MapSource.Generated(Biomes.City, 3, Sites.Industrial)));
            Assert.Equal(11, Groups(MapSource.Generated(Biomes.Facility, 3)));
            Assert.Equal(9, Groups(MapSource.Generated(Biomes.City, 1)));
            Assert.Equal(8, Groups(MapSource.Generated(Biomes.Wilds, 1, Sites.Roadside)));
            Assert.Equal(7, Groups(MapSource.Generated(Biomes.Wilds, 1)));
            // The town of a mission of the story is laid out as a city's, and holds a city's.
            Assert.Equal(13, Setup.PodsFor(MapSource.Generated(Biomes.City, 1), plan, 1).Count);
            // A hand-made map, its own.
            Assert.Equal(plan.Count, Setup.PodsFor(MapSource.Handmade("plaza"), plan).Count);
        }

        [Fact(DisplayName = "places › the stars on a card: a city's are its difficulty; the country's, fewer, for fewer dead")]
        public void TheStarsOnACard()
        {
            MissionOffer Offer(MapSource map, int difficulty) => new MissionOffer { Kind = MissionKinds.Elimination, Difficulty = difficulty, Map = map, Pods = Setup.MissionPods(difficulty, MissionKinds.Elimination) };
            for (var d = 1; d <= 4; d++)
            {
                Assert.Equal(d, Campaign.OfferStars(Offer(MapSource.Generated(Biomes.City, 3), d)));
                Assert.True(Campaign.OfferStars(Offer(MapSource.Generated(Biomes.Wilds, 1), d)) <= d);
            }
            Assert.True(Campaign.OfferStars(Offer(MapSource.Generated(Biomes.Wilds, 1), 4)) < 4);
            // On the base, the siege's own.
            Assert.Equal(3, Campaign.OfferStars(new MissionOffer { Kind = MissionKinds.Elimination, Difficulty = 3, Siege = true, Map = MapSource.Generated(Biomes.City, 3) }));
        }
    }
}
