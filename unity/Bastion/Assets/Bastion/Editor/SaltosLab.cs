// M11 (Arturo, 23/11: «hay veces que al correr o moverme da algún salto de animación el personaje o el modelo»).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// A watch for the figures' jumps (M11), over whatever is played (UI's play lab, the game's own flow with its bots): every
    /// frame, each figure's root, its turn, and its hips, a foot and its right hand in the root's frame; a line [salto] for
    /// each frame in which one of them moves more than its speed allows, and which kind it is:
    ///   figura   the root's place or turn at a stroke (the step from tile to tile, the end of a way, snapping into cover)
    ///   pose     the body within the root at a stroke (a clip changed with no blend)
    ///   largo    a long frame by the clock on the wall: whatever moved in it looks a jump, the animation being right
    ///   camara   the camera's place or turn at a stroke
    /// At the end (and every BASTION_SALTOS_CADA seconds, 30) the count of each kind for each figure, the frames watched and
    /// how far the figures went in all, so that «so many jumps in so many metres» can be said.
    ///   BASTION_SALTOS_LAB=PartidaMochila … -shotSetup Bastion.EditorTools.SaltosLab.Saltos -shotPlay …   (UI's lab named, beside any -shotScene)
    /// The game's own flow from «Inicio», played by Red's bots (BASTION_MEDIR, BASTION_CAMPANA), its scenes changing:
    ///   … -shotScene Assets/Bastion/UI/Scenes/Inicio.unity -shotCamera "Cámara de la medida" -shotSetup Bastion.EditorTools.SaltosLab.App -shotPlay …
    /// </summary>
    public static class SaltosLab
    {
        public static void Saltos()
        {
            // (BASTION_SALTOS_LAB: UI's lab to watch, a method of HudShots, as RendimientoLab.Parones takes its own.)
            if (Environment.GetEnvironmentVariable("BASTION_SALTOS_LAB") is string lab && lab != "")
                Type.GetType("Bastion.UI.EditorTools.HudShots, Bastion.UI.Editor")?.GetMethod(lab)?.Invoke(null, null);
            UnityEditor.EditorApplication.playModeStateChanged += change =>
            {
                if (change != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
                var o = new GameObject("Saltos") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(o);
                o.AddComponent<Watch>();
            };
        }

        /// <summary>Saltos over the app's own flow: with a camera of Shots' own that outlives the scenes and draws nothing (as RendimientoLab.Parones').</summary>
        public static void App()
        {
            var own = new GameObject("Cámara de la medida");
            var camera = own.AddComponent<Camera>();
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            UnityEditor.EditorApplication.playModeStateChanged += change =>
            {
                if (change == UnityEditor.PlayModeStateChange.EnteredPlayMode) UnityEngine.Object.DontDestroyOnLoad(own);
            };
            Saltos();
        }

        sealed class Seen
        {
            public Vector3 at, hips, foot, hand;
            public float yaw;
            public int frame;
            public float gone;
            public readonly Dictionary<string, int> jumps = new Dictionary<string, int>();
            /// <summary>Where its root was in the frames before (the last Trail of them), for a jump's line: how it came to it.</summary>
            public List<Vector3> trail = new List<Vector3>();
        }

        sealed class Watch : MonoBehaviour
        {
            // What a figure may do in a frame of `dt` seconds before it is a jump. A run is some 4.5 m/s, a fall from two
            // storeys ends at 9; a body turns at most a turn a second walking; a hand in a blow, 10 m/s within the body.
            const float RootSpeed = 11, RootLeast = 0.3f, TurnSpeed = 540, TurnLeast = 30, BodySpeed = 5, HandSpeed = 12, BodyLeast = 0.22f;
            const int Trail = 45;
            const float CameraSpeed = 60, CameraLeast = 1.5f, CameraTurn = 25;

            readonly Dictionary<Animator, Seen> seen = new Dictionary<Animator, Seen>();
            readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            readonly Dictionary<string, int> all = new Dictionary<string, int>();
            Animator[] figures = Array.Empty<Animator>();
            double last = -1;
            int frames;
            float metres, nextSummary;
            Vector3 cameraAt;
            Quaternion cameraTurn;
            Camera cameraSeen;
            // (BASTION_SALTOS_LARGO: from how many ms by the wall's clock a frame is a long one, 80.)
            readonly float LongFrame = float.TryParse(Environment.GetEnvironmentVariable("BASTION_SALTOS_LARGO"), NumberStyles.Float, CultureInfo.InvariantCulture, out float l) ? l / 1000 : 0.08f;
            readonly float every = float.TryParse(Environment.GetEnvironmentVariable("BASTION_SALTOS_CADA"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) ? s : 30;

            static string F(float f) => f.ToString("0.00", CultureInfo.InvariantCulture);

            void Note(string kind, string who, string what)
            {
                all[kind] = all.TryGetValue(kind, out int n) ? n + 1 : 1;
                Debug.Log($"[salto] {kind} {who} t={F(Time.time)} frame={Time.frameCount} dt={F(Time.deltaTime * 1000)} ms: {what}");
            }

            // After the animators and the figures' own LateUpdate have placed them.
            void LateUpdate()
            {
                frames++;
                double now = clock.Elapsed.TotalSeconds;
                float wall = last >= 0 ? (float)(now - last) : 0;
                last = now;
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                bool longFrame = wall >= LongFrame;
                if (longFrame) Note("largo", "-", $"{F(wall * 1000)} ms by the wall's clock, scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                if (frames % 30 == 1) figures = FindObjectsByType<Animator>(FindObjectsSortMode.None).Where(a => a.isHuman).ToArray();
                foreach (var a in figures)
                {
                    if (a == null || !a.gameObject.activeInHierarchy) { if (a != null) seen.Remove(a); continue; }
                    var root = a.transform;
                    var hips = a.GetBoneTransform(HumanBodyBones.Hips);
                    var foot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var hand = a.GetBoneTransform(HumanBodyBones.RightHand);
                    if (hips == null || foot == null || hand == null) continue;
                    var here = new Seen { at = root.position, yaw = root.eulerAngles.y, hips = root.InverseTransformPoint(hips.position), foot = root.InverseTransformPoint(foot.position), hand = root.InverseTransformPoint(hand.position), frame = Time.frameCount };
                    if (seen.TryGetValue(a, out var was) && was.frame == Time.frameCount - 1)
                    {
                        here.trail = was.trail;
                        here.trail.Add(was.at);
                        if (here.trail.Count > Trail) here.trail.RemoveAt(0);
                        here.gone = was.gone;
                        foreach (var kv in was.jumps) here.jumps[kv.Key] = kv.Value;
                        string who = Name(a);
                        float moved = Vector3.Distance(here.at, was.at), turned = Mathf.Abs(Mathf.DeltaAngle(here.yaw, was.yaw));
                        here.gone += moved;
                        metres += moved;
                        void Jump(string kind, string what)
                        {
                            // (In a long frame everything moves far: that frame's are its own kind, said once above.)
                            if (longFrame) return;
                            here.jumps[kind] = here.jumps.TryGetValue(kind, out int n) ? n + 1 : 1;
                            Note(kind, who, what + Clips(a));
                        }
                        if (moved > Mathf.Max(RootLeast, RootSpeed * dt)) Jump("figura", $"its root {F(moved)} m ({F(moved / dt)} m/s) from {was.at} to {here.at}" + (moved > 1 ? "; before, a frame each: " + string.Join(" ", here.trail.Select(p => $"{F(p.x)},{F(p.z)}")) : ""));
                        else if (turned > Mathf.Max(TurnLeast, TurnSpeed * dt)) Jump("figura", $"its root turned {F(turned)}° ({F(turned / dt)}°/s)");
                        float h = Vector3.Distance(here.hips, was.hips), f = Vector3.Distance(here.foot, was.foot), g = Vector3.Distance(here.hand, was.hand);
                        if (h > Mathf.Max(BodyLeast, BodySpeed * dt)) Jump("pose", $"its hips {F(h)} m within the body, by {here.hips - was.hips} (its own right, up, ahead), the root at {here.at}");
                        else if (f > Mathf.Max(BodyLeast * 1.5f, BodySpeed * 2 * dt)) Jump("pose", $"its left foot {F(f)} m within the body, by {here.foot - was.foot} (its own right, up, ahead), the root at {here.at}");
                        else if (g > Mathf.Max(BodyLeast * 2, HandSpeed * dt)) Jump("pose", $"its right hand {F(g)} m within the body, by {here.hand - was.hand} (its own right, up, ahead), the root at {here.at}");
                    }
                    seen[a] = here;
                }
                var camera = Camera.main;
                if (camera != null)
                {
                    if (camera == cameraSeen && !longFrame)
                    {
                        float moved = Vector3.Distance(camera.transform.position, cameraAt), turned = Quaternion.Angle(camera.transform.rotation, cameraTurn);
                        if (moved > Mathf.Max(CameraLeast, CameraSpeed * dt)) Note("camara", camera.name, $"{F(moved)} m in a frame");
                        else if (turned > CameraTurn) Note("camara", camera.name, $"turned {F(turned)}° in a frame");
                    }
                    cameraSeen = camera;
                    cameraAt = camera.transform.position;
                    cameraTurn = camera.transform.rotation;
                }
                if (Time.time >= nextSummary)
                {
                    nextSummary = Time.time + every;
                    Summary();
                }
            }

            /// <summary>A figure by its unit: the nearest parent named as a unit's root is, else its own name.</summary>
            static string Name(Animator a)
            {
                var t = a.transform;
                while (t.parent != null && t.parent.name != "Unidades" && t.parent.name != "La fila") t = t.parent;
                return t.name;
            }

            /// <summary>The clips playing on its controller's first layer, if it has one (the figures' own graph has none: nothing said).</summary>
            static string Clips(Animator a)
            {
                if (a.runtimeAnimatorController == null) return "";
                var playing = a.GetCurrentAnimatorClipInfo(0);
                return playing.Length == 0 ? "" : "; clips " + string.Join("+", playing.Select(c => $"{c.clip.name}:{F(c.weight)}"));
            }

            void Summary()
            {
                Debug.Log($"[salto] so far: {frames} frames, {figures.Length} figures now, the figures {F(metres)} m in all; " + (all.Count == 0 ? "no jumps" : string.Join(", ", all.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}"))));
            }

            void OnDestroy() => Summary();
        }
    }
}
