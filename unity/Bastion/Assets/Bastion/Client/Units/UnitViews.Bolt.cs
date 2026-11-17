using System.Collections.Generic;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// The bolt-action rifle's shot (Arturo's T14 of 18/11: «el fusil de cerrojo solo tiene un disparo y sin embargo ataca
    /// como si fuese un rifle de asalto»): one round, the engine's (its burst), and after its kick the bolt worked, there
    /// being no clip of it: the right hand off the grip to the bolt's handle (CerrojoLab.Medir: out on the right of the
    /// receiver, just behind the grip), back BoltPull and forward again, and back to the grip. Two-bone IK over the pose,
    /// after the gun is held (HoldNow puts it by the hand as the clip has it, so the gun stays where it is).
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>The bolt-action guns, by their model (WeaponArt.fbx).</summary>
        static readonly HashSet<string> BoltGuns = new HashSet<string> { "SM_Wep_HuntingRifle_01", "SM_Wep_HuntingRifle_Clean_01" };
        /// <summary>
        /// The bolt's handle in the gun's own frame (metres, unscaled), the hand a little over it (the handle lifted: it is
        /// only some 8 cm from the grip, and the bolt is part of the gun's mesh, so the hand's way is all that shows), and how
        /// far back it is pulled (a long pull, for it to be read).
        /// </summary>
        static readonly Vector3 BoltHandle = new Vector3(0.05f, 0.125f, -0.03f);
        const float BoltPull = 0.10f;
        /// <summary>Its times after the shot (s): the hand off to the handle, pulled back, pushed home, back on the grip.</summary>
        const float BoltFrom = 0.12f, BoltAt = 0.28f, BoltBack = 0.43f, BoltHome = 0.58f, BoltDone = 0.75f;

        static bool BoltInHand(View v) => v.weapon != null && v.inHand == Held.Rifle && v.drawn != null && v.arts.TryGetValue(v.drawn, out var art) && art.fbx != null && BoltGuns.Contains(art.fbx);

        /// <summary>The bolt worked after a shot (Recoil starts it), a step.</summary>
        static void WorkBolt(View v, float dt)
        {
            if (v.bolt < 0) return;
            v.bolt += dt;
            if (v.bolt >= BoltDone || !BoltInHand(v) || v.dead || v.animator == null)
            {
                v.bolt = -1;
                return;
            }
            float t = v.bolt;
            float weight = t < BoltFrom ? 0 : t < BoltAt ? Mathf.SmoothStep(0, 1, (t - BoltFrom) / (BoltAt - BoltFrom)) : t < BoltHome ? 1 : Mathf.SmoothStep(1, 0, (t - BoltHome) / (BoltDone - BoltHome));
            if (weight <= 0) return;
            float back = t < BoltAt ? 0 : t < BoltBack ? Mathf.SmoothStep(0, 1, (t - BoltAt) / (BoltBack - BoltAt)) : t < BoltHome ? Mathf.SmoothStep(1, 0, (t - BoltBack) / (BoltHome - BoltBack)) : 0;
            var a = v.animator;
            var hand = a.GetBoneTransform(HumanBodyBones.RightHand);
            var knuckle = a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if (hand == null) return;
            var grip = v.weapon.TransformPoint(BoltHandle + Vector3.back * (BoltPull * back));
            for (int pass = 0; pass < 2; pass++)
            {
                var palm = knuckle != null ? Vector3.Lerp(hand.position, knuckle.position, 0.6f) : hand.position;
                var goal = Vector3.Lerp(palm, grip, weight);
                Reach(a, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, hand.position + (goal - palm), v.root.right - v.root.up * 0.5f, 0.005f);
            }
        }
    }
}
