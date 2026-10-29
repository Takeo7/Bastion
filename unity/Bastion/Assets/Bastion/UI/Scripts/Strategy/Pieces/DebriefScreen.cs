using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The debrief (screen.ts renderDebrief): over the same line-up, a card under each figure (how they came out, their
    /// kills, what they learnt) and at the bottom the result («MISIÓN CUMPLIDA» on green, «FRACASADA» on red), the
    /// mission, the haul in equal tiles (the story's goal first and larger), what changed, the lines (the fallen, the
    /// base's damage with «Construir», the night), the story's page and CONTINUAR. The page shows open (Ana, Arturo
    /// 29/10: the 0.10.1 folded it under «Leer el parte»).
    /// </summary>
    public sealed class DebriefScreen
    {
        readonly VisualElement plate;
        readonly TooltipLayer tips;
        readonly System.Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        readonly LineupCards cards;
        bool shown;
        DebriefView last;
        string key;

        public DebriefScreen(VisualElement view, TooltipLayer tips, System.Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            cards = new LineupCards(view, tips, handlers);
            plate = Ui.El("debrief-card panel", view);
            cards.Floor = () => plate.worldBound.yMin;
        }

        public LineupCards Cards => cards;

        public void Show(bool on)
        {
            shown = on;
            Ui.Show(plate, on && last != null);
            cards.Show(on);
        }

        public void Set(DebriefView v)
        {
            var k = Ui.Key(v) + "|" + StrategyRoot.PictureStamp;
            if (k == key) return;
            key = k;
            last = v;
            plate.Clear();
            cards.Set(v?.Cards);
            Ui.Show(plate, shown && v != null);
            if (v == null) return;
            plate.EnableInClassList("debrief-card--defeat", !v.Victory);
            Ui.Text(v.Result.ToUpperInvariant(), "debrief-result font-title", plate);
            Ui.Text(v.Mission.ToUpperInvariant(), "debrief-mission font-title", plate);
            Ui.El("debrief-rule", plate);
            var haul = Ui.El("debrief-haul", plate);
            foreach (var h in v.Haul)
            {
                var tile = Ui.El("haul well", haul);
                tile.EnableInClassList("haul--big", h.Big);
                tile.EnableInClassList("haul--better", h.Better);
                Ui.Icon(icon, h.Icon, "haul__icon", tile);
                Ui.Text(h.Figure, "haul__figure font-title", tile);
                if (!string.IsNullOrEmpty(h.Tip)) tips.Attach(tile, h.Tip);
            }
            if (v.Events.Count > 0)
            {
                var events = Ui.El("strat-chips debrief-events", plate);
                foreach (var e in v.Events)
                {
                    var chip = Ui.Text(e.Text, "strat-chip", events);
                    if (!string.IsNullOrEmpty(e.Tone)) chip.AddToClassList("strat-chip--" + e.Tone);
                    if (!string.IsNullOrEmpty(e.Tip)) tips.Attach(chip, e.Tip);
                }
            }
            for (var i = 0; i < v.Lines.Count; i++)
            {
                var l = v.Lines[i];
                var row = Ui.El("debrief-line", plate);
                Ui.Text(StrategyText.Marked(l.Text), l.Bad ? "debrief-line__text debrief-line__text--bad" : "debrief-line__text", row);
                if (string.IsNullOrEmpty(l.Button)) continue;
                var index = i;
                var b = new Button(() => handlers.OnDebriefLine(index)) { text = l.Button.ToUpperInvariant() };
                Ui.AddClasses(b, "armory-small font-title");
                row.Add(b);
            }
            var foot = Ui.El("debrief-foot", plate);
            if (!string.IsNullOrEmpty(v.Story)) Ui.Text(StrategyText.Marked(v.Story), "debrief-story", foot);
            else Ui.El("debrief-foot__space", foot);
            var go = new Button(handlers.OnDebriefContinue) { text = v.Continue.ToUpperInvariant() };
            Ui.AddClasses(go, "debrief-continue btn-primary font-title");
            foot.Add(go);
        }
    }
}
