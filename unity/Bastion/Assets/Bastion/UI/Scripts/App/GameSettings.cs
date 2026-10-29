using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.App
{
    /// <summary>
    /// The graphics settings of Ajustes (settingsPanel.ts graphicsSettings of the 0.10.1), kept on this computer: the
    /// level (bajos, medios, altos: the project's quality levels, whose shadows, ambient occlusion, post-processing and
    /// render scale are Arte's), a cap on frames per second, the counter in a corner, the interface's size, and «Ajuste
    /// auto», the guard that takes the level down while a battle isn't fluid (QualityGuard). Applied as the app starts
    /// and whenever one changes.
    /// </summary>
    public static class GameSettings
    {
        const string LevelKey = "bastion.graphics";
        const string CapKey = "bastion.fpsCap";
        const string CounterKey = "bastion.showFps";
        const string TintKey = "bastion.squadTint";
        const string InterfaceKey = "bastion.uiScale";
        const string AutoKey = "bastion.autoQuality";
        const string AutoTakenKey = "bastion.autoTaken";

        /// <summary>The levels, low to high, and the quality levels they look for by name (Arte's: Bajo, Medio, Alto).</summary>
        public static readonly string[] LevelNames = { "bajos", "medios", "altos" };
        static readonly string[] QualityNames = { "Bajo", "Medio", "Alto" };

        /// <summary>The interface's sizes there are (settings.ts uiScale).</summary>
        public static readonly float[] InterfaceSizes = { 0.9f, 1f, 1.15f, 1.3f };

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

        /// <summary>
        /// For measuring (Measure, medir.bat: BASTION_GRAFICOS=bajos|medios|altos): the level as if it were so, never kept
        /// on this computer, whose player's own settings stay as they are (as TintForLab). Unset, none.
        /// </summary>
        static readonly int? LevelForTest = Array.IndexOf(LevelNames, Environment.GetEnvironmentVariable("BASTION_GRAFICOS")) is var i && i >= 0 ? i : (int?)null;

        /// <summary>0 bajos, 1 medios, 2 altos. Medios on a Mac (its target, ≥30 fps), altos elsewhere (the 0.10.1's).</summary>
        public static int Level
        {
            get => LevelForTest ?? Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor ? 1 : 2), 0, 2);
            set
            {
                if (LevelForTest == null) PlayerPrefs.SetInt(LevelKey, Mathf.Clamp(value, 0, 2));
                // What the player picks is theirs: the guard steps back from it (settings.ts setGraphics).
                AutoTaken = 0;
                Apply();
            }
        }

        /// <summary>
        /// The lab's slow frames (BASTION_LENTO=ms, CombatLab): the guard watches them with the editor out of focus, and
        /// «Ajuste auto» keeps nothing on this computer (its player's own stays as it is).
        /// </summary>
        public static readonly int SlowForTest = int.TryParse(Environment.GetEnvironmentVariable("BASTION_LENTO"), out var ms) && ms > 0 ? ms : 0;

        static bool? autoOn;
        static int? autoTaken;

        /// <summary>
        /// «Ajuste auto» (settings.ts autoQuality): the guard may take the level down, on by default. Off, it gives back
        /// what it took.
        /// </summary>
        public static bool AutoQuality
        {
            get => autoOn ??= SlowForTest > 0 || PlayerPrefs.GetInt(AutoKey, 1) == 1;
            set
            {
                autoOn = value;
                if (SlowForTest == 0) PlayerPrefs.SetInt(AutoKey, value ? 1 : 0);
                if (!value) AutoTaken = 0;
                Apply();
            }
        }

        /// <summary>Levels the guard has taken off on this computer, kept between battles: a slow one starts the next already lighter.</summary>
        static int AutoTaken
        {
            get => autoTaken ??= SlowForTest > 0 ? 0 : Mathf.Clamp(PlayerPrefs.GetInt(AutoTakenKey, 0), 0, 2);
            set
            {
                autoTaken = value;
                if (SlowForTest == 0) PlayerPrefs.SetInt(AutoTakenKey, value);
            }
        }

        /// <summary>The player picked the level in Ajustes (or a test did): the guard leaves it alone.</summary>
        static bool LevelChosen => SlowForTest == 0 && (LevelForTest != null || PlayerPrefs.HasKey(LevelKey));

        /// <summary>The level drawn: the chosen one, less what the guard took.</summary>
        public static int ShownLevel => AutoQuality && !LevelChosen ? Math.Max(0, Level - AutoTaken) : Level;

        /// <summary>Takes the level down one (QualityGuard); false when there is nothing left to take, or it isn't the guard's to.</summary>
        public static bool LowerQuality()
        {
            if (!AutoQuality || LevelChosen || ShownLevel == 0) return false;
            AutoTaken++;
            Apply();
            return true;
        }

        /// <summary>What the guard has lowered, in Ajustes' words (settings.ts autoQualityNote), or null if nothing.</summary>
        public static string AutoNote() => ShownLevel < Level ? $"Bajado para ir fluido: gráficos {LevelNames[ShownLevel]}." : null;

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
        /// «Ropa con el color del jugador» (settings.ts squadTint; off by default, Arturo 21/10): the soldiers' clothes in
        /// Personalizar's colours and the band of their player's colour on the arm. Arte's figures read it, in the battle
        /// and on the base, and hear `SquadTintChanged`.
        /// </summary>
        public static bool SquadTint
        {
            get => TintForLab ?? PlayerPrefs.GetInt(TintKey, 0) == 1;
            set
            {
                if (SquadTint == value) return;
                PlayerPrefs.SetInt(TintKey, value ? 1 : 0);
                // Arte's figures (Client can't see Ajustes): painted again at once.
                Bastion.Client.Units.SquadTint.On = value;
                SquadTintChanged?.Invoke(value);
                Changed?.Invoke();
            }
        }

        /// <summary>For a capture (BASTION_INTERFAZ=1.3): the size as if it were so, never kept; and in the editor, the only way it applies.</summary>
        static readonly float? InterfaceForTest = float.TryParse(Environment.GetEnvironmentVariable("BASTION_INTERFAZ"), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && Array.IndexOf(InterfaceSizes, f) >= 0 ? f : (float?)null;

        /// <summary>«Interfaz» (settings.ts uiScale): how big the texts and panels are, 90 to 130 %; the 3D view stays.</summary>
        public static float InterfaceSize
        {
            get => InterfaceForTest ?? (Array.IndexOf(InterfaceSizes, PlayerPrefs.GetFloat(InterfaceKey, 1)) is var i && i >= 0 ? InterfaceSizes[i] : 1);
            set
            {
                if (InterfaceForTest == null) PlayerPrefs.SetFloat(InterfaceKey, Array.IndexOf(InterfaceSizes, value) >= 0 ? value : 1);
                ApplyInterface();
                Changed?.Invoke();
            }
        }

        /// <summary>For a test (BASTION_DENSIDAD=1|1.5|2): the screen's density as if it were so.</summary>
        static readonly float? DensityForTest = float.TryParse(Environment.GetEnvironmentVariable("BASTION_DENSIDAD"), NumberStyles.Float, CultureInfo.InvariantCulture, out var dd) && dd > 0 ? dd : (float?)null;

        /// <summary>
        /// The screen's density, as the system's own scale would have it (a browser's device pixels per CSS pixel): 2 on a
        /// Retina screen, 1.5 on a 4K of 27", 1 on an ordinary one (by its dots per inch; none known, 1).
        /// </summary>
        public static float Density
        {
            get
            {
                if (DensityForTest is float test) return test;
                var dpi = Screen.dpi;
                return dpi >= 180 ? 2 : dpi >= 140 ? 1.5f : 1;
            }
        }

        /// <summary>
        /// How much bigger than at 1440 × 900 the HUD's pixels are on this screen (before Ajustes' «Interfaz»): with the
        /// screen, but never past its density. The 0.10.1 drew in CSS pixels, which grow only with the density: on a big
        /// screen of ordinary density its panels stayed their size, and Unity's, grown with the screen, came out two
        /// thirds bigger (Ana, 28/10: «está todo muy grande»). The map's lines (Bastion/Marks) take the same.
        /// </summary>
        public static float PixelScale(int width, int height) => Mathf.Min(Mathf.Sqrt(width / 1440f * height / 900f), Density);

        static Vector2Int appliedFor;

        /// <summary>What the panels draw on: the screen, or the texture a capture gives them (Shots: not the screen's size).</summary>
        static Vector2Int DrawnOn()
        {
            foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                // (Unity's own null test: an unassigned field is a «fake null» that `is` lets through.)
                var t = doc.panelSettings != null ? doc.panelSettings.targetTexture : null;
                if (t != null) return new Vector2Int(t.width, t.height);
            }
            return new Vector2Int(Screen.width, Screen.height);
        }

        /// <summary>Each frame (AppFlow, the labs): the window's size changed, the panels' and the lines' scale again.</summary>
        public static void WatchScreen()
        {
            if (DrawnOn() != appliedFor) ApplyInterface();
        }

        /// <summary>
        /// The size on the panels the screens draw on (the UIDocuments there are: AppFlow's, whose panel every screen
        /// shares). They scale with the screen (ScaleWithScreenSize, 1440 × 900, matched half and half), so their
        /// reference resolution is set to give PixelScale times «Interfaz»: at 130 % a 1440 × 900 screen is drawn as if it
        /// were 1108 × 692 (a panel's `scale` counts only at a constant pixel size). In the editor only for a test
        /// (BASTION_INTERFAZ, BASTION_DENSIDAD): the panel is the project's asset, everyone's captures', and it is put back
        /// as play stops.
        /// </summary>
        public static void ApplyInterface()
        {
            var on = appliedFor = DrawnOn();
            if (Application.isEditor && InterfaceForTest == null && DensityForTest == null) return;
            Shader.SetGlobalFloat(PixelScaleId, PixelScale(on.x, on.y));
            var size = InterfaceSize;
            foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var panel = doc.panelSettings;
                if (panel == null) continue;
                if (!references.TryGetValue(panel, out var own))
                {
                    references[panel] = own = panel.referenceResolution;
                    if (Application.isEditor) Application.quitting += () => panel.referenceResolution = own;
                }
                // The screen's own scale against the reference (ScaleWithScreenSize's, matched half and half), and the
                // reference that gives the one wanted instead.
                var screen = Mathf.Sqrt(on.x * (float)on.y / (own.x * (float)own.y));
                var wanted = Mathf.Min(screen, Density) * size;
                var k = wanted > 0 ? screen / wanted : 1 / size;
                panel.referenceResolution = new Vector2Int(Mathf.RoundToInt(own.x * k), Mathf.RoundToInt(own.y * k));
            }
        }

        static readonly int PixelScaleId = Shader.PropertyToID("_BastionPixelScale");

        /// <summary>Each panel's own reference resolution, as the project has it.</summary>
        static readonly System.Collections.Generic.Dictionary<PanelSettings, Vector2Int> references = new System.Collections.Generic.Dictionary<PanelSettings, Vector2Int>();

        /// <summary>For the labs' captures (BASTION_TINTE): the setting as if it were so, without keeping it on this computer.</summary>
        public static bool? TintForLab;

        /// <summary>«Ropa con el color del jugador» turned on or off (its new value).</summary>
        public static event Action<bool> SquadTintChanged;

        /// <summary>
        /// The level's quality level (by its name; while Arte's aren't there, the lowest, middle or highest there is), then
        /// the cap: none, the screen's refresh (vsync); a cap, that many frames and no vsync; out of the battle, `MenuFps`
        /// at most. In the app only: in the editor
        /// the project's own level stays (changing it there would change it for everyone's captures).
        /// </summary>
        public static void Apply()
        {
            // How much lies about on the ground (0.10.1's COVER_DENSITY by its effects): fewer on slower machines, from the next map.
            Bastion.Client.Map.GroundCover.Density = ShownLevel == 0 ? 0.3f : ShownLevel == 1 ? 0.6f : 1f;
            // «Ropa con el color del jugador» for Arte's figures, in the battle and on the base.
            Bastion.Client.Units.SquadTint.On = SquadTint;
            ApplyInterface();
            if (Application.isEditor)
            {
                Changed?.Invoke();
                return;
            }
            var names = QualitySettings.names;
            var level = ShownLevel;
            var quality = Array.FindIndex(names, n => string.Equals(n, QualityNames[level], StringComparison.OrdinalIgnoreCase));
            if (quality < 0 && names.Length > 0) quality = level == 0 ? 0 : level == 2 ? names.Length - 1 : (names.Length - 1) / 2;
            if (quality >= 0 && quality != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(quality, true);
            var cap = FpsCap;
            if (inMenu && (cap == 0 || cap > MenuFps)) cap = MenuFps;
            QualitySettings.vSyncCount = cap == 0 ? 1 : 0;
            Application.targetFrameRate = cap == 0 ? -1 : cap;
            Changed?.Invoke();
        }

        /// <summary>
        /// The settings in short, for the telemetry: «gráficos medios», what the guard took («auto: bajos») or that it is
        /// off, and a cap if there is one.
        /// </summary>
        public static string Summary()
        {
            var cap = FpsCap;
            var quality = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            var own = quality >= 0 && quality < names.Length ? names[quality] : "?";
            var auto = !AutoQuality ? ", sin ajuste auto" : ShownLevel < Level ? $", auto: {LevelNames[ShownLevel]}" : "";
            return $"gráficos {LevelNames[Level]}{auto} ({own})" + (cap > 0 ? $", límite {cap} fps" : "");
        }
    
        public const string GraphicsId = "graphics";
        public const string CapId = "fpsCap";
        public const string CounterId = "showFps";
        public const string TintId = "squadTint";
        public const string InterfaceId = "uiScale";
        public const string AutoId = "autoQuality";

        public const string SoundId = "sound";
        const string VolumePrefix = "volume.";

        /// <summary>
        /// Ajustes' rows (hud.ts settings: the sound, soundSettings, graphicsSettings), for the battle's menu and the
        /// base's: the sound and its volumes, the graphics, then the battle's own if any (`battle`: the speeds and the
        /// plans), then the clothes, the interface's size and «Ajuste auto», in the 0.10.1's order.
        /// </summary>
        public static System.Collections.Generic.List<Hud.SettingView> Views(System.Collections.Generic.IEnumerable<Hud.SettingView> battle = null)
        {
            var rows = Sound();
            rows.AddRange(Graphics());
            if (battle != null) rows.AddRange(battle);
            rows.AddRange(Looks());
            rows.Add(Auto());
            return rows;
        }

        /// <summary>The volumes' names and help (settingsPanel.ts soundSettings), by Red's keys (Bastion.Client.Audio.Volumes).</summary>
        static readonly (string key, string label, string about)[] VolumeRows =
        {
            (Bastion.Client.Audio.Volumes.Master, "Volumen", "Todo el sonido del juego. La tecla M lo silencia en combate."),
            (Bastion.Client.Audio.Volumes.Sfx, "Efectos", "Disparos, explosiones, pasos y acciones."),
            (Bastion.Client.Audio.Volumes.Music, "Música", "La música de la base, del mapa y del combate."),
            (Bastion.Client.Audio.Volumes.Ambience, "Ambiente", "Viento, ciudad, bosque, máquinas."),
            (Bastion.Client.Audio.Volumes.Ui, "Interfaz y radio", "Botones, avisos y las transmisiones de radio."),
        };

        /// <summary>
        /// The sound on or off (hud.ts «Sonido: activado», the M key's), then each volume from 0 to 100 % in steps of one
        /// (Ana, 22/10 #25: «si quiero ponerlo al 1 %»), heard as it moves.
        /// </summary>
        static System.Collections.Generic.List<Hud.SettingView> Sound()
        {
            var rows = new System.Collections.Generic.List<Hud.SettingView>
            {
                new Hud.SettingView
                {
                    Id = SoundId,
                    Label = "Sonido",
                    About = "Todo el sonido del juego, encendido o en silencio. En combate, la tecla M hace lo mismo.",
                    Choices = new System.Collections.Generic.List<string> { "Activado", "Desactivado" },
                    On = Bastion.Client.Audio.Volumes.Muted ? 1 : 0,
                    Sound = true,
                },
            };
            foreach (var (key, label, about) in VolumeRows)
                rows.Add(new Hud.SettingView
                {
                    Id = VolumePrefix + key,
                    Label = label,
                    About = about,
                    Slider = true,
                    Min = 0,
                    Max = 100,
                    Step = 1,
                    Value = Mathf.Round(Bastion.Client.Audio.Volumes.Get(key) * 100),
                    Percent = true,
                    Sound = true,
                });
            return rows;
        }

        static System.Collections.Generic.List<Hud.SettingView> Graphics() => new System.Collections.Generic.List<Hud.SettingView>
        {
            // The level, high to low as the 0.10.1's «Efectos» (the project's quality levels, Arte's).
            new Hud.SettingView
            {
                Id = GraphicsId,
                Label = "Gráficos",
                About = "Sombras, oclusión ambiental, posprocesado y resolución de la escena. «Altos» se ve mejor y es lo que más cuesta dibujar. Cambia al momento.",
                Choices = new System.Collections.Generic.List<string> { "Altos", "Medios", "Bajos" },
                On = 2 - Level,
            },
            new Hud.SettingView
            {
                Id = CapId,
                Label = "Límite de FPS",
                About = "Menos imágenes por segundo: menos trabajo para la tarjeta gráfica y la batería.",
                Choices = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(Caps, c => c == 0 ? "Sin límite" : c.ToString())),
                On = Math.Max(0, Array.IndexOf(Caps, FpsCap)),
            },
            new Hud.SettingView
            {
                Id = CounterId,
                Label = "Mostrar FPS",
                About = "Un contador de imágenes por segundo en una esquina.",
                Choices = new System.Collections.Generic.List<string> { "Sí", "No" },
                On = ShowFps ? 0 : 1,
            },
        };

        static System.Collections.Generic.List<Hud.SettingView> Looks() => new System.Collections.Generic.List<Hud.SettingView>
        {
            // settingsPanel.ts: No first, as the 0.10.1.
            new Hud.SettingView
            {
                Id = TintId,
                Label = "Ropa con el color del jugador",
                About = "Tiñe la ropa de los soldados con los colores de Personalizar y les pone el brazalete del color de su jugador.",
                Choices = new System.Collections.Generic.List<string> { "No", "Sí" },
                On = SquadTint ? 1 : 0,
                Tail = true,
            },
            new Hud.SettingView
            {
                Id = InterfaceId,
                Label = "Interfaz",
                About = "El tamaño de los textos y paneles del juego. La vista 3D no cambia.",
                Choices = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(InterfaceSizes, k => $"{Mathf.RoundToInt(k * 100)} %")),
                On = Math.Max(0, Array.IndexOf(InterfaceSizes, InterfaceSize)),
                Tail = true,
            },
        };

        /// <summary>The frame-rate guard (settingsPanel.ts autoQualityRow): on or off, and what it has lowered on this computer.</summary>
        static Hud.SettingView Auto() => new Hud.SettingView
        {
            Id = AutoId,
            Label = "Ajuste auto",
            About = "Si el combate va a menos de 45 imágenes por segundo, baja los gráficos un nivel cada vez. Si eliges tú los gráficos, no los toca.",
            Choices = new System.Collections.Generic.List<string> { "Sí", "No" },
            ChoiceTips = new System.Collections.Generic.List<string> { "Baja la calidad si el combate no va fluido", "Siempre la calidad elegida" },
            On = AutoQuality ? 0 : 1,
            Note = AutoNote(),
            NoteGoesWith = new System.Collections.Generic.List<string> { AutoId, GraphicsId },
            Tail = true,
        };

        /// <summary>One of those rows changed (its id, the choice's place): kept and applied; false if it isn't one of them.</summary>
        public static bool Change(string id, float value)
        {
            if (id == GraphicsId) Level = 2 - (int)value;
            else if (id == CapId) FpsCap = Caps[Mathf.Clamp((int)value, 0, Caps.Length - 1)];
            else if (id == CounterId) ShowFps = value == 0;
            else if (id == TintId) SquadTint = value == 1;
            else if (id == AutoId) AutoQuality = value == 0;
            else if (id == InterfaceId) InterfaceSize = InterfaceSizes[Mathf.Clamp((int)value, 0, InterfaceSizes.Length - 1)];
            else if (id == SoundId)
            {
                if ((value == 1) != Bastion.Client.Audio.Volumes.Muted) Bastion.Client.Audio.Volumes.ToggleMute();
            }
            else if (id.StartsWith(VolumePrefix, StringComparison.Ordinal)) Bastion.Client.Audio.Volumes.Set(id.Substring(VolumePrefix.Length), Mathf.Clamp01(value / 100f));
            else return false;
            return true;
        }
    }
}
