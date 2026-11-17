#nullable enable
// The squad's sight on a map that is one building (H7, the hospital only inside: MissionState.Inside; new in C#): found room
// by room, not whole, as Buildings finds a building. A room is a storey's inside joined side by side, not through its doors
// (a corridor is one); it is found going into it (its doorway too), or stopped just before one of its windows or open
// doors, from out of doors or from another room (Arturo, 31/10, as Buildings: «solo si estás asomado»). What is found is
// kept by node, z × area + tile (GameState.Discovered); inside a room not found, nobody shows to the squad.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public static partial class Buildings
    {
        /// <summary>A one-building map's rooms as it stands now.</summary>
        private sealed class RoomPlan
        {
            public string Key = "";
            public int Area;
            /// <summary>By node (z × area + tile), its room; -1 off any (a wall, a window, a door, out of doors).</summary>
            public int[] Of = new int[0];
            /// <summary>By room, its nodes.</summary>
            public List<List<int>> Nodes = new List<List<int>>();
            /// <summary>The nodes just before a window or an open door, with the rooms it looks into.</summary>
            public Dictionary<int, List<int>> Peeks = new Dictionary<int, List<int>>();
        }

        /// <summary>The last map's rooms. One per thread, as the buildings' plan.</summary>
        [ThreadStatic]
        private static RoomPlan? lastRooms;

        /// <summary>Whether the squad finds this map room by room (MissionState.Inside): a map that is one building.</summary>
        public static bool ByRoom(GameState s)
        {
            return s.Mission?.Inside == true && s.Mission.Siege == null;
        }

        /// <summary>What stands on `z`'s layout at tile `i`: the ground's, a storey's; null where nothing is laid.</summary>
        private static string? KindOn(GameState s, int z, int i)
        {
            if (z == 0) return s.Tiles[i];
            var level = s.Levels != null && z - 1 < s.Levels.Count ? s.Levels[z - 1] : null;
            return level != null && i < level.Tiles.Count ? level.Tiles[i] : null;
        }

        /// <summary>The map's rooms, worked out again when what stops sight changes, as PlanOf.</summary>
        private static RoomPlan RoomPlanOf(GameState s)
        {
            var area = s.Width * s.Height;
            var storeys = s.Storeys;
            var key = $"{s.Width}x{s.Height}|{Los.MapStamp(s)}|{(storeys == null ? -1 : storeys.Aggregate(17, (h, k) => unchecked(h * 31 + k)))}";
            if (lastRooms?.Key == key) return lastRooms;
            var plan = new RoomPlan { Key = key, Area = area };
            lastRooms = plan;
            if (storeys == null || storeys.Count != area || s.Tiles.Count != area) return plan;
            var levels = Grid.LevelCount(s);
            bool Inside(int z, int i) => storeys[i] > z;
            bool Room(int z, int i)
            {
                var kind = KindOn(s, z, i);
                return Inside(z, i) && kind != null && !Shut(kind) && !IsDoor(kind);
            }
            plan.Of = Enumerable.Repeat(-1, area * levels).ToArray();
            for (var z = 0; z < levels; z++)
            {
                for (var start = 0; start < area; start++)
                {
                    if (plan.Of[z * area + start] >= 0 || !Room(z, start)) continue;
                    var tiles = new List<int> { start };
                    plan.Of[z * area + start] = plan.Nodes.Count;
                    for (var k = 0; k < tiles.Count; k++)
                    {
                        foreach (var j in Beside(s, tiles[k]))
                        {
                            if (plan.Of[z * area + j] >= 0 || !Room(z, j)) continue;
                            plan.Of[z * area + j] = plan.Nodes.Count;
                            tiles.Add(j);
                        }
                    }
                    plan.Nodes.Add(tiles.Select((i) => z * area + i).ToList());
                }
            }
            // On each storey, a window or an open door with a room on one side and, on the other, where one can stand: out
            // of doors or another room.
            for (var z = 0; z < levels; z++)
            {
                for (var w = 0; w < area; w++)
                {
                    var kind = KindOn(s, z, w);
                    if ((kind != TileKinds.Window && kind != TileKinds.WindowBroken && kind != TileKinds.DoorOpen && kind != TileKinds.Bars) || !Inside(z, w)) continue;
                    int x = w % s.Width, y = w / s.Width;
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        if (!Grid.InBounds(s, x + dx, y + dy) || !Grid.InBounds(s, x - dx, y - dy)) continue;
                        int inside = Grid.TileIndex(s, x + dx, y + dy), outside = Grid.TileIndex(s, x - dx, y - dy);
                        var room = plan.Of[z * area + inside];
                        if (room < 0) continue;
                        var there = KindOn(s, z, outside);
                        if (there == null || !Grid.IsWalkable(there) || IsDoor(there) || plan.Of[z * area + outside] == room) continue;
                        if (Inside(z, outside) && plan.Of[z * area + outside] < 0) continue;
                        var at = z * area + outside;
                        if (!plan.Peeks.TryGetValue(at, out var list)) plan.Peeks[at] = list = new List<int>();
                        if (!list.Contains(room)) list.Add(room);
                    }
                }
            }
            return plan;
        }

        /// <summary>The room of a node; on a wall's, a window's or a door's, those beside it on its storey.</summary>
        private static IEnumerable<int> RoomsUnder(GameState s, RoomPlan plan, int node)
        {
            if (plan.Of[node] >= 0) return new[] { plan.Of[node] };
            int z = node / plan.Area, i = node % plan.Area;
            return Beside(s, i).Where((j) => plan.Of[z * plan.Area + j] >= 0).Select((j) => plan.Of[z * plan.Area + j]).Distinct();
        }

        /// <summary>The rooms found from `p`: the one it is in (its doorway too) and, `stopped` there, those it looks into.</summary>
        private static List<int> RoomsFound(GameState s, RoomPlan plan, Pos p, bool stopped)
        {
            var found = new List<int>();
            if (plan.Of.Length == 0 || !Grid.InBounds(s, p.X, p.Y) || p.Z < 0 || p.Z >= Grid.LevelCount(s)) return found;
            var i = Grid.TileIndex(s, p.X, p.Y);
            var node = p.Z * plan.Area + i;
            if (stopped && plan.Peeks.TryGetValue(node, out var through)) found.AddRange(through);
            if (s.Storeys![i] > p.Z) foreach (var r in RoomsUnder(s, plan, node)) if (!found.Contains(r)) found.Add(r);
            return found;
        }

        private static void DiscoverRooms(GameState s, Pos p, bool stopped)
        {
            var plan = RoomPlanOf(s);
            var found = RoomsFound(s, plan, p, stopped);
            if (found.Count == 0) return;
            var known = new HashSet<int>(s.Discovered ?? new List<int>());
            foreach (var r in found)
            {
                foreach (var n in plan.Nodes[r])
                {
                    if (known.Add(n)) (s.Discovered ??= new List<int>()).Add(n);
                }
            }
        }

        /// <summary>Whether a node is hidden, with the rooms, what is found and what the viewers find from where they stand.</summary>
        private static bool HiddenInRooms(GameState s, RoomPlan plan, HashSet<int> known, HashSet<int> seen, int node)
        {
            int z = node / plan.Area, i = node % plan.Area;
            if (IsDoor(KindOn(s, z, i))) return false;
            if (plan.Of[node] >= 0) return !known.Contains(node) && !seen.Contains(plan.Of[node]);
            var any = false;
            foreach (var j in Beside(s, i))
            {
                var n = z * plan.Area + j;
                if (plan.Of[n] < 0) continue;
                if (known.Contains(n) || seen.Contains(plan.Of[n])) return false;
                any = true;
            }
            return any;
        }

        private static bool HiddenByRoom(GameState s, Pos p, IEnumerable<Pos>? viewers)
        {
            var plan = RoomPlanOf(s);
            if (plan.Of.Length == 0 || p.Z < 0 || p.Z >= Grid.LevelCount(s)) return false;
            var seen = new HashSet<int>();
            if (viewers != null) foreach (var v in viewers) seen.UnionWith(RoomsFound(s, plan, v, true));
            return HiddenInRooms(s, plan, Known(s), seen, p.Z * plan.Area + Grid.TileIndex(s, p.X, p.Y));
        }

        private static HashSet<int> FoundRoomNodes(GameState s)
        {
            var found = new HashSet<int>(s.Discovered ?? new List<int>());
            var plan = RoomPlanOf(s);
            foreach (var u in s.Units)
            {
                if (!Los.IsViewer(u, Teams.Xcom)) continue;
                foreach (var r in RoomsFound(s, plan, u.Pos, true)) found.UnionWith(plan.Nodes[r]);
            }
            return found;
        }

        private static void VeilRooms(GameState s, bool[] mask)
        {
            var plan = RoomPlanOf(s);
            if (plan.Of.Length == 0) return;
            var known = Known(s);
            var seen = new HashSet<int>();
            foreach (var u in s.Units) if (Los.IsViewer(u, Teams.Xcom)) seen.UnionWith(RoomsFound(s, plan, u.Pos, true));
            var area = s.Width * s.Height;
            for (var node = 0; node < mask.Length && node < plan.Of.Length; node++)
            {
                if (!mask[node]) continue;
                int i = node % area, z = node / area;
                if (s.Storeys![i] <= 0 || z >= s.Storeys[i]) continue;
                if (HiddenInRooms(s, plan, known, seen, node)) mask[node] = false;
            }
        }

        /// <summary>
        /// On a map found room by room (ByRoom), the room `p` stands in: its nodes (z × area + tile); null off any (a wall, a
        /// window, a door, out of doors) and on any other map.
        /// </summary>
        public static IReadOnlyList<int>? RoomAt(GameState s, Pos p)
        {
            if (!ByRoom(s) || !Grid.InBounds(s, p.X, p.Y) || p.Z < 0 || p.Z >= Grid.LevelCount(s)) return null;
            var plan = RoomPlanOf(s);
            if (plan.Of.Length == 0) return null;
            var r = plan.Of[p.Z * plan.Area + Grid.TileIndex(s, p.X, p.Y)];
            return r >= 0 ? plan.Nodes[r] : null;
        }

        /// <summary>On a map found room by room (ByRoom), its rooms, each its nodes (z × area + tile); none on any other map.</summary>
        public static IReadOnlyList<IReadOnlyList<int>> Rooms(GameState s)
        {
            if (!ByRoom(s)) return new List<IReadOnlyList<int>>();
            return RoomPlanOf(s).Nodes;
        }
    }
}
