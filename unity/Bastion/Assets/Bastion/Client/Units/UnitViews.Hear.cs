using System;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// One of the enemy seen turning to a noise it heard (E3 of Arturo's notes of 15/11: «se lanzó una piedra detrás de un
    /// zombi… no se le vio girar ni ir hacia el ruido»). The engine turns whoever hears a noise towards it (NoiseEvent:
    /// Unit.Heard and Facing, Events.ApplyEvent), but a figure took its facing only as it was made: it stood as it was till
    /// it walked. Now, synced with something new heard (Sync, once the event player has the noise on the view: no call of
    /// its own), its head and chest go round first (HearLead, over HearDelay), then its body, at the usual pace on the
    /// spot (TurnTo; TurnMost), head and chest coming back as the body gets there. Off the camera or in the fog it turns
    /// all the same, unseen (only its bones wait). Its going there is its walk, the next events (Walk turns it as it sets
    /// off). Not while it walks, strikes, eats or reaches: what it does turns it.
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>
        /// Seconds its head and chest lead before its body sets off round; how far round they go at most (degrees: the
        /// chest's share, the neck's, the head the rest); how far off the noise has to be for it to turn (degrees).
        /// </summary>
        const float HearDelay = 0.25f, HearLeadMost = 55, HearChest = 0.35f, HearNeck = 0.3f, HearFrom = 10;
        /// <summary>BASTION_SIN_OIR=1: a noise turns nobody (the labs' «before»).</summary>
        public static bool HearOff = Environment.GetEnvironmentVariable("BASTION_SIN_OIR") == "1";

        /// <summary>What `u` has heard, from the state seen (Sync): something new, and it turns to it (the engine's Facing).</summary>
        void Hear(View v, Unit u)
        {
            var was = v.heard;
            v.heard = u.Heard == null ? null : new Pos { X = u.Heard.X, Y = u.Heard.Y, Z = u.Heard.Z };
            if (HearOff || !v.synced || u.Heard == null || u.Team == Teams.Xcom || !u.Alive || v.dead || v.root == null) return;
            if (was != null && was.X == u.Heard.X && was.Y == u.Heard.Y && was.Z == u.Heard.Z) return;
            if (v.walking || v.striking || v.feeding || v.reachFor != null || !(u.Facing is double facing)) return;
            float yaw = YawOf(facing);
            if (Mathf.Abs(Mathf.DeltaAngle(v.yawTo ?? v.root.eulerAngles.y, yaw)) < HearFrom) return;
            v.hearYaw = yaw;
            v.hearClock = 0;
            v.hearTurning = false;
        }

        /// <summary>The turn to a noise on by `dt` (Step): its body set off round once its head and chest have led; over once there.</summary>
        static void StepHear(View v, float dt)
        {
            // Walking, striking, eating, dead, or turned another way since (an aim, a blow): that one's.
            if (v.walking || v.striking || v.feeding || v.dead || v.root == null
                || (v.hearTurning && v.yawTo is float other && Mathf.Abs(Mathf.DeltaAngle(other, v.hearYaw)) > 1))
            {
                v.hearClock = -1;
                return;
            }
            v.hearClock += dt;
            if (!v.hearTurning && v.hearClock >= HearDelay)
            {
                v.hearTurning = true;
                TurnTo(v, v.hearYaw);
            }
            if (v.hearTurning && Mathf.Abs(Mathf.DeltaAngle(v.root.eulerAngles.y, v.hearYaw)) < 1) v.hearClock = -1;
        }

        /// <summary>
        /// Its head and chest turned ahead of its body to the noise (Step, over its clip and DeadPose): as far as is left to
        /// turn, HearLeadMost at most, a little at first (eased in over HearDelay), back to none as the body gets there.
        /// </summary>
        static void HearLead(View v)
        {
            if (v.hearClock < 0 || v.animator == null || !v.animator.isHuman || v.dead) return;
            float left = Mathf.DeltaAngle(v.root.eulerAngles.y, v.hearYaw);
            float s = v.hearTurning ? 1 : Mathf.Clamp01(v.hearClock / HearDelay);
            float lead = Mathf.Clamp(left, -HearLeadMost, HearLeadMost) * s * s * (3 - 2 * s);
            if (Mathf.Abs(lead) < 0.05f) return;
            var a = v.animator;
            var chest = a.GetBoneTransform(HumanBodyBones.UpperChest) ?? a.GetBoneTransform(HumanBodyBones.Chest) ?? a.GetBoneTransform(HumanBodyBones.Spine);
            var neck = a.GetBoneTransform(HumanBodyBones.Neck);
            var head = a.GetBoneTransform(HumanBodyBones.Head);
            if (chest != null) chest.rotation = Quaternion.AngleAxis(lead * HearChest, Vector3.up) * chest.rotation;
            if (neck != null) neck.rotation = Quaternion.AngleAxis(lead * HearNeck, Vector3.up) * neck.rotation;
            if (head != null) head.rotation = Quaternion.AngleAxis(lead * (1 - HearChest - HearNeck), Vector3.up) * head.rotation;
        }
    }
}
