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
    /// The campaigns saved while playing the 0.7.0 to the 0.10.x (golden/campaign/saves/, versions 20 to 25) and UI's
    /// sample (muestra.json), in the room's save (Net/SaveStore.cs) as the TypeScript host wrote it: read, written and
    /// read again, they say the same, nulls apart (engine-cs README); and the room goes on with them.
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

        /// <summary>
        /// A campaign C# wrote, made comparable with TypeScript's: UI's sample was written by hand, and its Taller's work
        /// lacks `plot`, `kind` and `total`, which C# writes with their defaults (CampaignJsonTests). Those go, once seen
        /// to be the defaults; nothing else.
        /// </summary>
        private static JToken Comparable(JToken csharp, JToken typescript)
        {
            var back = csharp.DeepClone();
            var works = (JArray?)back["base"]?["works"];
            var were = (JArray?)typescript["base"]?["works"];
            for (int i = 0; works != null && were != null && i < works.Count; i++)
                foreach (var (field, empty) in new[] { ("plot", (JToken)""), ("kind", ConstructionKinds.Craft), ("total", 0) })
                {
                    if (were[i][field] != null) continue;
                    Assert.True(JToken.DeepEquals(works[i][field], empty), $"works[{i}].{field}: {works[i][field]}");
                    ((JObject)works[i]).Remove(field);
                }
            return back;
        }

        /// <summary>The save C# wrote, made comparable with TypeScript's (Comparable).</summary>
        private JObject Written(JObject typescript)
        {
            var back = JObject.Parse(File.ReadAllText(SavePath));
            back["campaign"] = Comparable(back["campaign"]!, typescript["campaign"]!);
            return back;
        }

        [Theory]
        [MemberData(nameof(Saved))]
        public void ASavedCampaignIsReadAndWrittenTheSame(string name)
        {
            var typescript = SaveOf(CampaignOf(name));
            Put(typescript);
            var store = new SaveStore(SavePath);
            var save = store.Load();
            Assert.NotNull(save);
            Assert.Equal(("base", "campaign"), (save!.Phase, save.Mode));
            Assert.Equal((int)typescript["campaign"]!["version"]!, save.Campaign!.Version);
            Assert.Equal((int)typescript["campaign"]!["day"]!, save.Campaign.Day);
            Assert.Null(store.SetAside);
            store.Save(save);
            var difference = Json2.FirstDifference(Json2.Clean(typescript), Json2.Clean(Written(typescript)));
            Assert.True(difference == null, $"{name}: {difference}");
            // And again: what C# wrote, C# reads and writes to the letter.
            var first = File.ReadAllText(SavePath);
            store.Save(new SaveStore(SavePath).Load()!);
            Assert.Equal(first, File.ReadAllText(SavePath));
        }

        [Theory]
        [MemberData(nameof(Saved))]
        public void AnOlderOneIsBroughtUpToDateWithTheCampaignsRules(string name)
        {
            var version = (int)CampaignOf(name)["version"]!;
            Put(SaveOf(CampaignOf(name)));
            var rules = new FakeCampaignRules();
            var save = new SaveStore(SavePath, null, rules).Load()!;
            // From the 0.7 on, none is put aside; only those before the 25 go through migrateCampaign.
            Assert.Equal(version < Campaign.CampaignVersion ? 1 : 0, rules.Migrated);
            Assert.Equal(Campaign.CampaignVersion, save.Campaign!.Version);
        }

        [Fact]
        public void ACampaignThatCantBeBroughtUpToDateIsKeptAsideAndTheHostGoesOn()
        {
            // persistence.ts migrates within its try: whatever fails, the save is one that can't be read, never written over.
            Put(SaveOf(CampaignOf("saves/v24.json")));
            var log = new List<string>();
            var rules = new FakeCampaignRules { MigrationFails = true };
            var room = new Room(new SaveStore(SavePath, log.Add, rules), new FakeRules(), "1.0.0", null, new FakeClock(), new Rng(5), null, rules);
            Assert.Equal("lobby", room.Summary().phase);
            Assert.True(File.Exists(Path.Combine(temp, "save.ilegible.json")));
            Assert.Equal(24, (int)JObject.Parse(File.ReadAllText(Path.Combine(temp, "save.ilegible.json")))["campaign"]!["version"]!);
            Assert.Contains(log, (l) => l.Contains("no se pudo poner al día su campaña (KeyNotFoundException: una pieza que ya no existe)"));
        }

        [Theory]
        [MemberData(nameof(Saved))]
        public void TheRoomGoesOnWithIt(string name)
        {
            var typescript = SaveOf(CampaignOf(name));
            Put(typescript);
            var log = new List<string>();
            var rules = new FakeCampaignRules();
            var room = new Room(new SaveStore(SavePath, log.Add, rules), new FakeRules(), "1.0.0", null, new FakeClock(), new Rng(5), null, rules);
            Assert.Equal("base", room.Summary().phase);
            var (ana, host) = MemoryEnd.Pair("jugador", "10.0.0.1:5000");
            room.Connect(host);
            room.Pump();
            // Her seat by its token, and the campaign as it was saved (brought up to version 25).
            ana.Send("{\"t\":\"hello\",\"name\":\"Ana\",\"token\":\"ficha-0\",\"version\":\"1.0.0\"}");
            room.Pump();
            Assert.Equal(0, (int)ana.Take("welcome").Single()["slot"]!);
            var expected = (JObject)typescript["campaign"]!.DeepClone();
            expected["version"] = Campaign.CampaignVersion;
            var sent = ana.Take("campaign").Last()["state"]!;
            var difference = Json2.FirstDifference(Json2.Clean(expected), Json2.Clean(Comparable(sent, expected)));
            Assert.True(difference == null, $"{name}: {difference}");
            var saved = ana.Take("room").Last()["savedCampaign"]!;
            Assert.Equal((int)expected["day"]!, (int)saved["day"]!);
            Assert.Equal(Progression.BastionLevel((int?)expected["level"]), (int)saved["level"]!);
            Assert.DoesNotContain(log, (l) => l.Contains("Fallo"));
        }

        [Fact]
        public void AProposalWaitingIsReadAsItsActionAndItsClockStartsWhenSomeoneIsBack()
        {
            // Saved by the 0.9.3 with Ana's proposal waiting for Pepe: to buy grenades.
            Put(SaveOf(CampaignOf("saves/v25-093.json")));
            var save = new SaveStore(SavePath).Load()!;
            Assert.Equal("fragGrenade", Assert.IsType<BuyAction>(save.Campaign!.Proposal!.Action).Item);
            Assert.Contains("\"action\":{\"type\":\"buy\",\"item\":\"fragGrenade\"}", Json.Write(save.Campaign.Proposal));
            var rules = new FakeCampaignRules();
            var room = new Room(new SaveStore(SavePath, null, rules), new FakeRules(), "1.0.0", null, new FakeClock(), new Rng(5), null, rules);
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
            var typescript = SaveOf(CampaignOf("saves/v25.json"));
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
