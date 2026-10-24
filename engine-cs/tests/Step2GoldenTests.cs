using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bastion.Engine;
using Xunit;
using Xunit.Abstractions;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Step 2 of the first milestone (docs/UNITY.md): whole missions played by the TypeScript engine
    /// (engine-cs/golden/record-step2.ts writes golden/step2), replayed through the C# rules from the same state with the
    /// same Rng: every operation must give the same result, the same events and the same Rng state, and leave the same
    /// state, to the bit. Null and missing are the same (README); the TypeScript JSON is read through the C# types, so a
    /// field TypeScript leaves out reads as C# has it.
    /// </summary>
    public class Step2GoldenTests
    {
        private readonly ITestOutputHelper output;

        public Step2GoldenTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        public static IEnumerable<object[]> Missions => Directory.Exists(Path.Combine("golden", "step2"))
            ? Directory.GetFiles(Path.Combine("golden", "step2"), "*.json").Select((f) => new object[] { Path.GetFileNameWithoutExtension(f) }).OrderBy((x) => (string)x[0])
            : Enumerable.Empty<object[]>();

        [Theory]
        [MemberData(nameof(Missions))]
        public void AMissionReplaysAsTypeScriptPlayedIt(string name)
        {
            var file = JsonDocument.Parse(File.ReadAllText(Path.Combine("golden", "step2", name + ".json"))).RootElement;
            var replay = new Step2Replay(name, file);
            var failure = replay.Run();
            output.WriteLine($"{name}: {replay.Ops} operations, {replay.EventsChecked} events, {replay.StatesChecked} states replayed");
            if (failure != null) throw new Xunit.Sdk.XunitException(failure);
        }
    }

    /// <summary>One recorded mission replayed: the C# state and the TypeScript one rebuilt from the deltas, side by side.</summary>
    public sealed class Step2Replay
    {
        private readonly string name;
        private readonly JsonElement file;
        private readonly GameState actual;
        private readonly GameState expected;
        private readonly Rng rng;

        public int Ops { get; private set; }
        public int EventsChecked { get; private set; }
        public int StatesChecked { get; private set; }

        public Step2Replay(string name, JsonElement file)
        {
            this.name = name;
            this.file = file;
            actual = Read<GameState>(file.GetProperty("state"));
            expected = Read<GameState>(file.GetProperty("state"));
            rng = new Rng(file.GetProperty("rngSeed").GetInt32());
        }

        /// <summary>Plays every operation; the first difference, named, or null.</summary>
        public string? Run()
        {
            var ops = file.GetProperty("ops");
            var i = 0;
            foreach (var op in ops.EnumerateArray())
            {
                var where = $"{name}, operation {i} ({Describe(op)})";
                string? failure;
                try
                {
                    failure = Step(op);
                }
                catch (Exception e)
                {
                    failure = $"threw {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
                }
                Ops = i + 1;
                if (failure != null) return $"{where}: {failure}";
                i++;
            }
            var end = SameStatics();
            return end != null ? $"{name}, at the end: {end}" : null;
        }

        private static string Describe(JsonElement op)
        {
            var kind = op.GetProperty("op").GetString();
            if (kind == "command") return $"slot {op.GetProperty("slot")}: {op.GetProperty("cmd").GetRawText()}";
            return op.GetRawText().Length > 200 ? kind! : RawOp(op);
        }

        private static string RawOp(JsonElement op)
        {
            var o = JsonNode.Parse(op.GetRawText())!.AsObject();
            o.Remove("events");
            o.Remove("delta");
            o.Remove("rng");
            return o.ToJsonString();
        }

        private static List<int> Ints(JsonElement e)
        {
            return e.EnumerateArray().Select((x) => x.GetInt32()).ToList();
        }

        private string? Step(JsonElement op)
        {
            List<GameEvent> events;
            switch (op.GetProperty("op").GetString())
            {
                case "command":
                {
                    var cmd = CommandOf(op.GetProperty("cmd"));
                    var result = Rules.ExecuteCommand(actual, cmd, op.GetProperty("slot").GetInt32(), Ints(op.GetProperty("connected")), rng);
                    var ok = op.GetProperty("ok").GetBoolean();
                    if (result.Ok != ok) return $"ok: expected {ok}, got {result.Ok} ({result.Error})";
                    if (!ok)
                    {
                        var error = op.GetProperty("error").GetString();
                        if (result.Error != error) return $"error: expected «{error}», got «{result.Error}»";
                    }
                    events = result.Events ?? new List<GameEvent>();
                    break;
                }
                case "syncTurn":
                    events = Rules.SyncTurn(actual, Ints(op.GetProperty("connected")), rng);
                    break;
                case "runOutOfTime":
                    events = Rules.RunOutOfTime(actual, op.GetProperty("slot").GetInt32(), Ints(op.GetProperty("connected")), rng);
                    break;
                case "callAbandon":
                    events = Rules.CallAbandon(actual, op.GetProperty("slot").GetInt32(), Ints(op.GetProperty("connected")));
                    break;
                case "answerAbandon":
                    events = Rules.AnswerAbandon(actual, op.GetProperty("slot").GetInt32(), op.GetProperty("accept").GetBoolean());
                    break;
                default:
                    return "unknown operation";
            }

            var recorded = op.GetProperty("events").EnumerateArray().ToList();
            for (var k = 0; k < Math.Min(recorded.Count, events.Count); k++)
            {
                var diff = Step2Json.EventDiff(recorded[k], events[k]);
                EventsChecked++;
                if (diff != null) return $"event {k} ({recorded[k].GetProperty("t").GetString()}) {diff}\n  expected events: {string.Join(", ", recorded.Select((e) => e.GetProperty("t").GetString()))}\n  got: {string.Join(", ", events.Select((e) => e.T))}";
            }
            if (recorded.Count != events.Count)
            {
                return $"{recorded.Count} events expected, got {events.Count}\n  expected: {string.Join(", ", recorded.Select((e) => e.GetProperty("t").GetString()))}\n  got: {string.Join(", ", events.Select((e) => e.T))}";
            }

            var state = Ints(op.GetProperty("rng"));
            var now = rng.State;
            if (!state.Select((x) => (long)x).SequenceEqual(now)) return $"the Rng's state: expected [{string.Join(", ", state)}], got [{string.Join(", ", now)}]";

            Step2Json.ApplyDelta(expected, op.GetProperty("delta"));
            StatesChecked++;
            var stateDiff = Step2Json.StateDiff(expected, actual);
            return stateDiff != null ? $"the state after it, at {stateDiff}" : null;
        }

        private string? SameStatics()
        {
            foreach (var (key, a, b) in new (string, object?, object?)[]
            {
                ("frame", expected.Frame, actual.Frame),
                ("medical", expected.Medical, actual.Medical),
                ("homes", expected.Homes, actual.Homes),
                ("markets", expected.Markets, actual.Markets),
            })
            {
                var diff = Step2Json.Diff(Step2Json.Element(a), Step2Json.Element(b), key);
                if (diff != null) return diff;
            }
            return null;
        }

        /// <summary>A recorded command (protocol.ts commandSchema), by its `type`.</summary>
        public static Command CommandOf(JsonElement e)
        {
            switch (e.GetProperty("type").GetString())
            {
                case "move": return Read<MoveCommand>(e);
                case "ability": return Read<AbilityCommand>(e);
                case "endTurn": return Read<EndTurnCommand>(e);
                case "evac": return Read<EvacCommand>(e);
                case "evacAnswer": return Read<EvacAnswerCommand>(e);
                case "item": return Read<ItemCommand>(e);
                case "pass": return new PassCommand();
                default: throw new InvalidOperationException($"Unknown command {e.GetRawText()}");
            }
        }
    }

    /// <summary>The JSON side of the replay: events and states compared, and the recorded deltas applied.</summary>
    public static class Step2Json
    {
        private static readonly Dictionary<string, Type> EventTypes = typeof(GameEvent).Assembly.GetTypes()
            .Where((t) => !t.IsAbstract && typeof(GameEvent).IsAssignableFrom(t))
            .ToDictionary((t) => ((GameEvent)Activator.CreateInstance(t)!).T);

        private static readonly Dictionary<string, PropertyInfo> StateProps = typeof(GameState).GetProperties()
            .ToDictionary((p) => JsonNamingPolicy.CamelCase.ConvertName(p.Name));

        public static JsonElement Element(object? x)
        {
            return JsonDocument.Parse(x == null ? "null" : JsonSerializer.Serialize(x, x.GetType(), Options)).RootElement;
        }

        /// <summary>A recorded event against C#'s: read through its C# class (what TypeScript leaves out reads as C# has it), and nothing of the recorded one lost on the way.</summary>
        public static string? EventDiff(JsonElement recorded, GameEvent actual)
        {
            var t = recorded.GetProperty("t").GetString()!;
            if (actual.T != t) return $"is {actual.T}, not {t}";
            if (!EventTypes.TryGetValue(t, out var type)) return $"has no C# class";
            var normal = Element(JsonSerializer.Deserialize(recorded.GetRawText(), type, Options));
            var lost = Lost(recorded, normal, "");
            if (lost != null) return $"has a field C# doesn't read: {lost}";
            return Diff(normal, Element(actual), "");
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

        private static bool IsNull(JsonElement? e)
        {
            return e == null || e.Value.ValueKind == JsonValueKind.Null || e.Value.ValueKind == JsonValueKind.Undefined;
        }

        private static string Show(JsonElement? e)
        {
            if (IsNull(e)) return "null";
            var text = e!.Value.GetRawText();
            return text.Length > 300 ? text.Substring(0, 300) + "…" : text;
        }

        /// <summary>The first difference between two JSON values, as `path: expected …, got …`; null and missing alike, numbers by value (to the bit).</summary>
        public static string? Diff(JsonElement? a, JsonElement? b, string path)
        {
            if (IsNull(a) && IsNull(b)) return null;
            if (IsNull(a) || IsNull(b) || a!.Value.ValueKind != b!.Value.ValueKind)
            {
                // true and false are kinds of their own in JsonElement.
                return $"{path}: expected {Show(a)}, got {Show(b)}";
            }
            var x = a.Value;
            var y = b.Value;
            switch (x.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var keys = x.EnumerateObject().Select((p) => p.Name).ToList();
                    foreach (var p in y.EnumerateObject()) if (!keys.Contains(p.Name)) keys.Add(p.Name);
                    foreach (var k in keys)
                    {
                        JsonElement? xa = x.TryGetProperty(k, out var va) ? va : (JsonElement?)null;
                        JsonElement? yb = y.TryGetProperty(k, out var vb) ? vb : (JsonElement?)null;
                        var d = Diff(xa, yb, $"{path}.{k}");
                        if (d != null) return d;
                    }
                    return null;
                }
                case JsonValueKind.Array:
                {
                    var xs = x.EnumerateArray().ToList();
                    var ys = y.EnumerateArray().ToList();
                    for (var i = 0; i < Math.Min(xs.Count, ys.Count); i++)
                    {
                        var d = Diff(xs[i], ys[i], $"{path}[{i}]");
                        if (d != null) return d;
                    }
                    return xs.Count == ys.Count ? null : $"{path}: {xs.Count} items expected, got {ys.Count}: expected {Show(x)}, got {Show(y)}";
                }
                case JsonValueKind.Number:
                {
                    var dx = x.GetDouble();
                    var dy = y.GetDouble();
                    return dx == dy ? null : $"{path}: expected {dx:R}, got {dy:R}";
                }
                case JsonValueKind.String:
                    return x.GetString() == y.GetString() ? null : $"{path}: expected {Show(x)}, got {Show(y)}";
                default:
                    return null;
            }
        }

        // ------------------------------------------------------------ the deltas

        private static T? ReadNullable<T>(JsonElement e) where T : class
        {
            return e.ValueKind == JsonValueKind.Null ? null : Read<T>(e);
        }

        /// <summary>A `{ $sparse: [i, v…] }` change applied to `list`, or the whole new list.</summary>
        private static List<T>? Sparse<T>(List<T>? list, JsonElement change)
        {
            if (change.ValueKind == JsonValueKind.Object && change.TryGetProperty("$sparse", out var pairs))
            {
                var xs = pairs.EnumerateArray().ToList();
                for (var k = 0; k < xs.Count; k += 2) list![xs[k].GetInt32()] = xs[k + 1].ValueKind == JsonValueKind.Null ? default! : JsonSerializer.Deserialize<T>(xs[k + 1].GetRawText(), Options)!;
                return list;
            }
            return change.ValueKind == JsonValueKind.Null ? null : Read<List<T>>(change);
        }

        /// <summary>record-step2.ts stateDelta's changes, applied to the TypeScript state as C# holds it.</summary>
        public static void ApplyDelta(GameState s, JsonElement delta)
        {
            foreach (var p in delta.EnumerateObject())
            {
                var v = p.Value;
                switch (p.Name)
                {
                    case "units":
                        foreach (var u in v.EnumerateObject())
                        {
                            var i = int.Parse(u.Name);
                            if (u.Value.TryGetProperty("$new", out var whole))
                            {
                                if (i == s.Units.Count) s.Units.Add(Read<Unit>(whole));
                                else s.Units[i] = Read<Unit>(whole);
                                continue;
                            }
                            var node = JsonSerializer.SerializeToNode(s.Units[i], Options)!.AsObject();
                            foreach (var f in u.Value.EnumerateObject())
                            {
                                var key = node.Select((kv) => kv.Key).FirstOrDefault((k) => string.Equals(k, f.Name, StringComparison.OrdinalIgnoreCase)) ?? f.Name;
                                if (f.Value.ValueKind == JsonValueKind.Null) node.Remove(key);
                                else node[key] = JsonNode.Parse(f.Value.GetRawText());
                            }
                            s.Units[i] = JsonSerializer.Deserialize<Unit>(node.ToJsonString(), Options)!;
                        }
                        break;
                    case "tiles":
                        s.Tiles = Sparse(s.Tiles, v)!;
                        break;
                    case "elev":
                        s.Elev = Sparse(s.Elev, v)!;
                        break;
                    case "ground":
                        s.Ground = Sparse(s.Ground, v);
                        break;
                    case "decor":
                        s.Decor = Sparse(s.Decor, v);
                        break;
                    case "storeys":
                        s.Storeys = Sparse(s.Storeys, v);
                        break;
                    case "levels":
                        if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("$levels", out var levels))
                        {
                            foreach (var l in levels.EnumerateObject())
                            {
                                var level = s.Levels![int.Parse(l.Name)];
                                if (l.Value.TryGetProperty("tiles", out var tiles)) level.Tiles = Sparse(level.Tiles, tiles)!;
                                if (l.Value.TryGetProperty("decor", out var decor)) level.Decor = Sparse(level.Decor, decor);
                            }
                        }
                        else s.Levels = ReadNullable<List<Level>>(v);
                        break;
                    default:
                    {
                        if (!StateProps.TryGetValue(p.Name, out var prop)) throw new InvalidOperationException($"GameState has no {p.Name}");
                        prop.SetValue(s, v.ValueKind == JsonValueKind.Null ? null : JsonSerializer.Deserialize(v.GetRawText(), prop.PropertyType, Options));
                        break;
                    }
                }
            }
        }

        // ------------------------------------------------------------ the states

        /// <summary>
        /// Where two states differ first, or null: the big lists of the map compared as lists (tiles, elev, ground, decor,
        /// storeys, the storeys' tiles and decor), the rest as JSON (the map's furnishings, which nothing changes, once at
        /// the end).
        /// </summary>
        public static string? StateDiff(GameState expected, GameState actual)
        {
            string? Lists<T>(string what, IReadOnlyList<T>? a, IReadOnlyList<T>? b)
            {
                if (a == null && b == null) return null;
                if (a == null || b == null) return $"{what}: expected {(a == null ? "null" : "a list")}, got {(b == null ? "null" : "a list")}";
                if (a.Count != b.Count) return $"{what}: {a.Count} expected, got {b.Count}";
                for (var i = 0; i < a.Count; i++) if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) return $"{what}[{i}]: expected {a[i]}, got {b[i]}";
                return null;
            }
            var big = Lists(".tiles", expected.Tiles, actual.Tiles)
                ?? Lists(".elev", expected.Elev, actual.Elev)
                ?? Lists(".ground", expected.Ground, actual.Ground)
                ?? Lists(".decor", expected.Decor, actual.Decor)
                ?? Lists(".storeys", expected.Storeys, actual.Storeys);
            if (big != null) return big;
            if ((expected.Levels == null) != (actual.Levels == null)) return $".levels: expected {(expected.Levels == null ? "none" : "some")}, got {(actual.Levels == null ? "none" : "some")}";
            if (expected.Levels != null)
            {
                if (expected.Levels.Count != actual.Levels!.Count) return $".levels: {expected.Levels.Count} expected, got {actual.Levels.Count}";
                for (var k = 0; k < expected.Levels.Count; k++)
                {
                    var d = Lists($".levels[{k}].tiles", expected.Levels[k].Tiles, actual.Levels[k].Tiles) ?? Lists($".levels[{k}].decor", expected.Levels[k].Decor, actual.Levels[k].Decor);
                    if (d != null) return d;
                }
            }
            return Diff(Small(expected), Small(actual), "");
        }

        /// <summary>The state as JSON without the big lists StateDiff compares apart.</summary>
        private static JsonElement Small(GameState s)
        {
            var (tiles, elev, ground, decor, storeys, levels, frame, medical, homes, markets) = (s.Tiles, s.Elev, s.Ground, s.Decor, s.Storeys, s.Levels, s.Frame, s.Medical, s.Homes, s.Markets);
            try
            {
                s.Tiles = null!;
                s.Elev = null!;
                s.Ground = null;
                s.Decor = null;
                s.Storeys = null;
                s.Levels = null;
                s.Frame = null;
                s.Medical = null;
                s.Homes = null;
                s.Markets = null;
                return Element(s);
            }
            finally
            {
                (s.Tiles, s.Elev, s.Ground, s.Decor, s.Storeys, s.Levels, s.Frame, s.Medical, s.Homes, s.Markets) = (tiles, elev, ground, decor, storeys, levels, frame, medical, homes, markets);
            }
        }
    }
}
