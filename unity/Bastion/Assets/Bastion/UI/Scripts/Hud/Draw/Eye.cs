using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// An eye, in the element's text colour (USS `color`): on the flag of an enemy that sees one of the squad (styles.css
    /// .uflag.watching, playtest 21/10 #11). Only its outline and the pupil, small and not red (Ana, Arturo 29/10: the
    /// web's filled red one was too loud); the web's almond, drawn with Painter2D.
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
            var line = Mathf.Max(1, sy * 1.3f);
            // The almond's outline, inside the box by half the line.
            Vector2 Q(float x, float y) => P(x, Mathf.Lerp(line / 2 / sy, 10 - line / 2 / sy, y / 10));
            p.strokeColor = resolvedStyle.color;
            p.lineWidth = line;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(Q(0.6f, 5));
            p.BezierCurveTo(Q(2, 2.1f), Q(4.6f, 0), Q(8, 0));
            p.BezierCurveTo(Q(11.4f, 0), Q(14, 2.1f), Q(15.4f, 5));
            p.BezierCurveTo(Q(14, 7.9f), Q(11.4f, 10), Q(8, 10));
            p.BezierCurveTo(Q(4.6f, 10), Q(2, 7.9f), Q(0.6f, 5));
            p.ClosePath();
            p.Stroke();
            // The pupil.
            p.fillColor = resolvedStyle.color;
            p.BeginPath();
            p.Arc(P(8, 5), 2.1f * sy, 0, 360);
            p.Fill();
        }
    }
}
