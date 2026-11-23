using Bastion.Client.Net;
using Bastion.Engine;
using Bastion.Net;
using Bastion.UI.Hud;
using Bastion.UI.Start;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Bastion.UI.App
{
    /// <summary>
    /// What the app shows, from Red's NetLink (docs/UNITY-UI.md; UI's, Gameplay 23/10). Out of a room, the start screen;
    /// in one, its menu while it is in the lobby, Arte's scene «Base» with the base's screens while it is on the
    /// campaign's base (BaseSession), and the battle's scene while it is in a mission. It reads the room
    /// (NetLink.LastRoom) and never listens to NetLink.Message, which waits for the battle with its snapshot.
    ///
    /// Over any scene, its own card and line: the host closed the game («Ana ha cerrado la partida»: back in, to the
    /// start screen, or out, as net.ts hostClosed did), nothing changing scene meanwhile; the seat opened in another
    /// window (to the start screen, saying so); and the network's state at the top.
    ///
    /// A document without a panel draws nothing (24/10: the app came up black): each one gets Resources/HudPanel, with
    /// an error in the log. And `-shotOut &lt;file.png&gt; [-shotAfter &lt;seconds&gt;] [-shotQuit]` takes a picture of the
    /// built app's screen, to check the apps themselves and not only the editor.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    // After UIDocument's own (−100), which makes the root this reads in OnEnable; before the screens (0), so a panel it
    // puts on their documents is there when they draw.
    [DefaultExecutionOrder(-50)]
    public sealed class AppFlow : MonoBehaviour
    {
        [SerializeField] string startScene = "Inicio";
        [SerializeField] string battleScene = "Partida";
        [SerializeField] string baseScene = "Base";

        static AppFlow instance;
        static bool opened;
        NetLink link;
        VisualElement root;
        VisualElement card;
        /// <summary>The veil under the card: over the whole screen, so the HUD's own cards don't show through (24/10).</summary>
        VisualElement veil;
        /// <summary>The HUD hidden while the card is up (its arrival card still read through the veil, 24/10).</summary>
        VisualElement hiddenHud;
        Label cardNote;
        Label line;
        bool loading;
        bool rejoining;
        NetStatus status = NetStatus.Open;
        Perf perf;
        /// <summary>The start scene's two screens, found once (Unity's null again once the scene is gone).</summary>
        StartScreen start;
        RoomMenu room;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None)) PanelOf(doc);
            link = NetLink.Instance;
            link.HostClosed += OnHostClosed;
            link.Replaced += OnReplaced;
            link.Status += OnStatus;
            link.Rejoining += OnRejoining;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Tests with the app (TestSteps): steps from the app's start, for the start screen and the room («Continuar campaña»).
            var steps = System.Environment.GetEnvironmentVariable("BASTION_RECORRIDO_SALA");
            if (!string.IsNullOrEmpty(steps)) _ = TestSteps.Run(steps, null, destroyCancellationToken);
            // The graphics settings kept on this computer, and how the game runs told to the host (Perf).
            GameSettings.Apply();
            // The base's music from the start (main.ts).
            Bastion.Client.Audio.UiSounds.Lobby();
            perf = gameObject.AddComponent<Perf>();
            perf.BattleScene = battleScene;
            // BASTION_PERFILAR=<frames>: where the main thread's time goes, in the log (Perfil).
            if (int.TryParse(System.Environment.GetEnvironmentVariable("BASTION_PERFILAR"), out var measured))
                gameObject.AddComponent<Perfil>().Frames = measured;
            var shot = Arg("-shotOut");
            if (!string.IsNullOrEmpty(shot)) StartCoroutine(Shoot(shot));
        }

        /// <summary>
        /// A document's panel: its own, else Resources/HudPanel (set on it), with an error saying so; null if even that is
        /// missing.
        /// </summary>
        public static PanelSettings PanelOf(UIDocument doc)
        {
            if (doc.panelSettings != null || doc.parentUI != null) return doc.panelSettings;
            var panel = Resources.Load<PanelSettings>("HudPanel");
            Debug.LogError($"[app] UIDocument «{doc.name}» ({doc.gameObject.scene.name}) without PanelSettings: " + (panel != null ? "Resources/HudPanel put on it" : "Resources/HudPanel missing too, it draws nothing"));
            if (panel != null) doc.panelSettings = panel;
            return panel;
        }

        /// <summary>The value after a command-line flag; null without it.</summary>
        static string Arg(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }

        /// <summary>
        /// `-shotOut`: the screen as the player sees it, `-shotAfter` seconds in (6 unless said), saved where it says;
        /// `-shotQuit` closes the app once it is written. Not in batch mode without graphics: there is no screen.
        /// </summary>
        System.Collections.IEnumerator Shoot(string path)
        {
            var after = float.TryParse(Arg("-shotAfter"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 6;
            yield return new WaitForSecondsRealtime(after);
            var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(folder)) System.IO.Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(path);
            // Written after the frame ends: up to three seconds, then whether it is there.
            for (var i = 0; i < 60 && !System.IO.File.Exists(path); i++) yield return new WaitForSecondsRealtime(0.05f);
            Debug.Log(System.IO.File.Exists(path) ? $"[app] screen saved: {path}" : $"[app] screen NOT saved: {path}");
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-shotQuit") >= 0) Quit();
        }

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            // Over everything the scenes put up.
            doc.sortingOrder = 100;
            root = doc.rootVisualElement;
            // The app opening (cuadro 136): the game's title first, while the start screen builds its backdrop under it.
            if (!opened)
            {
                opened = true;
                if (SceneManager.GetActiveScene().name == startScene)
                {
                    Perf.Begin("abrir");
                    _ = RaiseVeil(new LoadView { Title = StartText.Title }, 0);
                }
            }
            root.pickingMode = PickingMode.Ignore;
            // A click or a key on any screen (they all share this panel): its sound (ui.ts).
            Bastion.Client.Audio.UiSounds.Install(root);
            foreach (var sheet in new[] { "Hud/Mixta", "Hud/Cards" })
            {
                var s = Resources.Load<StyleSheet>(sheet);
                if (s != null) root.styleSheets.Add(s);
            }
            root.AddToClassList("font-text");
            line = Ui.Text("", "app__line", root);
            line.pickingMode = PickingMode.Ignore;
            line.style.position = Position.Absolute;
            line.style.top = 6;
            line.style.alignSelf = Align.Center;
            line.style.backgroundColor = new Color(0.07f, 0.08f, 0.08f, 0.94f);
            line.style.paddingLeft = 10;
            line.style.paddingRight = 10;
            Ui.Show(line, false);
            // Ajustes' «Mostrar FPS»: top right, beside the Menú tab in the battle (Perf places and fills it).
            var counter = Ui.Text("", "app__fps font-title", root);
            counter.pickingMode = PickingMode.Ignore;
            counter.style.position = Position.Absolute;
            counter.style.top = 8;
            counter.style.right = 8;
            counter.style.fontSize = 11;
            counter.style.backgroundColor = new Color(0.07f, 0.08f, 0.08f, 0.94f);
            counter.style.paddingLeft = 6;
            counter.style.paddingRight = 6;
            Ui.Show(counter, GameSettings.ShowFps);
            if (perf != null) perf.Counter = counter;
        }

        void OnDestroy()
        {
            if (instance != this || link == null) return;
            link.HostClosed -= OnHostClosed;
            link.Replaced -= OnReplaced;
            link.Status -= OnStatus;
            link.Rejoining -= OnRejoining;
            instance = null;
        }

        /// <summary>The squad getting into La Lata (BaseSession.Depart): the mission loads once it has gone.</summary>
        bool departing;

        void Update()
        {
            GameSettings.WatchScreen();
            if (loading || departing || card != null) return;
            var phase = link.LastRoom?.Phase;
            // The scene for the room's phase: the battle's in a mission, Arte's base on the campaign's base, else the start.
            var want = phase == RoomPhases.Mission ? battleScene : phase == RoomPhases.Base ? baseScene : startScene;
            var active = SceneManager.GetActiveScene().name;
            if (active != want)
            {
                // From the base to a mission: La Lata leaves first (screen.ts launch, awaited before the battle).
                // (Each change of screen timed for red.log, Perf: from the yes to the new screen drawn, M2.)
                Perf.Begin($"{Label(active)} → {Label(want)}");
                if (want == battleScene && active == baseScene) Depart();
                else if (want == battleScene) _ = LoadBattle(WindowFor(want, active));
                // Back to the menu: no window (cuadro 139, Red's measure in the app: ~90 ms, nothing to cover).
                else if (want == startScene) Load(want);
                else _ = LoadWith(want, WindowFor(want, active));
                return;
            }
            // «Continuar partida» and «Nueva partida» (NetLink.Opening, 0.12.24): the start screen stays, saying so, while the
            // room is in the lobby on its way to the base; its menu only if it stops there (nothing to go on with, refused).
            if (want == startScene) Screens(phase == null || link.Opening);
        }

        /// <summary>
        /// A scene in: the battle's gets the HUD (Arte's scene stays his alone, Gameplay 23/10): a document with the HUD's
        /// panel (AppFlow's own), HudRoot, and the mission over the network (CombatSession: the controller, the camera's
        /// rig and the marks). The base's (Arte's «Base»: the clearing, Settlement) gets the base's screens: StrategyRoot
        /// and the campaign over the network (BaseSession).
        /// </summary>
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            loading = false;
            // A held preload is spent by any scene's load (another scene loaded over it lets the battle's in first, then
            // its own, in one frame: seen, the 0.14.2's review, point 4); kept, the next stage «let in» a spent one and
            // the app stopped loading for good.
            preloaded = null;
            // (When, for the labs' strips: which of their frames is the change.)
            Debug.Log(System.FormattableString.Invariant($"[app] escena «{scene.name}» a los {Time.time:0.00} s de juego, fotograma {Time.frameCount}"));
            // The base: its window goes once two frames of it are drawn. The start screen lifts it itself, once its backdrop
            // is built (StartScreen); the battle, once its map and figures are drawn (CombatSession, StageLoad.FirstDrawn).
            if (scene.name == baseScene) _ = LiftAfterFrames(2);
            // (The room's menu instead of the start screen, or a backdrop already up: lifted anyway, a moment later.) With no
            // window (back to the menu), the change is done two frames in (Perf's line).
            else if (scene.name == startScene) _ = LiftAfterFrames(loadVeil != null ? 90 : 2);
            GameSettings.InMenu = scene.name != battleScene;
            // Ajustes' «Interfaz» on whatever panel the new scene brought (all share AppFlow's today).
            GameSettings.ApplyInterface();
            if (scene.name == startScene) Bastion.Client.Audio.UiSounds.Lobby();
            if (scene.name == baseScene)
            {
                var screens = new GameObject("Base");
                SceneManager.MoveGameObjectToScene(screens, scene);
                screens.AddComponent<UIDocument>().panelSettings = PanelOf(GetComponent<UIDocument>());
                screens.AddComponent<Bastion.UI.Strategy.StrategyRoot>().Sound += Bastion.Client.Audio.UiSounds.Play;
                screens.AddComponent<Bastion.UI.Strategy.BaseSession>();
                return;
            }
            if (scene.name != battleScene) return;
            var hud = new GameObject("HUD");
            SceneManager.MoveGameObjectToScene(hud, scene);
            hud.AddComponent<UIDocument>().panelSettings = PanelOf(GetComponent<UIDocument>());
            var root = hud.AddComponent<HudRoot>();
            root.Sound += Bastion.Client.Audio.UiSounds.Play;
            hud.AddComponent<Bastion.UI.Combat.CombatSession>().Hud = root;
        }

        /// <summary>
        /// To a stage (cuadro 136): from the map on paper, the stage's sheet comes up over it (the same paper, faded in), and
        /// La Lata's leaving the clearing, which the map hides, is skipped; else (a siege, the old campaign) La Lata leaves
        /// first as ever. Then the battle under the window.
        /// </summary>
        /// <remarks>
        /// From the base (the first stage, cuadro 138, Arturo: «Ver salir a La Lata, corto»): the map on paper goes, the
        /// clearing is seen and La Lata drives off (BaseSession.Depart), the battle's scene loading meanwhile in the
        /// background (not shown till the sheet is up); then the sheet. Between stages, the sheet at once. The black of
        /// before (0.13.0 to 0.14.1): the map's tab had the clearing's camera off, and La Lata left unseen.
        /// </remarks>
        async void Depart()
        {
            departing = true;
            try
            {
                var session = FindFirstObjectByType<Bastion.UI.Strategy.BaseSession>();
                var sheet = session?.Control?.StageSheet();
                // The battle's scene loading from the start, in the background: through La Lata's leaving, or the sheet's coming in.
                PreloadBattle();
                if ((sheet == null || sheet.Departs) && session != null) await session.Depart();
                // (The 0.14.2's review, points 4 and 8, labs only: the room left during the departure, or its host gone.)
                var test = System.Environment.GetEnvironmentVariable("BASTION_PRUEBA_SALIDA");
                // («corte1»: only the first, to go to a stage again after it.)
                if (test == "corte" || (test == "corte1" && !cutOnce)) link.Leave();
                if (test == "corte1") cutOnce = true;
                if (test == "anfitrion") HostClosedCard();
                if (sheet != null) await RaiseVeil(sheet, FadeIn);
            }
            finally
            {
                departing = false;
            }
            if (link.LastRoom?.Phase == RoomPhases.Mission) await LoadBattle();
        }

        /// <summary>The battle's scene loading in the background, held back from showing (LoadBattle lets it in).</summary>
        AsyncOperation preloaded;

        /// <summary>(BASTION_PRUEBA_SALIDA=corte1: the room left during the first departure only.)</summary>
        static bool cutOnce;

        void PreloadBattle()
        {
            if (preloaded != null || System.Environment.GetEnvironmentVariable("BASTION_SIN_PRECARGA") == "1") return;
            preloaded = SceneManager.LoadSceneAsync(battleScene);
            if (preloaded != null) preloaded.allowSceneActivation = false;
        }

        /// <summary>
        /// What the player pressed to leave the start screen (R22, Red 23/11: the sheet said what the room said last, a new
        /// game «Continuando la partida», a stage half played «Escaramuza»): the next change's words. A skirmish brings its
        /// own (its kind, day or night). Whoever joins presses nothing: the room's words then.
        /// </summary>
        public enum Asked { None, New, GoOn, Skirmish }

        static Asked asked;
        static LoadView askedWords;
        static float askedAt;

        /// <summary>Said on the start screen or the room's menu as the key is pressed; good for a minute, taken once.</summary>
        public static void Ask(Asked what, LoadView words = null)
        {
            asked = what;
            askedWords = words;
            askedAt = Time.realtimeSinceStartup;
        }

        static Asked TakeAsked()
        {
            var what = Time.realtimeSinceStartup - askedAt < 60 ? asked : Asked.None;
            asked = Asked.None;
            return what;
        }

        /// <summary>What a scene is called in the change's line.</summary>
        string Label(string scene) => scene == battleScene ? "combate" : scene == baseScene ? "base" : "menú";

        /// <summary>The battle loaded under the window: up first (a dark one with no stage known: a game going on in one), drawn, then the scene.</summary>
        async Awaitable LoadBattle(LoadView window = null)
        {
            loading = true;
            if (loadVeil == null) await RaiseVeil(window, 0);
            if (preloaded != null)
            {
                // Loaded meanwhile: only let in now, under the window.
                preloaded.allowSceneActivation = true;
                preloaded = null;
                return;
            }
            Load(battleScene);
        }

        /// <summary>Any other scene under its window: up, drawn, then the scene (cuadro 136: «ventana en todas las esperas»).</summary>
        async Awaitable LoadWith(string scene, LoadView window)
        {
            loading = true;
            await RaiseVeil(window, 0);
            Load(scene);
        }

        /// <summary>
        /// The window for going to `scene` from `from`, all on paper (cuadro 139, Arturo: «todas las esperas en papel»; only
        /// «BASTIÓN», as the app opens, stays dark): the base from the battle, «Vuelta a la base» with the stage's result
        /// (CombatSession's words); from the start screen, the game going on («Capítulo 1 · Medio») or a new one; a battle
        /// from the start screen, the stage left half played, or a skirmish (its words as its state comes: VeilWords).
        /// </summary>
        LoadView WindowFor(string scene, string from)
        {
            if (scene == baseScene && from == battleScene)
            {
                var back = Bastion.UI.Combat.CombatSession.ReturnWindow() ?? new LoadView { Title = LoadText.Back };
                back.Paper = true;
                return back;
            }
            // From the start screen: what was pressed (TakeAsked), else what the room says (whoever joins).
            if (scene == battleScene || scene == baseScene) return SheetFor(scene == battleScene, TakeAsked(), askedWords, link.LastRoom?.SavedCampaign, StartScreen.HostLevel);
            return new LoadView { Title = StartText.Title };
        }

        /// <summary>
        /// The sheet out of the start screen (R22; the labs' too, VentanaLab): into a battle, a skirmish (its own words, its
        /// map with its state) or a game going on with its stage half played; into the base, a new game or one going on.
        /// Asked nothing, the room's words: a stage half played (InMission, Red's) is a game going on; a game with nothing
        /// played yet (no stage entered, none half played) is a new one.
        /// </summary>
        public static LoadView SheetFor(bool battle, Asked what, LoadView skirmish, SavedCampaign saved, string hostLevel)
        {
            var chapter = saved != null && saved.Chapter > 0 ? saved.Chapter : 1;
            var level = Lore.GameLevelText.NameOf(saved?.GameLevel ?? hostLevel);
            if (battle)
            {
                var goingOn = what == Asked.GoOn || (what == Asked.None && saved != null && saved.Chapter > 0 && saved.InMission != null);
                if (!goingOn) return skirmish != null && what == Asked.Skirmish ? skirmish : new LoadView { Paper = true, Kicker = RoomText.Skirmish };
                return new LoadView { Paper = true, Kicker = LoadText.GoingOn, Title = StartText.SavedLine(chapter, level), Place = saved?.InMission != null ? RoomText.SavedWhere(saved) : null };
            }
            var fresh = what == Asked.New || (what == Asked.None && (saved == null || saved.Chapter <= 0 || (saved.NodesPlayed == 0 && saved.Row == null && saved.InMission == null)));
            return new LoadView { Paper = true, Kicker = fresh ? LoadText.New : LoadText.GoingOn, Title = StartText.SavedLine(chapter, level) };
        }

        async Awaitable LiftAfterFrames(int frames)
        {
            var raised = veilRaised;
            for (var i = 0; i < frames; i++) await Awaitable.NextFrameAsync();
            // A change with no window (back to the menu): over now, for Perf's line.
            if (loadVeil == null) Perf.End();
            else LiftVeil(raised);
        }

        // ---------------------------------------------------------- loading

        /// <summary>The window comes off by itself this long after it went up, whatever happened to the scene (s).</summary>
        const float VeilMost = 20;
        /// <summary>How long it takes to come in over the map (the sheet) and to go (s).</summary>
        const float FadeIn = 0.3f, VeilOut = 0.4f;

        LoadWindow window;
        VisualElement loadVeil;
        LoadView shownView;
        float loadSince, liftSince = -1, fadeFrom = -1, fadeLength;
        /// <summary>
        /// Which raising the window is at (each change of screen its own): a lift asked for one before (the start screen's
        /// backdrop built as the base's window went up over it) leaves the newer window be.
        /// </summary>
        int veilRaised;

        /// <summary>The window's raising now (LiftVeil(raised) lifts only that one).</summary>
        public static int Raised => instance?.veilRaised ?? 0;

        /// <summary>
        /// The loading window (cuadro 136; hud.ts veil before it): over everything, its words from the start (no running bar:
        /// it froze halfway with the load, M2), faded in over `fade` seconds (the sheet over the map), and drawn before it
        /// returns: the load that follows freezes on it. Without a view, a dark one, to be told the battle's words
        /// (VeilWords). Up already: its view put in if it had none.
        /// </summary>
        async Awaitable RaiseVeil(LoadView v, float fade)
        {
            veilRaised++;
            loadSince = Time.realtimeSinceStartup;
            if (loadVeil != null)
            {
                // Up still (going, or the one before's): held, with this change's words if it has its own.
                liftSince = -1;
                loadVeil.pickingMode = PickingMode.Position;
                if (v != null && v != shownView) Show(v);
            }
            else
            {
                liftSince = -1;
                Show(v);
                if (fade > 0)
                {
                    fadeFrom = loadSince;
                    fadeLength = fade;
                    loadVeil.style.opacity = 0;
                }
            }
            // Faded in, then two frames drawn whole: what the load freezes on is the window.
            while (fadeFrom >= 0 && Time.realtimeSinceStartup - fadeFrom < fadeLength) await Awaitable.NextFrameAsync();
            if (loadVeil != null) loadVeil.style.opacity = 1;
            fadeFrom = -1;
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
        }

        void Show(LoadView v)
        {
            var opacity = loadVeil?.style.opacity ?? 1;
            loadVeil?.RemoveFromHierarchy();
            shownView = v;
            window = new LoadWindow(v ?? new LoadView());
            loadVeil = window.Root;
            loadVeil.style.opacity = opacity;
            root.Add(loadVeil);
            // The host's card over it, if it is up (the review's point 8: a sheet raised after it hid it).
            veil?.BringToFront();
            // Its step on the element now up: one replaced (the battle's words put in) took its schedule with it, and the
            // window never went (the way into a game going on, 23/11).
            loadVeil.schedule.Execute(StepVeil).Every(16);
        }

        void StepVeil()
        {
            if (loadVeil == null) return;
            var now = Time.realtimeSinceStartup;
            if (fadeFrom >= 0) loadVeil.style.opacity = Mathf.Clamp01((now - fadeFrom) / fadeLength);
            if (liftSince < 0)
            {
                if (now - loadSince > VeilMost) LiftVeil();
                return;
            }
            var gone = now - liftSince;
            loadVeil.style.opacity = Mathf.Clamp01(1 - gone / VeilOut);
            if (gone < VeilOut) return;
            loadVeil.RemoveFromHierarchy();
            loadVeil = null;
            window = null;
            shownView = null;
        }

        /// <summary>
        /// The battle's words on a window that has none (a skirmish's sheet, its kicker alone): what it is and where, on the
        /// same paper (or dark, a window that was).
        /// </summary>
        public static void VeilWords(string kicker, string title, string place)
        {
            var app = instance;
            if (app == null || app.loadVeil == null) return;
            var shown = app.shownView;
            // A game going on whose stage the room didn't name: the battle's stage under its chapter (R22).
            if (shown?.Kicker == LoadText.GoingOn && string.IsNullOrEmpty(place)) place = string.IsNullOrEmpty(title) ? null : RoomText.HalfPlayed(title);
            // Only what the sheet lacks (a skirmish's map; the stage of a game going on): what it says stays.
            if (shown != null && !string.IsNullOrEmpty(shown.Title) && (!string.IsNullOrEmpty(shown.Place) || string.IsNullOrEmpty(place))) return;
            var v = new LoadView { Paper = shown?.Paper == true, Kicker = shown?.Kicker ?? kicker, Title = string.IsNullOrEmpty(shown?.Title) ? title : shown.Title, Place = string.IsNullOrEmpty(shown?.Place) ? place : shown.Place };
            if (shown != null) v.Notes.AddRange(shown.Notes);
            app.Show(v);
            // For tests with the app (BASTION_FOTOS, Red's CampaignRun's): the window with its words, as this frame ends.
            var shots = System.Environment.GetEnvironmentVariable("BASTION_FOTOS");
            if (!string.IsNullOrEmpty(shots)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, $"velo-{++app.veilShots}.png"));
        }

        int veilShots;

        /// <summary>How far something real under the window has come (0–1: the squad's figures made): the sheet's red ring.</summary>
        public static void Progress(float f) => instance?.window?.Progress(f);

        /// <summary>
        /// The scene is drawn: the window goes, and the change is over (Perf's line). With `raised` (Raised when asked for),
        /// only if no other window went up since.
        /// </summary>
        public static void LiftVeil(int raised = -1)
        {
            var app = instance;
            if (app == null || app.loadVeil == null || app.liftSince >= 0) return;
            if (raised >= 0 && raised != app.veilRaised) return;
            Perf.End();
            app.liftSince = Time.realtimeSinceStartup;
            app.loadVeil.pickingMode = PickingMode.Ignore;
        }

        /// <summary>The window is up and not going: what comes after it (the arrival card) waits.</summary>
        public static bool Veiled => instance != null && instance.loadVeil != null && instance.liftSince < 0;

        /// <summary>The window is on screen at all, going or not: the first turn's stamp waits till it is gone, to be seen whole.</summary>
        public static bool WindowShown => instance != null && instance.loadVeil != null;

        void Load(string scene)
        {
            loading = true;
            Debug.Log(System.FormattableString.Invariant($"[app] cargando «{scene}» a los {Time.realtimeSinceStartup:0.00} s{(preloaded != null ? ", con la del combate precargada" : "")}"));
            // (Let in first, as Unity does anyway; spent then: OnSceneLoaded.)
            if (preloaded != null && scene != battleScene) preloaded.allowSceneActivation = true;
            SceneManager.LoadScene(scene);
        }

        /// <summary>In the start scene: the start screen out of a room, its menu in one (one at a time on its document).</summary>
        void Screens(bool outside)
        {
            if (start == null) start = FindFirstObjectByType<StartScreen>(FindObjectsInactive.Include);
            if (room == null) room = FindFirstObjectByType<RoomMenu>(FindObjectsInactive.Include);
            if (start == null || room == null) return;
            if (start.enabled == outside && room.enabled == !outside) return;
            // The one going off first: the one coming on clears the document and draws itself.
            if (outside) room.enabled = false;
            else start.enabled = false;
            if (outside) start.enabled = true;
            else room.enabled = true;
        }

        // ------------------------------------------------------- host closed

        /// <summary>The host closed the game: the card, for the guest (a host's own app is closing with it).</summary>
        void OnHostClosed()
        {
            if (link.Hosting || card != null) return;
            HostClosedCard();
        }

        void HostClosedCard()
        {
            if (card != null) return;
            var host = link.PartnerName;
            veil = Ui.El();
            veil.style.position = Position.Absolute;
            veil.style.left = 0;
            veil.style.top = 0;
            veil.style.right = 0;
            veil.style.bottom = 0;
            veil.style.backgroundColor = new Color(0.02f, 0.03f, 0.03f, 0.78f);
            card = Ui.El("card panel", veil);
            card.style.position = Position.Absolute;
            card.style.alignSelf = Align.Center;
            card.style.top = Length.Percent(32);
            Ui.Text(Lore.HostClosedLine(host), "card__title font-title", card);
            cardNote = Ui.Text(Lore.HostClosed.Back, "card__text", card);
            var actions = Ui.El("card__actions", card);
            var retry = new Button(Retry) { text = Lore.HostClosed.Retry };
            retry.AddToClassList("btn-primary");
            actions.Add(retry);
            actions.Add(new Button(Home) { text = Lore.HostClosed.Home });
            actions.Add(new Button(Quit) { text = Lore.QuitText.Button });
            root.Add(veil);
            hiddenHud = FindFirstObjectByType<HudRoot>()?.GetComponent<UIDocument>()?.rootVisualElement;
            if (hiddenHud != null) hiddenHud.style.display = DisplayStyle.None;
        }

        /// <summary>Back in, the same seat (NetLink keeps it by host): «Aún no está abierta» while it isn't.</summary>
        void Retry()
        {
            link.Joined -= OnBackIn;
            link.Failed -= OnNotYet;
            link.Joined += OnBackIn;
            link.Failed += OnNotYet;
            var problem = link.Join(link.LastAddress ?? "", link.LastName ?? "");
            if (problem != null) OnNotYet(problem);
        }

        void OnBackIn(int slot)
        {
            link.Joined -= OnBackIn;
            link.Failed -= OnNotYet;
            CloseCard();
        }

        void OnNotYet(string _)
        {
            link.Joined -= OnBackIn;
            link.Failed -= OnNotYet;
            if (cardNote != null) cardNote.text = Lore.HostClosed.NotYet;
        }

        void Home()
        {
            link.Leave();
            CloseCard();
        }

        void CloseCard()
        {
            veil?.RemoveFromHierarchy();
            veil = null;
            if (hiddenHud != null) hiddenHud.style.display = StyleKeyword.Null;
            hiddenHud = null;
            card = null;
            cardNote = null;
        }

        /// <summary>This seat opened in another window: back to the start screen, saying so.</summary>
        void OnReplaced()
        {
            StartScreen.Note = RoomText.Replaced;
            CloseCard();
            link.Leave();
        }

        /// <summary>Closes the game (the game's menu, the host's card); in the editor, stops playing.</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------- the line

        void OnStatus(NetStatus s)
        {
            status = s;
            DrawLine();
        }

        void OnRejoining(bool on)
        {
            rejoining = on;
            DrawLine();
        }

        /// <summary>At the top while not connected (hud.ts setConnection), or coming back after a sleep (RejoinLine).</summary>
        void DrawLine()
        {
            if (line == null) return;
            var text = rejoining ? Lore.RejoinLine : status == NetStatus.Connecting ? HudText.Net.Connecting : status == NetStatus.Closed && link.LastRoom != null ? HudText.Net.Lost : "";
            line.text = text;
            Ui.Show(line, text.Length > 0 && card == null);
        }
    }
}
