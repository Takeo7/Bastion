using System;
using System.Collections.Generic;
using System.Threading;
using Bastion.Client.Map;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The silent takedown (0.12.26: Arturo's «golpe silencioso» of 16/11, Gameplay's SIGILO-Y-RECLUTAS.md §2): a soldier
    /// with a blade kills one of the dead beside it that doesn't see it, at once and without a sound. Read as a blow from
    /// behind, not as the blade's stroke (Lunge, Strike): turned to it, up from its crouch as far as the dead one's neck
    /// asks (a dead one down low, eating or crouched, taken from the crouch), it walks in close to its back, its left hand
    /// takes the head, and the blade comes down on the nape from above (the blow from above's clip, macheteChop: the knife
    /// held the other way up and driven in, the machete and the swords on the neck, the rest on the nape), slow as it gets
    /// ready, quick as it goes in. The dead one goes down that very frame, limp and away from it, in one of its deaths that
    /// has room (no hit's clip, no hit's flash, no knock); the hand lets go as it falls, the soldier walks back to its
    /// tile, puts the blade away and crouches again if the state has it so. UI's event player waits for it as for a Lunge,
    /// till the blow lands (`onHit`), and the rest goes on by itself; the HitReact and the Die it plays then for the
    /// damage and the death leave the fall begun here alone (Hushed).
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>A soldier's takedown going on: on whom, with what, where its body has stepped to, how far its left hand has the head and the blow is laid on the nape.</summary>
        sealed class TakingDown
        {
            public View target;
            public Edge edge;
            /// <summary>Its body's step in (world, from its root: the root stays on its tile, which Sync puts it back on at every event).</summary>
            public Vector3 offset;
            /// <summary>How far the left hand is on the head (0 to 1), the blade laid on the nape (0 to 1: the right arm brought onto it), the trunk leant in (0 to 1).</summary>
            public float grab, lay, lean;
            /// <summary>The nape kept where the blow went in (the body falls away from the blade, not with it).</summary>
            public bool held;
            /// <summary>Where in the blow from above's clip it is (seconds), and where it lands there (ReleaseOf).</summary>
            public float clipAt, release;
            /// <summary>This frame's (for the labs): where the left palm goes and is, where the blade goes and what of it is there.</summary>
            public Vector3 grabAt, palm, nape, contact;
        }

        readonly Dictionary<View, TakingDown> takingDown = new Dictionary<View, TakingDown>();

        /// <summary>One of the dead felled silently (Takedown): when it went down, and when its fall is over.</summary>
        sealed class Hush
        {
            public float at, until;
        }

        readonly Dictionary<View, Hush> hushed = new Dictionary<View, Hush>();

        /// <summary>What of the blade goes into the dead one: the knife's point, a long blade's edge, the head of anything else.</summary>
        enum Edge { Point, Blade, Blunt }

        /// <summary>
        /// Where the soldier's root stops from the dead one's (tiles): close to its back, its chest a hand off the dead one's
        /// shoulder blades; from ahead, its arms out before it (DeadArms), further; from a side, in between. A Lunge stops
        /// LungeShort (0.72) off. BASTION_SILENCIO_HUECO=behind,ahead for the labs.
        /// </summary>
        static readonly Vector2 SilentGap = LabVector2("BASTION_SILENCIO_HUECO", new Vector2(0.32f, 0.62f));
        /// <summary>How far it walks in at most (tiles: from a diagonal tile, all of it; to one eating by the body it ate, gone down 0.6 tiles off its own).</summary>
        const float SilentMost = 1.8f;
        /// <summary>
        /// How fast it walks in and back (metres a second, its legs walking at its pace: TendClip's), eased in and out over
        /// SilentEase; SilentStepLeast to SilentStepMost seconds, quicker from a diagonal tile. Gameplay, 16/11: the blow in
        /// by about a second from the order, the body on the floor by about two (the combat's pace; a takedown comes often).
        /// </summary>
        const float SilentPace = 1.5f, SilentEase = 0.15f, SilentStepLeast = 0.3f, SilentStepMost = 0.72f;
        /// <summary>Degrees off its target past which it turns a moment before setting off (turned less, it turns as it walks), and how long at most.</summary>
        const float SilentTurnLeft = 100, SilentTurnWait = 0.25f;

        /// <summary>
        /// The blow from above (macheteChop, «Standing Melee Attack Downward»; ChopStroke) on its own clock: its wind-up from
        /// the clip's start to SilentRaise (the hand up and back, 0.42–0.55 s in: ClipProbe.Alone BASTION_MANOS=1) over at
        /// least SilentPrep, as it walks in and its hand takes the head; held up SilentHold; brought down to where the blow
        /// lands (SilentReleases) in SilentStroke, quickening; held in SilentIn, the clip on at SilentInRate; then let go
        /// over StrokeOut. A blade's stroke winds up in 0.34 s and takes 0.14 to land.
        /// </summary>
        const float SilentPrep = 0.55f, SilentHold = 0.08f, SilentIn = 0.22f, SilentInRate = 0.3f;
        static readonly float SilentRaise = LabFloat("BASTION_SILENCIO_ALZA", 0.45f), SilentStroke = LabFloat("BASTION_SILENCIO_TRAZO", 0.1f);
        /// <summary>
        /// Where in the clip the blow lands (seconds), by how far what goes in is from the fist (SilentReach: a knife's 25 cm
        /// to a stick's metre): over the trunk, head and arm alone (the Strike layer: its hips and legs not the clip's) the
        /// right hand sweeps from over the head behind (0.65 s) to 60 cm ahead of the shoulder and under it (0.79;
        /// CombatLab.Silencioso, the stroke slowed), and the longer what it holds, the further back and up its fist is as that
        /// comes onto the nape (Lay). BASTION_SILENCIO_SUELTAS=near,far for the labs.
        /// </summary>
        static readonly Vector2 SilentReleases = LabVector2("BASTION_SILENCIO_SUELTAS", new Vector2(0.76f, 0.66f));
        static float ReleaseOf(float reach) => Mathf.Lerp(SilentReleases.x, SilentReleases.y, Mathf.Clamp01((reach - 0.25f) / 0.8f));
        /// <summary>
        /// The way the blow goes in at the nape (degrees under the level, along the line to the dead one; Edge: the knife's
        /// point, a long blade's edge, a blunt one's head): the knife driven down into it, a blade's edge across the neck from
        /// above, a club's head brought down on it. What it holds is turned at the wrist to lie so as it lands (Lay).
        /// BASTION_SILENCIO_ANGULOS=point,blade,blunt for the labs.
        /// </summary>
        static readonly Vector3 SilentPitch = LabVector("BASTION_SILENCIO_ANGULOS", new Vector3(65, 35, 20));
        static float PitchOf(Edge edge) => edge == Edge.Point ? SilentPitch.x : edge == Edge.Blade ? SilentPitch.y : SilentPitch.z;
        /// <summary>
        /// How far its trunk tips at most (degrees from upright: ahead, behind): the blow from above's clip arches it back as it
        /// winds up and doubles it over the follow-through; with SilentLean on top, it went down to the dead one's shoulders.
        /// </summary>
        static readonly Vector2 SilentTilt = LabVector2("BASTION_SILENCIO_TRONCO", new Vector2(24, 8));
        /// <summary>
        /// How far its trunk leans in over the dead one as its hand takes the head (degrees, half at the spine and half at the
        /// chest): the blow from above's clip throws it back as it winds up, and a hunched one's head is ahead of its back.
        /// </summary>
        static readonly float SilentLean = LabFloat("BASTION_SILENCIO_INCLINA", 20);
        /// <summary>How long the left hand takes to let go of the head once the blade is in (seconds), and the blade drawn in, before that (SilentDraw).</summary>
        const float SilentLetGo = 0.3f, SilentDraw = 0.15f;
        /// <summary>The right arm brought onto the nape at most this far from where the clip has it (metres).</summary>
        static readonly float SilentLayMost = LabFloat("BASTION_SILENCIO_APOYO", 0.9f);

        /// <summary>
        /// The head and the nape on the dead one (metres at a common figure's size, Figures.CharacterScale): the skull's
        /// middle this far up from the head's bone along the neck; the left palm on it this far out towards the soldier and
        /// up (the back of the head from behind); the nape between the neck's bone and the head's (NapeUp of the way), this
        /// far out towards the soldier; the knife's point that far into it, a blunt one's head that far short of its end.
        /// </summary>
        const float SkullUp = 0.09f, GrabOut = 0.085f, GrabUp = 0.03f, NapeOut = 0.05f, PointIn = 0.02f, BluntIn = 0.05f, EdgeOff = 0.2f;
        static readonly float NapeUp = LabFloat("BASTION_SILENCIO_NUCA", 0.5f);
        /// <summary>How far the left hand's fingers close on the head (degrees at their first two joints: a grip on the hair, not a fist).</summary>
        static readonly Vector2 GrabBend = new Vector2(35, 45);
        /// <summary>
        /// The left elbow's way as it reaches for the head (the soldier's frame: out to its left, up, back): over the dead
        /// one's shoulder, the forearm down onto the head beside it. Out and down, the forearm went 13–50 cm through its
        /// shoulder blade and collarbone (CombatLab.Silencioso). BASTION_SILENCIO_CODO=x,y,z for the labs.
        /// </summary>
        static readonly Vector3 GrabPole = LabVector("BASTION_SILENCIO_CODO", new Vector3(-0.8f, 0.6f, -0.2f));
        /// <summary>When the left hand sets off for the head (of the way in: SilentPrep), and how high over the straight line it comes on its way (metres, at the middle).</summary>
        const float GrabFrom = 0.5f, GrabArc = 0.2f;
        /// <summary>How far from the left shoulder the head may be for the hand to go all the way to it (metres; a quarter metre further, none of the way): an arm and a bit.</summary>
        const float GrabNear = 0.7f;
        /// <summary>A dead one whose neck is under this (metres over its feet, at a common figure's size: eating, crouched) is taken from the crouch.</summary>
        const float LowNeck = 1.15f;

        /// <summary>
        /// The dead one's fall (Fell): in over SilentFade (a dead one down low, eating or crouched, over SilentLowFade), its
        /// death's clip begun SilentSkip of the way to where its head goes under 60 % of its height (Death.drop; a low one
        /// halfway between that and where it lies): a death's first part is the stagger of one hit, and deathForward's own took a second to come down
        /// (ClipProbe.Alone BASTION_CABEZA=1: its head at 92 % at 0.5 s, on the floor at 1.2). Die's comes in over 0.2 s
        /// from its start.
        /// </summary>
        const float SilentFade = 0.08f, SilentLowFade = 0.25f, SilentSkip = 0.5f;
        /// <summary>Seconds a felled one is kept down although the state still has it alive (its death comes behind the blow); past them, it gets up again.</summary>
        const float HushKeep = 4;

        /// <summary>
        /// A silent takedown of `target` by `id` (EventPlayer, for a ShotEvent of AbilityIds.Takedown): resolved as the blow
        /// lands, `onHit` then (`target` already falling); the rest goes on by itself. One that fails (a bone missing, a clip
        /// not loaded, a figure gone) lands all the same, the error in red.log, as a blade's stroke does (StrokeOn): the
        /// combat never waits for it for ever.
        /// </summary>
        public async Awaitable Takedown(string id, string target, Action onHit, CancellationToken ct = default)
        {
            View t = target != null && views.TryGetValue(target, out var found) ? found : null;
            View v = id != null && views.TryGetValue(id, out var striker) ? striker : null;
            bool landed = false;
            void Land()
            {
                if (landed) return;
                landed = true;
                // (Down the same frame the blow lands, before what UI does with it.)
                try
                {
                    Fell(t, v);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                onHit?.Invoke();
            }
            if (v == null || v.anim == null || v.root == null || v.carried.Count == 0)
            {
                await Awaitable.WaitForSecondsAsync(0.35f, ct);
                Land();
                await Awaitable.WaitForSecondsAsync(0.3f, ct);
                return;
            }
            _ = TakedownOn(v, t, Land, ct);
            while (!landed && !ct.IsCancellationRequested) await Awaitable.NextFrameAsync(ct);
        }

        /// <summary>The takedown, on by itself (Takedown); landed whatever happens, and put right after (Undo).</summary>
        async Awaitable TakedownOn(View v, View t, Action land, CancellationToken ct)
        {
            v.striking = true;
            bool crouched = v.crouched;
            try
            {
                await Silently(v, t, land, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                v.striking = false;
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
                try
                {
                    Undo(v, crouched);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        async Awaitable Silently(View v, View t, Action land, CancellationToken ct)
        {
            var td = new TakingDown { target = t };
            takingDown[v] = td;
            // The blow from where it stands, not at arm's length: the dead one falls away from it (UnitViews.Blows).
            if (t != null)
            {
                StruckBy(t, v.root.position, false);
                Face(v.id, t.root.position);
            }
            // Turned a moment first if it has its back to it, not walking in backwards; else it turns as it sets off.
            for (float w = 0; TurnLeft(v) > SilentTurnLeft && w < SilentTurnWait; w += Time.deltaTime) await Awaitable.NextFrameAsync(ct);
            // The blade from the belt (with only a blade, the one in its hands); the knife the other way up in the fist.
            var art = v.arts.TryGetValue(v.carried.ContainsKey(BeltCarry) ? BeltCarry : MainCarry, out var a) ? a : null;
            td.edge = EdgeOf(art);
            v.reversed = td.edge == Edge.Point;
            Draw(v, BeltCarry);
            AtRest(v, SilentDraw);
            td.release = ReleaseOf(Contact(v, td.edge) is var (fist, into) ? Vector3.Distance(fist, into) : 0.25f);
            // Up from its crouch for one standing, down to it for one low (Pose leaves it so till it is done: risen).
            bool low = t != null && Low(t);
            v.risen = true;
            v.crouched = low;
            string walk = low ? "crouchWalk" : "walk";
            // (To where its body is, not its tile: a dead one's idle sways its hips 20 cm about its root.)
            Vector3 To() => t != null && t.root != null ? Reach(v.root.position, low ? NeckAt(t) : BodyAt(t), low ? SilentLowGap : GapOf(v, t), SilentMost) : Vector3.zero;
            var to = To();
            float distance = Flat(to);
            float step = distance > 0.02f ? Mathf.Clamp(distance / SilentPace + SilentEase, SilentStepLeast, SilentStepMost) : 0;
            // (Eased in and out over SilentEase, its legs at the pace it goes at between, as Tend's.)
            float r = step > 0 ? Mathf.Min(0.5f, SilentEase / step) : 0, top = 1 / (1 - r);
            float pace = step > 0 ? distance / step * top / (TendClip * Figures.CharacterScale) : 1;
            float Along(float u) => r <= 0 ? u : u < r ? top * u * u / (2 * r) : u > 1 - r ? 1 - top * (1 - u) * (1 - u) / (2 * r) : top * (u - r / 2);
            float prep = Mathf.Max(SilentPrep, step), raised = prep + SilentHold, hit = raised + SilentStroke, letGo = hit + SilentIn;
            float back = letGo + SilentLetGo * 0.5f, outFrom = letGo, end = Mathf.Max(back + step, outFrom + StrokeOut) + 0.05f;
            var clip = FigureCatalog.Load()?.Clip(ChopStroke.clip);
            if (step > 0) v.anim.Motion(walk, pace);
            else v.anim.Motion(IdleOf(v));
            bool there = step <= 0, homeward = false, home = step <= 0, struck = false;
            for (float s = 0; s < end; s += Time.deltaTime)
            {
                // A walk begun meanwhile (the next order, quicker than this), its death: cut short here, put right by Undo.
                if (v.root == null || v.walking || v.dead) return;
                // In to its back, and home again once the hand has let go, its legs walking (backwards on the way back).
                if (!there && s >= step)
                {
                    there = true;
                    v.anim.Motion(IdleOf(v));
                    StartLanding(v);
                }
                if (!homeward && s >= back && step > 0)
                {
                    homeward = true;
                    v.anim.Motion(walk, -pace);
                }
                if (homeward && !home && s >= back + step)
                {
                    home = true;
                    v.anim.Motion(IdleOf(v));
                    StartLanding(v);
                }
                if (!struck) to = Vector3.MoveTowards(to, To(), SilentFollow * Time.deltaTime);
                td.offset = to * (homeward ? 1 - Along(Mathf.Clamp01((s - back) / step)) : step > 0 ? Along(Mathf.Clamp01(s / step)) : 0);
                // The blow from above on its own clock, over the trunk, head and right arm (its left arm free for the head).
                if (clip != null)
                {
                    float release = td.release;
                    float at = s < prep ? SilentRaise * Mathf.SmoothStep(0, 1, s / prep)
                        : s < raised ? SilentRaise
                        : s < hit ? SilentRaise + (release - SilentRaise) * Sq((s - raised) / SilentStroke)
                        : release + (s - hit) * SilentInRate;
                    float share = Mathf.Clamp01(Mathf.Min(s / 0.2f, (outFrom + StrokeOut - s) / StrokeOut));
                    v.anim.Scrub(share > 0 ? clip : null, Mathf.Clamp(at, 0, clip.length), share, true);
                    td.clipAt = at;
                }
                // The left hand onto the head as it gets there, kept on it till the blade is in, off as it falls; the blade
                // laid on the nape as it comes down, kept there, let go with the hand.
                td.grab = s < prep * GrabFrom ? 0 : s < prep ? Mathf.SmoothStep(0, 1, (s - prep * GrabFrom) / (prep * (1 - GrabFrom)))
                    : s < letGo ? 1 : 1 - Mathf.SmoothStep(0, 1, (s - letGo) / SilentLetGo);
                td.lay = s < raised ? 0 : s < hit ? Sq((s - raised) / SilentStroke) : s < letGo ? 1 : Mathf.Max(0, 1 - (s - letGo) / StrokeOut);
                // (Up again as the blow goes in: the clip's follow-through bends it over the falling body.)
                td.lean = s < prep * 0.3f ? 0 : s < prep ? Mathf.SmoothStep(0, 1, (s - prep * 0.3f) / (prep * 0.7f)) : s < hit ? 1 : 1 - Mathf.SmoothStep(0, 1, (s - hit) / 0.3f);
                if (!struck && s >= hit)
                {
                    struck = true;
                    td.held = true;
                    land();
                }
                await Awaitable.NextFrameAsync(ct);
            }
        }

        static float Sq(float u) => Mathf.Clamp01(u) * Mathf.Clamp01(u);

        /// <summary>Done (or cut short): the stroke's clip off, the body back on its root, the knife held as ever and put away, the main weapon in hand, crouched again if the state has it so.</summary>
        void Undo(View v, bool crouched)
        {
            takingDown.Remove(v);
            v.reversed = false;
            if (v.root == null) return;
            v.anim?.Scrub(null, 0, 0, true);
            if (v.body != null) v.body.localPosition = new Vector3(0, 0, -v.back * MapSpace.Tile);
            v.risen = false;
            if (v.dead) return;
            // (On watch with the blade, back on guard: as after a blow, Swing.)
            if (v.weapon != null) Draw(v, v.watch && v.guard ? BeltCarry : MainCarry);
            if (state?.Units.Find(x => x.Id == v.id) is Bastion.Engine.Unit u) Pose(v, u);
            else v.crouched = crouched;
            if (!v.walking && !v.feeding && !v.hiding) v.anim?.Motion(IdleOf(v));
            if (!v.hiding) Settle(v, 0.25f);
        }

        /// <summary>What of the blade `art` goes in: the knife's point, the machete's and the swords' edge, the head of the rest (the bat, the pan, the crowbar…).</summary>
        static Edge EdgeOf(WeaponArt art)
        {
            string fbx = art?.fbx ?? "";
            bool Has(string part) => fbx.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
            return Has("Knife") ? Edge.Point : Has("Machete") || Has("Katana") || Has("Sword") ? Edge.Blade : Edge.Blunt;
        }

        /// <summary>Whether the dead one is down low (eating, crouched, its neck under LowNeck): taken from the crouch, its death begun from low.</summary>
        static bool Low(View t)
        {
            if (t.feeding || t.crouched) return true;
            var neck = t.animator != null && t.animator.isHuman ? t.animator.GetBoneTransform(HumanBodyBones.Neck) : null;
            return neck != null && neck.position.y - t.root.position.y < LowNeck * Size(t);
        }

        /// <summary>A figure's size over a common one's (the bodies' scale: the Acorazados' 1.06).</summary>
        static float Size(View v) => v.body != null ? v.body.lossyScale.x / Figures.CharacterScale : 1;

        /// <summary>Where `t`'s body is on the ground: its hips (its root without a figure).</summary>
        static Vector3 BodyAt(View t)
        {
            var hips = t.animator != null && t.animator.isHuman ? t.animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            var at = hips != null ? hips.position : t.root.position;
            at.y = t.root.position.y;
            return at;
        }

        /// <summary>Where `t`'s neck is on the ground (one down low, bent over what it eats: its hips are half a metre behind it).</summary>
        static Vector3 NeckAt(View t)
        {
            var neck = t.animator != null && t.animator.isHuman ? t.animator.GetBoneTransform(HumanBodyBones.Neck) : null;
            if (neck == null) return BodyAt(t);
            var at = neck.position;
            at.y = t.root.position.y;
            return at;
        }

        /// <summary>Where it stops from the neck of one down low (tiles: kneeling by its hips, over its back).</summary>
        static readonly float SilentLowGap = LabFloat("BASTION_SILENCIO_HUECO_BAJO", 0.6f);

        /// <summary>How fast where it stops follows the dead one's body as it sways (metres a second).</summary>
        const float SilentFollow = 0.4f;

        /// <summary>Where it stops from the dead one (SilentGap): by how much it stands ahead of it or behind.</summary>
        static float GapOf(View v, View t)
        {
            var d = v.root.position - t.root.position;
            d.y = 0;
            float ahead = d.sqrMagnitude > 1e-6f ? Vector3.Dot(t.root.forward, d.normalized) : -1;
            return Mathf.Lerp(SilentGap.x, SilentGap.y, (ahead + 1) / 2);
        }

        /// <summary>
        /// The dead one down at once (the blow lands): not again if it is already, nor through Die's or HitReact's later;
        /// what it ate or reached for let go of (as Die); the flash of a death, the same; its death away from the blow,
        /// among those with room (UnitViews.Blows), not the one doubled up round a blow (the stomach's), from SilentSkip of
        /// its way down, so it begins to go down at once; drawn in to its tile if none has room (Tuck).
        /// </summary>
        void Fell(View t, View by)
        {
            if (t == null || t.dead || hushed.ContainsKey(t)) return;
            ForgetHushed();
            var hush = new Hush { at = Time.time, until = Time.time };
            hushed[t] = hush;
            bool low = Low(t);
            t.feeding = false;
            t.feedOn = null;
            t.reachFor = null;
            FlashDeath(t);
            t.dead = true;
            if (t.anim == null)
            {
                // (A stand-in: down on its back as Die has it.)
                hush.until = Time.time + 0.7f;
                _ = FallStandIn(t);
                return;
            }
            // (Never towards the soldier, whatever has room: one eating fell back onto it, the body it ate in its way.)
            float? toward = by != null && by.root != null ? WayOf(t, by.root.position + (takingDown.TryGetValue(by, out var td) ? td.offset : Vector3.zero)) : null;
            var chosen = DeathFor(t, out bool cramped, d => !d.close && (!(toward is float w) || Mathf.Abs(Wrap(d.falls - w)) > Mathf.PI / 2));
            var death = chosen != null ? FigureCatalog.Load()?.Clip(chosen.id) : null;
            t.deathId = chosen?.id;
            if (death == null) return;
            // (One down low from halfway down its fall, its head as low as it was: from 60 % of its height, eating, it came up 14 cm.)
            float at = chosen.drop == null || chosen.drop.Length == 0 ? 0 : low && chosen.drop.Length > 1 ? (chosen.drop[0] + chosen.drop[1]) / 2 : chosen.drop[0] * SilentSkip;
            at = Mathf.Min(death.length, at);
            t.anim.PlayWhole(death, low ? SilentLowFade : SilentFade, true, at);
            float settle = Mathf.Max(0.1f, Mathf.Min(death.length, FigureCatalog.Load()?.Settle(chosen.id) ?? death.length) - at);
            LetGo(t, LetGoShare * settle);
            if (cramped) _ = Tuck(t, chosen, settle, default);
            hush.until = Time.time + settle;
        }

        async Awaitable FallStandIn(View t)
        {
            for (float s = 0; s < 0.7f && t.body != null; s += Time.deltaTime)
            {
                t.body.localRotation = Quaternion.Euler(-90 * Mathf.SmoothStep(0, 1, s / 0.7f), 0, 0);
                t.body.localPosition = Vector3.up * Mathf.Lerp(0.9f, 0.3f, s / 0.7f);
                await Awaitable.NextFrameAsync();
            }
            if (t.body != null) Lie(t);
        }

        /// <summary>Whether `v` was felled silently and its Die has not come yet (HitReact does nothing on it, Die only waits for its fall).</summary>
        bool Hushed(View v) => hushed.Count > 0 && hushed.ContainsKey(v);

        /// <summary>
        /// Die for one felled silently: its fall, begun as the blow landed, waited for to its end; not begun again, nor its
        /// flash. It stays felled (KeptDown) till its death is in the state: a Sync before that would stand it up.
        /// </summary>
        async Awaitable HushedDie(View v, CancellationToken ct)
        {
            if (hushed.TryGetValue(v, out var hush) && hush.until > Time.time) await Awaitable.WaitForSecondsAsync(hush.until - Time.time, ct);
        }

        /// <summary>
        /// Whether Sync leaves `v` lying although the state has it alive: felled silently, its death behind the blow in the
        /// same batch (DamagedEvent, DiedEvent), till HushKeep seconds on (then the blow did not kill it after all).
        /// </summary>
        bool KeptDown(View v)
        {
            if (!hushed.TryGetValue(v, out var hush)) return false;
            if (Time.time - hush.at < HushKeep) return true;
            hushed.Remove(v);
            return false;
        }

        /// <summary>The felled whose time is long past (HushKeep): dropped as another is felled (dead in the state, Sync never asks for them again).</summary>
        void ForgetHushed()
        {
            List<View> old = null;
            foreach (var pair in hushed)
                if (Time.time - pair.Value.at >= HushKeep) (old ??= new List<View>()).Add(pair.Key);
            if (old != null) foreach (var v in old) hushed.Remove(v);
        }

        /// <summary>
        /// Each frame after every figure's pose (Step), the takedowns going on: the soldier's body stepped in, its left hand
        /// on the dead one's head by two-bone IK, the palm turned onto it and closed a little; its right arm brought onto the
        /// nape as far as the blow is laid (SilentLayMost at most), the hand turned as the clip has it, the blade in the
        /// fist again where it is (HoldBlade). After the dead one's own pose, wherever it comes in the frame's order.
        /// </summary>
        void StepTakedowns()
        {
            if (takingDown.Count == 0) return;
            foreach (var pair in takingDown)
            {
                var v = pair.Key;
                var td = pair.Value;
                if (v.root == null || v.body == null) continue;
                v.body.localPosition = v.root.InverseTransformVector(td.offset);
                var a = v.animator;
                var za = td.target?.animator;
                // (Not stepped this frame, unseen: its pose is last frame's, laid on already.)
                if (a == null || !a.isHuman || v.missed > 0) continue;
                // (The blade rides on the root, not the body: in the fist again where the body has stepped to.)
                if (v.inHand == Held.Blade && v.weapon != null) HoldBlade(v);
                if (za == null || !za.isHuman || td.target.root == null) continue;
                Transform head = za.GetBoneTransform(HumanBodyBones.Head), neck = za.GetBoneTransform(HumanBodyBones.Neck);
                if (head == null || neck == null) continue;
                float size = Size(td.target);
                // From the dead one's head out towards the soldier, along the ground.
                var toward = v.root.position + td.offset - head.position;
                toward.y = 0;
                toward = toward.sqrMagnitude > 1e-6f ? toward.normalized : -td.target.root.forward;
                var up = (head.position - neck.position).sqrMagnitude > 1e-6f ? (head.position - neck.position).normalized : Vector3.up;
                var skull = head.position + up * SkullUp * size;
                td.grabAt = skull + (toward * GrabOut + Vector3.up * GrabUp) * size;
                if (!td.held) td.nape = Vector3.Lerp(neck.position, head.position, NapeUp) + toward * NapeOut * size;
                Lean(v, SilentLean * td.lean);
                Upright(v, SilentTilt);
                Grab(v, td, skull, toward);
                Lay(v, td);
            }
        }

        /// <summary>The trunk leant in `degrees` round its right (half at the spine, half at the chest), what it holds and the arms with it.</summary>
        static void Lean(View v, float degrees)
        {
            if (Mathf.Abs(degrees) < 1e-3f) return;
            var a = v.animator;
            var right = v.root.right;
            foreach (var bone in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest })
                if (a.GetBoneTransform(bone) is Transform b) b.rotation = Quaternion.AngleAxis(degrees / 2, right) * b.rotation;
            if (v.inHand == Held.Blade && v.weapon != null) HoldBlade(v);
        }

        /// <summary>
        /// The trunk kept within `most` of upright (degrees ahead, behind: SilentTilt), tipped back about its right at the
        /// spine and the chest, half each, by what is over; what it holds with it.
        /// </summary>
        static void Upright(View v, Vector2 most)
        {
            var a = v.animator;
            Transform hips = a.GetBoneTransform(HumanBodyBones.Hips), neck = a.GetBoneTransform(HumanBodyBones.Neck);
            if (hips == null || neck == null) return;
            var right = v.root.right;
            var trunk = Vector3.ProjectOnPlane(neck.position - hips.position, right);
            if (trunk.sqrMagnitude < 1e-6f) return;
            // (Ahead of upright, positive: towards the root's forward.)
            float tip = Vector3.SignedAngle(Vector3.up, trunk, right);
            float over = tip > most.x ? tip - most.x : tip < -most.y ? tip + most.y : 0;
            if (Mathf.Abs(over) > 1e-3f) Lean(v, -over);
        }

        /// <summary>The left hand on the head (StepTakedowns): its palm onto td.grabAt, turned onto the skull, its fingers over it away from the soldier.</summary>
        void Grab(View v, TakingDown td, Vector3 skull, Vector3 toward)
        {
            var a = v.animator;
            Transform shoulder = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), elbow = a.GetBoneTransform(HumanBodyBones.LeftLowerArm), hand = a.GetBoneTransform(HumanBodyBones.LeftHand),
                knuckle = a.GetBoneTransform(HumanBodyBones.LeftMiddleProximal), index = a.GetBoneTransform(HumanBodyBones.LeftIndexProximal), little = a.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
            if (shoulder == null || elbow == null || hand == null || knuckle == null || index == null || little == null) return;
            Vector3 Palm() => Vector3.Lerp(hand.position, knuckle.position, 0.6f);
            td.palm = Palm();
            if (td.grab <= 0) return;
            // (Up and over the dead one's shoulder on its way, GrabArc at the middle: straight from the hip, it went through its back.)
            // (Not out at arm's length for a head still out of reach: as far as it comes within GrabNear of it.)
            float near = Mathf.Clamp01((GrabNear - Vector3.Distance(shoulder.position, td.grabAt)) / 0.25f + 1);
            float grab = td.grab * near;
            var goal = Vector3.Lerp(Palm(), td.grabAt, grab) + Vector3.up * (GrabArc * 4 * grab * (1 - grab) * Size(v));
            var pole = v.root.TransformDirection(GrabPole);
            // (Reached, turned, and reached again: the palm is ahead of the wrist, and turning the hand moves it.)
            for (int pass = 0; pass < 2; pass++)
            {
                Reach(a, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, hand.position + (goal - Palm()), pole, 0.005f);
                // Its palm onto the skull, its fingers over it and away from the soldier (from behind, over the top of the
                // head towards the face).
                var fingers = knuckle.position - hand.position;
                var palmward = -Vector3.Cross(fingers, index.position - little.position);
                var onto = skull - Palm();
                if (fingers.sqrMagnitude < 1e-8f || palmward.sqrMagnitude < 1e-8f || onto.sqrMagnitude < 1e-8f) break;
                var over = Vector3.ProjectOnPlane(Vector3.up - toward * 0.6f, onto);
                if (over.sqrMagnitude < 1e-6f) break;
                var turn = Quaternion.LookRotation(over, -onto) * Quaternion.Inverse(Quaternion.LookRotation(fingers, -palmward));
                hand.rotation = Quaternion.Slerp(Quaternion.identity, turn, grab) * hand.rotation;
            }
            if (grab > 0.5f) Fist(a, GrabBend, GrabBend, true);
            td.palm = Palm();
        }

        /// <summary>
        /// The blow laid on the nape (StepTakedowns), as far as td.lay: what it holds turned about its grip (in the palm) to
        /// go in the way the edge asks (SilentPitch: under the level, along the line to the dead one), and the right arm
        /// reaching so that what goes in (Contact) is on the nape; the weapon in the fist again (HoldBlade). Turned about the
        /// wrist instead, the knife's point swung half a metre and the arm, held to SilentLayMost, left it 15 cm off.
        /// </summary>
        void Lay(View v, TakingDown td)
        {
            if (!(Contact(v, td.edge) is var (fist, contact))) return;
            td.contact = contact;
            if (td.lay <= 0) return;
            var a = v.animator;
            Transform shoulder = a.GetBoneTransform(HumanBodyBones.RightUpperArm), elbow = a.GetBoneTransform(HumanBodyBones.RightLowerArm), hand = a.GetBoneTransform(HumanBodyBones.RightHand);
            var way = contact - fist;
            if (shoulder == null || elbow == null || hand == null || way.sqrMagnitude < 1e-8f) return;
            float reach = way.magnitude;
            var ahead = td.nape - (v.root.position + td.offset);
            ahead.y = 0;
            ahead = ahead.sqrMagnitude > 1e-6f ? ahead.normalized : v.root.forward;
            var into = Quaternion.AngleAxis(PitchOf(td.edge), Vector3.Cross(Vector3.up, ahead)) * ahead;
            var turn = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(way / reach, into), td.lay);
            // (The grip in the hand's frame, where the fist goes, and so where the hand goes turned so.)
            var grip = Quaternion.Inverse(hand.rotation) * (fist - hand.position);
            var turned = turn * hand.rotation;
            var gripTo = Vector3.Lerp(fist, td.nape - turn * (way / reach) * reach, td.lay);
            var want = gripTo - turned * grip - hand.position;
            if (want.magnitude > SilentLayMost) want = want.normalized * SilentLayMost;
            var was = hand.position;
            Reach(a, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, hand.position + want, elbow.position - shoulder.position, 0.005f);
            hand.rotation = turned;
            HoldBlade(v);
            if (Contact(v, td.edge) is var (_, now)) td.contact = now;
            // (BASTION_LOG_SILENCIO=1, the labs: how far the hand was moved for it and what is left, cm; how far turned, degrees.)
            if (LayLog) Debug.Log(FormattableString.Invariant($"[silencio-lay] {Time.time:0.000} {v.id} lay={td.lay:0.00} want={want.magnitude * 100:0.0} moved={(hand.position - was).magnitude * 100:0.0} left={(td.nape - td.contact).magnitude * 100:0.0} turn={Quaternion.Angle(Quaternion.identity, turn):0} reach={reach:0.00}"));
        }

        static readonly bool LayLog = Environment.GetEnvironmentVariable("BASTION_LOG_SILENCIO") == "1";

        /// <summary>Each weapon's length from its grip along the way it runs, tight to its meshes (model units; BladeLine's world box is longer for one held aslant).</summary>
        readonly Dictionary<Transform, float> farOf = new Dictionary<Transform, float>();

        /// <summary>
        /// The weapon in `v`'s hand: its grip, and what of it goes into the dead one (Edge), in the world: the knife's point
        /// PointIn short of its end, a blade's edge EdgeOff short of it (two fifths of its length at most), a blunt one's head
        /// BluntIn short of it.
        /// </summary>
        (Vector3 fist, Vector3 into)? Contact(View v, Edge edge)
        {
            if (v.inHand != Held.Blade || v.weapon == null || !v.arts.TryGetValue(v.drawn, out var art)) return null;
            var along = art.along != null && art.along.Length >= 3 ? new Vector3(art.along[0], art.along[1], art.along[2]).normalized : Vector3.forward;
            var grip = art.grip != null && art.grip.Length >= 3 ? new Vector3(art.grip[0], art.grip[1], art.grip[2]) : Vector3.zero;
            if (!farOf.TryGetValue(v.weapon, out float far))
            {
                far = 0;
                foreach (var mf in v.weapon.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null)
                        foreach (var corner in Corners(mf.sharedMesh.bounds)) far = Mathf.Max(far, Vector3.Dot(v.weapon.InverseTransformPoint(mf.transform.TransformPoint(corner)) - grip, along));
                farOf[v.weapon] = far;
            }
            float length = far * Mathf.Max(v.weapon.lossyScale.x, 1e-4f), scale = Size(v);
            float reach = edge == Edge.Point ? length - PointIn * scale : edge == Edge.Blade ? length - Mathf.Min(EdgeOff * scale, 0.4f * length) : length - BluntIn * scale;
            var at = v.weapon.TransformPoint(grip);
            return (at, at + v.weapon.TransformDirection(along) * Mathf.Max(0, reach));
        }

        /// <summary>A pair from the environment (x,y), for the labs to try; `or` without it.</summary>
        static Vector2 LabVector2(string name, Vector2 or)
        {
            var parts = (Environment.GetEnvironmentVariable(name) ?? "").Split(',');
            return parts.Length == 2 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y) ? new Vector2(x, y) : or;
        }

        // ------------------------------------------------------------ for the labs

        /// <summary>
        /// For the labs (CombatLab.Silencioso): `id`'s takedown as this frame left it, or null if none: where the left palm
        /// goes and is, where the blade goes and what of it is there, how far the hand has the head and the blow is laid,
        /// and its body's step in.
        /// </summary>
        public (Vector3 grabAt, Vector3 palm, Vector3 nape, Vector3 contact, float grab, float lay, Vector3 offset)? TakedownForLab(string id) =>
            id != null && views.TryGetValue(id, out var v) && takingDown.TryGetValue(v, out var td) ? (td.grabAt, td.palm, td.nape, td.contact, td.grab, td.lay, td.offset) : null;

        /// <summary>For the labs: where in the blow from above's clip `id`'s takedown is, and where its blow lands (seconds; −1 none).</summary>
        public Vector2 TakedownClipForLab(string id) => id != null && views.TryGetValue(id, out var v) && takingDown.TryGetValue(v, out var td) ? new Vector2(td.clipAt, td.release) : new Vector2(-1, -1);

        /// <summary>For the labs: when `id` was felled silently and when its fall ends (game seconds), while its Die has not come; null else.</summary>
        public (float at, float until)? HushedForLab(string id) =>
            id != null && views.TryGetValue(id, out var v) && hushed.TryGetValue(v, out var h) ? (h.at, h.until) : null;

        /// <summary>For the labs: the death `id` went down in (its clip's id), or null.</summary>
        public string DeathForLab(string id) => id != null && views.TryGetValue(id, out var v) ? v.deathId : null;
    }
}
