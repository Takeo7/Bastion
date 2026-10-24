using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bastion.Engine;
using Xunit;
using Xunit.Sdk;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Step 3 against the TypeScript engine (docs/UNITY.md §3): golden/step3 holds missions made by createMission (setup.ts)
    /// over every biome, kind of mission, scenario, difficulty, night and day, veterans, seats, hand-made map and a base's
    /// siege, and maps made by generateLayout, handmadeLayout and baseLayout alone (record-step3.ts). Setup.CreateMission and
    /// the layouts, with the same options, have to give the same JSON: read through the C# classes (null and missing
    /// alike), nothing of the recorded lost on the way, numbers to the bit. The states the step-1 and step-2 golden files
    /// start from were made by createMission too: they are made again here.
    /// </summary>
    public class Step3GoldenTests
    {
        public static IEnumerable<object[]> Groups => new[] { "city", "town", "wilds", "facility", "handmade", "base", "campaign" }.Select((g) => new object[] { g });

        [Theory]
        [MemberData(nameof(Groups))]
        public void CreateMissionMakesWhatTypeScriptMade(string group)
        {
            var failures = new List<string>();
            var count = 0;
            foreach (var c in Step3Json.File(group).GetProperty("cases").EnumerateArray())
            {
                count++;
                var name = c.GetProperty("name").GetString();
                var opts = Read<MissionOptions>(c.GetProperty("options"));
                opts.Rng = new Rng(c.GetProperty("rngSeed").GetInt32());
                var diff = Step3Json.Check(c.GetProperty("state"), () => Setup.CreateMission(opts));
                if (diff != null) failures.Add($"{name}: {diff}");
            }
            Step3Json.Assert(group, count, failures);
        }

        [Fact]
        public void LayoutsMakeWhatTypeScriptMade()
        {
            var failures = new List<string>();
            var count = 0;
            foreach (var c in Step3Json.File("layouts").GetProperty("layouts").EnumerateArray())
            {
                count++;
                var name = c.GetProperty("name").GetString();
                Func<MissionLayout> make;
                switch (c.GetProperty("api").GetString())
                {
                    case "generateLayout":
                        var opts = Read<GenOptions>(c.GetProperty("opts"));
                        make = () => Mapgen.GenerateLayout(opts);
                        break;
                    case "handmadeLayout":
                        var def = Maps.All[c.GetProperty("id").GetString()!];
                        var kind = c.GetProperty("kind").GetString()!;
                        make = () => Mapgen.HandmadeLayout(def, kind);
                        break;
                    default:
                        var @base = Read<BaseState>(c.GetProperty("base"));
                        make = () => Base.BaseLayout(@base);
                        break;
                }
                var diff = Step3Json.Check(c.GetProperty("layout"), make);
                if (diff != null) failures.Add($"{name}: {diff}");
            }
            Step3Json.Assert("layouts", count, failures);
        }

        /// <summary>record-step1.ts's missions: `createMission({ map, slots: [0, 1], rng: new Rng(seed), night })`.</summary>
        [Fact]
        public void CreateMissionMakesTheStep1Missions()
        {
            var missions = new (string Name, MapSource Map, int Seed, bool Night)[]
            {
                ("city-1", MapSource.Generated(Biomes.City, 1), 1, false),
                ("city-7", MapSource.Generated(Biomes.City, 7), 7, false),
                ("wilds-1", MapSource.Generated(Biomes.Wilds, 1), 1, false),
                ("wilds-7", MapSource.Generated(Biomes.Wilds, 7), 7, false),
                ("facility-1", MapSource.Generated(Biomes.Facility, 1), 1, false),
                ("facility-7", MapSource.Generated(Biomes.Facility, 7), 7, false),
                ("torre", MapSource.Handmade("torre"), 3, false),
                ("city-5-night", MapSource.Generated(Biomes.City, 5), 5, true),
                ("wilds-3-night", MapSource.Generated(Biomes.Wilds, 3), 3, true),
            };
            var failures = new List<string>();
            foreach (var m in missions)
            {
                var diff = Step3Json.Check(Golden(m.Name).GetProperty("state"), () => Setup.CreateMission(new MissionOptions { Map = m.Map, Slots = new List<int> { 0, 1 }, Rng = new Rng(m.Seed), Night = m.Night }));
                if (diff != null) failures.Add($"{m.Name}: {diff}");
            }
            Step3Json.Assert("step1", missions.Length, failures);
        }

        /// <summary>record-step2.ts's missions (not its arenas): `createMission({ ...options, slots: [0, 1], rng: new Rng(seed) })`.</summary>
        [Fact]
        public void CreateMissionMakesTheStep2Missions()
        {
            var failures = new List<string>();
            var count = 0;
            foreach (var path in Directory.GetFiles(Path.Combine("golden", "step2"), "*.json").OrderBy((p) => p, StringComparer.Ordinal))
            {
                var root = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
                var options = root.GetProperty("options");
                if (options.ValueKind != JsonValueKind.Object) continue;
                count++;
                var opts = Read<MissionOptions>(options);
                opts.Slots = new List<int> { 0, 1 };
                opts.Rng = new Rng(root.GetProperty("seed").GetInt32());
                var diff = Step3Json.Check(root.GetProperty("state"), () => Setup.CreateMission(opts));
                if (diff != null) failures.Add($"{Path.GetFileNameWithoutExtension(path)}: {diff}");
            }
            Step3Json.Assert("step2", count, failures);
        }
    }

    /// <summary>How step 3's golden files are read and compared.</summary>
    public static class Step3Json
    {
        private static readonly Dictionary<string, JsonElement> Files = new Dictionary<string, JsonElement>();

        /// <summary>A file of golden/step3, parsed once.</summary>
        public static JsonElement File(string name)
        {
            lock (Files)
            {
                if (!Files.TryGetValue(name, out var root))
                {
                    root = JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine("golden", "step3", name + ".json"))).RootElement;
                    Files[name] = root;
                }
                return root;
            }
        }

        /// <summary>
        /// What C# makes against what TypeScript recorded, or null when they are the same: the recorded read through C#'s
        /// class (what TypeScript leaves out reads as C# has it), none of its fields lost on the way, then the first JSON
        /// path that differs.
        /// </summary>
        public static string? Check<T>(JsonElement recorded, Func<T> make)
        {
            var normal = Step2Json.Element(Read<T>(recorded));
            var lost = Lost(recorded, normal, "");
            if (lost != null) return $"the recorded has a field C# doesn't read: {lost}";
            T actual;
            try
            {
                actual = make();
            }
            catch (Exception e)
            {
                return $"throws {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
            }
            return Step2Json.Diff(normal, Step2Json.Element(actual), "");
        }

        /// <summary>The first field of `raw` (not null) that `read` lacks.</summary>
        private static string? Lost(JsonElement raw, JsonElement read, string path)
        {
            if (raw.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in raw.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.Null) continue;
                    if (read.ValueKind != JsonValueKind.Object || !read.TryGetProperty(p.Name, out var r)) return $"{path}.{p.Name}";
                    var deeper = Lost(p.Value, r, $"{path}.{p.Name}");
                    if (deeper != null) return deeper;
                }
            }
            else if (raw.ValueKind == JsonValueKind.Array && read.ValueKind == JsonValueKind.Array)
            {
                var a = raw.EnumerateArray().ToList();
                var b = read.EnumerateArray().ToList();
                for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
                {
                    var deeper = Lost(a[i], b[i], $"{path}[{i}]");
                    if (deeper != null) return deeper;
                }
            }
            return null;
        }

        public static void Assert(string what, int count, List<string> failures)
        {
            if (count == 0) throw new XunitException($"{what}: nothing was checked.");
            if (failures.Count > 0) throw new XunitException($"{what}: {failures.Count} of {count} differ.\n  " + string.Join("\n  ", failures.Take(12)));
        }
    }
}
