using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Audio
{
    /// <summary>
    /// The sound's volumes and mute (audio/engine.ts Volumes, toggleMute): five, from 0 to 1, kept on this computer, and
    /// heard at once (Ajustes → Sonido, the M key). The radio follows «Interfaz y radio».
    /// </summary>
    public static class Volumes
    {
        public const string Master = "master";
        public const string Sfx = "sfx";
        public const string Music = "music";
        public const string Ambience = "ambience";
        public const string Ui = "ui";

        /// <summary>Their order in Ajustes: Volumen, Efectos, Música, Ambiente, Interfaz y radio.</summary>
        public static readonly IReadOnlyList<string> All = new[] { Master, Sfx, Music, Ambience, Ui };

        /// <summary>engine.ts DEFAULTS.</summary>
        private static readonly Dictionary<string, float> Defaults = new Dictionary<string, float> { [Master] = 0.8f, [Sfx] = 0.8f, [Music] = 0.5f, [Ambience] = 0.6f, [Ui] = 0.7f };

        private const string MuteKey = "sonido.silencio";

        /// <summary>A volume or the mute changed (the settings redraw, the sound applies it).</summary>
        public static event Action? Changed;

        /// <summary>A volume, 0 to 1 (its default if never set).</summary>
        public static float Get(string key)
        {
            if (!Defaults.TryGetValue(key, out var fallback)) throw new ArgumentException($"No hay volumen «{key}»");
            return Mathf.Clamp01(PlayerPrefs.GetFloat(Key(key), fallback));
        }

        /// <summary>Sets a volume (0 to 1), kept, and heard at once.</summary>
        public static void Set(string key, float value)
        {
            if (!Defaults.ContainsKey(key)) throw new ArgumentException($"No hay volumen «{key}»");
            PlayerPrefs.SetFloat(Key(key), Mathf.Clamp01(value));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>All the sound silenced (the M key), kept like the volumes.</summary>
        public static bool Muted => PlayerPrefs.GetInt(MuteKey, 0) == 1;

        /// <summary>Silences everything, or brings it back; returns whether it is now silenced.</summary>
        public static bool ToggleMute()
        {
            var muted = !Muted;
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return muted;
        }

        private static string Key(string key) => $"sonido.volumen.{key}";
    }
}
