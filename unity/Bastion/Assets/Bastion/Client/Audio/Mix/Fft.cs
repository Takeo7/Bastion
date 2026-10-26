using System;

namespace Bastion.Client.Audio.Mix
{
    /// <summary>A complex FFT of one power-of-two size, in place on separate real and imaginary arrays (for Reverb).</summary>
    internal sealed class Fft
    {
        public readonly int Size;
        private readonly int[] reversed;
        private readonly float[] cos;
        private readonly float[] sin;

        public Fft(int size)
        {
            if (size < 2 || (size & (size - 1)) != 0) throw new ArgumentException("power of two", nameof(size));
            Size = size;
            reversed = new int[size];
            var bits = 0;
            while ((1 << bits) < size) bits++;
            for (var i = 0; i < size; i++)
            {
                var r = 0;
                for (var b = 0; b < bits; b++)
                    if ((i & (1 << b)) != 0) r |= 1 << (bits - 1 - b);
                reversed[i] = r;
            }
            cos = new float[size / 2];
            sin = new float[size / 2];
            for (var i = 0; i < size / 2; i++)
            {
                cos[i] = (float)Math.Cos(2 * Math.PI * i / size);
                sin[i] = (float)Math.Sin(2 * Math.PI * i / size);
            }
        }

        /// <summary>Forward (e^-i), or inverse (e^+i, unscaled: divide by Size after).</summary>
        public void Run(float[] re, float[] im, bool inverse)
        {
            var n = Size;
            for (var i = 0; i < n; i++)
            {
                var j = reversed[i];
                if (j <= i) continue;
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
            var sign = inverse ? 1f : -1f;
            for (var len = 2; len <= n; len <<= 1)
            {
                var half = len >> 1;
                var step = n / len;
                for (var start = 0; start < n; start += len)
                {
                    for (int k = 0, w = 0; k < half; k++, w += step)
                    {
                        var wr = cos[w];
                        var wi = sign * sin[w];
                        var a = start + k;
                        var b = a + half;
                        var tr = re[b] * wr - im[b] * wi;
                        var ti = re[b] * wi + im[b] * wr;
                        re[b] = re[a] - tr;
                        im[b] = im[a] - ti;
                        re[a] += tr;
                        im[a] += ti;
                    }
                }
            }
        }
    }
}
