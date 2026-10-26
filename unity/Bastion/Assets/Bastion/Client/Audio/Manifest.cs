using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client.Audio.Mix;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Bastion.Client.Audio
{
    /// <summary>
    /// What the game's sound is, from Resources/Generado/manifiesto.json (unity/tools/sonido.sh, docs/UNITY-SONIDO.md):
    /// sounds.ts's recipes (bus, level, reverb, variants, loop), the recorded groups with their takes' levels, the live
    /// pieces and the interface's files. Their samples are read from the clips the first time they are asked for.
    /// </summary>
    internal sealed class Manifest
    {
        internal sealed class Recipe
        {
            public Bus bus;
            public float level;
            public float reverb;
            public string[] files = Array.Empty<string>();
            public string? loopFile;
        }

        internal readonly struct Take
        {
            public readonly string file;
            public readonly float db;

            public Take(string file, float db)
            {
                this.file = file;
                this.db = db;
            }
        }

        public const string Folder = "Generado";

        public readonly Dictionary<string, Recipe> recipes = new Dictionary<string, Recipe>();
        public readonly Dictionary<string, Take[]> recorded = new Dictionary<string, Take[]>();
        public readonly Dictionary<string, string> pieces = new Dictionary<string, string>();
        private readonly Dictionary<string, Sample> samples = new Dictionary<string, Sample>();

        /// <summary>The manifest in Resources, or null (no sound generated: the game plays silent).</summary>
        public static Manifest? Load()
        {
            var text = Resources.Load<TextAsset>(Folder + "/manifiesto");
            if (text == null) return null;
            var json = JObject.Parse(text.text);
            var m = new Manifest();
            foreach (var p in (JObject)json["sintetizados"]!)
            {
                var o = (JObject)p.Value!;
                m.recipes[p.Key] = new Recipe
                {
                    bus = BusOf((string?)o["bus"]),
                    level = (float?)o["level"] ?? 0,
                    reverb = (float?)o["reverb"] ?? 0,
                    files = o["files"]!.Select((f) => (string)f!).ToArray(),
                    loopFile = (string?)o["loopFile"],
                };
            }
            foreach (var p in (JObject)json["grabados"]!)
                m.recorded[p.Key] = p.Value!.Select((t) => new Take((string)t["file"]!, (float?)t["db"] ?? 0)).ToArray();
            foreach (var p in (JObject)json["vivos"]!) m.pieces[p.Key] = (string)p.Value!["file"]!;
            return m;
        }

        private static Bus BusOf(string? name) => name switch
        {
            "ui" => Bus.Ui,
            "radio" => Bus.Radio,
            "music" => Bus.Music,
            "ambience" => Bus.Ambience,
            _ => Bus.Sfx,
        };

        /// <summary>A file's clip in Resources (Generado/…/x.wav as Generado/…/x), or null.</summary>
        public static AudioClip? Clip(string file)
        {
            var path = Folder + "/" + (file.EndsWith(".wav", StringComparison.Ordinal) ? file.Substring(0, file.Length - 4) : file);
            return Resources.Load<AudioClip>(path);
        }

        /// <summary>A file's samples, read from its clip the first time (decompressed on load) and kept; null if it isn't there.</summary>
        public Sample? Samples(string file)
        {
            if (samples.TryGetValue(file, out var s)) return s;
            var clip = Clip(file);
            if (clip == null)
            {
                Debug.LogWarning($"[sonido] falta {file} en Resources/{Folder}");
                samples[file] = null!;
                return null;
            }
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0))
            {
                Debug.LogWarning($"[sonido] no se pudo leer {file} ({clip.loadType})");
                samples[file] = null!;
                return null;
            }
            s = Sample.FromFloats(data, clip.channels, clip.frequency);
            // Unity's copy is no longer needed: the mix reads its own.
            clip.UnloadAudioData();
            samples[file] = s;
            return s;
        }

        /// <summary>Whether a file's samples have been read already.</summary>
        public bool Ready(string file) => samples.ContainsKey(file);
    }
}
