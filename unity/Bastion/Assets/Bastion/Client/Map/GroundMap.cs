using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.Client.Map
{
    /// <summary>
    /// What each tile's floor is (0.10.1's ground.ts GroundMap; UI's), for the ground's shader and what is scattered on
    /// it: its kind (open, street, inside, roof, a hill, a furrow, a yard, a canal's water), and in town the lawns away
    /// from the streets, the zebra crossings before a crossroads and the centre line down the narrower streets. One texel
    /// a tile: r the kind, g the elevation, b the line's axis (or a lawn, a crossing), a where across the tile the line is.
    /// </summary>
    public sealed class GroundMap
    {
        public const byte Open = 0;
        public const byte Street = 1;
        public const byte Indoor = 2;
        public const byte Roof = 3;
        /// <summary>Top of a hill in the wilds: grass, like open ground.</summary>
        public const byte Hill = 4;
        /// <summary>A furrow of a ploughed field: one tile wide, along the field.</summary>
        public const byte Furrow = 5;
        /// <summary>Tarmac that is not a street (the engine's ground 3: a yard, a forecourt, a car park): no paint on it.</summary>
        public const byte Yard = 6;
        /// <summary>A canal's water, down between its walls: no ground there.</summary>
        public const byte Water = 7;

        /// <summary>Open ground marked as grass in a town (blue).</summary>
        public const byte Lawn = 3;
        /// <summary>A street tile of a zebra crossing (blue; +1 when the street runs along y).</summary>
        public const byte Crosswalk = 4;

        /// <summary>Things that stand on paving: open ground round them stays paved.</summary>
        static readonly HashSet<string> Paved = new HashSet<string>
        {
            DecorKinds.Planter, DecorKinds.Fountain, DecorKinds.FountainCorner, DecorKinds.Statue, DecorKinds.Kiosk, DecorKinds.CarFront,
            DecorKinds.CarRear, DecorKinds.Crates, DecorKinds.Barricade, DecorKinds.Bin, DecorKinds.Bench,
        };

        public readonly GameState S;
        /// <summary>RGBA per tile, row y at y (the shader reads tile (x, y) at texel (x, y)).</summary>
        public readonly byte[] Data;
        /// <summary>Kind per tile.</summary>
        public readonly byte[] Kinds;
        /// <summary>
        /// Down each street two to eight tiles wide (away from crossings), the way it runs on each tile holding its centre
        /// line (1 along x, 2 along y; 0 none) and where the line lies across the tile (0–1): the manholes'. The painted
        /// line is only the narrower streets' (the data's own axis).
        /// </summary>
        public readonly byte[] LineAxis;
        public readonly float[] LineOffset;
        /// <summary>The river, the railway or the motorway along a side (the outline), if any.</summary>
        public readonly MapOutline.Band Band;

        public GroundMap(GameState s)
        {
            S = s;
            int w = s.Width, h = s.Height;
            var generated = s.MapId.StartsWith("gen-");
            // The outline round what is played: a street going on past its closure is street; under the motorway and a
            // cordon, tarmac; the rest is open ground, which what stands on it covers.
            var outline = new byte[w * h];
            foreach (var p in s.Frame ?? new List<FramePiece>())
            {
                var kind = p.Kind == FrameKinds.Street ? Street : p.Kind == FrameKinds.Highway || p.Kind == FrameKinds.Barrier ? Yard : Open;
                for (int y = p.Y; y < p.Y + p.H; y++)
                    for (int x = p.X; x < p.X + p.W; x++) outline[y * w + x] = (byte)(kind + 1);
            }
            // The river's band (its banks past it are ground), or the country's water or ravine: down to its water or floor.
            Band = MapOutline.BandOf(s);
            if (Band != null && MapOutline.Sunken.Contains(Band.Kind))
            {
                var at = MapOutline.FrameOf(s);
                for (int i = 0; i < w * h; i++)
                    if (MapOutline.InBand(s, Band, i % w, i / w, at)) outline[i] = Water + 1;
            }
            bool IsStreet(int x, int y) =>
                x >= 0 && y >= 0 && x < w && y < h && Elev(y * w + x) == 0 &&
                (outline[y * w + x] == Street + 1 || (s.Ground != null ? GroundAt(y * w + x) == 1 : generated && w == 36 && (x % 13 >= 10 || y % 13 >= 10)));
            var wilds = s.Biome == Biomes.Wilds;
            // The forest road and the stop's yards: tarmac, the road with a town street's centre line.
            var tarmac = wilds && s.Site == Sites.Roadside;
            var siege = s.Mission?.Siege != null;
            Data = new byte[w * h * 4];
            Kinds = new byte[w * h];
            LineAxis = new byte[w * h];
            LineOffset = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    int e = Elev(i);
                    // A siege's watchtowers stand on the ground: their decks are the guard tower's own.
                    byte kind = outline[i] == Water + 1 ? Water : e > 0 && !siege ? (wilds ? Hill : Roof) : GroundAt(i) == 2 ? Indoor : GroundAt(i) == 3 || outline[i] == Yard + 1 ? Yard : IsStreet(x, y) ? Street : Open;
                    int axis = 0;
                    double offset = 0;
                    // A one-tile strip in the wilds is a furrow of a field, not the track.
                    var furrowX = wilds && !IsStreet(x, y - 1) && !IsStreet(x, y + 1) && (IsStreet(x - 1, y) || IsStreet(x + 1, y));
                    var furrowZ = wilds && !IsStreet(x - 1, y) && !IsStreet(x + 1, y) && (IsStreet(x, y - 1) || IsStreet(x, y + 1));
                    if (kind == Street && (furrowX || furrowZ))
                    {
                        kind = Furrow;
                        axis = furrowX ? 1 : 2;
                    }
                    else if (kind == Street && (!wilds || tarmac))
                    {
                        // Centre line of a street two to four tiles wide, away from crossings (the manholes' of one up to eight).
                        int Run(int dx, int dy)
                        {
                            int n = 0;
                            while (n < 8 && IsStreet(x + dx * (n + 1), y + dy * (n + 1))) n++;
                            return n;
                        }
                        int up = Run(0, -1), down = Run(0, 1), left = Run(-1, 0), right = Run(1, 0);
                        int across = up + down + 1, along = left + right + 1;
                        int line = 0;
                        double lineAt = 0;
                        if (across >= 2 && across <= 8 && along >= across + 3 && left > 0 && right > 0)
                        {
                            line = 1;
                            lineAt = y - up + across / 2.0 - y;
                        }
                        else if (along >= 2 && along <= 8 && across >= along + 3 && up > 0 && down > 0)
                        {
                            line = 2;
                            lineAt = x - left + along / 2.0 - x;
                        }
                        if (line != 0 && lineAt >= 0 && lineAt <= 1)
                        {
                            LineAxis[i] = (byte)line;
                            LineOffset[i] = (float)lineAt;
                        }
                        if (line != 0 && (line == 1 ? across : along) <= 4)
                        {
                            axis = line;
                            offset = lineAt;
                        }
                        if (offset < 0 || offset > 1) axis = 0;
                    }
                    Kinds[i] = kind;
                    Data[i * 4] = kind;
                    Data[i * 4 + 1] = (byte)Mathf.Clamp(e, 0, 255);
                    Data[i * 4 + 2] = (byte)axis;
                    Data[i * 4 + 3] = (byte)Math.Round(offset * 255, MidpointRounding.AwayFromZero);
                }
            }
            // Hand-made maps say nothing of streets: they stay paved.
            if (s.Biome == Biomes.City && s.Ground != null)
            {
                Lawns();
                Crossings(IsStreet);
            }
        }

        int Elev(int i) => i < S.Elev.Count ? S.Elev[i] : 0;

        int GroundAt(int i) => S.Ground != null && i < S.Ground.Count ? S.Ground[i] : 0;

        /// <summary>
        /// Town ground away from the streets is grass (gardens, verges), unless something paved stands near it: a square, a
        /// car park, a kiosk. Marked Lawn in the blue of open ground.
        /// </summary>
        void Lawns()
        {
            int w = S.Width, h = S.Height;
            // Tiles from the nearest of `from`'s tiles, four ways round.
            int[] Distance(Func<int, bool> from)
            {
                var far = Enumerable.Repeat(99, w * h).ToArray();
                var queue = new List<int>();
                for (int i = 0; i < w * h; i++)
                {
                    if (!from(i)) continue;
                    far[i] = 0;
                    queue.Add(i);
                }
                for (int k = 0; k < queue.Count; k++)
                {
                    int i = queue[k], x = i % w, y = (i - x) / w;
                    foreach (var d in Grid.Dirs4)
                    {
                        int nx = x + d.X, ny = y + d.Y;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h || far[ny * w + nx] <= far[i] + 1) continue;
                        far[ny * w + nx] = far[i] + 1;
                        queue.Add(ny * w + nx);
                    }
                }
                return far;
            }
            var far = Distance(i => Kinds[i] == Street || Kinds[i] == Yard);
            // A street of five tiles or more across has pavements of two: its second row is paving too.
            bool IsStreet(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && Kinds[y * w + x] == Street;
            int Span(int x, int y, int dx, int dy)
            {
                int n = 1;
                for (int k = 1; k < 8 && IsStreet(x + dx * k, y + dy * k); k++) n++;
                for (int k = 1; k < 8 && IsStreet(x - dx * k, y - dy * k); k++) n++;
                return n;
            }
            var wide = Distance(i => Kinds[i] == Street && Math.Min(Span(i % w, i / w, 1, 0), Span(i % w, i / w, 0, 1)) >= 5);
            var paved = new byte[w * h];
            for (int i = 0; i < w * h; i++)
            {
                var kind = Decor.DecorOf(S.Decor != null && i < S.Decor.Count ? S.Decor[i] : (int?)null)?.Kind;
                if (kind == null || !Paved.Contains(kind)) continue;
                int x = i % w, y = (i - x) / w;
                for (int dy = -3; dy <= 3; dy++)
                    for (int dx = -3; dx <= 3; dx++)
                        if (x + dx >= 0 && y + dy >= 0 && x + dx < w && y + dy < h) paved[(y + dy) * w + x + dx] = 1;
            }
            for (int i = 0; i < w * h; i++)
                if (Kinds[i] == Open && far[i] >= 2 && wide[i] >= 3 && paved[i] == 0) Data[i * 4 + 2] = Lawn;
        }

        /// <summary>Zebra crossings: the row of street tiles just before a crossroads, across the street (Crosswalk, +1 along y).</summary>
        void Crossings(Func<int, int, bool> isStreet)
        {
            int w = S.Width, h = S.Height;
            int Span(int x, int y, int dx, int dy)
            {
                int n = 1;
                for (int k = 1; k < 8 && isStreet(x + dx * k, y + dy * k); k++) n++;
                for (int k = 1; k < 8 && isStreet(x - dx * k, y - dy * k); k++) n++;
                return n;
            }
            // A crossroads: street far both ways.
            bool Cross(int x, int y) => isStreet(x, y) && Span(x, y, 1, 0) > 5 && Span(x, y, 0, 1) > 5;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!isStreet(x, y) || Cross(x, y)) continue;
                    var alongX = Span(x, y, 1, 0) > Span(x, y, 0, 1);
                    var next = alongX ? Cross(x - 1, y) || Cross(x + 1, y) : Cross(x, y - 1) || Cross(x, y + 1);
                    if (next) Data[(y * w + x) * 4 + 2] = (byte)(Crosswalk + (alongX ? 0 : 1));
                }
            }
        }

        /// <summary>Open ground in town marked as a lawn (garden, verge), not paving.</summary>
        public bool IsLawn(int i) => Kinds[i] == Open && Data[i * 4 + 2] == Lawn;

        /// <summary>Tile i is open ground (the outer half of a wall drawn thin along its middle: the building's plan).</summary>
        public void Outdoors(int i)
        {
            if (Kinds[i] != Indoor) return;
            Kinds[i] = Open;
            Data[i * 4] = Open;
        }

        /// <summary>The data as a texture: a texel a tile, unfiltered, not sRGB.</summary>
        public Texture2D Texture(Texture2D into = null)
        {
            var t = into != null && into.width == S.Width && into.height == S.Height ? into : new Texture2D(S.Width, S.Height, TextureFormat.RGBA32, false, true)
            {
                name = "Ground map",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            t.SetPixelData(Data, 0);
            t.Apply(false);
            return t;
        }
    }

    /// <summary>
    /// The outline round what is played (0.10.1's outline.ts; docs/MAPAS.md §5.10, GameState.frame), as the client reads
    /// it: which piece each tile is, and the river, the railway or the motorway along a side as one straight band.
    /// </summary>
    public static class MapOutline
    {
        /// <summary>How far past the map's edge a canal's far bank is (tiles).</summary>
        public const int FarBank = 5;

        /// <summary>The kinds that run along a side as a band; those of them the ground opens for (down to their water or floor).</summary>
        static readonly HashSet<string> Bands = new HashSet<string> { FrameKinds.River, FrameKinds.Rail, FrameKinds.Highway, FrameKinds.Water, FrameKinds.Ravine };
        public static readonly HashSet<string> Sunken = new HashSet<string> { FrameKinds.River, FrameKinds.Water, FrameKinds.Ravine };

        /// <summary>A band along `Side` (DIRS4, out of the map): `Depth` tiles in from the edge, along `From`–`To`.</summary>
        public sealed class Band
        {
            public string Kind;
            public int Side;
            public int Depth;
            public int From;
            public int To;
        }

        /// <summary>Each tile's piece of the outline (its index in GameState.frame), -1 where it is played.</summary>
        public static short[] FrameOf(GameState s)
        {
            var at = Enumerable.Repeat((short)-1, s.Width * s.Height).ToArray();
            var frame = s.Frame ?? new List<FramePiece>();
            for (int k = 0; k < frame.Count; k++)
            {
                var p = frame[k];
                for (int y = p.Y; y < p.Y + p.H; y++)
                    for (int x = p.X; x < p.X + p.W; x++) at[y * s.Width + x] = (short)k;
            }
            return at;
        }

        /// <summary>The map's band (one at most), or null.</summary>
        public static Band BandOf(GameState s)
        {
            var frame = s.Frame ?? new List<FramePiece>();
            var first = frame.FirstOrDefault(p => p.Side != null && Bands.Contains(p.Kind));
            var side = first?.Side;
            if (first == null)
            {
                // The country's water or ravine says no side: the one its biggest piece runs along furthest.
                var natural = frame.Where(p => p.Kind == FrameKinds.Water || p.Kind == FrameKinds.Ravine).ToList();
                if (natural.Count == 0) return null;
                first = natural.Aggregate((a, p) => p.W * p.H > a.W * a.H ? p : a);
                var runs = new[] { first.Y == 0 ? first.W : 0, first.X + first.W == s.Width ? first.H : 0, first.Y + first.H == s.Height ? first.W : 0, first.X == 0 ? first.H : 0 };
                side = Array.IndexOf(runs, runs.Max());
                if (runs[side.Value] == 0) return null;
            }
            var at = FrameOf(s);
            var kind = first.Kind;
            int n = side.Value % 2 != 0 ? s.Height : s.Width;
            int acrossN = side.Value % 2 != 0 ? s.Width : s.Height;
            int depth = int.MaxValue, from = n, to = -1;
            for (int t = 0; t < n; t++)
            {
                int k = 0;
                while (k < acrossN)
                {
                    var (x, y) = Cell(s, side.Value, t, k);
                    int i = at[y * s.Width + x];
                    if (i < 0 || frame[i].Kind != kind) break;
                    k++;
                }
                if (k > 0)
                {
                    depth = Math.Min(depth, k);
                    from = Math.Min(from, t);
                    to = t;
                }
            }
            return depth != int.MaxValue ? new Band { Kind = kind, Side = side.Value, Depth = depth, From = from, To = to } : null;
        }

        /// <summary>The tile `k` in from the edge on `side`, `t` along it.</summary>
        public static (int x, int y) Cell(GameState s, int side, int t, int k) =>
            side == 0 ? (t, k) : side == 1 ? (s.Width - 1 - k, t) : side == 2 ? (t, s.Height - 1 - k) : (k, t);

        /// <summary>Whether tile (x, y) is in band `b`: its kind, less than its depth in from its side.</summary>
        public static bool InBand(GameState s, Band b, int x, int y, short[] at)
        {
            int i = at[y * s.Width + x];
            if (i < 0 || s.Frame[i].Kind != b.Kind) return false;
            int k = b.Side == 0 ? y : b.Side == 1 ? s.Width - 1 - x : b.Side == 2 ? s.Height - 1 - y : x;
            int t = b.Side % 2 != 0 ? y : x;
            return k < b.Depth && t >= b.From && t <= b.To;
        }
    }
}
