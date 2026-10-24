using System;
using UnityEngine;

namespace Bastion.UI.App
{
    /// <summary>
    /// The graphics settings of Ajustes (settingsPanel.ts graphicsSettings of the 0.10.1), kept on this computer: the
    /// level (bajos, medios, altos: the project's quality levels, whose shadows, ambient occlusion, post-processing and
    /// render scale are Arte's), a cap on frames per second, and the counter in a corner. Applied as the app starts and
    /// whenever one changes.
    /// </summary>
    public static class GameSettings
    {
        const string LevelKey = "bastion.graphics";
        const string CapKey = "bastion.fpsCap";
        const string CounterKey = "bastion.showFps";

        /// <summary>The levels, low to high, and the quality levels they look for by name (Arte's: Bajo, Medio, Alto).</summary>
        public static readonly string[] LevelNames = { "bajos", "medios", "altos" };
        static readonly string[] QualityNames = { "Bajo", "Medio", "Alto" };

        /// <summary>The caps there are (0: none but the screen's own refresh).</summary>
        public static readonly int[] Caps = { 0, 120, 60, 30 };

        /// <summary>Out of the battle (the start screen, the room) at most this many frames a second: nothing there moves faster.</summary>
        public const int MenuFps = 60;

        static bool inMenu = true;

        /// <summary>
        /// Whether the app is out of the battle (AppFlow says so as scenes change). There the frames go at most at
        /// `MenuFps` (24/10: the menu ran at the screen's 144 Hz for a still picture).
        /// </summary>
        public static bool InMenu
        {
            get => inMenu;
            set
            {
                if (inMenu == value) return;
                inMenu = value;
                Apply();
            }
        }

        /// <summary>Something changed: the counter shows or goes, the telemetry says the new settings.</summary>
        public static event Action Changed;

        /// <summary>0 bajos, 1 medios, 2 altos. Medios on a Mac (its target, ≥30 fps), altos elsewhere (the 0.10.1's).</summary>
        public static int Level
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor ? 1 : 2), 0, 2);
            set
            {
                PlayerPrefs.SetInt(LevelKey, Mathf.Clamp(value, 0, 2));
                Apply();
            }
        }

        public static int FpsCap
        {
            get => PlayerPrefs.GetInt(CapKey, 0);
            set
            {
                PlayerPrefs.SetInt(CapKey, Array.IndexOf(Caps, value) >= 0 ? value : 0);
                Apply();
            }
        }

        public static bool ShowFps
        {
            get => PlayerPrefs.GetInt(CounterKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(CounterKey, value ? 1 : 0);
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// The level's quality level (by its name; while Arte's aren't there, the lowest, middle or highest there is), then
        /// the cap: none, the screen's refresh (vsync); a cap, that many frames and no vsync; out of the battle, `MenuFps`
        /// at most. In the app only: in the editor
        /// the project's own level stays (changing it there would change it for everyone's captures).
        /// </summary>
        public static void Apply()
        {
            // How much lies about on the ground (0.10.1's COVER_DENSITY by its effects): fewer on slower machines, from the next map.
            Bastion.Client.Map.GroundCover.Density = Level == 0 ? 0.3f : Level == 1 ? 0.6f : 1f;
            if (Application.isEditor)
            {
                Changed?.Invoke();
                return;
            }
            var names = QualitySettings.names;
            var level = Level;
            var quality = Array.FindIndex(names, n => string.Equals(n, QualityNames[level], StringComparison.OrdinalIgnoreCase));
            if (quality < 0 && names.Length > 0) quality = level == 0 ? 0 : level == 2 ? names.Length - 1 : (names.Length - 1) / 2;
            if (quality >= 0 && quality != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(quality, true);
            var cap = FpsCap;
            if (inMenu && (cap == 0 || cap > MenuFps)) cap = MenuFps;
            QualitySettings.vSyncCount = cap == 0 ? 1 : 0;
            Application.targetFrameRate = cap == 0 ? -1 : cap;
            Changed?.Invoke();
        }

        /// <summary>The settings in short, for the telemetry: «gráficos medios», and a cap if there is one.</summary>
        public static string Summary()
        {
            var cap = FpsCap;
            var quality = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            var own = quality >= 0 && quality < names.Length ? names[quality] : "?";
            return $"gráficos {LevelNames[Level]} ({own})" + (cap > 0 ? $", límite {cap} fps" : "");
        }
    }
}
