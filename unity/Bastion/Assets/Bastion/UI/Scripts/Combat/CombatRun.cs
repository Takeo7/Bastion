using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// For tests of the combat's HUD with two apps, without clicking (BASTION_PASOS_COMBATE, never in a game of the
    /// players): steps at given seconds from the mission's first state, as a player's presses. «intro» closes the arrival
    /// card, «banner» lets the turn's stamp go, «pass» hands the command over, «confirm» confirms the aim, «shot=name»
    /// saves the screen as BASTION_FOTOS/name.png, «quit» closes the app; the rest are PackSteps' (the backpack's, a key of
    /// the bar, the pointer on a tile, a click there). «2:intro,4:pick=s1,5:slot=0,6:shot=menu». Each step goes to the log as «[pasos]».
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
                default:
                    PackSteps.Do(control, what);
                    break;
            }
        }
    }
}
