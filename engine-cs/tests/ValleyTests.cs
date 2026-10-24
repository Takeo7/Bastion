using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/valley.test.ts (phase 5: the valley of Ossau, docs/FASE5.md), test by test: the two that read
    /// no campaign. The rest build one (createCampaign, the file's `quiet`, and executeCampaignCommand, advanceTime,
    /// migrateCampaign) and come with the rules of campaign.ts: «the valley › the zones have their names…», «contacting by
    /// road › only zones a road joins to Bielsa…» and the four of «the Marea».
    /// </summary>
    public class ValleyTests
    {
        [Fact(DisplayName = "the valley › roads go both ways, join every zone, and are the ones the map draws")]
        public void RoadsGoBothWaysJoinEveryZoneAndAreTheOnesTheMapDraws()
        {
            foreach (var a in RegionIds.All) foreach (var b in Campaign.Regions[a].Roads) Assert.Contains(a, Campaign.Regions[b].Roads);
            foreach (var a in RegionIds.All) foreach (var b in RegionIds.All) Assert.True(Campaign.RoadHops(a, b) >= 0);
            Assert.Equal(10, Campaign.Roads.Count);
            string Pair(IEnumerable<string> p) => string.Join("-", p.OrderBy((x) => x, StringComparer.Ordinal));
            var pairs = Campaign.Roads.Select(Pair).OrderBy((x) => x, StringComparer.Ordinal).ToList();
            var drawn = new[] { new[] { "weu", "oce" }, new[] { "weu", "sa" }, new[] { "weu", "eeu" }, new[] { "eeu", "eas" }, new[] { "eas", "oce" }, new[] { "oce", "afr" }, new[] { "oce", "me" }, new[] { "afr", "na" }, new[] { "me", "na" }, new[] { "sa", "afr" } }
                .Select(Pair).OrderBy((x) => x, StringComparer.Ordinal).ToList();
            Assert.Equal(drawn, pairs);
            // Where they lie on Arturo's map (15/10): 51,2 of its pixels a unit, the middle at (768, 512).
            Assert.Equal((-6.11, -5.86), (Campaign.Regions["weu"].X, Campaign.Regions["weu"].Z));
            Assert.Equal((2.87, 6.95), (Campaign.Regions["na"].X, Campaign.Regions["na"].Z));
            // The Marea comes from the south: Los Llanos de Ceniza lie south of every other zone.
            Assert.True(Campaign.Regions.Values.All((r) => r.Id == "na" || r.Z < Campaign.Regions["na"].Z));
        }

        [Fact(DisplayName = "the valley › stretches and routes by road")]
        public void StretchesAndRoutesByRoad()
        {
            Assert.Equal(0, Campaign.RoadHops("weu", "weu"));
            Assert.Equal(new List<string> { "weu" }, Campaign.RoadRoute("weu", "weu"));
            Assert.Equal(3, Campaign.RoadHops("weu", "na"));
            Assert.Equal(new List<string> { "weu", "oce", "afr", "na" }, Campaign.RoadRoute("weu", "na"));
            Assert.Equal(new List<string> { "na", "afr", "oce", "weu", "eeu" }, Campaign.RoadRoute("na", "eeu"));
            Assert.Equal(4, Campaign.RoadHops("eeu", "na"));
        }
    }
}
