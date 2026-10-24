using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Hud
{
    /// <summary>
    /// The help on hover, which the web gave with `title`: UI Toolkit shows `tooltip` only in the editor, never in the
    /// game (docs/UNITY-UI.md §4). One layer over the HUD: an element gets its words (or a card built on demand, as the
    /// ability's), shown a moment after the pointer enters it, above it and kept inside the screen, gone as it leaves.
    /// </summary>
    public sealed class TooltipLayer
    {
        const long DelayMs = 350;
        const float Gap = 6;
        const string HookedClass = "has-tip";

        readonly VisualElement layer;
        readonly VisualElement box;
        readonly Dictionary<VisualElement, Func<VisualElement>> cards = new Dictionary<VisualElement, Func<VisualElement>>();
        readonly Dictionary<VisualElement, string> texts = new Dictionary<VisualElement, string>();
        VisualElement over;
        IVisualElementScheduledItem pending;

        public TooltipLayer(VisualElement root)
        {
            layer = Ui.El("tooltip-layer", root);
            layer.pickingMode = PickingMode.Ignore;
            box = Ui.El("tooltip", layer);
            box.pickingMode = PickingMode.Ignore;
            Ui.Show(box, false);
        }

        /// <summary>Back over everything (a layer added after it, as the game's menu).</summary>
        public void Front() => layer.BringToFront();

        /// <summary>`target` says `text` on hover (empty: nothing). Called again, it replaces the words.</summary>
        public void Attach(VisualElement target, string text)
        {
            cards.Remove(target);
            if (string.IsNullOrEmpty(text))
            {
                texts.Remove(target);
                return;
            }
            Hook(target);
            texts[target] = text;
            if (over == target) Fill();
        }

        /// <summary>`target` shows the card `build` makes on hover (the ability's: name, cost, figures, why not).</summary>
        public void Attach(VisualElement target, Func<VisualElement> build)
        {
            texts.Remove(target);
            Hook(target);
            cards[target] = build;
            if (over == target) Fill();
        }

        public void Hide()
        {
            pending?.Pause();
            over = null;
            Ui.Show(box, false);
        }

        /// <summary>Its callbacks once (marked on the element itself, so nothing here keeps a dropped element).</summary>
        void Hook(VisualElement target)
        {
            if (target.ClassListContains(HookedClass)) return;
            target.AddToClassList(HookedClass);
            target.RegisterCallback<PointerEnterEvent>(_ => Enter(target));
            target.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (over == target) Hide();
            });
            target.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (over == target) Hide();
                texts.Remove(target);
                cards.Remove(target);
            });
        }

        void Enter(VisualElement target)
        {
            over = target;
            pending?.Pause();
            pending = layer.schedule.Execute(() =>
            {
                if (over != target) return;
                Fill();
            }).StartingIn(DelayMs);
        }

        void Fill()
        {
            box.Clear();
            if (over == null) return;
            if (cards.TryGetValue(over, out var build)) box.Add(build());
            else if (texts.TryGetValue(over, out var text)) Ui.Text(text, "tooltip__text", box);
            else return;
            Ui.Show(box, true);
            // Placed once its size is known: above the target, centred on it, inside the screen; below if no room above.
            box.style.visibility = Visibility.Hidden;
            box.schedule.Execute(Place);
        }

        void Place()
        {
            if (over == null) return;
            var t = over.worldBound;
            var size = box.layout.size;
            var screen = layer.layout;
            var x = Mathf.Clamp(t.center.x - size.x / 2, Gap, Mathf.Max(Gap, screen.width - size.x - Gap));
            var y = t.yMin - size.y - Gap;
            if (y < Gap) y = t.yMax + Gap;
            box.style.left = x;
            box.style.top = y;
            box.style.visibility = Visibility.Visible;
        }
    }
}
