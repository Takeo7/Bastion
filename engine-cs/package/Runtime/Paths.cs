#nullable enable
// path.ts: steps, their costs and the Dijkstra of a move. The class is Paths, not Path: System.IO.Path is in every
// file that reads one, the tests' too.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    /// <summary>A step stepsFrom allows: `{ to: Pos; cost: number }`.</summary>
    public sealed class PathStep
    {
        public Pos To { get; set; } = new Pos();
        public int Cost { get; set; }
    }

    /// <summary>A place destinations offers: `{ pos: Pos; cost: number }`.</summary>
    public sealed class Destination
    {
        public Pos Pos { get; set; } = new Pos();
        public int Cost { get; set; }
    }

    public sealed class Reach
    {
        public Pos Origin { get; set; } = new Pos();
        /// <summary>node (grid.ts nodeOf; on the ground, the tile index) -> accumulated cost</summary>
        public JsMap<int, int> Cost { get; set; } = new JsMap<int, int>();
        /// <summary>node -> previous node</summary>
        public JsMap<int, int> Prev { get; set; } = new JsMap<int, int>();
    }

    public static class Paths
    {
        /// <summary>Movement costs in half-tiles so diagonals (1.5 tiles) stay integral.</summary>
        public const int CostOrtho = 2;
        public const int CostDiag = 3;
        /// <summary>Extra cost to climb a ladder one storey up (or down it).</summary>
        public const int CostClimb = 2;
        /// <summary>Extra cost per storey when dropping off an edge.</summary>
        public const int CostDrop = 2;
        /// <summary>Extra cost to vault through a window.</summary>
        public const int CostVault = 2;
        /// <summary>Deepest drop a unit will jump down, in storeys.</summary>
        private const int MaxDrop = 2;

        /// <summary>
        /// Cost of a single step between neighbouring tiles on the ground (without unit
        /// checks), or null if the step is impossible: walls, climbing without a ladder,
        /// cutting corners, or slipping diagonally between levels or through a window.
        /// Maps without storeys move by it; stepBetween is the same on any level.
        /// </summary>
        public static int? StepCost(GameState s, int x, int y, int dx, int dy)
        {
            var nx = x + dx;
            var ny = y + dy;
            if (!Grid.InBounds(s, nx, ny)) return null;
            var w = s.Width;
            var from = s.Tiles[y * w + x];
            var to = s.Tiles[ny * w + nx];
            if (!Grid.IsWalkable(to)) return null;
            var fromElev = Js.At(s.Elev, y * w + x, 0);
            var dz = Js.At(s.Elev, ny * w + nx, 0) - fromElev;
            var diagonal = dx != 0 && dy != 0;

            if (diagonal)
            {
                if (dz != 0 || Grid.IsWindow(from) || Grid.IsWindow(to)) return null;
                foreach (var (ox, oy) in new[] { (nx, y), (x, ny) })
                {
                    var side = s.Tiles[oy * w + ox];
                    if (!Grid.IsWalkable(side) || Grid.IsWindow(side) || Js.At(s.Elev, oy * w + ox, 0) != fromElev) return null;
                }
                return CostDiag;
            }

            var cost = CostOrtho + (Grid.IsWindow(to) ? CostVault : 0);
            if (dz > 0)
            {
                if (dz != 1 || from != TileKinds.Ladder) return null;
                cost += CostClimb;
            }
            else if (dz < 0)
            {
                if (-dz > MaxDrop) return null;
                cost += to == TileKinds.Ladder && dz == -1 ? CostClimb : CostDrop * -dz;
            }
            return cost;
        }

        /// <summary>The way a building's stairs on `p` climb (a DIRS4 index), or null for a ladder or anything else.</summary>
        private static int? StairsFacing(GameState s, Pos p)
        {
            var decor = p.Z != 0 ? Grid.LevelOf(s, p.Z - 1)?.Decor : s.Decor;
            var i = p.Y * s.Width + p.X;
            var look = Decor.DecorOf(decor != null && i >= 0 && i < decor.Count ? decor[i] : (int?)null);
            return look?.Kind == DecorKinds.Stairs ? look.Facing : (int?)null;
        }

        /// <summary>
        /// Cost of a step from `from` to `to`, on the next tile, on any level (without
        /// unit checks), or null if it can't be taken (docs/FASE6-PLAN.md): stepCost's
        /// rules by height instead of elevation. A ladder (or a building's stairs)
        /// climbs one storey onto the next tile, with nothing overhead (a stairwell);
        /// going down onto one costs the same. A building's stairs are left and taken
        /// at the top only by their landing, the tile they face (docs/FASE7.md); a
        /// ladder by any side. Any other way down is a drop off an edge, with no floor
        /// or roof in between, out of an upper window too (docs/FASE7.md). A flight of
        /// several tiles is flightStep's.
        /// </summary>
        public static int? StepBetween(GameState s, Pos from, Pos to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1 || !Grid.InBounds(s, to.X, to.Y)) return null;
            var fromKind = Grid.KindAt(s, from);
            var toKind = Grid.KindAt(s, to);
            if (!Grid.IsWalkable(toKind)) return null;
            var on = fromKind == TileKinds.Ladder ? Flights.LongFlightAt(s, from) : null;
            var onto = toKind == TileKinds.Ladder ? Flights.LongFlightAt(s, to) : null;
            if (on != null || onto != null) return FlightStep(s, from, to, on, onto);
            var h = Grid.HeightAt(s, from);
            var landing = Grid.HeightAt(s, to);
            var dz = landing - h;

            if (dx != 0 && dy != 0)
            {
                if (dz != 0 || Grid.IsWindow(fromKind) || Grid.IsWindow(toKind)) return null;
                foreach (var (ox, oy) in new[] { (to.X, from.Y), (from.X, to.Y) })
                {
                    var side = Grid.SurfaceAt(s, ox, oy, h);
                    // Nor past the corner of a flight's banister.
                    if (side == null || Grid.IsWindow(Grid.KindAt(s, side)) || (Grid.KindAt(s, side) == TileKinds.Ladder && Flights.LongFlightAt(s, side) != null)) return null;
                }
                return CostDiag;
            }

            var cost = CostOrtho + (Grid.IsWindow(toKind) ? CostVault : 0);
            if (dz > 0)
            {
                if (dz != 1 || fromKind != TileKinds.Ladder || Grid.SlabAt(s, from.X, from.Y, h + 1)) return null;
                var up = StairsFacing(s, from);
                if (up != null && (Grid.Dirs4[up.Value].X != dx || Grid.Dirs4[up.Value].Y != dy)) return null;
                cost += CostClimb;
            }
            else if (dz < 0)
            {
                if (-dz > MaxDrop) return null;
                for (var k = landing + 1; k <= h; k++) if (Grid.SlabAt(s, to.X, to.Y, k)) return null;
                var up = toKind == TileKinds.Ladder && dz == -1 ? StairsFacing(s, to) : null;
                if (up != null && (Grid.Dirs4[up.Value].X != -dx || Grid.Dirs4[up.Value].Y != -dy)) return null;
                cost += toKind == TileKinds.Ladder && dz == -1 ? CostClimb : CostDrop * -dz;
            }
            return cost;
        }

        /// <summary>
        /// A step onto, along or off a flight of stairs of several tiles (docs/MAPAS.md,
        /// phase 2), `on` the one `from` is on and `onto` the one `to` is on: only
        /// along it, the way it climbs or back; on it from its foot, off it at the top
        /// onto its landing a storey up (and down the same way), off it at the bottom
        /// onto its foot. Never across its banisters, nor diagonally. Each step costs
        /// a tile's walk, the one up to the landing too (no climb on top): a flight of
        /// three, foot to landing, costs as four tiles.
        /// </summary>
        private static int? FlightStep(GameState s, Pos from, Pos to, FlightSpot? on, FlightSpot? onto)
        {
            var f = (on ?? onto)!.Flight;
            var u = Grid.Dirs4[f.Up];
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var along = dx == u.X && dy == u.Y ? 1 : dx == -u.X && dy == -u.Y ? -1 : 0;
            if (along == 0) return null;
            var top = f.Tiles.Count - 1;
            var rise = Grid.HeightAt(s, to) - Grid.HeightAt(s, from);
            var cost = CostOrtho + (Grid.IsWindow(Grid.KindAt(s, to)) ? CostVault : 0);
            if (on != null && onto != null) return from.Z == to.Z && on.Flight.Tiles[0].X == onto.Flight.Tiles[0].X && on.Flight.Tiles[0].Y == onto.Flight.Tiles[0].Y && onto.Step == on.Step + along ? cost : (int?)null;
            if (on != null)
            {
                // Off at the top onto the landing, or at the bottom onto the foot.
                if (along > 0) return on.Step == top && to.Z == f.Z + 1 && rise == 1 ? cost : (int?)null;
                return on.Step == 0 && to.Z == f.Z && rise == 0 ? cost : (int?)null;
            }
            // On from the foot, or down from the landing onto the top.
            if (along > 0) return onto!.Step == 0 && from.Z == f.Z && rise == 0 ? cost : (int?)null;
            return onto!.Step == top && from.Z == f.Z + 1 && rise == -1 ? cost : (int?)null;
        }

        /// <summary>
        /// Every step from `from` onto the tile (dx, dy) away, to each of its levels a
        /// unit can get to, with its cost. On a map without storeys, stepCost's one.
        /// With storeys there are three at most, each checked by stepBetween: the floor
        /// at the same height, the one a storey up (from a ladder or stairs), and the
        /// highest one below (going down, or a drop).
        /// </summary>
        public static List<PathStep> StepsFrom(GameState s, Pos from, int dx, int dy)
        {
            if (s.Levels == null)
            {
                var c = from.Z == 0 ? StepCost(s, from.X, from.Y, dx, dy) : null;
                return c == null ? new List<PathStep>() : new List<PathStep> { new PathStep { To = new Pos { X = from.X + dx, Y = from.Y + dy, Z = 0 }, Cost = c.Value } };
            }
            var out_ = new List<PathStep>();
            var nx = from.X + dx;
            var ny = from.Y + dy;
            if (!Grid.InBounds(s, nx, ny)) return out_;
            void Take(Pos? to)
            {
                var cost = to != null ? StepBetween(s, from, to) : null;
                if (to != null && cost != null) out_.Add(new PathStep { To = to, Cost = cost.Value });
            }
            var h = Grid.HeightAt(s, from);
            Take(Grid.SurfaceAt(s, nx, ny, h));
            if (dx != 0 && dy != 0) return out_;
            if (Grid.KindAt(s, from) == TileKinds.Ladder) Take(Grid.SurfaceAt(s, nx, ny, h + 1));
            for (var k = h - 1; k >= Math.Max(0, h - MaxDrop); k--)
            {
                var below = Grid.SurfaceAt(s, nx, ny, k);
                if (below != null)
                {
                    Take(below);
                    break;
                }
                if (Grid.SlabAt(s, nx, ny, k) || Grid.ElevAt(s, nx, ny) > k) break;
            }
            return out_;
        }

        /// <summary>Cost budget covered by one action point.</summary>
        public static double CostPerAp(Unit u)
        {
            return (Content.Templates[u.Template].Mobility + u.Mods.Mobility) * CostOrtho;
        }

        public static double MaxMoveCost(Unit u)
        {
            return CostPerAp(u) * Math.Min(u.Ap, 2);
        }

        /// <summary>AP needed to move with the given path cost (1 = blue move, 2 = dash).</summary>
        public static int ApForCost(Unit u, double cost)
        {
            return cost <= CostPerAp(u) ? 1 : 2;
        }

        /// <summary>path.ts MinHeap: [priority, value] pairs, the same sift up and down (equal priorities come out in its order).</summary>
        private sealed class MinHeap
        {
            private readonly List<KeyValuePair<int, int>> items = new List<KeyValuePair<int, int>>();

            public int Size => items.Count;

            public void Push(int priority, int value)
            {
                var a = items;
                a.Add(new KeyValuePair<int, int>(priority, value));
                var i = a.Count - 1;
                while (i > 0)
                {
                    var p = (i - 1) >> 1;
                    if (a[p].Key <= a[i].Key) break;
                    var t = a[p];
                    a[p] = a[i];
                    a[i] = t;
                    i = p;
                }
            }

            public KeyValuePair<int, int> Pop()
            {
                var a = items;
                var top = a[0];
                var last = a[a.Count - 1];
                a.RemoveAt(a.Count - 1);
                if (a.Count > 0)
                {
                    a[0] = last;
                    var i = 0;
                    for (;;)
                    {
                        var l = 2 * i + 1;
                        var r = l + 1;
                        var m = i;
                        if (l < a.Count && a[l].Key < a[m].Key) m = l;
                        if (r < a.Count && a[r].Key < a[m].Key) m = r;
                        if (m == i) break;
                        var t = a[m];
                        a[m] = a[i];
                        a[i] = t;
                        i = m;
                    }
                }
                return top;
            }
        }

        /// <summary>An alien whose pod is still on patrol: the squad hasn't run into it yet.</summary>
        public static bool OnPatrol(GameState s, Unit u)
        {
            return u.Team == Teams.Alien && !string.IsNullOrEmpty(u.PodId) && s.Pods.Any((p) => p.Id == u.PodId && !p.Active);
        }

        /// <summary>
        /// Dijkstra over 8 directions, on every level (docs/FASE6-PLAN.md: up and down
        /// ladders and stairs, off edges). Every living unit but the walker blocks its
        /// place, whoever's side (Arturo, 19/10: «los compañeros se bloquean», as in
        /// XCOM): a soldier, one of the dead, a turret, the VIP; not the floor above or
        /// below. Diagonals may not cut the corners of blocked tiles. A pod on patrol
        /// keeps to doors and open ground (playtest: undiscovered aliens went into a
        /// building through a window): vaulting in is for aliens in the fight. In a
        /// siege the dead don't open doors: they break them down (docs/FASE12.md §4).
        /// `ignore`: units a plan may look past, as if gone (ai.ts breachPlan asks
        /// whether the ground leaves a way, its own crowd about to move); never for a
        /// walk.
        /// </summary>
        public static Reach ComputeReach(GameState s, Unit u, double maxCost, Pos? from = null, Func<Unit, bool>? ignore = null)
        {
            var origin = from ?? u.Pos;
            var windows = !OnPatrol(s, u);
            var shut = s.Mission.Siege != null && Content.Templates[u.Template].Undead;
            // Barbed wire (docs/FASE12.md §11.4): costlier to cross, for everyone.
            var slow = s.Slow != null && s.Slow.Count > 0 ? new HashSet<int>(s.Slow) : null;
            var taken = new HashSet<int>();
            foreach (var o in s.Units)
            {
                if (o.Alive && o != u && !(ignore?.Invoke(o) ?? false)) taken.Add(Grid.NodeOf(s, o.Pos));
            }
            bool Blocked(Pos? p) => p != null && taken.Contains(Grid.NodeOf(s, p));
            var start = Grid.NodeOf(s, origin);
            var cost = new JsMap<int, int>();
            cost.Set(start, 0);
            var prev = new JsMap<int, int>();
            var heap = new MinHeap();
            heap.Push(0, start);

            while (heap.Size > 0)
            {
                var popped = heap.Pop();
                var c = popped.Key;
                var i = popped.Value;
                if (cost.TryGetValue(i, out var known) && c > known) continue;
                var p = Grid.PosOf(s, i);
                foreach (var d in Grid.Dirs8)
                {
                    foreach (var step in StepsFrom(s, p, d.X, d.Y))
                    {
                        var to = step.To;
                        var ni = Grid.NodeOf(s, to);
                        if (taken.Contains(ni) || (!windows && Grid.IsWindow(Grid.KindAt(s, to))) || (shut && Grid.KindAt(s, to) == TileKinds.Door)) continue;
                        if (d.X != 0 && d.Y != 0)
                        {
                            var h = Grid.HeightAt(s, p);
                            if (Blocked(Grid.SurfaceAt(s, to.X, p.Y, h)) || Blocked(Grid.SurfaceAt(s, p.X, to.Y, h))) continue;
                        }
                        var nc = c + step.Cost + (slow != null && slow.Contains(ni) ? Content.Traps.Wire : 0);
                        if (nc > maxCost) continue;
                        if (!cost.TryGetValue(ni, out var old) || nc < old)
                        {
                            cost.Set(ni, nc);
                            prev.Set(ni, i);
                            heap.Push(nc, ni);
                        }
                    }
                }
            }
            return new Reach { Origin = origin, Cost = cost, Prev = prev };
        }

        private static bool Occupied(GameState s, int i, Unit ignore)
        {
            var p = Grid.PosOf(s, i);
            return s.Units.Any((o) => o != ignore && o.Alive && o.Pos.X == p.X && o.Pos.Y == p.Y && o.Pos.Z == p.Z);
        }

        /// <summary>Tiles the unit may end its move on (excludes origin, occupied tiles, window frames and flights of stairs: flights.ts canStopAt).</summary>
        public static List<Destination> Destinations(GameState s, Unit u, Reach reach)
        {
            var out_ = new List<Destination>();
            var start = Grid.NodeOf(s, reach.Origin);
            foreach (var e in reach.Cost)
            {
                var i = e.Key;
                var p = Grid.PosOf(s, i);
                if (i == start || Occupied(s, i, u) || !Flights.CanStopAt(s, p)) continue;
                out_.Add(new Destination { Pos = p, Cost = e.Value });
            }
            return out_;
        }

        /// <summary>Path from the reach origin to `to` (origin excluded), or null if unreachable / occupied.</summary>
        public static List<Pos>? BuildPath(GameState s, Unit u, Reach reach, Pos to)
        {
            if (!Grid.InBounds(s, to.X, to.Y)) return null;
            var end = Grid.NodeOf(s, Grid.ToPos(to));
            var start = Grid.NodeOf(s, reach.Origin);
            if (end == start || !reach.Cost.Has(end) || Occupied(s, end, u) || !Flights.CanStopAt(s, Grid.PosOf(s, end))) return null;
            var path = new List<Pos>();
            int? i = end;
            while (i != null && i != start)
            {
                path.Add(Grid.PosOf(s, i.Value));
                i = reach.Prev.TryGetValue(i.Value, out var back) ? back : (int?)null;
            }
            path.Reverse();
            return path;
        }
    }
}
