using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// A filled triangle pointing up or down, in the element's text colour (USS `color`): the floor's buttons and the log's
    /// fold, as ▲ ▼ ▴ ▾ were on the web (Oswald and Barlow lack them; a sprite arrow read as a nail, Gameplay 23/10).
    /// </summary>
    public sealed class Triangle : VisualElement
    {
        bool up;

        public Triangle(bool up)
        {
            this.up = up;
            AddToClassList("triangle");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public bool Up
        {
            get => up;
            set
            {
                if (up == value) return;
                up = value;
                MarkDirtyRepaint();
            }
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            var p = ctx.painter2D;
            p.fillColor = resolvedStyle.color;
            p.BeginPath();
            if (up)
            {
                p.MoveTo(new Vector2(r.center.x, r.yMin));
                p.LineTo(new Vector2(r.xMax, r.yMax));
                p.LineTo(new Vector2(r.xMin, r.yMax));
            }
            else
            {
                p.MoveTo(new Vector2(r.xMin, r.yMin));
                p.LineTo(new Vector2(r.xMax, r.yMin));
                p.LineTo(new Vector2(r.center.x, r.yMax));
            }
            p.ClosePath();
            p.Fill();
        }
    }
}
