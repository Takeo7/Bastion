using System;
using Bastion.UI.Gear;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// A stop on the way (H4; .dev-snapshots/gameplay/runs/PLAN-RUNS.md §3.6, Lore's SUCESOS.md), a plate over the map:
    /// - **Refugio:** the rest, already had (everyone healed), and «Seguir».
    /// - **Trueque:** the wares, each with what it costs and «Cambiar» (OnBuy), into the pack of the soldier chosen on the
    ///   right; the supplies the player's soldiers carry.
    /// - **Suceso:** its text and two options, each with what it does beside it (OnOption: a vote with two); then what
    ///   happened.
    /// - **Superviviente:** who asks to come, and yes or no (OnOption 0 or 1).
    /// «Seguir» (OnLeaveStop) once answered; with two, it waits for both.
    /// </summary>
    public sealed class StopPlate
    {
        public readonly VisualElement Root;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        string key;
        StopView last;
        /// <summary>The worn thing whose sale is being asked about in its row (M10, Producto 24/11); null, none.</summary>
        string confirming;
        /// <summary>
        /// When the question opened (the real clock): its «Cambiar» heeds no press for AskGrace after it, so the second click
        /// of a double click, landing on the plate drawn again, doesn't sell unasked (the 0.14.3's independent review, 4).
        /// </summary>
        float askedAt;
        const float AskGrace = 0.5f;

        public StopPlate(VisualElement parent, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            Root = Ui.El("runstop", parent);
            Ui.Show(Root, false);
        }

        public void Set(StopView v)
        {
            Ui.Show(Root, v != null);
            last = v;
            // (Gone, or the stop over: no question left open.)
            if (confirming != null && (v?.Offers == null || !v.Offers.Exists(o => o.Gear.Id == confirming))) confirming = null;
            var k = Ui.Key(v) + "|" + confirming;
            if (k == key) return;
            key = k;
            Root.Clear();
            if (v == null) return;
            // The small line over the title only when it says something else (Arturo, 17/11: «TRUEQUE / Trueque» said it
            // twice); a Suceso's name under its kind keeps it.
            if (!string.IsNullOrEmpty(v.Kicker) && !string.Equals(v.Kicker, v.Title, System.StringComparison.OrdinalIgnoreCase))
                Ui.Text(v.Kicker.ToUpperInvariant(), "runstop__kicker font-title", Root);
            Ui.Text(v.Title ?? "", "runstop__title font-title", Root);
            if (!string.IsNullOrEmpty(v.Text)) Ui.Text(v.Text, "runstop__text", Root);

            if (v.Wares.Count > 0 || !string.IsNullOrEmpty(v.Purse))
            {
                var purse = Ui.El("runstop__purse", Root);
                // «Tus soldados llevan [95 and the crate].»: a stop after it, close to the crate.
                var line = Ui.El("runstop__purse-line", purse);
                var tail = StrategyText.Runs.PurseAfter;
                Ui.Text(StrategyText.Runs.PurseBefore, "runstop__purse-text font-title", line);
                SuppliesMark.Make(line, v.Purse, "supplies-mark--small runstop__purse-amount" + (tail.Length > 0 && char.IsPunctuation(tail[0]) ? " runstop__purse-amount--end" : ""));
                Ui.Text(StrategyText.Runs.PurseAfter, "runstop__purse-text font-title", line);
                if (!string.IsNullOrEmpty(v.For)) Ui.Text(v.For, "runstop__for muted", purse);
                if (!string.IsNullOrEmpty(v.PurseNote)) Ui.Text(v.PurseNote, "runstop__for muted", purse);
                // The Trueque's two sides (M10): what the trader has, and what the chosen soldier has for him; side by side
                // where they fit, the second under the first where not.
                var sides = v.Offers != null ? Ui.El("runstop__sides", Root) : Root;
                Root.EnableInClassList("runstop--trade", v.Offers != null);
                var has = v.Offers != null ? Ui.El("runstop__side", sides) : Root;
                if (v.Offers != null) Ui.Text(StrategyText.Runs.Wares.ToUpperInvariant(), "runstop__side-head font-title", has);
                var list = Ui.El("runstop__wares", has);
                foreach (var w in v.Wares) Ware(list, w, false);
                if (v.Offers != null)
                {
                    var offer = Ui.El("runstop__side", sides);
                    Ui.Text(StrategyText.Runs.Offers.ToUpperInvariant(), "runstop__side-head font-title", offer);
                    if (!string.IsNullOrEmpty(v.OffersFrom)) Ui.Text(v.OffersFrom, "runstop__for muted", offer);
                    if (!string.IsNullOrEmpty(v.OffersOther)) Ui.Text(v.OffersOther, "runstop__for muted", offer);
                    var offered = new ScrollView(ScrollViewMode.Vertical);
                    // The game's thin and faint bar (the sides' and the settlement's), not Unity's.
                    offered.AddToClassList("runstop__offers");
                    offered.AddToClassList("settle-scroll");
                    // (Never sideways: a row asking, with «No» beside «Cambiar», is narrowed, not scrolled.)
                    offered.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                    offer.Add(offered);
                    foreach (var w in v.Offers) Ware(offered.contentContainer, w, true);
                }
            }

            if (v.Options.Count > 0)
            {
                var options = Ui.El("runstop__options", Root);
                foreach (var o in v.Options)
                {
                    var index = o.Index;
                    var b = new Button(() => handlers.OnOption(index));
                    Ui.AddClasses(b, "runstop__option");
                    Ui.Text(o.Label, "runstop__option-label font-title", b);
                    if (!string.IsNullOrEmpty(o.Short)) Ui.Text(o.Short, "runstop__option-short", b);
                    options.Add(b);
                    if (o.Why != null)
                    {
                        b.SetEnabled(false);
                        tips.Attach(b, o.Why);
                    }
                }
            }
            if (!string.IsNullOrEmpty(v.Vote)) Ui.Text(v.Vote, "runstop__vote muted", Root);
            if (!string.IsNullOrEmpty(v.Outcome)) Ui.Text(v.Outcome, "runstop__outcome", Root);

            if (!string.IsNullOrEmpty(v.Leave))
            {
                var foot = Ui.El("runstop__foot", Root);
                // An event not answered yet: no «Seguir» greyed under its two cards, which are what is pressed (Producto's
                // review, 23/11, B19); why, in its place.
                if (v.LeaveWhy != null && v.Options.Count > 0) Ui.Text(v.LeaveWhy, "runstop__waiting muted", foot);
                else
                {
                    var leave = new Button(() => handlers.OnLeaveStop());
                    Ui.AddClasses(leave, "runstop__leave font-title");
                    leave.EnableInClassList("runstop__leave--on", v.LeaveOn);
                    leave.text = v.Leave.ToUpperInvariant();
                    foot.Add(leave);
                    if (v.LeaveWhy != null)
                    {
                        leave.SetEnabled(false);
                        tips.Attach(leave, v.LeaveWhy);
                    }
                }
                // A Refugio's way home (G22, U21), after «Seguir la expedición»: OnEndRun asks first (EndAsk), never at once.
                if (!string.IsNullOrEmpty(v.Home))
                {
                    var home = new Button(() => handlers.OnEndRun());
                    Ui.AddClasses(home, "runstop__home font-title");
                    home.text = v.Home.ToUpperInvariant();
                    home.SetEnabled(v.HomeWhy == null);
                    tips.Attach(home, v.HomeWhy ?? v.HomeTip);
                    foot.Add(home);
                }
                if (!string.IsNullOrEmpty(v.Waiting)) Ui.Text(v.Waiting, "runstop__waiting muted", foot);
            }
        }

        /// <summary>A ware: its picture, name, what it costs and «Cambiar»; offered (M10), what it pays and «Cambiar» to sell.</summary>
        void Ware(VisualElement list, WareView w, bool offered)
        {
            var row = Ui.El("runstop__ware", list);
            var tile = Ui.El("runstop__tile gear-item--" + (w.Gear.Kind ?? "thing"), row);
            var id = w.Gear.Icon;
            // Arte's picture, as in the grids; else the HUD's icon.
            if (Bastion.Client.GearPictures.Of(w.Gear.Kind, w.Gear.Type) is Texture2D art)
            {
                var pic = Ui.El("runstop__art", tile);
                pic.pickingMode = PickingMode.Ignore;
                pic.style.backgroundImage = new StyleBackground(art);
            }
            else if (id != null && id.Length == 1) Ui.Text(id, "runstop__glyph font-title", tile);
            else if (LineIcon.Has(id)) tile.Add(new LineIcon(id) { pickingMode = PickingMode.Ignore });
            else Ui.Icon(icon, id, "runstop__icon", tile);
            // The name, and under it why it can't be had now (B18: «Faltan 25 §.», «No cabe en la mochila de Anja.»), not only on
            // the greyed key's help.
            var words = Ui.El("runstop__ware-words", row);
            var asking = offered && confirming == w.Gear.Id;
            var name = Ui.Text(w.Gear.Name, "runstop__ware-name", words);
            tips.Attach(name, w.Gear.Tip);
            // What is worn goes for good: asked first, in its row (its name, the question under it, «Cambiar» and «No»).
            if (asking) Ui.Text(StrategyText.Runs.SellWorn(w.Gear.Kind, w.Gear.Type, w.Gear.Count, w.Price), "runstop__ware-ask", words);
            else if (w.Why != null) Ui.Text(w.Why, "runstop__ware-why", words);
            else if (w.Note != null) Ui.Text(w.Note, "runstop__ware-note muted", words);
            SuppliesMark.Make(row, w.Price, "supplies-mark--small runstop__price" + (offered ? " runstop__price--gain" : ""));
            var ware = w.Gear.Id;
            var worn = offered && w.Note != null;
            var b = new Button(() =>
            {
                if (!offered) handlers.OnBuy(ware);
                else if (worn && confirming != ware) Ask(ware);
                else if (worn && Time.realtimeSinceStartup - askedAt < AskGrace) return;
                else
                {
                    confirming = null;
                    handlers.OnSell(ware);
                }
            }) { text = StrategyText.Runs.Swap };
            Ui.AddClasses(b, "runstop__buy font-title");
            row.Add(b);
            if (w.Why != null)
            {
                b.SetEnabled(false);
                tips.Attach(b, w.Why);
            }
            if (asking)
            {
                row.AddToClassList("runstop__ware--asking");
                var no = new Button(() => Ask(null)) { text = StrategyText.Runs.SellNo };
                Ui.AddClasses(no, "runstop__buy runstop__no font-title");
                row.Add(no);
            }
        }

        /// <summary>The question in a worn thing's row (`id`), or none: the plate drawn again as it is.</summary>
        void Ask(string id)
        {
            confirming = id;
            askedAt = Time.realtimeSinceStartup;
            Set(last);
        }
    }
}
