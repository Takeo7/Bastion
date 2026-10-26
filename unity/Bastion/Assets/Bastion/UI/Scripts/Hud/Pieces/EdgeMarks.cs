using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// Arrows at the edge of the screen pointing at the marks that are off it (edgeMarks.ts, UX-REVISION-2 #33): the
    /// objective, the evacuation, a siege's breaches, where the partner just made something happen; with the distance in
    /// tiles from the picked soldier. The red ones (an alert) beat. A click centres the camera on it (Look). On screen, a
    /// mark needs no arrow: its beam shows.
    /// </summary>
    public sealed class EdgeMarks
    {
        /// <summary>The room the arrows keep clear of (panel pixels): the turn strip above, the sheet and the bar below.</summary>
        const float Top = 72;
        const float Right = 36;
        const float Bottom = 120;
        const float Left = 36;

        sealed class Shown
        {
            public EdgeMarkView View;
            public VisualElement El;
            public VisualElement Arrow;
        }

        readonly VisualElement layer;
        readonly TooltipLayer tips;
        readonly Action<Vector3> look;
        readonly List<Shown> shown = new List<Shown>();
        string key;

        public EdgeMarks(VisualElement layer, TooltipLayer tips, Action<Vector3> look)
        {
            this.layer = layer;
            this.tips = tips;
            this.look = look;
            // USS has no @keyframes: the alerts fade to 0.65 and back (styles.css pulse, 0.7 s each way; the fade is USS's).
            var dim = false;
            layer.schedule.Execute(() =>
            {
                dim = !dim;
                foreach (var s in shown)
                    if (s.View.Kind == "alert") s.El.EnableInClassList("edge-mark--dim", dim);
            }).Every(700);
        }

        /// <summary>The marks to point at; drawn again only when they change.</summary>
        public void Set(EdgeMarksView view)
        {
            var k = Ui.Key(view);
            if (k == key) return;
            key = k;
            foreach (var s in shown) s.El.RemoveFromHierarchy();
            shown.Clear();
            if (view == null) return;
            foreach (var mark in view.Marks)
            {
                var el = Ui.El("edge-mark panel edge-mark--" + mark.Kind, layer);
                var arrow = new Arrow();
                el.Add(arrow);
                Ui.Text(mark.Label, "edge-mark__label", el);
                var at = mark.World;
                el.RegisterCallback<ClickEvent>(_ => look?.Invoke(at));
                tips.Attach(el, $"Centrar la cámara en: {mark.Name} (O)");
                el.style.display = DisplayStyle.None;
                shown.Add(new Shown { View = mark, El = el, Arrow = arrow });
            }
        }

        /// <summary>Each frame: the arrows for the marks off screen, at the edge towards them, turned to point at them.</summary>
        public void Follow(Camera camera)
        {
            var size = layer.layout.size;
            if (camera == null || float.IsNaN(size.x) || size.x <= 0) return;
            var left = Left;
            var right = size.x - Right;
            var top = Top;
            var bottom = size.y - Bottom;
            var cx = (left + right) / 2;
            var cy = (top + bottom) / 2;
            foreach (var s in shown)
            {
                var vp = camera.WorldToViewportPoint(s.View.World);
                var x = vp.x * size.x;
                var y = (1 - vp.y) * size.y;
                // Behind the camera the projection comes out mirrored.
                var behind = vp.z < 0;
                if (behind)
                {
                    x = 2 * cx - x;
                    y = 2 * cy - y;
                }
                if (!behind && x >= left && x <= right && y >= top && y <= bottom)
                {
                    s.El.style.display = DisplayStyle.None;
                    continue;
                }
                var dx = x - cx;
                var dy = y - cy;
                var tx = dx > 0 ? (right - cx) / dx : dx < 0 ? (left - cx) / dx : float.PositiveInfinity;
                var ty = dy > 0 ? (bottom - cy) / dy : dy < 0 ? (top - cy) / dy : float.PositiveInfinity;
                var t = Mathf.Min(tx, ty);
                s.El.style.display = DisplayStyle.Flex;
                var box = s.El.layout.size;
                var halfW = float.IsNaN(box.x) ? 0 : box.x / 2;
                var halfH = float.IsNaN(box.y) ? 0 : box.y / 2;
                var px = Mathf.Clamp(cx + dx * t, left + halfW, right - halfW);
                var py = Mathf.Clamp(cy + dy * t, top + halfH, bottom - halfH);
                s.El.style.translate = new Translate(px - halfW, py - halfH);
                // The arrow points right: turned as the direction (screen y goes down).
                s.Arrow.style.rotate = new Rotate(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            }
        }

        /// <summary>The arrow (styles.css .edge-arrow, its drawing): a shaft and a head, round ends, pointing right, in the text colour.</summary>
        sealed class Arrow : VisualElement
        {
            public Arrow()
            {
                AddToClassList("edge-mark__arrow");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                if (r.width <= 0 || r.height <= 0) return;
                // Its 24 × 24 drawing fitted to the element, centred.
                var k = Mathf.Min(r.width, r.height) / 24;
                var o = new Vector2(r.x + (r.width - 24 * k) / 2, r.y + (r.height - 24 * k) / 2);
                Vector2 At(float x, float y) => o + new Vector2(x, y) * k;
                var p = ctx.painter2D;
                p.strokeColor = resolvedStyle.color;
                p.lineWidth = 2.8f * k;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(At(3, 12.4f));
                p.BezierCurveTo(At(8.5f, 11.8f), At(14, 12), At(19.6f, 12.6f));
                p.Stroke();
                p.BeginPath();
                p.MoveTo(At(13.2f, 5.6f));
                p.BezierCurveTo(At(15.6f, 7.9f), At(17.7f, 9.9f), At(19.8f, 12.2f));
                p.BezierCurveTo(At(17.6f, 14.3f), At(15.5f, 16.4f), At(13, 18.5f));
                p.Stroke();
            }
        }
    }
}
