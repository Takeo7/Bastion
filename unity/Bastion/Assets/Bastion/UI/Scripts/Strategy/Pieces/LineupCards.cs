using System;
using System.Collections.Generic;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The cards under the figures of the line-up (screen.ts anchorCards: Escuadra and the debrief): each pinned under
    /// its pad every frame, as wide as the pads' spacing allows, kept above the panels at the bottom. Where a pad is
    /// on the panel comes from `PadAt` (Arte's Settlement.PadWorld projected; the lab gives fixed places).
    /// </summary>
    public sealed class LineupCards
    {
        /// <summary>A card's width at most and at least, and the gap between two (strategy.css --card-w).</summary>
        const float MaxWidth = 150;
        const float MinWidth = 104;
        const float Gap = 6;

        readonly VisualElement layer;
        readonly TooltipLayer tips;
        readonly IStrategyHandlers handlers;
        readonly List<VisualElement> cards = new List<VisualElement>();
        string key;

        /// <summary>Where the card of pad `i` of `n` goes on the panel (its top centre, in panel pixels); null, not on screen.</summary>
        public Func<int, int, Vector2?> PadAt;
        /// <summary>A soldier's bust by id; null, the silhouette.</summary>
        public Func<string, Texture> PortraitOf;
        /// <summary>The lowest a card's bottom may go (the top of the panels at the bottom), in panel pixels.</summary>
        public Func<float> Floor;

        public LineupCards(VisualElement parent, TooltipLayer tips, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.handlers = handlers;
            layer = Ui.El("lineup-cards", parent);
            layer.pickingMode = PickingMode.Ignore;
            layer.schedule.Execute(Place).Every(16);
        }

        public void Show(bool on) => Ui.Show(layer, on);

        public void Set(List<LineupCardView> views)
        {
            var k = Ui.Key(views);
            if (k == key) return;
            key = k;
            layer.Clear();
            cards.Clear();
            if (views == null) return;
            foreach (var v in views) cards.Add(Card(v));
            Place();
        }

        VisualElement Card(LineupCardView v)
        {
            var card = Ui.El("lineup-card", layer);
            if (v.Id == null)
            {
                card.AddToClassList("lineup-card--empty");
                Ui.Text((v.EmptyLabel ?? "Libre").ToUpperInvariant(), "lineup-card__free font-title", card);
                if (!string.IsNullOrEmpty(v.EmptyHint)) Ui.Text(v.EmptyHint, "lineup-card__hint", card);
                if (!string.IsNullOrEmpty(v.Recruit))
                {
                    var b = new Button(handlers.OnSquadRecruit) { text = v.Recruit.ToUpperInvariant() };
                    // A plain steel key, as the 0.10.1's (recruitCard: .x-btn, not .primary).
                    Ui.AddClasses(b, "lineup-card__recruit font-title");
                    b.SetEnabled(v.RecruitEnabled);
                    card.Add(b);
                }
                if (!string.IsNullOrEmpty(v.Tip)) tips.Attach(card, v.Tip);
                return card;
            }
            card.style.borderTopColor = Ui.Hex(v.Accent, new Color(0.95f, 0.65f, 0.2f));
            card.EnableInClassList("lineup-card--ready", v.Ready);
            card.EnableInClassList("lineup-card--fallen", v.Fallen);
            var bust = Ui.El("lineup-card__bust", card);
            bust.style.borderTopColor = bust.style.borderBottomColor = bust.style.borderLeftColor = bust.style.borderRightColor = Ui.Hex(v.Accent, new Color(0.95f, 0.65f, 0.2f));
            var portrait = PortraitOf?.Invoke(v.Id);
            if (portrait is RenderTexture rt) bust.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else if (portrait is Texture2D t2) bust.style.backgroundImage = new StyleBackground(t2);
            if (v.Removable)
            {
                var id = v.Id;
                var x = new Button(() => handlers.OnSquadToggle(id, false)) { text = "×" };
                x.AddToClassList("lineup-card__remove");
                tips.Attach(x, StrategyText.Squad.RemoveTip);
                card.Add(x);
            }
            Ui.Text(v.Name, "lineup-card__name font-title", card);
            for (var i = 0; i < v.Lines.Count; i++) Ui.Text(v.Lines[i], i == 0 ? "lineup-card__line lineup-card__line--first" : "lineup-card__line", card);
            foreach (var l in v.Learned)
            {
                var tag = Ui.El("lineup-learned lineup-learned--" + l.Kind, card);
                if (l.Kind == "point")
                {
                    Ui.Text("→", "lineup-learned__arrow", tag);
                    Ui.Text($"+{l.Count}", "lineup-learned__hex font-title", tag);
                    tips.Attach(tag, l.Tip ?? StrategyText.Learned.PointsTip);
                }
                var text = Ui.Text(l.Kind == "point" ? StrategyText.Learned.Points(l.Count) : l.Text, "lineup-learned__text font-title", tag);
                if (l.Line >= 0)
                {
                    var color = SkillHex.Colors[Mathf.Clamp(l.Line, 0, SkillHex.Colors.Length - 1)];
                    if (l.Kind == "bar") tag.style.backgroundColor = color;
                    else
                    {
                        text.style.color = color;
                        tag.style.borderLeftColor = color;
                    }
                }
                if (!string.IsNullOrEmpty(l.Tip)) tips.Attach(tag, l.Tip);
            }
            if (!string.IsNullOrEmpty(v.Tip)) tips.Attach(card, v.Tip);
            return card;
        }

        /// <summary>Each card under its pad, as wide as the spacing allows, above the floor.</summary>
        void Place()
        {
            if (cards.Count == 0 || PadAt == null || layer.resolvedStyle.display == DisplayStyle.None) return;
            var n = cards.Count;
            var at = new Vector2?[n];
            for (var i = 0; i < n; i++) at[i] = PadAt(i, n);
            var spacing = MaxWidth + Gap;
            for (var i = 1; i < n; i++)
                if (at[i].HasValue && at[i - 1].HasValue) spacing = Mathf.Min(spacing, Mathf.Abs(at[i].Value.x - at[i - 1].Value.x));
            var width = Mathf.Clamp(spacing - Gap, MinWidth, MaxWidth);
            var floor = Floor?.Invoke() ?? float.MaxValue;
            for (var i = 0; i < n; i++)
            {
                var card = cards[i];
                Ui.Show(card, at[i].HasValue);
                if (!at[i].HasValue) continue;
                var p = at[i].Value;
                var height = card.resolvedStyle.height;
                var top = Mathf.Min(p.y, floor - (float.IsNaN(height) ? 0 : height) - 8);
                card.style.left = p.x - width / 2;
                card.style.top = top;
                card.style.width = width;
            }
        }
    }
}
