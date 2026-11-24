using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.App
{
    /// <summary>
    /// The graphics settings of Ajustes (settingsPanel.ts graphicsSettings of the 0.10.1), kept on this computer: the
    /// screen (full, borderless on Windows, or a window) and its resolution (Arturo, 02/11 A5: Unity's player keeps them), the
    /// level (bajos, medios, altos: the project's quality levels, whose shadows, ambient occlusion, post-processing and
    /// render scale are Arte's), a cap on frames per second, the counter in a corner, the interface's size, and «Ajuste
    /// auto», the guard that takes the level down while a battle isn't fluid (QualityGuard). Applied as the app starts
    /// and whenever one changes.
    /// </summary>
    public static class GameSettings
    {
        const string LevelKey = "bastion.graphics";
        const string CapKey = "bastion.fpsCap";
        const string VSyncKey = "bastion.vsync";
        const string CounterKey = "bastion.showFps";
        const string TintKey = "bastion.squadTint";
        const string CinemaKey = "bastion.cinematic";
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
            // «Medios» unless chosen on the Mac and on iOS (Arturo, 7/11: «que vaya con los ajustes igual que en Mac»), «altos» elsewhere.
            get => LevelForTest ?? Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.IPhonePlayer ? 1 : 2), 0, 2);
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

        /// <summary>
        /// N1 (Arturo, 22/11: «he visto la imagen cortada horizontalmente»): the screen's sync, on by default and kept. Off
        /// for a test (BASTION_SIN_VSYNC=1) and for publicar.sh's measures (BASTION_MEDIR: with the sync the Mac's gpu in ms
        /// is its clock, not the cost, and the frames stop at the screen's), so they stay comparable; never on iOS, where
        /// it doesn't count and the frames go by the cap. On the Mac only while the screen is surely awake (ScreenWatch).
        /// </summary>
        public static bool VSync
        {
            // (BASTION_MEDIR_SINCRONIA=1: a measure with it, to see the sync and ScreenWatch at work.)
            get => Environment.GetEnvironmentVariable("BASTION_SIN_VSYNC") != "1"
                && (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BASTION_MEDIR")) || Environment.GetEnvironmentVariable("BASTION_MEDIR_SINCRONIA") == "1")
                && Application.platform != RuntimePlatform.IPhonePlayer && PlayerPrefs.GetInt(VSyncKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
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

        /// <summary>
        /// «Aspecto de película» (Arte's T7, 18/11: Bastion.Client.Cinematic, the grade, the haze and the blurred
        /// background; on by default). BASTION_SIN_CINE=1 turns it off for the labs' «before» without keeping it.
        /// </summary>
        public static bool Cinematic
        {
            get => Environment.GetEnvironmentVariable("BASTION_SIN_CINE") != "1" && PlayerPrefs.GetInt(CinemaKey, 1) == 1;
            set
            {
                if (Cinematic == value) return;
                PlayerPrefs.SetInt(CinemaKey, value ? 1 : 0);
                Bastion.Client.Cinematic.On = value;
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
        /// screen, but never past its density times HeightGrowth. The 0.10.1 drew in CSS pixels, which grow only with the
        /// density: on a big screen of ordinary density its panels stayed their size, and Unity's, grown with the screen,
        /// came out two thirds bigger (Ana, 28/10: «está todo muy grande»); at 2560 × 1440 the letters were then too small
        /// (Pruebas, 24/11): half the way back (cuadro 146). The map's lines (Bastion/Marks) take the same.
        /// </summary>
        public static float PixelScale(int width, int height) => Mathf.Min(Mathf.Sqrt(width / 1440f * height / 900f), Density * HeightGrowth(height));

        /// <summary>
        /// What a window taller than 900 points (its pixels over the density) adds over the density (cuadro 146, «Que crezca
        /// sola, media»): half of what its height grows past 900: 1080 → 1.1, 1440 → 1.3. At 900 or less, 1: there the HUD
        /// shrinks with the screen as before (1280 × 800 as it was).
        /// </summary>
        public static float HeightGrowth(int heightPixels) => Mathf.Max(1, (1 + heightPixels / Density / 900f) / 2);

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
            if (scaleLine != null && Time.realtimeSinceStartup - scaleLineAt > ScaleLineAfter)
            {
                Debug.Log(scaleLine);
                scaleLine = null;
            }
        }

        /// <summary>The last scale applied, for the log once the window has kept its size ScaleLineAfter seconds.</summary>
        static string scaleLine;
        static float scaleLineAt;
        const float ScaleLineAfter = 0.5f;

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
                    // The project's own (HudPanel: 1440 × 900, as PixelScale), not the asset's as found: in the editor a first
                    // test's scale stayed on it past a reload and was taken as its own, the scale applied twice (24/11).
                    references[panel] = own = Reference;
                    if (Application.isEditor) Application.quitting += () => panel.referenceResolution = own;
                }
                // The screen's own scale against the reference (ScaleWithScreenSize's, matched half and half), and the
                // reference that gives the one wanted instead.
                var screen = Mathf.Sqrt(on.x * (float)on.y / (own.x * (float)own.y));
                var wanted = Mathf.Min(screen, Density * HeightGrowth(on.y)) * size;
                // Said once the size stops changing (WatchScreen): dragging the window's edge set it each frame (the 0.14.4's
                // independent review, 2).
                scaleLine = FormattableString.Invariant($"[interfaz] {on.x}×{on.y}, densidad {Density:0.##}, pantalla {screen:0.00}, crece {HeightGrowth(on.y):0.00}, «Interfaz» {size:0.##} → {wanted:0.00}");
                scaleLineAt = Time.realtimeSinceStartup;
                var k = wanted > 0 ? screen / wanted : 1 / size;
                panel.referenceResolution = new Vector2Int(Mathf.RoundToInt(own.x * k), Mathf.RoundToInt(own.y * k));
            }
        }

        static readonly int PixelScaleId = Shader.PropertyToID("_BastionPixelScale");

        /// <summary>The reference the panels are made for (HudPanel.asset, ScaleWithScreenSize).</summary>
        static readonly Vector2Int Reference = new Vector2Int(1440, 900);

        /// <summary>Each panel's own reference resolution, as the project has it.</summary>
        static readonly System.Collections.Generic.Dictionary<PanelSettings, Vector2Int> references = new System.Collections.Generic.Dictionary<PanelSettings, Vector2Int>();

        /// <summary>For the labs' captures (BASTION_TINTE): the setting as if it were so, without keeping it on this computer.</summary>
        public static bool? TintForLab;

        /// <summary>«Ropa con el color del jugador» turned on or off (its new value).</summary>
        public static event Action<bool> SquadTintChanged;

        /// <summary>
        /// The level's quality level (by its name; while Arte's aren't there, the lowest, middle or highest there is), then
        /// the cap (Pacing). In the app only: in the editor the project's own level stays (changing it there would change it
        /// for everyone's captures).
        /// </summary>
        public static void Apply()
        {
            // How much lies about on the ground (0.10.1's COVER_DENSITY by its effects): fewer on slower machines, from the next map.
            Bastion.Client.Map.GroundCover.Density = ShownLevel == 0 ? 0.3f : ShownLevel == 1 ? 0.6f : 1f;
            // «Ropa con el color del jugador» for Arte's figures, in the battle and on the base.
            Bastion.Client.Units.SquadTint.On = SquadTint;
            // «Aspecto de película», Arte's (the mood shown now applied again if it changed).
            Bastion.Client.Cinematic.On = Cinematic;
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
            Pace();
            Changed?.Invoke();
        }

        /// <summary>
        /// The frames' sync and cap as things stand (Pacing): Ajustes' cap, in the menus or the battle, the screen's refresh,
        /// and the sync if it is on and the screen surely awake (ScreenWatch.Dozing, the Mac's). In the app only; again from
        /// ScreenWatch as the screen goes to sleep or wakes. SetQualityLevel puts its level's own back (0), so after it.
        /// </summary>
        public static void Pace()
        {
            if (Application.isEditor) return;
            var (sync, rate) = Pacing(FpsCap, inMenu, ScreenHz(), VSync && !ScreenWatch.Dozing);
            QualitySettings.vSyncCount = sync;
            Application.targetFrameRate = rate;
            ScreenCap = sync == 0 && FpsCap == 0 && !inMenu;
        }

        /// <summary>
        /// The frames' sync and cap for Unity (vSyncCount, targetFrameRate) by the cap chosen in Ajustes (0: none), out of
        /// the battle (`MenuFps` at most) or in it, with the screen's refresh in Hz, and whether the screen's sync goes:
        /// - with it (N1, 22/11: the picture cut across without it), a frame every so many refreshes as the cap asks, never
        ///   fewer frames than it (144 Hz and 60: every second, 72; none, every refresh); Unity leaves targetFrameRate be;
        /// - without it, or the screen not saying its refresh, a cap: Ajustes', else the screen's refresh (60 if it doesn't
        ///   say). Never none: the Mac's battle went so with the screen asleep, its presentation never coming (1/11,
        ///   0.11.3); iOS, where -1 is 30 fps (Arturo, 7/11: «no la capes de base a 30»), the screen's too (120 on the Duo).
        /// </summary>
        public static (int sync, int rate) Pacing(int cap, bool menu, int hz, bool vsync)
        {
            if (menu && (cap == 0 || cap > MenuFps)) cap = MenuFps;
            if (vsync && hz >= 30) return (cap > 0 ? Math.Max(1, hz / cap) : 1, -1);
            if (cap == 0) cap = hz >= 30 ? hz : 60;
            return (0, cap);
        }

        /// <summary>
        /// The ways the game can take the screen here (Arturo, 02/11 A5), by name: on the Mac its own full screen (a desktop of
        /// its own) and a window; on Windows the full screen to itself, the borderless one and a window.
        /// </summary>
        static (FullScreenMode mode, string name)[] Modes() => Application.platform == RuntimePlatform.WindowsPlayer
            ? new[] { (FullScreenMode.ExclusiveFullScreen, "Completa"), (FullScreenMode.FullScreenWindow, "Sin bordes"), (FullScreenMode.Windowed, "Ventana") }
            : new[] { (FullScreenMode.FullScreenWindow, "Completa"), (FullScreenMode.Windowed, "Ventana") };

        /// <summary>The screen's own size, in pixels.</summary>
        static Vector2Int NativeSize()
        {
            var w = Display.main.systemWidth;
            var h = Display.main.systemHeight;
            return w > 0 && h > 0 ? new Vector2Int(w, h) : new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        }

        /// <summary>
        /// The sizes to choose for `mode`, biggest first: the screen's own and the usual heights under it in its shape (none
        /// stretched); a window, only those smaller than the screen. At most four.
        /// </summary>
        static System.Collections.Generic.List<Vector2Int> Sizes(FullScreenMode mode)
        {
            var native = NativeSize();
            var sizes = new System.Collections.Generic.List<Vector2Int>();
            if (mode != FullScreenMode.Windowed) sizes.Add(native);
            foreach (var h in new[] { 1440, 1080, 900, 720 })
            {
                if (h >= native.y || sizes.Count >= 4) continue;
                var w = Mathf.RoundToInt(h * (float)native.x / native.y);
                sizes.Add(new Vector2Int(w - w % 2, h));
            }
            if (sizes.Count == 0) sizes.Add(native);
            return sizes;
        }

        /// <summary>
        /// Ajustes' sizes: Sizes for the screen as it is, and the size it has now if that isn't one of them (a window
        /// started or dragged to another), in its place by height, so the row marks what there is (Gameplay, 3/11: it
        /// marked the first). Once another is chosen it goes.
        /// </summary>
        static System.Collections.Generic.List<Vector2Int> ShownSizes()
        {
            var sizes = Sizes(ScreenMode);
            var now = ScreenSize;
            if (sizes.Contains(now)) return sizes;
            var at = sizes.FindIndex(z => z.y < now.y || (z.y == now.y && z.x < now.x));
            sizes.Insert(at < 0 ? sizes.Count : at, now);
            return sizes;
        }

        /// <summary>What was last asked for (the screen takes it a frame later): Ajustes shows it meanwhile.</summary>
        static FullScreenMode? askedMode;
        static Vector2Int? askedSize;

        static FullScreenMode ScreenMode => askedMode ?? Screen.fullScreenMode;
        static Vector2Int ScreenSize => askedSize ?? new Vector2Int(Screen.width, Screen.height);

        /// <summary>The screen taken so, at that size: in the app only (the editor's window is its own).</summary>
        static void SetScreen(FullScreenMode mode, Vector2Int size)
        {
            askedMode = mode;
            askedSize = size;
            if (Application.isEditor) return;
            Screen.SetResolution(size.x, size.y, mode);
        }

        /// <summary>The screen's refresh in whole Hz (0 if it doesn't say).</summary>
        static int ScreenHz() => Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);

        /// <summary>Whether the frames go at the screen's refresh as a cap, without its sync (the Mac in the battle with no cap): the telemetry says so.</summary>
        public static bool ScreenCap { get; private set; }

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
    
        public const string ScreenId = "screenMode";
        public const string ResolutionId = "resolution";
        public const string GraphicsId = "graphics";
        public const string CapId = "fpsCap";
        public const string VSyncId = "vsync";
        public const string CounterId = "showFps";
        public const string TintId = "squadTint";
        public const string CinemaId = "cinematic";
        public const string InterfaceId = "uiScale";
        public const string AutoId = "autoQuality";
        public const string RouterId = "routerPort";

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
            // (A row not for this machine is null: the sync's on iOS.)
            rows.AddRange(Graphics().FindAll(r => r != null));
            if (battle != null) rows.AddRange(battle);
            rows.AddRange(Looks());
            rows.Add(Auto());
            rows.Add(Router());
            return rows;
        }

        /// <summary>
        /// Whether hosting opens the game's port on the router, to be joined from the internet (Gameplay's review, 10/11,
        /// #10: Red's NetLink.RouterWanted, kept in conexion.json; on unless turned off, as before). Turned while hosting, the
        /// port opens or closes at once. Without NetLink (the labs), as it starts: on.
        /// </summary>
        static Hud.SettingView Router() => new Hud.SettingView
        {
            Id = RouterId,
            Label = RouterText.Label,
            Hint = RouterText.Hint,
            About = RouterText.About,
            Choices = new System.Collections.Generic.List<string> { "Sí", "No" },
            ChoiceTips = new System.Collections.Generic.List<string> { RouterText.YesTip, RouterText.NoTip },
            On = Bastion.Client.Net.NetLink.Instance == null || Bastion.Client.Net.NetLink.Instance.RouterWanted ? 0 : 1,
            Tail = true,
        };

        /// <summary>
        /// What a row does in a line, under its name, where the name alone didn't say it (Producto's review, 23/11, B7);
        /// the cap's, only with the sync on (and a cap chosen). Lore's, 23/11.
        /// </summary>
        static class Hints
        {
            public const string Auto = "Baja los gráficos si el combate no va fluido.";
            public const string Interface = "El tamaño de los textos y los paneles.";
            public const string Cinema = "Más contraste y más bruma a lo lejos.";
            public const string Sync = "Que la imagen no se parta al mover la cámara.";
            public const string CapSynced = "Con la sincronía, el límite se redondea hacia arriba según la pantalla: con 60 en una de 144 Hz, va a 72.";
        }

        /// <summary>The router's row's words (Lore, .dev-snapshots/lore/0.12.17/TEXTOS.md §5).</summary>
        public static class RouterText
        {
            public const string Label = "Abrir el puerto del router";
            public const string About = "Al alojar una partida, el juego abre su puerto en el router. Así, quien juegue contigo puede entrar por internet con la dirección y la clave. Con el puerto abierto, quien entra por primera vez desde tu red también necesita la clave. Con «No», el juego no toca el router: se entra desde tu red o con Tailscale.";
            /// <summary>Under its name (B7; provisional, Lore reviews the five).</summary>
            public const string Hint = "Para jugar por internet sin Tailscale.";
            public const string YesTip = "Se puede entrar por internet";
            public const string NoTip = "Desde tu red o con Tailscale";
        }

        /// <summary>The volumes' names and help (settingsPanel.ts soundSettings), by Red's keys (Bastion.Client.Audio.Volumes).</summary>
        static readonly (string key, string label, string about)[] VolumeRows =
        {
            (Bastion.Client.Audio.Volumes.Master, "Volumen", "Todo el sonido del juego."),
            (Bastion.Client.Audio.Volumes.Sfx, "Efectos", "Disparos, explosiones, pasos y acciones."),
            (Bastion.Client.Audio.Volumes.Music, "Música", "La música de la base, del mapa y del combate."),
            (Bastion.Client.Audio.Volumes.Ambience, "Ambiente", "Viento, ciudad, bosque, máquinas."),
            (Bastion.Client.Audio.Volumes.Ui, "Botones y avisos", "Los sonidos de los botones y de los avisos."),
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
                    About = "Todo el sonido del juego, encendido o en silencio.",
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
            // The screen and its resolution first (Arturo, 02/11 A5).
            new Hud.SettingView
            {
                Id = ScreenId,
                Label = "Pantalla",
                About = Application.platform == RuntimePlatform.WindowsPlayer
                    ? "Completa: el juego ocupa toda la pantalla. Sin bordes: también, pero como una ventana, y se cambia antes a otro programa. Ventana: del tamaño de la resolución."
                    : "Completa: el juego ocupa toda la pantalla, en un escritorio propio. Ventana: del tamaño de la resolución.",
                Choices = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(Modes(), m => m.name)),
                On = Math.Max(0, Array.FindIndex(Modes(), m => m.mode == ScreenMode)),
            },
            new Hud.SettingView
            {
                Id = ResolutionId,
                Label = "Resolución",
                About = "Cuántos puntos tiene la imagen. Con menos va más fluido y se ve menos nítido; la interfaz guarda su tamaño.",
                Choices = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(ShownSizes(), z => $"{z.x} × {z.y}")),
                ChoiceTips = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(ShownSizes(), z => z == NativeSize() ? "La de la pantalla" : Sizes(ScreenMode).Contains(z) ? "" : "La de ahora")),
                On = Math.Max(0, ShownSizes().FindIndex(z => z == ScreenSize)),
            },
            // The level, high to low as the 0.10.1's «Efectos» (the project's quality levels, Arte's).
            new Hud.SettingView
            {
                Id = GraphicsId,
                Label = "Gráficos",
                About = "Sombras, oclusión ambiental, posprocesado y resolución de la escena. «Altos» se ve mejor y es lo que más cuesta dibujar. Cambia al momento.",
                Choices = new System.Collections.Generic.List<string> { "Altos", "Medios", "Bajos" },
                On = 2 - Level,
            },
            // Arte's look (T7, 18/11), Lore's words; «Sí» first as «Mostrar FPS».
            new Hud.SettingView
            {
                Id = CinemaId,
                Label = "Aspecto de película",
                Hint = Hints.Cinema,
                About = "Más contraste y colores algo más oscuros, y más bruma a lo lejos de día. No cambia las reglas ni lo que se ve del combate.",
                Choices = new System.Collections.Generic.List<string> { "Sí", "No" },
                On = Cinematic ? 0 : 1,
            },
            new Hud.SettingView
            {
                Id = CapId,
                Label = "Límite de FPS",
                // With the sync on it still counts, as a frame every so many refreshes (Pacing): said, not greyed (B7); only
                // with a cap chosen (Lore), and following both rows as they are changed (Producto, 23/11: it didn't).
                Hint = Application.platform != RuntimePlatform.IPhonePlayer ? Hints.CapSynced : null,
                HintWith = VSyncId,
                HintWithOn = 0,
                HintNotOn = Array.IndexOf(Caps, 0),
                About = "Menos imágenes por segundo: menos trabajo para la tarjeta gráfica y la batería.",
                Choices = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(Caps, c => c == 0 ? "Sin límite" : c.ToString())),
                On = Math.Max(0, Array.IndexOf(Caps, FpsCap)),
            },
            // N1 (22/11): the screen's sync, «Sí» first as «Mostrar FPS»; not on iOS, where it doesn't count.
            Application.platform == RuntimePlatform.IPhonePlayer ? null : new Hud.SettingView
            {
                Id = VSyncId,
                Label = "Sincronía vertical",
                Hint = Hints.Sync,
                About = "Que la imagen no se vea partida en horizontal al mover la cámara. No da más imágenes por segundo que la pantalla, y con un límite de FPS puede pasarlo un poco.",
                Choices = new System.Collections.Generic.List<string> { "Sí", "No" },
                On = VSync ? 0 : 1,
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
                Hint = Hints.Interface,
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
            Hint = Hints.Auto,
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
            if (id == ScreenId)
            {
                var mode = Modes()[Mathf.Clamp((int)value, 0, Modes().Length - 1)].mode;
                // The size kept if this way has it; a window the size under the screen's, else the screen's own.
                var sizes = Sizes(mode);
                SetScreen(mode, sizes.Contains(ScreenSize) ? ScreenSize : sizes[0]);
            }
            else if (id == ResolutionId)
            {
                var sizes = ShownSizes();
                SetScreen(ScreenMode, sizes[Mathf.Clamp((int)value, 0, sizes.Count - 1)]);
            }
            else if (id == GraphicsId) Level = 2 - (int)value;
            else if (id == CapId) FpsCap = Caps[Mathf.Clamp((int)value, 0, Caps.Length - 1)];
            else if (id == VSyncId) VSync = value == 0;
            else if (id == CounterId) ShowFps = value == 0;
            else if (id == TintId) SquadTint = value == 1;
            else if (id == CinemaId) Cinematic = value == 0;
            else if (id == AutoId) AutoQuality = value == 0;
            else if (id == InterfaceId) InterfaceSize = InterfaceSizes[Mathf.Clamp((int)value, 0, InterfaceSizes.Length - 1)];
            else if (id == RouterId)
            {
                var link = Bastion.Client.Net.NetLink.Instance;
                if (link != null) link.RouterWanted = value == 0;
            }
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
