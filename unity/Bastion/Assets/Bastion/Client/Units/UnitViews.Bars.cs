using System;
using System.Threading;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;
using Grid = Bastion.Engine.Grid;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The caged dead behind a police station's cell bars (H9 of the runs; Arturo, 17/11: «los encerrados van hacia las
    /// verjas y pueden atacarte si estás cac y tú también»; Gameplay's LEEME-H9, «Los encerrados»). The bars are a tile
    /// (TileKinds.Bars), the pack's cell front on its middle line; a blow goes across them between the two tiles straight
    /// either side of them (Aim.AcrossBars), both ways. One caged on a tile of its cell by its bars stands pressed to them,
    /// turned as the state has it (its TurnedEvent: to the corridor), its arms out between them. Its bite there is a lunge
    /// into the bars, its arms thrust out between them, and the one it bites jerked towards them as it is hit; a soldier's
    /// blow at it steps up to the bars and drives the blade in between them (a thrust, whatever the blade: a swing would go
    /// through them), the melee watch's too; a silent takedown reaches in between them, the left hand to the head and the
    /// blade to the nape (UnitViews.Takedown). Whatever goes past the bars goes through a gap (Thread), and the trunks keep
    /// to their side (KeepBehind). Laboratory: CombatLab.Rejas (Editor/RejasLab.cs).
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>A line of bars seen from one side: their middle line on the floor (through their tile's middle), across it to the far side, and along it (the right of one facing across).</summary>
        sealed class BarsLine
        {
            public Pos tile;
            public Vector3 middle, normal, along;

            /// <summary>How far past the bars' middle line `p` is (m; below 0, on this side).</summary>
            public float Depth(Vector3 p) => Vector3.Dot(p - middle, normal);

            /// <summary>How far along the bars from their tile's middle `p` is (m).</summary>
            public float Across(Vector3 p) => Vector3.Dot(p - middle, along);

            /// <summary>The middle of the gap nearest `across` (m along the bars).</summary>
            public static float GapNear(float across) => Mathf.Round((across - GapOffset) / BarsPitch) * BarsPitch + GapOffset;

            /// <summary>The middle of gap `k` (0 on the tile's middle line, −1 the next to the left of one facing across, 1 to the right), `depth` past the bars, at height `y`.</summary>
            public Vector3 Gap(int k, float depth, float y)
            {
                var p = middle + along * (GapOffset + k * BarsPitch) + normal * depth;
                p.y = y;
                return p;
            }

            /// <summary>The same bars from the other side.</summary>
            public BarsLine Other => new BarsLine { tile = tile, middle = middle, normal = -normal, along = -along };
        }

        /// <summary>
        /// The pack's cell front (SM_Bld_Wall_Cell_01; Blender, 17/11): round bars 5.3 cm thick every 25 cm on the piece's
        /// middle line, drawn 1.2 times bigger (1.25 m of the pack to a 1.5 m tile): a bar every 0.30 m, 6.4 cm thick, so
        /// 23.6 cm free between two. Either half of a cell's front has a gap on its tile's middle line (4 mm off it, one way
        /// or the other by how the piece is turned: GapOffset none). Measured in the scene by CombatLab.Rejas ([rejas] barrotes).
        /// </summary>
        const float BarsPitch = 0.3f, BarRadius = 0.032f, GapOffset = 0;

        /// <summary>
        /// How far from the bars' middle line one caged stands pressed to them (its root, m): facing them (x), its face at
        /// them (the common dead lean their head 0.3–0.4 m ahead of their hips); its back to them (y), nearer; between, by how
        /// it faces. BASTION_REJAS_HUECO=front,back for the labs.
        /// </summary>
        static readonly Vector2 CagedGap = LabVector2("BASTION_REJAS_HUECO", new Vector2(0.42f, 0.3f));
        /// <summary>How fast one caged keeps pressed to its bars as it turns or is jolted (m a second), and how long its arms take to go out between them or come back (s).</summary>
        const float CagedKeep = 0.8f, CagedArmsIn = 0.4f;
        /// <summary>
        /// How far past the bars one caged's hands go as it waits (m), a lunge's further (CagedLungeOut, as far as its arms are
        /// up and closing: LungeArms), and how far past their middle line its trunk and face may come (to their near side:
        /// a head 17 cm wide would go in a gap of 23.6, but the crossbars come at a hunched one's chin).
        /// </summary>
        static readonly float CagedOut = LabFloat("BASTION_REJAS_MANOS", 0.3f);
        const float CagedLungeOut = 0.3f, CagedFace = -0.06f;
        /// <summary>One caged's lunge into its bars: how far it surges at most (m: its root no nearer than CagedGap.x less this), and how long it holds on with its arms out after the blow lands (s).</summary>
        const float BarsSurge = 0.1f, BarsHold = 0.2f;
        /// <summary>
        /// A soldier's blow through the bars: where its root stops from their middle line (m), a hand's breadth clear of them
        /// as the stroke bends it in; and its point driven on into the dead one's chest (Drive) over ThroughIn before the blow
        /// lands, held ThroughHold (its body kept at the bars meanwhile: at 10 fps a frame of it at least), let go over
        /// ThroughOut (s).
        /// </summary>
        static readonly float StrikeGap = LabFloat("BASTION_REJAS_GOLPE", 0.42f);
        const float ThroughIn = 0.14f, ThroughHold = 0.2f, ThroughOut = 0.25f;
        /// <summary>
        /// One grabbed through the bars (its bite's hit): its root jerked in to YankGap from their middle line (m; its gun's
        /// muzzle at rest a hand short of them), in over YankIn, held YankHold, back over YankOut (s); within YankFresh of the
        /// blow (s). BASTION_REJAS_SIN_TIRON=1: none, for the labs.
        /// </summary>
        static readonly float YankGap = LabFloat("BASTION_REJAS_TIRON", 0.65f);
        const float YankIn = 0.1f, YankHold = 0.12f, YankOut = 0.3f, YankFresh = 0.6f;
        static readonly bool YankOff = Environment.GetEnvironmentVariable("BASTION_REJAS_SIN_TIRON") == "1";
        /// <summary>
        /// A silent takedown through the bars (UnitViews.Takedown): where the soldier's body stops from their middle line (m),
        /// how near them the dead one's root is drawn back by its head (m), where in the thrust's clip (knifeStab) the blade
        /// waits wound up (s), and how far under the level it goes in (degrees). BASTION_REJAS_SILENCIO, BASTION_REJAS_ALZA.
        /// </summary>
        static readonly float TakeGap = LabFloat("BASTION_REJAS_SILENCIO", 0.45f), TakeRaise = LabFloat("BASTION_REJAS_ALZA", 0.22f);
        const float TakeHold = 0.24f, ThroughPitch = 12;
        /// <summary>A soldier's trunk kept this far short of the bars' middle line at least (m: off their near side; its gas mask is ahead of its face).</summary>
        const float SoldierClear = BarRadius + 0.04f;
        /// <summary>For the labs (BASTION_REJAS_ANTES=1): the caged and the blows across bars as before, to set side by side.</summary>
        public static bool BarsOff = Environment.GetEnvironmentVariable("BASTION_REJAS_ANTES") == "1";

        /// <summary>The bars beside `p` (a tile of them straight beside it, on its storey), the one most ahead along `forward` if two; null none.</summary>
        Pos BarsBeside(Pos p, Vector3 forward)
        {
            if (state == null || p == null) return null;
            Pos best = null;
            float most = float.MinValue;
            foreach (var d in Grid.Dirs4)
            {
                var q = new Pos { X = p.X + d.X, Y = p.Y + d.Y, Z = p.Z };
                if (!Grid.InBounds(state, q.X, q.Y) || Grid.KindAt(state, q) != TileKinds.Bars) continue;
                // (The map's y runs south: Unity's −z.)
                float ahead = forward.x * d.X - forward.z * d.Y;
                if (best == null || ahead > most) (best, most) = (q, ahead);
            }
            return best;
        }

        /// <summary>The bars on tile `bars` seen from tile `from` beside them.</summary>
        BarsLine LineOf(Pos from, Pos bars)
        {
            var middle = MapSpace.TileToWorld(state, bars);
            var normal = middle - MapSpace.TileToWorld(state, from);
            normal.y = 0;
            normal.Normalize();
            return new BarsLine { tile = bars, middle = middle, normal = normal, along = Vector3.Cross(Vector3.up, normal) };
        }

        /// <summary>The bars between `v` and `t` when a blow between them goes across them (Aim.AcrossBars), seen from `v`'s side; null else.</summary>
        BarsLine Across(View v, View t)
        {
            if (BarsOff || state == null || v?.place == null || t?.place == null || !Bastion.Engine.Aim.AcrossBars(state, v.place, t.place)) return null;
            return LineOf(v.place, new Pos { X = (v.place.X + t.place.X) / 2, Y = (v.place.Y + t.place.Y) / 2, Z = v.place.Z });
        }

        /// <summary>The bars one caged stands by on `p`, seen from its side; null if it is not caged or none are beside it.</summary>
        BarsLine CageOf(View v, Pos p) => !BarsOff && v.caged && p != null && state != null && BarsBeside(p, v.root.forward) is Pos bars ? LineOf(p, bars) : null;

        /// <summary>Where one caged stands pressed to its bars as it faces now (CagedGap: facing them, its back to them, between), at height `y`.</summary>
        static Vector3 PressedAt(View v, BarsLine line, float y)
        {
            float facing = Vector3.Dot(v.root.forward, line.normal);
            var at = line.middle - line.normal * Mathf.Lerp(CagedGap.y, CagedGap.x, (facing + 1) / 2);
            at.y = y;
            return at;
        }

        /// <summary>
        /// Where `v` stands on `p` (`at`: its tile's place): one caged by its bars, pressed to them, as it faces now or (`ahead`,
        /// a walk's end) facing them; one beside the motorbike, out from its side; anyone else at `at`.
        /// </summary>
        Vector3 StandOf(View v, Pos p, Vector3 at, bool ahead = false)
        {
            var line = CageOf(v, p);
            // (Nor against the motorbike, on a tile of its zone: out from its side, LaLata.StandBeside.)
            if (line == null) return Map.MapView.Current?.Lata?.StandBeside(at) ?? at;
            if (!ahead) return PressedAt(v, line, at.y);
            var front = line.middle - line.normal * CagedGap.x;
            front.y = at.y;
            return front;
        }

        /// <summary>
        /// Each frame (Step), one caged by its bars: kept pressed to them as it turns or is jolted (not walking, lunging, held
        /// by a takedown, eating, down or dead), and its arms out between them as far as it faces them and is by them.
        /// </summary>
        static void KeepToBars(View v, float dt)
        {
            bool by = v.cage != null && !v.dead;
            if (by && !v.walking && !v.barsLunge && !v.barsHeld && !v.feeding && !v.down)
                v.root.position = Vector3.MoveTowards(v.root.position, PressedAt(v, v.cage, v.root.position.y), CagedKeep * dt);
            bool out_ = by && Vector3.Dot(v.root.forward, v.cage.normal) > 0.5f && -v.cage.Depth(v.root.position) < CagedGap.x + 0.25f;
            v.barsArms = Mathf.MoveTowards(v.barsArms, out_ ? 1 : 0, dt / CagedArmsIn);
        }

        /// <summary>
        /// Where one caged's hand goes between its bars (DeadArms; `side` 1 its left, −1 its right): the left by the gap on its
        /// left of the one before it, the right by the one on its right, CagedOut past them (a lunge's further, as far as its
        /// arms are up and closing), at height `y`.
        /// </summary>
        static Vector3 CagedHand(View v, float side, float y) =>
            v.cage.Gap(side > 0 ? -1 : 1, CagedOut + CagedLungeOut * Mathf.Max(v.reachAim, v.reachGrab), y);

        /// <summary>
        /// One of the dead's blow at `t` across bars (Lunge: the caged one's bite through them): the headbutt over its upper
        /// body as ever, its arms up and thrust out between the bars as it lands (DeadArms by its bars, LungeArms' grab), its
        /// body surging into them BarsSurge at most, its face stopped in between two of them (KeepBehind); `t` grabbed (its
        /// HitReact jerks it towards the bars: Yank). Resolved as the blow lands, as Lunge; it holds on BarsHold and goes
        /// back by itself. It eats nobody through the bars (CanFeed).
        /// </summary>
        async Awaitable LungeThrough(View v, View t, BarsLine line, Action onHit, CancellationToken ct)
        {
            // (One not caged in a lab: these bars its own while it lunges.)
            v.cage ??= line;
            v.barsLunge = true;
            var home = v.root.position;
            var surge = line.normal * Mathf.Clamp(-line.Depth(home) - (CagedGap.x - BarsSurge), 0, BarsSurge);
            surge.y = 0;
            try
            {
                if (v.anim != null && FigureCatalog.Load()?.Clip("zombieHeadbutt") is AnimationClip butt) v.anim.PlayUpper(butt, WindUp, 1, HeadbuttLead);
                _ = LungeArms(v, ct);
                await Slide(v, home, home, WindUp, ct);
                await Slide(v, home, home + surge, StrokeTime, ct);
            }
            catch (OperationCanceledException)
            {
                v.barsLunge = false;
                throw;
            }
            if (!YankOff && t.root != null)
            {
                t.grabbed = line.Other;
                t.grabbedAt = Time.time;
            }
            onHit?.Invoke();
            _ = BackFromBars(v, home + surge, home, ct);
        }

        /// <summary>A lunge into the bars held a moment, its arms out, and back (LungeThrough).</summary>
        static async Awaitable BackFromBars(View v, Vector3 from, Vector3 home, CancellationToken ct)
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(BarsHold, ct);
                await Slide(v, from, home, Back, ct);
            }
            catch (OperationCanceledException) { }
            finally
            {
                v.barsLunge = false;
            }
        }

        /// <summary>The bars `v` is jerked towards as it is hit (grabbed through them a moment ago: LungeThrough), taken once; null none.</summary>
        static BarsLine TakeYank(View v)
        {
            var line = v.grabbed != null && Time.time - v.grabbedAt <= YankFresh ? v.grabbed : null;
            v.grabbed = null;
            return line;
        }

        /// <summary>
        /// Jerked towards the bars by one caged that grabbed it through them (HitReact, its bite's hit): its root in to YankGap
        /// from their middle line over YankIn, held YankHold, back over YankOut; dead meanwhile (the bite killed it), it stays
        /// where it is (Sync leaves a body whose tile is the same).
        /// </summary>
        static async Awaitable Yank(View v, BarsLine line, CancellationToken ct)
        {
            var home = v.root.position;
            var to = home + line.normal * Mathf.Max(0, -line.Depth(home) - YankGap);
            to.y = home.y;
            try
            {
                for (float t = 0; t < YankIn && v.root != null; t += Time.deltaTime)
                {
                    v.root.position = Vector3.Lerp(home, to, Mathf.SmoothStep(0, 1, t / YankIn));
                    await Awaitable.NextFrameAsync(ct);
                }
                if (v.root != null) v.root.position = to;
                await Awaitable.WaitForSecondsAsync(YankHold, ct);
                for (float t = 0; t < YankOut && v.root != null && !v.dead; t += Time.deltaTime)
                {
                    v.root.position = Vector3.Lerp(to, home, Mathf.SmoothStep(0, 1, t / YankOut));
                    await Awaitable.NextFrameAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
            if (v.root != null && !v.dead) v.root.position = home;
        }

        /// <summary>
        /// Each frame after every pose and the takedowns' (Step): the caged by their bars and whoever reaches through bars now
        /// (a blow, a takedown: View.through), the trunk kept on its side (KeepBehind: the caged one's face in between two
        /// bars at most, a soldier's clear of them) and each arm, with what its hand holds, through a gap where it goes past
        /// them (Thread).
        /// </summary>
        void StepBars()
        {
            if (BarsOff) return;
            foreach (var v in views.Values)
            {
                if ((v.cage == null && v.through == null) || v.root == null || v.missed > 0 || !v.root.gameObject.activeInHierarchy) continue;
                var a = v.animator;
                if (a == null || !a.isHuman) continue;
                // (One caged falling or lying: its body drawn back off the bars, its death's legs or arms had gone through them.)
                if (v.dead)
                {
                    if (v.cage != null) LieBehind(v, a, v.cage);
                    continue;
                }
                var line = v.through ?? v.cage;
                try
                {
                    // (A takedown's trunk is kept off them before its hands go in: UnitViews.Takedown StepTakedowns.)
                    if (!takingDown.ContainsKey(v)) KeepBehind(v, line, v.through != null ? -SoldierClear : CagedFace);
                    if (v.inHand == Held.Blade && v.weapon != null) HoldBlade(v);
                    if (v.through != null && v.throughShare > 0) Drive(v, a);
                    // (The caged one's hands each by its own gap, the head's left free: CagedHand.)
                    bool own = v.through == null;
                    Thread(v, line, false, own ? GapOffset - BarsPitch : (float?)null);
                    Thread(v, line, true, own ? GapOffset + BarsPitch : (float?)null);
                }
                catch (Exception e)
                {
                    // (Never the frame's other figures for one that fails: once, in red.log.)
                    if (!barsFailed)
                    {
                        barsFailed = true;
                        Debug.LogException(e);
                    }
                }
            }
        }

        bool barsFailed;

        /// <summary>
        /// A blow through the bars (Swing): its right hand moved so that the point goes on to where it is driven
        /// (View.throughAt: the dead one's chest), as far as View.throughShare and the arm goes (two-bone IK, the elbow the way
        /// it was), the blade put back in the fist; Thread then keeps it in between two bars.
        /// </summary>
        static void Drive(View v, Animator a)
        {
            Transform s = a.GetBoneTransform(HumanBodyBones.RightUpperArm), e = a.GetBoneTransform(HumanBodyBones.RightLowerArm), h = a.GetBoneTransform(HumanBodyBones.RightHand);
            if (s == null || e == null || h == null || v.inHand != Held.Blade || v.weapon == null || !(BladeLine(v) is var (_, tip))) return;
            Reach(a, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, h.position + (v.throughAt - tip) * v.throughShare, e.position - s.position, 0.02f);
            HoldBlade(v);
        }

        /// <summary>
        /// One caged that dies by its bars kept on its side of them as it falls and lies (StepBars): its root drawn back from
        /// them by as much as any of its bones (with the body about them) comes past their near side, a frame at a time.
        /// </summary>
        static void LieBehind(View v, Animator a, BarsLine line)
        {
            float size = Size(v), over = 0;
            foreach (var (bone, r) in LyingParts)
                if (a.GetBoneTransform(bone) is Transform b) over = Mathf.Max(over, line.Depth(b.position) + (r + BarRadius + 0.01f) * size);
            if (over > 1e-3f) v.root.position -= line.normal * over;
        }

        /// <summary>The bones of a body lying (LieBehind), with how far out the body is about each (m, a common figure's).</summary>
        static readonly (HumanBodyBones bone, float r)[] LyingParts =
        {
            (HumanBodyBones.Head, 0.1f), (HumanBodyBones.Neck, 0.06f), (HumanBodyBones.Chest, 0.14f), (HumanBodyBones.Hips, 0.14f),
            (HumanBodyBones.LeftUpperArm, 0.05f), (HumanBodyBones.RightUpperArm, 0.05f), (HumanBodyBones.LeftLowerArm, 0.04f), (HumanBodyBones.RightLowerArm, 0.04f),
            (HumanBodyBones.LeftHand, 0.05f), (HumanBodyBones.RightHand, 0.05f), (HumanBodyBones.LeftUpperLeg, 0.07f), (HumanBodyBones.RightUpperLeg, 0.07f),
            (HumanBodyBones.LeftLowerLeg, 0.055f), (HumanBodyBones.RightLowerLeg, 0.055f), (HumanBodyBones.LeftFoot, 0.06f), (HumanBodyBones.RightFoot, 0.06f),
            (HumanBodyBones.LeftToes, 0.04f), (HumanBodyBones.RightToes, 0.04f),
        };

        /// <summary>
        /// The trunk and the head kept on their side of the bars, `allowed` past their middle line at most: what of them goes
        /// furthest past (the skull, the neck, the chest, the shoulders, each with how far out the body is about it), the trunk
        /// tipped back from the bars by as much (half at the spine, half at the chest, about the line along them; 40° at
        /// most), twice over.
        /// </summary>
        static void KeepBehind(View v, BarsLine line, float allowed)
        {
            var a = v.animator;
            Transform spine = a.GetBoneTransform(HumanBodyBones.Spine), chest = a.GetBoneTransform(HumanBodyBones.Chest), head = a.GetBoneTransform(HumanBodyBones.Head),
                neck = a.GetBoneTransform(HumanBodyBones.Neck), upper = a.GetBoneTransform(HumanBodyBones.UpperChest),
                left = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), right = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (spine == null || chest == null || head == null) return;
            float size = Size(v);
            var axis = Vector3.Cross(line.normal, Vector3.up);
            for (int pass = 0; pass < 2; pass++)
            {
                float worst = 0, high = 0;
                void Part(Transform bone, Vector3 at, float r)
                {
                    if (bone == null) return;
                    float over = line.Depth(at) + r * size - allowed;
                    if (over > worst) (worst, high) = (over, at.y - spine.position.y);
                }
                var up = neck != null && (head.position - neck.position).sqrMagnitude > 1e-6f ? (head.position - neck.position).normalized : Vector3.up;
                Part(head, head.position + up * SkullUp * size, 0.1f);
                Part(neck, neck != null ? neck.position : default, 0.06f);
                Part(upper, upper != null ? upper.position : default, 0.13f);
                Part(chest, chest.position, 0.14f);
                Part(left, left != null ? left.position : default, 0.06f);
                Part(right, right != null ? right.position : default, 0.06f);
                if (worst <= 1e-3f) return;
                float degrees = Mathf.Min(40, worst / Mathf.Max(0.15f, high) * Mathf.Rad2Deg);
                var half = Quaternion.AngleAxis(degrees / 2, axis);
                spine.rotation = half * spine.rotation;
                chest.rotation = half * chest.rotation;
            }
        }

        /// <summary>
        /// An arm, and what its hand holds, kept in between two bars and two crossbars where it goes past them: where the
        /// upper arm, the forearm, the hand or the blade comes onto the bars (OnBars), if that is not in a gap with room for
        /// it there, the hand moved along the bars and up or down by as much as brings that into the gap (along them to its
        /// middle; up or down within its window between two crossbars), scaled by how far out the hand is from the shoulder
        /// over how far the bars are, reached again (two-bone IK, its elbow the way it was) and the blade put back in the
        /// fist; three times over. Nothing for an arm that keeps to its side. `gap`: that gap's middle (along the bars)
        /// rather than the nearest.
        /// </summary>
        void Thread(View v, BarsLine line, bool right, float? gap = null)
        {
            var a = v.animator;
            HumanBodyBones upper = right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm, lower = right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm,
                hand = right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand, knuckle = right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal;
            Transform s = a.GetBoneTransform(upper), e = a.GetBoneTransform(lower), h = a.GetBoneTransform(hand), k = a.GetBoneTransform(knuckle);
            if (s == null || e == null || h == null) return;
            bool blade = right && v.inHand == Held.Blade && v.weapon != null;
            for (int pass = 0; pass < 3; pass++)
            {
                if (!(OnBars(v, line, s.position, e.position, h.position, k != null ? k.position : h.position, blade) is var (across, high, r))) return;
                float middle = gap ?? BarsLine.GapNear(across), room = Mathf.Max(0, BarsPitch / 2 - BarRadius - r);
                float sideways = Mathf.Abs(across - middle) <= room ? 0 : middle - across;
                float upward = Window(high, r) - high;
                if (Mathf.Abs(sideways) < 1e-3f && Mathf.Abs(upward) < 1e-3f) return;
                float behind = -line.Depth(s.position);
                if (behind < 0.02f) return;
                float scale = Mathf.Clamp((line.Depth(h.position) + behind) / behind, 0.3f, 6);
                Reach(a, upper, lower, hand, h.position + (line.along * sideways + Vector3.up * upward) * scale, e.position - s.position, 0.005f);
                if (blade) HoldBlade(v);
            }
        }

        /// <summary>
        /// The pack's crossbars (SM_Bld_Wall_Cell_01; Blender, 17/11), over the floor, its 3 m storey stretched to the game's
        /// (×1.15): flat bars from 0.70 to 0.75 m, 1.10 to 1.15, 1.52 to 1.57 and 2.48 to 2.54, the foot under 0.11 and the
        /// top over 3.33. Between them, the windows an arm goes through. Measured in the scene by CombatLab.Rejas.
        /// </summary>
        static readonly Vector2[] BarsWindows = { new Vector2(0.113f, 0.698f), new Vector2(0.748f, 1.098f), new Vector2(1.147f, 1.519f), new Vector2(1.574f, 2.482f), new Vector2(2.536f, 3.329f) };

        /// <summary>The height nearest `high` (m over the floor) that something `r` thick goes through between two crossbars at.</summary>
        static float Window(float high, float r)
        {
            float best = high, far = float.MaxValue;
            foreach (var w in BarsWindows)
            {
                float lo = w.x + r + 0.005f, hi = w.y - r - 0.005f;
                if (hi < lo) continue;
                float at = Mathf.Clamp(high, lo, hi), off = Mathf.Abs(at - high);
                if (off < far) (best, far) = (at, off);
            }
            return best;
        }

        /// <summary>
        /// Where an arm (shoulder `s`, elbow `e`, wrist `h`, knuckles `k`, and the blade in its fist if `blade`) comes onto the
        /// bars: along them and how high over their floor, at the stretch that goes furthest off a gap of those that come
        /// within the bars' and its own thickness of their middle line (where it goes past it, or its end nearest it); and how
        /// thick that stretch is. Null if it keeps clear of them.
        /// </summary>
        (float across, float high, float r)? OnBars(View v, BarsLine line, Vector3 s, Vector3 e, Vector3 h, Vector3 k, bool blade)
        {
            float size = Size(v);
            float worst = float.MinValue, at = 0, up = 0, thick = 0;
            bool found = false;
            void Stretch(Vector3 p, Vector3 q, float r)
            {
                r *= size;
                float dp = line.Depth(p), dq = line.Depth(q), reach = BarRadius + r;
                if (Mathf.Max(dp, dq) < -reach || Mathf.Min(dp, dq) > reach) return;
                // (Where it goes past the middle line, else its end nearest it.)
                float u = (dp < 0) != (dq < 0) ? dp / (dp - dq) : Mathf.Abs(dp) < Mathf.Abs(dq) ? 0 : 1;
                var there = Vector3.Lerp(p, q, u);
                float across = line.Across(there), high = there.y - line.middle.y;
                float off = Mathf.Max(Mathf.Abs(across - BarsLine.GapNear(across)) - (BarsPitch / 2 - BarRadius - r), Mathf.Abs(Window(high, r) - high));
                if (off > worst) (worst, at, up, thick, found) = (off, across, high, r, true);
            }
            Stretch(s, e, 0.05f);
            Stretch(e, h, 0.04f);
            Stretch(h, k, 0.045f);
            if (blade && BladeLine(v) is var (grip, tip)) Stretch(grip, tip, 0.015f);
            return found ? (at, up, thick) : null;
        }

        /// <summary>For the labs: whether `id` is caged by its bars now and how far out between them its arms are, and whether it reaches through bars (a blow, a takedown).</summary>
        public (bool caged, float arms, bool through) BarsForLab(string id) =>
            id != null && views.TryGetValue(id, out var v) ? (v.cage != null, v.barsArms, v.through != null) : (false, 0, false);
    }
}
