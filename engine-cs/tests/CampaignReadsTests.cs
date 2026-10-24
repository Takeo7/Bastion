using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// What only reads of campaign.ts (the second part of its port) against TypeScript: golden/campaign/reads.json holds,
    /// for each state of states.json, UI's sample and the saves of version 25, every reading record-campaign.ts `reads`
    /// makes, and those that read no campaign over all they take (`fixed`). The same readings in C#, made into the same
    /// JSON, must say the same, nulls apart (CampaignJsonTests.FirstDifference). Its texts went through text-fixes.ts.
    /// </summary>
    public class CampaignReadsTests
    {
        private static readonly JObject Recorded = CampaignJsonTests.Golden("reads.json");

        private static readonly IReadOnlyList<IReadOnlyList<int>> Conns = new[] { new[] { 0 }, new[] { 0, 1 } };
        private static readonly int[] Both = { 0, 1 };

        private static readonly IReadOnlyList<Vec2> TilesTried = new[]
        {
            new Vec2 { X = 0, Y = 0 }, new Vec2 { X = 2, Y = 2 }, new Vec2 { X = 27, Y = 27 }, new Vec2 { X = 30, Y = 31 }, new Vec2 { X = 12, Y = 40 },
            new Vec2 { X = 44, Y = 10 },
        };

        /// <summary>Its JSON, as the network writes it.</summary>
        private static JToken J(object? value) => value == null ? JValue.CreateNull() : JToken.Parse(CampaignJsonTests.Write(value));

        private static void Same(JToken typescript, JToken csharp, string label)
        {
            var difference = CampaignJsonTests.FirstDifference(CampaignJsonTests.Clean(typescript), CampaignJsonTests.Clean(csharp));
            Assert.True(difference == null, $"{label}: {difference}");
        }

        private static CampaignState Source(string source)
        {
            if (source.StartsWith("states.json#", StringComparison.Ordinal))
            {
                var name = source.Substring("states.json#".Length);
                var state = ((JArray)CampaignJsonTests.Golden("states.json")["states"]!).First((s) => (string)s["name"]! == name);
                return state["campaign"]!.ToObject<CampaignState>(CampaignJsonTests.Serializer)!;
            }
            return CampaignJsonTests.Golden(source)["campaign"]!.ToObject<CampaignState>(CampaignJsonTests.Serializer)!;
        }

        private static IEnumerable<string> Sorted(IEnumerable<string> ids) => ids.OrderBy((x) => x, StringComparer.Ordinal);

        /// <summary>record-campaign.ts actionsFor: every kind of shared decision, the tables' ids sorted.</summary>
        private static List<ProposalAction> ActionsFor(CampaignState c)
        {
            var pieces = c.Bastion.Pieces.Select((p) => p.Id).ToList();
            var actions = new List<ProposalAction>();
            actions.AddRange(Sorted(Base.Pieces.Keys).Select((piece) => new PlaceAction { Piece = piece, X = 0, Y = 0, Rot = 0 }));
            actions.AddRange(pieces.Append("zz").Select((piece) => new RaiseAction { Piece = piece }));
            actions.AddRange(pieces.Append("zz").Select((piece) => new MendAction { Piece = piece }));
            actions.Add(new PlaceLineAction { Piece = PieceIds.Palisade, Cells = new List<Vec2> { new Vec2 { X = 1, Y = 1 }, new Vec2 { X = 2, Y = 1 }, new Vec2 { X = 3, Y = 1 } }, Rot = 0 });
            actions.Add(new FellAction { Tiles = new List<Vec2> { new Vec2 { X = 1, Y = 1 }, new Vec2 { X = 2, Y = 2 } } });
            actions.Add(new RearmAction { Piece = "b1" });
            actions.Add(new DismantleAction { Piece = "b1" });
            actions.Add(new LevelUpAction());
            actions.Add(new ContactAction { Region = RegionIds.Oce });
            actions.AddRange(Sorted(Progression.Crafts.Keys).Select((project) => new CraftAction { Project = project }));
            actions.Add(new RecruitAction { For = 0 });
            actions.AddRange(Sorted(Content.Items.Keys).Select((item) => new BuyAction { Item = item }));
            actions.Add(new ChooseMissionAction { Id = "m1" });
            actions.Add(new EventAction { Option = 0 });
            actions.Add(new InvestigateAction { Id = "r1" });
            return actions;
        }

        /// <summary>record-campaign.ts reads, in C#.</summary>
        private static JObject Reads(CampaignState c)
        {
            var siege = Campaign.ActiveOffer(c)?.Siege == true;
            var posts = (c.SiegePosts ?? new Dictionary<string, Vec2>()).Values.ToList();
            var r = new JObject
            {
                ["soldierCap"] = Campaign.SoldierCap(c),
                ["beds"] = Campaign.Beds(c),
                ["bedsFree"] = Campaign.BedsFree(c),
                ["healRate"] = Campaign.HealRate(c),
                ["rewardMultiplier"] = Campaign.RewardMultiplier(c),
                ["memorialWill"] = Campaign.MemorialWill(c),
                ["rangeAim"] = Campaign.RangeAim(c),
                ["garageCut"] = J(Campaign.GarageCut(c)),
                ["listeningDelay"] = Campaign.ListeningDelay(c),
                ["workCrews"] = Campaign.WorkCrews(c),
                ["workDays"] = new JArray(Enumerable.Range(1, 8).Select((d) => Campaign.WorkDays(c, d))),
                ["mustWait"] = Campaign.MustWait(c),
                ["treatCost"] = Campaign.TreatCost(c),
                ["hasQuarantine"] = Campaign.HasQuarantine(c),
                ["dailyRations"] = Campaign.DailyRations(c),
                ["gardenYield"] = Campaign.GardenYield(c),
                ["convoyIn"] = Campaign.ConvoyIn(c),
                ["convoyIncome"] = Campaign.ConvoyIncome(c),
                ["rationsPerMission"] = Campaign.RationsPerMission(c),
                ["gardenPerMission"] = Campaign.GardenPerMission(c),
                ["floodedZones"] = J(Campaign.FloodedZones(c)),
                ["contactCost"] = Campaign.ContactCost(c),
                ["contactCapacity"] = Campaign.ContactCapacity(c),
                ["hasRadio"] = Campaign.HasRadio(c),
                ["recruitCost"] = Campaign.RecruitCost(c),
                ["wintry"] = Campaign.Wintry(c),
                ["veteranEnemies"] = Campaign.VeteranEnemies(c),
                ["learningBonus"] = Campaign.LearningBonus(c),
                ["siegeHeard"] = J(Campaign.SiegeHeard(c)),
                ["vehicleOf"] = Campaign.VehicleOf(c),
                ["squadMax"] = Campaign.SquadMax(c),
                ["squadLimit"] = new JArray(Conns.Select((k) => Campaign.SquadLimit(c, k))),
                ["defaultSquad"] = new JArray(Conns.Select((k) => J(Campaign.DefaultSquad(c, k)))),
                ["squadPads"] = J(Campaign.SquadPads(c)),
                ["squadPlace"] = new JArray(Both.SelectMany((slot) => Conns.Select((k) => J(Campaign.SquadPlace(c, slot, k))))),
                ["medicineCures"] = Campaign.MedicineCures(c),
                ["activeOffer"] = J(Campaign.ActiveOffer(c)?.Id),
                ["campaignHackBonus"] = Campaign.CampaignHackBonus(c),
                ["nextLevel"] = J(Campaign.NextLevel(c)),
                ["levelUpBlocker"] = J(Campaign.LevelUpBlocker(c)),
                ["canAfford"] = new JArray(new[] { 0, 50, 100, 1000 }.Select((k) => Campaign.CanAfford(c, k))),
                ["buildSquad"] = new JArray(Campaign.BuildSquad(c).Select((m) => m.CampaignId)),
                ["facilities"] = new JObject(Campaign.Facilities.Keys.Select((id) => new JProperty(id, new JArray(Campaign.FacilityCount(c, id), Campaign.FacilityLevel(c, id))))),
                ["items"] = new JObject(Content.Items.Keys.Select((id) => new JProperty(id, new JArray(Campaign.ShopPrice(c, id), Campaign.ItemsFree(c, id))))),
                ["regions"] = new JObject(RegionIds.All.Select((id) => new JProperty(id, new JArray(Campaign.UnderMarea(c, id), J(Campaign.ContactBlocker(c, id)), Campaign.RegionIncome(c, id))))),
                ["crafts"] = new JObject(Progression.Crafts.Keys.Select((id) => new JProperty(id, J(Campaign.CraftBlocker(c, id))))),
                ["quests"] = new JObject(QuestIds.All.Select((id) => new JProperty(id, Campaign.QuestAvailable(c, id)))),
                ["soldiers"] = new JArray(c.Soldiers.Select((s) => new JObject
                {
                    ["id"] = s.Id,
                    ["ready"] = Campaign.SoldierReady(s),
                    ["turnDay"] = J(Campaign.TurnDay(c, s)),
                    ["weaponLabel"] = Campaign.WeaponLabel(c, s),
                    ["arms"] = J(Campaign.SoldierArms(c, s)),
                    ["veterancy"] = Campaign.VeterancyName(s),
                    ["canSpend"] = Campaign.CanSpendPoints(c, s),
                    ["sex"] = J(Campaign.SexOfPerson(s.Name)),
                    ["member"] = s.Alive ? J(Campaign.SquadMember(c, s)) : JValue.CreateNull(),
                    ["post"] = new JArray(new Vec2?[] { null }.Concat(siege ? TilesTried.Concat(posts) : Enumerable.Empty<Vec2>()).Select((at) => J(Campaign.PostBlocker(c, s.Owner, s.Id, at)))),
                })),
                ["weapons"] = new JArray((c.FoundWeapons ?? new List<FoundWeapon>()).Select((w) => new JObject
                {
                    ["id"] = w.Id,
                    ["repairParts"] = Campaign.RepairParts(w),
                    ["repair"] = new JArray(J(Campaign.RepairBlocker(c, 0, w.Id)), J(Campaign.RepairBlocker(c, 1, w.Id, Both)), J(Campaign.RepairBlocker(c, 1, w.Id, new[] { 1 }))),
                    ["mods"] = new JObject(Content.WeaponMods.Keys.Select((m) => new JProperty(m, new JArray(J(Campaign.ModBlocker(c, 0, w.Id, m, true)), J(Campaign.ModBlocker(c, 0, w.Id, m, false)))))),
                })),
                ["pieces"] = new JArray(c.Bastion.Pieces.Select((p) =>
                {
                    var raise = Campaign.RaiseCost(p);
                    return new JObject
                    {
                        ["id"] = p.Id,
                        ["works"] = Campaign.BuildingWorks(p),
                        ["raise"] = raise.Error != null ? (JToken)raise.Error : new JObject { ["cost"] = raise.Cost, ["level"] = raise.Level },
                    };
                })),
                ["offers"] = new JArray(c.Offers.Select((o) =>
                {
                    var map = Campaign.OfferMap(c, o);
                    var night = Campaign.ByNight(o);
                    return new JObject
                    {
                        ["id"] = o.Id,
                        ["info"] = J(Campaign.MissionInfo(c, o)),
                        ["vip"] = J(Campaign.OfferVip(c, o)),
                        ["briefing"] = Campaign.OfferBriefing(c, o),
                        ["signature"] = Campaign.OfferSignature(c, o),
                        ["map"] = map.Kind == MapSourceKinds.Base ? new JObject { ["kind"] = "base", ["sameBase"] = ReferenceEquals(map.Base, c.Bastion) } : J(map),
                        ["siege"] = J(Campaign.SiegePlan(c, o)),
                        ["night"] = new JArray(ReferenceEquals(night, o), J(night.Night), J(night.Reward)),
                        ["nightFor"] = J(o.Map.Kind == MapSourceKinds.Generated ? Campaign.NightFor(o.Map.Seed ?? 0) : (bool?)null),
                    };
                })),
                ["proposalCosts"] = new JArray(ActionsFor(c).Select((a) => Campaign.ProposalCost(c, a))),
            };
            return r;
        }

        public static IEnumerable<object[]> Sources() => ((JArray)Recorded["states"]!).Select((s) => new object[] { (string)s["source"]! });

        [Theory]
        [MemberData(nameof(Sources))]
        public void EveryReadingOfAStateIsTypeScripts(string source)
        {
            var recorded = ((JArray)Recorded["states"]!).First((s) => (string)s["source"]! == source)["reads"]!;
            Same(recorded, Reads(Source(source)), source);
        }

        [Fact]
        public void TheReadingsOfNoCampaignAreTypeScripts()
        {
            var f = (JObject)Recorded["fixed"]!;
            Same(f["upgradeCost"]!, new JObject(Campaign.Facilities.Keys.Select((id) => new JProperty(id, new JArray(J(Campaign.UpgradeCost(id, 2)), J(Campaign.UpgradeCost(id, 3)))))), "upgradeCost");
            Same(f["roads"]!, new JObject(RegionIds.All.Select((a) => new JProperty(a, new JObject(RegionIds.All.Select((b) => new JProperty(b, new JArray(J(Campaign.RoadRoute(a, b)), Campaign.RoadHops(a, b)))))))), "roadRoute, roadHops");
            Same(f["mareaThreshold"]!, new JObject(RegionIds.All.Select((id) => new JProperty(id, J(Campaign.MareaThreshold(id))))), "mareaThreshold");
            Same(f["floodedZones"]!, new JArray(Enumerable.Range(0, 13).Select((doom) => J(Campaign.FloodedZones(doom)))), "floodedZones");
            Same(f["oldDaysToNow"]!, new JArray(Enumerable.Range(0, 14).Select((i) => Campaign.OldDaysToNow(i - 1))), "oldDaysToNow");
            Same(f["costText"]!, new JArray(new[] { 0, 1, 35, 120 }.Select(Campaign.CostText)), "costText");
            Same(f["campaignOutdated"]!, new JArray(Enumerable.Range(0, 9).Select((i) => Campaign.CampaignOutdated(18 + i))), "campaignOutdated");
            Same(f["nightFor"]!, new JArray(f["nightFor"]!.Select((p) => new JArray((int)p[0]!, Campaign.NightFor((int)p[0]!)))), "nightFor");
            Same(f["sexOfPerson"]!, new JArray(f["sexOfPerson"]!.Select((p) => new JArray((string)p[0]!, J(Campaign.SexOfPerson((string)p[0]!))))), "sexOfPerson");
        }
    }
}
