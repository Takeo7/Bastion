using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bastion.Engine;
using Xunit.Abstractions;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// golden/step2 recorded with C# (record-step2.ts was TypeScript's; Step2GoldenTests replays them). The operations stay
    /// as they were played (what, by whom, who was connected); the mission's state is made again by Setup.CreateMission
    /// (an arena's, made by hand in TypeScript, stays), and each operation's result, events, Rng and change of the state
    /// are C#'s. A recorded change that still leads to C#'s state stays as it was (StateDelta writes it otherwise, as
    /// stateDelta did). An operation a rule now refuses records its new error, and the game goes on.
    /// </summary>
    public class Step2Recorder
    {
        private static readonly Dictionary<string, Type> EventTypes = typeof(GameEvent).Assembly.GetTypes()
            .Where((t) => !t.IsAbstract && typeof(GameEvent).IsAssignableFrom(t))
            .ToDictionary((t) => ((GameEvent)Activator.CreateInstance(t)!).T);

        private readonly ITestOutputHelper output;

        public Step2Recorder(ITestOutputHelper output)
        {
            this.output = output;
        }

        [RecorderFact]
        public void Step2()
        {
            foreach (var path in Directory.GetFiles(Recording.PathOf("step2"), "*.json").OrderBy((p) => p, StringComparer.Ordinal)) Mission($"step2/{Path.GetFileName(path)}");
        }

        private void Mission(string file)
        {
            var (text, raw) = Recording.Load(file);
            var norm = Recording.Node(raw)!.AsObject();
            var fresh = Recording.Node(raw)!.AsObject();
            var options = raw.GetProperty("options");
            if (options.ValueKind == JsonValueKind.Object)
            {
                var opts = Read<MissionOptions>(options);
                opts.Slots = new List<int> { 0, 1 };
                opts.Rng = new Rng(raw.GetProperty("seed").GetInt32());
                norm["state"] = Recording.Normal<GameState>(raw.GetProperty("state"));
                fresh["state"] = Recording.Node(Setup.CreateMission(opts));
            }
            var actual = Read<GameState>(Recording.Element(fresh["state"]));
            var rng = new Rng(raw.GetProperty("rngSeed").GetInt32());
            var prev = Step2Json.Element(actual);
            var ops = raw.GetProperty("ops").EnumerateArray().ToList();
            for (var i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                var normOp = norm["ops"]![i]!.AsObject();
                var freshOp = fresh["ops"]![i]!.AsObject();
                List<GameEvent> events;
                var connected = op.TryGetProperty("connected", out var c) ? Ints(c).ToList() : new List<int>();
                switch (op.GetProperty("op").GetString())
                {
                    case "command":
                    {
                        var result = Rules.ExecuteCommand(actual, Step2Replay.CommandOf(op.GetProperty("cmd")), op.GetProperty("slot").GetInt32(), connected, rng);
                        freshOp["ok"] = result.Ok;
                        if (result.Ok) freshOp.Remove("error");
                        else freshOp["error"] = result.Error;
                        events = result.Events ?? new List<GameEvent>();
                        break;
                    }
                    case "syncTurn":
                        events = Rules.SyncTurn(actual, connected, rng);
                        break;
                    case "runOutOfTime":
                        events = Rules.RunOutOfTime(actual, op.GetProperty("slot").GetInt32(), connected, rng);
                        break;
                    case "callAbandon":
                        events = Rules.CallAbandon(actual, op.GetProperty("slot").GetInt32(), connected);
                        break;
                    case "answerAbandon":
                        events = Rules.AnswerAbandon(actual, op.GetProperty("slot").GetInt32(), op.GetProperty("accept").GetBoolean());
                        break;
                    default:
                        throw new InvalidOperationException($"{file}, operation {i}: unknown");
                }
                normOp["events"] = new JsonArray(op.GetProperty("events").EnumerateArray().Select((e) => EventTypes.TryGetValue(e.GetProperty("t").GetString()!, out var type)
                    ? Recording.Node(JsonSerializer.Deserialize(e.GetRawText(), type, Options))
                    : Recording.Node(e)).ToArray());
                freshOp["events"] = new JsonArray(events.Select((e) => Recording.Node(e)).ToArray());
                freshOp["rng"] = new JsonArray(rng.State.Select((x) => (JsonNode)JsonValue.Create(x)).ToArray());
                var now = Step2Json.Element(actual);
                var delta = StateDelta(prev, now);
                // The recorded change, made to the state before it, gives C#'s: it stays. If not, C#'s goes in whole, its
                // nulls (gone) and all.
                normOp["delta"] = Leads(prev, op.GetProperty("delta"), actual) ? delta.DeepClone() : Recording.Replaced();
                freshOp["delta"] = delta;
                prev = now;
            }
            output.WriteLine(Recording.Report(file, Recording.Write(file, text, raw, norm, fresh)));
        }

        /// <summary>Whether a recorded change, made to the state `prev` (as JSON), gives `now`.</summary>
        private static bool Leads(JsonElement prev, JsonElement delta, GameState now)
        {
            var before = Read<GameState>(prev);
            try
            {
                Step2Json.ApplyDelta(before, delta);
            }
            catch (Exception)
            {
                return false;
            }
            return Step2Json.StateDiff(before, now) == null;
        }

        // ------------------------------------------------------------ record-step2.ts stateDelta

        /// <summary>Arrays of numbers or strings (or null) kept as changes by index: `{ $sparse: [i, v, i, v…] }` when the length stays.</summary>
        private static readonly HashSet<string> SparseKeys = new HashSet<string> { "tiles", "elev", "ground", "decor", "storeys" };

        private static JsonNode? NodeOf(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) ? Recording.Node(v) : null;

        private static JsonElement Get(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) ? v : default;

        private static bool Same(JsonElement a, JsonElement b)
        {
            if (a.ValueKind == JsonValueKind.Undefined || b.ValueKind == JsonValueKind.Undefined) return (a.ValueKind == JsonValueKind.Undefined || a.ValueKind == JsonValueKind.Null) && (b.ValueKind == JsonValueKind.Undefined || b.ValueKind == JsonValueKind.Null);
            return Recording.Same(a, b);
        }

        private static JsonNode? Sparse(JsonElement prev, JsonElement now)
        {
            if (prev.ValueKind != JsonValueKind.Array || now.ValueKind != JsonValueKind.Array || prev.GetArrayLength() != now.GetArrayLength()) return now.ValueKind == JsonValueKind.Undefined ? null : Recording.Node(now);
            var pairs = new JsonArray();
            var a = prev.EnumerateArray().ToList();
            var b = now.EnumerateArray().ToList();
            for (var i = 0; i < b.Count; i++)
            {
                if (Same(a[i], b[i]) && a[i].ValueKind == b[i].ValueKind) continue;
                pairs.Add(i);
                pairs.Add(Recording.Node(b[i]));
            }
            return new JsonObject { ["$sparse"] = pairs };
        }

        /// <summary>
        /// How `now` differs from `prev` (both states as JSON), key by key: a key that changed, whole (null: gone); the big
        /// arrays by index (SparseKeys); the storeys level by level, their tiles and decor by index; the units by index,
        /// field by field (`$new`: a unit that wasn't there).
        /// </summary>
        public static JsonObject StateDelta(JsonElement prev, JsonElement now)
        {
            var d = new JsonObject();
            var keys = prev.EnumerateObject().Select((p) => p.Name).ToList();
            foreach (var p in now.EnumerateObject()) if (!keys.Contains(p.Name)) keys.Add(p.Name);
            foreach (var k in keys)
            {
                var a = Get(prev, k);
                var b = Get(now, k);
                if (k == "units")
                {
                    var units = new JsonObject();
                    var was = a.EnumerateArray().ToList();
                    var are = b.EnumerateArray().ToList();
                    for (var i = 0; i < are.Count; i++)
                    {
                        if (i >= was.Count)
                        {
                            units[i.ToString()] = new JsonObject { ["$new"] = Recording.Node(are[i]) };
                            continue;
                        }
                        var fields = new JsonObject();
                        var names = was[i].EnumerateObject().Select((f) => f.Name).ToList();
                        foreach (var f in are[i].EnumerateObject()) if (!names.Contains(f.Name)) names.Add(f.Name);
                        foreach (var f in names) if (!Same(Get(was[i], f), Get(are[i], f))) fields[f] = NodeOf(are[i], f);
                        if (fields.Count > 0) units[i.ToString()] = fields;
                    }
                    if (units.Count > 0) d["units"] = units;
                    continue;
                }
                if (Same(a, b)) continue;
                if (SparseKeys.Contains(k)) d[k] = Sparse(a, b);
                else if (k == "levels" && a.ValueKind == JsonValueKind.Array && b.ValueKind == JsonValueKind.Array && a.GetArrayLength() == b.GetArrayLength())
                {
                    var levels = new JsonObject();
                    var was = a.EnumerateArray().ToList();
                    var are = b.EnumerateArray().ToList();
                    for (var i = 0; i < are.Count; i++)
                    {
                        var change = new JsonObject();
                        if (!Same(Get(was[i], "tiles"), Get(are[i], "tiles"))) change["tiles"] = Sparse(Get(was[i], "tiles"), Get(are[i], "tiles"));
                        if (!Same(Get(was[i], "decor"), Get(are[i], "decor"))) change["decor"] = Sparse(Get(was[i], "decor"), Get(are[i], "decor"));
                        if (change.Count > 0) levels[i.ToString()] = change;
                    }
                    d["levels"] = new JsonObject { ["$levels"] = levels };
                }
                else d[k] = b.ValueKind == JsonValueKind.Undefined ? null : Recording.Node(b);
            }
            return d;
        }
    }
}
