using System;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The window over the base (screen.ts renderModal, mixta-base.css .strat-modal), one at a time: an event with its two
    /// options (and the vote when one is proposed), the diary or a siege's warning (with what a level brought), the end of
    /// the campaign, the questions before leaving. A sheet of paper on a dark veil; it rustles when it opens.
    /// </summary>
    public sealed class StrategyModal
    {
        readonly VisualElement host;
        readonly IStrategyHandlers handlers;
        readonly Func<string, Sprite> icon;
        readonly Action<string> sound;
        ProposalPlate vote;
        string key;

        public StrategyModal(VisualElement host, IStrategyHandlers handlers, Func<string, Sprite> icon, Action<string> sound, TooltipLayer tips = null)
        {
            this.host = host;
            this.handlers = handlers;
            this.icon = icon;
            this.sound = sound;
            this.tips = tips;
        }

        /// <summary>The helps on hover (what a level brought, in full); none, none.</summary>
        readonly TooltipLayer tips;

        public bool Open => host.resolvedStyle.display != DisplayStyle.None && host.childCount > 0;

        public void Set(StrategyModalView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            var opening = key == null || key == Ui.Key(null);
            key = k;
            host.Clear();
            vote = null;
            Ui.Show(host, v != null);
            host.EnableInClassList("strat-modal--high", v?.High == true);
            if (v == null) return;
            if (opening) sound?.Invoke("paper");
            // The level's (N8), on the start screen's plate rather than the torn panel: the same piece as there.
            var card = Ui.El((v.Level != null ? "strat-modal__card level-plate" : "strat-modal__card panel") + " strat-modal__card--" + v.Kind, host);
            // (The plate's style is the picker's sheet's: on the card, not only under the picker.)
            if (v.Level != null && Resources.Load<StyleSheet>("Hud/LevelPicker") is StyleSheet levelSheet) card.styleSheets.Add(levelSheet);
            // Opaque over the base's screens behind it (Soldados' slots showed through the cards): set on the card itself.
            if (v.Level != null)
            {
                card.style.backgroundImage = StyleKeyword.None;
                card.style.backgroundColor = new Color(0.04f, 0.045f, 0.05f, 1f);
            }
            if (!string.IsNullOrEmpty(v.Kicker))
            {
                var kicker = Ui.Text(v.Kicker.ToUpperInvariant(), "strat-modal__kicker font-title", card);
                if (!string.IsNullOrEmpty(v.Tone)) kicker.AddToClassList("strat-modal__kicker--" + v.Tone);
            }
            if (!string.IsNullOrEmpty(v.Title)) Ui.Text(v.Title.ToUpperInvariant(), "strat-modal__title font-title", card);
            foreach (var p in v.Body) Ui.Text(StrategyText.Marked(p), "strat-modal__body", card);
            if (!string.IsNullOrEmpty(v.Text)) Ui.Text(StrategyText.Marked(v.Text), "strat-modal__text", card);
            foreach (var g in v.Brought)
            {
                Ui.Text(g.Title.ToUpperInvariant(), "strat-brought__head font-title", card);
                var list = Ui.El("strat-brought", card);
                foreach (var item in g.Items)
                {
                    var row = Ui.El("strat-brought__item", list);
                    Ui.Icon(icon, item.Icon, "strat-brought__icon", row);
                    var words = Ui.El("strat-brought__words", row);
                    Ui.Text(item.Name, "strat-brought__name font-title", words);
                    if (!string.IsNullOrEmpty(item.Brief)) Ui.Text(item.Brief, "strat-brought__brief", words);
                    if (tips != null && !string.IsNullOrEmpty(item.Tip)) tips.Attach(row, item.Tip);
                }
            }
            // The level's cards (LevelPicker): a click picks one, as an option of the window.
            if (v.Level != null)
            {
                var picker = new LevelPicker(v.Level);
                picker.Picked += id => handlers.OnOption(System.Linq.Enumerable.ToList(Bastion.Engine.LevelIds.All).IndexOf(id));
                card.Add(picker.Root);
            }
            // In a column of their own.
            var options = v.Options.Count > 0 ? Ui.El("strat-modal__options", card) : null;
            for (var i = 0; i < v.Options.Count; i++)
            {
                var o = v.Options[i];
                var index = i;
                var option = new Button(() => handlers.OnOption(index));
                option.AddToClassList("strat-option");
                option.EnableInClassList("strat-option--proposed", o.Proposed);
                option.SetEnabled(!o.Locked && string.IsNullOrEmpty(o.Blocked));
                Ui.Text(o.Label, "strat-option__label font-title", option);
                Ui.Text(o.Effect, "strat-option__effect", option);
                if (!string.IsNullOrEmpty(o.Blocked)) Ui.Text(o.Blocked, "strat-option__blocked", option);
                options.Add(option);
            }
            if (v.Vote != null)
            {
                var plate = Ui.El("strat-modal__vote", card);
                vote = new ProposalPlate(plate, handlers);
                vote.Set(v.Vote, null);
            }
            else if (!string.IsNullOrEmpty(v.Foot)) Ui.Text(v.Foot, "strat-modal__foot", card);
            if (!string.IsNullOrEmpty(v.Note)) Ui.Text(v.Note, "strat-modal__note", card);
            if (v.Buttons.Count > 0)
            {
                var actions = Ui.El("strat-modal__actions", card);
                for (var i = 0; i < v.Buttons.Count; i++)
                {
                    var index = i;
                    var b = new Button(() => handlers.OnModalButton(index)) { text = v.Buttons[i].ToUpperInvariant() };
                    b.AddToClassList("font-title");
                    if (i == 0) b.AddToClassList("btn-primary");
                    actions.Add(b);
                }
            }
        }

        /// <summary>The vote's clock, when the window carries one.</summary>
        public void Tick(string text, float left, bool hurry) => vote?.Tick(text, left, hurry);
    }
}
