using System;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The base's tabs at the bottom (screen.ts renderNav, mixta-base.css .strat-nav): Mapa, Armería, Soldados,
    /// Asentamiento and, with a mission chosen, Escuadra in red; each with its news not looked at yet, «Nuevo» for what
    /// a level brought, and the partner's initial where the partner is.
    /// </summary>
    public sealed class NavTabs
    {
        readonly VisualElement host;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        string key;

        public NavTabs(VisualElement host, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.host = host;
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
        }

        public void Set(NavView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            host.Clear();
            Ui.Show(host, v != null && !v.Hidden);
            if (v == null) return;
            foreach (var t in v.Tabs)
            {
                var id = t.Id;
                var tab = new Button(() => handlers.OnTab(id));
                tab.AddToClassList("strat-tab");
                tab.EnableInClassList("strat-tab--on", t.On);
                tab.EnableInClassList("strat-tab--urgent", t.Urgent && !t.On);
                Ui.Icon(icon, "tab:" + t.Id, "strat-tab__icon", tab);
                Ui.Text(t.Label.ToUpperInvariant(), "strat-tab__label font-title", tab);
                if (!string.IsNullOrEmpty(t.Count))
                {
                    var count = Ui.Text(t.Count, "strat-tab__count font-title", tab);
                    count.EnableInClassList("strat-tab__count--urgent", t.CountUrgent);
                }
                else if (t.New)
                {
                    var fresh = Ui.Text(StrategyText.Tabs.NewTag, "strat-tab__new font-title", tab);
                    tips.Attach(fresh, StrategyText.Tabs.NewTip);
                }
                if (!string.IsNullOrEmpty(t.Partner))
                {
                    var dot = Ui.Text(t.Partner.Substring(0, 1).ToUpperInvariant(), "strat-tab__partner font-title", tab);
                    dot.style.backgroundColor = Ui.Hex(t.PartnerColor, new Color(0.65f, 0.47f, 1f));
                    tips.Attach(dot, StrategyText.Tabs.PartnerHere(t.Partner));
                }
                if (!string.IsNullOrEmpty(t.Tip)) tips.Attach(tab, t.Tip);
                host.Add(tab);
            }
        }
    }
}
