using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace Bastion.UI.Start
{
    /// <summary>
    /// The keyboard on the start screen and the room with no pointer (the 0.14.5's independent review, the four one-minute
    /// tests): BASTION_TECLAS="2:down,2.5:enter" presses those keys through the Input System, as a player's would come (UI
    /// Toolkit's own navigation sees them too), and logs after each where the focus is and which entry is chosen. The start
    /// screen only says what it would do (StartScreen.DryRun): no game opened, hosted nor joined.
    /// </summary>
    public sealed class TeclasLab : MonoBehaviour
    {
        void Start()
        {
            StartScreen.DryRun = true;
            Run();
        }

        async void Run()
        {
#if UNITY_EDITOR
            // (A batch run has no game view in focus: a copy of the settings that sends every device to the game, as CombatLab.)
            var settings = ScriptableObject.Instantiate(InputSystem.settings);
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
#endif
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keys = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            if (!keys.enabled) InputSystem.EnableDevice(keys);
            var steps = (System.Environment.GetEnvironmentVariable("BASTION_TECLAS") ?? "").Split(',').Select(t => t.Split(':')).Where(t => t.Length == 2)
                .Select(t => (at: float.Parse(t[0], System.Globalization.CultureInfo.InvariantCulture), what: t[1].Trim())).OrderBy(t => t.at).ToList();
            // (By the game's clock: a capture fixes it per frame, its real time far shorter.)
            var start = Time.time;
            Log("al empezar");
            foreach (var (at, what) in steps)
            {
                while (Time.time - start < at) await Awaitable.NextFrameAsync(destroyCancellationToken);
                var key = what switch
                {
                    "down" => Key.DownArrow, "up" => Key.UpArrow, "enter" => Key.Enter, "space" => Key.Space, "esc" => Key.Escape,
                    "s" => Key.S, "w" => Key.W, "tab" => Key.Tab, _ => Key.None,
                };
                if (key == Key.None) continue;
                InputSystem.QueueStateEvent(keys, new KeyboardState(key));
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                for (var i = 0; i < 6; i++) await Awaitable.NextFrameAsync(destroyCancellationToken);
                Log($"tras «{what}»");
            }
        }

        void Log(string when)
        {
            var panel = GetComponent<UIDocument>()?.rootVisualElement?.panel;
            var focused = panel?.focusController?.focusedElement as VisualElement;
            var root = GetComponent<UIDocument>()?.rootVisualElement;
            var chosen = root?.Query(className: "start__entry--chosen").ToList().Select(Words).Where(w => w != null);
            Debug.Log($"[teclas] {when}: foco en {Describe(focused)}; elegida {string.Join(" / ", chosen ?? new string[0])}");
        }

        static string Words(VisualElement e) => e.Q<Label>(className: "start__entry-text")?.text ?? (e as TextElement)?.text;

        static string Describe(VisualElement e)
        {
            if (e == null) return "nadie";
            if (e is TextField f) return $"el campo «{f.name}»";
            var words = Words(e);
            return words != null ? $"«{words}»" : $"{e.GetType().Name} {e.name}";
        }
    }
}
