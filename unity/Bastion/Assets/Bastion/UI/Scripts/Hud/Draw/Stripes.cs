using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// Thin diagonal stripes over the whole element, rising to the right, in its text colour (USS `color`): the bar's keys
    /// that are off (mixta-hud.css .ability:disabled: repeating-linear-gradient(-45deg, colour 0 4px, transparent 4px
    /// 8px)). USS has no gradients: each stripe is a band cut to the element's rectangle and filled.
    /// </summary>
    public sealed class Stripes : VisualElement
    {
        /// <summary>A stripe's width and the step from one to the next, across them.</summary>
        const float On = 4, Period = 8;

        static readonly float Root2 = Mathf.Sqrt(2);

        public Stripes()
        {
            AddToClassList("stripes");
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            var p = ctx.painter2D;
            p.fillColor = resolvedStyle.color;
            // Measured across the stripes from the bottom right corner, where the web's gradient starts: a stripe is
            // where that distance is in [n·Period, n·Period + On), i.e. x + y between two values.
            var far = r.xMax + r.yMax;
            var span = (r.width + r.height) / Root2;
            for (var d = 0f; d < span; d += Period)
            {
                var band = Cut(r, far - (d + On) * Root2, far - d * Root2);
                if (band.Count < 3) continue;
                p.BeginPath();
                p.MoveTo(band[0]);
                for (var i = 1; i < band.Count; i++) p.LineTo(band[i]);
                p.ClosePath();
                p.Fill();
            }
        }

        /// <summary>The part of `r` where lo ≤ x + y ≤ hi, as a polygon.</summary>
        static List<Vector2> Cut(Rect r, float lo, float hi)
        {
            var poly = new List<Vector2> { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
            poly = Keep(poly, v => v.x + v.y - lo);
            return Keep(poly, v => hi - v.x - v.y);
        }

        /// <summary>The polygon where `side` ≥ 0 (one pass of Sutherland–Hodgman).</summary>
        static List<Vector2> Keep(List<Vector2> poly, System.Func<Vector2, float> side)
        {
            var kept = new List<Vector2>();
            for (var i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                var sa = side(a);
                var sb = side(b);
                if (sa >= 0) kept.Add(a);
                if (sa >= 0 != sb >= 0) kept.Add(a + (b - a) * (sa / (sa - sb)));
            }
            return kept;
        }
    }
}
