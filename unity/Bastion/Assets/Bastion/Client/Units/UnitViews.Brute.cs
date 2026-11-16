using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bastion.Client.Map;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The boss of the runs' first chapter, the Brute (Arturo, 16/11: «el muerto único»; Gameplay's rules of 16/11, PLAN-RUNS
    /// §3.2): Boss Zombies' Brute (DeadBodies), a quarter bigger than the figures, hunched as the Chillón (its walk, its lean,
    /// its idle). What UI's event player plays on it: its roar as it sees the squad (Roar), its blow with both fists from
    /// above (Smash), its charge in a straight line, shoulder first (Charge), and on the soldier it charges, knocked down to
    /// the floor alive (KnockDown) till it gets up (GetUp); its death, heavy (Die's Thump). As the rest of UnitViews: what is
    /// waited for ends once it has been seen whole, the moment inside it (the blow landing) comes as an Action, and what
    /// goes on after it (the step back) goes on by itself; one that fails lands all the same, the error in red.log.
    /// </summary>
    public sealed partial class UnitViews
    {
        // ------------------------------------------------------------ the roar

        /// <summary>
        /// The Brute's roar as it sees the squad (Gameplay, 16/11: the Chillón's, without its psychic rings): Bellow, as the
        /// scream, «Roar High» over the upper body, the trunk and head thrown back and the arms up, held longer (a boss's,
        /// read from the game's camera), from a little of the clip's wind-up (its crouch, 0.6–0.8 s) on; as it is all the way
        /// up, the dust round its feet blown out and a ring of it rolling over the floor (Fx.Roar). Resolved once it has been
        /// seen whole (in and held); it lets go over BruteRoarOut by itself. UI turns it to the squad first if it isn't.
        /// </summary>
        public async Awaitable Roar(string id, CancellationToken ct = default) =>
            await Bellow(id, BruteRoarIn, BruteRoarHold, BruteRoarOut, BruteRoarFrom, BruteRoarTo, v => Effects.Fx.Current?.Roar(v.root.position, Size(v)), ct);

        /// <summary>The roar's in, held and out (seconds), and the part of «Roar High» played over them (its seconds: RoarClip's notes).</summary>
        const float BruteRoarIn = 0.3f, BruteRoarHold = 0.9f, BruteRoarOut = 0.4f, BruteRoarFrom = 0.9f, BruteRoarTo = 2.3f;

        // ------------------------------------------------------------ the blow

        /// <summary>
        /// The Brute's blow at `target` (Gameplay, 16/11: «golpe» of 6): both fists together brought down on its head from
        /// over its own. Its trunk and head as the blow from above has them (macheteChop, «Standing Melee Attack Downward»:
        /// ChopStroke's, the takedown's) on its own clock over the trunk, head and right arm, rearing back as it winds up,
        /// bent over the blow; both fists by two-bone IK on a way of their own (SmashHands), side by side: up from where they
        /// are to over its head and a little behind it (SmashWind), held there a moment (SmashHold), brought down fast and
        /// quickening over the top (SmashStroke) onto the target's head, kept where they land as it reels (SmashIn), lifted and
        /// back to where they were, then let go (SmashOut). On the clip alone its right fist went out wide to the side, an
        /// axe's swing, and landed a metre to one side of the soldier, the left arm out the other way (CombatLab.Brute, 16/11).
        /// No step in (SmashMost): its arms reach a soldier on the tile beside it, and its legs walking the step bowed it over
        /// the goblin walk's way, its head down at the soldier's.
        /// About 0.76 s to land, against the common dead's headbutt's 0.34 (Lunge). `onHit` as it lands, and resolved there
        /// (as Lunge); the rest goes on by itself.
        /// </summary>
        public async Awaitable Smash(string id, string target, Action onHit = null, CancellationToken ct = default)
        {
            View t = target != null && views.TryGetValue(target, out var found) ? found : null;
            if (!views.TryGetValue(id, out var v) || v.anim == null || v.root == null || v.animator == null || !v.animator.isHuman)
            {
                await Lunge(id, target, false, onHit, ct);
                return;
            }
            bool landed = false;
            void Land()
            {
                if (landed) return;
                landed = true;
                onHit?.Invoke();
            }
            _ = SmashOn(v, t, FigureCatalog.Load()?.Clip(ChopStroke.clip), Land, ct);
            while (!landed && !ct.IsCancellationRequested) await Awaitable.NextFrameAsync(ct);
        }

        /// <summary>
        /// The blow's clock (seconds): wound up, held up, brought down (it lands at their sum), kept on the head, let go; the
        /// share's way in. The clip's times over them: up and back (the hand highest and furthest back 0.42–0.55 s, ClipProbe
        /// BASTION_MANOS=1), where the blow lands (before the chest, 0.83), and how fast it goes on after (slowly: the trunk
        /// bent over the blow a while), and how much of it the trunk takes (SmashTrunk).
        /// </summary>
        static readonly float SmashWind = LabFloat("BASTION_BRUTO_ALZA", 0.5f), SmashHold = 0.12f, SmashStroke = LabFloat("BASTION_BRUTO_TRAZO", 0.14f);
        static readonly float SmashIn = 0.35f, SmashOut = 0.4f, SmashFadeIn = 0.15f;
        static readonly float SmashRaise = LabFloat("BASTION_BRUTO_ARRIBA", 0.5f), SmashRelease = LabFloat("BASTION_BRUTO_SUELTA", 0.8f), SmashInRate = LabFloat("BASTION_BRUTO_TRAS", 0.12f);
        static readonly float SmashTrunk = LabFloat("BASTION_BRUTO_TRONCO", 0.6f), SmashFrom = LabFloat("BASTION_BRUTO_DESDE", 0.2f);
        /// <summary>
        /// The fists' way (metres at a figure's size, Size): over the head at the top (SmashOver up from its head, SmashBehind
        /// back from it), how far out ahead their arcs bow (up to it and down from it), and on the head they strike, how far
        /// over its top and short of it towards the Brute the wrists stop (the fist's own size under them), and how far they
        /// sink as it reels.
        /// </summary>
        static readonly float SmashOver = LabFloat("BASTION_BRUTO_SOBRE", 0.55f), SmashBehind = 0.1f, SmashBow = 0.3f;
        static readonly float SmashOnHead = LabFloat("BASTION_BRUTO_CABEZA", 0.28f), SmashShortOf = LabFloat("BASTION_BRUTO_ANTES", 0.1f), SmashSink = 0.08f;
        /// <summary>Going back: how far up and back the fists come off the head on their way to where they were (m, Size), and when in it they begin to be let go (of SmashOut).</summary>
        const float SmashLift = 0.25f, SmashLetGo = 0.6f;
        /// <summary>
        /// Its step in (tiles: the gap less SmashShort, SmashMost at most; a common blow's LungeShort and LungeMost are 0.72 and
        /// 0.45: the Brute's arms are longer), how long it stays in after the blow lands and how long it takes to step back.
        /// </summary>
        static readonly float SmashShort = LabFloat("BASTION_BRUTO_CORTO", 0.85f), SmashMost = LabFloat("BASTION_BRUTO_PASO", 0);
        const float SmashStay = 0.35f, SmashBack = 0.45f;
        /// <summary>How far each wrist is from the fists' middle (metres of the model: the fists side by side), and the dust as the blow lands (Fx.Thud's size).</summary>
        const float SmashApart = 0.1f, SmashThud = 0.6f;

        /// <summary>The blow, on by itself (Smash); landed whatever happens (as StrokeOn), and put right after.</summary>
        async Awaitable SmashOn(View v, View t, AnimationClip clip, Action land, CancellationToken ct)
        {
            v.striking = true;
            try
            {
                await Smashing(v, t, clip, land, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                v.striking = false;
                v.smash = 0;
                v.anim?.Scrub(null, 0, 0, true);
                if (v.root != null && !v.dead)
                {
                    if (v.body != null) v.body.localPosition = Vector3.zero;
                    if (!v.walking) v.anim?.Motion(IdleOf(v));
                }
                if (!ct.IsCancellationRequested)
                {
                    try
                    {
                        land();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        async Awaitable Smashing(View v, View t, AnimationClip clip, Action land, CancellationToken ct)
        {
            var home = v.root.position;
            if (t != null)
            {
                // A blow from where it strikes from (the target's hit and death go by it: UnitViews.Blows), not one at arm's
                // length: from above on the head it throws the head back (hitHeadF), and a death falls away from it. As one
                // at arm's length the soldier doubled over onto its arms, 16 cm into its right forearm (CombatLab.Brute, 30 fps).
                StruckBy(t, home, false);
                Face(v.id, t.root.position);
            }
            // The step in on its body, not its root (a Sync meanwhile puts the root back on its tile), its legs walking it
            // at the pace it goes, backwards on the way back.
            var toward = t != null ? Reach(home, t.root.position, SmashShort, SmashMost) : Vector3.zero;
            var (one, _) = Gaits.Of(v.template);
            float walk = Gaits.ClipSpeed(v.template, one) / Stride(v), pace = MapSpace.Tile / one.seconds;
            float raised = SmashWind + SmashHold, hit = raised + SmashStroke, outFrom = hit + SmashIn, end = outFrom + SmashOut;
            float back = hit + SmashStay, backHome = back + SmashBack;
            bool stepping = toward.sqrMagnitude > 1e-6f, struck = false, there = false, backing = false;
            if (stepping) v.anim.Motion("walk", walk * toward.magnitude / SmashWind / pace);
            // The fists' way, in the body's frame as it steps and turns: from where they are (its arms out before it), up
            // to over its head (its head where it is now), down onto the target's head (where it is each frame).
            var a = v.animator;
            float size = Size(v);
            Vector3 Local(Vector3 p) => Quaternion.Inverse(v.body.rotation) * (p - v.body.position);
            Vector3 World(Vector3 p) => v.body.position + v.body.rotation * p;
            var rest = Local((a.GetBoneTransform(HumanBodyBones.LeftHand).position + a.GetBoneTransform(HumanBodyBones.RightHand).position) / 2);
            var top = Local(a.GetBoneTransform(HumanBodyBones.Head).position) + new Vector3(0, SmashOver, -SmashBehind) * size;
            top.x = 0;
            Vector3 OnHead()
            {
                if (t == null || t.root == null) return World(top + new Vector3(0, -SmashOver * 2, 0.8f) * size);
                var along = t.root.position - v.body.position;
                along.y = 0;
                along = along.sqrMagnitude > 1e-6f ? along.normalized : v.root.forward;
                return Head(t.id) + Vector3.up * SmashOnHead * size - along * SmashShortOf * size;
            }
            static Vector3 Arc(Vector3 p0, Vector3 bow, Vector3 p1, float u) => (1 - u) * (1 - u) * p0 + 2 * u * (1 - u) * bow + u * u * p1;
            Vector3 hitAt = Vector3.zero, left = rest;
            bool landing = false;
            for (float s = 0; s < Mathf.Max(end, backHome); s += Time.deltaTime)
            {
                if (v.root == null || v.dead || v.body == null) return;
                // (Kept whole as the fists go back to where they were, then let go to its arms' own: faded on the soldier's
                // head, the arms came down through the gun in its hands, CombatLab.Brute's [cuerpo].)
                float share = Mathf.Clamp01(Mathf.Min(s / SmashFadeIn, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(SmashLetGo, 1, (s - outFrom) / SmashOut))));
                v.smash = share;
                if (clip != null)
                {
                    // (From SmashFrom: before it the clip bows the trunk over, its head down to the soldier's.)
                    float at = s < SmashWind ? Mathf.Lerp(SmashFrom, SmashRaise, Mathf.SmoothStep(0, 1, s / SmashWind))
                        : s < raised ? SmashRaise
                        : s < hit ? SmashRaise + (SmashRelease - SmashRaise) * Sq((s - raised) / SmashStroke)
                        : SmashRelease + (s - hit) * SmashInRate;
                    v.anim.Scrub(share * SmashTrunk > 0 ? clip : null, Mathf.Clamp(at, 0, clip.length), share * SmashTrunk, true);
                }
                float k = s < SmashWind ? Mathf.SmoothStep(0, 1, s / SmashWind) : s < back ? 1 : s < backHome ? 1 - Mathf.SmoothStep(0, 1, (s - back) / SmashBack) : 0;
                v.body.localPosition = v.root.InverseTransformVector(toward * k);
                // Where the fists' middle goes now. (Kept where they land, sinking a little: after the head as it reeled
                // they went into the soldier's back.)
                var onHead = Local(OnHead());
                if (s >= hit && !landing)
                {
                    landing = true;
                    hitAt = World(onHead);
                }
                v.smashAt = World(s < SmashWind ? Arc(rest, (rest + top) / 2 + new Vector3(0, 0.15f, SmashBow) * size, top, Mathf.SmoothStep(0, 1, s / SmashWind))
                    : s < raised ? top
                    : s < hit ? Arc(top, new Vector3(0, top.y, Mathf.Lerp(top.z, onHead.z, 0.75f) + SmashBow * size), onHead, Sq((s - raised) / SmashStroke))
                    : s < outFrom ? Local(hitAt) - Vector3.up * SmashSink * size * Mathf.SmoothStep(0, 1, (s - hit) / SmashIn)
                    : Arc(left, (left + rest) / 2 + new Vector3(0, SmashLift, -SmashLift) * size, rest, Mathf.SmoothStep(0, 1, (s - outFrom) / SmashOut)));
                if (s < outFrom) left = Local(v.smashAt);
                if (stepping && !there && s >= SmashWind)
                {
                    there = true;
                    v.anim.Motion(IdleOf(v));
                    StartLanding(v);
                }
                if (stepping && !backing && s >= back)
                {
                    backing = true;
                    v.anim.Motion("walk", -walk * toward.magnitude / SmashBack / pace);
                }
                if (backing && stepping && s >= backHome)
                {
                    stepping = false;
                    v.anim.Motion(IdleOf(v));
                    StartLanding(v);
                }
                if (!struck && s >= hit)
                {
                    struck = true;
                    // (Its weight on its front foot as the fists come down: dust off the floor between them.)
                    if (t != null && t.root != null) Effects.Fx.Current?.Thud(Vector3.Lerp(home + toward, t.root.position, 0.5f), SmashThud);
                    land();
                }
                await Awaitable.NextFrameAsync(ct);
            }
        }

        /// <summary>
        /// Both fists on the blow's way (Smash), side by side about View.smashAt (SmashApart each side of it across the body),
        /// as far as it is in (View.smash), by two-bone IK, the elbows out, down and back; after the dead's own pose (DeadArms
        /// let go meanwhile) and over the blow's clip's right arm.
        /// </summary>
        static void SmashHands(View v)
        {
            var a = v.animator;
            if (a == null || !a.isHuman || v.dead) return;
            float scale = v.body != null ? v.body.lossyScale.x : 1;
            foreach (var (side, upper, lower, handBone) in new[] { (-1f, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
                (1f, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) })
            {
                var hand = a.GetBoneTransform(handBone);
                if (hand == null) continue;
                var goal = Vector3.Lerp(hand.position, v.smashAt + v.root.right * side * SmashApart * scale, v.smash);
                var pole = v.root.rotation * new Vector3(side * 0.8f, -0.3f, -0.4f);
                for (int pass = 0; pass < 2; pass++) Reach(a, upper, lower, handBone, goal, pole, SoftReach * scale);
            }
            if (BruteLog)
            {
                string In(Vector3 p) => FormattableString.Invariant($"({v.root.InverseTransformPoint(p).x:0.00},{v.root.InverseTransformPoint(p).y:0.00},{v.root.InverseTransformPoint(p).z:0.00})");
                Debug.Log(FormattableString.Invariant($"[mazazo] {Time.time:0.000} share {v.smash:0.00} at {In(v.smashAt)} left {In(a.GetBoneTransform(HumanBodyBones.LeftHand).position)} right {In(a.GetBoneTransform(HumanBodyBones.RightHand).position)}"));
            }
        }

        /// <summary>BASTION_BRUTO_LOG=1: the Brute's hands through its blow, a line [mazazo] a frame (the labs).</summary>
        static readonly bool BruteLog = Environment.GetEnvironmentVariable("BASTION_BRUTO_LOG") == "1";

        // ------------------------------------------------------------ the charge

        /// <summary>
        /// The Brute's charge (Gameplay, 16/11: «embestida» in a straight line, that knocks the soldier down): along `path`
        /// (its tiles, the last one beside `target`) at a sprint (goblinSprint, the dead's), up to speed over ChargeRamp and
        /// never slowing, its trunk down and turned a shoulder ahead (View.charge: DeadPose), dust off its feet; on past its
        /// last tile into the soldier (its body, ChargeContact off its middle), `onImpact` there and resolved there (UI's
        /// KnockDown then); its weight carries it on a little (ChargeSkid) and it steps back onto its tile, a step from the
        /// soldier, by itself.
        /// </summary>
        public async Awaitable Charge(string id, IReadOnlyList<Pos> path, string target, Action onImpact = null, CancellationToken ct = default)
        {
            View t = target != null && views.TryGetValue(target, out var found) ? found : null;
            if (!views.TryGetValue(id, out var v) || v.anim == null || v.root == null || state == null || path == null || path.Count == 0)
            {
                if (v != null && state != null && path != null && path.Count > 0) await MoveAlong(id, path, 2, 1, ct);
                onImpact?.Invoke();
                return;
            }
            bool hit = false;
            void Impact()
            {
                if (hit) return;
                hit = true;
                onImpact?.Invoke();
            }
            _ = ChargeOn(v, path, t, Impact, ct);
            while (!hit && !ct.IsCancellationRequested) await Awaitable.NextFrameAsync(ct);
        }

        /// <summary>
        /// The charge's pace: seconds a tile at full speed (the Corredor's sprint is 0.2), seconds to get up to it, to lean
        /// in, and how often dust comes off its feet; where its middle stops from the soldier's as they meet (tiles: its
        /// shoulder against the soldier's chest), how far its weight carries it on after (tiles) and over how long, how long it
        /// takes to step back onto its tile, and the dust as they meet (Fx.Thud's size).
        /// </summary>
        static readonly float ChargeTile = LabFloat("BASTION_BRUTO_CARRERA", 0.26f), ChargeContact = LabFloat("BASTION_BRUTO_CHOQUE", 0.9f);
        const float ChargeRamp = 0.35f, ChargeLeanIn = 0.3f, ChargeDust = 0.24f, ChargeSkid = 0.08f, ChargeSkidTime = 0.18f, ChargeBack = 0.55f, ChargeThud = 0.8f;
        /// <summary>
        /// How far down and round charging bends it (radians: over DeadStyles' lean, and the trunk's twist, the left shoulder
        /// ahead). Not down: goblinSprint is bent over already, and 0.3 more had it diving head first under the soldier.
        /// </summary>
        static readonly float ChargeLean = LabFloat("BASTION_BRUTO_INCLINA", -0.1f), ChargeTwist = LabFloat("BASTION_BRUTO_GIRO", 0.45f);

        /// <summary>The charge, on by itself (Charge); its impact whatever happens, and on its last tile after.</summary>
        async Awaitable ChargeOn(View v, IReadOnlyList<Pos> path, View t, Action impact, CancellationToken ct)
        {
            try
            {
                await Charging(v, path, t, impact, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                v.walking = false;
                v.charge = 0;
                if (v.root != null && state != null && !v.dead)
                {
                    if (v.body != null) v.body.localPosition = Vector3.zero;
                    v.root.position = MapSpace.StandAt(state, path[path.Count - 1]);
                    v.place = path[path.Count - 1];
                }
                if (!ct.IsCancellationRequested)
                {
                    try
                    {
                        impact();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        async Awaitable Charging(View v, IReadOnlyList<Pos> path, View t, Action impact, CancellationToken ct)
        {
            v.reachFor = null;
            if (v.feeding) await GetUp(v, ct);
            var points = new List<Vector3> { v.root.position };
            foreach (var p in path) points.Add(MapSpace.StandAt(state, p));
            var way = RoundedWay(points);
            // On past its last tile towards the soldier (its body: the root stays on its tile), till they meet.
            var last = points[points.Count - 1];
            var ahead = t != null && t.root != null ? t.root.position - last : points.Count > 1 ? last - points[points.Count - 2] : v.root.forward;
            ahead.y = 0;
            float over = t != null ? Mathf.Max(0, ahead.magnitude - ChargeContact * MapSpace.Tile) : 0;
            ahead = ahead.sqrMagnitude > 1e-6f ? ahead.normalized : v.root.forward;
            float length = way.length + over, top = MapSpace.Tile / ChargeTile;
            float sprint = Gaits.ClipSpeed(v.template, new Gaits.Gait("sprint", ChargeTile)) / Stride(v);
            v.walking = true;
            v.anim.Motion("sprint", 0);
            float d = 0, speed = 0, clock = 0, dust = 0;
            int passed = 0;
            while (d < length)
            {
                await Awaitable.NextFrameAsync(ct);
                if (v.root == null || v.dead) return;
                float dt = Time.deltaTime;
                clock += dt;
                // (Up to speed at once, then never slowing: «sin frenar hasta el soldado».)
                speed = Mathf.Min(top, speed + top / ChargeRamp * dt);
                d = Mathf.Min(length, d + speed * dt);
                v.charge = Mathf.Min(1, clock / ChargeLeanIn);
                v.anim.Motion("sprint", sprint * speed / top);
                var (at, heading) = OnWay(way, Mathf.Min(d, way.length));
                v.root.position = at;
                if (v.body != null) v.body.localPosition = v.root.InverseTransformVector(ahead * Mathf.Max(0, d - way.length));
                if (d > way.length) heading = ahead;
                if (heading.x * heading.x + heading.z * heading.z > 1e-8f) TurnTo(v, Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg);
                while (passed < path.Count && passed < way.marks.Count && d >= way.marks[passed] - 1e-4f) v.place = path[passed++];
                dust += dt;
                if (dust >= ChargeDust && speed > top * 0.5f)
                {
                    dust = 0;
                    Effects.Fx.Current?.Kick(BodyAt(v));
                }
            }
            v.place = path[path.Count - 1];
            // They meet: the soldier's (UI: KnockDown), the dust off the floor between them.
            var met = BodyAt(v) + ahead * (ChargeContact * 0.5f * MapSpace.Tile);
            impact();
            Effects.Fx.Current?.Thud(met, ChargeThud);
            // Its weight carries it on a little as its legs stop, then it steps back onto its tile, its lean let go.
            float from = over, to = over + ChargeSkid * MapSpace.Tile;
            for (float s = 0; s < ChargeSkidTime; s += Time.deltaTime)
            {
                float u = s / ChargeSkidTime;
                if (v.body != null) v.body.localPosition = v.root.InverseTransformVector(ahead * Mathf.Lerp(from, to, 1 - (1 - u) * (1 - u)));
                v.anim.Motion("sprint", sprint * (1 - u) * 0.5f);
                await Awaitable.NextFrameAsync(ct);
                if (v.root == null || v.dead) return;
            }
            var (one, _) = Gaits.Of(v.template);
            float walk = Gaits.ClipSpeed(v.template, one) / Stride(v);
            v.anim.Motion("walk", -walk * to / ChargeBack / (MapSpace.Tile / one.seconds));
            for (float s = 0; s < ChargeBack; s += Time.deltaTime)
            {
                float u = s / ChargeBack;
                if (v.body != null) v.body.localPosition = v.root.InverseTransformVector(ahead * to * (1 - Mathf.SmoothStep(0, 1, u)));
                v.charge = 1 - Mathf.SmoothStep(0, 1, u);
                await Awaitable.NextFrameAsync(ct);
                if (v.root == null || v.dead) return;
            }
            if (v.body != null) v.body.localPosition = Vector3.zero;
            v.charge = 0;
            v.walking = false;
            v.anim.Motion(IdleOf(v));
            StartLanding(v);
        }

        // ------------------------------------------------------------ knocked down

        /// <summary>
        /// The soldier the Brute's charge knocks down (Gameplay, 16/11: on the floor till the squad's next turn begins, getting
        /// up costs it an action): down in one of its deaths (DeathFor: away from `from`, where the Brute hit it from; room for
        /// it, else the least of them), in quickly (KnockIn), tucked into its own tile (Tuck: the engine leaves it there, Gameplay
        /// 16/11, «cae en su casilla»; thrown its death's reach away it lay a tile and more off it, where the ring and the next
        /// blow were not), dust as it hits the floor; no death's flash. It lies there alive (View.down, `dead` for its pose: Sync leaves it so) till
        /// GetUp. Resolved once it lies still. Hit meanwhile, its flash only; killed, it dies where it lies (DieDown).
        /// </summary>
        public async Awaitable KnockDown(string id, Vector3 from, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v) || v.root == null || v.dead) return;
            v.down = true;
            v.dead = true;
            v.aiming = false;
            v.picking = null;
            v.aimAt = null;
            StruckBy(v, from, false);
            bool cramped = false;
            var chosen = v.anim != null ? DeathFor(v, out cramped) : null;
            var death = chosen != null ? FigureCatalog.Load()?.Clip(chosen.id) : null;
            v.downId = chosen?.id;
            if (death == null)
            {
                // (A stand-in, or no death: over on its back as one dying.)
                for (float t = 0; t < 0.7f && v.body != null && v.anim == null; t += Time.deltaTime)
                {
                    v.body.localRotation = Quaternion.Euler(-90 * Mathf.SmoothStep(0, 1, t / 0.7f), 0, 0);
                    v.body.localPosition = Vector3.up * Mathf.Lerp(0.9f, 0.3f, t / 0.7f);
                    await Awaitable.NextFrameAsync(ct);
                }
                return;
            }
            // (Its death's id: if it dies lying there and rises, Reanimate gets up out of this one.)
            v.deathId = chosen.id;
            float length = v.anim.PlayWhole(death, KnockIn, true);
            float settle = Mathf.Min(length, FigureCatalog.Load()?.Settle(chosen.id) ?? length);
            _ = Thump(v, chosen, settle, KnockThud);
            await Tuck(v, chosen, settle, ct);
        }

        /// <summary>Knocked down: the death's way in (seconds: a death's own is 0.2, from a shot; a charge throws it at once), and the dust as it hits the floor (Fx.Thud's size).</summary>
        const float KnockIn = 0.08f, KnockThud = 0.8f;

        /// <summary>
        /// One knocked down gets up (Gameplay, 16/11: as the squad's next turn begins; it costs it an action): out of the death
        /// it went down in played backwards, as one of the dead rises out of its body (Reanimate; its drop given time, the 3D
        /// review's A-26), KnockUpShare of the clip, between KnockUpLeast and KnockUpMost seconds, back from where it was
        /// tucked to its tile's middle; then as it stands, its weapon held again, crouched or on watch if the state has it so.
        /// Resolved once it stands. Nothing if it isn't down.
        /// </summary>
        public async Awaitable GetUp(string id, CancellationToken ct = default)
        {
            if (!views.TryGetValue(id, out var v) || !v.down) return;
            var death = v.downId != null && v.anim != null ? FigureCatalog.Load()?.Clip(v.downId) : null;
            if (death == null)
            {
                await Awaitable.WaitForSecondsAsync(0.5f, ct);
                if (v.down) UpAgain(v);
                return;
            }
            var drop = Anims.Deaths(v.build).FirstOrDefault(d => d.id == v.downId)?.drop;
            var shoved = v.body != null ? v.body.localPosition : Vector3.zero;
            v.anim.PlayWhole(death, 0, true, death.length, 0);
            float seconds = Mathf.Clamp(death.length * KnockUpShare, KnockUpLeast, KnockUpMost), at = death.length;
            try
            {
                for (float t = 0; t < seconds; t += Time.deltaTime)
                {
                    // (Killed as it gets up: it falls again from where it is, in its own time; DieDown has the rest.)
                    if (!v.down)
                    {
                        if (v.dead && v.anim != null) v.anim.PlayWhole(death, 0, true, at);
                        return;
                    }
                    at = Backwards(death.length, seconds, drop, t);
                    v.anim.WholeAt(at);
                    if (v.body != null) v.body.localPosition = shoved * (1 - Mathf.SmoothStep(0, 1, t / seconds));
                    await Awaitable.NextFrameAsync(ct);
                }
            }
            finally
            {
                if (v.down) UpAgain(v);
            }
        }

        /// <summary>Getting up (GetUp): of its death's clip, between these seconds; the clip let go over KnockUpOut as it stands.</summary>
        const float KnockUpShare = 0.7f, KnockUpLeast = 0.9f, KnockUpMost = 1.5f, KnockUpOut = 0.25f;

        /// <summary>Up again (GetUp's end): alive in its pose, its whole body's clip let go, its weapon and stance as the state has them (Pose).</summary>
        void UpAgain(View v)
        {
            v.down = false;
            v.downId = null;
            v.dead = false;
            if (v.body != null) v.body.localPosition = Vector3.zero;
            if (v.anim != null)
            {
                v.anim.WholeAt(0);
                v.anim.StopWhole(KnockUpOut);
            }
            else if (v.body != null)
            {
                v.body.localRotation = Quaternion.identity;
                v.body.localPosition = Vector3.up * 0.9f;
            }
            if (v.weapon != null) Settle(v, KnockUpOut);
            if (state?.Units.Find(x => x.Id == v.id) is Unit u) Pose(v, u);
        }

        /// <summary>
        /// Killed lying knocked down (Die): no fall again, nor a second death, its death the one it lies in (deathId: what
        /// Reanimate gets up out of); its death's flash, its weapon let go to the floor, a moment to be seen.
        /// </summary>
        async Awaitable DieDown(View v, CancellationToken ct)
        {
            v.down = false;
            v.downId = null;
            v.dead = true;
            FlashDeath(v);
            LetGo(v, 0);
            await Awaitable.WaitForSecondsAsync(DownDies, ct);
        }

        const float DownDies = 0.4f;

        // ------------------------------------------------------------ its weight

        /// <summary>Whether `v` is the Brute: its death and its steps heavy (Thump, Stride).</summary>
        static bool Heavy(View v) => v.template == BruteKind;

        const string BruteKind = "brute";

        /// <summary>
        /// Where the Brute's hands hang at rest against the dead's (DeadArms, its frame: m of the model, down and ahead): its
        /// elbows bent (not straight, as the dead's), lower and a little further out; at the dead's place, bent, they were
        /// held up before its chest as a boxer's.
        /// </summary>
        static readonly Vector3 BruteArms = LabVector("BASTION_BRUTO_BRAZOS", new Vector3(0, -0.3f, 0.06f));

        /// <summary>The dust as the Brute's body comes down dying (Fx.Thud's size).</summary>
        const float HeavyThud = 1.6f;

        /// <summary>
        /// Dust thrown up where a body falling (`death`, from now) hits the floor: as its head comes within a tenth of its
        /// height lying (Death.drop's end), else at 0.8 of its settling; under its chest, `size` of it (Fx.Thud).
        /// </summary>
        static async Awaitable Thump(View v, Death death, float settle, float size)
        {
            try
            {
                float at = death.drop != null && death.drop.Length >= 2 ? death.drop[1] : settle * 0.8f;
                await Awaitable.WaitForSecondsAsync(Mathf.Max(0, at));
                if (v.root == null || v.animator == null || !v.dead) return;
                var chest = ChestOf(v.animator) is Transform c ? c.position : v.root.position;
                chest.y = v.root.position.y;
                Effects.Fx.Current?.Thud(chest, size);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// How much longer than a figure's its stride is (its clips played this much slower to keep its feet: Gaits.ClipSpeed
        /// is a common figure's): the Brute's (BruteStride), 1 for the rest. Measured 1 (CombatLab.Brute at 30 fps, how fast
        /// each foot goes back under the body while it is down against how fast the body goes: walking 1.97 and 2.45 m/s for
        /// 2.13, charging 5.58 and 6.49 for 5.7; the goblin clips' limp, each foot its own way, nothing to gain from a factor).
        /// </summary>
        static float Stride(View v) => Heavy(v) ? BruteStride : 1;

        static readonly float BruteStride = LabFloat("BASTION_ZANCADA_BRUTO", 1f);
    }
}
