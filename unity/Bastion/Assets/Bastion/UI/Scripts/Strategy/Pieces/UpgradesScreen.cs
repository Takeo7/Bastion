using System;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// Mejoras, the runs' base (H2; Lore's NOMBRES.md §5): a plate on the left with the supplies in the stash and the six
    /// upgrades, each with what its next level does, its level in marks, what it costs and «Mejorar» (OnUpgrade: a vote
    /// with two), off with why when it can't; the clearing's fire beside it (Arte).
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
            foreach (var r in v.Rows)
            {
                var row = Ui.El("upgrade", plate);
                var text = Ui.El("upgrade__text", row);
                Ui.Text(r.Name, "upgrade__name font-title", text);
                Ui.Text(r.Line, "upgrade__line muted", text);
                var marks = Ui.El("upgrade__marks", text);
                for (var i = 0; i < r.Levels; i++) Ui.El(i < r.Level ? "upgrade__mark upgrade__mark--on" : "upgrade__mark", marks);
                var side = Ui.El("upgrade__side", row);
                if (!string.IsNullOrEmpty(r.Price)) SuppliesMark.Make(side, r.Price, "supplies-mark--small upgrade__price");
                var id = r.Id;
                var b = new Button(() => handlers.OnUpgrade(id)) { text = (r.Button ?? "").ToUpperInvariant() };
                Ui.AddClasses(b, "upgrade__button font-title");
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
}
