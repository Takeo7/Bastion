using System;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// G23.3, El herido: the survivor that can't walk (Unit.Lame), alive and on the ground. Sat on the floor (the camp's
    /// loops), its left leg out straight before it (WoundedLeg, over the loop) and splinted: two boards along its shin and two bandages round them, ours, on its bones.
    /// BASTION_HERIDO=suelo|recostado|cruzado (a lab's, in the editor): which loop, to set them side by side.
    /// </summary>
    public sealed partial class UnitViews
    {
        static readonly string WoundedAs = Application.isEditor ? Environment.GetEnvironmentVariable("BASTION_HERIDO") : null;
        static Material splintWood, splintCloth;
        /// <summary>BASTION_HERIDO_LOG=1 (the editor's): how far the carried one sticks out to each side, «[herido]».</summary>
        static readonly bool WoundedLog = Application.isEditor && Environment.GetEnvironmentVariable("BASTION_HERIDO_LOG") == "1";

        /// <summary>The wounded's own pose on the ground: true if it has it now, and Pose leaves it be.</summary>
        bool WoundedPose(View v, Unit u)
        {
            bool sat = u.Lame == true && u.Alive;
            string clip = !sat ? null : WoundedAs == "recostado" ? "sitReclined" : WoundedAs == "cruzado" ? "sitCrossed" : "sitFloor";
            if (clip != v.woundedClip)
            {
                v.woundedClip = clip;
                var c = clip != null ? FigureCatalog.Load()?.Clip(clip) : null;
                // (At once, no blend: set down it stood on its broken leg for three frames before sitting.)
                var part = Anims.CampPartOf(clip);
                if (c != null && part?.loop != null && part.loop.Length >= 2) v.anim.CampLoop(c, true, part.loop[0], part.loop[1]);
                else v.anim.CampLoop(c, true);
                // (Posed now: the Sync that sets it down comes after the frame's step, and it was drawn a frame as it rode.)
                if (clip != null)
                {
                    v.anim.Step(0);
                    WoundedLeg(v);
                }
            }
            if (u.Lame == true && v.splint == null) v.splint = Splint(v);
            return clip != null;
        }

        /// <summary>Its broken leg out straight along the ground, a little to its side: over the loop, each frame.</summary>
        static void WoundedLeg(View v)
        {
            if (v.woundedClip == null || v.animator == null || v.root == null) return;
            var a = v.animator;
            Transform thigh = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), knee = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg), foot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (thigh == null || knee == null || foot == null) return;
            float length = (knee.position - thigh.position).magnitude + (foot.position - knee.position).magnitude;
            var way = Quaternion.AngleAxis(-WoundedSplay, Vector3.up) * v.root.forward;
            // (Its ankle just over the ground, wherever the loop has its hips: sat on the floor they are at its height.)
            float rise = Mathf.Clamp(v.root.position.y + WoundedHeel - thigh.position.y, -length * 0.9f, length * 0.9f);
            var dir = (way * Mathf.Sqrt(length * length - rise * rise) + Vector3.up * rise).normalized;
            thigh.rotation = Quaternion.FromToRotation(knee.position - thigh.position, dir) * thigh.rotation;
            knee.rotation = Quaternion.FromToRotation(foot.position - knee.position, dir) * knee.rotation;
        }

        /// <summary>Degrees its broken leg lies out to its left, and how high its ankle stays over the ground (metres).</summary>
        const float WoundedSplay = 14, WoundedHeel = 0.1f;

        /// <summary>
        /// The wounded carried (Unit.CarriedBy; not alive on the board, but no body): across its carrier's shoulders, belly
        /// down, its head and arms out to the carrier's right and its legs to the left, the splinted one seen. Higher and
        /// further back than a real carry, clear of the carrier's head (Producto, 19/11: what counts is the outline from the
        /// game's camera). The carrier leant forward under it, its rucksack and long gun not drawn meanwhile, its left hand
        /// on the legs and its right on an arm (the right its own again with a pistol or a blade in it). No clip of it
        /// (Mixamo's, only Arturo's to bring): over what both have, each frame, once all are posed.
        /// </summary>
        void StepCarried()
        {
            carriers.Clear();
            foreach (var v in views.Values)
                if (v.carriedBy != null) carriers.Add(v.carriedBy);
            foreach (var v in views.Values)
            {
                bool carries = carriers.Contains(v.id);
                bool hide = carries && v.inHand == Held.Rifle && v.weapon != null;
                if (hide != v.gunHidden)
                {
                    v.gunHidden = hide;
                    if (v.weapon != null)
                        foreach (var r in v.weapon.GetComponentsInChildren<Renderer>(true)) r.enabled = !hide;
                }
                // (Its rucksack, Sidekick's part on the back: of one mesh with the rest, so its bone shrunk to nothing.)
                if (carries && !v.backSought && v.body != null)
                {
                    v.backSought = true;
                    foreach (var t in v.body.GetComponentsInChildren<Transform>(true))
                        if (t.name == "backAttach") v.backBone = t;
                    if (v.backBone != null) v.backScale = v.backBone.localScale;
                }
                if (v.backBone != null) v.backBone.localScale = carries ? Vector3.one * 0.001f : v.backScale;
            }
            foreach (var v in views.Values)
            {
                if (v.carriedBy == null || v.root == null || v.animator == null || !v.animator.isHuman) continue;
                if (!views.TryGetValue(v.carriedBy, out var c) || c.root == null || c.animator == null || !c.animator.isHuman) continue;
                Transform A(HumanBodyBones b) => v.animator.GetBoneTransform(b);
                Transform C(HumanBodyBones b) => c.animator.GetBoneTransform(b);
                // The carrier bent forward under the weight, its head up again.
                if (c.missed <= 0)
                {
                    var across = c.root.right;
                    foreach (var bone in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest })
                        if (C(bone) is Transform b) b.rotation = Quaternion.AngleAxis(CarrierLean / 2, across) * b.rotation;
                    if (C(HumanBodyBones.Head) is Transform up) up.rotation = Quaternion.AngleAxis(-CarrierLean * 0.7f, across) * up.rotation;
                }
                var neck = C(HumanBodyBones.Neck) ?? C(HumanBodyBones.Head);
                Transform hips = A(HumanBodyBones.Hips), spine = A(HumanBodyBones.Spine);
                if (neck == null || hips == null || spine == null) continue;
                var ahead = c.root.forward;
                var right = c.root.right;
                v.root.rotation = Quaternion.LookRotation(Vector3.down, right);
                var belly = (hips.position + spine.position) / 2;
                v.root.position += neck.position + Vector3.up * CarriedOver - ahead * CarriedBehind - belly;
                // Out to both sides and hanging a little: its legs to the carrier's left, its trunk and arms to the right.
                var legs = (-right * 0.85f + Vector3.down * 0.5f + ahead * 0.15f).normalized;
                var trunk = (right * 0.85f + Vector3.down * 0.4f + ahead * 0.1f).normalized;
                var arms = (Vector3.down + ahead * 0.45f + right * 0.2f).normalized;
                void Point(HumanBodyBones from, HumanBodyBones to, Vector3 way)
                {
                    Transform f = A(from), t = A(to);
                    if (f != null && t != null) f.rotation = Quaternion.FromToRotation(t.position - f.position, way) * f.rotation;
                }
                Point(HumanBodyBones.Spine, HumanBodyBones.Neck, trunk);
                Point(HumanBodyBones.Neck, HumanBodyBones.Head, (trunk + Vector3.down * 0.5f).normalized);
                // (The splinted one, the left, straight and the higher of the two: its stripes seen; the other bent under it.)
                Point(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, (legs + Vector3.up * 0.25f).normalized);
                Point(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, (legs + Vector3.up * 0.25f).normalized);
                Point(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, legs);
                Point(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, (legs + Vector3.down * 0.8f).normalized);
                foreach (var (upper, lower, end) in new[] { (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) })
                {
                    Point(upper, lower, arms);
                    Point(lower, end, arms);
                }
                if (WoundedLog && Time.frameCount % 30 == 0 && A(HumanBodyBones.Head) is Transform top && A(HumanBodyBones.LeftFoot) is Transform toe)
                    Debug.Log(FormattableString.Invariant($"[herido] out to the right {Vector3.Dot(top.position - c.root.position, right) / Map.MapSpace.Tile:0.00} tiles (head), to the left {Vector3.Dot(c.root.position - toe.position, right) / Map.MapSpace.Tile:0.00} (splinted foot), top {top.position.y - c.root.position.y:0.00} m"));
                if (c.missed > 0) continue;
                // The carrier's hands on it: its left on the legs, its right on the arm that hangs before its shoulder.
                void Hold(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand, Vector3? at, float side)
                {
                    Transform u = C(upper), l = C(lower), h = C(hand);
                    if (u != null && l != null && h != null && at != null) Grasp(u, l, h, at.Value, Vector3.down + right * side * 0.8f - ahead * 0.2f);
                }
                Hold(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, A(HumanBodyBones.RightLowerLeg)?.position, -1);
                if (c.inHand != Held.Pistol && c.inHand != Held.Blade)
                    Hold(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, A(HumanBodyBones.LeftLowerArm)?.position, 1);
            }
        }

        /// <summary>
        /// An arm's hand put at `to` (two bones: the elbow bent as far as it takes, then the whole arm aimed), its elbow out
        /// towards `pole`. Out of reach, as far as it goes.
        /// </summary>
        static void Grasp(Transform upper, Transform lower, Transform hand, Vector3 to, Vector3 pole)
        {
            float a = (lower.position - upper.position).magnitude, b = (hand.position - lower.position).magnitude;
            if (a < 1e-4f || b < 1e-4f) return;
            var reach = to - upper.position;
            float d = Mathf.Clamp(reach.magnitude, Mathf.Abs(a - b) + 0.01f, a + b - 0.005f);
            float want = Mathf.Acos(Mathf.Clamp((a * a + b * b - d * d) / (2 * a * b), -1, 1)) * Mathf.Rad2Deg;
            Vector3 back = upper.position - lower.position, on = hand.position - lower.position;
            var axis = Vector3.Cross(back, on);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(back, pole);
            lower.rotation = Quaternion.AngleAxis(want - Vector3.Angle(back, on), axis.normalized) * lower.rotation;
            upper.rotation = Quaternion.FromToRotation(hand.position - upper.position, reach) * upper.rotation;
            var along = reach.normalized;
            Vector3 elbow = Vector3.ProjectOnPlane(lower.position - upper.position, along), wanted = Vector3.ProjectOnPlane(pole, along);
            if (elbow.sqrMagnitude > 1e-6f && wanted.sqrMagnitude > 1e-6f) upper.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(elbow, wanted, along), along) * upper.rotation;
        }

        readonly System.Collections.Generic.HashSet<string> carriers = new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// How far over its carrier's neck the wounded's belly rides, and how far behind it (metres); how far forward the
        /// carrier bends under it (degrees).
        /// </summary>
        const float CarriedOver = 0.2f, CarriedBehind = 0.16f, CarrierLean = 18;

        /// <summary>The splint on its left shin, hung from its knee: built along the bone as it stands now.</summary>
        static GameObject Splint(View v)
        {
            var a = v.animator;
            Transform knee = a != null ? a.GetBoneTransform(HumanBodyBones.LeftLowerLeg) : null, foot = a != null ? a.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            if (knee == null || foot == null) return null;
            splintWood ??= Tinted("Tablilla", new Color(0.62f, 0.45f, 0.27f));
            splintCloth ??= Tinted("Venda", new Color(0.9f, 0.87f, 0.8f));
            var along = foot.position - knee.position;
            float length = along.magnitude;
            if (length < 1e-4f) return null;
            var splint = new GameObject("tablilla");
            splint.transform.SetPositionAndRotation(knee.position, Quaternion.LookRotation(along / length, knee.right));
            // (Metres of the shin's own length: thicker than a leg's, to be seen from the game's camera.)
            float wide = length * 0.2f;
            void Piece(Material m, Vector3 at, Vector3 size)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(c.GetComponent<Collider>());
                c.transform.SetParent(splint.transform, false);
                c.transform.localPosition = at * length;
                c.transform.localScale = size * length;
                if (m != null) c.GetComponent<Renderer>().sharedMaterial = m;
            }
            foreach (float side in new[] { -1f, 1f }) Piece(splintWood, new Vector3(side * 0.17f, 0, 0.5f), new Vector3(0.05f, 0.2f, 1.05f));
            foreach (float at in new[] { 0.2f, 0.5f, 0.8f }) Piece(splintCloth, new Vector3(0, 0, at), new Vector3(0.42f, 0.36f, 0.13f));
            splint.transform.SetParent(knee, true);
            return splint;
        }

        static Material Tinted(string name, Color colour)
        {
            var m = KitMaterials.New(KitMaterials.Plain, name);
            if (m != null) m.SetColor("_BaseColor", colour);
            return m;
        }
    }
}
