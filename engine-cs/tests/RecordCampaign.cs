using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// golden/campaign recorded with C# (record-campaign.ts was TypeScript's). The campaigns of states.json, saves/ and
    /// muestra.json, and the commands of commands.json, are what the others start from: they stay. What C# says now:
    /// - tables.json, the tables, constants and ids (CampaignJsonTests);
    /// - reads.json, every reading of each state (CampaignReadsTests.Reads, Fixed);
    /// - games/, each campaign played again operation by operation as CampaignGamesTests does, the same commands by the
    ///   same seats and the missions over as they were: the start (a new campaign, or a save as it is), and after each
    ///   one its result, what it reads, the Rng and the change of the campaign (a recorded one that still leads to C#'s
    ///   stays; CampaignDelta writes it otherwise, as campaignDelta did).
    /// </summary>
    public class CampaignRecorder
    {
        private readonly ITestOutputHelper output;

        public CampaignRecorder(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static JToken J(object? value) => value == null ? JValue.CreateNull() : JToken.Parse(CampaignJsonTests.Write(value));

        private static JsonNode? Node(JToken token) => Recording.NodeOfText(token.ToString(Newtonsoft.Json.Formatting.None));

        private static bool Same(JToken? a, JToken? b)
        {
            var na = a == null || a.Type == JTokenType.Null;
            var nb = b == null || b.Type == JTokenType.Null;
            if (na || nb) return na && nb;
            return CampaignJsonTests.FirstDifference(CampaignJsonTests.Clean(a!), CampaignJsonTests.Clean(b!)) == null;
        }

        [RecorderFact]
        public void Campaign()
        {
            Tables();
            Reads();
            foreach (var path in Directory.GetFiles(Recording.PathOf(Path.Combine("campaign", "games")), "*.json").OrderBy((p) => p, StringComparer.Ordinal)) Game($"campaign/games/{Path.GetFileName(path)}");
        }

        private void Tables()
        {
            const string file = "campaign/tables.json";
            var (text, raw) = Recording.Load(file);
            var fresh = Node(new JObject
            {
                ["regions"] = J(Engine.Campaign.Regions),
                ["roads"] = J(Engine.Campaign.Roads),
                ["facilities"] = J(Engine.Campaign.Facilities),
                ["constants"] = J(CampaignJsonTests.Constants()),
                ["ids"] = new JObject
                {
                    ["regions"] = J(RegionIds.All),
                    ["facilities"] = J(Engine.Campaign.Facilities.Keys),
                    ["events"] = J(CampaignEventIds.All),
                    ["quests"] = J(QuestIds.All),
                    ["traits"] = J(TraitIds.All),
                    ["baseViews"] = J(BaseViews.All),
                },
            });
            output.WriteLine(Recording.Report(file, Recording.Write(file, text, raw, Recording.Node(raw), fresh)));
        }

        private void Reads()
        {
            const string file = "campaign/reads.json";
            var (text, raw) = Recording.Load(file);
            var recorded = JObject.Parse(text);
            var fresh = new JObject
            {
                ["fixed"] = CampaignReadsTests.Fixed((JObject)recorded["fixed"]!),
                ["states"] = new JArray(((JArray)recorded["states"]!).Select((s) => new JObject
                {
                    ["source"] = s["source"]!.DeepClone(),
                    ["reads"] = CampaignReadsTests.Reads(CampaignReadsTests.Source((string)s["source"]!)),
                })),
            };
            output.WriteLine(Recording.Report(file, Recording.Write(file, text, raw, Recording.Node(raw), Node(fresh))));
        }

        // ------------------------------------------------------------ the games

        /// <summary>CampaignResult as TypeScript writes it: `{ ok: true, launch? }` or `{ ok: false, error }`.</summary>
        private static JObject ResultOf(CampaignResult r)
        {
            var o = new JObject { ["ok"] = r.Ok };
            if (!r.Ok) o["error"] = r.Error;
            if (r.Launch) o["launch"] = true;
            return o;
        }

        private static JArray RngOf(Rng rng) => new JArray(rng.State);

        private void Game(string file)
        {
            var (text, raw) = Recording.Load(file);
            var game = JObject.Parse(text);
            var norm = (JObject)game.DeepClone();
            var fresh = (JObject)game.DeepClone();
            var rng = new Rng((int)game["seed"]!);
            var start = (JObject)game["start"]!;
            var c = start["create"] is JArray players
                ? Engine.Campaign.CreateCampaign(players.Select((p) => (int)p).ToList(), rng)
                : game["campaign"]!.ToObject<CampaignState>(CampaignJsonTests.Serializer)!;
            fresh["campaign"] = JObject.Parse(CampaignJsonTests.Write(c));
            fresh["rng"] = RngOf(rng);
            var prev = (JObject)fresh["campaign"]!;
            var both = new[] { 0, 1 };
            var ops = (JArray)game["ops"]!;
            for (var n = 0; n < ops.Count; n++)
            {
                var op = (JObject)ops[n];
                var freshOp = (JObject)fresh["ops"]![n]!;
                var normOp = (JObject)norm["ops"]![n]!;
                switch ((string)op["op"]!)
                {
                    case "cmd":
                    {
                        var cmd = op["cmd"]!.ToObject<CampaignCommand>(CampaignJsonTests.Serializer)!;
                        var slot = (int)op["slot"]!;
                        var connected = op["connected"]!.Select((x) => (int)x).ToList();
                        var pending = c.Proposal;
                        var r = Engine.Campaign.ExecuteCampaignCommand(c, cmd, new CampaignContext { Slot = slot, Connected = connected, Rng = rng });
                        freshOp["result"] = ResultOf(r);
                        // As CampaignGamesTests: a recorded cancel that C# has just proposed, answered yes by the partner at once.
                        if (cmd is CancelMissionCommand && r.Ok && pending == null && c.Proposal?.Action is CancelMissionAction)
                        {
                            var yes = Engine.Campaign.ExecuteCampaignCommand(c, new VoteCommand { Accept = true }, new CampaignContext { Slot = connected.First((s) => s != slot), Connected = connected, Rng = rng });
                            if (!yes.Ok) throw new InvalidOperationException($"{file}, operation {n}: the partner's yes: {yes.Error}");
                        }
                        break;
                    }
                    case "opening":
                        Engine.Campaign.OpeningMission(c);
                        break;
                    case "migrate":
                        c = Engine.Campaign.MigrateCampaign(c);
                        break;
                    case "addPlayer":
                    {
                        freshOp["got"] = Engine.Campaign.AddPlayer(c, (int)op["slot"]!, rng);
                        if (c.ActiveMission != null && Engine.Campaign.ActiveOffer(c)?.Siege != true)
                        {
                            c.Squad = Engine.Campaign.DefaultSquad(c, both);
                            Engine.Campaign.SyncPads(c);
                        }
                        break;
                    }
                    case "defaultSquad":
                        c.Squad = Engine.Campaign.DefaultSquad(c, op["connected"]!.Select((x) => (int)x).ToList());
                        Engine.Campaign.SyncPads(c);
                        break;
                    case "look":
                    {
                        var offer = Engine.Campaign.ActiveOffer(c);
                        var reads = new JObject { ["next"] = new JArray(J(Engine.Campaign.NextSteps(c, 0, "Ana")), J(Engine.Campaign.NextSteps(c, 1))) };
                        if (offer != null)
                        {
                            reads["squad"] = J(Engine.Campaign.BuildSquad(c));
                            reads["info"] = J(Engine.Campaign.MissionInfo(c, offer));
                            reads["briefing"] = Engine.Campaign.OfferBriefing(c, offer);
                            reads["signature"] = Engine.Campaign.OfferSignature(c, offer);
                        }
                        freshOp["reads"] = reads;
                        break;
                    }
                    case "mission":
                        Engine.Campaign.ApplyMissionResult(c, op["mission"]!.ToObject<GameState>(CampaignJsonTests.Serializer)!, rng);
                        break;
                    case "advanceTime":
                        Engine.Campaign.AdvanceTime(c, rng);
                        break;
                    case "end":
                        freshOp["reads"] = new JObject
                        {
                            ["title"] = J(c.Ending != null ? Lore.EndingTitles[c.Ending] : null),
                            ["epilogue"] = Lore.Epilogue(c.Outcome!, c.Ending, c.Doom >= Engine.Campaign.DoomMax),
                        };
                        break;
                    default:
                        throw new InvalidOperationException($"{file}, operation {n}: unknown");
                }
                freshOp["rng"] = RngOf(rng);
                var now = JObject.Parse(CampaignJsonTests.Write(c));
                var delta = CampaignDelta(prev, now);
                // The recorded change, made to the campaign before it, gives C#'s: it stays. If not, C#'s goes in whole.
                normOp["delta"] = Leads(prev, (JObject)op["delta"]!, now) ? delta.DeepClone() : "$replaced";
                freshOp["delta"] = delta;
                prev = now;
            }
            output.WriteLine(Recording.Report(file, Recording.Write(file, text, raw, Node(norm), Node(fresh))));
        }

        /// <summary>Whether a recorded change, made to the campaign `prev` (as JSON), gives `now`.</summary>
        private static bool Leads(JObject prev, JObject delta, JObject now)
        {
            var before = (JObject)prev.DeepClone();
            try
            {
                CampaignGamesTests.ApplyDelta(before, delta);
            }
            catch (Exception)
            {
                return false;
            }
            return CampaignGamesTests.Differs(before, now) == null;
        }

        /// <summary>
        /// record-campaign.ts campaignDelta: how `now` differs from `prev`, key by key. The log, what came in front
        /// (`$unshift`, with its length); an array of the same length, the items that changed (`$items`); the rest, whole
        /// (null: gone).
        /// </summary>
        public static JObject CampaignDelta(JObject prev, JObject now)
        {
            var d = new JObject();
            var keys = prev.Properties().Select((p) => p.Name).ToList();
            foreach (var p in now.Properties()) if (!keys.Contains(p.Name)) keys.Add(p.Name);
            foreach (var k in keys)
            {
                var a = prev[k];
                var b = now[k];
                if (Same(a, b)) continue;
                if (k == "log" && a is JArray was && b is JArray are)
                {
                    // What came in front, the old ones pushed back (and the last ones gone past 40).
                    var n = 0;
                    while (n <= are.Count && !SameItems(are.Skip(n).ToList(), was.Take(are.Count - n).ToList())) n++;
                    if (n <= are.Count)
                    {
                        d["log"] = new JObject { ["$unshift"] = new JArray(are.Take(n).Select((x) => x.DeepClone())), ["length"] = are.Count };
                        continue;
                    }
                }
                if (a is JArray xs && b is JArray ys && xs.Count == ys.Count)
                {
                    var items = new JObject();
                    for (var i = 0; i < ys.Count; i++) if (!Same(xs[i], ys[i])) items[i.ToString()] = ys[i].DeepClone();
                    d[k] = new JObject { ["$items"] = items };
                    continue;
                }
                d[k] = b == null ? JValue.CreateNull() : b.DeepClone();
            }
            return d;
        }

        private static bool SameItems(List<JToken> a, List<JToken> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++) if (!Same(a[i], b[i])) return false;
            return true;
        }
    }
}
