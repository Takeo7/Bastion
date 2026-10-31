using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Bastion.Net;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// golden/net recorded with C# (record-launch.ts was TypeScript's): launch.json, each campaign's mission launched again
    /// as the host does (EngineCampaignRules.LaunchMission) with its seed, and the Rng after (NetEngineCampaignTests).
    /// mision-ciudad.json is a state for the network's JSON to read and write (NetJsonTests), nothing a rule says: it stays.
    /// </summary>
    public class NetRecorder
    {
        private readonly ITestOutputHelper output;

        public NetRecorder(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static JToken CampaignOf(string source)
        {
            if (source.StartsWith("states.json#"))
            {
                var name = source.Substring("states.json#".Length);
                var states = (JArray)JObject.Parse(File.ReadAllText(Recording.PathOf(Path.Combine("campaign", "states.json"))))["states"]!;
                return states.Single((s) => (string)s["name"]! == name)["campaign"]!;
            }
            return JObject.Parse(File.ReadAllText(Recording.PathOf(Path.Combine("campaign", source))))["campaign"]!;
        }

        [RecorderFact]
        public void Launch()
        {
            const string file = "net/launch.json";
            var (text, raw) = Recording.Load(file);
            var fresh = JObject.Parse(text);
            foreach (var m in (JArray)fresh["missions"]!)
            {
                var c = CampaignOf((string)m["source"]!).ToObject<CampaignState>(Json.Serializer)!;
                var rng = new Rng((int)m["seed"]!);
                var made = new EngineCampaignRules().LaunchMission(c, rng);
                m["state"] = made == null ? JValue.CreateNull() : JObject.Parse(Json.Write(made.Value.state));
                m["rng"] = new JArray(rng.State);
            }
            output.WriteLine(Recording.Report(file, Recording.Write(file, text, raw, Recording.Node(raw), JsonNode.Parse(fresh.ToString(Newtonsoft.Json.Formatting.None)))));
        }
    }
}
