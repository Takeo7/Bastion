using System;
using Bastion.UI.Gear;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Almacén, the runs' base (H2; .dev-snapshots/gameplay/runs/PLAN-RUNS.md): on the left the shared stash in its grid,
    /// with the supplies it holds, how to handle things and the bin; on the right the soldiers to choose and the chosen
    /// one's kit (KitPanel). Between them, the clearing shows that soldier (Arte). Things go from one to another with the
    /// grids' board (GearBoard).
    /// </summary>
    public sealed class StashScreen
    {
        readonly VisualElement left;
        readonly KitPanel right;
        /// <summary>The soldiers' tabs and kit on the right (their busts: StrategyRoot).</summary>
        public KitPanel Kits => right;
        readonly TooltipLayer tips;
        readonly GearBoard board;
        readonly Label head;
        readonly VisualElement supplies;
        readonly VisualElement stash;
        readonly Label hint;
        readonly VisualElement bin;
        bool shown;
        StashScreenView view;

        public StashScreen(VisualElement view, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers, GearBoard board)
        {
            this.tips = tips;
            this.board = board;
            left = Ui.El("strat-side strat-side--left panel stash-left", view);
            var top = Ui.El("stash-top", left);
            head = Ui.Text("", "strat-head__text font-title", top);
            supplies = Ui.El("stash-supplies", top);
            stash = Ui.El("stash-grid", left);
            hint = Ui.Text("", "stash-hint muted", left);
            bin = Ui.El("stash-bin", left);
            // (gear-anchor: a kit place's list opens to its left, over the scene; U4, cuadro 163.)
            right = new KitPanel(view, "strat-side strat-side--right panel stash-right gear-anchor", tips, handlers, board) { Icon = icon };
            Show(false);
        }

        public void Show(bool on)
        {
            shown = on;
            Ui.Show(left, on);
            Ui.Show(right.Root, on && view?.Kit != null);
        }

        public void Set(StashScreenView v)
        {
            view = v;
            if (v == null)
            {
                Show(shown);
                return;
            }
            head.text = (v.Head ?? "").ToUpperInvariant();
            supplies.Clear();
            if (!string.IsNullOrEmpty(v.Supplies))
            {
                SuppliesMark.Make(supplies, v.Supplies);
                tips.Attach(supplies, v.SuppliesTip);
            }
            if (v.Stash != null) board.SetGrid(stash, v.Stash);
            // (U2: four words, the sentence on hover.)
            hint.text = string.IsNullOrEmpty(v.Hint) ? "" : GearText.HintShort;
            tips.Attach(hint, v.Hint);
            Ui.Show(hint, !string.IsNullOrEmpty(v.Hint));
            board.SetBin(bin);
            right.Set(v.Soldiers, v.Kit);
            Ui.Show(right.Root, shown && v.Kit != null);
        }
    }
}
