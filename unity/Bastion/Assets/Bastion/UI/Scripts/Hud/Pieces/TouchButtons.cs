using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The finger's buttons (docs/IOS.md, phase 2; .dev-snapshots/ui/unity/ios/MENSAJE-A-GAMEPLAY.md §3, Gameplay 7/11):
    /// on the screen what only a key did at the desktop. The menu (Esc) top right, by the mission's card; the map (M) on the
    /// right over the eye (the floors, FloorPicker's: the desktop's too since cuadro 121); and, before «Terminar turno» while the enemy plays, the key that
    /// held plays it faster (Espacio held). In the steel of the eye's and the goal arrow's rings, and of the bar's keys:
    /// nothing new but where they stand. Made only with the finger (HudRoot); the desktop has none of them.
    /// </summary>
    public sealed class TouchButtons
    {
        readonly VisualElement hurry;
        readonly VisualElement search;
        readonly IHudHandlers handlers;
        string key;
        bool hurrying;

        public TouchButtons(VisualElement root, VisualElement endSlot, IHudHandlers handlers, TooltipLayer tips, Func<string, Sprite> icon)
        {
            this.handlers = handlers;
            var menu = Ring(root, "touch-btn--menu", handlers.OnMenu, tips, HudText.TouchButtons.Menu);
            Ui.Icon(icon, "base:menu", "touch-btn__icon", menu);
            var map = Ring(root, "touch-btn--map", handlers.OnMiniMap, tips, HudText.TouchButtons.Map);
            Ui.Icon(icon, "tab:geoscape", "touch-btn__icon", map);
            // Mayús's, with the finger (U11, Arturo 18/11 cuadro 55): the rucksack, a tap on and another off, lit while on.
            search = Ring(root, "touch-btn--search", handlers.OnSearchAll, tips, HudText.TouchButtons.Search);
            Ui.Icon(icon, "search", "touch-btn__icon", search);
            // As the bar's keys, before «Terminar turno»: held, the enemy's turn faster; let go, the chosen speed.
            hurry = new VisualElement();
            hurry.AddToClassList("ability");
            hurry.AddToClassList("touch-hurry");
            Ui.Text("»", "touch-hurry__glyph font-title", hurry).pickingMode = PickingMode.Ignore;
            hurry.RegisterCallback<PointerDownEvent>(_ => Hurry(true));
            hurry.RegisterCallback<PointerUpEvent>(_ => Hurry(false));
            hurry.RegisterCallback<PointerLeaveEvent>(_ => Hurry(false));
            hurry.RegisterCallback<PointerCancelEvent>(_ => Hurry(false));
            endSlot.Insert(0, hurry);
            tips.Attach(hurry, HudText.TouchButtons.Hurry);
            Set(null);
        }

        /// <summary>Which show now: the faster key in the enemy's turn; the rucksack's lit.</summary>
        public void Set(TouchView v)
        {
            var k = Ui.Key(v);
            if (k == key) return;
            key = k;
            search.EnableInClassList("touch-btn--on", v != null && v.Search);
            var shown = v != null && v.Hurry;
            Ui.Show(hurry, shown);
            if (!shown) Hurry(false);
        }

        void Hurry(bool on)
        {
            if (on == hurrying) return;
            hurrying = on;
            hurry.EnableInClassList("ability--active", on);
            handlers.OnHurry(on);
        }

        /// <summary>A round button in the eye's ring: a Button, so that a long press for its help doesn't press it (TooltipLayer).</summary>
        static VisualElement Ring(VisualElement parent, string classes, Action pressed, TooltipLayer tips, string tip)
        {
            var b = new Button(pressed);
            b.AddToClassList("touch-btn");
            b.AddToClassList(classes);
            b.Add(new GoalArrow.Ring());
            parent.Add(b);
            tips.Attach(b, tip);
            return b;
        }
    }
}
