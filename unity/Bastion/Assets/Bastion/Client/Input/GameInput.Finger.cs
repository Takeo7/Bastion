using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Bastion.Client
{
    /// <summary>
    /// The finger (docs/IOS.md, phase 2; .dev-snapshots/ui/unity/ios/MENSAJE-A-GAMEPLAY.md, Arturo's choices of 7/11): the
    /// touches on the map read as gestures, once a frame before the game's scripts (Ticker). A tap; a long press (held
    /// still, LongTime); a drag of one finger, or of two together; two fingers pinching and turning. A finger that comes
    /// down on a panel is the panel's (OverUi: UI Toolkit takes it, a button's press, a list's scroll): the map never sees
    /// it. Only where the game is played with the finger (Touch); on the desktop none of this runs.
    /// </summary>
    public static partial class GameInput
    {
        /// <summary>
        /// The game is played with the finger: on a phone or a tablet (iOS); BASTION_TACTIL=1 tries it on the desktop, the
        /// mouse as one finger (the Input System's TouchSimulation). Set once, as the game loads.
        /// </summary>
        public static bool Touch { get; private set; }

        /// <summary>Whether a screen point (y up) is on a panel (the HUD, a menu, a window): UI sets it. A finger that comes down there isn't the map's.</summary>
        public static Func<Vector2, bool> OverUi;

        /// <summary>How far a finger moves before a touch is a drag, in points (Apple's 44-point targets, a quarter of one).</summary>
        public const float SlopPoints = 10;

        /// <summary>How long a finger is held still for a long press, seconds.</summary>
        public const float LongTime = 0.5f;

        /// <summary>How far two fingers turn for a quarter turn of the camera, degrees (Arturo, 7/11: in steps, as Q and E).</summary>
        public const float TwistStep = 30;

        // The gestures, this frame (Ticker sets them before any Update).

        /// <summary>A finger came down and up on the map without moving or being held (where: FingerAt).</summary>
        public static bool Tapped { get; private set; }

        /// <summary>A finger has been held still on the map for LongTime (this frame; where: FingerAt).</summary>
        public static bool LongPressed { get; private set; }

        /// <summary>The finger of a long press is still down.</summary>
        public static bool LongHeld { get; private set; }

        /// <summary>The finger of a long press came up (this frame).</summary>
        public static bool LongReleased { get; private set; }

        /// <summary>How far the map was dragged this frame, in screen pixels: one finger once it is a drag, or two together (their middle).</summary>
        public static Vector2 Drag { get; private set; }

        /// <summary>Two fingers' spread this frame over the last (1: none; more, apart: closer).</summary>
        public static float Pinch { get; private set; } = 1;

        /// <summary>−1 or +1 on the frame two fingers have turned TwistStep degrees (+1 the screen's counterclockwise); then it counts again.</summary>
        public static int Twist { get; private set; }

        /// <summary>Fingers down on the map now (not on a panel).</summary>
        public static int Fingers { get; private set; }

        /// <summary>Where the last finger on the map is or was, in screen pixels from the bottom left.</summary>
        public static Vector2 FingerAt { get; private set; }

        /// <summary>Where the first finger on the map is now, held or moving (FingerAt stays where a long press began): a tool's.</summary>
        public static Vector2 FingerNow { get; private set; }

        /// <summary>A finger came down on the map (this frame), the first of a gesture: with something in hand it may drag it (the base's felling, its rows).</summary>
        public static bool FingerDown { get; private set; }

        /// <summary>The gesture's last finger came up (this frame).</summary>
        public static bool FingerUp { get; private set; }

        /// <summary>The camera is the player's: a finger is on the map (the camera never fights the player).</summary>
        public static bool Busy => Fingers > 0;

        /// <summary>
        /// Screen pixels in a point (an iOS point, about 1/163 of an inch on a phone): on iOS, the screen's own scale
        /// (UIScreen.nativeScale, iOS/BastionScreen.mm; Screen.dpi doesn't know every device: the simulator's Duo, @3x, says
        /// 326); elsewhere from the dpi, or with none the HUD's 900-pixel height.
        /// </summary>
        public static float PixelsPerPoint
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                var scale = BastionScreenScale();
                if (scale > 0) return scale;
#endif
                return Screen.dpi > 0 ? Screen.dpi / 163f : Mathf.Max(1, Screen.height / 900f);
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern float BastionScreenScale();
#endif

        sealed class Finger
        {
            public Vector2 Start, Last;
            public float Since;
            public bool OnUi, Moved, Long, Paired;
        }

        static readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();
        static float twistTurned;
        static int tickedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void StartFinger()
        {
            Touch = Application.isMobilePlatform || Environment.GetEnvironmentVariable("BASTION_TACTIL") == "1";
            if (!Touch) return;
            EnhancedTouchSupport.Enable();
            // On the desktop, the mouse as a finger: one only (no pinching nor turning with it).
            if (!Application.isMobilePlatform) TouchSimulation.Enable();
            var o = new GameObject("Dedo");
            UnityEngine.Object.DontDestroyOnLoad(o);
            o.hideFlags = HideFlags.HideInHierarchy;
            o.AddComponent<Ticker>();
        }

        /// <summary>The touches of this frame read as gestures (once a frame, before any script reads them).</summary>
        static void TickFinger()
        {
            if (tickedFrame == Time.frameCount) return;
            tickedFrame = Time.frameCount;
            Tapped = LongPressed = LongReleased = FingerDown = FingerUp = false;
            Drag = Vector2.zero;
            Pinch = 1;
            Twist = 0;
            var slop = SlopPoints * PixelsPerPoint;
            var now = Time.realtimeSinceStartup;
            var wasDown = 0;
            foreach (var f in fingers.Values) if (!f.OnUi) wasDown++;
            var ended = new List<int>();
            foreach (var t in ETouch.activeTouches)
            {
                var id = t.touchId;
                var at = t.screenPosition;
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began || !fingers.ContainsKey(id))
                {
                    var onUi = OverUi != null && OverUi(at);
                    fingers[id] = new Finger { Start = at, Last = at, Since = now, OnUi = onUi };
                    if (!onUi && wasDown == 0) FingerDown = true;
                }
                var f = fingers[id];
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled) ended.Add(id);
                if (f.OnUi) continue;
                if (!f.Moved && (at - f.Start).magnitude > slop) f.Moved = true;
            }
            // The fingers on the map, as they are now.
            var map = new List<(int id, Finger f, Vector2 at)>();
            foreach (var t in ETouch.activeTouches)
                if (fingers.TryGetValue(t.touchId, out var f) && !f.OnUi) map.Add((t.touchId, f, t.screenPosition));
            // In the order they came down (their numbers), so a pair is the same pair from frame to frame.
            map.Sort((x, y) => x.id.CompareTo(y.id));
            if (map.Count >= 2)
            {
                // Two (the first two): spread, turn and their middle; neither is a tap or a long press any more.
                var (a, b) = (map[0], map[1]);
                a.f.Paired = b.f.Paired = true;
                var before = b.f.Last - a.f.Last;
                var after = b.at - a.at;
                if (before.magnitude > 1 && after.magnitude > 1)
                {
                    var spread = after.magnitude / before.magnitude;
                    // (Two fingers turning keep their spread but for a hair: no pinch.)
                    if (Mathf.Abs(spread - 1) > 0.002f) Pinch = spread;
                    twistTurned += Vector2.SignedAngle(before, after);
                    if (Mathf.Abs(twistTurned) >= TwistStep)
                    {
                        Twist = twistTurned > 0 ? 1 : -1;
                        twistTurned = 0;
                    }
                }
                Drag = (a.at + b.at) / 2 - (a.f.Last + b.f.Last) / 2;
                FingerAt = (a.at + b.at) / 2;
            }
            else if (map.Count == 1)
            {
                var (_, f, at) = map[0];
                twistTurned = 0;
                if (!f.Paired)
                {
                    if (f.Moved && !f.Long) Drag = at - f.Last;
                    if (!f.Moved && !f.Long && now - f.Since >= LongTime)
                    {
                        f.Long = true;
                        LongPressed = true;
                        FingerAt = f.Start;
                    }
                    if (!f.Long) FingerAt = at;
                }
            }
            foreach (var (id, f, at) in map) f.Last = at;
            if (map.Count > 0) FingerNow = map[0].at;
            // The ones that came up: a tap, the end of a long press.
            foreach (var id in ended)
            {
                var f = fingers[id];
                fingers.Remove(id);
                if (f.OnUi) continue;
                if (f.Long) LongReleased = true;
                else if (!f.Moved && !f.Paired)
                {
                    Tapped = true;
                    FingerAt = f.Last;
                }
            }
            var down = 0;
            var held = false;
            foreach (var f in fingers.Values)
                if (!f.OnUi)
                {
                    down++;
                    held |= f.Long;
                }
            Fingers = down;
            LongHeld = held;
            if (wasDown > 0 && down == 0) FingerUp = true;
            if (down == 0) twistTurned = 0;
            // BASTION_DEDO_LOG=1: each gesture in the log, to check them on the simulator.
            if (LogGestures && (Tapped || LongPressed || LongReleased || FingerDown || FingerUp || Twist != 0 || Drag != Vector2.zero || Pinch != 1))
                Debug.Log($"[dedo] {(Tapped ? "toque " : "")}{(LongPressed ? "largo " : "")}{(LongReleased ? "suelta-largo " : "")}{(FingerDown ? "baja " : "")}{(FingerUp ? "sube " : "")}"
                    + $"{(Twist != 0 ? $"gira {Twist} " : "")}{(Drag != Vector2.zero ? $"arrastra {Drag.x:0},{Drag.y:0} " : "")}{(Pinch != 1 ? $"pellizco {Pinch:0.000} " : "")}en {FingerAt.x:0},{FingerAt.y:0} · {Fingers} dedos");
        }

        static readonly bool LogGestures = Environment.GetEnvironmentVariable("BASTION_DEDO_LOG") == "1";

        /// <summary>BASTION_DEDO_LOG=1: the gestures, and what the game does with them, in the log.</summary>
        public static bool Logging => LogGestures;

        /// <summary>Reads the touches before any other script's Update, once a frame.</summary>
        [DefaultExecutionOrder(-10000)]
        sealed class Ticker : MonoBehaviour
        {
            void Update() => TickFinger();
        }
    }
}
