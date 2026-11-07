using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// Where the mission's goal lies, bottom right (UI-COMBATE UI-06): the map's black ring with a white arrow in it
    /// (Ana, 7/11 #4: Synty's ring and reticle arrow), turned every frame to where the goal is on the screen from its
    /// middle (the edge marks' way: EdgeMarks), so the camera's turns turn it too. No words, no distance; nothing happens
    /// on a click: its help says what it is. The goal is the presenter's, as the O key's (the objective while it is to
    /// do, else the evacuation); none, no ring.
    /// </summary>
    public sealed class GoalArrow
    {
        readonly VisualElement host;
        readonly VisualElement arrow;
        Vector3? goal;

        public GoalArrow(VisualElement host, TooltipLayer tips = null)
        {
            this.host = host;
            host.Add(new Ring());
            arrow = new Arrow();
            host.Add(arrow);
            // Its help only (Ana, 7/11 #5: it was taken for a second «Terminar turno»).
            tips?.Attach(host, HudText.Goal.Tip);
            Ui.Show(host, false);
        }

        /// <summary>The goal's point in the world; null, none (the ring goes).</summary>
        public void Set(Vector3? world)
        {
            goal = world;
            Ui.Show(host, world != null);
        }

        /// <summary>Each frame: the arrow turned towards the goal as the camera sees it (behind it, mirrored).</summary>
        public void Follow(Camera camera)
        {
            if (goal == null || camera == null) return;
            var vp = camera.WorldToViewportPoint(goal.Value);
            var dx = vp.x - 0.5f;
            // Screen y goes down here.
            var dy = 0.5f - vp.y;
            if (vp.z < 0)
            {
                dx = -dx;
                dy = -dy;
            }
            // In the screen's own proportions, not the viewport's square.
            dx *= camera.aspect;
            if (Mathf.Abs(dx) < 1e-5f && Mathf.Abs(dy) < 1e-5f) return;
            // The sprite points up: a quarter turn more than the screen's angle from the right.
            arrow.style.rotate = new Rotate(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 90);
        }

        /// <summary>
        /// The ring: Synty INTERFACE Apocalypse HUD's SPR_HUD_Apocalypse_Map_Circle_Large_02 in black, as the torn plates
        /// (Ana, 7/11 #4; EdgeMarks.uss .map-ring); the enemies' sight's button too.
        /// </summary>
        internal sealed class Ring : VisualElement
        {
            public Ring()
            {
                AddToClassList("map-ring");
                pickingMode = PickingMode.Ignore;
            }
        }

        /// <summary>
        /// The arrow: Synty INTERFACE Dark Fantasy HUD's SPR_HUD_DarkFantasy_Reticle_Arrow_Small_08_Variant_02, white (Ana,
        /// 7/11 #4: no orange), pointing up before it is turned.
        /// </summary>
        sealed class Arrow : VisualElement
        {
            public Arrow()
            {
                AddToClassList("goal-arrow__arrow");
                pickingMode = PickingMode.Ignore;
            }
        }
    }
}
