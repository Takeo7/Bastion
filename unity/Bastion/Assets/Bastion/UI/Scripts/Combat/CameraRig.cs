using UnityEngine;
using UnityEngine.InputSystem;

namespace Bastion.UI.Combat
{
    /// <summary>
    /// The tactical camera (camera.ts of the 0.10.1, UI's): a fixed pitch of 52°, the yaw in quarter turns (Q/E), WASD or
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
        const float Pitch = 52f;
        const float MinDistance = 9f * 1.5f;
        const float MaxDistance = 42f * 1.5f;
        const float Spring = 5.5f;
        /// <summary>Into the shot over the shoulder, out of it, and from one to the next (s).</summary>
        const float CloseIn = 0.8f;
        const float CloseOut = 0.9f;
        const float CloseSwap = 0.7f;

        /// <summary>Raised by every move of the player's: a move sent while it was another number lets go.</summary>
        public int Epoch { get; private set; }

        /// <summary>The map's floor plan (metres, x and z), which the point looked at stays in.</summary>
        public Rect Bounds = new Rect(0, -96, 96, 96);

        Vector3 target;
        Vector3 desiredTarget;
        Vector3 targetVel;
        bool springing;
        float yaw = 45f;
        float desiredYaw = 45f;
        float distance = 24f * 1.5f;
        float desiredDistance = 24f * 1.5f;
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

        /// <summary>The quarter turn it faces: 0–3 (camera.ts quadrant), for what is drawn by it.</summary>
        public int Quadrant => ((Mathf.RoundToInt((desiredYaw - 45f) / 90f) % 4) + 4) % 4;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.fieldOfView = 40f;
        }

        /// <summary>To `p` on the spring (or at once); it lets go if the player moves the camera meanwhile.</summary>
        public void Focus(Vector3 p, bool instant = false)
        {
            desiredTarget = Clamp(new Vector3(p.x, Mathf.Max(0, p.y), p.z));
            springing = !instant;
            if (!instant) return;
            target = desiredTarget;
            targetVel = Vector3.zero;
            Place();
        }

        /// <summary>A quarter turn left (−1) or right (1).</summary>
        public void Rotate(int dir)
        {
            TakeControl();
            desiredYaw += dir * 90f;
        }

        /// <summary>The player took the camera (a move, a pick): whatever was taking it somewhere lets go.</summary>
        public void TakeControl()
        {
            Epoch++;
            TakenAt = Time.unscaledTime;
            savedDistance = null;
            closeOn = false;
            springing = false;
        }

        /// <summary>The game's menu is up: the keys and the pointer are its own, the camera only finishes its moves.</summary>
        public System.Func<bool> Held;

        /// <summary>Whether a screen point is over the HUD: the wheel scrolls the log or a list there, not the camera.</summary>
        public System.Func<Vector2, bool> OverHud;

        /// <summary>The right button held drags the ground; off on the clearing (playtest 20/10 #6: «solo WASD»), where a right click lets go of what is in hand.</summary>
        public bool RightDrag = true;

        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var held = Held != null && Held();
            var keys = held ? null : Keyboard.current;
            var mouse = held ? null : Mouse.current;
            if (keys != null)
            {
                if (keys.qKey.wasPressedThisFrame) Rotate(-1);
                if (keys.eKey.wasPressedThisFrame) Rotate(1);
                var speed = 24f * dt * (distance / 36f);
                var forward = 0f;
                var right = 0f;
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) forward += speed;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) forward -= speed;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) right += speed;
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) right -= speed;
                if (right != 0 || forward != 0)
                {
                    TakeControl();
                    Pan(right, forward);
                }
            }
            if (mouse != null)
            {
                // With Shift the wheel changes floor (the controller's), and over the HUD it is the HUD's.
                var wheel = mouse.scroll.ReadValue().y;
                if (wheel != 0 && (keys == null || !keys.shiftKey.isPressed) && (OverHud == null || !OverHud(mouse.position.ReadValue())))
                {
                    TakeControl();
                    desiredDistance = Mathf.Clamp(desiredDistance * (1 - wheel * 0.001f), MinDistance, MaxDistance);
                }
                // The right button held drags the ground under it.
                var at = mouse.position.ReadValue();
                if (RightDrag && mouse.rightButton.wasPressedThisFrame)
                {
                    dragging = true;
                    dragFrom = at;
                }
                if (!mouse.rightButton.isPressed) dragging = false;
                if (dragging && at != dragFrom)
                {
                    TakeControl();
                    var scale = distance / Mathf.Max(1, Screen.height) * 1.2f;
                    Pan(-(at.x - dragFrom.x) * scale, -(at.y - dragFrom.y) * scale);
                    dragFrom = at;
                }
            }
            Step(dt);
        }

        /// <summary>Closer for an action (metres away, never further than the player had it); RestoreZoom goes back.</summary>
        public void CinematicZoom(float metres)
        {
            if (savedDistance == null) savedDistance = desiredDistance;
            desiredDistance = Mathf.Clamp(Mathf.Min(metres, savedDistance.Value), MinDistance, MaxDistance);
        }

        public void RestoreZoom()
        {
            if (savedDistance == null) return;
            desiredDistance = savedDistance.Value;
            savedDistance = null;
        }

        /// <summary>A shake of `strength` metres for `seconds` (an explosion, a heavy gun).</summary>
        public void Shake(float strength, float seconds)
        {
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
            closeFrom = close != null && closeness > 0.05f ? ShownClose() : ((Vector3, Vector3)?)null;
            swap = closeFrom != null ? 0 : 1;
            close = (pos, look);
            closeOn = true;
        }

        public void EndCloseShot() => closeOn = false;

        /// <summary>Whether it is in the shot over a shoulder, or on its way into it.</summary>
        public bool CloseUp => closeOn;

        (Vector3 pos, Vector3 look) ShownClose()
        {
            var to = close.Value;
            if (closeFrom == null || swap >= 1) return to;
            var k = Smoother(swap);
            return (Vector3.Lerp(closeFrom.Value.pos, to.pos, k), Vector3.Lerp(closeFrom.Value.look, to.look, k));
        }

        static float Smoother(float t) => t * t * t * (t * (t * 6 - 15) + 10);

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
                if ((target - desiredTarget).sqrMagnitude < 2.5e-5f && targetVel.sqrMagnitude < 1e-4f)
                {
                    target = desiredTarget;
                    targetVel = Vector3.zero;
                    springing = false;
                }
            }
            else target = Vector3.Lerp(target, desiredTarget, k);
            yaw += (desiredYaw - yaw) * k;
            distance += (desiredDistance - distance) * k;
            Place();
            // Into the shot over a shoulder, or back out of it: between the two views, eased at both ends.
            closeness = closeOn ? Mathf.Min(1, closeness + dt / CloseIn) : Mathf.Max(0, closeness - dt / CloseOut);
            if (swap < 1) swap = Mathf.Min(1, swap + dt / CloseSwap);
            if (close != null && (closeOn || closeness > 0))
            {
                var w = Smoother(closeness);
                var shot = ShownClose();
                transform.position = Vector3.Lerp(transform.position, shot.pos, w);
                transform.LookAt(Vector3.Lerp(target, shot.look, w));
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
        }

        /// <summary>
        /// Where the camera stands for the point looked at: back from it at the pitch, round it by the yaw. Yaw 45° looks
        /// north-west from the south-east, as the web's (north +z here, −z there).
        /// </summary>
        void Place()
        {
            var rad = yaw * Mathf.Deg2Rad;
            var pitch = Pitch * Mathf.Deg2Rad;
            var horizontal = Mathf.Cos(pitch) * distance;
            transform.position = new Vector3(target.x + Mathf.Sin(rad) * horizontal, target.y + Mathf.Sin(pitch) * distance, target.z - Mathf.Cos(rad) * horizontal);
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
