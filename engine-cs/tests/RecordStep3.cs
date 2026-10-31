using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bastion.Engine;
using Xunit.Abstractions;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// golden/step3 recorded with C# (record-step3.ts was TypeScript's; Step3GoldenTests reads them): each case's mission
    /// made again by Setup.CreateMission with its options and seed, each layout by its maker.
    /// </summary>
    public class Step3Recorder
    {
        private readonly ITestOutputHelper output;

        public Step3Recorder(ITestOutputHelper output)
        {
            this.output = output;
        }

        [RecorderFact]
        public void Step3()
        {
            foreach (var group in new[] { "city", "town", "wilds", "facility", "handmade", "base", "campaign" }) Missions(group);
            Layouts();
        }

        private void Missions(string group)
        {
            var file = $"step3/{group}.json";
            var (text, raw) = Recording.Load(file);
            var norm = Recording.Node(raw)!;
            var fresh = Recording.Node(raw)!;
            var cases = raw.GetProperty("cases").EnumerateArray().ToList();
            for (var i = 0; i < cases.Count; i++)
            {
                var c = cases[i];
                var opts = Read<MissionOptions>(c.GetProperty("options"));
                opts.Rng = new Rng(c.GetProperty("rngSeed").GetInt32());
                norm["cases"]![i]!["state"] = Recording.Normal<GameState>(c.GetProperty("state"));
                fresh["cases"]![i]!["state"] = Recording.Node(Setup.CreateMission(opts));
            }
            output.WriteLine(Recording.Report(file, Recording.Write(file, text, raw, norm, fresh)));
        }

        private void Layouts()
        {
            const string file = "step3/layouts.json";
            var (text, raw) = Recording.Load(file);
            var norm = Recording.Node(raw)!;
            var fresh = Recording.Node(raw)!;
            var layouts = raw.GetProperty("layouts").EnumerateArray().ToList();
            for (var i = 0; i < layouts.Count; i++)
            {
                var c = layouts[i];
                MissionLayout made;
                switch (c.GetProperty("api").GetString())
                {
                    case "generateLayout":
                        made = Mapgen.GenerateLayout(Read<GenOptions>(c.GetProperty("opts")));
                        break;
                    case "handmadeLayout":
                        made = Mapgen.HandmadeLayout(Maps.All[c.GetProperty("id").GetString()!], c.GetProperty("kind").GetString()!);
                        break;
                    default:
                        made = Base.BaseLayout(Read<BaseState>(c.GetProperty("base")));
                        break;
                }
                norm["layouts"]![i]!["layout"] = Recording.Normal<MissionLayout>(c.GetProperty("layout"));
                fresh["layouts"]![i]!["layout"] = Recording.Node(made);
            }
            output.WriteLine(Recording.Report(file, Recording.Write(file, text, raw, norm, fresh)));
        }
    }
}
