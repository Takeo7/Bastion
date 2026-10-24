using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bastion.Engine;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// packages/engine/test/helpers.ts for the step-1 tests: a small state from ASCII rows (stateFrom, withLevels) and
    /// units put on it (addUnit, with setup.ts makeUnit's fields). And reading the golden files' states.
    /// </summary>
    public static class Step1Helpers
    {
        private static readonly Dictionary<char, string> Legend = new Dictionary<char, string>
        {
            ['#'] = TileKinds.Wall,
            ['H'] = TileKinds.High,
            ['h'] = TileKinds.Low,
            ['+'] = TileKinds.Door,
            ['='] = TileKinds.Window,
            ['L'] = TileKinds.Ladder,
            ['S'] = TileKinds.Ladder,
            ['C'] = TileKinds.Console,
        };

        /// <summary>
        /// Builds a small state from ASCII rows (same legend as maps, without pods),
        /// with optional elevation digits. Units are added explicitly with `addUnit`.
        /// </summary>
        public static GameState StateFrom(string[] rows, string[]? heights = null)
        {
            var tiles = new List<string>();
            var elev = new List<int>();
            for (var y = 0; y < rows.Length; y++)
            {
                for (var x = 0; x < rows[y].Length; x++)
                {
                    tiles.Add(Legend.TryGetValue(rows[y][x], out var kind) ? kind : TileKinds.Floor);
                    var h = heights != null && y < heights.Length && x < heights[y].Length ? heights[y][x] : '0';
                    elev.Add(char.IsDigit(h) ? h - '0' : 0);
                }
            }
            return new GameState
            {
                Seq = 0,
                MapId = "test",
                MapName = "Prueba",
                Biome = Biomes.City,
                Width = rows[0].Length,
                Height = rows.Length,
                Tiles = tiles,
                Elev = elev,
                Units = new List<Unit>(),
                Pods = new List<Pod>(),
                Mission = new MissionState { Kind = MissionKinds.Elimination, ObjectiveDone = false, HackBonus = 0 },
                Turn = 1,
                ActiveTeam = Teams.Xcom,
                Concealed = false,
                Ready = new[] { false, false },
                Smoke = new List<SmokeCloud>(),
            };
        }

        /// <summary>
        /// Gives a state its storeys above the ground (docs/FASE6-PLAN.md), bottom up,
        /// drawn like the ground: a space where a storey has no slab (open air, a
        /// stairwell), `R` a roof nobody stands on, `S` stairs (a ladder).
        /// </summary>
        public static GameState WithLevels(GameState s, string[][] levels)
        {
            s.Levels = levels.Select((rows) => new Level
            {
                Tiles = rows.SelectMany((row) => row.Select((ch) => ch == ' ' ? null : ch == 'R' ? TileKinds.Roof : (Legend.TryGetValue(ch, out var k) ? k : TileKinds.Floor))).ToList(),
            }).ToList();
            return s;
        }

        /// <summary>setup.ts makeUnit, as far as these tests need it (the facing is addUnit's to leave out).</summary>
        public static Unit MakeUnit(string id, string template, string name, int? owner, Pos pos, string? podId)
        {
            var t = Content.Templates[template];
            var u = new Unit
            {
                Id = id,
                Team = t.Team,
                Template = template,
                Name = name,
                Owner = owner,
                Pos = Grid.ToPos(pos),
                Hp = t.Hp,
                MaxHp = t.Hp,
                Armor = t.Armor,
                Ap = t.Team == Teams.Xcom ? Content.ApPerTurn : 0,
                Ammo = Content.Weapons[t.Weapon].Clip,
                Charges = new Dictionary<string, int>(t.Charges),
                Alive = true,
                PodId = podId,
                Mods = Content.ZeroMods,
                Will = t.Will,
            };
            if (t.Team == Teams.Xcom && Content.Weapons[t.Weapon].Clip > 0) u.Mags = Content.SpareMags;
            return u;
        }

        /// <summary>helpers.ts addUnit; `extra` sets what its `extra` object would (after owner and ap, as its spread does).</summary>
        public static Unit AddUnit(GameState s, string id, string template, int x, int y, int z = 0, Action<Unit>? extra = null)
        {
            // Without a facing given, the dead look all round, as before the cone (los.ts inViewCone): tests about it set one.
            var u = MakeUnit(id, template, id, null, new Pos { X = x, Y = y, Z = z }, null);
            u.Owner = u.Team == Teams.Xcom ? 0 : (int?)null;
            u.Ap = 2;
            extra?.Invoke(u);
            s.Units.Add(u);
            return u;
        }

        /// <summary>toEqual on places.</summary>
        public static void SamePlace(Pos expected, Pos? actual)
        {
            Xunit.Assert.NotNull(actual);
            Xunit.Assert.Equal((expected.X, expected.Y, expected.Z), (actual!.X, actual.Y, actual.Z));
        }

        public static void SamePlaces(IEnumerable<Pos> expected, IEnumerable<Pos>? actual)
        {
            Xunit.Assert.NotNull(actual);
            Xunit.Assert.Equal(expected.Select((p) => (p.X, p.Y, p.Z)), actual!.Select((p) => (p.X, p.Y, p.Z)));
        }

        /// <summary>toContainEqual on places.</summary>
        public static void HasPlace(IEnumerable<Pos> places, Pos p)
        {
            Xunit.Assert.Contains((p.X, p.Y, p.Z), places.Select((q) => (q.X, q.Y, q.Z)));
        }

        /// <summary>A state of a golden mission file (createMission's, by TypeScript).</summary>
        public static GameState GoldenState(string name)
        {
            return Read<GameState>(Golden(name).GetProperty("state"));
        }

        public static Pos P(int x, int y, int z = 0)
        {
            return new Pos { X = x, Y = y, Z = z };
        }

        // ------------------------------------------------------------ golden files

        /// <summary>How the tests read TypeScript's JSON: camelCase, any case, numbers as they come.</summary>
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        private static readonly Dictionary<string, JsonElement> Files = new Dictionary<string, JsonElement>();

        /// <summary>A file of golden/step1, parsed once.</summary>
        public static JsonElement Golden(string name)
        {
            lock (Files)
            {
                if (!Files.TryGetValue(name, out var root))
                {
                    root = JsonDocument.Parse(File.ReadAllText(Path.Combine("golden", "step1", name + ".json"))).RootElement;
                    Files[name] = root;
                }
                return root;
            }
        }

        public static T Read<T>(JsonElement e)
        {
            return JsonSerializer.Deserialize<T>(e.GetRawText(), Options)!;
        }

        public static int[] Ints(JsonElement e)
        {
            return e.EnumerateArray().Select((x) => x.GetInt32()).ToArray();
        }

        public static double[] Doubles(JsonElement e)
        {
            return e.EnumerateArray().Select((x) => x.GetDouble()).ToArray();
        }

        public static string Bits(IEnumerable<bool> xs)
        {
            return string.Concat(xs.Select((b) => b ? '1' : '0'));
        }
    }

    /// <summary>Collects what differs, so one run shows how many cases fail and the first few, not only the first.</summary>
    public sealed class Mismatches
    {
        private readonly string what;
        private readonly List<string> first = new List<string>();
        private int count;
        private int total;

        public Mismatches(string what)
        {
            this.what = what;
        }

        public void Check<T>(T expected, T actual, Func<string> where)
        {
            total++;
            if (EqualityComparer<T>.Default.Equals(expected, actual)) return;
            count++;
            if (first.Count < 8) first.Add($"{where()}: expected {Show(expected)}, got {Show(actual)}");
        }

        public void Seq<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, Func<string> where)
        {
            total++;
            if (expected.Count == actual.Count && expected.SequenceEqual(actual)) return;
            count++;
            if (first.Count < 8) first.Add($"{where()}: expected [{string.Join(",", expected.Take(40))}] ({expected.Count}), got [{string.Join(",", actual.Take(40))}] ({actual.Count})");
        }

        private static string Show<T>(T x)
        {
            return x is double d ? d.ToString("R") : x?.ToString() ?? "null";
        }

        public int Total => total;

        public void Assert()
        {
            if (count > 0) throw new Xunit.Sdk.XunitException($"{what}: {count} of {total} differ.\n  " + string.Join("\n  ", first));
            if (total == 0) throw new Xunit.Sdk.XunitException($"{what}: nothing was checked.");
        }
    }
}
