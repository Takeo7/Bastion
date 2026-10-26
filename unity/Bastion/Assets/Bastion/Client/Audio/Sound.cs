using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Audio.Mix;
using UnityEngine;

namespace Bastion.Client.Audio
{
    /// <summary>What an interface sound means (ui.ts UiKind). The names come from the old case file: a key, the bell, a page, a stamp.</summary>
    public enum UiKind { Key, Space, Bell, Confirm, Page, Paper, Stamp, Error, CellBuy, CellHover, CellHold }

    /// <summary>
    /// The game's effects, as 0.10.1 played them (docs/UNITY-SONIDO.md): sounds.ts's recipes (bank.ts sound), the recorded
    /// groups (recorded.ts), the interface's (ui.ts), the loops, the music's ducking and the radio's Morse. Main thread
    /// only. Nothing plays while the sound is off (M), as engine.ts live() said.
    /// </summary>
    public static class Sound
    {
        private static readonly Dictionary<string, string> lastTake = new Dictionary<string, string>();

        /// <summary>
        /// For the tests (BASTION_SONIDO_TRAZA=1, as recorded.ts's window.__recorded): every sound played goes to the log,
        /// with its bus, level and place, and each bus's level every ten seconds (SoundPlayer).
        /// </summary>
        internal static readonly bool Tracing = Environment.GetEnvironmentVariable("BASTION_SONIDO_TRAZA") == "1";

        /// <summary>With the trace on, `what` to the log at its time (the combat's events as they start playing); else nothing.</summary>
        public static void Note(string what)
        {
            if (Tracing) Debug.Log($"[sonido] {Time.realtimeSinceStartup:F2} s · {what}");
        }

        internal static void Trace(string what, Bus bus, float gain, float pan, double delay)
        {
            if (Tracing) Debug.Log($"[sonido] {Time.realtimeSinceStartup:F2} s · {what} · {bus} · nivel {gain:F2}{(pan != 0 ? $" · pan {pan:F2}" : "")}{(delay > 0 ? $" · a {delay:F2} s" : "")}");
        }
        private static double lastHover = double.NegativeInfinity;

        /// <summary>The player and its manifest, if sound can play now.</summary>
        private static (SoundPlayer player, Manifest manifest)? Live()
        {
            var p = SoundPlayer.Instance;
            if (p == null || p.manifest == null || Volumes.Muted) return null;
            return (p, p.manifest);
        }

        private static float Db(float db) => (float)Math.Pow(10, db / 20);

        private static float Nudge(SoundPlayer p, float spread) => (float)(1 + (p.Random() * 2 - 1) * spread);

        /// <summary>
        /// bank.ts sound: one of the recipe's variants at random, its pitch nudged by up to ±`spread`, at its own level and
        /// reverb (plus `reverb`), on its bus unless `bus` says another. What stops it, fading out (a hold let go early).
        /// </summary>
        public static Action Play(string name, float gain = 1, float rate = 1, float pan = 0, double delay = 0, float reverb = 0, float spread = 0.04f, Bus? bus = null)
        {
            if (!(Live() is var (p, m)) || !m.recipes.TryGetValue(name, out var def) || def.files.Length == 0) return () => { };
            var file = def.files[(int)Math.Floor(p.Random() * def.files.Length)];
            var sample = m.Samples(file);
            if (sample == null) return () => { };
            var mix = p.Mix;
            var v = mix.Play(sample, bus ?? def.bus, gain * Db(def.level), rate * Nudge(p, spread), pan, delay, reverb + def.reverb);
            Trace(file, bus ?? def.bus, gain * Db(def.level), pan, delay);
            return () => mix.Stop(v);
        }

        /// <summary>Whether there is a recipe called `name` (sfx.ts's `name in SOUNDS`).</summary>
        public static bool Exists(string name) => SoundPlayer.Instance?.manifest?.recipes.ContainsKey(name) == true;

        /// <summary>recorded.ts hasRecorded: whether `group` has takes.</summary>
        public static bool HasRecorded(string group) => Live() is var (_, m) && m.recorded.TryGetValue(group, out var takes) && takes.Length > 0;

        /// <summary>
        /// recorded.ts playRecorded: one take of `group` at random, never the one before when there are others, at its
        /// own level; false when there is none (the caller plays its own sound).
        /// </summary>
        public static bool Recorded(string group, float gain = 1, float rate = 1, float pan = 0, double delay = 0, float reverb = 0, float spread = 0.04f, Bus bus = Bus.Sfx)
        {
            if (!(Live() is var (p, m)) || !m.recorded.TryGetValue(group, out var takes) || takes.Length == 0) return false;
            lastTake.TryGetValue(group, out var last);
            var fresh = takes.Length > 1 ? takes.Where((t) => t.file != last).ToArray() : takes;
            var take = fresh[(int)Math.Floor(p.Random() * fresh.Length)];
            var sample = m.Samples(take.file);
            if (sample == null) return false;
            lastTake[group] = take.file;
            p.Mix.Play(sample, bus, gain * Db(take.db), rate * Nudge(p, spread), pan, delay, reverb);
            Trace($"{group} {take.file}", bus, gain * Db(take.db), pan, delay);
            return true;
        }

        /// <summary>ui.ts RECORDED: Kenney's recordings of each moment (one at random).</summary>
        private static readonly Dictionary<UiKind, string[]> UiRecorded = new Dictionary<UiKind, string[]>
        {
            [UiKind.Key] = new[] { "tick_004", "click_001" },
            [UiKind.Space] = new[] { "tick_004" },
            [UiKind.Bell] = new[] { "select_002" },
            [UiKind.Confirm] = new[] { "switch_004" },
            [UiKind.Page] = new[] { "glass_006" },
            [UiKind.Paper] = new[] { "maximize_007" },
            [UiKind.Stamp] = new[] { "confirmation_001" },
            [UiKind.Error] = new[] { "error_004" },
        };

        /// <summary>ui.ts GAIN: how loud each moment sits (the recordings are all normalised to the same peak).</summary>
        private static readonly Dictionary<UiKind, float> UiGain = new Dictionary<UiKind, float>
        {
            [UiKind.Key] = 0.32f, [UiKind.Space] = 0.3f, [UiKind.Bell] = 0.4f, [UiKind.Confirm] = 0.5f,
            [UiKind.Page] = 0.4f, [UiKind.Paper] = 0.42f, [UiKind.Stamp] = 0.55f, [UiKind.Error] = 0.45f,
        };

        /// <summary>ui.ts SYNTH: the game's own sound of a moment without a recording (the skill board's).</summary>
        private static readonly Dictionary<UiKind, string> UiSynth = new Dictionary<UiKind, string>
        {
            [UiKind.Key] = "ui.tick", [UiKind.Space] = "ui.tick", [UiKind.Bell] = "command", [UiKind.Confirm] = "command",
            [UiKind.Page] = "holo.page", [UiKind.Paper] = "radio.close", [UiKind.Stamp] = "geo.ping", [UiKind.Error] = "ui.error",
            [UiKind.CellBuy] = "cell.buy", [UiKind.CellHover] = "cell.hover", [UiKind.CellHold] = "cell.hold",
        };

        /// <summary>The interface's files, for SoundPlayer to read before they are needed.</summary>
        internal static IEnumerable<string> InterfaceFiles => UiRecorded.Values.SelectMany((n) => n).Distinct().Select((n) => $"interfaz/{n}.wav");

        /// <summary>
        /// ui.ts uiSound: an interface sound on the interface's bus (its recording, or the game's own sound); the skill
        /// board's hover at most once each 45 ms. What stops it, fading out (the board's hold, let go before it buys).
        /// Spread: 0.08 for a key, 0.03 otherwise (ui.ts's listener).
        /// </summary>
        public static Action Ui(UiKind kind, float gain = 1, float rate = 1, float pan = 0, float? spread = null, double delay = 0)
        {
            if (!(Live() is var (p, m))) return () => { };
            if (kind == UiKind.CellHover)
            {
                var now = Time.realtimeSinceStartupAsDouble;
                if (now - lastHover < 0.045) return () => { };
                lastHover = now;
            }
            if (UiRecorded.TryGetValue(kind, out var names))
            {
                var sample = m.Samples($"interfaz/{names[(int)Math.Floor(p.Random() * names.Length)]}.wav");
                if (sample != null)
                {
                    var mix = p.Mix;
                    var v = mix.Play(sample, Bus.Ui, gain * UiGain[kind], rate * Nudge(p, spread ?? 0.03f), pan, delay);
                    Trace($"ui {kind}", Bus.Ui, gain * UiGain[kind], pan, delay);
                    return () => mix.Stop(v);
                }
            }
            return Play(UiSynth[kind], gain, rate, pan, delay, bus: Bus.Ui, spread: spread ?? 0.04f);
        }

        /// <summary>
        /// engine.ts loop on bank.ts loopBuffer: the recipe's loop (the sirens) over and over from anywhere in it, coming in
        /// from silence; null if it can't play now.
        /// </summary>
        public static SoundLoop? Loop(string name, float gain = 1, float reverb = 0, Bus? bus = null)
        {
            if (!(Live() is var (p, m)) || !m.recipes.TryGetValue(name, out var def) || def.loopFile == null) return null;
            var sample = m.Samples(def.loopFile);
            if (sample == null) return null;
            Trace($"bucle {def.loopFile}", bus ?? def.bus, gain, 0, 0);
            return new SoundLoop(p.Mix, p.Mix.Loop(sample, bus ?? def.bus, gain, reverb, p.Random() * sample.Seconds));
        }

        /// <summary>engine.ts duck: the music lowered by `depth` for `hold` seconds, back over `release`.</summary>
        public static void Duck(float depth = 0.45f, double hold = 0.5, double release = 1.4) => SoundPlayer.Instance?.Mix.Duck(depth, hold, release);

        /// <summary>engine.ts holdDuck: the music held down (the radio talking) until what it returns is called.</summary>
        public static Action HoldDuck(float depth = 0.35f) => SoundPlayer.Instance?.Mix.HoldDuck(depth) ?? (() => { });

        /// <summary>radio.ts morse: `word` tapped at the key under the operator's lines (random letters if none), quiet, on the radio bus.</summary>
        public static void Morse(string? word = null, float level = 0.05f, double unit = 0.055)
        {
            if (Live() is var (p, _)) p.Mix.Morse(word, p.Random, level, unit);
        }
    }

    /// <summary>A sound playing in a loop (engine.ts Loop).</summary>
    public sealed class SoundLoop
    {
        private readonly Mixer mix;
        private readonly Voice voice;

        internal SoundLoop(Mixer mix, Voice voice)
        {
            this.mix = mix;
            this.voice = voice;
        }

        /// <summary>Its level, its place in stereo (−1 to 1) and how muffled by distance (0 near, 1 far), eased over a tenth of a second.</summary>
        public void Set(float level, float place, float muffle = 0) => mix.SetLoop(voice, level, place, muffle);

        /// <summary>Stops it at once, or winding down over `windDown` seconds (the pitch sinking as it fades).</summary>
        public void Stop(double windDown = 0) => mix.EndLoop(voice, windDown);
    }
}
