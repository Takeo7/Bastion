using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The picked soldier's panel, bottom left (hud.ts setSelected; docs/UNITY-UI.md §2.2): veterancy, its best line's
    /// hexagon, name, whose; its figures as icons with pips or numbers (words in the tooltips); its weapon with its wear
    /// and improvements; its backpack (Backpack). A soldier's head opens its sheet (H). Null hides it.
    /// </summary>
    public sealed class UnitPanel
    {
        /// <summary>Whether the soldier's sheet is in Unity yet (Lore, 24/10: the panel mustn't promise a key that does nothing).</summary>
        const bool SheetExists = false;

        readonly VisualElement host;
        readonly IHudHandlers handlers;
        readonly TooltipLayer tips;
        readonly System.Func<string, Sprite> icon;
        readonly Backpack backpack;
        string key;
        string shownId;

        public UnitPanel(VisualElement host, IHudHandlers handlers, TooltipLayer tips, System.Func<string, Sprite> icon = null)
        {
            this.host = host;
            this.handlers = handlers;
            this.tips = tips;
            this.icon = icon;
            backpack = new Backpack(handlers, tips, icon);
        }

        /// <summary>The backpack's menu: open, and the swap opened for a thing on the floor (the notice's «Cambiar»).</summary>
        public bool PackMenuOpen => backpack.MenuOpen;
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
                backpack.Set(null, null, null);
                return;
            }
            shownId = view.Id;
            Ui.CloneInto("UnitPanel", host);
            // mixta.css: one of the big plates, in the steel frame with rivets (Arturo, 24/10: as the 0.10.1).
            PlateFrame.Dress(host);
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
            var bite = tree.Q<Label>("bite");
            Ui.Show(bite, !string.IsNullOrEmpty(view.Bite));
            bite.text = view.Bite ?? "";
            tips.Attach(bite, view.BiteTip);

            var stats = tree.Q("stats");
            foreach (var s in view.Stats) stats.Add(Stat(s));
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
            backpack.Set(tree.Q("pack"), view.Id, view.Pack);
        }

        VisualElement Stat(StatView s)
        {
            var e = Ui.El("stat stat--" + s.Kind);
            Ui.Icon(icon, "stat:" + s.Kind, "stat-icon stat-icon--" + s.Kind, e);
            if (s.Total > 0)
            {
                var pips = Ui.El("pips pips--" + (s.Kind == "ap" ? "ap" : s.Kind == "ammo" || s.Kind == "sidearm" ? "ammo" : "hp"), e);
                Ui.Pips(pips, s.Filled, s.Total);
            }
            if (!string.IsNullOrEmpty(s.Value)) Ui.Text(s.Value, s.ValueBad ? "stat__value stat__value--bad" : "stat__value", e);
            tips.Attach(e, s.Tip);
            return e;
        }
    }
}
