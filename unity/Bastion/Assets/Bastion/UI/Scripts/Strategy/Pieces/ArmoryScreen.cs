using System;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The Armería (screen.ts renderEngineering, arms.ts): on the left the two tabs and their lists (belt items with their
    /// price and stock; the good weapons with where they go and who has them, the loose mods, the armour tiers, what the
    /// Taller makes); on the right what is picked, with its silhouette in the corner: a weapon's figures and reach, its
    /// condition, its mods, who carries it and who could. The 3D bench behind it is Arte's clearing.
    /// </summary>
    public sealed class ArmoryScreen
    {
        readonly VisualElement left;
        readonly VisualElement right;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        string key;
        bool shown;
        bool hasDetail;

        public ArmoryScreen(VisualElement view, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            left = Ui.El("strat-side strat-side--left panel armory-left", view);
            right = Ui.El("strat-side strat-side--right strat-side--fit panel", view);
            left.Add(new ScrollView(ScrollViewMode.Vertical) { name = "left-scroll", verticalScrollerVisibility = ScrollerVisibility.Hidden, horizontalScrollerVisibility = ScrollerVisibility.Hidden });
            right.Add(new ScrollView(ScrollViewMode.Vertical) { name = "right-scroll", verticalScrollerVisibility = ScrollerVisibility.Hidden, horizontalScrollerVisibility = ScrollerVisibility.Hidden });
        }

        public void Show(bool on)
        {
            shown = on;
            Ui.Show(left, on);
            Ui.Show(right, on && hasDetail);
        }

        public void Set(ArmoryView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            var leftBody = left.Q<ScrollView>("left-scroll").contentContainer;
            var rightBody = right.Q<ScrollView>("right-scroll").contentContainer;
            leftBody.Clear();
            rightBody.Clear();
            hasDetail = v?.Detail != null;
            Ui.Show(right, shown && hasDetail);
            if (v == null) return;
            var head = Ui.El("strat-head", leftBody);
            Ui.Text(v.Head.ToUpperInvariant(), "strat-head__text font-title", head);
            var tabs = Ui.El("armory-tabs well", leftBody);
            Tab(tabs, "items", "Accesorios", v.Tab == "items", v.ItemsNew);
            Tab(tabs, "gear", "Armamento", v.Tab == "gear", v.GearNew);
            foreach (var s in v.Sections)
            {
                if (!string.IsNullOrEmpty(s.Title)) Ui.Text(s.Title.ToUpperInvariant(), "armory-section font-title", leftBody);
                foreach (var r in s.Rows) Row(r, leftBody);
            }
            if (!string.IsNullOrEmpty(v.Note)) Ui.Text(v.Note, "armory-note muted", leftBody);
            if (v.Detail != null) Detail(v.Detail, rightBody);
        }

        void Tab(VisualElement parent, string id, string label, bool on, bool fresh)
        {
            var b = new Button(() => handlers.OnArmoryTab(id));
            Ui.AddClasses(b, "armory-tab font-title");
            b.EnableInClassList("armory-tab--on", on);
            Ui.Text(label.ToUpperInvariant(), "armory-tab__label", b);
            if (fresh) Ui.Text(StrategyText.Tabs.NewTag, "strat-tab__new font-title", b);
            parent.Add(b);
        }

        void Row(ArmoryRowView r, VisualElement parent)
        {
            var id = r.Id;
            var row = new Button(() => handlers.OnArmoryPick(id));
            row.AddToClassList("armory-row");
            row.EnableInClassList("armory-row--on", r.Selected);
            row.EnableInClassList("armory-row--dim", r.Dim);
            if (r.Tier > 0)
            {
                var marks = Ui.El("tier-marks armory-row__tier", row);
                for (var i = 0; i < r.Tier; i++) Ui.Text("◆", "tier-marks__mark armory-row__tier-mark--" + r.Tier, marks);
            }
            else if (!string.IsNullOrEmpty(r.Icon)) Ui.Icon(icon, r.Icon, "armory-row__icon", row);
            var words = Ui.El("armory-row__words", row);
            var top = Ui.El("armory-row__top", words);
            Ui.Text(r.Name, "armory-row__name font-title", top);
            foreach (var t in r.Tags)
            {
                var tag = Ui.Text(t.Text, "armory-row__tag font-title", top);
                if (!string.IsNullOrEmpty(t.Tone)) tag.AddToClassList("armory-row__tag--" + t.Tone);
                if (!string.IsNullOrEmpty(t.Tip)) tips.Attach(tag, t.Tip);
            }
            if (r.New) Ui.Text(StrategyText.Tabs.NewTag, "strat-tab__new font-title", top);
            if (!string.IsNullOrEmpty(r.Price)) Ui.Text(r.Price, "strat-chip armory-row__price", words);
            var end = Ui.El("armory-row__end", row);
            if (!string.IsNullOrEmpty(r.Place)) Ui.Text(r.Place.ToUpperInvariant(), "armory-row__place font-title", end);
            if (!string.IsNullOrEmpty(r.Holder)) Ui.Text(r.Holder, "armory-row__holder", end);
            if (!string.IsNullOrEmpty(r.Right)) Ui.Text(r.Right, "armory-row__right", end);
            if (r.Chip != null)
            {
                var chip = Ui.Text(r.Chip.Text, "strat-chip armory-row__chip", end);
                if (!string.IsNullOrEmpty(r.Chip.Tone)) chip.AddToClassList("strat-chip--" + r.Chip.Tone);
                if (!string.IsNullOrEmpty(r.Chip.Tip)) tips.Attach(chip, r.Chip.Tip);
            }
            if (!string.IsNullOrEmpty(r.Tip)) tips.Attach(row, r.Tip);
            parent.Add(row);
        }

        void Detail(ArmoryDetailView d, VisualElement parent)
        {
            if (!string.IsNullOrEmpty(d.Art)) Ui.Icon(icon, d.Art, "armory-art", parent);
            Ui.Text(d.Kicker.ToUpperInvariant(), "sheet__kicker font-title", parent);
            Ui.Text(d.Name.ToUpperInvariant(), "armory-detail__name font-title", parent);
            foreach (var t in d.Text) Ui.Text(StrategyText.Marked(t), "armory-detail__text", parent);
            if (d.Chips.Count > 0)
            {
                var row = Ui.El("strat-chips", parent);
                foreach (var c in d.Chips)
                {
                    var chip = Ui.Text(c.Text, "strat-chip", row);
                    if (!string.IsNullOrEmpty(c.Tone)) chip.AddToClassList("strat-chip--" + c.Tone);
                    if (!string.IsNullOrEmpty(c.Tip)) tips.Attach(chip, c.Tip);
                }
            }
            if (!string.IsNullOrEmpty(d.Button) && d.Mods == null && d.Holder == null) MainButton(d, parent);
            if (d.Figures.Count > 0 || d.Range != null)
            {
                var box = Ui.El("armory-figures well", parent);
                foreach (var f in d.Figures)
                {
                    var row = Ui.El("armory-figure", box);
                    Ui.Text(f.Key, "armory-figure__key", row);
                    Ui.Text(f.Value, "armory-figure__value font-title", row);
                }
                if (d.Range != null) Range(d.Range, box);
            }
            if (d.Condition != null)
            {
                SubHead("Estado", parent);
                var row = Ui.El("armory-condition", parent);
                var gauge = Ui.El("armory-gauge well", row);
                var fill = Ui.El("armory-gauge__fill", gauge);
                fill.style.width = Length.Percent(Mathf.Clamp(d.Condition.Value, 0, 100));
                fill.EnableInClassList("armory-gauge__fill--worn", d.Condition.Value < 30);
                Ui.Text(d.Condition.Text, "armory-condition__text font-title", row);
                if (!string.IsNullOrEmpty(d.Condition.Line)) Ui.Text(d.Condition.Line, "armory-detail__text", parent);
                if (!string.IsNullOrEmpty(d.Condition.Button)) Small(d.Condition.Button, "repair", null, d.Condition.Enabled, parent);
                if (!string.IsNullOrEmpty(d.Condition.Why)) Ui.Text(d.Condition.Why, "strat-why strat-why--small", parent);
            }
            if (d.Mods != null)
            {
                SubHead(d.Mods.Head, parent);
                foreach (var m in d.Mods.Mounted)
                {
                    var row = Ui.El("armory-mod", parent);
                    Ui.Text(m.Name, "armory-mod__name font-title", row);
                    if (!string.IsNullOrEmpty(m.Line)) Ui.Text("· " + m.Line, "armory-mod__line", row);
                    if (!string.IsNullOrEmpty(m.Button)) Small(m.Button, "unmod", m.Id, m.Enabled, row);
                }
                if (!string.IsNullOrEmpty(d.Mods.FreeHead))
                {
                    var slot = Ui.El("armory-slot well", parent);
                    Ui.Text(d.Mods.FreeHead.ToUpperInvariant(), "armory-slot__head font-title", slot);
                    foreach (var o in d.Mods.Options) Small(o.Button, "mod", o.Id, o.Enabled, slot);
                }
            }
            if (d.Holder != null)
            {
                SubHead(d.Holder.Head, parent);
                var row = Ui.El("armory-mod", parent);
                Ui.Text(d.Holder.Name, "armory-mod__name", row);
                if (!string.IsNullOrEmpty(d.Holder.Button)) Small(d.Holder.Button, "take", null, true, row);
            }
            if (!string.IsNullOrEmpty(d.SoldiersHead))
            {
                SubHead(d.SoldiersHead, parent);
                if (d.Soldiers.Count == 0 && !string.IsNullOrEmpty(d.SoldiersEmpty)) Ui.Text(d.SoldiersEmpty, "armory-detail__text muted", parent);
                foreach (var c in d.Soldiers)
                {
                    var row = Ui.El("armory-carrier", parent);
                    var words = Ui.El("armory-carrier__words", row);
                    var top = Ui.El("armory-carrier__top", words);
                    Ui.Text(c.Name, "armory-carrier__name font-title", top);
                    if (!string.IsNullOrEmpty(c.Note)) Ui.Text(c.Note, "armory-carrier__note", top);
                    if (c.Diff.Count > 0)
                    {
                        var diff = Ui.El("armory-carrier__diff", words);
                        foreach (var x in c.Diff)
                        {
                            var chip = Ui.Text(x.Text, "armory-diff", diff);
                            if (!string.IsNullOrEmpty(x.Tone)) chip.AddToClassList("armory-diff--" + x.Tone);
                        }
                    }
                    if (!string.IsNullOrEmpty(c.Button)) Small(c.Button, "give", c.Id, c.Enabled, row);
                    else if (!string.IsNullOrEmpty(c.Chip)) Ui.Text(c.Chip, "strat-chip strat-chip--good", row);
                }
            }
            if (!string.IsNullOrEmpty(d.Button) && (d.Mods != null || d.Holder != null)) MainButton(d, parent);
        }

        void MainButton(ArmoryDetailView d, VisualElement parent)
        {
            var action = d.Action;
            var b = new Button(() => handlers.OnArmoryAction(action, null)) { text = d.Button.ToUpperInvariant() };
            Ui.AddClasses(b, "armory-main btn-primary font-title");
            b.SetEnabled(d.Enabled);
            parent.Add(b);
            if (!string.IsNullOrEmpty(d.Why)) Ui.Text(d.Why, "strat-why strat-why--small", parent);
        }

        void Small(string text, string action, string arg, bool enabled, VisualElement parent)
        {
            var b = new Button(() => handlers.OnArmoryAction(action, arg)) { text = text.ToUpperInvariant() };
            Ui.AddClasses(b, "armory-small font-title");
            b.SetEnabled(enabled);
            parent.Add(b);
        }

        static void SubHead(string text, VisualElement parent) => Ui.Text(text.ToUpperInvariant(), "sheet__head font-title", parent);

        /// <summary>The reach (arms.ts rangeChart): its name, a bar above or below the line at each distance, «casillas», its line.</summary>
        static void Range(RangeView r, VisualElement parent)
        {
            var head = Ui.El("armory-range__head", parent);
            Ui.Text("ALCANCE", "armory-range__label font-title", head);
            Ui.Text(r.Name, "armory-range__name font-title", head);
            var chart = Ui.El("armory-range", parent);
            foreach (var s in r.Steps)
            {
                var col = Ui.El("armory-range__col", chart);
                Ui.Text(s.Value, "armory-range__value font-title", col);
                var up = Ui.El("armory-range__up", col);
                var bar = Ui.El("armory-range__bar", up);
                bar.style.height = Length.Percent(Mathf.Clamp01(Mathf.Abs(s.Height)) * 100);
                bar.EnableInClassList("armory-range__bar--bad", s.Height < 0);
                Ui.El("armory-range__base", col);
                var down = Ui.El("armory-range__down", col);
                if (s.Height < 0)
                {
                    bar.RemoveFromHierarchy();
                    down.Add(bar);
                }
                Ui.Text(s.Distance, "armory-range__distance", col);
            }
            Ui.Text("casillas", "armory-range__unit", parent);
            if (!string.IsNullOrEmpty(r.Line)) Ui.Text(r.Line, "armory-range__line", parent);
        }
    }
}
