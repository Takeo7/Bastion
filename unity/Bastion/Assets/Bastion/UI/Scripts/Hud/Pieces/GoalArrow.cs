using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// Where the mission's goal lies, bottom right (UI-COMBATE UI-06): a dark steel ring with a yellow arrow in it, one
    /// head, turned every frame to where the goal is on the screen from its middle (the edge marks' way: EdgeMarks), so the
    /// camera's turns turn it too. No words, no distance; nothing happens on a click. The goal is the presenter's, as the
    /// O key's (the objective while it is to do, else the evacuation); none, no ring.
    /// </summary>
    public sealed class GoalArrow
    {
        readonly VisualElement host;
        readonly VisualElement arrow;
        Vector3? goal;

        public GoalArrow(VisualElement host)
        {
            this.host = host;
            host.pickingMode = PickingMode.Ignore;
            host.Add(new Ring());
            arrow = new Arrow();
            host.Add(arrow);
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
            arrow.style.rotate = new Rotate(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
        }

        /// <summary>The ring: dark steel, a lit rim and a sunk well, as the plates' steel (Mixta).</summary>
        sealed class Ring : VisualElement
        {
            public Ring()
            {
                AddToClassList("goal-arrow__ring");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                if (r.width <= 0 || r.height <= 0) return;
                var c = r.center;
                var radius = Mathf.Min(r.width, r.height) / 2;
                var p = ctx.painter2D;
                void Disc(float rad, Color colour)
                {
                    p.fillColor = colour;
                    p.BeginPath();
                    p.Arc(c, rad, 0, 360);
                    p.ClosePath();
                    p.Fill();
                }
                // The rim's edge, its steel lit from above, then the well.
                Disc(radius, new Color32(0x06, 0x07, 0x08, 0xff));
                Disc(radius - 1, new Color32(0x4a, 0x51, 0x55, 0xff));
                p.fillColor = new Color32(0x2b, 0x2f, 0x31, 0xff);
                p.BeginPath();
                p.Arc(c, radius - 1, 0, 180);
                p.ClosePath();
                p.Fill();
                Disc(radius - 6, new Color32(0x06, 0x07, 0x08, 0xff));
                Disc(radius - 7, new Color32(0x14, 0x17, 0x19, 0xff));
            }
        }

        /// <summary>The arrow: a shaft and a head, pointing right before it is turned, in the accent's yellow.</summary>
        sealed class Arrow : VisualElement
        {
            public Arrow()
            {
                AddToClassList("goal-arrow__arrow");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                if (r.width <= 0 || r.height <= 0) return;
                // Its 24 × 24 drawing fitted to the element, centred.
                var k = Mathf.Min(r.width, r.height) / 24;
                var o = new Vector2(r.x + (r.width - 24 * k) / 2, r.y + (r.height - 24 * k) / 2);
                Vector2 At(float x, float y) => o + new Vector2(x, y) * k;
                var p = ctx.painter2D;
                // A dark edge round it, then the yellow.
                foreach (var (fill, grow) in new[] { (new Color(0.02f, 0.02f, 0.02f, 0.85f), 1.2f), (resolvedStyle.color, 0f) })
                {
                    p.fillColor = fill;
                    p.BeginPath();
                    p.MoveTo(At(3 - grow, 10 - grow));
                    p.LineTo(At(13, 10 - grow));
                    p.LineTo(At(13, 4.5f - grow * 1.6f));
                    p.LineTo(At(21 + grow * 1.6f, 12));
                    p.LineTo(At(13, 19.5f + grow * 1.6f));
                    p.LineTo(At(13, 14 + grow));
                    p.LineTo(At(3 - grow, 14 + grow));
                    p.ClosePath();
                    p.Fill();
                }
            }
        }
    }
}
