using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// For tests of the combat's HUD with two apps, without clicking (BASTION_PASOS_COMBATE, never in a game of the
    /// players): steps at given seconds from the mission's first state, as a player's presses. «intro» closes the arrival
    /// card, «banner» lets the turn's stamp go, «pass» hands the command over, «confirm» confirms the aim, «shot=name»
    /// saves the screen as BASTION_FOTOS/name.png, «quit» closes the app, «hold=LeftShift/2» holds a key, «wheel=1» a notch
    /// of the wheel up (-1 down), «wheelx=1» sideways (as macOS turns it with Shift), the floor then said in the log; the
    /// rest are PackSteps' (the backpack's, a key of the bar, the pointer on a tile, a click there).
    /// «2:intro,4:pick=s1,5:slot=0,6:shot=menu». Each step goes to the log as «[pasos]».
    /// </summary>
    public sealed class CombatRun : MonoBehaviour
    {
        CombatController control;
        string steps;
        string shots;

        /// <summary>The run BASTION_PASOS_COMBATE asks for, on `control`, or none.</summary>
        public static void FromEnvironment(CombatController control)
        {
            var steps = Environment.GetEnvironmentVariable("BASTION_PASOS_COMBATE");
            if (string.IsNullOrEmpty(steps)) return;
            var run = control.gameObject.AddComponent<CombatRun>();
            run.control = control;
            run.steps = steps;
            run.shots = Environment.GetEnvironmentVariable("BASTION_FOTOS");
        }

        /// <summary>
        /// A test app is seldom the one in front: while a step's key or wheel goes in, its devices take it all the same;
        /// then back as they were, so the player's own keys and mouse in another window never reach it. How it was.
        /// </summary>
        static InputSettings.BackgroundBehavior Unfocused()
        {
            var was = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            return was;
        }

        async void Start()
        {
            try
            {
                while (control.State == null) await Awaitable.NextFrameAsync(destroyCancellationToken);
                var start = Time.realtimeSinceStartup;
                var list = steps.Split(',').Select(t => t.Split(new[] { ':' }, 2)).Where(t => t.Length == 2)
                    .Select(t => (at: float.Parse(t[0], CultureInfo.InvariantCulture), what: t[1].Trim())).OrderBy(t => t.at).ToList();
                foreach (var (at, what) in list)
                {
                    while (Time.realtimeSinceStartup - start < at) await Awaitable.NextFrameAsync(destroyCancellationToken);
                    Debug.Log($"[pasos] {at:0} s: {what}");
                    Do(what);
                }
            }
            catch (OperationCanceledException)
            {
                // The mission went first.
            }
        }

        void Do(string what)
        {
            var hud = control.Hud;
            var at = what.IndexOf('=');
            var arg = at < 0 ? "" : what.Substring(at + 1);
            switch (at < 0 ? what : what.Substring(0, at))
            {
                case "intro":
                    if (hud != null && hud.IntroOpen) hud.CloseIntro();
                    break;
                case "banner":
                    hud?.ReleaseBanner();
                    break;
                case "pass":
                    control.OnPassCommand();
                    break;
                case "confirm":
                    control.OnConfirmTarget();
                    break;
                case "shot":
                    if (string.IsNullOrEmpty(shots)) break;
                    Directory.CreateDirectory(shots);
                    ScreenCapture.CaptureScreenshot(Path.Combine(shots, arg + ".png"));
                    break;
                case "quit":
                    Application.Quit();
                    break;
                case "hold":
                    var held = arg.Split('/');
                    if (held.Length == 2 && Enum.TryParse<Key>(held[0], true, out var key) && float.TryParse(held[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)) Hold(key, seconds);
                    break;
                case "wheel":
                case "wheelx":
                    if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out var notch)) Wheel(what.StartsWith("wheelx") ? new Vector2(notch * 120, 0) : new Vector2(0, notch * 120));
                    break;
                default:
                    PackSteps.Do(control, what);
                    break;
            }
        }

        /// <summary>A key held for `seconds`, as the combat lab's (its state queued each frame), then let go.</summary>
        async void Hold(Key key, float seconds)
        {
            var keys = Keyboard.current;
            if (keys == null) return;
            var was = Unfocused();
            try
            {
                var until = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < until)
                {
                    InputSystem.QueueStateEvent(keys, new KeyboardState(key));
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
                InputSystem.QueueStateEvent(keys, new KeyboardState());
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                InputSystem.settings.backgroundBehavior = was;
            }
        }

        /// <summary>A notch of the wheel over the screen's middle, and the floor shown a few frames on (the log).</summary>
        async void Wheel(Vector2 turn)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var was = Unfocused();
            try
            {
                var middle = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = middle, scroll = turn });
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = middle });
                for (var i = 0; i < 5; i++) await Awaitable.NextFrameAsync(destroyCancellationToken);
                Debug.Log($"[pasos] rueda {turn}: planta {control.Level}");
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                InputSystem.settings.backgroundBehavior = was;
            }
        }
    }
}
