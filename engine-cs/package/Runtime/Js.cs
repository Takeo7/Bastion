#nullable enable
// JavaScript's numbers and collections where C#'s are not the same (docs/UNITY.md §3): the engine has to give the same
// results as the TypeScript one, bit for bit. Every port goes through these, never round them by hand.
using System;
using System.Collections;
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>JavaScript's Math where .NET's gives another value.</summary>
    public static class Js
    {
        /// <summary>
        /// Math.round: to the nearest whole number, halves up (towards +∞: -2.5 gives -2), not to the even one as
        /// System.Math.Round. Not `Math.Floor(x + 0.5)` either, which rounds 0.49999999999999994 up.
        /// </summary>
        public static double Round(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            var r = Math.Floor(x);
            return x - r >= 0.5 ? r + 1 : r;
        }

        /// <summary>
        /// Math.hypot(x, y) as V8 works it out (builtins/math.tq MathHypot): each over the largest, squared and summed with
        /// Kahan's compensation, then the square root times the largest. `Math.Sqrt(x * x + y * y)` differs in the last bit
        /// now and then.
        /// </summary>
        public static double Hypot(double x, double y)
        {
            var max = 0.0;
            var nan = false;
            if (double.IsNaN(x)) nan = true;
            else if (Math.Abs(x) > max) max = Math.Abs(x);
            if (double.IsNaN(y)) nan = true;
            else if (Math.Abs(y) > max) max = Math.Abs(y);
            if (double.IsPositiveInfinity(max)) return double.PositiveInfinity;
            if (nan) return double.NaN;
            if (max == 0) return 0;
            var sum = 0.0;
            var compensation = 0.0;
            var n = Math.Abs(x) / max;
            var summand = n * n - compensation;
            var preliminary = sum + summand;
            compensation = (preliminary - sum) - summand;
            sum = preliminary;
            n = Math.Abs(y) / max;
            summand = n * n - compensation;
            preliminary = sum + summand;
            sum = preliminary;
            return Math.Sqrt(sum) * max;
        }

        /// <summary>Math.sign: -1, 0 or 1 (NaN for NaN; -0 is 0 here, as it compares).</summary>
        public static double Sign(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            return x > 0 ? 1 : x < 0 ? -1 : 0;
        }

        /// <summary>Math.sign of a whole number.</summary>
        public static int Sign(int x)
        {
            return x > 0 ? 1 : x < 0 ? -1 : 0;
        }

        // ------------------------------------------------------------ atan2
        // V8's Math.atan and Math.atan2 are fdlibm's (src/base/ieee754.cc), not the C library's: these are them, step for
        // step, so the angles come out with the same bits on every platform. With one twist: V8 is built with clang, which
        // on arm64 (the Macs the golden files are recorded on) fuses each `a + b * c` of a single expression into one
        // multiply-add, rounded once. So where s_atan.c has one, here it is Math.FusedMultiplyAdd, and the angles are those
        // of node on Apple silicon to the bit (engine-cs/golden/step1/math.json). On x64, V8 doesn't fuse them: node on
        // Windows gives the plain fdlibm's, which differ from these in the last bit now and then (atan2(13, 34)).

        private static readonly double[] AtanHi =
        {
            4.63647609000806093515e-01, // atan(0.5)hi 0x3FDDAC67, 0x0561BB4F
            7.85398163397448278999e-01, // atan(1.0)hi 0x3FE921FB, 0x54442D18
            9.82793723247329054082e-01, // atan(1.5)hi 0x3FEF730B, 0xD281F69B
            1.57079632679489655800e+00, // atan(inf)hi 0x3FF921FB, 0x54442D18
        };

        private static readonly double[] AtanLo =
        {
            2.26987774529616870924e-17, // atan(0.5)lo 0x3C7A2B7F, 0x222F65E2
            3.06161699786838301793e-17, // atan(1.0)lo 0x3C81A626, 0x33145C07
            1.39033110312309984516e-17, // atan(1.5)lo 0x3C700788, 0x7AF0CBBD
            6.12323399573676603587e-17, // atan(inf)lo 0x3C91A626, 0x33145C07
        };

        private static readonly double[] AT =
        {
            3.33333333333329318027e-01, // 0x3FD55555, 0x5555550D
            -1.99999999998764832476e-01, // 0xBFC99999, 0x9998EBC4
            1.42857142725034663711e-01, // 0x3FC24924, 0x920083FF
            -1.11111104054623557880e-01, // 0xBFBC71C6, 0xFE231671
            9.09088713343650656196e-02, // 0x3FB745CD, 0xC54C206E
            -7.69187620504482999495e-02, // 0xBFB3B0F2, 0xAF749A6D
            6.66107313738753120669e-02, // 0x3FB10D66, 0xA0D03D51
            -5.83357013379057348645e-02, // 0xBFADDE2D, 0x52DEFD9A
            4.97687799461593236017e-02, // 0x3FA97B4B, 0x24760DEB
            -3.65315727442169155270e-02, // 0xBFA2B444, 0x2C6A6C2F
            1.62858201153657823623e-02, // 0x3F90AD3A, 0xE322DA11
        };

        private static double Fma(double a, double b, double c)
        {
            return FusedMultiplyAdd(a, b, c);
        }

        /// <summary>
        /// a * b + c rounded once (the fused multiply-add clang makes on arm64). Math.FusedMultiplyAdd is not in .NET
        /// Standard 2.1: this is Boldo and Melquiond's emulation (2008), the product and a sum exact (Dekker, Knuth) and the
        /// sum of their errors rounded to odd. Right for operands whose product neither overflows nor comes near the
        /// subnormals: atan's never do.
        /// </summary>
        public static double FusedMultiplyAdd(double a, double b, double c)
        {
            TwoProduct(a, b, out var uh, out var ul);
            TwoSum(c, uh, out var th, out var tl);
            return th + SumToOdd(tl, ul);
        }

        /// <summary>s + e is exactly a + b (Knuth).</summary>
        private static void TwoSum(double a, double b, out double s, out double e)
        {
            s = a + b;
            var bb = s - a;
            e = (a - (s - bb)) + (b - bb);
        }

        /// <summary>hi + lo is exactly a, each in 26 bits (Veltkamp).</summary>
        private static void Split(double a, out double hi, out double lo)
        {
            var t = 134217729.0 * a; // 2^27 + 1
            hi = t - (t - a);
            lo = a - hi;
        }

        /// <summary>p + e is exactly a * b (Dekker).</summary>
        private static void TwoProduct(double a, double b, out double p, out double e)
        {
            p = a * b;
            Split(a, out var ah, out var al);
            Split(b, out var bh, out var bl);
            e = al * bl - (((p - ah * bh) - al * bh) - ah * bl);
        }

        /// <summary>a + b rounded to odd: exact, or else whichever of the two doubles round it has its last bit set.</summary>
        private static double SumToOdd(double a, double b)
        {
            TwoSum(a, b, out var s, out var e);
            var bits = BitConverter.DoubleToInt64Bits(s);
            if (e == 0 || (bits & 1) != 0) return s;
            // Even and inexact: its neighbour on the side of the error is odd.
            return BitConverter.Int64BitsToDouble((e > 0) == (s > 0) ? bits + 1 : bits - 1);
        }

        private static int HighWord(double x)
        {
            return (int)(BitConverter.DoubleToInt64Bits(x) >> 32);
        }

        private static uint LowWord(double x)
        {
            return (uint)BitConverter.DoubleToInt64Bits(x);
        }

        /// <summary>Math.atan, fdlibm's s_atan.c.</summary>
        public static double Atan(double x)
        {
            const double one = 1.0;
            const double huge = 1.0e300;
            int hx = HighWord(x);
            int ix = hx & 0x7FFFFFFF;
            int id;
            if (ix >= 0x44100000)
            {
                // |x| >= 2^66
                var low = LowWord(x);
                if (ix > 0x7FF00000 || (ix == 0x7FF00000 && low != 0)) return x + x; // NaN
                if (hx > 0) return AtanHi[3] + AtanLo[3];
                return -AtanHi[3] - AtanLo[3];
            }
            if (ix < 0x3FDC0000)
            {
                // |x| < 0.4375
                if (ix < 0x3E400000)
                {
                    // |x| < 2^-27
                    if (huge + x > one) return x; // raise inexact
                }
                id = -1;
            }
            else
            {
                x = Math.Abs(x);
                if (ix < 0x3FF30000)
                {
                    // |x| < 1.1875
                    if (ix < 0x3FE60000)
                    {
                        // 7/16 <= |x| < 11/16
                        id = 0;
                        x = Fma(2.0, x, -one) / (2.0 + x);
                    }
                    else
                    {
                        // 11/16 <= |x| < 19/16
                        id = 1;
                        x = (x - one) / (x + one);
                    }
                }
                else
                {
                    if (ix < 0x40038000)
                    {
                        // |x| < 2.4375
                        id = 2;
                        x = (x - 1.5) / Fma(1.5, x, one);
                    }
                    else
                    {
                        // 2.4375 <= |x| < 2^66
                        id = 3;
                        x = -1.0 / x;
                    }
                }
            }
            // end of argument reduction
            var z = x * x;
            var w = z * z;
            // break sum from i=0 to 10 aT[i]z**(i+1) into odd and even poly
            // s1 = z * (aT[0] + w * (aT[2] + w * (aT[4] + w * (aT[6] + w * (aT[8] + w * aT[10])))))
            var s1 = z * Fma(w, Fma(w, Fma(w, Fma(w, Fma(w, AT[10], AT[8]), AT[6]), AT[4]), AT[2]), AT[0]);
            // s2 = w * (aT[1] + w * (aT[3] + w * (aT[5] + w * (aT[7] + w * aT[9]))))
            var s2 = w * Fma(w, Fma(w, Fma(w, Fma(w, AT[9], AT[7]), AT[5]), AT[3]), AT[1]);
            // x - x * (s1 + s2)
            if (id < 0) return Fma(-x, s1 + s2, x);
            // atanhi[id] - ((x * (s1 + s2) - atanlo[id]) - x)
            z = AtanHi[id] - (Fma(x, s1 + s2, -AtanLo[id]) - x);
            return hx < 0 ? -z : z;
        }

        /// <summary>Math.atan2(y, x), fdlibm's e_atan2.c.</summary>
        public static double Atan2(double y, double x)
        {
            const double tiny = 1.0e-300;
            const double zero = 0.0;
            const double piO4 = 7.8539816339744827900E-01; // 0x3FE921FB, 0x54442D18
            const double piO2 = 1.5707963267948965580E+00; // 0x3FF921FB, 0x54442D18
            const double pi = 3.1415926535897931160E+00; // 0x400921FB, 0x54442D18
            const double piLo = 1.2246467991473531772E-16; // 0x3CA1A626, 0x33145C07
            int hx = HighWord(x);
            uint lx = LowWord(x);
            int ix = hx & 0x7FFFFFFF;
            int hy = HighWord(y);
            uint ly = LowWord(y);
            int iy = hy & 0x7FFFFFFF;
            if (double.IsNaN(x) || double.IsNaN(y)) return x + y; // x or y is NaN
            if (((hx - 0x3FF00000) | (int)lx) == 0) return Atan(y); // x=1.0
            int m = ((hy >> 31) & 1) | ((hx >> 30) & 2); // 2*sign(x)+sign(y)
            double z;

            // when y = 0
            if ((iy | (int)ly) == 0)
            {
                switch (m)
                {
                    case 0:
                    case 1:
                        return y; // atan(+-0,+anything)=+-0
                    case 2:
                        return pi + tiny; // atan(+0,-anything) = pi
                    case 3:
                        return -pi - tiny; // atan(-0,-anything) =-pi
                }
            }
            // when x = 0
            if ((ix | (int)lx) == 0) return hy < 0 ? -piO2 - tiny : piO2 + tiny;

            // when x is INF
            if (ix == 0x7FF00000)
            {
                if (iy == 0x7FF00000)
                {
                    switch (m)
                    {
                        case 0:
                            return piO4 + tiny; // atan(+INF,+INF)
                        case 1:
                            return -piO4 - tiny; // atan(-INF,+INF)
                        case 2:
                            return Fma(3.0, piO4, tiny); // atan(+INF,-INF)
                        default:
                            return Fma(-3.0, piO4, -tiny); // atan(-INF,-INF)
                    }
                }
                switch (m)
                {
                    case 0:
                        return zero; // atan(+...,+INF)
                    case 1:
                        return -zero; // atan(-...,+INF)
                    case 2:
                        return pi + tiny; // atan(+...,-INF)
                    default:
                        return -pi - tiny; // atan(-...,-INF)
                }
            }
            // when y is INF
            if (iy == 0x7FF00000) return hy < 0 ? -piO2 - tiny : piO2 + tiny;

            // compute y/x
            int k = (iy - ix) >> 20;
            if (k > 60)
            {
                // |y/x| > 2**60
                z = Fma(0.5, piLo, piO2);
                m &= 1;
            }
            else if (hx < 0 && k < -60)
            {
                z = 0.0; // 0 > |y|/x > -2**-60
            }
            else
            {
                z = Atan(Math.Abs(y / x)); // safe to do y/x
            }
            switch (m)
            {
                case 0:
                    return z; // atan(+,+)
                case 1:
                    return -z; // atan(-,+)
                case 2:
                    return pi - (z - piLo); // atan(+,-)
                default:
                    return (z - piLo) - pi; // atan(-,-)
            }
        }

        // ------------------------------------------------------------ strings

        /// <summary>Whether `c` is white space to JavaScript (String.prototype.trim, `\s` in a RegExp): not quite char.IsWhiteSpace.</summary>
        public static bool IsSpace(char c)
        {
            switch (c)
            {
                case '\t':
                case '\n':
                case '\v':
                case '\f':
                case '\r':
                case ' ':
                case '\u00a0':
                case '\u1680':
                case '\u2028':
                case '\u2029':
                case '\u202f':
                case '\u205f':
                case '\u3000':
                case '\ufeff':
                    return true;
                default:
                    return c >= (char)0x2000 && c <= (char)0x200a;
            }
        }

        /// <summary>String.prototype.trim.</summary>
        public static string Trim(string s)
        {
            var start = 0;
            var end = s.Length;
            while (start < end && IsSpace(s[start])) start++;
            while (end > start && IsSpace(s[end - 1])) end--;
            return s.Substring(start, end - start);
        }

        // ------------------------------------------------------------ arrays

        /// <summary>`list[i]` where TypeScript reads past the end as undefined (and then `?? fallback`).</summary>
        public static T At<T>(IReadOnlyList<T>? list, int i, T fallback)
        {
            return list != null && i >= 0 && i < list.Count ? list[i] : fallback;
        }

        /// <summary>
        /// Array.prototype.sort with a comparator: stable, as JavaScript's is (List.Sort is not). In place, like it.
        /// </summary>
        public static void Sort<T>(List<T> list, Comparison<T> compare)
        {
            var keyed = new List<KeyValuePair<int, T>>(list.Count);
            for (var i = 0; i < list.Count; i++) keyed.Add(new KeyValuePair<int, T>(i, list[i]));
            keyed.Sort((a, b) =>
            {
                var c = compare(a.Value, b.Value);
                return c != 0 ? c : a.Key.CompareTo(b.Key);
            });
            for (var i = 0; i < keyed.Count; i++) list[i] = keyed[i].Value;
        }

        /// <summary>
        /// The same with a comparator that gives any number, as JavaScript's do (`dist(a, p) - dist(b, p)`): only its sign
        /// counts, and NaN is 0, as V8 has it (Math.Sign would throw on NaN).
        /// </summary>
        public static void Sort<T>(List<T> list, Func<T, T, double> compare)
        {
            Sort(list, (a, b) =>
            {
                var v = compare(a, b);
                return v > 0 ? 1 : v < 0 ? -1 : 0;
            });
        }

        /// <summary>
        /// Array.prototype.sort as V8 runs it (third_party/v8/builtins/array-sort.tq, ArrayTimSortImpl), comparison for
        /// comparison: for a comparator that is no order, one that draws from the Rng (townPlan.ts). Any stable sort gives
        /// a true order's result; this one also asks the same questions, in the same order, as many times. Below 64 items,
        /// TimSort's least run is the whole list: CountAndMakeRun, then BinaryInsertionSort over the rest. Longer lists merge
        /// runs, which is not here.
        /// </summary>
        public static void SortV8<T>(List<T> list, Func<T, T, double> compare)
        {
            var n = list.Count;
            if (n < 2) return;
            if (n >= 64) throw new NotSupportedException("Js.SortV8: only below 64 items (one run).");
            // SortCompareUserFn: NaN is 0.
            double Compare(T x, T y)
            {
                var v = compare(x, y);
                return double.IsNaN(v) ? 0 : v;
            }
            // CountAndMakeRun(0, n): the run at the start, ascending or strictly descending (then reversed).
            var runLength = 2;
            var descending = Compare(list[1], list[0]) < 0;
            var previous = list[1];
            for (var idx = 2; idx < n; idx++)
            {
                var current = list[idx];
                var order = Compare(current, previous);
                if (descending ? order >= 0 : order < 0) break;
                previous = current;
                runLength++;
            }
            if (descending) list.Reverse(0, runLength);
            // BinaryInsertionSort(0, runLength, n).
            for (var start = runLength; start < n; start++)
            {
                var left = 0;
                var right = start;
                var pivot = list[right];
                while (left < right)
                {
                    var mid = left + ((right - left) >> 1);
                    if (Compare(pivot, list[mid]) < 0) right = mid;
                    else left = mid + 1;
                }
                for (var p = start; p > left; p--) list[p] = list[p - 1];
                list[left] = pivot;
            }
        }
    }

    /// <summary>
    /// A JavaScript Map: walked in the order its keys went in (a key set again keeps its place; one deleted and set again
    /// goes last). Dictionary's order is not promised, and the engine's results depend on it (path.ts destinations).
    /// </summary>
    /// <remarks>
    /// Also a JavaScript object whose keys are deleted and set again, as the campaign's state has (`questLog`): it is an
    /// IDictionary, so the JSON writes it as an object, in this order, and reads it back in the order it comes.
    /// </remarks>
    public sealed class JsMap<TKey, TValue> : IDictionary<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, int> index = new Dictionary<TKey, int>();
        private readonly List<TKey> keys = new List<TKey>();
        private readonly List<TValue> values = new List<TValue>();

        public JsMap()
        {
        }

        public JsMap(IEnumerable<KeyValuePair<TKey, TValue>> entries)
        {
            foreach (var e in entries) Set(e.Key, e.Value);
        }

        /// <summary>Map.size.</summary>
        public int Count => keys.Count;

        /// <summary>Map.has.</summary>
        public bool Has(TKey key)
        {
            return index.ContainsKey(key);
        }

        /// <summary>Map.get, as a TryGetValue: false where it would be undefined.</summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (index.TryGetValue(key, out var i))
            {
                value = values[i];
                return true;
            }
            value = default!;
            return false;
        }

        /// <summary>Map.get with `?? fallback`.</summary>
        public TValue Get(TKey key, TValue fallback)
        {
            return index.TryGetValue(key, out var i) ? values[i] : fallback;
        }

        /// <summary>Map.set.</summary>
        public void Set(TKey key, TValue value)
        {
            if (index.TryGetValue(key, out var i))
            {
                values[i] = value;
                return;
            }
            index[key] = keys.Count;
            keys.Add(key);
            values.Add(value);
        }

        /// <summary>Map.delete.</summary>
        public bool Delete(TKey key)
        {
            if (!index.TryGetValue(key, out var i)) return false;
            index.Remove(key);
            keys.RemoveAt(i);
            values.RemoveAt(i);
            for (var k = i; k < keys.Count; k++) index[keys[k]] = k;
            return true;
        }

        /// <summary>Map.keys, in order.</summary>
        public IReadOnlyList<TKey> Keys => keys;

        /// <summary>Map.values, in order.</summary>
        public IReadOnlyList<TValue> Values => values;

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (var i = 0; i < keys.Count; i++) yield return new KeyValuePair<TKey, TValue>(keys[i], values[i]);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // As an IDictionary (a JavaScript object's `o[key]`, `delete o[key]`), the same Map underneath.

        /// <summary>`o[key]`; a key it doesn't have throws, as a Dictionary's; set, Map.set.</summary>
        public TValue this[TKey key]
        {
            get => index.TryGetValue(key, out var i) ? values[i] : throw new KeyNotFoundException();
            set => Set(key, value);
        }

        /// <summary>Map.set of a key it doesn't have; one it has throws, as a Dictionary's.</summary>
        public void Add(TKey key, TValue value)
        {
            if (index.ContainsKey(key)) throw new ArgumentException("JsMap: the key is there already.");
            Set(key, value);
        }

        public bool ContainsKey(TKey key) => Has(key);

        /// <summary>Map.delete.</summary>
        public bool Remove(TKey key) => Delete(key);

        /// <summary>Map.clear.</summary>
        public void Clear()
        {
            index.Clear();
            keys.Clear();
            values.Clear();
        }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => keys.AsReadOnly();

        ICollection<TValue> IDictionary<TKey, TValue>.Values => values.AsReadOnly();

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        {
            return TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach (var e in this) array[arrayIndex++] = e;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)this).Contains(item) && Delete(item.Key);
        }
    }

    /// <summary>
    /// A JavaScript Set: walked in the order its items went in (one added again keeps its place; one deleted and added
    /// again goes last), and, as JavaScript walks one, what is added while walking it is walked too and what is deleted
    /// before its turn is not. HashSet's order is not promised, and the generator's results depend on it (mapgen.ts flood).
    /// </summary>
    public sealed class JsSet<T> : IEnumerable<T> where T : notnull
    {
        private readonly Dictionary<T, int> index = new Dictionary<T, int>();
        private readonly List<T> items = new List<T>();
        private readonly List<bool> live = new List<bool>();
        private int count;

        public JsSet()
        {
        }

        public JsSet(IEnumerable<T> entries)
        {
            foreach (var e in entries) Add(e);
        }

        /// <summary>Set.size.</summary>
        public int Count => count;

        /// <summary>Set.has.</summary>
        public bool Has(T item)
        {
            return index.ContainsKey(item);
        }

        /// <summary>Set.add.</summary>
        public void Add(T item)
        {
            if (index.ContainsKey(item)) return;
            index[item] = items.Count;
            items.Add(item);
            live.Add(true);
            count++;
        }

        /// <summary>Set.delete.</summary>
        public bool Delete(T item)
        {
            if (!index.TryGetValue(item, out var i)) return false;
            index.Remove(item);
            live[i] = false;
            items[i] = default!;
            count--;
            return true;
        }

        /// <summary>Set.clear.</summary>
        public void Clear()
        {
            index.Clear();
            for (var i = 0; i < live.Count; i++)
            {
                live[i] = false;
                items[i] = default!;
            }
            count = 0;
        }

        public IEnumerator<T> GetEnumerator()
        {
            // Not foreach: what is added on the way is walked too, as in JavaScript.
            for (var i = 0; i < items.Count; i++) if (live[i]) yield return items[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
