using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// A straight-stocked long gun at rest (T22, Arturo's choice of 18/11, «hacerlo bien ya»: the rests' arms are posed for a
    /// pistol-gripped rifle, and on a straight stock the right hand lay on the receiver by the scope, or the stock ran along
    /// the forearm): the gun is placed first, the hands go to it after. Its butt under the right armpit against the ribs, the
    /// butt plate behind the elbow, the muzzle down and ahead to the left (TuckDown under the level, TuckLeft to the left);
    /// the right hand round the stock's wrist (thumb over, fingers under, the index along the trigger guard), the left palm up
    /// under the handguard ahead of the receiver, as far along it as the arm reaches; both closed round it. By its art's
    /// `tuck` (butt, grip, fore: points of its model's frame, unscaled); none, the clips' own hold. It follows the chest as
    /// the rest breathes.
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>The muzzle under the level and to the left of ahead (degrees), and the gun's cant (its top towards the body's left).</summary>
        const float TuckDown = 27, TuckLeft = 26, TuckCant = 12;
        /// <summary>
        /// Where the butt goes from the right shoulder's joint, in the root's frame (metres of a figure at scale 1; to its
        /// right, up, ahead): under the arm, between it and the ribs, the butt plate behind the elbow. (In the hollow of the
        /// shoulder, tried on 18/11, the stock went into the chest and the right arm crossed over it.)
        /// </summary>
        static readonly Vector3 TuckAt = Tried() ?? new Vector3(0.035f, -0.17f, -0.1f);

        /// <summary>BASTION_TUCK=x,y,z: another place for the butt, for the labs' search (CombatLab.Agarres' table).</summary>
        static Vector3? Tried()
        {
            var parts = System.Environment.GetEnvironmentVariable("BASTION_TUCK")?.Split(',');
            if (parts == null || parts.Length < 3) return null;
            float F(int i) => float.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
            return new Vector3(F(0), F(1), F(2));
        }
        /// <summary>The left arm reaches this share of its length at most; the handguard's point slid back to it, no nearer the grip than this (metres, unscaled).</summary>
        const float TuckReach = 0.97f, TuckForeLeast = 0.34f;
        /// <summary>Seconds a gun takes to come under the arm or leave it (aiming, reloading, the left hand busy).</summary>
        const float TuckEase = 0.25f;

        /// <summary>Whether `art` is held tucked (its tuck points given).</summary>
        public static bool Tucks(WeaponArt art) => art?.tuck != null && art.tuck.Length >= 9;

        /// <summary>The gun tucked and both hands on it, after the body's pose; `k` how far (0 the clips' own, 1 all).</summary>
        public static void HoldTucked(Animator a, Transform gun, Transform root, WeaponArt art, float k)
        {
            if (k <= 0 || !Tucks(art) || a == null || gun == null || root == null || !a.isHuman) return;
            Transform shoulderR = a.GetBoneTransform(HumanBodyBones.RightUpperArm), shoulderL = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform handR = a.GetBoneTransform(HumanBodyBones.RightHand), handL = a.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform elbowL = a.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (shoulderR == null || shoulderL == null || handR == null || handL == null || elbowL == null) return;
            float scale = Figures.CharacterScale, model = gun.lossyScale.x;
            var t = art.tuck;
            Vector3 butt = new Vector3(t[0], t[1], t[2]), grip = new Vector3(t[3], t[4], t[5]), fore = new Vector3(t[6], t[7], t[8]);
            // The gun: the way it points, canted a little, its butt at the armpit.
            var ahead = Quaternion.AngleAxis(-TuckLeft, root.up) * root.forward;
            var across = Vector3.Cross(root.up, ahead).normalized;
            var way = Quaternion.AngleAxis(TuckDown, across) * ahead;
            var turn = Quaternion.AngleAxis(TuckCant, way) * Quaternion.LookRotation(way, root.up);
            var at = shoulderR.position + root.TransformDirection(TuckAt) * scale;
            // (From where the clips' own hold has it, `k` of the way.)
            gun.SetPositionAndRotation(Vector3.Lerp(gun.position, at - turn * (butt * model), k), Quaternion.Slerp(gun.rotation, turn, k));
            way = gun.forward;
            LastTuckDown = Mathf.Asin(Mathf.Clamp(-gun.forward.y, -1, 1)) * Mathf.Rad2Deg;
            // The left hand's place on the handguard: as far along as its arm reaches.
            float arm = (Vector3.Distance(shoulderL.position, elbowL.position) + Vector3.Distance(elbowL.position, handL.position)) * TuckReach;
            for (int step = 0; step < 12 && fore.z > grip.z + TuckForeLeast && Vector3.Distance(gun.TransformPoint(fore), shoulderL.position) > arm; step++) fore.z -= 0.03f;
            // The right hand onto the stock's wrist from behind, on its right side: the palm against that side, covering it top
            // to bottom, the fingers ahead and down (to close under it), the thumb left on top; the back of the hand seen from
            // the right. (Facing down on its top, the wood showed under the knuckles: «la mano encima», Producto, 18/11.)
            // (On edge at its side, the fingers straight down, half the hand hung under the stock as a flat blade: Producto,
            // 18/11.)
            Grasp(a, false, k, gun.TransformPoint(grip), (way * 0.8f - gun.up * 0.6f).normalized, (-gun.right * 0.97f - gun.up * 0.24f).normalized, root.TransformDirection(new Vector3(0.75f, -0.35f, -0.9f)));
            // The left under the handguard: the palm up against it, the fingers across to its far side.
            Grasp(a, true, k, gun.TransformPoint(fore), gun.right, gun.up, root.TransformDirection(new Vector3(-0.8f, -0.7f, 0.1f)));
            // (The fingers once it is mostly there: in between, the clips' own.)
            if (k < 0.5f) return;
            Fist(a, TuckIndex, TuckBend);
            Fist(a, TuckBend, TuckBend, true);
            // The right index out along the trigger guard, and the thumb over the stock's wrist to its far side.
            // The right index along the trigger guard's side, a little bent in to it (straight and lower, it pointed), and the
            // thumb up over the top of the stock's wrist, its tip to the far side.
            Point(a, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal, way - gun.up * 0.1f, way * 0.8f - gun.up * 0.1f - gun.right * 0.5f);
            if (a.GetBoneTransform(HumanBodyBones.RightThumbProximal) is Transform thumb && a.GetBoneTransform(HumanBodyBones.RightThumbIntermediate) is Transform thumbMid)
                Point(a, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
                    gun.TransformPoint(new Vector3(0.012f, grip.y + 0.055f, grip.z + 0.005f)) - thumb.position, gun.TransformPoint(new Vector3(-0.04f, grip.y + 0.03f, grip.z + 0.012f)) - thumbMid.position);
        }

        /// <summary>For the labs: how far under the level the last gun tucked pointed (degrees).</summary>
        public static float LastTuckDown;

        /// <summary>A finger laid out: its first bone the way `first` goes and its second the way `second` does (the world's), each turned as far as PointMost.</summary>
        static void Point(Animator a, HumanBodyBones one, HumanBodyBones two, HumanBodyBones three, Vector3 first, Vector3 second)
        {
            Transform p = a.GetBoneTransform(one), m = a.GetBoneTransform(two), d = a.GetBoneTransform(three);
            if (p == null || m == null || first.sqrMagnitude < 1e-8f || second.sqrMagnitude < 1e-8f) return;
            p.rotation = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(m.position - p.position, first.normalized), PointMost) * p.rotation;
            if (d != null) m.rotation = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(d.position - m.position, second.normalized), PointMost) * m.rotation;
        }

        const float PointMost = 85;

        /// <summary>How far the fingers close round a stock or a handguard, and the right index along the trigger guard (degrees: first joint, second).</summary>
        static readonly Vector2 TuckBend = new Vector2(70, 75), TuckIndex = new Vector2(12, 8);

        /// <summary>
        /// A hand onto a point: its palm's middle at `at`, its fingers the way `fingers` goes and its palm facing `palmward`
        /// (the world's), the elbow towards `pole`; turned, reached and turned again (the reach turns the forearm, and the hand
        /// with it).
        /// </summary>
        static void Grasp(Animator a, bool left, float k, Vector3 at, Vector3 fingers, Vector3 palmward, Vector3 pole)
        {
            HumanBodyBones B(HumanBodyBones r, HumanBodyBones l) => left ? l : r;
            Transform hand = a.GetBoneTransform(B(HumanBodyBones.RightHand, HumanBodyBones.LeftHand)), index = a.GetBoneTransform(B(HumanBodyBones.RightIndexProximal, HumanBodyBones.LeftIndexProximal)),
                little = a.GetBoneTransform(B(HumanBodyBones.RightLittleProximal, HumanBodyBones.LeftLittleProximal)), middle = a.GetBoneTransform(B(HumanBodyBones.RightMiddleProximal, HumanBodyBones.LeftMiddleProximal));
            if (hand == null || index == null || little == null || middle == null) return;
            var want = Quaternion.LookRotation(fingers, Vector3.ProjectOnPlane(palmward, fingers));
            // (`k` of the way from where the clips have the hand: its place and its turn.)
            var from = Vector3.Lerp(hand.position, middle.position, 0.6f);
            at = Vector3.Lerp(from, at, k);
            {
                var along0 = middle.position - hand.position;
                var palm0 = Vector3.Cross(along0, index.position - little.position) * (left ? -1 : 1);
                if (along0.sqrMagnitude > 1e-8f && palm0.sqrMagnitude > 1e-8f) want = Quaternion.Slerp(Quaternion.LookRotation(along0, Vector3.ProjectOnPlane(palm0, along0)), want, k);
            }
            for (int pass = 0; pass < 3; pass++)
            {
                var along = middle.position - hand.position;
                // (Fist's palm side: a mirror's on the left.)
                var palm = Vector3.Cross(along, index.position - little.position) * (left ? -1 : 1);
                if (along.sqrMagnitude < 1e-8f || palm.sqrMagnitude < 1e-8f) return;
                hand.rotation = want * Quaternion.Inverse(Quaternion.LookRotation(along, Vector3.ProjectOnPlane(palm, along))) * hand.rotation;
                var middleOfPalm = Vector3.Lerp(hand.position, middle.position, 0.6f);
                if (pass == 2) break;
                Reach(a, B(HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperArm), B(HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerArm), B(HumanBodyBones.RightHand, HumanBodyBones.LeftHand),
                    hand.position + (at - middleOfPalm), pole, 0.005f);
            }
        }
    }
}
