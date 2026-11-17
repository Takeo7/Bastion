using System;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// A run's survivor waiting to be talked to (N2, Gameplay's contract B: Captive, MissionState.Survivor). Hidden on its
    /// own (MissionState.Guarded null), crouched behind what it hides by (Pose's crouch, as before). Held by the Cuervos
    /// (Guarded), knelt on the ground (Mixamo's «Kneel», its held part: the camp's warmHands loop). Once talked to (Captive
    /// gone) it stands up as one of ours. BASTION_SUPERVIVIENTE=agachado|rodillas|suelo (a lab's, in the editor): that way
    /// whichever it is, to set them side by side.
    /// </summary>
    public sealed partial class UnitViews
    {
        static readonly string SurvivorAs = Application.isEditor ? Environment.GetEnvironmentVariable("BASTION_SUPERVIVIENTE") : null;

        /// <summary>The survivor's own pose while it waits: true if it has one now (a camp loop), and Pose leaves it be.</summary>
        bool SurvivorPose(View v, Unit u)
        {
            var mission = state?.Mission;
            bool waiting = u.Captive && u.Alive && mission?.Survivor != null && u.Id == mission.Survivor;
            string way = !waiting ? null : SurvivorAs ?? (mission.Guarded == true ? "rodillas" : "agachado");
            string clip = way == "rodillas" ? "warmHands" : way == "suelo" ? "sitFloor" : null;
            if (clip != v.survivorClip)
            {
                v.survivorClip = clip;
                var c = clip != null ? FigureCatalog.Load()?.Clip(clip) : null;
                var part = Anims.CampPartOf(clip);
                if (c != null && part?.loop != null && part.loop.Length >= 2) v.anim.CampLoop(c, !v.synced, part.loop[0], part.loop[1]);
                else v.anim.CampLoop(c, !v.synced);
            }
            return clip != null;
        }
    }
}
