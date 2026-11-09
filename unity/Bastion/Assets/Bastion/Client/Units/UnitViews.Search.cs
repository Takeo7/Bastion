using System.Threading;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// A search (unitViews.ts rummage, syntyFigures.ts pose.search): turned to what it searches, down on its haunches and
    /// leaning in over 0.22 s, rummaging 0.45 s, back up over 0.18 s. Meanwhile its hand reaches in and moves about in front
    /// of its hips (rummage: the left, the gun kept in the right; both without one), its trunk leant 0.45 forward of it.
    /// </summary>
    public sealed partial class UnitViews
    {
        public async Awaitable Rummage(string id, Vector3 at, CancellationToken ct = default)
        {
            Face(id, at);
            if (!views.TryGetValue(id, out var v) || v.anim == null)
            {
                await Awaitable.WaitForSecondsAsync(0.85f, ct);
                return;
            }
            // (Its legs crouched, its upper body holding what it holds as it stands: pose.crouch 0.6 of the search. With
            // a rifle, Synty's crouch and not the kneel: a search drops the gun's clips, syntyFigures.ts weaponShares.)
            var catalog = Rifled(v) ? FigureCatalog.Load() : null;
            var squat = catalog?.Clip(Anims.Motion(v.build, "crouchIdle", v.deadKind));
            if (squat != null && v.animator != null && v.animator.isHuman) HoldAsNow(v);
            if (squat != null)
            {
                // The rifle in the right arm alone, the trunk the crouch's (holdWeapon over no clip: kept low as it leans).
                v.anim.SetMotion("crouchIdle", squat);
                v.anim.Rest(null, 0.2f);
                v.anim.ArmRest(catalog.Clip(RestOf(v)), 0, 0.2f);
            }
            v.anim.Motion("crouchIdle", 1, 0.6f);
            try
            {
                for (float t = 0; t < 0.22f; t += Time.deltaTime)
                {
                    v.search = Mathf.SmoothStep(0, 1, t / 0.22f);
                    await Awaitable.NextFrameAsync(ct);
                }
                v.search = 1;
                await Awaitable.WaitForSecondsAsync(0.45f, ct);
                v.anim.Motion(IdleOf(v));
                for (float t = 0; t < 0.18f; t += Time.deltaTime)
                {
                    v.search = 1 - Mathf.SmoothStep(0, 1, t / 0.18f);
                    await Awaitable.NextFrameAsync(ct);
                }
            }
            finally
            {
                v.search = 0;
                if (!v.walking && !v.feeding && !v.dead) v.anim?.Motion(IdleOf(v));
                if (squat != null)
                {
                    Settle(v, 0.2f);
                    _ = Kneel(v, catalog.Clip("rifleKneel"));
                }
            }
        }

        /// <summary>
        /// A medicine given (the review of 9/11, Gameplay: «agacharse y llevar las manos al herido»; till then it only
        /// turned): a syringe (Fx.Thrown "syringe": Gang Warfare's) in its left hand, on the chest of the one it treats (Tend)
        /// or, down as a search goes down (Rummage), on its own thigh; the gun low in its right hand. UI's event player waits for it.
        /// </summary>
        public async Awaitable Treat(string id, string patient, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v) || v.anim == null || v.dead)
            {
                await Awaitable.WaitForSecondsAsync(0.85f, ct);
                return;
            }
            bool self = patient == null || patient == id || !views.ContainsKey(patient);
            Transform syringe = null;
            if (v.animator != null && v.animator.isHuman && v.animator.GetBoneTransform(HumanBodyBones.LeftHand) is Transform hand)
            {
                syringe = Effects.Fx.Thrown("syringe", hand);
                if (syringe != null && v.animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal) is Transform finger)
                {
                    // (In the fist, along the fingers.)
                    var along = finger.position - hand.position;
                    var r = syringe.GetComponentInChildren<Renderer>();
                    syringe.position += hand.position + along * 0.8f - (r != null ? r.bounds.center : syringe.position);
                    syringe.rotation = Quaternion.FromToRotation(Vector3.up, along.normalized) * syringe.rotation;
                }
            }
            try
            {
                // Itself: down as a search, the syringe to its thigh (followed as it goes down: a point fixed as it stood
                // was over its head once down). Another: the wounded stand (slumped), so it steps in to it, standing.
                if (self)
                {
                    v.treatSelf = true;
                    await Rummage(id, v.root.position + v.root.forward * Map.MapSpace.Tile, ct);
                }
                else await Tend(v, views[patient], ct);
            }
            finally
            {
                v.treatSelf = false;
                v.tended = null;
                if (syringe != null) DestroyNow(syringe.gameObject);
            }
        }

        /// <summary>
        /// Treating another (Treat): turned to it, a step in to TendGap tiles off it over 0.22 s, leant a little (TendLean of
        /// a search's), the left hand on its chest 0.45 s, back over 0.18 s; the gun low in the right hand, as in a search.
        /// </summary>
        async Awaitable Tend(View v, View patient, CancellationToken ct)
        {
            Face(v.id, patient.root.position);
            var catalog = Rifled(v) ? FigureCatalog.Load() : null;
            if (catalog != null && v.animator != null && v.animator.isHuman)
            {
                HoldAsNow(v);
                v.anim.Rest(null, 0.2f);
                v.anim.ArmRest(catalog.Clip(RestOf(v)), 0, 0.2f);
            }
            v.tended = patient;
            var home = v.root.position;
            var to = home + Reach(home, patient.root.position, TendGap, 1);
            try
            {
                for (float t = 0; t < 0.22f; t += Time.deltaTime)
                {
                    v.search = Mathf.SmoothStep(0, 1, t / 0.22f);
                    v.root.position = Vector3.Lerp(home, to, v.search);
                    await Awaitable.NextFrameAsync(ct);
                }
                v.search = 1;
                v.root.position = to;
                await Awaitable.WaitForSecondsAsync(0.45f, ct);
                for (float t = 0; t < 0.18f; t += Time.deltaTime)
                {
                    v.search = 1 - Mathf.SmoothStep(0, 1, t / 0.18f);
                    v.root.position = Vector3.Lerp(home, to, v.search);
                    await Awaitable.NextFrameAsync(ct);
                }
            }
            finally
            {
                v.search = 0;
                if (v.root != null) v.root.position = home;
                if (catalog != null) Settle(v, 0.2f);
            }
        }

        /// <summary>How far off the one it treats it steps in to (tiles: about half a metre), and how far it leans (of a search's).</summary>
        const float TendGap = 0.37f, TendLean = 0.35f;

        /// <summary>The rifle's kneel back as the crouch once the search's has eased out (FigureAnimator.Blend).</summary>
        static async Awaitable Kneel(View v, AnimationClip kneel)
        {
            if (!v.crouched) await Awaitable.WaitForSecondsAsync(0.4f);
            if (v.anim != null && kneel != null) v.anim.SetMotion("crouchIdle", kneel);
        }

        /// <summary>
        /// Where a treating hand goes (SearchPose), each frame: on the near side of the chest of the one it treats, or on its
        /// own left thigh, a little ahead of it; null if not treating.
        /// </summary>
        Vector3? TreatAt(View v, float scale)
        {
            if (v.tended != null && v.tended.root != null)
            {
                var chest = BodyChest(v.tended);
                var near = v.root.position - v.tended.root.position;
                near.y = 0;
                return chest + (near.sqrMagnitude > 1e-6f ? near.normalized : v.root.forward) * 0.12f * scale;
            }
            if (!v.treatSelf || v.animator == null) return null;
            Transform thigh = v.animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), knee = v.animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            if (thigh == null || knee == null) return null;
            return Vector3.Lerp(thigh.position, knee.position, 0.45f) + v.root.forward * 0.09f * scale;
        }

        /// <summary>How the gun sits in its right hand now, and its chest standing (LowReady's lean measured from it).</summary>
        static void HoldAsNow(View v)
        {
            Transform hand = v.animator.GetBoneTransform(HumanBodyBones.RightHand), chest = ChestOf(v.animator);
            if (hand == null || chest == null || v.weapon == null) return;
            v.searchGunAt = Quaternion.Inverse(hand.rotation) * (v.weapon.position - hand.position);
            v.searchGun = Quaternion.Inverse(hand.rotation) * v.weapon.rotation;
            v.searchChest = Quaternion.Inverse(v.root.rotation) * chest.rotation;
        }

        /// <summary>
        /// syntyFigures.ts holdWeapon at low ready, over no clip (LOW_READY, crouched 0.6: CROUCH_LIFT, CROUCH_AHEAD): the grip
        /// 0.15 of the shoulder's way out, 0.22 below it less the lift, 0.22 ahead of it and the 0.05 more, carried by the
        /// upper chest as it leans; the gun level whatever the lean, the muzzle turned 0.9 to the figure's left and tipped
        /// 0.42 down. The right hand on it by two-bone IK, eased in by `k`.
        /// </summary>
        void LowReady(View v, float k, float scale, float crouch = 0.6f)
        {
            var a = v.animator;
            Transform hand = a.GetBoneTransform(HumanBodyBones.RightHand), shoulder = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform hips = a.GetBoneTransform(HumanBodyBones.Hips), chest = ChestOf(a);
            if (hand == null || shoulder == null || hips == null || chest == null) return;
            var turn = v.root.rotation;
            float side = Vector3.Dot(shoulder.position - hips.position, v.root.right);
            var grip = turn * new Vector3(-0.85f * side / Mathf.Max(scale, 1e-4f), -0.22f + crouch * 0.11f, 0.22f + crouch * 0.05f) * scale;
            var lean = chest.rotation * Quaternion.Inverse(turn * v.searchChest);
            var at = shoulder.position + lean * grip;
            // (Three's model x is the figure's left, Unity's −x.)
            const float tip = 0.42f, across = 0.9f;
            var muzzle = turn * new Vector3(-Mathf.Cos(tip) * Mathf.Sin(across), -Mathf.Sin(tip), Mathf.Cos(tip) * Mathf.Cos(across));
            var gun = Quaternion.LookRotation(muzzle, Vector3.ProjectOnPlane(Vector3.up, muzzle));
            var handTurn = gun * Quaternion.Inverse(v.searchGun);
            var target = at - handTurn * v.searchGunAt;
            Reach(a, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, Vector3.Lerp(hand.position, target, k),
                turn * new Vector3(0.7f, -1, -0.35f), SoftReach * scale);
            hand.rotation = Quaternion.Slerp(hand.rotation, handTurn, k);
            v.weapon.SetPositionAndRotation(hand.position + hand.rotation * v.searchGunAt, hand.rotation * v.searchGun);
        }

        /// <summary>The search's pose over the clips and the hold (applyPose: the lean, rummage's hands by two-bone IK).</summary>
        void SearchPose(View v)
        {
            var a = v.animator;
            if (a == null || !a.isHuman || v.search <= 0) return;
            float k = v.search, scale = v.body != null ? v.body.lossyScale.x : 1;
            // Leant over what it searches, half at the spine and half at the chest.
            var right = v.root.right;
            float lean = v.tended != null ? TendLean : 1;
            foreach (var bone in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest })
            {
                var b = a.GetBoneTransform(bone);
                if (b != null) b.rotation = Quaternion.AngleAxis(0.45f * k * 0.5f * lean * Mathf.Rad2Deg, right) * b.rotation;
            }
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;
            // (syntyFigures.ts rummage: about 0.42 ahead of the hips and down a third of their height, each hand a little to its
            // side and moving about; its x to the figure's left, Unity's −x.)
            float hip = Vector3.Dot(hips.position - v.root.position, Vector3.up) / Mathf.Max(scale, 1e-4f);
            float time = Time.time;
            foreach (var (s, upper, lower, hand) in new[] { (1f, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (-1f, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) })
            {
                // The gun stays in the right hand at low ready (holdWeapon over no clip), the hold rigid in it (Hold would
                // lay it along to the left hand, gone in among the things).
                if (s < 0 && v.weapon != null)
                {
                    if (Rifled(v)) LowReady(v, k, scale, v.tended != null ? 0 : 0.6f);
                    continue;
                }
                float t = time * 9 + (s > 0 ? 0 : 1.7f);
                var offset = new Vector3(-s * (0.1f + Mathf.Sin(t) * 0.06f), -hip * 0.35f + Mathf.Sin(t * 1.4f) * 0.03f, 0.42f + Mathf.Cos(t * 0.8f) * 0.04f) * scale;
                var rummage = hips.position + v.root.rotation * offset;
                // (Treating: the left hand on the one treated, still, a little to and fro; the right one stays as in a search.)
                if (TreatAt(v, scale) is Vector3 treat)
                {
                    if (s < 0) continue;
                    rummage = treat + v.root.rotation * new Vector3(0, Mathf.Sin(t * 0.5f) * 0.02f, 0) * scale;
                }
                var now = a.GetBoneTransform(hand);
                if (now == null) continue;
                var target = Vector3.Lerp(now.position, rummage, k);
                var pole = v.root.rotation * new Vector3(-s * 0.7f, -1, 0.2f);
                Reach(a, upper, lower, hand, target, pole, SoftReach * scale);
            }
        }
    }
}
