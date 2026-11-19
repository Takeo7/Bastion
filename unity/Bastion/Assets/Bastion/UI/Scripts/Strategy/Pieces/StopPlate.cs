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
            var k = Ui.Key(v);
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
                var list = Ui.El("runstop__wares", Root);
                foreach (var w in v.Wares) Ware(list, w);
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

        /// <summary>A ware: its picture, name, what it costs and «Cambiar».</summary>
        void Ware(VisualElement list, WareView w)
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
            var name = Ui.Text(w.Gear.Name, "runstop__ware-name", row);
            tips.Attach(name, w.Gear.Tip);
            SuppliesMark.Make(row, w.Price, "supplies-mark--small runstop__price");
            var ware = w.Gear.Id;
            var b = new Button(() => handlers.OnBuy(ware)) { text = StrategyText.Runs.Swap };
            Ui.AddClasses(b, "runstop__buy font-title");
            row.Add(b);
            if (w.Why != null)
            {
                b.SetEnabled(false);
                tips.Attach(b, w.Why);
            }
        }
    }
}
