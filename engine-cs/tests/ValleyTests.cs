using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/valley.test.ts (phase 5: the valley of Ossau, docs/FASE5.md), test by test. `toMatchObject`
    /// on an operation is compared field by field; TypeScript's `sort()` of ids is an ordinal sort.
    /// </summary>
    public class ValleyTests
    {
        // ------------------------------------------------------------ the test file's own helpers

        /// <summary>`run`: the command with `new Rng(7)`, its result (a refusal doesn't throw).</summary>
        private static CampaignResult RunOn(CampaignState c, CampaignCommand cmd, int slot = 0, IReadOnlyList<int>? connected = null)
        {
            return CampaignHelpers.Run(c, cmd, slot, connected, 7);
        }

        /// <summary>A campaign where nothing moves but what is being tested.</summary>
        private static CampaignState Quiet()
        {
            var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(1));
            c.NextEventDay = 999;
            c.NextMissionDay = 999;
            c.NextRumorDay = 999;
            c.NextFacilityDay = 999;
            c.NextSupplyDay = 999;
            c.NextDoomDay = 999;
            c.NextMedicineDay = 999;
            c.NextFoodDay = 999;
            c.Credits = 5000;
            // The Sala de radio at its top: the Centro de comunicaciones in it (docs/FASE12.md §11.3).
            CampaignHelpers.Raise(c, PieceIds.Radio, 3);
            return c;
        }

        /// <summary>An operation of its own in `region` (its groups its own too: the copy of another's would move with it).</summary>
        private static MissionOffer OfferIn(CampaignState c, string region)
        {
            var like = c.Offers.First((o) => o.Story == null && o.Tide != true);
            // `{ ...like, pods: like.pods.map((p) => [...p]), id, region, story: null, quest: null, facility: null, expiresDay: null }`
            return new MissionOffer
            {
                Id = $"t-{region}",
                Name = like.Name,
                Kind = like.Kind,
                Difficulty = like.Difficulty,
                ExpiresDay = null,
                Reward = like.Reward,
                Map = like.Map,
                Pods = like.Pods.Select((p) => p.ToList()).ToList(),
                Final = like.Final,
                Region = region,
                Facility = null,
                Story = null,
                Quest = null,
                Night = like.Night,
                Tide = like.Tide,
                Living = like.Living,
                Vip = like.Vip,
                Alarm = like.Alarm,
                Siege = like.Siege,
            };
        }

        /// <summary>Any command the base takes looks at where the Marea is; this one passes no time.</summary>
        private static CampaignResult Look(CampaignState c) => RunOn(c, new AdvanceCommand { Ready = false });

        /// <summary>Days go by until something happens (the clock goes on inside, with the missions: docs/FASE12.md §11.6), and the base looks.</summary>
        private static void Wait(CampaignState c)
        {
            Campaign.AdvanceTime(c, new Rng(7));
            Look(c);
        }

        private static List<string> Sorted(IEnumerable<string> ids) => ids.OrderBy((x) => x, StringComparer.Ordinal).ToList();

        // ------------------------------------------------------------ the valley

        // Lore, 2026-10-13: invented, neutral names.
        [Fact(DisplayName = "the valley › the zones have their names, and ordinary operations are named for the place they go")]
        public void TheZonesHaveTheirNamesAndOrdinaryOperationsAreNamedForThePlaceTheyGo()
        {
            Assert.Equal("Metsovo", Campaign.Regions[RegionIds.Weu].Name);
            Assert.Equal(new[] { "Tarbes", "Sappada", "Koper", "Rudnik" }, new[] { RegionIds.Afr, RegionIds.Eeu, RegionIds.Me, RegionIds.Sa }.Select((id) => Campaign.Regions[id].Name));
            Assert.Equal(new[] { "La Vega", "El Pantano Gris", "Los Llanos de Ceniza" }, new[] { RegionIds.Oce, RegionIds.Eas, RegionIds.Na }.Select((id) => Campaign.Regions[id].Name));
            var ordinary = 0;
            for (var seed = 1; seed <= 12; seed++)
            {
                var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(seed));
                foreach (var o in c.Offers.Where((x) => x.Story == null && !x.Final && x.Quest == null && x.Map.Kind == MapSourceKinds.Generated))
                {
                    ordinary++;
                    Assert.Equal(Lore.MissionName(o.Map.Biome!, Campaign.Regions[o.Region].Name, o.Map.Seed ?? 0, o.Map.Site), o.Name);
                    Assert.DoesNotContain("Operación", o.Name);
                    Assert.Contains(System.Text.RegularExpressions.Regex.Replace(Campaign.Regions[o.Region].Name, "^El ", ""), o.Name);
                }
                // The opening mission is presented without the Diario label.
                var story = c.Offers.First((x) => x.Story == 1);
                Assert.Null(Campaign.MissionInfo(c, story).Kicker);
                Assert.Equal("la insulina", Campaign.MissionInfo(c, story).Item);
            }
            Assert.True(ordinary > 10, $"{ordinary} ordinary operations");
            Assert.Equal("Sala de mapas", Campaign.Facilities[FacilityIds.Simulation].Name);
        }

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

        // ------------------------------------------------------------ contacting by road

        [Fact(DisplayName = "contacting by road › only zones a road joins to Metsovo or to a contacted zone; the reason says where to go first")]
        public void OnlyZonesARoadJoinsToMetsovoOrToAContactedZone()
        {
            var c = Quiet();
            Assert.Equal("No hay carretera desde vuestras zonas: contactad antes con La Vega o Rudnik.", Campaign.ContactBlocker(c, RegionIds.Afr));
            Assert.Equal("No hay carretera desde vuestras zonas: contactad antes con La Vega.", Campaign.ContactBlocker(c, RegionIds.Na));
            Assert.False(RunOn(c, new ContactCommand { Region = RegionIds.Na }).Ok);
            Assert.True(RunOn(c, new ContactCommand { Region = RegionIds.Oce }).Ok);
            Assert.Null(Campaign.ContactBlocker(c, RegionIds.Afr));
            Assert.True(RunOn(c, new ContactCommand { Region = RegionIds.Afr }).Ok);
            Assert.True(RunOn(c, new ContactCommand { Region = RegionIds.Na }).Ok);
        }

        // ------------------------------------------------------------ the Marea

        [Fact(DisplayName = "the Marea › rises zone by zone from Los Llanos as the activity reaches each threshold; Metsovo only falls with the campaign")]
        public void RisesZoneByZoneFromLosLlanosAsTheActivityReachesEachThreshold()
        {
            var c = Quiet();
            List<string> At(int doom) => Sorted(Campaign.FloodedZones(doom));
            Assert.Equal(new string[0], At(3));
            Assert.Equal(new[] { "na" }, At(4));
            Assert.Equal(new[] { "afr", "me", "na" }, At(6));
            Assert.Equal(new[] { "afr", "me", "na" }, At(8));
            Assert.Equal(new[] { "afr", "me", "na", "oce", "sa" }, At(9));
            Assert.Equal(new[] { "afr", "eas", "me", "na", "oce", "sa" }, At(10));
            Assert.Equal(new[] { "afr", "eas", "eeu", "me", "na", "oce", "sa" }, At(11));
            Assert.Null(Campaign.MareaThreshold(Campaign.HomeRegion));
            Assert.False(Campaign.UnderMarea(11, RegionIds.Weu));
            // At 12, the campaign is lost, as ever.
            c.Doom = Campaign.DoomMax - 1;
            c.NextDoomDay = c.Day + 1;
            Wait(c);
            Assert.Equal(Outcomes.Defeat, c.Outcome);
        }

        [Fact(DisplayName = "the Marea › the base hears when a zone falls and when it is freed, in words the Bastion knows; the Marea recedes with the activity")]
        public void TheBaseHearsWhenAZoneFallsAndWhenItIsFreed()
        {
            var c = Quiet();
            List<string> Titles(int level, string[] ids, bool reached) => Sorted(ids.Select((id) => Lore.MareaAlert(level, Campaign.Regions[id].Name, reached).Title));
            List<string> Alerts(string kicker) => Sorted(c.Log.Where((l) => l.Alert?.Kicker == kicker).Select((l) => l.Alert!.Title));
            // Before level 4, nobody has a word for the Marea: the dead take the zones.
            c.Doom = 6;
            Look(c);
            Assert.Equal(Titles(1, new[] { "afr", "me", "na" }, true), Alerts(Lore.DoomName(1)));
            Assert.Matches("Los muertos toman", Alerts(Lore.DoomName(1))[0]);
            // `c.flooded!.sort()` sorts it where it is.
            c.Flooded!.Sort(StringComparer.Ordinal);
            Assert.Equal(new[] { "afr", "me", "na" }, c.Flooded);
            c.Doom = 5;
            Look(c);
            Assert.Matches("se retira", c.Log[0].Alert?.Title);
            Assert.Equal(new[] { "na" }, c.Flooded);
            // From level 4, the Marea by its name.
            c.Level = 4;
            c.Doom = 9;
            Look(c);
            Assert.Equal(Titles(4, new[] { "afr", "me", "oce", "sa" }, true), Alerts(Lore.DoomName(4)));
            Assert.Matches("La Marea llega", Alerts(Lore.DoomName(4))[0]);
            c.Doom = 8;
            Look(c);
            Assert.Matches("se retira", c.Log[0].Alert?.Title);
        }

        [Fact(DisplayName = "the Marea › a zone under the Marea sends nothing, can’t be newly contacted, and its missions bring one more group until it is freed")]
        public void AZoneUnderTheMareaSendsNothingCantBeNewlyContacted()
        {
            var c = Quiet();
            RunOn(c, new ContactCommand { Region = RegionIds.Oce });
            c.Doom = 9;
            // Nothing from La Vega: no supplies, medicines or food.
            c.NextSupplyDay = c.Day + 1;
            c.NextMedicineDay = c.Day + 1;
            c.NextFoodDay = c.Day + 1;
            var credits = c.Credits;
            var medicines = c.Medicines!.Value;
            Wait(c);
            Assert.Equal(credits + 60 + 30, c.Credits);
            Assert.Equal(medicines, c.Medicines);
            Assert.DoesNotContain(c.Log, (l) => l.Text.Contains("La Vega manda"));
            // Not to be contacted while under it.
            Assert.Matches("La Marea cubre|Los muertos han tomado", Campaign.ContactBlocker(c, RegionIds.Sa));
            // An operation there: one more group of the dead (the difficulty stays), gone when the zone is freed.
            var o = OfferIn(c, RegionIds.Oce);
            o.Difficulty = 2;
            var pods = o.Pods.Count;
            c.Offers.Add(o);
            Look(c);
            Assert.Equal((2, (bool?)true), (o.Difficulty, o.Tide));
            Assert.Equal(pods + 1, o.Pods.Count);
            c.Doom = 8;
            Look(c);
            Assert.Equal(2, o.Difficulty);
            Assert.Equal(pods, o.Pods.Count);
            Assert.Null(o.Tide);
        }

        [Fact(DisplayName = "the Marea › Nidos gather under the Marea or beside it")]
        public void NidosGatherUnderTheMareaOrBesideIt()
        {
            var near = new[] { "na", "afr", "me", "oce", "sa" };
            var close = 0;
            const int n = 300;
            for (var seed = 1; seed <= n; seed++)
            {
                var c = Campaign.CreateCampaign(new[] { 0 }, new Rng(seed));
                c.NextEventDay = 999;
                c.NextMissionDay = 999;
                c.NextRumorDay = 999;
                c.NextSupplyDay = 999;
                c.NextDoomDay = 999;
                c.Doom = 6;
                c.NextFacilityDay = c.Day + 1;
                Wait(c);
                var nest = c.Regions.FirstOrDefault((r) => r.Facility != null);
                if (nest != null && near.Contains(nest.Id)) close++;
            }
            // Three times as often: 15 of 18 weights, against 5 of 8 zones if they were even.
            Assert.True((double)close / n > 0.75, $"{close} of {n}");
        }
    }
}
