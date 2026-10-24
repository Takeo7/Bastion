using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    // 0.10.1's homeFurnishing.ts: a house's rooms furnished beyond their cover (playtest 18/10, #36: «más cariño al
    // amueblar… que se sienta una ciudad o un pueblo de verdad»): what the generator put in each room (GameState.homes,
    // its decor) gets what goes with it. The sofa its rug and an armchair across the table, a lamp and a plant in the
    // corners; chairs round the kitchen table, the bread bin on the counter; the bedside table and its lamp by the bed, a
    // rug before it; the desk its books and lamp; a rug and the towels in the bathroom, a mat before the toilet in the
    // aseo; the coats by the door in the hall. One style of each for a house, by where it stands; in a wrecked one the
    // chairs are over and the lamp is down.
    // What stands up takes a tile of its own, and the engine picks it (Arturo, 19/10: «que ocupen»; Furnishing.cs): the
    // armchairs, the coffee table, the lamps, the plants, the bedside tables, the chair in a corner, the towel rail and
    // the coat stand are drawn where its decor says, while their tile stands. Only what lies flat (the rugs) or on the
    // furniture (a lamp, books, the bread bin) is picked here, on clear floor; chairs round a table are tucked in on its
    // own tile. The bed, the sofa, the table and the bath take two tiles or four (docs/MAPAS.md, phase 1): what goes with
    // them goes round the whole of them. Then the shops dressed as their trade, a barn's buckets, and what the engine
    // stood on tiles of their own in shops, libraries and barns.
    // The math is three's in doubles (Matrix4.compose, Quaternion.setFromEuler 'YXZ' and setFromAxisAngle), each element
    // made a float only as it goes into the matrix; sines and cosines V8's (Trig). No Mathf: it runs in a plain dotnet
    // harness.

    /// <summary>
    /// A thing put in a room (homeFurnishing.ts Furnishing): which prop (PropId), the matrix that places it in 0.10.1's
    /// world, in tiles (x east, y up, z south), the same mathematical matrix three's Matrix4 was, and its tile and storey
    /// (for the cutaway, and to drop it if its floor goes).
    /// </summary>
    public sealed class Furnishing
    {
        public string id;
        public Matrix4x4 matrix;
        public int x, y, z;
        /// <summary>Casts a shadow: false for what lies flat (a rug) or stands on the furniture of its tile (a lamp on the bedside table).</summary>
        public bool cast;
    }

    /// <summary>What is put in the houses, shops and barns beyond their cover (homeFurnishing.ts furnishHomes, furnishShops, furnishBarns, furnishOwnTiles).</summary>
    public static class HomeFurnishing
    {
        /// <summary>A storey's height in tiles, as a double (coords.ts STOREY; BuildingPlan.Storey is the float).</summary>
        const double Storey = 2.3;

        /// <summary>How high the top of what stands on a tile is, for what is put on it (tiles).</summary>
        static readonly Dictionary<string, double> Top = new Dictionary<string, double>
        {
            [DecorKinds.Table] = 0.57, [DecorKinds.Desk] = 0.5, [DecorKinds.Counter] = 0.6, [DecorKinds.Stove] = 0.6,
        };
        const double BedsideTop = 0.3;
        /// <summary>How far from a table's middle the chairs tucked in round it sit, on its own tile (and the stools at a counter).</summary>
        const double Tucked = 0.3;

        static readonly HashSet<string> Ways = new HashSet<string> { TileKinds.Door, TileKinds.DoorOpen, TileKinds.Ladder };

        /// <summary>Grid.Dirs4 (UnityEngine has a Grid of its own).</summary>
        static readonly IReadOnlyList<Vec2> Dirs4 = Bastion.Engine.Grid.Dirs4;

        /// <summary>Stable pseudo-random value in [0, 1) (JavaScript's 32-bit integers: Math.imul, >>>).</summary>
        static double Hash(int x, int y, int salt)
        {
            unchecked
            {
                int h = (x + 31) * 374761393 + (y + 7) * 668265263 + (salt + 13) * 2147483647;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>Quarter turns (counter-clockwise from above) that make a prop modelled facing north face DIRS4[f].</summary>
        static int TurnTo(int f) => (4 - f) % 4;

        /// <summary>Cover still standing: what the engine put there, and no blast took.</summary>
        static bool Standing(string tile) => tile == TileKinds.Low || tile == TileKinds.High;

        /// <summary>`tiles[i]`: null past its end.</summary>
        static string TileIn(IList<string> tiles, int i) => tiles != null && i >= 0 && i < tiles.Count ? tiles[i] : null;

        /// <summary>`decorOf(decor?.[i])`: null without decor or past its end.</summary>
        static DecorLook Look(IList<int> decor, int i) => decor != null && i >= 0 && i < decor.Count ? Bastion.Engine.Decor.DecorOf(decor[i]) : null;

        /// <summary>`s.elev[i] ?? 0`.</summary>
        static int ElevAt(GameState s, int i) => s.Elev != null && i >= 0 && i < s.Elev.Count ? s.Elev[i] : 0;

        /// <summary>Storey z's tiles and decor (`z ? s.levels?.[z - 1] : { tiles: s.tiles, decor: s.decor }`); false past the levels.</summary>
        static bool Layer(GameState s, int z, out List<string> tiles, out List<int> decor)
        {
            if (z == 0)
            {
                tiles = s.Tiles;
                decor = s.Decor;
                return true;
            }
            var level = s.Levels != null && z - 1 >= 0 && z - 1 < s.Levels.Count ? s.Levels[z - 1] : null;
            tiles = level?.Tiles;
            decor = level?.Decor;
            return level != null;
        }

        /// <summary>A THREE.Quaternion's doubles.</summary>
        readonly struct Quat
        {
            public readonly double x, y, z, w;

            Quat(double x, double y, double z, double w)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.w = w;
            }

            /// <summary>`setFromEuler(new THREE.Euler(ex, ey, ez, 'YXZ'))`.</summary>
            public static Quat Euler(double ex, double ey, double ez)
            {
                double c1 = Trig.Cos(ex / 2), c2 = Trig.Cos(ey / 2), c3 = Trig.Cos(ez / 2);
                double s1 = Trig.Sin(ex / 2), s2 = Trig.Sin(ey / 2), s3 = Trig.Sin(ez / 2);
                return new Quat(s1 * c2 * c3 + c1 * s2 * s3, c1 * s2 * c3 - s1 * c2 * s3, c1 * c2 * s3 - s1 * s2 * c3, c1 * c2 * c3 + s1 * s2 * s3);
            }

            /// <summary>`setFromAxisAngle(new THREE.Vector3(0, 1, 0), angle)`.</summary>
            public static Quat AxisY(double angle)
            {
                double half = angle / 2, s = Trig.Sin(half);
                return new Quat(0 * s, 1 * s, 0 * s, Trig.Cos(half));
            }
        }

        // V8's Math.sin and Math.cos are fdlibm's (src/base/ieee754.cc: k_sin.c, k_cos.c, e_rem_pio2.c), not the C
        // library's: .NET's Math.Sin and Math.Cos are the C runtime's, the last bit off for one argument in twenty on the
        // Mac, and that shows in the matrix's elements that should come out 0 (a chair knocked over at a right angle). As
        // Js.cs's atan: clang on arm64 fuses each `a + b * c` of an expression into a multiply-add, here Js.FusedMultiplyAdd;
        // with them, the same bits as node on Apple silicon (1.3 million arguments, up to ±1000 and near every π/4). The
        // reduction of arguments past 2^19 π/2 (__kernel_rem_pio2) is not here: no angle of these comes near it. Shared
        // with BasePieces.
        internal static class Trig
        {
            const double S1 = -1.66666666666666324348e-01; // 0xBFC55555, 0x55555549
            const double S2 = 8.33333333332248946124e-03; // 0x3F811111, 0x1110F8A6
            const double S3 = -1.98412698298579493134e-04; // 0xBF2A01A0, 0x19C161D5
            const double S4 = 2.75573137070700676789e-06; // 0x3EC71DE3, 0x57B1FE7D
            const double S5 = -2.50507602534068634195e-08; // 0xBE5AE5E6, 0x8A2B9CEB
            const double S6 = 1.58969099521155010221e-10; // 0x3DE5D93A, 0x5ACFD57C
            const double C1 = 4.16666666666666019037e-02; // 0x3FA55555, 0x5555554C
            const double C2 = -1.38888888888741095749e-03; // 0xBF56C16C, 0x16C15177
            const double C3 = 2.48015872894767294178e-05; // 0x3EFA01A0, 0x19CB1590
            const double C4 = -2.75573143513906633035e-07; // 0xBE927E4F, 0x809C52AD
            const double C5 = 2.08757232129817482790e-09; // 0x3E21EE9E, 0xBDB4B1C4
            const double C6 = -1.13596475577881948265e-11; // 0xBDA8FAE9, 0xBE8838D4
            /// <summary>53 bits of 2/π; π/2 in three pieces of 33 bits, and what each leaves.</summary>
            const double Invpio2 = 6.36619772367581382433e-01; // 0x3FE45F30, 0x6DC9C883
            const double Pio2_1 = 1.57079632673412561417e+00; // 0x3FF921FB, 0x54400000
            const double Pio2_1t = 6.07710050650619224932e-11; // 0x3DD0B461, 0x1A626331
            const double Pio2_2 = 6.07710050630396597660e-11; // 0x3DD0B461, 0x1A600000
            const double Pio2_2t = 2.02226624879595063154e-21; // 0x3BA3198A, 0x2E037073
            const double Pio2_3 = 2.02226624871116645580e-21; // 0x3BA3198A, 0x2E000000
            const double Pio2_3t = 8.47842766036889956997e-32; // 0x397B839A, 0x252049C1

            /// <summary>The high words of n π/2, n = 1…32 (where the quick reduction may cancel).</summary>
            static readonly int[] Npio2Hw =
            {
                0x3FF921FB, 0x400921FB, 0x4012D97C, 0x401921FB, 0x401F6A7A, 0x4022D97C, 0x4025FDBB, 0x402921FB, 0x402C463A, 0x402F6A7A, 0x4031475C,
                0x4032D97C, 0x40346B9C, 0x4035FDBB, 0x40378FDB, 0x403921FB, 0x403AB41B, 0x403C463A, 0x403DD85A, 0x403F6A7A, 0x40407E4C, 0x4041475C,
                0x4042106C, 0x4042D97C, 0x4043A28C, 0x40446B9C, 0x404534AC, 0x4045FDBB, 0x4046C6CB, 0x40478FDB, 0x404858EB, 0x404921FB,
            };

            static double Fma(double a, double b, double c) => Js.FusedMultiplyAdd(a, b, c);

            static int HighWord(double x) => (int)(BitConverter.DoubleToInt64Bits(x) >> 32);

            /// <summary>__kernel_sin: sin(x + y) on [-π/4, π/4], y the tail of x (iy 0: none).</summary>
            static double KernelSin(double x, double y, int iy)
            {
                int ix = HighWord(x) & 0x7FFFFFFF;
                if (ix < 0x3E400000 && (int)x == 0) return x; // |x| < 2^-27
                double z = x * x;
                double v = z * x;
                // S2 + z * (S3 + z * (S4 + z * (S5 + z * S6)))
                double r = Fma(z, Fma(z, Fma(z, Fma(z, S6, S5), S4), S3), S2);
                // x + v * (S1 + z * r)
                if (iy == 0) return Fma(v, Fma(z, r, S1), x);
                // x - ((z * (half * y - v * r) - y) - v * S1)
                return x - Fma(-v, S1, Fma(z, Fma(0.5, y, -(v * r)), -y));
            }

            /// <summary>__kernel_cos: cos(x + y) on [-π/4, π/4], y the tail of x.</summary>
            static double KernelCos(double x, double y)
            {
                int ix = HighWord(x) & 0x7FFFFFFF;
                if (ix < 0x3E400000 && (int)x == 0) return 1; // |x| < 2^-27
                double z = x * x;
                // z * (C1 + z * (C2 + z * (C3 + z * (C4 + z * (C5 + z * C6)))))
                double r = z * Fma(z, Fma(z, Fma(z, Fma(z, Fma(z, C6, C5), C4), C3), C2), C1);
                // one - (0.5 * z - (z * r - x * y)), |x| < 0.3
                if (ix < 0x3FD33333) return 1 - Fma(0.5, z, -Fma(z, r, -(x * y)));
                // x > 0.78125: 0.28125; else x / 4 (its high word less 2 in the exponent).
                double qx = ix > 0x3FE90000 ? 0.28125 : BitConverter.Int64BitsToDouble((long)(ix - 0x00200000) << 32);
                double iz = Fma(0.5, z, -qx);
                double a = 1 - qx;
                // a - (iz - (z * r - x * y))
                return a - (iz - Fma(z, r, -(x * y)));
            }

            /// <summary>__ieee754_rem_pio2 for |x| up to 2^19 π/2: x - n π/2 as y0 + y1; returns n.</summary>
            static int RemPio2(double x, out double y0, out double y1)
            {
                int hx = HighWord(x);
                int ix = hx & 0x7FFFFFFF;
                if (ix <= 0x3FE921FB)
                {
                    // |x| ~<= π/4: no need for reduction.
                    y0 = x;
                    y1 = 0;
                    return 0;
                }
                double z;
                if (ix < 0x4002D97C)
                {
                    // |x| < 3π/4: n is ±1.
                    if (hx > 0)
                    {
                        z = x - Pio2_1;
                        if (ix != 0x3FF921FB)
                        {
                            // 33 + 53 bits of π are enough.
                            y0 = z - Pio2_1t;
                            y1 = (z - y0) - Pio2_1t;
                        }
                        else
                        {
                            // Near π/2: 33 + 33 + 53 bits.
                            z -= Pio2_2;
                            y0 = z - Pio2_2t;
                            y1 = (z - y0) - Pio2_2t;
                        }
                        return 1;
                    }
                    z = x + Pio2_1;
                    if (ix != 0x3FF921FB)
                    {
                        y0 = z + Pio2_1t;
                        y1 = (z - y0) + Pio2_1t;
                    }
                    else
                    {
                        z += Pio2_2;
                        y0 = z + Pio2_2t;
                        y1 = (z - y0) + Pio2_2t;
                    }
                    return -1;
                }
                // Medium size.
                double t = Math.Abs(x);
                int n = (int)Fma(t, Invpio2, 0.5);
                double fn = n;
                double r = Fma(-fn, Pio2_1, t);
                double w = fn * Pio2_1t; // first round, good to 85 bits
                if (n < 32 && ix != Npio2Hw[n - 1]) y0 = r - w; // quick check, no cancellation
                else
                {
                    int j = ix >> 20;
                    y0 = r - w;
                    int i = j - ((HighWord(y0) >> 20) & 0x7FF);
                    if (i > 16)
                    {
                        // A second iteration, good to 118 bits.
                        t = r;
                        w = fn * Pio2_2;
                        r = t - w;
                        w = Fma(fn, Pio2_2t, -((t - r) - w));
                        y0 = r - w;
                        i = j - ((HighWord(y0) >> 20) & 0x7FF);
                        if (i > 49)
                        {
                            // A third, 151 bits: covers every case.
                            t = r;
                            w = fn * Pio2_3;
                            r = t - w;
                            w = Fma(fn, Pio2_3t, -((t - r) - w));
                            y0 = r - w;
                        }
                    }
                }
                y1 = (r - y0) - w;
                if (hx < 0)
                {
                    y0 = -y0;
                    y1 = -y1;
                    return -n;
                }
                return n;
            }

            /// <summary>Math.sin.</summary>
            public static double Sin(double x)
            {
                int ix = HighWord(x) & 0x7FFFFFFF;
                if (ix <= 0x3FE921FB) return KernelSin(x, 0, 0);
                if (ix >= 0x7FF00000) return x - x; // NaN
                if (ix > 0x413921FB) return Math.Sin(x); // past the medium size: not V8's
                switch (RemPio2(x, out double y0, out double y1) & 3)
                {
                    case 0: return KernelSin(y0, y1, 1);
                    case 1: return KernelCos(y0, y1);
                    case 2: return -KernelSin(y0, y1, 1);
                    default: return -KernelCos(y0, y1);
                }
            }

            /// <summary>Math.cos.</summary>
            public static double Cos(double x)
            {
                int ix = HighWord(x) & 0x7FFFFFFF;
                if (ix <= 0x3FE921FB) return KernelCos(x, 0);
                if (ix >= 0x7FF00000) return x - x; // NaN
                if (ix > 0x413921FB) return Math.Cos(x); // past the medium size: not V8's
                switch (RemPio2(x, out double y0, out double y1) & 3)
                {
                    case 0: return KernelCos(y0, y1);
                    case 1: return -KernelSin(y0, y1, 1);
                    case 2: return -KernelCos(y0, y1);
                    default: return KernelSin(y0, y1, 1);
                }
            }
        }

        /// <summary>
        /// `new THREE.Matrix4().compose((px, py, pz), q, (k, k, k))`, in doubles. Three's elements are column-major (te[0..3]
        /// the first column, te[12..14] the position): each goes to the same row and column here, a float only at the end.
        /// </summary>
        static Matrix4x4 Compose(double px, double py, double pz, Quat q, double k)
        {
            double x = q.x, y = q.y, z = q.z, w = q.w;
            double x2 = x + x, y2 = y + y, z2 = z + z;
            double xx = x * x2, xy = x * y2, xz = x * z2;
            double yy = y * y2, yz = y * z2, zz = z * z2;
            double wx = w * x2, wy = w * y2, wz = w * z2;
            var m = new Matrix4x4();
            m.m00 = (float)((1 - (yy + zz)) * k);
            m.m10 = (float)((xy + wz) * k);
            m.m20 = (float)((xz - wy) * k);
            m.m30 = 0;
            m.m01 = (float)((xy - wz) * k);
            m.m11 = (float)((1 - (xx + zz)) * k);
            m.m21 = (float)((yz + wx) * k);
            m.m31 = 0;
            m.m02 = (float)((xz + wy) * k);
            m.m12 = (float)((yz - wx) * k);
            m.m22 = (float)((1 - (xx + yy)) * k);
            m.m32 = 0;
            m.m03 = (float)px;
            m.m13 = (float)py;
            m.m23 = (float)pz;
            m.m33 = 1;
            return m;
        }

        /// <summary>furnishHomes: every house's rooms.</summary>
        public static List<Furnishing> Homes(GameState s)
        {
            var @out = new List<Furnishing>();
            if (s.Homes != null) foreach (var home in s.Homes) FurnishHome(s, home, @out);
            return @out;
        }

        static void FurnishHome(GameState s, HomeSite home, List<Furnishing> @out)
        {
            int w = s.Width;
            // One style for the whole house.
            int Style(int salt, int n) => (int)Math.Floor(Hash(home.X, home.Y, salt) * n);
            string chair = Style(1, 2) != 0 ? "diningChair2" : "diningChair";
            string rug = new[] { "rug", "rug2", "rug3" }[Style(2, 3)];
            string lamp = Style(3, 2) != 0 ? "floorLamp2" : "floorLamp";
            string small = Style(4, 2) != 0 ? "tableLamp2" : "tableLamp";
            string[] plants = { "pottedPlant", "pottedPlant2", "pottedPlant3" };
            bool wrecked = home.Wear > 0.6;
            var used = new HashSet<(int, int, int)>();
            foreach (var room in home.Rooms)
            {
                if (!Layer(s, room.Z, out var tiles, out var decor)) continue;
                string TileAt(int x, int y) => x < 0 || y < 0 || x >= w || y >= s.Height ? null : TileIn(tiles, y * w + x);
                bool InRoom((int x, int y) p) => p.x >= room.X && p.y >= room.Y && p.x < room.X + room.W && p.y < room.Y + room.H;
                (int, int, int) Key((int x, int y) p) => (p.x, p.y, room.Z);
                // Clear floor in the room, off the ways through its doors and up its ladders, nothing put there yet: for what lies flat.
                bool Clear((int x, int y) p) =>
                    InRoom(p) && TileAt(p.x, p.y) == TileKinds.Floor && Look(decor, p.y * w + p.x) == null && !used.Contains(Key(p))
                    && !Dirs4.Any((d) => Ways.Contains(TileAt(p.x + d.X, p.y + d.Y) ?? TileKinds.Floor));
                double @base = room.Z * Storey + (room.Z != 0 ? 0 : ElevAt(s, room.Y * w + room.X) * Storey);
                double R((int x, int y) p, int salt) => Hash(p.x * 3 + room.Z, p.y * 5, salt);
                // `id` on tile p, its front to DIRS4[f] (a turn more `turn`), off the tile's middle by (du, dv) along x and y, `up` (o.y) up.
                void Put(string id, (int x, int y) p, int f, double du = 0, double dv = 0, double up = 0, double turn = 0, double k = 1, double over = 0, bool cast = true)
                {
                    var m = Compose(p.x + 0.5 + du, @base + up, p.y + 0.5 + dv, Quat.Euler(0, TurnTo(f) * Math.PI / 2 + turn, over), k);
                    @out.Add(new Furnishing { id = id, matrix = m, x = p.x, y = p.y, z = room.Z, cast = cast });
                    if (up == 0) used.Add(Key(p));
                }
                // Knocked over in a wrecked house (now and then): on its side.
                double Over((int x, int y) p, int salt) => wrecked && R(p, salt) < 0.45 ? Math.PI / 2 : 0;
                var things = new List<((int x, int y) p, string kind, int facing)>();
                for (int y = room.Y; y < room.Y + room.H; y++)
                {
                    for (int x = room.X; x < room.X + room.W; x++)
                    {
                        var look = Look(decor, y * w + x);
                        if (look != null && Standing(TileAt(x, y))) things.Add(((x, y), look.Kind, look.Facing));
                    }
                }
                List<((int x, int y) p, string kind, int facing)> Of(string kind) => things.Where((t) => t.kind == kind).ToList();
                (int x, int y) Step((int x, int y) p, int f) => (p.x + Dirs4[f].X, p.y + Dirs4[f].Y);
                // The tiles of the piece anchored on p (several for a bed, a sofa, a table, a bath: docs/MAPAS.md, phase 1).
                List<(int x, int y)> TilesOf((int x, int y) p)
                {
                    int i = p.y * w + p.x;
                    var own = (decor != null ? Bastion.Engine.Decor.ObjectAt(decor, w, i)?.Tiles : null) ?? new List<int> { i };
                    return own.Select((j) => (j % w, (int)Math.Floor((double)j / w))).ToList();
                }
                // Before a piece facing f, at its middle: the tile ahead of its front row nearest the anchor's, and how far off
                // that tile's middle the piece's middle lies across (a sofa or a bed two wide).
                ((int x, int y) at, double du, double dv) Before((int x, int y) p, int f)
                {
                    var own = TilesOf(p);
                    var d = Dirs4[f];
                    int depth = own.Max((q) => (q.x - p.x) * d.X + (q.y - p.y) * d.Y) + 1;
                    double midX = (double)own.Sum((q) => q.x) / own.Count - p.x;
                    double midY = (double)own.Sum((q) => q.y) / own.Count - p.y;
                    return ((p.x + d.X * depth, p.y + d.Y * depth), d.X != 0 ? 0 : midX, d.Y != 0 ? 0 : midY);
                }
                // Pushed into the corner of the room it stands in (along no wall, not at all).
                (double du, double dv) Cornered((int x, int y) p, double by = 0.15) =>
                    (p.x == room.X ? -by : p.x == room.X + room.W - 1 ? by : 0, p.y == room.Y ? -by : p.y == room.Y + room.H - 1 ? by : 0);
                // Chairs round a table, tucked in on its own tiles on their sides facing open floor (three at most, four round
                // one of two tiles), facing it; over in a wrecked house.
                void RoundTable((int x, int y) t)
                {
                    var own = TilesOf(t);
                    int n = 0;
                    foreach (var q in own)
                    {
                        for (int f = 0; f < 4 && n < (own.Count > 1 ? 4 : 3); f++)
                        {
                            var side = Step(q, f);
                            if (!InRoom(side) || TileAt(side.x, side.y) != TileKinds.Floor) continue;
                            Put(chair, q, (f + 2) % 4, du: Dirs4[f].X * Tucked, dv: Dirs4[f].Y * Tucked, up: 0.001, over: Over(side, 70 + f), turn: (R(side, 80) - 0.5) * 0.3);
                            n++;
                        }
                    }
                }
                // What the engine put in the room on tiles of their own, each as it was drawn.
                foreach (var t in things)
                {
                    switch (t.kind)
                    {
                        case DecorKinds.Armchair:
                            Put(wrecked ? "armchairWorn" : "armchair", t.p, t.facing, over: Over(t.p, 20));
                            break;
                        case DecorKinds.CoffeeTable:
                            Put("coffeeTable", t.p, t.facing);
                            break;
                        case DecorKinds.FloorLamp:
                        {
                            var (du, dv) = Cornered(t.p);
                            Put(lamp, t.p, t.facing, du: du, dv: dv, over: Over(t.p, 31));
                            break;
                        }
                        case DecorKinds.PottedPlant:
                        {
                            int salt = room.Use == HomeUses.Kitchen ? 11 : room.Use == HomeUses.Bedroom ? 10 : room.Use == HomeUses.Study ? 8 : room.Use == HomeUses.Hall ? 9 : 5;
                            var (du, dv) = Cornered(t.p);
                            Put(plants[Style(salt, 3)], t.p, t.facing, du: du, dv: dv, k: room.Use == HomeUses.Kitchen || room.Use == HomeUses.Hall ? 0.9 : 1);
                            break;
                        }
                        case DecorKinds.DiningChair:
                        {
                            var (du, dv) = Cornered(t.p);
                            Put(chair, t.p, t.facing, du: du, dv: dv);
                            break;
                        }
                        case DecorKinds.Nightstand:
                        {
                            // Against the bed's side, towards its head; its lamp on it (in a wrecked house, now and then).
                            int bed = -1;
                            for (int f = 0; f < 4 && bed < 0; f++)
                            {
                                var d = Dirs4[f];
                                if (decor != null && Bastion.Engine.Decor.ObjectAt(decor, w, (t.p.y + d.Y) * w + t.p.x + d.X)?.Kind == DecorKinds.Bed) bed = f;
                            }
                            int side = bed >= 0 ? (bed + 2) % 4 : (t.facing + 1) % 4;
                            double du = -Dirs4[side].X * 0.25 - Dirs4[t.facing].X * 0.3, dv = -Dirs4[side].Y * 0.25 - Dirs4[t.facing].Y * 0.3;
                            Put("bedsideTable", t.p, t.facing, du: du, dv: dv);
                            if (!wrecked || R(t.p, 40) < 0.5) Put(small, t.p, t.facing, du: du, dv: dv, up: BedsideTop, cast: false);
                            break;
                        }
                        case DecorKinds.TowelRack:
                        {
                            var wall = Dirs4[(t.facing + 2) % 4];
                            Put("towelRack", t.p, t.facing, du: wall.X * 0.3, dv: wall.Y * 0.3);
                            break;
                        }
                        case DecorKinds.CoatRack:
                        {
                            var (du, dv) = Cornered(t.p, 0.2);
                            Put("coatRack", t.p, t.facing, du: du, dv: dv, over: Over(t.p, 60));
                            break;
                        }
                    }
                }
                switch (room.Use)
                {
                    case HomeUses.Living:
                    {
                        // The sofa's rug before it (under the coffee table, if one stands there), at its middle.
                        foreach (var sofa in Of(DecorKinds.Sofa))
                        {
                            var ahead = Before(sofa.p, sofa.facing);
                            if (InRoom(ahead.at)) Put(rug, ahead.at, sofa.facing, du: ahead.du - Dirs4[sofa.facing].X * 0.45, dv: ahead.dv - Dirs4[sofa.facing].Y * 0.45, up: 0.001, cast: false);
                        }
                        foreach (var table in Of(DecorKinds.Table)) if (Of(DecorKinds.Sofa).Count == 0) RoundTable(table.p);
                        break;
                    }
                    case HomeUses.Kitchen:
                    {
                        foreach (var table in Of(DecorKinds.Table)) RoundTable(table.p);
                        // On the counter: the bread bin, or the coffee machine.
                        var counters = Of(DecorKinds.Counter);
                        if (counters.Count > 0)
                        {
                            var counter = counters[0];
                            Put(Style(6, 2) != 0 ? "breadBin" : "coffeeMachine", counter.p, counter.facing, up: Top[DecorKinds.Counter], du: -Dirs4[counter.facing].X * 0.2, dv: -Dirs4[counter.facing].Y * 0.2);
                        }
                        break;
                    }
                    case HomeUses.Bedroom:
                    {
                        // A rug at the foot of the bed.
                        foreach (var bed in Of(DecorKinds.Bed))
                        {
                            var foot = Before(bed.p, bed.facing);
                            if (Clear(foot.at)) Put("rug3", foot.at, bed.facing, du: foot.du - Dirs4[bed.facing].X * 0.2, dv: foot.dv - Dirs4[bed.facing].Y * 0.2, cast: false, k: 0.8);
                        }
                        break;
                    }
                    case HomeUses.Bath:
                    {
                        // The bath rug beside the tub (by its anchor first).
                        var tubs = Of(DecorKinds.Bathtub);
                        var spots = tubs.Count > 0 ? TilesOf(tubs[0].p).SelectMany((q) => new[] { 0, 1, 2, 3 }.Select((f) => (p: Step(q, f), f))).ToList() : new List<((int x, int y) p, int f)>();
                        int spot = spots.FindIndex((o) => Clear(o.p));
                        if (spot >= 0) Put("bathRug", spots[spot].p, spots[spot].f, cast: false);
                        break;
                    }
                    case HomeUses.Toilet:
                    {
                        // The mat before the toilet.
                        var wcs = Of(DecorKinds.Wc);
                        if (wcs.Count > 0)
                        {
                            var ahead = Step(wcs[0].p, wcs[0].facing);
                            if (Clear(ahead)) Put("bathRug", ahead, wcs[0].facing, cast: false, k: 0.8);
                        }
                        break;
                    }
                    case HomeUses.Study:
                    {
                        // (Its chair is the map's, as every desk's.)
                        foreach (var desk in Of(DecorKinds.Desk))
                        {
                            Put(small, desk.p, desk.facing, up: Top[DecorKinds.Desk], du: 0.25, cast: false);
                            Put(Style(7, 2) != 0 ? "bookPile2" : "bookPile", desk.p, desk.facing, up: Top[DecorKinds.Desk], du: -0.2, turn: 0.3, cast: false);
                        }
                        var middle = (room.X + (int)Math.Floor(room.W / 2.0), room.Y + (int)Math.Floor(room.H / 2.0));
                        if (Clear(middle)) Put(rug, middle, 0, cast: false, k: 0.9);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// A shop's trade as its floor shows it (homeFurnishing.ts shopTradeIn, with shopTrade; MapBuilder.ShopTradeIn is the
        /// same, but its own): by what the engine furnished it with (a café's tables, a reading table, a drinks fridge), else
        /// by its looks, as the engine picked it from the same number.
        /// </summary>
        static string ShopTradeIn(GameState s, Building b)
        {
            for (int y = b.y0; y <= b.y1; y++)
            {
                for (int x = b.x0; x <= b.x1; x++)
                {
                    string kind = Look(s.Decor, y * s.Width + x)?.Kind;
                    if (kind == DecorKinds.CafeTable) return ShopTrades.Cafe;
                    if (kind == DecorKinds.ReadingTable) return ShopTrades.Books;
                    if (kind == DecorKinds.DrinksFridge) return ShopTrades.Grocer;
                }
            }
            return b.look < 0.3 ? ShopTrades.Books : b.look < 0.55 ? ShopTrades.Cafe : ShopTrades.Grocer;
        }

        /// <summary>
        /// furnishShops: the shops dressed as their trade (playtest 18/10, #36), on their ground floor: a bookshop's books on
        /// its counter; a café's stools at the bar, tucked in under it, and the coffee machine on it; a grocer's cooler and
        /// boxes of stock by the shelves. Upstairs, the stock room's boxes. Small things, on clear floor or on the furniture.
        /// The tables and the drinks fridge stand on tiles of their own (OwnTiles). `buildings`: BuildingPlan.Buildings.
        /// </summary>
        public static List<Furnishing> Shops(GameState s, IReadOnlyList<Building> buildings)
        {
            var @out = new List<Furnishing>();
            int w = s.Width;
            foreach (var b in buildings)
            {
                bool library = b.kind == BuildingKinds.Hall && b.look < 0.3;
                if (b.kind != BuildingKinds.Shop && !library) continue;
                string trade = library ? ShopTrades.Books : ShopTradeIn(s, b);
                for (int z = 0; z <= (s.Levels?.Count ?? 0); z++)
                {
                    if (!Layer(s, z, out var tiles, out var decor)) continue;
                    string TileAt(int x, int y) => x < b.x0 || y < b.y0 || x > b.x1 || y > b.y1 ? null : TileIn(tiles, y * w + x);
                    DecorLook LookAt(int x, int y) => Look(decor, y * w + x);
                    var used = new HashSet<int>();
                    bool Clear(int x, int y) =>
                        TileAt(x, y) == TileKinds.Floor && LookAt(x, y) == null && !used.Contains(y * w + x) && !Dirs4.Any((d) => Ways.Contains(TileAt(x + d.X, y + d.Y) ?? TileKinds.Floor));
                    double @base = z * Storey;
                    void Put(string id, int x, int y, double yaw, double du = 0, double dv = 0, double up = 0, double k = 1, bool cast = true)
                    {
                        var m = Compose(x + 0.5 + du, @base + up, y + 0.5 + dv, Quat.AxisY(yaw), k);
                        @out.Add(new Furnishing { id = id, matrix = m, x = x, y = y, z = z, cast = cast });
                        if (up == 0) used.Add(y * w + x);
                    }
                    double R(int x, int y, int salt) => Hash(x * 3 + z, y * 5, salt);
                    var counters = new List<(int x, int y, int f)>();
                    var shelves = new List<(int x, int y, int f)>();
                    for (int y = b.y0; y <= b.y1; y++)
                    {
                        for (int x = b.x0; x <= b.x1; x++)
                        {
                            var look = LookAt(x, y);
                            if (look == null || !Standing(TileAt(x, y))) continue;
                            if (look.Kind == DecorKinds.Counter) counters.Add((x, y, look.Facing));
                            if (look.Kind == DecorKinds.Shelf) shelves.Add((x, y, look.Facing));
                        }
                    }
                    double YawOf(int f) => TurnTo(f) * Math.PI / 2;
                    if (z > 0)
                    {
                        // The stock room: boxes by the shelves.
                        foreach (var sh in shelves)
                        {
                            var d = Dirs4[sh.f];
                            int px = sh.x + d.X, py = sh.y + d.Y;
                            if (Clear(px, py) && R(px, py, 1) < 0.3) Put("cardboard", px, py, R(px, py, 2) * 6.3, du: -d.X * 0.2, dv: -d.Y * 0.2);
                        }
                        continue;
                    }
                    switch (trade)
                    {
                        case ShopTrades.Books:
                            for (int k = 0; k < counters.Count; k++)
                            {
                                var c = counters[k];
                                if (k % 2 == 0) Put(k % 4 != 0 ? "bookPile3" : "bookPile", c.x, c.y, YawOf(c.f) + R(c.x, c.y, 4) * 0.6, up: Top[DecorKinds.Counter], cast: false);
                            }
                            break;
                        case ShopTrades.Cafe:
                            // The stools at the bar, tucked in under it on its tile.
                            for (int k = 0; k < counters.Count; k++)
                            {
                                var c = counters[k];
                                Put("barStool", c.x, c.y, YawOf(c.f), du: Dirs4[c.f].X * Tucked, dv: Dirs4[c.f].Y * Tucked, up: 0.001);
                                if (k == 1) Put("coffeeMachine", c.x, c.y, YawOf(c.f), up: Top[DecorKinds.Counter], cast: false);
                            }
                            break;
                        case ShopTrades.Grocer:
                        {
                            // The cooler by the counter, stock by the shelves.
                            if (counters.Count > 0)
                            {
                                var c = counters[0];
                                foreach (var d in Dirs4)
                                {
                                    if (!Clear(c.x + d.X, c.y + d.Y)) continue;
                                    Put("drinksCooler", c.x + d.X, c.y + d.Y, YawOf(c.f));
                                    break;
                                }
                            }
                            foreach (var sh in shelves)
                            {
                                var d = Dirs4[sh.f];
                                int px = sh.x + d.X, py = sh.y + d.Y;
                                if (Clear(px, py) && R(px, py, 6) < 0.25) Put("cardboard", px, py, R(px, py, 7) * 6.3, du: -d.X * 0.25, dv: -d.Y * 0.25);
                            }
                            break;
                        }
                    }
                }
            }
            return @out;
        }

        /// <summary>
        /// furnishOwnTiles: what the engine stood on tiles of their own in shops, libraries and barns (Furnishing.cs),
        /// wherever they are and while they stand, whatever the client makes of the building: a café's tables with their two
        /// chairs, a reading table with its chair, tucked in on its tile; a grocer's drinks fridge against its wall; a barn's
        /// tools leant against its wall and its wheelbarrow.
        /// </summary>
        public static List<Furnishing> OwnTiles(GameState s)
        {
            var @out = new List<Furnishing>();
            int w = s.Width;
            int tools = 0;
            for (int z = 0; z <= (s.Levels?.Count ?? 0); z++)
            {
                if (!Layer(s, z, out var tiles, out var decor) || decor == null) continue;
                for (int i = 0; i < tiles.Count; i++)
                {
                    var look = Look(decor, i);
                    if (look == null || !Standing(tiles[i])) continue;
                    int x = i % w;
                    int y = (i - x) / w;
                    double R(int salt) => Hash(x * 3 + z, y * 5, salt);
                    double @base = z * Storey + (z != 0 ? 0 : ElevAt(s, i) * Storey);
                    void Put(string id, double yaw, double du = 0, double dv = 0, double up = 0, double lean = 0) =>
                        @out.Add(new Furnishing { id = id, matrix = Compose(x + 0.5 + du, @base + up, y + 0.5 + dv, Quat.Euler(lean, yaw, 0), 1), x = x, y = y, z = z, cast = true });
                    switch (look.Kind)
                    {
                        case DecorKinds.CafeTable:
                        case DecorKinds.ReadingTable:
                        {
                            bool cafe = look.Kind == DecorKinds.CafeTable;
                            double turn = R(3) * 0.6;
                            Put(cafe ? "cafeTable" : "coffeeTable", turn);
                            for (int k = 0; k < (cafe ? 2 : 1); k++)
                            {
                                double a = turn + k * Math.PI + Math.PI / 2;
                                Put(cafe ? "cafeChair" : "diningChair", a + Math.PI, du: Trig.Sin(a) * Tucked, dv: Trig.Cos(a) * Tucked, up: 0.001);
                            }
                            break;
                        }
                        case DecorKinds.DrinksFridge:
                        {
                            var wall = Dirs4[(look.Facing + 2) % 4];
                            Put("drinksFridge", TurnTo(look.Facing) * Math.PI / 2, du: wall.X * 0.2, dv: wall.Y * 0.2);
                            break;
                        }
                        case DecorKinds.Tools:
                        {
                            // Leant against the wall, its handle up to it.
                            var d = Dirs4[look.Facing];
                            Put(new[] { "pitchfork", "rake", "spade" }[tools++ % 3], TurnTo(look.Facing) * Math.PI / 2 + (R(2) - 0.5) * 0.4, du: d.X * 0.36, dv: d.Y * 0.36, lean: -0.22);
                            break;
                        }
                        case DecorKinds.Wheelbarrow:
                            Put("wheelbarrow", R(4) * 6.3);
                            break;
                    }
                }
            }
            return @out;
        }

        /// <summary>furnishBarns: a barn's floor (playtest 18/10, #53), buckets on clear ground (its tools and wheelbarrow, OwnTiles). `buildings`: BuildingPlan.Buildings.</summary>
        public static List<Furnishing> Barns(GameState s, IReadOnlyList<Building> buildings)
        {
            var @out = new List<Furnishing>();
            int w = s.Width;
            foreach (var b in buildings)
            {
                if (b.kind != BuildingKinds.Barn) continue;
                string TileAt(int x, int y) => x < b.x0 || y < b.y0 || x > b.x1 || y > b.y1 ? null : TileIn(s.Tiles, y * w + x);
                var used = new HashSet<int>();
                bool Clear(int x, int y) =>
                    TileAt(x, y) == TileKinds.Floor && Look(s.Decor, y * w + x) == null && !used.Contains(y * w + x) && !Dirs4.Any((d) => Ways.Contains(TileAt(x + d.X, y + d.Y) ?? TileKinds.Floor));
                void Put(string id, int x, int y, double yaw, double du = 0, double dv = 0, double lean = 0, bool cast = true)
                {
                    var m = Compose(x + 0.5 + du, ElevAt(s, y * w + x) * Storey, y + 0.5 + dv, Quat.Euler(lean, yaw, 0), 1);
                    @out.Add(new Furnishing { id = id, matrix = m, x = x, y = y, z = 0, cast = cast });
                    used.Add(y * w + x);
                }
                for (int y = b.y0 + 1; y < b.y1; y++)
                {
                    for (int x = b.x0 + 1; x < b.x1; x++)
                    {
                        double R(int salt) => Hash(x, y, salt);
                        if (Clear(x, y) && R(5) < 0.1) Put("bucket", x, y, R(6) * 6.3, du: (R(7) - 0.5) * 0.5, dv: (R(8) - 0.5) * 0.5);
                    }
                }
            }
            return @out;
        }

        /// <summary>homeRoomAt: the room of a house a tile is in, if any (a home's shelves are its bookcases); null none.</summary>
        public static HomeRoom HomeRoomAt(GameState s, int x, int y, int z)
        {
            if (s.Homes == null) return null;
            foreach (var home in s.Homes)
            {
                if (x < home.X || y < home.Y || x >= home.X + home.W || y >= home.Y + home.H) continue;
                foreach (var room in home.Rooms) if (room.Z == z && x >= room.X && y >= room.Y && x < room.X + room.W && y < room.Y + room.H) return room;
            }
            return null;
        }
    }
}
