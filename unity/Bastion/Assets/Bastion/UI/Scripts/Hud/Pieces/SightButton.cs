using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The enemies' sight, bottom right by the goal's arrow (Arturo, 5/11 #6): the arrow's black ring (Ana, 7/11 #4) with
    /// an eye in it that stays on once pressed (V too): every enemy in sight's field of view on the ground at once, and an
    /// open eye over those that see the picked soldier now. What the red means is in its help, no legend over it (Ana,
    /// 7/11 #10: «Lo que ven» went).
    /// </summary>
    public sealed class SightButton
    {
        readonly VisualElement host;
        readonly VisualElement button;
        string key;

        /// <summary>The button pressed.</summary>
        public event Action Toggled;

        public SightButton(VisualElement host, TooltipLayer tips)
        {
            this.host = host;
            host.pickingMode = PickingMode.Ignore;
            button = Ui.El("sight__button", host);
            button.Add(new GoalArrow.Ring());
            button.Add(new Eye());
            button.RegisterCallback<ClickEvent>(_ => Toggled?.Invoke());
            tips.Attach(button, HudText.Sight.Tip);
            Ui.Show(host, false);
        }

        public void Set(SightView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            Ui.Show(host, v != null && v.Button);
            if (v == null) return;
            button.EnableInClassList("sight__button--on", v.On);
            // Its colour is the class's: the eye drawn again (its own resolvedStyle).
            button.Q<Eye>()?.MarkDirtyRepaint();
        }

        /// <summary>The button's eye: an almond and its pupil, grey off and white on (Flags.uss's eye, larger).</summary>
        sealed class Eye : VisualElement
        {
            public Eye()
            {
                AddToClassList("sight__eye");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
                RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                if (r.width <= 0 || r.height <= 0) return;
                var k = Mathf.Min(r.width / 16, r.height / 10);
                var o = new Vector2(r.x + (r.width - 16 * k) / 2, r.y + (r.height - 10 * k) / 2);
                Vector2 At(float x, float y) => o + new Vector2(x, y) * k;
                var p = ctx.painter2D;
                p.strokeColor = p.fillColor = resolvedStyle.color;
                p.lineWidth = 1.5f * k;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(At(0.8f, 5));
                p.BezierCurveTo(At(4, 0.2f), At(12, 0.2f), At(15.2f, 5));
                p.BezierCurveTo(At(12, 9.8f), At(4, 9.8f), At(0.8f, 5));
                p.ClosePath();
                p.Stroke();
                p.BeginPath();
                p.Arc(At(8, 5), 2.3f * k, Angle.Degrees(0), Angle.Degrees(360));
                p.Fill();
            }
        }
    }
}
