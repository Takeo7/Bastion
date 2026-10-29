using UnityEngine;
using UnityEngine.InputSystem;

namespace Bastion.UI.App
{
    /// <summary>
    /// Whose the keys are this frame. With Cmd or Ctrl held they are the system's or another app's, never the game's (Ana,
    /// 28/10: Cmd+Mayús+4, a screenshot, picked the pistol by its 4 and the next one fired it; in 0.10.1 macOS kept those
    /// keys, Unity reads the keyboard as it is). Mayús, Ctrl, Alt and Cmd alone are no key of the game's either (game.ts
    /// onKey: they don't let «TU TURNO» go).
    /// </summary>
    public static class Shortcuts
    {
        /// <summary>Cmd or Ctrl held: the game's keys do nothing.</summary>
        public static bool Elsewhere(Keyboard k) => k != null && (k.ctrlKey.isPressed || k.leftMetaKey.isPressed || k.rightMetaKey.isPressed);

        /// <summary>A key other than Mayús, Ctrl, Alt or Cmd pressed this frame.</summary>
        public static bool AnyOther(Keyboard k)
        {
            if (k == null || !k.anyKey.wasPressedThisFrame) return false;
            foreach (var key in k.allKeys)
                if (key.wasPressedThisFrame && !Modifier(key.keyCode)) return true;
            return false;
        }

        /// <summary>
        /// Cmd let go: whatever the keyboard still holds is let go too (Red, 28/10: with Cmd held macOS doesn't always send
        /// a key's release, so after Cmd+Mayús+4 the 4 stayed down and the next press of it was none).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Watch()
        {
            InputSystem.onAfterUpdate -= AfterUpdate;
            InputSystem.onAfterUpdate += AfterUpdate;
        }

        static bool commandWas;

        static void AfterUpdate()
        {
            var k = Keyboard.current;
            if (k == null) return;
            var command = k.leftMetaKey.isPressed || k.rightMetaKey.isPressed;
            if (commandWas && !command && k.anyKey.isPressed) InputSystem.ResetDevice(k);
            commandWas = command;
        }

        static bool Modifier(Key key) =>
            key == Key.LeftShift || key == Key.RightShift || key == Key.LeftCtrl || key == Key.RightCtrl
            || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftMeta || key == Key.RightMeta;
    }
}
