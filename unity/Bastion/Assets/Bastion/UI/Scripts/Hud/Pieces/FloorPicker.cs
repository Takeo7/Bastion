using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The floors' picker (cuadro 121, Arturo 22/11: «las dos»): low on the right, over the eye and the goal's arrow, ▲, the
    /// floor looked at («Planta baja», «Planta 1») and ▼, each a click (RePág, AvPág). Always says which floor is shown, and
    /// only on a map with more than one. The same piece at the desktop and with the finger (it was TouchButtons' up and down):
    /// the rings of the eye's and the goal arrow's steel.
    /// </summary>
    public sealed class FloorPicker
    {
        readonly VisualElement box;
        readonly Button up;
        readonly Button down;
        readonly Label name;
        string key;

        public FloorPicker(VisualElement root, IHudHandlers handlers, TooltipLayer tips)
        {
            box = new VisualElement { name = "floors", pickingMode = PickingMode.Ignore };
            box.AddToClassList("floors");
            up = Ring(box, "floors__up", "▲", () => handlers.OnFloor(1), tips, HudText.Floors.UpTip);
            name = Ui.Text("", "floors__name font-title", box);
            name.pickingMode = PickingMode.Ignore;
            down = Ring(box, "floors__down", "▼", () => handlers.OnFloor(-1), tips, HudText.Floors.DownTip);
            root.Add(box);
            Set(null);
        }

        /// <summary>The floor looked at and where it can go; null or a single floor, none shown.</summary>
        public void Set(FloorView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            Ui.Show(box, v != null);
            if (v == null) return;
            name.text = HudText.Floors.Name(v.Level).ToUpperInvariant();
            up.SetEnabled(v.CanUp);
            down.SetEnabled(v.CanDown);
        }

        /// <summary>A round key in the eye's ring: a Button, so that a long press for its help doesn't press it (TooltipLayer).</summary>
        static Button Ring(VisualElement parent, string classes, string glyph, Action pressed, TooltipLayer tips, string tip)
        {
            var b = new Button(pressed);
            b.AddToClassList("floors__key");
            b.AddToClassList(classes);
            b.Add(new GoalArrow.Ring());
            Ui.Text(glyph, "floors__glyph", b).pickingMode = PickingMode.Ignore;
            parent.Add(b);
            tips.Attach(b, tip);
            return b;
        }
    }
}
