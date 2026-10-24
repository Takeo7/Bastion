using System;
using System.Collections.Generic;

namespace Bastion.Engine
{
    /// <summary>
    /// Seeded sfc32 PRNG. Only the authoritative host rolls dice; clients receive
    /// outcomes inside events, so they never need the RNG. The same numbers as
    /// rng.ts, seed for seed (unsigned 32-bit arithmetic where it used `| 0` and `>>> 0`).
    /// </summary>
    public sealed class Rng
    {
        private uint a;
        private uint b;
        private uint c;
        private uint d;

        public Rng(uint seed)
        {
            a = 0x9e3779b9;
            b = 0x243f6a88;
            c = 0xb7e15162;
            d = seed;
            for (int i = 0; i < 16; i++) Next();
        }

        /// <summary>A seed as rng.ts takes it (`seed >>> 0`): a negative one wraps round.</summary>
        public Rng(int seed) : this(unchecked((uint)seed)) { }

        /// <summary>From a saved state (Rng.State): rng.ts keeps it as signed 32-bit numbers, which wrap to the same bits.</summary>
        public Rng(IReadOnlyList<long> state)
        {
            a = unchecked((uint)state[0]);
            b = unchecked((uint)state[1]);
            c = unchecked((uint)state[2]);
            d = unchecked((uint)state[3]);
        }

        /// <summary>Float in [0, 1).</summary>
        public double Next()
        {
            unchecked
            {
                uint t = a + b;
                a = b ^ (b >> 9);
                b = c + (c << 3);
                c = (c << 21) | (c >> 11);
                d = d + 1;
                t = t + d;
                c = c + t;
                return t / 4294967296.0;
            }
        }

        /// <summary>Integer in [0, n).</summary>
        public int Int(int n) => (int)Math.Floor(Next() * n);

        /// <summary>Integer in [min, max].</summary>
        public int Range(int min, int max) => min + Int(max - min + 1);

        public T Pick<T>(IReadOnlyList<T> items) => items[Int(items.Count)];

        /// <summary>As rng.ts gives it: signed 32-bit numbers.</summary>
        public long[] State => new long[] { unchecked((int)a), unchecked((int)b), unchecked((int)c), unchecked((int)d) };
    }
}
