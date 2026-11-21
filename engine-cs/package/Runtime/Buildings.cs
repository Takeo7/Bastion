#nullable enable
// The squad's sight into buildings (Ana, 31/10, docs/PRUEBA-2026-10-31.md #1 and #5; new in C#, not in los.ts): inside a
// building the squad has neither been into nor looked into from just before one of its windows, nobody shows to it,
// though they see it. A building is its tiles inside, joined side by side and through its doors, by the ground's: two
// houses wall to wall without a door are two (the client's Fog.Buildings and Fog.Peeks, Arte's, 31/10). What the squad
// has found is kept by tile (GameState.Discovered). A map that is one building is found room by room (BuildingRooms.cs).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Buildings
    {
        /// <summary>A map's buildings as it stands now.</summary>
        private sealed class Plan
        {
            public string Key = "";
            /// <summary>By ground tile, its building; -1 off any (a wall, a window, the street).</summary>
            public int[] Of = new int[0];
            /// <summary>By building, its ground tiles.</summary>
            public List<List<int>> Tiles = new List<List<int>>();
            /// <summary>The places just before a window or an open door (z * area + tile), with the buildings it looks into.</summary>
            public Dictionary<int, List<int>> Peeks = new Dictionary<int, List<int>>();
        }

        /// <summary>The last map's plan. One per thread, as Los.TeamView's: the room runs the rules on its own.</summary>
        [ThreadStatic]
        private static Plan? lastPlan;

        /// <summary>The last GameState.Discovered as a set, and what from (it only grows).</summary>
        [ThreadStatic]
        private static (List<int>? list, int count, HashSet<int> set) lastKnown;

        private static bool Shut(string? kind)
        {
            return kind == TileKinds.Wall || kind == TileKinds.Window || kind == TileKinds.WindowBroken || kind == TileKinds.Bars;
        }

        private static bool IsDoor(string? kind)
        {
            return kind == TileKinds.Door || kind == TileKinds.DoorOpen;
        }

        private static IEnumerable<int> Beside(GameState s, int i)
        {
            int x = i % s.Width, y = i / s.Width;
            if (x > 0) yield return i - 1;
            if (x < s.Width - 1) yield return i + 1;
            if (y > 0) yield return i - s.Width;
            if (y < s.Height - 1) yield return i + s.Width;
        }

        /// <summary>The map's buildings, worked out again when what stops sight changes (a door opened, a wall blown through).</summary>
        private static Plan PlanOf(GameState s)
        {
            var area = s.Width * s.Height;
            var storeys = s.Storeys;
            var key = $"{s.Width}x{s.Height}|{Los.MapStamp(s)}|{(storeys == null ? -1 : storeys.Aggregate(17, (h, k) => unchecked(h * 31 + k)))}";
            if (lastPlan?.Key == key) return lastPlan;
            var plan = new Plan { Key = key };
            lastPlan = plan;
            // Hand-made maps and older saves have no storeys: no buildings, nothing hidden.
            if (storeys == null || storeys.Count != area || s.Tiles.Count != area) return plan;
            bool Room(int i) => storeys[i] > 0 && !Shut(s.Tiles[i]);
            plan.Of = Enumerable.Repeat(-1, area).ToArray();
            for (var start = 0; start < area; start++)
            {
                if (plan.Of[start] >= 0 || !Room(start)) continue;
                var tiles = new List<int> { start };
                plan.Of[start] = plan.Tiles.Count;
                for (var k = 0; k < tiles.Count; k++)
                {
                    foreach (var j in Beside(s, tiles[k]))
                    {
                        if (plan.Of[j] >= 0 || !Room(j)) continue;
                        plan.Of[j] = plan.Tiles.Count;
                        tiles.Add(j);
                    }
                }
                plan.Tiles.Add(tiles);
            }
            // On each storey, a window of a building that tall with, along a row or a column, its inside on one side (not a
            // wall nor a window) and on the other a tile off any building that tall (the street, a yard, a lower roof). An
            // open door as well (Arturo, 5/11 #2: «si abro una puerta… tengo que ver el interior, porque estoy en la puerta»).
            for (var z = 0; z < Grid.LevelCount(s); z++)
            {
                var layout = z == 0 ? (IReadOnlyList<string?>)s.Tiles : s.Levels![z - 1].Tiles;
                if (layout.Count != area) continue;
                for (var w = 0; w < area; w++)
                {
                    if ((layout[w] != TileKinds.Window && layout[w] != TileKinds.WindowBroken && layout[w] != TileKinds.DoorOpen) || storeys[w] <= z) continue;
                    int x = w % s.Width, y = w / s.Width;
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        if (!Grid.InBounds(s, x + dx, y + dy) || !Grid.InBounds(s, x - dx, y - dy)) continue;
                        int inside = Grid.TileIndex(s, x + dx, y + dy), outside = Grid.TileIndex(s, x - dx, y - dy);
                        if (storeys[inside] <= z || layout[inside] == null || Shut(layout[inside]) || storeys[outside] > z) continue;
                        var key2 = z * area + outside;
                        if (!plan.Peeks.TryGetValue(key2, out var list)) plan.Peeks[key2] = list = new List<int>();
                        foreach (var b in Under(s, plan, inside)) if (!list.Contains(b)) list.Add(b);
                    }
                }
            }
            return plan;
        }

        /// <summary>G23.6: the map's buildings as it stands now, each its ground tiles (a copy: Evacuación's houses to hide the civilians in).</summary>
        public static List<List<int>> GroundTiles(GameState s) => PlanOf(s).Tiles.Select((t) => new List<int>(t)).ToList();

        /// <summary>The building of a ground tile; on a wall's (a storey above laid out otherwise), those beside it.</summary>
        private static IEnumerable<int> Under(GameState s, Plan plan, int i)
        {
            if (plan.Of[i] >= 0) return new[] { plan.Of[i] };
            return Beside(s, i).Where((j) => plan.Of[j] >= 0).Select((j) => plan.Of[j]).Distinct();
        }

        /// <summary>
        /// The buildings found from `p`: the one it is in (on one of its storeys, its threshold too; on its roof, not) and,
        /// `stopped` there, those it looks into from just before one of their windows (Arturo, 31/10: «solo si estás
        /// asomado en ese cuadro de la ventana por fuera»; walking past does not count).
        /// </summary>
        private static List<int> Found(GameState s, Plan plan, Pos p, bool stopped)
        {
            var found = new List<int>();
            if (plan.Of.Length == 0 || !Grid.InBounds(s, p.X, p.Y)) return found;
            var i = Grid.TileIndex(s, p.X, p.Y);
            if (stopped && plan.Peeks.TryGetValue(p.Z * s.Width * s.Height + i, out var through)) found.AddRange(through);
            if (s.Storeys![i] > 0 && p.Z < s.Storeys[i]) foreach (var b in Under(s, plan, i)) if (!found.Contains(b)) found.Add(b);
            return found;
        }

        private static HashSet<int> Known(GameState s)
        {
            var list = s.Discovered;
            if (list == null) return new HashSet<int>();
            if (lastKnown.list != list || lastKnown.count != list.Count) lastKnown = (list, list.Count, new HashSet<int>(list));
            return lastKnown.set;
        }

        /// <summary>A member of the squad on `p` (stopped there, or walking through): what it finds is kept for the mission.</summary>
        public static void Discover(GameState s, Pos p, bool stopped)
        {
            // A siege's map is the squad's own base: known inside.
            if (s.Mission?.Siege != null) return;
            // A map that is one building (H7): room by room (BuildingRooms.cs).
            if (ByRoom(s))
            {
                DiscoverRooms(s, p, stopped);
                return;
            }
            var plan = PlanOf(s);
            var found = Found(s, plan, p, stopped);
            if (found.Count == 0) return;
            var known = new HashSet<int>(s.Discovered ?? new List<int>());
            foreach (var b in found)
            {
                foreach (var i in plan.Tiles[b])
                {
                    if (known.Add(i)) (s.Discovered ??= new List<int>()).Add(i);
                }
            }
        }

        /// <summary>
        /// Whether whoever stands on `p` is hidden from the squad: inside a building it has not found, under its roof and
        /// off its doors (who stands in a doorway shows). `viewers`, places of the squad that count as found too (where
        /// they stand now, or would: Los.CanSee's viewerPos).
        /// </summary>
        public static bool Hidden(GameState s, Pos p, IEnumerable<Pos>? viewers = null)
        {
            if (s.Mission?.Siege != null || s.Storeys == null || !Grid.InBounds(s, p.X, p.Y)) return false;
            var i = Grid.TileIndex(s, p.X, p.Y);
            if (s.Storeys[i] <= 0 || p.Z >= s.Storeys[i]) return false;
            if (ByRoom(s)) return HiddenByRoom(s, p, viewers);
            var plan = PlanOf(s);
            if (plan.Of.Length == 0) return false;
            var seen = new HashSet<int>();
            if (viewers != null) foreach (var v in viewers) seen.UnionWith(Found(s, plan, v, true));
            return HiddenIn(s, plan, Known(s), seen, p, i) && !SideSeen(s, p, viewers);
        }

        // ------------------------------------------------- from beside a window (N1)

        /// <summary>How many rows behind a window glass the squad sees from beside it, against the wall (N1, B).</summary>
        public const int SideRows = 2;

        /// <summary>
        /// What a squad member on `p` sees from beside a window (N1, Arturo 17/11, cuadro 22, B: «solo un poco»): standing
        /// outside, on the ground, against a building's wall right beside one of its ground-floor windows (on the tile next to
        /// the one before it), the building's tiles one and two rows behind the glass, two to either side, that its line of
        /// sight reaches (Los.SightOrigin, the squad's window reach). Shown, not found: gone when it leaves (Hidden, Veil).
        /// Ground tile indices.
        /// </summary>
        public static List<int> SideStrip(GameState s, Pos p)
        {
            var strip = new List<int>();
            var storeys = s.Storeys;
            if (storeys == null || p.Z != 0 || !Grid.InBounds(s, p.X, p.Y) || storeys[Grid.TileIndex(s, p.X, p.Y)] > 0) return strip;
            bool Room(int x, int y) => Grid.InBounds(s, x, y) && storeys[Grid.TileIndex(s, x, y)] > 0 && !Shut(s.Tiles[Grid.TileIndex(s, x, y)]);
            foreach (var d in Grid.Dirs4)
            {
                // `d` into the wall it stands against: a plain wall there, not a window (before one, Found's peek).
                int wx = p.X + d.X, wy = p.Y + d.Y;
                if (!Grid.InBounds(s, wx, wy) || s.Tiles[Grid.TileIndex(s, wx, wy)] != TileKinds.Wall || storeys[Grid.TileIndex(s, wx, wy)] <= 0) continue;
                foreach (var t in new[] { new Vec2 { X = d.Y, Y = -d.X }, new Vec2 { X = -d.Y, Y = d.X } })
                {
                    int x = wx + t.X, y = wy + t.Y;
                    if (!Grid.InBounds(s, x, y) || !Grid.IsWindow(s.Tiles[Grid.TileIndex(s, x, y)]) || !Room(x + d.X, y + d.Y)) continue;
                    for (var k = 1; k <= SideRows; k++)
                    {
                        for (var j = -2; j <= 2; j++)
                        {
                            int qx = x + d.X * k + t.X * j, qy = y + d.Y * k + t.Y * j;
                            if (!Room(qx, qy)) continue;
                            var i = Grid.TileIndex(s, qx, qy);
                            if (strip.Contains(i)) continue;
                            if (Los.SightOrigin(s, p, new Pos { X = qx, Y = qy, Z = 0 }, Los.ViewReach(s), Los.WindowSightReach) != null) strip.Add(i);
                        }
                    }
                }
            }
            return strip;
        }

        /// <summary>
        /// Every tile the squad sees from beside a window now (SideStrip of each of its members on the ground): for the
        /// client's fog and cutaway (Arte, N1). Not found; a building found is as before.
        /// </summary>
        public static HashSet<int> SidePeeks(GameState s)
        {
            var all = new HashSet<int>();
            if (s.Mission?.Siege != null || s.Storeys == null || ByRoom(s)) return all;
            foreach (var u in s.Units) if (Los.IsViewer(u, Teams.Xcom) && u.Pos.Z == 0) all.UnionWith(SideStrip(s, u.Pos));
            return all;
        }

        /// <summary>Whether `p` is in the strip one of the squad (or of `viewers`) sees from beside a window, near it.</summary>
        private static bool SideSeen(GameState s, Pos p, IEnumerable<Pos>? viewers)
        {
            if (p.Z != 0 || ByRoom(s)) return false;
            var i = Grid.TileIndex(s, p.X, p.Y);
            var near = s.Units.Where((u) => Los.IsViewer(u, Teams.Xcom)).Select((u) => u.Pos).Concat(viewers ?? new Pos[0])
                .Where((v) => v.Z == 0 && Math.Max(Math.Abs(v.X - p.X), Math.Abs(v.Y - p.Y)) <= SideRows + 2);
            return near.Any((v) => SideStrip(s, v).Contains(i));
        }

        /// <summary>Hidden, with the plan, what is found and what the viewers find from where they stand at hand.</summary>
        private static bool HiddenIn(GameState s, Plan plan, HashSet<int> known, HashSet<int> seen, Pos p, int i)
        {
            if (IsDoor(Grid.KindAt(s, p))) return false;
            if (plan.Of[i] >= 0) return !known.Contains(i) && !seen.Contains(plan.Of[i]);
            var any = false;
            foreach (var j in Beside(s, i))
            {
                if (plan.Of[j] < 0) continue;
                if (known.Contains(j) || seen.Contains(plan.Of[j])) return false;
                any = true;
            }
            return any;
        }

        /// <summary>
        /// The ground tiles of the buildings the squad has found (GameState.Discovered), with those it finds where it
        /// stands now (where a mission starts, nobody has moved yet): for the client's fog. Empty in a siege, which is
        /// known whole.
        /// </summary>
        public static HashSet<int> FoundTiles(GameState s)
        {
            var found = new HashSet<int>(s.Discovered ?? new List<int>());
            if (s.Mission?.Siege != null) return found;
            // Room by room (H7): nodes, z × area + tile; a ground floor's are its tiles.
            if (ByRoom(s)) return FoundRoomNodes(s);
            var plan = PlanOf(s);
            foreach (var u in s.Units)
            {
                if (!Los.IsViewer(u, Teams.Xcom)) continue;
                foreach (var b in Found(s, plan, u.Pos, true)) found.UnionWith(plan.Tiles[b]);
            }
            return found;
        }

        /// <summary>The squad's sight (Los.TeamVisibility) with what is hidden from it taken out.</summary>
        public static void Veil(GameState s, bool[] mask)
        {
            if (s.Mission?.Siege != null || s.Storeys == null) return;
            if (ByRoom(s))
            {
                VeilRooms(s, mask);
                return;
            }
            var plan = PlanOf(s);
            if (plan.Of.Length == 0) return;
            var known = Known(s);
            var seen = new HashSet<int>();
            foreach (var u in s.Units) if (Los.IsViewer(u, Teams.Xcom)) seen.UnionWith(Found(s, plan, u.Pos, true));
            var side = SidePeeks(s);
            var area = s.Width * s.Height;
            for (var node = 0; node < mask.Length; node++)
            {
                if (!mask[node]) continue;
                int i = node % area, z = node / area;
                if (s.Storeys[i] <= 0 || z >= s.Storeys[i]) continue;
                if (z == 0 && side.Contains(i)) continue;
                if (HiddenIn(s, plan, known, seen, Grid.PosOf(s, node), i)) mask[node] = false;
            }
        }
    }
}
