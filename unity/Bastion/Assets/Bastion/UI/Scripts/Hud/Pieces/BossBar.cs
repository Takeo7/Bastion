using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// A chapter's boss (H6, El Toro), at the top in the middle once seen: its name, its health in a long bar with the
    /// figure, its armour, and whether its charge is ready or in how many turns; its abilities on hover, and its file
    /// (Lore.EnemyFiles) on a click.
    /// </summary>
    public sealed class BossBar
    {
        readonly VisualElement host;
        readonly TooltipLayer tips;
        string key;
        string id;

        public BossBar(VisualElement root, TooltipLayer tips, IHudHandlers handlers)
        {
            this.tips = tips;
            host = Ui.El("boss-bar", root);
            host.pickingMode = PickingMode.Position;
            host.RegisterCallback<ClickEvent>(_ =>
            {
                if (id != null) handlers.OnSheet(id);
            });
            Ui.Show(host, false);
        }

        public void Set(BossView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            id = v?.Id;
            host.Clear();
            Ui.Show(host, v != null);
            if (v == null) return;
            var top = Ui.El("boss-bar__top", host);
            Ui.Text(v.Name.ToUpperInvariant(), "boss-bar__name font-title", top);
            Ui.Text($"{v.Hp}/{v.Max}", "boss-bar__hp font-title", top);
            var bar = Ui.El("boss-bar__track", host);
            var fill = Ui.El("boss-bar__fill", bar);
            fill.style.width = Length.Percent(100f * Mathf.Clamp01(v.Hp / (float)Mathf.Max(1, v.Max)));
            var line = Ui.El("boss-bar__line", host);
            if (v.Armor > 0) Ui.Text(HudText.Boss.Armor(v.Armor), "boss-bar__chip", line);
            if (!string.IsNullOrEmpty(v.Charge)) Ui.Text(v.Charge, "boss-bar__chip" + (v.ChargeReady ? " boss-bar__chip--ready" : ""), line);
            tips.Attach(host, v.Tip);
        }
    }
}
