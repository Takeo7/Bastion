using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The picked soldier's sheet, one row over the squad's cards (UI-COMBATE UI-02; hud.ts setSelected; docs/UNITY-UI.md
    /// §2.2; Ana, 7/11 #13: one row, a thin line between its groups): veterancy, its best line's hexagon, name, rank or
    /// whose; its weapon with its wear and improvements; its rounds (the spare magazines red at none); its figures as
    /// icons with numbers (words in the tooltips), health and actions only when it has no card (its card shows them); at
    /// its end the backpack's mark, what the squad carries in its help, which opens the pockets over it (Backpack; #13:
    /// only then). A soldier's head opens its sheet (H). Null hides it.
    /// </summary>
    public sealed class UnitPanel
    {
        /// <summary>Whether the soldier's sheet is in Unity yet (Lore, 24/10: the panel mustn't promise a key that does nothing).</summary>
        const bool SheetExists = true;

        readonly VisualElement host;
        readonly VisualElement packHost;
        readonly IHudHandlers handlers;
        readonly TooltipLayer tips;
        readonly System.Func<string, Sprite> icon;
        readonly Backpack backpack;
        string key;
        string shownId;

        /// <param name="packHost">Where the backpack's slots go: over the sheet's end.</param>
        public UnitPanel(VisualElement host, VisualElement packHost, IHudHandlers handlers, TooltipLayer tips, System.Func<string, Sprite> icon = null)
        {
            this.host = host;
            this.packHost = packHost;
            this.handlers = handlers;
            this.tips = tips;
            this.icon = icon;
            backpack = new Backpack(handlers, tips, icon);
        }

        /// <summary>The backpack's menu: open, and the swap opened for a thing on the floor (the notice's «Cambiar»).</summary>
        public bool PackMenuOpen => backpack.MenuOpen;
        /// <summary>The pockets shown or hidden, as the backpack's mark's click does (the labs: PackSteps «pack»).</summary>
        public void TogglePockets() => backpack.Pockets = !backpack.Pockets;
        public void OpenSwap(string unit, string id) => backpack.OpenSwap(unit, id);
        public bool ClosePackMenu() => backpack.Close();
        public void PressedAt(VisualElement target) => backpack.PressedAt(target);

        public void Set(UnitPanelView view)
        {
            var k = Ui.Key(view);
            if (k == key) return;
            key = k;
            host.Clear();
            Ui.Show(host, view != null);
            if (view == null)
            {
                backpack.Set(packHost, null, null);
                return;
            }
            shownId = view.Id;
            Ui.CloneInto("UnitPanel", host);
            var tree = host;

            var head = tree.Q("head");
            // The soldier's sheet (H, a click on the name) only once it exists in Unity: till then nothing says it opens.
            var opens = view.Soldier && SheetExists;
            head.EnableInClassList("unit-head--opens", opens);
            tips.Attach(head, opens ? view.RankName + HudText.Squad.SheetTip : view.RankName);
            if (opens) head.RegisterCallback<ClickEvent>(_ => handlers.OnSheet(shownId));
            var rank = tree.Q("rank");
            for (var i = 0; i < view.Rank; i++) Ui.El("rank__chevron", rank);
            Ui.Show(rank, view.Rank > 0);
            if (view.Skills.Length > 0)
            {
                var hex = new SkillHex();
                hex.Set(view.Skills, tips);
                tree.Q("name-row").Insert(0, hex);
            }
            tree.Q<Label>("name").text = view.Name;

            // The partner's soldier says whose, in the partner's colour, where the veterancy goes.
            var whose = tree.Q<Label>("whose");
            var cls = tree.Q<Label>("class");
            var theirs = !string.IsNullOrEmpty(view.Whose);
            Ui.Show(whose, theirs);
            Ui.Show(cls, !theirs);
            if (theirs)
            {
                whose.text = HudText.Squad.Whose(view.Whose);
                whose.style.color = Ui.Hex(view.WhoseColor, Color.white);
            }
            else cls.text = view.RankName;
            // Its cover's shield in its colour, after its name (Arturo, 7/11: here, not by its health).
            if (view.Cover >= 0)
            {
                var shield = new CoverShield(icon?.Invoke("stat:armor")) { Level = view.Cover };
                head.Add(shield);
                var title = view.CoverTitle;
                var words = view.CoverTip;
                tips.Attach(shield, () =>
                {
                    var tip = new VisualElement();
                    var name = Ui.Text(title, "font-title", tip);
                    name.style.color = Color.white;
                    name.style.fontSize = 12;
                    name.style.letterSpacing = 1;
                    var line = Ui.Text(words, "", tip);
                    line.style.whiteSpace = WhiteSpace.Normal;
                    line.style.maxWidth = 300;
                    return tip;
                });
            }
            var bite = tree.Q<Label>("bite");
            Ui.Show(bite, !string.IsNullOrEmpty(view.Bite));
            bite.text = view.Bite ?? "";
            tips.Attach(bite, view.BiteTip);

            var stats = tree.Q("stats");
            // A thin line before the figures that aren't rounds (Ana, 7/11 #13: rounds, then the rest).
            var rounds = true;
            foreach (var s in view.Stats)
            {
                var e = Stat(s);
                var round = s.Kind == "ammo" || s.Kind == "sidearm" || s.Kind == "hp" || s.Kind == "ap" || s.Kind == "moves";
                if (rounds && !round && stats.childCount > 0) e.AddToClassList("stat--group");
                rounds &= round;
                stats.Add(e);
            }
            var weaponIcon = tree.Q(className: "stat-icon--weapon");
            var weaponSprite = icon?.Invoke("stat:weapon");
            if (weaponSprite != null) weaponIcon.style.backgroundImage = new StyleBackground(weaponSprite);
            tree.Q<Label>("weapon-name").text = view.Weapon;
            tips.Attach(tree.Q("weapon"), view.WeaponTip);
            // Its state and improvements (docs/FASE9.md §5): «GASTADA», «ROTA», «+2», each with its line.
            var marks = tree.Q("weapon-marks");
            foreach (var m in view.WeaponMarks)
                tips.Attach(Ui.Text(m.Text, "weapon-mark weapon-mark--" + m.Kind, marks), m.Tip);
            Ui.Show(marks, view.WeaponMarks.Count > 0);
            backpack.Set(packHost, view.Id, view.Pack);
            // The backpack's mark at the line's end, its slots over it: in its help, what it carries and what the squad does.
            var packMark = tree.Q("pack-mark");
            Ui.Show(packMark, view.Pack != null);
            if (view.Pack != null)
            {
                // Synty's backpack, as on the bar's key to search; a click opens the pockets over it, again shuts them (Ana,
                // 7/11 #13).
                Ui.Icon(icon, "search", "unit-pack-mark__icon", packMark);
                tips.Attach(packMark, backpack.Haul);
                packMark.EnableInClassList("unit-pack-mark--open", backpack.Pockets);
                packMark.RegisterCallback<ClickEvent>(_ =>
                {
                    backpack.Pockets = !backpack.Pockets;
                    packMark.EnableInClassList("unit-pack-mark--open", backpack.Pockets);
                });
            }
        }

        VisualElement Stat(StatView s)
        {
            var e = Ui.El("stat stat--" + s.Kind);
            var pic = Ui.Icon(icon, "stat:" + s.Kind, "stat-icon stat-icon--" + s.Kind, e);
            // Our own drawing where Synty has no icon (the molotovs: Arturo, 01/11 #10).
            if (icon?.Invoke("stat:" + s.Kind) == null && LineIcon.Has(s.Kind)) pic.Add(new LineIcon(s.Kind));
            if (s.Total > 0)
            {
                var pips = Ui.El("pips pips--" + (s.Kind == "ap" || s.Kind == "moves" ? s.Kind : s.Kind == "ammo" || s.Kind == "sidearm" ? "ammo" : "hp"), e);
                Ui.Pips(pips, s.Filled, s.Total);
            }
            if (!string.IsNullOrEmpty(s.Value)) Ui.Text(s.Value, s.ValueBad ? "stat__value stat__value--bad" : "stat__value", e);
            tips.Attach(e, s.Tip);
            return e;
        }
    }
}
