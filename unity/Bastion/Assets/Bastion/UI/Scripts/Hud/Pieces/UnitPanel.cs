using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The picked soldier's panel, bottom left (hud.ts setSelected; docs/UNITY-UI.md §2.2): veterancy, its best line's
    /// hexagon, name, whose; its figures as icons with pips or numbers (words in the tooltips); its weapon. A soldier's
    /// head opens its sheet (H). Null hides it.
    /// </summary>
    public sealed class UnitPanel
    {
        readonly VisualElement host;
        readonly IHudHandlers handlers;
        readonly TooltipLayer tips;
        readonly System.Func<string, Sprite> icon;
        string key;
        string shownId;

        public UnitPanel(VisualElement host, IHudHandlers handlers, TooltipLayer tips, System.Func<string, Sprite> icon = null)
        {
            this.host = host;
            this.handlers = handlers;
            this.tips = tips;
            this.icon = icon;
        }

        public void Set(UnitPanelView view)
        {
            var k = Ui.Key(view);
            if (k == key) return;
            key = k;
            host.Clear();
            Ui.Show(host, view != null);
            if (view == null) return;
            shownId = view.Id;
            Ui.CloneInto("UnitPanel", host);
            var tree = host;

            var head = tree.Q("head");
            head.EnableInClassList("unit-head--opens", view.Soldier);
            if (view.Soldier)
            {
                tips.Attach(head, view.RankName + HudText.Squad.SheetTip);
                head.RegisterCallback<ClickEvent>(_ => handlers.OnSheet(shownId));
            }
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
