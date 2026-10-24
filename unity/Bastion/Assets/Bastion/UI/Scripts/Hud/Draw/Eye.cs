using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// An eye, in the element's text colour (USS `color`): on the flag of an enemy that sees one of the squad (styles.css
    /// .uflag.watching, playtest 21/10 #11). The web's mask drawn with Painter2D: an almond, its iris hollow, a pupil.
    /// </summary>
    public sealed class Eye : VisualElement
    {
        public Eye()
        {
            AddToClassList("eye");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            // The web's 16 × 10 drawing, scaled to the element.
            var sx = r.width / 16;
            var sy = r.height / 10;
            Vector2 P(float x, float y) => new Vector2(r.x + x * sx, r.y + y * sy);
            var p = ctx.painter2D;
            p.fillColor = resolvedStyle.color;
            p.BeginPath();
            p.MoveTo(P(0, 5));
            p.BezierCurveTo(P(1.4f, 2.1f), P(4.4f, 0), P(8, 0));
            p.BezierCurveTo(P(11.6f, 0), P(14.6f, 2.1f), P(16, 5));
            p.BezierCurveTo(P(14.6f, 7.9f), P(11.6f, 10), P(8, 10));
            p.BezierCurveTo(P(4.4f, 10), P(1.4f, 7.9f), P(0, 5));
            p.ClosePath();
            // The iris's ring left out, the pupil in again (even-odd).
            p.MoveTo(P(8 + 3.2f, 5));
            p.Arc(P(8, 5), 3.2f * sy, 0, 360);
            p.ClosePath();
            p.MoveTo(P(8 + 1.6f, 5));
            p.Arc(P(8, 5), 1.6f * sy, 0, 360);
            p.ClosePath();
            p.Fill(FillRule.OddEven);
        }
    }
}
