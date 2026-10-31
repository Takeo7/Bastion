using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The golden files recorded with C# (engine-cs README, «Grabar con C#»). Each recorder (Record*.cs) makes its files
    /// again from their inputs (a mission's options, the operations of a game as they were played, a campaign and a seed)
    /// with the C# engine as it is now, and writes them over golden/ in the sources: `BASTION_GRABAR=1 dotnet test
    /// engine-cs/tests --filter Recorder`. What C# says the same as the file keeps the file's text, byte for byte; only
    /// what changed is written, as JSON.stringify would (Fresh). So with the rules unchanged nothing changes, and a rule
    /// changed on purpose leaves a diff that shows what it changed. Without BASTION_GRABAR the recorders are skipped.
    /// </summary>
    public static class Recording
    {
        public const string Variable = "BASTION_GRABAR";

        public static bool On => Environment.GetEnvironmentVariable(Variable) == "1";

        /// <summary>engine-cs/golden in the sources, not the copy beside the build that the tests read.</summary>
        public static string Dir
        {
            get
            {
                for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                {
                    if (File.Exists(Path.Combine(d.FullName, "Bastion.Engine.csproj")) && Directory.Exists(Path.Combine(d.FullName, "golden"))) return Path.Combine(d.FullName, "golden");
                }
                throw new DirectoryNotFoundException("engine-cs/golden");
            }
        }

        public static string PathOf(string relative) => Path.Combine(Dir, relative);

        /// <summary>A golden file's text and its parsed root.</summary>
        public static (string Text, JsonElement Root) Load(string relative)
        {
            var text = File.ReadAllText(PathOf(relative));
            return (text, JsonDocument.Parse(text).RootElement);
        }

        /// <summary>
        /// Writes `fresh` (what C# says now) over the file whose text is `old` and root `raw`. `norm` is the file read as
        /// C# reads it (`raw` itself where nothing reads it through a class; see Normal), the same shape as `raw`: where
        /// it says what `fresh` says, `raw`'s text stays. Returns what changed (JSON paths); the file is written only then.
        /// </summary>
        public static List<string> Write(string relative, string old, JsonElement raw, JsonNode? norm, JsonNode? fresh)
        {
            var changes = new List<string>();
            var text = new StringBuilder(old.Length + 64);
            Merge(text, raw, Element(norm), Element(fresh), "$", changes);
            if (old.EndsWith("\n", StringComparison.Ordinal)) text.Append('\n');
            var now = text.ToString();
            if (now != old) File.WriteAllText(PathOf(relative), now, new UTF8Encoding(false));
            else if (changes.Count > 0) throw new InvalidOperationException($"{relative}: changes found ({changes[0]}) but the same text");
            return changes;
        }

        /// <summary>What a recorder tells: the file, and what changed in it.</summary>
        public static string Report(string relative, List<string> changes)
        {
            if (changes.Count == 0) return $"{relative}: igual";
            return $"{relative}: {changes.Count} cambios\n  " + string.Join("\n  ", changes.Take(12)) + (changes.Count > 12 ? "\n  …" : "");
        }

        // ------------------------------------------------------------ JSON nodes

        public static JsonElement Element(JsonNode? node)
        {
            return node == null ? JsonDocument.Parse("null").RootElement : JsonSerializer.SerializeToElement(node);
        }

        /// <summary>A file's value as a node, to build `norm` and `fresh` from.</summary>
        public static JsonNode? Node(JsonElement e)
        {
            return e.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(e.GetRawText());
        }

        /// <summary>A C# value as the tests' JSON writes it (Step1Helpers.Options).</summary>
        public static JsonNode? Node(object? value)
        {
            return value == null ? null : JsonSerializer.SerializeToNode(value, value.GetType(), Step1Helpers.Options);
        }

        /// <summary>A recorded value read through its C# class and written again, as the tests compare it (Step3Json.Check).</summary>
        public static JsonNode? Normal<T>(JsonElement recorded)
        {
            return recorded.ValueKind == JsonValueKind.Null ? null : Node(Step1Helpers.Read<T>(recorded));
        }

        /// <summary>The same with Newtonsoft (the network's JSON, the campaign's): a value written as CampaignJsonTests.Write does.</summary>
        public static JsonNode? NodeOfText(string json) => JsonNode.Parse(json);

        /// <summary>
        /// For `norm`: what the file says there no longer holds, so `fresh` goes in whole. For changes recorded as JSON
        /// (a state's delta), where null means gone and isn't the same as missing.
        /// </summary>
        public static JsonNode Replaced() => JsonValue.Create("$replaced");

        // ------------------------------------------------------------ the merge

        private static bool IsNull(JsonElement e) => e.ValueKind == JsonValueKind.Null || e.ValueKind == JsonValueKind.Undefined;

        /// <summary>Whether two JSON values say the same: null and missing alike, numbers by value.</summary>
        public static bool Same(JsonElement a, JsonElement b)
        {
            if (IsNull(a) || IsNull(b)) return IsNull(a) && IsNull(b);
            if (a.ValueKind != b.ValueKind) return false;
            switch (a.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    foreach (var p in a.EnumerateObject())
                    {
                        var other = b.TryGetProperty(p.Name, out var q) ? q : default;
                        if (!Same(p.Value, other)) return false;
                    }
                    foreach (var q in b.EnumerateObject()) if (!a.TryGetProperty(q.Name, out _) && !IsNull(q.Value)) return false;
                    return true;
                }
                case JsonValueKind.Array:
                {
                    if (a.GetArrayLength() != b.GetArrayLength()) return false;
                    using (var x = a.EnumerateArray().GetEnumerator())
                    using (var y = b.EnumerateArray().GetEnumerator())
                    {
                        while (x.MoveNext() && y.MoveNext()) if (!Same(x.Current, y.Current)) return false;
                    }
                    return true;
                }
                case JsonValueKind.Number:
                    return a.GetDouble() == b.GetDouble();
                case JsonValueKind.String:
                    return a.GetString() == b.GetString();
                default:
                    return true;
            }
        }

        /// <summary>
        /// `raw`'s text where `norm` (raw as C# reads it) says what `fresh` says; elsewhere `fresh`, keeping `raw`'s keys
        /// in their order (a new key goes after the one before it in `fresh`) and its arrays item by item.
        /// </summary>
        private static void Merge(StringBuilder text, JsonElement raw, JsonElement norm, JsonElement fresh, string path, List<string> changes)
        {
            if (Same(norm, fresh))
            {
                text.Append(raw.GetRawText());
                return;
            }
            if (raw.ValueKind == JsonValueKind.Object && norm.ValueKind == JsonValueKind.Object && fresh.ValueKind == JsonValueKind.Object)
            {
                var entries = new List<(string Name, string Text)>();
                foreach (var p in raw.EnumerateObject())
                {
                    var hasFresh = fresh.TryGetProperty(p.Name, out var f) && !IsNull(f);
                    var hasNorm = norm.TryGetProperty(p.Name, out var n) && !IsNull(n);
                    if (!hasFresh)
                    {
                        if (!hasNorm) entries.Add((p.Name, p.Value.GetRawText()));
                        else changes.Add($"{path}.{p.Name} (fuera)");
                        continue;
                    }
                    var value = new StringBuilder();
                    if (hasNorm) Merge(value, p.Value, n, f, $"{path}.{p.Name}", changes);
                    else
                    {
                        changes.Add($"{path}.{p.Name}");
                        Fresh(value, f);
                    }
                    entries.Add((p.Name, value.ToString()));
                }
                var order = fresh.EnumerateObject().Select((p) => p.Name).ToList();
                for (var i = 0; i < order.Count; i++)
                {
                    var name = order[i];
                    var f = fresh.GetProperty(name);
                    if (raw.TryGetProperty(name, out _) || IsNull(f)) continue;
                    // What C# reads where the file says nothing (a field's default) is not a change.
                    if (norm.TryGetProperty(name, out var n) && Same(n, f)) continue;
                    changes.Add($"{path}.{name} (nuevo)");
                    var value = new StringBuilder();
                    Fresh(value, f);
                    var at = 0;
                    for (var j = i - 1; j >= 0; j--)
                    {
                        var k = entries.FindIndex((e) => e.Name == order[j]);
                        if (k >= 0)
                        {
                            at = k + 1;
                            break;
                        }
                    }
                    entries.Insert(at, (name, value.ToString()));
                }
                text.Append('{');
                for (var i = 0; i < entries.Count; i++)
                {
                    if (i > 0) text.Append(',');
                    Str(text, entries[i].Name);
                    text.Append(':').Append(entries[i].Text);
                }
                text.Append('}');
                return;
            }
            if (raw.ValueKind == JsonValueKind.Array && norm.ValueKind == JsonValueKind.Array && fresh.ValueKind == JsonValueKind.Array && raw.GetArrayLength() == norm.GetArrayLength())
            {
                var r = raw.EnumerateArray().ToList();
                var n = norm.EnumerateArray().ToList();
                var f = fresh.EnumerateArray().ToList();
                if (r.Count != f.Count) changes.Add($"{path}: {r.Count} → {f.Count}");
                text.Append('[');
                for (var i = 0; i < f.Count; i++)
                {
                    if (i > 0) text.Append(',');
                    if (i < r.Count) Merge(text, r[i], n[i], f[i], $"{path}[{i}]", changes);
                    else Fresh(text, f[i]);
                }
                text.Append(']');
                return;
            }
            changes.Add(path);
            Fresh(text, fresh);
        }

        // ------------------------------------------------------------ JSON.stringify

        /// <summary>A value written as JSON.stringify writes it: no spaces, numbers as JavaScript prints them, only what must be escaped.</summary>
        public static void Fresh(StringBuilder text, JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    text.Append('{');
                    var first = true;
                    foreach (var p in e.EnumerateObject())
                    {
                        if (!first) text.Append(',');
                        first = false;
                        Str(text, p.Name);
                        text.Append(':');
                        Fresh(text, p.Value);
                    }
                    text.Append('}');
                    break;
                }
                case JsonValueKind.Array:
                {
                    text.Append('[');
                    var first = true;
                    foreach (var x in e.EnumerateArray())
                    {
                        if (!first) text.Append(',');
                        first = false;
                        Fresh(text, x);
                    }
                    text.Append(']');
                    break;
                }
                case JsonValueKind.String:
                    Str(text, e.GetString()!);
                    break;
                case JsonValueKind.Number:
                    text.Append(e.TryGetInt64(out var whole) && Math.Abs(whole) < (1L << 53) ? whole.ToString(CultureInfo.InvariantCulture) : Number(e.GetDouble()));
                    break;
                case JsonValueKind.True:
                    text.Append("true");
                    break;
                case JsonValueKind.False:
                    text.Append("false");
                    break;
                default:
                    text.Append("null");
                    break;
            }
        }

        public static string Fresh(JsonNode? node)
        {
            var text = new StringBuilder();
            Fresh(text, Element(node));
            return text.ToString();
        }

        /// <summary>As JSON.stringify(value, null, 1) writes it: a key or an item a line, one space deeper each time.</summary>
        public static void Pretty(StringBuilder text, JsonElement e, int depth = 0)
        {
            var inner = new string(' ', depth + 1);
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var props = e.EnumerateObject().ToList();
                    if (props.Count == 0)
                    {
                        text.Append("{}");
                        break;
                    }
                    text.Append("{\n");
                    for (var i = 0; i < props.Count; i++)
                    {
                        text.Append(inner);
                        Str(text, props[i].Name);
                        text.Append(": ");
                        Pretty(text, props[i].Value, depth + 1);
                        text.Append(i < props.Count - 1 ? ",\n" : "\n");
                    }
                    text.Append(' ', depth).Append('}');
                    break;
                }
                case JsonValueKind.Array:
                {
                    var items = e.EnumerateArray().ToList();
                    if (items.Count == 0)
                    {
                        text.Append("[]");
                        break;
                    }
                    text.Append("[\n");
                    for (var i = 0; i < items.Count; i++)
                    {
                        text.Append(inner);
                        Pretty(text, items[i], depth + 1);
                        text.Append(i < items.Count - 1 ? ",\n" : "\n");
                    }
                    text.Append(' ', depth).Append(']');
                    break;
                }
                default:
                    Fresh(text, e);
                    break;
            }
        }

        /// <summary>
        /// A whole file written again as JSON.stringify(value, null, 1) does, nulls and all (golden/lore/scenes.json). Returns
        /// whether it changed.
        /// </summary>
        public static bool WritePretty(string relative, string old, JsonNode fresh)
        {
            var text = new StringBuilder(old.Length + 64);
            Pretty(text, Element(fresh));
            if (old.EndsWith("\n", StringComparison.Ordinal)) text.Append('\n');
            var now = text.ToString();
            if (now == old) return false;
            File.WriteAllText(PathOf(relative), now, new UTF8Encoding(false));
            return true;
        }

        /// <summary>A string as JSON.stringify quotes it: `"`, `\`, the controls and lone surrogates escaped, the rest as it is.</summary>
        public static void Str(StringBuilder text, string s)
        {
            text.Append('"');
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\b': text.Append("\\b"); break;
                    case '\f': text.Append("\\f"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (c < 0x20) text.Append("\\u").Append(((int)c).ToString("x4"));
                        else if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                        {
                            text.Append(c).Append(s[i + 1]);
                            i++;
                        }
                        else if (char.IsSurrogate(c)) text.Append("\\u").Append(((int)c).ToString("x4"));
                        else text.Append(c);
                        break;
                }
            }
            text.Append('"');
        }

        /// <summary>A number as JavaScript prints it (Number::toString): the shortest digits, in exponent form below 1e-6 and from 1e21.</summary>
        public static string Number(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "null";
            if (d == 0) return "0";
            var r = d.ToString("R", CultureInfo.InvariantCulture);
            var sign = "";
            if (r[0] == '-')
            {
                sign = "-";
                r = r.Substring(1);
            }
            var e = 0;
            var at = r.IndexOfAny(new[] { 'E', 'e' });
            if (at >= 0)
            {
                e = int.Parse(r.Substring(at + 1), CultureInfo.InvariantCulture);
                r = r.Substring(0, at);
            }
            var point = r.IndexOf('.');
            var whole = point < 0 ? r.Length : point;
            var digits = r.Replace(".", "");
            var lead = 0;
            while (lead < digits.Length - 1 && digits[lead] == '0') lead++;
            digits = digits.Substring(lead);
            whole -= lead;
            digits = digits.TrimEnd('0');
            if (digits.Length == 0) return "0";
            // value = 0.digits × 10^n
            var n = whole + e;
            var k = digits.Length;
            if (k <= n && n <= 21) return sign + digits + new string('0', n - k);
            if (0 < n && n <= 21) return sign + digits.Substring(0, n) + "." + digits.Substring(n);
            if (-6 < n && n <= 0) return sign + "0." + new string('0', -n) + digits;
            var exp = n - 1;
            var mantissa = k == 1 ? digits : digits.Substring(0, 1) + "." + digits.Substring(1);
            return sign + mantissa + "e" + (exp < 0 ? "-" : "+") + Math.Abs(exp).ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>A recorder: run only with BASTION_GRABAR=1 (Recording).</summary>
    public sealed class RecorderFactAttribute : FactAttribute
    {
        public RecorderFactAttribute()
        {
            if (!Recording.On) Skip = $"Solo con {Recording.Variable}=1: graba golden/ con C#.";
        }
    }

    /// <summary>JSON.stringify's numbers, against what node printed (golden/step1/math.json was written by it).</summary>
    public class RecordingNumbersTests
    {
        [Fact]
        public void NumbersAsJavaScriptPrintsThem()
        {
            Assert.Equal("0.1", Recording.Number(0.1));
            Assert.Equal("1e-7", Recording.Number(1e-7));
            Assert.Equal("0.000001", Recording.Number(1e-6));
            Assert.Equal("123456789012345680000", Recording.Number(123456789012345678901.0));
            Assert.Equal("1e+21", Recording.Number(1e21));
            Assert.Equal("-2.5e-8", Recording.Number(-2.5e-8));
            Assert.Equal("1.7976931348623157e+308", Recording.Number(double.MaxValue));
            Assert.Equal("5e-324", Recording.Number(double.Epsilon));
            Assert.Equal("-0.30000000000000004", Recording.Number(-(0.1 + 0.2)));
            Assert.Equal("100", Recording.Number(100));
        }

        [Fact]
        public void TheNumbersOfMathJsonComeOutAsNodeWroteThem()
        {
            // Every number in the file, written again, is the text node wrote: the arrays are flat, so the text splits on commas.
            var text = File.ReadAllText(Path.Combine("golden", "step1", "math.json"));
            var root = JsonDocument.Parse(text).RootElement;
            foreach (var key in new[] { "atan2", "hypot", "round" })
            {
                var start = text.IndexOf($"\"{key}\":[", StringComparison.Ordinal) + key.Length + 4;
                var end = text.IndexOf(']', start);
                var written = text.Substring(start, end - start).Split(',');
                var values = root.GetProperty(key).EnumerateArray().Select((x) => x.GetDouble()).ToList();
                Assert.Equal(written.Length, values.Count);
                for (var i = 0; i < values.Count; i++) Assert.Equal(written[i], Recording.Number(values[i]));
            }
        }
    }
}
