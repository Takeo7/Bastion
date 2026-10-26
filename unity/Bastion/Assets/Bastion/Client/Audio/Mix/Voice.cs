using System;

namespace Bastion.Client.Audio.Mix
{
    /// <summary>
    /// Something sounding, as 0.10.1's engine.ts strings its nodes: a sample (once or looping), a tone (the radio's Morse)
    /// or a feed from outside (Unity's streams: the music and the beds), through its level, the lowpass of distance if it
    /// has one, its place in stereo if it has one, into its bus and, if it asks, the reverb. Chromium's ways where they
    /// show: playbackRate read once each 128 frames, linear interpolation, a mono sound with no panner in both ears whole,
    /// with a panner at equal power.
    /// </summary>
    public sealed class Voice
    {
        internal readonly Bus bus;
        internal readonly Sample? sample;
        internal readonly IFeed? feed;
        internal readonly float tone;
        internal readonly bool loop;
        internal readonly float reverb;
        internal readonly Param gain;
        internal readonly Param rate = new Param(1);
        internal readonly Param? pan;
        internal readonly Param? cutoff;
        internal double start;
        internal double stop = double.MaxValue;
        /// <summary>Where in the sample, in its frames (WebAudio's virtual read index).</summary>
        internal double position;
        internal bool done;
        /// <summary>A loop told to stop: nothing moves it after.</summary>
        internal bool stopping;
        private float pitch;
        private bool pitched;
        private float placed = float.NaN;
        private double panCos, panSin;
        private bool begun;
        private double x1, x2, y1, y2;
        private double phase;

        internal Voice(Bus bus, Sample? sample, IFeed? feed, float tone, bool loop, float gain, float? pan, bool muffled, float reverb)
        {
            this.bus = bus;
            this.sample = sample;
            this.feed = feed;
            this.tone = tone;
            this.loop = loop;
            this.reverb = reverb;
            this.gain = new Param(gain);
            this.pan = pan.HasValue ? new Param(pan.Value) : null;
            cutoff = muffled ? new Param(12000) : null;
        }

        /// <summary>
        /// `frames` frames from `time` (the mix's seconds, frame `first` of its count) added into the bus's ears and its send
        /// to the reverb; `feedL`/`feedR` scratch for a feed. False once it is over.
        /// </summary>
        internal bool Render(double time, long first, int frames, int rateHz, float[] l, float[] r, float[] sendL, float[] sendR, float[] feedL, float[] feedR)
        {
            if (done) return false;
            var dt = 1.0 / rateHz;
            if (feed != null) feed.Read(feedL, feedR, frames);
            // Chromium's frames for a start and a stop: the time's, rounded up (a sample's start between two frames reads
            // that much into it: below).
            var startFrame = (long)Math.Ceiling(start * rateHz);
            var stopFrame = stop == double.MaxValue ? long.MaxValue : (long)Math.Ceiling(stop * rateHz);
            for (var i = 0; i < frames; i++)
            {
                var t = time + i * dt;
                if (first + i >= stopFrame)
                {
                    done = true;
                    return false;
                }
                if (first + i < startFrame) continue;
                if (!begun)
                {
                    begun = true;
                    if (sample != null && first + i == startFrame) position += (startFrame - start * rateHz) * rate.At(t) * ((double)sample.Rate / rateHz);
                }
                // playbackRate is k-rate: read at the start of each render quantum.
                if (((first + i) & 127) == 0 || !pitched)
                {
                    pitch = rate.At(t);
                    pitched = true;
                }
                float a, b;
                if (sample != null)
                {
                    if (!Read(out a, out b, rateHz))
                    {
                        done = true;
                        return false;
                    }
                }
                else if (feed != null)
                {
                    a = feedL[i];
                    b = feedR[i];
                }
                else
                {
                    a = b = (float)Math.Sin(phase);
                    phase += 2 * Math.PI * tone * dt;
                    if (phase > 2 * Math.PI) phase -= 2 * Math.PI;
                }
                if (cutoff != null)
                {
                    a = Lowpass(a, cutoff.At(t), rateHz);
                    b = a;
                }
                var g = gain.At(t);
                a *= g;
                b *= g;
                if (pan != null)
                {
                    // Equal power: its two gains again only when the place moves.
                    var p = Math.Max(-1f, Math.Min(1f, pan.At(t)));
                    if (p != placed)
                    {
                        placed = p;
                        var x = (Mono ? (p + 1) * 0.5 : p <= 0 ? p + 1 : p) * Math.PI / 2;
                        panCos = Math.Cos(x);
                        panSin = Math.Sin(x);
                    }
                    if (Mono)
                    {
                        var m = a;
                        a = (float)(m * panCos);
                        b = (float)(m * panSin);
                    }
                    else if (p <= 0)
                    {
                        var ll = a + b * panCos;
                        b = (float)(b * panSin);
                        a = (float)ll;
                    }
                    else
                    {
                        var rr = b + a * panSin;
                        a = (float)(a * panCos);
                        b = (float)rr;
                    }
                }
                l[i] += a;
                r[i] += b;
                if (reverb > 0)
                {
                    sendL[i] += a * reverb;
                    sendR[i] += b * reverb;
                }
            }
            return true;
        }

        private bool Mono => sample != null ? sample.Channels == 1 : feed == null;

        /// <summary>The sample at `position`, linearly between frames, and on by the pitch; false past its end (unless it loops).</summary>
        private bool Read(out float a, out float b, int rateHz)
        {
            var s = sample!;
            var length = s.Frames;
            if (position >= length)
            {
                if (!loop)
                {
                    a = b = 0;
                    return false;
                }
                position -= length * Math.Floor(position / length);
            }
            a = b = 0;
            var i1 = (int)position;
            var f = (float)(position - i1);
            var i2 = i1 + 1;
            // Past the last frame Chromium goes on in a straight line from the two before (looping, into the start).
            var past = i2 >= length && !loop;
            if (i2 >= length) i2 = loop ? i2 - length : Math.Max(0, i1 - 1);
            const float scale = 1f / 32768;
            var c = s.Channels;
            for (var ch = 0; ch < c; ch++)
            {
                float s1 = s.Data[i1 * c + ch];
                float s2 = past ? 2 * s1 - s.Data[i2 * c + ch] : s.Data[i2 * c + ch];
                var v = (s1 + (s2 - s1) * f) * scale;
                if (ch == 0) a = v;
                else b = v;
            }
            if (c == 1) b = a;
            position += pitch * ((double)s.Rate / rateHz);
            return true;
        }

        private double c0, c1, c2, c3, c4;
        private float lastCutoff = -1;

        /// <summary>Chromium's lowpass biquad (Q 1 dB), its coefficients again whenever the cutoff moves.</summary>
        private float Lowpass(float x, float hz, int rateHz)
        {
            if (hz != lastCutoff)
            {
                lastCutoff = hz;
                var cutoffN = Math.Max(0.0, Math.Min(1.0, hz / (rateHz / 2.0)));
                if (cutoffN >= 1)
                {
                    c0 = 1;
                    c1 = c2 = c3 = c4 = 0;
                }
                else
                {
                    var resonance = Math.Pow(10, 1 / 20.0);
                    var theta = Math.PI * cutoffN;
                    var alpha = Math.Sin(theta) / (2 * resonance);
                    var cosw = Math.Cos(theta);
                    var beta = (1 - cosw) / 2;
                    var a0 = 1 + alpha;
                    c0 = beta / a0;
                    c1 = 2 * beta / a0;
                    c2 = beta / a0;
                    c3 = -2 * cosw / a0;
                    c4 = (1 - alpha) / a0;
                }
            }
            var y = c0 * x + c1 * x1 + c2 * x2 - c3 * y1 - c4 * y2;
            x2 = x1;
            x1 = x;
            y2 = y1;
            y1 = y;
            return (float)y;
        }
    }

    /// <summary>Sound from outside the mix (a Unity AudioSource's, through a Tap): `frames` frames of each ear, at the mix's rate.</summary>
    public interface IFeed
    {
        void Read(float[] left, float[] right, int frames);
    }
}
