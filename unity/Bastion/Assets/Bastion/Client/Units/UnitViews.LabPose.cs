using System;
using UnityEngine;

namespace Bastion.Client.Units
{
    /// <summary>
    /// A lab's pose (the editor's alone): BASTION_POSE=id=clip;id=clip, each of those units held in that clip of the
    /// catalog, looping, whatever it is doing (the bow's and the crossbow's looks before the engine has them, 20/11).
    /// </summary>
    public sealed partial class UnitViews
    {
        static readonly string LabPoses = Application.isEditor ? Environment.GetEnvironmentVariable("BASTION_POSE") : null;

        /// <summary>The clip a lab's unit is in now («id=clip:seconds>clip>clip…»: each for its seconds, or once through; the last held), for the lab.</summary>
        public string LabClipOf(string id) => views.TryGetValue(id, out var v) ? v.labClip : null;

        /// <summary>A lab's unit's row of clips stepped on (Pose runs only when something changes: the lab calls this each frame).</summary>
        public void LabStep(string id)
        {
            if (views.TryGetValue(id, out var v) && state?.Units.Find(u => u.Id == id) is Bastion.Engine.Unit u && v.anim != null) LabPose(v, u, true);
        }

        bool LabPose(View v, Bastion.Engine.Unit u, bool advance = false)
        {
            if (LabPoses == null) return false;
            foreach (string spec in LabPoses.Split(';'))
            {
                var kv = spec.Split('=');
                if (kv.Length != 2 || kv[0] != u.Id) continue;
                var steps = kv[1].Split('>');
                if (advance) v.labTime += Time.deltaTime;
                while (true)
                {
                    var step = steps[Mathf.Min(v.labStep, steps.Length - 1)].Split(':');
                    var clip = FigureCatalog.Load()?.Clip(step[0]);
                    if (v.labClip != step[0])
                    {
                        v.labClip = step[0];
                        var part = Anims.CampPartOf(step[0]);
                        if (clip != null && part?.loop != null && part.loop.Length >= 2) v.anim.CampLoop(clip, true, part.loop[0], part.loop[1]);
                        else v.anim.CampLoop(clip, true);
                    }
                    float lasts = step.Length > 1 ? float.Parse(step[1], System.Globalization.CultureInfo.InvariantCulture) : clip != null ? clip.length : 0;
                    if (v.labStep >= steps.Length - 1 || v.labTime < lasts) break;
                    v.labTime -= lasts;
                    v.labStep++;
                }
                return true;
            }
            return false;
        }
    }
}
