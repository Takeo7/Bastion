using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.Client.Map
{
    /// <summary>
    /// The outline round what is played (0.10.1's frameArt.ts and the parts of dressing.ts it uses; UI's): the engine
    /// shuts a map's edge off with the pieces of GameState.frame (every tile of them a wall out of play, ground 4) and says
    /// what each piece is. Where a piece meets the ground that is played, its line: a lot's hoarding or its wall, a ruin's
    /// heaps, the hoarding round works, the closure across a street that goes on, a cordon's T-walls. Inside it, and on past
    /// the map's edge, what fills it; the river, the railway or the motorway along a side as one band. The blocks shut up
    /// are the buildings' (BuildingPlan.FrameBlocks). In 0.10.1's frame and tiles (x east, z south): Synty's props by id,
    /// and boxes, wedges and water of its own in flat colours. Always the same for a map.
    /// </summary>
    public static class FrameArt
    {
        /// <summary>A prop by id at a matrix (0.10.1's frame, tiles), and whether it casts a shadow.</summary>
        public struct Dressing
        {
            public string Id;
            public Matrix4x4 Matrix;
            public bool Cast;
        }

        /// <summary>A piece of the outline's own geometry: a unit box or wedge (its base on y = 0, the wedge rising to +z) or a square of water, by `Matrix`, in a colour (sRGB).</summary>
        public struct Shape
        {
            public string Form;
            public Matrix4x4 Matrix;
            public int Color;
        }

        public const string Box = "box";
        public const string Wedge = "wedge";
        public const string Water = "water";

        /// <summary>Tiles past the map's edge the outline goes on (the haze takes it from there).</summary>
        public const int Beyond = 6;
        /// <summary>How far the band's geometry runs on along its side past the map's corners (tiles), in stretches of four.</summary>
        const int Along = 24;
        const int Stretch = 4;
        /// <summary>What stands past the edge keeps this much clear of the edge tile (tiles).</summary>
        const double EdgeClear = 0.02;

        /// <summary>
        /// Half the size of the props the edge is shut with (tiles, along their own x and z, as they are fitted), so none of
        /// them reaches over the edge tile, where units walk.
        /// </summary>
        static readonly Dictionary<string, (double x, double z)> Half = new Dictionary<string, (double, double)>
        {
            ["rubble1"] = (0.475, 0.402), ["rubble2"] = (0.475, 0.423), ["razorWire"] = (0.5, 0.316), ["trashPile"] = (0.343, 0.525), ["tirePile"] = (0.261, 0.5),
            ["bush"] = (0.45, 0.237), ["boulder"] = (0.475, 0.665), ["rock"] = (0.443, 0.475), ["cliff"] = (0.2, 0.7), ["cliff2"] = (0.237, 0.7), ["log"] = (0.412, 0.5),
            ["sheetFence"] = (0.878, 0.124), ["sheetFence2"] = (0.836, 0.113), ["concreteFence"] = (0.833, 0.083), ["milBarrierTall"] = (0.203, 0.5),
            ["container"] = (0.988, 2.425), ["containerBlue"] = (0.988, 2.425), ["containerOrange"] = (0.988, 2.425), ["debrisPile"] = (2.704, 1.266),
            ["debrisPile2"] = (2.099, 1.252), ["bus"] = (3.809, 1.079), ["sandbags"] = (0.5, 0.279), ["barricade1"] = (0.5, 0.063), ["carWreck"] = (0.41, 0.975),
            ["carRusted"] = (0.41, 0.975), ["tire"] = (0.275, 0.275),
        };

        static readonly string[] Pines = { "pineLarge", "pineLarge2", "treePine" };
        static readonly string[] Mixed = { "tree", "pineLarge", "treePine", "pineLarge2", "tree" };
        static readonly string[] Colours = { "container", "containerBlue", "containerOrange" };

        struct V2
        {
            public int x, y;
            public V2(int x, int y) { this.x = x; this.y = y; }
        }

        // ------------------------------------------------------------ the outline

        /// <summary>What closes the map: each piece's line where it meets what is played, and what fills it, to the edge and past it.</summary>
        public static (List<Dressing> props, List<Shape> shapes) Dress(GameState s)
        {
            var frame = s.Frame ?? new List<FramePiece>();
            var shapes = new List<Shape>();
            if (frame.Count == 0) return (new List<Dressing>(), shapes);
            int w = s.Width, h = s.Height;
            var at = MapOutline.FrameOf(s);
            bool Played(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && at[y * w + x] < 0;
            var output = new List<Dressing>();
            // Each run of a line with its length: the longest first (Apart keeps them whole where two meet).
            var runs = new List<(int n, List<Dressing> props)>();
            var band = MapOutline.BandOf(s);
            foreach (var p in frame)
            {
                Func<double, double> r = salt => Hash(p.X * 7 + p.W, p.Y * 7 + p.H, salt);
                // Its line: on each side, the runs of its edge tiles with played ground across; `a` and `b` the played tiles
                // at the run's ends, `inward` the way into the piece from them.
                for (int side = 0; side < 4; side++)
                {
                    var d = Grid.Dirs4[side];
                    var along = d.X != 0 ? new V2(0, 1) : new V2(1, 0);
                    int n = d.X != 0 ? p.H : p.W;
                    var first = new V2(d.X > 0 ? p.X + p.W - 1 : p.X, d.Y > 0 ? p.Y + p.H - 1 : p.Y);
                    int start = -1;
                    for (int k = 0; k <= n; k++)
                    {
                        var t = new V2(first.x + along.x * k, first.y + along.y * k);
                        if (k < n && Played(t.x + d.X, t.y + d.Y))
                        {
                            if (start < 0) start = k;
                            continue;
                        }
                        if (start >= 0)
                        {
                            var a = new V2(first.x + along.x * start + d.X, first.y + along.y * start + d.Y);
                            var b = new V2(first.x + along.x * (k - 1) + d.X, first.y + along.y * (k - 1) + d.Y);
                            // On round a corner of the outline (the next tile along is out of play), so its line meets the next.
                            var ends = (Played(a.x - along.x, a.y - along.y) ? 0 : 1, Played(b.x + along.x, b.y + along.y) ? 0 : 1);
                            int sideNow = side;
                            var aa = a;
                            runs.Add((k - start, Line(s, p, a, b, new V2(-d.X, -d.Y), salt => Hash(aa.x * 3 + sideNow, aa.y * 3 + sideNow, salt), shapes, ends)));
                        }
                        start = -1;
                    }
                }
                output.AddRange(Fill(s, p, r, Played, band));
            }
            var lines = runs.Select((run, i) => (run, i)).OrderByDescending(x => x.run.n).ThenBy(x => x.i).SelectMany(x => x.run.props).ToList();
            output.InsertRange(0, lines);
            if (band != null && (band.Kind == FrameKinds.Water || band.Kind == FrameKinds.Ravine)) DrawSunken(s, band, output, shapes);
            else if (band != null) DrawBand(s, band, output, shapes);
            return (Apart(output), shapes);
        }

        /// <summary>A piece's line from played tile `a` to `b` (along its side), `inward` into the piece: what keeps anybody out.</summary>
        static List<Dressing> Line(GameState s, FramePiece p, V2 a, V2 b, V2 inward, Func<double, double> r, List<Shape> shapes, (int, int) ends)
        {
            var wired = (wired: s.Biome == Biomes.Facility, pines: false);
            var wood = (wired: false, pines: Pinewood(s));
            switch (p.Kind)
            {
                case FrameKinds.Forest:
                    // The wood closing in, now and then trees felled across the way.
                    return FenceOff(a, b, inward, r(4) < 0.18 ? 7 : 5, ends, r, wood);
                case FrameKinds.Rocks:
                    return FenceOff(a, b, inward, 6, ends, r, wood);
                case FrameKinds.Containers:
                    // (Not on round a corner: the next side's containers stand there, across these.)
                    return FenceOff(a, b, inward, 8, (0, 0), r, wired);
                case FrameKinds.Ravine:
                case FrameKinds.Water:
                    return Brink(a, b, inward, p.Kind == FrameKinds.Water, r);
                case FrameKinds.River:
                    // A canal's parapet, a railing on it: its water is seen over it.
                    Parapet(a, b, inward, shapes);
                    return new List<Dressing>();
                case FrameKinds.Highway:
                    return FenceOff(a, b, inward, 3, ends, r, wired);
                case FrameKinds.Street:
                    // The way shut where play ends: T-walls, a bus across it, or burnt-out cars and planks.
                    return Shut(a, b, inward, (int)Math.Floor(r(1) * 3), r);
                case FrameKinds.Lot:
                    // A hoarding of sheet metal, or a precast wall.
                    return FenceOff(a, b, inward, r(2) < 0.55 ? 1 : 2, ends, r, wired);
                case FrameKinds.Works:
                    return FenceOff(a, b, inward, 1, ends, r, wired);
                case FrameKinds.Ruin:
                    // A block that came down: its heaps along the way.
                    return FenceOff(a, b, inward, 0, ends, r, wired);
                case FrameKinds.Barrier:
                    return FenceOff(a, b, inward, 3, ends, r, wired);
                case FrameKinds.Block:
                    return Thin(p) ? FenceOff(a, b, inward, 2, ends, r, wired) : new List<Dressing>();
                default:
                    // The railway: a precast wall at the foot of its embankment.
                    return FenceOff(a, b, inward, 2, ends, r, wired);
            }
        }

        /// <summary>A block too thin to have an inside: walled off like a lot (the buildings lay the others).</summary>
        static bool Thin(FramePiece p) => p.Kind == FrameKinds.Block && (p.W < 3 || p.H < 3);

        /// <summary>Whether the map's woods are pines (a facility's, the forest road's) or mixed.</summary>
        static bool Pinewood(GameState s) => s.Biome == Biomes.Facility || s.Site == Sites.Roadside;

        /// <summary>Along a ravine's rim or a river's bank: low, so one sees over it (boulders and brush; rushes, brush and a stone).</summary>
        static List<Dressing> Brink(V2 a, V2 b, V2 inward, bool water, Func<double, double> r)
        {
            var list = new List<Dressing>();
            var along = new V2(Math.Sign(b.x - a.x) != 0 ? Math.Sign(b.x - a.x) : Math.Abs(inward.y), Math.Sign(b.y - a.y) != 0 ? Math.Sign(b.y - a.y) : Math.Abs(inward.x));
            int length = Math.Abs(b.x - a.x) + Math.Abs(b.y - a.y) + 1;
            int i = 0;
            for (double t = 0; t < length; t += 0.9 + r(600 + i) * 0.6, i++)
            {
                double beyond = 0.75 + r(610 + i) * 0.5;
                double x = a.x + 0.5 + along.x * (t - 0.5) + inward.x * beyond;
                double z = a.y + 0.5 + along.y * (t - 0.5) + inward.y * beyond;
                double turn = r(620 + i) * Math.PI * 2;
                double k = r(630 + i);
                string id = water ? (k < 0.55 ? "tuftWide" : k < 0.85 ? "bush" : "rock") : k < 0.45 ? "boulder" : k < 0.75 ? "rock" : "bush";
                double size = id == "tuftWide" ? 1.4 + r(640 + i) * 0.8 : id == "boulder" ? 0.7 + r(640 + i) * 0.5 : 0.8 + r(640 + i) * 0.4;
                list.Add(Placed(id, x, 0, z, Math.Cos(turn), Math.Sin(turn), id != "tuftWide", 1, size));
            }
            return list;
        }

        /// <summary>A shape at (x, y, z), its own x along `yaw` (about y), sized (along, tall, across).</summary>
        static Shape MakeShape(string form, double x, double y, double z, double yaw, double along, double tall, double across, int color) =>
            new Shape { Form = form, Color = color, Matrix = Matrix4x4.TRS(new Vector3((float)x, (float)y, (float)z), Yaw(yaw), new Vector3((float)along, (float)tall, (float)across)) };

        /// <summary>A canal's parapet from played tile `a` to `b`, `inward` the water's way: a low concrete wall and its railing.</summary>
        static void Parapet(V2 a, V2 b, V2 inward, List<Shape> shapes)
        {
            var along = new V2(Math.Sign(b.x - a.x) != 0 ? Math.Sign(b.x - a.x) : Math.Abs(inward.y), Math.Sign(b.y - a.y) != 0 ? Math.Sign(b.y - a.y) : Math.Abs(inward.x));
            int length = Math.Abs(b.x - a.x) + Math.Abs(b.y - a.y) + 1;
            double yaw = Math.Atan2(-along.y, along.x);
            double mx = (a.x + b.x) / 2.0 + 0.5 + inward.x * 0.62, mz = (a.y + b.y) / 2.0 + 0.5 + inward.y * 0.62;
            shapes.Add(MakeShape(Box, mx, 0, mz, yaw, length, 0.42, 0.24, 0xb6b0a4));
            shapes.Add(MakeShape(Box, mx, 0.42, mz, yaw, length, 0.06, 0.3, 0xc8c2b6));
            shapes.Add(MakeShape(Box, mx, 0.86, mz, yaw, length, 0.05, 0.06, 0x3d4246));
            for (int t = 0; t <= length; t++)
            {
                double x = a.x + 0.5 + inward.x * 0.62 + along.x * (t - 0.5);
                double z = a.y + 0.5 + inward.y * 0.62 + along.y * (t - 0.5);
                shapes.Add(MakeShape(Box, x, 0.48, z, yaw, 0.05, 0.4, 0.05, 0x3d4246));
            }
        }

        static int StoreysOf(GameState s, string kind, int otherwise) => s.Frame?.FirstOrDefault(p => p.Kind == kind)?.Storeys ?? otherwise;

        /// <summary>
        /// The river, the railway or the motorway along its side, as one straight band, from far past one corner to far past
        /// the other: the canal's walls and water (its hole in the ground is the ground's); the embankment with its tracks,
        /// a train of containers and the wires' posts; the motorway's deck on its piers, its barriers and lanes, its wrecks.
        /// </summary>
        static void DrawBand(GameState s, MapOutline.Band band, List<Dressing> props, List<Shape> shapes)
        {
            int w = s.Width, h = s.Height;
            int side = band.Side, D = band.Depth;
            int n = side % 2 != 0 ? h : w;
            // Its frame: u along the side, v in from the edge (past it, below 0); local x along, local z inwards.
            double yaw = new[] { 0, -Math.PI / 2, Math.PI, Math.PI / 2 }[side];
            (double, double) World(double u, double v) => side == 0 ? (u, v) : side == 1 ? (w - v, u) : side == 2 ? (u, h - v) : (v, u);
            var zero = World(0, 0);
            double inX = World(0, 1).Item1 - zero.Item1, inZ = World(0, 1).Item2 - zero.Item2;
            double alX = World(1, 0).Item1 - zero.Item1, alZ = World(1, 0).Item2 - zero.Item2;
            void Put(string form, double u, double v, double y, double along, double tall, double across, int color, bool flip = false)
            {
                var (x, z) = World(u, v);
                shapes.Add(MakeShape(form, x, y, z, yaw + (flip ? Math.PI : 0), along, tall, across, color));
            }
            double R(double k, double salt) => Hash(k, side * 31 + D, salt);
            for (int u0 = -Along; u0 < n + Along; u0 += Stretch)
            {
                double u = u0 + Stretch / 2.0;
                if (band.Kind == FrameKinds.River)
                {
                    // The water down between the walls, from the near wall to the far bank's.
                    Put(Water, u, (D - MapOutline.FarBank) / 2.0, -1.15, Stretch, 1, D + MapOutline.FarBank, 0x3e524c);
                    foreach (var (v, k) in new[] { (D + 0.12, 1), (-MapOutline.FarBank - 0.12, -1) })
                    {
                        Put(Box, u, v, -1.4, Stretch, 1.42, 0.24, 0x9f998d);
                        Put(Box, u, v + k * 0.04, 0, Stretch, 0.08, 0.36, 0xbcb6aa);
                    }
                }
                else if (band.Kind == FrameKinds.Rail)
                {
                    // The embankment: slopes either side of a crest of ballast, the edge of the map on its far slope.
                    double H = 0.6 * StoreysOf(s, FrameKinds.Rail, 1) + 0.25;
                    double crest0 = -1, crest1 = D - 1.8;
                    Put(Wedge, u, D - 0.9, 0, Stretch, H, 1.8, 0x6e7444, true);
                    Put(Box, u, (crest0 + crest1) / 2, 0, Stretch, H, crest1 - crest0, 0x77714f);
                    Put(Wedge, u, crest0 - 0.9, 0, Stretch, H, 1.8, 0x6e7444);
                    Put(Box, u, (crest0 + crest1) / 2, H, Stretch, 0.1, crest1 - crest0 - 0.3, 0x8b857a);
                    var tracks = crest1 - crest0 >= 3.4 ? new[] { (crest0 + crest1) / 2 - 0.95, (crest0 + crest1) / 2 + 0.95 } : new[] { (crest0 + crest1) / 2 };
                    foreach (var c in tracks)
                    {
                        for (double t = 0; t < Stretch; t += 0.42) Put(Box, u0 + t + 0.21, c, H + 0.1, 0.17, 0.06, 1.5, 0x4c3c2e);
                        foreach (var g in new[] { -0.48, 0.48 }) Put(Box, u, c + g, H + 0.16, Stretch, 0.07, 0.06, 0x707070);
                    }
                    // The posts of the overhead wires, every eight tiles, an arm over the tracks.
                    if (u0 % 8 == 0)
                    {
                        double pv = crest1 - 0.25;
                        Put(Box, u0, pv, H + 0.1, 0.14, 2.7, 0.14, 0x6a6e70);
                        Put(Box, u0, (tracks[0] + pv) / 2, H + 2.6, 0.1, 0.1, pv - tracks[0] + 0.6, 0x6a6e70);
                    }
                }
                else
                {
                    // The motorway's deck, two storeys up (or its own), its barriers and lanes.
                    double Hd = StoreysOf(s, FrameKinds.Highway, 2) * BuildingPlan.Storey;
                    double v0 = -3, v1 = D - 0.2;
                    Put(Box, u, (v0 + v1) / 2, Hd - 0.5, Stretch, 0.5, v1 - v0, 0x8c8a84);
                    Put(Box, u, (v0 + v1) / 2, Hd, Stretch, 0.02, v1 - v0 - 0.4, 0x4b4b49);
                    foreach (var v in new[] { v1 - 0.18, v0 + 0.18 }) Put(Box, u, v, Hd, Stretch, 0.48, 0.3, 0xaaa69c);
                    for (int k = 1; k < Math.Floor((v1 - v0 - 0.8) / 2.4); k++) Put(Box, u, v0 + 0.4 + k * 2.4, Hd + 0.021, 1.4, 0.005, 0.09, 0xd8d4c8);
                    // Its piers every eight tiles: two columns and a beam across.
                    if (u0 % 8 == 0)
                    {
                        foreach (var v in new[] { v1 - 1.4, v0 + 1.6 }) Put(Box, u0, v, 0, 0.75, Hd - 0.9, 0.75, 0x9a978f);
                        Put(Box, u0, (v0 + v1) / 2, Hd - 1.0, 0.95, 0.5, v1 - v0 - 1.2, 0x9a978f);
                    }
                    // A wreck now and then, along its lanes.
                    if (R(u0, 1) < 0.35)
                    {
                        string id = R(u0, 2) < 0.5 ? "carRusted" : R(u0, 2) < 0.8 ? "carWreck" : "van";
                        var (x, z) = World(u + (R(u0, 3) - 0.5) * 2, v0 + 1.2 + R(u0, 4) * (v1 - v0 - 2.4));
                        int flip = R(u0, 5) < 0.5 ? 1 : -1;
                        double skew = (R(u0, 6) - 0.5) * 0.5;
                        props.Add(Placed(id, x, Hd, z, alX * flip + inX * skew, alZ * flip + inZ * skew, true));
                    }
                }
            }
            if (band.Kind == FrameKinds.Rail)
            {
                // A train of containers stopped on the far track, some of its flatcars empty.
                double H = 0.6 * StoreysOf(s, FrameKinds.Rail, 1) + 0.25;
                double crest0 = -1, crest1 = D - 1.8;
                double c = crest1 - crest0 >= 3.4 ? (crest0 + crest1) / 2 - 0.95 : (crest0 + crest1) / 2;
                int cars = 6 + (int)Math.Floor(R(0, 7) * 6);
                double start = -Along / 2.0 + R(0, 8) * (n - cars * 5.4 + Along);
                for (int k = 0; k < cars; k++)
                {
                    double u = start + k * 5.4 + 2.6;
                    Put(Box, u, c, H + 0.36, 5.1, 0.2, 1.55, 0x3b3d3f);
                    foreach (var g in new[] { -1.75, 1.75 }) Put(Box, u + g, c, H + 0.16, 1.1, 0.24, 1.2, 0x26282a);
                    if (R(k, 9) < 0.2) continue;
                    var (x, z) = World(u, c);
                    props.Add(Placed(Colours[(int)Math.Floor(R(k, 10) * 3)], x, H + 0.56, z, inX, inZ, true));
                }
            }
        }

        /// <summary>
        /// The country's ravine or water along its side, over the stretch it runs: the ground open down to it (the ground's)
        /// from its depth to the far bank past the edge. A ravine's sheer walls, crags, its floor and a stream; a river's earth
        /// banks into its water; its ends walled the same, and past the far bank the wood.
        /// </summary>
        static void DrawSunken(GameState s, MapOutline.Band band, List<Dressing> props, List<Shape> shapes)
        {
            int w = s.Width, h = s.Height;
            int side = band.Side, D = band.Depth;
            var ravine = band.Kind == FrameKinds.Ravine;
            double yaw = new[] { 0, -Math.PI / 2, Math.PI, Math.PI / 2 }[side];
            (double, double) World(double u, double v) => side == 0 ? (u, v) : side == 1 ? (w - v, u) : side == 2 ? (u, h - v) : (v, u);
            void Put(string form, double u, double v, double y, double along, double tall, double across, int color, bool flip = false, double turn = 0)
            {
                var (x, z) = World(u, v);
                shapes.Add(MakeShape(form, x, y, z, yaw + turn + (flip ? Math.PI : 0), along, tall, across, color));
            }
            double R(double k, double salt) => Hash(k, side * 31 + D, salt);
            double near = D, far = -MapOutline.FarBank;
            int n = side % 2 != 0 ? h : w;
            int ua = band.From == 0 ? -Along : band.From, ub = band.To == n - 1 ? n + Along : band.To + 1;
            double stream = (near + far) / 2 + (R(0, 1) - 0.5) * 1.4;
            double deep = ravine ? 5.2 : 0.45;
            const int rock = 0x6f675c, earth = 0x6b604c;
            for (int u0 = ua; u0 < ub; u0 += Stretch)
            {
                double len = Math.Min(Stretch, ub - u0);
                double u = u0 + len / 2;
                if (ravine)
                {
                    Put(Box, u, (near + far) / 2, -deep - 0.3, len, 0.3, near - far, 0x4f4a42);
                    Put(Water, u, stream, -deep + 0.03, len, 1, 1.2, 0x34463f);
                    foreach (var v in new[] { near + 0.15, far - 0.15 }) Put(Box, u, v, -deep, len, deep, 0.3, rock);
                }
                else
                {
                    Put(Water, u, (near + far) / 2, -0.4, len, 1, near - far, 0x46605a);
                    Put(Wedge, u, near - 0.45, -deep, len, deep, 0.9, earth);
                    Put(Wedge, u, far + 0.45, -deep, len, deep, 0.9, earth, true);
                }
            }
            // Its ends, across it: walls of rock, or banks.
            foreach (var (u, k) in new[] { (ua, 1), (ub, -1) })
            {
                if (u < 0 || u > n) continue;
                if (ravine) Put(Box, u - k * 0.15, (near + far) / 2, -deep, 0.3, deep, near - far + 0.6, rock);
                // (The wedge turned a quarter rises along u: flipped at the start, where the land is before it.)
                else Put(Wedge, u + k * 0.45, (near + far) / 2, -deep, near - far, deep, 0.9, earth, k > 0, Math.PI / 2);
            }
            // A ravine's crags along the tops of its walls, sunk into them.
            if (ravine)
            {
                foreach (var (v, outward, salt) in new[] { (near + 0.2, -1, 0), (far - 0.2, 1, 7) })
                {
                    int i = 0;
                    for (double u = ua + 0.6; u < ub - 0.6; u += 2 + R(i, 40 + salt) * 0.8, i++)
                    {
                        var (x, z) = World(u, v);
                        double ox = World(u, v + outward).Item1 - x, oz = World(u, v + outward).Item2 - z;
                        double a = (R(i, 41 + salt) - 0.5) * 0.6;
                        string id = R(i, 42 + salt) < 0.5 ? "cliff" : "cliff2";
                        props.Add(Placed(id, x, -1.5 - R(i, 43 + salt) * 0.4, z, ox * Math.Cos(a) - oz * Math.Sin(a), ox * Math.Sin(a) + oz * Math.Cos(a), true, 1, 2 + R(i, 44 + salt) * 0.6));
                    }
                }
            }
            // The wood on the far bank (past the map's edge: no shadow on what is played).
            var trees = Pinewood(s) ? Pines : Mixed;
            foreach (var (v, k) in new[] { (far - 1.1, 0), (far - 2.5, 1), (far - 3.9, 2) })
            {
                int i = 0;
                for (double u = ua + 0.4 + k * 0.5; u < ub; u += 1.3 + R(i, 50 + k) * 0.5, i++)
                {
                    var (x, z) = World(u, v + (R(i, 51 + k) - 0.5) * 0.6);
                    double a = R(i, 52 + k) * Math.PI * 2;
                    props.Add(Placed(trees[(int)Math.Floor(R(i, 53 + k) * trees.Length)], x, 0, z, Math.Cos(a), Math.Sin(a), false, 1, 0.95 + R(i, 54 + k) * 0.35));
                }
            }
        }

        /// <summary>
        /// What fills piece `p` away from its line (two tiles in from the played ground or more), and on past the map's edge
        /// where it reaches it: a lot's weeds, wrecks and rubbish; a ruin's heaps; a building site's stacks; what was left on
        /// a street; a cordon's towers.
        /// </summary>
        static List<Dressing> Fill(GameState s, FramePiece p, Func<double, double> r, Func<int, int, bool> played, MapOutline.Band band)
        {
            var output = new List<Dressing>();
            int w = s.Width, h = s.Height;
            int x0 = p.X == 0 ? -Beyond : p.X, y0 = p.Y == 0 ? -Beyond : p.Y;
            int x1 = p.X + p.W == w ? w - 1 + Beyond : p.X + p.W - 1, y1 = p.Y + p.H == h ? h - 1 + Beyond : p.Y + p.H - 1;
            // How near played ground is (tiles, up to 3).
            int Near(int x, int y)
            {
                for (int k = 1; k <= 3; k++)
                    for (int dy = -k; dy <= k; dy++)
                        for (int dx = -k; dx <= k; dx++)
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == k && played(x + dx, y + dy)) return k;
                return 4;
            }
            (double, double) Turn(int x, int y, double salt)
            {
                double a = Hash(x, y, salt) * Math.PI * 2;
                return (Math.Cos(a), Math.Sin(a));
            }
            void Put(string id, int x, int y, double salt, double size = 1, bool cast = true)
            {
                var (dx, dz) = Turn(x, y, salt);
                output.Add(Placed(id, x + 0.5 + (Hash(x, y, salt + 1) - 0.5) * 0.5, 0, y + 0.5 + (Hash(x, y, salt + 2) - 0.5) * 0.5, dx, dz, cast, 1, size));
            }
            var along = p.W >= p.H;
            var trees = Pinewood(s) ? Pines : Mixed;
            // A tree of the wood on (x, y), a shadow from it only in its first row.
            void Tree(int x, int y, int gap, double salt)
            {
                var (dx, dz) = Turn(x, y, salt);
                output.Add(Placed(trees[(int)Math.Floor(Hash(x, y, salt + 1) * trees.Length)], x + 0.5 + (Hash(x, y, salt + 2) - 0.5) * 0.7, 0, y + 0.5 + (Hash(x, y, salt + 3) - 0.5) * 0.7, dx, dz, gap <= 3, 1, 0.9 + Hash(x, y, salt + 4) * 0.4));
            }
            // Where (x, y) is from the band's side (`t` along it, `v` in from the edge).
            (int t, int v) BandAt(int x, int y)
            {
                int side = band.Side;
                return (side % 2 != 0 ? y : x, side == 0 ? y : side == 1 ? w - 1 - x : side == 2 ? h - 1 - y : x);
            }
            if (p.Kind == FrameKinds.Containers) ContainerStacks(p, x0, y0, x1, y1, Near, output);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int gap = Near(x, y);
                    if (gap < 2) continue;
                    double v = Hash(x, y, 1);
                    switch (p.Kind)
                    {
                        case FrameKinds.Forest:
                            // Trees close together past the line's two rows, brush, a fallen trunk, a stone between them.
                            if (gap < 3) break;
                            if (v < 0.44) Tree(x, y, gap, 60);
                            else if (v < 0.52) Put("bush", x, y, 61, 0.85 + Hash(x, y, 62) * 0.4, false);
                            else if (v < 0.535) Put("log", x, y, 63, 1, false);
                            else if (v < 0.55) Put("rock", x, y, 64, 1, false);
                            break;
                        case FrameKinds.Rocks:
                            // Boulders and crags, now and then a tree and brush among them.
                            if (v < 0.15) Put("boulder", x, y, 70, 0.9 + Hash(x, y, 71) * 0.8, gap <= 3);
                            else if (v < 0.25) Put(Hash(x, y, 72) < 0.5 ? "cliff" : "cliff2", x, y, 73, 1.1 + Hash(x, y, 74) * 0.6, gap <= 3);
                            else if (v < 0.35) Put("rock", x, y, 75, 0.9 + Hash(x, y, 76) * 0.5, false);
                            else if (v < 0.39 && gap >= 3) Tree(x, y, gap, 77);
                            else if (v < 0.45) Put("bush", x, y, 78, 0.8, false);
                            break;
                        case FrameKinds.Ravine:
                        case FrameKinds.Water:
                        {
                            (int t, int v)? inBand = band != null && band.Kind == p.Kind ? BandAt(x, y) : ((int, int)?)null;
                            var inStretch = inBand != null && inBand.Value.t >= band.From && inBand.Value.t <= band.To;
                            // Down in it: nothing (DrawSunken's); past its far bank, the wood; on its rim or bank, what grows.
                            if (inStretch && inBand.Value.v < band.Depth && inBand.Value.v >= -MapOutline.FarBank) break;
                            if (inStretch && inBand.Value.v < -MapOutline.FarBank)
                            {
                                if (v < 0.3) Tree(x, y, 4, 80);
                                break;
                            }
                            if (p.Kind == FrameKinds.Ravine)
                            {
                                if (v < 0.1) Put("boulder", x, y, 81, 0.7 + Hash(x, y, 82) * 0.5);
                                else if (v < 0.18) Put("rock", x, y, 83, 0.9);
                                else if (v < 0.28) Put("bush", x, y, 84, 0.85, false);
                                else if (v < 0.4) Put("tuftLow", x, y, 85, 0.9, false);
                            }
                            else
                            {
                                if (v < 0.22) Put("tuftWide", x, y, 86, 1.4 + Hash(x, y, 87) * 0.6, false);
                                else if (v < 0.32) Put("bush", x, y, 88, 0.85, false);
                                else if (v < 0.36) Put("rock", x, y, 89, 0.9);
                            }
                            break;
                        }
                        case FrameKinds.Lot:
                        case FrameKinds.Block:
                            if (p.Kind == FrameKinds.Block && !Thin(p)) break;
                            // Weeds over all of it; now and then rubbish, a heap of rubble, a dead tree, a wreck where there's room.
                            if (v < 0.3) Put(Hash(x, y, 2) < 0.7 ? "tuftLow" : "tuft", x, y, 3, 0.8 + Hash(x, y, 4) * 0.6, false);
                            else if (v < 0.36) Put(Hash(x, y, 5) < 0.5 ? "trashPile" : "tirePile", x, y, 6);
                            else if (v < 0.4) Put(Hash(x, y, 7) < 0.5 ? "rubble1" : "rubble2", x, y, 8);
                            else if (v < 0.415 && gap >= 3) Put("treeDead", x, y, 9, 0.8 + Hash(x, y, 10) * 0.3);
                            else if (v < 0.44 && gap >= 3) Put(Hash(x, y, 11) < 0.5 ? "carWreck" : "carRusted", x, y, 12);
                            break;
                        case FrameKinds.Ruin:
                            // What a block that came down leaves: heaps of it, rubble between, weeds coming through.
                            if ((x - x0) % 5 == 2 && (y - y0) % 5 == 2 && gap >= 3) Put("ruinPile", x, y, 20, 0.9 + Hash(x, y, 21) * 0.3);
                            else if (v < 0.08) Put(Hash(x, y, 22) < 0.5 ? "debrisPile" : "debrisPile2", x, y, 23, 0.7);
                            else if (v < 0.22) Put(Hash(x, y, 24) < 0.5 ? "rubble1" : "rubble2", x, y, 25);
                            else if (v < 0.4) Put("tuftLow", x, y, 26, 0.9, false);
                            break;
                        case FrameKinds.Works:
                            // A building site: stacks of pallets and bricks, cement, pipes, a portaloo.
                            if (v < 0.07) Put("palletStack", x, y, 30);
                            else if (v < 0.12) Put("cementBags", x, y, 31);
                            else if (v < 0.15) Put("brickStack", x, y, 32);
                            else if (v < 0.17) Put("pipes", x, y, 33);
                            else if (v < 0.18 && gap >= 3) Put("portaloo", x, y, 34);
                            else if (v < 0.3) Put("tuftLow", x, y, 35, 0.8, false);
                            break;
                        case FrameKinds.Street:
                        {
                            // What was left on it, along the way out: a wreck now and then, bags and tyres.
                            var inside = x >= 0 && y >= 0 && x < w && y < h;
                            if (v < (inside ? 0.05 : 0.04))
                            {
                                string id = Hash(x, y, 40) < 0.5 ? "carRusted" : Hash(x, y, 40) < 0.8 ? "carWreck" : "van";
                                int flip = Hash(x, y, 41) < 0.5 ? 1 : -1;
                                double skew = (Hash(x, y, 42) - 0.5) * 0.4;
                                output.Add(Placed(id, x + 0.5, 0, y + 0.5, along ? flip : skew, along ? skew : flip, true));
                            }
                            else if (v < 0.1) Put(Hash(x, y, 43) < 0.5 ? "trashPile" : "tire", x, y, 44);
                            break;
                        }
                        case FrameKinds.Barrier:
                        {
                            // A cordon in rows behind its T-walls: wire along them, sandbags, a tower every fourteen tiles;
                            // past them, now and then a stack of crates or a floodlight.
                            int k = along ? x : y;
                            int ax = along ? 1 : 0, az = along ? 0 : 1;
                            if (gap == 2) output.Add(Placed("razorWire", x + 0.5, 0, y + 0.5, ax, az, false));
                            else if (gap == 3 && Mod(k, 14) == 7) Put("guardTower", x, y, 50);
                            else if (gap == 3 && Mod(k, 3) != 0) output.Add(Placed("sandbags", x + 0.5, 0, y + 0.5, ax, az, true));
                            else if (gap >= 4 && v < 0.025) Put(Hash(x, y, 53) < 0.6 ? "milCrate" : "floodlight", x, y, 54);
                            break;
                        }
                    }
                }
            }
            return output;
        }

        /// <summary>JavaScript's % on integers (the sign of the dividend), for the same results on negative tiles.</summary>
        static int Mod(int a, int b) => a % b;

        /// <summary>A container's size (tiles, as fitted: 4.85 long), and the height of one on another.</summary>
        const double ContainerLong = 4.85, ContainerWide = 1.98, ContainerHigh = 2.07;

        /// <summary>
        /// The containers apart: one that would stand through another put before it at its height, or through the bus a
        /// street is shut with, is left out.
        /// </summary>
        static List<Dressing> Apart(List<Dressing> props)
        {
            (double x0, double x1, double z0, double z1, double y) BoxOf(Dressing d, double hx, double hz)
            {
                var m = d.Matrix;
                double ex = Math.Abs(m.m00) * hx + Math.Abs(m.m02) * hz;
                double ez = Math.Abs(m.m20) * hx + Math.Abs(m.m22) * hz;
                var e = m.GetColumn(3);
                return (e.x - ex + 0.15, e.x + ex - 0.15, e.z - ez + 0.15, e.z + ez - 0.15, e.y);
            }
            bool Hits((double x0, double x1, double z0, double z1, double y) a, (double x0, double x1, double z0, double z1, double y) b) =>
                Math.Abs(a.y - b.y) < 1 && a.x0 < b.x1 && b.x0 < a.x1 && a.z0 < b.z1 && b.z0 < a.z1;
            // A wrecked bus is about 8 tiles long along its own x, 1.7 wide.
            var boxes = props.Where(d => d.Id == "bus").Select(d => BoxOf(d, 3.9, 0.85)).ToList();
            return props.Where(d =>
            {
                if (!Colours.Contains(d.Id)) return true;
                // Its own x across it, its z along it (as fitted).
                var box = BoxOf(d, ContainerWide / 2, ContainerLong / 2);
                if (boxes.Any(b => Hits(b, box))) return false;
                boxes.Add(box);
                return true;
            }).ToList();
        }

        /// <summary>A container yard's stacks, behind the line's containers end to end: rows along the piece, lanes between, one to three high.</summary>
        static void ContainerStacks(FramePiece p, int x0, int y0, int x1, int y1, Func<int, int, int> near, List<Dressing> output)
        {
            var alongX = x1 - x0 >= y1 - y0;
            double l0 = alongX ? x0 : y0, l1 = alongX ? x1 + 1 : y1 + 1, c0 = alongX ? y0 : x0, c1 = alongX ? y1 + 1 : x1 + 1;
            double stepL = ContainerLong + 0.35, stepC = ContainerWide + 0.6;
            for (double c = c0 + stepC / 2; c + ContainerWide / 2 <= c1; c += stepC)
            {
                for (double l = l0 + stepL / 2; l + ContainerLong / 2 <= l1; l += stepL)
                {
                    double x = alongX ? l : c, z = alongX ? c : l;
                    // Clear of what is played by three tiles all over it (the line's containers stand in the first two).
                    double hl = ContainerLong / 2 - 0.1, hw = ContainerWide / 2 - 0.1;
                    var corners = alongX
                        ? new[] { (x - hl, z - hw), (x + hl, z - hw), (x - hl, z + hw), (x + hl, z + hw) }
                        : new[] { (x - hw, z - hl), (x + hw, z - hl), (x - hw, z + hl), (x + hw, z + hl) };
                    if (corners.Any(q => near((int)Math.Floor(q.Item1), (int)Math.Floor(q.Item2)) < 3)) continue;
                    double k = Hash(Math.Floor(x * 3), Math.Floor(z * 3), 90 + p.W);
                    if (k < 0.12) continue;
                    int high = k < 0.5 ? 1 : k < 0.85 ? 2 : 3;
                    for (int n = 0; n < high; n++)
                    {
                        string id = Colours[(int)Math.Floor(Hash(Math.Floor(x * 3), Math.Floor(z * 3) + n, 91) * 3)];
                        double skew = (Hash(Math.Floor(x * 3) + n, Math.Floor(z * 3), 92) - 0.5) * 0.08;
                        // The model's length (its z) along the row: its +x across it.
                        double dx = alongX ? skew : 1, dz = alongX ? 1 : skew;
                        output.Add(Placed(id, x, n * ContainerHigh, z, dx, dz, n == high - 1 && near((int)Math.Floor(x), (int)Math.Floor(z)) <= 3));
                    }
                }
            }
        }

        // ------------------------------------------------------------ dressing.ts

        /// <summary>A prop at (x, y, z), its own +x turned towards (dx, dz), `scale` along its +x (a stretched wire), `size` all over.</summary>
        internal static Dressing Placed(string id, double x, double y, double z, double dx, double dz, bool cast, double scale = 1, double size = 1) =>
            new Dressing { Id = id, Cast = cast, Matrix = Matrix4x4.TRS(new Vector3((float)x, (float)y, (float)z), Yaw(Math.Atan2(-dz, dx)), new Vector3((float)(scale * size), (float)size, (float)size)) };

        /// <summary>A turn about y as 0.10.1's (three's setFromAxisAngle), the same numbers in Unity's matrix.</summary>
        static Quaternion Yaw(double radians) => Quaternion.Euler(0, (float)(radians * Mathf.Rad2Deg), 0);

        /// <summary>
        /// How far past the middle of an edge tile (along `out`) prop `id` has to stand, placed towards (dx, dz), `scale`
        /// along it and `size` all over, to keep off that tile: its outer side, and as far again as it reaches that way.
        /// </summary>
        static double ClearOf(string id, double dx, double dz, V2 outward, double scale = 1, double size = 1)
        {
            if (!Half.TryGetValue(id, out var half)) return 0;
            double l = Math.Sqrt(dx * dx + dz * dz);
            if (l == 0) l = 1;
            // Its own x is (dx, dz); its own z, (-dz, dx).
            double along = Math.Abs((dx * outward.x + dz * outward.y) / l);
            double across = Math.Abs((-dz * outward.x + dx * outward.y) / l);
            return 0.5 + (half.x * scale * along + half.z * across) * size + EdgeClear;
        }

        /// <summary>What shuts a street where it leaves the map, from tile `a` to `b` along the edge, just past it (`outward`): picked by `kind`.</summary>
        static List<Dressing> Shut(V2 a, V2 b, V2 outward, int kind, Func<double, double> r)
        {
            var list = new List<Dressing>();
            var along = new V2(Math.Sign(b.x - a.x), Math.Sign(b.y - a.y));
            int width = Math.Abs(b.x - a.x) + Math.Abs(b.y - a.y) + 1;
            (double x, double z) At(double t, double beyond = 1.1) => (a.x + 0.5 + along.x * t + outward.x * beyond, a.y + 0.5 + along.y * t + outward.y * beyond);
            double mid = (width - 1) / 2.0;
            if (kind == 0)
            {
                // A checkpoint: T-walls across the way, a tyre or two by its ends.
                for (int t = 0; t < width; t++)
                {
                    var p = At(t, 1.05 + (r(20 + t) - 0.5) * 0.12);
                    list.Add(Placed("milBarrierTall", p.x, 0, p.z, -along.y + (r(30 + t) - 0.5) * 0.08, along.x + (r(30 + t) - 0.5) * 0.08, true));
                }
                foreach (var t in new[] { -0.8, width - 0.2 })
                {
                    var p = At(t, 1.4);
                    list.Add(Placed("tire", p.x, 0, p.z, 1, 0, true));
                }
            }
            else if (kind == 1)
            {
                // A bus across the street, sandbags where it doesn't reach.
                var p = At(mid, Math.Max(1.6, ClearOf("bus", along.x, along.y, outward)));
                list.Add(Placed("bus", p.x, 0, p.z, along.x, along.y, true));
                foreach (var t in new[] { -0.6, width - 0.4 })
                {
                    var q = At(t, 0.9);
                    if (width > 6) list.Add(Placed("sandbags", q.x, 0, q.z, along.x, along.y, true));
                }
            }
            else
            {
                // Burnt-out cars nose to tail across it, planks nailed between them.
                for (double t = 0.5; t < width; t += 2.6)
                {
                    int flip = r(50 + t) < 0.5 ? 1 : -1;
                    string id = r(60 + t) < 0.5 ? "carWreck" : "carRusted";
                    var p = At(Math.Min(t + 0.5, width - 1), Math.Max(1.35 + (r(40 + t) - 0.5) * 0.3, ClearOf(id, along.x, along.y, outward)));
                    list.Add(Placed(id, p.x, 0, p.z, along.x * flip, along.y * flip, true));
                }
                for (double t = 0; t < width; t += 2.6)
                {
                    var p = At(t, 0.85);
                    list.Add(Placed("barricade1", p.x, 0, p.z, along.x, along.y, true));
                }
            }
            return list;
        }

        /// <summary>
        /// Along an open stretch of the edge, from `a` to `b`, what keeps anybody from going on, right past it (`ends` tiles
        /// more at each end): a ruin's heaps (0), sheet metal (1), a precast wall (2), T-walls (3), the wood (5), a rock face
        /// (6), trees felled across the way (7), containers end to end (8).
        /// </summary>
        static List<Dressing> FenceOff(V2 a, V2 b, V2 outward, int kind, (int, int) ends, Func<double, double> r, (bool wired, bool pines) look)
        {
            var list = new List<Dressing>();
            var along = new V2(Math.Sign(b.x - a.x) != 0 ? Math.Sign(b.x - a.x) : Math.Abs(outward.y), Math.Sign(b.y - a.y) != 0 ? Math.Sign(b.y - a.y) : Math.Abs(outward.x));
            double from = -0.5 - ends.Item1;
            double length = Math.Abs(b.x - a.x) + Math.Abs(b.y - a.y) + 1 + ends.Item1 + ends.Item2;
            (double x, double z) At(double t, double beyond) => (a.x + 0.5 + along.x * t + outward.x * beyond, a.y + 0.5 + along.y * t + outward.y * beyond);
            // Pieces `size` tiles long from one end to the other, as many as cover it, a little closer where it doesn't divide.
            void Row(double size, Action<double, int> put)
            {
                int n = Math.Max(1, (int)Math.Ceiling(length / size - 0.15));
                for (int i = 0; i < n; i++) put(from + (length / n) * (i + 0.5), i);
            }
            double Turn(double salt, double by) => (r(salt) - 0.5) * by;
            // Nothing reaches over the edge tile, where units walk.
            double Off(string id, double beyond, double dx, double dz, double scale = 1, double size = 1) => Math.Max(beyond, ClearOf(id, dx, dz, outward, scale, size));
            if (kind == 0)
            {
                // A block come down: its heaps along the edge, a wall of it still up, the whole ruin where there is room.
                Row(4.6, (t, i) =>
                {
                    int flip = r(80 + i) < 0.5 ? 1 : -1;
                    string id = r(90 + i) < 0.5 ? "debrisPile" : "debrisPile2";
                    double hx = along.x * flip - outward.x * Turn(100 + i, 0.3), hz = along.y * flip - outward.y * Turn(100 + i, 0.3);
                    var p = At(t, Off(id, 1.9 + Turn(70 + i, 0.4), hx, hz));
                    var heap = Placed(id, p.x, -0.05, p.z, hx, hz, true);
                    // Lower than they come: a storey's worth of rubble, not a hill of it.
                    heap.Matrix *= Matrix4x4.Scale(new Vector3(1, 0.7f, 1));
                    list.Add(heap);
                });
                if (length >= 8)
                {
                    var p = At(from + length * (0.35 + r(110) * 0.3), 4.2);
                    list.Add(Placed("ruinPile", p.x, 0, p.z, along.x, along.y, true));
                }
                Row(3.1, (t, i) =>
                {
                    if (r(120 + i) < 0.5) return;
                    string id = r(140 + i) < 0.5 ? "rubble1" : "rubble2";
                    double rx = Math.Cos(r(150 + i) * 6.3), rz = Math.Sin(r(150 + i) * 6.3);
                    var p = At(t + Turn(130 + i, 1), Off(id, 0.65, rx, rz));
                    list.Add(Placed(id, p.x, 0, p.z, rx, rz, true));
                });
            }
            else if (kind == 1)
            {
                // A hoarding of sheet metal, put up in a hurry: its panels a little askew.
                Row(1.7, (t, i) =>
                {
                    var p = At(t, 0.8 + Turn(160 + i, 0.12));
                    double k = Turn(170 + i, 0.1);
                    list.Add(Placed(r(180 + i) < 0.5 ? "sheetFence" : "sheetFence2", p.x, 0, p.z, along.x - outward.x * k, along.y - outward.y * k, true));
                });
            }
            else if (kind == 2)
            {
                // A precast wall, panel by panel, rubbish at its foot; a facility's with razor wire before it.
                double wire = ClearOf("razorWire", along.x, along.y, outward, 1.62);
                double heapAt = ClearOf("trashPile", outward.x, outward.y, outward);
                double front = look.wired ? wire + (wire - 0.5 - EdgeClear) : heapAt + (heapAt - 0.5 - EdgeClear);
                double wall = Math.Max(0.75, front + (Half["concreteFence"].z + EdgeClear));
                Row(1.62, (t, i) =>
                {
                    // Its origin is at its left end.
                    var p = At(t - 0.83, wall);
                    list.Add(Placed("concreteFence", p.x, 0, p.z, along.x, along.y, true));
                    if (look.wired)
                    {
                        var q = At(t, wire);
                        list.Add(Placed("razorWire", q.x, 0, q.z, along.x, along.y, false, 1.62));
                    }
                    else if (r(190 + i) < 0.3)
                    {
                        string id = r(210 + i) < 0.5 ? "trashPile" : "tirePile";
                        var q = At(t + Turn(200 + i, 0.6), ClearOf(id, outward.x, outward.y, outward));
                        list.Add(Placed(id, q.x, 0, q.z, outward.x, outward.y, true));
                    }
                });
            }
            else if (kind == 3)
            {
                // T-walls, a cordon.
                Row(1, (t, i) =>
                {
                    var p = At(t, 0.9 + Turn(220 + i, 0.1));
                    list.Add(Placed("milBarrierTall", p.x, 0, p.z, -along.y + Turn(230 + i, 0.08), along.x + Turn(230 + i, 0.08), true));
                });
            }
            else if (kind == 5)
            {
                // The wood closing in: two rows of trees, the second between the first's, brush at their feet.
                var trees = look.pines ? new[] { "pineLarge", "pineLarge2", "treePine" } : new[] { "tree", "treePine", "tree", "pineLarge" };
                Row(1.15, (t, i) =>
                {
                    foreach (var (k, beyond, cast) in new[] { (0, 0.75, true), (1, 1.95, false) })
                    {
                        var p = At(t + k * 0.55 + Turn(270 + i * 2 + k, 0.4), beyond + Turn(280 + i * 2 + k, 0.3));
                        double a = r(290 + i * 2 + k) * 6.3;
                        list.Add(Placed(trees[(int)Math.Floor(r(300 + i * 2 + k) * trees.Length)], p.x, 0, p.z, Math.Cos(a), Math.Sin(a), cast, 1, 0.9 + r(310 + i * 2 + k) * 0.35));
                    }
                    if (r(320 + i) < 0.55)
                    {
                        double size = 0.85 + r(340 + i) * 0.3;
                        var q = At(t + Turn(330 + i, 0.8), Off("bush", 0.3, Math.Cos(i * 2.1), Math.Sin(i * 2.1), 1, size));
                        list.Add(Placed("bush", q.x, 0, q.z, Math.Cos(i * 2.1), Math.Sin(i * 2.1), false, 1, size));
                    }
                });
            }
            else if (kind == 6)
            {
                // A rock face, boulders before it, a few trees above.
                Row(1.3, (t, i) =>
                {
                    double a = (r(370 + i) - 0.5) * 0.5;
                    string id = r(380 + i) < 0.5 ? "cliff" : "cliff2";
                    double cx = along.x * Math.Cos(a) - outward.x * Math.Sin(a), cz = along.y * Math.Cos(a) - outward.y * Math.Sin(a);
                    double size = 1 + r(390 + i) * 0.35;
                    var p = At(t + Turn(350 + i, 0.3), Off(id, 0.95 + Turn(360 + i, 0.25), cx, cz, 1, size));
                    list.Add(Placed(id, p.x, 0, p.z, cx, cz, true, 1, size));
                    if (r(400 + i) < 0.45)
                    {
                        string stone = r(420 + i) < 0.5 ? "boulder" : "rock";
                        double big = 0.8 + r(430 + i) * 0.4;
                        var q = At(t + Turn(410 + i, 0.9), Off(stone, 0.3, Math.Cos(i * 1.7), Math.Sin(i * 1.7), 1, big));
                        list.Add(Placed(stone, q.x, 0, q.z, Math.Cos(i * 1.7), Math.Sin(i * 1.7), true, 1, big));
                    }
                    if (r(440 + i) < 0.5)
                    {
                        var q = At(t + Turn(450 + i, 0.8), 2.3);
                        list.Add(Placed(look.pines ? "pineLarge" : "treePine", q.x, 0, q.z, 1, 0, false, 1, 0.9 + r(460 + i) * 0.3));
                    }
                });
            }
            else if (kind == 7)
            {
                // Trees felled across the way, trunk over trunk, the wood behind them.
                Row(2.2, (t, i) =>
                {
                    double behind = 0;
                    for (int k = 0; k < 2; k++)
                    {
                        double a = (r(480 + i * 2 + k) - 0.5) * 0.6;
                        double lx = along.x * Math.Cos(a) - outward.x * Math.Sin(a), lz = along.y * Math.Cos(a) - outward.y * Math.Sin(a);
                        // The second over the first, a little further out.
                        double beyond = Math.Max(Math.Max(0.6 + k * 0.45, ClearOf("log", lx, lz, outward, 2.4)), behind);
                        behind = beyond + 0.45;
                        var p = At(t + Turn(470 + i * 2 + k, 0.5), beyond);
                        list.Add(Placed("log", p.x, 0.12 * k, p.z, lx, lz, true, 2.4));
                    }
                    var q = At(t + 0.6, 1.8 + Turn(490 + i, 0.3));
                    double a2 = r(500 + i) * 6.3;
                    list.Add(Placed(look.pines ? "pineLarge" : "tree", q.x, 0, q.z, Math.Cos(a2), Math.Sin(a2), false, 1, 0.95 + r(510 + i) * 0.3));
                });
            }
            else
            {
                // Shipping containers end to end, dragged there; their length (the model's z) along the edge.
                Row(4.85, (t, i) =>
                {
                    double k = Turn(250 + i, 0.06);
                    string id = Colours[(int)Math.Floor(r(260 + i) * 3)];
                    double dx = -along.y + outward.y * k, dz = along.x - outward.x * k;
                    var p = At(t, Off(id, 1.45 + Turn(240 + i, 0.2), dx, dz));
                    list.Add(Placed(id, p.x, 0, p.z, dx, dz, true));
                });
            }
            return list;
        }

        // ------------------------------------------------------------ the hash

        /// <summary>frameArt.ts and dressing.ts hash: a stable value in [0, 1), JavaScript's numbers turned to 32-bit integers as Math.imul does.</summary>
        internal static double Hash(double x, double y, double salt)
        {
            unchecked
            {
                uint h = (uint)ToInt32(x + 11) * 374761393u + (uint)ToInt32(y + 5) * 668265263u + (uint)ToInt32(salt + 17) * 2147483647u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>ECMAScript's ToInt32: truncated toward zero, modulo 2³².</summary>
        static int ToInt32(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            double m = Math.Truncate(v) % 4294967296.0;
            if (m < 0) m += 4294967296.0;
            return unchecked((int)(uint)m);
        }
    }
}
