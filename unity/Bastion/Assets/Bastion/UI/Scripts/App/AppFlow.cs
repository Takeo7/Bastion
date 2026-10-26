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
            if (loading || departing || card != null) return;
            var phase = link.LastRoom?.Phase;
            // The scene for the room's phase: the battle's in a mission, Arte's base on the campaign's base, else the start.
            var want = phase == RoomPhases.Mission ? battleScene : phase == RoomPhases.Base ? baseScene : startScene;
            var active = SceneManager.GetActiveScene().name;
            if (active != want)
            {
                // From the base to a mission: La Lata leaves first (screen.ts launch, awaited before the battle).
                if (want == battleScene && active == baseScene) Depart();
                else if (want == battleScene) _ = LoadBattle();
                else Load(want);
                return;
            }
            if (want == startScene) Screens(phase == null);
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
            // Not the battle (back to the base, to the menu): nothing to wait for under the veil.
            if (scene.name != battleScene) LiftVeil();
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

        async void Depart()
        {
            departing = true;
            try
            {
                var session = FindFirstObjectByType<Bastion.UI.Strategy.BaseSession>();
                if (session != null) await session.Depart();
            }
            finally
            {
                departing = false;
            }
            if (link.LastRoom?.Phase == RoomPhases.Mission) await LoadBattle();
        }

        /// <summary>The battle loaded under the veil: up first, drawn a frame, then the scene.</summary>
        async Awaitable LoadBattle()
        {
            loading = true;
            RaiseVeil();
            await Awaitable.NextFrameAsync();
            Load(battleScene);
        }

        // ---------------------------------------------------------- loading

        /// <summary>The veil comes off by itself this long after it went up, whatever happened to the battle (s).</summary>
        const float VeilMost = 20;
        /// <summary>How long its words take to go, and then its black (mixta-hud.css .load-veil.out: 0.12 s, 0.4 s).</summary>
        const float WordsOut = 0.12f, VeilOut = 0.4f;

        VisualElement loadVeil, loadWords, loadRun;
        Label loadKicker, loadTitle, loadPlace;
        float loadSince, liftSince = -1;

        /// <summary>
        /// While the battle loads (hud.ts veil, game.ts raiseVeil; UX-REVISION-3 #1): the operation's name over a running
        /// bar instead of a frozen screen, over everything; its words once the battle has its state (VeilWords), lifted once
        /// the map is drawn (LiftVeil); the arrival card waits for it (Veiled).
        /// </summary>
        void RaiseVeil()
        {
            if (loadVeil != null)
            {
                liftSince = -1;
                return;
            }
            loadVeil = Ui.El();
            var v = loadVeil.style;
            v.position = Position.Absolute;
            v.left = 0;
            v.top = 0;
            v.right = 0;
            v.bottom = 0;
            v.backgroundColor = Color.black;
            // The words from 30 % of the height less 48 px (.load-veil: padding-top calc(30vh − 48px)): a spacer's height,
            // as a margin's percent goes by the width.
            var spacer = Ui.El(null, loadVeil);
            spacer.style.height = Length.Percent(30);
            spacer.style.flexShrink = 0;
            loadWords = Ui.El(null, loadVeil);
            loadWords.style.alignItems = Align.Center;
            loadWords.style.marginTop = -48;
            loadKicker = Ui.Text("", "font-title", loadWords);
            loadKicker.style.fontSize = 13;
            loadKicker.style.letterSpacing = 3.4f;
            loadKicker.style.color = (Color)new Color32(0xf2, 0xc2, 0x30, 0xff);
            loadTitle = Ui.Text("", "font-title", loadWords);
            loadTitle.style.fontSize = 44;
            loadTitle.style.letterSpacing = 2.2f;
            loadTitle.style.color = Color.white;
            loadTitle.style.marginTop = 4;
            loadTitle.style.marginBottom = 10;
            var rail = Ui.El(null, loadWords);
            rail.style.width = 220;
            rail.style.height = 4;
            rail.style.overflow = Overflow.Hidden;
            rail.style.backgroundColor = new Color(1, 1, 1, 0.08f);
            loadRun = Ui.El(null, rail);
            loadRun.style.position = Position.Absolute;
            loadRun.style.top = 0;
            loadRun.style.bottom = 0;
            loadRun.style.width = Length.Percent(40);
            loadRun.style.backgroundColor = (Color)new Color32(0xf2, 0xc2, 0x30, 0xff);
            loadPlace = Ui.Text("", null, loadWords);
            loadPlace.style.marginTop = 8;
            loadPlace.style.color = (Color)new Color32(0x8d, 0x91, 0x8a, 0xff);
            loadSince = Time.realtimeSinceStartup;
            liftSince = -1;
            root.Add(loadVeil);
            loadVeil.schedule.Execute(StepVeil).Every(16);
        }

        void StepVeil()
        {
            if (loadVeil == null) return;
            var now = Time.realtimeSinceStartup;
            // The stripe sliding along its rail (veil-run: 1.1 s, eased, from −100 % to 250 % of itself).
            var t = (now - loadSince) % 1.1f / 1.1f;
            loadRun.style.left = Length.Percent(Mathf.Lerp(-40, 100, t * t * (3 - 2 * t)));
            if (liftSince < 0)
            {
                if (now - loadSince > VeilMost) LiftVeil();
                return;
            }
            // Lifting, the words go first: the briefing comes up under them with the same name.
            var gone = now - liftSince;
            loadWords.style.opacity = Mathf.Clamp01(1 - gone / WordsOut);
            loadVeil.style.opacity = Mathf.Clamp01(1 - gone / VeilOut);
            if (gone < VeilOut) return;
            loadVeil.RemoveFromHierarchy();
            loadVeil = null;
        }

        /// <summary>The battle's words on the veil (game.ts raiseVeil): what kind of operation, its name, the place.</summary>
        public static void VeilWords(string kicker, string title, string place)
        {
            var app = instance;
            if (app == null || app.loadVeil == null) return;
            app.loadKicker.text = kicker ?? "";
            Ui.Show(app.loadKicker, !string.IsNullOrEmpty(kicker));
            app.loadTitle.text = (title ?? "").ToUpperInvariant();
            app.loadPlace.text = place ?? "";
            Ui.Show(app.loadPlace, !string.IsNullOrEmpty(place));
            // For tests with the app (BASTION_FOTOS, Red's CampaignRun's): the veil with its words, as this frame ends.
            var shots = System.Environment.GetEnvironmentVariable("BASTION_FOTOS");
            if (!string.IsNullOrEmpty(shots)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, $"velo-{++app.veilShots}.png"));
        }

        int veilShots;

        /// <summary>The map is drawn: the veil goes, its words first.</summary>
        public static void LiftVeil()
        {
            var app = instance;
            if (app == null || app.loadVeil == null || app.liftSince >= 0) return;
            app.liftSince = Time.realtimeSinceStartup;
            app.loadVeil.pickingMode = PickingMode.Ignore;
        }

        /// <summary>The veil is up and not going: what comes after it (the arrival card) waits.</summary>
        public static bool Veiled => instance != null && instance.loadVeil != null && instance.liftSince < 0;

        void Load(string scene)
        {
            loading = true;
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
