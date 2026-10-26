using System;
using System.Linq;
using Bastion.Client.Units;
using UnityEditor;
using UnityEngine;

namespace Bastion.EditorTools
{
    /// <summary>
    /// What a clip of the figures' catalog drives (a test): for each of BASTION_CLIPS (ids, comma-separated), its length,
    /// how many humanoid muscle curves it has, and those of each arm (a muscle with no curve stands at 0: the arm out level
    /// to the side, as in a T-pose), and whether it is humanoid.
    ///   BASTION_CLIPS=drinkInMasc,armsFoldedMasc unity/tools/unity.sh -executeMethod Bastion.EditorTools.ClipProbe.Run -quit
    /// </summary>
    public static class ClipProbe
    {
        /// <summary>
        /// One soldier (BASTION_SOLDADO's seed, «lab» unless said; male, BASTION_SEXO=f female) on a grey floor, seen from the
        /// front at chest height, playing BASTION_CLIPS one after the other over the whole body, each from its start, as the
        /// camp plays its moves (FigureAnimator.PlayWholePart, held at its end), nothing else under it but the plain idle. In
        /// play mode. BASTION_ANCHO=1: how far its body (as skinned) reaches to either side of its feet, every half second (the
        /// one leant on La Lata: its side against the bus).
        ///   BASTION_CLIPS=drinkInMasc,drinkMasc,sipMasc unity/tools/captura.sh -shotOut tira -shotSetup Bastion.EditorTools.ClipProbe.Alone -shotCamera "Cámara del clip" -shotPlay -shotFrames 60 -shotFps 10
        /// </summary>
        public static void Alone()
        {
            Bastion.Client.Mood.Show(Bastion.Client.Mood.Day);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 2;
            var cam = new GameObject("Cámara del clip").AddComponent<Camera>();
            cam.transform.position = new Vector3(0.6f, 1.35f, 2.6f);
            cam.transform.LookAt(new Vector3(0, 1.1f, 0));
            cam.fieldOfView = 40;
            var ids = (Environment.GetEnvironmentVariable("BASTION_CLIPS") ?? "").Split(',').Where(k => k.Length > 0).ToList();
            void Go(PlayModeStateChange change)
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                EditorApplication.playModeStateChanged -= Go;
                var catalog = FigureCatalog.Load();
                string sex = Environment.GetEnvironmentVariable("BASTION_SEXO") == "f" ? "f" : "m", build = sex == "f" ? "femn" : "masc";
                var dress = Dress.Of(Bastion.Engine.Appearances.DefaultAppearance(0, "#e8a33a", sex), Environment.GetEnvironmentVariable("BASTION_SOLDADO") ?? "lab");
                bool widths = Environment.GetEnvironmentVariable("BASTION_ANCHO") == "1";
                var baked = new Mesh();
                float nextWidth = 0;
                var figure = SoldierFigure.Make(dress, "clip", true);
                figure.transform.localScale = Vector3.one * Figures.CharacterScale;
                var animator = figure.GetComponentInChildren<Animator>();
                var anim = new FigureAnimator(animator, m => catalog.Clip(Anims.Motion(build, m, false)), null, m => false);
                int next = 0, frame = -1;
                float at = 0, until = 0.3f;
                void Tick()
                {
                    if (!EditorApplication.isPlaying)
                    {
                        EditorApplication.update -= Tick;
                        return;
                    }
                    // (The editor's updates come more often than the game's frames.)
                    if (Time.frameCount == frame) return;
                    frame = Time.frameCount;
                    float dt = Time.deltaTime;
                    at += dt;
                    if (at >= until && next < ids.Count)
                    {
                        var clip = catalog.Clip(ids[next]);
                        Debug.Log($"[clip] {ids[next]} from {at:0.0} s ({clip?.length:0.00} s)");
                        if (clip != null) anim.PlayWholePart(clip, 0, clip.length, 0.25f, false);
                        until = at + (clip != null ? clip.length : 0);
                        next++;
                    }
                    anim.Step(dt);
                    if (widths && at >= nextWidth)
                    {
                        nextWidth = at + 0.5f;
                        // Its skin as posed, in its own frame (x to its right), from the waist up and from the hips down.
                        float right = float.MinValue, left = float.MaxValue, rightLow = float.MinValue, leftLow = float.MaxValue;
                        var root = figure.transform;
                        foreach (var skin in figure.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            skin.BakeMesh(baked, true);
                            var m = skin.transform.localToWorldMatrix;
                            foreach (var v in baked.vertices)
                            {
                                var w = m.MultiplyPoint3x4(v);
                                var local = root.InverseTransformPoint(w);
                                float x = local.x * root.lossyScale.x;
                                if (w.y > 1.0f) { right = Mathf.Max(right, x); left = Mathf.Min(left, x); }
                                else if (w.y > 0.2f) { rightLow = Mathf.Max(rightLow, x); leftLow = Mathf.Min(leftLow, x); }
                            }
                        }
                        var arm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                        var armL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                        float ax = root.InverseTransformPoint(arm.position).x * root.lossyScale.x, bx = root.InverseTransformPoint(armL.position).x * root.lossyScale.x;
                        Debug.Log(FormattableString.Invariant($"[ancho] {build} {(next > 0 ? ids[next - 1] : "-")} {at:0.0} s: to its right {right:0.000} m (below the waist {rightLow:0.000}), to its left {-left:0.000} m ({-leftLow:0.000}); upper arms {ax:0.000} / {-bx:0.000}"));
                    }
                }
                EditorApplication.update += Tick;
            }
            EditorApplication.playModeStateChanged += Go;
        }

        public static void Run()
        {
            var catalog = FigureCatalog.Load();
            foreach (string id in (Environment.GetEnvironmentVariable("BASTION_CLIPS") ?? "").Split(',').Where(k => k.Length > 0))
            {
                var clip = catalog?.Clip(id);
                if (clip == null)
                {
                    Debug.Log($"[clip] {id}: none");
                    continue;
                }
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var muscles = bindings.Where(b => b.type == typeof(Animator)).Select(b => b.propertyName).ToList();
                string Arm(string side) => string.Join(" ", muscles.Where(m => m.StartsWith(side + " Arm") || m.StartsWith(side + " Forearm") || m.StartsWith(side + " Shoulder") || m.StartsWith(side + "Hand") || m.StartsWith(side + " Hand")).Select(m => m.Replace(side + " ", "")));
                Debug.Log($"[clip] {id}: {clip.length:0.00} s, humanoid {clip.humanMotion}, {muscles.Count} muscle curves, {bindings.Length} in all; right arm: [{Arm("Right")}]; left arm: [{Arm("Left")}]");
            }
        }
    }
}
