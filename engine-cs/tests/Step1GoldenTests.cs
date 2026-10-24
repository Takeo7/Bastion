using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bastion.Engine;
using Xunit;
using static Bastion.Engine.Tests.Step1Helpers;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// Step 1 of the first milestone (docs/UNITY.md): content, grid, decor, cover, flights, path and los answer as the
    /// TypeScript engine does on missions it made. engine-cs/golden/record-step1.ts writes golden/step1; doubles must be
    /// equal to the bit.
    /// </summary>
    public class Step1GoldenTests
    {
        public static IEnumerable<object[]> Missions => new[]
        {
            "city-1", "city-7", "wilds-1", "wilds-7", "facility-1", "facility-7", "torre", "city-5-night", "wilds-3-night",
        }.Select((n) => new object[] { n });

        private static GameState StateOf(JsonElement g)
        {
            return Read<GameState>(g.GetProperty("state"));
        }

        private static List<Unit> Alive(GameState s)
        {
            return s.Units.Where((u) => u.Alive).ToList();
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void TheStateReadsAsItWasWritten(string name)
        {
            var g = Golden(name);
            var s = StateOf(g);
            var m = new Mismatches($"{name} heights");
            var heights = Ints(g.GetProperty("heights"));
            Assert.Equal(heights.Length, s.Width * s.Height * Grid.LevelCount(s));
            for (var i = 0; i < heights.Length; i++)
            {
                var p = Grid.PosOf(s, i);
                m.Check(i, Grid.NodeOf(s, p), () => $"node {i}");
                m.Check(heights[i], Grid.HeightAt(s, p), () => $"node {i}");
            }
            m.Assert();
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void LinesOfSight(string name)
        {
            var g = Golden(name);
            var s = StateOf(g);
            var m = new Mismatches($"{name} lineClear");
            var l = Doubles(g.GetProperty("lines"));
            for (var n = 0; n < l.Length; n += 10)
            {
                var a = P((int)l[n], (int)l[n + 1], (int)l[n + 2]);
                var b = P((int)l[n + 3], (int)l[n + 4], (int)l[n + 5]);
                double? ha = l[n + 6] < 0 ? (double?)null : l[n + 6];
                double? hb = l[n + 7] < 0 ? (double?)null : l[n + 7];
                var reach = l[n + 8] == -1 ? double.PositiveInfinity : l[n + 8] == -2 ? Los.WindowSightReach : l[n + 8];
                var k = n;
                m.Check(l[n + 9] == 1, Los.LineClear(s, a, b, ha, hb, reach), () => $"line {k / 10} [{string.Join(", ", l.Skip(k).Take(9))}]");
            }
            m.Assert();
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void TheFogOfEachTeam(string name)
        {
            var g = Golden(name);
            var s = StateOf(g);
            Assert.Equal(g.GetProperty("visXcom").GetString(), Bits(Los.TeamVisibility(s, Teams.Xcom)));
            Assert.Equal(g.GetProperty("visAlien").GetString(), Bits(Los.TeamVisibility(s, Teams.Alien)));
            Assert.Equal(g.GetProperty("viewXcom").GetString(), Bits(Los.TeamView(s, Teams.Xcom)));
            Assert.Equal(g.GetProperty("viewAlien").GetString(), Bits(Los.TeamView(s, Teams.Alien)));
            // Twice: the second from teamView's cache.
            Assert.Equal(g.GetProperty("viewAlien").GetString(), Bits(Los.TeamView(s, Teams.Alien)));
            Assert.Equal(g.GetProperty("visReach").GetString(), Bits(Los.TeamVisibility(s, Teams.Xcom, 9.5)));
            var floors = g.GetProperty("floors").EnumerateArray().ToList();
            for (var f = 0; f < floors.Count; f++)
            {
                Assert.Equal(Ints(floors[f].GetProperty("levels")), Los.FloorLevels(s, f));
                Assert.Equal(floors[f].GetProperty("view").GetString(), Bits(Los.FloorView(s, Los.TeamVisibility(s, Teams.Xcom), f)));
            }
            Assert.Equal(g.GetProperty("visibleXcom").EnumerateArray().Select((e) => e.GetString()), Los.VisibleEnemies(s, Teams.Xcom).Select((u) => u.Id));
            Assert.Equal(g.GetProperty("visibleAlien").EnumerateArray().Select((e) => e.GetString()), Los.VisibleEnemies(s, Teams.Alien).Select((u) => u.Id));
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void WhoSeesWhom(string name)
        {
            var g = Golden(name);
            var s = StateOf(g);
            var alive = Alive(s);
            Assert.Equal(g.GetProperty("unitIds").EnumerateArray().Select((e) => e.GetString()), alive.Select((u) => u.Id));
            var see = g.GetProperty("canSee").GetString()!;
            var cone = g.GetProperty("inViewCone").GetString()!;
            var origin = Ints(g.GetProperty("sightOrigin"));
            var sightTo = Ints(g.GetProperty("sightTo"));
            var detection = Doubles(g.GetProperty("detectionTo"));
            var m = new Mismatches($"{name} who sees whom");
            var n = 0;
            foreach (var v in alive)
            {
                foreach (var t in alive)
                {
                    var k = n;
                    Func<string> where = () => $"{v.Id} → {t.Id} (#{k})";
                    m.Check(see[n] == '1', Los.CanSee(s, v, t), where);
                    m.Check(cone[n] == '1', Los.InViewCone(v, t.Pos), where);
                    var range = Los.SightTo(s, v, t.Pos);
                    m.Check(sightTo[n], range, where);
                    m.Check(detection[n], Los.DetectionTo(s, v, t.Pos), where);
                    var o = Los.SightOrigin(s, v.Pos, t.Pos, range);
                    m.Check(origin[n], o == null ? -1 : Grid.NodeOf(s, o), where);
                    n++;
                }
            }
            var peek = g.GetProperty("peek").EnumerateArray().ToList();
            for (var i = 0; i < alive.Count; i++)
            {
                var u = alive[i];
                m.Seq(Ints(peek[i]), Los.PeekOrigins(s, u.Pos).Select((p) => Grid.NodeOf(s, p)).ToList(), () => $"peekOrigins {u.Id}");
            }
            var c = Doubles(g.GetProperty("cones"));
            for (var i = 0; i < c.Length; i += 5)
            {
                var v = alive[(int)c[i]];
                var k = i;
                m.Check(c[i + 4] == 1, Los.InViewCone(v, P((int)c[i + 1], (int)c[i + 2]), null, c[i + 3]), () => $"cone {v.Id} [{string.Join(", ", c.Skip(k).Take(4).Select((x) => x.ToString("R")))}]");
            }
            var tiles = Ints(g.GetProperty("canSeeTile"));
            for (var i = 0; i < tiles.Length; i += 4)
            {
                var k = i;
                m.Check(tiles[i + 3] == 1, Los.CanSeeTile(s, Grid.PosOf(s, tiles[i]), Grid.PosOf(s, tiles[i + 1]), tiles[i + 2]), () => $"canSeeTile [{string.Join(", ", tiles.Skip(k).Take(3))}]");
            }
            var f = Doubles(g.GetProperty("facingTo"));
            for (var i = 0; i < f.Length; i += 5)
            {
                var k = i;
                var got = Los.FacingTo(P((int)f[i], (int)f[i + 1]), P((int)f[i + 2], (int)f[i + 3]));
                m.Check(f[i + 4], got ?? 99, () => $"facingTo [{string.Join(", ", f.Skip(k).Take(4))}]");
            }
            m.Assert();
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void LightsAtNight(string name)
        {
            var g = Golden(name);
            var s = StateOf(g);
            var m = new Mismatches($"{name} lights");
            var lights = g.GetProperty("lightTiles").EnumerateArray().ToList();
            Assert.Equal(lights.Count, s.Lights?.Count ?? 0);
            for (var i = 0; i < lights.Count; i++)
            {
                var l = s.Lights![i];
                m.Seq(Ints(lights[i]), Los.LightTiles(s, l.Tile, l.Radius, Content.Night.LampHeight), () => $"light {l.Id}");
            }
            m.Check(g.GetProperty("litMask").GetString(), Bits(Los.LitMask(s)), () => "litMask");
            var nodes = s.Width * s.Height * Grid.LevelCount(s);
            m.Check(g.GetProperty("isLit").GetString(), Bits(Enumerable.Range(0, nodes).Select((i) => Los.IsLit(s, Grid.PosOf(s, i)))), () => "isLit");
            m.Assert();
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void ReachesAndPaths(string name)
        {
            var g = Golden(name);
            var m = new Mismatches($"{name} reaches");
            foreach (var r in g.GetProperty("reaches").EnumerateArray())
            {
                var s = StateOf(g);
                if (r.GetProperty("siege").GetBoolean())
                {
                    s.Mission.Siege = new Siege { Hold = 11 };
                    s.Slow = Ints(r.GetProperty("slow")).ToList();
                }
                var id = r.GetProperty("unit").GetString()!;
                var u = s.Units.First((o) => o.Id == id);
                var maxCost = r.GetProperty("maxCost").GetDouble();
                var fromNode = r.GetProperty("from").GetInt32();
                var reach = Paths.ComputeReach(s, u, maxCost, fromNode < 0 ? null : Grid.PosOf(s, fromNode));
                var entries = new List<int>();
                foreach (var e in reach.Cost) entries.AddRange(new[] { e.Key, e.Value, reach.Prev.Get(e.Key, -1) });
                m.Seq(Ints(r.GetProperty("entries")), entries, () => $"computeReach {id} {maxCost}");
                m.Seq(Ints(r.GetProperty("dest")), Paths.Destinations(s, u, reach).SelectMany((d) => new[] { Grid.NodeOf(s, d.Pos), d.Cost }).ToList(), () => $"destinations {id}");
                foreach (var p in r.GetProperty("paths").EnumerateArray())
                {
                    var want = Ints(p);
                    var path = Paths.BuildPath(s, u, reach, Grid.PosOf(s, want[0]));
                    var got = new[] { want[0] }.Concat(path == null ? new[] { -1 } : path.Select((q) => Grid.NodeOf(s, q))).ToList();
                    m.Seq(want, got, () => $"buildPath {id} to {want[0]}");
                }
            }
            m.Assert();
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void Steps(string name)
        {
            var g = Golden(name);
            var s = StateOf(g);
            var m = new Mismatches($"{name} steps");
            var st = Ints(g.GetProperty("steps"));
            for (var i = 0; i < st.Length;)
            {
                var p = Grid.PosOf(s, st[i]);
                var d = Grid.Dirs8[st[i + 1]];
                var count = st[i + 2];
                var want = st.Skip(i + 3).Take(count * 2).ToList();
                var got = Paths.StepsFrom(s, p, d.X, d.Y).SelectMany((f) => new[] { Grid.NodeOf(s, f.To), f.Cost }).ToList();
                var k = i;
                m.Seq(want, got, () => $"stepsFrom {st[k]} dir {st[k + 1]}");
                i += 3 + count * 2;
            }
            var c = Ints(g.GetProperty("stepCost"));
            for (var i = 0; i < c.Length; i += 4)
            {
                var d = Grid.Dirs8[c[i + 2]];
                var k = i;
                m.Check(c[i + 3], Paths.StepCost(s, c[i], c[i + 1], d.X, d.Y) ?? -1, () => $"stepCost [{string.Join(", ", c.Skip(k).Take(3))}]");
            }
            var b = Ints(g.GetProperty("stepBetween"));
            for (var i = 0; i < b.Length; i += 3)
            {
                var k = i;
                m.Check(b[i + 2], Paths.StepBetween(s, Grid.PosOf(s, b[i]), Grid.PosOf(s, b[i + 1])) ?? -1, () => $"stepBetween {b[k]} → {b[k + 1]}");
            }
            m.Assert();
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void PlacesFlightsAndCover(string name)
        {
            var g = Golden(name);
            var s = StateOf(g);
            var nodes = s.Width * s.Height * Grid.LevelCount(s);
            var area = s.Width * s.Height;
            var m = new Mismatches($"{name} places");
            m.Check(g.GetProperty("canStop").GetString(), Bits(Enumerable.Range(0, nodes).Select((i) => Flights.CanStopAt(s, Grid.PosOf(s, i)))), () => "canStopAt");
            m.Check(g.GetProperty("bears").GetString(), Bits(Enumerable.Range(0, nodes).Select((i) => Flights.BearsFlight(s, Grid.PosOf(s, i)))), () => "bearsFlight");
            m.Check(g.GetProperty("cover").GetString(), string.Concat(Enumerable.Range(0, nodes).Select((i) => string.Concat(Cover.CoverSides(s, Grid.PosOf(s, i))))), () => "coverSides");
            var top = Doubles(g.GetProperty("sightTop"));
            for (var i = 0; i < area; i++)
            {
                var k = i;
                m.Check(top[i], Grid.SightTop(s, i % s.Width, i / s.Width), () => $"sightTop {k}");
            }
            m.Check(g.GetProperty("hasLongFlights").GetBoolean(), Flights.HasLongFlights(s), () => "hasLongFlights");
            // Twice: the second from its cache.
            m.Check(g.GetProperty("hasLongFlights").GetBoolean(), Flights.HasLongFlights(s), () => "hasLongFlights");
            var f = Ints(g.GetProperty("flights"));
            for (var i = 0; i < f.Length; i += 7)
            {
                var p = Grid.PosOf(s, f[i]);
                var at = Flights.FlightAt(s, p);
                var got = new[]
                {
                    f[i], at?.Step ?? -1, at?.Flight.Up ?? -1, at?.Flight.Tiles.Count ?? 0, at == null ? -1 : Grid.NodeOf(s, Flights.FlightFoot(at.Flight)),
                    at == null ? -1 : Grid.NodeOf(s, Flights.FlightLanding(at.Flight)), Grid.NodeOf(s, Flights.RestingPlace(s, p)),
                };
                var k = i;
                m.Seq(f.Skip(i).Take(7).ToList(), got, () => $"flightAt {f[k]}");
            }
            var sp = Ints(g.GetProperty("spaces"));
            for (var i = 0; i < sp.Length; i += 4)
            {
                var a = Grid.PosOf(s, sp[i]);
                var b = Grid.PosOf(s, sp[i + 1]);
                var k = i;
                m.Check(sp[i + 2] == 1, Grid.SameSpace(s, a, b), () => $"sameSpace {sp[k]} {sp[k + 1]}");
                m.Check(sp[i + 3], Grid.StoreysApart(s, a, b), () => $"storeysApart {sp[k]} {sp[k + 1]}");
            }
            var su = Ints(g.GetProperty("surfaces"));
            for (var i = 0; i < su.Length; i += 4)
            {
                var surface = Grid.SurfaceAt(s, su[i], su[i + 1], su[i + 2]);
                var k = i;
                m.Check(su[i + 3], surface == null ? -1 : Grid.NodeOf(s, surface), () => $"surfaceAt [{string.Join(", ", su.Skip(k).Take(3))}]");
            }
            var si = Doubles(g.GetProperty("sights"));
            for (var i = 0; i < si.Length; i += 6)
            {
                int x = (int)si[i], y = (int)si[i + 1];
                var h = si[i + 2];
                var roof = Grid.RoofSide(s, x, y, h);
                Func<string> where = () => $"({x}, {y}) at {h:R}";
                m.Check(si[i + 3] == 1, Grid.BlocksSightAt(s, x, y, h), where);
                m.Check(si[i + 4] == 1, Grid.OpaqueAtIndex(s, y * s.Width + x, h), where);
                m.Check((int)si[i + 5], roof == null ? 0 : roof == Grid.RoofSides.Under ? 1 : 2, where);
            }
            var ca = Ints(g.GetProperty("coverAgainst"));
            for (var i = 0; i < ca.Length; i += 4)
            {
                var k = i;
                m.Check(ca[i + 3], Cover.CoverAgainst(s, Grid.PosOf(s, ca[i]), new Vec2 { X = ca[i + 1], Y = ca[i + 2] }), () => $"coverAgainst [{string.Join(", ", ca.Skip(k).Take(3))}]");
            }
            m.Assert();
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void DecorObjects(string name)
        {
            var g = Golden(name);
            var s = StateOf(g);
            var m = new Mismatches($"{name} decor");
            var decors = new List<List<int>?> { s.Decor };
            decors.AddRange((s.Levels ?? new List<Level>()).Select((l) => l.Decor));
            var golden = g.GetProperty("decor").EnumerateArray().ToList();
            Assert.Equal(golden.Count, decors.Count);
            for (var z = 0; z < decors.Count; z++)
            {
                var decor = decors[z];
                if (decor == null)
                {
                    Assert.Equal(JsonValueKind.Null, golden[z].ValueKind);
                    continue;
                }
                var anchors = new List<int>();
                var objects = new List<int>();
                var cars = new List<int>();
                for (var i = 0; i < decor.Count; i++)
                {
                    anchors.Add(Decor.AnchorOf(decor, s.Width, i));
                    var o = Decor.ObjectAt(decor, s.Width, i);
                    if (o != null)
                    {
                        objects.AddRange(new[] { i, Decor.DecorCode(o.Kind, o.Facing), o.Anchor, o.Tiles.Count });
                        objects.AddRange(o.Tiles);
                    }
                    var c = Decor.CarPartner(decor, s.Width, i);
                    if (c >= 0) cars.AddRange(new[] { i, c });
                }
                var level = z;
                m.Seq(Ints(golden[z].GetProperty("anchors")), anchors, () => $"anchorOf, level {level}");
                m.Seq(Ints(golden[z].GetProperty("objects")), objects, () => $"objectAt, level {level}");
                m.Seq(Ints(golden[z].GetProperty("cars")), cars, () => $"carPartner, level {level}");
            }
            m.Assert();
        }

        // ------------------------------------------------------------ JavaScript's Math

        [Fact]
        public void Atan2HypotAndRoundAsV8()
        {
            var g = Golden("math");
            var m = new Mismatches("Math");
            var a = Doubles(g.GetProperty("atan2"));
            for (var i = 0; i < a.Length; i += 3)
            {
                var k = i;
                m.Check(a[i + 2], Js.Atan2(a[i], a[i + 1]), () => $"atan2({a[k]:R}, {a[k + 1]:R})");
            }
            var h = Doubles(g.GetProperty("hypot"));
            for (var i = 0; i < h.Length; i += 3)
            {
                var k = i;
                m.Check(h[i + 2], Js.Hypot(h[i], h[i + 1]), () => $"hypot({h[k]:R}, {h[k + 1]:R})");
            }
            var r = Doubles(g.GetProperty("round"));
            for (var i = 0; i < r.Length; i += 2)
            {
                var k = i;
                m.Check(r[i + 1], Js.Round(r[i]), () => $"round({r[k]:R})");
            }
            m.Check(g.GetProperty("coverMinDot").GetDouble(), Cover.CoverMinDot, () => "COVER_MIN_DOT");
            m.Assert();
        }

        [Fact]
        public void Atan2OfSignedZerosAsJavaScript()
        {
            // JSON writes -0 as 0, so these aren't in math.json.
            Assert.Equal(Math.PI, Js.Atan2(0.0, -1));
            Assert.Equal(-Math.PI, Js.Atan2(-0.0, -1));
            Assert.True(double.IsNegative(Js.Atan2(-0.0, 1)));
            Assert.Equal(Math.PI / 2, Js.Atan2(1, 0));
            Assert.Equal(-Math.PI / 2, Js.Atan2(-1, -0.0));
        }

        [Fact]
        public void TheEmulatedMultiplyAddIsTheHardwareOne()
        {
            // Js.FusedMultiplyAdd stands in for Math.FusedMultiplyAdd, which .NET Standard 2.1 lacks: the same, rounded once.
            var r = new Random(5);
            var m = new Mismatches("FusedMultiplyAdd");
            for (var n = 0; n < 200000; n++)
            {
                var a = (r.NextDouble() - 0.5) * Math.Pow(10, r.Next(-12, 12));
                var b = (r.NextDouble() - 0.5) * Math.Pow(10, r.Next(-12, 12));
                var c = n % 3 == 0 ? -a * b * (1 + (r.NextDouble() - 0.5) * 1e-12) : (r.NextDouble() - 0.5) * Math.Pow(10, r.Next(-24, 24));
                m.Check(Math.FusedMultiplyAdd(a, b, c), Js.FusedMultiplyAdd(a, b, c), () => $"fma({a:R}, {b:R}, {c:R})");
            }
            m.Assert();
        }

        // ------------------------------------------------------------ content.ts

        /// <summary>Whether two JSON values are the same: objects by their keys in any order, numbers as doubles.</summary>
        private static void SameJson(JsonElement expected, JsonElement actual, string path, List<string> out_)
        {
            if (expected.ValueKind != actual.ValueKind && !(IsBool(expected) && IsBool(actual)))
            {
                out_.Add($"{path}: expected {expected}, got {actual}");
                return;
            }
            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    var e = expected.EnumerateObject().ToDictionary((p) => p.Name, (p) => p.Value);
                    var a = actual.EnumerateObject().ToDictionary((p) => p.Name, (p) => p.Value);
                    foreach (var k in e.Keys.Union(a.Keys))
                    {
                        if (!e.ContainsKey(k)) out_.Add($"{path}.{k}: not in TypeScript, {a[k]} in C#");
                        else if (!a.ContainsKey(k)) out_.Add($"{path}.{k}: {e[k]} in TypeScript, missing in C#");
                        else SameJson(e[k], a[k], $"{path}.{k}", out_);
                    }
                    break;
                case JsonValueKind.Array:
                    var ea = expected.EnumerateArray().ToList();
                    var aa = actual.EnumerateArray().ToList();
                    if (ea.Count != aa.Count) out_.Add($"{path}: {ea.Count} elements in TypeScript, {aa.Count} in C#");
                    else for (var i = 0; i < ea.Count; i++) SameJson(ea[i], aa[i], $"{path}[{i}]", out_);
                    break;
                case JsonValueKind.Number:
                    if (expected.GetDouble() != actual.GetDouble()) out_.Add($"{path}: expected {expected}, got {actual}");
                    break;
                case JsonValueKind.String:
                    if (expected.GetString() != actual.GetString()) out_.Add($"{path}: expected {expected}, got {actual}");
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (expected.GetBoolean() != actual.GetBoolean()) out_.Add($"{path}: expected {expected}, got {actual}");
                    break;
            }
        }

        private static bool IsBool(JsonElement e)
        {
            return e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False;
        }

        private static JsonElement AsJson(object value)
        {
            return JsonDocument.Parse(JsonSerializer.Serialize(value, Options)).RootElement;
        }

        [Fact]
        public void TheTablesOfContentTs()
        {
            var g = Golden("content");
            var t = g.GetProperty("tables");
            var items = JsonNode.Parse(JsonSerializer.Serialize(Content.Items, Options))!.AsObject();
            // ITEMS' mods are Partial<UnitMods>: what an item doesn't name is 0 in C#.
            foreach (var item in items)
            {
                var mods = item.Value!["mods"]?.AsObject();
                if (mods == null) continue;
                foreach (var k in mods.Where((p) => p.Value!.GetValue<int>() == 0).Select((p) => p.Key).ToList()) mods.Remove(k);
            }
            var tables = new Dictionary<string, JsonElement>
            {
                ["templates"] = AsJson(Content.Templates),
                ["weapons"] = AsJson(Content.Weapons),
                ["abilities"] = AsJson(Content.Abilities),
                ["items"] = JsonDocument.Parse(items.ToJsonString()).RootElement,
                ["foundWeapons"] = AsJson(Content.FoundWeapons),
                ["weaponMods"] = AsJson(Content.WeaponMods),
                ["plainWeapon"] = AsJson(Content.PlainWeapon),
                ["rangeTables"] = AsJson(Content.RangeTables),
                ["infectionBite"] = AsJson(Content.Infection.Bite),
                ["soldierNames"] = AsJson(Content.SoldierNames),
                ["playerColors"] = AsJson(Content.PlayerColors),
                ["thrown"] = AsJson(Content.Thrown.OrderBy((x) => x, StringComparer.Ordinal)),
                ["zeroMods"] = AsJson(Content.ZeroMods),
            };
            var diffs = new List<string>();
            foreach (var p in t.EnumerateObject())
            {
                var expected = p.Name == "thrown" ? AsJson(p.Value.EnumerateArray().Select((x) => x.GetString()).OrderBy((x) => x, StringComparer.Ordinal)) : p.Value;
                SameJson(expected, tables[p.Name], p.Name, diffs);
                // The tables by id keep TypeScript's order.
                if (p.Value.ValueKind == JsonValueKind.Object) Assert.Equal(p.Value.EnumerateObject().Select((e) => e.Name), tables[p.Name].EnumerateObject().Select((e) => e.Name));
            }
            var constants = new Dictionary<string, object>
            {
                ["apPerTurn"] = Content.ApPerTurn, ["thirdActionRank"] = Content.ThirdActionRank, ["spareMags"] = Content.SpareMags,
                ["fallDamage"] = Content.FallDamage, ["lowCoverBonus"] = Content.LowCoverBonus, ["highCoverBonus"] = Content.HighCoverBonus,
                ["hunkerDefense"] = Content.HunkerDefense, ["hunkerDodge"] = Content.HunkerDodge, ["reactionPenalty"] = Content.ReactionPenalty,
                ["grazeDamageMult"] = Content.GrazeDamageMult, ["squadsightDistanceMod"] = Content.SquadsightDistanceMod,
                ["squadsightCritMod"] = Content.SquadsightCritMod, ["medkitHeal"] = Content.MedkitHeal, ["stoneDamage"] = Content.StoneDamage,
                ["heightAdvantageAim"] = Content.HeightAdvantageAim, ["coverMaxAngleDeg"] = Content.CoverMaxAngleDeg,
                ["recoveryTurns"] = Content.RecoveryTurns, ["hackBase"] = Content.HackBase, ["psiBase"] = Content.PsiBase,
                ["disorientedAim"] = Content.DisorientedAim, ["suppressedAim"] = Content.SuppressedAim, ["holoAim"] = Content.HoloAim,
                ["ruptureDamage"] = Content.RuptureDamage, ["controlTurns"] = Content.ControlTurns, ["restorationHeal"] = Content.RestorationHeal,
                ["smokeDefense"] = Content.SmokeDefense, ["smokeTurns"] = Content.SmokeTurns, ["backpackSlots"] = Content.BackpackSlots,
                ["medicineHeal"] = Content.MedicineHeal, ["pistolClip"] = Content.PistolClip, ["modSlots"] = Content.ModSlots,
                ["modAim"] = Content.ModAim, ["extMag"] = Content.ExtMag, ["wornBelow"] = Content.WornBelow, ["wornAim"] = Content.WornAim,
                ["repairPerPart"] = Content.RepairPerPart, ["patchUp"] = Content.PatchUp, ["soldierMobility"] = Content.SoldierMobility,
                ["zombiePace"] = Content.ZombiePace, ["squadSize"] = Content.SquadSize,
                ["noise"] = new { door = Content.Noise.Door, breach = Content.Noise.Breach, dash = Content.Noise.Dash, alarm = Content.Noise.Alarm },
                ["traps"] = new { mine = Content.Traps.Mine, mineRadius = Content.Traps.MineRadius, spikes = Content.Traps.Spikes, wire = Content.Traps.Wire },
                ["siege"] = new { door = Content.Siege.Door, barricade = Content.Siege.Barricade, smashNoise = Content.Siege.SmashNoise, mags = Content.Siege.Mags, hold = Content.Siege.Hold },
                ["horde"] = new { perTurn = Content.Horde.PerTurn, perNoise = Content.Horde.PerNoise, wave = Content.Horde.Wave, cap = Content.Horde.Cap, trickle = Content.Horde.Trickle },
                ["night"] = new
                {
                    sightCut = Content.Night.SightCut, minSight = Content.Night.MinSight, detection = Content.Night.Detection,
                    streetLight = Content.Night.StreetLight, floodlight = Content.Night.Floodlight, lampHeight = Content.Night.LampHeight,
                    hordeHead = Content.Night.HordeHead, supplies = Content.Night.Supplies,
                },
                ["infectionBurst"] = Content.Infection.Burst, ["infectionVest"] = Content.Infection.Vest,
            };
            SameJson(g.GetProperty("constants"), AsJson(constants), "constants", diffs);
            Assert.True(diffs.Count == 0, string.Join("\n", diffs.Take(30)));
        }

        [Fact]
        public void TheFunctionsOfContentTs()
        {
            var g = Golden("content");
            var m = new Mismatches("content.ts");
            var ids = g.GetProperty("templateIds").EnumerateArray().Select((e) => e.GetString()!).ToList();
            var hostile = g.GetProperty("hostile").GetString()!;
            var n = 0;
            foreach (var a in ids)
            {
                foreach (var b in ids)
                {
                    var k = n++;
                    m.Check(hostile[k] == '1', Content.HostileTo(Content.Templates[a].Team, a, Content.Templates[b].Team, b), () => $"hostileTo {a} {b}");
                }
            }
            var names = g.GetProperty("names").EnumerateArray().Select((e) => e.GetString()!).ToList();
            var sexes = g.GetProperty("sexes").EnumerateArray().Select((e) => e.ValueKind == JsonValueKind.Null ? null : e.GetString()).ToList();
            for (var i = 0; i < names.Count; i++)
            {
                var k = i;
                m.Check(sexes[i], Content.SexOfName(names[i]), () => $"sexOfName «{names[k]}»");
            }
            foreach (var r in g.GetProperty("ranges").EnumerateObject())
            {
                var want = Ints(r.Value);
                for (var i = 0; i < want.Length; i++)
                {
                    var k = i;
                    m.Check(want[i], Content.RangeModifier(r.Name, i * 0.5), () => $"rangeModifier {r.Name} {k * 0.5}");
                }
            }
            var points = Ints(g.GetProperty("hordePoints"));
            for (var i = 0; i < points.Length; i++)
            {
                var k = i;
                m.Check(points[i], Content.HordePoints(i * 0.75), () => $"hordePoints {k * 0.75}");
            }
            var wear = Ints(g.GetProperty("wear"));
            var weapons = g.GetProperty("tables").GetProperty("weapons").EnumerateObject().Select((p) => p.Name).ToList();
            for (var i = 0; i < weapons.Count; i++)
            {
                var k = i;
                m.Check(wear[i], Content.WearOf(weapons[i]), () => $"wearOf {weapons[k]}");
            }
            var units = Read<List<Unit>>(g.GetProperty("units"));
            var attacks = g.GetProperty("attacks").EnumerateArray().Select((e) => e.GetString()!).ToList();
            var abilities = g.GetProperty("tables").GetProperty("abilities").EnumerateObject().Select((p) => p.Name).ToList();
            var fires = g.GetProperty("fires").EnumerateArray().ToList();
            for (var i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var f = fires[i];
                m.Seq(f.GetProperty("weapons").EnumerateArray().Select((e) => e.GetString()!).ToList(), attacks.Select((a) => Content.AttackWeapon(u, a).Id).ToList(), () => $"attackWeapon {u.Id}");
                m.Seq(Ints(f.GetProperty("clips")), attacks.Select((a) => Content.ClipOf(u, a)).ToList(), () => $"clipOf {u.Id}");
                m.Seq(f.GetProperty("sidearm").EnumerateArray().Select((e) => e.GetBoolean()).ToList(), attacks.Select((a) => Content.FiresSidearm(u, a)).ToList(), () => $"firesSidearm {u.Id}");
                m.Seq(Ints(f.GetProperty("rounds")), attacks.Select((a) => Content.RoundsOf(u, a)).ToList(), () => $"roundsOf {u.Id}");
                m.Seq(f.GetProperty("loaded").EnumerateArray().Select((e) => e.GetBoolean()).ToList(), attacks.Select((a) => Content.LoadedFor(u, a)).ToList(), () => $"loadedFor {u.Id}");
                m.Seq(f.GetProperty("primary").EnumerateArray().Select((e) => e.GetBoolean()).ToList(), abilities.Select((a) => Content.FiresPrimary(u, a)).ToList(), () => $"firesPrimary {u.Id}");
                m.Check(f.GetProperty("condition").GetDouble(), Content.ConditionOf(u), () => $"conditionOf {u.Id}");
                m.Check(f.GetProperty("fullyLoaded").GetBoolean(), Content.FullyLoaded(u), () => $"fullyLoaded {u.Id}");
                m.Check(f.GetProperty("squadsight").GetBoolean(), Content.HasSquadsight(u), () => $"hasSquadsight {u.Id}");
                m.Check(f.GetProperty("apPerTurn").GetInt32(), Content.ApPerTurnOf(u), () => $"apPerTurn {u.Id}");
                var v = Read<Unit>(g.GetProperty("units")[i]);
                Content.ReloadWeapon(v);
                var re = f.GetProperty("reloaded");
                m.Check(re[0].GetInt32(), v.Ammo, () => $"reloadWeapon {u.Id} ammo");
                m.Check(re[1].ValueKind == JsonValueKind.Null ? (int?)null : re[1].GetInt32(), v.PistolAmmo, () => $"reloadWeapon {u.Id} pistolAmmo");
            }
            var commands = Ints(g.GetProperty("commandsFor"));
            var c = 0;
            foreach (var slot in new[] { 0, 1 })
            {
                foreach (var owner in new int?[] { null, 0, 1 })
                {
                    foreach (var connected in new[] { null, new int[0], new[] { 0 }, new[] { 1 }, new[] { 0, 1 } })
                    {
                        var k = c++;
                        m.Check(commands[k] == 1, Content.CommandsFor(slot, owner, connected), () => $"commandsFor {slot} {owner} [{string.Join(",", connected ?? new int[0])}]");
                    }
                }
            }
            m.Assert();
        }
    }
}
