using Bastion.Client;
using UnityEngine;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The tactical camera (camera.ts of the 0.10.1, UI's): a fixed pitch of 45° (the 0.10.1's 52°, a little less steep:
    /// Ana, Arturo 31/10 #12), the yaw in quarter turns (Q/E), WASD or
    /// the arrows to move, the wheel to come closer, the right button held to drag. Sent somewhere (Focus), it goes on a
    /// critically damped spring; the player's own moves are followed at once. Whatever the player does raises `Epoch`,
    /// and what was taking the camera somewhere lets go of it: the camera never fights the player.
    /// For an action it comes closer (CinematicZoom, RestoreZoom), shakes, and for the attack that kills it goes over the
    /// shoulder (CloseShot: shotCam.ts works out where), blended in and out of its own view.
    /// Distances in metres: the web's, in tiles, times 1.5.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        const float Pitch = 45f;
        /// <summary>From above (#12): all but straight down, so that north stays up the screen.</summary>
        const float OverheadPitch = 89f;
        const float MinDistance = 9f * 1.5f;
        /// <summary>How much closer a unit of the wheel takes the camera (0.001 till 18/11), and how fast the distance follows (7, the rest's).</summary>
        const float WheelZoom = 0.0025f, WheelFollow = 12f;
        /// <summary>
        /// The farthest: 33.5 tiles at 45°, which looks as far beyond the point looked at as the 0.10.1's 42 did at 52°
        /// (about 27 tiles), so neither the sky nor the map's edge comes in round the middle of a map.
        /// </summary>
        const float MaxDistance = 33.5f * 1.5f;
        const float Spring = 5.5f;
        /// <summary>Into the shot over the shoulder, out of it, and from one to the next (s).</summary>
        const float CloseIn = 0.8f;
        const float CloseOut = 0.9f;
        const float CloseSwap = 0.7f;

        /// <summary>Raised by every move of the player's: a move sent while it was another number lets go.</summary>
        public int Epoch { get; private set; }

        /// <summary>
        /// Lab only (CombatLab's BASTION_LAB_CAMARA_QUIETA; Arte's strips of a guard getting up, U28, 20/11): while set, the
        /// game's own moves of the camera are left undone (Focus, the zooms, the shake, the shot over the shoulder), and it
        /// stays where it is; a toque's «look=» still moves it (LabLook). Never set in the game.
        /// </summary>
        public bool Still;

        /// <summary>The lab's «look=»: to `p`, Still or not.</summary>
        public void LabLook(Vector3 p)
        {
            var was = Still;
            Still = false;
            Focus(p);
            Still = was;
        }

        /// <summary>
        /// For measuring only (Arte's ShadowFit, BASTION_MEDIR): BASTION_CAMARA=&lt;tiles&gt; holds the camera at that distance
        /// (33.5, the farthest), with no zoom of the player's or of an action's nor the shot over the shoulder; where it
        /// looks goes on moving as always. Unset, nothing changes.
        /// </summary>
        static readonly float? HeldAt = float.TryParse(System.Environment.GetEnvironmentVariable("BASTION_CAMARA"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tiles)
            ? Mathf.Clamp(tiles * 1.5f, MinDistance, MaxDistance)
            : (float?)null;

        /// <summary>
        /// For measuring only (the review of 10/11, A-01): BASTION_CAMARA_LOG=1 says each frame how far the camera is from
        /// what it looks at, where it looks and how far into the shot over a shoulder it is («[camara] …»).
        /// </summary>
        public static readonly bool Logging = System.Environment.GetEnvironmentVariable("BASTION_CAMARA_LOG") == "1";

        /// <summary>How far into the shot over a shoulder it is (0 its own view, 1 there): for the measure.</summary>
        public float Closeness => closeness;

        /// <summary>
        /// The seconds the shot over a shoulder still needs to be there (into it, or from one to the next); 0 when it is, or
        /// when there is none (the review of 10/11, A-01: the blow went before the camera got there).
        /// </summary>
        public float CloseLeft => closeOn && close != null ? Mathf.Max((1 - closeness) * CloseIn, swap < 1 ? (1 - swap) * CloseSwap : 0) : 0;

        /// <summary>The map's floor plan (metres, x and z), which the point looked at stays in.</summary>
        public Rect Bounds = new Rect(0, -96, 96, 96);

        Vector3 target;
        Vector3 desiredTarget;
        Vector3 targetVel;
        bool springing;
        float yaw = 45f;
        float desiredYaw = 45f;
        float pitch = Pitch;
        /// <summary>Where it stood before going up over the map (EnterOverhead): what LeaveOverhead goes back to.</summary>
        (float yaw, float distance, Vector3 target) beforeOverhead;
        float distance = 24f * 1.5f;
        float desiredDistance = 24f * 1.5f;
        /// <summary>
        /// The game's zooms (an action's, the enemy's turn framed, back after them) go on the point's spring, from rest and to
        /// rest (the review of 10/11, A-01: back from an action, half the way went in a frame); the player's, as before.
        /// </summary>
        bool zoomSpring;
        float distanceVel;
        /// <summary>The distance's spring: the point's, a little livelier framing the enemy's turn (FrameZoom), which waits for it (A-01).</summary>
        float zoomStiffness = Spring;
        const float FrameSpring = 8f;
        /// <summary>The nearest an attack's framing comes of itself (FrameZoom `closer`): 16 tiles.</summary>
        const float CloserFrame = 16f * 1.5f;
        Vector2 dragFrom;
        bool dragging;
        Camera cam;
        /// <summary>The player's distance while an action's comes closer (CinematicZoom); none, none taken.</summary>
        float? savedDistance;
        float shakeStrength;
        float shakeTime;
        (Vector3 pos, Vector3 look)? close;
        (Vector3 pos, Vector3 look)? closeFrom;
        bool closeOn;
        float closeness;
        float swap = 1;

        public Camera Camera => cam;
        public Vector3 Target => target;

        /// <summary>When the player last moved the camera (unscaled seconds): a cinematic doesn't take it for a while after.</summary>
        public float TakenAt { get; private set; } = -100;

        /// <summary>Whether a move sent (Focus) has got there.</summary>
        public bool Settled => !springing;

        /// <summary>How far the point looked at still is from where a move sent (Focus) takes it, in metres; 0 once there or with none.</summary>
        public float Left => springing ? Vector3.Distance(target, desiredTarget) : 0;

        /// <summary>
        /// For the measure (BASTION_CAMARA_LOG, N5 of 22/11): a move sent, how far and when, to say how long it takes to come
        /// within a tile and to stop («[cámara] viaje …»).
        /// </summary>
        float tripLength, tripStart;
        bool tripNear, tripOn;

        /// <summary>Whether a move sent has got there and so has the distance asked for (FrameZoom, CinematicZoom).</summary>
        public bool Arrived => !springing && Mathf.Abs(distance - desiredDistance) < 0.5f;

        /// <summary>The quarter turn it faces: 0–3 (camera.ts quadrant), for what is drawn by it.</summary>
        public int Quadrant => ((Mathf.RoundToInt((desiredYaw - 45f) / 90f) % 4) + 4) % 4;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.fieldOfView = 40f;
            if (HeldAt != null) distance = desiredDistance = HeldAt.Value;
        }

        /// <summary>
        /// Up over the map (Arturo, 31/10 #12 and 01/11: the map as the camera itself): straight down, north up, `metres`
        /// from `centre`, eased there; meanwhile nothing moves it (Focus, the zooms, the shot over a shoulder and the
        /// player's keys do nothing) till LeaveOverhead.
        /// </summary>
        public bool Overhead { get; private set; }

        /// <summary>From above, how near and how far the wheel and the fingers take it (U17): a few rooms, or the whole map.</summary>
        float overNear, overFar;

        /// <summary>`near` and `far` (metres): the wheel's bounds up there (none given, it stays at `metres`).</summary>
        public void EnterOverhead(Vector3 centre, float metres, float near = 0, float far = 0)
        {
            if (Overhead) return;
            beforeOverhead = (desiredYaw, desiredDistance, desiredTarget);
            Overhead = true;
            overNear = near > 0 ? Mathf.Min(near, metres) : metres;
            overFar = Mathf.Max(far, metres);
            dragging = false;
            closeOn = false;
            savedDistance = null;
            zoomSpring = false;
            desiredYaw = Mathf.Round(desiredYaw / 360f) * 360f;
            desiredTarget = centre;
            desiredDistance = metres;
            springing = true;
        }

        /// <summary>Back down from above: as it was, over `at` if given (a click on the map); the player's move.</summary>
        public void LeaveOverhead(Vector3? at = null)
        {
            if (!Overhead) return;
            Overhead = false;
            Epoch++;
            TakenAt = Time.unscaledTime;
            zoomSpring = false;
            desiredYaw = beforeOverhead.yaw;
            desiredDistance = beforeOverhead.distance;
            desiredTarget = at is Vector3 p ? Clamp(new Vector3(p.x, Mathf.Max(0, p.y), p.z)) : beforeOverhead.target;
            springing = true;
        }

        /// <summary>To `p` on the spring (or at once); it lets go if the player moves the camera meanwhile.</summary>
        public void Focus(Vector3 p, bool instant = false)
        {
            if (Overhead || Still) return;
            desiredTarget = Clamp(new Vector3(p.x, Mathf.Max(0, p.y), p.z));
            springing = !instant;
            if (Logging && !instant)
            {
                tripLength = Vector3.Distance(target, desiredTarget);
                tripStart = Time.unscaledTime;
                tripNear = false;
                tripOn = true;
                Debug.Log($"[cámara] viaje de {tripLength:0.0} m ({tripLength / 1.5f:0.0} casillas)");
            }
            if (!instant) return;
            target = desiredTarget;
            targetVel = Vector3.zero;
            Place();
        }

        /// <summary>
        /// The camera as it stands taken over (the clearing's free look, once Arte's shot has eased in: screen.ts «only
        /// once the stage has eased it home»): the point of the ground at `groundY` it looks at, its turn and its distance
        /// kept; the pitch is the rig's own.
        /// </summary>
        public void TakeOver(float groundY = 0)
        {
            var f = transform.forward;
            var from = transform.position;
            var reach = f.y < -0.05f ? (groundY - from.y) / f.y : 24f * 1.5f;
            var at = from + f * reach;
            target = desiredTarget = Clamp(new Vector3(at.x, Mathf.Max(0, groundY), at.z));
            targetVel = Vector3.zero;
            springing = false;
            var back = from - target;
            yaw = desiredYaw = Mathf.Atan2(back.x, -back.z) * Mathf.Rad2Deg;
            distance = desiredDistance = Mathf.Clamp(back.magnitude, MinDistance, MaxDistance);
            distanceVel = 0;
            zoomSpring = false;
            savedDistance = null;
            closeOn = false;
            close = null;
            closeFrom = null;
            closeness = 0;
            Place();
        }

        /// <summary>A quarter turn left (−1) or right (1).</summary>
        public void Rotate(int dir)
        {
            if (Overhead) return;
            // Aiming over a shoulder, Q and E (or two fingers turned) turn that shot instead of going back out (TurnClose).
            if (closeOn && TurnClose != null && TurnClose(dir)) return;
            TakeControl();
            desiredYaw += dir * 90f;
        }

        /// <summary>
        /// A quarter turn asked for while in a shot over a shoulder that the player may turn (aiming: Arturo, 14/11): whether
        /// it took it (CombatController.TurnAim); if not, the turn leaves the shot for the rig's own view, as before.
        /// </summary>
        public System.Func<int, bool> TurnClose;

        /// <summary>Round by `degrees` more, eased as a quarter turn is, for a cinematic (La Lata leaving: Arturo, 31/10 #1); not the player's.</summary>
        public void Turn(float degrees) => desiredYaw += degrees;

        /// <summary>
        /// The view turned to `degrees` (45° looks north-west), at once or eased: the opening shot inside a building, its
        /// way in ahead (the hospital, H7: CombatController.OpeningShot).
        /// </summary>
        public void FaceYaw(float degrees, bool instant = false)
        {
            if (Overhead) return;
            desiredYaw = degrees;
            if (!instant) return;
            yaw = degrees;
            Place();
        }

        /// <summary>The player took the camera (a move, a pick): whatever was taking it somewhere lets go.</summary>
        public void TakeControl()
        {
            // BASTION_CAMARA_LOG: who took it (the log carries the stack).
            if (Logging) Debug.Log($"[cámara] tomada por el jugador{(tripOn && springing ? $" a {Time.unscaledTime - tripStart:0.00} s del viaje, a {Left:0.0} m" : "")}");
            tripOn = false;
            Epoch++;
            TakenAt = Time.unscaledTime;
            savedDistance = null;
            closeOn = false;
            springing = false;
            zoomSpring = false;
            distanceVel = 0;
        }

        /// <summary>The game's menu is up: the keys and the pointer are its own, the camera only finishes its moves.</summary>
        public System.Func<bool> Held;

        /// <summary>Whether a screen point is over the HUD: the wheel scrolls the log or a list there, not the camera.</summary>
        public System.Func<Vector2, bool> OverHud;

        /// <summary>The right button held drags the ground; off on the clearing (playtest 20/10 #6: «solo WASD»), where a right click lets go of what is in hand.</summary>
        public bool RightDrag = true;

        /// <summary>With the finger, one finger is a tool's, not the camera's (the base with something in hand): two move it.</summary>
        public System.Func<bool> OneFingerTaken;

        void Update()
        {
            // The pause (Time.timeScale 0: CombatController.Halt): the camera waits where it is with the battle, whatever it
            // was doing (over a shoulder, after La Lata, framing the enemy's turn), and goes on from there (Ana, Arturo 01/11
            // #4); the menu has the keys meanwhile.
            if (Time.timeScale == 0) return;
            var dt = Time.unscaledDeltaTime;
            // From above, the clicks are the map's (the controller's); the wheel and the fingers bring it closer or take it
            // further, and it is moved round once closer (OverheadInput).
            if (Overhead)
            {
                OverheadInput(dt);
                Step(dt);
                return;
            }
            var held = Held != null && Held();
            // (Not with Cmd or Ctrl held: Cmd+W, Cmd+Q, Ctrl+A are the system's.)
            var keys = !held && !GameInput.SystemHeld && GameInput.HasKeys;
            var mouse = !held && GameInput.HasPointer;
            if (keys)
            {
                if (GameInput.Down(GameInput.Key.TurnLeft)) Rotate(-1);
                if (GameInput.Down(GameInput.Key.TurnRight)) Rotate(1);
                var speed = 24f * dt * (distance / 36f);
                var forward = 0f;
                var right = 0f;
                if (GameInput.Held(GameInput.Key.Forward)) forward += speed;
                if (GameInput.Held(GameInput.Key.Backward)) forward -= speed;
                if (GameInput.Held(GameInput.Key.Right)) right += speed;
                if (GameInput.Held(GameInput.Key.Left)) right -= speed;
                if (right != 0 || forward != 0)
                {
                    TakeControl();
                    Pan(right, forward);
                }
            }
            if (mouse)
            {
                // With Shift or Alt the wheel changes floor (the controller's), and over the HUD it is the HUD's.
                var wheel = GameInput.Wheel.y;
                if (wheel != 0 && (!keys || !GameInput.Shift && !GameInput.Alt) && (OverHud == null || !OverHud(GameInput.Point)))
                {
                    TakeControl();
                    // A turn of the wheel goes further, and the camera gets there sooner (Arturo, 18/11 T19: «que hacer
                    // scroll cueste menos»): from the farthest to the nearest in about four notches (it took about ten), no
                    // more than a good step a frame, so a trackpad's flick doesn't jump.
                    var step = Mathf.Clamp(wheel * WheelZoom, -0.45f, 0.45f);
                    desiredDistance = Mathf.Clamp(desiredDistance * (1 - step), MinDistance, MaxDistance);
                }
                // The right button held drags the ground under it.
                var at = GameInput.Point;
                if (RightDrag && GameInput.Pressed(GameInput.Button.Secondary))
                {
                    dragging = true;
                    dragFrom = at;
                }
                if (!GameInput.Held(GameInput.Button.Secondary)) dragging = false;
                if (dragging && at != dragFrom)
                {
                    TakeControl();
                    var scale = distance / Mathf.Max(1, Screen.height) * 1.2f;
                    Pan(-(at.x - dragFrom.x) * scale, -(at.y - dragFrom.y) * scale);
                    dragFrom = at;
                }
            }
            // The finger (GameInput, iOS; Arturo, 7/11): one finger dragged, or two, drag the ground under them; two pinch it
            // closer or further, within the wheel's bounds; two turned TwistStep degrees turn a quarter, as Q and E. A finger
            // on the map keeps the camera the player's (the camera never fights the player).
            if (!held && GameInput.Touch)
            {
                var drag = GameInput.Drag;
                if (drag != Vector2.zero && !(GameInput.Fingers == 1 && OneFingerTaken != null && OneFingerTaken()))
                {
                    TakeControl();
                    var scale = distance / Mathf.Max(1, Screen.height) * 1.2f;
                    Pan(-drag.x * scale, -drag.y * scale);
                }
                if (GameInput.Pinch != 1)
                {
                    TakeControl();
                    desiredDistance = Mathf.Clamp(desiredDistance / GameInput.Pinch, MinDistance, MaxDistance);
                }
                if (GameInput.Twist != 0) Rotate(-GameInput.Twist);
                if (GameInput.Busy) TakeControl();
            }
            Step(dt);
        }

        /// <summary>Closer for an action (metres away, never further than the player had it); RestoreZoom goes back.</summary>
        public void CinematicZoom(float metres)
        {
            if (Overhead || Still) return;
            if (savedDistance == null) savedDistance = desiredDistance;
            desiredDistance = Mathf.Clamp(Mathf.Min(metres, savedDistance.Value), MinDistance, MaxDistance);
            zoomSpring = true;
            zoomStiffness = Spring;
        }

        /// <summary>
        /// Back far enough for all of `points` to be in view round `centre` (the dead walking all at once: Ana, Arturo 31/10
        /// #4), within the share of the screen `room` leaves clear of the HUD; never nearer than the player had it, never
        /// past the farthest. RestoreZoom goes back.
        /// </summary>
        public void FrameZoom(Vector3 centre, System.Collections.Generic.IList<Vector3> points, float room = 0.72f, bool closer = false)
        {
            if (Overhead || Still) return;
            if (savedDistance == null) savedDistance = desiredDistance;
            // The camera's axes at the turn it is going to and the rig's pitch (Place).
            var rad = desiredYaw * Mathf.Deg2Rad;
            var pitch = Pitch * Mathf.Deg2Rad;
            var forward = -new Vector3(Mathf.Sin(rad) * Mathf.Cos(pitch), Mathf.Sin(pitch), -Mathf.Cos(rad) * Mathf.Cos(pitch));
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var up = Vector3.Cross(forward, right);
            var tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * room;
            var tanH = tanV * cam.aspect;
            // Each point inside the frame from `need` metres back: its sideways and upwards offsets within the view's slope
            // at its depth.
            var need = 0f;
            foreach (var p in points)
            {
                var r = p - centre;
                var depth = Vector3.Dot(r, forward);
                need = Mathf.Max(need, Mathf.Abs(Vector3.Dot(r, right)) / tanH - depth, Mathf.Abs(Vector3.Dot(r, up)) / tanV - depth);
            }
            // `closer`, an attack framed (Arturo, 14/11: «Acercar la cámara para encuadrar el ataque»): nearer than the player
            // had it too, down to CloserFrame, never so near that one of them is out.
            desiredDistance = Mathf.Clamp(Mathf.Max(closer ? Mathf.Min(savedDistance.Value, CloserFrame) : savedDistance.Value, need), MinDistance, MaxDistance);
            zoomSpring = true;
            zoomStiffness = FrameSpring;
        }

        public void RestoreZoom()
        {
            if (Overhead || Still || savedDistance == null) return;
            desiredDistance = savedDistance.Value;
            savedDistance = null;
            zoomSpring = true;
            zoomStiffness = Spring;
        }

        /// <summary>A shake of `strength` metres for `seconds` (an explosion, a heavy gun).</summary>
        public void Shake(float strength, float seconds)
        {
            if (Still) return;
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeTime = Mathf.Max(shakeTime, seconds);
        }

        /// <summary>
        /// The shot over the shoulder (Arturo, 20/10 #11): from `pos` looking at `look` (ShotCam.OverShoulder). The rig's own
        /// view stays where it was, and EndCloseShot (or the player taking the camera) goes back to it; from one such shot
        /// to another it moves, not cuts.
        /// </summary>
        public void CloseShot(Vector3 pos, Vector3 look)
        {
            if (Overhead || Still) return;
            closeFrom = close != null && closeness > 0.05f ? ShownClose() : ((Vector3, Vector3)?)null;
            swap = closeFrom != null ? 0 : 1;
            close = (pos, look);
            closeOn = true;
        }

        public void EndCloseShot() => closeOn = false;

        /// <summary>Whether it is in the shot over a shoulder, or on its way into it.</summary>
        public bool CloseUp => closeOn;

        /// <summary>Whether it is out of its own view at all: in the shot over a shoulder, on its way into it or back.</summary>
        public bool Away => closeOn || closeness > 0;

        (Vector3 pos, Vector3 look) ShownClose()
        {
            var to = close.Value;
            if (closeFrom == null || swap >= 1) return to;
            var k = Smoother(swap);
            return (Vector3.Lerp(closeFrom.Value.pos, to.pos, k), Vector3.Lerp(closeFrom.Value.look, to.look, k));
        }

        static float Smoother(float t) => t * t * t * (t * (t * 6 - 15) + 10);

        /// <summary>
        /// From above (U17, Arturo 18/11: «debería poder hacerse zoom en el mapa», the combat's map, M): the wheel, or two
        /// fingers' pinch, closer or further between overNear and overFar, the ground under the pointer kept under it; with
        /// Shift or Alt the wheel is the floor's (the controller's), over the HUD the HUD's. The right button dragged, a
        /// finger dragged, or the keys move it round, within the map. Straight down all the while: the map, not the game's
        /// view.
        /// </summary>
        void OverheadInput(float dt)
        {
            if (Held != null && Held()) return;
            var keys = !GameInput.SystemHeld && GameInput.HasKeys;
            // The ground a pixel spans up there (straight down): the drags move it under the pointer, one for one.
            var perPixel = 2 * distance * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            if (GameInput.HasPointer && !GameInput.Touch)
            {
                var at = GameInput.Point;
                var wheel = GameInput.Wheel.y;
                if (wheel != 0 && (!keys || !GameInput.Shift && !GameInput.Alt) && (OverHud == null || !OverHud(at)))
                    OverheadZoom(1 - Mathf.Clamp(wheel * WheelZoom, -0.45f, 0.45f), at);
                if (RightDrag && GameInput.Pressed(GameInput.Button.Secondary))
                {
                    dragging = true;
                    dragFrom = at;
                }
                if (!GameInput.Held(GameInput.Button.Secondary)) dragging = false;
                if (dragging && at != dragFrom)
                {
                    Pan(-(at.x - dragFrom.x) * perPixel, -(at.y - dragFrom.y) * perPixel);
                    dragFrom = at;
                }
            }
            if (keys)
            {
                var speed = 24f * dt * (distance / 36f);
                var forward = (GameInput.Held(GameInput.Key.Forward) ? speed : 0) - (GameInput.Held(GameInput.Key.Backward) ? speed : 0);
                var right = (GameInput.Held(GameInput.Key.Right) ? speed : 0) - (GameInput.Held(GameInput.Key.Left) ? speed : 0);
                if (right != 0 || forward != 0) Pan(right, forward);
            }
            if (GameInput.Touch)
            {
                if (GameInput.Drag != Vector2.zero) Pan(-GameInput.Drag.x * perPixel, -GameInput.Drag.y * perPixel);
                if (GameInput.Pinch != 1) OverheadZoom(1 / GameInput.Pinch, null);
            }
        }

        /// <summary>From above, `factor` times as far (within overNear and overFar), the ground under `at` (screen pixels) kept there.</summary>
        void OverheadZoom(float factor, Vector2? at)
        {
            var before = desiredDistance;
            var after = Mathf.Clamp(before * factor, overNear, overFar);
            if (Mathf.Abs(after - before) < 1e-3f) return;
            // The point under the pointer on the ground, and the middle drawn towards it (or away) by the same share.
            if (at is Vector2 p && Camera != null)
            {
                var ray = Camera.ScreenPointToRay(p);
                if (Mathf.Abs(ray.direction.y) > 1e-4f)
                {
                    var g = ray.origin + ray.direction * ((desiredTarget.y - ray.origin.y) / ray.direction.y);
                    desiredTarget = Clamp(g + (desiredTarget - g) * (after / before));
                }
            }
            desiredDistance = after;
        }

        void Pan(float right, float forward)
        {
            // Forward is away from the camera, along the ground.
            var rad = yaw * Mathf.Deg2Rad;
            var fwd = new Vector3(-Mathf.Sin(rad), 0, Mathf.Cos(rad));
            var rgt = new Vector3(fwd.z, 0, -fwd.x);
            desiredTarget = Clamp(desiredTarget + rgt * right + fwd * forward);
        }

        void Step(float dt)
        {
            if (HeldAt != null && !Overhead)
            {
                distance = desiredDistance = HeldAt.Value;
                savedDistance = null;
                closeOn = false;
            }
            var k = 1 - Mathf.Exp(-dt * 7);
            if (springing)
            {
                // In small steps, so it stays steady at a low frame rate.
                var steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 120f)));
                var h = dt / steps;
                for (var i = 0; i < steps; i++)
                {
                    var d = (desiredTarget - target) * (Spring * Spring) - targetVel * (2 * Spring);
                    targetVel += d * h;
                    target += targetVel * h;
                }
                if (Logging && tripOn && !tripNear && (target - desiredTarget).magnitude < 1.5f)
                {
                    tripNear = true;
                    Debug.Log($"[cámara] viaje de {tripLength:0.0} m: a una casilla en {Time.unscaledTime - tripStart:0.00} s");
                }
                if ((target - desiredTarget).sqrMagnitude < 2.5e-5f && targetVel.sqrMagnitude < 1e-4f)
                {
                    target = desiredTarget;
                    targetVel = Vector3.zero;
                    springing = false;
                    if (Logging && tripOn) Debug.Log($"[cámara] viaje de {tripLength:0.0} m: parada en {Time.unscaledTime - tripStart:0.00} s");
                    tripOn = false;
                }
            }
            else target = Vector3.Lerp(target, desiredTarget, k);
            yaw += (desiredYaw - yaw) * k;
            pitch += ((Overhead ? OverheadPitch : Pitch) - pitch) * k;
            if (zoomSpring)
            {
                // The point's spring (critically damped, in small steps), on the distance.
                var steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 120f)));
                var h = dt / steps;
                for (var i = 0; i < steps; i++)
                {
                    distanceVel += ((desiredDistance - distance) * (zoomStiffness * zoomStiffness) - distanceVel * (2 * zoomStiffness)) * h;
                    distance += distanceVel * h;
                }
                if (Mathf.Abs(desiredDistance - distance) < 0.02f && Mathf.Abs(distanceVel) < 0.05f)
                {
                    distance = desiredDistance;
                    distanceVel = 0;
                    zoomSpring = false;
                }
            }
            else distance += (desiredDistance - distance) * (1 - Mathf.Exp(-dt * WheelFollow));
            Place();
            // Into the shot over a shoulder, or back out of it: between the two views, eased at both ends.
            closeness = closeOn ? Mathf.Min(1, closeness + dt / CloseIn) : Mathf.Max(0, closeness - dt / CloseOut);
            if (swap < 1) swap = Mathf.Min(1, swap + dt / CloseSwap);
            var looked = target;
            if (close != null && (closeOn || closeness > 0))
            {
                // (The review of 10/11, A-01: in a straight line, the first tenth of the way out already took the view two or
                // three times as far and 30–40° round, and the rest hardly changed it: a jump, then a halt. Now the view turns
                // round what it looks at, and its distance to it changes by the same share each frame.)
                var w = Smoother(closeness);
                var shot = ShownClose();
                looked = Vector3.Lerp(target, shot.look, w);
                var own = transform.position - target;
                var over = shot.pos - shot.look;
                var dir = Vector3.Slerp(own.normalized, over.normalized, w);
                var reach = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(own.magnitude, 0.01f)), Mathf.Log(Mathf.Max(over.magnitude, 0.01f)), w));
                transform.position = looked + dir * reach;
                transform.LookAt(looked);
            }
            else
            {
                closeness = 0;
                close = null;
                closeFrom = null;
            }
            if (shakeTime > 0)
            {
                shakeTime = Mathf.Max(0, shakeTime - dt);
                var s = shakeStrength * Mathf.Min(1, shakeTime * 4);
                transform.position += new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f) * s;
                if (shakeTime == 0) shakeStrength = 0;
            }
            if (Logging)
            {
                var f = transform.forward;
                Debug.Log($"[camara] {Time.time:0.000} d={Vector3.Distance(transform.position, looked):0.00} fwd=({f.x:0.000},{f.y:0.000},{f.z:0.000}) close={closeness:0.00}");
            }
        }

        /// <summary>
        /// Where the camera stands for the point looked at: back from it at the pitch, round it by the yaw. Yaw 45° looks
        /// north-west from the south-east, as the web's (north +z here, −z there).
        /// </summary>
        void Place()
        {
            var rad = yaw * Mathf.Deg2Rad;
            var up = pitch * Mathf.Deg2Rad;
            var horizontal = Mathf.Cos(up) * distance;
            transform.position = new Vector3(target.x + Mathf.Sin(rad) * horizontal, target.y + Mathf.Sin(up) * distance, target.z - Mathf.Cos(rad) * horizontal);
            transform.LookAt(target);
        }

        Vector3 Clamp(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, Bounds.xMin, Bounds.xMax);
            p.z = Mathf.Clamp(p.z, Bounds.yMin, Bounds.yMax);
            return p;
        }
    }
}
