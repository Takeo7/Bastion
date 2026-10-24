using System;
using System.Collections.Generic;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Map
{
    // 0.10.1's mapView.ts, what draws the base's pinar (docs/FASE12.md §11.1): its own pines, high cover with a tree's
    // decor, off the grid and of all sizes, a dead one now and then ('basePines', dressed); where a pine was felled, its
    // stump till something is built over it; and past the map's edge the wood going on as thick as along it, out to where
    // the haze has it all (baseWood, playtest 21/10 #27 and 22/10 #30, Ana: no square cut), its tracks' ways left open and
    // the mouths of the ways through the pines (the engine's siegeTrails, Ana 23/10 #33). Out to BaseWood.Near its pines
    // are the map's; further 0.10.1 instanced them, plain and in the haze, and past BaseWood.Far all of them the simplest
    // pine at the others' heights (farWood).
    // What 0.10.1 handed three (addSyntyId, addSynty, the far wood's instances) is a list of placements here: the prop, the
    // matrix three's Matrix4 was, in 0.10.1's world (tiles; x east, y up, z south), and whether it casts a shadow. The math
    // is three's in doubles (Matrix4.compose, Quaternion.setFromAxisAngle, multiplyMatrices), each element made a float only
    // as it goes into the matrix; sines and cosines V8's (Trig), atan2 and hypot Js.cs's. No Mathf: it runs in a plain
    // dotnet harness.

    /// <summary>
    /// A pine, a stump or a pine of the wood past the edge: which prop (PropId), the matrix that places it in 0.10.1's world
    /// (tiles: x east, y up, z south; the same mathematical matrix three's Matrix4 was), whether it casts a shadow, and where.
    /// </summary>
    public sealed class WoodPlacement
    {
        public string id;
        public Matrix4x4 matrix;
        public bool shadows;
        /// <summary>The map's tile it stands on (y * width + x): a pine's, a stump's; -1 for the wood past the edge.</summary>
        public int tile = -1;
        /// <summary>The storey a pine stands on (0 the ground, k + 1 for GameState.levels[k]).</summary>
        public int z;
        /// <summary>The wood past the edge (baseWood's WoodTree): its spot, in tiles off the map (x east, y south; the tile's corner).</summary>
        public int x, y;
        /// <summary>How far out of the map it stands (WoodTree.out: tiles, round at the corners); 0 on the map.</summary>
        public double beyond;
        /// <summary>Past BaseWoods.Near: 0.10.1 drew it instanced (farWood), plain (Lambert) and without ambient occlusion.</summary>
        public bool far;
        /// <summary>A far one's side of the map (farWood's lots: a mesh by kind of pine and side, each drawn only in view); -1 near.</summary>
        public int side = -1;
    }

    /// <summary>The base's pinar as 0.10.1 drew it (mapView.ts: dressed's 'basePines', the felled pines' stumps, baseWood and farWood).</summary>
    public static class BaseWoods
    {
        /// <summary>A storey's height in tiles, as a double (coords.ts STOREY).</summary>
        const double Storey = 2.3;
        /// <summary>
        /// How far past its edge the base's pinar goes on (BASE_WOOD: tiles out, round at the corners): to where the haze
        /// has it all (clearingArt.ts fadeBeyond), so the eye never reaches its end (playtest 22/10 #30).
        /// </summary>
        public const int Reach = 31;
        /// <summary>Out to here (BASE_WOOD_NEAR) its pines are the map's (their crowns fade over what is looked at); further, instanced and plain.</summary>
        public const double Near = 5;
        /// <summary>
        /// Past here (BASE_WOOD_FAR), where the haze begins, its pines are all the simplest one at the others' heights, and by
        /// its far end a third of them are left out (Thin): under the haze it reads the same, at about half the cost.
        /// </summary>
        public const double Far = 14;
        /// <summary>BASE_WOOD_THIN: from this far out, up to this share left out at Reach (smoothstep).</summary>
        const double ThinFrom = 22;
        const double ThinMost = 0.35;
        /// <summary>
        /// How far out (BASE_WOOD_MOUTH, tiles) the wood leaves open the mouth of a way through the pines: before any open
        /// tile of the band along the edge, the siege's trails' among them. The tracks go on open as far as it goes.
        /// </summary>
        const double Mouth = 2.5;
        /// <summary>The far wood's one pine (FAR_PINE).</summary>
        public const string FarPine = "pineLarge2";

        /// <summary>
        /// The base's pines (syntyManifest.ts PROP_SETS.basePines; MapArt.json's sets.basePines): of all sizes, a dead one
        /// one time in twenty. Picked by hash(x, y, 1).
        /// </summary>
        public static readonly IReadOnlyList<string> BasePines = new[]
        {
            "pineLarge", "treePine", "pineLarge2", "pineLarge", "treePine", "pineLarge2", "pineLarge", "treePine", "pineLarge2", "pineLarge",
            "treePine", "pineLarge2", "pineLarge", "treePine", "pineLarge2", "pineLarge", "treePine", "pineLarge2", "pineLarge", "pineDead",
        };

        /// <summary>
        /// mapView.ts hash: a stable pseudo-random value in [0, 1) per tile, for variety without state (JavaScript's 32-bit
        /// integers: each operand of ^ made an int32, the result >>> 0). A double, as 0.10.1's (MapArt.Hash is a float).
        /// </summary>
        static double Hash(int x, int y, int salt)
        {
            unchecked
            {
                int a = (int)((x + salt * 31L) * 73856093L);
                int b = (int)(y * 19349663L);
                int c = (int)(salt * 83492791L);
                return (uint)(a ^ b ^ c) % 1000 / 1000.0;
            }
        }

        /// <summary>THREE.MathUtils.clamp.</summary>
        static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));

        /// <summary>THREE.MathUtils.smoothstep.</summary>
        static double Smoothstep(double x, double min, double max)
        {
            if (x <= min) return 0;
            if (x >= max) return 1;
            x = (x - min) / (max - min);
            return x * x * (3 - 2 * x);
        }

        /// <summary>The prop of the set that `pick` (in [0, 1)) chooses, as addSynty picks it.</summary>
        static string Pick(IReadOnlyList<string> ids, double pick) => ids[(int)Math.Min(ids.Count - 1, Math.Floor(pick * ids.Count))];

        /// <summary>`decorOf(decor?.[i])`: null without decor or past its end.</summary>
        static DecorLook Look(IReadOnlyList<int> decor, int i) => decor != null && i >= 0 && i < decor.Count ? Bastion.Engine.Decor.DecorOf(decor[i]) : null;

        /// <summary>`s.elev[i] ?? 0`.</summary>
        static int ElevAt(GameState s, int i) => s.Elev != null && i >= 0 && i < s.Elev.Count ? s.Elev[i] : 0;

        // HomeFurnishing.Trig, copied whole (it is private there): V8's Math.sin and Math.cos are fdlibm's
        // (src/base/ieee754.cc: k_sin.c, k_cos.c, e_rem_pio2.c), not the C library's: .NET's Math.Sin and Math.Cos are the C
        // runtime's, the last bit off for one argument in twenty on the Mac. As Js.cs's atan: clang on arm64 fuses each
        // `a + b * c` of an expression into a multiply-add, here Js.FusedMultiplyAdd; with them, the same bits as node on
        // Apple silicon. The reduction of arguments past 2^19 π/2 (__kernel_rem_pio2) is not here: no angle of these comes
        // near it (they are under 2π).
        static class Trig
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

        /// <summary>A THREE.Matrix4's elements, doubles, column-major (te[0..3] the first column, te[12..14] the position).</summary>
        sealed class M
        {
            public readonly double[] te = new double[16];

            /// <summary>
            /// mapView.ts placed: `new THREE.Matrix4().compose((x, y, z), new THREE.Quaternion().setFromAxisAngle((0, 1, 0), yaw),
            /// (sx, sy, sz))`, a box of size (sx, sy, sz) standing at (x, y, z), turned `yaw` radians.
            /// </summary>
            public static M Placed(double px, double py, double pz, double sx, double sy, double sz, double yaw)
            {
                // setFromAxisAngle: the axis times sin(angle / 2), w cos(angle / 2).
                double half = yaw / 2, s = Trig.Sin(half);
                double x = 0 * s, y = 1 * s, z = 0 * s, w = Trig.Cos(half);
                // compose.
                double x2 = x + x, y2 = y + y, z2 = z + z;
                double xx = x * x2, xy = x * y2, xz = x * z2;
                double yy = y * y2, yz = y * z2, zz = z * z2;
                double wx = w * x2, wy = w * y2, wz = w * z2;
                var m = new M();
                var te = m.te;
                te[0] = (1 - (yy + zz)) * sx;
                te[1] = (xy + wz) * sx;
                te[2] = (xz - wy) * sx;
                te[3] = 0;
                te[4] = (xy - wz) * sy;
                te[5] = (1 - (xx + zz)) * sy;
                te[6] = (yz + wx) * sy;
                te[7] = 0;
                te[8] = (xz + wy) * sz;
                te[9] = (yz - wx) * sz;
                te[10] = (1 - (xx + yy)) * sz;
                te[11] = 0;
                te[12] = px;
                te[13] = py;
                te[14] = pz;
                te[15] = 1;
                return m;
            }

            /// <summary>`new THREE.Matrix4().makeScale(k, k, k)`.</summary>
            public static M Scale(double k)
            {
                var m = new M();
                m.te[0] = k;
                m.te[5] = k;
                m.te[10] = k;
                m.te[15] = 1;
                return m;
            }

            /// <summary>`this.clone().multiply(b)`: Matrix4.multiplyMatrices(this, b), term for term as three sums them.</summary>
            public M Times(M b)
            {
                double[] ae = te, be = b.te;
                double a11 = ae[0], a12 = ae[4], a13 = ae[8], a14 = ae[12];
                double a21 = ae[1], a22 = ae[5], a23 = ae[9], a24 = ae[13];
                double a31 = ae[2], a32 = ae[6], a33 = ae[10], a34 = ae[14];
                double a41 = ae[3], a42 = ae[7], a43 = ae[11], a44 = ae[15];
                double b11 = be[0], b12 = be[4], b13 = be[8], b14 = be[12];
                double b21 = be[1], b22 = be[5], b23 = be[9], b24 = be[13];
                double b31 = be[2], b32 = be[6], b33 = be[10], b34 = be[14];
                double b41 = be[3], b42 = be[7], b43 = be[11], b44 = be[15];
                var m = new M();
                var o = m.te;
                o[0] = a11 * b11 + a12 * b21 + a13 * b31 + a14 * b41;
                o[4] = a11 * b12 + a12 * b22 + a13 * b32 + a14 * b42;
                o[8] = a11 * b13 + a12 * b23 + a13 * b33 + a14 * b43;
                o[12] = a11 * b14 + a12 * b24 + a13 * b34 + a14 * b44;
                o[1] = a21 * b11 + a22 * b21 + a23 * b31 + a24 * b41;
                o[5] = a21 * b12 + a22 * b22 + a23 * b32 + a24 * b42;
                o[9] = a21 * b13 + a22 * b23 + a23 * b33 + a24 * b43;
                o[13] = a21 * b14 + a22 * b24 + a23 * b34 + a24 * b44;
                o[2] = a31 * b11 + a32 * b21 + a33 * b31 + a34 * b41;
                o[6] = a31 * b12 + a32 * b22 + a33 * b32 + a34 * b42;
                o[10] = a31 * b13 + a32 * b23 + a33 * b33 + a34 * b43;
                o[14] = a31 * b14 + a32 * b24 + a33 * b34 + a34 * b44;
                o[3] = a41 * b11 + a42 * b21 + a43 * b31 + a44 * b41;
                o[7] = a41 * b12 + a42 * b22 + a43 * b32 + a44 * b42;
                o[11] = a41 * b13 + a42 * b23 + a43 * b33 + a44 * b43;
                o[15] = a41 * b14 + a42 * b24 + a43 * b34 + a44 * b44;
                return m;
            }

            /// <summary>As Unity's: the same row and column (m00 = te[0], m10 = te[1], … m03 = te[12]), each a float.</summary>
            public Matrix4x4 ToUnity()
            {
                var m = new Matrix4x4();
                m.m00 = (float)te[0];
                m.m10 = (float)te[1];
                m.m20 = (float)te[2];
                m.m30 = (float)te[3];
                m.m01 = (float)te[4];
                m.m11 = (float)te[5];
                m.m21 = (float)te[6];
                m.m31 = (float)te[7];
                m.m02 = (float)te[8];
                m.m12 = (float)te[9];
                m.m22 = (float)te[10];
                m.m32 = (float)te[11];
                m.m03 = (float)te[12];
                m.m13 = (float)te[13];
                m.m23 = (float)te[14];
                m.m33 = (float)te[15];
                return m;
            }
        }

        /// <summary>
        /// The base's own pines (mapView.ts dressed, 'basePines'): on each tile of high cover whose decor is a tree, on the
        /// ground and on each storey, a pine of BasePines (hash(x, y, 1)) off the grid, of its own size and turn. None but on
        /// the base's map (GameState.base). `pieceAt`: a piece of the base stands on tile i (basePieces.ts pieceMap: at[i] >= 0;
        /// all of them are drawn, DRAWN); `yard`: tile i is the Bastion's yard (pieceMap's yard): on the ground those tiles are
        /// the pieces' own, nothing else drawn there.
        /// </summary>
        public static List<WoodPlacement> Pines(GameState s, Func<int, bool> pieceAt, Func<int, bool> yard)
        {
            var @out = new List<WoodPlacement>();
            if (s.Base == null) return @out;
            int w = s.Width, h = s.Height;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (s.Tiles[i] != TileKinds.High || pieceAt(i) || yard(i)) continue;
                    Pine(s, s.Decor, i, x, y, 0, ElevAt(s, i) * Storey, @out);
                }
            }
            // Each storey's cover (GameState.levels), as on the ground.
            if (s.Levels != null)
            {
                for (int k = 0; k < s.Levels.Count; k++)
                {
                    var storey = s.Levels[k];
                    int level = k + 1;
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            int i = y * w + x;
                            if (i < storey.Tiles.Count && storey.Tiles[i] == TileKinds.High) Pine(s, storey.Decor, i, x, y, level, level * Storey, @out);
                        }
                    }
                }
            }
            return @out;
        }

        /// <summary>The pine on high cover tile i, if its decor is a tree (dressed: an object over several tiles is drawn as that).</summary>
        static void Pine(GameState s, List<int> decor, int i, int x, int y, int z, double @base, List<WoodPlacement> @out)
        {
            var look = Look(decor, i);
            if (look == null || look.Kind != DecorKinds.Tree) return;
            // thingOf: an anchor and its parts (docs/MAPAS.md, phase 1) is that object, or its rubble.
            var o = Bastion.Engine.Decor.ObjectAt(decor, s.Width, i);
            if (o != null && o.Tiles.Count >= 2 && Look(decor, o.Tiles[1])?.Kind == DecorKinds.Part) return;
            double cx = x + 0.5, cz = y + 0.5;
            double k = 0.8 + Hash(x, y, 14) * 0.45;
            var m = M.Placed(cx + (Hash(x, y, 15) - 0.5) * 0.45, @base, cz + (Hash(x, y, 16) - 0.5) * 0.45, k, k * (0.9 + Hash(x, y, 17) * 0.25), k, Hash(x, y, 18) * Math.PI * 2);
            @out.Add(new WoodPlacement { id = Pick(BasePines, Hash(x, y, 1)), matrix = m.ToUnity(), shadows = true, tile = i, z = z });
        }

        /// <summary>
        /// Where a pine was felled (GameState.base.felled, in the order felled), its stump: flat ('bStump', no cover) till
        /// something is built over it. Only on floor, and not under a piece (`pieceAt`: basePieces.ts pieceMap's at[i] >= 0).
        /// </summary>
        public static List<WoodPlacement> Stumps(GameState s, Func<int, bool> pieceAt)
        {
            var @out = new List<WoodPlacement>();
            if (s.Base?.Felled == null) return @out;
            int w = s.Width;
            foreach (int i in s.Base.Felled)
            {
                if (i < 0 || i >= s.Tiles.Count || s.Tiles[i] != TileKinds.Floor || pieceAt(i)) continue;
                int x = i % w;
                int y = (i - x) / w;
                double k = 0.6 + Hash(x, y, 21) * 0.35;
                var m = M.Placed(x + 0.5 + (Hash(x, y, 22) - 0.5) * 0.4, 0, y + 0.5 + (Hash(x, y, 23) - 0.5) * 0.4, k, k, k, Hash(x, y, 24) * Math.PI * 2);
                @out.Add(new WoodPlacement { id = "bStump", matrix = m.ToUnity(), shadows = true, tile = i });
            }
            return @out;
        }

        /// <summary>
        /// The base's pinar past its edge (baseWood, then farWood), out to Reach (round at the corners): as thick as along the
        /// edge, in the strip nobody fells (Base.BaseEdge), its stands and glades from a smooth noise of the engine's grain
        /// (Base.FreshBase), so the edge and the wood beyond read as one wood; the tracks' ways left open, and the mouths of
        /// the ways through the pines (Mouth). In 0.10.1's order (y, then x, from the far corner north-west), no shadows. Out
        /// to Near, as the map's pines (addSyntyId); past it `far` (instanced), and past Far every one FarPine, as tall as
        /// the one it stands for, in proportion. None but on the base's map.
        /// `street`: tile i is track (the map's GroundMap kind Street; ground.ts made from basePieces.ts terrainOf, which
        /// reads the base's tracks, ground 3, as 1: from the state as it is they would be Yard, and no way left open).
        /// `tall`: how tall a pine stands as placed (its parts' boxes' top: 0.10.1's topOf), for the pines of BasePines past Far.
        /// </summary>
        public static List<WoodPlacement> Wood(GameState s, Func<int, bool> street, Func<string, double> tall)
        {
            var @out = new List<WoodPlacement>();
            if (s.Base == null) return @out;
            int w = s.Width, h = s.Height;
            var decor = s.Decor;
            // The share of the band along the edge (but its tracks) that stands in pines.
            int trees = 0, tiles = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (Math.Min(Math.Min(x, y), Math.Min(w - 1 - x, h - 1 - y)) >= Base.BaseEdge || street(i)) continue;
                    tiles++;
                    if (s.Tiles[i] == TileKinds.High && Look(decor, i)?.Kind == DecorKinds.Tree) trees++;
                }
            }
            double share = tiles != 0 ? (double)trees / tiles : 0.6;
            double Smooth(double t) => t * t * (3 - 2 * t);
            double Noise(int x, int y)
            {
                double fx = x / 6.0;
                double fy = y / 6.0;
                int gx = (int)Math.Floor(fx);
                int gy = (int)Math.Floor(fy);
                double tx = Smooth(fx - gx);
                double ty = Smooth(fy - gy);
                double top = Hash(gx, gy, 43) * (1 - tx) + Hash(gx + 1, gy, 43) * tx;
                double bottom = Hash(gx, gy + 1, 43) * (1 - tx) + Hash(gx + 1, gy + 1, 43) * tx;
                return top * (1 - ty) + bottom * ty;
            }
            bool Track(int x, int y) => street(Clamp(y, 0, h - 1) * w + Clamp(x, 0, w - 1));
            // An open tile of the band along the edge (a trail, a glade): floor with nothing on it.
            bool Open(int x, int y)
            {
                int i = Clamp(y, 0, h - 1) * w + Clamp(x, 0, w - 1);
                return s.Tiles[i] == TileKinds.Floor && (decor == null || i >= decor.Count || decor[i] == 0);
            }
            var spots = new List<(int x, int y, double beyond, double n)>();
            for (int y = -Reach; y < h + Reach; y++)
            {
                for (int x = -Reach; x < w + Reach; x++)
                {
                    double beyond = Js.Hypot(Math.Max(Math.Max(-x, x - (w - 1)), 0), Math.Max(Math.Max(-y, y - (h - 1)), 0));
                    if (beyond <= 0 || beyond > Reach) continue;
                    int cx = Clamp(x, 0, w - 1);
                    int cy = Clamp(y, 0, h - 1);
                    if (Track(cx, cy) || Track(cx + 1, cy) || Track(cx - 1, cy) || Track(cx, cy + 1) || Track(cx, cy - 1)) continue;
                    if (beyond <= Mouth && (Open(cx, cy) || Open(cx + 1, cy) || Open(cx - 1, cy) || Open(cx, cy + 1) || Open(cx, cy - 1))) continue;
                    spots.Add((x, y, beyond, Noise(x, y)));
                }
            }
            // Where the noise is cut for that share (a smooth noise isn't spread evenly between 0 and 1).
            var sorted = new double[spots.Count];
            for (int k = 0; k < spots.Count; k++) sorted[k] = spots[k].n;
            Array.Sort(sorted);
            double cut = sorted.Length > 0 ? sorted[(int)Math.Min(sorted.Length - 1, Math.Floor(share * sorted.Length))] : 0;
            // farWood: past Far, each pine of another kind is FarPine scaled to its height (the three are as slim).
            var lift = new Dictionary<string, M>();
            foreach (var p in spots)
            {
                if (!(p.n < cut && Hash(p.x, p.y, 44) >= ThinMost * Smoothstep(p.beyond, ThinFrom, Reach))) continue;
                int x = p.x, y = p.y;
                double k = 0.8 + Hash(x, y, 14) * 0.45;
                string id = Pick(BasePines, Hash(x, y, 1));
                var m = M.Placed(x + 0.5 + (Hash(x, y, 15) - 0.5) * 0.6, 0, y + 0.5 + (Hash(x, y, 16) - 0.5) * 0.6, k, k * (0.9 + Hash(x, y, 17) * 0.25), k, Hash(x, y, 18) * Math.PI * 2);
                var tree = new WoodPlacement { id = id, shadows = false, x = x, y = y, beyond = p.beyond, far = p.beyond > Near };
                if (tree.far)
                {
                    if (p.beyond > Far && id != FarPine)
                    {
                        if (!lift.TryGetValue(id, out var scale))
                        {
                            double r = tall(id) / tall(FarPine);
                            lift[id] = scale = M.Scale(r);
                        }
                        tree.id = FarPine;
                        m = m.Times(scale);
                    }
                    // Its lot: the side of the map it stands off (a quarter turn round the middle, from the west).
                    tree.side = (int)Math.Floor((Js.Atan2(m.te[14] - h / 2.0, m.te[12] - w / 2.0) / Math.PI + 1) * 2 + 0.5) % 4;
                }
                tree.matrix = m.ToUnity();
                @out.Add(tree);
            }
            return @out;
        }

        /// <summary>Wood with `street` read off the map's GroundMap (made from basePieces.ts terrainOf: see Wood).</summary>
        public static List<WoodPlacement> Wood(GameState s, GroundMap ground, Func<string, double> tall) => Wood(s, (i) => ground.Kinds[i] == GroundMap.Street, tall);
    }
}
