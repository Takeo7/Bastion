using System;
using System.Collections.Generic;
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
        ValleyMap valley;
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
        public Func<string, Texture> PortraitOf;

        /// <summary>A base piece's picture by id, for the Asentamiento's palette (Arte's renders); null, the 0.10.1's.</summary>
        public Func<string, Texture> PieceArtOf;

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
            modal = new StrategyModal(root.Q("modal"), relay, Icon, s => Sound?.Invoke(s));
            menu = new GameMenu(root, tips, s => Sound?.Invoke(s));
            menu.Chose += what => MenuChose?.Invoke(what);
            menu.Changed += (id, value) => SettingChanged?.Invoke(id, value);
            soldiers = new SoldiersScreen(root.Q("view"), tips, Icon, relay) { PortraitOf = id => PortraitOf?.Invoke(id) };
            soldiers.Show(false);
            armory = new ArmoryScreen(root.Q("view"), tips, Icon, relay);
            armory.Show(false);
            squad = new SquadScreen(root.Q("view"), tips, relay);
            debrief = new DebriefScreen(root.Q("view"), tips, Icon, relay);
            foreach (var cards in new[] { squad.Cards, debrief.Cards })
            {
                cards.PadAt = (i, n) => PadAt?.Invoke(i, n);
                cards.PortraitOf = id => PortraitOf?.Invoke(id);
            }
            squad.Show(false);
            debrief.Show(false);
            map = new MapScreen(root.Q("view"), tips, Icon, relay);
            map.Show(false);
            settlement = new SettlementScreen(root.Q("view"), tips, Icon, relay) { ArtOf = id => PieceArtOf?.Invoke(id) };
            settlement.Show(false);
            skillBoard = new SkillBoardModal(root.Q("board"), relay, s => Sound?.Invoke(s), tips) { PortraitOf = id => PortraitOf?.Invoke(id) };
            toast = root.Q<Label>("toast");
            Ui.Show(toast, false);
            Ui.Show(root.Q("proposal"), false);
            Ui.Show(root.Q("modal"), false);
            tips.Front();
        }

        void OnDisable()
        {
            root = null;
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
            Dress();
        }

        public void SetSoldiers(SoldiersView v)
        {
            soldiers.Set(v);
            Dress();
        }

        public void SetSkillBoard(SkillBoardView v)
        {
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
            squad.Set(v);
            Dress();
        }

        public void SetDebrief(DebriefView v)
        {
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
        void Dress()
        {
            if (root == null) return;
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
            foreach (var r in settlement.Plates()) out_.Add(new Rect(r.x / size.x, r.y / size.y, r.width / size.x, r.height / size.y));
            return out_;
        }

        /// <summary>The Mapa: the valley is made the first time (its painting, the Marea's field, its camera).</summary>
        public void SetMap(MapView v)
        {
            if (valley == null && v != null)
            {
                valley = new GameObject("Valle").AddComponent<ValleyMap>();
                valley.transform.SetParent(transform, false);
                valley.Build(ValleyCamera);
                map.Bind(valley);
            }
            map.Set(v);
            Dress();
        }
        public void ToggleMenu(GameMenuView v) => menu.Toggle(v);
        public void CloseMenu() => menu.Close();
        public bool MenuBack() => menu != null && menu.Back();
        public void MenuPage(string page) => menu.Go(page);

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
