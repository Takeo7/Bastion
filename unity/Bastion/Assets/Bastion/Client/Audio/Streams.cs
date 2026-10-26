using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Audio.Mix;
using UnityEngine;

namespace Bastion.Client.Audio
{
    /// <summary>
    /// A long piece playing from disk into the mix: its clip looping on an AudioSource of its own under the player, its
    /// sound taken by a Tap into a voice of the Mixer, whose level fades it in and out.
    /// </summary>
    internal sealed class Streamed
    {
        private readonly GameObject holder;
        public readonly Voice voice;

        private Streamed(GameObject holder, Voice voice)
        {
            this.holder = holder;
            this.voice = voice;
        }

        /// <summary>`file` (Generado's) on `bus`, from silence; null if it isn't there.</summary>
        public static Streamed? Start(SoundPlayer player, string file, Bus bus, float reverb)
        {
            var clip = Manifest.Clip(file);
            if (clip == null)
            {
                Debug.LogWarning($"[sonido] falta {file} en Resources/{Manifest.Folder}");
                return null;
            }
            var o = new GameObject(file);
            o.transform.SetParent(player.transform, false);
            var source = o.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.priority = 0;
            var tap = o.AddComponent<Tap>();
            var voice = player.Mix.Feed(tap, bus, reverb);
            source.Play();
            return new Streamed(o, voice);
        }

        /// <summary>To `level` over `seconds`, in a straight line.</summary>
        public void Fade(SoundPlayer player, float level, double seconds) => player.Mix.Fade(voice, level, seconds);

        /// <summary>Faded out over `seconds`, then gone (`after` more seconds, as music.ts and ambience.ts left them).</summary>
        public void End(SoundPlayer player, double seconds, double after)
        {
            player.Mix.Fade(voice, 0, seconds, true);
            player.After(seconds + after, () => UnityEngine.Object.Destroy(holder));
        }
    }

    /// <summary>
    /// The music (music.ts): the score where it isn't recorded (the base and the menus, the Map: «base», «geo»), rendered
    /// from 0.10.1 (Generado/vivos), with 0.35 of it to the reverb; in combat its recorded beds instead («calm», «tense»,
    /// «alien»: bed.ambience and bed.tension, each at its level), a bed that plays on keeping its place. Main thread.
    /// </summary>
    public static class Music
    {
        /// <summary>music.ts BEDS: the combat's recorded layers and how loud each sits, by piece.</summary>
        private static readonly Dictionary<string, (string group, float level)[]> Beds = new Dictionary<string, (string, float)[]>
        {
            ["calm"] = new[] { ("bed.ambience", 0.8f) },
            ["tense"] = new[] { ("bed.ambience", 0.6f), ("bed.tension", 0.6f) },
            ["alien"] = new[] { ("bed.ambience", 0.3f), ("bed.tension", 0.85f) },
        };

        private static string? score;
        private static Streamed? playing;
        private static readonly Dictionary<string, (Streamed stream, float trim)> layers = new Dictionary<string, (Streamed, float)>();

        /// <summary>The piece asked for last (null: silence).</summary>
        public static string? Current { get; private set; }

        /// <summary>music.ts play: to `piece` (null: silence), fading over `fade` seconds.</summary>
        public static void Play(string? piece, double fade = 2.5)
        {
            if (piece != Current) Sound.Trace($"música {piece ?? "ninguna"}", Bus.Music, 1, 0, 0);
            Current = piece;
            var p = SoundPlayer.Instance;
            if (p?.manifest == null) return;
            var beds = piece != null && Beds.TryGetValue(piece, out var wanted) && p.manifest.recorded.ContainsKey("bed.ambience") ? wanted : null;
            Score(p, beds != null ? null : piece, fade);
            Layers(p, beds, fade);
        }

        /// <summary>music.ts apply: the score to `piece` (null: none), the old one fading out as the new one fades in.</summary>
        private static void Score(SoundPlayer p, string? piece, double fade)
        {
            if (score == piece) return;
            playing?.End(p, fade, 4);
            playing = null;
            score = piece;
            if (piece == null || !p.manifest!.pieces.TryGetValue($"music.{piece}", out var file)) return;
            playing = Streamed.Start(p, file, Bus.Music, 0.35f);
            playing?.Fade(p, 1, fade);
        }

        /// <summary>music.ts Beds.set: the layers to `wanted` (null: none) over `fade` seconds; one already playing keeps its place.</summary>
        private static void Layers(SoundPlayer p, (string group, float level)[]? wanted, double fade)
        {
            foreach (var group in layers.Keys.ToList())
            {
                if (wanted != null && wanted.Any((w) => w.group == group)) continue;
                layers[group].stream.End(p, fade, 0.5);
                layers.Remove(group);
            }
            foreach (var (group, level) in wanted ?? Array.Empty<(string, float)>())
            {
                if (!layers.TryGetValue(group, out var layer))
                {
                    if (!p.manifest!.recorded.TryGetValue(group, out var takes) || takes.Length == 0) continue;
                    var take = takes[(int)Math.Floor(p.Random() * takes.Length)];
                    var stream = Streamed.Start(p, take.file, Bus.Music, 0);
                    if (stream == null) continue;
                    layers[group] = layer = (stream, (float)Math.Pow(10, take.db / 20));
                }
                layer.stream.Fade(p, level * layer.trim, fade);
            }
        }
    }

    /// <summary>
    /// The ambience under everything (ambience.ts): the town, the outskirts, the plant, the Bastión outside, the globe
    /// («city», «wilds», «facility», «base», «geo»), rendered from 0.10.1 (Generado/vivos); one fades into the next. Main thread.
    /// </summary>
    public static class Ambience
    {
        private static string? running;
        private static Streamed? playing;

        /// <summary>ambience.ts play: to `bed` (null: none), fading over `fade` seconds.</summary>
        public static void Play(string? bed, double fade = 2)
        {
            var p = SoundPlayer.Instance;
            if (p?.manifest == null || running == bed) return;
            Sound.Trace($"ambiente {bed ?? "ninguno"}", Bus.Ambience, 1, 0, 0);
            playing?.End(p, fade, 0.2);
            playing = null;
            running = bed;
            if (bed == null || !p.manifest.pieces.TryGetValue($"ambience.{bed}", out var file)) return;
            playing = Streamed.Start(p, file, Bus.Ambience, 0);
            playing?.Fade(p, 1, fade);
        }
    }
}
