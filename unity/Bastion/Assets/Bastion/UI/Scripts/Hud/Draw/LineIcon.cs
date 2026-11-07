using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The icons that were our own drawings on the web, not Synty's (icons.ts: 24 × 24, a line of 2, round ends): drawn
    /// with Painter2D in the element's tint (USS `-unity-background-image-tint-color`, as the sprites are tinted), so
    /// the bar's rules for its icons (muted when off, green when free) reach them too. The doors, the base's axe, the
    /// missions' places and their weather (new in Unity: Synty has neither), «Terminar turno», the molotov, the movement's
    /// footprints and a move's noise (Ana, 7/11 #3, #7: Synty's INTERFACE packs have neither).
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
            // A mission's place on its card (Places, Arturo 31/10 #9), each on the ground line. The city: three blocks, the
            // middle one tallest, lit.
            ["place:city"] = pen =>
            {
                pen.Line(2, 21.5f, 22, 21.5f);
                pen.Lines(true, false, (4, 21.5f), (4, 10), (9, 10));
                pen.Lines(true, false, (9, 21.5f), (9, 4), (15, 4), (15, 21.5f));
                pen.Lines(true, false, (15, 13), (20, 13), (20, 21.5f));
                pen.Dot(12, 8.5f, 1);
                pen.Dot(12, 13, 1);
            },
            // The town: a house and its church's steeple with the cross.
            ["place:town"] = pen =>
            {
                pen.Line(2, 21.5f, 22, 21.5f);
                pen.Lines(true, false, (4, 21.5f), (4, 13.5f), (9, 9), (14, 13.5f), (14, 21.5f));
                pen.Lines(true, false, (8, 21.5f), (8, 17.5f), (10, 17.5f), (10, 21.5f));
                pen.Lines(true, false, (14, 11), (17, 6.5f), (20, 11), (20, 21.5f));
                pen.Line(17, 2, 17, 6.5f);
                pen.Line(15.6f, 3.6f, 18.4f, 3.6f);
            },
            // The estate: a shed with a sawtooth roof and its roller door.
            ["place:estate"] = pen =>
            {
                pen.Line(2, 21.5f, 22, 21.5f);
                pen.Lines(true, false, (3, 21.5f), (3, 12), (9, 7), (9, 12), (15, 7), (15, 12), (21, 7), (21, 21.5f));
                pen.Lines(true, false, (9, 21.5f), (9, 15.5f), (15, 15.5f), (15, 21.5f));
                pen.Line(9, 18.5f, 15, 18.5f);
            },
            // The factory: a flat block with its door, its tall chimney and the smoke off it.
            ["place:factory"] = pen =>
            {
                pen.Line(2, 21.5f, 22, 21.5f);
                pen.Lines(true, false, (3, 21.5f), (3, 12.5f), (14, 12.5f), (14, 21.5f));
                pen.Lines(true, false, (7, 21.5f), (7, 17), (10, 17), (10, 21.5f));
                pen.Lines(true, false, (16, 21.5f), (16, 7), (19.5f, 7), (19.5f, 21.5f));
                pen.Begin(17.7f, 4.2f);
                pen.Curve(18.6f, 2, 20.6f, 1.8f, 21.8f, 3.2f);
                pen.Stroke();
            },
            // The service area: a fuel pump, its window and its hose.
            ["place:roadside"] = pen =>
            {
                pen.Line(2, 21.5f, 22, 21.5f);
                pen.Lines(true, false, (5, 21.5f), (5, 4), (14, 4), (14, 21.5f));
                pen.Rect(7.5f, 7, 4, 4);
                pen.Lines(true, false, (14, 8.5f), (16.5f, 8.5f), (18.5f, 10.5f), (18.5f, 17));
                pen.Line(18.5f, 17, 17, 18.5f);
            },
            // The country: a barn, its roof broken twice, its door crossed.
            ["place:country"] = pen =>
            {
                pen.Line(2, 21.5f, 22, 21.5f);
                pen.Lines(true, false, (4, 21.5f), (4, 11.5f), (7.5f, 6.5f), (16.5f, 6.5f), (20, 11.5f), (20, 21.5f));
                pen.Lines(true, false, (9, 21.5f), (9, 14.5f), (15, 14.5f), (15, 21.5f));
                pen.Line(9, 14.5f, 15, 21.5f);
                pen.Line(15, 14.5f, 9, 21.5f);
            },
            // The weather (Arturo, 01/11), each its cloud and what falls from it.
            ["weather:drizzle"] = pen =>
            {
                Cloud(pen);
                pen.Line(8, 17, 7.3f, 19);
                pen.Line(12.5f, 17, 11.8f, 19);
                pen.Line(17, 17, 16.3f, 19);
                pen.Line(10.2f, 20.8f, 9.5f, 22.8f);
                pen.Line(14.7f, 20.8f, 14, 22.8f);
            },
            ["weather:downpour"] = pen =>
            {
                Cloud(pen);
                pen.Line(7.5f, 16.8f, 5.8f, 22.2f);
                pen.Line(11.5f, 16.8f, 9.8f, 22.2f);
                pen.Line(15.5f, 16.8f, 13.8f, 22.2f);
                pen.Line(19.5f, 16.8f, 17.8f, 22.2f);
            },
            ["weather:storm"] = pen =>
            {
                Cloud(pen);
                pen.Begin(13, 15.2f);
                pen.To(9.6f, 19.6f);
                pen.To(12.2f, 19.6f);
                pen.To(10.6f, 23.2f);
                pen.To(15.4f, 17.8f);
                pen.To(12.8f, 17.8f);
                pen.To(14.6f, 15.2f);
                pen.Fill();
                pen.Line(7, 16.8f, 6, 19.8f);
                pen.Line(18.6f, 16.8f, 17.6f, 19.8f);
            },
            ["weather:snowfall"] = pen =>
            {
                Cloud(pen);
                pen.Dot(8, 17.8f, 1.15f);
                pen.Dot(12.5f, 18.4f, 1.15f);
                pen.Dot(17, 17.8f, 1.15f);
                pen.Dot(10.2f, 21.8f, 1.15f);
                pen.Dot(14.8f, 21.8f, 1.15f);
            },
            // Snow on the wind: flakes beside two gusts.
            ["weather:blizzard"] = pen =>
            {
                Cloud(pen);
                pen.Line(4, 18.2f, 13.5f, 18.2f);
                pen.Dot(17, 18.2f, 1.15f);
                pen.Line(9, 21.8f, 19.5f, 21.8f);
                pen.Dot(5.5f, 21.8f, 1.15f);
            },
            // The molotov on the bar (Arturo, 01/11 #10; Synty's icons have none): a bottle, round-shouldered, its rag lit at the
            // neck, filled like the icons beside it.
            ["molotov"] = pen =>
            {
                pen.Begin(8.5f, 22);
                pen.To(8.5f, 13.5f);
                pen.Curve(8.5f, 11.6f, 10.4f, 10.8f, 10.8f, 9.6f);
                pen.To(10.8f, 7.2f);
                pen.To(13.2f, 7.2f);
                pen.To(13.2f, 9.6f);
                pen.Curve(13.6f, 10.8f, 15.5f, 11.6f, 15.5f, 13.5f);
                pen.To(15.5f, 22);
                pen.Fill();
                pen.Begin(12, 6.2f);
                pen.Curve(9.4f, 5.4f, 9.6f, 2.6f, 12.6f, 1);
                pen.Curve(12.1f, 2.9f, 15, 3.2f, 14.2f, 5.4f);
                pen.Curve(13.8f, 6, 12.9f, 6.3f, 12, 6.2f);
                pen.Fill();
            },
            // The movement left (Ana, 7/11 #7): two footprints, a sole and its heel each, the left one ahead.
            ["footprint"] = pen =>
            {
                Ellipse(pen, 8, 7, 3.2f, 4.6f);
                Ellipse(pen, 8.4f, 14.6f, 2.4f, 2.2f);
                Ellipse(pen, 16, 11.6f, 3.2f, 4.6f);
                Ellipse(pen, 15.6f, 19.2f, 2.4f, 2.2f);
            },
            // A move that is heard, on its way (Ana, 7/11 #3): a loudspeaker and two waves.
            ["noise"] = pen =>
            {
                pen.Begin(2.5f, 9);
                pen.To(7, 9);
                pen.To(12.5f, 4);
                pen.To(12.5f, 20);
                pen.To(7, 15);
                pen.To(2.5f, 15);
                pen.Fill();
                pen.Begin(16.04f, 8.82f);
                pen.Round(12.5f, 12, 4.5f, -45, 45);
                pen.Stroke();
                pen.Begin(18.21f, 6.18f);
                pen.Round(12.5f, 12, 8.2f, -45, 45);
                pen.Stroke();
            },
            // «Terminar turno» on the bar (UI-COMBATE UI-04): an arrow against a bar, filled like Synty's icons beside it.
            ["endTurn"] = pen =>
            {
                pen.Begin(2.5f, 10);
                pen.To(11, 10);
                pen.To(11, 5);
                pen.To(18, 12);
                pen.To(11, 19);
                pen.To(11, 14);
                pen.To(2.5f, 14);
                pen.Fill();
                pen.Begin(19.5f, 3.5f);
                pen.To(22, 3.5f);
                pen.To(22, 20.5f);
                pen.To(19.5f, 20.5f);
                pen.Fill();
            },
        };

        /// <summary>A filled ellipse round (cx, cy), in four curves.</summary>
        static void Ellipse(Pen pen, float cx, float cy, float rx, float ry)
        {
            const float c = 0.5523f;
            pen.Begin(cx + rx, cy);
            pen.Curve(cx + rx, cy + ry * c, cx + rx * c, cy + ry, cx, cy + ry);
            pen.Curve(cx - rx * c, cy + ry, cx - rx, cy + ry * c, cx - rx, cy);
            pen.Curve(cx - rx, cy - ry * c, cx - rx * c, cy - ry, cx, cy - ry);
            pen.Curve(cx + rx * c, cy - ry, cx + rx, cy - ry * c, cx + rx, cy);
            pen.Fill();
        }

        /// <summary>The weather's cloud, its foot on 14.5: three bumps, the middle one highest.</summary>
        static void Cloud(Pen pen)
        {
            pen.Begin(6.5f, 14.5f);
            pen.Round(6.8f, 11.2f, 3.3f, 95, 265);
            pen.Round(12.5f, 8.3f, 4.5f, 200, 345);
            pen.Round(17.6f, 11, 3.5f, 255, 450);
            pen.To(6.5f, 14.5f);
            pen.Stroke();
        }

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
