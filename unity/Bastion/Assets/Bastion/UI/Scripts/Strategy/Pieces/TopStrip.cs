using System;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The steel band across the top of the base (screen.ts renderTop, mixta-base.css .strat-top): the Bastión's plate
    /// with the outings, the siege's badge, the day and the stores each in its well (with what a proposal would spend),
    /// the level with its bar, the Marea's marks, «Menú». Every part has its tooltip, its figures picked out.
    /// </summary>
    public sealed class TopStrip
    {
        readonly VisualElement host;
        readonly TooltipLayer tips;
        readonly Func<string, Sprite> icon;
        readonly IStrategyHandlers handlers;
        string key;

        public TopStrip(VisualElement host, TooltipLayer tips, Func<string, Sprite> icon, IStrategyHandlers handlers)
        {
            this.host = host;
            this.tips = tips;
            this.icon = icon;
            this.handlers = handlers;
        }

        public void Set(TopView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            host.Clear();
            if (v == null) return;
            var brand = Ui.El("strat-brand", host);
            Ui.Text(StrategyText.Brand, "strat-brand__name font-title", brand);
            if (v.Outings > 0)
            {
                var outings = Ui.El("strat-brand__outings", brand);
                Ui.Icon(icon, "base:day", "strat-brand__icon", outings);
                Ui.Text($"{v.Outings} SALIDAS", "font-title", outings);
                tips.Attach(brand, v.OutingsTip);
            }
            if (!string.IsNullOrEmpty(v.Siege))
            {
                var siege = Ui.Text(v.Siege, "strat-siege font-title", host);
                siege.EnableInClassList("strat-siege--now", v.SiegeNow);
                siege.EnableInClassList("strat-siege--near", v.SiegeNear);
                tips.Attach(siege, v.SiegeTip);
            }
            var stores = Ui.El("strat-res", host);
            var day = Well(stores, "base:day", "strat-well--day", $"DÍA {v.Day}");
            tips.Attach(day, v.DayTip);
            foreach (var s in v.Stores)
            {
                var well = Well(stores, "store:" + s.Id, "strat-well--" + s.Id, s.Value);
                well.EnableInClassList("strat-well--bad", s.Bad);
                if (!string.IsNullOrEmpty(s.Owed))
                {
                    var owed = Ui.Text(s.Owed, "strat-owed font-title", well);
                    tips.Attach(owed, s.OwedTip);
                }
                var store = s;
                tips.Attach(well, () => Tip(store.TipName, store.Tip));
            }
            if (!string.IsNullOrEmpty(v.Level))
            {
                var level = Well(stores, "base:level", "strat-well--level", v.Level);
                var bar = Ui.El("strat-level-bar", level);
                for (var i = 0; i < v.LevelTotal; i++) Ui.El(i < v.LevelFilled ? "strat-level-bar__mark strat-level-bar__mark--on" : "strat-level-bar__mark", bar);
                tips.Attach(level, v.LevelTip);
            }
            var doom = Ui.El("strat-doom", host);
            Ui.Icon(icon, "base:doom", "strat-doom__icon", doom);
            var marks = Ui.El("strat-doom__marks", doom);
            for (var i = 0; i < v.DoomMax; i++) Ui.El(i < v.DoomFilled ? "strat-doom__mark strat-doom__mark--on" : "strat-doom__mark", marks);
            Ui.Text(v.DoomFigure, "strat-doom__figure font-title", doom);
            tips.Attach(doom, v.DoomTip);
            var menu = new Button(handlers.OnMenu);
            menu.AddToClassList("strat-menu");
            Ui.Icon(icon, "base:menu", "strat-menu__icon", menu);
            Ui.Text(StrategyText.Menu.ToUpperInvariant(), "font-title", menu);
            host.Add(menu);
            tips.Attach(menu, StrategyText.MenuTip);
        }

        /// <summary>A store's well: its icon, its figure.</summary>
        VisualElement Well(VisualElement parent, string iconId, string cls, string value)
        {
            var well = Ui.El("strat-well " + cls, parent);
            Ui.Icon(icon, iconId, "strat-well__icon", well);
            Ui.Text(value, "strat-well__value font-title", well);
            return well;
        }

        /// <summary>A rich tooltip: the name, then each sentence with its figures picked out (withTip, sentences, figures).</summary>
        static VisualElement Tip(string name, System.Collections.Generic.List<string> sentences)
        {
            var body = Ui.El("strat-tip");
            if (!string.IsNullOrEmpty(name)) Ui.Text(name, "strat-tip__name font-title", body);
            foreach (var s in sentences) Ui.Text(StrategyText.Figures(s), "strat-tip__line", body);
            return body;
        }
    }
}
