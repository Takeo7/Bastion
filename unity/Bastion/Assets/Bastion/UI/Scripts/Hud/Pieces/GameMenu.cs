using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The game's menu (Esc; hud.ts toggleMenu, renderMenu) and the questions asked before going (confirmLeave,
    /// confirmAbandon, confirmQuit). Over the battle, dimmed, a plate with one entry a line: Continuar, Ajustes, Volver al
    /// menú de inicio, Abandonar la misión (in red), Salir de Bastión. Ajustes has its rows and Controles, the keys and the
    /// night's rules. It decides nothing: what each entry does and what each question says come from the presenter.
    /// </summary>
    public sealed class GameMenu
    {
        public const string Leave = "leave";
        public const string Abandon = "abandon";
        public const string Quit = "quit";

        readonly VisualElement layer;
        readonly VisualElement modal;
        readonly TooltipLayer tips;
        readonly Action<string> sound;
        GameMenuView view;
        Action yes;

        /// <summary>An entry chosen (Leave, Abandon, Quit); the menu has closed.</summary>
        public event Action<string> Chose;

        /// <summary>A setting changed: its id and its value (a choice's index, or the slider's).</summary>
        public event Action<string, float> Changed;

        public GameMenu(VisualElement host, TooltipLayer tips, Action<string> sound)
        {
            this.tips = tips;
            this.sound = sound;
            layer = Ui.El("game-menu", host);
            modal = Ui.El("game-menu game-menu--modal", host);
            Ui.Show(layer, false);
            Ui.Show(modal, false);
        }

        public bool Open { get; private set; }
        public bool Asking { get; private set; }

        /// <summary>Opened on its first page, or closed.</summary>
        public void Toggle(GameMenuView v)
        {
            if (Open) Close();
            else Show(v);
        }

        public void Show(GameMenuView v)
        {
            view = v ?? new GameMenuView();
            if (!Open) sound?.Invoke("paper");
            Open = true;
            Ui.Show(layer, true);
            Page("main");
        }

        public void Close()
        {
            if (!Open) return;
            sound?.Invoke("paper");
            Open = false;
            layer.Clear();
            Ui.Show(layer, false);
        }

        /// <summary>Esc: the question first, then the menu. Whether there was one to close.</summary>
        public bool Back()
        {
            if (Asking)
            {
                CloseConfirm();
                return true;
            }
            if (!Open) return false;
            Close();
            return true;
        }

        /// <summary>A page of it, open (main, settings, controls): for the labs' captures.</summary>
        public void Go(string page)
        {
            if (Open) Page(page);
        }

        void Page(string page)
        {
            layer.Clear();
            var card = Ui.El("panel game-menu__card", layer);
            if (page == "main")
            {
                // The same list as the base's (playtest 19/10 #7), one entry a line, all alike; the controls are in Ajustes.
                Ui.Text(HudText.Menu.Title, "game-menu__title font-title", card);
                Entry(card, HudText.Menu.Continue, Close);
                Entry(card, HudText.Menu.Settings, () => Page("settings"));
                if (view.Leave) Entry(card, HudText.Menu.Leave, () => Choose(Leave));
                if (view.Abandon)
                {
                    var waiting = !string.IsNullOrEmpty(view.AbandonWaiting);
                    var entry = Entry(card, waiting ? view.AbandonWaiting : view.AbandonLabel, () => Choose(Abandon), "game-menu__entry--danger");
                    entry.SetEnabled(!waiting);
                    tips?.Attach(entry, waiting ? HudText.Menu.AbandonWaitingTip : view.AbandonTip);
                }
                if (view.Quit) Entry(card, view.QuitLabel, () => Choose(Quit));
            }
            else if (page == "settings")
            {
                Ui.Text(HudText.Menu.Settings.ToUpperInvariant(), "game-menu__title font-title", card);
                var rows = Ui.El("settings", card);
                foreach (var s in view.Settings) Setting(rows, s);
                Entry(card, HudText.Menu.Controls, () => Page("controls"));
                BackTo(card, "main");
            }
            else
            {
                // Wider, so the keys and the night's rules fit whole without a scroll.
                card.AddToClassList("game-menu__card--wide");
                Ui.Text(HudText.Menu.Controls.ToUpperInvariant(), "game-menu__title font-title", card);
                var list = Ui.El("controls", card);
                foreach (var c in view.Controls)
                {
                    var row = Ui.El("controls__row", list);
                    Ui.Text(c.Keys, "controls__keys", row);
                    Ui.Text(c.What, "controls__what", row);
                }
                // How the light works at night (Lore's, #50), here too for whenever it is wanted.
                if (!string.IsNullOrEmpty(view.NightLabel))
                {
                    Ui.Text(view.NightLabel, "controls__night font-title", list);
                    foreach (var line in view.NightRules) Ui.Text("· " + line, "controls__rule", list);
                }
                BackTo(card, "settings");
            }
        }

        Button Entry(VisualElement card, string text, Action click, string extra = null)
        {
            // In capitals, as mixta-hud.css's text-transform (USS has none).
            var b = new Button(click) { text = text.ToUpperInvariant() };
            b.AddToClassList("game-menu__entry");
            b.AddToClassList("font-title");
            Ui.AddClasses(b, extra);
            card.Add(b);
            return b;
        }

        void BackTo(VisualElement card, string page)
        {
            var b = new Button(() => Page(page)) { text = HudText.Menu.Back };
            b.AddToClassList("game-menu__back");
            card.Add(b);
        }

        void Choose(string what)
        {
            Close();
            Chose?.Invoke(what);
        }

        /// <summary>A row: its name over its choices (the one on marked here, at once) or over its slider and value.</summary>
        void Setting(VisualElement rows, SettingView s)
        {
            var row = Ui.El("setting", rows);
            Ui.Text(s.Label.ToUpperInvariant(), "setting__name", row);
            tips?.Attach(row, s.About);
            var choices = Ui.El("setting__choices", row);
            if (s.Slider)
            {
                Ui.Text(s.Low, "setting__end", choices);
                var slider = new Slider(s.Min, s.Max) { value = s.Value };
                slider.AddToClassList("setting__slider");
                choices.Add(slider);
                Ui.Text(s.High, "setting__end", choices);
                var value = Ui.Text(Rate(s.Value), "setting__value", choices);
                slider.RegisterValueChangedCallback(e =>
                {
                    var k = s.Step > 0 ? Mathf.Round(e.newValue / s.Step) * s.Step : e.newValue;
                    value.text = Rate(k);
                    s.Value = k;
                    Changed?.Invoke(s.Id, k);
                });
                return;
            }
            var buttons = new List<Button>();
            for (var i = 0; i < s.Choices.Count; i++)
            {
                var n = i;
                var b = new Button { text = s.Choices[i] };
                b.AddToClassList("setting__choice");
                b.EnableInClassList("setting__choice--on", i == s.On);
                b.clicked += () =>
                {
                    if (s.On == n) return;
                    s.On = n;
                    for (var j = 0; j < buttons.Count; j++) buttons[j].EnableInClassList("setting__choice--on", j == n);
                    Changed?.Invoke(s.Id, n);
                };
                buttons.Add(b);
                choices.Add(b);
            }
        }

        /// <summary>«×1,4»: a speed's rate, the Spanish way.</summary>
        static string Rate(float k) => "×" + k.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

        // ------------------------------------------------------- the question

        /// <summary>A question over everything; `onYes` if it is answered yes. Esc, or «No», closes it.</summary>
        public void Confirm(ConfirmView v, Action onYes)
        {
            modal.Clear();
            yes = onYes;
            Asking = true;
            Ui.Show(modal, true);
            sound?.Invoke("paper");
            var card = Ui.El("panel game-menu__card game-menu__card--ask", modal);
            Ui.Text(v.Title, "game-menu__ask-title font-title", card);
            foreach (var line in v.Lines) Ui.Text(line, "game-menu__line", card);
            if (!string.IsNullOrEmpty(v.Warning)) Ui.Text(v.Warning, "game-menu__line game-menu__warning", card);
            var actions = Ui.El("card__actions game-menu__actions", card);
            var accept = new Button(() =>
            {
                var then = yes;
                CloseConfirm();
                then?.Invoke();
            }) { text = (v.Yes ?? "").ToUpperInvariant() };
            var refuse = new Button(CloseConfirm) { text = (v.No ?? "").ToUpperInvariant() };
            // The dangerous answer in red, the safe one the one to press; otherwise, yes is.
            accept.AddToClassList(v.Danger ? "btn-danger" : "btn-primary");
            if (v.Danger) refuse.AddToClassList("btn-primary");
            actions.Add(accept);
            actions.Add(refuse);
        }

        public void CloseConfirm()
        {
            if (!Asking) return;
            Asking = false;
            yes = null;
            modal.Clear();
            Ui.Show(modal, false);
        }
    }
}
