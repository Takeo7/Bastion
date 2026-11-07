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
    /// Only while the button has `btn-primary`: without it, it is the plain steel key it was. With `btn-notched` (Arturo's
    /// report of 7/11, the maqueta v39's «LANZAR MISIÓN»): not slanted but a rectangle with a few notches torn in its ends,
    /// in the same colours.
    /// </summary>
    public static class Slant
    {
        public const string ClassName = "btn-slant";
        public const string Notched = "btn-notched";
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
            var notched = e.ClassListContains(Notched);
            // Where the plate's left and right edges are at a height: slanted, or notched.
            System.Func<float, float> left = notched ? y => Notch(LeftNotches, y / h) : y => Cut * (1 - y / h);
            System.Func<float, float> right = notched ? y => w - Notch(RightNotches, y / h) : y => w - Cut + Cut * (1 - y / h);
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
                Band(p, left, right, y, h, k < 0.46f ? Color.Lerp(hi, fill, k / 0.46f) : Color.Lerp(fill, lo, (k - 0.46f) / 0.54f));
            }
            Band(p, left, right, 0, 1, top);
            if (foot != lo) Band(p, left, right, h - 3, h, foot);
            if (e.customStyle.TryGetValue(Edge, out var edge) && edge.a > 0)
            {
                p.strokeColor = edge;
                p.lineWidth = 2;
                p.BeginPath();
                if (notched)
                {
                    // Down its right edge and up its left, through each notch.
                    p.MoveTo(new Vector2(left(1) + 1, 1));
                    foreach (var (k, _) in RightNotches) p.LineTo(new Vector2(right(Mathf.Clamp(k * h, 1, h - 1)) - 1, Mathf.Clamp(k * h, 1, h - 1)));
                    for (var i = LeftNotches.Length - 1; i >= 0; i--) p.LineTo(new Vector2(left(Mathf.Clamp(LeftNotches[i].at * h, 1, h - 1)) + 1, Mathf.Clamp(LeftNotches[i].at * h, 1, h - 1)));
                }
                else
                {
                    p.MoveTo(new Vector2(Cut + 1, 1));
                    p.LineTo(new Vector2(w - 1, 1));
                    p.LineTo(new Vector2(w - Cut - 1, h - 1));
                    p.LineTo(new Vector2(1, h - 1));
                }
                p.ClosePath();
                p.Stroke();
            }
        }

        /// <summary>The plate between heights y0 and y1, its edges where `left` and `right` say.</summary>
        static void Band(Painter2D p, System.Func<float, float> left, System.Func<float, float> right, float y0, float y1, Color colour)
        {
            p.fillColor = colour;
            p.BeginPath();
            p.MoveTo(new Vector2(left(y0), y0));
            p.LineTo(new Vector2(right(y0), y0));
            p.LineTo(new Vector2(right(y1), y1));
            p.LineTo(new Vector2(left(y1), y1));
            p.ClosePath();
            p.Fill();
        }

        /// <summary>
        /// A notched end (btn-notched): how far in the edge is (px) at each share of the height, straight between them; the
        /// maqueta's, a notch near the top and the foot on the left, one low on the right.
        /// </summary>
        static readonly (float at, float inset)[] LeftNotches = { (0f, 2f), (0.1f, 0f), (0.3f, 0f), (0.37f, 2.5f), (0.44f, 0f), (0.78f, 0f), (0.88f, 2f), (1f, 1f) };
        static readonly (float at, float inset)[] RightNotches = { (0f, 0f), (0.5f, 0f), (0.58f, 2f), (0.66f, 0f), (0.82f, 0f), (0.9f, 2.5f), (1f, 0f) };

        static float Notch((float at, float inset)[] notches, float share)
        {
            for (var i = 1; i < notches.Length; i++)
                if (share <= notches[i].at)
                {
                    var (a, ia) = notches[i - 1];
                    var (b, ib) = notches[i];
                    return Mathf.Lerp(ia, ib, b > a ? (share - a) / (b - a) : 0);
                }
            return notches[notches.Length - 1].inset;
        }
    }
}
