using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The icons that were our own drawings on the web, not Synty's (icons.ts: 24 × 24, a line of 2, round ends): drawn
    /// with Painter2D in the element's tint (USS `-unity-background-image-tint-color`, as the sprites are tinted), so
    /// the bar's rules for its icons (muted when off, green when free) reach them too. The doors, the base's axe, and the
    /// Coro's wave.
    /// </summary>
    public sealed class LineIcon : VisualElement
    {
        static readonly Dictionary<string, Action<Pen>> Drawings = new Dictionary<string, Action<Pen>>
        {
            // The door open: its frame, the leaf swung in, the gap it leaves (dashed), the knob.
            ["openDoor"] = pen =>
            {
                pen.Line(3, 22, 21, 22);
                pen.Lines(true, false, (5.5f, 22), (5.5f, 2.5f), (18.5f, 2.5f), (18.5f, 22));
                pen.Lines(true, true, (5.5f, 2.5f), (12.5f, 4.9f), (12.5f, 22.6f), (5.5f, 21.4f));
                pen.Dashed(15.5f, 6.5f, 15.5f, 20.5f, 1.6f, 2);
                pen.Dot(10.4f, 13, 1.9f);
            },
            // The door shut: the leaf in its frame, two panels, the knob.
            ["closeDoor"] = pen =>
            {
                pen.Line(3, 22, 21, 22);
                pen.Rect(5.5f, 2.5f, 13, 19.5f);
                pen.Rect(8.5f, 5.5f, 5, 6);
                pen.Rect(8.5f, 14, 5, 5);
                pen.Dot(16, 12.8f, 1.9f);
            },
            // Talar's axe (strategy.css .fell-ico, a filled shape): the blade at the top right, the haft down to the left.
            ["axe"] = pen =>
            {
                pen.Begin(13.2f, 3.1f);
                pen.Curve(16.1f, 2.5f, 19.2f, 3.7f, 20.9f, 6.2f);
                pen.To(16.3f, 10.8f);
                pen.To(14.7f, 9.2f);
                pen.To(5.8f, 18.1f);
                pen.Round(4.63f, 16.97f, 1.6f, 45, 225);
                pen.To(12.4f, 6.9f);
                pen.To(10.8f, 5.3f);
                pen.Fill();
            },
            // The Coro's mark on the radio when it has no bust (transmissions.ts portrait: «∿», which none of our fonts
            // has): one period of a sine wave, up then down.
            ["wave"] = pen =>
            {
                pen.Begin(3, 12);
                pen.Curve(6.5f, 6, 8.5f, 6, 12, 12);
                pen.Curve(15.5f, 18, 17.5f, 18, 21, 12);
                pen.Stroke();
            },
        };

        /// <summary>Whether there is a drawing for `id`.</summary>
        public static bool Has(string id) => id != null && Drawings.ContainsKey(id);

        readonly Action<Pen> draw;

        public LineIcon(string id)
        {
            draw = Drawings.TryGetValue(id ?? "", out var d) ? d : null;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (draw == null || r.width <= 0 || r.height <= 0) return;
            // The 24 × 24 drawing fitted to the element, centred.
            var k = Mathf.Min(r.width, r.height) / 24;
            var o = new Vector2(r.x + (r.width - 24 * k) / 2, r.y + (r.height - 24 * k) / 2);
            var colour = parent != null ? parent.resolvedStyle.unityBackgroundImageTintColor : resolvedStyle.color;
            var p = ctx.painter2D;
            p.strokeColor = colour;
            p.fillColor = colour;
            p.lineWidth = 2 * k;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            draw(new Pen(p, o, k));
        }

        /// <summary>Painter2D in the drawing's 24 × 24 units.</summary>
        readonly struct Pen
        {
            readonly Painter2D p;
            readonly Vector2 o;
            readonly float k;

            public Pen(Painter2D p, Vector2 o, float k)
            {
                this.p = p;
                this.o = o;
                this.k = k;
            }

            Vector2 At(float x, float y) => o + new Vector2(x, y) * k;

            public void Line(float x0, float y0, float x1, float y1)
            {
                p.BeginPath();
                p.MoveTo(At(x0, y0));
                p.LineTo(At(x1, y1));
                p.Stroke();
            }

            public void Lines(bool stroke, bool closed, params (float x, float y)[] points)
            {
                p.BeginPath();
                p.MoveTo(At(points[0].x, points[0].y));
                for (var i = 1; i < points.Length; i++) p.LineTo(At(points[i].x, points[i].y));
                if (closed) p.ClosePath();
                if (stroke) p.Stroke();
            }

            public void Rect(float x, float y, float w, float h) => Lines(true, true, (x, y), (x + w, y), (x + w, y + h), (x, y + h));

            /// <summary>A line in dashes `on` long, `off` apart (SVG stroke-dasharray).</summary>
            public void Dashed(float x0, float y0, float x1, float y1, float on, float off)
            {
                var from = new Vector2(x0, y0);
                var to = new Vector2(x1, y1);
                var length = Vector2.Distance(from, to);
                var dir = (to - from) / length;
                for (var d = 0f; d < length; d += on + off)
                {
                    var a = from + dir * d;
                    var b = from + dir * Mathf.Min(length, d + on);
                    Line(a.x, a.y, b.x, b.y);
                }
            }

            /// <summary>A filled shape, a point at a time: Begin, To, Curve, Round, then Fill.</summary>
            public void Begin(float x, float y)
            {
                p.BeginPath();
                p.MoveTo(At(x, y));
            }

            public void To(float x, float y) => p.LineTo(At(x, y));

            public void Curve(float x1, float y1, float x2, float y2, float x, float y) => p.BezierCurveTo(At(x1, y1), At(x2, y2), At(x, y));

            /// <summary>An arc round (cx, cy), clockwise on screen from one angle to the other (degrees, 0 to the right).</summary>
            public void Round(float cx, float cy, float radius, float from, float to) => p.Arc(At(cx, cy), radius * k, from, to);

            public void Fill()
            {
                p.ClosePath();
                p.Fill();
            }

            /// <summary>The same path, open and drawn as a line.</summary>
            public void Stroke() => p.Stroke();

            /// <summary>A filled dot (the SVG's small circle, filled and stroked: its radius plus half the line).</summary>
            public void Dot(float x, float y, float radius)
            {
                p.BeginPath();
                p.Arc(At(x, y), radius * k, 0, 360);
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
