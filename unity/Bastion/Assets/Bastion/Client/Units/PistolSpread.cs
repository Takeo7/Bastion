using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// syntyFigures.ts spreadPistol (22/10: the muzzle in a heavy build's thigh): the pistol at rest by the thigh kept out of
    /// it, the right arm turned out at the shoulder about the body's forward line, the least that clears it (π/45 at a
    /// time, π/9 at most), back in as far as it stays a centimetre clear, eased. The body as bulk.ts measured it, its two
    /// parts in the pistol's way: the right thigh and the hips, each slice by slice along its bone and sector by sector
    /// round it, from its mesh as bound (its build's blend shapes with it), the first time it is needed. One for each
    /// figure that holds a pistol; lengths in the model's metres, as 0.10.1's.
    /// </summary>
    public sealed class PistolSpread
    {
        /// <summary>syntyFigures.ts SPREAD_STEP, PISTOL_SPREAD_MAX, TOUCH, PISTOL_BACK_CLEAR, CLEAR_EASE, CLEAR_TURN, SEARCH_REST, GUN_POINTS.</summary>
        const float Step = Mathf.PI / 45, Most = Mathf.PI / 9, Touch = 0.012f, BackClear = 0.01f, Ease = 12, Fastest = 1.5f;
        const int SearchRest = 10, GunPoints = 6;
        /// <summary>bulk.ts SECTORS, SLICE, OWN, TORSO_PURE.</summary>
        const int Sectors = 16;
        const float Slice = 0.03f, Own = 0.5f, TorsoPure = 0.9f;

        /// <summary>bulk.ts Girth: a part along its bone (its frame, model metres), how far out it goes slice by slice and sector by sector.</summary>
        sealed class Girth
        {
            public Transform bone;
            public Vector3 origin, axis, u, v;
            public int slices;
            public float[] radii;
        }

        List<Girth> parts;
        /// <summary>The body's mesh units in the world (its renderer's scale): what the parts are measured in.</summary>
        float metre = 1;
        Transform gun;
        Vector3 gunMiddle;
        float gunZ0, gunZ1, gunR, gunLo, gunHi;
        float now, goal;
        int wait;
        /// <summary>Its points' heights off its middle line: on it, below, above.</summary>
        readonly float[] heights = new float[3];

        /// <summary>How far the arm is turned out now (radians).</summary>
        public float Out => now;

        /// <summary>For the labs: none turned out (as before it, 0.10.1's SYNTY_HANDS.gun false).</summary>
        public static bool Off;

        /// <summary>
        /// After the hand has the pistol (UnitViews.HoldPistol): while it is at rest by the thigh (FigureAnimator.PistolRest
        /// over half), the arm turned out of the body as far as it needs, the pistol with it; else eased back in.
        /// </summary>
        public void Apply(Animator a, FigureAnimator anim, Transform pistol, float dt)
        {
            if (Off || a == null || pistol == null || !a.isHuman) return;
            var shoulder = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (shoulder == null) return;
            if (parts == null) Measure(a);
            if (pistol != gun) Span(pistol);
            var s = shoulder.position;
            var axis = a.transform.forward;
            float target = now;
            if (anim != null && anim.PistolRest > 0.5f && parts.Count > 0)
            {
                float least = Depth(pistol, s, axis, now);
                if (least > Touch && wait > 0)
                {
                    wait--;
                    target = goal;
                }
                else if (least > Touch)
                {
                    bool clear = false;
                    for (float turn = now + Step; turn <= Most + 1e-6f; turn += Step)
                    {
                        float d = Depth(pistol, s, axis, turn);
                        if (d <= Touch)
                        {
                            target = turn;
                            clear = true;
                            break;
                        }
                        if (d < least)
                        {
                            least = d;
                            target = turn;
                        }
                    }
                    goal = target;
                    wait = clear ? 0 : SearchRest;
                }
                else if (now > 0)
                {
                    float back = Mathf.Max(0, now - Step);
                    if (Depth(pistol, s, axis, back) <= Touch - BackClear) target = back;
                }
            }
            else target = 0;
            // (Eased out too as it leaves the rest: 0.10.1 let it go at once, the hand a few centimetres in a frame.)
            now = dt <= 0 ? target : now + Mathf.Clamp((target - now) * (1 - Mathf.Exp(-dt * Ease)), -Fastest * dt, Fastest * dt);
            if (now <= 0) return;
            var q = Turn(axis, now);
            shoulder.rotation = q * shoulder.rotation;
            pistol.SetPositionAndRotation(s + q * (pistol.position - s), q * pistol.rotation);
        }

        /// <summary>The turn out of the right arm by `angle` about the forward line: the hanging hand to the body's right.</summary>
        static Quaternion Turn(Vector3 axis, float angle) => Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis);

        /// <summary>How deep the pistol would be in the body with the arm turned out `angle` more (0.10.1's depth): the worst of its points, ≤ 0 clear.</summary>
        float Depth(Transform pistol, Vector3 s, Vector3 axis, float angle)
        {
            var q = Turn(axis, angle);
            float worst = -1;
            for (int k = 0; k < GunPoints; k++)
                foreach (float y in heights)
                {
                    var local = new Vector3(gunMiddle.x, gunMiddle.y + y, Mathf.Lerp(gunZ0, gunZ1, k / (GunPoints - 1f)));
                    var at = s + q * (pistol.TransformPoint(local) - s);
                    foreach (var g in parts) worst = Mathf.Max(worst, DepthIn(g, Quaternion.Inverse(g.bone.rotation) * (at - g.bone.position) / metre, gunR));
                }
            return worst;
        }

        /// <summary>
        /// syntyFigures.ts equip's span: the pistol's box in its own frame (model metres), along its barrel (z), its middle,
        /// half its thickness and how far above and below that its middle line may go.
        /// </summary>
        void Span(Transform pistol)
        {
            gun = pistol;
            bool any = false;
            var box = new Bounds();
            foreach (var r in pistol.GetComponentsInChildren<Renderer>(true))
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = pistol.InverseTransformPoint(new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i / 2 % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z));
                    if (!any) box = new Bounds(p, Vector3.zero);
                    else box.Encapsulate(p);
                    any = true;
                }
            }
            // (Its own frame is scaled: its local units into the model's metres.)
            float unit = pistol.lossyScale.x / Mathf.Max(metre, 1e-6f);
            gunMiddle = box.center;
            gunZ0 = box.min.z;
            gunZ1 = box.max.z;
            float r0 = Mathf.Min(box.size.x, box.size.y) / 2;
            gunR = r0 * unit;
            gunLo = Mathf.Min(0, box.min.y + r0 - box.center.y);
            gunHi = Mathf.Max(0, box.max.y - r0 - box.center.y);
            heights[1] = gunLo;
            heights[2] = gunHi;
        }

        /// <summary>
        /// bulk.ts bulkOf for the two parts: the body's skinned mesh as bound, its build's blend shapes as they are weighted,
        /// each vertex by its main bone (half its weight at least; the hips' only when nearly all of it is the torso's),
        /// into that bone's frame as bound.
        /// </summary>
        void Measure(Animator a)
        {
            parts = new List<Girth>();
            Transform thigh = a.GetBoneTransform(HumanBodyBones.RightUpperLeg), knee = a.GetBoneTransform(HumanBodyBones.RightLowerLeg), hips = a.GetBoneTransform(HumanBodyBones.Hips),
                spine = a.GetBoneTransform(HumanBodyBones.Spine);
            if (thigh == null || knee == null || hips == null || spine == null) return;
            var torso = new HashSet<Transform> { hips, spine, a.GetBoneTransform(HumanBodyBones.Chest), a.GetBoneTransform(HumanBodyBones.UpperChest) };
            SkinnedMeshRenderer body = null;
            foreach (var r in a.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMesh != null && System.Array.IndexOf(r.bones, thigh) >= 0 && (body == null || r.sharedMesh.vertexCount > body.sharedMesh.vertexCount)) body = r;
            if (body == null) return;
            var mesh = body.sharedMesh;
            metre = body.transform.lossyScale.x;
            var vertices = mesh.vertices;
            // Its build: the blend shapes at their weights, each at its last frame.
            var delta = new Vector3[vertices.Length];
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
            {
                float weight = body.GetBlendShapeWeight(shape);
                if (Mathf.Abs(weight) < 1e-3f) continue;
                int frame = mesh.GetBlendShapeFrameCount(shape) - 1;
                float full = mesh.GetBlendShapeFrameWeight(shape, frame);
                mesh.GetBlendShapeFrameVertices(shape, frame, delta, null, null);
                for (int i = 0; i < vertices.Length; i++) vertices[i] += delta[i] * (weight / full);
            }
            var bones = body.bones;
            var binds = mesh.bindposes;
            var weights = mesh.boneWeights;
            int thighAt = System.Array.IndexOf(bones, thigh), hipsAt = System.Array.IndexOf(bones, hips);
            var ofThigh = new List<Vector3>();
            var ofHips = new List<Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                int main = w.boneIndex0;
                float most = w.weight0, torsoWeight = 0;
                void Weigh(int bone, float weight)
                {
                    if (weight <= 0 || bone < 0 || bone >= bones.Length) return;
                    if (torso.Contains(bones[bone])) torsoWeight += weight;
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
                if (main == thighAt && most >= Own) ofThigh.Add(InBone(binds[thighAt], vertices[i]));
                else if (main == hipsAt && most >= Own && torsoWeight >= TorsoPure) ofHips.Add(InBone(binds[hipsAt], vertices[i]));
            }
            // Each along the way to the next bone: the thigh to the knee, the hips up to the spine.
            if (GirthOf(thigh, ofThigh, Quaternion.Inverse(thigh.rotation) * (knee.position - thigh.position)) is Girth leg) parts.Add(leg);
            if (GirthOf(hips, ofHips, Quaternion.Inverse(hips.rotation) * (spine.position - hips.position)) is Girth middle) parts.Add(middle);
        }

        /// <summary>A vertex of the mesh as bound in a bone's frame as bound (its turn, the mesh's metres): what moves with that bone.</summary>
        static Vector3 InBone(Matrix4x4 bind, Vector3 vertex)
        {
            // (The bind pose takes the mesh into the bone's own space, scaled: its turn alone, and the bone where it was.)
            var back = bind.inverse;
            var origin = back.MultiplyPoint3x4(Vector3.zero);
            var turn = Quaternion.LookRotation(back.GetColumn(2), back.GetColumn(1));
            return Quaternion.Inverse(turn) * (vertex - origin);
        }

        /// <summary>bulk.ts girthOf: the points round `bone` along `towards` (its frame), slice by slice and sector by sector.</summary>
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
                int k = SectorOf(x, y);
                int i = s * Sectors + k;
                g.radii[i] = Mathf.Max(g.radii[i], Mathf.Sqrt(x * x + y * y));
            }
            // A sector nothing fell in (a thin slice) takes the larger of its neighbours; an empty slice, the one before.
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

        /// <summary>bulk.ts depthIn: how deep a ball of `radius` at `q` (the part's bone's frame) is in it; ≤ 0 clear of it.</summary>
        static float DepthIn(Girth g, Vector3 q, float radius)
        {
            var o = q - g.origin;
            float t = Vector3.Dot(o, g.axis);
            float length = g.slices * Slice;
            if (t < -radius || t > length + radius) return -1;
            float x = Vector3.Dot(o, g.u), y = Vector3.Dot(o, g.v);
            float rho = Mathf.Sqrt(x * x + y * y);
            // Between the two slices and the two sectors either side.
            float s = Mathf.Clamp(t / Slice - 0.5f, 0, g.slices - 1);
            int s0 = Mathf.FloorToInt(s), s1 = Mathf.Min(g.slices - 1, s0 + 1);
            float fs = s - s0;
            float turn = Mathf.Atan2(y, x) / (2 * Mathf.PI);
            float a = (turn % 1 + 1) % 1 * Sectors - 0.5f;
            int k0 = (Mathf.FloorToInt(a) + Sectors) % Sectors, k1 = (k0 + 1) % Sectors;
            float fk = a - Mathf.Floor(a);
            float At(int sl) => g.radii[sl * Sectors + k0] * (1 - fk) + g.radii[sl * Sectors + k1] * fk;
            float R = At(s0) * (1 - fs) + At(s1) * fs;
            // Past an end the ball only reaches in as far as it overlaps it.
            float over = t < 0 ? -t : t > length ? t - length : 0;
            return R + Mathf.Sqrt(Mathf.Max(0, radius * radius - over * over)) - rho;
        }
    }
}
