using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Bastion.Client.Audio
{
    /// <summary>The buses of 0.10.1's engine.ts, each with its own volume; the radio follows the interface's.</summary>
    public enum Bus { Sfx, Ui, Radio, Music, Ambience }
}

namespace Bastion.Client.Audio.Mix
{
    /// <summary>
    /// The game's sound mixed as 0.10.1's audio/engine.ts mixed it in Chromium, sample for sample: the voices into their
    /// buses (each at its share of the mix times its volume), the music through the duck, the sends into the shared reverb
    /// (each bus's at its volume), all through the master volume and the compressor. Render runs on the audio thread
    /// (Unity's OnAudioFilterRead); everything else is asked from any thread and done there at the start of the next block,
    /// «now» being that block's first frame (engine.ts's ctx.currentTime).
    /// </summary>
    public sealed class Mixer
    {
        /// <summary>engine.ts MIX: how loud each bus sits before the player's volume (by Bus).</summary>
        public static readonly float[] Share = { 1f, 0.55f, 0.7f, 0.42f, 0.5f };
        /// <summary>The volumes the reverb's sends are measured from (engine.ts DEFAULTS, by Bus: the radio is the interface's).</summary>
        private static readonly float[] Defaults = { 0.8f, 0.7f, 0.7f, 0.5f, 0.6f };
        private const int Buses = 5;
        private const float Wet = 0.55f;

        public readonly int Rate;
        private long frame;
        private readonly ConcurrentQueue<Action<double>> asked = new ConcurrentQueue<Action<double>>();
        private readonly List<Voice> voices = new List<Voice>();
        private readonly Param[] buses = new Param[Buses];
        private readonly Param[] wets = new Param[Buses];
        private readonly Param duck = new Param(1);
        private readonly Param master = new Param(1);
        private readonly float[] impulseL;
        private readonly float[] impulseR;
        private readonly float impulseScale;
        private Reverb reverb;
        private readonly Compressor compressor;
        private float[][] busL = Array.Empty<float[]>(), busR = Array.Empty<float[]>(), sendL = Array.Empty<float[]>(), sendR = Array.Empty<float[]>();
        private float[] inL = Array.Empty<float>(), inR = Array.Empty<float>(), wetL = Array.Empty<float>(), wetR = Array.Empty<float>();
        private float[] outL = Array.Empty<float>(), outR = Array.Empty<float>(), feedL = Array.Empty<float>(), feedR = Array.Empty<float>();

        /// <summary>
        /// At `rate`, in blocks of `block` frames (Unity's DSP buffer: the reverb works in them, up to 512), with the
        /// reverb's noise from `random` (engine.ts used Math.random: any will do) and the volumes as given (Volumes).
        /// </summary>
        public Mixer(int rate, int block, Func<double> random, float[] volumes, bool muted)
        {
            Rate = rate;
            (impulseL, impulseR) = Reverb.Impulse(rate, 2.4, 2.8, random);
            impulseScale = Reverb.Normalisation(rate, impulseL, impulseR);
            reverb = new Reverb(impulseL, impulseR, impulseScale, ReverbBlock(block));
            compressor = new Compressor(rate);
            // As engine.ts unlock leaves them: every gain at 1, eased to its volume over 50 ms (applyVolumes).
            for (var b = 0; b < Buses; b++)
            {
                buses[b] = new Param(1);
                buses[b].Target(Share[b] * VolumeOf(volumes, b), 0, 0.05);
                wets[b] = new Param(1);
                wets[b].Target(VolumeOf(volumes, b) / Defaults[b], 0, 0.05);
            }
            master.Target(muted ? 0 : volumes[0], 0, 0.05);
        }

        /// <summary>The volume that moves bus `b`, of master, sfx, music, ambience, ui (Volumes.All's order).</summary>
        private static float VolumeOf(float[] volumes, int b) => b switch
        {
            (int)Bus.Sfx => volumes[1],
            (int)Bus.Music => volumes[2],
            (int)Bus.Ambience => volumes[3],
            _ => volumes[4],
        };

        private static int ReverbBlock(int frames)
        {
            var b = 512;
            while (b > 32 && (b > frames || frames % b != 0)) b >>= 1;
            return b;
        }

        /// <summary>Whether each bus's output is measured (Levels): for the tests, it costs a little.</summary>
        public volatile bool metering;
        private readonly double[] energy = new double[Buses];
        private long metered;

        /// <summary>
        /// Each bus's level since the last call, in dB of RMS after its volume (−∞ for silence), by Bus; and back to
        /// zero. Read from any thread: a block's worth may land on either side.
        /// </summary>
        public double[] Levels()
        {
            var n = Math.Max(1, Interlocked.Exchange(ref metered, 0));
            var levels = new double[Buses];
            for (var b = 0; b < Buses; b++)
            {
                var e = Interlocked.Exchange(ref energy[b], 0);
                levels[b] = e > 0 ? 10 * Math.Log10(e / (2.0 * n)) : double.NegativeInfinity;
            }
            return levels;
        }

        /// <summary>The mix's seconds so far (on the audio thread, the block being made).</summary>
        public double Time => (double)frame / Rate;

        /// <summary>
        /// Frames before a voice asked for can start (Chromium's: a node made while it renders joins at the next quantum,
        /// 128). Only the comparison with 0.10.1's renders sets it; for the game, a block's start is soon enough.
        /// </summary>
        internal int Late { get; set; }

        /// <summary>Done on the audio thread at the start of the next block, with its time.</summary>
        public void Ask(Action<double> what) => asked.Enqueue(what);

        // ------------------------------------------------------------------ asked from the game

        /// <summary>The volumes changed (master, sfx, music, ambience, ui) or the mute: engine.ts applyVolumes, eased over 50 ms.</summary>
        public void SetVolumes(float[] volumes, bool muted)
        {
            var v = (float[])volumes.Clone();
            Ask((now) =>
            {
                master.Target(muted ? 0 : v[0], now, 0.05);
                for (var b = 0; b < Buses; b++)
                {
                    buses[b].Target(Share[b] * VolumeOf(v, b), now, 0.05);
                    wets[b].Target(VolumeOf(v, b) / Defaults[b], now, 0.05);
                }
            });
        }

        /// <summary>
        /// engine.ts play: `sample` once on `bus`, at `gain`, at `rate` times its pitch, at `pan` (0: no panner, mono in both
        /// ears whole), `delay` seconds from now, with `reverb` of it sent to the reverb. What stops it early (Stop).
        /// </summary>
        public Voice Play(Sample sample, Bus bus, float gain = 1, float rate = 1, float pan = 0, double delay = 0, float reverb = 0)
        {
            var v = new Voice(bus, sample, null, 0, false, gain, pan != 0 ? Math.Max(-1, Math.Min(1, pan)) : (float?)null, false, reverb);
            v.rate.Set(rate, 0);
            var late = (double)Late / Rate;
            Ask((now) =>
            {
                v.start = now + Math.Max(delay, late);
                voices.Add(v);
            });
            return v;
        }

        /// <summary>engine.ts stop: faded out over `fade` seconds, so it never clicks.</summary>
        public void Stop(Voice v, double fade = 0.05)
        {
            Ask((now) =>
            {
                v.gain.Cancel(now);
                v.gain.Set(v.gain.Value, now);
                v.gain.Linear(0, now + fade);
                v.stop = Math.Min(v.stop, now + fade + 0.01);
            });
        }

        /// <summary>
        /// engine.ts loop: `sample` over and over from `offset` seconds into it, through the lowpass of distance and a panner,
        /// coming in from silence to `gain`. Moved with SetLoop, ended with EndLoop.
        /// </summary>
        public Voice Loop(Sample sample, Bus bus, float gain, float reverb, double offset)
        {
            var v = new Voice(bus, sample, null, 0, true, 0, 0, true, reverb);
            var late = (double)Late / Rate;
            Ask((now) =>
            {
                v.start = now + late;
                // Chromium starts a buffer at an offset on the nearest whole frame.
                v.position = Math.Floor(offset * sample.Rate + 0.5);
                v.gain.Target(gain, now, 0.4);
                voices.Add(v);
            });
            return v;
        }

        /// <summary>engine.ts Loop.set: its level, its place in stereo and how muffled by distance (0 near, 1 far), eased over a tenth of a second.</summary>
        public void SetLoop(Voice v, float level, float place, float muffle)
        {
            Ask((now) =>
            {
                if (v.stopping) return;
                v.gain.Target(level, now, 0.12);
                v.pan!.Target(Math.Max(-1, Math.Min(1, place)), now, 0.12);
                v.cutoff!.Target(12000 - Math.Max(0, Math.Min(1, muffle)) * 9500, now, 0.12);
            });
        }

        /// <summary>engine.ts Loop.stop: at once, or winding down over `windDown` seconds, the pitch sinking as it fades.</summary>
        public void EndLoop(Voice v, double windDown = 0)
        {
            Ask((now) =>
            {
                if (v.stopping) return;
                v.stopping = true;
                v.gain.Cancel(now);
                v.gain.Set(v.gain.Value, now);
                if (windDown > 0)
                {
                    v.rate.Set(1, now);
                    v.rate.Exponential(0.3f, now + windDown);
                    v.gain.Linear(0, now + windDown);
                    v.stop = now + windDown + 0.05;
                }
                else
                {
                    v.gain.Target(0, now, 0.03);
                    v.stop = now + 0.2;
                }
            });
        }

        /// <summary>
        /// A tone of `hz` on `bus` (radio.ts morse's oscillator), silent but for what `shape` writes, given now: its start,
        /// its stop and its level's changes.
        /// </summary>
        internal Voice Tone(Bus bus, float hz, Action<Voice, double> shape)
        {
            var v = new Voice(bus, null, null, hz, false, 0, null, false, 0);
            Ask((now) =>
            {
                shape(v, now);
                voices.Add(v);
            });
            return v;
        }

        /// <summary>
        /// radio.ts morse: `word` tapped at the key on the radio bus (letters without a code skipped), quiet, a tone of 720 Hz;
        /// two to four random letters from `random` when there is no word.
        /// </summary>
        public void Morse(string? word, Func<double> random, float level = 0.05f, double unit = 0.055)
        {
            var letters = MorseCodes.Keys.ToArray();
            var text = word ?? new string(Enumerable.Range(0, 2 + (int)Math.Floor(random() * 3)).Select((_) => letters[(int)Math.Floor(random() * letters.Length)]).ToArray());
            Tone(Bus.Radio, 720, (v, now) =>
            {
                var t = now + 0.08;
                v.start = t;
                foreach (var ch in text.ToUpperInvariant())
                {
                    if (!MorseCodes.TryGetValue(ch, out var code)) continue;
                    foreach (var sym in code)
                    {
                        var dur = sym == '.' ? unit : unit * 3;
                        v.gain.Set(0, t);
                        v.gain.Linear(level, t + 0.006);
                        v.gain.Set(level, t + dur - 0.006);
                        v.gain.Linear(0, t + dur);
                        t += dur + unit;
                    }
                    t += unit * 2;
                }
                v.stop = t + 0.05;
            });
        }

        /// <summary>radio.ts CODES.</summary>
        private static readonly Dictionary<char, string> MorseCodes = new Dictionary<char, string>
        {
            ['A'] = ".-", ['D'] = "-..", ['E'] = ".", ['G'] = "--.", ['I'] = "..", ['K'] = "-.-", ['M'] = "--",
            ['N'] = "-.", ['O'] = "---", ['R'] = ".-.", ['S'] = "...", ['T'] = "-", ['U'] = "..-", ['W'] = ".--",
        };

        /// <summary>Sound from outside (a stream) on `bus`, silent until Fade brings it in; `reverb` of it to the reverb.</summary>
        public Voice Feed(IFeed feed, Bus bus, float reverb = 0)
        {
            var v = new Voice(bus, null, feed, 0, false, 0, null, false, reverb);
            var late = (double)Late / Rate;
            Ask((now) =>
            {
                v.start = now + late;
                voices.Add(v);
            });
            return v;
        }

        /// <summary>A voice's level to `level` over `seconds` in a straight line (music.ts's and ambience.ts's fades); `end`, then it stops.</summary>
        public void Fade(Voice v, float level, double seconds, bool end = false)
        {
            Ask((now) =>
            {
                v.gain.Cancel(now);
                v.gain.Set(v.gain.Value, now);
                v.gain.Linear(level, now + seconds);
                if (end) v.stop = now + seconds;
            });
        }

        /// <summary>engine.ts duck: the music lowered by `depth` for `hold` seconds, back over `release`.</summary>
        public void Duck(float depth = 0.45f, double hold = 0.5, double release = 1.4)
        {
            Ask((now) =>
            {
                duck.Cancel(now);
                duck.Set(duck.Value, now);
                duck.Linear(1 - depth, now + 0.06);
                duck.Set(1 - depth, now + 0.06 + hold);
                duck.Linear(1, now + 0.06 + hold + release);
            });
        }

        /// <summary>engine.ts holdDuck: the music held down (the radio talking) until what it returns is called.</summary>
        public Action HoldDuck(float depth = 0.35f)
        {
            Ask((now) =>
            {
                duck.Cancel(now);
                duck.Set(duck.Value, now);
                duck.Linear(1 - depth, now + 0.3);
            });
            return () => Ask((now) =>
            {
                duck.Cancel(now);
                duck.Set(duck.Value, now);
                duck.Linear(1, now + 1.2);
            });
        }

        // ------------------------------------------------------------------ the audio thread

        /// <summary>The next block into `data` (interleaved, `channels` of them), overwriting it.</summary>
        public void Render(float[] data, int channels)
        {
            var frames = data.Length / channels;
            Room(frames);
            var now = Time;
            while (asked.TryDequeue(out var what)) what(now);
            for (var b = 0; b < Buses; b++)
            {
                Array.Clear(busL[b], 0, frames);
                Array.Clear(busR[b], 0, frames);
                Array.Clear(sendL[b], 0, frames);
                Array.Clear(sendR[b], 0, frames);
            }
            for (var i = voices.Count - 1; i >= 0; i--)
            {
                var v = voices[i];
                var b = (int)v.bus;
                if (v.Render(now, frame, frames, Rate, busL[b], busR[b], sendL[b], sendR[b], feedL, feedR)) continue;
                voices[i] = voices[voices.Count - 1];
                voices.RemoveAt(voices.Count - 1);
            }
            var dt = 1.0 / Rate;
            for (var i = 0; i < frames; i++)
            {
                var t = now + i * dt;
                float l = 0, r = 0, sl = 0, sr = 0;
                for (var b = 0; b < Buses; b++)
                {
                    var g = buses[b].At(t);
                    if (b == (int)Bus.Music) g *= duck.At(t);
                    var bl = busL[b][i] * g;
                    var br = busR[b][i] * g;
                    if (metering) energy[b] += bl * bl + br * br;
                    l += bl;
                    r += br;
                    var w = wets[b].At(t);
                    sl += sendL[b][i] * w;
                    sr += sendR[b][i] * w;
                }
                outL[i] = l;
                outR[i] = r;
                inL[i] = sl;
                inR[i] = sr;
            }
            if (metering) Interlocked.Add(ref metered, frames);
            if (frames % reverb.Block != 0) reverb = new Reverb(impulseL, impulseR, impulseScale, ReverbBlock(frames));
            reverb.Run(inL, inR, wetL, wetR, frames);
            for (var i = 0; i < frames; i++)
            {
                var m = master.At(now + i * dt);
                outL[i] = (outL[i] + wetL[i] * Wet) * m;
                outR[i] = (outR[i] + wetR[i] * Wet) * m;
            }
            compressor.Run(outL, outR, frames);
            for (var i = 0; i < frames; i++)
            {
                var at = i * channels;
                if (channels == 1)
                {
                    data[at] = 0.5f * (outL[i] + outR[i]);
                    continue;
                }
                data[at] = outL[i];
                data[at + 1] = outR[i];
                for (var c = 2; c < channels; c++) data[at + c] = 0;
            }
            frame += frames;
        }

        private void Room(int frames)
        {
            if (outL.Length >= frames) return;
            busL = Arrays(frames);
            busR = Arrays(frames);
            sendL = Arrays(frames);
            sendR = Arrays(frames);
            inL = new float[frames];
            inR = new float[frames];
            wetL = new float[frames];
            wetR = new float[frames];
            outL = new float[frames];
            outR = new float[frames];
            feedL = new float[frames];
            feedR = new float[frames];
        }

        private static float[][] Arrays(int frames)
        {
            var a = new float[Buses][];
            for (var b = 0; b < Buses; b++) a[b] = new float[frames];
            return a;
        }
    }
}
