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
        static readonly float TuckDown = Tried("BASTION_TUCK_ANG")?.x ?? 27, TuckLeft = Tried("BASTION_TUCK_ANG")?.y ?? 26, TuckCant = 12;
        /// <summary>
        /// Where the butt goes from the right shoulder's joint, in the root's frame (metres of a figure at scale 1; to its
        /// right, up, ahead): under the arm, between it and the ribs, the butt plate behind the elbow. (In the hollow of the
        /// shoulder, tried on 18/11, the stock went into the chest and the right arm crossed over it.)
        /// </summary>
        static readonly Vector3 TuckAt = Tried() ?? new Vector3(0.07f, -0.215f, -0.1f);

        /// <summary>
        /// Which way the right elbow goes, in the root's frame (to its right, up, ahead): out from the body, to leave the butt
        /// room under the arm (back and down by the ribs, the stock was 5–14 cm into the upper arm: CombatLab.Agarres, 18/11).
        /// </summary>
        static readonly Vector3 TuckElbow = Tried("BASTION_TUCK_CODO") ?? new Vector3(1f, 0.1f, -0.3f);

        /// <summary>BASTION_TUCK=x,y,z, BASTION_TUCK_CODO=x,y,z: another place for the butt, another way for the elbow, for the labs' search (CombatLab.Agarres' table).</summary>
        static Vector3? Tried(string name = "BASTION_TUCK") => Tried(name, 3) is Vector4 v ? (Vector3)v : (Vector3?)null;

        /// <summary>BASTION_TUCK_PASO=seconds,x,y,z: the way in and out (TuckEase, TuckRound).</summary>
        static Vector4? Tried(string name, int count)
        {
            var parts = System.Environment.GetEnvironmentVariable(name)?.Split(',');
            if (parts == null || parts.Length < count) return null;
            float F(int i) => i < count ? float.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture) : 0;
            return new Vector4(F(0), F(1), F(2), F(3));
        }
        /// <summary>The left arm reaches this share of its length at most; the handguard's point slid back to it, no nearer the grip than this (metres, unscaled).</summary>
        const float TuckReach = 0.97f;
        static readonly float TuckForeLeast = Tried("BASTION_TUCK_ANG")?.z ?? 0.34f;
        const float TuckForeLeastGripped = 0.26f;
        /// <summary>
        /// TuckAt and TuckElbow for a pistol-gripped gun (the assault rifle, the machine guns): its butt 8 cm further back and
        /// 3 out, the elbow higher. Their stocks are deeper and their bodies wider than a hunting rifle's: where that one
        /// goes, they were 7–8 cm into the right upper arm at rest and running (lab/pg.sh in Arte's T22 notes, 18/11); so, the
        /// assault rifle 0 at rest and 3–4 running, the machine guns 4–7.
        /// </summary>
        static readonly Vector3 TuckAtGripped = Tried() ?? new Vector3(0.1f, -0.22f, -0.18f), TuckElbowGripped = Tried("BASTION_TUCK_CODO") ?? new Vector3(1f, 0.4f, -0.2f);
        /// <summary>Seconds a gun takes to come under the arm or leave it (aiming, reloading, the left hand busy).</summary>
        static readonly float TuckEase = Tried("BASTION_TUCK_PASO", 4)?.x ?? 0.18f;
        /// <summary>
        /// Part way there the gun goes under the arm, not through it: off by this at the middle of the way, in the root's frame
        /// (metres of a figure at scale 1; to its right, up, ahead). (Straight from the clips' hold to under the arm, the stock
        /// crossed the upper arm 14–17 cm deep for three or four frames of 30 a second; so, 4–6, what it is at rest: lab/paso.sh
        /// in Arte's T22 notes, 18/11.)
        /// </summary>
        static readonly Vector3 TuckRound = Tried("BASTION_TUCK_PASO", 4) is Vector4 r ? new Vector3(r.y, r.z, r.w) : new Vector3(0, -0.18f, 0);

        /// <summary>Whether `art` is held tucked (its tuck points given).</summary>
        public static bool Tucks(WeaponArt art) => art?.tuck != null && art.tuck.Length >= 9;

        /// <summary>
        /// Whether a tucked gun's grip is a pistol's, under it (its `hand` given: the assault rifle and the machine guns, tucked
        /// since Producto's note of 18/11, «la culata sube por fuera del brazo hasta el hombro»): the right hand round it from
        /// its right, the knuckles ahead; and loaded as any rifle, by its clip, not by its bolt (Loading).
        /// </summary>
        static bool PistolGripped(WeaponArt art) => art?.hand != null && art.hand.Length >= 3;

        /// <summary>The gun tucked and both hands on it, after the body's pose; `k` how far (0 the clips' own, 1 all).</summary>
        public static void HoldTucked(Animator a, Transform gun, Transform root, WeaponArt art, float k) => HoldTucked(a, gun, root, art, k, 0, null);

        /// <summary>
        /// The same as it is loaded (Loading): `work` of the way to the gun low and across before the body, its butt by the
        /// right hip (LoadAt, LoadDown, LoadLeft), and the right hand off the stock's wrist at `right` (a point of the gun's
        /// frame, unscaled), its palm down on it.
        /// </summary>
        static void HoldTucked(Animator a, Transform gun, Transform root, WeaponArt art, float k, float work, Vector3? right)
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
            var ahead = Quaternion.AngleAxis(-Mathf.Lerp(TuckLeft, LoadLeft, work), root.up) * root.forward;
            var across = Vector3.Cross(root.up, ahead).normalized;
            var way = Quaternion.AngleAxis(Mathf.Lerp(TuckDown, LoadDown, work), across) * ahead;
            var turn = Quaternion.AngleAxis(TuckCant, way) * Quaternion.LookRotation(way, root.up);
            bool gripped = PistolGripped(art);
            var at = shoulderR.position + root.TransformDirection(gripped ? TuckAtGripped : Vector3.Lerp(TuckAt, LoadAt, work)) * scale;
            // (From where the clips' own hold has it, `k` of the way.)
            // (And round the arm in between: TuckRound.)
            var round = root.TransformDirection(TuckRound) * (scale * Mathf.Sin(k * Mathf.PI));
            gun.SetPositionAndRotation(Vector3.Lerp(gun.position, at - turn * (butt * model), k) + round, Quaternion.Slerp(gun.rotation, turn, k));
            way = gun.forward;
            LastTuckDown = Mathf.Asin(Mathf.Clamp(-gun.forward.y, -1, 1)) * Mathf.Rad2Deg;
            // The left hand's place on the handguard: as far along as its arm reaches.
            float arm = (Vector3.Distance(shoulderL.position, elbowL.position) + Vector3.Distance(elbowL.position, handL.position)) * TuckReach;
            // (A pistol-gripped one's, nearer: the machine gun's butt is long, and its handguard was out of the arm's reach,
            // the hand left short of it by its top.)
            float least = gripped ? TuckForeLeastGripped : Mathf.Lerp(TuckForeLeast, LoadForeLeast, work);
            for (int step = 0; step < 12 && fore.z > grip.z + least && Vector3.Distance(gun.TransformPoint(fore), shoulderL.position) > arm; step++) fore.z -= 0.03f;
            // The right hand onto the stock's wrist from behind, on its right side: the palm against that side, covering it top
            // to bottom, the fingers ahead and down (to close under it), the thumb left on top; the back of the hand seen from
            // the right. (Facing down on its top, the wood showed under the knuckles: «la mano encima», Producto, 18/11.)
            // (On edge at its side, the fingers straight down, half the hand hung under the stock as a flat blade: Producto,
            // 18/11.)
            // (Off it, loading: over the action from its right, the palm down on it.)
            float off = right is Vector3 r ? Mathf.Clamp01(Vector3.Distance(r, grip) / 0.06f) : 0;
            if (gripped)
            {
                // (A pistol grip: the palm against its right side, the knuckles ahead, the fingers closed round its front and
                // the index along the trigger guard.)
                Grasp(a, false, k, gun.TransformPoint(grip) + gun.right * (0.02f * model), (way * 0.9f + gun.up * 0.3f).normalized, -gun.right, root.TransformDirection(TuckElbowGripped));
                Grasp(a, true, k, gun.TransformPoint(fore), gun.right, gun.up, root.TransformDirection(new Vector3(-0.8f, -0.7f, 0.1f)));
                if (k < 0.5f) return;
                Fist(a, TriggerIndex, TriggerOthers);
                Fist(a, TuckBend, TuckBend, true);
                return;
            }
            var elbow = root.TransformDirection(Vector3.Lerp(TuckElbow, LoadElbow, work));
            Grasp(a, false, k, gun.TransformPoint(right ?? grip), Vector3.Slerp((way * 0.8f - gun.up * 0.6f).normalized, (way * 0.7f - gun.right * 0.7f).normalized, off),
                Vector3.Slerp((-gun.right * 0.97f - gun.up * 0.24f).normalized, (-gun.up * 0.9f - gun.right * 0.4f).normalized, off), elbow);
            // The left under the handguard: the palm up against it, the fingers across to its far side.
            Grasp(a, true, k, gun.TransformPoint(fore), gun.right, gun.up, root.TransformDirection(new Vector3(-0.8f, -0.7f, 0.1f)));
            // (The fingers once it is mostly there: in between, the clips' own.)
            if (k < 0.5f) return;
            Fist(a, off > 0 ? Vector2.Lerp(TuckIndex, LoadBend, off) : TuckIndex, off > 0 ? Vector2.Lerp(TuckBend, LoadBend, off) : TuckBend);
            Fist(a, TuckBend, TuckBend, true);
            if (off > 0.2f) return;
            // The right index out along the trigger guard, and the thumb over the stock's wrist to its far side.
            // The right index along the trigger guard's side, a little bent in to it (straight and lower, it pointed), and the
            // thumb up over the top of the stock's wrist, its tip to the far side.
            Point(a, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal, way - gun.up * 0.1f, way * 0.8f - gun.up * 0.1f - gun.right * 0.5f);
            if (a.GetBoneTransform(HumanBodyBones.RightThumbProximal) is Transform thumb && a.GetBoneTransform(HumanBodyBones.RightThumbIntermediate) is Transform thumbMid)
                Point(a, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
                    gun.TransformPoint(new Vector3(0.012f, grip.y + 0.055f, grip.z + 0.005f)) - thumb.position, gun.TransformPoint(new Vector3(-0.04f, grip.y + 0.03f, grip.z + 0.012f)) - thumbMid.position);
        }

        // ------------------------------------------------------------ loading (T25)

        /// <summary>
        /// A straight-stocked gun loaded (T25, Arturo 18/11, «que entre para esta»: the reload was the assault rifle's clip,
        /// the gun near upright, the left hand open in the air and the magazine loose for a frame): no clip of it, so the body
        /// as it stands or kneels, the gun brought low and across before it, its butt by the right hip, the left hand kept
        /// under the handguard; the right hand off the stock's wrist to the bolt's handle, the bolt back, the hand over the
        /// open action and down into it (the rounds in from above), the bolt home and the hand back; nothing loose. As long
        /// as any reload (ReloadSeconds: its sound and the battle's pace; Producto, 18/11).
        /// </summary>
        const float LoadSeconds = ReloadSeconds;
        /// <summary>Loading: where the butt goes from the right shoulder, the elbow's way, the muzzle under the level and to the left, the left hand's least place (as TuckAt, TuckElbow, TuckDown, TuckLeft, TuckForeLeast).</summary>
        static readonly Vector3 LoadAt = new Vector3(0.15f, -0.38f, -0.06f), LoadElbow = new Vector3(0.5f, -1f, -0.6f);
        const float LoadDown = 12, LoadLeft = 45, LoadForeLeast = 0.28f;
        /// <summary>The right hand's fingers over the action (degrees: first joint, second).</summary>
        static readonly Vector2 LoadBend = new Vector2(35, 30);
        /// <summary>
        /// From the stock's wrist (a tuck's second point) to the bolt's handle, to over the action ahead of it, and how far
        /// the bolt comes back and the hand goes down into the action (metres of the gun's frame, unscaled).
        /// </summary>
        static readonly Vector3 LoadHandle = new Vector3(0.006f, 0.11f, 0.04f), LoadOver = new Vector3(-0.04f, 0.17f, 0.1f);
        const float LoadPull = 0.13f, LoadIn = 0.06f;
        /// <summary>Its beats (seconds): low by, the hand at the handle, the bolt back, over the action, in, out, at the bolt, home, on the wrist again; the gun back under the arm from LoadUp.</summary>
        static readonly float[] LoadBeats = { 0.13f, 0.3f, 0.43f, 0.59f, 0.71f, 0.81f, 0.92f, 1.04f, 1.2f };
        const float LoadLow = 0.17f, LoadUp = 1.13f;

        /// <summary>`t` seconds into a loading: how far the gun is low and across, and where the right hand is on it (the gun's frame, unscaled).</summary>
        static (float work, Vector3 right) Loading(float t, WeaponArt art)
        {
            var tuck = art.tuck;
            var grip = new Vector3(tuck[3], tuck[4], tuck[5]);
            // (The hunting rifles' handle as T14 measured it, UnitViews.Bolt.)
            var handle = art.fbx != null && BoltGuns.Contains(art.fbx) ? BoltHandle : grip + LoadHandle;
            Vector3 back = handle + Vector3.back * LoadPull, over = new Vector3(grip.x, grip.y, grip.z) + LoadOver, inside = over + Vector3.down * LoadIn;
            var points = new[] { grip, handle, back, over, inside, over, back, handle, grip };
            float work = t < LoadUp ? Mathf.SmoothStep(0, 1, t / LoadLow) : Mathf.SmoothStep(1, 0, (t - LoadUp) / (LoadSeconds - LoadUp));
            if (t <= LoadBeats[0]) return (work, grip);
            for (int i = 1; i < LoadBeats.Length; i++)
                if (t <= LoadBeats[i]) return (work, Vector3.Lerp(points[i - 1], points[i], Mathf.SmoothStep(0, 1, (t - LoadBeats[i - 1]) / (LoadBeats[i] - LoadBeats[i - 1]))));
            return (work, grip);
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
            // (And the elbow's side: part way, the arm swung whole to `pole` went through a gun not yet there, 17 cm deep the
            // frame a reload ended. CombatLab.Agarres, 18/11.)
            if (k < 1 && a.GetBoneTransform(B(HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperArm)) is Transform upper && a.GetBoneTransform(B(HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerArm)) is Transform lower)
            {
                var own = Vector3.ProjectOnPlane(lower.position - upper.position, hand.position - upper.position);
                if (own.sqrMagnitude > 1e-8f && pole.sqrMagnitude > 1e-8f) pole = Vector3.Slerp(own.normalized, pole.normalized, k);
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
