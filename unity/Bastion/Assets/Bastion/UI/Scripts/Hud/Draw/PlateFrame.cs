using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The big plates' steel frame and rivets (mixta.css: border-image mh/frame-a.webp 34 / 15px / 8px, and --m-rivets):
    /// USS has neither border-image nor its outset, so a child of the plate, first and out of the flow, draws the frame
    /// cut in nine (Strategy.uss .plate-frame: the texture's 34 px edge drawn 15 px wide, 8 px outside the plate) and the
    /// four rivets 6 px in from the plate's corners. Under the plate's content, over its steel. Rivets alone for the
    /// plates the 0.10.1 riveted without a frame (the partner's proposal).
    /// </summary>
    public sealed class PlateFrame : VisualElement
    {
        public const string ClassName = "plate-frame";
        /// <summary>How far the frame stands out of the plate (border-image-outset).</summary>
        const float Outset = 8;

        readonly float inset;

        PlateFrame(bool frame)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList(ClassName);
            if (!frame) AddToClassList(ClassName + "--rivets");
            inset = frame ? Outset : 0;
            generateVisualContent += Rivets;
        }

        /// <summary>`plate` framed (or only riveted), once: again after its children were cleared.</summary>
        public static void Dress(VisualElement plate, bool frame = true)
        {
            if (plate == null) return;
            foreach (var child in plate.Children())
                if (child is PlateFrame) return;
            plate.Insert(0, new PlateFrame(frame));
        }

        /// <summary>A rivet: a lit dome on a dark collar (--m-rivet: its centre at 42 % 38 % of a 7 px square, 2.9 px across).</summary>
        void Rivets(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 2 * inset + 30 || r.height < 2 * inset + 30) return;
            var p = ctx.painter2D;
            float near = inset + 6, farX = r.width - inset - 6 - 7, farY = r.height - inset - 6 - 7;
            foreach (var corner in new[] { new Vector2(near, near), new Vector2(farX, near), new Vector2(near, farY), new Vector2(farX, farY) })
            {
                var c = corner + new Vector2(2.94f, 2.66f);
                Dot(p, c, 2.6f, new Color32(0x23, 0x27, 0x2a, 0xff));
                Dot(p, c, 1.6f, new Color32(0x5b, 0x62, 0x66, 0xff));
                Dot(p, c, 0.85f, new Color32(0xa7, 0xae, 0xb2, 0xff));
            }
        }

        static void Dot(Painter2D p, Vector2 at, float radius, Color colour)
        {
            p.fillColor = colour;
            p.BeginPath();
            p.Arc(at, radius, 0, 360);
            p.ClosePath();
            p.Fill();
        }
    }

    /// <summary>
    /// A dashed edge (CSS border-style: dashed, which USS lacks): a child out of the flow that strokes 3 px dashes 2 px
    /// apart round its parent, 1 px wide, in `--dash-color` (Strategy.uss). The recruit's card has one (strategy.css).
    /// </summary>
    public sealed class DashedEdge : VisualElement
    {
        public const string ClassName = "dashed-edge";
        static readonly CustomStyleProperty<Color> DashColor = new CustomStyleProperty<Color>("--dash-color");
        Color colour = new Color32(52, 53, 54, 255);

        DashedEdge()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList(ClassName);
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (customStyle.TryGetValue(DashColor, out var c)) colour = c;
                MarkDirtyRepaint();
            });
        }

        public static void Dress(VisualElement box)
        {
            if (box == null) return;
            foreach (var child in box.Children())
                if (child is DashedEdge) return;
            box.Insert(0, new DashedEdge());
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 2 || r.height <= 2) return;
            var p = ctx.painter2D;
            p.fillColor = colour;
            // Each side's dashes as thin rectangles (crisp at 1 px, as the 0.10.1's), from its corner.
            for (var x = 0f; x < r.width; x += 5)
            {
                Box(p, x, 0, Mathf.Min(3, r.width - x), 1);
                Box(p, x, r.height - 1, Mathf.Min(3, r.width - x), 1);
            }
            for (var y = 0f; y < r.height; y += 5)
            {
                Box(p, 0, y, 1, Mathf.Min(3, r.height - y));
                Box(p, r.width - 1, y, 1, Mathf.Min(3, r.height - y));
            }
        }

        static void Box(Painter2D p, float x, float y, float w, float h)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x, y));
            p.LineTo(new Vector2(x + w, y));
            p.LineTo(new Vector2(x + w, y + h));
            p.LineTo(new Vector2(x, y + h));
            p.ClosePath();
            p.Fill();
        }
    }
}
