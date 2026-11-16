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
    /// The room's save (Net/SaveStore.cs) with a campaign. Those saved while playing the 0.7.0 to the 0.10.x
    /// (golden/campaign/saves/, versions 20 to 25) and UI's sample (muestra.json), as the TypeScript host wrote them, don't
    /// go on into the runs (Arturo, 16/11): each is kept aside as it is, never brought up to date, and the room starts in
    /// the lobby. One of runs, read, written and read again, says the same, and the room goes on with it. Bringing one up
    /// to date (MigrateCampaign, which failing kept the save aside) has nothing to do till the runs' campaign changes
    /// shape: its test comes back then.
    /// </summary>
    public sealed class NetCampaignSaveTests : IDisposable
    {
        private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "golden", "campaign");

        private readonly string temp = Path.Combine(Path.GetTempPath(), "bastion-guardadas-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(temp, true);
            }
            catch (Exception)
            {
                /* a temporary folder */
            }
        }

        private string SavePath => Path.Combine(temp, "save.json");

        public static IEnumerable<object[]> Saved() => new[]
        {
            "saves/v20.json", "saves/v20-asedio-ganado.json", "saves/v21.json", "saves/v22.json", "saves/v23-migrada.json", "saves/v23-derrota.json",
            "saves/v24.json", "saves/v25-093.json", "saves/v25.json", "muestra.json",
        }.Select((n) => new object[] { n });

        private static JObject CampaignOf(string name) => (JObject)JObject.Parse(File.ReadAllText(Path.Combine(Dir, name)))["campaign"]!;

        /// <summary>A save as the 0.10.1's host writes it (persistence.ts SaveFile), with `campaign`: on the base, both seats taken.</summary>
        private static JObject SaveOf(JObject? campaign, string phase = "base", string mode = "campaign")
        {
            return new JObject
            {
                ["version"] = 6,
                ["seats"] = new JArray(Seat(0, "Ana"), Seat(1, "Pepe")),
                ["phase"] = phase,
                ["mode"] = mode,
                ["state"] = JValue.CreateNull(),
                ["campaign"] = campaign ?? (JToken)JValue.CreateNull(),
                ["resultApplied"] = false,
                ["rng"] = new JArray(2654435769, 1013904223, 1664525, 22695477),
                ["missionKind"] = "elimination",
                ["skirmishMap"] = "city",
                ["skirmishVeterans"] = true,
            };
        }

        private static JObject Seat(int slot, string name) => new JObject { ["slot"] = slot, ["name"] = name, ["token"] = $"ficha-{slot}" };

        private void Put(JObject save)
        {
            Directory.CreateDirectory(temp);
            File.WriteAllText(SavePath, save.ToString(Formatting.None));
        }

        [Theory]
        [MemberData(nameof(Saved))]
        public void ACampaignOfBeforeTheRunsIsKeptAsideAsItIs(string name)
        {
            Put(SaveOf(CampaignOf(name)));
            var before = File.ReadAllText(SavePath);
            var log = new List<string>();
            var rules = new FakeCampaignRules();
            var room = new Room(new SaveStore(SavePath, log.Add, rules), new FakeRules(), "1.0.0", null, new FakeClock(), new Rng(5), null, rules);
            Assert.Equal("lobby", room.Summary().phase);
            Assert.Equal(before, File.ReadAllText(Path.Combine(temp, $"save.{SaveStore.OutdatedLabel}.json")));
            Assert.Equal(0, rules.Migrated);
            Assert.Contains(log, (l) => l.StartsWith("[save] Partida de antes de las runs: guardada aparte en "));
            var (ana, host) = MemoryEnd.Pair("jugador", "10.0.0.1:5000");
            room.Connect(host);
            room.Pump();
            ana.Send("{\"t\":\"hello\",\"name\":\"Ana\",\"token\":\"ficha-0\",\"version\":\"1.0.0\"}");
            room.Pump();
            var lobby = ana.Take("room").Last();
            Assert.Equal($"save.{SaveStore.OutdatedLabel}.json", (string?)lobby["setAside"]);
            Assert.Null(lobby["savedCampaign"]);
            Assert.Empty(ana.Take("campaign"));
        }

        /// <summary>
        /// A campaign of runs as the host saves it: two players, supplies in the stash and a medicine in a pack, the run at
        /// a Trade it has just reached (its wares, the node chosen by both).
        /// </summary>
        private static CampaignState RunsCampaign()
        {
            var c = RunsHelpers.Two(3);
            RunsHelpers.InStash(c, GearKinds.Supplies, null, 0, 0, 40);
            RunsHelpers.InPack(c, RunsHelpers.Soldier(c, 1), GearKinds.Medicine, null, 0, 0, 2);
            RunsHelpers.GoTo(c, NodeKinds.Trade);
            Assert.Equal(RunPhases.Stop, c.Run!.Phase);
            Assert.NotEmpty(c.Run.Stop!.Wares!);
            return c;
        }

        private static JObject JsonOf(CampaignState c) => JObject.Parse(Json.Write(c));

        [Fact]
        public void ACampaignOfRunsIsReadAndWrittenTheSame()
        {
            var c = RunsCampaign();
            Put(SaveOf(JsonOf(c)));
            var store = new SaveStore(SavePath);
            var save = store.Load();
            Assert.NotNull(save);
            Assert.Equal(("base", "campaign"), (save!.Phase, save.Mode));
            Assert.Null(store.SetAside);
            Assert.Equal(Json.Write(c), Json.Write(save.Campaign!));
            store.Save(save);
            // And again: what C# wrote, C# reads and writes to the letter.
            var first = File.ReadAllText(SavePath);
            store.Save(new SaveStore(SavePath).Load()!);
            Assert.Equal(first, File.ReadAllText(SavePath));
        }

        [Fact]
        public void TheRoomGoesOnWithARunsCampaign()
        {
            var c = RunsCampaign();
            Put(SaveOf(JsonOf(c)));
            var log = new List<string>();
            var rules = new EngineCampaignRules();
            var room = new Room(new SaveStore(SavePath, log.Add, rules), new EngineRules(), "1.0.0", null, new FakeClock(), new Rng(5), null, rules);
            Assert.Equal("base", room.Summary().phase);
            var (ana, host) = MemoryEnd.Pair("jugador", "10.0.0.1:5000");
            room.Connect(host);
            room.Pump();
            ana.Send("{\"t\":\"hello\",\"name\":\"Ana\",\"token\":\"ficha-0\",\"version\":\"1.0.0\"}");
            room.Pump();
            Assert.Equal(0, (int)ana.Take("welcome").Single()["slot"]!);
            // Her seat by its token, and the campaign as it was saved: she has soldiers in the run, nothing to hand over.
            var difference = Json2.FirstDifference(Json2.Clean(JsonOf(c)), Json2.Clean(ana.Take("campaign").Last()["state"]!));
            Assert.True(difference == null, difference);
            // The lobby's summary (and the start screen's): chapter, the row reached, the living, the stash's supplies.
            var saved = ana.Take("room").Last()["savedCampaign"]!;
            var at = c.Run!.Nodes.Single((n) => n.Id == c.Run.At).Row;
            Assert.Equal((1, at, 4, 40, 1), ((int)saved["chapter"]!, (int?)saved["row"], (int)saved["soldiers"]!, (int)saved["supplies"]!, (int)saved["nodesPlayed"]!));
            Assert.DoesNotContain(log, (l) => l.Contains("Fallo") || l.Contains("aparte"));
        }

        [Fact]
        public void AProposalWaitingIsReadAsItsActionAndItsClockStartsWhenSomeoneIsBack()
        {
            // Ana's proposal to go out waiting for Pepe, as the host saved it.
            var c = RunsHelpers.Two(3);
            RunsHelpers.Ok(c, new StartRunCommand(), 0, RunsHelpers.Both);
            Put(SaveOf(JsonOf(c)));
            var save = new SaveStore(SavePath).Load()!;
            Assert.IsType<StartRunAction>(save.Campaign!.Proposal!.Action);
            Assert.Contains("\"action\":{\"type\":\"startRun\"", Json.Write(save.Campaign.Proposal));
            var rules = new EngineCampaignRules();
            var room = new Room(new SaveStore(SavePath, null, rules), new EngineRules(), "1.0.0", null, new FakeClock(), new Rng(5), null, rules);
            var (pepe, host) = MemoryEnd.Pair("jugador", "10.0.0.2:5000");
            room.Connect(host);
            room.Pump();
            pepe.Send("{\"t\":\"hello\",\"name\":\"Pepe\",\"token\":\"ficha-1\",\"version\":\"1.0.0\"}");
            room.Pump();
            var deadline = pepe.Take("deadline").Last();
            Assert.Equal(("proposal", 30_000L), ((string?)deadline["kind"], (long)deadline["ms"]!));
        }

        [Fact]
        public void ASkirmishSavedByTheTypeScriptHostHasANullCampaign()
        {
            // persistence.ts writes `campaign: null` (and `state: null`) when there is none: read as missing.
            Put(SaveOf(null, "lobby", "skirmish"));
            var log = new List<string>();
            var save = new SaveStore(SavePath, log.Add).Load();
            Assert.NotNull(save);
            Assert.Null(save!.Campaign);
            Assert.Equal("Ana", save.Seats[0]!.Name);
            Assert.Empty(log);
            Assert.False(File.Exists(Path.Combine(temp, "save.ilegible.json")));
        }

        [Fact]
        public void WithoutTheCampaignsRulesTheCampaignStaysInTheSave()
        {
            // A host with skirmishes only, on a save with a campaign: it isn't played, but nor is it lost.
            var typescript = SaveOf(JsonOf(RunsCampaign()));
            Put(typescript);
            var room = new Room(new SaveStore(SavePath), new FakeRules(), "1.0.0", null, new FakeClock(), new Rng(5));
            Assert.Equal("lobby", room.Summary().phase);
            var (ana, host) = MemoryEnd.Pair("jugador", "10.0.0.1:5000");
            room.Connect(host);
            room.Pump();
            ana.Send("{\"t\":\"hello\",\"name\":\"Ana\",\"token\":\"ficha-0\",\"version\":\"1.0.0\"}");
            ana.Send("{\"t\":\"start\"}");
            room.Pump();
            Assert.Equal("mission", room.Summary().phase);
            Assert.Empty(ana.Take("campaign"));
            var back = JObject.Parse(File.ReadAllText(SavePath));
            var difference = Json2.FirstDifference(Json2.Clean(typescript["campaign"]!), Json2.Clean(back["campaign"]!));
            Assert.True(difference == null, difference);
        }

        [Fact]
        public void WithoutTheCampaignsRulesOneOfBeforeTheRunsIsKeptAsideToo()
        {
            Put(SaveOf(CampaignOf("saves/v25.json")));
            var before = File.ReadAllText(SavePath);
            var room = new Room(new SaveStore(SavePath), new FakeRules(), "1.0.0", null, new FakeClock(), new Rng(5));
            Assert.Equal("lobby", room.Summary().phase);
            Assert.Equal(before, File.ReadAllText(Path.Combine(temp, $"save.{SaveStore.OutdatedLabel}.json")));
        }
    }

    /// <summary>Comparing JSON as the saves and the recorded games are compared (engine-cs README): nulls apart, 2 and 2.0 the same.</summary>
    public static class Json2
    {
        /// <summary>Nulls taken out, recursively: missing and null are the same.</summary>
        public static JToken Clean(JToken token)
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
        public static string? FirstDifference(JToken a, JToken b, string path = "$")
        {
            if (a.Type != b.Type)
            {
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
    }
}
