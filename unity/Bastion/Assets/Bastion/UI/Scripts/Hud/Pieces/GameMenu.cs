using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>The partner's menu open: the battle dimmed under a card saying so (SetPause).</summary>
        readonly VisualElement pause;
        string paused;
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
            // (Under the menu: whoever opens theirs too sees their own.)
            pause = Ui.El("game-menu game-menu--pause", host);
            pause.pickingMode = PickingMode.Ignore;
            Ui.Show(pause, false);
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
            layer.EnableInClassList("game-menu--cover", view.Cover);
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

        /// <summary>
        /// The partner's menu open in the mission (Arturo, 28/10): the battle dimmed under a card with `line` and `sub`, the
        /// pointer going through (the partner's calls to evacuate or abandon are still answered on the bar). Null, gone.
        /// </summary>
        public void SetPause(string line, string sub)
        {
            var key = line == null ? null : line + "|" + sub;
            if (key == paused) return;
            paused = key;
            pause.Clear();
            Ui.Show(pause, line != null);
            if (line == null) return;
            var card = Ui.El("panel game-menu__card game-menu__card--pause", pause);
            PlateFrame.Dress(card);
            Ui.Text(line, "game-menu__paused font-title", card);
            if (!string.IsNullOrEmpty(sub)) Ui.Text(sub, "game-menu__note", card);
            card.Query<VisualElement>().ForEach(x => x.pickingMode = PickingMode.Ignore);
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
            notes.Clear();
            var card = Ui.El("panel game-menu__card", layer);
            // mixta.css .game-menu-card: a big plate in the steel frame with rivets (Arturo, 24/10: as the 0.10.1).
            PlateFrame.Dress(card);
            if (page == "main")
            {
                // The same list as the base's (playtest 19/10 #7), one entry a line, all alike; the controls are in Ajustes.
                Ui.Text(HudText.Menu.Title, "game-menu__title font-title", card);
                if (!string.IsNullOrEmpty(view.PauseNote)) Ui.Text(view.PauseNote, "game-menu__note", card);
                Entry(card, HudText.Menu.Continue, Close);
                Entry(card, HudText.Menu.Settings, () => Page("settings"));
                if (view.Leave)
                {
                    var leave = Entry(card, HudText.Menu.Leave, () => Choose(Leave));
                    if (!string.IsNullOrEmpty(view.LeaveTip)) tips?.Attach(leave, view.LeaveTip);
                }
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
                if (!string.IsNullOrEmpty(view.SettingsNote)) Ui.Text(view.SettingsNote, "game-menu__note", card);
                // Two columns (Lore, 26/10: one ran up over the turn strip): the sound's rows, then the rest; one without sound.
                var rows = Scroll(card, "settings");
                var sound = view.Settings.Where(s => s.Sound).ToList();
                if (sound.Count > 0)
                {
                    card.AddToClassList("game-menu__card--settings");
                    rows.AddToClassList("settings--cols");
                    var first = Ui.El("settings__col", rows);
                    var second = Ui.El("settings__col settings__col--second", rows);
                    // The last rows, one by one, under the sound's while the first column is the shorter (the battle's second,
                    // with its speeds and plans, ran past the card at 130 % in 720).
                    var rest = view.Settings.Where(s => !s.Sound).ToList();
                    foreach (var s in rest.Where(s => s.Tail).ToList())
                    {
                        if (sound.Count >= rest.Count) break;
                        sound.Add(s);
                        rest.Remove(s);
                    }
                    foreach (var s in sound) Setting(first, s);
                    foreach (var s in rest) Setting(second, s);
                }
                else
                    foreach (var s in view.Settings) Setting(rows, s);
                Entry(card, HudText.Menu.Controls, () => Page("controls"));
                BackTo(card, "main");
            }
            else
            {
                // Wider, so the keys and the night's rules fit whole without a scroll.
                card.AddToClassList("game-menu__card--wide");
                Ui.Text(HudText.Menu.Controls.ToUpperInvariant(), "game-menu__title font-title", card);
                var list = Scroll(card, "controls");
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

        /// <summary>
        /// A list that scrolls when the card would be taller than the screen (Ajustes with the sound, a big «Interfaz»):
        /// its scrollbar slim, over the card's padding, only when there is more below. The rows go in what it returns.
        /// </summary>
        static VisualElement Scroll(VisualElement card, string classes)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Auto, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("game-menu__scroll");
            Ui.AddClasses(scroll.contentContainer, classes);
            card.Add(scroll);
            return scroll.contentContainer;
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

        /// <summary>The notes under the rows of the page open, and the rows whose change takes each away (SettingView.NoteGoesWith).</summary>
        readonly List<(VisualElement note, List<string> ids)> notes = new List<(VisualElement, List<string>)>();

        /// <summary>A row changed: the notes it makes stale go (Ajuste auto's, as it gives back or the level is chosen), and it is said.</summary>
        void Change(string id, float value)
        {
            notes.RemoveAll(n =>
            {
                if (n.ids == null || !n.ids.Contains(id)) return false;
                n.note.RemoveFromHierarchy();
                return true;
            });
            Changed?.Invoke(id, value);
        }

        /// <summary>A row: its name over its choices (the one on marked here, at once) or over its slider and value; its note under them.</summary>
        void Setting(VisualElement rows, SettingView s)
        {
            var row = Ui.El("setting", rows);
            Ui.Text(s.Label.ToUpperInvariant(), "setting__name", row);
            tips?.Attach(row, s.About);
            var choices = Ui.El("setting__choices", row);
            if (!string.IsNullOrEmpty(s.Note)) notes.Add((Ui.Text(s.Note, "setting__note", row), s.NoteGoesWith));
            if (s.Slider)
            {
                // A volume's has no words at its ends (settingsPanel.ts volumeRow): only its share beside it.
                if (!string.IsNullOrEmpty(s.Low)) Ui.Text(s.Low, "setting__end", choices);
                var slider = new Slider(s.Min, s.Max) { value = s.Value };
                slider.AddToClassList("setting__slider");
                choices.Add(slider);
                if (!string.IsNullOrEmpty(s.High)) Ui.Text(s.High, "setting__end", choices);
                string Shown(float k) => s.Percent ? $"{Mathf.RoundToInt(k)} %" : Rate(k);
                var value = Ui.Text(Shown(s.Value), "setting__value", choices);
                slider.RegisterValueChangedCallback(e =>
                {
                    var k = s.Step > 0 ? Mathf.Round(e.newValue / s.Step) * s.Step : e.newValue;
                    value.text = Shown(k);
                    s.Value = k;
                    Change(s.Id, k);
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
                    Change(s.Id, n);
                };
                buttons.Add(b);
                choices.Add(b);
                if (s.ChoiceTips != null && i < s.ChoiceTips.Count) tips?.Attach(b, s.ChoiceTips[i]);
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
            PlateFrame.Dress(card);
            Slant.Dress(v.Danger ? refuse : accept);
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
