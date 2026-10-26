using System;
using System.Collections.Generic;
using System.Linq;

namespace Bastion.Client.Audio.Mix
{
    /// <summary>
    /// A script of sounds (unity/tools/sonido/guiones/*.json: what plays, stops, loops, ducks, when) played on a Mixer at
    /// its frames, with the random numbers of 0.10.1's tools: for comparing the mix with engine.ts's (mezcla.mjs), in dotnet
    /// and in the app (SoundRun). Each reads the JSON its own way into Events.
    /// </summary>
    public sealed class Script
    {
        public sealed class Event
        {
            public double At;
            public string Do = "";
            public string? Id;
            public string? File;
            public string? Bus;
            public string? Word;
            public readonly Dictionary<string, double> Numbers = new Dictionary<string, double>();
            public readonly Dictionary<string, float> Patch = new Dictionary<string, float>();

            public float F(string name, float otherwise) => Numbers.TryGetValue(name, out var v) ? (float)v : otherwise;

            public double D(string name, double otherwise) => Numbers.TryGetValue(name, out var v) ? v : otherwise;
        }

        public readonly int Rate;
        public readonly double Seconds;
        public readonly Func<double> Random;
        /// <summary>The blocks it is rendered in (Unity's DSP buffer; 128 by default, Chromium's quantum).</summary>
        public readonly int Block;
        public readonly long Frames;
        private readonly List<(long frame, Event e)> events;
        private readonly Func<string, Sample?> samples;
        private readonly Dictionary<string, object> ids = new Dictionary<string, object>();
        private readonly Dictionary<string, float> volumes = new Dictionary<string, float> { ["master"] = 0.8f, ["sfx"] = 0.8f, ["music"] = 0.5f, ["ambience"] = 0.6f, ["ui"] = 0.7f };
        private bool muted;
        private int next;

        /// <summary>`events` at `rate` for `seconds`, the samples of their files from `samples`; each at the quantum nearest its time (as Chromium suspends).</summary>
        public Script(int rate, double seconds, uint seed, int block, IEnumerable<Event> events, Func<string, Sample?> samples)
        {
            Rate = rate;
            Seconds = seconds;
            Random = Rng(seed);
            Block = block;
            Frames = (long)Math.Ceiling(seconds * rate);
            this.samples = samples;
            this.events = events.Select((e) => ((long)Math.Floor(e.At * rate / 128 + 0.5) * 128, e)).OrderBy((x) => x.Item1).ToList();
        }

        /// <summary>The default volumes, as the tools' renders start.</summary>
        public static float[] Volumes => new[] { 0.8f, 0.8f, 0.5f, 0.6f, 0.7f };

        /// <summary>The files the events play, to read before.</summary>
        public IEnumerable<string> Files => events.Select((x) => x.e.File).Where((f) => f != null).Distinct()!;

        /// <summary>
        /// The events due in the block from `frame` (`frames` long) asked of `mixer`, before it renders it. What is made after
        /// the first block joins at the next quantum, as in Chromium (Mixer.Late) — only for blocks of 128.
        /// </summary>
        public void Run(Mixer mixer, long frame, int frames)
        {
            while (next < events.Count && events[next].frame < frame + frames) Apply(mixer, events[next++].e);
        }

        private Sample Load(string? file) => samples(file ?? "") ?? throw new InvalidOperationException($"no hay {file}");

        private float[] Values() => new[] { volumes["master"], volumes["sfx"], volumes["music"], volumes["ambience"], volumes["ui"] };

        private static Bus BusOf(string? name) => name == null ? Bus.Sfx : (Bus)Enum.Parse(typeof(Bus), name, true);

        private void Apply(Mixer mixer, Event e)
        {
            switch (e.Do)
            {
                case "play":
                    ids[e.Id!] = mixer.Play(Load(e.File), BusOf(e.Bus), e.F("gain", 1), e.F("rate", 1), e.F("pan", 0), e.D("delay", 0), e.F("reverb", 0));
                    break;
                case "stop":
                    mixer.Stop((Voice)ids[e.Id!], e.D("fade", 0.05));
                    break;
                case "loop":
                {
                    var s = Load(e.File);
                    ids[e.Id!] = mixer.Loop(s, BusOf(e.Bus), e.F("gain", 1), e.F("reverb", 0), Random() * s.Seconds);
                    break;
                }
                case "setLoop":
                    mixer.SetLoop((Voice)ids[e.Id!], e.F("level", 1), e.F("place", 0), e.F("muffle", 0));
                    break;
                case "endLoop":
                    mixer.EndLoop((Voice)ids[e.Id!], e.D("windDown", 0));
                    break;
                case "duck":
                    mixer.Duck(e.F("depth", 0.45f), e.D("hold", 0.5), e.D("release", 1.4));
                    break;
                case "holdDuck":
                    ids[e.Id!] = mixer.HoldDuck(e.F("depth", 0.35f));
                    break;
                case "release":
                    ((Action)ids[e.Id!])();
                    break;
                case "volumes":
                    foreach (var p in e.Patch) volumes[p.Key] = p.Value;
                    mixer.SetVolumes(Values(), muted);
                    break;
                case "mute":
                    muted = !muted;
                    mixer.SetVolumes(Values(), muted);
                    break;
                case "morse":
                    mixer.Morse(e.Word, Random, e.F("level", 0.05f), e.D("unit", 0.055));
                    break;
                case "music":
                {
                    // music.ts apply's piece, as an AudioBufferSourceNode plays it (the app streams it: SoundPlayer's Tap).
                    var v = mixer.Feed(new SampleFeed(Load(e.File), Rate), Bus.Music, 0.35f);
                    mixer.Fade(v, 1, e.D("fade", 2.5));
                    ids[e.Id!] = v;
                    break;
                }
                case "fade":
                    mixer.Fade((Voice)ids[e.Id!], e.F("level", 0), e.D("seconds", 1));
                    break;
                default:
                    throw new InvalidOperationException($"no sé hacer «{e.Do}»");
            }
        }

        /// <summary>The tools' random numbers (render.ts rng): the reverb's noise and the loops' starts as Chromium got them.</summary>
        public static Func<double> Rng(uint seed)
        {
            var s = seed == 0 ? 1u : seed;
            return () =>
            {
                s += 0x6d2b79f5;
                var t = (s ^ (s >> 15)) * (1 | s);
                t = (t + (t ^ (t >> 7)) * (61 | t)) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            };
        }

        /// <summary>A sample looping as an AudioBufferSourceNode at pitch 1 reads it: linearly between frames, at its rate's share of the mix's.</summary>
        private sealed class SampleFeed : IFeed
        {
            private readonly Sample sample;
            private readonly double step;
            private double position;

            public SampleFeed(Sample sample, int rate)
            {
                this.sample = sample;
                step = (double)sample.Rate / rate;
            }

            public void Read(float[] left, float[] right, int frames)
            {
                var n = sample.Frames;
                var c = sample.Channels;
                for (var i = 0; i < frames; i++)
                {
                    if (position >= n) position -= n;
                    var i1 = (int)position;
                    var f = (float)(position - i1);
                    var i2 = i1 + 1 >= n ? 0 : i1 + 1;
                    left[i] = (sample.Data[i1 * c] + (sample.Data[i2 * c] - sample.Data[i1 * c]) * f) / 32768f;
                    right[i] = c == 1 ? left[i] : (sample.Data[i1 * c + 1] + (sample.Data[i2 * c + 1] - sample.Data[i1 * c + 1]) * f) / 32768f;
                    position += step;
                }
            }
        }
    }
}
