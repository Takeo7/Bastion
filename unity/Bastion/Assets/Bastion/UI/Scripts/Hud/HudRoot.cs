using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The combat HUD on a UIDocument (docs/UNITY-UI.md): it clones Resources/Hud/Hud.uxml into the document, makes a
    /// piece for each holder and hands each its view, as hud.ts's set… methods did. It doesn't read the game: the
    /// presenter (or the lab) calls it with views, and binds the handlers that answer its buttons. Each frame it puts
    /// the flags over the units and turns the siege's compass with the camera.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudRoot : MonoBehaviour
    {
        /// <summary>The camera the flags follow and the compass turns with; the main one if none is set.</summary>
        [SerializeField] Camera worldCamera = null;

        /// <summary>
        /// The map's north in the world (the engine's y−): +z in Unity (Arte, docs/UNITY-ARTE.md §7: east +x, north +z, the
        /// map's y to −z, so it isn't mirrored in a left-handed world; on the web it was −z).
        /// </summary>
        [SerializeField] Vector3 north = Vector3.forward;

        readonly HandlerRelay relay = new HandlerRelay();
        VisualElement root;
        TooltipLayer tips;
        TurnStrip strip;
        SquadList squad;
        UnitPanel unit;
        ActionBar bar;
        ShotPanel shot;
        TurnActions turnActions;
        GoalCard goal;
        Log log;
        UnitFlags flags;
        EdgeMarks edges;
        Banner banner;
        Cards cards;
        Notes notes;
        GameMenu menu;
        VisualElement abilitiesHost;
        HudIconTable icons;

        /// <summary>A sound of the interface by name (stamp, confirm, paper): the game plays it (Red's).</summary>
        public event Action<string> Sound;

        /// <summary>The point over a unit's head in the world, for its flag; null if it has no body to follow.</summary>
        public Func<string, Vector3?> HeadOf;

        /// <summary>The players' turn clocks now: this player's (0) and the partner's (1); null outside the squad's turn.</summary>
        public Func<int, ClockView> ClockOf;

        /// <summary>An ability's icon by its id («weapon:rifle» for Disparar's gun): the Synty INTERFACE sprites.</summary>
        public Func<string, Sprite> IconOf;

        public bool Ready => root != null;

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var tree = Resources.Load<VisualTreeAsset>("Hud/Hud");
            if (tree == null)
            {
                Debug.LogError("HUD: Resources/Hud/Hud.uxml missing");
                return;
            }
            doc.rootVisualElement.Clear();
            tree.CloneTree(doc.rootVisualElement);
            root = doc.rootVisualElement.Q("hud");
            icons = Resources.Load<HudIconTable>("HudIcons");
            tips = new TooltipLayer(root);
            strip = new TurnStrip(root, relay, tips, Icon);
            squad = new SquadList(root.Q("squad"), relay, tips);
            unit = new UnitPanel(root.Q("unit-panel"), relay, tips, Icon);
            abilitiesHost = root.Q("abilities");
            bar = new ActionBar(abilitiesHost, relay, tips, Icon);
            shot = new ShotPanel(root.Q("shot-panel"), relay, tips);
            turnActions = new TurnActions(root.Q("turn-actions"), relay, tips, Icon);
            cards = new Cards(root.Q("cards"), relay, s => Sound?.Invoke(s));
            goal = new GoalCard(root.Q("goal"), root.Q("compass"), tips, ReopenIntro);
            log = new Log(root.Q("log"), Icon);
            flags = new UnitFlags(root.Q("flags"));
            edges = new EdgeMarks(root.Q("edge-marks"), tips, at => Look?.Invoke(at));
            banner = new Banner(root.Q("banner"), relay, s => Sound?.Invoke(s));
            notes = new Notes(root.Q("notes"));
            menu = new GameMenu(root, tips, s => Sound?.Invoke(s));
            menu.Chose += what => MenuChose?.Invoke(what);
            menu.Changed += (id, value) => SettingChanged?.Invoke(id, value);
            tips.Front();
            cards.IntroClosed += () => goal.SetWaiting(false);
            var menuButton = root.Q<Button>("menu-button");
            menuButton.text = HudText.Menu.Label;
            menuButton.clicked += () => relay.OnMenu();
            tips.Attach(menuButton, HudText.Menu.Tip);
            // A press anywhere on the HUD outside the backpack closes its menu.
            root.RegisterCallback<PointerDownEvent>(e => unit.PressedAt(e.target as VisualElement), TrickleDown.TrickleDown);
        }

        void OnDisable()
        {
            root = null;
        }

        /// <summary>An icon by id: the game's own (IconOf) first, else the table of Synty's (Resources/HudIcons).</summary>
        Sprite Icon(string id) => IconOf?.Invoke(id) ?? (icons != null ? icons.Get(id) : null);

        /// <summary>Who answers the HUD's buttons: the combat controller (or the lab); null, nobody.</summary>
        public void Bind(IHudHandlers handlers)
        {
            relay.Target = handlers ?? NoHandlers.Instance;
        }

        void LateUpdate()
        {
            if (root == null) return;
            var cam = worldCamera != null ? worldCamera : Camera.main;
            if (HeadOf != null) flags.Follow(cam, HeadOf);
            edges.Follow(cam);
            turnActions.TickClocks(ClockOf?.Invoke(0), ClockOf?.Invoke(1));
            if (cam != null) goal.TurnCompass(NorthOnScreen(cam));
        }

        /// <summary>
        /// Where the map's north is on the screen, clockwise from up, as seen (game.ts frame: the ground in perspective;
        /// the camera's heading alone was off by some 7°): a point on the ground ahead and one a metre north of it,
        /// projected.
        /// </summary>
        float NorthOnScreen(Camera cam)
        {
            var f = cam.transform.forward;
            if (f.y > -0.05f) return 0;
            var ground = cam.transform.position + f * (-cam.transform.position.y / f.y);
            var a = cam.WorldToScreenPoint(ground);
            var b = cam.WorldToScreenPoint(ground + north.normalized);
            // Screen y goes up here: clockwise from up is atan2(dx, dy).
            return Mathf.Atan2(b.x - a.x, b.y - a.y);
        }

        // ------------------------------------------------------- the views

        public void SetTurn(TurnView v) => strip.SetTurn(v);
        public void SetPlayers(PlayersView v) => strip.SetPlayers(v);
        public void SetFloor(FloorView v) => strip.SetFloor(v);
        public void SetSquad(SquadView v) => squad.Set(v);
        public void SetSelected(UnitPanelView v) => unit.Set(v);
        public void SetAbilities(ActionBarView v) => bar.Set(v);
        public void SetEndTurn(EndTurnView v) => turnActions.SetEndTurn(v);
        public void SetCommand(CommandView v) => turnActions.SetCommand(v);
        public void SetGoTo(string name) => turnActions.SetGoTo(name);
        public void SetMission(MissionView v) => goal.Set(v);
        public void SetCompass(CompassView v) => goal.SetCompass(v);
        public void SetFlags(FlagsView v) => flags.Set(v);
        public void SetEdgeMarks(EdgeMarksView v) => edges.Set(v);

        /// <summary>An arrow at the screen's edge clicked: the camera to that point.</summary>
        public event Action<Vector3> Look;
        public void SetCall(CallView v) => cards.SetCall(v);
        public void SetOutcome(OutcomeView v) => cards.SetOutcome(v);

        /// <summary>The aim's panel; a self ability's sits over its key (the bar's active one).</summary>
        public void SetTarget(ShotView v)
        {
            shot.Set(v, abilitiesHost.Q(className: "ability--active"));
        }

        /// <summary>The camera switch («Cámara al soldado»), as last left on this computer.</summary>
        public bool FollowsTurn => turnActions.FollowsTurn;

        /// <summary>A menu of the bar is open: the number keys pick its choices.</summary>
        public bool AbilityMenuOpen => bar.MenuOpen;
        public bool PickMenu(int n) => bar.PickMenu(n);
        public bool CloseAbilityMenu() => bar.CloseMenu();
        public void ToggleAbilityMenu(string id) => bar.ToggleMenu(id);

        /// <summary>The backpack's menu (Backpack): open; the swap for a thing on the floor opened; closed (Esc, a press on the map).</summary>
        public bool PackMenuOpen => unit != null && unit.PackMenuOpen;
        public void OpenSwap(string unitId, string id) => unit.OpenSwap(unitId, id);
        public bool ClosePackMenu() => unit != null && unit.ClosePackMenu();

        // ------------------------------------------------------ the moments

        public void Banner(string text, string kind = "xcom", string detail = null, bool held = false) => banner.Show(text, kind, detail, held);
        public bool ReleaseBanner(bool quiet = false) => banner.Release(quiet);

        /// <summary>Whether a screen point (y up) is on the turn's stamp: a click there only lets it go, with its sound (Banner).</summary>
        public bool OverBanner(Vector2 screen)
        {
            var picked = PickAt(screen);
            var stamp = root?.Q("banner");
            return picked != null && stamp != null && (picked == stamp || stamp.Contains(picked));
        }

        /// <summary>The arrival card; the objective's card waits under it until it closes.</summary>
        public void Intro(IntroView v)
        {
            goal.SetWaiting(v != null);
            cards.ShowIntro(v);
        }

        public bool CloseIntro() => cards.CloseIntro(false);

        /// <summary>The arrival card is up (its keys are its own: Intro and Space close it, Esc does nothing).</summary>
        public bool IntroOpen => cards.IntroOpen;

        /// <summary>The squad is on its way back to the base (this player's «Volver a la base», or the partner's).</summary>
        public void Returning() => cards.Returning();

        void ReopenIntro()
        {
            goal.SetWaiting(true);
            cards.ReopenIntro();
        }

        public void Log(string text, string color = null) => log.Add(text, color);
        public void ClearLog() => log.Clear();
        public void Toast(string text) => notes.Toast(text);
        /// <summary>The short note gone, if it still says `text`.</summary>
        public void ClearToast(string text) => notes.ClearToast(text);
        public void Notice(string text, string color = null, Action onSee = null) => notes.Notice(text, color, onSee);
        /// <summary>A find left on the floor with the backpack full (hud.ts packOffer): its line, with «Cambiar» for 7 s.</summary>
        public void PackOffer(string text, Action swap) => notes.Offer(text, HudText.Pack.Swap, swap);

        /// <summary>A word or number rising over a point of the world (damage, «ATURDIDO»…).</summary>
        public void Float(Vector3 world, string text, string kind) => flags.Float(world, text, kind);

        /// <summary>One short line on the tile under the pointer, or none (null).</summary>
        public void SetNote(Vector3? world, string text) => flags.SetNote(world, text);

        /// <summary>A tag over a point for a while (whose ping it is), on a plate of `color`.</summary>
        public void Pin(Vector3 world, string text, string color, float seconds = 4) => flags.Pin(world, text, color, seconds);

        /// <summary>The shot over the shoulder: only the target's flag stays.</summary>
        public void SetCloseShot(bool on) => flags.SetCloseShot(on);

        /// <summary>Something the game put up covers the field to be read (the stamp held, the call, the arrival): the turn clock waits.</summary>
        public bool Covering => banner.Covering || cards.Covering;

        // -------------------------------------------------------- the menu

        /// <summary>The game's menu or a question is up: the keys and the pointer are theirs.</summary>
        public bool MenuOpen => menu != null && (menu.Open || menu.Asking);

        /// <summary>The game's menu opened on its first page with `v`, or closed.</summary>
        public void ToggleMenu(GameMenuView v) => menu.Toggle(v);
        public void CloseMenu() => menu.Close();

        /// <summary>The open menu on a page (main, settings, controls): for the labs.</summary>
        public void MenuPage(string page) => menu.Go(page);

        /// <summary>Esc: the question, then the menu; whether there was one to close.</summary>
        public bool MenuBack() => menu != null && menu.Back();

        /// <summary>A question before going; `yes` if it is answered so.</summary>
        public void Confirm(ConfirmView v, Action yes) => menu.Confirm(v, yes);

        /// <summary>An entry of the menu chosen (GameMenu.Leave, Abandon, Quit).</summary>
        public event Action<string> MenuChose;

        /// <summary>A setting of Ajustes changed: its id and value (a choice's index, or the slider's).</summary>
        public event Action<string, float> SettingChanged;

        /// <summary>Whether the pointer is over the HUD at a screen point (Input System coordinates, y up): then a click isn't the map's.</summary>
        public bool Over(Vector2 screen)
        {
            var picked = PickAt(screen);
            return picked != null && picked.pickingMode == PickingMode.Position && picked != root;
        }

        /// <summary>
        /// What is on the panel at a screen point (y up), by the size of the game camera's picture: the screen's in the game,
        /// the shot's texture in the captures (Shots draws the cameras into one of its own).
        /// </summary>
        VisualElement PickAt(Vector2 screen)
        {
            if (root?.panel == null) return null;
            var camera = Camera.main;
            var w = camera != null ? camera.pixelWidth : Screen.width;
            var h = camera != null ? camera.pixelHeight : Screen.height;
            var size = root.panel.visualTree.layout.size;
            return root.panel.Pick(new Vector2(screen.x / Mathf.Max(1, w) * size.x, (1 - screen.y / Mathf.Max(1, h)) * size.y));
        }
    }
}
