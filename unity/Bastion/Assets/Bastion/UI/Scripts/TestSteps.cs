using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI
{
    /// <summary>
    /// For tests with the app, without clicking (never in a game of the players): a list of «t:step», each at so many
    /// seconds from when the list starts, as a player's press. «press=Texto» presses every button showing whose text is
    /// that (any case; as Red's UiPress), «click=Texto» clicks a text showing that says it (what listens above it gets
    /// it), «escribe=campo:texto» writes in the text field of that name (Write), «ventana=1920x1080» resizes the window
    /// (Window), «tecla=DownArrow/0.1» holds a key as a real one (Tecla), «shot=name» saves the screen as
    /// BASTION_FOTOS/name.png, «quit» closes the app; the rest goes to the screen's
    /// own (`other`: BaseRun's steps on the base). Each step goes to the log as «[recorrido]».
    /// </summary>
    public static class TestSteps
    {
        public static async Awaitable Run(string list, Func<string, bool> other, CancellationToken ct)
        {
            var steps = list.Split(',').Select(t => t.Split(new[] { ':' }, 2)).Where(t => t.Length == 2)
                .Select(t => (at: float.Parse(t[0], CultureInfo.InvariantCulture), what: t[1].Trim())).OrderBy(t => t.at).ToList();
            var start = Time.realtimeSinceStartup;
            try
            {
                foreach (var (at, what) in steps)
                {
                    while (Time.realtimeSinceStartup - start < at) await Awaitable.NextFrameAsync(ct);
                    var done = Do(what, other);
                    Debug.Log($"[recorrido] {at:0.#} s: {what}{(done ? "" : " (nada que hacer)")}");
                }
            }
            catch (OperationCanceledException)
            {
                // The screen went first.
            }
        }

        static bool Do(string what, Func<string, bool> other)
        {
            var eq = what.IndexOf('=');
            // Only «name=…» and «quit» are these; the rest is the screen's («press» on the clearing is BaseRun's: taken
            // here as a press with no text, it pressed every button without one, the strip's «MENÚ» among them).
            if (eq < 0 && what != "quit") return other != null && other(what);
            var arg = eq < 0 ? "" : what.Substring(eq + 1);
            switch (eq < 0 ? what : what.Substring(0, eq))
            {
                case "press":
                    return Press(arg) > 0;
                case "click":
                    return Click(arg) > 0;
                case "escribe":
                    return Write(arg) > 0;
                case "ventana":
                    return Window(arg);
                case "tecla":
                    return Tecla(arg);
                case "shot":
                    var folder = Environment.GetEnvironmentVariable("BASTION_FOTOS");
                    if (string.IsNullOrEmpty(folder)) return false;
                    Directory.CreateDirectory(folder);
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, arg + ".png"));
                    return true;
                case "quit":
                    Application.Quit();
                    return true;
                default:
                    return other != null && other(what);
            }
        }

        /// <summary>Presses every button showing whose text, or a text inside it (the strip's «MENÚ»), is `text` (any case); how many.</summary>
        public static int Press(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            bool Says(Button x) => string.Equals(x.text, text, StringComparison.OrdinalIgnoreCase) || x.Query<TextElement>().ToList().Any(t => t != x && string.Equals(t.text, text, StringComparison.OrdinalIgnoreCase));
            var pressed = 0;
            foreach (var b in Showing<Button>().Where(Says))
            {
                using (var e = NavigationSubmitEvent.GetPooled())
                {
                    e.target = b;
                    b.SendEvent(e);
                }
                pressed++;
            }
            return pressed;
        }

        /// <summary>A click on every text showing that says `text` (any case), for what listens above it; how many.</summary>
        public static int Click(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var clicked = 0;
            foreach (var t in Showing<TextElement>().Where(x => string.Equals(x.text, text, StringComparison.OrdinalIgnoreCase)))
            {
                using (var e = ClickEvent.GetPooled())
                {
                    e.target = t;
                    t.SendEvent(e);
                }
                clicked++;
            }
            return clicked;
        }

        /// <summary>
        /// «escribe=address:127.0.0.1:3410» (Red, 24/11, Producto's yes; for joining by address without the keyboard): the text
        /// field showing whose name is the one before the first «:» takes the rest as typing leaves it (focused, its value
        /// set: the ChangeEvent goes out as with a key); how many.
        /// </summary>
        public static int Write(string arg)
        {
            var colon = arg.IndexOf(':');
            if (colon <= 0) return 0;
            var name = arg.Substring(0, colon);
            var text = arg.Substring(colon + 1);
            var written = 0;
            foreach (var field in Showing<TextField>().Where(x => x.name == name))
            {
                field.Focus();
                field.value = text;
                written++;
            }
            return written;
        }

        /// <summary>
        /// «ventana=1920x1080» (Red, 24/11, Producto's yes; for the interface's scale laid out again on a resize, Pruebas):
        /// the window made that size, windowed (Screen.SetResolution), as dragging its corner would. Unity keeps the last
        /// size in the app's own preferences when it quits: a test app's, with its own identifier, never Arturo's. False if
        /// the size isn't «<width>x<height>».
        /// </summary>
        public static bool Window(string arg)
        {
            var size = arg.ToLowerInvariant().Split('x');
            if (size.Length != 2 || !int.TryParse(size[0], out var width) || !int.TryParse(size[1], out var height) || width < 320 || height < 200) return false;
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            return true;
        }

        /// <summary>
        /// «tecla=DownArrow/0.1» (Red, 25/11, Producto's yes; for Pruebas, the menu by the keyboard): that key (InputSystem's
        /// Key, any case: UpArrow, Enter, Escape…) held so many seconds (0.1 unless said) and let go, as the fight's «hold»
        /// (CombatRun.Hold): the keyboard's state queued each frame, so it goes through the input actions and UI Toolkit's
        /// Navigate and Submit as a real key; the window's focus ignored meanwhile (a test app runs behind). False if the key
        /// isn't one.
        /// </summary>
        public static bool Tecla(string arg)
        {
            var part = arg.Split('/');
            if (!Enum.TryParse<UnityEngine.InputSystem.Key>(part[0], true, out var key) || key == UnityEngine.InputSystem.Key.None) return false;
            var seconds = part.Length > 1 && float.TryParse(part[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var held) ? held : 0.1f;
            if (UnityEngine.InputSystem.Keyboard.current == null) return false;
            _ = HoldKey(key, seconds);
            return true;
        }

        static async Awaitable HoldKey(UnityEngine.InputSystem.Key key, float seconds)
        {
            var keys = UnityEngine.InputSystem.Keyboard.current;
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            var was = settings.backgroundBehavior;
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                var until = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < until)
                {
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keys, new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
                    await Awaitable.NextFrameAsync();
                }
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keys, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            }
            finally
            {
                settings.backgroundBehavior = was;
            }
        }

        static System.Collections.Generic.IEnumerable<T> Showing<T>() where T : VisualElement
        {
            foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var root = doc.rootVisualElement;
                if (root == null) continue;
                foreach (var e in root.Query<T>().ToList())
                    if (e.panel != null && e.enabledInHierarchy && e.resolvedStyle.display != DisplayStyle.None && e.resolvedStyle.visibility == Visibility.Visible && e.worldBound.width > 0)
                        yield return e;
            }
        }
    }
}
