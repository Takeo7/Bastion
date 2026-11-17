using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// N1 (Ana's note after the 0.13.0, 17/11): what the squad sees today into a building from beside its windows. For
    /// every ground-floor window of a house on the runs' towns and cities, from five places outside (just before it, one
    /// further off, against the wall one and two to its side, one off the wall and one to the side): the tiles inside a
    /// line of sight reaches (Los.SightOrigin, the squad's window reach), and of those the ones where somebody would show
    /// (Buildings.Hidden: a building not found hides who is in it). Only with BASTION_MEDIR_VENTANAS, the folder it
    /// writes to; a measure, not a test.
    /// </summary>
    public class WindowPeekMeasure
    {
        private static readonly (string Key, string Name)[] Spots =
        {
            ("F1", "delante, pegado"),
            ("F2", "delante, a una casilla"),
            ("L1", "al lado, pegado a la pared"),
            ("L2", "a dos del lado, pegado a la pared"),
            ("D1", "en diagonal, a una de la pared"),
        };

        private static (int X, int Y) SpotOf(string key, int wx, int wy, (int X, int Y) n, (int X, int Y) t)
        {
            switch (key)
            {
                case "F1": return (wx + n.X, wy + n.Y);
                case "F2": return (wx + 2 * n.X, wy + 2 * n.Y);
                case "L1": return (wx + n.X + t.X, wy + n.Y + t.Y);
                case "L2": return (wx + n.X + 2 * t.X, wy + n.Y + 2 * t.Y);
                default: return (wx + 2 * n.X + t.X, wy + 2 * n.Y + t.Y);
            }
        }

        private static bool Shut(string? kind) => kind == TileKinds.Wall || kind == TileKinds.Window || kind == TileKinds.WindowBroken || kind == TileKinds.Bars;

        private sealed class Tally
        {
            public int Windows;
            public int Seen;
            public int Shown;
            public int AnySeen;
            public int AnyShown;
            public int DeepSeen;
            public readonly List<int> Depths = new List<int>();
        }

        [Fact]
        public void WhatTheSquadSeesFromBesideAWindow()
        {
            var dir = Environment.GetEnvironmentVariable("BASTION_MEDIR_VENTANAS");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            var tallies = Spots.ToDictionary((p) => p.Key, (_) => new Tally());
            var report = new StringBuilder();
            var pictures = new StringBuilder();
            var picked = 0;
            var json = new List<string>();
            foreach (var place in new[] { Places.Town, Places.City })
            {
                for (var seed = 1; seed <= 12; seed++)
                {
                    var (_, s) = RunInteriorTests.At(place, NodeKinds.Normal, seed);
                    if (s.Storeys == null || s.Mission.Inside == true) continue;
                    var storeys = s.Storeys;
                    var area = s.Width * s.Height;
                    var viewer = s.Units.First((u) => u.Team == Teams.Xcom && u.CampaignId != null);
                    var known = new HashSet<int>(s.Discovered ?? new List<int>());
                    // The buildings: inside tiles joined side by side (Buildings.PlanOf, without the doors' ways).
                    var of = Enumerable.Repeat(-1, area).ToArray();
                    var buildings = new List<List<int>>();
                    bool Room(int i) => storeys[i] > 0 && !Shut(s.Tiles[i]);
                    for (var start = 0; start < area; start++)
                    {
                        if (of[start] >= 0 || !Room(start)) continue;
                        var tiles = new List<int> { start };
                        of[start] = buildings.Count;
                        for (var k = 0; k < tiles.Count; k++)
                        {
                            int x = tiles[k] % s.Width, y = tiles[k] / s.Width;
                            foreach (var d in Grid.Dirs4)
                            {
                                if (!Grid.InBounds(s, x + d.X, y + d.Y)) continue;
                                var j = Grid.TileIndex(s, x + d.X, y + d.Y);
                                if (of[j] >= 0 || !Room(j) || Grid.KindAt(s, new Pos { X = x + d.X, Y = y + d.Y, Z = 0 }) == TileKinds.Door) continue;
                                of[j] = buildings.Count;
                                tiles.Add(j);
                            }
                        }
                        buildings.Add(tiles);
                    }
                    for (var w = 0; w < area; w++)
                    {
                        if (!Grid.IsWindow(s.Tiles[w]) || storeys[w] <= 0) continue;
                        int wx = w % s.Width, wy = w / s.Width;
                        foreach (var d in Grid.Dirs4)
                        {
                            // n points out of the building; t along its wall.
                            var n = (X: -d.X, Y: -d.Y);
                            if (!Grid.InBounds(s, wx + d.X, wy + d.Y) || !Grid.InBounds(s, wx + 2 * n.X, wy + 2 * n.Y)) continue;
                            var inside = Grid.TileIndex(s, wx + d.X, wy + d.Y);
                            var outside = Grid.TileIndex(s, wx + n.X, wy + n.Y);
                            if (!Room(inside) || storeys[outside] > 0 || known.Contains(inside)) continue;
                            var b = of[inside];
                            // A window alone in a straight wall (Ana's: against the wall beside it): plain wall two on each side.
                            var t = (X: d.Y, Y: -d.X);
                            bool WallAt(int x, int y) => Grid.InBounds(s, x, y) && s.Tiles[Grid.TileIndex(s, x, y)] == TileKinds.Wall;
                            if (!WallAt(wx + t.X, wy + t.Y) || !WallAt(wx - t.X, wy - t.Y) || !WallAt(wx + 2 * t.X, wy + 2 * t.Y) || !WallAt(wx - 2 * t.X, wy - 2 * t.Y)) continue;
                            foreach (var side in new[] { 1, -1 })
                            {
                                var tt = (X: t.X * side, Y: t.Y * side);
                                var sees = new Dictionary<string, (HashSet<int> Seen, HashSet<int> Shown)>();
                                foreach (var (key, _) in Spots)
                                {
                                    var (px, py) = SpotOf(key, wx, wy, n, tt);
                                    var p = new Pos { X = px, Y = py, Z = 0 };
                                    if (!Grid.InBounds(s, px, py) || storeys[Grid.TileIndex(s, px, py)] > 0 || !Flights.CanStopAt(s, p)) continue;
                                    var seen = new HashSet<int>();
                                    var shown = new HashSet<int>();
                                    foreach (var i in buildings[b])
                                    {
                                        var q = new Pos { X = i % s.Width, Y = i / s.Width, Z = 0 };
                                        if (Los.SightOrigin(s, p, q, Los.SightTo(s, viewer, q), Los.WindowSightReach) == null) continue;
                                        seen.Add(i);
                                        if (!Buildings.Hidden(s, q, new[] { p })) shown.Add(i);
                                    }
                                    sees[key] = (seen, shown);
                                    var tally = tallies[key];
                                    tally.Windows++;
                                    tally.Seen += seen.Count;
                                    tally.Shown += shown.Count;
                                    if (seen.Count > 0) tally.AnySeen++;
                                    if (shown.Count > 0) tally.AnyShown++;
                                    // How deep in from the wall the furthest tile seen is.
                                    var depth = seen.Count == 0 ? 0 : seen.Max((i) => (i % s.Width - wx) * -n.X + (i / s.Width - wy) * -n.Y);
                                    tally.Depths.Add(depth);
                                    if (depth >= 3) tally.DeepSeen++;
                                }
                                // A few pictures: a room at least 4 deep and 5 wide behind the window, all five places free.
                                if (picked < 4 && sees.Count == Spots.Length && side == 1 && Deep(s, of, b, wx, wy, n, t) && seed % 3 == 1)
                                {
                                    picked++;
                                    pictures.AppendLine($"== {place} {seed}, ventana ({wx},{wy}), fuera hacia ({n.X},{n.Y})");
                                    foreach (var (key, name) in Spots)
                                    {
                                        pictures.AppendLine($"-- {key}: {name} · línea de vista a {sees[key].Seen.Count}, se enseña {sees[key].Shown.Count}");
                                        pictures.Append(Picture(s, of, b, wx, wy, n, t, SpotOf(key, wx, wy, n, tt), sees[key].Seen, sees[key].Shown));
                                    }
                                    json.Add(Json(s, of, b, wx, wy, n, t, place, seed, sees, (key) => SpotOf(key, wx, wy, n, tt)));
                                }
                            }
                        }
                    }
                }
            }
            report.AppendLine("Desde cada sitio, por ventana suelta de la planta baja (con pared a dos casillas por cada lado; pueblo y ciudad, semillas 1–12):");
            foreach (var (key, name) in Spots)
            {
                var t = tallies[key];
                var depths = t.Depths.Count == 0 ? "" : $", fondo medio {t.Depths.Average():0.0}, 3 o más dentro en {Pct(t.DeepSeen, t.Windows)}";
                report.AppendLine($"{key} {name}: {t.Windows} ventanas · línea de vista a algo dentro en {Pct(t.AnySeen, t.Windows)} (media {(double)t.Seen / Math.Max(1, t.Windows):0.0} casillas{depths}) · se enseña algo en {Pct(t.AnyShown, t.Windows)} (media {(double)t.Shown / Math.Max(1, t.Windows):0.0})");
            }
            File.WriteAllText(Path.Combine(dir, "medida.txt"), report.ToString());
            File.WriteAllText(Path.Combine(dir, "dibujos.txt"), pictures.ToString());
            File.WriteAllText(Path.Combine(dir, "dibujos.json"), "[\n" + string.Join(",\n", json) + "\n]\n");
        }

        private static string Pct(int a, int b) => b == 0 ? "—" : $"{100.0 * a / b:0}%";

        private static bool Deep(GameState s, int[] of, int b, int wx, int wy, (int X, int Y) n, (int X, int Y) t)
        {
            for (var depth = 1; depth <= 4; depth++)
            {
                for (var a = -2; a <= 2; a++)
                {
                    int x = wx - n.X * depth + t.X * a, y = wy - n.Y * depth + t.Y * a;
                    if (!Grid.InBounds(s, x, y) || of[Grid.TileIndex(s, x, y)] != b) return false;
                }
            }
            return true;
        }

        /// <summary>Tiles of the picture round the window: along the wall −5..5, out 3 to in 7.</summary>
        private static IEnumerable<(int A, int D, int X, int Y)> Frame(int wx, int wy, (int X, int Y) n, (int X, int Y) t)
        {
            for (var depth = -3; depth <= 7; depth++)
            {
                for (var a = -5; a <= 5; a++) yield return (a, depth, wx - n.X * depth + t.X * a, wy - n.Y * depth + t.Y * a);
            }
        }

        private static string Picture(GameState s, int[] of, int b, int wx, int wy, (int X, int Y) n, (int X, int Y) t, (int X, int Y) spot, HashSet<int> seen, HashSet<int> shown)
        {
            var sb = new StringBuilder();
            var row = -99;
            foreach (var (a, depth, x, y) in Frame(wx, wy, n, t))
            {
                if (depth != row)
                {
                    if (row != -99) sb.AppendLine();
                    row = depth;
                }
                sb.Append(Cell(s, of, b, x, y, spot, seen, shown));
            }
            sb.AppendLine();
            return sb.ToString();
        }

        private static char Cell(GameState s, int[] of, int b, int x, int y, (int X, int Y) spot, HashSet<int> seen, HashSet<int> shown)
        {
            if (x == spot.X && y == spot.Y) return '@';
            if (!Grid.InBounds(s, x, y)) return ' ';
            var i = Grid.TileIndex(s, x, y);
            var kind = s.Tiles[i];
            if (Grid.IsWindow(kind)) return 'V';
            if (Shut(kind)) return '#';
            if (shown.Contains(i)) return 'o';
            if (seen.Contains(i)) return '+';
            if (of[i] == b) return '·';
            return s.Storeys![i] > 0 ? ',' : '.';
        }

        private static string Json(GameState s, int[] of, int b, int wx, int wy, (int X, int Y) n, (int X, int Y) t, string place, int seed, Dictionary<string, (HashSet<int> Seen, HashSet<int> Shown)> sees, Func<string, (int X, int Y)> spotOf)
        {
            var views = new List<string>();
            foreach (var (key, name) in Spots)
            {
                var spot = spotOf(key);
                var rows = new List<string>();
                var row = new StringBuilder();
                var last = -99;
                foreach (var (a, depth, x, y) in Frame(wx, wy, n, t))
                {
                    if (depth != last && last != -99)
                    {
                        rows.Add(row.ToString());
                        row.Clear();
                    }
                    last = depth;
                    row.Append(Cell(s, of, b, x, y, spot, sees[key].Seen, sees[key].Shown));
                }
                rows.Add(row.ToString());
                views.Add($"{{\"key\":\"{key}\",\"name\":\"{name}\",\"seen\":{sees[key].Seen.Count},\"shown\":{sees[key].Shown.Count},\"rows\":[{string.Join(",", rows.Select((r) => $"\"{r}\""))}]}}");
            }
            return $"{{\"place\":\"{place}\",\"seed\":{seed},\"window\":[{wx},{wy}],\"views\":[{string.Join(",", views)}]}}";
        }
    }
}
