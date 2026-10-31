using System.Linq;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// golden/lore/scenes.json recorded with C# (make-lore-tests.ts was TypeScript's; TransmissionsGoldenTests reads it):
    /// each case's campaign (its base and patch), the scenes it unlocks, the next scene for each `seen` and `moment`, the
    /// lines of the scenes it names and the recap for each seat it names, as Transmissions says them. Written whole, as
    /// JSON.stringify(…, null, 1). LoreGoldenTests.cs, the texts of lore.ts, is C# that make-lore-tests.ts wrote: it stays.
    /// </summary>
    public class LoreRecorder
    {
        private readonly ITestOutputHelper output;

        public LoreRecorder(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static JsonArray Lines(System.Collections.Generic.IEnumerable<(string Who, string Text)> lines)
        {
            return new JsonArray(lines.Select((l) => (JsonNode)new JsonArray(l.Who, l.Text)).ToArray());
        }

        [RecorderFact]
        public void Scenes()
        {
            const string file = "lore/scenes.json";
            var (text, _) = Recording.Load(file);
            var recorded = TransmissionsGoldenTests.Read("campaign", "states.json");
            var sample = TransmissionsGoldenTests.Read("campaign", "muestra.json");
            var cases = new JsonArray();
            foreach (var k in (JArray)JObject.Parse(text)["cases"]!)
            {
                var c = TransmissionsGoldenTests.Campaign(recorded, sample, k);
                var lines = new JsonObject();
                foreach (var p in ((JObject)k["lines"]!).Properties())
                {
                    var scene = Transmissions.Scenes.First((s) => s.Id == p.Name);
                    lines[p.Name] = Lines(Transmissions.SceneLines(scene, c).Select((l) => (l.Who, l.Text)));
                }
                var recap = new JsonObject();
                foreach (var r in ((JObject)k["recap"]!).Properties()) recap[r.Name] = Lines(Transmissions.RecapLines(c, int.Parse(r.Name)).Select((l) => (l.Who, l.Text)));
                cases.Add(new JsonObject
                {
                    ["name"] = (string)k["name"]!,
                    ["base"] = (string)k["base"]!,
                    ["patch"] = JsonNode.Parse(k["patch"]!.ToString(Newtonsoft.Json.Formatting.None)),
                    ["unlocked"] = new JsonArray(Transmissions.UnlockedScenes(c).Select((s) => (JsonNode)s.Id).ToArray()),
                    ["next"] = new JsonArray(((JArray)k["next"]!).Select((n) =>
                    {
                        var seen = n["seen"]!.Select((x) => (string)x!).ToList();
                        var moment = (bool)n["moment"]!;
                        return (JsonNode)new JsonObject
                        {
                            ["seen"] = new JsonArray(seen.Select((x) => (JsonNode)x).ToArray()),
                            ["moment"] = moment,
                            ["scene"] = Transmissions.NextScene(c, seen, moment)?.Id,
                        };
                    }).ToArray()),
                    ["lines"] = lines,
                    ["recap"] = recap,
                });
            }
            var changed = Recording.WritePretty(file, text, new JsonObject { ["cases"] = cases });
            output.WriteLine(changed ? $"{file}: cambia" : $"{file}: igual");
        }
    }
}
