using System;
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
        readonly Slot[] slots = { null, null, new Slot(), new Slot(), new Slot() };
        int motion;
        float speed = 1;
        // The weapon held standing: two clips, the one going and the one coming, crossfaded.
        AnimationMixerPlayable rest;
        readonly AnimationClipPlayable[] rests = new AnimationClipPlayable[2];
        readonly float[] restTimes = new float[2];
        readonly float[] restLengths = new float[2];
        readonly float[] restWeights = new float[2];
        int restNow = -1;
        float restFade = 0.25f;

        const int Held = 1;
        const int Upper = 2;
        /// <summary>The upper body but the gun arm (syntyAnims.ts 'free': a throw by the free hand, the gun kept as it was held).</summary>
        const int Free = 3;
        const int Whole = 4;

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
            layers = AnimationLayerMixerPlayable.Create(graph, 5);
            motions = AnimationMixerPlayable.Create(graph, Anims.MotionOrder.Length);
            graph.Connect(motions, 0, layers, 0);
            layers.SetInputWeight(0, 1);
            rest = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(rest, 0, layers, Held);
            var upper = UpperBody();
            layers.SetLayerMaskFromAvatarMask(Held, upper);
            layers.SetLayerMaskFromAvatarMask(Upper, upper);
            layers.SetLayerMaskFromAvatarMask(Free, UpperBody(false));
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
        public void Rest(AnimationClip clip, float fade = 0.25f)
        {
            int next = restNow < 0 ? 0 : 1 - restNow;
            if (rests[next].IsValid())
            {
                graph.Disconnect(rest, next);
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
            if (restNow < 0) restWeights[next] = fade <= 0 ? 1 : 0;
            restNow = next;
        }

        /// <summary>A clip over the upper body, once (from `at` seconds into it), fading in and out; its seconds from there.</summary>
        public float PlayUpper(AnimationClip clip, float fade = 0.12f, float speed = 1, float at = 0) => Play(Upper, clip, fade, 0.2f, false, at, speed);

        /// <summary>A clip over the upper body but the gun arm, once (a throw by the free hand); its seconds from `at`.</summary>
        public float PlayFree(AnimationClip clip, float fade = 0.12f, float speed = 1, float at = 0) => Play(Free, clip, fade, 0.2f, false, at, speed);

        /// <summary>A clip over the whole body (from `at` seconds into it); `hold`, it stays on its last pose. Its seconds from there.</summary>
        public float PlayWhole(AnimationClip clip, float fade, bool hold, float at = 0, float speed = 1) => Play(Whole, clip, fade, 0.2f, hold, at, speed);

        /// <summary>
        /// A part of a clip over the whole body, `from` to `to` seconds of it (syntyManifest.ts FEED_CLIPS); `loop`, round
        /// and round, else it holds its last pose. Its seconds.
        /// </summary>
        public float PlayWholePart(AnimationClip clip, float from, float to, float fade, bool loop)
        {
            if (clip == null) return 0;
            float seconds = Play(Whole, clip, fade, 0.2f, true, from, 1);
            var slot = slots[Whole];
            slot.length = Mathf.Clamp(to, from + 1e-3f, clip.length);
            slot.from = from;
            slot.loop = loop;
            return slot.length - from;
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
            if (fadeIn <= 0) slot.weight = 1;
            Step(0);
            return (slot.length - slot.time) / Mathf.Max(speed, 1e-3f);
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
            for (int k = 0; k < weights.Length; k++)
            {
                if (!motionClips[k].IsValid()) continue;
                motions.SetInputWeight(k, total > 0 ? weights[k] / total : 0);
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
                rests[k].SetTime(restTimes[k] % restLengths[k]);
                held += restWeights[k];
            }
            for (int k = 0; k < 2; k++) rest.SetInputWeight(k, held > 0 ? restWeights[k] / held : 0);
            layers.SetInputWeight(Held, Mathf.Min(1, held) * standing);
            for (int layer = Upper; layer <= Whole; layer++)
            {
                var slot = slots[layer];
                if (!slot.clip.IsValid()) continue;
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
                    graph.Disconnect(layers, layer);
                    slot.clip.Destroy();
                }
            }
            graph.Evaluate(0);
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
