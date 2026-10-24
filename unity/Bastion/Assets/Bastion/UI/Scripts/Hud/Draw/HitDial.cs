using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The odds' dial on the aim's panel (mixta-hud.css: an arc round the number, --p of 100): a dark ring and, over it,
    /// the arc of the odds from the top, clockwise, in the accent. The number itself is a Label inside it (ShotPanel).
    /// </summary>
    public sealed class HitDial : VisualElement
    {
        static readonly Color Ring = new Color(0, 0, 0, 0.45f);
        static readonly Color Arc = new Color32(0xf2, 0xc2, 0x30, 0xff);

        float odds;

        public HitDial()
        {
            AddToClassList("hit-dial");
            generateVisualContent += Draw;
        }

        /// <summary>The odds, 0–100.</summary>
        public void Set(int percent)
        {
            var v = Mathf.Clamp01(percent / 100f);
            if (Mathf.Approximately(v, odds)) return;
            odds = v;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            var width = 4f;
            var radius = Mathf.Min(r.width, r.height) / 2 - width / 2;
            if (radius <= 0) return;
            var centre = r.center;
            var p = ctx.painter2D;
            p.lineWidth = width;
            p.strokeColor = Ring;
            p.BeginPath();
            p.Arc(centre, radius, new Angle(0, AngleUnit.Degree), new Angle(360, AngleUnit.Degree));
            p.Stroke();
            if (odds <= 0) return;
            // From the top (−90° in panel space, y down), clockwise.
            p.strokeColor = Arc;
            p.lineCap = LineCap.Butt;
            p.BeginPath();
            p.Arc(centre, radius, new Angle(-90, AngleUnit.Degree), new Angle(-90 + 360 * odds, AngleUnit.Degree), ArcDirection.Clockwise);
            p.Stroke();
        }
    }

    /// <summary>The compass's needle (#34): a small triangle from the middle towards the north, in the accent.</summary>
    public sealed class CompassNeedle : VisualElement
    {
        static readonly Color Accent = new Color32(0xf2, 0xc2, 0x30, 0xff);

        public CompassNeedle()
        {
            AddToClassList("compass-needle");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += ctx =>
            {
                var r = contentRect;
                var p = ctx.painter2D;
                p.fillColor = Accent;
                p.BeginPath();
                p.MoveTo(new Vector2(r.center.x, r.yMin));
                p.LineTo(new Vector2(r.xMax, r.yMax));
                p.LineTo(new Vector2(r.xMin, r.yMax));
                p.ClosePath();
                p.Fill();
            };
        }
    }
}
