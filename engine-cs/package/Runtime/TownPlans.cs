#nullable enable
// Town plans (docs/MAPAS.md §5, phase 3): where a town's streets run, the outline that closes the map round what is
// played (§5.10) and the ways in.
//
// A grid town is a net of streets 26–34 tiles apart, one of them the avenue (7 wide; the others 5), some of them
// stepping sideways at a crossing. An old town grows from its square: three or four streets 3–4 wide leave it, each
// bending every 8–14 tiles, a ring goes round it and alleys of 2 sew the biggest blocks. The map stays a rectangle,
// but what is played has a shape of its own: along the edges, blocks shut up, fenced lots, ruins and works close it
// at a depth of their own, and at most once a river, the railway, the motorway or a barrier runs along a side. Two to
// four streets stay open to the edge: the squad comes in by one, the dead by all of them. What is left is cut into
// rectangles, the blocks the town's programs build on.
//
// townPlan.ts in C#: its types are in TownPlanTypes.cs; the class is TownPlans, as the module's name is the type's.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Engine
{
    public sealed class PlanRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
    }

    public sealed class PlanRoad
    {
        public PlanRect R { get; set; } = new PlanRect();
        /// <summary>Runs north–south.</summary>
        public bool Vertical { get; set; }
        /// <summary>The avenue, the map's one wide street (the bus waits on it).</summary>
        public bool? Avenue { get; set; }
    }

    /// <summary>A street open to the map's edge: the squad comes in by one, the dead by all of them.</summary>
    public sealed class Entry
    {
        /// <summary>The map's side it leaves by (a DIRS4 index: 0 north, 1 east, 2 south, 3 west).</summary>
        public int Side { get; set; }
        public PlanRoad Road { get; set; } = new PlanRoad();
    }

    public sealed class TownPlan
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<PlanRoad> Roads { get; set; } = new List<PlanRoad>();
        /// <summary>Out of play, by tile (docs/MAPAS.md §5.10).</summary>
        public bool[] Out { get; set; } = new bool[0];
        public List<FramePiece> Frame { get; set; } = new List<FramePiece>();
        /// <summary>The ground left to build on, in rectangles: each a block for the town's programs.</summary>
        public List<PlanRect> Blocks { get; set; } = new List<PlanRect>();
        /// <summary>An old town's square, open ground in the middle of it.</summary>
        public PlanRect? Square { get; set; }
        public List<Entry> Entries { get; set; } = new List<Entry>();
        /// <summary>The squad's way in (one of `entries`) and the tile on it, a little in from the edge, it gathers round.</summary>
        public Entry SquadEntry { get; set; } = new Entry();
        public Vec2 Squad { get; set; } = new Vec2();
    }

    public static class TownPlans
    {
        /// <summary>Street widths (the roadway; the pavements are the blocks'): the avenue, a grid's street, an old town's, an alley.</summary>
        public const int Avenue = 7;
        public const int Street = 5;
        private static readonly IReadOnlyList<int> OldStreet = new[] { 3, 4 };
        private const int Alley = 2;
        /// <summary>A grid's streets, centre to centre (docs/MAPAS.md §5.1: 26–34): blocks deep enough for a courtyard now and then (§4.1).</summary>
        private static readonly IReadOnlyList<int> Spacing = new[] { 26, 34 };
        /// <summary>A grid's with big blocks (a hospital's: 34 apart).</summary>
        private static readonly IReadOnlyList<int> WideSpacing = new[] { 32, 36 };
        /// <summary>An industrial estate's (docs/MAPAS.md §4.1: blocks of 24–36, streets for the lorries).</summary>
        private static readonly IReadOnlyList<int> EstateSpacing = new[] { 34, 40 };
        /// <summary>The first street from the map's edge: the blocks along the edge keep some ground past the outline.</summary>
        private static readonly IReadOnlyList<int> First = new[] { 13, 19 };
        /// <summary>Nothing is played this close to the map's edge but on the ways in (rules.ts EDGE_BAND: where the dead come in).</summary>
        private const int EdgeShut = 2;
        /// <summary>The narrowest a block may be; thinner leftovers are open ground.</summary>
        private const int MinBlock = 5;
        /// <summary>
        /// What stays of a block the outline cuts into, at least: a row of buildings seven deep and its pavement (Arte, 20/10:
        /// a strip of five or six holds nothing); less, and it is closed whole. Only where that leaves too little to play, a
        /// back wall and a narrow yard instead.
        /// </summary>
        private const int MinKept = 9;
        /// <summary>The deepest a block along the edge is closed whole; deeper ones keep their front.</summary>
        private const int WholeMax = 12;
        /// <summary>The least share of the map to play: closed deeper than that, the edge is closed again by back walls only.</summary>
        private const double PlayedLeast = 0.65;

        private static PlanRect Rect(int x, int y, int w, int h)
        {
            return new PlanRect { X = x, Y = y, W = w, H = h };
        }

        private static Vec2 V(int x, int y)
        {
            return new Vec2 { X = x, Y = y };
        }

        private static readonly IReadOnlyList<Vec2> Dirs = new[]
        {
            V(0, -1),
            V(1, 0),
            V(0, 1),
            V(-1, 0),
        };

        private static T Weighted<T>(Rng rng, IReadOnlyList<(T Value, int Weight)> options)
        {
            var total = 0;
            foreach (var (_, w) in options) total += w;
            var roll = rng.Next() * total;
            foreach (var (v, w) in options) if ((roll -= w) < 0) return v;
            return options[options.Count - 1].Value;
        }

        /// <summary>Street centre lines across `span`: the first FIRST in, then every `spacing`, none closer than FIRST to the far edge.</summary>
        private static List<int> Axes(int span, Rng rng, IReadOnlyList<int> spacing)
        {
            var @out = new List<int>();
            for (var a = rng.Range(First[0], First[1]); a <= span - First[0]; a += rng.Range(spacing[0], spacing[1])) @out.Add(a);
            return @out;
        }

        /// <summary>A grid's street centre line (gridRoads' `{ vertical, a, off }`), compared by reference as there.</summary>
        private sealed class AxisLine
        {
            public bool Vertical;
            public int A;
            public double Off;
        }

        /// <summary>
        /// A grid town's streets: the axes both ways, the one nearest the middle the avenue. Each street is cut at its
        /// crossings, and a street that isn't the avenue may step one to three tiles sideways at one (§5.1: «calles que se
        /// desplazan 1–3 casillas en un cruce»). Every stretch reaches across the streets it meets, so they join.
        /// </summary>
        private static List<PlanRoad> GridRoads(int W, int H, Rng rng, bool wide, bool estate = false)
        {
            var spacing = estate ? EstateSpacing : wide ? WideSpacing : Spacing;
            var xs = Axes(W, rng, spacing);
            var ys = Axes(H, rng, spacing);
            var lines = xs.Select((a) => new AxisLine { Vertical = true, A = a, Off = Math.Abs(a - W / 2.0) }).Concat(ys.Select((a) => new AxisLine { Vertical = false, A = a, Off = Math.Abs(a - H / 2.0) })).ToList();
            AxisLine? avenue = lines.Count > 0 ? lines[0] : null;
            var low = double.PositiveInfinity;
            foreach (var l in lines)
            {
                var score = l.Off + rng.Next() * 8;
                if (score < low) (low, avenue) = (score, l);
            }
            int WidthOf(AxisLine l) => l == avenue ? Avenue : Street;
            var roads = new List<PlanRoad>();
            foreach (var line in lines)
            {
                var w = WidthOf(line);
                var span = line.Vertical ? H : W;
                var across = lines.Where((l) => l.Vertical != line.Vertical).ToList();
                Js.Sort(across, (a, b) => a.A - b.A);
                var shift = 0;
                for (var k = 0; k <= across.Count; k++)
                {
                    var before = Js.At<AxisLine?>(across, k - 1, null);
                    var after = Js.At<AxisLine?>(across, k, null);
                    // A step at the crossing this stretch starts from.
                    if (before != null && line != avenue && rng.Next() < 0.3) shift = Math.Max(-3, Math.Min(3, shift + (rng.Int(2) != 0 ? 1 : -1) * rng.Range(1, 3)));
                    var from = before != null ? before.A - WidthOf(before) / 2 : 0;
                    var to = after != null ? after.A - WidthOf(after) / 2 + WidthOf(after) : span;
                    var at = line.A - w / 2 + shift;
                    roads.Add(new PlanRoad { R = line.Vertical ? Rect(at, from, w, to - from) : Rect(from, at, to - from, w), Vertical = line.Vertical, Avenue = line == avenue ? true : (bool?)null });
                }
            }
            return roads;
        }

        /// <summary>
        /// An old town's streets: three or four leave its square, by sides of their own, and run out to the map's edge,
        /// stepping two or three tiles sideways every 8–14 (the same way, mostly, so the street runs on a slant); a ring goes
        /// round the square, some way off, stepping halfway along each side. The first is the high street, straight and five
        /// wide, the town's avenue (the bus waits on it). The square itself, open ground.
        /// </summary>
        private static (List<PlanRoad> Roads, PlanRect Square) OldTownRoads(int W, int H, Rng rng)
        {
            var sw = rng.Range(12, 16);
            var sh = rng.Range(12, 16);
            var square = Rect((int)Js.Round(W / 2.0 - sw / 2.0) + rng.Range(-4, 4), (int)Js.Round(H / 2.0 - sh / 2.0) + rng.Range(-4, 4), sw, sh);
            var roads = new List<PlanRoad>();
            var sides = new List<int> { 0, 1, 2, 3 };
            for (var i = sides.Count - 1; i > 0; i--)
            {
                var j = rng.Int(i + 1);
                (sides[i], sides[j]) = (sides[j], sides[i]);
            }
            // The high street first, out to the east or the south: away from where the squad comes in (the north or the west),
            // so the bus has its way out.
            var away = sides.FindIndex((s) => s == 1 || s == 2);
            (sides[0], sides[away]) = (sides[away], sides[0]);
            var leaving = sides.GetRange(0, rng.Range(3, 4));
            for (var n = 0; n < leaving.Count; n++)
            {
                var side = leaving[n];
                var high = n == 0;
                var w = high ? Street : rng.Pick(OldStreet);
                var vertical = side % 2 == 0;
                var d = Dirs[side];
                var len = vertical ? square.W : square.H;
                // Where along the square's side it leaves, clear of its corners; and the way it drifts.
                var u = (vertical ? square.X : square.Y) + rng.Range(2, Math.Max(2, len - w - 2));
                var drift = rng.Int(2) != 0 ? 1 : -1;
                // From the square's side out to the edge, stretch by stretch.
                var t = side == 0 ? square.Y : side == 1 ? square.X + square.W : side == 2 ? square.Y + square.H : square.X;
                var edge = side == 0 ? 0 : side == 1 ? W : side == 2 ? H : 0;
                for (;;)
                {
                    var stretch = rng.Range(8, 14);
                    var left = Math.Abs(edge - t);
                    var run = high || left - stretch < 6 ? left : stretch;
                    var a = d.X + d.Y > 0 ? t : t - run;
                    roads.Add(new PlanRoad { R = vertical ? Rect(u, a, w, run) : Rect(a, u, run, w), Vertical = vertical, Avenue = high ? true : (bool?)null });
                    t += (d.X + d.Y) * run;
                    if (run == left) break;
                    // The step: a short cross street, as wide as the street, the stretch after it shifted.
                    var step = (rng.Next() < 0.75 ? drift : -drift) * rng.Range(2, 3);
                    var lo = Math.Min(u, u + step);
                    var hi = Math.Max(u, u + step) + w;
                    var max = vertical ? W : H;
                    if (lo < 4 || hi > max - 4) continue;
                    var b = d.X + d.Y > 0 ? t : t - w;
                    roads.Add(new PlanRoad { R = vertical ? Rect(lo, b, hi - lo, w) : Rect(b, lo, w, hi - lo), Vertical = !vertical });
                    u += step;
                }
            }
            // The ring, 9–12 tiles off the square, three wide; it steps halfway along a side now and then.
            var gap = rng.Range(9, 12);
            var ring = Rect(square.X - gap - 3, square.Y - gap - 3, square.W + 2 * gap + 6, square.H + 2 * gap + 6);
            for (var side = 0; side < 4; side++)
            {
                var vertical = side % 2 == 1;
                var along = vertical ? ring.H : ring.W;
                var @fixed = side == 0 ? ring.Y : side == 1 ? ring.X + ring.W - 3 : side == 2 ? ring.Y + ring.H - 3 : ring.X;
                var start = vertical ? ring.Y : ring.X;
                var step = rng.Next() < 0.4 ? (rng.Int(2) != 0 ? 2 : -2) : 0;
                var half = along / 2;
                foreach (var (from, to, off) in new[] { (start, start + half + 3, 0), (start + half, start + along, step) })
                {
                    roads.Add(new PlanRoad { R = vertical ? Rect(@fixed + off, from, 3, to - from) : Rect(from, @fixed + off, to - from, 3), Vertical = vertical });
                }
                if (step != 0) roads.Add(new PlanRoad { R = vertical ? Rect(Math.Min(@fixed, @fixed + step), start + half, 3 + Math.Abs(step), 3) : Rect(start + half, Math.Min(@fixed, @fixed + step), 3, 3 + Math.Abs(step)), Vertical = !vertical });
            }
            var clipped = roads.Select((road) => new PlanRoad { R = Clip(road.R, W, H), Vertical = road.Vertical, Avenue = road.Avenue }).Where((road) => road.R.W > 0 && road.R.H > 0).ToList();
            return (clipped, square);
        }

        private static PlanRect Clip(PlanRect r, int W, int H)
        {
            var x = Math.Max(0, r.X);
            var y = Math.Max(0, r.Y);
            return Rect(x, y, Math.Min(W, r.X + r.W) - x, Math.Min(H, r.Y + r.H) - y);
        }

        /// <summary>The biggest rectangle of tiles where `mask` holds (row-major, W × H), at least `min` a side, or null.</summary>
        private static PlanRect? LargestRect(byte[] mask, int W, int H, int min)
        {
            var heights = new int[W];
            PlanRect? found = null;
            var area = 0;
            var stack = new List<int>();
            for (var y = 0; y < H; y++)
            {
                for (var x = 0; x < W; x++) heights[x] = mask[y * W + x] != 0 ? heights[x] + 1 : 0;
                stack.Clear();
                for (var x = 0; x <= W; x++)
                {
                    var h = x < W ? heights[x] : 0;
                    while (stack.Count > 0 && heights[stack[stack.Count - 1]] >= h)
                    {
                        var top = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        var height = heights[top];
                        var left = stack.Count > 0 ? stack[stack.Count - 1] + 1 : 0;
                        var width = x - left;
                        if (height * width > area && height >= min && width >= min)
                        {
                            area = height * width;
                            found = Rect(left, y - height + 1, width, height);
                        }
                    }
                    stack.Add(x);
                }
            }
            return found;
        }

        /// <summary>`mask` cut into rectangles, the biggest first, down to `min` a side (what is left stays in `mask`).</summary>
        public static List<PlanRect> RectsOf(byte[] mask, int W, int H, int min)
        {
            var @out = new List<PlanRect>();
            for (var r = LargestRect(mask, W, H, min); r != null; r = LargestRect(mask, W, H, min))
            {
                @out.Add(r);
                for (var y = r.Y; y < r.Y + r.H; y++) Array.Clear(mask, y * W + r.X, r.W);
            }
            return @out;
        }

        /// <summary>The roads' ends on the map's edge: each stretch that touches a side, by that side.</summary>
        private static List<Entry> RoadEnds(List<PlanRoad> roads, int W, int H)
        {
            var ends = new List<Entry>();
            foreach (var road in roads)
            {
                var r = road.R;
                if (road.Vertical)
                {
                    if (r.Y == 0) ends.Add(new Entry { Side = 0, Road = road });
                    if (r.Y + r.H == H) ends.Add(new Entry { Side = 2, Road = road });
                }
                else
                {
                    if (r.X == 0) ends.Add(new Entry { Side = 3, Road = road });
                    if (r.X + r.W == W) ends.Add(new Entry { Side = 1, Road = road });
                }
            }
            return ends;
        }

        /// <summary>The middle of an end of a road on the map's edge, on that edge.</summary>
        private static Vec2 EndAt(Entry e, int W, int H)
        {
            var r = e.Road.R;
            var mx = r.X + r.W / 2;
            var my = r.Y + r.H / 2;
            return e.Side == 0 ? V(mx, 0) : e.Side == 1 ? V(W - 1, my) : e.Side == 2 ? V(mx, H - 1) : V(0, my);
        }

        /// <summary>Kinds of closed ground along a town's edge, and how often each.</summary>
        private static readonly IReadOnlyList<(string, int)> Closed = new[]
        {
            (FrameKinds.Block, 50),
            (FrameKinds.Lot, 20),
            (FrameKinds.Ruin, 15),
            (FrameKinds.Works, 15),
        };
        /// <summary>What runs along one side, at most once a map, and how often each.</summary>
        private static readonly IReadOnlyList<(string, int)> Linear = new[]
        {
            (FrameKinds.River, 35),
            (FrameKinds.Rail, 25),
            (FrameKinds.Highway, 20),
            (FrameKinds.Barrier, 20),
        };
        /// <summary>An industrial estate's: container yards and fenced lots, works, a few sheds shut up; the railway or the motorway.</summary>
        private static readonly IReadOnlyList<(string, int)> EstateClosed = new[]
        {
            (FrameKinds.Containers, 35),
            (FrameKinds.Lot, 25),
            (FrameKinds.Works, 20),
            (FrameKinds.Block, 20),
        };
        private static readonly IReadOnlyList<(string, int)> EstateLinear = new[]
        {
            (FrameKinds.Rail, 45),
            (FrameKinds.Highway, 35),
            (FrameKinds.Barrier, 20),
        };
        /// <summary>How deep a run of block along the edge is closed (closeEdges' weighted styles).</summary>
        private static readonly IReadOnlyList<(string, int)> EdgeStyles = new[]
        {
            ("thin", 40),
            ("half", 40),
            ("whole", 20),
        };

        /// <summary>
        /// The plan of a town of W × H: a grid's or an old town's streets, the outline round what is played, the ways in, and
        /// the blocks left to build on. The squad comes in from the north or the west, near the north-west corner (the map is
        /// turned at random afterwards).
        /// </summary>
        public static TownPlan TownPlan(int W, int H, Rng rng, bool old, bool wide = false, bool estate = false)
        {
            List<PlanRoad> roads;
            PlanRect? square;
            if (old) (roads, square) = OldTownRoads(W, H, rng);
            else (roads, square) = (GridRoads(W, H, rng, wide, estate), null);
            var N = W * H;
            var road = new byte[N];
            foreach (var rd in roads) for (var y = rd.R.Y; y < rd.R.Y + rd.R.H; y++) for (var x = rd.R.X; x < rd.R.X + rd.R.W; x++) road[y * W + x] = 1;
            // `road[i]` as TypeScript reads it: undefined (falsy) off the array.
            bool RoadAt(int i) => i >= 0 && i < N && road[i] != 0;

            // ---------------------------------------------------------------- the ways in
            var ends = RoadEnds(roads, W, H);
            int Near(Entry e)
            {
                var p = EndAt(e, W, H);
                return p.X + p.Y;
            }
            // The squad's: on the north or the west side, nearest the corner (an avenue's, only if there is no other).
            var west = ends.Where((e) => e.Side == 0 || e.Side == 3).ToList();
            Js.Sort(west, (a, b) =>
            {
                var c = (a.Road.Avenue == true ? 1 : 0) - (b.Road.Avenue == true ? 1 : 0);
                return c != 0 ? c : Near(a) - Near(b);
            });
            var squadEntry = west.Count > 0 ? west[0] : ends[0];
            var sp = EndAt(squadEntry, W, H);
            // A river, the railway, the motorway or a barrier along one side (never the squad's).
            var wanted = rng.Next() < (old ? 0.35 : 0.5) ? Weighted(rng, estate ? EstateLinear : Linear) : null;
            // Never the squad's side, nor where the avenue goes out (the bus's way).
            var avenueSides = new HashSet<int>(ends.Where((e) => e.Road.Avenue == true && e != squadEntry).Select((e) => e.Side));
            var lineSides = new[] { 0, 1, 2, 3 }.Where((s) => s != squadEntry.Side && !avenueSides.Contains(s)).ToList();
            var lineSide = wanted != null && lineSides.Count > 0 ? rng.Pick(lineSides) : -1;
            var linear = lineSide >= 0 ? wanted : null;
            bool Crosses(Entry e) => e.Side == lineSide && linear != FrameKinds.Barrier;
            // The others: far from the squad's and from each other, the avenue's far end first (the bus leaves by it).
            int Apart(Entry a, Entry b)
            {
                var p = EndAt(a, W, H);
                var q = EndAt(b, W, H);
                return Math.Abs(p.X - q.X) + Math.Abs(p.Y - q.Y);
            }
            var others = ends.Where((e) => e != squadEntry && !Crosses(e) && Apart(e, squadEntry) >= 0.6 * Math.Max(W, H)).ToList();
            // The comparator draws from the Rng: V8's own comparisons, in order.
            Js.SortV8(others, (a, b) =>
            {
                var c = (b.Road.Avenue == true ? 1 : 0) - (a.Road.Avenue == true ? 1 : 0);
                return c != 0 ? c : Apart(b, squadEntry) - Apart(a, squadEntry) + rng.Next() * 8;
            });
            var entries = new List<Entry> { squadEntry };
            var want = rng.Range(2, 4);
            foreach (var e in others)
            {
                if (entries.Count >= want) break;
                if (entries.All((o) => Apart(o, e) >= 16)) entries.Add(e);
            }
            // The avenue always goes out somewhere (the bus leaves by it): its end furthest from the squad's, if none is in.
            if (!entries.Any((e) => e.Road.Avenue == true))
            {
                var outward = ends.Where((e) => e.Road.Avenue == true && e != squadEntry && !Crosses(e) && EndAt(e, W, H).X + EndAt(e, W, H).Y != sp.X + sp.Y).ToList();
                Js.Sort(outward, (a, b) => Apart(b, squadEntry) - Apart(a, squadEntry));
                var far = Js.At<Entry?>(outward, 0, null);
                if (far != null)
                {
                    if (entries.Count >= 4) entries[entries.Count - 1] = far;
                    else entries.Add(far);
                }
            }

            // ------------------------------------------------------------- the outline
            // Each tile out of play gets a kind (an index into kinds) and storeys; 0 is in play.
            var kinds = new List<string>(); // FrameKinds
            var kindOf = new short[N];
            for (var i = 0; i < N; i++) kindOf[i] = -1;
            var storeysOf = new byte[N];
            void Close(int x, int y, int kind, int storeys)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                var i = y * W + x;
                if (kindOf[i] >= 0) return;
                kindOf[i] = (short)kind;
                storeysOf[i] = (byte)storeys;
            }
            int KindIndex(string k)
            {
                var i = kinds.IndexOf(k);
                if (i >= 0) return i;
                kinds.Add(k);
                return kinds.Count - 1;
            }
            // (TypeScript's `depth(s, x, y)`, the depth in from side `s` of a tile, is never called: left out.)
            // The tile `d` in from side `s`, `u` along it.
            (int X, int Y) At(int s, int u, int d) => s == 0 ? (u, d) : s == 1 ? (W - 1 - d, u) : s == 2 ? (u, H - 1 - d) : (d, u);
            int Along(int s) => s % 2 == 0 ? W : H;
            bool IsEntry(int x, int y) => entries.Any((e) => Inside(e.Road.R, x, y));

            // The linear piece: from the edge to two tiles short of the first street along that side (a quay, a pavement),
            // in stretches of 10–16 each a tile or two shallower now and then; every street that crosses it ends there.
            if (linear != null)
            {
                var k = KindIndex(linear);
                var parallel = roads.Where((r) => r.Vertical == (lineSide % 2 == 1) && !CrossesSide(r, lineSide, W, H)).ToList();
                double GapTo(int u)
                {
                    var g = double.PositiveInfinity;
                    foreach (var r in parallel)
                    {
                        var p = At(lineSide, u, 0);
                        var spans = lineSide % 2 == 0 ? InRange(p.X, r.R.X, r.R.W) : InRange(p.Y, r.R.Y, r.R.H);
                        if (!spans) continue;
                        var d = lineSide == 0 ? r.R.Y : lineSide == 1 ? W - r.R.X - r.R.W : lineSide == 2 ? H - r.R.Y - r.R.H : r.R.X;
                        if (d >= 0) g = Math.Min(g, d);
                    }
                    return g;
                }
                var storeys = linear == FrameKinds.Highway ? 2 : linear == FrameKinds.Rail ? 1 : 0;
                for (var u = 0; u < Along(lineSide);)
                {
                    var run = rng.Range(10, 16);
                    var trim = rng.Next() < 0.4 ? rng.Range(1, 2) : 0;
                    // With no street near that side, a band of its own depth.
                    var own = rng.Range(8, 11);
                    for (var v = u; v < Math.Min(Along(lineSide), u + run); v++)
                    {
                        var g = GapTo(v);
                        var d = Math.Min(g, Math.Max(6, (g <= 20 ? g - 2 : own) - trim));
                        for (var t = 0; t < d; t++)
                        {
                            var p = At(lineSide, v, t);
                            if (linear == FrameKinds.Barrier && IsEntry(p.X, p.Y)) continue;
                            Close(p.X, p.Y, k, storeys);
                        }
                    }
                    u += run;
                }
            }

            // Blocks shut up, lots, ruins and works along the other sides: in runs between the streets that meet the edge, a
            // depth of their own (a back wall two or three in, half a block, or all of it), the corners from both sides.
            // Far from the squad's way in, the blocks keep their ground (only a back wall): the objective is out there.
            bool FarFrom((int X, int Y) p) => Math.Abs(p.X - sp.X) + Math.Abs(p.Y - sp.Y) > 0.9 * Math.Max(W, H);
            // Should that leave too little to play (PLAYED_LEAST), the edge is closed again by back walls only.
            var bandedKinds = (short[])kindOf.Clone();
            var bandedStoreys = (byte[])storeysOf.Clone();
            for (var pass = 0; pass < 2; pass++)
            {
                if (pass != 0)
                {
                    var n = 0;
                    foreach (var k in kindOf) n += k < 0 ? 1 : 0;
                    var played = (double)n / N;
                    if (played >= PlayedLeast) break;
                    Array.Copy(bandedKinds, kindOf, N);
                    Array.Copy(bandedStoreys, storeysOf, N);
                }
                CloseEdges(pass == 1);
            }
            void CloseEdges(bool thinOnly)
            {
                for (var s = 0; s < 4; s++)
                {
                    if (s == lineSide) continue;
                    var u = 0;
                    while (u < Along(s))
                    {
                        var p = At(s, u, 0);
                        if (road[p.Y * W + p.X] != 0)
                        {
                            u++;
                            continue;
                        }
                        // A run of block along the edge, to the next street or 8–16 long.
                        var end = u;
                        var most = u + rng.Range(8, 16);
                        while (end < Along(s) && end < most && !RoadAt(At(s, end, 0).Y * W + At(s, end, 0).X)) end++;
                        // Stop short of a street's end only where the rest is too short to stand alone.
                        while (end < Along(s) && !RoadAt(At(s, end, 0).Y * W + At(s, end, 0).X) && end - most < 6 && RunLeft(s, end) < 6) end++;
                        var style = thinOnly || FarFrom(At(s, (u + end) / 2, 0)) ? "thin" : Weighted(rng, EdgeStyles);
                        var thin = rng.Range(EdgeShut, 3);
                        var wanted = style == "thin" ? thin : style == "half" ? rng.Range(4, 6) : double.PositiveInfinity;
                        var k = KindIndex(Weighted(rng, estate ? EstateClosed : Closed));
                        var kind = kinds[k];
                        var storeys = kind == FrameKinds.Block ? rng.Range(2, 4) : kind == FrameKinds.Ruin ? rng.Range(1, 3) : kind == FrameKinds.Works ? rng.Range(1, 2) : 0;
                        for (var v = u; v < end; v++)
                        {
                            // How deep the block runs here: to the next street in.
                            var room = 0;
                            while (room < Math.Max(W, H))
                            {
                                var q = At(s, v, room);
                                if (q.X < 0 || q.Y < 0 || q.X >= W || q.Y >= H || road[q.Y * W + q.X] != 0) break;
                                room++;
                            }
                            // Closed whole if so meant (a block not too deep); else what was meant if enough is left in front, else a back
                            // wall and a narrow yard; what is too thin even for that, closed.
                            var kept = thinOnly ? 4 : MinKept;
                            var d = style == "whole" && room <= WholeMax ? room : room - wanted >= kept ? wanted : room - thin >= kept ? thin : room;
                            for (var t = 0; t < d; t++)
                            {
                                var q = At(s, v, t);
                                Close(q.X, q.Y, k, storeys);
                            }
                        }
                        u = end;
                    }
                }
            }
            int RunLeft(int s, int from)
            {
                var n = 0;
                while (from + n < Along(s) && !RoadAt(At(s, from + n, 0).Y * W + At(s, from + n, 0).X)) n++;
                return n;
            }

            // A street that meets the edge and isn't a way in: shut as deep as the blocks beside it are (a barricade there, no
            // dead end between walls), the street going on beyond it.
            var street = KindIndex(FrameKinds.Street);
            foreach (var e in ends)
            {
                if (entries.Contains(e) || Crosses(e)) continue;
                var r = e.Road.R;
                var width = e.Side % 2 == 0 ? r.W : r.H;
                var first = e.Side % 2 == 0 ? r.X : r.Y;
                // How deep its neighbours are shut, just beside it.
                int ShutAt(int u)
                {
                    var d = 0;
                    while (d < Math.Max(W, H))
                    {
                        var q = At(e.Side, u, d);
                        if (q.X < 0 || q.Y < 0 || q.X >= W || q.Y >= H || kindOf[q.Y * W + q.X] < 0) break;
                        d++;
                    }
                    return d;
                }
                // No further than its own stretch, short of the street it meets (a whole row of blocks shut beside it is no
                // reason to shut the street across the map), and never a way in.
                var sides = new[] { ShutAt(first - 1), ShutAt(first + width) }.Where((d) => d > 0).ToList();
                var span = e.Side % 2 == 0 ? r.H : r.W;
                var d = Math.Max(EdgeShut, Math.Min(Math.Max(EdgeShut, sides.Count > 0 ? sides.Max() : EdgeShut), span - Street - 1));
                for (var u = first; u < first + width; u++)
                {
                    for (var t = 0; t < d; t++)
                    {
                        var q = At(e.Side, u, t);
                        if (RoadAt(q.Y * W + q.X) && !IsEntry(q.X, q.Y)) Close(q.X, q.Y, street, 0);
                    }
                }
            }
            // Nothing played on the edge but the ways in: any tile left there is shut with what is next to it.
            for (var pass = 0; pass < EdgeShut; pass++)
            {
                for (var y = 0; y < H; y++)
                {
                    for (var x = 0; x < W; x++)
                    {
                        var i = y * W + x;
                        if (kindOf[i] >= 0 || IsEntry(x, y)) continue;
                        if (Math.Min(Math.Min(x, y), Math.Min(W - 1 - x, H - 1 - y)) >= EdgeShut) continue;
                        Vec2? by = null;
                        foreach (var dir in Dirs)
                        {
                            var q = V(x + dir.X, y + dir.Y);
                            if (q.X >= 0 && q.Y >= 0 && q.X < W && q.Y < H && kindOf[q.Y * W + q.X] >= 0)
                            {
                                by = q;
                                break;
                            }
                        }
                        if (by != null) Close(x, y, kindOf[by.Y * W + by.X], storeysOf[by.Y * W + by.X]);
                        else if (pass == EdgeShut - 1) Close(x, y, KindIndex(road[i] != 0 ? FrameKinds.Street : FrameKinds.Lot), 0);
                    }
                }
            }

            // The outline in rectangles, one piece per tile: by kind and storeys.
            var @out = new bool[N];
            for (var i = 0; i < N; i++) @out[i] = kindOf[i] >= 0;
            var frame = new List<FramePiece>();
            var keys = new JsSet<int>();
            for (var i = 0; i < N; i++) if (kindOf[i] >= 0) keys.Add(kindOf[i] * 256 + storeysOf[i]);
            var sorted = keys.ToList();
            Js.Sort(sorted, (a, b) => a - b);
            foreach (var key in sorted)
            {
                var mask = new byte[N];
                for (var i = 0; i < N; i++) if (kindOf[i] >= 0 && kindOf[i] * 256 + storeysOf[i] == key) mask[i] = 1;
                var kind = kinds[key / 256];
                var storeys = key % 256;
                foreach (var r in RectsOf(mask, W, H, 1))
                {
                    frame.Add(new FramePiece { Kind = kind, X = r.X, Y = r.Y, W = r.W, H = r.H, Side = kind == linear ? lineSide : (int?)null, Storeys = storeys != 0 ? storeys : (int?)null });
                }
            }

            // --------------------------------------------------------------- the blocks
            // A street the outline shut is shut: what is left of the roads is played.
            var kept = roads.Where((r) => !Shut(r.R, @out, W)).ToList();
            var free = new byte[N];
            for (var i = 0; i < N; i++) free[i] = !@out[i] && road[i] == 0 ? (byte)1 : (byte)0;
            if (square != null) for (var y = square.Y; y < square.Y + square.H; y++) for (var x = square.X; x < square.X + square.W; x++) free[y * W + x] = 0;
            // An old town's biggest blocks sewn through by an alley, across their length.
            if (old)
            {
                foreach (var b in RectsOf((byte[])free.Clone(), W, H, MinBlock))
                {
                    var @long = Math.Max(b.W, b.H);
                    if (@long < 26 || Math.Min(b.W, b.H) < 10) continue;
                    var vertical = b.W > b.H;
                    var a = (vertical ? b.X : b.Y) + @long / 2 + rng.Range(-3, 3);
                    var alley = new PlanRoad { R = vertical ? Rect(a, b.Y, Alley, b.H) : Rect(b.X, a, b.W, Alley), Vertical = vertical };
                    kept.Add(alley);
                    for (var y = alley.R.Y; y < alley.R.Y + alley.R.H; y++) for (var x = alley.R.X; x < alley.R.X + alley.R.W; x++) free[y * W + x] = 0;
                }
            }
            var blocks = RectsOf(free, W, H, MinBlock);

            // Where the squad gathers: on its street, four tiles in from the edge.
            var squad = squadEntry.Side == 0 ? V(sp.X, 4) : V(4, sp.Y);
            return new TownPlan { Width = W, Height = H, Roads = kept, Out = @out, Frame = frame, Blocks = blocks, Square = square, Entries = entries, SquadEntry = squadEntry, Squad = squad };
        }

        private static bool InRange(int v, int from, int n) => v >= from && v < from + n;
        private static bool Inside(PlanRect r, int x, int y) => InRange(x, r.X, r.W) && InRange(y, r.Y, r.H);

        /// <summary>Whether a road runs along (not across) side `s`'s edge: a street parallel to it never crosses it.</summary>
        private static bool CrossesSide(PlanRoad road, int s, int W, int H)
        {
            var r = road.R;
            return s == 0 ? r.Y == 0 && road.Vertical : s == 1 ? r.X + r.W == W && !road.Vertical : s == 2 ? r.Y + r.H == H && road.Vertical : r.X == 0 && !road.Vertical;
        }

        /// <summary>Whether all of a road's rectangle is out of play.</summary>
        private static bool Shut(PlanRect r, bool[] @out, int W)
        {
            for (var y = r.Y; y < r.Y + r.H; y++) for (var x = r.X; x < r.X + r.W; x++) if (!@out[y * W + x]) return false;
            return true;
        }
    }
}
