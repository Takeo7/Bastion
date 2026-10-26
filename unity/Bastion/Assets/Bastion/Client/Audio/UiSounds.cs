using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.Client.Audio
{
    /// <summary>
    /// The interface's sounds outside the combat's events (0.10.1's ui.ts, hud.ts, transmissions.ts, stage.ts): a click
    /// or a key on any screen, the sounds the screens ask for by name (HudRoot.Sound, StrategyRoot.Sound: «stamp»,
    /// «paper», «radio.open»…), and the music and ambience of the start screen and the base. Main thread.
    /// </summary>
    public static class UiSounds
    {
        // ------------------------------------------------------------ clicks and keys (ui.ts installUiSounds)

        /// <summary>
        /// Clickable without being a Button (0.10.1's [role="button"], .edge-mark…): the soldier's card, the unit's head
        /// that opens its sheet, the objective's dock, a zone of the valley, the radio's card, a held banner.
        /// </summary>
        private static readonly string[] Clickable = { "edge-mark", "squad-card", "unit-head--opens", "goal-dock", "valley-zone", "radio", "banner" };

        private static readonly HashSet<VisualElement> hooked = new HashSet<VisualElement>();

        /// <summary>The clicks and keys of `element`'s panel (all the screens share AppFlow's): once per panel, now or when it is on one.</summary>
        public static void Install(VisualElement element)
        {
            if (element.panel != null)
            {
                Hook(element.panel.visualTree);
                return;
            }
            element.RegisterCallback<AttachToPanelEvent>(Attached);
        }

        private static void Attached(AttachToPanelEvent e)
        {
            var element = (VisualElement)e.target;
            element.UnregisterCallback<AttachToPanelEvent>(Attached);
            if (element.panel != null) Hook(element.panel.visualTree);
        }

        private static void Hook(VisualElement tree)
        {
            if (!hooked.Add(tree)) return;
            tree.RegisterCallback<PointerDownEvent>(OnPointer, TrickleDown.TrickleDown);
            tree.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
        }

        /// <summary>A left press on something clickable: «error» on a disabled button, «page» on the base's tabs, «confirm» on a main one, a key otherwise.</summary>
        private static void OnPointer(PointerDownEvent e)
        {
            if (e.button != 0) return;
            VisualElement? x = e.target as VisualElement;
            while (x != null && !(x is Button) && !IsClickable(x)) x = x.parent;
            // The skill board's cells have their own (SkillBoard: cellHover, cellHold, cellBuy).
            if (x == null || x.ClassListContains("hex-cell")) return;
            var kind = !x.enabledInHierarchy ? UiKind.Error
                : x.ClassListContains("strat-tab") || x.ClassListContains("armory-tab") || x.GetFirstAncestorWithClass("strat-nav") != null ? UiKind.Page
                : x.ClassListContains("btn-primary") || x.ClassListContains("squad-launch") ? UiKind.Confirm
                : UiKind.Key;
            Sound.Ui(kind, spread: kind == UiKind.Key ? 0.08f : 0.03f);
        }

        private static bool IsClickable(VisualElement x)
        {
            foreach (var c in Clickable)
                if (x.ClassListContains(c)) return true;
            return false;
        }

        private static VisualElement? GetFirstAncestorWithClass(this VisualElement x, string name)
        {
            for (var p = x.parent; p != null; p = p.parent)
                if (p.ClassListContains(name)) return p;
            return null;
        }

        /// <summary>
        /// Typing in a text field: Intro rings the bell, the space bar and the keys tick. UI Toolkit sends a key twice (its
        /// code, then its character): Intro and Backspace are heard by their code, the rest by their character.
        /// </summary>
        private static void OnKey(KeyDownEvent e)
        {
            var target = e.target as VisualElement;
            if (target == null || !(target is TextField || target.GetFirstAncestorOfType<TextField>() != null)) return;
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Sound.Ui(UiKind.Bell);
            else if (e.keyCode == KeyCode.Backspace) Sound.Ui(UiKind.Key, spread: 0.1f);
            else if (e.character == ' ') Sound.Ui(UiKind.Space, spread: 0.03f);
            else if (e.character != '\0' && !char.IsControl(e.character)) Sound.Ui(UiKind.Key, spread: 0.1f);
        }

        // ------------------------------------------------------------ the screens' sounds, by name

        private const double KeyGap = 0.07;
        private static double lastKey = double.NegativeInfinity;
        private static Action? hold;
        private static Action? radio;
        private static readonly HashSet<string> unknown = new HashSet<string>();

        /// <summary>
        /// A sound a screen asks for by name (HudRoot.Sound, StrategyRoot.Sound), as 0.10.1 played it: the banner's stamp
        /// (0.55), the report's stamp (a quarter of a second late), paper, the skill board's cells (a hold rings until
        /// let go before it buys), the radio (static, keys, bells, Morse, its click, and the music held down while it
        /// talks: transmissions.ts RadioSound), the partner's notice (static, 0.6), or any recipe by its name.
        /// </summary>
        public static void Play(string name)
        {
            switch (name)
            {
                case "stamp": Sound.Ui(UiKind.Stamp, 0.55f); return;
                case "stamp.outcome": Sound.Ui(UiKind.Stamp, delay: 0.25); return;
                case "confirm": Sound.Ui(UiKind.Confirm); return;
                case "paper": Sound.Ui(UiKind.Paper); return;
                case "bell": Sound.Ui(UiKind.Bell); return;
                case "cellHover": Sound.Ui(UiKind.CellHover); return;
                case "cellBuy": Sound.Ui(UiKind.CellBuy); return;
                case "cellHold": hold = Sound.Ui(UiKind.CellHold); return;
                // Bought: it rings out.
                case "cellHold.done": hold = null; return;
                case "cellHold.stop":
                    hold?.Invoke();
                    hold = null;
                    return;
                case "radio.open":
                    Sound.Play("radio.static");
                    radio ??= Sound.HoldDuck(0.4f);
                    return;
                case "radio.static": Sound.Play("radio.static"); return;
                case "radio.key":
                    // transmissions.ts typed a letter each 26 ms and keyed every third: never two keys closer than that, even
                    // when a long frame types many letters at once (26/10: 13 keys together as the base came up).
                    var now = Time.realtimeSinceStartupAsDouble;
                    if (now - lastKey < KeyGap) return;
                    lastKey = now;
                    Sound.Play("radio.key", spread: 0.15f);
                    return;
                case "radio.bells": Sound.Play("radio.bells", 0.35f); return;
                case "radio.morse": Sound.Morse(); return;
                case "radio.close":
                    Sound.Play("radio.close");
                    ReleaseRadio();
                    return;
                case "radio.end": ReleaseRadio(); return;
                case "notice": Sound.Play("radio.static", 0.6f); return;
                case "tick": Sound.Play("ui.tick"); return;
            }
            if (Sound.Exists(name)) Sound.Play(name);
            else if (unknown.Add(name)) Debug.LogWarning($"[sonido] la interfaz pide «{name}», que no es ningún sonido");
        }

        /// <summary>The music back up if the radio held it down (transmissions.ts close): a transmission open at take-off must not keep it low the whole mission.</summary>
        public static void ReleaseRadio()
        {
            radio?.Invoke();
            radio = null;
        }

        // ------------------------------------------------------------ music and ambience of the screens

        /// <summary>The start screen and the room (main.ts showPhase lobby): the base's music, no ambience, and no radio.</summary>
        public static void Lobby()
        {
            ReleaseRadio();
            Music.Play("base");
            Ambience.Play(null);
        }

        /// <summary>The base's view `view` (stage.ts): the Map («geoscape») with the globe's music and hum, the rest with the base's.</summary>
        public static void View(string view)
        {
            var piece = view == "geoscape" ? "geo" : "base";
            Music.Play(piece);
            Ambience.Play(piece);
        }
    }
}
