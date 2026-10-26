using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The campaign's types (Types/Campaign.cs, CampaignCommand.cs) against TypeScript's: campaigns read into the C#
    /// CampaignState and written again must say the same, nulls apart (engine-cs README). From golden/campaign/:
    /// - states.json: 26 states made by the TypeScript engine (record-campaign.ts);
    /// - saves/: campaigns saved while playing the 0.7.0 to the 0.10.x (versions 20 to 25), as they were;
    /// - muestra.json: UI's sample (.dev-snapshots/ui/estrategia-0101/partida-muestra.json, version 25 on day 20 with two
    ///   players), written by hand: its Taller's work lacks `plot`, `kind` and `total`, which C# writes with their
    ///   defaults, and nothing else.
    /// And the tables, constants and ids of Campaign/Campaign.cs against tables.json, and one command of each kind
    /// (commands.json, as campaignCommandSchema takes them).
    /// </summary>
    public class CampaignJsonTests
    {
        private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "golden", "campaign");

        /// <summary>The network's JSON (Net/Json.cs), which tells the proposals' actions and the commands apart by `type`.</summary>
        internal static readonly JsonSerializer Serializer = Json.Serializer;

        internal static JObject Golden(string name) => JObject.Parse(File.ReadAllText(Path.Combine(Dir, name)));

        internal static string Write(object value)
        {
            using (var text = new StringWriter())
            {
                Serializer.Serialize(text, value);
                return text.ToString();
            }
        }

        /// <summary>Nulls taken out, recursively: missing and null are the same.</summary>
        internal static JToken Clean(JToken token)
        {
            switch (token)
            {
                case JObject o:
                    var copy = new JObject();
                    foreach (var p in o.Properties())
                        if (p.Value.Type != JTokenType.Null) copy[p.Name] = Clean(p.Value);
                    return copy;
                case JArray a:
                    return new JArray(a.Select(Clean));
                default:
                    return token.DeepClone();
            }
        }

        /// <summary>Where `a` (TypeScript) and `b` (C#) first differ: a JSON path and the two values; null if they don't.</summary>
        internal static string? FirstDifference(JToken a, JToken b, string path = "$")
        {
            if (a.Type != b.Type)
            {
                // A whole number written as 2.0 or 2 is the same number.
                if ((a.Type == JTokenType.Integer || a.Type == JTokenType.Float) && (b.Type == JTokenType.Integer || b.Type == JTokenType.Float) && (double)a == (double)b) return null;
                return $"{path}: TypeScript {Short(a)} · C# {Short(b)}";
            }
            switch (a)
            {
                case JObject oa:
                    var ob = (JObject)b;
                    foreach (var name in oa.Properties().Select((p) => p.Name).Union(ob.Properties().Select((p) => p.Name)))
                    {
                        if (oa[name] == null) return $"{path}.{name}: only in C# ({Short(ob[name]!)})";
                        if (ob[name] == null) return $"{path}.{name}: lost in C# (TypeScript {Short(oa[name]!)})";
                        var d = FirstDifference(oa[name]!, ob[name]!, $"{path}.{name}");
                        if (d != null) return d;
                    }
                    return null;
                case JArray aa:
                    var ab = (JArray)b;
                    if (aa.Count != ab.Count) return $"{path}: {aa.Count} items in TypeScript, {ab.Count} in C#";
                    for (int i = 0; i < aa.Count; i++)
                    {
                        var d = FirstDifference(aa[i], ab[i], $"{path}[{i}]");
                        if (d != null) return d;
                    }
                    return null;
                default:
                    return JToken.DeepEquals(a, b) ? null : $"{path}: TypeScript {Short(a)} · C# {Short(b)}";
            }
        }

        private static string Short(JToken t)
        {
            var s = t.ToString(Formatting.None);
            return s.Length > 100 ? s.Substring(0, 100) + "…" : s;
        }

        /// <summary>Read as `T` and written again: what came back, which must say what `typescript` says but for its nulls.</summary>
        private static T SameThrough<T>(JToken typescript, string label, Action<JObject>? written = null)
        {
            var value = typescript.ToObject<T>(Serializer)!;
            var back = JObject.Parse(Write(value!));
            written?.Invoke(back);
            var difference = FirstDifference(Clean(typescript), Clean(back));
            Assert.True(difference == null, $"{label}: {difference}");
            return value;
        }

        // ------------------------------------------------------------------- the state

        [Fact]
        public void EveryRecordedStateComesThroughTheSame()
        {
            var states = (JArray)Golden("states.json")["states"]!;
            Assert.Equal(26, states.Count);
            foreach (var s in states)
            {
                var c = SameThrough<CampaignState>(s["campaign"]!, (string)s["name"]!);
                Assert.Equal(Campaign.CampaignVersion, c.Version);
            }
        }

        public static IEnumerable<object[]> Saves() => new[] { "v20.json", "v20-asedio-ganado.json", "v21.json", "v22.json", "v23-migrada.json", "v23-derrota.json", "v24.json", "v25-093.json", "v25.json" }.Select((n) => new object[] { n });

        [Theory]
        [MemberData(nameof(Saves))]
        public void ASavedCampaignComesThroughTheSame(string name)
        {
            SameThrough<CampaignState>(Golden(Path.Combine("saves", name))["campaign"]!, name);
        }

        [Fact]
        public void TheSampleComesThroughTheSame()
        {
            var sample = Golden("muestra.json")["campaign"]!;
            var c = SameThrough<CampaignState>(sample, "muestra.json", (back) =>
            {
                // Written by hand: its works hold only `project` and `daysLeft`. What C# adds is the defaults, and only those.
                foreach (var w in (JArray)back["base"]!["works"]!)
                {
                    Assert.Equal("", (string)w["plot"]!);
                    Assert.Equal(ConstructionKinds.Craft, (string)w["kind"]!);
                    Assert.Equal(0, (int)w["total"]!);
                    ((JObject)w).Remove("plot");
                    ((JObject)w).Remove("kind");
                    ((JObject)w).Remove("total");
                }
            });
            Assert.Equal(25, c.Version);
            Assert.Equal(20, c.Day);
            Assert.Equal(new List<int> { 0, 1 }, c.Players);
            Assert.Equal(3, c.Level);
            Assert.Equal(8, c.Soldiers.Count);
            Assert.Equal(new[] { false, false }, c.LaunchReady);
            Assert.Null(c.Proposal);
            Assert.Equal("r1", c.Listening);
            Assert.Equal(56, c.Bastion.Width);
            Assert.Equal(3, c.Siege.In);
            Assert.Equal(2, c.Inventory["fragGrenade"]);
            Assert.True(c.Offers[0].Reward.Recruit is bool recruit && recruit);
            Assert.Equal("generated", c.Offers[0].Map.Kind);
            Assert.Equal(24, c.FoundWeapons![2].Condition);
            Assert.Equal("r12", c.Log.First((l) => l.Alert?.Rumor != null).Alert!.Rumor);
            Assert.Equal(new List<int> { 8 }, c.LastReport!.Squad[0].Bars![0].Growth);
        }

        private readonly ITestOutputHelper output;

        public CampaignJsonTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void TheOriginalSampleIsTheOneCopied()
        {
            // Outside git (.dev-snapshots), only on the Mac that has it, and UI's to make again: the one that counts is
            // golden/campaign/muestra.json. So this only says so, never fails, when the original is missing or has changed.
            var original = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".dev-snapshots", "ui", "estrategia-0101", "partida-muestra.json");
            if (!File.Exists(original))
            {
                output.WriteLine("partida-muestra.json no está: vale la de golden/campaign/muestra.json.");
                return;
            }
            var campaign = JObject.Parse(File.ReadAllText(original))["campaign"];
            if (!JToken.DeepEquals(campaign, Golden("muestra.json")["campaign"])) output.WriteLine("Aviso: partida-muestra.json ha cambiado desde que se copió en golden/campaign/muestra.json, que es la que vale.");
        }

        [Fact]
        public void ARewardFromBeforeTheSevenTwoKeepsItsClass()
        {
            // Campaigns of the 0.7.0 and 0.7.1 named the class a recruit came with (MissionReward.recruit): just as true.
            var c = Golden(Path.Combine("saves", "v20.json"))["campaign"]!.ToObject<CampaignState>(Serializer)!;
            var named = c.Offers.Select((o) => o.Reward.Recruit).OfType<string>().ToList();
            Assert.NotEmpty(named);
            Assert.All(named, (r) => Assert.Contains(r, ClassIds.All));
            Assert.Contains("\"recruit\":\"" + named[0] + "\"", Write(c));
            // And the base of before: its plots, always none.
            Assert.NotNull(c.Base.Plots);
            Assert.Empty(c.Base.Plots!);
        }

        [Fact]
        public void TheQuestLogKeepsJavaScriptsOrder()
        {
            // Read in the order it comes; a quest that starts again leaves it and, when it ends again, comes back last.
            var c = JObject.Parse("{\"questLog\":{\"mole\":{\"outcome\":\"success\",\"day\":4},\"faces\":{\"outcome\":\"failure\",\"day\":6}}}").ToObject<CampaignState>(Serializer)!;
            Assert.Equal(new[] { "mole", "faces" }, c.QuestLog.Keys);
            c.QuestLog.Delete("mole");
            c.QuestLog.Set("numbers", new QuestLogEntry { Outcome = "success", Day = 8 });
            c.QuestLog.Set("mole", new QuestLogEntry { Outcome = "failure", Day = 9 });
            Assert.Equal(new[] { "faces", "numbers", "mole" }, c.QuestLog.Keys);
            var written = (JObject)JObject.Parse(Write(c))["questLog"]!;
            Assert.Equal(new[] { "faces", "numbers", "mole" }, written.Properties().Select((p) => p.Name));
        }

        [Fact]
        public void ProposalsComeAsTheirOwnClass()
        {
            var states = ((JArray)Golden("states.json")["states"]!).ToDictionary((s) => (string)s["name"]!, (s) => s["campaign"]!.ToObject<CampaignState>(Serializer)!);
            Assert.IsType<ContactAction>(states["proposal-contact"].Proposal!.Action);
            Assert.IsType<ChooseMissionAction>(states["proposal-choose"].Proposal!.Action);
            Assert.Equal(1, Assert.IsType<RecruitAction>(states["proposal-recruit"].Proposal!.Action).For);
            Assert.Equal("fragGrenade", Assert.IsType<BuyAction>(states["proposal-buy"].Proposal!.Action).Item);
            Assert.Equal("palisade", Assert.IsType<PlaceAction>(states["proposal-place"].Proposal!.Action).Piece);
            Assert.All(states.Values.Where((c) => c.Proposal != null), (c) => Assert.Equal(1, c.Proposal!.By));
            // The ones no new campaign makes, those of the base of before, and calling off a mission (C# only: Arturo, 26/10).
            var others = new ProposalAction[]
            {
                new RaiseAction { Piece = "b2" },
                new PlaceLineAction { Piece = "wall", Cells = new List<Vec2> { new Vec2 { X = 3, Y = 4 } }, Rot = 2 },
                new FellAction { Tiles = new List<Vec2> { new Vec2 { X = 5, Y = 6 } } },
                new MendAction { Piece = "b3" },
                new RearmAction { Piece = "b4" },
                new DismantleAction { Piece = "b5" },
                new LevelUpAction(),
                new EventAction { Option = 1 },
                new InvestigateAction { Id = "r3" },
                new CraftAction { Project = "ammo1" },
                new RecruitAction { For = 0, Squad = true },
                new ClearAction { Plot = "p1" },
                new BuildAction { Plot = "p1", Facility = "workshop" },
                new UpgradeAction { Plot = "p2" },
                new CancelMissionAction { Id = "m4" },
            };
            foreach (var action in others)
            {
                var json = Write(new Proposal { By = 0, Action = action, Text = "algo" });
                var back = JObject.Parse(json).ToObject<Proposal>(Serializer)!;
                Assert.Equal(action.GetType(), back.Action.GetType());
                Assert.Equal(json, Write(back));
            }
            Assert.Equal(19, new TagConverter<ProposalAction>("type", (a) => a.Type).Tags.Count);
        }

        // ------------------------------------------------------------------- the commands

        [Fact]
        public void EveryCommandComesThroughTheSame()
        {
            var commands = (JArray)Golden("commands.json")["commands"]!;
            var tags = new HashSet<string>();
            foreach (var cmd in commands)
            {
                var read = SameThrough<CampaignCommand>(cmd, (string)cmd["type"]!);
                Assert.Equal((string)cmd["type"]!, read.Type);
                tags.Add(read.Type);
            }
            // 30 variants of CampaignCommand, each told by its `type`, and every one of them recorded.
            var converter = new TagConverter<CampaignCommand>("type", (c) => c.Type);
            Assert.Equal(30, converter.Tags.Count);
            Assert.Equal(converter.Tags.OrderBy((t) => t, StringComparer.Ordinal), tags.OrderBy((t) => t, StringComparer.Ordinal));
        }

        [Fact]
        public void ACommandsNullsLeaveTheJson()
        {
            var post = Assert.IsType<PostCommand>(JObject.Parse("{\"type\":\"post\",\"soldier\":\"c1\",\"at\":null}").ToObject<CampaignCommand>(Serializer));
            Assert.Null(post.At);
            Assert.Equal("{\"type\":\"post\",\"soldier\":\"c1\"}", Write(post));
            var assign = Assert.IsType<AssignWeaponCommand>(JObject.Parse("{\"type\":\"assignWeapon\",\"weapon\":\"w1\"}").ToObject<CampaignCommand>(Serializer));
            Assert.Null(assign.Soldier);
            Assert.Throws<JsonSerializationException>(() => JObject.Parse("{\"type\":\"teleport\"}").ToObject<CampaignCommand>(Serializer));
        }

        // ------------------------------------------------------------------- the tables

        private static void SameAs(JToken typescript, object csharp, string label)
        {
            var difference = FirstDifference(Clean(typescript), Clean(JToken.Parse(Write(csharp))));
            Assert.True(difference == null, $"{label}: {difference}");
        }

        [Fact]
        public void TheRegionsAndRoadsAreTypeScripts()
        {
            var tables = Golden("tables.json");
            SameAs(tables["regions"]!, Campaign.Regions, "REGIONS");
            SameAs(tables["roads"]!, Campaign.Roads, "ROADS");
            // In the same order, which Object.keys gives.
            Assert.Equal(tables["regions"]!.Children<JProperty>().Select((p) => p.Name), Campaign.Regions.Keys);
            Assert.Equal(RegionIds.All, Campaign.Regions.Keys);
        }

        [Fact]
        public void TheFacilitiesAreTypeScripts()
        {
            var tables = Golden("tables.json");
            SameAs(tables["facilities"]!, Campaign.Facilities, "FACILITIES");
            Assert.Equal(tables["facilities"]!.Children<JProperty>().Select((p) => p.Name), Campaign.Facilities.Keys);
            Assert.Equal(FacilityIds.All.OrderBy((x) => x, StringComparer.Ordinal), Campaign.Facilities.Keys.OrderBy((x) => x, StringComparer.Ordinal));
        }

        [Fact]
        public void TheConstantsAreTypeScripts()
        {
            var constants = new Dictionary<string, object>
            {
                ["BUNKER_BEDS"] = Campaign.BunkerBeds,
                ["HOME_REGION"] = Campaign.HomeRegion,
                ["MAREA_THRESHOLDS"] = Campaign.MareaThresholds,
                ["MAREA_SOURCE"] = Campaign.MareaSource,
                ["RECRUIT_COST"] = Campaign.RecruitPrice,
                ["MAX_SOLDIERS_PER_PLAYER"] = Campaign.MaxSoldiersPerPlayer,
                ["DOOM_MAX"] = Campaign.DoomMax,
                ["OLD_DAYS_PER_MISSION"] = Campaign.OldDaysPerMission,
                ["FRESH_CAMPAIGN_VERSION"] = Campaign.FreshCampaignVersion,
                ["CAMPAIGN_VERSION"] = Campaign.CampaignVersion,
                ["START_MEDICINES"] = Campaign.StartMedicines,
                ["TURN_DAYS"] = Campaign.TurnDays,
                ["INFIRMARY_TURN_DAYS"] = Campaign.InfirmaryTurnDays,
                ["MEDICATE_COST"] = Campaign.MedicateCost,
                ["MEDICATE_DAYS"] = Campaign.MedicateDays,
                ["ENCLAVES"] = Campaign.Enclaves,
                ["ENCLAVE_DAYS"] = Campaign.EnclaveDays,
                ["TURNED_CAUSE"] = Campaign.TurnedCause,
                ["START_FOOD"] = Campaign.StartFood,
                ["EATERS_PER_RATION"] = Campaign.EatersPerRation,
                ["LA_VEGA_FOOD"] = Campaign.LaVegaFood,
                ["LA_VEGA_FOOD_DAYS"] = Campaign.LaVegaFoodDays,
                ["GARDEN_RATIONS"] = Campaign.GardenRations,
                ["HUNGRY_WILL"] = Campaign.HungryWill,
                ["REWARD_FOOD"] = Campaign.RewardFood,
                ["REWARD_PARTS"] = Campaign.RewardParts,
                ["THAW_AFTER_STORY"] = Campaign.ThawAfterStory,
                ["SIEGE_FIRST_IN"] = Campaign.SiegeFirstIn,
                ["SIEGE_EVERY"] = Campaign.SiegeEvery,
                ["SIEGE_OPENING"] = Campaign.SiegeOpening,
                ["SIEGE_FIRST"] = Campaign.SiegeFirst,
                ["SIEGE_GROWTH"] = Campaign.SiegeGrowth,
                ["DAYS_PER_MISSION"] = Campaign.DaysPerMission,
                ["SIEGE_NIGHT"] = Campaign.SiegeNight,
                ["VEHICLE_SEATS"] = Campaign.VehicleSeats,
                ["VEHICLE_FROM"] = Campaign.VehicleFrom,
                ["LATE_KIT"] = Campaign.LateKit,
            };
            SameAs(Golden("tables.json")["constants"]!, constants, "constants");
        }

        [Fact]
        public void TheIdsAreTypeScripts()
        {
            var ids = (JObject)Golden("tables.json")["ids"]!;
            Assert.Equal(ids["regions"]!.Values<string>(), RegionIds.All);
            Assert.Equal(ids["events"]!.Values<string>(), CampaignEventIds.All);
            Assert.Equal(ids["quests"]!.Values<string>(), QuestIds.All);
            Assert.Equal(ids["traits"]!.Values<string>(), TraitIds.All);
            Assert.Equal(ids["baseViews"]!.Values<string>(), BaseViews.All);
            Assert.Equal(ids["facilities"]!.Values<string>(), Campaign.Facilities.Keys);
        }
    }
}
