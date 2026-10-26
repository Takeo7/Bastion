using System;
using System.Collections.Generic;
using System.Linq;
using Bastion.Client;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The base's screens (strategy/screen.ts of the 0.10.1; docs/UNITY-ESTRATEGIA.md): a document over the clearing or
    /// the valley with the top strip, the proposal's plate, the tabs, the window and a short note; the screens' own
    /// panels go in `View`. The combat's menu and Ajustes, as they are. Driven by views (StrategyViews); the buttons go to
    /// `IStrategyHandlers`.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class StrategyRoot : MonoBehaviour
    {
        /// <summary>How long a short note stays (screen.ts toast: 2.6 s).</summary>
        const float ToastSeconds = 2.6f;

        readonly StrategyRelay relay = new StrategyRelay();
        VisualElement root;
        TooltipLayer tips;
        TopStrip top;
        NavTabs nav;
        ProposalPlate proposal;
        StrategyModal modal;
        GameMenu menu;
        SoldiersScreen soldiers;
        SkillBoardModal skillBoard;
        ArmoryScreen armory;
        SquadScreen squad;
        DebriefScreen debrief;
        MapScreen map;
        SettlementScreen settlement;
        RadioPanel radio;
        VisualElement radioCard;
        ValleyMap valley;
        ClearingLabels labels;
        Label toast;
        float toastUntil;
        HudIconTable icons;

        /// <summary>A sound of the interface by name (paper, bell): the game plays it.</summary>
        public event Action<string> Sound;
        /// <summary>The menu's choices (leave, quit) and Ajustes' changes, as in combat.</summary>
        public event Action<string> MenuChose;
        public event Action<string, float> SettingChanged;

        /// <summary>An icon by id: the game's own first, else Synty's table (Resources/HudIcons).</summary>
        public Func<string, Sprite> IconOf;

        /// <summary>A soldier's bust by id (Arte's Portraits.Soldier); null, a silhouette.</summary>
        public Func<string, Texture> PortraitOf = Portraits.Soldier;

        /// <summary>A base piece's picture by id, for the Asentamiento's palette (Arte's PiecePictures).</summary>
        public Func<string, Texture> PieceArtOf = PiecePictures.Of;

        /// <summary>A radio voice's bust by speaker (Arte's Portraits.Voice); null, their initials.</summary>
        public Func<string, Texture> VoiceOf = Portraits.Voice;

        /// <summary>
        /// Goes up when a bust is drawn (Portraits.Changed, once a frame at most): the screens that show busts take it into
        /// their keys, so the same view draws again with it.
        /// </summary>
        public static int PictureStamp { get; private set; }

        bool picturesChanged;
        SoldiersView lastSoldiers;
        SquadView lastSquad;
        DebriefView lastDebrief;
        SkillBoardView lastBoard;

        /// <summary>The radio's card: Radio (the controller) plays the scenes on it.</summary>
        public RadioPanel RadioCard => radio;

        /// <summary>
        /// Where the card of pad `i` of `n` of the line-up goes, in panel pixels (its top centre): Arte's
        /// Settlement.PadWorld projected; null, off screen.
        /// </summary>
        public Func<int, int, Vector2?> PadAt;

        /// <summary>The camera that shows the valley (the lab's shot camera); none, the valley's own.</summary>
        public Camera ValleyCamera;

        /// <summary>The proposal's clock now (the server's deadline): its words, how much is left (0–1), hurrying; null, none.</summary>
        public Func<(string text, float left, bool hurry)?> ClockOf;

        public bool Ready => root != null;

        /// <summary>Where the screens put their panels (left, right, the middle), under the strip and over the tabs.</summary>
        public VisualElement View => root?.Q("view");

        public bool MenuOpen => menu != null && (menu.Open || menu.Asking);
        public bool ModalOpen => modal != null && modal.Open;

        /// <summary>A text field has the keys (Personalizar's name): the base's letters are the field's (screen.ts onKey: input, textarea).</summary>
        public bool Typing => root?.focusController?.focusedElement is VisualElement f && (f is TextField || f.GetFirstAncestorOfType<TextField>() != null);

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var tree = Resources.Load<VisualTreeAsset>("Strategy/Strategy");
            if (tree == null)
            {
                Debug.LogError("Strategy: Resources/Strategy/Strategy.uxml missing");
                return;
            }
            doc.rootVisualElement.Clear();
            tree.CloneTree(doc.rootVisualElement);
            root = doc.rootVisualElement.Q("strat");
            icons = Resources.Load<HudIconTable>("HudIcons");
            tips = new TooltipLayer(root);
            top = new TopStrip(root.Q("top"), tips, Icon, relay);
            nav = new NavTabs(root.Q("nav"), tips, Icon, relay);
            proposal = new ProposalPlate(root.Q("proposal"), relay);
            modal = new StrategyModal(root.Q("modal"), relay, Icon, s => Sound?.Invoke(s), tips);
            menu = new GameMenu(root, tips, s => Sound?.Invoke(s));
            menu.Chose += what => MenuChose?.Invoke(what);
            menu.Changed += (id, value) => SettingChanged?.Invoke(id, value);
            soldiers = new SoldiersScreen(root.Q("view"), tips, Icon, relay) { PortraitOf = id => PortraitOf?.Invoke(id) };
            soldiers.Show(false);
            armory = new ArmoryScreen(root.Q("view"), tips, Icon, relay);
            armory.Show(false);
            squad = new SquadScreen(root.Q("view"), tips, relay, Icon);
            debrief = new DebriefScreen(root.Q("view"), tips, Icon, relay);
            foreach (var cards in new[] { squad.Cards, debrief.Cards })
            {
                cards.PadAt = (i, n) => PadAt != null ? PadAt(i, n) : ClearingPad(i, n);
                cards.PortraitOf = id => PortraitOf?.Invoke(id);
            }
            squad.Show(false);
            debrief.Show(false);
            // Under everything: the words by the pointer on the clearing and the names over it (the 0.10.1's, in the stage).
            labels = new ClearingLabels(root);
            map = new MapScreen(root.Q("view"), tips, Icon, relay);
            map.Show(false);
            settlement = new SettlementScreen(root.Q("view"), tips, Icon, relay) { ArtOf = id => PieceArtOf?.Invoke(id) };
            settlement.Show(false);
            // The radio over everything but the short note, as the 0.10.1 appended it (transmissions.ts; z-index 13).
            radioCard = Ui.El("radio panel");
            root.Insert(root.IndexOf(root.Q("toast")), radioCard);
            radio = new RadioPanel(radioCard, tips, s => Sound?.Invoke(s)) { VoiceOf = id => VoiceOf?.Invoke(id) };
            skillBoard = new SkillBoardModal(root.Q("board"), relay, s => Sound?.Invoke(s), tips) { PortraitOf = id => PortraitOf?.Invoke(id) };
            toast = root.Q<Label>("toast");
            Ui.Show(toast, false);
            Ui.Show(root.Q("proposal"), false);
            Ui.Show(root.Q("modal"), false);
            tips.Front();
            Portraits.Changed += OnPicture;
        }

        void OnDisable()
        {
            Portraits.Changed -= OnPicture;
            if (watched != null) watched.MapShown -= OnMapShown;
            watched = null;
            root = null;
        }

        void OnPicture(string id) => picturesChanged = true;

        /// <summary>The plates last given to the clearing, as text, to give them again only when they move.</summary>
        string insets = "";

        /// <summary>Where the plates sit, for the clearing's shots to keep clear of them (Arte's Settlement.SetInsets), when the layout changes.</summary>
        void Insets()
        {
            var clearing = Settlement.Instance;
            // Not before the screens are laid out (their bounds NaN): nothing is sent until there is something to send.
            if (clearing == null || root == null || float.IsNaN(root.layout.width) || root.layout.width <= 0) return;
            var panels = SettlementPanels();
            var now = string.Join(";", panels.Select(r => $"{r.x:0.000},{r.y:0.000},{r.width:0.000},{r.height:0.000}"));
            if (now == insets) return;
            insets = now;
            clearing.SetInsets(panels);
        }

        /// <summary>Place `i` of a row of `n` by La Lata on this panel (Arte's Settlement.PadWorld through its camera); null without it, or behind.</summary>
        Vector2? ClearingPad(int i, int n)
        {
            var clearing = Settlement.Instance;
            var camera = clearing != null ? clearing.Camera : null;
            if (camera == null || !camera.isActiveAndEnabled || root?.panel == null) return null;
            // By the camera's viewport over the whole panel: the screen's own height can differ from the camera's (a
            // capture drawing into a texture of its own).
            var vp = camera.WorldToViewportPoint(clearing.PadWorld(i, n));
            if (vp.z <= 0) return null;
            var size = root.panel.visualTree.layout.size;
            return new Vector2(vp.x * size.x, (1 - vp.y) * size.y);
        }

        /// <summary>A bust came: the screens that show busts drawn again with what they showed.</summary>
        void Repaint()
        {
            picturesChanged = false;
            PictureStamp++;
            if (lastSoldiers != null) soldiers.Set(lastSoldiers);
            if (lastSquad != null) squad.Set(lastSquad);
            if (lastDebrief != null) debrief.Set(lastDebrief);
            if (lastBoard != null) skillBoard.Set(lastBoard);
            radio.RepaintPortrait();
            Dress();
        }

        Sprite Icon(string id) => IconOf?.Invoke(id) ?? (icons != null ? icons.Get(id) : null);

        /// <summary>Who answers the buttons: the campaign's controller (or the lab); null, nobody.</summary>
        public void Bind(IStrategyHandlers handlers)
        {
            relay.Target = handlers ?? NoStrategyHandlers.Instance;
        }

        void Update()
        {
            if (root == null) return;
            if (toastUntil > 0 && Time.unscaledTime >= toastUntil)
            {
                toastUntil = 0;
                Ui.Show(toast, false);
            }
            if (picturesChanged) Repaint();
            Insets();
            radio.Tick(Time.unscaledDeltaTime);
            var clock = ClockOf?.Invoke();
            proposal.Tick(clock?.text, clock?.left ?? 0, clock?.hurry ?? false);
            modal.Tick(clock?.text, clock?.left ?? 0, clock?.hurry ?? false);
        }

        // ------------------------------------------------------- the views

        public void SetTop(TopView v) => top.Set(v);
        public void SetNav(NavView v) => nav.Set(v);

        public void SetProposal(ProposalView p, WaitingView waiting)
        {
            proposal.Set(p, waiting);
            Dress();
        }

        public void SetModal(StrategyModalView v)
        {
            modal.Set(v);
            Dress();
        }

        /// <summary>
        /// Which screen is up (geoscape, engineering, barracks, facilities, squad, debrief): its panels shown, the others'
        /// hidden.
        /// </summary>
        public void SetScreen(string view)
        {
            soldiers.Show(view == "barracks");
            armory.Show(view == "engineering");
            squad.Show(view == "squad");
            debrief.Show(view == "debrief");
            map.Show(view == "geoscape");
            settlement.Show(view == "facilities");
            // Where the radio sits goes with the screen (transmissions.css): the free corner of each.
            foreach (var v in new[] { "geoscape", "squad", "debrief", "barracks" }) radioCard.EnableInClassList("radio--" + v, view == v);
            Dress();
        }

        public void SetSoldiers(SoldiersView v)
        {
            lastSoldiers = v;
            soldiers.Set(v);
            Dress();
        }

        public void SetSkillBoard(SkillBoardView v)
        {
            lastBoard = v;
            skillBoard.Set(v);
            Dress();
        }

        public void SetArmory(ArmoryView v)
        {
            armory.Set(v);
            Dress();
        }

        public void SetSquad(SquadView v)
        {
            lastSquad = v;
            squad.Set(v);
            Dress();
        }

        public void SetDebrief(DebriefView v)
        {
            lastDebrief = v;
            debrief.Set(v);
            Dress();
        }

        public void SetSettlement(SettlementView v)
        {
            settlement.Set(v);
            Dress();
        }

        /// <summary>
        /// The big plates (the side columns, the Asentamiento's, «no hay misión», the windows, the board)
        /// in their steel frame with rivets; the proposal riveted; what to press next slanted; the recruit's card dashed
        /// (mixta.css: border-image, --m-rivets, clip-path; strategy.css .recruit-card). Once each, after every change,
        /// since the screens make some of them again.
        /// </summary>
        /// <summary>A key for the radio (escape, enter, space): true if it took it (the base's Esc then does nothing else).</summary>
        public bool RadioKey(string key) => radio != null && radio.Key(key);

        /// <summary>
        /// What a «Ver» points at, or what just changed there, blinks (screen.ts blink; strategy.css .blink: three times in
        /// 0.45 s, an outline and a wash in the warning's colour), so it is found even when it is already on screen. By
        /// class, once drawn (the 0.10.1 waited 60 ms).
        /// </summary>
        public void Blink(params string[] classes)
        {
            if (root == null) return;
            root.schedule.Execute(() =>
            {
                foreach (var name in classes)
                {
                    var shown = root.Query(className: name).ToList().Where(el => el.resolvedStyle.display != DisplayStyle.None && el.panel != null).ToList();
                    foreach (var el in shown) BlinkOn(el);
                    // What blinks, said (a class that blinks nothing, renamed or not drawn, is noticed).
                    Debug.Log($"[estrategia] parpadea «{name}»: {shown.Count}");
                }
            }).ExecuteLater(60);
        }

        /// <summary>
        /// A key waiting for this player's yes pulses while it is up (strategy.css .x-btn.waiting: a ring in the stamp's red,
        /// every 1.4 s): its own edge, the slant's (--slant-edge, as Escuadra's «Lanzar» outlined), red and back, for as long
        /// as it is drawn. A frame round it would be cut by the key and square on a slant.
        /// </summary>
        public static void Pulse(VisualElement el)
        {
            // On the game's clock (the base's runs at 1), so a capture's frames see its beat as the app does.
            var start = Time.time;
            el.schedule.Execute(() =>
            {
                var t = Mathf.Repeat(Time.time - start, PulseSeconds);
                el.EnableInClassList("btn-pulse--on", t >= PulseSeconds / 4 && t < PulseSeconds * 3 / 4);
            }).Every(16).Until(() => el.panel == null);
        }

        /// <summary>A pulse's beat (strategy.css x-pulse: 1.4 s, the ring at its middle) and a blink's (x-blink: 0.45 s, three).</summary>
        const float PulseSeconds = 1.4f;
        const float BlinkSeconds = 0.45f;

        /// <summary>
        /// A frame over `el` (USS has no outline: one that takes no room), easing in and out three times (x-blink:
        /// ease-in-out, strongest at the middle of each), then gone. On the game's clock, as Pulse.
        /// </summary>
        static void BlinkOn(VisualElement el)
        {
            el.Q(className: "blink")?.RemoveFromHierarchy();
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("blink");
            frame.style.opacity = 0;
            el.Add(frame);
            var start = Time.time;
            frame.schedule.Execute(() =>
            {
                var t = Time.time - start;
                if (t >= BlinkSeconds * 3) frame.RemoveFromHierarchy();
                else frame.style.opacity = 0.5f - 0.5f * Mathf.Cos(2 * Mathf.PI * t / BlinkSeconds);
            }).Every(16).Until(() => frame.parent == null);
        }

        /// <summary>Esc on Soldados: the choice open on the sheet (Mano, Cinto, the slots), closed; whether there was one.</summary>
        public bool CloseSoldierChoice() => soldiers != null && soldiers.CloseChoice();

        /// <summary>Esc on the board while a cell is held: the purchase let go, the board stays; whether one was held.</summary>
        public bool CancelBoardHold() => skillBoard != null && skillBoard.CancelHold();

        /// <summary>The choice open on Soldados' sheet; null, none (tests). Opening one, as its head's click.</summary>
        public string SoldierChoice => soldiers?.OpenChoice;
        public void OpenSoldierChoice(string id) => soldiers?.Open(id);
        /// <summary>An element's help shown now and kept (the lab's captures, TooltipLayer.Pin).</summary>
        public void PinTip(VisualElement target) => tips?.Pin(target);

        void Dress()
        {
            if (root == null) return;
            PlateFrame.Dress(radioCard, false);
            foreach (var cls in FramedPlates) root.Query(className: cls).ForEach(e => PlateFrame.Dress(e));
            root.Query(className: "strat-proposal").ForEach(e => PlateFrame.Dress(e, false));
            root.Query<Button>(className: "btn-primary").ForEach(Slant.Dress);
            root.Query(className: "recruit-card").ForEach(DashedEdge.Dress);
        }

        /// <summary>The plates the 0.10.1 framed (.strat-left, .strat-right, .strat-center-top, the windows); not the mission's nor the debrief's, which sit in .squad-bottom.</summary>
        static readonly string[] FramedPlates = { "strat-side", "settle-plate", "squad-empty", "strat-modal__card", "board-modal" };

        /// <summary>
        /// The Asentamiento's plates on screen, as fractions of it (0 to 1, from the top left): what Arte's camera
        /// leaves the clearing clear of (Settlement.SetInsets). Fractions, so they hold at any size, the captures' too.
        /// </summary>
        public List<Rect> SettlementPanels()
        {
            var out_ = new List<Rect>();
            var size = root?.panel?.visualTree.layout.size ?? Vector2.zero;
            if (settlement == null || size.x <= 0 || size.y <= 0) return out_;
            void Add(Rect r)
            {
                // Laid out only: before its first layout an element's bounds are NaN.
                if (!float.IsNaN(r.x) && !float.IsNaN(r.y) && r.width >= 4 && r.height >= 4) out_.Add(new Rect(r.x / size.x, r.y / size.y, r.width / size.x, r.height / size.y));
            }
            foreach (var r in settlement.Plates()) Add(r);
            // The other screens' (stage.ts PANELS: the strip, the tabs, the side columns, the Escuadra's and the debrief's foot).
            foreach (var cls in ShotPanels)
                root.Query(className: cls).ForEach(e =>
                {
                    if (Shown(e) && !e.ClassListContains("settle-plate")) Add(e.worldBound);
                });
            return out_;
        }

        /// <summary>stage.ts PANELS: what a fitted shot of the clearing keeps clear of.</summary>
        static readonly string[] ShotPanels = { "strat-top", "strat-nav", "strat-side", "squad-bottom", "debrief-card" };

        /// <summary>On screen: it and all that holds it laid out (a hidden screen's panels keep their own display).</summary>
        static bool Shown(VisualElement e)
        {
            for (var at = e; at != null; at = at.parent)
                if (at.resolvedStyle.display == DisplayStyle.None || !at.visible) return false;
            return true;
        }

        /// <summary>
        /// Walking about the clearing (Arte's Settlement.WalkAbout; walkAbout.ts): every panel over the view hidden (stage.ts
        /// PANELS) and, at the bottom in the middle, how to walk; all back as it ends.
        /// </summary>
        public void Walking(bool on)
        {
            if (root == null) return;
            if (walkHint == null)
            {
                walkHint = Ui.Text(StrategyText.WalkHint, "walk-hint", root);
                walkHint.pickingMode = PickingMode.Ignore;
            }
            root.EnableInClassList("strat--walking", on);
        }

        Label walkHint;

        /// <summary>What the pointer would do on the clearing, by it (a screen point, y up): its cost, or why not (`bad`); null, none.</summary>
        public void ClearingHint(string text, bool bad, Vector2 screen) => labels?.Hint(text, bad, screen, Settlement.Instance?.Camera);

        /// <summary>The pointer moved and nothing else: the words go with it.</summary>
        public void ClearingHintAt(Vector2 screen) => labels?.HintAt(screen, Settlement.Instance?.Camera);

        /// <summary>The names over the clearing (the partner's, the siege's defenders), placed through its camera.</summary>
        public void ClearingTags(IReadOnlyList<ClearingTag> tags) => labels?.Tags(tags, Settlement.Instance?.Camera);

        /// <summary>
        /// Whether the pointer is over the base's panels at a screen point (Input System coordinates, y up): then a click
        /// or the wheel isn't the clearing's (Arte's camera rig, the building's pointer).
        /// </summary>
        public bool Over(Vector2 screen)
        {
            if (root?.panel == null) return false;
            // By the size of the clearing's picture, which is the screen's in the game and the shot's texture in the
            // captures (Shots draws the cameras into one of its own): the same point the pointer's words take.
            var camera = Settlement.Instance?.Camera;
            var w = camera != null ? camera.pixelWidth : Screen.width;
            var h = camera != null ? camera.pixelHeight : Screen.height;
            var size = root.panel.visualTree.layout.size;
            var p = new Vector2(screen.x / Mathf.Max(1, w) * size.x, (1 - screen.y / Mathf.Max(1, h)) * size.y);
            var picked = root.panel.Pick(p);
            return picked != null && picked.pickingMode == PickingMode.Position && picked != root && picked != root.parent;
        }

        /// <summary>The Mapa: the valley is made the first time (its painting, the Marea's field, its camera).</summary>
        public void SetMap(MapView v)
        {
            if (valley == null && v != null)
            {
                valley = new GameObject("Valle").AddComponent<ValleyMap>();
                valley.transform.SetParent(transform, false);
                valley.Build(ValleyCamera);
            }
            if (valley != null && !map.Bound) map.Bind(valley);
            WatchClearing();
            map.Set(v);
            Dress();
        }

        /// <summary>The clearing whose fade into the Mapa and out of it says when the valley takes the screen.</summary>
        Settlement watched;

        /// <summary>
        /// Arte's clearing, when there is one, says when the valley takes the screen: at the bottom of its fade to black
        /// (Settlement.MapShown), the clearing's camera off or on; till then the camera that was showing stays.
        /// </summary>
        void WatchClearing()
        {
            var clearing = Settlement.Instance;
            if (clearing == watched) return;
            if (watched != null) watched.MapShown -= OnMapShown;
            watched = clearing;
            if (clearing != null) clearing.MapShown += OnMapShown;
            map.ValleyUp(clearing != null ? clearing.MapOn : (bool?)null);
        }

        void OnMapShown(bool on) => map?.ValleyUp(on);
        public void ToggleMenu(GameMenuView v) => menu.Toggle(v);
        public void CloseMenu() => menu.Close();
        public bool MenuBack() => menu != null && menu.Back();
        public void MenuPage(string page) => menu.Go(page);

        /// <summary>
        /// Take-off (screen.ts launch): every panel fades away but the stamp, «EN MARCHA» (a siege: «YA ESTÁN AQUÍ»), while
        /// the clearing plays it; false, the screens back.
        /// </summary>
        public void Launching(string banner)
        {
            if (root == null) return;
            var on = !string.IsNullOrEmpty(banner);
            root.EnableInClassList("strat--launching", on);
            root.Q(className: "strat-launch")?.RemoveFromHierarchy();
            if (!on) return;
            menu.Close();
            var stamp = Ui.El("strat-launch", root);
            stamp.pickingMode = PickingMode.Ignore;
            Ui.Text(banner.ToUpperInvariant(), "strat-launch__text font-title", stamp);
        }

        /// <summary>A short note at the bottom (the server's «rejected», a proposal's cost): gone after 2.6 s.</summary>
        public void Toast(string text)
        {
            if (toast == null) return;
            toast.text = text;
            Ui.Show(toast, true);
            toastUntil = Time.unscaledTime + ToastSeconds;
        }
    }
}
