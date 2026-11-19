using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>Whether a card has the soldier's bust: not in Escuadra, where the figure over it shows the face (Ana, Arturo 01/11 #2).</summary>
        public bool Busts = true;
        /// <summary>The lowest a card's bottom may go (the top of the panels at the bottom), in panel pixels.</summary>
        public Func<float> Floor;
        /// <summary>All the cards at one height, the lowest pad's and a little under it, clear of the boots (Arturo's report of 7/11).</summary>
        public bool Level;
        /// <summary>Every card this wide, whatever the pads' spacing (the report's 200 px at 2560 × 1440); null, by the spacing.</summary>
        public float? Width;
        /// <summary>Under the boots, how far a level card goes (Level): the maqueta's 45 px at 1440 high (Arte: PadWorld is where the boots stand).</summary>
        const float BelowBoots = 27;

        /// <summary>The cards' layer, for a screen's own class.</summary>
        public VisualElement Layer => layer;

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
            var k = Ui.Key(views) + "|" + StrategyRoot.PictureStamp;
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
            // The owner's colour on the top edge, but green when ready and red when fallen (their classes: an inline colour
            // would hide them).
            if (!v.Ready && !v.Fallen) card.style.borderTopColor = Ui.Hex(v.Accent, new Color(0.95f, 0.65f, 0.2f));
            card.EnableInClassList("lineup-card--ready", v.Ready);
            card.EnableInClassList("lineup-card--fallen", v.Fallen);
            if (Busts)
            {
                card.AddToClassList("lineup-card--bust");
                var bust = Ui.El("lineup-card__bust", card);
                if (!v.Fallen) bust.style.borderTopColor = bust.style.borderBottomColor = bust.style.borderLeftColor = bust.style.borderRightColor = Ui.Hex(v.Accent, new Color(0.95f, 0.65f, 0.2f));
                var portrait = PortraitOf?.Invoke(v.Id);
                if (portrait is RenderTexture rt) bust.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                else if (portrait is Texture2D t2) bust.style.backgroundImage = new StyleBackground(t2);
            }
            if (v.Removable)
            {
                var id = v.Id;
                var x = new Button(() => handlers.OnSquadToggle(id, false)) { text = "×" };
                x.AddToClassList("lineup-card__remove");
                tips.Attach(x, StrategyText.Squad.RemoveTip);
                card.Add(x);
            }
            Ui.Text(v.Name, "lineup-card__name font-title", card);
            // strategy.css .lineup-card.ready::after, its stamp, but «A PUNTO» as the player's state says everywhere since Lore's
            // pass (without gender: «LISTO» spoke of a soldier as a man); mixta-base.css .lineup-card.dead::after, a red «Caído» tag.
            if (v.Ready) Ui.Text("A PUNTO", "lineup-card__stamp font-title", card);
            if (v.Fallen) Ui.Text("CAÍDO", "lineup-card__dead font-title", card);
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
                var text = Ui.Text(l.Kind == "point" ? StrategyText.Learned.Points(l.Count) : Glued(l.Text), "lineup-learned__text font-title", tag);
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
            // The stage's report (U10): what it can learn now, a key to its board.
            if (!string.IsNullOrEmpty(v.Learn))
            {
                var id = v.Id;
                var learn = new Button(() => handlers.OnBoardFor(id)) { text = "▲ " + Glued(v.Learn.ToUpperInvariant()) };
                Ui.AddClasses(learn, "skill-board skill-board--pulse lineup-card__learn font-title");
                card.Add(learn);
            }
            if (!string.IsNullOrEmpty(v.Tip)) tips.Attach(card, v.Tip);
            return card;
        }

        /// <summary>A card's line on two lines keeps its «%» with the figure («+10 %»): a space that doesn't break.</summary>
        private static string Glued(string text) => text.Replace(" %", " %");

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
            var width = Width ?? Mathf.Clamp(spacing - Gap, MinWidth, MaxWidth);
            var floor = Floor?.Invoke() ?? float.MaxValue;
            var level = float.MinValue;
            if (Level)
                for (var i = 0; i < n; i++)
                    if (at[i].HasValue) level = Mathf.Max(level, at[i].Value.y + BelowBoots);
            var xs = Bastion.Client.GameInput.Touch ? Apart(at, ref width) : null;
            // Spread apart, the row at one height: under the lowest pad, or higher only as the tallest card needs to stay
            // above the floor (Gameplay, 8/11: on the Duo the debrief's tallest card stood out above the rest).
            float? shared = null;
            if (xs != null)
            {
                var lowest = float.MinValue;
                var cap = float.MaxValue;
                for (var i = 0; i < n; i++)
                {
                    if (!at[i].HasValue) continue;
                    lowest = Mathf.Max(lowest, Level ? level : at[i].Value.y);
                    var h = cards[i].resolvedStyle.height;
                    cap = Mathf.Min(cap, floor - (float.IsNaN(h) ? 0 : h) - 8);
                }
                shared = Mathf.Min(lowest, cap);
            }
            for (var i = 0; i < n; i++)
            {
                var card = cards[i];
                Ui.Show(card, at[i].HasValue);
                if (!at[i].HasValue) continue;
                var p = at[i].Value;
                if (xs != null) p.x = xs[i];
                var height = card.resolvedStyle.height;
                var top = shared ?? Mathf.Min(Level ? level : p.y, floor - (float.IsNaN(height) ? 0 : height) - 8);
                card.style.left = p.x - width / 2;
                card.style.top = top;
                card.style.width = width;
            }
        }

        /// <summary>The panel's edge a row spread apart keeps clear of (panel pixels).</summary>
        const float Edge = 12;

        /// <summary>
        /// With the finger (docs/IOS.md; Arturo's Duo, 8/11): on a screen of 951 points the figures stand closer than the
        /// cards are wide, and the cards sat one on another. Where two would touch, the row is spread out from its middle,
        /// in the figures' order, a card's width and a gap apart, kept on the panel (narrower if even the panel is short);
        /// null, none touch (each stays under its figure). The desktop keeps its cards under the figures.
        /// </summary>
        float[] Apart(Vector2?[] at, ref float width)
        {
            var shown = Enumerable.Range(0, at.Length).Where(i => at[i].HasValue).OrderBy(i => at[i].Value.x).ToList();
            if (shown.Count < 2) return null;
            var touching = false;
            for (var k = 1; k < shown.Count; k++)
                if (at[shown[k]].Value.x - at[shown[k - 1]].Value.x < width + Gap) touching = true;
            if (!touching) return null;
            var room = layer.layout.width;
            if (float.IsNaN(room) || room <= 0) room = float.MaxValue;
            // As wide as the row lets them (up to MaxWidth), unless they are all one width (Escuadra's).
            var fit = (room - 2 * Edge - (shown.Count - 1) * Gap) / shown.Count;
            width = Width.HasValue ? Mathf.Min(width, fit) : Mathf.Min(MaxWidth, fit);
            var step = width + Gap;
            var middle = shown.Average(i => at[i].Value.x);
            var row = shown.Count * width + (shown.Count - 1) * Gap;
            var left = Mathf.Clamp(middle - row / 2, Edge, Mathf.Max(Edge, room - Edge - row));
            var xs = new float[at.Length];
            for (var k = 0; k < shown.Count; k++) xs[shown[k]] = left + width / 2 + k * step;
            return xs;
        }
    }
}
