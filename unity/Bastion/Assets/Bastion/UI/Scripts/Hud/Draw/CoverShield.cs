using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// A cover's shield on the soldier's panel (Arturo, 7/11: «el escudito correspondiente, del color correspondiente»):
    /// the shield the move shows on each side with cover (MoveOverlay.ShieldTexture: its outline), in the colour the
    /// flag's square had: full, filled in blue; half, its lower part in yellow; flanked, empty, ringed in red.
    /// </summary>
    public sealed class CoverShield : VisualElement
    {
        /// <summary>Flanked, half, full: the flag's colours (Flags.uss .uflag__cover--0, 1, 2).</summary>
        static readonly Color[] Colours = { new Color32(0xf0, 0x50, 0x3c, 0xff), new Color32(0xff, 0xcf, 0x5a, 0xff), new Color32(0x7f, 0xd3, 0xff, 0xff) };
        static readonly Color Dark = new Color(10 / 255f, 20 / 255f, 30 / 255f, 0.75f);

        /// <summary>Its outline in MoveOverlay's 64 square: a point at the top, the sides, two curves to the tip.</summary>
        static readonly List<Vector2> Shape = MakeShape();

        /// <summary>Where a half shield's colour starts, in the 64 square (MoveOverlay: 34).</summary>
        const float Half = 34;

        int level = -1;

        public CoverShield()
        {
            AddToClassList("cover-shield");
            generateVisualContent += Paint;
        }

        /// <summary>2 full, 1 half, 0 flanked.</summary>
        public int Level
        {
            get => level;
            set
            {
                level = value;
                MarkDirtyRepaint();
            }
        }

        void Paint(MeshGenerationContext ctx)
        {
            if (level < 0) return;
            var size = contentRect.size;
            if (size.x <= 0 || size.y <= 0) return;
            var k = Mathf.Min(size.x, size.y) / 64f;
            var at = new Vector2((size.x - 64 * k) / 2, (size.y - 64 * k) / 2);
            Vector2 P(Vector2 q) => at + q * k;
            var colour = Colours[Mathf.Clamp(level, 0, 2)];
            var p = ctx.painter2D;
            Fill(p, Shape, P, Dark);
            if (level == 2) Fill(p, Shape, P, colour);
            else if (level == 1) Fill(p, Below(Shape, Half), P, colour);
            p.strokeColor = level == 0 ? colour : Color.Lerp(colour, Color.white, 0.5f);
            p.lineWidth = Mathf.Max(1, 3 * k);
            p.BeginPath();
            p.MoveTo(P(Shape[0]));
            for (var i = 1; i < Shape.Count; i++) p.LineTo(P(Shape[i]));
            p.ClosePath();
            p.Stroke();
        }

        static void Fill(Painter2D p, List<Vector2> shape, System.Func<Vector2, Vector2> at, Color colour)
        {
            if (shape.Count < 3) return;
            p.fillColor = colour;
            p.BeginPath();
            p.MoveTo(at(shape[0]));
            for (var i = 1; i < shape.Count; i++) p.LineTo(at(shape[i]));
            p.ClosePath();
            p.Fill();
        }

        /// <summary>The part of `shape` under the line y = `y` (down the canvas): clipped there.</summary>
        static List<Vector2> Below(List<Vector2> shape, float y)
        {
            var o = new List<Vector2>();
            for (var i = 0; i < shape.Count; i++)
            {
                var a = shape[i];
                var b = shape[(i + 1) % shape.Count];
                var ain = a.y >= y;
                var bin = b.y >= y;
                if (ain) o.Add(a);
                if (ain != bin) o.Add(Vector2.Lerp(a, b, (y - a.y) / (b.y - a.y)));
            }
            return o;
        }

        static List<Vector2> MakeShape()
        {
            var shape = new List<Vector2> { new Vector2(32, 4), new Vector2(56, 13), new Vector2(53, 38) };
            for (var k = 1; k <= 8; k++) shape.Add(Bezier(new Vector2(53, 38), new Vector2(46, 54), new Vector2(32, 60), k / 8f));
            for (var k = 1; k <= 8; k++) shape.Add(Bezier(new Vector2(32, 60), new Vector2(18, 54), new Vector2(11, 38), k / 8f));
            shape.Add(new Vector2(8, 13));
            return shape;
        }

        static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t) => (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
    }
}
