using Bastion.UI.Hud;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.EditorTools
{
    /// <summary>
    /// Setups for Arte's captures (unity/tools/captura.sh -shotPlay -shotSetup Bastion.UI.EditorTools.HudShots.Aim):
    /// the HUD lab (HudLab, made-up views) on a UIDocument with the HUD's PanelSettings, over a grey floor, at a given
    /// state. The capture draws the panel into a texture and lays it over the camera's picture.
    /// </summary>
    public static class HudShots
    {
        // In Resources: the app falls back on the panel from there (AppFlow.PanelOf).
        const string ThemePath = "Assets/Bastion/UI/Resources/HudTheme.tss";
        const string PanelPath = AppScenes.PanelPath;

        /// <summary>As it starts: the arrival card over the HUD.</summary>
        public static void Arrival() => Mount();

        /// <summary>Aiming at a zombie: the aim's panel, its target's flag, three lines in the log.</summary>
        public static void Aim() => Mount("llegada", "disparo", "registro", "registro", "registro");

        /// <summary>A self ability armed by its first press (Cubrirse): its panel over its key.</summary>
        public static void Armed() => Mount("llegada", "disparo", "disparo", "disparo");

        /// <summary>The partner's turn: the bar resting, «MANDO: ANA», aiming without the command.</summary>
        public static void Partner() => Mount("llegada", "turno", "disparo");

        /// <summary>«TU TURNO» held, the clocks waiting.</summary>
        public static void Banner() => Mount("llegada", "aviso");

        /// <summary>A siege: the compass with the next wave's sides, the partner's call to leave, a floating number.</summary>
        public static void Siege() => Mount("llegada", "asedio", "llamada", "flotante");

        /// <summary>The mission won.</summary>
        public static void Outcome() => Mount("llegada", "resultado");

        /// <summary>A search's find: «+12 §» in the log and floating, the supplies' sign drawn as the crate.</summary>
        public static void Loot() => Mount("llegada", "registro", "botin");

        /// <summary>Over the shoulder: only the target's flag stays.</summary>
        public static void Shoulder() => Mount("llegada", "disparo", "hombro");

        /// <summary>The start screen (StartScreen with Red's NetLink): the name, be the host, the games on the network, join.</summary>
        public static void Entry()
        {
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.06f);
            var screen = new GameObject("Start");
            screen.AddComponent<UIDocument>().panelSettings = Panel();
            screen.AddComponent<Bastion.UI.Start.StartScreen>();
        }

        /// <summary>The room menu with a made-up room (RoomLab): Arturo hosting, Ana to come, the addresses to give her.</summary>
        public static void Room()
        {
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.06f);
            var screen = new GameObject("Room");
            screen.AddComponent<UIDocument>().panelSettings = Panel();
            screen.AddComponent<Bastion.UI.Start.RoomMenu>();
            screen.AddComponent<RoomLab>();
            // BASTION_TEMA: one of the proposals over the room's sheets (Assets/Bastion/UI/Propuestas).
            var tema = System.Environment.GetEnvironmentVariable("BASTION_TEMA");
            if (!string.IsNullOrEmpty(tema)) screen.AddComponent<Temas>().Tema = tema;
        }

        /// <summary>
        /// The base's screens with made-up views (StrategyLab): `BASTION_ESTRATEGIA` picks what is open (propuesta,
        /// acontecimiento, diario, fin, menu…), over a flat snowy grey where the 0.10.1 had the clearing.
        /// </summary>
        public static void Estrategia()
        {
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.75f);
            var screen = new GameObject("Base");
            screen.AddComponent<UIDocument>().panelSettings = Panel();
            screen.AddComponent<Bastion.UI.Strategy.StrategyRoot>();
            screen.AddComponent<Bastion.UI.Strategy.StrategyLab>();
        }

        /// <summary>
        /// The base's lab in Arte's scene «Base» (run with -shotScene Assets/Bastion/Scenes/Base.unity): the screens over
        /// the clearing, which the sample's cases fill (StrategyLab: Settlement.Show, SetView, Showcase; the busts). The
        /// Mapa's cases from the valley's own camera, as in the game, the clearing's off (run with -shotCamera "Cámara del
        /// valle").
        /// </summary>
        public static void EstrategiaBase()
        {
            if ((System.Environment.GetEnvironmentVariable("BASTION_ESTRATEGIA") ?? "").Contains("mapa")) new GameObject("Cámara del valle").AddComponent<Camera>();
            var screen = new GameObject("Base");
            screen.AddComponent<UIDocument>().panelSettings = Panel();
            screen.AddComponent<Bastion.UI.Strategy.StrategyRoot>();
            screen.AddComponent<Bastion.UI.Strategy.StrategyLab>();
        }

        /// <summary>
        /// The base's orders through a real room in this app (Strategy.BaseRun): BASTION_ANFITRION hosts it (on
        /// BASTION_DATOS, BASTION_PUERTO), a new campaign opens, and BASTION_PASOS are clicked one at a time.
        /// </summary>
        public static void BaseRed()
        {
            // In Arte's scene «Base» (-shotScene), its own camera, which the clearing moves; else a plain sky.
            if (Camera.main == null)
            {
                var camera = new GameObject("Shot camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.62f, 0.68f, 0.75f);
            }
            var run = new GameObject("BaseRun");
            run.AddComponent<UIDocument>().panelSettings = Panel();
            run.AddComponent<Bastion.UI.Strategy.BaseRun>();
        }

        /// <summary>
        /// The base's lab over Arte's clearing (Bastion.EditorTools.SettlementLab.Clearing: the lab's base, seed 3, with
        /// its pieces; BASTION_LOOK frames it, as there) instead of a plain sky: for the Asentamiento's panels. Called by
        /// name, so that this assembly needn't reference Arte's editor tools; without them, the plain sky.
        /// </summary>
        public static void EstrategiaClaro()
        {
            var lab = System.Type.GetType("Bastion.EditorTools.SettlementLab, Bastion.Editor");
            var clearing = lab?.GetMethod("Clearing", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (clearing == null)
            {
                Debug.LogWarning("[estrategia] no SettlementLab.Clearing: the plain sky");
                Estrategia();
                return;
            }
            clearing.Invoke(null, null);
            var screen = new GameObject("Base");
            screen.AddComponent<UIDocument>().panelSettings = Panel();
            screen.AddComponent<Bastion.UI.Strategy.StrategyRoot>();
            screen.AddComponent<Bastion.UI.Strategy.StrategyLab>();
        }

        /// <summary>
        /// The combat on a recorded state (CombatLab, engine-cs/golden/step2/city-3-elimination.json) with its HUD: the reach
        /// of a soldier's move, the way to a tile and the cover there, over a rough floor with capsules for the units.
        /// </summary>
        public static void Combat() => CombatScene("");

        /// <summary>The same, played up to the first shot of the recording: the soldier aiming it, the target's ring, the aim's panel.</summary>
        public static void CombatShot() => CombatScene("", true);

        /// <summary>The same, a grenade armed: how far it reaches.</summary>
        public static void CombatThrow() => CombatScene("grenade");

        /// <summary>
        /// In the scene «Partida» (-shotScene Assets/Bastion/Scenes/Partida.unity -shotCamera Cámara), on its map and
        /// figures: city-3-elimination played up to its first shot, aimed, and fired by itself a moment after; a strip of
        /// it (-shotFrames 40 -shotFps 10) shows the event player with Arte's animations.
        /// </summary>
        public static void PartidaShot() => InPartida("city-3-elimination", false);

        /// <summary>As PartidaShot, up to the first shot that kills (city-6-hard-veterans, or BASTION_LAB_GOLDEN's): the shot over the shoulder.</summary>
        public static void PartidaKill() => InPartida(System.Environment.GetEnvironmentVariable("BASTION_LAB_GOLDEN") ?? "city-6-hard-veterans", true);

        /// <summary>
        /// In «Partida», a mission with La Lata at its bay (BASTION_LAB_GOLDEN's, city-11-scavenge by default): the squad on
        /// the evacuation, the call to leave a second in, and the event player's way out: the camera on La Lata, each one
        /// boarding, and it pulling away (a strip: -shotFrames 50 -shotFps 10).
        /// </summary>
        public static void PartidaEvac()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                var hud = new GameObject("HUD");
                hud.AddComponent<UIDocument>().panelSettings = Panel();
                var lab = new GameObject("CombatLab").AddComponent<Bastion.UI.Combat.CombatLab>();
                lab.Hud = hud.AddComponent<HudRoot>();
                lab.Golden = System.Environment.GetEnvironmentVariable("BASTION_LAB_GOLDEN") ?? "city-11-scavenge";
                lab.Evacuate = true;
                lab.InScene = true;
            };
        }

        /// <summary>In «Partida», a recorded state as it starts (BASTION_LAB_GOLDEN's, city-11-scavenge by default): the mission's marks.</summary>
        public static void PartidaMarks()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                var hud = new GameObject("HUD");
                hud.AddComponent<UIDocument>().panelSettings = Panel();
                var lab = new GameObject("CombatLab").AddComponent<Bastion.UI.Combat.CombatLab>();
                lab.Hud = hud.AddComponent<HudRoot>();
                lab.Golden = System.Environment.GetEnvironmentVariable("BASTION_LAB_GOLDEN") ?? "city-11-scavenge";
                lab.InScene = true;
            };
        }

        /// <summary>
        /// In «Partida», city-3-elimination as it starts with the game's menu open: BASTION_MENU's page (menu, ajustes,
        /// controles) or question (volver, abandonar, salir).
        /// </summary>
        public static void PartidaMenu()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                var hud = new GameObject("HUD");
                hud.AddComponent<UIDocument>().panelSettings = Panel();
                var lab = new GameObject("CombatLab").AddComponent<Bastion.UI.Combat.CombatLab>();
                lab.Hud = hud.AddComponent<HudRoot>();
                lab.Golden = "city-3-elimination";
                lab.InScene = true;
                lab.Menu = System.Environment.GetEnvironmentVariable("BASTION_MENU") ?? "menu";
            };
        }

        /// <summary>In «Partida», a recorded state (BASTION_LAB_GOLDEN's, city-11-scavenge by default): a path up a flight of stairs.</summary>
        public static void PartidaStairs()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                var hud = new GameObject("HUD");
                hud.AddComponent<UIDocument>().panelSettings = Panel();
                var lab = new GameObject("CombatLab").AddComponent<Bastion.UI.Combat.CombatLab>();
                lab.Hud = hud.AddComponent<HudRoot>();
                lab.Golden = System.Environment.GetEnvironmentVariable("BASTION_LAB_GOLDEN") ?? "city-11-scavenge";
                lab.InScene = true;
                lab.Stairs = true;
            };
        }

        /// <summary>
        /// The HUD's proposals for Arturo: «Partida», city-3-elimination up to its first shot, aimed and not fired (the shot's
        /// panel over a zombie), with BASTION_TEMA's sheet (Assets/Bastion/UI/Propuestas) over the HUD's; none, today's.
        /// </summary>
        public static void PartidaTema()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                var hud = new GameObject("HUD");
                hud.AddComponent<UIDocument>().panelSettings = Panel();
                var lab = new GameObject("CombatLab").AddComponent<Bastion.UI.Combat.CombatLab>();
                lab.Hud = hud.AddComponent<HudRoot>();
                hud.AddComponent<Temas>().Tema = System.Environment.GetEnvironmentVariable("BASTION_TEMA") ?? "";
                lab.Golden = "city-3-elimination";
                lab.ToShot = true;
                lab.InScene = true;
            };
        }

        /// <summary>In «Partida», city-3-elimination as it starts, Ana in: her plan for a soldier and her ping.</summary>
        public static void PartidaPlans()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                var hud = new GameObject("HUD");
                hud.AddComponent<UIDocument>().panelSettings = Panel();
                var lab = new GameObject("CombatLab").AddComponent<Bastion.UI.Combat.CombatLab>();
                lab.Hud = hud.AddComponent<HudRoot>();
                lab.Golden = "city-3-elimination";
                lab.InScene = true;
                lab.Plans = true;
            };
        }

        /// <summary>As PartidaShot, the pointer on the target instead: what that enemy sees, and the eye on its flag if it sees one of ours.</summary>
        public static void PartidaVision() => InPartida("city-3-elimination", false, true);

        static void InPartida(string golden, bool kills, bool hover = false)
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                var hud = new GameObject("HUD");
                hud.AddComponent<UIDocument>().panelSettings = Panel();
                var lab = new GameObject("CombatLab").AddComponent<Bastion.UI.Combat.CombatLab>();
                lab.Hud = hud.AddComponent<HudRoot>();
                lab.Golden = golden;
                lab.ToShot = true;
                lab.Kills = kills;
                lab.HoverTarget = hover;
                lab.InScene = true;
                // BASTION_LAB_SIN_DISPARO=1: aimed and left so, the shot's panel open (the 0.10.1's shot while aiming).
                lab.Fire = !hover && System.Environment.GetEnvironmentVariable("BASTION_LAB_SIN_DISPARO") != "1";
            };
        }

        /// <summary>
        /// In «Partida», as PartidaShot: city-11-scavenge played up to its first big enemy turn (16 of the dead walking), and
        /// that turn's recorded events played by the event player: the dead walking together, the camera on those who act.
        /// </summary>
        public static void PartidaTurn()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                var hud = new GameObject("HUD");
                hud.AddComponent<UIDocument>().panelSettings = Panel();
                var lab = new GameObject("CombatLab").AddComponent<Bastion.UI.Combat.CombatLab>();
                lab.Hud = hud.AddComponent<HudRoot>();
                lab.Golden = System.Environment.GetEnvironmentVariable("BASTION_LAB_GOLDEN") ?? "city-11-scavenge";
                lab.ToEnemyTurn = true;
                lab.InScene = true;
            };
        }

        /// <summary>
        /// In «Partida», the backpack (docs/FASE8.md §C): a recorded mission with things in the backpacks, on the floor and a
        /// fallen soldier's rucksack (BASTION_LAB_ARCHIVO: a golden's file, its path without «.json»), played by BASTION_TOQUES
        /// (PackSteps).
        /// </summary>
        public static void PartidaMochila()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                var hud = new GameObject("HUD");
                hud.AddComponent<UIDocument>().panelSettings = Panel();
                var lab = new GameObject("CombatLab").AddComponent<Bastion.UI.Combat.CombatLab>();
                lab.Hud = hud.AddComponent<HudRoot>();
                lab.Golden = System.Environment.GetEnvironmentVariable("BASTION_LAB_ARCHIVO") ?? "city-3-elimination";
                lab.InScene = true;
            };
        }

        static void CombatScene(string arm, bool toShot = false)
        {
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.09f);
            camera.gameObject.AddComponent<Bastion.UI.Combat.CameraRig>();
            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50, 30, 0);
            var hud = new GameObject("HUD");
            hud.AddComponent<UIDocument>().panelSettings = Panel();
            var lab = new GameObject("CombatLab").AddComponent<Bastion.UI.Combat.CombatLab>();
            lab.Hud = hud.AddComponent<HudRoot>();
            lab.Arm = arm;
            lab.ToShot = toShot;
        }

        static void Mount(params string[] steps)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(4, 1, 4);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null) floor.GetComponent<Renderer>().sharedMaterial = new Material(lit) { color = new Color(0.36f, 0.38f, 0.37f) };
            var camera = new GameObject("Shot camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 9, -12);
            camera.transform.LookAt(Vector3.zero);
            var hud = new GameObject("HUD");
            hud.AddComponent<UIDocument>().panelSettings = Panel();
            hud.AddComponent<HudRoot>();
            hud.AddComponent<HudLab>().Steps = steps;
        }

        /// <summary>The HUD's PanelSettings (made the first time): the default runtime theme, scaled with the screen from 1440×900.</summary>
        static PanelSettings Panel()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel != null) return panel;
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1440, 900);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            AssetDatabase.CreateAsset(panel, PanelPath);
            AssetDatabase.SaveAssets();
            return panel;
        }
    }
}
