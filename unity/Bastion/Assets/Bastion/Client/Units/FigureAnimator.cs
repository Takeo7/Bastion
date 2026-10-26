using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Bastion.Client.Units
{
    /// <summary>
    /// What a figure's body does (0.10.1's syntyAnims.ts AnimDriver), on Unity's humanoid rig: the clips it stands and
    /// moves in blended into each other (MOTION_ORDER); over the upper body, how it holds its weapon while it stands (at
    /// rest, aimed: Rest), and a clip once (a hit, a shot: the legs keep what they do); and one over the whole body (a
    /// death, which it then lies in). Stepped by hand (Step), the same every time, in play mode and in the captures alike.
    /// </summary>
    public sealed class FigureAnimator : IDisposable
    {
        /// <summary>syntyAnims.ts BLEND: seconds from one motion to the next.</summary>
        const float Blend = 0.35f;

        sealed class Slot
        {
            public AnimationClipPlayable clip;
            public float time;
            public float length;
            public float speed = 1;
            public float weight;
            public float fadeIn;
            public float fadeOut;
            /// <summary>Stays on its last pose (a death); otherwise it fades out as it ends.</summary>
            public bool hold;
            /// <summary>Played round and round from `from` (a dead one eating), until something else takes its place.</summary>
            public bool loop;
            public float from;
            public bool stopping;
            /// <summary>Its time and weight set from outside every frame (Scrub: a reload, a blade's stroke), not by fades.</summary>
            public bool manual;
        }

        PlayableGraph graph;
        AnimationLayerMixerPlayable layers;
        AnimationMixerPlayable motions;
        readonly AnimationClipPlayable[] motionClips = new AnimationClipPlayable[Anims.MotionOrder.Length];
        readonly float[] lengths = new float[Anims.MotionOrder.Length];
        readonly float[] weights = new float[Anims.MotionOrder.Length];
        readonly float[] times = new float[Anims.MotionOrder.Length];
        readonly float[] paces = new float[Anims.MotionOrder.Length];
        /// <summary>The motions whose clip holds the gun its own way (Mixamo's armed walk, run and sprint): the held clip off during them.</summary>
        readonly bool[] ownHold = new bool[Anims.MotionOrder.Length];
        /// <summary>The layers that play once (Upper, Free, Whole), by their index (the motions, the gun arm and the held rest have none).</summary>
        readonly Slot[] slots = { null, null, null, new Slot(), new Slot(), new Slot() };
        int motion;
        float speed = 1;
        // The weapon held standing: two clips, the one going and the one coming, crossfaded.
        AnimationMixerPlayable rest;
        readonly AnimationClipPlayable[] rests = new AnimationClipPlayable[2];
        readonly float[] restTimes = new float[2];
        readonly float[] restLengths = new float[2];
        readonly float[] restWeights = new float[2];
        /// <summary>Each rest's time held fast (≥ 0: syntyFigures.ts PISTOL_HOLD), else round and round.</summary>
        readonly float[] restFrozen = { -1, -1 };
        int restNow = -1;
        float restFade = 0.25f;

        /// <summary>
        /// At the camp (syntyAnims.ts campLoop, campVariations): a clip stayed in instead of the idle (sat, knelt, leant,
        /// drinking), eased in and out over StanceBlend (two, the one going and the one coming); a part of one, played back
        /// and forth if its ends don't meet; and a clip once now and then in the idle's place (a stretch, a yawn), eased in
        /// and out over Blend, let go of in VariationCut once it is to stop.
        /// </summary>
        const float StanceBlend = 0.6f, VariationCut = 0.2f;
        sealed class CampClip
        {
            public AnimationClipPlayable clip;
            public float time, from, length, weight;
            public bool pingPong;
        }
        readonly CampClip[] campLoops = { new CampClip(), new CampClip() };
        int campNow = -1;
        readonly CampClip variation = new CampClip();
        float variationKeep;
        bool variationStopping;
        int CampInput(int k) => Anims.MotionOrder.Length + k;

        /// <summary>The gun arm alone (ArmRest: a pistol at rest), under the weapon held at rest and all that plays over it.</summary>
        const int ArmLayer = 1;
        const int Held = 2;
        const int Upper = 3;
        /// <summary>The upper body but the gun arm (syntyAnims.ts 'free': a throw by the free hand, the gun kept as it was held).</summary>
        const int Free = 4;
        const int Whole = 5;

        /// <summary>
        /// `clipOf` gives each of MOTION_ORDER's clips (idle, crouchIdle, walk, run, crouchWalk, sprint), or null; `paceOf`
        /// how much faster than the gait's speed one plays (a soldier's armed walk: gaits.ts armedPace).
        /// </summary>
        public FigureAnimator(Animator animator, Func<string, AnimationClip> clipOf, Func<string, float> paceOf = null, Func<string, bool> holdsOwn = null)
        {
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            graph = PlayableGraph.Create(animator.gameObject.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "figura", animator);
            layers = AnimationLayerMixerPlayable.Create(graph, 6);
            // (And after the motions, the camp's: two loops, the one going and the one coming, and a variation: CampLoop, Vary.)
            motions = AnimationMixerPlayable.Create(graph, Anims.MotionOrder.Length + 3);
            graph.Connect(motions, 0, layers, 0);
            layers.SetInputWeight(0, 1);
            rest = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(rest, 0, layers, Held);
            var upper = UpperBody();
            layers.SetLayerMaskFromAvatarMask(Held, upper);
            layers.SetLayerMaskFromAvatarMask(Upper, upper);
            layers.SetLayerMaskFromAvatarMask(Free, UpperBody(false));
            layers.SetLayerMaskFromAvatarMask(ArmLayer, GunArm());
            for (int k = 0; k < Anims.MotionOrder.Length; k++)
            {
                var clip = clipOf(Anims.MotionOrder[k]);
                if (clip == null) continue;
                motionClips[k] = AnimationClipPlayable.Create(graph, clip);
                motionClips[k].SetApplyFootIK(false);
                lengths[k] = Mathf.Max(clip.length, 1e-3f);
                paces[k] = paceOf?.Invoke(Anims.MotionOrder[k]) ?? 1;
                ownHold[k] = holdsOwn?.Invoke(Anims.MotionOrder[k]) ?? k > 1;
                graph.Connect(motionClips[k], 0, motions, k);
            }
            weights[0] = 1;
            output.SetSourcePlayable(layers);
            graph.Play();
            Step(0);
        }

        /// <summary>The trunk, head and arms (the legs and the root left to the motions underneath); `gunArm` false, the right arm too.</summary>
        static AvatarMask UpperBody(bool gunArm = true)
        {
            var mask = new AvatarMask();
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm
                    || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.LeftHandIK
                    || (gunArm && (part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.RightFingers || part == AvatarMaskBodyPart.RightHandIK)));
            return mask;
        }

        /// <summary>Stands or moves in one of MOTION_ORDER's clips, at `speed` times its own (its feet kept on the ground: Gaits.ClipSpeed).</summary>
        public void Motion(string name, float speed = 1)
        {
            int k = Array.IndexOf(Anims.MotionOrder, name);
            if (k < 0 || !motionClips[k].IsValid()) k = 0;
            motion = k;
            this.speed = speed;
        }

        /// <summary>
        /// How it holds its weapon standing (a rifle at rest, aimed), looped over the upper body as long as it stands (the
        /// moving clips hold it their own way); null, nothing (unarmed). Crossfaded over `fade` seconds.
        /// </summary>
        public void Rest(AnimationClip clip, float fade = 0.25f) => Rest(clip, fade, -1);

        /// <summary>The same, held at `at` seconds into it (a pistol at rest by the thigh: the tail of «Pistol Aim», PISTOL_HOLD) when it is ≥ 0.</summary>
        public void Rest(AnimationClip clip, float fade, float at)
        {
            int next = restNow < 0 ? 0 : 1 - restNow;
            if (rests[next].IsValid())
            {
                Unplug(rest, next);
                rests[next].Destroy();
            }
            restFade = Mathf.Max(fade, 1e-3f);
            if (clip == null)
            {
                restNow = -1;
                return;
            }
            rests[next] = AnimationClipPlayable.Create(graph, clip);
            rests[next].SetApplyFootIK(false);
            graph.Connect(rests[next], 0, rest, next);
            restTimes[next] = 0;
            restLengths[next] = Mathf.Max(clip.length, 1e-3f);
            restFrozen[next] = at >= 0 ? Mathf.Min(at, clip.length) : -1;
            if (restNow < 0) restWeights[next] = fade <= 0 ? 1 : 0;
            restNow = next;
        }

        /// <summary>
        /// A clip over the gun arm alone, held at `at` seconds into it (a pistol at rest by the thigh: the tail of «Pistol
        /// Aim», syntyFigures.ts PISTOL_HOLD, over GUN_ARM, the rest of the body its stance), fading in or out over `fade`;
        /// null lets it go. Under the rests: a pistol aimed with both hands (Rest) takes over from it as it fades.
        /// </summary>
        public void ArmRest(AnimationClip clip, float at, float fade = 0.25f)
        {
            armFade = Mathf.Max(fade, 1e-3f);
            if (clip == null)
            {
                armOn = false;
                return;
            }
            if (!armClip.IsValid() || armClip.GetAnimationClip() != clip)
            {
                if (armClip.IsValid())
                {
                    Unplug(layers, ArmLayer);
                    armClip.Destroy();
                }
                armClip = AnimationClipPlayable.Create(graph, clip);
                armClip.SetApplyFootIK(false);
                graph.Connect(armClip, 0, layers, ArmLayer);
                if (fade <= 0) armWeight = 1;
            }
            armClip.SetTime(Mathf.Clamp(at, 0, clip.length));
            armOn = true;
        }

        AnimationClipPlayable armClip;
        bool armOn;
        float armWeight, armFade = 0.25f;

        /// <summary>The right arm alone (syntyAnims.ts GUN_ARM: clavicle, shoulder, elbow, hand).</summary>
        static AvatarMask GunArm()
        {
            var mask = new AvatarMask();
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.RightFingers || part == AvatarMaskBodyPart.RightHandIK);
            return mask;
        }

        /// <summary>
        /// A clip over the upper body at `time` seconds into it, weighing `weight`, both set every frame from outside (a
        /// reload scrubbed, a blade's stroke on its own clock: syntyFigures.ts direct's spin, strike); `clip` null lets go.
        /// </summary>
        public void Scrub(AnimationClip clip, float time, float weight)
        {
            var slot = slots[Upper];
            if (clip == null)
            {
                if (slot.manual && slot.clip.IsValid())
                {
                    Unplug(layers, Upper);
                    slot.clip.Destroy();
                }
                slot.manual = false;
                return;
            }
            if (!slot.manual || !slot.clip.IsValid() || slot.clip.GetAnimationClip() != clip)
            {
                Play(Upper, clip, 0, 0, true, 0, 0);
                slot.manual = true;
            }
            slot.time = Mathf.Clamp(time, 0, slot.length);
            slot.weight = Mathf.Clamp01(weight);
            slot.clip.SetTime(slot.time);
            layers.SetInputWeight(Upper, slot.weight);
        }

        /// <summary>A clip over the upper body, once (from `at` seconds into it), fading in and out; its seconds from there.</summary>
        public float PlayUpper(AnimationClip clip, float fade = 0.12f, float speed = 1, float at = 0) => Play(Upper, clip, fade, 0.2f, false, at, speed);

        /// <summary>A clip over the upper body but the gun arm, once (a throw by the free hand); its seconds from `at`.</summary>
        public float PlayFree(AnimationClip clip, float fade = 0.12f, float speed = 1, float at = 0) => Play(Free, clip, fade, 0.2f, false, at, speed);

        /// <summary>A clip over the whole body (from `at` seconds into it); `hold`, it stays on its last pose. Its seconds from there.</summary>
        public float PlayWhole(AnimationClip clip, float fade, bool hold, float at = 0, float speed = 1) => Play(Whole, clip, fade, 0.2f, hold, at, speed);

        /// <summary>
        /// A part of a clip over the whole body, `from` to `to` seconds of it (syntyManifest.ts FEED_CLIPS, CAMP_SPANS) at
        /// `speed` times its own; `loop`, round and round, else it holds its last pose. Its seconds.
        /// </summary>
        public float PlayWholePart(AnimationClip clip, float from, float to, float fade, bool loop, float speed = 1)
        {
            if (clip == null) return 0;
            Play(Whole, clip, fade, 0.2f, true, from, speed);
            var slot = slots[Whole];
            slot.length = Mathf.Clamp(to, from + 1e-3f, clip.length);
            slot.from = from;
            slot.loop = loop;
            return (slot.length - from) / Mathf.Max(speed, 1e-3f);
        }

        /// <summary>Stands or moves in `name` (one of MOTION_ORDER) in another clip from now on (a stance: the wounded's slump).</summary>
        public void SetMotion(string name, AnimationClip clip)
        {
            int k = Array.IndexOf(Anims.MotionOrder, name);
            if (k < 0 || clip == null) return;
            if (motionClips[k].IsValid())
            {
                graph.Disconnect(motions, k);
                motionClips[k].Destroy();
            }
            motionClips[k] = AnimationClipPlayable.Create(graph, clip);
            motionClips[k].SetApplyFootIK(false);
            lengths[k] = Mathf.Max(clip.length, 1e-3f);
            graph.Connect(motionClips[k], 0, motions, k);
        }

        /// <summary>
        /// Stays in `clip` instead of the idle (from `from` to `to` seconds of it, round and round; `pingPong`, back and
        /// forth), or in none (null): eased over StanceBlend, or at once (`now`: where a move into it left the body).
        /// </summary>
        public void CampLoop(AnimationClip clip, bool now = false, float from = 0, float to = -1, bool pingPong = false)
        {
            var current = campNow >= 0 ? campLoops[campNow] : null;
            if (clip == null)
            {
                campNow = -1;
                if (now) foreach (var c in campLoops) c.weight = 0;
                return;
            }
            if (current == null || !current.clip.IsValid() || current.clip.GetAnimationClip() != clip || current.from != from)
            {
                int next = campNow < 0 ? (campLoops[0].weight <= campLoops[1].weight ? 0 : 1) : 1 - campNow;
                var c = campLoops[next];
                if (c.clip.IsValid())
                {
                    graph.Disconnect(motions, CampInput(next));
                    c.clip.Destroy();
                }
                c.clip = AnimationClipPlayable.Create(graph, clip);
                c.clip.SetApplyFootIK(false);
                graph.Connect(c.clip, 0, motions, CampInput(next));
                c.from = Mathf.Clamp(from, 0, clip.length);
                c.length = Mathf.Max((to > c.from ? Mathf.Min(to, clip.length) : clip.length) - c.from, 1e-3f);
                c.time = 0;
                c.weight = 0;
                c.pingPong = pingPong;
                campNow = next;
            }
            if (now)
                for (int k = 0; k < 2; k++) campLoops[k].weight = k == campNow ? 1 : 0;
        }

        /// <summary>Whether a camp clip is stayed in now (or easing out).</summary>
        public bool InCamp => campLoops[0].weight > 0 || campLoops[1].weight > 0;

        /// <summary>Plays `clip` once in the idle's place (a variation), eased in and out; its seconds.</summary>
        public float Vary(AnimationClip clip)
        {
            if (clip == null) return 0;
            if (variation.clip.IsValid())
            {
                graph.Disconnect(motions, CampInput(2));
                variation.clip.Destroy();
            }
            variation.clip = AnimationClipPlayable.Create(graph, clip);
            variation.clip.SetApplyFootIK(false);
            graph.Connect(variation.clip, 0, motions, CampInput(2));
            variation.time = 0;
            variation.from = 0;
            variation.length = Mathf.Max(clip.length, 1e-3f);
            variationKeep = 1;
            variationStopping = false;
            return variation.length;
        }

        /// <summary>Whether a variation plays now; and stopping it (it goes over VariationCut).</summary>
        public bool Varying => variation.clip.IsValid();

        public void StopVariation() => variationStopping = true;

        /// <summary>The whole body's clip let go (a body getting up), over `fade` seconds.</summary>
        public void StopWhole(float fade)
        {
            var slot = slots[Whole];
            slot.hold = false;
            slot.stopping = true;
            slot.fadeOut = Mathf.Max(fade, 1e-3f);
        }

        float Play(int layer, AnimationClip clip, float fadeIn, float fadeOut, bool hold, float at, float speed)
        {
            if (clip == null) return 0;
            var slot = slots[layer];
            if (slot.clip.IsValid())
            {
                graph.Disconnect(layers, layer);
                slot.clip.Destroy();
            }
            slot.clip = AnimationClipPlayable.Create(graph, clip);
            slot.clip.SetApplyFootIK(false);
            graph.Connect(slot.clip, 0, layers, layer);
            slot.length = Mathf.Max(clip.length, 1e-3f);
            slot.time = Mathf.Min(at, slot.length);
            slot.speed = speed;
            slot.fadeIn = fadeIn;
            slot.fadeOut = fadeOut;
            slot.hold = hold;
            slot.stopping = false;
            slot.loop = false;
            slot.from = 0;
            slot.manual = false;
            if (fadeIn <= 0) slot.weight = 1;
            Step(0);
            return (slot.length - slot.time) / Mathf.Max(speed, 1e-3f);
        }

        /// <summary>The layers' weights and the motions mixer's inputs' (MOTION_ORDER, the camp's two loops, the variation), for the labs.</summary>
        /// <summary>How far it moves rather than stands (syntyFigures.ts pose.run: the motions' share but the two idles'), 0 to 1.</summary>
        public float Moving
        {
            get
            {
                float total = 0, still = 0;
                for (int k = 0; k < weights.Length; k++)
                {
                    if (!motionClips[k].IsValid()) continue;
                    total += weights[k];
                    if (k <= 1) still += weights[k];
                }
                return total > 0 ? 1 - still / total : 0;
            }
        }

        /// <summary>
        /// A sway kept in step with the clips (syntyFigures.ts sway): a sine about `still` radians a second through the idle
        /// it stands in and `moving[k]` through motion k it moves in, each a whole number of times its loop, blended as far
        /// as it moves (Moving).
        /// </summary>
        public float Sway(float still, float[] moving, float offset = 0)
        {
            float At(int k, float w) => lengths[k] > 0 ? Mathf.Sin(2 * Mathf.PI * Mathf.Max(1, Mathf.Round(w * lengths[k] / (2 * Mathf.PI))) * (times[k] % lengths[k] / lengths[k]) + offset) : 0;
            int moves = -1;
            for (int k = 2; k < weights.Length; k++) if (motionClips[k].IsValid() && (moves < 0 || weights[k] > weights[moves])) moves = k;
            int stands = weights[1] > weights[0] && motionClips[1].IsValid() ? 1 : 0;
            float run = Moving;
            return Mathf.Lerp(run < 1 && motionClips[stands].IsValid() ? At(stands, still) : 0, run > 0 && moves >= 0 ? At(moves, moving[moves]) : 0, run);
        }

        public string Weights()
        {
            if (!graph.IsValid()) return "-";
            var layer = string.Join(" ", Enumerable.Range(0, layers.GetInputCount()).Select(k => layers.GetInputWeight(k).ToString("0.00")));
            var mix = string.Join(" ", Enumerable.Range(0, motions.GetInputCount()).Select(k => motions.GetInputWeight(k).ToString("0.00")));
            return $"layers {layer} | motions {mix}";
        }

        public void Step(float dt)
        {
            if (!graph.IsValid()) return;
            float total = 0;
            for (int k = 0; k < weights.Length; k++)
            {
                if (!motionClips[k].IsValid()) continue;
                weights[k] = k == motion ? Mathf.Min(1, weights[k] + dt / Blend) : Mathf.Max(0, weights[k] - dt / Blend);
                if (dt == 0 && k == motion && weights[k] == 0) weights[k] = 1;
                total += weights[k];
                times[k] += dt * (k == motion ? speed * paces[k] : paces[k]);
            }
            // The camp's loop and variation take the idle's place (syntyAnims.ts update: idle × held, the idle what is left).
            float idleShare = total > 0 && motionClips[0].IsValid() ? weights[0] / total : 0;
            float stood = 0;
            for (int k = 0; k < 2; k++)
            {
                var c = campLoops[k];
                c.weight = Mathf.Clamp01(c.weight + (k == campNow ? dt : -dt) / StanceBlend);
                if (!c.clip.IsValid()) continue;
                c.time += dt;
                float t = c.pingPong ? Mathf.PingPong(c.time, c.length) : c.time % c.length;
                c.clip.SetTime(c.from + t);
                motions.SetInputWeight(CampInput(k), idleShare * c.weight);
                stood += c.weight;
                if (c.weight <= 0 && k != campNow)
                {
                    Unplug(motions, CampInput(k));
                    c.clip.Destroy();
                }
            }
            stood = Mathf.Min(1, stood);
            float varied = 0;
            if (variation.clip.IsValid())
            {
                variation.time += dt;
                variationKeep = Mathf.Clamp01(variationKeep + (variationStopping ? -dt : dt) / VariationCut);
                if (variation.time >= variation.length - 1e-3f || variationKeep <= 0)
                {
                    Unplug(motions, CampInput(2));
                    variation.clip.Destroy();
                }
                else
                {
                    variation.clip.SetTime(variation.time);
                    varied = Mathf.Min(1, Mathf.Min(variation.time / Blend, (variation.length - variation.time) / Blend)) * idleShare * (1 - stood) * variationKeep;
                    motions.SetInputWeight(CampInput(2), varied);
                }
            }
            for (int k = 0; k < weights.Length; k++)
            {
                if (!motionClips[k].IsValid()) continue;
                float w = total > 0 ? weights[k] / total : 0;
                if (k == 0) w = w * (1 - stood) - varied;
                motions.SetInputWeight(k, Mathf.Max(0, w));
                motionClips[k].SetTime(times[k] % lengths[k]);
            }
            // The weapon held as long as it stands, or moves in a clip that doesn't hold it (crouched: Synty's legs, the gun
            // at rest over them): as much as those weigh.
            float standing = 0;
            for (int k = 0; k < weights.Length; k++) if (!ownHold[k] && motionClips[k].IsValid()) standing += weights[k];
            standing = total > 0 ? standing / total : 1;
            float held = 0;
            for (int k = 0; k < 2; k++)
            {
                if (!rests[k].IsValid()) continue;
                restWeights[k] = k == restNow ? Mathf.Min(1, restWeights[k] + dt / restFade) : Mathf.Max(0, restWeights[k] - dt / restFade);
                restTimes[k] += dt;
                rests[k].SetTime(restFrozen[k] >= 0 ? restFrozen[k] : restTimes[k] % restLengths[k]);
                held += restWeights[k];
            }
            for (int k = 0; k < 2; k++) rest.SetInputWeight(k, held > 0 ? restWeights[k] / held : 0);
            layers.SetInputWeight(Held, Mathf.Min(1, held) * standing);
            // The pistol at rest: as long as it stands (moving, Synty's own arms swing: syntyAnims.ts 'own').
            armWeight = armOn ? Mathf.Min(1, armWeight + dt / armFade) : Mathf.Max(0, armWeight - dt / armFade);
            if (armClip.IsValid())
            {
                layers.SetInputWeight(ArmLayer, armWeight * standing);
                if (!armOn && armWeight <= 0)
                {
                    Unplug(layers, ArmLayer);
                    armClip.Destroy();
                }
            }
            for (int layer = Upper; layer <= Whole; layer++)
            {
                var slot = slots[layer];
                if (!slot.clip.IsValid()) continue;
                if (slot.manual)
                {
                    slot.clip.SetTime(slot.time);
                    layers.SetInputWeight(layer, slot.weight);
                    continue;
                }
                slot.time += dt * slot.speed;
                if (slot.loop && slot.time >= slot.length) slot.time = slot.from + (slot.time - slot.from) % Mathf.Max(slot.length - slot.from, 1e-3f);
                slot.time = Mathf.Min(slot.length, slot.time);
                bool ending = slot.stopping || (!slot.hold && slot.time >= slot.length - slot.fadeOut * slot.speed);
                slot.weight = ending
                    ? Mathf.Max(0, slot.weight - dt / Mathf.Max(slot.fadeOut, 1e-3f))
                    : Mathf.Min(1, slot.weight + (slot.fadeIn > 0 ? dt / slot.fadeIn : 1));
                slot.clip.SetTime(slot.time);
                layers.SetInputWeight(layer, slot.weight);
                if (ending && slot.weight <= 0)
                {
                    Unplug(layers, layer);
                    slot.clip.Destroy();
                }
            }
            graph.Evaluate(0);
        }

        /// <summary>
        /// An input let go of with its weight to 0: Unity sets a disconnected input's weight back to 1, and a mixer blends an
        /// empty input as the default pose (the arms out level, as in a T-pose: the camp's drink, Gameplay 26/10).
        /// </summary>
        void Unplug(Playable mixer, int input)
        {
            graph.Disconnect(mixer, input);
            mixer.SetInputWeight(input, 0);
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
