using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// What to press next, cut at the ends like the packs' buttons (mixta.css .x-btn.primary: clip-path polygon(9px 0,
    /// 100% 0, calc(100% - 9px) 100%, 0 100%)). USS has no clip-path nor skew: the button's own background goes clear
    /// (Strategy.uss .btn-slant) and this paints the slanted plate under its text, in the colours the sheet gives it as
    /// custom properties, so hover, disabled and «a punto» stay in USS:
    ///   --slant-fill   the plate at 46 % of its height, --slant-hi at its top and --slant-lo at its foot (the 0.10.1's
    ///                  linear-gradient(180deg, hi 0%, fill 46%, lo 100%));
    ///   --slant-top    its lit top line (inset 0 1px 0);
    ///   --slant-foot   its shaded foot (inset 0 -3px 0);
    ///   --slant-edge   an outline round it (the partner waiting), clear for none.
    /// Only while the button has `btn-primary`: without it, it is the plain steel key it was.
    /// </summary>
    public static class Slant
    {
        public const string ClassName = "btn-slant";
        /// <summary>--t-cut: how far each end is cut in.</summary>
        const float Cut = 9;

        static readonly CustomStyleProperty<Color> Fill = new CustomStyleProperty<Color>("--slant-fill");
        static readonly CustomStyleProperty<Color> Hi = new CustomStyleProperty<Color>("--slant-hi");
        static readonly CustomStyleProperty<Color> Lo = new CustomStyleProperty<Color>("--slant-lo");
        static readonly CustomStyleProperty<Color> Top = new CustomStyleProperty<Color>("--slant-top");
        static readonly CustomStyleProperty<Color> Foot = new CustomStyleProperty<Color>("--slant-foot");
        static readonly CustomStyleProperty<Color> Edge = new CustomStyleProperty<Color>("--slant-edge");

        /// <summary>`b` painted slanted from now on (once).</summary>
        public static void Dress(Button b)
        {
            if (b == null || b.ClassListContains(ClassName)) return;
            b.AddToClassList(ClassName);
            // Under the text: the plate first, then what the button already drew (its label).
            var before = b.generateVisualContent;
            b.generateVisualContent = Paint;
            b.generateVisualContent += before;
            b.RegisterCallback<CustomStyleResolvedEvent>(e => b.MarkDirtyRepaint());
        }

        static void Paint(MeshGenerationContext ctx)
        {
            var e = ctx.visualElement;
            if (!e.ClassListContains("btn-primary")) return;
            var w = e.layout.width;
            var h = e.layout.height;
            if (w <= 2 * Cut || h <= 0) return;
            if (!e.customStyle.TryGetValue(Fill, out var fill)) return;
            var hi = e.customStyle.TryGetValue(Hi, out var a) ? a : fill;
            var lo = e.customStyle.TryGetValue(Lo, out var b) ? b : fill;
            var top = e.customStyle.TryGetValue(Top, out var t) ? t : hi;
            var foot = e.customStyle.TryGetValue(Foot, out var f) ? f : lo;
            var p = ctx.painter2D;
            // The plate's gradient (Painter2D fills one colour): a strip every 2 px from there to the foot, each over
            // the last, so their edges blend into each other; then its lit top line and its shaded foot.
            var strips = Mathf.Max(2, Mathf.CeilToInt(h / 2));
            for (var i = 0; i < strips; i++)
            {
                var y = h * i / strips;
                var k = (y + h / strips / 2) / h;
                Band(p, w, h, y, h, k < 0.46f ? Color.Lerp(hi, fill, k / 0.46f) : Color.Lerp(fill, lo, (k - 0.46f) / 0.54f));
            }
            Band(p, w, h, 0, 1, top);
            if (foot != lo) Band(p, w, h, h - 3, h, foot);
            if (e.customStyle.TryGetValue(Edge, out var edge) && edge.a > 0)
            {
                p.strokeColor = edge;
                p.lineWidth = 2;
                p.BeginPath();
                p.MoveTo(new Vector2(Cut + 1, 1));
                p.LineTo(new Vector2(w - 1, 1));
                p.LineTo(new Vector2(w - Cut - 1, h - 1));
                p.LineTo(new Vector2(1, h - 1));
                p.ClosePath();
                p.Stroke();
            }
        }

        /// <summary>The slanted plate between heights y0 and y1: its left edge runs from (Cut, 0) down to (0, h).</summary>
        static void Band(Painter2D p, float w, float h, float y0, float y1, Color colour)
        {
            float Left(float y) => Cut * (1 - y / h);
            p.fillColor = colour;
            p.BeginPath();
            p.MoveTo(new Vector2(Left(y0), y0));
            p.LineTo(new Vector2(w - Cut + Left(y0), y0));
            p.LineTo(new Vector2(w - Cut + Left(y1), y1));
            p.LineTo(new Vector2(Left(y1), y1));
            p.ClosePath();
            p.Fill();
        }
    }
}
