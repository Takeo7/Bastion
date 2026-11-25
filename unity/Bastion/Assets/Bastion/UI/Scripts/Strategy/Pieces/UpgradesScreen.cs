using System;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Mejoras, the runs' base (H2; Lore's NOMBRES.md §5): a plate on the left with the supplies in the stash and the
    /// upgrades in their three groups side by side (cuadro 164: Soldados, Expedición, Base), each with what its next level
    /// does, its level in marks, what it costs and «Mejorar» (OnUpgrade: a vote with two), off with why when it can't; the
    /// clearing's fire beside it (Arte).
    /// </summary>
    public sealed class UpgradesScreen
    {
        readonly VisualElement plate;
        readonly TooltipLayer tips;
        readonly IStrategyHandlers handlers;
        string key;

        public UpgradesScreen(VisualElement view, TooltipLayer tips, IStrategyHandlers handlers)
        {
            this.tips = tips;
            this.handlers = handlers;
            plate = Ui.El("strat-side strat-side--left panel upgrades", view);
            Ui.Show(plate, false);
        }

        public void Show(bool on) => Ui.Show(plate, on);

        public void Set(UpgradesView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            plate.Clear();
            if (v == null) return;
            var top = Ui.El("stash-top", plate);
            Ui.Text((v.Head ?? "").ToUpperInvariant(), "strat-head__text font-title", top);
            var supplies = SuppliesMark.Make(top, v.Supplies);
            tips.Attach(supplies, v.SuppliesTip);
            // In its groups' columns (cuadro 164: Soldados, Expedición, Base), each its title and its rows; without groups, one list.
            var cols = Ui.El("upgrades__cols", plate);
            var groups = v.Groups != null && v.Groups.Count > 0 ? v.Groups : new System.Collections.Generic.List<UpgradeGroupView> { new UpgradeGroupView() };
            foreach (var g in groups)
            {
                var rows = v.Rows.FindAll(r => g.Id == null || r.Group == g.Id);
                if (rows.Count == 0) continue;
                var col = Ui.El("upgrades__col", cols);
                if (!string.IsNullOrEmpty(g.Title)) Ui.Text(g.Title, "upgrades__group font-title", col);
                foreach (var r in rows) Row(col, r);
            }
        }

        /// <summary>An upgrade's row (cuadro 163's look): its name and its next level's figures (the sentence on hover), its marks and level, and the key with its price.</summary>
        void Row(VisualElement col, UpgradeRowView r)
        {
            var row = Ui.El("upgrade", col);
            var text = Ui.El("upgrade__text", row);
            var name = Ui.Text(r.Name, "upgrade__name font-title", text);
            // In figures (U3, cuadro 157), the sentence on hover.
            var delta = Ui.Text(r.Delta ?? r.Line, "upgrade__delta", text);
            tips.Attach(name, r.Line);
            tips.Attach(delta, r.Line);
            var marks = Ui.El("upgrade__marks", text);
            for (var i = 0; i < r.Levels; i++) Ui.El(i < r.Level ? "upgrade__mark upgrade__mark--on" : "upgrade__mark", marks);
            // Its level in words beside the marks (Producto's review, 23/11, B12: the marks alone read badly).
            if (!string.IsNullOrEmpty(r.LevelText)) Ui.Text(r.LevelText, "upgrade__level", marks);
            var side = Ui.El("upgrade__side", row);
            var id = r.Id;
            var b = new Button(() => handlers.OnUpgrade(id));
            Ui.AddClasses(b, "upgrade__button font-title");
            // Its price on the key itself (U3): «MEJORAR 150 [caja]», its word and the mark side by side.
            Ui.Text((r.Button ?? "").ToUpperInvariant(), "upgrade__button-text", b).pickingMode = PickingMode.Ignore;
            if (!string.IsNullOrEmpty(r.Price)) SuppliesMark.Make(b, r.Price, "supplies-mark--small upgrade__price").pickingMode = PickingMode.Ignore;
            side.Add(b);
            if (r.Why != null)
            {
                b.SetEnabled(false);
                if (r.Why.Length > 0) tips.Attach(b, r.Why);
            }
            else tips.Attach(b, r.Tip);
        }
    }
}
