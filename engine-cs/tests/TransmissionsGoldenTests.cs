using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bastion.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// transmissions.ts's scenes on campaigns, against what TypeScript says (golden/lore/scenes.json, written by
    /// golden/make-lore-tests.ts): the states recorded in golden/campaign, UI's sample, and states made from them by a
    /// patch to reach every scene's condition. The scenes' texts are LoreGoldenTests'.
    /// </summary>
    public class TransmissionsGoldenTests
    {
        private static readonly string Golden = Path.Combine(AppContext.BaseDirectory, "golden");

        /// <summary>The network's JSON, with the campaign's two unions (as CampaignJsonTests reads them).</summary>
        private static readonly JsonSerializer Serializer = MakeSerializer();

        private static JsonSerializer MakeSerializer()
        {
            var net = Json.Settings;
            var settings = new JsonSerializerSettings
            {
                ContractResolver = net.ContractResolver,
                NullValueHandling = net.NullValueHandling,
                MissingMemberHandling = net.MissingMemberHandling,
                TypeNameHandling = net.TypeNameHandling,
                MaxDepth = net.MaxDepth,
            };
            foreach (var c in net.Converters) settings.Converters.Add(c);
            settings.Converters.Add(new TagConverter<ProposalAction>("type", (a) => a.Type));
            settings.Converters.Add(new TagConverter<CampaignCommand>("type", (c) => c.Type));
            return JsonSerializer.Create(settings);
        }

        private static JObject Read(params string[] path) => JObject.Parse(File.ReadAllText(Path.Combine(new[] { Golden }.Concat(path).ToArray())));

        /// <summary>A case's campaign: its base, with each `[path, value]` of the patch set (or, with a third `true`, added to the list).</summary>
        private static CampaignState Campaign(JObject recorded, JObject sample, JToken k)
        {
            var name = (string)k["base"]!;
            var state = (name == "muestra" ? sample["campaign"]! : ((JArray)recorded["states"]!).First((s) => (string)s["name"]! == name)["campaign"]!).DeepClone();
            foreach (var p in (JArray)k["patch"]!)
            {
                var keys = ((string)p[0]!).Split('.');
                var at = state;
                foreach (var key in keys.Take(keys.Length - 1)) at = Step(at, key);
                var last = keys[keys.Length - 1];
                if (p.Count() > 2 && (bool)p[2]!) ((JArray)Step(at, last)).Add(p[1]!.DeepClone());
                else if (at is JArray list) list[int.Parse(last)] = p[1]!.DeepClone();
                else at[last] = p[1]!.DeepClone();
            }
            return state.ToObject<CampaignState>(Serializer)!;
        }

        private static JToken Step(JToken at, string key) => at is JArray list ? list[int.Parse(key)] : at[key]!;

        [Fact]
        public void TheScenesOnEveryCampaign()
        {
            var recorded = Read("campaign", "states.json");
            var sample = Read("campaign", "muestra.json");
            var cases = (JArray)Read("lore", "scenes.json")["cases"]!;
            Assert.True(cases.Count > 40);
            foreach (var k in cases)
            {
                var label = (string)k["name"]!;
                var c = Campaign(recorded, sample, k);
                Assert.True(k["unlocked"]!.Select((x) => (string)x!).SequenceEqual(Transmissions.UnlockedScenes(c).Select((s) => s.Id)), $"{label}: unlocked");
                foreach (var n in (JArray)k["next"]!)
                {
                    var seen = n["seen"]!.Select((x) => (string)x!).ToList();
                    var moment = (bool)n["moment"]!;
                    Assert.True((string?)n["scene"] == Transmissions.NextScene(c, seen, moment)?.Id, $"{label}: next after [{string.Join(", ", seen)}], moment {moment}");
                }
                foreach (var p in ((JObject)k["lines"]!).Properties())
                {
                    var scene = Transmissions.Scenes.First((s) => s.Id == p.Name);
                    var expected = ((JArray)p.Value).Select((l) => $"{(string)l[0]!}: {(string)l[1]!}");
                    Assert.Equal(expected, Transmissions.SceneLines(scene, c).Select((l) => $"{l.Who}: {l.Text}"));
                }
            }
        }
    }
}
