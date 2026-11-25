using System;
using System.Linq;
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
        /// <summary>
        /// The offers' list as it was scrolled, for the plate drawn again (a question opened or closed): it went back to the top,
        /// and the second click of a double click landed on another row (the 0.14.4's independent review, 1). By whose they are.
        /// </summary>
        ScrollView offersList;
        Vector2 offersAt;
        string offersOf;
        /// <summary>The Trueque's whole words, scrolling when the plate is taller than the room (runstop__body), kept as the offers'.</summary>
        ScrollView bodyList;
        Vector2 bodyAt;

        public StopPlate(VisualElement parent, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
            Root = Ui.El("runstop", parent);
            Ui.Show(Root, false);
            parent.RegisterCallback<GeometryChangedEvent>(_ => FitHeight());
            Root.RegisterCallback<GeometryChangedEvent>(_ => FitHeight());
        }

        /// <summary>
        /// The Trueque's plate no lower than the map's foot (the 0.14.4's review, 5: at 130 % on 1280 × 800 its two sides went one
        /// under the other and the second ran off the screen): its words scroll inside it. Other plates as they were.
        /// </summary>
        void FitHeight()
        {
            var parent = Root.parent;
            if (!Root.ClassListContains("runstop--trade") || parent == null || float.IsNaN(parent.layout.height) || float.IsNaN(Root.layout.y))
            {
                if (fitRoom >= 0) Root.style.maxHeight = StyleKeyword.Null;
                fitRoom = -1;
                return;
            }
            var foot = parent.layout.height - 8;
            // Over the next stages only when that takes little off it (FloorTrim): with the Trueque high on the map they are at
            // its top, and the plate would be left too small to use; it covers them then, as before.
            if (Floor?.Invoke() is float floor && !float.IsNaN(floor) && floor < foot && bodyList != null)
            {
                var s = Root.resolvedStyle;
                var natural = s.paddingTop + s.paddingBottom + s.borderTopWidth + s.borderBottomWidth;
                foreach (var child in Root.Children())
                    natural += child == bodyList ? bodyList.contentContainer.layout.height : child.layout.height + child.resolvedStyle.marginTop + child.resolvedStyle.marginBottom;
                if (!float.IsNaN(natural) && Root.layout.y + natural - floor <= FloorTrim) foot = floor;
            }
            var room = Mathf.Round(foot - Root.layout.y);
            if (room <= 0 || Mathf.Abs(room - fitRoom) < 1) return;
            fitRoom = room;
            Root.style.maxHeight = room;
        }

        float fitRoom = -1;

        /// <summary>The most the Trueque's plate gives up (scrolled) to stay over the next stages (Floor).</summary>
        const float FloorTrim = 80;

        /// <summary>Fitted again: the map laid out anew, its next stages elsewhere (Floor).</summary>
        public void Refit() => FitHeight();

        /// <summary>Where the Trueque's plate must end over (the map's next stages: RunMapScreen.NextRowTop), in its parent's pixels; NaN, nowhere.</summary>
        public Func<float> Floor;

        public void Set(StopView v)
        {
            Ui.Show(Root, v != null);
            last = v;
            // (Gone, or the stop over: no question left open.)
            if (confirming != null && (v?.Offers == null || !v.Offers.Exists(o => o.Gear.Id == confirming))) confirming = null;
            var k = Ui.Key(v) + "|" + confirming;
            if (k == key) return;
            key = k;
            if (offersList != null) offersAt = offersList.scrollOffset;
            if (bodyList != null) bodyAt = bodyList.scrollOffset;
            offersList = bodyList = null;
            if (v?.OffersFrom != offersOf) offersAt = bodyAt = Vector2.zero;
            offersOf = v?.OffersFrom;
            Root.Clear();
            if (v == null) return;
            // The Trueque (M10): its words in a list that scrolls when the plate is taller than the room (FitHeight).
            var trade = v.Offers != null;
            Root.EnableInClassList("runstop--trade", trade);
            VisualElement into = Root;
            if (trade)
            {
                var body = new ScrollView(ScrollViewMode.Vertical);
                body.AddToClassList("runstop__body");
                body.AddToClassList("settle-scroll");
                body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                Root.Add(body);
                into = body.contentContainer;
                bodyList = body;
                KeepScroll(body, bodyAt);
            }
            // The small line over the title only when it says something else (Arturo, 17/11: «TRUEQUE / Trueque» said it
            // twice); a Suceso's name under its kind keeps it.
            if (!string.IsNullOrEmpty(v.Kicker) && !string.Equals(v.Kicker, v.Title, System.StringComparison.OrdinalIgnoreCase))
                Ui.Text(v.Kicker.ToUpperInvariant(), "runstop__kicker font-title", into);
            Ui.Text(v.Title ?? "", "runstop__title font-title", into);
            if (!string.IsNullOrEmpty(v.Text)) Ui.Text(v.Text, "runstop__text", into);

            if (v.Wares.Count > 0 || !string.IsNullOrEmpty(v.Purse))
            {
                var purse = Ui.El("runstop__purse", into);
                // «Tus soldados llevan [95 and the crate].»: a stop after it, close to the crate.
                var line = Ui.El("runstop__purse-line", purse);
                var tail = StrategyText.Runs.PurseAfter;
                Ui.Text(StrategyText.Runs.PurseBefore, "runstop__purse-text font-title", line);
                SuppliesMark.Make(line, v.Purse, "supplies-mark--small runstop__purse-amount" + (tail.Length > 0 && char.IsPunctuation(tail[0]) ? " runstop__purse-amount--end" : ""));
                Ui.Text(StrategyText.Runs.PurseAfter, "runstop__purse-text font-title", line);
                // (U2: that only what they carry counts, on hover; whose pack it goes to, by the wares' head.)
                if (!string.IsNullOrEmpty(v.PurseNote)) tips.Attach(line, v.PurseNote);
                // The Trueque's two sides (M10): what the trader has, and what the chosen soldier has for him; side by side
                // where they fit, the second under the first where not.
                var sides = v.Offers != null ? Ui.El("runstop__sides", into) : into;
                var has = v.Offers != null ? Ui.El("runstop__side", sides) : into;
                if (v.Offers != null)
                {
                    var wares = Ui.El("runstop__side-heads", has);
                    Ui.Text(StrategyText.Runs.Wares.ToUpperInvariant(), "runstop__side-head font-title", wares);
                    if (!string.IsNullOrEmpty(v.ForName)) tips.Attach(Ui.Text("→ " + v.ForName, "runstop__side-for font-title", wares), v.For);
                }
                var list = Ui.El("runstop__wares", has);
                foreach (var w in v.Wares) Ware(list, w, false);
                if (v.Offers != null)
                {
                    var offer = Ui.El("runstop__side", sides);
                    var offersHead = Ui.Text(StrategyText.Runs.Offers.ToUpperInvariant(), "runstop__side-head font-title", offer);
                    // (U2: whose things and how to offer another's, on hover; with nothing to offer, said.)
                    if (v.Offers.Count == 0 && !string.IsNullOrEmpty(v.OffersFrom)) Ui.Text(v.OffersFrom, "runstop__for muted", offer);
                    else
                    {
                        var about = string.Join(" ", new[] { v.OffersFrom, v.OffersOther }.Where(x => !string.IsNullOrEmpty(x)));
                        if (about.Length > 0) tips.Attach(offersHead, about);
                    }
                    var offered = new ScrollView(ScrollViewMode.Vertical);
                    // The game's thin and faint bar (the sides' and the settlement's), not Unity's.
                    offered.AddToClassList("runstop__offers");
                    offered.AddToClassList("settle-scroll");
                    // (Never sideways: a row asking grows a line under it, never wider.)
                    offered.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                    offer.Add(offered);
                    foreach (var w in v.Offers) Ware(offered.contentContainer, w, true);
                    offersList = offered;
                    KeepScroll(offered, offersAt);
                }
            }

            if (v.Options.Count > 0)
            {
                var options = Ui.El("runstop__options", into);
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
            if (!string.IsNullOrEmpty(v.Vote)) Ui.Text(v.Vote, "runstop__vote muted", into);
            if (!string.IsNullOrEmpty(v.Outcome)) Ui.Text(v.Outcome, "runstop__outcome", into);

            if (!string.IsNullOrEmpty(v.Leave))
            {
                // (In the Trueque, «Seguir» under its words that scroll, always in sight: the 0.14.4's pass, 1920.)
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
        /// <summary>A list drawn again where it was scrolled to, once laid out (before, the offset is clamped to nothing).</summary>
        static void KeepScroll(ScrollView list, Vector2 at)
        {
            if (at == Vector2.zero) return;
            void Back(GeometryChangedEvent e)
            {
                list.scrollOffset = at;
                list.UnregisterCallback<GeometryChangedEvent>(Back);
            }
            list.RegisterCallback<GeometryChangedEvent>(Back);
        }

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
            var nameRow = Ui.El("runstop__ware-name-row", words);
            var name = Ui.Text(w.Gear.Name, "runstop__ware-name", nameRow);
            tips.Attach(name, w.Gear.Tip);
            // Worn (U2): a mark by its name, «Lo lleva puesto» on hover.
            if (w.Note != null) tips.Attach(Ui.Text(StrategyText.Runs.WornTag, "runstop__worn font-title", nameRow), w.Note);
            // Given by the base (cuadro 165): its lock, why on hover.
            if (w.Gear.Issued) tips.Attach(Ui.Icon(icon, "lock", "runstop__issued", nameRow), StrategyText.Stash.Issued);
            // Short of supplies (U2): its price red, the reason on hover; another reason stays written (B18).
            if (w.Why != null && !w.Short) Ui.Text(w.Why, "runstop__ware-why", words);
            // (No price for what the base gave, cuadro 165: it isn't offered.)
            if (w.Price != null)
            {
                var price = SuppliesMark.Make(row, w.Price, "supplies-mark--small runstop__price" + (offered ? " runstop__price--gain" : "") + (w.Short ? " runstop__price--short" : ""));
                if (w.Short && w.Why != null) tips.Attach(price, w.Why);
            }
            else Ui.El("runstop__price", row);
            var ware = w.Gear.Id;
            var worn = offered && w.Note != null;
            var b = new Button(() =>
            {
                // For a moment after a question opens or closes, no key of the plate buys or sells: the second click of a
                // double click lands on the plate drawn again, on whatever row is there (the 0.14.4's independent review, 1).
                if (Time.realtimeSinceStartup - askedAt < AskGrace) return;
                if (!offered) handlers.OnBuy(ware);
                else if (worn && confirming != ware) Ask(ware);
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
            // What is worn goes for good: asked first, on a line of its own under the row, which stays as it was: «Cambiar»
            // where it was (a double click presses it twice, the second heeding nothing for AskGrace), «Cancelar» on the
            // other side, under the picture, and the question beside it under the name, room enough for it (R23 B7).
            if (asking)
            {
                row.AddToClassList("runstop__ware--asking");
                var below = Ui.El("runstop__ware-below", row);
                var no = new Button(() => Ask(null)) { text = StrategyText.Runs.SellNo };
                Ui.AddClasses(no, "runstop__buy runstop__no font-title");
                below.Add(no);
                Ui.Text(StrategyText.Runs.SellWorn(w.Gear.Kind, w.Gear.Type, w.Gear.Count, w.Price), "runstop__ware-ask", below);
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
