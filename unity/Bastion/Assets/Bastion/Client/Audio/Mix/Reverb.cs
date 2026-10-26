using System;

namespace Bastion.Client.Audio.Mix
{
    /// <summary>
    /// The shared reverb of 0.10.1 (audio/engine.ts): a ConvolverNode on a stereo tail of decaying noise, impulse(2.4 s,
    /// decay 2.8), normalised as Chromium normalises it. The convolution is exact: its first 4096 frames in blocks of the
    /// mix's own, the rest in blocks of 4096 worked out one block ahead (overlap-save, the impulse cut into parts of one
    /// block each, the left and right ears in one complex FFT). All of it in blocks of 512 cost the audio thread a fifth
    /// of its time in Mono (26/10); this, about a third of that.
    /// </summary>
    internal sealed class Reverb
    {
        /// <summary>Where the impulse's tail starts, and the blocks it is worked in.</summary>
        private const int Tail = 4096;

        public readonly int Block;
        private readonly Segment head;
        private readonly Segment? tail;
        private readonly float[] tailL = new float[Tail], tailR = new float[Tail];
        private readonly float[] aheadL = new float[Tail], aheadR = new float[Tail];
        private int filled;

        /// <summary>The impulse as engine.ts makes it, from `random` (0 to 1): silence for 12 ms, then noise fading as (1 − t)^decay.</summary>
        public static (float[] left, float[] right) Impulse(int rate, double seconds, double decay, Func<double> random)
        {
            var length = (int)Math.Floor(rate * seconds);
            var ears = new[] { new float[length], new float[length] };
            foreach (var data in ears)
                for (var i = 0; i < length; i++)
                {
                    var t = (double)i / length;
                    data[i] = i < rate * 0.012 ? 0 : (float)((random() * 2 - 1) * Math.Pow(1 - t, decay));
                }
            return (ears[0], ears[1]);
        }

        /// <summary>
        /// Chromium's normalisation of a convolver's impulse (Reverb::CalculateNormalizationScale): by its RMS power, −58 dB,
        /// and in proportion to 44.1 kHz.
        /// </summary>
        public static float Normalisation(int rate, float[] left, float[] right)
        {
            double power = 0;
            foreach (var data in new[] { left, right })
                for (var i = 0; i < data.Length; i++) power += (double)data[i] * data[i];
            power = Math.Sqrt(power / (2.0 * left.Length));
            if (double.IsNaN(power) || double.IsInfinity(power) || power < 0.000125) power = 0.000125;
            return (float)(1 / power * Math.Pow(10, -58 / 20.0) * (44100.0 / rate));
        }

        /// <summary>Convolves with `left` and `right` (each ear its own), scaled by `scale`, in blocks of `block` frames (a power of two up to 4096).</summary>
        public Reverb(float[] left, float[] right, float scale, int block)
        {
            Block = block;
            head = new Segment(left, right, 0, Math.Min(left.Length, Tail), scale, block);
            if (left.Length > Tail) tail = new Segment(left, right, Tail, left.Length, scale, Tail);
        }

        /// <summary>`frames` frames (a whole number of blocks) of the reverb's input into its output, which it overwrites.</summary>
        public void Run(float[] inL, float[] inR, float[] outL, float[] outR, int frames)
        {
            for (var at = 0; at < frames; at += Block)
            {
                head.Next(inL, inR, at, outL, outR, at);
                if (tail == null) continue;
                // The tail's part of these frames, worked out a block ago; their input kept for the next.
                for (var i = 0; i < Block; i++)
                {
                    outL[at + i] += aheadL[filled + i];
                    outR[at + i] += aheadR[filled + i];
                }
                Array.Copy(inL, at, tailL, filled, Block);
                Array.Copy(inR, at, tailR, filled, Block);
                filled += Block;
                if (filled < Tail) continue;
                filled = 0;
                tail.Next(tailL, tailR, 0, aheadL, aheadR, 0);
            }
        }

        /// <summary>
        /// The impulse from `from` to `to`, in parts of `block` frames, convolved one block at a time: what a block of input
        /// gives the output from `from` frames on (from the same block if `from` is 0, the next if it is one block).
        /// </summary>
        private sealed class Segment
        {
            private readonly int block;
            private readonly int parts;
            private readonly int bins;
            private readonly Fft fft;
            private readonly float[] hLre, hLim, hRre, hRim;
            private readonly float[] xLre, xLim, xRre, xRim;
            private readonly float[] yLre, yLim, yRre, yRim;
            private readonly float[] lastL, lastR;
            private readonly float[] re, im;
            private int slot;

            public Segment(float[] left, float[] right, int from, int to, float scale, int block)
            {
                this.block = block;
                var n = block * 2;
                fft = new Fft(n);
                bins = block + 1;
                parts = Math.Max(1, (to - from + block - 1) / block);
                hLre = new float[parts * bins];
                hLim = new float[parts * bins];
                hRre = new float[parts * bins];
                hRim = new float[parts * bins];
                xLre = new float[parts * bins];
                xLim = new float[parts * bins];
                xRre = new float[parts * bins];
                xRim = new float[parts * bins];
                yLre = new float[bins];
                yLim = new float[bins];
                yRre = new float[bins];
                yRim = new float[bins];
                lastL = new float[block];
                lastR = new float[block];
                re = new float[n];
                im = new float[n];
                for (var p = 0; p < parts; p++)
                {
                    Array.Clear(re, 0, n);
                    Array.Clear(im, 0, n);
                    for (var i = 0; i < block && from + p * block + i < to; i++)
                    {
                        re[i] = left[from + p * block + i] * scale;
                        im[i] = right[from + p * block + i] * scale;
                    }
                    fft.Run(re, im, false);
                    Split(p * bins, hLre, hLim, hRre, hRim);
                }
            }

            /// <summary>One block of input from `at`; what it gives, with the blocks before, into the output from `outAt` (overwritten).</summary>
            public void Next(float[] inL, float[] inR, int at, float[] outL, float[] outR, int outAt)
            {
                var b = block;
                var n = b * 2;
                // The last block and this one, left as the real part and right as the imaginary.
                Array.Copy(lastL, 0, re, 0, b);
                Array.Copy(lastR, 0, im, 0, b);
                Array.Copy(inL, at, re, b, b);
                Array.Copy(inR, at, im, b, b);
                Array.Copy(inL, at, lastL, 0, b);
                Array.Copy(inR, at, lastR, 0, b);
                fft.Run(re, im, false);
                slot = (slot + 1) % parts;
                Split(slot * bins, xLre, xLim, xRre, xRim);
                Array.Clear(yLre, 0, bins);
                Array.Clear(yLim, 0, bins);
                Array.Clear(yRre, 0, bins);
                Array.Clear(yRim, 0, bins);
                for (var p = 0; p < parts; p++)
                {
                    var x = ((slot - p + parts) % parts) * bins;
                    var h = p * bins;
                    for (var k = 0; k < bins; k++)
                    {
                        float ar = xLre[x + k], ai = xLim[x + k], br = hLre[h + k], bi = hLim[h + k];
                        yLre[k] += ar * br - ai * bi;
                        yLim[k] += ar * bi + ai * br;
                        ar = xRre[x + k];
                        ai = xRim[x + k];
                        br = hRre[h + k];
                        bi = hRim[h + k];
                        yRre[k] += ar * br - ai * bi;
                        yRim[k] += ar * bi + ai * br;
                    }
                }
                // Back together: left + i·right, the upper half the mirror of the lower.
                for (var k = 0; k < bins; k++)
                {
                    re[k] = yLre[k] - yRim[k];
                    im[k] = yLim[k] + yRre[k];
                }
                for (var k = 1; k < b; k++)
                {
                    re[n - k] = yLre[k] + yRim[k];
                    im[n - k] = yRre[k] - yLim[k];
                }
                fft.Run(re, im, true);
                var scale = 1f / n;
                for (var i = 0; i < b; i++)
                {
                    outL[outAt + i] = re[b + i] * scale;
                    outR[outAt + i] = im[b + i] * scale;
                }
            }

            /// <summary>The spectra of the two real signals in `re` + i·`im` (an FFT of them), from bin 0 to the middle, into `at`.</summary>
            private void Split(int at, float[] lre, float[] lim, float[] rre, float[] rim)
            {
                var n = re.Length;
                for (var k = 0; k < bins; k++)
                {
                    var m = k == 0 ? 0 : n - k;
                    float a = re[k], bb = im[k], c = re[m], d = im[m];
                    lre[at + k] = (a + c) * 0.5f;
                    lim[at + k] = (bb - d) * 0.5f;
                    rre[at + k] = (bb + d) * 0.5f;
                    rim[at + k] = (c - a) * 0.5f;
                }
            }
        }
    }
}
