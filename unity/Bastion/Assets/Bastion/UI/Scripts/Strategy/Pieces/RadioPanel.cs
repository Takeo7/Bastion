using System;
using System.Linq;
using System.Text.RegularExpressions;
using Bastion.UI.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// The radio's card (strategy/transmissions.ts RadioPanel): docked over the base, never blocking it. It types each
    /// line a letter every 26 ms, then waits for the reader: a click on the card (which first finishes the line being
    /// typed), Intro or Espacio go on, «Seguir» always goes on, ✕ or Esc close it. Its sounds go to `sound` (RadioSound):
    ///   radio.open    the channel opens (static; the music held down while it talks);
    ///   radio.static  the voice changes, a line on;
    ///   radio.key     a key, every third letter;
    ///   radio.morse   Morse's taps as she comes on the air, on her first line;
    ///   radio.bells   distant bells under the Coro;
    ///   radio.close   the channel closes with its click; radio.end, without it (a mission took the card away).
    /// Where it sits goes with the screen (StrategyRoot: radio--geoscape, --squad, --debrief, --barracks).
    /// </summary>
    public sealed class RadioPanel
    {
        /// <summary>A letter every 26 ms (transmissions.ts type).</summary>
        const float Letter = 0.026f;

        readonly VisualElement card;
        readonly TooltipLayer tips;
        readonly Action<string> sound;
        RadioView view;
        int index;
        int shown;
        string text = "";
        float clock;
        float waiting;
        bool tapped;
        Label textEl;
        VisualElement next;

        /// <summary>A voice's bust by speaker (Arte's Portraits.Voice); null, their initials on a card.</summary>
        public Func<string, Texture> VoiceOf;

        /// <summary>The scene played to its end or closed (its id): the controller plays the next when its moment comes.</summary>
        public event Action<string> Ended;

        public RadioPanel(VisualElement card, TooltipLayer tips, Action<string> sound)
        {
            this.card = card;
            this.tips = tips;
            this.sound = sound;
            Ui.Show(card, false);
            // A click anywhere on it goes on (its two buttons answer for themselves).
            card.RegisterCallback<ClickEvent>(e =>
            {
                for (var t = e.target as VisualElement; t != null && t != card; t = t.parent)
                    if (t.ClassListContains("radio__close") || t.ClassListContains("radio__next")) return;
                Advance();
            });
        }

        public bool Active => view != null;

        /// <summary>The scene now, its first line (another one playing stops without a sound).</summary>
        public void Play(RadioView v)
        {
            if (v == null || v.Lines.Count == 0) return;
            view = v;
            index = 0;
            tapped = false;
            Ui.Show(card, true);
            sound?.Invoke("radio.open");
            ShowLine();
        }

        /// <summary>`click`: its closing is heard (not when a mission ending takes it away).</summary>
        public void Close(bool click = true)
        {
            if (view == null) return;
            var id = view.Scene;
            sound?.Invoke(click ? "radio.close" : "radio.end");
            view = null;
            card.Clear();
            Ui.Show(card, false);
            Ended?.Invoke(id);
        }

        /// <summary>Esc closes it, Intro and Espacio go on; true if the key was its. In battle the card takes clicks only.</summary>
        public bool Key(string key)
        {
            if (view == null) return false;
            if (key == "escape") Close();
            else if (key == "enter" || key == "space") Advance();
            else return false;
            return true;
        }

        /// <summary>The line being typed, finished at once (a click on the card; the lab, for its captures).</summary>
        public void Finish()
        {
            if (view == null || shown >= text.Length) return;
            Typed();
        }

        /// <summary>Each frame: the next letters, and «Seguir» nudging while it waits.</summary>
        public void Tick(float dt)
        {
            if (view == null) return;
            if (shown < text.Length)
            {
                clock += dt;
                while (clock >= Letter && shown < text.Length)
                {
                    clock -= Letter;
                    shown++;
                    if (shown % 3 == 0 && text[shown - 1] != ' ') sound?.Invoke("radio.key");
                }
                textEl.text = text.Substring(0, shown);
                if (shown >= text.Length) Typed();
                return;
            }
            // radio-wait: 3 px to the right and back, every 1.4 s.
            waiting += dt;
            if (next != null) next.style.translate = new Translate(1.5f - 1.5f * Mathf.Cos(waiting * 2 * Mathf.PI / 1.4f), 0);
        }

        void Advance()
        {
            if (view == null) return;
            if (shown < text.Length) Typed();
            else Next();
        }

        void Next()
        {
            if (++index >= view.Lines.Count) Close();
            else ShowLine();
        }

        void Typed()
        {
            shown = text.Length;
            textEl.text = text;
            card.AddToClassList("radio--ready");
            waiting = 0;
        }

        void ShowLine()
        {
            var line = view.Lines[index];
            var many = view.Lines.Count > 1;
            text = line.Text ?? "";
            shown = 0;
            clock = 0;
            card.Clear();
            // mixta.css: the radio is one of the big plates, riveted, without the frame.
            PlateFrame.Dress(card, false);
            card.RemoveFromClassList("radio--ready");
            Portrait(line, card);
            var body = Ui.El("radio__body", card);
            var head = Ui.El("radio__head", body);
            Ui.Text($"{StrategyText.Radio.Kicker} · {view.Channel}".ToUpperInvariant(), "radio__channel font-title", head);
            var close = new Button(() => Close()) { text = "✕" };
            close.AddToClassList("radio__close");
            tips.Attach(close, StrategyText.Radio.CloseTip);
            head.Add(close);
            var who = Ui.El("radio__who", body);
            Ui.Text(line.Name, "radio__name font-title", who);
            if (!string.IsNullOrEmpty(line.Role)) Ui.Text(line.Role, "radio__role", who);
            textEl = Ui.Text("", "radio__text", body);
            textEl.EnableInClassList("radio__text--coro", line.Who == "coro");
            if (!string.IsNullOrEmpty(line.Rule)) Ui.Text(line.Rule, "radio__rule", body);
            var foot = Ui.El("radio__foot", body);
            Ui.Text(many ? $"{view.Title} · {index + 1}/{view.Lines.Count}" : view.Title, "radio__count", foot);
            var go = new Button(Next);
            go.AddToClassList("radio__next");
            Ui.Text(index + 1 < view.Lines.Count ? StrategyText.Radio.Next : many ? StrategyText.Radio.End : StrategyText.Radio.Ok, "radio__next-label font-title", go);
            go.Add(new Triangle(false, true) { name = "arrow" });
            Ui.Text(StrategyText.Radio.Hint, "radio__hint", go);
            foot.Add(go);
            next = go;
            if (index > 0) sound?.Invoke("radio.static");
            if (line.Who == "coro") sound?.Invoke("radio.bells");
            else if (line.Who == "morse" && !tapped)
            {
                tapped = true;
                sound?.Invoke("radio.morse");
            }
        }

        /// <summary>The voice's bust in its sunken frame (Arte's), or their initials on a card; the Coro, a signal.</summary>
        void Portrait(RadioLineView line, VisualElement parent)
        {
            var frame = Ui.El("radio__portrait", parent);
            frame.EnableInClassList("radio__portrait--coro", line.Who == "coro");
            var bust = VoiceOf?.Invoke(line.Who);
            if (bust is RenderTexture rt) frame.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else if (bust is Texture2D t2) frame.style.backgroundImage = new StyleBackground(t2);
            else if (line.Who == "coro") Ui.El("radio__wave", frame).Add(new LineIcon("wave"));
            else Ui.Text(Initials(line), "radio__initials font-title", frame);
            Ui.El("radio__scan", frame).pickingMode = PickingMode.Ignore;
        }

        /// <summary>transmissions.ts portrait's stand-in: the first two capitalised words' initials, no «nickname», no «Dra.».</summary>
        static string Initials(RadioLineView line)
        {
            var name = Regex.Replace(line.Name ?? "", "«.*»|,", "");
            return string.Concat(name.Split(' ').Where(w => w.Length > 0 && char.IsUpper(w[0]) && w != "Dra.").Take(2).Select(w => w[0]));
        }
    }
}
