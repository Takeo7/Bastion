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
    /// golden/step1 and rng.json recorded with C#: record-step1.ts in C# (Step1GoldenTests reads them). Each mission made
    /// again by Setup.CreateMission, and on it, sampled with the same seeded Rng in the same order, the inputs and what C#
    /// answers. math.json keeps its numbers and takes C#'s answers (Js.cs); content.json, the tables and functions of
    /// Content (its units, made by hand in TypeScript, stay).
    /// </summary>
    public class Step1Recorder
    {
        private readonly ITestOutputHelper output;

        public Step1Recorder(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>record-step1.ts's missions: `createMission({ map, slots: [0, 1], rng: new Rng(seed), night })`.</summary>
        private static readonly (string Name, MapSource Map, int Seed, bool Night)[] Missions =
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

        [RecorderFact]
        public void Step1()
        {
            foreach (var m in Missions)
            {
                var file = $"step1/{m.Name}.json";
                var (text, raw) = Recording.Load(file);
                var norm = Recording.Node(raw)!.AsObject();
                norm["state"] = Recording.Normal<GameState>(raw.GetProperty("state"));
                var fresh = Mission(m.Name, Setup.CreateMission(new MissionOptions { Map = m.Map, Slots = new List<int> { 0, 1 }, Rng = new Rng(m.Seed), Night = m.Night }));
                output.WriteLine(Recording.Report(file, Recording.Write(file, text, raw, norm, fresh)));
            }
            MathJson();
            ContentJson();
            RngJson();
        }

        // ------------------------------------------------------------ a mission

        private static string Bits(IEnumerable<bool> xs) => string.Concat(xs.Select((b) => b ? '1' : '0'));

        private static int PosNode(GameState s, Pos? p) => p == null ? -1 : Grid.NodeOf(s, p);

        private static JsonArray Numbers(IEnumerable<double> xs) => new JsonArray(xs.Select((x) => (JsonNode)JsonValue.Create(x)).ToArray());

        private static JsonArray Numbers(IEnumerable<int> xs) => new JsonArray(xs.Select((x) => (JsonNode)JsonValue.Create(x)).ToArray());

        private static Pos Copy(Pos p) => new Pos { X = p.X, Y = p.Y, Z = p.Z };

        /// <summary>A random place: a tile, then one of the places on it (the ground or a storey with a slab).</summary>
        private static Pos RandomPlace(GameState s, Rng r, Pos? near = null, int span = 12)
        {
            var x = near != null ? Math.Max(0, Math.Min(s.Width - 1, near.X + r.Range(-span, span))) : r.Int(s.Width);
            var y = near != null ? Math.Max(0, Math.Min(s.Height - 1, near.Y + r.Range(-span, span))) : r.Int(s.Height);
            return r.Pick(Grid.PlacesOn(s, x, y));
        }

        private static JsonObject Mission(string name, GameState s)
        {
            var r = new Rng(12345);
            var area = s.Width * s.Height;
            var nodes = area * Grid.LevelCount(s);
            var g = new JsonObject { ["name"] = name };
            // The state as it is before anything below runs (teamView and the rest only read it).
            g["state"] = Recording.Node(s);
            var alive = s.Units.Where((u) => u.Alive).ToList();

            // [ax, ay, az, bx, by, bz, ha (-1 default), hb (-1 default), reach (-1 Infinity, -2 default), clear]
            var lines = new List<double>();
            for (var n = 0; n < 2000; n++)
            {
                var a = n % 4 == 0 ? RandomPlace(s, r) : r.Pick(alive).Pos;
                var b = n % 3 == 0 ? RandomPlace(s, r) : n % 7 == 0 ? Copy(a) : RandomPlace(s, r, a);
                var heights = n % 2 == 1;
                var ha = heights ? Js.Round(r.Next() * 3500) / 1000 : -1;
                var hb = heights ? (n % 5 == 0 ? r.Next() * 3.2 : Js.Round(r.Next() * 3500) / 1000) : -1;
                var reach = n % 11 == 0 ? -1 : n % 13 == 0 ? r.Int(3) : -2;
                var clear = Los.LineClear(s, a, b, ha < 0 ? (double?)null : ha, hb < 0 ? (double?)null : hb, reach == -1 ? double.PositiveInfinity : reach == -2 ? Los.WindowSightReach : reach);
                lines.AddRange(new double[] { a.X, a.Y, a.Z, b.X, b.Y, b.Z, ha, hb, reach, clear ? 1 : 0 });
            }
            g["lines"] = Numbers(lines);

            // The fog.
            g["visXcom"] = Bits(Los.TeamVisibility(s, Teams.Xcom));
            g["visAlien"] = Bits(Los.TeamVisibility(s, Teams.Alien));
            g["viewXcom"] = Bits(Los.TeamView(s, Teams.Xcom));
            g["viewAlien"] = Bits(Los.TeamView(s, Teams.Alien));
            g["visReach"] = Bits(Los.TeamVisibility(s, Teams.Xcom, 9.5));
            g["floors"] = new JsonArray(Enumerable.Range(0, Grid.LevelCount(s) + 1).Select((f) => (JsonNode)new JsonObject
            {
                ["levels"] = Numbers(Los.FloorLevels(s, f)),
                ["view"] = Bits(Los.FloorView(s, Los.TeamVisibility(s, Teams.Xcom), f)),
            }).ToArray());
            g["visibleXcom"] = new JsonArray(Los.VisibleEnemies(s, Teams.Xcom).Select((u) => (JsonNode)u.Id).ToArray());
            g["visibleAlien"] = new JsonArray(Los.VisibleEnemies(s, Teams.Alien).Select((u) => (JsonNode)u.Id).ToArray());

            // Who sees whom, by ordered pair of living units (viewer, target).
            var see = new List<bool>();
            var cone = new List<bool>();
            var origin = new List<int>();
            var sightRange = new List<int>();
            var detection = new List<double>();
            foreach (var v in alive)
            {
                foreach (var t in alive)
                {
                    see.Add(Los.CanSee(s, v, t));
                    cone.Add(Los.InViewCone(v, t.Pos));
                    var range = Los.SightTo(s, v, t.Pos);
                    sightRange.Add(range);
                    detection.Add(Los.DetectionTo(s, v, t.Pos));
                    origin.Add(PosNode(s, Los.SightOrigin(s, v.Pos, t.Pos, range)));
                }
            }
            g["unitIds"] = new JsonArray(alive.Select((u) => (JsonNode)u.Id).ToArray());
            g["canSee"] = Bits(see);
            g["inViewCone"] = Bits(cone);
            g["sightOrigin"] = Numbers(origin);
            g["sightTo"] = Numbers(sightRange);
            g["detectionTo"] = Numbers(detection);
            g["peek"] = new JsonArray(alive.Select((u) => (JsonNode)Numbers(Los.PeekOrigins(s, u.Pos).Select((p) => Grid.NodeOf(s, p)))).ToArray());
            // A facing given (a patrol on its way): [viewer index, x, y, facing, in cone]
            var cones = new List<double>();
            for (var n = 0; n < 1500; n++)
            {
                var v = r.Pick(alive);
                var p = RandomPlace(s, r, v.Pos, 6);
                var facing = n % 3 == 0 ? r.Int(8) * Math.PI / 4 : r.Next() * 7 - 0.5;
                cones.AddRange(new[] { alive.IndexOf(v), p.X, p.Y, facing, Los.InViewCone(v, p, null, facing) ? 1 : 0 });
            }
            g["cones"] = Numbers(cones);
            // canSeeTile: [from node, to node, range, seen]
            var tiles = new List<int>();
            for (var n = 0; n < 1000; n++)
            {
                var from = r.Pick(alive).Pos;
                var to = RandomPlace(s, r, from, 16);
                var range = r.Range(4, 20);
                tiles.AddRange(new[] { Grid.NodeOf(s, from), Grid.NodeOf(s, to), range, Los.CanSeeTile(s, from, to, range) ? 1 : 0 });
            }
            g["canSeeTile"] = Numbers(tiles);
            var facings = new List<double>();
            for (var n = 0; n < 500; n++)
            {
                var a = RandomPlace(s, r);
                var b = n % 9 == 0 ? a : RandomPlace(s, r, a);
                facings.AddRange(new[] { a.X, a.Y, b.X, b.Y, Los.FacingTo(a, b) ?? 99 });
            }
            g["facingTo"] = Numbers(facings);

            // Night.
            g["lightTiles"] = new JsonArray((s.Lights ?? new List<Light>()).Select((l) => (JsonNode)Numbers(Los.LightTiles(s, l.Tile, l.Radius, Content.Night.LampHeight))).ToArray());
            g["litMask"] = Bits(Los.LitMask(s));
            g["isLit"] = Bits(Enumerable.Range(0, nodes).Select((i) => Los.IsLit(s, Grid.PosOf(s, i))));

            // Reaches and paths.
            var xcom = alive.Where((u) => u.Team == Teams.Xcom).ToList();
            var enemies = alive.Where((u) => u.Team != Teams.Xcom).ToList();
            var walkers = new List<(Unit U, double MaxCost, Pos? From, bool Siege, List<int>? Slow)>();
            walkers.AddRange(xcom.Take(3).Select((u) => (u, Paths.MaxMoveCost(u), (Pos?)null, false, (List<int>?)null)));
            walkers.AddRange(enemies.Take(4).Select((u) =>
            {
                var two = Read<Unit>(Recording.Element(Recording.Node(u)));
                two.Ap = 2;
                return (u, Paths.MaxMoveCost(two), (Pos?)null, false, (List<int>?)null);
            }));
            walkers.Add((xcom[0], 400, null, false, null));
            walkers.Add((enemies[enemies.Count - 1], 120, null, false, null));
            walkers.Add((xcom[1], 60, RandomPlace(s, r, xcom[1].Pos, 6), false, null));
            var zombie = enemies.FirstOrDefault((u) => Content.Templates[u.Template].Undead);
            if (zombie != null)
            {
                var slow = Enumerable.Range(0, 40).Select((_) => Grid.NodeOf(s, RandomPlace(s, r, zombie.Pos, 8))).ToList();
                walkers.Add((zombie, 80, null, true, slow));
            }
            g["reaches"] = new JsonArray(walkers.Select((walker) =>
            {
                var t = s;
                if (walker.Siege)
                {
                    t = Read<GameState>(Recording.Element(Recording.Node(s)));
                    t.Mission.Siege = new Siege { Hold = 11 };
                    t.Slow = walker.Slow;
                }
                var w = t.Units.First((o) => o.Id == walker.U.Id);
                var reach = Paths.ComputeReach(t, w, walker.MaxCost, walker.From);
                var entries = new List<int>();
                foreach (var e in reach.Cost) entries.AddRange(new[] { e.Key, e.Value, reach.Prev.Get(e.Key, -1) });
                var dest = Paths.Destinations(t, w, reach).SelectMany((d) => new[] { Grid.NodeOf(t, d.Pos), d.Cost });
                var keys = reach.Cost.Select((e) => e.Key).ToList();
                var paths = new JsonArray();
                for (var n = 0; n < 30; n++)
                {
                    var to = n % 3 == 0 ? RandomPlace(t, r, w.Pos, 10) : Grid.PosOf(t, r.Pick(keys));
                    var path = Paths.BuildPath(t, w, reach, to);
                    paths.Add(Numbers(new[] { Grid.NodeOf(t, to) }.Concat(path == null ? new[] { -1 } : path.Select((p) => Grid.NodeOf(t, p)))));
                }
                return (JsonNode)new JsonObject
                {
                    ["unit"] = walker.U.Id,
                    ["maxCost"] = walker.MaxCost,
                    ["from"] = walker.From != null ? Grid.NodeOf(s, walker.From) : -1,
                    ["siege"] = walker.Siege,
                    ["slow"] = Numbers(walker.Slow ?? new List<int>()),
                    ["entries"] = Numbers(entries),
                    ["dest"] = Numbers(dest),
                    ["paths"] = paths,
                };
            }).ToArray());

            // stepsFrom: [node, dir (DIRS8), count, to, cost, …]
            var steps = new List<int>();
            for (var n = 0; n < 3000; n++)
            {
                var p = n % 2 != 0 ? RandomPlace(s, r) : Grid.PosOf(s, r.Int(nodes));
                var d = r.Int(8);
                var found = Paths.StepsFrom(s, p, Grid.Dirs8[d].X, Grid.Dirs8[d].Y);
                steps.AddRange(new[] { Grid.NodeOf(s, p), d, found.Count });
                foreach (var f in found) steps.AddRange(new[] { Grid.NodeOf(s, f.To), f.Cost });
            }
            g["steps"] = Numbers(steps);
            // stepCost on the ground: [x, y, dir, cost (-1 null)]
            var costs = new List<int>();
            for (var n = 0; n < 2000; n++)
            {
                var x = r.Int(s.Width);
                var y = r.Int(s.Height);
                var d = r.Int(8);
                costs.AddRange(new[] { x, y, d, Paths.StepCost(s, x, y, Grid.Dirs8[d].X, Grid.Dirs8[d].Y) ?? -1 });
            }
            g["stepCost"] = Numbers(costs);
            // stepBetween: [from node, to node, cost (-1 null)]
            var between = new List<int>();
            for (var n = 0; n < 2000; n++)
            {
                var a = RandomPlace(s, r);
                var b = RandomPlace(s, r, a, 1);
                between.AddRange(new[] { Grid.NodeOf(s, a), Grid.NodeOf(s, b), Paths.StepBetween(s, a, b) ?? -1 });
            }
            g["stepBetween"] = Numbers(between);

            // Places.
            g["canStop"] = Bits(Enumerable.Range(0, nodes).Select((i) => Flights.CanStopAt(s, Grid.PosOf(s, i))));
            g["bears"] = Bits(Enumerable.Range(0, nodes).Select((i) => Flights.BearsFlight(s, Grid.PosOf(s, i))));
            g["cover"] = string.Concat(Enumerable.Range(0, nodes).Select((i) => string.Concat(Cover.CoverSides(s, Grid.PosOf(s, i)))));
            g["sightTop"] = Numbers(Enumerable.Range(0, area).Select((i) => Grid.SightTop(s, i % s.Width, i / s.Width)));
            g["hasLongFlights"] = Flights.HasLongFlights(s);
            // Every ladder place: [node, step (-1 none), up, tiles, foot node, landing node, resting node]
            var flights = new List<int>();
            for (var i = 0; i < nodes; i++)
            {
                var p = Grid.PosOf(s, i);
                if (Grid.KindAt(s, p) != TileKinds.Ladder) continue;
                var at = Flights.FlightAt(s, p);
                flights.AddRange(new[]
                {
                    i, at?.Step ?? -1, at?.Flight.Up ?? -1, at?.Flight.Tiles.Count ?? 0, at == null ? -1 : Grid.NodeOf(s, Flights.FlightFoot(at.Flight)),
                    at == null ? -1 : Grid.NodeOf(s, Flights.FlightLanding(at.Flight)), Grid.NodeOf(s, Flights.RestingPlace(s, p)),
                });
            }
            g["flights"] = Numbers(flights);
            // [a node, b node, sameSpace, storeysApart]
            var spaces = new List<int>();
            for (var n = 0; n < 1500; n++)
            {
                var a = RandomPlace(s, r);
                var b = RandomPlace(s, r, a, 2);
                spaces.AddRange(new[] { Grid.NodeOf(s, a), Grid.NodeOf(s, b), Grid.SameSpace(s, a, b) ? 1 : 0, Grid.StoreysApart(s, a, b) });
            }
            g["spaces"] = Numbers(spaces);
            // [x, y, h, surface node (-1 null)]
            var surfaces = new List<int>();
            for (var n = 0; n < 1500; n++)
            {
                var x = r.Range(-1, s.Width);
                var y = r.Range(-1, s.Height);
                var h = r.Range(-1, 4);
                surfaces.AddRange(new[] { x, y, h, PosNode(s, Grid.SurfaceAt(s, x, y, h)) });
            }
            g["surfaces"] = Numbers(surfaces);
            // [x, y, h, blocksSightAt, opaqueAtIndex (in bounds), roofSide (0 null, 1 under, 2 over)]
            var sights = new List<double>();
            for (var n = 0; n < 3000; n++)
            {
                var x = r.Int(s.Width);
                var y = r.Int(s.Height);
                var h = n % 4 != 0 ? r.Next() * 3.8 : r.Int(8) / 2.0;
                var roof = Grid.RoofSide(s, x, y, h);
                sights.AddRange(new[] { x, y, h, Grid.BlocksSightAt(s, x, y, h) ? 1 : 0, Grid.OpaqueAtIndex(s, y * s.Width + x, h) ? 1 : 0, roof == null ? 0 : roof == Grid.RoofSides.Under ? 1 : 2 });
            }
            g["sights"] = Numbers(sights);
            // coverAgainst: [target node, attacker x, attacker y, cover]
            var against = new List<int>();
            for (var n = 0; n < 3000; n++)
            {
                var t = n % 2 != 0 ? r.Pick(alive).Pos : RandomPlace(s, r);
                Vec2 a;
                if (n % 5 == 0) a = new Vec2 { X = t.X, Y = t.Y };
                else
                {
                    var ax = t.X + r.Range(-15, 15);
                    var ay = t.Y + r.Range(-15, 15);
                    a = new Vec2 { X = ax, Y = ay };
                }
                against.AddRange(new[] { Grid.NodeOf(s, t), a.X, a.Y, Cover.CoverAgainst(s, t, a) });
            }
            g["coverAgainst"] = Numbers(against);

            // Decor.
            var decors = new List<List<int>?> { s.Decor };
            decors.AddRange((s.Levels ?? new List<Level>()).Select((l) => l.Decor));
            g["decor"] = new JsonArray(decors.Select((decor) =>
            {
                if (decor == null) return (JsonNode?)null;
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
                return new JsonObject { ["anchors"] = Numbers(anchors), ["objects"] = Numbers(objects), ["cars"] = Numbers(cars) };
            }).ToArray());
            // heightAt of every place, a check on nodeOf and posOf.
            g["heights"] = Numbers(Enumerable.Range(0, nodes).Select((i) => Grid.HeightAt(s, Grid.PosOf(s, i))));
            return g;
        }

        // ------------------------------------------------------------ JavaScript's Math, content.ts, the Rng

        /// <summary>math.json: its numbers stay (node drew them), the answers are Js.cs's.</summary>
        private void MathJson()
        {
            const string file = "step1/math.json";
            var (text, raw) = Recording.Load(file);
            var fresh = Recording.Node(raw)!.AsObject();
            JsonArray Answer(string key, int width, Func<double[], int, double> f)
            {
                var xs = Doubles(raw.GetProperty(key));
                for (var i = 0; i < xs.Length; i += width) xs[i + width - 1] = f(xs, i);
                return Numbers(xs);
            }
            fresh["atan2"] = Answer("atan2", 3, (a, i) => Js.Atan2(a[i], a[i + 1]));
            fresh["hypot"] = Answer("hypot", 3, (h, i) => Js.Hypot(h[i], h[i + 1]));
            fresh["round"] = Answer("round", 2, (r, i) => Js.Round(r[i]));
            fresh["coverMinDot"] = Cover.CoverMinDot;
            output.WriteLine(Recording.Report(file, Recording.Write(file, text, raw, Recording.Node(raw), fresh)));
        }

        /// <summary>content.json: content.ts's tables and the answers of its functions; the names tried and the units stay.</summary>
        private void ContentJson()
        {
            const string file = "step1/content.json";
            var (text, raw) = Recording.Load(file);
            var norm = Recording.Node(raw)!.AsObject();
            var fresh = Recording.Node(raw)!.AsObject();
            // `thrown` is a set in C#: sorted on both sides, as the test compares it; the file's order stays if it says the same.
            norm["tables"]!["thrown"] = new JsonArray(raw.GetProperty("tables").GetProperty("thrown").EnumerateArray().Select((x) => x.GetString()!).OrderBy((x) => x, StringComparer.Ordinal).Select((x) => (JsonNode)x).ToArray());
            var tables = new JsonObject();
            foreach (var p in Step1GoldenTests.ContentTables()) tables[p.Key] = Recording.Node(p.Value);
            fresh["tables"] = tables;
            fresh["constants"] = Recording.Node(Step1GoldenTests.AsJson(Step1GoldenTests.ContentConstants()));
            var templates = Content.Templates.Keys.ToList();
            fresh["templateIds"] = new JsonArray(templates.Select((x) => (JsonNode)x).ToArray());
            fresh["hostile"] = Bits(templates.SelectMany((a) => templates.Select((b) => Content.HostileTo(Content.Templates[a].Team, a, Content.Templates[b].Team, b))));
            var names = Content.SoldierNames.Select((n) => n.Name).Concat(new[] { "Kenji Watanabe", "  Ana   Pérez ", "Luis", "Nadie Conocido", "", "Óscar", "Pablo Rey", "Iker\tOrtega", "Ana Ríos" }).ToList();
            fresh["names"] = new JsonArray(names.Select((x) => (JsonNode)x).ToArray());
            fresh["sexes"] = new JsonArray(names.Select((x) => Content.SexOfName(x) is string sex ? (JsonNode)sex : null).ToArray());
            var ranges = new JsonObject();
            foreach (var table in Content.RangeTables.Keys) ranges[table] = Numbers(Enumerable.Range(0, 70).Select((i) => Content.RangeModifier(table, i * 0.5)));
            fresh["ranges"] = ranges;
            fresh["hordePoints"] = Numbers(Enumerable.Range(0, 40).Select((i) => Content.HordePoints(i * 0.75)));
            fresh["wear"] = Numbers(Content.Weapons.Keys.Select(Content.WearOf));
            var attacks = Content.Abilities.Keys.Where(Content.IsAttack).ToList();
            fresh["attacks"] = new JsonArray(attacks.Select((x) => (JsonNode)x).ToArray());
            var units = raw.GetProperty("units").EnumerateArray().ToList();
            fresh["fires"] = new JsonArray(units.Select((e) =>
            {
                var u = Read<Unit>(e);
                var v = Read<Unit>(e);
                Content.ReloadWeapon(v);
                return (JsonNode)new JsonObject
                {
                    ["weapons"] = new JsonArray(attacks.Select((a) => (JsonNode)Content.AttackWeapon(u, a).Id).ToArray()),
                    ["clips"] = Numbers(attacks.Select((a) => Content.ClipOf(u, a))),
                    ["sidearm"] = new JsonArray(attacks.Select((a) => (JsonNode)Content.FiresSidearm(u, a)).ToArray()),
                    ["rounds"] = Numbers(attacks.Select((a) => Content.RoundsOf(u, a))),
                    ["loaded"] = new JsonArray(attacks.Select((a) => (JsonNode)Content.LoadedFor(u, a)).ToArray()),
                    ["primary"] = new JsonArray(Content.Abilities.Keys.Select((a) => (JsonNode)Content.FiresPrimary(u, a)).ToArray()),
                    ["condition"] = Content.ConditionOf(u),
                    ["fullyLoaded"] = Content.FullyLoaded(u),
                    ["squadsight"] = Content.HasSquadsight(u),
                    ["apPerTurn"] = Content.ApPerTurnOf(u),
                    ["reloaded"] = new JsonArray(v.Ammo, v.PistolAmmo is int pistol ? (JsonNode)pistol : null),
                };
            }).ToArray());
            var commands = new List<int>();
            foreach (var slot in new[] { 0, 1 })
                foreach (var owner in new int?[] { null, 0, 1 })
                    foreach (var connected in new[] { null, new int[0], new[] { 0 }, new[] { 1 }, new[] { 0, 1 } })
                        commands.Add(Content.CommandsFor(slot, owner, connected) ? 1 : 0);
            fresh["commandsFor"] = Numbers(commands);
            output.WriteLine(Recording.Report(file, Recording.Write(file, text, raw, norm, fresh)));
        }

        /// <summary>rng.json: eight numbers, eight ints below 100 and the state after, by seed; and four numbers from a saved state.</summary>
        private void RngJson()
        {
            const string file = "rng.json";
            var (text, raw) = Recording.Load(file);
            var fresh = new JsonObject();
            foreach (var p in raw.EnumerateObject())
            {
                if (p.Name == "state")
                {
                    var saved = new Rng(new long[] { -1, 2, -3, 4 });
                    fresh["state"] = new JsonObject { ["next"] = Numbers(Enumerable.Range(0, 4).Select((_) => saved.Next())) };
                    continue;
                }
                var r = new Rng(uint.Parse(p.Name));
                fresh[p.Name] = new JsonObject
                {
                    ["next"] = Numbers(Enumerable.Range(0, 8).Select((_) => r.Next())),
                    ["ints"] = Numbers(Enumerable.Range(0, 8).Select((_) => r.Int(100))),
                    ["state"] = new JsonArray(r.State.Select((x) => (JsonNode)JsonValue.Create(x)).ToArray()),
                };
            }
            output.WriteLine(Recording.Report(file, Recording.Write(file, text, raw, Recording.Node(raw), fresh)));
        }
    }
}
