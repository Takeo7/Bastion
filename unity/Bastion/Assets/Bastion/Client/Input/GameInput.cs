using UnityEngine;
using UnityEngine.InputSystem;

namespace Bastion.Client
{
    /// <summary>
    /// The player's orders, from one place (docs/IOS.md, phase 2: the finger and the mouse give the same orders): where the
    /// pointer is and its buttons, the wheel, the modifiers held, and each key of the game by what it does. The game asks
    /// here, never the mouse or the keyboard. On the desktop this reads them as the scripts did themselves, call for call,
    /// so nothing changes there; a finger on iOS will give the same orders here, with the camera's gestures. The tests that
    /// play keys and clicks (UI's CombatLab, CombatRun) feed the devices, so they come through here too.
    ///
    /// Whose the keys are: with Cmd or Ctrl held they are the system's or another app's (SystemHeld; Ana, 28/10: Cmd+Mayús+4,
    /// a screenshot, picked the pistol by its 4 and the next one fired it). Mayús, Ctrl, Alt and Cmd alone are no key of
    /// the game's (AnyOtherKey; game.ts onKey: they don't let «TU TURNO» go).
    /// </summary>
    public static partial class GameInput
    {
        /// <summary>The pointer's buttons by what they do: the main one (pick, act), the other (back out, drag the ground), the wheel's (mark).</summary>
        public enum Button { Primary, Secondary, Middle }

        /// <summary>The game's keys by what they do; on the desktop, the key in the comment.</summary>
        public enum Key
        {
            Confirm,     // Intro, or the numeric pad's
            Space,       // the bar: confirm; held in the enemy's turn, «Rápida»
            Back,        // Esc
            Sheet,       // H
            Cycle,       // Tab (with Mayús, the other way)
            Pass,        // C
            Map,         // M
            Sight,       // V
            NextMark,    // O
            Mark,        // G
            Ready,       // Retroceso
            FloorUp,     // RePág
            FloorDown,   // AvPág
            Focus,       // F
            Screenshot,  // F12
            TurnLeft,    // Q
            TurnRight,   // E
            Build,       // B
            Rotate,      // R (the piece in hand)
            Forward,     // W, ↑
            Backward,    // S, ↓
            Right,       // D, →
            Left,        // A, ←
        }

        // ------------------------------------------------------------------ pointer

        /// <summary>Whether there is a pointer at all (a mouse; with the finger, a touch screen).</summary>
        public static bool HasPointer => Touch ? Touchscreen.current != null : Mouse.current != null;

        /// <summary>Where the pointer is, in screen pixels from the bottom left (as Mouse.position); with the finger, where it last touched the map.</summary>
        public static Vector2 Point => Touch ? FingerAt : Mouse.current is Mouse m ? m.position.ReadValue() : Vector2.zero;

        /// <summary>How far the pointer moved this frame; with the finger nothing hovers, so nothing (the gestures say what it did).</summary>
        public static Vector2 PointMoved => Touch ? Vector2.zero : Mouse.current is Mouse m ? m.delta.ReadValue() : Vector2.zero;

        /// <summary>A button went down this frame.</summary>
        public static bool Pressed(Button b) => ButtonOf(b) is var c && c != null && c.wasPressedThisFrame;

        /// <summary>A button came up this frame.</summary>
        public static bool Released(Button b) => ButtonOf(b) is var c && c != null && c.wasReleasedThisFrame;

        /// <summary>A button is down.</summary>
        public static bool Held(Button b) => ButtonOf(b) is var c && c != null && c.isPressed;

        /// <summary>The wheel this frame: y up and down; x sideways (macOS turns it so with Mayús held). With the finger, none (Pinch).</summary>
        public static Vector2 Wheel => !Touch && Mouse.current is Mouse m ? m.scroll.ReadValue() : Vector2.zero;

        /// <summary>The mouse's button; with the finger there are none (the gestures: Tapped, LongPressed, Drag…).</summary>
        static UnityEngine.InputSystem.Controls.ButtonControl ButtonOf(Button b)
        {
            if (Touch) return null;
            var m = Mouse.current;
            if (m == null) return null;
            return b == Button.Primary ? m.leftButton : b == Button.Secondary ? m.rightButton : m.middleButton;
        }

        // --------------------------------------------------------------------- keys

        /// <summary>Whether there are keys at all (a keyboard, also one joined to an iPad).</summary>
        public static bool HasKeys => Keyboard.current != null;

        /// <summary>A key of the game's went down this frame.</summary>
        public static bool Down(Key key)
        {
            var k = Keyboard.current;
            if (k == null) return false;
            var (a, b) = KeysOf(k, key);
            return a.wasPressedThisFrame || (b != null && b.wasPressedThisFrame);
        }

        /// <summary>A key of the game's came up this frame.</summary>
        public static bool Up(Key key)
        {
            var k = Keyboard.current;
            if (k == null) return false;
            var (a, b) = KeysOf(k, key);
            return a.wasReleasedThisFrame || (b != null && b.wasReleasedThisFrame);
        }

        /// <summary>A key of the game's is down.</summary>
        public static bool Held(Key key)
        {
            var k = Keyboard.current;
            if (k == null) return false;
            var (a, b) = KeysOf(k, key);
            return a.isPressed || (b != null && b.isPressed);
        }

        /// <summary>
        /// The number `n` (0 to 9) went down this frame: an ability of the bar. (The Input System's keys run Digit1…Digit9,
        /// Digit0, then Mayús, Alt, Ctrl and Cmd: Digit0 + n made Mayús the 1 and left the numbers dead. Ana, 28/10.)
        /// </summary>
        public static bool Number(int n)
        {
            var k = Keyboard.current;
            return k != null && n >= 0 && n <= 9 && k[n == 0 ? UnityEngine.InputSystem.Key.Digit0 : UnityEngine.InputSystem.Key.Digit1 + (n - 1)].wasPressedThisFrame;
        }

        /// <summary>Mayús held (a click marks the tile; Tab goes the other way; the wheel changes floor).</summary>
        public static bool Shift => Keyboard.current is Keyboard k && k.shiftKey.isPressed;

        /// <summary>Alt held (the wheel changes floor).</summary>
        public static bool Alt => Keyboard.current is Keyboard k && k.altKey.isPressed;

        /// <summary>Cmd or Ctrl held: the keys are the system's, the game's do nothing.</summary>
        public static bool SystemHeld => Keyboard.current is Keyboard k && (k.ctrlKey.isPressed || k.leftMetaKey.isPressed || k.rightMetaKey.isPressed);

        /// <summary>A key other than Mayús, Ctrl, Alt or Cmd went down this frame.</summary>
        public static bool AnyOtherKey
        {
            get
            {
                var k = Keyboard.current;
                if (k == null || !k.anyKey.wasPressedThisFrame) return false;
                foreach (var key in k.allKeys)
                    if (key.wasPressedThisFrame && !Modifier(key.keyCode)) return true;
                return false;
            }
        }

        static (UnityEngine.InputSystem.Controls.KeyControl, UnityEngine.InputSystem.Controls.KeyControl) KeysOf(Keyboard k, Key key)
        {
            switch (key)
            {
                case Key.Confirm: return (k.enterKey, k.numpadEnterKey);
                case Key.Space: return (k.spaceKey, null);
                case Key.Back: return (k.escapeKey, null);
                case Key.Sheet: return (k.hKey, null);
                case Key.Cycle: return (k.tabKey, null);
                case Key.Pass: return (k.cKey, null);
                case Key.Map: return (k.mKey, null);
                case Key.Sight: return (k.vKey, null);
                case Key.NextMark: return (k.oKey, null);
                case Key.Mark: return (k.gKey, null);
                case Key.Ready: return (k.backspaceKey, null);
                case Key.FloorUp: return (k.pageUpKey, null);
                case Key.FloorDown: return (k.pageDownKey, null);
                case Key.Focus: return (k.fKey, null);
                case Key.Screenshot: return (k.f12Key, null);
                case Key.TurnLeft: return (k.qKey, null);
                case Key.TurnRight: return (k.eKey, null);
                case Key.Build: return (k.bKey, null);
                case Key.Rotate: return (k.rKey, null);
                case Key.Forward: return (k.wKey, k.upArrowKey);
                case Key.Backward: return (k.sKey, k.downArrowKey);
                case Key.Right: return (k.dKey, k.rightArrowKey);
                default: return (k.aKey, k.leftArrowKey);
            }
        }

        static bool Modifier(UnityEngine.InputSystem.Key key) =>
            key == UnityEngine.InputSystem.Key.LeftShift || key == UnityEngine.InputSystem.Key.RightShift
            || key == UnityEngine.InputSystem.Key.LeftCtrl || key == UnityEngine.InputSystem.Key.RightCtrl
            || key == UnityEngine.InputSystem.Key.LeftAlt || key == UnityEngine.InputSystem.Key.RightAlt
            || key == UnityEngine.InputSystem.Key.LeftMeta || key == UnityEngine.InputSystem.Key.RightMeta;

        // ------------------------------------------------------------ the keyboard

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
    }
}
