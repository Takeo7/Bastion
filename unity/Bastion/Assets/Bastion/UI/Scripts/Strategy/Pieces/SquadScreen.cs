using System;
using System.Collections.Generic;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Escuadra (screen.ts renderSquad): one's own soldiers to pick on the left (the partner's on the right, to look at),
    /// who is ready above the mission, the mission's plate at the bottom with LANZAR and «Volver al Bastión», and a card
    /// under each figure of the line-up (LineupCards). With no mission chosen, only that and «Ir al mapa».
    /// </summary>
    public sealed class SquadScreen
    {
        readonly VisualElement left;
        readonly VisualElement right;
        readonly VisualElement bottom;
        readonly VisualElement empty;
        readonly TooltipLayer tips;
        readonly IStrategyHandlers handlers;
        readonly LineupCards cards;
        bool shown;
        SquadView last;
        string key;

        public SquadScreen(VisualElement view, TooltipLayer tips, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.handlers = handlers;
            cards = new LineupCards(view, tips, handlers) { Floor = () => bottom.worldBound.yMin };
            left = Ui.El("strat-side strat-side--left strat-side--fit squad-list panel", view);
            right = Ui.El("strat-side strat-side--right strat-side--fit squad-list panel", view);
            bottom = Ui.El("squad-bottom", view);
            empty = Ui.El("squad-empty panel", view);
        }

        public LineupCards Cards => cards;

        public void Show(bool on)
        {
            shown = on;
            Draw();
        }

        public void Set(SquadView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            last = v;
            left.Clear();
            right.Clear();
            bottom.Clear();
            empty.Clear();
            cards.Set(v?.Cards);
            if (v == null) return;
            if (!string.IsNullOrEmpty(v.Empty))
            {
                Ui.Text(v.Empty, "squad-empty__text", empty);
                var go = new Button(handlers.OnGoMap) { text = "IR AL MAPA" };
                Ui.AddClasses(go, "btn-primary font-title");
                empty.Add(go);
                Draw();
                return;
            }
            List(v.MineHead, v.MineTip, v.Mine, left);
            if (v.Partner.Count > 0) List(v.PartnerHead, null, v.Partner, right);
            if (v.Ready.Count > 0)
            {
                var board = Ui.El("ready-board", bottom);
                foreach (var r in v.Ready)
                {
                    var mark = Ui.El("ready-mark", board);
                    mark.EnableInClassList("ready-mark--on", r.Ready);
                    Ui.El("ready-mark__lamp", mark);
                    var name = Ui.Text(r.Name, "ready-mark__name font-title", mark);
                    name.style.color = Ui.Hex(r.Color, Color.white);
                    Ui.Text(r.Ready ? "A PUNTO" : "ELIGIENDO", "ready-mark__state font-title", mark);
                    if (!string.IsNullOrEmpty(r.Tip)) tips.Attach(mark, r.Tip);
                }
            }
            var plate = Ui.El("squad-mission panel", bottom);
            var words = Ui.El("squad-mission__words", plate);
            var title = Ui.El("squad-mission__title-row", words);
            Ui.Text(v.Title.ToUpperInvariant(), "squad-mission__title font-title", title);
            if (!string.IsNullOrEmpty(v.Night)) Ui.Text(v.Night.ToUpperInvariant(), "squad-mission__night font-title", title);
            Ui.Text(StrategyText.Marked(v.Briefing), "squad-mission__briefing", words);
            if (!string.IsNullOrEmpty(v.Warning)) Ui.Text(v.Warning, "squad-mission__warning", words);
            var actions = Ui.El("squad-mission__actions", plate);
            var launch = new Button(handlers.OnLaunch) { text = v.Launch.ToUpperInvariant() };
            Ui.AddClasses(launch, "squad-launch btn-primary font-title");
            launch.EnableInClassList("squad-launch--ready", v.LaunchReady);
            launch.EnableInClassList("squad-launch--waiting", v.LaunchWaiting);
            launch.SetEnabled(v.LaunchEnabled);
            if (!string.IsNullOrEmpty(v.LaunchWhy)) tips.Attach(launch, v.LaunchWhy);
            actions.Add(launch);
            if (!string.IsNullOrEmpty(v.Back))
            {
                var back = new Button(handlers.OnSquadBack) { text = v.Back.ToUpperInvariant() };
                Ui.AddClasses(back, "squad-back font-title");
                actions.Add(back);
            }
            Draw();
        }

        /// <summary>A squad list: its head and its rows (hexagon, name, rank, a chip).</summary>
        void List(string head, string tip, List<PickRowView> rows, VisualElement parent)
        {
            var h = Ui.El("strat-head", parent);
            Ui.Text(head.ToUpperInvariant(), "strat-head__text font-title", h);
            if (!string.IsNullOrEmpty(tip)) tips.Attach(h, tip);
            foreach (var r in rows)
            {
                var id = r.Id;
                var row = new Button(() => handlers.OnSquadToggle(id, !r.In));
                row.AddToClassList("squad-row");
                row.EnableInClassList("squad-row--in", r.In);
                row.SetEnabled(r.Enabled);
                row.style.borderLeftColor = Ui.Hex(r.Accent, new Color(0.95f, 0.65f, 0.2f));
                var hex = new SkillHex();
                hex.Set(r.Skills, tips);
                row.Add(hex);
                Ui.Text(r.Name, "squad-row__name font-title", row);
                if (r.Rank > 0)
                {
                    var rank = Ui.El("squad-row__rank", row);
                    for (var i = 0; i < r.Rank; i++) Ui.El("rank__chevron", rank);
                }
                if (!string.IsNullOrEmpty(r.Chip))
                {
                    var chip = Ui.Text(r.Chip, "squad-row__chip font-title", row);
                    if (!string.IsNullOrEmpty(r.ChipTone)) chip.AddToClassList("squad-row__chip--" + r.ChipTone);
                    if (!string.IsNullOrEmpty(r.ChipTip)) tips.Attach(chip, r.ChipTip);
                }
                if (!string.IsNullOrEmpty(r.Tip)) tips.Attach(row, r.Tip);
                parent.Add(row);
            }
        }

        void Draw()
        {
            var isEmpty = last != null && !string.IsNullOrEmpty(last.Empty);
            Ui.Show(empty, shown && isEmpty);
            Ui.Show(left, shown && !isEmpty && last != null);
            Ui.Show(right, shown && !isEmpty && last != null && last.Partner.Count > 0);
            Ui.Show(bottom, shown && !isEmpty && last != null);
            cards.Show(shown && !isEmpty);
        }
    }
}
