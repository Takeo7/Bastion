using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The combat HUD on a UIDocument (docs/UNITY-UI.md): it clones Resources/Hud/Hud.uxml into the document, makes a
    /// piece for each holder and hands each its view, as hud.ts's set… methods did. It doesn't read the game: the
    /// presenter (or the lab) calls it with views, and binds the handlers that answer its buttons. Each frame it puts
    /// the flags over the units, turns the goal's arrow and the siege's compass with the camera.
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
        GoalArrow goalArrow;
        SightButton sight;
        MiniMap miniMap;
        Banner banner;
        Cards cards;
        Notes notes;
        BattleWindow window;
        GameMenu menu;
        VisualElement abilitiesHost;
        VisualElement squadPlate;
        VisualElement actions;
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
            strip = new TurnStrip(root, tips);
            // The squad's plate, bottom left (UI-COMBATE UI-01): the sheet over the cards, the backpack over the sheet's end.
            // mixta.css: one of the big plates, in the steel frame with rivets (Arturo, 24/10: as the 0.10.1).
            squadPlate = root.Q("squad-plate");
            PlateFrame.Dress(squadPlate);
            squad = new SquadList(root.Q("squad-cards"), squadPlate, relay, tips);
            unit = new UnitPanel(root.Q("unit-panel"), root.Q("pack"), relay, tips, Icon);
            abilitiesHost = root.Q("abilities");
            bar = new ActionBar(abilitiesHost, relay, tips, Icon);
            shot = new ShotPanel(root.Q("shot-panel"), relay, tips);
            turnActions = new TurnActions(root.Q("end-slot"), root.Q("clocks"), relay, tips, Icon);
            // The keys' row in the screen's middle, but clear of the squad's plate when it grows (eight cards).
            actions = root.Q("actions");
            squadPlate.RegisterCallback<GeometryChangedEvent>(_ => PlaceActions());
            actions.RegisterCallback<GeometryChangedEvent>(_ => PlaceActions());
            root.RegisterCallback<GeometryChangedEvent>(_ => PlaceActions());
            cards = new Cards(root.Q("cards"), relay, s => Sound?.Invoke(s));
            goal = new GoalCard(root.Q("goal"), root.Q("compass"), tips, ReopenIntro, root.Q("edge-marks"));
            log = new Log(root.Q("log"), Icon);
            flags = new UnitFlags(root.Q("flags"), Icon);
            flags.TargetPicked += id => TargetPicked?.Invoke(id);
            edges = new EdgeMarks(root.Q("edge-marks"), tips, at => Look?.Invoke(at));
            goalArrow = new GoalArrow(root.Q("goal-arrow"), tips);
            sight = new SightButton(root.Q("sight"), tips);
            sight.Toggled += () => SightToggled?.Invoke();
            miniMap = new MiniMap(root.Q("mini-map"));
            banner = new Banner(root.Q("banner"), relay, s => Sound?.Invoke(s));
            notes = new Notes(root.Q("notes"));
            // A menu open on the bar (the guns, Lanzar…): the notes go up over it, not on its keys (Ana, Arturo 31/10 #2).
            bar.MenuShown += open => root.Q("notes").EnableInClassList("notes--over-menu", open);
            window = new BattleWindow(root, tips, s => Sound?.Invoke(s));
            menu = new GameMenu(root, tips, s => Sound?.Invoke(s));
            menu.Chose += what => MenuChose?.Invoke(what);
            menu.Changed += (id, value) =>
            {
                // «Cámara al soldado» is the HUD's own, kept on this computer (it was its switch: UI-07).
                if (id == GameMenu.FollowId)
                {
                    TurnActions.FollowsTurn = value != 0;
                    relay.OnFollowTurn(value != 0);
                }
                else SettingChanged?.Invoke(id, value);
            };
            tips.Front();
            cards.IntroClosed += () => goal.SetWaiting(false);
            // A press anywhere on the HUD outside the backpack closes its menu.
            root.RegisterCallback<PointerDownEvent>(e => unit.PressedAt(e.target as VisualElement), TrickleDown.TrickleDown);
        }

        void OnDisable()
        {
            // The window stops listening for busts (a static event) as the HUD goes.
            window?.Close();
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
            turnActions.TickClock(ClockOf?.Invoke(0));
            goalArrow.Follow(cam);
            if (cam != null) goal.TurnCompass(NorthOnScreen(cam));
        }

        /// <summary>The keys' row with its middle at the screen's, or further right while the squad's plate would reach it.</summary>
        void PlaceActions()
        {
            var width = root?.layout.width ?? float.NaN;
            var row = actions.layout.width;
            if (float.IsNaN(width) || float.IsNaN(row)) return;
            // The plate's frame stands 8 px out of it (PlateFrame).
            var plate = squadPlate.resolvedStyle.display == DisplayStyle.None ? 0 : squadPlate.layout.xMax + 8 + 12;
            var left = Mathf.Round(Mathf.Max(width / 2, plate + row / 2));
            // The row's width changes by a pixel with where it stands (its keys snapped to whole pixels): moved for that,
            // it was laid out again and again, the left and the width taking turns (the 0.12.9: «Layout update is
            // struggling», hundreds a second, with the partner's cards widening the plate). Less than two pixels, it stays.
            if (!float.IsNaN(actionsLeft) && Mathf.Abs(left - actionsLeft) < 2) return;
            actionsLeft = left;
            actions.style.left = left;
        }

        /// <summary>Where PlaceActions last put the keys' row (panel pixels); NaN, not yet.</summary>
        float actionsLeft = float.NaN;

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

        /// <summary>The players at the top.</summary>
        public void SetPlayers(PlayersView v) => strip.SetPlayers(v);

        public void SetSquad(SquadView v) => squad.Set(v);

        /// <summary>A soldier's bust by id for the squad list (Arte's Portraits.Soldier); null, none yet.</summary>
        public Func<string, Texture> PortraitOf
        {
            set => squad.PortraitOf = value;
        }

        /// <summary>A bust was drawn: the squad list drawn again with it.</summary>
        public void PicturesChanged() => squad?.Redraw();
        public void SetSelected(UnitPanelView v) => unit.Set(v);
        public void SetAbilities(ActionBarView v) => bar.Set(v);
        public void SetEndTurn(EndTurnView v) => turnActions.SetEndTurn(v);
        public void SetMission(MissionView v) => goal.Set(v);
        public void SetCompass(CompassView v) => goal.SetCompass(v);
        public void SetFlags(FlagsView v) => flags.Set(v);
        public void SetWorldTags(System.Collections.Generic.List<WorldTagView> v) => flags.SetTags(v);
        public void SetEdgeMarks(EdgeMarksView v) => edges.Set(v);

        /// <summary>Where the mission's goal lies, for the ring's arrow (UI-06); null, none.</summary>
        public void SetGoal(Vector3? world) => goalArrow.Set(world);

        /// <summary>The enemies' sight's button and legend (Arturo, 5/11 #6, #9).</summary>
        public void SetSight(SightView v) => sight?.Set(v);

        /// <summary>The enemies' sight's button pressed.</summary>
        public event Action SightToggled;

        /// <summary>Another target's odds clicked over its flag (Arturo, 5/11 #11): its id.</summary>
        public event Action<string> TargetPicked;

        /// <summary>An arrow at the screen's edge clicked: the camera to that point.</summary>
        public event Action<Vector3> Look;
        /// <summary>The camera up over the map (#12): its strip shown or not.</summary>
        public void SetMiniMap(bool open) => miniMap?.Set(open);
        public void SetCall(CallView v) => cards.SetCall(v);
        public void SetOutcome(OutcomeView v) => cards.SetOutcome(v);

        /// <summary>The aim's panel; a self ability's sits over its key (the bar's active one).</summary>
        public void SetTarget(ShotView v)
        {
            shot.Set(v, abilitiesHost.Q(className: "ability--active"));
        }

        /// <summary>The camera switch («Cámara al soldado», in the game's menu), as last left on this computer.</summary>
        public bool FollowsTurn => TurnActions.FollowsTurn;

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

        public bool CloseIntro(bool quiet = false) => cards.CloseIntro(quiet);

        /// <summary>The arrival card is up (its keys are its own: Intro and Space close it, Esc does nothing).</summary>
        public bool IntroOpen => cards.IntroOpen;

        /// <summary>The squad is on its way back to the base (this player's «Volver a la base», or the partner's).</summary>
        public void Returning() => cards.Returning();

        /// <summary>The mission's window as it stands now (the controller's), for the objective's card to open again; null, none.</summary>
        public Func<IntroView> IntroNow;

        /// <summary>
        /// The objective's card pressed: the mission's window again, its whole text as it stands now, and the card stays
        /// where it is (Ana, Arturo 31/10 #13: it went, with nothing open, in a mission taken up again, which had shown none).
        /// </summary>
        void ReopenIntro() => cards.ReopenIntro(IntroNow?.Invoke());

        public void Log(string text, string color = null, Action see = null) => log.Add(text, color, see);
        public void ClearLog() => log.Clear();
        public void Toast(string text) => notes.Toast(text);

        /// <summary>The window over the battle (BattleWindow): a soldier's sheet, an enemy's file; open; closed (whether it was).</summary>
        public void ShowSheet(SoldierSheetView v) => window.Sheet(v);
        public void ShowEnemyFile(EnemyFileView v) => window.File(v);
        public bool WindowOpen => window != null && window.Open;
        public bool CloseWindow() => window != null && window.Close();
        /// <summary>The short note gone, if it still says `text`.</summary>
        public void ClearToast(string text) => notes.ClearToast(text);
        public void Notice(string text, string color = null, Action onSee = null)
        {
            notes.Notice(text, color, onSee);
            // The partner's key moment: a crackle of the radio (hud.ts notice).
            if (!string.IsNullOrEmpty(text)) Sound?.Invoke("notice");
        }
        /// <summary>A find left on the floor with the backpack full (hud.ts packOffer): its line, with «Cambiar» for 7 s.</summary>
        public void PackOffer(string text, Action swap) => notes.Offer(text, HudText.Pack.Swap, swap);

        /// <summary>A word or number rising over a point of the world (damage, «ATURDIDO»…).</summary>
        public void Float(Vector3 world, string text, string kind, string iconId = null) => flags.Float(world, text, kind, iconId);

        /// <summary>An enemy just seen: a mark on its flag for 2 s, an eye if it hasn't noticed the squad, a «!» if it has (UnitFlags.Spot).</summary>
        public void Spot(string id, bool alert) => flags.Spot(id, alert);

        /// <summary>For the labs: the help of the first element of class `className` pinned open; whether there was one.</summary>
        public bool PinTip(string className)
        {
            var over = root?.Q(className: className);
            if (over != null) tips?.Pin(over);
            return over != null;
        }

        /// <summary>One short line on the tile under the pointer, or none (null).</summary>
        public void SetNote(Vector3? world, string text, string icon = null) => flags.SetNote(world, text, icon);

        /// <summary>The backpack's pockets shown or hidden, as its mark's click (the labs: PackSteps «pack»).</summary>
        public void TogglePockets() => unit.TogglePockets();

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
        /// <summary>The partner's menu open: the battle dimmed under a card saying so; null, gone.</summary>
        public void SetPause(string line, string sub) => menu?.SetPause(line, sub);
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
