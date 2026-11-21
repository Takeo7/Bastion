using System.Linq;
using Bastion.Engine;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// G23.5, Saqueadores: the raiders on guard by their camp's fire (Unit.Guard «fire», Gameplay's contract of 20/11), sat
    /// on its chairs (the base's loop, as Ride's), turned as the chair is, while their pod sleeps and they have heard
    /// nothing; up as anyone once either changes, and sat again when they are back and it is quiet.
    /// </summary>
    public sealed partial class UnitViews
    {
        /// <summary>The guard's own pose on its chair: true if it has it now, and Pose leaves it be.</summary>
        bool GuardPose(View v, Unit u)
        {
            // (As the engine's Rules.Sitting, but for its pod's stop, which a player's view of the state may not carry: of
            // «fire», its pod asleep, nothing heard, and on one of the camp's chairs.)
            bool sat = u.Guard == GuardPosts.Fire && u.Alive && u.Heard == null && !v.walking
                && state?.Pods?.FirstOrDefault(p => p.Id == u.PodId)?.Active != true
                && state?.Interiors != null && state.Interiors.Exists(site => site?.Dressing != null
                    && site.Dressing.Exists(d => d.Kind == CampDetailKinds.CampChair && d.X == u.Pos.X && d.Y == u.Pos.Y && d.Z == u.Pos.Z));
            string clip = sat ? "sitChair" : null;
            if (clip != v.guardClip)
            {
                v.guardClip = clip;
                var c = clip != null ? FigureCatalog.Load()?.Clip(clip) : null;
                var part = Anims.CampPartOf(clip);
                if (c != null && part?.loop != null && part.loop.Length >= 2) v.anim.CampLoop(c, !v.synced, part.loop[0], part.loop[1]);
                else v.anim.CampLoop(c, !v.synced);
            }
            return clip != null;
        }
    }
}
