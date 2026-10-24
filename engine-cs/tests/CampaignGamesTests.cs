using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Whole campaigns recorded with the TypeScript engine (golden/campaign/games/, record-campaign.ts), played again in
    /// C# operation by operation: the same commands with the same seat and who is connected, the same missions over (as
    /// the recording kept them), the same Rng. After each one the result, the Rng and the whole campaign must be
    /// TypeScript's, nulls apart (CampaignJsonTests.FirstDifference); and what the screens read at a look (nextSteps of both
    /// seats, the squad going out, what the operation names) and at the end (the vaccine's title, the epilogue). Two of
    /// them (final-break, final-record) reach the final mission and the vaccine, and win. Texts went through text-fixes.ts.
    /// </summary>
    public class CampaignGamesTests
    {
        /// <summary>The recorded ones; CAMPAIGN_GAMES=<folder>, others recorded there (record-campaign.ts EXTRA_GAMES), outside git.</summary>
        private static readonly string Dir = Environment.GetEnvironmentVariable("CAMPAIGN_GAMES") ?? Path.Combine(AppContext.BaseDirectory, "golden", "campaign", "games");

        public static IEnumerable<object[]> Games() => Directory.GetFiles(Dir, "*.json").OrderBy((f) => f, StringComparer.Ordinal).Select((f) => new object[] { Path.GetFileNameWithoutExtension(f) });

        private static JToken J(object? value) => value == null ? JValue.CreateNull() : JToken.Parse(CampaignJsonTests.Write(value));

        /// <summary>A recorded change (record-campaign.ts campaignDelta) made to `expected`.</summary>
        private static void ApplyDelta(JObject expected, JObject delta)
        {
            foreach (var p in delta.Properties())
            {
                if (p.Value is JObject o && o["$items"] is JObject items)
                {
                    var list = (JArray)expected[p.Name]!;
                    foreach (var i in items.Properties()) list[int.Parse(i.Name)] = i.Value.DeepClone();
                }
                else if (p.Value is JObject u && u["$unshift"] is JArray front)
                {
                    var was = (JArray)expected[p.Name]!;
                    var now = new JArray(front.Select((x) => x.DeepClone()).Concat(was.Select((x) => x.DeepClone())).Take((int)u["length"]!));
                    expected[p.Name] = now;
                }
                else if (p.Value.Type == JTokenType.Null) expected.Remove(p.Name);
                else expected[p.Name] = p.Value.DeepClone();
            }
        }

        private static string? Differs(JToken typescript, JToken csharp)
        {
            return CampaignJsonTests.FirstDifference(CampaignJsonTests.Clean(typescript), CampaignJsonTests.Clean(csharp));
        }

        private static void Check(CampaignState c, JObject expected, Rng rng, JToken state, string where)
        {
            var d = Differs(expected, JObject.Parse(CampaignJsonTests.Write(c)));
            Assert.True(d == null, $"{where}: {d}");
            Assert.True(JToken.DeepEquals(state, new JArray(rng.State)), $"{where}: the Rng is {string.Join(",", rng.State)}, TypeScript's {state.ToString(Newtonsoft.Json.Formatting.None)}");
        }

        /// <summary>CampaignResult as TypeScript writes it: `{ ok: true, launch? }` or `{ ok: false, error }`.</summary>
        private static JObject ResultOf(CampaignResult r)
        {
            var o = new JObject { ["ok"] = r.Ok };
            if (!r.Ok) o["error"] = r.Error;
            if (r.Launch) o["launch"] = true;
            return o;
        }

        [Theory]
        [MemberData(nameof(Games))]
        public void AWholeCampaignGoesAsInTypeScript(string name)
        {
            var game = JObject.Parse(File.ReadAllText(Path.Combine(Dir, $"{name}.json")));
            var rng = new Rng((int)game["seed"]!);
            var expected = (JObject)game["campaign"]!.DeepClone();
            var start = (JObject)game["start"]!;
            var c = start["create"] is JArray players
                ? Campaign.CreateCampaign(players.Select((p) => (int)p).ToList(), rng)
                : expected.ToObject<CampaignState>(CampaignJsonTests.Serializer)!;
            Check(c, expected, rng, game["rng"]!, $"{name}, start");
            var both = new[] { 0, 1 };
            var n = 0;
            foreach (var op in (JArray)game["ops"]!)
            {
                var kind = (string)op["op"]!;
                var where = $"{name}, operation {n} ({kind}{(kind == "cmd" ? $" {(string)op["cmd"]!["type"]!} by {(int)op["slot"]!}" : "")})";
                switch (kind)
                {
                    case "cmd":
                    {
                        var cmd = op["cmd"]!.ToObject<CampaignCommand>(CampaignJsonTests.Serializer)!;
                        var connected = op["connected"]!.Select((x) => (int)x).ToList();
                        var r = Campaign.ExecuteCampaignCommand(c, cmd, new CampaignContext { Slot = (int)op["slot"]!, Connected = connected, Rng = rng });
                        var d = Differs(op["result"]!, ResultOf(r));
                        Assert.True(d == null, $"{where}, its result: {d}");
                        break;
                    }
                    case "opening":
                        Campaign.OpeningMission(c);
                        break;
                    case "migrate":
                        c = Campaign.MigrateCampaign(c);
                        break;
                    case "addPlayer":
                    {
                        var got = Campaign.AddPlayer(c, (int)op["slot"]!, rng);
                        Assert.Equal((int)op["got"]!, got);
                        // The room's bringPlayersIn: a squad being picked is redone for both.
                        if (c.ActiveMission != null && Campaign.ActiveOffer(c)?.Siege != true)
                        {
                            c.Squad = Campaign.DefaultSquad(c, both);
                            Campaign.SyncPads(c);
                        }
                        break;
                    }
                    case "defaultSquad":
                        c.Squad = Campaign.DefaultSquad(c, op["connected"]!.Select((x) => (int)x).ToList());
                        Campaign.SyncPads(c);
                        break;
                    case "look":
                    {
                        var offer = Campaign.ActiveOffer(c);
                        var reads = new JObject { ["next"] = new JArray(J(Campaign.NextSteps(c, 0, "Ana")), J(Campaign.NextSteps(c, 1))) };
                        if (offer != null)
                        {
                            reads["squad"] = J(Campaign.BuildSquad(c));
                            reads["info"] = J(Campaign.MissionInfo(c, offer));
                            reads["briefing"] = Campaign.OfferBriefing(c, offer);
                            reads["signature"] = Campaign.OfferSignature(c, offer);
                        }
                        var d = Differs(op["reads"]!, reads);
                        Assert.True(d == null, $"{where}, what it reads: {d}");
                        break;
                    }
                    case "mission":
                    {
                        var mission = op["mission"]!.ToObject<GameState>(CampaignJsonTests.Serializer)!;
                        Campaign.ApplyMissionResult(c, mission, rng);
                        break;
                    }
                    case "advanceTime":
                        Campaign.AdvanceTime(c, rng);
                        break;
                    case "end":
                    {
                        // How it ended: the title of the vaccine's answer and the last page (Lore.EndingTitles, Lore.Epilogue).
                        var reads = new JObject
                        {
                            ["title"] = J(c.Ending != null ? Lore.EndingTitles[c.Ending] : null),
                            ["epilogue"] = Lore.Epilogue(c.Outcome!, c.Ending, c.Doom >= Campaign.DoomMax),
                        };
                        var d = Differs(op["reads"]!, reads);
                        Assert.True(d == null, $"{where}, the ending: {d}");
                        break;
                    }
                    default:
                        throw new InvalidOperationException($"{where}: unknown operation");
                }
                ApplyDelta(expected, (JObject)op["delta"]!);
                Check(c, expected, rng, op["rng"]!, where);
                n++;
            }
        }
    }
}
