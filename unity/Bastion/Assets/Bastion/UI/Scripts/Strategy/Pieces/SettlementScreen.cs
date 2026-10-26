using System;
using System.Collections.Generic;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The Asentamiento (screen.ts renderFacilities, buildMode.ts): on the left a column of plates, Talar and the palette
    /// by kind (a grid of pictures with the name and the cost under each, what it does on hover), which scrolls as one;
    /// on the right the Bastion in figures, «Andar por la base» and the card of the piece picked on the clearing. The
    /// clearing, the piece by the pointer and putting it down are Arte's; this says what is in hand.
    /// </summary>
    public sealed class SettlementScreen
    {
        readonly VisualElement left;
        readonly ScrollView leftScroll;
        readonly VisualElement right;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        string key;
        bool shown;

        /// <summary>A piece's picture by id (Arte's renders); null, the 0.10.1's (Resources/Pieces).</summary>
        public Func<string, Texture> ArtOf;

        public SettlementScreen(VisualElement view, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            left = Ui.El("settle-col settle-col--left", view);
            left.pickingMode = PickingMode.Ignore;
            // Its scrollbar slim and over the padding (Strategy.uss), only when there is more below: a big interface cuts it.
            leftScroll = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Auto, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            leftScroll.AddToClassList("settle-scroll");
            left.Add(leftScroll);
            // The plates take the clicks; between and under them they reach the clearing.
            right = Ui.El("settle-col settle-col--right", view);
            right.pickingMode = PickingMode.Ignore;
        }

        public void Show(bool on)
        {
            shown = on;
            Ui.Show(left, on);
            Ui.Show(right, on);
        }

        /// <summary>
        /// The plates on screen now, in the panel's pixels: what Arte's camera leaves the clearing clear of
        /// (Settlement.SetInsets).
        /// </summary>
        public List<Rect> Plates()
        {
            var out_ = new List<Rect>();
            if (!shown) return out_;
            foreach (var column in new[] { leftScroll.contentContainer, right })
                foreach (var plate in column.Children())
                    if (plate.resolvedStyle.display == DisplayStyle.Flex) out_.Add(plate.worldBound);
            return out_;
        }

        public void Set(SettlementView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            var body = leftScroll.contentContainer;
            body.Clear();
            right.Clear();
            if (v == null) return;
            if (v.Fell != null) Fell(v.Fell, Plate(body));
            foreach (var g in v.Groups) Group(g, v.Pending, Plate(body));
            if (v.Stats.Count > 0) Stats(v, Plate(right));
            if (!string.IsNullOrEmpty(v.Walk))
            {
                var walk = new Button(handlers.OnWalk) { text = v.Walk.ToUpperInvariant() };
                Ui.AddClasses(walk, "settle-walk");
                Plate(right).Add(walk);
            }
            if (v.Card != null) Card(v.Card, Plate(right));
        }

        static VisualElement Plate(VisualElement column) => Ui.El("panel settle-plate", column);

        void Fell(FellView f, VisualElement plate)
        {
            var b = new Button(f.On ? (Action)handlers.OnBuildDrop : handlers.OnFell);
            Ui.AddClasses(b, "settle-fell");
            b.EnableInClassList("settle-fell--on", f.On);
            var axe = Ui.El("settle-fell__icon", b);
            axe.Add(new LineIcon("axe"));
            Ui.Text(f.Label.ToUpperInvariant(), "settle-fell__label font-title", b);
            var cost = Cost(f.Supplies, 0, b);
            cost.AddToClassList("settle-fell__cost");
            if (!string.IsNullOrEmpty(f.Per)) Ui.Text(f.Per, "settle-cost__per", cost);
            if (!string.IsNullOrEmpty(f.Tip)) tips.Attach(b, f.Tip);
            plate.Add(b);
            if (f.On && !string.IsNullOrEmpty(f.Keys)) Ui.Text(f.Keys, "settle-keys", plate);
        }

        void Group(PaletteGroupView g, string pending, VisualElement plate)
        {
            var head = Ui.El("strat-head settle-head", plate);
            Ui.Text(g.Title.ToUpperInvariant(), "strat-head__text font-title", head);
            Ui.El("settle-head__rule", head);
            if (!string.IsNullOrEmpty(pending)) Ui.Text(pending, "settle-pending", plate);
            var grid = Ui.El("settle-grid", plate);
            foreach (var t in g.Tiles) Tile(t, grid);
            if (!string.IsNullOrEmpty(g.Keys)) Ui.Text(g.Keys, "settle-keys", plate);
        }

        void Tile(PieceTileView t, VisualElement grid)
        {
            var id = t.Id;
            var on = t.On;
            var b = new Button(() =>
            {
                if (on) handlers.OnBuildDrop();
                else handlers.OnPieceTake(id);
            });
            Ui.AddClasses(b, "settle-tile");
            b.EnableInClassList("settle-tile--on", t.On);
            b.EnableInClassList("settle-tile--built", t.Built);
            b.EnableInClassList("settle-tile--short", t.Short);
            var pic = Ui.El("settle-tile__pic", b);
            var texture = Art(t.Id);
            if (texture is Texture2D still) pic.style.backgroundImage = new StyleBackground(still);
            else if (texture is RenderTexture rt) pic.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            Ui.Text(t.Name, "settle-tile__name", b);
            if (t.Built) Ui.Text("✓", "settle-cost settle-cost--built font-title", b);
            else Cost(t.Supplies, t.Parts, b);
            if (t.New)
            {
                var tag = Ui.Text(StrategyText.Tabs.NewTag, "strat-tab__new settle-tile__new font-title", b);
                var seen = t.NewKey;
                b.RegisterCallback<PointerEnterEvent>(e =>
                {
                    tag.RemoveFromHierarchy();
                    if (!string.IsNullOrEmpty(seen)) handlers.OnSeen(seen);
                });
            }
            // A built one doesn't take the pointer: its help goes on a box round it (a disabled button gets no hover).
            if (t.Built)
            {
                b.SetEnabled(false);
                var holder = Ui.El("settle-tile__holder", grid);
                holder.Add(b);
                if (!string.IsNullOrEmpty(t.Tip)) tips.Attach(holder, t.Tip);
                return;
            }
            if (!string.IsNullOrEmpty(t.Tip)) tips.Attach(b, t.Tip);
            grid.Add(b);
        }

        /// <summary>A piece's picture: Arte's (PiecePictures, by StrategyRoot.PieceArtOf).</summary>
        Texture Art(string id) => ArtOf?.Invoke(id);

        /// <summary>A cost as the stores show it: the supplies in their gold with the crate, the parts after with their wrench.</summary>
        VisualElement Cost(int supplies, int parts, VisualElement parent)
        {
            var row = Ui.El("settle-cost", parent);
            Ui.Text($"{supplies} §", "settle-cost__supplies font-title", row);
            if (parts > 0)
            {
                var p = Ui.El("settle-cost__parts", row);
                Ui.Text(parts.ToString(), "settle-cost__parts-n font-title", p);
                Ui.Icon(icon, "store:parts", "settle-cost__parts-icon", p);
                tips.Attach(p, parts == 1 ? "1 pieza" : $"{parts} piezas");
            }
            return row;
        }

        void Stats(SettlementView v, VisualElement plate)
        {
            var head = Ui.El("strat-head settle-head", plate);
            Ui.Text((v.StatsHead ?? "").ToUpperInvariant(), "strat-head__text font-title", head);
            Ui.El("settle-head__rule", head);
            var grid = Ui.El("settle-stats", plate);
            foreach (var s in v.Stats)
            {
                var tile = Ui.El("settle-stat well", grid);
                var i = Ui.Icon(icon, s.Icon, "settle-stat__icon", tile);
                if (!string.IsNullOrEmpty(s.IconColor)) i.style.unityBackgroundImageTintColor = Ui.Hex(s.IconColor, Color.white);
                var words = Ui.El("settle-stat__words", tile);
                var value = Ui.Text(s.Value, "settle-stat__value font-title", words);
                if (!string.IsNullOrEmpty(s.Tone)) value.AddToClassList("settle-stat__value--" + s.Tone);
                Ui.Text(s.Label, "settle-stat__label", words);
            }
        }

        void Card(PieceCardView c, VisualElement plate)
        {
            var top = Ui.El("settle-card__top", plate);
            Ui.Text(c.Title.ToUpperInvariant(), "settle-card__title font-title", top);
            var close = new Button(handlers.OnPieceClose) { text = "✕" };
            Ui.AddClasses(close, "settle-card__close");
            if (!string.IsNullOrEmpty(c.CloseTip)) tips.Attach(close, c.CloseTip);
            top.Add(close);
            if (!string.IsNullOrEmpty(c.Figure)) Ui.Text(c.Figure, "settle-card__figure font-title", plate);
            else if (!string.IsNullOrEmpty(c.Line)) Ui.Text(c.Line, "settle-card__line muted", plate);
            if (c.Ladder.Count > 0)
            {
                var ladder = Ui.El("settle-ladder", plate);
                foreach (var s in c.Ladder)
                {
                    var step = Ui.El("settle-step", ladder);
                    step.EnableInClassList("settle-step--now", s.Now);
                    step.EnableInClassList("settle-step--past", s.Past);
                    Ui.Text(s.Label.ToUpperInvariant(), "settle-step__label", step);
                    Ui.Text(s.Figure, "settle-step__figure font-title", step);
                }
            }
            if (!string.IsNullOrEmpty(c.LevelLine)) Ui.Text(c.LevelLine, "settle-card__level", plate);
            if (!string.IsNullOrEmpty(c.Holds))
            {
                var holds = Ui.Text(c.Holds, "settle-card__holds", plate);
                if (!string.IsNullOrEmpty(c.HoldsTip)) tips.Attach(holds, c.HoldsTip);
            }
            if (!string.IsNullOrEmpty(c.Uses)) Ui.Text(c.Uses, "settle-card__uses muted", plate);
            if (!string.IsNullOrEmpty(c.State)) Ui.Text(c.State, "strat-why", plate);
            if (!string.IsNullOrEmpty(c.Note)) Ui.Text(c.Note, "settle-card__line muted", plate);
            if (c.Actions.Count == 0 && c.Confirm == null) return;
            var actions = Ui.El("settle-actions", plate);
            foreach (var a in c.Actions)
            {
                if (a.Action == "dismantle" && c.Confirm != null)
                {
                    Confirm(c.Confirm, actions);
                    continue;
                }
                Action(a, actions);
            }
        }

        void Action(PieceActionView a, VisualElement parent)
        {
            var box = Ui.El("settle-action", parent);
            var what = a.Action;
            var b = new Button(() => handlers.OnPieceAction(what)) { text = a.Label.ToUpperInvariant() };
            Ui.AddClasses(b, "settle-action__button");
            b.SetEnabled(string.IsNullOrEmpty(a.Why));
            box.Add(b);
            if (a.New)
            {
                var tag = Ui.Text(StrategyText.Tabs.NewTag, "strat-tab__new settle-action__new font-title", b);
                var seen = a.NewKey;
                box.RegisterCallback<PointerEnterEvent>(e =>
                {
                    tag.RemoveFromHierarchy();
                    if (!string.IsNullOrEmpty(seen)) handlers.OnSeen(seen);
                });
            }
            var hover = a.Hover ?? a.Tip;
            if (!string.IsNullOrEmpty(hover)) tips.Attach(box, hover);
            if (!string.IsNullOrEmpty(a.Why)) Ui.Text(a.Why, "strat-why strat-why--small", box);
            else if (!string.IsNullOrEmpty(a.Tip)) Ui.Text(a.Tip, "settle-action__tip muted", box);
        }

        void Confirm(PieceConfirmView c, VisualElement parent)
        {
            var row = Ui.El("settle-confirm", parent);
            Ui.Text(c.Text, "settle-confirm__text", row);
            var buttons = Ui.El("settle-confirm__buttons", row);
            var yes = new Button(() => handlers.OnPieceAction("dismantleYes")) { text = c.Yes.ToUpperInvariant() };
            Ui.AddClasses(yes, "settle-confirm__button");
            buttons.Add(yes);
            var no = new Button(() => handlers.OnPieceAction("dismantleNo")) { text = c.No.ToUpperInvariant() };
            Ui.AddClasses(no, "settle-confirm__button");
            buttons.Add(no);
        }
    }
}
