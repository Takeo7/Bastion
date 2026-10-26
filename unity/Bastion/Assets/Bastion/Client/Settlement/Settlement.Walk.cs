using System;
using System.Linq;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bastion.Client
{
    /// <summary>
    /// The Asentamiento walked about in first person (0.10.1's walkAbout.ts, playtest 22/10 #43: a test, to see whether
    /// it is liked): WASD or the arrows to walk, the mouse to look (the pointer held by the view), Shift to run, Esc to
    /// come out. The eyes at a person's height; what can't be stood on stops it (WalkBlocked: the pines, the buildings'
    /// walls, the defences, the clearing's edge; and La Lata), sliding along. UI's input keeps off while Walking; UI hides
    /// its panels and shows the keys' line on WalkingChanged.
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>Eyes this high (tiles: 1.6 m), a body this wide round them (0.45 m: no nose against a wall), and how fast it walks and runs (tiles a second: 1.4 and 3.5 m/s).</summary>
        const float WalkEyes = 1.07f, WalkRadius = 0.3f, WalkPace = 0.93f, RunPace = 2.3f;
        /// <summary>The view's field (degrees) while walking, and how far the mouse turns it (radians a pixel).</summary>
        const float WalkFov = 70, WalkTurn = 0.0022f;
        /// <summary>The head's bob as it walks: how high (tiles) and how many a tile walked.</summary>
        const float WalkBob = 0.02f, WalkBobRate = 2.2f;

        /// <summary>
        /// For the labs (no keyboard nor mouse in batchmode): what the keys and the mouse do while walking, each frame, in
        /// their place: ahead and to the right (−1 to 1), running, and the mouse's move (pixels, y up).
        /// </summary>
        public static Func<(float ahead, float side, bool run, Vector2 look)> WalkFake;

        /// <summary>Where it stands (tiles) and which way it looks (three's yaw and pitch, radians), while walking.</summary>
        public Vector2 WalkAt => walkAt;
        public float WalkYaw => walkYaw;

        float walkYaw, walkPitch, walkBob;
        Vector2 walkAt;
        bool walkOut;
        Rect? walkSolid;
        (Vector3 position, Quaternion rotation, float fov) walkWas;
        /// <summary>The clearing as its siege map has it (Show: ClearingState), what one walking about it can stand on.</summary>
        GameState ground;

        /// <summary>
        /// Walking about on (from the ground the camera looks at, facing as it looks) or off (the camera back as it was:
        /// UI's rig takes it on from there). Call `on` from a click (UI's «Andar por la base»); Esc comes out by itself.
        /// </summary>
        public void WalkAbout(bool on)
        {
            var camera = Camera;
            if (on == Walking || (on && (camera == null || Base == null || ground == null))) return;
            if (!on)
            {
                Walking = false;
                walkOut = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (camera != null)
                {
                    camera.transform.SetPositionAndRotation(walkWas.position, walkWas.rotation);
                    camera.fieldOfView = walkWas.fov;
                }
                WalkingChanged?.Invoke(false);
                return;
            }
            walkWas = (camera.transform.position, camera.transform.rotation, camera.fieldOfView);
            // From the ground it looks at (the rig's focus), facing as it looked (three's yaw: from −z, turning to −x).
            var look = camera.transform.forward;
            var from = camera.transform.position;
            if (look.y < -1e-3f) from += look * (-from.y / look.y);
            walkSolid = WalkSolid();
            var tile = Tiles(from);
            walkAt = WalkFree(tile.x, tile.z);
            walkYaw = Mathf.Atan2(-look.x, look.z);
            walkPitch = 0;
            walkBob = 0;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Walking = true;
            PlaceWalker(camera);
            WalkingChanged?.Invoke(true);
        }

        /// <summary>Every frame while walking: the keys walk it, the mouse turns the view; Esc, or another tab, out (at the frame's end: UI's Esc this frame still sees it walking).</summary>
        void StepWalkAbout(float dt)
        {
            if (!Walking) return;
            var camera = Camera;
            if (camera == null || view != Views.Facilities)
            {
                walkOut = true;
                return;
            }
            float ahead = 0, side = 0;
            bool run = false;
            var turn = Vector2.zero;
            if (WalkFake != null) (ahead, side, run, turn) = WalkFake();
            else if (Application.isFocused)
            {
                var keys = Keyboard.current;
                if (keys != null)
                {
                    if (keys.escapeKey.wasPressedThisFrame) walkOut = true;
                    ahead = (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0);
                    side = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0);
                    run = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed;
                }
                if (Mouse.current != null) turn = Mouse.current.delta.ReadValue();
            }
            walkYaw -= turn.x * WalkTurn;
            walkPitch = Mathf.Clamp(walkPitch + turn.y * WalkTurn, -1.3f, 1.3f);
            if (ahead != 0 || side != 0)
            {
                float speed = (run ? RunPace : WalkPace) * Mathf.Min(dt, 0.1f);
                // Ahead is where the view looks, along the ground (the camera's −z turned by yaw, in 0.10.1's tiles).
                float fx = -Mathf.Sin(walkYaw), fz = -Mathf.Cos(walkYaw);
                float n = Mathf.Sqrt(ahead * ahead + side * side);
                float dx = (fx * ahead - fz * side) / n * speed, dz = (fz * ahead + fx * side) / n * speed;
                // Each way apart, so it slides along what stops it.
                if (!WalkHits(walkAt.x + dx, walkAt.y)) walkAt.x += dx;
                if (!WalkHits(walkAt.x, walkAt.y + dz)) walkAt.y += dz;
                walkBob += Mathf.Sqrt(dx * dx + dz * dz) * WalkBobRate * Mathf.PI * 2;
            }
            PlaceWalker(camera);
        }

        /// <summary>The eyes where it stands, looking its way (three's Euler(pitch, yaw, 0, 'YXZ') on a camera down −z; 0.10.1's south is Unity's −z).</summary>
        void PlaceWalker(Camera camera)
        {
            camera.fieldOfView = WalkFov;
            camera.transform.position = World(new Vector3(walkAt.x, WalkEyes + Mathf.Sin(walkBob) * WalkBob, walkAt.y));
            float level = Mathf.Cos(walkPitch);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(walkYaw) * level, Mathf.Sin(walkPitch), Mathf.Cos(walkYaw) * level));
        }

        /// <summary>Out at the frame's end if it was asked to (StepWalkAbout).</summary>
        void EndWalk()
        {
            if (walkOut) WalkAbout(false);
        }

        /// <summary>
        /// Whether one walking about can't stand on tile x, y (clearingArt.ts blocked): off the clearing, a pine standing,
        /// a building's wall, a defence or a bed (a low one with nothing on it is the siege's alone, unseen: no), a raised
        /// deck; floors and doorways can be.
        /// </summary>
        bool WalkBlocked(int x, int y)
        {
            var s = ground;
            if (s == null || Base == null || x < 0 || y < 0 || x >= s.Width || y >= s.Height) return true;
            int i = y * s.Width + x;
            string kind = s.Tiles[i];
            if (i < s.Elev.Count && s.Elev[i] > 0) return true;
            if (kind == "low") return Bastion.Engine.Base.PieceAt(Base, x, y) != null || Bastion.Engine.Decor.DecorOf(s.Decor != null && i < s.Decor.Count ? s.Decor[i] : (int?)null) != null;
            return kind != "floor" && kind != "door" && kind != "doorOpen";
        }

        /// <summary>What else stops one walking about (clearingArt.ts solids): La Lata where it is parked, its box (x, z in tiles); none while it is away or driving.</summary>
        Rect? WalkSolid()
        {
            if (Lata == null || !Lata.Shown || away || Lata.Moving) return null;
            var drawn = Lata.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).Select(r => r.bounds).ToList();
            if (drawn.Count == 0) return null;
            var box = drawn.Aggregate((a, b) => { a.Encapsulate(b); return a; });
            Vector3 a0 = Tiles(box.min), a1 = Tiles(box.max);
            return Rect.MinMaxRect(Mathf.Min(a0.x, a1.x), Mathf.Min(a0.z, a1.z), Mathf.Max(a0.x, a1.x), Mathf.Max(a0.z, a1.z));
        }

        /// <summary>Whether a body standing at x, z (tiles) would be in something it can't stand on.</summary>
        bool WalkHits(float x, float z)
        {
            bool Inside(float nx, float nz) => (nx - x) * (nx - x) + (nz - z) * (nz - z) < WalkRadius * WalkRadius;
            for (int ty = Mathf.FloorToInt(z - WalkRadius); ty <= Mathf.FloorToInt(z + WalkRadius); ty++)
                for (int tx = Mathf.FloorToInt(x - WalkRadius); tx <= Mathf.FloorToInt(x + WalkRadius); tx++)
                {
                    if (!WalkBlocked(tx, ty)) continue;
                    // The tile's nearest point to the body's middle, inside the body?
                    if (Inside(Mathf.Clamp(x, tx, tx + 1), Mathf.Clamp(z, ty, ty + 1))) return true;
                }
            return walkSolid is Rect b && Inside(Mathf.Clamp(x, b.xMin, b.xMax), Mathf.Clamp(z, b.yMin, b.yMax));
        }

        /// <summary>The middle of the nearest tile it can stand on to x, z (tiles), out to a few tiles; where it was asked if none.</summary>
        Vector2 WalkFree(float x, float z)
        {
            int cx = Mathf.FloorToInt(x), cz = Mathf.FloorToInt(z);
            for (int r = 0; r < 8; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r || WalkHits(cx + dx + 0.5f, cz + dz + 0.5f)) continue;
                        return new Vector2(cx + dx + 0.5f, cz + dz + 0.5f);
                    }
            return new Vector2(x, z);
        }
    }
}
