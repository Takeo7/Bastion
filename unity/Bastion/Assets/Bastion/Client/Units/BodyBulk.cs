using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// A figure's body as bulk.ts measured it (PistolSpread's measure, for more of the body): the hips, each thigh and the
    /// trunk, each part slice by slice along its bone and sector by sector round it, from its mesh as bound (its build's
    /// blend shapes with it). What a carried weapon is kept off (the 3D review of 10/11, F-07, F-08: the knife at the belt
    /// and the pistol in its holster stood fixed, 10–16 cm into a heavy build, into a sweater tied at the waist, a coat;
    /// 0.10.1 pushed them out by a body's own measure, syntyFigures.ts outOfBody and BELT_ROOM). Measured once, as the
    /// figure is armed (UnitViews.Arm), in the pose it is made in.
    /// </summary>
    public sealed class BodyBulk
    {
        /// <summary>bulk.ts SECTORS, SLICE, OWN, TORSO_PURE (as PistolSpread's).</summary>
        const int Sectors = 16;
        const float Slice = 0.03f, Own = 0.5f, TorsoPure = 0.9f;

        sealed class Girth
        {
            public Transform bone;
            public Vector3 origin, axis, u, v;
            public int slices;
            public float[] radii;
        }

        readonly List<Girth> parts = new List<Girth>();
        /// <summary>The body's mesh units in the world (its renderer's scale): what the parts are measured in.</summary>
        float metre = 1;

        public bool Empty => parts.Count == 0;

        BodyBulk() { }

        /// <summary>
        /// BASTION_LOG_BULK=1 (the labs; Gameplay, 10/11: a city mission loaded 0.6 s slower in the 0.12.20 app): a line
        /// [bulk] for each body measured, how long it took and what came out (the girths' slices and the sum of their radii,
        /// to tell the same results), and the milliseconds and bodies so far.
        /// </summary>
        static readonly bool Log = System.Environment.GetEnvironmentVariable("BASTION_LOG_BULK") == "1";
        static double spent;
        static int measured;

        /// <summary>The body of `a`'s figure: its thighs (to the knees), its hips (to the spine) and its trunk (the spine to the neck).</summary>
        public static BodyBulk Of(Animator a)
        {
            var watch = Log ? System.Diagnostics.Stopwatch.StartNew() : null;
            var bulk = Measure(a);
            if (watch != null)
            {
                double ms = watch.Elapsed.TotalMilliseconds;
                spent += ms;
                measured++;
                int slices = 0;
                double sum = 0;
                foreach (var g in bulk.parts)
                {
                    slices += g.slices;
                    foreach (float r in g.radii) sum += r;
                }
                Debug.Log(FormattableString.Invariant($"[bulk] of {(a != null && a.transform.parent != null ? a.transform.parent.name : "-")} {ms:0.00} ms, {bulk.parts.Count} parts, {slices} slices, radii {sum:0.0000}; {measured} so far, {spent:0.0} ms"));
            }
            return bulk;
        }

        /// <summary>
        /// What one body's mesh, build and scale gave (Measure): its girths without their bones, bound to each figure's own
        /// (the same outfit and build again, a squad's Cuervos: one measure). Kept for the meshes still about, SameMost at most.
        /// </summary>
        sealed class Measured
        {
            public Mesh mesh;
            public float[] shapes;
            public float metre;
            public List<(HumanBodyBones bone, Girth girth)> girths;
        }

        static readonly Dictionary<int, Measured> same = new Dictionary<int, Measured>();
        /// <summary>BASTION_BULK_ANTES=1: measured as in the 0.12.20, each vertex into its bone's frame and nothing kept (the labs' «before»).</summary>
        static readonly bool SlowWay = System.Environment.GetEnvironmentVariable("BASTION_BULK_ANTES") == "1";
        const int SameMost = 64;

        static BodyBulk Measure(Animator a)
        {
            var bulk = new BodyBulk();
            if (a == null || !a.isHuman) return bulk;
            Transform thighR = a.GetBoneTransform(HumanBodyBones.RightUpperLeg), kneeR = a.GetBoneTransform(HumanBodyBones.RightLowerLeg),
                thighL = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), kneeL = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
                hips = a.GetBoneTransform(HumanBodyBones.Hips), spine = a.GetBoneTransform(HumanBodyBones.Spine),
                neck = a.GetBoneTransform(HumanBodyBones.Neck);
            if (thighR == null || kneeR == null || thighL == null || kneeL == null || hips == null || spine == null) return bulk;
            var torso = new HashSet<Transform> { hips, spine, a.GetBoneTransform(HumanBodyBones.Chest), a.GetBoneTransform(HumanBodyBones.UpperChest) };
            SkinnedMeshRenderer body = null;
            foreach (var r in a.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMesh != null && System.Array.IndexOf(r.bones, thighR) >= 0 && (body == null || r.sharedMesh.vertexCount > body.sharedMesh.vertexCount)) body = r;
            if (body == null) return bulk;
            var mesh = body.sharedMesh;
            bulk.metre = body.transform.lossyScale.x;
            var shapes = new float[mesh.blendShapeCount];
            for (int shape = 0; shape < shapes.Length; shape++) shapes[shape] = body.GetBlendShapeWeight(shape);
            // (The same mesh, build and scale measured before: its girths on this figure's bones. Gameplay, 10/11: a city
            // mission loaded 0.6 s slower in the 0.12.20 app.)
            if (!SlowWay && same.TryGetValue(mesh.GetInstanceID(), out var known) && known.mesh == mesh && known.metre == bulk.metre && Same(known.shapes, shapes))
            {
                foreach (var (bone, g) in known.girths)
                    if (a.GetBoneTransform(bone) is Transform b) bulk.parts.Add(new Girth { bone = b, origin = g.origin, axis = g.axis, u = g.u, v = g.v, slices = g.slices, radii = g.radii });
                return bulk;
            }
            var vertices = mesh.vertices;
            var delta = new Vector3[vertices.Length];
            for (int shape = 0; shape < shapes.Length; shape++)
            {
                float weight = shapes[shape];
                if (Mathf.Abs(weight) < 1e-3f) continue;
                int frame = mesh.GetBlendShapeFrameCount(shape) - 1;
                float full = mesh.GetBlendShapeFrameWeight(shape, frame);
                mesh.GetBlendShapeFrameVertices(shape, frame, delta, null, null);
                for (int i = 0; i < vertices.Length; i++) vertices[i] += delta[i] * (weight / full);
            }
            var bones = body.bones;
            var binds = mesh.bindposes;
            var weights = mesh.boneWeights;
            // (The trunk: the spine's three, each vertex into the spine's frame; the chest's and the upper chest's go along.)
            var trunk = new HashSet<int>();
            var ofTorso = new bool[bones.Length];
            for (int k = 0; k < bones.Length; k++)
            {
                ofTorso[k] = bones[k] != null && torso.Contains(bones[k]);
                if (ofTorso[k] && bones[k] != hips) trunk.Add(k);
            }
            int spineAt = System.Array.IndexOf(bones, spine), hipsAt = System.Array.IndexOf(bones, hips);
            int rightAt = System.Array.IndexOf(bones, thighR), leftAt = System.Array.IndexOf(bones, thighL);
            // (Each of the four bones' frame worked out once, not for each vertex: InBone's inverse and turn were most of
            // the 0.12.20's time, as much again for every figure armed.)
            var (rightFrom, rightTurn) = BoneFrame(binds, rightAt);
            var (leftFrom, leftTurn) = BoneFrame(binds, leftAt);
            var (hipsFrom, hipsTurn) = BoneFrame(binds, hipsAt);
            var (spineFrom, spineTurn) = BoneFrame(binds, spineAt);
            var ofRight = new List<Vector3>();
            var ofLeft = new List<Vector3>();
            var ofHips = new List<Vector3>();
            var ofTrunk = new List<Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                int main = w.boneIndex0;
                float most = w.weight0, torsoWeight = 0;
                void Weigh(int bone, float weight)
                {
                    if (weight <= 0 || bone < 0 || bone >= bones.Length) return;
                    if (ofTorso[bone]) torsoWeight += weight;
                    if (weight > most)
                    {
                        most = weight;
                        main = bone;
                    }
                }
                Weigh(w.boneIndex0, w.weight0);
                Weigh(w.boneIndex1, w.weight1);
                Weigh(w.boneIndex2, w.weight2);
                Weigh(w.boneIndex3, w.weight3);
                if (most < Own) continue;
                if (SlowWay)
                {
                    if (main == rightAt) ofRight.Add(InBone(binds[rightAt], vertices[i]));
                    else if (main == leftAt) ofLeft.Add(InBone(binds[leftAt], vertices[i]));
                    else if (main == hipsAt && torsoWeight >= TorsoPure) ofHips.Add(InBone(binds[hipsAt], vertices[i]));
                    else if (trunk.Contains(main) && spineAt >= 0) ofTrunk.Add(InBone(binds[spineAt], vertices[i]));
                    continue;
                }
                if (main == rightAt) ofRight.Add(rightTurn * (vertices[i] - rightFrom));
                else if (main == leftAt) ofLeft.Add(leftTurn * (vertices[i] - leftFrom));
                else if (main == hipsAt && torsoWeight >= TorsoPure) ofHips.Add(hipsTurn * (vertices[i] - hipsFrom));
                else if (trunk.Contains(main) && spineAt >= 0) ofTrunk.Add(spineTurn * (vertices[i] - spineFrom));
            }
            var girths = new List<(HumanBodyBones, Girth)>();
            void Add(HumanBodyBones of, Transform bone, List<Vector3> points, Transform to)
            {
                if (to != null && GirthOf(bone, points, Quaternion.Inverse(bone.rotation) * (to.position - bone.position)) is Girth g)
                {
                    bulk.parts.Add(g);
                    girths.Add((of, g));
                }
            }
            Add(HumanBodyBones.RightUpperLeg, thighR, ofRight, kneeR);
            Add(HumanBodyBones.LeftUpperLeg, thighL, ofLeft, kneeL);
            Add(HumanBodyBones.Hips, hips, ofHips, spine);
            Add(HumanBodyBones.Spine, spine, ofTrunk, neck);
            if (same.Count >= SameMost) same.Clear();
            same[mesh.GetInstanceID()] = new Measured { mesh = mesh, shapes = shapes, metre = bulk.metre, girths = girths };
            return bulk;
        }

        static bool Same(float[] a, float[] b)
        {
            if (a.Length != b.Length) return false;
            for (int k = 0; k < a.Length; k++) if (a[k] != b[k]) return false;
            return true;
        }

        /// <summary>A bone's frame from its bind pose (InBone's, once): its origin in the mesh, and the turn into it.</summary>
        static (Vector3 from, Quaternion turn) BoneFrame(Matrix4x4[] binds, int at)
        {
            if (at < 0 || at >= binds.Length) return (Vector3.zero, Quaternion.identity);
            var back = binds[at].inverse;
            return (back.MultiplyPoint3x4(Vector3.zero), Quaternion.Inverse(Quaternion.LookRotation(back.GetColumn(2), back.GetColumn(1))));
        }

        /// <summary>
        /// How deep `model` (any carried thing, as it is placed now) is in the body: the worst of points along its longest
        /// side and across its middle, each a ball of half its thickness (metres of the model's world; ≤ 0, clear by that).
        /// </summary>
        public float Depth(Transform model) => Depth(model, false);

        /// <summary>
        /// The same along its middle line alone, a point at a time (`line`): how far the body's skin is past it, for a gun
        /// across the back whose box takes in a deep stock (F-18's measure, 10/11).
        /// </summary>
        public float Depth(Transform model, bool line)
        {
            if (parts.Count == 0 || model == null) return -1;
            var box = new Bounds();
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = model.InverseTransformPoint(new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i / 2 % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z));
                    if (!any) box = new Bounds(p, Vector3.zero);
                    else box.Encapsulate(p);
                    any = true;
                }
            }
            if (!any) return -1;
            // (Its longest side, its thinnest and the one between, in its own frame.)
            var size = box.size;
            int longest = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            int thin = longest == 0 ? (size.y <= size.z ? 1 : 2) : longest == 1 ? (size.x <= size.z ? 0 : 2) : (size.x <= size.y ? 0 : 1);
            int mid = 3 - longest - thin;
            float scale = model.lossyScale.x;
            float radius = line ? 0 : size[thin] / 2 * scale / Mathf.Max(metre, 1e-6f);
            float across = line ? 0 : Mathf.Max(0, size[mid] / 2 - size[thin] / 2);
            float worst = -1;
            for (int k = 0; k < Points; k++)
                for (int h = -1; h <= 1; h++)
                {
                    var local = box.center;
                    local[longest] = Mathf.Lerp(box.min[longest], box.max[longest], k / (Points - 1f));
                    local[mid] += h * across;
                    var at = model.TransformPoint(local);
                    foreach (var g in parts) worst = Mathf.Max(worst, DepthIn(g, Quaternion.Inverse(g.bone.rotation) * (at - g.bone.position) / metre, radius) * metre);
                }
            return worst;
        }

        /// <summary>Points taken along a carried thing's longest side (Depth).</summary>
        const int Points = 8;

        /// <summary>
        /// `mount` (where `model` rides) moved out along `outward` (the world's) by `step` at a time, `most` in all at most,
        /// till `model` is no deeper than `touch` (below 0: that far clear); how far it went.
        /// </summary>
        public float Clear(Transform mount, Transform model, Vector3 outward, float step, float most, float touch)
        {
            if (parts.Count == 0 || mount == null || model == null || outward.sqrMagnitude < 1e-8f) return 0;
            outward.Normalize();
            float moved = 0;
            while (moved < most - 1e-6f && Depth(model) > touch)
            {
                mount.position += outward * step;
                moved += step;
            }
            return moved;
        }

        static Vector3 InBone(Matrix4x4 bind, Vector3 vertex)
        {
            var back = bind.inverse;
            var origin = back.MultiplyPoint3x4(Vector3.zero);
            var turn = Quaternion.LookRotation(back.GetColumn(2), back.GetColumn(1));
            return Quaternion.Inverse(turn) * (vertex - origin);
        }

        static Girth GirthOf(Transform bone, List<Vector3> points, Vector3 towards)
        {
            if (points.Count < 8 || towards.sqrMagnitude < 1e-10f) return null;
            var axis = towards.normalized;
            var u = Vector3.right;
            if (Mathf.Abs(Vector3.Dot(u, axis)) > 0.9f) u = Vector3.forward;
            u = (u - axis * Vector3.Dot(u, axis)).normalized;
            var v = Vector3.Cross(axis, u);
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            foreach (var p in points)
            {
                float t = Vector3.Dot(p, axis);
                lo = Mathf.Min(lo, t);
                hi = Mathf.Max(hi, t);
            }
            int slices = Mathf.Max(1, Mathf.CeilToInt((hi - lo) / Slice));
            var g = new Girth { bone = bone, origin = axis * lo, axis = axis, u = u, v = v, slices = slices, radii = new float[slices * Sectors] };
            foreach (var p in points)
            {
                var w = p - g.origin;
                int s = Mathf.Clamp(Mathf.FloorToInt(Vector3.Dot(w, axis) / Slice), 0, slices - 1);
                float x = Vector3.Dot(w, u), y = Vector3.Dot(w, v);
                int i = s * Sectors + SectorOf(x, y);
                g.radii[i] = Mathf.Max(g.radii[i], Mathf.Sqrt(x * x + y * y));
            }
            var filled = new float[Sectors];
            for (int s = 0; s < slices; s++)
            {
                bool some = false;
                for (int k = 0; k < Sectors; k++) some |= g.radii[s * Sectors + k] > 0;
                if (!some && s > 0) System.Array.Copy(g.radii, (s - 1) * Sectors, g.radii, s * Sectors, Sectors);
                for (int k = 0; k < Sectors; k++)
                {
                    float r = g.radii[s * Sectors + k];
                    filled[k] = r > 0 ? r : Mathf.Max(g.radii[s * Sectors + (k + Sectors - 1) % Sectors], g.radii[s * Sectors + (k + 1) % Sectors]);
                }
                System.Array.Copy(filled, 0, g.radii, s * Sectors, Sectors);
            }
            return g;
        }

        static int SectorOf(float x, float y)
        {
            float turn = Mathf.Atan2(y, x) / (2 * Mathf.PI);
            return Mathf.FloorToInt((turn % 1 + 1) % 1 * Sectors) % Sectors;
        }

        static float DepthIn(Girth g, Vector3 q, float radius)
        {
            var o = q - g.origin;
            float t = Vector3.Dot(o, g.axis);
            float length = g.slices * Slice;
            if (t < -radius || t > length + radius) return -1;
            float x = Vector3.Dot(o, g.u), y = Vector3.Dot(o, g.v);
            float rho = Mathf.Sqrt(x * x + y * y);
            float s = Mathf.Clamp(t / Slice - 0.5f, 0, g.slices - 1);
            int s0 = Mathf.FloorToInt(s), s1 = Mathf.Min(g.slices - 1, s0 + 1);
            float fs = s - s0;
            float turn = Mathf.Atan2(y, x) / (2 * Mathf.PI);
            float a = (turn % 1 + 1) % 1 * Sectors - 0.5f;
            int k0 = (Mathf.FloorToInt(a) + Sectors) % Sectors, k1 = (k0 + 1) % Sectors;
            float fk = a - Mathf.Floor(a);
            float At(int sl) => g.radii[sl * Sectors + k0] * (1 - fk) + g.radii[sl * Sectors + k1] * fk;
            float R = At(s0) * (1 - fs) + At(s1) * fs;
            float over = t < 0 ? -t : t > length ? t - length : 0;
            return R + Mathf.Sqrt(Mathf.Max(0, radius * radius - over * over)) - rho;
        }
    }
}
